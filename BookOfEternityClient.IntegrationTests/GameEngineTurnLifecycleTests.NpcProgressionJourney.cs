using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.AgentConsole;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.WebUi;
using BookOfEternityClient.UI;
using BookOfEternityClient.Core;
using BookOfEternityClient.IO;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    private const string ProgressionJourneyActor = "npc_progression_journey";

    [Fact]
    public Task NpcProgressionJourney_OrdinaryXpGainCompletesWholeTurnAndReloads() =>
        RunProgressionJourneyAsync(withConsumers: false);

    [Fact]
    public Task NpcProgressionJourney_AcceptedLevelFeedsMerchantAndTeacherFreshness() =>
        RunProgressionJourneyAsync(withConsumers: true);

    private async Task RunProgressionJourneyAsync(bool withConsumers, bool repairInvalidResult = false)
    {
        var resultLevel = withConsumers ? 16 : 2;
        var resultThreshold = withConsumers ? 102400 : 565;
        using var owned = new CleanupOwnedFixture(_rootPath, line => _directGachaOutput?.WriteLine(line));
        await SeedProgressionJourneyAsync();
        if (withConsumers) await SeedProgressionJourneyConsumersAsync();
        var consumerBaseline = withConsumers ? await CaptureReadyJourneyConsumersAsync() : null;
        var log = new SpiritualLifecycleTestLogger();
        var input = new ProgressionJourneyInput(_fs, repairInvalidResult);
        var runtime = CreateGameEngineRuntime(_fs, input,
            settings => { InertRealmSettings(settings); settings.AutosaveIntervalTurns = 0; },
            logger: log, finalizationHooks: new GameEngineSessionFinalizationHooks { AtCheckpointAsync = async checkpoint =>
            {
                if (checkpoint != SessionFinalizationCheckpoint.TerminalWaitStarted) return;
                var request = JsonSerializer.Deserialize<TurnRequest>((await _fs.ReadFileAsync("input/turn_request.json"))!, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed)!;
                var command = new JsonObject
                {
                    ["NPCId"] = ProgressionJourneyActor, ["reason"] = "Accepted field observation experience",
                    ["progression"] = new JsonObject
                    {
                        ["source"] = new JsonObject { ["sessionId"] = request.SessionId, ["requestId"] = request.RequestId, ["turnNumber"] = request.TurnNumber },
                        ["experienceDelta"] = 2, ["level"] = resultLevel, ["experience"] = repairInvalidResult ? 2 : 1, ["experienceForNextLevel"] = resultThreshold
                    }
                };
                await new StateDistributor(_fs, NullLogger<StateDistributor>.Instance).DistributeAsync(new GameResponse
                { NPCCoreChanges = new[] { JsonSerializer.SerializeToElement(command) } });
                await WriteProgressionJourneyOutputsAsync(request, resultLevel, resultThreshold);
            }});
        runtime.GameLoop.SetSession("session_progression_journey", 41);
        await _fs.WriteFileAtomicAsync("game_state/history/chat_log.json", "{\"sessionId\":\"session_progression_journey\",\"entries\":[]}");
        await runtime.StateManager.RefreshGameStateAsync();
        var initial = await runtime.ValidationService.ValidateGameStateAsync();
        Assert.True(!initial.Any(x => x.Severity == IssueSeverity.Error), "Initial state: " + JsonSerializer.Serialize(initial));
        var beforeActor = repairInvalidResult
            ? JsonNode.Parse((await _fs.ReadFileAsync(NpcCoreChangesContract.NpcCorePath))!)!["NPCsInScene"]![0]!.ToJsonString()
            : string.Empty;
        using var repairCancellation = new CancellationTokenSource();
        var repair = repairInvalidResult
            ? ObserveAndRepairProgressionJourneyAsync(runtime.GameLoop, input, beforeActor, repairCancellation.Token)
            : Task.CompletedTask;
        try
        {
            await runtime.Engine.ProcessPlayerTurn("Выслушать отчёт наблюдателя о завершённом упражнении.");
            if (repairInvalidResult) Assert.True(input.RepairCompleted, "Expected a real rejected-result repair before acceptance.");
        }
        finally
        {
            repairCancellation.Cancel();
            await repair;
        }
        Assert.True(runtime.GameLoop.TurnNumber == 42, log.Describe());
        var core = JsonNode.Parse((await _fs.ReadFileAsync(NpcCoreChangesContract.NpcCorePath))!)!.AsObject();
        var actor = Assert.Single(core["NPCsInScene"]!.AsArray());
        Assert.Equal(resultLevel, actor!["level"]!.GetValue<int>());
        Assert.Equal(1, actor["experience"]!.GetValue<int>());
        Assert.Equal(resultThreshold, actor["experienceForNextLevel"]!.GetValue<int>());
        Assert.False(core.ContainsKey("NPCCoreChanges"));
        var story = (await _fs.ReadFileAsync("stories/mortal_life_2.jsonl"))!;
        Assert.Equal(42, JsonNode.Parse(Assert.Single(story.Split('\n', StringSplitOptions.RemoveEmptyEntries)))!["turn"]!.GetValue<int>());
        var reloadFiles = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        var reloaded = new StateManager(reloadFiles, new GameSettings(), NullLogger<StateManager>.Instance);
        await reloaded.RefreshGameStateAsync();
        Assert.Equal("session_progression_journey", reloaded.CurrentState.SessionId);
        Assert.Equal(42, reloaded.CurrentState.TurnNumber);
        var reloadView = new ExplorerWebCommandService(reloadFiles, reloaded, new LocalizationManager(),
            new ValidationService(reloadFiles, NullLogger<ValidationService>.Instance));
        var details = await reloadView.ExecuteAsync(new ExplorerWebCommandRequest($"/npc section {ProgressionJourneyActor} mechanics"));
        Assert.Equal(CommandExecutionState.Completed, details.State);
        var publicOutput = string.Join("\n", details.Blocks.Select(block => JsonSerializer.Serialize(block, block.GetType(), SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed)));
        Assert.Contains($"1/{resultThreshold}", publicOutput, StringComparison.Ordinal);
        Assert.Contains("Стандартный", publicOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("session_progression_journey", publicOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("snapshotToken", publicOutput, StringComparison.Ordinal);
        Assert.Equal(core.ToJsonString(), JsonNode.Parse((await reloadFiles.ReadFileAsync(NpcCoreChangesContract.NpcCorePath))!)!.ToJsonString());
        if (consumerBaseline != null)
        {
            await AssertJourneyConsumersAfterProgressionAsync(reloadFiles, consumerBaseline);
            var diagnosticRoot = Environment.GetEnvironmentVariable("BOE_TEST_DIAGNOSTIC_ROOT");
            if (!string.IsNullOrWhiteSpace(diagnosticRoot))
            {
                var exportedRoot = Path.Combine(diagnosticRoot, "1538-accepted-ui-" + Guid.NewGuid().ToString("N"));
                CopyDirectory(_rootPath, exportedRoot);
                var exportedFiles = new FileSystemManager(exportedRoot, NullLogger<FileSystemManager>.Instance);
                Assert.Equal(await reloadFiles.ReadFileBytesAsync(NpcCoreChangesContract.NpcCorePath),
                    await exportedFiles.ReadFileBytesAsync(NpcCoreChangesContract.NpcCorePath));
                Console.WriteLine("Accepted UI session export: " + exportedRoot);
            }
        }
    }

    private async Task SeedProgressionJourneyAsync()
    {
        CopyDirectory(TestRepoPaths.BaseSessionRoot, _fs.GameSessionPath);
        // The journey owns one NPC, so template journals for other actors are not its baseline.
        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_journals.json", "{\"npcJournals\":[]}");
        await _fs.WriteFileAtomicAsync("game_state/core/game_settings.json", "{\"qteEventsEnabled\":false}");
        await _fs.WriteFileAtomicAsync("game_state/misc/characteristics.json", JsonSerializer.Serialize(Characteristics.All.ToDictionary(name => name, _ => 20), SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        await _fs.WriteFileAtomicAsync("game_state/player/experience.json", "{\"level\":1,\"experience\":0,\"experienceForNextLevel\":100}");
        await _fs.WriteFileAtomicAsync(StorageTransportMoveService.VehiclesPath, MortalVehicleBirthReceiptState.CreateEmptyRoot().ToJsonString());
        var resources = ResourceBootstrapStateBuilder.BuildMortalPlayer(2, 41, 20, 20, 20, 20, 20);
        Assert.True(resources.IsValid, JsonSerializer.Serialize(resources.Issues));
        await _fs.WriteFileAtomicAsync(ResourceMaterializationContract.DefinitionsPath, resources.Definitions!.ToCanonicalJson());
        await _fs.WriteFileAtomicAsync(ResourceMaterializationContract.StatePath, resources.State!.ToCanonicalJson());
        await _fs.WriteFileAtomicAsync(ResourceMaterializationContract.HistoryPath, resources.History!.ToCanonicalJson());
        var authority = await CanonicalResourceOwnerAuthorityComposer.ComposeAsync(resources.Definitions, path => _fs.ReadFileAsync(path), resources.State, resources.History, CanonicalResourceOwnerAuthorityPurpose.FinalAfterImage);
        Assert.True(authority.IsValid, JsonSerializer.Serialize(authority.Issues));
        await _fs.WriteFileAtomicAsync(CanonicalResourceOwnerAuthorityComposer.AuthorityPath, authority.CanonicalAuthorityJson!);
        var map = JsonNode.Parse((await _fs.ReadFileAsync(MortalLocationMaterializationContract.WorldMapPath))!)!.AsObject();
        var projection = MortalStorageAccessProjection.Build(map, null);
        Assert.True(projection.IsValid, JsonSerializer.Serialize(projection.Issues));
        await _fs.WriteFileAtomicAsync(MortalStorageAccessProjection.StatePath, projection.Root!.ToJsonString());
        var location = Assert.Single(map["locations"]!.AsArray())!;
        var actor = MortalActorTestFixtures.CreateActor(ProgressionJourneyActor, "loc_valmont_private_chambers", location["name"]!.GetValue<string>());
        actor["name"] = "Лев Наблюдатель";
        actor["level"] = 1; actor["experience"] = 99; actor["experienceForNextLevel"] = 100;
        actor["teacherProfile"] = new JsonObject { ["canTeach"] = false, ["skills"] = new JsonArray() };
        actor["progressionProfile"] = JsonNode.Parse("""{"schemaVersion":1,"mode":"standard","consequences":{"skills":{"levelsPerAward":2,"allowedAwards":["newSkill","masteryIncrease"]},"characteristics":{"mode":"currentWorld"}}}""");
        await _fs.WriteFileAtomicAsync(NpcCoreChangesContract.NpcCorePath, new JsonObject { ["NPCsInScene"] = new JsonArray(actor) }.ToJsonString());
    }

    private async Task WriteProgressionJourneyOutputsAsync(TurnRequest request, int resultLevel, int resultThreshold)
    {
        var control = request.ProgressionControl!;
        await WriteJsonAsync(ProgressionScheduleService.ReportPath, new { progressionProcessingReport = new
        { sessionId = request.SessionId, requestId = request.RequestId, turnNumber = request.TurnNumber,
          worldCyclesProcessed = control.WorldCyclesAlreadyPendingBeforeTurn, factionCyclesProcessed = control.FactionCyclesAlreadyPendingBeforeTurn,
          newLastWorldSimulationTimeInMinutes = control.LastWorldSimulationTimeInMinutes + control.WorldCyclesAlreadyPendingBeforeTurn * control.WorldCycleMinutes,
          newLastFactionSimulationTimeInMinutes = control.LastFactionSimulationTimeInMinutes + control.FactionCyclesAlreadyPendingBeforeTurn * control.FactionCycleMinutes }});
        var timestamp = DateTime.UtcNow.ToString("o");
        await WriteJsonAsync("game_state/npcs/npc_journals.json", new { NPCJournals = new[] { new
        { npcId = ProgressionJourneyActor, npcName = "Лев Наблюдатель", journalEntries = new[] { new
          { entryId = "progression_journey_" + request.TurnNumber, timestamp,
            description = "Я завершил упражнение и увереннее различаю детали. Я расскажу об этом спокойно, не приписывая себе нового навыка." } } } } });
        var actorReasoning = $$"""
        ## Actor Brain 2.0
        ### Лев Наблюдатель
        - Current location: loc_valmont_private_chambers, остаётся на месте.
        - Situation: Завершил упражнение и отвечает на просьбу об отчёте.
        - Profile inputs: Известный наблюдатель; исходный опыт из принятого профиля, явный стандартный профиль развития.
        - Motivation: Честно сообщить результат наблюдения.
        - Constraints: Не присваивает себе новую способность и не покидает локацию.
        - Thoughts: Я стал внимательнее и могу спокойно рассказать о выполненной работе.
        - Strategy options:
          1. Сообщить результат точно. Benefit: собеседник понимает результат. Risk: отчёт покажется скромным.
          2. Преувеличить достижения. Benefit: привлечь внимание. Risk: потерять доверие.
        - Chosen strategy: Точно сообщить результат упражнения.
        - Rejected alternatives: Преувеличение не подтверждено упражнением.
        - Actions: Рассказывает о завершённом упражнении, оставаясь на месте.
        - State changes: NPCCoreChanges progression добавляет 2 XP, итог level {{resultLevel}}, experience 1, threshold {{resultThreshold}}; NPCJournals добавляет собственную мысль. Награда навыком не положена за один полученный уровень.
        """;
        await WriteJsonAsync("output/narrative_response.json", new { response = $"Лев завершил упражнение и стал опытнее. Его уровень повысился до {resultLevel}.", timestamp });
        await WriteJsonAsync("output/interface_updates.json", new { dialogueOptions = Array.Empty<object>(), timestamp });
        await WriteJsonAsync("output/debug_logs.json", new { timestamp, npcSkillClaims = Array.Empty<object>(), gm_thoughts_markdown = "## NPC Scope\n- Mode: Scene-local\n- Relevant actors: Лев Наблюдатель\n- Why relevant: Завершил упражнение, получает опыт.\n- Actors outside scope: нет\n- Why outside scope: Других NPC нет.\n\n## Reasoning\n- Применён точный переход опыта без награды навыком.\n\n" + actorReasoning });
        await WriteJsonAsync("ready/turn_complete.json", new { sessionId = request.SessionId, requestId = request.RequestId, turnNumber = request.TurnNumber, status = "success", timestamp, filesModified = new[] { NpcCoreChangesContract.NpcCorePath, "game_state/npcs/npc_journals.json", ProgressionScheduleService.ReportPath, "output/narrative_response.json", "output/interface_updates.json", "output/debug_logs.json" } });
    }

    private sealed class ProgressionJourneyInput(FileSystemManager files, bool allowRepair = false) : IConsoleInputSource
    {
        public Exception? RepairFailure { get; set; }
        public bool RepairCompleted { get; set; }
        public bool IsScripted => true;
        public bool KeyAvailable => RepairFailure != null || (!allowRepair && files.FileExists("game_state/control/validation_repair_request.json"));
        public ConsoleKeyInfo ReadKey(bool intercept = true)
        {
            if (RepairFailure != null) throw new InvalidOperationException("Progression repair fixture failed", RepairFailure);
            var repair = files.ResolvePath("game_state/control/validation_repair_request.json");
            if (File.Exists(repair)) throw new InvalidOperationException("Progression journey repair: " + File.ReadAllText(repair));
            return new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false);
        }
        public string? ReadLine() => string.Empty;
        public void AssertCompleted() { }
    }
}
