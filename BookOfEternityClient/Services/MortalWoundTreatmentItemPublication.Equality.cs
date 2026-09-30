using System.Text.Json.Nodes;
using System.Globalization;

namespace BookOfEternityClient.Services;

internal sealed partial class MortalWoundTreatmentItemPublicationAuthority
{
    private static bool ItemEnvelopesEqual(
        MortalTreatmentItemCommandEnvelope left,
        MortalTreatmentItemCommandEnvelope right) =>
        string.Equals(left.Fingerprint, right.Fingerprint, StringComparison.Ordinal) &&
        JsonNode.DeepEquals(left.UpdateInventory, right.UpdateInventory) &&
        JsonNode.DeepEquals(left.MoveInventoryItems, right.MoveInventoryItems) &&
        JsonNode.DeepEquals(left.RemoveInventoryItems, right.RemoveInventoryItems) &&
        JsonNode.DeepEquals(left.NPCInventoryAdds, right.NPCInventoryAdds) &&
        JsonNode.DeepEquals(left.NPCInventoryUpdates, right.NPCInventoryUpdates) &&
        JsonNode.DeepEquals(left.NPCInventoryRemovals, right.NPCInventoryRemovals) &&
        JsonNode.DeepEquals(left.NPCEquipmentChanges, right.NPCEquipmentChanges);

    private static bool ItemPhasesEqual(
        MortalItemCanonicalProjectionResult left,
        MortalItemCanonicalProjectionResult right) =>
        left.IsValid == right.IsValid &&
        string.Equals(left.Fingerprint, right.Fingerprint, StringComparison.Ordinal) &&
        RootMapsEqual(left.ItemPhaseAfterImages, right.ItemPhaseAfterImages) &&
        JsonNode.DeepEquals(
            left.IdentityIndexAfterImage,
            right.IdentityIndexAfterImage) &&
        IssuesEqual(left.Issues, right.Issues);

    private static bool BaselinesEqual(
        MortalItemPublicationBaselineResult left,
        MortalItemPublicationBaselineResult right) =>
        string.Equals(left.Fingerprint, right.Fingerprint, StringComparison.Ordinal) &&
        RootMapsEqual(left.FinalCarrierRoots, right.FinalCarrierRoots) &&
        JsonNode.DeepEquals(
            left.IdentityIndexAfterImage,
            right.IdentityIndexAfterImage) &&
        left.AppliedTransformIds.SequenceEqual(
            right.AppliedTransformIds,
            StringComparer.Ordinal) &&
        IssuesEqual(left.Issues, right.Issues);

    private static bool RootMapsEqual(
        IReadOnlyDictionary<string, JsonNode?> left,
        IReadOnlyDictionary<string, JsonNode?> right) =>
        left.Count == right.Count &&
        left.All(pair =>
            right.TryGetValue(pair.Key, out var root) &&
            JsonNode.DeepEquals(pair.Value, root));

    private static bool ObjectMapsEqual(
        IReadOnlyDictionary<string, JsonObject> left,
        IReadOnlyDictionary<string, JsonObject> right) =>
        left.Count == right.Count &&
        left.All(pair =>
            right.TryGetValue(pair.Key, out var root) &&
            JsonNode.DeepEquals(pair.Value, root));

    private static Dictionary<string, JsonObject> CloneObjects(
        IReadOnlyDictionary<string, JsonObject> value) =>
        value.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value.DeepClone().AsObject(),
            StringComparer.Ordinal);

    private static bool CarrierInputsEqual(
        MortalItemCarrierCatalogInput left,
        MortalItemCarrierCatalogInput right) =>
        JsonNode.DeepEquals(left.PlayerInventory, right.PlayerInventory) &&
        JsonNode.DeepEquals(left.NpcCore, right.NpcCore) &&
        JsonNode.DeepEquals(
            left.NpcInventoryCommands,
            right.NpcInventoryCommands) &&
        JsonNode.DeepEquals(left.CurrentLocation, right.CurrentLocation) &&
        JsonNode.DeepEquals(left.Vehicles, right.Vehicles) &&
        JsonNode.DeepEquals(
            left.OffscreenLocationStorageContents,
            right.OffscreenLocationStorageContents) &&
        ObjectMapsEqual(left.CompanionRoots, right.CompanionRoots);

    private static bool IssuesEqual(
        IReadOnlyList<ValidationIssue> left,
        IReadOnlyList<ValidationIssue> right) =>
        left.Count == right.Count &&
        left.Zip(right).All(pair => pair.First == pair.Second);

    private string ComputeFingerprint()
    {
        var itemEnvelope = _snapshot.CloneItemCommandEnvelope();
        var itemPhase = _snapshot.CloneItemPhase();
        var baseline = _snapshot.CloneFinalBaseline();
        var fields = new List<string?>
        {
            "book_of_eternity.mortal_wound_treatment.item_publication_authority",
            "1",
            AcceptedStateAuthority.AcceptedStateFingerprint,
            RequestAuthority.RequestFingerprint,
            ResolutionAuthority.ResultFingerprint,
            Finalization.FinalizationFingerprint,
            ContinuationFingerprint,
            _identitySeed,
            _snapshot.ProofFingerprint,
            itemEnvelope?.Fingerprint,
            itemPhase?.Fingerprint,
            baseline?.Fingerprint,
            SkillProjectionFingerprint,
            _consumption.Fingerprint,
            _baselineOwnerAuthority.Fingerprint,
            _effectSourceBeforeFingerprint,
            _normalizedEffectSourceFingerprint,
            FinalOwnerAuthority.Fingerprint
        };
        foreach (var pair in _finalRoots.OrderBy(static pair => pair.Key,
                     StringComparer.Ordinal))
        {
            fields.Add(pair.Key);
            fields.Add(pair.Value is null ? "missing" :
                WoundAcceptedTurnFingerprintWriter.CanonicalJson(pair.Value));
        }
        foreach (var pair in _publicationAfterImages.OrderBy(static pair => pair.Key,
                     StringComparer.Ordinal))
        {
            fields.Add("publication");
            fields.Add(pair.Key);
            fields.Add(WoundAcceptedTurnFingerprintWriter.CanonicalJson(pair.Value));
        }
        foreach (var capacity in _capacityTransitions)
        {
            fields.Add(capacity.EventRef);
            fields.Add(capacity.OriginKind);
            fields.Add(capacity.OriginId);
            fields.Add(Describe(capacity.Coordinate));
            fields.Add(capacity.Operation.ToString());
            fields.Add(capacity.ResolvedCapacity?.Maximum.ToString(
                CultureInfo.InvariantCulture));
            fields.Add(capacity.PolicyFingerprint);
        }
        foreach (var owner in _terminalOwners)
        {
            fields.Add(owner.Realm);
            fields.Add(owner.OwnerKind.ToString());
            fields.Add(owner.ResourceOwnerId);
        }
        return WoundAcceptedTurnFingerprintWriter.Compute(fields);
    }

    private static MortalItemConsumptionPlanningInput Clone(
        MortalItemConsumptionPlanningInput input) => new(
        input.Turn,
        input.BaselineFingerprint,
        Clone(input.CarrierRoots),
        MortalItemIdentityState.Parse(input.IdentityState.Root.DeepClone()),
        input.Commands.Select(static command => command with { }).ToArray(),
        input.Definitions,
        new ResourceStateLedger(input.ResourceState.Entries),
        input.CapacitySourceEvidence with { },
        input.CapacityPolicyFingerprint);

    private static MortalItemCarrierCatalogInput Clone(
        MortalItemCarrierCatalogInput roots) => new(
        roots.PlayerInventory?.DeepClone().AsObject(),
        roots.NpcCore?.DeepClone().AsObject(),
        roots.NpcInventoryCommands?.DeepClone().AsObject(),
        roots.CurrentLocation?.DeepClone().AsObject(),
        roots.Vehicles?.DeepClone().AsObject(),
        roots.CompanionRoots.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value.DeepClone().AsObject(),
            StringComparer.Ordinal),
        roots.OffscreenLocationStorageContents?.DeepClone().AsObject());

    private static MortalItemCanonicalProjectionResult Clone(
        MortalItemCanonicalProjectionResult value) => new(
        MortalItemAcceptedTurnNormalizationSnapshot.CloneRoots(
            value.ItemPhaseAfterImages),
        value.IdentityIndexAfterImage.DeepClone().AsObject(),
        value.Issues.Select(WoundAcceptedTurnData.CloneIssue).ToArray(),
        value.Fingerprint);

    private static MortalItemPublicationBaselineResult Clone(
        MortalItemPublicationBaselineResult value) => new(
        MortalItemAcceptedTurnNormalizationSnapshot.CloneRoots(
            value.FinalCarrierRoots),
        value.IdentityIndexAfterImage.DeepClone().AsObject(),
        value.AppliedTransformIds.ToArray(),
        value.Issues.Select(WoundAcceptedTurnData.CloneIssue).ToArray(),
        value.Fingerprint);

    private static MortalItemConsumptionPlanningResult Clone(
        MortalItemConsumptionPlanningResult value) => new(
        value.CarrierAfterImages.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value.DeepClone().AsObject(),
            StringComparer.Ordinal),
        value.IdentityIndexAfterImage?.DeepClone().AsObject(),
        value.IdentityTransitions.Select(static value =>
            value.DeepClone().AsObject()).ToArray(),
        value.CapacityTransitions.ToArray(),
        value.TerminalOwners.ToArray(),
        value.Issues.Select(WoundAcceptedTurnData.CloneIssue).ToArray(),
        value.Fingerprint);

    private static bool ResultsEqual(
        MortalItemConsumptionPlanningResult left,
        MortalItemConsumptionPlanningResult right) =>
        left.IsValid == right.IsValid &&
        string.Equals(left.Fingerprint, right.Fingerprint, StringComparison.Ordinal) &&
        JsonNode.DeepEquals(left.IdentityIndexAfterImage,
            right.IdentityIndexAfterImage) &&
        left.CarrierAfterImages.Count == right.CarrierAfterImages.Count &&
        left.CarrierAfterImages.All(pair =>
            right.CarrierAfterImages.TryGetValue(pair.Key, out var root) &&
            JsonNode.DeepEquals(pair.Value, root)) &&
        CapacityTransitionsEqual(
            left.CapacityTransitions,
            right.CapacityTransitions) &&
        left.TerminalOwners.SequenceEqual(right.TerminalOwners);

    private static bool CapacityTransitionsEqual(
        IReadOnlyList<ResourceCapacityIntent> left,
        IReadOnlyList<ResourceCapacityIntent> right) =>
        left.Count == right.Count &&
        left.Zip(right).All(static pair => CapacityTransitionEqual(
            pair.First,
            pair.Second));

    private static bool CapacityTransitionEqual(
        ResourceCapacityIntent left,
        ResourceCapacityIntent right) =>
        string.Equals(left.EventRef, right.EventRef, StringComparison.Ordinal) &&
        string.Equals(left.OriginKind, right.OriginKind, StringComparison.Ordinal) &&
        string.Equals(left.OriginId, right.OriginId, StringComparison.Ordinal) &&
        ResourceCoordinateComparer.Instance.Equals(
            left.Coordinate,
            right.Coordinate) &&
        left.Operation == right.Operation &&
        ResolvedCapacityEqual(left.ResolvedCapacity, right.ResolvedCapacity) &&
        left.CurrentDisposition == right.CurrentDisposition &&
        left.Phase == right.Phase &&
        left.Priority == right.Priority &&
        left.SourceEvidence == right.SourceEvidence &&
        string.Equals(
            left.PolicyFingerprint,
            right.PolicyFingerprint,
            StringComparison.Ordinal) &&
        string.Equals(left.ReceiptId, right.ReceiptId, StringComparison.Ordinal);

    private static bool ResolvedCapacityEqual(
        ResolvedResourceCapacity? left,
        ResolvedResourceCapacity? right) =>
        left is null && right is null ||
        left is not null &&
        right is not null &&
        left.Maximum == right.Maximum &&
        left.Binding == right.Binding &&
        left.Initialization == right.Initialization;

    private static string Describe(ResourceCoordinate coordinate) =>
        $"{coordinate.Realm}/" +
        $"{ResourceDefinitionCatalog.GetOwnerKindToken(coordinate.OwnerKind)}/" +
        $"{coordinate.ResourceOwnerId}/{coordinate.ResourceKey}";

    private static ValidationIssue Issue(string path, string expected, string actual) =>
        new(
            "treatmentPublication.items." + path,
            IssueSeverity.Error,
            "The guaranteed Mortal wound item publication authority did not agree.",
            code: "mortal_wound_treatment_publication_item_authority_mismatch",
            actor: "Client",
            section: "wound_materialization",
            expected: expected,
            actual: actual);
}
