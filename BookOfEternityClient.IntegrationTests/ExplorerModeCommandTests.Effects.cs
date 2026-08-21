using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class ExplorerModeCommandTests
{
    [Fact]
    public async Task TryProcessCommand_EffectsUsesAcceptedProjectionAndHidesInternalOrGmOnlyState()
    {
        await SeedMortalStateAsync();
        var visible = EffectMaterializationTestFixture.CreateCanonicalEffect();
        var hidden = CreateDistinctEffect("effect_private_hidden", "effect_transition_private_hidden", "gm_only");
        await SeedCanonicalEffectsAsync(visible, hidden);
        await _stateManager.RefreshGameStateAsync();
        var snapshot = await EffectMechanicsSnapshot.LoadAsync(_fs);
        Assert.True(snapshot.IsAccepted, string.Join("\n", snapshot.Issues.Select(static issue =>
            $"{issue.Code}: {issue.FilePath}")));
        Assert.NotEmpty(snapshot.Effects);

        var exception = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/эффекты"));
        var rendered = ExtractRenderedText();

        Assert.Null(exception);
        Assert.Contains("Кровотечение", rendered, StringComparison.Ordinal);
        Assert.Contains("Рана продолжает отнимать силы", rendered, StringComparison.Ordinal);
        Assert.Contains("Рваная рана", rendered, StringComparison.Ordinal);
        Assert.Contains("3", rendered, StringComparison.Ordinal);
        Assert.DoesNotContain("effect_test_bleeding", rendered, StringComparison.Ordinal);
        Assert.DoesNotContain("effect_private_hidden", rendered, StringComparison.Ordinal);
        Assert.DoesNotContain("effect_transition", rendered, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("game_state/", rendered, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("gm_only", rendered, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_EffectsMalformedAuthorityFailsClosedWithoutStatusFallback()
    {
        await SeedMortalStateAsync();
        var visible = EffectMaterializationTestFixture.CreateCanonicalEffect();
        await SeedCanonicalEffectsAsync(visible);
        await WriteRawJsonAsync(
            "game_state/effects/effect_identity_index.json",
            "{\"schemaVersion\":1,\"entries\":[]}");
        await WriteJsonAsync("game_state/core/player_status.json", new
        {
            currentCondition = "PRIVATE FALLBACK CONDITION",
            activeConditions = new[] { "PRIVATE FALLBACK ACTIVE" }
        });
        await _stateManager.RefreshGameStateAsync();

        var exception = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/effects"));
        var rendered = ExtractRenderedText();

        Assert.Null(exception);
        Assert.Contains("Сейчас невозможно надёжно определить действующие эффекты", rendered, StringComparison.Ordinal);
        Assert.DoesNotContain("Кровотечение", rendered, StringComparison.Ordinal);
        Assert.DoesNotContain("PRIVATE FALLBACK", rendered, StringComparison.Ordinal);
        Assert.DoesNotContain("identity", rendered, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("validation", rendered, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_EffectDetailAndActionUseOpaqueAcceptedSelectors()
    {
        await SeedMortalStateAsync();
        var visible = EffectMaterializationTestFixture.CreateCanonicalEffect();
        await SeedCanonicalEffectsAsync(visible);
        await _stateManager.RefreshGameStateAsync();
        var projection = EffectPlayerProjection.Build(new EffectPlayerProjectionInput(
            await EffectMechanicsSnapshot.LoadAsync(_fs),
            Realm: "mortal_world",
            TargetKind: "player",
            TargetId: "player_current"));
        var entry = Assert.Single(projection.Entries);
        var action = Assert.Single(entry.Actions, candidate =>
            candidate.Label.Contains("Противодействовать", StringComparison.Ordinal));

        var detailException = await Record.ExceptionAsync(() =>
            _explorer.TryProcessCommand("/эффекты эффект " + entry.Selector));
        var detailText = ExtractRenderedText();

        Assert.Null(detailException);
        Assert.Contains("Кровотечение", detailText, StringComparison.Ordinal);
        Assert.Contains("Противодействовать", detailText, StringComparison.Ordinal);
        Assert.Contains("не лечит", detailText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(EffectMaterializationTestFixture.EffectId, detailText, StringComparison.Ordinal);
        Assert.DoesNotContain("wound_test_torn_side", detailText, StringComparison.Ordinal);

        var actionException = await Record.ExceptionAsync(() =>
            _explorer.TryProcessCommand("/эффекты действие " + action.Selector));
        var actionText = ExtractRenderedText();

        Assert.Null(actionException);
        Assert.Contains("Подтвердить выбранное противодействие", actionText, StringComparison.Ordinal);
        Assert.Contains("заново проверено", actionText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(EffectMaterializationTestFixture.EffectId, actionText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TryProcessCommand_EffectActionRejectsForgedSelector()
    {
        await SeedMortalStateAsync();
        await SeedCanonicalEffectsAsync(EffectMaterializationTestFixture.CreateCanonicalEffect());
        await _stateManager.RefreshGameStateAsync();

        var exception = await Record.ExceptionAsync(() =>
            _explorer.TryProcessCommand("/effects action effect_action_000000000000000000000000"));
        var rendered = ExtractRenderedText();

        Assert.Null(exception);
        Assert.Contains("действие эффекта больше недоступно", rendered, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(EffectMaterializationTestFixture.EffectId, rendered, StringComparison.Ordinal);
        Assert.DoesNotContain("identity", rendered, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("validation", rendered, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TryProcessCommand_StatusUsesAcceptedEffectsInsteadOfRawActiveConditions()
    {
        await SeedMortalStateAsync();
        var visible = EffectMaterializationTestFixture.CreateCanonicalEffect();
        var hidden = CreateDistinctEffect(
            "effect_private_status",
            "effect_transition_private_status",
            "gm_only");
        await SeedCanonicalEffectsAsync(visible, hidden);
        await WriteJsonAsync("game_state/core/player_status.json", new
        {
            currentCondition = "Собран",
            activeConditions = new[] { "PRIVATE RAW STATUS CONDITION" }
        });
        await _stateManager.RefreshGameStateAsync();

        var exception = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/статус"));
        var rendered = ExtractRenderedText();

        Assert.Null(exception);
        Assert.Contains("Кровотечение", rendered, StringComparison.Ordinal);
        Assert.DoesNotContain("PRIVATE RAW STATUS CONDITION", rendered, StringComparison.Ordinal);
        Assert.DoesNotContain("PRIVATE HIDDEN", rendered, StringComparison.Ordinal);
        Assert.DoesNotContain(EffectMaterializationTestFixture.EffectId, rendered, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TryProcessCommand_CombatUsesAcceptedCombatantEffectProjection()
    {
        await SeedMortalStateAsync();
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect("combatant");
        await WriteRawJsonAsync(
            "game_state/combat/enemies.json",
            new JsonObject
            {
                ["enemiesData"] = new JsonArray(new JsonObject
                {
                    ["combatantId"] = EffectMaterializationTestFixture.CombatantId,
                    ["name"] = "Пепельный налётчик",
                    ["currentHealth"] = 27,
                    ["maxHealth"] = 40,
                    ["activeBuffs"] = new JsonArray(),
                    ["activeDebuffs"] = new JsonArray(effect.DeepClone())
                })
            }.ToJsonString());
        await WriteRawJsonAsync(
            "game_state/effects/effect_identity_index.json",
            CreateDistinctIdentityIndex([effect]).ToJsonString());
        await _stateManager.RefreshGameStateAsync();

        var exception = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/бой"));
        var rendered = ExtractRenderedText();

        Assert.Null(exception);
        Assert.Contains("Пепельный налётчик", rendered, StringComparison.Ordinal);
        Assert.Contains("Кровотечение", rendered, StringComparison.Ordinal);
        Assert.DoesNotContain(EffectMaterializationTestFixture.EffectId, rendered, StringComparison.Ordinal);
        Assert.DoesNotContain("activeDebuffs", rendered, StringComparison.Ordinal);
    }

    private async Task SeedCanonicalEffectsAsync(params JsonObject[] effects)
    {
        var activeEffects = new JsonArray();
        foreach (var effect in effects)
            activeEffects.Add(effect.DeepClone());

        await WriteRawJsonAsync(
            "game_state/player/effects.json",
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["activeEffects"] = activeEffects
            }.ToJsonString());
        await WriteRawJsonAsync(
            "game_state/effects/effect_identity_index.json",
            CreateDistinctIdentityIndex(effects).ToJsonString());
    }

    private static JsonObject CreateDistinctIdentityIndex(IReadOnlyList<JsonObject> effects)
    {
        var index = EffectMaterializationTestFixture.CreateIdentityIndex(effects.ToArray());
        var entries = index["entries"]!.AsArray();
        for (var i = 0; i < effects.Count; i++)
        {
            var chronology = effects[i]["chronology"]!.AsObject();
            var transition = entries[i]!["transitions"]![0]!.AsObject();
            transition["transitionId"] = chronology["lastTransitionId"]!.GetValue<string>();
            transition["eventRef"] = chronology["createdEventRef"]!.GetValue<string>();
        }
        return index;
    }

    private static JsonObject CreateDistinctEffect(
        string effectId,
        string transitionId,
        string visibility)
    {
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect();
        effect["effectId"] = effectId;
        effect["source"]!["sourceId"] = "wound_" + effectId;
        effect["stacking"]!["stackKey"] = "stack_" + effectId;
        effect["chronology"]!["lastTransitionId"] = transitionId;
        effect["chronology"]!["createdEventRef"] = "turn_42:" + effectId;
        effect["display"]!["name"] = "PRIVATE HIDDEN EFFECT";
        effect["display"]!["description"] = "PRIVATE HIDDEN DESCRIPTION";
        effect["display"]!["sourceLabel"] = "PRIVATE HIDDEN SOURCE";
        effect["display"]!["visibility"] = visibility;
        return effect;
    }
}
