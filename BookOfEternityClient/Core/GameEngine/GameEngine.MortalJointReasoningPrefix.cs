using System.Text.Json;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.Core;

/// <summary>Detached source coordinates identify actual observed leaves, never reader authority.</summary>
internal sealed record MortalJointDebugPrefixSource(long Ordinal, string Path, string Route, string State);
/// <summary>Detached actual prefix/phase counters; no pass, original, owner or mutable source buffer escapes.</summary>
internal sealed record MortalJointDebugPrefixObservation(string Phase, string Disposition, bool HistoricalCurrentOnly,
    int Evaluations, int SourceChecks, int TimestampCalls, string? Fault, IReadOnlyList<MortalJointDebugPrefixSource> Sources, bool DataDisposed, bool AttemptExpired);

public partial class GameEngine
{
    private MortalJointDebugPrefixObservation? _mortalJointReasoningPrefixObservation;

    /// <summary>Copies the last actual prefix observation without granting phase or publication authority.</summary>
    internal MortalJointDebugPrefixObservation? CaptureMortalJointReasoningPrefixObservation() =>
        _mortalJointReasoningPrefixObservation is { } value ? value with { Sources = Array.AsReadOnly(value.Sources.ToArray()) } : null;

    private sealed class MortalJointDebugPrefixRejectedException(IReadOnlyList<ValidationIssue> issues)
        : Exception("Actual debug prefix refused common preparation.")
    {
        internal IReadOnlyList<ValidationIssue> Issues { get; } = Array.AsReadOnly(issues.Select(WoundAcceptedTurnData.CloneIssue).ToArray());
    }

    internal sealed partial class MortalJointPublicationAttempt
    {
        private readonly byte[]? _prefixGenerationBytes;
        private readonly FileSystemManager.MainAdmission? _prefixMain;
        private readonly long _prefixIteration;
        private readonly MortalJointInterfaceReady? _prefixReady;
        private readonly HotOrdinaryOriginal? _prefixStage;
        private readonly string _prefixCheckpoint;
        private readonly bool _prefixResume, _prefixSelected;
        private readonly int _prefixProtocol;
        private readonly string _prefixManifestData, _prefixAuthorityData;
        private ValidationService.DebugPrefixPass? _prefix;
        private int _prefixEvaluations, _prefixChecks;

        private bool RequireLegacyPrefixDispatch(bool capturing)
        {
            if (_prefixSelected) return false;
            RequireActualOwner(_files, _lease);
            if (capturing ? !_capturing || _phase != "Capturing" : _capturing || _phase != "PreparationStarted" || _stages.Count != 0)
                throw new InvalidDataException("debug_prefix_legacy_phase_changed");
            if (_delegated || _close is not null || _prefixProtocol != 1 ||
                _engine._mortalJointRepairCheckpoint is not { UsesInterfacePublication: false } checkpoint)
                throw new InvalidDataException("debug_prefix_legacy_dispatch_changed");
            var binding = checkpoint.ToJsonObject();
            if (binding["schemaVersion"]!.GetValue<int>() != _prefixProtocol ||
                binding["fingerprint"]!.GetValue<string>() != _prefixCheckpoint)
                throw new InvalidDataException("debug_prefix_legacy_checkpoint_changed");
            if (_prefix is not null || _prefixEvaluations != 0 || _prefixChecks != 0)
                throw new InvalidDataException("debug_prefix_legacy_cannot_retain_selected_data");
            _engine._mortalJointReasoningPrefixObservation = new(_phase, "UnavailableLegacyProtocol", false,
                _prefixEvaluations, _prefixChecks, 0, null, Array.Empty<MortalJointDebugPrefixSource>(), true, false);
            return true;
        }

        private ValidatedPendingTurnSnapshotContext RequirePrefixBinding(bool capturing)
        {
            RequireActualOwner(_files, _lease);
            RequireOrdinaryJointRepairLease(_lease);
            if (capturing ? !_capturing || _phase != "Capturing" : _capturing || _phase != "PreparationStarted" || _stages.Count != 0)
                throw new InvalidDataException("debug_prefix_attempt_phase_unavailable");
            if (_delegated || _close is not null || _frame is not ValidatedPendingTurnSnapshotContext frame ||
                _engine._mortalJointRepairCheckpoint is not { } checkpoint ||
                checkpoint.ToJsonObject()["fingerprint"]!.GetValue<string>() != _prefixCheckpoint ||
                checkpoint.ToJsonObject()["schemaVersion"]!.GetValue<int>() != _prefixProtocol ||
                checkpoint.UsesInterfacePublication != _prefixSelected ||
                _engine._mortalJointInterfaceValidationIteration != _prefixIteration)
                throw new InvalidDataException("debug_prefix_attempt_binding_changed");
            if (_engine._validator.CaptureAcceptedTurnReasoningOriginalData(frame.Manifest) != _prefixManifestData ||
                JsonSerializer.Serialize(frame.Payload) != _prefixAuthorityData)
                throw new InvalidDataException("debug_prefix_actual_frame_data_changed");
            var main = _lease.MainAdmission ?? throw new InvalidDataException("debug_prefix_main_missing");
            main.Validate(_lease);
            if (!ReferenceEquals(main, _prefixMain) || _prefixMain is not { Acquired: true, Closed: false })
                throw new InvalidDataException("debug_prefix_main_replaced");
            using var fresh = FileSystemManager.SuppressValidationReuse();
            var generation = _files.ReadLocalGenerationSnapshot(_lease);
            if (generation.Binding.Id != _generation || _prefixGenerationBytes is null || generation.Bytes is null ||
                !generation.Bytes.AsSpan().SequenceEqual(_prefixGenerationBytes))
                throw new InvalidDataException("debug_prefix_physical_generation_changed");
            if (_prefixResume)
            {
                // The existing I dispatcher/graph authenticated this current-only
                // continuation. No fresh Stage/Ready memory or old prefix is invented.
                if (!_engine.IsCurrentMortalJointNarrativeResume(checkpoint))
                    throw new InvalidDataException("debug_prefix_current_resume_changed");
            }
            else if (_prefixIteration <= 0 || !ReferenceEquals(_prefixReady, _engine._mortalJointInterfaceReady) ||
                !ReferenceEquals(_prefixStage, _engine._hotOrdinaryOriginal) ||
                _prefixReady is not { Faulted: false, DeleteReturned: true, ReleaseReturned: true } ||
                _prefixStage is not { Preparation: not null, Ordinal: > 0 } ||
                !ReferenceEquals(_prefixReady.Stage, _prefixStage) || _prefixStage.Generation != _generation ||
                _prefixReady.Owner.Closed || !_prefixReady.Owner.Acquired || !main.SharesAccess(_prefixReady.Owner) ||
                !SameMortalJointOriginalFrame(_prefixReady.Frame, frame, frame.TurnNumber))
                throw new InvalidDataException("debug_prefix_fresh_stage_ready_changed");
            _lease.EnsureNoPendingLocalDecision();
            return frame;
        }

        internal async Task<IReadOnlyList<ValidationIssue>> ConsumeDebugPrefixAsync(object issuer)
        {
            RequireIssuer(issuer);
            if (RequireLegacyPrefixDispatch(capturing: true)) return Array.Empty<ValidationIssue>();
            var frame = RequirePrefixBinding(capturing: true);
            if (_prefix is { } retained)
            {
                await retained.RequireCurrentAsync(_files, _lease);
                RequirePrefixBinding(capturing: true); _prefixChecks++;
                ObservePrefix(retained); return retained.Issues;
            }
            ValidationService.DebugPrefixPass? produced = null;
            try
            {
                produced = await _engine._validator.EvaluateObservedDebugPrefixAsync(_lease);
                _prefixEvaluations++;
                if (!ReferenceEquals(frame, RequirePrefixBinding(capturing: true)))
                    throw new InvalidDataException("debug_prefix_actual_frame_replaced");
                produced.RequireIssuer(_engine._validator);
                // Full private-validator DTO equality joins scoped/supplied DATA
                // to this actual physically authenticated engine frame. No caller
                // result can be imported through this method.
                produced.RequireOriginalContext(frame.Manifest);
                await produced.RequireCurrentAsync(_files, _lease);
                RequirePrefixBinding(capturing: true); _prefixChecks++;
                _prefix = produced; produced = null;
                ObservePrefix(_prefix); return _prefix.Issues;
            }
            catch (Exception error)
            {
                ObservePrefix(produced, error.Message);
                throw;
            }
            finally { produced?.Dispose(); }
        }

        internal async Task RecheckClosedDebugPrefixAsync(object issuer)
        {
            RequireIssuer(issuer);
            if (RequireLegacyPrefixDispatch(capturing: false)) return;
            var frame = RequirePrefixBinding(capturing: false);
            var retained = _prefix ?? throw new InvalidDataException("debug_prefix_closed_source_missing");
            try
            {
                // This is only a fresh refusal fence over detached source DATA;
                // it cannot evaluate, reenter Capturing or attach new evidence.
                await retained.RequireCurrentAsync(_files, _lease);
                if (!ReferenceEquals(frame, RequirePrefixBinding(capturing: false)))
                    throw new InvalidDataException("debug_prefix_closed_frame_changed");
                _prefixChecks++; ObservePrefix(retained);
            }
            catch (Exception error)
            {
                ObservePrefix(retained, error.Message); retained.Dispose(); _prefix = null;
                MarkPrefixDataDisposed(); throw;
            }
        }

        private void ObservePrefix(ValidationService.DebugPrefixPass? value, string? fault = null)
        {
            var data = value?.CaptureDiagnostic();
            _engine._mortalJointReasoningPrefixObservation = new(_phase, value?.Disposition ?? "Unavailable",
                _prefixResume, _prefixEvaluations, _prefixChecks, data?.TimestampCalls ?? 0, fault ?? data?.Fault,
                Array.AsReadOnly(data?.Occurrences.Select(row => new MortalJointDebugPrefixSource(row.Ordinal, row.Path, row.Route, row.State)).ToArray() ?? []), false, false);
        }
        private void MarkPrefixDataDisposed()
        {
            if (_engine._mortalJointReasoningPrefixObservation is { } observation)
                _engine._mortalJointReasoningPrefixObservation = observation with { DataDisposed = true };
        }
        private void ReleaseDebugPrefix()
        {
            if (_prefix is { } retained) { ObservePrefix(retained); retained.Dispose(); _prefix = null; }
            MarkPrefixDataDisposed();
            if (_prefixGenerationBytes is not null) Array.Clear(_prefixGenerationBytes);
        }
    }
}
