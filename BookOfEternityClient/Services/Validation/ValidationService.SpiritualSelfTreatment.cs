using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    private enum SpiritualSelfTreatmentPhase { DirectHeldLease, DeferredRouting, Publication }

    /// <summary>Prepares the self contribution only under the coordinator's actual publication lease.</summary>
    /// <param name="lease">The active lease held through preparation and common publication.</param>
    /// <returns>Fresh preparation issues, without writes or an executable fallback on failure.</returns>
    internal async Task<IReadOnlyList<ValidationIssue>> PrepareSpiritualSelfTreatmentPublicationAsync(
        FileSystemManager.CanonicalWriteLease lease)
    {
        _fs.EnsureCanonicalWriteLeaseActive(lease);
        // A missing live request still requires its signed family's strict replay
        // proof. Mutable absence cannot select the ordinary no-plan route.
        return await TryPrepareSpiritualSelfTreatmentAsync(lease, SpiritualSelfTreatmentPhase.Publication)
            ?? Array.Empty<ValidationIssue>();
    }

    private async Task<IReadOnlyList<ValidationIssue>?> TryPrepareSpiritualSelfTreatmentAsync(
        FileSystemManager.CanonicalWriteLease lease,
        SpiritualSelfTreatmentPhase phase = SpiritualSelfTreatmentPhase.DirectHeldLease)
    {
        var deferPublication = phase == SpiritualSelfTreatmentPhase.DeferredRouting;
        var currentBytes = await _fs.ReadFileBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath);
        var liveCandidate = false;
        if (currentBytes is not null)
        {
            try
            {
                using var classifier = JsonDocument.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(currentBytes));
                var root = classifier.RootElement;
                liveCandidate = root.ValueKind == JsonValueKind.Object &&
                    (root.TryGetProperty("schemaVersion", out var version) && version.ValueKind == JsonValueKind.Number && version.TryGetInt32(out var v) && v == 2 ||
                     root.TryGetProperty("kind", out var kind) && kind.ValueKind == JsonValueKind.String && kind.GetString() == "spiritual_self_treatment_request");
            }
            catch (Exception failure) when (failure is JsonException or System.Text.DecoderFallbackException)
            { } // The authenticated original still selects the family.
        }

        AfterlifeWoundHealingPlanner.Continuation? owner = null;
        var retainOwner = false;
        var spiritualOriginal = false;
        try
        {
            var manifestText = await _fs.ReadFileAsync(lease, LiveTurnPreparationService.PendingTurnSnapshotManifestPath);
            if (manifestText is null) return null;
            var frame = StrictJsonAuthority.Deserialize<LiveTurnPendingSnapshotManifest>(manifestText,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, "spiritual treatment manifest");
            if (frame?.Files.ContainsKey(AcceptedMechanicsPlan.WoundCommandPath) != true) return null;
            // Ordinary v1 needs only its original family classification here. A live
            // v2 candidate shares the full observed source DATA directly with admission.
            var observed = liveCandidate && !deferPublication ? AfterlifeWoundHealingPlanner.ReadOriginalSource(_fs, lease) :
                PendingTurnSnapshotReader.ReadObservedCurrent(_fs, lease, new[] { AcceptedMechanicsPlan.WoundCommandPath });
            if (!observed.Success || observed.Snapshot is not { } signed || observed.Evidence is not { } evidence)
                return liveCandidate ? observed.Issues : null;
            var original = StrictJsonAuthority.Deserialize<JsonObject>(CanonicalJsonUtf8.DecodeOneOptionalBom(
                signed.ReadRequiredBytes(AcceptedMechanicsPlan.WoundCommandPath)), new JsonSerializerOptions(), "original wound request")!;
            if (original["kind"]?.GetValue<string>() != "spiritual_self_treatment_request") return null;
            spiritualOriginal = true;
            if (frame.ProgressionControl is null || frame.ManifestPayloadHash != signed.SnapshotToken ||
                PendingTurnSnapshotAuthority.ComputeManifestPayloadHash(frame, LiveTurnPreparationService.ManifestHashJsonOptions,
                    static m => m.ManifestPayloadHash, static (m, hash) => m.ManifestPayloadHash = hash) != signed.SnapshotToken)
                throw new InvalidDataException("Spiritual treatment lost its original signed control.");
            evidence.RequireCurrent(_fs, lease, signed);
            // A former self entry may retain its full planning result after the
            // generic validated slot was cleared. Its old live owner cannot cross leases.
            if (phase != SpiritualSelfTreatmentPhase.DirectHeldLease)
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
            foreach (var path in new[] { ResourceMaterializationContract.CommandPath, EffectAcceptedTurnPlan.CommandPath,
                         ResourcePendingResolutionState.PendingPath })
                if (await _fs.ReadFileBytesAsync(lease, path) is not null)
                    throw new InvalidDataException("Concurrent resource/effect/pending work requires its ordinary owner: " + path);
            // The real GameEngine releases this diagnostic lease before refresh.
            // Full catalog admission, scheduler mutation and all identity allocation
            // occur once, under the coordinator's later publication lease.
            if (deferPublication) return Array.Empty<ValidationIssue>();
            owner = await AfterlifeWoundHealingPlanner.Continuation.AdmitAsync(_fs, lease, frame,
                liveCandidate ? observed : null);
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
        catch (Exception failure) when (failure is JsonException or InvalidDataException or InvalidOperationException or
                                           ArgumentException or OverflowException or KeyNotFoundException or NullReferenceException)
        {
            if (!spiritualOriginal && !liveCandidate) return null; // Preserve the original v1 parser's failures.
            return new[] { new ValidationIssue(AcceptedMechanicsPlan.WoundCommandPath, IssueSeverity.Error,
                "Spiritual self-treatment could not produce one complete accepted contribution.",
                code: "spiritual_self_treatment_admission_invalid", actual: failure.Message,
                repairHint: "Keep the original attempt and dice. Correct the exact signed source, progression report or requested destination graphs.") };
        }
        finally
        {
            if (!retainOwner) owner?.Close();
        }
    }
}
