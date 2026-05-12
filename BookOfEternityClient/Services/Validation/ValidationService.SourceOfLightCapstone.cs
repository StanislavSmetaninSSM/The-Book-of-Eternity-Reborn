using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    private void ValidatePendingSourceOfLightCapstoneRequestFile(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new ValidationIssue(
                contextPrefix,
                IssueSeverity.Error,
                "pending_source_of_light_capstone.json должен быть JSON object.",
                code: "source_of_light_pending_invalid_root",
                section: "ShiningAbode",
                expected: "JSON object",
                actual: root.ValueKind.ToString()));
            return;
        }

        var requestId = RequireString(root, contextPrefix, issues, "requestId");
        if (!string.IsNullOrWhiteSpace(requestId) &&
            !requestId.StartsWith("source_of_light_capstone:", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.requestId",
                IssueSeverity.Error,
                "Source of Light requestId должен начинаться с source_of_light_capstone:.",
                code: "source_of_light_pending_invalid_request_id",
                section: "ShiningAbode",
                expected: "source_of_light_capstone:<turn>",
                actual: requestId));
        }

        ValidateIntegerField(root, contextPrefix, issues, "createdAtTurn");
        RequireString(root, contextPrefix, issues, "createdAtUtc");
        ValidateIntegerField(root, contextPrefix, issues, "radianceExperienceAtRequest");
        ValidateIntegerField(root, contextPrefix, issues, "radianceTierAtRequest");
        var passiveId = RequireString(root, contextPrefix, issues, "rewardPassiveId");
        var relicId = RequireString(root, contextPrefix, issues, "rewardRelicId");

        if (TryReadInt(root, "createdAtTurn", out var createdAtTurn) && createdAtTurn <= 0)
            AddSourceOfLightFieldIssue(issues, $"{contextPrefix}.createdAtTurn", "source_of_light_pending_invalid_created_turn", "positive integer", createdAtTurn.ToString());
        if (TryReadInt(root, "radianceExperienceAtRequest", out var experience) && experience < SourceOfLightCapstoneState.RequiredRadianceExperience)
            AddSourceOfLightFieldIssue(issues, $"{contextPrefix}.radianceExperienceAtRequest", "source_of_light_pending_low_radiance_experience", $">= {SourceOfLightCapstoneState.RequiredRadianceExperience}", experience.ToString());
        if (TryReadInt(root, "radianceTierAtRequest", out var tier) && tier != SourceOfLightCapstoneState.RequiredRadianceTier)
            AddSourceOfLightFieldIssue(issues, $"{contextPrefix}.radianceTierAtRequest", "source_of_light_pending_invalid_radiance_tier", SourceOfLightCapstoneState.RequiredRadianceTier.ToString(), tier.ToString());
        if (!string.IsNullOrWhiteSpace(passiveId) && !string.Equals(passiveId, SourceOfLightCapstoneState.PassiveId, StringComparison.OrdinalIgnoreCase))
            AddSourceOfLightFieldIssue(issues, $"{contextPrefix}.rewardPassiveId", "source_of_light_pending_passive_mismatch", SourceOfLightCapstoneState.PassiveId, passiveId);
        if (!string.IsNullOrWhiteSpace(relicId) && !string.Equals(relicId, SourceOfLightCapstoneState.RelicId, StringComparison.OrdinalIgnoreCase))
            AddSourceOfLightFieldIssue(issues, $"{contextPrefix}.rewardRelicId", "source_of_light_pending_relic_mismatch", SourceOfLightCapstoneState.RelicId, relicId);
    }

    private async Task ValidatePendingSourceOfLightCapstoneRequestContextAsync(List<ValidationIssue> issues)
    {
        var state = await SourceOfLightCapstoneState.ReadRequestStateAsync(_fs);
        if (!state.Exists)
            return;

        if (state.IsMalformed || state.Request == null)
        {
            issues.Add(new ValidationIssue(
                SourceOfLightCapstoneState.PendingRequestPath,
                IssueSeverity.Error,
                "pending_source_of_light_capstone.json повреждён и не может быть resolved.",
                code: "source_of_light_pending_malformed",
                section: "ShiningAbode",
                expected: "canonical Source of Light pending request object",
                actual: state.Error ?? "malformed",
                repairHint: "Исправь pending_source_of_light_capstone.json по matrix/example или очисти через явный repair path; не угадывай reward ids."));
            return;
        }

        var soulRoot = await ReadJsonObjectAsync("game_state/meta/soul_state.json");
        var shiningRoot = await ReadJsonObjectAsync(ShiningAbodeState.StatePath);
        if (!SourceOfLightCapstoneState.IsUnlockSatisfied(soulRoot, shiningRoot, out var blocker))
        {
            issues.Add(new ValidationIssue(
                SourceOfLightCapstoneState.PendingRequestPath,
                IssueSeverity.Error,
                "Source of Light pending request допустим только для полного Сияния в ordinary active Shining Abode.",
                code: "source_of_light_pending_invalid_mode_or_unlock",
                section: "ShiningAbode",
                expected: "currentRealm=Shining Abode, availability=active, no preparedIncarnationPackage, radiance tier 4 and experience >= 580",
                actual: blocker,
                repairHint: "Не создавай и не resolve Источник Света вне ordinary active Shining Abode или до полного Radiance."));
            return;
        }

        var radiance = shiningRoot?["radiance"] as JsonObject;
        var experience = SourceOfLightCapstoneState.GetNodeInt(radiance?["experience"]);
        var tier = SourceOfLightCapstoneState.GetNodeInt(radiance?["tier"]);
        if (state.Request.RadianceExperienceAtRequest != experience ||
            state.Request.RadianceTierAtRequest != tier)
        {
            issues.Add(new ValidationIssue(
                SourceOfLightCapstoneState.PendingRequestPath,
                IssueSeverity.Error,
                "Source of Light pending request должен сохранять exact Radiance snapshot на момент создания.",
                code: "source_of_light_pending_radiance_snapshot_mismatch",
                section: "ShiningAbode",
                expected: $"radianceExperienceAtRequest={experience}; radianceTierAtRequest={tier}",
                actual: $"{state.Request.RadianceExperienceAtRequest}; {state.Request.RadianceTierAtRequest}",
                repairHint: "Не редактируй pending_source_of_light_capstone.json после создания клиентом."));
        }
    }

    private async Task ValidatePendingSourceOfLightCapstoneResolutionAsync(List<ValidationIssue> issues)
    {
        var preTurnRequestJson = await ReadValidatedCurrentPreTurnTrackedFileAsync(SourceOfLightCapstoneState.PendingRequestPath);
        if (string.IsNullOrWhiteSpace(preTurnRequestJson))
            return;

        var requestState = SourceOfLightCapstoneState.ReadRequestState(preTurnRequestJson, exists: true);
        if (requestState.IsMalformed || requestState.Request == null)
        {
            issues.Add(new ValidationIssue(
                SourceOfLightCapstoneState.PendingRequestPath,
                IssueSeverity.Error,
                "validated snapshot pending_source_of_light_capstone.json malformed.",
                code: "source_of_light_malformed_validated_snapshot_request",
                section: "ShiningAbode",
                actual: requestState.Error ?? "malformed"));
            return;
        }

        var request = requestState.Request;
        var preTurnShiningJson = await ReadRequiredValidatedCurrentPreTurnTrackedFileAsync(
            ShiningAbodeState.StatePath,
            issues,
            code: "source_of_light_missing_pre_turn_shining_state",
            section: "ShiningAbode",
            message: "Source of Light closure требует validated pre-turn shining_abode_state.json.",
            repairHint: "Сохраняй pre-turn shining_abode_state.json в pending_turn_snapshot для строгой проверки Radiance unlock.");
        var preTurnSoulJson = await ReadRequiredValidatedCurrentPreTurnTrackedFileAsync(
            "game_state/meta/soul_state.json",
            issues,
            code: "source_of_light_missing_pre_turn_soul_state",
            section: "ShiningAbode",
            message: "Source of Light closure требует validated pre-turn soul_state.json.",
            repairHint: "Сохраняй pre-turn soul_state.json в pending_turn_snapshot для anti-dup проверки capstone passive/relic.");
        var currentShiningJson = await _fs.ReadFileAsync(ShiningAbodeState.StatePath);
        var currentSoulJson = await _fs.ReadFileAsync("game_state/meta/soul_state.json");

        if (string.IsNullOrWhiteSpace(preTurnShiningJson) ||
            string.IsNullOrWhiteSpace(preTurnSoulJson) ||
            string.IsNullOrWhiteSpace(currentShiningJson) ||
            string.IsNullOrWhiteSpace(currentSoulJson))
        {
            if (string.IsNullOrWhiteSpace(currentShiningJson))
                AddMissingSourceOfLightFileIssue(issues, ShiningAbodeState.StatePath, "source_of_light_missing_current_shining_state");
            if (string.IsNullOrWhiteSpace(currentSoulJson))
                AddMissingSourceOfLightFileIssue(issues, "game_state/meta/soul_state.json", "source_of_light_missing_current_soul_state");
            return;
        }

        try
        {
            var preTurnShiningRoot = JsonNode.Parse(preTurnShiningJson) as JsonObject;
            var preTurnSoulRoot = JsonNode.Parse(preTurnSoulJson) as JsonObject;
            var currentShiningRoot = JsonNode.Parse(currentShiningJson) as JsonObject;
            var currentSoulRoot = JsonNode.Parse(currentSoulJson) as JsonObject;
            if (preTurnShiningRoot == null || preTurnSoulRoot == null || currentShiningRoot == null || currentSoulRoot == null)
                return;

            ShiningAbodeState.NormalizeStateRoot(preTurnShiningRoot, residentRoot: null);
            ShiningAbodeState.NormalizeStateRoot(currentShiningRoot, residentRoot: null);

            if (!SourceOfLightCapstoneState.IsUnlockSatisfied(preTurnSoulRoot, preTurnShiningRoot, out var unlockBlocker))
            {
                issues.Add(new ValidationIssue(
                    SourceOfLightCapstoneState.PendingRequestPath,
                    IssueSeverity.Error,
                    "Source of Light closure нельзя принять: pre-turn состояние не удовлетворяло unlock contract.",
                    code: "source_of_light_closure_pre_turn_unlock_missing",
                    section: "ShiningAbode",
                    expected: "validated pre-turn ordinary active Shining Abode with radiance tier 4 / experience >= 580",
                    actual: unlockBlocker));
            }

            var preTurnRadiance = preTurnShiningRoot["radiance"] as JsonObject;
            if (request.RadianceExperienceAtRequest != SourceOfLightCapstoneState.GetNodeInt(preTurnRadiance?["experience"]) ||
                request.RadianceTierAtRequest != SourceOfLightCapstoneState.GetNodeInt(preTurnRadiance?["tier"]))
            {
                issues.Add(new ValidationIssue(
                    SourceOfLightCapstoneState.PendingRequestPath,
                    IssueSeverity.Error,
                    "Source of Light request Radiance snapshot не совпадает с validated pre-turn Shining state.",
                    code: "source_of_light_closure_request_snapshot_mismatch",
                    section: "ShiningAbode",
                    expected: $"pre-turn radiance {SourceOfLightCapstoneState.GetNodeInt(preTurnRadiance?["experience"])}/tier {SourceOfLightCapstoneState.GetNodeInt(preTurnRadiance?["tier"])}",
                    actual: $"{request.RadianceExperienceAtRequest}/tier {request.RadianceTierAtRequest}"));
            }

            if (SourceOfLightCapstoneState.HasLightIncarnate(preTurnSoulRoot) ||
                SourceOfLightCapstoneState.CountIncarnatedLightRelics(preTurnSoulRoot) > 0 ||
                SourceOfLightCapstoneState.HasCompletedCapstone(preTurnShiningRoot))
            {
                issues.Add(new ValidationIssue(
                    SourceOfLightCapstoneState.PendingRequestPath,
                    IssueSeverity.Error,
                    "Source of Light capstone уже существовал до closure; повторная награда запрещена.",
                    code: "source_of_light_closure_duplicate_pre_turn_reward",
                    section: "ShiningAbode",
                    expected: "no pre-turn light_incarnate passive, Incarnated Light relic, or completed marker",
                    actual: "pre-turn capstone reward already present"));
            }

            if (!SourceOfLightCapstoneState.HasCompletedCapstone(currentShiningRoot, request))
            {
                issues.Add(new ValidationIssue(
                    $"{ShiningAbodeState.StatePath}.{SourceOfLightCapstoneState.ShiningStateProperty}",
                    IssueSeverity.Error,
                    "Accepted Source of Light closure должен записать matching completed marker в Shining state.",
                    code: "source_of_light_missing_completed_marker",
                    section: "ShiningAbode",
                    expected: "sourceOfLightCapstone.completed=true with matching request/radiance/reward ids",
                    actual: currentShiningRoot[SourceOfLightCapstoneState.ShiningStateProperty]?.ToJsonString() ?? "missing"));
            }

            if (!SourceOfLightCapstoneState.HasLightIncarnate(currentSoulRoot))
            {
                issues.Add(new ValidationIssue(
                    $"game_state/meta/soul_state.json.{AfterlifeSpiritualConflictState.SoulStateProfileProperty}.{SourceOfLightCapstoneState.CapstonesProperty}.{SourceOfLightCapstoneState.LightIncarnateProperty}",
                    IssueSeverity.Error,
                    "Accepted Source of Light closure должен grant soul-owned passive light_incarnate.",
                    code: "source_of_light_missing_light_incarnate_passive",
                    section: "ShiningAbode",
                    expected: "afterlifeCombatProfile.capstones.lightIncarnate.passiveId=light_incarnate",
                    actual: "missing"));
            }

            var relicCount = SourceOfLightCapstoneState.CountIncarnatedLightRelics(currentSoulRoot);
            if (relicCount != 1)
            {
                issues.Add(new ValidationIssue(
                    "game_state/meta/soul_state.json.soulRelics",
                    IssueSeverity.Error,
                    "Accepted Source of Light closure должен иметь ровно одну Soul Relic Воплощенный Свет.",
                    code: relicCount == 0 ? "source_of_light_missing_incarnated_light_relic" : "source_of_light_duplicate_incarnated_light_relic",
                    section: "ShiningAbode",
                    expected: "exactly one source_of_light_incarnated_light relic",
                    actual: relicCount.ToString()));
            }

            if (SourceOfLightCapstoneState.HasMatchingClosure(currentShiningRoot, currentSoulRoot, request))
                ValidateIncarnatedLightRelicPayload(currentSoulRoot, issues);
        }
        catch (JsonException)
        {
            // JSON integrity and state-file validators report malformed roots.
        }
    }

    private async Task ValidateSourceOfLightCapstoneGlobalStateAsync(List<ValidationIssue> issues)
    {
        var soulRoot = await ReadJsonObjectAsync("game_state/meta/soul_state.json");
        var shiningRoot = await ReadJsonObjectAsync(ShiningAbodeState.StatePath);
        if (soulRoot == null)
            return;

        var relicCount = SourceOfLightCapstoneState.CountIncarnatedLightRelics(soulRoot);
        if (relicCount > 1)
        {
            issues.Add(new ValidationIssue(
                "game_state/meta/soul_state.json.soulRelics",
                IssueSeverity.Error,
                "Воплощенный Свет является one-per-soul Soul Relic и не должен дублироваться.",
                code: "source_of_light_duplicate_incarnated_light_relic",
                section: "ShiningAbode",
                expected: "0 or 1 source_of_light_incarnated_light relic",
                actual: relicCount.ToString()));
        }

        if (SourceOfLightCapstoneState.HasCompletedCapstone(shiningRoot) &&
            (!SourceOfLightCapstoneState.HasLightIncarnate(soulRoot) || relicCount != 1))
        {
            issues.Add(new ValidationIssue(
                $"{ShiningAbodeState.StatePath}.{SourceOfLightCapstoneState.ShiningStateProperty}",
                IssueSeverity.Error,
                "Completed Source of Light marker должен быть согласован с passive light_incarnate и одной relic Воплощенный Свет.",
                code: "source_of_light_completed_marker_reward_mismatch",
                section: "ShiningAbode",
                expected: "completed marker + light_incarnate + exactly one source_of_light_incarnated_light",
                actual: $"hasPassive={SourceOfLightCapstoneState.HasLightIncarnate(soulRoot)}, relicCount={relicCount}"));
        }
    }

    private void ValidateSourceOfLightCapstoneMarker(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        if (!root.TryGetProperty(SourceOfLightCapstoneState.ShiningStateProperty, out var marker) ||
            marker.ValueKind == JsonValueKind.Null)
        {
            return;
        }

        var context = $"{contextPrefix}.{SourceOfLightCapstoneState.ShiningStateProperty}";
        if (!RequireObject(marker, context, issues))
            return;

        RequireBooleanField(marker, context, issues, "completed");
        RequireString(marker, context, issues, "requestId");
        RequireString(marker, context, issues, "rewardPassiveId");
        RequireString(marker, context, issues, "rewardRelicId");
        ValidateIntegerField(marker, context, issues, "radianceExperienceAtRequest");
        ValidateIntegerField(marker, context, issues, "radianceTierAtRequest");

        var passiveId = GetFirstNonEmptyString(marker, "rewardPassiveId");
        if (!string.IsNullOrWhiteSpace(passiveId) && !string.Equals(passiveId, SourceOfLightCapstoneState.PassiveId, StringComparison.OrdinalIgnoreCase))
            AddSourceOfLightFieldIssue(issues, $"{context}.rewardPassiveId", "source_of_light_marker_passive_mismatch", SourceOfLightCapstoneState.PassiveId, passiveId);

        var relicId = GetFirstNonEmptyString(marker, "rewardRelicId");
        if (!string.IsNullOrWhiteSpace(relicId) && !string.Equals(relicId, SourceOfLightCapstoneState.RelicId, StringComparison.OrdinalIgnoreCase))
            AddSourceOfLightFieldIssue(issues, $"{context}.rewardRelicId", "source_of_light_marker_relic_mismatch", SourceOfLightCapstoneState.RelicId, relicId);
    }

    private void ValidateLightIncarnateCombatProfileCapstone(JsonElement profile, string context, List<ValidationIssue> issues)
    {
        if (!profile.TryGetProperty(SourceOfLightCapstoneState.CapstonesProperty, out var capstones))
            return;

        if (!RequireObject(capstones, $"{context}.{SourceOfLightCapstoneState.CapstonesProperty}", issues))
            return;

        if (!capstones.TryGetProperty(SourceOfLightCapstoneState.LightIncarnateProperty, out var lightIncarnate))
            return;

        var capstoneContext = $"{context}.{SourceOfLightCapstoneState.CapstonesProperty}.{SourceOfLightCapstoneState.LightIncarnateProperty}";
        if (!RequireObject(lightIncarnate, capstoneContext, issues))
            return;

        var passiveId = GetFirstNonEmptyString(lightIncarnate, "passiveId", "id");
        if (!string.Equals(passiveId, SourceOfLightCapstoneState.PassiveId, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new ValidationIssue(
                $"{capstoneContext}.passiveId",
                IssueSeverity.Error,
                "lightIncarnate capstone должен иметь passiveId/id=light_incarnate.",
                code: "afterlife_combat_profile_light_incarnate_invalid_id",
                section: "AfterlifeSpiritualConflict",
                expected: SourceOfLightCapstoneState.PassiveId,
                actual: string.IsNullOrWhiteSpace(passiveId) ? "missing" : passiveId));
        }
    }

    private static void ValidateIncarnatedLightRelicPayload(JsonObject soulRoot, List<ValidationIssue> issues)
    {
        var relic = EnumerateSourceOfLightRelics(soulRoot).SingleOrDefault();
        if (relic == null)
            return;

        var bonuses = relic["effects"]?["characteristicBonuses"] as JsonObject;
        foreach (var characteristic in Characteristics.All)
        {
            if (SourceOfLightCapstoneState.GetNodeInt(bonuses?[characteristic]) == SourceOfLightCapstoneState.MortalCharacteristicBonus)
                continue;

            issues.Add(new ValidationIssue(
                "game_state/meta/soul_state.json.soulRelics",
                IssueSeverity.Error,
                "Воплощенный Свет должен давать +25 ко всем основным Mortal characteristics через effects.characteristicBonuses.",
                code: "source_of_light_relic_missing_characteristic_bonus",
                section: "ShiningAbode",
                expected: $"{characteristic}=+{SourceOfLightCapstoneState.MortalCharacteristicBonus}",
                actual: bonuses?[characteristic]?.ToJsonString() ?? "missing"));
        }
    }

    private static void ValidateLightIncarnateDiceAuditModifier(
        JsonObject payload,
        JsonObject diceAudit,
        string context,
        List<ValidationIssue> issues,
        AfterlifeConflictDiceContext diceContext)
    {
        if (!diceContext.HasLightIncarnate)
            return;

        var expected = ResolveLightIncarnateExpectedDiceBonus(payload);
        var actual = SourceOfLightCapstoneState.SumLightIncarnatePlayerModifiers(diceAudit);
        if (actual == expected)
            return;

        issues.Add(new ValidationIssue(
            $"{context}.modifierBreakdown.player",
            IssueSeverity.Error,
            "Воплощение Света должно быть явно учтено в afterlife_spiritual_conflict_v1 diceAudit.",
            code: "afterlife_conflict_light_incarnate_modifier_mismatch",
            section: "AfterlifeSpiritualConflict",
            expected: $"modifier source/id/passiveId={SourceOfLightCapstoneState.PassiveId} sum {expected}",
            actual: actual.ToString(),
            repairHint: "Добавь в diceAudit.modifierBreakdown.player отдельный модификатор light_incarnate: +8 если игрок lead contestant, +4 если supporter/champion-side contributor, и ещё +4 для force_incarnation/force_binding/break_binding."));
    }

    private static int ResolveLightIncarnateExpectedDiceBonus(JsonObject payload)
    {
        var role = ResolveLightIncarnatePlayerRole(payload);
        var baseBonus = string.Equals(role, "supporter", StringComparison.OrdinalIgnoreCase)
            ? SourceOfLightCapstoneState.SupportDiceBonus
            : SourceOfLightCapstoneState.LeadDiceBonus;

        var operationType = SourceOfLightCapstoneState.GetNodeString(payload["operationType"]) ??
                            SourceOfLightCapstoneState.GetNodeString(payload["finalOperationType"]);
        if (SourceOfLightCapstoneState.IsCoerciveOperation(operationType))
            baseBonus += SourceOfLightCapstoneState.CoerciveOperationExtraBonus;

        return baseBonus;
    }

    private static string ResolveLightIncarnatePlayerRole(JsonObject payload)
    {
        foreach (var key in new[] { "playerCombatRole", "playerContestantRole", "playerRole" })
        {
            var role = SourceOfLightCapstoneState.GetNodeString(payload[key]);
            if (IsSupportRole(role))
                return "supporter";
            if (IsLeadRole(role))
                return "lead";
        }

        foreach (var candidate in EnumerateConflictSnapshots(payload))
        {
            if (PlayerSideHasPlayerLead(candidate))
                return "lead";
        }

        foreach (var candidate in EnumerateConflictSnapshots(payload))
        {
            if (PlayerSideHasPlayerSupporter(candidate))
                return "supporter";
        }

        return "lead";
    }

    private static IEnumerable<JsonObject> EnumerateConflictSnapshots(JsonObject payload)
    {
        yield return payload;

        foreach (var key in new[] { "after", "before", "activeConflictAfter", "conflictStateAfter", "conflictState", "resolution" })
        {
            if (payload[key] is JsonObject obj)
                yield return obj;
        }
    }

    private static bool PlayerSideHasPlayerLead(JsonObject root)
    {
        var lead = root["playerSide"]?["leadContestant"] as JsonObject;
        var actorType = SourceOfLightCapstoneState.GetNodeString(lead?["actorType"]);
        return IsPlayerActorType(actorType);
    }

    private static bool PlayerSideHasPlayerSupporter(JsonObject root)
    {
        if (root["playerSide"]?["supporters"] is not JsonArray supporters)
            return false;

        return supporters
            .OfType<JsonObject>()
            .Any(supporter => IsPlayerActorType(SourceOfLightCapstoneState.GetNodeString(supporter["actorType"])));
    }

    private static bool IsPlayerActorType(string? actorType) =>
        string.Equals(actorType, "player", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(actorType, "soul", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(actorType, "player_soul", StringComparison.OrdinalIgnoreCase);

    private static bool IsSupportRole(string? role) =>
        string.Equals(role, "supporter", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(role, "support", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(role, "champion_side_contributor", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(role, "champion_support", StringComparison.OrdinalIgnoreCase);

    private static bool IsLeadRole(string? role) =>
        string.Equals(role, "lead", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(role, "lead_contestant", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(role, "player_lead", StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<JsonObject> EnumerateSourceOfLightRelics(JsonObject soulRoot)
    {
        if (soulRoot["soulRelics"] is JsonObject soulRelics)
        {
            foreach (var collectionName in new[] { "equipped", "stored" })
            {
                if (soulRelics[collectionName] is not JsonArray collection)
                    continue;

                foreach (var relic in collection.OfType<JsonObject>())
                {
                    var id = SourceOfLightCapstoneState.GetNodeString(relic["relicId"]) ??
                             SourceOfLightCapstoneState.GetNodeString(relic["id"]);
                    if (string.Equals(id, SourceOfLightCapstoneState.RelicId, StringComparison.OrdinalIgnoreCase))
                        yield return relic;
                }
            }
        }
    }

    private static void AddSourceOfLightFieldIssue(
        List<ValidationIssue> issues,
        string path,
        string code,
        string expected,
        string actual)
    {
        issues.Add(new ValidationIssue(
            path,
            IssueSeverity.Error,
            "Source of Light capstone field не соответствует canonical contract.",
            code: code,
            section: "ShiningAbode",
            expected: expected,
            actual: actual));
    }

    private static void AddMissingSourceOfLightFileIssue(List<ValidationIssue> issues, string path, string code)
    {
        issues.Add(new ValidationIssue(
            path,
            IssueSeverity.Error,
            "Source of Light closure требует current authoritative state file.",
            code: code,
            section: "ShiningAbode",
            expected: "current state file present",
            actual: "missing/empty"));
    }
}
