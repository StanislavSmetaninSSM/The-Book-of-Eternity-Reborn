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
        var isResolvingValidatedRequest = await IsResolvingValidatedSourceOfLightRequestAsync(state.Request);
        if (!isResolvingValidatedRequest && HasAnySourceOfLightRewardSurface(soulRoot, shiningRoot))
        {
            issues.Add(new ValidationIssue(
                SourceOfLightCapstoneState.PendingRequestPath,
                IssueSeverity.Error,
                "Source of Light pending request нельзя держать после уже выданной или частично записанной Source reward.",
                code: "source_of_light_pending_duplicate_reward_state",
                section: "ShiningAbode",
                expected: "no existing sourceOfLightCapstone marker, light_incarnate passive, or source_of_light_incarnated_light relic before opening pending request",
                actual: DescribeSourceOfLightRewardSurfaceState(soulRoot, shiningRoot),
                repairHint: "Удалите stale pending_source_of_light_capstone.json или repair уже записанный Source of Light reward tuple; не запускайте второй capstone request."));
            return;
        }

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

        var pendingBlocker = await SourceOfLightCapstoneState.TryDescribeBlockingPendingContractAsync(_fs, shiningRoot);
        if (pendingBlocker != null)
        {
            issues.Add(new ValidationIssue(
                SourceOfLightCapstoneState.PendingRequestPath,
                IssueSeverity.Error,
                "Source of Light pending request нельзя держать рядом с другим active/malformed afterlife pending/control contract.",
                code: "source_of_light_pending_blocked_by_other_contract",
                section: "ShiningAbode",
                expected: "no other active/malformed afterlife pending/control contract",
                actual: pendingBlocker,
                repairHint: "Закрой или repair другой afterlife pending/control contract до Source of Light; не запускай GM с взаимоисключающими pending contracts."));
            return;
        }

        var radianceBaseline = await ResolvePendingSourceOfLightRadianceBaselineAsync(state.Request, shiningRoot);
        if (radianceBaseline is { } baseline &&
            (state.Request.RadianceExperienceAtRequest != baseline.Experience ||
             state.Request.RadianceTierAtRequest != baseline.Tier))
        {
            issues.Add(new ValidationIssue(
                SourceOfLightCapstoneState.PendingRequestPath,
                IssueSeverity.Error,
                "Source of Light pending request должен сохранять exact Radiance snapshot на момент создания.",
                code: "source_of_light_pending_radiance_snapshot_mismatch",
                section: "ShiningAbode",
                expected: $"{baseline.Label} radianceExperienceAtRequest={baseline.Experience}; radianceTierAtRequest={baseline.Tier}",
                actual: $"{state.Request.RadianceExperienceAtRequest}; {state.Request.RadianceTierAtRequest}",
                repairHint: "Не редактируй pending_source_of_light_capstone.json после создания клиентом."));
        }
    }

    private async Task<bool> IsResolvingValidatedSourceOfLightRequestAsync(SourceOfLightCapstoneState.SourceOfLightCapstoneRequest request)
    {
        var preTurnRequestJson = await ReadValidatedCurrentPreTurnTrackedFileAsync(SourceOfLightCapstoneState.PendingRequestPath);
        if (string.IsNullOrWhiteSpace(preTurnRequestJson))
            return false;

        var preTurnRequest = SourceOfLightCapstoneState.ReadRequestState(preTurnRequestJson, exists: true).Request;
        if (preTurnRequest == null ||
            !string.Equals(preTurnRequest.RequestId, request.RequestId, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            var preTurnSoulJson = await ReadValidatedCurrentPreTurnTrackedFileAsync("game_state/meta/soul_state.json");
            var preTurnShiningJson = await ReadValidatedCurrentPreTurnTrackedFileAsync(ShiningAbodeState.StatePath);
            if (string.IsNullOrWhiteSpace(preTurnSoulJson) || string.IsNullOrWhiteSpace(preTurnShiningJson))
                return false;

            var preTurnSoulRoot = JsonNode.Parse(preTurnSoulJson) as JsonObject;
            var preTurnShiningRoot = JsonNode.Parse(preTurnShiningJson) as JsonObject;
            return preTurnSoulRoot != null &&
                   preTurnShiningRoot != null &&
                   !HasAnySourceOfLightRewardSurface(preTurnSoulRoot, preTurnShiningRoot);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private async Task<(int Experience, int Tier, string Label)?> ResolvePendingSourceOfLightRadianceBaselineAsync(
        SourceOfLightCapstoneState.SourceOfLightCapstoneRequest request,
        JsonObject? currentShiningRoot)
    {
        var preTurnRequestJson = await ReadValidatedCurrentPreTurnTrackedFileAsync(SourceOfLightCapstoneState.PendingRequestPath);
        if (!string.IsNullOrWhiteSpace(preTurnRequestJson))
        {
            var preTurnRequest = SourceOfLightCapstoneState.ReadRequestState(preTurnRequestJson, exists: true).Request;
            if (preTurnRequest != null &&
                string.Equals(preTurnRequest.RequestId, request.RequestId, StringComparison.OrdinalIgnoreCase))
            {
                var preTurnShiningJson = await ReadValidatedCurrentPreTurnTrackedFileAsync(ShiningAbodeState.StatePath);
                if (string.IsNullOrWhiteSpace(preTurnShiningJson))
                    return null;

                try
                {
                    if (JsonNode.Parse(preTurnShiningJson) is not JsonObject preTurnShiningRoot)
                        return null;

                    ShiningAbodeState.NormalizeStateRoot(preTurnShiningRoot, residentRoot: null);
                    var preTurnRadiance = preTurnShiningRoot["radiance"] as JsonObject;
                    return (
                        SourceOfLightCapstoneState.GetNodeInt(preTurnRadiance?["experience"]),
                        SourceOfLightCapstoneState.GetNodeInt(preTurnRadiance?["tier"]),
                        "validated pre-turn");
                }
                catch (JsonException)
                {
                    return null;
                }
            }
        }

        var currentRadiance = currentShiningRoot?["radiance"] as JsonObject;
        return (
            SourceOfLightCapstoneState.GetNodeInt(currentRadiance?["experience"]),
            SourceOfLightCapstoneState.GetNodeInt(currentRadiance?["tier"]),
            "current");
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

            var rawCurrentMarkerMatches = SourceOfLightCapstoneState.HasCompletedCapstone(currentShiningRoot, request);
            if (!rawCurrentMarkerMatches)
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

            ShiningAbodeState.NormalizeStateRoot(currentShiningRoot, residentRoot: null);

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
            else if (CountIncarnatedLightStoredRelics(currentSoulRoot) != 1)
            {
                issues.Add(new ValidationIssue(
                    "game_state/meta/soul_state.json.soulRelics.stored",
                    IssueSeverity.Error,
                    "Accepted Source of Light closure должен материализовать Воплощенный Свет в soulRelics.stored[], не сразу в equipped[].",
                    code: "source_of_light_relic_not_stored_on_closure",
                    section: "ShiningAbode",
                    expected: "exactly one source_of_light_incarnated_light relic in soulRelics.stored[]",
                    actual: "matching relic is not in soulRelics.stored[]",
                    repairHint: "Добавь новую Source of Light Soul Relic в soulRelics.stored[]; экипировка должна происходить отдельным player/client action."));
            }

            if (rawCurrentMarkerMatches &&
                SourceOfLightCapstoneState.HasLightIncarnate(currentSoulRoot) &&
                relicCount == 1)
            {
                ValidateIncarnatedLightRelicPayload(currentSoulRoot, issues);
            }

            var allowShiningProgressionDeltas = await AllowsSourceOfLightShiningProgressionDeltasAsync();
            ValidateSourceOfLightClosureDiffs(
                preTurnSoulRoot,
                preTurnShiningRoot,
                currentSoulRoot,
                currentShiningRoot,
                request,
                allowShiningProgressionDeltas,
                issues);
        }
        catch (JsonException)
        {
            // JSON integrity and state-file validators report malformed roots.
        }
    }

    private async Task<bool> AllowsSourceOfLightShiningProgressionDeltasAsync()
    {
        var progressionControl = await ResolveValidatedCurrentProgressionControlAsync();
        var hasVerifiedProgressionReport = await HasVerifiedAfterlifeProgressionReportForCompositeAsync(progressionControl);
        return hasVerifiedProgressionReport &&
               progressionControl != null &&
               (progressionControl.ShiningAbodeCyclesExpectedThisTurn > 0 ||
                progressionControl.ShiningFactionCyclesExpectedThisTurn > 0 ||
                progressionControl.ShiningTradeCyclesExpectedThisTurn > 0 ||
                progressionControl.AfterlifeCatchupRequired);
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

        var lightIncarnateGrantTurn = SourceOfLightCapstoneState.GetLightIncarnateGrantTurn(soulRoot, shiningRoot);
        if (HasAnySourceOfLightRewardSurface(soulRoot, shiningRoot) &&
            await IsNewSourceOfLightRewardWithoutValidatedPendingRequestAsync(soulRoot, shiningRoot))
        {
            issues.Add(new ValidationIssue(
                SourceOfLightCapstoneState.PendingRequestPath,
                IssueSeverity.Error,
                "Source of Light reward tuple нельзя создавать без validated pre-turn pending_source_of_light_capstone.json.",
                code: "source_of_light_missing_validated_pending_request",
                section: "ShiningAbode",
                expected: "validated pre-turn pending_source_of_light_capstone.json matching the current Source of Light reward tuple",
                actual: "current accepted turn has new Source of Light reward surfaces but no validated pending request",
                repairHint: "Запускай Source of Light только через client-authored /source_of_light pending request; не добавляй marker/passive/relic из обычного GM response."));
        }

        if (HasAnySourceOfLightRewardSurface(soulRoot, shiningRoot) &&
            lightIncarnateGrantTurn is not > 0)
        {
            issues.Add(new ValidationIssue(
                $"{ShiningAbodeState.StatePath}.{SourceOfLightCapstoneState.ShiningStateProperty}",
                IssueSeverity.Error,
                "Source of Light reward tuple должен быть полностью согласован: marker, passive и relic обязаны ссылаться на один request/turn.",
                code: "source_of_light_closure_tuple_mismatch",
                section: "ShiningAbode",
                expected: "completed marker + light_incarnate passive with matching requestId/grantedAtTurn + exactly one source_of_light_incarnated_light relic with matching sourceRequestId",
                actual: DescribeSourceOfLightRewardSurfaceState(soulRoot, shiningRoot),
                repairHint: "Repair Source of Light tuple so sourceOfLightCapstone.requestId, lightIncarnate.requestId, completedAtTurn/grantedAtTurn, and relic.sourceRequestId all agree."));
        }
        else if (lightIncarnateGrantTurn is > 0 && relicCount == 1)
        {
            ValidateIncarnatedLightRelicPayload(soulRoot, issues);
        }
    }

    private async Task<bool> IsNewSourceOfLightRewardWithoutValidatedPendingRequestAsync(JsonObject? soulRoot, JsonObject? shiningRoot)
    {
        if (!_fs.FileExists("ready/turn_complete.json"))
            return false;

        if (await LoadValidatedCurrentPendingTurnSnapshotManifestAsync() == null)
            return false;

        var preTurnRequestJson = await ReadValidatedCurrentPreTurnTrackedFileAsync(SourceOfLightCapstoneState.PendingRequestPath);
        if (!string.IsNullOrWhiteSpace(preTurnRequestJson))
            return false;

        var preTurnSoulJson = await ReadValidatedCurrentPreTurnTrackedFileAsync("game_state/meta/soul_state.json");
        var preTurnShiningJson = await ReadValidatedCurrentPreTurnTrackedFileAsync(ShiningAbodeState.StatePath);
        if (string.IsNullOrWhiteSpace(preTurnSoulJson) || string.IsNullOrWhiteSpace(preTurnShiningJson))
            return false;

        try
        {
            var preTurnSoulRoot = JsonNode.Parse(preTurnSoulJson) as JsonObject;
            var preTurnShiningRoot = JsonNode.Parse(preTurnShiningJson) as JsonObject;
            return preTurnSoulRoot != null &&
                   preTurnShiningRoot != null &&
                   !HasAnySourceOfLightRewardSurface(preTurnSoulRoot, preTurnShiningRoot) &&
                   HasAnySourceOfLightRewardSurface(soulRoot, shiningRoot);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool HasAnySourceOfLightRewardSurface(JsonObject? soulRoot, JsonObject? shiningRoot) =>
        SourceOfLightCapstoneState.HasCompletedCapstone(shiningRoot) ||
        SourceOfLightCapstoneState.HasLightIncarnate(soulRoot) ||
        SourceOfLightCapstoneState.CountIncarnatedLightRelics(soulRoot) > 0;

    private static string DescribeSourceOfLightRewardSurfaceState(JsonObject? soulRoot, JsonObject? shiningRoot)
    {
        var marker = shiningRoot?[SourceOfLightCapstoneState.ShiningStateProperty] as JsonObject;
        JsonObject? passive = null;
        if (soulRoot?[AfterlifeSpiritualConflictState.SoulStateProfileProperty] is JsonObject profile &&
            profile[SourceOfLightCapstoneState.CapstonesProperty] is JsonObject capstones)
        {
            passive = capstones[SourceOfLightCapstoneState.LightIncarnateProperty] as JsonObject;
        }

        var relicCount = SourceOfLightCapstoneState.CountIncarnatedLightRelics(soulRoot);
        return "markerCompleted=" + SourceOfLightCapstoneState.HasCompletedCapstone(shiningRoot) +
               "; markerRequestId=" + (SourceOfLightCapstoneState.GetNodeString(marker?["requestId"]) ?? "missing") +
               "; markerCompletedAtTurn=" + SourceOfLightCapstoneState.GetNodeInt(marker?["completedAtTurn"]) +
               "; hasPassive=" + SourceOfLightCapstoneState.HasLightIncarnate(soulRoot) +
               "; passiveRequestId=" + (SourceOfLightCapstoneState.GetNodeString(passive?["requestId"]) ?? "missing") +
               "; passiveGrantedAtTurn=" + SourceOfLightCapstoneState.GetNodeInt(passive?["grantedAtTurn"]) +
               "; relicCount=" + relicCount +
               "; trustedGrantTurn=" + (SourceOfLightCapstoneState.GetLightIncarnateGrantTurn(soulRoot, shiningRoot)?.ToString() ?? "missing");
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
        ValidateIntegerField(marker, context, issues, "completedAtTurn");
        RequireString(marker, context, issues, "rewardPassiveId");
        RequireString(marker, context, issues, "rewardRelicId");
        ValidateIntegerField(marker, context, issues, "radianceExperienceAtRequest");
        ValidateIntegerField(marker, context, issues, "radianceTierAtRequest");

        if (TryReadInt(marker, "completedAtTurn", out var completedAtTurn) && completedAtTurn <= 0)
            AddSourceOfLightFieldIssue(issues, $"{context}.completedAtTurn", "source_of_light_marker_completed_turn_mismatch", "positive Source closure turn", completedAtTurn.ToString());

        var passiveId = GetFirstNonEmptyString(marker, "rewardPassiveId");
        if (!string.IsNullOrWhiteSpace(passiveId) && !string.Equals(passiveId, SourceOfLightCapstoneState.PassiveId, StringComparison.OrdinalIgnoreCase))
            AddSourceOfLightFieldIssue(issues, $"{context}.rewardPassiveId", "source_of_light_marker_passive_mismatch", SourceOfLightCapstoneState.PassiveId, passiveId);

        var relicId = GetFirstNonEmptyString(marker, "rewardRelicId");
        if (!string.IsNullOrWhiteSpace(relicId) && !string.Equals(relicId, SourceOfLightCapstoneState.RelicId, StringComparison.OrdinalIgnoreCase))
            AddSourceOfLightFieldIssue(issues, $"{context}.rewardRelicId", "source_of_light_marker_relic_mismatch", SourceOfLightCapstoneState.RelicId, relicId);
    }

    private static void ValidateSourceOfLightClosureDiffs(
        JsonObject preTurnSoulRoot,
        JsonObject preTurnShiningRoot,
        JsonObject currentSoulRoot,
        JsonObject currentShiningRoot,
        SourceOfLightCapstoneState.SourceOfLightCapstoneRequest request,
        bool allowShiningProgressionDeltas,
        List<ValidationIssue> issues)
    {
        var expectedShiningRoot = CloneJsonObject(preTurnShiningRoot);
        var expectedMarker = SourceOfLightCapstoneState.CreateCompletedShiningMarker(request);
        if (currentShiningRoot[SourceOfLightCapstoneState.ShiningStateProperty] is JsonObject currentMarker &&
            currentMarker["completedAtUtc"] != null)
        {
            expectedMarker["completedAtUtc"] = currentMarker["completedAtUtc"]!.DeepClone();
        }

        expectedShiningRoot[SourceOfLightCapstoneState.ShiningStateProperty] = expectedMarker;
        ShiningAbodeState.NormalizeStateRoot(expectedShiningRoot, residentRoot: null);

        if (allowShiningProgressionDeltas)
            ValidateSourceOfLightForbiddenShiningProgressionDeltas(expectedShiningRoot, currentShiningRoot, issues);

        if (!allowShiningProgressionDeltas && !JsonNode.DeepEquals(expectedShiningRoot, currentShiningRoot))
        {
            issues.Add(new ValidationIssue(
                ShiningAbodeState.StatePath,
                IssueSeverity.Error,
                "Source of Light closure содержит посторонние изменения shining_abode_state.json.",
                code: "source_of_light_unexpected_shining_state_diff",
                section: "ShiningAbode",
                expected: "validated pre-turn Shining state plus only sourceOfLightCapstone completed marker",
                actual: "current shining_abode_state.json differs from projected Source-only closure state",
                repairHint: "Откати unrelated Shining mutations; Source of Light closure может добавлять только completed marker."));
        }

        var expectedSoulRoot = CloneJsonObject(preTurnSoulRoot);
        ApplyExpectedSourceOfLightSoulDelta(expectedSoulRoot, currentSoulRoot, request);

        if (!JsonNode.DeepEquals(expectedSoulRoot, currentSoulRoot))
        {
            issues.Add(new ValidationIssue(
                "game_state/meta/soul_state.json",
                IssueSeverity.Error,
                "Source of Light closure содержит посторонние изменения soul_state.json.",
                code: "source_of_light_unexpected_soul_state_diff",
                section: "ShiningAbode",
                expected: "validated pre-turn Soul state plus only light_incarnate passive and one canonical Incarnated Light Soul Relic",
                actual: "current soul_state.json differs from projected Source-only closure state",
                repairHint: "Откати unrelated Soul mutations; Source of Light closure не должна менять currencies, existing relics, inventory, skills, or unrelated profile fields."));
        }
    }

    private static void ValidateSourceOfLightForbiddenShiningProgressionDeltas(
        JsonObject expectedShiningRoot,
        JsonObject currentShiningRoot,
        List<ValidationIssue> issues)
    {
        foreach (var path in SourceOfLightForbiddenShiningProgressionDeltaPaths)
        {
            var expectedNode = GetNestedNode(expectedShiningRoot, path);
            var currentNode = GetNestedNode(currentShiningRoot, path);
            if (JsonNode.DeepEquals(expectedNode, currentNode))
                continue;

            var pathText = string.Join(".", path);
            issues.Add(new ValidationIssue(
                $"{ShiningAbodeState.StatePath}.{pathText}",
                IssueSeverity.Error,
                "Source of Light closure с progression/catch-up содержит запрещённые Shining изменения вне progression contract.",
                code: "source_of_light_unexpected_shining_state_diff",
                section: "ShiningAbode",
                expected: $"validated pre-turn Shining {pathText}; Source closure is not a Shining action receipt/treasury/gates contract",
                actual: $"current shining_abode_state.json.{pathText} differs during Source closure",
                repairHint: "Оставь verified progression deltas только в scheduler-owned Shining fields; не добавляй core/founding/realignment/gacha/gates/treasury surfaces в Source of Light closure."));
        }
    }

    private static readonly string[][] SourceOfLightForbiddenShiningProgressionDeltaPaths =
    {
        new[] { "coreActionReceipts" },
        new[] { "factionFoundingReceipts" },
        new[] { "factionRealignmentReceipts" },
        new[] { "gates" },
        new[] { "gachaSystem", "gachaHistory" },
        new[] { "pendingNativeFactionDiscovery" },
        new[] { "preparedIncarnationPackage" },
        new[] { "lightSparks" },
        new[] { "treasury" }
    };

    private static JsonNode? GetNestedNode(JsonObject root, IReadOnlyList<string> path)
    {
        JsonNode? current = root;
        foreach (var segment in path)
        {
            if (current is not JsonObject obj)
                return null;

            current = obj[segment];
        }

        return current;
    }

    private static void ApplyExpectedSourceOfLightSoulDelta(
        JsonObject expectedSoulRoot,
        JsonObject currentSoulRoot,
        SourceOfLightCapstoneState.SourceOfLightCapstoneRequest request)
    {
        if (expectedSoulRoot[AfterlifeSpiritualConflictState.SoulStateProfileProperty] is not JsonObject profile)
        {
            profile = new JsonObject();
            expectedSoulRoot[AfterlifeSpiritualConflictState.SoulStateProfileProperty] = profile;
        }

        if (profile[SourceOfLightCapstoneState.CapstonesProperty] is not JsonObject capstones)
        {
            capstones = new JsonObject();
            profile[SourceOfLightCapstoneState.CapstonesProperty] = capstones;
        }

        capstones[SourceOfLightCapstoneState.LightIncarnateProperty] =
            SourceOfLightCapstoneState.CreateLightIncarnatePassive(request);

        var relic = SourceOfLightCapstoneState.CreateIncarnatedLightRelic(request);
        AppendExpectedIncarnatedLightRelic(expectedSoulRoot, relic);
    }

    private static void AppendExpectedIncarnatedLightRelic(
        JsonObject expectedSoulRoot,
        JsonObject relic)
    {
        if (expectedSoulRoot["soulRelics"] is JsonObject expectedRelics)
        {
            if (expectedRelics["stored"] is not JsonArray targetCollection)
            {
                targetCollection = new JsonArray();
                expectedRelics["stored"] = targetCollection;
            }

            targetCollection.Add(relic.DeepClone());
            return;
        }

        if (expectedSoulRoot["soulRelics"] is JsonArray expectedFlatRelics)
        {
            expectedFlatRelics.Add(relic.DeepClone());
            return;
        }

        expectedSoulRoot["soulRelics"] = new JsonObject
        {
            ["equipped"] = new JsonArray(),
            ["stored"] = new JsonArray(relic.DeepClone())
        };
    }

    private static int CountIncarnatedLightStoredRelics(JsonObject soulRoot)
    {
        if (soulRoot["soulRelics"] is not JsonObject soulRelics ||
            soulRelics["stored"] is not JsonArray storedRelics)
        {
            return 0;
        }

        return storedRelics.OfType<JsonObject>().Count(IsIncarnatedLightRelic);
    }

    private static bool IsIncarnatedLightRelic(JsonObject relic)
    {
        var id = SourceOfLightCapstoneState.GetNodeString(relic["relicId"]) ??
                 SourceOfLightCapstoneState.GetNodeString(relic["id"]);
        return string.Equals(id, SourceOfLightCapstoneState.RelicId, StringComparison.OrdinalIgnoreCase);
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
        var actual = SourceOfLightCapstoneState.SumLightIncarnatePlayerModifiers(diceAudit);
        var auditTurn = ResolveLightIncarnateAuditTurn(payload, diceAudit);
        if (!diceContext.HasLightIncarnate)
        {
            if (actual != 0)
            {
                AddUnauthorizedLightIncarnateModifierIssue(
                    context,
                    issues,
                    "no light_incarnate passive is present",
                    actual,
                    auditTurn,
                    diceContext.LightIncarnateGrantTurn);
            }

            return;
        }

        if (auditTurn is not > 0)
        {
            if (diceContext.IsPreTurnNoTurnDicePayload(payload))
                return;

            if (actual == 0 && !diceContext.HasAuthoritativeDice)
                return;

            issues.Add(new ValidationIssue(
                $"{context}.turnNumber",
                IssueSeverity.Error,
                "Воплощение Света требует явный turn marker в contested diceAudit после Source of Light unlock.",
                code: "afterlife_conflict_light_incarnate_modifier_mismatch",
                section: "AfterlifeSpiritualConflict",
                expected: $"exchangeAtTurn/resolvedAtTurn/turnNumber >= {diceContext.LightIncarnateGrantTurn!.Value} and modifier source/id/passiveId={SourceOfLightCapstoneState.PassiveId}",
                actual: $"auditTurn=missing; modifier sum={actual}",
                repairHint: "Добавь exchangeAtTurn/resolvedAtTurn/turnNumber к contested exchange/resolution audit и явный light_incarnate modifier, либо докажи, что audit predates grantedAtTurn."));
            return;
        }

        if (auditTurn.Value < diceContext.LightIncarnateGrantTurn!.Value)
        {
            if (actual != 0)
            {
                AddUnauthorizedLightIncarnateModifierIssue(
                    context,
                    issues,
                    "dice audit turn predates light_incarnate grant",
                    actual,
                    auditTurn,
                    diceContext.LightIncarnateGrantTurn);
            }

            return;
        }

        var expected = ResolveLightIncarnateExpectedDiceBonus(payload);
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

    private static void AddUnauthorizedLightIncarnateModifierIssue(
        string context,
        List<ValidationIssue> issues,
        string reason,
        int actual,
        int? auditTurn,
        int? grantTurn)
    {
        issues.Add(new ValidationIssue(
            $"{context}.modifierBreakdown.player",
            IssueSeverity.Error,
            "Воплощение Света нельзя учитывать в diceAudit до доказанного unlock Source of Light.",
            code: "afterlife_conflict_light_incarnate_modifier_unauthorized",
            section: "AfterlifeSpiritualConflict",
            expected: "no light_incarnate modifier before grant turn, or audit turn >= grantedAtTurn after unlock",
            actual: $"sum={actual}; auditTurn={(auditTurn?.ToString() ?? "missing")}; grantedAtTurn={(grantTurn?.ToString() ?? "missing")}; reason={reason}"));
    }

    private static int? ResolveLightIncarnateAuditTurn(JsonObject payload, JsonObject diceAudit)
    {
        foreach (var key in new[] { "resolvedAtTurn", "exchangeAtTurn", "turnNumber" })
        {
            var value = SourceOfLightCapstoneState.GetNodeInt(payload[key]);
            if (value > 0)
                return value;
        }

        foreach (var key in new[] { "turnNumber", "resolvedAtTurn", "exchangeAtTurn" })
        {
            var value = SourceOfLightCapstoneState.GetNodeInt(diceAudit[key]);
            if (value > 0)
                return value;
        }

        if (payload["resolution"] is JsonObject resolution)
        {
            var value = SourceOfLightCapstoneState.GetNodeInt(resolution["resolvedAtTurn"]);
            if (value > 0)
                return value;
        }

        return null;
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

        if (IsAssistedDuelConflict(payload))
            return "lead";

        if (IsChampionSideConflict(payload))
            return "supporter";

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

    private static bool IsChampionSideConflict(JsonObject payload)
    {
        foreach (var candidate in EnumerateConflictSnapshots(payload))
        {
            foreach (var key in new[] { "sideModel", "conflictModel", "conflictMode", "conflictType", "duelType", "operationType", "mode" })
            {
                if (IsChampionSideValue(SourceOfLightCapstoneState.GetNodeString(candidate[key])))
                    return true;
            }
        }

        return false;
    }

    private static bool IsAssistedDuelConflict(JsonObject payload)
    {
        foreach (var candidate in EnumerateConflictSnapshots(payload))
        {
            foreach (var key in new[] { "sideModel", "conflictModel", "conflictMode", "conflictType", "duelType", "operationType", "mode" })
            {
                if (IsAssistedDuelValue(SourceOfLightCapstoneState.GetNodeString(candidate[key])))
                    return true;
            }
        }

        return false;
    }

    private static bool IsChampionSideValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var normalized = value.Trim().Replace('-', '_').Replace(' ', '_');
        return normalized.Contains("champion", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAssistedDuelValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var normalized = value.Trim().Replace('-', '_').Replace(' ', '_');
        return normalized.Equals("assisted_duel", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("_assisted_duel", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("assisted_duel_", StringComparison.OrdinalIgnoreCase);
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
