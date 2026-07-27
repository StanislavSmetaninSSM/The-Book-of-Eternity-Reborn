using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.WebUi;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests.WebUi;

public sealed class BrowserAfterlifeWriteServiceTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly StateManager _stateManager;
    private readonly BrowserAfterlifeWriteService _service;

    public BrowserAfterlifeWriteServiceTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-afterlife-write-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
        _stateManager = new StateManager(_fs, new GameSettings(), NullLogger<StateManager>.Instance);
        var lockService = new LocalUiSessionLockService(_fs);
        var coordinator = new BrowserLocalWriteCoordinator(_fs, lockService, TimeProvider.System);
        _service = new BrowserAfterlifeWriteService(_fs, _stateManager, coordinator);
    }

    [Fact]
    public async Task TryApplyAsync_GachaDirectPull_DeductsFeathersAndReturnsGmPayload()
    {
        await SeedSoulStateAsync(stored: Array.Empty<(string, string)>(), equipped: Array.Empty<(string, string, string)>(), inkFeathers: 18);
        await SeedPendingGachaBaseAsync("Rare", 72, [18, 18, 18, 18]);

        var result = await _service.TryApplyAsync(
            "/gacha",
            Answers(("gacha_banner", "direct_chaos_sea"), ("feather_cost", 7), ("confirm_gacha_pull", true)),
            Owner("browser-test"));

        Assert.True(result.Success, result.Message);
        Assert.Equal(CommandExecutionState.Completed, result.State);
        var soul = await ReadSoulAsync();
        Assert.Equal(11, soul["inkFeathers"]!["current"]!.GetValue<int>());
        Assert.Equal("gacha_browser_write", result.Payload!["sourceSurface"]!.GetValue<string>());
        Assert.Equal("direct_chaos_sea", result.Payload!["banner"]!.GetValue<string>());
        Assert.Equal(7, result.Payload!["spentInkFeathers"]!.GetValue<int>());
        Assert.Equal(11, result.Payload!["remainingInkFeathers"]!.GetValue<int>());
        Assert.Equal("Rare", result.Payload!["gachaBaseResult"]!["baseRarity"]!.GetValue<string>());
        Assert.Contains("7 Чернильных Перьев", result.Payload!["gmAction"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Contains("[CHAOS_SEA_DIRECT_GACHA]", result.Payload!["gmAction"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.False(_fs.FileExists(LocalUiSessionLockService.LockPath));
    }

    [Fact]
    public async Task TryApplyAsync_GachaDirectPull_StagesPreSpendExplorerRollbackEvidence()
    {
        await SeedSoulStateAsync(stored: Array.Empty<(string, string)>(), equipped: Array.Empty<(string, string, string)>(), inkFeathers: 18);
        await SeedPendingGachaBaseAsync("Rare", 72, [18, 18, 18, 18]);
        var beforeSoul = await _fs.ReadFileAsync("game_state/meta/soul_state.json");

        var result = await _service.TryApplyAsync(
            "/gacha",
            Answers(("gacha_banner", "direct_chaos_sea"), ("feather_cost", 7), ("confirm_gacha_pull", true)),
            Owner("browser-test"));

        Assert.True(result.Success, result.Message);
        var rollbackRoot = _fs.ResolvePath("game_state/control/explorer_local_turn_rollback");
        Assert.True(Directory.Exists(rollbackRoot));
        var rollbackPath = Assert.Single(Directory.GetFiles(
            rollbackRoot,
            "*soul_state.json.rollback.*",
            SearchOption.AllDirectories));
        var rollbackJson = await File.ReadAllTextAsync(rollbackPath);
        Assert.Equal(beforeSoul, rollbackJson);
        var rollbackSoul = JsonNode.Parse(rollbackJson)!.AsObject();
        Assert.Equal(18, rollbackSoul["inkFeathers"]!["current"]!.GetValue<int>());
    }

    [Theory]
    [InlineData("Shining Abode")]
    [InlineData("Mortal World")]
    [InlineData("chaosSea")]
    public async Task TryApplyAsync_GachaDirectPull_RejectsNonOrdinaryChaosSeaRealmBeforeSpend(string currentRealm)
    {
        await SeedSoulStateAsync(
            stored: Array.Empty<(string, string)>(),
            equipped: Array.Empty<(string, string, string)>(),
            inkFeathers: 18,
            currentRealm: currentRealm);
        var beforeSoul = await _fs.ReadFileAsync("game_state/meta/soul_state.json");

        var result = await _service.TryApplyAsync(
            "/gacha",
            Answers(("gacha_banner", "direct_chaos_sea"), ("feather_cost", 7), ("confirm_gacha_pull", true)),
            Owner("browser-test"));

        Assert.False(result.Success);
        Assert.Contains("Море Хаоса", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(beforeSoul, await _fs.ReadFileAsync("game_state/meta/soul_state.json"));
    }

    [Fact]
    public async Task TryApplyAsync_GachaDirectPull_RejectsUnsupportedCommandArgumentsBeforeSpend()
    {
        await SeedSoulStateAsync(stored: Array.Empty<(string, string)>(), equipped: Array.Empty<(string, string, string)>(), inkFeathers: 18);
        var beforeSoul = await _fs.ReadFileAsync("game_state/meta/soul_state.json");

        var result = await _service.TryApplyAsync(
            "/gacha guardian_pull",
            Answers(("gacha_banner", "direct_chaos_sea"), ("feather_cost", 7), ("confirm_gacha_pull", true)),
            Owner("browser-test"));

        Assert.False(result.Success);
        Assert.Contains("аргумент", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(beforeSoul, await _fs.ReadFileAsync("game_state/meta/soul_state.json"));
    }

    [Fact]
    public async Task TryApplyAsync_GachaDirectPull_RequiresConfirmation()
    {
        await SeedSoulStateAsync(stored: Array.Empty<(string, string)>(), equipped: Array.Empty<(string, string, string)>(), inkFeathers: 18);
        var beforeSoul = await _fs.ReadFileAsync("game_state/meta/soul_state.json");

        var result = await _service.TryApplyAsync(
            "/gacha",
            Answers(("gacha_banner", "direct_chaos_sea"), ("feather_cost", 7), ("confirm_gacha_pull", false)),
            Owner("browser-test"));

        Assert.False(result.Success);
        Assert.True(result.KeepSessionOpen);
        Assert.Contains("Подтвердите", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(beforeSoul, await _fs.ReadFileAsync("game_state/meta/soul_state.json"));
    }

    [Theory]
    [InlineData("guardian_azalia", 7, "поддерживает только прямой призыв")]
    [InlineData("direct_chaos_sea", 0, "положительным")]
    [InlineData("direct_chaos_sea", 25, "Недостаточно")]
    public async Task TryApplyAsync_GachaDirectPull_ValidatesBannerCostAndBalance(string banner, int cost, string expectedMessage)
    {
        await SeedSoulStateAsync(stored: Array.Empty<(string, string)>(), equipped: Array.Empty<(string, string, string)>(), inkFeathers: 18);
        var beforeSoul = await _fs.ReadFileAsync("game_state/meta/soul_state.json");

        var result = await _service.TryApplyAsync(
            "/gacha",
            Answers(("gacha_banner", banner), ("feather_cost", cost), ("confirm_gacha_pull", true)),
            Owner("browser-test"));

        Assert.False(result.Success);
        Assert.Contains(expectedMessage, result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(beforeSoul, await _fs.ReadFileAsync("game_state/meta/soul_state.json"));
    }

    [Fact]
    public async Task TryApplyAsync_GachaDirectPull_RollsBackSoulOnMalformedState()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", "{ invalid json");
        var beforeSoul = await _fs.ReadFileAsync("game_state/meta/soul_state.json");

        var result = await _service.TryApplyAsync(
            "/gacha",
            Answers(("gacha_banner", "direct_chaos_sea"), ("feather_cost", 1), ("confirm_gacha_pull", true)),
            Owner("browser-test"));

        Assert.False(result.Success);
        Assert.Equal(beforeSoul, await _fs.ReadFileAsync("game_state/meta/soul_state.json"));
    }

    [Fact]
    public async Task TryApplyAsync_GachaDirectPull_QueueFailureRestoresCanonicalProfileAndRuntimeState()
    {
        await SeedSoulStateAsync(
            stored: Array.Empty<(string, string)>(),
            equipped: Array.Empty<(string, string, string)>(),
            inkFeathers: 18);
        await SeedPendingGachaBaseAsync("Rare", 72, [18, 18, 18, 18]);
        await SeedStalePlayerSoulProfileAsync();
        await _stateManager.RefreshGameStateAsync();

        var beforeSoul = await _fs.ReadFileBytesAsync("game_state/meta/soul_state.json");
        var beforeProfile = await _fs.ReadFileBytesAsync(AfterlifeEntityProfileState.StatePath);
        var beforeDice = await _fs.ReadFileBytesAsync(PendingTurnStateService.PendingDiceStatePath);
        Assert.Equal(18, _stateManager.CurrentState.InkFeathers);

        Directory.CreateDirectory(_fs.ResolvePath(BrowserPendingTurnInspector.TurnRequestPath));

        var result = await _service.TryApplyAsync(
            "/gacha",
            Answers(("gacha_banner", "direct_chaos_sea"), ("feather_cost", 7), ("confirm_gacha_pull", true)),
            Owner("browser-test"));

        Assert.False(result.Success);
        Assert.Equal(beforeSoul, await _fs.ReadFileBytesAsync("game_state/meta/soul_state.json"));
        Assert.Equal(beforeProfile, await _fs.ReadFileBytesAsync(AfterlifeEntityProfileState.StatePath));
        Assert.Equal(beforeDice, await _fs.ReadFileBytesAsync(PendingTurnStateService.PendingDiceStatePath));
        Assert.Equal(18, _stateManager.CurrentState.InkFeathers);
        Assert.False(_fs.FileExists(BrowserPendingTurnInspector.PendingTurnSnapshotManifestPath));
        Assert.False(_fs.FileExists(PendingTurnSnapshotAuthority.AuthorityPath));
        Assert.False(_fs.FileExists(LocalUiSessionLockService.LockPath));
        Assert.False(Directory.Exists(_fs.ResolvePath(ExplorerLocalTurnRollbackArtifacts.Root)));
    }

    [Fact]
    public async Task TryApplyAsync_GachaDirectPull_ConcurrentNewGameWaitsThenReceivesNoOldArtifacts()
    {
        var concurrentRoot = Path.Combine(_rootPath, "concurrent-new-game");
        Directory.CreateDirectory(concurrentRoot);
        var profileInputsRead = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var allowProfileRefresh = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var replacementContended = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var fs = new FileSystemManager(
            concurrentRoot,
            NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance,
            new FileSystemManagerHooks
            {
                CanonicalWriteLockContendedAsync = () =>
                {
                    replacementContended.TrySetResult();
                    return Task.CompletedTask;
                }
            });
        fs.EnsureDirectoryStructure();
        var stateManager = new StateManager(
            fs,
            new GameSettings(),
            NullLogger<StateManager>.Instance,
            new StateManagerHooks
            {
                AfterPlayerSoulProfileInputsReadAsync = async () =>
                {
                    profileInputsRead.TrySetResult();
                    await allowProfileRefresh.Task;
                }
            });
        var coordinator = new BrowserLocalWriteCoordinator(
            fs,
            new LocalUiSessionLockService(fs),
            TimeProvider.System);
        var service = new BrowserAfterlifeWriteService(fs, stateManager, coordinator);

        await fs.WriteFileAtomicAsync(
            "game_state/meta/soul_state.json",
            new JsonObject
            {
                ["soulName"] = "Тестовая душа",
                ["currentRealm"] = "Chaos Sea",
                ["currentIncarnation"] = 4,
                ["inkFeathers"] = new JsonObject
                {
                    ["current"] = 18,
                    ["total"] = 18
                },
                ["soulRelics"] = new JsonObject
                {
                    ["stored"] = new JsonArray(),
                    ["equipped"] = new JsonArray()
                }
            }.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        await fs.WriteFileAtomicAsync(
            PendingTurnStateService.PendingDiceStatePath,
            new JsonObject
            {
                ["preGeneratedDices1d20"] = new JsonArray(1, 2, 3, 4),
                ["gachaBaseResult"] = new JsonObject
                {
                    ["diceUsed"] = new JsonArray(18, 18, 18, 18),
                    ["baseScore"] = 72,
                    ["baseRarity"] = "Rare",
                    ["formula"] = "client-computed gacha base (range 4-80)"
                },
                ["isFateLocked"] = false
            }.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        await fs.WriteFileAtomicAsync(
            AfterlifeEntityProfileState.StatePath,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["profiles"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["actorType"] = "player_soul",
                        ["actorId"] = "player_soul",
                        ["displayName"] = "Тестовая душа",
                        ["realm"] = "Chaos Sea",
                        ["currencies"] = new JsonObject
                        {
                            ["inkFeathers"] = 0,
                            ["lightSparks"] = 0
                        },
                        ["progression"] = new JsonObject(),
                        ["standardArts"] = new JsonObject(),
                        ["specialArts"] = new JsonArray(),
                        ["customStates"] = new JsonArray(),
                        ["soulDissipationTier"] = 0,
                        ["ledger"] = new JsonArray()
                    }
                }
            }.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));

        var gacha = service.TryApplyAsync(
            "/gacha",
            Answers(
                ("gacha_banner", "direct_chaos_sea"),
                ("feather_cost", 7),
                ("confirm_gacha_pull", true)),
            Owner("browser-concurrent-gacha"));

        await profileInputsRead.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var replacement = fs.ClearGameStateAsync();
        await replacementContended.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(replacement.IsCompleted);

        allowProfileRefresh.TrySetResult();
        var result = await gacha.WaitAsync(TimeSpan.FromSeconds(5));
        await replacement.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(result.Success, result.Message);
        Assert.False(fs.FileExists("game_state/meta/soul_state.json"));
        Assert.False(fs.FileExists(AfterlifeEntityProfileState.StatePath));
        Assert.False(fs.FileExists(BrowserPendingTurnInspector.TurnRequestPath));
        Assert.False(fs.FileExists(BrowserPendingTurnInspector.PendingTurnSnapshotManifestPath));
        Assert.False(fs.FileExists(PendingTurnSnapshotAuthority.AuthorityPath));
        var rollbackRoot = fs.ResolvePath(ExplorerLocalTurnRollbackArtifacts.Root);
        Assert.Empty(
            Directory.Exists(rollbackRoot)
                ? Directory.GetFiles(rollbackRoot, "*", SearchOption.AllDirectories)
                : Array.Empty<string>());
    }

    [Fact]
    public async Task TryApplyAsync_GuardianSocial_RevalidatesAuthorityAfterCanonicalContention()
    {
        var concurrentRoot = Path.Combine(_rootPath, "guardian-social-contention");
        Directory.CreateDirectory(concurrentRoot);
        var writeContended = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var fs = new FileSystemManager(
            concurrentRoot,
            NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance,
            new FileSystemManagerHooks
            {
                CanonicalWriteLockContendedAsync = () =>
                {
                    writeContended.TrySetResult();
                    return Task.CompletedTask;
                }
            });
        fs.EnsureDirectoryStructure();
        var stateManager = new StateManager(
            fs,
            new GameSettings(),
            NullLogger<StateManager>.Instance);
        var coordinator = new BrowserLocalWriteCoordinator(
            fs,
            new LocalUiSessionLockService(fs),
            TimeProvider.System);
        var service = new BrowserAfterlifeWriteService(fs, stateManager, coordinator);

        await fs.WriteFileAtomicAsync(
            "game_state/meta/soul_state.json",
            """
            {
              "soulName": "Тестовая душа",
              "currentRealm": "Chaos Sea",
              "currentIncarnation": 3
            }
            """);
        await fs.WriteFileAtomicAsync(
            "game_state/meta/guardians.json",
            """
            {
              "guardians": [
                {
                  "guardianId": "guardian_old",
                  "canonicalName": "Старый Хранитель",
                  "abode": {
                    "abodeId": "abode_old",
                    "name": "Старая обитель"
                  }
                }
              ],
              "activeGuardian": {
                "guardianId": "guardian_old",
                "canonicalName": "Старый Хранитель",
                "abode": {
                  "abodeId": "abode_old",
                  "name": "Старая обитель"
                }
              },
              "chaosSeaNavigation": {
                "currentAbodeId": "abode_old",
                "currentGuardianId": "guardian_old"
              }
            }
            """);

        string generation;
        await using (var setupLease = await fs.AcquireCanonicalWriteLeaseAsync())
            generation = fs.GetOrCreateSessionGeneration(setupLease);

        var blockingLease = await fs.AcquireCanonicalWriteLeaseAsync();
        try
        {
            var write = SessionOperationContext.RunBoundAsync(
                fs,
                generation,
                () => service.TryApplyAsync(
                    "/guardian_social guardian_old",
                    Answers(
                        ("guardian_id", "guardian_old"),
                        ("guardian_interaction_type", ActorSocialInteractionRequestState.GuardianInteractionTypeTalk)),
                    Owner("browser-guardian-contention")));

            await writeContended.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await fs.WriteFileAtomicAsync(
                blockingLease,
                "game_state/meta/guardians.json",
                """
                {
                  "guardians": [
                    {
                      "guardianId": "guardian_new",
                      "canonicalName": "Новый Хранитель",
                      "abode": {
                        "abodeId": "abode_new",
                        "name": "Новая обитель"
                      }
                    }
                  ],
                  "activeGuardian": {
                    "guardianId": "guardian_new",
                    "canonicalName": "Новый Хранитель",
                    "abode": {
                      "abodeId": "abode_new",
                      "name": "Новая обитель"
                    }
                  },
                  "chaosSeaNavigation": {
                    "currentAbodeId": "abode_new",
                    "currentGuardianId": "guardian_new"
                  }
                }
                """);
            await blockingLease.DisposeAsync();

            var result = await write.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.False(result.Success);
            Assert.Contains("Хранител", result.Message, StringComparison.OrdinalIgnoreCase);
            Assert.False(fs.FileExists(ActorSocialInteractionRequestState.PendingGuardianRequestPath));
        }
        finally
        {
            await blockingLease.DisposeAsync();
        }
    }

    [Fact]
    public async Task TryApplyAsync_ShiningFactionFounding_InvalidFormDoesNotRefreshCanonicalProfile()
    {
        await SeedSoulStateAsync(
            stored: Array.Empty<(string, string)>(),
            equipped: Array.Empty<(string, string, string)>(),
            currentRealm: "Shining Abode");
        await SeedStalePlayerSoulProfileAsync();
        var beforeProfile = await _fs.ReadFileBytesAsync(AfterlifeEntityProfileState.StatePath);

        var result = await _service.TryApplyAsync(
            "/shining_faction_founding",
            Answers(("confirm_shining_politics_write", true)),
            Owner("browser-test"));

        Assert.False(result.Success);
        Assert.Contains("Заполните", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(beforeProfile, await _fs.ReadFileBytesAsync(AfterlifeEntityProfileState.StatePath));
    }

    [Fact]
    public async Task TryApplyAsync_ShiningRelicForge_UnavailableStateDoesNotRefreshCanonicalProfile()
    {
        await SeedSoulStateAsync(
            stored: Array.Empty<(string, string)>(),
            equipped: Array.Empty<(string, string, string)>(),
            currentRealm: "Shining Abode");
        await SeedStalePlayerSoulProfileAsync();
        var beforeProfile = await _fs.ReadFileBytesAsync(AfterlifeEntityProfileState.StatePath);

        var result = await _service.TryApplyAsync(
            "/shining_relic_forge",
            Answers(
                ("confirm_shining_relic_forge_write", true),
                ("faction_id", "test-faction"),
                ("forge_action_type", ShiningCoreActionRequestState.ActionTypeForgeRelicUpliftRarity),
                ("relic_id", "test-relic")),
            Owner("browser-test"));

        Assert.False(result.Success);
        Assert.Contains("Сияющей Обители", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(beforeProfile, await _fs.ReadFileBytesAsync(AfterlifeEntityProfileState.StatePath));
    }

    [Theory]
    [InlineData(
        "/guardian_trade guardian_lock_target",
        "guardian_trade_choice",
        "request:guardian_lock_target",
        "Chaos Sea")]
    [InlineData(
        "/shining_trade faction_lock_target",
        "shining_trade_choice",
        "request:faction_lock_target",
        "Shining Abode")]
    public async Task TryApplyAsync_BlockedAfterlifeTrade_DoesNotRefreshCanonicalProfile(
        string command,
        string choiceAnswerId,
        string choice,
        string currentRealm)
    {
        await SeedSoulStateAsync(
            stored: Array.Empty<(string, string)>(),
            equipped: Array.Empty<(string, string, string)>(),
            currentRealm: currentRealm);
        await SeedStalePlayerSoulProfileAsync();
        var beforeProfile = await _fs.ReadFileBytesAsync(AfterlifeEntityProfileState.StatePath);
        var lockService = new LocalUiSessionLockService(_fs);
        var blockingOwner = Owner("blocking-browser-owner");
        var lockResult = await lockService.AcquireOrRefreshAsync(blockingOwner, "Другая локальная операция");
        Assert.True(lockResult.Acquired, lockResult.BlockerMessage);

        try
        {
            var result = await _service.TryApplyAsync(
                command,
                Answers((choiceAnswerId, choice), ("confirm_trade_write", true)),
                Owner("blocked-browser-owner"));

            Assert.False(result.Success);
            Assert.Equal(CommandExecutionState.Blocked, result.State);
            Assert.Equal(
                beforeProfile,
                await _fs.ReadFileBytesAsync(AfterlifeEntityProfileState.StatePath));
        }
        finally
        {
            await lockService.ReleaseAsync(blockingOwner);
        }
    }

    [Fact]
    public async Task TryApplyAsync_SoulRelicEquip_MovesRelicFromStoredToEquipped()
    {
        await SeedSoulStateAsync(stored: new[] { ("r1", "Кулон Тишины") }, equipped: Array.Empty<(string, string, string)>());

        var result = await _service.TryApplyAsync(
            "/soul_relic_equip",
            Answers(("soul_relic_identity", "r1"), ("soul_relic_slot", "head"), ("confirm_soul_relic_write", true)),
            Owner("browser-test"));

        Assert.True(result.Success, result.Message);
        var soul = JsonNode.Parse((await _fs.ReadFileAsync("game_state/meta/soul_state.json"))!)!.AsObject();
        var stored = soul["soulRelics"]!["stored"]!.AsArray();
        var equipped = soul["soulRelics"]!["equipped"]!.AsArray();
        Assert.Empty(stored);
        Assert.Single(equipped);
        Assert.Equal("r1", equipped[0]!["relicId"]!.GetValue<string>());
        Assert.Equal("head", equipped[0]!["gameplayStatus"]!["currentSlot"]!.GetValue<string>());
    }

    [Fact]
    public async Task TryApplyAsync_SoulRelicEquip_ResolvesByName()
    {
        await SeedSoulStateAsync(stored: new[] { ("r1", "Кулон Тишины") }, equipped: Array.Empty<(string, string, string)>());

        var result = await _service.TryApplyAsync(
            "/soul_relic_equip",
            Answers(("soul_relic_identity", "Кулон Тишины"), ("soul_relic_slot", "body"), ("confirm_soul_relic_write", true)),
            Owner("browser-test"));

        Assert.True(result.Success, result.Message);
        var soul = JsonNode.Parse((await _fs.ReadFileAsync("game_state/meta/soul_state.json"))!)!.AsObject();
        var stored = soul["soulRelics"]!["stored"]!.AsArray();
        var equipped = soul["soulRelics"]!["equipped"]!.AsArray();
        Assert.Empty(stored);
        Assert.Equal("r1", equipped[0]!["relicId"]!.GetValue<string>());
        Assert.Equal("body", equipped[0]!["gameplayStatus"]!["currentSlot"]!.GetValue<string>());
    }

    [Fact]
    public async Task TryApplyAsync_SoulRelicEquip_RejectsUnknownRelic()
    {
        await SeedSoulStateAsync(stored: new[] { ("r1", "Кулон Тишины") }, equipped: Array.Empty<(string, string, string)>());

        var result = await _service.TryApplyAsync(
            "/soul_relic_equip",
            Answers(("soul_relic_identity", "missing"), ("soul_relic_slot", "head"), ("confirm_soul_relic_write", true)),
            Owner("browser-test"));

        Assert.False(result.Success);
        Assert.Contains("не найдена", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryApplyAsync_SoulRelicEquip_RejectsAlreadyEquipped()
    {
        await SeedSoulStateAsync(stored: Array.Empty<(string, string)>(), equipped: new[] { ("r1", "Кулон Тишины", "head") });

        var result = await _service.TryApplyAsync(
            "/soul_relic_equip",
            Answers(("soul_relic_identity", "r1"), ("soul_relic_slot", "body"), ("confirm_soul_relic_write", true)),
            Owner("browser-test"));

        Assert.False(result.Success);
        Assert.Contains("уже экипирована", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryApplyAsync_SoulRelicEquip_RejectsInvalidSlot()
    {
        await SeedSoulStateAsync(stored: new[] { ("r1", "Кулон Тишины") }, equipped: Array.Empty<(string, string, string)>());

        var result = await _service.TryApplyAsync(
            "/soul_relic_equip",
            Answers(("soul_relic_identity", "r1"), ("soul_relic_slot", "wing"), ("confirm_soul_relic_write", true)),
            Owner("browser-test"));

        Assert.False(result.Success);
        Assert.Contains("слот", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryApplyAsync_SoulRelicUnequip_MovesRelicFromEquippedToStored()
    {
        await SeedSoulStateAsync(stored: Array.Empty<(string, string)>(), equipped: new[] { ("r1", "Кулон Тишины", "head") });

        var result = await _service.TryApplyAsync(
            "/soul_relic_unequip",
            Answers(("soul_relic_slot", "head"), ("confirm_soul_relic_write", true)),
            Owner("browser-test"));

        Assert.True(result.Success, result.Message);
        var soul = JsonNode.Parse((await _fs.ReadFileAsync("game_state/meta/soul_state.json"))!)!.AsObject();
        var stored = soul["soulRelics"]!["stored"]!.AsArray();
        var equipped = soul["soulRelics"]!["equipped"]!.AsArray();
        Assert.Single(stored);
        Assert.Equal("r1", stored[0]!["relicId"]!.GetValue<string>());
        Assert.Empty(equipped);
    }

    [Fact]
    public async Task TryApplyAsync_SoulRelicUnequip_RejectsEmptySlot()
    {
        await SeedSoulStateAsync(stored: new[] { ("r1", "Кулон Тишины") }, equipped: Array.Empty<(string, string, string)>());

        var result = await _service.TryApplyAsync(
            "/soul_relic_unequip",
            Answers(("soul_relic_slot", "head"), ("confirm_soul_relic_write", true)),
            Owner("browser-test"));

        Assert.False(result.Success);
        Assert.Contains("нет экипированной реликвии", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryApplyAsync_SoulRelicUnequip_RejectsInvalidSlot()
    {
        await SeedSoulStateAsync(stored: new[] { ("r1", "Кулон Тишины") }, equipped: Array.Empty<(string, string, string)>());

        var result = await _service.TryApplyAsync(
            "/soul_relic_unequip",
            Answers(("soul_relic_slot", "wing"), ("confirm_soul_relic_write", true)),
            Owner("browser-test"));

        Assert.False(result.Success);
        Assert.Contains("слот", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryApplyAsync_SoulRelicEquip_ReleasesLockOnSuccess()
    {
        await SeedSoulStateAsync(stored: new[] { ("r1", "Кулон Тишины") }, equipped: Array.Empty<(string, string, string)>());

        await _service.TryApplyAsync(
            "/soul_relic_equip",
            Answers(("soul_relic_identity", "r1"), ("soul_relic_slot", "head"), ("confirm_soul_relic_write", true)),
            Owner("browser-test"));

        Assert.False(_fs.FileExists(LocalUiSessionLockService.LockPath));
    }

    [Fact]
    public async Task TryApplyAsync_SoulRelicEquip_RollsBackOnInvalidSlot()
    {
        await SeedSoulStateAsync(stored: new[] { ("r1", "Кулон Тишины") }, equipped: Array.Empty<(string, string, string)>());
        var beforeSoul = await _fs.ReadFileAsync("game_state/meta/soul_state.json");

        var result = await _service.TryApplyAsync(
            "/soul_relic_equip",
            Answers(("soul_relic_identity", "r1"), ("soul_relic_slot", "wing"), ("confirm_soul_relic_write", true)),
            Owner("browser-test"));

        Assert.False(result.Success);
        var afterSoul = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        Assert.Equal(beforeSoul, afterSoul);
    }

    [Fact]
    public async Task TryApplyAsync_SoulRelicUnequip_ReleasesLockOnSuccess()
    {
        await SeedSoulStateAsync(stored: Array.Empty<(string, string)>(), equipped: new[] { ("r1", "Кулон Тишины", "head") });

        await _service.TryApplyAsync(
            "/soul_relic_unequip",
            Answers(("soul_relic_slot", "head"), ("confirm_soul_relic_write", true)),
            Owner("browser-test"));

        Assert.False(_fs.FileExists(LocalUiSessionLockService.LockPath));
    }

    [Fact]
    public async Task TryApplyAsync_SpiritualArtsSelfUpgrade_UsesExpensiveStandardArtFallbackCost()
    {
        await SeedSoulStateAsync(stored: Array.Empty<(string, string)>(), equipped: Array.Empty<(string, string, string)>(), inkFeathers: 499);
        await SeedAfterlifeCombatProfileAsync();
        var beforeSoul = await _fs.ReadFileAsync("game_state/meta/soul_state.json");

        var result = await _service.TryApplyAsync(
            "/spiritual_arts",
            Answers(("upgrade_target", "pressure"), ("upgrade_currency", "ink_feathers")),
            Owner("browser-test"));

        Assert.False(result.Success);
        Assert.Contains("нужно 500", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(beforeSoul, await _fs.ReadFileAsync("game_state/meta/soul_state.json"));
    }

    [Fact]
    public async Task TryApplyAsync_SpiritualArtsSelfUpgrade_UsesExpensiveSpiritFocusFallbackCost()
    {
        await SeedSoulStateAsync(stored: Array.Empty<(string, string)>(), equipped: Array.Empty<(string, string, string)>(), inkFeathers: 599);
        await SeedAfterlifeCombatProfileAsync();
        var beforeSoul = await _fs.ReadFileAsync("game_state/meta/soul_state.json");

        var result = await _service.TryApplyAsync(
            "/spiritual_arts",
            Answers(("upgrade_target", "spirit_focus"), ("upgrade_currency", "ink_feathers")),
            Owner("browser-test"));

        Assert.False(result.Success);
        Assert.Contains("нужно 600", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(beforeSoul, await _fs.ReadFileAsync("game_state/meta/soul_state.json"));
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
        }
    }

    private async Task SeedSoulStateAsync(
        IReadOnlyList<(string relicId, string name)> stored,
        IReadOnlyList<(string relicId, string name, string slot)> equipped,
        int inkFeathers = 0,
        string currentRealm = "Chaos Sea")
    {
        var storedArray = new JsonArray();
        foreach (var (relicId, name) in stored)
        {
            storedArray.Add(new JsonObject
            {
                ["relicId"] = relicId,
                ["name"] = name,
                ["rarity"] = "rare",
                ["gameplayStatus"] = new JsonObject { ["equipped"] = false }
            });
        }
        var equippedArray = new JsonArray();
        foreach (var (relicId, name, slot) in equipped)
        {
            equippedArray.Add(new JsonObject
            {
                ["relicId"] = relicId,
                ["name"] = name,
                ["rarity"] = "rare",
                ["gameplayStatus"] = new JsonObject
                {
                    ["equipped"] = true,
                    ["currentSlot"] = slot
                }
            });
        }
        var soul = new JsonObject
        {
            ["soulName"] = "Тестовая душа",
            ["currentRealm"] = currentRealm,
            ["currentIncarnation"] = 4,
            ["inkFeathers"] = new JsonObject
            {
                ["current"] = inkFeathers,
                ["total"] = Math.Max(inkFeathers, 0)
            },
            ["soulRelics"] = new JsonObject
            {
                ["stored"] = storedArray,
                ["equipped"] = equippedArray
            }
        };
        await _fs.WriteFileAtomicAsync(
            "game_state/meta/soul_state.json",
            soul.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private async Task SeedAfterlifeCombatProfileAsync()
    {
        var soul = await ReadSoulAsync();
        soul["enlightenment"] = new JsonObject
        {
            ["level"] = 1,
            ["experience"] = 100
        };
        soul[AfterlifeSpiritualConflictState.SoulStateProfileProperty] = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["enlightenmentRank"] = 1,
            ["radianceRank"] = 0,
            ["retainedRadianceRank"] = 0,
            ["artTiers"] = new JsonObject(),
            [AfterlifeSpiritualConflictState.SpiritFocusTierProperty] = 0,
            ["lastRecoveryTurn"] = 0
        };
        await _fs.WriteFileAtomicAsync(
            "game_state/meta/soul_state.json",
            soul.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private async Task SeedPendingGachaBaseAsync(string baseRarity, int baseScore, IReadOnlyList<int> diceUsed)
    {
        var diceArray = new JsonArray();
        foreach (var die in diceUsed)
            diceArray.Add(die);

        await _fs.WriteFileAtomicAsync(
            PendingTurnStateService.PendingDiceStatePath,
            new JsonObject
            {
                ["preGeneratedDices1d20"] = new JsonArray(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20),
                ["gachaBaseResult"] = new JsonObject
                {
                    ["diceUsed"] = diceArray,
                    ["baseScore"] = baseScore,
                    ["baseRarity"] = baseRarity,
                    ["formula"] = "client-computed gacha base (range 4-80)"
                },
                ["isFateLocked"] = false,
                ["createdAtUtc"] = "2026-06-02T00:00:00Z",
                ["lastUpdatedUtc"] = "2026-06-02T00:00:00Z"
            }.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
    }

    private Task SeedStalePlayerSoulProfileAsync() =>
        _fs.WriteFileAtomicAsync(
            AfterlifeEntityProfileState.StatePath,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["profiles"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["actorType"] = "player_soul",
                        ["actorId"] = "player_soul",
                        ["displayName"] = "Тестовая душа",
                        ["realm"] = "Chaos Sea",
                        ["currencies"] = new JsonObject
                        {
                            ["inkFeathers"] = 0,
                            ["lightSparks"] = 0
                        },
                        ["progression"] = new JsonObject(),
                        ["standardArts"] = new JsonObject(),
                        ["specialArts"] = new JsonArray(),
                        ["customStates"] = new JsonArray(),
                        ["soulDissipationTier"] = 0,
                        ["ledger"] = new JsonArray()
                    }
                }
            }.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));

    private async Task<JsonObject> ReadSoulAsync() =>
        JsonNode.Parse((await _fs.ReadFileAsync("game_state/meta/soul_state.json"))!)!.AsObject();

    [Fact]
    public async Task TryApplyAsync_SoulRelicEquip_RequiresConfirmation()
    {
        await SeedSoulStateAsync(stored: new[] { ("r1", "Кулон Тишины") }, equipped: Array.Empty<(string, string, string)>());

        var result = await _service.TryApplyAsync(
            "/soul_relic_equip",
            Answers(("soul_relic_identity", "r1"), ("soul_relic_slot", "head"), ("confirm_soul_relic_write", false)),
            Owner("browser-test"));

        Assert.False(result.Success);
        Assert.Contains("Подтвердите", result.Message, StringComparison.OrdinalIgnoreCase);
        var soul = JsonNode.Parse((await _fs.ReadFileAsync("game_state/meta/soul_state.json"))!)!.AsObject();
        Assert.Single(soul["soulRelics"]!["stored"]!.AsArray());
        Assert.Empty(soul["soulRelics"]!["equipped"]!.AsArray());
    }

    [Fact]
    public async Task TryApplyAsync_SoulRelicUnequip_RequiresConfirmation()
    {
        await SeedSoulStateAsync(stored: Array.Empty<(string, string)>(), equipped: new[] { ("r1", "Кулон Тишины", "head") });

        var result = await _service.TryApplyAsync(
            "/soul_relic_unequip",
            Answers(("soul_relic_slot", "head"), ("confirm_soul_relic_write", false)),
            Owner("browser-test"));

        Assert.False(result.Success);
        Assert.Contains("Подтвердите", result.Message, StringComparison.OrdinalIgnoreCase);
        var soul = JsonNode.Parse((await _fs.ReadFileAsync("game_state/meta/soul_state.json"))!)!.AsObject();
        Assert.Empty(soul["soulRelics"]!["stored"]!.AsArray());
        Assert.Single(soul["soulRelics"]!["equipped"]!.AsArray());
    }

    private static IReadOnlyDictionary<string, JsonNode?> Answers(params (string key, object? value)[] pairs)
    {
        var dict = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
        foreach (var (key, value) in pairs)
        {
            dict[key] = value switch
            {
                null => null,
                bool flag => JsonValue.Create(flag),
                int number => JsonValue.Create(number),
                string text => JsonValue.Create(text),
                _ => JsonValue.Create(value.ToString())
            };
        }
        return dict;
    }

    private static LocalUiSessionLockOwner Owner(string id) =>
        new(id, "browser", "Browser", TimeSpan.FromMinutes(5));
}
