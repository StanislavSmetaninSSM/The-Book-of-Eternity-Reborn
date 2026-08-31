using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class MortalWoundDeteriorationPolicyContractTests
{
    private const string WoundPath = "wound";
    private const string PolicyPath = WoundPath + ".recovery.deteriorationPolicy";

    [Theory]
    [InlineData("increase_severity")]
    [InlineData("death_contour")]
    [InlineData("no_change")]
    [InlineData("add_recovery")]
    public void Parse_RecognizesEveryClosedScalarPolicyResult(string kind)
    {
        var wound = WoundContractTestData.CreateActiveWound();
        wound["recovery"]!["deteriorationPolicy"] = Policy(
            new JsonObject { ["kind"] = kind });

        var parsed = WoundMaterializationContract.Parse(
            wound.ToJsonString(),
            WoundPath);

        Assert.True(parsed.IsValid, Describe(parsed.Issues));
        Assert.NotNull(parsed.Wound);
    }

    [Fact]
    public void Parse_RecognizesExistingCompleteArbitraryComplicationDraft()
    {
        var wound = WoundContractTestData.CreateActiveWound();
        wound["recovery"]!["deteriorationPolicy"] = Policy(
            EffectlessComplicationResult());

        var parsed = WoundMaterializationContract.Parse(
            wound.ToJsonString(),
            WoundPath);

        Assert.True(parsed.IsValid, Describe(parsed.Issues));
        Assert.NotNull(parsed.Wound);
    }

    [Theory]
    [InlineData("missing_policy_ref")]
    [InlineData("extra_policy_field")]
    [InlineData("empty_conditions")]
    [InlineData("multiple_conditions")]
    [InlineData("negative_grace")]
    [InlineData("zero_cadence")]
    [InlineData("unknown_result")]
    [InlineData("open_scalar_result")]
    [InlineData("zero_difficulty_complication")]
    public void Parse_RejectsMalformedOrOpenPolicyBeforeAcceptedStateAuthority(
        string mutation)
    {
        var wound = WoundContractTestData.CreateActiveWound();
        var policy = Policy(new JsonObject { ["kind"] = "increase_severity" });
        switch (mutation)
        {
            case "missing_policy_ref":
                policy.Remove("policyRef");
                break;
            case "extra_policy_field":
                policy["callerAuthority"] = "forged";
                break;
            case "empty_conditions":
                policy["unmetConditions"] = new JsonArray();
                break;
            case "multiple_conditions":
                policy["unmetConditions"] = new JsonArray(
                    "not_stabilized",
                    "missing_care");
                break;
            case "negative_grace":
                policy["graceMinutes"] = -1L;
                break;
            case "zero_cadence":
                policy["cadenceMinutes"] = 0L;
                break;
            case "unknown_result":
                policy["result"] = new JsonObject { ["kind"] = "become_better_later" };
                break;
            case "open_scalar_result":
                policy["result"] = new JsonObject
                {
                    ["kind"] = "increase_severity",
                    ["steps"] = 2
                };
                break;
            case "zero_difficulty_complication":
                var result = EffectlessComplicationResult();
                result["complicationDraft"]!["complications"]![0]![
                    "treatmentDifficultyModifier"] = 0;
                policy["result"] = result;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null);
        }
        wound["recovery"]!["deteriorationPolicy"] = policy;

        var parsed = WoundMaterializationContract.Parse(
            wound.ToJsonString(),
            WoundPath);

        Assert.False(parsed.IsValid);
        Assert.Null(parsed.Wound);
        Assert.Contains(parsed.Issues, issue =>
            issue.Code == "mortal_wound_deterioration_policy_invalid" &&
            issue.FilePath.StartsWith(PolicyPath, StringComparison.Ordinal));
    }

    private static JsonObject Policy(JsonObject result) => new()
    {
        ["policyRef"] = "untreated_infection",
        ["unmetConditions"] = new JsonArray("not_stabilized"),
        ["graceMinutes"] = 30L,
        ["cadenceMinutes"] = 10L,
        ["result"] = result
    };

    private static JsonObject EffectlessComplicationResult() => new()
    {
        ["kind"] = "add_complication",
        ["complicationDraft"] = new JsonObject
        {
            ["complications"] = new JsonArray(new JsonObject
            {
                ["complicationRef"] = "deterioration_infection",
                ["kind"] = "infection",
                ["state"] = "active",
                ["displayName"] = "Распространяющееся заражение",
                ["treatmentDifficultyModifier"] = 1,
                ["visibility"] = "known_to_player"
            }),
            ["consequenceDefinitions"] = new JsonArray()
        }
    };

    private static string Describe(IEnumerable<ValidationIssue> issues) =>
        string.Join(" | ", issues.Select(issue =>
            $"{issue.Code}@{issue.FilePath}:{issue.Actual}"));
}
