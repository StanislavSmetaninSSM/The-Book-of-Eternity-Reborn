using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class AfterlifeArchiveProjectFuelServiceTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly AfterlifeArchiveProjectFuelService _service;

    public AfterlifeArchiveProjectFuelServiceTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-afterlife-archive-fuel-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);

        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
        _service = new AfterlifeArchiveProjectFuelService(_fs, NullLogger<AfterlifeArchiveProjectFuelService>.Instance);
    }

    [Fact]
    public async Task CreateRequestAsync_LoreFragment_ReservesArchiveAndWritesFuelRequest()
    {
        await SeedSoulStateAsync("archive_lore_001", "lore_fragment", "Rare");
        await SeedGuardianAsync("guardian_brann", "Бранн", 120);
        await SeedActiveProjectAsync("guardian_brann", "forge_project", "Расширение кузни");

        var result = await _service.CreateRequestAsync("guardian_brann", "Бранн", "archive_lore_001", "Chaos Sea", 12);

        Assert.NotNull(result);
        Assert.Equal("forge_project", result!.ProjectId);
        Assert.Contains(AfterlifeArchiveActionState.ProjectFuelActionTag, result.PendingGmAction, StringComparison.Ordinal);

        using var soulDoc = JsonDocument.Parse((await _fs.ReadFileAsync("game_state/meta/soul_state.json"))!);
        var storedEntry = soulDoc.RootElement.GetProperty("afterlifeArchive").GetProperty("stored").EnumerateArray().Single();
        Assert.Equal("archive_lore_001", storedEntry.GetProperty("archiveId").GetString());
        Assert.Equal("project_fuel", storedEntry.GetProperty("reservation").GetProperty("reservationKind").GetString());
        Assert.Equal("forge_project", storedEntry.GetProperty("reservation").GetProperty("targetProjectId").GetString());

        using var requestDoc = JsonDocument.Parse((await _fs.ReadFileAsync(AfterlifeArchiveActionState.ProjectFuelRequestPath))!);
        Assert.Equal("guardian_brann", requestDoc.RootElement.GetProperty("guardianId").GetString());
        Assert.Equal("archive_lore_001", requestDoc.RootElement.GetProperty("archiveId").GetString());
        Assert.Equal("forge_project", requestDoc.RootElement.GetProperty("targetProjectId").GetString());
        Assert.Equal("project_fuel", requestDoc.RootElement.GetProperty("requestedMode").GetString());
    }

    [Fact]
    public async Task CreateRequestAsync_SecretRecord_UsesTargetProject()
    {
        await SeedSoulStateAsync("archive_secret_001", "secret_record", "Epic");
        await SeedGuardianAsync("guardian_azalia", "Азалия", 130);
        await SeedActiveProjectAsync("guardian_azalia", "social_project", "Дворцовая сеть");

        var result = await _service.CreateRequestAsync("guardian_azalia", "Азалия", "archive_secret_001", "Chaos Sea", 14);

        Assert.NotNull(result);
        Assert.Equal("social_project", result!.ProjectId);

        using var requestDoc = JsonDocument.Parse((await _fs.ReadFileAsync(AfterlifeArchiveActionState.ProjectFuelRequestPath))!);
        Assert.Equal("secret_record", requestDoc.RootElement.GetProperty("archiveEntryType").GetString());
        Assert.Equal("social_project", requestDoc.RootElement.GetProperty("targetProjectId").GetString());
    }

    [Fact]
    public async Task CreateRequestAsync_MalformedPendingProjectFuelFile_BlocksNewRequest()
    {
        await SeedSoulStateAsync("archive_lore_001", "lore_fragment", "Rare");
        await SeedGuardianAsync("guardian_brann", "Бранн", 120);
        await SeedActiveProjectAsync("guardian_brann", "forge_project", "Расширение кузни");
        await _fs.WriteFileAtomicAsync(
            AfterlifeArchiveActionState.ProjectFuelRequestPath,
            """
            {
              "requestId": "fuel_req_broken",
              "guardianId":
            """
        );

        var result = await _service.CreateRequestAsync("guardian_brann", "Бранн", "archive_lore_001", "Chaos Sea", 12);

        Assert.Null(result);
        Assert.True(_fs.FileExists(AfterlifeArchiveActionState.ProjectFuelRequestPath));
    }

    [Fact]
    public async Task CreateRequestAsync_PreservesUnrelatedMetaStateUpdatesAndOnlyPrunesConflictingArchiveTransientEntries()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
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
          "afterlifeArchiveUpdates": [
            {
              "command": "remove",
              "archiveId": "archive_lore_001"
            },
            {
              "command": "remove",
              "archiveId": "archive_keep"
            }
          ],
          "archiveActionResolutions": [
            {
              "requestId": "req_conflict",
              "archiveId": "archive_lore_001",
              "requestedMode": "project_fuel",
              "status": "cancelled"
            },
            {
              "requestId": "req_keep",
              "archiveId": "archive_keep",
              "requestedMode": "consultation",
              "status": "rejected"
            }
          ],
          "afterlifeArchive": {
            "stored": [
              {
                "archiveId": "archive_lore_001",
                "entryType": "lore_fragment",
                "title": "Тестовая архивная запись",
                "summary": "Тестовая запись для подпитки проекта.",
                "rarity": "Rare",
                "sourceLife": 2,
                "sourceKind": "codex",
                "acquiredAtUtc": "2026-03-26T00:00:00Z"
              }
            ]
          }
        }
        """);
        await SeedGuardianAsync("guardian_brann", "Бранн", 120);
        await SeedActiveProjectAsync("guardian_brann", "forge_project", "Расширение кузни");

        var result = await _service.CreateRequestAsync("guardian_brann", "Бранн", "archive_lore_001", "Chaos Sea", 12);

        Assert.NotNull(result);

        using var soulDoc = JsonDocument.Parse((await _fs.ReadFileAsync("game_state/meta/soul_state.json"))!);
        Assert.False(soulDoc.RootElement.TryGetProperty("crossIncarnationData", out _));
        Assert.True(soulDoc.RootElement.TryGetProperty("metaStateUpdates", out var metaStateUpdates));
        Assert.True(metaStateUpdates.TryGetProperty("memoryLegacyGrant", out _));

        var archiveUpdates = soulDoc.RootElement.GetProperty("afterlifeArchiveUpdates").EnumerateArray().ToList();
        Assert.Single(archiveUpdates);
        Assert.Equal("archive_keep", archiveUpdates[0].GetProperty("archiveId").GetString());

        var archiveResolutions = soulDoc.RootElement.GetProperty("archiveActionResolutions").EnumerateArray().ToList();
        Assert.Single(archiveResolutions);
        Assert.Equal("req_keep", archiveResolutions[0].GetProperty("requestId").GetString());
        Assert.Equal("archive_keep", archiveResolutions[0].GetProperty("archiveId").GetString());
    }

    private async Task SeedSoulStateAsync(string archiveId, string entryType, string rarity)
    {
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            currentRealm = "Chaos Sea",
            currentIncarnation = 2,
            afterlifeArchive = new
            {
                stored = new[]
                {
                    new
                    {
                        archiveId,
                        entryType,
                        title = "Тестовая архивная запись",
                        summary = "Тестовая запись для подпитки проекта.",
                        rarity,
                        sourceLife = 2,
                        sourceKind = "codex",
                        acquiredAtUtc = "2026-03-26T00:00:00Z"
                    }
                }
            }
        });
    }

    private async Task SeedGuardianAsync(string guardianId, string guardianName, int reputation)
    {
        await WriteJsonAsync("game_state/meta/guardians.json", new
        {
            guardians = new[]
            {
                new
                {
                    guardianId,
                    canonicalName = guardianName,
                    domain = "Любой Домен",
                    relationshipData = new
                    {
                        currentReputation = reputation
                    }
                }
            }
        });
    }

    private async Task SeedActiveProjectAsync(string guardianId, string projectId, string projectName)
    {
        await WriteJsonAsync(GuardianProjectState.TrackerPath, new
        {
            activeProjects = new[]
            {
                new
                {
                    guardianId,
                    project = new
                    {
                        projectId,
                        projectName,
                        projectType = "lore_research",
                        workDone = 2,
                        totalWork = 8,
                        pressure = 9
                    }
                }
            },
            completedProjects = Array.Empty<object>(),
            temporaryProjectModifiers = Array.Empty<object>()
        });
    }

    private async Task WriteJsonAsync(string relativePath, object payload) =>
        await _fs.WriteFileAtomicAsync(relativePath, JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_rootPath))
                Directory.Delete(_rootPath, recursive: true);
        }
        catch
        {
            // ignore temp cleanup failures
        }
    }
}
