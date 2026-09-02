using System.Globalization;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal sealed partial class MortalWoundTreatmentItemPublicationAuthority
{
    private string ComputeIdentitySeed(string semanticFingerprint) =>
        WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
        {
            "book_of_eternity.mortal_wound_treatment.resource_publication_identity",
            "1",
            AcceptedStateAuthority.AcceptedStateFingerprint,
            RequestAuthority.RequestFingerprint,
            ResolutionAuthority.ResultFingerprint,
            Finalization.FinalizationFingerprint,
            semanticFingerprint
        });

    private bool TryRecompose(
        out IReadOnlyDictionary<string, JsonObject> skillAfterImages,
        out IReadOnlyDictionary<string, JsonNode?> finalRoots,
        out IReadOnlyDictionary<string, JsonObject> publicationAfterImages)
    {
        skillAfterImages = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        finalRoots = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
        publicationAfterImages = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        var itemEnvelope = _snapshot.CloneItemCommandEnvelope();
        var itemPhase = _snapshot.CloneItemPhase();
        var baseline = _snapshot.CloneFinalBaseline();
        if (!_snapshot.HasFinalPublicationBaseline ||
            !_snapshot.RecomputesFinalPublicationBaseline() ||
            !_snapshot.MatchesFinalPublicationBaseline(_snapshot) ||
            itemEnvelope is null ||
            itemPhase is null ||
            baseline is null ||
            !itemPhase.IsValid ||
            baseline.Issues.Count != 0 ||
            !_consumption.IsValid ||
            !BaselinesEqual(baseline, _baseline) ||
            baseline.FinalCarrierRoots.GetValueOrDefault(
                NpcCoreChangesContract.NpcCorePath) is not JsonObject npcBaseline)
        {
            return false;
        }

        var skillIssues = WoundAcceptedTurnPlanner
            .ComposeTreatmentSkillProjectionOnFinalItemBaseline(
                _continuationAuthority,
                PublicationReservationAuthority,
                npcBaseline,
                out var recomposedSkillAfterImages,
                out var recomposedSkillAuthority,
                out var recomposedSkillFingerprint);
        if (skillIssues.Count != 0 ||
            !ReferenceEquals(recomposedSkillAuthority, SkillProjectionAuthority) ||
            !string.Equals(
                recomposedSkillFingerprint,
                SkillProjectionFingerprint,
                StringComparison.Ordinal) ||
            !ObjectMapsEqual(recomposedSkillAfterImages, _skillAfterImages))
        {
            return false;
        }

        var skillRoots = ComposeSkillRoots(
            baseline.FinalCarrierRoots,
            recomposedSkillAfterImages);
        var expectedCarrierInput = CreateConsumptionCarrierRoots(skillRoots);
        var expectedIdentity = MortalItemIdentityState.Parse(
            baseline.IdentityIndexAfterImage.DeepClone());
        if (!CarrierInputsEqual(
                expectedCarrierInput,
                _consumptionInput.CarrierRoots) ||
            expectedIdentity.Issues.Count != 0 ||
            !JsonNode.DeepEquals(
                expectedIdentity.Root,
                _consumptionInput.IdentityState.Root) ||
            !string.Equals(
                baseline.Fingerprint,
                _consumptionInput.BaselineFingerprint,
                StringComparison.Ordinal) ||
            _consumptionInput.Turn != ResolutionAuthority.Coordinates.Turn ||
            !ConsumptionCommandsMatchFinalization() ||
            !CapacityAuthorityInputMatches(baseline.Fingerprint))
        {
            return false;
        }

        var recomposedConsumption = MortalItemConsumptionPlanner.Plan(
            Clone(_consumptionInput));
        if (!ResultsEqual(recomposedConsumption, _consumption) ||
            recomposedConsumption.IdentityIndexAfterImage is null ||
            !CapacityTransitionsEqual(
                AdaptCapacityTransitions(recomposedConsumption.CapacityTransitions),
                _capacityTransitions))
        {
            return false;
        }

        var recomposedFinalRoots = ComposeFinalRoots(
            skillRoots,
            recomposedConsumption);
        var recomposedPublicationAfterImages = ComposePublicationAfterImages(
            skillRoots,
            baseline,
            recomposedSkillAfterImages,
            recomposedConsumption);
        if (!RootMapsEqual(recomposedFinalRoots, _finalRoots) ||
            !ObjectMapsEqual(
                recomposedPublicationAfterImages,
                _publicationAfterImages) ||
            !MortalWoundTreatmentItemOwnerSuccessor.IsExactSuccessor(
                _baselineOwnerAuthority,
                _terminalOwners,
                FinalOwnerAuthority) ||
            !TryRecomposeEffectSourceSuccessor(out _, out _))
        {
            return false;
        }

        skillAfterImages = CloneObjects(recomposedSkillAfterImages);
        finalRoots = MortalItemAcceptedTurnNormalizationSnapshot.CloneRoots(
            recomposedFinalRoots);
        publicationAfterImages = CloneObjects(recomposedPublicationAfterImages);
        return true;
    }

    private static ResourceCapacityIntent[] AdaptCapacityTransitions(
        IReadOnlyList<ResourceCapacityIntent> transitions) =>
        transitions.Select(static transition => transition.ResolvedCapacity is null
                ? transition
                : transition with
                {
                    PolicyFingerprint = transition.ResolvedCapacity.Binding
                        .AuthorityFingerprint
                })
            .ToArray();

    private bool ConsumptionCommandsMatchFinalization()
    {
        var itemRows = Finalization.Consumptions
            .Select((consumption, index) => (Consumption: consumption, Index: index))
            .Where(static row => string.Equals(
                row.Consumption.Kind,
                "item_quantity",
                StringComparison.Ordinal))
            .ToArray();
        if (itemRows.Length != _consumptionInput.Commands.Count)
            return false;
        var claimGroups = RequestAuthority.ResourceAuthority.Claims
            .GroupBy(static claim => claim.ClaimFingerprint, StringComparer.Ordinal)
            .ToArray();
        if (claimGroups.Any(static group => group.Count() != 1))
            return false;
        var claims = claimGroups.ToDictionary(
            static group => group.Key,
            static group => group.Single(),
            StringComparer.Ordinal);
        for (var index = 0; index < itemRows.Length; index++)
        {
            var row = itemRows[index];
            var consumption = row.Consumption;
            if (!claims.TryGetValue(consumption.ClaimFingerprint, out var claim) ||
                !ConsumptionMatchesClaim(consumption, claim))
            {
                return false;
            }
            var fingerprint = WoundAcceptedTurnFingerprintWriter.Compute(
                new string?[]
                {
                    "book_of_eternity.mortal_wound_treatment.item_consumption_command",
                    "1",
                    AcceptedStateAuthority.AcceptedStateFingerprint,
                    RequestAuthority.RequestFingerprint,
                    ResolutionAuthority.ResultFingerprint,
                    Finalization.FinalizationFingerprint,
                    row.Index.ToString("D4", CultureInfo.InvariantCulture),
                    consumption.AuthorityRef,
                    consumption.Quantity.ToString(CultureInfo.InvariantCulture),
                    consumption.ClaimFingerprint,
                    consumption.IntentFingerprint
                });
            var command = _consumptionInput.Commands[index];
            if (command.FinalizationOrdinal != row.Index + 1 ||
                !string.Equals(command.ItemId, consumption.AuthorityRef,
                    StringComparison.Ordinal) ||
                command.Quantity != consumption.Quantity ||
                !string.Equals(command.ClaimFingerprint,
                    consumption.ClaimFingerprint, StringComparison.Ordinal) ||
                !string.Equals(command.TransitionId,
                    "mitrn_" + fingerprint["sha256:".Length..],
                    StringComparison.Ordinal) ||
                !string.Equals(command.AuthorityKind,
                    "mortal_wound_treatment", StringComparison.Ordinal) ||
                !string.Equals(command.AuthorityId,
                    "treatment_item_" +
                    fingerprint["sha256:".Length..("sha256:".Length + 24)],
                    StringComparison.Ordinal))
            {
                return false;
            }
        }
        return true;
    }

    private static bool ConsumptionMatchesClaim(
        MortalWoundTreatmentResourceConsumptionIntent consumption,
        MortalWoundTreatmentResourceClaim claim) =>
        string.Equals(consumption.Scope, claim.Scope, StringComparison.Ordinal) &&
        consumption.RequirementIndex == claim.RequirementIndex &&
        string.Equals(consumption.Kind, claim.Kind, StringComparison.Ordinal) &&
        string.Equals(consumption.AuthorityRef, claim.AuthorityRef,
            StringComparison.Ordinal) &&
        string.Equals(consumption.Realm, claim.Realm, StringComparison.Ordinal) &&
        string.Equals(consumption.OwnerKind, claim.OwnerKind,
            StringComparison.Ordinal) &&
        string.Equals(consumption.OwnerId, claim.OwnerId, StringComparison.Ordinal) &&
        consumption.Quantity == claim.Quantity &&
        string.Equals(consumption.ClaimFingerprint, claim.ClaimFingerprint,
            StringComparison.Ordinal);

    private bool CapacityAuthorityInputMatches(string baselineFingerprint)
    {
        var sourceFingerprint = WoundAcceptedTurnFingerprintWriter.Compute(
            new string?[]
            {
                "book_of_eternity.mortal_wound_treatment.item_capacity_source",
                "1",
                _identitySeed,
                baselineFingerprint
            });
        var policyFingerprint = WoundAcceptedTurnFingerprintWriter.Compute(
            new string?[]
            {
                "book_of_eternity.mortal_wound_treatment.item_capacity_policy",
                "1",
                _identitySeed,
                baselineFingerprint
            });
        return _consumptionInput.CapacitySourceEvidence == new ResourceSourceEvidence(
                   "mortal_wound_treatment",
                   "treatment_item_capacity_" +
                   sourceFingerprint["sha256:".Length..("sha256:".Length + 20)],
                   sourceFingerprint) &&
               string.Equals(
                   _consumptionInput.CapacityPolicyFingerprint,
                   policyFingerprint,
                   StringComparison.Ordinal);
    }

    private bool TryRecomposeEffectSourceSuccessor(
        out string beforeFingerprint,
        out string normalizedFingerprint)
    {
        beforeFingerprint = string.Empty;
        normalizedFingerprint = string.Empty;
        var before = EffectAcceptedTurnInputComposer.BuildCanonicalSourceAuthority(
            MortalItemAcceptedTurnNormalizationSnapshot.CloneRoots(
                _effectSourceBeforeRoots),
            _preparedWoundPlan.ClonePreservingCacheAuthority());
        if (before.Issues.Count != 0 ||
            !string.Equals(
                before.CanonicalFingerprint,
                _effectSourceBeforeFingerprint,
                StringComparison.Ordinal))
        {
            return false;
        }

        var normalizedRoots = MortalItemAcceptedTurnNormalizationSnapshot.CloneRoots(
            _effectSourceBeforeRoots);
        foreach (var path in EffectAcceptedTurnInputComposer.SourceAuthorityPaths)
        {
            if (_baseline.FinalCarrierRoots.TryGetValue(path, out var baselineRoot))
                normalizedRoots[path] = baselineRoot?.DeepClone();
        }
        var normalized = EffectAcceptedTurnInputComposer.BuildCanonicalSourceAuthority(
            normalizedRoots,
            _preparedWoundPlan.ClonePreservingCacheAuthority());
        if (normalized.Issues.Count != 0 ||
            !string.Equals(
                normalized.CanonicalFingerprint,
                _normalizedEffectSourceFingerprint,
                StringComparison.Ordinal))
        {
            return false;
        }

        beforeFingerprint = before.CanonicalFingerprint;
        normalizedFingerprint = normalized.CanonicalFingerprint;
        return true;
    }

    private static Dictionary<string, JsonNode?> ComposeSkillRoots(
        IReadOnlyDictionary<string, JsonNode?> baselineRoots,
        IReadOnlyDictionary<string, JsonObject> skillAfterImages)
    {
        var roots = MortalItemAcceptedTurnNormalizationSnapshot.CloneRoots(
            baselineRoots);
        if (skillAfterImages.TryGetValue(
                NpcCoreChangesContract.NpcCorePath,
                out var npcSkillAfterImage))
        {
            roots[NpcCoreChangesContract.NpcCorePath] =
                npcSkillAfterImage.DeepClone();
        }
        return roots;
    }

    private static Dictionary<string, JsonNode?> ComposeFinalRoots(
        IReadOnlyDictionary<string, JsonNode?> skillRoots,
        MortalItemConsumptionPlanningResult consumption)
    {
        var roots = MortalItemAcceptedTurnNormalizationSnapshot.CloneRoots(skillRoots);
        foreach (var pair in consumption.CarrierAfterImages)
            roots[pair.Key] = pair.Value.DeepClone();
        roots[MortalItemIdentityState.StatePath] =
            consumption.IdentityIndexAfterImage!.DeepClone();
        return roots;
    }

    private static Dictionary<string, JsonObject> ComposePublicationAfterImages(
        IReadOnlyDictionary<string, JsonNode?> skillRoots,
        MortalItemPublicationBaselineResult baseline,
        IReadOnlyDictionary<string, JsonObject> skillAfterImages,
        MortalItemConsumptionPlanningResult consumption)
    {
        var afterImages = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        if (skillAfterImages.TryGetValue(
                NpcCoreChangesContract.NpcCorePath,
                out var composedNpc))
        {
            afterImages[NpcCoreChangesContract.NpcCorePath] =
                composedNpc.DeepClone().AsObject();
        }
        foreach (var pair in consumption.CarrierAfterImages)
        {
            if (!JsonNode.DeepEquals(skillRoots.GetValueOrDefault(pair.Key), pair.Value))
                afterImages[pair.Key] = pair.Value.DeepClone().AsObject();
        }
        if (!JsonNode.DeepEquals(
                baseline.IdentityIndexAfterImage,
                consumption.IdentityIndexAfterImage))
        {
            afterImages[MortalItemIdentityState.StatePath] =
                consumption.IdentityIndexAfterImage!.DeepClone().AsObject();
        }
        return afterImages;
    }

    private static MortalItemCarrierCatalogInput CreateConsumptionCarrierRoots(
        IReadOnlyDictionary<string, JsonNode?> roots)
    {
        var companions = MortalItemCanonicalProjectionPlanner.ProjectionRootPaths
            .Skip(8)
            .Where(path => roots.GetValueOrDefault(path) is JsonObject)
            .ToDictionary(
                static path => path,
                path => roots[path]!.DeepClone().AsObject(),
                StringComparer.Ordinal);
        return new MortalItemCarrierCatalogInput(
            roots.GetValueOrDefault(InventoryEquipmentService.ItemsPath) as JsonObject,
            roots.GetValueOrDefault(NpcCoreChangesContract.NpcCorePath) as JsonObject,
            roots.GetValueOrDefault(
                MortalItemAcceptedTransferCatalog.NpcCommandsPath) as JsonObject,
            roots.GetValueOrDefault(
                StorageTransportMoveService.CurrentLocationPath) as JsonObject,
            MortalItemProjectionRootParser.ToCarrierCatalogObject(
                roots.GetValueOrDefault(StorageTransportMoveService.VehiclesPath),
                StorageTransportMoveService.VehiclesPath),
            companions,
            roots.GetValueOrDefault(
                MortalLocationStorageContentsState.StatePath) as JsonObject);
    }

}
