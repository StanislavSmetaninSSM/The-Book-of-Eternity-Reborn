using System.Security.Cryptography;
using System.Text.Json;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class LiveTurnPreparationServiceTests
{
    [Theory]
    [InlineData("gm_prompt_delivery.json", true)]
    [InlineData("gm_relay_apply_receipt.json", true)]
    [InlineData("gm_prompt_delivery.json", false)]
    [InlineData("gm_relay_apply_receipt.json", false)]
    public async Task RelayOperationalRecords_ActualLivePhysicalAliasRefusesBeforePublication(string name, bool alias)
    {
        using var cleanup = new GmRelayLongSessionTests.OwnedRoot(_rootPath, Console.WriteLine);
        await WriteCanonicalAndGeneratedHarnessFilesAsync();
        var relative = "game_state/control/" + (alias ? "GM" + name[2..] : name + ".neighbor");
        var physical = Path.Combine(_fs.GameSessionPath, relative.Replace('/', Path.DirectorySeparatorChar));
        byte[] before = "{\"opaqueAliasWitness\":1}"u8.ToArray();
        await File.WriteAllBytesAsync(physical, before); // An actual physical alias, not a logical-path mock.
        var beforeImages = RelayPhysicalImages(_fs.GameSessionPath);
        var publications = 0;
        var service = new LiveTurnPreparationService(_fs, new LiveTurnPreparationServiceHooks
        {
            BeforePublicationAsync = () => { publications++; return Task.CompletedTask; }
        });
        var failure = await Record.ExceptionAsync(() => service.PrepareAsync(new LiveTurnPreparationOptions
        {
            SessionId = "owned-relay-alias", RequestId = "owned-relay-alias-request", TurnNumber = 2,
            PlayerAction = "Осмотреть спокойную комнату.", PreGeneratedDices1d20 = [14, 8, 17]
        }));
        var afterImages = RelayPhysicalImages(_fs.GameSessionPath);
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            Owner = "ACTUAL_LIVE_PREPARE", Path = relative, ExpectedRefusal = alias,
            Failure = failure?.ToString(), PublicationWitnessCount = publications,
            BeforeFileCount = beforeImages.Count, AfterFileCount = afterImages.Count,
            ChangedPhysicalImages = beforeImages.Keys.Union(afterImages.Keys, StringComparer.Ordinal)
                .Where(path => !beforeImages.TryGetValue(path, out var left) || !afterImages.TryGetValue(path, out var right) || left != right).ToArray()
        }));
        Assert.Equal(before, await File.ReadAllBytesAsync(physical));
        if (alias)
        {
            var rejected = Assert.IsType<InvalidDataException>(failure);
            Assert.Contains("distinct case aliases", rejected.Message, StringComparison.Ordinal);
            Assert.Contains(relative, rejected.Message, StringComparison.Ordinal);
            Assert.Equal(0, publications);
            Assert.Equal(beforeImages.ToArray(), afterImages.ToArray());
        }
        else
        {
            Assert.Null(failure);
            Assert.Equal(1, publications);
            var manifest = JsonSerializer.Deserialize<LiveTurnPendingSnapshotManifest>(
                (await _fs.ReadFileAsync(LiveTurnPreparationService.PendingTurnSnapshotManifestPath))!, LiveTurnPreparationService.ManifestJsonOptions)!;
            Assert.Contains(relative, manifest.Files.Keys);
            Assert.Contains(relative, manifest.RollbackBaselineFiles);
            Assert.Equal("owned-relay-alias-request", manifest.RequestId);
        }
    }

    private static SortedDictionary<string, string> RelayPhysicalImages(string session)
    {
        var images = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var path in Directory.GetFiles(session, "*", SearchOption.AllDirectories))
            images.Add(Path.GetRelativePath(session, path), Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))));
        return images;
    }
}
