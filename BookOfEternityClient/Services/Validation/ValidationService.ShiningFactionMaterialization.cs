using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    private async Task
        ValidateAcceptedTurnShiningFactionMaterializationCompletenessAsync(
            bool rawBeforeNormalization,
            List<ValidationIssue> issues)
    {
        var currentRoot = await ReadJsonObjectAsync(
            ShiningAbodeState.StatePath);
        if (currentRoot == null)
            return;

        var snapshotLookup =
            await LoadValidatedPendingTurnSnapshotLookupAsync();
        var hasUsablePreTurnAuthority =
            snapshotLookup.Status ==
            ValidatedPendingTurnSnapshotStatus.Usable;
        var preTurnRoot = hasUsablePreTurnAuthority
            ? ParseShiningMaterializationRoot(
                await ReadPreTurnTrackedFileAsync(
                    ShiningAbodeState.StatePath))
            : null;
        var preTurnFactions =
            ReadShiningMaterializationTargetMap(preTurnRoot);
        var currentFactions =
            ReadShiningMaterializationTargets(currentRoot);

        var residentRoot = await ReadJsonObjectAsync(
            GuardianAbodeResidentState.StatePath);
        var guardiansRoot = await ReadJsonObjectAsync(
            "game_state/meta/guardians.json");
        var afterlifeProfilesRoot = await ReadJsonObjectAsync(
            AfterlifeEntityProfileState.StatePath);
        var sarefStoryRoot = await ReadJsonObjectAsync(
            SarefMainStoryState.StatePath);
        var currentTradeAuthorities =
            await LoadCurrentAfterlifeActorTradeAuthoritiesAsync();
        var radianceTier =
            ShiningAbodeState.ResolveRadianceTierFromAuthoredState(
                currentRoot);

        foreach (var target in currentFactions)
        {
            preTurnFactions.TryGetValue(
                target.FactionId,
                out var previous);
            var previousHadReceipt =
                previous?.ContainsKey(
                    FactionMaterializationContract.PropertyName) == true;
            var touchKind = hasUsablePreTurnAuthority
                ? FactionTouchClassifier.Classify(
                    existedPreTurn: previous != null,
                    hadReceiptPreTurn: previousHadReceipt)
                : target.Faction.ContainsKey(
                    FactionMaterializationContract.PropertyName)
                    ? FactionTouchKind.AlreadyMaterialized
                    : FactionTouchKind.InvalidReceiptless;
            var isCreation = touchKind == FactionTouchKind.New;
            if (touchKind == FactionTouchKind.InvalidReceiptless)
                continue;
            var factionIssueStart = issues.Count;

            var factionElement =
                JsonSerializer.SerializeToElement(target.Faction);
            ValidateShiningFactionMaterializationSemanticCore(
                factionElement,
                target.Context,
                target.FactionId,
                issues);

            var affiliatedResidentIds =
                CollectAffiliatedShiningResidentIds(
                    residentRoot,
                    target.FactionId,
                    issues);
            if (rawBeforeNormalization && isCreation)
            {
                await ValidateNewShiningFactionCreationRouteAuthorityAsync(
                    target,
                    currentRoot,
                    residentRoot,
                    issues);
            }
            ValidateShiningFactionMaterializationReferences(
                target,
                currentRoot,
                residentRoot,
                guardiansRoot,
                afterlifeProfilesRoot,
                sarefStoryRoot,
                isCreation,
                affiliatedResidentIds,
                currentTradeAuthorities,
                issues);

            if (!rawBeforeNormalization || !isCreation)
            {
                ApplyFactionRepairClassification(
                    issues,
                    factionIssueStart,
                    $"shining_faction:{target.FactionId}",
                    touchKind);
                continue;
            }

            var hasTradeContent =
                target.Faction["tradeInventory"] is JsonObject ||
                HasObjectArrayEntries(
                    target.Faction,
                    "tradeInventoryReceipts");
            var canTrade =
                ShiningAbodeState.FactionHasAvailableTrade(
                    target.Faction,
                    ShiningAbodeState.ComputeFactionStrength(
                        target.Faction,
                        residentRoot,
                        radianceTier));
            var evidence = new FactionMaterializationEvidence(
                "shining_faction",
                target.FactionId,
                new Dictionary<string, bool>(
                    StringComparer.Ordinal)
                {
                    ["projects"] = HasObjectArrayEntries(
                        target.Faction,
                        "projects"),
                    ["territorialInfluence"] =
                        HasObjectArrayEntries(
                            target.Faction,
                            "territorialInfluence"),
                    ["resourceLedger"] = HasObjectArrayEntries(
                        target.Faction,
                        "resourceLedger"),
                    ["residentAffiliations"] =
                        affiliatedResidentIds.Count > 0,
                    ["trade"] = hasTradeContent,
                    ["leadershipHistory"] =
                        HasObjectArrayEntries(
                            target.Faction,
                            "leadershipHistory") ||
                        HasObjectArrayEntries(
                            target.Faction,
                            "leadershipReceipts"),
                    ["storyState"] =
                        target.Faction["storyAuthority"] is
                            JsonObject
                },
                new Dictionary<string, bool>(
                    StringComparer.Ordinal)
                {
                    ["runsProjects"] = HasObjectArrayEntries(
                        target.Faction,
                        "projects"),
                    ["holdsTerritorialInfluence"] =
                        HasObjectArrayEntries(
                            target.Faction,
                            "territorialInfluence"),
                    ["usesResourceLedger"] =
                        HasObjectArrayEntries(
                            target.Faction,
                            "resourceLedger"),
                    ["hasResidentAffiliations"] =
                        affiliatedResidentIds.Count > 0,
                    ["canTrade"] = canTrade,
                    ["hasLeadershipHistory"] =
                        HasObjectArrayEntries(
                            target.Faction,
                            "leadershipHistory") ||
                        HasObjectArrayEntries(
                            target.Faction,
                            "leadershipReceipts"),
                    ["usesStoryState"] =
                        target.Faction["storyAuthority"] is
                            JsonObject
                },
                new Dictionary<string, bool>(
                    StringComparer.Ordinal)
                {
                    ["projects"] = HasExactEmptyArray(
                        target.Faction,
                        "projects"),
                    ["territorialInfluence"] =
                        HasExactEmptyArray(
                            target.Faction,
                            "territorialInfluence"),
                    ["resourceLedger"] = HasExactEmptyArray(
                        target.Faction,
                        "resourceLedger"),
                    ["residentAffiliations"] =
                        affiliatedResidentIds.Count == 0,
                    ["trade"] =
                        HasExplicitNull(
                            target.Faction,
                            "tradeInventory") &&
                        HasExactEmptyArray(
                            target.Faction,
                            "tradeInventoryReceipts"),
                    ["leadershipHistory"] =
                        HasExactEmptyArray(
                            target.Faction,
                            "leadershipHistory") &&
                        HasExactEmptyArray(
                            target.Faction,
                            "leadershipReceipts"),
                    ["storyState"] = HasExplicitNull(
                        target.Faction,
                        "storyAuthority")
                },
                touchKind);

            issues.AddRange(
                FactionMaterializationContract.Validate(
                    factionElement,
                    target.Context,
                    FactionMaterializationFamily.Shining,
                    evidence,
                    requireEnvelope: true,
                    deferEvidenceConsistency: false));
            ApplyFactionRepairClassification(
                issues,
                factionIssueStart,
                $"shining_faction:{target.FactionId}",
                touchKind);
        }
    }

    private static JsonObject? ParseShiningMaterializationRoot(
        string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonNode.Parse(json) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static List<ShiningFactionMaterializationTarget>
        ReadShiningMaterializationTargets(JsonObject? root)
    {
        var result =
            new List<ShiningFactionMaterializationTarget>();
        if (root?["factions"] is not JsonArray factions)
            return result;

        for (var index = 0; index < factions.Count; index++)
        {
            if (factions[index] is not JsonObject faction)
                continue;

            var factionId =
                ReadShiningMaterializationString(
                    faction,
                    "factionId");
            if (string.IsNullOrWhiteSpace(factionId))
                continue;

            result.Add(
                new ShiningFactionMaterializationTarget(
                    faction,
                    $"{ShiningAbodeState.StatePath}.factions[{index}]",
                    factionId));
        }

        return result;
    }

    private async Task ValidateNewShiningFactionCreationRouteAuthorityAsync(
        ShiningFactionMaterializationTarget target,
        JsonObject currentRoot,
        JsonObject? residentRoot,
        List<ValidationIssue> issues)
    {
        var provenance =
            target.Faction["creationProvenance"] as JsonObject;
        var route = ReadShiningMaterializationString(
            provenance,
            "route");
        var authorityType = ReadShiningMaterializationString(
            provenance,
            "authorityType");
        var authorityId = ReadShiningMaterializationString(
            provenance,
            "authorityId");

        if (string.Equals(route, "story", StringComparison.Ordinal))
            return;

        if (string.Equals(
                route,
                "native_discovery",
                StringComparison.Ordinal))
        {
            await ValidateNewNativeShiningFactionAuthorityAsync(
                target,
                currentRoot,
                residentRoot,
                authorityType,
                authorityId,
                issues);
            return;
        }

        if (string.Equals(
                route,
                "player_founding",
                StringComparison.Ordinal))
        {
            await ValidateNewPlayerFoundedShiningFactionAuthorityAsync(
                target,
                currentRoot,
                residentRoot,
                authorityType,
                authorityId,
                issues);
        }
    }

    private async Task ValidateNewNativeShiningFactionAuthorityAsync(
        ShiningFactionMaterializationTarget target,
        JsonObject currentRoot,
        JsonObject? residentRoot,
        string? authorityType,
        string? authorityId,
        List<ValidationIssue> issues)
    {
        var requestJson = await ReadPreTurnTrackedFileAsync(
            ShiningCoreActionRequestState.PendingActionsRequestPath);
        var matchingRequests =
            ShiningCoreActionRequestState.ReadRequests(requestJson)
                .Where(request =>
                    string.Equals(
                        request.RequestId,
                        authorityId,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        request.ActionType,
                        ShiningCoreActionRequestState.ActionTypeDiscoverNativeFaction,
                        StringComparison.Ordinal))
                .ToArray();
        var matchingReceipts = FindExactShiningRouteReceipts(
            currentRoot,
            "coreActionReceipts",
            authorityId);
        var request = matchingRequests.Length == 1
            ? matchingRequests[0]
            : null;
        var receipt = matchingReceipts.Length == 1
            ? matchingReceipts[0]
            : null;
        var hallId = ReadShiningMaterializationString(
            target.Faction,
            "hallId");
        IReadOnlySet<string>? residentIds = null;
        IReadOnlySet<string>? projectIds = null;
        var authorityValid =
            string.Equals(
                authorityType,
                "shining_core_action_request",
                StringComparison.Ordinal) &&
            request != null &&
            receipt != null &&
            ShiningCoreActionReceiptMatchesRequest(
                receipt,
                request,
                out _) &&
            string.Equals(
                GetNodeString(receipt["status"]),
                ShiningCoreActionRequestState.RequestStatusAccepted,
                StringComparison.OrdinalIgnoreCase) &&
            string.Equals(
                GetNodeString(receipt["resolvedFactionId"]),
                target.FactionId,
                StringComparison.Ordinal) &&
            string.Equals(
                GetNodeString(receipt["hallId"]),
                hallId,
                StringComparison.Ordinal) &&
            TryReadExactShiningIdentitySet(
                receipt["newResidentIds"],
                out residentIds) &&
            TryReadExactShiningIdentitySet(
                receipt["seededProjectIds"],
                out projectIds) &&
            residentRoot != null;
        if (!authorityValid)
        {
            issues.Add(ShiningRouteAuthorityIssue(
                target,
                "native_discovery",
                authorityType,
                authorityId,
                matchingRequests.Length,
                matchingReceipts.Length,
                ShiningCoreActionRequestState.PendingActionsRequestPath));
            return;
        }

        ValidateShiningRouteMaterialization(
            route: "native_discovery",
            authorityType: "shining_core_action_request",
            authorityId: request!.RequestId,
            expectedFactionId: target.FactionId,
            faction: target.Faction,
            hallId: hallId!,
            residentIds: residentIds!,
            projectIds: projectIds!,
            currentResidentsRoot: residentRoot!,
            issues: issues);
    }

    private async Task ValidateNewPlayerFoundedShiningFactionAuthorityAsync(
        ShiningFactionMaterializationTarget target,
        JsonObject currentRoot,
        JsonObject? residentRoot,
        string? authorityType,
        string? authorityId,
        List<ValidationIssue> issues)
    {
        var requestJson = await ReadPreTurnTrackedFileAsync(
            ShiningFactionRequestState.PendingFoundingsRequestPath);
        var hallId = ReadShiningMaterializationString(
            target.Faction,
            "hallId");
        var matchingRequests =
            ShiningFactionRequestState.ReadFoundingRequests(requestJson)
                .Where(request =>
                    string.Equals(
                        request.RequestId,
                        authorityId,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        request.ProposedFactionId,
                        target.FactionId,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        request.ProposedHallId,
                        hallId,
                        StringComparison.Ordinal))
                .ToArray();
        var matchingReceipts = FindExactShiningRouteReceipts(
            currentRoot,
            "factionFoundingReceipts",
            authorityId);
        var request = matchingRequests.Length == 1
            ? matchingRequests[0]
            : null;
        var receipt = matchingReceipts.Length == 1
            ? matchingReceipts[0]
            : null;
        var authorityValid =
            string.Equals(
                authorityType,
                "shining_founding_request",
                StringComparison.Ordinal) &&
            request != null &&
            receipt != null &&
            ShiningFoundingReceiptMatchesRequest(
                receipt,
                request,
                out _) &&
            string.Equals(
                GetNodeString(receipt["status"]),
                ShiningFactionRequestState.RequestStatusAccepted,
                StringComparison.OrdinalIgnoreCase) &&
            residentRoot != null;
        if (!authorityValid)
        {
            issues.Add(ShiningRouteAuthorityIssue(
                target,
                "player_founding",
                authorityType,
                authorityId,
                matchingRequests.Length,
                matchingReceipts.Length,
                ShiningFactionRequestState.PendingFoundingsRequestPath));
            return;
        }

        ValidateShiningRouteMaterialization(
            route: "player_founding",
            authorityType: "shining_founding_request",
            authorityId: request!.RequestId,
            expectedFactionId: target.FactionId,
            faction: target.Faction,
            hallId: hallId!,
            residentIds: request.SupportingResidentIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id.Trim())
                .ToHashSet(StringComparer.Ordinal),
            projectIds: new HashSet<string>(StringComparer.Ordinal),
            currentResidentsRoot: residentRoot!,
            issues: issues);
    }

    private static JsonObject[] FindExactShiningRouteReceipts(
        JsonObject currentRoot,
        string propertyName,
        string? requestId) =>
        currentRoot[propertyName] is JsonArray receipts &&
        !string.IsNullOrWhiteSpace(requestId)
            ? receipts
                .OfType<JsonObject>()
                .Where(receipt => string.Equals(
                    GetNodeString(receipt["requestId"]),
                    requestId,
                    StringComparison.Ordinal))
                .ToArray()
            : Array.Empty<JsonObject>();

    private static bool TryReadExactShiningIdentitySet(
        JsonNode? node,
        out IReadOnlySet<string>? values)
    {
        values = null;
        if (node is not JsonArray array)
            return false;

        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in array)
        {
            if (item is not JsonValue value ||
                !value.TryGetValue<string>(out var identity) ||
                string.IsNullOrWhiteSpace(identity) ||
                !result.Add(identity.Trim()))
            {
                return false;
            }
        }

        values = result;
        return true;
    }

    private static ValidationIssue ShiningRouteAuthorityIssue(
        ShiningFactionMaterializationTarget target,
        string route,
        string? authorityType,
        string? authorityId,
        int requestMatches,
        int receiptMatches,
        string requestPath) =>
        ShiningFactionMaterializationIssue(
            $"{target.Context}.creationProvenance",
            "faction_materialization_shining_route_authority_invalid",
            target.FactionId,
            "A newly materialized Shining faction must reverse-resolve to one exact accepted creation-route authority.",
            expected:
                $"one {route} request and one accepted matching receipt",
            actual:
                $"{authorityType ?? "missing"}:{authorityId ?? "missing"}; requests={requestMatches}; receipts={receiptMatches}",
            repairTargetFiles:
            [
                ShiningAbodeState.StatePath,
                requestPath,
                GuardianAbodeResidentState.StatePath
            ],
            repairClassification: FactionTouchKind.New);

    private static Dictionary<string, JsonObject>
        ReadShiningMaterializationTargetMap(JsonObject? root)
    {
        var result = new Dictionary<string, JsonObject>(
            StringComparer.Ordinal);
        foreach (var target in
                 ReadShiningMaterializationTargets(root))
        {
            result.TryAdd(target.FactionId, target.Faction);
        }

        return result;
    }

    private static HashSet<string>
        CollectAffiliatedShiningResidentIds(
            JsonObject? residentRoot,
            string factionId,
            List<ValidationIssue>? issues = null)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        if (residentRoot?["entries"] is not JsonArray residents)
            return result;

        var sourceResidentIdentities =
            new Dictionary<string, bool>(StringComparer.Ordinal);
        for (var index = 0; index < residents.Count; index++)
        {
            if (residents[index] is not JsonObject resident)
                continue;

            var linkedToTarget =
                string.Equals(
                    ReadShiningMaterializationString(
                        resident,
                        "shiningFactionId"),
                    factionId,
                    StringComparison.Ordinal);
            var residentId =
                ReadShiningMaterializationString(
                    resident,
                    "residentId");
            if (string.IsNullOrWhiteSpace(residentId))
            {
                if (linkedToTarget)
                {
                    issues?.Add(
                        ShiningFactionMaterializationIssue(
                            $"{GuardianAbodeResidentState.StatePath}.entries[{index}]",
                            "faction_materialization_shining_actor_profile_invalid",
                            factionId,
                            "A linked Shining resident requires a usable exact residentId.",
                            expected: "non-empty residentId",
                            actual: resident.ToJsonString()));
                }

                continue;
            }

            if (!sourceResidentIdentities.TryAdd(
                    residentId,
                    linkedToTarget))
            {
                var priorLinkedToTarget =
                    sourceResidentIdentities[residentId];
                if (linkedToTarget || priorLinkedToTarget)
                {
                    issues?.Add(
                        ShiningFactionMaterializationIssue(
                            $"{GuardianAbodeResidentState.StatePath}.entries[{index}]",
                            "faction_materialization_shining_actor_profile_invalid",
                            factionId,
                            "Linked Shining resident identities must be unique within the resident source.",
                            expected:
                                "one source entry per residentId",
                            actual: residentId));
                }

                if (linkedToTarget && !priorLinkedToTarget)
                    sourceResidentIdentities[residentId] = true;
            }

            if (linkedToTarget)
                result.Add(residentId);
        }

        return result;
    }

    private void ValidateShiningRouteMaterialization(
        string route,
        string authorityType,
        string authorityId,
        string expectedFactionId,
        JsonObject? faction,
        string hallId,
        IReadOnlySet<string> residentIds,
        IReadOnlySet<string> projectIds,
        JsonObject currentResidentsRoot,
        List<ValidationIssue> issues)
    {
        if (faction == null)
            return;

        var factionId =
            ReadShiningMaterializationString(faction, "factionId");
        var actualHallId =
            ReadShiningMaterializationString(faction, "hallId");
        var issueFactionId = factionId ?? expectedFactionId;
        var context =
            $"{ShiningAbodeState.StatePath}.factions[{issueFactionId}]";
        if (!string.Equals(
                factionId,
                expectedFactionId,
                StringComparison.Ordinal) ||
            !string.Equals(
                actualHallId,
                hallId,
                StringComparison.Ordinal))
        {
            issues.Add(
                ShiningFactionMaterializationIssue(
                    $"{context}.factionId",
                    "faction_materialization_shining_route_identity_invalid",
                    issueFactionId,
                    "Shining creation route must materialize the exact resolved faction and hall identities.",
                    expected:
                        $"{expectedFactionId} / hall {hallId}",
                    actual:
                        $"{factionId ?? "missing"} / hall {actualHallId ?? "missing"}",
                    repairClassification: FactionTouchKind.New));
        }

        if (string.IsNullOrWhiteSpace(factionId))
            return;

        var actualProjectIds =
            new HashSet<string>(StringComparer.Ordinal);
        var projects = faction["projects"] as JsonArray;
        var projectShapeValid = projects != null;
        if (projects != null)
        {
            foreach (var projectNode in projects)
            {
                if (projectNode is not JsonObject project)
                {
                    projectShapeValid = false;
                    continue;
                }

                var projectId =
                    ReadShiningMaterializationString(
                        project,
                        "projectId");
                if (projectId == null ||
                    !actualProjectIds.Add(projectId))
                {
                    projectShapeValid = false;
                }
            }
        }

        if (!projectShapeValid ||
            !actualProjectIds.SetEquals(projectIds))
        {
            var actualProjectSet =
                string.Join(
                    ",",
                    actualProjectIds.OrderBy(
                        value => value,
                        StringComparer.Ordinal));
            issues.Add(
                ShiningFactionMaterializationIssue(
                    $"{context}.projects",
                    "faction_materialization_shining_route_project_set_invalid",
                    factionId,
                    "Shining creation route projects must match the exact resolved project set.",
                    expected:
                        string.Join(
                            ",",
                            projectIds.OrderBy(
                                value => value,
                                StringComparer.Ordinal)),
                    actual:
                        projectShapeValid
                            ? actualProjectSet
                            : $"malformed or duplicate projectId entries; ids={actualProjectSet}",
                    repairClassification: FactionTouchKind.New));
        }

        var provenance = faction["creationProvenance"] as JsonObject;
        if (provenance == null ||
            !string.Equals(
                ReadShiningMaterializationString(provenance, "route"),
                route,
                StringComparison.Ordinal) ||
            !string.Equals(
                ReadShiningMaterializationString(
                    provenance,
                    "authorityType"),
                authorityType,
                StringComparison.Ordinal) ||
            !string.Equals(
                ReadShiningMaterializationString(
                    provenance,
                    "authorityId"),
                authorityId,
                StringComparison.Ordinal))
        {
            issues.Add(
                ShiningFactionMaterializationIssue(
                    $"{context}.creationProvenance",
                    "faction_materialization_shining_route_provenance_invalid",
                    factionId,
                    "Shining faction creation provenance must bind to the exact resolved route authority.",
                    expected:
                        $"{route} / {authorityType} / {authorityId}",
                    actual:
                        $"{ReadShiningMaterializationString(provenance, "route") ?? "missing"} / " +
                        $"{ReadShiningMaterializationString(provenance, "authorityType") ?? "missing"} / " +
                        $"{ReadShiningMaterializationString(provenance, "authorityId") ?? "missing"}",
                    repairClassification: FactionTouchKind.New));
        }

        var actualResidentIds =
            CollectAffiliatedShiningResidentIds(
                currentResidentsRoot,
                factionId);
        if (!actualResidentIds.SetEquals(residentIds))
        {
            issues.Add(
                ShiningFactionMaterializationIssue(
                    $"{context}.materialization.sections.residentAffiliations",
                    "faction_materialization_shining_route_resident_affiliation_invalid",
                    factionId,
                    "Shining creation route resident affiliations must match the exact resolved resident set.",
                    expected:
                        string.Join(
                            ",",
                            residentIds.OrderBy(
                                value => value,
                                StringComparer.Ordinal)),
                    actual:
                        string.Join(
                            ",",
                            actualResidentIds.OrderBy(
                                value => value,
                                StringComparer.Ordinal)),
                    repairClassification: FactionTouchKind.New));
        }

        if (string.Equals(route, "player_founding", StringComparison.Ordinal))
        {
            var historyMatches =
                faction["leadershipHistory"] is JsonArray history &&
                history
                    .OfType<JsonObject>()
                    .Count(entry =>
                        string.Equals(
                            ReadShiningMaterializationString(
                                entry,
                                "requestId"),
                            authorityId,
                            StringComparison.Ordinal) &&
                        string.Equals(
                            ReadShiningMaterializationString(
                                entry,
                                "eventType"),
                            "founded",
                            StringComparison.Ordinal)) == 1;
            if (!historyMatches)
            {
                issues.Add(
                    ShiningFactionMaterializationIssue(
                        $"{context}.leadershipHistory",
                        "faction_materialization_shining_route_history_invalid",
                        factionId,
                        "Player-founded Shining faction must record one exact founding leadership history entry.",
                        expected:
                            $"{authorityId} / founded",
                        actual:
                            faction["leadershipHistory"]?.ToJsonString() ??
                            "missing",
                        repairClassification: FactionTouchKind.New));
            }
        }
    }

    private void ValidateShiningFactionMaterializationReferences(
        ShiningFactionMaterializationTarget target,
        JsonObject currentRoot,
        JsonObject? residentRoot,
        JsonObject? guardiansRoot,
        JsonObject? afterlifeProfilesRoot,
        JsonObject? sarefStoryRoot,
        bool isCreation,
        IReadOnlySet<string> affiliatedResidentIds,
        IReadOnlySet<string> currentTradeAuthorities,
        List<ValidationIssue> issues)
    {
        var hallId = ReadShiningMaterializationString(
            target.Faction,
            "hallId");
        if (!string.IsNullOrWhiteSpace(hallId))
        {
            var hallMatches =
                currentRoot["halls"] is JsonArray halls
                    ? halls
                        .OfType<JsonObject>()
                        .Count(hall => string.Equals(
                            ReadShiningMaterializationString(
                                hall,
                                "hallId"),
                            hallId,
                            StringComparison.Ordinal))
                    : 0;
            if (hallMatches != 1)
            {
                issues.Add(
                    ShiningFactionMaterializationIssue(
                        $"{target.Context}.hallId",
                        "faction_materialization_shining_hall_reference_invalid",
                        target.FactionId,
                        "A materialized Shining faction must resolve one exact hallId.",
                        expected: "exactly one matching halls[].hallId",
                        actual: hallMatches.ToString()));
            }
        }

        var requiredActors =
            new Dictionary<string, AfterlifeActorBinding>(
                StringComparer.Ordinal);
        var route = ReadShiningMaterializationString(
            target.Faction["creationProvenance"] as JsonObject,
            "route");
        if (string.Equals(
                route,
                "story",
                StringComparison.Ordinal) ||
            target.Faction["storyAuthority"] is JsonObject)
        {
            ValidateShiningStoryAuthorityReference(
                target,
                guardiansRoot,
                sarefStoryRoot,
                issues);
            AddShiningGuardianStoryAuthorityActor(
                target,
                guardiansRoot,
                requiredActors);
        }

        AddShiningHeadActor(
            target,
            currentRoot,
            residentRoot,
            guardiansRoot,
            requiredActors,
            issues);

        if (isCreation)
        {
            foreach (var residentId in affiliatedResidentIds)
            {
                AddRequiredShiningActor(
                    requiredActors,
                    ShiningAbodeState.HeadActorTypeResident,
                    residentId,
                    $"{GuardianAbodeResidentState.StatePath}.entries[{residentId}]");
            }

            AddNewlySignificantShiningPoliticalActors(
                target,
                currentRoot,
                requiredActors,
                issues);
        }

        foreach (var actor in requiredActors.Values)
        {
            ValidateRequiredShiningActorMaterializationProfile(
                afterlifeProfilesRoot,
                actor,
                target,
                currentTradeAuthorities,
                issues);
        }
    }

    private static void AddShiningHeadActor(
        ShiningFactionMaterializationTarget target,
        JsonObject currentRoot,
        JsonObject? residentRoot,
        JsonObject? guardiansRoot,
        Dictionary<string, AfterlifeActorBinding> requiredActors,
        List<ValidationIssue> issues)
    {
        if (target.Faction["leadership"] is not
            JsonObject leadership)
        {
            return;
        }

        var leadershipState =
            ReadShiningMaterializationString(
                leadership,
                "leadershipState");
        if (string.Equals(
                leadershipState,
                ShiningAbodeState.LeadershipStateVacant,
                StringComparison.Ordinal))
        {
            if (leadership.ContainsKey("headActorType") &&
                leadership["headActorType"] is null &&
                leadership.ContainsKey("headActorId") &&
                leadership["headActorId"] is null)
            {
                return;
            }

            issues.Add(
                ShiningFactionMaterializationIssue(
                    $"{target.Context}.leadership",
                    "faction_materialization_shining_leadership_reference_invalid",
                    target.FactionId,
                    "Vacant Shining leadership skips Actor Materialization only with exact null head fields.",
                    expected:
                        "leadershipState=vacant, headActorType=null, headActorId=null",
                    actual: leadership.ToJsonString()));
            return;
        }

        var headActorType =
            ReadShiningMaterializationString(
                leadership,
                "headActorType");
        var headActorId =
            ReadShiningMaterializationString(
                leadership,
                "headActorId");
        if (string.IsNullOrWhiteSpace(headActorType) ||
            string.IsNullOrWhiteSpace(headActorId))
        {
            issues.Add(
                ShiningFactionMaterializationIssue(
                    $"{target.Context}.leadership",
                    "faction_materialization_shining_leadership_reference_invalid",
                    target.FactionId,
                    "A non-vacant Shining faction head requires exact actor type and ID.",
                    expected:
                        "non-empty headActorType and headActorId",
                    actual: leadership.ToJsonString()));
            return;
        }

        if (string.Equals(
                headActorType,
                ShiningAbodeState.HeadActorTypePlayerSoul,
                StringComparison.Ordinal) &&
            string.Equals(
                headActorId,
                ShiningAbodeState.HeadActorTypePlayerSoul,
                StringComparison.Ordinal))
        {
            return;
        }

        var bindingValid = headActorType switch
        {
            ShiningAbodeState.HeadActorTypeResident =>
                HasExactResidentLeadershipBinding(
                    residentRoot,
                    headActorId,
                    target.FactionId),
            ShiningAbodeState.HeadActorTypeRadiantActor =>
                HasExactPoliticalActorLeadershipBinding(
                    currentRoot,
                    headActorId,
                    target.FactionId),
            ShiningAbodeState.HeadActorTypeGuardian =>
                HasExactGuardianIdentity(
                    guardiansRoot,
                    headActorId),
            _ => false
        };
        if (!bindingValid)
        {
            issues.Add(
                ShiningFactionMaterializationIssue(
                    $"{target.Context}.leadership.headActorId",
                    "faction_materialization_shining_leadership_reference_invalid",
                    target.FactionId,
                    "A non-player Shining faction head must resolve through exact current actor authority.",
                    expected:
                        $"{headActorType}:{headActorId} bound to {target.FactionId}",
                    actual: "unresolved or mismatched"));
            return;
        }

        AddRequiredShiningActor(
            requiredActors,
            headActorType,
            headActorId,
            $"{target.Context}.leadership");
    }

    private static bool HasExactResidentLeadershipBinding(
        JsonObject? residentRoot,
        string actorId,
        string factionId) =>
        residentRoot?["entries"] is JsonArray residents &&
        residents.OfType<JsonObject>().Count(resident =>
            string.Equals(
                ReadShiningMaterializationString(
                    resident,
                    "residentId"),
                actorId,
                StringComparison.Ordinal) &&
            string.Equals(
                ReadShiningMaterializationString(
                    resident,
                    "shiningFactionId"),
                factionId,
                StringComparison.Ordinal)) == 1;

    private static bool HasExactPoliticalActorLeadershipBinding(
        JsonObject currentRoot,
        string actorId,
        string factionId) =>
        currentRoot["shiningPoliticalActors"] is JsonArray actors &&
        actors.OfType<JsonObject>().Count(actor =>
            string.Equals(
                ReadShiningMaterializationString(
                    actor,
                    "actorId"),
                actorId,
                StringComparison.Ordinal) &&
            string.Equals(
                ReadShiningMaterializationString(
                    actor,
                    "currentFactionId"),
                factionId,
                StringComparison.Ordinal)) == 1;

    private static bool HasExactGuardianIdentity(
        JsonObject? guardiansRoot,
        string guardianId) =>
        TryResolveUniqueGuardianIdentity(
            guardiansRoot,
            guardianId,
            out _);

    private static bool TryResolveUniqueGuardianIdentity(
        JsonObject? guardiansRoot,
        string guardianId,
        out JsonObject? guardian)
    {
        guardian = null;
        var activeMatch =
            guardiansRoot?["activeGuardian"] is JsonObject active &&
            string.Equals(
                ReadShiningMaterializationString(
                    active,
                    "guardianId"),
                guardianId,
                StringComparison.Ordinal)
                ? active
                : null;
        var guardianMatches =
            guardiansRoot?["guardians"] is JsonArray guardians
                ? guardians
                    .OfType<JsonObject>()
                    .Where(candidate => string.Equals(
                        ReadShiningMaterializationString(
                            candidate,
                            "guardianId"),
                        guardianId,
                        StringComparison.Ordinal))
                    .ToArray()
                : Array.Empty<JsonObject>();
        if (guardianMatches.Length > 1)
        {
            return false;
        }

        guardian = guardianMatches.SingleOrDefault() ?? activeMatch;
        return guardian != null;
    }

    private static void AddShiningGuardianStoryAuthorityActor(
        ShiningFactionMaterializationTarget target,
        JsonObject? guardiansRoot,
        Dictionary<string, AfterlifeActorBinding> requiredActors)
    {
        if (target.Faction["storyAuthority"] is not
                JsonObject storyAuthority ||
            !string.Equals(
                ReadShiningMaterializationString(
                    storyAuthority,
                    "authorityType"),
                "guardian_ascension",
                StringComparison.Ordinal))
        {
            return;
        }

        var authorityId =
            ReadShiningMaterializationString(
                storyAuthority,
                "authorityId");
        if (authorityId == null ||
            !TryResolveUniqueGuardianIdentity(
                guardiansRoot,
                authorityId,
                out _))
        {
            return;
        }

        AddRequiredShiningActor(
            requiredActors,
            ShiningAbodeState.HeadActorTypeGuardian,
            authorityId,
            $"{target.Context}.storyAuthority");
    }

    private static void AddNewlySignificantShiningPoliticalActors(
        ShiningFactionMaterializationTarget target,
        JsonObject currentRoot,
        Dictionary<string, AfterlifeActorBinding> requiredActors,
        List<ValidationIssue> issues)
    {
        if (currentRoot["shiningPoliticalActors"] is not
            JsonArray actors)
        {
            return;
        }

        var sourceActorIdentities =
            new Dictionary<string, bool>(StringComparer.Ordinal);
        for (var index = 0; index < actors.Count; index++)
        {
            if (actors[index] is not JsonObject actor)
                continue;

            var linkedToTarget =
                string.Equals(
                    ReadShiningMaterializationString(
                        actor,
                        "currentFactionId"),
                    target.FactionId,
                    StringComparison.Ordinal);

            var actorId =
                ReadShiningMaterializationString(
                    actor,
                    "actorId");
            var actorType =
                ReadShiningMaterializationString(
                    actor,
                    "actorType");
            if (actorId == null ||
                !string.Equals(
                    actorType,
                    ShiningAbodeState.HeadActorTypeRadiantActor,
                    StringComparison.Ordinal))
            {
                if (linkedToTarget)
                {
                    issues.Add(
                        ShiningFactionMaterializationIssue(
                            $"{ShiningAbodeState.StatePath}.shiningPoliticalActors[{index}]",
                            "faction_materialization_shining_actor_profile_invalid",
                            target.FactionId,
                            "A newly significant political actor requires exact radiant_actor identity.",
                            expected:
                                "actorType=radiant_actor and non-empty actorId",
                            actual: actor.ToJsonString()));
                }

                continue;
            }

            var sourceIdentity =
                BuildAfterlifeActorIdentityKey(
                    ShiningAbodeState.HeadActorTypeRadiantActor,
                    actorId);
            if (!sourceActorIdentities.TryAdd(
                    sourceIdentity,
                    linkedToTarget))
            {
                var priorLinkedToTarget =
                    sourceActorIdentities[sourceIdentity];
                if (linkedToTarget || priorLinkedToTarget)
                {
                    issues.Add(
                        ShiningFactionMaterializationIssue(
                            $"{ShiningAbodeState.StatePath}.shiningPoliticalActors[{index}]",
                            "faction_materialization_shining_actor_profile_invalid",
                            target.FactionId,
                            "Linked Shining political actor identities must be unique within the political source.",
                            expected:
                                "one source entry per radiant_actor identity",
                            actual: $"{actorType}:{actorId}"));
                }

                if (linkedToTarget && !priorLinkedToTarget)
                    sourceActorIdentities[sourceIdentity] = true;
            }

            if (linkedToTarget)
            {
                AddRequiredShiningActor(
                    requiredActors,
                    ShiningAbodeState.HeadActorTypeRadiantActor,
                    actorId,
                    $"{ShiningAbodeState.StatePath}.shiningPoliticalActors[{index}]");
            }
        }
    }

    private static void AddRequiredShiningActor(
        Dictionary<string, AfterlifeActorBinding> requiredActors,
        string actorType,
        string actorId,
        string context)
    {
        var binding = new AfterlifeActorBinding(
            actorType,
            actorId,
            context,
            HasTypeSpecificMemory: false);
        requiredActors.TryAdd(binding.IdentityKey, binding);
    }

    private static void ValidateRequiredShiningActorMaterializationProfile(
        JsonObject? profilesRoot,
        AfterlifeActorBinding actor,
        ShiningFactionMaterializationTarget target,
        IReadOnlySet<string> currentTradeAuthorities,
        List<ValidationIssue> issues)
    {
        var profiles =
            profilesRoot?[AfterlifeEntityProfileState.ProfilesProperty]
                is JsonArray profileArray
                ? profileArray
                    .Select((node, index) =>
                        (Profile: node as JsonObject, Index: index))
                    .Where(candidate =>
                        candidate.Profile != null &&
                        string.Equals(
                            ReadShiningMaterializationString(
                                candidate.Profile,
                                "actorId"),
                            actor.ActorId,
                            StringComparison.Ordinal) &&
                        string.Equals(
                            ReadShiningMaterializationString(
                                candidate.Profile,
                                "actorType"),
                            actor.ActorType,
                            StringComparison.Ordinal))
                    .ToArray()
                : Array.Empty<(JsonObject? Profile, int Index)>();
        if (profiles.Length != 1)
        {
            issues.Add(
                ShiningFactionMaterializationIssue(
                    actor.Context,
                    "faction_materialization_shining_actor_profile_invalid",
                    target.FactionId,
                    "A required Shining actor needs one exact afterlife Actor Materialization profile.",
                    expected:
                        $"one {actor.ActorType}:{actor.ActorId} profile",
                    actual:
                        $"{profiles.Length} exact-pair candidates",
                    repairTargetFiles:
                    [
                        AfterlifeEntityProfileState.StatePath
                    ]));
            return;
        }

        var profile = profiles[0];
        var profileElement =
            JsonSerializer.SerializeToElement(profile.Profile);
        var profileContext =
            $"{AfterlifeEntityProfileState.StatePath}.{AfterlifeEntityProfileState.ProfilesProperty}[{profile.Index}]";
        issues.AddRange(
            ActorMaterializationContract.ValidateAfterlifeProfile(
                profileElement,
                profileContext,
                requireEnvelope: true,
                canTradeEvidence:
                    HasCurrentAfterlifeActorTradeAuthority(
                        profileElement,
                        currentTradeAuthorities)));
    }

    private static void ValidateShiningStoryAuthorityReference(
        ShiningFactionMaterializationTarget target,
        JsonObject? guardiansRoot,
        JsonObject? sarefStoryRoot,
        List<ValidationIssue> issues)
    {
        if (TryResolveShiningStoryAuthority(
                target.Faction,
                sarefStoryRoot,
                guardiansRoot,
                out var actual))
        {
            return;
        }

        var storyAuthority =
            target.Faction["storyAuthority"] as JsonObject;
        var authorityType =
            ReadShiningMaterializationString(
                storyAuthority,
                "authorityType");
        var authorityId =
            ReadShiningMaterializationString(
                storyAuthority,
                "authorityId");

        issues.Add(
            ShiningFactionMaterializationIssue(
                $"{target.Context}.storyAuthority",
                "faction_materialization_shining_story_authority_reference_invalid",
                target.FactionId,
                "Story-created Shining faction authority must resolve through the closed canonical story registry.",
                expected:
                    "matching saref_main_story or guardian_ascension authority",
                actual:
                    $"{authorityType ?? "missing"}:{authorityId ?? "missing"} ({actual})"));
    }

    private static bool TryResolveShiningStoryAuthority(
        JsonObject shiningFaction,
        JsonObject? sarefRoot,
        JsonObject? guardiansRoot,
        out string actual)
    {
        actual = "missing or mismatched story authority";
        if (shiningFaction["storyAuthority"] is not
            JsonObject authority)
        {
            return false;
        }

        var authorityType =
            ReadShiningMaterializationString(
                authority,
                "authorityType");
        var authorityId =
            ReadShiningMaterializationString(
                authority,
                "authorityId");
        var factionRole =
            ReadShiningMaterializationString(
                authority,
                "factionRole");
        var factionId =
            ReadShiningMaterializationString(
                shiningFaction,
                "factionId");
        if (shiningFaction["creationProvenance"] is not
                JsonObject provenance ||
            !string.Equals(
                ReadShiningMaterializationString(
                    provenance,
                    "route"),
                "story",
                StringComparison.Ordinal) ||
            !string.Equals(
                ReadShiningMaterializationString(
                    provenance,
                    "authorityType"),
                authorityType,
                StringComparison.Ordinal) ||
            !string.Equals(
                ReadShiningMaterializationString(
                    provenance,
                    "authorityId"),
                authorityId,
                StringComparison.Ordinal))
        {
            return false;
        }

        if (string.Equals(
                authorityType,
                "saref_main_story",
                StringComparison.Ordinal))
        {
            var valid = SarefStoryAuthorizesFaction(
                sarefRoot,
                authorityId,
                factionId,
                factionRole,
                ReadShiningMaterializationString(
                    shiningFaction,
                    "visibility"),
                ReadShiningMaterializationString(
                    shiningFaction,
                    "sarefFactionRole"),
                ReadShiningMaterializationString(
                    shiningFaction,
                    "sarefVisibility"));
            actual = valid
                ? "matched saref_main_story"
                : actual;
            return valid;
        }

        if (string.Equals(
                authorityType,
                "guardian_ascension",
                StringComparison.Ordinal))
        {
            var valid = GuardianAscensionAuthorizesFaction(
                guardiansRoot,
                authorityId,
                factionId,
                factionRole,
                ReadShiningMaterializationString(
                    shiningFaction,
                    "originType"),
                ReadShiningMaterializationString(
                    shiningFaction,
                    "visibility"),
                shiningFaction["leadership"] as JsonObject);
            actual = valid
                ? "matched guardian_ascension"
                : actual;
            return valid;
        }

        return false;
    }

    private static bool SarefStoryAuthorizesFaction(
        JsonObject? sarefRoot,
        string? authorityId,
        string? factionId,
        string? factionRole,
        string? visibility,
        string? sarefFactionRole,
        string? sarefVisibility)
    {
        var factionLinks =
            sarefRoot?["factionLinks"] as JsonObject;
        return authorityId != null &&
               factionId != null &&
               visibility != null &&
               string.Equals(
                   authorityId,
                   factionId,
                   StringComparison.Ordinal) &&
               string.Equals(
                   ReadShiningMaterializationString(
                       factionLinks,
                       "wingsFactionId"),
                   factionId,
                   StringComparison.Ordinal) &&
               string.Equals(
                   ReadShiningMaterializationString(
                       factionLinks,
                       "visibility"),
                   visibility,
                   StringComparison.Ordinal) &&
               string.Equals(
                   factionRole,
                   "wings_of_angels",
                   StringComparison.Ordinal) &&
               string.Equals(
                   sarefFactionRole,
                   "wings_of_angels",
                   StringComparison.Ordinal) &&
               string.Equals(
                   sarefVisibility,
                   visibility,
                   StringComparison.Ordinal);
    }

    private static bool GuardianAscensionAuthorizesFaction(
        JsonObject? guardiansRoot,
        string? authorityId,
        string? factionId,
        string? factionRole,
        string? originType,
        string? visibility,
        JsonObject? leadership) =>
        authorityId != null &&
        factionId != null &&
        TryResolveUniqueGuardianIdentity(
            guardiansRoot,
            authorityId,
            out _) &&
        string.Equals(
            originType,
            ShiningAbodeState.OriginTypeAscendedGuardian,
            StringComparison.Ordinal) &&
        string.Equals(
            factionRole,
            "patron_guardian",
            StringComparison.Ordinal) &&
        string.Equals(
            visibility,
            "revealed",
            StringComparison.Ordinal) &&
        string.Equals(
            ReadShiningMaterializationString(
                leadership,
                "leadershipState"),
            ShiningAbodeState.LeadershipStateSecure,
            StringComparison.Ordinal) &&
        string.Equals(
            ReadShiningMaterializationString(
                leadership,
                "headActorType"),
            ShiningAbodeState.HeadActorTypeGuardian,
            StringComparison.Ordinal) &&
        string.Equals(
            ReadShiningMaterializationString(
                leadership,
                "headActorId"),
            authorityId,
            StringComparison.Ordinal);

    private static string? ReadShiningMaterializationString(
        JsonObject? owner,
        string propertyName)
    {
        if (owner?[propertyName] is not JsonValue value ||
            !value.TryGetValue<string>(out var text) ||
            string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return text;
    }

    private static bool HasObjectArrayEntries(
        JsonObject owner,
        string propertyName) =>
        owner[propertyName] is JsonArray array &&
        array.OfType<JsonObject>().Any();

    private static bool HasExactEmptyArray(
        JsonObject owner,
        string propertyName) =>
        owner.ContainsKey(propertyName) &&
        owner[propertyName] is JsonArray { Count: 0 };

    private static bool HasExplicitNull(
        JsonObject owner,
        string propertyName) =>
        owner.ContainsKey(propertyName) &&
        owner[propertyName] is null;

    private sealed record ShiningFactionMaterializationTarget(
        JsonObject Faction,
        string Context,
        string FactionId);
}
