using System.Text.Json.Nodes;
using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Services;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>Real guided navigation exposes public targets without granting treatment authority.</summary>
public sealed partial class WoundTargetSelectionTests(ITestOutputHelper output)
{
    /// <summary>A reachable visible actor is offered before wound selection even without an aid grant.</summary>
    [Fact]
    public async Task OpenTreatmentOffersSelfAndCurrentVisibleActorBeforeWoundSelectionWithoutGrant()
    {
        // Break caught: filtering target discovery by existing treatment grants,
        // or retaining the old flat wound-only menu, hides a reachable actor.
        var wound = WoundContractTestData.CreateSpiritualActiveWound("wound_target_self", "chaos_sea", "player_soul",
            "player_soul", WoundCarrierCatalog.AfterlifeProfilesPath);
        var input = WoundPlayerProjectionTestData.InputInRealm("chaos_sea", wound);
        input.Carriers.AfterlifeProfiles!["profiles"]![0]!["standardArts"] = new JsonObject { ["spiritual_healing"] = 3 };
        input.Carriers.AfterlifeProfiles["profiles"]!.AsArray().Add(new JsonObject
        {
            ["actorType"] = "guardian", ["actorId"] = "guardian_target_choice", ["realm"] = "Chaos Sea",
            ["displayName"] = "Хранитель выбора", ["locationId"] = "abode_target_choice",
            ["activeWounds"] = new JsonArray(), ["activeEffects"] = new JsonArray()
        });
        await using var fixture = await WoundPlayerReadFixture.CreateAsync(output, realm: "chaos_sea", suppliedInput: input);
        await using (var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync())
        {
            var soul = JsonNode.Parse((await fixture.Files.ReadFileAsync(lease, "game_state/meta/soul_state.json"))!)!.AsObject();
            soul["afterlifeCombatProfile"] = new JsonObject { ["artTiers"] = new JsonObject { ["spiritual_healing"] = 3 } };
            await fixture.Files.WriteFileAtomicAsync(lease, "game_state/meta/soul_state.json", soul.ToJsonString());
            var guardian = new JsonObject { ["guardianId"] = "guardian_target_choice", ["canonicalName"] = "Хранитель выбора",
                ["abode"] = new JsonObject { ["abodeId"] = "abode_target_choice", ["name"] = "Обитель выбора" } };
            await fixture.Files.WriteFileAtomicAsync(lease, "game_state/meta/guardians.json", new JsonObject
            {
                ["guardians"] = new JsonArray(guardian), ["activeGuardian"] = guardian.DeepClone(),
                ["chaosSeaNavigation"] = new JsonObject { ["currentGuardianId"] = "guardian_target_choice", ["currentAbodeId"] = "abode_target_choice" }
            }.ToJsonString());
            await fixture.Files.WriteFileAtomicAsync(lease, GuardianAbodeResidentState.StatePath,
                new JsonObject { ["entries"] = new JsonArray() }.ToJsonString());
            _ = fixture.Files.GetOrCreateSessionGeneration(lease);
        }
        var before = fixture.Snapshot(); var mutations = fixture.MutationCalls;
        var opened = await WoundApplicationService.OpenAsync(fixture.Files, "/лечить", treatment: true);
        var state = Assert.IsType<WoundCommandState>(opened.WoundCommandState);
        Assert.Contains(state.Choices, c => c.Available && c.Selection.Aid is null);
        Assert.Equal(before, fixture.Snapshot()); Assert.Equal(mutations, fixture.MutationCalls);
        // The assertions above establish a valid, inert real read before this
        // deliberately missing target-navigation behavior can be called RED.
        var targets = Assert.IsType<UiSelectionPrompt>(Assert.Single(opened.Prompts, p => p.Id == "wound_target"));
        Assert.Equal("Себя", targets.Options[0].Label);
        Assert.Contains(targets.Options, p => p.Label.Contains("Хранитель выбора", StringComparison.Ordinal));
        Assert.DoesNotContain(opened.Prompts, p => p.Id == "confirm_wound_treatment");
    }
}
