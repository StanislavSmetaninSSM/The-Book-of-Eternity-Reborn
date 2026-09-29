using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

/// <summary>
/// Holds a pure wound reduction's detached output without claiming effect completion or publication authority.
/// </summary>
internal sealed class WoundStateReduction
{
    private readonly WoundCarrierCatalogInput _carriers;
    private readonly JsonObject _identity;
    private readonly JsonObject _history;
    private readonly WoundCarrierContribution[] _contributions;
    private readonly WoundTransitionIntent[] _intents;

    /// <summary>
    /// Detaches the wound reducer's resulting images and ordered contributions.
    /// </summary>
    /// <param name="carriers">
    /// Wound carriers after the reduction.
    /// </param>
    /// <param name="identity">
    /// Resulting wound identity index.
    /// </param>
    /// <param name="history">
    /// Resulting wound history, including earlier transitions.
    /// </param>
    /// <param name="contributions">
    /// Ordered carrier changes relative to the operation's before-state.
    /// </param>
    /// <param name="intents">
    /// Ordered transition intents emitted by the wound reducer.
    /// </param>
    internal WoundStateReduction(WoundCarrierCatalogInput carriers, JsonObject identity, JsonObject history,
        IReadOnlyList<WoundCarrierContribution> contributions, IReadOnlyList<WoundTransitionIntent> intents)
    {
        _carriers = WoundAcceptedTurnData.CloneWoundCarriers(carriers)!;
        _identity = identity.DeepClone().AsObject();
        _history = history.DeepClone().AsObject();
        _contributions = contributions.Select(WoundAcceptedTurnData.CloneCarrierContribution).ToArray();
        _intents = intents.Select(WoundAcceptedTurnData.CloneTransitionIntent).ToArray();
    }

    /// <summary>
    /// Gets detached wound carriers after reduction.
    /// </summary>
    internal WoundCarrierCatalogInput Carriers => WoundAcceptedTurnData.CloneWoundCarriers(_carriers)!;
    /// <summary>
    /// Gets the detached resulting wound identity index.
    /// </summary>
    internal JsonObject Identity => _identity.DeepClone().AsObject();
    /// <summary>
    /// Gets the detached resulting wound history.
    /// </summary>
    internal JsonObject History => _history.DeepClone().AsObject();
    /// <summary>
    /// Gets detached carrier contributions in reduction order.
    /// </summary>
    internal IReadOnlyList<WoundCarrierContribution> Contributions =>
        Array.AsReadOnly(_contributions.Select(WoundAcceptedTurnData.CloneCarrierContribution).ToArray());
    /// <summary>
    /// Gets detached transition intents in reduction order.
    /// </summary>
    internal IReadOnlyList<WoundTransitionIntent> Intents =>
        Array.AsReadOnly(_intents.Select(WoundAcceptedTurnData.CloneTransitionIntent).ToArray());
}

/// <summary>
/// Reports detached wound reduction output or its validation failures.
/// </summary>
/// <param name="State">
/// Reduced state, or <see langword="null"/> when reduction fails.
/// </param>
/// <param name="Issues">
/// Validation failures; empty when reduction succeeds.
/// </param>
internal sealed record WoundStateReductionResult(WoundStateReduction? State, IReadOnlyList<ValidationIssue> Issues);
