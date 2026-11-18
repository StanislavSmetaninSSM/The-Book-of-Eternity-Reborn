using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Fact]
    public async Task MortalLivingWorldNarrativeOmission_ConcreteWorldEventWithoutDeclarationRefusesRealTurn()
    {
        using var owned = new CleanupOwnedFixture(_rootPath, line => _directGachaOutput?.WriteLine(line));
        const string storyPath = "stories/mortal_life_2.jsonl";
        const string mapPath = "game_state/world/world_map.json";
        const string narrative = "За эти четыре часа у ближайшей к усадьбе переправы рухнули опоры. " +
            "Переправа уничтожена и полностью закрыта для движения.";
        var dispatches = 0;
        var runtime = await CreateMortalLivingWorldJourneyAsync(async request =>
        {
            dispatches++;
            var time = JsonNode.Parse((await _fs.ReadFileAsync(MortalLivingWorldSource.WorldTimePath))!)!.AsObject();
            var from = time["currentTimeInMinutes"]!.GetValue<long>();
            var to = from + 240;
            var control = request.ProgressionControl!;
            var context = new MortalLivingWorldAssessmentContext(request.SessionId, request.RequestId,
                request.TurnNumber, from, to, control.LastWorldSimulationTimeInMinutes,
                control.LastFactionSimulationTimeInMinutes, control.WorldCycleMinutes, control.FactionCycleMinutes);
            var reportFrom = from;
            if (to - control.LastWorldSimulationTimeInMinutes >= control.WorldCycleMinutes)
                reportFrom = Math.Min(reportFrom, control.LastWorldSimulationTimeInMinutes);
            if (to - control.LastFactionSimulationTimeInMinutes >= control.FactionCycleMinutes)
                reportFrom = Math.Min(reportFrom, control.LastFactionSimulationTimeInMinutes);
            var audit = new JsonArray();
            foreach (var scope in new[] { "npcs", "factions", "locations", "projects", "world" })
                audit.Add(new JsonObject
                {
                    ["scope"] = scope,
                    ["reason"] = "Проверено текущее состояние за четыре часа: нет значимых автономных изменений в этой области."
                });
            var report = new JsonObject
            {
                ["schemaVersion"] = 1,
                ["sessionId"] = request.SessionId,
                ["requestId"] = request.RequestId,
                ["turn"] = request.TurnNumber,
                ["fromMinute"] = reportFrom,
                ["toMinute"] = to,
                ["resolution"] = "noSignificantChange",
                ["audit"] = audit,
                ["events"] = new JsonArray()
            };
            // Establish the negative's premise: this is a structurally valid,
            // correlated no-change declaration, not a missing or malformed report.
            // Detached validation supplies no source or publication authority.
            Assert.Empty(MortalLivingWorldAssessment.Validate(context, report));
            time["currentTimeInMinutes"] = to;
            time["timeOfDay"] = "12:00";
            await _fs.WriteFileAtomicAsync(MortalLivingWorldSource.WorldTimePath, time.ToJsonString());
            await _fs.WriteFileAtomicAsync(MortalLivingWorldSource.ReportPath, report.ToJsonString());
            // Only the elapsed clock and outputs are authored. There is no map,
            // NPC, faction or project command and no declared event/effect row.
            await WriteMortalLivingWorldJourneyOutputAsync(request, narrative,
                [MortalLivingWorldSource.WorldTimePath, MortalLivingWorldSource.ReportPath]);
        });
        var storyBefore = await _fs.ReadFileBytesAsync(storyPath);
        var historyBefore = await _fs.ReadFileBytesAsync(MortalLivingWorldSource.HistoryPath);
        var mapBefore = await _fs.ReadFileBytesAsync(mapPath);

        await runtime.Engine.ProcessPlayerTurn("Провести четыре часа в покоях, ожидая новостей.");

        Assert.Equal(1, dispatches);
        // Desired refusal, deliberately distinct from declared-effect mismatch:
        // the concrete event exists ONLY in prose. The current explicit-report
        // validator excludes NLP, so acceptance here is the expected causal RED,
        // not permission to implement a new audit system inside this fixture.
        Assert.Equal(41, runtime.GameLoop.TurnNumber);
        Assert.Equal(storyBefore, await _fs.ReadFileBytesAsync(storyPath));
        Assert.Equal(historyBefore, await _fs.ReadFileBytesAsync(MortalLivingWorldSource.HistoryPath));
        Assert.Equal(mapBefore, await _fs.ReadFileBytesAsync(mapPath));
    }
}
