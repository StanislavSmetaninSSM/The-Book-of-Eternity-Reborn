using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

/// <summary>Independent receipt/history DATA, never source authentication or publication permission.</summary>
internal sealed record MortalLivingWorldReceiptData(string HistoryJson, byte[] HistoryBytes, JsonObject Receipt);

/// <summary>Deterministic private receipt declarations shared by ordinary and pre-seal owners.</summary>
internal static class MortalLivingWorldReceipt
{
    private const int Revision = 2;
    private const string DeclarationDomain = "book_of_eternity.mortal_living_world.assessment_declaration.v2";
    private const string ReceiptDomain = "book_of_eternity.mortal_living_world.receipt.v2";

    /// <summary>Validates and independently copies current revision history; it grants no replay authority.</summary>
    internal static JsonObject ValidateHistory(JsonObject? history)
    {
        try
        {
            var copy = history?.DeepClone().AsObject() ??
                new JsonObject { ["schemaVersion"] = Revision, ["entries"] = new JsonArray() };
            if (!Shape(copy, "schemaVersion", "entries") || !Integer(copy["schemaVersion"], out var revision) ||
                revision != Revision || copy["entries"] is not JsonArray entries)
                throw new InvalidDataException("Expected exact current living-world history schemaVersion=2 and entries.");
            var snapshots = new HashSet<string>(StringComparer.Ordinal);
            var sources = new HashSet<string>(StringComparer.Ordinal);
            foreach (var node in entries)
            {
                if (node is not JsonObject entry || !Shape(entry, "receiptRevision", "snapshotToken", "sessionId", "requestId", "turn",
                        "fromMinute", "toMinute", "report", "declarationFingerprint", "receiptFingerprint") ||
                    !Integer(entry["receiptRevision"], out var entryRevision) || entryRevision != Revision ||
                    Text(entry["snapshotToken"]) is not { } snapshot || Text(entry["sessionId"]) is not { } session ||
                    Text(entry["requestId"]) is not { } request || !Integer(entry["turn"], out var turn) || turn < 0 || turn > int.MaxValue ||
                    !Integer(entry["fromMinute"], out var from) || !Integer(entry["toMinute"], out var to) || from < 0 || to < from ||
                    entry["report"] is not JsonObject report || !snapshots.Add(snapshot) || !sources.Add(SourceKey(session, request, turn)))
                    throw new InvalidDataException("Invalid or duplicate immutable living-world receipt source.");
                RequireStoredReport(report, session, request, turn, from, to);
                if (Text(entry["declarationFingerprint"]) != DeclarationFingerprint(snapshot, session, request, turn, from, to, report) ||
                    Text(entry["receiptFingerprint"]) != ReceiptFingerprint(entry))
                    throw new InvalidDataException("Living-world receipt declarations or fingerprint were changed.");
            }
            return copy;
        }
        catch (Exception error) when (error is JsonException or ArgumentException or InvalidOperationException)
        { throw new InvalidDataException("Malformed living-world receipt history DATA.", error); }
    }

    /// <summary>Appends exact source/report/window declarations without validating actual domain effects or retaining caller DATA.</summary>
    internal static MortalLivingWorldReceiptData Build(string snapshotToken, MortalLivingWorldAssessmentContext context,
        JsonObject report, JsonObject? priorHistory)
    {
        try
        {
            if (!Exact(snapshotToken) || context is null || report is null || MortalLivingWorldAssessment.Validate(context, report).Count != 0)
                throw new InvalidDataException("Receipt declarations require an exact source and admitted numeric assessment window.");
            if (!Integer(report["fromMinute"], out var from) || !Integer(report["toMinute"], out var to))
                throw new InvalidDataException("Receipt assessment window must use exact integer minutes.");
            var history = ValidateHistory(priorHistory);
            var entries = history["entries"]!.AsArray();
            if (entries.OfType<JsonObject>().Any(entry => Text(entry["snapshotToken"]) == snapshotToken ||
                Text(entry["sessionId"]) == context.SessionId && Text(entry["requestId"]) == context.RequestId &&
                Integer(entry["turn"], out var turn) && turn == context.Turn))
                throw new InvalidDataException("An assessment for this original source already exists; history grants no replay authority.");
            var receipt = new JsonObject
            {
                ["receiptRevision"] = Revision, ["snapshotToken"] = snapshotToken,
                ["sessionId"] = context.SessionId, ["requestId"] = context.RequestId, ["turn"] = context.Turn,
                ["fromMinute"] = from, ["toMinute"] = to, ["report"] = report.DeepClone(),
                ["declarationFingerprint"] = DeclarationFingerprint(snapshotToken, context.SessionId, context.RequestId, context.Turn, from, to, report)
            };
            receipt["receiptFingerprint"] = ReceiptFingerprint(receipt);
            entries.Add(receipt);
            var historyJson = WoundAcceptedTurnFingerprintWriter.CanonicalJson(history)!;
            var historyBytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(historyJson)).ToArray();
            return new(historyJson, historyBytes, receipt.DeepClone().AsObject());
        }
        catch (Exception error) when (error is JsonException or ArgumentException or InvalidOperationException)
        { throw new InvalidDataException("Malformed living-world receipt declaration DATA.", error); }
    }

    // Historical receipts retain their assessed window, not the original scheduler. Check stored admission
    // shape/correlation directly rather than inventing a clock/schedule or re-running historical effects.
    private static void RequireStoredReport(JsonObject report, string session, string request, long turn, long from, long to)
    {
        if (!Shape(report, "schemaVersion", "sessionId", "requestId", "turn", "fromMinute", "toMinute", "resolution", "audit", "events") ||
            !Integer(report["schemaVersion"], out var revision) || revision != 1 || Text(report["sessionId"]) != session ||
            Text(report["requestId"]) != request || !Integer(report["turn"], out var reportTurn) || reportTurn != turn ||
            !Integer(report["fromMinute"], out var reportFrom) || reportFrom != from || !Integer(report["toMinute"], out var reportTo) || reportTo != to ||
            Text(report["resolution"]) is not ("changes" or "noSignificantChange") || report["audit"] is not JsonArray audit || audit.Count != 5 ||
            report["events"] is not JsonArray events || (Text(report["resolution"]) == "changes"
                ? events.Count == 0 || events.Any(node => node is not JsonObject) : events.Count != 0))
            throw new InvalidDataException("Stored receipt report does not match its exact source, shape and numeric window.");
        var scopes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in audit)
            if (node is not JsonObject row || !Shape(row, "scope", "reason") || Text(row["scope"]) is not { } scope ||
                scope is not ("npcs" or "factions" or "locations" or "projects" or "world") || !scopes.Add(scope) ||
                row["reason"] is not JsonValue reason || !reason.TryGetValue<string>(out var text) || string.IsNullOrWhiteSpace(text))
                throw new InvalidDataException("Stored receipt report must retain all five exact audit scopes and reasons.");
    }

    private static string DeclarationFingerprint(string snapshot, string session, string request, long turn, long from, long to, JsonObject report) =>
        WoundAcceptedTurnFingerprintWriter.Compute([DeclarationDomain, snapshot, session, request,
            turn.ToString(CultureInfo.InvariantCulture), from.ToString(CultureInfo.InvariantCulture), to.ToString(CultureInfo.InvariantCulture),
            WoundAcceptedTurnFingerprintWriter.CanonicalJson(report)]);

    private static string ReceiptFingerprint(JsonObject entry)
    {
        var body = entry.DeepClone().AsObject();
        body.Remove("receiptFingerprint");
        return WoundAcceptedTurnFingerprintWriter.Compute([ReceiptDomain, WoundAcceptedTurnFingerprintWriter.CanonicalJson(body)]);
    }

    private static string SourceKey(string session, string request, long turn) => new JsonArray(session, request, turn).ToJsonString();
    private static bool Shape(JsonObject obj, params string[] keys) => obj.Count == keys.Length && keys.All(obj.ContainsKey);
    private static bool Exact(string value) => !string.IsNullOrWhiteSpace(value) && value == value.Trim();
    private static string? Text(JsonNode? node) => node is JsonValue value && value.TryGetValue<string>(out var text) &&
        text is not null && Exact(text) ? text : null;
    private static bool Integer(JsonNode? node, out long number)
    {
        number = 0;
        return node is JsonValue value && (value.TryGetValue<long>(out number) || value.TryGetValue<int>(out var small) && (number = small) == small);
    }
}
