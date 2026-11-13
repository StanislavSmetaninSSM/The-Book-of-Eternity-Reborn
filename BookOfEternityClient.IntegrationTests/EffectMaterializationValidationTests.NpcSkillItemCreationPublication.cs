using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class EffectMaterializationValidationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OrdinaryNpcProgressionSkillAndPlayerItemCreation_PublishesOneAuthorizedItemAndCompletedCandidate(bool toNpc)
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        await context.WriteJsonAsync(InventoryEquipmentService.ItemsPath,
            new JsonObject { ["items"] = new JsonArray(), ["equipment"] = new JsonObject() });
        var backups = await PrepareNpcSkillCommonPublicationAsync(context, includeProgressionAward: true);
        var sourcePaths = backups.Values.Concat(new[] { "input/turn_request.json",
            "game_state/control/pending_turn_snapshot.json", PendingTurnSnapshotAuthority.AuthorityPath })
            .Distinct(StringComparer.Ordinal).ToArray();
        var sourceBefore = await context.CaptureBytesAsync(sourcePaths);
        var request = (await context.ReadJsonAsync("input/turn_request.json"))!;
        var turn = request["turnNumber"]!.GetValue<int>();
        const string creationRef = "new_item_npc_progression_reward";
        var authorityId = toNpc ? $"npc_inventory_add:{turn}:0:{NpcSkillOwner}" : $"turn_{turn}";
        var rawItem = MortalItemTestFixture.CreateRawRoot(
            route: toNpc ? "npc_acquisition" : "player_acquisition",
            authorityKind: toNpc ? "npc_inventory_add" : "turn_outcome", authorityId: authorityId,
            sourceTurn: turn, creationRef: creationRef, materializationId: "mat_npc_progression_reward");
        var inventory = (await context.ReadJsonAsync(InventoryEquipmentService.ItemsPath))!.AsObject();
        if (toNpc)
            await context.WriteJsonAsync(MortalItemAcceptedTransferCatalog.NpcCommandsPath, new JsonObject
            {
                ["NPCInventoryAdds"] = new JsonArray(new JsonObject
                {
                    ["NPCId"] = NpcSkillOwner, ["NPCName"] = "Test mentor",
                    ["item"] = rawItem, ["destinationContainerId"] = null
                })
            });
        else
        {
            inventory["UpdateInventory"] = new JsonArray(rawItem);
            await context.WriteJsonAsync(InventoryEquipmentService.ItemsPath, inventory);
        }
        var admitted = await AdmitNpcSkillCommonPublicationAsync(context);

        var result = await AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(
            context.FileSystem, context.Normalizer, context.Validator, backups);

        Assert.DoesNotContain(result.Issues, issue => issue.Severity == IssueSeverity.Error);
        Assert.Equal(admitted.InputFingerprint, Assert.IsType<AcceptedMechanicsPlan>(result.MechanicsPlan).InputFingerprint);
        var core = (await context.ReadJsonAsync(NpcSkillCorePath))!.AsObject();
        Assert.False(core.ContainsKey("NPCCoreChanges"));
        var actor = Assert.Single(core["NPCsInScene"]!.AsArray())!;
        Assert.Equal(NpcSkillOwner, actor["NPCId"]!.GetValue<string>());
        Assert.Equal(3, actor["level"]!.GetValue<int>());
        Assert.Equal(0, actor["experience"]!.GetValue<int>());
        Assert.Equal(1558, actor["experienceForNextLevel"]!.GetValue<int>());
        var skill = Assert.Single(actor["activeSkills"]!.AsArray())!;
        Assert.Equal(GrantedNpcSkillId, skill["skillId"]!.GetValue<string>());
        Assert.Equal(1, skill["masteryLevel"]!.GetValue<int>());
        Assert.Empty((await context.ReadJsonAsync(PendingNpcSkillsPath))!.AsObject());
        var player = (await context.ReadJsonAsync(InventoryEquipmentService.ItemsPath))!.AsObject();
        Assert.False(player.ContainsKey("UpdateInventory"));
        var item = Assert.Single((toNpc ? actor["inventory"] : player["items"])!.AsArray())!;
        Assert.Empty((toNpc ? player["items"] : actor["inventory"])!.AsArray());
        var npcCommands = await context.ReadJsonAsync(MortalItemAcceptedTransferCatalog.NpcCommandsPath);
        Assert.False(npcCommands is JsonObject commands && commands.ContainsKey("NPCInventoryAdds"));
        var itemId = item["itemId"]!.GetValue<string>();
        Assert.False(string.IsNullOrWhiteSpace(itemId));
        Assert.False(item.AsObject().ContainsKey("creationRef"));
        var receipt = item["materializationReceipt"]!;
        Assert.Equal(itemId, receipt["itemId"]!.GetValue<string>());
        Assert.Equal(creationRef, receipt["creationRef"]!.GetValue<string>());
        Assert.Equal(turn, receipt["acceptedAtTurn"]!.GetValue<int>());
        Assert.Equal(authorityId, item["materialization"]!["sourceAuthority"]!["authorityId"]!.GetValue<string>());
        var index = MortalItemIdentityState.Parse((await context.ReadJsonAsync(MortalItemIdentityState.StatePath))!);
        Assert.Empty(index.Issues);
        Assert.Single(index.EntriesByItemId);
        Assert.Equal(toNpc ? "npc_inventory" : "player_inventory", index.EntriesByItemId[itemId]["currentCarrier"]!["kind"]!.GetValue<string>());
        Assert.Equal(toNpc ? NpcSkillOwner : "player", index.EntriesByItemId[itemId]["currentCarrier"]!["ownerId"]!.GetValue<string>());
        Assert.Null(await context.ReadJsonAsync(EffectMaterializationTestContext.CommandPath));
        var effect = Assert.Single((await context.ReadJsonAsync(EffectMaterializationTestContext.PlayerEffectsPath))!["activeEffects"]!.AsArray())!;
        Assert.Equal(GrantedNpcSkillId, effect["source"]!["sourceId"]!.GetValue<string>());
        var effectEntry = Assert.Single((await context.ReadJsonAsync(EffectMaterializationTestContext.IdentityIndexPath))!["entries"]!.AsArray())!;
        Assert.Equal(effect["effectId"]!.GetValue<string>(), effectEntry["effectId"]!.GetValue<string>());
        Assert.Equal(sourceBefore, await context.CaptureBytesAsync(sourcePaths));
    }
}
