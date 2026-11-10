using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>Checks detached combat-healing scheduler DATA without advancing any realm.</summary>
public sealed class SpiritualCombatHealingRealmTests
{
    /// <summary>A settled Shining duel may leave the inactive Chaos contour behind.</summary>
    [Fact]
    public void SettledShiningAllowsBoundedInactiveChaosLagWithoutStartingCycles()
    {
        var (accepted, control) = BuildPreservingInputs(Settled("shining_abode"));

        Assert.True(accepted);
        AssertNoCycleControl(Assert.IsType<ProgressionControl>(control), "shining_abode", 3, 7);
    }

    /// <summary>The existing Chaos route retains its lawful inactive Shining lag.</summary>
    [Fact]
    public void SettledChaosAllowsBoundedInactiveShiningLagWithoutStartingCycles()
    {
        var (accepted, control) = BuildPreservingInputs(Settled("chaos_sea"));

        Assert.True(accepted);
        AssertNoCycleControl(Assert.IsType<ProgressionControl>(control), "chaos_sea", 7, 3);
    }

    /// <summary>Every active or shared completed contour must be settled at ordinal seven.</summary>
    /// <param name="realm">Realm whose no-cycle control is requested.</param>
    /// <param name="contour">One active or shared completed contour to leave overdue.</param>
    [Theory]
    [InlineData("chaos_sea", CompletedContour.ChaosSea)]
    [InlineData("shining_abode", CompletedContour.GuardianProjects)]
    [InlineData("shining_abode", CompletedContour.Residents)]
    [InlineData("shining_abode", CompletedContour.ShiningAbode)]
    [InlineData("shining_abode", CompletedContour.ShiningFactions)]
    [InlineData("shining_abode", CompletedContour.ShiningTrade)]
    public void OverdueActiveOrSharedContourIsRejected(string realm, CompletedContour contour)
    {
        var schedule = Settled(realm);
        switch (contour)
        {
            case CompletedContour.ChaosSea: schedule.LastChaosSeaSimulationOrdinal = 6; break;
            case CompletedContour.GuardianProjects: schedule.LastGuardianProjectCycleOrdinal = 6; break;
            case CompletedContour.Residents: schedule.LastResidentAgencyCycleOrdinal = 6; break;
            case CompletedContour.ShiningAbode: schedule.LastShiningAbodeCycleOrdinal = 6; break;
            case CompletedContour.ShiningFactions: schedule.LastShiningFactionCycleOrdinal = 6; break;
            case CompletedContour.ShiningTrade: schedule.LastShiningTradeCycleOrdinal = 6; break;
            default: throw new ArgumentOutOfRangeException(nameof(contour));
        }

        AssertRejected(schedule);
    }

    /// <summary>Inactive Chaos may lag but cannot have a negative or future completed ordinal.</summary>
    /// <param name="completedOrdinal">Invalid inactive Chaos completed ordinal.</param>
    [Theory]
    [InlineData(-1)]
    [InlineData(8)]
    public void InactiveChaosOutsideCurrentOrdinalBoundsIsRejected(int completedOrdinal)
    {
        var schedule = Settled("shining_abode");
        schedule.LastChaosSeaSimulationOrdinal = completedOrdinal;

        AssertRejected(schedule);
    }

    /// <summary>A pending Shining cycle cannot be hidden by a combat-healing no-cycle control.</summary>
    [Fact]
    public void NonzeroPendingCycleIsRejected()
    {
        var schedule = Settled("shining_abode");
        schedule.PendingShiningFactionCycles = 1;

        AssertRejected(schedule);
    }

    /// <summary>Unknown realm names never acquire combat-healing scheduler authority.</summary>
    [Fact]
    public void UnknownRealmIsRejected()
    {
        var schedule = Settled("shining_abode");
        schedule.CurrentRealm = "unknown_realm";

        AssertRejected(schedule);
    }

    /// <summary>The authoritative absolute world clock must agree with the settled schedule.</summary>
    [Fact]
    public void AbsoluteWorldClockMismatchIsRejected()
    {
        AssertRejected(Settled("shining_abode"), Encoding.UTF8.GetBytes("{\"currentTimeInMinutes\":1320}"));
    }

    /// <summary>One elapsed catchup interval cannot be waived for an otherwise settled duel.</summary>
    [Fact]
    public void ElapsedAfterlifeCatchupIsRejected()
    {
        var schedule = Settled("shining_abode");
        schedule.LastAfterlifeCatchupWorldTimeInMinutes = 1200;

        AssertRejected(schedule);
    }

    /// <summary>Closed intent DATA binds its declared realm to the actual control rather than a payment.</summary>
    [Theory]
    [InlineData("Chaos Sea", "chaos_sea")]
    [InlineData("Shining Abode", "shining_abode")]
    public void IntentRetainsItsControlRealmAndRejectsResealedMismatch(string displayRealm, string canonicalRealm)
    {
        var fingerprint = "sha256:" + new string('a', 64);
        var selection = new SpiritualSelfTreatmentSelection("wound_realm", fingerprint)
        { Combat = new("conflict_realm", fingerprint, 1) };
        var sources = AfterlifeWoundTreatmentAuthority.CombatSourcePaths.Select(path =>
            new AfterlifeWoundTreatmentAuthority.SourceFile(path, new(false, null), null)).ToArray();
        var control = JsonSerializer.SerializeToNode(new ProgressionControl { CurrentRealm = displayRealm })!.AsObject();
        Assert.Equal(displayRealm, control["currentRealm"]!.GetValue<string>());
        Assert.False(control.ContainsKey("CurrentRealm"));
        var intent = SpiritualCombatHealingIntent.Create("session_realm", "request_realm", 42, selection, 3, control, sources);
        Assert.Equal(canonicalRealm, intent.Realm);
        Assert.Equal(1, intent.Root["schemaVersion"]!.GetValue<int>());
        Assert.True(JsonNode.DeepEquals(control, intent.Root["progressionControl"]));
        Assert.Equal(intent.Fingerprint, SpiritualCombatHealingIntent.Parse(intent.Root).Fingerprint);
        foreach (var changedRealm in new[] { canonicalRealm == "chaos_sea" ? "shining_abode" : "chaos_sea", "mortal_world", "" })
        {
            var changed = intent.Root;
            changed["realm"] = changedRealm;
            changed.Remove("intentFingerprint");
            changed["intentFingerprint"] = WoundAcceptedTurnFingerprintWriter.Compute(new[]
                { "book_of_eternity.spiritual_combat_healing.intent", "1", WoundAcceptedTurnFingerprintWriter.CanonicalJson(changed) });
            Assert.Throws<InvalidDataException>(() => SpiritualCombatHealingIntent.Parse(changed));
        }
        control["currentRealm"] = "unknown_realm";
        Assert.Throws<InvalidDataException>(() => SpiritualCombatHealingIntent.Create(
            "session_realm", "request_realm", 42, selection, 3, control, sources));
    }

    /// <summary>Shining mode is current DATA and never inferred from an absent package alone.</summary>
    [Theory]
    [InlineData("active", true)]
    [InlineData("Active", true)]
    [InlineData("missing", false)]
    [InlineData("sealed", false)]
    [InlineData("package", false)]
    [InlineData("malformed", false)]
    public void ShiningModeRequiresItsActualActiveStateWithoutHandoff(string mode, bool expected)
    {
        JsonObject? shining = mode == "missing" ? null : ShiningAbodeState.CreateDefaultState();
        if (mode == "Active") shining!["availability"] = "Active";
        if (mode == "sealed") shining!["availability"] = ShiningAbodeState.AvailabilitySealedUntilNextAscension;
        if (mode == "package") shining!["preparedIncarnationPackage"] = new JsonObject();
        if (mode == "malformed") shining!["preparedIncarnationPackage"] = "broken";
        var before = shining?.ToJsonString();
        Assert.Equal(expected, AfterlifeWoundTreatmentAuthority.HasCurrentCombatRealmSource("shining_abode", shining));
        Assert.Equal(before, shining?.ToJsonString());
        Assert.True(AfterlifeWoundTreatmentAuthority.HasCurrentCombatRealmSource("chaos_sea", shining));
        Assert.False(AfterlifeWoundTreatmentAuthority.HasCurrentCombatRealmSource("mortal_world", shining));
    }

    /// <summary>Identifies a typed completed contour without private-member reflection.</summary>
    public enum CompletedContour
    {
        /// <summary>The Chaos-only simulation contour.</summary>
        ChaosSea,
        /// <summary>The shared guardian-project contour.</summary>
        GuardianProjects,
        /// <summary>The shared resident-agency contour.</summary>
        Residents,
        /// <summary>The Shining abode contour.</summary>
        ShiningAbode,
        /// <summary>The Shining faction contour.</summary>
        ShiningFactions,
        /// <summary>The Shining trade contour.</summary>
        ShiningTrade
    }

    /// <summary>Creates independent settled DATA; the inactive realm last completed ordinal three.</summary>
    /// <param name="realm">Active afterlife realm.</param>
    /// <returns>A fresh complete typed schedule with no pending or elapsed catchup cycles.</returns>
    private static ProgressionScheduleState Settled(string realm) => new()
    {
        CurrentRealm = realm,
        CurrentWorldTimeInMinutes = 1260,
        LastWorldSimulationTimeInMinutes = 1260,
        LastFactionSimulationTimeInMinutes = 1260,
        HasAuthoritativeWorldTimeBaseline = true,
        WorldCycleMinutes = 240,
        FactionCycleMinutes = 1440,
        ChaosSeaCycleEquivalentHours = 24,
        AfterlifeCatchupCycleEquivalentMinutes = 60,
        LastAfterlifeCatchupWorldTimeInMinutes = 1260,
        HasAfterlifeCatchupWorldTimeBaseline = true,
        CurrentChaosSeaTurnOrdinal = 7,
        LastChaosSeaSimulationOrdinal = realm == "chaos_sea" ? 7 : 3,
        LastGuardianProjectCycleOrdinal = 7,
        LastResidentAgencyCycleOrdinal = 7,
        LastShiningAbodeCycleOrdinal = realm == "shining_abode" ? 7 : 3,
        LastShiningFactionCycleOrdinal = realm == "shining_abode" ? 7 : 3,
        LastShiningTradeCycleOrdinal = realm == "shining_abode" ? 7 : 3,
        PendingWorldCycles = 0,
        PendingFactionCycles = 0,
        PendingChaosSeaCycles = 0,
        PendingGuardianProjectCycles = 0,
        PendingResidentAgencyCycles = 0,
        PendingShiningAbodeCycles = 0,
        PendingShiningFactionCycles = 0,
        PendingShiningTradeCycles = 0,
        LastUpdatedUtc = "2026-11-04T00:00:00.0000000Z"
    };

    /// <summary>Calls the actual internal builder and checks that caller-owned input DATA stays unchanged.</summary>
    /// <param name="schedule">Complete typed scheduler DATA to serialize as canonical camel case.</param>
    /// <param name="worldTime">Optional explicit clock bytes; otherwise the matching absolute clock.</param>
    /// <returns>The real admission result and its optional detached control.</returns>
    private static (bool Accepted, ProgressionControl? Control) BuildPreservingInputs(
        ProgressionScheduleState schedule, byte[]? worldTime = null)
    {
        var root = JsonSerializer.SerializeToNode(schedule, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        })!.AsObject();
        worldTime ??= Encoding.UTF8.GetBytes("{\"currentTimeInMinutes\":1260}");
        var rootBefore = root.ToJsonString();
        var bytesBefore = worldTime.ToArray();

        var accepted = ProgressionScheduleService.TryBuildSpiritualCombatControl(root, worldTime, out var control);

        Assert.Equal(rootBefore, root.ToJsonString());
        Assert.Equal(bytesBefore, worldTime);
        return (accepted, control);
    }

    /// <summary>Requires refusal without returning a usable control or mutating the input.</summary>
    /// <param name="schedule">Invalid scheduler DATA for this case.</param>
    /// <param name="worldTime">Optional explicit absolute clock bytes.</param>
    private static void AssertRejected(ProgressionScheduleState schedule, byte[]? worldTime = null)
    {
        var (accepted, control) = BuildPreservingInputs(schedule, worldTime);

        Assert.False(accepted);
        Assert.Null(control);
    }

    /// <summary>Checks a literal no-cycle control, including preservation of inactive completed ordinals.</summary>
    /// <param name="control">Control returned by the real builder.</param>
    /// <param name="realm">Expected realm name.</param>
    /// <param name="chaosOrdinal">Expected completed Chaos ordinal.</param>
    /// <param name="shiningOrdinal">Expected completed and next inactive-or-active Shining ordinal.</param>
    private static void AssertNoCycleControl(ProgressionControl control, string realm, int chaosOrdinal, int shiningOrdinal)
    {
        Assert.Equal(realm, control.CurrentRealm);
        Assert.Equal(1260, control.CurrentWorldTimeInMinutes);
        Assert.Equal(1260, control.LastWorldSimulationTimeInMinutes);
        Assert.Equal(1260, control.LastFactionSimulationTimeInMinutes);
        Assert.Equal(7, control.CurrentChaosSeaTurnOrdinal);
        Assert.Equal(7, control.NextChaosSeaTurnOrdinal);
        Assert.Equal(chaosOrdinal, control.LastChaosSeaSimulationOrdinal);
        Assert.Equal(7, control.LastGuardianProjectCycleOrdinal);
        Assert.Equal(7, control.NextGuardianProjectCycleOrdinal);
        Assert.Equal(7, control.LastResidentAgencyCycleOrdinal);
        Assert.Equal(7, control.NextResidentAgencyCycleOrdinal);
        Assert.Equal(shiningOrdinal, control.LastShiningAbodeCycleOrdinal);
        Assert.Equal(shiningOrdinal, control.NextShiningAbodeCycleOrdinal);
        Assert.Equal(shiningOrdinal, control.LastShiningFactionCycleOrdinal);
        Assert.Equal(shiningOrdinal, control.NextShiningFactionCycleOrdinal);
        Assert.Equal(shiningOrdinal, control.LastShiningTradeCycleOrdinal);
        Assert.Equal(shiningOrdinal, control.NextShiningTradeCycleOrdinal);
        Assert.All(new[] { control.WorldCyclesAlreadyPendingBeforeTurn, control.FactionCyclesAlreadyPendingBeforeTurn,
            control.ChaosSeaCyclesExpectedThisTurn, control.GuardianProjectCyclesExpectedThisTurn,
            control.ResidentAgencyCyclesExpectedThisTurn, control.ShiningAbodeCyclesExpectedThisTurn,
            control.ShiningFactionCyclesExpectedThisTurn, control.ShiningTradeCyclesExpectedThisTurn }, value => Assert.Equal(0, value));
        Assert.All(new[] { control.MustEvaluateWorldProgression, control.MustEvaluateFactionProgression,
            control.MustEvaluateChaosSeaProgression, control.MustEvaluateGuardianProjectProgression,
            control.MustEvaluateResidentAgencyProgression, control.MustEvaluateShiningAbodeProgression,
            control.MustEvaluateShiningFactionProgression, control.MustEvaluateShiningTradeProgression }, value => Assert.False(value));
        Assert.False(control.AfterlifeCatchupRequired);
        Assert.Equal(0, control.AfterlifeCatchupElapsedCycles);
        Assert.Equal("none", control.AfterlifeCatchupPressureTier);
        Assert.Equal(0, control.AfterlifeCatchupSummaryEventsRequired);
        Assert.Empty(control.AfterlifeCatchupContours);
    }
}
