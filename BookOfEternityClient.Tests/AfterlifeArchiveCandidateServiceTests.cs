using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class AfterlifeArchiveCandidateServiceTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly AfterlifeArchiveCandidateService _service;

    public AfterlifeArchiveCandidateServiceTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-afterlife-archive-candidates-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);

        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
        _service = new AfterlifeArchiveCandidateService(_fs, NullLogger<AfterlifeArchiveCandidateService>.Instance);
    }

    [Fact]
    public async Task RefreshFromCurrentStateAsync_BuildsCandidatesForCurrentCompletedLifeOnly()
    {
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Тест",
            currentRealm = "Chaos Sea",
            currentIncarnation = 2,
            livesHistory = new[] { new { incarnation = 1 }, new { incarnation = 2 } },
            afterlifeArchive = new { stored = Array.Empty<object>() }
        });
        await WriteRawJsonAsync("lore/codex_entries.json", """
        {
          "entries": [
            {
              "entryId": "codex_old_001",
              "title": "Старый след",
              "category": "history",
              "content": "Не должен попасть в текущий manifest.",
              "discoveredAt": "2026-03-20T00:00:00Z",
              "discoveryContext": "old",
              "incarnation": 1
            },
            {
              "entryId": "codex_life_002",
              "title": "Свод рун",
              "category": "magic",
              "content": "Тайные руны, пережившие смерть героя.",
              "discoveredAt": "2026-03-24T00:00:00Z",
              "discoveryContext": "life",
              "incarnation": 2,
              "tags": ["hidden"]
            }
          ],
          "totalEntries": 2,
          "categories": {
            "cosmology": 0,
            "geography": 0,
            "history": 1,
            "cultures": 0,
            "creatures": 0,
            "characters": 0,
            "artifacts": 0,
            "factions": 0,
            "magic": 1,
            "other": 0
          }
        }
        """);

        await _service.RefreshFromCurrentStateAsync();
        var manifest = await _service.ReadAsync();

        Assert.NotNull(manifest);
        Assert.Equal(2, manifest!.SourceLife);
        Assert.Single(manifest.Candidates);
        Assert.Equal("codex_life_002", manifest.Candidates[0].SourceEntryId);
        Assert.Equal(AfterlifeArchiveState.SourceKindCodex, manifest.Candidates[0].SourceKind);
        Assert.Equal(AfterlifeArchiveState.EntryTypeSecretRecord, manifest.Candidates[0].ProposedEntryType);
        Assert.Equal("Epic", manifest.Candidates[0].Rarity);
    }

    [Fact]
    public async Task ArchiveCandidateAsync_AppendsEntryToAfterlifeArchiveAndMarksCandidateArchived()
    {
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Тест",
            currentRealm = "Chaos Sea",
            currentIncarnation = 1,
            livesHistory = new[] { new { incarnation = 1 } },
            afterlifeArchive = new { stored = Array.Empty<object>() }
        });
        await WriteJsonAsync(AfterlifeArchiveCandidateService.ManifestPath, new
        {
            sourceLife = 1,
            lastExtractedAt = "2026-03-26T00:00:00Z",
            candidates = new[]
            {
                new
                {
                    candidateId = "archive_candidate_codex_test_001",
                    sourceEntryId = "codex_test_001",
                    sourceLife = 1,
                    proposedEntryType = "lore_fragment",
                    title = "Хроника Ворот",
                    summary = "Подходит для архива.",
                    rarity = "Uncommon",
                    status = "pending",
                    discoveredAt = "2026-03-24T00:00:00Z",
                    tags = new[] { "lore" }
                }
            }
        });

        var result = await _service.ArchiveCandidateAsync("archive_candidate_codex_test_001");

        Assert.True(result);

        var manifest = await _service.ReadAsync();
        Assert.NotNull(manifest);
        Assert.Equal(AfterlifeArchiveCandidateService.StatusArchived, manifest!.Candidates[0].Status);

        var soulJson = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        Assert.NotNull(soulJson);
        using var soulDoc = JsonDocument.Parse(soulJson!);
        var stored = soulDoc.RootElement.GetProperty("afterlifeArchive").GetProperty("stored");
        var entries = stored.EnumerateArray().ToList();
        Assert.Single(entries);
        Assert.Equal("codex_test_001", entries[0].GetProperty("sourceEntryId").GetString());
        Assert.Equal(AfterlifeArchiveState.SourceKindCodex, entries[0].GetProperty("sourceKind").GetString());
        Assert.False(entries[0].TryGetProperty("codexCategory", out _));
        Assert.False(entries[0].TryGetProperty("facets", out _));
    }

    private async Task WriteJsonAsync(string relativePath, object payload) =>
        await _fs.WriteFileAtomicAsync(relativePath, JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));

    private async Task WriteRawJsonAsync(string relativePath, string rawJson) =>
        await _fs.WriteFileAtomicAsync(relativePath, rawJson);

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
