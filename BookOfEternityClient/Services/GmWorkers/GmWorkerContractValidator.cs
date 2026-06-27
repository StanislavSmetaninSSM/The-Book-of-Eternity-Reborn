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
        }
        else if (task.AuthoringRequest != null)
        {
            errors.Add("authoringRequest is only allowed for content-authoring tasks.");
        }

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

    private static WorkerAuthoringDomain? TaskTypeToDomain(WorkerTaskType taskType) =>
        taskType switch
        {
            WorkerTaskType.InventoryContent => WorkerAuthoringDomain.Inventory,
            WorkerTaskType.SkillContent => WorkerAuthoringDomain.Skill,
            WorkerTaskType.NpcContent => WorkerAuthoringDomain.Npc,
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
}
