using BookOfEternityClient.Core;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    private MortalOriginalTurnCapture? _mortalOriginalTurnCapture;

    // Only the real validator pipeline supplies this sink; no caller-provided input is accepted.
    private sealed class MortalOriginalInputSink
    {
        internal AcceptedMechanicsInput? Input;
        internal AcceptedTurnWoundSelection? Selection;
    }

    /// <summary>
    /// Reports acquisition of an original Mortal turn without wound allocation or common publication.
    /// </summary>
    /// <param name="Capture">
    /// Actual retained owner, or null when input validation fails.
    /// </param>
    /// <param name="Issues">
    /// Validation diagnostics from the original accepted-input pipeline.
    /// </param>
    internal sealed record MortalOriginalTurnCaptureResult(MortalOriginalTurnCapture? Capture,
        IReadOnlyList<ValidationIssue> Issues);

    /// <summary>
    /// Captures a signed pending physical-wound decision before ordinary wound preparation allocates identities.
    /// </summary>
    /// <param name="lease">
    /// Active canonical write lease protecting acquisition and invalidation.
    /// </param>
    /// <returns>
    /// An owned unpublished turn or diagnostics preventing capture.
    /// </returns>
    internal Task<MortalOriginalTurnCaptureResult> CaptureMortalOriginalTurnAsync(
        FileSystemManager.CanonicalWriteLease lease) => MortalOriginalTurnCapture.CaptureAsync(this, lease);

    /// <summary>
    /// Retains validator-authenticated original Mortal inputs and their uncompleted effect draft.
    /// The capture does not itself grant insertion or common publication authority.
    /// </summary>
    internal sealed partial class MortalOriginalTurnCapture : IDisposable
    {
        private readonly ValidationService _validator;
        private readonly AcceptedMechanicsInput _input;
        private readonly AcceptedTurnWoundSelection _selection;
        private readonly WoundResponseCommandParsingResult _originalCommands;
        private readonly IReadOnlyDictionary<string, CanonicalBeforeImage> _observed;
        private readonly EffectAcceptedTurnPlanner.EffectAcceptedDraft _effects;
        private readonly IReadOnlyList<string> _snapshotPaths;
        private readonly SemaphoreSlim _gate = new(1, 1);
        private bool _disposed;

        /// <summary>
        /// Retains the validated input objects and detached observations without materializing a wound.
        /// </summary>
        /// <param name="validator">
        /// Validator registering this capture as its current original owner.
        /// </param>
        /// <param name="input">
        /// Real accepted resource input containing the validated original effect base.
        /// </param>
        /// <param name="selection">
        /// Independently reconstructed pending occurrence and validated response before allocation.
        /// </param>
        /// <param name="observed">
        /// Current file observations retained for subsequent freshness checks.
        /// </param>
        /// <param name="snapshotPaths">
        /// Signed payload paths verified during capture and rechecked before continuation.
        /// </param>
        private MortalOriginalTurnCapture(ValidationService validator, AcceptedMechanicsInput input,
            AcceptedTurnWoundSelection selection, IReadOnlyDictionary<string, CanonicalBeforeImage> observed, IReadOnlyList<string> snapshotPaths)
        {
            _validator = validator;
            _snapshotPaths = Array.AsReadOnly(snapshotPaths.ToArray());
            _input = input;
            _originalCommands = selection.OriginalCommands;
            _selection = new(selection.Commands.DeepClone().AsObject(),
                WoundAcceptedTurnData.CloneInput(selection.Input)!, selection.EffectLocations, _originalCommands);
            _observed = AcceptedMechanicsPlanBinding.CloneReadOnlyBeforeImages(observed);
            _effects = EffectAcceptedTurnPlanner.EffectAcceptedDraft.Begin(input.PlanningContext!.EffectPlan!,
                input.PlanningContext.EffectIdentityFactory ?? new EffectIdentityFactory());
        }

        /// <summary>
        /// Gets whether this capture remains the validator's exact current original owner.
        /// </summary>
        internal bool IsCurrentOwner => !_disposed && ReferenceEquals(_validator._mortalOriginalTurnCapture, this);

        /// <summary>
        /// Replaces the previous owner using the real signed validation pipeline with wound preparation deferred.
        /// </summary>
        /// <param name="validator">
        /// Validator whose retained capture is replaced.
        /// </param>
        /// <param name="lease">
        /// Active canonical lease for signed input acquisition and cache invalidation.
        /// </param>
        /// <returns>
        /// The registered capture, or diagnostics with no retained capture on failure.
        /// </returns>
        internal static async Task<MortalOriginalTurnCaptureResult> CaptureAsync(ValidationService validator,
            FileSystemManager.CanonicalWriteLease lease)
        {
            validator._fs.EnsureCanonicalWriteLeaseActive(lease);
            validator._mortalOriginalTurnCapture?.Dispose();
            validator._spiritualOriginalTurnCapture?.Dispose();
            validator.InvalidateSpiritualWoundSourceSession();
            AcceptedMechanicsPlanAuthority.InvalidateValidated(validator._fs, lease);
            EffectAcceptedTurnPlanAuthority.InvalidateValidated(validator._fs, lease);
            var retained = false;
            try
            {
                var snapshot = PendingTurnSnapshotReader.ReadCurrent(validator._fs, lease,
                    CanonicalWoundSnapshotReadPaths);
                if (!snapshot.Success || snapshot.Snapshot == null)
                    return new(null, snapshot.Issues);
                if (snapshot.Snapshot.Realm != "mortal_world")
                    return new(null, new[] { WoundIssue(AcceptedMechanicsPlan.WoundCommandPath,
                        "mortal_original_capture_realm_mismatch", "a signed Mortal-world turn", snapshot.Snapshot.Realm) });
                var manifestBytes = await validator._fs.ReadFileBytesAsync(lease,
                    "game_state/control/pending_turn_snapshot.json");
                var manifest = JsonNode.Parse(manifestBytes!)!.AsObject();
                var snapshotPaths = manifest["files"]!.AsObject().Select(pair => pair.Key)
                    .Concat(CanonicalWoundSnapshotReadPaths).Distinct(StringComparer.Ordinal).ToArray();
                var sink = new MortalOriginalInputSink();
                var issues = await validator.ValidateAcceptedTurnRawResourceMaterializationCoreAsync(lease, mortalCapture: sink);
                if (issues.Any(issue => issue.Severity == IssueSeverity.Error))
                    return new(null, issues);
                if (sink.Input is not { PlanningContext.EffectPlan: { } plan } input || input.Realm != "mortal_world" ||
                    sink.Selection is not { } selection || selection.Input.Transitions.Count == 0 ||
                    selection.Input.Transitions.Any(value => value.Kind is not ("create" or "worsen")) ||
                    selection.OriginalCommands.Commands.Count != selection.Input.Transitions.Count ||
                    selection.Input.Opportunities.Any(value => value.Domain != "physical") ||
                    selection.Input.Binding.SessionId != input.SessionId ||
                    selection.Input.Binding.RequestId != input.RequestId ||
                    selection.Input.Binding.SnapshotToken != input.SnapshotToken ||
                    selection.Input.Binding.Turn != input.Turn ||
                    !EffectAcceptedTurnPlanAuthority.TryPeekValidated(validator._fs, lease, out var accepted) ||
                    !ReferenceEquals(accepted.Plan, plan))
                    return new(null, new[] { WoundIssue(AcceptedMechanicsPlan.WoundCommandPath,
                        "mortal_original_capture_missing", "exact signed physical decisions and their original effect base", "missing or mismatched") });
                var snapshotIssues = CheckSignedSnapshot(validator._fs, lease, snapshotPaths, input);
                if (snapshotIssues.Count != 0)
                    return new(null, snapshotIssues);
                var observed = new Dictionary<string, CanonicalBeforeImage>(StringComparer.Ordinal);
                var paths = input.BeforeImages.Keys.Concat(CanonicalWoundSnapshotReadPaths)
                    .Concat(EffectAcceptedTurnInputComposer.SourceAuthorityPaths)
                    .Append(AcceptedMechanicsPlan.WoundCommandPath)
                    .Append(EffectAcceptedTurnPlan.CommandPath)
                    .Append(ResourceMaterializationContract.CommandPath)
                    .Append(EffectAcceptedTurnInputComposer.WorldTimePath)
                    .Append("game_state/control/pending_turn_snapshot.json")
                    .Append("input/turn_request.json")
                    .Append(FullPartyInteractionsPath)
                    .Append(StorageTransportMoveService.VehiclesPath)
                    .Append("game_state/meta/guardians.json")
                    .Append("game_state/control/life_transitions.json")
                    .Distinct(StringComparer.Ordinal);
                foreach (var path in paths)
                {
                    var bytes = await validator._fs.ReadFileBytesAsync(lease, path);
                    observed.Add(path, new CanonicalBeforeImage(bytes != null, bytes));
                }
                var capture = new MortalOriginalTurnCapture(validator, input, selection, observed, snapshotPaths);
                validator._mortalOriginalTurnCapture = capture;
                retained = true;
                return new(capture, issues);
            }
            finally
            {
                if (!retained)
                    EffectAcceptedTurnPlanAuthority.InvalidateValidated(validator._fs, lease);
            }
        }

        /// <summary>
        /// Revalidates every retained original file observation before a capture-dependent operation.
        /// Any changed input revokes this capture and its matching original effect handoff.
        /// </summary>
        /// <param name="lease">
        /// Active canonical lease protecting the input comparison.
        /// </param>
        /// <returns>
        /// Empty when current, or the first changed-input diagnostic after revocation.
        /// </returns>
        internal async Task<IReadOnlyList<ValidationIssue>> CheckRetainedInputsAsync(FileSystemManager.CanonicalWriteLease lease)
        {
            _validator._fs.EnsureCanonicalWriteLeaseActive(lease);
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!IsCurrentOwner)
                throw new InvalidOperationException("The original Mortal capture was revoked.");
            if (!EffectAcceptedTurnPlanAuthority.TryPeekValidated(_validator._fs, lease, out var retained) ||
                !ReferenceEquals(retained.Plan, _input.PlanningContext!.EffectPlan))
            {
                Dispose();
                return new[] { WoundIssue(EffectAcceptedTurnPlan.CommandPath,
                    "mortal_original_effect_binding_changed", "the exact retained original effect plan", "superseded") };
            }
            var snapshotIssues = CheckSignedSnapshot(_validator._fs, lease, _snapshotPaths, _input);
            if (snapshotIssues.Count != 0)
            {
                EffectAcceptedTurnPlanAuthority.InvalidateValidated(_validator._fs, lease);
                Dispose();
                return snapshotIssues;
            }
            foreach (var pair in _observed)
            {
                var bytes = await _validator._fs.ReadFileBytesAsync(lease, pair.Key);
                if (pair.Value.Fingerprint == new CanonicalBeforeImage(bytes != null, bytes).Fingerprint)
                    continue;
                if (EffectAcceptedTurnPlanAuthority.TryPeekValidated(_validator._fs, lease, out var accepted) &&
                    ReferenceEquals(accepted.Plan, _input.PlanningContext!.EffectPlan))
                    EffectAcceptedTurnPlanAuthority.InvalidateValidated(_validator._fs, lease);
                Dispose();
                return new[] { WoundIssue(pair.Key, "mortal_original_input_changed",
                    "the exact original observed input", "changed after capture") };
            }
            return Array.Empty<ValidationIssue>();
        }

        /// <summary>
        /// Rechecks every retained signed payload in bounded reader selections under the same canonical lease.
        /// Each selection must retain the original turn binding and valid current lifecycle contexts.
        /// </summary>
        /// <param name="fileSystem">
        /// Workspace containing the signed pending turn.
        /// </param>
        /// <param name="lease">
        /// Active canonical write lease held across every bounded selection.
        /// </param>
        /// <param name="paths">
        /// Nonempty logical path coverage from the authenticated manifest and required wound snapshot paths.
        /// </param>
        /// <param name="input">
        /// Actual captured accepted input supplying the required original turn binding.
        /// </param>
        /// <returns>
        /// Empty when every payload and binding remains valid; otherwise the first failing selection's diagnostics.
        /// </returns>
        private static IReadOnlyList<ValidationIssue> CheckSignedSnapshot(FileSystemManager fileSystem,
            FileSystemManager.CanonicalWriteLease lease, IReadOnlyList<string> paths, AcceptedMechanicsInput input)
        {
            if (paths.Count == 0)
                throw new InvalidOperationException("An original capture requires signed payload coverage.");
            // The reader bounds each selection to 64 paths; a real saved game may contain many more files.
            foreach (var chunk in paths.Chunk(32))
            {
                var read = PendingTurnSnapshotReader.ReadCurrent(fileSystem, lease, chunk);
                if (!read.Success || read.Snapshot == null)
                    return read.Issues;
                var snapshot = read.Snapshot;
                if (snapshot.SessionId != input.SessionId || snapshot.RequestId != input.RequestId ||
                    snapshot.SnapshotToken != input.SnapshotToken || snapshot.TurnNumber != input.Turn ||
                    snapshot.Realm != input.Realm)
                    return new[] { WoundIssue("game_state/control/pending_turn_snapshot.json",
                        "mortal_original_snapshot_changed", "the exact signed original snapshot", "changed") };
            }
            return Array.Empty<ValidationIssue>();
        }

        /// <summary>
        /// Abandons the unpublished draft and revokes this validator's retained capture identity.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _resources?.Dispose();
            _effects.Dispose();
            if (ReferenceEquals(_validator._mortalOriginalTurnCapture, this))
                _validator._mortalOriginalTurnCapture = null;
        }
    }
}
