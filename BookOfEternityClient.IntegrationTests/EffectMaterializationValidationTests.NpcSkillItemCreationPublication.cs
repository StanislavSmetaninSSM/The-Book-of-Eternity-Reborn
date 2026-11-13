using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class EffectMaterializationValidationTests
{
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(true, false)]
    public async Task OrdinaryNpcProgressionSkillAndPlayerItemCreation_PublishesOneAuthorizedItemAndCompletedCandidate(bool toNpc, bool withEffect)
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        var prepared = await PrepareNpcSkillCreationAsync(context, toNpc, withEffect);
        var (backups, turn, authorityId, admitted, allocatedId, allocatedReceipt) = prepared;
        var sourcePaths = backups.Values.Concat(new[] { "input/turn_request.json",
            "game_state/control/pending_turn_snapshot.json", PendingTurnSnapshotAuthority.AuthorityPath })
            .Distinct(StringComparer.Ordinal).ToArray();
        var sourceBefore = await context.CaptureBytesAsync(sourcePaths);
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
        Assert.Equal(allocatedId, itemId);
        Assert.False(item.AsObject().ContainsKey("creationRef"));
        var receipt = item["materializationReceipt"]!;
        Assert.Equal(itemId, receipt["itemId"]!.GetValue<string>());
        Assert.Equal(NpcSkillCreationRef, receipt["creationRef"]!.GetValue<string>());
        Assert.Equal(allocatedReceipt, receipt["receiptId"]!.GetValue<string>());
        Assert.Equal(turn, receipt["acceptedAtTurn"]!.GetValue<int>());
        Assert.Equal(authorityId, item["materialization"]!["sourceAuthority"]!["authorityId"]!.GetValue<string>());
        var index = MortalItemIdentityState.Parse((await context.ReadJsonAsync(MortalItemIdentityState.StatePath))!);
        Assert.Empty(index.Issues);
        Assert.Single(index.EntriesByItemId);
        Assert.Equal(toNpc ? "npc_inventory" : "player_inventory", index.EntriesByItemId[itemId]["currentCarrier"]!["kind"]!.GetValue<string>());
        Assert.Equal(toNpc ? NpcSkillOwner : "player", index.EntriesByItemId[itemId]["currentCarrier"]!["ownerId"]!.GetValue<string>());
        Assert.Null(await context.ReadJsonAsync(EffectMaterializationTestContext.CommandPath));
        if (withEffect)
        {
            var effect = Assert.Single((await context.ReadJsonAsync(EffectMaterializationTestContext.PlayerEffectsPath))!["activeEffects"]!.AsArray())!;
            Assert.Equal(GrantedNpcSkillId, effect["source"]!["sourceId"]!.GetValue<string>());
            var effectEntry = Assert.Single((await context.ReadJsonAsync(EffectMaterializationTestContext.IdentityIndexPath))!["entries"]!.AsArray())!;
            Assert.Equal(effect["effectId"]!.GetValue<string>(), effectEntry["effectId"]!.GetValue<string>());
        }
        else
        {
            var effects = await context.ReadJsonAsync(EffectMaterializationTestContext.PlayerEffectsPath);
            Assert.True(effects is null || effects["activeEffects"] is JsonArray { Count: 0 });
            var effectIndex = await context.ReadJsonAsync(EffectMaterializationTestContext.IdentityIndexPath);
            Assert.True(effectIndex is null || effectIndex["entries"] is JsonArray { Count: 0 });
        }
        var paths = CanonicalStateNormalizer.NormalizerRollbackTrackedFiles;
        var accepted = await context.CaptureBytesAsync(paths);
        var repeated = await AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(
            context.FileSystem, context.Normalizer, context.Validator, backups);
        Assert.DoesNotContain(repeated.Issues, issue => issue.Severity == IssueSeverity.Error);
        Assert.Equal(accepted, await context.CaptureBytesAsync(paths));
        Assert.Equal(sourceBefore, await context.CaptureBytesAsync(sourcePaths));
    }

    private const string NpcSkillCreationRef = "new_item_npc_progression_reward";

    private static async Task<(IReadOnlyDictionary<string, string> Backups, int Turn, string AuthorityId,
        AcceptedMechanicsPlan Plan, string ItemId, string ReceiptId)> PrepareNpcSkillCreationAsync(
        EffectMaterializationTestContext context, bool toNpc, bool withEffect)
    {
        await context.WriteJsonAsync(InventoryEquipmentService.ItemsPath,
            new JsonObject { ["items"] = new JsonArray(), ["equipment"] = new JsonObject() });
        var backups = await PrepareNpcSkillCommonPublicationAsync(context, includeProgressionAward: true);
        var request = (await context.ReadJsonAsync("input/turn_request.json"))!;
        var turn = request["turnNumber"]!.GetValue<int>();
        var authorityId = toNpc ? $"npc_inventory_add:{turn}:0:{NpcSkillOwner}" : $"turn_{turn}";
        var rawItem = MortalItemTestFixture.CreateRawRoot(
            route: toNpc ? "npc_acquisition" : "player_acquisition",
            authorityKind: toNpc ? "npc_inventory_add" : "turn_outcome", authorityId: authorityId,
            sourceTurn: turn, creationRef: NpcSkillCreationRef, materializationId: "mat_npc_progression_reward");
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
        if (!withEffect) context.FileSystem.DeleteFile(EffectMaterializationTestContext.CommandPath);
        Assert.DoesNotContain(await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync(),
            issue => issue.Severity == IssueSeverity.Error);
        var issues = await context.Validator.ValidateAcceptedTurnRawResourceMaterializationAsync();
        Assert.True(issues.All(issue => issue.Severity != IssueSeverity.Error),
            string.Join(Environment.NewLine, issues.Select(issue => $"{issue.Code}: {issue.FilePath}: {issue.Actual}")));
        var common = await AcceptedMechanicsAuthorityTestProbe.PeekCommonAsync(context.FileSystem);
        Assert.NotNull(common);
        var plan = Assert.IsType<AcceptedMechanicsPlan>(common!.Result.Plan);
        Assert.NotNull(plan.EffectPlan?.NpcSkillInput);
        if (!withEffect)
        {
            Assert.Empty(plan.EffectPlan!.ActiveEffects);
            Assert.Null(await context.ReadJsonAsync(EffectMaterializationTestContext.CommandPath));
        }
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        Assert.True(MortalItemAcceptedTurnAuthority.TryCaptureNormalizationSnapshot(context.FileSystem, lease,
            common.Binding.SessionId, common.Binding.SnapshotToken, common.Binding.Turn, out var snapshot));
        Assert.True(snapshot.MatchesAcceptedOwnerAuthority(plan.OwnerAuthority));
        Assert.True(snapshot.TryGetAllocatedItemId(NpcSkillCreationRef, out var itemId));
        Assert.True(snapshot.TryGetRootReceiptId(NpcSkillCreationRef, out var receiptId));
        return (backups, turn, authorityId, plan, itemId, receiptId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OrdinaryNpcProgressionSkillAndItemCreation_ReachedLateCutRestoresAllocatedPublication(bool toNpc)
    {
        var armed = false;
        var reached = false;
        string? allocatedId = null;
        string? allocatedReceipt = null;
        FileSystemManager? files = null;
        const string marker = "NPC skill item creation late publication cut";
        var hooks = new FileSystemManagerHooks
        {
            BeforeCanonicalMutationBoundaryAsync = path =>
            {
                if (!armed || reached || path != EffectMaterializationTestContext.CommandPath)
                    return Task.CompletedTask;
                reached = true;
                JsonNode Read(string file) => JsonNode.Parse(File.ReadAllText(files!.ResolvePath(file)))!;
                var core = Read(NpcSkillCorePath).AsObject();
                Assert.False(core.ContainsKey("NPCCoreChanges"));
                var actor = Assert.Single(core["NPCsInScene"]!.AsArray())!;
                Assert.Equal(3, actor["level"]!.GetValue<int>());
                Assert.Equal(0, actor["experience"]!.GetValue<int>());
                Assert.Equal(GrantedNpcSkillId, Assert.Single(actor["activeSkills"]!.AsArray())!["skillId"]!.GetValue<string>());
                var item = Assert.Single((toNpc ? actor["inventory"] : Read(InventoryEquipmentService.ItemsPath)["items"])!.AsArray())!;
                Assert.Equal(allocatedId, item["itemId"]!.GetValue<string>());
                Assert.Equal(allocatedReceipt, item["materializationReceipt"]!["receiptId"]!.GetValue<string>());
                Assert.Empty(Read(PendingNpcSkillsPath).AsObject());
                Assert.Equal(GrantedNpcSkillId, Assert.Single(Read(EffectMaterializationTestContext.PlayerEffectsPath)["activeEffects"]!.AsArray())!["source"]!["sourceId"]!.GetValue<string>());
                throw new InvalidOperationException(marker);
            }
        };
        await using var context = await EffectMaterializationTestContext.CreateAsync(hooks);
        files = context.FileSystem;
        var prepared = await PrepareNpcSkillCreationAsync(context, toNpc, withEffect: true);
        allocatedId = prepared.ItemId;
        allocatedReceipt = prepared.ReceiptId;
        var paths = CanonicalStateNormalizer.NormalizerRollbackTrackedFiles.Concat(prepared.Backups.Values)
            .Concat(new[] { "input/turn_request.json", "game_state/control/pending_turn_snapshot.json",
                PendingTurnSnapshotAuthority.AuthorityPath, "output/narrative_response.json",
                "output/interface_updates.json", "output/debug_logs.json" }).Distinct(StringComparer.Ordinal).ToArray();
        var before = await context.CaptureBytesAsync(paths);
        armed = true;
        var failure = await Record.ExceptionAsync(() =>
            AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(
                context.FileSystem, context.Normalizer, context.Validator, prepared.Backups));
        armed = false;
        Assert.True(reached, "The real writer must reach effect-command consumption after allocated item publication.");
        Assert.NotNull(failure);
        Assert.Contains(marker, failure!.ToString(), StringComparison.Ordinal);
        Assert.Equal(before, await context.CaptureBytesAsync(paths));
    }
}
