using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ShiningCoreActionRequestStateTests
{
    [Fact]
    public async Task WriteRequestAsync_RejectsForeignLivePendingRequest()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();

            await ShiningCoreActionRequestState.WriteRequestAsync(fs, new ShiningCoreActionRequestState.PendingShiningCoreActionRequest
            {
                RequestId = "core_req_existing",
                ActionType = ShiningCoreActionRequestState.ActionTypeInvestInFaction,
                FactionId = "faction_old",
                FactionName = "Старый Дом"
            });

            await Assert.ThrowsAsync<InvalidOperationException>(() => ShiningCoreActionRequestState.WriteRequestAsync(fs, new ShiningCoreActionRequestState.PendingShiningCoreActionRequest
            {
                RequestId = "core_req_foreign",
                ActionType = ShiningCoreActionRequestState.ActionTypeOpenGates
            }));

            var requests = await ShiningCoreActionRequestState.ReadRequestsAsync(fs);
            var request = Assert.Single(requests);
            Assert.Equal("core_req_existing", request.RequestId);
            Assert.Equal(ShiningCoreActionRequestState.ActionTypeInvestInFaction, request.ActionType);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task BuildSystemReminderFragmentAsync_ListsPendingCoreAction()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();

            await ShiningCoreActionRequestState.WriteRequestAsync(fs, new ShiningCoreActionRequestState.PendingShiningCoreActionRequest
            {
                RequestId = "core_req_complete_project",
                ActionType = ShiningCoreActionRequestState.ActionTypeCompleteProject,
                FactionId = "faction_old",
                FactionName = "Старый Дом",
                ProjectId = "project_archive",
                ProjectDisplayName = "Архив Света",
                ProjectDraft = new JsonObject
                {
                    ["displayName"] = "Архив Света",
                    ["projectArchetype"] = "accord"
                },
                RadianceTierAtRequest = 3,
                QuotedCostFeathers = 20,
                QuotedCostLightSparks = 10,
                SourceDraftVersion = 4,
                SelectedCardIds = { "card_social" },
                SelectedCards = new JsonArray(new JsonObject
                {
                    ["cardId"] = "card_social",
                    ["displayName"] = "Память Света"
                }),
                ReplacementProperty = new JsonObject
                {
                    ["propertyId"] = "prop_memory",
                    ["displayName"] = "Память Света"
                },
                CreatedAtTurn = 7,
                CreatedAtUtc = "2026-04-27T10:00:00Z"
            });

            var reminder = await ShiningCoreActionRequestState.BuildSystemReminderFragmentAsync(fs, "Shining Abode");

            Assert.NotNull(reminder);
            Assert.Contains("SHINING ABODE CORE ACTION:", reminder);
            Assert.Contains("complete_project", reminder);
            Assert.Contains("Архив Света", reminder);
            Assert.Contains("Full pending core-action DTO", reminder);
            Assert.Contains("\"requestId\": \"core_req_complete_project\"", reminder);
            Assert.Contains("\"projectId\": \"project_archive\"", reminder);
            Assert.Contains("\"projectDraft\"", reminder);
            Assert.Contains("\"radianceTierAtRequest\": 3", reminder);
            Assert.Contains("\"quotedCostLightSparks\": 10", reminder);
            Assert.Contains("\"sourceDraftVersion\": 4", reminder);
            Assert.Contains("\"selectedCardIds\"", reminder);
            Assert.Contains("\"selectedCards\"", reminder);
            Assert.Contains("\"replacementProperty\"", reminder);
            Assert.Contains("\"createdAtUtc\": \"2026-04-27T10:00:00Z\"", reminder);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task BuildSystemReminderFragmentAsync_PendingBootstrapBlocksAndPreservesCoreActionReminder()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();

            await fs.WriteFileAtomicAsync(ShiningAbodeState.StatePath, """
            {
              "availability": "active",
              "preparedIncarnationPackage": {
                "selectedCardIds": ["card_memory"],
                "selectedCards": [
                  {
                    "cardId": "card_memory",
                    "dedupeKey": "memory:card_memory",
                    "sourceType": "project",
                    "sourceFactionId": "faction_old",
                    "sourceActorId": "project_old",
                    "effectFamily": "memory",
                    "rarity": "common",
                    "displayName": "Память Света",
                    "displaySummary": "Сохраняет эхо.",
                    "effectPayload": {}
                  }
                ]
              }
            }
            """);
            await ShiningCoreActionRequestState.WriteRequestAsync(fs, new ShiningCoreActionRequestState.PendingShiningCoreActionRequest
            {
                RequestId = "core_req_open_gates",
                ActionType = ShiningCoreActionRequestState.ActionTypeOpenGates,
                CreatedAtTurn = 7
            });

            var reminder = await ShiningCoreActionRequestState.BuildSystemReminderFragmentAsync(fs, "Shining Abode");

            Assert.NotNull(reminder);
            Assert.Contains("SHINING ABODE CORE ACTIONS BLOCKED", reminder);
            Assert.Contains("pending-bootstrap handoff", reminder, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Preserve pending_shining_abode_actions.json", reminder);
            Assert.Contains("core_req_open_gates", reminder);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task BuildSystemReminderFragmentAsync_MalformedPreparedPackageBlocksOrdinaryCoreActions()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();

            await fs.WriteFileAtomicAsync(ShiningAbodeState.StatePath, """
            {
              "availability": "active",
              "preparedIncarnationPackage": "broken package"
            }
            """);

            var reminder = await ShiningCoreActionRequestState.BuildSystemReminderFragmentAsync(fs, "Shining Abode");

            Assert.NotNull(reminder);
            Assert.Contains("SHINING ABODE CORE ACTIONS BLOCKED", reminder);
            Assert.Contains("malformed or fails bootstrap validation", reminder);
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

            await ShiningCoreActionRequestState.WriteRequestAsync(fs, new ShiningCoreActionRequestState.PendingShiningCoreActionRequest
            {
                ActionType = ShiningCoreActionRequestState.ActionTypeOpenGates,
                CreatedAtTurn = 8
            });

            await ShiningCoreActionRequestState.EnsureHealthyAsync(fs, "Chaos Sea");

            Assert.Single(await ShiningCoreActionRequestState.ReadRequestsAsync(fs));
            Assert.True(fs.FileExists(ShiningCoreActionRequestState.PendingActionsRequestPath));
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

            await ShiningCoreActionRequestState.WriteRequestAsync(fs, new ShiningCoreActionRequestState.PendingShiningCoreActionRequest
            {
                ActionType = ShiningCoreActionRequestState.ActionTypeOpenGates,
                CreatedAtTurn = 8
            });

            await ShiningCoreActionRequestState.EnsureHealthyAsync(fs, "");

            Assert.True(fs.FileExists(ShiningCoreActionRequestState.PendingActionsRequestPath));
            Assert.Single(await ShiningCoreActionRequestState.ReadRequestsAsync(fs));
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task EnsureHealthyAsync_ActiveShiningWithMultiplePendingRequests_PreservesMalformedFile()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();
            await WriteMinimalActiveShiningStateAsync(fs);
            await WritePendingRequestsAsync(
                fs,
                new ShiningCoreActionRequestState.PendingShiningCoreActionRequest
                {
                    RequestId = "core_req_first",
                    ActionType = ShiningCoreActionRequestState.ActionTypeOpenGates,
                    CreatedAtTurn = 8
                },
                new ShiningCoreActionRequestState.PendingShiningCoreActionRequest
                {
                    RequestId = "core_req_second",
                    ActionType = ShiningCoreActionRequestState.ActionTypeInvestInFaction,
                    FactionId = "faction_old",
                    FactionName = "Старый Дом",
                    CreatedAtTurn = 8
                });

            var beforeJson = await fs.ReadFileAsync(ShiningCoreActionRequestState.PendingActionsRequestPath);

            await ShiningCoreActionRequestState.EnsureHealthyAsync(fs, "Shining Abode");

            var afterJson = await fs.ReadFileAsync(ShiningCoreActionRequestState.PendingActionsRequestPath);
            var requests = await ShiningCoreActionRequestState.ReadRequestsAsync(fs);
            Assert.Equal(beforeJson, afterJson);
            Assert.Equal(2, requests.Count);
            Assert.Equal("core_req_first", requests[0].RequestId);
            Assert.Equal("core_req_second", requests[1].RequestId);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task EnsureHealthyAsync_MismatchedSameRequestIdReceipt_DoesNotClearPendingRequest()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();
            await WriteMinimalActiveShiningStateAsync(fs);
            await WritePendingRequestsAsync(
                fs,
                new ShiningCoreActionRequestState.PendingShiningCoreActionRequest
                {
                    RequestId = "core_req_collision",
                    ActionType = ShiningCoreActionRequestState.ActionTypeOpenGates,
                    CreatedAtTurn = 8
                });

            var shiningRoot = JsonNode.Parse(await fs.ReadFileAsync(ShiningAbodeState.StatePath)!)!.AsObject();
            ((JsonArray)shiningRoot["coreActionReceipts"]!).Add(new JsonObject
            {
                ["requestId"] = "core_req_collision",
                ["actionType"] = ShiningCoreActionRequestState.ActionTypeInvestInFaction,
                ["status"] = ShiningCoreActionRequestState.RequestStatusAccepted,
                ["factionId"] = "faction_old",
                ["projectId"] = "",
                ["selectedCardIds"] = new JsonArray(),
                ["newResidentIds"] = new JsonArray(),
                ["seededProjectIds"] = new JsonArray(),
                ["generatedDraftVersion"] = 0,
                ["resolvedAtTurn"] = 9,
                ["resolvedAtUtc"] = "2026-04-17T00:10:00Z"
            });
            await fs.WriteFileAtomicAsync(ShiningAbodeState.StatePath, shiningRoot.ToJsonString());

            await ShiningCoreActionRequestState.EnsureHealthyAsync(fs, "Shining Abode");

            var requests = await ShiningCoreActionRequestState.ReadRequestsAsync(fs);
            var request = Assert.Single(requests);
            Assert.Equal("core_req_collision", request.RequestId);
            Assert.True(fs.FileExists(ShiningCoreActionRequestState.PendingActionsRequestPath));
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task EnsureHealthyAsync_CompleteProjectReceiptWithResolvedProjectId_ClearsPendingRequest()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();
            await WriteMinimalActiveShiningStateAsync(fs);
            await WritePendingRequestsAsync(
                fs,
                new ShiningCoreActionRequestState.PendingShiningCoreActionRequest
                {
                    RequestId = "core_req_complete_project",
                    ActionType = ShiningCoreActionRequestState.ActionTypeCompleteProject,
                    FactionId = "faction_old",
                    FactionName = "Старый Дом",
                    ProjectId = "",
                    CreatedAtTurn = 8
                });

            var shiningRoot = JsonNode.Parse(await fs.ReadFileAsync(ShiningAbodeState.StatePath)!)!.AsObject();
            ((JsonArray)shiningRoot["coreActionReceipts"]!).Add(new JsonObject
            {
                ["requestId"] = "core_req_complete_project",
                ["actionType"] = ShiningCoreActionRequestState.ActionTypeCompleteProject,
                ["status"] = ShiningCoreActionRequestState.RequestStatusAccepted,
                ["factionId"] = "faction_old",
                ["projectId"] = "project_materialized",
                ["selectedCardIds"] = new JsonArray(),
                ["newResidentIds"] = new JsonArray(),
                ["seededProjectIds"] = new JsonArray(),
                ["generatedDraftVersion"] = 0,
                ["resolvedAtTurn"] = 9,
                ["resolvedAtUtc"] = "2026-04-17T00:10:00Z"
            });
            await fs.WriteFileAtomicAsync(ShiningAbodeState.StatePath, shiningRoot.ToJsonString());

            await ShiningCoreActionRequestState.EnsureHealthyAsync(fs, "Shining Abode");

            Assert.False(fs.FileExists(ShiningCoreActionRequestState.PendingActionsRequestPath));
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task EnsureHealthyAsync_AcceptedPullRelicGachaWithGeneratedRelicId_ClearsPendingRequest()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();
            await WriteMinimalActiveShiningStateAsync(fs);
            await WritePendingRequestsAsync(
                fs,
                new ShiningCoreActionRequestState.PendingShiningCoreActionRequest
                {
                    RequestId = "core_req_gacha",
                    ActionType = ShiningCoreActionRequestState.ActionTypePullRelicGacha,
                    FactionId = "faction_old",
                    ReturnCycleId = "shining_return_2",
                    RelicId = "",
                    CreatedAtTurn = 8
                });

            var shiningRoot = JsonNode.Parse(await fs.ReadFileAsync(ShiningAbodeState.StatePath)!)!.AsObject();
            shiningRoot["gachaSystem"] = new JsonObject
            {
                ["gachaHistory"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["requestId"] = "core_req_gacha",
                        ["relicId"] = "relic_generated_001",
                        ["returnCycleId"] = "shining_return_2"
                    }
                }
            };
            ((JsonArray)shiningRoot["coreActionReceipts"]!).Add(new JsonObject
            {
                ["requestId"] = "core_req_gacha",
                ["actionType"] = ShiningCoreActionRequestState.ActionTypePullRelicGacha,
                ["status"] = ShiningCoreActionRequestState.RequestStatusAccepted,
                ["factionId"] = "faction_old",
                ["relicId"] = "relic_generated_001",
                ["returnCycleId"] = "shining_return_2",
                ["selectedCardIds"] = new JsonArray(),
                ["newResidentIds"] = new JsonArray(),
                ["seededProjectIds"] = new JsonArray(),
                ["generatedDraftVersion"] = 0,
                ["resolvedAtTurn"] = 9,
                ["resolvedAtUtc"] = "2026-04-17T00:10:00Z"
            });
            await fs.WriteFileAtomicAsync(ShiningAbodeState.StatePath, shiningRoot.ToJsonString());

            await ShiningCoreActionRequestState.EnsureHealthyAsync(fs, "Shining Abode");

            Assert.False(fs.FileExists(ShiningCoreActionRequestState.PendingActionsRequestPath));
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task EnsureHealthyAsync_NonAcceptedPullRelicGachaWithGeneratedRelicId_DoesNotClearPendingRequest()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();
            await WriteMinimalActiveShiningStateAsync(fs);
            await WritePendingRequestsAsync(
                fs,
                new ShiningCoreActionRequestState.PendingShiningCoreActionRequest
                {
                    RequestId = "core_req_gacha_refused",
                    ActionType = ShiningCoreActionRequestState.ActionTypePullRelicGacha,
                    FactionId = "faction_old",
                    ReturnCycleId = "shining_return_2",
                    RelicId = "",
                    CreatedAtTurn = 8
                });

            var shiningRoot = JsonNode.Parse(await fs.ReadFileAsync(ShiningAbodeState.StatePath)!)!.AsObject();
            ((JsonArray)shiningRoot["coreActionReceipts"]!).Add(new JsonObject
            {
                ["requestId"] = "core_req_gacha_refused",
                ["actionType"] = ShiningCoreActionRequestState.ActionTypePullRelicGacha,
                ["status"] = ShiningCoreActionRequestState.RequestStatusRefused,
                ["factionId"] = "faction_old",
                ["relicId"] = "relic_should_not_exist",
                ["returnCycleId"] = "shining_return_2",
                ["selectedCardIds"] = new JsonArray(),
                ["newResidentIds"] = new JsonArray(),
                ["seededProjectIds"] = new JsonArray(),
                ["generatedDraftVersion"] = 0,
                ["resolvedAtTurn"] = 9,
                ["resolvedAtUtc"] = "2026-04-17T00:10:00Z"
            });
            await fs.WriteFileAtomicAsync(ShiningAbodeState.StatePath, shiningRoot.ToJsonString());

            await ShiningCoreActionRequestState.EnsureHealthyAsync(fs, "Shining Abode");

            Assert.True(fs.FileExists(ShiningCoreActionRequestState.PendingActionsRequestPath));
            var request = Assert.Single(await ShiningCoreActionRequestState.ReadRequestsAsync(fs));
            Assert.Equal("core_req_gacha_refused", request.RequestId);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task EnsureHealthyAsync_StubReceiptWithoutResolvedMarkers_DoesNotClearPendingRequest()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();
            await WriteMinimalActiveShiningStateAsync(fs);
            await WritePendingRequestsAsync(
                fs,
                new ShiningCoreActionRequestState.PendingShiningCoreActionRequest
                {
                    RequestId = "core_req_stub",
                    ActionType = ShiningCoreActionRequestState.ActionTypeOpenGates,
                    CreatedAtTurn = 8
                });

            var shiningRoot = JsonNode.Parse(await fs.ReadFileAsync(ShiningAbodeState.StatePath)!)!.AsObject();
            ((JsonArray)shiningRoot["coreActionReceipts"]!).Add(new JsonObject
            {
                ["requestId"] = "core_req_stub",
                ["actionType"] = ShiningCoreActionRequestState.ActionTypeOpenGates,
                ["status"] = ShiningCoreActionRequestState.RequestStatusAccepted,
                ["selectedCardIds"] = new JsonArray(),
                ["newResidentIds"] = new JsonArray(),
                ["seededProjectIds"] = new JsonArray(),
                ["generatedDraftVersion"] = 0,
                ["resolvedAtTurn"] = 0,
                ["resolvedAtUtc"] = ""
            });
            await fs.WriteFileAtomicAsync(ShiningAbodeState.StatePath, shiningRoot.ToJsonString());

            await ShiningCoreActionRequestState.EnsureHealthyAsync(fs, "Shining Abode");

            Assert.True(fs.FileExists(ShiningCoreActionRequestState.PendingActionsRequestPath));
            Assert.Single(await ShiningCoreActionRequestState.ReadRequestsAsync(fs));
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task EnsureHealthyAsync_PreparePackageReceiptWithReorderedSelectedCardIds_DoesNotClearPendingRequest()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();
            await WriteMinimalActiveShiningStateAsync(fs);
            await WritePendingRequestsAsync(
                fs,
                new ShiningCoreActionRequestState.PendingShiningCoreActionRequest
                {
                    RequestId = "core_req_prepare_ordered",
                    ActionType = ShiningCoreActionRequestState.ActionTypePrepareIncarnationPackage,
                    SourceDraftVersion = 1,
                    SelectedCardIds = { "card_route_dawn", "card_social_dawn" },
                    CreatedAtTurn = 8
                });

            var shiningRoot = JsonNode.Parse(await fs.ReadFileAsync(ShiningAbodeState.StatePath)!)!.AsObject();
            ((JsonArray)shiningRoot["coreActionReceipts"]!).Add(new JsonObject
            {
                ["requestId"] = "core_req_prepare_ordered",
                ["actionType"] = ShiningCoreActionRequestState.ActionTypePrepareIncarnationPackage,
                ["status"] = ShiningCoreActionRequestState.RequestStatusAccepted,
                ["selectedCardIds"] = new JsonArray("card_social_dawn", "card_route_dawn"),
                ["newResidentIds"] = new JsonArray(),
                ["seededProjectIds"] = new JsonArray(),
                ["generatedDraftVersion"] = 1,
                ["resolvedAtTurn"] = 9,
                ["resolvedAtUtc"] = "2026-04-17T00:10:00Z"
            });
            await fs.WriteFileAtomicAsync(ShiningAbodeState.StatePath, shiningRoot.ToJsonString());

            await ShiningCoreActionRequestState.EnsureHealthyAsync(fs, "Shining Abode");

            Assert.True(fs.FileExists(ShiningCoreActionRequestState.PendingActionsRequestPath));
            Assert.Single(await ShiningCoreActionRequestState.ReadRequestsAsync(fs));
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task EnsureHealthyAsync_PreparedPackageWithoutMatchingClosure_PreservesPendingRequest()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();
            await WriteMinimalActiveShiningStateAsync(fs);
            await WritePendingRequestsAsync(
                fs,
                new ShiningCoreActionRequestState.PendingShiningCoreActionRequest
                {
                    RequestId = "core_req_unresolved",
                    ActionType = ShiningCoreActionRequestState.ActionTypeOpenGates,
                    CreatedAtTurn = 8
                });

            var shiningRoot = JsonNode.Parse(await fs.ReadFileAsync(ShiningAbodeState.StatePath)!)!.AsObject();
            shiningRoot["preparedIncarnationPackage"] = new JsonObject
            {
                ["selectedCardIds"] = new JsonArray("card_social"),
                ["selectedCards"] = new JsonArray(CreateCard("card_social", "social", "uncommon")),
                ["generatedFromDraftVersion"] = 1,
                ["preparedAtTurn"] = 9,
                ["preparedAtUtc"] = "2026-04-17T00:10:00Z"
            };
            await fs.WriteFileAtomicAsync(ShiningAbodeState.StatePath, shiningRoot.ToJsonString());

            await ShiningCoreActionRequestState.EnsureHealthyAsync(fs, "Shining Abode");

            Assert.True(fs.FileExists(ShiningCoreActionRequestState.PendingActionsRequestPath));
            var request = Assert.Single(await ShiningCoreActionRequestState.ReadRequestsAsync(fs));
            Assert.Equal("core_req_unresolved", request.RequestId);
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
            await fs.WriteFileAtomicAsync(ShiningCoreActionRequestState.PendingActionsRequestPath, "{");

            await Assert.ThrowsAsync<InvalidOperationException>(() => ShiningCoreActionRequestState.WriteRequestAsync(fs, new ShiningCoreActionRequestState.PendingShiningCoreActionRequest
            {
                ActionType = ShiningCoreActionRequestState.ActionTypeOpenGates,
                CreatedAtTurn = 8
            }));

            Assert.Equal("{", await fs.ReadFileAsync(ShiningCoreActionRequestState.PendingActionsRequestPath));
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
            await WriteMinimalActiveShiningStateAsync(fs);

            var shiningRoot = JsonNode.Parse(await fs.ReadFileAsync(ShiningAbodeState.StatePath)!)!.AsObject();
            shiningRoot["availability"] = "broken_mode";
            await fs.WriteFileAtomicAsync(ShiningAbodeState.StatePath, shiningRoot.ToJsonString());

            var error = await ShiningCoreActionRequestState.ValidateRequestAgainstCurrentStateAsync(fs, new ShiningCoreActionRequestState.PendingShiningCoreActionRequest
            {
                ActionType = ShiningCoreActionRequestState.ActionTypeOpenGates,
                CreatedAtTurn = 5
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
    public async Task ValidateRequestAgainstCurrentStateAsync_FreeCoreActionWithNonzeroCost_Fails()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();
            await WriteMinimalActiveShiningStateAsync(fs);

            var error = await ShiningCoreActionRequestState.ValidateRequestAgainstCurrentStateAsync(fs, new ShiningCoreActionRequestState.PendingShiningCoreActionRequest
            {
                ActionType = ShiningCoreActionRequestState.ActionTypeOpenGates,
                QuotedCostFeathers = 1,
                QuotedCostLightSparks = 0,
                CreatedAtTurn = 5
            });

            Assert.NotNull(error);
            Assert.Contains("0 Feathers / 0 Light Sparks", error, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task ValidateRequestAgainstCurrentStateAsync_SupportAlreadySupportedProject_Fails()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();
            await WriteMinimalActiveShiningStateAsync(fs);

            var error = await ShiningCoreActionRequestState.ValidateRequestAgainstCurrentStateAsync(fs, new ShiningCoreActionRequestState.PendingShiningCoreActionRequest
            {
                ActionType = ShiningCoreActionRequestState.ActionTypeSupportProject,
                FactionId = "faction_old",
                ProjectId = "project_old",
                CreatedAtTurn = 5
            });

            Assert.NotNull(error);
            Assert.Contains("support_project", error, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task ValidateRequestAgainstCurrentStateAsync_UnsupportAlreadyUnsupportedProject_Fails()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();
            await WriteMinimalActiveShiningStateAsync(fs);

            var shiningRoot = JsonNode.Parse(await fs.ReadFileAsync(ShiningAbodeState.StatePath)!)!.AsObject();
            shiningRoot["factions"]![0]!["projects"]![0]!["isSupported"] = false;
            await fs.WriteFileAtomicAsync(ShiningAbodeState.StatePath, shiningRoot.ToJsonString());

            var error = await ShiningCoreActionRequestState.ValidateRequestAgainstCurrentStateAsync(fs, new ShiningCoreActionRequestState.PendingShiningCoreActionRequest
            {
                ActionType = ShiningCoreActionRequestState.ActionTypeUnsupportProject,
                FactionId = "faction_old",
                ProjectId = "project_old",
                CreatedAtTurn = 5
            });

            Assert.NotNull(error);
            Assert.Contains("unsupport_project", error, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task ValidateRequestAgainstCurrentStateAsync_PreparePackageWithoutSelectedCards_Fails()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();
            await WriteMinimalActiveShiningStateAsync(fs);

            var error = await ShiningCoreActionRequestState.ValidateRequestAgainstCurrentStateAsync(fs, new ShiningCoreActionRequestState.PendingShiningCoreActionRequest
            {
                ActionType = ShiningCoreActionRequestState.ActionTypePrepareIncarnationPackage,
                SourceDraftVersion = 1,
                CreatedAtTurn = 5
            });

            Assert.NotNull(error);
            Assert.Contains("минимум одну", error, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task ValidateRequestAgainstCurrentStateAsync_PreparePackageWithSelectedIdsButMissingSnapshots_Fails()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();
            await WriteMinimalActiveShiningStateAsync(fs);

            var error = await ShiningCoreActionRequestState.ValidateRequestAgainstCurrentStateAsync(fs, new ShiningCoreActionRequestState.PendingShiningCoreActionRequest
            {
                ActionType = ShiningCoreActionRequestState.ActionTypePrepareIncarnationPackage,
                SourceDraftVersion = 1,
                SelectedCardIds = new List<string> { "card_social" },
                CreatedAtTurn = 5
            });

            Assert.NotNull(error);
            Assert.Contains("selectedCards", error, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task ValidateRequestAgainstCurrentStateAsync_PreparePackageWithEmptySelectedSnapshots_Fails()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();
            await WriteMinimalActiveShiningStateAsync(fs);

            var error = await ShiningCoreActionRequestState.ValidateRequestAgainstCurrentStateAsync(fs, new ShiningCoreActionRequestState.PendingShiningCoreActionRequest
            {
                ActionType = ShiningCoreActionRequestState.ActionTypePrepareIncarnationPackage,
                SourceDraftVersion = 1,
                SelectedCardIds = new List<string> { "card_social" },
                SelectedCards = new JsonArray(),
                CreatedAtTurn = 5
            });

            Assert.NotNull(error);
            Assert.Contains("selectedCards", error, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task ValidateRequestAgainstCurrentStateAsync_PreparePackageWithMalformedSelectedSnapshot_Fails()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();
            await WriteMinimalActiveShiningStateAsync(fs);

            var malformedCard = CreateCard("card_social", "social", "uncommon");
            malformedCard.Remove("effectPayload");
            var error = await ShiningCoreActionRequestState.ValidateRequestAgainstCurrentStateAsync(fs, new ShiningCoreActionRequestState.PendingShiningCoreActionRequest
            {
                ActionType = ShiningCoreActionRequestState.ActionTypePrepareIncarnationPackage,
                SourceDraftVersion = 1,
                SelectedCardIds = new List<string> { "card_social" },
                SelectedCards = new JsonArray(malformedCard),
                CreatedAtTurn = 5
            });

            Assert.NotNull(error);
            Assert.Contains("selectedCards", error, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task ValidateRequestAgainstCurrentStateAsync_PreparePackageWithMutatedSelectedSnapshot_Fails()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();
            await WriteMinimalActiveShiningStateAsync(fs);

            var mutatedCard = CreateCard("card_social", "social", "uncommon");
            mutatedCard["rarity"] = ShiningAbodeState.RarityRare;
            mutatedCard["effectPayload"] = new JsonObject
            {
                ["type"] = "mutated"
            };

            var error = await ShiningCoreActionRequestState.ValidateRequestAgainstCurrentStateAsync(fs, new ShiningCoreActionRequestState.PendingShiningCoreActionRequest
            {
                ActionType = ShiningCoreActionRequestState.ActionTypePrepareIncarnationPackage,
                SourceDraftVersion = 1,
                SelectedCardIds = new List<string> { "card_social" },
                SelectedCards = new JsonArray(mutatedCard),
                CreatedAtTurn = 5
            });

            Assert.NotNull(error);
            Assert.Contains("selectedCards", error, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task ValidateRequestAgainstCurrentStateAsync_PreparePackageWithSelectedSnapshot_Passes()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();
            await WriteMinimalActiveShiningStateAsync(fs);

            var error = await ShiningCoreActionRequestState.ValidateRequestAgainstCurrentStateAsync(fs, new ShiningCoreActionRequestState.PendingShiningCoreActionRequest
            {
                ActionType = ShiningCoreActionRequestState.ActionTypePrepareIncarnationPackage,
                SourceDraftVersion = 1,
                SelectedCardIds = new List<string> { "card_social" },
                SelectedCards = new JsonArray(CreateCard("card_social", "social", "uncommon")),
                CreatedAtTurn = 5
            });

            Assert.Null(error);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task ValidateRequestAgainstCurrentStateAsync_PreparePackageWithDuplicateSelectedCards_Fails()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();
            await WriteMinimalActiveShiningStateAsync(fs);

            var error = await ShiningCoreActionRequestState.ValidateRequestAgainstCurrentStateAsync(fs, new ShiningCoreActionRequestState.PendingShiningCoreActionRequest
            {
                ActionType = ShiningCoreActionRequestState.ActionTypePrepareIncarnationPackage,
                SourceDraftVersion = 1,
                SelectedCardIds = new List<string> { "card_route_dawn", "card_route_dawn" },
                CreatedAtTurn = 5
            });

            Assert.NotNull(error);
            Assert.Contains("duplicate selectedCardIds", error, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task BuildSystemReminderFragmentAsync_MultiplePendingRequests_ReturnsCorruptionReminder()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();
            await WriteMinimalActiveShiningStateAsync(fs);
            await WritePendingRequestsAsync(
                fs,
                new ShiningCoreActionRequestState.PendingShiningCoreActionRequest
                {
                    RequestId = "core_req_first",
                    ActionType = ShiningCoreActionRequestState.ActionTypeOpenGates,
                    CreatedAtTurn = 8
                },
                new ShiningCoreActionRequestState.PendingShiningCoreActionRequest
                {
                    RequestId = "core_req_second",
                    ActionType = ShiningCoreActionRequestState.ActionTypePrepareIncarnationPackage,
                    SourceDraftVersion = 1,
                    SelectedCardIds = { "card_social" },
                    CreatedAtTurn = 8
                });

            var reminder = await ShiningCoreActionRequestState.BuildSystemReminderFragmentAsync(fs, "Shining Abode");

            Assert.NotNull(reminder);
            Assert.Contains("SHINING ABODE CORE ACTION CORRUPTION:", reminder);
            Assert.Contains("multiple pending requests", reminder, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Pending requests detected: 2", reminder);
            Assert.DoesNotContain("Pending action:", reminder, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task ValidateRequestAgainstCurrentStateAsync_ForgeWithoutRefinementProject_Fails()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();
            await WriteMinimalActiveShiningStateAsync(fs);

            var error = await ShiningCoreActionRequestState.ValidateRequestAgainstCurrentStateAsync(fs, new ShiningCoreActionRequestState.PendingShiningCoreActionRequest
            {
                ActionType = ShiningCoreActionRequestState.ActionTypeForgeRelicReshape,
                FactionId = "faction_old",
                FactionName = "Старый Дом",
                RadianceTierAtRequest = 2,
                QuotedCostFeathers = 10,
                QuotedCostLightSparks = 10,
                RelicId = "relic_old",
                RelicName = "Старый Клинок",
                TargetFormTag = "lance",
                CreatedAtTurn = 5
            });

            Assert.NotNull(error);
            Assert.Contains("refinement", error, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task ValidateRequestAgainstCurrentStateAsync_RelicGachaWithCanonicalBannerState_Passes()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();
            await WriteMinimalActiveShiningStateAsync(fs);

            var error = await ShiningCoreActionRequestState.ValidateRequestAgainstCurrentStateAsync(fs, new ShiningCoreActionRequestState.PendingShiningCoreActionRequest
            {
                ActionType = ShiningCoreActionRequestState.ActionTypePullRelicGacha,
                FactionId = "faction_old",
                FactionName = "Старый Дом",
                RadianceTierAtRequest = 2,
                QuotedCostFeathers = 30,
                QuotedCostLightSparks = 0,
                ReturnCycleId = "shining_return_2",
                ProjectedGachaBonusSteps = 1,
                CreatedAtTurn = 5
            });

            Assert.Null(error);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    private static async Task WriteMinimalActiveShiningStateAsync(FileSystemManager fs)
    {
        await fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", new JsonObject
        {
            ["currentRealm"] = "Shining Abode",
            ["currentIncarnation"] = 2,
            ["inkFeathers"] = new JsonObject
            {
                ["current"] = 50
            },
            ["soulRelics"] = new JsonObject
            {
                ["equipped"] = new JsonArray(),
                ["stored"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["relicId"] = "relic_old",
                        ["name"] = "Старый Клинок",
                        ["rarity"] = "rare",
                        ["formTag"] = "blade",
                        ["properties"] = new JsonArray
                        {
                            new JsonObject
                            {
                                ["propertyId"] = "edge",
                                ["band"] = "rare"
                            }
                        }
                    }
                }
            }
        }.ToJsonString());

        var shiningRoot = ShiningAbodeState.CreateDefaultState();
        shiningRoot["availability"] = ShiningAbodeState.AvailabilityActive;
        shiningRoot["radiance"] = new JsonObject
        {
            ["experience"] = 250,
            ["tier"] = 2
        };
        shiningRoot["lightSparks"] = 80;
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
                    ["favoredArchetype"] = ShiningAbodeState.ProjectArchetypeAccord,
                    ["patronEffectFamily"] = ShiningAbodeState.EffectFamilySocial,
                    ["summary"] = "Старая сияющая фракция."
                },
                ["leadership"] = new JsonObject
                {
                    ["headActorType"] = ShiningAbodeState.HeadActorTypeGuardian,
                    ["headActorId"] = "guardian_old",
                    ["leadershipState"] = ShiningAbodeState.LeadershipStateSecure
                },
                ["baseStrength"] = 35,
                ["factionStrength"] = 45,
                ["investCountThisAscension"] = 0,
                ["projectArchetypesCountedThisAscension"] = new JsonArray(),
                ["projects"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["projectId"] = "project_old",
                        ["displayName"] = "Архив Света",
                        ["summary"] = "Собирает голоса памяти.",
                        ["toneTags"] = new JsonArray("memory"),
                        ["targetFactionIds"] = new JsonArray(),
                        ["projectArchetype"] = ShiningAbodeState.ProjectArchetypeRemembrance,
                        ["outputEffectFamily"] = ShiningAbodeState.EffectFamilyMemory,
                        ["tier"] = 1,
                        ["status"] = ShiningAbodeState.ProjectStatusCompleted,
                        ["isSupported"] = true,
                        ["strengthReward"] = 8
                    }
                },
                ["leadershipReceipts"] = new JsonArray(),
                ["leadershipHistory"] = new JsonArray()
            }
        };
        shiningRoot["gates"] = new JsonObject
        {
            ["draftVersion"] = 1,
            ["hasOpenDraft"] = true,
            ["isStale"] = false,
            ["allCandidateBlessingCards"] = new JsonArray
            {
                CreateCard("card_social", "social", "uncommon"),
                CreateCard("card_memory", "memory", "rare")
            },
            ["availableBlessingCards"] = new JsonArray
            {
                CreateCard("card_social", "social", "uncommon"),
                CreateCard("card_memory", "memory", "rare")
            },
            ["shownBlessingCardIds"] = new JsonArray("card_social", "card_memory"),
            ["selectedBlessingCardIds"] = new JsonArray(),
            ["nextCandidateCursor"] = 2,
            ["rerollsRemaining"] = 1
        };
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

    private static JsonObject CreateCard(string cardId, string effectFamily, string rarity) => new()
    {
        ["cardId"] = cardId,
        ["dedupeKey"] = $"{effectFamily}:{cardId}",
        ["sourceType"] = ShiningAbodeState.CardSourceTypeProject,
        ["sourceFactionId"] = "faction_old",
        ["sourceActorId"] = "project_old",
        ["effectFamily"] = effectFamily,
        ["rarity"] = rarity,
        ["displayName"] = cardId,
        ["displaySummary"] = "summary",
        ["effectPayload"] = new JsonObject
        {
            ["type"] = "noop"
        }
    };

    private static async Task WritePendingRequestsAsync(
        FileSystemManager fs,
        params ShiningCoreActionRequestState.PendingShiningCoreActionRequest[] requests)
    {
        await fs.WriteFileAtomicAsync(
            ShiningCoreActionRequestState.PendingActionsRequestPath,
            JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                [ShiningCoreActionRequestState.RequestsProperty] = requests
            }));
    }

    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-shining-core-request-tests-" + Guid.NewGuid().ToString("N"));
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
