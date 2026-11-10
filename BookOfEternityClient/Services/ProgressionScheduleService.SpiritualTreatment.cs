using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Models;

namespace BookOfEternityClient.Services;

public partial class ProgressionScheduleService
{
    /// <summary>Checks detached prepared scheduler DATA without initializing, advancing or rewriting it.</summary>
    internal static bool TryBindPreparedSpiritualCycle(JsonObject? root, byte[]? worldTime, JsonObject? shiningState, ProgressionControl control,
        string realm, out int ordinal)
    {
        ordinal = 0;
        if (root is null || !AfterlifeEntityProfileState.TryNormalizeEffectRealm(control.CurrentRealm, out var controlRealm) ||
            controlRealm != realm || realm is not ("chaos_sea" or "shining_abode") ||
            (realm == "shining_abode" && ShiningAbodeState.GetPreparedIncarnationPackageMode(shiningState) !=
                ShiningAbodeState.PreparedIncarnationPackageMode.Absent))
            return false;
        // Canonical scheduler serialization supplies every state field. Refuse
        // malformed/partial DATA instead of applying DTO defaults as authority.
        var fieldTemplate = JsonSerializer.SerializeToNode(new ProgressionScheduleState(), JsonOpts)!.AsObject();
        if (fieldTemplate.Any(p => !root.ContainsKey(p.Key))) return false;
        var schedule = root.Deserialize<ProgressionScheduleState>(JsonOpts);
        if (schedule is null || !AfterlifeEntityProfileState.TryNormalizeEffectRealm(schedule.CurrentRealm, out var scheduleRealm) ||
            scheduleRealm != realm || schedule.CurrentChaosSeaTurnOrdinal < 0 || schedule.CurrentChaosSeaTurnOrdinal == int.MaxValue ||
            schedule.PendingWorldCycles != 0 || schedule.PendingFactionCycles != 0 ||
            schedule.PendingChaosSeaCycles != (realm == "chaos_sea" ? 1 : 0) ||
            schedule.PendingGuardianProjectCycles != 1 || schedule.PendingResidentAgencyCycles != 1 ||
            schedule.PendingShiningAbodeCycles != (realm == "shining_abode" ? 1 : 0) ||
            schedule.PendingShiningFactionCycles != (realm == "shining_abode" ? 1 : 0) ||
            schedule.PendingShiningTradeCycles != (realm == "shining_abode" ? 1 : 0) ||
            control.AfterlifeCatchupRequired ||
            BuildAfterlifeCatchupContext(schedule,
                realm == "chaos_sea" ? AfterlifeRealmKind.ChaosSea : AfterlifeRealmKind.ShiningAbode,
                includeShiningContoursInMortalCatchup: false).Required)
            return false;
        var contours = new[] { schedule.LastChaosSeaSimulationOrdinal, schedule.LastGuardianProjectCycleOrdinal,
            schedule.LastResidentAgencyCycleOrdinal, schedule.LastShiningAbodeCycleOrdinal,
            schedule.LastShiningFactionCycleOrdinal, schedule.LastShiningTradeCycleOrdinal };
        if (contours.Any(last => last < 0 || last > schedule.CurrentChaosSeaTurnOrdinal) ||
            schedule.LastGuardianProjectCycleOrdinal != schedule.CurrentChaosSeaTurnOrdinal ||
            schedule.LastResidentAgencyCycleOrdinal != schedule.CurrentChaosSeaTurnOrdinal ||
            (realm == "chaos_sea" && schedule.LastChaosSeaSimulationOrdinal != schedule.CurrentChaosSeaTurnOrdinal) ||
            (realm == "shining_abode" && (schedule.LastShiningAbodeCycleOrdinal != schedule.CurrentChaosSeaTurnOrdinal ||
                schedule.LastShiningFactionCycleOrdinal != schedule.CurrentChaosSeaTurnOrdinal ||
                schedule.LastShiningTradeCycleOrdinal != schedule.CurrentChaosSeaTurnOrdinal)) ||
            schedule.CurrentWorldTimeInMinutes < 0 || schedule.LastWorldSimulationTimeInMinutes < 0 ||
            schedule.LastFactionSimulationTimeInMinutes < 0 || schedule.WorldCycleMinutes <= 0 ||
            schedule.FactionCycleMinutes <= 0 || schedule.ChaosSeaCycleEquivalentHours <= 0 ||
            schedule.AfterlifeCatchupCycleEquivalentMinutes <= 0 ||
            !schedule.HasAfterlifeCatchupWorldTimeBaseline || schedule.LastAfterlifeCatchupWorldTimeInMinutes < 0 ||
            schedule.LastAfterlifeCatchupWorldTimeInMinutes > schedule.CurrentWorldTimeInMinutes)
            return false;
        var currentTime = ResolveFrozenWorldTime(worldTime, schedule.CurrentWorldTimeInMinutes,
            allowIncrementalTimeChange: false);
        if (!currentTime.HasUnresolvedAbsoluteOverride && currentTime.Minutes != schedule.CurrentWorldTimeInMinutes)
            return false;
        var expected = BuildProgressionControl(schedule, 0, 0,
            new AfterlifeCatchupContext(false, 0, "none", 0, Array.Empty<string>()));
        if (!JsonNode.DeepEquals(JsonSerializer.SerializeToNode(control, JsonOpts),
                JsonSerializer.SerializeToNode(expected, JsonOpts)))
            return false;
        ordinal = expected.NextChaosSeaTurnOrdinal;
        return true;
    }
}
