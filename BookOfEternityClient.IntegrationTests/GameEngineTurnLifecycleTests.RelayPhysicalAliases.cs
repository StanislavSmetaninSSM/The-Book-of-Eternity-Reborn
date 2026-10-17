using System.Security.Cryptography;
using System.Text.Json;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Theory]
    [InlineData("gm_prompt_delivery.json", true)]
    [InlineData("gm_relay_apply_receipt.json", true)]
    [InlineData("gm_prompt_delivery.json", false)]
    [InlineData("gm_relay_apply_receipt.json", false)]
    public async Task RelayOperationalRecords_RealEnginePhysicalAliasRefusesBeforeBackupPublication(string name, bool alias)
    {
        using var cleanup = new CleanupOwnedFixture(_rootPath, line => _directGachaOutput?.WriteLine(line));
        var relative = "game_state/control/" + (alias ? "GM" + name[2..] : name + ".neighbor");
        var physical = Path.Combine(_fs.GameSessionPath, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(physical)!);
        byte[] before = "{\"opaqueAliasWitness\":1}"u8.ToArray();
        await File.WriteAllBytesAsync(physical, before); // Preserve the actual on-disk spelling.
        var engine = CreateGameEngine();
        const string backupId = "relay-physical-alias";
        var mutations = new List<string>();
        _consoleMutationObserver = mutations.Add;
        Exception? failure;
        try { failure = await Record.ExceptionAsync(() => InvokePrivateTaskResultAsync(engine, "CreatePreTurnBackup", backupId)); }
        finally { _consoleMutationObserver = null; }
        var backups = Directory.GetFiles(_fs.GameSessionPath, "*.rollback." + backupId, SearchOption.AllDirectories);
        _directGachaOutput?.WriteLine(JsonSerializer.Serialize(new
        {
            Owner = "ACTUAL_ENGINE_CREATE_PRE_TURN_BACKUP", Path = relative, ExpectedRefusal = alias,
            Failure = failure?.ToString(), Mutations = mutations, BackupCount = backups.Length,
            BeforeSha256 = Convert.ToHexString(SHA256.HashData(before)),
            AfterSha256 = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(physical)))
        }));
        Assert.Equal(before, await File.ReadAllBytesAsync(physical));
        if (alias)
        {
            var rejected = Assert.IsType<InvalidDataException>(failure);
            Assert.Contains("distinct case aliases", rejected.Message, StringComparison.Ordinal);
            Assert.Contains(relative, rejected.Message, StringComparison.Ordinal);
            Assert.Empty(mutations);
            Assert.Empty(backups);
        }
        else
        {
            Assert.Null(failure);
            Assert.Equal(before, await File.ReadAllBytesAsync(physical + ".rollback." + backupId));
            Assert.NotEmpty(backups);
        }
    }
}
