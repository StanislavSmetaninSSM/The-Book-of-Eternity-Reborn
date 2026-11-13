using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

public partial class CanonicalStateNormalizer
{
    /// <summary>
    /// Admits only the existing completed ordinary common owner. The immutable skill
    /// input is DATA, never a replacement for this current lease and actual handoff.
    /// </summary>
    private void RequireOrdinaryNpcSkillCommonPlan(AcceptedMechanicsPlan plan)
    {
        var input = _npcProgressionSource?.SkillInput;
        var source = _npcProgressionSource?.Context;
        if (_writeLease is null || input is null || source is null || source.Realm != "mortal_world" ||
            plan.EffectPlan?.NpcSkillInput is not { } admitted ||
            !string.Equals(input.Fingerprint, admitted.Fingerprint, StringComparison.Ordinal) ||
            plan.AwaitsPendingResolution || plan.TreatmentResourcePublicationAuthority is not null ||
            plan.JointRepairCommonAuthority is not null || plan.SpiritualHealingContinuation is not null ||
            plan.DirectWoundPublicationAuthority is not null || plan.WoundStageBundle is not null ||
            plan.LiveWoundProofFingerprint is not null)
            throw new InvalidDataException("Ordinary NPC skills require an exact completed ordinary common effect plan; specialized or pending publication is unsupported.");

        if (!AcceptedMechanicsPlanAuthority.TryPeekValidated(_fs, _writeLease, out var binding, out var current) ||
            !current.Success || !ReferenceEquals(current.Plan, plan) ||
            binding.SessionId != source.SessionId || binding.RequestId != source.RequestId ||
            binding.SnapshotToken != source.SnapshotToken || binding.Turn != source.TurnNumber ||
            binding.Realm != source.Realm)
            throw new InvalidDataException("Ordinary NPC skill publication lost its exact current common owner or source binding.");

        foreach (var path in new[] { NpcCoreChangesContract.NpcCorePath, MortalNpcAcceptedSkillInput.SidecarPath })
        {
            if (plan.HasWholeCarrierAfterImage(path) || plan.PendingAfterImages.ContainsKey(path) ||
                plan.ConsumedPaths.Contains(path, StringComparer.Ordinal) ||
                plan.OwnerTransitions.Any(transition => transition.Path == path))
                throw new InvalidDataException("Ordinary NPC skills cannot overlap a common NPC carrier after-image or transition before semantic composition is supported.");
        }
        var before = plan.BeforeImages;
        RequireRawWitness(NpcCoreChangesContract.NpcCorePath, input.RawCore);
        RequireRawWitness(MortalNpcAcceptedSkillInput.SidecarPath, input.RawSidecar);

        void RequireRawWitness(string path, JsonObject raw)
        {
            if (!before.TryGetValue(path, out var image) || !image.Existed || image.Bytes is not { } bytes ||
                !JsonNode.DeepEquals(JsonNode.Parse(DecodeAcceptedMechanicsUtf8(bytes)), raw))
                throw new InvalidDataException("Ordinary NPC skill common before-image differs from its admitted raw input: " + path);
        }
    }

    /// <summary>
    /// This first common contour has no item/location semantics. Reuse the already
    /// captured item snapshot rather than constructing another projection or cache.
    /// The unchanged raw core/sidecar fence is checked again immediately before writes.
    /// </summary>
    private async Task RequireOrdinaryNpcSkillCommonHasNoItemOrLocationWorkAsync(
        MortalItemAcceptedTurnNormalizationMode itemMode)
    {
        if (itemMode is not MortalItemAcceptedTurnNormalizationMode.Validated validated ||
            validated.Snapshot.HasFinalPublicationBaseline || validated.Snapshot.Transfers.Count != 0 ||
            validated.Snapshot.CloneRouteCatalog().ByCreationRef.Count != 0)
            throw new InvalidDataException("Ordinary NPC skill common publication does not yet support item materialization or transfers.");

        var current = validated.Snapshot.CloneCurrentProjectionRoots();
        var original = validated.Snapshot.CloneBackupProjectionRoots();
        foreach (var path in MortalItemCanonicalProjectionPlanner.ProjectionRootPaths)
        {
            if (path == NpcCoreChangesContract.NpcCorePath) continue;
            if (!JsonNode.DeepEquals(current[path], original[path]))
                throw new InvalidDataException("Ordinary NPC skill common publication does not yet support changed item/location projection inputs: " + path);
        }
        var input = _npcProgressionSource!.SkillInput!;
        if (!JsonNode.DeepEquals(current[NpcCoreChangesContract.NpcCorePath], input.RawCore) ||
            !JsonNode.DeepEquals(MortalItemPublicationBaselinePlanner.ComposeNpcItemPhaseComparisonBaseline(
                input.RawCore, input.OriginalCore), input.OriginalCore))
            throw new InvalidDataException("Ordinary NPC skill common publication does not yet support NPC item changes.");

        if ((await ReadNodeAsync(MortalLocationMaterializationContract.WorldMapPath) as JsonObject)?["worldMapUpdates"] is not null ||
            (await ReadNodeAsync(MortalLocationMaterializationContract.CurrentLocationPath) as JsonObject)?["currentLocationData"] is not null)
            throw new InvalidDataException("Ordinary NPC skill common publication does not yet support location commands or governed rewrites.");
        if (current[MortalItemAcceptedTransferCatalog.NpcCommandsPath] is JsonObject commands &&
            commands.Any(pair => pair.Key.StartsWith("NPC", StringComparison.Ordinal) &&
                (pair.Value is not JsonArray rows || rows.Count != 0)))
            throw new InvalidDataException("Ordinary NPC skill common publication does not yet support NPC item commands.");
    }
}
