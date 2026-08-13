using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal sealed record MortalLocationAcceptedTurnInput(
    JsonObject PreTurnWorldMap,
    JsonObject? PreTurnCurrentLocation,
    JsonObject PreTurnIdentityIndex,
    JsonObject? RawCurrentLocationData,
    JsonObject? RawWorldMapUpdates,
    int Turn,
    JsonObject? BootstrapScaffold = null,
    MortalLocationCompanionAuthority? CompanionAuthority = null,
    JsonObject? RawNpcCore = null,
    JsonObject? RawFactionCore = null,
    JsonObject? PreTurnStorageContents = null,
    JsonObject? RawCurrentItemCarrier = null);

internal sealed record MortalLocationStorageCoordinate(
    string LocationId,
    string StorageId,
    string? InitialLocationId = null);

internal sealed record MortalLocationGovernedRewrite(
    string CarrierPath,
    string EntryPath,
    string OwnerField,
    string OwnerId,
    string TemporaryFieldPath,
    string PermanentFieldPath,
    string InitialId,
    string PermanentId);

internal sealed record MortalLocationRepairContext(
    string CarrierPath,
    string EntityKind,
    string? InitialId,
    string? MaterializationId,
    IReadOnlyList<string> RepairableFields,
    string? ExistingId = null,
    int? ExpectedSourceTurn = null,
    string? ExpectedSourceAuthorityKind = null,
    string? ExpectedSourceAuthorityId = null);

internal sealed class MortalLocationAcceptedTurnPlan
{
    internal MortalLocationAcceptedTurnPlan(
        JsonObject finalWorldMap,
        JsonObject? finalCurrentLocation,
        JsonObject finalIdentityIndex,
        JsonObject finalStorageContents,
        IReadOnlyDictionary<string, string> locationIdsByInitialId,
        IReadOnlyDictionary<string, string> linkIdsByInitialId,
        IReadOnlyList<MortalLocationStorageCoordinate> acceptedStorageCoordinates,
        IReadOnlyList<MortalLocationGovernedRewrite> governedRewrites,
        IReadOnlyList<string> touchedPaths,
        IReadOnlyList<MortalLocationRepairContext> repairContexts,
        JsonObject? finalBootstrapScaffold = null)
    {
        FinalWorldMap = finalWorldMap ?? throw new ArgumentNullException(nameof(finalWorldMap));
        FinalCurrentLocation = finalCurrentLocation;
        FinalIdentityIndex = finalIdentityIndex ?? throw new ArgumentNullException(nameof(finalIdentityIndex));
        FinalStorageContents = finalStorageContents ?? throw new ArgumentNullException(nameof(finalStorageContents));
        LocationIdsByInitialId = locationIdsByInitialId ?? throw new ArgumentNullException(nameof(locationIdsByInitialId));
        LinkIdsByInitialId = linkIdsByInitialId ?? throw new ArgumentNullException(nameof(linkIdsByInitialId));
        AcceptedStorageCoordinates = acceptedStorageCoordinates ?? throw new ArgumentNullException(nameof(acceptedStorageCoordinates));
        GovernedRewrites = governedRewrites ?? throw new ArgumentNullException(nameof(governedRewrites));
        TouchedPaths = touchedPaths ?? throw new ArgumentNullException(nameof(touchedPaths));
        RepairContexts = repairContexts ?? throw new ArgumentNullException(nameof(repairContexts));
        FinalBootstrapScaffold = finalBootstrapScaffold;
    }

    internal JsonObject FinalWorldMap { get; }

    internal JsonObject? FinalCurrentLocation { get; }

    internal JsonObject FinalIdentityIndex { get; }

    internal JsonObject FinalStorageContents { get; }

    internal IReadOnlyDictionary<string, string> LocationIdsByInitialId { get; }

    internal IReadOnlyDictionary<string, string> LinkIdsByInitialId { get; }

    internal IReadOnlyList<MortalLocationStorageCoordinate> AcceptedStorageCoordinates { get; }

    internal IReadOnlyList<MortalLocationGovernedRewrite> GovernedRewrites { get; }

    internal IReadOnlyList<string> TouchedPaths { get; }

    internal IReadOnlyList<MortalLocationRepairContext> RepairContexts { get; }

    internal JsonObject? FinalBootstrapScaffold { get; }
}

internal sealed record MortalLocationAcceptedTurnPlanningResult(
    MortalLocationAcceptedTurnPlan? Plan,
    IReadOnlyList<ValidationIssue> Issues)
{
    internal bool Success => Plan != null && Issues.Count == 0;
}
