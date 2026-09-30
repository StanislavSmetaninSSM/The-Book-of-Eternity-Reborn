using System.Collections.ObjectModel;
using System.Text.Json;

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
    private const int MaximumCapturedRows = 10_000;

    internal static EffectRollSourceCaptureResult Capture(
        EffectMechanicsSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (!snapshot.IsAccepted)
        {
            return new EffectRollSourceCaptureResult(
                false,
                null,
                ReadOnly(snapshot.Issues));
        }

        var rows = new List<EffectDetachedRollSourceRow>();
        foreach (var component in snapshot.Components.Where(
                     static value => value.Profile == "roll_modifier"))
        {
            if (rows.Count == MaximumCapturedRows)
            {
                return new EffectRollSourceCaptureResult(
                    false,
                    null,
                    InvalidIssues("effect_roll_source_capture_too_many_rows"));
            }

            if (!TryCapture(component, rows.Count, out var row))
            {
                return new EffectRollSourceCaptureResult(
                    false,
                    null,
                    InvalidIssues("effect_roll_source_capture_invalid"));
            }

            rows.Add(row!);
        }

        var authority = EffectDetachedRollSourceAuthority.Create(rows);
        return authority.HasValidSeal(out var issues)
            ? new EffectRollSourceCaptureResult(
                true,
                authority,
                Array.Empty<ValidationIssue>())
            : new EffectRollSourceCaptureResult(false, null, issues);
    }

    internal static EffectRollContributionResolution Resolve(
        EffectMechanicsSnapshot snapshot,
        EffectRollContext context)
    {
        var capture = Capture(snapshot);
        if (!capture.IsValid || capture.Authority is null)
        {
            return Invalid(capture.Issues);
        }

        return Resolve(capture.Authority, context, snapshot.SkillScopeAuthority);
    }

    internal static EffectRollContributionResolution Resolve(
        EffectDetachedRollSourceAuthority source,
        EffectRollContext context,
        EffectRollSkillScopeAuthority currentSkills)
    {
        ArgumentNullException.ThrowIfNull(currentSkills);

        return ResolveCore(
            source,
            context,
            row =>
            {
                if (context.SkillId is null ||
                    !string.Equals(
                        row.ScopeSkillId,
                        context.SkillId,
                        StringComparison.Ordinal))
                {
                    return new ScopeDecision(false, null);
                }

                var authority = currentSkills.ResolveCurrent(
                    new EffectTargetKey(
                        context.Realm,
                        context.ActorKind,
                        context.ActorId),
                    context.SkillId,
                    "effects[" + row.EffectId + "].components[" +
                    row.ComponentId + "].payload.scope.skillId");
                return authority.State == EffectRollSkillScopeState.InvalidAuthority
                    ? new ScopeDecision(false, authority.Issues)
                    : new ScopeDecision(authority.IsUsable, null);
            });
    }

    internal static EffectRollContributionResolution Resolve(
        EffectDetachedRollSourceAuthority source,
        EffectRollContext context,
        EffectRollSkillUsabilityProof? selectedSkill)
    {
        return ResolveCore(
            source,
            context,
            row =>
            {
                var accepted = context.SkillId is not null &&
                    selectedSkill is not null &&
                    string.Equals(
                        row.ScopeSkillId,
                        context.SkillId,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        selectedSkill.Realm,
                        context.Realm,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        selectedSkill.ActorKind,
                        context.ActorKind,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        selectedSkill.ActorId,
                        context.ActorId,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        selectedSkill.SkillId,
                        context.SkillId,
                        StringComparison.Ordinal);
                return new ScopeDecision(accepted, null);
            });
    }

    private static EffectRollContributionResolution ResolveCore(
        EffectDetachedRollSourceAuthority source,
        EffectRollContext context,
        Func<EffectDetachedRollSourceRow, ScopeDecision> scoped)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(context);

        if (!source.HasValidSeal(out var issues))
        {
            return Invalid(issues);
        }

        var accepted = new List<EffectRollContributionEvidence>();
        foreach (var row in source.Rows)
        {
            if (!string.Equals(row.Realm, context.Realm, StringComparison.Ordinal) ||
                !string.Equals(
                    row.TargetKind,
                    context.ActorKind,
                    StringComparison.Ordinal) ||
                !string.Equals(row.TargetId, context.ActorId, StringComparison.Ordinal) ||
                !row.Operations.Contains(context.Operation, StringComparer.Ordinal))
            {
                continue;
            }

            if (row.ScopeKind == "skill")
            {
                var decision = scoped(row);
                if (decision.Issues is not null)
                {
                    return Invalid(decision.Issues);
                }

                if (!decision.Accepted)
                {
                    continue;
                }
            }

            accepted.Add(new EffectRollContributionEvidence(
                row.EffectId,
                row.ComponentId,
                row.Contribution));
        }

        var advantage = accepted.Any(
            static value => value.Contribution == "advantage");
        var disadvantage = accepted.Any(
            static value => value.Contribution == "disadvantage");
        var rollMode = advantage == disadvantage
            ? "normal"
            : advantage
                ? "advantage"
                : "disadvantage";
        return new EffectRollContributionResolution(
            true,
            rollMode,
            ReadOnly(accepted),
            Array.Empty<ValidationIssue>());
    }

    private static bool TryCapture(
        EffectMechanicalComponent component,
        int ordinal,
        out EffectDetachedRollSourceRow? row)
    {
        row = null;
        var payload = component.Payload;
        if (payload.ValueKind != JsonValueKind.Object ||
            !TryOperations(payload, out var operations) ||
            !TryString(payload, "contribution", out var contribution) ||
            !TryScope(payload, out var scopeKind, out var scopeSkillId))
        {
            return false;
        }

        row = EffectDetachedRollSourceRow.Create(
            ordinal,
            component.EffectId,
            component.ComponentId,
            component.Realm,
            component.TargetKind,
            component.TargetId,
            operations!,
            contribution!,
            scopeKind!,
            scopeSkillId);
        return row.IsValid(ordinal);
    }

    private static bool TryOperations(
        JsonElement payload,
        out IReadOnlyList<string>? values)
    {
        values = null;
        if (!payload.TryGetProperty("operations", out var operations) ||
            operations.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var result = new List<string>();
        foreach (var value in operations.EnumerateArray())
        {
            if (value.ValueKind != JsonValueKind.String ||
                value.GetString() is not { } operation)
            {
                return false;
            }

            if (result.Count == 6)
            {
                return false;
            }

            result.Add(operation);
        }

        values = result;
        return true;
    }

    private static bool TryScope(
        JsonElement payload,
        out string? kind,
        out string? skillId)
    {
        kind = null;
        skillId = null;
        if (!payload.TryGetProperty("scope", out var scope) ||
            scope.ValueKind != JsonValueKind.Object ||
            !TryString(scope, "kind", out kind))
        {
            return false;
        }

        if (!scope.TryGetProperty("skillId", out var value))
        {
            return true;
        }

        if (value.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        skillId = value.GetString();
        return true;
    }

    private static bool TryString(
        JsonElement root,
        string property,
        out string? value)
    {
        value = null;
        return root.TryGetProperty(property, out var item) &&
            item.ValueKind == JsonValueKind.String &&
            (value = item.GetString()) is not null;
    }

    private static EffectRollContributionResolution Invalid(
        IReadOnlyList<ValidationIssue> issues)
    {
        return new EffectRollContributionResolution(
            false,
            "normal",
            Array.Empty<EffectRollContributionEvidence>(),
            ReadOnly(issues));
    }

    private static IReadOnlyList<ValidationIssue> InvalidIssues(string code)
    {
        return Array.AsReadOnly(new[]
        {
            new ValidationIssue(
                "treatmentAttempt.procedureCheck.rollSourceAuthority",
                IssueSeverity.Error,
                "Roll sources must be complete normalized mechanics.",
                code: code,
                section: "effect_materialization")
        });
    }

    private static IReadOnlyList<T> ReadOnly<T>(IEnumerable<T> values)
    {
        return new ReadOnlyCollection<T>(values.ToArray());
    }

    private sealed record ScopeDecision(
        bool Accepted,
        IReadOnlyList<ValidationIssue>? Issues);
}
