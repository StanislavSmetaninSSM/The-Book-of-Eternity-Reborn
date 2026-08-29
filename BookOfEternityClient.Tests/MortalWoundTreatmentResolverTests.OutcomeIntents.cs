using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// T061/T067 RED coverage for the typed outcome-intent handoff.  This partial owns no
/// request, resolution, intent, receipt, history transition, proof, ID allocator, or
/// fingerprint: all of them must come from the production treatment resolver.
/// </summary>
public sealed partial class MortalWoundTreatmentResolverTests
{
    public static IEnumerable<object[]> OutcomeIntentRows => new[]
    {
        new OutcomeIntentCase("no_improvement", "procedure_disadvantage_uses_two_contiguous_dice", "procedure", "failed_attempt", new[] { "no_improvement" }),
        new OutcomeIntentCase("stabilize_reduce", "procedure_normal_uses_lowest_free_die", "procedure", "success", new[] { "stabilize", "reduce_severity" }),
        new OutcomeIntentCase("stabilize_recovery", "procedure_normal_uses_lowest_free_die", "procedure", "success", new[] { "stabilize", "add_recovery" }),
        new OutcomeIntentCase("effectful_complication", "procedure_disadvantage_uses_two_contiguous_dice", "procedure", "failed_attempt", new[] { "add_complication" }),
        new OutcomeIntentCase("deterioration_handoff", "procedure_disadvantage_uses_two_contiguous_dice", "procedure", "failed_attempt", new[] { "apply_deterioration" }),
        new OutcomeIntentCase("guaranteed_remove_then_heal", "guaranteed_severity_one_heal_has_empty_legacy_array", "guaranteed", "success", new[] { "remove_complication", "heal" })
    }.Select(static row => new object[] { row });

    [Theory]
    [MemberData(nameof(OutcomeIntentRows))]
    public void OutcomeIntents_AreProductionDerivedOneForEachDeclaredOperation(
        OutcomeIntentCase testCase)
    {
        AssertResolvedOutcomeIntentPair(ResolveProductionOutcomeIntent(testCase), testCase);
    }

    [Theory]
    [MemberData(nameof(OutcomeIntentRows))]
    public void FixtureControl_OutcomeIntentModifiedValidRouteParsesBeforePlannerExists(
        OutcomeIntentCase testCase)
    {
        var scenario = CreateOutcomeIntentScenario(testCase);
        var before = WoundMaterializationContract.Parse(scenario.Before.ToJsonString(), "wound");
        var history = WoundHistoryState.Parse(scenario.History.ToJsonString(), "history");

        Assert.True(before.IsValid, DescribeIssues(before.Issues));
        Assert.True(history.IsValid, DescribeIssues(history.Issues));
        Assert.Equal(testCase.Mode, Assert.Single(before.Wound!.Treatment.Routes).Mode);
        Assert.Equal(testCase.Kinds, SelectedOutcomeKinds(before.Wound!, testCase));
    }

    [Fact]
    public void Parser_OutcomeIntentDuplicateAddComplicationRefRejectsAtTheSecondLocalRef()
    {
        var testCase = new OutcomeIntentCase(
            "effectful_complication",
            "procedure_disadvantage_uses_two_contiguous_dice",
            "procedure",
            "failed_attempt",
            new[] { "add_complication" });
        var scenario = CreateOutcomeIntentScenario(testCase);
        var route = scenario.Before["treatment"]!["routes"]![0]!.AsObject();
        route["outcomes"]![2]!["result"] = new JsonArray(
            new JsonObject { ["kind"] = "add_complication", ["complicationDraft"] = CreateEffectfulComplicationDraft("t061_collision") },
            new JsonObject { ["kind"] = "add_complication", ["complicationDraft"] = CreateEffectfulComplicationDraft("t061_collision") });

        var before = WoundMaterializationContract.Parse(scenario.Before.ToJsonString(), "wound");

        Assert.False(before.IsValid);
        Assert.Contains(before.Issues, issue =>
            string.Equals(issue.FilePath,
                "wound.treatment.routes[0].outcomes[2].result[1].complicationDraft.complications[0].complicationRef",
                StringComparison.Ordinal)
            && string.Equals(issue.Code, "wound_materialization_invalid_field", StringComparison.Ordinal));
    }

    [Fact]
    public void DeteriorationHandoff_UsesTheExactT069TypedAuthorityFactory()
    {
        var authority = typeof(WoundMaterializationContract).Assembly.GetType(
            "BookOfEternityClient.Services.MortalWoundDeteriorationPolicyAuthority",
            throwOnError: false,
            ignoreCase: false);
        Assert.True(authority is not null,
            "T069 must own the typed deterioration authority; T061 must never supply policy JSON or a fingerprint.");
        var create = ExactStaticMethod(authority!, "Create", 5);
        Assert.Equal("MortalWoundDeteriorationPolicyAuthorityResult", create.ReturnType.Name);
        Assert.Equal("FileSystemManager", create.GetParameters()[0].ParameterType.Name);
        Assert.Equal("CanonicalWriteLease", create.GetParameters()[1].ParameterType.Name);
        Assert.Equal("WoundAcceptedTurnBinding", create.GetParameters()[2].ParameterType.Name);
        Assert.Equal(typeof(string), create.GetParameters()[3].ParameterType);
        Assert.Equal(typeof(string), create.GetParameters()[4].ParameterType);
        AssertClosedResultType(create.ReturnType, "Authority");

        // The resolver-facing overload is intentionally distinct: planner code obtains
        // the T069 authority from its accepted state and coordinates and never accepts a
        // caller-made fingerprint, file-system root, lease, or binding.
        var resolverFacing = ExactStaticMethod(authority, "Create", 3);
        Assert.Equal(typeof(string), resolverFacing.GetParameters()[2].ParameterType);
        Assert.DoesNotContain(resolverFacing.GetParameters(), static parameter =>
            typeof(JsonNode).IsAssignableFrom(parameter.ParameterType) ||
            parameter.Name!.Contains("fingerprint", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("MortalWoundDeteriorationPolicyAuthorityResult", resolverFacing.ReturnType.Name);
        AssertClosedResultType(resolverFacing.ReturnType, "Authority");
    }

    [Fact]
    public void OutcomeIntent_DerivedComplicationIdsAreStableForExactInputAndChangeWithSealedRequestOrLocalRef()
    {
        var testCase = new OutcomeIntentCase(
            "effectful_complication",
            "procedure_disadvantage_uses_two_contiguous_dice",
            "procedure",
            "failed_attempt",
            new[] { "add_complication" });
        var exactRun = ResolveProductionOutcomeIntentRun(testCase, resolveExactRetry: true);
        var exactFirst = OutcomeIntentAt(exactRun.First, 0);
        var exactSecond = OutcomeIntentAt(exactRun.ExactRetry!, 0);
        Assert.Equal(
            Convert.ToString(ReadRequiredProperty(exactFirst, "ComplicationId")),
            Convert.ToString(ReadRequiredProperty(exactSecond, "ComplicationId")));
        Assert.Equal(
            Convert.ToString(ReadRequiredProperty(exactFirst, "IntentFingerprint")),
            Convert.ToString(ReadRequiredProperty(exactSecond, "IntentFingerprint")));

        var changedRequest = OutcomeIntentAt(
            ResolveProductionOutcomeIntent(testCase, operationSuffix: "_different_request"), 0);
        var changedLocalRef = OutcomeIntentAt(
            ResolveProductionOutcomeIntent(testCase, complicationRef: "t061_irritation_changed"), 0);
        Assert.NotEqual(
            Convert.ToString(ReadRequiredProperty(exactFirst, "ComplicationId")),
            Convert.ToString(ReadRequiredProperty(changedRequest, "ComplicationId")));
        Assert.NotEqual(
            Convert.ToString(ReadRequiredProperty(exactFirst, "ComplicationId")),
            Convert.ToString(ReadRequiredProperty(changedLocalRef, "ComplicationId")));
        Assert.NotEqual(
            CanonicalBindings(exactFirst, "DefinitionReferenceBindings"),
            CanonicalBindings(changedLocalRef, "DefinitionReferenceBindings"));
        Assert.NotEqual(
            CanonicalBindings(exactFirst, "ApplicationReferenceBindings"),
            CanonicalBindings(changedLocalRef, "ApplicationReferenceBindings"));
    }

    [Fact]
    public void OutcomeIntent_LegacyIdChangesWhenTheSameLocalRefMovesToAnotherOrdinal()
    {
        var testCase = new OutcomeIntentCase(
            "guaranteed_remove_then_heal",
            "guaranteed_severity_one_heal_has_empty_legacy_array",
            "guaranteed",
            "success",
            new[] { "remove_complication", "heal" });
        var exactRun = ResolveProductionOutcomeIntentRun(testCase, resolveExactRetry: true);
        var first = MechanicalLegacySeed(OutcomeIntentAt(exactRun.First, 1));
        var exactRetry = MechanicalLegacySeed(OutcomeIntentAt(exactRun.ExactRetry!, 1));
        var reordered = MechanicalLegacySeed(OutcomeIntentAt(
            ResolveProductionOutcomeIntent(testCase, reverseLegacies: true), 1));
        Assert.Equal(Convert.ToString(ReadRequiredProperty(first, "LegacyId")),
            Convert.ToString(ReadRequiredProperty(exactRetry, "LegacyId")));
        Assert.NotEqual(Convert.ToString(ReadRequiredProperty(first, "LegacyId")),
            Convert.ToString(ReadRequiredProperty(reordered, "LegacyId")));
    }

    [Fact]
    public void OutcomeIntent_HealChildCoordinatesAreStableForRetryAndChangeWithTheSealedRequest()
    {
        var testCase = GuaranteedHealCase();
        var exactRun = ResolveProductionOutcomeIntentRun(testCase, resolveExactRetry: true);
        var first = OutcomeIntentAt(exactRun.First, 1);
        var retry = OutcomeIntentAt(exactRun.ExactRetry!, 1);
        var changedRequest = OutcomeIntentAt(
            ResolveProductionOutcomeIntent(testCase, operationSuffix: "_different_request"), 1);

        var firstCoordinates = CanonicalValue(ReadRequiredProperty(first, "HealChildCoordinates"));
        Assert.Equal(firstCoordinates,
            CanonicalValue(ReadRequiredProperty(retry, "HealChildCoordinates")));
        Assert.NotEqual(firstCoordinates,
            CanonicalValue(ReadRequiredProperty(changedRequest, "HealChildCoordinates")));
    }

    [Fact]
    public void OutcomeIntent_QualifyingRouteCompletesOnceButAnAlreadyRecordedRouteDoesNotAppend()
    {
        var testCase = new OutcomeIntentCase(
            "stabilize_reduce",
            "procedure_normal_uses_lowest_free_die",
            "procedure",
            "success",
            new[] { "stabilize", "reduce_severity" });
        var first = ResolveProductionOutcomeIntent(testCase);
        var recorded = ResolveProductionOutcomeIntent(testCase, routeAlreadyCompleted: true);
        Assert.Equal("AppendOnce", Convert.ToString(ReadRequiredProperty(
            ReadRequiredProperty(first, "Resolution"), "RouteCompletion")));
        Assert.Equal("None", Convert.ToString(ReadRequiredProperty(
            ReadRequiredProperty(recorded, "Resolution"), "RouteCompletion")));
    }

    private static object ResolveProductionOutcomeIntent(
        OutcomeIntentCase testCase,
        string? operationSuffix = null,
        string? complicationRef = null,
        bool reverseLegacies = false,
        bool routeAlreadyCompleted = false) =>
        ResolveProductionOutcomeIntentRun(
            testCase,
            operationSuffix,
            complicationRef,
            reverseLegacies,
            routeAlreadyCompleted,
            resolveExactRetry: false).First;

    private static OutcomeResolutionRun ResolveProductionOutcomeIntentRun(
        OutcomeIntentCase testCase,
        string? operationSuffix = null,
        string? complicationRef = null,
        bool reverseLegacies = false,
        bool routeAlreadyCompleted = false,
        bool resolveExactRetry = false)
    {
        var planner = RequireOutcomeResolver();
        var scenario = CreateOutcomeIntentScenario(testCase, complicationRef, reverseLegacies);
        if (operationSuffix != null)
            scenario = scenario with { OperationKey = scenario.OperationKey + operationSuffix };
        if (routeAlreadyCompleted)
            scenario.Before["treatment"]!["completedRouteIds"] = new JsonArray(scenario.RouteId);
        var before = WoundMaterializationContract.Parse(scenario.Before.ToJsonString(), "wound");
        var history = WoundHistoryState.Parse(scenario.History.ToJsonString(), "history");
        Assert.True(before.IsValid, DescribeIssues(before.Issues));
        Assert.True(history.IsValid, DescribeIssues(history.Issues));
        Assert.Equal("active", before.Wound!.Lifecycle);

        using var fixture = AcceptedStateFixture.Create(scenario);
        if (scenario.Mode == "procedure")
            fixture.AssertUnchangedT060RequirementResolution(Assert.Single(before.Wound!.Treatment.Routes));
        var acceptedState = fixture.GetAcceptedState();
        fixture.AssertCanonicalScenarioAuthority(acceptedState, scenario);
        var factory = ExactStaticMethod(planner, testCase.Mode switch
        {
            "procedure" => "PrepareProcedureRequest",
            "guaranteed" => "PrepareGuaranteedRequest",
            _ => throw new ArgumentOutOfRangeException(nameof(testCase.Mode))
        }, 6);
        var prepared = ReadValidTypedResult(Invoke(factory, new object?[]
        {
            acceptedState, history, before.Wound!, scenario.OperationKey, scenario.RouteId,
            fixture.AcceptedEventRef(acceptedState)
        }), "Request", testCase.Name + " request");
        object? deteriorationAuthority = null;
        if (testCase.Name == "deterioration_handoff")
            deteriorationAuthority = AssertResolverFacingDeteriorationAuthority(
                acceptedState,
                ReadRequiredProperty(prepared, "Coordinates"),
                "t061_strict_deterioration");
        var resolver = ExactStaticMethod(planner, testCase.Mode == "procedure"
            ? "CreateProcedureAttempt"
            : "CreateGuaranteedAttempt", 4);
        var resolverArguments = new object?[]
        {
            prepared, history, before.Wound, acceptedState
        };
        var resolution = Invoke(resolver, resolverArguments);
        var exactRetry = resolveExactRetry
            ? Invoke(resolver, resolverArguments)
            : null;

        if (exactRetry is not null)
        {
            Assert.Equal(
                CanonicalValue(ReadRequiredProperty(resolution, "Resolution")),
                CanonicalValue(ReadRequiredProperty(exactRetry, "Resolution")));
            Assert.Equal(
                CanonicalValue(ReadRequiredProperty(
                    ReadRequiredProperty(resolution, "Resolution"),
                    "RequestAuthority")),
                CanonicalValue(ReadRequiredProperty(
                    ReadRequiredProperty(exactRetry, "Resolution"),
                    "RequestAuthority")));
        }

        if (deteriorationAuthority is not null)
        {
            var intent = OutcomeIntentAt(resolution, 0);
            Assert.Equal(
                Convert.ToString(ReadRequiredProperty(deteriorationAuthority, "AuthorityFingerprint")),
                Convert.ToString(ReadRequiredProperty(intent, "DeteriorationAuthorityFingerprint")));
        }

        return new OutcomeResolutionRun(resolution, exactRetry);
    }

    [Fact]
    public void OutcomeIntent_PublishedReceiptIsDetachedAndOwnsNoActionableSurface()
    {
        var scenario = CreateScenario(
            "procedure_normal_uses_lowest_free_die",
            "procedure");
        using var fixture = AcceptedStateFixture.Create(scenario);
        var flow = ResolveCurrentTreatment(
            fixture,
            "procedure",
            scenario.OperationKey + "_receipt",
            scenario.RouteId);
        ComposeAndPublishTreatment(fixture, flow);
        var probe = ProbePublishedTreatment(fixture, flow.Request);
        Assert.Equal("ExactReplay", Convert.ToString(ReadRequiredProperty(probe, "Status")));
        var receipt = ReadRequiredProperty(probe, "Receipt");
        AssertReceiptOwnsNoActionableOutcomeSurface(receipt.GetType());
        AssertClosedTreatmentReceipt(receipt, flow.Request, flow.Resolution);
    }

    private static Type RequireOutcomeResolver()
    {
        var planner = typeof(WoundMaterializationContract).Assembly.GetType(
            PlannerTypeName,
            throwOnError: false,
            ignoreCase: false);
        Assert.True(planner is not null,
            "T061 outcome-intent resolution is unavailable until MortalWoundTreatmentPlanner is implemented.");
        return planner!;
    }

    private static ResolverScenario CreateOutcomeIntentScenario(
        OutcomeIntentCase testCase,
        string? complicationRef = null,
        bool reverseLegacies = false)
    {
        var scenario = CreateScenario(testCase.BaselineScenario, testCase.Mode);
        var route = scenario.Before["treatment"]!["routes"]![0]!.AsObject();
        switch (testCase.Name)
        {
            case "stabilize_recovery":
                route["outcomes"]![0]!["result"] = new JsonArray(
                    new JsonObject { ["kind"] = "stabilize" },
                    new JsonObject { ["kind"] = "add_recovery", ["points"] = 2 });
                break;
            case "effectful_complication":
                route["outcomes"]![2]!["result"] = new JsonArray(new JsonObject
                {
                    ["kind"] = "add_complication",
                    ["complicationDraft"] = CreateEffectfulComplicationDraft(
                        complicationRef ?? "t061_irritation")
                });
                break;
            case "deterioration_handoff":
                scenario.Before["recovery"]!["deteriorationPolicy"] = new JsonObject
                {
                    ["policyRef"] = "t061_strict_deterioration",
                    ["unmetConditions"] = new JsonArray("not_stabilized"),
                    ["graceMinutes"] = 30L,
                    ["cadenceMinutes"] = 10L,
                    ["result"] = new JsonObject { ["kind"] = "increase_severity" }
                };
                route["outcomes"]![2]!["result"] = new JsonArray(new JsonObject
                {
                    ["kind"] = "apply_deterioration",
                    ["policyRef"] = "t061_strict_deterioration"
                });
                break;
            case "guaranteed_remove_then_heal":
                scenario.Before["complications"] = new JsonArray(new JsonObject
                {
                    ["complicationId"] = "infection_01",
                    ["kind"] = "infection",
                    ["state"] = "active",
                    ["displayName"] = "T061 infection",
                    ["treatmentDifficultyModifier"] = 1,
                    ["ownedEffectIds"] = new JsonArray(),
                    ["visibility"] = "known_to_player"
                });
                route["outcomes"]![0]!["result"] = new JsonArray(
                    new JsonObject { ["kind"] = "remove_complication", ["complicationId"] = "infection_01" },
                    new JsonObject { ["kind"] = "heal", ["legacies"] = CreateOrderedLegacyDrafts(reverseLegacies) });
                break;
        }

        return scenario;
    }

    private static object AssertResolverFacingDeteriorationAuthority(
        object acceptedState,
        object coordinates,
        string policyRef)
    {
        var authority = typeof(WoundMaterializationContract).Assembly.GetType(
            "BookOfEternityClient.Services.MortalWoundDeteriorationPolicyAuthority",
            throwOnError: false,
            ignoreCase: false);
        Assert.True(authority is not null,
            "T069 typed deterioration authority is required before a valid interruption can resolve.");
        var create = ExactStaticMethod(authority!, "Create", 3);
        Assert.Equal(acceptedState.GetType(), create.GetParameters()[0].ParameterType);
        Assert.Equal(coordinates.GetType(), create.GetParameters()[1].ParameterType);
        var result = Invoke(create, new[] { acceptedState, coordinates, (object)policyRef });
        var typed = ReadValidTypedResult(result, "Authority", "T069 resolver-facing deterioration authority");
        Assert.Equal(policyRef, Convert.ToString(ReadRequiredProperty(typed, "PolicyRef")));
        Assert.Equal("StrictlyWorsening", Convert.ToString(ReadRequiredProperty(typed, "Classification")));
        Assert.False(string.IsNullOrWhiteSpace(
            Convert.ToString(ReadRequiredProperty(typed, "AuthorityFingerprint"))));
        return typed;
    }

    private static JsonObject CreateEffectfulComplicationDraft(string complicationRef) => new()
    {
        ["complications"] = new JsonArray(new JsonObject
        {
            ["complicationRef"] = complicationRef,
            ["kind"] = "pain",
            ["state"] = "active",
            ["displayName"] = "T061 irritation",
            ["treatmentDifficultyModifier"] = 1,
            ["visibility"] = "known_to_player"
        }),
        ["consequenceDefinitions"] = new JsonArray(new JsonObject
        {
            ["definitionRef"] = complicationRef + "_definition",
            ["definition"] = CreateSourceBoundActionControlDefinition("t061-irritation"),
            ["root"] = new JsonObject
            {
                ["ownership"] = new JsonObject { ["kind"] = "complication", ["complicationRef"] = complicationRef },
                ["slots"] = new JsonArray(new JsonObject
                {
                    ["profileKey"] = "action_control",
                    ["readableSummary"] = "T061 irritation restricts one action."
                })
            }
        })
    };

    private static JsonArray CreateOrderedLegacyDrafts(bool reverse) => reverse
        ? new JsonArray(CreateMechanicalLegacyDraft(), CreateCosmeticLegacyDraft())
        : new JsonArray(CreateCosmeticLegacyDraft(), CreateMechanicalLegacyDraft());

    private static JsonObject CreateCosmeticLegacyDraft() => new()
    {
        ["localLegacyRef"] = "t061_cosmetic",
        ["kind"] = "cosmetic",
        ["readableSummary"] = "T061 cosmetic scar."
    };

    private static JsonObject CreateMechanicalLegacyDraft() =>
        CreateMechanicalLegacyDraft(
            "t061_mechanical",
            "t061_legacy_application");

    private static JsonObject CreateMechanicalLegacyDraft(
        string localLegacyRef,
        params string[] applicationRefs)
    {
        var definitionRef = localLegacyRef + "_definition";
        return new JsonObject
        {
            ["localLegacyRef"] = localLegacyRef,
            ["kind"] = "mechanical_effect",
            ["effectDraft"] = new JsonObject
            {
                ["schemaVersion"] = 1,
                ["definitions"] = new JsonArray(new JsonObject
                {
                    ["definitionRef"] = definitionRef,
                    ["definition"] = CreateSourceBoundActionControlDefinition(
                        "t061-legacy-" + localLegacyRef)
                }),
                ["applications"] = new JsonArray(applicationRefs.Select(applicationRef =>
                    (JsonNode)new JsonObject
                    {
                        ["applicationRef"] = applicationRef,
                        ["definitionRef"] = definitionRef,
                        ["parameters"] = new JsonObject()
                    }).ToArray())
            }
        };
    }

    private static JsonObject CreateSourceBoundActionControlDefinition(string definitionKey)
    {
        var definition = EffectMaterializationTestFixture.CreateDefinition("action_control");
        definition["definitionKey"] = definitionKey;
        definition["links"] = new JsonArray();
        definition["lifetime"] = new JsonObject
        {
            ["mode"] = "source_bound",
            ["activePredicate"] = "active",
            ["onSourceLoss"] = "expire"
        };
        definition["triggers"] = new JsonArray();
        return definition;
    }

    private static void AssertResolvedOutcomeIntentPair(object result, OutcomeIntentCase testCase)
    {
        AssertClosedProperties(result, new[] { "Disposition", "Issues", "Resolution", "ReplayReceipt" });
        Assert.Equal("Resolved", Convert.ToString(ReadRequiredProperty(result, "Disposition")));
        Assert.Null(ReadPropertyAllowingNull(result, "ReplayReceipt"));
        var resolution = ReadRequiredProperty(result, "Resolution");
        Assert.NotNull(resolution);
        Assert.Equal("AcceptedTerminal", Convert.ToString(ReadRequiredProperty(resolution, "AttemptDisposition")));
        Assert.Equal(testCase.Category, Convert.ToString(ReadRequiredProperty(resolution, "ResultCategory")));

        var declared = AsObjects(ReadRequiredProperty(resolution, "DeclaredResult"));
        var intents = AsObjects(ReadRequiredProperty(resolution, "OutcomeIntents"));
        Assert.Equal(testCase.Kinds, declared.Select(operation =>
            Convert.ToString(ReadRequiredProperty(operation, "Kind"))));
        Assert.Equal(testCase.Kinds, intents.Select(intent =>
            Convert.ToString(ReadRequiredProperty(intent, "Kind"))));
        Assert.Equal(declared.Length, intents.Length);
        for (var ordinal = 0; ordinal < intents.Length; ordinal++)
        {
            AssertClosedOutcomeIntent(intents[ordinal], testCase.Kinds[ordinal]);
            Assert.Equal(ordinal, Convert.ToInt32(ReadRequiredProperty(intents[ordinal], "OperationOrdinal")));
            Assert.False(string.IsNullOrWhiteSpace(
                Convert.ToString(ReadRequiredProperty(intents[ordinal], "DeclaredOperationFingerprint"))));
            Assert.False(string.IsNullOrWhiteSpace(
                Convert.ToString(ReadRequiredProperty(intents[ordinal], "IntentFingerprint"))));
        }

        Assert.Equal(
            testCase.Category == "success" ? "AppendOnce" : "None",
            Convert.ToString(ReadRequiredProperty(resolution, "RouteCompletion")));
    }

    private static object[] AsObjects(object value) =>
        Assert.IsAssignableFrom<IEnumerable>(value).Cast<object>().ToArray();

    private static string[] SelectedOutcomeKinds(WoundMaterializationEnvelope wound, OutcomeIntentCase testCase)
    {
        var route = Assert.Single(wound.Treatment.Routes);
        var selected = Assert.Single(route.Outcomes, outcome =>
            string.Equals(
                outcome.GetProperty("category").GetString(),
                testCase.Category,
                StringComparison.Ordinal));
        return selected.GetProperty("result")
            .EnumerateArray()
            .Select(static operation => operation.GetProperty("kind").GetString()!)
            .ToArray();
    }

    private static string CanonicalBindings(object intent, string property) =>
        CanonicalValue(ReadRequiredProperty(intent, property));

    private static string CanonicalValue(object? value) => JsonSerializer.Serialize(value);

    private static object OutcomeIntentAt(object resolutionResult, int ordinal)
    {
        var resolution = ReadRequiredProperty(resolutionResult, "Resolution");
        Assert.NotNull(resolution);
        var intents = AsObjects(ReadRequiredProperty(resolution, "OutcomeIntents"));
        return intents[ordinal];
    }

    private static object MechanicalLegacySeed(object healIntent) =>
        Assert.Single(
            AsObjects(ReadRequiredProperty(healIntent, "LegacySeedBindings")),
            seed => string.Equals(
                Convert.ToString(ReadRequiredProperty(seed, "Kind")),
                "mechanical_effect",
                StringComparison.Ordinal));

    private static void AssertClosedOutcomeIntent(object intent, string kind)
    {
        var common = new[] { "OperationOrdinal", "Kind", "DeclaredOperationFingerprint", "IntentFingerprint" };
        var expected = kind switch
        {
            "no_improvement" or "stabilize" => common,
            "add_recovery" => common.Append("Points").ToArray(),
            "reduce_severity" => common.Append("Steps").ToArray(),
            "remove_complication" => common.Append("ComplicationId").ToArray(),
            "apply_deterioration" => common.Append("PolicyRef").Append("DeteriorationAuthorityFingerprint").ToArray(),
            "add_complication" => common.Append("ComplicationRef").Append("ComplicationId")
                .Append("DefinitionReferenceBindings").Append("ApplicationReferenceBindings")
                .Append("PreparationFingerprint").ToArray(),
            "heal" => common.Append("HealChildCoordinates").Append("LegacySeedBindings")
                .Append("PreparationFingerprint").ToArray(),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };
        AssertClosedProperties(intent, expected);
        if (kind == "add_recovery")
            Assert.Equal(2, Convert.ToInt32(ReadRequiredProperty(intent, "Points")));
        if (kind == "reduce_severity")
            Assert.Equal(1, Convert.ToInt32(ReadRequiredProperty(intent, "Steps")));
        if (kind == "remove_complication")
            Assert.Equal("infection_01", Convert.ToString(ReadRequiredProperty(intent, "ComplicationId")));
        if (kind == "apply_deterioration")
        {
            Assert.Equal("t061_strict_deterioration", Convert.ToString(ReadRequiredProperty(intent, "PolicyRef")));
            Assert.False(string.IsNullOrWhiteSpace(
                Convert.ToString(ReadRequiredProperty(intent, "DeteriorationAuthorityFingerprint"))));
        }
        if (kind == "add_complication")
        {
            Assert.Equal("t061_irritation", Convert.ToString(ReadRequiredProperty(intent, "ComplicationRef")));
            Assert.NotNull(ReadRequiredProperty(intent, "ComplicationId"));
            Assert.NotEmpty(AsObjects(ReadRequiredProperty(intent, "DefinitionReferenceBindings")));
            Assert.NotEmpty(AsObjects(ReadRequiredProperty(intent, "ApplicationReferenceBindings")));
        }
        if (kind == "heal")
        {
            Assert.NotNull(ReadRequiredProperty(intent, "HealChildCoordinates"));
            var seeds = AsObjects(ReadRequiredProperty(intent, "LegacySeedBindings"));
            Assert.Equal(2, seeds.Length);
            Assert.Equal(new[] { "t061_cosmetic", "t061_mechanical" }, seeds.Select(seed =>
                Convert.ToString(ReadRequiredProperty(seed, "LocalLegacyRef"))));
            Assert.Collection(seeds,
                seed => AssertLegacySeed(seed, 0, "cosmetic"),
                seed => AssertLegacySeed(seed, 1, "mechanical_effect"));
        }
    }

    private static void AssertLegacySeed(object seed, int ordinal, string kind)
    {
        AssertClosedProperties(seed, new[]
        {
            "LegacyOrdinal", "LocalLegacyRef", "LegacyId", "Kind", "DefinitionReferenceBindings",
            "ApplicationReferenceBindings", "DeclaredLegacyFingerprint", "SeedFingerprint"
        });
        Assert.Equal(ordinal, Convert.ToInt32(ReadRequiredProperty(seed, "LegacyOrdinal")));
        Assert.Equal(kind, Convert.ToString(ReadRequiredProperty(seed, "Kind")));
        Assert.False(string.IsNullOrWhiteSpace(Convert.ToString(ReadRequiredProperty(seed, "LegacyId"))));
        Assert.False(string.IsNullOrWhiteSpace(Convert.ToString(ReadRequiredProperty(seed, "DeclaredLegacyFingerprint"))));
        Assert.False(string.IsNullOrWhiteSpace(Convert.ToString(ReadRequiredProperty(seed, "SeedFingerprint"))));
        if (kind == "cosmetic")
        {
            Assert.Empty(AsObjects(ReadRequiredProperty(seed, "DefinitionReferenceBindings")));
            Assert.Empty(AsObjects(ReadRequiredProperty(seed, "ApplicationReferenceBindings")));
        }
        else
        {
            Assert.NotEmpty(AsObjects(ReadRequiredProperty(seed, "DefinitionReferenceBindings")));
            Assert.NotEmpty(AsObjects(ReadRequiredProperty(seed, "ApplicationReferenceBindings")));
        }
    }

    private static void AssertReceiptOwnsNoActionableOutcomeSurface(Type receipt)
    {
        var properties = receipt.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(static property => property.GetIndexParameters().Length == 0)
            .Select(static property => property.Name)
            .ToHashSet(StringComparer.Ordinal);
        Assert.Equal(
            new[]
            {
                "Mode", "Coordinates", "AttemptDisposition", "ResultCategory", "SelectedOutcomeIndex",
                "Interruption", "DeclaredResult", "ConsumptionTrigger", "CourseId",
                "CourseMilestoneOrdinal", "CourseDisposition", "RequirementAuthorityFingerprint",
                "ResourceAuthorityFingerprint", "ModeEvidence", "RouteFingerprint",
                "ResolutionAuthorityFingerprint", "RequestFingerprint", "ResultFingerprint",
                "RouteCompletion", "ReceiptFingerprint"
            }.OrderBy(static property => property),
            properties.OrderBy(static property => property));
        Assert.DoesNotContain("OutcomeIntents", properties);
        Assert.DoesNotContain("RequirementAuthority", properties);
        Assert.DoesNotContain("ResourceAuthority", properties);
        Assert.DoesNotContain("PublicationAuthority", properties);
        Assert.DoesNotContain("HistoryIntents", properties);
    }

    private sealed record OutcomeResolutionRun(object First, object? ExactRetry);

    public sealed record OutcomeIntentCase(
        string Name,
        string BaselineScenario,
        string Mode,
        string Category,
        string[] Kinds);
}
