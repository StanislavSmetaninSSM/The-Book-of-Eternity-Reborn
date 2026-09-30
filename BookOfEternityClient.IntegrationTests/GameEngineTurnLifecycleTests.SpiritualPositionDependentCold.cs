using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>
    /// Authors a harmful first pressure and a later no-effect pressure whose signed dice need a saved wound's position correction.
    /// </summary>
    /// <param name="context">
    /// Complete GameEngine-signed fixture with original dice 15, 5, 8, 9 and six action points per side.
    /// </param>
    /// <returns>
    /// A task completing after the lawful original draft preserves canonical position and the second exchange's strain.
    /// </returns>
    internal static async Task WriteSpiritualGameEnginePositionDependentExchangeAsync(ResourceMaterializationTestContext context)
    {
        await WriteCompleteConflictFrameExchangeAsync(context);
        await WriteSourceContinuationAppendAsync(context, duplicateDice: false);
        var candidate = await ReadProjectedSourceContinuationCandidateAsync(context);
        var active = candidate["activeConflict"]!;
        var second = active["exchangeLog"]![1]!;
        second["outcome"] = "no_effect";
        second["after"] = second["before"]!.DeepClone();
        var dice = second["diceAudit"]!;
        dice["diceUsed"]![0]!["value"] = 8;
        dice["diceUsed"]![1]!["value"] = 9;
        dice["playerTotal"] = 8;
        dice["oppositionTotal"] = 9;
        dice["margin"] = -1;
        dice["outcomeBand"] = "mixed_or_no_effect";
        Assert.Empty(dice["modifierBreakdown"]!["player"]!.AsArray());
        Assert.Empty(dice["modifierBreakdown"]!["opposition"]!.AsArray());
        foreach (var side in new[] { "player", "opposition" })
        {
            Assert.Equal("pressure", second["matchupAudit"]![side + "Operation"]!.GetValue<string>());
            Assert.Equal(3, second["actionCostAudit"]![side]!["effectiveCost"]!.GetValue<int>());
            Assert.Equal(3, second["actionCostAudit"]![side]!["before"]!.GetValue<int>());
            Assert.Equal(0, second["actionCostAudit"]![side]!["after"]!.GetValue<int>());
            active[side + "SideStrain"] = second["after"]![side + "SideStrain"]!.DeepClone();
        }
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, candidate.ToJsonString());
    }

    /// <summary>
    /// Creates the existing rank-I guardian wound with its exact pressure-specific position component.
    /// </summary>
    /// <param name="opportunityRef">
    /// Genuine reference supplied by the initial GameEngine continuation offer.
    /// </param>
    /// <returns>
    /// A detached materialization decision carrying magnitude one and no cost burden.
    /// </returns>
    internal static JsonElement CreateSpiritualDependentPositionDecision(string opportunityRef)
    {
        var decision = JsonNode.Parse(OriginalSpiritualWoundDecision(opportunityRef,
            "spiritual_position_burden", "pressure").GetRawText())!;
        decision["proposal"]!["consequenceDefinitions"]![0]!["definition"]!["components"]![0]!["payload"]!["magnitude"] = 1;
        return JsonSerializer.SerializeToElement(decision);
    }
}

public sealed partial class GameEngineTurnLifecycleTests
{
    /// <summary>
    /// Recovers the actual saved position wound in a fresh engine and publishes only its later same-band arithmetic correction once.
    /// </summary>
    /// <returns>
    /// A task completing after real warm GM wait, durable-boundary cancellation, cold correction and final publication agree.
    /// This is a deterministic lifecycle interruption, not an operating-system process-kill test.
    /// </returns>
    [Fact]
    public Task AcceptedTurnSpiritualContinuation_FilePositionDependentDraftResumesSavedChoiceAfterInterruption() =>
        RunDependentSpiritualColdContinuationAsync(worker: false, position: true);

    /// <summary>
    /// Authors the independently known correction from contested plus guardian burden one to player advantage.
    /// </summary>
    /// <param name="conflict">
    /// Original direct two-exchange carrier with uncorrected signed totals eight and nine; only its later dice group is changed.
    /// </param>
    private static void ApplySpiritualPositionDependentCorrection(JsonObject conflict)
    {
        var dice = conflict["activeConflict"]!["exchangeLog"]![1]!["diceAudit"]!;
        Assert.Equal(8, dice["playerTotal"]!.GetValue<int>());
        Assert.Equal(9, dice["oppositionTotal"]!.GetValue<int>());
        Assert.Equal(-1, dice["margin"]!.GetValue<int>());
        Assert.Empty(dice["modifierBreakdown"]!["player"]!.AsArray());
        Assert.Empty(dice["modifierBreakdown"]!["opposition"]!.AsArray());
        dice["modifierBreakdown"]!["player"]!.AsArray().Add(new JsonObject
        {
            ["modifierType"] = "conflict_position", ["source"] = "conflictPosition",
            ["position"] = "player_advantaged", ["value"] = 2
        });
        dice["playerTotal"] = 10;
        dice["margin"] = 1;
    }

    /// <summary>
    /// Checks literal published position arithmetic and unchanged game consequences independently of the authoring helper.
    /// </summary>
    /// <param name="original">
    /// Frozen complete draft from before initial selection.
    /// </param>
    /// <param name="published">
    /// Actual canonical conflict after the cold GameEngine caller completes publication.
    /// </param>
    /// <param name="receipt">
    /// Actual client-owned receipt, which must contain only the first harmful source and its one decision.
    /// </param>
    private static void AssertSpiritualPositionDependentPublication(JsonObject original, JsonObject published, JsonObject receipt)
    {
        var originalLog = original["activeConflict"]!["exchangeLog"]!.AsArray();
        var actualLog = published["activeConflict"]!["exchangeLog"]!.AsArray();
        Assert.Equal(2, actualLog.Count);
        Assert.True(JsonNode.DeepEquals(originalLog[0], actualLog[0]), "The already-closed first exchange changed.");
        Assert.Equal("exchange_conflict_frame_42", Assert.Single(receipt["sources"]!.AsArray())!["witness"]!["exchangeId"]!.GetValue<string>());
        Assert.Single(receipt["decisions"]!.AsArray());
        var second = actualLog[1]!;
        Assert.Equal("no_effect", second["outcome"]!.GetValue<string>());
        Assert.True(JsonNode.DeepEquals(second["before"], second["after"]), "Same-band correction invented new consequences.");
        Assert.Equal("contested", published["activeConflict"]!["conflictPosition"]!.GetValue<string>());
        foreach (var side in new[] { "player", "opposition" })
        {
            Assert.Equal("pressure", second["matchupAudit"]![side + "Operation"]!.GetValue<string>());
            Assert.Equal(3, second["actionCostAudit"]![side]!["effectiveCost"]!.GetValue<int>());
            Assert.True(JsonNode.DeepEquals(second["after"]![side + "SideStrain"],
                published["activeConflict"]![side + "SideStrain"]));
        }
        foreach (var exchange in actualLog)
        {
            Assert.Equal("contested", exchange!["before"]!["conflictPosition"]!.GetValue<string>());
            Assert.Equal("contested", exchange["after"]!["conflictPosition"]!.GetValue<string>());
        }
        var dice = second["diceAudit"]!;
        Assert.Equal(8, dice["diceUsed"]![0]!["value"]!.GetValue<int>());
        Assert.Equal(9, dice["diceUsed"]![1]!["value"]!.GetValue<int>());
        Assert.Equal(2, dice["diceUsed"]![0]!["sourceIndex"]!.GetValue<int>());
        Assert.Equal(3, dice["diceUsed"]![1]!["sourceIndex"]!.GetValue<int>());
        Assert.Equal(10, dice["playerTotal"]!.GetValue<int>());
        Assert.Equal(9, dice["oppositionTotal"]!.GetValue<int>());
        Assert.Equal(1, dice["margin"]!.GetValue<int>());
        Assert.Equal("mixed_or_no_effect", dice["outcomeBand"]!.GetValue<string>());
        var modifier = Assert.Single(dice["modifierBreakdown"]!["player"]!.AsArray())!;
        Assert.Equal("conflict_position", modifier["modifierType"]!.GetValue<string>());
        Assert.Equal("conflictPosition", modifier["source"]!.GetValue<string>());
        Assert.Equal("player_advantaged", modifier["position"]!.GetValue<string>());
        Assert.Equal(2, modifier["value"]!.GetValue<int>());
        Assert.Empty(dice["modifierBreakdown"]!["opposition"]!.AsArray());
    }

    /// <summary>
    /// Supplies accurate normal outputs for both original pressure exchanges before the actual continuation caller starts.
    /// </summary>
    /// <param name="context">
    /// Complete signed fixture containing the two-exchange position-dependent draft.
    /// </param>
    /// <param name="request">
    /// Original request whose identity and ready signal are preserved through cold recovery.
    /// </param>
    /// <returns>
    /// A task completing after the standard outputs and matching guardian reasoning are written.
    /// </returns>
    private static async Task WritePositionDependentSpiritualLifecycleOutputsAsync(ResourceMaterializationTestContext context,
        TurnRequest request)
    {
        await WriteSpiritualLifecycleOutputsAsync(context, request);
        var debug = Assert.IsType<JsonObject>(await context.ReadJsonAsync("output/debug_logs.json"));
        debug["gm_thoughts_markdown"] = string.Join("\n",
            "## NPC Scope",
            "- Mode: Scene-local",
            "- Relevant actors: guardian_frame",
            "- Why relevant: Хранитель участвует в двух духовных обменах; меняются его запас действий и духовное состояние.",
            "- Actors outside scope: нет",
            "- Why outside scope: Другие самостоятельные акторы не участвуют.",
            "", "## Reasoning", "### guardian_frame",
            "- Current location: Море Хаоса; перемещения нет.",
            "- Situation: Хранитель сохраняет встречное pressure в обоих обменах с player_soul.",
            "- Profile inputs: guardian_frame — guardian из afterlife_entity_profiles; базовые искусства имеют ранг 0, особые искусства отсутствуют.",
            "- Thoughts: Продолжу давление, хотя первый натиск надломил мою волю.",
            "- Motivation: Сохранить собственную устойчивость в духовном противостоянии.",
            "- Constraints: Каждое pressure стоит 3; возможная рана даёт штраф стартовой позиции pressure величиной 1, но не меняет цену или каноническую позицию.",
            "- Strategy options:",
            "1. Продолжить исходное pressure. Benefit: сохранить выбранное действие. Risk: потратить оставшиеся силы без нового результата.",
            "2. Перейти к защите. Benefit: сосредоточиться на устойчивости. Risk: отказаться от уже выбранного давления.",
            "- Chosen strategy: Сохранить исходную последовательность pressure и pressure.",
            "- Rejected alternatives: Guard не выбран: действия исходного хода уже определены.",
            "- Actions: Первый обмен использует исходные кубики 15 и 5. Второй использует 8 и 9; margin -1 и 1 после позиционного бонуса player +2 остаются mixed_or_no_effect. Второй no_effect не меняет напряжение.",
            $"- State changes: {AfterlifeSpiritualConflictState.StatePath} содержит оба исходных обмена; {AfterlifeEntityProfileState.StatePath} отражает состояние guardian_frame. В {ResourceMaterializationContract.StatePath} оба запаса spiritual_action_points идут 6→3→0. Исправление ограничено позиционным модификатором, playerTotal и margin второго обмена. Личные цели, отношения и память не изменяются.");
        await context.WriteExactJsonAsync("output/debug_logs.json", debug.ToJsonString());
    }
}
