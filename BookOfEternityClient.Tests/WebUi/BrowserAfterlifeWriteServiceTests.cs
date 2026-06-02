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
