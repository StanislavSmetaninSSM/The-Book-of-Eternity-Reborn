using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal sealed class ResourceRepairPacket
{
    internal string Kind { get; init; } = "resource_semantic_omission_repair";
    internal string Priority { get; init; } = "blocking";
    internal string Title { get; init; } = "Дополнить одну семантику изменения ресурса";
    internal string Route { get; init; } = "resourceChanges";
    internal int CommandOrdinal { get; init; } = -1;
    internal string CommandSemantic { get; init; } = "";
    internal IReadOnlyList<string> MissingSemantics { get; init; } = Array.Empty<string>();
    internal IReadOnlyList<string> TargetFiles { get; init; } = Array.Empty<string>();
    internal bool FullTurnResubmissionRequired { get; init; } = true;
    internal IReadOnlyList<string> ResubmissionObligations { get; init; } = Array.Empty<string>();
    internal IReadOnlyList<string> ExpectedShape { get; init; } = Array.Empty<string>();
    internal IReadOnlyList<string> SafeCorrectionRules { get; init; } = Array.Empty<string>();
    internal IReadOnlyList<string> Steps { get; init; } = Array.Empty<string>();
    internal IReadOnlyList<string> DoNotDo { get; init; } = Array.Empty<string>();

    internal JsonObject ToJsonObject() => new()
    {
        ["kind"] = Kind,
        ["priority"] = Priority,
        ["title"] = Title,
        ["route"] = Route,
        ["commandSemantic"] = CommandSemantic,
        ["missingSemantics"] = ToArray(MissingSemantics),
        ["targetFiles"] = ToArray(TargetFiles),
        ["fullTurnResubmissionRequired"] = FullTurnResubmissionRequired,
        ["resubmissionObligations"] = ToArray(ResubmissionObligations),
        ["expectedShape"] = ToArray(ExpectedShape),
        ["safeCorrectionRules"] = ToArray(SafeCorrectionRules),
        ["steps"] = ToArray(Steps),
        ["doNotDo"] = ToArray(DoNotDo)
    };

    private static JsonArray ToArray(IEnumerable<string> values) =>
        new(values.Select(static value => (JsonNode)value).ToArray());
}

/// <summary>
/// Projects validation evidence into one bounded GM-authored semantic repair.
/// Protected coordinates, selectors, paths, identities, current state, history,
/// and authority evidence never enter the actionable packet.
/// </summary>
internal static class ResourceRepairPacketBuilder
{
    private const string RepairableCode = "resource_command_invalid_field";

    internal static IReadOnlyList<ResourceRepairPacket> Build(
        IEnumerable<ValidationIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(issues);
        var materialized = DistinctEvidence(issues);
        if (materialized.Length != 1 ||
            !TryResolveMissingReason(materialized[0], out var commandNumber))
        {
            return Array.Empty<ResourceRepairPacket>();
        }

        return new[]
        {
            new ResourceRepairPacket
            {
                CommandOrdinal = commandNumber - 1,
                CommandSemantic = $"ordinary resource change #{commandNumber}",
                MissingSemantics = new[] { "narrative reason" },
                FullTurnResubmissionRequired = true,
                ResubmissionObligations = new[]
                {
                    "complete rejected turn response",
                    "same in-world resource change",
                    "all unrelated accepted-turn narration and state"
                },
                ExpectedShape = new[]
                {
                    "One complete ordinary resource change with a readable in-world reason.",
                    "The complete rejected turn is resubmitted coherently."
                },
                SafeCorrectionRules = new[]
                {
                    "Add only the missing readable reason to the named resource change.",
                    "Keep the same in-world intent and resubmit the complete turn response."
                },
                Steps = new[]
                {
                    "Restore one short readable reason for the named resource change.",
                    "Resubmit the complete rejected turn and wait for normal validation."
                },
                DoNotDo = new[]
                {
                    "Do not write current or maximum values directly.",
                    "Do not invent identifiers, selectors, event evidence, history, receipts, or repair metadata.",
                    "Do not repair more than the single named semantic omission."
                }
            }
        };
    }

    internal static bool RequiresFailClosedRollback(
        IEnumerable<ValidationIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(issues);
        var materialized = issues.ToArray();
        return materialized.Any(IsResourceIssue) && Build(materialized).Count == 0;
    }

    private static bool TryResolveMissingReason(
        ValidationIssue issue,
        out int commandNumber)
    {
        commandNumber = 0;
        if (!IsResourceIssue(issue) ||
            issue.Severity != IssueSeverity.Error ||
            !string.Equals(issue.Code, RepairableCode, StringComparison.Ordinal) ||
            !string.Equals(issue.Actor, "Client", StringComparison.Ordinal) ||
            !string.Equals(
                issue.Section,
                "UnifiedResourceAuthority",
                StringComparison.Ordinal) ||
            issue.Category != IssueCategory.StateConsistency ||
            !string.Equals(issue.Actual, "missing", StringComparison.OrdinalIgnoreCase) ||
            issue.RepairTargetFiles.Count != 0)
        {
            return false;
        }

        const string prefix = "resourceChanges[";
        const string suffix = "].reason";
        if (!issue.FilePath.StartsWith(prefix, StringComparison.Ordinal) ||
            !issue.FilePath.EndsWith(suffix, StringComparison.Ordinal))
        {
            return false;
        }

        var token = issue.FilePath[prefix.Length..^suffix.Length];
        if (!int.TryParse(token, out var zeroBasedIndex) ||
            zeroBasedIndex < 0 ||
            zeroBasedIndex == int.MaxValue ||
            !string.Equals(token, zeroBasedIndex.ToString(), StringComparison.Ordinal))
        {
            return false;
        }

        commandNumber = zeroBasedIndex + 1;
        return true;
    }

    private static bool IsResourceIssue(ValidationIssue issue) =>
        issue.Code?.StartsWith("resource_", StringComparison.Ordinal) == true ||
        string.Equals(issue.Section, "UnifiedResourceAuthority", StringComparison.Ordinal) ||
        string.Equals(issue.Section, "resource_materialization", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(issue.Section, "resource_planner", StringComparison.OrdinalIgnoreCase);

    private static ValidationIssue[] DistinctEvidence(
        IEnumerable<ValidationIssue> issues)
    {
        var distinct = new List<ValidationIssue>();
        foreach (var issue in issues)
        {
            if (!distinct.Any(existing => SameEvidence(existing, issue)))
                distinct.Add(issue);
        }
        return distinct.ToArray();
    }

    private static bool SameEvidence(ValidationIssue left, ValidationIssue right) =>
        string.Equals(left.FilePath, right.FilePath, StringComparison.Ordinal) &&
        left.Severity == right.Severity &&
        string.Equals(left.Message, right.Message, StringComparison.Ordinal) &&
        left.Category == right.Category &&
        string.Equals(left.Code, right.Code, StringComparison.Ordinal) &&
        string.Equals(left.Actor, right.Actor, StringComparison.Ordinal) &&
        string.Equals(left.Section, right.Section, StringComparison.Ordinal) &&
        string.Equals(left.Expected, right.Expected, StringComparison.Ordinal) &&
        string.Equals(left.Actual, right.Actual, StringComparison.Ordinal) &&
        string.Equals(left.RepairHint, right.RepairHint, StringComparison.Ordinal) &&
        left.RepairTargetFiles.SequenceEqual(
            right.RepairTargetFiles,
            StringComparer.Ordinal);
}
