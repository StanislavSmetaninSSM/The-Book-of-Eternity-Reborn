using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace BookOfEternityClient.Services;

internal static class FactionCoreChangesContract
{
    internal const string PropertyName = "factionCoreChanges";
    internal const string FactionCorePath =
        "game_state/factions/faction_core.json";

    private static readonly HashSet<string> CommandKeys =
        new(StringComparer.Ordinal)
        {
            "factionId",
            "reason",
            "profile",
            "purposeAndPrinciples",
            "progressionAndPower",
            "governanceAndLeadership",
            "playerMembership",
            "relations"
        };

    private static readonly HashSet<string> GroupKeys =
        new(StringComparer.Ordinal)
        {
            "profile",
            "purposeAndPrinciples",
            "progressionAndPower",
            "governanceAndLeadership",
            "playerMembership",
            "relations"
        };

    private static readonly HashSet<string> ProfileKeys =
        new(StringComparer.Ordinal)
        {
            "name",
            "description",
            "image_prompt",
            "factionColor"
        };

    private static readonly HashSet<string> PurposeAndPrinciplesKeys =
        new(StringComparer.Ordinal)
        {
            "purpose",
            "currentAgenda",
            "principles"
        };

    private static readonly HashSet<string> ProgressionAndPowerKeys =
        new(StringComparer.Ordinal)
        {
            "level",
            "experience",
            "experienceForNextLevel",
            "developmentArchetype",
            "customArchetypePriorities",
            "powerProfile"
        };

    private static readonly HashSet<string> PowerProfileKeys =
        new(StringComparer.Ordinal)
        {
            "military",
            "economic",
            "social",
            "covert",
            "logistics",
            "stability",
            "arcane_tech",
            "exploration"
        };

    private static readonly HashSet<string> CustomArchetypePriorityKeys =
        new(StringComparer.Ordinal)
        {
            "primary",
            "secondary",
            "tertiary"
        };

    private static readonly HashSet<string> GovernanceAndLeadershipKeys =
        new(StringComparer.Ordinal)
        {
            "governance",
            "leadership"
        };

    private static readonly HashSet<string> GovernanceKeys =
        new(StringComparer.Ordinal)
        {
            "model",
            "decisionProcess"
        };

    private static readonly HashSet<string> LeadershipKeys =
        new(StringComparer.Ordinal)
        {
            "leadershipState",
            "summary",
            "leaderNpcIds"
        };

    private static readonly HashSet<string> LeadershipStates =
        new(StringComparer.Ordinal)
        {
            "headed",
            "collective",
            "vacant"
        };

    private static readonly HashSet<string> PlayerMembershipKeys =
        new(StringComparer.Ordinal)
        {
            "isPlayerFaction",
            "isPlayerMember",
            "playerRank",
            "playerBranch",
            "playerStrategyDirective",
            "reputation",
            "reputationDescription"
        };

    private static readonly HashSet<string> RelationsKeys =
        new(StringComparer.Ordinal)
        {
            "entries"
        };

    private static readonly HashSet<string> RelationEntryKeys =
        new(StringComparer.Ordinal)
        {
            "targetFactionId",
            "status",
            "description"
        };

    private static readonly HashSet<string> ProtectedKeys =
        new(StringComparer.Ordinal)
        {
            "factionId",
            "initialId",
            "initialFactionId",
            "isNewFaction",
            "materialization",
            "ranks",
            "branches",
            "structuredBonuses",
            "resources",
            "metaResources",
            "strategicGoods",
            "activeProjects",
            "completedProjects",
            "customStates",
            "scribeChronicle",
            "chronicle",
            "controlledTerritories",
            "territories",
            "factionControl",
            "locationControl",
            "NPCFactionAffiliationChanges",
            "npcFactionAffiliations",
            "factionAffiliations"
        };

    private static readonly Regex FactionColorRegex =
        new(
            "^#[0-9A-Fa-f]{6}$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

    internal sealed record Authority(
        HashSet<string> KnownFactionIds,
        HashSet<string> KnownMortalNpcIds);

    internal sealed record ChangePlan(
        string FactionId,
        JsonObject Command);

    internal sealed class Evaluation
    {
        internal bool HasCommand { get; init; }
        internal List<ValidationIssue> Issues { get; } = [];
        internal List<ChangePlan> Plans { get; } = [];
        internal bool CanApply =>
            HasCommand && Issues.Count == 0 && Plans.Count > 0;
    }

    private sealed record FactionReference(
        string Section,
        int Index,
        string? FactionId,
        JsonObject Faction)
    {
        internal string Path =>
            $"{FactionCorePath}.{Section}[{Index}]";
    }

    internal static Evaluation Evaluate(
        JsonObject currentRoot,
        JsonObject preTurnRoot,
        Authority authority)
    {
        var evaluation = new Evaluation
        {
            HasCommand = HasCommandLikeProperty(currentRoot)
        };
        var currentFactions = CollectFactions(currentRoot);
        var preTurnFactions = CollectFactions(
            preTurnRoot,
            includeFullCarrier: false);
        ValidateDirectFullResendBypass(
            currentRoot,
            preTurnFactions,
            evaluation.Issues);
        ValidateDuplicateEffectiveIdentities(
            currentFactions,
            evaluation.Issues);
        evaluation.Issues.AddRange(
            ValidateCommandTopLevelNames(currentRoot));
        if (evaluation.Issues.Any(issue =>
                issue.Code ==
                "faction_core_changes_invalid_top_level_name"))
        {
            return evaluation;
        }

        ValidateCommands(
            currentRoot,
            currentFactions,
            preTurnFactions,
            authority,
            evaluation);
        return evaluation;
    }

    internal static bool HasCommandLikeProperty(JsonObject root) =>
        root.Any(property =>
            string.Equals(
                property.Key,
                PropertyName,
                StringComparison.OrdinalIgnoreCase));

    internal static IReadOnlyList<ValidationIssue>
        ValidateCommandTopLevelNames(JsonObject root) =>
        root
            .Where(property =>
                string.Equals(
                    property.Key,
                    PropertyName,
                    StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(
                    property.Key,
                    PropertyName,
                    StringComparison.Ordinal))
            .Select(property => Error(
                $"{FactionCorePath}.{property.Key}",
                "faction_core_changes_invalid_top_level_name",
                $"The command name must be exactly {PropertyName}."))
            .ToList();

    internal static void Apply(
        JsonObject result,
        Evaluation evaluation)
    {
        if (!evaluation.CanApply)
            return;

        foreach (var plan in evaluation.Plans)
        {
            var target = FindFaction(result, plan.FactionId);
            if (target != null)
                ApplyCommand(target, plan.Command);
        }

        result.Remove(PropertyName);
    }

    private static void ValidateCommands(
        JsonObject currentRoot,
        IReadOnlyList<FactionReference> currentFactions,
        IReadOnlyList<FactionReference> preTurnFactions,
        Authority authority,
        Evaluation evaluation)
    {
        if (!evaluation.HasCommand)
            return;

        if (currentRoot[PropertyName] is not JsonArray commands)
        {
            evaluation.Issues.Add(Error(
                $"{FactionCorePath}.{PropertyName}",
                "faction_core_changes_invalid_shape",
                "factionCoreChanges must be an array of closed absolute faction changes."));
            return;
        }

        if (commands.Count == 0)
        {
            evaluation.Issues.Add(Error(
                $"{FactionCorePath}.{PropertyName}",
                "faction_core_changes_empty_mutation",
                "factionCoreChanges must contain at least one change entry."));
            return;
        }

        var knownFactionIds = new HashSet<string>(
            authority.KnownFactionIds,
            StringComparer.Ordinal);
        knownFactionIds.UnionWith(
            currentFactions
                .Select(reference => reference.FactionId)
                .Where(id => id != null)
                .Cast<string>());
        knownFactionIds.UnionWith(
            preTurnFactions
                .Select(reference => reference.FactionId)
                .Where(id => id != null)
                .Cast<string>());

        var seenTargets = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < commands.Count; index++)
        {
            var context =
                $"{FactionCorePath}.{PropertyName}[{index}]";
            if (commands[index] is not JsonObject command)
            {
                evaluation.Issues.Add(Error(
                    context,
                    "faction_core_changes_invalid_shape",
                    "Each factionCoreChanges entry must be an object."));
                continue;
            }

            var factionId = ReadRequiredString(command, "factionId");
            var issueStart = evaluation.Issues.Count;
            ValidateMembers(
                command,
                CommandKeys,
                context,
                factionId,
                evaluation.Issues);

            if (factionId == null)
            {
                evaluation.Issues.Add(Error(
                    $"{context}.factionId",
                    "faction_core_changes_invalid_identity",
                    "factionCoreChanges requires one exact permanent factionId."));
            }
            else if (!seenTargets.Add(factionId))
            {
                evaluation.Issues.Add(Error(
                    $"{context}.factionId",
                    "faction_core_changes_duplicate_target",
                    "Only one factionCoreChanges entry may target a faction.",
                    factionId));
            }

            if (factionId != null)
            {
                ValidateTarget(
                    factionId,
                    context,
                    currentFactions,
                    preTurnFactions,
                    evaluation.Issues);
            }

            if (ReadRequiredString(command, "reason") == null)
            {
                evaluation.Issues.Add(Error(
                    $"{context}.reason",
                    "faction_core_changes_reason_required",
                    "factionCoreChanges requires a non-empty authored reason.",
                    factionId));
            }

            var suppliedGroups = GroupKeys
                .Where(command.ContainsKey)
                .ToArray();
            if (suppliedGroups.Length == 0)
            {
                evaluation.Issues.Add(Error(
                    context,
                    "faction_core_changes_empty_mutation",
                    "factionCoreChanges requires at least one complete group.",
                    factionId));
            }

            foreach (var groupName in suppliedGroups)
            {
                var groupContext = $"{context}.{groupName}";
                switch (groupName)
                {
                    case "profile":
                        ValidateProfile(
                            command[groupName],
                            groupContext,
                            factionId,
                            evaluation.Issues);
                        break;
                    case "purposeAndPrinciples":
                        ValidatePurposeAndPrinciples(
                            command[groupName],
                            groupContext,
                            factionId,
                            evaluation.Issues);
                        break;
                    case "progressionAndPower":
                        ValidateProgressionAndPower(
                            command[groupName],
                            groupContext,
                            factionId,
                            evaluation.Issues);
                        break;
                    case "governanceAndLeadership":
                        ValidateGovernanceAndLeadership(
                            command[groupName],
                            authority,
                            groupContext,
                            factionId,
                            evaluation.Issues);
                        break;
                    case "playerMembership":
                        ValidatePlayerMembership(
                            command[groupName],
                            groupContext,
                            factionId,
                            evaluation.Issues);
                        break;
                    case "relations":
                        ValidateRelations(
                            command[groupName],
                            knownFactionIds,
                            groupContext,
                            factionId,
                            evaluation.Issues);
                        break;
                }
            }

            if (factionId != null &&
                evaluation.Issues.Count == issueStart)
            {
                evaluation.Plans.Add(new ChangePlan(
                    factionId,
                    command.DeepClone().AsObject()));
            }
        }
    }

    private static void ValidateTarget(
        string factionId,
        string context,
        IReadOnlyList<FactionReference> currentFactions,
        IReadOnlyList<FactionReference> preTurnFactions,
        List<ValidationIssue> issues)
    {
        var exactCurrent = currentFactions
            .Where(reference => string.Equals(
                reference.FactionId,
                factionId,
                StringComparison.Ordinal))
            .ToList();
        var exactPreTurn = preTurnFactions
            .Where(reference => string.Equals(
                reference.FactionId,
                factionId,
                StringComparison.Ordinal))
            .ToList();
        var hasCaseVariant =
            currentFactions.Concat(preTurnFactions).Any(reference =>
                reference.FactionId != null &&
                !string.Equals(
                    reference.FactionId,
                    factionId,
                    StringComparison.Ordinal) &&
                string.Equals(
                    reference.FactionId,
                    factionId,
                    StringComparison.OrdinalIgnoreCase));

        if (exactCurrent.Count == 0 || exactPreTurn.Count == 0)
        {
            issues.Add(Error(
                $"{context}.factionId",
                hasCaseVariant
                    ? "faction_core_changes_target_not_exact"
                    : "faction_core_changes_target_not_existing",
                hasCaseVariant
                    ? "factionId must match exact ordinal Mortal faction authority."
                    : "factionCoreChanges may target only a pre-turn permanent Mortal faction that remains in the current effective core.",
                factionId));
            return;
        }

        if (exactCurrent.Count > 1 || exactPreTurn.Count > 1)
        {
            issues.Add(Error(
                $"{context}.factionId",
                "faction_core_changes_ambiguous_target",
                "Duplicate exact faction identities make the command target ambiguous.",
                factionId));
            return;
        }

        var current = exactCurrent[0];
        var previous = exactPreTurn[0];
        if (HasCompleteMortalReceipt(previous.Faction, factionId))
        {
            if (!HasCompleteMortalReceipt(current.Faction, factionId))
            {
                issues.Add(Error(
                    $"{context}.factionId",
                    "faction_core_changes_target_not_materialized",
                    "The current target must preserve its complete Mortal materialization receipt.",
                    factionId));
            }

            return;
        }

        var sameTurnPromotion =
            string.Equals(
                current.Section,
                "factionDataChanges",
                StringComparison.Ordinal) &&
            HasCompleteMortalReceipt(current.Faction, factionId);
        if (!sameTurnPromotion)
        {
            issues.Add(Error(
                $"{context}.factionId",
                "faction_core_changes_target_not_materialized",
                "A legacy target must be completely promoted through factionDataChanges in the same turn before a narrow command may apply.",
                factionId));
        }
    }

    private static void ValidateDirectFullResendBypass(
        JsonObject currentRoot,
        IReadOnlyList<FactionReference> preTurnFactions,
        List<ValidationIssue> issues)
    {
        if (currentRoot["factionDataChanges"] is not JsonArray changes)
            return;

        for (var index = 0; index < changes.Count; index++)
        {
            if (changes[index] is not JsonObject change)
                continue;
            var factionId = ReadRequiredString(change, "factionId");
            if (factionId == null)
                continue;
            if (!preTurnFactions.Any(reference =>
                    string.Equals(
                        reference.FactionId,
                        factionId,
                        StringComparison.Ordinal) &&
                    HasCompleteMortalReceipt(
                        reference.Faction,
                        factionId)))
            {
                continue;
            }

            issues.Add(Error(
                $"{FactionCorePath}.factionDataChanges[{index}]",
                "faction_existing_full_resend_forbidden",
                "An already materialized Mortal faction cannot be resent through the full factionDataChanges carrier.",
                factionId));
        }
    }

    private static void ValidateDuplicateEffectiveIdentities(
        IReadOnlyList<FactionReference> currentFactions,
        List<ValidationIssue> issues)
    {
        foreach (var group in currentFactions
                     .Where(reference => reference.FactionId != null)
                     .GroupBy(
                         reference => reference.FactionId!,
                         StringComparer.Ordinal)
                     .Where(group => group.Count() > 1))
        {
            issues.Add(Error(
                group.Skip(1).First().Path,
                "faction_core_changes_duplicate_effective_identity",
                "Current faction core carriers contain a duplicate exact effective factionId.",
                group.Key));
        }
    }

    private static void ValidateProfile(
        JsonNode? node,
        string context,
        string? factionId,
        List<ValidationIssue> issues)
    {
        const string code = "faction_core_changes_profile_invalid";
        if (!TryRequireCompleteObject(
                node,
                ProfileKeys,
                context,
                factionId,
                code,
                issues,
                out var profile))
        {
            return;
        }

        foreach (var field in new[]
                 {
                     "name",
                     "description",
                     "image_prompt",
                     "factionColor"
                 })
        {
            if (ReadRequiredString(profile, field) == null)
            {
                issues.Add(Error(
                    $"{context}.{field}",
                    code,
                    $"{field} must be a non-empty absolute string.",
                    factionId));
            }
        }

        var imagePrompt = ReadRequiredString(profile, "image_prompt");
        if (imagePrompt != null &&
            (imagePrompt.Length > 150 ||
             imagePrompt.Any(character =>
                 character is >= '\u0400' and <= '\u052f')))
        {
            issues.Add(Error(
                $"{context}.image_prompt",
                code,
                "image_prompt must be production-valid English text no longer than 150 characters.",
                factionId));
        }

        var color = ReadRequiredString(profile, "factionColor");
        if (color != null && !FactionColorRegex.IsMatch(color))
        {
            issues.Add(Error(
                $"{context}.factionColor",
                code,
                "factionColor must be an exact #RRGGBB value.",
                factionId));
        }
    }

    private static void ValidatePurposeAndPrinciples(
        JsonNode? node,
        string context,
        string? factionId,
        List<ValidationIssue> issues)
    {
        const string code =
            "faction_core_changes_purpose_and_principles_invalid";
        if (!TryRequireCompleteObject(
                node,
                PurposeAndPrinciplesKeys,
                context,
                factionId,
                code,
                issues,
                out var group))
        {
            return;
        }

        foreach (var field in new[] { "purpose", "currentAgenda" })
        {
            if (ReadRequiredString(group, field) == null)
            {
                issues.Add(Error(
                    $"{context}.{field}",
                    code,
                    $"{field} must be a non-empty absolute string.",
                    factionId));
            }
        }

        if (group["principles"] is not JsonArray principles ||
            principles.Count == 0)
        {
            issues.Add(Error(
                $"{context}.principles",
                code,
                "principles must contain one or more unique non-empty strings.",
                factionId));
            return;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < principles.Count; index++)
        {
            var principle = ReadString(principles[index]);
            if (principle == null || !seen.Add(principle))
            {
                issues.Add(Error(
                    $"{context}.principles[{index}]",
                    code,
                    "principles must contain unique non-empty strings.",
                    factionId));
            }
        }
    }

    private static void ValidateProgressionAndPower(
        JsonNode? node,
        string context,
        string? factionId,
        List<ValidationIssue> issues)
    {
        const string code =
            "faction_core_changes_progression_and_power_invalid";
        if (!TryRequireCompleteObject(
                node,
                ProgressionAndPowerKeys,
                context,
                factionId,
                code,
                issues,
                out var group))
        {
            return;
        }

        foreach (var field in new[]
                 {
                     "level",
                     "experience",
                     "experienceForNextLevel"
                 })
        {
            if (!TryReadNonNegativeInteger(group[field], out _))
            {
                issues.Add(Error(
                    $"{context}.{field}",
                    code,
                    $"{field} must be a non-negative integer.",
                    factionId));
            }
        }

        if (ReadRequiredString(group, "developmentArchetype") == null)
        {
            issues.Add(Error(
                $"{context}.developmentArchetype",
                code,
                "developmentArchetype must be a supported non-empty authored value.",
                factionId));
        }

        if (group["customArchetypePriorities"] is JsonObject priorities)
        {
            if (TryRequireCompleteObject(
                    priorities,
                    CustomArchetypePriorityKeys,
                    $"{context}.customArchetypePriorities",
                    factionId,
                    code,
                    issues,
                    out var completePriorities))
            {
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var priority in CustomArchetypePriorityKeys)
                {
                    var value = ReadRequiredString(
                        completePriorities,
                        priority);
                    if (value == null ||
                        !PowerProfileKeys.Contains(value) ||
                        !seen.Add(value))
                    {
                        issues.Add(Error(
                            $"{context}.customArchetypePriorities.{priority}",
                            code,
                            "Custom archetype priorities must be unique exact power-profile scale names.",
                            factionId));
                    }
                }
            }
        }
        else if (group["customArchetypePriorities"] != null)
        {
            issues.Add(Error(
                $"{context}.customArchetypePriorities",
                code,
                "customArchetypePriorities must be explicit null or one complete priority object.",
                factionId));
        }

        if (!TryRequireCompleteObject(
                group["powerProfile"],
                PowerProfileKeys,
                $"{context}.powerProfile",
                factionId,
                code,
                issues,
                out var powerProfile))
        {
            return;
        }

        foreach (var scale in PowerProfileKeys)
        {
            if (!TryReadNonNegativeInteger(powerProfile[scale], out _))
            {
                issues.Add(Error(
                    $"{context}.powerProfile.{scale}",
                    code,
                    "Every powerProfile scale must be a non-negative integer.",
                    factionId));
            }
        }
    }

    private static void ValidateGovernanceAndLeadership(
        JsonNode? node,
        Authority authority,
        string context,
        string? factionId,
        List<ValidationIssue> issues)
    {
        const string code =
            "faction_core_changes_governance_and_leadership_invalid";
        if (!TryRequireCompleteObject(
                node,
                GovernanceAndLeadershipKeys,
                context,
                factionId,
                code,
                issues,
                out var group))
        {
            return;
        }

        if (TryRequireCompleteObject(
                group["governance"],
                GovernanceKeys,
                $"{context}.governance",
                factionId,
                code,
                issues,
                out var governance))
        {
            foreach (var field in GovernanceKeys)
            {
                if (ReadRequiredString(governance, field) == null)
                {
                    issues.Add(Error(
                        $"{context}.governance.{field}",
                        code,
                        $"governance.{field} must be a non-empty string.",
                        factionId));
                }
            }
        }

        if (!TryRequireCompleteObject(
                group["leadership"],
                LeadershipKeys,
                $"{context}.leadership",
                factionId,
                code,
                issues,
                out var leadership))
        {
            return;
        }

        var state = ReadRequiredString(
            leadership,
            "leadershipState");
        if (state == null || !LeadershipStates.Contains(state))
        {
            issues.Add(Error(
                $"{context}.leadership.leadershipState",
                code,
                "leadershipState must be headed, collective, or vacant.",
                factionId));
        }

        if (ReadRequiredString(leadership, "summary") == null)
        {
            issues.Add(Error(
                $"{context}.leadership.summary",
                code,
                "leadership.summary must be a non-empty string.",
                factionId));
        }

        if (leadership["leaderNpcIds"] is not JsonArray leaderIds)
        {
            issues.Add(Error(
                $"{context}.leadership.leaderNpcIds",
                code,
                "leaderNpcIds must be an explicit array.",
                factionId));
            return;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var validCount = 0;
        for (var index = 0; index < leaderIds.Count; index++)
        {
            var leaderId = ReadString(leaderIds[index]);
            if (leaderId == null ||
                !seen.Add(leaderId) ||
                !authority.KnownMortalNpcIds.Contains(leaderId))
            {
                issues.Add(Error(
                    $"{context}.leadership.leaderNpcIds[{index}]",
                    code,
                    "Each leaderNpcId must be one unique exact known Mortal NPC ID.",
                    factionId));
            }
            else
            {
                validCount++;
            }
        }

        if ((string.Equals(
                 state,
                 "vacant",
                 StringComparison.Ordinal) &&
             validCount != 0) ||
            (string.Equals(
                 state,
                 "headed",
                 StringComparison.Ordinal) &&
             validCount == 0))
        {
            issues.Add(Error(
                $"{context}.leadership.leaderNpcIds",
                code,
                "Vacant leadership requires no leaders; headed leadership requires at least one.",
                factionId));
        }
    }

    private static void ValidatePlayerMembership(
        JsonNode? node,
        string context,
        string? factionId,
        List<ValidationIssue> issues)
    {
        const string code =
            "faction_core_changes_player_membership_invalid";
        if (!TryRequireCompleteObject(
                node,
                PlayerMembershipKeys,
                context,
                factionId,
                code,
                issues,
                out var group))
        {
            return;
        }

        foreach (var field in new[]
                 {
                     "isPlayerFaction",
                     "isPlayerMember"
                 })
        {
            if (!TryReadBoolean(group[field], out _))
            {
                issues.Add(Error(
                    $"{context}.{field}",
                    code,
                    $"{field} must be boolean.",
                    factionId));
            }
        }

        foreach (var field in new[]
                 {
                     "playerRank",
                     "playerBranch",
                     "playerStrategyDirective",
                     "reputationDescription"
                 })
        {
            if (!IsExplicitNullableString(group, field))
            {
                issues.Add(Error(
                    $"{context}.{field}",
                    code,
                    $"{field} must be explicit null or a non-empty string.",
                    factionId));
            }
        }

        if (!TryReadInteger(group["reputation"], out _))
        {
            issues.Add(Error(
                $"{context}.reputation",
                code,
                "reputation must be an absolute integer.",
                factionId));
        }
    }

    private static void ValidateRelations(
        JsonNode? node,
        IReadOnlySet<string> knownFactionIds,
        string context,
        string? factionId,
        List<ValidationIssue> issues)
    {
        const string code =
            "faction_core_changes_relations_invalid";
        if (!TryRequireCompleteObject(
                node,
                RelationsKeys,
                context,
                factionId,
                code,
                issues,
                out var group))
        {
            return;
        }

        if (group["entries"] is not JsonArray entries)
        {
            issues.Add(Error(
                $"{context}.entries",
                code,
                "relations.entries must be one complete absolute array.",
                factionId));
            return;
        }

        var seenTargets = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < entries.Count; index++)
        {
            var entryContext = $"{context}.entries[{index}]";
            if (!TryRequireCompleteObject(
                    entries[index],
                    RelationEntryKeys,
                    entryContext,
                    factionId,
                    code,
                    issues,
                    out var relation))
            {
                continue;
            }

            var targetFactionId = ReadRequiredString(
                relation,
                "targetFactionId");
            if (targetFactionId == null ||
                !knownFactionIds.Contains(targetFactionId) ||
                string.Equals(
                    targetFactionId,
                    factionId,
                    StringComparison.Ordinal) ||
                !seenTargets.Add(targetFactionId))
            {
                issues.Add(Error(
                    $"{entryContext}.targetFactionId",
                    code,
                    "Each relation target must be one unique, exact, different known factionId.",
                    factionId));
            }

            foreach (var field in new[] { "status", "description" })
            {
                if (ReadRequiredString(relation, field) == null)
                {
                    issues.Add(Error(
                        $"{entryContext}.{field}",
                        code,
                        $"Relation {field} must be a non-empty string.",
                        factionId));
                }
            }
        }
    }

    private static bool TryRequireCompleteObject(
        JsonNode? node,
        HashSet<string> requiredKeys,
        string context,
        string? factionId,
        string invalidCode,
        List<ValidationIssue> issues,
        out JsonObject value)
    {
        value = null!;
        if (node is not JsonObject candidate)
        {
            issues.Add(Error(
                context,
                invalidCode,
                "The supplied group must be one complete object.",
                factionId));
            return false;
        }

        value = candidate;
        ValidateMembers(
            candidate,
            requiredKeys,
            context,
            factionId,
            issues);
        foreach (var required in requiredKeys)
        {
            if (!candidate.ContainsKey(required))
            {
                issues.Add(Error(
                    $"{context}.{required}",
                    invalidCode,
                    $"Complete group member {required} is required.",
                    factionId));
            }
        }

        return true;
    }

    private static void ValidateMembers(
        JsonObject value,
        HashSet<string> allowed,
        string context,
        string? factionId,
        List<ValidationIssue> issues)
    {
        foreach (var property in value)
        {
            if (allowed.Contains(property.Key))
                continue;
            var isProtected = ProtectedKeys.Contains(property.Key);
            issues.Add(Error(
                $"{context}.{property.Key}",
                isProtected
                    ? "faction_core_changes_protected_member"
                    : "faction_core_changes_unknown_member",
                isProtected
                    ? $"Protected faction authority cannot appear in factionCoreChanges: {property.Key}."
                    : $"Unknown factionCoreChanges member: {property.Key}.",
                factionId));
        }
    }

    private static void ApplyCommand(
        JsonObject faction,
        JsonObject command)
    {
        if (command["profile"] is JsonObject profile)
            CopyFields(faction, profile, ProfileKeys);
        if (command["purposeAndPrinciples"] is JsonObject purpose)
            CopyFields(faction, purpose, PurposeAndPrinciplesKeys);
        if (command["progressionAndPower"] is JsonObject progression)
            CopyFields(faction, progression, ProgressionAndPowerKeys);
        if (command["governanceAndLeadership"] is JsonObject governance)
        {
            faction["governance"] =
                governance["governance"]?.DeepClone();
            faction["leadership"] =
                governance["leadership"]?.DeepClone();
        }

        if (command["playerMembership"] is JsonObject membership)
            CopyFields(faction, membership, PlayerMembershipKeys);
        if (command["relations"] is JsonObject relations)
            faction["relations"] = relations["entries"]?.DeepClone();
    }

    private static void CopyFields(
        JsonObject target,
        JsonObject source,
        IEnumerable<string> fields)
    {
        foreach (var field in fields)
        {
            if (source.ContainsKey(field))
                target[field] = source[field]?.DeepClone();
        }
    }

    private static JsonObject? FindFaction(
        JsonObject root,
        string factionId)
    {
        foreach (var section in new[] { "factions", "factionDataChanges" })
        {
            if (root[section] is not JsonArray factions)
                continue;
            foreach (var faction in factions.OfType<JsonObject>())
            {
                if (string.Equals(
                        ReadRequiredString(faction, "factionId"),
                        factionId,
                        StringComparison.Ordinal))
                {
                    return faction;
                }
            }
        }

        return null;
    }

    private static IReadOnlyList<FactionReference> CollectFactions(
        JsonObject root,
        bool includeFullCarrier = true)
    {
        var result = new List<FactionReference>();
        foreach (var section in includeFullCarrier
                     ? new[] { "factions", "factionDataChanges" }
                     : new[] { "factions" })
        {
            if (root[section] is not JsonArray factions)
                continue;
            for (var index = 0; index < factions.Count; index++)
            {
                if (factions[index] is not JsonObject faction)
                    continue;
                result.Add(new FactionReference(
                    section,
                    index,
                    ReadRequiredString(faction, "factionId"),
                    faction));
            }
        }

        return result;
    }

    private static bool HasCompleteMortalReceipt(
        JsonObject faction,
        string factionId) =>
        faction["materialization"] is JsonObject materialization &&
        ReadRequiredString(materialization, "factionType") ==
        "mortal_faction" &&
        ReadRequiredString(materialization, "factionId") == factionId &&
        ReadRequiredString(materialization, "state") == "complete";

    private static bool IsExplicitNullableString(
        JsonObject value,
        string propertyName) =>
        value.ContainsKey(propertyName) &&
        (value[propertyName] == null ||
         ReadRequiredString(value, propertyName) != null);

    private static string? ReadRequiredString(
        JsonObject value,
        string propertyName) =>
        ReadString(value[propertyName]);

    private static string? ReadString(JsonNode? node)
    {
        if (node is not JsonValue value ||
            !value.TryGetValue<string>(out var result) ||
            string.IsNullOrWhiteSpace(result))
        {
            return null;
        }

        return result;
    }

    private static bool TryReadNonNegativeInteger(
        JsonNode? node,
        out int value)
    {
        value = 0;
        return node is JsonValue jsonValue &&
               jsonValue.TryGetValue<int>(out value) &&
               value >= 0;
    }

    private static bool TryReadInteger(
        JsonNode? node,
        out int value)
    {
        value = 0;
        return node is JsonValue jsonValue &&
               jsonValue.TryGetValue<int>(out value);
    }

    private static bool TryReadBoolean(
        JsonNode? node,
        out bool value)
    {
        value = false;
        return node is JsonValue jsonValue &&
               jsonValue.TryGetValue<bool>(out value);
    }

    private static ValidationIssue Error(
        string path,
        string code,
        string message,
        string? factionId = null) =>
        new(
            path,
            IssueSeverity.Error,
            message,
            code: code,
            actor: factionId == null
                ? null
                : $"mortal_faction:{factionId}",
            section: "FactionCoreChanges",
            repairHint:
            "Use one exact permanent Mortal factionId and only complete absolute factionCoreChanges groups; preserve every unrelated field and the historical materialization receipt.");
}
