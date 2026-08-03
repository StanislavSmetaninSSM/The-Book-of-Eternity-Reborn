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

        foreach (var target in currentFactions)
        {
            preTurnFactions.TryGetValue(
                target.FactionId,
                out var previous);
            var currentHasReceipt =
                target.Faction.ContainsKey(
                    FactionMaterializationContract.PropertyName);
            var previousHadReceipt =
                previous?.ContainsKey(
                    FactionMaterializationContract.PropertyName) == true;
            var exactEquality =
                previous != null &&
                JsonNode.DeepEquals(target.Faction, previous);
            var derivedEquality =
                previous != null &&
                ShiningFactionNodesEqualIgnoringDerivedFields(
                    target.Faction,
                    previous);
            var touchKind = hasUsablePreTurnAuthority
                ? FactionTouchClassifier.Classify(
                    existedPreTurn: previous != null,
                    hadReceiptPreTurn: previousHadReceipt,
                    gmAuthoredTouch:
                        previous == null || !derivedEquality,
                    clientDerivedOnly:
                        !exactEquality && derivedEquality)
                : currentHasReceipt
                    ? FactionTouchKind.AlreadyMaterialized
                    : FactionTouchKind.UntouchedLegacy;
            var isCreationOrPromotion = touchKind is
                FactionTouchKind.New or
                FactionTouchKind.LegacyPromotion;
            if (!currentHasReceipt && !isCreationOrPromotion)
                continue;

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
                    target.FactionId);
            ValidateShiningFactionMaterializationReferences(
                target,
                currentRoot,
                residentRoot,
                guardiansRoot,
                afterlifeProfilesRoot,
                sarefStoryRoot,
                issues);

            if (!rawBeforeNormalization || !isCreationOrPromotion)
                continue;

            var hasTradeContent =
                target.Faction["tradeInventory"] is JsonObject ||
                HasObjectArrayEntries(
                    target.Faction,
                    "tradeInventoryReceipts");
            var canTrade =
                ShiningAbodeState.FactionHasAvailableTrade(
                    target.Faction);
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
                });

            issues.AddRange(
                FactionMaterializationContract.Validate(
                    factionElement,
                    target.Context,
                    FactionMaterializationFamily.Shining,
                    evidence,
                    requireEnvelope: true,
                    deferEvidenceConsistency: false));
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

    private static bool
        ShiningFactionNodesEqualIgnoringDerivedFields(
            JsonObject current,
            JsonObject previous)
    {
        var currentClone = current.DeepClone().AsObject();
        var previousClone = previous.DeepClone().AsObject();
        currentClone.Remove("factionStrength");
        previousClone.Remove("factionStrength");
        return JsonNode.DeepEquals(currentClone, previousClone);
    }

    private static HashSet<string>
        CollectAffiliatedShiningResidentIds(
            JsonObject? residentRoot,
            string factionId)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        if (residentRoot?["entries"] is not JsonArray residents)
            return result;

        foreach (var resident in residents.OfType<JsonObject>())
        {
            if (!string.Equals(
                    ReadShiningMaterializationString(
                        resident,
                        "shiningFactionId"),
                    factionId,
                    StringComparison.Ordinal))
            {
                continue;
            }

            var residentId =
                ReadShiningMaterializationString(
                    resident,
                    "residentId");
            if (!string.IsNullOrWhiteSpace(residentId))
                result.Add(residentId);
        }

        return result;
    }

    private void ValidateShiningFactionMaterializationReferences(
        ShiningFactionMaterializationTarget target,
        JsonObject currentRoot,
        JsonObject? residentRoot,
        JsonObject? guardiansRoot,
        JsonObject? afterlifeProfilesRoot,
        JsonObject? sarefStoryRoot,
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

        if (target.Faction["storyAuthority"] is
            JsonObject storyAuthority)
        {
            ValidateShiningStoryAuthorityReference(
                target,
                storyAuthority,
                guardiansRoot,
                sarefStoryRoot,
                issues);
        }

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
            return;
        }

        if (string.Equals(
                headActorType,
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

        ValidateShiningHeadActorMaterializationProfile(
            afterlifeProfilesRoot,
            headActorId,
            target,
            issues);
    }

    private static bool HasExactResidentLeadershipBinding(
        JsonObject? residentRoot,
        string actorId,
        string factionId) =>
        residentRoot?["entries"] is JsonArray residents &&
        residents.OfType<JsonObject>().Any(resident =>
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
                StringComparison.Ordinal));

    private static bool HasExactPoliticalActorLeadershipBinding(
        JsonObject currentRoot,
        string actorId,
        string factionId) =>
        currentRoot["shiningPoliticalActors"] is JsonArray actors &&
        actors.OfType<JsonObject>().Any(actor =>
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
                StringComparison.Ordinal));

    private static bool HasExactGuardianIdentity(
        JsonObject? guardiansRoot,
        string guardianId)
    {
        if (guardiansRoot == null)
            return false;

        if (guardiansRoot["activeGuardian"] is JsonObject active &&
            string.Equals(
                ReadShiningMaterializationString(
                    active,
                    "guardianId"),
                guardianId,
                StringComparison.Ordinal))
        {
            return true;
        }

        return guardiansRoot["guardians"] is JsonArray guardians &&
               guardians.OfType<JsonObject>().Any(guardian =>
                   string.Equals(
                       ReadShiningMaterializationString(
                           guardian,
                           "guardianId"),
                       guardianId,
                       StringComparison.Ordinal));
    }

    private static void ValidateShiningHeadActorMaterializationProfile(
        JsonObject? profilesRoot,
        string actorId,
        ShiningFactionMaterializationTarget target,
        List<ValidationIssue> issues)
    {
        var profiles =
            profilesRoot?[AfterlifeEntityProfileState.ProfilesProperty]
                is JsonArray profileArray
                ? profileArray
                    .OfType<JsonObject>()
                    .Where(profile => string.Equals(
                        ReadShiningMaterializationString(
                            profile,
                            "actorId"),
                        actorId,
                        StringComparison.Ordinal))
                    .ToArray()
                : Array.Empty<JsonObject>();
        if (profiles.Length != 1)
        {
            issues.Add(
                ShiningFactionMaterializationIssue(
                    $"{target.Context}.leadership.headActorId",
                    "faction_materialization_shining_actor_profile_invalid",
                    target.FactionId,
                    "A non-player Shining faction head requires one exact afterlife Actor Materialization profile.",
                    expected: $"one profile for actorId={actorId}",
                    actual: profiles.Length.ToString()));
            return;
        }

        var profileContext =
            $"{AfterlifeEntityProfileState.StatePath}.{AfterlifeEntityProfileState.ProfilesProperty}";
        issues.AddRange(
            ActorMaterializationContract.ValidateAfterlifeProfile(
                JsonSerializer.SerializeToElement(profiles[0]),
                profileContext,
                requireEnvelope: true,
                canTradeEvidence: null));
    }

    private static void ValidateShiningStoryAuthorityReference(
        ShiningFactionMaterializationTarget target,
        JsonObject storyAuthority,
        JsonObject? guardiansRoot,
        JsonObject? sarefStoryRoot,
        List<ValidationIssue> issues)
    {
        var authorityType =
            ReadShiningMaterializationString(
                storyAuthority,
                "authorityType");
        var authorityId =
            ReadShiningMaterializationString(
                storyAuthority,
                "authorityId");
        var role = ReadShiningMaterializationString(
            storyAuthority,
            "factionRole");
        var visibility =
            ReadShiningMaterializationString(
                target.Faction,
                "visibility");
        var valid = authorityType switch
        {
            "guardian_ascension" =>
                !string.IsNullOrWhiteSpace(authorityId) &&
                HasExactGuardianIdentity(
                    guardiansRoot,
                    authorityId) &&
                string.Equals(
                    ReadShiningMaterializationString(
                        target.Faction,
                        "originType"),
                    ShiningAbodeState.OriginTypeAscendedGuardian,
                    StringComparison.Ordinal) &&
                string.Equals(
                    role,
                    "patron_guardian",
                    StringComparison.Ordinal) &&
                string.Equals(
                    visibility,
                    "revealed",
                    StringComparison.Ordinal) &&
                target.Faction["leadership"] is
                    JsonObject guardianLeadership &&
                string.Equals(
                    ReadShiningMaterializationString(
                        guardianLeadership,
                        "headActorType"),
                    ShiningAbodeState.HeadActorTypeGuardian,
                    StringComparison.Ordinal) &&
                string.Equals(
                    ReadShiningMaterializationString(
                        guardianLeadership,
                        "headActorId"),
                    authorityId,
                    StringComparison.Ordinal),
            "saref_main_story" =>
                !string.IsNullOrWhiteSpace(authorityId) &&
                string.Equals(
                    authorityId,
                    target.FactionId,
                    StringComparison.Ordinal) &&
                string.Equals(
                    ReadShiningMaterializationString(
                        sarefStoryRoot?["factionLinks"]
                            as JsonObject,
                        "wingsFactionId"),
                    target.FactionId,
                    StringComparison.Ordinal) &&
                string.Equals(
                    ReadShiningMaterializationString(
                        sarefStoryRoot?["factionLinks"]
                            as JsonObject,
                        "visibility"),
                    visibility,
                    StringComparison.Ordinal) &&
                string.Equals(
                    role,
                    "wings_of_angels",
                    StringComparison.Ordinal) &&
                string.Equals(
                    ReadShiningMaterializationString(
                        target.Faction,
                        "sarefFactionRole"),
                    role,
                    StringComparison.Ordinal) &&
                string.Equals(
                    ReadShiningMaterializationString(
                        target.Faction,
                        "sarefVisibility"),
                    visibility,
                    StringComparison.Ordinal),
            _ => false
        };
        if (valid)
            return;

        issues.Add(
            ShiningFactionMaterializationIssue(
                $"{target.Context}.storyAuthority",
                "faction_materialization_shining_story_authority_reference_invalid",
                target.FactionId,
                "Story-created Shining faction authority must resolve through the closed canonical story registry.",
                expected:
                    "matching saref_main_story or guardian_ascension authority",
                actual:
                    $"{authorityType ?? "missing"}:{authorityId ?? "missing"}"));
    }

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
