using System.Security.Cryptography;
using System.Collections;
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
using Spectre.Console;
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
        public int[]? PreGeneratedDices1d20 { get; set; }
        public JsonObject? GachaBaseResult { get; set; }
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
    public async Task ProcessPlayerTurn_UnresolvedRealm_DoesNotCreatePendingDiceState()
    {
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Тестовая Душа",
            currentRealm = "",
            currentIncarnation = 1,
            inkFeathers = new { current = 50 }
        });
        var engine = CreateGameEngine();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            InvokePrivateTaskAsync(engine, "ProcessPlayerTurn", "Тестовый ход", null));

        Assert.Contains("currentRealm", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(_fs.FileExists(PendingTurnStateService.PendingDiceStatePath));
        Assert.False(_fs.FileExists("input/turn_request.json"));
    }

    [Fact]
    public async Task HasCurrentSessionAsync_TerminalSoulDissipation_BlocksContinueSession()
    {
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Развеянная Душа",
            currentRealm = "Chaos Sea",
            currentIncarnation = 3,
            terminalGameOver = new
            {
                state = "soul_dispersed",
                message = AfterlifeSpiritualConflictState.TerminalSoulDissipationMessage,
                conflictId = "afterlife_conflict_terminal_001",
                proofId = "soul_dissipation_proof_terminal_001"
            }
        });
        var engine = CreateGameEngine();

        var hasCurrentSession = await InvokePrivateAsync<bool>(engine, "HasCurrentSessionAsync");

        Assert.False(hasCurrentSession);
        var warning = GetPrivateField<string>(engine, "_mainMenuSessionWarning");
        Assert.Contains(AfterlifeSpiritualConflictState.TerminalSoulDissipationMessage, warning, StringComparison.Ordinal);
        Assert.Contains("загрузите сохранение", warning, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryDescribeMalformedPendingWorldSetup_RejectsNullWorldDirectives()
    {
        var method = typeof(GameEngine).GetMethod(
            "TryDescribeMalformedPendingWorldSetup",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);

        object?[] args =
        {
            """
            {
              "mode": "manual",
              "worldDirectives": null
            }
            """,
            string.Empty
        };

        var malformed = Assert.IsType<bool>(method!.Invoke(null, args));

        Assert.True(malformed);
        var description = Assert.IsType<string>(args[1]);
        Assert.Contains("worldDirectives", description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("non-null JSON object", description, StringComparison.OrdinalIgnoreCase);
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
    public async Task CollectIncarnationBlockersAsync_NpcSocialAndTradeBlockSoulGates()
    {
        await WriteJsonAsync(ActorSocialInteractionRequestState.PendingNpcRequestPath, new
        {
            requests = new[]
            {
                new
                {
                    requestId = "npc_social_alpha",
                    npcId = "npc_mira",
                    npcName = "Мира",
                    interactionType = "talk",
                    createdAtTurn = 14,
                    createdAtUtc = "2026-04-27T14:00:00Z"
                }
            }
        });
        await WriteJsonAsync(NpcTradeRequestState.PendingRequestPath, new
        {
            requests = new[]
            {
                new
                {
                    requestId = "npc_trade_alpha",
                    npcId = "npc_mira",
                    npcName = "Мира",
                    merchantProfile = "local_merchant",
                    tradeCycleId = "mortal_trade_14",
                    derivedTradeSlotCount = 4,
                    createdAtTurn = 14,
                    createdAtUtc = "2026-04-27T14:00:00Z"
                }
            }
        });
        var engine = CreateGameEngine();

        var blockers = await InvokePrivateAsync<List<string>>(engine, "CollectIncarnationBlockersAsync");

        Assert.Contains(blockers, blocker =>
            blocker.Contains(ActorSocialInteractionRequestState.PendingNpcRequestPath, StringComparison.OrdinalIgnoreCase) &&
            blocker.Contains("npc_social_alpha", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(blockers, blocker =>
            blocker.Contains(NpcTradeRequestState.PendingRequestPath, StringComparison.OrdinalIgnoreCase) &&
            blocker.Contains("npc_trade_alpha", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CollectIncarnationBlockersAsync_EmptyGuardianSocialRequestsDoNotBlockSoulGates()
    {
        await WriteJsonAsync(ActorSocialInteractionRequestState.PendingGuardianRequestPath, new
        {
            requests = Array.Empty<object>()
        });
        var engine = CreateGameEngine();

        var blockers = await InvokePrivateAsync<List<string>>(engine, "CollectIncarnationBlockersAsync");

        Assert.Empty(blockers);
        Assert.False(_fs.FileExists(ActorSocialInteractionRequestState.PendingGuardianRequestPath));
    }

    [Theory]
    [InlineData(GuardianAbodeResidentRequestState.PendingResidentsRequestPath)]
    [InlineData(GuardianAbodeResidentRequestState.PendingInteractionsRequestPath)]
    [InlineData(GuardianAbodeResidentRequestState.PendingTransfersRequestPath)]
    public async Task CollectIncarnationBlockersAsync_EmptyResidentRequestBundlesDoNotBlockSoulGates(string pendingPath)
    {
        await WriteJsonAsync(pendingPath, new
        {
            requests = Array.Empty<object>()
        });
        var engine = CreateGameEngine();

        var blockers = await InvokePrivateAsync<List<string>>(engine, "CollectIncarnationBlockersAsync");

        Assert.Empty(blockers);
        Assert.False(_fs.FileExists(pendingPath));
    }

    [Fact]
    public async Task CollectIncarnationBlockersAsync_ValidManifestationRequestDoesNotBlockSoulGates()
    {
        await WriteJsonAsync(GuardianAbodeResidentRequestState.PendingManifestationRequestPath, new
        {
            requests = new[]
            {
                new
                {
                    requestId = "manifest_next_life",
                    manifestationSource = "resident_relic",
                    relicId = "relic_echo",
                    relicName = "Эхо Лиоры",
                    sourceResidentId = "resident_liora",
                    sourceGuardianId = "guardian_azalia",
                    sourceGuardianName = "Азалия",
                    targetIncarnation = 5,
                    companionNameHint = "Лиора",
                    originWorldSummary = "Следующая смертная жизнь.",
                    futureCompanionPrompt = "Лиора проявится как ранняя спутница в следующей смертной жизни.",
                    bondReason = "Связь закреплена через реликвию резидента.",
                    coreTraits = new[] { "loyal" },
                    archetypeHints = new[] { "guide" },
                    appearanceMotifs = new[] { "dawn" },
                    createdAtUtc = "2026-04-27T14:00:00Z"
                }
            }
        });
        var engine = CreateGameEngine();

        var blockers = await InvokePrivateAsync<List<string>>(engine, "CollectIncarnationBlockersAsync");

        Assert.Empty(blockers);
        Assert.True(_fs.FileExists(GuardianAbodeResidentRequestState.PendingManifestationRequestPath));
    }

    [Fact]
    public async Task CollectIncarnationBlockersAsync_ShiningArrayPayloadIsSafeForSoulGatesPanel()
    {
        await WriteJsonAsync(ShiningCoreActionRequestState.PendingActionsRequestPath, new
        {
            requests = new[]
            {
                new
                {
                    requestId = "core_blocker_array_001",
                    actionType = "prepare_incarnation_package",
                    selectedCardIds = new[] { "card_alpha", "card_beta" },
                    createdAtTurn = 14,
                    createdAtUtc = "2026-04-28T00:00:00Z"
                }
            }
        });
        var engine = CreateGameEngine();

        var blockers = await InvokePrivateAsync<List<string>>(engine, "CollectIncarnationBlockersAsync");

        var blocker = Assert.Single(blockers);
        Assert.Contains("core_blocker_array_001", blocker, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("selectedCardIds=[card_alpha, card_beta]", blocker, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"selectedCardIds\": [", blocker, StringComparison.OrdinalIgnoreCase);

        var panelBody = string.Join("\n", new[]
        {
            "Нельзя войти в новую смертную жизнь, пока остаются незакрытые загробные контракты.",
            string.Empty,
            string.Join("\n", blockers.Select(item => $"• {item}")),
            string.Empty,
            "Сначала дождитесь явного закрытия GM или почините повреждённый pending contract."
        });
        var ex = Record.Exception(() => new Panel(GameInterface.SafeMarkup(panelBody)));

        Assert.Null(ex);
    }

    [Fact]
    public async Task CollectIncarnationBlockersAsync_SourceOfLightPendingBlocksSoulGates()
    {
        await WriteJsonAsync(
            SourceOfLightCapstoneState.PendingRequestPath,
            SourceOfLightCapstoneState.CreateRequest(12, 580, 4));
        var engine = CreateGameEngine();

        var blockers = await InvokePrivateAsync<List<string>>(engine, "CollectIncarnationBlockersAsync");

        Assert.Contains(blockers, blocker =>
            blocker.Contains(SourceOfLightCapstoneState.PendingRequestPath, StringComparison.OrdinalIgnoreCase) &&
            blocker.Contains(SourceOfLightCapstoneState.PassiveId, StringComparison.OrdinalIgnoreCase) &&
            blocker.Contains(SourceOfLightCapstoneState.RelicId, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CollectIncarnationBlockersAsync_ActiveSpiritualConflictBlocksSoulGates()
    {
        await WriteJsonAsync(AfterlifeSpiritualConflictState.StatePath, new
        {
            schemaVersion = 1,
            activeConflict = new
            {
                conflictId = "afterlife_conflict_active_gate_blocker",
                realm = "Chaos Sea",
                operationType = "pressure",
                resolutionState = "active"
            },
            recentConflicts = Array.Empty<object>()
        });
        var engine = CreateGameEngine();

        var blockers = await InvokePrivateAsync<List<string>>(engine, "CollectIncarnationBlockersAsync");

        Assert.Contains(blockers, blocker =>
            blocker.Contains(AfterlifeSpiritualConflictState.StatePath, StringComparison.OrdinalIgnoreCase) &&
            blocker.Contains("activeConflict", StringComparison.OrdinalIgnoreCase) &&
            blocker.Contains("resolve", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ChaosSeaToSoulGatesJourney_HygieneBlockersAndSnapshotsRemainConsistent()
    {
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Тестовая Душа",
            currentRealm = "Chaos Sea",
            currentIncarnation = 4,
            inkFeathers = new { current = 40 }
        });
        await WriteJsonAsync(ActorSocialInteractionRequestState.PendingGuardianRequestPath, new { requests = Array.Empty<object>() });
        await WriteJsonAsync(GuardianAbodeResidentRequestState.PendingResidentsRequestPath, new { requests = Array.Empty<object>() });
        Directory.CreateDirectory(_fs.ResolvePath("game_state/control/pending_turn_snapshot"));
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
        await WriteJsonAsync(AfterlifeSpiritualConflictState.StatePath, new
        {
            schemaVersion = 1,
            activeConflict = new
            {
                conflictId = "afterlife_conflict_gate_journey",
                realm = "Chaos Sea",
                operationType = "pressure",
                resolutionState = "active"
            },
            recentConflicts = Array.Empty<object>()
        });
        var engine = CreateGameEngine();

        var initialBlockers = await InvokePrivateAsync<List<string>>(engine, "CollectIncarnationBlockersAsync");

        Assert.Contains(initialBlockers, blocker =>
            blocker.Contains("притяжение к извечному Хранителю", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(initialBlockers, blocker =>
            blocker.Contains(AfterlifeSpiritualConflictState.StatePath, StringComparison.OrdinalIgnoreCase) &&
            blocker.Contains("activeConflict", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(initialBlockers, blocker =>
            blocker.Contains("pending_turn_snapshot", StringComparison.OrdinalIgnoreCase));
        Assert.False(_fs.FileExists(ActorSocialInteractionRequestState.PendingGuardianRequestPath));
        Assert.False(_fs.FileExists(GuardianAbodeResidentRequestState.PendingResidentsRequestPath));

        _fs.DeleteFile(SystemGuardianLibraryService.AttractionRequestPath);
        await WriteJsonAsync(AfterlifeSpiritualConflictState.StatePath, new
        {
            schemaVersion = 1,
            activeConflict = (object?)null,
            recentConflicts = new[]
            {
                new
                {
                    conflictId = "afterlife_conflict_gate_journey",
                    realm = "Chaos Sea",
                    resolutionState = "repair_cancelled",
                    resolvedAtTurn = 40,
                    repairReason = "test cleanup before Soul Gates"
                }
            }
        });
        await _fs.WriteFileAtomicAsync(GuardianTradeRequestState.PendingRequestPath, "{ malformed");

        var malformedBlockers = await InvokePrivateAsync<List<string>>(engine, "CollectIncarnationBlockersAsync");

        Assert.Contains(malformedBlockers, blocker =>
            blocker.Contains(GuardianTradeRequestState.PendingRequestPath, StringComparison.OrdinalIgnoreCase) &&
            blocker.Contains("повреждённый", StringComparison.OrdinalIgnoreCase));

        _fs.DeleteFile(GuardianTradeRequestState.PendingRequestPath);
        var clearBlockers = await InvokePrivateAsync<List<string>>(engine, "CollectIncarnationBlockersAsync");
        Assert.Empty(clearBlockers);

        var request = new TurnRequest
        {
            SessionId = "session_chaos_soul_gates_journey",
            RequestId = "request_chaos_soul_gates_journey",
            TurnNumber = 41,
            PlayerAction = "Soul Gates prep after Chaos Sea journey",
            Timestamp = "2026-03-24T00:00:00Z",
            ProgressionControl = new ProgressionControl { CurrentRealm = "Chaos Sea" }
        };
        await InvokePrivateTaskResultAsync(engine, "CreateCanonicalBaselineSnapshotAsync", request, null, "chaos-to-soul-gates-journey");

        var manifestJson = await _fs.ReadFileAsync("game_state/control/pending_turn_snapshot.json");
        Assert.NotNull(manifestJson);
        var manifest = Assert.IsType<JsonObject>(JsonNode.Parse(manifestJson!)!);
        var files = Assert.IsType<JsonObject>(manifest["files"]);
        var snapshotHashes = Assert.IsType<JsonObject>(manifest["snapshotFileHashes"]);
        var rollbackBaselineFiles = Assert.IsType<JsonArray>(manifest["rollbackBaselineFiles"])
            .Select(node => node?.GetValue<string>() ?? string.Empty)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.True(files.ContainsKey("game_state/meta/soul_state.json"));
        Assert.True(files.ContainsKey(AfterlifeSpiritualConflictState.StatePath));
        Assert.False(files.ContainsKey(SystemGuardianLibraryService.AttractionRequestPath));
        Assert.False(files.ContainsKey(GuardianTradeRequestState.PendingRequestPath));
        Assert.Contains("game_state/meta/soul_state.json", rollbackBaselineFiles);
        Assert.Contains(AfterlifeSpiritualConflictState.StatePath, rollbackBaselineFiles);
        Assert.DoesNotContain(SystemGuardianLibraryService.AttractionRequestPath, rollbackBaselineFiles);

        foreach (var fileEntry in files)
        {
            var snapshotPath = fileEntry.Value?.GetValue<string>();
            Assert.False(string.IsNullOrWhiteSpace(snapshotPath));
            Assert.True(_fs.FileExists(snapshotPath!), $"{snapshotPath} should exist for {fileEntry.Key}.");
            Assert.True(snapshotHashes.TryGetPropertyValue(fileEntry.Key, out var hashNode));
            var expectedHash = hashNode?.GetValue<string>();
            Assert.False(string.IsNullOrWhiteSpace(expectedHash));
            var snapshotContent = await _fs.ReadFileAsync(snapshotPath!);
            Assert.NotNull(snapshotContent);
            Assert.Equal(expectedHash, ComputeSha256(snapshotContent!), ignoreCase: true);
        }
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
    public async Task NormalizeRuntimeUiArtifactsAsync_PreservesResolvedPendingContractsBeforeValidation_AndAcceptedCleanupClearsThem()
    {
        const string sessionId = "session-terminal-validation";
        const string requestId = "request-terminal-validation";
        const int turnNumber = 21;
        var pendingRequest = new
        {
            requestId = "guardian_trade_late_response",
            guardianId = "guardian_alpha",
            guardianName = "Азалия",
            abodeId = "abode_alpha",
            returnCycleId = "return_21",
            currentReputation = 110,
            derivedTradeSlotCount = 1,
            effectiveRarityCeilingBonusSteps = 0,
            projectBonusSignature = "0|0|0",
            createdAtUtc = "2026-04-27T00:00:00Z",
            createdAtTurn = turnNumber
        };

        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            currentRealm = "Chaos Sea",
            currentIncarnation = 4,
            inkFeathers = new { current = 10, total = 10 },
            soulRelics = new
            {
                equipped = Array.Empty<object>(),
                stored = Array.Empty<object>()
            }
        });
        await WriteJsonAsync("game_state/meta/guardians.json", new
        {
            guardians = new[]
            {
                new
                {
                    guardianId = "guardian_alpha",
                    guardianName = "Азалия",
                    tradeInventory = new
                    {
                        tradeCycleId = "return_21",
                        generatedAtUtc = "2026-04-27T01:00:00Z",
                        generationReputationTier = "Friendly",
                        pricingReputationTier = "Friendly",
                        effectiveRarityCeilingBonusSteps = 0,
                        projectBonusSignature = "0|0|0",
                        items = new[]
                        {
                            new { slotId = "slot_guardian_trade_late_response_001" }
                        }
                    },
                    tradeInventoryReceipts = new[]
                    {
                        new
                        {
                            requestId = "guardian_trade_late_response",
                            guardianId = "guardian_alpha",
                            guardianName = "Азалия",
                            abodeId = "abode_alpha",
                            tradeCycleId = "return_21",
                            status = "ready",
                            itemCount = 1,
                            resolvedAtTurn = turnNumber,
                            resolvedAtUtc = "2026-04-27T01:01:00Z"
                        }
                    }
                }
            }
        });
        await WriteJsonAsync(GuardianTradeRequestState.PendingRequestPath, pendingRequest);
        await WriteJsonAsync(
            $"game_state/control/pending_turn_snapshot/{GuardianTradeRequestState.PendingRequestPath}",
            pendingRequest);
        await WriteJsonAsync("input/turn_request.json", new
        {
            sessionId,
            requestId,
            turnNumber
        });
        await WriteJsonAsync("ready/turn_complete.json", new
        {
            accepted = true,
            sessionId,
            requestId,
            turnNumber
        });
        await WritePendingTurnSnapshotManifestAsync(
            sessionId,
            requestId,
            turnNumber,
            GuardianTradeRequestState.PendingRequestPath);

        var engine = CreateGameEngine();

        await InvokePrivateTaskAsync(engine, "NormalizeRuntimeUiArtifactsAsync");

        Assert.True(_fs.FileExists(GuardianTradeRequestState.PendingRequestPath));

        await InvokePrivateTaskAsync(engine, "CleanupAcceptedTurnTerminalArtifactsAsync");

        Assert.False(_fs.FileExists(GuardianTradeRequestState.PendingRequestPath));
    }

    [Fact]
    public async Task CleanupAcceptedTurnTerminalArtifactsAsync_PreservesSnapshotButNotReadyForIncarnationTrigger()
    {
        const string sessionId = "session-late-incarnation";
        const string requestId = "request-late-incarnation";
        const int turnNumber = 23;
        await WriteJsonAsync("game_state/control/pending_turn_snapshot/game_state/meta/soul_state.json", new
        {
            currentRealm = "Chaos Sea",
            currentIncarnation = 2
        });
        await WritePendingTurnSnapshotManifestAsync(sessionId, requestId, turnNumber, "game_state/meta/soul_state.json");
        await WriteJsonAsync("ready/turn_complete.json", new
        {
            accepted = true,
            sessionId,
            requestId,
            turnNumber
        });
        await WriteJsonAsync("game_state/control/incarnation_trigger.json", new
        {
            worldDescription = "Тестовый смертный мир.",
            characterDescription = "Тестовая душа.",
            circumstances = "Проверка late accepted trigger.",
            source = "test"
        });

        var engine = CreateGameEngine();

        await InvokePrivateTaskAsync(engine, "CleanupAcceptedTurnTerminalArtifactsAsync");

        Assert.False(_fs.FileExists("ready/turn_complete.json"));
        Assert.True(_fs.FileExists("game_state/control/pending_turn_snapshot.json"));
        Assert.True(_fs.FileExists("game_state/control/incarnation_trigger.json"));
    }

    [Fact]
    public async Task CleanupAcceptedTurnTerminalArtifactsAsync_WithoutIncarnationTrigger_RemovesTerminalContext()
    {
        const string sessionId = "session-normal-cleanup";
        const string requestId = "request-normal-cleanup";
        const int turnNumber = 24;
        await WriteJsonAsync("game_state/control/pending_turn_snapshot/game_state/meta/soul_state.json", new
        {
            currentRealm = "Chaos Sea",
            currentIncarnation = 2
        });
        await WritePendingTurnSnapshotManifestAsync(sessionId, requestId, turnNumber, "game_state/meta/soul_state.json");
        await WriteJsonAsync("ready/turn_complete.json", new
        {
            accepted = true,
            sessionId,
            requestId,
            turnNumber
        });

        var engine = CreateGameEngine();

        await InvokePrivateTaskAsync(engine, "CleanupAcceptedTurnTerminalArtifactsAsync");

        Assert.False(_fs.FileExists("ready/turn_complete.json"));
        Assert.False(_fs.FileExists("game_state/control/pending_turn_snapshot.json"));
    }

    [Fact]
    public async Task CheckGmIncarnationTrigger_InputOnlySnapshotWithoutAcceptedContext_DoesNotDispatch()
    {
        const string sessionId = "session-input-only-incarnation";
        const string requestId = "request-input-only-incarnation";
        const int turnNumber = 25;
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Тестовая Душа",
            currentRealm = "Chaos Sea",
            currentIncarnation = 2,
            inkFeathers = new { current = 12 }
        });
        await WriteJsonAsync("game_state/control/pending_turn_snapshot/game_state/meta/soul_state.json", new
        {
            currentRealm = "Chaos Sea",
            currentIncarnation = 2
        });
        await WritePendingTurnSnapshotManifestAsync(sessionId, requestId, turnNumber, "game_state/meta/soul_state.json");
        await WriteJsonAsync("input/turn_request.json", new
        {
            sessionId,
            requestId,
            turnNumber,
            playerAction = "Обычный ожидающий ход без accepted ready signal."
        });
        await WriteJsonAsync("game_state/control/incarnation_trigger.json", new
        {
            worldDescription = "Тестовый смертный мир.",
            characterDescription = "Тестовая душа.",
            circumstances = "Этот trigger не подтверждён accepted turn.",
            source = "test"
        });

        var engine = CreateGameEngine();
        var stateManager = GetPrivateField<StateManager>(engine, "_stateManager");
        await stateManager.RefreshGameStateAsync();

        var dispatched = await InvokePrivateAsync<bool>(engine, "CheckGmIncarnationTrigger", new object?[] { null });

        Assert.False(dispatched);
        Assert.False(_fs.FileExists("game_state/control/incarnation_trigger.json"));
        Assert.False(_fs.FileExists("game_state/control/pending_turn_snapshot.json"));
        var inputTurn = await _fs.ReadFileAsync("input/turn_request.json");
        Assert.NotNull(inputTurn);
        Assert.Contains(requestId, inputTurn, StringComparison.Ordinal);
        Assert.DoesNotContain("Тестовый смертный мир", inputTurn, StringComparison.Ordinal);
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
    public async Task ResolveLifecycleAuthorizedTriggerLifeEndFromPendingSnapshotAsync_GachaManifest_Authorizes()
    {
        await WriteJsonAsync("game_state/control/pending_turn_snapshot/game_state/meta/soul_state.json", new
        {
            currentRealm = "Mortal World",
            currentIncarnation = 2
        });
        await WritePendingTurnSnapshotManifestAsync(
            "test-session",
            "test-request",
            14,
            new JsonObject
            {
                ["baseRarity"] = "Rare",
                ["formula"] = "test-gacha-roll"
            },
            "game_state/meta/soul_state.json");
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

        var manifest = JsonNode.Parse((await _fs.ReadFileAsync("game_state/control/pending_turn_snapshot.json"))!)!.AsObject();
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
    public async Task TryPerformOrdinaryReturnToChaosSeaFromShiningAbodeAsync_ResetsEnlightenmentAndPreservesInkFeathers()
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
            },
            soulProgression = new
            {
                tier = 4,
                tierName = "Transcendence",
                progressPercent = 100,
                totalExperience = 999,
                experienceInCurrentTier = 999
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

        var engine = CreateGameEngine();
        var stateManager = GetPrivateField<StateManager>(engine, "_stateManager");

        await stateManager.RefreshGameStateAsync();

        var completed = await InvokePrivateAsync<bool>(engine, "TryPerformOrdinaryReturnToChaosSeaFromShiningAbodeAsync");

        Assert.True(completed);

        var soulRoot = JsonNode.Parse((await _fs.ReadFileAsync("game_state/meta/soul_state.json"))!)!.AsObject();
        var shiningRoot = JsonNode.Parse((await _fs.ReadFileAsync("game_state/meta/shining_abode_state.json"))!)!.AsObject();

        Assert.Equal("Chaos Sea", soulRoot["currentRealm"]?.GetValue<string>());
        var enlightenment = Assert.IsType<JsonObject>(soulRoot["enlightenment"]);
        Assert.Equal("Новичок", enlightenment["currentTier"]?.GetValue<string>());
        Assert.Equal(0, enlightenment["experience"]?.GetValue<int>());
        Assert.Equal(0, enlightenment["level"]?.GetValue<int>());
        Assert.Equal(0, enlightenment["progressPercent"]?.GetValue<int>());
        var soulProgression = Assert.IsType<JsonObject>(soulRoot["soulProgression"]);
        Assert.Equal(0, soulProgression["tier"]?.GetValue<int>());
        Assert.Equal("Новичок", soulProgression["tierName"]?.GetValue<string>());
        Assert.Equal(0, soulProgression["progressPercent"]?.GetValue<int>());
        Assert.Equal(0, soulProgression["totalExperience"]?.GetValue<int>());
        Assert.Equal(0, soulProgression["experienceInCurrentTier"]?.GetValue<int>());
        var inkFeathers = Assert.IsType<JsonObject>(soulRoot["inkFeathers"]);
        Assert.Equal(7, inkFeathers["current"]?.GetValue<int>());
        Assert.Equal(31, inkFeathers["total"]?.GetValue<int>());
        Assert.Equal(ShiningAbodeState.AvailabilitySealedUntilNextAscension, shiningRoot["availability"]?.GetValue<string>());
        Assert.Equal("Chaos Sea", stateManager.CurrentState.CurrentRealm);
        Assert.Equal("Новичок", stateManager.CurrentState.EnlightenmentTier);
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

        var soulRoot = JsonNode.Parse((await _fs.ReadFileAsync("game_state/meta/soul_state.json"))!)!.AsObject();
        var shiningRoot = JsonNode.Parse((await _fs.ReadFileAsync("game_state/meta/shining_abode_state.json"))!)!.AsObject();

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
        await WriteJsonAsync(ShiningFactionRequestState.PendingRealignmentsRequestPath, new { });
        await WriteJsonAsync(
            SourceOfLightCapstoneState.PendingRequestPath,
            SourceOfLightCapstoneState.CreateRequest(12, 580, 4));

        var engine = CreateGameEngine();

        var blockingPaths = await InvokePrivateAsync<IReadOnlyList<string>>(engine, "GetBlockingShiningPendingContractPathsAsync");

        Assert.DoesNotContain(ShiningCoreActionRequestState.PendingActionsRequestPath, blockingPaths);
        Assert.False(_fs.FileExists(ShiningCoreActionRequestState.PendingActionsRequestPath));
        Assert.Contains(blockingPaths, item =>
            item.Contains(ShiningTradeRequestState.PendingRequestsPath, StringComparison.OrdinalIgnoreCase) &&
            item.Contains("trade-request-1", StringComparison.OrdinalIgnoreCase) &&
            item.Contains("shining_return_4", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(blockingPaths, item =>
            item.Contains(SourceOfLightCapstoneState.PendingRequestPath, StringComparison.OrdinalIgnoreCase) &&
            item.Contains(SourceOfLightCapstoneState.PassiveId, StringComparison.OrdinalIgnoreCase) &&
            item.Contains(SourceOfLightCapstoneState.RelicId, StringComparison.OrdinalIgnoreCase));
        Assert.Contains(blockingPaths, item =>
            item.Contains(ShiningFactionRequestState.PendingFoundingsRequestPath, StringComparison.OrdinalIgnoreCase) &&
            item.Contains("malformed", StringComparison.OrdinalIgnoreCase));
        var wrongShapeBlocker = Assert.Single(
            blockingPaths,
            item => item.Contains(ShiningFactionRequestState.PendingRealignmentsRequestPath, StringComparison.OrdinalIgnoreCase));
        Assert.Contains("missing requests[] array", wrongShapeBlocker, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("repair", wrongShapeBlocker, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("active Shining pending contract", wrongShapeBlocker, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("root full payload", wrongShapeBlocker, StringComparison.OrdinalIgnoreCase);
        Assert.True(_fs.FileExists(ShiningTradeRequestState.PendingRequestsPath));
        Assert.True(_fs.FileExists(ShiningFactionRequestState.PendingFoundingsRequestPath));
        Assert.True(_fs.FileExists(ShiningFactionRequestState.PendingRealignmentsRequestPath));
        Assert.True(_fs.FileExists(SourceOfLightCapstoneState.PendingRequestPath));
    }

    [Fact]
    public async Task NormalizeRuntimeUiArtifactsAsync_PreservesResolvedShiningPendingRequestDuringActiveSnapshot()
    {
        var requestRoot = new
        {
            requests = new[]
            {
                new
                {
                    requestId = "core_req_open_gates",
                    actionType = ShiningCoreActionRequestState.ActionTypeOpenGates,
                    factionId = "",
                    factionName = "",
                    projectId = "",
                    projectDisplayName = "",
                    radianceTierAtRequest = 1,
                    quotedCostFeathers = 0,
                    quotedCostLightSparks = 0,
                    sourceDraftVersion = 0,
                    selectedCardIds = Array.Empty<string>(),
                    createdAtTurn = 12,
                    createdAtUtc = "2026-04-30T00:00:00Z"
                }
            }
        };
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            currentRealm = "Shining Abode",
            currentIncarnation = 3,
            inkFeathers = new { current = 40 }
        });
        await WriteJsonAsync(ShiningAbodeState.StatePath, new
        {
            availability = ShiningAbodeState.AvailabilityActive,
            radiance = new { experience = 120, tier = 1 },
            lightSparks = 12,
            gates = new
            {
                draftVersion = 2,
                hasOpenDraft = true,
                isStale = false,
                allCandidateBlessingCards = Array.Empty<object>(),
                availableBlessingCards = Array.Empty<object>(),
                shownBlessingCardIds = Array.Empty<string>(),
                selectedBlessingCardIds = Array.Empty<string>(),
                nextCandidateCursor = 0,
                rerollsRemaining = 0
            },
            coreActionReceipts = new[]
            {
                new
                {
                    requestId = "core_req_open_gates",
                    actionType = ShiningCoreActionRequestState.ActionTypeOpenGates,
                    status = ShiningCoreActionRequestState.RequestStatusAccepted,
                    generatedDraftVersion = 2,
                    selectedCardIds = Array.Empty<string>(),
                    newResidentIds = Array.Empty<string>(),
                    seededProjectIds = Array.Empty<string>(),
                    quotedCostFeathers = 0,
                    quotedCostLightSparks = 0,
                    resolvedAtTurn = 12,
                    resolvedAtUtc = "2026-04-30T00:01:00Z",
                    reason = "gates_opened"
                }
            }
        });
        await WriteJsonAsync(ShiningCoreActionRequestState.PendingActionsRequestPath, requestRoot);
        await WriteJsonAsync(
            $"game_state/control/pending_turn_snapshot/{ShiningCoreActionRequestState.PendingActionsRequestPath}",
            requestRoot);
        await WritePendingTurnSnapshotManifestAsync(
            "test-session",
            "test-request",
            12,
            ShiningCoreActionRequestState.PendingActionsRequestPath);
        await WriteJsonAsync("input/turn_request.json", new
        {
            sessionId = "test-session",
            requestId = "test-request",
            turnNumber = 12
        });
        await WriteJsonAsync("ready/turn_complete.json", new
        {
            sessionId = "test-session",
            requestId = "test-request",
            turnNumber = 12,
            accepted = true
        });
        var engine = CreateGameEngine();

        await InvokePrivateTaskAsync(engine, "NormalizeRuntimeUiArtifactsAsync");

        Assert.True(_fs.FileExists(ShiningCoreActionRequestState.PendingActionsRequestPath));
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
        var soulRoot = JsonNode.Parse((await _fs.ReadFileAsync("game_state/meta/soul_state.json"))!)!.AsObject();
        var shiningRoot = JsonNode.Parse((await _fs.ReadFileAsync("game_state/meta/shining_abode_state.json"))!)!.AsObject();
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
        var soulRoot = JsonNode.Parse((await _fs.ReadFileAsync("game_state/meta/soul_state.json"))!)!.AsObject();
        var shiningRoot = JsonNode.Parse((await _fs.ReadFileAsync("game_state/meta/shining_abode_state.json"))!)!.AsObject();
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

    [Fact]
    public async Task IncarnationLocalPrepRollbackSnapshot_SurvivesCurrentWorldLoreClear()
    {
        const string worldSettingPath = "lore/current_world/world_setting.json";
        const string nestedLorePath = "lore/current_world/history/era.json";
        const string worldSettingJson = """{ "worldName": "Old World" }""";
        const string nestedLoreJson = """{ "era": "Before Gates" }""";

        await _fs.WriteFileAtomicAsync(worldSettingPath, worldSettingJson);
        await _fs.WriteFileAtomicAsync(nestedLorePath, nestedLoreJson);

        var engine = CreateGameEngine();
        var explorer = GetPrivateField<ExplorerMode>(engine, "_explorer");
        var rollbackFiles = InvokePrivateValue<string[]>(engine, "EnumerateIncarnationLocalPrepRollbackFiles");

        Assert.Contains(worldSettingPath, rollbackFiles);
        Assert.Contains(nestedLorePath, rollbackFiles);

        await explorer.StagePendingLocalTurnRollbackSnapshotAsync(rollbackFiles);
        var rollbackSnapshot = await InvokePrivateTaskResultAsync(engine, "CreatePreTurnBackup", "explorer_rollback_filter");
        var baselineFilesValue = rollbackSnapshot.GetType().GetProperty("BaselineFiles")?.GetValue(rollbackSnapshot);
        var baselineFiles = Assert.IsAssignableFrom<IEnumerable>(baselineFilesValue);
        Assert.DoesNotContain(
            baselineFiles.Cast<object>().Select(value => value?.ToString() ?? string.Empty),
            path => path.StartsWith("game_state/control/explorer_local_turn_rollback/", StringComparison.OrdinalIgnoreCase));

        _fs.ClearCurrentWorldLore();

        Assert.False(_fs.FileExists(worldSettingPath));
        Assert.False(_fs.FileExists(nestedLorePath));

        await explorer.RestoreStagedLocalTurnRollbackSnapshotAsync();

        Assert.Equal(worldSettingJson, await _fs.ReadFileAsync(worldSettingPath));
        Assert.Equal(nestedLoreJson, await _fs.ReadFileAsync(nestedLorePath));
        Assert.False(Directory.Exists(_fs.ResolvePath("game_state/control/explorer_local_turn_rollback")));
    }

    [Fact]
    public async Task IncarnationLocalPrepNewSetupFiles_AreSnapshottedButStillRollbackDeleted()
    {
        const string soulStatePath = "game_state/meta/soul_state.json";
        const string soulStateJson = """{ "soulName": "Тестовая Душа", "currentRealm": "Mortal World", "currentIncarnation": 1 }""";
        const string pendingSetupJson = """{ "mode": "manual", "worldDirectives": { "settingSummary": "New setup" } }""";
        const string scenarioCoreJson = """{ "scenarioCore": { "summary": "New scenario" } }""";

        await _fs.WriteFileAtomicAsync(soulStatePath, soulStateJson);

        var engine = CreateGameEngine();
        var explorer = GetPrivateField<ExplorerMode>(engine, "_explorer");
        var rollbackFiles = InvokePrivateValue<string[]>(engine, "EnumerateIncarnationLocalPrepRollbackFiles");

        Assert.Contains(WorldDirectiveService.PendingSetupPath, rollbackFiles);
        Assert.Contains(ScenarioCoreService.ManifestPath, rollbackFiles);

        await explorer.StagePendingLocalTurnRollbackSnapshotAsync(rollbackFiles);
        await _fs.WriteFileAtomicAsync(WorldDirectiveService.PendingSetupPath, pendingSetupJson);
        await _fs.WriteFileAtomicAsync(ScenarioCoreService.ManifestPath, scenarioCoreJson);
        explorer.MarkExistingPendingLocalTurnValidationSnapshotFiles(
            WorldDirectiveService.PendingSetupPath,
            ScenarioCoreService.ManifestPath);

        var rollbackSnapshot = await InvokePrivateTaskResultAsync(engine, "CreatePreTurnBackup", "incarnation_setup_validation_snapshot");
        var stagedSnapshot = explorer.ConsumePendingLocalTurnRollbackSnapshot();
        InvokePrivate(engine, "OverlayExplorerLocalRollbackSnapshot", rollbackSnapshot, stagedSnapshot);

        var request = new TurnRequest
        {
            SessionId = "session_incarnation_setup_snapshot",
            RequestId = "request_incarnation_setup_snapshot",
            TurnNumber = 42,
            PlayerAction = "incarnation setup snapshot test",
            Timestamp = "2026-03-24T00:00:00Z",
            ProgressionControl = new ProgressionControl { CurrentRealm = "Mortal World" }
        };
        await InvokePrivateTaskResultAsync(engine, "CreateCanonicalBaselineSnapshotAsync", request, rollbackSnapshot, "test");

        var manifestJson = await _fs.ReadFileAsync("game_state/control/pending_turn_snapshot.json");
        Assert.NotNull(manifestJson);
        var manifest = Assert.IsType<JsonObject>(JsonNode.Parse(manifestJson!)!);
        var files = Assert.IsType<JsonObject>(manifest["files"]);
        var rollbackBaselineFiles = Assert.IsType<JsonArray>(manifest["rollbackBaselineFiles"]);
        var rollbackBaselineSet = rollbackBaselineFiles
            .Select(node => node?.GetValue<string>() ?? string.Empty)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.True(files.ContainsKey(WorldDirectiveService.PendingSetupPath));
        Assert.True(files.ContainsKey(ScenarioCoreService.ManifestPath));
        Assert.DoesNotContain(WorldDirectiveService.PendingSetupPath, rollbackBaselineSet);
        Assert.DoesNotContain(ScenarioCoreService.ManifestPath, rollbackBaselineSet);

        await InvokePrivateTaskAsync(engine, "RestorePreTurnBackup", rollbackSnapshot);

        Assert.False(_fs.FileExists(WorldDirectiveService.PendingSetupPath));
        Assert.False(_fs.FileExists(ScenarioCoreService.ManifestPath));
    }

    [Fact]
    public async Task CreateCanonicalBaselineSnapshotAsync_AbsentSourceOfLightPending_IsNotRollbackBaseline()
    {
        var engine = CreateGameEngine();
        var request = new TurnRequest
        {
            SessionId = "session_no_source_pending_snapshot",
            RequestId = "request_no_source_pending_snapshot",
            TurnNumber = 42,
            PlayerAction = "ordinary turn without Source pending",
            Timestamp = "2026-03-24T00:00:00Z",
            ProgressionControl = new ProgressionControl { CurrentRealm = "Chaos Sea" }
        };

        await InvokePrivateTaskResultAsync(engine, "CreateCanonicalBaselineSnapshotAsync", request, null, "test");

        var manifestJson = await _fs.ReadFileAsync("game_state/control/pending_turn_snapshot.json");
        Assert.NotNull(manifestJson);
        var manifest = Assert.IsType<JsonObject>(JsonNode.Parse(manifestJson!)!);
        var files = Assert.IsType<JsonObject>(manifest["files"]);
        var rollbackBaselineFiles = Assert.IsType<JsonArray>(manifest["rollbackBaselineFiles"]);
        var rollbackBaselineSet = rollbackBaselineFiles
            .Select(node => node?.GetValue<string>() ?? string.Empty)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.False(files.ContainsKey(SourceOfLightCapstoneState.PendingRequestPath));
        Assert.DoesNotContain(SourceOfLightCapstoneState.PendingRequestPath, rollbackBaselineSet);
    }

    [Fact]
    public async Task CreateCanonicalBaselineSnapshotAsync_AbsentAfterlifePendingContracts_AreNotRollbackBaseline()
    {
        var engine = CreateGameEngine();
        var request = new TurnRequest
        {
            SessionId = "session_no_afterlife_pending_snapshot",
            RequestId = "request_no_afterlife_pending_snapshot",
            TurnNumber = 42,
            PlayerAction = "ordinary turn without afterlife pending contracts",
            Timestamp = "2026-03-24T00:00:00Z",
            ProgressionControl = new ProgressionControl { CurrentRealm = "Chaos Sea" }
        };

        await InvokePrivateTaskResultAsync(engine, "CreateCanonicalBaselineSnapshotAsync", request, null, "test");

        var manifestJson = await _fs.ReadFileAsync("game_state/control/pending_turn_snapshot.json");
        Assert.NotNull(manifestJson);
        var manifest = Assert.IsType<JsonObject>(JsonNode.Parse(manifestJson!)!);
        var files = Assert.IsType<JsonObject>(manifest["files"]);
        var rollbackBaselineFiles = Assert.IsType<JsonArray>(manifest["rollbackBaselineFiles"]);
        var rollbackBaselineSet = rollbackBaselineFiles
            .Select(node => node?.GetValue<string>() ?? string.Empty)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var optionalPendingPaths = new[]
        {
            GuardianAbodeOfferingState.PendingRequestPath,
            GuardianTradeRequestState.PendingRequestPath,
            NpcTradeRequestState.PendingRequestPath,
            ShiningCoreActionRequestState.PendingActionsRequestPath,
            ShiningTradeRequestState.PendingRequestsPath,
            GuardianAbodeResidentRequestState.PendingResidentsRequestPath,
            GuardianAbodeResidentRequestState.PendingInteractionsRequestPath,
            GuardianAbodeResidentRequestState.PendingTransfersRequestPath,
            ActorSocialInteractionRequestState.PendingGuardianRequestPath,
            ActorSocialInteractionRequestState.PendingNpcRequestPath,
            AfterlifeArchiveActionState.ConsultationRequestPath,
            AfterlifeArchiveActionState.ProjectFuelRequestPath
        };

        foreach (var pendingPath in optionalPendingPaths)
        {
            Assert.False(files.ContainsKey(pendingPath), $"{pendingPath} should not have a snapshot entry when absent.");
            Assert.DoesNotContain(pendingPath, rollbackBaselineSet);
        }
    }

    [Fact]
    public async Task CreateCanonicalBaselineSnapshotAsync_PresentSourceOfLightPending_IsRollbackBaseline()
    {
        var sourceRequest = SourceOfLightCapstoneState.CreateRequest(42, 580, 4);
        await SourceOfLightCapstoneState.WriteRequestAsync(_fs, sourceRequest);
        var engine = CreateGameEngine();
        var request = new TurnRequest
        {
            SessionId = "session_source_pending_snapshot",
            RequestId = "request_source_pending_snapshot",
            TurnNumber = 42,
            PlayerAction = "Source pending snapshot",
            Timestamp = "2026-03-24T00:00:00Z",
            ProgressionControl = new ProgressionControl { CurrentRealm = "Shining Abode" }
        };

        await InvokePrivateTaskResultAsync(engine, "CreateCanonicalBaselineSnapshotAsync", request, null, "test");

        var manifestJson = await _fs.ReadFileAsync("game_state/control/pending_turn_snapshot.json");
        Assert.NotNull(manifestJson);
        var manifest = Assert.IsType<JsonObject>(JsonNode.Parse(manifestJson!)!);
        var files = Assert.IsType<JsonObject>(manifest["files"]);
        var rollbackBaselineFiles = Assert.IsType<JsonArray>(manifest["rollbackBaselineFiles"]);
        var rollbackBaselineSet = rollbackBaselineFiles
            .Select(node => node?.GetValue<string>() ?? string.Empty)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.True(files.ContainsKey(SourceOfLightCapstoneState.PendingRequestPath));
        Assert.Contains(SourceOfLightCapstoneState.PendingRequestPath, rollbackBaselineSet);
    }

    [Fact]
    public async Task ValidatedRollbackSnapshot_PreservesExplorerLocalTurnRollbackBackups()
    {
        const string sessionId = "session_explorer_rollback_restart_001";
        const string requestId = "request_explorer_rollback_restart_001";
        const int turnNumber = 77;
        const string soulStatePath = "game_state/meta/soul_state.json";
        const string soulSnapshotPath = "game_state/control/pending_turn_snapshot/game_state/meta/soul_state.json";
        const string trackedPath = "lore/current_world/world_setting.json";
        const string snapshotPath = "game_state/control/pending_turn_snapshot/lore/current_world/world_setting.json";
        const string backupPath = "game_state/control/explorer_local_turn_rollback/restart/world_setting.json.rollback.001";
        const string soulStateJson = """{ "soulName": "Тестовая Душа", "currentRealm": "Mortal World", "currentIncarnation": 1 }""";
        const string worldSettingJson = """{ "worldName": "Old World" }""";

        await _fs.WriteFileAtomicAsync(soulStatePath, soulStateJson);
        await _fs.WriteFileAtomicAsync(soulSnapshotPath, soulStateJson);
        await _fs.WriteFileAtomicAsync(trackedPath, worldSettingJson);
        await _fs.WriteFileAtomicAsync(snapshotPath, worldSettingJson);
        await _fs.WriteFileAtomicAsync(backupPath, worldSettingJson);
        await WriteJsonAsync("input/turn_request.json", new
        {
            sessionId,
            requestId,
            turnNumber
        });

        var manifest = new PendingTurnSnapshotManifestPayload
        {
            SessionId = sessionId,
            RequestId = requestId,
            TurnNumber = turnNumber,
            RequestTimestamp = "2026-03-24T00:00:00Z",
            PlayerAction = "restart rollback test",
            ProgressionControl = new ProgressionControl { CurrentRealm = "Mortal World" },
            Files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [soulStatePath] = soulSnapshotPath,
                [trackedPath] = snapshotPath
            },
            SnapshotFileHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [soulStatePath] = ComputeSha256(soulStateJson),
                [trackedPath] = ComputeSha256(worldSettingJson)
            },
            ClientOwnedValidationHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            RollbackBackups = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [trackedPath] = backupPath
            },
            RollbackBaselineFiles = new List<string> { soulStatePath, trackedPath },
            SourceLabel = "game-engine-turn-lifecycle-tests"
        };
        manifest.ManifestPayloadHash = ComputeManifestPayloadHash(manifest);

        await _fs.WriteFileAtomicAsync(
            "game_state/control/pending_turn_snapshot.json",
            JsonSerializer.Serialize(manifest, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        await PendingTurnSnapshotTestAuthority.SyncAuthorityForCurrentManifestAsync(_fs);

        _fs.DeleteFile(trackedPath);
        var engine = CreateGameEngine();
        var loadedManifest = await InvokePrivateTaskResultAsync(engine, "LoadPendingTurnSnapshotManifestAsync");
        var rollbackSnapshot = await InvokePrivateTaskResultAsync(engine, "GetValidatedRollbackSnapshotAsync", loadedManifest);
        var backupFilesValue = rollbackSnapshot.GetType().GetProperty("BackupFiles")?.GetValue(rollbackSnapshot);
        var backupFiles = Assert.IsAssignableFrom<Dictionary<string, string>>(backupFilesValue);

        Assert.True(backupFiles.TryGetValue(trackedPath, out var restoredBackupPath));
        Assert.Equal(backupPath, restoredBackupPath);

        await InvokePrivateTaskAsync(engine, "RestorePreTurnBackup", rollbackSnapshot);

        Assert.Equal(worldSettingJson, await _fs.ReadFileAsync(trackedPath));
    }

    [Fact]
    public async Task ProcessPlayerTurn_StagingFailureRestoresConsumedIncarnationLocalPrepRollback()
    {
        const string worldSettingPath = "lore/current_world/world_setting.json";
        const string worldSettingJson = """{ "worldName": "Old World" }""";
        const string pendingSetupJson = """{ "mode": "manual", "worldDirectives": { "settingSummary": "Old setup" } }""";
        const string scenarioCoreJson = """{ "scenarioCore": { "summary": "Old scenario" } }""";

        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Тестовая Душа",
            currentRealm = "Mortal World",
            currentIncarnation = 1,
            inkFeathers = new { current = 50 }
        });
        await _fs.WriteFileAtomicAsync(worldSettingPath, worldSettingJson);
        await _fs.WriteFileAtomicAsync(WorldDirectiveService.PendingSetupPath, pendingSetupJson);
        await _fs.WriteFileAtomicAsync(ScenarioCoreService.ManifestPath, scenarioCoreJson);

        var engine = CreateGameEngine();
        var explorer = GetPrivateField<ExplorerMode>(engine, "_explorer");
        var rollbackFiles = InvokePrivateValue<string[]>(engine, "EnumerateIncarnationLocalPrepRollbackFiles");
        await explorer.StagePendingLocalTurnRollbackSnapshotAsync(rollbackFiles);

        _fs.ClearCurrentWorldLore();
        await _fs.WriteFileAtomicAsync(WorldDirectiveService.PendingSetupPath, """{ "mode": "manual", "worldDirectives": { "settingSummary": "Changed setup" } }""");
        await _fs.WriteFileAtomicAsync(ScenarioCoreService.ManifestPath, """{ "scenarioCore": { "summary": "Changed scenario" } }""");
        Directory.CreateDirectory(_fs.ResolvePath("input/turn_request.json"));

        await Assert.ThrowsAnyAsync<Exception>(() =>
            InvokePrivateTaskAsync(engine, "ProcessPlayerTurn", "Тестовый ход", null));

        Assert.Equal(worldSettingJson, await _fs.ReadFileAsync(worldSettingPath));
        Assert.Equal(pendingSetupJson, await _fs.ReadFileAsync(WorldDirectiveService.PendingSetupPath));
        Assert.Equal(scenarioCoreJson, await _fs.ReadFileAsync(ScenarioCoreService.ManifestPath));
        Assert.False(_fs.FileExists("input/turn_request.json"));
        Assert.False(_fs.FileExists("game_state/control/pending_turn_snapshot.json"));
        Assert.False(Directory.Exists(_fs.ResolvePath("game_state/control/explorer_local_turn_rollback")));
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
        await WritePendingTurnSnapshotManifestAsync(sessionId, requestId, turnNumber, gachaBaseResult: null, trackedPaths);
    }

    private async Task WritePendingTurnSnapshotManifestAsync(
        string sessionId,
        string requestId,
        int turnNumber,
        JsonObject? gachaBaseResult,
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
            GachaBaseResult = gachaBaseResult,
            ProgressionControl = new ProgressionControl { CurrentRealm = "Mortal World" },
            Files = files,
            SnapshotFileHashes = snapshotHashes,
            ClientOwnedValidationHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            RollbackBackups = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            RollbackBaselineFiles = trackedPaths.ToList(),
            SourceLabel = "game-engine-turn-lifecycle-tests"
        };
        manifest.ManifestPayloadHash = ComputeManifestPayloadHash(manifest);

        await _fs.WriteFileAtomicAsync(
            "game_state/control/pending_turn_snapshot.json",
            JsonSerializer.Serialize(manifest, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
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

    private static async Task InvokePrivateTaskAsync(object instance, string methodName, params object?[]? args)
    {
        var method = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var task = method!.Invoke(instance, args) as Task;
        Assert.NotNull(task);
        await task!;
    }

    private static void InvokePrivate(object instance, string methodName, params object?[]? args)
    {
        var method = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(instance, args);
    }

    private static T InvokePrivateValue<T>(object instance, string methodName, params object?[]? args)
    {
        var method = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var value = method!.Invoke(instance, args);
        Assert.IsType<T>(value);
        return (T)value!;
    }

    private static async Task<object> InvokePrivateTaskResultAsync(object instance, string methodName, params object?[]? args)
    {
        var method = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var task = method!.Invoke(instance, args) as Task;
        Assert.NotNull(task);
        await task!;
        var resultProperty = task.GetType().GetProperty("Result");
        Assert.NotNull(resultProperty);
        var result = resultProperty!.GetValue(task);
        Assert.NotNull(result);
        return result!;
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
