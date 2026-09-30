using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class EffectMaterializationContractTests
{
    private static readonly string[] RequiredRootFields =
    {
        "schemaVersion",
        "entityKind",
        "effectId",
        "state",
        "realm",
        "target",
        "display",
        "source",
        "components",
        "lifetime",
        "stacking",
        "triggers",
        "removal",
        "links",
        "chronology"
    };

    [Fact]
    public void Validate_CompleteCanonicalEffect_ReturnsNoIssues()
    {
        using var document = Parse(EffectMaterializationTestFixture.CreateCanonicalEffect());

        var issues = EffectMaterializationContract.Validate(
            document.RootElement,
            "game_state.player.activeEffects[0]",
            EffectMaterializationPhase.CanonicalActive);

        Assert.Empty(issues);
    }

    [Theory]
    [InlineData(100L, true)]
    [InlineData(-1L, false)]
    [InlineData("100", false)]
    public void Validate_UntilTimeRequiresNonNegativeCanonicalWorldMinutes(
        object deadline,
        bool expectedValid)
    {
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect();
        effect["lifetime"] = new JsonObject
        {
            ["mode"] = "until_time",
            ["deadline"] = JsonValue.Create(deadline)
        };
        using var document = Parse(effect);

        var issues = EffectMaterializationContract.Validate(
            document.RootElement,
            "effects[0]",
            EffectMaterializationPhase.CanonicalActive);

        Assert.Equal(expectedValid, issues.Count == 0);
        if (!expectedValid)
        {
            Assert.Contains(issues, issue =>
                issue.Code == "effect_materialization_invalid_field" &&
                issue.FilePath == "effects[0].lifetime.deadline");
        }
    }

    [Theory]
    [MemberData(nameof(RequiredRootFieldCases))]
    public void Validate_MissingRequiredRootField_IsRejected(string field)
    {
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect();
        effect.Remove(field);
        using var document = Parse(effect);

        var issues = EffectMaterializationContract.Validate(
            document.RootElement,
            "effects[0]",
            EffectMaterializationPhase.CanonicalActive);

        Assert.Contains(issues, issue =>
            issue.Code == "effect_materialization_missing_field" &&
            issue.FilePath == $"effects[0].{field}");
    }

    [Fact]
    public void Validate_UnknownRootField_IsRejected()
    {
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect();
        effect["futureMechanics"] = new JsonObject { ["value"] = 10 };
        using var document = Parse(effect);

        var issues = EffectMaterializationContract.Validate(
            document.RootElement,
            "effects[0]",
            EffectMaterializationPhase.CanonicalActive);

        Assert.Contains(issues, issue =>
            issue.Code == "effect_materialization_unknown_field" &&
            issue.FilePath == "effects[0].futureMechanics");
    }

    [Fact]
    public void Validate_DuplicateRawProperty_IsRejectedBeforeNodeConversion()
    {
        var json = EffectMaterializationTestFixture.CreateCanonicalEffect().ToJsonString();
        json = json.Replace(
            "\"state\":\"active\"",
            "\"state\":\"active\",\"state\":\"active\"",
            StringComparison.Ordinal);
        using var document = JsonDocument.Parse(json);

        var issues = EffectMaterializationContract.Validate(
            document.RootElement,
            "effects[0]",
            EffectMaterializationPhase.CanonicalActive);

        Assert.Contains(issues, issue =>
            issue.Code == "effect_materialization_duplicate_property" &&
            issue.FilePath == "effects[0].state");
    }

    [Theory]
    [InlineData("entityKind", "temporary_effect", "effect_materialization_invalid_field")]
    [InlineData("state", "expired", "effect_materialization_invalid_field")]
    [InlineData("realm", "Mortal", "effect_materialization_invalid_field")]
    public void Validate_ClosedRootScalar_IsOrdinalAndCurrent(
        string field,
        string value,
        string code)
    {
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect();
        effect[field] = value;
        using var document = Parse(effect);

        Assert.Contains(
            EffectMaterializationContract.Validate(
                document.RootElement,
                "effects[0]",
                EffectMaterializationPhase.CanonicalActive),
            issue => issue.Code == code && issue.FilePath == $"effects[0].{field}");
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
    public void Validate_EachRegisteredComponentProfile_AcceptsCompletePayload(string profile)
    {
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect(profile: profile);
        using var document = Parse(effect);

        Assert.Empty(EffectMaterializationContract.Validate(
            document.RootElement,
            "effects[0]",
            EffectMaterializationPhase.CanonicalActive));
    }

    [Theory]
    [InlineData("characteristic_modifier", "characteristic", "unknown")]
    [InlineData("roll_modifier", "contribution", "bonus")]
    [InlineData("resistance_modifier", "value", "not-a-number")]
    [InlineData("periodic_damage", "amount", 0)]
    [InlineData("periodic_restore", "amount", -1)]
    [InlineData("action_control", "operation", "rewrite")]
    [InlineData("event_reaction", "resultKind", "arbitrary_json")]
    [InlineData("wound_consequence", "woundId", "  wound_test  ")]
    [InlineData("afterlife_combat_condition", "conditionKind", "mortal_buff")]
    public void Validate_ProfilePayloadViolation_RejectsWholeComponent(
        string profile,
        string field,
        object replacement)
    {
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect(profile: profile);
        effect["components"]![0]!["payload"]![field] = JsonValue.Create(replacement);
        using var document = Parse(effect);

        var issues = EffectMaterializationContract.Validate(
            document.RootElement,
            "effects[0]",
            EffectMaterializationPhase.CanonicalActive);

        Assert.Contains(issues, issue =>
            issue.Code == "effect_materialization_invalid_component" &&
            issue.FilePath.Contains($"payload.{field}", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_NonFiniteNumericPayload_IsRejected()
    {
        var json = EffectMaterializationTestFixture.CreateCanonicalEffect().ToJsonString();
        json = json.Replace("\"amount\":3", "\"amount\":1e9999", StringComparison.Ordinal);
        using var document = JsonDocument.Parse(json);

        Assert.Contains(
            EffectMaterializationContract.Validate(
                document.RootElement,
                "effects[0]",
                EffectMaterializationPhase.CanonicalActive),
            issue => issue.Code == "effect_materialization_invalid_component");
    }

    [Fact]
    public void Validate_UnknownProfileAndPayloadField_AreRejected()
    {
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect();
        var component = effect["components"]![0]!.AsObject();
        component["profile"] = "free_form_effect";
        component["payload"]!["descriptionMechanics"] = "-10 ко всему";
        using var document = Parse(effect);

        var issues = EffectMaterializationContract.Validate(
            document.RootElement,
            "effects[0]",
            EffectMaterializationPhase.CanonicalActive);

        Assert.Contains(issues, issue => issue.Code == "effect_materialization_unknown_profile");
        Assert.DoesNotContain(issues, issue => issue.Severity != IssueSeverity.Error);
    }

    [Fact]
    public void Validate_RegisteredProfileUnknownPayloadField_IsRejected()
    {
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect();
        effect["components"]![0]!["payload"]!["descriptionMechanics"] = "-10 ко всему";
        using var document = Parse(effect);

        Assert.Contains(
            EffectMaterializationContract.Validate(
                document.RootElement,
                "effects[0]",
                EffectMaterializationPhase.CanonicalActive),
            issue => issue.Code == "effect_materialization_invalid_component" &&
                     issue.FilePath == "effects[0].components[0].payload.descriptionMechanics");
    }

    [Theory]
    [MemberData(nameof(RollModifierScopeCases))]
    public void Validate_RollModifier_RequiresClosedStructuralScopeUnion(
        JsonObject? scope,
        JsonArray operations,
        string? expectedPath)
    {
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect(profile: "roll_modifier");
        var payload = effect["components"]![0]!["payload"]!.AsObject();
        if (scope == null)
            payload.Remove("scope");
        else
            payload["scope"] = scope;
        payload["operations"] = operations;
        using var document = Parse(effect);

        var issues = EffectMaterializationContract.Validate(
            document.RootElement,
            "effects[0]",
            EffectMaterializationPhase.CanonicalActive);

        if (expectedPath == null)
        {
            Assert.Empty(issues);
            return;
        }

        Assert.Contains(issues, issue =>
            issue.Code == "effect_materialization_invalid_component" &&
            issue.FilePath == $"effects[0].components[0].{expectedPath}");
    }

    [Fact]
    public void Validate_RollModifier_RejectsUnknownPayloadAndScopeFields()
    {
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect(profile: "roll_modifier");
        var payload = effect["components"]![0]!["payload"]!.AsObject();
        payload["unknownPayloadField"] = true;
        payload["scope"] = new JsonObject { ["kind"] = "all" };
        payload["scope"]!["unknownScopeField"] = true;
        using var document = Parse(effect);

        var issues = EffectMaterializationContract.Validate(
            document.RootElement,
            "effects[0]",
            EffectMaterializationPhase.CanonicalActive);

        Assert.Contains(issues, issue =>
            issue.Code == "effect_materialization_invalid_component" &&
            issue.FilePath == "effects[0].components[0].payload.unknownPayloadField");
        Assert.Contains(issues, issue =>
            issue.Code == "effect_materialization_invalid_component" &&
            issue.FilePath == "effects[0].components[0].payload.scope.unknownScopeField");
    }

    [Theory]
    [MemberData(nameof(RollModifierInvalidSkillOperationCases))]
    public void Validate_RollModifier_InvalidSkillOperations_ReportOnlyEveryStructuralOperationIssue(
        JsonNode? operations,
        bool removeOperations,
        string[] expectedOperationPaths)
    {
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect(profile: "roll_modifier");
        var payload = effect["components"]![0]!["payload"]!.AsObject();
        payload["scope"] = new JsonObject
        {
            ["kind"] = "skill",
            ["skillId"] = "skill_lockpicking"
        };
        if (removeOperations)
            payload.Remove("operations");
        else
            payload["operations"] = operations;
        using var document = Parse(effect);

        var issues = EffectMaterializationContract.Validate(
            document.RootElement,
            "effects[0]",
            EffectMaterializationPhase.CanonicalActive);
        var operationPath = "effects[0].components[0].payload.operations";

        Assert.Equal(
            expectedOperationPaths.Length,
            issues.Count(issue =>
                issue.Code == "effect_materialization_invalid_component" &&
                issue.FilePath.StartsWith(operationPath, StringComparison.Ordinal)));
        foreach (var expectedPath in expectedOperationPaths)
        {
            Assert.Contains(issues, issue =>
                issue.Code == "effect_materialization_invalid_component" &&
                issue.FilePath == $"effects[0].components[0].{expectedPath}");
        }
        Assert.DoesNotContain(issues, issue =>
            issue.Code == "effect_materialization_invalid_component" &&
            issue.Expected == "exactly [\"skill_check\"] for scope.kind=skill");
    }

    [Fact]
    public void Validate_DisplayProseCannotReplaceRequiredMechanicalAmount()
    {
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect();
        effect["components"]![0]!["payload"]!.AsObject().Remove("amount");
        effect["display"]!["description"] = "Наносит три единицы урона каждый ход.";
        using var document = Parse(effect);

        Assert.Contains(
            EffectMaterializationContract.Validate(
                document.RootElement,
                "effects[0]",
                EffectMaterializationPhase.CanonicalActive),
            issue => issue.Code == "effect_materialization_invalid_component" &&
                     issue.FilePath == "effects[0].components[0].payload.amount");
    }

    [Theory]
    [InlineData("target")]
    [InlineData("display")]
    [InlineData("source")]
    [InlineData("components")]
    [InlineData("lifetime")]
    [InlineData("stacking")]
    [InlineData("triggers")]
    [InlineData("removal")]
    [InlineData("links")]
    [InlineData("chronology")]
    public void Validate_WrongRequiredSectionType_IsRejected(string field)
    {
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect();
        effect[field] = "mechanics in prose";
        using var document = Parse(effect);

        Assert.Contains(
            EffectMaterializationContract.Validate(
                document.RootElement,
                "effects[0]",
                EffectMaterializationPhase.CanonicalActive),
            issue => issue.Code == "effect_materialization_invalid_field" &&
                     issue.FilePath == $"effects[0].{field}");
    }

    [Theory]
    [InlineData("characteristic_modifier", "Deterministic")]
    [InlineData("roll_modifier", "Deterministic")]
    [InlineData("resistance_modifier", "Deterministic")]
    [InlineData("periodic_damage", "Deterministic")]
    [InlineData("periodic_restore", "Deterministic")]
    [InlineData("action_control", "Deterministic")]
    [InlineData("event_reaction", "Declared")]
    [InlineData("wound_consequence", "Deterministic")]
    [InlineData("afterlife_combat_condition", "Deterministic")]
    public void Registry_EachProfileDeclaresExecutionMergeAndProjectionMetadata(
        string profile,
        string expectedMode)
    {
        Assert.True(EffectComponentProfiles.TryGetDescriptor(profile, out var descriptor));
        Assert.Equal(expectedMode, descriptor.ResolutionMode.ToString());
        Assert.NotEmpty(descriptor.LegalMergeReducers);
        Assert.False(string.IsNullOrWhiteSpace(descriptor.ProjectionDescriptor));
    }

    [Fact]
    public void Registry_EveryProfilePublishesAnImmutableMergeReducerSet()
    {
        foreach (var profile in EffectComponentProfiles.RegisteredProfiles)
        {
            Assert.True(EffectComponentProfiles.TryGetDescriptor(profile, out var descriptor));
            Assert.IsAssignableFrom<IImmutableSet<string>>(descriptor.LegalMergeReducers);
        }
    }

    [Fact]
    public void ValidateCarrier_MissingPristineCarrier_IsAcceptedAsEmpty()
    {
        Assert.Empty(EffectMaterializationContract.ValidateCarrier(
            carrier: null,
            "game_state/player/effects.json",
            EffectCarrierKind.Player));
    }

    [Fact]
    public void ValidateCarrier_NonEmptyLegacyCarrier_IsRejected()
    {
        using var document = JsonDocument.Parse(
            "{\"playerActiveEffectsChanges\":[{\"name\":\"legacy\",\"duration\":999}]}");

        var issues = EffectMaterializationContract.ValidateCarrier(
            document.RootElement,
            "game_state/player/effects.json",
            EffectCarrierKind.Player);

        Assert.Contains(issues, issue => issue.Code == "effect_materialization_legacy_carrier_unsupported");
    }

    public static IEnumerable<object[]> RequiredRootFieldCases() =>
        RequiredRootFields.Select(field => new object[] { field });

    public static IEnumerable<object[]> RollModifierScopeCases()
    {
        yield return ValidScope(new JsonObject { ["kind"] = "all" });
        yield return ValidScope(new JsonObject
        {
            ["kind"] = "skill",
            ["skillId"] = "skill_lockpicking"
        });
        yield return InvalidScope(null, "payload.scope");
        yield return InvalidScope(new JsonObject(), "payload.scope.kind");
        yield return InvalidScope(new JsonObject
        {
            ["kind"] = "all",
            ["skillId"] = "skill_lockpicking"
        }, "payload.scope.skillId");
        yield return InvalidScope(new JsonObject
        {
            ["kind"] = "skill"
        }, "payload.scope.skillId");
        yield return InvalidScope(
            new JsonObject
            {
                ["kind"] = "skill",
                ["skillId"] = "skill_lockpicking"
            },
            "payload.operations",
            new JsonArray("attack_roll"));
        yield return InvalidScope(
            new JsonObject
            {
                ["kind"] = "skill",
                ["skillId"] = "skill_lockpicking"
            },
            "payload.operations",
            new JsonArray("skill_check", "attack_roll"));
    }

    private static object[] ValidScope(JsonObject scope) =>
        new object[]
        {
            scope,
            scope["kind"]!.GetValue<string>() == "skill"
                ? new JsonArray("skill_check")
                : new JsonArray("attack_roll"),
            null!
        };

    private static object[] InvalidScope(
        JsonObject? scope,
        string expectedPath,
        JsonArray? operations = null) =>
        new object[]
        {
            scope!,
            operations ?? (scope?["kind"]?.GetValue<string>() == "skill"
                ? new JsonArray("skill_check")
                : new JsonArray("attack_roll")),
            expectedPath
        };

    public static IEnumerable<object[]> RollModifierInvalidSkillOperationCases()
    {
        yield return InvalidSkillOperations(null, true, "payload.operations");
        yield return InvalidSkillOperations(null, false, "payload.operations");
        yield return InvalidSkillOperations(JsonValue.Create("not-an-array"), false, "payload.operations");
        yield return InvalidSkillOperations(new JsonArray(), false, "payload.operations");
        yield return InvalidSkillOperations(new JsonArray("skill_check", "skill_check"), false, "payload.operations[1]");
        yield return InvalidSkillOperations(new JsonArray("not_registered"), false, "payload.operations[0]");
        yield return InvalidSkillOperations(
            new JsonArray("not_registered", "also_not_registered"),
            false,
            "payload.operations[0]",
            "payload.operations[1]");
    }

    private static object[] InvalidSkillOperations(
        JsonNode? operations,
        bool removeOperations,
        params string[] expectedOperationPaths) =>
        new object[] { operations!, removeOperations, expectedOperationPaths };

    private static JsonDocument Parse(JsonNode node) =>
        JsonDocument.Parse(node.ToJsonString());
}
