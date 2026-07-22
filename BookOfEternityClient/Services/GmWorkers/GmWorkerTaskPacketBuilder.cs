using BookOfEternityClient.Services;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services.GmWorkers;

public static class GmWorkerTaskPacketBuilder
{
    public static WorkerTaskPacket BuildValidationRepairTask(
        WorkerBridgeProfile profile,
        string taskId,
        WorkerTurnReference sourceTurn,
        IReadOnlyList<ValidationIssue> validationIssues,
        IReadOnlyDictionary<string, string> contextFileHashes,
        string createdAtUtc,
        WorkerAfterlifeTaskContract? afterlifeContract = null)
    {
        var profileValidation = GmWorkerContractValidator.ValidateProfile(profile);
        if (!profileValidation.IsValid)
            throw new ArgumentException(string.Join(Environment.NewLine, profileValidation.Errors), nameof(profile));
        if (!profile.Permissions.TaskTypes.Contains(WorkerTaskType.ValidationRepair))
            throw new ArgumentException("Worker profile cannot handle validation-repair tasks.", nameof(profile));
        if (validationIssues.Count == 0)
            throw new ArgumentException("At least one validation issue is required.", nameof(validationIssues));

        ValidateMortalContinuityDispatchPolicy(validationIssues);

        var requiresCharacteristicAuthority = validationIssues.Any(issue => string.Equals(
            issue.Code,
            "npc_characteristics_empty",
            StringComparison.OrdinalIgnoreCase));
        if (requiresCharacteristicAuthority &&
            (!contextFileHashes.TryGetValue(MortalCharacteristicAuthorityContract.StatePath, out var authorityHash) ||
             string.IsNullOrWhiteSpace(authorityHash) ||
             string.Equals(authorityHash, "missing", StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException(
                $"Characteristics repair requires hash-pinned setting authority at {MortalCharacteristicAuthorityContract.StatePath}.",
                nameof(contextFileHashes));
        }

        var allowedPaths = validationIssues
            .Select(ResolveValidationTargetPath)
            .Where(GmWorkerContractValidator.IsSafeRelativePath)
            .Where(path => profile.Permissions.ProposalWritePaths.Any(pattern => GmWorkerContractValidator.PathMatches(pattern, path)))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        if (allowedPaths.Length == 0)
            throw new ArgumentException("Validation issues do not map to any safe worker proposal path.", nameof(validationIssues));

        var contextPaths = requiresCharacteristicAuthority
            ? allowedPaths.Append(MortalCharacteristicAuthorityContract.StatePath)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray()
            : allowedPaths;
        var contextFiles = contextPaths
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
            Role = profile.Role,
            TaskType = WorkerTaskType.ValidationRepair,
            CreatedAtUtc = createdAtUtc,
            TimeoutSeconds = profile.TimeoutSeconds,
            SourceTurn = sourceTurn,
            ValidationIssues = validationIssues.Select(issue => new WorkerValidationIssue
            {
                Code = string.IsNullOrWhiteSpace(issue.Code) ? "validation_issue" : issue.Code!,
                Path = issue.FilePath.Replace('\\', '/'),
                Message = issue.Message,
                Actor = issue.Actor,
                Section = issue.Section,
                Expected = issue.Expected,
                Actual = issue.Actual
            }).ToArray(),
            ContextFiles = contextFiles,
            AfterlifeContract = afterlifeContract,
            AllowedProposalPaths = allowedPaths,
            AcceptanceCriteria =
            [
                "Return a worker-proposal-v1 JSON proposal.",
                "Include changedFiles only for allowedProposalPaths.",
                "Validation must pass after the apply gate applies proposed changes.",
                "For actor materialization repair, preserve protected actor data and change only the exact actor/section coordinates carried by validationIssues.",
                "For Mortal characteristics repair, use only keys from the hash-pinned read-only setting authority in game_state/misc/characteristics.json.",
                "Keep session/request/turn metadata tied to sourceTurn."
            ],
            ForbiddenActions =
            [
                "Do not edit canonical game_session files directly.",
                "Do not write outside allowedProposalPaths.",
                "Do not rewrite untargeted actor fields, untargeted actors, or unrelated canonical root data.",
                "Do not create terminal signals or validation ready files manually."
            ],
            Instructions =
                "Return a worker-proposal-v1 JSON proposal. Include changedFiles only for allowedProposalPaths. " +
                "Use validationIssues actor/section/expected/actual coordinates as the exact repair scope. " +
                "Do not edit canonical game_session files directly." +
                BuildAfterlifeInstructions(afterlifeContract, validationRepair: true)
        };

        var taskValidation = GmWorkerContractValidator.ValidateTaskPacket(task, profile);
        if (!taskValidation.IsValid)
            throw new InvalidOperationException(string.Join(Environment.NewLine, taskValidation.Errors));

        return task;
    }

    internal static string ResolveValidationTargetPath(ValidationIssue issue)
    {
        var actorMaterializationAuthority = ResolveActorMaterializationAuthorityPath(issue);
        if (actorMaterializationAuthority != null)
            return actorMaterializationAuthority;

        var code = issue.Code ?? string.Empty;
        var actor = issue.Actor ?? string.Empty;
        var normalized = issue.FilePath.Replace('\\', '/');
        var jsonlEnd = FindExtensionEnd(normalized, ".jsonl");
        var jsonEnd = FindExtensionEnd(normalized, ".json");
        var end = jsonlEnd >= 0 ? jsonlEnd : jsonEnd;
        var filePath = end >= 0 ? normalized[..end] : normalized;
        if (code.Contains("actor_materialization", StringComparison.OrdinalIgnoreCase) &&
            GmWorkerContractValidator.IsSafeRelativePath(filePath))
        {
            return filePath;
        }

        if (actor.StartsWith("mortal_npc:", StringComparison.Ordinal) &&
            code.Contains("actor_materialization", StringComparison.OrdinalIgnoreCase))
        {
            return "game_state/npcs/npc_core.json";
        }

        return filePath;
    }

    internal static string? ResolveActorMaterializationAuthorityPath(ValidationIssue issue)
    {
        var code = issue.Code ?? string.Empty;
        if ((code is "npc_existing_inventory_resend_forbidden" or "npc_characteristics_empty") &&
            issue.Actor?.StartsWith("mortal_npc:", StringComparison.Ordinal) == true)
        {
            return "game_state/npcs/npc_core.json";
        }

        if (!code.Contains("actor_materialization", StringComparison.OrdinalIgnoreCase))
            return null;

        var actor = issue.Actor ?? string.Empty;
        if (actor.StartsWith("mortal_npc:", StringComparison.Ordinal))
            return "game_state/npcs/npc_core.json";
        if (code is "afterlife_actor_materialization_profile_missing" or
            "afterlife_actor_materialization_profile_ambiguous")
        {
            return AfterlifeEntityProfileState.StatePath;
        }

        if (!string.Equals(
                code,
                "afterlife_actor_materialization_memory_missing",
                StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (actor.StartsWith("guardian:", StringComparison.Ordinal))
            return GuardianThoughtJournalState.StatePath;
        if (actor.StartsWith("resident:", StringComparison.Ordinal))
            return GuardianAbodeResidentState.StatePath;
        if (actor.StartsWith("radiant_actor:", StringComparison.Ordinal) ||
            actor.StartsWith("saref_agent:", StringComparison.Ordinal) ||
            actor.Contains(':', StringComparison.Ordinal))
        {
            return AfterlifeEntityProfileState.StatePath;
        }

        return null;
    }

    private static void ValidateMortalContinuityDispatchPolicy(
        IReadOnlyList<ValidationIssue> validationIssues)
    {
        foreach (var issue in validationIssues)
        {
            if (!TryGetMortalContinuitySection(issue.Code, out var expectedSection))
                continue;

            var actor = issue.Actor ?? string.Empty;
            if (!actor.StartsWith("mortal_npc:", StringComparison.Ordinal) ||
                actor.Length == "mortal_npc:".Length ||
                !string.Equals(issue.Section, expectedSection, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    $"{issue.Code} requires exact Mortal actor and section metadata; use the main GM rollback/repair path.",
                    nameof(validationIssues));
            }
        }

        if (validationIssues.Any(issue => string.Equals(
                issue.Code,
                "npc_initial_id_collides_with_existing_permanent_id",
                StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException(
                "NPC identity collisions require the main GM rollback/repair path.",
                nameof(validationIssues));
        }

        if (validationIssues.Any(issue =>
                string.Equals(
                    issue.Code,
                    "npc_existing_inventory_resend_forbidden",
                    StringComparison.OrdinalIgnoreCase) &&
                !IsExactInventorySnapshot(issue.Expected)))
        {
            throw new ArgumentException(
                "Existing-NPC inventory repair without an exact validated pre-turn snapshot requires the main GM rollback/repair path.",
                nameof(validationIssues));
        }
    }

    private static bool TryGetMortalContinuitySection(string? code, out string section)
    {
        if (string.Equals(code, "npc_initial_id_collides_with_existing_permanent_id", StringComparison.OrdinalIgnoreCase))
            section = "NPCIdentity";
        else if (string.Equals(code, "npc_existing_inventory_resend_forbidden", StringComparison.OrdinalIgnoreCase))
            section = "NPCInventory";
        else if (string.Equals(code, "npc_characteristics_empty", StringComparison.OrdinalIgnoreCase))
            section = "NPCCharacteristics";
        else
        {
            section = string.Empty;
            return false;
        }

        return true;
    }

    private static bool IsExactInventorySnapshot(string? expected)
    {
        if (string.IsNullOrWhiteSpace(expected))
            return false;

        try
        {
            return JsonNode.Parse(expected) is JsonArray;
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException)
        {
            return false;
        }
    }

    private static int FindExtensionEnd(string path, string extension)
    {
        var index = path.IndexOf(extension, StringComparison.OrdinalIgnoreCase);
        return index < 0 ? -1 : index + extension.Length;
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
            Role = profile.Role,
            TaskType = WorkerTaskType.NarrativeDraft,
            CreatedAtUtc = createdAtUtc,
            TimeoutSeconds = profile.TimeoutSeconds,
            SourceTurn = sourceTurn,
            DraftRequest = draftRequest,
            ContextFiles = contextFiles,
            AllowedProposalPaths = [],
            AcceptanceCriteria =
            [
                "Return a worker-proposal-v1 JSON proposal.",
                "Include draftText for main-GM review.",
                "Use findings only for compact notes the main GM can accept, reject, or rewrite.",
                "Do not resolve the player turn or assert canonical state changes."
            ],
            ForbiddenActions =
            [
                "Do not edit canonical game_session files directly.",
                "Do not include changedFiles.",
                "Do not write player-facing output or terminal signals."
            ],
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
        string createdAtUtc,
        WorkerAfterlifeTaskContract? afterlifeContract = null)
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
            Role = profile.Role,
            TaskType = WorkerTaskType.Analysis,
            CreatedAtUtc = createdAtUtc,
            TimeoutSeconds = profile.TimeoutSeconds,
            SourceTurn = sourceTurn,
            ContextFiles = contextFiles,
            AfterlifeContract = afterlifeContract,
            AllowedProposalPaths = [],
            AcceptanceCriteria = BuildAcceptanceCriteria(
            [
                "Return a worker-proposal-v1 JSON proposal.",
                "Include findings that answer the requested questions.",
                "Keep recommendations scoped to supplied read-only context references.",
                "Mark uncertainty instead of broad source spelunking."
            ],
            afterlifeContract),
            ForbiddenActions = BuildForbiddenActions(
            [
                "Do not edit canonical game_session files directly.",
                "Do not include changedFiles.",
                "Do not write player-facing output or terminal signals."
            ],
            afterlifeContract),
            Instructions =
                "Return a worker-proposal-v1 JSON proposal with findings only. " +
                "This is proposal-only: do not include changedFiles and do not edit canonical game_session files directly. " +
                $"Analysis goal: {analysisGoal}{Environment.NewLine}Questions:{Environment.NewLine}{questionText}" +
                BuildAfterlifeInstructions(afterlifeContract)
        };

        var taskValidation = GmWorkerContractValidator.ValidateTaskPacket(task, profile);
        if (!taskValidation.IsValid)
            throw new InvalidOperationException(string.Join(Environment.NewLine, taskValidation.Errors));

        return task;
    }

    public static WorkerTaskPacket BuildContentAuthoringTask(
        WorkerBridgeProfile profile,
        WorkerTaskType taskType,
        string taskId,
        WorkerTurnReference sourceTurn,
        WorkerContentAuthoringRequest authoringRequest,
        IReadOnlyList<WorkerFileReference> contextFiles,
        string createdAtUtc,
        WorkerAfterlifeTaskContract? afterlifeContract = null,
        WorkerGuardianAbodeRequest? guardianAbodeRequest = null,
        WorkerSoulContentRequest? soulContentRequest = null)
    {
        var profileValidation = GmWorkerContractValidator.ValidateProfile(profile);
        if (!profileValidation.IsValid)
            throw new ArgumentException(string.Join(Environment.NewLine, profileValidation.Errors), nameof(profile));
        if (!WorkerTaskTypes.IsContentAuthoring(taskType))
            throw new ArgumentException("Task type must be a content-authoring task.", nameof(taskType));
        if (!profile.Permissions.TaskTypes.Contains(taskType))
            throw new ArgumentException($"Worker profile cannot handle {taskType} tasks.", nameof(profile));
        if (!profile.Permissions.ProposalOnly)
            throw new ArgumentException("Content-authoring workers must be proposal-only.", nameof(profile));
        if (string.IsNullOrWhiteSpace(authoringRequest.Goal))
            throw new ArgumentException("Authoring goal is required.", nameof(authoringRequest));

        var hintsText = authoringRequest.EntityHints.Count == 0
            ? "No entity hints were provided."
            : string.Join(Environment.NewLine, authoringRequest.EntityHints.Select((hint, index) => $"{index + 1}. {hint}"));
        var linksText = authoringRequest.RequiredLinks.Count == 0
            ? "No required links were provided."
            : string.Join(Environment.NewLine, authoringRequest.RequiredLinks.Select((link, index) => $"{index + 1}. {link}"));
        var notesText = authoringRequest.OutputNotes.Count == 0
            ? "No additional output notes were provided."
            : string.Join(Environment.NewLine, authoringRequest.OutputNotes.Select((note, index) => $"{index + 1}. {note}"));

        var task = new WorkerTaskPacket
        {
            TaskId = taskId,
            WorkerId = profile.WorkerId,
            Role = profile.Role,
            TaskType = taskType,
            CreatedAtUtc = createdAtUtc,
            TimeoutSeconds = profile.TimeoutSeconds,
            SourceTurn = sourceTurn,
            AuthoringRequest = authoringRequest,
            GuardianAbodeRequest = guardianAbodeRequest,
            SoulContentRequest = soulContentRequest,
            ContextFiles = contextFiles,
            AfterlifeContract = afterlifeContract,
            AllowedProposalPaths = [],
            AcceptanceCriteria = BuildSoulContentAcceptanceCriteria(
                BuildGuardianAbodeAcceptanceCriteria(
                    BuildAcceptanceCriteria(
            [
                "Return a worker-proposal-v1 JSON proposal.",
                "Include a structured authoringProposal with createdEntities or updatedEntities.",
                "Include requiredLinks, validatorRisks, and gmReviewNotes for main-GM review.",
                "Do not apply or persist any entity changes yourself."
            ],
            afterlifeContract),
                    guardianAbodeRequest),
                soulContentRequest),
            ForbiddenActions = BuildSoulContentForbiddenActions(
                BuildGuardianAbodeForbiddenActions(
                    BuildForbiddenActions(
            [
                "Do not edit canonical game_session files directly.",
                "Do not include changedFiles.",
                "Do not write player-facing output or terminal signals.",
                "Do not invent hidden authority beyond supplied context references."
            ],
            afterlifeContract),
                    guardianAbodeRequest),
                soulContentRequest),
            Instructions =
                "Return a worker-proposal-v1 JSON proposal with authoringProposal. " +
                "This is proposal-only: do not include changedFiles and do not edit canonical game_session files directly. " +
                $"Authoring domain: {authoringRequest.Domain}. Goal: {authoringRequest.Goal}{Environment.NewLine}" +
                $"Entity hints:{Environment.NewLine}{hintsText}{Environment.NewLine}" +
                $"Required links:{Environment.NewLine}{linksText}{Environment.NewLine}" +
                $"Output notes:{Environment.NewLine}{notesText}" +
                BuildAfterlifeInstructions(afterlifeContract) +
                BuildGuardianAbodeInstructions(guardianAbodeRequest) +
                BuildSoulContentInstructions(soulContentRequest)
        };

        var taskValidation = GmWorkerContractValidator.ValidateTaskPacket(task, profile);
        if (!taskValidation.IsValid)
            throw new InvalidOperationException(string.Join(Environment.NewLine, taskValidation.Errors));

        return task;
    }

    private static IReadOnlyList<string> BuildAcceptanceCriteria(
        IReadOnlyList<string> baseCriteria,
        WorkerAfterlifeTaskContract? afterlifeContract)
    {
        if (afterlifeContract == null)
            return baseCriteria;

        return baseCriteria.Concat(
        [
            "Include afterlifeProposal with realmGate, targetSurfaces, requiredReceipts, requiredReports, playerVisibleSummary, gmReviewNotes, and validatorRisks.",
            "Use only afterlife state surfaces listed in afterlifeContract.allowedAfterlifeSurfaces.",
            "Review OtherGuides/Afterlife_Contract_Matrix.md before recommending afterlife state changes."
        ]).ToArray();
    }

    private static IReadOnlyList<string> BuildForbiddenActions(
        IReadOnlyList<string> baseActions,
        WorkerAfterlifeTaskContract? afterlifeContract)
    {
        if (afterlifeContract == null)
            return baseActions;

        var forbiddenSubstitutes = afterlifeContract.ForbiddenMortalSubstitutes.Count == 0
            ? "worldStateFlags, worldEventsLog, Mortal NPC relationships, Mortal combat HP/status, Mortal factions, or Mortal map files"
            : FormatList(afterlifeContract.ForbiddenMortalSubstitutes);

        return baseActions.Concat(
        [
            $"Do not use Mortal World substitutes for afterlife state: {forbiddenSubstitutes}.",
            "Do not propose afterlife changes outside the realm gate and allowed afterlife surfaces."
        ]).ToArray();
    }

    private static string BuildAfterlifeInstructions(
        WorkerAfterlifeTaskContract? afterlifeContract,
        bool validationRepair = false)
    {
        if (afterlifeContract == null)
            return "";

        return Environment.NewLine +
            "Afterlife realm-aware contract:" + Environment.NewLine +
            $"- realmGate: {afterlifeContract.RealmGate}" + Environment.NewLine +
            $"- currentRealm: {afterlifeContract.CurrentRealm}" + Environment.NewLine +
            $"- progressionControlPaths: {FormatList(afterlifeContract.ProgressionControlPaths)}" + Environment.NewLine +
            $"- pendingControlFiles: {FormatList(afterlifeContract.PendingControlFiles)}" + Environment.NewLine +
            $"- allowedAfterlifeSurfaces: {FormatList(afterlifeContract.AllowedAfterlifeSurfaces)}" + Environment.NewLine +
            $"- requiredReceipts: {FormatList(afterlifeContract.RequiredReceipts)}" + Environment.NewLine +
            $"- requiredReports: {FormatList(afterlifeContract.RequiredReports)}" + Environment.NewLine +
            $"- forbiddenMortalSubstitutes: {FormatList(afterlifeContract.ForbiddenMortalSubstitutes)}" + Environment.NewLine +
            (validationRepair
                ? "For this bounded validation-repair task, changedFiles must stay inside allowedAfterlifeSurfaces and afterlifeProposal is optional. "
                : "Return afterlifeProposal when this contract is present. ") +
            "Use Afterlife_Contract_Matrix.md for exact state-surface meaning.";
    }

    private static IReadOnlyList<string> BuildGuardianAbodeAcceptanceCriteria(
        IReadOnlyList<string> baseCriteria,
        WorkerGuardianAbodeRequest? guardianAbodeRequest)
    {
        if (guardianAbodeRequest == null)
            return baseCriteria;

        return baseCriteria.Concat(
        [
            "Include guardianAbodeProposal with guardianUpdates, abodeUpdates, projectSuggestions, powerReputationConsequences, tradeFavorHooks, dossierNotes, requiredReceipts, requiredReports, validatorRisks, and gmReviewNotes.",
            "Keep GM-only hidden Guardian facts out of playerVisibleSummary and visible proposal items.",
            "Use exact Guardian/Abode/project/politics afterlife surfaces; do not rewrite the task as Mortal NPC or Mortal faction updates."
        ]).ToArray();
    }

    private static IReadOnlyList<string> BuildGuardianAbodeForbiddenActions(
        IReadOnlyList<string> baseActions,
        WorkerGuardianAbodeRequest? guardianAbodeRequest)
    {
        if (guardianAbodeRequest == null)
            return baseActions;

        return baseActions.Concat(
        [
            "Do not model Guardians as Mortal NPCs.",
            "Do not model Abodes or Guardian politics as Mortal factions.",
            "Do not place hidden Guardian dossier facts in player-visible fields."
        ]).ToArray();
    }

    private static string BuildGuardianAbodeInstructions(WorkerGuardianAbodeRequest? guardianAbodeRequest)
    {
        if (guardianAbodeRequest == null)
            return "";

        return Environment.NewLine +
            "Guardian/Abode content request:" + Environment.NewLine +
            $"- realm: {guardianAbodeRequest.Realm}" + Environment.NewLine +
            $"- guardianIds: {FormatList(guardianAbodeRequest.GuardianIds)}" + Environment.NewLine +
            $"- abodeIds: {FormatList(guardianAbodeRequest.AbodeIds)}" + Environment.NewLine +
            $"- pendingControlFiles: {FormatList(guardianAbodeRequest.PendingControlFiles)}" + Environment.NewLine +
            $"- focusAreas: {FormatList(guardianAbodeRequest.FocusAreas)}" + Environment.NewLine +
            $"- readScope: {FormatList(guardianAbodeRequest.ReadScope)}" + Environment.NewLine +
            "Return guardianAbodeProposal. Use Guardian/Abode/project/politics surfaces only. Keep hidden facts GM-only.";
    }

    private static IReadOnlyList<string> BuildSoulContentAcceptanceCriteria(
        IReadOnlyList<string> baseCriteria,
        WorkerSoulContentRequest? soulContentRequest)
    {
        if (soulContentRequest == null)
            return baseCriteria;

        return baseCriteria.Concat(
        [
            "Include soulContentProposal with safeSoulSummaries, progressionSuggestions, rewardNotes, nextLifePreparationHooks, forbiddenReadonlyFields, requiredReceipts, requiredReports, validatorRisks, and gmReviewNotes.",
            "Treat soulName and soulFormDescription as player-owned readonly identity fields.",
            "Use exact soul_state/afterlife surfaces; do not rewrite soul progression as ordinary character, inventory, or Mortal World state."
        ]).ToArray();
    }

    private static IReadOnlyList<string> BuildSoulContentForbiddenActions(
        IReadOnlyList<string> baseActions,
        WorkerSoulContentRequest? soulContentRequest)
    {
        if (soulContentRequest == null)
            return baseActions;

        return baseActions.Concat(
        [
            "Do not overwrite soulName or soulFormDescription.",
            "Do not model the player soul as an ordinary Mortal character.",
            "Do not model soul rewards as Mortal inventory or item state."
        ]).ToArray();
    }

    private static string BuildSoulContentInstructions(WorkerSoulContentRequest? soulContentRequest)
    {
        if (soulContentRequest == null)
            return "";

        return Environment.NewLine +
            "Soul content request:" + Environment.NewLine +
            $"- realm: {soulContentRequest.Realm}" + Environment.NewLine +
            $"- soulContext: {soulContentRequest.SoulContext}" + Environment.NewLine +
            $"- requestedScope: {FormatList(soulContentRequest.RequestedScope)}" + Environment.NewLine +
            $"- progressionConstraints: {FormatList(soulContentRequest.ProgressionConstraints)}" + Environment.NewLine +
            $"- readScope: {FormatList(soulContentRequest.ReadScope)}" + Environment.NewLine +
            $"- playerOwnedIdentityFields: {FormatList(soulContentRequest.PlayerOwnedIdentityFields)}" + Environment.NewLine +
            "Return soulContentProposal. Reference player-owned identity only as readonly context; do not overwrite it.";
    }

    private static string FormatList(IReadOnlyList<string> values) =>
        values.Count == 0
            ? "none specified"
            : string.Join(", ", values);
}
