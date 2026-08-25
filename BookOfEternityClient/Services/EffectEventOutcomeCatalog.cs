using System.Collections.Frozen;

namespace BookOfEternityClient.Services;

internal sealed record EffectEventOutcomeTransition(
    string EventType,
    string OriginalOutcome,
    string ResolvedOutcome);

/// <summary>
/// Closed source-owned transformations that a validated event adapter may
/// attest. The adapter still proves the event from accepted client evidence;
/// a source definition cannot invent an open-ended outcome token or mapping.
/// </summary>
internal static class EffectEventOutcomeCatalog
{
    private static readonly FrozenSet<EffectEventOutcomeTransition> Registered =
        new[]
        {
            new EffectEventOutcomeTransition(
                "owner_critical_failure",
                "critical_failure",
                "failure")
        }.ToFrozenSet();

    internal static bool Contains(
        string eventType,
        string originalOutcome,
        string resolvedOutcome) =>
        Registered.Contains(new EffectEventOutcomeTransition(
            eventType,
            originalOutcome,
            resolvedOutcome));
}
