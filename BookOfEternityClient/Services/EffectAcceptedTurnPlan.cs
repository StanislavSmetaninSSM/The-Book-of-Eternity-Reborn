using System.Collections.ObjectModel;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal sealed record EffectAcceptedTurnInput(
    string SessionId,
    string SnapshotToken,
    JsonObject RawCommands,
    EffectSourceAuthority SourceAuthority,
    EffectTargetAuthority TargetAuthority,
    JsonObject EventInput,
    string Realm = "mortal_world");

internal sealed class EffectAcceptedTurnPlan
{
    internal EffectAcceptedTurnPlan(
        string inputFingerprint,
        IReadOnlyList<string> allocatedEffectIds,
        IReadOnlyList<string> allocatedTransitionIds,
        IReadOnlyList<EffectSourceKey> sources,
        IReadOnlyList<EffectTargetKey> targets)
    {
        InputFingerprint = inputFingerprint;
        AllocatedEffectIds = new ReadOnlyCollection<string>(allocatedEffectIds.ToArray());
        AllocatedTransitionIds = new ReadOnlyCollection<string>(allocatedTransitionIds.ToArray());
        Sources = new ReadOnlyCollection<EffectSourceKey>(sources.ToArray());
        Targets = new ReadOnlyCollection<EffectTargetKey>(targets.ToArray());
    }

    internal string InputFingerprint { get; }

    internal IReadOnlyList<string> AllocatedEffectIds { get; }

    internal IReadOnlyList<string> AllocatedTransitionIds { get; }

    internal IReadOnlyList<EffectSourceKey> Sources { get; }

    internal IReadOnlyList<EffectTargetKey> Targets { get; }
}

internal sealed record EffectAcceptedTurnPlanningResult(
    EffectAcceptedTurnPlan? Plan,
    IReadOnlyList<ValidationIssue> Issues)
{
    internal bool Success => Plan != null && Issues.Count == 0;
}

internal static class EffectAcceptedTurnPlanBuilder
{
    internal static EffectAcceptedTurnPlanningResult Build(
        EffectAcceptedTurnInput input,
        string fingerprint,
        EffectIdentityFactory identityFactory)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(identityFactory);
        var issues = new List<ValidationIssue>();
        issues.AddRange(input.SourceAuthority.Issues);
        issues.AddRange(input.TargetAuthority.Issues);
        if (!TryExact(input.SessionId) || !TryExact(input.SnapshotToken) || !TryExact(input.Realm))
        {
            Add(issues, "effectAcceptedTurn", "effect_plan_input_invalid", "exact session, snapshot, and realm authority", input.SessionId + "/" + input.SnapshotToken + "/" + input.Realm);
        }
        if (input.RawCommands["effectChanges"] is not JsonArray changes)
        {
            Add(issues, "effectChanges", "effect_plan_input_invalid", "effectChanges array", input.RawCommands.ToJsonString());
            return new EffectAcceptedTurnPlanningResult(null, issues);
        }

        var resolvedSources = new List<EffectSourceKey>();
        var resolvedTargets = new List<EffectTargetKey>();
        for (var index = 0; index < changes.Count; index++)
        {
            if (changes[index] is not JsonObject change ||
                !TryRead(change["operation"], out var operation) ||
                !string.Equals(operation, "apply", StringComparison.Ordinal))
            {
                continue;
            }
            var path = $"effectChanges[{index}]";
            if (change["source"] is not JsonObject source ||
                !TryRead(source["kind"], out var sourceKind) ||
                !TryRead(source["sourceId"], out var sourceId) ||
                !TryRead(source["definitionKey"], out var definitionKey) ||
                change["target"] is not JsonObject target ||
                !TryRead(target["kind"], out var targetKind))
            {
                Add(issues, path, "effect_plan_input_invalid", "complete exact apply source and target selectors", change.ToJsonString());
                continue;
            }
            var sourceResolution = input.SourceAuthority.Resolve(
                new EffectSourceKey(input.Realm, sourceKind, sourceId, definitionKey),
                targetKind,
                change["parameters"] as JsonObject);
            var targetResolution = input.TargetAuthority.Resolve(target, input.Realm);
            issues.AddRange(sourceResolution.Issues.Select(issue => Prefix(issue, path + ".source")));
            issues.AddRange(targetResolution.Issues.Select(issue => Prefix(issue, path + ".target")));
            if (sourceResolution.Success && targetResolution.Success)
            {
                resolvedSources.Add(sourceResolution.Source!.Key);
                resolvedTargets.Add(targetResolution.Target!);
            }
        }

        if (issues.Count > 0)
            return new EffectAcceptedTurnPlanningResult(null, issues);

        var effectIds = new List<string>(resolvedSources.Count);
        var transitionIds = new List<string>(resolvedSources.Count);
        for (var index = 0; index < resolvedSources.Count; index++)
        {
            effectIds.Add(identityFactory.CreateEffectId());
            transitionIds.Add(identityFactory.CreateTransitionId());
        }
        return new EffectAcceptedTurnPlanningResult(
            new EffectAcceptedTurnPlan(
                fingerprint,
                effectIds,
                transitionIds,
                resolvedSources,
                resolvedTargets),
            Array.Empty<ValidationIssue>());
    }

    private static ValidationIssue Prefix(ValidationIssue issue, string prefix) =>
        new(
            prefix + "." + issue.FilePath,
            issue.Severity,
            issue.Message,
            code: issue.Code,
            section: issue.Section,
            expected: issue.Expected,
            actual: issue.Actual,
            repairHint: issue.RepairHint);

    private static bool TryRead(JsonNode? node, out string value)
    {
        value = node is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var text) ? text : string.Empty;
        return TryExact(value);
    }

    private static bool TryExact(string value) =>
        value.Length > 0 && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    private static void Add(List<ValidationIssue> issues, string path, string code, string expected, string actual) =>
        issues.Add(new ValidationIssue(
            path,
            IssueSeverity.Error,
            "Accepted effect plan input is incomplete or lacks exact source/target authority.",
            code: code,
            section: "effect_materialization",
            expected: expected,
            actual: actual,
            repairHint: "Resubmit one coherent effect command package against the current validated session and authority catalogs."));
}
