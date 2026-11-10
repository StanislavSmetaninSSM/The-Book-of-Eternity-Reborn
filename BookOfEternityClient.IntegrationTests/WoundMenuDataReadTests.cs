using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Services;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

public sealed class WoundMenuDataReadTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("chaos_sea")]
    [InlineData("shining_abode")]
    public async Task RetainedMenuEffectAndSkillDataMatchesFreshReaderAndNextOpenIsFresh(string realm)
    {
        var wound = WoundContractTestData.CreateSpiritualActiveWound("wound_menu_data", realm,
            ownerId: "player_soul", carrierPath: WoundCarrierCatalog.AfterlifeProfilesPath);
        var supplied = WoundPlayerProjectionTestData.InputInRealm(realm, wound);
        var profile = Assert.Single(supplied.Carriers.AfterlifeProfiles!["profiles"]!.AsArray())!.AsObject();
        profile["standardArts"] = new JsonObject { ["spiritual_healing"] = 0 };
        var reads = new CanonicalReadActivityAccumulator();
        await using var fixture = await WoundPlayerReadFixture.CreateAsync(output, realm: realm, suppliedInput: supplied, reads: reads);
        const string activePath = "game_state/player/skills_active.json";
        const string npcPath = "game_state/npcs/npc_core.json";
        await using (var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync())
        {
            await fixture.Files.WriteFileAtomicAsync(lease, activePath, new JsonObject
            {
                ["activeSkillChanges"] = new JsonArray(new JsonObject
                { ["skillId"] = "skill_menu_data", ["skillName"] = "Исходный навык", ["lifecycle"] = "active", ["active"] = true })
            }.ToJsonString());
            await fixture.Files.WriteFileAtomicAsync(lease, npcPath, "{\"NPCsInScene\":[]}");
        }
        var before = fixture.Snapshot(); string retainedScope;
        await using (var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync())
        {
            var npcReads = reads.ReadCount(npcPath);
            var context = Assert.IsType<WoundPlayerReadContext>(await WoundPlayerReadService.LoadContextAsync(fixture.Files, lease, retainMenuInputs: true));
            Assert.True(context.Projection.IsAvailable); Assert.True(context.Input.Effects.IsAccepted);
            Assert.Equal(npcReads + 1, reads.ReadCount(npcPath));
            retainedScope = context.Input.Effects.SkillScopeAuthority.CreateGmCatalog().ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed);
            var fresh = await EffectMechanicsSnapshot.LoadAsync(fixture.Files, lease);
            Assert.Equal(fresh.IsAccepted, context.Input.Effects.IsAccepted);
            Assert.True(JsonNode.DeepEquals(fresh.SkillScopeAuthority.CreateGmCatalog(), context.Input.Effects.SkillScopeAuthority.CreateGmCatalog()));
            Assert.Equal(fresh.Components.Select(row => row.EffectId + ":" + row.ComponentId + ":" + row.Payload.GetRawText()),
                context.Input.Effects.Components.Select(row => row.EffectId + ":" + row.ComponentId + ":" + row.Payload.GetRawText()));
            var goldenInput = context.Input with { Effects = fresh };
            var golden = context with { Input = goldenInput, Projection = WoundPlayerProjection.Build(goldenInput, context.Catalog) };
            var retainedMenu = WoundApplicationService.CreateState("menu_data_generation", context);
            var freshMenu = WoundApplicationService.CreateState("menu_data_generation", golden);
            Assert.Equal(freshMenu.Choices.Select(choice => (choice.Selection.ToJson().ToJsonString(), choice.Name, choice.Available, choice.Reason)),
                retainedMenu.Choices.Select(choice => (choice.Selection.ToJson().ToJsonString(), choice.Name, choice.Available, choice.Reason)));
            Assert.Contains("Исходный навык", retainedScope);
        }
        Assert.Equal(before, fixture.Snapshot());
        await using (var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync())
        {
            var root = JsonNode.Parse(Assert.IsType<string>(await fixture.Files.ReadFileAsync(lease, activePath)))!.AsObject();
            root["activeSkillChanges"]!.AsArray()[0]!["skillName"] = "Следующий навык";
            await fixture.Files.WriteFileAtomicAsync(lease, activePath, root.ToJsonString());
        }
        var changed = fixture.Snapshot();
        await using (var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync())
        {
            var next = Assert.IsType<WoundPlayerReadContext>(await WoundPlayerReadService.LoadContextAsync(fixture.Files, lease, retainMenuInputs: true));
            var catalog = next.Input.Effects.SkillScopeAuthority.CreateGmCatalog().ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed);
            Assert.Contains("Следующий навык", catalog); Assert.DoesNotContain("Исходный навык", catalog);
            Assert.Contains("Исходный навык", retainedScope);
        }
        Assert.Equal(changed, fixture.Snapshot());
        await fixture.DamageAsync("invalid_effect_utf8");
        var invalid = fixture.Snapshot();
        await using (var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync())
            Assert.Null(await WoundPlayerReadService.LoadContextAsync(fixture.Files, lease, retainMenuInputs: true));
        Assert.Equal(invalid, fixture.Snapshot());
    }
}
