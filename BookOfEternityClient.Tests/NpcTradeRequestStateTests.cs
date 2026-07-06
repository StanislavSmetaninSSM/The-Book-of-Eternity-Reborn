using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class NpcTradeRequestStateTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;

    public NpcTradeRequestStateTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-npc-trade-request-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
    }

    [Fact]
    public async Task BuildSystemReminderFragmentAsync_MortalWorld_PointsGmToNpcTradeHelper()
    {
        await _fs.WriteFileAtomicAsync(NpcTradeRequestState.PendingRequestPath, """
        {
          "requests": [
            {
              "requestId": "npc_trade_req_egor_001",
              "npcId": "npc_egor_frontier_trader",
              "npcName": "Егор",
              "merchantProfile": "GeneralGoods",
              "tradeCycleId": "world_trade_0",
              "derivedTradeSlotCount": 6,
              "createdAtTurn": 8,
              "createdAtUtc": "2026-07-06T00:00:00Z",
              "createdAtWorldDate": 100,
              "refreshAfterWorldDate": 43200
            }
          ]
        }
        """);

        var reminder = await NpcTradeRequestState.BuildSystemReminderFragmentAsync(_fs, "Mortal World");

        Assert.NotNull(reminder);
        Assert.Contains("Complete-BoeNpcTradeInventoryRequest", reminder!, StringComparison.Ordinal);
        Assert.Contains("npc_trade_req_egor_001", reminder, StringComparison.Ordinal);
        Assert.Contains("initialId", reminder, StringComparison.Ordinal);
        Assert.Contains("requestId", reminder, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
            Directory.Delete(_rootPath, recursive: true);
    }
}
