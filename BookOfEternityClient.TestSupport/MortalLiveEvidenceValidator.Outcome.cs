using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.Tests;

internal static partial class MortalLiveEvidenceValidator
{
    internal sealed record HistoryWitness(bool Present, string? Sha256);
    internal sealed record Outcome(string Kind, string SessionId, string RequestId, int Turn, string SnapshotToken,
        int PriorCount, int CurrentCount, HistoryWitness HistoryBefore, HistoryWitness HistoryAfter,
        Observation? Receipt, MortalLivingWorldAssessmentContext? Applicability, bool PublicationAuthority = false);

    /// <summary>Checks detached captured DATA, never signatures or permission; changed history cannot downgrade to absence.</summary>
    internal static Outcome ValidateOutcome(string manifest, string request,
        IReadOnlyDictionary<string, byte[]?> originals, byte[] currentClock, byte[] currentSchedule,
        byte[]? currentHistory, byte[]? currentLivingReport, byte[]? currentProgressionReport)
    {
        RequireNoOriginalAliases(manifest);
        var snapshot = StrictJsonAuthority.Deserialize<LiveTurnPendingSnapshotManifest>(manifest,
            LiveTurnPreparationService.ManifestJsonOptions, "Captured snapshot") ?? throw new InvalidDataException("Missing snapshot.");
        var req = Object(request, "Actual request");
        var rawControl = Object(manifest, "Captured snapshot")["progressionControl"];
        if (rawControl is not JsonObject || req["progressionControl"] is not JsonObject ||
            !JsonNode.DeepEquals(rawControl, req["progressionControl"]) || snapshot.ProgressionControl is null ||
            !JsonNode.DeepEquals(rawControl, JsonSerializer.SerializeToNode(snapshot.ProgressionControl,
                LiveTurnPreparationService.ManifestJsonOptions)))
            throw new InvalidDataException("Raw captured/request progression control is not the exact closed control DTO.");
        var token = snapshot.ManifestPayloadHash;
        var computed = PendingTurnSnapshotAuthority.ComputeManifestPayloadHash(snapshot,
            LiveTurnPreparationService.ManifestHashJsonOptions, value => value.ManifestPayloadHash,
            (value, hash) => value.ManifestPayloadHash = hash);
        if (string.IsNullOrEmpty(token) || token != computed ||
            Text(req, "sessionId") != snapshot.SessionId || Text(req, "requestId") != snapshot.RequestId ||
            Text(req, "playerAction") != snapshot.PlayerAction || Text(req, "timestamp") != snapshot.RequestTimestamp ||
            req["turnNumber"] is not JsonValue tv || !tv.TryGetValue<int>(out var turn) || turn != snapshot.TurnNumber || turn < 0 ||
            snapshot.ProgressionControl is null || !JsonNode.DeepEquals(req["progressionControl"],
                JsonSerializer.SerializeToNode(snapshot.ProgressionControl, LiveTurnPreparationService.ManifestJsonOptions)))
            throw new InvalidDataException("Captured outcome source does not match the explicit request/control.");
        foreach (var logical in OriginalOutcomePaths)
        {
            if (!originals.TryGetValue(logical, out var bytes)) throw new InvalidDataException("Missing original input coverage.");
            RequireOriginalMapping(snapshot, logical, bytes);
        }
        if (currentLivingReport is not null) throw new InvalidDataException("Living-world report was not consumed.");
        var prior = originals[MortalLivingWorldSource.HistoryPath];
        var before = Witness(prior); var after = Witness(currentHistory);
        // Parse both even when bytes match: malformed unchanged history is not absence.
        var priorCount = MortalLivingWorldReceipt.ValidateHistory(prior is null ? null : Object(Decode(prior), "Prior history"))["entries"]!.AsArray().Count;
        if (currentHistory is not null) MortalLivingWorldReceipt.ValidateHistory(Object(Decode(currentHistory), "Current history"));
        if ((prior is null) != (currentHistory is null) || (prior is not null && !prior.AsSpan().SequenceEqual(currentHistory)))
        {
            var receipt = Validate(prior is null ? null : Decode(prior),
                currentHistory is null ? throw new InvalidDataException("History was removed.") : Decode(currentHistory), manifest, request);
            return new("ReceiptAppended", receipt.SessionId, receipt.RequestId, receipt.Turn, token,
                receipt.PriorCount, receipt.CurrentCount, before, after, receipt, null);
        }
        var control = snapshot.ProgressionControl;
        if (originals[MortalLivingWorldSource.ReportPath] is not null ||
            Text(Object(Decode(Required(originals, "game_state/meta/soul_state.json")), "Original soul"), "currentRealm") != "Mortal World")
            throw new InvalidDataException("No-receipt outcome requires original Mortal realm and absent original report.");
        var originalMinute = SettledMinute(Required(originals, MortalLivingWorldSource.WorldTimePath));
        var resultMinute = SettledMinute(currentClock);
        if (originalMinute != control.CurrentWorldTimeInMinutes)
            throw new InvalidDataException("Original clock differs from request control.");
        var context = new MortalLivingWorldAssessmentContext(snapshot.SessionId, snapshot.RequestId, turn,
            originalMinute, resultMinute, control.LastWorldSimulationTimeInMinutes, control.LastFactionSimulationTimeInMinutes,
            control.WorldCycleMinutes, control.FactionCycleMinutes);
        if (MortalLivingWorldAssessment.Validate(context, null).Count != 0)
            throw new InvalidDataException("A living-world receipt is required for this clock/control outcome.");
        // Strict duplicate-key parsing precedes the existing complete production schedule comparator.
        Object(Decode(Required(originals, ProgressionScheduleService.SchedulePath)), "Original schedule");
        Object(Decode(currentSchedule), "Current schedule");
        ProgressionScheduleService.RequirePublishedMortalTreatmentOutcome(control, snapshot.SessionId, snapshot.RequestId, turn,
            Required(originals, ProgressionScheduleService.SchedulePath), currentClock, currentSchedule, currentProgressionReport);
        return new("NotRequired", snapshot.SessionId, snapshot.RequestId, turn, token,
            priorCount, priorCount, before, after, null, context);
    }

    private static readonly string[] OriginalOutcomePaths = ["game_state/meta/soul_state.json", MortalLivingWorldSource.WorldTimePath,
        ProgressionScheduleService.SchedulePath, MortalLivingWorldSource.HistoryPath, MortalLivingWorldSource.ReportPath];

    /// <summary>Checks raw logical-key spelling before dictionary deserialization can collapse aliases.</summary>
    private static void RequireNoOriginalAliases(string manifest)
    {
        var root = Object(manifest, "Captured snapshot");
        foreach (var name in new[] { "files", "snapshotFileHashes", "originalPathPresenceV1" })
        {
            if (root[name] is not JsonObject entries) throw new InvalidDataException("Missing original source dictionary.");
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in entries)
            {
                var key = pair.Key;
                if (!seen.Add(key) || key.StartsWith('/') || key.Contains('\\') || key.Contains(':') ||
                    key.Split('/').Any(segment => segment is "" or "." or "..") ||
                    OriginalOutcomePaths.Any(logical => logical.Equals(key, StringComparison.OrdinalIgnoreCase) && logical != key))
                    throw new InvalidDataException("Aliased original source key.");
            }
        }
    }

    /// <summary>Requires explicit exact publisher mapping and exact byte hashes; absent DATA must be explicitly covered.</summary>
    private static void RequireOriginalMapping(LiveTurnPendingSnapshotManifest snapshot, string logical, byte[]? bytes)
    {
        var presence = snapshot.OriginalPathPresenceV1;
        if (presence is null || !presence.Keys.Contains(logical, StringComparer.Ordinal) || presence[logical] != (bytes is not null))
            throw new InvalidDataException("Original presence coverage differs.");
        if (bytes is null)
        {
            if (snapshot.Files.ContainsKey(logical) || snapshot.SnapshotFileHashes.ContainsKey(logical))
                throw new InvalidDataException("Absent original has a selected file/hash.");
            return;
        }
        if (!snapshot.Files.Keys.Contains(logical, StringComparer.Ordinal) || !snapshot.SnapshotFileHashes.Keys.Contains(logical, StringComparer.Ordinal) ||
            snapshot.Files[logical] != "game_state/control/pending_turn_snapshot/" + logical ||
            snapshot.SnapshotFileHashes[logical] != Convert.ToHexString(SHA256.HashData(bytes)))
            throw new InvalidDataException("Original mapping/hash differs from captured bytes.");
    }

    /// <summary>Reads only explicit captured members once; links are refused rather than followed into another source.</summary>
    private static async Task<Outcome> ReadOutcomeAsync(string baseline, string currentSession, string actualRequest)
    {
        var manifest = Decode((await ReadOutcomeBytesAsync(baseline, LiveTurnPreparationService.PendingTurnSnapshotManifestPath, false))!);
        RequireNoOriginalAliases(manifest);
        var snapshot = StrictJsonAuthority.Deserialize<LiveTurnPendingSnapshotManifest>(manifest,
            LiveTurnPreparationService.ManifestJsonOptions, "Captured snapshot") ?? throw new InvalidDataException("Missing snapshot.");
        var inputs = new Dictionary<string, byte[]?>(StringComparer.Ordinal);
        foreach (var logical in OriginalOutcomePaths)
        {
            if (snapshot.OriginalPathPresenceV1 is null || !snapshot.OriginalPathPresenceV1.Keys.Contains(logical, StringComparer.Ordinal))
                throw new InvalidDataException("Missing explicit original presence.");
            byte[]? bytes = null;
            if (snapshot.OriginalPathPresenceV1[logical])
            {
                if (!snapshot.Files.Keys.Contains(logical, StringComparer.Ordinal) || snapshot.Files[logical] != "game_state/control/pending_turn_snapshot/" + logical)
                    throw new InvalidDataException("Unexpected selected original path.");
                bytes = await ReadOutcomeBytesAsync(baseline, snapshot.Files[logical], false);
            }
            else if (await ReadOutcomeBytesAsync(baseline, "game_state/control/pending_turn_snapshot/" + logical, true) is not null)
                throw new InvalidDataException("Explicitly absent original has a physical captured member.");
            RequireOriginalMapping(snapshot, logical, bytes); inputs.Add(logical, bytes);
        }
        return ValidateOutcome(manifest, await ReadUtf8Async(actualRequest), inputs,
            (await ReadOutcomeBytesAsync(currentSession, MortalLivingWorldSource.WorldTimePath, false))!,
            (await ReadOutcomeBytesAsync(currentSession, ProgressionScheduleService.SchedulePath, false))!,
            await ReadOutcomeBytesAsync(currentSession, MortalLivingWorldSource.HistoryPath, true),
            await ReadOutcomeBytesAsync(currentSession, MortalLivingWorldSource.ReportPath, true),
            await ReadOutcomeBytesAsync(currentSession, ProgressionScheduleService.ReportPath, true));
    }

    /// <summary>Optional means physically absent only, never unreadable, empty, linked or malformed.</summary>
    private static async Task<byte[]?> ReadOutcomeBytesAsync(string root, string relative, bool optional)
    {
        var fullRoot = Path.GetFullPath(root);
        for (DirectoryInfo? ancestor = new(fullRoot); ancestor is not null; ancestor = ancestor.Parent)
            if ((ancestor.Attributes & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Linked evidence root/ancestor.");
        var cursor = fullRoot;
        foreach (var segment in relative.Split('/'))
        {
            if (segment is "" or "." or ".." || segment.Contains('\\') || segment.Contains(':')) throw new InvalidDataException("Invalid evidence path.");
            cursor = Path.Combine(cursor, segment);
            try { if ((File.GetAttributes(cursor) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Linked evidence path."); }
            catch (FileNotFoundException) { if (optional) return null; throw; }
            catch (DirectoryNotFoundException) { if (optional) return null; throw; }
        }
        if ((File.GetAttributes(fullRoot) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Linked evidence root.");
        return await File.ReadAllBytesAsync(cursor);
    }

    /// <summary>Requires settled clock DATA with no incremental or absolute pending command.</summary>
    private static long SettledMinute(byte[] bytes)
    {
        var value = Object(Decode(bytes), "Clock");
        if (value.ContainsKey("timeChange") || value.ContainsKey("setWorldTime") || value["currentTimeInMinutes"] is not JsonValue number ||
            !number.TryGetValue<long>(out var minute) || minute < 0) throw new InvalidDataException("Unsettled clock.");
        return minute;
    }
    /// <summary>Returns a required captured member without fallback to current or flat DATA.</summary>
    private static byte[] Required(IReadOnlyDictionary<string, byte[]?> inputs, string logical) =>
        inputs[logical] ?? throw new InvalidDataException("Required original is absent.");
    /// <summary>Preserves physical absence and hashes exact bytes, including a UTF8 BOM.</summary>
    private static HistoryWitness Witness(byte[]? bytes) => new(bytes is not null,
        bytes is null ? null : Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
    /// <summary>Decodes one optional BOM only after exact-byte evidence has been retained.</summary>
    private static string Decode(byte[] bytes) => CanonicalJsonUtf8.DecodeOneOptionalBom(bytes);
}
