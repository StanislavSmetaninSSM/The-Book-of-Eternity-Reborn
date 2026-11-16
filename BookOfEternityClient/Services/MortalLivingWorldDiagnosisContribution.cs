using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal sealed record MortalLivingWorldDiagnosisPreparation(IReadOnlyList<ValidationIssue> Issues,
    MortalLivingWorldDiagnosisContribution? Contribution);

/// <summary>One actual diagnosis owner's pre-seal contribution. Detached declarations are not replay permission.</summary>
internal sealed class MortalLivingWorldDiagnosisContribution
{
    private readonly FileSystemManager _files;
    private readonly MortalLivingWorldSource _source;
    private readonly MortalWoundTreatmentAcceptedStateAuthority _owner;
    private readonly MortalWoundDiagnosisWorkingProjection _working;
    private readonly string _progressionFingerprint;
    private readonly JsonObject _report;
    private readonly byte[]? _reportBefore;
    private readonly byte[]? _historyBefore;
    private readonly string _historyJson;
    private readonly JsonObject _control;
    private readonly JsonObject _clockAfter;
    private readonly MortalLivingWorldAssessmentContext _context;
    private readonly string _snapshotToken;
    private readonly IReadOnlyDictionary<string, byte[]?> _openingGuards;
    private readonly IReadOnlyDictionary<string, byte[]?> _opening;
    private readonly Dictionary<string, JsonObject> _original = new(StringComparer.Ordinal);
    private readonly HashSet<string> _absent = new(StringComparer.Ordinal);

    private MortalLivingWorldDiagnosisContribution(FileSystemManager files, MortalLivingWorldSource source,
        MortalWoundTreatmentAcceptedStateAuthority owner, MortalWoundDiagnosisWorkingProjection working,
        ProgressionScheduleService.FrozenMortalCalculation progression, JsonObject report)
    {
        _files = files; _source = source; _owner = owner; _working = working;
        _context = source.Context; _snapshotToken = source.SnapshotToken; _clockAfter = working.ClockAfter;
        _openingGuards = source.ReadOpeningGuardData(); _opening = source.ReadOpeningData();
        _progressionFingerprint = progression.MutationFingerprint; _control = working.OriginalControl;
        _report = report.DeepClone().AsObject();
        _reportBefore = source.ReadOpeningBytes(MortalLivingWorldSource.ReportPath);
        _historyBefore = source.ReadOpeningBytes(MortalLivingWorldSource.HistoryPath);
        var receipt = MortalLivingWorldReceipt.Build(source.SnapshotToken, source.Context, _report,
            Root(_historyBefore, MortalLivingWorldSource.HistoryPath));
        _historyJson = receipt.HistoryJson;
        Fingerprint = ComputeFingerprint(working.Fingerprint, progression.MutationFingerprint, receipt.Receipt["receiptFingerprint"]!.GetValue<string>());
        foreach (var path in MortalLivingWorldSource.DomainPaths)
            if (source.ReadOriginalDocument(path) is { } root) _original.Add(path, root);
            else _absent.Add(path);
    }

    internal static string ComputeFingerprint(string working, string progression, string receipt) =>
        WoundAcceptedTurnFingerprintWriter.Compute(new[] { "mortal_living_world.diagnosis.v1", working, progression, receipt });

    internal JsonObject ToComparisonData() => new()
    {
        ["schemaVersion"] = 1, ["snapshotToken"] = _snapshotToken,
        ["context"] = JsonSerializer.SerializeToNode(_context), ["report"] = _report.DeepClone(),
        ["historyBefore"] = Root(_historyBefore, MortalLivingWorldSource.HistoryPath), ["historyAfter"] = HistoryAfter
    };

    internal sealed record Comparison(MortalLivingWorldAssessmentContext Context, string SnapshotToken,
        JsonObject Report, MortalLivingWorldReceiptData Receipt);

    /// <summary>Closed comparison DATA only. Rebuilding a receipt does not authenticate an original or authorize replay.</summary>
    internal static Comparison ParseComparison(JsonObject value)
    {
        var names = new[] { "schemaVersion", "snapshotToken", "context", "report", "historyBefore", "historyAfter" };
        if (value.Count != names.Length || names.Any(name => !value.ContainsKey(name)) ||
            value["schemaVersion"]?.GetValue<int>() != 1 || value["snapshotToken"] is not JsonValue token ||
            !token.TryGetValue<string>(out var snapshot) || value["context"] is not JsonObject contextRoot ||
            value["report"] is not JsonObject report || value["historyBefore"] is not null and not JsonObject ||
            value["historyAfter"] is not JsonObject history)
            throw new InvalidDataException("Malformed living-world diagnosis comparison DATA.");
        var context = contextRoot.Deserialize<MortalLivingWorldAssessmentContext>() ?? throw new InvalidDataException("Missing living-world context.");
        if (!JsonNode.DeepEquals(contextRoot, JsonSerializer.SerializeToNode(context)) || report["resolution"]?.GetValue<string>() != "noSignificantChange")
            throw new InvalidDataException("Unknown living-world context fields or unsupported diagnosis event changes.");
        var receipt = MortalLivingWorldReceipt.Build(snapshot, context, report, value["historyBefore"] as JsonObject);
        if (!JsonNode.DeepEquals(history, JsonNode.Parse(receipt.HistoryJson)))
            throw new InvalidDataException("Living-world comparison history does not recompose from exact declarations.");
        return new(context, snapshot, report.DeepClone().AsObject(), receipt);
    }

    internal string Fingerprint { get; }
    internal Task RequireOpeningAsync(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease) =>
        _source.RequireUnchangedInputsAsync(files, lease);
    /// <summary>Readmits source under a genuine retained-plan owner; copied opening DATA cannot authorize this transition.</summary>
    internal async Task RequireRetainedOpeningAsync(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease,
        MortalWoundTreatmentAcceptedStateAuthority accepted, MortalWoundTreatmentAttemptRequest request,
        MortalWoundTreatmentResourcePublicationAuthority publication)
    {
        using var fresh = FileSystemManager.SuppressValidationReuse();
        if (!ReferenceEquals(files, _files) ||
            publication.ContinuationAuthority is not WoundAcceptedTurnPlanner.MortalDiagnosisContinuation continuation ||
            !ReferenceEquals(continuation.LivingWorld, this) ||
            !AcceptedTurnAuthorityRegistry.CanReuseMortalDiagnosisPublication(files, lease, accepted, request, publication))
            throw new InvalidDataException("Living-world retained opening requires the actual current diagnosis handoff.");
        var outcome = MortalWoundDiagnosisOutcome.Resolve(accepted,
            new WoundHistoryParseResult(accepted.History, Array.Empty<ValidationIssue>()), request);
        var working = MortalWoundDiagnosisWorkingProjection.Build(accepted, outcome);
        var prepared = await MortalLivingWorldSource.PrepareForDiagnosisAsync(files, lease, accepted, working, continuation.Progression);
        if (prepared.Issues.Count != 0 || prepared.Source is not { } current ||
            current.Context != _context || current.SnapshotToken != _snapshotToken || working.Fingerprint != _working.Fingerprint)
            throw new InvalidDataException("Living-world retained source no longer matches its actual original working calculation.");
        var opening = current.ReadOpeningData();
        if (opening.Count != _opening.Count || _opening.Any(pair =>
                !opening.TryGetValue(pair.Key, out var bytes) || !Same(pair.Value, bytes)))
            throw new InvalidDataException("Living-world retained opening DATA changed across the genuine lease handoff.");
        await current.RequireUnchangedInputsAsync(files, lease);
    }

    internal JsonObject HistoryAfter => JsonNode.Parse(_historyJson)!.AsObject();
    internal static async Task<MortalLivingWorldDiagnosisPreparation> PrepareAsync(FileSystemManager files,
        FileSystemManager.CanonicalWriteLease lease, MortalWoundTreatmentAcceptedStateAuthority owner,
        MortalWoundDiagnosisWorkingProjection working, ProgressionScheduleService.FrozenMortalCalculation progression)
    {
        var prepared = await MortalLivingWorldSource.PrepareForDiagnosisAsync(files, lease, owner, working, progression);
        if (prepared.Issues.Count != 0 || prepared.Source is null) return new(prepared.Issues, null);
        try
        {
            var report = Root(prepared.Source.ReadOpeningBytes(MortalLivingWorldSource.ReportPath), MortalLivingWorldSource.ReportPath);
            MortalLivingWorldReceipt.ValidateHistory(Root(prepared.Source.ReadOpeningBytes(MortalLivingWorldSource.HistoryPath), MortalLivingWorldSource.HistoryPath));
            // Admission already proved absence legal. This block intentionally has no event reducer join yet.
            if (report is null) return new([], null);
            if (report["resolution"]?.GetValue<string>() != "noSignificantChange")
                return Failure("owner_unsupported", "Diagnosis currently requires a legal noSignificantChange assessment; declared event changes need their own pre-seal reducer join.");
            var contribution = new MortalLivingWorldDiagnosisContribution(files, prepared.Source, owner, working, progression, report);
            await prepared.Source.RequireUnchangedInputsAsync(files, lease);
            return new([], contribution);
        }
        catch (Exception error) when (error is InvalidDataException or JsonException or ArgumentException or InvalidOperationException)
        { return Failure("contribution_invalid", error.Message); }
    }

    internal void RequireOwner(MortalWoundTreatmentAcceptedStateAuthority owner,
        MortalWoundDiagnosisWorkingProjection working, ProgressionScheduleService.FrozenMortalCalculation progression)
    {
        if (!ReferenceEquals(owner, _owner) || !ReferenceEquals(working, _working) ||
            progression.MutationFingerprint != _progressionFingerprint)
            throw new InvalidDataException("Living-world contribution belongs to another actual diagnosis owner.");
        working.RequireCurrent(owner);
    }

    internal void RequireBeforeImages(CanonicalBeforeImage report, CanonicalBeforeImage history)
    {
        if (report.Existed != (_reportBefore is not null) || history.Existed != (_historyBefore is not null) ||
            !Same(report.Bytes, _reportBefore) || !Same(history.Bytes, _historyBefore))
            throw new InvalidDataException("Living-world report/history changed before common seal.");
    }

    internal void RequireComposed(IReadOnlyDictionary<string, JsonObject> companions)
    {
        if (!companions.TryGetValue(MortalLivingWorldSource.HistoryPath, out var history) ||
            !JsonNode.DeepEquals(history, HistoryAfter) || companions.ContainsKey(MortalLivingWorldSource.ReportPath))
            throw new InvalidDataException("Diagnosis common contribution omitted or changed its private history or report consumption.");
    }

    /// <summary>Fresh verification under the transaction's actual new lease; no old source capability is reused.</summary>
    internal async Task RequirePublishedAsync(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease)
    {
        using var fresh = FileSystemManager.SuppressValidationReuse();
        if (!ReferenceEquals(files, _files)) throw new InvalidDataException("Living-world diagnosis changed physical owner.");
        files.EnsureCanonicalWriteLeaseActive(lease); files.VerifyCurrentSessionOperation(lease);
        foreach (var guard in _openingGuards)
            if (!Same(guard.Value, await files.ReadFileBytesAsync(lease, guard.Key)))
                throw new InvalidDataException("Living-world diagnosis opening guard changed: " + guard.Key);
        var selection = PendingTurnSnapshotPathSelection.CreateWithObservedOptionalPaths(
            new[] { "game_state/meta/soul_state.json", MortalLivingWorldSource.WorldTimePath, ProgressionScheduleService.SchedulePath },
            MortalLivingWorldSource.DomainPaths.Concat(new[] { MortalLivingWorldSource.ReportPath, MortalLivingWorldSource.HistoryPath }));
        var read = PendingTurnSnapshotReader.ReadCurrent(files, lease, selection);
        var context = _context;
        if (!read.Success || read.Snapshot is not { } original || original.SnapshotToken != _snapshotToken ||
            original.Realm != "mortal_world" || original.SessionId != context.SessionId || original.RequestId != context.RequestId ||
            original.TurnNumber != context.Turn || !JsonNode.DeepEquals(original.ReadOriginalControl(), _control))
            throw new InvalidDataException("Living-world diagnosis original authentication changed before completion.");
        var request = Root(await files.ReadFileBytesAsync(lease, "input/turn_request.json"), "input/turn_request.json");
        var soul = Root(await files.ReadFileBytesAsync(lease, "game_state/meta/soul_state.json"), "game_state/meta/soul_state.json");
        if (!JsonNode.DeepEquals(request?["progressionControl"], _control) ||
            !RealmSemantics.IsMortalRealm(soul?["currentRealm"]?.GetValue<string>()))
            throw new InvalidDataException("Living-world diagnosis current control or realm changed.");
        if (await files.ReadFileBytesAsync(lease, MortalLivingWorldSource.ReportPath) is not null ||
            !JsonNode.DeepEquals(Root(await files.ReadFileBytesAsync(lease, MortalLivingWorldSource.HistoryPath), MortalLivingWorldSource.HistoryPath), HistoryAfter))
            throw new InvalidDataException("Living-world diagnosis receipt or report consumption changed.");
        var clock = Root(await files.ReadFileBytesAsync(lease, MortalLivingWorldSource.WorldTimePath), MortalLivingWorldSource.WorldTimePath);
        if (!JsonNode.DeepEquals(clock, _clockAfter))
            throw new InvalidDataException("Living-world diagnosis final clock differs from its actual planned T1.");
        var current = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        foreach (var path in MortalLivingWorldSource.DomainPaths)
            if (Root(await files.ReadFileBytesAsync(lease, path), path) is { } root) current.Add(path, root);
        if (MortalLivingWorldEvents.Validate(_report, _original, current, _absent).Count != 0)
            throw new InvalidDataException("Living-world diagnosis final effects do not match its declarations.");
    }

    private static JsonObject? Root(byte[]? bytes, string path) => bytes is null ? null :
        StrictJsonAuthority.Deserialize<JsonObject>(CanonicalJsonUtf8.DecodeOneOptionalBom(bytes), new JsonSerializerOptions(), path)
        ?? throw new InvalidDataException("Invalid living-world object: " + path);
    private static bool Same(byte[]? a, byte[]? b) => a is null ? b is null : b is not null && a.AsSpan().SequenceEqual(b);
    private static MortalLivingWorldDiagnosisPreparation Failure(string code, string message) => new(
        [new ValidationIssue(MortalLivingWorldSource.ReportPath, IssueSeverity.Error, message,
            code: "mortal_living_world_" + code, section: "MortalLivingWorld")], null);
}
