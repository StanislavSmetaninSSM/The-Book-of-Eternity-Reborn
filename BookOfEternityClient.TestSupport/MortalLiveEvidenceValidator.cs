using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.Tests;

// Read-only detached DATA check, never admission, source authentication or publication authority.
internal static partial class MortalLiveEvidenceValidator
{
    internal sealed record Observation(string SessionId, string RequestId, int Turn, string SnapshotToken,
        int PriorCount, int CurrentCount, string DeclarationFingerprint, string ReceiptFingerprint,
        bool PublicationAuthority = false);

    internal static Observation Validate(string? priorHistory, string currentHistory, string manifest, string request)
    {
        try
        {
            var options = LiveTurnPreparationService.ManifestJsonOptions;
            var snapshot = StrictJsonAuthority.Deserialize<LiveTurnPendingSnapshotManifest>(manifest, options, "Captured snapshot")
                ?? throw new InvalidDataException("Missing captured snapshot.");
            var original = Object(request, "Actual request");
            var session = Text(original, "sessionId");
            var requestId = Text(original, "requestId");
            var action = Text(original, "playerAction");
            var timestamp = Text(original, "timestamp");
            if (original["turnNumber"] is not JsonValue turnValue || !turnValue.TryGetValue<int>(out var turn) || turn < 0 ||
                snapshot.SessionId != session || snapshot.RequestId != requestId || snapshot.TurnNumber != turn ||
                snapshot.PlayerAction != action || snapshot.RequestTimestamp != timestamp)
                throw new InvalidDataException("Captured snapshot does not match the explicit original request.");
            var token = snapshot.ManifestPayloadHash;
            var computed = PendingTurnSnapshotAuthority.ComputeManifestPayloadHash(snapshot,
                LiveTurnPreparationService.ManifestHashJsonOptions, value => value.ManifestPayloadHash,
                (value, hash) => value.ManifestPayloadHash = hash);
            if (string.IsNullOrEmpty(token) || token != computed)
                throw new InvalidDataException("Captured snapshot payload hash does not match.");

            var prior = MortalLivingWorldReceipt.ValidateHistory(priorHistory is null ? null : Object(priorHistory, "Prior history"))["entries"]!.AsArray();
            var current = MortalLivingWorldReceipt.ValidateHistory(Object(currentHistory, "Current history"))["entries"]!.AsArray();
            if (current.Count != prior.Count + 1)
                throw new InvalidDataException("Expected exactly one appended receipt.");
            for (var i = 0; i < prior.Count; i++)
                if (!JsonNode.DeepEquals(prior[i], current[i]))
                    throw new InvalidDataException("Prior immutable receipt changed.");
            var last = current[^1]!.AsObject();
            if (Text(last, "sessionId") != session || Text(last, "requestId") != requestId ||
                last["turn"]!.GetValue<int>() != turn || Text(last, "snapshotToken") != token)
                throw new InvalidDataException("Appended receipt does not match captured request and snapshot.");
            return new(session, requestId, turn, token, prior.Count, current.Count,
                Text(last, "declarationFingerprint"), Text(last, "receiptFingerprint"));
        }
        catch (Exception error) when (error is JsonException or ArgumentException or InvalidOperationException or FormatException or OverflowException)
        { throw new InvalidDataException("Malformed typed live evidence.", error); }
    }

    private static JsonObject Object(string json, string name) =>
        StrictJsonAuthority.Deserialize<JsonObject>(json, LiveTurnPreparationService.ManifestJsonOptions, name)
        ?? throw new InvalidDataException($"Missing {name} object.");

    private static string Text(JsonObject obj, string name) =>
        obj[name] is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text)
            ? text : throw new InvalidDataException($"Missing explicit {name} string.");

    /// <summary>Loads detached outcome DATA; no runtime ownership or signature authentication is inferred.</summary>
    internal static Task<Outcome> ReadAsync(string baseline, string currentSession, string actualRequest) =>
        ReadOutcomeAsync(baseline, currentSession, actualRequest);

    private static async Task<string> ReadUtf8Async(string path)
    {
        var bytes = await File.ReadAllBytesAsync(path);
        var offset = bytes.Length >= 3 && bytes[0] == 0xef && bytes[1] == 0xbb && bytes[2] == 0xbf ? 3 : 0;
        return new UTF8Encoding(false, true).GetString(bytes, offset, bytes.Length - offset);
    }

    internal static async Task<int> RunAsync(string baseline, string currentSession, string actualRequest)
    {
        try
        {
            var result = await ReadAsync(baseline, currentSession, actualRequest);
            var wire = new Dictionary<string, object?>
            {
                ["valid"] = true, ["publicationAuthority"] = false, ["kind"] = result.Kind,
                ["sessionId"] = result.SessionId, ["requestId"] = result.RequestId, ["turn"] = result.Turn,
                ["snapshotToken"] = result.SnapshotToken, ["priorCount"] = result.PriorCount, ["currentCount"] = result.CurrentCount,
                ["historyBefore"] = new { present = result.HistoryBefore.Present, sha256 = result.HistoryBefore.Sha256 },
                ["historyAfter"] = new { present = result.HistoryAfter.Present, sha256 = result.HistoryAfter.Sha256 }
            };
            if (result.Receipt is { } receipt)
            {
                wire["declarationFingerprint"] = receipt.DeclarationFingerprint;
                wire["receiptFingerprint"] = receipt.ReceiptFingerprint;
            }
            else if (result.Applicability is { } context)
                wire["applicability"] = new { originalMinute = context.OriginalMinute, resultMinute = context.ResultMinute,
                    lastWorldMinute = context.LastWorldMinute, lastFactionMinute = context.LastFactionMinute,
                    worldCycleMinutes = context.WorldCycleMinutes, factionCycleMinutes = context.FactionCycleMinutes };
            Console.WriteLine(JsonSerializer.Serialize(wire));
            return 0;
        }
        catch (Exception error) when (error is InvalidDataException or IOException or UnauthorizedAccessException or ArgumentException or JsonException or InvalidOperationException or System.Security.SecurityException)
        {
            Console.WriteLine("{\"valid\":false,\"publicationAuthority\":false,\"errorCode\":\"typed_evidence_invalid\"}");
            return 2;
        }
    }
}
