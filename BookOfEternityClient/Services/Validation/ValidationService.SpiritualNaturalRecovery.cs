using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    private async Task<bool> HasNaturalProjectionHintAsync(FileSystemManager.CanonicalWriteLease lease)
    {
        var json = await _fs.ReadFileAsync(lease, LiveTurnPreparationService.TurnRequestPath);
        if (json is null) return false;
        try { return JsonNode.Parse(json)?["spiritualHealing"]?["mode"]?.GetValue<string>() == "natural"; }
        catch (Exception failure) when (failure is JsonException or InvalidOperationException or ArgumentException) { return false; }
    }

    private async Task<IReadOnlyList<ValidationIssue>?> TryPrepareSpiritualNaturalRecoveryAsync(
        FileSystemManager.CanonicalWriteLease lease, SpiritualSelfTreatmentPhase phase,
        SpiritualCycleInvocationData invocation, bool knownNatural)
    {
        // This hint requires refusal on lost proof; only the real observed original
        // below may select or admit a natural cycle.
        var expected = knownNatural || await HasNaturalProjectionHintAsync(lease);
        var selected = false;
        AfterlifeWoundHealingPlanner.Continuation? owner = null;
        var retain = false;
        try
        {
            var frame = await ReadSpiritualCycleManifestAsync(lease, invocation);
            if (frame?.ProgressionControl is not { } control ||
                !AfterlifeEntityProfileState.TryNormalizeEffectRealm(control.CurrentRealm, out var realm) ||
                realm is not ("chaos_sea" or "shining_abode"))
            {
                if (expected) throw new InvalidDataException("The selected natural cycle lost its actual original/control.");
                return null;
            }
            if (frame.Files.ContainsKey(AcceptedMechanicsPlan.WoundCommandPath))
            {
                if (expected) throw new InvalidDataException("The selected natural cycle was replaced by another signed wound family.");
                return null; // The original v1/self/other owner retains its own dispatcher.
            }
            if (!frame.Files.ContainsKey(WoundCarrierCatalog.AfterlifeProfilesPath))
            { if (expected) throw new InvalidDataException("The selected natural cycle lost its original profiles."); return null; }
            if (AcceptedMechanicsPlanAuthority.TryPeekSpiritualPublication(_fs, lease, out _))
                return null; // An actual private C4 owner, never a file-presence permission.

            var deferred = phase == SpiritualSelfTreatmentPhase.DeferredRouting;
            var observed = deferred ? PendingTurnSnapshotReader.ReadObservedCurrent(_fs, lease,
                new PendingTurnSnapshotPathSelection(new[] { "game_state/meta/soul_state.json", WoundCarrierCatalog.AfterlifeProfilesPath,
                    ProgressionScheduleService.SchedulePath }, new[] { AfterlifeSpiritualConflictState.StatePath,
                    ShiningAbodeState.StatePath, EffectAcceptedTurnInputComposer.WorldTimePath }))
                : AfterlifeWoundHealingPlanner.ReadOriginalNaturalSource(_fs, lease);
            if (!observed.Success || observed.Snapshot is not { } snapshot || observed.Evidence is not { } evidence)
            {
                return observed.Issues; // Missing original proof is never an ordinary no-plan permission.
            }
            if (snapshot.SessionId != frame.SessionId || snapshot.RequestId != frame.RequestId || snapshot.TurnNumber != frame.TurnNumber ||
                PendingTurnSnapshotAuthority.ComputeManifestPayloadHash(frame, LiveTurnPreparationService.ManifestHashJsonOptions,
                    static m => m.ManifestPayloadHash, static (m, hash) => m.ManifestPayloadHash = hash) != snapshot.SnapshotToken)
                throw new InvalidDataException("Natural cycle control differs from its actual signed original.");
            evidence.RequireCurrent(_fs, lease, snapshot);
            if (deferred)
            {
                JsonObject? Root(string path) => snapshot.CoveredLogicalPaths.Contains(path, StringComparer.Ordinal)
                    ? StrictJsonAuthority.Deserialize<JsonObject>(CanonicalJsonUtf8.DecodeOneOptionalBom(snapshot.ReadRequiredBytes(path)),
                        new JsonSerializerOptions(), path) : null;
                var profiles = Root(WoundCarrierCatalog.AfterlifeProfilesPath);
                var candidate = (profiles?["profiles"] as JsonArray)?.OfType<JsonObject>().Any(p =>
                    AfterlifeEntityProfileState.TryNormalizeEffectRealm(p["realm"]?.GetValue<string>(), out var r) && r == realm &&
                    p["activeWounds"] is JsonArray wounds && wounds.Count != 0) == true;
                if (!candidate) { if (expected) throw new InvalidDataException("The selected natural source lost its wound cohort."); return null; }
                if (control.AfterlifeCatchupRequired || Root(AfterlifeSpiritualConflictState.StatePath)?["activeConflict"] is not null ||
                    realm == "shining_abode" && ShiningAbodeState.GetPreparedIncarnationPackageMode(Root(ShiningAbodeState.StatePath)) !=
                        ShiningAbodeState.PreparedIncarnationPackageMode.Absent)
                { if (expected) throw new InvalidDataException("The selected natural cycle is no longer ordinary and safe."); return null; }
                var time = snapshot.CoveredLogicalPaths.Contains(EffectAcceptedTurnInputComposer.WorldTimePath, StringComparer.Ordinal)
                    ? snapshot.ReadRequiredBytes(EffectAcceptedTurnInputComposer.WorldTimePath) : null;
                if (!ProgressionScheduleService.TryBindPreparedSpiritualCycle(Root(ProgressionScheduleService.SchedulePath),
                        time, Root(ShiningAbodeState.StatePath), control, realm, out _))
                    throw new InvalidDataException("Natural routing lost its exact signed prepared cycle.");
                selected = true;
                AcceptedMechanicsPlanAuthority.InvalidateSpiritualSelfTreatment(_fs, lease);
                evidence.RequireCurrent(_fs, lease, snapshot);
                return Array.Empty<ValidationIssue>(); // No complete Admit, catalog, effects or IDs on A.
            }

            selected = true; // Actual original afterlife inventory is authenticated; corrupt source now refuses.
            var admission = await AfterlifeWoundHealingPlanner.Continuation.AdmitNaturalAsync(_fs, lease, frame, observed);
            if (admission.Published) return Array.Empty<ValidationIssue>();
            owner = admission.Owner;
            if (owner is null)
            { if (expected) throw new InvalidDataException("The selected natural cycle has no supported original cohort."); return null; }
            selected = true;
            owner.RequireCurrent(_fs, lease);
            // Actual observed proof remains owned and is rechecked after this DATA
            // scope; the DTO itself grants no original/source/publication authority.
            using var frameData = UsePrevalidatedPendingTurnSnapshotScope(frame);
            var issues = await ValidateAcceptedTurnRawResourceMaterializationCoreAsync(lease, naturalOwner: owner);
            owner.RequireCurrent(_fs, lease);
            retain = !issues.Any(i => i.Severity == IssueSeverity.Error) && AcceptedMechanicsPlanAuthority.HasValidated(_fs, lease);
            return issues;
        }
        catch (Exception failure) when (failure is not CoordinatedStatePublicationUncertainException &&
            failure is (JsonException or InvalidDataException or InvalidOperationException or ArgumentException or
                KeyNotFoundException or OverflowException or NullReferenceException))
        {
            if (!selected && !expected) return null;
            return new[] { new ValidationIssue(WoundCarrierCatalog.AfterlifeProfilesPath, IssueSeverity.Error,
                "Natural spiritual recovery could not form one complete ordinary contribution.",
                code: "spiritual_natural_recovery_admission_invalid", actual: failure.Message,
                repairHint: "Keep the same turn and cycle; correct only the report or requested weaker graphs. No treatment attempt or roll is created.") };
        }
        finally { if (!retain) owner?.Close(); }
    }

    private void CompleteNaturalWoundHandoff(AfterlifeWoundHealingPlanner.Continuation owner,
        WoundPreparedAcceptedTurnPlan prepared, EffectAcceptedTurnInput input,
        FileSystemManager.CanonicalWriteLease lease, AcceptedTurnWoundHandoffSink? sink,
        List<ValidationIssue> issues)
    {
        if (sink is null) throw new InvalidDataException("Natural recovery requires the actual ordinary common handoff sink.");
        owner.RequireCurrent(_fs, lease);
        owner.BindOrdinaryEffectInput(prepared, input);
        // One actual execution under B. Do not reuse a registry effect proof whose
        // private spiritual owner may have belonged to a prior closed lease.
        var effects = WoundEffectBatchPlanner.Build(prepared, input, new EffectIdentityFactory());
        issues.AddRange(effects.Issues);
        if (!effects.Success) return;
        var final = WoundAcceptedTurnPlanner.Finalize(prepared, effects);
        issues.AddRange(final.Issues);
        if (!final.Success) return;
        var woundInput = owner.Input;
        var bundle = new AcceptedMechanicsWoundStageBundle(woundInput, prepared, effects.Plan!, final.Plan!);
        sink.Value = new(new JsonObject
        {
            ["schemaVersion"] = 1, ["authorityKind"] = "accepted_spiritual_natural_recovery",
            ["inputFingerprint"] = bundle.InputFingerprint, ["bundleFingerprint"] = bundle.BundleFingerprint
        }, woundInput, bundle, null);
    }
}
