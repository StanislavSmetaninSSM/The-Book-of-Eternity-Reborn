using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>
    /// Seeds actual tier-two pressure authority before the full engine signs its original snapshot.
    /// </summary>
    /// <param name="context">
    /// Full original fixture with both actor profiles and the continuing conflict already present.
    /// </param>
    /// <returns>
    /// A task completing after profile, soul and original opposition tier mirrors agree.
    /// </returns>
    internal static async Task SeedSpiritualWorseningTierAuthorityAsync(ResourceMaterializationTestContext context)
    {
        var profiles = Assert.IsType<JsonObject>(await context.ReadJsonAsync(AfterlifeEntityProfileState.StatePath));
        foreach (var profile in profiles["profiles"]!.AsArray())
            profile!["standardArts"]!["pressure"] = 2;
        await context.WriteExactJsonAsync(AfterlifeEntityProfileState.StatePath, profiles.ToJsonString());
        const string soulPath = "game_state/meta/soul_state.json";
        var soul = Assert.IsType<JsonObject>(await context.ReadJsonAsync(soulPath));
        soul[AfterlifeSpiritualConflictState.SoulStateProfileProperty]!["artTiers"]!["pressure"] = 2;
        await context.WriteExactJsonAsync(soulPath, soul.ToJsonString());
        var conflict = Assert.IsType<JsonObject>(await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath));
        conflict["activeConflict"]!["oppositionSide"]!["leadContestant"]!["actorArtTierSnapshot"]!["pressure"] = 2;
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, conflict.ToJsonString());
    }

    /// <summary>
    /// Authors two complete original pressure exchanges that offer creation followed by worsening of the same conflict wound.
    /// </summary>
    /// <param name="context">
    /// Full engine original signed with dice 15, 5, 18, 3 and tier-two pressure on both sides.
    /// </param>
    /// <returns>
    /// A task completing after the raw draft has chronological costs one and one for each actor.
    /// </returns>
    internal static async Task WriteSpiritualWorseningExchangesAsync(ResourceMaterializationTestContext context)
    {
        await WriteCompleteConflictFrameExchangeAsync(context);
        var root = await ReadProjectedSourceContinuationCandidateAsync(context);
        var active = root["activeConflict"]!.AsObject();
        var first = active["exchangeLog"]![0]!.AsObject();
        foreach (var side in new[] { "player", "opposition" })
        {
            first["actionCostAudit"]![side]!["artTier"] = 2;
            first["actionCostAudit"]![side]!["effectiveCost"] = 1;
            first["actionCostAudit"]![side]!["after"] = 5;
        }
        var second = first.DeepClone().AsObject();
        second["exchangeId"] = "exchange_engine_worsening_42";
        second["before"] = first["after"]!.DeepClone();
        second["after"] = second["before"]!.DeepClone();
        second["after"]!["oppositionSideStrain"] = "broken";
        var dice = second["diceAudit"]!;
        dice["diceUsed"]![0]!["sourceIndex"] = 2;
        dice["diceUsed"]![0]!["value"] = 18;
        dice["diceUsed"]![1]!["sourceIndex"] = 3;
        dice["diceUsed"]![1]!["value"] = 3;
        dice["playerTotal"] = 18;
        dice["oppositionTotal"] = 3;
        dice["margin"] = 15;
        dice["outcomeBand"] = "decisive_player_success";
        foreach (var side in new[] { "player", "opposition" })
        {
            second["actionCostAudit"]![side]!["before"] = 5;
            second["actionCostAudit"]![side]!["after"] = 4;
        }
        active["exchangeLog"]!.AsArray().Add(second);
        active["oppositionSideStrain"] = "broken";
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, root.ToJsonString());
    }

    /// <summary>
    /// Authors the existing guard-burden proposal, adding its second required consequence for a rank-II worsening.
    /// </summary>
    /// <param name="opportunityRef">
    /// Exact opportunity reference from the actual issued request.
    /// </param>
    /// <param name="worsening">
    /// <see langword="true"/> for rank II with a maneuver consequence and distinct later narration; <see langword="false"/> for the first rank-I acquisition.
    /// </param>
    /// <returns>
    /// A detached GM decision containing no client identity or private continuation authority.
    /// </returns>
    internal static JsonElement CreateSpiritualWorseningLifecycleDecision(string opportunityRef, bool worsening)
    {
        var decision = JsonNode.Parse(OriginalSpiritualWoundDecision(opportunityRef,
            "spiritual_action_cost_burden", "guard").GetRawText())!;
        if (worsening)
        {
            decision["proposal"]!["severity"] = "II";
            decision["proposal"]!["display"]!["acquisitionNarration"] = "Новый натиск углубил надлом воли хранителя.";
            var definitions = decision["proposal"]!["consequenceDefinitions"]!.AsArray();
            var extra = definitions[0]!.DeepClone();
            extra["definitionRef"] = "maneuver_burden";
            extra["definition"]!["definitionKey"] = "maneuver_burden";
            extra["definition"]!["stacking"]!["stackKey"] = "stack_maneuver_burden";
            extra["definition"]!["components"]![0]!["payload"]!["operation"] = "maneuver";
            definitions.Add(extra);
        }
        return JsonSerializer.SerializeToElement(decision);
    }
}

public sealed partial class GameEngineTurnLifecycleTests
{
    /// <summary>
    /// Writes complete ordinary lifecycle outputs explaining both original exchanges before any wound decision is submitted.
    /// </summary>
    /// <param name="context">
    /// Full signed engine fixture with the two-exchange draft.
    /// </param>
    /// <param name="request">
    /// Original turn identity and progression control used by the accepted terminal signal.
    /// </param>
    /// <returns>
    /// A task completing after the normal outputs contain complete guardian reasoning and the exact two-exchange costs.
    /// </returns>
    private static async Task WriteSpiritualWorseningLifecycleOutputsAsync(ResourceMaterializationTestContext context, TurnRequest request)
    {
        await WriteSpiritualLifecycleOutputsAsync(context, request);
        var debug = Assert.IsType<JsonObject>(await context.ReadJsonAsync("output/debug_logs.json"));
        debug["gm_thoughts_markdown"] = string.Join("\n",
            "## NPC Scope", "- Mode: Scene-local", "- Relevant actors: guardian_frame",
            "- Why relevant: Хранитель участвует в двух духовных обменах; меняются его запас действий и духовное состояние.",
            "- Actors outside scope: нет", "- Why outside scope: Другие самостоятельные акторы не участвуют.",
            "", "## Reasoning", "### guardian_frame",
            "- Current location: Море Хаоса; перемещения нет.",
            "- Situation: Хранитель дважды противостоит давлению player_soul и сохраняет выбранное встречное давление.",
            "- Profile inputs: guardian_frame — guardian из afterlife_entity_profiles; pressure имеет ранг 2 у обоих участников, остальные базовые искусства имеют ранг 0, особые искусства отсутствуют.",
            "- Thoughts: Я продолжу встречное давление, хотя второй натиск сильнее первого.",
            "- Motivation: Сохранить собственную устойчивость в духовном противостоянии.",
            "- Constraints: Оба исходных действия хранителя — pressure по цене 1. Возможные последствия для guard и maneuver не меняют исходное pressure.",
            "- Strategy options:",
            "1. Продолжить оба исходных pressure. Benefit: сохранить инициативу. Risk: усилить напряжение.",
            "2. Перейти к защите. Benefit: удержать устойчивость. Risk: изменить уже выбранное действие.",
            "- Chosen strategy: Выполнить оба исходных pressure.",
            "- Rejected alternatives: Guard не выбран: порядок и действия исходного хода уже определены.",
            "- Actions: Первый обмен использует исходные кубики 15 и 5 с индексами 0 и 1; второй — 18 и 3 с индексами 2 и 3. Напряжение хранителя изменяется clear→strained→broken. Действия и все исходные кубики сохраняются.",
            $"- State changes: {AfterlifeSpiritualConflictState.StatePath} сохраняет два обмена. В {ResourceMaterializationContract.StatePath} запас каждого участника меняется 6→5→4. В {AfterlifeEntityProfileState.StatePath} допустимый Надлом воли определяется отдельными решениями клиента; второй источник может ухудшить ту же рану. Личные цели, отношения и память не изменяются.");
        await context.WriteExactJsonAsync("output/debug_logs.json", debug.ToJsonString());
    }
}
