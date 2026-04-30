using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Reflection;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.IO;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class GameEngineTurnLifecycleTests : IDisposable
{
    private sealed class PendingTurnSnapshotManifestPayload
    {
        public string SessionId { get; set; } = "";
        public string RequestId { get; set; } = "";
        public int TurnNumber { get; set; }
        public string RequestTimestamp { get; set; } = "";
        public string PlayerAction { get; set; } = "";
        public ProgressionControl? ProgressionControl { get; set; }
        public Dictionary<string, string> Files { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> SnapshotFileHashes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> ClientOwnedValidationHashes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> RollbackBackups { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public List<string> RollbackBaselineFiles { get; set; } = new();
        public string? SourceLabel { get; set; }
        public string ManifestPayloadHash { get; set; } = "";
    }

    private static readonly JsonSerializerOptions SnapshotHashJsonOpts = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly string _rootPath;
    private readonly FileSystemManager _fs;

    public GameEngineTurnLifecycleTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-gameengine-turnlifecycle-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
    }

    [Fact]
    public void TryDescribeInvalidTriggerLifeEndRuntimeContext_MortalPreTurnAndMortalCurrent_IsReadable()
    {
        var invalid = GameEngine.TryDescribeInvalidTriggerLifeEndRuntimeContext(
            "Mortal World",
            "Mortal World",
            out var failureDescription);

        Assert.False(invalid);
        Assert.Equal(string.Empty, failureDescription);
    }

    [Fact]
    public void TryDescribeInvalidTriggerLifeEndRuntimeContext_AfterlifeCurrentRealm_FailsClosed()
    {
        var invalid = GameEngine.TryDescribeInvalidTriggerLifeEndRuntimeContext(
            "Mortal World",
            "Chaos Sea",
            out var failureDescription);

        Assert.True(invalid);
        Assert.Contains("currentRealm", failureDescription, StringComparison.Ordinal);
    }

    [Fact]
    public void TryDescribeInvalidTriggerLifeEndRuntimeContext_UnreadablePreTurnRealm_FailsClosed()
    {
        var invalid = GameEngine.TryDescribeInvalidTriggerLifeEndRuntimeContext(
            null,
            "Mortal World",
            out var failureDescription);

        Assert.True(invalid);
        Assert.Contains("pre-turn mortal realm authority", failureDescription, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HandleInvalidTriggerLifeEndRuntimeFailure_DeletesSignalAndWritesErrorLog()
    {
        await _fs.WriteFileAtomicAsync("game_state/control/life_transitions.json", """
        {
          "reason": "Death",
          "summary": "Смертная жизнь завершена."
        }
        """);

        var exception = new GameEngine.TriggerLifeEndRuntimeContextException(
            "Canonical TriggerLifeEnd runtime flow requires mortal pre-turn realm authority.");

        GameEngine.HandleInvalidTriggerLifeEndRuntimeFailure(_fs, exception);

        Assert.False(_fs.FileExists("game_state/control/life_transitions.json"));

        var logPath = Path.Combine(_fs.GameSessionPath, "error_log.txt");
        Assert.True(File.Exists(logPath));
        var log = File.ReadAllText(logPath, Encoding.UTF8);
        Assert.Contains("TriggerLifeEndRuntimeContextException", log, StringComparison.Ordinal);
        Assert.Contains("mortal pre-turn realm authority", log, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("[ABODE_OFFERING] Игрок подносит Реликвию Души.", true)]
    [InlineData("[INK_FEATHER_ACTION: ABODE_OFFERING] Игрок подносит 100 Чернильных Перьев.", true)]
    [InlineData("[INK_FEATHER_ACTION: DONATE_TO_GUARDIAN] Игрок жертвует 60 Чернильных Перьев.", false)]
    public void IsPendingAbodeOfferingTurnAction_DetectsPlainAndInkFeatherOfferingTags(string action, bool expected)
    {
        Assert.Equal(expected, GameEngine.IsPendingAbodeOfferingTurnAction(action));
    }

    [Fact]
    public async Task CollectIncarnationBlockersAsync_SystemGuardianAttractionBlocksIncarnation()
    {
        await WriteJsonAsync(SystemGuardianLibraryService.AttractionRequestPath, new
        {
            mode = "system_guardian_attraction",
            targetPresetId = "eternal_tide_001",
            targetPresetDisplayName = "Прилив Памяти",
            targetPresetVersion = "1.0",
            sourceLibrary = "built_in",
            targetSummary = "Извечный Хранитель памяти.",
            renderedPromptPackage = "Досье Хранителя.",
            _lastUpdated = "2026-04-27T12:00:00Z"
        });

        var engine = CreateGameEngine();

        var blockers = await InvokePrivateAsync<List<string>>(engine, "CollectIncarnationBlockersAsync");

        Assert.Contains(blockers, blocker =>
            blocker.Contains("притяжение к извечному Хранителю", StringComparison.OrdinalIgnoreCase) &&
            blocker.Contains("отмените attraction contract", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CollectIncarnationBlockersAsync_MalformedSystemGuardianAttractionBlocksIncarnation()
    {
        await _fs.WriteFileAtomicAsync(SystemGuardianLibraryService.AttractionRequestPath, "{ malformed");

        var engine = CreateGameEngine();

        var blockers = await InvokePrivateAsync<List<string>>(engine, "CollectIncarnationBlockersAsync");

        Assert.Contains(blockers, blocker =>
            blocker.Contains("system_guardian_attraction.json повреждён", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CollectIncarnationBlockersAsync_ResidentTransferEnumeratesEveryPendingRequest()
    {
        await WriteJsonAsync(GuardianAbodeResidentRequestState.PendingTransfersRequestPath, new
        {
            requests = new[]
            {
                new
                {
                    requestId = "transfer_alpha",
                    residentId = "resident_alpha",
                    sourceGuardianId = "guardian_old_alpha",
                    sourceAbodeId = "abode_old_alpha",
                    targetGuardianId = "guardian_new_alpha",
                    targetAbodeId = "abode_new_alpha",
                    createdAtTurn = 12
                },
                new
                {
                    requestId = "transfer_beta",
                    residentId = "resident_beta",
                    sourceGuardianId = "guardian_old_beta",
                    sourceAbodeId = "abode_old_beta",
                    targetGuardianId = "guardian_new_beta",
                    targetAbodeId = "abode_new_beta",
                    createdAtTurn = 13
                }
            }
        });
        var engine = CreateGameEngine();

        var blockers = await InvokePrivateAsync<List<string>>(engine, "CollectIncarnationBlockersAsync");

        var blocker = Assert.Single(blockers);
        Assert.Contains("pending_guardian_abode_resident_transfers.json", blocker, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("request[0]:", blocker, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("requestId=transfer_alpha", blocker, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("targetAbodeId=abode_new_alpha", blocker, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("request[1]:", blocker, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("requestId=transfer_beta", blocker, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sourceGuardianId=guardian_old_beta", blocker, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("full payload:", blocker, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CollectIncarnationBlockersAsync_AbodeOfferingShowsGenericPayloadAndClosure()
    {
        await WriteJsonAsync(GuardianAbodeOfferingState.PendingRequestPath, new
        {
            requestId = "offering_blocker_001",
            guardianId = "guardian_offering_001",
            abodeId = "abode_offering_001",
            offeringType = "ink_feathers",
            inkFeathersOffered = 100,
            powerGain = 20,
            createdAtTurn = 14
        });
        var engine = CreateGameEngine();

        var blockers = await InvokePrivateAsync<List<string>>(engine, "CollectIncarnationBlockersAsync");

        var blocker = Assert.Single(blockers);
        Assert.Contains("pending_abode_offering.json", blocker, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("requestId=offering_blocker_001", blocker, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("offeringType=ink_feathers", blocker, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("inkFeathersOffered=100", blocker, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("output/ink_feather_action_result.json", blocker, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"powerGain\": 20", blocker, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CleanupAfterCancelledChaosSeaMarkerTurn_PreservesSystemGuardianAttractionForLateResponse()
    {
        await WriteJsonAsync(SystemGuardianLibraryService.AttractionRequestPath, new
        {
            mode = "system_guardian_attraction",
            targetPresetId = "eternal_tide_001",
            targetPresetDisplayName = "Прилив Памяти",
            targetPresetVersion = "1.0",
            sourceLibrary = "built_in",
            targetSummary = "Извечный Хранитель памяти.",
            renderedPromptPackage = "Досье Хранителя.",
            _lastUpdated = "2026-04-27T12:00:00Z"
        });
        var engine = CreateGameEngine();

        InvokePrivate(
            engine,
            "CleanupAfterCancelledChaosSeaMarkerTurn",
            "[CHAOS_SEA_SYSTEM_GUARDIAN_ATTRACTION: eternal_tide_001] Игрок слышит зов.");

        Assert.True(_fs.FileExists(SystemGuardianLibraryService.AttractionRequestPath));
        var json = await _fs.ReadFileAsync(SystemGuardianLibraryService.AttractionRequestPath);
        Assert.Contains("eternal_tide_001", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ResolveLifecycleAuthorizedTriggerLifeEndFromPendingSnapshotAsync_ValidActiveManifest_Authorizes()
    {
        await WriteJsonAsync("game_state/control/pending_turn_snapshot/game_state/meta/soul_state.json", new
        {
            currentRealm = "Mortal World",
            currentIncarnation = 2
        });
        await WritePendingTurnSnapshotManifestAsync("test-session", "test-request", 14, "game_state/meta/soul_state.json");
        await WriteJsonAsync("input/turn_request.json", new
        {
            sessionId = "test-session",
            requestId = "test-request",
            turnNumber = 14
        });
        await WriteJsonAsync("game_state/control/life_transitions.json", new
        {
            reason = "Death",
            summary = "Жизнь завершена."
        });

        var resolution = await CanonicalStateNormalizer.ResolveLifecycleAuthorizedTriggerLifeEndFromPendingSnapshotAsync(
            _fs,
            await _fs.ReadFileAsync("game_state/control/life_transitions.json"),
            "Mortal World");

        Assert.True(resolution.IsAuthorized);
        Assert.Equal("authorized", resolution.Code);
    }

    [Fact]
    public async Task ResolveLifecycleAuthorizedTriggerLifeEndFromPendingSnapshotAsync_InactiveManifest_FailsClosed()
    {
        await WriteJsonAsync("game_state/control/pending_turn_snapshot/game_state/meta/soul_state.json", new
        {
            currentRealm = "Mortal World",
            currentIncarnation = 2
        });
        await WritePendingTurnSnapshotManifestAsync("stale-session", "stale-request", 99, "game_state/meta/soul_state.json");
        await WriteJsonAsync("input/turn_request.json", new
        {
            sessionId = "test-session",
            requestId = "test-request",
            turnNumber = 14
        });
        await WriteJsonAsync("game_state/control/life_transitions.json", new
        {
            reason = "Death",
            summary = "Жизнь завершена."
        });

        var resolution = await CanonicalStateNormalizer.ResolveLifecycleAuthorizedTriggerLifeEndFromPendingSnapshotAsync(
            _fs,
            await _fs.ReadFileAsync("game_state/control/life_transitions.json"),
            "Mortal World");

        Assert.False(resolution.IsAuthorized);
        Assert.Equal("inactive_manifest", resolution.Code);
    }

    [Fact]
    public async Task ResolveLifecycleAuthorizedTriggerLifeEndFromPendingSnapshotAsync_StructurallyInvalidManifest_FailsClosed()
    {
        await WriteJsonAsync("game_state/control/pending_turn_snapshot/game_state/meta/soul_state.json", new
        {
            currentRealm = "Mortal World",
            currentIncarnation = 2
        });
        await WritePendingTurnSnapshotManifestAsync("test-session", "test-request", 14, "game_state/meta/soul_state.json");
        await WriteJsonAsync("input/turn_request.json", new
        {
            sessionId = "test-session",
            requestId = "test-request",
            turnNumber = 14
        });
        await WriteJsonAsync("game_state/control/life_transitions.json", new
        {
            reason = "Death",
            summary = "Жизнь завершена."
        });

        var manifest = JsonNode.Parse(await _fs.ReadFileAsync("game_state/control/pending_turn_snapshot.json")!)!.AsObject();
        manifest["snapshotFileHashes"] = new JsonObject();
        manifest["manifestPayloadHash"] = ComputeManifestPayloadHash(JsonSerializer.Deserialize<PendingTurnSnapshotManifestPayload>(
            manifest.ToJsonString(),
            SnapshotHashJsonOpts)!);
        await _fs.WriteFileAtomicAsync(
            "game_state/control/pending_turn_snapshot.json",
            manifest.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));

        var resolution = await CanonicalStateNormalizer.ResolveLifecycleAuthorizedTriggerLifeEndFromPendingSnapshotAsync(
            _fs,
            await _fs.ReadFileAsync("game_state/control/life_transitions.json"),
            "Mortal World");

        Assert.False(resolution.IsAuthorized);
        Assert.Equal("invalid_manifest", resolution.Code);
    }

    [Fact]
    public async Task TryPerformOrdinaryReturnToChaosSeaFromShiningAbodeAsync_PreservesEnlightenmentState()
    {
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Испытующая Душа",
            currentRealm = "Shining Abode",
            currentIncarnation = 4,
            inkFeathers = new { current = 7, total = 31 },
            enlightenment = new
            {
                currentTier = "Сияющий Мудрец",
                experience = 187,
                level = 6,
                progressPercent = 73
            }
        });
        await WriteJsonAsync("game_state/meta/shining_abode_state.json", new
        {
            availability = "active",
            radiance = new { experience = 22, tier = 3 },
            lightSparks = 88,
            halls = Array.Empty<object>(),
            factions = Array.Empty<object>(),
            shiningPoliticalActors = Array.Empty<object>(),
            preparedIncarnationPackage = (object?)null,
            gates = new
            {
                hasOpenDraft = false,
                isStale = false,
                openedThisAscension = false,
                draftOpenedAtTurn = (int?)null,
                draftOpenedAtUtc = (string?)null,
                blessingDraft = Array.Empty<object>(),
                selectedBlessingCardIds = Array.Empty<string>()
            }
        });

        var beforeSoulRoot = JsonNode.Parse(await _fs.ReadFileAsync("game_state/meta/soul_state.json")!)!.AsObject();
        var expectedEnlightenment = beforeSoulRoot["enlightenment"]!.DeepClone();
        var engine = CreateGameEngine();
        var stateManager = GetPrivateField<StateManager>(engine, "_stateManager");

        await stateManager.RefreshGameStateAsync();

        var completed = await InvokePrivateAsync<bool>(engine, "TryPerformOrdinaryReturnToChaosSeaFromShiningAbodeAsync");

        Assert.True(completed);

        var soulRoot = JsonNode.Parse(await _fs.ReadFileAsync("game_state/meta/soul_state.json")!)!.AsObject();
        var shiningRoot = JsonNode.Parse(await _fs.ReadFileAsync("game_state/meta/shining_abode_state.json")!)!.AsObject();

        Assert.Equal("Chaos Sea", soulRoot["currentRealm"]?.GetValue<string>());
        Assert.True(JsonNode.DeepEquals(expectedEnlightenment, soulRoot["enlightenment"]));
        Assert.Equal(ShiningAbodeState.AvailabilitySealedUntilNextAscension, shiningRoot["availability"]?.GetValue<string>());
        Assert.Equal("Chaos Sea", stateManager.CurrentState.CurrentRealm);
        Assert.Equal("Сияющий Мудрец", stateManager.CurrentState.EnlightenmentTier);
    }

    [Fact]
    public async Task TryPerformOrdinaryReturnToChaosSeaFromShiningAbodeAsync_BlocksWhenPendingShiningRequestsExist()
    {
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Испытующая Душа",
            currentRealm = "Shining Abode",
            currentIncarnation = 4,
            inkFeathers = new { current = 7, total = 31 },
            enlightenment = new
            {
                currentTier = "Сияющий Мудрец",
                experience = 187,
                level = 6,
                progressPercent = 73
            }
        });
        await WriteJsonAsync("game_state/meta/shining_abode_state.json", new
        {
            availability = "active",
            radiance = new { experience = 22, tier = 3 },
            lightSparks = 88,
            halls = Array.Empty<object>(),
            factions = Array.Empty<object>(),
            shiningPoliticalActors = Array.Empty<object>(),
            preparedIncarnationPackage = (object?)null,
            gates = new
            {
                hasOpenDraft = false,
                isStale = false,
                openedThisAscension = false,
                draftOpenedAtTurn = (int?)null,
                draftOpenedAtUtc = (string?)null,
                blessingDraft = Array.Empty<object>(),
                selectedBlessingCardIds = Array.Empty<string>()
            }
        });

        await ShiningCoreActionRequestState.WriteRequestAsync(_fs, new ShiningCoreActionRequestState.PendingShiningCoreActionRequest
        {
            RequestId = "core-request-1",
            ActionType = ShiningCoreActionRequestState.ActionTypeOpenGates,
            FactionId = "faction-alpha",
            FactionName = "Фракция Альфа",
            CreatedAtTurn = 41,
            CreatedAtUtc = "2026-04-19T00:00:00Z"
        });
        await ShiningTradeRequestState.WriteRequestAsync(_fs, new ShiningTradeRequestState.PendingShiningTradeInventoryRequest
        {
            RequestId = "trade-request-1",
            FactionId = "faction-alpha",
            FactionName = "Фракция Альфа",
            TradeCycleId = "cycle-alpha",
            DerivedTradeTier = 2,
            DerivedTradeSlotCount = 3,
            DerivedRarityCeiling = "legendary",
            DerivedServiceMultiplier = 1.15,
            CreatedAtTurn = 41,
            CreatedAtUtc = "2026-04-19T00:00:00Z"
        });
        await ShiningFactionRequestState.WriteFoundingRequestAsync(_fs, new ShiningFactionRequestState.PendingShiningFactionFoundingRequest
        {
            RequestId = "founding-request-1",
            ProposedFactionId = "faction-founded",
            ProposedHallId = "hall-founded",
            ProposedHallName = "Зал Основания",
            ProposedHallDescription = "Первый зал новой фракции.",
            CreatedAtTurn = 41,
            CreatedAtUtc = "2026-04-19T00:00:00Z"
        });
        await ShiningFactionRequestState.WriteRealignmentRequestAsync(_fs, new ShiningFactionRequestState.PendingShiningFactionRealignmentRequest
        {
            RequestId = "realignment-request-1",
            ResidentId = "resident-alpha",
            ResidentName = "Альфа",
            SourceFactionId = "faction-alpha",
            SourceFactionName = "Фракция Альфа",
            TargetFactionId = "faction-beta",
            TargetFactionName = "Фракция Бета",
            CreatedAtTurn = 41,
            CreatedAtUtc = "2026-04-19T00:00:00Z"
        });
        await ShiningFactionRequestState.WriteLeadershipTransitionRequestAsync(_fs, new ShiningFactionRequestState.PendingShiningFactionLeadershipTransitionRequest
        {
            RequestId = "leadership-request-1",
            FactionId = "faction-alpha",
            FactionName = "Фракция Альфа",
            IncumbentHeadActorType = ShiningAbodeState.HeadActorTypeGuardian,
            IncumbentHeadActorId = "guardian-alpha",
            CandidateHeadActorType = ShiningAbodeState.HeadActorTypePlayerSoul,
            CandidateHeadActorId = "player",
            CreatedAtTurn = 41,
            CreatedAtUtc = "2026-04-19T00:00:00Z"
        });

        Assert.True(_fs.FileExists(ShiningCoreActionRequestState.PendingActionsRequestPath));
        Assert.True(_fs.FileExists(ShiningTradeRequestState.PendingRequestsPath));
        Assert.True(_fs.FileExists(ShiningFactionRequestState.PendingFoundingsRequestPath));
        Assert.True(_fs.FileExists(ShiningFactionRequestState.PendingRealignmentsRequestPath));
        Assert.True(_fs.FileExists(ShiningFactionRequestState.PendingLeadershipTransitionsRequestPath));

        var engine = CreateGameEngine();
        var stateManager = GetPrivateField<StateManager>(engine, "_stateManager");

        await stateManager.RefreshGameStateAsync();

        var completed = await InvokePrivateAsync<bool>(engine, "TryPerformOrdinaryReturnToChaosSeaFromShiningAbodeAsync");

        Assert.False(completed);
        Assert.True(_fs.FileExists(ShiningCoreActionRequestState.PendingActionsRequestPath));
        Assert.True(_fs.FileExists(ShiningTradeRequestState.PendingRequestsPath));
        Assert.True(_fs.FileExists(ShiningFactionRequestState.PendingFoundingsRequestPath));
        Assert.True(_fs.FileExists(ShiningFactionRequestState.PendingRealignmentsRequestPath));
        Assert.True(_fs.FileExists(ShiningFactionRequestState.PendingLeadershipTransitionsRequestPath));

        var soulRoot = JsonNode.Parse(await _fs.ReadFileAsync("game_state/meta/soul_state.json")!)!.AsObject();
        var shiningRoot = JsonNode.Parse(await _fs.ReadFileAsync("game_state/meta/shining_abode_state.json")!)!.AsObject();

        Assert.Equal("Shining Abode", soulRoot["currentRealm"]?.GetValue<string>());
        Assert.Equal(ShiningAbodeState.AvailabilityActive, shiningRoot["availability"]?.GetValue<string>());
        Assert.Equal("Shining Abode", stateManager.CurrentState.CurrentRealm);
        Assert.True(stateManager.CurrentState.IsInShiningAbode);
    }

    [Fact]
    public async Task GetBlockingShiningPendingContractPathsAsync_DeletesExplicitEmptyFilesButKeepsMalformedAndActive()
    {
        await WriteJsonAsync(ShiningCoreActionRequestState.PendingActionsRequestPath, new
        {
            requests = Array.Empty<object>()
        });
        await WriteJsonAsync(ShiningTradeRequestState.PendingRequestsPath, new
        {
            requests = new[]
            {
                new
                {
                    requestId = "trade-request-1",
                    factionId = "faction-alpha",
                    tradeCycleId = "shining_return_4",
                    createdAtTurn = 12
                }
            }
        });
        await _fs.WriteFileAtomicAsync(ShiningFactionRequestState.PendingFoundingsRequestPath, "{ malformed");

        var engine = CreateGameEngine();

        var blockingPaths = await InvokePrivateAsync<IReadOnlyList<string>>(engine, "GetBlockingShiningPendingContractPathsAsync");

        Assert.DoesNotContain(ShiningCoreActionRequestState.PendingActionsRequestPath, blockingPaths);
        Assert.False(_fs.FileExists(ShiningCoreActionRequestState.PendingActionsRequestPath));
        Assert.Contains(blockingPaths, item =>
            item.Contains(ShiningTradeRequestState.PendingRequestsPath, StringComparison.OrdinalIgnoreCase) &&
            item.Contains("trade-request-1", StringComparison.OrdinalIgnoreCase) &&
            item.Contains("shining_return_4", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(ShiningFactionRequestState.PendingFoundingsRequestPath, blockingPaths);
        Assert.True(_fs.FileExists(ShiningTradeRequestState.PendingRequestsPath));
        Assert.True(_fs.FileExists(ShiningFactionRequestState.PendingFoundingsRequestPath));
    }

    [Fact]
    public async Task TryPerformOrdinaryReturnToChaosSeaFromShiningAbodeAsync_BlocksLegacyPendingNativeDiscovery()
    {
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Испытующая Душа",
            currentRealm = "Shining Abode",
            currentIncarnation = 4,
            inkFeathers = new { current = 32, total = 64 }
        });
        await WriteJsonAsync("game_state/meta/shining_abode_state.json", new
        {
            availability = "active",
            radiance = new { experience = 22, tier = 3 },
            lightSparks = 68,
            halls = Array.Empty<object>(),
            factions = Array.Empty<object>(),
            shiningPoliticalActors = Array.Empty<object>(),
            preparedIncarnationPackage = (object?)null,
            pendingNativeFactionDiscovery = new
            {
                requestId = "discover_native_faction:0041",
                createdAtTurn = 41,
                createdAtUtc = "2026-04-19T00:00:00Z",
                radianceTierAtRequest = 3,
                costFeathers = 25,
                costLightSparks = 20
            },
            gates = new
            {
                hasOpenDraft = false,
                isStale = false,
                openedThisAscension = false,
                draftOpenedAtTurn = (int?)null,
                draftOpenedAtUtc = (string?)null,
                blessingDraft = Array.Empty<object>(),
                selectedBlessingCardIds = Array.Empty<string>()
            }
        });

        var engine = CreateGameEngine();
        var stateManager = GetPrivateField<StateManager>(engine, "_stateManager");
        await stateManager.RefreshGameStateAsync();

        var completed = await InvokePrivateAsync<bool>(engine, "TryPerformOrdinaryReturnToChaosSeaFromShiningAbodeAsync");

        Assert.False(completed);
        var soulRoot = JsonNode.Parse(await _fs.ReadFileAsync("game_state/meta/soul_state.json")!)!.AsObject();
        var shiningRoot = JsonNode.Parse(await _fs.ReadFileAsync("game_state/meta/shining_abode_state.json")!)!.AsObject();
        Assert.Equal("Shining Abode", soulRoot["currentRealm"]?.GetValue<string>());
        Assert.NotNull(shiningRoot["pendingNativeFactionDiscovery"]);
        Assert.Equal(ShiningAbodeState.AvailabilityActive, shiningRoot["availability"]?.GetValue<string>());
        Assert.Equal("Shining Abode", stateManager.CurrentState.CurrentRealm);
    }

    [Fact]
    public async Task TryPerformOrdinaryReturnToChaosSeaFromShiningAbodeAsync_BlocksMalformedNonNullLegacyPendingNativeDiscovery()
    {
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Испытующая Душа",
            currentRealm = "Shining Abode",
            currentIncarnation = 4,
            inkFeathers = new { current = 32, total = 64 }
        });
        await WriteJsonAsync("game_state/meta/shining_abode_state.json", new
        {
            availability = "active",
            radiance = new { experience = 22, tier = 3 },
            lightSparks = 68,
            halls = Array.Empty<object>(),
            factions = Array.Empty<object>(),
            shiningPoliticalActors = Array.Empty<object>(),
            preparedIncarnationPackage = (object?)null,
            pendingNativeFactionDiscovery = "malformed_contract",
            gates = new
            {
                hasOpenDraft = false,
                isStale = false,
                openedThisAscension = false,
                draftOpenedAtTurn = (int?)null,
                draftOpenedAtUtc = (string?)null,
                blessingDraft = Array.Empty<object>(),
                selectedBlessingCardIds = Array.Empty<string>()
            }
        });

        var engine = CreateGameEngine();
        var stateManager = GetPrivateField<StateManager>(engine, "_stateManager");
        await stateManager.RefreshGameStateAsync();

        var completed = await InvokePrivateAsync<bool>(engine, "TryPerformOrdinaryReturnToChaosSeaFromShiningAbodeAsync");

        Assert.False(completed);
        var soulRoot = JsonNode.Parse(await _fs.ReadFileAsync("game_state/meta/soul_state.json")!)!.AsObject();
        var shiningRoot = JsonNode.Parse(await _fs.ReadFileAsync("game_state/meta/shining_abode_state.json")!)!.AsObject();
        Assert.Equal("Shining Abode", soulRoot["currentRealm"]?.GetValue<string>());
        Assert.Equal("malformed_contract", shiningRoot["pendingNativeFactionDiscovery"]?.GetValue<string>());
        Assert.Equal(ShiningAbodeState.AvailabilityActive, shiningRoot["availability"]?.GetValue<string>());
    }

    [Fact]
    public async Task UpdateSoulStateRealm_WriteFailureReturnsFalseAndLeavesSoulStateUnchanged()
    {
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Испытующая Душа",
            currentRealm = "Chaos Sea",
            currentIncarnation = 4,
            inkFeathers = new { current = 7, total = 31 }
        });

        var engine = CreateGameEngine();
        var beforeSoulJson = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        using var soulLock = File.Open(
            _fs.ResolvePath("game_state/meta/soul_state.json"),
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);

        var updated = await InvokePrivateAsync<bool>(engine, "UpdateSoulStateRealm", "Shining Abode", null, false);

        Assert.False(updated);
        Assert.Equal(beforeSoulJson, await _fs.ReadFileAsync("game_state/meta/soul_state.json"));
    }

    [Fact]
    public async Task TryPerformOrdinaryReturnToChaosSeaFromShiningAbodeAsync_SoulWriteFailureRestoresShiningStateAndReturnsFalse()
    {
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Испытующая Душа",
            currentRealm = "Shining Abode",
            currentIncarnation = 4,
            inkFeathers = new { current = 7, total = 31 },
            enlightenment = new
            {
                currentTier = "Сияющий Мудрец",
                experience = 187,
                level = 6,
                progressPercent = 73
            }
        });
        await WriteJsonAsync("game_state/meta/shining_abode_state.json", new
        {
            availability = "active",
            radiance = new { experience = 22, tier = 3 },
            lightSparks = 88,
            halls = Array.Empty<object>(),
            factions = Array.Empty<object>(),
            shiningPoliticalActors = Array.Empty<object>(),
            preparedIncarnationPackage = (object?)null,
            gates = new
            {
                hasOpenDraft = false,
                isStale = false,
                openedThisAscension = false,
                draftOpenedAtTurn = (int?)null,
                draftOpenedAtUtc = (string?)null,
                blessingDraft = Array.Empty<object>(),
                selectedBlessingCardIds = Array.Empty<string>()
            }
        });

        var beforeSoulJson = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        var beforeShiningJson = await _fs.ReadFileAsync("game_state/meta/shining_abode_state.json");
        var engine = CreateGameEngine();
        var stateManager = GetPrivateField<StateManager>(engine, "_stateManager");

        await stateManager.RefreshGameStateAsync();
        using var soulLock = File.Open(
            _fs.ResolvePath("game_state/meta/soul_state.json"),
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);

        var completed = await InvokePrivateAsync<bool>(engine, "TryPerformOrdinaryReturnToChaosSeaFromShiningAbodeAsync");

        Assert.False(completed);
        Assert.Equal(beforeSoulJson, await _fs.ReadFileAsync("game_state/meta/soul_state.json"));
        Assert.Equal(beforeShiningJson, await _fs.ReadFileAsync("game_state/meta/shining_abode_state.json"));
    }

    private async Task WriteJsonAsync(string relativePath, object payload)
    {
        await _fs.WriteFileAtomicAsync(
            relativePath,
            JsonSerializer.Serialize(payload, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
    }

    private async Task WritePendingTurnSnapshotManifestAsync(
        string sessionId,
        string requestId,
        int turnNumber,
        params string[] trackedPaths)
    {
        var files = trackedPaths.ToDictionary(
            path => path,
            path => $"game_state/control/pending_turn_snapshot/{path}",
            StringComparer.OrdinalIgnoreCase);
        var snapshotHashes = trackedPaths.ToDictionary(
            path => path,
            path =>
            {
                var snapshotPath = _fs.ResolvePath($"game_state/control/pending_turn_snapshot/{path}");
                return ComputeSha256(File.ReadAllText(snapshotPath, Encoding.UTF8));
            },
            StringComparer.OrdinalIgnoreCase);

        var manifest = new PendingTurnSnapshotManifestPayload
        {
            SessionId = sessionId,
            RequestId = requestId,
            TurnNumber = turnNumber,
            RequestTimestamp = "2026-03-24T00:00:00Z",
            PlayerAction = "game-engine-turn-lifecycle-test",
            ProgressionControl = new ProgressionControl { CurrentRealm = "Mortal World" },
            Files = files,
            SnapshotFileHashes = snapshotHashes,
            ClientOwnedValidationHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            RollbackBackups = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            RollbackBaselineFiles = trackedPaths.ToList(),
            SourceLabel = "game-engine-turn-lifecycle-tests"
        };
        manifest.ManifestPayloadHash = ComputeManifestPayloadHash(manifest);

        await WriteJsonAsync("game_state/control/pending_turn_snapshot.json", new
        {
            sessionId,
            requestId,
            turnNumber,
            requestTimestamp = manifest.RequestTimestamp,
            playerAction = manifest.PlayerAction,
            progressionControl = manifest.ProgressionControl,
            files,
            snapshotFileHashes = snapshotHashes,
            clientOwnedValidationHashes = manifest.ClientOwnedValidationHashes,
            rollbackBackups = manifest.RollbackBackups,
            rollbackBaselineFiles = manifest.RollbackBaselineFiles,
            sourceLabel = manifest.SourceLabel,
            manifestPayloadHash = manifest.ManifestPayloadHash
        });
        await PendingTurnSnapshotTestAuthority.SyncAuthorityForCurrentManifestAsync(_fs);
    }

    private static string ComputeManifestPayloadHash(PendingTurnSnapshotManifestPayload manifest)
    {
        var originalHash = manifest.ManifestPayloadHash;
        manifest.ManifestPayloadHash = string.Empty;
        var payload = JsonSerializer.Serialize(manifest, SnapshotHashJsonOpts);
        manifest.ManifestPayloadHash = originalHash;
        return ComputeSha256(payload);
    }

    private static string ComputeSha256(string content)
    {
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(content)));
    }

    private GameEngine CreateGameEngine()
    {
        var settings = new GameSettings();
        var stateManager = new StateManager(_fs, settings, NullLogger<StateManager>.Instance);
        var localization = new LocalizationManager { CurrentLanguage = "ru" };
        var gameLoop = new GameLoop();
        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        var progressionSchedule = new ProgressionScheduleService(_fs, NullLogger<ProgressionScheduleService>.Instance);
        var gameInterface = new GameInterface(localization, settings);
        var clipboardService = new TestClipboardService();
        var explorer = new ExplorerMode(stateManager, _fs, localization, clipboardService: clipboardService, console: new TestExplorerConsole());
        var saveLoad = new SaveLoadService(_fs, stateManager, NullLogger<SaveLoadService>.Instance);
        var imageService = new ImageService(_fs, settings, localization, NullLogger<ImageService>.Instance);
        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var characteristicsService = new CharacteristicsService(_fs, stateManager, NullLogger<CharacteristicsService>.Instance);
        var storyService = new StoryService(_fs, NullLogger<StoryService>.Instance);
        var actorMemoryService = new ActorMemoryService(_fs, NullLogger<ActorMemoryService>.Instance);
        var audioService = new AudioService(_fs, settings, NullLogger<AudioService>.Instance);
        var consoleAppearance = new ConsoleAppearanceService(settings, NullLogger<ConsoleAppearanceService>.Instance);
        var systemModService = new SystemModService(_fs, settings, NullLogger<SystemModService>.Instance);
        var systemGuardianLibraryService = new SystemGuardianLibraryService(_fs, NullLogger<SystemGuardianLibraryService>.Instance);
        var criticalStateHealth = new CriticalStateHealthService(_fs, NullLogger<CriticalStateHealthService>.Instance);
        var worldDirectiveService = new WorldDirectiveService(_fs, NullLogger<WorldDirectiveService>.Instance);
        var scenarioCoreService = new ScenarioCoreService(_fs, NullLogger<ScenarioCoreService>.Instance);
        var afterlifeArchiveCandidateService = new AfterlifeArchiveCandidateService(_fs, NullLogger<AfterlifeArchiveCandidateService>.Instance);
        var afterlifeReturnGuardService = new AfterlifeReturnGuardService(_fs, NullLogger<AfterlifeReturnGuardService>.Instance);
        var rivalSoulArcService = new RivalSoulArcService(_fs, NullLogger<RivalSoulArcService>.Instance);
        var guardianCorrectionService = new GuardianCorrectionService(_fs, scenarioCoreService, NullLogger<GuardianCorrectionService>.Instance);
        var pendingTurnState = new PendingTurnStateService(_fs, NullLogger<PendingTurnStateService>.Instance);
        var stateDistributor = new StateDistributor(_fs, NullLogger<StateDistributor>.Instance);
        var qteSceneService = new QteSceneService(
            _fs,
            settings,
            characteristicsService,
            imageService,
            audioService,
            stateDistributor,
            validator,
            normalizer,
            stateManager,
            NullLogger<QteSceneService>.Instance);

        return new GameEngine(
            _fs,
            stateManager,
            gameLoop,
            normalizer,
            progressionSchedule,
            gameInterface,
            explorer,
            localization,
            saveLoad,
            imageService,
            validator,
            characteristicsService,
            storyService,
            actorMemoryService,
            audioService,
            consoleAppearance,
            systemModService,
            systemGuardianLibraryService,
            criticalStateHealth,
            worldDirectiveService,
            scenarioCoreService,
            afterlifeArchiveCandidateService,
            afterlifeReturnGuardService,
            rivalSoulArcService,
            guardianCorrectionService,
            pendingTurnState,
            qteSceneService,
            clipboardService,
            NullLogger<GameEngine>.Instance);
    }

    private static T GetPrivateField<T>(object instance, string fieldName) where T : class
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        var value = field!.GetValue(instance) as T;
        Assert.NotNull(value);
        return value!;
    }

    private static async Task<T> InvokePrivateAsync<T>(object instance, string methodName, params object?[]? args)
    {
        var method = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var task = method!.Invoke(instance, args) as Task<T>;
        Assert.NotNull(task);
        return await task!;
    }

    private static void InvokePrivate(object instance, string methodName, params object?[]? args)
    {
        var method = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(instance, args);
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
            // best-effort cleanup
        }
    }
}
