namespace BookOfEternityClient.Services;

internal sealed record EffectReplayIdentity
{
    internal EffectReplayIdentity(
        string effectId,
        ResourcePendingAuthorityBinding authority)
    {
        ArgumentNullException.ThrowIfNull(authority);
        EffectAcceptedTurnPlanner.ValidatePendingEffectAuthority(
            effectId,
            authority);
        EffectId = effectId;
        Authority = authority with { };
    }

    internal string EffectId { get; }

    internal ResourcePendingAuthorityBinding Authority { get; }
}
