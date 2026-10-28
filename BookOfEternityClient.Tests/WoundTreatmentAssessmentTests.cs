using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>Known-wound assessment is readable even when no treatment can be selected.</summary>
public sealed class WoundTreatmentAssessmentTests
{
    [Theory]
    [InlineData("chaos_sea", 0)]
    [InlineData("chaos_sea", 1)]
    [InlineData("shining_abode", 0)]
    [InlineData("shining_abode", 1)]
    public void InsufficientArtShowsActualAssessmentWithoutOfferingTreatment(string realm, int tier)
    {
        var context = Context(realm, tier);
        var before = Snapshot(context);
        var state = WoundApplicationService.CreateState("generation", context);
        var choice = Assert.Single(state.Choices);
        Assert.False(choice.Available);
        Assert.Equal(tier, choice.HealingTier);
        Assert.Throws<InvalidDataException>(() => state.Resolve(choice.Value));
        var result = ExplorerWoundCommandResultBuilder.Build("/лечить", context.Projection, state, true);
        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.Empty(result.Prompts);
        Assert.Null(result.InteractiveSession);
        var visible = Text(result.Blocks);
        Assert.Contains($"Исцеление: {tier}", visible);
        Assert.Contains("II", visible);
        Assert.Contains("ступень 2", visible);
        Assert.Contains("только оценка", visible);
        Assert.Equal(before, Snapshot(context));
        Assert.DoesNotContain("wound_spiritual_test", JsonSerializer.Serialize(result));
    }

    [Theory]
    [InlineData("chaos_sea", 2)]
    [InlineData("shining_abode", 5)]
    public void SufficientArtKeepsConfirmationAndExistingSafeCycleTerms(string realm, int tier)
    {
        var context = Context(realm, tier);
        var state = WoundApplicationService.CreateState("generation", context);
        var choice = Assert.Single(state.Choices);
        Assert.True(choice.Available);
        Assert.Same(choice, state.Resolve(choice.Value));
        var result = ExplorerWoundCommandResultBuilder.Build("/лечить", context.Projection, state, true);
        Assert.Equal(CommandExecutionState.RequiresInput, result.State);
        var option = Assert.Single(Assert.IsType<UiSelectionPrompt>(result.Prompts[0]).Options);
        Assert.False(option.Disabled);
        Assert.Contains("Без затрат ОД", option.Description);
        Assert.Contains("один обычный цикл мира даже при неудаче", option.Description);
        Assert.IsType<UiConfirmationPrompt>(result.Prompts[1]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InvalidOrMismatchingSourceIsNotReportedAsZero(bool mismatch)
    {
        var context = Context("chaos_sea", 0);
        var arts = context.Input.Carriers.AfterlifeProfiles!["profiles"]![0]!["standardArts"]!.AsObject();
        if (mismatch) arts["spiritual_healing"] = 1;
        else arts.Remove("spiritual_healing");
        var state = WoundApplicationService.CreateState("generation", context);
        var choice = Assert.Single(state.Choices);
        Assert.False(choice.Available);
        Assert.Null((int?)choice.HealingTier);
        var result = ExplorerWoundCommandResultBuilder.Build("/лечить", context.Projection, state, true);
        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.DoesNotContain("Исцеление: 0", Text(result.Blocks));
        Assert.DoesNotContain("только оценка", Text(result.Blocks));
        Assert.Throws<InvalidDataException>(() => state.Resolve(choice.Value));
    }

    [Fact]
    public void MixedMenuRetainsReadableDisabledAssessmentAndAvailableChoice()
    {
        var context = Context("chaos_sea", 0);
        var disabled = Assert.Single(WoundApplicationService.CreateState("generation", context).Choices);
        var aid = disabled with { Value = "aid", Available = true, HealingTier = 3,
            Name = "Помощь Хранителя", Reason = "Подтвердите добровольную помощь." };
        var state = new WoundCommandState("generation", [disabled, aid]);
        var result = ExplorerWoundCommandResultBuilder.Build("/лечить", context.Projection, state, true);
        Assert.Equal(CommandExecutionState.RequiresInput, result.State);
        Assert.Contains("Исцеление: 0", Text(result.Blocks));
        var options = Assert.IsType<UiSelectionPrompt>(result.Prompts[0]).Options;
        Assert.True(options[0].Disabled);
        Assert.False(options[1].Disabled);
        Assert.Same(aid, state.Resolve("aid"));
        Assert.Throws<InvalidDataException>(() => state.Resolve(disabled.Value));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InspectionDoesNotAssessHiddenWoundsOrOtherRealm(bool otherRealm)
    {
        var context = Context("chaos_sea", 0, hidden: !otherRealm);
        if (otherRealm)
        {
            var input = context.Input with { Realm = "mortal_world" };
            context = context with { Input = input, Projection = WoundPlayerProjection.Build(input) };
        }
        var state = WoundApplicationService.CreateState("generation", context);
        Assert.Empty(state.Choices);
        var result = ExplorerWoundCommandResultBuilder.Build("/лечить", context.Projection, state, true);
        Assert.DoesNotContain("Исцеление: 0", Text(result.Blocks));
        Assert.Empty(result.Prompts);
    }

    private static WoundPlayerReadContext Context(string realm, int tier, bool hidden = false)
    {
        var wound = WoundContractTestData.CreateSpiritualActiveWound(ownerId: "player_soul",
            carrierPath: WoundCarrierCatalog.AfterlifeProfilesPath, realm: realm);
        if (hidden) wound["display"]!["visibility"] = "hidden";
        var input = WoundPlayerProjectionTestData.InputInRealm(realm, wound);
        input.Carriers.AfterlifeProfiles!["profiles"]![0]!["standardArts"] = new JsonObject { ["spiritual_healing"] = tier };
        var soul = new JsonObject { ["currentRealm"] = realm == "chaos_sea" ? "Chaos Sea" : "Shining Abode",
            ["afterlifeCombatProfile"] = new JsonObject { ["artTiers"] = new JsonObject { ["spiritual_healing"] = tier } } };
        var catalog = WoundCarrierCatalog.Build(input.Carriers);
        return new(input, catalog, soul, null, WoundPlayerProjection.Build(input, catalog));
    }

    private static string Snapshot(WoundPlayerReadContext context) =>
        context.Input.Carriers.AfterlifeProfiles!.ToJsonString() + context.Soul.ToJsonString();

    internal static string Text(IEnumerable<UiBlock> blocks) => string.Join("\n", blocks.Select(b => b switch
    {
        UiTextBlock text => text.Text,
        UiPanelBlock panel => panel.Title + "\n" + Text(panel.Blocks),
        _ => ""
    }));
}
