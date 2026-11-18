using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;

namespace BookOfEternityClient.Services;

/// <summary>One lease-local living-world contribution retained by the genuine joint common owner.</summary>
internal sealed class MortalJointLivingWorldContribution
{
    private readonly MortalLivingWorldSource _source;
    private readonly JsonObject? _report;
    private readonly JsonObject? _priorHistory;
    private readonly Dictionary<string, CanonicalBeforeImage> _before;
    private readonly Dictionary<string, JsonObject> _originalDomains = new(StringComparer.Ordinal);
    private readonly HashSet<string> _originalAbsent = new(StringComparer.Ordinal);

    private MortalJointLivingWorldContribution(MortalLivingWorldSource source)
    {
        _source = source;
        _report = Parse(source.ReadOpeningBytes(MortalLivingWorldSource.ReportPath), MortalLivingWorldSource.ReportPath);
        _priorHistory = Parse(source.ReadOpeningBytes(MortalLivingWorldSource.HistoryPath), MortalLivingWorldSource.HistoryPath);
        MortalLivingWorldReceipt.ValidateHistory(_priorHistory);
        _before = MortalLivingWorldSource.ObservedPaths.Distinct(StringComparer.Ordinal).ToDictionary(path => path,
            path => { var bytes = source.ReadOpeningBytes(path); return new CanonicalBeforeImage(bytes is not null, bytes); },
            StringComparer.Ordinal);
        foreach (var path in MortalLivingWorldSource.DomainPaths)
        {
            var original = source.ReadOriginalDocument(path);
            if (original is null) _originalAbsent.Add(path);
            else _originalDomains.Add(path, original);
        }
        Receipt = _report is null ? null : MortalLivingWorldReceipt.Build(source.SnapshotToken, source.Context, _report, _priorHistory);
        var fields = new List<string?> { "book_of_eternity.mortal_joint_repair.living_world_contribution.v1",
            source.SnapshotToken, source.Context.SessionId, source.Context.RequestId,
            source.Context.Turn.ToString(System.Globalization.CultureInfo.InvariantCulture),
            source.Context.OriginalMinute.ToString(System.Globalization.CultureInfo.InvariantCulture),
            source.Context.ResultMinute.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Receipt is null ? null : WoundAcceptedTurnFingerprintWriter.CanonicalJson(Receipt.Receipt) };
        foreach (var row in _before.OrderBy(row => row.Key, StringComparer.Ordinal))
        { fields.Add(row.Key); fields.Add(row.Value.Fingerprint); }
        Fingerprint = WoundAcceptedTurnFingerprintWriter.Compute(fields);
    }

    internal string Fingerprint { get; }
    internal MortalLivingWorldReceiptData? Receipt { get; }
    internal JsonObject? Report => _report?.DeepClone().AsObject();
    internal JsonObject? HistorySuccessor => Receipt is null ? null : JsonNode.Parse(Receipt.HistoryJson)!.AsObject();
    internal JsonObject MarkerDeclaration => new()
    {
        ["report"] = _report?.DeepClone(),
        ["receiptFingerprint"] = Receipt?.Receipt["receiptFingerprint"]?.DeepClone()
    };
    internal IReadOnlyDictionary<string, CanonicalBeforeImage> BeforeImages => _before.ToDictionary(row => row.Key,
        row => new CanonicalBeforeImage(row.Value.Existed, row.Value.Bytes?.ToArray()), StringComparer.Ordinal);

    internal static async Task<(MortalJointLivingWorldContribution? Contribution, IReadOnlyList<ValidationIssue> Issues)> PrepareAsync(
        FileSystemManager files, FileSystemManager.CanonicalWriteLease lease)
    {
        var source = await MortalLivingWorldSource.PrepareAsync(files, lease);
        if (source.Source is null || source.Issues.Count != 0) return (null, source.Issues);
        try { return (new MortalJointLivingWorldContribution(source.Source), []); }
        catch (Exception error) when (error is InvalidDataException or JsonException or ArgumentException or InvalidOperationException)
        { return (null, [Issue("history_invalid", error.Message)]); }
    }

    internal Task ValidateOpeningAsync(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease) =>
        _source.RequireUnchangedInputsAsync(files, lease);

    /// <summary>Required final, physical check before the joint transaction can mint a readback proof.</summary>
    internal async Task ValidatePublishedAsync(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease)
    {
        using var fresh = FileSystemManager.SuppressValidationReuse();
        await _source.RequireOriginalCurrentAsync(files, lease);
        var clock = Parse(await files.ReadFileBytesAsync(lease, MortalLivingWorldSource.WorldTimePath), MortalLivingWorldSource.WorldTimePath);
        if (clock is null || clock.ContainsKey("timeChange") || clock.ContainsKey("setWorldTime") ||
            clock["currentTimeInMinutes"] is not JsonValue minuteValue ||
            !minuteValue.TryGetValue<long>(out var minute) || minute < 0)
            throw new InvalidDataException("Joint living-world completion requires a settled numeric clock.");
        var context = _source.Context with { ResultMinute = minute };
        var admission = MortalLivingWorldAssessment.Validate(context, _report);
        if (admission.Count != 0)
            throw new InvalidDataException("Joint living-world final clock/report disagreement: " + string.Join(";", admission.Select(row => row.Code)));
        var current = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        foreach (var path in MortalLivingWorldSource.DomainPaths)
            if (Parse(await files.ReadFileBytesAsync(lease, path), path) is { } root) current.Add(path, root);
        if (_report is not null)
        {
            var effectIssues = MortalLivingWorldEvents.Validate(_report, _originalDomains, current, _originalAbsent);
            if (effectIssues.Count != 0)
                throw new InvalidDataException("Joint living-world final effects disagree: " + string.Join(";", effectIssues.Select(row => row.Code)));
        }
        if (await files.ReadFileBytesAsync(lease, MortalLivingWorldSource.ReportPath) is not null)
            throw new InvalidDataException("Joint living-world report was not consumed in the common cohort.");
        var history = Parse(await files.ReadFileBytesAsync(lease, MortalLivingWorldSource.HistoryPath), MortalLivingWorldSource.HistoryPath);
        var expected = Receipt is null ? _priorHistory : JsonNode.Parse(Receipt.HistoryJson)!.AsObject();
        if (!JsonNode.DeepEquals(history, expected))
            throw new InvalidDataException("Joint living-world published history does not match its accepted source/report.");
        await _source.RequireOriginalCurrentAsync(files, lease);
    }

    /// <summary>Fresh read-only phase proof from the signed original, marker declaration and actual final bytes.</summary>
    internal static async Task ValidateCompletedAsync(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease,
        JsonObject marker)
    {
        using var fresh = FileSystemManager.SuppressValidationReuse();
        if (marker["livingWorld"] is not JsonObject declaration || declaration.Count != 2 ||
            !declaration.ContainsKey("report") || !declaration.ContainsKey("receiptFingerprint"))
            throw new InvalidDataException("Joint living-world declaration shape changed.");
        var report = declaration["report"] is null ? null : declaration["report"] is JsonObject submitted
            ? submitted.DeepClone().AsObject() : throw new InvalidDataException("Joint living-world report declaration is not an object.");
        var selection = PendingTurnSnapshotPathSelection.CreateWithObservedOptionalPaths(
            ["game_state/meta/soul_state.json", MortalLivingWorldSource.WorldTimePath, ProgressionScheduleService.SchedulePath],
            MortalLivingWorldSource.DomainPaths.Concat([MortalLivingWorldSource.ReportPath, MortalLivingWorldSource.HistoryPath]));
        var read = PendingTurnSnapshotReader.ReadCurrent(files, lease, selection);
        if (!read.Success || read.Snapshot is not { } signed || signed.Realm != "mortal_world" ||
            signed.SessionId != marker["sessionId"]?.GetValue<string>() ||
            signed.RequestId != marker["requestId"]?.GetValue<string>() ||
            signed.SnapshotToken != marker["snapshotToken"]?.GetValue<string>() ||
            signed.TurnNumber != marker["turn"]?.GetValue<int>())
            throw new InvalidDataException("Joint living-world signed original changed.");
        byte[]? Original(string path) => signed.AbsentLogicalPaths.Contains(path, StringComparer.Ordinal)
            ? null : signed.ReadRequiredBytes(path);
        // A post-original GM report is client-issued comparison DATA retained by the accepted
        // joint marker, not cryptographic provenance. Portable storage does not resist a coherent
        // rewrite of the affected joint marker and living history with their public hashes;
        // original, clock and domains can stay unchanged. This binds a signed report when present.
        var signedReport = Parse(Original(MortalLivingWorldSource.ReportPath), MortalLivingWorldSource.ReportPath);
        if (signedReport is not null && !JsonNode.DeepEquals(signedReport, report))
            throw new InvalidDataException("Joint living-world report differs from signed original evidence.");
        var originalClock = Parse(Original(MortalLivingWorldSource.WorldTimePath), MortalLivingWorldSource.WorldTimePath)
            ?? throw new InvalidDataException("Joint living-world signed clock is absent.");
        var currentClock = Parse(await files.ReadFileBytesAsync(lease, MortalLivingWorldSource.WorldTimePath),
            MortalLivingWorldSource.WorldTimePath) ?? throw new InvalidDataException("Joint living-world final clock is absent.");
        var controlRoot = signed.ReadOriginalControl() ?? throw new InvalidDataException("Joint living-world signed control is absent.");
        var control = controlRoot.Deserialize<ProgressionControl>() ?? throw new InvalidDataException("Joint living-world signed control is malformed.");
        var schedule = Parse(Original(ProgressionScheduleService.SchedulePath), ProgressionScheduleService.SchedulePath)
            ?? throw new InvalidDataException("Joint living-world signed schedule is absent.");
        if (!Numeric(originalClock, "currentTimeInMinutes", out var originalMinute) ||
            originalMinute != control.CurrentWorldTimeInMinutes || originalClock.ContainsKey("timeChange") ||
            originalClock.ContainsKey("setWorldTime") || !Numeric(currentClock, "currentTimeInMinutes", out var resultMinute) ||
            resultMinute < originalMinute || currentClock.ContainsKey("timeChange") || currentClock.ContainsKey("setWorldTime") ||
            !Numeric(schedule, "currentWorldTimeInMinutes", out var scheduleMinute) ||
            scheduleMinute != originalMinute ||
            !Numeric(schedule, "lastWorldSimulationTimeInMinutes", out var lastWorld) ||
            lastWorld != control.LastWorldSimulationTimeInMinutes ||
            !Numeric(schedule, "lastFactionSimulationTimeInMinutes", out var lastFaction) ||
            lastFaction != control.LastFactionSimulationTimeInMinutes ||
            !Numeric(schedule, "worldCycleMinutes", out var worldCycle) || worldCycle != control.WorldCycleMinutes ||
            !Numeric(schedule, "factionCycleMinutes", out var factionCycle) || factionCycle != control.FactionCycleMinutes)
            throw new InvalidDataException("Joint living-world signed schedule and settled clock disagree.");
        var context = new MortalLivingWorldAssessmentContext(signed.SessionId, signed.RequestId, signed.TurnNumber,
            originalMinute, resultMinute, lastWorld, lastFaction, control.WorldCycleMinutes, control.FactionCycleMinutes);
        var admission = MortalLivingWorldAssessment.Validate(context, report);
        if (admission.Count != 0)
            throw new InvalidDataException("Joint living-world completed assessment is invalid: " + string.Join(";", admission.Select(row => row.Code)));
        var prior = Parse(Original(MortalLivingWorldSource.HistoryPath), MortalLivingWorldSource.HistoryPath);
        MortalLivingWorldReceipt.ValidateHistory(prior);
        var receipt = report is null ? null : MortalLivingWorldReceipt.Build(signed.SnapshotToken, context, report, prior);
        if (declaration["receiptFingerprint"]?.GetValue<string>() != receipt?.Receipt["receiptFingerprint"]?.GetValue<string>())
            throw new InvalidDataException("Joint living-world completed receipt declaration changed.");
        var history = Parse(await files.ReadFileBytesAsync(lease, MortalLivingWorldSource.HistoryPath), MortalLivingWorldSource.HistoryPath);
        var expectedHistory = receipt is null ? prior : JsonNode.Parse(receipt.HistoryJson)!.AsObject();
        if (!JsonNode.DeepEquals(history, expectedHistory) ||
            await files.ReadFileBytesAsync(lease, MortalLivingWorldSource.ReportPath) is not null)
            throw new InvalidDataException("Joint living-world completed history/report differs from its bound declaration.");
        if (report is not null)
        {
            var originals = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
            var current = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
            var absent = new HashSet<string>(StringComparer.Ordinal);
            foreach (var path in MortalLivingWorldSource.DomainPaths)
            {
                if (Parse(Original(path), path) is { } oldRoot) originals.Add(path, oldRoot);
                else absent.Add(path);
                if (Parse(await files.ReadFileBytesAsync(lease, path), path) is { } newRoot) current.Add(path, newRoot);
            }
            var effects = MortalLivingWorldEvents.Validate(report, originals, current, absent);
            if (effects.Count != 0)
                throw new InvalidDataException("Joint living-world completed effects differ from signed source: " +
                    string.Join(";", effects.Select(row => row.Code)));
        }
    }

    private static bool Numeric(JsonObject root, string key, out long value)
    {
        value = 0;
        return root[key] is JsonValue node && (node.TryGetValue<long>(out value) ||
            node.TryGetValue<int>(out var small) && (value = small) == small) && value >= 0;
    }

    private static JsonObject? Parse(byte[]? bytes, string path) => bytes is null ? null :
        StrictJsonAuthority.Deserialize<JsonObject>(CanonicalJsonUtf8.DecodeOneOptionalBom(bytes), new JsonSerializerOptions(), path)
            ?? throw new InvalidDataException("Null living-world root: " + path);
    private static ValidationIssue Issue(string code, string detail) => new(MortalLivingWorldSource.HistoryPath,
        IssueSeverity.Error, detail, code: "mortal_living_world_" + code, section: "MortalLivingWorld");
}
