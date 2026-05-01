using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ShiningTradeResolutionValidationTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly ValidationService _validator;

    public ShiningTradeResolutionValidationTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-shining-trade-resolution-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
        _validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
    }

    [Fact]
    public async Task ValidatePendingShiningTradeInventoryResolutionAsync_ReadyInventoryWithMatchingReceipt_Passes()
    {
        var preTurnShiningRoot = CreateBaseShiningRoot();
        var currentShiningRoot = CloneJsonObject(preTurnShiningRoot);
        var faction = currentShiningRoot["factions"]!.AsArray()[0]!.AsObject();
        faction["tradeInventory"] = new JsonObject
        {
            ["tradeCycleId"] = "shining_return_2",
            ["generatedAtUtc"] = "2026-04-17T01:00:00Z",
            ["generationTradeTier"] = 2,
            ["generationRarityCeiling"] = "rare",
            ["serviceMultiplierSnapshot"] = 1.25,
            ["merchantProfile"] = "shining_faction",
            ["items"] = new JsonArray
            {
                CreateTradeSlot("slot_1", 70, "relic_trade_1", "Rare"),
                CreateTradeSlot("slot_2", 30, "relic_trade_2", "Common"),
                CreateTradeSlot("slot_3", 30, "relic_trade_3", "Common"),
                CreateTradeSlot("slot_4", 30, "relic_trade_4", "Common"),
                CreateTradeSlot("slot_5", 30, "relic_trade_5", "Common"),
                CreateTradeSlot("slot_6", 30, "relic_trade_6", "Common")
            }
        };
        faction["tradeInventoryReceipts"] = new JsonArray
        {
            new JsonObject
            {
                ["requestId"] = "shining_trade_req_1",
                ["factionId"] = "faction_old",
                ["factionName"] = "Старый Дом",
                ["tradeCycleId"] = "shining_return_2",
                ["status"] = "ready",
                ["itemCount"] = 6,
                ["soldOutCount"] = 0,
                ["resolvedAtTurn"] = 14,
                ["resolvedAtUtc"] = "2026-04-17T01:00:00Z"
            }
        };

        await WriteNodeAsync(ShiningAbodeState.StatePath, currentShiningRoot);
        await WriteNodeAsync(GuardianAbodeResidentState.StatePath, new JsonObject
        {
            ["entries"] = new JsonArray()
        });
        await WriteNodeAsync("game_state/meta/guardians.json", new JsonObject
        {
            ["guardians"] = new JsonArray
            {
                new JsonObject
                {
                    ["guardianId"] = "guardian_old",
                    ["guardianName"] = "Азалия"
                }
            }
        });
        await WriteNodeAsync("ready/turn_complete.json", new JsonObject
        {
            ["accepted"] = true
        });

        var requestRoot = new JsonObject
        {
            [ShiningTradeRequestState.RequestsProperty] = new JsonArray
            {
                new JsonObject
                {
                    ["requestId"] = "shining_trade_req_1",
                    ["factionId"] = "faction_old",
                    ["factionName"] = "Старый Дом",
                    ["tradeCycleId"] = "shining_return_2",
                    ["derivedTradeTier"] = 2,
                    ["derivedTradeSlotCount"] = 6,
                    ["derivedRarityCeiling"] = "rare",
                    ["derivedServiceMultiplier"] = 1.25,
                    ["merchantProfile"] = "shining_faction",
                    ["createdAtTurn"] = 13,
                    ["createdAtUtc"] = "2026-04-17T00:59:00Z"
                }
            }
        };
        await WriteNodeAsync(ShiningTradeRequestState.PendingRequestsPath, requestRoot);
        await WritePendingTurnSnapshotManifestAsync(preTurnShiningRoot, requestRoot);

        var issues = await InvokeValidationAsync("ValidatePendingShiningTradeInventoryResolutionAsync");

        Assert.DoesNotContain(issues, issue => string.Equals(issue.Code, "shining_trade_request_missing_inventory_resolution", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue => string.Equals(issue.Code, "shining_trade_request_missing_receipt_resolution", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidatePendingShiningTradeInventoryResolutionAsync_WrongSoldOutSnapshot_Fails()
    {
        var preTurnShiningRoot = CreateBaseShiningRoot();
        var currentShiningRoot = CloneJsonObject(preTurnShiningRoot);
        var faction = currentShiningRoot["factions"]!.AsArray()[0]!.AsObject();
        faction["tradeInventory"] = new JsonObject
        {
            ["tradeCycleId"] = "shining_return_2",
            ["generatedAtUtc"] = "2026-04-17T01:00:00Z",
            ["generationTradeTier"] = 2,
            ["generationRarityCeiling"] = "rare",
            ["serviceMultiplierSnapshot"] = 1.25,
            ["merchantProfile"] = "shining_faction",
            ["items"] = new JsonArray
            {
                CreateTradeSlot("slot_1", 70, "relic_trade_1", "Rare"),
                CreateTradeSlot("slot_2", 30, "relic_trade_2", "Common")
            }
        };
        faction["tradeInventoryReceipts"] = new JsonArray
        {
            new JsonObject
            {
                ["requestId"] = "shining_trade_req_bad_snapshot",
                ["factionId"] = "faction_old",
                ["factionName"] = "Старый Дом",
                ["tradeCycleId"] = "shining_return_2",
                ["status"] = "ready",
                ["itemCount"] = 2,
                ["soldOutCount"] = 1,
                ["resolvedAtTurn"] = 14,
                ["resolvedAtUtc"] = "2026-04-17T01:00:00Z"
            }
        };

        await WriteNodeAsync(ShiningAbodeState.StatePath, currentShiningRoot);
        await WriteNodeAsync(GuardianAbodeResidentState.StatePath, new JsonObject { ["entries"] = new JsonArray() });
        await WriteNodeAsync("game_state/meta/guardians.json", new JsonObject
        {
            ["guardians"] = new JsonArray(new JsonObject
            {
                ["guardianId"] = "guardian_old",
                ["guardianName"] = "Азалия"
            })
        });
        await WriteNodeAsync("ready/turn_complete.json", new JsonObject { ["accepted"] = true });

        var requestRoot = new JsonObject
        {
            [ShiningTradeRequestState.RequestsProperty] = new JsonArray
            {
                new JsonObject
                {
                    ["requestId"] = "shining_trade_req_bad_snapshot",
                    ["factionId"] = "faction_old",
                    ["factionName"] = "Старый Дом",
                    ["tradeCycleId"] = "shining_return_2",
                    ["derivedTradeTier"] = 2,
                    ["derivedTradeSlotCount"] = 2,
                    ["derivedRarityCeiling"] = "rare",
                    ["derivedServiceMultiplier"] = 1.25,
                    ["merchantProfile"] = "shining_faction",
                    ["createdAtTurn"] = 13,
                    ["createdAtUtc"] = "2026-04-17T00:59:00Z"
                }
            }
        };
        await WriteNodeAsync(ShiningTradeRequestState.PendingRequestsPath, requestRoot);
        await WritePendingTurnSnapshotManifestAsync(preTurnShiningRoot, requestRoot);

        var issues = await InvokeValidationAsync("ValidatePendingShiningTradeInventoryResolutionAsync");

        Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_trade_request_missing_receipt_resolution", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidatePendingShiningTradeInventoryResolutionAsync_DuplicateMatchingReadyReceipts_Fails()
    {
        var preTurnShiningRoot = CreateBaseShiningRoot();
        var currentShiningRoot = CloneJsonObject(preTurnShiningRoot);
        var faction = currentShiningRoot["factions"]!.AsArray()[0]!.AsObject();
        faction["tradeInventory"] = new JsonObject
        {
            ["tradeCycleId"] = "shining_return_2",
            ["generatedAtUtc"] = "2026-04-17T01:00:00Z",
            ["generationTradeTier"] = 2,
            ["generationRarityCeiling"] = "rare",
            ["serviceMultiplierSnapshot"] = 1.25,
            ["merchantProfile"] = "shining_faction",
            ["items"] = new JsonArray
            {
                CreateTradeSlot("slot_1", 70, "relic_trade_1", "Rare"),
                CreateTradeSlot("slot_2", 30, "relic_trade_2", "Common"),
                CreateTradeSlot("slot_3", 30, "relic_trade_3", "Common"),
                CreateTradeSlot("slot_4", 30, "relic_trade_4", "Common"),
                CreateTradeSlot("slot_5", 30, "relic_trade_5", "Common"),
                CreateTradeSlot("slot_6", 30, "relic_trade_6", "Common")
            }
        };
        faction["tradeInventoryReceipts"] = new JsonArray
        {
            new JsonObject
            {
                ["requestId"] = "shining_trade_req_1",
                ["factionId"] = "faction_old",
                ["factionName"] = "Старый Дом",
                ["tradeCycleId"] = "shining_return_2",
                ["status"] = "ready",
                ["itemCount"] = 6,
                ["soldOutCount"] = 0,
                ["resolvedAtTurn"] = 14,
                ["resolvedAtUtc"] = "2026-04-17T01:00:00Z"
            },
            new JsonObject
            {
                ["requestId"] = "shining_trade_req_1",
                ["factionId"] = "faction_old",
                ["factionName"] = "Старый Дом",
                ["tradeCycleId"] = "shining_return_2",
                ["status"] = "ready",
                ["itemCount"] = 6,
                ["soldOutCount"] = 0,
                ["resolvedAtTurn"] = 15,
                ["resolvedAtUtc"] = "2026-04-17T01:01:00Z"
            }
        };

        await WriteNodeAsync(ShiningAbodeState.StatePath, currentShiningRoot);
        await WriteNodeAsync(GuardianAbodeResidentState.StatePath, new JsonObject { ["entries"] = new JsonArray() });
        await WriteNodeAsync("game_state/meta/guardians.json", new JsonObject
        {
            ["guardians"] = new JsonArray(new JsonObject
            {
                ["guardianId"] = "guardian_old",
                ["guardianName"] = "Азалия"
            })
        });
        await WriteNodeAsync("ready/turn_complete.json", new JsonObject { ["accepted"] = true });

        var requestRoot = new JsonObject
        {
            [ShiningTradeRequestState.RequestsProperty] = new JsonArray
            {
                new JsonObject
                {
                    ["requestId"] = "shining_trade_req_1",
                    ["factionId"] = "faction_old",
                    ["factionName"] = "Старый Дом",
                    ["tradeCycleId"] = "shining_return_2",
                    ["derivedTradeTier"] = 2,
                    ["derivedTradeSlotCount"] = 6,
                    ["derivedRarityCeiling"] = "rare",
                    ["derivedServiceMultiplier"] = 1.25,
                    ["merchantProfile"] = "shining_faction",
                    ["createdAtTurn"] = 13,
                    ["createdAtUtc"] = "2026-04-17T00:59:00Z"
                }
            }
        };
        await WriteNodeAsync(ShiningTradeRequestState.PendingRequestsPath, requestRoot);
        await WritePendingTurnSnapshotManifestAsync(preTurnShiningRoot, requestRoot);

        var issues = await InvokeValidationAsync("ValidatePendingShiningTradeInventoryResolutionAsync");

        Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_trade_request_missing_receipt_resolution", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateShiningTradeInventoryObject_DuplicateSlotIds_Fails()
    {
        var issues = new List<ValidationIssue>();
        var tradeInventory = new JsonObject
        {
            ["tradeCycleId"] = "shining_return_2",
            ["generatedAtUtc"] = "2026-04-17T01:00:00Z",
            ["generationTradeTier"] = 2,
            ["generationRarityCeiling"] = "rare",
            ["serviceMultiplierSnapshot"] = 1.25,
            ["merchantProfile"] = "shining_faction",
            ["items"] = new JsonArray
            {
                CreateTradeSlot("slot_dup", 70, "relic_trade_1", "Rare"),
                CreateTradeSlot("slot_dup", 30, "relic_trade_2", "Common")
            }
        };

        using var document = JsonDocument.Parse(tradeInventory.ToJsonString());
        var method = typeof(ValidationService).GetMethod(
            "ValidateShiningTradeInventoryObject",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(_validator, new object[] { document.RootElement, "test.tradeInventory", issues });

        Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_trade_inventory_duplicate_slot_id", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateShiningTradeInventoryObject_DuplicateRelicIds_Fails()
    {
        var issues = new List<ValidationIssue>();
        var tradeInventory = new JsonObject
        {
            ["tradeCycleId"] = "shining_return_2",
            ["generatedAtUtc"] = "2026-04-17T01:00:00Z",
            ["generationTradeTier"] = 2,
            ["generationRarityCeiling"] = "rare",
            ["serviceMultiplierSnapshot"] = 1.25,
            ["merchantProfile"] = "shining_faction",
            ["items"] = new JsonArray
            {
                CreateTradeSlot("slot_1", 70, "relic_dup", "Rare"),
                CreateTradeSlot("slot_2", 30, "relic_dup", "Common")
            }
        };

        using var document = JsonDocument.Parse(tradeInventory.ToJsonString());
        var method = typeof(ValidationService).GetMethod(
            "ValidateShiningTradeInventoryObject",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(_validator, new object[] { document.RootElement, "test.tradeInventory", issues });

        Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_trade_inventory_duplicate_relic_id", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidatePendingShiningTradeInventoryResolutionAsync_OwnedRelicIdCollision_Fails()
    {
        var preTurnShiningRoot = CreateBaseShiningRoot();
        var currentShiningRoot = CloneJsonObject(preTurnShiningRoot);
        var faction = currentShiningRoot["factions"]!.AsArray()[0]!.AsObject();
        faction["tradeInventory"] = new JsonObject
        {
            ["tradeCycleId"] = "shining_return_2",
            ["generatedAtUtc"] = "2026-04-17T01:00:00Z",
            ["generationTradeTier"] = 2,
            ["generationRarityCeiling"] = "rare",
            ["serviceMultiplierSnapshot"] = 1.25,
            ["merchantProfile"] = "shining_faction",
            ["items"] = new JsonArray
            {
                CreateTradeSlot("slot_1", 70, "relic_owned", "Rare"),
                CreateTradeSlot("slot_2", 30, "relic_trade_2", "Common"),
                CreateTradeSlot("slot_3", 30, "relic_trade_3", "Common"),
                CreateTradeSlot("slot_4", 30, "relic_trade_4", "Common"),
                CreateTradeSlot("slot_5", 30, "relic_trade_5", "Common"),
                CreateTradeSlot("slot_6", 30, "relic_trade_6", "Common")
            }
        };
        faction["tradeInventoryReceipts"] = new JsonArray
        {
            new JsonObject
            {
                ["requestId"] = "shining_trade_req_owned",
                ["factionId"] = "faction_old",
                ["factionName"] = "Старый Дом",
                ["tradeCycleId"] = "shining_return_2",
                ["status"] = "ready",
                ["itemCount"] = 6,
                ["soldOutCount"] = 0,
                ["resolvedAtTurn"] = 14,
                ["resolvedAtUtc"] = "2026-04-17T01:00:00Z"
            }
        };

        await WriteNodeAsync(ShiningAbodeState.StatePath, currentShiningRoot);
        await WriteNodeAsync("game_state/meta/soul_state.json", new JsonObject
        {
            ["soulRelics"] = new JsonObject
            {
                ["equipped"] = new JsonArray(),
                ["stored"] = new JsonArray(CreateTradeSlot("owned_source", 1, "relic_owned", "Rare")["relicData"]!.DeepClone())
            }
        });
        await WriteNodeAsync(GuardianAbodeResidentState.StatePath, new JsonObject { ["entries"] = new JsonArray() });
        await WriteNodeAsync("game_state/meta/guardians.json", new JsonObject
        {
            ["guardians"] = new JsonArray(new JsonObject
            {
                ["guardianId"] = "guardian_old",
                ["guardianName"] = "Азалия"
            })
        });
        await WriteNodeAsync("ready/turn_complete.json", new JsonObject { ["accepted"] = true });

        var requestRoot = new JsonObject
        {
            [ShiningTradeRequestState.RequestsProperty] = new JsonArray
            {
                CreatePendingTradeRequest("shining_trade_req_owned")
            }
        };
        await WriteNodeAsync(ShiningTradeRequestState.PendingRequestsPath, requestRoot);
        await WritePendingTurnSnapshotManifestAsync(preTurnShiningRoot, requestRoot);

        var issues = await InvokeValidationAsync("ValidatePendingShiningTradeInventoryResolutionAsync");

        Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_trade_inventory_owned_relic_id_collision", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidatePendingShiningTradeInventoryResolutionAsync_PreTurnOwnedRelicIdCollision_Fails()
    {
        var preTurnShiningRoot = CreateBaseShiningRoot();
        var currentShiningRoot = CloneJsonObject(preTurnShiningRoot);
        var faction = currentShiningRoot["factions"]!.AsArray()[0]!.AsObject();
        faction["tradeInventory"] = new JsonObject
        {
            ["tradeCycleId"] = "shining_return_2",
            ["generatedAtUtc"] = "2026-04-17T01:00:00Z",
            ["generationTradeTier"] = 2,
            ["generationRarityCeiling"] = "rare",
            ["serviceMultiplierSnapshot"] = 1.25,
            ["merchantProfile"] = "shining_faction",
            ["items"] = new JsonArray
            {
                CreateTradeSlot("slot_1", 70, "relic_owned_pre_turn", "Rare"),
                CreateTradeSlot("slot_2", 30, "relic_trade_2", "Common"),
                CreateTradeSlot("slot_3", 30, "relic_trade_3", "Common"),
                CreateTradeSlot("slot_4", 30, "relic_trade_4", "Common"),
                CreateTradeSlot("slot_5", 30, "relic_trade_5", "Common"),
                CreateTradeSlot("slot_6", 30, "relic_trade_6", "Common")
            }
        };
        faction["tradeInventoryReceipts"] = new JsonArray
        {
            new JsonObject
            {
                ["requestId"] = "shining_trade_req_pre_owned",
                ["factionId"] = "faction_old",
                ["factionName"] = "Старый Дом",
                ["tradeCycleId"] = "shining_return_2",
                ["status"] = "ready",
                ["itemCount"] = 6,
                ["soldOutCount"] = 0,
                ["resolvedAtTurn"] = 14,
                ["resolvedAtUtc"] = "2026-04-17T01:00:00Z"
            }
        };

        await WriteNodeAsync(ShiningAbodeState.StatePath, currentShiningRoot);
        await WriteNodeAsync("game_state/meta/soul_state.json", CreateSoulStateRoot());
        await WriteNodeAsync(GuardianAbodeResidentState.StatePath, new JsonObject { ["entries"] = new JsonArray() });
        await WriteNodeAsync("game_state/meta/guardians.json", new JsonObject
        {
            ["guardians"] = new JsonArray(new JsonObject
            {
                ["guardianId"] = "guardian_old",
                ["guardianName"] = "Азалия"
            })
        });
        await WriteNodeAsync("ready/turn_complete.json", new JsonObject { ["accepted"] = true });

        var requestRoot = new JsonObject
        {
            [ShiningTradeRequestState.RequestsProperty] = new JsonArray
            {
                CreatePendingTradeRequest("shining_trade_req_pre_owned")
            }
        };
        var preTurnSoulRoot = CreateSoulStateRoot("relic_owned_pre_turn");
        await WriteNodeAsync(ShiningTradeRequestState.PendingRequestsPath, requestRoot);
        await WritePendingTurnSnapshotManifestAsync(preTurnShiningRoot, requestRoot, preTurnSoulRoot);

        var issues = await InvokeValidationAsync("ValidatePendingShiningTradeInventoryResolutionAsync");

        Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_trade_inventory_owned_relic_id_collision", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidatePendingShiningTradeInventoryRequestContextAsync_DuplicateSameCycleFactionRequests_Fails()
    {
        await WriteNodeAsync("game_state/meta/soul_state.json", new JsonObject
        {
            ["currentRealm"] = "Shining Abode",
            ["currentIncarnation"] = 2
        });
        await WriteNodeAsync(ShiningAbodeState.StatePath, CreateBaseShiningRoot());
        await WriteNodeAsync(GuardianAbodeResidentState.StatePath, new JsonObject
        {
            ["entries"] = new JsonArray()
        });
        await WriteNodeAsync("game_state/meta/guardians.json", new JsonObject
        {
            ["guardians"] = new JsonArray()
        });
        await WriteNodeAsync(ShiningTradeRequestState.PendingRequestsPath, new JsonObject
        {
            [ShiningTradeRequestState.RequestsProperty] = new JsonArray
            {
                CreatePendingTradeRequest("trade_dup_1"),
                CreatePendingTradeRequest("trade_dup_2")
            }
        });

        var issues = await InvokeValidationAsync("ValidatePendingShiningTradeInventoryRequestContextAsync");

        Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_trade_duplicate_same_cycle_faction_requests", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidatePendingShiningTradeInventoryRequestContextAsync_DuplicateRequestIds_Fails()
    {
        await WriteNodeAsync("game_state/meta/soul_state.json", new JsonObject
        {
            ["currentRealm"] = "Shining Abode",
            ["currentIncarnation"] = 2
        });
        await WriteNodeAsync(ShiningAbodeState.StatePath, CreateBaseShiningRoot());
        await WriteNodeAsync(GuardianAbodeResidentState.StatePath, new JsonObject
        {
            ["entries"] = new JsonArray()
        });
        await WriteNodeAsync("game_state/meta/guardians.json", new JsonObject
        {
            ["guardians"] = new JsonArray()
        });

        var firstRequest = CreatePendingTradeRequest("trade_req_shared");
        var secondRequest = CreatePendingTradeRequest("trade_req_shared");
        secondRequest["tradeCycleId"] = "shining_return_3";
        await WriteNodeAsync(ShiningTradeRequestState.PendingRequestsPath, new JsonObject
        {
            [ShiningTradeRequestState.RequestsProperty] = new JsonArray
            {
                firstRequest,
                secondRequest
            }
        });

        var issues = await InvokeValidationAsync("ValidatePendingShiningTradeInventoryRequestContextAsync");

        Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_trade_duplicate_request_id", StringComparison.OrdinalIgnoreCase));
    }

    private async Task WritePendingTurnSnapshotManifestAsync(
        JsonObject preTurnShiningRoot,
        JsonObject requestRoot,
        JsonObject? preTurnSoulRoot = null,
        JsonObject? preTurnResidentRoot = null)
    {
        const string requestSnapshotPath = "game_state/control/pending_turn_snapshot/pre_shining_trade_request.json";
        const string shiningSnapshotPath = "game_state/control/pending_turn_snapshot/game_state/meta/shining_abode_state.json";
        const string soulSnapshotPath = "game_state/control/pending_turn_snapshot/game_state/meta/soul_state.json";
        const string residentsSnapshotPath = "game_state/control/pending_turn_snapshot/game_state/meta/guardian_abode_residents.json";

        await WriteNodeAsync(requestSnapshotPath, requestRoot);
        await WriteNodeAsync(shiningSnapshotPath, preTurnShiningRoot);
        await WriteNodeAsync(soulSnapshotPath, preTurnSoulRoot ?? CreateSoulStateRoot());
        await WriteNodeAsync(residentsSnapshotPath, preTurnResidentRoot ?? new JsonObject { ["entries"] = new JsonArray() });
        await WriteNodeAsync("input/turn_request.json", new JsonObject
        {
            ["sessionId"] = "test-session",
            ["requestId"] = "test-request",
            ["turnNumber"] = 12
        });

        var manifest = new JsonObject
        {
            ["sessionId"] = "test-session",
            ["requestId"] = "test-request",
            ["turnNumber"] = 12,
            ["requestTimestamp"] = "2026-04-17T00:00:00Z",
            ["playerAction"] = "test",
            ["files"] = new JsonObject
            {
                [NormalizeRelativePath(ShiningTradeRequestState.PendingRequestsPath)] = requestSnapshotPath,
                ["game_state/meta/shining_abode_state.json"] = shiningSnapshotPath,
                ["game_state/meta/soul_state.json"] = soulSnapshotPath,
                ["game_state/meta/guardian_abode_residents.json"] = residentsSnapshotPath
            },
            ["snapshotFileHashes"] = new JsonObject
            {
                [NormalizeRelativePath(ShiningTradeRequestState.PendingRequestsPath)] = ComputeSha256(await _fs.ReadFileAsync(requestSnapshotPath) ?? string.Empty),
                ["game_state/meta/shining_abode_state.json"] = ComputeSha256(await _fs.ReadFileAsync(shiningSnapshotPath) ?? string.Empty),
                ["game_state/meta/soul_state.json"] = ComputeSha256(await _fs.ReadFileAsync(soulSnapshotPath) ?? string.Empty),
                ["game_state/meta/guardian_abode_residents.json"] = ComputeSha256(await _fs.ReadFileAsync(residentsSnapshotPath) ?? string.Empty)
            },
            ["clientOwnedValidationHashes"] = new JsonObject(),
            ["rollbackBackups"] = new JsonObject
            {
                [NormalizeRelativePath(ShiningTradeRequestState.PendingRequestsPath)] = requestSnapshotPath,
                ["game_state/meta/shining_abode_state.json"] = shiningSnapshotPath,
                ["game_state/meta/soul_state.json"] = soulSnapshotPath,
                ["game_state/meta/guardian_abode_residents.json"] = residentsSnapshotPath
            },
            ["rollbackBaselineFiles"] = new JsonArray(
                NormalizeRelativePath(ShiningTradeRequestState.PendingRequestsPath),
                "game_state/meta/shining_abode_state.json",
                "game_state/meta/soul_state.json",
                "game_state/meta/guardian_abode_residents.json"),
            ["sourceLabel"] = "shining-trade-resolution-tests",
            ["manifestPayloadHash"] = string.Empty
        };

        manifest["manifestPayloadHash"] = PendingTurnSnapshotTestAuthority.ComputeManifestPayloadHash(manifest);
        await WriteNodeAsync("game_state/control/pending_turn_snapshot.json", manifest);
        await PendingTurnSnapshotTestAuthority.SyncAuthorityForCurrentManifestAsync(_fs);
    }

    private async Task<List<ValidationIssue>> InvokeValidationAsync(string methodName)
    {
        var issues = new List<ValidationIssue>();
        var method = typeof(ValidationService).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var task = method!.Invoke(_validator, new object[] { issues }) as Task;
        Assert.NotNull(task);
        await task!;
        return issues;
    }

    private static JsonObject CreateBaseShiningRoot()
    {
        var root = ShiningAbodeState.CreateDefaultState();
        root["availability"] = ShiningAbodeState.AvailabilityActive;
        root["radiance"] = new JsonObject
        {
            ["experience"] = 250,
            ["tier"] = 2
        };
        root["factions"] = new JsonArray
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
                ["baseStrength"] = 62,
                ["factionStrength"] = 62,
                ["investCountThisAscension"] = 0,
                ["projectArchetypesCountedThisAscension"] = new JsonArray(),
                ["projects"] = new JsonArray(),
                ["tradeInventoryReceipts"] = new JsonArray(),
                ["leadershipReceipts"] = new JsonArray(),
                ["leadershipHistory"] = new JsonArray()
            }
        };
        return root;
    }

    private static JsonObject CreateTradeSlot(string slotId, int priceInFeathers, string relicId, string quality) => new()
    {
        ["slotId"] = slotId,
        ["priceInFeathers"] = priceInFeathers,
        ["soldOut"] = false,
        ["relicData"] = new JsonObject
        {
            ["relicId"] = relicId,
            ["name"] = relicId,
            ["quality"] = quality
        }
    };

    private static JsonObject CreateSoulStateRoot(params string[] storedRelicIds) => new()
    {
        ["currentRealm"] = "Shining Abode",
        ["currentIncarnation"] = 2,
        ["soulRelics"] = new JsonObject
        {
            ["equipped"] = new JsonArray(),
            ["stored"] = new JsonArray(storedRelicIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => (JsonNode?)new JsonObject
                {
                    ["relicId"] = id,
                    ["name"] = id,
                    ["quality"] = "Rare"
                })
                .ToArray())
        }
    };

    private static JsonObject CloneJsonObject(JsonObject source) => JsonNode.Parse(source.ToJsonString())!.AsObject();

    private static JsonObject CreatePendingTradeRequest(string requestId) => new()
    {
        ["requestId"] = requestId,
        ["factionId"] = "faction_old",
        ["factionName"] = "Старый Дом",
        ["tradeCycleId"] = "shining_return_2",
        ["derivedTradeTier"] = 2,
        ["derivedTradeSlotCount"] = 6,
        ["derivedRarityCeiling"] = "rare",
        ["derivedServiceMultiplier"] = 1.25,
        ["merchantProfile"] = "shining_faction",
        ["createdAtTurn"] = 13,
        ["createdAtUtc"] = "2026-04-17T00:59:00Z"
    };

    private async Task WriteNodeAsync(string relativePath, JsonNode node)
    {
        await _fs.WriteFileAtomicAsync(relativePath, node.ToJsonString());
    }

    private static string NormalizeRelativePath(string path) => path.Replace('\\', '/');

    private static string ComputeSha256(string content)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_rootPath))
                Directory.Delete(_rootPath, recursive: true);
        }
        catch
        {
            // ignored
        }
    }
}
