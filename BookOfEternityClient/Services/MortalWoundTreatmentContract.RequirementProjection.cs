using System.Collections.Immutable;
using System.Text.Json;

namespace BookOfEternityClient.Services;

internal sealed record WoundRequirementProjection(
    ImmutableArray<MortalWoundTreatmentRequirement> Requirements,
    ImmutableArray<ValidationIssue> Issues)
{
    internal bool IsValid => Issues.IsEmpty;
}

internal static partial class MortalWoundTreatmentContract
{
    // Shared closed DATA grammar only. This does not create a Mortal route,
    // accepted-state authority, reservation or permission to execute anything.
    // Existing Mortal validation/building behavior is unchanged.
    internal static WoundRequirementProjection ParseRequirementProjection(
        IReadOnlyList<JsonElement> requirements, string path)
    {
        ArgumentNullException.ThrowIfNull(requirements);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var issues = new List<ValidationIssue>();
        ValidateRequirements(requirements, path, issues);
        return new(issues.Count == 0 ? BuildRequirements(requirements) : [], issues.ToImmutableArray());
    }
}
