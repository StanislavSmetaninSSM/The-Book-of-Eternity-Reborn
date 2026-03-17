using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using Microsoft.Extensions.Logging;

namespace BookOfEternityClient.Services;

/// <summary>
/// Client-authoritative scheduler for world, faction, and Chaos Sea progression.
/// The GM decides outcomes, but the client decides when progression must be processed.
/// </summary>
public class ProgressionScheduleService
{
    public const string SchedulePath = "game_state/control/progression_schedule.json";
    public const string ReportPath = "game_state/control/progression_report.json";

    private readonly FileSystemManager _fs;
    private readonly ILogger<ProgressionScheduleService> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public ProgressionScheduleService(FileSystemManager fs, ILogger<ProgressionScheduleService> logger)
    {
        _fs = fs;
        _logger = logger;
    }

    public async Task<ProgressionScheduleState> EnsureInitializedAsync()
    {
        var existing = await ReadScheduleAsync();
        if (existing != null)
        {
            var sanitized = await SanitizeScheduleAsync(existing);
            if (!SchedulesEqual(existing, sanitized))
                await WriteScheduleAsync(sanitized);
            return sanitized;
        }

        var realm = await ResolveCurrentRealmAsync(string.Empty);
        var worldTimeResolution = await ResolveWorldTimeFromFileAsync(0);
        var currentWorldTime = worldTimeResolution.Minutes;
        var schedule = new ProgressionScheduleState
        {
            CurrentRealm = realm,
            CurrentWorldTimeInMinutes = currentWorldTime,
            LastWorldSimulationTimeInMinutes = currentWorldTime,
            LastFactionSimulationTimeInMinutes = currentWorldTime,
            HasAuthoritativeWorldTimeBaseline = !worldTimeResolution.HasUnresolvedAbsoluteOverride,
            WorldCycleMinutes = 240,
            FactionCycleMinutes = 1440,
            ChaosSeaCycleEquivalentHours = 24,
            CurrentChaosSeaTurnOrdinal = 0,
            LastChaosSeaSimulationOrdinal = 0,
            LastGuardianProjectCycleOrdinal = 0,
            PendingWorldCycles = 0,
            PendingFactionCycles = 0,
            PendingChaosSeaCycles = 0,
            PendingGuardianProjectCycles = 0,
            LastUpdatedUtc = DateTime.UtcNow.ToString("o")
        };

        await WriteScheduleAsync(schedule);
        return schedule;
    }

    public async Task<ProgressionControl> BuildControlForNextTurnAsync(string? activeTurnRealm = null)
    {
        var schedule = await EnsureInitializedAsync();
        schedule = await SanitizeScheduleAsync(schedule, activeTurnRealm);

        if (!HasResolvedRealm(schedule.CurrentRealm))
        {
            schedule.PendingWorldCycles = 0;
            schedule.PendingFactionCycles = 0;
            schedule.PendingChaosSeaCycles = 0;
            schedule.PendingGuardianProjectCycles = 0;
            schedule.LastUpdatedUtc = DateTime.UtcNow.ToString("o");
            await WriteScheduleAsync(schedule);

            return new ProgressionControl
            {
                CurrentRealm = schedule.CurrentRealm,
                CurrentWorldTimeInMinutes = schedule.CurrentWorldTimeInMinutes,
                LastWorldSimulationTimeInMinutes = schedule.LastWorldSimulationTimeInMinutes,
                LastFactionSimulationTimeInMinutes = schedule.LastFactionSimulationTimeInMinutes,
                WorldCycleMinutes = schedule.WorldCycleMinutes,
                FactionCycleMinutes = schedule.FactionCycleMinutes,
                WorldCyclesAlreadyPendingBeforeTurn = 0,
                FactionCyclesAlreadyPendingBeforeTurn = 0,
                MustEvaluateWorldProgression = false,
                MustEvaluateFactionProgression = false,
                CurrentChaosSeaTurnOrdinal = schedule.CurrentChaosSeaTurnOrdinal,
                NextChaosSeaTurnOrdinal = schedule.CurrentChaosSeaTurnOrdinal,
                LastChaosSeaSimulationOrdinal = schedule.LastChaosSeaSimulationOrdinal,
                LastGuardianProjectCycleOrdinal = schedule.LastGuardianProjectCycleOrdinal,
                ChaosSeaCycleEquivalentHours = schedule.ChaosSeaCycleEquivalentHours,
                ChaosSeaCyclesExpectedThisTurn = 0,
                GuardianProjectCyclesExpectedThisTurn = 0,
                MustEvaluateChaosSeaProgression = false,
                MustEvaluateGuardianProjectProgression = false
            };
        }

        if (IsChaosSea(schedule.CurrentRealm))
        {
            schedule.PendingWorldCycles = 0;
            schedule.PendingFactionCycles = 0;
            schedule.PendingChaosSeaCycles = 1;
            schedule.PendingGuardianProjectCycles = 1;
            schedule.LastUpdatedUtc = DateTime.UtcNow.ToString("o");
            await WriteScheduleAsync(schedule);

            return new ProgressionControl
            {
                CurrentRealm = schedule.CurrentRealm,
                CurrentWorldTimeInMinutes = schedule.CurrentWorldTimeInMinutes,
                LastWorldSimulationTimeInMinutes = schedule.LastWorldSimulationTimeInMinutes,
                LastFactionSimulationTimeInMinutes = schedule.LastFactionSimulationTimeInMinutes,
                WorldCycleMinutes = schedule.WorldCycleMinutes,
                FactionCycleMinutes = schedule.FactionCycleMinutes,
                WorldCyclesAlreadyPendingBeforeTurn = 0,
                FactionCyclesAlreadyPendingBeforeTurn = 0,
                MustEvaluateWorldProgression = false,
                MustEvaluateFactionProgression = false,
                CurrentChaosSeaTurnOrdinal = schedule.CurrentChaosSeaTurnOrdinal,
                NextChaosSeaTurnOrdinal = schedule.CurrentChaosSeaTurnOrdinal + 1,
                LastChaosSeaSimulationOrdinal = schedule.LastChaosSeaSimulationOrdinal,
                LastGuardianProjectCycleOrdinal = schedule.LastGuardianProjectCycleOrdinal,
                ChaosSeaCycleEquivalentHours = schedule.ChaosSeaCycleEquivalentHours,
                ChaosSeaCyclesExpectedThisTurn = schedule.PendingChaosSeaCycles,
                GuardianProjectCyclesExpectedThisTurn = schedule.PendingGuardianProjectCycles,
                MustEvaluateChaosSeaProgression = schedule.PendingChaosSeaCycles > 0,
                MustEvaluateGuardianProjectProgression = schedule.PendingGuardianProjectCycles > 0
            };
        }

        var worldTimeResolution = await ResolveWorldTimeFromFileAsync(schedule.CurrentWorldTimeInMinutes);
        schedule.CurrentWorldTimeInMinutes = worldTimeResolution.Minutes;
        if (worldTimeResolution.HasUnresolvedAbsoluteOverride)
        {
            schedule.PendingWorldCycles = 0;
            schedule.PendingFactionCycles = 0;
            _logger.LogDebug("Progression scheduler skipped Mortal World cycle expectations because authoritative world time could not be resolved safely for this turn.");
        }
        else if (!schedule.HasAuthoritativeWorldTimeBaseline)
        {
            schedule.LastWorldSimulationTimeInMinutes = schedule.CurrentWorldTimeInMinutes;
            schedule.LastFactionSimulationTimeInMinutes = schedule.CurrentWorldTimeInMinutes;
            schedule.PendingWorldCycles = 0;
            schedule.PendingFactionCycles = 0;
            schedule.HasAuthoritativeWorldTimeBaseline = true;
            _logger.LogDebug("Progression scheduler seeded authoritative Mortal World baseline after previously unresolved world_time.json.");
        }
        else
        {
            schedule.PendingWorldCycles = ComputeElapsedCycles(
                schedule.CurrentWorldTimeInMinutes,
                schedule.LastWorldSimulationTimeInMinutes,
                schedule.WorldCycleMinutes);
            schedule.PendingFactionCycles = ComputeElapsedCycles(
                schedule.CurrentWorldTimeInMinutes,
                schedule.LastFactionSimulationTimeInMinutes,
                schedule.FactionCycleMinutes);
        }
        schedule.PendingChaosSeaCycles = 0;
        schedule.PendingGuardianProjectCycles = 0;
        schedule.LastUpdatedUtc = DateTime.UtcNow.ToString("o");
        await WriteScheduleAsync(schedule);

        return new ProgressionControl
        {
            CurrentRealm = schedule.CurrentRealm,
            CurrentWorldTimeInMinutes = schedule.CurrentWorldTimeInMinutes,
            LastWorldSimulationTimeInMinutes = schedule.LastWorldSimulationTimeInMinutes,
            LastFactionSimulationTimeInMinutes = schedule.LastFactionSimulationTimeInMinutes,
            WorldCycleMinutes = schedule.WorldCycleMinutes,
            FactionCycleMinutes = schedule.FactionCycleMinutes,
            WorldCyclesAlreadyPendingBeforeTurn = schedule.PendingWorldCycles,
            FactionCyclesAlreadyPendingBeforeTurn = schedule.PendingFactionCycles,
            MustEvaluateWorldProgression = schedule.PendingWorldCycles > 0,
            MustEvaluateFactionProgression = schedule.PendingFactionCycles > 0,
            CurrentChaosSeaTurnOrdinal = schedule.CurrentChaosSeaTurnOrdinal,
            NextChaosSeaTurnOrdinal = schedule.CurrentChaosSeaTurnOrdinal,
            LastChaosSeaSimulationOrdinal = schedule.LastChaosSeaSimulationOrdinal,
            LastGuardianProjectCycleOrdinal = schedule.LastGuardianProjectCycleOrdinal,
            ChaosSeaCycleEquivalentHours = schedule.ChaosSeaCycleEquivalentHours,
            ChaosSeaCyclesExpectedThisTurn = 0,
            GuardianProjectCyclesExpectedThisTurn = 0,
            MustEvaluateChaosSeaProgression = false,
            MustEvaluateGuardianProjectProgression = false
        };
    }

    public async Task<List<ValidationIssue>> ValidateAcceptedTurnOutcomeAsync(ProgressionControl? control)
    {
        var issues = new List<ValidationIssue>();
        if (control == null)
            return issues;

        if (!HasResolvedRealm(control.CurrentRealm))
            return issues;

        var reportSnapshot = await ReadProcessingReportSnapshotAsync();
        var report = reportSnapshot.Report;
        var suppressMissingReportIssue = report == null && reportSnapshot.FilePresent;
        if (IsChaosSea(control.CurrentRealm))
        {
            ValidateChaosSeaOutcome(control, report, issues, suppressMissingReportIssue);
        }
        else
        {
            var worldTimeResolution = await ResolveWorldTimeFromFileAsync(control.CurrentWorldTimeInMinutes);
            if (worldTimeResolution.HasUnresolvedAbsoluteOverride)
            {
                _logger.LogDebug("Skipping Mortal World progression outcome validation because authoritative world time could not be resolved safely for this turn.");
            }
            else
            {
                ValidateMortalOutcome(control, report, worldTimeResolution.Minutes, issues, suppressMissingReportIssue);
            }
        }

        return issues;
    }

    public async Task ApplyAcceptedTurnOutcomeAsync(ProgressionControl? control)
    {
        if (control == null)
        {
            await DeleteTransientReportAsync();
            return;
        }

        var schedule = await EnsureInitializedAsync();
        var realmAfterTurn = await ResolveCurrentRealmAsync(string.Empty);
        var report = await ReadProcessingReportAsync();

        if (!HasResolvedRealm(control.CurrentRealm))
        {
            schedule.PendingWorldCycles = 0;
            schedule.PendingFactionCycles = 0;
            schedule.PendingChaosSeaCycles = 0;
            schedule.PendingGuardianProjectCycles = 0;
        }
        else if (IsChaosSea(control.CurrentRealm))
        {
            schedule.CurrentChaosSeaTurnOrdinal = control.NextChaosSeaTurnOrdinal;

            if ((report?.ChaosSeaCyclesProcessed ?? 0) > 0)
                schedule.LastChaosSeaSimulationOrdinal = report?.NewLastChaosSeaSimulationOrdinal ?? control.NextChaosSeaTurnOrdinal;
            if ((report?.GuardianProjectCyclesProcessed ?? 0) > 0)
                schedule.LastGuardianProjectCycleOrdinal = report?.NewLastGuardianProjectCycleOrdinal ?? control.NextChaosSeaTurnOrdinal;

            schedule.PendingWorldCycles = 0;
            schedule.PendingFactionCycles = 0;
            schedule.PendingChaosSeaCycles = 0;
            schedule.PendingGuardianProjectCycles = 0;
        }
        else
        {
            var worldTimeResolution = await ResolveWorldTimeFromFileAsync(control.CurrentWorldTimeInMinutes);
            var resultingWorldTime = worldTimeResolution.Minutes;
            if (worldTimeResolution.HasUnresolvedAbsoluteOverride)
            {
                resultingWorldTime = Math.Max(
                    resultingWorldTime,
                    Math.Max(
                        report?.NewLastWorldSimulationTimeInMinutes ?? control.LastWorldSimulationTimeInMinutes,
                        report?.NewLastFactionSimulationTimeInMinutes ?? control.LastFactionSimulationTimeInMinutes));
            }

            schedule.CurrentWorldTimeInMinutes = resultingWorldTime;

            if ((report?.WorldCyclesProcessed ?? 0) > 0)
            {
                schedule.LastWorldSimulationTimeInMinutes =
                    report?.NewLastWorldSimulationTimeInMinutes
                    ?? (control.LastWorldSimulationTimeInMinutes + (report?.WorldCyclesProcessed ?? 0) * control.WorldCycleMinutes);
            }

            if ((report?.FactionCyclesProcessed ?? 0) > 0)
            {
                schedule.LastFactionSimulationTimeInMinutes =
                    report?.NewLastFactionSimulationTimeInMinutes
                    ?? (control.LastFactionSimulationTimeInMinutes + (report?.FactionCyclesProcessed ?? 0) * control.FactionCycleMinutes);
            }

            schedule.PendingWorldCycles = 0;
            schedule.PendingFactionCycles = 0;
            schedule.PendingChaosSeaCycles = 0;
            schedule.PendingGuardianProjectCycles = 0;
        }

        schedule.CurrentRealm = realmAfterTurn;
        schedule.LastUpdatedUtc = DateTime.UtcNow.ToString("o");

        await WriteScheduleAsync(schedule);
        await DeleteTransientReportAsync();
    }

    public Task DeleteTransientReportAsync()
    {
        if (_fs.FileExists(ReportPath))
            _fs.DeleteFile(ReportPath);
        return Task.CompletedTask;
    }

    private void ValidateMortalOutcome(ProgressionControl control, ProgressionProcessingReport? report,
        int resultingWorldTime, List<ValidationIssue> issues, bool suppressMissingReportIssue)
    {
        var expectedWorldCycles = Math.Max(0, control.WorldCyclesAlreadyPendingBeforeTurn);
        var expectedFactionCycles = Math.Max(0, control.FactionCyclesAlreadyPendingBeforeTurn);

        if (report == null)
        {
            if (!suppressMissingReportIssue && (expectedWorldCycles > 0 || expectedFactionCycles > 0))
            {
                issues.Add(BuildMissingProgressionReportIssue(
                    "progression_report_missing_for_required_mortal_progression",
                    "world/faction progression was expected for this Mortal World turn",
                    "Создай progressionProcessingReport в game_state/control/progression_report.json и укажи точные processed cycle counts и новые last-* markers для этого Mortal World turn."));
            }
            return;
        }

        if ((report.ChaosSeaCyclesProcessed ?? 0) != 0 || (report.GuardianProjectCyclesProcessed ?? 0) != 0)
        {
            issues.Add(BuildForbiddenProgressionFieldIssue(
                "progression_report_forbidden_afterlife_fields_in_mortal",
                "chaosSeaCyclesProcessed / guardianProjectCyclesProcessed",
                "0 for both afterlife-only fields",
                $"{report.ChaosSeaCyclesProcessed ?? 0} / {report.GuardianProjectCyclesProcessed ?? 0}",
                "В Mortal World не указывай afterlife progression fields. Оставь только world/faction processed counts и их new last-* markers."));
        }

        if ((report.WorldCyclesProcessed ?? 0) != expectedWorldCycles)
        {
            if (report.WorldCyclesProcessed == null)
            {
                issues.Add(new ValidationIssue(ReportPath, IssueSeverity.Error,
                    "progressionProcessingReport не содержит обязательное поле worldCyclesProcessed",
                    code: "progression_report_missing_world_cycles_processed",
                    section: "ProgressionReport",
                    expected: expectedWorldCycles.ToString(),
                    actual: "missing",
                    repairHint: "Добавь worldCyclesProcessed в progressionProcessingReport и укажи фактически обработанное число мировых циклов для этого хода."));
            }
            else
            {
                issues.Add(BuildProgressionMismatchIssue(
                    "progression_report_world_cycles_processed_mismatch",
                    "worldCyclesProcessed",
                    expectedWorldCycles,
                    report.WorldCyclesProcessed ?? 0,
                    "Исправь worldCyclesProcessed в progressionProcessingReport, чтобы он отражал точное число world cycles, которые клиент ожидал для этого хода."));
            }
        }

        if ((report.FactionCyclesProcessed ?? 0) != expectedFactionCycles)
        {
            if (report.FactionCyclesProcessed == null)
            {
                issues.Add(new ValidationIssue(ReportPath, IssueSeverity.Error,
                    "progressionProcessingReport не содержит обязательное поле factionCyclesProcessed",
                    code: "progression_report_missing_faction_cycles_processed",
                    section: "ProgressionReport",
                    expected: expectedFactionCycles.ToString(),
                    actual: "missing",
                    repairHint: "Добавь factionCyclesProcessed в progressionProcessingReport и укажи фактически обработанное число фракционных циклов для этого хода."));
            }
            else
            {
                issues.Add(BuildProgressionMismatchIssue(
                    "progression_report_faction_cycles_processed_mismatch",
                    "factionCyclesProcessed",
                    expectedFactionCycles,
                    report.FactionCyclesProcessed ?? 0,
                    "Исправь factionCyclesProcessed в progressionProcessingReport, чтобы он отражал точное число faction cycles, которые клиент ожидал для этого хода."));
            }
        }

        if (expectedWorldCycles > 0)
        {
            var expectedLastWorld = control.LastWorldSimulationTimeInMinutes + expectedWorldCycles * control.WorldCycleMinutes;
            if (report.NewLastWorldSimulationTimeInMinutes != expectedLastWorld)
            {
                if (report.NewLastWorldSimulationTimeInMinutes == null)
                {
                    issues.Add(new ValidationIssue(ReportPath, IssueSeverity.Error,
                        "progressionProcessingReport не содержит обязательное поле newLastWorldSimulationTimeInMinutes",
                        code: "progression_report_missing_new_last_world_time",
                        section: "ProgressionReport",
                        expected: expectedLastWorld.ToString(),
                        actual: "missing",
                        repairHint: "Если мировые циклы реально обработаны, укажи newLastWorldSimulationTimeInMinutes с новым authoritative last-world marker."));
                }
                else
                {
                    issues.Add(BuildProgressionMismatchIssue(
                        "progression_report_new_last_world_time_mismatch",
                        "newLastWorldSimulationTimeInMinutes",
                        expectedLastWorld,
                        report.NewLastWorldSimulationTimeInMinutes ?? 0,
                        "Исправь newLastWorldSimulationTimeInMinutes, чтобы он указывал новый authoritative last-world marker после обработанных world cycles."));
                }
            }
        }

        if (expectedFactionCycles > 0)
        {
            var expectedLastFaction = control.LastFactionSimulationTimeInMinutes + expectedFactionCycles * control.FactionCycleMinutes;
            if (report.NewLastFactionSimulationTimeInMinutes != expectedLastFaction)
            {
                if (report.NewLastFactionSimulationTimeInMinutes == null)
                {
                    issues.Add(new ValidationIssue(ReportPath, IssueSeverity.Error,
                        "progressionProcessingReport не содержит обязательное поле newLastFactionSimulationTimeInMinutes",
                        code: "progression_report_missing_new_last_faction_time",
                        section: "ProgressionReport",
                        expected: expectedLastFaction.ToString(),
                        actual: "missing",
                        repairHint: "Если фракционные циклы реально обработаны, укажи newLastFactionSimulationTimeInMinutes с новым authoritative last-faction marker."));
                }
                else
                {
                    issues.Add(BuildProgressionMismatchIssue(
                        "progression_report_new_last_faction_time_mismatch",
                        "newLastFactionSimulationTimeInMinutes",
                        expectedLastFaction,
                        report.NewLastFactionSimulationTimeInMinutes ?? 0,
                        "Исправь newLastFactionSimulationTimeInMinutes, чтобы он указывал новый authoritative last-faction marker после обработанных faction cycles."));
                }
            }
        }
    }

    private static ValidationIssue BuildProgressionMismatchIssue(string code, string fieldName, int expected, int actual, string repairHint)
    {
        return new ValidationIssue(
            ReportPath,
            IssueSeverity.Error,
            $"{fieldName} должен быть равен {expected}, получено {actual}",
            code: code,
            section: "ProgressionReport",
            expected: expected.ToString(),
            actual: actual.ToString(),
            repairHint: repairHint);
    }

    private static ValidationIssue BuildMissingProgressionReportIssue(string code, string expected, string repairHint)
    {
        return new ValidationIssue(
            ReportPath,
            IssueSeverity.Error,
            "Отсутствует progressionProcessingReport при обязательной прогрессии",
            code: code,
            section: "ProgressionReport",
            expected: expected,
            actual: "missing progressionProcessingReport",
            repairHint: repairHint);
    }

    private static ValidationIssue BuildForbiddenProgressionFieldIssue(
        string code,
        string fieldName,
        string expected,
        string actual,
        string repairHint)
    {
        return new ValidationIssue(
            ReportPath,
            IssueSeverity.Error,
            $"{fieldName} запрещены для текущего realm progression contract",
            code: code,
            section: "ProgressionReport",
            expected: expected,
            actual: actual,
            repairHint: repairHint);
    }

    private void ValidateChaosSeaOutcome(ProgressionControl control, ProgressionProcessingReport? report,
        List<ValidationIssue> issues, bool suppressMissingReportIssue)
    {
        var expectedChaosCycles = control.ChaosSeaCyclesExpectedThisTurn;
        var expectedGuardianCycles = control.GuardianProjectCyclesExpectedThisTurn;

        if (report == null)
        {
            if (!suppressMissingReportIssue && (expectedChaosCycles > 0 || expectedGuardianCycles > 0))
            {
                issues.Add(BuildMissingProgressionReportIssue(
                    "progression_report_missing_for_required_chaos_progression",
                    "chaos sea / guardian progression was expected for this afterlife turn",
                    "Создай progressionProcessingReport в game_state/control/progression_report.json и укажи точные processed cycle counts и новые last-* ordinals для этого afterlife turn."));
            }
            return;
        }

        if ((report.WorldCyclesProcessed ?? 0) != 0 || (report.FactionCyclesProcessed ?? 0) != 0)
        {
            issues.Add(BuildForbiddenProgressionFieldIssue(
                "progression_report_forbidden_mortal_fields_in_chaos",
                "worldCyclesProcessed / factionCyclesProcessed",
                "0 for both mortal-only fields",
                $"{report.WorldCyclesProcessed ?? 0} / {report.FactionCyclesProcessed ?? 0}",
                "В afterlife realm не указывай mortal progression fields. Оставь только chaosSea/guardian processed counts и их new last-* ordinals."));
        }

        if ((report.ChaosSeaCyclesProcessed ?? 0) != expectedChaosCycles)
        {
            if (report.ChaosSeaCyclesProcessed == null)
            {
                issues.Add(new ValidationIssue(ReportPath, IssueSeverity.Error,
                    "progressionProcessingReport не содержит обязательное поле chaosSeaCyclesProcessed",
                    code: "progression_report_missing_chaos_cycles_processed",
                    section: "ProgressionReport",
                    expected: expectedChaosCycles.ToString(),
                    actual: "missing",
                    repairHint: "Добавь chaosSeaCyclesProcessed в progressionProcessingReport и укажи фактически обработанное число afterlife cycles для текущего afterlife realm в этом ходу."));
            }
            else
            {
                issues.Add(BuildProgressionMismatchIssue(
                    "progression_report_chaos_cycles_processed_mismatch",
                    "chaosSeaCyclesProcessed",
                    expectedChaosCycles,
                    report.ChaosSeaCyclesProcessed ?? 0,
                    "Исправь chaosSeaCyclesProcessed в progressionProcessingReport, чтобы он отражал точное число afterlife cycles, которые клиент ожидал для текущего afterlife realm в этом ходу."));
            }
        }

        if ((report.GuardianProjectCyclesProcessed ?? 0) != expectedGuardianCycles)
        {
            if (report.GuardianProjectCyclesProcessed == null)
            {
                issues.Add(new ValidationIssue(ReportPath, IssueSeverity.Error,
                    "progressionProcessingReport не содержит обязательное поле guardianProjectCyclesProcessed",
                    code: "progression_report_missing_guardian_cycles_processed",
                    section: "ProgressionReport",
                    expected: expectedGuardianCycles.ToString(),
                    actual: "missing",
                    repairHint: "Добавь guardianProjectCyclesProcessed в progressionProcessingReport и укажи фактически обработанное число guardian project cycles для этого хода."));
            }
            else
            {
                issues.Add(BuildProgressionMismatchIssue(
                    "progression_report_guardian_cycles_processed_mismatch",
                    "guardianProjectCyclesProcessed",
                    expectedGuardianCycles,
                    report.GuardianProjectCyclesProcessed ?? 0,
                    "Исправь guardianProjectCyclesProcessed в progressionProcessingReport, чтобы он отражал точное число guardian project cycles, которые клиент ожидал для этого хода."));
            }
        }

        if (expectedChaosCycles > 0 && report.NewLastChaosSeaSimulationOrdinal != control.NextChaosSeaTurnOrdinal)
        {
            if (report.NewLastChaosSeaSimulationOrdinal == null)
            {
                issues.Add(new ValidationIssue(ReportPath, IssueSeverity.Error,
                    "progressionProcessingReport не содержит обязательное поле newLastChaosSeaSimulationOrdinal",
                    code: "progression_report_missing_new_last_chaos_ordinal",
                    section: "ProgressionReport",
                    expected: control.NextChaosSeaTurnOrdinal.ToString(),
                    actual: "missing",
                    repairHint: "Если afterlife cycles обработаны, укажи newLastChaosSeaSimulationOrdinal с новым authoritative ordinal marker для текущего afterlife realm."));
            }
            else
            {
                issues.Add(BuildProgressionMismatchIssue(
                    "progression_report_new_last_chaos_ordinal_mismatch",
                    "newLastChaosSeaSimulationOrdinal",
                    control.NextChaosSeaTurnOrdinal,
                    report.NewLastChaosSeaSimulationOrdinal ?? 0,
                    "Исправь newLastChaosSeaSimulationOrdinal, чтобы он указывал новый authoritative afterlife ordinal marker после обработанных afterlife cycles в текущем afterlife realm."));
            }
        }

        if (expectedGuardianCycles > 0 && report.NewLastGuardianProjectCycleOrdinal != control.NextChaosSeaTurnOrdinal)
        {
            if (report.NewLastGuardianProjectCycleOrdinal == null)
            {
                issues.Add(new ValidationIssue(ReportPath, IssueSeverity.Error,
                    "progressionProcessingReport не содержит обязательное поле newLastGuardianProjectCycleOrdinal",
                    code: "progression_report_missing_new_last_guardian_ordinal",
                    section: "ProgressionReport",
                    expected: control.NextChaosSeaTurnOrdinal.ToString(),
                    actual: "missing",
                    repairHint: "Если guardian project cycles обработаны, укажи newLastGuardianProjectCycleOrdinal с новым authoritative ordinal marker."));
            }
            else
            {
                issues.Add(BuildProgressionMismatchIssue(
                    "progression_report_new_last_guardian_ordinal_mismatch",
                    "newLastGuardianProjectCycleOrdinal",
                    control.NextChaosSeaTurnOrdinal,
                    report.NewLastGuardianProjectCycleOrdinal ?? 0,
                    "Исправь newLastGuardianProjectCycleOrdinal, чтобы он указывал новый authoritative guardian-project ordinal marker после обработанных guardian cycles."));
            }
        }
    }

    private async Task<ProgressionScheduleState> SanitizeScheduleAsync(ProgressionScheduleState schedule, string? activeTurnRealm = null)
    {
        schedule.WorldCycleMinutes = schedule.WorldCycleMinutes > 0 ? schedule.WorldCycleMinutes : 240;
        schedule.FactionCycleMinutes = schedule.FactionCycleMinutes > 0 ? schedule.FactionCycleMinutes : 1440;
        schedule.ChaosSeaCycleEquivalentHours = schedule.ChaosSeaCycleEquivalentHours > 0
            ? schedule.ChaosSeaCycleEquivalentHours
            : 24;

        schedule.CurrentRealm = activeTurnRealm ?? await ResolveCurrentRealmAsync(string.Empty);
        if (HasResolvedRealm(schedule.CurrentRealm) && !IsChaosSea(schedule.CurrentRealm))
        {
            schedule.CurrentWorldTimeInMinutes = (await ResolveWorldTimeFromFileAsync(schedule.CurrentWorldTimeInMinutes)).Minutes;
        }

        schedule.PendingWorldCycles = Math.Max(0, schedule.PendingWorldCycles);
        schedule.PendingFactionCycles = Math.Max(0, schedule.PendingFactionCycles);
        schedule.PendingChaosSeaCycles = Math.Max(0, schedule.PendingChaosSeaCycles);
        schedule.PendingGuardianProjectCycles = Math.Max(0, schedule.PendingGuardianProjectCycles);
        schedule.LastUpdatedUtc ??= DateTime.UtcNow.ToString("o");
        return schedule;
    }

    private async Task<ProgressionScheduleState?> ReadScheduleAsync()
    {
        var json = await _fs.ReadFileAsync(SchedulePath);
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<ProgressionScheduleState>(json, JsonOpts);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Не удалось разобрать progression_schedule.json");
            return null;
        }
    }

    private async Task WriteScheduleAsync(ProgressionScheduleState schedule)
    {
        await _fs.WriteFileAtomicAsync(SchedulePath, JsonSerializer.Serialize(schedule, JsonOpts));
    }

    private async Task<(ProgressionProcessingReport? Report, bool FilePresent)> ReadProcessingReportSnapshotAsync()
    {
        var json = await _fs.ReadFileAsync(ReportPath);
        if (string.IsNullOrWhiteSpace(json))
            return (null, false);

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return (null, true);

            if (root.TryGetProperty("progressionProcessingReport", out var nested) &&
                nested.ValueKind == JsonValueKind.Object)
            {
                return (JsonSerializer.Deserialize<ProgressionProcessingReport>(nested.GetRawText(), JsonOpts), true);
            }

            return (JsonSerializer.Deserialize<ProgressionProcessingReport>(root.GetRawText(), JsonOpts), true);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Не удалось разобрать progression_report.json");
            return (null, true);
        }
    }

    private async Task<ProgressionProcessingReport?> ReadProcessingReportAsync()
    {
        var snapshot = await ReadProcessingReportSnapshotAsync();
        return snapshot.Report;
    }

    private async Task<string> ResolveCurrentRealmAsync(string fallback = "")
    {
        var soulJson = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        if (string.IsNullOrWhiteSpace(soulJson))
            return fallback;

        try
        {
            using var doc = JsonDocument.Parse(soulJson);
            if (doc.RootElement.TryGetProperty("currentRealm", out var realm) && realm.ValueKind == JsonValueKind.String)
                return realm.GetString() ?? fallback;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Не удалось определить currentRealm для progression scheduler");
        }

        return fallback;
    }

    private async Task<WorldTimeResolutionResult> ResolveWorldTimeFromFileAsync(int fallback)
    {
        var json = await _fs.ReadFileAsync("game_state/world/world_time.json");
        if (string.IsNullOrWhiteSpace(json))
            return new WorldTimeResolutionResult(fallback, true);

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (TryReadIntLike(root, "currentTimeInMinutes", out var absolute))
                return new WorldTimeResolutionResult(absolute, false);

            if (root.TryGetProperty("setWorldTime", out var setWorldTime) &&
                setWorldTime.ValueKind == JsonValueKind.Object)
            {
                if (TryReadIntLike(setWorldTime, "currentTimeInMinutes", out absolute))
                    return new WorldTimeResolutionResult(absolute, false);

                if (LooksLikeAbsoluteWorldTimeObject(setWorldTime))
                    return new WorldTimeResolutionResult(fallback, true);
            }

            if (TryReadIntLike(root, "timeChange", out var delta))
                return new WorldTimeResolutionResult(Math.Max(0, fallback + delta), false);

            if (LooksLikeAbsoluteWorldTimeObject(root))
                return new WorldTimeResolutionResult(fallback, true);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Не удалось прочитать world_time.json для progression scheduler");
            return new WorldTimeResolutionResult(fallback, true);
        }

        return new WorldTimeResolutionResult(fallback, true);
    }

    private static bool TryReadIntLike(JsonElement root, string propertyName, out int value)
    {
        value = 0;
        if (!root.TryGetProperty(propertyName, out var prop))
            return false;

        return prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out value);
    }

    private static int ComputeElapsedCycles(int current, int lastProcessed, int cycleMinutes)
    {
        if (cycleMinutes <= 0 || current <= lastProcessed)
            return 0;

        return (current - lastProcessed) / cycleMinutes;
    }

    private static bool LooksLikeAbsoluteWorldTimeObject(JsonElement root)
    {
        return root.ValueKind == JsonValueKind.Object &&
               root.TryGetProperty("year", out _) &&
               root.TryGetProperty("dayOfMonth", out _) &&
               root.TryGetProperty("monthName", out var monthName) &&
               monthName.ValueKind == JsonValueKind.String &&
               !string.IsNullOrWhiteSpace(monthName.GetString()) &&
               root.TryGetProperty("timeOfDay", out var timeOfDay) &&
               timeOfDay.ValueKind == JsonValueKind.String &&
               !string.IsNullOrWhiteSpace(timeOfDay.GetString());
    }

    private static bool HasResolvedRealm(string? realm) =>
        !string.IsNullOrWhiteSpace(realm);

    private static bool IsChaosSea(string? realm) =>
        string.Equals(realm, "Chaos Sea", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(realm, "Море Хаоса", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(realm, "Shining Abode", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(realm, "Сияющая Обитель", StringComparison.OrdinalIgnoreCase) ||
        (!string.IsNullOrWhiteSpace(realm) && realm.Contains("Chaos", StringComparison.OrdinalIgnoreCase));

    private static bool SchedulesEqual(ProgressionScheduleState left, ProgressionScheduleState right)
    {
        return left.CurrentRealm == right.CurrentRealm &&
               left.CurrentWorldTimeInMinutes == right.CurrentWorldTimeInMinutes &&
               left.LastWorldSimulationTimeInMinutes == right.LastWorldSimulationTimeInMinutes &&
               left.LastFactionSimulationTimeInMinutes == right.LastFactionSimulationTimeInMinutes &&
               left.HasAuthoritativeWorldTimeBaseline == right.HasAuthoritativeWorldTimeBaseline &&
               left.WorldCycleMinutes == right.WorldCycleMinutes &&
               left.FactionCycleMinutes == right.FactionCycleMinutes &&
               left.PendingWorldCycles == right.PendingWorldCycles &&
               left.PendingFactionCycles == right.PendingFactionCycles &&
               left.CurrentChaosSeaTurnOrdinal == right.CurrentChaosSeaTurnOrdinal &&
               left.LastChaosSeaSimulationOrdinal == right.LastChaosSeaSimulationOrdinal &&
               left.LastGuardianProjectCycleOrdinal == right.LastGuardianProjectCycleOrdinal &&
               left.PendingChaosSeaCycles == right.PendingChaosSeaCycles &&
               left.PendingGuardianProjectCycles == right.PendingGuardianProjectCycles &&
               left.ChaosSeaCycleEquivalentHours == right.ChaosSeaCycleEquivalentHours;
    }
}

internal readonly record struct WorldTimeResolutionResult(int Minutes, bool HasUnresolvedAbsoluteOverride);

public class ProgressionScheduleState
{
    public string CurrentRealm { get; set; } = string.Empty;
    public int CurrentWorldTimeInMinutes { get; set; }
    public int LastWorldSimulationTimeInMinutes { get; set; }
    public int LastFactionSimulationTimeInMinutes { get; set; }
    public bool HasAuthoritativeWorldTimeBaseline { get; set; }
    public int WorldCycleMinutes { get; set; } = 240;
    public int FactionCycleMinutes { get; set; } = 1440;
    public int PendingWorldCycles { get; set; }
    public int PendingFactionCycles { get; set; }
    public int CurrentChaosSeaTurnOrdinal { get; set; }
    public int LastChaosSeaSimulationOrdinal { get; set; }
    public int LastGuardianProjectCycleOrdinal { get; set; }
    public int PendingChaosSeaCycles { get; set; }
    public int PendingGuardianProjectCycles { get; set; }
    public int ChaosSeaCycleEquivalentHours { get; set; } = 24;
    public string? LastUpdatedUtc { get; set; }
}
