using System.IO;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ShiningTradeRequestStateTests
{
    [Fact]
    public async Task BuildSystemReminderFragmentAsync_ListsPendingTradeRequests()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();

            await ShiningTradeRequestState.WriteRequestAsync(fs, new ShiningTradeRequestState.PendingShiningTradeInventoryRequest
            {
                RequestId = "trade_req_old_house",
                FactionId = "faction_old",
                FactionName = "Старый Дом",
                TradeCycleId = "shining_return_2",
                DerivedTradeTier = 2,
                DerivedTradeSlotCount = 6,
                DerivedRarityCeiling = "rare",
                DerivedServiceMultiplier = 1.25,
                MerchantProfile = ShiningTradeRequestState.MerchantProfileShiningFaction,
                CreatedAtTurn = 11,
                CreatedAtUtc = "2026-04-27T11:00:00Z"
            });

            var reminder = await ShiningTradeRequestState.BuildSystemReminderFragmentAsync(fs, "Shining Abode");

            Assert.NotNull(reminder);
            Assert.Contains("SHINING TRADE REQUESTS:", reminder);
            Assert.Contains("Старый Дом", reminder);
            Assert.Contains("slots 6", reminder, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Full pending trade DTO", reminder);
            Assert.Contains("\"requestId\": \"trade_req_old_house\"", reminder);
            Assert.Contains("\"derivedServiceMultiplier\": 1.25", reminder);
            Assert.Contains("\"merchantProfile\": \"shining_faction\"", reminder);
            Assert.Contains("\"createdAtUtc\": \"2026-04-27T11:00:00Z\"", reminder);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task BuildSystemReminderFragmentAsync_ChaosSeaTreatsPendingTradeAsWrongRealm()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();

            await ShiningTradeRequestState.WriteRequestAsync(fs, new ShiningTradeRequestState.PendingShiningTradeInventoryRequest
            {
                RequestId = "trade_req_old_house",
                FactionId = "faction_old",
                FactionName = "Старый Дом",
                TradeCycleId = "shining_return_2",
                DerivedTradeTier = 2,
                DerivedTradeSlotCount = 6,
                DerivedRarityCeiling = "rare",
                DerivedServiceMultiplier = 1.25,
                MerchantProfile = ShiningTradeRequestState.MerchantProfileShiningFaction,
                CreatedAtTurn = 11,
                CreatedAtUtc = "2026-04-27T11:00:00Z"
            });

            var reminder = await ShiningTradeRequestState.BuildSystemReminderFragmentAsync(fs, "Chaos Sea");

            Assert.NotNull(reminder);
            Assert.Contains("WRONG REALM", reminder, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("repair-only", reminder, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("trade_req_old_house", reminder, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Resolve each request in accepted turn", reminder, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task BuildSystemReminderFragmentAsync_DoesNotTruncatePendingTradeRequests()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();

            var requests = Enumerable.Range(1, 6)
                .Select(index => new ShiningTradeRequestState.PendingShiningTradeInventoryRequest
                {
                    RequestId = $"trade_req_{index}",
                    FactionId = $"faction_{index}",
                    FactionName = $"Фракция {index}",
                    TradeCycleId = $"shining_return_{index}",
                    DerivedTradeTier = 2,
                    DerivedTradeSlotCount = 6,
                    DerivedRarityCeiling = "rare",
                    DerivedServiceMultiplier = 1.25,
                    CreatedAtTurn = index
                })
                .ToArray();
            await ShiningTradeRequestState.WriteRequestsAsync(fs, requests);

            var reminder = await ShiningTradeRequestState.BuildSystemReminderFragmentAsync(fs, "Shining Abode");

            Assert.NotNull(reminder);
            Assert.Contains("trade_req_1", reminder);
            Assert.Contains("trade_req_6", reminder);
            Assert.Contains("Фракция 6", reminder);
            Assert.DoesNotContain("more pending", reminder, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("...", reminder, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task EnsureHealthyAsync_ChaosSeaPreservesActivePendingRequests()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();

            await ShiningTradeRequestState.WriteRequestAsync(fs, new ShiningTradeRequestState.PendingShiningTradeInventoryRequest
            {
                FactionId = "faction_old",
                FactionName = "Старый Дом",
                TradeCycleId = "shining_return_2",
                DerivedTradeTier = 2,
                DerivedTradeSlotCount = 6,
                DerivedRarityCeiling = "rare",
                DerivedServiceMultiplier = 1.25,
                CreatedAtTurn = 10
            });

            await ShiningTradeRequestState.EnsureHealthyAsync(fs, "Chaos Sea");

            Assert.Single(await ShiningTradeRequestState.ReadRequestsAsync(fs));
            Assert.True(fs.FileExists(ShiningTradeRequestState.PendingRequestsPath));
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task EnsureHealthyAsync_UnresolvedRealmPreservesPendingRequests()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();

            await ShiningTradeRequestState.WriteRequestAsync(fs, new ShiningTradeRequestState.PendingShiningTradeInventoryRequest
            {
                FactionId = "faction_old",
                FactionName = "Старый Дом",
                TradeCycleId = "shining_return_2",
                DerivedTradeTier = 2,
                DerivedTradeSlotCount = 6,
                DerivedRarityCeiling = "rare",
                DerivedServiceMultiplier = 1.25,
                CreatedAtTurn = 10
            });

            await ShiningTradeRequestState.EnsureHealthyAsync(fs, "");

            Assert.Single(await ShiningTradeRequestState.ReadRequestsAsync(fs));
            Assert.True(fs.FileExists(ShiningTradeRequestState.PendingRequestsPath));
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task EnsureHealthyAsync_SealedShiningPreservesNonEmptyTradeRequests()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();
            await WriteMinimalShiningTradeStateAsync(fs, factionStrength: 62);
            var shiningRoot = JsonNode.Parse(await fs.ReadFileAsync(ShiningAbodeState.StatePath)!)!.AsObject();
            shiningRoot["availability"] = ShiningAbodeState.AvailabilitySealedUntilNextAscension;
            await fs.WriteFileAtomicAsync(ShiningAbodeState.StatePath, shiningRoot.ToJsonString());

            await ShiningTradeRequestState.WriteRequestAsync(fs, new ShiningTradeRequestState.PendingShiningTradeInventoryRequest
            {
                RequestId = "trade_req_sealed",
                FactionId = "faction_old",
                FactionName = "Старый Дом",
                TradeCycleId = "shining_return_2",
                DerivedTradeTier = 2,
                DerivedTradeSlotCount = 6,
                DerivedRarityCeiling = "rare",
                DerivedServiceMultiplier = 1.25,
                CreatedAtTurn = 10
            });

            await ShiningTradeRequestState.EnsureHealthyAsync(fs, "Shining Abode");

            Assert.True(fs.FileExists(ShiningTradeRequestState.PendingRequestsPath));
            var request = Assert.Single(await ShiningTradeRequestState.ReadRequestsAsync(fs));
            Assert.Equal("trade_req_sealed", request.RequestId);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task EnsureHealthyAsync_PendingBootstrapPreservesTradeRequestAndShowsBlockingReminder()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();
            await WriteMinimalShiningTradeStateAsync(fs, factionStrength: 62);

            var shiningRoot = JsonNode.Parse(await fs.ReadFileAsync(ShiningAbodeState.StatePath)!)!.AsObject();
            shiningRoot["preparedIncarnationPackage"] = CreateValidPreparedPackage();
            await fs.WriteFileAtomicAsync(ShiningAbodeState.StatePath, shiningRoot.ToJsonString());
            await ShiningTradeRequestState.WriteRequestAsync(fs, new ShiningTradeRequestState.PendingShiningTradeInventoryRequest
            {
                RequestId = "trade_req_blocked_handoff",
                FactionId = "faction_old",
                FactionName = "Старый Дом",
                TradeCycleId = "shining_return_2",
                DerivedTradeTier = 2,
                DerivedTradeSlotCount = 6,
                DerivedRarityCeiling = "rare",
                DerivedServiceMultiplier = 1.25,
                MerchantProfile = ShiningTradeRequestState.MerchantProfileShiningFaction,
                CreatedAtTurn = 10
            });

            await ShiningTradeRequestState.EnsureHealthyAsync(fs, "Shining Abode");

            Assert.True(fs.FileExists(ShiningTradeRequestState.PendingRequestsPath));
            var request = Assert.Single(await ShiningTradeRequestState.ReadRequestsAsync(fs));
            Assert.Equal("trade_req_blocked_handoff", request.RequestId);
            var reminder = await ShiningTradeRequestState.BuildSystemReminderFragmentAsync(fs, "Shining Abode");
            Assert.NotNull(reminder);
            Assert.Contains("SHINING TRADE REQUESTS BLOCKED", reminder);
            Assert.Contains("pending-bootstrap handoff", reminder, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("trade_req_blocked_handoff", reminder);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task EnsureHealthyAsync_MalformedPendingFile_PreservesCorruptedContract()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();
            await WriteMinimalShiningTradeStateAsync(fs, factionStrength: 62);
            await fs.WriteFileAtomicAsync(ShiningTradeRequestState.PendingRequestsPath, "{ not valid json");

            await ShiningTradeRequestState.EnsureHealthyAsync(fs, "Shining Abode");

            Assert.True(fs.FileExists(ShiningTradeRequestState.PendingRequestsPath));
            var reminder = await ShiningTradeRequestState.BuildSystemReminderFragmentAsync(fs, "Shining Abode");
            Assert.NotNull(reminder);
            Assert.Contains("CORRUPTION", reminder, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task EnsureHealthyAsync_WhitespacePendingFile_PreservesCorruptedContract()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();
            await WriteMinimalShiningTradeStateAsync(fs, factionStrength: 62);
            await fs.WriteFileAtomicAsync(ShiningTradeRequestState.PendingRequestsPath, "   ");

            await ShiningTradeRequestState.EnsureHealthyAsync(fs, "Shining Abode");

            Assert.True(fs.FileExists(ShiningTradeRequestState.PendingRequestsPath));
            var reminder = await ShiningTradeRequestState.BuildSystemReminderFragmentAsync(fs, "Shining Abode");
            Assert.NotNull(reminder);
            Assert.Contains("CORRUPTION", reminder, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task ValidateRequestAgainstCurrentStateAsync_MalformedPendingFile_FailsClosed()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();
            await WriteMinimalShiningTradeStateAsync(fs, factionStrength: 62);
            await fs.WriteFileAtomicAsync(ShiningTradeRequestState.PendingRequestsPath, "{");

            var error = await ShiningTradeRequestState.ValidateRequestAgainstCurrentStateAsync(fs, new ShiningTradeRequestState.PendingShiningTradeInventoryRequest
            {
                FactionId = "faction_old",
                FactionName = "Старый Дом",
                TradeCycleId = "shining_return_2",
                DerivedTradeTier = 2,
                DerivedTradeSlotCount = 6,
                DerivedRarityCeiling = "rare",
                DerivedServiceMultiplier = 1.25,
                CreatedAtTurn = 10
            });

            Assert.NotNull(error);
            Assert.Contains("pending_shining_trade_inventory_requests.json", error, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task WriteRequestAsync_MalformedExistingFile_ThrowsAndPreservesCorruption()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();
            await fs.WriteFileAtomicAsync(ShiningTradeRequestState.PendingRequestsPath, "{");

            await Assert.ThrowsAsync<InvalidOperationException>(() => ShiningTradeRequestState.WriteRequestAsync(fs, new ShiningTradeRequestState.PendingShiningTradeInventoryRequest
            {
                FactionId = "faction_old",
                FactionName = "Старый Дом",
                TradeCycleId = "shining_return_2",
                DerivedTradeTier = 2,
                DerivedTradeSlotCount = 6,
                DerivedRarityCeiling = "rare",
                DerivedServiceMultiplier = 1.25,
                CreatedAtTurn = 10
            }));

            Assert.Equal("{", await fs.ReadFileAsync(ShiningTradeRequestState.PendingRequestsPath));
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task HasReadyInventoryForCurrentContract_LocalSoldOutDriftFailsReadyContract()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();
            await WriteMinimalShiningTradeStateAsync(fs, factionStrength: 62, withReadyInventory: true);

            var shiningRoot = JsonNode.Parse(await fs.ReadFileAsync(ShiningAbodeState.StatePath)!)!.AsObject();
            var faction = shiningRoot["factions"]!.AsArray()[0]!.AsObject();
            faction["tradeInventory"]!["items"]!.AsArray()[0]!.AsObject()["soldOut"] = true;

            var ready = ShiningTradeRequestState.HasReadyInventoryForCurrentContract(
                faction,
                new ShiningTradeRequestState.PendingShiningTradeInventoryRequest
                {
                    RequestId = "shining_trade_existing",
                    FactionId = "faction_old",
                    FactionName = "Старый Дом",
                    TradeCycleId = "shining_return_2",
                    DerivedTradeTier = 2,
                    DerivedTradeSlotCount = 6,
                    DerivedRarityCeiling = "rare",
                    DerivedServiceMultiplier = 1.25,
                    MerchantProfile = ShiningTradeRequestState.MerchantProfileShiningFaction
                });

            Assert.False(ready);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task ReadTradeViewAsync_MismatchedPendingRequestId_DoesNotUnlockBuying()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();
            await WriteMinimalShiningTradeStateAsync(fs, factionStrength: 62, withReadyInventory: true);

            await ShiningTradeRequestState.WriteRequestAsync(fs, new ShiningTradeRequestState.PendingShiningTradeInventoryRequest
            {
                RequestId = "shining_trade_expected",
                FactionId = "faction_old",
                FactionName = "Старый Дом",
                TradeCycleId = "shining_return_2",
                DerivedTradeTier = 2,
                DerivedTradeSlotCount = 6,
                DerivedRarityCeiling = "rare",
                DerivedServiceMultiplier = 1.25,
                MerchantProfile = ShiningTradeRequestState.MerchantProfileShiningFaction,
                CreatedAtTurn = 10
            });

            var shiningRoot = JsonNode.Parse(await fs.ReadFileAsync(ShiningAbodeState.StatePath)!)!.AsObject();
            var receipt = shiningRoot["factions"]!.AsArray()[0]!["tradeInventoryReceipts"]!.AsArray()[0]!.AsObject();
            receipt["requestId"] = "shining_trade_other";
            await fs.WriteFileAtomicAsync(ShiningAbodeState.StatePath, shiningRoot.ToJsonString());

            var view = await ShiningTradeService.ReadTradeViewAsync(fs, "faction_old");

            Assert.NotNull(view);
            Assert.False(view!.InventoryReady);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task ReadTradeViewAsync_DuplicateSameCyclePendingRequests_BlocksRuntimeReadiness()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();
            await WriteMinimalShiningTradeStateAsync(fs, factionStrength: 62, withReadyInventory: true);

            await ShiningTradeRequestState.WriteRequestsAsync(fs, new[]
            {
                new ShiningTradeRequestState.PendingShiningTradeInventoryRequest
                {
                    RequestId = "shining_trade_first",
                    FactionId = "faction_old",
                    FactionName = "Старый Дом",
                    TradeCycleId = "shining_return_2",
                    DerivedTradeTier = 2,
                    DerivedTradeSlotCount = 6,
                    DerivedRarityCeiling = "rare",
                    DerivedServiceMultiplier = 1.25,
                    MerchantProfile = ShiningTradeRequestState.MerchantProfileShiningFaction,
                    CreatedAtTurn = 10
                },
                new ShiningTradeRequestState.PendingShiningTradeInventoryRequest
                {
                    RequestId = "shining_trade_second",
                    FactionId = "faction_old",
                    FactionName = "Старый Дом",
                    TradeCycleId = "shining_return_2",
                    DerivedTradeTier = 2,
                    DerivedTradeSlotCount = 6,
                    DerivedRarityCeiling = "rare",
                    DerivedServiceMultiplier = 1.25,
                    MerchantProfile = ShiningTradeRequestState.MerchantProfileShiningFaction,
                    CreatedAtTurn = 10
                }
            });

            var view = await ShiningTradeService.ReadTradeViewAsync(fs, "faction_old");

            Assert.NotNull(view);
            Assert.False(view!.InventoryReady);
            Assert.Contains("несколько ожидающих запросов", view.InventoryStatusMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task ReadTradeViewAndBuyAsync_MalformedPendingRequestBlocksReadyInventory()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();
            await WriteMinimalShiningTradeStateAsync(fs, factionStrength: 62, withReadyInventory: true);
            await fs.WriteFileAtomicAsync(ShiningTradeRequestState.PendingRequestsPath, "{}");

            var view = await ShiningTradeService.ReadTradeViewAsync(fs, "faction_old");
            var buy = await ShiningTradeService.BuyAsync(fs, "faction_old", "slot_1", currentTurn: 11);

            Assert.NotNull(view);
            Assert.False(view!.InventoryReady);
            Assert.True(view.InventoryRequestPending);
            Assert.Contains("поврежд", view.InventoryStatusMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            Assert.False(buy.Success);
            Assert.Contains("поврежд", buy.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task EnsureHealthyAsync_DuplicateReadyReceipts_DoNotClearPendingRequest()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();
            await WriteMinimalShiningTradeStateAsync(fs, factionStrength: 62, withReadyInventory: true);

            var pendingRequest = new ShiningTradeRequestState.PendingShiningTradeInventoryRequest
            {
                RequestId = "shining_trade_existing",
                FactionId = "faction_old",
                FactionName = "Старый Дом",
                TradeCycleId = "shining_return_2",
                DerivedTradeTier = 2,
                DerivedTradeSlotCount = 6,
                DerivedRarityCeiling = "rare",
                DerivedServiceMultiplier = 1.25,
                MerchantProfile = ShiningTradeRequestState.MerchantProfileShiningFaction,
                CreatedAtTurn = 10
            };
            await ShiningTradeRequestState.WriteRequestAsync(fs, pendingRequest);

            var shiningRoot = JsonNode.Parse(await fs.ReadFileAsync(ShiningAbodeState.StatePath)!)!.AsObject();
            var receipts = shiningRoot["factions"]!.AsArray()[0]!["tradeInventoryReceipts"]!.AsArray();
            receipts.Add(receipts[0]!.DeepClone());
            await fs.WriteFileAtomicAsync(ShiningAbodeState.StatePath, shiningRoot.ToJsonString());

            await ShiningTradeRequestState.EnsureHealthyAsync(fs, "Shining Abode");

            var requests = await ShiningTradeRequestState.ReadRequestsAsync(fs);
            var remaining = Assert.Single(requests);
            Assert.Equal("shining_trade_existing", remaining.RequestId);

            var view = await ShiningTradeService.ReadTradeViewAsync(fs, "faction_old");
            Assert.NotNull(view);
            Assert.False(view!.InventoryReady);
            Assert.True(view.InventoryRequestPending);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task BuyAsync_LocalPurchase_KeepsSameCycleInventoryReady()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();
            await WriteMinimalShiningTradeStateAsync(fs, factionStrength: 62, withReadyInventory: true);

            var result = await ShiningTradeService.BuyAsync(fs, "faction_old", "slot_1", currentTurn: 11);
            var view = await ShiningTradeService.ReadTradeViewAsync(fs, "faction_old");

            Assert.True(result.Success);
            Assert.NotNull(view);
            Assert.True(view!.InventoryReady);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task BuyAsync_BlocksActiveGmTurnLifecycleWithoutMutatingState()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();
            await WriteMinimalShiningTradeStateAsync(fs, factionStrength: 62, withReadyInventory: true);
            await fs.WriteFileAtomicAsync("input/turn_request.json", new JsonObject
            {
                ["turnNumber"] = 11,
                ["playerAction"] = "Shining turn awaiting GM response"
            }.ToJsonString());
            await fs.WriteFileAtomicAsync("game_state/control/pending_turn_snapshot.json", new JsonObject
            {
                ["turnNumber"] = 11
            }.ToJsonString());

            var beforeShiningJson = await fs.ReadFileAsync(ShiningAbodeState.StatePath);
            var beforeSoulJson = await fs.ReadFileAsync("game_state/meta/soul_state.json");

            var result = await ShiningTradeService.BuyAsync(fs, "faction_old", "slot_1", currentTurn: 11);

            Assert.False(result.Success);
            Assert.False(result.StateChanged);
            Assert.Contains("GM-turn", result.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(beforeShiningJson, await fs.ReadFileAsync(ShiningAbodeState.StatePath));
            Assert.Equal(beforeSoulJson, await fs.ReadFileAsync("game_state/meta/soul_state.json"));
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task BuyAsync_BlocksUnresolvedShiningCostContractsWithoutMutatingState()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();
            await WriteMinimalShiningTradeStateAsync(fs, factionStrength: 62, withReadyInventory: true);
            await ShiningFactionRequestState.WriteFoundingRequestAsync(
                fs,
                new ShiningFactionRequestState.PendingShiningFactionFoundingRequest
                {
                    RequestId = "founding_blocks_trade_buy_001",
                    ProposedFactionId = "faction_reserved",
                    ProposedHallId = "hall_reserved",
                    ProposedHallName = "Зал Резерва",
                    ProposedHallDescription = "Проверяет блокировку локальной покупки.",
                    ProposedHallServiceTags = new List<string> { ShiningAbodeState.HallServiceTagSocial },
                    Charter = new ShiningFactionRequestState.FactionCharterPayload
                    {
                        FactionName = "Резерв",
                        FavoredArchetype = ShiningAbodeState.ProjectArchetypeAccord,
                        PatronEffectFamily = ShiningAbodeState.EffectFamilySocial,
                        Summary = "Заявка уже зарезервировала цену."
                    },
                    SupportingResidentIds = new List<string> { "resident_a", "resident_b", "resident_c" },
                    QuotedCostFeathers = ShiningFactionRequestState.FactionFoundingCostFeathers,
                    QuotedCostLightSparks = ShiningFactionRequestState.FactionFoundingCostLightSparks,
                    ReservedInkFeathersBefore = 80,
                    ReservedLightSparksBefore = 100,
                    CreatedAtTurn = 12
                });

            var beforeShiningJson = await fs.ReadFileAsync(ShiningAbodeState.StatePath);
            var beforeSoulJson = await fs.ReadFileAsync("game_state/meta/soul_state.json");

            var result = await ShiningTradeService.BuyAsync(fs, "faction_old", "slot_1", currentTurn: 11);

            Assert.False(result.Success);
            Assert.False(result.StateChanged);
            Assert.Contains("pending", result.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("founding_blocks_trade_buy_001", result.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(beforeShiningJson, await fs.ReadFileAsync(ShiningAbodeState.StatePath));
            Assert.Equal(beforeSoulJson, await fs.ReadFileAsync("game_state/meta/soul_state.json"));
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task BuyAsync_BlocksCostBearingCoreRequestWithMissingQuoteFieldsWithoutMutatingState()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();
            await WriteMinimalShiningTradeStateAsync(fs, factionStrength: 62, withReadyInventory: true);
            await fs.WriteFileAtomicAsync(ShiningCoreActionRequestState.PendingActionsRequestPath, """
            {
              "requests": [
                {
                  "requestId": "core_missing_quote_blocks_trade_buy_001",
                  "actionType": "pull_relic_gacha",
                  "factionId": "faction_old",
                  "factionName": "Старый Дом",
                  "returnCycleId": "shining_return_2",
                  "projectedGachaBonusSteps": 0,
                  "createdAtTurn": 12,
                  "createdAtUtc": "2026-04-17T00:12:00Z"
                }
              ]
            }
            """);

            var beforeShiningJson = await fs.ReadFileAsync(ShiningAbodeState.StatePath);
            var beforeSoulJson = await fs.ReadFileAsync("game_state/meta/soul_state.json");

            var result = await ShiningTradeService.BuyAsync(fs, "faction_old", "slot_1", currentTurn: 11);

            Assert.False(result.Success);
            Assert.False(result.StateChanged);
            Assert.Contains("quotedCostFeathers", result.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("core_missing_quote_blocks_trade_buy_001", result.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(beforeShiningJson, await fs.ReadFileAsync(ShiningAbodeState.StatePath));
            Assert.Equal(beforeSoulJson, await fs.ReadFileAsync("game_state/meta/soul_state.json"));
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task BuyAsync_LegacyNumericInkFeathers_PersistsCanonicalObjectShape()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();
            await WriteMinimalShiningTradeStateAsync(fs, factionStrength: 62, withReadyInventory: true);

            var soulRoot = JsonNode.Parse(await fs.ReadFileAsync("game_state/meta/soul_state.json")!)!.AsObject();
            soulRoot["inkFeathers"] = 80;
            await fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", soulRoot.ToJsonString());

            var result = await ShiningTradeService.BuyAsync(fs, "faction_old", "slot_1", currentTurn: 11);
            var soulRaw = await fs.ReadFileAsync("game_state/meta/soul_state.json");

            Assert.True(result.Success);
            Assert.NotNull(soulRaw);
            Assert.Contains("\"inkFeathers\": {", soulRaw, StringComparison.Ordinal);
            Assert.Contains("\"current\": 10", soulRaw, StringComparison.Ordinal);
            Assert.DoesNotContain("\"inkFeathers\": 10", soulRaw, StringComparison.Ordinal);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task BuyAsync_ExistingRelicId_FailsWithoutOverwritingSoulRelic()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();
            await WriteMinimalShiningTradeStateAsync(fs, factionStrength: 62, withReadyInventory: true);

            var soulRoot = JsonNode.Parse(await fs.ReadFileAsync("game_state/meta/soul_state.json")!)!.AsObject();
            soulRoot["soulRelics"]!["stored"]!.AsArray().Add(new JsonObject
            {
                ["relicId"] = "relic_trade_1",
                ["name"] = "Already Owned",
                ["quality"] = "Rare"
            });
            await fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", soulRoot.ToJsonString());

            var result = await ShiningTradeService.BuyAsync(fs, "faction_old", "slot_1", currentTurn: 11);
            var postSoulRoot = JsonNode.Parse(await fs.ReadFileAsync("game_state/meta/soul_state.json")!)!.AsObject();
            var stored = postSoulRoot["soulRelics"]!["stored"]!.AsArray();
            var postShiningRoot = JsonNode.Parse(await fs.ReadFileAsync(ShiningAbodeState.StatePath)!)!.AsObject();
            var slot = postShiningRoot["factions"]!.AsArray()[0]!["tradeInventory"]!["items"]!.AsArray()[0]!.AsObject();

            Assert.False(result.Success);
            Assert.Contains("уже есть у души", result.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Single(stored);
            Assert.Equal("Already Owned", stored[0]!["name"]!.GetValue<string>());
            Assert.Equal(80, postSoulRoot["inkFeathers"]!["current"]!.GetValue<int>());
            Assert.False(slot["soldOut"]!.GetValue<bool>());
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task BuyAsync_WhenSoulWriteFails_RollsBackShiningInventoryState()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();
            await WriteMinimalShiningTradeStateAsync(fs, factionStrength: 62, withReadyInventory: true);

            var preBuyShiningJson = await fs.ReadFileAsync(ShiningAbodeState.StatePath);
            var preBuySoulJson = await fs.ReadFileAsync("game_state/meta/soul_state.json");
            using var soulLock = File.Open(
                fs.ResolvePath("game_state/meta/soul_state.json"),
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);

            var result = await ShiningTradeService.BuyAsync(fs, "faction_old", "slot_1", currentTurn: 11);
            var postBuyShiningRoot = JsonNode.Parse(await fs.ReadFileAsync(ShiningAbodeState.StatePath)!)!.AsObject();
            var postBuyFaction = postBuyShiningRoot["factions"]!.AsArray()[0]!.AsObject();
            var postBuySlot = postBuyFaction["tradeInventory"]!["items"]!.AsArray()[0]!.AsObject();

            Assert.False(result.Success);
            Assert.Contains("состоянием души и Обители", result.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("откатилось к исходной версии", result.Message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("требует ручной сверки", result.Message, StringComparison.OrdinalIgnoreCase);
            Assert.NotNull(preBuyShiningJson);
            Assert.NotNull(preBuySoulJson);
            Assert.False(postBuySlot["soldOut"]!.GetValue<bool>());
            var restoredShiningRoot = JsonNode.Parse(await fs.ReadFileAsync(ShiningAbodeState.StatePath)!)!.AsObject();
            var restoredReceipt = restoredShiningRoot["factions"]!.AsArray()[0]!["tradeInventoryReceipts"]!.AsArray()[0]!.AsObject();
            Assert.Equal(0, restoredReceipt["soldOutCount"]!.GetValue<int>());

            var restoredSoulRoot = JsonNode.Parse(await fs.ReadFileAsync("game_state/meta/soul_state.json")!)!.AsObject();
            Assert.Equal("Shining Abode", restoredSoulRoot["currentRealm"]!.GetValue<string>());
            Assert.Equal(80, restoredSoulRoot["inkFeathers"]!["current"]!.GetValue<int>());
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task EnsureHealthyAsync_SameCycleMismatchedReceipt_DoesNotClearPendingRequest()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();
            await WriteMinimalShiningTradeStateAsync(fs, factionStrength: 62, withReadyInventory: true);

            var pendingRequest = new ShiningTradeRequestState.PendingShiningTradeInventoryRequest
            {
                RequestId = "shining_trade_expected",
                FactionId = "faction_old",
                FactionName = "Старый Дом",
                TradeCycleId = "shining_return_2",
                DerivedTradeTier = 2,
                DerivedTradeSlotCount = 6,
                DerivedRarityCeiling = "rare",
                DerivedServiceMultiplier = 1.25,
                MerchantProfile = ShiningTradeRequestState.MerchantProfileShiningFaction,
                CreatedAtTurn = 10
            };
            await ShiningTradeRequestState.WriteRequestAsync(fs, pendingRequest);

            var shiningRoot = JsonNode.Parse(await fs.ReadFileAsync(ShiningAbodeState.StatePath)!)!.AsObject();
            var receipt = shiningRoot["factions"]!.AsArray()[0]!["tradeInventoryReceipts"]!.AsArray()[0]!.AsObject();
            receipt["requestId"] = "shining_trade_other";
            await fs.WriteFileAtomicAsync(ShiningAbodeState.StatePath, shiningRoot.ToJsonString());

            await ShiningTradeRequestState.EnsureHealthyAsync(fs, "Shining Abode");

            var requests = await ShiningTradeRequestState.ReadRequestsAsync(fs);
            var remaining = Assert.Single(requests);
            Assert.Equal("shining_trade_expected", remaining.RequestId);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task ValidateRequestAgainstCurrentStateAsync_InvalidAvailability_FailsClosed()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();
            await WriteMinimalShiningTradeStateAsync(fs, factionStrength: 62);

            var shiningRoot = JsonNode.Parse(await fs.ReadFileAsync(ShiningAbodeState.StatePath)!)!.AsObject();
            shiningRoot["availability"] = "broken_mode";
            await fs.WriteFileAtomicAsync(ShiningAbodeState.StatePath, shiningRoot.ToJsonString());

            var error = await ShiningTradeRequestState.ValidateRequestAgainstCurrentStateAsync(fs, new ShiningTradeRequestState.PendingShiningTradeInventoryRequest
            {
                FactionId = "faction_old",
                FactionName = "Старый Дом",
                TradeCycleId = "shining_return_2",
                DerivedTradeTier = 2,
                DerivedTradeSlotCount = 6,
                DerivedRarityCeiling = "rare",
                DerivedServiceMultiplier = 1.25,
                CreatedAtTurn = 10
            });

            Assert.NotNull(error);
            Assert.Contains("availability", error, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task ReadTradeViewAndBuyAsync_InvalidRawOwnerState_FailClosed()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();
            await WriteMinimalShiningTradeStateAsync(fs, factionStrength: 62, withReadyInventory: true);

            var shiningRoot = JsonNode.Parse(await fs.ReadFileAsync(ShiningAbodeState.StatePath)!)!.AsObject();
            var faction = shiningRoot["factions"]!.AsArray()[0]!.AsObject();
            faction["leadership"]!["leadershipState"] = "broken_state";
            await fs.WriteFileAtomicAsync(ShiningAbodeState.StatePath, shiningRoot.ToJsonString());

            var view = await ShiningTradeService.ReadTradeViewAsync(fs, "faction_old");
            var buy = await ShiningTradeService.BuyAsync(fs, "faction_old", "slot_1", currentTurn: 11);

            Assert.NotNull(view);
            Assert.True(view!.TradeBlocked);
            Assert.Contains("actionable", view.BlockReason ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            Assert.False(view.InventoryReady);
            Assert.False(buy.Success);
            Assert.Contains("actionable", buy.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task ValidateRequestAgainstCurrentStateAsync_DormantFaction_Fails()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();
            await WriteMinimalShiningTradeStateAsync(fs, factionStrength: 20);

            var error = await ShiningTradeRequestState.ValidateRequestAgainstCurrentStateAsync(fs, new ShiningTradeRequestState.PendingShiningTradeInventoryRequest
            {
                FactionId = "faction_old",
                FactionName = "Старый Дом",
                TradeCycleId = "shining_return_2",
                DerivedTradeTier = 0,
                DerivedTradeSlotCount = 0,
                DerivedRarityCeiling = "none",
                DerivedServiceMultiplier = 0.75,
                CreatedAtTurn = 10
            });

            Assert.NotNull(error);
            Assert.Contains("dormant", error, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task ValidateRequestAgainstCurrentStateAsync_ReadyInventoryCurrentCycle_Fails()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();
            await WriteMinimalShiningTradeStateAsync(fs, factionStrength: 62, withReadyInventory: true);

            var error = await ShiningTradeRequestState.ValidateRequestAgainstCurrentStateAsync(fs, new ShiningTradeRequestState.PendingShiningTradeInventoryRequest
            {
                FactionId = "faction_old",
                FactionName = "Старый Дом",
                TradeCycleId = "shining_return_2",
                DerivedTradeTier = 2,
                DerivedTradeSlotCount = 6,
                DerivedRarityCeiling = "rare",
                DerivedServiceMultiplier = 1.25,
                CreatedAtTurn = 10
            });

            Assert.NotNull(error);
            Assert.Contains("materialized matching", error, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task InventoryMatchesRequestContract_DuplicateSlotIds_Fails()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();
            await WriteMinimalShiningTradeStateAsync(fs, factionStrength: 62, withReadyInventory: true);

            var shiningRoot = JsonNode.Parse(await fs.ReadFileAsync(ShiningAbodeState.StatePath)!)!.AsObject();
            var faction = shiningRoot["factions"]!.AsArray()[0]!.AsObject();
            var items = faction["tradeInventory"]!["items"]!.AsArray();
            items[1]!.AsObject()["slotId"] = "slot_1";

            var matches = ShiningTradeRequestState.InventoryMatchesRequestContract(
                faction["tradeInventory"]!.AsObject(),
                new ShiningTradeRequestState.PendingShiningTradeInventoryRequest
                {
                    FactionId = "faction_old",
                    FactionName = "Старый Дом",
                    TradeCycleId = "shining_return_2",
                    DerivedTradeTier = 2,
                    DerivedTradeSlotCount = 6,
                    DerivedRarityCeiling = "rare",
                    DerivedServiceMultiplier = 1.25,
                    MerchantProfile = ShiningTradeRequestState.MerchantProfileShiningFaction
                });

            Assert.False(matches);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task InventoryMatchesRequestContract_ZeroPriceSlot_Fails()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();
            await WriteMinimalShiningTradeStateAsync(fs, factionStrength: 62, withReadyInventory: true);

            var shiningRoot = JsonNode.Parse(await fs.ReadFileAsync(ShiningAbodeState.StatePath)!)!.AsObject();
            var faction = shiningRoot["factions"]!.AsArray()[0]!.AsObject();
            var items = faction["tradeInventory"]!["items"]!.AsArray();
            items[0]!.AsObject()["priceInFeathers"] = 0;

            var matches = ShiningTradeRequestState.InventoryMatchesRequestContract(
                faction["tradeInventory"]!.AsObject(),
                new ShiningTradeRequestState.PendingShiningTradeInventoryRequest
                {
                    FactionId = "faction_old",
                    FactionName = "Старый Дом",
                    TradeCycleId = "shining_return_2",
                    DerivedTradeTier = 2,
                    DerivedTradeSlotCount = 6,
                    DerivedRarityCeiling = "rare",
                    DerivedServiceMultiplier = 1.25,
                    MerchantProfile = ShiningTradeRequestState.MerchantProfileShiningFaction
                });

            Assert.False(matches);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task InventoryMatchesRequestContract_NonObjectItem_FailsClosed()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();
            await WriteMinimalShiningTradeStateAsync(fs, factionStrength: 62, withReadyInventory: true);

            var shiningRoot = JsonNode.Parse(await fs.ReadFileAsync(ShiningAbodeState.StatePath)!)!.AsObject();
            var faction = shiningRoot["factions"]!.AsArray()[0]!.AsObject();
            var tradeInventory = faction["tradeInventory"]!.AsObject();
            var items = tradeInventory["items"]!.AsArray();
            items[0] = "not an item object";

            var matches = ShiningTradeRequestState.InventoryMatchesRequestContract(
                tradeInventory,
                new ShiningTradeRequestState.PendingShiningTradeInventoryRequest
                {
                    FactionId = "faction_old",
                    FactionName = "Старый Дом",
                    TradeCycleId = "shining_return_2",
                    DerivedTradeTier = 2,
                    DerivedTradeSlotCount = 6,
                    DerivedRarityCeiling = "rare",
                    DerivedServiceMultiplier = 1.25,
                    MerchantProfile = ShiningTradeRequestState.MerchantProfileShiningFaction
                });

            Assert.False(matches);
            Assert.Equal(-1, ShiningTradeRequestState.GetTradeInventoryItemCount(tradeInventory));
            Assert.Equal(-1, ShiningTradeRequestState.GetTradeInventorySoldOutCount(tradeInventory));
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task BuyAsync_DuplicateSlotIds_Fails()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();
            await WriteMinimalShiningTradeStateAsync(fs, factionStrength: 62, withReadyInventory: true);

            var shiningRoot = JsonNode.Parse(await fs.ReadFileAsync(ShiningAbodeState.StatePath)!)!.AsObject();
            var faction = shiningRoot["factions"]!.AsArray()[0]!.AsObject();
            var items = faction["tradeInventory"]!["items"]!.AsArray();
            items[1]!.AsObject()["slotId"] = "slot_1";
            await fs.WriteFileAtomicAsync(ShiningAbodeState.StatePath, shiningRoot.ToJsonString());

            var result = await ShiningTradeService.BuyAsync(fs, "faction_old", "slot_1", currentTurn: 11);

            Assert.False(result.Success);
            Assert.Contains("duplicate slotId", result.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task SyncAutoRefreshRequestsForCurrentCycleAsync_CreatesEligibleCurrentCycleRequests()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();
            await WriteMinimalShiningTradeStateAsync(fs, factionStrength: 62);
            await ShiningTradeRequestState.WriteRequestsAsync(fs, new[]
            {
                new ShiningTradeRequestState.PendingShiningTradeInventoryRequest
                {
                    RequestId = "old_cycle_request",
                    FactionId = "faction_old",
                    FactionName = "Старый Дом",
                    TradeCycleId = "shining_return_1",
                    DerivedTradeTier = 2,
                    DerivedTradeSlotCount = 6,
                    DerivedRarityCeiling = "rare",
                    DerivedServiceMultiplier = 1.25,
                    CreatedAtTurn = 5
                }
            });

            var result = await ShiningTradeService.SyncAutoRefreshRequestsForCurrentCycleAsync(fs, currentTurn: 10);
            var requests = await ShiningTradeRequestState.ReadRequestsAsync(fs);
            Assert.Single(requests);
            Assert.DoesNotContain(requests, request => string.Equals(request.RequestId, "old_cycle_request", StringComparison.OrdinalIgnoreCase));
            var currentCycleRequest = Assert.Single(requests.Where(request => string.Equals(request.TradeCycleId, "shining_return_2", StringComparison.OrdinalIgnoreCase)));

            Assert.True(result.StateChanged);
            Assert.Equal(1, result.CreatedRequestCount);
            Assert.Equal("shining_return_2", result.TradeCycleId);
            Assert.Equal("shining_return_2", currentCycleRequest.TradeCycleId);
            Assert.Equal("faction_old", currentCycleRequest.FactionId);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task SyncAutoRefreshRequestsForCurrentCycleAsync_RemovesOnlyStaleOldCycleRequestsWhenNoCurrentTradeIsAvailable()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();
            await WriteMinimalShiningTradeStateAsync(fs, factionStrength: 62);
            var shiningRoot = JsonNode.Parse(await fs.ReadFileAsync(ShiningAbodeState.StatePath)!)!.AsObject();
            var faction = ShiningAbodeState.EnsureFactionsArray(shiningRoot).OfType<JsonObject>().Single();
            faction["baseStrength"] = 20;
            faction["factionStrength"] = 20;
            faction.Remove("tradeInventory");
            await fs.WriteFileAtomicAsync(ShiningAbodeState.StatePath, shiningRoot.ToJsonString());
            await ShiningTradeRequestState.WriteRequestsAsync(fs, new[]
            {
                new ShiningTradeRequestState.PendingShiningTradeInventoryRequest
                {
                    RequestId = "old_cycle_request",
                    FactionId = "faction_old",
                    FactionName = "Старый Дом",
                    TradeCycleId = "shining_return_1",
                    DerivedTradeTier = 2,
                    DerivedTradeSlotCount = 6,
                    DerivedRarityCeiling = "rare",
                    DerivedServiceMultiplier = 1.25,
                    CreatedAtTurn = 5
                }
            });

            var result = await ShiningTradeService.SyncAutoRefreshRequestsForCurrentCycleAsync(fs, currentTurn: 10);
            var requests = await ShiningTradeRequestState.ReadRequestsAsync(fs);

            Assert.True(result.StateChanged);
            Assert.Equal(0, result.CreatedRequestCount);
            Assert.Equal("shining_return_2", result.TradeCycleId);
            Assert.Empty(requests);
            Assert.False(fs.FileExists(ShiningTradeRequestState.PendingRequestsPath));
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task PreviewAutoRefreshRequestsForCurrentCycleAsync_UsesProjectedReentryStateWithoutWriting()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();
            await WriteMinimalShiningTradeStateAsync(fs, factionStrength: 62);

            var projectedSoulRoot = JsonNode.Parse(await fs.ReadFileAsync("game_state/meta/soul_state.json")!)!.AsObject();
            projectedSoulRoot["currentRealm"] = "Shining Abode";
            projectedSoulRoot["currentIncarnation"] = 3;
            var projectedShiningRoot = JsonNode.Parse(await fs.ReadFileAsync(ShiningAbodeState.StatePath)!)!.AsObject();
            ShiningAbodeState.SyncShiningReturnCycle(projectedShiningRoot, 3, out _);

            var result = await ShiningTradeService.PreviewAutoRefreshRequestsForCurrentCycleAsync(
                fs,
                projectedSoulRoot,
                projectedShiningRoot,
                currentTurn: 12);

            Assert.True(result.StateChanged);
            Assert.Equal(1, result.CreatedRequestCount);
            Assert.Equal("shining_return_3", result.TradeCycleId);
            Assert.False(fs.FileExists(ShiningTradeRequestState.PendingRequestsPath));
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task WriteRequestsAsync_DuplicateRequestIds_Throws()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();

            await Assert.ThrowsAsync<InvalidOperationException>(() => ShiningTradeRequestState.WriteRequestsAsync(fs, new[]
            {
                new ShiningTradeRequestState.PendingShiningTradeInventoryRequest
                {
                    RequestId = "trade_req_shared",
                    FactionId = "faction_old",
                    FactionName = "Старый Дом",
                    TradeCycleId = "shining_return_2",
                    DerivedTradeTier = 2,
                    DerivedTradeSlotCount = 6,
                    DerivedRarityCeiling = "rare",
                    DerivedServiceMultiplier = 1.25,
                    CreatedAtTurn = 10
                },
                new ShiningTradeRequestState.PendingShiningTradeInventoryRequest
                {
                    RequestId = "trade_req_shared",
                    FactionId = "faction_old",
                    FactionName = "Старый Дом",
                    TradeCycleId = "shining_return_3",
                    DerivedTradeTier = 2,
                    DerivedTradeSlotCount = 6,
                    DerivedRarityCeiling = "rare",
                    DerivedServiceMultiplier = 1.25,
                    CreatedAtTurn = 11
                }
            }));

            Assert.False(fs.FileExists(ShiningTradeRequestState.PendingRequestsPath));
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    private static async Task WriteMinimalShiningTradeStateAsync(FileSystemManager fs, int factionStrength, bool withReadyInventory = false)
    {
        await fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", new JsonObject
        {
            ["currentRealm"] = "Shining Abode",
            ["currentIncarnation"] = 2,
            ["inkFeathers"] = new JsonObject
            {
                ["current"] = 80
            },
            ["soulRelics"] = new JsonObject
            {
                ["equipped"] = new JsonArray(),
                ["stored"] = new JsonArray()
            }
        }.ToJsonString());

        var shiningRoot = ShiningAbodeState.CreateDefaultState();
        shiningRoot["availability"] = ShiningAbodeState.AvailabilityActive;
        shiningRoot["radiance"] = new JsonObject
        {
            ["experience"] = 250,
            ["tier"] = 2
        };
        shiningRoot["factions"] = new JsonArray
        {
            new JsonObject
            {
                ["factionId"] = "faction_old",
                ["originType"] = ShiningAbodeState.OriginTypeAscendedGuardian,
                ["hallId"] = "hall_old",
                ["charter"] = new JsonObject
                {
                    ["factionName"] = "Старый Дом",
                    ["favoredArchetype"] = ShiningAbodeState.ProjectArchetypeProvision,
                    ["patronEffectFamily"] = ShiningAbodeState.EffectFamilyResource,
                    ["summary"] = "Торговая фракция."
                },
                ["leadership"] = new JsonObject
                {
                    ["headActorType"] = ShiningAbodeState.HeadActorTypeGuardian,
                    ["headActorId"] = "guardian_old",
                    ["leadershipState"] = ShiningAbodeState.LeadershipStateSecure
                },
                ["baseStrength"] = factionStrength,
                ["factionStrength"] = factionStrength,
                ["investCountThisAscension"] = 0,
                ["projectArchetypesCountedThisAscension"] = new JsonArray(),
                ["projects"] = new JsonArray(),
                ["tradeInventoryReceipts"] = new JsonArray(),
                ["leadershipReceipts"] = new JsonArray(),
                ["leadershipHistory"] = new JsonArray()
            }
        };

        if (withReadyInventory)
        {
            var faction = shiningRoot["factions"]!.AsArray()[0]!.AsObject();
            faction["tradeInventory"] = new JsonObject
            {
                ["tradeCycleId"] = "shining_return_2",
                ["generatedAtUtc"] = "2026-04-17T00:10:00Z",
                ["generationTradeTier"] = 2,
                ["generationRarityCeiling"] = "rare",
                ["serviceMultiplierSnapshot"] = 1.25,
                ["merchantProfile"] = "shining_faction",
                ["items"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["slotId"] = "slot_1",
                        ["priceInFeathers"] = 70,
                        ["soldOut"] = false,
                        ["relicData"] = new JsonObject
                        {
                            ["relicId"] = "relic_trade_1",
                            ["name"] = "Торговая Реликвия",
                            ["quality"] = "Rare"
                        }
                    },
                    new JsonObject
                    {
                        ["slotId"] = "slot_2",
                        ["priceInFeathers"] = 30,
                        ["soldOut"] = false,
                        ["relicData"] = new JsonObject
                        {
                            ["relicId"] = "relic_trade_2",
                            ["name"] = "Малая Реликвия",
                            ["quality"] = "Common"
                        }
                    },
                    new JsonObject
                    {
                        ["slotId"] = "slot_3",
                        ["priceInFeathers"] = 30,
                        ["soldOut"] = false,
                        ["relicData"] = new JsonObject
                        {
                            ["relicId"] = "relic_trade_3",
                            ["name"] = "Малая Реликвия II",
                            ["quality"] = "Common"
                        }
                    },
                    new JsonObject
                    {
                        ["slotId"] = "slot_4",
                        ["priceInFeathers"] = 30,
                        ["soldOut"] = false,
                        ["relicData"] = new JsonObject
                        {
                            ["relicId"] = "relic_trade_4",
                            ["name"] = "Малая Реликвия III",
                            ["quality"] = "Common"
                        }
                    },
                    new JsonObject
                    {
                        ["slotId"] = "slot_5",
                        ["priceInFeathers"] = 30,
                        ["soldOut"] = false,
                        ["relicData"] = new JsonObject
                        {
                            ["relicId"] = "relic_trade_5",
                            ["name"] = "Малая Реликвия IV",
                            ["quality"] = "Common"
                        }
                    },
                    new JsonObject
                    {
                        ["slotId"] = "slot_6",
                        ["priceInFeathers"] = 30,
                        ["soldOut"] = false,
                        ["relicData"] = new JsonObject
                        {
                            ["relicId"] = "relic_trade_6",
                            ["name"] = "Малая Реликвия V",
                            ["quality"] = "Common"
                        }
                    }
                }
            };
            faction["tradeInventoryReceipts"] = new JsonArray
            {
                new JsonObject
                {
                    ["requestId"] = "shining_trade_existing",
                    ["factionId"] = "faction_old",
                    ["factionName"] = "Старый Дом",
                    ["tradeCycleId"] = "shining_return_2",
                    ["status"] = "ready",
                    ["itemCount"] = 6,
                    ["soldOutCount"] = 0,
                    ["resolvedAtTurn"] = 9,
                    ["resolvedAtUtc"] = "2026-04-17T00:10:00Z"
                }
            };
        }

        await fs.WriteFileAtomicAsync(ShiningAbodeState.StatePath, shiningRoot.ToJsonString());
        await fs.WriteFileAtomicAsync(GuardianAbodeResidentState.StatePath, new JsonObject
        {
            ["entries"] = new JsonArray()
        }.ToJsonString());
        await fs.WriteFileAtomicAsync("game_state/meta/guardians.json", new JsonObject
        {
            ["guardians"] = new JsonArray
            {
                new JsonObject
                {
                    ["guardianId"] = "guardian_old",
                    ["guardianName"] = "Азалия"
                }
            }
        }.ToJsonString());
    }

    private static JsonObject CreateValidPreparedPackage() => new()
    {
        ["selectedCardIds"] = new JsonArray("card_memory"),
        ["selectedCards"] = new JsonArray(new JsonObject
        {
            ["cardId"] = "card_memory",
            ["dedupeKey"] = "memory:card_memory",
            ["sourceType"] = "project",
            ["sourceFactionId"] = "faction_old",
            ["sourceActorId"] = "project_old",
            ["effectFamily"] = "memory",
            ["rarity"] = "common",
            ["displayName"] = "Память Света",
            ["displaySummary"] = "Сохраняет эхо.",
            ["effectPayload"] = new JsonObject()
        }),
        ["generatedFromDraftVersion"] = 1,
        ["preparedAtTurn"] = 10,
        ["preparedAtUtc"] = "2026-04-17T00:10:00Z"
    };

    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-shining-trade-request-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void CleanupTempRoot(string root)
    {
        try
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
        catch
        {
            // ignored
        }
    }
}
