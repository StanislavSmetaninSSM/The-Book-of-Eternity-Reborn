using BookOfEternityClient.Services;

namespace BookOfEternityClient.Services.GmWorkers;

public static class GmWorkerTaskPacketBuilder
{
    public static WorkerTaskPacket BuildValidationRepairTask(
        WorkerBridgeProfile profile,
        string taskId,
        WorkerTurnReference sourceTurn,
        IReadOnlyList<ValidationIssue> validationIssues,
        IReadOnlyDictionary<string, string> contextFileHashes,
        string createdAtUtc)
    {
        var profileValidation = GmWorkerContractValidator.ValidateProfile(profile);
        if (!profileValidation.IsValid)
            throw new ArgumentException(string.Join(Environment.NewLine, profileValidation.Errors), nameof(profile));
        if (!profile.Permissions.TaskTypes.Contains(WorkerTaskType.ValidationRepair))
            throw new ArgumentException("Worker profile cannot handle validation-repair tasks.", nameof(profile));
        if (validationIssues.Count == 0)
            throw new ArgumentException("At least one validation issue is required.", nameof(validationIssues));

        var allowedPaths = validationIssues
            .Select(issue => issue.FilePath.Replace('\\', '/'))
            .Where(GmWorkerContractValidator.IsSafeRelativePath)
            .Where(path => profile.Permissions.ProposalWritePaths.Any(pattern => GmWorkerContractValidator.PathMatches(pattern, path)))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        if (allowedPaths.Length == 0)
            throw new ArgumentException("Validation issues do not map to any safe worker proposal path.", nameof(validationIssues));

        var contextFiles = allowedPaths
            .Select(path => new WorkerFileReference
            {
                Path = path,
                Sha256 = contextFileHashes.TryGetValue(path, out var hash) ? hash : ""
            })
            .ToArray();

        var task = new WorkerTaskPacket
        {
            TaskId = taskId,
            WorkerId = profile.WorkerId,
            TaskType = WorkerTaskType.ValidationRepair,
            CreatedAtUtc = createdAtUtc,
            SourceTurn = sourceTurn,
            ValidationIssues = validationIssues.Select(issue => new WorkerValidationIssue
            {
                Code = string.IsNullOrWhiteSpace(issue.Code) ? "validation_issue" : issue.Code!,
                Path = issue.FilePath.Replace('\\', '/'),
                Message = issue.Message
            }).ToArray(),
            ContextFiles = contextFiles,
            AllowedProposalPaths = allowedPaths,
            Instructions =
                "Return a worker-proposal-v1 JSON proposal. Include changedFiles only for allowedProposalPaths. " +
                "Do not edit canonical game_session files directly."
        };

        var taskValidation = GmWorkerContractValidator.ValidateTaskPacket(task, profile);
        if (!taskValidation.IsValid)
            throw new InvalidOperationException(string.Join(Environment.NewLine, taskValidation.Errors));

        return task;
    }

    public static WorkerTaskPacket BuildNarrativeDraftTask(
        WorkerBridgeProfile profile,
        string taskId,
        WorkerTurnReference sourceTurn,
        WorkerDraftRequest draftRequest,
        IReadOnlyList<WorkerFileReference> contextFiles,
        string createdAtUtc)
    {
        var profileValidation = GmWorkerContractValidator.ValidateProfile(profile);
        if (!profileValidation.IsValid)
            throw new ArgumentException(string.Join(Environment.NewLine, profileValidation.Errors), nameof(profile));
        if (!profile.Permissions.TaskTypes.Contains(WorkerTaskType.NarrativeDraft))
            throw new ArgumentException("Worker profile cannot handle narrative-draft tasks.", nameof(profile));
        if (!profile.Permissions.ProposalOnly)
            throw new ArgumentException("Narrative draft workers must be proposal-only.", nameof(profile));

        var task = new WorkerTaskPacket
        {
            TaskId = taskId,
            WorkerId = profile.WorkerId,
            TaskType = WorkerTaskType.NarrativeDraft,
            CreatedAtUtc = createdAtUtc,
            SourceTurn = sourceTurn,
            DraftRequest = draftRequest,
            ContextFiles = contextFiles,
            AllowedProposalPaths = [],
            Instructions =
                "Return a worker-proposal-v1 JSON proposal with draftText and optional findings. " +
                "This is proposal-only: do not include changedFiles and do not edit canonical game_session files directly."
        };

        var taskValidation = GmWorkerContractValidator.ValidateTaskPacket(task, profile);
        if (!taskValidation.IsValid)
            throw new InvalidOperationException(string.Join(Environment.NewLine, taskValidation.Errors));

        return task;
    }

    public static WorkerTaskPacket BuildAnalysisTask(
        WorkerBridgeProfile profile,
        string taskId,
        WorkerTurnReference sourceTurn,
        string analysisGoal,
        IReadOnlyList<string> questions,
        IReadOnlyList<WorkerFileReference> contextFiles,
        string createdAtUtc)
    {
        var profileValidation = GmWorkerContractValidator.ValidateProfile(profile);
        if (!profileValidation.IsValid)
            throw new ArgumentException(string.Join(Environment.NewLine, profileValidation.Errors), nameof(profile));
        if (!profile.Permissions.TaskTypes.Contains(WorkerTaskType.Analysis))
            throw new ArgumentException("Worker profile cannot handle analysis tasks.", nameof(profile));
        if (!profile.Permissions.ProposalOnly)
            throw new ArgumentException("Analysis workers must be proposal-only.", nameof(profile));
        if (string.IsNullOrWhiteSpace(analysisGoal))
            throw new ArgumentException("Analysis goal is required.", nameof(analysisGoal));

        var questionText = questions.Count == 0
            ? "No explicit questions were provided."
            : string.Join(Environment.NewLine, questions.Select((question, index) => $"{index + 1}. {question}"));

        var task = new WorkerTaskPacket
        {
            TaskId = taskId,
            WorkerId = profile.WorkerId,
            TaskType = WorkerTaskType.Analysis,
            CreatedAtUtc = createdAtUtc,
            SourceTurn = sourceTurn,
            ContextFiles = contextFiles,
            AllowedProposalPaths = [],
            Instructions =
                "Return a worker-proposal-v1 JSON proposal with findings only. " +
                "This is proposal-only: do not include changedFiles and do not edit canonical game_session files directly. " +
                $"Analysis goal: {analysisGoal}{Environment.NewLine}Questions:{Environment.NewLine}{questionText}"
        };

        var taskValidation = GmWorkerContractValidator.ValidateTaskPacket(task, profile);
        if (!taskValidation.IsValid)
            throw new InvalidOperationException(string.Join(Environment.NewLine, taskValidation.Errors));

        return task;
    }
}
