using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

#if DEBUG
public sealed partial class GameEngineTurnLifecycleTests
{
    [Fact]
    public Task CombinedMortalBirth_OrdinaryDeferAcceptsThroughActualPipeline()
        => RunCombinedMortalBirthAsync(false, null, validateRetiredState: true, validateOrdinaryContinuation: true);

    private async Task AssertCombinedBirthOrdinaryContinuationAsync(
        GameEngineTestRuntime runtime, string artifact, Action<Func<TurnRequest, Task>> setAuthor, Func<int> calls)
    {
        const string npcPath = "game_state/npcs/npc_core.json";
        const string journalPath = "game_state/npcs/npc_journals.json";
        const string storyPath = "stories/mortal_life_2.jsonl";
        const string action = "Прошу у совета один кусочек мела для разметки у переправы. Ничего не беру и не перемещаю.";
        Assert.Equal(42, runtime.GameLoop.TurnNumber);
        Assert.Equal(1, calls());
        Assert.True(runtime.Engine.HasRetiredOrdinaryOriginal);
        var retained = new Dictionary<string, byte[]>();
        foreach (var path in new[] { npcPath, MortalLocationMaterializationContract.WorldMapPath,
            MortalLocationIdentityState.StatePath, "game_state/world/current_location.json",
            MortalStorageAccessProjection.StatePath, "game_state/inventory/items.json",
            "game_state/inventory/item_identity_index.json", StorageTransportMoveService.VehiclesPath,
            "game_state/factions/faction_core.json", "game_state/factions/faction_projects.json" })
        {
            var bytes = await _fs.ReadFileBytesAsync(path);
            Assert.NotNull(bytes);
            retained.Add(path, bytes);
        }
        var core = JsonNode.Parse(retained[npcPath])!;
        var steward = Assert.Single(core["UpdateNPCs"]!.AsArray());
        var stewardId = steward!["NPCId"]!.GetValue<string>();
        var stewardName = steward["name"]!.GetValue<string>();
        var locationId = steward["currentLocationId"]!.GetValue<string>();
        Assert.Equal(42, steward["materialization"]!["materializedAtTurn"]!.GetValue<int>());
        Assert.Empty(steward["inventory"]!.AsArray());
        var priorStory = (await _fs.ReadFileAsync(storyPath))!;
        Assert.Single(priorStory.Split('\n', StringSplitOptions.RemoveEmptyEntries));
        var journalBytesBefore = (await _fs.ReadFileBytesAsync(journalPath))!;
        var journalBefore = JsonNode.Parse(journalBytesBefore)!;
        var scheduleBefore = JsonSerializer.Deserialize<ProgressionScheduleState>(
            (await _fs.ReadFileAsync(ProgressionScheduleService.SchedulePath))!, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed)!;
        Assert.Equal(0, scheduleBefore.PendingWorldCycles);
        Assert.Equal(0, scheduleBefore.PendingFactionCycles);
        JsonNode? expectedJournals = null;
        TurnRequest? actualRequest = null;
        setAuthor(async request =>
        {
            actualRequest = request;
            Assert.Equal(43, request.TurnNumber);
            Assert.Equal("session_mortal_living_world_journey", request.SessionId);
            Assert.Equal(action, request.PlayerAction);
            Assert.NotNull(request.ProgressionControl);
            Assert.Equal(0, request.ProgressionControl!.WorldCyclesAlreadyPendingBeforeTurn);
            Assert.Equal(0, request.ProgressionControl.FactionCyclesAlreadyPendingBeforeTurn);
            var manifest = JsonSerializer.Deserialize<LiveTurnPendingSnapshotManifest>(
                (await _fs.ReadFileAsync(LiveTurnPreparationService.PendingTurnSnapshotManifestPath))!,
                LiveTurnPreparationService.ManifestJsonOptions)!;
            Assert.True(PendingTurnSnapshotAuthority.TryValidateManifestForReaderAuthority(manifest,
                await _fs.ReadFileAsync(PendingTurnSnapshotAuthority.AuthorityPath), LiveTurnPreparationService.ManifestHashJsonOptions,
                m => m.ManifestPayloadHash, (m, h) => m.ManifestPayloadHash = h,
                m => m.SessionId, m => m.RequestId, m => m.TurnNumber, m => m.Files,
                m => m.SnapshotFileHashes, m => m.ClientOwnedValidationHashes, m => m.RollbackBaselineFiles,
                m => m.SourceLabel, m => m.RollbackBackups,
                path => File.Exists(_fs.ResolvePath(path)) ? File.ReadAllBytes(_fs.ResolvePath(path)) : null,
                out var admitted, out var failure), failure);
            Assert.NotNull(admitted);
            Assert.Equal(request.RequestId, admitted!.RequestId);
            Assert.Equal(request.SessionId, admitted.SessionId);
            Assert.Equal(43, admitted.TurnNumber);
            Assert.Equal(request.Timestamp, manifest.RequestTimestamp);
            Assert.Equal(retained[npcPath], await _fs.ReadFileBytesAsync(manifest.Files[npcPath]));
            Assert.Equal(journalBytesBefore, await _fs.ReadFileBytesAsync(manifest.Files[journalPath]));
            Assert.Equal(journalBytesBefore, await _fs.ReadFileBytesAsync(journalPath));
            Assert.Equal(retained[npcPath], await _fs.ReadFileBytesAsync(npcPath));
            Assert.False(_fs.FileExists(ProgressionScheduleService.ReportPath));
            var journals = journalBefore.DeepClone();
            var row = Assert.Single(journals["NPCJournals"]!.AsArray(), j => j!["npcId"]?.GetValue<string>() == stewardId)!;
            row["journalEntries"]!.AsArray().Add(new JsonObject
            {
                ["entryId"] = "steward_chalk_request_deferred_t034_visit43", ["timestamp"] = request.Timestamp,
                ["event"] = "Просьба о меле", ["description"] = "Я записал просьбу о меле и передам её совету. Пока согласия нет, кладовая останется закрытой.",
                ["emotionalImpact"] = "сдержанность", ["relationshipChange"] = "0"
            });
            row["lastJournalNote"] = "Я передам просьбу совету, не выдавая мел без согласия.";
            expectedJournals = journals.DeepClone();
            await _fs.WriteFileAtomicAsync(journalPath, journals.ToJsonString());
            var time = await ReadCombinedJsonAsync(MortalLivingWorldSource.WorldTimePath);
            Assert.Equal(500, time["currentTimeInMinutes"]!.GetValue<int>());
            time["currentTimeInMinutes"] = 505; time["timeOfDay"] = "08:25";
            await _fs.WriteFileAtomicAsync(MortalLivingWorldSource.WorldTimePath, time.ToJsonString());
            var fixedPath = Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient.Tests", "TestData", "mm25-authored-debug-fixed.json");
            var debug = JsonNode.Parse(await File.ReadAllTextAsync(fixedPath))!;
            var reasoning = debug["gm_thoughts_markdown"]!.GetValue<string>()
                .Replace("Распорядитель двора совета", stewardName, StringComparison.Ordinal)
                .Replace("npc_steward_t034_visit42", stewardId, StringComparison.Ordinal)
                .Replace("loc_42ab98676d6f4e258e2250db02f656e4", locationId, StringComparison.Ordinal)
                .Replace("Хозяйственный двор у нижней лестницы", steward["currentLocationName"]!.GetValue<string>(), StringComparison.Ordinal);
            await WriteMortalLivingWorldJourneyOutputAsync(request,
                "Распорядитель записывает просьбу и обещает передать её совету. Кладовая остаётся закрытой; мел не выдан.",
                [journalPath, MortalLivingWorldSource.WorldTimePath], reasoning, omitNoDueProgressionReport: true);
            foreach (var pair in retained) Assert.Equal(pair.Value, await _fs.ReadFileBytesAsync(pair.Key));
        });
        try
        {
            await runtime.Engine.ProcessPlayerTurn(action);
        }
        finally
        {
            await File.WriteAllTextAsync(Path.Combine(artifact, "ordinary43-logger.txt"), _journeyLogger.Describe());
        }
        Assert.NotNull(actualRequest);
        Assert.Equal(2, calls());
        Assert.True(JsonNode.DeepEquals(expectedJournals, await ReadCombinedJsonAsync(journalPath)));
        Assert.True(runtime.GameLoop.TurnNumber == 43, _journeyLogger.Describe());
        var story = (await _fs.ReadFileAsync(storyPath))!;
        Assert.StartsWith(priorStory, story);
        Assert.Equal(2, story.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
        Assert.Contains("мел не выдан", story);
        Assert.Equal(505, (await ReadCombinedJsonAsync(MortalLivingWorldSource.WorldTimePath))["currentTimeInMinutes"]!.GetValue<int>());
        foreach (var pair in retained) Assert.Equal(pair.Value, await _fs.ReadFileBytesAsync(pair.Key));
        foreach (var path in new[] { LiveTurnPreparationService.TurnRequestPath,
            LiveTurnPreparationService.PendingTurnSnapshotManifestPath, PendingTurnSnapshotAuthority.AuthorityPath,
            "game_state/control/validation_repair_request.json", "game_state/control/validation_repair_ready.json",
            "game_state/control/validation_diagnostic_failure_report.json", "ready/turn_complete.json", "ready/turn_error.json",
            ProgressionScheduleService.ReportPath }) Assert.False(_fs.FileExists(path), path);
        Assert.False(await runtime.Engine.ProcessLateTerminalAndIdleTransitionsForCurrentSessionAsync());
        Assert.True(runtime.Engine.HasRetiredOrdinaryOriginal);
        Assert.True(await runtime.Engine.ValidateBeforeOrdinaryPlayerTurnAsync(), _journeyLogger.Describe());
        Assert.Equal(43, runtime.GameLoop.TurnNumber);
        Assert.Equal(2, calls());
        foreach (var pair in retained) Assert.Equal(pair.Value, await _fs.ReadFileBytesAsync(pair.Key));
        var scheduleAfter = JsonSerializer.Deserialize<ProgressionScheduleState>(
            (await _fs.ReadFileAsync(ProgressionScheduleService.SchedulePath))!, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed)!;
        Assert.Equal(505, scheduleAfter.CurrentWorldTimeInMinutes);
        Assert.Equal(scheduleBefore.LastWorldSimulationTimeInMinutes, scheduleAfter.LastWorldSimulationTimeInMinutes);
        Assert.Equal(scheduleBefore.LastFactionSimulationTimeInMinutes, scheduleAfter.LastFactionSimulationTimeInMinutes);
        Assert.Equal(0, scheduleAfter.PendingWorldCycles);
        Assert.Equal(0, scheduleAfter.PendingFactionCycles);
        var snapshotDirectory = _fs.ResolvePath(LiveTurnPreparationService.PendingTurnSnapshotDirectory);
        Assert.True(!Directory.Exists(snapshotDirectory) || !Directory.EnumerateFileSystemEntries(snapshotDirectory).Any());
    }
}
#endif
