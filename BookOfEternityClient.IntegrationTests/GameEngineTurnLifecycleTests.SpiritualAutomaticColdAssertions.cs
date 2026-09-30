using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    /// <summary>
    /// Supplies complete lifecycle outputs describing the original two-pressure sequence and its single narrated acquisition.
    /// </summary>
    /// <param name="context">
    /// Signed full engine fixture with both original pressure exchanges already authored.
    /// </param>
    /// <param name="request">
    /// Original turn identity, dice and progression control used by the normal output fixture.
    /// </param>
    /// <returns>
    /// A task completing after complete actor reasoning and accurate final narrative are written before C2 captures their bytes.
    /// </returns>
    private static async Task WriteSpiritualAutomaticLifecycleOutputsAsync(ResourceMaterializationTestContext context,
        TurnRequest request)
    {
        await WriteSpiritualLifecycleOutputsAsync(context, request);
        var narrative = Assert.IsType<JsonObject>(await context.ReadJsonAsync("output/narrative_response.json"));
        narrative["response"] = "Душа удерживает встречное давление. Чужое давление надломило волю хранителя. Второе давление искусства Нить надлома вновь отзывается в той же ране, не создавая новой.";
        await context.WriteExactJsonAsync("output/narrative_response.json", narrative.ToJsonString());
        var debug = Assert.IsType<JsonObject>(await context.ReadJsonAsync("output/debug_logs.json"));
        debug["gm_thoughts_markdown"] = string.Join("\n",
            "## NPC Scope", "- Mode: Scene-local", "- Relevant actors: guardian_frame",
            "- Why relevant: Хранитель участвует в двух духовных обменах; меняются его запас действий и духовное состояние.",
            "- Actors outside scope: нет", "- Why outside scope: Другие самостоятельные акторы не участвуют.",
            "", "## Reasoning", "### guardian_frame",
            "- Current location: Море Хаоса; перемещения нет.",
            "- Situation: Хранитель дважды противостоит давлению player_soul; во втором обмене душа использует Нить надлома.",
            "- Profile inputs: guardian_frame — guardian из afterlife_entity_profiles; базовые искусства имеют ранг 0, особые искусства отсутствуют. Искусство art_source_owner принадлежит player_soul, имеет tier 5 и множитель стоимости 200 процентов.",
            "- Thoughts: Я сохраню встречное давление, хотя след первого удара уже мешает удерживать волю.",
            "- Motivation: Сохранить собственную устойчивость в духовном противостоянии.",
            "- Constraints: Оба исходных действия хранителя — pressure по цене 3. Бремя guard не меняет цену pressure. Гарантия ранга I второго источника удовлетворена первой раной и не создаёт новую.",
            "- Strategy options:",
            "1. Сохранить два исходных pressure. Benefit: удержать противостояние. Risk: исчерпать запас действий.",
            "2. Заменить второй pressure на guard. Benefit: перейти к защите. Risk: изменить уже выбранное действие.",
            "- Chosen strategy: Сохранить оба исходных pressure.",
            "- Rejected alternatives: Guard не выбран: порядок и действия исходного хода уже определены.",
            "- Actions: Первый обмен использует исходные кубики 15 и 5 с индексами 0 и 1; второй — 15 и 5 с индексами 2 и 3. Во втором обмене давление души усилено искусством Нить надлома; напряжение хранителя достигает fractured.",
            $"- State changes: {AfterlifeSpiritualConflictState.StatePath} сохраняет оба обмена и исходные кубики. В {ResourceMaterializationContract.StatePath} запас души меняется 6→3→1, хранителя 6→3→0. В {AfterlifeEntityProfileState.StatePath} у guardian_frame появляется один Надлом воли ранга I с бременем guard; второй источник удовлетворяет гарантию той же раной. Личные цели, отношения и память не изменяются.");
        await context.WriteExactJsonAsync("output/debug_logs.json", debug.ToJsonString());
    }

    /// <summary>
    /// Verifies the common publication of one materialized wound and one automatic satisfaction without duplicate effects or charges.
    /// </summary>
    /// <param name="context">
    /// Physical session after the actual cold late-terminal caller completes.
    /// </param>
    /// <param name="engine">
    /// Cold engine retaining the accepted response and final turn cursor.
    /// </param>
    /// <param name="originalDraft">
    /// Exact two-exchange draft whose action, dice, cost and ordering fields must survive publication.
    /// </param>
    /// <param name="woundId">
    /// Exact existing wound identity recorded by the saved automatic satisfaction.
    /// </param>
    /// <param name="materializeFingerprint">
    /// First committed staged fingerprint retained across cold recovery and used to derive the published decision ID.
    /// </param>
    /// <param name="automaticFingerprint">
    /// Staged satisfaction fingerprint retained in the saved submission and used to derive its published decision ID.
    /// </param>
    /// <returns>
    /// A task completing after receipts, resource history, wound/effect history, notification, story and cleanup checks.
    /// </returns>
    private static async Task AssertSpiritualAutomaticPublicationAsync(ResourceMaterializationTestContext context,
        GameEngine engine, byte[] originalDraft, string woundId, string materializeFingerprint, string automaticFingerprint)
    {
        var receipt = Assert.IsType<JsonObject>(await context.ReadJsonAsync(SpiritualWoundOpportunityReceiptState.StatePath));
        var parsedReceipt = SpiritualWoundOpportunityReceiptState.Parse(receipt.ToJsonString(), SpiritualWoundOpportunityReceiptState.StatePath);
        Assert.True(parsedReceipt.IsValid, string.Join("\n", parsedReceipt.Issues));
        Assert.Single(receipt["instances"]!.AsArray());
        Assert.Equal(2, receipt["sources"]!.AsArray().Count);
        var decisions = receipt["decisions"]!.AsArray();
        Assert.Equal(2, decisions.Count);
        Assert.Equal("materialize", decisions[0]!["decision"]!.GetValue<string>());
        Assert.Equal("guarantee_satisfied", decisions[1]!["decision"]!.GetValue<string>());
        Assert.Equal("spiritual_decision_" + materializeFingerprint[7..], decisions[0]!["decisionId"]!.GetValue<string>());
        Assert.Equal("spiritual_decision_" + automaticFingerprint[7..], decisions[1]!["decisionId"]!.GetValue<string>());
        Assert.All(decisions, decision => Assert.Equal(woundId, decision!["woundId"]!.GetValue<string>()));
        Assert.Null(decisions[1]!["transitionId"]);
        Assert.Equal(1, decisions[1]!["satisfiedSeverityRank"]!.GetValue<int>());
        await AssertSpiritualLifecycleMaterializedWoundAsync(context, decisions[0]!);
        var effects = EffectIdentityState.Parse(JsonSerializer.SerializeToElement(
            await context.ReadJsonAsync(EffectIdentityState.StatePath)), EffectIdentityState.StatePath);
        Assert.Empty(effects.Issues);
        var effect = Assert.Single(effects.State!.Entries);
        Assert.Equal(woundId, effect.Source["sourceId"]!.GetValue<string>());
        Assert.Equal(42, effect.CreatedAtTurn);
        Assert.Equal("create", Assert.Single(effect.Transitions).Kind);

        var definitions = ResourceDefinitionCatalog.ParseCanonical(
            await context.FileSystem.ReadFileAsync(ResourceMaterializationContract.DefinitionsPath), allowMissingPristine: false);
        Assert.True(definitions.IsValid, string.Join("\n", definitions.Issues));
        var resources = ResourceStateContract.ParseCanonical(await context.FileSystem.ReadFileAsync(ResourceMaterializationContract.StatePath),
            definitions.Catalog!, allowMissingPristine: false);
        Assert.True(resources.IsValid, string.Join("\n", resources.Issues));
        var balances = resources.Ledger!.Entries.Where(entry => entry.Coordinate.ResourceKey == "spiritual_action_points").ToArray();
        Assert.Equal(2, balances.Length);
        Assert.Equal(1m, Assert.Single(balances, entry => entry.Coordinate.OwnerKind == ResourceOwnerKind.AfterlifeActor).Current);
        Assert.Equal(0m, Assert.Single(balances, entry => entry.Coordinate.OwnerKind == ResourceOwnerKind.AfterlifeConflictSide).Current);
        var history = ResourceHistoryState.ParseCanonical(await context.FileSystem.ReadFileAsync(ResourceMaterializationContract.HistoryPath),
            definitions.Catalog!, allowMissingPristine: false);
        Assert.True(history.IsValid, string.Join("\n", history.Issues));
        var spends = history.History!.Transitions.Where(entry => entry.Turn == 42 &&
            entry.Operation == ResourceTransitionOperation.Spend && entry.Coordinate.ResourceKey == "spiritual_action_points").ToArray();
        Assert.Equal(4, spends.Length);
        foreach (var balance in balances)
        {
            var first = Assert.Single(spends, entry => entry.Coordinate == balance.Coordinate && entry.OriginId == "exchange_conflict_frame_42");
            Assert.Equal(6m, first.BeforeState!.Current);
            Assert.Equal(3m, first.AfterState!.Current);
            Assert.Equal(3m, first.AppliedAmount);
            var second = Assert.Single(spends, entry => entry.Coordinate == balance.Coordinate && entry.OriginId == "exchange_guarantee_satisfied");
            Assert.Equal(3m, second.BeforeState!.Current);
            Assert.Equal(balance.Current, second.AfterState!.Current);
            Assert.Equal(balance.Coordinate.OwnerKind == ResourceOwnerKind.AfterlifeActor ? 2m : 3m, second.AppliedAmount);
        }
        var actualDraft = Assert.IsType<JsonObject>(await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath));
        Assert.False(actualDraft.ContainsKey(AfterlifeSpiritualConflictState.ResponseField));
        Assert.True(JsonNode.DeepEquals(ParseDependentSpiritualBytes(originalDraft)["activeConflict"]!["exchangeLog"],
            actualDraft["activeConflict"]!["exchangeLog"]), "Cold publication changed original exchange fields.");
        Assert.Equal(42, GetPrivateField<GameLoop>(engine, "_gameLoop").TurnNumber);
        var response = GetPrivateField<GameResponse>(engine, "_lastResponse");
        Assert.Contains("Чужое давление надломило волю хранителя.", response.Response, StringComparison.Ordinal);
        var notification = Assert.Single(Assert.IsType<string[]>(response.WoundNotifications));
        Assert.Contains("Получена духовная рана", notification, StringComparison.Ordinal);
        Assert.Contains("Надлом воли", notification, StringComparison.Ordinal);
        var refreshed = Assert.IsType<GameResponse>(await InvokePrivateTaskResultAsync(engine, "BuildGameResponseFromFiles"));
        Assert.True(refreshed.WoundNotifications is null or { Length: 0 });
        var stories = new StoryService(context.FileSystem, NullLogger<StoryService>.Instance);
        var story = Assert.Single(await stories.ReadStoryAsync("stories/chaos_sea.jsonl"), entry => entry.Turn == 42);
        Assert.Equal("Удержать встречное духовное давление.", story.Player);
        Assert.Equal(response.Response, story.Narrative);
        foreach (var path in new[] { "input/turn_request.json", "ready/turn_complete.json", "ready/turn_error.json",
                     "game_state/control/pending_turn_snapshot.json", "game_state/control/pending_turn_snapshot.authority.json",
                     "game_state/control/validation_repair_request.json", "game_state/control/validation_repair_ready.json",
                     SpiritualWoundCaptureCheckpointState.StatePath, SpiritualWoundDecisionPendingState.StatePath,
                     AcceptedMechanicsPlan.WoundCommandPath })
            Assert.False(context.FileSystem.FileExists(path), path);
    }
}
