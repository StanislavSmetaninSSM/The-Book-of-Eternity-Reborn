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
        if (task.Role != profile.Role)
            errors.Add("task.role must match profile.role.");
        if (!profile.Permissions.TaskTypes.Contains(task.TaskType))
            errors.Add($"Task type {task.TaskType} is not allowed by the worker profile.");
        RequireText(task.CreatedAtUtc, "createdAtUtc", errors);
        if (task.TimeoutSeconds <= 0)
            errors.Add("timeoutSeconds must be greater than zero.");
        else if (task.TimeoutSeconds > profile.TimeoutSeconds)
            errors.Add("task.timeoutSeconds must not exceed profile.timeoutSeconds.");
        RequireText(task.ResponseContract, "responseContract", errors);
        if (task.AcceptanceCriteria.Count == 0)
            errors.Add("acceptanceCriteria must contain at least one criterion.");
        foreach (var criterion in task.AcceptanceCriteria)
            RequireText(criterion, "acceptanceCriteria", errors);
        if (task.ForbiddenActions.Count == 0)
            errors.Add("forbiddenActions must contain at least one forbidden action.");
        foreach (var action in task.ForbiddenActions)
            RequireText(action, "forbiddenActions", errors);
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

        ValidateAfterlifeTaskPacket(task, errors);

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
        if (WorkerTaskTypes.IsContentAuthoring(task.TaskType))
        {
            if (!profile.Permissions.ProposalOnly)
                errors.Add("content-authoring workers must be proposal-only.");
            if (task.AllowedProposalPaths.Count > 0)
                errors.Add("content-authoring tasks must not include allowedProposalPaths.");
            ValidateAuthoringRequest(task.AuthoringRequest, task.TaskType, errors);
            ValidateGuardianAbodeTaskPacket(task, errors);
            ValidateSoulContentTaskPacket(task, errors);
        }
        else if (task.AuthoringRequest != null)
        {
            errors.Add("authoringRequest is only allowed for content-authoring tasks.");
        }

        if (task.TaskType != WorkerTaskType.GuardianAbodeContent && task.GuardianAbodeRequest != null)
            errors.Add("guardianAbodeRequest is only allowed for guardian-abode-content tasks.");
        if (task.TaskType != WorkerTaskType.SoulContent && task.SoulContentRequest != null)
            errors.Add("soulContentRequest is only allowed for soul-content tasks.");

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
        if (WorkerTaskTypes.IsContentAuthoring(task.TaskType))
        {
            ValidateAuthoringProposal(proposal.AuthoringProposal, task, errors);
        }
        else if (proposal.AuthoringProposal != null)
        {
            errors.Add("authoringProposal is only allowed for content-authoring proposals.");
        }

        ValidateAfterlifeProposal(proposal, task, errors);
        ValidateGuardianAbodeProposal(proposal, task, errors);
        ValidateSoulContentProposal(proposal, task, errors);

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

    private static void ValidateAuthoringRequest(
        WorkerContentAuthoringRequest? request,
        WorkerTaskType taskType,
        List<string> errors)
    {
        if (request == null)
        {
            errors.Add("content-authoring tasks must include authoringRequest.");
            return;
        }

        if (TaskTypeToDomain(taskType) is { } expectedDomain && request.Domain != expectedDomain)
            errors.Add($"authoringRequest.domain must match taskType {taskType}.");
        RequireText(request.Goal, "authoringRequest.goal", errors);
        foreach (var hint in request.EntityHints)
            RequireText(hint, "authoringRequest.entityHints", errors);
        foreach (var link in request.RequiredLinks)
            RequireText(link, "authoringRequest.requiredLinks", errors);
        foreach (var note in request.OutputNotes)
            RequireText(note, "authoringRequest.outputNotes", errors);
    }

    private static void ValidateAfterlifeTaskPacket(WorkerTaskPacket task, List<string> errors)
    {
        if (task.AfterlifeContract == null)
        {
            if (TaskLooksAfterlifeScoped(task))
                errors.Add("afterlife worker tasks must include afterlifeContract realm-aware wrapper.");
            return;
        }

        var contract = task.AfterlifeContract;
        if (contract.RealmGate == WorkerAfterlifeRealmGate.None)
            errors.Add("afterlifeContract.realmGate must be ChaosSea, ShiningAbode, or ShiningAbodePendingBootstrap.");
        RequireText(contract.CurrentRealm, "afterlifeContract.currentRealm", errors);

        if (contract.AllowedAfterlifeSurfaces.Count == 0)
            errors.Add("afterlifeContract.allowedAfterlifeSurfaces must contain at least one exact afterlife state surface.");
        foreach (var path in contract.AllowedAfterlifeSurfaces)
        {
            ValidatePathOrPattern(path, "afterlifeContract.allowedAfterlifeSurfaces", errors);
            if (IsMortalWorldSubstitutePath(path))
                errors.Add($"afterlifeContract.allowedAfterlifeSurfaces contains a Mortal World substitute path: {path}");
        }

        foreach (var path in contract.ProgressionControlPaths)
        {
            ValidatePath(path, "afterlifeContract.progressionControlPaths", errors);
            if (IsMortalWorldSubstitutePath(path))
                errors.Add($"afterlifeContract.progressionControlPaths contains a Mortal World substitute path: {path}");
        }

        foreach (var path in contract.PendingControlFiles)
        {
            ValidatePath(path, "afterlifeContract.pendingControlFiles", errors);
            if (IsMortalWorldSubstitutePath(path))
                errors.Add($"afterlifeContract.pendingControlFiles contains a Mortal World substitute path: {path}");
        }

        if (contract.RequiredReceipts.Count == 0)
            errors.Add("afterlifeContract.requiredReceipts must contain at least one receipt or explicit no-receipt note.");
        foreach (var receipt in contract.RequiredReceipts)
            RequireText(receipt, "afterlifeContract.requiredReceipts", errors);

        if (contract.RequiredReports.Count == 0)
            errors.Add("afterlifeContract.requiredReports must contain at least one report or explicit no-report note.");
        foreach (var report in contract.RequiredReports)
            RequireText(report, "afterlifeContract.requiredReports", errors);

        if (contract.ForbiddenMortalSubstitutes.Count == 0)
            errors.Add("afterlifeContract.forbiddenMortalSubstitutes must explicitly name forbidden Mortal World substitutes.");
        foreach (var substitute in contract.ForbiddenMortalSubstitutes)
            RequireText(substitute, "afterlifeContract.forbiddenMortalSubstitutes", errors);
    }

    private static void ValidateGuardianAbodeTaskPacket(WorkerTaskPacket task, List<string> errors)
    {
        if (task.TaskType != WorkerTaskType.GuardianAbodeContent)
        {
            return;
        }

        if (task.AfterlifeContract == null)
            errors.Add("guardian-abode-content tasks must include afterlifeContract.");

        if (task.AuthoringRequest?.Domain != WorkerAuthoringDomain.GuardianAbode)
            errors.Add("guardian-abode-content tasks must use authoringRequest.domain GuardianAbode.");

        if (task.GuardianAbodeRequest == null)
        {
            errors.Add("guardian-abode-content tasks must include guardianAbodeRequest.");
            return;
        }

        var request = task.GuardianAbodeRequest;
        RequireText(request.Realm, "guardianAbodeRequest.realm", errors);
        if (request.GuardianIds.Count == 0 && request.AbodeIds.Count == 0)
            errors.Add("guardianAbodeRequest must name at least one Guardian or Abode id.");
        foreach (var guardianId in request.GuardianIds)
            ValidateId(guardianId, "guardianAbodeRequest.guardianIds", errors);
        foreach (var abodeId in request.AbodeIds)
            ValidateId(abodeId, "guardianAbodeRequest.abodeIds", errors);

        if (request.PendingControlFiles.Count == 0)
            errors.Add("guardianAbodeRequest.pendingControlFiles must include relevant afterlife pending/control files.");
        foreach (var path in request.PendingControlFiles)
        {
            ValidatePath(path, "guardianAbodeRequest.pendingControlFiles", errors);
            if (IsMortalWorldSubstitutePath(path))
                errors.Add($"guardianAbodeRequest.pendingControlFiles contains a Mortal World substitute path: {path}");
        }

        if (request.FocusAreas.Count == 0)
            errors.Add("guardianAbodeRequest.focusAreas must describe Guardian/Abode focus areas.");
        foreach (var focusArea in request.FocusAreas)
            RequireText(focusArea, "guardianAbodeRequest.focusAreas", errors);

        if (request.ReadScope.Count == 0)
            errors.Add("guardianAbodeRequest.readScope must include exact Guardian/Abode afterlife surfaces.");
        foreach (var path in request.ReadScope)
        {
            ValidatePath(path, "guardianAbodeRequest.readScope", errors);
            if (IsMortalWorldSubstitutePath(path))
                errors.Add($"guardianAbodeRequest.readScope contains a Mortal World substitute path: {path}");
        }
    }

    private static void ValidateSoulContentTaskPacket(WorkerTaskPacket task, List<string> errors)
    {
        if (task.TaskType != WorkerTaskType.SoulContent)
        {
            return;
        }

        if (task.AfterlifeContract == null)
            errors.Add("soul-content tasks must include afterlifeContract.");

        if (task.AuthoringRequest?.Domain != WorkerAuthoringDomain.Soul)
            errors.Add("soul-content tasks must use authoringRequest.domain Soul.");

        if (task.SoulContentRequest == null)
        {
            errors.Add("soul-content tasks must include soulContentRequest.");
            return;
        }

        var request = task.SoulContentRequest;
        RequireText(request.Realm, "soulContentRequest.realm", errors);
        RequireText(request.SoulContext, "soulContentRequest.soulContext", errors);

        if (request.RequestedScope.Count == 0)
            errors.Add("soulContentRequest.requestedScope must describe the requested soul-content scope.");
        foreach (var scope in request.RequestedScope)
            RequireText(scope, "soulContentRequest.requestedScope", errors);

        if (request.ProgressionConstraints.Count == 0)
            errors.Add("soulContentRequest.progressionConstraints must include soul progression constraints.");
        foreach (var constraint in request.ProgressionConstraints)
            RequireText(constraint, "soulContentRequest.progressionConstraints", errors);

        if (request.ReadScope.Count == 0)
            errors.Add("soulContentRequest.readScope must include exact soul/afterlife surfaces.");
        foreach (var path in request.ReadScope)
        {
            ValidatePath(path, "soulContentRequest.readScope", errors);
            if (IsMortalWorldSubstitutePath(path))
                errors.Add($"soulContentRequest.readScope contains a Mortal World substitute path: {path}");
        }

        ValidateReadonlyIdentityFields(
            request.PlayerOwnedIdentityFields,
            "soulContentRequest.playerOwnedIdentityFields",
            errors);
    }

    private static void ValidateAuthoringProposal(
        WorkerContentAuthoringProposal? proposal,
        WorkerTaskPacket task,
        List<string> errors)
    {
        if (proposal == null)
        {
            errors.Add("content-authoring proposals must include authoringProposal.");
            return;
        }

        if (TaskTypeToDomain(task.TaskType) is { } expectedDomain && proposal.Domain != expectedDomain)
            errors.Add($"authoringProposal.domain must match taskType {task.TaskType}.");
        if (task.AuthoringRequest != null && proposal.Domain != task.AuthoringRequest.Domain)
            errors.Add("authoringProposal.domain must match task.authoringRequest.domain.");
        RequireText(proposal.Goal, "authoringProposal.goal", errors);

        var entityCount = proposal.CreatedEntities.Count + proposal.UpdatedEntities.Count;
        if (entityCount == 0)
            errors.Add("authoringProposal must include at least one createdEntities or updatedEntities item.");
        foreach (var entity in proposal.CreatedEntities)
            ValidateAuthoredEntity(entity, "authoringProposal.createdEntities", errors);
        foreach (var entity in proposal.UpdatedEntities)
            ValidateAuthoredEntity(entity, "authoringProposal.updatedEntities", errors);

        if (proposal.RequiredLinks.Count == 0)
            errors.Add("authoringProposal.requiredLinks must contain at least one link the main GM must review.");
        foreach (var link in proposal.RequiredLinks)
        {
            RequireText(link.Source, "authoringProposal.requiredLinks.source", errors);
            RequireText(link.Target, "authoringProposal.requiredLinks.target", errors);
            RequireText(link.Reason, "authoringProposal.requiredLinks.reason", errors);
        }

        if (proposal.ValidatorRisks.Count == 0)
            errors.Add("authoringProposal.validatorRisks must contain at least one validator risk or no-risk note.");
        foreach (var risk in proposal.ValidatorRisks)
        {
            ValidateId(risk.Code, "authoringProposal.validatorRisks.code", errors);
            RequireText(risk.Message, "authoringProposal.validatorRisks.message", errors);
            RequireText(risk.Mitigation, "authoringProposal.validatorRisks.mitigation", errors);
        }

        if (proposal.GmReviewNotes.Count == 0)
            errors.Add("authoringProposal.gmReviewNotes must contain at least one main-GM review note.");
        foreach (var note in proposal.GmReviewNotes)
            RequireText(note, "authoringProposal.gmReviewNotes", errors);

        ValidateDomainAuthoringProposal(proposal, errors);
    }

    private static void ValidateAfterlifeProposal(
        WorkerProposal proposal,
        WorkerTaskPacket task,
        List<string> errors)
    {
        if (task.AfterlifeContract == null)
        {
            if (proposal.AfterlifeProposal != null)
                errors.Add("afterlifeProposal requires task.afterlifeContract realm-aware wrapper.");
            return;
        }

        if (proposal.AfterlifeProposal == null)
        {
            if (task.TaskType == WorkerTaskType.ValidationRepair)
                return;
            errors.Add("afterlife worker proposals must include afterlifeProposal.");
            return;
        }

        var contract = task.AfterlifeContract;
        var afterlife = proposal.AfterlifeProposal;
        if (afterlife.RealmGate != contract.RealmGate)
            errors.Add("afterlifeProposal.realmGate must match task.afterlifeContract.realmGate.");

        if (afterlife.TargetSurfaces.Count == 0)
            errors.Add("afterlifeProposal.targetSurfaces must contain at least one afterlife state surface.");
        foreach (var surface in afterlife.TargetSurfaces)
        {
            ValidatePath(surface, "afterlifeProposal.targetSurfaces", errors);
            if (IsMortalWorldSubstitutePath(surface))
            {
                errors.Add($"afterlifeProposal.targetSurfaces contains a Mortal World substitute path: {surface}");
                continue;
            }

            if (!contract.AllowedAfterlifeSurfaces.Any(pattern => PathMatches(pattern, surface)))
                errors.Add($"afterlifeProposal.targetSurfaces contains a surface outside task.afterlifeContract.allowedAfterlifeSurfaces: {surface}");
        }

        ValidateAfterlifeRequiredNames(
            contract.RequiredReceipts,
            afterlife.RequiredReceipts,
            "afterlifeProposal.requiredReceipts",
            errors);
        ValidateAfterlifeRequiredNames(
            contract.RequiredReports,
            afterlife.RequiredReports,
            "afterlifeProposal.requiredReports",
            errors);

        RequireText(afterlife.PlayerVisibleSummary, "afterlifeProposal.playerVisibleSummary", errors);
        if (ContainsMortalWorldSubstituteText(afterlife.PlayerVisibleSummary))
            errors.Add("afterlifeProposal.playerVisibleSummary contains a Mortal World substitute reference.");

        if (afterlife.GmReviewNotes.Count == 0)
            errors.Add("afterlifeProposal.gmReviewNotes must contain at least one main-GM review note.");
        foreach (var note in afterlife.GmReviewNotes)
        {
            RequireText(note, "afterlifeProposal.gmReviewNotes", errors);
            if (ContainsMortalWorldSubstituteText(note))
                errors.Add("afterlifeProposal.gmReviewNotes contains a Mortal World substitute reference.");
        }

        if (afterlife.ValidatorRisks.Count == 0)
            errors.Add("afterlifeProposal.validatorRisks must contain at least one validator risk or no-risk note.");
        foreach (var risk in afterlife.ValidatorRisks)
        {
            ValidateId(risk.Code, "afterlifeProposal.validatorRisks.code", errors);
            RequireText(risk.Message, "afterlifeProposal.validatorRisks.message", errors);
            RequireText(risk.Mitigation, "afterlifeProposal.validatorRisks.mitigation", errors);
            if (ContainsMortalWorldSubstituteText(risk.Message) ||
                ContainsMortalWorldSubstituteText(risk.Mitigation))
            {
                errors.Add("afterlifeProposal.validatorRisks contains a Mortal World substitute reference.");
            }
        }
    }

    private static void ValidateGuardianAbodeProposal(
        WorkerProposal proposal,
        WorkerTaskPacket task,
        List<string> errors)
    {
        if (task.TaskType != WorkerTaskType.GuardianAbodeContent)
        {
            if (proposal.GuardianAbodeProposal != null)
                errors.Add("guardianAbodeProposal is only allowed for guardian-abode-content proposals.");
            return;
        }

        if (proposal.GuardianAbodeProposal == null)
        {
            errors.Add("guardian-abode-content proposals must include guardianAbodeProposal.");
            return;
        }

        var guardian = proposal.GuardianAbodeProposal;
        RequireText(guardian.PlayerVisibleSummary, "guardianAbodeProposal.playerVisibleSummary", errors);
        if (ContainsMortalWorldSubstituteText(guardian.PlayerVisibleSummary))
            errors.Add("guardianAbodeProposal.playerVisibleSummary contains a Mortal World substitute reference.");
        if (ContainsHiddenFactMarker(guardian.PlayerVisibleSummary))
            errors.Add("guardianAbodeProposal.playerVisibleSummary must not reveal hidden or GM-only Guardian facts.");

        ValidateGuardianAbodeProposalItems(
            guardian.GuardianUpdates,
            "guardianAbodeProposal.guardianUpdates",
            task.AfterlifeContract,
            errors);
        ValidateGuardianAbodeProposalItems(
            guardian.AbodeUpdates,
            "guardianAbodeProposal.abodeUpdates",
            task.AfterlifeContract,
            errors);
        ValidateGuardianAbodeProposalItems(
            guardian.ProjectSuggestions,
            "guardianAbodeProposal.projectSuggestions",
            task.AfterlifeContract,
            errors);
        ValidateGuardianAbodeProposalItems(
            guardian.PowerReputationConsequences,
            "guardianAbodeProposal.powerReputationConsequences",
            task.AfterlifeContract,
            errors);
        ValidateGuardianAbodeProposalItems(
            guardian.TradeFavorHooks,
            "guardianAbodeProposal.tradeFavorHooks",
            task.AfterlifeContract,
            errors);
        ValidateGuardianAbodeProposalItems(
            guardian.DossierNotes,
            "guardianAbodeProposal.dossierNotes",
            task.AfterlifeContract,
            errors);

        ValidateAfterlifeRequiredNames(
            task.AfterlifeContract?.RequiredReceipts ?? [],
            guardian.RequiredReceipts,
            "guardianAbodeProposal.requiredReceipts",
            errors);
        ValidateAfterlifeRequiredNames(
            task.AfterlifeContract?.RequiredReports ?? [],
            guardian.RequiredReports,
            "guardianAbodeProposal.requiredReports",
            errors);

        if (guardian.ValidatorRisks.Count == 0)
            errors.Add("guardianAbodeProposal.validatorRisks must contain at least one validator risk or no-risk note.");
        foreach (var risk in guardian.ValidatorRisks)
        {
            ValidateId(risk.Code, "guardianAbodeProposal.validatorRisks.code", errors);
            RequireText(risk.Message, "guardianAbodeProposal.validatorRisks.message", errors);
            RequireText(risk.Mitigation, "guardianAbodeProposal.validatorRisks.mitigation", errors);
            if (ContainsMortalWorldSubstituteText(risk.Message) ||
                ContainsMortalWorldSubstituteText(risk.Mitigation))
            {
                errors.Add("guardianAbodeProposal.validatorRisks contains a Mortal World substitute reference.");
            }
        }

        if (guardian.GmReviewNotes.Count == 0)
            errors.Add("guardianAbodeProposal.gmReviewNotes must contain at least one main-GM review note.");
        foreach (var note in guardian.GmReviewNotes)
        {
            RequireText(note, "guardianAbodeProposal.gmReviewNotes", errors);
            if (ContainsMortalWorldSubstituteText(note))
                errors.Add("guardianAbodeProposal.gmReviewNotes contains a Mortal World substitute reference.");
        }
    }

    private static void ValidateSoulContentProposal(
        WorkerProposal proposal,
        WorkerTaskPacket task,
        List<string> errors)
    {
        if (task.TaskType != WorkerTaskType.SoulContent)
        {
            if (proposal.SoulContentProposal != null)
                errors.Add("soulContentProposal is only allowed for soul-content proposals.");
            return;
        }

        if (proposal.SoulContentProposal == null)
        {
            errors.Add("soul-content proposals must include soulContentProposal.");
            return;
        }

        var soul = proposal.SoulContentProposal;
        RequireText(soul.PlayerVisibleSummary, "soulContentProposal.playerVisibleSummary", errors);
        if (ContainsMortalWorldSubstituteText(soul.PlayerVisibleSummary))
            errors.Add("soulContentProposal.playerVisibleSummary contains a Mortal World substitute reference.");

        ValidateSoulContentProposalItems(
            soul.SafeSoulSummaries,
            "soulContentProposal.safeSoulSummaries",
            task.AfterlifeContract,
            errors);
        ValidateSoulContentProposalItems(
            soul.ProgressionSuggestions,
            "soulContentProposal.progressionSuggestions",
            task.AfterlifeContract,
            errors);
        ValidateSoulContentProposalItems(
            soul.RewardNotes,
            "soulContentProposal.rewardNotes",
            task.AfterlifeContract,
            errors);
        ValidateSoulContentProposalItems(
            soul.NextLifePreparationHooks,
            "soulContentProposal.nextLifePreparationHooks",
            task.AfterlifeContract,
            errors);

        ValidateAfterlifeRequiredNames(
            task.AfterlifeContract?.RequiredReceipts ?? [],
            soul.RequiredReceipts,
            "soulContentProposal.requiredReceipts",
            errors);
        ValidateAfterlifeRequiredNames(
            task.AfterlifeContract?.RequiredReports ?? [],
            soul.RequiredReports,
            "soulContentProposal.requiredReports",
            errors);

        var requiredReadonlyFields = task.SoulContentRequest?.PlayerOwnedIdentityFields ?? [];
        ValidateReadonlyIdentityFields(
            soul.ForbiddenReadonlyFields,
            "soulContentProposal.forbiddenReadonlyFields",
            errors,
            requiredReadonlyFields);

        if (soul.ValidatorRisks.Count == 0)
            errors.Add("soulContentProposal.validatorRisks must contain at least one validator risk or no-risk note.");
        foreach (var risk in soul.ValidatorRisks)
        {
            ValidateId(risk.Code, "soulContentProposal.validatorRisks.code", errors);
            RequireText(risk.Message, "soulContentProposal.validatorRisks.message", errors);
            RequireText(risk.Mitigation, "soulContentProposal.validatorRisks.mitigation", errors);
            if (ContainsMortalWorldSubstituteText(risk.Message) ||
                ContainsMortalWorldSubstituteText(risk.Mitigation))
            {
                errors.Add("soulContentProposal.validatorRisks contains a Mortal World substitute reference.");
            }
        }

        if (soul.GmReviewNotes.Count == 0)
            errors.Add("soulContentProposal.gmReviewNotes must contain at least one main-GM review note.");
        foreach (var note in soul.GmReviewNotes)
        {
            RequireText(note, "soulContentProposal.gmReviewNotes", errors);
            if (ContainsMortalWorldSubstituteText(note))
                errors.Add("soulContentProposal.gmReviewNotes contains a Mortal World substitute reference.");
        }
    }

    private static void ValidateSoulContentProposalItems(
        IReadOnlyList<WorkerSoulContentProposalItem> items,
        string fieldName,
        WorkerAfterlifeTaskContract? afterlifeContract,
        List<string> errors)
    {
        if (items.Count == 0)
            errors.Add($"{fieldName} must contain at least one soul proposal item.");

        foreach (var item in items)
        {
            ValidateId(item.ItemId, $"{fieldName}.itemId", errors);
            RequireText(item.Title, $"{fieldName}.title", errors);
            RequireText(item.Summary, $"{fieldName}.summary", errors);
            RequireText(item.Visibility, $"{fieldName}.visibility", errors);

            if (ContainsMortalWorldSubstituteText(item.Title) ||
                ContainsMortalWorldSubstituteText(item.Summary) ||
                ContainsMortalWorldSubstituteText(item.Visibility))
            {
                errors.Add($"{fieldName} contains a Mortal World substitute reference.");
            }

            if (item.TargetSurfaces.Count == 0)
                errors.Add($"{fieldName}.targetSurfaces must contain at least one exact soul/afterlife surface.");
            foreach (var surface in item.TargetSurfaces)
            {
                ValidatePath(surface, $"{fieldName}.targetSurfaces", errors);
                if (IsMortalWorldSubstitutePath(surface))
                {
                    errors.Add($"{fieldName}.targetSurfaces contains a Mortal World substitute path: {surface}");
                    continue;
                }

                if (afterlifeContract != null &&
                    !afterlifeContract.AllowedAfterlifeSurfaces.Any(pattern => PathMatches(pattern, surface)))
                {
                    errors.Add($"{fieldName}.targetSurfaces contains a surface outside task.afterlifeContract.allowedAfterlifeSurfaces: {surface}");
                }
            }

            if (item.Fields.Count == 0)
                errors.Add($"{fieldName}.fields must contain at least one field for main-GM review.");
            foreach (var field in item.Fields)
            {
                RequireText(field.Name, $"{fieldName}.fields.name", errors);
                RequireText(field.Value, $"{fieldName}.fields.value", errors);
                if (IsPlayerOwnedIdentityMutationField(field.Name))
                    errors.Add($"{fieldName}.fields cannot mutate readonly player-owned soul identity field {field.Name}.");
                if (ContainsMortalWorldSubstituteText(field.Name) ||
                    ContainsMortalWorldSubstituteText(field.Value))
                {
                    errors.Add($"{fieldName}.fields contains a Mortal World substitute reference.");
                }
            }
        }
    }

    private static void ValidateGuardianAbodeProposalItems(
        IReadOnlyList<WorkerGuardianAbodeProposalItem> items,
        string fieldName,
        WorkerAfterlifeTaskContract? afterlifeContract,
        List<string> errors)
    {
        if (items.Count == 0)
            errors.Add($"{fieldName} must contain at least one Guardian/Abode proposal item.");

        foreach (var item in items)
        {
            ValidateId(item.ItemId, $"{fieldName}.itemId", errors);
            ValidateId(item.TargetId, $"{fieldName}.targetId", errors);
            RequireText(item.Title, $"{fieldName}.title", errors);
            RequireText(item.Summary, $"{fieldName}.summary", errors);
            RequireText(item.Visibility, $"{fieldName}.visibility", errors);

            if (ContainsMortalWorldSubstituteText(item.Title) ||
                ContainsMortalWorldSubstituteText(item.Summary) ||
                ContainsMortalWorldSubstituteText(item.Visibility))
            {
                errors.Add($"{fieldName} contains a Mortal World substitute reference.");
            }

            if (ContainsHiddenFactMarker(item.Title) || ContainsHiddenFactMarker(item.Summary))
            {
                if (!IsHiddenVisibility(item.Visibility))
                    errors.Add($"{fieldName} contains hidden or GM-only Guardian facts but visibility is not hidden/GM-only.");
            }

            if (item.TargetSurfaces.Count == 0)
                errors.Add($"{fieldName}.targetSurfaces must contain at least one exact afterlife surface.");
            foreach (var surface in item.TargetSurfaces)
            {
                ValidatePath(surface, $"{fieldName}.targetSurfaces", errors);
                if (IsMortalWorldSubstitutePath(surface))
                {
                    errors.Add($"{fieldName}.targetSurfaces contains a Mortal World substitute path: {surface}");
                    continue;
                }

                if (afterlifeContract != null &&
                    !afterlifeContract.AllowedAfterlifeSurfaces.Any(pattern => PathMatches(pattern, surface)))
                {
                    errors.Add($"{fieldName}.targetSurfaces contains a surface outside task.afterlifeContract.allowedAfterlifeSurfaces: {surface}");
                }
            }

            if (item.Fields.Count == 0)
                errors.Add($"{fieldName}.fields must contain at least one field for main-GM review.");
            foreach (var field in item.Fields)
            {
                RequireText(field.Name, $"{fieldName}.fields.name", errors);
                RequireText(field.Value, $"{fieldName}.fields.value", errors);
                if (ContainsMortalWorldSubstituteText(field.Name) ||
                    ContainsMortalWorldSubstituteText(field.Value))
                {
                    errors.Add($"{fieldName}.fields contains a Mortal World substitute reference.");
                }

                if (ContainsHiddenFactMarker(field.Value) && !IsHiddenVisibility(item.Visibility))
                    errors.Add($"{fieldName}.fields contains hidden or GM-only Guardian facts but visibility is not hidden/GM-only.");
            }
        }
    }

    private static void ValidateAfterlifeRequiredNames(
        IReadOnlyList<string> requiredByTask,
        IReadOnlyList<string> providedByProposal,
        string fieldName,
        List<string> errors)
    {
        foreach (var value in providedByProposal)
            RequireText(value, fieldName, errors);

        foreach (var required in requiredByTask.Where(value => !LooksLikeExplicitNone(value)))
        {
            if (!providedByProposal.Any(value => string.Equals(value, required, StringComparison.OrdinalIgnoreCase)))
                errors.Add($"{fieldName} must include required task value: {required}");
        }
    }

    private static void ValidateReadonlyIdentityFields(
        IReadOnlyList<string> providedFields,
        string fieldName,
        List<string> errors,
        IReadOnlyList<string>? requiredFields = null)
    {
        foreach (var field in providedFields)
            RequireText(field, fieldName, errors);

        var required = requiredFields is { Count: > 0 }
            ? requiredFields
            : ["soulName", "soulFormDescription"];

        foreach (var requiredField in required)
        {
            if (!providedFields.Any(field => string.Equals(field, requiredField, StringComparison.OrdinalIgnoreCase)))
                errors.Add($"{fieldName} must include readonly player-owned soul identity field {requiredField}.");
        }

        if (!providedFields.Any(field => string.Equals(field, "soulName", StringComparison.OrdinalIgnoreCase)))
            errors.Add($"{fieldName} must include readonly player-owned soul identity field soulName.");
        if (!providedFields.Any(field => string.Equals(field, "soulFormDescription", StringComparison.OrdinalIgnoreCase)))
            errors.Add($"{fieldName} must include readonly player-owned soul identity field soulFormDescription.");
    }

    private static bool IsPlayerOwnedIdentityMutationField(string fieldName) =>
        string.Equals(fieldName, "soulName", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(fieldName, "soulFormDescription", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(fieldName, "newSoulName", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(fieldName, "newSoulFormDescription", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(fieldName, "identityChange", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(fieldName, "identityOverride", StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikeExplicitNone(string value) =>
        value.Contains("none", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("no-", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("not required", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("не требуется", StringComparison.OrdinalIgnoreCase);

    private static void ValidateAuthoredEntity(
        WorkerAuthoredEntity entity,
        string fieldName,
        List<string> errors)
    {
        RequireText(entity.EntityType, $"{fieldName}.entityType", errors);
        ValidateId(entity.EntityId, $"{fieldName}.entityId", errors);
        RequireText(entity.DisplayName, $"{fieldName}.displayName", errors);
        RequireText(entity.Summary, $"{fieldName}.summary", errors);
        foreach (var requiredField in entity.RequiredFields)
        {
            RequireText(requiredField.Name, $"{fieldName}.requiredFields.name", errors);
            RequireText(requiredField.Value, $"{fieldName}.requiredFields.value", errors);
        }

        foreach (var relationship in entity.Relationships)
            RequireText(relationship, $"{fieldName}.relationships", errors);
    }

    private static void ValidateDomainAuthoringProposal(
        WorkerContentAuthoringProposal proposal,
        List<string> errors)
    {
        if (proposal.Domain == WorkerAuthoringDomain.Inventory)
            ValidateInventoryAuthoringProposal(proposal, errors);
        if (proposal.Domain == WorkerAuthoringDomain.Skill)
            ValidateSkillAuthoringProposal(proposal, errors);
        if (proposal.Domain == WorkerAuthoringDomain.Npc)
            ValidateNpcAuthoringProposal(proposal, errors);
        if (proposal.Domain == WorkerAuthoringDomain.GuardianAbode)
            ValidateGuardianAbodeAuthoringProposal(proposal, errors);
        if (proposal.Domain == WorkerAuthoringDomain.Soul)
            ValidateSoulAuthoringProposal(proposal, errors);
    }

    private static void ValidateInventoryAuthoringProposal(
        WorkerContentAuthoringProposal proposal,
        List<string> errors)
    {
        foreach (var entity in proposal.CreatedEntities.Concat(proposal.UpdatedEntities))
        {
            if (!string.Equals(entity.EntityType, "item", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"inventory authoring entity {entity.EntityId} must use entityType item.");
            }

            if (!HasAnyField(entity, "description", "playerFacingDescription", "player-facing-description", "displayDescription"))
            {
                errors.Add($"inventory authoring entity {entity.EntityId} must include a player-facing description field.");
            }

            if (!HasAnyField(entity, "value", "price", "cost", "quality", "rarity", "balanceNote", "balance-note"))
            {
                errors.Add($"inventory authoring entity {entity.EntityId} must include balance details such as value, price, quality, rarity, or balanceNote.");
            }

            if (!HasAnyField(entity, "owner", "ownerId", "inventoryOwner", "storage", "storageId", "container", "containerId") &&
                !HasText(entity.Relationships, "inventory", "storage", "container", "owner") &&
                !HasLinkForEntity(proposal.RequiredLinks, entity.EntityId, "inventory", "storage", "container", "owner"))
            {
                errors.Add($"inventory authoring entity {entity.EntityId} must include an owner/storage link reviewed by the main GM.");
            }

            if (LooksLikeReadableDocument(entity) &&
                !HasText(entity.Relationships, "readable", "content", "book", "document") &&
                !HasLinkForEntity(proposal.RequiredLinks, entity.EntityId, "readable", "content", "book", "document"))
            {
                errors.Add($"inventory document/book entity {entity.EntityId} must link to readable content or mark that content as a GM review gap.");
            }
        }
    }

    private static bool HasAnyField(WorkerAuthoredEntity entity, params string[] names) =>
        entity.RequiredFields.Any(field => names.Any(name =>
            string.Equals(field.Name, name, StringComparison.OrdinalIgnoreCase)));

    private static bool HasText(IEnumerable<string> values, params string[] fragments) =>
        values.Any(value => fragments.Any(fragment =>
            value.Contains(fragment, StringComparison.OrdinalIgnoreCase)));

    private static bool HasLinkForEntity(
        IReadOnlyList<WorkerRequiredEntityLink> links,
        string entityId,
        params string[] fragments) =>
        links.Any(link =>
            (string.Equals(link.Source, entityId, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(link.Target, entityId, StringComparison.OrdinalIgnoreCase)) &&
            (HasText([link.Source, link.Target, link.Reason], fragments)));

    private static bool LooksLikeReadableDocument(WorkerAuthoredEntity entity) =>
        HasText(
            [
                entity.EntityType,
                entity.DisplayName,
                entity.Summary,
                .. entity.RequiredFields.Select(field => field.Name),
                .. entity.RequiredFields.Select(field => field.Value)
            ],
            "book",
            "document",
            "книга",
            "документ");

    private static void ValidateSkillAuthoringProposal(
        WorkerContentAuthoringProposal proposal,
        List<string> errors)
    {
        foreach (var entity in proposal.CreatedEntities.Concat(proposal.UpdatedEntities))
        {
            if (!string.Equals(entity.EntityType, "skill", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(entity.EntityType, "effect", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"skill authoring entity {entity.EntityId} must use entityType skill or effect.");
            }

            if (entity.Summary.Trim().Length < 40)
                errors.Add($"skill authoring entity {entity.EntityId} must include a detailed player-facing summary, not a short bonus label.");

            if (!HasDetailedField(entity, 40, "description", "playerFacingDescription", "player-facing-description", "displayDescription"))
                errors.Add($"skill authoring entity {entity.EntityId} must include a detailed player-facing description field.");

            var hasScalingAttribute = HasAnyField(entity, "scalingAttribute", "scalingStat", "characteristic", "attribute");
            var hasNoScalingReason = HasAnyField(entity, "noScalingReason", "no-scaling-reason");
            if (!hasScalingAttribute && !hasNoScalingReason)
                errors.Add($"skill authoring entity {entity.EntityId} must include scalingAttribute or noScalingReason.");
            if (hasScalingAttribute && !HasAnyField(entity, "localizedScalingAttribute", "scalingAttributeRu", "localizedCharacteristic", "attributeRu"))
                errors.Add($"skill authoring entity {entity.EntityId} must include localized scaling attribute text.");
            if (!HasDetailedField(entity, 30, "scalingExplanation", "scalingRule", "noScalingReason", "no-scaling-reason"))
                errors.Add($"skill authoring entity {entity.EntityId} must include a readable scaling explanation.");

            if (HasAnyField(entity, "bonus", "structuredBonus", "mechanicalBonus") &&
                !HasDetailedField(entity, 30, "bonusExplanation", "structuredBonusExplanation", "mechanicalBonusExplanation"))
            {
                errors.Add($"skill authoring entity {entity.EntityId} must include player-facing bonus explanation for every proposed bonus.");
            }

            if (!HasText(entity.Relationships, "effect", "status", "combat", "characteristic", "check", "skill") &&
                !HasLinkForEntity(proposal.RequiredLinks, entity.EntityId, "effect", "status", "combat", "characteristic", "check", "skill"))
            {
                errors.Add($"skill authoring entity {entity.EntityId} must link to effects, status, combat, characteristic checks, or skill progression surfaces.");
            }
        }
    }

    private static bool HasDetailedField(WorkerAuthoredEntity entity, int minLength, params string[] names) =>
        entity.RequiredFields.Any(field =>
            names.Any(name => string.Equals(field.Name, name, StringComparison.OrdinalIgnoreCase)) &&
            field.Value.Trim().Length >= minLength);

    private static void ValidateNpcAuthoringProposal(
        WorkerContentAuthoringProposal proposal,
        List<string> errors)
    {
        foreach (var entity in proposal.CreatedEntities.Concat(proposal.UpdatedEntities))
        {
            if (!string.Equals(entity.EntityType, "npc", StringComparison.OrdinalIgnoreCase))
                errors.Add($"npc authoring entity {entity.EntityId} must use entityType npc.");

            if (entity.Summary.Trim().Length < 50)
                errors.Add($"npc authoring entity {entity.EntityId} must include a useful player-facing summary, not only a role label.");

            if (!HasDetailedField(entity, 40, "description", "playerFacingDescription", "player-facing-description", "displayDescription"))
                errors.Add($"npc authoring entity {entity.EntityId} must include a detailed player-facing description.");
            if (!HasDetailedField(entity, 30, "publicKnowledge", "public-knowledge"))
                errors.Add($"npc authoring entity {entity.EntityId} must include public knowledge visible or discoverable by the player.");
            if (!HasDetailedField(entity, 30, "privateKnowledge", "private-knowledge", "secrets"))
                errors.Add($"npc authoring entity {entity.EntityId} must include private knowledge or secrets for main-GM review.");
            if (!HasDetailedField(entity, 30, "thoughtJournal", "thoughts", "thoughtEntries"))
                errors.Add($"npc authoring entity {entity.EntityId} must include thought journal entries as a separate linked section.");
            if (!HasDetailedField(entity, 30, "relationshipHooks", "relationships", "relationshipGates"))
                errors.Add($"npc authoring entity {entity.EntityId} must include relationship hooks as a separate linked section.");
            if (!HasDetailedField(entity, 30, "personalQuests", "personalQuest", "questHooks"))
                errors.Add($"npc authoring entity {entity.EntityId} must include personal quest hooks as a separate linked section.");
            if (!HasDetailedField(entity, 30, "dialogueSeeds", "dialogueOptions", "dialogueHooks"))
                errors.Add($"npc authoring entity {entity.EntityId} must include dialogue seeds for player interaction.");
            if (!HasDetailedField(entity, 30, "detailSurfaces", "detailCommands", "menuSurfaces"))
                errors.Add($"npc authoring entity {entity.EntityId} must list detail menu/command surfaces that reveal thoughts, quests, relationships, and dialogue details.");

            if (!HasText(entity.Relationships, "location", "scene") &&
                !HasLinkForEntity(proposal.RequiredLinks, entity.EntityId, "location", "scene"))
            {
                errors.Add($"npc authoring entity {entity.EntityId} must link to a current location or scene.");
            }

            if (!HasText(entity.Relationships, "faction", "quest", "relationship", "thought", "dialogue") &&
                !HasLinkForEntity(proposal.RequiredLinks, entity.EntityId, "faction", "quest", "relationship", "thought", "dialogue"))
            {
                errors.Add($"npc authoring entity {entity.EntityId} must link NPC details to factions, quests, relationships, thoughts, or dialogue surfaces.");
            }
        }
    }

    private static void ValidateGuardianAbodeAuthoringProposal(
        WorkerContentAuthoringProposal proposal,
        List<string> errors)
    {
        foreach (var entity in proposal.CreatedEntities.Concat(proposal.UpdatedEntities))
        {
            if (IsMortalGuardianSubstituteEntityType(entity.EntityType))
            {
                errors.Add($"guardian-abode authoring entity {entity.EntityId} must use Guardian/Abode afterlife entity types, not Mortal NPC/faction substitutes.");
            }
            else if (!IsGuardianAbodeEntityType(entity.EntityType))
            {
                errors.Add($"guardian-abode authoring entity {entity.EntityId} must use a Guardian/Abode entityType such as guardian, abode, guardian-project, guardian-politics, trade-favor, or dossier-note.");
            }

            if (!HasAnyField(entity, "playerFacingSummary", "player-facing-summary", "playerVisibleSummary", "player-visible-summary"))
                errors.Add($"guardian-abode authoring entity {entity.EntityId} must include playerFacingSummary.");
            if (!HasAnyField(entity, "gmOnlyHiddenFacts", "gm-only-hidden-facts", "hiddenFacts", "hidden-facts"))
                errors.Add($"guardian-abode authoring entity {entity.EntityId} must include gmOnlyHiddenFacts for main-GM review.");
            if (!HasAnyField(entity, "exactAfterlifeSurfaces", "exact-afterlife-surfaces", "targetSurfaces", "target-surfaces"))
                errors.Add($"guardian-abode authoring entity {entity.EntityId} must include exactAfterlifeSurfaces.");

            if (ContainsMortalWorldSubstituteText(entity.EntityType) ||
                ContainsMortalWorldSubstituteText(entity.DisplayName) ||
                ContainsMortalWorldSubstituteText(entity.Summary) ||
                entity.Relationships.Any(ContainsMortalWorldSubstituteText) ||
                entity.RequiredFields.Any(field =>
                    ContainsMortalWorldSubstituteText(field.Name) ||
                    ContainsMortalWorldSubstituteText(field.Value)))
            {
                errors.Add($"guardian-abode authoring entity {entity.EntityId} contains a Mortal World substitute reference.");
            }
        }
    }

    private static void ValidateSoulAuthoringProposal(
        WorkerContentAuthoringProposal proposal,
        List<string> errors)
    {
        foreach (var entity in proposal.CreatedEntities.Concat(proposal.UpdatedEntities))
        {
            if (IsMortalSoulSubstituteEntityType(entity.EntityType))
            {
                errors.Add($"soul authoring entity {entity.EntityId} must use soul/afterlife entity types, not ordinary character/inventory/state substitutes.");
            }
            else if (!IsSoulEntityType(entity.EntityType))
            {
                errors.Add($"soul authoring entity {entity.EntityId} must use a soul entityType such as soul-summary, soul-progression, soul-reward, next-life-prep, soul-facing-note, or soul-archive-hook.");
            }

            if (!HasAnyField(entity, "playerFacingSummary", "player-facing-summary", "playerVisibleSummary", "player-visible-summary"))
                errors.Add($"soul authoring entity {entity.EntityId} must include playerFacingSummary.");
            if (!HasAnyField(entity, "exactAfterlifeSurfaces", "exact-afterlife-surfaces", "targetSurfaces", "target-surfaces"))
                errors.Add($"soul authoring entity {entity.EntityId} must include exactAfterlifeSurfaces.");
            if (!HasAnyField(entity, "readonlyIdentityFields", "readonly-identity-fields", "forbiddenReadonlyFields", "forbidden-readonly-fields"))
                errors.Add($"soul authoring entity {entity.EntityId} must include readonlyIdentityFields.");

            foreach (var field in entity.RequiredFields)
            {
                if (IsPlayerOwnedIdentityMutationField(field.Name))
                    errors.Add($"soul authoring entity {entity.EntityId} cannot mutate readonly player-owned soul identity field {field.Name}.");
            }

            if (ContainsMortalWorldSubstituteText(entity.EntityType) ||
                ContainsMortalWorldSubstituteText(entity.DisplayName) ||
                ContainsMortalWorldSubstituteText(entity.Summary) ||
                entity.Relationships.Any(ContainsMortalWorldSubstituteText) ||
                entity.RequiredFields.Any(field =>
                    ContainsMortalWorldSubstituteText(field.Name) ||
                    ContainsMortalWorldSubstituteText(field.Value)))
            {
                errors.Add($"soul authoring entity {entity.EntityId} contains a Mortal World substitute reference.");
            }
        }
    }

    private static bool IsGuardianAbodeEntityType(string entityType) =>
        string.Equals(entityType, "guardian", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(entityType, "abode", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(entityType, "guardian-project", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(entityType, "guardian-politics", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(entityType, "trade-favor", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(entityType, "dossier-note", StringComparison.OrdinalIgnoreCase);

    private static bool IsMortalGuardianSubstituteEntityType(string entityType) =>
        string.Equals(entityType, "npc", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(entityType, "mortal-npc", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(entityType, "faction", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(entityType, "mortal-faction", StringComparison.OrdinalIgnoreCase);

    private static bool IsSoulEntityType(string entityType) =>
        string.Equals(entityType, "soul-summary", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(entityType, "soul-progression", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(entityType, "soul-reward", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(entityType, "next-life-prep", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(entityType, "soul-facing-note", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(entityType, "soul-archive-hook", StringComparison.OrdinalIgnoreCase);

    private static bool IsMortalSoulSubstituteEntityType(string entityType) =>
        string.Equals(entityType, "character", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(entityType, "player-character", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(entityType, "mortal-character", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(entityType, "inventory", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(entityType, "item", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(entityType, "mortal-item", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(entityType, "state", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(entityType, "mortal-state", StringComparison.OrdinalIgnoreCase);

    private static WorkerAuthoringDomain? TaskTypeToDomain(WorkerTaskType taskType) =>
        taskType switch
        {
            WorkerTaskType.InventoryContent => WorkerAuthoringDomain.Inventory,
            WorkerTaskType.SkillContent => WorkerAuthoringDomain.Skill,
            WorkerTaskType.NpcContent => WorkerAuthoringDomain.Npc,
            WorkerTaskType.GuardianAbodeContent => WorkerAuthoringDomain.GuardianAbode,
            WorkerTaskType.SoulContent => WorkerAuthoringDomain.Soul,
            WorkerTaskType.SocialDialogueContent => WorkerAuthoringDomain.SocialDialogue,
            WorkerTaskType.FactionContent => WorkerAuthoringDomain.Faction,
            WorkerTaskType.LocationContent => WorkerAuthoringDomain.Location,
            WorkerTaskType.QuestContent => WorkerAuthoringDomain.Quest,
            WorkerTaskType.BookDocumentContent => WorkerAuthoringDomain.BookDocument,
            WorkerTaskType.EconomyCraftingContent => WorkerAuthoringDomain.EconomyCrafting,
            WorkerTaskType.WorldStateContent => WorkerAuthoringDomain.WorldState,
            WorkerTaskType.EncounterContent => WorkerAuthoringDomain.Encounter,
            WorkerTaskType.QteContent => WorkerAuthoringDomain.Qte,
            _ => null
        };

    private static bool TaskLooksAfterlifeScoped(WorkerTaskPacket task)
    {
        var values = new List<string> { task.Instructions };
        values.AddRange(task.ContextFiles.Select(file => file.Path));
        values.AddRange(task.AcceptanceCriteria);

        if (task.AuthoringRequest != null)
        {
            values.Add(task.AuthoringRequest.Goal);
            values.AddRange(task.AuthoringRequest.RequiredLinks);
            values.AddRange(task.AuthoringRequest.OutputNotes);
            values.AddRange(task.AuthoringRequest.EntityHints);
        }

        if (task.DraftRequest != null)
        {
            values.Add(task.DraftRequest.SceneGoal);
            values.Add(task.DraftRequest.Tone);
            values.Add(task.DraftRequest.TargetLength);
            values.AddRange(task.DraftRequest.ContinuityNotes);
        }

        return values.Any(value =>
            value.Contains("afterlife", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("Chaos Sea", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("Shining Abode", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("Afterlife_Contract_Matrix", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("game_state/meta/guardians", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("game_state/meta/afterlife", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("game_state/meta/soul_state", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsMortalWorldSubstitutePath(string path) =>
        path.StartsWith("game_state/world/", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("game_state/npcs/", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("game_state/factions/", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("game_state/player/", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("game_state/inventory/", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("game_state/combat/", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("game_state/quests/", StringComparison.OrdinalIgnoreCase);

    private static bool ContainsMortalWorldSubstituteText(string value)
    {
        if (value.Contains("worldStateFlags", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("worldEventsLog", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("UpdateCharacter", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("UpdateNPCs", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("NPCRelationshipChanges", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("factionDataChanges", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("factionProjectUpdates", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("completeFactionProjects", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("factionChronicleUpdates", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("worldMapUpdates", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("currentLocationData", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("game_state/world", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("game_state/npcs", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("game_state/factions", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("game_state/player", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("game_state/inventory", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("game_state/combat", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("Mortal NPC", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("Mortal character", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("Mortal inventory", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("Mortal combat", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("Mortal faction", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("Mortal map", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return ContainsAll(value, "смертн", "журнал") ||
               ContainsAll(value, "смертн", "флаг") ||
               ContainsAll(value, "смертн", "фракц") ||
               ContainsAll(value, "смертн", "npc") ||
               ContainsAll(value, "смертн", "нпс") ||
               ContainsAll(value, "смертн", "отношен") ||
               ContainsAll(value, "смертн", "карта") ||
               ContainsAll(value, "смертн", "бой") ||
               ContainsAll(value, "смертн", "hp");
    }

    private static bool ContainsHiddenFactMarker(string value) =>
        value.Contains("GM-only", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("gm only", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("hidden", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("secret", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("скрыт", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("тайн", StringComparison.OrdinalIgnoreCase);

    private static bool IsHiddenVisibility(string value) =>
        string.Equals(value, "hidden", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, "gm-only", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, "gm_only", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, "private", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, "secret", StringComparison.OrdinalIgnoreCase);

    private static bool ContainsAll(string value, params string[] fragments) =>
        fragments.All(fragment => value.Contains(fragment, StringComparison.OrdinalIgnoreCase));
}
