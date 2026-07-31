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
        ValidationService validationService,
        GmWorkerAuditLog? auditLog = null)
        : this(
            GetProductionFileSystem(validationService),
            CreateProductionValidator(validationService),
            auditLog)
    {
    }

    internal GmWorkerApplyGate(
        FileSystemManager fs,
        Func<Task<IReadOnlyList<ValidationIssue>>> validateGameStateAsync,
        GmWorkerAuditLog? auditLog = null)
    {
        _fs = fs ?? throw new ArgumentNullException(nameof(fs));
        _validateGameStateAsync = validateGameStateAsync ??
            throw new ArgumentNullException(nameof(validateGameStateAsync));
        _auditLog = auditLog;
    }

    private static FileSystemManager GetProductionFileSystem(
        ValidationService validationService)
    {
        ArgumentNullException.ThrowIfNull(validationService);
        return validationService.CanonicalFileSystem;
    }

    private static Func<Task<IReadOnlyList<ValidationIssue>>> CreateProductionValidator(
        ValidationService validationService)
    {
        ArgumentNullException.ThrowIfNull(validationService);
        return async () =>
            (IReadOnlyList<ValidationIssue>)await validationService.ValidateGameStateAsync();
    }

    internal async Task<ApplyGateDecision> ApplyReservedAsync(
        WorkerProposal proposal,
        WorkerBridgeProfile profile,
        string expectedSessionGeneration)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedSessionGeneration);
        var checkedPaths = proposal.ChangedFiles.Select(file => file.Path).ToArray();
        ApplyGateDecision decision;
        await using (var writeLease = await _fs.AcquireCanonicalWriteLeaseAsync())
        {
            if (!IsSafeIdentifier(proposal.TaskId))
            {
                decision = BuildRejectedDecision(
                    proposal,
                    profile,
                    checkedPaths,
                    "Worker proposal taskId is unsafe.");
            }
            else
            {
                var taskPath = GmWorkerBridgePool.GetTaskPacketPath(proposal.TaskId);
                var taskBytes = await _fs.ReadFileBytesAsync(writeLease, taskPath);
                if (taskBytes == null)
                {
                    decision = !_fs.IsCurrentSessionGeneration(writeLease, expectedSessionGeneration)
                        ? BuildSessionReplacedDecision(proposal, profile, checkedPaths)
                        : BuildRejectedDecision(
                            proposal,
                            profile,
                            checkedPaths,
                            $"Canonical worker task reservation is missing: {taskPath}.");
                }
                else
                {
                    WorkerTaskPacket? reservedTask;
                    try
                    {
                        reservedTask = GmWorkerJson.Deserialize<WorkerTaskPacket>(DecodeUtf8(taskBytes)!);
                    }
                    catch (Exception ex) when (ex is InvalidDataException or System.Text.Json.JsonException)
                    {
                        reservedTask = null;
                        decision = BuildRejectedDecision(
                            proposal,
                            profile,
                            checkedPaths,
                            $"Canonical worker task reservation is malformed: {ex.Message}");
                    }

                    if (reservedTask == null)
                    {
                        decision = BuildRejectedDecision(
                            proposal,
                            profile,
                            checkedPaths,
                            "Canonical worker task reservation is empty.");
                    }
                    else
                    {
                        decision = !string.Equals(
                                reservedTask.SessionGeneration,
                                expectedSessionGeneration,
                                StringComparison.Ordinal)
                            ? BuildSessionReplacedDecision(
                                proposal,
                                profile,
                                checkedPaths)
                            : await ApplyAuthoritativeTaskWithinCanonicalLeaseAsync(
                                proposal,
                                reservedTask,
                                profile,
                                checkedPaths,
                                writeLease);
                    }
                }
            }
            if (decision.Result != ApplyGateResult.SessionReplaced && _auditLog != null)
                await _auditLog.RecordApplyDecisionAsync(writeLease, proposal, decision);
        }

        return decision;
    }

    private async Task<ApplyGateDecision> ApplyAuthoritativeTaskWithinCanonicalLeaseAsync(
        WorkerProposal proposal,
        WorkerTaskPacket task,
        WorkerBridgeProfile profile,
        IReadOnlyList<string> checkedPaths,
        FileSystemManager.CanonicalWriteLease writeLease)
    {
        var proposalValidation = GmWorkerContractValidator.ValidateProposal(proposal, task, profile);
        if (!proposalValidation.IsValid)
        {
            return BuildDecision(
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
        }

        if (proposal.Status != WorkerProposalStatus.Completed)
        {
            var rejectionReasons = new[] { "Only completed worker proposals can enter the apply gate." };
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

        var capturedContents = new Dictionary<string, byte[]>(GmWorkerContractValidator.CanonicalPathComparer);
        var contentErrors = await VerifyProposalContentRefsAsync(proposal, capturedContents, writeLease);
        if (contentErrors.Count > 0)
        {
            return BuildDecision(
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
        }

        if (!_fs.IsCurrentSessionGeneration(writeLease, task.SessionGeneration))
        {
            var generationErrors = new[]
            {
                "Worker task does not belong to the current game session generation."
            };
            return BuildDecision(
                proposal.ProposalId,
                ApplyGateResult.SessionReplaced,
                checkedPaths,
                scopePassed: false,
                violations: generationErrors,
                validationRequired: profile.Permissions.RequiresValidation,
                validationPassed: false,
                issueCount: 0,
                appliedFiles: [],
                rejectionReasons: generationErrors);
        }

        return await ApplyWithinCanonicalLeaseAsync(
            proposal,
            task,
            profile,
            checkedPaths,
            capturedContents,
            writeLease);
    }

    private async Task<ApplyGateDecision> ApplyWithinCanonicalLeaseAsync(
        WorkerProposal proposal,
        WorkerTaskPacket task,
        WorkerBridgeProfile profile,
        IReadOnlyList<string> checkedPaths,
        IReadOnlyDictionary<string, byte[]> capturedContents,
        FileSystemManager.CanonicalWriteLease writeLease)
    {
        var (contextErrors, baselines) = await CaptureAndVerifyTaskContextAsync(
            proposal,
            task,
            writeLease);
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

        var rollback = proposal.ChangedFiles
            .Select(changedFile =>
            {
                baselines.TryGetValue(changedFile.Path, out var baselineBytes);
                var appliedBytes = changedFile.ChangeKind == WorkerFileChangeKind.Delete
                    ? null
                    : capturedContents[changedFile.Path];
                return new RollbackEntry(changedFile.Path, baselineBytes, appliedBytes);
            })
            .ToList();
        var appliedFiles = new List<string>();
        CanonicalWorkerApplyTransaction? durableTransaction = null;
        try
        {
            if (rollback.Count > 0)
            {
                durableTransaction = await _fs.BeginWorkerApplyTransactionAsync(
                    writeLease,
                    rollback.Select(entry => new CanonicalWorkerApplyChange(
                        entry.Path,
                        entry.BaselineBytes,
                        entry.AppliedBytes)).ToArray());
            }

            foreach (var entry in rollback)
            {
                var mutationResult = await _fs.CompareExchangeFileBytesAsync(
                    writeLease,
                    entry.Path,
                    entry.BaselineBytes,
                    entry.AppliedBytes);
                if (mutationResult == CanonicalFileMutationResult.Conflict)
                {
                    var rollbackErrors = await RollbackDurableTransactionAsync(writeLease, durableTransaction);
                    var conflict = $"canonical file changed concurrently before worker apply: {entry.Path}.";
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

                appliedFiles.Add(entry.Path);
            }

            var validationIssues = profile.Permissions.RequiresValidation
                ? await _validateGameStateAsync()
                : [];
            if (validationIssues.Count > 0)
            {
                var rollbackErrors = await RollbackDurableTransactionAsync(writeLease, durableTransaction);
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

            var ownershipErrors = (await VerifyAppliedFilesRemainOwnedAsync(rollback, writeLease))
                .Concat(await VerifyReadOnlyTaskContextRemainsOwnedAsync(
                    task,
                    proposal,
                    baselines,
                    writeLease))
                .ToArray();
            if (ownershipErrors.Length > 0)
            {
                var rollbackErrors = await RollbackDurableTransactionAsync(writeLease, durableTransaction);
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

            if (durableTransaction != null)
                _fs.CommitWorkerApplyTransaction(writeLease, durableTransaction);

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
            await RollbackDurableTransactionAsync(writeLease, durableTransaction);
            throw;
        }
    }

    private async Task<IReadOnlyList<string>> VerifyProposalContentRefsAsync(
        WorkerProposal proposal,
        IDictionary<string, byte[]> capturedContents,
        FileSystemManager.CanonicalWriteLease writeLease)
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

            var content = await _fs.ReadFileBytesAsync(writeLease, changedFile.ContentRef);
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
            WorkerTaskPacket task,
            FileSystemManager.CanonicalWriteLease writeLease)
    {
        var errors = new List<string>();
        var baselines = new Dictionary<string, byte[]?>(GmWorkerContractValidator.CanonicalPathComparer);
        var contextByPath = task.ContextFiles
            .GroupBy(file => file.Path, GmWorkerContractValidator.CanonicalPathComparer)
            .ToDictionary(group => group.Key, group => group.First(), GmWorkerContractValidator.CanonicalPathComparer);

        foreach (var contextFile in task.ContextFiles)
        {
            var content = await _fs.ReadFileBytesAsync(writeLease, contextFile.Path);
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
        IReadOnlyList<RollbackEntry> rollback,
        FileSystemManager.CanonicalWriteLease writeLease)
    {
        var errors = new List<string>();
        foreach (var entry in rollback)
        {
            var current = await _fs.ReadFileBytesAsync(writeLease, entry.Path);
            if (!ExactBytesEqual(current, entry.AppliedBytes))
                errors.Add($"canonical file changed concurrently after worker apply: {entry.Path}.");
        }

        return errors;
    }

    private async Task<IReadOnlyList<string>> VerifyReadOnlyTaskContextRemainsOwnedAsync(
        WorkerTaskPacket task,
        WorkerProposal proposal,
        IReadOnlyDictionary<string, byte[]?> baselines,
        FileSystemManager.CanonicalWriteLease writeLease)
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
            var current = await _fs.ReadFileBytesAsync(writeLease, contextFile.Path);
            if (!ExactBytesEqual(current, baseline))
                errors.Add($"read-only task context changed during worker apply: {contextFile.Path}.");
        }

        return errors;
    }

    private async Task<IReadOnlyList<string>> RollbackDurableTransactionAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        CanonicalWorkerApplyTransaction? transaction)
    {
        if (transaction == null)
            return [];
        var errors = await _fs.RollbackWorkerApplyTransactionAsync(writeLease, transaction);
        return errors
            .Select(error => $"rollback conflict or recovery failure: {error}")
            .ToArray();
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

    private static bool IsSafeIdentifier(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.All(ch => char.IsLower(ch) || char.IsDigit(ch) || ch is '_' or '-');

    private static ApplyGateDecision BuildRejectedDecision(
        WorkerProposal proposal,
        WorkerBridgeProfile profile,
        IReadOnlyList<string> checkedPaths,
        string reason) =>
        BuildDecision(
            proposal.ProposalId,
            ApplyGateResult.Rejected,
            checkedPaths,
            scopePassed: false,
            violations: [reason],
            validationRequired: profile.Permissions.RequiresValidation,
            validationPassed: false,
            issueCount: 0,
            appliedFiles: [],
            rejectionReasons: [reason]);

    private static ApplyGateDecision BuildSessionReplacedDecision(
        WorkerProposal proposal,
        WorkerBridgeProfile profile,
        IReadOnlyList<string> checkedPaths)
    {
        const string reason = "Worker task does not belong to the current game session generation.";
        return BuildDecision(
            proposal.ProposalId,
            ApplyGateResult.SessionReplaced,
            checkedPaths,
            scopePassed: false,
            violations: [reason],
            validationRequired: profile.Permissions.RequiresValidation,
            validationPassed: false,
            issueCount: 0,
            appliedFiles: [],
            rejectionReasons: [reason]);
    }

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

    private const string MissingFileSha256 = "missing";

    private sealed record RollbackEntry(string Path, byte[]? BaselineBytes, byte[]? AppliedBytes);
}
