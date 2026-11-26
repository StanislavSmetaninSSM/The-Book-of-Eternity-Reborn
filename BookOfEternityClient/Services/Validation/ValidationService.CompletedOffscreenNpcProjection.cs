using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    internal const string OffscreenNpcActivityPath = "game_state/npcs/npc_activities.json";
    private const string TerminalNpcActivityCommands = "completeNPCActivities";

    // Immutable, operation-local DATA. The ordinary publication comparison owns
    // source identity and exact bytes at both ends of the final validation lease.
    internal sealed class OffscreenNpcProjection(JsonObject expected, IReadOnlyDictionary<string, string> rows,
        bool consumedActivity, string? originalActivityHash)
    {
        internal bool MatchesCompleteRoot(byte[] bytes) =>
            TryReadOffscreenObject(bytes) is { } actual && JsonNode.DeepEquals(expected, actual);

        internal bool MatchesActivityAfterImage(byte[]? bytes) => consumedActivity
            ? bytes is not null && TryReadOffscreenObject(bytes) is { Count: 0 }
            : (bytes is null ? null : LocationHash(bytes)) == originalActivityHash;

        internal bool MatchesRow(string id, JsonElement row) =>
            rows.TryGetValue(id, out var expectedRow) && JsonValuesSemanticallyEqual(expectedRow, row.GetRawText());
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
        byte[]? originalCore, byte[]? rawCore, byte[]? rawActivities, int originalTurn)
    {
        var expected = TryReadOffscreenObject(originalCore);
        var raw = TryReadOffscreenObject(rawCore);
        if (expected is null || raw is null || expected.ContainsKey(NpcCoreChangesContract.PropertyName) ||
            expected["UpdateNPCs"] is not JsonArray historical || expected["NPCsInScene"] is not JsonArray scene)
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

        return touched.Count == 0 ? null : new OffscreenNpcProjection(expected,
            touched.ToDictionary(pair => pair.Key, pair => pair.Value.ToJsonString(), StringComparer.Ordinal),
            terminal is not null || activities?["NPCActivityUpdates"] is JsonArray { Count: > 0 },
            rawActivities is null ? null : LocationHash(rawActivities));

        JsonObject? FindHistorical(string id)
        {
            if (scene.OfType<JsonObject>().Any(row => ExactOffscreenText(row["NPCId"]) == id)) return null;
            var matches = historical.OfType<JsonObject>().Where(row => ExactOffscreenText(row["NPCId"]) == id).Take(2).ToArray();
            return matches.Length == 1 && matches[0]["materialization"] is JsonObject ? matches[0] : null;
        }
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
