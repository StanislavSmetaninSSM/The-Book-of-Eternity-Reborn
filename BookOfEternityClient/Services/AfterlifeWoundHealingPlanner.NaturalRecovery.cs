using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;

namespace BookOfEternityClient.Services;

internal static partial class AfterlifeWoundHealingPlanner
{
    private static readonly string[] NaturalRequiredPaths =
    [
        "game_state/meta/soul_state.json", WoundCarrierCatalog.AfterlifeProfilesPath,
        WoundIdentityState.StatePath, WoundHistoryState.HistoryPath,
        EffectIdentityState.StatePath, ProgressionScheduleService.SchedulePath
    ];

    internal static PendingTurnSnapshotObservedReadResult ReadOriginalNaturalSource(FileSystemManager files,
        FileSystemManager.CanonicalWriteLease lease) => PendingTurnSnapshotReader.ReadObservedCurrent(files, lease,
            new PendingTurnSnapshotPathSelection(NaturalRequiredPaths, AfterlifeWoundTreatmentAuthority.SourcePaths
                .Append(AcceptedMechanicsPlan.WoundCommandPath).Except(NaturalRequiredPaths, StringComparer.Ordinal).ToArray()));

    internal sealed partial class Continuation
    {
        internal JsonObject? NaturalProjection => _natural is null ? null : SpiritualHealingRequestProjection.CreateNatural(_natural);

        internal static async Task<Continuation?> AdmitNaturalAsync(FileSystemManager files,
            FileSystemManager.CanonicalWriteLease lease, LiveTurnPendingSnapshotManifest manifest,
            PendingTurnSnapshotObservedReadResult? observedSource = null)
        {
            var observed = observedSource ?? ReadOriginalNaturalSource(files, lease);
            if (!observed.Success || observed.Snapshot is not { } snapshot || observed.Evidence is not { } evidence)
                throw new InvalidDataException("Signed natural recovery source is unavailable: " + string.Join(";", observed.Issues.Select(i => i.Code)));
            if (snapshot.SessionId != manifest.SessionId || snapshot.RequestId != manifest.RequestId ||
                snapshot.TurnNumber != manifest.TurnNumber || snapshot.SnapshotToken != manifest.ManifestPayloadHash ||
                manifest.ProgressionControl is null || PendingTurnSnapshotAuthority.ComputeManifestPayloadHash(manifest,
                    LiveTurnPreparationService.ManifestHashJsonOptions, static m => m.ManifestPayloadHash,
                    static (m, hash) => m.ManifestPayloadHash = hash) != snapshot.SnapshotToken ||
                !PendingTurnSnapshotAuthority.HasValidatedRollbackSnapshotCoverage(manifest, m => m.Files,
                    m => m.SnapshotFileHashes, m => m.RollbackBaselineFiles,
                    AfterlifeWoundTreatmentAuthority.SourcePaths.Append(AcceptedMechanicsPlan.WoundCommandPath).ToArray(), out _) ||
                snapshot.CoveredLogicalPaths.Contains(AcceptedMechanicsPlan.WoundCommandPath, StringComparer.Ordinal))
                throw new InvalidDataException("Natural recovery requires the exact ordinary signed turn, without a signed wound-command family.");
            evidence.RequireCurrent(files, lease, snapshot);
            var sources = ReadSignedSources(snapshot);
            var context = new SpiritualSelfTreatmentStageContext(snapshot.SessionId, snapshot.RequestId, snapshot.TurnNumber,
                JsonSerializer.Deserialize<ProgressionControl>(JsonSerializer.Serialize(manifest.ProgressionControl))!);
            var data = AfterlifeWoundTreatmentAuthority.PrepareNaturalSourceData(context, sources);
            if (data is null) return null;
            if (await files.ReadFileBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath) is not null)
                throw new InvalidDataException("Natural recovery cannot overwrite concurrent wound commands or opportunity stages.");
            var currentImages = new Dictionary<string, CanonicalBeforeImage>(StringComparer.Ordinal);
            foreach (var source in sources)
            {
                var current = await files.ReadFileBytesAsync(lease, source.Path);
                var image = new CanonicalBeforeImage(current is not null, current);
                // These client-owned original baselines cannot be rewritten by raw
                // ordinary input. Lawful profile/resource siblings are composed later.
                if (source.Path is WoundIdentityState.StatePath or WoundHistoryState.HistoryPath or EffectIdentityState.StatePath or
                    ProgressionScheduleService.SchedulePath or EffectAcceptedTurnInputComposer.WorldTimePath or
                    AfterlifeSpiritualConflictState.StatePath or ShiningAbodeState.StatePath)
                    if (image.Fingerprint != source.Image.Fingerprint)
                        throw new InvalidDataException("Natural recovery original baseline changed: " + source.Path);
                currentImages.Add(source.Path, image);
            }
            var roots = sources.ToDictionary(s => s.Path, s => s.Root, StringComparer.Ordinal);
            var input = CreateCycleInput(snapshot, data.Carriers, roots);
            var (wounds, stages) = SpiritualHealingRequestProjection.BuildNaturalProgram(data);
            var (scheduler, mutation) = await PrepareCycleSchedulerAsync(files, lease, context.Progression, data.CycleOrdinal);
            var response = await files.ReadFileBytesAsync(lease, "output/interface_updates.json");
            var updates = response is null ? null : StrictJsonAuthority.Deserialize<JsonObject>(
                CanonicalJsonUtf8.DecodeOneOptionalBom(response), new JsonSerializerOptions(), "natural recovery graph response");
            var owner = new Continuation(files, lease, snapshot, evidence, currentImages, null, null, input,
                wounds, stages, context.Progression, scheduler, mutation, new(response is not null, response),
                sources.ToDictionary(s => s.Path, s => (JsonNode?)s.Root, StringComparer.Ordinal), data);
            owner.AdmitGraphs(updates);
            owner.RequireCurrent(files, lease);
            return owner;
        }

        // The ordinary composer already owns actual accepted source/target exports
        // and resource after-images. Bind its real input rather than replacing it
        // with self-treatment's empty command root and source projections.
        internal void BindOrdinaryEffectInput(WoundPreparedAcceptedTurnPlan prepared, EffectAcceptedTurnInput input)
        {
            if (!IsNatural || !PreparedAgrees(prepared) || !WoundAcceptedTurnPlannerCore.EffectInputAgreesWithPrepared(prepared, input))
                throw new InvalidDataException("Natural recovery received a foreign ordinary effect input.");
            RequireExecutionCurrent();
            ValidateProtectedOrdinaryWounds(input);
            var fingerprint = WoundAcceptedTurnFingerprints.ComputeEffectInput(prepared, input);
            if (_composedEffectInputFingerprint is not null && _composedEffectInputFingerprint != fingerprint)
                throw new InvalidDataException("The actual ordinary effect input changed within the natural recovery owner.");
            _composedEffectInputFingerprint = fingerprint;
        }

        private void ValidateProtectedOrdinaryWounds(EffectAcceptedTurnInput input)
        {
            // Use parsed roots supplied by the actual ordinary composer; no second
            // physical read/catalog parse is needed to compare protected wound data.
            var current = input.PublicationCarrierBaselines?.AfterlifeProfiles
                ?? throw new InvalidDataException("Natural recovery requires current ordinary profile baselines.");
            foreach (var owner in _wounds.GroupBy(w => w.Owner))
            {
                if (!WoundCarrierCollectionAuthority.TryResolve(current, owner.Key, out var rows, out _) ||
                    rows.Count != _natural!.Catalog.Occurrences.Count(o => o.Wound.Owner == owner.Key))
                    throw new InvalidDataException("Natural recovery cannot silently drop or replace an original owner/wound cohort.");
                foreach (var wound in owner)
                {
                    var matching = rows.OfType<JsonObject>().Where(r => r["woundId"]?.GetValue<string>() == wound.WoundId).ToArray();
                    if (matching.Length != 1 || !WoundCarrierCollectionAuthority.MatchesSemanticBeforeImage(matching[0],
                            wound, wound.Owner, wound.Owner.CarrierPath + ".naturalBefore"))
                        throw new InvalidDataException("An ordinary input changed a protected original spiritual wound.");
                }
                var profile = (current["profiles"] as JsonArray)?.OfType<JsonObject>()
                    .Single(p => WoundCarrierCollectionAuthority.AfterlifeProfileMatches(p, owner.Key))
                    ?? throw new InvalidDataException("Natural recovery lost its original persistent owner.");
                var woundIds = owner.Select(w => w.WoundId).ToHashSet(StringComparer.Ordinal);
                bool IsProtectedEffect(JsonObject effect) => effect["source"]?["kind"]?.GetValue<string>() == "wound" &&
                    woundIds.Contains(effect["source"]?["sourceId"]?.GetValue<string>() ?? "");
                var originalEffects = _natural.Effects.Occurrences.Where(o => IsProtectedEffect(o.Effect))
                    .ToDictionary(o => o.EffectId, o => o.Effect, StringComparer.Ordinal);
                var currentEffects = (profile["activeEffects"] as JsonArray)?.OfType<JsonObject>().Where(IsProtectedEffect)
                    .ToDictionary(e => e["effectId"]!.GetValue<string>(), StringComparer.Ordinal)
                    ?? throw new InvalidDataException("Natural recovery requires its current original wound effects.");
                if (originalEffects.Count != currentEffects.Count || originalEffects.Any(p =>
                        !currentEffects.TryGetValue(p.Key, out var actual) ||
                        WoundAcceptedTurnFingerprintWriter.CanonicalJson(p.Value) != WoundAcceptedTurnFingerprintWriter.CanonicalJson(actual)))
                    throw new InvalidDataException("An ordinary input changed a protected original wound effect before execution.");
            }
        }
    }
}
