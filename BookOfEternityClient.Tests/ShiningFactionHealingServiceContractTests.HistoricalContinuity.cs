using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class ShiningFactionHealingServiceContractTests
{
    /// <summary>Actual first progression may grow gameplay history without rewriting validated creation evidence.</summary>
    [Fact]
    public void FirstAutomaticIncomePreservesTheEntireHistoricalEnvelope()
    {
        var input = ShiningFactionHealingServiceTestData.Create();
        var original = input.Profiles.ToJsonString();
        var initialActor = input.Profiles["profiles"]![0]!.AsObject();
        var envelope = initialActor["materialization"]!.DeepClone();
        Assert.Empty(ActorMaterializationContract.ValidateAfterlifeProfile(JsonSerializer.SerializeToElement(initialActor),
            "initial.historical.healer", requireEnvelope: true, canTradeEvidence: false));
        Assert.Empty(initialActor["progressionLedger"]!.AsArray());
        Assert.Equal("empty_by_design", envelope["sections"]!["progressionHistory"]!["state"]!.GetValue<string>());
        Assert.Single(ShiningFactionHealingSupport.ProjectCurrentCurrencyServices(input.Shining, input.Residents, input.Profiles));

        var projected = AfterlifeEntityProfileState.ProjectAcceptedRoot(input.Profiles, input.Profiles,
            OneShiningCycle(), null, input.Shining);
        var current = projected["profiles"]![0]!.AsObject();
        var row = Assert.Single(current["progressionLedger"]!.AsArray());
        Assert.Equal("client_auto_strategy", row!["source"]!.GetValue<string>());
        Assert.Equal("shining:1", row["cycleKey"]!.GetValue<string>());
        Assert.Equal(6, current["currencies"]!["inkFeathers"]!.GetValue<int>());
        Assert.Equal(1, current["currencies"]!["lightSparks"]!.GetValue<int>());
        Assert.Equal(original, input.Profiles.ToJsonString());
        Assert.Empty(ActorMaterializationContract.ValidateHistoricalAfterlifeProfile(JsonSerializer.SerializeToElement(current),
            "current.historical.healer"));
        Assert.Single(ShiningFactionHealingSupport.ProjectCurrentCurrencyServices(input.Shining, input.Residents, projected));
        Assert.True(JsonNode.DeepEquals(projected, AfterlifeEntityProfileState.ProjectAcceptedRoot(projected, projected,
            OneShiningCycle(), null, input.Shining)));

        // Final causal oracle: income and admission above are prerequisites;
        // changing this immutable creation envelope caused the actual Engine repair.
        Assert.True(JsonNode.DeepEquals(envelope, current["materialization"]),
            "Client automatic progression rewrote the validated historical materialization envelope.");
    }

    /// <summary>Historical shape is never substituted for strict new-materialization evidence.</summary>
    [Fact]
    public void NewlyAuthoredHistoryCannotClaimAnEmptyCreationDisposition()
    {
        var input = ShiningFactionHealingServiceTestData.Create();
        var actor = input.Profiles["profiles"]![0]!.AsObject();
        actor["progressionLedger"]!.AsArray().Add(new JsonObject { ["entryId"] = "new_authored_history" });
        var issues = ActorMaterializationContract.ValidateAfterlifeProfile(JsonSerializer.SerializeToElement(actor),
            "new.creation.history", requireEnvelope: true, canTradeEvidence: false);
        Assert.Contains(issues, i => i.Code == "actor_materialization_section_empty_surface_invalid");
    }

    /// <summary>Current roster projection still rejects malformed creation evidence before offering a service.</summary>
    [Theory]
    [InlineData("missing")] [InlineData("binding")] [InlineData("reason")]
    [InlineData("foreign")] [InlineData("schema")]
    public void CurrentHistoricalProviderStillRequiresAnExactWellFormedEnvelope(string change)
    {
        var input = ShiningFactionHealingServiceTestData.Create();
        var actor = input.Profiles["profiles"]![0]!.AsObject();
        switch (change)
        {
            case "missing": actor.Remove(ActorMaterializationContract.PropertyName); break;
            case "binding": actor["materialization"]!["actorId"] = "another_actor"; break;
            case "reason": actor["materialization"]!["sections"]!["progressionHistory"]!.AsObject().Remove("reason"); break;
            case "foreign": actor["materialization"]!["sections"]!["progressionHistory"]!["foreignField"] = true; break;
            case "schema": actor["materialization"]!["schemaVersion"] = 99; break;
        }
        var original = input.Profiles.ToJsonString();
        Assert.Empty(ShiningFactionHealingSupport.ProjectCurrentCurrencyServices(input.Shining, input.Residents, input.Profiles));
        Assert.Equal(original, input.Profiles.ToJsonString());
    }

}
