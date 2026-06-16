using System.Text.RegularExpressions;
using BookOfEternityClient.CommandProtocol;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class AfterlifeDrilldownAuditTests
{
    [Fact]
    public void AfterlifeDrilldownAudit_CoversRequiredCategoriesAndReviewFields()
    {
        var audit = ReadAudit();

        var requiredFragments = new[]
        {
            "guardians",
            "abodes",
            "abode power",
            "soul relics",
            "archive candidates",
            "guardian local systems",
            "Shining Abode systems",
            "spiritual conflict",
            "afterlife profile/support surfaces",
            "Severity",
            "Console parity",
            "Browser parity",
            "Player-facing reason",
            "Docs/contract impact",
            "`adequate`",
            "`fixed in #949`",
            "`follow-up required`",
            "`not applicable`"
        };

        var missing = requiredFragments
            .Where(fragment => !audit.Contains(fragment, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.True(
            missing.Length == 0,
            "The #949 afterlife drill-down audit must cover every required category, classification, and review field. Missing: " +
            string.Join(", ", missing));
    }

    [Fact]
    public void AfterlifeDrilldownAudit_CoversCandidateCommandDescriptors()
    {
        var audit = ReadAudit();
        var commandIds = new[]
        {
            "guardians",
            "abodes",
            "abode_power",
            "guardian_projects",
            "guardian_politics",
            "guardian_trade",
            "guardian_social",
            "abode_residents",
            "resident_interaction",
            "resident_transfer",
            "soul_relics",
            "soul_relic_equip",
            "soul_relic_unequip",
            "afterlife_archive",
            "archive_candidates",
            "archive_consultation",
            "archive_project_fuel",
            "shining_abode",
            "shining_politics",
            "shining_faction_founding",
            "shining_faction_realignment",
            "shining_faction_leadership",
            "shining_native_faction_discovery",
            "shining_faction_investment",
            "shining_project_support",
            "shining_project_unsupport",
            "shining_project_retirement",
            "shining_gates_open",
            "shining_gates_select",
            "shining_gates_deselect",
            "shining_gates_reroll",
            "shining_incarnation_prepare",
            "shining_relic_forge",
            "shining_trade",
            "shining_treasury",
            "source_of_light",
            "afterlife_profiles",
            "afterlife_threats",
            "afterlife_chronicles",
            "afterlife_inbox",
            "spiritual_conflict",
            "spiritual_combat_log",
            "spiritual_combat_help",
            "spiritual_arts"
        };

        var missing = commandIds
            .Select(ExplorerCommandCatalog.Require)
            .Where(descriptor =>
                !audit.Contains($"`{descriptor.Id}`", StringComparison.OrdinalIgnoreCase) ||
                !audit.Contains($"`{descriptor.PrimaryAlias}`", StringComparison.OrdinalIgnoreCase))
            .Select(static descriptor => $"{descriptor.Id} ({descriptor.PrimaryAlias})")
            .ToArray();

        Assert.True(
            missing.Length == 0,
            "The #949 afterlife drill-down audit must list every candidate command id and primary alias. Missing: " +
            string.Join(", ", missing));
    }

    [Fact]
    public void AfterlifeDrilldownAudit_FollowUpRowsLinkGitHubIssues()
    {
        var audit = ReadAudit();
        var followUpRows = audit
            .Split('\n')
            .Select(static line => line.Trim())
            .Where(static line => line.StartsWith("|", StringComparison.Ordinal) &&
                                  line.Contains("`follow-up required`", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.NotEmpty(followUpRows);

        var issuePattern = new Regex(
            @"https://github\.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/\d+",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var rowsWithoutIssue = followUpRows
            .Where(row => !issuePattern.IsMatch(row))
            .ToArray();

        Assert.True(
            rowsWithoutIssue.Length == 0,
            "Every #949 audit row classified as follow-up required must link a GitHub follow-up issue. Missing links in rows: " +
            string.Join(Environment.NewLine, rowsWithoutIssue));
    }

    private static string ReadAudit()
    {
        var auditPath = Path.Combine(
            TestRepoPaths.RepoRoot,
            "docs",
            "audits",
            "afterlife-drilldown-audit.md");
        Assert.True(File.Exists(auditPath), $"Missing #949 audit artifact at {auditPath}.");

        return File.ReadAllText(auditPath);
    }
}
