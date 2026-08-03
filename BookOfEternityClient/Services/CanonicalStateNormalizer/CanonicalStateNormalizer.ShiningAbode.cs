using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

public partial class CanonicalStateNormalizer
{
    private async Task NormalizeShiningAbodeStateAsync(IReadOnlyDictionary<string, string>? backups)
    {
        const string path = ShiningAbodeState.StatePath;
        var currentNode = await ReadNodeAsync(path);
        if (currentNode == null)
            return;

        var previous = await ReadBackupObjectAsync(path, backups);
        var result = CloneObject(previous ?? ShiningAbodeState.CreateDefaultState());

        if (currentNode is JsonObject currentObject)
            MergeShiningAbodeRoot(result, currentObject);
        else
            return;

        ShiningAbodeState.ApplyFactionPoliticalUpdateSurfaces(result);
        var factionNormalizationModes =
            BuildShiningFactionNormalizationModes(
                result,
                previous);

        JsonObject? residentRoot = null;
        if (await ReadNodeAsync(GuardianAbodeResidentState.StatePath) is JsonObject currentResidentObject)
        {
            residentRoot = CloneObject(currentResidentObject);
            GuardianAbodeResidentState.NormalizeShape(residentRoot);
        }

        JsonObject? guardiansRoot = null;
        if (await ReadNodeAsync("game_state/meta/guardians.json") is JsonObject currentGuardiansObject)
            guardiansRoot = CloneObject(currentGuardiansObject);

        ShiningAbodeState.NormalizeStateRoot(
            result,
            residentRoot,
            guardiansRoot,
            factionNormalizationModes);
        await WriteIfChangedAsync(path, currentNode, result);
    }

    private static IReadOnlyDictionary<
        string,
        ShiningFactionNormalizationMode>
        BuildShiningFactionNormalizationModes(
            JsonObject current,
            JsonObject? previous)
    {
        var previousById =
            new Dictionary<string, JsonObject>(
                StringComparer.Ordinal);
        if (previous?["factions"] is JsonArray previousFactions)
        {
            foreach (var faction in
                     previousFactions.OfType<JsonObject>())
            {
                var factionId =
                    GetNodeString(faction["factionId"]);
                if (!string.IsNullOrWhiteSpace(factionId))
                    previousById.TryAdd(factionId, faction);
            }
        }

        var result =
            new Dictionary<
                string,
                ShiningFactionNormalizationMode>(
                StringComparer.Ordinal);
        if (current["factions"] is not JsonArray currentFactions)
            return result;

        foreach (var faction in
                 currentFactions.OfType<JsonObject>())
        {
            var factionId =
                GetNodeString(faction["factionId"]);
            if (string.IsNullOrWhiteSpace(factionId))
                continue;

            var hasReceipt = faction.ContainsKey(
                FactionMaterializationContract.PropertyName);
            var untouchedLegacy =
                !hasReceipt &&
                previousById.TryGetValue(
                    factionId,
                    out var previousFaction) &&
                !previousFaction.ContainsKey(
                    FactionMaterializationContract.PropertyName) &&
                JsonNode.DeepEquals(
                    faction,
                    previousFaction);
            result[factionId] = untouchedLegacy
                ? ShiningFactionNormalizationMode.LegacyCompatibility
                : ShiningFactionNormalizationMode.AuthoredMaterialization;
        }

        return result;
    }

    private static void MergeShiningAbodeRoot(JsonObject target, JsonObject source)
    {
        foreach (var prop in source)
        {
            if (prop.Value is JsonArray sourceArray &&
                TryGetShiningRootArrayIdentity(prop.Key, out var identityProperty))
            {
                MergeJsonObjectArrayByIdentity(
                    EnsureArray(target, prop.Key),
                    sourceArray,
                    identityProperty,
                    prop.Key.Equals("factions", StringComparison.OrdinalIgnoreCase)
                        ? MergeShiningFactionObject
                        : MergeObject);
                continue;
            }

            target[prop.Key] = prop.Value?.DeepClone();
        }
    }

    private static bool TryGetShiningRootArrayIdentity(string propertyName, out string identityProperty)
    {
        identityProperty = propertyName switch
        {
            "halls" => "hallId",
            "factions" => "factionId",
            "shiningPoliticalActors" => "actorId",
            "coreActionReceipts" => "requestId",
            "factionFoundingReceipts" => "requestId",
            "factionRealignmentReceipts" => "requestId",
            _ => string.Empty
        };

        return !string.IsNullOrWhiteSpace(identityProperty);
    }

    private static void MergeShiningFactionObject(JsonObject target, JsonObject source)
    {
        foreach (var prop in source)
        {
            if (prop.Value is JsonArray sourceArray &&
                TryGetShiningFactionArrayIdentity(prop.Key, out var identityProperty))
            {
                MergeJsonObjectArrayByIdentity(EnsureArray(target, prop.Key), sourceArray, identityProperty, MergeObject);
                continue;
            }

            if (prop.Value is JsonObject sourceObject && target[prop.Key] is JsonObject targetObject)
            {
                MergeObject(targetObject, sourceObject);
                continue;
            }

            target[prop.Key] = prop.Value?.DeepClone();
        }
    }

    private static bool TryGetShiningFactionArrayIdentity(string propertyName, out string identityProperty)
    {
        identityProperty = propertyName switch
        {
            "projects" => "projectId",
            "tradeInventoryReceipts" => "requestId",
            "leadershipReceipts" => "requestId",
            "leadershipHistory" => "requestId",
            ShiningAbodeState.FactionChronicleProperty => "entryId",
            ShiningAbodeState.FactionInfluenceProperty => "zoneId",
            ShiningAbodeState.FactionResourceLedgerProperty => "entryId",
            _ => string.Empty
        };

        return !string.IsNullOrWhiteSpace(identityProperty);
    }

    private static void MergeJsonObjectArrayByIdentity(
        JsonArray target,
        JsonArray source,
        string identityProperty,
        Action<JsonObject, JsonObject> mergeExisting)
    {
        foreach (var sourceNode in source)
        {
            if (sourceNode is not JsonObject sourceItem)
            {
                target.Add(sourceNode?.DeepClone());
                continue;
            }

            var sourceId = GetNodeString(sourceItem[identityProperty]);
            if (string.IsNullOrWhiteSpace(sourceId))
            {
                target.Add(sourceItem.DeepClone());
                continue;
            }

            var targetItem = target
                .OfType<JsonObject>()
                .FirstOrDefault(item => string.Equals(GetNodeString(item[identityProperty]), sourceId, StringComparison.OrdinalIgnoreCase));
            if (targetItem == null)
            {
                target.Add(sourceItem.DeepClone());
                continue;
            }

            mergeExisting(targetItem, sourceItem);
        }
    }
}
