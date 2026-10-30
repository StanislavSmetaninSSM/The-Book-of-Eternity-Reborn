using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class ShiningFactionHealingServiceContractTests
{
    [Fact]
    public void FirstAutomaticIncomeKeepsAnInitiallyEmptyFactionHealerMaterializationComplete()
    {
        var input = ShiningFactionHealingServiceTestData.Create();
        var original = input.Profiles.ToJsonString();
        Assert.Single(ShiningFactionHealingSupport.ProjectCurrentCurrencyServices(input.Shining, input.Residents, input.Profiles));
        var projected = AfterlifeEntityProfileState.ProjectAcceptedRoot(input.Profiles, input.Profiles,
            OneShiningCycle(), null, input.Shining);
        Assert.Equal(original, input.Profiles.ToJsonString());
        var actor = projected["profiles"]![0]!.AsObject();
        var row = Assert.Single(actor["progressionLedger"]!.AsArray());
        Assert.Equal("client_auto_strategy", row!["source"]!.GetValue<string>());
        Assert.Equal("shining:1", row["cycleKey"]!.GetValue<string>());
        Assert.Equal(6, actor["currencies"]!["inkFeathers"]!.GetValue<int>());
        Assert.Equal(1, actor["currencies"]!["lightSparks"]!.GetValue<int>());
        var issues = ActorMaterializationContract.ValidateHistoricalAfterlifeProfile(JsonSerializer.SerializeToElement(actor),
            "actual.client.projected.healer");
        Assert.True(issues.Count == 0, string.Join(";", issues.Select(i => i.Code + ":" + i.FilePath + ":" + i.Actual)));
        Assert.Equal("empty_by_design", actor["materialization"]!["sections"]!["progressionHistory"]!["state"]!.GetValue<string>());
        var expectedEnvelope = input.Profiles["profiles"]![0]!["materialization"]!.DeepClone();
        Assert.True(JsonNode.DeepEquals(expectedEnvelope, actor["materialization"]));
        Assert.Single(ShiningFactionHealingSupport.ProjectCurrentCurrencyServices(input.Shining, input.Residents, projected));
        var repeated = AfterlifeEntityProfileState.ProjectAcceptedRoot(projected, projected, OneShiningCycle(), null, input.Shining);
        Assert.True(JsonNode.DeepEquals(projected, repeated));
    }

    [Theory]
    [InlineData("disabled")] [InlineData("no_report")] [InlineData("invalid_section")]
    [InlineData("missing_reason")] [InlineData("nonempty_history")] [InlineData("missing_ledger")] [InlineData("foreign_field")]
    public void AutomaticIncomeNeverRepairsUnrelatedOrUnprovedHistory(string condition)
    {
        var input = ShiningFactionHealingServiceTestData.Create();
        var actor = input.Profiles["profiles"]![0]!.AsObject();
        var disposition = actor["materialization"]!["sections"]!["progressionHistory"]!.AsObject();
        switch (condition)
        {
            case "disabled": actor["progressionStrategy"]!["autoProgressionEnabled"] = false; break;
            case "invalid_section": disposition["state"] = "pending"; break;
            case "missing_reason": disposition.Remove("reason"); break;
            case "nonempty_history": actor["ledger"]!.AsArray().Add(new JsonObject { ["entryId"] = "existing_unexplained_history" }); break;
            case "missing_ledger": actor.Remove("ledger"); break;
            case "foreign_field": disposition["unownedExtra"] = true; break;
        }
        var before = disposition.ToJsonString();
        var projected = AfterlifeEntityProfileState.ProjectAcceptedRoot(input.Profiles, input.Profiles,
            condition == "no_report" ? null : OneShiningCycle(), null, input.Shining);
        var after = projected["profiles"]![0]!.AsObject();
        Assert.Equal(before, after["materialization"]!["sections"]!["progressionHistory"]!.ToJsonString());
        var issues = ActorMaterializationContract.ValidateAfterlifeProfile(JsonSerializer.SerializeToElement(after),
            "actual.client.history.control", requireEnvelope: true, canTradeEvidence: false);
        if (condition is "disabled" or "no_report")
        {
            Assert.Empty(issues); Assert.Empty(after["progressionLedger"]!.AsArray());
            Assert.Equal(0, after["currencies"]!["inkFeathers"]!.GetValue<int>());
        }
        else Assert.NotEmpty(issues);
    }

    [Fact]
    public void FirstAutomaticChaosIncomeKeepsTheHistoricalMaterializationContract()
    {
        var actor = AfterlifeActorMaterializationTestFixture.CreateCompleteProfile("guardian", "guardian_auto_history", "Chaos Sea", 42);
        var before = actor["materialization"]!.DeepClone();
        var root = new JsonObject { ["profiles"] = new JsonArray(actor) };
        var report = new JsonObject { ["chaosSeaCyclesProcessed"] = 1, ["newLastChaosSeaSimulationOrdinal"] = 1 };
        var projected = AfterlifeEntityProfileState.ProjectAcceptedRoot(root, root, report, null, null);
        var after = projected["profiles"]![0]!.AsObject();
        Assert.Empty(ActorMaterializationContract.ValidateHistoricalAfterlifeProfile(JsonSerializer.SerializeToElement(after),
            "actual.client.chaos.history"));
        Assert.True(JsonNode.DeepEquals(before, after["materialization"]));
        Assert.Equal("chaos:1", Assert.Single(after["progressionLedger"]!.AsArray())!["cycleKey"]!.GetValue<string>());
        Assert.Equal(12, after["currencies"]!["inkFeathers"]!.GetValue<int>());
        Assert.Equal(0, after["currencies"]!["lightSparks"]!.GetValue<int>());
        Assert.Empty(actor["progressionLedger"]!.AsArray());
        Assert.True(JsonNode.DeepEquals(projected, AfterlifeEntityProfileState.ProjectAcceptedRoot(projected, projected, report, null, null)));
    }

    private static JsonObject OneShiningCycle() => new()
    {
        ["shiningAbodeCyclesProcessed"] = 1, ["newLastShiningAbodeCycleOrdinal"] = 1
    };
}
