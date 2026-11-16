using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal sealed record MortalLivingWorldPublicationPreparation(
    IReadOnlyList<ValidationIssue> Issues, MortalLivingWorldPublication? Publication);

/// <summary>
/// One ordinary canonical-refresh operation. Retains detached DATA, never reusable acceptance permission.
/// The enclosing refresh owns compensation of every returned before-image and every publication below.
/// This bounded adapter supports plain ordinary publication only. Treatment/diagnosis/deferred owners
/// must join their sealed cohort before publishing an assessment. Successful same-original reentry
/// remains fail-closed until an actual owner-issued replay witness is implemented; failed-write retry
/// uses the enclosing owner's exact compensation, not a serialized receipt as authority.
/// </summary>
internal sealed class MortalLivingWorldPublication
{
    private readonly MortalLivingWorldSource _source;
    private readonly JsonObject? _report;
    private readonly Dictionary<string, JsonObject> _originalDocuments = new(StringComparer.Ordinal);
    private readonly byte[]? _reportBefore;
    private readonly byte[]? _historyBefore;
    private Dictionary<string, byte[]?>? _finalImages;
    private byte[]? _historyAfter;
    private string? _evidenceFingerprint;
    private bool _published;

    private MortalLivingWorldPublication(MortalLivingWorldSource source)
    {
        _source = source;
        _reportBefore = source.ReadOpeningBytes(MortalLivingWorldSource.ReportPath);
        _historyBefore = source.ReadOpeningBytes(MortalLivingWorldSource.HistoryPath);
        _report = Root(_reportBefore, MortalLivingWorldSource.ReportPath);
        foreach (var path in MortalLivingWorldSource.DomainPaths)
            if (source.ReadOriginalDocument(path) is { } root) _originalDocuments.Add(path, root);
    }

    internal IReadOnlyList<MortalWoundTreatmentPublicationBeforeImage> BeforeImages =>
    [new(MortalLivingWorldSource.ReportPath, _reportBefore?.ToArray()),
        new(MortalLivingWorldSource.HistoryPath, _historyBefore?.ToArray())];

    internal static async Task<MortalLivingWorldPublicationPreparation> PrepareAsync(
        FileSystemManager files, FileSystemManager.CanonicalWriteLease lease)
    {
        using var fresh = FileSystemManager.SuppressValidationReuse();
        files.EnsureCanonicalWriteLeaseActive(lease);
        files.VerifyCurrentSessionOperation(lease);
        // Bare maintenance/bootstrap callers own no accepted original. A supplied report must
        // still authenticate; deleting the original cannot turn a submitted report into permission.
        var manifest = await files.ReadFileBytesAsync(lease, LiveTurnPreparationService.PendingTurnSnapshotManifestPath);
        if (manifest is null && await files.ReadFileBytesAsync(lease, MortalLivingWorldSource.ReportPath) is null &&
            await files.ReadFileBytesAsync(lease, LiveTurnPreparationService.TurnRequestPath) is null)
            return new([], null);
        PendingTurnSnapshotReadResult original;
        using (files.ForceFreshRecoveryValidation())
            original = PendingTurnSnapshotReader.ReadCurrent(files, lease, ["game_state/meta/soul_state.json"]);
        if (!original.Success || original.Snapshot is null) return new(original.Issues, null);
        if (original.Snapshot.Realm != "mortal_world") return new([], null);
        var preparation = await MortalLivingWorldSource.PrepareAsync(files, lease);
        if (preparation.Issues.Count != 0 || preparation.Source is null) return new(preparation.Issues, null);
        try
        {
            var publication = new MortalLivingWorldPublication(preparation.Source);
            publication.ValidateHistory();
            return new([], publication);
        }
        catch (Exception error) when (error is JsonException or ArgumentException or InvalidOperationException or InvalidDataException)
        { return new(Issue("history_invalid", "The signed prior living-world history is invalid: " + error.Message), null); }
    }

    internal Task RequireOpeningAsync(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease) =>
        _source.RequireUnchangedInputsAsync(files, lease);

    /// <summary>Reads settled outputs once, checks final time and declared effects, and retains exact publication guards.</summary>
    internal async Task<IReadOnlyList<ValidationIssue>> ValidateFinalAsync(FileSystemManager files,
        FileSystemManager.CanonicalWriteLease lease, bool supportsPublication)
    {
        using var fresh = FileSystemManager.SuppressValidationReuse();
        if (_published) throw new InvalidOperationException("A published living-world operation cannot prepare a second result.");
        _finalImages = null;
        _evidenceFingerprint = null;
        await _source.RequireOriginalCurrentAsync(files, lease);
        await RequireBytesAsync(files, lease, MortalLivingWorldSource.ReportPath, _reportBefore);
        await RequireBytesAsync(files, lease, MortalLivingWorldSource.HistoryPath, _historyBefore);
        var final = new Dictionary<string, byte[]?>(StringComparer.Ordinal);
        foreach (var path in MortalLivingWorldSource.DomainPaths.Append(MortalLivingWorldSource.WorldTimePath))
            final.Add(path, await files.ReadFileBytesAsync(lease, path));
        JsonObject? clock;
        try { clock = Root(final[MortalLivingWorldSource.WorldTimePath], MortalLivingWorldSource.WorldTimePath); }
        catch (Exception error) when (error is JsonException or ArgumentException or InvalidOperationException or InvalidDataException)
        { return Issue("clock_unresolved", "Settled Mortal time must be one canonical numeric clock."); }
        if (clock is null || clock["timeChange"] is not null || clock["setWorldTime"] is not null ||
            clock["currentTimeInMinutes"] is not JsonValue minuteValue || !minuteValue.TryGetValue<long>(out var minute) || minute < 0)
            return Issue("clock_unresolved", "Settled Mortal time must be a nonnegative numeric clock without pending time commands.");
        var context = _source.Context with { ResultMinute = minute };
        var due = minute >= context.LastWorldMinute && minute - context.LastWorldMinute >= context.WorldCycleMinutes ||
                  minute >= context.LastFactionMinute && minute - context.LastFactionMinute >= context.FactionCycleMinutes;
        if (!supportsPublication && (_report is not null || due))
            return Issue("owner_unsupported", "This treatment, diagnosis or deferred owner has no pre-seal living-world contribution yet. Client integration is required; rewriting the report cannot authorize a sidecar publication.");
        var issues = MortalLivingWorldAssessment.Validate(context, _report);
        if (issues.Count != 0) return issues;
        var current = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        try
        {
            foreach (var path in MortalLivingWorldSource.DomainPaths)
                if (Root(final[path], path) is { } root) current.Add(path, root);
        }
        catch (Exception error) when (error is JsonException or ArgumentException or InvalidOperationException or InvalidDataException)
        { return Issue("effect_invalid", "A settled living-world domain is not valid canonical object DATA: " + error.Message); }
        if (_report is not null)
        {
            issues = MortalLivingWorldEvents.Validate(_report, _originalDocuments, current);
            if (issues.Count != 0) return issues;
            // Reuse the already parsed settled DATA; do not parse every domain again for its receipt.
            var fields = new List<string?> { "book_of_eternity.mortal_living_world.settled_evidence.v1" };
            foreach (var path in final.Keys.OrderBy(path => path, StringComparer.Ordinal))
            {
                var root = path == MortalLivingWorldSource.WorldTimePath ? clock : current.GetValueOrDefault(path);
                fields.Add(path);
                fields.Add(root is null ? null : WoundAcceptedTurnFingerprintWriter.CanonicalJson(root));
            }
            _evidenceFingerprint = WoundAcceptedTurnFingerprintWriter.Compute(fields);
        }
        // Closing authentication cannot be replaced by any opening parse or effect validation result.
        await _source.RequireOriginalCurrentAsync(files, lease);
        _finalImages = final;
        return [];
    }

    /// <summary>Publishes only private evidence after validation; existing domain reducers retain all gameplay mutation authority.</summary>
    internal async Task PublishAsync(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease)
    {
        using var fresh = FileSystemManager.SuppressValidationReuse();
        if (_finalImages is null) throw new InvalidOperationException("Living-world final evidence has not been validated.");
        await RequireFinalInputsAsync(files, lease);
        if (_report is null) return;
        var history = ValidateHistory();
        var entries = history["entries"]!.AsArray();
        if (entries.OfType<JsonObject>().Any(row => row["snapshotToken"]?.GetValue<string>() == _source.SnapshotToken ||
            row["sessionId"]?.GetValue<string>() == _source.Context.SessionId &&
            row["requestId"]?.GetValue<string>() == _source.Context.RequestId && row["turn"]?.GetValue<int>() == _source.Context.Turn))
            throw new InvalidDataException("An assessment for this exact original already exists; serialized history grants no replay authority.");
        var entry = new JsonObject
        {
            ["sessionId"] = _source.Context.SessionId, ["requestId"] = _source.Context.RequestId,
            ["turn"] = _source.Context.Turn, ["snapshotToken"] = _source.SnapshotToken,
            ["report"] = _report.DeepClone(),
            ["evidenceFingerprint"] = _evidenceFingerprint ?? throw new InvalidOperationException("Missing settled evidence fingerprint.")
        };
        entry["receiptFingerprint"] = ReceiptFingerprint(entry);
        entries.Add(entry);
        var historyJson = history.ToJsonString();
        var writes = _finalImages.Select(pair => CoordinatedStateWriteHelper.CreateExactGuardWrite(pair.Key,
            new CanonicalBeforeImage(pair.Value is not null, pair.Value))).Concat(new[]
        {
            new CoordinatedStateWriteHelper.PlannedWrite(MortalLivingWorldSource.HistoryPath, null, historyJson,
                RequireCurrentBaseline: true, ExactPrevious: new(_historyBefore is not null, _historyBefore)),
            new CoordinatedStateWriteHelper.PlannedWrite(MortalLivingWorldSource.ReportPath, null, null,
                RequireCurrentBaseline: true, ExactPrevious: new(_reportBefore is not null, _reportBefore))
        }).ToArray();
        if (!await CoordinatedStateWriteHelper.TryCommitAsync(files, lease, writes))
            throw new InvalidDataException("Living-world private publication lost its exact current baseline.");
        _historyAfter = FileSystemManager.EncodeUtf8WithPreamble(historyJson);
        _published = true;
        await RequirePublishedAsync(files, lease);
    }

    internal async Task RequirePublishedAsync(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease)
    {
        using var fresh = FileSystemManager.SuppressValidationReuse();
        await _source.RequireOriginalCurrentAsync(files, lease);
        await RequireBytesAsync(files, lease, MortalLivingWorldSource.HistoryPath, _published ? _historyAfter : _historyBefore);
        await RequireBytesAsync(files, lease, MortalLivingWorldSource.ReportPath, _published ? null : _reportBefore);
        if (_finalImages is null) throw new InvalidOperationException("Living-world output is not validated.");
        foreach (var pair in _finalImages) await RequireBytesAsync(files, lease, pair.Key, pair.Value);
    }

    private async Task RequireFinalInputsAsync(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease)
    {
        using var fresh = FileSystemManager.SuppressValidationReuse();
        await _source.RequireOriginalCurrentAsync(files, lease);
        await RequireBytesAsync(files, lease, MortalLivingWorldSource.ReportPath, _reportBefore);
        await RequireBytesAsync(files, lease, MortalLivingWorldSource.HistoryPath, _historyBefore);
        foreach (var pair in _finalImages!) await RequireBytesAsync(files, lease, pair.Key, pair.Value);
    }

    private JsonObject ValidateHistory()
    {
        var history = Root(_historyBefore, MortalLivingWorldSource.HistoryPath) ??
            new JsonObject { ["schemaVersion"] = 1, ["entries"] = new JsonArray() };
        if (history.Count != 2 || history["schemaVersion"] is not JsonValue version || !version.TryGetValue<int>(out var number) ||
            number != 1 || history["entries"] is not JsonArray entries)
            throw new InvalidDataException("Expected exact schemaVersion=1 and entries.");
        var sources = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in entries)
        {
            if (node is not JsonObject entry || entry.Count != 7 || entry["report"] is not JsonObject ||
                entry["sessionId"] is not JsonValue || entry["requestId"] is not JsonValue ||
                entry["turn"] is not JsonValue turn || !turn.TryGetValue<int>(out var ordinal) || ordinal < 0 ||
                entry["snapshotToken"]?.GetValue<string>() is not { Length: > 0 } token || !sources.Add(token) ||
                entry["evidenceFingerprint"]?.GetValue<string>() is not { Length: > 0 } ||
                entry["receiptFingerprint"]?.GetValue<string>() != ReceiptFingerprint(entry))
                throw new InvalidDataException("Invalid or duplicate immutable assessment receipt.");
        }
        return history;
    }

    private static string ReceiptFingerprint(JsonObject entry)
    {
        var body = entry.DeepClone().AsObject();
        body.Remove("receiptFingerprint");
        return WoundAcceptedTurnFingerprintWriter.Compute([
            "book_of_eternity.mortal_living_world.receipt.v1", WoundAcceptedTurnFingerprintWriter.CanonicalJson(body)]);
    }

    private static async Task RequireBytesAsync(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease,
        string path, byte[]? expected)
    {
        using var fresh = FileSystemManager.SuppressValidationReuse();
        var current = await files.ReadFileBytesAsync(lease, path);
        if (expected is null ? current is not null : current is null || !expected.AsSpan().SequenceEqual(current))
            throw new InvalidDataException("Living-world publication input or output changed: " + path);
    }

    private static JsonObject? Root(byte[]? bytes, string path) => bytes is null ? null :
        StrictJsonAuthority.Deserialize<JsonObject>(CanonicalJsonUtf8.DecodeOneOptionalBom(bytes), new JsonSerializerOptions(), path)
        ?? throw new InvalidDataException("Expected object at " + path);

    private static IReadOnlyList<ValidationIssue> Issue(string code, string detail) =>
        [new ValidationIssue(MortalLivingWorldSource.ReportPath, IssueSeverity.Error, detail,
            code: "mortal_living_world_" + code, section: "MortalLivingWorld",
            repairHint: "Preserve the authenticated original and client history. Repair only the named clock, report or evidence; unsupported owners need client integration.")];
}
