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

    private static JsonDocument Parse(JsonNode node) =>
        JsonDocument.Parse(node.ToJsonString());
}
