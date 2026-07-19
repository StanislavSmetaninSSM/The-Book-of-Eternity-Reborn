using BookOfEternityClient.Core;
using BookOfEternityClient.Services;

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

        var contentErrors = await VerifyProposalContentRefsAsync(proposal);
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

        var preservationErrors = await VerifyActorMaterializationRepairPreservationAsync(proposal, task);
        if (preservationErrors.Count > 0)
        {
            var decision = BuildDecision(
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
            await RecordDecisionAsync(proposal, decision);
            return decision;
        }

        var rollback = new List<RollbackEntry>();
        var appliedFiles = new List<string>();
        try
        {
            foreach (var changedFile in proposal.ChangedFiles)
            {
                rollback.Add(new RollbackEntry(
                    changedFile.Path,
                    _fs.FileExists(changedFile.Path),
                    _fs.CreateBackup(changedFile.Path)));

                if (changedFile.ChangeKind == WorkerFileChangeKind.Delete)
                {
                    _fs.DeleteFile(changedFile.Path);
                }
                else
                {
                    var content = await _fs.ReadFileAsync(changedFile.ContentRef!);
                    await _fs.WriteFileAtomicAsync(changedFile.Path, content!);
                }

                appliedFiles.Add(changedFile.Path);
            }

            var validationIssues = profile.Permissions.RequiresValidation
                ? await _validateGameStateAsync()
                : [];
            if (validationIssues.Count > 0)
            {
                Rollback(rollback);
                var decision = BuildDecision(
                    proposal.ProposalId,
                    ApplyGateResult.ValidationFailed,
                    checkedPaths,
                    scopePassed: true,
                    violations: [],
                    validationRequired: true,
                    validationPassed: false,
                    issueCount: validationIssues.Count,
                    appliedFiles: [],
                    rejectionReasons: validationIssues.Select(issue => issue.ToString()).ToArray());
                await RecordDecisionAsync(proposal, decision);
                return decision;
            }

            CleanupBackups(rollback);
            var acceptedDecision = BuildDecision(
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
            await RecordDecisionAsync(proposal, acceptedDecision);
            return acceptedDecision;
        }
        catch
        {
            Rollback(rollback);
            throw;
        }
    }

    private async Task<IReadOnlyList<string>> VerifyProposalContentRefsAsync(WorkerProposal proposal)
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

            var content = await _fs.ReadFileAsync(changedFile.ContentRef);
            if (content == null)
                errors.Add($"changedFiles contentRef does not exist: {changedFile.ContentRef}");
        }

        return errors;
    }

    private async Task<IReadOnlyList<string>> VerifyActorMaterializationRepairPreservationAsync(
        WorkerProposal proposal,
        WorkerTaskPacket task)
    {
        if (task.TaskType != WorkerTaskType.ValidationRepair)
            return [];

        var errors = new List<string>();
        foreach (var changedFile in proposal.ChangedFiles)
        {
            var baseline = await _fs.ReadFileAsync(changedFile.Path);
            var proposed = changedFile.ChangeKind == WorkerFileChangeKind.Delete
                ? null
                : await _fs.ReadFileAsync(changedFile.ContentRef!);
            errors.AddRange(ActorMaterializationRepairPreservationGuard.Validate(
                changedFile.Path,
                baseline,
                proposed,
                task.ValidationIssues));
        }

        return errors;
    }

    private void Rollback(IReadOnlyList<RollbackEntry> rollback)
    {
        foreach (var entry in rollback.AsEnumerable().Reverse())
        {
            if (entry.ExistedBefore && !string.IsNullOrWhiteSpace(entry.BackupFullPath))
                _fs.RestoreBackup(entry.BackupFullPath!, entry.Path);
            else
                _fs.DeleteFile(entry.Path);
        }
    }

    private void CleanupBackups(IReadOnlyList<RollbackEntry> rollback)
    {
        foreach (var entry in rollback)
        {
            if (!string.IsNullOrWhiteSpace(entry.BackupFullPath))
                _fs.CleanupBackup(entry.BackupFullPath!);
        }
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

    private Task RecordDecisionAsync(WorkerProposal proposal, ApplyGateDecision decision) =>
        _auditLog?.RecordApplyDecisionAsync(proposal, decision) ?? Task.CompletedTask;

    private sealed record RollbackEntry(string Path, bool ExistedBefore, string? BackupFullPath);
}
