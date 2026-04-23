using System.IO.Compression;
using System.Linq;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class SaveLoadServiceTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly SaveLoadService _service;

    public SaveLoadServiceTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-save-load-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);

        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();

        var settings = new GameSettings();
        var stateManager = new StateManager(_fs, settings, NullLogger<StateManager>.Instance);
        stateManager.RefreshGameStateAsync().GetAwaiter().GetResult();
        _service = new SaveLoadService(_fs, stateManager, NullLogger<SaveLoadService>.Instance);
    }

    [Fact]
    public async Task SaveAndLoad_TreatsProgressionReportAsEphemeralControlFile()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Тестовая Душа",
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 3
        }
        """);
        await _fs.WriteFileAtomicAsync(ProgressionScheduleService.ReportPath, """
        {
          "progressionProcessingReport": {
            "chaosSeaCyclesProcessed": 1,
            "guardianProjectCyclesProcessed": 1,
            "newLastChaosSeaSimulationOrdinal": 4,
            "newLastGuardianProjectCycleOrdinal": 4
          }
        }
        """);

        Assert.True(await _service.SaveGameAsync("ephemeral_progression_report", "save/load ephemeral regression"));

        var savePath = Directory.GetFiles(_fs.ResolvePath("saves/manual_saves"), "*.zip").Single();
        using (var archive = ZipFile.OpenRead(savePath))
            Assert.Null(archive.GetEntry(ProgressionScheduleService.ReportPath));

        Assert.True(await _service.LoadGameAsync(savePath));
        Assert.False(_fs.FileExists(ProgressionScheduleService.ReportPath));
    }

    [Fact]
    public async Task SaveAndLoad_PreservesLivePendingContracts()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Тестовая Душа",
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 3
        }
        """);
        await _fs.WriteFileAtomicAsync(GuardianAbodeResidentRequestState.PendingResidentsRequestPath, """
        {
          "requests": [
            {
              "requestId": "abode_roster_req_1",
              "guardianId": "guardian_test",
              "guardianName": "Тестовый Хранитель",
              "abodeId": "abode_test",
              "abodeName": "Тестовая Обитель",
              "currentReputation": 10,
              "requestMode": "standard_roster",
              "createdAtTurn": 3,
              "createdAtUtc": "2026-04-22T00:00:00Z"
            }
          ]
        }
        """);
        await _fs.WriteFileAtomicAsync(ActorSocialInteractionRequestState.PendingGuardianRequestPath, """
        {
          "requests": [
            {
              "requestId": "guardian_social_req_1",
              "guardianId": "guardian_test",
              "guardianName": "Тестовый Хранитель",
              "interactionType": "talk",
              "createdAtTurn": 3,
              "createdAtUtc": "2026-04-22T00:00:00Z"
            }
          ]
        }
        """);
        await _fs.WriteFileAtomicAsync(SystemGuardianLibraryService.AttractionRequestPath, """
        {
          "mode": "system_guardian_attraction",
          "targetPresetId": "preset_test",
          "targetPresetDisplayName": "Тестовый Хранитель",
          "targetPresetVersion": "1.0",
          "sourceLibrary": "built_in",
          "targetSummary": "summary",
          "renderedPromptPackage": "dossier",
          "lastUpdated": "2026-04-22T00:00:00Z"
        }
        """);
        await _fs.WriteFileAtomicAsync(ShiningTradeRequestState.PendingRequestsPath, """
        {
          "requests": [
            {
              "requestId": "shining_trade_req_1",
              "factionId": "faction_test",
              "factionName": "Хор Теста",
              "tradeCycleId": "shining_return_3",
              "derivedTradeTier": 2,
              "derivedTradeSlotCount": 6,
              "derivedRarityCeiling": "rare",
              "derivedServiceMultiplier": 1.25,
              "merchantProfile": "shining_faction",
              "createdAtTurn": 3,
              "createdAtUtc": "2026-04-22T00:00:00Z"
            }
          ]
        }
        """);
        await _fs.WriteFileAtomicAsync(AfterlifeArchiveActionState.ConsultationRequestPath, """
        {
          "requestId": "archive_consult_1",
          "archiveId": "archive_entry_1",
          "archiveTitle": "Запись Памяти",
          "requestedMode": "consultation",
          "createdAtTurn": 3,
          "createdAtUtc": "2026-04-22T00:00:00Z"
        }
        """);

        Assert.True(await _service.SaveGameAsync("ephemeral_chaos_pending", "save/load chaos pending regression"));

        var savePath = Directory.GetFiles(_fs.ResolvePath("saves/manual_saves"), "*.zip").Single();
        using (var archive = ZipFile.OpenRead(savePath))
        {
            Assert.NotNull(archive.GetEntry(GuardianAbodeResidentRequestState.PendingResidentsRequestPath));
            Assert.NotNull(archive.GetEntry(ActorSocialInteractionRequestState.PendingGuardianRequestPath));
            Assert.NotNull(archive.GetEntry(SystemGuardianLibraryService.AttractionRequestPath));
            Assert.NotNull(archive.GetEntry(ShiningTradeRequestState.PendingRequestsPath));
            Assert.NotNull(archive.GetEntry(AfterlifeArchiveActionState.ConsultationRequestPath));
        }

        Assert.True(await _service.LoadGameAsync(savePath));
        Assert.True(_fs.FileExists(GuardianAbodeResidentRequestState.PendingResidentsRequestPath));
        Assert.True(_fs.FileExists(ActorSocialInteractionRequestState.PendingGuardianRequestPath));
        Assert.True(_fs.FileExists(SystemGuardianLibraryService.AttractionRequestPath));
        Assert.True(_fs.FileExists(ShiningTradeRequestState.PendingRequestsPath));
        Assert.True(_fs.FileExists(AfterlifeArchiveActionState.ConsultationRequestPath));
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
            // ignore temp cleanup failures
        }
    }
}
