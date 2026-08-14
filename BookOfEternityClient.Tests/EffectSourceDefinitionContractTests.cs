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
