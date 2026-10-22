using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookOfEternityClient.Services;

internal static partial class MortalQteDecisionPublication
{
    private static readonly object JointProgressionPhaseIssuer = new();
    private static readonly string[] JointProgressionFields = ["version", "phase", "controlFingerprint", "preparedAtUtc",
        "sourceGuards", "scheduleBefore", "reportBefore", "scheduleAfter", "reportAfter", "mutationFingerprint"];
    private static readonly string[] JointProgressionGuardFields = ["calculation", "originalHeaders", "basisFingerprint"];
    private static readonly string[] JointProgressionHeaderPaths =
        [LiveTurnPreparationService.PendingTurnSnapshotManifestPath, PendingTurnSnapshotAuthority.AuthorityPath];
    private static readonly string[] JointProgressionImageFields = ["exists", "bytes", "digest"];

    // Fresh physical owner proof only; no parsed marker, diagnostic witness or
    // old lease-local calculation value can construct this capability. The
    // owning three-member phase publisher is connected in the following block.
    internal sealed class JointProgressionPhaseCapability
    {
        private readonly FileSystemManager _files;
        private readonly FileSystemManager.CanonicalWriteLease _lease;
        private readonly string _generation;
        private readonly string _sessionId, _requestId, _snapshotToken;
        private readonly int _turn;
        private readonly byte[] _marker;
        private readonly byte[]? _decision;
        private readonly string _basisFingerprint;
        private readonly string _preparedAtUtc;
        private readonly ProgressionScheduleService.FrozenMortalCalculation _calculation;
        private int _consumed;
        internal JointProgressionPhaseCapability(object issuer, FileSystemManager files,
            FileSystemManager.CanonicalWriteLease lease, byte[] marker, JsonObject root,
            string phase, string basisFingerprint, ProgressionScheduleService.FrozenMortalCalculation calculation, byte[]? decision)
        {
            if (!ReferenceEquals(issuer, JointProgressionPhaseIssuer)) throw Invalid("joint_progression_issuer");
            _files = files; _lease = lease; _generation = Text(root, "generation");
            _sessionId = Text(root, "sessionId"); _requestId = Text(root, "requestId");
            _snapshotToken = Text(root, "snapshotToken"); _turn = JointInteger(root, "turn");
            _marker = marker.ToArray(); _basisFingerprint = basisFingerprint; Phase = phase;
            _preparedAtUtc = Text(ClosedMap(root["progression"], JointProgressionFields), "preparedAtUtc");
            _decision = decision?.ToArray();
            _calculation = new(Clone(calculation.ScheduleBefore), Clone(calculation.ReportBefore),
                Clone(calculation.ScheduleAfter), Clone(calculation.ReportAfter), calculation.MutationFingerprint);
        }
        internal string Phase { get; }
        internal string PreparedAtUtcForOwner => _preparedAtUtc;
        internal string SessionIdForOwner => _sessionId;
        internal string RequestIdForOwner => _requestId;
        internal string SnapshotTokenForOwner => _snapshotToken;
        internal int TurnForOwner => _turn;
        internal byte[] MarkerBefore => _marker.ToArray();
        internal CanonicalBeforeImage ScheduleBefore => Clone(_calculation.ScheduleBefore);
        internal CanonicalBeforeImage ReportBefore => Clone(_calculation.ReportBefore);
        internal CanonicalBeforeImage ScheduleAfter => Clone(_calculation.ScheduleAfter);
        internal CanonicalBeforeImage ReportAfter => Clone(_calculation.ReportAfter);
        internal async Task ValidateCurrentAsync(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease)
            => await ValidateCurrentCoreAsync(files, lease, allowConsumedForOwner: false);
        internal async Task ValidateForPublicationAsync(object issuer, FileSystemManager files, FileSystemManager.CanonicalWriteLease lease)
        {
            if (!ReferenceEquals(issuer, JointProgressionPhaseIssuer) || Volatile.Read(ref _consumed) != 1)
                throw Invalid("joint_progression_publication_capability");
            await ValidateCurrentCoreAsync(files, lease, allowConsumedForOwner: true);
        }
        private async Task ValidateCurrentCoreAsync(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease, bool allowConsumedForOwner)
        {
            files.EnsureCanonicalWriteLeaseActive(lease);
            if ((!allowConsumedForOwner && Volatile.Read(ref _consumed) != 0) || !ReferenceEquals(files, _files) || !ReferenceEquals(lease, _lease) ||
                !files.IsCurrentSessionGeneration(lease, _generation)) throw Invalid("joint_progression_stale_capability");
            var fresh = await ReadJointProgressionPhaseAsync(files, lease, _sessionId, _requestId, _snapshotToken, _turn);
            if (fresh.Phase != Phase || fresh._basisFingerprint != _basisFingerprint || !Same(_marker, fresh._marker) ||
                !Same(_decision, fresh._decision))
                throw Invalid("joint_progression_capability_changed");
        }
        internal async Task<JointProgressionPhaseCapability> ReadFreshAsync(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease)
        {
            files.EnsureCanonicalWriteLeaseActive(lease);
            if (!ReferenceEquals(files, _files) || !ReferenceEquals(lease, _lease) || !files.IsCurrentSessionGeneration(lease, _generation))
                throw Invalid("joint_progression_stale_reader");
            return await ReadJointProgressionPhaseAsync(files, lease, _sessionId, _requestId, _snapshotToken, _turn);
        }
        internal bool SameImmutableBasis(JointProgressionPhaseCapability fresh) =>
            ReferenceEquals(_files, fresh._files) && ReferenceEquals(_lease, fresh._lease) && _generation == fresh._generation &&
            _sessionId == fresh._sessionId && _requestId == fresh._requestId && _snapshotToken == fresh._snapshotToken && _turn == fresh._turn &&
            _basisFingerprint == fresh._basisFingerprint && _calculation.MutationFingerprint == fresh._calculation.MutationFingerprint &&
            _calculation.ScheduleBefore.Fingerprint == fresh._calculation.ScheduleBefore.Fingerprint &&
            _calculation.ReportBefore.Fingerprint == fresh._calculation.ReportBefore.Fingerprint &&
            _calculation.ScheduleAfter.Fingerprint == fresh._calculation.ScheduleAfter.Fingerprint &&
            _calculation.ReportAfter.Fingerprint == fresh._calculation.ReportAfter.Fingerprint;
        internal bool TryConsume(object issuer) => ReferenceEquals(issuer, JointProgressionPhaseIssuer) &&
            Phase == "unapplied" && Interlocked.CompareExchange(ref _consumed, 1, 0) == 0;
        internal bool TryBeginCompensation(object issuer) => ReferenceEquals(issuer, JointProgressionPhaseIssuer) &&
            Phase == "unapplied" && Interlocked.CompareExchange(ref _consumed, 2, 1) == 1;
        internal byte[] CreateAppliedMarker(object issuer)
        {
            if (!ReferenceEquals(issuer, JointProgressionPhaseIssuer) || Phase != "unapplied")
                throw Invalid("joint_progression_phase_writer");
            var root = StrictJsonAuthority.Deserialize<JsonObject>(CanonicalJsonUtf8.DecodeOneOptionalBom(_marker),
                SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed, "owned joint unapplied marker") ?? throw Invalid("joint_progression_marker_null");
            root["progression"]!["phase"] = "applied";
            root.Remove("fingerprint"); root["fingerprint"] = JointFingerprint(root);
            return SerializePublicationRoot(root);
        }
        internal JsonObject ToDiagnosticWitness() => new()
        {
            ["phase"] = Phase, ["generation"] = _generation, ["sessionId"] = _sessionId,
            ["requestId"] = _requestId, ["snapshotToken"] = _snapshotToken, ["turn"] = _turn,
            ["basisFingerprint"] = _basisFingerprint, ["markerHash"] = Digest(_marker)
        };
        private static CanonicalBeforeImage Clone(CanonicalBeforeImage image) => new(image.Existed, image.Bytes?.ToArray());
    }

    private sealed record JointProgressionData(string Phase, string BasisFingerprint,
        ProgressionScheduleService.FrozenMortalCalculation Calculation);

    /// <summary>
    /// Reconstructs a lease-bound progression capability from the actual current original and completed publication proof.
    /// </summary>
    /// <param name="files">
    /// The filesystem whose current original, generation and publication data are verified.
    /// </param>
    /// <param name="lease">
    /// Its active participating canonical lease, retained by the resulting capability.
    /// </param>
    /// <param name="sessionId">
    /// The expected session ID, which must match the verified original.
    /// </param>
    /// <param name="requestId">
    /// The expected request ID, which must match the verified original.
    /// </param>
    /// <param name="snapshotToken">
    /// The expected original snapshot token.
    /// </param>
    /// <param name="turn">
    /// The expected original turn number.
    /// </param>
    /// <returns>
    /// A new current phase capability issued by this production reader; inconsistent or stale data is rejected.
    /// </returns>
    internal static async Task<JointProgressionPhaseCapability> ReadJointProgressionPhaseAsync(FileSystemManager files,
        FileSystemManager.CanonicalWriteLease lease, string sessionId, string requestId, string snapshotToken, int turn)
    {
        using var t318Timer19 = files.MeasureOperation("JointPhase.VerifyLease");
        files.EnsureCanonicalWriteLeaseActive(lease);
        t318Timer19?.Dispose();
        // Existing recovery resolves its own journal debt before phase/category
        // classification. Unknown debt throws; it is never interpreted as absence.
        using var t318Timer20 = files.MeasureOperation("JointPhase.ResolveRecovery");
        files.ResolveBackupPublicationRecovery(lease);
        t318Timer20?.Dispose();
        using var t318Timer21 = files.MeasureOperation("JointPhase.ReadMarkerAsync");
        var marker = await files.ReadFileBytesAsync(lease, StatePath) ?? throw Invalid("joint_progression_marker_missing");
        t318Timer21?.Dispose();
        using var t318Timer22 = files.MeasureOperation("JointPhase.ReadCompletedProofAsync");
        var proof = await ReadCompletedProofAsync(files, lease, ReadInspectionSnapshot(files, lease));
        t318Timer22?.Dispose();
        using var t318Timer23 = files.MeasureOperation("JointPhase.VerifyTupleAndNoContinuationAsync");
        RequireJointCompletionTuple(proof, sessionId, requestId, snapshotToken, turn);
        await RequireNoJointContinuationCommandsAsync(files, lease);
        t318Timer23?.Dispose();
        using var t318Timer24 = files.MeasureOperation("JointPhase.ReadDecisionAsync");
        var decision = await files.ReadFileBytesAsync(lease, MortalWoundOpportunityAdapter.DecisionPath);
        if (decision is not null && Text(proof.Root, "decisionPayloadFingerprint") != ComputeDecisionPayloadFingerprint(decision))
            throw Invalid("joint_progression_changed_public_decision");
        t318Timer24?.Dispose();
        using var t318Timer25 = files.MeasureOperation("JointPhase.ValidateProgressionDataAsync");
        var data = await ValidateJointProgressionDataAsync(files, lease, proof.Root);
        t318Timer25?.Dispose();
        using var t318Timer26 = files.MeasureOperation("JointPhase.RecheckMarkerAsync");
        if (!Same(marker, await files.ReadFileBytesAsync(lease, StatePath))) throw Invalid("joint_progression_marker_drift");
        t318Timer26?.Dispose();
        return new(JointProgressionPhaseIssuer, files, lease, marker, proof.Root, data.Phase, data.BasisFingerprint, data.Calculation, decision);
    }

    private static async Task<JsonObject> CreateJointProgressionUnappliedAsync(FileSystemManager files,
        FileSystemManager.CanonicalWriteLease lease, GameEngine.MortalJointProgressionSource source)
    {
        await source.ValidateCurrentAsync(files, lease);
        var basis = source.Inputs.ToDiagnosticJson();
        var calculation = NewProgressionCalculator(files).RecomposeFrozenMortalCalculation(basis);
        var headers = new JsonObject();
        foreach (var path in JointProgressionHeaderPaths) headers[path] = JointProgressionImage(source.BeforeImages[path]);
        return new JsonObject
        {
            ["version"] = 1, ["phase"] = "unapplied", ["controlFingerprint"] = JointProgressionControlFingerprint(basis["control"]!),
            ["preparedAtUtc"] = Text(basis, "preparedAtUtc"),
            ["sourceGuards"] = new JsonObject { ["calculation"] = basis.DeepClone(), ["originalHeaders"] = headers, ["basisFingerprint"] = source.Fingerprint },
            ["scheduleBefore"] = JointProgressionImage(calculation.ScheduleBefore), ["reportBefore"] = JointProgressionImage(calculation.ReportBefore),
            ["scheduleAfter"] = JointProgressionImage(calculation.ScheduleAfter), ["reportAfter"] = JointProgressionImage(calculation.ReportAfter),
            ["mutationFingerprint"] = calculation.MutationFingerprint
        };
    }

    private static async Task<JointProgressionData> ValidateJointProgressionDataAsync(FileSystemManager files,
        FileSystemManager.CanonicalWriteLease lease, JsonObject root)
    {
        using var dataBody = files.MeasureOperation("JointProgression.ValidateDataBodyAsync");
        var progression = ClosedMap(root["progression"], JointProgressionFields);
        if (JointInteger(progression, "version") != 1) throw Invalid("joint_progression_version");
        var phase = Text(progression, "phase");
        if (phase is not ("unapplied" or "applied")) throw Invalid("joint_progression_phase");
        var guards = ClosedMap(progression["sourceGuards"], JointProgressionGuardFields);
        var basis = guards["calculation"] as JsonObject ?? throw Invalid("joint_progression_basis_missing");
        var calculation = NewProgressionCalculator(files).RecomposeFrozenMortalCalculation(basis);
        var headers = ClosedMap(guards["originalHeaders"], JointProgressionHeaderPaths);
        var headerImages = JointProgressionHeaderPaths.ToDictionary(path => path,
            path => ReadJointProgressionImage(headers[path]), StringComparer.Ordinal);
        foreach (var row in headerImages)
            if (!row.Value.Existed || !Same(row.Value.Bytes, await files.ReadFileBytesAsync(lease, row.Key)))
                throw Invalid("joint_progression_original_header: " + row.Key);
        var manifest = StrictJsonAuthority.Deserialize<JsonObject>(CanonicalJsonUtf8.DecodeOneOptionalBom(
            headerImages[LiveTurnPreparationService.PendingTurnSnapshotManifestPath].Bytes!),
            SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed, "joint progression actual original") ?? throw Invalid("joint_progression_original_null");
        var original = PendingTurnSnapshotReader.ReadCurrent(files, lease,
            new PendingTurnSnapshotPathSelection(["game_state/meta/soul_state.json", ProgressionScheduleService.SchedulePath], []));
        if (!original.Success || original.Snapshot is not { } signed || signed.Realm != "mortal_world" ||
            signed.SessionId != Text(root, "sessionId") || signed.RequestId != Text(root, "requestId") ||
            signed.SnapshotToken != Text(root, "snapshotToken") || signed.TurnNumber != JointInteger(root, "turn") ||
            !Same(signed.ReadRequiredBytes(ProgressionScheduleService.SchedulePath), calculation.ScheduleBefore.Bytes) ||
            !JsonNode.DeepEquals(manifest["progressionControl"], basis["control"])) throw Invalid("joint_progression_signed_original");
        var context = basis["requestContext"] as JsonObject ?? throw Invalid("joint_progression_context_missing");
        if (Text(context, "sessionId") != signed.SessionId || Text(context, "requestId") != signed.RequestId ||
            JointInteger(context, "turn") != signed.TurnNumber) throw Invalid("joint_progression_context_binding");
        if (Text(progression, "controlFingerprint") != JointProgressionControlFingerprint(basis["control"]!) ||
            Text(progression, "preparedAtUtc") != Text(basis, "preparedAtUtc") ||
            Text(progression, "mutationFingerprint") != calculation.MutationFingerprint) throw Invalid("joint_progression_recomposition_binding");
        var images = new[] { ("scheduleBefore", calculation.ScheduleBefore), ("reportBefore", calculation.ReportBefore),
            ("scheduleAfter", calculation.ScheduleAfter), ("reportAfter", calculation.ReportAfter) };
        foreach (var (field, expected) in images)
            if (ReadJointProgressionImage(progression[field]).Fingerprint != expected.Fingerprint)
                throw Invalid("joint_progression_calculated_image: " + field);
        var basisFingerprint = ComputeJointProgressionSourceFingerprint(root, basis, headerImages);
        if (Text(guards, "basisFingerprint") != basisFingerprint) throw Invalid("joint_progression_basis_seal");
        // Only the schedule/report pair changes in the owning phase. All other
        // exact CURRENT submitted/source guards stay bound; they need not equal
        // original world-time/report baseline images.
        var raw = basis["sourceBytes"]!.AsObject();
        foreach (var path in ProgressionScheduleService.MortalJointCalculationSourcePaths)
        {
            var expected = path == ProgressionScheduleService.SchedulePath
                ? (phase == "applied" ? calculation.ScheduleAfter.Bytes : calculation.ScheduleBefore.Bytes)
                : path == ProgressionScheduleService.ReportPath
                    ? (phase == "applied" ? calculation.ReportAfter.Bytes : calculation.ReportBefore.Bytes)
                    : raw[path] is null ? null : Convert.FromBase64String(raw[path]!.GetValue<string>());
            if (!Same(expected, await files.ReadFileBytesAsync(lease, path))) throw Invalid("joint_progression_current_phase_image: " + path);
        }
        return new(phase, basisFingerprint, calculation);
    }

    private static ProgressionScheduleService NewProgressionCalculator(FileSystemManager files) =>
        new(files, NullLogger<ProgressionScheduleService>.Instance);
    private static string JointProgressionControlFingerprint(JsonNode control) =>
        WoundAcceptedTurnFingerprintWriter.Compute(new[] { "book_of_eternity.mortal_joint_progression.control.v1",
            WoundAcceptedTurnFingerprintWriter.CanonicalJson(control) });
    private static string ComputeJointProgressionSourceFingerprint(JsonObject root, JsonObject basis,
        IReadOnlyDictionary<string, CanonicalBeforeImage> headers)
    {
        var generation = Text(root, "generation");
        var frozenFingerprint = WoundAcceptedTurnFingerprintWriter.Compute(new[] {
            "book_of_eternity.mortal_joint_progression.frozen_basis.v1", generation,
            WoundAcceptedTurnFingerprintWriter.CanonicalJson(basis) });
        var fields = new List<string?> { "book_of_eternity.mortal_joint_progression.original_source.v1",
            generation, Text(root, "sessionId"), Text(root, "requestId"), Text(root, "snapshotToken"),
            JointInteger(root, "turn").ToString(CultureInfo.InvariantCulture), frozenFingerprint };
        foreach (var row in headers.OrderBy(row => row.Key, StringComparer.Ordinal))
        { fields.Add(row.Key); fields.Add(row.Value.Fingerprint); }
        return WoundAcceptedTurnFingerprintWriter.Compute(fields);
    }
    private static JsonObject JointProgressionImage(CanonicalBeforeImage image) => new()
    {
        ["exists"] = image.Existed, ["bytes"] = image.Existed ? Convert.ToBase64String(image.Bytes!) : null,
        ["digest"] = image.Existed ? Digest(image.Bytes) : null
    };
    private static CanonicalBeforeImage ReadJointProgressionImage(JsonNode? node)
    {
        var image = ClosedMap(node, JointProgressionImageFields);
        if (image["exists"] is not JsonValue value || !value.TryGetValue<bool>(out var exists)) throw Invalid("joint_progression_image_exists");
        if (!exists)
        {
            if (image["bytes"] is not null || image["digest"] is not null) throw Invalid("joint_progression_absence_shape");
            return new(false, null);
        }
        var encoded = Text(image, "bytes");
        var bytes = Convert.FromBase64String(encoded);
        if (Convert.ToBase64String(bytes) != encoded || Text(image, "digest") != Digest(bytes)) throw Invalid("joint_progression_image_digest");
        return new(true, bytes);
    }
    private static string JointImmutableFingerprint(JsonObject root)
    {
        var immutable = root.DeepClone().AsObject(); immutable.Remove("fingerprint");
        var progression = ClosedMap(immutable["progression"], JointProgressionFields);
        progression["phase"] = "unapplied";
        return JointFingerprint(immutable);
    }
}
