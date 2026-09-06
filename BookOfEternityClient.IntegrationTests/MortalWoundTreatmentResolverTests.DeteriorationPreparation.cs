using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalWoundTreatmentResolverTests
{
    private static ResolverScenario CreatePolicyPreparationScenario(
        string resultKind,
        bool effectless = false)
    {
        var scenario = CreateAdditionPublicationScenario(true);
        var result = resultKind == "add_complication"
            ? AdditionOperation("policy_preparation", effectless)
            : new JsonObject { ["kind"] = resultKind };
        scenario.Before["recovery"]!["deteriorationPolicy"] = new JsonObject
        {
            ["policyRef"] = "policy_preparation",
            ["unmetConditions"] = new JsonArray("not_stabilized"),
            ["graceMinutes"] = 30L,
            ["cadenceMinutes"] = 10L,
            ["result"] = result
        };
        scenario.Before["treatment"]!["routes"]![0]!["outcomes"]!.AsArray()
            .OfType<JsonObject>()
            .Single(row => row["category"]!.GetValue<string>() == "failed_attempt")
            ["result"] = new JsonArray(new JsonObject
            {
                ["kind"] = "apply_deterioration",
                ["policyRef"] = "policy_preparation"
            });
        return PrepareProcedurePublicationScenario(scenario with
        {
            OperationKey = "operation_t070_policy_preparation",
            ExpectedIntentCount = 1
        });
    }

    private static object RequirePrivatePolicyPreparation(TreatmentFlow flow)
    {
        var resolution = Assert.IsType<MortalWoundTreatmentResolution>(flow.Resolution);
        var intent = Assert.IsType<MortalWoundApplyDeteriorationOutcomeIntent>(
            Assert.Single(resolution.OutcomeIntents));
        var method = intent.GetType().GetMethod(
            "TryGetPreparedDeterioration",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.True(
            method is not null,
            "T067 must carry an independently checked private policy preparation.");
        object?[] args = { flow.Request, Assert.Single(resolution.DeclaredResult), null };
        Assert.True((bool)method!.Invoke(intent, args)!);
        return Assert.IsAssignableFrom<object>(args[2]);
    }

    private static object ReadPolicyMember(object value, string name)
    {
        var property = value.GetType().GetProperty(
            name,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(property);
        return Assert.IsAssignableFrom<object>(property!.GetValue(value));
    }

    private static MortalWoundTreatmentDeteriorationPreparation PreparedPolicyAt(
        TreatmentFlow flow,
        int operationOrdinal)
    {
        var request = Assert.IsType<MortalWoundTreatmentAttemptRequest>(flow.Request);
        var resolution = Assert.IsType<MortalWoundTreatmentResolution>(flow.Resolution);
        var operation = Assert.IsType<MortalWoundApplyDeteriorationOperation>(
            resolution.DeclaredResult[operationOrdinal]);
        var intent = Assert.IsType<MortalWoundApplyDeteriorationOutcomeIntent>(
            resolution.OutcomeIntents[operationOrdinal]);
        Assert.True(intent.TryGetPreparedDeterioration(request, operation, out var prepared));
        return Assert.IsType<MortalWoundTreatmentDeteriorationPreparation>(prepared);
    }

    private static T ClonePolicyField<T>(T source, string fieldName, object? value)
        where T : class
    {
        var clone = (T)typeof(object)
            .GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(source, null)!;
        SetPolicyField(clone, fieldName, value);
        return clone;
    }

    private static void SetPolicyField(object target, string fieldName, object? value)
    {
        FieldInfo? field = null;
        for (Type? type = target.GetType(); type is not null && field is null; type = type.BaseType)
        {
            field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        }
        Assert.NotNull(field);
        field!.SetValue(target, value);
    }

    private static ResolverScenario CreateMultiRootPolicyPreparationScenario()
    {
        var scenario = CreatePolicyPreparationScenario("add_complication");
        var draft = scenario.Before["recovery"]!["deteriorationPolicy"]!
            ["result"]!["complicationDraft"]!;
        var original = draft["consequenceDefinitions"]![0]!.DeepClone();
        var definitions = WoundContractTestData
            .CreateRootBoundReactionComplicationDefinitions("independent");
        definitions[1]!["root"] = null;
        definitions[0]!["root"]!["ownership"]!["complicationRef"] =
            "policy_preparation";
        definitions[0]!["root"]!["slots"]!.AsArray().Add(new JsonObject
        {
            ["profileKey"] = "roll_modifier",
            ["readableSummary"] = "Reachable child definition slot."
        });
        definitions[1]!["definition"]!["components"] =
            EffectMaterializationTestFixture.CreateDefinition("roll_modifier")
                ["components"]!.DeepClone();
        definitions[1]!["definition"]!["components"]![0]!["payload"] =
            new JsonObject
            {
                ["operations"] = new JsonArray("skill_check"),
                ["contribution"] = "disadvantage",
                ["scope"] = new JsonObject
                {
                    ["kind"] = "skill",
                    ["skillId"] = "skill_field_medicine_01"
                }
            };
        definitions[1]!["definition"]!["triggers"] = new JsonArray();
        original!["definition"]!["components"]![0]!["payload"]!["action"] =
            "use_item";
        definitions.Add(original);
        draft["consequenceDefinitions"] = definitions;
        scenario.Before["severity"]!["rank"] = 4;
        scenario.Before["severity"]!["value"] = "IV";
        scenario.Before["consequences"]!["slotBudget"] = 4;
        return PrepareProcedurePublicationScenario(scenario with
        {
            OperationKey = "operation_t070_policy_preparation_multi"
        });
    }

    [Theory]
    [InlineData("increase_severity", false)]
    [InlineData("add_complication", false)]
    [InlineData("add_complication", true)]
    [InlineData("death_contour", false)]
    public void PolicyPreparation_ProductionIntentCarriesExactDetachedPolicy(
        string kind,
        bool effectless)
    {
        var scenario = CreatePolicyPreparationScenario(kind, effectless);
        using var fixture = AcceptedStateFixture.Create(scenario);
        var flow = ResolveCurrentTreatment(
            fixture,
            "procedure",
            scenario.OperationKey,
            scenario.RouteId);
        var prepared = RequirePrivatePolicyPreparation(flow);
        Assert.Equal("policy_preparation", ReadPolicyMember(prepared, "PolicyRef"));
        Assert.Equal(0, ReadPolicyMember(prepared, "OperationOrdinal"));
        Assert.Equal(
            ReadRequiredProperty(flow.Request, "RequestFingerprint"),
            ReadPolicyMember(prepared, "RequestFingerprint"));

        var request = Assert.IsType<MortalWoundTreatmentAttemptRequest>(flow.Request);
        var acceptedState = Assert.IsType<MortalWoundTreatmentAcceptedStateAuthority>(
            flow.AcceptedState);
        var resolution = Assert.IsType<MortalWoundTreatmentResolution>(flow.Resolution);
        var operation = Assert.IsType<MortalWoundApplyDeteriorationOperation>(
            Assert.Single(resolution.DeclaredResult));
        var intent = Assert.IsType<MortalWoundApplyDeteriorationOutcomeIntent>(
            Assert.Single(resolution.OutcomeIntents));
        Assert.True(intent.TryGetPreparedDeterioration(request, operation, out var typed));
        var authority = MortalWoundDeteriorationPolicyAuthority.Create(
            acceptedState,
            request.Coordinates,
            operation.PolicyRef);
        Assert.True(authority.IsValid);
        Assert.Equal(
            authority.Authority!.AuthorityFingerprint,
            typed!.DeteriorationAuthorityFingerprint);
        Assert.Equal(
            authority.Authority.Policy.CanonicalProjection,
            typed.Policy.CanonicalProjection);
        Assert.Equal(
            MortalWoundTreatmentOutcomeIntentComposer.DeclaredFingerprint(0, operation),
            typed.DeclaredOperationFingerprint);
        Assert.Equal(intent.IntentFingerprint, typed.IntentFingerprint);
        if (typed.Draft is { } draft)
        {
            var expected = MortalWoundTreatmentOutcomeIntentComposer.PrepareComplicationBindings(
                request.RequestFingerprint,
                0,
                draft,
                typed.DeclaredOperationFingerprint);
            var actual = Assert.IsType<MortalWoundTreatmentComplicationBindingPreparation>(
                typed.ComplicationBinding);
            Assert.Equal(expected.ComplicationId, actual.ComplicationId);
            Assert.Equal(expected.PreparationFingerprint, actual.PreparationFingerprint);
            Assert.Equal(
                expected.DefinitionReferenceBindings
                    .Select(row => (row.LocalRef, row.NamespacedRef))
                    .ToArray(),
                actual.DefinitionReferenceBindings
                    .Select(row => (row.LocalRef, row.NamespacedRef))
                    .ToArray());
            Assert.Equal(
                expected.ApplicationReferenceBindings
                    .Select(row => (row.LocalRef, row.NamespacedRef))
                    .ToArray(),
                actual.ApplicationReferenceBindings
                    .Select(row => (row.LocalRef, row.NamespacedRef))
                    .ToArray());
            Assert.Equal(expected.Roots.ToArray(), actual.Roots.ToArray());
        }
        else
        {
            Assert.Null(typed.ComplicationBinding);
        }

        Assert.False(MortalWoundApplyDeteriorationOutcomeIntent.Create(
                intent.OperationOrdinal,
                intent.DeclaredOperationFingerprint,
                intent.IntentFingerprint,
                intent.PolicyRef,
                intent.DeteriorationAuthorityFingerprint)
            .TryGetPreparedDeterioration(request, operation, out _));
    }

    [Fact]
    public void PolicyPreparation_ExactRetryAndPersistedRehydrationKeepPrivatePacket()
    {
        var scenario = CreatePolicyPreparationScenario("add_complication");
        using var fixture = AcceptedStateFixture.Create(scenario);
        var initial = ResolveCurrentTreatment(
            fixture,
            "procedure",
            scenario.OperationKey,
            scenario.RouteId);
        var retry = ResolveCurrentTreatment(
            fixture,
            "procedure",
            scenario.OperationKey,
            scenario.RouteId);
        var original = PreparedPolicyAt(initial, 0);
        var retried = PreparedPolicyAt(retry, 0);
        Assert.Equal(original.Fingerprint, retried.Fingerprint);
        Assert.Equal(CanonicalValue(initial.Resolution), CanonicalValue(retry.Resolution));

        var carrier = ReadCanonicalBytes(fixture, fixture.TargetCarrierPath);
        var identity = ReadCanonicalBytes(fixture, WoundIdentityState.StatePath);
        var history = ReadCanonicalBytes(fixture, WoundHistoryState.HistoryPath);
        var restored = PersistAndRehydrateTreatmentPublication(
            fixture,
            initial,
            "policy preparation");
        var rehydrated = PreparedPolicyAt(restored, 0);
        Assert.Equal(original.Fingerprint, rehydrated.Fingerprint);
        Assert.Equal(CanonicalValue(initial.Resolution), CanonicalValue(restored.Resolution));
        Assert.Equal(carrier, ReadCanonicalBytes(fixture, fixture.TargetCarrierPath));
        Assert.Equal(identity, ReadCanonicalBytes(fixture, WoundIdentityState.StatePath));
        Assert.Equal(history, ReadCanonicalBytes(fixture, WoundHistoryState.HistoryPath));
    }

    [Fact]
    public void PolicyPreparation_RepeatedEffectlessPolicyKeepsDistinctOrdinalBindings()
    {
        var repeated = CreatePolicyPreparationScenario("add_complication", true);
        repeated.Before["treatment"]!["routes"]![0]!["outcomes"]!.AsArray()
            .OfType<JsonObject>()
            .Single(row => row["category"]!.GetValue<string>() == "failed_attempt")
            ["result"] = new JsonArray(
                new JsonObject
                {
                    ["kind"] = "apply_deterioration",
                    ["policyRef"] = "policy_preparation"
                },
                new JsonObject
                {
                    ["kind"] = "apply_deterioration",
                    ["policyRef"] = "policy_preparation"
                });
        repeated = PrepareProcedurePublicationScenario(repeated with
        {
            OperationKey = "operation_t070_policy_preparation_repeat",
            ExpectedIntentCount = 2
        });
        using (var fixture = AcceptedStateFixture.Create(repeated))
        {
            var flow = ResolveCurrentTreatment(
                fixture,
                "procedure",
                repeated.OperationKey,
                repeated.RouteId);
            var first = PreparedPolicyAt(flow, 0);
            var second = PreparedPolicyAt(flow, 1);
            Assert.Equal(0, first.OperationOrdinal);
            Assert.Equal(1, second.OperationOrdinal);
            Assert.NotEqual(
                first.ComplicationBinding!.ComplicationId,
                second.ComplicationBinding!.ComplicationId);
            Assert.NotEqual(
                first.ComplicationBinding.PreparationFingerprint,
                second.ComplicationBinding.PreparationFingerprint);
            Assert.Empty(first.ComplicationBinding.DefinitionReferenceBindings);
            Assert.Empty(first.ComplicationBinding.ApplicationReferenceBindings);
            Assert.Empty(first.ComplicationBinding.Roots);
            Assert.Empty(second.ComplicationBinding.DefinitionReferenceBindings);
            Assert.Empty(second.ComplicationBinding.ApplicationReferenceBindings);
            Assert.Empty(second.ComplicationBinding.Roots);
        }

        var mixed = CreatePolicyPreparationScenario("add_complication", true);
        mixed.Before["treatment"]!["routes"]![0]!["outcomes"]!.AsArray()
            .OfType<JsonObject>()
            .Single(row => row["category"]!.GetValue<string>() == "failed_attempt")
            ["result"] = new JsonArray(
                AdditionOperation("policy_preparation", true),
                new JsonObject
                {
                    ["kind"] = "apply_deterioration",
                    ["policyRef"] = "policy_preparation"
                });
        mixed = PrepareProcedurePublicationScenario(mixed with
        {
            OperationKey = "operation_t070_direct_policy_same_local",
            ExpectedIntentCount = 2
        });
        using (var fixture = AcceptedStateFixture.Create(mixed))
        {
            var flow = ResolveCurrentTreatment(
                fixture,
                "procedure",
                mixed.OperationKey,
                mixed.RouteId);
            var resolution = Assert.IsType<MortalWoundTreatmentResolution>(flow.Resolution);
            Assert.IsType<MortalWoundAddComplicationOutcomeIntent>(resolution.OutcomeIntents[0]);
            Assert.Equal(1, PreparedPolicyAt(flow, 1).OperationOrdinal);
        }
    }

    [Fact]
    public void PolicyPreparation_BorrowedRequestAndChangedPolicyReject()
    {
        var scenario = CreatePolicyPreparationScenario("increase_severity");
        using var fixture = AcceptedStateFixture.Create(scenario);
        var original = ResolveCurrentTreatment(
            fixture,
            "procedure",
            scenario.OperationKey,
            scenario.RouteId);
        var originalRequest = Assert.IsType<MortalWoundTreatmentAttemptRequest>(original.Request);
        var originalResolution = Assert.IsType<MortalWoundTreatmentResolution>(original.Resolution);
        var originalOperation = Assert.IsType<MortalWoundApplyDeteriorationOperation>(
            Assert.Single(originalResolution.DeclaredResult));
        var originalIntent = Assert.IsType<MortalWoundApplyDeteriorationOutcomeIntent>(
            Assert.Single(originalResolution.OutcomeIntents));
        var originalPrepared = PreparedPolicyAt(original, 0);

        var borrowedScenario = CreatePolicyPreparationScenario("increase_severity") with
        {
            OperationKey = "operation_t070_policy_preparation_borrowed"
        };
        using var borrowedFixture = AcceptedStateFixture.Create(borrowedScenario);
        var borrowed = ResolveCurrentTreatment(
            borrowedFixture,
            "procedure",
            borrowedScenario.OperationKey,
            borrowedScenario.RouteId);
        var borrowedRequest = Assert.IsType<MortalWoundTreatmentAttemptRequest>(borrowed.Request);
        Assert.False(originalIntent.TryGetPreparedDeterioration(
            borrowedRequest,
            originalOperation,
            out _));
        Assert.False(originalIntent.TryGetPreparedDeterioration(
            originalRequest,
            new MortalWoundApplyDeteriorationOperation("changed_policy"),
            out _));

        foreach (var axis in new[] { "grace", "cadence", "result" })
        {
            var changedScenario = CreatePolicyPreparationScenario("increase_severity");
            var policy = changedScenario.Before["recovery"]!["deteriorationPolicy"]!;
            if (axis == "grace")
            {
                policy["graceMinutes"] = 31L;
            }
            else if (axis == "cadence")
            {
                policy["cadenceMinutes"] = 11L;
            }
            else
            {
                policy["result"] = AdditionOperation("changed_policy_body", true);
            }
            changedScenario = PrepareProcedurePublicationScenario(changedScenario with
            {
                OperationKey = "operation_t070_policy_preparation_changed_" + axis
            });
            using var changedFixture = AcceptedStateFixture.Create(changedScenario);
            var changed = ResolveCurrentTreatment(
                changedFixture,
                "procedure",
                changedScenario.OperationKey,
                changedScenario.RouteId);
            var changedPrepared = PreparedPolicyAt(changed, 0);
            Assert.False(changedPrepared.AgreesWith(
                originalRequest,
                originalOperation,
                originalIntent));
            Assert.NotEqual(originalPrepared.Fingerprint, changedPrepared.Fingerprint);
        }
    }

    [Fact]
    public void PolicyPreparation_PrivatePayloadTamperRejects()
    {
        var scenario = CreateMultiRootPolicyPreparationScenario();
        using var fixture = AcceptedStateFixture.Create(scenario);
        var flow = ResolveCurrentTreatment(
            fixture,
            "procedure",
            scenario.OperationKey,
            scenario.RouteId);
        var request = Assert.IsType<MortalWoundTreatmentAttemptRequest>(flow.Request);
        var resolution = Assert.IsType<MortalWoundTreatmentResolution>(flow.Resolution);
        var operation = Assert.IsType<MortalWoundApplyDeteriorationOperation>(
            Assert.Single(resolution.DeclaredResult));
        var intent = Assert.IsType<MortalWoundApplyDeteriorationOutcomeIntent>(
            Assert.Single(resolution.OutcomeIntents));
        var original = PreparedPolicyAt(flow, 0);
        var draft = Assert.IsType<MortalWoundComplicationProposalDraft>(original.Draft);
        var binding = Assert.IsType<MortalWoundTreatmentComplicationBindingPreparation>(
            original.ComplicationBinding);
        Assert.True(binding.DefinitionReferenceBindings.Length > 1);
        Assert.True(binding.ApplicationReferenceBindings.Length > 1);
        Assert.True(binding.Roots.Length > 1);
        Assert.Contains(draft.ConsequenceDefinitions, static row => row.Root is null);
        Assert.Equal(
            draft.ConsequenceDefinitions.Select(static row => row.DefinitionRef),
            binding.DefinitionReferenceBindings.Select(static row => row.LocalRef));
        Assert.Equal(
            draft.ConsequenceDefinitions
                .Where(static row => row.Root is not null)
                .Select(static row => row.DefinitionRef + "_application"),
            binding.ApplicationReferenceBindings.Select(static row => row.LocalRef));

        var changedPolicyKind = original.Policy with
        {
            ResultKind = MortalWoundDeteriorationResultKind.IncreaseSeverity
        };
        var changedPolicyResult = original.Policy with
        {
            Result = JsonSerializer.SerializeToElement(new { kind = "death_contour" })
        };
        var changedPolicyProjection = original.Policy with
        {
            CanonicalProjection = original.Policy.CanonicalProjection + "x"
        };
        var changedDraft = draft with
        {
            Complication = draft.Complication with { DisplayName = "forged" }
        };
        var changedComplicationId = MortalWoundTreatmentComplicationBindingPreparation.Create(
            binding.ComplicationRef,
            binding.ComplicationId + "x",
            binding.DefinitionReferenceBindings,
            binding.ApplicationReferenceBindings,
            binding.PreparationFingerprint,
            binding.Roots);
        var reversedDefinitions = MortalWoundTreatmentComplicationBindingPreparation.Create(
            binding.ComplicationRef,
            binding.ComplicationId,
            binding.DefinitionReferenceBindings.Reverse(),
            binding.ApplicationReferenceBindings,
            binding.PreparationFingerprint,
            binding.Roots);
        var reversedApplications = MortalWoundTreatmentComplicationBindingPreparation.Create(
            binding.ComplicationRef,
            binding.ComplicationId,
            binding.DefinitionReferenceBindings,
            binding.ApplicationReferenceBindings.Reverse(),
            binding.PreparationFingerprint,
            binding.Roots);
        var changedRoots = MortalWoundTreatmentComplicationBindingPreparation.Create(
            binding.ComplicationRef,
            binding.ComplicationId,
            binding.DefinitionReferenceBindings,
            binding.ApplicationReferenceBindings,
            binding.PreparationFingerprint,
            binding.Roots.Select((row, index) => index == 0
                ? row with { OperationKey = row.OperationKey + "x" }
                : row));
        var defaultDefinitions = ClonePolicyField(
            binding,
            "<DefinitionReferenceBindings>k__BackingField",
            default(ImmutableArray<MortalWoundTreatmentReferenceBinding>));
        var nullDefinition = ClonePolicyField(
            binding,
            "<DefinitionReferenceBindings>k__BackingField",
            ImmutableArray.CreateRange(
                new MortalWoundTreatmentReferenceBinding[] { null! }));
        var defaultApplications = ClonePolicyField(
            binding,
            "<ApplicationReferenceBindings>k__BackingField",
            default(ImmutableArray<MortalWoundTreatmentReferenceBinding>));
        var nullApplication = ClonePolicyField(
            binding,
            "<ApplicationReferenceBindings>k__BackingField",
            ImmutableArray.CreateRange(
                new MortalWoundTreatmentReferenceBinding[] { null! }));
        var defaultRoots = ClonePolicyField(
            binding,
            "<Roots>k__BackingField",
            default(ImmutableArray<MortalWoundTreatmentComplicationRootBinding>));
        var nullRoot = ClonePolicyField(
            binding,
            "<Roots>k__BackingField",
            ImmutableArray.CreateRange(
                new MortalWoundTreatmentComplicationRootBinding[] { null! }));
        var defaultDraft = draft with
        {
            ConsequenceDefinitions = default
        };
        var defaultPolicyResult = original.Policy with
        {
            Result = default
        };

        var tampered = new[]
        {
            ClonePolicyField(original, "_policy", changedPolicyKind),
            ClonePolicyField(original, "_policy", changedPolicyResult),
            ClonePolicyField(original, "_policy", changedPolicyProjection),
            ClonePolicyField(original, "_policy", defaultPolicyResult),
            ClonePolicyField(original, "_policy", null),
            ClonePolicyField(original, "_draft", changedDraft),
            ClonePolicyField(original, "_draft", defaultDraft),
            ClonePolicyField(original, "_draft", null),
            ClonePolicyField(original, "_binding", changedComplicationId),
            ClonePolicyField(original, "_binding", reversedDefinitions),
            ClonePolicyField(original, "_binding", reversedApplications),
            ClonePolicyField(original, "_binding", changedRoots),
            ClonePolicyField(original, "_binding", defaultDefinitions),
            ClonePolicyField(original, "_binding", nullDefinition),
            ClonePolicyField(original, "_binding", defaultApplications),
            ClonePolicyField(original, "_binding", nullApplication),
            ClonePolicyField(original, "_binding", defaultRoots),
            ClonePolicyField(original, "_binding", nullRoot),
            ClonePolicyField(original, "_binding", null),
            ClonePolicyField(original, "_requestFingerprint", null),
            ClonePolicyField(original, "_coordinatesFingerprint", null),
            ClonePolicyField(original, "_beforeCanonical", null),
            ClonePolicyField(original, "_authorityFingerprint", null),
            ClonePolicyField(original, "_declaredFingerprint", null),
            ClonePolicyField(original, "_intentFingerprint", null),
            ClonePolicyField(original, "_woundSourcePath", null),
            ClonePolicyField(original, "_ordinal", -1),
            ClonePolicyField(original, "_fingerprint", original.Fingerprint + "x"),
            ClonePolicyField(original, "_fingerprint", null)
        };

        foreach (var forged in tampered)
        {
            var exception = Record.Exception(() =>
                Assert.False(forged.AgreesWith(request, operation, intent)));
            Assert.Null(exception);
        }
    }

    [Fact]
    public void PolicyPreparation_ReturnedCopiesCannotChangeTheAttachedPacket()
    {
        var scenario = CreateMultiRootPolicyPreparationScenario();
        using var fixture = AcceptedStateFixture.Create(scenario);
        var flow = ResolveCurrentTreatment(
            fixture,
            "procedure",
            scenario.OperationKey,
            scenario.RouteId);
        var request = Assert.IsType<MortalWoundTreatmentAttemptRequest>(flow.Request);
        var resolution = Assert.IsType<MortalWoundTreatmentResolution>(flow.Resolution);
        var operation = Assert.IsType<MortalWoundApplyDeteriorationOperation>(
            Assert.Single(resolution.DeclaredResult));
        var intent = Assert.IsType<MortalWoundApplyDeteriorationOutcomeIntent>(
            Assert.Single(resolution.OutcomeIntents));
        var original = PreparedPolicyAt(flow, 0);
        var originalFingerprint = original.Fingerprint;
        var originalPolicy = original.Policy.CanonicalProjection;
        var originalDraft = CanonicalValue(original.Draft);
        var originalBinding = original.ComplicationBinding!;

        SetPolicyField(
            original,
            "_policy",
            original.Policy with { GraceMinutes = original.Policy.GraceMinutes + 1 });
        var forgedPacketCopy = original.DetachedCopy();
        Assert.Equal(originalFingerprint, forgedPacketCopy.Fingerprint);
        Assert.False(original.AgreesWith(request, operation, intent));
        Assert.False(forgedPacketCopy.AgreesWith(request, operation, intent));
        var returnedDraft = original.Draft!;
        SetPolicyField(
            returnedDraft,
            "<Complication>k__BackingField",
            returnedDraft.Complication with { DisplayName = "forged" });
        var returnedBinding = original.ComplicationBinding!;
        SetPolicyField(
            returnedBinding,
            "<ComplicationId>k__BackingField",
            returnedBinding.ComplicationId + "x");
        Assert.Equal("forged", returnedDraft.Complication.DisplayName);
        Assert.EndsWith("x", returnedBinding.ComplicationId, StringComparison.Ordinal);

        var fresh = PreparedPolicyAt(flow, 0);
        Assert.Equal(originalFingerprint, fresh.Fingerprint);
        Assert.Equal(originalPolicy, fresh.Policy.CanonicalProjection);
        Assert.Equal(originalDraft, CanonicalValue(fresh.Draft));
        Assert.Equal(originalBinding.ComplicationId, fresh.ComplicationBinding!.ComplicationId);
        Assert.True(fresh.AgreesWith(request, operation, intent));

        var forgedIntents = new MortalWoundApplyDeteriorationOutcomeIntent[]
        {
            ClonePolicyField(intent, "<OperationOrdinal>k__BackingField", 1),
            ClonePolicyField(intent, "<Kind>k__BackingField", "forged_kind"),
            ClonePolicyField(
                intent,
                "<DeclaredOperationFingerprint>k__BackingField",
                intent.DeclaredOperationFingerprint + "x"),
            ClonePolicyField(
                intent,
                "<IntentFingerprint>k__BackingField",
                intent.IntentFingerprint + "x"),
            ClonePolicyField(intent, "<PolicyRef>k__BackingField", "forged_policy"),
            ClonePolicyField(
                intent,
                "<DeteriorationAuthorityFingerprint>k__BackingField",
                intent.DeteriorationAuthorityFingerprint + "x")
        };
        foreach (var forgedIntent in forgedIntents)
        {
            var copiedIntent = forgedIntent.DetachedCopy();
            Assert.Equal(forgedIntent.OperationOrdinal, copiedIntent.OperationOrdinal);
            Assert.Equal(forgedIntent.Kind, copiedIntent.Kind);
            Assert.Equal(
                forgedIntent.DeclaredOperationFingerprint,
                copiedIntent.DeclaredOperationFingerprint);
            Assert.Equal(forgedIntent.IntentFingerprint, copiedIntent.IntentFingerprint);
            Assert.Equal(forgedIntent.PolicyRef, copiedIntent.PolicyRef);
            Assert.Equal(
                forgedIntent.DeteriorationAuthorityFingerprint,
                copiedIntent.DeteriorationAuthorityFingerprint);
            Assert.False(copiedIntent.TryGetPreparedDeterioration(
                request,
                operation,
                out _));
        }
    }

    [Fact]
    public void PolicyPreparation_PublicShapeAndPublicationBoundaryRemainClosed()
    {
        var scenario = CreatePolicyPreparationScenario("death_contour");
        using var fixture = AcceptedStateFixture.Create(scenario);
        var flow = ResolveCurrentTreatment(
            fixture,
            "procedure",
            scenario.OperationKey,
            scenario.RouteId);
        var request = Assert.IsType<MortalWoundTreatmentAttemptRequest>(flow.Request);
        var accepted = Assert.IsType<MortalWoundTreatmentAcceptedStateAuthority>(flow.AcceptedState);
        var resolution = Assert.IsType<MortalWoundTreatmentResolution>(flow.Resolution);
        var operation = Assert.IsType<MortalWoundApplyDeteriorationOperation>(
            Assert.Single(resolution.DeclaredResult));
        var intent = Assert.IsType<MortalWoundApplyDeteriorationOutcomeIntent>(
            Assert.Single(resolution.OutcomeIntents));
        var authority = MortalWoundDeteriorationPolicyAuthority.Create(
            accepted,
            request.Coordinates,
            operation.PolicyRef);
        Assert.True(authority.IsValid);
        AssertClosedProperties(intent, new[]
        {
            "OperationOrdinal",
            "Kind",
            "DeclaredOperationFingerprint",
            "IntentFingerprint",
            "PolicyRef",
            "DeteriorationAuthorityFingerprint"
        });
        var ordinal = intent.OperationOrdinal.ToString(
            System.Globalization.CultureInfo.InvariantCulture);
        var baseIntent = WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
        {
            "book_of_eternity.mortal_wound_treatment.outcome_intent",
            "1",
            request.RequestFingerprint,
            ordinal,
            "apply_deterioration",
            intent.DeclaredOperationFingerprint,
            operation.PolicyRef
        });
        Assert.Equal(WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
        {
            "book_of_eternity.mortal_wound_treatment.deterioration_intent",
            "1",
            baseIntent,
            operation.PolicyRef,
            authority.Authority!.AuthorityFingerprint
        }), intent.IntentFingerprint);
        var json = JsonSerializer.Serialize(intent);
        Assert.DoesNotContain("prepared", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ComplicationBinding", json, StringComparison.Ordinal);

        var tree = CaptureResolverFixtureTree(fixture.Root);
        var publication = MortalWoundTreatmentOutcomePublicationPlanner.Prepare(
            accepted,
            request,
            resolution,
            accepted.CurrentGameMinute);
        Assert.False(publication.IsValid);
        Assert.Null(publication.Preparation);
        Assert.Contains(publication.Issues, static issue =>
            issue.Code == "mortal_wound_treatment_publication_slice_unsupported");
        AssertResolverFixtureTreeUnchanged(fixture.Root, tree);
    }

    [Fact]
    public void PolicyPreparation_PartialAndInterruptedCourseKeepPolicyPreparation()
    {
        var partial = CreateRecoveryPublicationScenario(
            "procedure",
            "partial_success",
            "a1");
        partial.Before["recovery"]!["deteriorationPolicy"] = new JsonObject
        {
            ["policyRef"] = "policy_preparation",
            ["unmetConditions"] = new JsonArray("not_stabilized"),
            ["graceMinutes"] = 30L,
            ["cadenceMinutes"] = 10L,
            ["result"] = new JsonObject { ["kind"] = "increase_severity" }
        };
        partial.Before["treatment"]!["routes"]![0]!["outcomes"]!.AsArray()
            .OfType<JsonObject>()
            .Single(row => row["category"]!.GetValue<string>() == "partial_success")
            ["result"]!.AsArray()
            .Add(new JsonObject
            {
                ["kind"] = "apply_deterioration",
                ["policyRef"] = "policy_preparation"
            });
        partial = PrepareProcedurePublicationScenario(partial with
        {
            OperationKey = "operation_t070_policy_preparation_partial",
            ExpectedIntentCount = 2
        });
        using (var fixture = AcceptedStateFixture.Create(partial))
        {
            var flow = ResolveCurrentTreatment(
                fixture,
                "procedure",
                partial.OperationKey,
                partial.RouteId);
            Assert.Equal(1, PreparedPolicyAt(flow, 1).OperationOrdinal);
        }

        var course = CreateScalarCoursePublicationScenario();
        course.Before["recovery"]!["deteriorationPolicy"] = new JsonObject
        {
            ["policyRef"] = "policy_preparation",
            ["unmetConditions"] = new JsonArray("not_stabilized"),
            ["graceMinutes"] = 30L,
            ["cadenceMinutes"] = 10L,
            ["result"] = new JsonObject { ["kind"] = "increase_severity" }
        };
        course.Before["treatment"]!["routes"]![0]!["interruption"]!["result"] =
            new JsonArray(new JsonObject
            {
                ["kind"] = "apply_deterioration",
                ["policyRef"] = "policy_preparation"
            });
        course = PrepareProcedurePublicationScenario(course);
        using var courseFixture = AcceptedStateFixture.Create(course);
        var initial = ResolveCurrentTreatment(
            courseFixture,
            "course",
            course.OperationKey,
            course.RouteId);
        ComposeAndPublishTreatment(courseFixture, initial);
        courseFixture.PrepareNextTurn(43, 1_081, "policy_preparation_interruption");
        var interrupted = ResolveCurrentTreatment(
            courseFixture,
            "course",
            course.OperationKey + "_interrupt",
            course.RouteId);
        var interruptedResolution = Assert.IsType<MortalWoundTreatmentResolution>(
            interrupted.Resolution);
        Assert.True(interruptedResolution.Interruption);
        Assert.Equal("interrupted", interruptedResolution.CourseDisposition);
        _ = RequirePrivatePolicyPreparation(interrupted);
    }
}
