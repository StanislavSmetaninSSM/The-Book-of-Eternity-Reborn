using System.Collections.Frozen;

namespace BookOfEternityClient.Services;

internal enum EffectReactionResultBehavior
{
    ApplyDefinition,
    PeriodicComponent,
    EventOutcome,
    Suspend,
    Remove
}

internal sealed record EffectReactionResultDescriptor(
    string Kind,
    string ResolutionMode,
    EffectReactionResultBehavior Behavior,
    IReadOnlySet<string> AllowedDependencies);

/// <summary>
/// One closed registry for reaction schema, phase eligibility, and runtime
/// dispatch. Adding a result kind without an execution policy is impossible.
/// </summary>
internal static class EffectReactionResultCatalog
{
    private static readonly FrozenSet<string> AllDependencies =
        new[]
        {
            "before_current_event",
            "after_component",
            "after_current_event"
        }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> ComponentDependencies =
        new[] { "after_component" }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly IReadOnlyList<EffectReactionResultDescriptor> Descriptors =
        Array.AsReadOnly(new[]
    {
        Descriptor(
            "apply_definition",
            "deterministic",
            EffectReactionResultBehavior.ApplyDefinition,
            AllDependencies),
        Descriptor(
            "bounded_receipt",
            "bounded_receipt",
            EffectReactionResultBehavior.PeriodicComponent,
            ComponentDependencies),
        Descriptor(
            "event_outcome",
            "deterministic",
            EffectReactionResultBehavior.EventOutcome,
            AllDependencies),
        Descriptor(
            "remove",
            "deterministic",
            EffectReactionResultBehavior.Remove,
            AllDependencies),
        Descriptor(
            "suspend",
            "deterministic",
            EffectReactionResultBehavior.Suspend,
            AllDependencies),
        Descriptor(
            "trigger_component",
            "deterministic",
            EffectReactionResultBehavior.PeriodicComponent,
            ComponentDependencies)
    });

    private static readonly FrozenDictionary<string, EffectReactionResultDescriptor> ByKind =
        Descriptors.ToFrozenDictionary(
            static descriptor => descriptor.Kind,
            StringComparer.Ordinal);

    private static readonly FrozenSet<string> Kinds =
        ByKind.Keys.ToFrozenSet(StringComparer.Ordinal);

    internal static IReadOnlyList<EffectReactionResultDescriptor> All => Descriptors;

    internal static IReadOnlySet<string> RegisteredKinds => Kinds;

    internal static bool TryResolve(
        string? kind,
        out EffectReactionResultDescriptor descriptor)
    {
        descriptor = null!;
        return kind != null && ByKind.TryGetValue(kind, out descriptor!);
    }

    private static EffectReactionResultDescriptor Descriptor(
        string kind,
        string resolutionMode,
        EffectReactionResultBehavior behavior,
        IReadOnlySet<string> dependencies) =>
        new(kind, resolutionMode, behavior, dependencies);
}
