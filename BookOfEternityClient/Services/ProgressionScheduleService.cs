using System.Text.Json;
using System.Text.Json.Nodes;
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
    private const int DefaultWorldCycleMinutes = 240;
    private const int DefaultFactionCycleMinutes = 1440;
    private const int DefaultChaosSeaCycleEquivalentHours = 24;
    private const int DefaultAfterlifeCatchupCycleEquivalentMinutes = 1440;
    private const int MaxAfterlifeCatchupSummaryEvents = 5;

    private readonly FileSystemManager _fs;
    private readonly ILogger<ProgressionScheduleService> _logger;

    private enum ProgressionFileReadState
    {
        Missing,
        Valid,
        Malformed
    }

    private enum AfterlifeRealmKind
    {
        None,
        ChaosSea,
        ShiningAbode,
        ShiningBootstrapHandoff
    }

    private readonly record struct ProgressionScheduleSnapshot(
        ProgressionScheduleState? Schedule,
        ProgressionFileReadState State,
        bool FilePresent);

    private readonly record struct ProgressionReportSnapshot(
        ProgressionProcessingReport? Report,
        ProgressionFileReadState State,
        bool FilePresent);

    private readonly record struct PendingTurnRequestContext(
        string SessionId,
        string RequestId,
        int TurnNumber);

    private readonly record struct AfterlifeCatchupContext(
        bool Required,
        int ElapsedCycles,
        string PressureTier,
        int SummaryEventsRequired,
        string[] Contours);

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
        var scheduleSnapshot = await ReadScheduleSnapshotAsync();
        if (scheduleSnapshot.State == ProgressionFileReadState.Malformed)
        {
            throw new InvalidOperationException(
                "game_state/control/progression_schedule.json повреждён или не соответствует canonical contract. " +
                "Progression ledger сохраняется fail-closed и не может быть silently re-bootstrap-нут.");
        }

        var existing = scheduleSnapshot.Schedule;
        if (existing != null)
        {
            var sanitized = await SanitizeScheduleAsync(existing, existing.CurrentRealm);
            if (!SchedulesEqual(existing, sanitized))
                await WriteScheduleAsync(sanitized);
            return sanitized;
        }

        var realm = await ResolveCurrentRealmAsync(string.Empty);
        if (!HasResolvedRealm(realm))
        {
            throw new InvalidOperationException(
                "game_state/meta/soul_state.json не содержит безопасно читаемый currentRealm. " +
                "Progression ledger сохраняется fail-closed и не может быть создан с пустым realm contract.");
        }

        var worldTimeResolution = await ResolveWorldTimeFromFileAsync(0);
        var currentWorldTime = worldTimeResolution.Minutes;
        var schedule = new ProgressionScheduleState
        {
            CurrentRealm = realm,
            CurrentWorldTimeInMinutes = currentWorldTime,
            LastWorldSimulationTimeInMinutes = currentWorldTime,
            LastFactionSimulationTimeInMinutes = currentWorldTime,
            HasAuthoritativeWorldTimeBaseline = !worldTimeResolution.HasUnresolvedAbsoluteOverride,
            WorldCycleMinutes = DefaultWorldCycleMinutes,
            FactionCycleMinutes = DefaultFactionCycleMinutes,
            ChaosSeaCycleEquivalentHours = DefaultChaosSeaCycleEquivalentHours,
            AfterlifeCatchupCycleEquivalentMinutes = DefaultAfterlifeCatchupCycleEquivalentMinutes,
            HasAfterlifeCatchupWorldTimeBaseline = true,
            LastAfterlifeCatchupWorldTimeInMinutes = currentWorldTime,
            CurrentChaosSeaTurnOrdinal = 0,
            LastChaosSeaSimulationOrdinal = 0,
            LastGuardianProjectCycleOrdinal = 0,
            LastResidentAgencyCycleOrdinal = 0,
            LastShiningAbodeCycleOrdinal = 0,
            LastShiningFactionCycleOrdinal = 0,
            LastShiningTradeCycleOrdinal = 0,
            PendingWorldCycles = 0,
            PendingFactionCycles = 0,
            PendingChaosSeaCycles = 0,
            PendingGuardianProjectCycles = 0,
            PendingResidentAgencyCycles = 0,
            PendingShiningAbodeCycles = 0,
            PendingShiningFactionCycles = 0,
            PendingShiningTradeCycles = 0,
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
            throw BuildUnresolvedRealmException();

        var afterlifeRealmKind = await ResolveAfterlifeRealmKindAsync(schedule.CurrentRealm);
        if (afterlifeRealmKind != AfterlifeRealmKind.None)
        {
            var afterlifeWorldTimeResolution = await ResolveWorldTimeFromFileAsync(schedule.CurrentWorldTimeInMinutes);
            if (!afterlifeWorldTimeResolution.HasUnresolvedAbsoluteOverride)
                schedule.CurrentWorldTimeInMinutes = afterlifeWorldTimeResolution.Minutes;

            schedule.PendingWorldCycles = 0;
            schedule.PendingFactionCycles = 0;
            if (afterlifeRealmKind == AfterlifeRealmKind.ShiningBootstrapHandoff)
            {
                ClearAfterlifePendingCycles(schedule);
            }
            else if (afterlifeRealmKind == AfterlifeRealmKind.ChaosSea)
            {
                schedule.PendingChaosSeaCycles = Math.Max(1, schedule.PendingChaosSeaCycles);
                schedule.PendingGuardianProjectCycles = Math.Max(1, schedule.PendingGuardianProjectCycles);
                schedule.PendingResidentAgencyCycles = Math.Max(1, schedule.PendingResidentAgencyCycles);
                schedule.PendingShiningAbodeCycles = 0;
                schedule.PendingShiningFactionCycles = 0;
                schedule.PendingShiningTradeCycles = 0;
            }
            else
            {
                schedule.PendingChaosSeaCycles = 0;
                schedule.PendingGuardianProjectCycles = Math.Max(1, schedule.PendingGuardianProjectCycles);
                schedule.PendingResidentAgencyCycles = Math.Max(1, schedule.PendingResidentAgencyCycles);
                schedule.PendingShiningAbodeCycles = Math.Max(1, schedule.PendingShiningAbodeCycles);
                schedule.PendingShiningFactionCycles = Math.Max(1, schedule.PendingShiningFactionCycles);
                schedule.PendingShiningTradeCycles = Math.Max(1, schedule.PendingShiningTradeCycles);
            }

            var includeShiningContoursInMortalCatchup = await HasActiveShiningAbodeWithoutPreparedPackageAsync();
            var catchup = BuildAfterlifeCatchupContext(
                schedule,
                afterlifeRealmKind,
                includeShiningContoursInMortalCatchup);
            schedule.LastUpdatedUtc = DateTime.UtcNow.ToString("o");
            await WriteScheduleAsync(schedule);

            return BuildProgressionControl(schedule, worldCycles: 0, factionCycles: 0, catchup);
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
        schedule.PendingResidentAgencyCycles = 0;
        schedule.PendingShiningAbodeCycles = 0;
        schedule.PendingShiningFactionCycles = 0;
        schedule.PendingShiningTradeCycles = 0;
        schedule.LastUpdatedUtc = DateTime.UtcNow.ToString("o");
        await WriteScheduleAsync(schedule);

        return BuildProgressionControl(
            schedule,
            schedule.PendingWorldCycles,
            schedule.PendingFactionCycles,
            new AfterlifeCatchupContext(false, 0, "none", 0, Array.Empty<string>()));
    }

    private static ProgressionControl BuildProgressionControl(
        ProgressionScheduleState schedule,
        int worldCycles,
        int factionCycles,
        AfterlifeCatchupContext catchup)
    {
        var afterlifeCyclesExpected = new[]
        {
            schedule.PendingChaosSeaCycles,
            schedule.PendingGuardianProjectCycles,
            schedule.PendingResidentAgencyCycles,
            schedule.PendingShiningAbodeCycles,
            schedule.PendingShiningFactionCycles,
            schedule.PendingShiningTradeCycles
        }.Max();
        var nextAfterlifeOrdinal = schedule.CurrentChaosSeaTurnOrdinal + afterlifeCyclesExpected;

        return new ProgressionControl
        {
            CurrentRealm = schedule.CurrentRealm,
            CurrentWorldTimeInMinutes = schedule.CurrentWorldTimeInMinutes,
            LastWorldSimulationTimeInMinutes = schedule.LastWorldSimulationTimeInMinutes,
            LastFactionSimulationTimeInMinutes = schedule.LastFactionSimulationTimeInMinutes,
            WorldCycleMinutes = schedule.WorldCycleMinutes,
            FactionCycleMinutes = schedule.FactionCycleMinutes,
            WorldCyclesAlreadyPendingBeforeTurn = worldCycles,
            FactionCyclesAlreadyPendingBeforeTurn = factionCycles,
            MustEvaluateWorldProgression = worldCycles > 0,
            MustEvaluateFactionProgression = factionCycles > 0,
            CurrentChaosSeaTurnOrdinal = schedule.CurrentChaosSeaTurnOrdinal,
            NextChaosSeaTurnOrdinal = nextAfterlifeOrdinal,
            LastChaosSeaSimulationOrdinal = schedule.LastChaosSeaSimulationOrdinal,
            LastGuardianProjectCycleOrdinal = schedule.LastGuardianProjectCycleOrdinal,
            NextGuardianProjectCycleOrdinal = schedule.PendingGuardianProjectCycles > 0 ? nextAfterlifeOrdinal : schedule.LastGuardianProjectCycleOrdinal,
            LastResidentAgencyCycleOrdinal = schedule.LastResidentAgencyCycleOrdinal,
            LastShiningAbodeCycleOrdinal = schedule.LastShiningAbodeCycleOrdinal,
            LastShiningFactionCycleOrdinal = schedule.LastShiningFactionCycleOrdinal,
            LastShiningTradeCycleOrdinal = schedule.LastShiningTradeCycleOrdinal,
            ChaosSeaCycleEquivalentHours = schedule.ChaosSeaCycleEquivalentHours,
            NextResidentAgencyCycleOrdinal = schedule.PendingResidentAgencyCycles > 0 ? nextAfterlifeOrdinal : schedule.LastResidentAgencyCycleOrdinal,
            NextShiningAbodeCycleOrdinal = schedule.PendingShiningAbodeCycles > 0 ? nextAfterlifeOrdinal : schedule.LastShiningAbodeCycleOrdinal,
            NextShiningFactionCycleOrdinal = schedule.PendingShiningFactionCycles > 0 ? nextAfterlifeOrdinal : schedule.LastShiningFactionCycleOrdinal,
            NextShiningTradeCycleOrdinal = schedule.PendingShiningTradeCycles > 0 ? nextAfterlifeOrdinal : schedule.LastShiningTradeCycleOrdinal,
            ChaosSeaCyclesExpectedThisTurn = schedule.PendingChaosSeaCycles,
            GuardianProjectCyclesExpectedThisTurn = schedule.PendingGuardianProjectCycles,
            ResidentAgencyCyclesExpectedThisTurn = schedule.PendingResidentAgencyCycles,
            ShiningAbodeCyclesExpectedThisTurn = schedule.PendingShiningAbodeCycles,
            ShiningFactionCyclesExpectedThisTurn = schedule.PendingShiningFactionCycles,
            ShiningTradeCyclesExpectedThisTurn = schedule.PendingShiningTradeCycles,
            MustEvaluateChaosSeaProgression = schedule.PendingChaosSeaCycles > 0,
            MustEvaluateGuardianProjectProgression = schedule.PendingGuardianProjectCycles > 0,
            MustEvaluateResidentAgencyProgression = schedule.PendingResidentAgencyCycles > 0,
            MustEvaluateShiningAbodeProgression = schedule.PendingShiningAbodeCycles > 0,
            MustEvaluateShiningFactionProgression = schedule.PendingShiningFactionCycles > 0,
            MustEvaluateShiningTradeProgression = schedule.PendingShiningTradeCycles > 0,
            AfterlifeCatchupRequired = catchup.Required,
            AfterlifeCatchupElapsedCycles = catchup.ElapsedCycles,
            AfterlifeCatchupPressureTier = catchup.PressureTier,
            AfterlifeCatchupSummaryEventsRequired = catchup.SummaryEventsRequired,
            AfterlifeCatchupContours = catchup.Contours
        };
    }

    private static void ClearAfterlifePendingCycles(ProgressionScheduleState schedule)
    {
        schedule.PendingChaosSeaCycles = 0;
        schedule.PendingGuardianProjectCycles = 0;
        schedule.PendingResidentAgencyCycles = 0;
        schedule.PendingShiningAbodeCycles = 0;
        schedule.PendingShiningFactionCycles = 0;
        schedule.PendingShiningTradeCycles = 0;
    }

    private static void PreserveAfterlifePendingCycles(
        ProgressionScheduleState schedule,
        ProgressionControl control)
    {
        schedule.PendingChaosSeaCycles = Math.Max(
            schedule.PendingChaosSeaCycles,
            Math.Max(0, control.ChaosSeaCyclesExpectedThisTurn));
        schedule.PendingGuardianProjectCycles = Math.Max(
            schedule.PendingGuardianProjectCycles,
            Math.Max(0, control.GuardianProjectCyclesExpectedThisTurn));
        schedule.PendingResidentAgencyCycles = Math.Max(
            schedule.PendingResidentAgencyCycles,
            Math.Max(0, control.ResidentAgencyCyclesExpectedThisTurn));
        schedule.PendingShiningAbodeCycles = Math.Max(
            schedule.PendingShiningAbodeCycles,
            Math.Max(0, control.ShiningAbodeCyclesExpectedThisTurn));
        schedule.PendingShiningFactionCycles = Math.Max(
            schedule.PendingShiningFactionCycles,
            Math.Max(0, control.ShiningFactionCyclesExpectedThisTurn));
        schedule.PendingShiningTradeCycles = Math.Max(
            schedule.PendingShiningTradeCycles,
            Math.Max(0, control.ShiningTradeCyclesExpectedThisTurn));
    }

    private static void ApplyVerifiedAfterlifeProgression(
        ProgressionScheduleState schedule,
        ProgressionControl control,
        ProgressionProcessingReport? report)
    {
        schedule.CurrentChaosSeaTurnOrdinal = Math.Max(
            schedule.CurrentChaosSeaTurnOrdinal,
            control.NextChaosSeaTurnOrdinal);

        if ((report?.ChaosSeaCyclesProcessed ?? 0) > 0)
            schedule.LastChaosSeaSimulationOrdinal = report?.NewLastChaosSeaSimulationOrdinal ?? control.NextChaosSeaTurnOrdinal;
        if ((report?.GuardianProjectCyclesProcessed ?? 0) > 0)
            schedule.LastGuardianProjectCycleOrdinal = report?.NewLastGuardianProjectCycleOrdinal ?? control.NextGuardianProjectCycleOrdinal;
        if ((report?.ResidentAgencyCyclesProcessed ?? 0) > 0)
            schedule.LastResidentAgencyCycleOrdinal = report?.NewLastResidentAgencyCycleOrdinal ?? control.NextResidentAgencyCycleOrdinal;
        if ((report?.ShiningAbodeCyclesProcessed ?? 0) > 0)
            schedule.LastShiningAbodeCycleOrdinal = report?.NewLastShiningAbodeCycleOrdinal ?? control.NextShiningAbodeCycleOrdinal;
        if ((report?.ShiningFactionCyclesProcessed ?? 0) > 0)
            schedule.LastShiningFactionCycleOrdinal = report?.NewLastShiningFactionCycleOrdinal ?? control.NextShiningFactionCycleOrdinal;
        if ((report?.ShiningTradeCyclesProcessed ?? 0) > 0)
            schedule.LastShiningTradeCycleOrdinal = report?.NewLastShiningTradeCycleOrdinal ?? control.NextShiningTradeCycleOrdinal;

        if (control.AfterlifeCatchupRequired && report?.AfterlifeCatchupProcessed == true)
        {
            schedule.LastAfterlifeCatchupWorldTimeInMinutes = Math.Max(
                schedule.LastAfterlifeCatchupWorldTimeInMinutes,
                control.CurrentWorldTimeInMinutes);
            schedule.HasAfterlifeCatchupWorldTimeBaseline = true;
            ApplyCatchupContourMarkers(schedule, control);
        }
    }

    private static void ApplyCatchupContourMarkers(
        ProgressionScheduleState schedule,
        ProgressionControl control)
    {
        var contours = new HashSet<string>(control.AfterlifeCatchupContours ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        var catchupOrdinal = Math.Max(schedule.CurrentChaosSeaTurnOrdinal, control.NextChaosSeaTurnOrdinal);

        if (contours.Contains("chaos_sea"))
            schedule.LastChaosSeaSimulationOrdinal = Math.Max(schedule.LastChaosSeaSimulationOrdinal, catchupOrdinal);
        if (contours.Contains("guardian_projects"))
            schedule.LastGuardianProjectCycleOrdinal = Math.Max(schedule.LastGuardianProjectCycleOrdinal, catchupOrdinal);
        if (contours.Contains("residents"))
            schedule.LastResidentAgencyCycleOrdinal = Math.Max(schedule.LastResidentAgencyCycleOrdinal, catchupOrdinal);
        if (contours.Contains("shining_abode"))
            schedule.LastShiningAbodeCycleOrdinal = Math.Max(schedule.LastShiningAbodeCycleOrdinal, catchupOrdinal);
        if (contours.Contains("shining_factions"))
            schedule.LastShiningFactionCycleOrdinal = Math.Max(schedule.LastShiningFactionCycleOrdinal, catchupOrdinal);
        if (contours.Contains("shining_trade"))
            schedule.LastShiningTradeCycleOrdinal = Math.Max(schedule.LastShiningTradeCycleOrdinal, catchupOrdinal);
    }

    private static AfterlifeCatchupContext BuildAfterlifeCatchupContext(
        ProgressionScheduleState schedule,
        AfterlifeRealmKind realmKind,
        bool includeShiningContoursInMortalCatchup)
    {
        if (realmKind == AfterlifeRealmKind.None || realmKind == AfterlifeRealmKind.ShiningBootstrapHandoff)
            return new AfterlifeCatchupContext(false, 0, "none", 0, Array.Empty<string>());

        var mortalElapsedCycles = schedule.HasAfterlifeCatchupWorldTimeBaseline
            ? ComputeElapsedCycles(
                schedule.CurrentWorldTimeInMinutes,
                schedule.LastAfterlifeCatchupWorldTimeInMinutes,
                schedule.AfterlifeCatchupCycleEquivalentMinutes)
            : 0;

        var realmAbsenceCycles = realmKind switch
        {
            AfterlifeRealmKind.ChaosSea => Math.Max(0, schedule.CurrentChaosSeaTurnOrdinal - schedule.LastChaosSeaSimulationOrdinal),
            AfterlifeRealmKind.ShiningAbode => Math.Max(0, schedule.CurrentChaosSeaTurnOrdinal - schedule.LastShiningAbodeCycleOrdinal),
            _ => 0
        };
        var elapsedCycles = Math.Max(mortalElapsedCycles, realmAbsenceCycles);
        if (elapsedCycles <= 0)
            return new AfterlifeCatchupContext(false, 0, "none", 0, Array.Empty<string>());

        var tier = ResolveAfterlifeCatchupPressureTier(elapsedCycles);
        return new AfterlifeCatchupContext(
            true,
            elapsedCycles,
            tier,
            ResolveAfterlifeCatchupSummaryEvents(tier),
            BuildAfterlifeCatchupContours(realmKind, includeShiningContoursInMortalCatchup && mortalElapsedCycles > 0));
    }

    private static string ResolveAfterlifeCatchupPressureTier(int elapsedCycles)
    {
        if (elapsedCycles >= 61)
            return "epochal";
        if (elapsedCycles >= 15)
            return "severe";
        if (elapsedCycles >= 4)
            return "major";
        return "minor";
    }

    private static int ResolveAfterlifeCatchupSummaryEvents(string tier) =>
        tier switch
        {
            "epochal" => MaxAfterlifeCatchupSummaryEvents,
            "severe" => 3,
            "major" => 2,
            "minor" => 1,
            _ => 0
        };

    private static string[] BuildAfterlifeCatchupContours(AfterlifeRealmKind realmKind, bool includeAllActiveAfterlifeContours)
    {
        if (includeAllActiveAfterlifeContours)
        {
            return new[]
            {
                "chaos_sea",
                "guardian_projects",
                "residents",
                "shining_abode",
                "shining_factions",
                "shining_trade"
            };
        }

        return realmKind == AfterlifeRealmKind.ShiningAbode
            ? new[] { "shining_abode", "shining_factions", "shining_trade", "guardian_projects", "residents" }
            : new[] { "chaos_sea", "guardian_projects", "residents" };
    }

    public async Task<List<ValidationIssue>> ValidateAcceptedTurnOutcomeAsync(ProgressionControl? control)
    {
        var issues = new List<ValidationIssue>();
        if (control == null)
            return issues;

        if (!HasResolvedRealm(control.CurrentRealm))
        {
            issues.Add(BuildUnresolvedRealmIssue(control.CurrentRealm));
            return issues;
        }

        var reportSnapshot = await ReadProcessingReportSnapshotAsync();
        var currentTurnContext = await ReadCurrentTurnRequestContextAsync();
        if (IsAfterlifeRealm(control.CurrentRealm))
        {
            ValidateAfterlifeOutcome(control, reportSnapshot, currentTurnContext, issues);
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
                ValidateMortalOutcome(control, reportSnapshot, currentTurnContext, worldTimeResolution.Minutes, issues);
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
        var reportSnapshot = await ReadProcessingReportSnapshotAsync();
        var currentTurnContext = await ReadCurrentTurnRequestContextAsync();
        var report = reportSnapshot.Report;
        var reportConsumed = false;

        if (!HasResolvedRealm(control.CurrentRealm))
        {
            _logger.LogWarning(
                "Accepted turn outcome arrived with unresolved currentRealm in progression control. Preserving progression ledger fail-closed.");
        }
        else if (IsAfterlifeRealm(control.CurrentRealm))
        {
            if (reportSnapshot.State == ProgressionFileReadState.Valid &&
                HasVerifiedAfterlifeProgressionOutcome(control, report, currentTurnContext))
            {
                ApplyVerifiedAfterlifeProgression(schedule, control, report);
                schedule.PendingWorldCycles = 0;
                schedule.PendingFactionCycles = 0;
                ClearAfterlifePendingCycles(schedule);
                reportConsumed = true;
            }
            else
            {
                _logger.LogWarning(
                    "Afterlife accepted turn completed without a valid progression_report.json outcome. Keeping CurrentChaosSeaTurnOrdinal={CurrentOrdinal}, LastChaosSeaSimulationOrdinal={ChaosOrdinal}, LastGuardianProjectCycleOrdinal={GuardianOrdinal}.",
                    schedule.CurrentChaosSeaTurnOrdinal,
                    schedule.LastChaosSeaSimulationOrdinal,
                    schedule.LastGuardianProjectCycleOrdinal);
                schedule.PendingWorldCycles = 0;
                schedule.PendingFactionCycles = 0;
                PreserveAfterlifePendingCycles(schedule, control);
            }
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

            if (reportSnapshot.State == ProgressionFileReadState.Valid &&
                ProgressionReportMatchesCurrentTurn(report, currentTurnContext))
            {
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
                ClearAfterlifePendingCycles(schedule);
                reportConsumed = true;
            }
            else
            {
                schedule.PendingWorldCycles = Math.Max(
                    schedule.PendingWorldCycles,
                    Math.Max(0, control.WorldCyclesAlreadyPendingBeforeTurn));
                schedule.PendingFactionCycles = Math.Max(
                    schedule.PendingFactionCycles,
                    Math.Max(0, control.FactionCyclesAlreadyPendingBeforeTurn));
                ClearAfterlifePendingCycles(schedule);
            }
        }

        if (HasResolvedRealm(realmAfterTurn))
            schedule.CurrentRealm = realmAfterTurn;
        schedule.LastUpdatedUtc = DateTime.UtcNow.ToString("o");

        await WriteScheduleAsync(schedule);
        if (reportConsumed && reportSnapshot.FilePresent)
            await DeleteTransientReportAsync();
    }

    public Task DeleteTransientReportAsync()
    {
        if (_fs.FileExists(ReportPath))
            _fs.DeleteFile(ReportPath);
        return Task.CompletedTask;
    }

    private void ValidateMortalOutcome(
        ProgressionControl control,
        ProgressionReportSnapshot reportSnapshot,
        PendingTurnRequestContext? currentTurnContext,
        int resultingWorldTime,
        List<ValidationIssue> issues)
    {
        var report = reportSnapshot.Report;
        var expectedWorldCycles = Math.Max(0, control.WorldCyclesAlreadyPendingBeforeTurn);
        var expectedFactionCycles = Math.Max(0, control.FactionCyclesAlreadyPendingBeforeTurn);

        if (report == null)
        {
            if ((expectedWorldCycles > 0 || expectedFactionCycles > 0) &&
                reportSnapshot.State == ProgressionFileReadState.Malformed)
            {
                issues.Add(BuildMalformedProgressionReportIssue(
                    "progression_report_malformed_for_required_mortal_progression",
                    "world/faction progression was expected for this Mortal World turn",
                    "Перезапиши progression_report.json валидным JSON object с progressionProcessingReport и точными world/faction processed counts и new last-* markers."));
            }
            else if (expectedWorldCycles > 0 || expectedFactionCycles > 0)
            {
                issues.Add(BuildMissingProgressionReportIssue(
                    "progression_report_missing_for_required_mortal_progression",
                    "world/faction progression was expected for this Mortal World turn",
                    "Создай progressionProcessingReport в game_state/control/progression_report.json и укажи точные processed cycle counts и новые last-* markers для этого Mortal World turn."));
            }
            return;
        }

        if (!ValidateProgressionReportCorrelation(report, currentTurnContext, issues))
            return;

        var afterlifeProcessedCount =
            (report.ChaosSeaCyclesProcessed ?? 0) +
            (report.GuardianProjectCyclesProcessed ?? 0) +
            (report.ResidentAgencyCyclesProcessed ?? 0) +
            (report.ShiningAbodeCyclesProcessed ?? 0) +
            (report.ShiningFactionCyclesProcessed ?? 0) +
            (report.ShiningTradeCyclesProcessed ?? 0);
        if (afterlifeProcessedCount != 0 || report.AfterlifeCatchupProcessed == true)
        {
            issues.Add(BuildForbiddenProgressionFieldIssue(
                "progression_report_forbidden_afterlife_fields_in_mortal",
                "afterlife processed counts / afterlifeCatchupProcessed",
                "0/false for all afterlife-only fields",
                $"{afterlifeProcessedCount} / {report.AfterlifeCatchupProcessed == true}",
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

    private void ValidateAfterlifeOutcome(
        ProgressionControl control,
        ProgressionReportSnapshot reportSnapshot,
        PendingTurnRequestContext? currentTurnContext,
        List<ValidationIssue> issues)
    {
        var report = reportSnapshot.Report;
        var reportRequired = AfterlifeReportRequired(control);

        if (report == null)
        {
            if (reportRequired && reportSnapshot.State == ProgressionFileReadState.Malformed)
            {
                issues.Add(BuildMalformedProgressionReportIssue(
                    "progression_report_malformed_for_required_chaos_progression",
                    "afterlife progression or catch-up was expected for this afterlife turn",
                    "Перезапиши progression_report.json валидным JSON object с progressionProcessingReport, точными processed counts, catch-up proof и new last-* ordinals."));
            }
            else if (reportRequired)
            {
                issues.Add(BuildMissingProgressionReportIssue(
                    "progression_report_missing_for_required_chaos_progression",
                    "afterlife progression or catch-up was expected for this afterlife turn",
                    "Создай progressionProcessingReport в game_state/control/progression_report.json и укажи bounded processed cycle counts, catch-up proof и новые last-* ordinals для этого afterlife turn."));
            }
            return;
        }

        if (!ValidateProgressionReportCorrelation(report, currentTurnContext, issues))
            return;

        if ((report.WorldCyclesProcessed ?? 0) != 0 || (report.FactionCyclesProcessed ?? 0) != 0)
        {
            issues.Add(BuildForbiddenProgressionFieldIssue(
                "progression_report_forbidden_mortal_fields_in_chaos",
                "worldCyclesProcessed / factionCyclesProcessed",
                "0 for both mortal-only fields",
                $"{report.WorldCyclesProcessed ?? 0} / {report.FactionCyclesProcessed ?? 0}",
                "В afterlife realm не указывай mortal progression fields. Оставь только afterlife processed counts, catch-up proof и их new last-* ordinals."));
        }

        ValidateExpectedProcessedCount(issues, "chaosSeaCyclesProcessed", report.ChaosSeaCyclesProcessed, control.ChaosSeaCyclesExpectedThisTurn, "progression_report_missing_chaos_cycles_processed", "progression_report_chaos_cycles_processed_mismatch");
        ValidateExpectedProcessedCount(issues, "guardianProjectCyclesProcessed", report.GuardianProjectCyclesProcessed, control.GuardianProjectCyclesExpectedThisTurn, "progression_report_missing_guardian_cycles_processed", "progression_report_guardian_cycles_processed_mismatch");
        ValidateExpectedProcessedCount(issues, "residentAgencyCyclesProcessed", report.ResidentAgencyCyclesProcessed, control.ResidentAgencyCyclesExpectedThisTurn, "progression_report_missing_resident_agency_cycles_processed", "progression_report_resident_agency_cycles_processed_mismatch");
        ValidateExpectedProcessedCount(issues, "shiningAbodeCyclesProcessed", report.ShiningAbodeCyclesProcessed, control.ShiningAbodeCyclesExpectedThisTurn, "progression_report_missing_shining_abode_cycles_processed", "progression_report_shining_abode_cycles_processed_mismatch");
        ValidateExpectedProcessedCount(issues, "shiningFactionCyclesProcessed", report.ShiningFactionCyclesProcessed, control.ShiningFactionCyclesExpectedThisTurn, "progression_report_missing_shining_faction_cycles_processed", "progression_report_shining_faction_cycles_processed_mismatch");
        ValidateExpectedProcessedCount(issues, "shiningTradeCyclesProcessed", report.ShiningTradeCyclesProcessed, control.ShiningTradeCyclesExpectedThisTurn, "progression_report_missing_shining_trade_cycles_processed", "progression_report_shining_trade_cycles_processed_mismatch");

        ValidateExpectedOrdinal(issues, "newLastChaosSeaSimulationOrdinal", report.NewLastChaosSeaSimulationOrdinal, control.ChaosSeaCyclesExpectedThisTurn, control.NextChaosSeaTurnOrdinal, "progression_report_missing_new_last_chaos_ordinal", "progression_report_new_last_chaos_ordinal_mismatch");
        ValidateExpectedOrdinal(issues, "newLastGuardianProjectCycleOrdinal", report.NewLastGuardianProjectCycleOrdinal, control.GuardianProjectCyclesExpectedThisTurn, control.NextGuardianProjectCycleOrdinal, "progression_report_missing_new_last_guardian_ordinal", "progression_report_new_last_guardian_ordinal_mismatch");
        ValidateExpectedOrdinal(issues, "newLastResidentAgencyCycleOrdinal", report.NewLastResidentAgencyCycleOrdinal, control.ResidentAgencyCyclesExpectedThisTurn, control.NextResidentAgencyCycleOrdinal, "progression_report_missing_new_last_resident_agency_ordinal", "progression_report_new_last_resident_agency_ordinal_mismatch");
        ValidateExpectedOrdinal(issues, "newLastShiningAbodeCycleOrdinal", report.NewLastShiningAbodeCycleOrdinal, control.ShiningAbodeCyclesExpectedThisTurn, control.NextShiningAbodeCycleOrdinal, "progression_report_missing_new_last_shining_abode_ordinal", "progression_report_new_last_shining_abode_ordinal_mismatch");
        ValidateExpectedOrdinal(issues, "newLastShiningFactionCycleOrdinal", report.NewLastShiningFactionCycleOrdinal, control.ShiningFactionCyclesExpectedThisTurn, control.NextShiningFactionCycleOrdinal, "progression_report_missing_new_last_shining_faction_ordinal", "progression_report_new_last_shining_faction_ordinal_mismatch");
        ValidateExpectedOrdinal(issues, "newLastShiningTradeCycleOrdinal", report.NewLastShiningTradeCycleOrdinal, control.ShiningTradeCyclesExpectedThisTurn, control.NextShiningTradeCycleOrdinal, "progression_report_missing_new_last_shining_trade_ordinal", "progression_report_new_last_shining_trade_ordinal_mismatch");

        ValidateAfterlifeCatchupProof(control, report, issues);
    }

    private static bool AfterlifeReportRequired(ProgressionControl control) =>
        control.ChaosSeaCyclesExpectedThisTurn > 0 ||
        control.GuardianProjectCyclesExpectedThisTurn > 0 ||
        control.ResidentAgencyCyclesExpectedThisTurn > 0 ||
        control.ShiningAbodeCyclesExpectedThisTurn > 0 ||
        control.ShiningFactionCyclesExpectedThisTurn > 0 ||
        control.ShiningTradeCyclesExpectedThisTurn > 0 ||
        control.AfterlifeCatchupRequired;

    private static void ValidateExpectedProcessedCount(
        List<ValidationIssue> issues,
        string fieldName,
        int? actualValue,
        int expectedValue,
        string missingCode,
        string mismatchCode)
    {
        if ((actualValue ?? 0) == expectedValue)
            return;

        if (actualValue == null)
        {
            issues.Add(new ValidationIssue(ReportPath, IssueSeverity.Error,
                $"progressionProcessingReport не содержит обязательное поле {fieldName}",
                code: missingCode,
                section: "ProgressionReport",
                expected: expectedValue.ToString(),
                actual: "missing",
                repairHint: $"Добавь {fieldName} в progressionProcessingReport и укажи bounded число cycles, которое клиент запросил для этого хода."));
            return;
        }

        issues.Add(BuildProgressionMismatchIssue(
            mismatchCode,
            fieldName,
            expectedValue,
            actualValue ?? 0,
            $"Исправь {fieldName}: валидатор принимает только bounded count из progressionControl, а не raw elapsed backlog."));
    }

    private static void ValidateExpectedOrdinal(
        List<ValidationIssue> issues,
        string fieldName,
        int? actualValue,
        int expectedCycles,
        int expectedOrdinal,
        string missingCode,
        string mismatchCode)
    {
        if (expectedCycles <= 0 || actualValue == expectedOrdinal)
            return;

        if (actualValue == null)
        {
            issues.Add(new ValidationIssue(ReportPath, IssueSeverity.Error,
                $"progressionProcessingReport не содержит обязательное поле {fieldName}",
                code: missingCode,
                section: "ProgressionReport",
                expected: expectedOrdinal.ToString(),
                actual: "missing",
                repairHint: $"Если соответствующий afterlife contour обработан, укажи {fieldName} с новым authoritative ordinal marker из progressionControl."));
            return;
        }

        issues.Add(BuildProgressionMismatchIssue(
            mismatchCode,
            fieldName,
            expectedOrdinal,
            actualValue ?? 0,
            $"Исправь {fieldName}, чтобы он закрывал bounded cycle/catch-up через authoritative ordinal marker из progressionControl."));
    }

    private static void ValidateAfterlifeCatchupProof(
        ProgressionControl control,
        ProgressionProcessingReport report,
        List<ValidationIssue> issues)
    {
        if (!control.AfterlifeCatchupRequired)
        {
            if (report.AfterlifeCatchupProcessed == true)
            {
                issues.Add(new ValidationIssue(ReportPath, IssueSeverity.Error,
                    "progressionProcessingReport содержит afterlifeCatchupProcessed=true без запрошенного catch-up.",
                    code: "progression_report_unexpected_afterlife_catchup",
                    section: "ProgressionReport",
                    expected: "afterlifeCatchupProcessed omitted or false when afterlifeCatchupRequired=false",
                    actual: "afterlifeCatchupProcessed=true",
                    repairHint: "Не закрывай catch-up, которого нет в progressionControl. Для обычных afterlife cycles достаточно processed counts и new last-* ordinals."));
            }
            return;
        }

        if (report.AfterlifeCatchupProcessed != true)
        {
            issues.Add(new ValidationIssue(ReportPath, IssueSeverity.Error,
                "progressionProcessingReport не подтверждает обязательный bounded afterlife catch-up.",
                code: "progression_report_missing_afterlife_catchup_processed",
                section: "ProgressionReport",
                expected: "afterlifeCatchupProcessed=true",
                actual: report.AfterlifeCatchupProcessed?.ToString() ?? "missing",
                repairHint: "Если progressionControl.afterlifeCatchupRequired=true, обработай bounded summary catch-up и укажи afterlifeCatchupProcessed=true."));
        }

        if ((report.AfterlifeCatchupSummaryEventsProcessed ?? 0) != control.AfterlifeCatchupSummaryEventsRequired)
        {
            if (report.AfterlifeCatchupSummaryEventsProcessed == null)
            {
                issues.Add(new ValidationIssue(ReportPath, IssueSeverity.Error,
                    "progressionProcessingReport не содержит afterlifeCatchupSummaryEventsProcessed для обязательного catch-up.",
                    code: "progression_report_missing_afterlife_catchup_summary_events",
                    section: "ProgressionReport",
                    expected: control.AfterlifeCatchupSummaryEventsRequired.ToString(),
                    actual: "missing",
                    repairHint: "Укажи, сколько bounded summary outcomes ГМ реально обработал для catch-up; значение должно совпадать с progressionControl.afterlifeCatchupSummaryEventsRequired."));
                return;
            }

            issues.Add(BuildProgressionMismatchIssue(
                "progression_report_afterlife_catchup_summary_events_mismatch",
                "afterlifeCatchupSummaryEventsProcessed",
                control.AfterlifeCatchupSummaryEventsRequired,
                report.AfterlifeCatchupSummaryEventsProcessed ?? 0,
                "Не пытайся догонять raw elapsed backlog. Обработай ровно bounded summary count из progressionControl."));
        }
    }

    private async Task<ProgressionScheduleState> SanitizeScheduleAsync(ProgressionScheduleState schedule, string? activeTurnRealm = null)
    {
        schedule.WorldCycleMinutes = schedule.WorldCycleMinutes > 0 ? schedule.WorldCycleMinutes : DefaultWorldCycleMinutes;
        schedule.FactionCycleMinutes = schedule.FactionCycleMinutes > 0 ? schedule.FactionCycleMinutes : DefaultFactionCycleMinutes;
        schedule.ChaosSeaCycleEquivalentHours = schedule.ChaosSeaCycleEquivalentHours > 0
            ? schedule.ChaosSeaCycleEquivalentHours
            : DefaultChaosSeaCycleEquivalentHours;
        schedule.AfterlifeCatchupCycleEquivalentMinutes = schedule.AfterlifeCatchupCycleEquivalentMinutes > 0
            ? schedule.AfterlifeCatchupCycleEquivalentMinutes
            : DefaultAfterlifeCatchupCycleEquivalentMinutes;

        var resolvedRealm = activeTurnRealm;
        if (!HasResolvedRealm(resolvedRealm))
            resolvedRealm = await ResolveCurrentRealmAsync(string.Empty);
        if (!HasResolvedRealm(resolvedRealm))
            throw BuildUnresolvedRealmException();

        schedule.CurrentRealm = resolvedRealm ?? string.Empty;
        if (HasResolvedRealm(schedule.CurrentRealm) && !IsAfterlifeRealm(schedule.CurrentRealm))
        {
            schedule.CurrentWorldTimeInMinutes = (await ResolveWorldTimeFromFileAsync(schedule.CurrentWorldTimeInMinutes)).Minutes;
        }

        schedule.PendingWorldCycles = Math.Max(0, schedule.PendingWorldCycles);
        schedule.PendingFactionCycles = Math.Max(0, schedule.PendingFactionCycles);
        schedule.PendingChaosSeaCycles = Math.Max(0, schedule.PendingChaosSeaCycles);
        schedule.PendingGuardianProjectCycles = Math.Max(0, schedule.PendingGuardianProjectCycles);
        schedule.PendingResidentAgencyCycles = Math.Max(0, schedule.PendingResidentAgencyCycles);
        schedule.PendingShiningAbodeCycles = Math.Max(0, schedule.PendingShiningAbodeCycles);
        schedule.PendingShiningFactionCycles = Math.Max(0, schedule.PendingShiningFactionCycles);
        schedule.PendingShiningTradeCycles = Math.Max(0, schedule.PendingShiningTradeCycles);
        schedule.CurrentChaosSeaTurnOrdinal = Math.Max(0, schedule.CurrentChaosSeaTurnOrdinal);
        schedule.LastChaosSeaSimulationOrdinal = Math.Max(0, schedule.LastChaosSeaSimulationOrdinal);
        schedule.LastGuardianProjectCycleOrdinal = Math.Max(0, schedule.LastGuardianProjectCycleOrdinal);
        schedule.LastResidentAgencyCycleOrdinal = Math.Max(0, schedule.LastResidentAgencyCycleOrdinal);
        schedule.LastShiningAbodeCycleOrdinal = Math.Max(0, schedule.LastShiningAbodeCycleOrdinal);
        schedule.LastShiningFactionCycleOrdinal = Math.Max(0, schedule.LastShiningFactionCycleOrdinal);
        schedule.LastShiningTradeCycleOrdinal = Math.Max(0, schedule.LastShiningTradeCycleOrdinal);
        if (!schedule.HasAfterlifeCatchupWorldTimeBaseline)
        {
            schedule.LastAfterlifeCatchupWorldTimeInMinutes = schedule.CurrentWorldTimeInMinutes;
            schedule.HasAfterlifeCatchupWorldTimeBaseline = true;
        }
        else
        {
            schedule.LastAfterlifeCatchupWorldTimeInMinutes = Math.Max(0, schedule.LastAfterlifeCatchupWorldTimeInMinutes);
        }
        schedule.LastUpdatedUtc ??= DateTime.UtcNow.ToString("o");
        return schedule;
    }

    private async Task<ProgressionScheduleSnapshot> ReadScheduleSnapshotAsync()
    {
        var filePresent = _fs.FileExists(SchedulePath);
        var json = await _fs.ReadFileAsync(SchedulePath);
        if (string.IsNullOrWhiteSpace(json))
            return filePresent
                ? new ProgressionScheduleSnapshot(null, ProgressionFileReadState.Malformed, true)
                : new ProgressionScheduleSnapshot(null, ProgressionFileReadState.Missing, false);

        try
        {
            var schedule = JsonSerializer.Deserialize<ProgressionScheduleState>(json, JsonOpts);
            return schedule == null
                ? new ProgressionScheduleSnapshot(null, ProgressionFileReadState.Malformed, true)
                : new ProgressionScheduleSnapshot(schedule, ProgressionFileReadState.Valid, true);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Не удалось разобрать progression_schedule.json");
            return new ProgressionScheduleSnapshot(null, ProgressionFileReadState.Malformed, true);
        }
    }

    private async Task WriteScheduleAsync(ProgressionScheduleState schedule)
    {
        await _fs.WriteFileAtomicAsync(SchedulePath, JsonSerializer.Serialize(schedule, JsonOpts));
    }

    private async Task<ProgressionReportSnapshot> ReadProcessingReportSnapshotAsync()
    {
        var filePresent = _fs.FileExists(ReportPath);
        var json = await _fs.ReadFileAsync(ReportPath);
        if (string.IsNullOrWhiteSpace(json))
            return filePresent
                ? new ProgressionReportSnapshot(null, ProgressionFileReadState.Malformed, true)
                : new ProgressionReportSnapshot(null, ProgressionFileReadState.Missing, false);

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return new ProgressionReportSnapshot(null, ProgressionFileReadState.Malformed, true);

            if (root.TryGetProperty("progressionProcessingReport", out var nested) &&
                nested.ValueKind == JsonValueKind.Object)
            {
                var report = JsonSerializer.Deserialize<ProgressionProcessingReport>(nested.GetRawText(), JsonOpts);
                return report == null
                    ? new ProgressionReportSnapshot(null, ProgressionFileReadState.Malformed, true)
                    : new ProgressionReportSnapshot(report, ProgressionFileReadState.Valid, true);
            }

            var directReport = JsonSerializer.Deserialize<ProgressionProcessingReport>(root.GetRawText(), JsonOpts);
            return directReport == null
                ? new ProgressionReportSnapshot(null, ProgressionFileReadState.Malformed, true)
                : new ProgressionReportSnapshot(directReport, ProgressionFileReadState.Valid, true);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Не удалось разобрать progression_report.json");
            return new ProgressionReportSnapshot(null, ProgressionFileReadState.Malformed, true);
        }
    }

    private async Task<ProgressionProcessingReport?> ReadProcessingReportAsync()
    {
        var snapshot = await ReadProcessingReportSnapshotAsync();
        return snapshot.Report;
    }

    private async Task<PendingTurnRequestContext?> ReadCurrentTurnRequestContextAsync()
    {
        var json = await _fs.ReadFileAsync("input/turn_request.json");
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return null;

            if (!doc.RootElement.TryGetProperty("sessionId", out var sessionIdNode) ||
                sessionIdNode.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(sessionIdNode.GetString()) ||
                !doc.RootElement.TryGetProperty("requestId", out var requestIdNode) ||
                requestIdNode.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(requestIdNode.GetString()) ||
                !doc.RootElement.TryGetProperty("turnNumber", out var turnNumberNode) ||
                turnNumberNode.ValueKind != JsonValueKind.Number ||
                !turnNumberNode.TryGetInt32(out var turnNumber) ||
                turnNumber <= 0)
            {
                return null;
            }

            return new PendingTurnRequestContext(
                sessionIdNode.GetString() ?? string.Empty,
                requestIdNode.GetString() ?? string.Empty,
                turnNumber);
        }
        catch
        {
            return null;
        }
    }

    private bool ValidateProgressionReportCorrelation(
        ProgressionProcessingReport report,
        PendingTurnRequestContext? currentTurnContext,
        List<ValidationIssue> issues)
    {
        if (currentTurnContext == null)
        {
            issues.Add(new ValidationIssue(
                ReportPath,
                IssueSeverity.Error,
                "Не удалось прочитать текущий turn_request для корреляции progression_report.json.",
                code: "progression_report_missing_turn_context",
                section: "ProgressionReport",
                expected: "readable input/turn_request.json with sessionId/requestId/turnNumber",
                actual: "missing or unreadable input/turn_request.json",
                repairHint: "Коррелируй progression_report.json с текущим input/turn_request.json и не принимай stale progression proof без turn context."));
            return false;
        }

        if (!ProgressionReportMatchesCurrentTurn(report, currentTurnContext))
        {
            issues.Add(new ValidationIssue(
                ReportPath,
                IssueSeverity.Error,
                "progression_report.json не совпадает с текущим turn_request и выглядит как stale или чужой progression proof.",
                code: "progression_report_turn_context_mismatch",
                section: "ProgressionReport",
                expected: $"{currentTurnContext.Value.SessionId} / {currentTurnContext.Value.RequestId} / {currentTurnContext.Value.TurnNumber}",
                actual: $"{report.SessionId} / {report.RequestId} / {report.TurnNumber}",
                repairHint: "Записывай в progressionProcessingReport exact sessionId, requestId и turnNumber текущего turn_request, чтобы stale report нельзя было переиспользовать."));
            return false;
        }

        return true;
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

    private static bool IsAfterlifeRealm(string? realm) =>
        IsChaosSeaRealm(realm) || IsShiningAbodeRealm(realm);

    private static bool IsChaosSeaRealm(string? realm) =>
        string.Equals(realm, "Chaos Sea", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(realm, "Море Хаоса", StringComparison.OrdinalIgnoreCase);

    private static bool IsShiningAbodeRealm(string? realm) =>
        string.Equals(realm, "Shining Abode", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(realm, "Сияющая Обитель", StringComparison.OrdinalIgnoreCase);

    private async Task<AfterlifeRealmKind> ResolveAfterlifeRealmKindAsync(string? realm)
    {
        if (IsChaosSeaRealm(realm))
            return AfterlifeRealmKind.ChaosSea;

        if (!IsShiningAbodeRealm(realm))
            return AfterlifeRealmKind.None;

        return await HasPreparedShiningBootstrapPackageAsync()
            ? AfterlifeRealmKind.ShiningBootstrapHandoff
            : AfterlifeRealmKind.ShiningAbode;
    }

    private async Task<bool> HasPreparedShiningBootstrapPackageAsync()
    {
        var json = await _fs.ReadFileAsync(ShiningAbodeState.StatePath);
        if (string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            var root = JsonNode.Parse(json) as JsonObject;
            return root?["preparedIncarnationPackage"] is JsonObject;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Не удалось прочитать preparedIncarnationPackage для progression scheduler; Shining progression будет удержана fail-closed.");
            return true;
        }
    }

    private async Task<bool> HasActiveShiningAbodeWithoutPreparedPackageAsync()
    {
        var json = await _fs.ReadFileAsync(ShiningAbodeState.StatePath);
        if (string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            var root = JsonNode.Parse(json) as JsonObject;
            var availability = root?["availability"]?.GetValue<string>();
            return string.Equals(availability, ShiningAbodeState.AvailabilityActive, StringComparison.OrdinalIgnoreCase) &&
                   root?["preparedIncarnationPackage"] is not JsonObject;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Не удалось прочитать active Shining Abode state для afterlife catch-up contours.");
            return false;
        }
    }

    private static bool ProgressionReportMatchesCurrentTurn(
        ProgressionProcessingReport? report,
        PendingTurnRequestContext? currentTurnContext)
    {
        return report != null &&
               currentTurnContext != null &&
               report.TurnNumber == currentTurnContext.Value.TurnNumber &&
               !string.IsNullOrWhiteSpace(report.SessionId) &&
               string.Equals(report.SessionId, currentTurnContext.Value.SessionId, StringComparison.OrdinalIgnoreCase) &&
               !string.IsNullOrWhiteSpace(report.RequestId) &&
               string.Equals(report.RequestId, currentTurnContext.Value.RequestId, StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasVerifiedAfterlifeProgressionOutcome(
        ProgressionControl control,
        ProgressionProcessingReport? report,
        PendingTurnRequestContext? currentTurnContext)
    {
        if (report == null || !ProgressionReportMatchesCurrentTurn(report, currentTurnContext))
            return false;

        if ((report.WorldCyclesProcessed ?? 0) != 0 || (report.FactionCyclesProcessed ?? 0) != 0)
            return false;

        if ((report.ChaosSeaCyclesProcessed ?? 0) != Math.Max(0, control.ChaosSeaCyclesExpectedThisTurn))
            return false;

        if ((report.GuardianProjectCyclesProcessed ?? 0) != Math.Max(0, control.GuardianProjectCyclesExpectedThisTurn))
            return false;

        if ((report.ResidentAgencyCyclesProcessed ?? 0) != Math.Max(0, control.ResidentAgencyCyclesExpectedThisTurn))
            return false;

        if ((report.ShiningAbodeCyclesProcessed ?? 0) != Math.Max(0, control.ShiningAbodeCyclesExpectedThisTurn))
            return false;

        if ((report.ShiningFactionCyclesProcessed ?? 0) != Math.Max(0, control.ShiningFactionCyclesExpectedThisTurn))
            return false;

        if ((report.ShiningTradeCyclesProcessed ?? 0) != Math.Max(0, control.ShiningTradeCyclesExpectedThisTurn))
            return false;

        if (control.ChaosSeaCyclesExpectedThisTurn > 0 &&
            report.NewLastChaosSeaSimulationOrdinal != control.NextChaosSeaTurnOrdinal)
        {
            return false;
        }

        if (control.GuardianProjectCyclesExpectedThisTurn > 0 &&
            report.NewLastGuardianProjectCycleOrdinal != control.NextGuardianProjectCycleOrdinal)
        {
            return false;
        }

        if (control.ResidentAgencyCyclesExpectedThisTurn > 0 &&
            report.NewLastResidentAgencyCycleOrdinal != control.NextResidentAgencyCycleOrdinal)
        {
            return false;
        }

        if (control.ShiningAbodeCyclesExpectedThisTurn > 0 &&
            report.NewLastShiningAbodeCycleOrdinal != control.NextShiningAbodeCycleOrdinal)
        {
            return false;
        }

        if (control.ShiningFactionCyclesExpectedThisTurn > 0 &&
            report.NewLastShiningFactionCycleOrdinal != control.NextShiningFactionCycleOrdinal)
        {
            return false;
        }

        if (control.ShiningTradeCyclesExpectedThisTurn > 0 &&
            report.NewLastShiningTradeCycleOrdinal != control.NextShiningTradeCycleOrdinal)
        {
            return false;
        }

        if (control.AfterlifeCatchupRequired)
        {
            if (report.AfterlifeCatchupProcessed != true)
                return false;
            if ((report.AfterlifeCatchupSummaryEventsProcessed ?? 0) != control.AfterlifeCatchupSummaryEventsRequired)
                return false;
        }
        else if (report.AfterlifeCatchupProcessed == true)
        {
            return false;
        }

        return true;
    }

    private static ValidationIssue BuildMalformedProgressionReportIssue(
        string code,
        string expectation,
        string repairHint) =>
        new(
            ReportPath,
            IssueSeverity.Error,
            "progression_report.json повреждён, unreadable или не соответствует canonical progressionProcessingReport contract.",
            code: code,
            section: "ProgressionReport",
            expected: expectation,
            actual: "malformed progression_report.json",
            repairHint: repairHint);

    private static ValidationIssue BuildUnresolvedRealmIssue(string? actualRealm) =>
        new(
            SchedulePath,
            IssueSeverity.Error,
            "Не удалось безопасно определить currentRealm для progression control.",
            code: "progression_control_unresolved_current_realm",
            section: "ProgressionSchedule",
            expected: "resolved currentRealm in soul_state or active turn context",
            actual: string.IsNullOrWhiteSpace(actualRealm) ? "missing currentRealm" : actualRealm,
            repairHint: "Восстанови валидный soul_state.currentRealm перед turn-build/apply, чтобы progression ledger не приходилось удерживать fail-closed.");

    private static InvalidOperationException BuildUnresolvedRealmException() =>
        new(
            "game_state/meta/soul_state.json не содержит безопасно читаемый currentRealm. " +
            "Progression ledger сохраняется fail-closed и не может продолжать turn scheduling с неразрешённым realm contract.");

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
               left.LastResidentAgencyCycleOrdinal == right.LastResidentAgencyCycleOrdinal &&
               left.LastShiningAbodeCycleOrdinal == right.LastShiningAbodeCycleOrdinal &&
               left.LastShiningFactionCycleOrdinal == right.LastShiningFactionCycleOrdinal &&
               left.LastShiningTradeCycleOrdinal == right.LastShiningTradeCycleOrdinal &&
               left.PendingChaosSeaCycles == right.PendingChaosSeaCycles &&
               left.PendingGuardianProjectCycles == right.PendingGuardianProjectCycles &&
               left.PendingResidentAgencyCycles == right.PendingResidentAgencyCycles &&
               left.PendingShiningAbodeCycles == right.PendingShiningAbodeCycles &&
               left.PendingShiningFactionCycles == right.PendingShiningFactionCycles &&
               left.PendingShiningTradeCycles == right.PendingShiningTradeCycles &&
               left.ChaosSeaCycleEquivalentHours == right.ChaosSeaCycleEquivalentHours &&
               left.AfterlifeCatchupCycleEquivalentMinutes == right.AfterlifeCatchupCycleEquivalentMinutes &&
               left.LastAfterlifeCatchupWorldTimeInMinutes == right.LastAfterlifeCatchupWorldTimeInMinutes &&
               left.HasAfterlifeCatchupWorldTimeBaseline == right.HasAfterlifeCatchupWorldTimeBaseline;
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
    public int LastResidentAgencyCycleOrdinal { get; set; }
    public int LastShiningAbodeCycleOrdinal { get; set; }
    public int LastShiningFactionCycleOrdinal { get; set; }
    public int LastShiningTradeCycleOrdinal { get; set; }
    public int PendingChaosSeaCycles { get; set; }
    public int PendingGuardianProjectCycles { get; set; }
    public int PendingResidentAgencyCycles { get; set; }
    public int PendingShiningAbodeCycles { get; set; }
    public int PendingShiningFactionCycles { get; set; }
    public int PendingShiningTradeCycles { get; set; }
    public int ChaosSeaCycleEquivalentHours { get; set; } = 24;
    public int AfterlifeCatchupCycleEquivalentMinutes { get; set; } = 1440;
    public int LastAfterlifeCatchupWorldTimeInMinutes { get; set; }
    public bool HasAfterlifeCatchupWorldTimeBaseline { get; set; }
    public string? LastUpdatedUtc { get; set; }
}
