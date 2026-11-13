using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

public partial class CanonicalStateNormalizer
{
    /// <summary>
    /// Checks the live owner/source at each phase. Immutable witness parsing belongs
    /// to opening validation, not to these repeated current-authority fences.
    /// </summary>
    private void RequireOrdinaryNpcSkillCommonPlan(AcceptedMechanicsPlan plan)
    {
        var input = _npcProgressionSource?.SkillInput;
        var source = _npcProgressionSource?.Context;
        if (_writeLease is null || input is null || source is null || source.Realm != "mortal_world" ||
            plan.EffectPlan?.NpcSkillInput is not { } admitted ||
            !string.Equals(input.Fingerprint, admitted.Fingerprint, StringComparison.Ordinal))
            throw new InvalidDataException("Ordinary NPC skills require their exact common effect input and current Mortal source.");

        if (!AcceptedMechanicsPlanAuthority.TryPeekValidated(_fs, _writeLease, out var binding, out var current) ||
            !current.Success || !ReferenceEquals(current.Plan, plan) ||
            binding.SessionId != source.SessionId || binding.RequestId != source.RequestId ||
            binding.SnapshotToken != source.SnapshotToken || binding.Turn != source.TurnNumber ||
            binding.Realm != source.Realm)
            throw new InvalidDataException("Ordinary NPC skill publication lost its exact current common owner or source binding.");
    }

    /// <summary>
    /// Validates immutable shape and raw witnesses once before normalization writes.
    /// Later phases require this same immutable plan through its existing live owner;
    /// no cached permission or independently retained validation result is introduced.
    /// </summary>
    private void ValidateOrdinaryNpcSkillCommonOpening(AcceptedMechanicsPlan plan)
    {
        RequireOrdinaryNpcSkillCommonPlan(plan);
        if (plan.AwaitsPendingResolution || plan.TreatmentResourcePublicationAuthority is not null ||
            plan.JointRepairCommonAuthority is not null || plan.SpiritualHealingContinuation is not null ||
            plan.DirectWoundPublicationAuthority is not null || plan.HasWoundStageBundle ||
            plan.LiveWoundProofFingerprint is not null)
            throw new InvalidDataException("Ordinary NPC skills require an exact completed ordinary common effect plan; specialized or pending publication is unsupported.");

        var pending = plan.PendingAfterImages;
        var transitions = plan.OwnerTransitions;
        foreach (var path in new[] { NpcCoreChangesContract.NpcCorePath, MortalNpcAcceptedSkillInput.SidecarPath })
        {
            if (plan.HasWholeCarrierAfterImage(path) || pending.ContainsKey(path) ||
                plan.ConsumedPaths.Contains(path, StringComparer.Ordinal) ||
                transitions.Any(transition => transition.Path == path))
                throw new InvalidDataException("Ordinary NPC skills cannot overlap a common NPC carrier after-image or transition before semantic composition is supported.");
        }
        var input = _npcProgressionSource!.SkillInput!;
        var before = plan.BeforeImages;
        RequireRawWitness(NpcCoreChangesContract.NpcCorePath, input.RawCore);
        RequireRawWitness(MortalNpcAcceptedSkillInput.SidecarPath, input.RawSidecar);

        void RequireRawWitness(string path, JsonObject raw)
        {
            if (!before.TryGetValue(path, out var image) || !image.Existed || image.Bytes is not { } bytes ||
                !JsonNode.DeepEquals(JsonNode.Parse(DecodePendingSnapshotText(bytes)), raw))
                throw new InvalidDataException("Ordinary NPC skill common before-image differs from its admitted raw input: " + path);
        }
    }

    /// <summary>
    /// Reuses the existing captured item snapshot for bounded existing-item transfers.
    /// The immutable projection proof and exact physical raw inputs remain binding;
    /// creation, specialized publication and location rewrites remain unsupported.
    /// </summary>
    private async Task RequireOrdinaryNpcSkillCommonHasNoItemOrLocationWorkAsync(
        MortalItemAcceptedTurnNormalizationMode itemMode)
    {
        if (itemMode is not MortalItemAcceptedTurnNormalizationMode.Validated validated ||
            validated.Snapshot.HasFinalPublicationBaseline ||
            validated.Snapshot.CloneRouteCatalog().ByCreationRef.Count != 0)
            throw new InvalidDataException("Ordinary NPC skill common publication does not yet support item creation or specialized item publication.");

        var current = validated.Snapshot.CloneCurrentProjectionRoots();
        var original = validated.Snapshot.CloneBackupProjectionRoots();
        var admitted = _npcProgressionSource!.SkillInput!;
        var hasTransfers = validated.Snapshot.Transfers.Count != 0;
        if (hasTransfers)
        {
            if (admitted.ItemProjectionProof != validated.Snapshot.ProjectionProofFingerprint ||
                admitted.CloneItemPhase() is not { IsValid: true } phase ||
                admitted.ItemPhaseFingerprint != phase.Fingerprint ||
                !JsonNode.DeepEquals(admitted.ItemPhaseCore, phase.ItemPhaseAfterImages[NpcCoreChangesContract.NpcCorePath]))
                throw new InvalidDataException("Ordinary NPC skill item projection differs from the admitted immutable phase.");
            foreach (var (path, root) in current)
                if (!JsonNode.DeepEquals(await ReadNodeAsync(path), root))
                    throw new InvalidDataException("Ordinary NPC skill item projection input changed: " + path);
        }
        else if (admitted.ItemProjectionProof is not null)
            throw new InvalidDataException("Ordinary NPC skill item transfer authority disappeared.");
        foreach (var path in MortalItemCanonicalProjectionPlanner.ProjectionRootPaths)
        {
            if (path == NpcCoreChangesContract.NpcCorePath) continue;
            if (hasTransfers && (path is InventoryEquipmentService.ItemsPath or
                    MortalItemAcceptedTransferCatalog.PlayerRemovalPath or MortalItemAcceptedTransferCatalog.NpcCommandsPath))
                continue;
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
        if (!hasTransfers && current[MortalItemAcceptedTransferCatalog.NpcCommandsPath] is JsonObject commands &&
            commands.Any(pair => pair.Key.StartsWith("NPC", StringComparison.Ordinal) &&
                (pair.Value is not JsonArray rows || rows.Count != 0)))
            throw new InvalidDataException("Ordinary NPC skill common publication does not yet support NPC item commands.");
    }
}
