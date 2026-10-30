using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    private enum SpiritualSelfTreatmentPhase { DirectHeldLease, DeferredRouting, Publication }

    // Manifest DATA shared only within this invocation/lease; it grants no source authority.
    private sealed class SpiritualCycleInvocationData
    {
        internal bool Read;
        internal string? Json;
        internal LiveTurnPendingSnapshotManifest? Manifest;
    }

    private async Task<LiveTurnPendingSnapshotManifest?> ReadSpiritualCycleManifestAsync(
        FileSystemManager.CanonicalWriteLease lease, SpiritualCycleInvocationData data)
    {
        if (!data.Read)
        {
            data.Json = await _fs.ReadFileAsync(lease, LiveTurnPreparationService.PendingTurnSnapshotManifestPath);
            data.Read = true;
        }
        return data.Json is null ? null : data.Manifest ??= StrictJsonAuthority.Deserialize<LiveTurnPendingSnapshotManifest>(data.Json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, "spiritual cycle manifest");
    }

    private async Task<IReadOnlyList<ValidationIssue>?> TryPrepareSpiritualCycleAsync(
        FileSystemManager.CanonicalWriteLease lease, SpiritualSelfTreatmentPhase phase)
    {
        var data = new SpiritualCycleInvocationData();
        var naturalExpected = AcceptedMechanicsPlanAuthority.HasOpenSpiritualSelfTreatment(_fs, lease, natural: true);
        var self = await TryPrepareSpiritualSelfTreatmentAsync(lease, phase, data);
        return self ?? await TryPrepareSpiritualNaturalRecoveryAsync(lease, phase, data, naturalExpected);
    }

    /// <summary>Prepares the self contribution only under the coordinator's actual publication lease.</summary>
    /// <param name="lease">The active lease held through preparation and common publication.</param>
    /// <returns>Fresh preparation issues, without writes or an executable fallback on failure.</returns>
    internal async Task<IReadOnlyList<ValidationIssue>> PrepareSpiritualSelfTreatmentPublicationAsync(
        FileSystemManager.CanonicalWriteLease lease)
    {
        _fs.EnsureCanonicalWriteLeaseActive(lease);
        // A missing live request still requires its signed family's strict replay
        // proof. Mutable absence cannot select the ordinary no-plan route.
        return await TryPrepareSpiritualCycleAsync(lease, SpiritualSelfTreatmentPhase.Publication)
            ?? Array.Empty<ValidationIssue>();
    }

    private async Task<IReadOnlyList<ValidationIssue>?> TryPrepareSpiritualSelfTreatmentAsync(
        FileSystemManager.CanonicalWriteLease lease,
        SpiritualSelfTreatmentPhase phase,
        SpiritualCycleInvocationData invocation)
    {
        var deferPublication = phase == SpiritualSelfTreatmentPhase.DeferredRouting;
        var currentBytes = await _fs.ReadFileBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath);
        var liveCandidate = false;
        var liveDiagnosis = false;
        if (currentBytes is not null)
        {
            try
            {
                using var classifier = JsonDocument.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(currentBytes));
                var root = classifier.RootElement;
                liveCandidate = root.ValueKind == JsonValueKind.Object &&
                    (root.TryGetProperty("schemaVersion", out var version) && version.ValueKind == JsonValueKind.Number && version.TryGetInt32(out var v) && v == 2 ||
                     root.TryGetProperty("kind", out var kind) && kind.ValueKind == JsonValueKind.String &&
                        kind.GetString() is "spiritual_self_treatment_request" or "spiritual_treatment_request" or "spiritual_diagnosis_request");
                liveDiagnosis = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("kind", out var family) &&
                    family.ValueKind == JsonValueKind.String && family.GetString() == "spiritual_diagnosis_request";
            }
            catch (Exception failure) when (failure is JsonException or System.Text.DecoderFallbackException)
            { } // The authenticated original still selects the family.
        }

        AfterlifeWoundHealingPlanner.Continuation? owner = null;
        var retainOwner = false;
        var spiritualOriginal = false;
        var diagnosisOriginal = false;
        // These hints can only require refusal when original proof is lost. They
        // never select an admissible family or substitute for the fresh reader.
        var sourceExpected = liveCandidate || AcceptedMechanicsPlanAuthority.HasOpenSpiritualSelfTreatment(_fs, lease) ||
            _prevalidatedPendingTurnSnapshotOverride?.Files.ContainsKey(AcceptedMechanicsPlan.WoundCommandPath) == true;
        if (phase == SpiritualSelfTreatmentPhase.Publication)
            AcceptedMechanicsPlanAuthority.InvalidateSpiritualSelfTreatment(_fs, lease);
        try
        {
            var frame = await ReadSpiritualCycleManifestAsync(lease, invocation);
            if (frame is null)
            {
                if (sourceExpected) throw new InvalidDataException("Spiritual treatment requires its signed original manifest.");
                return null;
            }
            if (frame?.Files.ContainsKey(AcceptedMechanicsPlan.WoundCommandPath) != true)
            {
                if (sourceExpected) throw new InvalidDataException("Spiritual treatment requires its signed original request member.");
                return null;
            }
            // Ordinary v1 needs only its original family classification here. A live
            // v2 candidate shares the full observed source DATA directly with admission.
            var observed = liveCandidate && !deferPublication ? liveDiagnosis
                ? AfterlifeWoundHealingPlanner.ReadOriginalDiagnosisSource(_fs, lease,
                    AfterlifeEntityProfileState.TryNormalizeEffectRealm(frame.ProgressionControl?.CurrentRealm, out var diagnosisRealm) ? diagnosisRealm : "")
                : AfterlifeWoundHealingPlanner.ReadOriginalSource(_fs, lease) :
                PendingTurnSnapshotReader.ReadObservedCurrent(_fs, lease, new[] { AcceptedMechanicsPlan.WoundCommandPath });
            if (!observed.Success || observed.Snapshot is not { } signed || observed.Evidence is not { } evidence)
                return sourceExpected ? observed.Issues : null;
            var original = StrictJsonAuthority.Deserialize<JsonObject>(CanonicalJsonUtf8.DecodeOneOptionalBom(
                signed.ReadRequiredBytes(AcceptedMechanicsPlan.WoundCommandPath)), new JsonSerializerOptions(), "original wound request")!;
            var diagnosis = SpiritualDiagnosisRequestCodec.IsKind(original);
            diagnosisOriginal = diagnosis;
            if (!diagnosis && !SpiritualSelfTreatmentRequestCodec.IsActiveKind(original)) return null;
            spiritualOriginal = true;
            if (frame.ProgressionControl is null || frame.ManifestPayloadHash != signed.SnapshotToken ||
                PendingTurnSnapshotAuthority.ComputeManifestPayloadHash(frame, LiveTurnPreparationService.ManifestHashJsonOptions,
                    static m => m.ManifestPayloadHash, static (m, hash) => m.ManifestPayloadHash = hash) != signed.SnapshotToken)
                throw new InvalidDataException("Spiritual treatment lost its original signed control.");
            evidence.RequireCurrent(_fs, lease, signed);
            var context = new SpiritualSelfTreatmentStageContext(signed.SessionId, signed.RequestId, signed.TurnNumber, frame.ProgressionControl);
            if (diagnosis)
            {
                if (!SpiritualDiagnosisRequestCodec.IsBound(original, context))
                    throw new InvalidDataException("Diagnosis requires its exact signed request and full original control.");
            }
            else
            {
                var selection = SpiritualSelfTreatmentRequestCodec.ReadActiveSelection(original);
                if (!AfterlifeEntityProfileState.TryNormalizeEffectRealm(frame.ProgressionControl.CurrentRealm, out var realm) ||
                    !SpiritualSelfTreatmentRequestCodec.IsBoundActive(original, context, selection, realm, frame.ProgressionControl.NextChaosSeaTurnOrdinal))
                    throw new InvalidDataException("Spiritual treatment requires its exact closed signed active family.");
            }
            // A former self entry may retain its full planning result after the
            // generic validated slot was cleared. Its old live owner cannot cross leases.
            if (phase == SpiritualSelfTreatmentPhase.DeferredRouting)
                AcceptedMechanicsPlanAuthority.InvalidateSpiritualSelfTreatment(_fs, lease);
            if (currentBytes is null)
            {
                var scheduler = new ProgressionScheduleService(_fs, Microsoft.Extensions.Logging.Abstractions.NullLogger<ProgressionScheduleService>.Instance);
                if (await scheduler.IsPublishedSpiritualCycleAsync(frame.ProgressionControl, lease, signed.SnapshotToken, original)) return Array.Empty<ValidationIssue>();
                return new[] { new ValidationIssue(AcceptedMechanicsPlan.WoundCommandPath, IssueSeverity.Error,
                    "The signed spiritual request is absent without its exact published result.", code: "spiritual_self_treatment_replay_conflict") };
            }
            if (!currentBytes.AsSpan().SequenceEqual(signed.ReadRequiredBytes(AcceptedMechanicsPlan.WoundCommandPath)))
                throw new InvalidDataException("The retained spiritual treatment request changed.");
            foreach (var path in diagnosis ? new[] { ResourcePendingResolutionState.PendingPath } :
                new[] { ResourceMaterializationContract.CommandPath, EffectAcceptedTurnPlan.CommandPath, ResourcePendingResolutionState.PendingPath })
                if (await _fs.ReadFileBytesAsync(lease, path) is not null)
                    throw new InvalidDataException("Concurrent resource/effect/pending work requires its ordinary owner: " + path);
            // The real GameEngine releases this diagnostic lease before refresh.
            // Full catalog admission, scheduler mutation and all identity allocation
            // occur once, under the coordinator's later publication lease.
            if (deferPublication) return Array.Empty<ValidationIssue>();
            owner = diagnosis ? await AfterlifeWoundHealingPlanner.Continuation.AdmitDiagnosisAsync(_fs, lease, frame,
                liveDiagnosis ? observed : null) : await AfterlifeWoundHealingPlanner.Continuation.AdmitAsync(_fs, lease, frame,
                liveCandidate ? observed : null);
            if (diagnosis)
            {
                using var frameData = UsePrevalidatedPendingTurnSnapshotScope(frame);
                var issues = await ValidateAcceptedTurnRawResourceMaterializationCoreAsync(lease, naturalOwner: owner);
                owner.RequireCurrent(_fs, lease);
                retainOwner = !issues.Any(i => i.Severity == IssueSeverity.Error) && AcceptedMechanicsPlanAuthority.HasValidated(_fs, lease);
                return issues;
            }
            var prepared = owner.Prepare();
            var effectInput = owner.ComposeEffectInput(prepared);
            var effect = WoundEffectBatchPlanner.Build(prepared, effectInput, new EffectIdentityFactory());
            if (!effect.Success) return effect.Issues;
            var final = WoundAcceptedTurnPlanner.Finalize(prepared, effect);
            if (!final.Success) return final.Issues;
            var bundle = new AcceptedMechanicsWoundStageBundle(owner.Input, prepared, effect.Plan!, final.Plan!);
            owner.RequireCurrent(_fs, lease);
            var common = AcceptedMechanicsPlanAuthority.GetOrBuildWoundValidated(_fs, lease, bundle);
            if (!common.Success && common.Issues.Count == 0) throw new InvalidDataException("Spiritual treatment produced no complete common plan.");
            retainOwner = common.Success;
            return common.Issues;
        }
        catch (Exception failure) when (failure is not CoordinatedStatePublicationUncertainException &&
            failure is (JsonException or InvalidDataException or InvalidOperationException or
                ArgumentException or OverflowException or KeyNotFoundException or NullReferenceException))
        {
            if (!spiritualOriginal && !sourceExpected) return null; // Preserve the original v1 parser's failures.
            return new[] { new ValidationIssue(AcceptedMechanicsPlan.WoundCommandPath, IssueSeverity.Error,
                diagnosisOriginal ? "Spiritual diagnosis could not produce its complete knowledge and ordinary recovery contribution."
                    : "Spiritual self-treatment could not produce one complete accepted contribution.",
                code: diagnosisOriginal ? "spiritual_diagnosis_admission_invalid" : "spiritual_self_treatment_admission_invalid", actual: failure.Message,
                repairHint: diagnosisOriginal ? "Keep the exact diagnostic request/control. No diagnosis dice or fees may be created; correct only the source, progression report or requested natural graphs."
                    : "Keep the original attempt and dice. Correct the exact signed source, progression report or requested destination graphs.") };
        }
        finally
        {
            if (!retainOwner) owner?.Close();
        }
    }
}
