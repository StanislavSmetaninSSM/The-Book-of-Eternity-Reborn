using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

/// <summary>
/// Identifies an effect allocation by its admitted event, semantic role and actual subject.
/// </summary>
/// <param name="EventRef">
/// Exact causal event reference supplied by the effect owner.
/// </param>
/// <param name="Role">
/// Stable semantic allocation slot, distinguishing replacement retirement and creation.
/// </param>
/// <param name="SubjectId">
/// Exact affected effect identity or canonical target coordinate.
/// </param>
internal sealed record EffectIdentityAllocationKey(string EventRef, string Role, string SubjectId);

/// <summary>
/// Replays ordinary effect and combatant allocations without replacing their execution owners.
/// </summary>
internal sealed class SpiritualWoundEffectIdentityFactory : EffectIdentityFactory
{
    private readonly SpiritualWoundReplayJournal _journal;
    private readonly EffectIdentityFactory _underlying;

    /// <summary>
    /// Binds a capture attempt to its ordinary random allocation factory.
    /// </summary>
    /// <param name="journal">
    /// Non-null retained allocation stream for this attempt.
    /// </param>
    /// <param name="underlying">
    /// Non-null ordinary factory invoked only for new allocations.
    /// </param>
    internal SpiritualWoundEffectIdentityFactory(SpiritualWoundReplayJournal journal, EffectIdentityFactory underlying)
    {
        _journal = journal ?? throw new ArgumentNullException(nameof(journal));
        _underlying = underlying ?? throw new ArgumentNullException(nameof(underlying));
    }

    /// <inheritdoc/>
    internal override string CreateEffectId(EffectIdentityAllocationKey key) =>
        Allocate("effect", () =>
        {
            ArgumentNullException.ThrowIfNull(key);
            SpiritualWoundStateJson.Text(JsonValue.Create(key.EventRef));
            SpiritualWoundStateJson.Text(JsonValue.Create(key.Role));
            SpiritualWoundStateJson.Text(JsonValue.Create(key.SubjectId));
            return JsonSerializer.SerializeToNode(key)!.AsObject();
        }, () => _underlying.CreateEffectId(key));

    /// <inheritdoc/>
    internal override string CreateTransitionId(EffectIdentityAllocationKey key) =>
        Allocate("effect_transition", () =>
        {
            ArgumentNullException.ThrowIfNull(key);
            SpiritualWoundStateJson.Text(JsonValue.Create(key.EventRef));
            SpiritualWoundStateJson.Text(JsonValue.Create(key.Role));
            SpiritualWoundStateJson.Text(JsonValue.Create(key.SubjectId));
            return JsonSerializer.SerializeToNode(key)!.AsObject();
        }, () => _underlying.CreateTransitionId(key));

    /// <inheritdoc/>
    internal override string CreateResolutionId(EffectIdentityAllocationKey key) =>
        Allocate("effect_resolution", () =>
        {
            ArgumentNullException.ThrowIfNull(key);
            SpiritualWoundStateJson.Text(JsonValue.Create(key.EventRef));
            SpiritualWoundStateJson.Text(JsonValue.Create(key.Role));
            SpiritualWoundStateJson.Text(JsonValue.Create(key.SubjectId));
            return JsonSerializer.SerializeToNode(key)!.AsObject();
        }, () => _underlying.CreateResolutionId(key));

    /// <inheritdoc/>
    internal override string CreateCombatantId(string combatantRef) =>
        Allocate("combatant", () => new JsonObject
        {
            ["reference"] = SpiritualWoundStateJson.Text(JsonValue.Create(combatantRef))
        }, () => _underlying.CreateCombatantId(combatantRef));

    /// <inheritdoc/>
    internal override string CreateMemberId(string memberRef) =>
        Allocate("member", () => new JsonObject
        {
            ["reference"] = SpiritualWoundStateJson.Text(JsonValue.Create(memberRef))
        }, () => _underlying.CreateMemberId(memberRef));

    /// <summary>
    /// Rejects allocation without the owning causal coordinate.
    /// </summary>
    /// <returns>
    /// Never returns an unjournaled value.
    /// </returns>
    internal override string CreateEffectId() => RejectUnbound();

    /// <summary>
    /// Rejects allocation without the owning causal coordinate.
    /// </summary>
    /// <returns>
    /// Never returns an unjournaled value.
    /// </returns>
    internal override string CreateTransitionId() => RejectUnbound();

    /// <summary>
    /// Rejects allocation without the owning causal coordinate.
    /// </summary>
    /// <returns>
    /// Never returns an unjournaled value.
    /// </returns>
    internal override string CreateResolutionId() => RejectUnbound();

    /// <summary>
    /// Rejects allocation without the owning causal coordinate.
    /// </summary>
    /// <returns>
    /// Never returns an unjournaled value.
    /// </returns>
    internal override string CreateCombatantId() => RejectUnbound();

    /// <summary>
    /// Rejects allocation without the owning causal coordinate.
    /// </summary>
    /// <returns>
    /// Never returns an unjournaled value.
    /// </returns>
    internal override string CreateMemberId() => RejectUnbound();

    /// <summary>
    /// Computes a comparison coordinate and keeps any failed allocation attempt unusable.
    /// </summary>
    /// <param name="kind">
    /// Closed identity family used as a separate coordinate domain.
    /// </param>
    /// <param name="readKey">
    /// Returns the actual typed owner key; failure revokes this attempt.
    /// </param>
    /// <param name="allocate">
    /// Ordinary owner callback invoked only for a new journal row.
    /// </param>
    /// <returns>
    /// The exact retained or newly allocated opaque identity.
    /// </returns>
    private string Allocate(string kind, Func<JsonObject> readKey, Func<string> allocate)
    {
        try
        {
            var coordinate = SpiritualWoundStateJson.Hash(readKey(), "allocation_" + kind);
            return _journal.Request(kind, "effects", coordinate, allocate);
        }
        catch
        {
            _journal.Invalidate();
            throw;
        }
    }

    /// <summary>
    /// Revokes an attempt that tried to bypass causal allocation binding.
    /// </summary>
    /// <returns>
    /// Never returns a value.
    /// </returns>
    private string RejectUnbound()
    {
        _journal.Invalidate();
        throw new InvalidOperationException("An owning causal coordinate is required for journaled allocation.");
    }
}
