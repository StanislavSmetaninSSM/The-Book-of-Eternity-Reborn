using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class CriticalStateHealthServiceTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly CriticalStateHealthService _service;

    public CriticalStateHealthServiceTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-critical-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
        _service = new CriticalStateHealthService(_fs, NullLogger<CriticalStateHealthService>.Instance);
    }

    [Fact]
    public async Task ValidateAcceptedTurnRawStateAsync_FlagsPowerShellAstLikeGuardiansPayload()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/guardians.json", """
        {
          "guardians": [
            {
              "guardianId": "azalia",
              "name": "Азалия",
              "loreFragments": [
                {
                  "Ast": {},
                  "StartPosition": {
                    "Content": "{ fragmentId = \"lore_az_02\"; category = \"cosmic_secret\"; }"
                  }
                }
              ]
            }
          ]
        }
        """);

        var issues = await _service.ValidateAcceptedTurnRawStateAsync();

        Assert.Contains(issues, issue => issue.Code == "guardians_contains_powershell_runtime_object");
    }

    [Fact]
    public async Task AssessCurrentSessionHealthAsync_MarksOversizedGuardiansAsBrokenSession()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Асуран",
          "currentRealm": "Chaos Sea"
        }
        """);

        var oversizedContent = "{" + new string(' ', 17 * 1024 * 1024) + "}";
        await File.WriteAllTextAsync(_fs.ResolvePath("game_state/meta/guardians.json"), oversizedContent);

        var result = await _service.AssessCurrentSessionHealthAsync();

        Assert.True(result.HasRecoverableSessionError);
        Assert.Contains(result.Issues, issue => issue.Code == "critical_state_file_oversized");
    }

    [Fact]
    public async Task ValidateCriticalCanonicalStateAsync_AllowsCanonicalGuardiansShape()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/guardians.json", """
        {
          "guardians": [],
          "pendingGuardianCreation": {
            "description": "Азалия",
            "soulName": "Асуран"
          }
        }
        """);
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Асуран",
          "currentRealm": "Chaos Sea"
        }
        """);
        await _fs.WriteFileAtomicAsync("game_state/meta/achievements.json", """
        {
          "unlockedAchievements": [],
          "trackedProgress": [],
          "stats": {}
        }
        """);
        await _fs.WriteFileAtomicAsync("lore/codex_entries.json", """
        {
          "entries": [],
          "totalEntries": 0,
          "categories": {}
        }
        """);

        var issues = await _service.ValidateCriticalCanonicalStateAsync();

        Assert.DoesNotContain(issues, issue => issue.FilePath == "game_state/meta/guardians.json");
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
