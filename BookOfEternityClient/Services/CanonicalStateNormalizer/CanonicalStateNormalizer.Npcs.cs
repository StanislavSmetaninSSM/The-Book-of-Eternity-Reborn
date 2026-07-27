using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

public partial class CanonicalStateNormalizer
{
    private async Task NormalizeNpcCoreChangesAsync(IReadOnlyDictionary<string, string>? backups)
    {
        var currentNode = await ReadNodeAsync(NpcCoreChangesContract.NpcCorePath);
        if (currentNode is not JsonObject currentRoot ||
            !currentRoot.ContainsKey(NpcCoreChangesContract.PropertyName))
        {
            return;
        }

        var preTurnRoot = await ReadBackupObjectAsync(NpcCoreChangesContract.NpcCorePath, backups);
        if (preTurnRoot == null)
            return;

        var result = CloneObject(currentRoot);
        var authority = await ReadNpcCoreAuthorityAsync();
        var evaluation = NpcCoreChangesContract.Evaluate(
            result,
            preTurnRoot,
            authority,
            ValidationService.ValidateNpcCoreFateCardsAgainstProductionContract,
            detectDirectMutations: true);
        if (!evaluation.CanApply)
            return;

        NpcCoreChangesContract.Apply(result, evaluation);
        await WriteIfChangedAsync(NpcCoreChangesContract.NpcCorePath, currentNode, result);
    }

    private async Task<NpcCoreChangesContract.Authority> ReadNpcCoreAuthorityAsync()
    {
        var permanentLocationIds = new HashSet<string>(StringComparer.Ordinal);
        var sameTurnLocationIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var path in new[]
                 {
                     "game_state/world/current_location.json",
                     "game_state/world/world_map.json"
                 })
        {
            CollectNpcLocationAuthority(
                await ReadNodeAsync(path),
                permanentLocationIds,
                sameTurnLocationIds);
        }

        var factionNamesById = new Dictionary<string, string>(StringComparer.Ordinal);
        CollectNpcFactionAuthority(
            await ReadNodeAsync("game_state/factions/faction_core.json"),
            factionNamesById);

        var characteristicKeys = new HashSet<string>(StringComparer.Ordinal);
        if (await ReadNodeAsync("game_state/misc/characteristics.json") is JsonObject characteristics)
        {
            foreach (var property in characteristics)
            {
                if (!property.Key.StartsWith("_", StringComparison.Ordinal) &&
                    property.Value is JsonValue)
                {
                    characteristicKeys.Add(property.Key);
                }
            }
        }

        return new NpcCoreChangesContract.Authority(
            permanentLocationIds,
            sameTurnLocationIds,
            factionNamesById,
            characteristicKeys);
    }

    private static void CollectNpcLocationAuthority(
        JsonNode? node,
        HashSet<string> permanentIds,
        HashSet<string> sameTurnIds)
    {
        switch (node)
        {
            case JsonObject obj:
            {
                var locationId = GetNodeString(obj["locationId"]);
                var initialId = GetNodeString(obj["initialId"]);
                if (!string.IsNullOrWhiteSpace(locationId))
                    permanentIds.Add(locationId);
                else if (!string.IsNullOrWhiteSpace(initialId))
                    sameTurnIds.Add(initialId);

                foreach (var property in obj)
                    CollectNpcLocationAuthority(property.Value, permanentIds, sameTurnIds);
                break;
            }
            case JsonArray array:
                foreach (var item in array)
                    CollectNpcLocationAuthority(item, permanentIds, sameTurnIds);
                break;
        }
    }

    private static void CollectNpcFactionAuthority(
        JsonNode? node,
        Dictionary<string, string> factionNamesById)
    {
        switch (node)
        {
            case JsonObject obj:
            {
                var factionId = GetNodeString(obj["factionId"]);
                var factionName = FirstNonEmptyString(
                    GetNodeString(obj["name"]),
                    GetNodeString(obj["factionName"]));
                if (!string.IsNullOrWhiteSpace(factionId) &&
                    !string.IsNullOrWhiteSpace(factionName))
                {
                    factionNamesById.TryAdd(factionId, factionName);
                }

                foreach (var property in obj)
                    CollectNpcFactionAuthority(property.Value, factionNamesById);
                break;
            }
            case JsonArray array:
                foreach (var item in array)
                    CollectNpcFactionAuthority(item, factionNamesById);
                break;
        }
    }

    private async Task NormalizeNpcJournalsAsync(IReadOnlyDictionary<string, string>? backups)
    {
        const string path = "game_state/npcs/npc_journals.json";
        var currentNode = await ReadNodeAsync(path);
        if (currentNode is not JsonObject currentObj)
            return;

        var result = CloneObject(currentObj);
        var changed = false;

        foreach (var collectionName in new[] { "NPCJournals", "npcJournals" })
        {
            if (result[collectionName] is not JsonArray journals)
                continue;

            foreach (var journal in journals.OfType<JsonObject>())
                changed |= NormalizeNpcJournalEntry(journal);
        }

        if (changed)
            await WriteIfChangedAsync(path, currentNode, result);
    }

    private static bool NormalizeNpcJournalEntry(JsonObject journal)
    {
        var changed = false;
        var fallbackNote = FirstNonEmptyString(
            GetNodeString(journal["lastJournalNote"]),
            GetNodeString(journal["entry"]),
            GetNodeString(journal["note"]),
            GetNodeString(journal["text"]),
            GetNodeString(journal["description"]));

        if (string.IsNullOrWhiteSpace(GetNodeString(journal["lastJournalNote"])) &&
            !string.IsNullOrWhiteSpace(fallbackNote))
        {
            journal["lastJournalNote"] = fallbackNote;
            changed = true;
        }

        if (journal["journalEntries"] is not JsonArray journalEntries)
        {
            if (string.IsNullOrWhiteSpace(fallbackNote))
                return changed;

            journal["journalEntries"] = new JsonArray(BuildNpcJournalEntryObject(fallbackNote, journal));
            return true;
        }

        for (var index = 0; index < journalEntries.Count; index++)
        {
            var item = journalEntries[index];
            switch (item)
            {
                case JsonValue value when value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text):
                    journalEntries[index] = BuildNpcJournalEntryObject(text, journal);
                    changed = true;
                    break;
                case JsonObject obj:
                    changed |= EnsureNpcJournalEntryDescription(obj, fallbackNote);
                    break;
            }
        }

        if (journalEntries.Count == 0 && !string.IsNullOrWhiteSpace(fallbackNote))
        {
            journalEntries.Add(BuildNpcJournalEntryObject(fallbackNote, journal));
            changed = true;
        }

        return changed;
    }

    private static JsonObject BuildNpcJournalEntryObject(string description, JsonObject parent)
    {
        var entry = new JsonObject
        {
            ["description"] = description
        };

        CopyStringIfPresent(parent, entry, "timestamp");
        CopyStringIfPresent(parent, entry, "event");
        CopyStringIfPresent(parent, entry, "emotionalImpact");
        CopyStringIfPresent(parent, entry, "relationshipChange");
        return entry;
    }

    private static bool EnsureNpcJournalEntryDescription(JsonObject entry, string? fallbackNote)
    {
        if (!string.IsNullOrWhiteSpace(GetNodeString(entry["description"])))
            return false;

        var description = FirstNonEmptyString(
            GetNodeString(entry["note"]),
            GetNodeString(entry["entry"]),
            GetNodeString(entry["text"]),
            fallbackNote);
        if (string.IsNullOrWhiteSpace(description))
            return false;

        entry["description"] = description;
        return true;
    }

    private static void CopyStringIfPresent(JsonObject source, JsonObject destination, string propertyName)
    {
        var value = GetNodeString(source[propertyName]);
        if (!string.IsNullOrWhiteSpace(value))
            destination[propertyName] = value;
    }

    private static string FirstNonEmptyString(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return string.Empty;
    }

    private async Task NormalizeNpcTradeCoreAsync(IReadOnlyDictionary<string, string>? backups)
    {
        const string path = "game_state/npcs/npc_core.json";
        var currentNode = await ReadNodeAsync(path);
        if (currentNode is not JsonObject currentObj)
            return;

        var previousObj = await ReadBackupObjectAsync(path, backups);
        var result = CloneObject(currentObj);
        var changed = PreserveHistoricalMortalMaterialization(result, previousObj);

        changed |= NormalizeMortalTeacherTrainingShowcasePatches(result);

        if (result[NpcTradeRequestState.UpdateReceiptsProperty] is JsonArray receiptUpdates)
        {
            NpcTradeRequestState.ApplyReceiptUpdates(result, receiptUpdates);
            result.Remove(NpcTradeRequestState.UpdateReceiptsProperty);
            changed = true;
        }

        foreach (var npcs in GuardianPolicyContracts.EnumerateCanonicalNpcObjectArrays(result))
        {
            foreach (var npc in npcs.OfType<JsonObject>())
            {
                var before = npc.ToJsonString();
                NpcTradeRequestState.NormalizeNpcTradeReceiptsShape(npc);
                if (!string.Equals(before, npc.ToJsonString(), StringComparison.Ordinal))
                    changed = true;
            }
        }

        if (changed)
            await WriteIfChangedAsync(path, currentNode, result);
    }

    private static bool PreserveHistoricalMortalMaterialization(
        JsonObject currentRoot,
        JsonObject? previousRoot)
    {
        if (previousRoot == null ||
            currentRoot[GuardianPolicyContracts.NpcCoreSceneSectionName] is not JsonArray currentActors)
        {
            return false;
        }

        var historicalEnvelopeByActorId = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        var seenHistoricalActorIds = new HashSet<string>(StringComparer.Ordinal);
        var ambiguousHistoricalActorIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var previousActor in GuardianPolicyContracts.EnumerateCanonicalNpcObjects(previousRoot))
        {
            var actorId = ResolveMortalMaterializationIdentity(previousActor);
            if (string.IsNullOrWhiteSpace(actorId))
                continue;

            if (!seenHistoricalActorIds.Add(actorId))
                ambiguousHistoricalActorIds.Add(actorId);

            if (previousActor[ActorMaterializationContract.PropertyName] is not JsonObject historicalEnvelope)
                continue;

            if (!historicalEnvelopeByActorId.TryAdd(actorId, historicalEnvelope))
                ambiguousHistoricalActorIds.Add(actorId);
        }

        foreach (var actorId in ambiguousHistoricalActorIds)
            historicalEnvelopeByActorId.Remove(actorId);

        var seenCurrentActorIds = new HashSet<string>(StringComparer.Ordinal);
        var ambiguousCurrentActorIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var currentActor in currentActors.OfType<JsonObject>())
        {
            var actorId = ResolveMortalMaterializationIdentity(currentActor);
            if (!string.IsNullOrWhiteSpace(actorId) && !seenCurrentActorIds.Add(actorId))
                ambiguousCurrentActorIds.Add(actorId);
        }

        var changed = false;
        foreach (var currentActor in currentActors.OfType<JsonObject>())
        {
            if (!HasCompleteNpcCoreSurface(currentActor) ||
                currentActor.ContainsKey(ActorMaterializationContract.PropertyName))
            {
                continue;
            }

            var actorId = ResolveMortalMaterializationIdentity(currentActor);
            if (string.IsNullOrWhiteSpace(actorId) ||
                ambiguousCurrentActorIds.Contains(actorId) ||
                !historicalEnvelopeByActorId.TryGetValue(actorId, out var historicalEnvelope))
            {
                continue;
            }

            currentActor[ActorMaterializationContract.PropertyName] = historicalEnvelope.DeepClone();
            changed = true;
        }

        return changed;
    }

    private static bool NormalizeMortalTeacherTrainingShowcasePatches(JsonObject root)
    {
        if (root["UpdateNPCs"] is not JsonArray updates)
            return false;

        var changed = false;
        for (var index = updates.Count - 1; index >= 0; index--)
        {
            if (updates[index] is not JsonObject patch ||
                !IsMortalTeacherTrainingShowcasePatchOrDebris(patch, out var hasShowcase))
            {
                continue;
            }

            var npcId = ResolveNpcPatchIdentity(patch);
            if (string.IsNullOrWhiteSpace(npcId))
                continue;

            var existing = FindExistingNpcForTrainingShowcasePatch(root, npcId, patch);
            if (existing == null)
                continue;

            if (hasShowcase && patch["trainingShowcase"] is JsonObject showcase)
                existing["trainingShowcase"] = showcase.DeepClone();

            updates.RemoveAt(index);
            changed = true;
        }

        if (updates.Count == 0)
        {
            root.Remove("UpdateNPCs");
            changed = true;
        }

        return changed;
    }

    private static bool IsMortalTeacherTrainingShowcasePatchOrDebris(
        JsonObject patch,
        out bool hasShowcase)
    {
        hasShowcase = patch["trainingShowcase"] is JsonObject;
        if (!patch.ContainsKey("trainingShowcase"))
            return false;

        if (string.IsNullOrWhiteSpace(ResolveNpcPatchIdentity(patch)))
            return false;

        foreach (var property in patch)
        {
            if (IsNpcPatchIdentityField(property.Key))
            {
                if (property.Value == null || !string.IsNullOrWhiteSpace(GetNodeString(property.Value)))
                    continue;
                return false;
            }

            if (string.Equals(property.Key, "trainingShowcase", StringComparison.OrdinalIgnoreCase))
            {
                if (property.Value is null or JsonObject)
                    continue;
                return false;
            }

            if (string.Equals(property.Key, "inventory", StringComparison.OrdinalIgnoreCase))
                continue;

            if (string.Equals(property.Key, "name", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(property.Key, "npcName", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(property.Key, "NPCName", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(property.Key, "role", StringComparison.OrdinalIgnoreCase))
            {
                if (property.Value == null || !string.IsNullOrWhiteSpace(GetNodeString(property.Value)))
                    continue;
                return false;
            }

            if (property.Value == null)
                continue;

            return false;
        }

        return true;
    }

    private static JsonObject? FindExistingNpcForTrainingShowcasePatch(
        JsonObject root,
        string npcId,
        JsonObject patch)
    {
        foreach (var sectionName in new[] { "NPCsInScene", "NPCs", "npcs", "npcDataChanges", "UpdateNPCs" })
        {
            if (root[sectionName] is not JsonArray npcs)
                continue;

            foreach (var npc in npcs.OfType<JsonObject>())
            {
                if (ReferenceEquals(npc, patch) ||
                    !string.Equals(ResolveNpcPatchIdentity(npc), npcId, StringComparison.OrdinalIgnoreCase) ||
                    !HasCompleteNpcCoreSurface(npc))
                {
                    continue;
                }

                return npc;
            }
        }

        return null;
    }

    private static bool HasCompleteNpcCoreSurface(JsonObject npc) =>
        !string.IsNullOrWhiteSpace(GetNodeString(npc["name"])) &&
        npc.ContainsKey("currentLocationId") &&
        npc.ContainsKey("relationshipLevel") &&
        npc.ContainsKey("attitude") &&
        npc.ContainsKey("inventory") &&
        npc.ContainsKey("goals");

    private static string? ResolveMortalMaterializationIdentity(JsonObject npc)
    {
        string? permanentId = null;
        var hasNullPermanentAlias = false;
        foreach (var fieldName in new[] { "NPCId", "npcId", "id" })
        {
            if (!npc.TryGetPropertyValue(fieldName, out var node))
                continue;
            if (node == null)
            {
                hasNullPermanentAlias = true;
                continue;
            }
            if (node is not JsonValue value ||
                !value.TryGetValue<string>(out var candidate) ||
                string.IsNullOrWhiteSpace(candidate))
            {
                return null;
            }

            if (permanentId != null && !string.Equals(permanentId, candidate, StringComparison.Ordinal))
                return null;

            permanentId = candidate;
        }

        if (permanentId != null)
            return hasNullPermanentAlias ? null : permanentId;

        if (!npc.TryGetPropertyValue("initialId", out var initialIdNode) ||
            initialIdNode is not JsonValue initialIdValue ||
            !initialIdValue.TryGetValue<string>(out var initialId) ||
            string.IsNullOrWhiteSpace(initialId))
        {
            return null;
        }

        return initialId;
    }

    private static string? ResolveNpcPatchIdentity(JsonObject npc) =>
        GetNodeString(npc["NPCId"]) ??
        GetNodeString(npc["npcId"]) ??
        GetNodeString(npc["id"]) ??
        GetNodeString(npc["initialId"]);

    private static bool IsNpcPatchIdentityField(string fieldName) =>
        string.Equals(fieldName, "NPCId", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(fieldName, "npcId", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(fieldName, "id", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(fieldName, "initialId", StringComparison.OrdinalIgnoreCase);
}
