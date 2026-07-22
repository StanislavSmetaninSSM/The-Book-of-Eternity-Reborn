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
    public async Task GetAvailableSavesAsync_RetriesWhenSaveMetadataIsTemporarilyLocked()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Тестовая Душа",
          "currentRealm": "Mortal World",
          "currentIncarnation": 1
        }
        """);
        Assert.True(await _service.SaveGameAsync("temporarily_locked_metadata", "save list transient lock regression"));

        var savePath = Directory.GetFiles(_fs.ResolvePath("saves/manual_saves"), "*.zip").Single();
        await using var lockStream = new FileStream(savePath, FileMode.Open, FileAccess.Read, FileShare.None);
        var readTask = _service.GetAvailableSavesAsync("saves/manual_saves");

        await Task.Delay(75);
        await lockStream.DisposeAsync();
        var saves = await readTask;

        var save = Assert.Single(saves);
        Assert.Equal(savePath, save.FileName);
        Assert.Equal("temporarily_locked_metadata", save.Metadata?.SaveName);
    }

    [Fact]
    public async Task SaveGameAsync_WaitsForCanonicalWriteLeaseBeforeReadingSessionSnapshot()
    {
        await _fs.WriteFileAtomicAsync("game_state/world/weather.json", "{\"state\":\"stable\"}");
        Task<bool> saveTask;

        var writeLease = await _fs.AcquireCanonicalWriteLeaseAsync();
        try
        {
            saveTask = _service.SaveGameAsync("leased_save", "canonical save snapshot regression");
            await Task.Delay(200);
            Assert.False(saveTask.IsCompleted);
        }
        finally
        {
            await writeLease.DisposeAsync();
        }

        Assert.True(await saveTask);
    }

    [Fact]
    public async Task LoadGameAsync_WaitsForCanonicalWriteLeaseBeforeReplacingLiveSession()
    {
        const string weatherPath = "game_state/world/weather.json";
        await _fs.WriteFileAtomicAsync(weatherPath, "{\"state\":\"saved\"}");
        Assert.True(await _service.SaveGameAsync("leased_load", "canonical load swap regression"));
        var savePath = Directory.GetFiles(_fs.ResolvePath("saves/manual_saves"), "*.zip").Single();
        await _fs.WriteFileAtomicAsync(weatherPath, "{\"state\":\"live\"}");
        Task<bool> loadTask;

        var writeLease = await _fs.AcquireCanonicalWriteLeaseAsync();
        try
        {
            loadTask = _service.LoadGameAsync(savePath);
            await Task.Delay(250);
            Assert.False(loadTask.IsCompleted);
            Assert.Equal("{\"state\":\"live\"}", await _fs.ReadFileAsync(weatherPath));
        }
        finally
        {
            await writeLease.DisposeAsync();
        }

        Assert.True(await loadTask);
        Assert.Equal("{\"state\":\"saved\"}", await _fs.ReadFileAsync(weatherPath));
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

    [Fact]
    public async Task LoadGameAsync_RejectsZipSlipEntriesAndPreservesLiveSession()
    {
        const string soulStatePath = "game_state/meta/soul_state.json";
        await _fs.WriteFileAtomicAsync(soulStatePath, """
        {
          "soulName": "Живая Душа",
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 7
        }
        """);

        var outsidePath = Path.Combine(_rootPath, "outside.txt");
        var maliciousZip = Path.Combine(_rootPath, "zip_slip.zip");
        using (var archive = ZipFile.Open(maliciousZip, ZipArchiveMode.Create))
        {
            using var entryWriter = new StreamWriter(archive.CreateEntry("../../outside.txt").Open());
            await entryWriter.WriteAsync("owned");
        }

        Assert.False(await _service.LoadGameAsync(maliciousZip));
        Assert.False(File.Exists(outsidePath));

        var soulState = await _fs.ReadFileAsync(soulStatePath);
        Assert.NotNull(soulState);
        Assert.Contains("Живая Душа", soulState);
    }

    [Fact]
    public async Task LoadGameAsync_CorruptArchiveDoesNotDestroyLiveSession()
    {
        const string soulStatePath = "game_state/meta/soul_state.json";
        await _fs.WriteFileAtomicAsync(soulStatePath, """
        {
          "soulName": "Неприкосновенная Душа",
          "currentRealm": "Shining Abode",
          "currentIncarnation": 2
        }
        """);
        await _fs.WriteFileAtomicAsync("mods/custom_rule.json", """
        { "enabled": true }
        """);

        var corruptZip = Path.Combine(_rootPath, "corrupt.zip");
        await File.WriteAllTextAsync(corruptZip, "this is not a zip archive");

        Assert.False(await _service.LoadGameAsync(corruptZip));
        Assert.True(_fs.FileExists(soulStatePath));
        Assert.True(_fs.FileExists("mods/custom_rule.json"));

        var soulState = await _fs.ReadFileAsync(soulStatePath);
        Assert.NotNull(soulState);
        Assert.Contains("Неприкосновенная Душа", soulState);
    }

    [Fact]
    public async Task SaveGameAsync_ExcludesLifecycleTriggers_AndLoadRemovesLegacyTriggerFiles()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Тестовая Душа",
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 5
        }
        """);
        await _fs.WriteFileAtomicAsync("game_state/control/life_transitions.json", """
        { "transitions": [{ "transitionId": "life_transition_1" }] }
        """);
        await _fs.WriteFileAtomicAsync("game_state/control/incarnation_trigger.json", """
        { "triggeredAtTurn": 10 }
        """);
        await _fs.WriteFileAtomicAsync("game_state/control/ascension.json", """
        { "triggeredAtTurn": 11 }
        """);

        Assert.True(await _service.SaveGameAsync("no_lifecycle_triggers", "save/load lifecycle regression"));

        var savePath = Directory.GetFiles(_fs.ResolvePath("saves/manual_saves"), "*.zip").Single();
        using (var archive = ZipFile.OpenRead(savePath))
        {
            Assert.Null(archive.GetEntry("game_state/control/life_transitions.json"));
            Assert.Null(archive.GetEntry("game_state/control/incarnation_trigger.json"));
            Assert.Null(archive.GetEntry("game_state/control/ascension.json"));
        }

        var legacyZip = Path.Combine(_rootPath, "legacy_with_triggers.zip");
        using (var archive = ZipFile.Open(legacyZip, ZipArchiveMode.Create))
        {
            var soulEntry = archive.CreateEntry("game_state/meta/soul_state.json");
            await using (var soulStream = soulEntry.Open())
            await using (var writer = new StreamWriter(soulStream))
            {
                await writer.WriteAsync("""
                {
                  "soulName": "Загруженная Душа",
                  "currentRealm": "Chaos Sea",
                  "currentIncarnation": 6
                }
                """);
            }

            var transitionsEntry = archive.CreateEntry("game_state/control/life_transitions.json");
            await using (var transitionStream = transitionsEntry.Open())
            await using (var writer = new StreamWriter(transitionStream))
            {
                await writer.WriteAsync("""{ "transitions": [{ "transitionId": "stale" }] }""");
            }

            var incarnationEntry = archive.CreateEntry("game_state/control/incarnation_trigger.json");
            await using (var incarnationStream = incarnationEntry.Open())
            await using (var writer = new StreamWriter(incarnationStream))
            {
                await writer.WriteAsync("""{ "triggeredAtTurn": 42 }""");
            }

            var ascensionEntry = archive.CreateEntry("game_state/control/ascension.json");
            await using (var ascensionStream = ascensionEntry.Open())
            await using (var writer = new StreamWriter(ascensionStream))
            {
                await writer.WriteAsync("""{ "triggeredAtTurn": 43 }""");
            }
        }

        Assert.True(await _service.LoadGameAsync(legacyZip));
        Assert.False(_fs.FileExists("game_state/control/life_transitions.json"));
        Assert.False(_fs.FileExists("game_state/control/incarnation_trigger.json"));
        Assert.False(_fs.FileExists("game_state/control/ascension.json"));
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
