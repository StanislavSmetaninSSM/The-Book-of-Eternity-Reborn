using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalWoundTreatmentResolverTests
{
    private static ResolverScenario CreateRecoveryPublicationScenario(
        string mode, string category, string shape, long progress = 0)
    {
        var tokens = shape.Split(',');
        var scenario = CreateOrderedReductionScenario(mode, category, "r1", 1);
        var operations = new JsonArray(tokens.Select(token => (JsonNode)(token switch
        {
            "s" => new JsonObject { ["kind"] = "stabilize" },
            "r1" => new JsonObject { ["kind"] = "reduce_severity", ["steps"] = 1 },
            "r2" => new JsonObject { ["kind"] = "reduce_severity", ["steps"] = 2 },
            _ when token.StartsWith("a", StringComparison.Ordinal) => new JsonObject
            {
                ["kind"] = "add_recovery",
                ["points"] = int.Parse(token.AsSpan(1), CultureInfo.InvariantCulture)
            },
            _ => throw new ArgumentOutOfRangeException(nameof(shape), token, null)
        })).ToArray());
        var route = scenario.Before["treatment"]!["routes"]![0]!;
        foreach (var band in route["outcomes"]!.AsArray().OfType<JsonObject>())
            band["result"] = operations.DeepClone();
        const string passiveEffectId = "effect_t070_recovery_characteristic";
        scenario.Before["consequences"]!["ownedEffectSources"] =
            WoundContractTestData.CreateOwnedEffectSourcesForTarget(
                scenario.Before["woundId"]!.GetValue<string>(),
                "mortal_world",
                "player",
                new[]
                {
                    (
                        EffectId: passiveEffectId,
                        DefinitionKey: "definition_t070_recovery_characteristic",
                        Profile: "characteristic_modifier")
                });
        scenario.Before["consequences"]!["ownedEffectSources"]!
            ["definitions"]![0]!["triggers"] = new JsonArray();
        scenario.Before["consequences"]!["ownedEffectSources"]!
            ["definitions"]![0]!["components"]![0]!["payload"]!["value"] = -1;
        scenario.Before["consequences"]!["entries"] = new JsonArray(new JsonObject
        {
            ["slot"] = 1,
            ["profileKey"] = "characteristic_modifier",
            ["effectId"] = passiveEffectId,
            ["readableSummary"] = "Passive recovery-publication consequence."
        });
        scenario.Before["recovery"]!["currentStepProgress"] = progress;
        scenario.Before["recovery"]!["currentStepThreshold"] = 2;
        return PrepareProcedurePublicationScenario(scenario with
        {
            OperationKey = "operation_t070_recovery_" + mode + "_" +
                category + "_" + shape.Replace(',', '_'),
            ExpectedIntentCount = tokens.Length
        });
    }

    [Fact]
    public void RecoveryPublication_AccumulatesWithoutImplicitThresholdTransition()
    {
        var scenario = CreateRecoveryPublicationScenario("procedure", "success", "a2", 1);
        using var fixture = AcceptedStateFixture.Create(scenario);
        var before = fixture.ReadCurrentWound();
        var initial = ResolveCurrentTreatment(fixture, "procedure",
            scenario.OperationKey, scenario.RouteId);
        var flow = PersistAndRehydrateTreatmentPublication(fixture, initial, "recovery publication");
        ComposeAndPublishCoordinatedTreatment(fixture, flow);
        var after = fixture.ReadCurrentWound();
        Assert.Equal(3L, after.Recovery.CurrentStepProgress);
        var beforeRoot = CanonicalWoundRoot(before);
        var afterRoot = CanonicalWoundRoot(after);
        Assert.True(JsonNode.DeepEquals(beforeRoot["severity"], afterRoot["severity"]));
        var expectedRecovery = beforeRoot["recovery"]!.DeepClone();
        expectedRecovery["currentStepProgress"] = 3L;
        Assert.True(JsonNode.DeepEquals(expectedRecovery, afterRoot["recovery"]));
        Assert.Equal(before.Care.State, after.Care.State);
        Assert.Single(fixture.ReadCurrentHistory().State!.Transitions,
            static row => row.Kind == "treat");
        fixture.RestartForReplay();
        Assert.Equal(3L, fixture.ReadCurrentWound().Recovery.CurrentStepProgress);
        var tree = CaptureResolverFixtureTree(fixture.Root);
        var replay = ProbePublishedTreatment(fixture, flow.Request);
        Assert.Equal("ExactReplay", Convert.ToString(ReadRequiredProperty(replay, "Status")));
        AssertResolverFixtureTreeUnchanged(fixture.Root, tree);
    }

    [Fact]
    public void RecoveryPublication_ThresholdAndMaximumProgressRemainNonterminal()
    {
        foreach (var (progress, expected) in new[]
                 {
                     (0L, 1L),
                     (1L, 2L),
                     (2L, 3L),
                     (long.MaxValue - 1, long.MaxValue)
                 })
        {
            var scenario = CreateRecoveryPublicationScenario(
                "procedure", "success", "a1", progress);
            using var fixture = AcceptedStateFixture.Create(scenario);
            var before = fixture.ReadCurrentWound();
            var effectCarrier = fixture.ReadPlayerEffectCarrier();
            var effectIdentity = fixture.ReadEffectIdentityIndex();
            var flow = ResolveCurrentTreatment(fixture, "procedure",
                scenario.OperationKey, scenario.RouteId);

            ComposeAndPublishCoordinatedTreatment(fixture,
                PersistAndRehydrateTreatmentPublication(
                    fixture, flow, "nonterminal recovery progress"));

            var after = fixture.ReadCurrentWound();
            Assert.Equal(expected, after.Recovery.CurrentStepProgress);
            Assert.Equal("active", after.Lifecycle);
            AssertRecoveryMembersUnchangedExceptProgress(before, after, expected);
            Assert.Equal(
                before.Consequences.OwnedEffectSources.RootBindings
                    .Select(static value => value.EffectId),
                after.Consequences.OwnedEffectSources.RootBindings
                    .Select(static value => value.EffectId));
            var afterEffectCarrier = fixture.ReadPlayerEffectCarrier();
            Assert.True(
                JsonNode.DeepEquals(effectCarrier, afterEffectCarrier),
                $"Expected unchanged carrier: {effectCarrier}\nActual: {afterEffectCarrier}");
            var afterEffectIdentity = fixture.ReadEffectIdentityIndex();
            Assert.True(
                JsonNode.DeepEquals(effectIdentity, afterEffectIdentity),
                $"Expected unchanged identity: {effectIdentity}\nActual: {afterEffectIdentity}");
        }

        var severityOne = CreateRecoveryPublicationScenario(
            "procedure", "success", "a1", 1);
        severityOne.Before["severity"]!["value"] = "I";
        severityOne.Before["severity"]!["rank"] = 1;
        severityOne.Before["severity"]!["maximumAtCreation"] = "I";
        severityOne.Before["consequences"]!["slotBudget"] = 1;
        severityOne.Before["consequences"]!["slotsUsed"] = 1;
        Assert.Single(severityOne.Before["consequences"]!["entries"]!.AsArray());
        Assert.Single(severityOne.Before["consequences"]!["ownedEffectSources"]!["definitions"]!
            .AsArray());
        Assert.Single(severityOne.Before["consequences"]!["ownedEffectSources"]!["rootBindings"]!
            .AsArray());
        severityOne = severityOne with
        {
            History = CreateCurrentWoundHistory(severityOne.Before)
        };
        using var severityOneFixture = AcceptedStateFixture.Create(severityOne);
        var severityOneBefore = severityOneFixture.ReadCurrentWound();
        var severityOneEffectCarrier = severityOneFixture.ReadPlayerEffectCarrier();
        var severityOneEffectIdentity = severityOneFixture.ReadEffectIdentityIndex();
        var severityOneFlow = ResolveCurrentTreatment(
            severityOneFixture,
            "procedure",
            severityOne.OperationKey,
            severityOne.RouteId);

        ComposeAndPublishCoordinatedTreatment(
            severityOneFixture,
            PersistAndRehydrateTreatmentPublication(
                severityOneFixture,
                severityOneFlow,
                "severity-one nonterminal recovery progress"));

        var severityOneAfter = severityOneFixture.ReadCurrentWound();
        Assert.Equal("active", severityOneAfter.Lifecycle);
        Assert.Equal("I", severityOneAfter.Severity.Value);
        Assert.Equal(1, severityOneAfter.Severity.Rank);
        Assert.Equal(2L, severityOneAfter.Recovery.CurrentStepProgress);
        Assert.Empty(severityOneAfter.Relations.LegacyRefs);
        Assert.DoesNotContain(
            severityOneFixture.ReadCurrentHistory().State!.Transitions,
            static row => row.Terminal || row.Kind == "heal");
        AssertRecoveryMembersUnchangedExceptProgress(
            severityOneBefore, severityOneAfter, 2L);
        Assert.Equal(
            Assert.Single(severityOneBefore.Consequences.OwnedEffectSources.RootBindings)
                .EffectId,
            Assert.Single(severityOneAfter.Consequences.OwnedEffectSources.RootBindings)
                .EffectId);
        Assert.True(JsonNode.DeepEquals(
            severityOneEffectCarrier,
            severityOneFixture.ReadPlayerEffectCarrier()));
        Assert.True(JsonNode.DeepEquals(
            severityOneEffectIdentity,
            severityOneFixture.ReadEffectIdentityIndex()));
    }

    [Fact]
    public void RecoveryPublication_OrderedRecoverySupportsEveryImplementedCombination()
    {
        var cases = new[]
        {
            (Shape: "a2", Progress: 2L, Severity: 3, Stabilized: false),
            (Shape: "a1,a1", Progress: 2L, Severity: 3, Stabilized: false),
            (Shape: "s,a1", Progress: 1L, Severity: 3, Stabilized: true),
            (Shape: "a1,s", Progress: 1L, Severity: 3, Stabilized: true),
            (Shape: "a1,r1", Progress: 1L, Severity: 2, Stabilized: false),
            (Shape: "r1,a1", Progress: 1L, Severity: 2, Stabilized: false),
            (Shape: "a1,r1,s,a1", Progress: 2L, Severity: 2, Stabilized: true),
            (Shape: "r1,a1,r1", Progress: 1L, Severity: 1, Stabilized: false),
            (Shape: "a1,a1,a1,a1,a1,a1,a1,a1", Progress: 8L, Severity: 3,
                Stabilized: false)
        };
        foreach (var item in cases)
        {
            var scenario = CreateRecoveryPublicationScenario(
                "procedure", "success", item.Shape);
            using var fixture = AcceptedStateFixture.Create(scenario);
            var before = fixture.ReadCurrentWound();
            var flow = ResolveCurrentTreatment(
                fixture, "procedure", scenario.OperationKey, scenario.RouteId);
            var resolution = Assert.IsType<MortalWoundTreatmentResolution>(
                flow.Resolution);
            var tokens = item.Shape.Split(',');
            Assert.Equal(Enumerable.Range(0, tokens.Length),
                resolution.OutcomeIntents.Select(static intent => intent.OperationOrdinal));
            Assert.Equal(
                tokens.Select(static token => token.StartsWith("a", StringComparison.Ordinal)
                    ? int.Parse(token.AsSpan(1), CultureInfo.InvariantCulture)
                    : (int?)null),
                resolution.OutcomeIntents.Select(static intent =>
                    (intent as MortalWoundAddRecoveryOutcomeIntent)?.Points));

            ComposeAndPublishCoordinatedTreatment(
                fixture,
                PersistAndRehydrateTreatmentPublication(
                    fixture, flow, "ordered recovery publication"));

            var after = fixture.ReadCurrentWound();
            Assert.Equal(item.Progress, after.Recovery.CurrentStepProgress);
            Assert.Equal(item.Severity, after.Severity.Rank);
            Assert.Equal(item.Stabilized ? "stabilized" : "untreated", after.Care.State);
            if (item.Stabilized)
            {
                var anchor = Assert.IsType<WoundRecoveryAnchor>(after.Recovery.RecoveryAnchor);
                Assert.Equal("stabilization", anchor.AnchorKind);
                Assert.Equal(1_260L, anchor.AnchorMinute);
                Assert.Equal(after.LastTransition.TransitionId, anchor.AnchorTransitionId);
                Assert.DoesNotContain("not_stabilized", after.Recovery.Blockers);
            }
            else
            {
                AssertRecoveryMembersUnchangedExceptProgress(before, after, item.Progress);
            }

            var beforeRoots = before.Consequences.OwnedEffectSources.RootBindings
                .Select(static value => value.EffectId)
                .ToArray();
            var afterRoots = after.Consequences.OwnedEffectSources.RootBindings
                .Select(static value => value.EffectId)
                .ToArray();
            if (tokens.Any(static token => token.StartsWith("r", StringComparison.Ordinal)))
            {
                Assert.NotEmpty(afterRoots);
                Assert.Empty(beforeRoots.Intersect(afterRoots, StringComparer.Ordinal));
            }
            else
            {
                Assert.Equal(beforeRoots, afterRoots);
            }
            using var identityDocument = JsonDocument.Parse(
                fixture.ReadEffectIdentityIndex().ToJsonString());
            Assert.Empty(EffectIdentityState.Parse(
                identityDocument.RootElement,
                EffectIdentityState.StatePath).Issues);
        }
    }

    [Fact]
    public void RecoveryPublication_SelectedCategoryAndGuaranteeKeepExistingCompletionRules()
    {
        foreach (var category in new[] { "success", "partial_success", "failed_attempt" })
        {
            var scenario = CreateRecoveryPublicationScenario(
                "procedure", category, "a1");
            using var fixture = AcceptedStateFixture.Create(scenario);
            var flow = ResolveCurrentTreatment(
                fixture, "procedure", scenario.OperationKey, scenario.RouteId);
            var request = Assert.IsType<MortalWoundTreatmentAttemptRequest>(flow.Request);
            var resolution = Assert.IsType<MortalWoundTreatmentResolution>(flow.Resolution);
            var check = Assert.IsType<MortalWoundProcedureCheckAuthority>(request.ModeAuthority);
            Assert.Equal(category, resolution.ResultCategory);
            Assert.Equal(
                category == "success" ? "AppendOnce" : "None",
                resolution.RouteCompletion);
            Assert.Equal(
                category == "failed_attempt" ? new[] { 0, 1 } : new[] { 0 },
                check.SourceIndices);
            Assert.Equal("held", request.ResourceAuthority.ReservationDisposition);

            ComposeAndPublishCoordinatedTreatment(
                fixture,
                PersistAndRehydrateTreatmentPublication(
                    fixture, flow, "selected recovery category"));

            Assert.Equal(1, fixture.ReadNpcItemCount("sterile_thread"));
            var after = fixture.ReadCurrentWound();
            Assert.Equal(1L, after.Recovery.CurrentStepProgress);
            Assert.Equal(
                category == "success" ? new[] { scenario.RouteId } : Array.Empty<string>(),
                after.Treatment.CompletedRouteIds);
            var row = Assert.Single(
                fixture.ReadCurrentHistory().State!.Transitions,
                static candidate => candidate.Kind == "treat");
            var receipt = Assert.IsType<MortalWoundTreatmentReceipt>(
                row.TreatmentResult!.Receipt);
            Assert.Equal(category, receipt.ResultCategory);
            Assert.Equal(resolution.RouteCompletion, receipt.RouteCompletion);
            Assert.Equal(
                check.SourceIndices,
                Assert.IsType<MortalWoundProcedureModeEvidence>(receipt.ModeEvidence)
                    .SourceIndices);
        }

        foreach (var shape in new[] { "a1,a1", "s,a2" })
        {
            var scenario = CreateRecoveryPublicationScenario(
                "guaranteed", "success", shape);
            using var fixture = AcceptedStateFixture.Create(scenario);
            var flow = ResolveCurrentTreatment(
                fixture, "guaranteed", scenario.OperationKey, scenario.RouteId);
            var request = Assert.IsType<MortalWoundTreatmentAttemptRequest>(flow.Request);
            var resolution = Assert.IsType<MortalWoundTreatmentResolution>(flow.Resolution);
            var proof = Assert.IsType<MortalWoundTreatmentCapabilityProof>(
                request.ModeAuthority);
            Assert.Equal(2, proof.OperationLimits.MaximumRecoveryPoints);
            Assert.Equal("not_required", request.ResourceAuthority.ReservationDisposition);
            Assert.Equal("success", resolution.ResultCategory);
            Assert.Equal("AppendOnce", resolution.RouteCompletion);

            ComposeAndPublishTreatment(fixture, flow);

            Assert.Equal(2, fixture.ReadNpcItemCount("sterile_thread"));
            Assert.Equal(2L, fixture.ReadCurrentWound().Recovery.CurrentStepProgress);
            Assert.Equal(
                scenario.RouteId,
                Assert.Single(fixture.ReadCurrentWound().Treatment.CompletedRouteIds));
            var row = Assert.Single(
                fixture.ReadCurrentHistory().State!.Transitions,
                static candidate => candidate.Kind == "treat");
            Assert.Equal(
                proof.OperationLimits.MaximumRecoveryPoints,
                Assert.IsType<MortalWoundTreatmentCapabilityProof>(request.ModeAuthority)
                    .OperationLimits.MaximumRecoveryPoints);
            Assert.Equal("success", row.TreatmentResult!.Receipt.ResultCategory);
        }

        var unauthorized = CreateRecoveryPublicationScenario(
            "guaranteed", "success", "a3");
        using var unauthorizedFixture = AcceptedStateFixture.Create(unauthorized);
        var unauthorizedState = Assert.IsType<MortalWoundTreatmentAcceptedStateAuthority>(
            unauthorizedFixture.GetAcceptedState());
        var unauthorizedTree = CaptureResolverFixtureTree(unauthorizedFixture.Root);
        var rejected = MortalWoundTreatmentPlanner.PrepareGuaranteedRequest(
            unauthorizedState,
            unauthorizedFixture.ReadCurrentHistory(),
            unauthorizedFixture.ReadCurrentWound(),
            unauthorized.OperationKey,
            unauthorized.RouteId,
            unauthorizedFixture.AcceptedEventRef(unauthorizedState));
        Assert.False(rejected.IsValid);
        Assert.Null(rejected.Request);
        Assert.Contains(rejected.Issues, static issue =>
            issue.Code == "mortal_wound_treatment_capability_source_missing");
        AssertResolverFixtureTreeUnchanged(unauthorizedFixture.Root, unauthorizedTree);
    }

    [Fact]
    public void RecoveryPublication_CourseAccumulatesAcrossRealMilestonesAndRestart()
    {
        var scenario = CreateRecoveryCoursePublicationScenario();
        using var fixture = AcceptedStateFixture.Create(scenario);
        string? courseId = null;
        TreatmentFlow? lastFlow = null;
        for (var ordinal = 1; ordinal <= 3; ordinal++)
        {
            if (ordinal > 1)
            {
                fixture.RestartForReplay();
                fixture.PrepareNextTurn(
                    41 + ordinal,
                    (ordinal - 1) * 480,
                    "recovery_course_" + ordinal);
            }
            var flow = ResolveCurrentTreatment(
                fixture,
                "course",
                scenario.OperationKey + "_" + ordinal,
                scenario.RouteId);
            lastFlow = flow;
            var resolution = Assert.IsType<MortalWoundTreatmentResolution>(
                flow.Resolution);
            courseId ??= resolution.CourseId;
            Assert.Equal(courseId, resolution.CourseId);
            Assert.Equal(ordinal, resolution.CourseMilestoneOrdinal);
            Assert.Equal(ordinal == 3 ? "completed" : "active",
                resolution.CourseDisposition);
            Assert.Equal(ordinal == 3 ? "AppendOnce" : "None",
                resolution.RouteCompletion);
            var expectedKinds = ordinal switch
            {
                1 => Array.Empty<string>(),
                2 => new[] { "add_recovery", "reduce_severity" },
                _ => new[] { "add_recovery", "stabilize" }
            };
            Assert.Equal(expectedKinds,
                resolution.OutcomeIntents.Select(static intent => intent.Kind));

            ComposeAndPublishTreatment(fixture, flow);

            var wound = fixture.ReadCurrentWound();
            Assert.Equal(ordinal - 1, wound.Recovery.CurrentStepProgress);
            Assert.Equal(ordinal < 2 ? 3 : 2, wound.Severity.Rank);
            Assert.Equal(ordinal == 3 ? "stabilized" : "untreated", wound.Care.State);
            Assert.Equal(ordinal == 3 ? null : courseId, wound.Care.ActiveCourseId);
            Assert.Equal(8 - ordinal, fixture.ReadPlayerItemCount("antibiotic_dose"));
            fixture.AssertItemIdentityIndexValid();
            var rows = fixture.ReadCurrentHistory().State!.Transitions
                .Where(static row => row.Kind == "treat")
                .ToArray();
            Assert.Equal(ordinal, rows.Length);
            var receipt = Assert.IsType<MortalWoundTreatmentReceipt>(
                rows[^1].TreatmentResult!.Receipt);
            Assert.Equal(courseId, rows[^1].CourseId);
            Assert.Equal(ordinal, rows[^1].CourseMilestoneOrdinal);
            Assert.Equal(courseId, receipt.CourseId);
            Assert.Equal(ordinal, receipt.CourseMilestoneOrdinal);
            Assert.Equal(
                Assert.IsType<MortalWoundTreatmentAttemptRequest>(flow.Request)
                    .Coordinates.OperationKey,
                receipt.Coordinates.OperationKey);
        }

        fixture.RestartForReplay();
        Assert.Null(fixture.ReadCurrentWound().Care.ActiveCourseId);
        Assert.Equal(2L, fixture.ReadCurrentWound().Recovery.CurrentStepProgress);
        var tree = CaptureResolverFixtureTree(fixture.Root);
        var replay = ProbePublishedTreatment(fixture, lastFlow!.Request);
        Assert.Equal("ExactReplay", Convert.ToString(ReadRequiredProperty(replay, "Status")));
        AssertResolverFixtureTreeUnchanged(fixture.Root, tree);
    }

    [Fact]
    public void RecoveryPublication_OverflowRejectsBeforeReservationAndPublication()
    {
        AssertRecoveryOverflowRejected("procedure", "a1", long.MaxValue);
        AssertRecoveryOverflowRejected("guaranteed", "a1", long.MaxValue);
        AssertRecoveryOverflowRejected(
            "procedure", "a1,a1", long.MaxValue - 1);
    }

    [Theory]
    [InlineData("points")]
    [InlineData("ordinal")]
    [InlineData("operation_kind")]
    [InlineData("ordered_position")]
    [InlineData("provisional_progress")]
    public void RecoveryPublication_DetachedPreparationRejectsChangedRecoveryPayload(
        string mutation)
    {
        var scenario = CreateRecoveryPublicationScenario(
            "procedure", "success", "a1,s", 1);
        using var fixture = AcceptedStateFixture.Create(scenario);
        var flow = ResolveCurrentTreatment(
            fixture, "procedure", scenario.OperationKey, scenario.RouteId);
        var acceptedState = Assert.IsType<MortalWoundTreatmentAcceptedStateAuthority>(
            flow.AcceptedState);
        var request = Assert.IsType<MortalWoundTreatmentAttemptRequest>(flow.Request);
        var resolution = Assert.IsType<MortalWoundTreatmentResolution>(flow.Resolution);
        var prepared = MortalWoundTreatmentOutcomePublicationPlanner.Prepare(
            acceptedState,
            request,
            resolution,
            acceptedState.CurrentGameMinute);
        Assert.True(prepared.IsValid, DescribeIssues(prepared.Issues));
        var preparation = Assert.IsType<MortalWoundTreatmentOutcomePreparation>(
            prepared.Preparation);
        var positive = MortalWoundTreatmentOutcomePublicationPlanner.Finalize(
            preparation.DetachedCopy(),
            resolution,
            null,
            new Dictionary<string, EffectAcceptedApplicationResult>());
        Assert.True(positive.IsValid, DescribeIssues(positive.Issues));
        Assert.Equal(2L, positive.After!.Recovery.CurrentStepProgress);
        var tree = CaptureResolverFixtureTree(fixture.Root);

        if (mutation == "provisional_progress")
        {
            var alteredPreparation = preparation.DetachedCopy();
            var internalAfter = Assert.IsType<WoundMaterializationEnvelope>(
                typeof(MortalWoundTreatmentOutcomePreparation)
                    .GetField("_provisionalAfter",
                        System.Reflection.BindingFlags.NonPublic |
                        System.Reflection.BindingFlags.Instance)!
                    .GetValue(alteredPreparation));
            SetCourseTestBackingField(
                internalAfter.Recovery,
                "CurrentStepProgress",
                3L);
            var rejected = MortalWoundTreatmentOutcomePublicationPlanner.Finalize(
                alteredPreparation,
                resolution,
                null,
                new Dictionary<string, EffectAcceptedApplicationResult>());
            Assert.False(rejected.IsValid);
            Assert.Null(rejected.After);
            AssertResolverFixtureTreeUnchanged(fixture.Root, tree);
            return;
        }

        var recovery = Assert.IsType<MortalWoundAddRecoveryOutcomeIntent>(
            resolution.OutcomeIntents[0]);
        var stabilization = Assert.IsType<MortalWoundStabilizeOutcomeIntent>(
            resolution.OutcomeIntents[1]);
        IReadOnlyList<MortalWoundTreatmentOutcomeIntent> changedIntents = mutation switch
        {
            "points" => new MortalWoundTreatmentOutcomeIntent[]
            {
                MortalWoundAddRecoveryOutcomeIntent.Create(
                    0,
                    recovery.DeclaredOperationFingerprint,
                    recovery.IntentFingerprint,
                    2),
                stabilization
            },
            "ordinal" => new MortalWoundTreatmentOutcomeIntent[]
            {
                MortalWoundAddRecoveryOutcomeIntent.Create(
                    1,
                    recovery.DeclaredOperationFingerprint,
                    recovery.IntentFingerprint,
                    recovery.Points),
                stabilization
            },
            "operation_kind" => new MortalWoundTreatmentOutcomeIntent[]
            {
                MortalWoundStabilizeOutcomeIntent.Create(
                    0,
                    recovery.DeclaredOperationFingerprint,
                    recovery.IntentFingerprint),
                stabilization
            },
            "ordered_position" => new MortalWoundTreatmentOutcomeIntent[]
            {
                stabilization,
                recovery
            },
            _ => throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null)
        };
        var changedResolution = mutation is "ordinal" or "operation_kind" or
            "ordered_position"
            ? CloneResolutionWithIntentsUnchecked(resolution, changedIntents)
            : ResealRecoveryResolution(resolution, request, changedIntents);
        var rejectedPreparation = MortalWoundTreatmentOutcomePublicationPlanner.Prepare(
            acceptedState,
            request,
            changedResolution,
            acceptedState.CurrentGameMinute);
        Assert.False(rejectedPreparation.IsValid);
        Assert.Null(rejectedPreparation.Preparation);
        var rejectedFinal = MortalWoundTreatmentOutcomePublicationPlanner.Finalize(
            preparation.DetachedCopy(),
            changedResolution,
            null,
            new Dictionary<string, EffectAcceptedApplicationResult>());
        Assert.False(rejectedFinal.IsValid);
        Assert.Null(rejectedFinal.After);
        AssertResolverFixtureTreeUnchanged(fixture.Root, tree);
    }

    [Fact]
    public void RecoveryPublication_PostWriteFailureRestoresProgressResourcesAndHistoryThenRetriesOnce()
    {
        var fault = new ResourcePublicationFailureInjection();
        var scenario = CreateRecoveryPublicationScenario(
            "procedure", "success", "a2");
        using var fixture = AcceptedStateFixture.Create(scenario, new FileSystemManagerHooks
        {
            BeforeCanonicalMutationAsync = fault.BeforeCanonicalMutationAsync
        });
        var flow = PersistAndRehydrateTreatmentPublication(
            fixture,
            ResolveCurrentTreatment(
                fixture, "procedure", scenario.OperationKey, scenario.RouteId),
            "recovery publication rollback");
        var plan = ComposeCoordinatedTreatmentPlan(fixture, flow);
        var tree = CaptureResolverFixtureTree(fixture.Root);
        fault.Arm(
            WoundHistoryState.HistoryPath,
            fixture.TargetCarrierPath,
            ReadCanonicalBytes(fixture, fixture.TargetCarrierPath),
            fixture.FileSystem);

        var exception = Assert.Throws<CanonicalStateWriteException>(() =>
            PublishCachedResourcePlanOpen(fixture, flow, plan));

        Assert.Equal(WoundHistoryState.HistoryPath, exception.RelativePath);
        Assert.True(fault.Fired);
        Assert.True(fault.ObservedEarlierResourceWrite);
        AssertResolverFixtureTreeUnchanged(fixture.Root, tree);
        using var publication = PublishCachedResourcePlanOpen(fixture, flow, plan);
        publication.CompleteAtFullPipelineEnd();
        Assert.Equal(2L, fixture.ReadCurrentWound().Recovery.CurrentStepProgress);
        Assert.Equal(1, fixture.ReadNpcItemCount("sterile_thread"));
        fixture.AssertItemIdentityIndexValid();
        Assert.Single(
            fixture.ReadCurrentHistory().State!.Transitions,
            static row => row.Kind == "treat");
    }

    private static ResolverScenario CreateRecoveryCoursePublicationScenario()
    {
        var scenario = CreateScalarCoursePublicationScenario();
        scenario.Before["severity"]!["value"] = "III";
        scenario.Before["severity"]!["rank"] = 3;
        scenario.Before["severity"]!["maximumAtCreation"] = "III";
        scenario.Before["consequences"]!["slotBudget"] = 3;
        var route = scenario.Before["treatment"]!["routes"]![0]!;
        route["outcomes"] = new JsonArray(
            CourseMilestone(1, 0, "active", new JsonArray()),
            CourseMilestone(2, 480, "active", new JsonArray(
                new JsonObject { ["kind"] = "add_recovery", ["points"] = 1 },
                new JsonObject { ["kind"] = "reduce_severity", ["steps"] = 1 })),
            CourseMilestone(3, 960, "completed", new JsonArray(
                new JsonObject { ["kind"] = "add_recovery", ["points"] = 1 },
                new JsonObject { ["kind"] = "stabilize" })));
        route["resourcePolicy"]!["mutations"] = new JsonArray(
            CourseMutation(1), CourseMutation(2), CourseMutation(3));
        return scenario with
        {
            OperationKey = "operation_t070_recovery_course",
            History = CreateCurrentWoundHistory(scenario.Before),
            SeedCanonicalWoundEffects = true
        };
    }

    private static void AssertRecoveryOverflowRejected(
        string mode,
        string shape,
        long progress)
    {
        var scenario = CreateRecoveryPublicationScenario(
            mode, "success", shape, progress);
        const string legalSuffix = "_legal_stabilization";
        var rejectedRoute = scenario.Before["treatment"]!["routes"]![0]!.AsObject();
        var legalRoute = rejectedRoute.DeepClone().AsObject();
        var legalRouteId = scenario.RouteId + legalSuffix;
        legalRoute["routeId"] = legalRouteId;
        foreach (var outcome in legalRoute["outcomes"]!.AsArray().OfType<JsonObject>())
        {
            outcome["result"] = new JsonArray(new JsonObject
            {
                ["kind"] = "stabilize"
            });
        }
        scenario.Before["treatment"]!["routes"] = new JsonArray(
            rejectedRoute.DeepClone(), legalRoute);
        scenario.Before["treatment"]!["knownRouteIds"] = new JsonArray(
            scenario.RouteId, legalRouteId);
        scenario = scenario with
        {
            History = CreateCurrentWoundHistory(scenario.Before)
        };
        using var fixture = AcceptedStateFixture.Create(scenario);
        var acceptedState = Assert.IsType<MortalWoundTreatmentAcceptedStateAuthority>(
            fixture.GetAcceptedState());
        var before = fixture.ReadCurrentWound();
        var history = fixture.ReadCurrentHistory();
        var tree = CaptureResolverFixtureTree(fixture.Root);
        var itemCount = fixture.ReadNpcItemCount("sterile_thread");

        MortalWoundTreatmentAttemptRequestResult rejected = mode == "procedure"
            ? MortalWoundTreatmentPlanner.PrepareProcedureRequest(
                acceptedState,
                history,
                before,
                scenario.OperationKey,
                scenario.RouteId,
                fixture.AcceptedEventRef(acceptedState))
            : MortalWoundTreatmentPlanner.PrepareGuaranteedRequest(
                acceptedState,
                history,
                before,
                scenario.OperationKey,
                scenario.RouteId,
                fixture.AcceptedEventRef(acceptedState));
        Assert.False(rejected.IsValid);
        Assert.Null(rejected.Request);
        Assert.NotEmpty(rejected.Issues);
        Assert.Equal(itemCount, fixture.ReadNpcItemCount("sterile_thread"));
        Assert.Equal(
            history.State!.Transitions.Count,
            fixture.ReadCurrentHistory().State!.Transitions.Count);
        Assert.DoesNotContain(
            fixture.ReadCurrentHistory().State!.Transitions,
            static row => row.Kind == "treat");
        AssertResolverFixtureTreeUnchanged(fixture.Root, tree);

        MortalWoundTreatmentAttemptRequestResult legal = mode == "procedure"
            ? MortalWoundTreatmentPlanner.PrepareProcedureRequest(
                acceptedState,
                history,
                before,
                scenario.OperationKey + legalSuffix,
                legalRouteId,
                fixture.AcceptedEventRef(acceptedState))
            : MortalWoundTreatmentPlanner.PrepareGuaranteedRequest(
                acceptedState,
                history,
                before,
                scenario.OperationKey + legalSuffix,
                legalRouteId,
                fixture.AcceptedEventRef(acceptedState));
        Assert.True(legal.IsValid, DescribeIssues(legal.Issues));
        var legalRequest = Assert.IsType<MortalWoundTreatmentAttemptRequest>(legal.Request);
        if (mode == "procedure")
        {
            Assert.Equal(
                new[] { 0 },
                Assert.IsType<MortalWoundProcedureCheckAuthority>(legalRequest.ModeAuthority)
                    .SourceIndices);
            AssertSingleHeldClaim(legalRequest, "sterile_thread");
        }
        else
        {
            Assert.Equal(
                2,
                Assert.IsType<MortalWoundTreatmentCapabilityProof>(legalRequest.ModeAuthority)
                    .OperationLimits.MaximumRecoveryPoints);
            Assert.Equal("not_required",
                legalRequest.ResourceAuthority.ReservationDisposition);
        }
    }

    private static MortalWoundTreatmentResolution ResealRecoveryResolution(
        MortalWoundTreatmentResolution resolution,
        MortalWoundTreatmentAttemptRequest request,
        IReadOnlyList<MortalWoundTreatmentOutcomeIntent> intents) =>
        MortalWoundTreatmentResolution.Create(
            resolution.Mode,
            resolution.Coordinates,
            resolution.AttemptDisposition,
            resolution.ResultCategory,
            resolution.SelectedOutcomeIndex,
            resolution.Interruption,
            resolution.DeclaredResult,
            intents,
            resolution.CriticalReactionIntent,
            resolution.ConsumptionTrigger,
            resolution.CourseId,
            resolution.CourseMilestoneOrdinal,
            resolution.CourseDisposition,
            request,
            resolution.ModeEvidence,
            resolution.RouteFingerprint,
            resolution.RouteCompletion);

    private static void AssertRecoveryMembersUnchangedExceptProgress(
        WoundMaterializationEnvelope before,
        WoundMaterializationEnvelope after,
        long expectedProgress)
    {
        var expected = CanonicalWoundRoot(before)["recovery"]!.DeepClone();
        expected["currentStepProgress"] = expectedProgress;
        Assert.True(JsonNode.DeepEquals(
            expected,
            CanonicalWoundRoot(after)["recovery"]));
    }

}
