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
    public async Task EnsureInitializedAsync_MissingScheduleAndSoulState_DefersLedgerUntilRealmExists()
    {
        var schedule = await _service.EnsureInitializedAsync();

        Assert.Equal(string.Empty, schedule.CurrentRealm);
        Assert.False(_fs.FileExists(ProgressionScheduleService.SchedulePath));
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
            "residentAgencyCyclesProcessed": 1,
            "newLastChaosSeaSimulationOrdinal": 1,
            "newLastGuardianProjectCycleOrdinal": 1,
            "newLastResidentAgencyCycleOrdinal": 1
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
    public async Task ValidateAcceptedTurnOutcomeAsync_LegacyGuardianOrdinalFallsBackToChaosOrdinal()
    {
        await WriteTurnRequestContextAsync("session_legacy", "req_legacy", 7);
        var control = new ProgressionControl
        {
            CurrentRealm = "Chaos Sea",
            CurrentChaosSeaTurnOrdinal = 6,
            NextChaosSeaTurnOrdinal = 7,
            LastChaosSeaSimulationOrdinal = 6,
            LastGuardianProjectCycleOrdinal = 6,
            NextGuardianProjectCycleOrdinal = 0,
            ChaosSeaCyclesExpectedThisTurn = 1,
            GuardianProjectCyclesExpectedThisTurn = 1
        };

        await _fs.WriteFileAtomicAsync(ProgressionScheduleService.ReportPath, """
        {
          "progressionProcessingReport": {
            "sessionId": "session_legacy",
            "requestId": "req_legacy",
            "turnNumber": 7,
            "worldCyclesProcessed": 0,
            "factionCyclesProcessed": 0,
            "chaosSeaCyclesProcessed": 1,
            "guardianProjectCyclesProcessed": 1,
            "newLastChaosSeaSimulationOrdinal": 7,
            "newLastGuardianProjectCycleOrdinal": 7
          }
        }
        """);

        var issues = await _service.ValidateAcceptedTurnOutcomeAsync(control);

        Assert.DoesNotContain(issues, issue => string.Equals(issue.Code, "progression_report_new_last_guardian_ordinal_mismatch", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);
    }

    [Fact]
    public async Task ApplyAcceptedTurnOutcomeAsync_LegacyGuardianOrdinalFallback_AdvancesGuardianOrdinal()
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
          "currentChaosSeaTurnOrdinal": 6,
          "lastChaosSeaSimulationOrdinal": 6,
          "lastGuardianProjectCycleOrdinal": 6,
          "pendingWorldCycles": 0,
          "pendingFactionCycles": 0,
          "pendingChaosSeaCycles": 1,
          "pendingGuardianProjectCycles": 1,
          "lastUpdatedUtc": "2026-04-21T00:00:00.0000000Z"
        }
        """);
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Асуран",
          "currentRealm": "Chaos Sea"
        }
        """);
        await WriteTurnRequestContextAsync("session_legacy", "req_legacy_apply", 8);

        var control = new ProgressionControl
        {
            CurrentRealm = "Chaos Sea",
            CurrentChaosSeaTurnOrdinal = 6,
            NextChaosSeaTurnOrdinal = 7,
            LastChaosSeaSimulationOrdinal = 6,
            LastGuardianProjectCycleOrdinal = 6,
            NextGuardianProjectCycleOrdinal = 0,
            ChaosSeaCyclesExpectedThisTurn = 1,
            GuardianProjectCyclesExpectedThisTurn = 1
        };
        await _fs.WriteFileAtomicAsync(ProgressionScheduleService.ReportPath, """
        {
          "progressionProcessingReport": {
            "sessionId": "session_legacy",
            "requestId": "req_legacy_apply",
            "turnNumber": 8,
            "worldCyclesProcessed": 0,
            "factionCyclesProcessed": 0,
            "chaosSeaCyclesProcessed": 1,
            "guardianProjectCyclesProcessed": 1,
            "newLastChaosSeaSimulationOrdinal": 7,
            "newLastGuardianProjectCycleOrdinal": 7
          }
        }
        """);

        await _service.ApplyAcceptedTurnOutcomeAsync(control);

        var schedule = await ReadScheduleAsync();
        Assert.Equal(7, schedule.CurrentChaosSeaTurnOrdinal);
        Assert.Equal(7, schedule.LastChaosSeaSimulationOrdinal);
        Assert.Equal(7, schedule.LastGuardianProjectCycleOrdinal);
        Assert.Equal(0, schedule.PendingGuardianProjectCycles);
        Assert.False(_fs.FileExists(ProgressionScheduleService.ReportPath));
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
        Assert.Equal(1, schedule.PendingResidentAgencyCycles);
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
    public async Task BuildControlForNextTurnAsync_EmptyCurrentRealm_FailsClosedWithoutTreatingAsChaosSea()
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
          "currentRealm": ""
        }
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
        Assert.Equal(1, control.ResidentAgencyCyclesExpectedThisTurn);
        Assert.Equal(10, control.NextChaosSeaTurnOrdinal);
        Assert.Equal(9, control.NextGuardianProjectCycleOrdinal);
        Assert.Equal(8, control.NextResidentAgencyCycleOrdinal);
        Assert.True(control.MustEvaluateChaosSeaProgression);
        Assert.True(control.MustEvaluateGuardianProjectProgression);
        Assert.True(control.MustEvaluateResidentAgencyProgression);

        var schedule = await ReadScheduleAsync();
        Assert.Equal(3, schedule.PendingChaosSeaCycles);
        Assert.Equal(2, schedule.PendingGuardianProjectCycles);
        Assert.Equal(1, schedule.PendingResidentAgencyCycles);
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
        Assert.Equal(1, control.ResidentAgencyCyclesExpectedThisTurn);
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
            "residentAgencyCyclesProcessed": 1,
            "newLastChaosSeaSimulationOrdinal": 1,
            "newLastGuardianProjectCycleOrdinal": 1,
            "newLastResidentAgencyCycleOrdinal": 1
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
        Assert.Equal(1, schedule.PendingResidentAgencyCycles);
        Assert.True(_fs.FileExists(ProgressionScheduleService.ReportPath));
    }

    [Fact]
    public async Task ValidateAcceptedTurnOutcomeAsync_LateReadySignalProvidesProgressionCorrelationContext()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Асуран",
          "currentRealm": "Chaos Sea"
        }
        """);
        await WriteTurnRequestContextAsync("session_late", "req_late", 5);

        var control = await _service.BuildControlForNextTurnAsync();
        _fs.DeleteFile("input/turn_request.json");
        await WriteReadySignalContextAsync("session_late", "req_late", 5);
        await _fs.WriteFileAtomicAsync(ProgressionScheduleService.ReportPath, """
        {
          "progressionProcessingReport": {
            "sessionId": "session_late",
            "requestId": "req_late",
            "turnNumber": 5,
            "worldCyclesProcessed": 0,
            "factionCyclesProcessed": 0,
            "chaosSeaCyclesProcessed": 1,
            "guardianProjectCyclesProcessed": 1,
            "residentAgencyCyclesProcessed": 1,
            "newLastChaosSeaSimulationOrdinal": 1,
            "newLastGuardianProjectCycleOrdinal": 1,
            "newLastResidentAgencyCycleOrdinal": 1
          }
        }
        """);

        var issues = await _service.ValidateAcceptedTurnOutcomeAsync(control);

        Assert.DoesNotContain(issues, issue => string.Equals(issue.Code, "progression_report_missing_turn_context", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue => string.Equals(issue.Code, "progression_report_turn_context_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task BuildControlForNextTurnAsync_ShiningAbodeUsesShiningContoursWithoutChaosCycle()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Асуран",
          "currentRealm": "Shining Abode"
        }
        """);
        await _fs.WriteFileAtomicAsync(ShiningAbodeState.StatePath, """
        {
          "availability": "active",
          "preparedIncarnationPackage": null
        }
        """);

        var control = await _service.BuildControlForNextTurnAsync();

        Assert.Equal(0, control.ChaosSeaCyclesExpectedThisTurn);
        Assert.Equal(1, control.GuardianProjectCyclesExpectedThisTurn);
        Assert.Equal(1, control.ResidentAgencyCyclesExpectedThisTurn);
        Assert.Equal(1, control.ShiningAbodeCyclesExpectedThisTurn);
        Assert.Equal(1, control.ShiningFactionCyclesExpectedThisTurn);
        Assert.Equal(1, control.ShiningTradeCyclesExpectedThisTurn);
        Assert.False(control.MustEvaluateChaosSeaProgression);
        Assert.True(control.MustEvaluateShiningAbodeProgression);
        Assert.True(control.MustEvaluateShiningFactionProgression);
        Assert.True(control.MustEvaluateShiningTradeProgression);
    }

    [Fact]
    public async Task BuildControlForNextTurnAsync_AfterlifeIgnoresStaleMortalTimeChange()
    {
        await _fs.WriteFileAtomicAsync(ProgressionScheduleService.SchedulePath, """
        {
          "currentRealm": "Chaos Sea",
          "currentWorldTimeInMinutes": 1000,
          "lastWorldSimulationTimeInMinutes": 1000,
          "lastFactionSimulationTimeInMinutes": 1000,
          "hasAuthoritativeWorldTimeBaseline": true,
          "worldCycleMinutes": 240,
          "factionCycleMinutes": 1440,
          "chaosSeaCycleEquivalentHours": 24,
          "afterlifeCatchupCycleEquivalentMinutes": 1440,
          "lastAfterlifeCatchupWorldTimeInMinutes": 1000,
          "hasAfterlifeCatchupWorldTimeBaseline": true,
          "currentChaosSeaTurnOrdinal": 0,
          "lastChaosSeaSimulationOrdinal": 0,
          "lastGuardianProjectCycleOrdinal": 0,
          "lastResidentAgencyCycleOrdinal": 0,
          "lastShiningAbodeCycleOrdinal": 0,
          "lastShiningFactionCycleOrdinal": 0,
          "lastShiningTradeCycleOrdinal": 0,
          "pendingWorldCycles": 0,
          "pendingFactionCycles": 0,
          "pendingChaosSeaCycles": 0,
          "pendingGuardianProjectCycles": 0,
          "pendingResidentAgencyCycles": 0,
          "pendingShiningAbodeCycles": 0,
          "pendingShiningFactionCycles": 0,
          "pendingShiningTradeCycles": 0,
          "lastUpdatedUtc": "2026-04-21T00:00:00.0000000Z"
        }
        """);
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Асуран",
          "currentRealm": "Chaos Sea"
        }
        """);
        await _fs.WriteFileAtomicAsync("game_state/world/world_time.json", """
        {
          "timeChange": 1440
        }
        """);

        var firstControl = await _service.BuildControlForNextTurnAsync("Chaos Sea");
        var firstSchedule = await ReadScheduleAsync();
        var secondControl = await _service.BuildControlForNextTurnAsync("Chaos Sea");
        var secondSchedule = await ReadScheduleAsync();

        Assert.False(firstControl.AfterlifeCatchupRequired);
        Assert.False(secondControl.AfterlifeCatchupRequired);
        Assert.Equal(1000, firstSchedule.CurrentWorldTimeInMinutes);
        Assert.Equal(1000, secondSchedule.CurrentWorldTimeInMinutes);
    }

    [Fact]
    public async Task BuildControlForNextTurnAsync_MalformedShiningPackageSuppressesOrdinaryShiningProgression()
    {
        await _fs.WriteFileAtomicAsync(ProgressionScheduleService.SchedulePath, """
        {
          "currentRealm": "Shining Abode",
          "currentWorldTimeInMinutes": 0,
          "lastWorldSimulationTimeInMinutes": 0,
          "lastFactionSimulationTimeInMinutes": 0,
          "hasAuthoritativeWorldTimeBaseline": true,
          "worldCycleMinutes": 240,
          "factionCycleMinutes": 1440,
          "chaosSeaCycleEquivalentHours": 24,
          "afterlifeCatchupCycleEquivalentMinutes": 1440,
          "lastAfterlifeCatchupWorldTimeInMinutes": 0,
          "hasAfterlifeCatchupWorldTimeBaseline": true,
          "currentChaosSeaTurnOrdinal": 8,
          "lastChaosSeaSimulationOrdinal": 8,
          "lastGuardianProjectCycleOrdinal": 6,
          "lastResidentAgencyCycleOrdinal": 5,
          "lastShiningAbodeCycleOrdinal": 8,
          "lastShiningFactionCycleOrdinal": 8,
          "lastShiningTradeCycleOrdinal": 8,
          "pendingWorldCycles": 0,
          "pendingFactionCycles": 0,
          "pendingChaosSeaCycles": 4,
          "pendingGuardianProjectCycles": 2,
          "pendingResidentAgencyCycles": 3,
          "pendingShiningAbodeCycles": 4,
          "pendingShiningFactionCycles": 5,
          "pendingShiningTradeCycles": 6,
          "lastUpdatedUtc": "2026-04-21T00:00:00.0000000Z"
        }
        """);
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Асуран",
          "currentRealm": "Shining Abode"
        }
        """);
        await _fs.WriteFileAtomicAsync(ShiningAbodeState.StatePath, """
        {
          "availability": "active",
          "preparedIncarnationPackage":
        """);

        var control = await _service.BuildControlForNextTurnAsync();
        var schedule = await ReadScheduleAsync();

        Assert.Equal(0, control.ChaosSeaCyclesExpectedThisTurn);
        Assert.Equal(0, control.GuardianProjectCyclesExpectedThisTurn);
        Assert.Equal(0, control.ResidentAgencyCyclesExpectedThisTurn);
        Assert.Equal(0, control.ShiningAbodeCyclesExpectedThisTurn);
        Assert.Equal(0, control.ShiningFactionCyclesExpectedThisTurn);
        Assert.Equal(0, control.ShiningTradeCyclesExpectedThisTurn);
        Assert.Equal(8, control.NextChaosSeaTurnOrdinal);
        Assert.Equal(6, control.NextGuardianProjectCycleOrdinal);
        Assert.Equal(5, control.NextResidentAgencyCycleOrdinal);
        Assert.Equal(8, control.NextShiningAbodeCycleOrdinal);
        Assert.Equal(8, control.NextShiningFactionCycleOrdinal);
        Assert.Equal(8, control.NextShiningTradeCycleOrdinal);
        Assert.False(control.MustEvaluateShiningAbodeProgression);
        Assert.Equal(0, schedule.PendingGuardianProjectCycles);
        Assert.Equal(0, schedule.PendingResidentAgencyCycles);
        Assert.Equal(0, schedule.PendingShiningAbodeCycles);
        Assert.Equal(0, schedule.PendingShiningFactionCycles);
        Assert.Equal(0, schedule.PendingShiningTradeCycles);
    }

    [Fact]
    public async Task BuildControlForNextTurnAsync_InvalidShiningPackageObjectSuppressesOrdinaryShiningProgression()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Асуран",
          "currentRealm": "Shining Abode"
        }
        """);
        await _fs.WriteFileAtomicAsync(ShiningAbodeState.StatePath, """
        {
          "availability": "active",
          "preparedIncarnationPackage": {
            "selectedCardIds": ["card_1"],
            "selectedCards": []
          }
        }
        """);

        var control = await _service.BuildControlForNextTurnAsync();

        Assert.Equal(0, control.ChaosSeaCyclesExpectedThisTurn);
        Assert.Equal(0, control.GuardianProjectCyclesExpectedThisTurn);
        Assert.Equal(0, control.ResidentAgencyCyclesExpectedThisTurn);
        Assert.Equal(0, control.ShiningAbodeCyclesExpectedThisTurn);
        Assert.Equal(0, control.ShiningFactionCyclesExpectedThisTurn);
        Assert.Equal(0, control.ShiningTradeCyclesExpectedThisTurn);
        Assert.False(control.MustEvaluateShiningAbodeProgression);
        Assert.False(control.AfterlifeCatchupRequired);
    }

    [Fact]
    public async Task ValidateAcceptedTurnOutcomeAsync_MixedAfterlifeBacklogRequiresPerContourOrdinals()
    {
        await WriteTurnRequestContextAsync("session_mixed", "req_mixed", 12);
        var control = BuildMixedAfterlifeBacklogControl();

        await _fs.WriteFileAtomicAsync(ProgressionScheduleService.ReportPath, """
        {
          "progressionProcessingReport": {
            "sessionId": "session_mixed",
            "requestId": "req_mixed",
            "turnNumber": 12,
            "worldCyclesProcessed": 0,
            "factionCyclesProcessed": 0,
            "chaosSeaCyclesProcessed": 0,
            "guardianProjectCyclesProcessed": 2,
            "residentAgencyCyclesProcessed": 3,
            "shiningAbodeCyclesProcessed": 4,
            "shiningFactionCyclesProcessed": 5,
            "shiningTradeCyclesProcessed": 6,
            "newLastGuardianProjectCycleOrdinal": 10,
            "newLastResidentAgencyCycleOrdinal": 11,
            "newLastShiningAbodeCycleOrdinal": 12,
            "newLastShiningFactionCycleOrdinal": 13,
            "newLastShiningTradeCycleOrdinal": 14
          }
        }
        """);

        var issues = await _service.ValidateAcceptedTurnOutcomeAsync(control);

        Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);

        await _fs.WriteFileAtomicAsync(ProgressionScheduleService.ReportPath, """
        {
          "progressionProcessingReport": {
            "sessionId": "session_mixed",
            "requestId": "req_mixed",
            "turnNumber": 12,
            "worldCyclesProcessed": 0,
            "factionCyclesProcessed": 0,
            "chaosSeaCyclesProcessed": 0,
            "guardianProjectCyclesProcessed": 2,
            "residentAgencyCyclesProcessed": 3,
            "shiningAbodeCyclesProcessed": 4,
            "shiningFactionCyclesProcessed": 5,
            "shiningTradeCyclesProcessed": 6,
            "newLastGuardianProjectCycleOrdinal": 14,
            "newLastResidentAgencyCycleOrdinal": 11,
            "newLastShiningAbodeCycleOrdinal": 12,
            "newLastShiningFactionCycleOrdinal": 13,
            "newLastShiningTradeCycleOrdinal": 14
          }
        }
        """);

        issues = await _service.ValidateAcceptedTurnOutcomeAsync(control);

        Assert.Contains(issues, issue => string.Equals(issue.Code, "progression_report_new_last_guardian_ordinal_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ApplyAcceptedTurnOutcomeAsync_MixedAfterlifeBacklogStoresPerContourOrdinals()
    {
        await _fs.WriteFileAtomicAsync(ProgressionScheduleService.SchedulePath, """
        {
          "currentRealm": "Shining Abode",
          "currentWorldTimeInMinutes": 0,
          "lastWorldSimulationTimeInMinutes": 0,
          "lastFactionSimulationTimeInMinutes": 0,
          "hasAuthoritativeWorldTimeBaseline": true,
          "worldCycleMinutes": 240,
          "factionCycleMinutes": 1440,
          "chaosSeaCycleEquivalentHours": 24,
          "afterlifeCatchupCycleEquivalentMinutes": 1440,
          "lastAfterlifeCatchupWorldTimeInMinutes": 0,
          "hasAfterlifeCatchupWorldTimeBaseline": true,
          "currentChaosSeaTurnOrdinal": 8,
          "lastChaosSeaSimulationOrdinal": 8,
          "lastGuardianProjectCycleOrdinal": 6,
          "lastResidentAgencyCycleOrdinal": 5,
          "lastShiningAbodeCycleOrdinal": 8,
          "lastShiningFactionCycleOrdinal": 8,
          "lastShiningTradeCycleOrdinal": 8,
          "pendingWorldCycles": 0,
          "pendingFactionCycles": 0,
          "pendingChaosSeaCycles": 0,
          "pendingGuardianProjectCycles": 2,
          "pendingResidentAgencyCycles": 3,
          "pendingShiningAbodeCycles": 4,
          "pendingShiningFactionCycles": 5,
          "pendingShiningTradeCycles": 6,
          "lastUpdatedUtc": "2026-04-21T00:00:00.0000000Z"
        }
        """);
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Асуран",
          "currentRealm": "Shining Abode"
        }
        """);
        await WriteTurnRequestContextAsync("session_mixed", "req_mixed_apply", 13);
        var control = BuildMixedAfterlifeBacklogControl();

        await _fs.WriteFileAtomicAsync(ProgressionScheduleService.ReportPath, """
        {
          "progressionProcessingReport": {
            "sessionId": "session_mixed",
            "requestId": "req_mixed_apply",
            "turnNumber": 13,
            "worldCyclesProcessed": 0,
            "factionCyclesProcessed": 0,
            "chaosSeaCyclesProcessed": 0,
            "guardianProjectCyclesProcessed": 2,
            "residentAgencyCyclesProcessed": 3,
            "shiningAbodeCyclesProcessed": 4,
            "shiningFactionCyclesProcessed": 5,
            "shiningTradeCyclesProcessed": 6,
            "newLastGuardianProjectCycleOrdinal": 10,
            "newLastResidentAgencyCycleOrdinal": 11,
            "newLastShiningAbodeCycleOrdinal": 12,
            "newLastShiningFactionCycleOrdinal": 13,
            "newLastShiningTradeCycleOrdinal": 14
          }
        }
        """);

        await _service.ApplyAcceptedTurnOutcomeAsync(control);

        var schedule = await ReadScheduleAsync();
        Assert.Equal(14, schedule.CurrentChaosSeaTurnOrdinal);
        Assert.Equal(8, schedule.LastChaosSeaSimulationOrdinal);
        Assert.Equal(10, schedule.LastGuardianProjectCycleOrdinal);
        Assert.Equal(11, schedule.LastResidentAgencyCycleOrdinal);
        Assert.Equal(12, schedule.LastShiningAbodeCycleOrdinal);
        Assert.Equal(13, schedule.LastShiningFactionCycleOrdinal);
        Assert.Equal(14, schedule.LastShiningTradeCycleOrdinal);
        Assert.Equal(0, schedule.PendingGuardianProjectCycles);
        Assert.Equal(0, schedule.PendingResidentAgencyCycles);
        Assert.Equal(0, schedule.PendingShiningAbodeCycles);
        Assert.Equal(0, schedule.PendingShiningFactionCycles);
        Assert.Equal(0, schedule.PendingShiningTradeCycles);
        Assert.False(_fs.FileExists(ProgressionScheduleService.ReportPath));
    }

    [Fact]
    public async Task BuildControlForNextTurnAsync_ShiningPreparedBootstrapSkipsOrdinaryShiningProgression()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Асуран",
          "currentRealm": "Shining Abode"
        }
        """);
        await _fs.WriteFileAtomicAsync(ShiningAbodeState.StatePath, """
        {
          "availability": "active",
          "preparedIncarnationPackage": {
            "selectedCardIds": ["card_1"],
            "selectedCards": [
              {
                "cardId": "card_1",
                "dedupeKey": "memory:card_1",
                "sourceType": "project",
                "sourceFactionId": "faction_old",
                "sourceActorId": "project_old",
                "effectFamily": "memory",
                "rarity": "common",
                "displayName": "Память",
                "displaySummary": "Сохраняет эхо.",
                "effectPayload": {}
              }
            ]
          }
        }
        """);

        var control = await _service.BuildControlForNextTurnAsync();

        Assert.Equal(0, control.ChaosSeaCyclesExpectedThisTurn);
        Assert.Equal(0, control.GuardianProjectCyclesExpectedThisTurn);
        Assert.Equal(0, control.ResidentAgencyCyclesExpectedThisTurn);
        Assert.Equal(0, control.ShiningAbodeCyclesExpectedThisTurn);
        Assert.Equal(0, control.ShiningFactionCyclesExpectedThisTurn);
        Assert.Equal(0, control.ShiningTradeCyclesExpectedThisTurn);
        Assert.False(control.AfterlifeCatchupRequired);
    }

    [Fact]
    public async Task BuildControlForNextTurnAsync_LegacyMortalSchedulePreservesElapsedTimeForFirstAfterlifeCatchup()
    {
        await _fs.WriteFileAtomicAsync(ProgressionScheduleService.SchedulePath, """
        {
          "currentRealm": "Mortal World",
          "currentWorldTimeInMinutes": 0,
          "lastWorldSimulationTimeInMinutes": 0,
          "lastFactionSimulationTimeInMinutes": 0,
          "hasAuthoritativeWorldTimeBaseline": true,
          "worldCycleMinutes": 240,
          "factionCycleMinutes": 1440,
          "chaosSeaCycleEquivalentHours": 24,
          "afterlifeCatchupCycleEquivalentMinutes": 1440,
          "currentChaosSeaTurnOrdinal": 0,
          "lastChaosSeaSimulationOrdinal": 0,
          "lastGuardianProjectCycleOrdinal": 0,
          "lastResidentAgencyCycleOrdinal": 0,
          "lastShiningAbodeCycleOrdinal": 0,
          "lastShiningFactionCycleOrdinal": 0,
          "lastShiningTradeCycleOrdinal": 0,
          "pendingWorldCycles": 0,
          "pendingFactionCycles": 0,
          "pendingChaosSeaCycles": 0,
          "pendingGuardianProjectCycles": 0,
          "pendingResidentAgencyCycles": 0,
          "pendingShiningAbodeCycles": 0,
          "pendingShiningFactionCycles": 0,
          "pendingShiningTradeCycles": 0,
          "lastUpdatedUtc": "2026-04-21T00:00:00.0000000Z"
        }
        """);
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Асуран",
          "currentRealm": "Chaos Sea"
        }
        """);
        await _fs.WriteFileAtomicAsync("game_state/world/world_time.json", """
        {
          "currentTimeInMinutes": 100000
        }
        """);

        var control = await _service.BuildControlForNextTurnAsync("Chaos Sea");

        Assert.True(control.AfterlifeCatchupRequired);
        Assert.Equal(69, control.AfterlifeCatchupElapsedCycles);
        Assert.Equal("epochal", control.AfterlifeCatchupPressureTier);
        Assert.Equal(5, control.AfterlifeCatchupSummaryEventsRequired);
        Assert.Contains("chaos_sea", control.AfterlifeCatchupContours);
        Assert.Contains("guardian_projects", control.AfterlifeCatchupContours);
        Assert.Contains("residents", control.AfterlifeCatchupContours);

        var schedule = await ReadScheduleAsync();
        Assert.Equal(0, schedule.LastAfterlifeCatchupWorldTimeInMinutes);
        Assert.True(schedule.HasAfterlifeCatchupWorldTimeBaseline);
    }

    [Fact]
    public async Task AfterlifeCatchup_LongMortalAbsenceCollapsesIntoSingleBoundedProofAndDoesNotRepeat()
    {
        await _fs.WriteFileAtomicAsync(ProgressionScheduleService.SchedulePath, """
        {
          "currentRealm": "Mortal World",
          "currentWorldTimeInMinutes": 0,
          "lastWorldSimulationTimeInMinutes": 0,
          "lastFactionSimulationTimeInMinutes": 0,
          "hasAuthoritativeWorldTimeBaseline": true,
          "worldCycleMinutes": 240,
          "factionCycleMinutes": 1440,
          "chaosSeaCycleEquivalentHours": 24,
          "afterlifeCatchupCycleEquivalentMinutes": 1440,
          "lastAfterlifeCatchupWorldTimeInMinutes": 0,
          "hasAfterlifeCatchupWorldTimeBaseline": true,
          "currentChaosSeaTurnOrdinal": 0,
          "lastChaosSeaSimulationOrdinal": 0,
          "lastGuardianProjectCycleOrdinal": 0,
          "lastResidentAgencyCycleOrdinal": 0,
          "lastShiningAbodeCycleOrdinal": 0,
          "lastShiningFactionCycleOrdinal": 0,
          "lastShiningTradeCycleOrdinal": 0,
          "pendingWorldCycles": 0,
          "pendingFactionCycles": 0,
          "pendingChaosSeaCycles": 0,
          "pendingGuardianProjectCycles": 0,
          "pendingResidentAgencyCycles": 0,
          "pendingShiningAbodeCycles": 0,
          "pendingShiningFactionCycles": 0,
          "pendingShiningTradeCycles": 0,
          "lastUpdatedUtc": "2026-04-21T00:00:00.0000000Z"
        }
        """);
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Асуран",
          "currentRealm": "Chaos Sea"
        }
        """);
        await _fs.WriteFileAtomicAsync(ShiningAbodeState.StatePath, """
        {
          "availability": "active",
          "preparedIncarnationPackage": null
        }
        """);
        await _fs.WriteFileAtomicAsync("game_state/world/world_time.json", """
        {
          "currentTimeInMinutes": 100000
        }
        """);
        await WriteTurnRequestContextAsync("session_progression", "req_catchup", 4);

        var control = await _service.BuildControlForNextTurnAsync("Chaos Sea");

        Assert.True(control.AfterlifeCatchupRequired);
        Assert.Equal(69, control.AfterlifeCatchupElapsedCycles);
        Assert.Equal("epochal", control.AfterlifeCatchupPressureTier);
        Assert.Equal(5, control.AfterlifeCatchupSummaryEventsRequired);
        Assert.Contains("shining_abode", control.AfterlifeCatchupContours);
        Assert.Contains("shining_factions", control.AfterlifeCatchupContours);
        Assert.Equal(1, control.ChaosSeaCyclesExpectedThisTurn);
        Assert.Equal(1, control.GuardianProjectCyclesExpectedThisTurn);
        Assert.Equal(1, control.ResidentAgencyCyclesExpectedThisTurn);

        await _fs.WriteFileAtomicAsync(ProgressionScheduleService.ReportPath, """
        {
          "progressionProcessingReport": {
            "sessionId": "session_progression",
            "requestId": "req_catchup",
            "turnNumber": 4,
            "worldCyclesProcessed": 0,
            "factionCyclesProcessed": 0,
            "chaosSeaCyclesProcessed": 1,
            "guardianProjectCyclesProcessed": 1,
            "residentAgencyCyclesProcessed": 1,
            "newLastChaosSeaSimulationOrdinal": 1,
            "newLastGuardianProjectCycleOrdinal": 1,
            "newLastResidentAgencyCycleOrdinal": 1,
            "afterlifeCatchupProcessed": true,
            "afterlifeCatchupSummaryEventsProcessed": 5
          }
        }
        """);

        var issues = await _service.ValidateAcceptedTurnOutcomeAsync(control);
        Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);

        await _service.ApplyAcceptedTurnOutcomeAsync(control);

        var schedule = await ReadScheduleAsync();
        Assert.Equal(100000, schedule.LastAfterlifeCatchupWorldTimeInMinutes);
        Assert.Equal(1, schedule.LastShiningAbodeCycleOrdinal);
        Assert.Equal(1, schedule.LastShiningFactionCycleOrdinal);
        Assert.Equal(1, schedule.LastShiningTradeCycleOrdinal);
        Assert.False(_fs.FileExists(ProgressionScheduleService.ReportPath));

        var nextControl = await _service.BuildControlForNextTurnAsync("Chaos Sea");
        Assert.False(nextControl.AfterlifeCatchupRequired);

        var reenterShiningControl = await _service.BuildControlForNextTurnAsync("Shining Abode");
        Assert.False(reenterShiningControl.AfterlifeCatchupRequired);
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

    private static ProgressionControl BuildMixedAfterlifeBacklogControl()
    {
        return new ProgressionControl
        {
            CurrentRealm = "Shining Abode",
            CurrentChaosSeaTurnOrdinal = 8,
            NextChaosSeaTurnOrdinal = 14,
            LastChaosSeaSimulationOrdinal = 8,
            LastGuardianProjectCycleOrdinal = 6,
            LastResidentAgencyCycleOrdinal = 5,
            LastShiningAbodeCycleOrdinal = 8,
            LastShiningFactionCycleOrdinal = 8,
            LastShiningTradeCycleOrdinal = 8,
            NextGuardianProjectCycleOrdinal = 14,
            NextResidentAgencyCycleOrdinal = 14,
            NextShiningAbodeCycleOrdinal = 14,
            NextShiningFactionCycleOrdinal = 14,
            NextShiningTradeCycleOrdinal = 14,
            ChaosSeaCyclesExpectedThisTurn = 0,
            GuardianProjectCyclesExpectedThisTurn = 2,
            ResidentAgencyCyclesExpectedThisTurn = 3,
            ShiningAbodeCyclesExpectedThisTurn = 4,
            ShiningFactionCyclesExpectedThisTurn = 5,
            ShiningTradeCyclesExpectedThisTurn = 6
        };
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

    private Task WriteReadySignalContextAsync(string sessionId, string requestId, int turnNumber)
    {
        return _fs.WriteFileAtomicAsync("ready/turn_complete.json", $$"""
        {
          "sessionId": "{{sessionId}}",
          "requestId": "{{requestId}}",
          "turnNumber": {{turnNumber}},
          "status": "success",
          "timestamp": "2026-04-23T00:00:01.0000000Z",
          "filesModified": []
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
