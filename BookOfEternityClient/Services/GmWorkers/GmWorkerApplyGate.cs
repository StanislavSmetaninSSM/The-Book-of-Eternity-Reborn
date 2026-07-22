using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using System.Security.Cryptography;
using System.Text;

namespace BookOfEternityClient.Services.GmWorkers;

public sealed class GmWorkerApplyGate
{
    private readonly FileSystemManager _fs;
    private readonly Func<Task<IReadOnlyList<ValidationIssue>>> _validateGameStateAsync;
    private readonly GmWorkerAuditLog? _auditLog;

    public GmWorkerApplyGate(
        FileSystemManager fs,
        Func<Task<IReadOnlyList<ValidationIssue>>>? validateGameStateAsync = null,
        GmWorkerAuditLog? auditLog = null)
    {
        _fs = fs;
        _validateGameStateAsync = validateGameStateAsync ?? (() => Task.FromResult<IReadOnlyList<ValidationIssue>>([]));
        _auditLog = auditLog;
    }

    public async Task<ApplyGateDecision> ApplyAsync(
        WorkerProposal proposal,
        WorkerTaskPacket task,
        WorkerBridgeProfile profile)
    {
        var checkedPaths = proposal.ChangedFiles.Select(file => file.Path).ToArray();
        var proposalValidation = GmWorkerContractValidator.ValidateProposal(proposal, task, profile);
        if (!proposalValidation.IsValid)
        {
            var decision = BuildDecision(
                proposal.ProposalId,
                ApplyGateResult.Rejected,
                checkedPaths,
                scopePassed: false,
                violations: proposalValidation.Errors,
                validationRequired: profile.Permissions.RequiresValidation,
                validationPassed: false,
                issueCount: 0,
                appliedFiles: [],
                rejectionReasons: proposalValidation.Errors);
            await RecordDecisionAsync(proposal, decision);
            return decision;
        }

        if (proposal.Status != WorkerProposalStatus.Completed)
        {
            var rejectionReasons = new[] { "Only completed worker proposals can enter the apply gate." };
            var decision = BuildDecision(
                proposal.ProposalId,
                ApplyGateResult.Rejected,
                checkedPaths,
                scopePassed: false,
                violations: rejectionReasons,
                validationRequired: profile.Permissions.RequiresValidation,
                validationPassed: false,
                issueCount: 0,
                appliedFiles: [],
                rejectionReasons: rejectionReasons);
            await RecordDecisionAsync(proposal, decision);
            return decision;
        }

        var capturedContents = new Dictionary<string, byte[]>(GmWorkerContractValidator.CanonicalPathComparer);
        var contentErrors = await VerifyProposalContentRefsAsync(proposal, capturedContents);
        if (contentErrors.Count > 0)
        {
            var decision = BuildDecision(
                proposal.ProposalId,
                ApplyGateResult.Rejected,
                checkedPaths,
                scopePassed: false,
                violations: contentErrors,
                validationRequired: profile.Permissions.RequiresValidation,
                validationPassed: false,
                issueCount: 0,
                appliedFiles: [],
                rejectionReasons: contentErrors);
            await RecordDecisionAsync(proposal, decision);
            return decision;
        }

        ApplyGateDecision transactionDecision;
        await using (var writeLease = await _fs.AcquireCanonicalWriteLeaseAsync())
        {
            transactionDecision = await ApplyWithinCanonicalLeaseAsync(
                proposal,
                task,
                profile,
                checkedPaths,
                capturedContents,
                writeLease);
        }

        await RecordDecisionAsync(proposal, transactionDecision);
        return transactionDecision;
    }

    private async Task<ApplyGateDecision> ApplyWithinCanonicalLeaseAsync(
        WorkerProposal proposal,
        WorkerTaskPacket task,
        WorkerBridgeProfile profile,
        IReadOnlyList<string> checkedPaths,
        IReadOnlyDictionary<string, byte[]> capturedContents,
        FileSystemManager.CanonicalWriteLease writeLease)
    {
        var (contextErrors, baselines) = await CaptureAndVerifyTaskContextAsync(proposal, task);
        if (contextErrors.Count > 0)
        {
            return BuildDecision(
                proposal.ProposalId,
                ApplyGateResult.Rejected,
                checkedPaths,
                scopePassed: false,
                violations: contextErrors,
                validationRequired: profile.Permissions.RequiresValidation,
                validationPassed: false,
                issueCount: 0,
                appliedFiles: [],
                rejectionReasons: contextErrors);
        }

        var preservationErrors = VerifyAfterlifeRealmAuthority(task, baselines)
            .Concat(VerifyActorMaterializationRepairPreservation(
                proposal,
                task,
                capturedContents,
                baselines))
            .ToArray();
        if (preservationErrors.Length > 0)
        {
            return BuildDecision(
                proposal.ProposalId,
                ApplyGateResult.Rejected,
                checkedPaths,
                scopePassed: false,
                violations: preservationErrors,
                validationRequired: profile.Permissions.RequiresValidation,
                validationPassed: false,
                issueCount: 0,
                appliedFiles: [],
                rejectionReasons: preservationErrors);
        }

        var rollback = new List<RollbackEntry>();
        var appliedFiles = new List<string>();
        try
        {
            foreach (var changedFile in proposal.ChangedFiles)
            {
                baselines.TryGetValue(changedFile.Path, out var baselineBytes);
                var appliedBytes = changedFile.ChangeKind == WorkerFileChangeKind.Delete
                    ? null
                    : capturedContents[changedFile.Path];
                var mutationResult = await _fs.CompareExchangeFileBytesAsync(
                    writeLease,
                    changedFile.Path,
                    baselineBytes,
                    appliedBytes);
                if (mutationResult == CanonicalFileMutationResult.Conflict)
                {
                    var rollbackErrors = await RollbackOwnedChangesAsync(writeLease, rollback);
                    var conflict = $"canonical file changed concurrently before worker apply: {changedFile.Path}.";
                    var rejectionReasons = new[] { conflict }.Concat(rollbackErrors).ToArray();
                    return BuildDecision(
                        proposal.ProposalId,
                        ApplyGateResult.Rejected,
                        checkedPaths,
                        scopePassed: false,
                        violations: rejectionReasons,
                        validationRequired: profile.Permissions.RequiresValidation,
                        validationPassed: false,
                        issueCount: 0,
                        appliedFiles: [],
                        rejectionReasons: rejectionReasons);
                }

                rollback.Add(new RollbackEntry(changedFile.Path, baselineBytes, appliedBytes));
                appliedFiles.Add(changedFile.Path);
            }

            var validationIssues = profile.Permissions.RequiresValidation
                ? await _validateGameStateAsync()
                : [];
            if (validationIssues.Count > 0)
            {
                var rollbackErrors = await RollbackOwnedChangesAsync(writeLease, rollback);
                var rejectionReasons = validationIssues
                    .Select(issue => issue.ToString())
                    .Concat(rollbackErrors)
                    .ToArray();
                return BuildDecision(
                    proposal.ProposalId,
                    ApplyGateResult.ValidationFailed,
                    checkedPaths,
                    scopePassed: true,
                    violations: [],
                    validationRequired: true,
                    validationPassed: false,
                    issueCount: validationIssues.Count,
                    appliedFiles: [],
                    rejectionReasons: rejectionReasons);
            }

            var ownershipErrors = (await VerifyAppliedFilesRemainOwnedAsync(rollback))
                .Concat(await VerifyReadOnlyTaskContextRemainsOwnedAsync(task, proposal, baselines))
                .ToArray();
            if (ownershipErrors.Length > 0)
            {
                var rollbackErrors = await RollbackOwnedChangesAsync(writeLease, rollback);
                var rejectionReasons = ownershipErrors.Concat(rollbackErrors).ToArray();
                return BuildDecision(
                    proposal.ProposalId,
                    ApplyGateResult.ValidationFailed,
                    checkedPaths,
                    scopePassed: true,
                    violations: rejectionReasons,
                    validationRequired: profile.Permissions.RequiresValidation,
                    validationPassed: false,
                    issueCount: ownershipErrors.Length,
                    appliedFiles: [],
                    rejectionReasons: rejectionReasons);
            }

            return BuildDecision(
                proposal.ProposalId,
                ApplyGateResult.Accepted,
                checkedPaths,
                scopePassed: true,
                violations: [],
                validationRequired: profile.Permissions.RequiresValidation,
                validationPassed: true,
                issueCount: 0,
                appliedFiles: appliedFiles,
                rejectionReasons: []);
        }
        catch
        {
            await RollbackOwnedChangesAsync(writeLease, rollback);
            throw;
        }
    }

    private async Task<IReadOnlyList<string>> VerifyProposalContentRefsAsync(
        WorkerProposal proposal,
        IDictionary<string, byte[]> capturedContents)
    {
        var errors = new List<string>();
        foreach (var changedFile in proposal.ChangedFiles)
        {
            if (changedFile.ChangeKind == WorkerFileChangeKind.Delete)
                continue;
            if (string.IsNullOrWhiteSpace(changedFile.ContentRef))
            {
                errors.Add($"changedFiles contentRef is required for {changedFile.Path}.");
                continue;
            }

            var content = await _fs.ReadFileBytesAsync(changedFile.ContentRef);
            if (content == null)
            {
                errors.Add($"changedFiles contentRef does not exist: {changedFile.ContentRef}");
                continue;
            }

            var actualSha256 = ComputeSha256(content);
            if (!string.IsNullOrWhiteSpace(changedFile.AfterSha256) &&
                !string.Equals(actualSha256, changedFile.AfterSha256, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"changedFiles contentRef hash changed for {changedFile.Path}.");
                continue;
            }

            capturedContents[changedFile.Path] = content;
        }

        return errors;
    }

    private async Task<(IReadOnlyList<string> Errors, IReadOnlyDictionary<string, byte[]?> Baselines)>
        CaptureAndVerifyTaskContextAsync(
            WorkerProposal proposal,
            WorkerTaskPacket task)
    {
        var errors = new List<string>();
        var baselines = new Dictionary<string, byte[]?>(GmWorkerContractValidator.CanonicalPathComparer);
        var contextByPath = task.ContextFiles
            .GroupBy(file => file.Path, GmWorkerContractValidator.CanonicalPathComparer)
            .ToDictionary(group => group.Key, group => group.First(), GmWorkerContractValidator.CanonicalPathComparer);

        foreach (var contextFile in task.ContextFiles)
        {
            var content = await _fs.ReadFileBytesAsync(contextFile.Path);
            baselines[contextFile.Path] = content;
            var actualSha256 = content == null ? MissingFileSha256 : ComputeSha256(content);
            if (!string.Equals(actualSha256, contextFile.Sha256, StringComparison.OrdinalIgnoreCase))
                errors.Add($"task context changed since dispatch: {contextFile.Path}.");
        }

        foreach (var changedFile in proposal.ChangedFiles)
        {
            if (!contextByPath.TryGetValue(changedFile.Path, out var contextFile))
            {
                errors.Add($"changedFiles path is not pinned by task.contextFiles: {changedFile.Path}.");
                continue;
            }

            if (!string.IsNullOrWhiteSpace(changedFile.BeforeSha256) &&
                !string.Equals(changedFile.BeforeSha256, contextFile.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"changedFiles beforeSha256 does not match task context for {changedFile.Path}.");
            }
        }

        return (errors, baselines);
    }

    private static IReadOnlyList<string> VerifyActorMaterializationRepairPreservation(
        WorkerProposal proposal,
        WorkerTaskPacket task,
        IReadOnlyDictionary<string, byte[]> capturedContents,
        IReadOnlyDictionary<string, byte[]?> baselines)
    {
        if (task.TaskType != WorkerTaskType.ValidationRepair)
            return [];

        baselines.TryGetValue(MortalCharacteristicAuthorityContract.StatePath, out var characteristicAuthorityBytes);
        var characteristicAuthority = DecodeUtf8(characteristicAuthorityBytes);
        var errors = new List<string>();
        foreach (var changedFile in proposal.ChangedFiles)
        {
            baselines.TryGetValue(changedFile.Path, out var baselineBytes);
            var baseline = DecodeUtf8(baselineBytes);
            var proposed = changedFile.ChangeKind == WorkerFileChangeKind.Delete
                ? null
                : DecodeUtf8(capturedContents[changedFile.Path]);
            errors.AddRange(ActorMaterializationRepairPreservationGuard.Validate(
                changedFile.Path,
                baseline,
                proposed,
                task.ValidationIssues,
                characteristicAuthority));
        }

        return errors;
    }

    private static IReadOnlyList<string> VerifyAfterlifeRealmAuthority(
        WorkerTaskPacket task,
        IReadOnlyDictionary<string, byte[]?> baselines)
    {
        if (task.TaskType != WorkerTaskType.ValidationRepair || task.AfterlifeContract == null)
            return [];

        baselines.TryGetValue(AfterlifeRealmAuthorityContract.StatePath, out var authorityBytes);
        if (!AfterlifeRealmAuthorityContract.TryRead(
                DecodeUtf8(authorityBytes),
                out var realmGate,
                out var currentRealm,
                out var error))
        {
            return [error];
        }

        if (task.AfterlifeContract.RealmGate != realmGate ||
            !string.Equals(task.AfterlifeContract.CurrentRealm, currentRealm, StringComparison.Ordinal))
        {
            return
            [
                $"Afterlife repair realm contract does not match pinned realm authority in {AfterlifeRealmAuthorityContract.StatePath}."
            ];
        }

        return [];
    }

    private async Task<IReadOnlyList<string>> VerifyAppliedFilesRemainOwnedAsync(
        IReadOnlyList<RollbackEntry> rollback)
    {
        var errors = new List<string>();
        foreach (var entry in rollback)
        {
            var current = await _fs.ReadFileBytesAsync(entry.Path);
            if (!ExactBytesEqual(current, entry.AppliedBytes))
                errors.Add($"canonical file changed concurrently after worker apply: {entry.Path}.");
        }

        return errors;
    }

    private async Task<IReadOnlyList<string>> VerifyReadOnlyTaskContextRemainsOwnedAsync(
        WorkerTaskPacket task,
        WorkerProposal proposal,
        IReadOnlyDictionary<string, byte[]?> baselines)
    {
        var changedPaths = proposal.ChangedFiles
            .Select(file => file.Path)
            .ToHashSet(GmWorkerContractValidator.CanonicalPathComparer);
        var errors = new List<string>();
        foreach (var contextFile in task.ContextFiles)
        {
            if (changedPaths.Contains(contextFile.Path))
                continue;

            baselines.TryGetValue(contextFile.Path, out var baseline);
            var current = await _fs.ReadFileBytesAsync(contextFile.Path);
            if (!ExactBytesEqual(current, baseline))
                errors.Add($"read-only task context changed during worker apply: {contextFile.Path}.");
        }

        return errors;
    }

    private async Task<IReadOnlyList<string>> RollbackOwnedChangesAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        IReadOnlyList<RollbackEntry> rollback)
    {
        var errors = new List<string>();
        foreach (var entry in rollback.AsEnumerable().Reverse())
        {
            var result = await _fs.CompareExchangeFileBytesAsync(
                writeLease,
                entry.Path,
                entry.AppliedBytes,
                entry.BaselineBytes);
            if (result == CanonicalFileMutationResult.Conflict)
                errors.Add($"rollback conflict preserved a newer canonical write: {entry.Path}.");
        }

        return errors;
    }

    private static string? DecodeUtf8(byte[]? content)
    {
        if (content == null)
            return null;
        var text = Encoding.UTF8.GetString(content);
        return text.Length > 0 && text[0] == '\ufeff' ? text[1..] : text;
    }

    private static string ComputeSha256(byte[] content) =>
        Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();

    private static bool ExactBytesEqual(byte[]? left, byte[]? right) =>
        left == null ? right == null : right != null && left.AsSpan().SequenceEqual(right);

    private static ApplyGateDecision BuildDecision(
        string proposalId,
        ApplyGateResult result,
        IReadOnlyList<string> checkedPaths,
        bool scopePassed,
        IReadOnlyList<string> violations,
        bool validationRequired,
        bool validationPassed,
        int issueCount,
        IReadOnlyList<string> appliedFiles,
        IReadOnlyList<string> rejectionReasons) =>
        new()
        {
            DecisionId = "apply_decision_" + DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfff"),
            ProposalId = proposalId,
            Result = result,
            ScopeCheck = new ApplyGateScopeCheck
            {
                Passed = scopePassed,
                CheckedPaths = checkedPaths,
                Violations = violations
            },
            ValidationCheck = new ApplyGateValidationCheck
            {
                Required = validationRequired,
                Passed = validationPassed,
                Command = validationRequired ? "ValidateGameStateAsync" : "",
                IssueCount = issueCount
            },
            AppliedFiles = appliedFiles,
            RejectionReasons = rejectionReasons,
            DecidedAtUtc = DateTimeOffset.UtcNow.ToString("O")
        };

    private Task RecordDecisionAsync(WorkerProposal proposal, ApplyGateDecision decision) =>
        _auditLog?.RecordApplyDecisionAsync(proposal, decision) ?? Task.CompletedTask;

    private const string MissingFileSha256 = "missing";

    private sealed record RollbackEntry(string Path, byte[]? BaselineBytes, byte[]? AppliedBytes);
}
