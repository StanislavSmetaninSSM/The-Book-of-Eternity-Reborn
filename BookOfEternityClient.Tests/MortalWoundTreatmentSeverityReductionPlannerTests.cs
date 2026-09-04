using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class MortalWoundTreatmentSeverityReductionPlannerTests
{
    [Theory]
    [InlineData(1, "II", 2)]
    [InlineData(2, "I", 1)]
    public void Project_ValidReductionPreservesSemanticGraphAndChangesNoRuntimeIdentity(
        int steps,
        string expectedValue,
        int expectedRank)
    {
        var source = CreateRankThreeWound(steps == 1
            ? new[] { "action_control", "resistance_modifier" }
            : new[] { "action_control" });
        if (steps == 1)
        {
            source["complications"] = new JsonArray(new JsonObject
            {
                ["complicationId"] = "complication_destination_restriction",
                ["kind"] = "impairment",
                ["state"] = "active",
                ["displayName"] = "Restricted movement",
                ["treatmentDifficultyModifier"] = 1,
                ["ownedEffectIds"] = new JsonArray("effect_destination_2"),
                ["visibility"] = "known_to_player"
            });
        }
        var before = Parse(source);
        var beforeJson = JsonNode.Parse(
            WoundMaterializationContract.SerializeCanonical(before))!.AsObject();

        var result = MortalWoundTreatmentSeverityReductionPlanner.Project(
            before,
            steps,
            "turn_43:treatment_projection");

        Assert.True(result.IsValid, Describe(result.Issues));
        var projection = Assert.IsType<MortalWoundTreatmentSeverityReductionProjection>(
            result.Projection);
        var after = projection.ProvisionalAfter;
        var afterJson = JsonNode.Parse(
            WoundMaterializationContract.SerializeCanonical(after))!.AsObject();
        Assert.Equal(expectedValue, after.Severity.Value);
        Assert.Equal(expectedRank, after.Severity.Rank);
        Assert.Equal("III", after.Severity.MaximumAtCreation);
        Assert.Equal("turn_43:treatment_projection", after.Severity.LastChangeEventRef);
        Assert.Equal(expectedRank, after.Consequences.SlotBudget);
        Assert.Equal(steps, projection.Steps);
        Assert.False(string.IsNullOrWhiteSpace(projection.Fingerprint));
        foreach (var member in new[]
                 {
                     "origin", "classification", "display", "care", "complications",
                     "treatment", "recovery", "relations", "lastTransition"
                 })
        {
            Assert.True(
                JsonNode.DeepEquals(beforeJson[member], afterJson[member]),
                $"Projection changed wound.{member}.");
        }
        foreach (var member in new[] { "slotsUsed", "ownedEffectSources", "entries" })
        {
            Assert.True(
                JsonNode.DeepEquals(
                    beforeJson["consequences"]![member],
                    afterJson["consequences"]![member]),
                $"Projection changed wound.consequences.{member}.");
        }

        var oldEffectIds = before.Consequences.OwnedEffectSources.RootBindings
            .Select(static binding => binding.EffectId)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(
            oldEffectIds,
            after.Consequences.OwnedEffectSources.RootBindings
                .Select(static binding => binding.EffectId)
                .Order(StringComparer.Ordinal));
        Assert.Equal(oldEffectIds, projection.Roots.Select(static root => root.PriorEffectId));
        Assert.DoesNotContain(
            projection.Roots,
            static root => root.PriorEffectId.Contains("replacement", StringComparison.Ordinal));
        Assert.NotSame(projection.Before, projection.Before);
        Assert.NotSame(projection.ProvisionalAfter, projection.ProvisionalAfter);
        Assert.NotSame(projection.Roots, projection.Roots);
        var rootsReadback = Assert.IsType<MortalWoundTreatmentRematerializationRoot[]>(
            projection.Roots);
        rootsReadback[0] = null!;
        Assert.NotNull(projection.Roots[0]);
        if (steps == 1)
        {
            Assert.Equal("base_wound", projection.Roots[0].OwnershipDomain.Kind);
            Assert.Equal("complication", projection.Roots[1].OwnershipDomain.Kind);
            Assert.Equal(
                "complication_destination_restriction",
                projection.Roots[1].OwnershipDomain.ComplicationId);
        }
    }

    [Fact]
    public void Project_DestinationEnvelopeRejectsExcessSlotsWithoutPruning()
    {
        var before = Parse(CreateRankThreeWound(
            "action_control",
            "resistance_modifier",
            "periodic_damage"));
        var canonicalBefore = WoundMaterializationContract.SerializeCanonical(before);

        var result = MortalWoundTreatmentSeverityReductionPlanner.Project(
            before,
            1,
            "turn_43:slot_overflow");

        Assert.False(result.IsValid);
        Assert.Null(result.Projection);
        Assert.NotEmpty(result.Issues);
        Assert.Equal(canonicalBefore, WoundMaterializationContract.SerializeCanonical(before));
        Assert.Equal(3, before.Consequences.SlotsUsed);
        Assert.Equal(3, before.Consequences.Entries.Count);
    }

    [Fact]
    public void Project_DestinationEnvelopeRejectsOverpoweredComponentWithoutWeakening()
    {
        var source = CreateRankThreeWound("action_control");
        source["consequences"]!["ownedEffectSources"]!["definitions"]![0]!
            ["components"]![0]!["payload"]!["operation"] = "forbid";
        var before = Parse(source);
        var canonicalBefore = WoundMaterializationContract.SerializeCanonical(before);

        var result = MortalWoundTreatmentSeverityReductionPlanner.Project(
            before,
            1,
            "turn_43:power_overflow");

        Assert.False(result.IsValid);
        Assert.Null(result.Projection);
        Assert.Contains(
            result.Issues,
            static issue => string.Equals(
                issue.Code,
                "wound_consequence_action_forbid_invalid",
                StringComparison.Ordinal));
        Assert.Equal(canonicalBefore, WoundMaterializationContract.SerializeCanonical(before));
    }

    [Fact]
    public void Project_DestinationEnvelopeRejectsReactionExpansionPowerWithoutWeakening()
    {
        var source = CreateRankFourReactionWound();
        var before = Parse(source);
        var canonicalBefore = WoundMaterializationContract.SerializeCanonical(before);

        var result = MortalWoundTreatmentSeverityReductionPlanner.Project(
            before,
            1,
            "turn_43:reaction_power_overflow");

        Assert.False(result.IsValid);
        Assert.Null(result.Projection);
        Assert.Contains(result.Issues, static issue =>
            string.Equals(
                issue.Code,
                "wound_consequence_magnitude_exceeded",
                StringComparison.Ordinal) &&
            string.Equals(
                issue.FilePath,
                "mortalWoundTreatment.severityReductionProjection.before.consequences.ownedEffectSources.definitions[0].components[0].payload.value",
                StringComparison.Ordinal));
        Assert.Equal(canonicalBefore, WoundMaterializationContract.SerializeCanonical(before));
        var definitions = before.Consequences.OwnedEffectSources.Definitions;
        Assert.Equal(2, definitions.Count);
        Assert.Contains(definitions, static definition =>
            definition.GetProperty("definitionKey").GetString() ==
            "definition_destination_leaf");
    }

    private static JsonObject CreateRankThreeWound(params string[] profiles)
    {
        var wound = WoundContractTestData.CreateActiveWound();
        wound["severity"]!["value"] = "III";
        wound["severity"]!["rank"] = 3;
        wound["severity"]!["maximumAtCreation"] = "III";
        wound["consequences"]!["slotBudget"] = 3;
        wound["consequences"]!["slotsUsed"] = profiles.Length;
        var roots = profiles.Select((profile, index) => (
            EffectId: $"effect_destination_{index + 1}",
            DefinitionKey: $"definition_destination_{index + 1}",
            Profile: profile)).ToArray();
        wound["consequences"]!["ownedEffectSources"] =
            WoundContractTestData.CreateOwnedEffectSourcesForTarget(
                wound["woundId"]!.GetValue<string>(),
                "mortal_world",
                "player",
                roots);
        wound["consequences"]!["entries"] = new JsonArray(
            profiles.Select((profile, index) => (JsonNode)new JsonObject
            {
                ["slot"] = index + 1,
                ["profileKey"] = profile,
                ["effectId"] = roots[index].EffectId,
                ["readableSummary"] = $"Preserved destination slot {index + 1}."
            }).ToArray());
        return wound;
    }

    private static JsonObject CreateRankFourReactionWound()
    {
        var wound = CreateRankThreeWound("event_reaction", "characteristic_modifier");
        wound["severity"]!["value"] = "IV";
        wound["severity"]!["rank"] = 4;
        wound["severity"]!["maximumAtCreation"] = "IV";
        wound["consequences"]!["slotBudget"] = 4;
        var sources = wound["consequences"]!["ownedEffectSources"]!.AsObject();
        var root = WoundContractTestData.CreateApplyDefinitionRoot(
            wound["woundId"]!.GetValue<string>(),
            "mortal_world",
            "definition_destination_reaction",
            "definition_destination_leaf");
        var leaf = WoundContractTestData.CreateOwnedEffectDefinition(
            wound["woundId"]!.GetValue<string>(),
            "mortal_world",
            "definition_destination_leaf",
            "characteristic_modifier");
        leaf["components"]![0]!["payload"]!["operation"] = "flat";
        leaf["components"]![0]!["payload"]!["value"] = 4;
        sources["definitions"] = new JsonArray(root, leaf);
        sources["rootBindings"] = new JsonArray(
            WoundContractTestData.CreateRootBinding(
                "effect_destination_reaction",
                "definition_destination_reaction"));
        wound["consequences"]!["slotsUsed"] = 2;
        wound["consequences"]!["entries"] = new JsonArray(
            new JsonObject
            {
                ["slot"] = 1,
                ["profileKey"] = "event_reaction",
                ["effectId"] = "effect_destination_reaction",
                ["readableSummary"] = "The wound reacts to renewed harm."
            },
            new JsonObject
            {
                ["slot"] = 2,
                ["profileKey"] = "characteristic_modifier",
                ["effectId"] = "effect_destination_reaction",
                ["readableSummary"] = "The reaction imposes a severe penalty."
            });
        return wound;
    }

    private static WoundMaterializationEnvelope Parse(JsonObject wound)
    {
        var parsed = WoundMaterializationContract.Parse(
            wound.ToJsonString(),
            "projectionTest.wound");
        Assert.True(parsed.IsValid, Describe(parsed.Issues));
        return Assert.IsType<WoundMaterializationEnvelope>(parsed.Wound);
    }

    private static string Describe(IEnumerable<ValidationIssue> issues) => string.Join(
        Environment.NewLine,
        issues.Select(static issue =>
            $"{issue.Code}@{issue.FilePath}: {issue.Expected}; actual={issue.Actual}"));
}
