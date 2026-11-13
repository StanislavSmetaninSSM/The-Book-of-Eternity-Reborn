using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class EffectMaterializationValidationTests
{
    private const string NpcSkillTransferItemId = "itm_npc_skill_transfer";

    [Fact]
    public async Task OrdinaryNpcProgressionSkillAndExistingItemTransfer_CommonEffectPreservesAllAcceptedResults()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        var (backups, originalItem) = await PrepareNpcSkillItemTransferAsync(context);
        var sourcePaths = backups.Values.Concat(new[]
        {
            "game_state/control/pending_turn_snapshot.json", PendingTurnSnapshotAuthority.AuthorityPath,
            "input/turn_request.json"
        }).Distinct(StringComparer.Ordinal).ToArray();
        var sourceBefore = await context.CaptureBytesAsync(sourcePaths);
        var admitted = await AdmitNpcSkillCommonPublicationAsync(context);

        var result = await AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(
            context.FileSystem, context.Normalizer, context.Validator, backups);

        Assert.DoesNotContain(result.Issues, issue => issue.Severity == IssueSeverity.Error);
        Assert.Equal(admitted.InputFingerprint, Assert.IsType<AcceptedMechanicsPlan>(result.MechanicsPlan).InputFingerprint);
        AssertNpcSkillTransferPublished(context.FileSystem, originalItem);
        Assert.Null(await context.ReadJsonAsync(EffectMaterializationTestContext.CommandPath));
        Assert.DoesNotContain(await context.Validator.ValidateAcceptedTurnCanonicalEffectMaterializationAsync(),
            issue => issue.Severity == IssueSeverity.Error);
        Assert.Equal(sourceBefore, await context.CaptureBytesAsync(sourcePaths));
        Assert.Null(await AcceptedMechanicsAuthorityTestProbe.PeekCommonAsync(context.FileSystem));
    }

    [Fact]
    public async Task OrdinaryNpcProgressionSkillAndExistingItemTransfer_ReachedLateCutRestoresWholeTransaction()
    {
        var armed = false;
        var cutReached = false;
        FileSystemManager? files = null;
        JsonObject? originalItem = null;
        const string marker = "NPC skill transfer reached common late cut";
        var hooks = new FileSystemManagerHooks
        {
            BeforeCanonicalMutationBoundaryAsync = path =>
            {
                if (!armed || cutReached || path != EffectMaterializationTestContext.CommandPath)
                    return Task.CompletedTask;
                cutReached = true;
                // Assertions observe real published state, not just successful admission.
                AssertNpcSkillTransferPublished(files!, originalItem!);
                throw new InvalidOperationException(marker);
            }
        };
        await using var context = await EffectMaterializationTestContext.CreateAsync(hooks);
        files = context.FileSystem;
        var prepared = await PrepareNpcSkillItemTransferAsync(context);
        originalItem = prepared.Item;
        await AdmitNpcSkillCommonPublicationAsync(context);
        var paths = CanonicalStateNormalizer.NormalizerRollbackTrackedFiles.Concat(prepared.Backups.Values)
            .Concat(new[] { NpcSkillCorePath, PendingNpcSkillsPath, "output/narrative_response.json",
                "output/interface_updates.json", "output/debug_logs.json", "input/turn_request.json",
                "game_state/control/pending_turn_snapshot.json", PendingTurnSnapshotAuthority.AuthorityPath })
            .Distinct(StringComparer.Ordinal).ToArray();
        var before = await context.CaptureBytesAsync(paths);

        armed = true;
        var failure = await Record.ExceptionAsync(() =>
            AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(
                context.FileSystem, context.Normalizer, context.Validator, prepared.Backups));
        armed = false;

        Assert.True(cutReached, "The actual common writer must reach its late effect-command consumption cut.");
        Assert.NotNull(failure);
        Assert.Contains(marker, failure!.ToString(), StringComparison.Ordinal);
        Assert.Equal(before, await context.CaptureBytesAsync(paths));
    }

    [Fact]
    public async Task OrdinaryNpcSkillTransfer_InputDriftAfterAdmissionRefusesWithoutMutation()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        var prepared = await PrepareNpcSkillItemTransferAsync(context);
        await AdmitNpcSkillCommonPublicationAsync(context);
        var commands = (await context.ReadJsonAsync(MortalItemAcceptedTransferCatalog.NpcCommandsPath))!;
        commands["NPCInventoryAdds"]![0]!["NPCName"] = "Changed display metadata after admission";
        await context.WriteJsonAsync(MortalItemAcceptedTransferCatalog.NpcCommandsPath, commands);
        var before = await context.CaptureBytesAsync(CanonicalStateNormalizer.NormalizerRollbackTrackedFiles);
        AcceptedTurnCanonicalStateRefresh.Result? result = null;
        var failure = await Record.ExceptionAsync(async () =>
            result = await AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(
                context.FileSystem, context.Normalizer, context.Validator, prepared.Backups));
        Assert.True(failure is InvalidDataException || result?.Issues.Any(issue => issue.Severity == IssueSeverity.Error) == true);
        Assert.Equal(before, await context.CaptureBytesAsync(before.Keys.ToArray()));
    }

    [Fact]
    public async Task OrdinaryNpcSkillTransfer_UnexpectedCoreMutationAfterItemWriteRollsBack()
    {
        var armed = false;
        var reached = false;
        FileSystemManager? files = null;
        var hooks = new FileSystemManagerHooks
        {
            BeforeCanonicalMutationBoundaryAsync = path =>
            {
                if (!armed || reached || path != MortalItemAcceptedTransferCatalog.NpcCommandsPath)
                    return Task.CompletedTask;
                var core = JsonNode.Parse(File.ReadAllText(files!.ResolvePath(NpcSkillCorePath)))!;
                Assert.Equal(NpcSkillTransferItemId,
                    Assert.Single(core["NPCsInScene"]![0]!["inventory"]!.AsArray())!["itemId"]!.GetValue<string>());
                reached = true;
                core["NPCsInScene"]![0]!["unexpectedPostItemMutation"] = true;
                File.WriteAllText(files.ResolvePath(NpcSkillCorePath), core.ToJsonString());
                return Task.CompletedTask;
            }
        };
        await using var context = await EffectMaterializationTestContext.CreateAsync(hooks);
        files = context.FileSystem;
        var prepared = await PrepareNpcSkillItemTransferAsync(context);
        await AdmitNpcSkillCommonPublicationAsync(context);
        var before = await context.CaptureBytesAsync(CanonicalStateNormalizer.NormalizerRollbackTrackedFiles);
        armed = true;
        var failure = await Record.ExceptionAsync(() =>
            AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(
                context.FileSystem, context.Normalizer, context.Validator, prepared.Backups));
        armed = false;
        Assert.True(reached, "The real item writer must publish NPC inventory before the mutation injection.");
        Assert.IsType<InvalidDataException>(failure);
        Assert.Contains("item-phase input differs", failure!.Message, StringComparison.Ordinal);
        Assert.Equal(before, await context.CaptureBytesAsync(before.Keys.ToArray()));
    }

    [Fact]
    public async Task OrdinaryNpcSkillTransfer_SameTurnNpcEndpointRefusesBoundedComposition()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        await PrepareNpcSkillItemTransferAsync(context);
        var core = (await context.ReadJsonAsync(NpcSkillCorePath))!.AsObject();
        var created = core["NPCsInScene"]![0]!.DeepClone().AsObject();
        created["NPCId"] = null;
        created["initialId"] = "new_transfer_recipient";
        core["UpdateNPCs"] = new JsonArray(created);
        await context.WriteJsonAsync(NpcSkillCorePath, core);
        var commands = (await context.ReadJsonAsync(MortalItemAcceptedTransferCatalog.NpcCommandsPath))!;
        commands["NPCInventoryAdds"]![0]!["NPCId"] = "new_transfer_recipient";
        await context.WriteJsonAsync(MortalItemAcceptedTransferCatalog.NpcCommandsPath, commands);
        var itemIssues = await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync();
        Assert.DoesNotContain(itemIssues, issue => issue.Severity == IssueSeverity.Error);
        var before = await context.CaptureBytesAsync(CanonicalStateNormalizer.NormalizerRollbackTrackedFiles);

        // Exercise the real issued skill-input gate directly after item admission;
        // unrelated new-actor resource composition is not this endpoint control's subject.
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var source = await context.Validator.PrepareNpcProgressionPublicationAsync(lease,
            await context.FileSystem.ReadFileBytesAsync(lease, NpcSkillCorePath),
            await context.FileSystem.ReadFileBytesAsync(lease, PendingNpcSkillsPath));

        Assert.Contains(source.Issues, issue => issue.Code == "npc_skill_item_phase_invalid");
        Assert.Equal(before, await context.CaptureBytesAsync(before.Keys.ToArray()));
    }

    [Fact]
    public async Task OrdinaryNpcSkillTransfer_ItemAdmissionWithoutCommonPlanRefusesBeforeWrites()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        var prepared = await PrepareNpcSkillItemTransferAsync(context);
        context.FileSystem.DeleteFile(EffectMaterializationTestContext.CommandPath);
        Assert.DoesNotContain(await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync(),
            issue => issue.Severity == IssueSeverity.Error);
        Assert.Null(await AcceptedMechanicsAuthorityTestProbe.PeekCommonAsync(context.FileSystem));
        var before = await context.CaptureBytesAsync(CanonicalStateNormalizer.NormalizerRollbackTrackedFiles);

        var failure = await Assert.ThrowsAsync<InvalidDataException>(() =>
            AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(
                context.FileSystem, context.Normalizer, context.Validator, prepared.Backups));

        Assert.Equal("Ordinary NPC skill transfers require a validated ordinary common plan.", failure.Message);
        Assert.Equal(before, await context.CaptureBytesAsync(before.Keys.ToArray()));
    }

    private static async Task<(IReadOnlyDictionary<string, string> Backups, JsonObject Item)>
        PrepareNpcSkillItemTransferAsync(EffectMaterializationTestContext context)
    {
        var item = MortalItemTestFixture.CreateCanonicalRoot(NpcSkillTransferItemId);
        await context.WriteJsonAsync(InventoryEquipmentService.ItemsPath, new JsonObject
        {
            ["items"] = new JsonArray(item.DeepClone()), ["equipment"] = new JsonObject()
        });
        await context.WriteJsonAsync(MortalItemIdentityState.StatePath, MortalItemTestFixture.CreateIndex(item));
        // This existing helper captures the real signed original before authoring progression/skills/effect.
        await PrepareNpcSkillGrant(context, includeProgressionAward: true);
        await context.WriteJsonAsync("game_state/inventory/item_removals.json", new JsonObject
        {
            ["removeInventoryItems"] = new JsonArray(new JsonObject
            {
                ["removedItemId"] = NpcSkillTransferItemId, ["itemName"] = item["name"]!.DeepClone(),
                ["currentContentsPath"] = null
            })
        });
        var core = (await context.ReadJsonAsync(NpcSkillCorePath))!;
        await context.WriteJsonAsync("game_state/npcs/npc_inventory.json", new JsonObject
        {
            ["NPCInventoryAdds"] = new JsonArray(new JsonObject
            {
                ["NPCId"] = NpcSkillOwner, ["NPCName"] = core["NPCsInScene"]![0]!["name"]!.DeepClone(),
                ["item"] = item.DeepClone(), ["destinationContainerId"] = null
            })
        });
        return (await context.ReadPendingSnapshotBackupsAsync(), item);
    }

    private static void AssertNpcSkillTransferPublished(FileSystemManager files, JsonObject originalItem)
    {
        JsonNode Read(string path) => JsonNode.Parse(File.ReadAllText(files.ResolvePath(path)))!;
        var core = Read(NpcSkillCorePath).AsObject();
        Assert.False(core.ContainsKey("NPCCoreChanges"));
        var actor = Assert.Single(core["NPCsInScene"]!.AsArray())!;
        Assert.Equal(3, actor["level"]!.GetValue<int>());
        Assert.Equal(0, actor["experience"]!.GetValue<int>());
        Assert.Equal(1558, actor["experienceForNextLevel"]!.GetValue<int>());
        var skill = Assert.Single(actor["activeSkills"]!.AsArray())!;
        Assert.Equal(GrantedNpcSkillId, skill["skillId"]!.GetValue<string>());
        Assert.Equal(1, skill["masteryLevel"]!.GetValue<int>());
        var moved = Assert.Single(actor["inventory"]!.AsArray())!;
        Assert.Equal(NpcSkillTransferItemId, moved["itemId"]!.GetValue<string>());
        Assert.True(JsonNode.DeepEquals(originalItem["materializationReceipt"], moved["materializationReceipt"]));
        Assert.Empty(Read(InventoryEquipmentService.ItemsPath)["items"]!.AsArray());
        var index = MortalItemIdentityState.Parse(Read(MortalItemIdentityState.StatePath));
        Assert.Empty(index.Issues);
        var carrier = index.EntriesByItemId[NpcSkillTransferItemId]["currentCarrier"]!;
        Assert.Equal("npc_inventory", carrier["kind"]!.GetValue<string>());
        Assert.Equal(NpcSkillOwner, carrier["ownerId"]!.GetValue<string>());
        Assert.Empty(Read(PendingNpcSkillsPath).AsObject());
        var effect = Assert.Single(Read(EffectMaterializationTestContext.PlayerEffectsPath)["activeEffects"]!.AsArray())!;
        Assert.Equal(GrantedNpcSkillId, effect["source"]!["sourceId"]!.GetValue<string>());
        var effectEntry = Assert.Single(Read(EffectMaterializationTestContext.IdentityIndexPath)["entries"]!.AsArray())!;
        Assert.Equal(effect["effectId"]!.GetValue<string>(), effectEntry["effectId"]!.GetValue<string>());
    }
}
