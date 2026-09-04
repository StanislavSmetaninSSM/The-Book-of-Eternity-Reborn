using System.Reflection;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class MortalWoundTreatmentSeverityReductionPlannerTests
{
    [Fact]
    public void Prepare_StabilizeThenReducePreservesDeclaredOrdinalOrder()
    {
        var before = Parse(CreateRankThreeWound("action_control"));
        var projected = AssertValidProjection(before, 1);
        var firstResolution = CreateSyntheticResolution("s,r1");
        var secondResolution = CreateSyntheticResolution("r1,s");
        var first = CreateSyntheticPreparation(firstResolution, projected);
        var second = CreateSyntheticPreparation(secondResolution, projected);

        Assert.True(first.AgreesWith(firstResolution));
        Assert.True(second.AgreesWith(secondResolution));
        Assert.NotEqual(first.Fingerprint, second.Fingerprint);
        Assert.Equal(1, first.SeverityReduction!.Steps);
        Assert.Equal("II", first.ProvisionalAfter.Severity.Value);
        Assert.NotSame(first.Before, first.Before);
        Assert.NotSame(first.ProvisionalAfter, first.ProvisionalAfter);
        Assert.NotSame(first.SeverityReduction, first.SeverityReduction);
    }

    [Fact]
    public void Prepare_TwoOneStepReductionsAggregateToOneAtomicDestination()
    {
        var before = Parse(CreateRankThreeWound("action_control"));
        var projected = AssertValidProjection(before, 2);
        var resolution = CreateSyntheticResolution("r1,r1");
        var preparation = CreateSyntheticPreparation(resolution, projected);

        Assert.True(preparation.AgreesWith(resolution));
        Assert.Equal(2, preparation.SeverityReduction!.Steps);
        Assert.Equal("I", preparation.ProvisionalAfter.Severity.Value);
        Assert.Equal(1, preparation.ProvisionalAfter.Severity.Rank);
    }

    [Theory]
    [InlineData("s,r1,r1")]
    [InlineData("r1,s,r1")]
    [InlineData("r1,r1,s")]
    public void Prepare_StabilizationPlacementIsBoundInOrderedPreparation(string shape)
    {
        var before = Parse(CreateRankThreeWound("action_control"));
        var projected = AssertValidProjection(before, 2);
        var resolution = CreateSyntheticResolution(shape);
        var preparation = CreateSyntheticPreparation(resolution, projected);

        Assert.True(preparation.AgreesWith(resolution));
        Assert.False(string.IsNullOrWhiteSpace(preparation.Fingerprint));
        Assert.Equal("I", preparation.ProvisionalAfter.Severity.Value);
    }

    [Theory]
    [InlineData("steps")]
    [InlineData("ordinal")]
    [InlineData("intent_fingerprint")]
    [InlineData("route_completion")]
    public void Prepare_SealedMismatchIsRejected(string mutation)
    {
        var before = Parse(CreateRankThreeWound("action_control"));
        var projected = AssertValidProjection(before, 1);
        var resolution = CreateSyntheticResolution("r1");
        var preparation = CreateSyntheticPreparation(resolution, projected);
        var original = Assert.IsType<MortalWoundReduceSeverityOutcomeIntent>(
            Assert.Single(resolution.OutcomeIntents));
        var changedIntent = mutation switch
        {
            "steps" => MortalWoundReduceSeverityOutcomeIntent.Create(
                0,
                original.DeclaredOperationFingerprint,
                original.IntentFingerprint,
                2),
            "ordinal" => MortalWoundReduceSeverityOutcomeIntent.Create(
                1,
                original.DeclaredOperationFingerprint,
                original.IntentFingerprint,
                1),
            "intent_fingerprint" => MortalWoundReduceSeverityOutcomeIntent.Create(
                0,
                original.DeclaredOperationFingerprint,
                original.IntentFingerprint + "_changed",
                1),
            _ => original
        };
        var changed = CreateSyntheticResolution(
            new MortalWoundTreatmentOperation[] { new MortalWoundReduceSeverityOperation(1) },
            new MortalWoundTreatmentOutcomeIntent[] { changedIntent },
            mutation == "route_completion" ? "None" : "AppendOnce");

        var result = MortalWoundTreatmentOutcomePublicationPlanner.Finalize(
            preparation,
            changed,
            null,
            new Dictionary<string, EffectAcceptedApplicationResult>(
                StringComparer.Ordinal));

        Assert.False(result.IsValid);
        Assert.Null(result.DeclaredOutcome);
        Assert.Contains(result.Issues, static issue =>
            issue.Code == "mortal_wound_treatment_outcome_preparation_mismatch");
    }

    [Fact]
    public void Prepare_UnchangedResultFinalizesOnlyFromExactProvisionalAfter()
    {
        var before = Parse(WoundContractTestData.CreateActiveWound());
        var resolution = CreateSyntheticResolution("n", routeCompletion: "None");
        var preparation = CreateSyntheticPreparation(
            resolution,
            severityReduction: null,
            provisionalAfter: before);

        var result = MortalWoundTreatmentOutcomePublicationPlanner.Finalize(
            preparation,
            resolution,
            null,
            new Dictionary<string, EffectAcceptedApplicationResult>(
                StringComparer.Ordinal));

        Assert.True(result.IsValid, Describe(result.Issues));
        Assert.Equal(
            WoundMaterializationContract.SerializeCanonical(preparation.ProvisionalAfter),
            WoundMaterializationContract.SerializeCanonical(result.After!));
        Assert.Equal(before.Severity.Rank, result.DeclaredOutcome!.ResultingSeverityRank);
        Assert.False(string.IsNullOrWhiteSpace(result.Fingerprint));
    }

    [Fact]
    public void FinalizeReduction_WithoutAuthenticatedBatchFailsClosed()
    {
        var before = Parse(CreateRankThreeWound("action_control"));
        var resolution = CreateSyntheticResolution("r1");
        var preparation = CreateSyntheticPreparation(
            resolution,
            AssertValidProjection(before, 1));
        var arbitrary = new Dictionary<string, EffectAcceptedApplicationResult>(
            StringComparer.Ordinal)
        {
            ["application_arbitrary"] = null!
        };

        var result = MortalWoundTreatmentOutcomePublicationPlanner.Finalize(
            preparation,
            resolution,
            null,
            arbitrary);

        Assert.False(result.IsValid);
        Assert.Null(result.After);
        Assert.Null(result.DeclaredOutcome);
        Assert.Contains(result.Issues, static issue =>
            issue.Code == "mortal_wound_treatment_publication_slice_unsupported");
    }

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

    private static MortalWoundTreatmentSeverityReductionProjection AssertValidProjection(
        WoundMaterializationEnvelope before,
        int steps)
    {
        var result = MortalWoundTreatmentSeverityReductionPlanner.Project(
            before,
            steps,
            "turn_43:treatment_projection");
        Assert.True(result.IsValid, Describe(result.Issues));
        return Assert.IsType<MortalWoundTreatmentSeverityReductionProjection>(
            result.Projection);
    }

    private static MortalWoundTreatmentOutcomePreparation CreateSyntheticPreparation(
        MortalWoundTreatmentResolution resolution,
        MortalWoundTreatmentSeverityReductionProjection? severityReduction,
        WoundMaterializationEnvelope? provisionalAfter = null)
    {
        var before = severityReduction?.Before ?? provisionalAfter ??
            throw new InvalidOperationException("A synthetic preparation needs a wound.");
        var after = provisionalAfter ?? severityReduction!.ProvisionalAfter;
        const string transitionId = "wound_transition_t070_b6_direct";
        const long currentMinute = 1_260;
        var fingerprint =
            MortalWoundTreatmentOutcomePublicationPlanner.ComputePreparationFingerprint(
                before,
                after,
                resolution,
                transitionId,
                currentMinute,
                severityReduction?.Fingerprint);
        return new MortalWoundTreatmentOutcomePreparation(
            before,
            after,
            transitionId,
            severityReduction,
            resolution.RequestFingerprint,
            resolution.ResultFingerprint,
            resolution.ResolutionAuthorityFingerprint,
            resolution.OutcomeIntents,
            resolution.RouteCompletion,
            resolution.ResultCategory,
            resolution.SelectedOutcomeIndex,
            currentMinute,
            fingerprint);
    }

    private static MortalWoundTreatmentResolution CreateSyntheticResolution(
        string shape,
        string routeCompletion = "AppendOnce")
    {
        var operations = shape.Split(',').Select(static token => token switch
        {
            "n" => (MortalWoundTreatmentOperation)new MortalWoundNoImprovementOperation(),
            "s" => new MortalWoundStabilizeOperation(),
            "r1" => new MortalWoundReduceSeverityOperation(1),
            _ => throw new ArgumentOutOfRangeException(nameof(shape), token, null)
        }).ToArray();
        var intents = operations.Select((operation, ordinal) =>
        {
            var declared = MortalWoundTreatmentOutcomeIntentComposer.DeclaredFingerprint(
                ordinal, operation);
            var intent = $"sha256:intent_{ordinal}_{operation.Kind}";
            return operation switch
            {
                MortalWoundNoImprovementOperation =>
                    (MortalWoundTreatmentOutcomeIntent)
                    MortalWoundNoImprovementOutcomeIntent.Create(
                        ordinal, declared, intent),
                MortalWoundStabilizeOperation => MortalWoundStabilizeOutcomeIntent.Create(
                    ordinal, declared, intent),
                MortalWoundReduceSeverityOperation reduction =>
                    MortalWoundReduceSeverityOutcomeIntent.Create(
                        ordinal, declared, intent, reduction.Steps),
                _ => throw new InvalidOperationException()
            };
        }).ToArray();
        return CreateSyntheticResolution(operations, intents, routeCompletion);
    }

    private static MortalWoundTreatmentResolution CreateSyntheticResolution(
        IReadOnlyList<MortalWoundTreatmentOperation> operations,
        IReadOnlyList<MortalWoundTreatmentOutcomeIntent> intents,
        string routeCompletion)
    {
        var constructor = typeof(MortalWoundTreatmentResolution)
            .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single();
        return Assert.IsType<MortalWoundTreatmentResolution>(constructor.Invoke(new object?[]
        {
            "procedure",
            null,
            "AcceptedTerminal",
            routeCompletion == "AppendOnce" ? "success" : "failed_attempt",
            0,
            false,
            operations,
            intents,
            null,
            "never",
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            "sha256:route",
            "sha256:resolution",
            "sha256:request",
            "sha256:result",
            routeCompletion
        }));
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
