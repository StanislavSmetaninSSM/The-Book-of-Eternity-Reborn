using System.Text;
using System.Text.Json;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RelayOperationalRecords_RealRollbackPreservesLatestRecordsAndRestoresOrdinaryNeighbors(bool existedBefore)
    {
        using var cleanup = new CleanupOwnedFixture(_rootPath, line => _directGachaOutput?.WriteLine(line));
        string[] operational = ["game_state/control/gm_prompt_delivery.json", "game_state/control/gm_relay_apply_receipt.json"];
        const string ordinary = "game_state/core/relay_ordinary_neighbor.json";
        const string created = "game_state/core/relay_created_neighbor.json";
        byte[] before = Encoding.UTF8.GetBytes("{\"revision\":1}");
        byte[] after = Encoding.UTF8.GetBytes("{\"revision\":2}");
        await _fs.WriteFileAtomicBytesAsync(ordinary, before);
        if (existedBefore)
            foreach (var path in operational) await _fs.WriteFileAtomicBytesAsync(path, before);
        var engine = CreateGameEngine();
        var snapshot = await InvokePrivateTaskResultAsync(engine, "CreatePreTurnBackup", "relay-operational");
        await _fs.WriteFileAtomicBytesAsync(ordinary, after);
        await _fs.WriteFileAtomicBytesAsync(created, after);
        foreach (var path in operational) await _fs.WriteFileAtomicBytesAsync(path, after);
        await InvokePrivateTaskAsync(engine, "RestorePreTurnBackup", snapshot);
        Assert.Equal(before, await _fs.ReadFileBytesAsync(ordinary));
        Assert.False(_fs.FileExists(created));
        var issues = operational.Where(path => !File.Exists(_fs.ResolvePath(path)) || !File.ReadAllBytes(_fs.ResolvePath(path)).AsSpan().SequenceEqual(after)).ToArray();
        _directGachaOutput?.WriteLine(JsonSerializer.Serialize(new { ExistedBefore = existedBefore, ActualOrdinaryRestored = true, ActualNewOrdinaryRemoved = true, OperationalChanges = issues, RealRestoreCompleted = true }));
        Assert.Empty(issues);
    }
}
