using System.Reflection;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AcceptedMechanicsPlanCacheTests
{
    private const string WoundCommonCommandPath =
        "game_state/wounds/wound_commands.json";

    [Fact]
    public void WoundCommonStageBundle_DetachesFullGraphAgainstReturnedMutation()
    {
        var stages = CreateWoundCommonStages("detached", "detached");

        Assert.NotSame(stages.Input, stages.Bundle.Input);
        Assert.NotSame(stages.Prepared, stages.Bundle.PreparedPlan);
        Assert.NotSame(stages.Effect, stages.Bundle.EffectBatchPlan);
        Assert.NotSame(stages.Final, stages.Bundle.FinalPlan);

        var returnedInput = stages.Bundle.Input;
        returnedInput.PreTurnIdentityIndex["forged"] = true;
        var returnedPrepared = stages.Bundle.PreparedPlan;
        returnedPrepared.BaselineAuthority.PreTurnIdentityIndex["forged"] = true;
        var returnedEffect = stages.Bundle.EffectBatchPlan;
        returnedEffect.EffectInput.RawCommands["forged"] = true;
        var returnedResults = returnedEffect.ApplicationResults.ToArray();
        returnedResults[0] = returnedResults[0] with { EffectId = "effect_forged" };
        var returnedFinal = stages.Bundle.FinalPlan;
        returnedFinal.HistoryAfterImage["forged"] = true;

        Assert.False(stages.Bundle.Input.PreTurnIdentityIndex.ContainsKey("forged"));
        Assert.False(stages.Bundle.PreparedPlan.BaselineAuthority
            .PreTurnIdentityIndex.ContainsKey("forged"));
        Assert.False(stages.Bundle.EffectBatchPlan.EffectInput.RawCommands
            .ContainsKey("forged"));
        Assert.DoesNotContain(
            stages.Bundle.EffectBatchPlan.ApplicationResults,
            static value => value.EffectId == "effect_forged");
        Assert.False(stages.Bundle.FinalPlan.HistoryAfterImage
            .ContainsKey("forged"));
    }

    [Fact]
    public void WoundCommonStageBundle_RejectsEveryCrossSwappedStage()
    {
        var first = CreateWoundCommonStages("first", "first");
        var second = CreateWoundCommonStages("second", "second");

        Assert.ThrowsAny<ArgumentException>(() =>
            new AcceptedMechanicsWoundStageBundle(
                first.Input,
                second.Prepared,
                second.Effect,
                second.Final));
        Assert.ThrowsAny<ArgumentException>(() =>
            new AcceptedMechanicsWoundStageBundle(
                first.Input,
                first.Prepared,
                second.Effect,
                second.Final));
        Assert.ThrowsAny<ArgumentException>(() =>
            new AcceptedMechanicsWoundStageBundle(
                first.Input,
                first.Prepared,
                first.Effect,
                second.Final));
    }

    [Fact]
    public void WoundCommonInputFingerprint_DistinguishesAbsentAndEmptyAuthorities()
    {
        var absent = AcceptedMechanicsPlanFingerprints.ComputeInput(
            CreateWoundCommonBinding(null, null));
        var emptyCommands = AcceptedMechanicsPlanFingerprints.ComputeInput(
            CreateWoundCommonBinding(new JsonObject(), null));
        var emptyInput = AcceptedMechanicsPlanFingerprints.ComputeInput(
            CreateWoundCommonBinding(null, WoundInput()));
        var bothPresent = AcceptedMechanicsPlanFingerprints.ComputeInput(
            CreateWoundCommonBinding(new JsonObject(), WoundInput()));

        Assert.Equal(
            4,
            new[] { absent, emptyCommands, emptyInput, bothPresent }
                .Distinct(StringComparer.Ordinal)
                .Count());
    }

    [Fact]
    public void WoundCommonInputFingerprint_DistinguishesChangedTypedInput()
    {
        var common = Input();
        var first = CreateWoundCommonInput(common, "first");
        var secondHistory = first.PreTurnHistory;
        secondHistory["nextOrdinal"] = 2;
        var second = first with { PreTurnHistory = secondHistory };

        Assert.NotEqual(
            AcceptedMechanicsPlanFingerprints.ComputeInput(
                CreateWoundCommonBinding(null, first)),
            AcceptedMechanicsPlanFingerprints.ComputeInput(
                CreateWoundCommonBinding(null, second)));
    }

    [Fact]
    public void WoundCommonPreparedFingerprint_IsComputedOnlyFromDetachedPlan()
    {
        var stages = CreateWoundCommonStages("computed", "computed");
        var plan = CreateWoundCommonPlan(stages.Bundle, afterImageMarker: 1);

        Assert.DoesNotContain(
            typeof(AcceptedMechanicsPlan)
                .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
                .SelectMany(static constructor => constructor.GetParameters()),
            static parameter => string.Equals(
                parameter.Name,
                "preparedPlanFingerprint",
                StringComparison.OrdinalIgnoreCase));
        Assert.True(ResourceMaterializationContract.IsAuthorityFingerprint(
            plan.PreparedPlanFingerprint));
        Assert.Equal(
            AcceptedMechanicsPlanFingerprints.ComputePrepared(plan),
            plan.PreparedPlanFingerprint);
    }

    [Fact]
    public void WoundCommonPlan_PublishesExactFinalTypedCarrierContribution()
    {
        var stages = CreateWoundCommonStages(
            "validated_publication",
            "validated_publication");

        var plan = CreateWoundCommonPlan(stages.Bundle, afterImageMarker: 1);

        var root = plan.WoundCarrierAfterImages[
            WoundCarrierCatalog.PlayerPath];
        var published = Assert.Single(root["activeWounds"]!.AsArray());
        var expected = Assert.IsType<WoundMaterializationEnvelope>(
            Assert.Single(Assert.Single(stages.Final.CarrierContributions)
                .Mutations).AfterWound);
        Assert.Equal(
            expected.WoundId,
            published!["woundId"]!.GetValue<string>());
    }

    [Fact]
    public void WoundPublication_HasNoAssemblyVisibleRawConstructor()
    {
        var constructors = typeof(AcceptedMechanicsWoundPublication)
            .GetConstructors(
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic);

        Assert.All(constructors, static constructor =>
            Assert.True(constructor.IsPrivate));
    }

    [Fact]
    public void WoundCommonPreparedFingerprint_CoversResultMapsAndAfterImages()
    {
        var input = CreateWoundCommonInput("result-map");
        var prepared = AssertPrepared(WoundAcceptedTurnPlanner.Prepare(input));
        var firstStages = CreateWoundCommonStages(
            input,
            prepared,
            "result_first");
        var secondStages = CreateWoundCommonStages(
            input,
            prepared,
            "result_second");
        Assert.NotEqual(
            Assert.Single(firstStages.Effect.ApplicationResults).EffectId,
            Assert.Single(secondStages.Effect.ApplicationResults).EffectId);

        var first = CreateWoundCommonPlan(firstStages.Bundle, afterImageMarker: 1);
        var changedResultMap = CreateWoundCommonPlan(
            secondStages.Bundle,
            afterImageMarker: 1);
        var changedAfterImage = CreateWoundCommonPlan(
            firstStages.Bundle,
            afterImageMarker: 2);

        Assert.NotEqual(
            first.PreparedPlanFingerprint,
            changedResultMap.PreparedPlanFingerprint);
        Assert.NotEqual(
            first.PreparedPlanFingerprint,
            changedAfterImage.PreparedPlanFingerprint);

        var returnedCarriers = first.WoundCarrierAfterImages;
        returnedCarriers[WoundCarrierCatalog.PlayerPath]["forged"] = true;
        Assert.False(first.WoundCarrierAfterImages[WoundCarrierCatalog.PlayerPath]
            .ContainsKey("forged"));
    }

    [Theory]
    [InlineData("sources")]
    [InlineData("targets")]
    [InlineData("source_bindings")]
    [InlineData("deferred_reactions")]
    [InlineData("reaction_usage")]
    public void WoundCommonPreparedFingerprint_CoversCompleteTopLevelEffectPlan(
        string mutation)
    {
        var stages = CreateWoundCommonStages(
            "effect_payload",
            "effect_payload");
        var effectPlan = CompleteEffectPlanForAssembly(
            stages.Effect.EffectPlan);
        var changedEffectPlan = MutateTopLevelEffectPlan(
            effectPlan,
            mutation);

        var first = CreateWoundCommonPlan(
            stages.Bundle,
            afterImageMarker: 1,
            commonEffectPlan: effectPlan);
        var changed = CreateWoundCommonPlan(
            stages.Bundle,
            afterImageMarker: 1,
            commonEffectPlan: changedEffectPlan);

        Assert.NotEqual(
            first.PreparedPlanFingerprint,
            changed.PreparedPlanFingerprint);
    }

    [Fact]
    public void WoundCommonCache_ChangedOpaqueResultsInPlanningContextBuildFresh()
    {
        var commonInput = Input();
        var woundInput = CreateWoundCommonInput(
            commonInput,
            "planning_context");
        var prepared = AssertPrepared(WoundAcceptedTurnPlanner.Prepare(woundInput));
        var firstStages = CreateWoundCommonStages(
            woundInput,
            prepared,
            "planning_first");
        var secondStages = CreateWoundCommonStages(
            woundInput,
            prepared,
            "planning_second");
        var firstInput = CreateWoundCommonAcceptedInput(firstStages.Bundle);
        var secondInput = CreateWoundCommonAcceptedInput(secondStages.Bundle);
        var planner = new WoundCommonPlanner();
        var cache = new AcceptedMechanicsPlanCache(planner.Build);

        var first = cache.GetOrBuildValidated(firstInput);
        var second = cache.GetOrBuildValidated(secondInput);

        Assert.Equal(
            AcceptedMechanicsPlanFingerprints.ComputeInput(
                firstInput.CreateBinding()),
            AcceptedMechanicsPlanFingerprints.ComputeInput(
                secondInput.CreateBinding()));
        Assert.NotEqual(
            Assert.Single(firstStages.Bundle.ApplicationResults).Value.EffectId,
            Assert.Single(secondStages.Bundle.ApplicationResults).Value.EffectId);
        Assert.True(
            first.Success,
            string.Join(Environment.NewLine, first.Issues.Select(static issue =>
                $"{issue.Code}: {issue.Message}")));
        Assert.True(
            second.Success,
            string.Join(Environment.NewLine, second.Issues.Select(static issue =>
                $"{issue.Code}: {issue.Message}")));
        Assert.NotSame(first.Plan, second.Plan);
        Assert.NotEqual(
            first.Plan!.InputFingerprint,
            second.Plan!.InputFingerprint);
        Assert.Equal(2, planner.Calls);
    }

    [Fact]
    public void WoundCommonCache_RejectsWoundBindingFromDifferentCommonRequest()
    {
        var stages = CreateWoundCommonStages(
            "foreign_common_request",
            "foreign_common_request");
        var input = CreateWoundCommonAcceptedInput(
            stages.Bundle,
            alignCommonBinding: false);
        var planner = new WoundCommonPlanner();
        var cache = new AcceptedMechanicsPlanCache(planner.Build);

        var result = cache.GetOrBuildValidated(input);

        Assert.False(result.Success);
        Assert.Null(result.Plan);
        Assert.Contains(
            result.Issues,
            static issue => issue.Code ==
                "accepted_mechanics_wound_stage_mismatch");
        Assert.False(cache.HasValidated);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void WoundCommonCache_RejectsIncompleteWoundCommandPath(
        bool omitTouched,
        bool omitConsumed)
    {
        var commonInput = Input();
        var woundInput = CreateWoundCommonInput(
            commonInput,
            "command_path");
        var stages = CreateWoundCommonStages(
            woundInput,
            AssertPrepared(WoundAcceptedTurnPlanner.Prepare(woundInput)),
            "command_path");
        var input = CreateWoundCommonAcceptedInput(stages.Bundle);
        var cache = new AcceptedMechanicsPlanCache((accepted, fingerprint) =>
            new AcceptedMechanicsPlanningResult(
                CreateWoundCommonPlan(
                    stages.Bundle,
                    1,
                    omitWoundCommandTouched: omitTouched,
                    omitWoundCommandConsumed: omitConsumed,
                    acceptedInput: accepted,
                    acceptedInputFingerprint: fingerprint),
                Array.Empty<ValidationIssue>()));

        var result = cache.GetOrBuildValidated(input);

        Assert.False(result.Success);
        Assert.Contains(
            result.Issues,
            static issue => issue.Code ==
                "accepted_mechanics_wound_command_path_mismatch");
        Assert.False(cache.HasValidated);
    }

    [Fact]
    public void WoundCommonCache_RejectsCorruptedPreparedPayloadOnInitialBuild()
    {
        var planner = new CountingPlanner();
        var cache = new AcceptedMechanicsPlanCache((input, fingerprint) =>
        {
            var result = planner.Build(input, fingerprint);
            CorruptWoundCommonPreparedPayload(result.Plan!);
            return result;
        });

        var result = cache.GetOrBuildValidated(Input());

        Assert.False(result.Success);
        Assert.Null(result.Plan);
        Assert.Contains(
            result.Issues,
            static issue => issue.Code ==
                "accepted_mechanics_prepared_plan_fingerprint_mismatch");
        Assert.False(cache.HasValidated);
        Assert.Equal(1, planner.Calls);
    }

    [Theory]
    [InlineData("hit")]
    [InlineData("peek")]
    [InlineData("take")]
    public void WoundCommonCache_RevalidatesPreparedPayloadAtEveryCachedBoundary(
        string boundary)
    {
        var planner = new CountingPlanner();
        var cache = new AcceptedMechanicsPlanCache(planner.Build);
        var input = Input();
        var first = cache.GetOrBuildValidated(input);
        Assert.True(first.Success);
        CorruptWoundCommonPreparedPayload(first.Plan!);

        switch (boundary)
        {
            case "hit":
            {
                var result = cache.GetOrBuildValidated(input);
                Assert.False(result.Success);
                Assert.Contains(
                    result.Issues,
                    static issue => issue.Code ==
                        "accepted_mechanics_prepared_plan_fingerprint_mismatch");
                break;
            }
            case "peek":
                Assert.False(cache.TryPeekValidated(out _, out _));
                break;
            case "take":
                Assert.False(cache.TryTakeValidated(input.CreateBinding(), out _));
                break;
            default:
                throw new InvalidOperationException("Unknown cache boundary.");
        }

        Assert.False(cache.HasValidated);
        Assert.Equal(1, planner.Calls);
    }

    [Theory]
    [InlineData("hit")]
    [InlineData("peek")]
    [InlineData("take")]
    public void WoundCommonCache_CorruptedNestedBundleFailsClosed(
        string boundary)
    {
        var commonInput = Input();
        var woundInput = CreateWoundCommonInput(
            commonInput,
            "nested_corruption");
        var stages = CreateWoundCommonStages(
            woundInput,
            AssertPrepared(WoundAcceptedTurnPlanner.Prepare(woundInput)),
            "nested_corruption");
        var input = CreateWoundCommonAcceptedInput(stages.Bundle);
        var planner = new WoundCommonPlanner();
        var cache = new AcceptedMechanicsPlanCache(planner.Build);
        var first = cache.GetOrBuildValidated(input);
        Assert.True(first.Success);
        CorruptWoundCommonNestedBundle(first.Plan!);

        switch (boundary)
        {
            case "hit":
            {
                var result = cache.GetOrBuildValidated(input);
                Assert.False(result.Success);
                Assert.Contains(
                    result.Issues,
                    static issue => issue.Code ==
                        "accepted_mechanics_wound_stage_mismatch");
                break;
            }
            case "peek":
                Assert.False(cache.TryPeekValidated(out _, out _));
                break;
            case "take":
                Assert.False(cache.TryTakeValidated(
                    input.CreateBinding(),
                    out _));
                break;
            default:
                throw new InvalidOperationException("Unknown cache boundary.");
        }

        Assert.False(cache.HasValidated);
    }

    [Theory]
    [InlineData("game_state/wounds/wound_commands.json")]
    [InlineData("game_state/player/wounds.json")]
    [InlineData("game_state/npcs/npc_wounds.json")]
    [InlineData("game_state/combat/enemies.json")]
    [InlineData("game_state/combat/allies.json")]
    [InlineData("game_state/meta/afterlife_entity_profiles.json")]
    [InlineData("game_state/wounds/wound_identity_index.json")]
    [InlineData("game_state/wounds/wound_history.json")]
    [InlineData("game_state/control/pending_wound_resolutions.json")]
    [InlineData("game_state/control/progression_schedule.json")]
    [InlineData("game_state/control/progression_report.json")]
    [InlineData("output/narrative_response.json")]
    [InlineData("output/interface_updates.json")]
    [InlineData("output/debug_logs.json")]
    public void WoundCommonPlan_RejectsMissingWoundBeforeImage(string path)
    {
        var stages = CreateWoundCommonStages("missing_before", "missing_before");

        var exception = Assert.Throws<ArgumentException>(() =>
            CreateWoundCommonPlan(
                stages.Bundle,
                afterImageMarker: 1,
                omittedBeforeImagePath: path));

        Assert.Contains(path, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void WoundCommonPlan_RejectsCompetingEffectAndWoundWholeRootProducer()
    {
        var stages = CreateWoundCommonStages("producer", "producer");

        var exception = Assert.Throws<ArgumentException>(() =>
            CreateWoundCommonPlan(
                stages.Bundle,
                afterImageMarker: 1,
                woundCarrierPath: "game_state/effects/effects.json"));

        Assert.Contains("competing", exception.Message, StringComparison.Ordinal);
        Assert.Contains("effect", exception.Message, StringComparison.Ordinal);
        Assert.Contains("wound", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("game_state/resources/resource_definitions.json")]
    [InlineData("game_state/resources/resource_state.json")]
    [InlineData("game_state/resources/resource_history.json")]
    [InlineData("game_state/resources/resource_owner_authority.json")]
    [InlineData("game_state/effects/effect_identity_index.json")]
    [InlineData("game_state/wounds/wound_identity_index.json")]
    [InlineData("game_state/wounds/wound_history.json")]
    [InlineData("game_state/wounds/wound_commands.json")]
    public void WoundCommonPlan_RejectsReservedWriteOrDeleteProducerCollision(
        string path)
    {
        var stages = CreateWoundCommonStages("reserved", "reserved");

        var exception = Assert.Throws<ArgumentException>(() =>
            CreateWoundCommonPlan(
                stages.Bundle,
                afterImageMarker: 1,
                woundCarrierPath: path));

        Assert.Contains("competing", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WoundCommonPlan_RejectsPendingWriteOrDeleteProducerCollision(
        bool delete)
    {
        const string path = "game_state/control/pending_wound_collision.json";
        var stages = CreateWoundCommonStages("pending", "pending");
        var pending = new Dictionary<string, JsonObject?>(StringComparer.Ordinal)
        {
            [path] = delete ? null : Object("schemaVersion", 1)
        };

        var exception = Assert.Throws<ArgumentException>(() =>
            CreateWoundCommonPlan(
                stages.Bundle,
                afterImageMarker: 1,
                woundCarrierPath: path,
                pendingAfterImages: pending));

        Assert.Contains("competing", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void WoundCommonPlan_AllowsOrderedOwnerTransitionGroupOnOneRoot()
    {
        var stages = CreateWoundCommonStages("owner_group", "owner_group");
        var transitions = new[]
        {
            AcceptedMechanicsOwnerTransition.CreateMortalNpcCreation(
                "npc_ref_first",
                "npc_first",
                Object("schemaVersion", 1)),
            AcceptedMechanicsOwnerTransition.CreateMortalNpcCreation(
                "npc_ref_second",
                "npc_second",
                Object("schemaVersion", 1))
        };

        var plan = CreateWoundCommonPlan(
            stages.Bundle,
            afterImageMarker: 1,
            ownerTransitions: transitions);

        Assert.Equal(2, plan.OwnerTransitions.Count);
        Assert.Single(plan.OwnerTransitions.Select(static value => value.Path)
            .Distinct(StringComparer.Ordinal));
    }

    [Fact]
    public void AcceptedTurnState_CommonValidationRejectsForeignWoundBundle()
    {
        var commonPlanner = new WoundCommonPlanner();
        var current = AcceptedTurnStateHarness.Create(
            commonPlan: new AcceptedMechanicsPlanCache(commonPlanner.Build));
        var currentStages = BuildRegistryOwnedWoundStages(current);
        var currentInput = CreateWoundCommonAcceptedInput(currentStages);

        var accepted = current.GetOrBuildCommonValidated(currentInput);

        Assert.True(
            accepted.Success,
            string.Join(Environment.NewLine, accepted.Issues.Select(static issue =>
                $"{issue.Code}: {issue.Message}")));

        var foreign = AcceptedTurnStateHarness.Create();
        var foreignStages = BuildRegistryOwnedWoundStages(foreign);
        var foreignInput = CreateWoundCommonAcceptedInput(foreignStages);

        var rejected = current.GetOrBuildCommonValidated(foreignInput);

        Assert.False(rejected.Success);
        Assert.Null(rejected.Plan);
        Assert.Contains(
            rejected.Issues,
            static issue => issue.Code ==
                "accepted_mechanics_wound_stage_provenance_mismatch");
        Assert.False(current.TryPeekCommonValidated(out _, out _));
        Assert.False(current.TryPeekWoundPrepared(out _));
        Assert.False(current.TryPeekWoundFinal(out _));
        Assert.False(current.TryPeekEffectValidated(out _));
    }

    [Fact]
    public void AcceptedTurnState_CommonValidationRejectsOmittedCurrentWoundBundle()
    {
        var commonPlanner = new CountingPlanner();
        var state = AcceptedTurnStateHarness.Create(
            commonPlan: new AcceptedMechanicsPlanCache(commonPlanner.Build));
        var stages = BuildRegistryOwnedWoundStages(state);
        var complete = CreateWoundCommonAcceptedInput(stages);
        var baselineContext = Assert.IsType<AcceptedMechanicsPlanningContext>(
            ValidCommonInput().PlanningContext);
        var omitted = new AcceptedMechanicsInput(
            complete.SessionId,
            complete.RequestId,
            complete.SnapshotToken,
            complete.Realm,
            complete.Turn,
            complete.AcceptedEvents,
            complete.ResourceCommands,
            complete.EffectCommands,
            complete.PendingInput,
            complete.InternalInputs,
            complete.AuthorityFingerprints,
            complete.BeforeImages,
            complete.ValidationIssues,
            baselineContext);

        var rejected = state.GetOrBuildCommonValidated(omitted);

        Assert.False(rejected.Success);
        Assert.Null(rejected.Plan);
        Assert.Contains(
            rejected.Issues,
            static issue => issue.Code ==
                "accepted_mechanics_wound_stage_provenance_mismatch");
        Assert.Equal(0, commonPlanner.Calls);
        Assert.False(state.TryPeekCommonValidated(out _, out _));
        Assert.False(state.TryPeekWoundPrepared(out _));
        Assert.False(state.TryPeekWoundFinal(out _));
        Assert.False(state.TryPeekEffectValidated(out _));
    }

    private static WoundCommonStageFixture CreateWoundCommonStages(
        string requestSuffix,
        string effectIdentityPrefix)
    {
        var input = CreateWoundCommonInput(requestSuffix);
        var prepared = AssertPrepared(WoundAcceptedTurnPlanner.Prepare(input));
        return CreateWoundCommonStages(input, prepared, effectIdentityPrefix);
    }

    private static WoundCommonStageFixture CreateWoundCommonStages(
        WoundAcceptedTurnInput input,
        WoundPreparedAcceptedTurnPlan prepared,
        string effectIdentityPrefix)
    {
        var effectInput = WoundEffectBatchPlannerTests
            .CreateEffectInputForAcceptedCache(prepared);
        var effectResult = WoundEffectBatchPlanner.Build(
            prepared,
            effectInput,
            new WoundCommonEffectIdentityFactory(effectIdentityPrefix));
        var effect = AssertWoundCommonEffect(effectResult);
        var final = AssertWoundCommonFinal(
            WoundAcceptedTurnPlanner.Finalize(prepared, effectResult));
        return new WoundCommonStageFixture(
            input,
            prepared,
            effect,
            final,
            new AcceptedMechanicsWoundStageBundle(
                input,
                prepared,
                effect,
                final));
    }

    private static AcceptedMechanicsWoundStageBundle BuildRegistryOwnedWoundStages(
        AcceptedTurnStateHarness state)
    {
        var woundInput = WoundInput();
        var prepared = AssertPrepared(
            state.GetOrBuildWoundPrepared(woundInput));
        var effectInput = BuildEmptyEffectStage(prepared).Plan!.EffectInput;
        var effect = state.GetOrBuildWoundEffectValidated(
            prepared,
            effectInput);
        Assert.True(
            effect.Success,
            string.Join(Environment.NewLine, effect.Issues.Select(static issue =>
                $"{issue.Code}: {issue.Message}")));
        var final = state.GetOrBuildWoundFinal(prepared, effect);
        Assert.True(
            final.Success,
            string.Join(Environment.NewLine, final.Issues.Select(static issue =>
                $"{issue.Code}: {issue.Message}")));
        return new AcceptedMechanicsWoundStageBundle(
            woundInput,
            prepared,
            effect.Plan!,
            final.Plan!);
    }

    private static WoundAcceptedTurnInput CreateWoundCommonInput(
        string requestSuffix)
    {
        var input = WoundEffectBatchPlannerTests.CreateInputForAcceptedCache();
        var requestId = $"request_wound_common_{requestSuffix}";
        return input with
        {
            Binding = input.Binding with
            {
                RequestId = requestId
            },
            Opportunities = input.Opportunities.Select(value =>
                ResealWoundOpportunity(value with
                {
                    RequestId = requestId
                })).ToArray()
        };
    }

    private static WoundAcceptedTurnInput CreateWoundCommonInput(
        AcceptedMechanicsInput commonInput,
        string requestSuffix)
    {
        var input = CreateWoundCommonInput(requestSuffix);
        return input with
        {
            Binding = input.Binding with
            {
                SessionId = commonInput.SessionId,
                RequestId = commonInput.RequestId,
                SnapshotToken = commonInput.SnapshotToken,
                Realm = commonInput.Realm,
                Turn = commonInput.Turn
            },
            Opportunities = input.Opportunities.Select(value =>
                ResealWoundOpportunity(value with
                {
                    SessionId = commonInput.SessionId,
                    RequestId = commonInput.RequestId,
                    SnapshotToken = commonInput.SnapshotToken
                })).ToArray()
        };
    }

    private static WoundOpportunityAuthority ResealWoundOpportunity(
        WoundOpportunityAuthority value) => value with
    {
        AuthorityFingerprint =
            WoundOpportunityAuthority.RecomputeAuthorityFingerprint(value)
    };

    private static AcceptedMechanicsPlanBinding CreateWoundCommonBinding(
        JsonObject? woundCommands,
        WoundAcceptedTurnInput? woundInput)
    {
        var input = Input();
        return new AcceptedMechanicsPlanBinding(
            input.SessionId,
            input.RequestId,
            input.SnapshotToken,
            input.Realm,
            input.Turn,
            input.AcceptedEvents,
            input.ResourceCommands,
            input.EffectCommands,
            input.PendingInput,
            input.InternalInputs,
            input.AuthorityFingerprints,
            input.BeforeImages,
            woundCommands: woundCommands,
            woundInput: woundInput);
    }

    private static AcceptedMechanicsPlan CreateWoundCommonPlan(
        AcceptedMechanicsWoundStageBundle woundStages,
        int afterImageMarker,
        string? omittedBeforeImagePath = null,
        string? woundCarrierPath = null,
        bool omitWoundCommandTouched = false,
        bool omitWoundCommandConsumed = false,
        IReadOnlyDictionary<string, JsonObject?>? pendingAfterImages = null,
        IReadOnlyList<AcceptedMechanicsOwnerTransition>? ownerTransitions = null,
        EffectAcceptedTurnPlan? commonEffectPlan = null,
        AcceptedMechanicsInput? acceptedInput = null,
        string? acceptedInputFingerprint = null)
    {
        var input = acceptedInput ?? CreateWoundCommonAcceptedInput(
            woundStages,
            omittedBeforeImagePath,
            woundCarrierPath,
            pendingAfterImages,
            ownerTransitions);
        var binding = new AcceptedMechanicsPlanBinding(
            input.SessionId,
            input.RequestId,
            input.SnapshotToken,
            input.Realm,
            input.Turn,
            input.AcceptedEvents,
            input.ResourceCommands,
            input.EffectCommands,
            input.PendingInput,
            input.InternalInputs,
            input.AuthorityFingerprints,
            input.BeforeImages,
            woundCommands: Object("schemaVersion", 1),
            woundInput: woundStages.Input);
        var inputFingerprint = acceptedInputFingerprint ??
            AcceptedMechanicsPlanFingerprints.ComputeInput(binding);
        var basePlan = Assert.IsType<AcceptedMechanicsPlan>(
            new CountingPlanner().Build(input, inputFingerprint).Plan);
        var stateAfterImage = basePlan.StateAfterImage;
        stateAfterImage["woundCommonMarker"] = afterImageMarker;
        var effectiveEffectPlan = commonEffectPlan ??
            CompleteEffectPlanForAssembly(
                woundStages.EffectBatchPlan.EffectPlan);
        var composition = AcceptedMechanicsCarrierAssembler.Compose(
            effectiveEffectPlan,
            basePlan.OwnerCompanionAfterImages,
            woundStages);
        Assert.True(
            composition.Success,
            string.Join(Environment.NewLine, composition.Issues.Select(
                static issue => $"{issue.Code}: {issue.Message}")));
        var effectCarrierAfterImages =
            composition.EffectCarrierAfterImages.ToDictionary(
                static pair => pair.Key,
                static pair => pair.Value.DeepClone().AsObject(),
                StringComparer.Ordinal);
        if (woundCarrierPath is not null)
        {
            var competingPath = string.Equals(
                woundCarrierPath,
                "game_state/effects/effects.json",
                StringComparison.Ordinal)
                ? WoundCarrierCatalog.PlayerPath
                : woundCarrierPath;
            effectCarrierAfterImages[competingPath] =
                new JsonObject { ["schemaVersion"] = 1 };
        }
        var woundPaths = new[]
        {
            WoundCommonCommandPath,
            WoundCarrierCatalog.PlayerPath,
            WoundIdentityState.StatePath,
            WoundHistoryState.HistoryPath
        }.Concat(woundCarrierPath is null
            ? Array.Empty<string>()
            : new[]
            {
                string.Equals(
                    woundCarrierPath,
                    "game_state/effects/effects.json",
                    StringComparison.Ordinal)
                    ? WoundCarrierCatalog.PlayerPath
                    : woundCarrierPath
            }).ToArray();
        var touchedWoundPaths = omitWoundCommandTouched
            ? woundPaths.Where(static path =>
                path != WoundCommonCommandPath).ToArray()
            : woundPaths;
        var effectivePending = pendingAfterImages ??
            basePlan.PendingAfterImages;
        var effectiveOwnerTransitions = ownerTransitions ??
            basePlan.OwnerTransitions;

        return new AcceptedMechanicsPlan(
            basePlan.InputFingerprint,
            basePlan.DefinitionAfterImage,
            stateAfterImage,
            basePlan.HistoryAfterImage,
            effectCarrierAfterImages,
            effectiveEffectPlan.IdentityIndexAfterImage,
            effectivePending,
            basePlan.OwnerCompanionAfterImages,
            basePlan.BeforeImages,
            basePlan.TouchedPaths
                .Where(path =>
                    !omitWoundCommandTouched ||
                    path != WoundCommonCommandPath)
                .Concat(touchedWoundPaths)
                .Concat(effectCarrierAfterImages.Keys)
                .Concat(effectivePending.Keys)
                .Concat(effectiveOwnerTransitions.Select(static value => value.Path))
                .ToArray(),
            omitWoundCommandConsumed
                ? basePlan.ConsumedPaths.Where(static path =>
                    path != WoundCommonCommandPath).ToArray()
                : basePlan.ConsumedPaths.Append(WoundCommonCommandPath).ToArray(),
            basePlan.AuthorityFingerprints,
            basePlan.ResourceEvents,
            basePlan.ProjectionInput,
            basePlan.OwnerAuthority,
            effectiveEffectPlan,
            effectiveOwnerTransitions,
            basePlan.PendingGmPacket,
            woundStageBundle: woundStages,
            carrierComposition: composition);
    }

    private static EffectAcceptedTurnPlan CompleteEffectPlanForAssembly(
        EffectAcceptedTurnPlan source)
    {
        var transcriptBuilder = new AcceptedEffectBoundaryTranscript.Builder(
            AcceptedMechanicsPlanner.CreateEffectPlanAuthority(source));
        transcriptBuilder.SealUseProjection();
        var transcript = transcriptBuilder.Freeze();
        Assert.True(
            transcript.IsValid,
            string.Join(Environment.NewLine, transcript.Issues.Select(static issue =>
                $"{issue.Code}: expected={issue.Expected}; actual={issue.Actual}")));
        var completed = EffectAcceptedTurnPlanner.CompleteAcceptedBoundaryTranscript(
            source,
            transcript.Transcript!,
            new EffectIdentityFactory());
        Assert.True(
            completed.Success,
            string.Join(Environment.NewLine, completed.Issues.Select(static issue =>
                $"{issue.Code}: {issue.Message}")));
        Assert.True(completed.Plan!.IsAcceptedBoundaryComplete);
        return completed.Plan;
    }

    private static AcceptedMechanicsInput CreateWoundCommonAcceptedInput(
        AcceptedMechanicsWoundStageBundle woundStages,
        string? omittedBeforeImagePath = null,
        string? woundCarrierPath = null,
        IReadOnlyDictionary<string, JsonObject?>? pendingAfterImages = null,
        IReadOnlyList<AcceptedMechanicsOwnerTransition>? ownerTransitions = null,
        bool alignCommonBinding = true)
    {
        var beforeImages = BeforeImages(new byte[] { 4, 5, 6 });
        beforeImages[WoundCommonCommandPath] =
            new CanonicalBeforeImage(true, new byte[] { 8 });
        beforeImages[EffectCarrierCatalog.PlayerPath] =
            new CanonicalBeforeImage(true, new byte[] { 15 });
        beforeImages[WoundCarrierCatalog.PlayerPath] =
            new CanonicalBeforeImage(true, new byte[] { 9 });
        beforeImages[WoundIdentityState.StatePath] =
            new CanonicalBeforeImage(true, new byte[] { 10 });
        beforeImages[WoundHistoryState.HistoryPath] =
            new CanonicalBeforeImage(true, new byte[] { 11 });
        foreach (var path in WoundAcceptedTurnSnapshotContract.RequiredPaths)
        {
            beforeImages.TryAdd(
                path,
                new CanonicalBeforeImage(false, null));
        }
        beforeImages[woundCarrierPath ?? WoundCarrierCatalog.PlayerPath] =
            new CanonicalBeforeImage(true, new byte[] { 12 });
        foreach (var path in pendingAfterImages?.Keys ?? Array.Empty<string>())
            beforeImages[path] = new CanonicalBeforeImage(true, new byte[] { 13 });
        foreach (var path in ownerTransitions?.Select(static value => value.Path) ??
                     Array.Empty<string>())
        {
            beforeImages[path] = new CanonicalBeforeImage(true, new byte[] { 14 });
        }
        if (omittedBeforeImagePath is not null)
            beforeImages.Remove(omittedBeforeImagePath);
        var baseline = Input(
            beforeImages: beforeImages,
            planningContext: CreateWoundCommonPlanningContext(woundStages),
            woundCommands: Object("schemaVersion", 1),
            woundInput: woundStages.Input,
            preserveMissingWoundCommandBeforeImage:
                omittedBeforeImagePath == WoundCommonCommandPath);
        if (!alignCommonBinding)
            return baseline;
        var binding = woundStages.Input.Binding;
        return new AcceptedMechanicsInput(
            binding.SessionId,
            binding.RequestId,
            binding.SnapshotToken,
            binding.Realm,
            binding.Turn,
            baseline.AcceptedEvents,
            baseline.ResourceCommands,
            baseline.EffectCommands,
            baseline.PendingInput,
            baseline.InternalInputs,
            baseline.AuthorityFingerprints,
            baseline.BeforeImages,
            baseline.ValidationIssues,
            baseline.PlanningContext,
            baseline.WoundCommands,
            baseline.WoundInput);
    }

    private static AcceptedMechanicsPlanningContext
        CreateWoundCommonPlanningContext(
            AcceptedMechanicsWoundStageBundle woundStages)
    {
        var baseline = Assert.IsType<AcceptedMechanicsPlanningContext>(
            ValidCommonInput().PlanningContext);
        return new AcceptedMechanicsPlanningContext(
            baseline.DefinitionRoot,
            baseline.Definitions,
            baseline.State,
            baseline.History,
            baseline.Owners,
            baseline.Sources,
            baseline.Commands,
            baseline.EffectIdentityRoot,
            woundStages.EffectBatchPlan.EffectPlan,
            baseline.CapacityTransitions,
            baseline.OwnerCapacityDrafts,
            baseline.TerminalOwners,
            baseline.OwnerCompanionAfterImages,
            baseline.OwnerTransitions,
            baseline.RegisteredSystemOutcomes,
            baseline.PendingResolutionState,
            baseline.ResourceIdentityFactory,
            baseline.EffectIdentityFactory,
            baseline.ExecutionSequenceOffset,
            woundStages);
    }

    private static void CorruptWoundCommonPreparedPayload(
        AcceptedMechanicsPlan plan)
    {
        var field = typeof(AcceptedMechanicsPlan).GetField(
            "_stateAfterImage",
            BindingFlags.Instance | BindingFlags.NonPublic) ??
            throw new InvalidOperationException(
                "Accepted mechanics state after-image field was not found.");
        var state = Assert.IsType<JsonObject>(field.GetValue(plan));
        state["forgedAfterConstruction"] = true;
    }

    private static void CorruptWoundCommonNestedBundle(
        AcceptedMechanicsPlan plan)
    {
        var bundleField = typeof(AcceptedMechanicsPlan).GetField(
            "_woundStageBundle",
            BindingFlags.Instance | BindingFlags.NonPublic) ??
            throw new InvalidOperationException(
                "Accepted mechanics wound bundle field was not found.");
        var bundle = Assert.IsType<AcceptedMechanicsWoundStageBundle>(
            bundleField.GetValue(plan));
        var finalField = typeof(AcceptedMechanicsWoundStageBundle).GetField(
            "_finalPlan",
            BindingFlags.Instance | BindingFlags.NonPublic) ??
            throw new InvalidOperationException(
                "Accepted mechanics wound final-stage field was not found.");
        var final = Assert.IsType<WoundAcceptedTurnPlan>(
            finalField.GetValue(bundle));
        var historyField = typeof(WoundAcceptedTurnPlan).GetField(
            "_historyAfterImage",
            BindingFlags.Instance | BindingFlags.NonPublic) ??
            throw new InvalidOperationException(
                "Accepted wound history after-image field was not found.");
        var history = Assert.IsType<JsonObject>(historyField.GetValue(final));
        history["forgedAfterConstruction"] = true;
    }

    private static EffectAcceptedTurnPlan MutateTopLevelEffectPlan(
        EffectAcceptedTurnPlan plan,
        string mutation)
    {
        var sources = plan.Sources.ToArray();
        var targets = plan.Targets.ToArray();
        var sourceBindings = plan.SourceBindings.ToArray();
        var deferredReactions = plan.DeferredReactions.ToArray();
        var reactionExpansionCount = plan.ReactionExpansionCount;
        var reactionExpansionUsage = plan.ReactionExpansionUsage.ToDictionary();

        switch (mutation)
        {
            case "sources":
            {
                var source = Assert.Single(sources);
                sources = sources.Append(source with
                {
                    SourceId = source.SourceId + "_changed"
                }).ToArray();
                break;
            }
            case "targets":
            {
                var target = Assert.Single(targets);
                targets = targets.Append(target with
                {
                    TargetId = target.TargetId + "_changed"
                }).ToArray();
                break;
            }
            case "source_bindings":
            {
                var binding = Assert.Single(sourceBindings);
                var definition = binding.Definition;
                definition["fingerprintProbe"] = true;
                sourceBindings[0] = binding with { Definition = definition };
                break;
            }
            case "deferred_reactions":
            {
                deferredReactions = deferredReactions.Append(
                    new EffectReactionExecution(
                        "event_effect_payload_probe",
                        "trigger_effect_payload_probe",
                        "causal_effect_payload_probe",
                        42,
                        "effect_payload_probe",
                        Assert.Single(targets),
                        "effect_effect_payload_probe",
                        "trigger_effect_payload_probe",
                        "component_effect_payload_probe",
                        "applied",
                        "none",
                        null,
                        1,
                        null,
                        null)).ToArray();
                break;
            }
            case "reaction_usage":
                reactionExpansionCount++;
                reactionExpansionUsage.Add(
                    new EffectReactionExpansionKey(
                        "effect_effect_payload_probe",
                        "component_effect_payload_probe"),
                    new EffectReactionExpansionUsage(1, 1));
                break;
            default:
                throw new InvalidOperationException(
                    "Unknown top-level effect-plan mutation.");
        }

        return new EffectAcceptedTurnPlan(
            plan.InputFingerprint,
            plan.CarrierAuthorityFingerprint,
            plan.SourceAuthorityFingerprint,
            plan.TargetAuthorityFingerprint,
            plan.AllocatedCombatantIds,
            plan.AllocatedEffectIds,
            plan.AllocatedTransitionIds,
            sources,
            targets,
            sourceBindings,
            deferredReactions,
            reactionExpansionCount,
            reactionExpansionUsage,
            plan.ActiveEffects,
            plan.ResourceTriggerCarriers,
            plan.SourceAuthority,
            plan.TargetAuthority,
            plan.EventInput,
            plan.CarrierBeforeImages,
            plan.CarrierAfterImages,
            plan.IdentityIndexBeforeImage,
            plan.IdentityIndexAfterImage,
            plan.TouchedPaths,
            plan.DeletedPaths,
            plan.AcceptedCarrierBaselines,
            acceptedBoundaryCompletionProof:
                ReadAcceptedBoundaryCompletionProof(plan),
            acceptedBoundaryBasePlanFingerprint:
                plan.AcceptedBoundaryBasePlanFingerprint,
            woundApplicationRootEffectBindings:
                plan.WoundApplicationRootEffectBindings);
    }

    private static EffectAcceptedTurnPlanner.AcceptedBoundaryCompletionProof?
        ReadAcceptedBoundaryCompletionProof(EffectAcceptedTurnPlan plan) =>
        (EffectAcceptedTurnPlanner.AcceptedBoundaryCompletionProof?)typeof(
                EffectAcceptedTurnPlan)
            .GetField(
                "_acceptedBoundaryCompletionProof",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic)!
            .GetValue(plan);

    private static WoundEffectBatchAcceptedPlan AssertWoundCommonEffect(
        WoundEffectBatchPlanningResult result)
    {
        Assert.True(
            result.Success,
            string.Join(Environment.NewLine, result.Issues.Select(static issue =>
                $"{issue.Code}: {issue.Message}")));
        return Assert.IsType<WoundEffectBatchAcceptedPlan>(result.Plan);
    }

    private static WoundAcceptedTurnPlan AssertWoundCommonFinal(
        WoundAcceptedTurnPlanningResult result)
    {
        Assert.True(
            result.Success,
            string.Join(Environment.NewLine, result.Issues.Select(static issue =>
                $"{issue.Code}: {issue.Message}")));
        return Assert.IsType<WoundAcceptedTurnPlan>(result.Plan);
    }

    private sealed record WoundCommonStageFixture(
        WoundAcceptedTurnInput Input,
        WoundPreparedAcceptedTurnPlan Prepared,
        WoundEffectBatchAcceptedPlan Effect,
        WoundAcceptedTurnPlan Final,
        AcceptedMechanicsWoundStageBundle Bundle);

    private sealed class WoundCommonEffectIdentityFactory : EffectIdentityFactory
    {
        private readonly string _prefix;
        private int _effectOrdinal;
        private int _transitionOrdinal;

        internal WoundCommonEffectIdentityFactory(string prefix) =>
            _prefix = prefix;

        internal override string CreateEffectId() =>
            $"effect_{_prefix}_{++_effectOrdinal:D3}";

        internal override string CreateTransitionId() =>
            $"effect_transition_{_prefix}_{++_transitionOrdinal:D3}";
    }

    private sealed class WoundCommonPlanner
    {
        internal int Calls { get; private set; }

        internal AcceptedMechanicsPlanningResult Build(
            AcceptedMechanicsInput input,
            string inputFingerprint)
        {
            Calls++;
            var stages = input.PlanningContext?.WoundStageBundle ??
                throw new InvalidOperationException(
                    "Expected a wound stage bundle in the common planning context.");
            return new AcceptedMechanicsPlanningResult(
                CreateWoundCommonPlan(
                    stages,
                    Calls,
                    acceptedInput: input,
                    acceptedInputFingerprint: inputFingerprint),
                Array.Empty<ValidationIssue>());
        }
    }
}
