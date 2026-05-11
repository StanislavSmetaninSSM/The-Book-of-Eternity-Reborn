using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    private void ValidateShiningAbodeStateFile(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        RequireString(root, contextPrefix, issues, "availability");
        ValidateIntegerField(root, contextPrefix, issues, "lightSparks");

        var availability = GetFirstNonEmptyString(root, "availability");
        if (!string.IsNullOrWhiteSpace(availability) && !ShiningAbodeState.IsSupportedAvailability(availability))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.availability",
                IssueSeverity.Error,
                "shining_abode_state.json использует неподдерживаемое availability",
                code: "shining_abode_invalid_availability",
                section: "ShiningAbode",
                expected: "active | sealed_until_next_ascension",
                actual: availability,
                repairHint: "Используй availability=active или sealed_until_next_ascension."));
        }

        if (TryReadInt(root, "lightSparks", out var lightSparks) && (lightSparks < 0 || lightSparks > 100))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.lightSparks",
                IssueSeverity.Error,
                "lightSparks должен быть в диапазоне 0..100",
                code: "shining_abode_light_sparks_out_of_bounds",
                section: "ShiningAbode",
                expected: "0..100",
                actual: lightSparks.ToString(),
                repairHint: "Сохраняй lightSparks как integer от 0 до 100."));
        }

        if (root.TryGetProperty("radiance", out var radiance) && RequireObject(radiance, $"{contextPrefix}.radiance", issues))
        {
            ValidateIntegerField(radiance, $"{contextPrefix}.radiance", issues, "experience");
            ValidateIntegerField(radiance, $"{contextPrefix}.radiance", issues, "tier");
            if (TryReadInt(radiance, "experience", out var experience) && TryReadInt(radiance, "tier", out var tier))
            {
                var expectedTier = ShiningAbodeState.ResolveRadianceTier(experience);
                if (tier != expectedTier)
                {
                    issues.Add(new ValidationIssue(
                        $"{contextPrefix}.radiance.tier",
                        IssueSeverity.Error,
                        "radiance.tier должен совпадать с tier, выведенным из radiance.experience",
                        code: "shining_abode_radiance_tier_mismatch",
                        section: "ShiningAbode",
                        expected: expectedTier.ToString(),
                        actual: tier.ToString(),
                        repairHint: "Пересчитывай radiance.tier из radiance.experience по canonical threshold table."));
                }
            }
        }
        else
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.radiance",
                IssueSeverity.Error,
                "shining_abode_state.json должен содержать radiance object",
                code: "shining_abode_missing_radiance",
                section: "ShiningAbode",
                expected: "radiance object",
                actual: "missing_or_invalid",
                repairHint: "Сохраняй radiance.experience и radiance.tier в canonical owner-state."));
        }

        ValidateArrayItems(root, $"{contextPrefix}.halls", issues, "halls", ValidateShiningHallObject);
        ValidateArrayItems(root, $"{contextPrefix}.factions", issues, "factions", ValidateShiningFactionObject);
        ValidateArrayItems(root, $"{contextPrefix}.shiningPoliticalActors", issues, "shiningPoliticalActors", ValidateShiningPoliticalActorObject);
        if (root.TryGetProperty("halls", out var halls))
            ValidateDuplicateStringIdsInArray(halls, $"{contextPrefix}.halls", issues, "hallId", "shining_abode_duplicate_hall_id");
        if (root.TryGetProperty("factions", out var factionIdentities))
        {
            ValidateDuplicateStringIdsInArray(factionIdentities, $"{contextPrefix}.factions", issues, "factionId", "shining_abode_duplicate_faction_id");
            ValidateDuplicateProjectIdsAcrossShiningFactions(factionIdentities, $"{contextPrefix}.factions", issues);
            ValidateShiningSupportedProjectCap(root, factionIdentities, contextPrefix, issues);
        }
        if (root.TryGetProperty("shiningPoliticalActors", out var politicalActors))
            ValidateDuplicateStringIdsInArray(politicalActors, $"{contextPrefix}.shiningPoliticalActors", issues, "actorId", "shining_abode_duplicate_political_actor_id");

        if (root.TryGetProperty("pendingNativeFactionDiscovery", out var pendingDiscovery) &&
            pendingDiscovery.ValueKind != JsonValueKind.Null)
        {
            ValidateShiningPendingNativeFactionDiscoveryObject(pendingDiscovery, $"{contextPrefix}.pendingNativeFactionDiscovery", issues);
        }

        if (root.TryGetProperty("gates", out var gates) && RequireObject(gates, $"{contextPrefix}.gates", issues))
        {
            ValidateIntegerField(gates, $"{contextPrefix}.gates", issues, "draftVersion");
            RequireBooleanField(gates, $"{contextPrefix}.gates", issues, "hasOpenDraft");
            RequireBooleanField(gates, $"{contextPrefix}.gates", issues, "isStale");
            ValidateIntegerField(gates, $"{contextPrefix}.gates", issues, "nextCandidateCursor");
            ValidateIntegerField(gates, $"{contextPrefix}.gates", issues, "rerollsRemaining");
            ValidateArrayItems(gates, $"{contextPrefix}.gates.allCandidateBlessingCards", issues, "allCandidateBlessingCards", ValidateShiningBlessingCardObject);
            ValidateArrayItems(gates, $"{contextPrefix}.gates.availableBlessingCards", issues, "availableBlessingCards", ValidateShiningBlessingCardObject);
            RequireArrayOfStrings(gates, $"{contextPrefix}.gates", issues, "shownBlessingCardIds");
            RequireArrayOfStrings(gates, $"{contextPrefix}.gates", issues, "selectedBlessingCardIds");
        }
        else
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.gates",
                IssueSeverity.Error,
                "shining_abode_state.json должен содержать gates object",
                code: "shining_abode_missing_gates",
                section: "ShiningAbode",
                expected: "gates object",
                actual: "missing_or_invalid",
                repairHint: "Сохраняй gates как canonical draft container."));
        }

        if (root.TryGetProperty("preparedIncarnationPackage", out var preparedIncarnationPackage) &&
            preparedIncarnationPackage.ValueKind != JsonValueKind.Null)
        {
            ValidateShiningPreparedIncarnationPackageObject(preparedIncarnationPackage, $"{contextPrefix}.preparedIncarnationPackage", issues);
        }

        if (root.TryGetProperty("gachaSystem", out var gachaSystem) && RequireObject(gachaSystem, $"{contextPrefix}.gachaSystem", issues))
        {
            ValidateShiningGachaSystemObject(gachaSystem, $"{contextPrefix}.gachaSystem", issues);
        }
        else
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.gachaSystem",
                IssueSeverity.Error,
                "shining_abode_state.json должен содержать gachaSystem object",
                code: "shining_abode_missing_gacha_system",
                section: "ShiningAbode",
                expected: "gachaSystem object",
                actual: "missing_or_invalid",
                repairHint: "Сохраняй в Shining owner-state gachaSystem с chargesPerReturn, chargesUsedThisReturn, currentReturnCycleId и gachaHistory[]."));
        }

        if (root.TryGetProperty(ShiningAbodeState.TreasuryProperty, out var treasury))
        {
            if (RequireObject(treasury, $"{contextPrefix}.{ShiningAbodeState.TreasuryProperty}", issues))
                ValidateShiningTreasuryObject(treasury, $"{contextPrefix}.{ShiningAbodeState.TreasuryProperty}", issues);
        }

        ValidateArrayItems(root, $"{contextPrefix}.coreActionReceipts", issues, "coreActionReceipts", ValidateShiningCoreActionReceiptObject);
        ValidateArrayItems(root, $"{contextPrefix}.factionFoundingReceipts", issues, "factionFoundingReceipts", ValidateShiningFoundingReceiptObject);
        ValidateArrayItems(root, $"{contextPrefix}.factionRealignmentReceipts", issues, "factionRealignmentReceipts", ValidateShiningRealignmentReceiptObject);

        if (root.TryGetProperty("coreActionReceipts", out var coreActionReceipts))
            ValidateDuplicateRequestIdsInArray(coreActionReceipts, $"{contextPrefix}.coreActionReceipts", issues, "shining_core_action_duplicate_receipt_request_id");
        if (root.TryGetProperty("factionFoundingReceipts", out var foundingReceipts))
            ValidateDuplicateRequestIdsInArray(foundingReceipts, $"{contextPrefix}.factionFoundingReceipts", issues, "shining_founding_duplicate_receipt_request_id");
        if (root.TryGetProperty("factionRealignmentReceipts", out var realignmentReceipts))
            ValidateDuplicateRequestIdsInArray(realignmentReceipts, $"{contextPrefix}.factionRealignmentReceipts", issues, "shining_realignment_duplicate_receipt_request_id");

        if (root.TryGetProperty("factions", out var factions) && factions.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var faction in factions.EnumerateArray())
            {
                var factionContext = $"{contextPrefix}.factions[{index++}]";
                if (faction.ValueKind != JsonValueKind.Object)
                    continue;

                if (faction.TryGetProperty("tradeInventoryReceipts", out var tradeReceipts))
                    ValidateDuplicateRequestIdsInArray(tradeReceipts, $"{factionContext}.tradeInventoryReceipts", issues, "shining_trade_duplicate_receipt_request_id");
                if (faction.TryGetProperty("leadershipReceipts", out var leadershipReceipts))
                    ValidateDuplicateRequestIdsInArray(leadershipReceipts, $"{factionContext}.leadershipReceipts", issues, "shining_leadership_duplicate_receipt_request_id");
                if (faction.TryGetProperty("leadershipHistory", out var leadershipHistory))
                    ValidateDuplicateRequestIdsInArray(leadershipHistory, $"{factionContext}.leadershipHistory", issues, "shining_leadership_duplicate_history_request_id");
            }
        }
    }

    private async Task ValidateShiningTreasuryClientOwnedStateAsync(List<ValidationIssue> issues)
    {
        var preTurnJson = await ReadValidatedCurrentPreTurnTrackedFileAsync(ShiningAbodeState.StatePath);
        if (string.IsNullOrWhiteSpace(preTurnJson))
            return;

        JsonObject? preTurnRoot;
        try
        {
            preTurnRoot = JsonNode.Parse(preTurnJson) as JsonObject;
        }
        catch
        {
            return;
        }

        if (preTurnRoot?[ShiningAbodeState.TreasuryProperty] is not JsonObject preTurnTreasury)
            return;

        var currentJson = await _fs.ReadFileAsync(ShiningAbodeState.StatePath);
        if (string.IsNullOrWhiteSpace(currentJson))
        {
            AddShiningTreasuryClientOwnedIssue(issues, "missing_shining_state");
            return;
        }

        JsonObject? currentRoot;
        try
        {
            currentRoot = JsonNode.Parse(currentJson) as JsonObject;
        }
        catch
        {
            return;
        }

        if (currentRoot?[ShiningAbodeState.TreasuryProperty] is not JsonObject currentTreasury)
        {
            AddShiningTreasuryClientOwnedIssue(issues, "missing_or_invalid_treasury");
            return;
        }

        if (!JsonNode.DeepEquals(preTurnTreasury, currentTreasury))
            AddShiningTreasuryClientOwnedIssue(issues, "treasury_changed");
    }

    private static void AddShiningTreasuryClientOwnedIssue(List<ValidationIssue> issues, string actual)
    {
        issues.Add(new ValidationIssue(
            $"{ShiningAbodeState.StatePath}.{ShiningAbodeState.TreasuryProperty}",
            IssueSeverity.Error,
            "Shining treasury является client-owned state и должен сохраняться неизменным в GM accepted turn.",
            code: "shining_treasury_client_owned_modified",
            section: "ShiningAbode",
            expected: "current treasury object byte-equivalent to validated pre-turn treasury object",
            actual: actual,
            repairHint: "Не удаляй, не пересоздавай и не редактируй shining_abode_state.json.treasury в GM output; перенеси pre-turn treasury object без изменений."));
    }

    private void ValidateDuplicateRequestIdsInArray(JsonElement array, string contextPrefix, List<ValidationIssue> issues, string code)
    {
        if (array.ValueKind != JsonValueKind.Array)
            return;

        var duplicateRequestIds = array.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.Object)
            .Select(item => GetFirstNonEmptyString(item, "requestId"))
            .Where(requestId => !string.IsNullOrWhiteSpace(requestId))
            .GroupBy(requestId => requestId!, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (duplicateRequestIds.Count == 0)
            return;

        issues.Add(new ValidationIssue(
            contextPrefix,
            IssueSeverity.Error,
            $"Shining receipts/history contain duplicated requestId: {string.Join(", ", duplicateRequestIds)}",
            code: code,
            section: "ShiningAbode",
            repairHint: "Оставляй в canonical Shining receipts/history уникальный requestId для каждого resolved contract, чтобы strict validation не зависела от порядка массива."));
    }

    private void ValidateDuplicateStringIdsInArray(
        JsonElement array,
        string contextPrefix,
        List<ValidationIssue> issues,
        string idProperty,
        string code)
    {
        if (array.ValueKind != JsonValueKind.Array)
            return;

        var duplicateIds = array.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.Object)
            .Select(item => GetFirstNonEmptyString(item, idProperty))
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .GroupBy(id => id!, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (duplicateIds.Count == 0)
            return;

        issues.Add(new ValidationIssue(
            contextPrefix,
            IssueSeverity.Error,
            $"Shining state содержит duplicated {idProperty}: {string.Join(", ", duplicateIds)}",
            code: code,
            section: "ShiningAbode",
            expected: $"unique non-empty {idProperty}",
            actual: string.Join(", ", duplicateIds),
            repairHint: $"Сохраняй уникальные {idProperty} в canonical Shining owner-state; runtime helpers выбирают первый match и не могут безопасно обработать duplicates."));
    }

    private void ValidateDuplicateProjectIdsAcrossShiningFactions(
        JsonElement factions,
        string contextPrefix,
        List<ValidationIssue> issues)
    {
        if (factions.ValueKind != JsonValueKind.Array)
            return;

        var projectIds = new List<string>();
        foreach (var faction in factions.EnumerateArray())
        {
            if (faction.ValueKind != JsonValueKind.Object ||
                !faction.TryGetProperty("projects", out var projects) ||
                projects.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            projectIds.AddRange(projects.EnumerateArray()
                .Where(project => project.ValueKind == JsonValueKind.Object)
                .Select(project => GetFirstNonEmptyString(project, "projectId"))
                .Where(projectId => !string.IsNullOrWhiteSpace(projectId))
                .Cast<string>());
        }

        var duplicates = projectIds
            .GroupBy(projectId => projectId, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .OrderBy(projectId => projectId, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (duplicates.Count == 0)
            return;

        issues.Add(new ValidationIssue(
            contextPrefix,
            IssueSeverity.Error,
            $"Shining state содержит duplicated projects[].projectId: {string.Join(", ", duplicates)}",
            code: "shining_abode_duplicate_project_id",
            section: "ShiningAbode",
            expected: "unique projectId across all Shining factions",
            actual: string.Join(", ", duplicates),
            repairHint: "Каждый Shining projectId должен быть уникальным across all factions; не переиспользуй id даже в другой faction.projects[]."));
    }

    private void ValidateShiningSupportedProjectCap(
        JsonElement root,
        JsonElement factions,
        string contextPrefix,
        List<ValidationIssue> issues)
    {
        if (factions.ValueKind != JsonValueKind.Array)
            return;

        var radianceTier = 0;
        if (root.TryGetProperty("radiance", out var radiance))
            TryReadInt(radiance, "tier", out radianceTier);

        var supportedProjects = 0;
        foreach (var faction in factions.EnumerateArray())
        {
            if (faction.ValueKind != JsonValueKind.Object ||
                !faction.TryGetProperty("projects", out var projects) ||
                projects.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            supportedProjects += projects.EnumerateArray().Count(project =>
                project.ValueKind == JsonValueKind.Object &&
                string.Equals(GetFirstNonEmptyString(project, "status"), ShiningAbodeState.ProjectStatusCompleted, StringComparison.OrdinalIgnoreCase) &&
                GetBoolean(project, "isSupported", defaultValue: false));
        }

        var cap = ShiningAbodeState.GetSupportedProjectCap(radianceTier);
        if (supportedProjects <= cap)
            return;

        issues.Add(new ValidationIssue(
            $"{contextPrefix}.factions",
            IssueSeverity.Error,
            "Количество supported completed Shining projects превышает Radiance cap",
            code: "shining_abode_supported_project_cap_exceeded",
            section: "ShiningAbode",
            expected: $"<= {cap} supported completed projects at radiance tier {radianceTier}",
            actual: supportedProjects.ToString(),
            repairHint: "Оставь supported=true только у allowed project cap; лишние completed projects должны быть unsupported через canonical unsupport_project closure."));
    }

    private void ValidatePendingShiningCoreActionsRequestFile(JsonElement root, string contextPrefix, List<ValidationIssue> issues) =>
        ValidateArrayItems(root, $"{contextPrefix}.{ShiningCoreActionRequestState.RequestsProperty}", issues, ShiningCoreActionRequestState.RequestsProperty, ValidatePendingShiningCoreActionRequestObject);

    private void ValidatePendingShiningTradeInventoryRequestsFile(JsonElement root, string contextPrefix, List<ValidationIssue> issues) =>
        ValidateArrayItems(root, $"{contextPrefix}.{ShiningTradeRequestState.RequestsProperty}", issues, ShiningTradeRequestState.RequestsProperty, ValidatePendingShiningTradeInventoryRequestObject);

    private void ValidatePendingShiningFactionFoundingsRequestFile(JsonElement root, string contextPrefix, List<ValidationIssue> issues) =>
        ValidateArrayItems(root, $"{contextPrefix}.{ShiningFactionRequestState.RequestsProperty}", issues, ShiningFactionRequestState.RequestsProperty, ValidatePendingShiningFactionFoundingRequestObject);

    private void ValidatePendingShiningFactionRealignmentsRequestFile(JsonElement root, string contextPrefix, List<ValidationIssue> issues) =>
        ValidateArrayItems(root, $"{contextPrefix}.{ShiningFactionRequestState.RequestsProperty}", issues, ShiningFactionRequestState.RequestsProperty, ValidatePendingShiningFactionRealignmentRequestObject);

    private void ValidatePendingShiningFactionLeadershipTransitionsRequestFile(JsonElement root, string contextPrefix, List<ValidationIssue> issues) =>
        ValidateArrayItems(root, $"{contextPrefix}.{ShiningFactionRequestState.RequestsProperty}", issues, ShiningFactionRequestState.RequestsProperty, ValidatePendingShiningFactionLeadershipTransitionRequestObject);

    private async Task ValidatePendingShiningFoundingRequestContextAsync(List<ValidationIssue> issues)
    {
        if (await ShiningFactionRequestState.IsRequestFileMalformedAsync(
                _fs,
                ShiningFactionRequestState.PendingFoundingsRequestPath,
                static json => JsonSerializer.Deserialize<ShiningFactionRequestState.PendingShiningFactionFoundingRequest>(json)))
        {
            issues.Add(new ValidationIssue(
                ShiningFactionRequestState.PendingFoundingsRequestPath,
                IssueSeverity.Error,
                "pending_shining_faction_foundings.json unreadable или malformed.",
                code: "shining_founding_malformed_live_request",
                section: "ShiningAbode",
                repairHint: "Исправь pending_shining_faction_foundings.json до machine-readable requests[] shape или очисти файл явно."));
            return;
        }

        var requests = await ShiningFactionRequestState.ReadFoundingRequestsAsync(_fs);
        if (requests.Count == 0)
            return;

        if (await ShouldSkipLiveShiningPendingEligibilityContextAsync(ShiningFactionRequestState.PendingFoundingsRequestPath))
            return;

        await ValidateShiningPoliticalRequestModeAsync(
            issues,
            ShiningFactionRequestState.PendingFoundingsRequestPath,
            "shining_founding_wrong_realm_or_mode");

        var duplicateRequestIds = requests
            .Where(request => !string.IsNullOrWhiteSpace(request.RequestId))
            .GroupBy(request => request.RequestId, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (duplicateRequestIds.Count > 0)
        {
            issues.Add(new ValidationIssue(
                ShiningFactionRequestState.PendingFoundingsRequestPath,
                IssueSeverity.Error,
                $"pending_shining_faction_foundings.json содержит duplicated requestId: {string.Join(", ", duplicateRequestIds)}",
                code: "shining_founding_duplicate_request_id",
                section: "ShiningAbode",
                repairHint: "Оставляй в pending_shining_faction_foundings.json уникальный requestId для каждого founding request и не переиспользуй его для другой фракции или другого зала."));
            return;
        }

        for (var i = 0; i < requests.Count; i++)
        {
            var error = await ShiningFactionRequestState.ValidateFoundingRequestAgainstCurrentStateAsync(_fs, requests[i]);
            if (string.IsNullOrWhiteSpace(error))
                continue;

            issues.Add(new ValidationIssue(
                $"{ShiningFactionRequestState.PendingFoundingsRequestPath}.requests[{i}]",
                IssueSeverity.Error,
                error,
                code: "shining_founding_invalid_context",
                section: "ShiningAbode",
                repairHint: "Исправь founding request так, чтобы он соответствовал canonical Shining founding eligibility и current state."));
        }
    }

    private async Task ValidatePendingShiningCoreActionRequestContextAsync(List<ValidationIssue> issues)
    {
        var requestState = await ShiningCoreActionRequestState.ReadRequestsStateAsync(_fs);
        if (requestState.IsMalformed)
        {
            issues.Add(new ValidationIssue(
                ShiningCoreActionRequestState.PendingActionsRequestPath,
                IssueSeverity.Error,
                "pending_shining_abode_actions.json unreadable или malformed.",
                code: "shining_core_action_malformed_live_request",
                section: "ShiningAbode",
                repairHint: "Исправь pending_shining_abode_actions.json до machine-readable requests[] shape или очисти файл явно."));
            return;
        }

        var requests = requestState.Requests;
        if (requests.Count == 0)
            return;

        if (await ShouldSkipLiveShiningPendingEligibilityContextAsync(ShiningCoreActionRequestState.PendingActionsRequestPath))
            return;

        await ValidateShiningPoliticalRequestModeAsync(
            issues,
            ShiningCoreActionRequestState.PendingActionsRequestPath,
            "shining_core_action_wrong_realm_or_mode",
            "Pending Shining core action допустим только в ordinary active Shining Abode state");

        if (requests.Count > 1)
        {
            issues.Add(new ValidationIssue(
                ShiningCoreActionRequestState.PendingActionsRequestPath,
                IssueSeverity.Error,
                "pending_shining_abode_actions.json пока поддерживает только один pending request за раз",
                code: "shining_core_action_multiple_pending_requests",
                section: "ShiningAbode",
                repairHint: "Оставляй в pending_shining_abode_actions.json не больше одного активного core action request."));
            return;
        }

        var error = await ShiningCoreActionRequestState.ValidateRequestAgainstCurrentStateAsync(_fs, requests[0]);
        if (string.IsNullOrWhiteSpace(error))
            return;

        issues.Add(new ValidationIssue(
            $"{ShiningCoreActionRequestState.PendingActionsRequestPath}.requests[0]",
            IssueSeverity.Error,
            error,
            code: "shining_core_action_invalid_context",
            section: "ShiningAbode",
            repairHint: "Исправь pending Shining core action request так, чтобы он соответствовал canonical active Shining state, quoted costs и draft prerequisites."));
    }

    private async Task ValidatePendingShiningTradeInventoryRequestContextAsync(List<ValidationIssue> issues)
    {
        var rawJson = await _fs.ReadFileAsync(ShiningTradeRequestState.PendingRequestsPath);
        var requests = ShiningTradeRequestState.ReadRequests(rawJson);
        if (_fs.FileExists(ShiningTradeRequestState.PendingRequestsPath) &&
            requests.Count == 0 &&
            !HasExplicitEmptyShiningRequestsArrayLocal(rawJson))
        {
            issues.Add(new ValidationIssue(
                ShiningTradeRequestState.PendingRequestsPath,
                IssueSeverity.Error,
                "pending_shining_trade_inventory_requests.json unreadable или malformed.",
                code: "shining_trade_request_malformed_live_request",
                section: "ShiningAbode",
                repairHint: "Исправь pending_shining_trade_inventory_requests.json до machine-readable requests[] shape или очисти файл явно."));
            return;
        }

        if (requests.Count == 0)
            return;

        if (await ShouldSkipLiveShiningPendingEligibilityContextAsync(ShiningTradeRequestState.PendingRequestsPath))
            return;

        await ValidateShiningPoliticalRequestModeAsync(
            issues,
            ShiningTradeRequestState.PendingRequestsPath,
            "shining_trade_request_wrong_realm_or_mode",
            "Pending Shining trade request допустим только в ordinary active Shining Abode state");

        var duplicateRequestIds = requests
            .Where(request => !string.IsNullOrWhiteSpace(request.RequestId))
            .GroupBy(request => request.RequestId, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (duplicateRequestIds.Count > 0)
        {
            issues.Add(new ValidationIssue(
                ShiningTradeRequestState.PendingRequestsPath,
                IssueSeverity.Error,
                $"pending_shining_trade_inventory_requests.json содержит duplicated requestId: {string.Join(", ", duplicateRequestIds)}",
                code: "shining_trade_duplicate_request_id",
                section: "ShiningAbode",
                repairHint: "Оставляй в pending_shining_trade_inventory_requests.json уникальный requestId для каждого Shining trade contract, чтобы resolved notification и receipt identity не сливались."));
            return;
        }

        var duplicateFactionCycleContracts = requests
            .GroupBy(
                request => $"{request.FactionId}::{request.TradeCycleId}",
                StringComparer.OrdinalIgnoreCase)
            .Where(group =>
                !string.IsNullOrWhiteSpace(group.First().FactionId) &&
                !string.IsNullOrWhiteSpace(group.First().TradeCycleId) &&
                group.Count() > 1)
            .Select(group => $"{group.First().FactionId}/{group.First().TradeCycleId}")
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (duplicateFactionCycleContracts.Count > 0)
        {
            issues.Add(new ValidationIssue(
                ShiningTradeRequestState.PendingRequestsPath,
                IssueSeverity.Error,
                $"pending_shining_trade_inventory_requests.json содержит duplicate same-cycle contracts: {string.Join(", ", duplicateFactionCycleContracts)}",
                code: "shining_trade_duplicate_same_cycle_faction_requests",
                section: "ShiningAbode",
                repairHint: "Оставляй не больше одного pending trade request на factionId + tradeCycleId и не допускай order-dependent runtime выбора."));
        }

        for (var i = 0; i < requests.Count; i++)
        {
            var error = await ShiningTradeRequestState.ValidateRequestAgainstCurrentStateAsync(_fs, requests[i]);
            if (string.IsNullOrWhiteSpace(error))
                continue;

            issues.Add(new ValidationIssue(
                $"{ShiningTradeRequestState.PendingRequestsPath}.requests[{i}]",
                IssueSeverity.Error,
                error,
                code: "shining_trade_request_invalid_context",
                section: "ShiningAbode",
                repairHint: "Исправь pending Shining trade request так, чтобы он соответствовал canonical active Shining state и derived trade profile фракции."));
        }
    }

    private async Task ValidatePendingShiningRealignmentRequestContextAsync(List<ValidationIssue> issues)
    {
        if (await ShiningFactionRequestState.IsRequestFileMalformedAsync(
                _fs,
                ShiningFactionRequestState.PendingRealignmentsRequestPath,
                static json => JsonSerializer.Deserialize<ShiningFactionRequestState.PendingShiningFactionRealignmentRequest>(json)))
        {
            issues.Add(new ValidationIssue(
                ShiningFactionRequestState.PendingRealignmentsRequestPath,
                IssueSeverity.Error,
                "pending_shining_faction_realignments.json unreadable или malformed.",
                code: "shining_realignment_malformed_live_request",
                section: "ShiningAbode",
                repairHint: "Исправь pending_shining_faction_realignments.json до machine-readable requests[] shape или очисти файл явно."));
            return;
        }

        var requests = await ShiningFactionRequestState.ReadRealignmentRequestsAsync(_fs);
        if (requests.Count == 0)
            return;

        if (await ShouldSkipLiveShiningPendingEligibilityContextAsync(ShiningFactionRequestState.PendingRealignmentsRequestPath))
            return;

        await ValidateShiningPoliticalRequestModeAsync(
            issues,
            ShiningFactionRequestState.PendingRealignmentsRequestPath,
            "shining_realignment_wrong_realm_or_mode");

        var duplicateRequestIds = requests
            .Where(request => !string.IsNullOrWhiteSpace(request.RequestId))
            .GroupBy(request => request.RequestId, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (duplicateRequestIds.Count > 0)
        {
            issues.Add(new ValidationIssue(
                ShiningFactionRequestState.PendingRealignmentsRequestPath,
                IssueSeverity.Error,
                $"pending_shining_faction_realignments.json содержит duplicated requestId: {string.Join(", ", duplicateRequestIds)}",
                code: "shining_realignment_duplicate_request_id",
                section: "ShiningAbode",
                repairHint: "Оставляй в pending_shining_faction_realignments.json уникальный requestId для каждого realignment request и не переиспользуй его для другого резидента или другого transition contract."));
            return;
        }

        for (var i = 0; i < requests.Count; i++)
        {
            var error = await ShiningFactionRequestState.ValidateRealignmentRequestAgainstCurrentStateAsync(_fs, requests[i]);
            if (string.IsNullOrWhiteSpace(error))
                continue;

            issues.Add(new ValidationIssue(
                $"{ShiningFactionRequestState.PendingRealignmentsRequestPath}.requests[{i}]",
                IssueSeverity.Error,
                error,
                code: "shining_realignment_invalid_context",
                section: "ShiningAbode",
                repairHint: "Исправь Shining realignment request так, чтобы он соответствовал canonical resident and faction state."));
        }
    }

    private async Task ValidatePendingShiningLeadershipTransitionRequestContextAsync(List<ValidationIssue> issues)
    {
        if (await ShiningFactionRequestState.IsRequestFileMalformedAsync(
                _fs,
                ShiningFactionRequestState.PendingLeadershipTransitionsRequestPath,
                static json => JsonSerializer.Deserialize<ShiningFactionRequestState.PendingShiningFactionLeadershipTransitionRequest>(json)))
        {
            issues.Add(new ValidationIssue(
                ShiningFactionRequestState.PendingLeadershipTransitionsRequestPath,
                IssueSeverity.Error,
                "pending_shining_faction_leadership_transitions.json unreadable или malformed.",
                code: "shining_leadership_malformed_live_request",
                section: "ShiningAbode",
                repairHint: "Исправь pending_shining_faction_leadership_transitions.json до machine-readable requests[] shape или очисти файл явно."));
            return;
        }

        var requests = await ShiningFactionRequestState.ReadLeadershipTransitionRequestsAsync(_fs);
        if (requests.Count == 0)
            return;

        if (await ShouldSkipLiveShiningPendingEligibilityContextAsync(ShiningFactionRequestState.PendingLeadershipTransitionsRequestPath))
            return;

        await ValidateShiningPoliticalRequestModeAsync(
            issues,
            ShiningFactionRequestState.PendingLeadershipTransitionsRequestPath,
            "shining_leadership_wrong_realm_or_mode");

        var duplicateRequestIds = requests
            .Where(request => !string.IsNullOrWhiteSpace(request.RequestId))
            .GroupBy(request => request.RequestId, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (duplicateRequestIds.Count > 0)
        {
            issues.Add(new ValidationIssue(
                ShiningFactionRequestState.PendingLeadershipTransitionsRequestPath,
                IssueSeverity.Error,
                $"pending_shining_faction_leadership_transitions.json содержит duplicated requestId: {string.Join(", ", duplicateRequestIds)}",
                code: "shining_leadership_duplicate_request_id",
                section: "ShiningAbode",
                repairHint: "Оставляй в pending_shining_faction_leadership_transitions.json уникальный requestId для каждого leadership transition, чтобы resolved notification и receipt identity не сливались."));
            return;
        }

        for (var i = 0; i < requests.Count; i++)
        {
            var error = await ShiningFactionRequestState.ValidateLeadershipTransitionRequestAgainstCurrentStateAsync(_fs, requests[i]);
            if (string.IsNullOrWhiteSpace(error))
                continue;

            issues.Add(new ValidationIssue(
                $"{ShiningFactionRequestState.PendingLeadershipTransitionsRequestPath}.requests[{i}]",
                IssueSeverity.Error,
                error,
                code: "shining_leadership_invalid_context",
                section: "ShiningAbode",
                repairHint: "Исправь leadership request так, чтобы он соответствовал canonical faction leadership и supporter rules."));
        }
    }

    private async Task<bool> ShouldSkipLiveShiningPendingEligibilityContextAsync(string requestPath)
    {
        if (!_fs.FileExists("ready/turn_complete.json") &&
            !_fs.FileExists("ready/turn_error.json"))
        {
            return false;
        }

        var lookup = await LoadValidatedPendingTurnSnapshotLookupAsync();
        if (lookup.Status != ValidatedPendingTurnSnapshotStatus.Usable || lookup.Manifest == null)
            return false;

        var snapshotJson = await ReadValidatedPendingTurnSnapshotFileAsync(lookup.Manifest, requestPath);
        return !string.IsNullOrWhiteSpace(snapshotJson);
    }

    private async Task ValidateShiningPoliticalRequestModeAsync(List<ValidationIssue> issues, string requestPath, string code, string? messageOverride = null)
    {
        var soulRoot = await ReadJsonObjectAsync("game_state/meta/soul_state.json");
        var shiningRoot = await ReadJsonObjectAsync(ShiningAbodeState.StatePath);
        var currentRealm = GetShiningNodeString(soulRoot?["currentRealm"]);
        var availability = GetShiningNodeString(shiningRoot?["availability"]);
        var hasPendingPackage = shiningRoot?["preparedIncarnationPackage"] is JsonObject;
        if (!IsSupportedShiningRealm(currentRealm) ||
            !string.Equals(availability, ShiningAbodeState.AvailabilityActive, StringComparison.OrdinalIgnoreCase) ||
            hasPendingPackage)
        {
            issues.Add(new ValidationIssue(
                requestPath,
                IssueSeverity.Error,
                messageOverride ?? "Pending Shining political request допустим только в ordinary active Shining Abode state",
                code: code,
                section: "ShiningAbode",
                repairHint: "Создавай pending Shining political request только при currentRealm, указывающем на Сияющую Обитель, availability=active и preparedIncarnationPackage=null."));
        }
    }

    private static bool IsSupportedShiningRealm(string? currentRealm) =>
        string.Equals(currentRealm, "Shining Abode", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(currentRealm, "Сияющая Обитель", StringComparison.OrdinalIgnoreCase);

    private static bool HasExplicitEmptyShiningRequestsArrayLocal(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind == JsonValueKind.Object &&
                   document.RootElement.TryGetProperty("requests", out var requestsNode) &&
                   requestsNode.ValueKind == JsonValueKind.Array &&
                   requestsNode.GetArrayLength() == 0;
        }
        catch
        {
            return false;
        }
    }

    private void ValidateShiningHallObject(JsonElement hall, string contextPrefix, List<ValidationIssue> issues)
    {
        if (!RequireObject(hall, contextPrefix, issues))
            return;
        RequireString(hall, contextPrefix, issues, "hallId");
        RequireString(hall, contextPrefix, issues, "hallName");
        RequireString(hall, contextPrefix, issues, "description");
        RequireArrayOfStrings(hall, contextPrefix, issues, "serviceTags");
    }

    private void ValidateShiningFactionObject(JsonElement faction, string contextPrefix, List<ValidationIssue> issues)
    {
        if (!RequireObject(faction, contextPrefix, issues))
            return;
        RequireString(faction, contextPrefix, issues, "factionId");
        var originType = RequireString(faction, contextPrefix, issues, "originType");
        RequireString(faction, contextPrefix, issues, "hallId");
        ValidateIntegerField(faction, contextPrefix, issues, "baseStrength");
        ValidateIntegerField(faction, contextPrefix, issues, "factionStrength");
        ValidateIntegerField(faction, contextPrefix, issues, "investCountThisAscension");
        if (!string.IsNullOrWhiteSpace(originType) && !ShiningAbodeState.IsSupportedOriginType(originType))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.originType",
                IssueSeverity.Error,
                "Faction originType должен быть canonical enum значением",
                code: "shining_abode_invalid_origin_type",
                section: "ShiningAbode",
                repairHint: "Используй для faction.originType только ascended_guardian, native_radiant или player_founded."));
        }
        if (faction.TryGetProperty("charter", out var charter))
            ValidateShiningFactionCharterObject(charter, $"{contextPrefix}.charter", issues);
        else
            issues.Add(new ValidationIssue($"{contextPrefix}.charter", IssueSeverity.Error, "Faction должен содержать nested charter object", code: "shining_abode_missing_faction_charter", section: "ShiningAbode"));
        if (faction.TryGetProperty("leadership", out var leadership))
            ValidateShiningFactionLeadershipObject(leadership, $"{contextPrefix}.leadership", issues);
        else
            issues.Add(new ValidationIssue($"{contextPrefix}.leadership", IssueSeverity.Error, "Faction должен содержать nested leadership object", code: "shining_abode_missing_faction_leadership", section: "ShiningAbode"));
        ValidateArrayItems(faction, $"{contextPrefix}.projects", issues, "projects", ValidateShiningProjectObject);
        if (faction.TryGetProperty("tradeInventory", out var tradeInventory) && tradeInventory.ValueKind != JsonValueKind.Null)
            ValidateShiningTradeInventoryObject(tradeInventory, $"{contextPrefix}.tradeInventory", issues);
        if (faction.TryGetProperty("tradeInventoryReceipts", out _))
            ValidateArrayItems(faction, $"{contextPrefix}.tradeInventoryReceipts", issues, "tradeInventoryReceipts", ValidateShiningTradeInventoryReceiptObject);
        ValidateArrayItems(faction, $"{contextPrefix}.leadershipReceipts", issues, "leadershipReceipts", ValidateShiningLeadershipReceiptObject);
        ValidateArrayItems(faction, $"{contextPrefix}.leadershipHistory", issues, "leadershipHistory", ValidateShiningLeadershipHistoryObject);
    }

    private void ValidateShiningFactionCharterObject(JsonElement charter, string contextPrefix, List<ValidationIssue> issues)
    {
        if (!RequireObject(charter, contextPrefix, issues))
            return;
        RequireString(charter, contextPrefix, issues, "factionName");
        var favoredArchetype = RequireString(charter, contextPrefix, issues, "favoredArchetype");
        var patronEffectFamily = RequireString(charter, contextPrefix, issues, "patronEffectFamily");
        RequireString(charter, contextPrefix, issues, "summary");
        if (!string.IsNullOrWhiteSpace(favoredArchetype) && !ShiningAbodeState.IsSupportedProjectArchetype(favoredArchetype))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.favoredArchetype",
                IssueSeverity.Error,
                "charter.favoredArchetype должен быть canonical project archetype",
                code: "shining_abode_invalid_favored_archetype",
                section: "ShiningAbode",
                repairHint: "Используй только поддерживаемые archetype значения для favoredArchetype."));
        }

        if (!string.IsNullOrWhiteSpace(patronEffectFamily) && !ShiningAbodeState.IsSupportedEffectFamily(patronEffectFamily))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.patronEffectFamily",
                IssueSeverity.Error,
                "charter.patronEffectFamily должен быть canonical effect family",
                code: "shining_abode_invalid_patron_effect_family",
                section: "ShiningAbode",
                repairHint: "Используй только lore, social, resource, memory, descent, survival, relic или route."));
        }
    }

    private void ValidateShiningFactionLeadershipObject(JsonElement leadership, string contextPrefix, List<ValidationIssue> issues)
    {
        if (!RequireObject(leadership, contextPrefix, issues))
            return;
        RequireString(leadership, contextPrefix, issues, "leadershipState");
        ValidateOptionalNullableStringField(leadership, contextPrefix, issues, "headActorType");
        ValidateOptionalNullableStringField(leadership, contextPrefix, issues, "headActorId");

        var leadershipState = GetFirstNonEmptyString(leadership, "leadershipState");
        var headActorType = GetFirstNonEmptyString(leadership, "headActorType");
        var headActorId = GetFirstNonEmptyString(leadership, "headActorId");
        var isVacant = string.Equals(leadershipState, ShiningAbodeState.LeadershipStateVacant, StringComparison.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(leadershipState) &&
            !ShiningAbodeState.IsSupportedLeadershipState(leadershipState))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.leadershipState",
                IssueSeverity.Error,
                "leadership.leadershipState использует неподдерживаемое canonical значение",
                code: "shining_leadership_invalid_state",
                section: "ShiningAbode",
                repairHint: "Используй только secure, contested или vacant для faction.leadership.leadershipState."));
            return;
        }

        if (!isVacant)
        {
            if (string.IsNullOrWhiteSpace(headActorType) || string.IsNullOrWhiteSpace(headActorId))
            {
                issues.Add(new ValidationIssue(
                    contextPrefix,
                    IssueSeverity.Error,
                    "non-vacant leadership обязан содержать canonical headActorType и headActorId",
                    code: "shining_leadership_missing_head_binding",
                    section: "ShiningAbode",
                    expected: "non-empty headActorType + headActorId for non-vacant leadership",
                    actual: $"{headActorType ?? "missing"}:{headActorId ?? "missing"} / {leadershipState ?? "missing"}",
                    repairHint: "Для secure/contested leadership указывай canonical главу фракции через headActorType/headActorId. Только vacant leadership может хранить пустой head binding."));
                return;
            }

            if (!ShiningAbodeState.IsSupportedHeadActorType(headActorType))
            {
                issues.Add(new ValidationIssue(
                    $"{contextPrefix}.headActorType",
                    IssueSeverity.Error,
                    "leadership.headActorType использует неподдерживаемое значение",
                    code: "shining_leadership_invalid_actor_type",
                    section: "ShiningAbode",
                    repairHint: "Используй guardian, player_soul, resident или radiant_actor для non-vacant leadership state."));
                return;
            }

            if (string.Equals(headActorType, ShiningAbodeState.HeadActorTypePlayerSoul, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(headActorId, ShiningAbodeState.HeadActorTypePlayerSoul, StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new ValidationIssue(
                    $"{contextPrefix}.headActorId",
                    IssueSeverity.Error,
                    "player_soul leadership должен использовать canonical headActorId=player_soul",
                    code: "shining_leadership_invalid_player_soul_binding",
                    section: "ShiningAbode",
                    expected: ShiningAbodeState.HeadActorTypePlayerSoul,
                    actual: string.IsNullOrWhiteSpace(headActorId) ? "missing" : headActorId,
                    repairHint: "Если главой фракции является душа игрока, сохраняй exact pair player_soul / player_soul."));
            }
        }
    }

    private void ValidateShiningPoliticalActorObject(JsonElement actor, string contextPrefix, List<ValidationIssue> issues)
    {
        if (!RequireObject(actor, contextPrefix, issues))
            return;
        RequireString(actor, contextPrefix, issues, "actorId");
        var actorType = RequireString(actor, contextPrefix, issues, "actorType");
        RequireString(actor, contextPrefix, issues, "displayName");
        RequireString(actor, contextPrefix, issues, "originFactionId");
        ValidateOptionalNullableStringField(actor, contextPrefix, issues, "currentFactionId");
        var politicalStatus = RequireString(actor, contextPrefix, issues, "politicalStatus");

        if (!string.IsNullOrWhiteSpace(actorType) &&
            !string.Equals(actorType, ShiningAbodeState.HeadActorTypeRadiantActor, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.actorType",
                IssueSeverity.Error,
                "shiningPoliticalActors[].actorType должен использовать canonical radiant_actor",
                code: "shining_political_actor_invalid_type",
                section: "ShiningAbode",
                repairHint: "Используй actorType = radiant_actor для materialized shiningPoliticalActors[]."));
        }

        if (!string.IsNullOrWhiteSpace(politicalStatus) &&
            !ShiningAbodeState.IsSupportedPoliticalStatus(politicalStatus))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.politicalStatus",
                IssueSeverity.Error,
                "shiningPoliticalActors[].politicalStatus использует неподдерживаемое canonical значение",
                code: "shining_political_actor_invalid_status",
                section: "ShiningAbode",
                repairHint: "Используй only head, former_head, claimant, elder или retired в shiningPoliticalActors[].politicalStatus."));
        }
    }

    private void ValidateShiningPendingNativeFactionDiscoveryObject(JsonElement pendingDiscovery, string contextPrefix, List<ValidationIssue> issues)
    {
        if (!RequireObject(pendingDiscovery, contextPrefix, issues))
            return;

        RequireString(pendingDiscovery, contextPrefix, issues, "requestId");
        ValidateIntegerField(pendingDiscovery, contextPrefix, issues, "createdAtTurn");
        ValidateOptionalString(pendingDiscovery, contextPrefix, issues, "createdAtUtc");
        ValidateIntegerField(pendingDiscovery, contextPrefix, issues, "radianceTierAtRequest");
        ValidateIntegerField(pendingDiscovery, contextPrefix, issues, "costFeathers");
        ValidateIntegerField(pendingDiscovery, contextPrefix, issues, "costLightSparks");
    }

    private void ValidateShiningProjectObject(JsonElement project, string contextPrefix, List<ValidationIssue> issues)
    {
        if (!RequireObject(project, contextPrefix, issues))
            return;
        RequireString(project, contextPrefix, issues, "projectId");
        RequireString(project, contextPrefix, issues, "displayName");
        RequireString(project, contextPrefix, issues, "summary");
        RequireArrayOfStrings(project, contextPrefix, issues, "toneTags");
        RequireArrayOfStrings(project, contextPrefix, issues, "targetFactionIds");
        var projectArchetype = RequireString(project, contextPrefix, issues, "projectArchetype");
        var outputEffectFamily = RequireString(project, contextPrefix, issues, "outputEffectFamily");
        ValidateIntegerField(project, contextPrefix, issues, "tier");
        var status = RequireString(project, contextPrefix, issues, "status");
        RequireBooleanField(project, contextPrefix, issues, "isSupported");
        ValidateIntegerField(project, contextPrefix, issues, "strengthReward");
        if (!string.IsNullOrWhiteSpace(projectArchetype) && !ShiningAbodeState.IsSupportedProjectArchetype(projectArchetype))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.projectArchetype",
                IssueSeverity.Error,
                "projectArchetype должен быть canonical project archetype",
                code: "shining_abode_invalid_project_archetype",
                section: "ShiningAbode",
                repairHint: "Используй только поддерживаемые archetype значения в projects[].projectArchetype."));
        }

        if (!string.IsNullOrWhiteSpace(outputEffectFamily) && !ShiningAbodeState.IsSupportedEffectFamily(outputEffectFamily))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.outputEffectFamily",
                IssueSeverity.Error,
                "outputEffectFamily должен быть canonical effect family",
                code: "shining_abode_invalid_output_effect_family",
                section: "ShiningAbode",
                repairHint: "Используй только поддерживаемые effect family значения в projects[].outputEffectFamily."));
        }

        if (!string.IsNullOrWhiteSpace(status) && !ShiningAbodeState.IsSupportedProjectStatus(status))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.status",
                IssueSeverity.Error,
                "project status должен быть canonical project status",
                code: "shining_abode_invalid_project_status",
                section: "ShiningAbode",
                repairHint: "Используй только active, completed или retired в projects[].status."));
        }
    }

    private void ValidateShiningBlessingCardObject(JsonElement card, string contextPrefix, List<ValidationIssue> issues)
    {
        if (!RequireObject(card, contextPrefix, issues))
            return;
        RequireString(card, contextPrefix, issues, "cardId");
        RequireString(card, contextPrefix, issues, "dedupeKey");
        var sourceType = RequireString(card, contextPrefix, issues, "sourceType");
        RequireString(card, contextPrefix, issues, "sourceFactionId");
        ValidateOptionalString(card, contextPrefix, issues, "sourceFactionName");
        ValidateOptionalString(card, contextPrefix, issues, "sourceActorId");
        ValidateOptionalString(card, contextPrefix, issues, "sourceActorName");
        var effectFamily = RequireString(card, contextPrefix, issues, "effectFamily");
        var rarity = RequireString(card, contextPrefix, issues, "rarity");
        RequireString(card, contextPrefix, issues, "displayName");
        RequireString(card, contextPrefix, issues, "displaySummary");
        if (!string.IsNullOrWhiteSpace(sourceType) && !ShiningAbodeState.IsSupportedCardSourceType(sourceType))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.sourceType",
                IssueSeverity.Error,
                "Blessing card sourceType должен быть canonical source token",
                code: "shining_abode_invalid_blessing_card_source_type",
                section: "ShiningAbode",
                expected: "head | project | resident_descent",
                actual: sourceType,
                repairHint: "Используй только поддерживаемые sourceType значения для gates/preparedIncarnationPackage blessing cards."));
        }

        if (!string.IsNullOrWhiteSpace(effectFamily) && !ShiningAbodeState.IsSupportedEffectFamily(effectFamily))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.effectFamily",
                IssueSeverity.Error,
                "Blessing card effectFamily должен быть canonical effect family",
                code: "shining_abode_invalid_blessing_card_effect_family",
                section: "ShiningAbode",
                expected: "lore | social | resource | memory | descent | survival | relic | route",
                actual: effectFamily,
                repairHint: "Используй только поддерживаемые effectFamily значения для gates/preparedIncarnationPackage blessing cards."));
        }

        if (!string.IsNullOrWhiteSpace(rarity) && !ShiningAbodeState.IsSupportedRarity(rarity))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.rarity",
                IssueSeverity.Error,
                "Blessing card rarity должен быть canonical Shining rarity",
                code: "shining_abode_invalid_blessing_card_rarity",
                section: "ShiningAbode",
                expected: "common | uncommon | rare | epic | legendary | radiant",
                actual: rarity,
                repairHint: "Используй только поддерживаемые rarity значения для gates/preparedIncarnationPackage blessing cards."));
        }

        if (!card.TryGetProperty("effectPayload", out var effectPayload) || !RequireObject(effectPayload, $"{contextPrefix}.effectPayload", issues))
        {
            issues.Add(new ValidationIssue($"{contextPrefix}.effectPayload", IssueSeverity.Error, "Blessing card должен содержать effectPayload object", code: "shining_abode_missing_card_payload", section: "ShiningAbode"));
        }
    }

    private void ValidateShiningPreparedIncarnationPackageObject(JsonElement package, string contextPrefix, List<ValidationIssue> issues)
    {
        if (!RequireObject(package, contextPrefix, issues))
            return;
        RequireArrayOfStrings(package, contextPrefix, issues, "selectedCardIds");
        ValidateArrayItems(package, $"{contextPrefix}.selectedCards", issues, "selectedCards", ValidateShiningBlessingCardObject);
        ValidateIntegerField(package, contextPrefix, issues, "generatedFromDraftVersion");
        ValidateIntegerField(package, contextPrefix, issues, "preparedAtTurn");
        ValidateOptionalString(package, contextPrefix, issues, "preparedAtUtc");

        if (JsonNode.Parse(package.GetRawText()) is JsonObject packageRoot)
        {
            var bootstrapValidationError = ShiningAbodeState.ValidatePreparedIncarnationPackageForBootstrap(packageRoot);
            if (!string.IsNullOrWhiteSpace(bootstrapValidationError))
            {
                issues.Add(new ValidationIssue(
                    contextPrefix,
                    IssueSeverity.Error,
                    "preparedIncarnationPackage не проходит runtime bootstrap validation.",
                    code: "shining_abode_prepare_package_bootstrap_invalid",
                    section: "ShiningAbode",
                    expected: "non-empty unique selectedCardIds/selectedCards bootstrap package",
                    actual: bootstrapValidationError,
                    repairHint: "Храни preparedIncarnationPackage в той же форме, которую runtime может consume for Shining pending-bootstrap handoff."));
            }
        }

        if (!package.TryGetProperty("selectedCardIds", out var selectedCardIds) ||
            selectedCardIds.ValueKind != JsonValueKind.Array ||
            !package.TryGetProperty("selectedCards", out var selectedCards) ||
            selectedCards.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var storedIds = selectedCardIds.EnumerateArray()
            .Where(node => node.ValueKind == JsonValueKind.String)
            .Select(node => (node.GetString() ?? string.Empty).Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToList();
        var cardIds = selectedCards.EnumerateArray()
            .Where(node => node.ValueKind == JsonValueKind.Object)
            .Select(node => GetFirstNonEmptyString(node, "cardId")?.Trim() ?? string.Empty)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToList();

        if (storedIds.Count == 0 || cardIds.Count == 0)
            return;

        if (storedIds.Count != cardIds.Count ||
            !storedIds.SequenceEqual(cardIds, StringComparer.OrdinalIgnoreCase))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.selectedCardIds",
                IssueSeverity.Error,
                "preparedIncarnationPackage.selectedCardIds должен точно совпадать с selectedCards[].cardId в том же порядке",
                code: "shining_abode_prepare_package_selected_card_sequence_mismatch",
                section: "ShiningAbode",
                expected: string.Join(", ", cardIds),
                actual: string.Join(", ", storedIds),
                repairHint: "Храни selectedCardIds как ordered snapshot тех же карт, что и selectedCards[]."));
        }
    }

    private async Task ValidateShiningLeadershipHeadReferencesAsync(List<ValidationIssue> issues)
    {
        var shiningRoot = await ReadJsonObjectAsync(ShiningAbodeState.StatePath);
        if (shiningRoot == null)
            return;

        var residentRoot = await ReadJsonObjectAsync(GuardianAbodeResidentState.StatePath);
        var guardiansRoot = await ReadJsonObjectAsync("game_state/meta/guardians.json");
        ShiningAbodeState.NormalizeStateRoot(shiningRoot, residentRoot, guardiansRoot);

        var politicalActors = new Dictionary<string, JsonObject>(StringComparer.OrdinalIgnoreCase);
        if (shiningRoot["shiningPoliticalActors"] is JsonArray politicalActorArray)
        {
            foreach (var actor in politicalActorArray.OfType<JsonObject>())
            {
                var actorId = GetNodeString(actor["actorId"]);
                if (!string.IsNullOrWhiteSpace(actorId) && !politicalActors.ContainsKey(actorId))
                    politicalActors[actorId] = actor;
            }
        }

        var exclusiveLeadershipHeads = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var factions = ShiningAbodeState.EnsureFactionsArray(shiningRoot);
        for (var i = 0; i < factions.Count; i++)
        {
            if (factions[i] is not JsonObject faction || faction["leadership"] is not JsonObject leadership)
                continue;

            var factionId = GetNodeString(faction["factionId"]) ?? $"factions[{i}]";
            var leadershipState = GetNodeString(leadership["leadershipState"]);
            if (string.Equals(leadershipState, ShiningAbodeState.LeadershipStateVacant, StringComparison.OrdinalIgnoreCase))
                continue;

            var headActorType = GetNodeString(leadership["headActorType"]);
            var headActorId = GetNodeString(leadership["headActorId"]);
            if (string.IsNullOrWhiteSpace(headActorType) || string.IsNullOrWhiteSpace(headActorId))
                continue;

            var isResolvable = true;
            if (string.Equals(headActorType, ShiningAbodeState.HeadActorTypePlayerSoul, StringComparison.OrdinalIgnoreCase))
            {
                isResolvable = string.Equals(headActorId, ShiningAbodeState.HeadActorTypePlayerSoul, StringComparison.OrdinalIgnoreCase);
            }
            else if (string.Equals(headActorType, ShiningAbodeState.HeadActorTypeGuardian, StringComparison.OrdinalIgnoreCase))
            {
                isResolvable = LeadershipGuardianExists(guardiansRoot, headActorId);
            }
            else if (string.Equals(headActorType, ShiningAbodeState.HeadActorTypeResident, StringComparison.OrdinalIgnoreCase))
            {
                var resident = residentRoot == null
                    ? null
                    : GuardianAbodeResidentState.FindResident(residentRoot, headActorId);
                if (resident == null)
                {
                    isResolvable = false;
                }
                else
                {
                    var ascensionState = GetNodeString(resident["ascensionState"]);
                    if (!string.Equals(ascensionState, ShiningAbodeState.AscensionStateAscended, StringComparison.OrdinalIgnoreCase))
                    {
                        issues.Add(new ValidationIssue(
                            $"{ShiningAbodeState.StatePath}.factions[{i}].leadership.headActorId",
                            IssueSeverity.Error,
                            "resident-глава Shining faction должен быть ascended resident.",
                            code: "shining_leadership_resident_head_not_ascended",
                            section: "ShiningAbode",
                            expected: ShiningAbodeState.AscensionStateAscended,
                            actual: string.IsNullOrWhiteSpace(ascensionState) ? "missing" : ascensionState,
                            repairHint: "Назначай главой фракции только ascended resident или сначала переведи resident в Сияющую Обитель canonically."));
                    }

                    var residentFactionId = GetNodeString(resident["shiningFactionId"]);
                    if (!string.Equals(residentFactionId, factionId, StringComparison.OrdinalIgnoreCase))
                    {
                        issues.Add(new ValidationIssue(
                            $"{ShiningAbodeState.StatePath}.factions[{i}].leadership.headActorId",
                            IssueSeverity.Error,
                            "resident-глава должен принадлежать той же Shining faction, которой руководит.",
                            code: "shining_leadership_resident_head_faction_mismatch",
                            section: "ShiningAbode",
                            expected: factionId,
                            actual: string.IsNullOrWhiteSpace(residentFactionId) ? "missing" : residentFactionId,
                            repairHint: "Синхронизируй resident.shiningFactionId с factionId руководимой фракции или выбери другого главу."));
                    }
                }
            }
            else if (string.Equals(headActorType, ShiningAbodeState.HeadActorTypeRadiantActor, StringComparison.OrdinalIgnoreCase))
            {
                if (!politicalActors.TryGetValue(headActorId, out var actor))
                {
                    isResolvable = false;
                }
                else
                {
                    var actorFactionId = GetNodeString(actor["currentFactionId"]);
                    if (!string.Equals(actorFactionId, factionId, StringComparison.OrdinalIgnoreCase))
                    {
                        issues.Add(new ValidationIssue(
                            $"{ShiningAbodeState.StatePath}.factions[{i}].leadership.headActorId",
                            IssueSeverity.Error,
                            "radiant_actor-глава должен быть привязан к той же currentFactionId, которой руководит.",
                            code: "shining_leadership_radiant_head_faction_mismatch",
                            section: "ShiningAbode",
                            expected: factionId,
                            actual: string.IsNullOrWhiteSpace(actorFactionId) ? "missing" : actorFactionId,
                            repairHint: "Синхронизируй shiningPoliticalActors[].currentFactionId с руководимой factionId или выбери другого radiant actor."));
                    }

                    var politicalStatus = GetNodeString(actor["politicalStatus"]);
                    if (!string.Equals(politicalStatus, ShiningAbodeState.PoliticalStatusHead, StringComparison.OrdinalIgnoreCase))
                    {
                        issues.Add(new ValidationIssue(
                            $"{ShiningAbodeState.StatePath}.shiningPoliticalActors[].politicalStatus",
                            IssueSeverity.Error,
                            "radiant_actor-глава Shining faction должен иметь politicalStatus=head в actor registry.",
                            code: "shining_leadership_radiant_head_status_mismatch",
                            section: "ShiningAbode",
                            expected: ShiningAbodeState.PoliticalStatusHead,
                            actual: string.IsNullOrWhiteSpace(politicalStatus) ? "missing" : politicalStatus,
                            repairHint: "Синхронизируй shiningPoliticalActors[].politicalStatus=head для текущего radiant_actor главы или выбери другого главу."));
                    }
                }
            }

            if (!isResolvable)
            {
                issues.Add(new ValidationIssue(
                    $"{ShiningAbodeState.StatePath}.factions[{i}].leadership.headActorId",
                    IssueSeverity.Error,
                    "non-player leadership должен ссылаться на существующего guardian, resident или shiningPoliticalActor",
                    code: "shining_leadership_missing_head_actor_reference",
                    section: "ShiningAbode",
                    expected: "existing actor id for the declared headActorType",
                    actual: $"{headActorType}:{headActorId}",
                    repairHint: "Используй существующий guardian/resident/radiant actor или очисти broken leadership binding перед сохранением state."));
                continue;
            }

            var headKey = $"{headActorType.Trim()}:{headActorId.Trim()}";
            if (exclusiveLeadershipHeads.TryGetValue(headKey, out var existingFactionId) &&
                !string.Equals(existingFactionId, factionId, StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new ValidationIssue(
                    $"{ShiningAbodeState.StatePath}.factions[{i}].leadership.headActorId",
                    IssueSeverity.Error,
                    "Один и тот же actor не может быть текущим главой нескольких Shining factions без отдельного supported transition.",
                    code: "shining_leadership_duplicate_head_actor",
                    section: "ShiningAbode",
                    expected: $"single current faction head for {headKey}",
                    actual: $"{existingFactionId}, {factionId}",
                    repairHint: "Оставь actor главой только одной текущей фракции; для второй фракции используй vacant/contested leadership, другого главу или явный leadership transition."));
            }
            else
            {
                exclusiveLeadershipHeads[headKey] = factionId;
            }
        }
    }

    private static bool LeadershipGuardianExists(JsonObject? guardiansRoot, string guardianId)
    {
        if (guardiansRoot == null || string.IsNullOrWhiteSpace(guardianId))
            return false;

        if (guardiansRoot["activeGuardian"] is JsonObject activeGuardian &&
            string.Equals(GetNodeString(activeGuardian["guardianId"]), guardianId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return guardiansRoot["guardians"] is JsonArray guardians &&
               guardians.OfType<JsonObject>()
                   .Any(guardian => string.Equals(GetNodeString(guardian["guardianId"]), guardianId, StringComparison.OrdinalIgnoreCase));
    }

    private void ValidateShiningGachaSystemObject(JsonElement gachaSystem, string contextPrefix, List<ValidationIssue> issues)
    {
        if (!RequireObject(gachaSystem, contextPrefix, issues))
            return;

        ValidateNonNegativeIntegerField(gachaSystem, contextPrefix, issues, "chargesPerReturn", "ShiningAbode");
        ValidateNonNegativeIntegerField(gachaSystem, contextPrefix, issues, "chargesUsedThisReturn", "ShiningAbode");
        RequireString(gachaSystem, contextPrefix, issues, "currentReturnCycleId");
        ValidateArrayItems(gachaSystem, $"{contextPrefix}.gachaHistory", issues, "gachaHistory", ValidateShiningGachaHistoryEntryObject);

        if (TryReadInt(gachaSystem, "chargesPerReturn", out var chargesPerReturn) &&
            TryReadInt(gachaSystem, "chargesUsedThisReturn", out var chargesUsed) &&
            chargesUsed > chargesPerReturn)
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.chargesUsedThisReturn",
                IssueSeverity.Error,
                "chargesUsedThisReturn не может превышать chargesPerReturn",
                code: "shining_gacha_used_charges_exceed_limit",
                section: "ShiningAbode",
                expected: $"<= {chargesPerReturn}",
                actual: chargesUsed.ToString(),
                repairHint: "Синхронизируй used charges с canonical chargesPerReturn текущего return-cycle."));
        }

        var returnCycleId = GetFirstNonEmptyString(gachaSystem, "currentReturnCycleId");
        if (string.IsNullOrWhiteSpace(returnCycleId) &&
            TryReadInt(gachaSystem, "chargesUsedThisReturn", out var chargesUsedWithoutCycle) &&
            chargesUsedWithoutCycle > 0)
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.chargesUsedThisReturn",
                IssueSeverity.Error,
                "chargesUsedThisReturn не может быть положительным без currentReturnCycleId",
                code: "shining_gacha_used_charges_without_cycle",
                section: "ShiningAbode",
                expected: "chargesUsedThisReturn = 0 when currentReturnCycleId is empty",
                actual: chargesUsedWithoutCycle.ToString(),
                repairHint: "Если currentReturnCycleId пустой legacy/state bootstrap marker, сбрось chargesUsedThisReturn в 0 до первого resolved Shining gacha pull."));
        }
    }

    private void ValidateShiningTreasuryObject(JsonElement treasury, string contextPrefix, List<ValidationIssue> issues)
    {
        ValidateIntegerField(treasury, contextPrefix, issues, "depositedInkFeathers");
        ValidateIntegerField(treasury, contextPrefix, issues, "claimableInkFeatherInterest");
        ValidateIntegerField(treasury, contextPrefix, issues, "totalInterestClaimed");
        ValidateIntegerField(treasury, contextPrefix, issues, "exchangeThisCycleLightSparks");
        RequireTreasuryStringFieldAllowEmpty(treasury, contextPrefix, issues, "lastInterestSettlementCycleId");
        RequireTreasuryStringFieldAllowEmpty(treasury, contextPrefix, issues, "exchangeCycleId");
        ValidateArrayItems(treasury, $"{contextPrefix}.exchangeHistory", issues, "exchangeHistory", ValidateShiningTreasuryExchangeHistoryEntryObject);

        foreach (var fieldName in new[] { "depositedInkFeathers", "claimableInkFeatherInterest", "totalInterestClaimed", "exchangeThisCycleLightSparks" })
        {
            if (TryReadInt(treasury, fieldName, out var value) && value < 0)
            {
                issues.Add(new ValidationIssue(
                    $"{contextPrefix}.{fieldName}",
                    IssueSeverity.Error,
                    "Shining Treasury integer fields must be non-negative",
                    code: "shining_treasury_negative_integer",
                    section: "ShiningAbode",
                    expected: ">= 0",
                    actual: value.ToString(),
                    repairHint: "Казначейство Сияющей Обители хранит только неотрицательные integer counters."));
            }
        }

        if (TryReadInt(treasury, "exchangeThisCycleLightSparks", out var exchangedThisCycle) &&
            exchangedThisCycle > ShiningAbodeState.TreasuryMaxLightSparksExchangePerCycle)
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.exchangeThisCycleLightSparks",
                IssueSeverity.Error,
                "Shining Treasury exchange cap exceeded",
                code: "shining_treasury_exchange_cycle_cap_exceeded",
                section: "ShiningAbode",
                expected: $"<= {ShiningAbodeState.TreasuryMaxLightSparksExchangePerCycle}",
                actual: exchangedThisCycle.ToString(),
                repairHint: "Ограничь локальный обмен казначейства лимитом текущего Shining return cycle."));
        }

        foreach (var forbiddenField in new[] { "depositedLightSparks", "claimableLightSparkInterest", "lightSparkDepositHistory" })
        {
            if (!treasury.TryGetProperty(forbiddenField, out _))
                continue;

            issues.Add(new ValidationIssue(
                $"{contextPrefix}.{forbiddenField}",
                IssueSeverity.Error,
                "Light Sparks cannot be deposited in Shining Treasury",
                code: "shining_treasury_light_spark_deposit_forbidden",
                section: "ShiningAbode",
                expected: "Only Ink Feather deposits; Light Sparks are exchange target only",
                actual: forbiddenField,
                repairHint: "Удали Light Spark deposit/interest fields; Искры Света нельзя сдавать в казначейский вклад."));
        }
    }

    private void ValidateShiningTreasuryExchangeHistoryEntryObject(JsonElement entry, string contextPrefix, List<ValidationIssue> issues)
    {
        if (!RequireObject(entry, contextPrefix, issues))
            return;

        RequireString(entry, contextPrefix, issues, "exchangeId");
        RequireString(entry, contextPrefix, issues, "cycleId");
        RequireString(entry, contextPrefix, issues, "createdAtUtc");
        ValidateIntegerField(entry, contextPrefix, issues, "inkFeathersSpent");
        ValidateIntegerField(entry, contextPrefix, issues, "lightSparksReceived");
        ValidateIntegerField(entry, contextPrefix, issues, "rateFeathersPerSpark");

        var hasFeathers = TryReadInt(entry, "inkFeathersSpent", out var feathersSpent);
        var hasSparks = TryReadInt(entry, "lightSparksReceived", out var sparksReceived);
        var hasRate = TryReadInt(entry, "rateFeathersPerSpark", out var rate);

        if (hasFeathers && feathersSpent <= 0)
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.inkFeathersSpent",
                IssueSeverity.Error,
                "Treasury exchange must spend positive Ink Feathers",
                code: "shining_treasury_exchange_invalid_feather_cost",
                section: "ShiningAbode",
                expected: "> 0",
                actual: feathersSpent.ToString(),
                repairHint: "Записывай только состоявшиеся локальные обмены казначейства."));
        }

        if (hasSparks && sparksReceived <= 0)
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.lightSparksReceived",
                IssueSeverity.Error,
                "Treasury exchange must receive positive Light Sparks",
                code: "shining_treasury_exchange_invalid_spark_gain",
                section: "ShiningAbode",
                expected: "> 0",
                actual: sparksReceived.ToString(),
                repairHint: "Записывай lightSparksReceived как положительный integer."));
        }

        if (hasRate && rate != ShiningAbodeState.TreasuryFeathersPerLightSpark)
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.rateFeathersPerSpark",
                IssueSeverity.Error,
                "Treasury exchange rate must be canonical",
                code: "shining_treasury_exchange_rate_mismatch",
                section: "ShiningAbode",
                expected: ShiningAbodeState.TreasuryFeathersPerLightSpark.ToString(),
                actual: rate.ToString(),
                repairHint: "Используй фиксированный консервативный курс казначейства."));
        }

        var expectedFeathers = (long)sparksReceived * ShiningAbodeState.TreasuryFeathersPerLightSpark;
        if (hasFeathers && hasSparks && feathersSpent != expectedFeathers)
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.inkFeathersSpent",
                IssueSeverity.Error,
                "Treasury exchange cost does not match received Light Sparks",
                code: "shining_treasury_exchange_cost_mismatch",
                section: "ShiningAbode",
                expected: expectedFeathers.ToString(),
                actual: feathersSpent.ToString(),
                repairHint: "Синхронизируй inkFeathersSpent = lightSparksReceived * rateFeathersPerSpark."));
        }
    }

    private static void RequireTreasuryStringFieldAllowEmpty(
        JsonElement root,
        string contextPrefix,
        List<ValidationIssue> issues,
        string propertyName)
    {
        if (root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String)
            return;

        issues.Add(new ValidationIssue(
            $"{contextPrefix}.{propertyName}",
            IssueSeverity.Error,
            "Treasury cycle marker must be a string, empty before first synced cycle is allowed",
            code: "shining_treasury_cycle_marker_not_string",
            section: "ShiningAbode",
            expected: "string (empty allowed)",
            actual: root.TryGetProperty(propertyName, out value) ? value.ValueKind.ToString() : "missing",
            repairHint: "Сохраняй cycle marker казначейства строкой; пустая строка допустима до первого synced Shining return cycle."));
    }

    private void ValidateShiningGachaHistoryEntryObject(JsonElement entry, string contextPrefix, List<ValidationIssue> issues)
    {
        if (!RequireObject(entry, contextPrefix, issues))
            return;

        RequireString(entry, contextPrefix, issues, "requestId");
        RequireString(entry, contextPrefix, issues, "factionId");
        ValidateOptionalString(entry, contextPrefix, issues, "factionName");
        RequireString(entry, contextPrefix, issues, "returnCycleId");
        ValidateNonNegativeIntegerField(entry, contextPrefix, issues, "costInFeathers", "ShiningAbode");
        RequireString(entry, contextPrefix, issues, "baseRarity");
        RequireString(entry, contextPrefix, issues, "finalRarity");
        RequireString(entry, contextPrefix, issues, "relicId");
        ValidateOptionalString(entry, contextPrefix, issues, "relicName");
        ValidateNonNegativeIntegerField(entry, contextPrefix, issues, "turnNumber", "ShiningAbode");
        RequireString(entry, contextPrefix, issues, "timestamp");

        var baseRarity = GetFirstNonEmptyString(entry, "baseRarity");
        if (!string.IsNullOrWhiteSpace(baseRarity) && GetRarityRank(baseRarity) == 0)
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.baseRarity",
                IssueSeverity.Error,
                "Shining gacha history baseRarity должна быть canonical Soul Relic rarity",
                code: "shining_gacha_history_invalid_base_rarity",
                section: "ShiningAbode",
                repairHint: "Используй baseRarity из canonical soul relic rarity ladder."));
        }

        var finalRarity = GetFirstNonEmptyString(entry, "finalRarity");
        if (!string.IsNullOrWhiteSpace(finalRarity) && GetRarityRank(finalRarity) == 0)
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.finalRarity",
                IssueSeverity.Error,
                "Shining gacha history finalRarity должна быть canonical Soul Relic rarity",
                code: "shining_gacha_history_invalid_final_rarity",
                section: "ShiningAbode",
                repairHint: "Используй finalRarity из canonical soul relic rarity ladder."));
        }

        if (!string.IsNullOrWhiteSpace(baseRarity) &&
            !string.IsNullOrWhiteSpace(finalRarity) &&
            GetRarityRank(baseRarity) > 0 &&
            GetRarityRank(finalRarity) > 0 &&
            GetRarityRank(finalRarity) < GetRarityRank(baseRarity))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.finalRarity",
                IssueSeverity.Error,
                "Shining gacha history не может понижать финальную редкость ниже baseRarity",
                code: "shining_gacha_history_final_rarity_below_base",
                section: "ShiningAbode",
                expected: $">= {baseRarity}",
                actual: finalRarity,
                repairHint: "Shining banner modifiers могут только повышать или сохранять baseRarity."));
        }
    }

    private void ValidateShiningTradeInventoryObject(JsonElement tradeInventory, string contextPrefix, List<ValidationIssue> issues)
    {
        if (!RequireObject(tradeInventory, contextPrefix, issues))
            return;

        RequireString(tradeInventory, contextPrefix, issues, "tradeCycleId");
        RequireString(tradeInventory, contextPrefix, issues, "generatedAtUtc");
        ValidateIntegerField(tradeInventory, contextPrefix, issues, "generationTradeTier");
        RequireString(tradeInventory, contextPrefix, issues, "generationRarityCeiling");
        ValidateNumberField(tradeInventory, contextPrefix, issues, "serviceMultiplierSnapshot");
        ValidateOptionalString(tradeInventory, contextPrefix, issues, "merchantProfile");

        if (!tradeInventory.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.items",
                IssueSeverity.Error,
                "Shining tradeInventory.items должен быть массивом торговых слотов",
                code: "shining_trade_inventory_missing_items",
                section: "ShiningAbode",
                expected: "items array",
                actual: !tradeInventory.TryGetProperty("items", out var missingItems) ? "missing" : missingItems.ValueKind.ToString(),
                repairHint: "Сохраняй explicit Shining trade inventory как массив торговых слотов с relicData payloads."));
            return;
        }

        if (TryReadInt(tradeInventory, "generationTradeTier", out var generationTradeTier) && generationTradeTier <= 0)
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.generationTradeTier",
                IssueSeverity.Error,
                "Shining trade inventory недопустим для dormant trade tier",
                code: "shining_trade_inventory_dormant_tier",
                section: "ShiningAbode",
                expected: "1..3",
                actual: generationTradeTier.ToString(),
                repairHint: "Materialize tradeInventory только для фракций с derived tradeTier >= 1."));
        }

        var generationRarityCeiling = GetFirstNonEmptyString(tradeInventory, "generationRarityCeiling");
        if (!string.IsNullOrWhiteSpace(generationRarityCeiling) &&
            !ShiningAbodeState.IsSupportedTradeInventoryRarityCeiling(generationRarityCeiling))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.generationRarityCeiling",
                IssueSeverity.Error,
                "generationRarityCeiling использует неподдерживаемое значение",
                code: "shining_trade_inventory_invalid_ceiling",
                section: "ShiningAbode",
                repairHint: "Используй none | common | uncommon | rare | radiant."));
        }

        if (tradeInventory.TryGetProperty("merchantProfile", out var merchantProfileNode) &&
            merchantProfileNode.ValueKind != JsonValueKind.Null)
        {
            var merchantProfile = merchantProfileNode.GetString();
            if (!string.IsNullOrWhiteSpace(merchantProfile) &&
                !string.Equals(merchantProfile, ShiningTradeRequestState.MerchantProfileShiningFaction, StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new ValidationIssue(
                    $"{contextPrefix}.merchantProfile",
                    IssueSeverity.Error,
                    "Shining trade inventory merchantProfile использует неподдерживаемое значение",
                    code: "shining_trade_inventory_invalid_merchant_profile",
                    section: "ShiningAbode",
                    repairHint: $"Используй merchantProfile={ShiningTradeRequestState.MerchantProfileShiningFaction} или опусти поле."));
            }
        }

        var index = 0;
        var seenSlotIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenRelicIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items.EnumerateArray())
        {
            var itemContext = $"{contextPrefix}.items[{index++}]";
            if (!RequireObject(item, itemContext, issues))
                continue;

            RequireString(item, itemContext, issues, "slotId");
            var slotId = GetFirstNonEmptyString(item, "slotId");
            if (!string.IsNullOrWhiteSpace(slotId) && !seenSlotIds.Add(slotId))
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.slotId",
                    IssueSeverity.Error,
                    "Shining tradeInventory.items не должен содержать duplicate slotId",
                    code: "shining_trade_inventory_duplicate_slot_id",
                    section: "ShiningAbode",
                    expected: "unique slotId per trade inventory item",
                    actual: slotId,
                    repairHint: "Материализуй tradeInventory.items с уникальными slotId, чтобы локальная покупка всегда ссылалась ровно на один слот."));
            }

            ValidatePositiveIntegerField(item, itemContext, issues, "priceInFeathers");
            RequireBooleanField(item, itemContext, issues, "soldOut");
            if (!item.TryGetProperty("relicData", out var relicData) || !RequireObject(relicData, $"{itemContext}.relicData", issues))
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.relicData",
                    IssueSeverity.Error,
                    "Shining trade slot должен содержать relicData object",
                    code: "shining_trade_inventory_item_missing_relic_data",
                    section: "ShiningAbode"));
                continue;
            }

            ValidateMinimalSoulRelicObject(relicData, $"{itemContext}.relicData", issues, "ShiningAbode");
            var relicId = GetFirstNonEmptyString(relicData, "relicId", "id");
            if (!string.IsNullOrWhiteSpace(relicId) && !seenRelicIds.Add(relicId))
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.relicData.relicId",
                    IssueSeverity.Error,
                    "Shining tradeInventory.items не должен содержать duplicate relicData.relicId",
                    code: "shining_trade_inventory_duplicate_relic_id",
                    section: "ShiningAbode",
                    expected: "unique relicData.relicId per trade inventory item",
                    actual: relicId,
                    repairHint: "Каждый слот сияющей витрины должен материализовать новую уникальную Soul Relic identity; не повторяй relicId между слотами."));
            }

            var relicRarity = GetFirstNonEmptyString(relicData, "quality", "rarity");
            if (!string.IsNullOrWhiteSpace(relicRarity) &&
                !string.IsNullOrWhiteSpace(generationRarityCeiling) &&
                !ShiningAbodeState.IsSoulRelicRarityAllowedForTradeCeiling(relicRarity, generationRarityCeiling))
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.relicData",
                    IssueSeverity.Error,
                    "Shining trade slot rarity превышает generationRarityCeiling текущей сияющей витрины",
                    code: "shining_trade_inventory_item_exceeds_ceiling",
                    section: "ShiningAbode",
                    expected: generationRarityCeiling,
                    actual: relicRarity));
            }
        }
    }

    private void ValidateShiningTradeInventoryReceiptObject(JsonElement receipt, string contextPrefix, List<ValidationIssue> issues)
    {
        if (!RequireObject(receipt, contextPrefix, issues))
            return;

        RequireString(receipt, contextPrefix, issues, "requestId");
        RequireString(receipt, contextPrefix, issues, "factionId");
        ValidateOptionalString(receipt, contextPrefix, issues, "factionName");
        RequireString(receipt, contextPrefix, issues, "tradeCycleId");
        RequireString(receipt, contextPrefix, issues, "status");
        ValidateIntegerField(receipt, contextPrefix, issues, "itemCount");
        ValidateNonNegativeNumberField(receipt, contextPrefix, issues, "soldOutCount");
        ValidateIntegerField(receipt, contextPrefix, issues, "resolvedAtTurn");
        ValidateOptionalString(receipt, contextPrefix, issues, "resolvedAtUtc");

        var status = GetFirstNonEmptyString(receipt, "status");
        if (!string.IsNullOrWhiteSpace(status) &&
            !string.Equals(status, ShiningTradeRequestState.ReceiptStatusReady, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.status",
                IssueSeverity.Error,
                "Shining tradeInventoryReceipts[].status использует неподдерживаемое значение",
                code: "shining_trade_receipt_invalid_status",
                section: "ShiningAbode",
                repairHint: "Используй status = ready."));
        }

        if (string.Equals(status, ShiningTradeRequestState.ReceiptStatusReady, StringComparison.OrdinalIgnoreCase))
        {
            RequireCanonicalShiningReceiptClosureMarkers(receipt, contextPrefix, issues, "trade", "tradeInventoryReceipts");

            if (!receipt.TryGetProperty("soldOutCount", out var soldOutCount) ||
                soldOutCount.ValueKind != JsonValueKind.Number ||
                !soldOutCount.TryGetInt32(out var soldOutCountValue) ||
                soldOutCountValue < 0)
            {
                issues.Add(new ValidationIssue(
                    $"{contextPrefix}.soldOutCount",
                    IssueSeverity.Error,
                    "tradeInventoryReceipts[].soldOutCount должен быть неотрицательным canonical ready marker",
                    code: "shining_trade_receipt_missing_sold_out_count",
                    section: "ShiningAbode",
                    repairHint: "Для ready tradeInventoryReceipts указывай soldOutCount exact по materialized faction.tradeInventory."));
            }
        }
    }

    private void ValidateShiningCoreActionReceiptObject(JsonElement receipt, string contextPrefix, List<ValidationIssue> issues)
    {
        if (!RequireObject(receipt, contextPrefix, issues))
            return;
        RequireString(receipt, contextPrefix, issues, "requestId");
        RequireString(receipt, contextPrefix, issues, "actionType");
        RequireString(receipt, contextPrefix, issues, "status");
        ValidateOptionalString(receipt, contextPrefix, issues, "factionId");
        ValidateOptionalString(receipt, contextPrefix, issues, "projectId");
        ValidateOptionalString(receipt, contextPrefix, issues, "hallId");
        ValidateOptionalString(receipt, contextPrefix, issues, "resolvedFactionId");
        ValidateOptionalString(receipt, contextPrefix, issues, "relicId");
        ValidateOptionalString(receipt, contextPrefix, issues, "relicName");
        ValidateOptionalString(receipt, contextPrefix, issues, "returnCycleId");
        ValidateOptionalString(receipt, contextPrefix, issues, "baseRarity");
        ValidateOptionalString(receipt, contextPrefix, issues, "finalRarity");
        ValidateOptionalString(receipt, contextPrefix, issues, "targetFormTag");
        RequireArrayOfStrings(receipt, contextPrefix, issues, "selectedCardIds");
        RequireArrayOfStrings(receipt, contextPrefix, issues, "newResidentIds");
        RequireArrayOfStrings(receipt, contextPrefix, issues, "seededProjectIds");
        ValidateIntegerField(receipt, contextPrefix, issues, "quotedCostFeathers");
        ValidateIntegerField(receipt, contextPrefix, issues, "quotedCostLightSparks");
        ValidateIntegerField(receipt, contextPrefix, issues, "generatedDraftVersion");
        ValidateIntegerField(receipt, contextPrefix, issues, "resolvedAtTurn");
        ValidateOptionalString(receipt, contextPrefix, issues, "resolvedAtUtc");
        ValidateOptionalString(receipt, contextPrefix, issues, "reason");

        if (receipt.TryGetProperty("propertyIndex", out var propertyIndex) &&
            propertyIndex.ValueKind != JsonValueKind.Null &&
            (propertyIndex.ValueKind != JsonValueKind.Number || !propertyIndex.TryGetInt32(out _)))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.propertyIndex",
                IssueSeverity.Error,
                "coreActionReceipts[].propertyIndex должен быть integer when present",
                code: "shining_core_action_receipt_invalid_property_index",
                section: "ShiningAbode",
                repairHint: "Используй integer propertyIndex в forge receipts или не указывай поле для других core actions."));
        }

        var actionType = GetFirstNonEmptyString(receipt, "actionType");
        if (!string.IsNullOrWhiteSpace(actionType) && !ShiningCoreActionRequestState.IsSupportedActionType(actionType))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.actionType",
                IssueSeverity.Error,
                "coreActionReceipts[].actionType использует неподдерживаемое значение",
                code: "shining_core_action_receipt_invalid_action_type",
                section: "ShiningAbode",
                repairHint: "Используй один из canonical Shining core action types."));
        }

        var status = GetFirstNonEmptyString(receipt, "status");
        if (!string.IsNullOrWhiteSpace(status) && !ShiningCoreActionRequestState.IsSupportedStatus(status))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.status",
                IssueSeverity.Error,
                "coreActionReceipts[].status использует неподдерживаемое значение",
                code: "shining_core_action_receipt_invalid_status",
                section: "ShiningAbode",
                repairHint: "Используй accepted, refused или withdrawn."));
        }

        if (ShiningCoreActionRequestState.IsSupportedStatus(status))
        {
            var resolvedAtTurn = TryReadInt(receipt, "resolvedAtTurn", out var parsedResolvedAtTurn) ? parsedResolvedAtTurn : 0;
            var resolvedAtUtc = GetFirstNonEmptyString(receipt, "resolvedAtUtc");
            if (resolvedAtTurn <= 0)
            {
                issues.Add(new ValidationIssue(
                    $"{contextPrefix}.resolvedAtTurn",
                    IssueSeverity.Error,
                    "coreActionReceipts[].resolvedAtTurn должен быть положительным canonical closure marker",
                    code: "shining_core_action_receipt_missing_resolved_at_turn",
                    section: "ShiningAbode",
                    repairHint: "Для accepted/refused/withdrawn receipt указывай положительный resolvedAtTurn."));
            }

            if (string.IsNullOrWhiteSpace(resolvedAtUtc))
            {
                issues.Add(new ValidationIssue(
                    $"{contextPrefix}.resolvedAtUtc",
                    IssueSeverity.Error,
                    "coreActionReceipts[].resolvedAtUtc должен быть непустым canonical closure marker",
                    code: "shining_core_action_receipt_missing_resolved_at_utc",
                    section: "ShiningAbode",
                    repairHint: "Для accepted/refused/withdrawn receipt указывай ISO 8601 resolvedAtUtc."));
            }
        }

        if (receipt.TryGetProperty("selectedCards", out _))
            ValidateArrayItems(receipt, $"{contextPrefix}.selectedCards", issues, "selectedCards", ValidateShiningBlessingCardObject);

        if (string.Equals(actionType, ShiningCoreActionRequestState.ActionTypePrepareIncarnationPackage, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(status, ShiningCoreActionRequestState.RequestStatusAccepted, StringComparison.OrdinalIgnoreCase))
        {
            if (!receipt.TryGetProperty("selectedCardIds", out var requiredSelectedCardIds) ||
                requiredSelectedCardIds.ValueKind != JsonValueKind.Array ||
                requiredSelectedCardIds.GetArrayLength() == 0)
            {
                issues.Add(new ValidationIssue(
                    $"{contextPrefix}.selectedCardIds",
                    IssueSeverity.Error,
                    "accepted prepare_incarnation_package receipt должен хранить non-empty selectedCardIds snapshot",
                    code: "shining_prepare_package_receipt_missing_selected_card_ids",
                    section: "ShiningAbode",
                    repairHint: "Для accepted prepare_incarnation_package receipt сохрани selectedCardIds[] из frozen package."));
            }

            if (!receipt.TryGetProperty("selectedCards", out var requiredSelectedCards) ||
                requiredSelectedCards.ValueKind != JsonValueKind.Array ||
                requiredSelectedCards.GetArrayLength() == 0)
            {
                issues.Add(new ValidationIssue(
                    $"{contextPrefix}.selectedCards",
                    IssueSeverity.Error,
                    "accepted prepare_incarnation_package receipt должен хранить non-empty selectedCards[] frozen snapshot",
                    code: "shining_prepare_package_receipt_missing_selected_cards",
                    section: "ShiningAbode",
                    repairHint: "Для accepted prepare_incarnation_package receipt сохрани selectedCards[] snapshots, совпадающие с selectedCardIds[]."));
            }
        }

        if (string.Equals(actionType, ShiningCoreActionRequestState.ActionTypePrepareIncarnationPackage, StringComparison.OrdinalIgnoreCase) &&
            receipt.TryGetProperty("selectedCardIds", out var selectedCardIds) &&
            selectedCardIds.ValueKind == JsonValueKind.Array &&
            receipt.TryGetProperty("selectedCards", out var selectedCards) &&
            selectedCards.ValueKind == JsonValueKind.Array)
        {
            var storedIds = selectedCardIds.EnumerateArray()
                .Where(node => node.ValueKind == JsonValueKind.String)
                .Select(node => (node.GetString() ?? string.Empty).Trim())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToList();
            var cardSnapshotIds = selectedCards.EnumerateArray()
                .Where(node => node.ValueKind == JsonValueKind.Object)
                .Select(node => GetFirstNonEmptyString(node, "cardId")?.Trim() ?? string.Empty)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToList();

            if (storedIds.Count > 0 &&
                cardSnapshotIds.Count > 0 &&
                (storedIds.Count != cardSnapshotIds.Count ||
                 !storedIds.SequenceEqual(cardSnapshotIds, StringComparer.OrdinalIgnoreCase)))
            {
                issues.Add(new ValidationIssue(
                    $"{contextPrefix}.selectedCards",
                    IssueSeverity.Error,
                    "prepare_incarnation_package receipt содержит stale selectedCards snapshot, не совпадающий с selectedCardIds",
                    code: "shining_prepare_package_receipt_selected_cards_mismatch",
                    section: "ShiningAbode",
                    expected: string.Join(", ", storedIds),
                    actual: string.Join(", ", cardSnapshotIds),
                    repairHint: "Храни selectedCards exact как frozen snapshot тех же cardId, что записаны в selectedCardIds, и в том же порядке."));
            }
        }
    }

    private void ValidateShiningFoundingReceiptObject(JsonElement receipt, string contextPrefix, List<ValidationIssue> issues)
    {
        if (!RequireObject(receipt, contextPrefix, issues))
            return;
        RequireString(receipt, contextPrefix, issues, "requestId");
        RequireString(receipt, contextPrefix, issues, "proposedFactionId");
        RequireString(receipt, contextPrefix, issues, "proposedHallId");
        RequireString(receipt, contextPrefix, issues, "hallName");
        RequireString(receipt, contextPrefix, issues, "factionId");
        RequireString(receipt, contextPrefix, issues, "hallId");
        RequireString(receipt, contextPrefix, issues, "status");
        RequireArrayOfStrings(receipt, contextPrefix, issues, "supportingResidentIds");
        ValidateIntegerField(receipt, contextPrefix, issues, "quotedCostFeathers");
        ValidateIntegerField(receipt, contextPrefix, issues, "quotedCostLightSparks");
        ValidateIntegerField(receipt, contextPrefix, issues, "resolvedAtTurn");
        ValidateOptionalString(receipt, contextPrefix, issues, "resolvedAtUtc");
        ValidateOptionalString(receipt, contextPrefix, issues, "reason");

        var status = GetFirstNonEmptyString(receipt, "status");
        if (!string.IsNullOrWhiteSpace(status) && !ShiningFactionRequestState.IsSupportedFoundingStatus(status))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.status",
                IssueSeverity.Error,
                "factionFoundingReceipts[].status использует неподдерживаемое значение",
                code: "shining_founding_receipt_invalid_status",
                section: "ShiningAbode",
                repairHint: "Используй accepted, refused или withdrawn."));
        }

        if (ShiningFactionRequestState.IsSupportedFoundingStatus(status))
            RequireCanonicalShiningReceiptClosureMarkers(receipt, contextPrefix, issues, "founding", "foundingReceipts");
    }

    private void ValidateShiningRealignmentReceiptObject(JsonElement receipt, string contextPrefix, List<ValidationIssue> issues)
    {
        if (!RequireObject(receipt, contextPrefix, issues))
            return;
        RequireString(receipt, contextPrefix, issues, "requestId");
        RequireString(receipt, contextPrefix, issues, "residentId");
        ValidateOptionalString(receipt, contextPrefix, issues, "residentName");
        RequireString(receipt, contextPrefix, issues, "sourceFactionId");
        ValidateOptionalString(receipt, contextPrefix, issues, "sourceFactionName");
        ValidateOptionalString(receipt, contextPrefix, issues, "targetFactionId");
        ValidateOptionalString(receipt, contextPrefix, issues, "targetFactionName");
        RequireString(receipt, contextPrefix, issues, "status");
        RequireString(receipt, contextPrefix, issues, "realignmentMode");
        ValidateOptionalString(receipt, contextPrefix, issues, "residentHistoryEntryId");
        ValidateIntegerField(receipt, contextPrefix, issues, "resolvedAtTurn");
        ValidateOptionalString(receipt, contextPrefix, issues, "resolvedAtUtc");
        ValidateOptionalString(receipt, contextPrefix, issues, "reason");

        var status = GetFirstNonEmptyString(receipt, "status");
        if (!string.IsNullOrWhiteSpace(status) && !ShiningFactionRequestState.IsSupportedRealignmentStatus(status))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.status",
                IssueSeverity.Error,
                "factionRealignmentReceipts[].status использует неподдерживаемое значение",
                code: "shining_realignment_receipt_invalid_status",
                section: "ShiningAbode",
                repairHint: "Используй accepted, refused, departed_to_neutral или withdrawn."));
        }

        var mode = GetFirstNonEmptyString(receipt, "realignmentMode");
        if (!string.IsNullOrWhiteSpace(mode) && !ShiningFactionRequestState.IsSupportedRealignmentMode(mode))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.realignmentMode",
                IssueSeverity.Error,
                "factionRealignmentReceipts[].realignmentMode использует неподдерживаемое значение",
                code: "shining_realignment_receipt_invalid_mode",
                section: "ShiningAbode",
                repairHint: "Используй accepted_transfer, refused_transfer или departure_to_neutral."));
        }

        if (ShiningFactionRequestState.IsSupportedRealignmentStatus(status))
        {
            RequireCanonicalShiningReceiptClosureMarkers(receipt, contextPrefix, issues, "realignment", "realignmentReceipts");
            if ((string.Equals(status, ShiningFactionRequestState.RequestStatusAccepted, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(status, ShiningFactionRequestState.RequestStatusDepartedToNeutral, StringComparison.OrdinalIgnoreCase)) &&
                string.IsNullOrWhiteSpace(GetFirstNonEmptyString(receipt, "residentHistoryEntryId")))
            {
                issues.Add(new ValidationIssue(
                    $"{contextPrefix}.residentHistoryEntryId",
                    IssueSeverity.Error,
                    "accepted/departed Shining realignment receipt должен ссылаться на resident history entry.",
                    code: "shining_realignment_receipt_missing_resident_history_entry_id",
                    section: "ShiningAbode",
                    repairHint: "Для accepted или departed_to_neutral realignment receipt укажи residentHistoryEntryId, соответствующий записи истории резидента."));
            }
        }
    }

    private void ValidateShiningLeadershipReceiptObject(JsonElement receipt, string contextPrefix, List<ValidationIssue> issues)
    {
        if (!RequireObject(receipt, contextPrefix, issues))
            return;
        RequireString(receipt, contextPrefix, issues, "requestId");
        ValidateOptionalString(receipt, contextPrefix, issues, "factionName");
        RequireString(receipt, contextPrefix, issues, "transitionMode");
        ValidateOptionalNullableStringField(receipt, contextPrefix, issues, "previousHeadActorType");
        ValidateOptionalNullableStringField(receipt, contextPrefix, issues, "previousHeadActorId");
        ValidateOptionalString(receipt, contextPrefix, issues, "previousHeadLabel");
        ValidateOptionalNullableStringField(receipt, contextPrefix, issues, "newHeadActorType");
        ValidateOptionalNullableStringField(receipt, contextPrefix, issues, "newHeadActorId");
        ValidateOptionalString(receipt, contextPrefix, issues, "newHeadLabel");
        RequireString(receipt, contextPrefix, issues, "status");
        ValidateIntegerField(receipt, contextPrefix, issues, "resolvedAtTurn");
        ValidateOptionalString(receipt, contextPrefix, issues, "resolvedAtUtc");
        ValidateOptionalString(receipt, contextPrefix, issues, "reason");

        var status = GetFirstNonEmptyString(receipt, "status");
        if (!string.IsNullOrWhiteSpace(status) && !ShiningFactionRequestState.IsSupportedLeadershipStatus(status))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.status",
                IssueSeverity.Error,
                "leadershipReceipts[].status использует неподдерживаемое значение",
                code: "shining_leadership_receipt_invalid_status",
                section: "ShiningAbode",
                repairHint: "Используй accepted, refused или withdrawn."));
        }

        var transitionMode = GetFirstNonEmptyString(receipt, "transitionMode");
        if (!string.IsNullOrWhiteSpace(transitionMode) && !ShiningFactionRequestState.IsSupportedTransitionMode(transitionMode))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.transitionMode",
                IssueSeverity.Error,
                "leadershipReceipts[].transitionMode использует неподдерживаемое значение",
                code: "shining_leadership_receipt_invalid_mode",
                section: "ShiningAbode",
                repairHint: "Используй abdication, peaceful_succession или revolt."));
        }

        if (ShiningFactionRequestState.IsSupportedLeadershipStatus(status))
            RequireCanonicalShiningReceiptClosureMarkers(receipt, contextPrefix, issues, "leadership", "leadershipReceipts");
    }

    private void RequireCanonicalShiningReceiptClosureMarkers(
        JsonElement receipt,
        string contextPrefix,
        List<ValidationIssue> issues,
        string receiptFamilyCodePrefix,
        string receiptFamilyLabel)
    {
        var resolvedAtTurn = TryReadInt(receipt, "resolvedAtTurn", out var parsedResolvedAtTurn) ? parsedResolvedAtTurn : 0;
        var resolvedAtUtc = GetFirstNonEmptyString(receipt, "resolvedAtUtc");
        if (resolvedAtTurn <= 0)
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.resolvedAtTurn",
                IssueSeverity.Error,
                $"{receiptFamilyLabel}[].resolvedAtTurn должен быть положительным canonical closure marker",
                code: $"shining_{receiptFamilyCodePrefix}_receipt_missing_resolved_at_turn",
                section: "ShiningAbode",
                repairHint: $"Для accepted/refused/withdrawn {receiptFamilyLabel} указывай положительный resolvedAtTurn."));
        }

        if (string.IsNullOrWhiteSpace(resolvedAtUtc))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.resolvedAtUtc",
                IssueSeverity.Error,
                $"{receiptFamilyLabel}[].resolvedAtUtc должен быть непустым canonical closure marker",
                code: $"shining_{receiptFamilyCodePrefix}_receipt_missing_resolved_at_utc",
                section: "ShiningAbode",
                repairHint: $"Для accepted/refused/withdrawn {receiptFamilyLabel} указывай ISO 8601 resolvedAtUtc."));
        }
    }

    private void ValidateShiningLeadershipHistoryObject(JsonElement history, string contextPrefix, List<ValidationIssue> issues)
    {
        if (!RequireObject(history, contextPrefix, issues))
            return;
        RequireString(history, contextPrefix, issues, "eventId");
        RequireString(history, contextPrefix, issues, "requestId");
        RequireString(history, contextPrefix, issues, "eventType");
        RequireString(history, contextPrefix, issues, "summary");
        ValidateIntegerField(history, contextPrefix, issues, "turnNumber");
        ValidateOptionalString(history, contextPrefix, issues, "occurredAtUtc");

        var eventType = GetFirstNonEmptyString(history, "eventType");
        if (!string.IsNullOrWhiteSpace(eventType) && !ShiningFactionRequestState.IsSupportedLeadershipHistoryEventType(eventType))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.eventType",
                IssueSeverity.Error,
                "leadershipHistory[].eventType использует неподдерживаемое значение",
                code: "shining_leadership_history_invalid_event_type",
                section: "ShiningAbode",
                repairHint: "Используй abdicated, succeeded, revolted, refused или vacated."));
        }
    }

    private void ValidatePendingShiningFactionFoundingRequestObject(JsonElement request, string contextPrefix, List<ValidationIssue> issues)
    {
        if (!RequireObject(request, contextPrefix, issues))
            return;
        RequireString(request, contextPrefix, issues, "requestId");
        RequireString(request, contextPrefix, issues, "proposedFactionId");
        RequireString(request, contextPrefix, issues, "proposedHallId");
        RequireString(request, contextPrefix, issues, "proposedHallName");
        RequireString(request, contextPrefix, issues, "proposedHallDescription");
        RequireArrayOfStrings(request, contextPrefix, issues, "proposedHallServiceTags");
        ValidateIntegerField(request, contextPrefix, issues, "quotedCostFeathers");
        ValidateIntegerField(request, contextPrefix, issues, "quotedCostLightSparks");
        ValidateIntegerField(request, contextPrefix, issues, "createdAtTurn");
        ValidateOptionalString(request, contextPrefix, issues, "createdAtUtc");
        if (request.TryGetProperty("charter", out var charter))
            ValidateShiningFactionCharterObject(charter, $"{contextPrefix}.charter", issues);
        RequireArrayOfStrings(request, contextPrefix, issues, "supportingResidentIds");

        if (request.TryGetProperty("proposedHallServiceTags", out var serviceTags) && serviceTags.ValueKind == JsonValueKind.Array)
        {
            var parsedTags = serviceTags.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString())
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Cast<string>()
                .ToList();
            if (parsedTags.Count is < 1 or > 2 || parsedTags.Any(tag => !ShiningAbodeState.IsSupportedHallServiceTag(tag)))
            {
                issues.Add(new ValidationIssue(
                    $"{contextPrefix}.proposedHallServiceTags",
                    IssueSeverity.Error,
                    "Founding hall должен содержать 1..2 supported service tags",
                    code: "shining_founding_invalid_hall_service_tags",
                    section: "ShiningAbode",
                    repairHint: "Используй для proposedHallServiceTags 1..2 уникальных тега из canonical hall-service allowlist."));
            }
        }
    }

    private void ValidatePendingShiningCoreActionRequestObject(JsonElement request, string contextPrefix, List<ValidationIssue> issues)
    {
        if (!RequireObject(request, contextPrefix, issues))
            return;

        RequireString(request, contextPrefix, issues, "requestId");
        RequireString(request, contextPrefix, issues, "actionType");
        ValidateOptionalString(request, contextPrefix, issues, "factionId");
        ValidateOptionalString(request, contextPrefix, issues, "factionName");
        ValidateOptionalString(request, contextPrefix, issues, "projectId");
        ValidateOptionalString(request, contextPrefix, issues, "projectDisplayName");
        ValidateIntegerField(request, contextPrefix, issues, "radianceTierAtRequest");
        ValidateIntegerField(request, contextPrefix, issues, "quotedCostFeathers");
        ValidateIntegerField(request, contextPrefix, issues, "quotedCostLightSparks");
        ValidateIntegerField(request, contextPrefix, issues, "sourceDraftVersion");
        RequireArrayOfStrings(request, contextPrefix, issues, "selectedCardIds");
        ValidateOptionalString(request, contextPrefix, issues, "returnCycleId");
        ValidateOptionalString(request, contextPrefix, issues, "relicId");
        ValidateOptionalString(request, contextPrefix, issues, "relicName");
        ValidateOptionalString(request, contextPrefix, issues, "targetFormTag");
        ValidateIntegerField(request, contextPrefix, issues, "createdAtTurn");
        ValidateOptionalString(request, contextPrefix, issues, "createdAtUtc");

        var actionType = GetFirstNonEmptyString(request, "actionType");
        if (!string.IsNullOrWhiteSpace(actionType) && !ShiningCoreActionRequestState.IsSupportedActionType(actionType))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.actionType",
                IssueSeverity.Error,
                "pending_shining_abode_actions.json uses unsupported actionType",
                code: "shining_core_action_invalid_action_type",
                section: "ShiningAbode",
                repairHint: "Используй один из canonical Shining core action types."));
        }

        if (request.TryGetProperty("projectDraft", out var projectDraft) &&
            projectDraft.ValueKind != JsonValueKind.Null)
        {
            ValidateShiningProjectDraftObject(projectDraft, $"{contextPrefix}.projectDraft", issues);
        }

        if (request.TryGetProperty("replacementProperty", out var replacementProperty) &&
            replacementProperty.ValueKind != JsonValueKind.Null)
        {
            if (!RequireObject(replacementProperty, $"{contextPrefix}.replacementProperty", issues))
                return;
        }

        if (request.TryGetProperty("propertyIndex", out var propertyIndex) &&
            propertyIndex.ValueKind != JsonValueKind.Null &&
            (propertyIndex.ValueKind != JsonValueKind.Number || !propertyIndex.TryGetInt32(out _)))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.propertyIndex",
                IssueSeverity.Error,
                "propertyIndex должен быть integer when present",
                code: "shining_core_action_invalid_property_index",
                section: "ShiningAbode",
                repairHint: "Передавай propertyIndex как integer или не указывай поле для action types, которым он не нужен."));
        }

        if (request.TryGetProperty("projectedGachaBonusSteps", out var projectedGachaBonusSteps) &&
            projectedGachaBonusSteps.ValueKind != JsonValueKind.Null &&
            (projectedGachaBonusSteps.ValueKind != JsonValueKind.Number || !projectedGachaBonusSteps.TryGetInt32(out _)))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.projectedGachaBonusSteps",
                IssueSeverity.Error,
                "projectedGachaBonusSteps должен быть integer when present",
                code: "shining_core_action_invalid_projected_gacha_bonus_steps",
                section: "ShiningAbode",
                repairHint: "Передавай projectedGachaBonusSteps как integer или не указывай поле для не-gacha action types."));
        }

        if (request.TryGetProperty("addedProperties", out var addedProperties) &&
            addedProperties.ValueKind != JsonValueKind.Null)
        {
            ValidateArrayItems(addedProperties, $"{contextPrefix}.addedProperties", issues, ValidateShiningForgePropertyObject);
        }

        if (string.Equals(actionType, ShiningCoreActionRequestState.ActionTypePrepareIncarnationPackage, StringComparison.OrdinalIgnoreCase) &&
            request.TryGetProperty("selectedCardIds", out var selectedCardIds) &&
            selectedCardIds.ValueKind == JsonValueKind.Array)
        {
            var normalizedIds = selectedCardIds.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString()))
                .Select(item => item.GetString()!.Trim())
                .ToList();
            if (normalizedIds.Count != normalizedIds.Distinct(StringComparer.OrdinalIgnoreCase).Count())
            {
                issues.Add(new ValidationIssue(
                    $"{contextPrefix}.selectedCardIds",
                    IssueSeverity.Error,
                    "prepare_incarnation_package не допускает duplicate selectedCardIds",
                    code: "shining_prepare_package_duplicate_selected_card_ids",
                    section: "ShiningAbode",
                    repairHint: "Передавай в selectedCardIds уникальный ordered snapshot без повторов."));
            }
        }
    }

    private void ValidatePendingShiningTradeInventoryRequestObject(JsonElement request, string contextPrefix, List<ValidationIssue> issues)
    {
        if (!RequireObject(request, contextPrefix, issues))
            return;

        RequireString(request, contextPrefix, issues, "requestId");
        RequireString(request, contextPrefix, issues, "factionId");
        RequireString(request, contextPrefix, issues, "factionName");
        RequireString(request, contextPrefix, issues, "tradeCycleId");
        ValidateIntegerField(request, contextPrefix, issues, "derivedTradeTier");
        ValidateIntegerField(request, contextPrefix, issues, "derivedTradeSlotCount");
        RequireString(request, contextPrefix, issues, "derivedRarityCeiling");
        ValidateNumberField(request, contextPrefix, issues, "derivedServiceMultiplier");
        ValidateOptionalString(request, contextPrefix, issues, "merchantProfile");
        ValidateIntegerField(request, contextPrefix, issues, "createdAtTurn");
        ValidateOptionalString(request, contextPrefix, issues, "createdAtUtc");

        if (TryReadInt(request, "derivedTradeTier", out var tradeTier) && tradeTier <= 0)
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.derivedTradeTier",
                IssueSeverity.Error,
                "pending Shining trade request требует derivedTradeTier >= 1",
                code: "shining_trade_request_dormant_tier",
                section: "ShiningAbode",
                repairHint: "Не создавай trade request для dormant Shining faction."));
        }

        if (TryReadInt(request, "derivedTradeSlotCount", out var slotCount) && slotCount <= 0)
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.derivedTradeSlotCount",
                IssueSeverity.Error,
                "pending Shining trade request требует derivedTradeSlotCount > 0",
                code: "shining_trade_request_invalid_slot_count",
                section: "ShiningAbode",
                repairHint: "Передавай в request точное положительное количество слотов derived trade profile."));
        }

        var rarityCeiling = GetFirstNonEmptyString(request, "derivedRarityCeiling");
        if (!string.IsNullOrWhiteSpace(rarityCeiling) &&
            !ShiningAbodeState.IsSupportedTradeInventoryRarityCeiling(rarityCeiling))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.derivedRarityCeiling",
                IssueSeverity.Error,
                "pending Shining trade request использует неподдерживаемый derivedRarityCeiling",
                code: "shining_trade_request_invalid_ceiling",
                section: "ShiningAbode",
                repairHint: "Используй none | common | uncommon | rare | radiant."));
        }

        var merchantProfile = GetFirstNonEmptyString(request, "merchantProfile");
        if (!string.IsNullOrWhiteSpace(merchantProfile) &&
            !string.Equals(merchantProfile, ShiningTradeRequestState.MerchantProfileShiningFaction, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.merchantProfile",
                IssueSeverity.Error,
                "pending Shining trade request использует неподдерживаемый merchantProfile",
                code: "shining_trade_request_invalid_merchant_profile",
                section: "ShiningAbode",
                repairHint: $"Используй merchantProfile={ShiningTradeRequestState.MerchantProfileShiningFaction} или опусти поле."));
        }
    }

    private void ValidateShiningProjectDraftObject(JsonElement projectDraft, string contextPrefix, List<ValidationIssue> issues)
    {
        if (!RequireObject(projectDraft, contextPrefix, issues))
            return;

        RequireString(projectDraft, contextPrefix, issues, "displayName");
        RequireString(projectDraft, contextPrefix, issues, "summary");
        RequireArrayOfStrings(projectDraft, contextPrefix, issues, "toneTags");
        RequireArrayOfStrings(projectDraft, contextPrefix, issues, "targetFactionIds");
        RequireString(projectDraft, contextPrefix, issues, "projectArchetype");
        RequireString(projectDraft, contextPrefix, issues, "outputEffectFamily");
        ValidateIntegerField(projectDraft, contextPrefix, issues, "tier");
    }

    private void ValidateShiningForgePropertyObject(JsonElement property, string contextPrefix, List<ValidationIssue> issues)
    {
        if (!RequireObject(property, contextPrefix, issues))
            return;

        ValidateOptionalString(property, contextPrefix, issues, "propertyId");
        ValidateOptionalString(property, contextPrefix, issues, "name");
        ValidateOptionalString(property, contextPrefix, issues, "stat");
        if (!property.TryGetProperty("band", out var band))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.band",
                IssueSeverity.Error,
                "Forge property object должен содержать band",
                code: "shining_forge_property_missing_band",
                section: "ShiningAbode",
                repairHint: "Передавай в replacementProperty/addedProperties canonical band value."));
            return;
        }

        if (band.ValueKind is not JsonValueKind.Number and not JsonValueKind.String)
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.band",
                IssueSeverity.Error,
                "Forge property band должен быть string или integer",
                code: "shining_forge_property_invalid_band",
                section: "ShiningAbode",
                repairHint: "Используй для band string step или integer level."));
        }
    }

    private void ValidatePendingShiningFactionRealignmentRequestObject(JsonElement request, string contextPrefix, List<ValidationIssue> issues)
    {
        if (!RequireObject(request, contextPrefix, issues))
            return;
        RequireString(request, contextPrefix, issues, "requestId");
        RequireString(request, contextPrefix, issues, "residentId");
        RequireString(request, contextPrefix, issues, "residentName");
        RequireString(request, contextPrefix, issues, "sourceFactionId");
        ValidateOptionalString(request, contextPrefix, issues, "sourceFactionName");
        ValidateOptionalString(request, contextPrefix, issues, "targetFactionId");
        ValidateOptionalString(request, contextPrefix, issues, "targetFactionName");
        RequireString(request, contextPrefix, issues, "realignmentMode");
        ValidateIntegerField(request, contextPrefix, issues, "factionLoyaltyLevel");
        RequireString(request, contextPrefix, issues, "factionLoyaltyTier");
        ValidateIntegerField(request, contextPrefix, issues, "factionRestlessness");
        RequireString(request, contextPrefix, issues, "factionRealignmentState");
        ValidateIntegerField(request, contextPrefix, issues, "createdAtTurn");
        ValidateOptionalString(request, contextPrefix, issues, "createdAtUtc");

        var mode = GetFirstNonEmptyString(request, "realignmentMode");
        if (!string.IsNullOrWhiteSpace(mode) && !ShiningFactionRequestState.IsSupportedRealignmentMode(mode))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.realignmentMode",
                IssueSeverity.Error,
                "realignmentMode использует неподдерживаемое значение",
                code: "shining_realignment_invalid_mode",
                section: "ShiningAbode",
                repairHint: "Используй accepted_transfer, refused_transfer или departure_to_neutral."));
        }

        var loyaltyTier = GetFirstNonEmptyString(request, "factionLoyaltyTier");
        if (!string.IsNullOrWhiteSpace(loyaltyTier) && !ShiningAbodeState.IsSupportedFactionLoyaltyTier(loyaltyTier))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.factionLoyaltyTier",
                IssueSeverity.Error,
                "factionLoyaltyTier использует неподдерживаемое значение",
                code: "shining_realignment_invalid_loyalty_tier",
                section: "ShiningAbode",
                repairHint: "Используй alienated, uncertain, attached, devoted или steadfast."));
        }

        var realignmentState = GetFirstNonEmptyString(request, "factionRealignmentState");
        if (!string.IsNullOrWhiteSpace(realignmentState) && !ShiningAbodeState.IsSupportedFactionRealignmentState(realignmentState))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.factionRealignmentState",
                IssueSeverity.Error,
                "factionRealignmentState использует неподдерживаемое значение",
                code: "shining_realignment_invalid_state",
                section: "ShiningAbode",
                repairHint: "Используй settled, wavering, restless, considering_realignment или ready_to_realign."));
        }
    }

    private void ValidatePendingShiningFactionLeadershipTransitionRequestObject(JsonElement request, string contextPrefix, List<ValidationIssue> issues)
    {
        if (!RequireObject(request, contextPrefix, issues))
            return;
        RequireString(request, contextPrefix, issues, "requestId");
        RequireString(request, contextPrefix, issues, "factionId");
        ValidateOptionalString(request, contextPrefix, issues, "factionName");
        RequireString(request, contextPrefix, issues, "transitionMode");
        ValidateOptionalString(request, contextPrefix, issues, "incumbentHeadActorType");
        ValidateOptionalString(request, contextPrefix, issues, "incumbentHeadActorId");
        ValidateOptionalString(request, contextPrefix, issues, "candidateHeadActorType");
        ValidateOptionalString(request, contextPrefix, issues, "candidateHeadActorId");
        RequireArrayOfStrings(request, contextPrefix, issues, "supportingResidentIds");
        ValidateIntegerField(request, contextPrefix, issues, "createdAtTurn");
        ValidateOptionalString(request, contextPrefix, issues, "createdAtUtc");

        var transitionMode = GetFirstNonEmptyString(request, "transitionMode");
        if (!string.IsNullOrWhiteSpace(transitionMode) && !ShiningFactionRequestState.IsSupportedTransitionMode(transitionMode))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.transitionMode",
                IssueSeverity.Error,
                "transitionMode использует неподдерживаемое значение",
                code: "shining_leadership_invalid_mode",
                section: "ShiningAbode",
                repairHint: "Используй abdication, peaceful_succession или revolt."));
        }

        foreach (var fieldName in new[] { "incumbentHeadActorType", "candidateHeadActorType" })
        {
            var actorType = GetFirstNonEmptyString(request, fieldName);
            if (!string.IsNullOrWhiteSpace(actorType) && !ShiningAbodeState.IsSupportedHeadActorType(actorType))
            {
                issues.Add(new ValidationIssue(
                    $"{contextPrefix}.{fieldName}",
                    IssueSeverity.Error,
                    $"{fieldName} использует неподдерживаемый headActorType",
                    code: "shining_leadership_invalid_actor_type",
                    section: "ShiningAbode",
                    repairHint: "Используй guardian, player_soul, resident или radiant_actor."));
            }
        }
    }

    private static string? GetShiningNodeString(JsonNode? node)
    {
        if (node == null)
            return null;

        try
        {
            return node.GetValue<string>();
        }
        catch
        {
            return node.ToJsonString().Trim('"');
        }
    }

    private static void ValidateArrayItems(
        JsonElement root,
        string contextPrefix,
        List<ValidationIssue> issues,
        string propertyName,
        Action<JsonElement, string, List<ValidationIssue>> validator)
    {
        if (!root.TryGetProperty(propertyName, out var array))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.{propertyName}",
                IssueSeverity.Error,
                "Обязательное массивное поле отсутствует"));
            return;
        }

        ValidateArrayItems(array, $"{contextPrefix}.{propertyName}", issues, validator);
    }

    private static void RequireArrayOfStrings(JsonElement root, string contextPrefix, List<ValidationIssue> issues, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var array))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.{propertyName}",
                IssueSeverity.Error,
                "Обязательное поле-массив строк отсутствует"));
            return;
        }

        RequireArrayOfStrings(array, $"{contextPrefix}.{propertyName}", issues);
    }

    private static void RequireIntegerField(
        JsonElement root,
        string contextPrefix,
        List<ValidationIssue> issues,
        string propertyName,
        string code,
        string message)
    {
        if (root.TryGetProperty(propertyName, out var value) &&
            value.ValueKind == JsonValueKind.Number &&
            value.TryGetInt32(out _))
        {
            return;
        }

        issues.Add(new ValidationIssue(
            $"{contextPrefix}.{propertyName}",
            IssueSeverity.Error,
            message,
            code: code,
            section: "ShiningAbode",
            repairHint: $"Укажи {propertyName} как integer, exact from the canonical Shining request/legacy contract."));
    }
}
