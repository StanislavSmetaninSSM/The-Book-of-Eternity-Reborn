using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    private sealed record SarefUnlockReference(
        string Id,
        string? SourceGuardianId,
        int SourceQuestOrdinal,
        string Context);

    private sealed record SarefAdvantageState(
        string AdvantageId,
        string? State,
        HashSet<string> ApplicableScenes,
        string Context,
        string? SpentUsageId);

    private sealed record SarefAdvantageUse(
        string? UsageId,
        string? AdvantageId,
        string? SceneType,
        bool ConsumesAdvantage,
        string Context);

    private void ValidatePendingSarefWingsInfiltrationRequestFile(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            AddSarefWingsIssue(
                issues,
                contextPrefix,
                "pending_saref_wings_infiltration.json должен быть JSON object.",
                "saref_wings_pending_invalid_root",
                "object",
                root.ValueKind.ToString());
            return;
        }

        var requestId = RequireSarefString(root, contextPrefix, "requestId", "saref_wings_pending_missing_request_id", issues);
        if (!string.IsNullOrWhiteSpace(requestId) &&
            !requestId.StartsWith("saref_wings_infiltration:", StringComparison.OrdinalIgnoreCase))
        {
            AddSarefWingsIssue(
                issues,
                $"{contextPrefix}.requestId",
                "requestId поиска Крыльев должен начинаться с saref_wings_infiltration:.",
                "saref_wings_pending_invalid_request_id",
                "saref_wings_infiltration:<turn>",
                requestId);
        }

        RequireSarefTurnNumber(root, contextPrefix, "createdAtTurn", "saref_wings_pending_missing_created_turn", issues);
        RequireSarefString(root, contextPrefix, "createdAtUtc", "saref_wings_pending_missing_created_at_utc", issues);

        var routeSafety = RequireSarefString(root, contextPrefix, "routeSafety", "saref_wings_pending_missing_route_safety", issues);
        if (!string.IsNullOrWhiteSpace(routeSafety) && !SarefMainStoryState.WingsRouteSafetyStates.Contains(routeSafety))
        {
            AddSarefWingsIssue(
                issues,
                $"{contextPrefix}.routeSafety",
                "routeSafety поиска Крыльев не поддерживается.",
                "saref_wings_pending_invalid_route_safety",
                string.Join("/", SarefMainStoryState.WingsRouteSafetyStates),
                routeSafety);
        }

        RequireSarefString(root, contextPrefix, "entryMode", "saref_wings_pending_missing_entry_mode", issues);
        var responseSurface = RequireSarefString(root, contextPrefix, "expectedResponseSurface", "saref_wings_pending_missing_response_surface", issues);
        if (!string.IsNullOrWhiteSpace(responseSurface) &&
            !string.Equals(responseSurface, SarefMainStoryState.ResponseField, StringComparison.OrdinalIgnoreCase))
        {
            AddSarefWingsIssue(
                issues,
                $"{contextPrefix}.expectedResponseSurface",
                "Поиск Крыльев должен закрываться через sarefMainStoryUpdate.",
                "saref_wings_pending_response_surface_mismatch",
                SarefMainStoryState.ResponseField,
                responseSurface);
        }

        var categories = ValidateSarefWingsFragments(root, contextPrefix, "routeFragments", issues);
        var substituteCategories = ValidateSarefWingsFragments(root, contextPrefix, "substituteFragments", issues, allowEmpty: true);
        ValidateSarefWingsArray(root, contextPrefix, "availableAdvantages", issues);
        var disadvantages = ValidateSarefWingsStringArray(root, contextPrefix, "disadvantages", issues);
        ValidateSarefWingsExpectedClosure(root, contextPrefix, requestId, issues);

        if (string.Equals(routeSafety, SarefMainStoryState.WingsRouteSafetySafe, StringComparison.OrdinalIgnoreCase) &&
            !SarefMainStoryState.MandatoryWingsCategories.All(categories.Contains))
        {
            AddSarefWingsIssue(
                issues,
                $"{contextPrefix}.routeFragments",
                "safe route требует все четыре mandatory фрагмента identity/method/faction/path.",
                "saref_wings_pending_safe_route_incomplete",
                string.Join("/", SarefMainStoryState.MandatoryWingsCategories),
                string.Join(", ", categories.OrderBy(value => value, StringComparer.OrdinalIgnoreCase)));
        }

        if (string.Equals(routeSafety, SarefMainStoryState.WingsRouteSafetyRisky, StringComparison.OrdinalIgnoreCase) &&
            (SarefMainStoryState.MandatoryWingsCategories.Count(categories.Contains) < 3 || substituteCategories.Count < 2 || disadvantages.Count == 0))
        {
            AddSarefWingsIssue(
                issues,
                $"{contextPrefix}.routeFragments",
                "risky route требует минимум 3 mandatory фрагмента, 2 substitute fragments и explicit disadvantages.",
                "saref_wings_pending_risky_route_incomplete",
                "3 mandatory + 2 substitutes + disadvantages[]",
                $"mandatory={SarefMainStoryState.MandatoryWingsCategories.Count(categories.Contains)}, substitutes={substituteCategories.Count}, disadvantages={disadvantages.Count}");
        }

        if (string.Equals(routeSafety, SarefMainStoryState.WingsRouteSafetyDesperate, StringComparison.OrdinalIgnoreCase) &&
            (SarefMainStoryState.MandatoryWingsCategories.Count(categories.Contains) < 2 || substituteCategories.Count < 4 || disadvantages.Count == 0))
        {
            AddSarefWingsIssue(
                issues,
                $"{contextPrefix}.routeFragments",
                "desperate route требует минимум 2 mandatory фрагмента, 4 substitute fragments и explicit disadvantages.",
                "saref_wings_pending_desperate_route_incomplete",
                "2 mandatory + 4 substitutes + disadvantages[]",
                $"mandatory={SarefMainStoryState.MandatoryWingsCategories.Count(categories.Contains)}, substitutes={substituteCategories.Count}, disadvantages={disadvantages.Count}");
        }
    }

    private async Task ValidatePendingSarefWingsInfiltrationRequestContextAsync(List<ValidationIssue> issues)
    {
        var read = await SarefMainStoryState.ReadWingsInfiltrationRequestStateAsync(_fs);
        if (!read.Exists)
            return;

        if (read.IsMalformed || read.Request == null)
        {
            AddSarefWingsIssue(
                issues,
                SarefMainStoryState.PendingWingsInfiltrationPath,
                "pending_saref_wings_infiltration.json повреждён и не может быть resolved.",
                "saref_wings_pending_malformed",
                "canonical Wings infiltration pending request",
                read.Error ?? "malformed");
            return;
        }

        var soulRoot = await ReadJsonObjectAsync("game_state/meta/soul_state.json");
        var shiningRoot = await ReadJsonObjectAsync(ShiningAbodeState.StatePath);
        var currentRealm = SarefMainStoryState.GetNodeString(soulRoot?["currentRealm"]);
        if (!RealmSemantics.IsShiningRealm(currentRealm))
        {
            AddSarefWingsIssue(
                issues,
                SarefMainStoryState.PendingWingsInfiltrationPath,
                "Поиск Крыльев Ангелов можно держать только в ordinary active Сияющей Обители.",
                "saref_wings_pending_wrong_realm",
                "soul_state.currentRealm=Shining Abode",
                currentRealm ?? "missing");
            return;
        }

        if (shiningRoot == null ||
            !string.Equals(SarefMainStoryState.GetNodeString(shiningRoot["availability"]), ShiningAbodeState.AvailabilityActive, StringComparison.OrdinalIgnoreCase) ||
            ShiningAbodeState.GetPreparedIncarnationPackageMode(shiningRoot) != ShiningAbodeState.PreparedIncarnationPackageMode.Absent)
        {
            AddSarefWingsIssue(
                issues,
                SarefMainStoryState.PendingWingsInfiltrationPath,
                "Поиск Крыльев требует ordinary active Shining Abode без preparedIncarnationPackage.",
                "saref_wings_pending_invalid_shining_mode",
                "availability=active and no preparedIncarnationPackage",
                shiningRoot == null ? "missing shining state" : "inactive or handoff mode");
            return;
        }

        var pendingBlocker = await SourceOfLightCapstoneState.TryDescribeBlockingPendingContractAsync(_fs, shiningRoot);
        if (pendingBlocker != null)
        {
            AddSarefWingsIssue(
                issues,
                SarefMainStoryState.PendingWingsInfiltrationPath,
                "Поиск Крыльев нельзя держать рядом с другим active/malformed afterlife pending/control contract.",
                "saref_wings_pending_blocked_by_other_contract",
                "no other active/malformed afterlife pending/control contract",
                pendingBlocker);
            return;
        }

        var storyRoot = await ReadJsonObjectAsync(SarefMainStoryState.StatePath);
        if (!SarefMainStoryState.TryBuildWingsUnlockRoute(storyRoot, out var computedSafety, out _, out _, out _))
        {
            AddSarefWingsIssue(
                issues,
                SarefMainStoryState.PendingWingsInfiltrationPath,
                "pending_saref_wings_infiltration.json требует достаточный маршрут раскрытия Крыльев в main_story_saref_state.json.",
                "saref_wings_pending_missing_unlock_route",
                "all four mandatory, or 3 mandatory + 2 additional, or 2 mandatory + 4 additional",
                "route not available");
            return;
        }

        var requestSafety = SarefMainStoryState.GetNodeString(read.Request["routeSafety"]);
        if (!string.Equals(requestSafety, computedSafety, StringComparison.OrdinalIgnoreCase))
        {
            AddSarefWingsIssue(
                issues,
                SarefMainStoryState.PendingWingsInfiltrationPath,
                "routeSafety pending-файла должен совпадать с текущим маршрутом раскрытия.",
                "saref_wings_pending_route_safety_mismatch",
                computedSafety,
                requestSafety ?? "missing");
        }
    }

    private async Task ValidatePendingSarefWingsInfiltrationResolutionAsync(List<ValidationIssue> issues)
    {
        var preTurnRequestJson = await ReadValidatedCurrentPreTurnTrackedFileAsync(SarefMainStoryState.PendingWingsInfiltrationPath);
        if (string.IsNullOrWhiteSpace(preTurnRequestJson))
            return;

        var requestState = SarefMainStoryState.ReadWingsInfiltrationRequestState(preTurnRequestJson, exists: true);
        if (requestState.IsMalformed || requestState.Request == null)
        {
            AddSarefWingsIssue(
                issues,
                SarefMainStoryState.PendingWingsInfiltrationPath,
                "validated snapshot pending_saref_wings_infiltration.json malformed.",
                "saref_wings_malformed_validated_snapshot_request",
                "canonical pending request",
                requestState.Error ?? "malformed");
            return;
        }

        var storyRoot = await ReadJsonObjectAsync(SarefMainStoryState.StatePath);
        if (!SarefMainStoryState.HasMatchingWingsInfiltrationClosure(storyRoot, requestState.Request))
        {
            AddSarefWingsIssue(
                issues,
                SarefMainStoryState.StatePath,
                "Accepted closure поиска Крыльев должен закрыть pending request через wingsInfiltration с matching requestId/status/resolvedAtTurn.",
                "saref_wings_pending_missing_closure",
                "wingsInfiltration.status=revealed/refused/blocked with matching requestId and resolvedAtTurn",
                "missing or mismatched closure");
        }
    }

    private async Task ValidateSarefWingsFactionLinksContextAsync(List<ValidationIssue> issues)
    {
        var storyRoot = await ReadJsonObjectAsync(SarefMainStoryState.StatePath);
        if (storyRoot == null)
            return;

        var revealStage = SarefMainStoryState.GetNodeString(storyRoot["revealStage"]);
        var factionLinks = storyRoot["factionLinks"] as JsonObject;
        var visibility = SarefMainStoryState.GetNodeString(factionLinks?["visibility"]);
        if (!SarefMainStoryStageRequiresActionableFaction(revealStage, visibility))
            return;

        var wingsFactionId = SarefMainStoryState.GetNodeString(factionLinks?["wingsFactionId"]);
        if (string.IsNullOrWhiteSpace(wingsFactionId))
            return;

        var shiningRoot = await ReadJsonObjectAsync(ShiningAbodeState.StatePath);
        if (shiningRoot == null)
            return;

        var factions = shiningRoot["factions"] as JsonArray;
        var matchingFaction = factions?.OfType<JsonObject>()
            .FirstOrDefault(faction => string.Equals(
                SarefMainStoryState.GetNodeString(faction["factionId"]),
                wingsFactionId,
                StringComparison.OrdinalIgnoreCase));
        if (matchingFaction == null)
        {
            AddSarefIssue(
                issues,
                $"{ShiningAbodeState.StatePath}.factions",
                "После раскрытия Крылья Ангелов должны существовать как actionable Shining faction actor.",
                "saref_main_story_wings_faction_missing_shining_actor",
                $"factions[] contains factionId={wingsFactionId}",
                "missing");
            return;
        }

        var role = SarefMainStoryState.GetNodeString(matchingFaction["sarefFactionRole"]);
        var factionVisibility = SarefMainStoryState.GetNodeString(matchingFaction["sarefVisibility"]);
        if (string.Equals(role, SarefMainStoryState.WingsFactionRole, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(factionVisibility, SarefMainStoryState.FactionVisibilityRevealed, StringComparison.OrdinalIgnoreCase))
        {
            AddSarefIssue(
                issues,
                $"{ShiningAbodeState.StatePath}.factions[].sarefVisibility",
                "Раскрытая faction actor Крыльев Ангелов должна быть видимой и actionable.",
                "saref_main_story_wings_faction_not_revealed",
                "sarefVisibility=revealed",
                factionVisibility ?? "missing");
        }
    }

    private void ValidateSarefMainStoryStateFile(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new ValidationIssue(
                SarefMainStoryState.StatePath,
                IssueSeverity.Error,
                "main_story_saref_state.json должен быть JSON object.",
                code: "saref_main_story_invalid_root",
                section: "SarefMainStory",
                expected: "object with schemaVersion, revealStage, guardianQuestlines[], latentTraces[], sarefRevelations[], sarefAdvantages[]",
                actual: root.ValueKind.ToString()));
            return;
        }

        ValidateNonNegativeIntegerField(root, contextPrefix, issues, "schemaVersion", "SarefMainStory");

        var revealStage = RequireSarefString(root, contextPrefix, "revealStage", "saref_main_story_missing_reveal_stage", issues);
        if (!string.IsNullOrWhiteSpace(revealStage) && !SarefMainStoryState.RevealStages.Contains(revealStage))
        {
            AddSarefIssue(
                issues,
                $"{contextPrefix}.revealStage",
                "revealStage main story Сарефа не поддерживается.",
                "saref_main_story_invalid_reveal_stage",
                string.Join("/", SarefMainStoryState.RevealStages.OrderBy(item => item, StringComparer.OrdinalIgnoreCase)),
                revealStage);
        }

        var guardianQuestlines = ValidateSarefGuardianQuestlines(root, contextPrefix, issues);
        ValidateSarefLatentTraces(root, contextPrefix, issues);
        ValidateSarefRevelations(root, contextPrefix, issues, out var revealedCategories, out var revelationCount, out var questFourRevelations);
        var advantages = ValidateSarefAdvantages(root, contextPrefix, issues, out var advantageCount, out var questFourAdvantages);
        ValidateSarefAdvantageUses(root, contextPrefix, advantages, issues);
        ValidateSarefQuestFourUnlockLinks(guardianQuestlines, questFourRevelations, questFourAdvantages, contextPrefix, issues);
        ValidateSarefArray(root, contextPrefix, "defeatOutcomes", "outcomeId", "saref_main_story_duplicate_defeat_outcome", issues);
        ValidateSarefArray(root, contextPrefix, "endings", "endingId", "saref_main_story_duplicate_ending", issues);
        var factionVisibility = ValidateSarefFactionLinks(root, contextPrefix, issues);
        ValidateSarefActionableFactionLink(revealStage, root, factionVisibility, contextPrefix, issues);
        ValidateSarefNullableStateObject(root, contextPrefix, "playerOathState", "state", SarefMainStoryState.PlayerOathStates, "saref_main_story_invalid_player_oath_state", issues);
        ValidateSarefNullableStateObject(root, contextPrefix, "sarefPersonalBond", "state", SarefMainStoryState.PersonalBondStates, "saref_main_story_invalid_personal_bond_state", issues);
        ValidateSarefNullableObject(root, contextPrefix, "wingsInfiltration", issues);
        ValidateSarefNullableObject(root, contextPrefix, "finalConfrontation", issues);
        ValidateSarefRevealStageInvariants(
            revealStage,
            revelationCount,
            advantageCount,
            revealedCategories,
            factionVisibility,
            contextPrefix,
            issues);
    }

    private static Dictionary<string, Dictionary<int, string>> ValidateSarefGuardianQuestlines(
        JsonElement root,
        string contextPrefix,
        List<ValidationIssue> issues)
    {
        var result = new Dictionary<string, Dictionary<int, string>>(StringComparer.OrdinalIgnoreCase);
        if (!TryGetRequiredSarefArray(root, contextPrefix, "guardianQuestlines", issues, out var questlines))
            return result;

        var guardianIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var index = 0;
        foreach (var item in questlines.EnumerateArray())
        {
            var context = $"{contextPrefix}.guardianQuestlines[{index++}]";
            if (!ValidateSarefArrayObject(item, context, issues))
                continue;

            var guardianId = RequireSarefString(item, context, "guardianId", "saref_main_story_missing_guardian_questline_guardian_id", issues);
            if (!string.IsNullOrWhiteSpace(guardianId) && !guardianIds.Add(guardianId))
            {
                AddSarefIssue(
                    issues,
                    context,
                    "Дубликат guardianQuestlines[].guardianId.",
                    "saref_main_story_duplicate_guardian_questline",
                    "unique guardianId",
                    guardianId);
            }

            ValidateSarefTurnFields(item, context, issues);
            var states = ValidateSarefQuestStates(item, context, issues);
            if (!string.IsNullOrWhiteSpace(guardianId))
                result[guardianId] = states;
        }

        return result;
    }

    private static Dictionary<int, string> ValidateSarefQuestStates(
        JsonElement questline,
        string context,
        List<ValidationIssue> issues)
    {
        var states = new Dictionary<int, string>();
        if (!questline.TryGetProperty("questStates", out var questStates))
        {
            AddSarefIssue(
                issues,
                $"{context}.questStates",
                "guardianQuestlines[] должен хранить questStates[] с questOrdinal 1..4.",
                "saref_main_story_missing_quest_states",
                "questStates[]",
                "missing");
            return states;
        }

        if (questStates.ValueKind != JsonValueKind.Array)
        {
            AddSarefIssue(
                issues,
                $"{context}.questStates",
                "guardianQuestlines[].questStates должен быть массивом.",
                "saref_main_story_array_not_array",
                "array",
                questStates.ValueKind.ToString());
            return states;
        }

        var index = 0;
        foreach (var item in questStates.EnumerateArray())
        {
            var itemContext = $"{context}.questStates[{index++}]";
            if (!ValidateSarefArrayObject(item, itemContext, issues))
                continue;

            if (ContainsForbiddenSarefPhysicalEvidenceField(item))
            {
                AddSarefIssue(
                    issues,
                    itemContext,
                    "Квесты Сарефа не могут переносить физические mortal предметы; используй memory/image/echo/proof.",
                    "saref_main_story_physical_mortal_item_evidence",
                    "memoryImprint/itemEcho/lifeEventEvidence/locationWitness/knowledgeTrace/soulResonance without physical transfer fields",
                    "physical mortal item field");
            }

            var questOrdinal = RequireSarefQuestOrdinal(item, itemContext, issues);
            var status = RequireSarefString(item, itemContext, "status", "saref_main_story_missing_quest_status", issues);
            if (!string.IsNullOrWhiteSpace(status) && !SarefMainStoryState.QuestProgressStates.Contains(status))
            {
                AddSarefIssue(
                    issues,
                    $"{itemContext}.status",
                    "Saref guardian quest status не поддерживается.",
                    "saref_main_story_invalid_quest_status",
                    string.Join("/", SarefMainStoryState.QuestProgressStates.OrderBy(value => value, StringComparer.OrdinalIgnoreCase)),
                    status);
            }

            ValidateSarefTurnFields(item, itemContext, issues);
            if (questOrdinal is < 1 or > 4 || string.IsNullOrWhiteSpace(status) ||
                !SarefMainStoryState.QuestProgressStates.Contains(status))
            {
                continue;
            }

            if (states.ContainsKey(questOrdinal))
            {
                AddSarefIssue(
                    issues,
                    itemContext,
                    "Дубликат questStates[].questOrdinal внутри guardianQuestline.",
                    "saref_main_story_duplicate_quest_ordinal",
                    "unique questOrdinal 1..4",
                    questOrdinal.ToString());
                continue;
            }

            states[questOrdinal] = status;
        }

        foreach (var (ordinal, status) in states.OrderBy(entry => entry.Key))
        {
            if (!string.Equals(status, SarefMainStoryState.QuestStateCompleted, StringComparison.OrdinalIgnoreCase))
                continue;

            for (var requiredOrdinal = 1; requiredOrdinal < ordinal; requiredOrdinal++)
            {
                if (states.TryGetValue(requiredOrdinal, out var priorStatus) &&
                    string.Equals(priorStatus, SarefMainStoryState.QuestStateCompleted, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                AddSarefIssue(
                    issues,
                    $"{context}.questStates",
                    "Официальное восстановление памяти Сарефа должно закрываться по порядку 1 -> 2 -> 3 -> 4.",
                    "saref_main_story_questline_out_of_order",
                    $"quest {requiredOrdinal} completed before quest {ordinal}",
                    $"quest {ordinal}=completed");
                break;
            }
        }

        return states;
    }

    private static void ValidateSarefLatentTraces(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        if (!TryGetRequiredSarefArray(root, contextPrefix, "latentTraces", issues, out var traces))
            return;

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var index = 0;
        foreach (var item in traces.EnumerateArray())
        {
            var context = $"{contextPrefix}.latentTraces[{index++}]";
            if (!ValidateSarefArrayObject(item, context, issues))
                continue;

            if (ContainsForbiddenSarefPhysicalEvidenceField(item))
            {
                AddSarefIssue(
                    issues,
                    context,
                    "Latent trace Сарефа не может хранить физический mortal предмет.",
                    "saref_main_story_physical_mortal_item_evidence",
                    "non-physical memory/image/echo/proof",
                    "physical mortal item field");
            }

            var traceId = RequireSarefString(item, context, "traceId", "saref_main_story_missing_trace_id", issues);
            if (!string.IsNullOrWhiteSpace(traceId) && !ids.Add(traceId))
            {
                AddSarefIssue(
                    issues,
                    context,
                    "Дубликат latentTraces[].traceId.",
                    "saref_main_story_duplicate_latent_trace",
                    "unique traceId",
                    traceId);
            }

            if (item.TryGetProperty("questOrdinal", out _))
                RequireSarefQuestOrdinal(item, context, issues);

            if (item.TryGetProperty("status", out var statusNode) && TryGetSarefString(statusNode, out var status) &&
                !SarefMainStoryState.LatentTraceStates.Contains(status))
            {
                AddSarefIssue(
                    issues,
                    $"{context}.status",
                    "latentTraces[].status может быть только latent или recognized.",
                    "saref_main_story_invalid_latent_trace_status",
                    string.Join("/", SarefMainStoryState.LatentTraceStates),
                    status);
            }

            ValidateSarefTurnFields(item, context, issues);
        }
    }

    private static void ValidateSarefQuestFourUnlockLinks(
        IReadOnlyDictionary<string, Dictionary<int, string>> guardianQuestlines,
        IReadOnlyList<SarefUnlockReference> questFourRevelations,
        IReadOnlyList<SarefUnlockReference> questFourAdvantages,
        string contextPrefix,
        List<ValidationIssue> issues)
    {
        var revelationGuardians = questFourRevelations
            .Where(reference => !string.IsNullOrWhiteSpace(reference.SourceGuardianId))
            .Select(reference => reference.SourceGuardianId!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var advantageGuardians = questFourAdvantages
            .Where(reference => !string.IsNullOrWhiteSpace(reference.SourceGuardianId))
            .Select(reference => reference.SourceGuardianId!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var reference in questFourRevelations)
        {
            if (!HasCompletedSarefQuestlineThroughQuestFour(guardianQuestlines, reference.SourceGuardianId))
            {
                AddSarefIssue(
                    issues,
                    reference.Context,
                    "sarefRevelation из 4-го квеста требует завершенные квесты 1-4 этого Хранителя.",
                    "saref_main_story_revelation_without_questline_completion",
                    "guardianQuestlines[].questStates 1..4 completed for sourceGuardianId",
                    reference.SourceGuardianId ?? "missing");
            }
        }

        foreach (var reference in questFourAdvantages)
        {
            if (!HasCompletedSarefQuestlineThroughQuestFour(guardianQuestlines, reference.SourceGuardianId))
            {
                AddSarefIssue(
                    issues,
                    reference.Context,
                    "sarefAdvantage из 4-го квеста требует завершенные квесты 1-4 этого Хранителя.",
                    "saref_main_story_advantage_without_questline_completion",
                    "guardianQuestlines[].questStates 1..4 completed for sourceGuardianId",
                    reference.SourceGuardianId ?? "missing");
            }
        }

        foreach (var (guardianId, states) in guardianQuestlines)
        {
            if (!HasCompletedSarefQuestlineThroughQuestFour(states))
                continue;

            if (!revelationGuardians.Contains(guardianId))
            {
                AddSarefIssue(
                    issues,
                    $"{contextPrefix}.sarefRevelations",
                    "Завершенный 4-й квест Хранителя должен открыть canonical sarefRevelation.",
                    "saref_main_story_completed_quest_four_missing_revelation",
                    $"sarefRevelations[] with sourceGuardianId={guardianId} and sourceQuestOrdinal=4",
                    "missing");
            }

            if (!advantageGuardians.Contains(guardianId))
            {
                AddSarefIssue(
                    issues,
                    $"{contextPrefix}.sarefAdvantages",
                    "Завершенный 4-й квест Хранителя должен открыть canonical sarefAdvantage.",
                    "saref_main_story_completed_quest_four_missing_advantage",
                    $"sarefAdvantages[] with sourceGuardianId={guardianId} and sourceQuestOrdinal=4",
                    "missing");
            }
        }
    }

    private static bool HasCompletedSarefQuestlineThroughQuestFour(
        IReadOnlyDictionary<string, Dictionary<int, string>> questlines,
        string? guardianId)
    {
        if (string.IsNullOrWhiteSpace(guardianId) || !questlines.TryGetValue(guardianId, out var states))
            return false;

        return HasCompletedSarefQuestlineThroughQuestFour(states);
    }

    private static bool HasCompletedSarefQuestlineThroughQuestFour(IReadOnlyDictionary<int, string> states)
    {
        for (var ordinal = 1; ordinal <= 4; ordinal++)
        {
            if (!states.TryGetValue(ordinal, out var status) ||
                !string.Equals(status, SarefMainStoryState.QuestStateCompleted, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static void ValidateSarefRevealStageInvariants(
        string? revealStage,
        int revelationCount,
        int advantageCount,
        HashSet<string> revealedCategories,
        string? factionVisibility,
        string contextPrefix,
        List<ValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(revealStage) || !SarefMainStoryState.RevealStages.Contains(revealStage))
            return;

        var noSpoilerStage =
            string.Equals(revealStage, SarefMainStoryState.RevealStageUnknown, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(revealStage, SarefMainStoryState.RevealStageShadow, StringComparison.OrdinalIgnoreCase);
        if (noSpoilerStage &&
            (revelationCount > 0 || advantageCount > 0 ||
             string.Equals(factionVisibility, "revealed", StringComparison.OrdinalIgnoreCase)))
        {
            AddSarefIssue(
                issues,
                $"{contextPrefix}.revealStage",
                "No-spoiler стадия Сарефа не может содержать раскрытые фрагменты, преимущества или раскрытую фракцию.",
                "saref_main_story_no_spoiler_stage_has_revealed_content",
                "unknown/shadow without sarefRevelations[], sarefAdvantages[], or factionLinks.visibility=revealed",
                revealStage);
        }

        if (StageAtLeast(revealStage, SarefMainStoryState.RevealStageNameRevealed) && revelationCount == 0)
        {
            AddSarefIssue(
                issues,
                $"{contextPrefix}.sarefRevelations",
                "Стадия name_revealed или выше требует хотя бы один canonical sarefRevelation.",
                "saref_main_story_revealed_stage_without_revelation",
                "sarefRevelations[] with sourceGuardianId/category/revealedAtTurn",
                "empty");
        }

        if (StageAtLeast(revealStage, SarefMainStoryState.RevealStageWingsRevealed) &&
            !HasWingsUnlockRoute(revealedCategories))
        {
            AddSarefIssue(
                issues,
                $"{contextPrefix}.sarefRevelations",
                "Нельзя перейти к Wings/final стадиям без достаточного маршрута раскрытия Крыльев Ангелов.",
                "saref_main_story_wings_stage_without_unlock_route",
                "all four mandatory categories, or 3 mandatory + 2 additional, or 2 mandatory + 4 additional",
                string.Join(", ", revealedCategories.OrderBy(item => item, StringComparer.OrdinalIgnoreCase)));
        }
    }

    private static bool StageAtLeast(string revealStage, string requiredStage) =>
        StageRank(revealStage) >= StageRank(requiredStage);

    private static int StageRank(string revealStage)
    {
        if (string.Equals(revealStage, SarefMainStoryState.RevealStageShadow, StringComparison.OrdinalIgnoreCase))
            return 1;
        if (string.Equals(revealStage, SarefMainStoryState.RevealStageNameRevealed, StringComparison.OrdinalIgnoreCase))
            return 2;
        if (string.Equals(revealStage, SarefMainStoryState.RevealStageWingsRevealed, StringComparison.OrdinalIgnoreCase))
            return 3;
        if (string.Equals(revealStage, SarefMainStoryState.RevealStageInfiltrationActive, StringComparison.OrdinalIgnoreCase))
            return 4;
        if (string.Equals(revealStage, SarefMainStoryState.RevealStageConfrontationAvailable, StringComparison.OrdinalIgnoreCase))
            return 5;
        if (string.Equals(revealStage, SarefMainStoryState.RevealStageCompleted, StringComparison.OrdinalIgnoreCase))
            return 6;

        return 0;
    }

    private static bool HasWingsUnlockRoute(HashSet<string> categories)
    {
        var mandatoryCount = SarefMainStoryState.MandatoryWingsCategories.Count(categories.Contains);
        var additionalCount = categories.Count(category => !SarefMainStoryState.MandatoryWingsCategories.Contains(category));
        return mandatoryCount == 4 ||
               mandatoryCount >= 3 && additionalCount >= 2 ||
               mandatoryCount >= 2 && additionalCount >= 4;
    }

    private static void ValidateSarefRevelations(
        JsonElement root,
        string contextPrefix,
        List<ValidationIssue> issues,
        out HashSet<string> revealedCategories,
        out int revelationCount,
        out List<SarefUnlockReference> questFourRevelations)
    {
        revealedCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        revelationCount = 0;
        questFourRevelations = new List<SarefUnlockReference>();
        if (!TryGetRequiredSarefArray(root, contextPrefix, "sarefRevelations", issues, out var revelations))
            return;

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var index = 0;
        foreach (var item in revelations.EnumerateArray())
        {
            var context = $"{contextPrefix}.sarefRevelations[{index++}]";
            if (!ValidateSarefArrayObject(item, context, issues))
                continue;

            revelationCount++;
            var revelationId = RequireSarefString(item, context, "revelationId", "saref_main_story_missing_revelation_id", issues);
            if (!string.IsNullOrWhiteSpace(revelationId) && !ids.Add(revelationId))
            {
                AddSarefIssue(
                    issues,
                    context,
                    "Дубликат sarefRevelations[].revelationId.",
                    "saref_main_story_duplicate_revelation",
                    "unique revelationId",
                    revelationId);
            }

            var category = RequireSarefString(item, context, "category", "saref_main_story_missing_revelation_category", issues);
            if (!string.IsNullOrWhiteSpace(category))
            {
                if (!SarefMainStoryState.RevelationCategories.Contains(category))
                {
                    AddSarefIssue(
                        issues,
                        $"{context}.category",
                        "Категория sarefRevelation не поддерживается.",
                        "saref_main_story_invalid_revelation_category",
                        string.Join("/", SarefMainStoryState.RevelationCategories.OrderBy(value => value, StringComparer.OrdinalIgnoreCase)),
                        category);
                }
                else
                {
                    revealedCategories.Add(category);
                }
            }

            var sourceGuardianId = RequireSarefString(item, context, "sourceGuardianId", "saref_main_story_missing_revelation_source", issues);
            var sourceQuestOrdinal = ResolveSarefSourceQuestOrdinal(item, context, issues);
            if (!string.IsNullOrWhiteSpace(sourceGuardianId))
            {
                if (sourceQuestOrdinal == 4)
                {
                    questFourRevelations.Add(new SarefUnlockReference(revelationId ?? string.Empty, sourceGuardianId, sourceQuestOrdinal, context));
                }
                else
                {
                    AddSarefIssue(
                        issues,
                        context,
                        "Canonical sarefRevelation от Хранителя может открываться только 4-м квестом.",
                        "saref_main_story_revelation_not_from_quest_four",
                        "sourceQuestOrdinal=4 or sourceQuestId ending with q4",
                        sourceQuestOrdinal <= 0 ? "missing" : sourceQuestOrdinal.ToString());
                }
            }

            ValidateSarefTurnFields(item, context, issues);
        }
    }

    private static Dictionary<string, SarefAdvantageState> ValidateSarefAdvantages(
        JsonElement root,
        string contextPrefix,
        List<ValidationIssue> issues,
        out int advantageCount,
        out List<SarefUnlockReference> questFourAdvantages)
    {
        advantageCount = 0;
        questFourAdvantages = new List<SarefUnlockReference>();
        var result = new Dictionary<string, SarefAdvantageState>(StringComparer.OrdinalIgnoreCase);
        if (!TryGetRequiredSarefArray(root, contextPrefix, "sarefAdvantages", issues, out var advantages))
            return result;

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var index = 0;
        foreach (var item in advantages.EnumerateArray())
        {
            var context = $"{contextPrefix}.sarefAdvantages[{index++}]";
            if (!ValidateSarefArrayObject(item, context, issues))
                continue;

            advantageCount++;
            var advantageId = RequireSarefString(item, context, "advantageId", "saref_main_story_missing_advantage_id", issues);
            if (!string.IsNullOrWhiteSpace(advantageId) && !ids.Add(advantageId))
            {
                AddSarefIssue(
                    issues,
                    context,
                    "Дубликат sarefAdvantages[].advantageId.",
                    "saref_main_story_duplicate_advantage",
                    "unique advantageId",
                    advantageId);
            }

            var state = RequireSarefString(item, context, "state", "saref_main_story_missing_advantage_state", issues);
            if (!string.IsNullOrWhiteSpace(state) && !SarefMainStoryState.AdvantageStates.Contains(state))
            {
                AddSarefIssue(
                    issues,
                    $"{context}.state",
                    "Состояние преимущества против Сарефа не поддерживается.",
                    "saref_main_story_invalid_advantage_state",
                    string.Join("/", SarefMainStoryState.AdvantageStates.OrderBy(value => value, StringComparer.OrdinalIgnoreCase)),
                    state);
            }

            var applicableScenes = ValidateSarefAdvantageApplicableScenes(item, context, issues);
            string? spentUsageId = null;
            if (string.Equals(state, SarefMainStoryState.AdvantageStateSpent, StringComparison.OrdinalIgnoreCase))
                spentUsageId = ValidateSarefSpentAdvantageAudit(item, context, applicableScenes, issues);

            var sourceGuardianId = GetSarefOptionalString(item, "sourceGuardianId");
            var sourceQuestOrdinal = ResolveSarefSourceQuestOrdinal(item, context, issues);
            if (!string.IsNullOrWhiteSpace(sourceGuardianId))
            {
                if (sourceQuestOrdinal == 4)
                {
                    questFourAdvantages.Add(new SarefUnlockReference(advantageId ?? string.Empty, sourceGuardianId, sourceQuestOrdinal, context));
                }
                else
                {
                    AddSarefIssue(
                        issues,
                        context,
                        "Canonical sarefAdvantage от Хранителя может открываться только 4-м квестом.",
                        "saref_main_story_advantage_not_from_quest_four",
                        "sourceQuestOrdinal=4 or sourceQuestId ending with q4",
                        sourceQuestOrdinal <= 0 ? "missing" : sourceQuestOrdinal.ToString());
                }
            }

            ValidateSarefTurnFields(item, context, issues);
            if (!string.IsNullOrWhiteSpace(advantageId) && SarefMainStoryState.AdvantageStates.Contains(state ?? string.Empty))
                result[advantageId] = new SarefAdvantageState(advantageId, state, applicableScenes, context, spentUsageId);
        }

        return result;
    }

    private static HashSet<string> ValidateSarefAdvantageApplicableScenes(JsonElement item, string context, List<ValidationIssue> issues)
    {
        var scenes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!item.TryGetProperty("applicableScenes", out var scenesNode))
        {
            AddSarefIssue(
                issues,
                $"{context}.applicableScenes",
                "Преимущество Сарефа должно явно перечислять сцены, где его можно использовать.",
                "saref_main_story_missing_advantage_applicable_scenes",
                string.Join("/", SarefMainStoryState.AdvantageSceneTypes.OrderBy(value => value, StringComparer.OrdinalIgnoreCase)),
                "missing");
            return scenes;
        }

        if (scenesNode.ValueKind != JsonValueKind.Array)
        {
            AddSarefIssue(
                issues,
                $"{context}.applicableScenes",
                "applicableScenes должен быть массивом sceneType.",
                "saref_main_story_advantage_scenes_not_array",
                "array",
                scenesNode.ValueKind.ToString());
            return scenes;
        }

        var index = 0;
        foreach (var sceneNode in scenesNode.EnumerateArray())
        {
            var sceneContext = $"{context}.applicableScenes[{index++}]";
            if (!TryGetSarefString(sceneNode, out var scene))
            {
                AddSarefIssue(
                    issues,
                    sceneContext,
                    "applicableScenes[] должен содержать непустые sceneType строки.",
                    "saref_main_story_invalid_advantage_scene",
                    string.Join("/", SarefMainStoryState.AdvantageSceneTypes.OrderBy(value => value, StringComparer.OrdinalIgnoreCase)),
                    sceneNode.ValueKind.ToString());
                continue;
            }

            if (!SarefMainStoryState.AdvantageSceneTypes.Contains(scene))
            {
                AddSarefIssue(
                    issues,
                    sceneContext,
                    "sceneType преимущества Сарефа не поддерживается.",
                    "saref_main_story_invalid_advantage_scene",
                    string.Join("/", SarefMainStoryState.AdvantageSceneTypes.OrderBy(value => value, StringComparer.OrdinalIgnoreCase)),
                    scene);
                continue;
            }

            if (!scenes.Add(scene))
            {
                AddSarefIssue(
                    issues,
                    sceneContext,
                    "Дубликат applicableScenes[] у преимущества Сарефа.",
                    "saref_main_story_duplicate_advantage_scene",
                    "unique sceneType",
                    scene);
            }
        }

        if (scenes.Count == 0)
        {
            AddSarefIssue(
                issues,
                $"{context}.applicableScenes",
                "Преимущество Сарефа должно иметь хотя бы одну применимую сцену.",
                "saref_main_story_empty_advantage_applicable_scenes",
                "non-empty applicableScenes[]",
                "empty");
        }

        return scenes;
    }

    private static string? ValidateSarefSpentAdvantageAudit(
        JsonElement item,
        string context,
        IReadOnlySet<string> applicableScenes,
        List<ValidationIssue> issues)
    {
        if (!item.TryGetProperty("spentAudit", out var audit) || audit.ValueKind != JsonValueKind.Object)
        {
            AddSarefIssue(
                issues,
                $"{context}.spentAudit",
                "Потраченное преимущество Сарефа требует spentAudit с usageId, ходом, сценой и кратким итогом.",
                "saref_main_story_spent_advantage_missing_audit",
                "spentAudit object",
                item.TryGetProperty("spentAudit", out var present) ? present.ValueKind.ToString() : "missing");
            return null;
        }

        var usageId = RequireSarefString(audit, $"{context}.spentAudit", "usageId", "saref_main_story_spent_advantage_missing_audit", issues);
        var sceneType = RequireSarefString(audit, $"{context}.spentAudit", "sceneType", "saref_main_story_spent_advantage_missing_audit", issues);
        RequireSarefTurnNumber(audit, $"{context}.spentAudit", "usedAtTurn", "saref_main_story_spent_advantage_missing_audit", issues);
        RequireSarefString(audit, $"{context}.spentAudit", "summary", "saref_main_story_spent_advantage_missing_audit", issues);
        if (!string.IsNullOrWhiteSpace(sceneType))
            ValidateSarefAdvantageSceneUsage(sceneType, applicableScenes, $"{context}.spentAudit.sceneType", issues);

        return usageId;
    }

    private static void ValidateSarefAdvantageUses(
        JsonElement root,
        string contextPrefix,
        IReadOnlyDictionary<string, SarefAdvantageState> advantages,
        List<ValidationIssue> issues)
    {
        if (!root.TryGetProperty("sarefAdvantageUses", out var usesNode))
            return;

        if (usesNode.ValueKind != JsonValueKind.Array)
        {
            AddSarefIssue(
                issues,
                $"{contextPrefix}.sarefAdvantageUses",
                "sarefAdvantageUses должен быть массивом аудитов использования преимуществ.",
                "saref_main_story_advantage_uses_not_array",
                "array",
                usesNode.ValueKind.ToString());
            return;
        }

        var usageIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var consumedUsageIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var index = 0;
        foreach (var item in usesNode.EnumerateArray())
        {
            var context = $"{contextPrefix}.sarefAdvantageUses[{index++}]";
            if (!ValidateSarefArrayObject(item, context, issues))
                continue;

            var usage = ValidateSarefAdvantageUse(item, context, issues);
            if (!string.IsNullOrWhiteSpace(usage.UsageId) && !usageIds.Add(usage.UsageId))
            {
                AddSarefIssue(
                    issues,
                    context,
                    "Дубликат sarefAdvantageUses[].usageId.",
                    "saref_main_story_duplicate_advantage_usage",
                    "unique usageId",
                    usage.UsageId);
            }

            if (string.IsNullOrWhiteSpace(usage.AdvantageId))
                continue;

            if (!advantages.TryGetValue(usage.AdvantageId, out var advantage))
            {
                AddSarefIssue(
                    issues,
                    $"{context}.advantageId",
                    "Использование преимущества Сарефа ссылается на неизвестный advantageId.",
                    "saref_main_story_unknown_advantage_usage",
                    "existing sarefAdvantages[].advantageId",
                    usage.AdvantageId);
                continue;
            }

            if (!string.IsNullOrWhiteSpace(usage.SceneType))
                ValidateSarefAdvantageSceneUsage(usage.SceneType, advantage.ApplicableScenes, $"{context}.sceneType", issues);

            if (string.Equals(advantage.State, SarefMainStoryState.AdvantageStateSuppressed, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(advantage.State, SarefMainStoryState.AdvantageStateDisabled, StringComparison.OrdinalIgnoreCase))
            {
                AddSarefIssue(
                    issues,
                    context,
                    "Подавленное или отключённое преимущество Сарефа нельзя использовать.",
                    "saref_main_story_advantage_usage_unauthorized_state",
                    $"{SarefMainStoryState.AdvantageStateAvailable}/{SarefMainStoryState.AdvantageStatePassive} or matching spentAudit for consumed one-use",
                    advantage.State);
            }

            if (string.Equals(advantage.State, SarefMainStoryState.AdvantageStateAvailable, StringComparison.OrdinalIgnoreCase))
            {
                AddSarefIssue(
                    issues,
                    context,
                    "Если доступное одноразовое преимущество было использовано, финальное состояние должно стать spent с spentAudit.",
                    "saref_main_story_consumed_advantage_not_spent",
                    "state=spent + spentAudit matching usageId",
                    "state=available");
            }
            else if (string.Equals(advantage.State, SarefMainStoryState.AdvantageStatePassive, StringComparison.OrdinalIgnoreCase) && usage.ConsumesAdvantage)
            {
                AddSarefIssue(
                    issues,
                    context,
                    "Пассивное преимущество Сарефа не должно расходоваться как одноразовое.",
                    "saref_main_story_passive_advantage_consumed",
                    "consumesAdvantage=false",
                    "consumesAdvantage=true");
            }
            else if (string.Equals(advantage.State, SarefMainStoryState.AdvantageStateSpent, StringComparison.OrdinalIgnoreCase))
            {
                if (!usage.ConsumesAdvantage ||
                    string.IsNullOrWhiteSpace(usage.UsageId) ||
                    !string.Equals(advantage.SpentUsageId, usage.UsageId, StringComparison.OrdinalIgnoreCase))
                {
                    AddSarefIssue(
                        issues,
                        context,
                        "Потраченное преимущество должно ссылаться на тот же usageId в spentAudit и use log.",
                        "saref_main_story_spent_advantage_audit_mismatch",
                        "consumesAdvantage=true and usageId == spentAudit.usageId",
                        usage.UsageId ?? "missing");
                }
                else
                {
                    consumedUsageIds.Add(usage.UsageId);
                }
            }
        }

        foreach (var advantage in advantages.Values)
        {
            if (!string.Equals(advantage.State, SarefMainStoryState.AdvantageStateSpent, StringComparison.OrdinalIgnoreCase))
                continue;

            if (string.IsNullOrWhiteSpace(advantage.SpentUsageId) || !consumedUsageIds.Contains(advantage.SpentUsageId))
            {
                AddSarefIssue(
                    issues,
                    $"{advantage.Context}.spentAudit",
                    "Потраченное преимущество Сарефа должно иметь matching запись в sarefAdvantageUses[].",
                    "saref_main_story_spent_advantage_missing_usage_log",
                    "sarefAdvantageUses[] with matching usageId",
                    advantage.SpentUsageId ?? "missing");
            }
        }
    }

    private static SarefAdvantageUse ValidateSarefAdvantageUse(JsonElement item, string context, List<ValidationIssue> issues)
    {
        var usageId = RequireSarefString(item, context, "usageId", "saref_main_story_missing_advantage_usage_id", issues);
        var advantageId = RequireSarefString(item, context, "advantageId", "saref_main_story_missing_advantage_usage_advantage_id", issues);
        var sceneType = RequireSarefString(item, context, "sceneType", "saref_main_story_missing_advantage_usage_scene", issues);
        if (!string.IsNullOrWhiteSpace(sceneType) && !SarefMainStoryState.AdvantageSceneTypes.Contains(sceneType))
        {
            AddSarefIssue(
                issues,
                $"{context}.sceneType",
                "sceneType использования преимущества Сарефа не поддерживается.",
                "saref_main_story_invalid_advantage_scene",
                string.Join("/", SarefMainStoryState.AdvantageSceneTypes.OrderBy(value => value, StringComparer.OrdinalIgnoreCase)),
                sceneType);
        }

        var consumesAdvantage = GetSarefOptionalBool(item, "consumesAdvantage") ?? true;
        RequireSarefTurnNumber(item, context, "usedAtTurn", "saref_main_story_missing_advantage_usage_turn", issues);
        RequireSarefString(item, context, "summary", "saref_main_story_missing_advantage_usage_summary", issues);
        ValidateSarefTurnFields(item, context, issues);
        return new SarefAdvantageUse(usageId, advantageId, sceneType, consumesAdvantage, context);
    }

    private static void ValidateSarefAdvantageSceneUsage(
        string sceneType,
        IReadOnlySet<string> applicableScenes,
        string context,
        List<ValidationIssue> issues)
    {
        if (!SarefMainStoryState.AdvantageSceneTypes.Contains(sceneType))
            return;

        if (applicableScenes.Contains(SarefMainStoryState.SceneAny) || applicableScenes.Contains(sceneType))
            return;

        AddSarefIssue(
            issues,
            context,
            "Преимущество Сарефа используется в сцене, для которой оно не применимо.",
            "saref_main_story_advantage_usage_inapplicable_scene",
            string.Join("/", applicableScenes.OrderBy(value => value, StringComparer.OrdinalIgnoreCase)),
            sceneType);
    }

    private static void ValidateSarefArray(
        JsonElement root,
        string contextPrefix,
        string propertyName,
        string idProperty,
        string duplicateCode,
        List<ValidationIssue> issues)
    {
        if (!TryGetRequiredSarefArray(root, contextPrefix, propertyName, issues, out var array))
            return;

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var index = 0;
        foreach (var item in array.EnumerateArray())
        {
            var context = $"{contextPrefix}.{propertyName}[{index++}]";
            if (!ValidateSarefArrayObject(item, context, issues))
                continue;

            if (item.TryGetProperty(idProperty, out var idNode) &&
                TryGetSarefString(idNode, out var id) &&
                !string.IsNullOrWhiteSpace(id) &&
                !ids.Add(id))
            {
                AddSarefIssue(
                    issues,
                    context,
                    $"Дубликат {propertyName}[].{idProperty}.",
                    duplicateCode,
                    $"unique {idProperty}",
                    id);
            }

            ValidateSarefTurnFields(item, context, issues);
        }
    }

    private static string? ValidateSarefFactionLinks(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        if (!root.TryGetProperty("factionLinks", out var factionLinks))
        {
            AddSarefIssue(
                issues,
                $"{contextPrefix}.factionLinks",
                "main_story_saref_state.json должен содержать factionLinks object.",
                "saref_main_story_missing_required_property",
                "factionLinks object",
                "missing");
            return null;
        }

        if (factionLinks.ValueKind != JsonValueKind.Object)
        {
            AddSarefIssue(
                issues,
                $"{contextPrefix}.factionLinks",
                "factionLinks должен быть object.",
                "saref_main_story_object_or_null_not_object",
                "object",
                factionLinks.ValueKind.ToString());
            return null;
        }

        if (!factionLinks.TryGetProperty("visibility", out var visibilityNode) ||
            !TryGetSarefString(visibilityNode, out var visibility))
        {
            AddSarefIssue(
                issues,
                $"{contextPrefix}.factionLinks.visibility",
                "factionLinks.visibility должен быть непустой строкой.",
                "saref_main_story_missing_faction_visibility",
                string.Join("/", SarefMainStoryState.FactionVisibilityStates),
                "missing");
            return null;
        }

        if (!SarefMainStoryState.FactionVisibilityStates.Contains(visibility))
        {
            AddSarefIssue(
                issues,
                $"{contextPrefix}.factionLinks.visibility",
                "factionLinks.visibility не поддерживается.",
                "saref_main_story_invalid_faction_visibility",
                string.Join("/", SarefMainStoryState.FactionVisibilityStates.OrderBy(value => value, StringComparer.OrdinalIgnoreCase)),
                visibility);
        }

        ValidateSarefWingsShadowTraces(factionLinks, $"{contextPrefix}.factionLinks", issues);
        ValidateSarefWingsKnownAgents(factionLinks, $"{contextPrefix}.factionLinks", issues);
        return visibility;
    }

    private static void ValidateSarefActionableFactionLink(
        string? revealStage,
        JsonElement root,
        string? factionVisibility,
        string contextPrefix,
        List<ValidationIssue> issues)
    {
        if (!SarefMainStoryStageRequiresActionableFaction(revealStage, factionVisibility))
            return;

        if (!root.TryGetProperty("factionLinks", out var factionLinks) ||
            factionLinks.ValueKind != JsonValueKind.Object ||
            !factionLinks.TryGetProperty("wingsFactionId", out var wingsFactionIdNode) ||
            !TryGetSarefString(wingsFactionIdNode, out _))
        {
            AddSarefIssue(
                issues,
                $"{contextPrefix}.factionLinks.wingsFactionId",
                "Раскрытые Крылья Ангелов должны иметь wingsFactionId для actionable Shining faction actor.",
                "saref_main_story_wings_revealed_missing_faction_id",
                "non-empty factionLinks.wingsFactionId",
                "missing");
        }
    }

    private static bool SarefMainStoryStageRequiresActionableFaction(string? revealStage, string? factionVisibility) =>
        (!string.IsNullOrWhiteSpace(revealStage) &&
         SarefMainStoryState.RevealStages.Contains(revealStage) &&
         StageRank(revealStage) >= StageRank(SarefMainStoryState.RevealStageWingsRevealed)) ||
        string.Equals(factionVisibility, SarefMainStoryState.FactionVisibilityRevealed, StringComparison.OrdinalIgnoreCase);

    private static void ValidateSarefWingsShadowTraces(JsonElement factionLinks, string contextPrefix, List<ValidationIssue> issues)
    {
        if (!factionLinks.TryGetProperty("shadowTraces", out var traces) || traces.ValueKind == JsonValueKind.Null)
            return;

        if (traces.ValueKind != JsonValueKind.Array)
        {
            AddSarefIssue(
                issues,
                $"{contextPrefix}.shadowTraces",
                "factionLinks.shadowTraces должен быть массивом.",
                "saref_main_story_wings_shadow_traces_not_array",
                "array",
                traces.ValueKind.ToString());
            return;
        }

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var index = 0;
        foreach (var item in traces.EnumerateArray())
        {
            var itemContext = $"{contextPrefix}.shadowTraces[{index++}]";
            if (!ValidateSarefArrayObject(item, itemContext, issues))
                continue;

            var traceId = RequireSarefString(item, itemContext, "traceId", "saref_main_story_wings_shadow_trace_missing_id", issues);
            if (!string.IsNullOrWhiteSpace(traceId) && !ids.Add(traceId))
            {
                AddSarefIssue(
                    issues,
                    itemContext,
                    "Дубликат factionLinks.shadowTraces[].traceId.",
                    "saref_main_story_wings_shadow_trace_duplicate_id",
                    "unique traceId",
                    traceId);
            }

            var stage = RequireSarefString(item, itemContext, "stage", "saref_main_story_wings_shadow_trace_missing_stage", issues);
            if (!string.IsNullOrWhiteSpace(stage) && !SarefMainStoryState.WingsTraceStages.Contains(stage))
            {
                AddSarefIssue(
                    issues,
                    $"{itemContext}.stage",
                    "stage следа Крыльев должен быть shadow/name/faction.",
                    "saref_main_story_wings_shadow_trace_invalid_stage",
                    string.Join("/", SarefMainStoryState.WingsTraceStages),
                    stage);
            }

            RequireSarefString(item, itemContext, "summary", "saref_main_story_wings_shadow_trace_missing_summary", issues);
            ValidateSarefTurnFields(item, itemContext, issues);
        }
    }

    private static void ValidateSarefWingsKnownAgents(JsonElement factionLinks, string contextPrefix, List<ValidationIssue> issues)
    {
        if (!factionLinks.TryGetProperty("knownAgents", out var agents) || agents.ValueKind == JsonValueKind.Null)
            return;

        if (agents.ValueKind != JsonValueKind.Array)
        {
            AddSarefIssue(
                issues,
                $"{contextPrefix}.knownAgents",
                "factionLinks.knownAgents должен быть массивом.",
                "saref_main_story_wings_agents_not_array",
                "array",
                agents.ValueKind.ToString());
            return;
        }

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var archetypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var validAgentCount = 0;
        var index = 0;
        foreach (var item in agents.EnumerateArray())
        {
            var itemContext = $"{contextPrefix}.knownAgents[{index++}]";
            if (!ValidateSarefArrayObject(item, itemContext, issues))
                continue;

            var agentId = RequireSarefString(item, itemContext, "agentId", "saref_main_story_wings_agent_missing_id", issues);
            if (!string.IsNullOrWhiteSpace(agentId) && !ids.Add(agentId))
            {
                AddSarefIssue(
                    issues,
                    itemContext,
                    "Дубликат factionLinks.knownAgents[].agentId.",
                    "saref_main_story_wings_agent_duplicate_id",
                    "unique agentId",
                    agentId);
            }

            var archetype = RequireSarefString(item, itemContext, "supporterArchetype", "saref_main_story_wings_agent_missing_archetype", issues);
            if (!string.IsNullOrWhiteSpace(archetype) && SarefMainStoryState.WingsSupporterArchetypes.Contains(archetype))
            {
                archetypes.Add(archetype);
                validAgentCount++;
            }
            else if (!string.IsNullOrWhiteSpace(archetype))
            {
                AddSarefIssue(
                    issues,
                    $"{itemContext}.supporterArchetype",
                    "supporterArchetype агента Крыльев не поддерживается.",
                    "saref_main_story_wings_agent_invalid_archetype",
                    string.Join("/", SarefMainStoryState.WingsSupporterArchetypes),
                    archetype);
            }

            ValidateSarefWingsAgentInteractionRoutes(item, itemContext, issues);
            ValidateSarefTurnFields(item, itemContext, issues);
        }

        if (validAgentCount >= 2 && archetypes.Count < 2)
        {
            AddSarefIssue(
                issues,
                $"{contextPrefix}.knownAgents",
                "Известные агенты Крыльев не могут все иметь один archetype; Сареф вербует обманутых, связанных клятвой, фанатиков и оппортунистов.",
                "saref_main_story_wings_agents_need_mixed_archetypes",
                "at least two supporterArchetype values when 2+ knownAgents are present",
                string.Join(", ", archetypes));
        }
    }

    private static void ValidateSarefWingsAgentInteractionRoutes(JsonElement item, string itemContext, List<ValidationIssue> issues)
    {
        var isImportant =
            string.Equals(GetSarefOptionalString(item, "importance"), "important", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(GetSarefOptionalString(item, "importance"), "lieutenant", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(GetSarefOptionalString(item, "agentRank"), "important", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(GetSarefOptionalString(item, "agentRank"), "lieutenant", StringComparison.OrdinalIgnoreCase);

        if (!item.TryGetProperty("interactionRoutes", out var routes))
        {
            if (isImportant)
            {
                AddSarefIssue(
                    issues,
                    $"{itemContext}.interactionRoutes",
                    "Важный агент/лейтенант Крыльев должен иметь route взаимодействия: убедить, освободить, разоблачить, шантажировать или победить.",
                    "saref_main_story_wings_agent_missing_interaction_routes",
                    string.Join("/", SarefMainStoryState.WingsAgentInteractionRoutes),
                    "missing");
            }

            return;
        }

        if (routes.ValueKind != JsonValueKind.Array)
        {
            AddSarefIssue(
                issues,
                $"{itemContext}.interactionRoutes",
                "interactionRoutes агента Крыльев должен быть массивом.",
                "saref_main_story_wings_agent_routes_not_array",
                "array",
                routes.ValueKind.ToString());
            return;
        }

        var routeCount = 0;
        foreach (var routeNode in routes.EnumerateArray())
        {
            if (TryGetSarefString(routeNode, out var route) &&
                SarefMainStoryState.WingsAgentInteractionRoutes.Contains(route))
            {
                routeCount++;
                continue;
            }

            AddSarefIssue(
                issues,
                $"{itemContext}.interactionRoutes",
                "interactionRoutes агента Крыльев содержит неподдерживаемый route.",
                "saref_main_story_wings_agent_invalid_interaction_route",
                string.Join("/", SarefMainStoryState.WingsAgentInteractionRoutes),
                routeNode.ValueKind.ToString());
        }

        if (isImportant && routeCount == 0)
        {
            AddSarefIssue(
                issues,
                $"{itemContext}.interactionRoutes",
                "Важный агент/лейтенант Крыльев должен иметь хотя бы один valid interaction route.",
                "saref_main_story_wings_agent_missing_interaction_routes",
                string.Join("/", SarefMainStoryState.WingsAgentInteractionRoutes),
                "empty");
        }
    }

    private static void ValidateSarefNullableStateObject(
        JsonElement root,
        string contextPrefix,
        string propertyName,
        string stateProperty,
        HashSet<string> allowedStates,
        string invalidCode,
        List<ValidationIssue> issues)
    {
        if (!root.TryGetProperty(propertyName, out var node) || node.ValueKind == JsonValueKind.Null)
            return;

        string? state = null;
        if (node.ValueKind == JsonValueKind.String)
        {
            state = node.GetString();
        }
        else if (node.ValueKind == JsonValueKind.Object)
        {
            state = RequireSarefString(node, $"{contextPrefix}.{propertyName}", stateProperty, $"saref_main_story_missing_{propertyName}", issues);
            ValidateSarefTurnFields(node, $"{contextPrefix}.{propertyName}", issues);
        }
        else
        {
            AddSarefIssue(
                issues,
                $"{contextPrefix}.{propertyName}",
                $"{propertyName} должен быть null, string или object.",
                "saref_main_story_object_or_null_not_object",
                "null/string/object",
                node.ValueKind.ToString());
        }

        if (!string.IsNullOrWhiteSpace(state) && !allowedStates.Contains(state))
        {
            AddSarefIssue(
                issues,
                $"{contextPrefix}.{propertyName}.{stateProperty}",
                $"{propertyName}.{stateProperty} не поддерживается.",
                invalidCode,
                string.Join("/", allowedStates.OrderBy(value => value, StringComparer.OrdinalIgnoreCase)),
                state);
        }
    }

    private static void ValidateSarefNullableObject(JsonElement root, string contextPrefix, string propertyName, List<ValidationIssue> issues)
    {
        if (!root.TryGetProperty(propertyName, out var node) || node.ValueKind == JsonValueKind.Null)
            return;

        if (node.ValueKind != JsonValueKind.Object)
        {
            AddSarefIssue(
                issues,
                $"{contextPrefix}.{propertyName}",
                $"{propertyName} должен быть null или object.",
                "saref_main_story_object_or_null_not_object",
                "null/object",
                node.ValueKind.ToString());
            return;
        }

        ValidateSarefTurnFields(node, $"{contextPrefix}.{propertyName}", issues);
    }

    private static bool TryGetRequiredSarefArray(
        JsonElement root,
        string contextPrefix,
        string propertyName,
        List<ValidationIssue> issues,
        out JsonElement array)
    {
        array = default;
        if (!root.TryGetProperty(propertyName, out var node))
        {
            AddSarefIssue(
                issues,
                $"{contextPrefix}.{propertyName}",
                $"main_story_saref_state.json должен содержать {propertyName}[].",
                "saref_main_story_missing_required_property",
                $"{propertyName}[]",
                "missing");
            return false;
        }

        if (node.ValueKind != JsonValueKind.Array)
        {
            AddSarefIssue(
                issues,
                $"{contextPrefix}.{propertyName}",
                $"{propertyName} должен быть массивом.",
                "saref_main_story_array_not_array",
                "array",
                node.ValueKind.ToString());
            return false;
        }

        array = node;
        return true;
    }

    private static bool ValidateSarefArrayObject(JsonElement item, string context, List<ValidationIssue> issues)
    {
        if (item.ValueKind == JsonValueKind.Object)
            return true;

        AddSarefIssue(
            issues,
            context,
            "Элемент массива main_story_saref_state.json должен быть object.",
            "saref_main_story_entry_not_object",
            "object",
            item.ValueKind.ToString());
        return false;
    }

    private static void ValidateSarefTurnFields(JsonElement item, string context, List<ValidationIssue> issues)
    {
        foreach (var fieldName in new[]
                 {
                     "createdAtTurn", "updatedAtTurn", "recognizedAtTurn", "revealedAtTurn", "unlockedAtTurn",
                     "spentAtTurn", "disabledAtTurn", "suppressedAtTurn", "startedAtTurn", "completedAtTurn",
                     "resolvedAtTurn", "defeatedAtTurn", "endedAtTurn"
                 })
        {
            if (!item.TryGetProperty(fieldName, out var value))
                continue;

            if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var turn))
            {
                AddSarefIssue(
                    issues,
                    $"{context}.{fieldName}",
                    "Turn-поля main_story_saref_state.json должны быть целыми числами.",
                    "saref_main_story_invalid_turn",
                    "integer >= 0",
                    value.ValueKind.ToString());
            }
            else if (turn < 0)
            {
                AddSarefIssue(
                    issues,
                    $"{context}.{fieldName}",
                    "Turn-поля main_story_saref_state.json не могут быть отрицательными.",
                    "saref_main_story_negative_turn",
                    "integer >= 0",
                    turn.ToString());
            }
        }
    }

    private static int RequireSarefQuestOrdinal(JsonElement item, string context, List<ValidationIssue> issues)
    {
        if (!item.TryGetProperty("questOrdinal", out var node))
        {
            AddSarefIssue(
                issues,
                $"{context}.questOrdinal",
                "questOrdinal должен быть целым числом от 1 до 4.",
                "saref_main_story_missing_quest_ordinal",
                "integer 1..4",
                "missing");
            return 0;
        }

        if (node.ValueKind != JsonValueKind.Number || !node.TryGetInt32(out var ordinal) || ordinal is < 1 or > 4)
        {
            AddSarefIssue(
                issues,
                $"{context}.questOrdinal",
                "questOrdinal должен быть целым числом от 1 до 4.",
                "saref_main_story_invalid_quest_ordinal",
                "integer 1..4",
                node.ValueKind == JsonValueKind.Number ? node.ToString() : node.ValueKind.ToString());
            return 0;
        }

        return ordinal;
    }

    private static int ResolveSarefSourceQuestOrdinal(JsonElement item, string context, List<ValidationIssue> issues)
    {
        if (item.TryGetProperty("sourceQuestOrdinal", out var ordinalNode))
        {
            if (ordinalNode.ValueKind == JsonValueKind.Number &&
                ordinalNode.TryGetInt32(out var ordinal) &&
                ordinal is >= 1 and <= 4)
            {
                return ordinal;
            }

            AddSarefIssue(
                issues,
                $"{context}.sourceQuestOrdinal",
                "sourceQuestOrdinal должен быть целым числом от 1 до 4.",
                "saref_main_story_invalid_source_quest_ordinal",
                "integer 1..4",
                ordinalNode.ValueKind == JsonValueKind.Number ? ordinalNode.ToString() : ordinalNode.ValueKind.ToString());
            return 0;
        }

        var sourceQuestId = GetSarefOptionalString(item, "sourceQuestId");
        if (string.IsNullOrWhiteSpace(sourceQuestId))
            return 0;

        var trimmed = sourceQuestId.Trim();
        for (var ordinal = 1; ordinal <= 4; ordinal++)
        {
            if (trimmed.EndsWith($"q{ordinal}", StringComparison.OrdinalIgnoreCase) ||
                trimmed.EndsWith($"quest_{ordinal}", StringComparison.OrdinalIgnoreCase) ||
                trimmed.EndsWith($"quest{ordinal}", StringComparison.OrdinalIgnoreCase))
            {
                return ordinal;
            }
        }

        return 0;
    }

    private static bool ContainsForbiddenSarefPhysicalEvidenceField(JsonElement node)
    {
        if (node.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in node.EnumerateObject())
            {
                if (GuardianProjectState.IsForbiddenQuestPhysicalEvidenceField(property.Name) ||
                    ContainsForbiddenSarefPhysicalEvidenceField(property.Value))
                {
                    return true;
                }
            }
        }
        else if (node.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in node.EnumerateArray())
            {
                if (ContainsForbiddenSarefPhysicalEvidenceField(item))
                    return true;
            }
        }

        return false;
    }

    private static string? RequireSarefString(
        JsonElement root,
        string context,
        string propertyName,
        string missingCode,
        List<ValidationIssue> issues)
    {
        if (root.TryGetProperty(propertyName, out var node) &&
            TryGetSarefString(node, out var value))
        {
            return value;
        }

        AddSarefIssue(
            issues,
            $"{context}.{propertyName}",
            $"{propertyName} должен быть непустой строкой.",
            missingCode,
            "non-empty string",
            root.TryGetProperty(propertyName, out var present) ? present.ValueKind.ToString() : "missing");
        return null;
    }

    private static string? GetSarefOptionalString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var node) || !TryGetSarefString(node, out var value))
            return null;

        return value;
    }

    private static bool? GetSarefOptionalBool(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var node))
            return null;

        return node.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null
        };
    }

    private static int RequireSarefTurnNumber(
        JsonElement root,
        string context,
        string propertyName,
        string missingCode,
        List<ValidationIssue> issues)
    {
        if (!root.TryGetProperty(propertyName, out var node) ||
            node.ValueKind != JsonValueKind.Number ||
            !node.TryGetInt32(out var value))
        {
            AddSarefIssue(
                issues,
                $"{context}.{propertyName}",
                $"{propertyName} должен быть целым числом >= 0.",
                missingCode,
                "integer >= 0",
                root.TryGetProperty(propertyName, out var present) ? present.ValueKind.ToString() : "missing");
            return 0;
        }

        if (value < 0)
        {
            AddSarefIssue(
                issues,
                $"{context}.{propertyName}",
                $"{propertyName} не может быть отрицательным.",
                "saref_main_story_negative_turn",
                "integer >= 0",
                value.ToString());
        }

        return value;
    }

    private static bool TryGetSarefString(JsonElement node, out string value)
    {
        value = string.Empty;
        if (node.ValueKind != JsonValueKind.String)
            return false;

        value = node.GetString()?.Trim() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(value);
    }

    private static HashSet<string> ValidateSarefWingsFragments(
        JsonElement root,
        string contextPrefix,
        string propertyName,
        List<ValidationIssue> issues,
        bool allowEmpty = false)
    {
        var categories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!root.TryGetProperty(propertyName, out var array))
        {
            AddSarefWingsIssue(
                issues,
                $"{contextPrefix}.{propertyName}",
                $"{propertyName} должен быть массивом фрагментов маршрута.",
                "saref_wings_pending_missing_array",
                "array",
                "missing");
            return categories;
        }

        if (array.ValueKind != JsonValueKind.Array)
        {
            AddSarefWingsIssue(
                issues,
                $"{contextPrefix}.{propertyName}",
                $"{propertyName} должен быть массивом.",
                "saref_wings_pending_array_not_array",
                "array",
                array.ValueKind.ToString());
            return categories;
        }

        if (!allowEmpty && array.GetArrayLength() == 0)
        {
            AddSarefWingsIssue(
                issues,
                $"{contextPrefix}.{propertyName}",
                $"{propertyName} не может быть пустым.",
                "saref_wings_pending_empty_route_fragments",
                "non-empty array",
                "empty");
        }

        var index = 0;
        foreach (var item in array.EnumerateArray())
        {
            var itemContext = $"{contextPrefix}.{propertyName}[{index++}]";
            if (item.ValueKind != JsonValueKind.Object)
            {
                AddSarefWingsIssue(
                    issues,
                    itemContext,
                    "Фрагмент маршрута поиска Крыльев должен быть object.",
                    "saref_wings_pending_fragment_not_object",
                    "object",
                    item.ValueKind.ToString());
                continue;
            }

            RequireSarefString(item, itemContext, "revelationId", "saref_wings_pending_fragment_missing_revelation_id", issues);
            var category = RequireSarefString(item, itemContext, "category", "saref_wings_pending_fragment_missing_category", issues);
            if (!string.IsNullOrWhiteSpace(category))
            {
                if (!SarefMainStoryState.RevelationCategories.Contains(category))
                {
                    AddSarefWingsIssue(
                        issues,
                        $"{itemContext}.category",
                        "Категория фрагмента маршрута поиска Крыльев не поддерживается.",
                        "saref_wings_pending_invalid_fragment_category",
                        string.Join("/", SarefMainStoryState.RevelationCategories),
                        category);
                }
                else
                {
                    categories.Add(category);
                }
            }
        }

        return categories;
    }

    private static void ValidateSarefWingsArray(
        JsonElement root,
        string contextPrefix,
        string propertyName,
        List<ValidationIssue> issues)
    {
        if (!root.TryGetProperty(propertyName, out var array))
        {
            AddSarefWingsIssue(
                issues,
                $"{contextPrefix}.{propertyName}",
                $"{propertyName} должен быть массивом.",
                "saref_wings_pending_missing_array",
                "array",
                "missing");
            return;
        }

        if (array.ValueKind != JsonValueKind.Array)
        {
            AddSarefWingsIssue(
                issues,
                $"{contextPrefix}.{propertyName}",
                $"{propertyName} должен быть массивом.",
                "saref_wings_pending_array_not_array",
                "array",
                array.ValueKind.ToString());
        }
    }

    private static List<string> ValidateSarefWingsStringArray(
        JsonElement root,
        string contextPrefix,
        string propertyName,
        List<ValidationIssue> issues)
    {
        var result = new List<string>();
        if (!root.TryGetProperty(propertyName, out var array))
        {
            AddSarefWingsIssue(
                issues,
                $"{contextPrefix}.{propertyName}",
                $"{propertyName} должен быть массивом строк.",
                "saref_wings_pending_missing_array",
                "array",
                "missing");
            return result;
        }

        if (array.ValueKind != JsonValueKind.Array)
        {
            AddSarefWingsIssue(
                issues,
                $"{contextPrefix}.{propertyName}",
                $"{propertyName} должен быть массивом строк.",
                "saref_wings_pending_array_not_array",
                "array",
                array.ValueKind.ToString());
            return result;
        }

        var index = 0;
        foreach (var item in array.EnumerateArray())
        {
            if (TryGetSarefString(item, out var value))
            {
                result.Add(value);
            }
            else
            {
                AddSarefWingsIssue(
                    issues,
                    $"{contextPrefix}.{propertyName}[{index}]",
                    $"{propertyName} должен содержать только непустые строки.",
                    "saref_wings_pending_invalid_string_array_item",
                    "non-empty string",
                    item.ValueKind.ToString());
            }

            index++;
        }

        return result;
    }

    private static void ValidateSarefWingsExpectedClosure(
        JsonElement root,
        string contextPrefix,
        string? requestId,
        List<ValidationIssue> issues)
    {
        if (!root.TryGetProperty("expectedClosure", out var expectedClosure) ||
            expectedClosure.ValueKind != JsonValueKind.Object)
        {
            AddSarefWingsIssue(
                issues,
                $"{contextPrefix}.expectedClosure",
                "expectedClosure должен быть object.",
                "saref_wings_pending_missing_expected_closure",
                "object",
                root.TryGetProperty("expectedClosure", out var present) ? present.ValueKind.ToString() : "missing");
            return;
        }

        var mode = RequireSarefString(expectedClosure, $"{contextPrefix}.expectedClosure", "mode", "saref_wings_pending_missing_expected_mode", issues);
        if (!string.IsNullOrWhiteSpace(mode) &&
            !string.Equals(mode, SarefMainStoryState.WingsUpdateModeReveal, StringComparison.OrdinalIgnoreCase))
        {
            AddSarefWingsIssue(
                issues,
                $"{contextPrefix}.expectedClosure.mode",
                "expectedClosure.mode должен быть reveal_wings.",
                "saref_wings_pending_expected_mode_mismatch",
                SarefMainStoryState.WingsUpdateModeReveal,
                mode);
        }

        var closureRequestId = RequireSarefString(expectedClosure, $"{contextPrefix}.expectedClosure", "requestId", "saref_wings_pending_missing_expected_request_id", issues);
        if (!string.IsNullOrWhiteSpace(requestId) &&
            !string.IsNullOrWhiteSpace(closureRequestId) &&
            !string.Equals(requestId, closureRequestId, StringComparison.OrdinalIgnoreCase))
        {
            AddSarefWingsIssue(
                issues,
                $"{contextPrefix}.expectedClosure.requestId",
                "expectedClosure.requestId должен совпадать с requestId.",
                "saref_wings_pending_expected_request_id_mismatch",
                requestId,
                closureRequestId);
        }
    }

    private static void AddSarefWingsIssue(
        List<ValidationIssue> issues,
        string path,
        string message,
        string code,
        string? expected = null,
        string? actual = null)
    {
        issues.Add(new ValidationIssue(
            path,
            IssueSeverity.Error,
            message,
            code: code,
            section: "SarefWingsInfiltration",
            expected: expected,
            actual: actual,
            repairHint: "Исправь pending_saref_wings_infiltration.json или main_story_saref_state.json по контракту поиска Крыльев Ангелов; не оставляй pending request без reveal_wings/refuse_wings/block_wings."));
    }

    private static void AddSarefIssue(
        List<ValidationIssue> issues,
        string path,
        string message,
        string code,
        string? expected = null,
        string? actual = null)
    {
        issues.Add(new ValidationIssue(
            path,
            IssueSeverity.Error,
            message,
            code: code,
            section: "SarefMainStory",
            expected: expected,
            actual: actual,
            repairHint: "Исправь game_state/meta/main_story_saref_state.json по контракту Крыльев над Бездной; не повышай revealStage без canonical evidence."));
    }
}
