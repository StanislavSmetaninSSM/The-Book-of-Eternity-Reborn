using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>Checks the actual GM fragment through the same retained source readers.</summary>
public sealed class SpiritualWoundNoRollGuidanceTests
{
    /// <summary>Preserves explicit choice, zero/missing distinction and unchanged authority boundaries in guidance.</summary>
    [Fact]
    public void WorkedNoRollExchangeUsesActualRetainedValidators()
    {
        const string marker = "spiritual_wound_no_roll_source_v1";
        var entry = Assert.Single(ExampleValidationManifest.Load().EffectMaterializationCoverage,
            row => row.ContractId == marker);
        var example = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "Examples", entry.File));
        Assert.All(entry.RequiredText, text => Assert.Contains(text, example));
        var block = Assert.Single(Regex.Matches(example, "```json\\s*([\\s\\S]*?)```",
            RegexOptions.CultureInvariant).Cast<Match>());
        var exchange = JsonNode.Parse(block.Groups[1].Value)!.AsObject();
        Assert.False(exchange.ContainsKey("diceAudit"));
        Assert.Equal((true, (long?)10), SpiritualWoundNoRollMargin.Read(exchange, "player"));
        var issues = new List<ValidationIssue>();
        ValidationService.ValidateRetainedSpiritualExchangeShape(exchange, "example.exchange", issues);
        ValidationService.ValidateSpiritualWoundSourceActionShape(exchange, "example.exchange", issues);
        ValidationService.ValidateRetainedSpiritualExchangeRules(exchange, issues);
        Assert.Empty(issues);
        var source = SpiritualWoundNoRollSourceTests.Source();
        source["exchangeJsonBase64"] = Convert.ToBase64String(
            Encoding.UTF8.GetBytes(SpiritualWoundStateJson.Canonical(exchange)));
        var digest = SpiritualWoundStateJson.Hash(source, "source", "sourceId", "sourceFingerprint");
        source["sourceFingerprint"] = digest;
        source["sourceId"] = "spiritual_source_" + digest[7..];
        Assert.Equal(1, SpiritualWoundSourceWitness.Validate(source).MaximumSeverityRank);
        exchange[SpiritualWoundNoRollMargin.Property]!["player"] = 0;
        Assert.Equal((true, (long?)0), SpiritualWoundNoRollMargin.Read(exchange, "player"));
        exchange.Remove(SpiritualWoundNoRollMargin.Property);
        Assert.Equal((true, (long?)null), SpiritualWoundNoRollMargin.Read(exchange, "player"));
        foreach (var path in new[] { "OtherGuides/Wound_Materialization_Contract.md",
            "OtherGuides/Afterlife_Contract_Matrix.md", "TaskGuides/CLI_Step_Main.txt", "Examples/E_CLI_Afterlife_Turns.txt" })
            Assert.Contains(marker, File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, path)));
    }
}
