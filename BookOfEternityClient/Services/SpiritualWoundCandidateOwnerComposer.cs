using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

/// <summary>
/// Combines detached spiritual candidate fields according to their existing source and effect owners.
/// </summary>
internal static class SpiritualWoundCandidateOwnerComposer
{
    /// <summary>
    /// Rejects an ordinary companion that changes a non-effect field in a shared carrier root.
    /// </summary>
    /// <param name="path">
    /// Registered shared carrier path.
    /// </param>
    /// <param name="companion">
    /// Complete ordinary outcome root proposed for the path.
    /// </param>
    /// <param name="effectCarrier">
    /// Current effect-owned root for the same path; <see langword="null"/> rejects the shared companion.
    /// </param>
    internal static void EnsureCompatibleSharedCompanion(string path, JsonObject companion,
        JsonObject? effectCarrier)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(companion);
        if (effectCarrier is null ||
            !AcceptedMechanicsCarrierAssembler.NonEffectFieldsAgree(path, companion,
                effectCarrier))
            throw new InvalidOperationException(
                $"The C1 companion owner conflicts with the effect carrier for '{path}'.");
    }

    /// <summary>
    /// Applies typed accepted owner transitions to a detached copy of one original draft root.
    /// </summary>
    /// <param name="originalJson">
    /// Retained original owner root JSON selected by the named capture.
    /// </param>
    /// <param name="transitions">
    /// Validated transitions for this one owner path.
    /// </param>
    /// <returns>
    /// Detached projected owner root after every transition.
    /// </returns>
    internal static JsonObject ProjectTypedOwnerRoot(string originalJson,
        IReadOnlyList<AcceptedMechanicsOwnerTransition> transitions)
    {
        ArgumentNullException.ThrowIfNull(originalJson);
        ArgumentNullException.ThrowIfNull(transitions);
        if (transitions.Count == 0 || transitions.Any(value =>
                !string.Equals(value.Path, transitions[0].Path, StringComparison.Ordinal)))
            throw new InvalidOperationException("Typed C1 owner transitions must share one path.");
        var root = JsonNode.Parse(originalJson) as JsonObject ??
            throw new InvalidOperationException("The typed C1 owner root is malformed.");
        foreach (var transition in transitions.OrderBy(value => value.OwnerRef, StringComparer.Ordinal))
            CanonicalStateNormalizer.ApplyOwnerTransition(root, transition);
        return root;
    }

    /// <summary>
    /// Joins the source's executed conflict with its effect-owned combat conditions or exact terminal preparation.
    /// </summary>
    /// <param name="sourceConflict">
    /// Current source-owned conflict state, including only executed exchanges.
    /// </param>
    /// <param name="effectConflict">
    /// Current effect lifecycle carrier for the same signed conflict, or <see langword="null"/> when absent.
    /// </param>
    /// <param name="terminal">
    /// Capture-issued terminal preparation for an exact resolved candidate; <see langword="null"/>
    /// preserves the active-conflict join.
    /// </param>
    /// <returns>
    /// Detached conflict state preserving source progression; terminal projection is permitted only
    /// by the exact capture preparation while the effect owner retains its lifecycle work.
    /// </returns>
    internal static JsonObject MergeExecutedConflict(
        JsonObject sourceConflict, JsonObject? effectConflict,
        ValidationService.SpiritualOriginalTurnCapture.PreparedTerminal? terminal = null)
    {
        ArgumentNullException.ThrowIfNull(sourceConflict);
        var result = sourceConflict.DeepClone().AsObject();
        if (effectConflict is null)
            return result;
        if (terminal != null && terminal.MatchesCandidate(result) &&
            terminal.MatchesEffectExecution(effectConflict))
            return result;
        if (result["activeConflict"] is not JsonObject sourceActive ||
            effectConflict["activeConflict"] is not JsonObject effectActive ||
            !JsonNode.DeepEquals(result["realm"], effectConflict["realm"]) ||
            !JsonNode.DeepEquals(sourceActive["conflictId"], effectActive["conflictId"]) ||
            !JsonNode.DeepEquals(sourceActive["playerSide"], effectActive["playerSide"]) ||
            !JsonNode.DeepEquals(sourceActive["oppositionSide"], effectActive["oppositionSide"]))
            throw new InvalidOperationException(
                "The effect conflict does not match the executed source identity and participants.");
        if (!effectActive.ContainsKey("combatConditions"))
            sourceActive.Remove("combatConditions");
        else if (effectActive["combatConditions"] is JsonArray conditions)
            sourceActive["combatConditions"] = conditions.DeepClone();
        else
            throw new InvalidOperationException("The effect owner has malformed combat conditions.");
        return result;
    }
}
