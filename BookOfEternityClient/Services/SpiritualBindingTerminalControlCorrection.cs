using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

/// <summary>
/// Retains the exact separate terminal echo of one actually corrected binding; it cannot execute or publish state.
/// </summary>
internal sealed class SpiritualBindingTerminalControlCorrection
{
    private readonly bool _originalPresent;
    private readonly JsonNode? _original;
    private readonly bool _correctedPresent;
    private readonly JsonNode? _corrected;

    /// <summary>
    /// Freezes only raw value and presence comparisons from an actual bounded response.
    /// </summary>
    /// <param name="pointer">
    /// Unique effective final-control pointer, outside exchange snapshots.
    /// </param>
    /// <param name="originalOwner">
    /// Actual A carrier whose terminal control is still the immutable original value.
    /// </param>
    /// <param name="correctedAfter">
    /// Actual last exchange after-snapshot; its control is the proposed exact echo.
    /// </param>
    internal SpiritualBindingTerminalControlCorrection(string pointer, JsonObject originalOwner, JsonObject correctedAfter)
    {
        Pointer = pointer;
        _originalPresent = originalOwner.ContainsKey("controlState");
        _original = originalOwner["controlState"]?.DeepClone();
        _correctedPresent = correctedAfter.ContainsKey("controlState");
        _corrected = correctedAfter["controlState"]?.DeepClone();
    }

    /// <summary>
    /// Gets the sole permitted raw field.
    /// </summary>
    internal string Pointer { get; }

    /// <summary>
    /// Checks exact raw presence/value while preserving the originally selected carrier.
    /// </summary>
    /// <param name="raw">
    /// Actual complete raw draft being compared.
    /// </param>
    /// <param name="correctedOnly">
    /// Requires the exact corrected echo instead of also admitting the unchanged original value.
    /// </param>
    /// <returns>
    /// <see langword="true"/> only for the selected allowed complete value and presence.
    /// </returns>
    internal bool Allows(JsonObject raw, bool correctedOnly = false)
    {
        if (AfterlifeSpiritualConflictState.ResolveRawFinalControl(raw) is not { } carrier || carrier.Pointer != Pointer)
            return false;
        var present = carrier.Owner.ContainsKey("controlState");
        var value = carrier.Owner["controlState"];
        return present == _correctedPresent && JsonNode.DeepEquals(value, _corrected) ||
            !correctedOnly && present == _originalPresent && JsonNode.DeepEquals(value, _original);
    }

    /// <summary>
    /// Replaces only the proved terminal echo in a detached diagnostic draft; no physical input is written.
    /// </summary>
    /// <param name="raw">
    /// Disposable copy whose original carrier and value must still match this comparison.
    /// </param>
    /// <returns>
    /// <see langword="true"/> after exact projection, or <see langword="false"/> for an unexpected carrier or value.
    /// </returns>
    internal bool TryApply(JsonObject raw)
    {
        if (!Allows(raw) || AfterlifeSpiritualConflictState.ResolveRawFinalControl(raw) is not { } carrier) return false;
        if (_correctedPresent) carrier.Owner["controlState"] = _corrected?.DeepClone();
        else carrier.Owner.Remove("controlState");
        return true;
    }
}
