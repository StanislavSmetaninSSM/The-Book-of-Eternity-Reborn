namespace BookOfEternityClient.Services.GmWorkers;

public static class GmWorkerContractValidator
{
    public static WorkerContractValidationResult ValidateProfile(WorkerBridgeProfile? profile)
    {
        var errors = new List<string>();
        if (profile == null)
            return WorkerContractValidationResult.Failure(["Worker profile is required."]);

        ValidateId(profile.WorkerId, "workerId", errors);
        RequireText(profile.DisplayName, "displayName", errors);
        if (profile.Enabled)
            RequireText(profile.LaunchCommand, "launchCommand", errors);
        if (profile.LaunchVisibility != WorkerLaunchVisibility.Hidden)
            errors.Add("Worker profiles must use hidden/background launch visibility.");
        if (profile.TimeoutSeconds <= 0)
            errors.Add("timeoutSeconds must be greater than zero.");
        if (profile.MaxConcurrentTasks <= 0)
            errors.Add("maxConcurrentTasks must be greater than zero.");

        if (profile.Permissions.TaskTypes.Count == 0)
            errors.Add("permissions.taskTypes must contain at least one task type.");
        foreach (var path in profile.Permissions.ReadPaths)
            ValidatePathOrPattern(path, "permissions.readPaths", errors);
        foreach (var path in profile.Permissions.ProposalWritePaths)
            ValidatePathOrPattern(path, "permissions.proposalWritePaths", errors);

        if (profile.Permissions.ProposalOnly)
        {
            if (profile.Permissions.ProposalWritePaths.Count > 0)
                errors.Add("proposal-only profiles must not declare proposalWritePaths.");
            if (profile.Permissions.RequiresValidation)
                errors.Add("proposal-only profiles must not require canonical validation.");
        }
        else if (!profile.Permissions.RequiresValidation)
        {
            errors.Add("Profiles allowed to propose file changes must require validation.");
        }

        return ToResult(errors);
    }

    public static WorkerContractValidationResult ValidateTaskPacket(
        WorkerTaskPacket? task,
        WorkerBridgeProfile profile)
    {
        var errors = new List<string>();
        if (task == null)
            return WorkerContractValidationResult.Failure(["Worker task packet is required."]);

        errors.AddRange(ValidateProfile(profile).Errors);
        if (task.SchemaVersion != 1)
            errors.Add("schemaVersion must be 1.");
        ValidateId(task.TaskId, "taskId", errors);
        ValidateId(task.WorkerId, "workerId", errors);
        if (!string.Equals(task.WorkerId, profile.WorkerId, StringComparison.Ordinal))
            errors.Add("task.workerId must match profile.workerId.");
        if (!profile.Permissions.TaskTypes.Contains(task.TaskType))
            errors.Add($"Task type {task.TaskType} is not allowed by the worker profile.");
        RequireText(task.CreatedAtUtc, "createdAtUtc", errors);
        RequireText(task.ResponseContract, "responseContract", errors);
        RequireText(task.Instructions, "instructions", errors);

        foreach (var issue in task.ValidationIssues)
        {
            RequireText(issue.Code, "validationIssues.code", errors);
            ValidatePath(issue.Path, "validationIssues.path", errors);
            RequireText(issue.Message, "validationIssues.message", errors);
        }

        foreach (var file in task.ContextFiles)
        {
            ValidatePath(file.Path, "contextFiles.path", errors);
            RequireText(file.Sha256, "contextFiles.sha256", errors);
        }

        foreach (var path in task.AllowedProposalPaths)
        {
            ValidatePath(path, "allowedProposalPaths", errors);
            if (!profile.Permissions.ProposalOnly &&
                !profile.Permissions.ProposalWritePaths.Any(pattern => PathMatches(pattern, path)))
                errors.Add($"allowedProposalPaths contains a path outside profile write scope: {path}");
        }

        if (profile.Permissions.ProposalOnly && task.AllowedProposalPaths.Count > 0)
            errors.Add("proposal-only tasks must not include allowedProposalPaths.");
        if (task.TaskType == WorkerTaskType.ValidationRepair && task.ValidationIssues.Count == 0)
            errors.Add("validation-repair tasks must include validationIssues.");
        if (task.TaskType == WorkerTaskType.NarrativeDraft && task.DraftRequest == null)
            errors.Add("narrative-draft tasks must include draftRequest.");

        return ToResult(errors);
    }

    public static WorkerContractValidationResult ValidateProposal(
        WorkerProposal? proposal,
        WorkerTaskPacket task,
        WorkerBridgeProfile profile)
    {
        var errors = new List<string>();
        if (proposal == null)
            return WorkerContractValidationResult.Failure(["Worker proposal is required."]);

        errors.AddRange(ValidateTaskPacket(task, profile).Errors);
        if (proposal.SchemaVersion != 1)
            errors.Add("schemaVersion must be 1.");
        ValidateId(proposal.ProposalId, "proposalId", errors);
        ValidateId(proposal.TaskId, "taskId", errors);
        ValidateId(proposal.WorkerId, "workerId", errors);
        if (!string.Equals(proposal.TaskId, task.TaskId, StringComparison.Ordinal))
            errors.Add("proposal.taskId must match task.taskId.");
        if (!string.Equals(proposal.WorkerId, profile.WorkerId, StringComparison.Ordinal))
            errors.Add("proposal.workerId must match profile.workerId.");
        RequireText(proposal.Summary, "summary", errors);
        RequireText(proposal.CreatedAtUtc, "createdAtUtc", errors);

        if (profile.Permissions.ProposalOnly && proposal.ChangedFiles.Count > 0)
            errors.Add("proposal-only worker proposals must not include changedFiles.");

        foreach (var changedFile in proposal.ChangedFiles)
        {
            ValidatePath(changedFile.Path, "changedFiles.path", errors);
            if (!task.AllowedProposalPaths.Contains(changedFile.Path, StringComparer.Ordinal))
                errors.Add($"changedFiles contains a path outside task allowedProposalPaths: {changedFile.Path}");
            if (!profile.Permissions.ProposalOnly &&
                !profile.Permissions.ProposalWritePaths.Any(pattern => PathMatches(pattern, changedFile.Path)))
                errors.Add($"changedFiles contains a path outside profile write scope: {changedFile.Path}");
            if (changedFile.ChangeKind != WorkerFileChangeKind.Delete)
                RequireText(changedFile.ContentRef, "changedFiles.contentRef", errors);
            if (!string.IsNullOrWhiteSpace(changedFile.ContentRef))
                ValidatePath(changedFile.ContentRef!, "changedFiles.contentRef", errors);
        }

        if (task.TaskType == WorkerTaskType.NarrativeDraft && string.IsNullOrWhiteSpace(proposal.DraftText))
            errors.Add("narrative-draft proposals must include draftText.");

        return ToResult(errors);
    }

    public static bool IsSafeRelativePath(string path) =>
        IsSafeRelativePath(path, allowGlob: false);

    public static bool PathMatches(string pattern, string path)
    {
        if (!IsSafeRelativePath(pattern, allowGlob: true) || !IsSafeRelativePath(path, allowGlob: false))
            return false;
        if (string.Equals(pattern, path, StringComparison.Ordinal))
            return true;
        if (pattern.EndsWith("/**", StringComparison.Ordinal))
        {
            var prefix = pattern[..^3];
            return string.Equals(prefix, path, StringComparison.Ordinal) ||
                   path.StartsWith(prefix + "/", StringComparison.Ordinal);
        }

        return false;
    }

    private static WorkerContractValidationResult ToResult(List<string> errors) =>
        errors.Count == 0 ? WorkerContractValidationResult.Success : WorkerContractValidationResult.Failure(errors);

    private static void ValidateId(string value, string fieldName, List<string> errors)
    {
        RequireText(value, fieldName, errors);
        if (string.IsNullOrWhiteSpace(value))
            return;
        if (value.Any(ch => !(char.IsLower(ch) || char.IsDigit(ch) || ch is '_' or '-')))
            errors.Add($"{fieldName} must contain only lowercase letters, digits, underscores, and hyphens.");
    }

    private static void RequireText(string? value, string fieldName, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
            errors.Add($"{fieldName} is required.");
    }

    private static void ValidatePath(string? path, string fieldName, List<string> errors)
    {
        RequireText(path, fieldName, errors);
        if (!string.IsNullOrWhiteSpace(path) && !IsSafeRelativePath(path, allowGlob: false))
            errors.Add($"{fieldName} must be a safe relative path.");
    }

    private static void ValidatePathOrPattern(string? path, string fieldName, List<string> errors)
    {
        RequireText(path, fieldName, errors);
        if (!string.IsNullOrWhiteSpace(path) && !IsSafeRelativePath(path, allowGlob: true))
            errors.Add($"{fieldName} must be a safe relative path or /** pattern.");
    }

    private static bool IsSafeRelativePath(string path, bool allowGlob)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;
        if (path.Contains('\\', StringComparison.Ordinal) ||
            path.Contains(':', StringComparison.Ordinal) ||
            path.StartsWith("/", StringComparison.Ordinal) ||
            path.StartsWith("~", StringComparison.Ordinal))
        {
            return false;
        }

        var segments = path.Split('/');
        if (segments.Any(segment => string.IsNullOrWhiteSpace(segment) || segment is "." or ".."))
            return false;
        if (!allowGlob && segments.Any(segment => segment.Contains('*', StringComparison.Ordinal)))
            return false;
        if (allowGlob && segments.Any(segment => segment.Contains('*', StringComparison.Ordinal) && segment != "**"))
            return false;

        return true;
    }
}
