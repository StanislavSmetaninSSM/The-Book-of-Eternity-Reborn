using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal sealed record ShiningReturnCycleResourcePlanningInput(
    int Turn,
    int CurrentIncarnation,
    ResourceDefinitionCatalog Definitions,
    ResourceStateLedger State,
    ResourceHistoryState History,
    AfterlifeResourceOwnerRoots PreTurnOwners,
    JsonObject AcceptedProfiles,
    JsonObject AcceptedShiningAbode,
    JsonObject AcceptedSoulState);

internal sealed class ShiningReturnCycleResourcePlanningResult
{
    private readonly ValidationIssue[] _issues;

    internal ShiningReturnCycleResourcePlanningResult(
        JsonObject? profilesAfterImage,
        JsonObject? shiningAfterImage,
        ResourceStateLedger? stateAfterImage,
        ResourceHistoryState? historyAfterImage,
        bool cycleChanged,
        string previousReturnCycleId,
        string currentReturnCycleId,
        IReadOnlyList<ValidationIssue> issues)
    {
        ProfilesAfterImage = profilesAfterImage?.DeepClone().AsObject();
        ShiningAfterImage = shiningAfterImage?.DeepClone().AsObject();
        StateAfterImage = stateAfterImage;
        HistoryAfterImage = historyAfterImage;
        CycleChanged = cycleChanged;
        PreviousReturnCycleId = previousReturnCycleId;
        CurrentReturnCycleId = currentReturnCycleId;
        _issues = (issues ?? throw new ArgumentNullException(nameof(issues))).ToArray();
    }

    internal JsonObject? ProfilesAfterImage { get; }
    internal JsonObject? ShiningAfterImage { get; }
    internal ResourceStateLedger? StateAfterImage { get; }
    internal ResourceHistoryState? HistoryAfterImage { get; }
    internal bool CycleChanged { get; }
    internal string PreviousReturnCycleId { get; }
    internal string CurrentReturnCycleId { get; }
    internal IReadOnlyList<ValidationIssue> Issues => Array.AsReadOnly(_issues.ToArray());
    internal bool IsValid =>
        ProfilesAfterImage != null &&
        ShiningAfterImage != null &&
        StateAfterImage != null &&
        HistoryAfterImage != null &&
        _issues.Length == 0;
}

internal static class ShiningReturnCycleResourcePlanner
{
    internal static ShiningReturnCycleResourcePlanningResult Build(
        ShiningReturnCycleResourcePlanningInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.Turn <= 0)
            throw new ArgumentOutOfRangeException(nameof(input.Turn));
        if (input.CurrentIncarnation < 0)
            throw new ArgumentOutOfRangeException(nameof(input.CurrentIncarnation));
        ArgumentNullException.ThrowIfNull(input.Definitions);
        ArgumentNullException.ThrowIfNull(input.State);
        ArgumentNullException.ThrowIfNull(input.History);
        ArgumentNullException.ThrowIfNull(input.PreTurnOwners);
        ArgumentNullException.ThrowIfNull(input.AcceptedProfiles);
        ArgumentNullException.ThrowIfNull(input.AcceptedShiningAbode);
        ArgumentNullException.ThrowIfNull(input.AcceptedSoulState);

        var acceptedShining = input.AcceptedShiningAbode.DeepClone().AsObject();
        var previousReturnCycleId = ReadReturnCycleId(acceptedShining);
        ShiningAbodeState.SyncShiningReturnCycle(
            acceptedShining,
            input.CurrentIncarnation,
            out var cycleChanged);
        var currentReturnCycleId = ReadReturnCycleId(acceptedShining);

        var acceptedOwners = new AfterlifeResourceOwnerRoots(
            input.AcceptedProfiles,
            input.PreTurnOwners.SpiritualConflict,
            input.AcceptedSoulState,
            acceptedShining,
            input.PreTurnOwners.Guardians);
        var ownerResourcePlan = AfterlifeOwnerResourceStatePlanner.Build(
            new AfterlifeOwnerResourceStatePlanningInput(
                input.Turn,
                input.Definitions,
                input.State,
                input.History,
                input.PreTurnOwners,
                acceptedOwners));
        if (!ownerResourcePlan.IsValid ||
            ownerResourcePlan.Composition == null ||
            ownerResourcePlan.StateAfterImage == null ||
            ownerResourcePlan.HistoryAfterImage == null)
        {
            return Failure(
                cycleChanged,
                previousReturnCycleId,
                currentReturnCycleId,
                ownerResourcePlan.Issues);
        }

        var shiningAfterImage = ownerResourcePlan.Composition.OwnerCompanionAfterImages.TryGetValue(
            ShiningAbodeState.StatePath,
            out var composedShining)
            ? composedShining.DeepClone().AsObject()
            : acceptedShining;
        var profilesAfterImage = ownerResourcePlan.Composition.OwnerCompanionAfterImages.TryGetValue(
            AfterlifeEntityProfileState.StatePath,
            out var composedProfiles)
            ? composedProfiles.DeepClone().AsObject()
            : input.AcceptedProfiles.DeepClone().AsObject();
        return new ShiningReturnCycleResourcePlanningResult(
            profilesAfterImage,
            shiningAfterImage,
            ownerResourcePlan.StateAfterImage,
            ownerResourcePlan.HistoryAfterImage,
            cycleChanged,
            previousReturnCycleId,
            currentReturnCycleId,
            Array.Empty<ValidationIssue>());
    }

    private static ShiningReturnCycleResourcePlanningResult Failure(
        bool cycleChanged,
        string previousReturnCycleId,
        string currentReturnCycleId,
        IReadOnlyList<ValidationIssue> issues) =>
        new(
            null,
            null,
            null,
            null,
            cycleChanged,
            previousReturnCycleId,
            currentReturnCycleId,
            issues);

    private static string ReadReturnCycleId(JsonObject root) =>
        root["gachaSystem"]?["currentReturnCycleId"] is JsonValue value &&
        value.TryGetValue<string>(out var returnCycleId)
            ? returnCycleId ?? string.Empty
            : string.Empty;
}
