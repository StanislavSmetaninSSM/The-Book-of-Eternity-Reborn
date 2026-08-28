using System.Text.RegularExpressions;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class PromptDocumentationCoverageTests
{
    [Fact]
    public void WoundMaterializationContract_DocumentsStrictConstructorAndAuthority()
    {
        var contract = ReadRepoFile(
            "OtherGuides",
            "Wound_Materialization_Contract.md");

        foreach (var marker in new[]
                 {
                     "wound_constructor_v1",
                     "wound_optional_creation_v1",
                     "wound_guaranteed_creation_v1",
                     "wound_acquisition_narration_v1",
                     "wound_effect_separation_v1",
                     "wound_spiritual_profiles_v1"
                 })
        {
            Assert.Contains(marker, contract, StringComparison.Ordinal);
        }

        foreach (var required in new[]
                 {
                     "woundDecisions",
                     "\"decision\": \"none\"",
                     "\"decision\": \"materialize\"",
                     "woundRef",
                     "acquisitionNarration",
                     "severity I-IV",
                     "maximumSeverity",
                     "guaranteed",
                     "client-owned",
                     "activeEffectDefinitions[]",
                     "effectChanges[]",
                     "Effect removal never heals or deletes the wound",
                     "Death and soul dissipation are separate outcomes",
                     "no migration"
                 })
        {
            Assert.Contains(required, contract, StringComparison.OrdinalIgnoreCase);
        }

        Assert.DoesNotContain("playerWoundChanges", contract, StringComparison.Ordinal);
        Assert.DoesNotContain("NPCWoundChanges", contract, StringComparison.Ordinal);
    }

    [Fact]
    public void WoundRulesAndWorkedExamples_UseOnlyStrictOptionalConstructor()
    {
        var rules = ReadRepoFile("Rules", "Block_5.txt");
        var examples = ReadRepoFile("Examples", "E_Block_5.txt");

        foreach (var required in new[]
                 {
                     "Wound Materialization v1",
                     "Wound_Materialization_Contract.md",
                     "woundDecisions",
                     "opportunityRef",
                     "acquisitionNarration",
                     "ordinary wound is optional",
                     "guaranteed result"
                 })
        {
            Assert.Contains(required, rules, StringComparison.OrdinalIgnoreCase);
        }

        foreach (var contractId in new[]
                 {
                     "wound_optional_creation_v1",
                     "wound_lower_severity_creation_v1",
                     "wound_guaranteed_creation_v1",
                     "wound_rejected_creation_v1"
                 })
        {
            Assert.Contains(contractId, examples, StringComparison.Ordinal);
        }

        foreach (var document in new[] { rules, examples })
        {
            Assert.DoesNotContain("playerWoundChanges", document, StringComparison.Ordinal);
            Assert.DoesNotContain("NPCWoundChanges", document, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void GmDaemon_ForcesWoundContractForExposedOpportunities()
    {
        var daemon = ReadRepoFile(
            "BookOfEternityClient",
            "game_master_daemon.ps1");

        foreach (var required in new[]
                 {
                     "Wound_Materialization_Contract.md",
                     "WoundMaterializationContractPath",
                     "WoundMaterializationDirective",
                     "woundDecisions",
                     "opportunityRef",
                     "acquisitionNarration",
                     "ordinary wound is optional",
                     "guaranteed"
                 })
        {
            Assert.Contains(required, daemon, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void SpiritualWoundProfileDocumentation_MatchesExactRuntimeRegistry()
    {
        var expected = SpiritualWoundEffectProfileCatalog.RegisteredProfiles
            .OrderBy(static profile => profile, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(8, expected.Length);

        foreach (var document in new[]
                 {
                     ReadRepoFile("OtherGuides", "Wound_Materialization_Contract.md"),
                     ReadRepoFile("OtherGuides", "Effect_Materialization_Contract.md")
                 })
        {
            var section = ExtractWoundDocumentationSection(
                document,
                "wound_spiritual_profiles_v1");
            var documented = Regex.Matches(
                    section,
                    "`(spiritual_[a-z_]+)`",
                    RegexOptions.CultureInvariant)
                .Select(static match => match.Groups[1].Value)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(static profile => profile, StringComparer.Ordinal)
                .ToArray();

            Assert.Equal(expected, documented);
            Assert.Contains("profile_specific", section, StringComparison.Ordinal);
            Assert.Contains("operation", section, StringComparison.Ordinal);
            Assert.Contains("axis", section, StringComparison.Ordinal);
            Assert.Contains("magnitude", section, StringComparison.Ordinal);
            Assert.Contains("afterlife_combat_condition", section, StringComparison.Ordinal);
            Assert.Contains("MUST NOT", section, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static string ExtractWoundDocumentationSection(
        string document,
        string marker)
    {
        var markerIndex = document.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(markerIndex >= 0, $"Missing documentation section '{marker}'.");
        var nextHeading = document.IndexOf(
            "\n## ",
            markerIndex + marker.Length,
            StringComparison.Ordinal);
        return nextHeading < 0
            ? document[markerIndex..]
            : document[markerIndex..nextHeading];
    }
}
