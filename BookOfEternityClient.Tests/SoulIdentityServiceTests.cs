using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class SoulIdentityServiceTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly SoulIdentityService _service;

    public SoulIdentityServiceTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-soul-identity-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);

        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
        _service = new SoulIdentityService(_fs, NullLogger<SoulIdentityService>.Instance);
    }

    [Fact]
    public async Task RenameSoulAsync_AppendsCurrentNameToPreviousNamesAndUpdatesPendingGuardianCreation()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Аурелия",
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 2
        }
        """);
        await _fs.WriteFileAtomicAsync("game_state/meta/guardians.json", """
        {
          "pendingGuardianCreation": {
            "description": "Тестовый хранитель",
            "soulName": "Аурелия"
          }
        }
        """);

        var result = await _service.RenameSoulAsync("Пепельная Искра");

        Assert.True(result.Success);
        Assert.True(result.Changed);
        Assert.Equal("Пепельная Искра", result.CurrentSoulName);
        Assert.Contains("Аурелия", result.PreviousSoulNames);

        var soulRaw = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        Assert.NotNull(soulRaw);
        Assert.Contains("\"soulName\": \"Пепельная Искра\"", soulRaw, StringComparison.Ordinal);
        Assert.Contains("\"previousSoulNames\": [", soulRaw, StringComparison.Ordinal);
        Assert.Contains("\"Аурелия\"", soulRaw, StringComparison.Ordinal);

        var guardiansRaw = await _fs.ReadFileAsync("game_state/meta/guardians.json");
        Assert.NotNull(guardiansRaw);
        Assert.Contains("\"soulName\": \"Пепельная Искра\"", guardiansRaw, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RenameSoulAsync_ReturningToFormerName_RemovesItFromHistoryAndKeepsOnlyFormerAliases()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Пепельная Искра",
          "previousSoulNames": ["Аурелия", "Сумеречный Прах"],
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 2
        }
        """);

        var result = await _service.RenameSoulAsync("Аурелия");

        Assert.True(result.Success);
        Assert.True(result.Changed);
        Assert.Equal("Аурелия", result.CurrentSoulName);
        Assert.DoesNotContain("Аурелия", result.PreviousSoulNames, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("Пепельная Искра", result.PreviousSoulNames);
        Assert.Contains("Сумеречный Прах", result.PreviousSoulNames);

        var updatedSoulJson = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        Assert.NotNull(updatedSoulJson);
        var soulNode = JsonNode.Parse(updatedSoulJson)!.AsObject();
        var previousSoulNames = soulNode["previousSoulNames"]!.AsArray().Select(node => node!.GetValue<string>()).ToArray();
        Assert.Equal(new[] { "Сумеречный Прах", "Пепельная Искра" }, previousSoulNames);
    }

    [Fact]
    public async Task RenameSoulAsync_RejectsEmptyName()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Аурелия",
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 1
        }
        """);

        var result = await _service.RenameSoulAsync("   ");

        Assert.False(result.Success);
        Assert.False(result.Changed);
        Assert.Equal("Имя души не может быть пустым.", result.ErrorMessage);
    }

    [Fact]
    public async Task RenameSoulAsync_PreservesUnrelatedTransientCommandRootsButStripsLegacySoulStateRootKeysOnWrite()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Аурелия",
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 2,
          "crossIncarnationData": {
            "legacyThreadId": "thread_alpha"
          },
          "metaStateUpdates": {
            "memoryLegacyGrant": {
              "legacyId": "legacy_alpha",
              "legacyType": "startingCharacteristicBonus",
              "sourceLifeHint": "life_001",
              "characteristic": "strength",
              "bonus": 2
            }
          },
          "afterlifeArchiveUpdates": [],
          "archiveActionResolutions": []
        }
        """);

        var result = await _service.RenameSoulAsync("Пепельная Искра");

        Assert.True(result.Success);

        var soulRaw = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        Assert.NotNull(soulRaw);
        Assert.Contains("\"soulName\": \"Пепельная Искра\"", soulRaw, StringComparison.Ordinal);
        Assert.DoesNotContain("crossIncarnationData", soulRaw, StringComparison.Ordinal);
        Assert.Contains("metaStateUpdates", soulRaw, StringComparison.Ordinal);
        Assert.Contains("afterlifeArchiveUpdates", soulRaw, StringComparison.Ordinal);
        Assert.Contains("archiveActionResolutions", soulRaw, StringComparison.Ordinal);
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
            // Ignore temp cleanup failures on Windows.
        }
    }
}
