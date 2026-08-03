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
                    target.FactionId,
                    issues);
            ValidateShiningFactionMaterializationReferences(
                target,
                currentRoot,
                residentRoot,
                guardiansRoot,
                afterlifeProfilesRoot,
                sarefStoryRoot,
                isCreationOrPromotion,
                affiliatedResidentIds,
                currentTradeAuthorities,
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

    private async Task<HashSet<string>>
        ReadRawShiningExternalFactionTouchIdsAsync(
            JsonElement currentRootElement,
            JsonElement preTurnRootElement,
            List<ValidationIssue> issues)
    {
        var lookup = await LoadValidatedPendingTurnSnapshotLookupAsync();
        if (lookup.Status != ValidatedPendingTurnSnapshotStatus.Usable ||
            lookup.Manifest == null)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        var currentEvidence =
            new Dictionary<string, List<RawFactionTouchEvidence>>(
                StringComparer.Ordinal);
        var preTurnEvidence =
            new Dictionary<string, List<RawFactionTouchEvidence>>(
                StringComparer.Ordinal);
        var currentRoot =
            JsonNode.Parse(currentRootElement.GetRawText()) as JsonObject;
        var preTurnRoot =
            JsonNode.Parse(preTurnRootElement.GetRawText()) as JsonObject;
        var projectedCurrentRoot =
            ProjectRawShiningRootLinkSurfaces(
                currentRoot,
                preTurnRoot);
        CollectRawShiningRootTouchEvidence(
            projectedCurrentRoot,
            currentEvidence);
        CollectRawShiningRootTouchEvidence(
            preTurnRoot,
            preTurnEvidence);

        var currentResidents = await ReadRawShiningTouchRootAsync(
            lookup.Manifest,
            GuardianAbodeResidentState.StatePath,
            preTurn: false,
            issues);
        var preTurnResidents = await ReadRawShiningTouchRootAsync(
            lookup.Manifest,
            GuardianAbodeResidentState.StatePath,
            preTurn: true,
            issues);
        CollectRawShiningResidentTouchEvidence(
            ProjectRawShiningResidentLinkSurface(
                currentResidents,
                preTurnResidents),
            currentEvidence);
        CollectRawShiningResidentTouchEvidence(
            preTurnResidents,
            preTurnEvidence);

        var currentStory = await ReadRawShiningTouchRootAsync(
            lookup.Manifest,
            SarefMainStoryState.StatePath,
            preTurn: false,
            issues);
        var preTurnStory = await ReadRawShiningTouchRootAsync(
            lookup.Manifest,
            SarefMainStoryState.StatePath,
            preTurn: true,
            issues);
        CollectRawShiningStoryTouchEvidence(
            ProjectRawShiningStoryLinkSurface(
                currentStory,
                preTurnStory),
            currentEvidence);
        CollectRawShiningStoryTouchEvidence(
            preTurnStory,
            preTurnEvidence);

        var currentGuardians = await ReadRawShiningTouchRootAsync(
            lookup.Manifest,
            "game_state/meta/guardians.json",
            preTurn: false,
            issues);
        var preTurnGuardians = await ReadRawShiningTouchRootAsync(
            lookup.Manifest,
            "game_state/meta/guardians.json",
            preTurn: true,
            issues);
        CollectRawShiningGuardianStoryTouchEvidence(
            currentRoot,
            ProjectRawShiningGuardianIdentitySurface(
                currentGuardians,
                preTurnGuardians),
            currentEvidence);
        CollectRawShiningGuardianStoryTouchEvidence(
            preTurnRoot,
            preTurnGuardians,
            preTurnEvidence);

        return FindChangedRawFactionTouchIds(
            currentEvidence,
            preTurnEvidence);
    }

    private async Task<JsonObject?> ReadRawShiningTouchRootAsync(
        ValidationPendingTurnSnapshotManifest manifest,
        string path,
        bool preTurn,
        List<ValidationIssue> issues)
    {
        var json = preTurn
            ? await ReadValidatedPendingTurnSnapshotFileAsync(
                manifest,
                path)
            : await _fs.ReadFileAsync(path);
        if (string.IsNullOrWhiteSpace(json))
            return null;

        using var document = TryParseFactionMaterializationDocument(
            json,
            path,
            currentAuthority: !preTurn,
            issues);
        return document == null
            ? null
            : JsonNode.Parse(
                document.RootElement.GetRawText()) as JsonObject;
    }

    private static JsonObject ProjectRawShiningRootLinkSurfaces(
        JsonObject? current,
        JsonObject? preTurn)
    {
        var result = preTurn?.DeepClone().AsObject() ??
                     new JsonObject();
        foreach (var (arrayName, identityField) in
                 new[]
                 {
                     ("shiningPoliticalActors", "actorId"),
                     ("coreActionReceipts", "requestId"),
                     ("factionFoundingReceipts", "requestId"),
                     ("factionRealignmentReceipts", "requestId")
                 })
        {
            if (current?[arrayName] is not JsonArray upserts)
                continue;

            var rows = result[arrayName] as JsonArray ??
                       new JsonArray();
            result[arrayName] = rows;
            ApplyRawShiningIdentityUpserts(
                rows,
                upserts,
                identityField,
                replaceExisting: false);
        }

        if (current?.TryGetPropertyValue(
                ShiningAbodeState.FactionConflictCampaignsProperty,
                out var campaigns) == true)
        {
            result[
                ShiningAbodeState.FactionConflictCampaignsProperty] =
                campaigns?.DeepClone();
        }

        return result;
    }

    private static JsonObject? ProjectRawShiningResidentLinkSurface(
        JsonObject? current,
        JsonObject? preTurn)
    {
        if (current == null && preTurn == null)
            return null;

        var result = preTurn?.DeepClone().AsObject() ??
                     new JsonObject();
        var residents = result["entries"] as JsonArray ??
                        new JsonArray();
        result["entries"] = residents;
        foreach (var arrayName in new[]
                 {
                     GuardianAbodeResidentState.EntriesProperty,
                     GuardianAbodeResidentState.UpdateProperty
                 })
        {
            if (current?[arrayName] is not JsonArray upserts)
                continue;

            ApplyRawShiningIdentityUpserts(
                residents,
                upserts,
                "residentId",
                replaceExisting: true);
        }

        return result;
    }

    private static JsonObject? ProjectRawShiningStoryLinkSurface(
        JsonObject? current,
        JsonObject? preTurn)
    {
        if (current == null)
            return preTurn;
        if (current[SarefMainStoryState.StateResponseField]
            is JsonObject canonicalRoot)
        {
            return canonicalRoot.DeepClone().AsObject();
        }

        if (current[SarefMainStoryState.ResponseField]
            is not JsonObject update)
        {
            return current;
        }

        var currentWithoutWrapper =
            current.DeepClone().AsObject();
        currentWithoutWrapper.Remove(
            SarefMainStoryState.ResponseField);
        var baseline =
            LooksLikeRawShiningStoryCanonicalRoot(
                currentWithoutWrapper)
                ? currentWithoutWrapper
                : preTurn?.DeepClone().AsObject() ??
                  SarefMainStoryState.CreateDefaultRoot();
        return SarefMainStoryState.ApplyUpdate(
            baseline,
            update);
    }

    private static bool LooksLikeRawShiningStoryCanonicalRoot(
        JsonObject root) =>
        root.ContainsKey("schemaVersion") ||
        root.ContainsKey("revealStage") ||
        root.ContainsKey("sarefRevelations") ||
        root.ContainsKey("wingsInfiltration");

    private static JsonObject? ProjectRawShiningGuardianIdentitySurface(
        JsonObject? current,
        JsonObject? preTurn)
    {
        if (current == null && preTurn == null)
            return null;

        var result = preTurn?.DeepClone().AsObject() ??
                     new JsonObject();
        if (current != null)
        {
            foreach (var property in current)
                result[property.Key] =
                    property.Value?.DeepClone();
        }

        var guardians = result["guardians"] as JsonArray ??
                        new JsonArray();
        result["guardians"] = guardians;
        if (current?["UpdateGuardians"] is JsonArray commands)
        {
            foreach (var command in commands.OfType<JsonObject>())
            {
                if (!string.Equals(
                        ReadShiningMaterializationString(
                            command,
                            "command"),
                        "create",
                        StringComparison.Ordinal) ||
                    command["data"] is not JsonObject data)
                {
                    continue;
                }

                var guardianId =
                    ReadShiningMaterializationString(
                        data,
                        "guardianId");
                var alreadyExists = guardianId != null &&
                    guardians.OfType<JsonObject>().Any(guardian =>
                        string.Equals(
                            ReadShiningMaterializationString(
                                guardian,
                                "guardianId"),
                            guardianId,
                            StringComparison.OrdinalIgnoreCase));
                if (alreadyExists)
                    continue;

                var created = data.DeepClone().AsObject();
                guardians.Add(created);
                if (result["activeGuardian"] == null)
                    result["activeGuardian"] =
                        created.DeepClone();
            }
        }

        if (result["activeGuardian"] is JsonObject active)
        {
            var activeGuardianId =
                ReadShiningMaterializationString(
                    active,
                    "guardianId");
            var synced = guardians
                .OfType<JsonObject>()
                .FirstOrDefault(guardian => string.Equals(
                    ReadShiningMaterializationString(
                        guardian,
                        "guardianId"),
                    activeGuardianId,
                    StringComparison.OrdinalIgnoreCase));
            if (synced == null)
                result.Remove("activeGuardian");
            else
                result["activeGuardian"] =
                    synced.DeepClone();
        }

        return result;
    }

    private static void ApplyRawShiningIdentityUpserts(
        JsonArray target,
        JsonArray source,
        string identityField,
        bool replaceExisting)
    {
        var (indices, ambiguous) =
            BuildRawShiningIdentityIndex(
                target,
                identityField);
        foreach (var sourceRow in source.OfType<JsonObject>())
        {
            var identity = ReadShiningMaterializationString(
                sourceRow,
                identityField);
            if (identity == null)
            {
                target.Add(sourceRow.DeepClone());
                continue;
            }

            if (indices.TryGetValue(
                    identity,
                    out var existingIndex) &&
                target[existingIndex] is JsonObject existing)
            {
                if (replaceExisting)
                {
                    target[existingIndex] =
                        sourceRow.DeepClone();
                }
                else
                {
                    foreach (var property in sourceRow)
                    {
                        existing[property.Key] =
                            property.Value?.DeepClone();
                    }
                }

                continue;
            }

            var addedIndex = target.Count;
            target.Add(sourceRow.DeepClone());
            if (!ambiguous.Contains(identity))
                indices[identity] = addedIndex;
        }
    }

    private static (
        Dictionary<string, int> Indices,
        HashSet<string> Ambiguous)
        BuildRawShiningIdentityIndex(
            JsonArray rows,
            string identityField)
    {
        var indices = new Dictionary<string, int>(
            StringComparer.OrdinalIgnoreCase);
        var ambiguous = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < rows.Count; index++)
        {
            if (rows[index] is not JsonObject row)
                continue;

            var identity = ReadShiningMaterializationString(
                row,
                identityField);
            if (identity == null || ambiguous.Contains(identity))
                continue;

            if (indices.ContainsKey(identity))
            {
                indices.Remove(identity);
                ambiguous.Add(identity);
            }
            else
            {
                indices[identity] = index;
            }
        }

        return (indices, ambiguous);
    }

    private static void CollectRawShiningRootTouchEvidence(
        JsonObject? root,
        Dictionary<string, List<RawFactionTouchEvidence>> evidence)
    {
        CollectRawShiningTargetArray(
            root,
            "shiningPoliticalActors",
            "actorId",
            ["originFactionId", "currentFactionId"],
            evidence);
        CollectRawShiningTargetArray(
            root,
            ShiningAbodeState.FactionConflictCampaignsProperty,
            "campaignId",
            ["targetFactionId"],
            evidence);
        CollectRawShiningTargetArray(
            root,
            "coreActionReceipts",
            "requestId",
            ["factionId", "resolvedFactionId", "targetFactionId"],
            evidence);
        CollectRawShiningTargetArray(
            root,
            "factionFoundingReceipts",
            "requestId",
            ["factionId", "proposedFactionId"],
            evidence);
        CollectRawShiningTargetArray(
            root,
            "factionRealignmentReceipts",
            "requestId",
            ["sourceFactionId", "targetFactionId"],
            evidence);
    }

    private static void CollectRawShiningTargetArray(
        JsonObject? root,
        string arrayName,
        string rowIdentityField,
        IReadOnlyList<string> factionIdFields,
        Dictionary<string, List<RawFactionTouchEvidence>> evidence)
    {
        if (root?[arrayName] is not JsonArray rows)
            return;

        foreach (var row in rows.OfType<JsonObject>())
        {
            var rowIdentity = ReadShiningMaterializationString(
                row,
                rowIdentityField);
            if (rowIdentity == null)
                continue;

            foreach (var factionIdField in factionIdFields)
            {
                var factionId = ReadShiningMaterializationString(
                    row,
                    factionIdField);
                AddRawFactionTouchEvidence(
                    evidence,
                    factionId,
                    $"{ShiningAbodeState.StatePath}.{arrayName}:" +
                    $"{rowIdentity}.{factionIdField}",
                    JsonValue.Create(factionId));
            }
        }
    }

    private static void CollectRawShiningResidentTouchEvidence(
        JsonObject? root,
        Dictionary<string, List<RawFactionTouchEvidence>> evidence)
    {
        if (root?["entries"] is not JsonArray residents)
            return;

        foreach (var resident in residents.OfType<JsonObject>())
        {
            var factionId = ReadShiningMaterializationString(
                resident,
                "shiningFactionId");
            var residentId = ReadShiningMaterializationString(
                resident,
                "residentId");
            if (factionId == null || residentId == null)
                continue;

            var affiliation = new JsonObject();
            foreach (var field in new[]
                     {
                         "residentId",
                         "shiningFactionId"
                     })
            {
                if (resident.TryGetPropertyValue(field, out var value))
                    affiliation[field] = value?.DeepClone();
            }

            AddRawFactionTouchEvidence(
                evidence,
                factionId,
                $"{GuardianAbodeResidentState.StatePath}.entries:" +
                $"{residentId}." +
                "shiningFactionId",
                affiliation);
        }
    }

    private static void CollectRawShiningStoryTouchEvidence(
        JsonObject? root,
        Dictionary<string, List<RawFactionTouchEvidence>> evidence)
    {
        if (root?["factionLinks"] is not JsonObject factionLinks)
            return;

        AddRawFactionTouchEvidence(
            evidence,
            ReadShiningMaterializationString(
                factionLinks,
                "wingsFactionId"),
            $"{SarefMainStoryState.StatePath}.factionLinks",
            new JsonObject
            {
                ["wingsFactionId"] =
                    factionLinks["wingsFactionId"]?.DeepClone(),
                ["visibility"] =
                    factionLinks["visibility"]?.DeepClone()
            });
    }

    private static void CollectRawShiningGuardianStoryTouchEvidence(
        JsonObject? shiningRoot,
        JsonObject? guardiansRoot,
        Dictionary<string, List<RawFactionTouchEvidence>> evidence)
    {
        if (shiningRoot?["factions"] is not JsonArray factions ||
            guardiansRoot == null)
        {
            return;
        }

        foreach (var faction in factions.OfType<JsonObject>())
        {
            var factionId = ReadShiningMaterializationString(
                faction,
                "factionId");
            if (faction["storyAuthority"] is not JsonObject authority ||
                !string.Equals(
                    ReadShiningMaterializationString(
                        authority,
                        "authorityType"),
                    "guardian_ascension",
                    StringComparison.Ordinal))
            {
                continue;
            }

            var guardianId = ReadShiningMaterializationString(
                authority,
                "authorityId");
            if (guardianId == null)
                continue;

            AddRawFactionTouchEvidence(
                evidence,
                factionId,
                $"game_state/meta/guardians.json.storyAuthority:" +
                guardianId,
                JsonValue.Create(
                    TryResolveUniqueGuardianIdentity(
                        guardiansRoot,
                        guardianId,
                        out _)));
        }
    }

    private static bool
        ShiningFactionNodesEqualIgnoringDerivedFields(
            JsonObject current,
            JsonObject previous)
    {
        var currentClone = current.DeepClone().AsObject();
        var previousClone = previous.DeepClone().AsObject();
        foreach (var field in ShiningClientDerivedFactionFields)
        {
            currentClone.Remove(field);
            previousClone.Remove(field);
        }

        return JsonNode.DeepEquals(currentClone, previousClone);
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
                        $"{factionId ?? "missing"} / hall {actualHallId ?? "missing"}"));
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
                            : $"malformed or duplicate projectId entries; ids={actualProjectSet}"));
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
                        $"{ReadShiningMaterializationString(provenance, "authorityId") ?? "missing"}"));
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
                                StringComparer.Ordinal))));
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
                            "missing"));
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
        bool isCreationOrPromotion,
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

        if (isCreationOrPromotion)
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
                        $"{profiles.Length} exact-pair candidates"));
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
