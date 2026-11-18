using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class MortalLiveSeedBootstrapTests
{
    [Fact]
    public async Task FreshSeedPreservesAlreadyPresentSelectedCompanionBytes()
    {
        var root = Path.Combine(TestDiagnosticPaths.Root, "mortal-seed-bootstrap-preserve", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var example = Path.Combine(root, "example");
            foreach (var source in Directory.GetFiles(TestRepoPaths.BaseSessionRoot, "*", SearchOption.AllDirectories))
            {
                var destination = Path.Combine(example, Path.GetRelativePath(TestRepoPaths.BaseSessionRoot, source));
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(source, destination);
            }
            var companion = Path.Combine(example, StorageTransportMoveService.VehiclesPath);
            Directory.CreateDirectory(Path.GetDirectoryName(companion)!);
            var bytes = System.Text.Encoding.UTF8.GetBytes("{\n  \"birthReceipts\": [], \"activeVehicleId\": null, \"vehicles\": []\n}\n");
            await File.WriteAllBytesAsync(companion, bytes);
            var files = new FileSystemManager(Path.Combine(root, "play"), NullLogger<FileSystemManager>.Instance);
            await MortalLiveSeedBuilder.PrepareAsync(files, example, seed: null);
            Assert.Equal(bytes, await File.ReadAllBytesAsync(files.ResolvePath(StorageTransportMoveService.VehiclesPath)));
            Assert.True(JsonNode.DeepEquals(MortalVehicleBirthReceiptState.CreateEmptyRoot(),
                JsonNode.Parse((await files.ReadFileAsync(StorageTransportMoveService.VehiclesPath))!)));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FreshSeedInitializesClientOwnedEmptyCompanionsBeforeOriginal(bool stateOnly)
    {
        var root = Path.Combine(TestDiagnosticPaths.Root, "mortal-seed-bootstrap", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            await MortalLiveSeedBuilder.PrepareAsync(files, TestRepoPaths.BaseSessionRoot,
                MortalLiveSeedBuilder.SeedActorsAsync, stateOnly);
            var bootstrap = MortalBootstrapStateBuilder.BuildFreshMortalBootstrapFiles(
                2, 41, "Seed actor", "Seed world", "Seed setup", DateTimeOffset.UnixEpoch);
            var differences = new List<string>();
            // Only empty infrastructure belongs here. The example's populated inventory,
            // location identities and authored NPC/faction/project history must not be reset.
            foreach (var path in new[] { StorageTransportMoveService.VehiclesPath,
                         "game_state/npcs/npc_skills.json",
                         "game_state/inventory/item_bonds.json", "game_state/inventory/item_text_updates.json" })
            {
                var text = await files.ReadFileAsync(path);
                if (text == null || !JsonNode.DeepEquals(bootstrap[path], JsonNode.Parse(text)))
                    differences.Add(path + (text == null ? ": missing" : ": differs from production empty root"));
            }
            Assert.True(differences.Count == 0, string.Join("\n", differences));
            foreach (var path in new[] { "game_state/inventory/items.json",
                         MortalItemIdentityState.StatePath, "game_state/npcs/item_journals.json" })
                Assert.Equal(await File.ReadAllBytesAsync(Path.Combine(TestRepoPaths.BaseSessionRoot, path)),
                    await File.ReadAllBytesAsync(files.ResolvePath(path)));
            Assert.True(JsonNode.DeepEquals(MortalVehicleBirthReceiptState.CreateEmptyRoot(),
                JsonNode.Parse((await files.ReadFileAsync(StorageTransportMoveService.VehiclesPath))!)));
            foreach (var path in new[] { ResourceMaterializationContract.DefinitionsPath,
                         ResourceMaterializationContract.StatePath, ResourceMaterializationContract.HistoryPath,
                         CanonicalResourceOwnerAuthorityComposer.AuthorityPath })
                Assert.NotNull(await files.ReadFileAsync(path));
            // Final canonical authority contains ledger/history-backed DATA, not a guessed
            // birth health allocation for the already-arranged historical scout.
            var definitions = ResourceDefinitionCatalog.ParseCanonical(
                await files.ReadFileAsync(ResourceMaterializationContract.DefinitionsPath), allowMissingPristine: false);
            Assert.True(definitions.IsValid);
            var state = ResourceStateContract.ParseCanonical(
                await files.ReadFileAsync(ResourceMaterializationContract.StatePath), definitions.Catalog!, allowMissingPristine: false);
            var history = ResourceHistoryState.ParseCanonical(
                await files.ReadFileAsync(ResourceMaterializationContract.HistoryPath), definitions.Catalog!, allowMissingPristine: false);
            Assert.True(state.IsValid);
            Assert.True(history.IsValid);
            var recomposed = await CanonicalResourceOwnerAuthorityComposer.ComposeAsync(
                definitions.Catalog!, path => files.ReadFileAsync(path), state.Ledger, history.History,
                CanonicalResourceOwnerAuthorityPurpose.FinalAfterImage);
            Assert.True(recomposed.IsValid, string.Join("\n", recomposed.Issues.Select(i => i.Code)));
            Assert.Equal(await files.ReadFileAsync(CanonicalResourceOwnerAuthorityComposer.AuthorityPath),
                recomposed.CanonicalAuthorityJson);
            var map = JsonNode.Parse((await files.ReadFileAsync(MortalLocationMaterializationContract.WorldMapPath))!)!.AsObject();
            var storage = JsonNode.Parse((await files.ReadFileAsync(MortalStorageAccessProjection.StatePath))!)!.AsObject();
            Assert.Empty(MortalStorageAccessProjection.ValidateCanonical(map, storage));
            var npc = JsonNode.Parse((await files.ReadFileAsync("game_state/npcs/npc_core.json"))!)!;
            Assert.Equal("npc_journey_scout", Assert.Single(npc["NPCsInScene"]!.AsArray())!["NPCId"]!.GetValue<string>());
            var projects = JsonNode.Parse((await files.ReadFileAsync("game_state/factions/faction_projects.json"))!)!;
            Assert.Equal("project_journey_bridge", Assert.Single(projects["activeProjects"]!.AsArray())!["projectId"]!.GetValue<string>());
            Assert.False(files.FileExists(LiveTurnPreparationService.PendingTurnSnapshotManifestPath));
            Assert.False(files.FileExists(PendingTurnSnapshotAuthority.AuthorityPath));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
