using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Fact]
    public async Task MortalItemCanonicalRepair_RejectsStaleAcquisitionConfirmationUntilRewritten()
    {
        await WriteJsonAsync("output/narrative_response.json", new
        {
            response = "Вы получаете клинок и убираете его в инвентарь.",
            timestamp = "2026-08-11T10:00:00Z"
        });
        await WriteJsonAsync("output/interface_updates.json", new
        {
            dialogueOptions = new[] { "Осмотреть полученный клинок" },
            timestamp = "2026-08-11T10:00:00Z"
        });
        var repairBoundaryUtc = DateTime.UtcNow.AddMinutes(-1);
        var staleWrittenAtUtc = repairBoundaryUtc.AddMinutes(-1);
        File.SetLastWriteTimeUtc(
            _fs.ResolvePath("output/narrative_response.json"),
            staleWrittenAtUtc);
        File.SetLastWriteTimeUtc(
            _fs.ResolvePath("output/interface_updates.json"),
            staleWrittenAtUtc);
        var itemIssue = new ValidationIssue(
            "game_state/inventory/items.json.UpdateInventory[0].materialization.sections.mechanics",
            IssueSeverity.Error,
            "Mortal item materialization was repaired after the acquisition output was written.",
            code: "mortal_item_materialization_section_missing",
            actor: "mortal_item:new:reward_blade_42",
            section: "MortalItemMaterialization",
            expected: "complete accepted item package",
            actual: "mechanics section missing",
            repairHint: "Complete the exact item package.",
            repairTargetFiles: new[] { "game_state/inventory/items.json" });

        var engine = CreateGameEngine();
        var staleIssues = InvokePrivateValue<List<ValidationIssue>>(
            engine,
            "CollectPlayerFacingOutputStaleAfterCanonicalRepairIssues",
            new[] { itemIssue },
            repairBoundaryUtc);

        Assert.Equal(2, staleIssues.Count);
        Assert.All(
            staleIssues,
            issue => Assert.Equal(
                "accepted_turn_stale_player_facing_output_after_canonical_repair",
                issue.Code));
        Assert.Contains(
            staleIssues,
            issue => issue.FilePath == "output/narrative_response.json");
        Assert.Contains(
            staleIssues,
            issue => issue.FilePath == "output/interface_updates.json");

        await InvokePrivateTaskAsync(
            engine,
            "WriteValidationRepairRequestAsync",
            "обработки хода",
            staleIssues,
            2);

        var request = await _fs.ReadFileAsync(
            "game_state/control/validation_repair_request.json");
        Assert.Contains("accepted_turn_output_artifact_repair", request, StringComparison.Ordinal);
        Assert.Contains("output/narrative_response.json", request, StringComparison.Ordinal);
        Assert.Contains("output/interface_updates.json", request, StringComparison.Ordinal);
        Assert.DoesNotContain(MortalItemIdentityState.StatePath, request, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MortalItemRollback_RemovesUnacceptedItemAndAcquisitionConfirmationByteForByte()
    {
        const string itemsPath = "game_state/inventory/items.json";
        var baselineItems = "{\"items\":[]}"u8.ToArray();
        var baselineIndex = "{\"schemaVersion\":1,\"entries\":[]}"u8.ToArray();
        await _fs.WriteFileAtomicBytesAsync(itemsPath, baselineItems);
        await _fs.WriteFileAtomicBytesAsync(MortalItemIdentityState.StatePath, baselineIndex);
        var engine = CreateGameEngine();
        var rollbackSnapshot = await InvokePrivateTaskResultAsync(
            engine,
            "CreatePreTurnBackup",
            "mortal_item_output_rollback");

        await _fs.WriteFileAtomicAsync(
            itemsPath,
            """{"items":[{"itemId":"itm_unaccepted_blade","name":"Клинок"}]}""");
        await _fs.WriteFileAtomicAsync(
            MortalItemIdentityState.StatePath,
            """{"schemaVersion":1,"entries":[{"itemId":"itm_unaccepted_blade"}]}""");
        await WriteJsonAsync("output/narrative_response.json", new
        {
            response = "Вы получили Клинок.",
            timestamp = "2026-08-11T10:00:00Z"
        });
        await WriteJsonAsync("output/interface_updates.json", new
        {
            dialogueOptions = new[] { "Использовать Клинок" },
            timestamp = "2026-08-11T10:00:00Z"
        });

        await InvokePrivateTaskAsync(engine, "RestorePreTurnBackup", rollbackSnapshot);

        Assert.Equal(baselineItems, await _fs.ReadFileBytesAsync(itemsPath));
        Assert.Equal(
            baselineIndex,
            await _fs.ReadFileBytesAsync(MortalItemIdentityState.StatePath));
        Assert.False(_fs.FileExists("output/narrative_response.json"));
        Assert.False(_fs.FileExists("output/interface_updates.json"));
    }
}
