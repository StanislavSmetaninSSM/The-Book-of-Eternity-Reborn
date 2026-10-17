using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

public sealed class DarenRetirementSaveTests(ITestOutputHelper output)
{
    [Fact]
    public async Task RealSaveLoad_PreservesEarnedCurrencyGrantHistoryAndInertProfile()
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-daren-retirement-save-" + Guid.NewGuid().ToString("N"));
        using var cleanup = new CleanupOwnedFixture(root, output.WriteLine);
        var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
        await new StateManager(files, new GameSettings(), NullLogger<StateManager>.Instance).BootstrapLocalStorageAsync();
        var state = PortableSaveFixture.Seed(files);
        var soul = JsonNode.Parse(File.ReadAllBytes(files.ResolvePath("game_state/meta/soul_state.json")))!.AsObject();
        soul["inkFeathers"] = new JsonObject { ["current"] = 17, ["total"] = 29 };
        var grant = new JsonObject { ["source"] = "daren_qte_showcase", ["tierId"] = "perfect_shadow", ["inkFeatherBonus"] = 6,
            ["grantedAtUtc"] = "2026-06-11T05:00:00Z", ["historicalUnknownField"] = "retain-owned-data" };
        soul["clientRewardGrants"] = new JsonObject { ["darenQteShowcase"] = grant.DeepClone(), ["unrelatedGrant"] = "unchanged" };
        await files.WriteFileAtomicAsync("game_state/meta/soul_state.json", soul.ToJsonString());
        DarenRetirementEvidenceFixtures.Put(root, DarenRetirementEvidenceFixtures.Profile, DarenRetirementEvidenceFixtures.Before);
        DarenRetirementEvidenceFixtures.Put(root, "unrelated-owned-history.bin", DarenRetirementEvidenceFixtures.After);
        var service = new SaveLoadService(files, state, NullLogger<SaveLoadService>.Instance);
        Assert.True(await service.SaveGameAsync("retirement-earned", "owned old-reward preservation"));
        var archive = Assert.Single(Directory.GetFiles(files.ResolvePath("saves/manual_saves"), "*.zip"));
        var archiveBefore = File.ReadAllBytes(archive);
        Assert.True(await service.LoadGameAsync(archive));
        var loaded = JsonNode.Parse(File.ReadAllBytes(files.ResolvePath("game_state/meta/soul_state.json")))!.AsObject();
        Assert.Equal(17, loaded["inkFeathers"]!["current"]!.GetValue<int>());
        Assert.Equal(29, loaded["inkFeathers"]!["total"]!.GetValue<int>());
        Assert.True(JsonNode.DeepEquals(grant, loaded["clientRewardGrants"]!["darenQteShowcase"]));
        Assert.Equal("unchanged", loaded["clientRewardGrants"]!["unrelatedGrant"]!.GetValue<string>());
        Assert.Equal(DarenRetirementEvidenceFixtures.Before, File.ReadAllBytes(Path.Combine(root, DarenRetirementEvidenceFixtures.Profile)));
        Assert.Equal(DarenRetirementEvidenceFixtures.After, File.ReadAllBytes(Path.Combine(root, "unrelated-owned-history.bin")));
        Assert.Equal(archiveBefore, File.ReadAllBytes(archive));
        output.WriteLine(JsonSerializer.Serialize(new { RealSaveAndLoad = true, Archive = DarenRetirementEvidenceFixtures.Hash(archiveBefore),
            InkFeathers = loaded["inkFeathers"], RetainedGrant = loaded["clientRewardGrants"],
            Profile = DarenRetirementEvidenceFixtures.Hash(DarenRetirementEvidenceFixtures.Before), ProofLimit = "owned supported-schema fixture, no arbitrary historical-schema compatibility claim" }));
    }
}
