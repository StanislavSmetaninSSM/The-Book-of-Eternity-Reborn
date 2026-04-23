using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ProgressionScheduleServiceTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly ProgressionScheduleService _service;

    public ProgressionScheduleServiceTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-progression-schedule-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);

        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
        _service = new ProgressionScheduleService(_fs, NullLogger<ProgressionScheduleService>.Instance);
    }

    [Fact]
    public async Task BuildControlForNextTurnAsync_DoesNotTreatChaosSubstringRealmAsChaosSea()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Асуран",
          "currentRealm": "Chaos Chamber"
        }
        """);

        var control = await _service.BuildControlForNextTurnAsync();

        Assert.False(control.MustEvaluateChaosSeaProgression);
        Assert.False(control.MustEvaluateGuardianProjectProgression);
        Assert.Equal(0, control.NextChaosSeaTurnOrdinal);
    }

    [Fact]
    public async Task ApplyAcceptedTurnOutcomeAsync_MissingChaosProgressionReport_DoesNotAdvanceOrdinals()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Асуран",
          "currentRealm": "Chaos Sea"
        }
        """);

        var control = await _service.BuildControlForNextTurnAsync();
        Assert.True(control.MustEvaluateChaosSeaProgression);

        await _service.ApplyAcceptedTurnOutcomeAsync(control);

        var schedule = await ReadScheduleAsync();
        Assert.Equal(0, schedule.CurrentChaosSeaTurnOrdinal);
        Assert.Equal(0, schedule.LastChaosSeaSimulationOrdinal);
        Assert.Equal(0, schedule.LastGuardianProjectCycleOrdinal);
    }

    [Fact]
    public async Task ApplyAcceptedTurnOutcomeAsync_ValidChaosProgressionReport_AdvancesOrdinals()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Асуран",
          "currentRealm": "Chaos Sea"
        }
        """);
        await WriteTurnRequestContextAsync("session_progression", "req_progression_valid", 1);

        var control = await _service.BuildControlForNextTurnAsync();
        await _fs.WriteFileAtomicAsync(ProgressionScheduleService.ReportPath, """
        {
          "progressionProcessingReport": {
            "sessionId": "session_progression",
            "requestId": "req_progression_valid",
            "turnNumber": 1,
            "worldCyclesProcessed": 0,
            "factionCyclesProcessed": 0,
            "chaosSeaCyclesProcessed": 1,
            "guardianProjectCyclesProcessed": 1,
            "newLastChaosSeaSimulationOrdinal": 1,
            "newLastGuardianProjectCycleOrdinal": 1
          }
        }
        """);

        await _service.ApplyAcceptedTurnOutcomeAsync(control);

        var schedule = await ReadScheduleAsync();
        Assert.Equal(1, schedule.CurrentChaosSeaTurnOrdinal);
        Assert.Equal(1, schedule.LastChaosSeaSimulationOrdinal);
        Assert.Equal(1, schedule.LastGuardianProjectCycleOrdinal);
    }

    [Fact]
    public async Task ValidateAcceptedTurnOutcomeAsync_MalformedChaosProgressionReport_FailsClosed()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Асуран",
          "currentRealm": "Chaos Sea"
        }
        """);

        var control = await _service.BuildControlForNextTurnAsync();
        await _fs.WriteFileAtomicAsync(ProgressionScheduleService.ReportPath, """
        {
          "progressionProcessingReport": {
            "chaosSeaCyclesProcessed": "oops"
        """);

        var issues = await _service.ValidateAcceptedTurnOutcomeAsync(control);

        Assert.Contains(issues, issue => string.Equals(issue.Code, "progression_report_malformed_for_required_chaos_progression", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ApplyAcceptedTurnOutcomeAsync_MalformedChaosProgressionReport_PreservesPendingCyclesAndReport()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Асуран",
          "currentRealm": "Chaos Sea"
        }
        """);

        var control = await _service.BuildControlForNextTurnAsync();
        await _fs.WriteFileAtomicAsync(ProgressionScheduleService.ReportPath, """
        {
          "progressionProcessingReport": {
            "chaosSeaCyclesProcessed":
        """);

        await _service.ApplyAcceptedTurnOutcomeAsync(control);

        var schedule = await ReadScheduleAsync();
        Assert.Equal(0, schedule.CurrentChaosSeaTurnOrdinal);
        Assert.Equal(1, schedule.PendingChaosSeaCycles);
        Assert.Equal(1, schedule.PendingGuardianProjectCycles);
        Assert.True(_fs.FileExists(ProgressionScheduleService.ReportPath));
    }

    [Fact]
    public async Task BuildControlForNextTurnAsync_MalformedSchedule_FailsClosed()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Асуран",
          "currentRealm": "Chaos Sea"
        }
        """);
        await _fs.WriteFileAtomicAsync(ProgressionScheduleService.SchedulePath, """
        {
          "currentRealm": "Chaos Sea",
        """);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.BuildControlForNextTurnAsync());

        Assert.Contains("progression_schedule.json", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BuildControlForNextTurnAsync_UnreadableCurrentRealm_FailsClosedWithoutMutatingExistingLedger()
    {
        await _fs.WriteFileAtomicAsync(ProgressionScheduleService.SchedulePath, """
        {
          "currentRealm": "Chaos Sea",
          "currentWorldTimeInMinutes": 0,
          "lastWorldSimulationTimeInMinutes": 0,
          "lastFactionSimulationTimeInMinutes": 0,
          "hasAuthoritativeWorldTimeBaseline": true,
          "worldCycleMinutes": 240,
          "factionCycleMinutes": 1440,
          "chaosSeaCycleEquivalentHours": 24,
          "currentChaosSeaTurnOrdinal": 7,
          "lastChaosSeaSimulationOrdinal": 6,
          "lastGuardianProjectCycleOrdinal": 5,
          "pendingWorldCycles": 0,
          "pendingFactionCycles": 0,
          "pendingChaosSeaCycles": 3,
          "pendingGuardianProjectCycles": 2,
          "lastUpdatedUtc": "2026-04-21T00:00:00.0000000Z"
        }
        """);
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Асуран",
          "currentRealm":
        """);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.BuildControlForNextTurnAsync());

        Assert.Contains("currentRealm", exception.Message, StringComparison.OrdinalIgnoreCase);

        var schedule = await ReadScheduleAsync();
        Assert.Equal("Chaos Sea", schedule.CurrentRealm);
        Assert.Equal(3, schedule.PendingChaosSeaCycles);
        Assert.Equal(2, schedule.PendingGuardianProjectCycles);
        Assert.Equal(7, schedule.CurrentChaosSeaTurnOrdinal);
    }

    [Fact]
    public async Task ApplyAcceptedTurnOutcomeAsync_UnreadableCurrentRealmAfterTurn_PreservesRealmAndPendingLedger()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Асуран",
          "currentRealm": "Chaos Sea"
        }
        """);

        var control = await _service.BuildControlForNextTurnAsync();

        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Асуран",
          "currentRealm":
        """);

        await _service.ApplyAcceptedTurnOutcomeAsync(control);

        var schedule = await ReadScheduleAsync();
        Assert.Equal("Chaos Sea", schedule.CurrentRealm);
        Assert.Equal(1, schedule.PendingChaosSeaCycles);
        Assert.Equal(1, schedule.PendingGuardianProjectCycles);
        Assert.Equal(0, schedule.CurrentChaosSeaTurnOrdinal);
    }

    [Fact]
    public async Task ValidateAcceptedTurnOutcomeAsync_UnresolvedCurrentRealm_FailsClosed()
    {
        var issues = await _service.ValidateAcceptedTurnOutcomeAsync(new ProgressionControl
        {
            CurrentRealm = "",
            ChaosSeaCyclesExpectedThisTurn = 1,
            GuardianProjectCyclesExpectedThisTurn = 1
        });

        Assert.Contains(issues, issue => string.Equals(issue.Code, "progression_control_unresolved_current_realm", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task BuildControlForNextTurnAsync_PreservesAccumulatedChaosBacklog()
    {
        await _fs.WriteFileAtomicAsync(ProgressionScheduleService.SchedulePath, """
        {
          "currentRealm": "Chaos Sea",
          "currentWorldTimeInMinutes": 0,
          "lastWorldSimulationTimeInMinutes": 0,
          "lastFactionSimulationTimeInMinutes": 0,
          "hasAuthoritativeWorldTimeBaseline": true,
          "worldCycleMinutes": 240,
          "factionCycleMinutes": 1440,
          "chaosSeaCycleEquivalentHours": 24,
          "currentChaosSeaTurnOrdinal": 7,
          "lastChaosSeaSimulationOrdinal": 6,
          "lastGuardianProjectCycleOrdinal": 5,
          "pendingWorldCycles": 0,
          "pendingFactionCycles": 0,
          "pendingChaosSeaCycles": 3,
          "pendingGuardianProjectCycles": 2,
          "lastUpdatedUtc": "2026-04-21T00:00:00.0000000Z"
        }
        """);
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Асуран",
          "currentRealm": "Chaos Sea"
        }
        """);

        var control = await _service.BuildControlForNextTurnAsync();

        Assert.Equal(3, control.ChaosSeaCyclesExpectedThisTurn);
        Assert.Equal(2, control.GuardianProjectCyclesExpectedThisTurn);
        Assert.True(control.MustEvaluateChaosSeaProgression);
        Assert.True(control.MustEvaluateGuardianProjectProgression);

        var schedule = await ReadScheduleAsync();
        Assert.Equal(3, schedule.PendingChaosSeaCycles);
        Assert.Equal(2, schedule.PendingGuardianProjectCycles);
    }

    [Fact]
    public async Task BuildControlForNextTurnAsync_ZeroChaosBacklogStillCreatesSingleCycle()
    {
        await _fs.WriteFileAtomicAsync(ProgressionScheduleService.SchedulePath, """
        {
          "currentRealm": "Chaos Sea",
          "currentWorldTimeInMinutes": 0,
          "lastWorldSimulationTimeInMinutes": 0,
          "lastFactionSimulationTimeInMinutes": 0,
          "hasAuthoritativeWorldTimeBaseline": true,
          "worldCycleMinutes": 240,
          "factionCycleMinutes": 1440,
          "chaosSeaCycleEquivalentHours": 24,
          "currentChaosSeaTurnOrdinal": 7,
          "lastChaosSeaSimulationOrdinal": 6,
          "lastGuardianProjectCycleOrdinal": 5,
          "pendingWorldCycles": 0,
          "pendingFactionCycles": 0,
          "pendingChaosSeaCycles": 0,
          "pendingGuardianProjectCycles": 0,
          "lastUpdatedUtc": "2026-04-21T00:00:00.0000000Z"
        }
        """);
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Асуран",
          "currentRealm": "Chaos Sea"
        }
        """);

        var control = await _service.BuildControlForNextTurnAsync();

        Assert.Equal(1, control.ChaosSeaCyclesExpectedThisTurn);
        Assert.Equal(1, control.GuardianProjectCyclesExpectedThisTurn);
    }

    [Fact]
    public async Task ApplyAcceptedTurnOutcomeAsync_StaleChaosProgressionReport_DoesNotAdvanceOrdinals()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Асуран",
          "currentRealm": "Chaos Sea"
        }
        """);
        await WriteTurnRequestContextAsync("session_progression", "req_progression_current", 3);

        var control = await _service.BuildControlForNextTurnAsync();
        await _fs.WriteFileAtomicAsync(ProgressionScheduleService.ReportPath, """
        {
          "progressionProcessingReport": {
            "sessionId": "session_progression",
            "requestId": "req_progression_stale",
            "turnNumber": 2,
            "worldCyclesProcessed": 0,
            "factionCyclesProcessed": 0,
            "chaosSeaCyclesProcessed": 1,
            "guardianProjectCyclesProcessed": 1,
            "newLastChaosSeaSimulationOrdinal": 1,
            "newLastGuardianProjectCycleOrdinal": 1
          }
        }
        """);

        var issues = await _service.ValidateAcceptedTurnOutcomeAsync(control);
        Assert.Contains(issues, issue => string.Equals(issue.Code, "progression_report_turn_context_mismatch", StringComparison.OrdinalIgnoreCase));

        await _service.ApplyAcceptedTurnOutcomeAsync(control);

        var schedule = await ReadScheduleAsync();
        Assert.Equal(0, schedule.CurrentChaosSeaTurnOrdinal);
        Assert.Equal(1, schedule.PendingChaosSeaCycles);
        Assert.Equal(1, schedule.PendingGuardianProjectCycles);
        Assert.True(_fs.FileExists(ProgressionScheduleService.ReportPath));
    }

    private async Task<ProgressionScheduleState> ReadScheduleAsync()
    {
        var json = await _fs.ReadFileAsync(ProgressionScheduleService.SchedulePath);
        Assert.False(string.IsNullOrWhiteSpace(json));

        var schedule = JsonSerializer.Deserialize<ProgressionScheduleState>(json!, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.NotNull(schedule);
        return schedule!;
    }

    private Task WriteTurnRequestContextAsync(string sessionId, string requestId, int turnNumber)
    {
        return _fs.WriteFileAtomicAsync("input/turn_request.json", $$"""
        {
          "sessionId": "{{sessionId}}",
          "requestId": "{{requestId}}",
          "turnNumber": {{turnNumber}},
          "playerAction": "test progression",
          "timestamp": "2026-04-23T00:00:00.0000000Z",
          "gameMode": "normal"
        }
        """);
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
