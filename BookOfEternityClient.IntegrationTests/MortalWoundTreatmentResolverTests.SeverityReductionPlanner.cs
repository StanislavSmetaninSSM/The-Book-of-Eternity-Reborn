using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalWoundTreatmentResolverTests
{
    // These 41 rows were pure projection callers until finalization began requiring
    // the real detached treatment selection. Only this admission seam owns files.
    private sealed class ReductionPlannerFixture : IDisposable
    {
        internal ReductionPlannerFixture(AcceptedStateFixture fixture, TreatmentFlow flow,
            MortalWoundTreatmentOutcomePreparation preparation)
        {
            Fixture = fixture;
            Flow = flow;
            Preparation = preparation;
        }
        internal AcceptedStateFixture Fixture { get; }
        internal TreatmentFlow Flow { get; }
        internal MortalWoundTreatmentResolution Resolution =>
            Assert.IsType<MortalWoundTreatmentResolution>(Flow.Resolution);
        internal MortalWoundTreatmentOutcomePreparation Preparation { get; }
        public void Dispose() => Fixture.Dispose();
    }

    private static ReductionPlannerFixture CreateReductionPlannerFixture(
        WoundMaterializationEnvelope before, string shape, string routeCompletion = "AppendOnce")
    {
        var scenario = CreateScenario("procedure_normal_uses_lowest_free_die", "procedure");
        var root = CanonicalWoundRoot(before);
        var route = StrictProcedureRoute();
        route["requirements"] = new JsonArray();
        route["resourcePolicy"] = Policy(new JsonArray(), new JsonArray());
        route["resolution"]!["modifierSource"] = new JsonObject { ["kind"] = "fixed_zero" };
        var operations = new JsonArray(shape.Split(',').Select(token => (JsonNode)(token switch
        {
            "n" => new JsonObject { ["kind"] = "no_improvement" },
            "s" => new JsonObject { ["kind"] = "stabilize" },
            "r1" => new JsonObject { ["kind"] = "reduce_severity", ["steps"] = 1 },
            _ => throw new ArgumentOutOfRangeException(nameof(shape))
        })).ToArray());
        foreach (var band in route["outcomes"]!.AsArray().OfType<JsonObject>())
            band["result"] = shape == "n" && band["category"]!.GetValue<string>() != "failed_attempt"
                ? new JsonArray(new JsonObject { ["kind"] = "stabilize" }) : operations.DeepClone();
        root["treatment"]!["routes"] = new JsonArray(route);
        root["treatment"]!["knownRouteIds"] = new JsonArray("procedure_v1");
        root["treatment"]!["completedRouteIds"] = new JsonArray();
        scenario.AcceptedState["acceptedDice"] = new JsonArray(shape == "n" ? 4 : 20);
        scenario = scenario with
        {
            Before = root,
            History = CreateCurrentWoundHistory(root),
            RouteId = "procedure_v1",
            SeedCanonicalWoundEffects = true,
            OperationKey = "operation_t070_reduction_planner"
        };
        var fixture = AcceptedStateFixture.Create(scenario);
        try
        {
            var flow = ResolveCurrentTreatment(fixture, "procedure", scenario.OperationKey, scenario.RouteId);
            var state = Assert.IsType<MortalWoundTreatmentAcceptedStateAuthority>(flow.AcceptedState);
            var resolution = Assert.IsType<MortalWoundTreatmentResolution>(flow.Resolution);
            Assert.Equal(routeCompletion, resolution.RouteCompletion);
            var prepared = MortalWoundTreatmentOutcomePublicationPlanner.Prepare(state,
                Assert.IsType<MortalWoundTreatmentAttemptRequest>(flow.Request), resolution, state.CurrentGameMinute);
            Assert.True(prepared.IsValid, DescribeIssues(prepared.Issues));
            return new ReductionPlannerFixture(fixture, flow, prepared.Preparation!);
        }
        catch { fixture.Dispose(); throw; }
    }

    private static MortalWoundTreatmentOutcomePreparation CreatePreparedReductionForTest(
        ReductionPlannerFixture fixture,
        MortalWoundTreatmentSeverityReductionProjection? severityReduction,
        WoundMaterializationEnvelope? provisionalAfter = null)
    {
        var baseline = fixture.Preparation;
        var resolution = fixture.Resolution;
        if (severityReduction is null && baseline.SeverityReduction is null)
            return baseline.DetachedCopy();
        MortalWoundTreatmentSeverityReductionProjection? changed = null;
        if (severityReduction is not null)
        {
            var source = MortalWoundTreatmentSeverityReductionPlanner.Project(severityReduction.Before,
                severityReduction.Steps, severityReduction.ProvisionalAfter.Severity.LastChangeEventRef).Projection!;
            var validFingerprint = source.Fingerprint == severityReduction.Fingerprint;
            var validAfter = WoundMaterializationContract.SerializeCanonical(source.ProvisionalAfter) ==
                WoundMaterializationContract.SerializeCanonical(severityReduction.ProvisionalAfter);
            var validRoots = source.Roots.Count == severityReduction.Roots.Count && source.Roots.Zip(severityReduction.Roots)
                .All(pair => pair.First.PriorEffectId == pair.Second.PriorEffectId &&
                    pair.First.DefinitionKey == pair.Second.DefinitionKey &&
                    pair.First.OwnershipDomain == pair.Second.OwnershipDomain &&
                    pair.First.Slots.SequenceEqual(pair.Second.Slots));
            var actual = baseline.SeverityReduction;
            if (actual is not null && actual.Steps == severityReduction.Steps && validFingerprint && validAfter && validRoots)
                return baseline.DetachedCopy();
            changed = new MortalWoundTreatmentSeverityReductionProjection(
                actual?.Before ?? baseline.ProvisionalAfter,
                validAfter ? baseline.ProvisionalAfter : baseline.Before,
                severityReduction.Steps,
                validRoots ? actual?.Roots ?? severityReduction.Roots : severityReduction.Roots,
                validFingerprint ? actual?.Fingerprint ?? severityReduction.Fingerprint : severityReduction.Fingerprint);
        }
        var after = changed?.ProvisionalAfter ?? provisionalAfter ?? baseline.ProvisionalAfter;
        var state = Assert.IsType<MortalWoundTreatmentAcceptedStateAuthority>(fixture.Flow.AcceptedState);
        var fingerprint = MortalWoundTreatmentOutcomePublicationPlanner.ComputePreparationFingerprint(
            baseline.Before, after, resolution, baseline.TransitionId, state.CurrentGameMinute, changed?.Fingerprint);
        return new MortalWoundTreatmentOutcomePreparation(baseline.Before, after, baseline.TransitionId,
            changed, resolution.RequestFingerprint, resolution.ResultFingerprint, resolution.ResolutionAuthorityFingerprint,
            resolution.OutcomeIntents, resolution.RouteCompletion, resolution.ResultCategory, resolution.SelectedOutcomeIndex,
            state.CurrentGameMinute, fingerprint, resolution);
    }

    private static WoundAcceptedTurnInput CreateReductionPlannerInput(ReductionPlannerFixture fixture)
    {
        var baselines = Invoke(ExactStaticMethod(typeof(WoundAcceptedTurnPlanner), "ReadPublicationBaselines", 2),
            new object?[] { fixture.Fixture.FileSystem, fixture.Fixture.Lease });
        return new WoundAcceptedTurnInput(
            Assert.IsType<MortalWoundTreatmentAcceptedStateAuthority>(fixture.Flow.AcceptedState).Binding,
            Array.Empty<WoundOpportunityAuthority>(), Array.Empty<WoundAcceptedTransitionDraft>(),
            Assert.IsType<WoundCarrierCatalogInput>(ReadRequiredProperty(baselines, "WoundCarriers")),
            Assert.IsType<JsonObject>(ReadRequiredProperty(baselines, "WoundIdentity")),
            Assert.IsType<JsonObject>(ReadRequiredProperty(baselines, "WoundHistory")),
            Assert.IsType<EffectCarrierCatalogInput>(ReadRequiredProperty(baselines, "EffectCarriers")),
            Assert.IsType<JsonObject>(ReadRequiredProperty(baselines, "EffectIdentity")));
    }

    [Fact]
    public void Prepare_AuthorityFreeResolutionCannotFinalize()
    {
        var before = Parse(CreateRankThreeWound("action_control"));
        using var fixture = CreateReductionPlannerFixture(before, "r1");
        var changed = CloneResolutionWithIntentsUnchecked(fixture.Resolution, fixture.Resolution.OutcomeIntents);
        typeof(MortalWoundTreatmentResolution).GetField("<RequestAuthority>k__BackingField",
            BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(changed, null);
        var result = MortalWoundTreatmentOutcomePublicationPlanner.Finalize(fixture.Preparation, changed,
            null, new Dictionary<string, EffectAcceptedApplicationResult>());
        Assert.False(result.IsValid);
        Assert.Null(result.After);
        Assert.Contains(result.Issues, issue => issue.Code == "mortal_wound_treatment_outcome_preparation_mismatch");
    }

    [Fact]
    public void Prepare_StabilizeThenReducePreservesDeclaredOrdinalOrder()
    {
        var before = Parse(CreateRankThreeWound("action_control"));
        var projected = AssertValidProjection(before, 1);
        using var firstResolutionFixture = CreateReductionPlannerFixture(before, "s,r1");
        var firstResolution = firstResolutionFixture.Resolution;
        using var secondResolutionFixture = CreateReductionPlannerFixture(before, "r1,s");
        var secondResolution = secondResolutionFixture.Resolution;
        var first = CreatePreparedReductionForTest(
            firstResolutionFixture, projected);
        var second = CreatePreparedReductionForTest(
            secondResolutionFixture, projected);

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
        using var resolutionFixture = CreateReductionPlannerFixture(before, "r1,r1");
        var resolution = resolutionFixture.Resolution;
        var preparation = CreatePreparedReductionForTest(
            resolutionFixture, projected);

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
        using var resolutionFixture = CreateReductionPlannerFixture(before, shape);
        var resolution = resolutionFixture.Resolution;
        var preparation = CreatePreparedReductionForTest(
            resolutionFixture, projected);

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
        using var resolutionFixture = CreateReductionPlannerFixture(before, "r1");
        var resolution = resolutionFixture.Resolution;
        var preparation = CreatePreparedReductionForTest(
            resolutionFixture, projected);
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
        var changed = CloneResolutionWithIntentsUnchecked(resolution, new[] { changedIntent });
        if (mutation == "route_completion")
            SetCourseTestBackingField(changed, "RouteCompletion", "None");

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
        using var resolutionFixture = CreateReductionPlannerFixture(before, "n", routeCompletion: "None");
        var resolution = resolutionFixture.Resolution;
        var preparation = CreatePreparedReductionForTest(
            resolutionFixture,
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
        using var resolutionFixture = CreateReductionPlannerFixture(before, "r1");
        var resolution = resolutionFixture.Resolution;
        var preparation = CreatePreparedReductionForTest(
            resolutionFixture,
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
            issue.Code == "mortal_wound_treatment_outcome_effect_handoff_mismatch");
    }

    [Fact]
    public void FinalizeReduction_AuthenticatedResultsBuildOneFreshCanonicalWound()
    {
        var source = CreateRankThreeWound(
            "action_control",
            "resistance_modifier");
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
        var before = Parse(source);
        using var resolutionFixture = CreateReductionPlannerFixture(before, "r1");
        var resolution = resolutionFixture.Resolution;
        var preparation = CreatePreparedReductionForTest(
            resolutionFixture,
            AssertValidProjection(before, 1));
        var input = CreateReductionPlannerInput(resolutionFixture);
        var (batch, _) = InvokeRematerializationPrepare(
            input,
            preparation,
            resolution);
        var accepted = CreateAcceptedApplications(
            batch,
            "effect_final_zeta",
            "effect_final_alpha");

        var result = MortalWoundTreatmentOutcomePublicationPlanner.Finalize(
            preparation,
            resolution,
            batch,
            CreateApplicationMap(accepted));

        Assert.True(result.IsValid, Describe(result.Issues));
        var after = Assert.IsType<WoundMaterializationEnvelope>(result.After);
        Assert.Equal("II", after.Severity.Value);
        Assert.Equal(2, after.Severity.Rank);
        Assert.Equal(
            new[] { "effect_final_alpha", "effect_final_zeta" },
            after.Consequences.OwnedEffectSources.RootBindings
                .Select(static binding => binding.EffectId));
        Assert.Equal(
            new[] { 1, 2 },
            after.Consequences.Entries.Select(static entry => entry.Slot));
        Assert.Equal(
            new[] { "effect_final_alpha" },
            Assert.Single(after.Complications).OwnedEffectIds);
        Assert.DoesNotContain(
            after.Consequences.OwnedEffectSources.RootBindings,
            binding => before.Consequences.OwnedEffectSources.RootBindings.Any(
                prior => string.Equals(
                    prior.EffectId,
                    binding.EffectId,
                    StringComparison.Ordinal)));
        Assert.Equal(
            after.Consequences.OwnedEffectSources.RootBindings
                .Select(static binding => binding.EffectId)
                .Order(StringComparer.Ordinal),
            result.DeclaredOutcome!.ResultingEffectIds);
    }

    [Fact]
    public void FinalizeReduction_SkillScopedRollPreservesSelectorAndReplacesRuntimeIdentity()
    {
        var source = CreateRankThreeWound("roll_modifier");
        var sourceDefinition = source["consequences"]!["ownedEffectSources"]!
            ["definitions"]![0]!.AsObject();
        var sourceComponent = sourceDefinition["components"]![0]!.AsObject();
        sourceComponent["payload"]!["operations"] = new JsonArray("skill_check");
        sourceComponent["payload"]!["scope"] = new JsonObject
        {
            ["kind"] = "skill",
            ["skillId"] = "skill_medicine"
        };
        var changedSelector = source.DeepClone().AsObject();
        changedSelector["consequences"]!["ownedEffectSources"]!["definitions"]![0]!
            ["components"]![0]!["payload"]!["scope"]!["skillId"] =
            "skill_lockpicking";
        var before = Parse(source);
        var changed = Parse(changedSelector);
        Assert.NotEqual(
            WoundIdentityState.ComputeSemanticFingerprint(before),
            WoundIdentityState.ComputeSemanticFingerprint(changed));

        var priorBinding = Assert.Single(
            before.Consequences.OwnedEffectSources.RootBindings);
        var beforeDefinition = Assert.Single(
            before.Consequences.OwnedEffectSources.Definitions);
        var beforeScope = beforeDefinition.GetProperty("components")[0]
            .GetProperty("payload").GetProperty("scope").GetRawText();
        var beforeComponentId = beforeDefinition.GetProperty("components")[0]
            .GetProperty("componentId").GetString();
        using var resolutionFixture = CreateReductionPlannerFixture(before, "r1");
        var resolution = resolutionFixture.Resolution;
        var preparation = CreatePreparedReductionForTest(
            resolutionFixture,
            AssertValidProjection(before, 1));
        var input = CreateReductionPlannerInput(resolutionFixture);
        var (batch, _) = InvokeRematerializationPrepare(
            input,
            preparation,
            resolution);
        var accepted = Assert.Single(CreateAcceptedApplications(
            batch,
            "effect_final_skill_scope"));

        var result = MortalWoundTreatmentOutcomePublicationPlanner.Finalize(
            preparation,
            resolution,
            batch,
            CreateApplicationMap(new[] { accepted }));

        Assert.True(result.IsValid, Describe(result.Issues));
        var after = Assert.IsType<WoundMaterializationEnvelope>(result.After);
        var afterBinding = Assert.Single(
            after.Consequences.OwnedEffectSources.RootBindings);
        Assert.Equal("effect_final_skill_scope", afterBinding.EffectId);
        Assert.NotEqual(priorBinding.EffectId, afterBinding.EffectId);
        Assert.Equal(accepted.EffectId, afterBinding.EffectId);
        Assert.False(string.IsNullOrWhiteSpace(accepted.CreateTransitionId));
        var afterDefinition = Assert.Single(
            after.Consequences.OwnedEffectSources.Definitions);
        var afterComponent = afterDefinition.GetProperty("components")[0];
        Assert.Equal(
            beforeScope,
            afterComponent.GetProperty("payload").GetProperty("scope").GetRawText());
        Assert.Equal(
            "skill_medicine",
            afterComponent.GetProperty("payload").GetProperty("scope")
                .GetProperty("skillId").GetString());
        Assert.Equal(beforeComponentId, afterComponent.GetProperty("componentId").GetString());
    }

    [Fact]
    public void FinalizeReduction_AuthenticatedEmptyBatchPublishesZeroRootSeverityChange()
    {
        var before = Parse(CreateRankThreeWound());
        using var resolutionFixture = CreateReductionPlannerFixture(before, "r1");
        var resolution = resolutionFixture.Resolution;
        var preparation = CreatePreparedReductionForTest(
            resolutionFixture,
            AssertValidProjection(before, 1));
        var input = CreateReductionPlannerInput(resolutionFixture);
        var (batch, _) = InvokeRematerializationPrepare(
            input,
            preparation,
            resolution);

        var result = MortalWoundTreatmentOutcomePublicationPlanner.Finalize(
            preparation,
            resolution,
            batch,
            new Dictionary<string, EffectAcceptedApplicationResult>(
                StringComparer.Ordinal));

        Assert.True(result.IsValid, Describe(result.Issues));
        Assert.Empty(batch.RootApplications);
        Assert.Empty(batch.TerminalOperations);
        Assert.Equal("II", result.After!.Severity.Value);
        Assert.Empty(result.After.Consequences.OwnedEffectSources.RootBindings);
        Assert.Empty(result.After.Consequences.Entries);
        Assert.Empty(result.DeclaredOutcome!.ResultingEffectIds);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("extra")]
    [InlineData("reordered")]
    [InlineData("borrowed")]
    [InlineData("changed_result")]
    [InlineData("malformed_result")]
    [InlineData("changed_slots")]
    [InlineData("duplicate_effect_id")]
    [InlineData("confusable_effect_id")]
    [InlineData("reused_prior_id")]
    public void FinalizeReduction_InexactApplicationResultMapFailsClosed(string mutation)
    {
        var before = Parse(CreateRankThreeWound(
            "action_control",
            "resistance_modifier"));
        using var resolutionFixture = CreateReductionPlannerFixture(before, "r1");
        var resolution = resolutionFixture.Resolution;
        var preparation = CreatePreparedReductionForTest(
            resolutionFixture,
            AssertValidProjection(before, 1));
        var input = CreateReductionPlannerInput(resolutionFixture);
        var (batch, _) = InvokeRematerializationPrepare(
            input,
            preparation,
            resolution);
        var accepted = CreateAcceptedApplications(
            batch,
            "effect_final_alpha",
            "effect_final_beta").ToList();
        switch (mutation)
        {
            case "missing":
                accepted.RemoveAt(1);
                break;
            case "extra":
                accepted.Add(accepted[0] with
                {
                    ApplicationRef = "application_unexpected",
                    EffectId = "effect_final_unexpected"
                });
                break;
            case "reordered":
                accepted.Reverse();
                break;
            case "borrowed":
                accepted[0] = accepted[1] with
                {
                    ApplicationRef = accepted[0].ApplicationRef
                };
                break;
            case "changed_result":
                accepted[0] = accepted[0] with
                {
                    SourceKey = accepted[0].SourceKey with
                    {
                        DefinitionKey = "definition_changed"
                    }
                };
                break;
            case "malformed_result":
                accepted[0] = accepted[0] with
                {
                    Materialization = null!
                };
                break;
            case "changed_slots":
                accepted[0] = accepted[0] with
                {
                    Materialization = new WoundEffectMaterializationAgreement(
                        accepted[0].Materialization.SlotBindings.Select(slot =>
                            slot with { Slot = slot.Slot + 1 }).ToArray(),
                        accepted[0].Materialization.ComponentCount,
                        accepted[0].Materialization.MaterializationFingerprint)
                };
                break;
            case "duplicate_effect_id":
                accepted[1] = accepted[1] with
                {
                    EffectId = accepted[0].EffectId
                };
                break;
            case "confusable_effect_id":
                accepted[1] = accepted[1] with
                {
                    EffectId = accepted[0].EffectId.ToUpperInvariant()
                };
                break;
            case "reused_prior_id":
                accepted[0] = accepted[0] with
                {
                    EffectId = batch.RootApplications[0].PriorRootEffectId!
                };
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation));
        }

        var result = MortalWoundTreatmentOutcomePublicationPlanner.Finalize(
            preparation,
            resolution,
            batch,
            CreateApplicationMap(accepted));

        Assert.False(result.IsValid);
        Assert.Null(result.After);
        Assert.Null(result.DeclaredOutcome);
        Assert.Contains(result.Issues, static issue =>
            issue.Code == "mortal_wound_treatment_outcome_effect_handoff_mismatch");
    }

    [Fact]
    public void FinalizeReduction_NullProjectionCannotBypassFailClosedBoundary()
    {
        var before = Parse(CreateRankThreeWound("action_control"));
        using var resolutionFixture = CreateReductionPlannerFixture(before, "r1");
        var resolution = resolutionFixture.Resolution;
        var preparation = CreatePreparedReductionForTest(
            resolutionFixture,
            severityReduction: null,
            provisionalAfter: before);

        var result = MortalWoundTreatmentOutcomePublicationPlanner.Finalize(
            preparation,
            resolution,
            null,
            new Dictionary<string, EffectAcceptedApplicationResult>(
                StringComparer.Ordinal));

        Assert.False(result.IsValid);
        Assert.Null(result.After);
        Assert.Null(result.DeclaredOutcome);
        Assert.Contains(result.Issues, static issue =>
            issue.Code == "mortal_wound_treatment_outcome_preparation_mismatch");
    }

    [Fact]
    public void PrepareReductionBatch_CompleteCurrentTeardownAndGenerationLinksAreSealed()
    {
        var before = Parse(CreateRankThreeWound(
            "action_control",
            "resistance_modifier"));
        using var resolutionFixture = CreateReductionPlannerFixture(before, "r1");
        var resolution = resolutionFixture.Resolution;
        var preparation = CreatePreparedReductionForTest(
            resolutionFixture,
            AssertValidProjection(before, 1));
        var input = CreateReductionPlannerInput(resolutionFixture);

        var (batch, authority) = InvokeRematerializationPrepare(
            input,
            preparation,
            resolution);

        Assert.Equal(2, batch.RootApplications.Count);
        Assert.Equal(2, batch.TerminalOperations.Count);
        Assert.DoesNotContain(batch.RootLineageAuthority, static row =>
            row.EffectId is not null);
        Assert.Equal(
            preparation.SeverityReduction!.Roots
                .Select(static root => root.PriorEffectId),
            batch.RootApplications.Select(ReadPriorRootEffectId));
        Assert.All(batch.RootApplications, static root =>
            Assert.Null(root.SourceSelector.SourceId));
        Assert.All(batch.RootApplications, root =>
            Assert.Equal(batch.LocalWoundRef, root.SourceSelector.SourceRef));
        Assert.Equal("treat", batch.TransitionAuthority.TransitionKind);
        AssertPrivateAuthoritySeals(authority);
    }

    [Fact]
    public void ReductionEffectHandoff_TreatBatchRequiresPrivateAuthority()
    {
        var before = Parse(CreateRankThreeWound("action_control"));
        using var resolutionFixture = CreateReductionPlannerFixture(before, "r1");
        var resolution = resolutionFixture.Resolution;
        var preparation = CreatePreparedReductionForTest(
            resolutionFixture,
            AssertValidProjection(before, 1));
        var input = CreateReductionPlannerInput(resolutionFixture);
        var (batch, _) = InvokeRematerializationPrepare(
            input,
            preparation,
            resolution);
        var baseline = WoundAcceptedTurnPlannerCore.CreateBaselineAuthority(
            WoundAcceptedTurnFingerprints.ComputeInput(input),
            input);
        var provisional = new WoundPreparedAcceptedTurnPlan(
            input.Binding,
            WoundAcceptedTurnFingerprints.ComputeBinding(input.Binding),
            WoundAcceptedTurnFingerprints.ComputeInput(input),
            string.Empty,
            new[] { before.WoundId },
            new[] { preparation.TransitionId },
            new[] { preparation.ProvisionalAfter },
            new[] { batch },
            baseline);
        var forged = new WoundPreparedAcceptedTurnPlan(
            input.Binding,
            provisional.BindingFingerprint,
            provisional.InputFingerprint,
            WoundAcceptedTurnFingerprints.ComputePreparation(provisional),
            provisional.AllocatedWoundIds,
            provisional.AllocatedTransitionIds,
            provisional.PreparedWounds,
            provisional.EffectOperationBatches,
            provisional.BaselineAuthority);

        var issues = WoundAcceptedTurnPlannerCore.ValidatePreparedAuthority(forged);

        Assert.Contains(issues, static issue =>
            issue.Code == "wound_plan_prepared_seal_mismatch");
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("reordered")]
    [InlineData("extra")]
    public void PrepareReductionBatch_ChangedTerminalTopologyCannotBorrowPrivateSeal(
        string mutation)
    {
        var before = Parse(CreateRankThreeWound(
            "action_control",
            "resistance_modifier"));
        using var resolutionFixture = CreateReductionPlannerFixture(before, "r1");
        var resolution = resolutionFixture.Resolution;
        var preparation = CreatePreparedReductionForTest(
            resolutionFixture,
            AssertValidProjection(before, 1));
        var input = CreateReductionPlannerInput(resolutionFixture);
        var (batch, authority) = InvokeRematerializationPrepare(
            input,
            preparation,
            resolution);
        var terminals = mutation switch
        {
            "missing" => batch.TerminalOperations.Skip(1).ToArray(),
            "reordered" => batch.TerminalOperations.Reverse().ToArray(),
            "extra" => batch.TerminalOperations
                .Append(batch.TerminalOperations[0])
                .ToArray(),
            _ => throw new ArgumentOutOfRangeException(nameof(mutation))
        };
        var provisional = new WoundEffectOperationBatch(
            batch.LocalWoundRef,
            batch.PreparedWoundId,
            batch.SourceExport,
            batch.RootApplications,
            terminals,
            batch.RootLineageAuthority,
            string.Empty,
            batch.TransitionAuthority);
        var resealed = new WoundEffectOperationBatch(
            provisional.LocalWoundRef,
            provisional.PreparedWoundId,
            provisional.SourceExport,
            provisional.RootApplications,
            provisional.TerminalOperations,
            provisional.RootLineageAuthority,
            WoundAcceptedTurnFingerprints.ComputeSourceExport(provisional),
            provisional.TransitionAuthority);

        Assert.False(InvokeRematerializationAgrees(
            input,
            preparation,
            resolution,
            resealed,
            authority));
    }

    [Theory]
    [InlineData("projection")]
    [InlineData("source_export")]
    public void PrepareReductionBatch_ChangedProjectionOrSourceExportCannotBorrowPrivateSeal(
        string mutation)
    {
        var before = Parse(CreateRankThreeWound(
            "action_control",
            "resistance_modifier"));
        using var resolutionFixture = CreateReductionPlannerFixture(before, "r1");
        var resolution = resolutionFixture.Resolution;
        var preparation = CreatePreparedReductionForTest(
            resolutionFixture,
            AssertValidProjection(before, 1));
        var input = CreateReductionPlannerInput(resolutionFixture);
        var (batch, authority) = InvokeRematerializationPrepare(
            input,
            preparation,
            resolution);
        var changedPreparation = mutation == "projection"
            ? CreatePreparedReductionForTest(
            resolutionFixture,
                new MortalWoundTreatmentSeverityReductionProjection(
                    preparation.SeverityReduction!.Before,
                    preparation.SeverityReduction.ProvisionalAfter,
                    preparation.SeverityReduction.Steps,
                    preparation.SeverityReduction.Roots,
                    preparation.SeverityReduction.Fingerprint + "_changed"))
            : preparation;
        var changedExport = mutation == "source_export"
            ? new WoundEffectSourceExport(
                batch.SourceExport.SchemaVersion,
                batch.SourceExport.Kind,
                batch.SourceExport.SourceId,
                batch.SourceExport.SourceRef,
                batch.SourceExport.State,
                batch.SourceExport.Materializable,
                batch.SourceExport.Realm,
                batch.SourceExport.Owner,
                batch.SourceExport.CausalEventRef,
                batch.SourceExport.EventSemanticFingerprint,
                batch.SourceExport.OpportunityId,
                batch.SourceExport.OpportunityAuthorityFingerprint,
                batch.SourceExport.Definitions.Reverse().ToArray())
            : batch.SourceExport;
        var changedBatch = mutation == "source_export"
            ? new WoundEffectOperationBatch(
                batch.LocalWoundRef,
                batch.PreparedWoundId,
                changedExport,
                batch.RootApplications,
                batch.TerminalOperations,
                batch.RootLineageAuthority,
                batch.SourceExportFingerprint,
                batch.TransitionAuthority)
            : batch;

        Assert.False(InvokeRematerializationAgrees(
            input,
            changedPreparation,
            resolution,
            changedBatch,
            authority));
    }

    [Fact]
    public void PrepareReductionBatch_AuthenticProjectionFingerprintCannotSealChangedRoots()
    {
        var before = Parse(CreateRankThreeWound("action_control"));
        using var resolutionFixture = CreateReductionPlannerFixture(before, "r1");
        var resolution = resolutionFixture.Resolution;
        var projection = AssertValidProjection(before, 1);
        var root = Assert.Single(projection.Roots);
        var changedProjection = new MortalWoundTreatmentSeverityReductionProjection(
            projection.Before,
            projection.ProvisionalAfter,
            projection.Steps,
            new[]
            {
                new MortalWoundTreatmentRematerializationRoot(
                    root.PriorEffectId + "_forged",
                    root.DefinitionKey,
                    root.OwnershipDomain,
                    root.Slots)
            },
            projection.Fingerprint);
        var preparation = CreatePreparedReductionForTest(
            resolutionFixture,
            changedProjection);
        var input = CreateReductionPlannerInput(resolutionFixture);

        var (batch, authority, issues) = InvokeRawRematerializationPrepare(
            input,
            preparation,
            resolution);

        Assert.Null(batch);
        Assert.Null(authority);
        Assert.Contains(issues, static issue =>
            issue.Code == "wound_plan_treatment_rematerialization_invalid");
    }

    [Theory]
    [InlineData("missing_root")]
    [InlineData("reordered_roots")]
    [InlineData("changed_lineage")]
    public void PrepareReductionBatch_ChangedRootOrLineageCannotBorrowPrivateSeal(
        string mutation)
    {
        var before = Parse(CreateRankThreeWound(
            "action_control",
            "resistance_modifier"));
        using var resolutionFixture = CreateReductionPlannerFixture(before, "r1");
        var resolution = resolutionFixture.Resolution;
        var preparation = CreatePreparedReductionForTest(
            resolutionFixture,
            AssertValidProjection(before, 1));
        var input = CreateReductionPlannerInput(resolutionFixture);
        var (batch, authority) = InvokeRematerializationPrepare(
            input,
            preparation,
            resolution);
        var roots = mutation switch
        {
            "missing_root" => batch.RootApplications.Skip(1).ToArray(),
            "reordered_roots" => batch.RootApplications.Reverse().ToArray(),
            _ => batch.RootApplications.ToArray()
        };
        var lineage = mutation == "changed_lineage"
            ? batch.RootLineageAuthority.Select((row, index) => index == 0
                ? row with { DefinitionKey = row.DefinitionKey + "_changed" }
                : row).ToArray()
            : batch.RootLineageAuthority.ToArray();
        var provisional = new WoundEffectOperationBatch(
            batch.LocalWoundRef,
            batch.PreparedWoundId,
            batch.SourceExport,
            roots,
            batch.TerminalOperations,
            lineage,
            string.Empty,
            batch.TransitionAuthority);
        var resealed = new WoundEffectOperationBatch(
            provisional.LocalWoundRef,
            provisional.PreparedWoundId,
            provisional.SourceExport,
            provisional.RootApplications,
            provisional.TerminalOperations,
            provisional.RootLineageAuthority,
            WoundAcceptedTurnFingerprints.ComputeSourceExport(provisional),
            provisional.TransitionAuthority);

        Assert.False(InvokeRematerializationAgrees(
            input,
            preparation,
            resolution,
            resealed,
            authority));
    }

    [Fact]
    public void PrepareReductionBatch_IsDeterministicDetachedAndRecomputesAuthorityDag()
    {
        var before = Parse(CreateRankThreeWound(
            "action_control",
            "resistance_modifier"));
        using var resolutionFixture = CreateReductionPlannerFixture(before, "r1");
        var resolution = resolutionFixture.Resolution;
        var preparation = CreatePreparedReductionForTest(
            resolutionFixture,
            AssertValidProjection(before, 1));
        var input = CreateReductionPlannerInput(resolutionFixture);

        var (firstBatch, firstAuthority) = InvokeRematerializationPrepare(
            input,
            preparation,
            resolution);
        firstBatch.SourceExport.Definitions[0].Definition["display"]!["name"] =
            "detached mutation";
        var (secondBatch, secondAuthority) = InvokeRematerializationPrepare(
            input,
            preparation,
            resolution);

        Assert.Equal(
            ReadAuthorityString(firstAuthority, "AuthoritySeal"),
            ReadAuthorityString(secondAuthority, "AuthoritySeal"));
        Assert.Equal(
            ReadAuthorityString(secondAuthority, "SourceExportFingerprint"),
            WoundAcceptedTurnFingerprints.ComputeSourceExport(secondBatch));
        Assert.Equal(
            ReadAuthorityString(secondAuthority, "BatchTopologyFingerprint"),
            MortalWoundTreatmentSeverityRematerializationPlanner
                .ComputeBatchTopologyFingerprint(secondBatch));
        var expectedSeed = WoundAcceptedTurnFingerprintWriter.Compute(new[]
        {
            "book_of_eternity.mortal_wound_treatment.rematerialization_seed",
            "1",
            ReadAuthorityString(secondAuthority, "PreparedInputFingerprint"),
            ReadAuthorityString(secondAuthority, "RequestFingerprint"),
            ReadAuthorityString(
                secondAuthority,
                "ResolutionAuthorityFingerprint"),
            ReadAuthorityString(secondAuthority, "ResultFingerprint"),
            ReadAuthorityString(secondAuthority, "AttemptId"),
            ReadAuthorityString(secondAuthority, "OperationKey"),
            ReadAuthorityString(secondAuthority, "TransitionId"),
            ReadAuthorityString(secondAuthority, "ExpectedBeforeFingerprint"),
            ReadAuthorityString(secondAuthority, "ProjectionFingerprint")
        });
        Assert.Equal(
            expectedSeed,
            ReadAuthorityString(secondAuthority, "AuthoritySeedFingerprint"));
        var expectedSeal = WoundAcceptedTurnFingerprintWriter.Compute(new[]
        {
            "book_of_eternity.mortal_wound_treatment.rematerialization_authority",
            "1",
            ReadAuthorityString(secondAuthority, "AuthoritySeedFingerprint"),
            ReadAuthorityString(secondAuthority, "BatchTopologyFingerprint")
        });
        Assert.Equal(
            expectedSeal,
            ReadAuthorityString(secondAuthority, "AuthoritySeal"));
        Assert.Equal(
            secondBatch.RootApplications.Count,
            secondBatch.RootApplications.Select(static value =>
                    MortalLocationIdentityState.BuildConfusableKey(
                        value.ApplicationRef))
                .Distinct(StringComparer.Ordinal)
                .Count());
    }

    [Theory]
    [InlineData("projection_steps")]
    [InlineData("projection_fingerprint")]
    [InlineData("projection_after")]
    [InlineData("non_reduction_projection")]
    public void Prepare_InconsistentProjectionSealCannotFinalize(string mutation)
    {
        var before = Parse(CreateRankThreeWound("action_control"));
        using var resolutionFixture = CreateReductionPlannerFixture(before,
            mutation == "non_reduction_projection" ? "n" : "r1",
            mutation == "non_reduction_projection" ? "None" : "AppendOnce");
        var resolution = resolutionFixture.Resolution;
        var valid = AssertValidProjection(
            before,
            mutation == "projection_steps" ? 2 : 1);
        var projection = mutation switch
        {
            "projection_fingerprint" =>
                new MortalWoundTreatmentSeverityReductionProjection(
                    valid.Before,
                    valid.ProvisionalAfter,
                    valid.Steps,
                    valid.Roots,
                    valid.Fingerprint + "_changed"),
            "projection_after" =>
                new MortalWoundTreatmentSeverityReductionProjection(
                    valid.Before,
                    before,
                    valid.Steps,
                    valid.Roots,
                    valid.Fingerprint),
            _ => valid
        };
        var preparation = CreatePreparedReductionForTest(
            resolutionFixture, projection);

        var result = MortalWoundTreatmentOutcomePublicationPlanner.Finalize(
            preparation,
            resolution,
            null,
            new Dictionary<string, EffectAcceptedApplicationResult>(
                StringComparer.Ordinal));

        Assert.False(result.IsValid);
        Assert.Null(result.After);
        Assert.Null(result.DeclaredOutcome);
        Assert.Contains(result.Issues, static issue =>
            issue.Code == "mortal_wound_treatment_outcome_preparation_mismatch");
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

    private static (WoundEffectOperationBatch Batch, object Authority)
        InvokeRematerializationPrepare(
            WoundAcceptedTurnInput input,
            MortalWoundTreatmentOutcomePreparation preparation,
            MortalWoundTreatmentResolution resolution)
    {
        var carrierIssues = EffectCarrierCatalog.Build(
            input.PreTurnEffectCarriers!).Issues;
        Assert.True(carrierIssues.Count == 0, Describe(carrierIssues));
        using var identityDocument = JsonDocument.Parse(
            input.PreTurnEffectIdentityIndex!.ToJsonString());
        var identityParse = EffectIdentityState.Parse(
            identityDocument.RootElement,
            EffectIdentityState.StatePath);
        Assert.True(identityParse.Issues.Count == 0,
            Describe(identityParse.Issues));
        var (rawBatch, authority, issues) = InvokeRawRematerializationPrepare(
            input,
            preparation,
            resolution);
        Assert.True(issues.Count == 0, Describe(issues));
        var batch = Assert.IsType<WoundEffectOperationBatch>(rawBatch);
        Assert.NotNull(authority);
        return (batch, authority!);
    }

    private static IReadOnlyList<EffectAcceptedApplicationResult>
        CreateAcceptedApplications(
            WoundEffectOperationBatch batch,
            params string[] effectIds)
    {
        Assert.Equal(batch.RootApplications.Count, effectIds.Length);
        var slotByApplicationRef = new Dictionary<
            string,
            IReadOnlyList<WoundEffectSlotAgreement>>(StringComparer.Ordinal);
        var nextSlot = 1;
        foreach (var pair in batch.RootApplications
                     .Zip(effectIds)
                     .OrderBy(static pair => pair.Second, StringComparer.Ordinal))
        {
            slotByApplicationRef.Add(
                pair.First.ApplicationRef,
                pair.First.SlotBindings.Select(slot =>
                    new WoundEffectSlotAgreement(
                        nextSlot++,
                        slot.ProfileKey,
                        slot.ReadableSummary)).ToArray());
        }

        return batch.RootApplications.Select((application, index) =>
            new EffectAcceptedApplicationResult(
                application.ApplicationRef,
                "created_new_identity",
                effectIds[index],
                $"effect_transition_final_{index + 1}",
                WoundEffectOperationEventRef.Create(
                    application.CausalEventRef,
                    application.MechanicsOrdinal,
                    application.OperationOrdinal,
                    application.OperationKind),
                application.CausalEventRef,
                application.ExpectedSourceKey,
                application.ExpectedTargetKey,
                application.ExpectedCarrierCoordinate,
                new WoundEffectMaterializationAgreement(
                    slotByApplicationRef[application.ApplicationRef],
                    application.ExpectedComponentCount,
                    application.ExpectedMaterializationFingerprint)))
            .ToArray();
    }

    private static IReadOnlyDictionary<string, EffectAcceptedApplicationResult>
        CreateApplicationMap(
            IEnumerable<EffectAcceptedApplicationResult> applications)
    {
        var result = new Dictionary<string, EffectAcceptedApplicationResult>(
            StringComparer.Ordinal);
        foreach (var application in applications)
            result.Add(application.ApplicationRef, application);
        return result;
    }

    private static (object? Batch, object? Authority,
        IReadOnlyList<ValidationIssue> Issues) InvokeRawRematerializationPrepare(
            WoundAcceptedTurnInput input,
            MortalWoundTreatmentOutcomePreparation preparation,
            MortalWoundTreatmentResolution resolution)
    {
        var type = typeof(WoundAcceptedTurnPlanner).Assembly.GetType(
            "BookOfEternityClient.Services.MortalWoundTreatmentSeverityRematerializationPlanner");
        Assert.True(type is not null,
            "The private treatment rematerialization planner must exist.");
        var method = type!.GetMethod(
            "Prepare",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.True(method is not null,
            "The private treatment rematerialization planner must expose Prepare.");
        var result = method!.Invoke(null, new object?[]
        {
            input,
            preparation,
            resolution.RequestFingerprint,
            resolution.ResolutionAuthorityFingerprint,
            resolution.ResultFingerprint,
            resolution.Coordinates.AttemptId,
            resolution.Coordinates.OperationKey,
            WoundIdentityState.ComputeSemanticFingerprint(preparation.Before)
        });
        Assert.NotNull(result);
        var resultType = result!.GetType();
        var issues = Assert.IsAssignableFrom<IReadOnlyList<ValidationIssue>>(
            resultType.GetProperty("Issues", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(result));
        var batch = resultType.GetProperty(
                "Batch",
                BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(result);
        var authority = resultType.GetProperty(
                "Authority",
                BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(result);
        return (batch, authority, issues);
    }

    private static bool InvokeRematerializationAgrees(
        WoundAcceptedTurnInput input,
        MortalWoundTreatmentOutcomePreparation preparation,
        MortalWoundTreatmentResolution resolution,
        WoundEffectOperationBatch batch,
        object authority)
    {
        var type = authority.GetType().Assembly.GetType(
            "BookOfEternityClient.Services.MortalWoundTreatmentSeverityRematerializationPlanner");
        Assert.NotNull(type);
        var method = type!.GetMethod(
            "Agrees",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return Assert.IsType<bool>(method!.Invoke(null, new object?[]
        {
            input,
            preparation,
            resolution.RequestFingerprint,
            resolution.ResolutionAuthorityFingerprint,
            resolution.ResultFingerprint,
            resolution.Coordinates.AttemptId,
            resolution.Coordinates.OperationKey,
            WoundIdentityState.ComputeSemanticFingerprint(preparation.Before),
            batch,
            authority
        }));
    }

    private static void AssertPrivateAuthoritySeals(object authority)
    {
        var type = authority.GetType();
        foreach (var propertyName in new[]
                 {
                     "AuthoritySeedFingerprint",
                     "SourceExportFingerprint",
                     "BatchTopologyFingerprint",
                     "AuthoritySeal"
                 })
        {
            var value = Assert.IsType<string>(type.GetProperty(propertyName)!
                .GetValue(authority));
            Assert.StartsWith("sha256:", value);
        }
    }

    private static string ReadAuthorityString(
        object authority,
        string propertyName) => Assert.IsType<string>(authority.GetType()
        .GetProperty(propertyName)!
        .GetValue(authority));

    private static string? ReadPriorRootEffectId(
        WoundRootEffectApplication application)
    {
        var property = typeof(WoundRootEffectApplication).GetProperty(
            "PriorRootEffectId",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(property);
        return Assert.IsType<string>(property!.GetValue(application));
    }
}
