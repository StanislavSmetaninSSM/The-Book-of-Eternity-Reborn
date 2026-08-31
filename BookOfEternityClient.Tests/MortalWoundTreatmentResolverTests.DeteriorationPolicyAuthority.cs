using System.Reflection;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalWoundTreatmentResolverTests
{
    [Fact]
    public void DeteriorationPolicyAuthority_ExposesOnlyTwoProductionFactories()
    {
        var type = RequireDeteriorationPolicyAuthority();
        var creates = type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(static method => method.Name == "Create")
            .ToArray();

        Assert.Equal(2, creates.Length);
        Assert.Contains(creates, method => method.GetParameters().Select(static parameter => parameter.ParameterType)
            .SequenceEqual(new[]
            {
                typeof(FileSystemManager),
                typeof(FileSystemManager.CanonicalWriteLease),
                typeof(WoundAcceptedTurnBinding),
                typeof(string),
                typeof(string)
            }));
        Assert.Contains(creates, method => method.GetParameters().Select(static parameter => parameter.ParameterType)
            .SequenceEqual(new[]
            {
                typeof(MortalWoundTreatmentAcceptedStateAuthority),
                typeof(MortalWoundTreatmentAttemptCoordinates),
                typeof(string)
            }));
        Assert.All(creates, method => Assert.DoesNotContain(method.GetParameters(), parameter =>
            parameter.Name!.Contains("fingerprint", StringComparison.OrdinalIgnoreCase) ||
            parameter.ParameterType == typeof(JsonNode)));
    }

    [Fact]
    public void DeteriorationPolicyAuthority_ResolverFactoryBindsCurrentProductionCoordinates()
    {
        var scenario = DeteriorationScenario(
            new JsonObject { ["kind"] = "increase_severity" });
        using var fixture = AcceptedStateFixture.Create(scenario);
        var acceptedState = Assert.IsType<MortalWoundTreatmentAcceptedStateAuthority>(
            fixture.GetAcceptedState());
        var coordinates = CreateDeteriorationCoordinates(
            fixture,
            acceptedState,
            scenario,
            scenario.OperationKey);
        var create = DeteriorationFactory(3);

        var first = ValidDeteriorationAuthority(Invoke(create, new object?[]
        {
            acceptedState,
            coordinates,
            "untreated_infection"
        }));
        var retry = ValidDeteriorationAuthority(Invoke(create, new object?[]
        {
            acceptedState,
            coordinates,
            "untreated_infection"
        }));
        Assert.Equal(
            ReadRequiredProperty(first, "AuthorityFingerprint"),
            ReadRequiredProperty(retry, "AuthorityFingerprint"));
        Assert.Equal("StrictlyWorsening", Convert.ToString(
            ReadRequiredProperty(first, "Classification")));
        Assert.Equal("untreated_infection", ReadRequiredProperty(first, "PolicyRef"));
        AssertClosedProperties(first, new[]
        {
            "PolicyRef", "Classification", "AuthorityFingerprint"
        });

        var otherCoordinates = CreateDeteriorationCoordinates(
            fixture,
            acceptedState,
            scenario,
            scenario.OperationKey + "_second");
        var other = ValidDeteriorationAuthority(Invoke(create, new object?[]
        {
            acceptedState,
            otherCoordinates,
            "untreated_infection"
        }));
        Assert.NotEqual(
            ReadRequiredProperty(first, "AuthorityFingerprint"),
            ReadRequiredProperty(other, "AuthorityFingerprint"));

        AssertInvalidDeteriorationAuthority(
            Invoke(create, new object?[]
            {
                acceptedState,
                coordinates,
                "another_policy"
            }),
            "mortal_wound_deterioration_policy_reference_mismatch");

        fixture.InvalidateDeteriorationAcceptedState();
        AssertInvalidDeteriorationAuthority(
            Invoke(create, new object?[]
            {
                acceptedState,
                coordinates,
                "untreated_infection"
            }),
            "mortal_wound_deterioration_policy_authority_invalid");
    }

    [Fact]
    public void DeteriorationPolicyAuthority_CanonicalFactoryRejectsForeignBindingAndIsStableOnRetry()
    {
        var scenario = DeteriorationScenario(
            new JsonObject { ["kind"] = "increase_severity" });
        using var fixture = AcceptedStateFixture.Create(scenario);
        using var foreignFixture = AcceptedStateFixture.Create(scenario with
        {
            OperationKey = scenario.OperationKey + "_foreign"
        });
        var acceptedState = Assert.IsType<MortalWoundTreatmentAcceptedStateAuthority>(
            fixture.GetAcceptedState());
        var foreignState = Assert.IsType<MortalWoundTreatmentAcceptedStateAuthority>(
            foreignFixture.GetAcceptedState());
        var create = DeteriorationFactory(5);

        var first = ValidDeteriorationAuthority(Invoke(create, new object?[]
        {
            fixture.FileSystem,
            fixture.Lease,
            acceptedState.Binding,
            fixture.WoundId,
            "untreated_infection"
        }));
        var retry = ValidDeteriorationAuthority(Invoke(create, new object?[]
        {
            fixture.FileSystem,
            fixture.Lease,
            acceptedState.Binding,
            fixture.WoundId,
            "untreated_infection"
        }));
        Assert.Equal(
            ReadRequiredProperty(first, "AuthorityFingerprint"),
            ReadRequiredProperty(retry, "AuthorityFingerprint"));

        AssertInvalidDeteriorationAuthority(
            Invoke(create, new object?[]
            {
                fixture.FileSystem,
                fixture.Lease,
                foreignState.Binding,
                fixture.WoundId,
                "untreated_infection"
            }),
            "mortal_wound_deterioration_policy_authority_invalid");
    }

    [Fact]
    public void DeteriorationPolicyAuthority_RequiresExplicitDeathContourAtSeverityFour()
    {
        using var severityFixture = AcceptedStateFixture.Create(DeteriorationScenario(
            new JsonObject { ["kind"] = "increase_severity" },
            severityRank: 4));
        var severityState = Assert.IsType<MortalWoundTreatmentAcceptedStateAuthority>(
            severityFixture.GetAcceptedState());
        var severityScenario = DeteriorationScenario(
            new JsonObject { ["kind"] = "increase_severity" },
            severityRank: 4);
        var severityCoordinates = CreateDeteriorationCoordinates(
            severityFixture,
            severityState,
            severityScenario,
            severityScenario.OperationKey);

        AssertInvalidDeteriorationAuthority(
            Invoke(DeteriorationFactory(3), new object?[]
            {
                severityState,
                severityCoordinates,
                "untreated_infection"
            }),
            "mortal_wound_deterioration_policy_not_strictly_worsening");

        var deathScenario = DeteriorationScenario(
            new JsonObject { ["kind"] = "death_contour" },
            severityRank: 4);
        using var deathFixture = AcceptedStateFixture.Create(deathScenario);
        var deathState = Assert.IsType<MortalWoundTreatmentAcceptedStateAuthority>(
            deathFixture.GetAcceptedState());
        var deathCoordinates = CreateDeteriorationCoordinates(
            deathFixture,
            deathState,
            deathScenario,
            deathScenario.OperationKey);
        Assert.Equal(
            "StrictlyWorsening",
            Convert.ToString(ReadRequiredProperty(
                ValidDeteriorationAuthority(Invoke(DeteriorationFactory(3), new object?[]
                {
                    deathState,
                    deathCoordinates,
                    "untreated_infection"
                })),
                "Classification")));
    }

    [Fact]
    public void DeteriorationPolicyAuthority_AcceptsApplicableComplicationAndRejectsFullEnvelope()
    {
        var result = DeteriorationComplicationResult();
        var availableScenario = DeteriorationScenario(result);
        using var availableFixture = AcceptedStateFixture.Create(availableScenario);
        var availableState = Assert.IsType<MortalWoundTreatmentAcceptedStateAuthority>(
            availableFixture.GetAcceptedState());
        var availableCoordinates = CreateDeteriorationCoordinates(
            availableFixture,
            availableState,
            availableScenario,
            availableScenario.OperationKey);
        ValidDeteriorationAuthority(Invoke(DeteriorationFactory(3), new object?[]
        {
            availableState,
            availableCoordinates,
            "untreated_infection"
        }));

        var fullScenario = DeteriorationScenario(
            DeteriorationComplicationResult(),
            fillComplications: true);
        using var fullFixture = AcceptedStateFixture.Create(fullScenario);
        var fullState = Assert.IsType<MortalWoundTreatmentAcceptedStateAuthority>(
            fullFixture.GetAcceptedState());
        var fullCoordinates = CreateDeteriorationCoordinates(
            fullFixture,
            fullState,
            fullScenario,
            fullScenario.OperationKey);
        AssertInvalidDeteriorationAuthority(
            Invoke(DeteriorationFactory(3), new object?[]
            {
                fullState,
                fullCoordinates,
                "untreated_infection"
            }),
            "mortal_wound_deterioration_policy_not_strictly_worsening");
    }

    private static ResolverScenario DeteriorationScenario(
        JsonObject result,
        int severityRank = 2,
        bool fillComplications = false)
    {
        var scenario = CreateScenario(
            "procedure_disadvantage_uses_two_contiguous_dice",
            "procedure");
        var before = scenario.Before.DeepClone().AsObject();
        before["recovery"]!["deteriorationPolicy"] = new JsonObject
        {
            ["policyRef"] = "untreated_infection",
            ["unmetConditions"] = new JsonArray("not_stabilized"),
            ["graceMinutes"] = 30L,
            ["cadenceMinutes"] = 10L,
            ["result"] = result.DeepClone()
        };
        if (severityRank == 4)
        {
            before["severity"]!["value"] = "IV";
            before["severity"]!["rank"] = 4;
            before["severity"]!["maximumAtCreation"] = "IV";
        }
        if (fillComplications)
        {
            var complications = before["complications"]!.AsArray();
            for (var index = 0; index < WoundMaterializationContract.MaxComplications; index++)
            {
                complications.Add(new JsonObject
                {
                    ["complicationId"] = $"existing_complication_{index:D2}",
                    ["kind"] = "pain",
                    ["state"] = "active",
                    ["displayName"] = $"Существующее осложнение {index + 1}",
                    ["treatmentDifficultyModifier"] = 1,
                    ["ownedEffectIds"] = new JsonArray(),
                    ["visibility"] = "known_to_player"
                });
            }
        }
        return scenario with
        {
            Before = before,
            OperationKey = scenario.OperationKey + "_deterioration_" +
                           result["kind"]!.GetValue<string>() +
                           (fillComplications ? "_full" : string.Empty)
        };
    }

    private static JsonObject DeteriorationComplicationResult() => new()
    {
        ["kind"] = "add_complication",
        ["complicationDraft"] = new JsonObject
        {
            ["complications"] = new JsonArray(new JsonObject
            {
                ["complicationRef"] = "deterioration_infection",
                ["kind"] = "infection",
                ["state"] = "active",
                ["displayName"] = "Распространяющееся заражение",
                ["treatmentDifficultyModifier"] = 1,
                ["visibility"] = "known_to_player"
            }),
            ["consequenceDefinitions"] = new JsonArray()
        }
    };

    private static MortalWoundTreatmentAttemptCoordinates CreateDeteriorationCoordinates(
        AcceptedStateFixture fixture,
        MortalWoundTreatmentAcceptedStateAuthority acceptedState,
        ResolverScenario scenario,
        string operationKey)
    {
        var result = MortalWoundTreatmentPlanner.CreateAttemptCoordinates(
            acceptedState,
            fixture.ReadCurrentWound(),
            operationKey,
            scenario.RouteId,
            fixture.AcceptedEventRef(acceptedState));
        Assert.True(result.IsValid, DescribeIssues(result.Issues));
        return Assert.IsType<MortalWoundTreatmentAttemptCoordinates>(result.Coordinates);
    }

    private static Type RequireDeteriorationPolicyAuthority()
    {
        var type = typeof(WoundMaterializationContract).Assembly.GetType(
            "BookOfEternityClient.Services.MortalWoundDeteriorationPolicyAuthority",
            throwOnError: false,
            ignoreCase: false);
        Assert.NotNull(type);
        return type;
    }

    private static MethodInfo DeteriorationFactory(int arity) =>
        ExactStaticMethod(RequireDeteriorationPolicyAuthority(), "Create", arity);

    private static object ValidDeteriorationAuthority(object result)
    {
        AssertClosedProperties(result, new[] { "Authority", "IsValid", "Issues" });
        Assert.True(Assert.IsType<bool>(ReadRequiredProperty(result, "IsValid")),
            DescribeIssues(AsObjects(ReadRequiredProperty(result, "Issues"))
                .Select(Assert.IsType<ValidationIssue>)));
        Assert.Empty(AsObjects(ReadRequiredProperty(result, "Issues")));
        return ReadRequiredProperty(result, "Authority");
    }

    private static void AssertInvalidDeteriorationAuthority(
        object result,
        string expectedCode)
    {
        AssertClosedProperties(result, new[] { "Authority", "IsValid", "Issues" });
        Assert.False(Assert.IsType<bool>(ReadRequiredProperty(result, "IsValid")));
        Assert.Null(ReadPropertyAllowingNull(result, "Authority"));
        var issues = AsObjects(ReadRequiredProperty(result, "Issues"))
            .Select(Assert.IsType<ValidationIssue>)
            .ToArray();
        var issue = Assert.Single(issues);
        Assert.Equal(expectedCode, issue.Code);
        Assert.StartsWith(
            "game_state/player/wounds.json.activeWounds[0].recovery.deteriorationPolicy",
            issue.FilePath,
            StringComparison.Ordinal);
    }

    private sealed partial class AcceptedStateFixture
    {
        internal void InvalidateDeteriorationAcceptedState() =>
            PrepareFreshSnapshot("stale_deterioration_authority");
    }
}
