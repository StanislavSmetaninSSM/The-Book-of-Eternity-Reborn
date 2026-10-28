using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;

namespace BookOfEternityClient.Services;

public partial class ProgressionScheduleService
{
    internal sealed record CombatControlPreparation(ProgressionControl Control,
        IReadOnlyList<AfterlifeWoundTreatmentAuthority.SourceFile> Sources);

    // This is the scheduler's actual held-lease staging route. It never calls
    // EnsureInitialized/Sanitize or ordinary BuildControl and never writes pending
    // cycles/LastUpdatedUtc. Unsupported backlog remains physically untouched.
    internal async Task<CombatControlPreparation> BuildCombatControlForNextTurnAsync(
        FileSystemManager.CanonicalWriteLease lease, SpiritualCombatTreatmentSelection selection)
    {
        _fs.EnsureCanonicalWriteLeaseActive(lease);
        _fs.VerifyCurrentSessionOperation(lease);
        var sources = new List<AfterlifeWoundTreatmentAuthority.SourceFile>();
        foreach (var path in new[] { SchedulePath, AfterlifeSpiritualConflictState.StatePath,
                     "game_state/meta/soul_state.json", EffectAcceptedTurnInputComposer.WorldTimePath, ShiningAbodeState.StatePath })
        {
            var bytes = await _fs.ReadFileBytesAsync(lease, path);
            var root = bytes is null ? null : StrictJsonAuthority.Deserialize<JsonObject>(
                CanonicalJsonUtf8.DecodeOneOptionalBom(bytes), new JsonSerializerOptions(), path);
            sources.Add(new(path, new(bytes is not null, bytes), root));
        }
        JsonObject? Root(string path) => sources.Single(s => s.Path == path).Root;
        if (Root(AfterlifeSpiritualConflictState.StatePath)?["activeConflict"] is not JsonObject active ||
            !AfterlifeWoundTreatmentAuthority.IsSelectedCombatConflict(active, selection) ||
            !AfterlifeEntityProfileState.TryNormalizeEffectRealm(Root("game_state/meta/soul_state.json")?["currentRealm"]?.GetValue<string>(), out var realm) || realm != "chaos_sea" ||
            !TryBuildSpiritualCombatControl(Root(SchedulePath),
                sources.Single(s => s.Path == EffectAcceptedTurnInputComposer.WorldTimePath).Image.Bytes, out var control))
            throw new InvalidDataException("Combat healing requires its actual direct duel and a settled zero-pending schedule.");
        return new(control!, sources.AsReadOnly());
    }

    // Recomputable scheduler DATA shared by staging, original binding and settled
    // replay. An all-zero caller DTO alone never admits a combat publication.
    internal static bool TryBuildSpiritualCombatControl(JsonObject? root, byte[]? worldTime,
        out ProgressionControl? control)
    {
        control = null;
        if (root is null || JsonSerializer.SerializeToNode(new ProgressionScheduleState(), JsonOpts)!
                .AsObject().Any(p => !root.ContainsKey(p.Key))) return false;
        var schedule = root.Deserialize<ProgressionScheduleState>(JsonOpts);
        if (schedule is null || !AfterlifeEntityProfileState.TryNormalizeEffectRealm(schedule.CurrentRealm, out var realm) || realm != "chaos_sea" ||
            schedule.CurrentChaosSeaTurnOrdinal < 0 ||
            schedule.CurrentChaosSeaTurnOrdinal == int.MaxValue ||
            new[] { schedule.PendingWorldCycles, schedule.PendingFactionCycles, schedule.PendingChaosSeaCycles,
                schedule.PendingGuardianProjectCycles, schedule.PendingResidentAgencyCycles, schedule.PendingShiningAbodeCycles,
                schedule.PendingShiningFactionCycles, schedule.PendingShiningTradeCycles }.Any(v => v != 0) ||
            schedule.LastChaosSeaSimulationOrdinal != schedule.CurrentChaosSeaTurnOrdinal ||
            schedule.LastGuardianProjectCycleOrdinal != schedule.CurrentChaosSeaTurnOrdinal ||
            schedule.LastResidentAgencyCycleOrdinal != schedule.CurrentChaosSeaTurnOrdinal ||
            new[] { schedule.LastShiningAbodeCycleOrdinal, schedule.LastShiningFactionCycleOrdinal,
                schedule.LastShiningTradeCycleOrdinal }.Any(v => v < 0 || v > schedule.CurrentChaosSeaTurnOrdinal) ||
            schedule.CurrentWorldTimeInMinutes < 0 || schedule.LastWorldSimulationTimeInMinutes < 0 ||
            schedule.LastFactionSimulationTimeInMinutes < 0 || schedule.WorldCycleMinutes <= 0 || schedule.FactionCycleMinutes <= 0 ||
            schedule.ChaosSeaCycleEquivalentHours <= 0 || schedule.AfterlifeCatchupCycleEquivalentMinutes <= 0 ||
            !schedule.HasAfterlifeCatchupWorldTimeBaseline || schedule.LastAfterlifeCatchupWorldTimeInMinutes < 0 ||
            schedule.LastAfterlifeCatchupWorldTimeInMinutes > schedule.CurrentWorldTimeInMinutes ||
            BuildAfterlifeCatchupContext(schedule, AfterlifeRealmKind.ChaosSea, false).Required)
            return false;
        var time = ResolveFrozenWorldTime(worldTime, schedule.CurrentWorldTimeInMinutes, allowIncrementalTimeChange: false);
        if (time.HasUnresolvedAbsoluteOverride || time.Minutes != schedule.CurrentWorldTimeInMinutes) return false;
        control = BuildProgressionControl(schedule, 0, 0, new(false, 0, "none", 0, Array.Empty<string>()));
        return true;
    }
}
