using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class SpiritualWoundCandidateOwnerComposerTests
{
    /// <summary>
    /// Rejects an ordinary companion that rewrites the realm while the effect owner changes effects.
    /// </summary>
    [Fact]
    public void EnsureCompatibleSharedCompanion_RejectsNonEffectDelta()
    {
        var companion = JsonNode.Parse("""
            {"profiles":[{"actorId":"guardian_1","realm":"chaos_sea","activeEffects":[]}]}
            """)!.AsObject();
        var effectCarrier = JsonNode.Parse("""
            {"profiles":[{"actorId":"guardian_1","realm":"shining_abode",
            "activeEffects":[{"effectId":"effect_1"}]}]}
            """)!.AsObject();

        Assert.Throws<InvalidOperationException>(() =>
            SpiritualWoundCandidateOwnerComposer.EnsureCompatibleSharedCompanion(
                EffectCarrierCatalog.AfterlifeProfilesPath, companion, effectCarrier));
        effectCarrier["profiles"]![0]!["realm"] = "chaos_sea";
        SpiritualWoundCandidateOwnerComposer.EnsureCompatibleSharedCompanion(
            EffectCarrierCatalog.AfterlifeProfilesPath, companion, effectCarrier);
    }

    /// <summary>
    /// Projects a real Guardian gacha owner transition without changing the original draft root.
    /// </summary>
    [Fact]
    public void ProjectTypedOwnerRoot_AppendsGuardianHistoryAndConsumesCommand()
    {
        const string original = """
            {"guardians":[{"guardianId":"guardian_1","gachaSystem":
            {"currentReturnCycleId":"return_1","gachaHistory":[]}}],
            "activeGuardian":{"guardianId":"guardian_1","gachaSystem":
            {"currentReturnCycleId":"return_1","gachaHistory":[]}},
            "UpdateGuardians":[{"command":"processGacha","guardianId":"guardian_1"}]}
            """;
        var transition = AcceptedMechanicsOwnerTransition.CreateAfterlifeGuardianGacha(
            "guardian_1", "return_1", new JsonArray(),
            new JsonArray(new JsonObject { ["relicId"] = "relic_1" }));

        var projected = SpiritualWoundCandidateOwnerComposer.ProjectTypedOwnerRoot(
            original, [transition]);

        Assert.False(projected.ContainsKey("UpdateGuardians"));
        var guardian = projected["guardians"]![0]!;
        Assert.Equal("relic_1", (string?)guardian["gachaSystem"]?["gachaHistory"]?[0]?["relicId"]);
        Assert.True(JsonNode.DeepEquals(guardian, projected["activeGuardian"]));
        Assert.Empty(JsonNode.Parse(original)!["guardians"]![0]!["gachaSystem"]!["gachaHistory"]!.AsArray());
    }

    /// <summary>
    /// Rejects a stale Guardian return cycle before projecting a typed candidate owner root.
    /// </summary>
    [Fact]
    public void ProjectTypedOwnerRoot_RejectsStaleGuardianCycle()
    {
        const string original = """
            {"guardians":[{"guardianId":"guardian_1","gachaSystem":
            {"currentReturnCycleId":"return_old","gachaHistory":[]}}]}
            """;
        var transition = AcceptedMechanicsOwnerTransition.CreateAfterlifeGuardianGacha(
            "guardian_1", "return_new", new JsonArray(),
            new JsonArray(new JsonObject { ["relicId"] = "relic_1" }));

        Assert.Throws<InvalidDataException>(() =>
            SpiritualWoundCandidateOwnerComposer.ProjectTypedOwnerRoot(original, [transition]));
    }

    /// <summary>
    /// Preserves the executed source exchange while taking only combat conditions from the effect owner.
    /// </summary>
    [Fact]
    public void MergeExecutedConflict_PreservesSourceHistoryAndEffectConditions()
    {
        var source = JsonNode.Parse("""
            {"realm":"Chaos Sea","activeConflict":{"conflictId":"conflict_1",
            "playerSide":{"leadContestant":{"actorType":"player_soul","actorId":"player_soul"}},
            "oppositionSide":{"leadContestant":{"actorType":"guardian","actorId":"guardian_1"}},
            "exchangeLog":[{"exchangeId":"first"}],"combatConditions":[{"effectId":"stale"}]},
            "recentConflicts":[]}
            """)!.AsObject();
        var effects = JsonNode.Parse("""
            {"realm":"Chaos Sea","activeConflict":{"conflictId":"conflict_1",
            "playerSide":{"leadContestant":{"actorType":"player_soul","actorId":"player_soul"}},
            "oppositionSide":{"leadContestant":{"actorType":"guardian","actorId":"guardian_1"}},
            "exchangeLog":[],"combatConditions":[{"effectId":"current"}]},
            "recentConflicts":[]}
            """)!.AsObject();

        var merged = SpiritualWoundCandidateOwnerComposer.MergeExecutedConflict(source, effects);
        Assert.Equal("first", (string?)merged["activeConflict"]?["exchangeLog"]?[0]?["exchangeId"]);
        Assert.Equal("current", (string?)merged["activeConflict"]?["combatConditions"]?[0]?["effectId"]);
        merged["activeConflict"]!["combatConditions"]![0]!["effectId"] = "mutated";
        Assert.Equal("current", (string?)effects["activeConflict"]?["combatConditions"]?[0]?["effectId"]);
    }

    /// <summary>
    /// Rejects an effect view whose active participant binding differs from the executed source.
    /// </summary>
    [Fact]
    public void MergeExecutedConflict_RejectsForeignParticipant()
    {
        var source = JsonNode.Parse("""
            {"realm":"chaos_sea","activeConflict":{"conflictId":"conflict_1",
            "playerSide":{"leadContestant":{"actorId":"player_soul"}},
            "oppositionSide":{"leadContestant":{"actorId":"guardian_1"}}}}
            """)!.AsObject();
        var effects = source.DeepClone().AsObject();
        effects["activeConflict"]!["oppositionSide"]!["leadContestant"]!["actorId"] = "guardian_2";

        Assert.Throws<InvalidOperationException>(() =>
            SpiritualWoundCandidateOwnerComposer.MergeExecutedConflict(source, effects));
    }
}
