using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

/// <summary>
/// Holds detached inputs read by one wound operation, independently of their original or live provenance.
/// This data container grants no admission, preparation or publication authority.
/// </summary>
internal sealed class WoundOperationBeforeData
{
    private readonly WoundCarrierCatalogInput? _woundCarriers;
    private readonly JsonObject? _woundIdentity;
    private readonly JsonObject? _woundHistory;
    private readonly EffectCarrierCatalogInput? _effectCarriers;
    private readonly JsonObject? _effectIdentity;

    /// <summary>
    /// Detaches the operation's wound and effect snapshots without validating their provenance.
    /// </summary>
    /// <param name="woundCarriers">
    /// Wound carrier snapshots; <see langword="null"/> remains missing for normal input validation.
    /// </param>
    /// <param name="woundIdentity">
    /// Wound identity snapshot; <see langword="null"/> remains missing for normal input validation.
    /// </param>
    /// <param name="woundHistory">
    /// Wound history snapshot; <see langword="null"/> remains missing for normal input validation.
    /// </param>
    /// <param name="effectCarriers">
    /// Effect carriers, required by operations that inspect existing generations.
    /// </param>
    /// <param name="effectIdentity">
    /// Effect identities, required by operations that inspect existing generations.
    /// </param>
    internal WoundOperationBeforeData(WoundCarrierCatalogInput? woundCarriers, JsonObject? woundIdentity,
        JsonObject? woundHistory, EffectCarrierCatalogInput? effectCarriers, JsonObject? effectIdentity)
    {
        _woundCarriers = WoundAcceptedTurnData.CloneWoundCarriers(woundCarriers);
        _woundIdentity = WoundAcceptedTurnData.CloneObject(woundIdentity);
        _woundHistory = WoundAcceptedTurnData.CloneObject(woundHistory);
        _effectCarriers = WoundAcceptedTurnData.CloneEffectCarriers(effectCarriers);
        _effectIdentity = WoundAcceptedTurnData.CloneObject(effectIdentity);
    }

    /// <summary>
    /// Gets a detached copy of the wound carriers read by this operation.
    /// </summary>
    internal WoundCarrierCatalogInput? WoundCarriers => WoundAcceptedTurnData.CloneWoundCarriers(_woundCarriers);
    /// <summary>
    /// Gets a detached copy of the wound identity snapshot.
    /// </summary>
    internal JsonObject? WoundIdentity => WoundAcceptedTurnData.CloneObject(_woundIdentity);
    /// <summary>
    /// Gets a detached copy of the wound history snapshot.
    /// </summary>
    internal JsonObject? WoundHistory => WoundAcceptedTurnData.CloneObject(_woundHistory);
    /// <summary>
    /// Gets a detached copy of the effect carriers used for generation validation.
    /// </summary>
    internal EffectCarrierCatalogInput? EffectCarriers => WoundAcceptedTurnData.CloneEffectCarriers(_effectCarriers);
    /// <summary>
    /// Gets a detached copy of the effect identities used for generation validation.
    /// </summary>
    internal JsonObject? EffectIdentity => WoundAcceptedTurnData.CloneObject(_effectIdentity);

    /// <summary>
    /// Reads the ordinary preparation path's original snapshots without changing the input.
    /// </summary>
    /// <param name="input">
    /// Original wound input whose pre-turn values supply this operation's before-state.
    /// </param>
    /// <returns>
    /// Detached original before-state data, without an additional authority claim.
    /// </returns>
    internal static WoundOperationBeforeData FromOriginal(WoundAcceptedTurnInput input) => new(
        input.PreTurnCarriers, input.PreTurnIdentityIndex, input.PreTurnHistory,
        input.PreTurnEffectCarriers, input.PreTurnEffectIdentityIndex);
}
