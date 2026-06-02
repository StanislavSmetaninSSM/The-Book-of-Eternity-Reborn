using System.Text.Json;
using System.Text.Json.Nodes;
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
    public async Task TryApplyAsync_SoulRelicEquip_MovesRelicFromStoredToEquipped()
    {
        await SeedSoulStateAsync(stored: new[] { ("r1", "Кулон Тишины") }, equipped: Array.Empty<(string, string, string)>());

        var result = await _service.TryApplyAsync(
            "/soul_relic_equip",
            Answers(("soul_relic_identity", "r1"), ("soul_relic_slot", "head")),
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
            Answers(("soul_relic_identity", "Кулон Тишины"), ("soul_relic_slot", "body")),
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
            Answers(("soul_relic_identity", "missing"), ("soul_relic_slot", "head")),
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
            Answers(("soul_relic_identity", "r1"), ("soul_relic_slot", "body")),
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
            Answers(("soul_relic_identity", "r1"), ("soul_relic_slot", "wing")),
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
            Answers(("soul_relic_slot", "head")),
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
            Answers(("soul_relic_slot", "head")),
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
            Answers(("soul_relic_slot", "wing")),
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
            Answers(("soul_relic_identity", "r1"), ("soul_relic_slot", "head")),
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
            Answers(("soul_relic_identity", "r1"), ("soul_relic_slot", "wing")),
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
            Answers(("soul_relic_slot", "head")),
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
        IReadOnlyList<(string relicId, string name, string slot)> equipped)
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
            ["currentRealm"] = "chaosSea",
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

    private static IReadOnlyDictionary<string, JsonNode?> Answers(params (string key, string value)[] pairs)
    {
        var dict = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
        foreach (var (key, value) in pairs)
            dict[key] = JsonValue.Create(value);
        return dict;
    }

    private static LocalUiSessionLockOwner Owner(string id) =>
        new(id, "browser", "Browser", TimeSpan.FromMinutes(5));
}
