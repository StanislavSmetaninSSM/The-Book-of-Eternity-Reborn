using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    internal const string OffscreenNpcActivityPath = "game_state/npcs/npc_activities.json";
    internal const string OffscreenNpcInventoryPath = "game_state/npcs/npc_inventory.json";
    private const string TerminalNpcActivityCommands = "completeNPCActivities";

    // Immutable, operation-local DATA. The ordinary publication comparison owns
    // source identity and exact bytes at both ends of the final validation lease.
    internal sealed class OffscreenNpcProjection(JsonObject expected, Dictionary<string, JsonObject> rows,
        bool consumedActivity, string? originalActivityHash, bool observeAcquisitions)
    {
        internal bool MatchesCompleteRoot(byte[] bytes) =>
            rows.Count != 0 && TryReadOffscreenObject(bytes) is { } actual && JsonNode.DeepEquals(expected, actual);

        internal bool MatchesActivityAfterImage(byte[]? bytes) => consumedActivity
            ? bytes is not null && TryReadOffscreenObject(bytes) is { Count: 0 }
            : (bytes is null ? null : LocationHash(bytes)) == originalActivityHash;

        internal bool MatchesRow(string id, JsonElement row) =>
            rows.TryGetValue(id, out var expectedRow) && JsonValuesSemanticallyEqual(expectedRow.ToJsonString(), row.GetRawText());

        // The item owner supplies its actual chosen and successfully written
        // phase, not a reread of the final NPC row. Import only admitted root
        // appends; the existing comparison still owns every other NPC byte.
        internal bool TryComposeAcquisitions(MortalItemAcceptedTurnNormalizationSnapshot snapshot,
            IReadOnlyDictionary<string, JsonNode?> phase)
        {
            if (!observeAcquisitions ||
                phase[NpcCoreChangesContract.NpcCorePath] is not JsonObject phaseCore ||
                phaseCore["UpdateNPCs"] is not JsonArray phaseRows ||
                phase[MortalItemIdentityState.StatePath] is not JsonObject index ||
                index["entries"] is not JsonArray entries)
                return false;

            var routes = snapshot.CloneRouteCatalog().ByCreationRef
                .Where(pair => pair.Value is { Route: "npc_acquisition", AuthorityKind: "npc_inventory_add",
                    Destination: { Kind: "npc_inventory", ContainerId: null, ContainerPath.Count: 0 } })
                .GroupBy(pair => pair.Value.Destination.OwnerId, StringComparer.Ordinal).ToArray();
            if (routes.Length == 0) return false;

            var byId = entries.OfType<JsonObject>().ToLookup(
                entry => ExactOffscreenText(entry["itemId"]), StringComparer.Ordinal);
            var appends = new List<(string Id, JsonObject Row, JsonObject[] Items)>();
            foreach (var owner in routes)
            {
                if (FindHistoricalOffscreenRow(expected, owner.Key) is not { } row)
                    continue;
                var projectedOwner = phaseRows.OfType<JsonObject>()
                    .Where(candidate => ExactOffscreenText(candidate["NPCId"]) == owner.Key).Take(2).ToArray();
                if (projectedOwner.Length != 1 || projectedOwner[0]["inventory"] is not JsonArray projectedInventory ||
                    row["inventory"] is not (null or JsonArray))
                    return false;

                var allocated = new Dictionary<string, (string CreationRef, string ReceiptId)>(StringComparer.Ordinal);
                foreach (var route in owner)
                {
                    if (!snapshot.TryGetAllocatedItemId(route.Key, out var itemId) ||
                        !snapshot.TryGetRootReceiptId(route.Key, out var receiptId) ||
                        !allocated.TryAdd(itemId, (route.Key, receiptId)))
                        return false;
                }

                var additions = new List<JsonObject>();
                foreach (var item in projectedInventory.OfType<JsonObject>())
                {
                    if (ExactOffscreenText(item["itemId"]) is not { } itemId ||
                        !allocated.Remove(itemId, out var allocation))
                        continue;
                    if (item[MortalItemMaterializationContract.ReceiptProperty] is not JsonObject receipt ||
                        ExactOffscreenText(receipt["receiptId"]) != allocation.ReceiptId ||
                        ExactOffscreenText(receipt["creationRef"]) != allocation.CreationRef ||
                        ExactOffscreenText(receipt["instanceKind"]) != "root" ||
                        receipt["acceptedAtTurn"]?.GetValue<int>() != snapshot.Turn)
                        return false;

                    var itemEntries = byId[itemId].Take(2).ToArray();
                    if (itemEntries.Length != 1 ||
                        ExactOffscreenText(itemEntries[0]["receiptId"]) != allocation.ReceiptId ||
                        itemEntries[0]["currentCarrier"] is not JsonObject carrier ||
                        ExactOffscreenText(carrier["kind"]) != "npc_inventory" ||
                        ExactOffscreenText(carrier["ownerId"]) != owner.Key)
                        return false;
                    additions.Add(item);
                }
                if (allocated.Count != 0) return false;
                appends.Add((owner.Key, row, additions.ToArray()));
            }

            foreach (var append in appends)
            {
                if (append.Row["inventory"] is not JsonArray inventory)
                {
                    inventory = new JsonArray();
                    append.Row["inventory"] = inventory;
                }
                foreach (var item in append.Items) inventory.Add(item.DeepClone());
                rows[append.Id] = append.Row;
            }
            return appends.Count != 0;
        }
    }

    private static bool NeedsOffscreenNpcProjection(byte[]? core, byte[]? activities) =>
        core is not null &&
        (core.AsSpan().IndexOf("\"NPCCoreChanges\""u8) >= 0 ||
         activities is not null &&
         (activities.AsSpan().IndexOf("\"NPCActivityUpdates\""u8) >= 0 ||
          activities.AsSpan().IndexOf("\"completeNPCActivities\""u8) >= 0));

    private static JsonObject? TryReadOffscreenObject(byte[]? bytes)
    {
        if (bytes is null) return null;
        try
        {
            using var document = JsonDocument.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(bytes));
            return document.RootElement.ValueKind == JsonValueKind.Object &&
                   !TryFindDuplicateJsonProperty(document.RootElement, out _)
                ? JsonNode.Parse(document.RootElement.GetRawText()) as JsonObject : null;
        }
        catch (Exception error) when (error is JsonException or ArgumentException or InvalidDataException)
        {
            return null; // Malformed sources gain no comparison; the existing raw validators own diagnosis.
        }
    }

    private static OffscreenNpcProjection? BuildOffscreenNpcProjection(
        byte[]? originalCore, byte[]? rawCore, byte[]? rawActivities, int originalTurn, bool observeAcquisitions)
    {
        var expected = TryReadOffscreenObject(originalCore);
        var raw = TryReadOffscreenObject(rawCore);
        if (expected is null || raw is null || expected.ContainsKey(NpcCoreChangesContract.PropertyName) ||
            expected["UpdateNPCs"] is not JsonArray || expected["NPCsInScene"] is not JsonArray)
            return null;

        var coreCommands = raw[NpcCoreChangesContract.PropertyName];
        if (raw.Remove(NpcCoreChangesContract.PropertyName) && coreCommands is not JsonArray ||
            !JsonNode.DeepEquals(expected, raw))
            return null; // Do not bless any direct carrier write, including an unrelated actor.

        var activities = TryReadOffscreenObject(rawActivities);
        if (rawActivities is not null && activities is null ||
            activities?.Any(property => property.Key is not ("NPCActivityUpdates" or TerminalNpcActivityCommands)) == true ||
            activities?.ContainsKey("NPCActivityUpdates") == true && activities["NPCActivityUpdates"] is not JsonArray)
            return null;
        var terminal = activities?[TerminalNpcActivityCommands] as JsonArray;
        if (activities?.ContainsKey(TerminalNpcActivityCommands) == true &&
            (terminal is not { Count: > 0 } || activities.Count != 1 || coreCommands is not null))
            return null; // This observer certifies only the ordinary separate terminal-command carrier.

        var touched = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        if (coreCommands is JsonArray moves)
        {
            foreach (var node in moves)
            {
                if (node is not JsonObject move || !ExactOffscreenKeys(move, "NPCId", "reason", "location") ||
                    ExactOffscreenText(move["NPCId"]) is not { } id || !seen.Add(id) ||
                    ExactOffscreenText(move["reason"]) is null ||
                    move["location"] is not JsonObject location ||
                    !ExactOffscreenKeys(location, "currentLocationId", "initialLocationId") ||
                    location["initialLocationId"] is not null ||
                    ExactOffscreenText(location["currentLocationId"]) is not { } destination ||
                    FindHistorical(id) is not { } row)
                    return null;

                row["currentLocationId"] = destination;
                row["initialLocationId"] = null;
                touched[id] = row;
            }
        }

        seen.Clear();
        if (activities?["NPCActivityUpdates"] is JsonArray work)
        {
            foreach (var node in work)
            {
                if (node is not JsonObject command ||
                    !ExactOffscreenKeys(command, "NPCId", "NPCName", "activityUpdate") ||
                    ExactOffscreenText(command["NPCId"]) is not { } id || !seen.Add(id) ||
                    FindHistorical(id) is not { } row || ExactOffscreenText(command["NPCName"]) is not { } name ||
                    ExactOffscreenText(row["name"]) != name ||
                    row["currentActivity"] is not JsonObject current ||
                    command["activityUpdate"] is not JsonObject patch || patch.Count == 0 ||
                    patch.Any(property => property.Key is not ("timeSpentMinutes" or "currentStepNumber") ||
                        property.Value is not JsonValue scalar || !scalar.TryGetValue<int>(out var number) || number < 0))
                    return null;

                foreach (var pair in patch) current[pair.Key] = pair.Value!.DeepClone();
                touched[id] = row;
            }
        }

        seen.Clear();
        if (terminal is not null)
        {
            foreach (var node in terminal)
            {
                if (node is not JsonObject command ||
                    !ExactOffscreenKeys(command, "NPCId", "NPCName", "activityName", "finalState", "finalOutcome", "narrativeSummary") ||
                    ExactOffscreenText(command["NPCId"]) is not { } id || !seen.Add(id) ||
                    FindHistorical(id) is not { } row || ExactOffscreenText(command["NPCName"]) is not { } name ||
                    ExactOffscreenText(row["name"]) != name ||
                    row["currentActivity"] is not JsonObject active ||
                    ExactOffscreenText(command["activityName"]) is not { } activity ||
                    ExactOffscreenText(active["activityName"]) != activity ||
                    row["completedActivities"] is not JsonArray history ||
                    ExactOffscreenText(command["finalState"]) is not { } state || state is not ("Completed" or "Abandoned") ||
                    ExactOffscreenText(command["finalOutcome"]) is not { } outcome ||
                    outcome is not ("Success" or "SuccessWithComplication" or "Failure") ||
                    state == "Abandoned" && outcome != "Failure" ||
                    ExactOffscreenText(command["narrativeSummary"]) is not { } summary)
                    return null;

                // Only the two fields changed by the terminal reducer, with the
                // signed original turn supplying completionTurn, not the command.
                history.Add(new JsonObject
                {
                    ["activityName"] = activity,
                    ["completionTurn"] = originalTurn,
                    ["finalOutcome"] = outcome,
                    ["narrativeSummary"] = summary
                });
                row["currentActivity"] = null;
                touched[id] = row;
            }
        }

        return touched.Count == 0 && !observeAcquisitions ? null : new OffscreenNpcProjection(expected, touched,
            terminal is not null || activities?["NPCActivityUpdates"] is JsonArray { Count: > 0 },
            rawActivities is null ? null : LocationHash(rawActivities), observeAcquisitions);

        JsonObject? FindHistorical(string id) => FindHistoricalOffscreenRow(expected, id);
    }

    private static JsonObject? FindHistoricalOffscreenRow(JsonObject root, string id)
    {
        if (root["NPCsInScene"] is not JsonArray scene || root["UpdateNPCs"] is not JsonArray historical ||
            scene.OfType<JsonObject>().Any(row => ExactOffscreenText(row["NPCId"]) == id))
            return null;
        var matches = historical.OfType<JsonObject>().Where(row => ExactOffscreenText(row["NPCId"]) == id).Take(2).ToArray();
        return matches.Length == 1 && matches[0]["materialization"] is JsonObject ? matches[0] : null;
    }

    private static bool ExactOffscreenKeys(JsonObject node, params string[] keys) =>
        node.Count == keys.Length && keys.All(node.ContainsKey);

    private static string? ExactOffscreenText(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) &&
        !string.IsNullOrWhiteSpace(text) && string.Equals(text, text.Trim(), StringComparison.Ordinal)
            ? text : null;

    private bool IsCompletedOffscreenNpcRow(JsonElement row, string section, string id)
    {
        if (!string.Equals(section, GuardianPolicyContracts.NpcCoreUpdateSectionName, StringComparison.Ordinal))
            return false;
        lock (_worldReadGate)
        {
            var invocation = _worldValidationInvocation.Value;
            if (invocation?.OffscreenNpcRows is null) return false;
            if (invocation.Closed || !ReferenceEquals(_worldReservation, invocation))
                throw new InvalidOperationException("Completed off-screen NPC comparison is no longer current.");
            return invocation.OffscreenNpcRows.MatchesRow(id, row);
        }
    }
}
