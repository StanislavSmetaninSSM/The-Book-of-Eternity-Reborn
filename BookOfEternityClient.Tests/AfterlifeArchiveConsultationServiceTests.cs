using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class AfterlifeArchiveConsultationServiceTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly AfterlifeArchiveConsultationService _service;

    public AfterlifeArchiveConsultationServiceTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-afterlife-archive-consult-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);

        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
        _service = new AfterlifeArchiveConsultationService(_fs, NullLogger<AfterlifeArchiveConsultationService>.Instance);
    }

    [Fact]
    public async Task CreateRequestAsync_LoreFragment_ReservesArchiveAndWritesPendingConsultationRequest()
    {
        await SeedSoulStateAsync("archive_lore_001", "lore_fragment", "Rare");
        await SeedGuardianAsync("guardian_azalia", "Азалия", 120);

        var result = await _service.CreateRequestAsync(
            "guardian_azalia",
            "Азалия",
            "archive_lore_001",
            currentIncarnation: 1,
            currentRealm: "Chaos Sea",
            currentTurn: 7);

        Assert.NotNull(result);
        Assert.Equal(2, result!.TargetIncarnation);
        Assert.Contains(AfterlifeArchiveActionState.ConsultationActionTag, result.PendingGmAction, StringComparison.Ordinal);

        using var soulDoc = JsonDocument.Parse((await _fs.ReadFileAsync("game_state/meta/soul_state.json"))!);
        var storedEntry = soulDoc.RootElement.GetProperty("afterlifeArchive").GetProperty("stored").EnumerateArray().Single();
        Assert.Equal("archive_lore_001", storedEntry.GetProperty("archiveId").GetString());
        Assert.Equal("consultation", storedEntry.GetProperty("reservation").GetProperty("reservationKind").GetString());
        Assert.Equal("guardian_azalia", storedEntry.GetProperty("reservation").GetProperty("guardianId").GetString());

        using var requestDoc = JsonDocument.Parse((await _fs.ReadFileAsync(AfterlifeArchiveActionState.ConsultationRequestPath))!);
        Assert.Equal("guardian_azalia", requestDoc.RootElement.GetProperty("guardianId").GetString());
        Assert.Equal("archive_lore_001", requestDoc.RootElement.GetProperty("archiveId").GetString());
        Assert.Equal("lore_fragment", requestDoc.RootElement.GetProperty("archiveEntryType").GetString());
        Assert.Equal(2, requestDoc.RootElement.GetProperty("targetIncarnation").GetInt32());
        Assert.Equal("consultation", requestDoc.RootElement.GetProperty("requestedMode").GetString());
    }

    [Fact]
    public async Task CreateRequestAsync_SecretRecord_UsesNextLifeTargetAndPendingFile()
    {
        await SeedSoulStateAsync("archive_secret_001", "secret_record", "Epic");
        await SeedGuardianAsync("guardian_noctis", "Ноктис", 130);

        var result = await _service.CreateRequestAsync(
            "guardian_noctis",
            "Ноктис",
            "archive_secret_001",
            currentIncarnation: 4,
            currentRealm: "Chaos Sea",
            currentTurn: 11);

        Assert.NotNull(result);
        Assert.Equal(5, result!.TargetIncarnation);

        using var requestDoc = JsonDocument.Parse((await _fs.ReadFileAsync(AfterlifeArchiveActionState.ConsultationRequestPath))!);
        Assert.Equal("secret_record", requestDoc.RootElement.GetProperty("archiveEntryType").GetString());
        Assert.Equal("Epic", requestDoc.RootElement.GetProperty("archiveRarity").GetString());
        Assert.Equal(5, requestDoc.RootElement.GetProperty("targetIncarnation").GetInt32());
    }

    private async Task SeedSoulStateAsync(string archiveId, string entryType, string rarity)
    {
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            currentRealm = "Chaos Sea",
            currentIncarnation = 1,
            afterlifeArchive = new
            {
                stored = new[]
                {
                    new
                    {
                        archiveId,
                        entryType,
                        title = "Тестовая архивная запись",
                        summary = "Тестовая запись для консультации.",
                        rarity,
                        sourceLife = 1,
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
