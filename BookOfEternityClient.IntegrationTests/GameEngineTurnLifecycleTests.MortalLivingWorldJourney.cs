using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.AgentConsole;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Fact]
    public async Task MortalLivingWorldJourney_ElapsedNoChangeCompletesRealPlayerTurn()
    {
        using var owned = new CleanupOwnedFixture(_rootPath, line => _directGachaOutput?.WriteLine(line));
        var runtime = await CreateMortalLivingWorldJourneyAsync(async request =>
        {
            var time = JsonNode.Parse((await _fs.ReadFileAsync(MortalLivingWorldSource.WorldTimePath))!)!.AsObject();
            var from = time["currentTimeInMinutes"]!.GetValue<long>();
            time["currentTimeInMinutes"] = from + 240;
            time["timeOfDay"] = "12:00";
            await _fs.WriteFileAtomicAsync(MortalLivingWorldSource.WorldTimePath, time.ToJsonString());
            var audit = new JsonArray();
            foreach (var scope in new[] { "npcs", "factions", "locations", "projects", "world" })
                audit.Add(new JsonObject { ["scope"] = scope, ["reason"] = "Проверено текущее состояние за четыре часа: нет значимых автономных изменений в этой области." });
            await _fs.WriteFileAtomicAsync(MortalLivingWorldSource.ReportPath, new JsonObject
            {
                ["schemaVersion"] = 1, ["sessionId"] = request.SessionId, ["requestId"] = request.RequestId,
                ["turn"] = request.TurnNumber, ["fromMinute"] = from, ["toMinute"] = from + 240,
                ["resolution"] = "noSignificantChange", ["audit"] = audit, ["events"] = new JsonArray()
            }.ToJsonString());
            await WriteMortalLivingWorldJourneyOutputAsync(request,
                "Вы проводите четыре спокойных часа в своих покоях. Наступает полдень.",
                [MortalLivingWorldSource.WorldTimePath, MortalLivingWorldSource.ReportPath]);
        });
        await runtime.Engine.ProcessPlayerTurn("Провести четыре часа в покоях, ожидая новостей.");
        if (runtime.GameLoop.TurnNumber != 42)
        {
            var issues = await runtime.ValidationService.ValidateGameStateAsync();
            Assert.True(false, "Turn did not advance: " + _journeyLogger.Describe() + "\n" + JsonSerializer.Serialize(issues));
        }
        Assert.Equal(42, runtime.GameLoop.TurnNumber);
        var story = (await _fs.ReadFileAsync("stories/mortal_life_2.jsonl"))!;
        Assert.False(string.IsNullOrWhiteSpace(story));
        var row = JsonNode.Parse(Assert.Single(story.Split('\n', StringSplitOptions.RemoveEmptyEntries)))!;
        Assert.Equal(42, row["turn"]!.GetValue<int>());
        Assert.Equal(720, JsonNode.Parse((await _fs.ReadFileAsync(MortalLivingWorldSource.WorldTimePath))!)!["currentTimeInMinutes"]!.GetValue<int>());
        var history = JsonNode.Parse((await _fs.ReadFileAsync(MortalLivingWorldSource.HistoryPath))!)!;
        Assert.Single(history["entries"]!.AsArray());
        Assert.False(_fs.FileExists(MortalLivingWorldSource.ReportPath));
        Assert.False(_fs.FileExists("game_state/control/validation_repair_request.json"));
    }

    // A single genuine session is reused across sequential turns; fresh ownership and snapshots
    // are still issued by ProcessPlayerTurn. No mutable fixture or authority cache is introduced.
    private readonly SpiritualLifecycleTestLogger _journeyLogger = new();

    private async Task<GameEngineTestRuntime> CreateMortalLivingWorldJourneyAsync(
        Func<TurnRequest, Task> author, Func<FileSystemManager, Task>? seed = null, IConsoleInputSource? inputOverride = null)
    {
        await MortalLiveSeedBuilder.PrepareAsync(_fs, TestRepoPaths.BaseSessionRoot, seed);
        var input = inputOverride ?? new MortalLivingWorldJourneyInput(_fs);
        var runtime = CreateGameEngineRuntime(_fs, input,
            settings => { InertRealmSettings(settings); settings.AutosaveIntervalTurns = 0; },
            logger: _journeyLogger, finalizationHooks: new GameEngineSessionFinalizationHooks { AtCheckpointAsync = async checkpoint =>
            {
                if (checkpoint != SessionFinalizationCheckpoint.TerminalWaitStarted) return;
                var request = JsonSerializer.Deserialize<TurnRequest>((await _fs.ReadFileAsync("input/turn_request.json"))!,
                    SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed)!;
                await author(request);
            }});
        runtime.GameLoop.SetSession("session_mortal_living_world_journey", 41);
        await _fs.WriteFileAtomicAsync("game_state/history/chat_log.json", new JsonObject
            { ["sessionId"] = "session_mortal_living_world_journey", ["entries"] = new JsonArray() }.ToJsonString());
        await runtime.StateManager.RefreshGameStateAsync();
        return runtime;
    }

    private async Task WriteMortalLivingWorldJourneyOutputAsync(TurnRequest request, string narrative, string[] changedPaths, string? actorReasoning = null)
    {
        var control = request.ProgressionControl!;
        await WriteJsonAsync(ProgressionScheduleService.ReportPath, new { progressionProcessingReport = new
        {
            sessionId = request.SessionId, requestId = request.RequestId, turnNumber = request.TurnNumber,
            worldCyclesProcessed = control.WorldCyclesAlreadyPendingBeforeTurn,
            factionCyclesProcessed = control.FactionCyclesAlreadyPendingBeforeTurn,
            newLastWorldSimulationTimeInMinutes = control.LastWorldSimulationTimeInMinutes + control.WorldCyclesAlreadyPendingBeforeTurn * control.WorldCycleMinutes,
            newLastFactionSimulationTimeInMinutes = control.LastFactionSimulationTimeInMinutes + control.FactionCyclesAlreadyPendingBeforeTurn * control.FactionCycleMinutes
        }});
        changedPaths = changedPaths.Append(ProgressionScheduleService.ReportPath).ToArray();
        var timestamp = DateTime.UtcNow.ToString("o");
        await WriteJsonAsync("output/narrative_response.json", new { response = narrative, timestamp });
        await WriteJsonAsync("output/interface_updates.json", new { dialogueOptions = Array.Empty<object>(), timestamp });
        await WriteJsonAsync("output/debug_logs.json", new { timestamp, npcSkillClaims = Array.Empty<object>(),
            gm_thoughts_markdown = actorReasoning ?? "## NPC Scope\n- Mode: Scene-local\n- Relevant actors: нет\n- Why relevant: Ожидание в покоях.\n- Actors outside scope: нет\n- Why outside scope: Нет присутствующих NPC.\n\n## Reasoning\n- Проверен указанный период времени; навыки NPC не заявлены." });
        await WriteJsonAsync("ready/turn_complete.json", new { sessionId = request.SessionId, requestId = request.RequestId,
            turnNumber = request.TurnNumber, status = "success", timestamp,
            filesModified = changedPaths.Concat(new[] { "output/narrative_response.json", "output/interface_updates.json", "output/debug_logs.json" }).ToArray() });
    }

    private sealed class MortalLivingWorldJourneyInput(FileSystemManager files) : IConsoleInputSource
    {
        public bool IsScripted => true;
        public bool KeyAvailable => files.FileExists("game_state/control/validation_repair_request.json");
        public ConsoleKeyInfo ReadKey(bool intercept = true)
        {
            var repair = files.ResolvePath("game_state/control/validation_repair_request.json");
            if (File.Exists(repair)) throw new InvalidOperationException("Full journey reached unexpected repair: " + File.ReadAllText(repair));
            return new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false);
        }
        public string? ReadLine() => string.Empty;
        public void AssertCompleted() { }
    }
}
