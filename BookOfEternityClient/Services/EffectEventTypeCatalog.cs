using System.Collections.Frozen;

namespace BookOfEternityClient.Services;

internal static class EffectEventTypeCatalog
{
    private static readonly FrozenSet<string> ResourceEventSet = new[]
    {
        "resource_damaged",
        "resource_restored",
        "resource_spent",
        "resource_gained",
        "resource_depleted",
        "resource_filled"
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> RegisteredEventSet = new[]
    {
        "owner_turn_start",
        "owner_turn_end",
        "owner_damaged",
        "owner_restored",
        "owner_action_started",
        "owner_action_completed",
        "owner_critical_failure",
        "afterlife_exchange_end",
        "scene_started",
        "scene_ended",
        "source_state_changed",
        "condition_changed"
    }.Concat(ResourceEventSet).ToFrozenSet(StringComparer.Ordinal);

    internal static IReadOnlySet<string> Registered => RegisteredEventSet;

    internal static IReadOnlySet<string> ResourceEvents => ResourceEventSet;

    internal static bool IsRegistered(string? eventType) =>
        eventType != null && RegisteredEventSet.Contains(eventType);

    internal static bool IsResourceEvent(string? eventType) =>
        eventType != null && ResourceEventSet.Contains(eventType);
}
