using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.Core;

public partial class GameEngine
{
    private static readonly object MortalJointProgressionSourceIssuer = new();

    // Engine-local original authentication, separate from the detached service
    // calculation. Neither a serialized body nor its hash can construct this
    // proof. The actual common/basis issuer revalidates it before sealing.
    internal sealed class MortalJointProgressionSource
    {
        private readonly FileSystemManager _files;
        private readonly FileSystemManager.CanonicalWriteLease _lease;
        private readonly string _generation;
        private readonly string _sessionId, _requestId, _snapshotToken;
        private readonly int _turn;
        private readonly Dictionary<string, byte[]> _originalGuards;
        internal MortalJointProgressionSource(object issuer, FileSystemManager files,
            FileSystemManager.CanonicalWriteLease lease, ProgressionScheduleService.FrozenMortalOutcomeInputs inputs,
            IReadOnlyDictionary<string, byte[]> originalGuards, string sessionId, string requestId, string snapshotToken, int turn)
        {
            if (!ReferenceEquals(issuer, MortalJointProgressionSourceIssuer)) throw new InvalidOperationException("Joint progression source requires the actual engine issuer.");
            _files = files; _lease = lease; _generation = files.ReadLocalGenerationSnapshot(lease).Binding.Id;
            _sessionId = sessionId; _requestId = requestId; _snapshotToken = snapshotToken; _turn = turn;
            Inputs = inputs; _originalGuards = originalGuards.ToDictionary(row => row.Key, row => row.Value.ToArray(), StringComparer.Ordinal);
            Fingerprint = ComputeFingerprint();
        }
        internal ProgressionScheduleService.FrozenMortalOutcomeInputs Inputs { get; }
        internal string Fingerprint { get; }
        internal bool HasValidSeal => Fingerprint == ComputeFingerprint();
        internal bool MatchesFrame(string sessionId, string requestId, string snapshotToken, int turn) =>
            _sessionId == sessionId && _requestId == requestId && _snapshotToken == snapshotToken && _turn == turn;
        internal IReadOnlyDictionary<string, CanonicalBeforeImage> BeforeImages
        {
            get
            {
                var images = Inputs.BeforeImages.ToDictionary(row => row.Key,
                    row => new CanonicalBeforeImage(row.Value.Existed, row.Value.Bytes?.ToArray()), StringComparer.Ordinal);
                foreach (var row in _originalGuards) images.Add(row.Key, new(true, row.Value.ToArray()));
                return images;
            }
        }
        private string ComputeFingerprint()
        {
            var fields = new List<string?> { "book_of_eternity.mortal_joint_progression.original_source.v1",
                _generation, _sessionId, _requestId, _snapshotToken, _turn.ToString(System.Globalization.CultureInfo.InvariantCulture), Inputs.Fingerprint };
            foreach (var row in _originalGuards.OrderBy(row => row.Key, StringComparer.Ordinal))
            { fields.Add(row.Key); fields.Add(new CanonicalBeforeImage(true, row.Value).Fingerprint); }
            return WoundAcceptedTurnFingerprintWriter.Compute(fields);
        }
        internal async Task ValidateCurrentAsync(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease)
        {
            files.EnsureCanonicalWriteLeaseActive(lease);
            if (!HasValidSeal || !ReferenceEquals(files, _files) || !ReferenceEquals(lease, _lease) || !files.IsCurrentSessionGeneration(lease, _generation))
                throw new InvalidDataException("Joint progression source cannot cross physical owner/generation/lease.");
            await Inputs.ValidateCurrentAsync(files, lease);
            foreach (var (path, expected) in _originalGuards)
            {
                var actual = await files.ReadFileBytesAsync(lease, path);
                if (actual is null || !expected.AsSpan().SequenceEqual(actual))
                    throw new InvalidDataException("Authenticated progression original changed: " + path);
            }
            var read = PendingTurnSnapshotReader.ReadCurrent(files, lease,
                new PendingTurnSnapshotPathSelection(["game_state/meta/soul_state.json", ProgressionScheduleService.SchedulePath], []));
            if (!read.Success || read.Snapshot is not { } original || original.Realm != "mortal_world" ||
                original.SessionId != _sessionId || original.RequestId != _requestId || original.SnapshotToken != _snapshotToken || original.TurnNumber != _turn)
                throw new InvalidDataException("Progression signed original/current snapshot no longer authenticates.");
            var schedule = Inputs.ReadSourceBytes(ProgressionScheduleService.SchedulePath);
            if (schedule is null || !original.ReadRequiredBytes(ProgressionScheduleService.SchedulePath).AsSpan().SequenceEqual(schedule))
                throw new InvalidDataException("Signed progression schedule coverage changed.");
        }
    }

    private async Task<MortalJointProgressionSource> CaptureMortalJointProgressionSourceAsync(
        FileSystemManager.CanonicalWriteLease lease, ValidatedPendingTurnSnapshotContext frame)
    {
        RequireOrdinaryJointRepairLease(lease);
        if (frame.ProgressionControl is not { } control)
            throw new InvalidDataException("The genuine joint original requires its actual non-null progression control.");
        var manifestBytes = await _fs.ReadFileBytesAsync(lease, LiveTurnPreparationService.PendingTurnSnapshotManifestPath) ??
            throw new InvalidDataException("The authenticated progression manifest is missing.");
        var authorityBytes = await _fs.ReadFileBytesAsync(lease, PendingTurnSnapshotAuthority.AuthorityPath) ??
            throw new InvalidDataException("The authenticated progression authority is missing.");
        var read = PendingTurnSnapshotReader.ReadCurrent(_fs, lease,
            new PendingTurnSnapshotPathSelection(["game_state/meta/soul_state.json", ProgressionScheduleService.SchedulePath], []));
        if (!read.Success || read.Snapshot is not { } original)
            throw new InvalidDataException("Joint progression original coverage refused: " + string.Join(";", read.Issues.Select(row => row.Code + ":" + row.FilePath)));
        if (original.Realm != "mortal_world" || original.SessionId != frame.SessionId || original.RequestId != frame.RequestId ||
            original.SnapshotToken != frame.Payload.ManifestPayloadHash || original.TurnNumber != frame.TurnNumber)
            throw new InvalidDataException("The actual progression original does not match the admitted joint frame.");
        var manifest = StrictJsonAuthority.Deserialize<JsonObject>(CanonicalJsonUtf8.DecodeOneOptionalBom(manifestBytes),
            SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed, "joint progression original manifest") ??
            throw new InvalidDataException("The actual progression manifest is null.");
        if (!JsonNode.DeepEquals(manifest["progressionControl"], JsonSerializer.SerializeToNode(control, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed)))
            throw new InvalidDataException("The actual progression control is not the signed original's control.");
        var inputs = await _progressionSchedule.CaptureMortalOutcomeInputsAsync(lease, control,
            frame.SessionId, frame.RequestId, frame.TurnNumber);
        var signedSchedule = original.ReadRequiredBytes(ProgressionScheduleService.SchedulePath);
        var currentSchedule = inputs.ReadSourceBytes(ProgressionScheduleService.SchedulePath);
        if (currentSchedule is null || !signedSchedule.AsSpan().SequenceEqual(currentSchedule))
            throw new InvalidDataException("Progression schedule-before does not agree with authenticated original coverage.");
        // CURRENT report/world-time inputs are independently validated/captured;
        // legitimate GM output is not required to equal its original baseline.
        var result = new MortalJointProgressionSource(MortalJointProgressionSourceIssuer, _fs, lease, inputs,
            new Dictionary<string, byte[]>(StringComparer.Ordinal)
            {
                [LiveTurnPreparationService.PendingTurnSnapshotManifestPath] = manifestBytes,
                [PendingTurnSnapshotAuthority.AuthorityPath] = authorityBytes
            }, frame.SessionId, frame.RequestId, frame.Payload.ManifestPayloadHash, frame.TurnNumber);
        await result.ValidateCurrentAsync(_fs, lease);
        return result;
    }
}
