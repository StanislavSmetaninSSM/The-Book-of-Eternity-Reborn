using System.Collections.ObjectModel;

namespace BookOfEternityClient.Services;

internal sealed record EffectRollContext(
    string Realm,
    string ActorKind,
    string ActorId,
    string Operation,
    string? SkillId);

internal sealed record EffectRollContributionEvidence(
    string EffectId,
    string ComponentId,
    string Contribution);

internal sealed record EffectRollContributionResolution(
    bool IsValid,
    string RollMode,
    IReadOnlyList<EffectRollContributionEvidence> Contributions,
    IReadOnlyList<ValidationIssue> Issues);

internal static class EffectRollContributionResolver
{
    internal static EffectRollContributionResolution Resolve(
        EffectMechanicsSnapshot snapshot,
        EffectRollContext context)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(context);

        if (!snapshot.IsAccepted)
            return Invalid(snapshot.Issues);

        var accepted = new List<EffectRollContributionEvidence>();
        foreach (var component in snapshot.Components)
        {
            if (!string.Equals(component.Profile, "roll_modifier", StringComparison.Ordinal) ||
                !string.Equals(component.Realm, context.Realm, StringComparison.Ordinal) ||
                !string.Equals(component.TargetKind, context.ActorKind, StringComparison.Ordinal) ||
                !string.Equals(component.TargetId, context.ActorId, StringComparison.Ordinal) ||
                !MatchesOperation(component.Payload, context.Operation) ||
                !TryReadScope(component.Payload, out var scopeKind, out var scopedSkillId) ||
                !TryReadContribution(component.Payload, out var contribution))
            {
                continue;
            }

            if (string.Equals(scopeKind, "skill", StringComparison.Ordinal))
            {
                if (context.SkillId == null ||
                    !string.Equals(scopedSkillId, context.SkillId, StringComparison.Ordinal))
                {
                    continue;
                }

                var authority = snapshot.SkillScopeAuthority.ResolveCurrent(
                    new EffectTargetKey(context.Realm, context.ActorKind, context.ActorId),
                    context.SkillId,
                    $"effects[{component.EffectId}].components[{component.ComponentId}].payload.scope.skillId");
                if (authority.State == EffectRollSkillScopeState.InvalidAuthority)
                {
                    return Invalid(authority.Issues);
                }
                if (!authority.IsUsable)
                {
                    continue;
                }
            }
            else if (!string.Equals(scopeKind, "all", StringComparison.Ordinal))
            {
                continue;
            }

            accepted.Add(new EffectRollContributionEvidence(
                component.EffectId,
                component.ComponentId,
                contribution));
        }

        var hasAdvantage = accepted.Any(static item => item.Contribution == "advantage");
        var hasDisadvantage = accepted.Any(static item => item.Contribution == "disadvantage");
        var mode = hasAdvantage == hasDisadvantage
            ? "normal"
            : hasAdvantage ? "advantage" : "disadvantage";
        return new EffectRollContributionResolution(
            true,
            mode,
            ReadOnly(accepted),
            Array.Empty<ValidationIssue>());
    }

    private static EffectRollContributionResolution Invalid(IReadOnlyList<ValidationIssue> issues) => new(
        false,
        "normal",
        Array.Empty<EffectRollContributionEvidence>(),
        ReadOnly(issues));

    private static bool MatchesOperation(System.Text.Json.JsonElement payload, string operation) =>
        payload.TryGetProperty("operations", out var operations) &&
        operations.ValueKind == System.Text.Json.JsonValueKind.Array &&
        operations.EnumerateArray().Any(value =>
            value.ValueKind == System.Text.Json.JsonValueKind.String &&
            string.Equals(value.GetString(), operation, StringComparison.Ordinal));

    private static bool TryReadScope(
        System.Text.Json.JsonElement payload,
        out string? kind,
        out string? skillId)
    {
        kind = null;
        skillId = null;
        if (!payload.TryGetProperty("scope", out var scope) ||
            scope.ValueKind != System.Text.Json.JsonValueKind.Object ||
            !scope.TryGetProperty("kind", out var kindValue) ||
            kindValue.ValueKind != System.Text.Json.JsonValueKind.String)
        {
            return false;
        }

        kind = kindValue.GetString();
        if (string.Equals(kind, "skill", StringComparison.Ordinal) &&
            scope.TryGetProperty("skillId", out var skillValue) &&
            skillValue.ValueKind == System.Text.Json.JsonValueKind.String)
        {
            skillId = skillValue.GetString();
        }
        return kind != null;
    }

    private static bool TryReadContribution(System.Text.Json.JsonElement payload, out string contribution)
    {
        contribution = string.Empty;
        if (!payload.TryGetProperty("contribution", out var value) ||
            value.ValueKind != System.Text.Json.JsonValueKind.String)
        {
            return false;
        }

        contribution = value.GetString() ?? string.Empty;
        return contribution is "advantage" or "disadvantage";
    }

    private static IReadOnlyList<T> ReadOnly<T>(IEnumerable<T> values) =>
        new ReadOnlyCollection<T>(values.ToArray());
}
