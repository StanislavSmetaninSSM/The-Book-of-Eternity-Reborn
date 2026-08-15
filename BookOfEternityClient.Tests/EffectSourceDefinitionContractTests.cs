using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class EffectSourceDefinitionContractTests
{
    [Fact]
    public void ValidateArray_CompleteDefinition_ReturnsNoIssues()
    {
        using var document = Parse(new JsonArray(
            EffectMaterializationTestFixture.CreateDefinition()));

        Assert.Empty(EffectSourceDefinitionContract.ValidateArray(
            document.RootElement,
            "source.activeEffectDefinitions",
            "mortal_world"));
    }

    [Fact]
    public void ValidateArray_IndependentPolicyAllowsBoundedSimultaneousInstances()
    {
        var definition = EffectMaterializationTestFixture.CreateDefinition();
        definition["stacking"] = new JsonObject
        {
            ["stackKey"] = "bleeding",
            ["policy"] = "independent",
            ["maxStacks"] = 2,
            ["atMaximum"] = "no_change",
            ["refreshMode"] = null,
            ["mergeRule"] = null
        };
        using var document = Parse(new JsonArray(definition));

        Assert.Empty(EffectSourceDefinitionContract.ValidateArray(
            document.RootElement,
            "source.activeEffectDefinitions",
            "mortal_world"));
    }

    [Theory]
    [InlineData("world_time.currentTimeInMinutes", true)]
    [InlineData("gm_clock", false)]
    public void ValidateArray_UntilTimeUsesRegisteredCanonicalAuthority(
        string timeAuthority,
        bool expectedValid)
    {
        var definition = EffectMaterializationTestFixture.CreateDefinition();
        definition["lifetime"] = new JsonObject
        {
            ["mode"] = "until_time",
            ["duration"] = 30,
            ["timeAuthority"] = timeAuthority
        };
        using var document = Parse(new JsonArray(definition));

        var issues = EffectSourceDefinitionContract.ValidateArray(
            document.RootElement,
            "source.activeEffectDefinitions",
            "mortal_world");

        Assert.Equal(expectedValid, issues.Count == 0);
        if (!expectedValid)
        {
            Assert.Contains(issues, issue =>
                issue.Code == "effect_source_definition_invalid_field" &&
                issue.FilePath.EndsWith(".lifetime.timeAuthority", StringComparison.Ordinal));
        }
    }

    [Theory]
    [InlineData("periodic_damage", "sum", true)]
    [InlineData("roll_modifier", "sum", false)]
    [InlineData("event_reaction", "profile_specific", true)]
    public void ValidateArray_MergeReducerMustBeRegisteredForEveryComponentProfile(
        string profile,
        string mergeRule,
        bool expectedValid)
    {
        var definition = EffectMaterializationTestFixture.CreateDefinition(profile);
        definition["stacking"] = new JsonObject
        {
            ["stackKey"] = "effect_merge",
            ["policy"] = "merge",
            ["maxStacks"] = 3,
            ["atMaximum"] = "no_change",
            ["refreshMode"] = null,
            ["mergeRule"] = mergeRule
        };
        using var document = Parse(new JsonArray(definition));

        var issues = EffectSourceDefinitionContract.ValidateArray(
            document.RootElement,
            "source.activeEffectDefinitions",
            "mortal_world");

        Assert.Equal(expectedValid, issues.Count == 0);
        if (!expectedValid)
        {
            Assert.Contains(issues, issue =>
                issue.Code == "effect_source_definition_invalid_merge_rule");
        }
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
    public void ValidateArray_EachRegisteredProfileProducesCompleteDefinition(string profile)
    {
        using var document = Parse(new JsonArray(
            EffectMaterializationTestFixture.CreateDefinition(profile)));

        Assert.Empty(EffectSourceDefinitionContract.ValidateArray(
            document.RootElement,
            "source.activeEffectDefinitions",
            "mortal_world"));
    }

    [Theory]
    [InlineData("effectId")]
    [InlineData("currentStacks")]
    [InlineData("chronology")]
    [InlineData("materializationReceipt")]
    [InlineData("transitionId")]
    public void ValidateArray_ClientOwnedRuntimeField_IsForbidden(string field)
    {
        var definition = EffectMaterializationTestFixture.CreateDefinition();
        definition[field] = "forged";
        using var document = Parse(new JsonArray(definition));

        Assert.Contains(
            EffectSourceDefinitionContract.ValidateArray(
                document.RootElement,
                "source.activeEffectDefinitions",
                "mortal_world"),
            issue => issue.Code == "effect_source_definition_client_field_forbidden" &&
                     issue.FilePath.EndsWith('.' + field, StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateArray_UnknownDefinitionField_IsRejected()
    {
        var definition = EffectMaterializationTestFixture.CreateDefinition();
        definition["futurePolicy"] = new JsonObject();
        using var document = Parse(new JsonArray(definition));

        Assert.Contains(
            EffectSourceDefinitionContract.ValidateArray(
                document.RootElement,
                "source.activeEffectDefinitions",
                "mortal_world"),
            issue => issue.Code == "effect_source_definition_unknown_field");
    }

    [Theory]
    [InlineData("bleeding_consequence", "bleeding_consequence", "effect_source_definition_duplicate_key")]
    [InlineData("bleeding_consequence", "BLEEDING_CONSEQUENCE", "effect_source_definition_confusable_key")]
    [InlineData("bleeding_consequence", "bleedіng_consequence", "effect_source_definition_confusable_key")]
    public void ValidateArray_DuplicateOrConfusableDefinitionKey_IsRejected(
        string firstKey,
        string secondKey,
        string expectedCode)
    {
        var first = EffectMaterializationTestFixture.CreateDefinition();
        first["definitionKey"] = firstKey;
        var second = EffectMaterializationTestFixture.CreateDefinition();
        second["definitionKey"] = secondKey;
        using var document = Parse(new JsonArray(first, second));

        Assert.Contains(
            EffectSourceDefinitionContract.ValidateArray(
                document.RootElement,
                "source.activeEffectDefinitions",
                "mortal_world"),
            issue => issue.Code == expectedCode);
    }

    [Theory]
    [InlineData(" definition_key ")]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateArray_DefinitionKeyMustBeExactTrimmedNonEmpty(string key)
    {
        var definition = EffectMaterializationTestFixture.CreateDefinition();
        definition["definitionKey"] = key;
        using var document = Parse(new JsonArray(definition));

        Assert.Contains(
            EffectSourceDefinitionContract.ValidateArray(
                document.RootElement,
                "source.activeEffectDefinitions",
                "mortal_world"),
            issue => issue.Code == "effect_source_definition_invalid_field");
    }

    [Fact]
    public void ValidateArray_WrongRealmAndTargetKindAreRejected()
    {
        var definition = EffectMaterializationTestFixture.CreateDefinition();
        definition["allowedRealms"] = new JsonArray("Mortal");
        definition["allowedTargetKinds"] = new JsonArray("PlayerByName");
        using var document = Parse(new JsonArray(definition));

        var issues = EffectSourceDefinitionContract.ValidateArray(
            document.RootElement,
            "source.activeEffectDefinitions",
            "mortal_world");

        Assert.Contains(issues, issue => issue.FilePath.Contains("allowedRealms", StringComparison.Ordinal));
        Assert.Contains(issues, issue => issue.FilePath.Contains("allowedTargetKinds", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("components")]
    [InlineData("stacking")]
    [InlineData("lifetime")]
    [InlineData("triggers")]
    [InlineData("removal")]
    [InlineData("links")]
    public void ValidateArray_MissingCompletePolicySection_IsRejected(string field)
    {
        var definition = EffectMaterializationTestFixture.CreateDefinition();
        definition.Remove(field);
        using var document = Parse(new JsonArray(definition));

        Assert.Contains(
            EffectSourceDefinitionContract.ValidateArray(
                document.RootElement,
                "source.activeEffectDefinitions",
                "mortal_world"),
            issue => issue.Code == "effect_source_definition_missing_field" &&
                     issue.FilePath.EndsWith('.' + field, StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateArray_EmptyComponentsAndOutOfBoundParameterAreRejected()
    {
        var definition = EffectMaterializationTestFixture.CreateDefinition();
        definition["components"] = new JsonArray();
        definition["parameterBounds"]!["amount"]!["minimum"] = 20;
        definition["parameterBounds"]!["amount"]!["maximum"] = 10;
        using var document = Parse(new JsonArray(definition));

        var issues = EffectSourceDefinitionContract.ValidateArray(
            document.RootElement,
            "source.activeEffectDefinitions",
            "mortal_world");

        Assert.Contains(issues, issue => issue.Code == "effect_source_definition_invalid_components");
        Assert.Contains(issues, issue => issue.Code == "effect_source_definition_invalid_parameter_bound");
    }

    [Fact]
    public void ValidateArray_SourceBoundLifetimeRejectsUnregisteredActivePredicate()
    {
        var definition = EffectMaterializationTestFixture.CreateDefinition();
        definition["lifetime"] = new JsonObject
        {
            ["mode"] = "source_bound",
            ["activePredicate"] = "gm_invented_predicate",
            ["onSourceLoss"] = "expire"
        };
        using var document = Parse(new JsonArray(definition));

        Assert.Contains(
            EffectSourceDefinitionContract.ValidateArray(
                document.RootElement,
                "source.activeEffectDefinitions",
                "mortal_world"),
            issue => issue.Code == "effect_source_definition_invalid_active_predicate");
    }

    [Fact]
    public void ValidateArray_TriggerComponentIdsMustResolveWithinDefinition()
    {
        var definition = EffectMaterializationTestFixture.CreateDefinition();
        definition["triggers"]![0]!["componentIds"] = new JsonArray(
            "component_missing",
            "component_missing");
        using var document = Parse(new JsonArray(definition));

        var issues = EffectSourceDefinitionContract.ValidateArray(
            document.RootElement,
            "source.activeEffectDefinitions",
            "mortal_world");

        Assert.Contains(issues, issue =>
            issue.Code == "effect_source_definition_invalid_trigger_component");
    }

    [Fact]
    public void ValidateArray_UsesLifetimeRequiresExactConsumingTriggerSet()
    {
        var definition = EffectMaterializationTestFixture.CreateDefinition();
        definition["lifetime"] = new JsonObject
        {
            ["mode"] = "uses",
            ["initialUses"] = 2,
            ["consumingEventTypes"] = new JsonArray("owner_turn_end")
        };
        definition["triggers"]![0]!["consumeUses"] = false;
        using var document = Parse(new JsonArray(definition));

        var issues = EffectSourceDefinitionContract.ValidateArray(
            document.RootElement,
            "source.activeEffectDefinitions",
            "mortal_world");

        Assert.Contains(issues, issue =>
            issue.Code == "effect_source_definition_consuming_trigger_mismatch");
    }

    [Fact]
    public void ValidateArray_UsesLifetimeRequiresTriggerForEveryConsumingEventType()
    {
        var definition = EffectMaterializationTestFixture.CreateDefinition();
        definition["lifetime"] = new JsonObject
        {
            ["mode"] = "uses",
            ["initialUses"] = 2,
            ["consumingEventTypes"] = new JsonArray("owner_damaged")
        };
        definition["triggers"]![0]!["consumeUses"] = false;
        using var document = Parse(new JsonArray(definition));

        var issues = EffectSourceDefinitionContract.ValidateArray(
            document.RootElement,
            "source.activeEffectDefinitions",
            "mortal_world");

        Assert.Contains(issues, issue =>
            issue.Code == "effect_source_definition_consuming_trigger_mismatch");
    }

    [Fact]
    public void ValidateArray_NonUsesLifetimeRejectsConsumingTrigger()
    {
        var definition = EffectMaterializationTestFixture.CreateDefinition();
        definition["triggers"]![0]!["consumeUses"] = true;
        using var document = Parse(new JsonArray(definition));

        var issues = EffectSourceDefinitionContract.ValidateArray(
            document.RootElement,
            "source.activeEffectDefinitions",
            "mortal_world");

        Assert.Contains(issues, issue =>
            issue.Code == "effect_source_definition_consuming_trigger_mismatch");
    }

    [Fact]
    public void ValidateArray_DuplicateRawProperty_IsRejectedBeforeNodeConversion()
    {
        var json = new JsonArray(EffectMaterializationTestFixture.CreateDefinition()).ToJsonString();
        json = json.Replace(
            "\"definitionKey\":\"bleeding_consequence\"",
            "\"definitionKey\":\"bleeding_consequence\",\"definitionKey\":\"bleeding_consequence\"",
            StringComparison.Ordinal);
        using var document = JsonDocument.Parse(json);

        Assert.Contains(
            EffectSourceDefinitionContract.ValidateArray(
                document.RootElement,
                "source.activeEffectDefinitions",
                "mortal_world"),
            issue => issue.Code == "effect_source_definition_duplicate_property");
    }

    private static JsonDocument Parse(JsonNode node) =>
        JsonDocument.Parse(node.ToJsonString());
}
