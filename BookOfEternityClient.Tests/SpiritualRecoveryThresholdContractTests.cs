using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>Detached scalar/graph DATA only; native birth authority is covered by the real C2 journey.</summary>
public sealed class SpiritualRecoveryThresholdContractTests
{
    [Theory]
    [InlineData(1, 2L, 3L)]
    [InlineData(2, 4L, 3L)]
    [InlineData(3, 6L, 3L)]
    [InlineData(3, 6L, 4L)]
    [InlineData(4, 8L, 3L)]
    [InlineData(4, 8L, 6L)]
    public void CanonicalRequiresCurrentRankThresholdAtAdmission(int rank, long expected, long wrong)
    {
        var legal = CreateLegalWound(rank, expected);
        var frozen = legal.ToJsonString();
        var parsed = WoundMaterializationContract.Parse(frozen, "threshold.wound");
        Assert.True(parsed.IsValid, string.Join("\n", parsed.Issues));
        var wound = Assert.IsType<WoundMaterializationEnvelope>(parsed.Wound);
        Assert.Equal(expected, wound.Recovery.CurrentStepThreshold);
        var changed = legal.DeepClone().AsObject();
        changed["recovery"]!["currentStepThreshold"] = wrong;
        var rejected = WoundMaterializationContract.Parse(changed.ToJsonString(), "threshold.wound");
        Assert.False(rejected.IsValid);
        Assert.Null(rejected.Wound);
        AssertMismatch(rejected.Issues, expected, wrong);
        Assert.Equal(frozen, legal.ToJsonString());
        Assert.Equal(expected, wound.Recovery.CurrentStepThreshold);
    }

    [Theory]
    [InlineData(1, 2L, 3L)]
    [InlineData(2, 4L, 3L)]
    [InlineData(3, 6L, 3L)]
    [InlineData(3, 6L, 4L)]
    [InlineData(4, 8L, 3L)]
    [InlineData(4, 8L, 6L)]
    public void SymbolicRequiresCurrentRankThresholdAtAdmission(int rank, long expected, long wrong)
    {
        var legal = CreateLegalWound(rank, expected);
        var frozen = legal.ToJsonString();
        var parsed = WoundMaterializationContract.Parse(frozen, "threshold.wound");
        Assert.True(parsed.IsValid, string.Join("\n", parsed.Issues));
        var wound = Assert.IsType<WoundMaterializationEnvelope>(parsed.Wound);
        var graph = WoundMaterializationContract.ImportOwnedGraph(wound.Complications, wound.Consequences);
        var context = new WoundWorkingGraphContext(wound.WoundId, wound.Lifecycle, wound.Owner,
            wound.Classification, wound.Severity, wound.Care, wound.Recovery,
            wound.Consequences.SlotBudget, wound.Consequences.SlotsUsed);
        var legalIssues = new List<ValidationIssue>();
        Assert.True(WoundMaterializationContract.ValidateWorkingGraphBoundary(context, graph, "threshold.wound", legalIssues));
        Assert.Empty(legalIssues);

        var symbolicIssues = new List<ValidationIssue>();
        Assert.False(WoundMaterializationContract.ValidateWorkingGraphBoundary(
            context with { Recovery = wound.Recovery with { CurrentStepThreshold = wrong } }, graph,
            "threshold.wound", symbolicIssues));
        AssertMismatch(symbolicIssues, expected, wrong);
        Assert.Equal(frozen, legal.ToJsonString());
        Assert.Equal(expected, wound.Recovery.CurrentStepThreshold);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("1.5")]
    [InlineData("\"6\"")]
    [InlineData("9223372036854775808")]
    public void MalformedThresholdKeepsSingleNumericDiagnostic(string value)
    {
        var wound = CreateLegalWound(3, 6);
        wound["recovery"]!["currentStepThreshold"] = JsonNode.Parse(value);
        var result = WoundMaterializationContract.Parse(wound.ToJsonString(), "threshold.wound");
        Assert.False(result.IsValid); Assert.Null(result.Wound);
        var issue = Assert.Single(result.Issues);
        Assert.Equal("threshold.wound.recovery.currentStepThreshold", issue.FilePath);
        Assert.Equal("wound_materialization_invalid_field", issue.Code);
    }

    [Fact]
    public void PhysicalPositiveThresholdAndHealedSpiritualRankOneRemainLegal()
    {
        var physical = WoundContractTestData.CreateActiveWound();
        physical["recovery"]!["currentStepThreshold"] = 97;
        var ordinary = WoundMaterializationContract.Parse(physical.ToJsonString(), "threshold.physical");
        Assert.True(ordinary.IsValid, string.Join("\n", ordinary.Issues));
        Assert.Equal(97L, ordinary.Wound!.Recovery.CurrentStepThreshold);
        var healed = CreateLegalWound(1, 2);
        healed["lifecycle"] = "healed";
        healed["care"]!["state"] = "healed";
        healed["lastTransition"]!["kind"] = "heal";
        healed["lastTransition"]!["ordinal"] = 2;
        healed["lastTransition"]!["turn"] = 43;
        var terminal = WoundMaterializationContract.Parse(healed.ToJsonString(), "threshold.healed");
        Assert.True(terminal.IsValid, string.Join("\n", terminal.Issues));
        Assert.Equal(2L, terminal.Wound!.Recovery.CurrentStepThreshold);
    }

    private static void AssertMismatch(IReadOnlyList<ValidationIssue> issues, long expected, long actual)
    {
        var issue = Assert.Single(issues);
        Assert.Equal("threshold.wound.recovery.currentStepThreshold", issue.FilePath);
        Assert.Equal("wound_spiritual_recovery_threshold_mismatch", issue.Code);
        Assert.Equal(expected.ToString(System.Globalization.CultureInfo.InvariantCulture), issue.Expected);
        Assert.Equal(actual.ToString(System.Globalization.CultureInfo.InvariantCulture), issue.Actual);
    }

    private static JsonObject CreateLegalWound(int rank, long threshold)
    {
        var wound = rank >= 3
            ? SpiritualGuardianAttributionTestData.CreateRetained(rank: rank).Parents.Single().DeepClone().AsObject()
            : WoundContractTestData.CreateSpiritualActiveWound();
        if (rank == 1)
        {
            wound["severity"]!["rank"] = 1;
            wound["severity"]!["value"] = "I";
            wound["severity"]!["maximumAtCreation"] = "I";
            var consequences = wound["consequences"]!;
            consequences["slotBudget"] = 1; consequences["slotsUsed"] = 1;
            consequences["entries"]!.AsArray().RemoveAt(1);
            consequences["ownedEffectSources"]!["definitions"]!.AsArray().RemoveAt(1);
            consequences["ownedEffectSources"]!["rootBindings"]!.AsArray().RemoveAt(1);
        }
        wound["recovery"]!["currentStepThreshold"] = threshold;
        return wound;
    }
}
