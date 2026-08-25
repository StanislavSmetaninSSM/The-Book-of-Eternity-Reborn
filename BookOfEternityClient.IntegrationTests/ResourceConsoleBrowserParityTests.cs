using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;
using BookOfEternityClient.WebUi;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

[Trait("Category", "RegressionIntegration")]
public sealed class ResourceConsoleBrowserParityTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly StateManager _stateManager;

    public ResourceConsoleBrowserParityTests()
    {
        _rootPath = Path.Combine(
            Path.GetTempPath(),
            "boe-resource-projection-parity-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
        _stateManager = new StateManager(
            _fs,
            new GameSettings(),
            NullLogger<StateManager>.Instance);
    }

    [Fact]
    public async Task MissingProjection_ConsoleAndSharedBrowserResultsUseOneSafeFailureWithoutFakeValues()
    {
        await _fs.WriteFileAtomicAsync(
            "game_state/meta/soul_state.json",
            """
            {
              "soulName": "Пепельная Искра",
              "currentRealm": "Mortal Realm",
              "currentIncarnation": 2
            }
            """);
        await _fs.WriteFileAtomicAsync(
            "game_state/core/player_status.json",
            """
            {
              "characterName": "Асуран",
              "currentCondition": "Насторожен",
              "healthPercentage": "97%",
              "energyPercentage": "96%",
              "poisePercentage": "95%"
            }
            """);

        var stats = await ExplorerMortalWorldCommandResultBuilder.TryBuildAsync(
            "/статы",
            _stateManager,
            _fs);
        var status = await ExplorerUniversalMetaCommandResultBuilder.TryBuildAsync(
            "/статус",
            _stateManager,
            _fs,
            new LocalizationManager { CurrentLanguage = "ru" });

        var options = new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        var statsJson = JsonSerializer.Serialize(stats, options);
        var statusJson = JsonSerializer.Serialize(status, options);
        foreach (var payload in new[] { statsJson, statusJson })
        {
            Assert.Contains(ResourcePlayerFailureMessages.Unavailable, payload, StringComparison.Ordinal);
            Assert.DoesNotContain("97%", payload, StringComparison.Ordinal);
            Assert.DoesNotContain("96%", payload, StringComparison.Ordinal);
            Assert.DoesNotContain("95%", payload, StringComparison.Ordinal);
            Assert.DoesNotContain("100%", payload, StringComparison.Ordinal);
            Assert.DoesNotContain("resource_", payload, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("game_state", payload, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task MissingProjection_BrowserPlayerDtoCarriesOnlySafeUnavailableState()
    {
        await _stateManager.RefreshGameStateAsync();
        var state = _stateManager.CurrentState;
        state.PlayerStatus.HealthPercentage = "97%";
        state.PlayerStatus.EnergyPercentage = "96%";
        state.PlayerStatus.PoisePercentage = "95%";
        state.PlayerStatus.ResourceProjectionAvailable = false;

        var factory = typeof(BrowserGameScreenService).GetMethod(
            "BuildPlayerDto",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(factory);

        var dto = Assert.IsType<BrowserGameScreenPlayerDto>(factory.Invoke(
            null,
            [state, Array.Empty<string>()]));
        var payload = JsonSerializer.Serialize(
            dto,
            new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

        Assert.False(dto.ResourceProjectionAvailable);
        Assert.Equal(ResourcePlayerFailureMessages.Unavailable, dto.ResourceUnavailableMessage);
        Assert.DoesNotContain("97%", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("96%", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("95%", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("resource_", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("game_state", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SuspendedMortalCoreProjection_SharedStatusSurfacesFailClosed()
    {
        await ResourceProjectionFixture.SeedAsync(
            _fs,
            new ProjectedResourceSeed(
                "mortal_world",
                ResourceOwnerKind.Player,
                "player_current",
                "health",
                137m,
                137m),
            new ProjectedResourceSeed(
                "mortal_world",
                ResourceOwnerKind.Player,
                "player_current",
                "energy",
                149m,
                149m),
            new ProjectedResourceSeed(
                "mortal_world",
                ResourceOwnerKind.Player,
                "player_current",
                "poise",
                173m,
                173m,
                ResourceLifecycleState.Suspended));
        await _fs.WriteFileAtomicAsync(
            "game_state/meta/soul_state.json",
            """
            {
              "soulName": "Пепельная Искра",
              "currentRealm": "Mortal Realm",
              "currentIncarnation": 2
            }
            """);

        var stats = await ExplorerMortalWorldCommandResultBuilder.TryBuildAsync(
            "/статы",
            _stateManager,
            _fs);
        var status = await ExplorerUniversalMetaCommandResultBuilder.TryBuildAsync(
            "/статус",
            _stateManager,
            _fs,
            new LocalizationManager { CurrentLanguage = "ru" });

        Assert.False(_stateManager.CurrentState.PlayerStatus.ResourceProjectionAvailable);
        var options = new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        foreach (var payload in new[]
                 {
                     JsonSerializer.Serialize(stats, options),
                     JsonSerializer.Serialize(status, options)
                 })
        {
            Assert.Contains(ResourcePlayerFailureMessages.Unavailable, payload, StringComparison.Ordinal);
            Assert.DoesNotContain("100%", payload, StringComparison.Ordinal);
            Assert.DoesNotContain("resource_", payload, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("game_state", payload, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task MissingProjection_CombatDetailDoesNotFallBackToRawHealthOrPoise()
    {
        await _fs.WriteFileAtomicAsync(
            "game_state/combat/enemies.json",
            """
            {
              "enemiesData": [
                {
                  "enemyId": "legacy_projection_enemy",
                  "combatantId": "combatant_projection_enemy",
                  "name": "Пепельный страж",
                  "currentHealth": 91,
                  "maxHealth": 99,
                  "currentPoise": 81,
                  "maxPoise": 89,
                  "activeBuffs": [],
                  "activeDebuffs": []
                }
              ]
            }
            """);

        var result = await ExplorerMortalWorldCommandResultBuilder.TryBuildAsync(
            "/бой враг legacy_projection_enemy",
            _stateManager,
            _fs);
        var payload = JsonSerializer.Serialize(
            result,
            new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

        Assert.NotNull(result);
        Assert.Contains(ResourcePlayerFailureMessages.Unavailable, payload, StringComparison.Ordinal);
        Assert.DoesNotContain("91/99", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("81/89", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("currentHealth", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("currentPoise", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("resource_state", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MissingProjection_ItemDetailDoesNotFallBackToRawDurability()
    {
        var item = MortalItemTestFixture.CreateCanonicalRoot("item_projection_legacy");
        item["name"] = "Клинок старого зеркала";
        item["type"] = "weapon";
        MortalItemTestFixture.ResealCanonical(item);
        await _fs.WriteFileAtomicAsync(
            "game_state/inventory/items.json",
            new JsonObject
            {
                ["items"] = new JsonArray(item)
            }.ToJsonString());
        await _fs.WriteFileAtomicAsync(
            "game_state/inventory/item_resources.json",
            """
            {
              "entries": [
                {
                  "itemId": "item_projection_legacy",
                  "durability": 7,
                  "maxDurability": 9
                }
              ]
            }
            """);

        var result = await ExplorerMortalWorldCommandResultBuilder.TryBuildAsync(
            "/инв предмет item_projection_legacy",
            _stateManager,
            _fs);
        var payload = JsonSerializer.Serialize(
            result,
            new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

        Assert.NotNull(result);
        Assert.Contains(ResourcePlayerFailureMessages.Unavailable, payload, StringComparison.Ordinal);
        Assert.DoesNotContain("7/9", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("durability", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("maxDurability", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("resource_state", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task StatusRecentResourceChanges_UseCanonicalHistoryInsteadOfLegacyStatusDeltas()
    {
        await ResourceProjectionFixture.SeedAsync(
            _fs,
            new ProjectedResourceSeed(
                "mortal_world",
                ResourceOwnerKind.Player,
                "player_current",
                "health",
                75m,
                100m),
            new ProjectedResourceSeed(
                "mortal_world",
                ResourceOwnerKind.Player,
                "player_current",
                "energy",
                80m,
                100m),
            new ProjectedResourceSeed(
                "mortal_world",
                ResourceOwnerKind.Player,
                "player_current",
                "poise",
                90m,
                100m));
        await _fs.WriteFileAtomicAsync(
            "game_state/meta/soul_state.json",
            """
            {
              "soulName": "Пепельная Искра",
              "currentRealm": "Mortal Realm",
              "currentIncarnation": 2
            }
            """);
        await _fs.WriteFileAtomicAsync(
            "game_state/player/status_changes.json",
            """
            {
              "currentHealthChange": 999,
              "currentEnergyChange": 777,
              "currentPoiseChange": 555
            }
            """);

        var result = await ExplorerUniversalMetaCommandResultBuilder.TryBuildAsync(
            "/статус",
            _stateManager,
            _fs,
            new LocalizationManager { CurrentLanguage = "ru" });
        var payload = JsonSerializer.Serialize(
            result,
            new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

        Assert.NotNull(result);
        Assert.Contains("-25", payload, StringComparison.Ordinal);
        Assert.Contains("-20", payload, StringComparison.Ordinal);
        Assert.Contains("-10", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("999", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("777", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("555", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("currentHealthChange", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("currentEnergyChange", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("currentPoiseChange", payload, StringComparison.Ordinal);
    }

    [Fact]
    public void InventoryPlayerProjection_DoesNotExposeLegacyInlineDurability()
    {
        var projectionMethod = typeof(ExplorerMortalWorldCommandResultBuilder).GetMethod(
            "ProjectInventoryItemForPlayer",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(projectionMethod);
        var projection = Assert.IsType<JsonObject>(projectionMethod.Invoke(
            null,
            [new JsonObject
            {
                ["name"] = "Клинок старого зеркала",
                ["type"] = "weapon",
                ["durability"] = 7,
                ["maxDurability"] = 9
            }]));
        var payload = JsonSerializer.Serialize(
            projection,
            new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

        Assert.DoesNotContain("durability", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("maxDurability", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Прочность", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("7/9", payload, StringComparison.Ordinal);
    }

    [Fact]
    public void NpcTradeBuybackProjection_DoesNotPersistLegacyInlineDurability()
    {
        var projectionMethod = typeof(NpcTradeService).GetMethod(
            "CreateTradeItemProjection",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(projectionMethod);
        var projection = Assert.IsType<JsonObject>(projectionMethod.Invoke(
            null,
            [new JsonObject
            {
                ["itemId"] = "item_trade_projection",
                ["name"] = "Клинок старого зеркала",
                ["durability"] = 7,
                ["maxDurability"] = 9
            }]));

        Assert.False(projection.ContainsKey("durability"));
        Assert.False(projection.ContainsKey("maxDurability"));
    }

    [Fact]
    public void ShiningGachaCycleStatus_DoesNotExposeInternalCycleIdentifiers()
    {
        var overviewFormatter = typeof(ExplorerMode).GetMethod(
            "BuildShiningReturnCycleStatusLabel",
            BindingFlags.NonPublic | BindingFlags.Static);
        var tradeFormatter = typeof(ExplorerMode).GetMethod(
            "DescribeReturnCycleStatus",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(overviewFormatter);
        Assert.NotNull(tradeFormatter);

        var overview = Assert.IsType<string>(overviewFormatter.Invoke(
            null,
            [new JsonObject
            {
                ["gachaSystem"] = new JsonObject
                {
                    ["currentReturnCycleId"] = "return_cycle_internal_7"
                }
            }]));
        var trade = Assert.IsType<string>(tradeFormatter.Invoke(
            null,
            ["return_cycle_internal_7"]));

        foreach (var value in new[] { overview, trade })
        {
            Assert.Contains("цикл возвращения", value, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("синхронизирован", value, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("return_cycle_internal_7", value, StringComparison.Ordinal);
            Assert.DoesNotContain("currentReturnCycleId", value, StringComparison.Ordinal);
        }
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
            // Best-effort cleanup for isolated test state.
        }
    }
}
