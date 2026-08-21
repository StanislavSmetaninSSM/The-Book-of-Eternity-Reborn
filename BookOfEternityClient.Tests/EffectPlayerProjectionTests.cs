using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class EffectPlayerProjectionTests
{
    [Fact]
    public void Build_VisibleEffectProjectsReadableMechanicsWithoutPermanentAuthority()
    {
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect();
        effect["stacking"]!["currentStacks"] = 2;
        var projection = BuildProjection(effect);

        Assert.True(projection.IsAvailable);
        var entry = Assert.Single(projection.Entries);
        Assert.Equal("Кровотечение", entry.Name);
        Assert.Equal("Рана продолжает отнимать силы.", entry.Summary);
        Assert.Equal("active", entry.State);
        Assert.StartsWith("effect_view_", entry.Selector, StringComparison.Ordinal);
        Assert.Contains(entry.Facts, fact => fact.Kind == "category" && fact.Value == "Ослабление");
        Assert.Contains(entry.Facts, fact => fact.Kind == "source" && fact.Value == "Рваная рана");
        Assert.Contains(entry.Facts, fact => fact.Kind == "stacks" && fact.Value == "2/3");
        Assert.Contains(entry.Facts, fact => fact.Kind == "lifetime" && fact.Value.Contains("три хода", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(entry.Facts, fact => fact.Kind == "periodic_damage" && fact.Value.Contains("3", StringComparison.Ordinal));
        Assert.Contains(entry.Actions, action => action.Label.Contains("физическое лечение", StringComparison.Ordinal));
        Assert.Contains(entry.Actions, action => action.Label.Contains("остановить кровотечение", StringComparison.Ordinal));
        Assert.DoesNotContain(entry.Actions, action => action.Label.Contains('_'));

        var serialized = JsonSerializer.Serialize(projection);
        Assert.DoesNotContain(EffectMaterializationTestFixture.EffectId, serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("wound_test_torn_side", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain(EffectMaterializationTestFixture.TransitionId, serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("game_state/", serialized, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("turns", "Осталось ходов")]
    [InlineData("uses", "Осталось применений")]
    [InlineData("until_time", "До отметки времени")]
    [InlineData("scene", "До завершения текущей сцены")]
    [InlineData("source_bound", "Пока действует связанный источник")]
    [InlineData("condition_bound", "Пока выполняется связанное условие")]
    [InlineData("permanent", "Постоянный эффект")]
    [InlineData("manual", "До явного снятия")]
    public void Build_EveryLifetimeModeHasCompleteReadableState(
        string mode,
        string expected)
    {
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect();
        effect["lifetime"] = CreateLifetime(mode);
        if (string.Equals(mode, "uses", StringComparison.Ordinal))
            effect["triggers"]![0]!["consumeUses"] = true;

        var entry = Assert.Single(BuildProjection(effect).Entries);
        var fact = Assert.Single(entry.Facts, candidate => candidate.Kind == "lifetime");

        Assert.Contains(expected, fact.Value, StringComparison.Ordinal);
        Assert.DoesNotContain('_', fact.Value);
        if (mode is "scene" or "source_bound" or "condition_bound")
            Assert.Contains("при потере", fact.Value, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Build_VisibleSourceLinkUsesReadableContextWithoutPermanentSelector()
    {
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect(profile: "wound_consequence");

        var entry = Assert.Single(BuildProjection(effect).Entries);

        Assert.Contains(
            entry.Facts,
            fact => fact.Kind == "link" && fact.Value == "Рваная рана");
        var serialized = JsonSerializer.Serialize(entry);
        Assert.DoesNotContain("wound_test_torn_side", serialized, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_FiniteNumberOutsideDecimalRangeRemainsReadable()
    {
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect();
        effect["components"]![0]!["payload"]!["amount"] = 1e100;

        var projection = BuildProjection(effect);

        Assert.True(projection.IsAvailable);
        var entry = Assert.Single(projection.Entries);
        Assert.Contains(
            entry.Facts,
            fact => fact.Kind == "periodic_damage" &&
                    fact.Value.Contains("E+100", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("characteristic_modifier")]
    [InlineData("roll_modifier")]
    [InlineData("resistance_modifier")]
    [InlineData("periodic_damage")]
    [InlineData("periodic_restore")]
    [InlineData("action_control")]
    [InlineData("event_reaction")]
    [InlineData("wound_consequence")]
    [InlineData("afterlife_combat_condition")]
    public void Build_EveryRegisteredProfileHasAPlayerFact(string profile)
    {
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect(profile: profile);

        var projection = BuildProjection(effect);

        Assert.True(projection.IsAvailable);
        var entry = Assert.Single(projection.Entries);
        Assert.Contains(entry.Facts, fact => fact.Kind == profile && !string.IsNullOrWhiteSpace(fact.Value));
    }

    [Theory]
    [InlineData("characteristic_modifier", "Ловкость", "dexterity")]
    [InlineData("roll_modifier", "бросок атаки", "attack_roll")]
    [InlineData("resistance_modifier", "кровотечение", "bleeding")]
    [InlineData("periodic_damage", "здоровье", "health")]
    [InlineData("periodic_restore", "здоровье", "health")]
    [InlineData("action_control", "перемещение", "movement")]
    [InlineData("event_reaction", "владелец получает урон", "owner_damaged")]
    [InlineData("wound_consequence", "кровотечение", "bleeding")]
    [InlineData("afterlife_combat_condition", "бремя", "burden")]
    public void Build_RegisteredProfileFactsUseReadableRussianClosedTokenLabels(
        string profile,
        string expected,
        string forbiddenRawToken)
    {
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect(profile: profile);

        var entry = Assert.Single(BuildProjection(effect).Entries);
        var fact = Assert.Single(entry.Facts, candidate => candidate.Kind == profile);

        Assert.Contains(expected, fact.Value, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(forbiddenRawToken, fact.Value, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_ProjectsBoundsPoliciesTriggerIntervalAndCompleteAfterlifeSemantics()
    {
        var resistance = Assert.Single(BuildProjection(
            EffectMaterializationTestFixture.CreateCanonicalEffect(profile: "resistance_modifier")).Entries);
        Assert.Contains(
            resistance.Facts,
            fact => fact.Kind == "resistance_modifier" &&
                    fact.Value.Contains("предел: от -100 до 100", StringComparison.Ordinal));
        Assert.Contains(
            resistance.Facts,
            fact => fact.Kind == "trigger" &&
                    fact.Value.Contains("завершение хода владельца", StringComparison.Ordinal));

        var damage = Assert.Single(BuildProjection(
            EffectMaterializationTestFixture.CreateCanonicalEffect(profile: "periodic_damage")).Entries);
        Assert.Contains(
            damage.Facts,
            fact => fact.Kind == "periodic_damage" &&
                    fact.Value.Contains("нижний предел ресурса", StringComparison.Ordinal));

        var restore = Assert.Single(BuildProjection(
            EffectMaterializationTestFixture.CreateCanonicalEffect(profile: "periodic_restore")).Entries);
        Assert.Contains(
            restore.Facts,
            fact => fact.Kind == "periodic_restore" &&
                    fact.Value.Contains("верхний предел ресурса", StringComparison.Ordinal));

        var reaction = Assert.Single(BuildProjection(
            EffectMaterializationTestFixture.CreateCanonicalEffect(profile: "event_reaction")).Entries);
        Assert.Contains(
            reaction.Facts,
            fact => fact.Kind == "event_reaction" &&
                    fact.Value.Contains("после текущего события", StringComparison.Ordinal));

        var afterlife = Assert.Single(BuildProjection(
            EffectMaterializationTestFixture.CreateCanonicalEffect(profile: "afterlife_combat_condition")).Entries);
        var condition = Assert.Single(
            afterlife.Facts,
            fact => fact.Kind == "afterlife_combat_condition");
        foreach (var expected in new[]
                 {
                     "противостоящая сторона",
                     "давление",
                     "режим броска",
                     "очищение",
                     "истощение"
                 })
        {
            Assert.Contains(expected, condition.Value, StringComparison.OrdinalIgnoreCase);
        }

        Assert.All(
            resistance.Facts
                .Concat(damage.Facts)
                .Concat(restore.Facts)
                .Concat(reaction.Facts)
                .Concat(afterlife.Facts),
            fact => Assert.DoesNotContain('_', fact.Value));
    }

    [Theory]
    [InlineData("hidden")]
    [InlineData("gm_only")]
    public void Build_HiddenAndGmOnlyEffectsAreAbsentFromRowsCountsAndActions(string visibility)
    {
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect();
        effect["display"]!["visibility"] = visibility;

        var projection = BuildProjection(effect);

        Assert.True(projection.IsAvailable);
        Assert.Empty(projection.Entries);
        Assert.Equal(0, projection.VisibleCount);
        Assert.Empty(projection.Actions);
    }

    [Fact]
    public void Build_MalformedAcceptedSetFailsClosedWithoutPartialFacts()
    {
        var valid = EffectMaterializationTestFixture.CreateCanonicalEffect();
        var malformed = EffectMaterializationTestFixture.CreateCanonicalEffect();
        malformed["effectId"] = "effect_malformed_sibling";
        malformed["stacking"]!["stackKey"] = "malformed_sibling";
        malformed["source"]!["sourceId"] = "wound_malformed_sibling";
        malformed["chronology"]!["lastTransitionId"] = "effect_transition_malformed_sibling";
        malformed["chronology"]!["createdEventRef"] = "turn_42:malformed_sibling";
        malformed["components"]![0]!.AsObject().Remove("payload");
        var snapshot = EffectMechanicsSnapshot.Build(CreateInput(
            new JsonArray(valid.DeepClone(), malformed.DeepClone()),
            EffectMaterializationTestFixture.CreateIdentityIndex(valid, malformed)));

        var projection = EffectPlayerProjection.Build(new EffectPlayerProjectionInput(snapshot));

        Assert.False(projection.IsAvailable);
        Assert.Empty(projection.Entries);
        Assert.Empty(projection.Actions);
        Assert.Equal("Сейчас невозможно надёжно определить действующие эффекты.", projection.StatusMessage);
        Assert.DoesNotContain(projection.StatusMessage, "validation", StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(projection.StatusMessage, "materialization", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Build_OpaqueSelectorsAreDeterministicForAcceptedStateAndDistinctFromPermanentIds()
    {
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect();

        var first = Assert.Single(BuildProjection(effect).Entries);
        var second = Assert.Single(BuildProjection(effect).Entries);

        Assert.Equal(first.Selector, second.Selector);
        Assert.NotEqual(EffectMaterializationTestFixture.EffectId, first.Selector);
        Assert.DoesNotContain("bleeding", first.Selector, StringComparison.OrdinalIgnoreCase);
        Assert.All(first.Actions, action =>
        {
            Assert.StartsWith("effect_action_", action.Selector, StringComparison.Ordinal);
            Assert.DoesNotContain(EffectMaterializationTestFixture.EffectId, action.Selector, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void SanitizeSemanticValue_RemovesAnnotatedEffectTechnicalDtosAndPreservesOrdinaryWorldObjects()
    {
        var root = new JsonObject
        {
            ["identity"] = new JsonObject
            {
                ["effectId"] = "effect_private",
                ["state"] = "active",
                ["realm"] = "mortal_world",
                ["owner"] = new JsonObject
                {
                    ["kind"] = "player",
                    ["ownerId"] = "player_current",
                    ["carrierPath"] = "game_state/player/effects.json",
                    ["collection"] = "activeEffects"
                },
                ["target"] = new JsonObject { ["kind"] = "player", ["targetId"] = "player_current" },
                ["source"] = new JsonObject { ["kind"] = "wound", ["sourceId"] = "wound_private", ["definitionKey"] = "private" },
                ["stackCoordinate"] = new JsonObject
                {
                    ["realm"] = "mortal_world",
                    ["targetKind"] = "player",
                    ["targetId"] = "player_current",
                    ["sourceKind"] = "wound",
                    ["sourceId"] = "wound_private",
                    ["stackKey"] = "private"
                },
                ["createdAtTurn"] = 42,
                ["transitions"] = new JsonArray(),
                ["annotation"] = "PRIVATE ANNOTATION"
            },
            ["pending"] = new JsonObject
            {
                ["requestId"] = "effect_request_private",
                ["effectId"] = "effect_private",
                ["operation"] = "dispel",
                ["source"] = new JsonObject(),
                ["target"] = new JsonObject(),
                ["requiredFields"] = new JsonArray(),
                ["fullTurnResubmissionRequired"] = true,
                ["annotation"] = "PRIVATE PENDING"
            },
            ["repair"] = new JsonObject
            {
                ["kind"] = "effect_materialization_repair",
                ["targetFiles"] = new JsonArray("game_state/player/effects.json"),
                ["templateRefs"] = new JsonArray("effect_private"),
                ["missingFields"] = new JsonArray("source"),
                ["annotation"] = "PRIVATE REPAIR"
            },
            ["worldRoute"] = new JsonObject
            {
                ["kind"] = "pilgrimage",
                ["title"] = "Дорога к башне",
                ["steps"] = new JsonArray("Перейти мост"),
                ["turn"] = 42,
                ["route"] = "north_road",
                ["source"] = "слух трактирщика"
            }
        };

        var projected = Assert.IsType<JsonObject>(EffectPlayerProjection.SanitizeSemanticValue(root));

        Assert.False(projected.ContainsKey("identity"));
        Assert.False(projected.ContainsKey("pending"));
        Assert.False(projected.ContainsKey("repair"));
        var route = Assert.IsType<JsonObject>(projected["worldRoute"]);
        Assert.Equal("pilgrimage", route["kind"]!.GetValue<string>());
        Assert.Equal("Дорога к башне", route["title"]!.GetValue<string>());
        Assert.Equal("north_road", route["route"]!.GetValue<string>());
        Assert.Equal("слух трактирщика", route["source"]!.GetValue<string>());
    }

    private static EffectPlayerProjectionResult BuildProjection(JsonObject effect)
    {
        var snapshot = EffectMechanicsSnapshot.Build(CreateInput(
            new JsonArray(effect.DeepClone()),
            EffectMaterializationTestFixture.CreateIdentityIndex(effect)));
        return EffectPlayerProjection.Build(new EffectPlayerProjectionInput(snapshot));
    }

    private static EffectMechanicsInput CreateInput(
        JsonArray activeEffects,
        JsonObject identityIndex) =>
        new(
            new EffectCarrierCatalogInput(
                new JsonObject
                {
                    ["schemaVersion"] = 1,
                    ["activeEffects"] = activeEffects
                },
                null,
                null,
                null,
                null,
                null),
            identityIndex);

    private static JsonObject CreateLifetime(string mode) => mode switch
    {
        "turns" => new JsonObject
        {
            ["mode"] = mode,
            ["remainingTurns"] = 2,
            ["advancePhase"] = "world_turn_start",
            ["displayText"] = "Ещё два мировых хода"
        },
        "uses" => new JsonObject
        {
            ["mode"] = mode,
            ["remainingUses"] = 2,
            ["consumingTriggerIds"] = new JsonArray("on_owner_turn_end"),
            ["displayText"] = "Ещё два срабатывания"
        },
        "until_time" => new JsonObject
        {
            ["mode"] = mode,
            ["deadline"] = 123L,
            ["displayText"] = "До рассвета"
        },
        "scene" => new JsonObject
        {
            ["mode"] = mode,
            ["sceneId"] = "scene_test",
            ["onSceneExit"] = "expire",
            ["displayText"] = "Пока длится эта сцена"
        },
        "source_bound" => new JsonObject
        {
            ["mode"] = mode,
            ["linkKind"] = "wound",
            ["targetId"] = "wound_test_torn_side",
            ["activePredicate"] = "active",
            ["onSourceLoss"] = "suspend",
            ["displayText"] = "Пока сохраняется причина"
        },
        "condition_bound" => new JsonObject
        {
            ["mode"] = mode,
            ["conditionKey"] = "source_is_active",
            ["operands"] = new JsonObject { ["expected"] = true },
            ["onConditionLoss"] = "expire",
            ["displayText"] = "Пока выполняется условие"
        },
        "permanent" => new JsonObject
        {
            ["mode"] = mode,
            ["displayText"] = "Без естественного окончания"
        },
        "manual" => new JsonObject
        {
            ["mode"] = mode,
            ["authorities"] = new JsonArray("physical_treatment"),
            ["displayText"] = "Пока не будет снят"
        },
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported test lifetime.")
    };
}
