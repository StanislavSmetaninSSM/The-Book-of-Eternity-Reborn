using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ShiningHealingAccessDocumentationTests
{
    private const string Marker = "shining_healer_access_view_v1";

    [Fact]
    public void EffectiveGuidanceDeclaresReadOnlyPublicAccessAndNoTreatmentAuthority()
    {
        foreach (var file in new[] { "OtherGuides/Wound_Materialization_Contract.md", "OtherGuides/Afterlife_Contract_Matrix.md",
            "Rules/Block_32_Guardians.txt", "CLI_API_Specification.md", "TaskGuides/CLI_Step_Main.txt" })
        {
            var text = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, file));
            var start = text.IndexOf(Marker + ":", StringComparison.Ordinal); Assert.True(start >= 0);
            var section = text[start..];
            foreach (var token in new[] { "availability", "minimumRelationship", "accessConditions", "compensationKinds",
                "H>=W", "no active treatment button", "historical" }) Assert.Contains(token, section);
        }
        XDocument.Parse(File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "Rules/Block_32_Guardians.txt")));
        var daemon = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient/game_master_daemon.ps1"));
        var assignments = Regex.Matches(daemon, "\\$script:WoundMaterializationDirective = \\\"[^\\\"]*\\\"");
        Assert.Equal(2, assignments.Count); Assert.All(assignments.Cast<Match>(), a => Assert.Contains(Marker, a.Value));
        var entry = Assert.Single(ExampleValidationManifest.Load().EffectMaterializationCoverage, e => e.ContractId == Marker);
        var example = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "Examples", entry.File));
        Assert.All(entry.RequiredText, token => Assert.Contains(token, example));
    }

    [Fact]
    public void WorkedPublicFavorOnlyDeclarationUsesTheActualProjectionWithoutAPriceOrGrant()
    {
        var example = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "Examples/E_CLI_Afterlife_Turns.txt"));
        var start = example.IndexOf(Marker + " — worked", StringComparison.Ordinal); Assert.True(start >= 0);
        var end = example.IndexOf("end_" + Marker, start, StringComparison.Ordinal); Assert.True(end > start);
        var fragments = Regex.Matches(example[start..end], "```json\\s*([\\s\\S]*?)```", RegexOptions.CultureInvariant)
            .Cast<Match>().Select(m => JsonNode.Parse(m.Groups[1].Value)!.AsObject()).ToArray();
        Assert.Equal(2, fragments.Length);
        var actor = AfterlifeActorMaterializationTestFixture.CreateCompleteProfile("resident", "resident_iva", "Shining Abode", 42);
        foreach (var (key, value) in fragments[1])
        {
            if (key == "standardArts") foreach (var (art, tier) in value!.AsObject()) actor[key]![art] = tier!.DeepClone();
            else actor[key] = value!.DeepClone();
        }
        var roster = ShiningFactionHealingSupport.ProjectVisibleRoster(new() { ["factionId"] = "faction_dawn" },
            new() { ["entries"] = new JsonArray(fragments[0]) }, new() { ["profiles"] = new JsonArray(actor) });
        var member = Assert.Single(roster); Assert.Equal(3, member.HealingTier);
        var terms = Assert.IsType<AfterlifeHealingServiceDisplayTerms>(member.DisplayTerms);
        Assert.False(terms.DeclaredAvailable); Assert.Equal(25, terms.MinimumRelationship);
        var lines = ShiningFactionHealingSupport.DescribeAccess(member);
        Assert.Contains("Условие: Согласие Совета", lines);
        Assert.DoesNotContain(lines, line => line.StartsWith("Объявленный тариф", StringComparison.Ordinal));
        Assert.Contains("Лечение через команду этого целителя пока недоступно.", lines);
        Assert.Null(AfterlifeHealingServiceContract.ReadPublicCurrencyProfile(
            new("shining_abode", "resident", "resident_iva", WoundCarrierCatalog.AfterlifeProfilesPath), actor));
    }
}
