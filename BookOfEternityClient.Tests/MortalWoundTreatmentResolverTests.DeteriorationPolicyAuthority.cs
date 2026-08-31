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

        AssertInvalidDeteriorationAuthority(
            Invoke(create, new object?[]
            {
                acceptedState,
                null,
                "untreated_infection"
            }),
            "mortal_wound_deterioration_policy_authority_invalid");

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

        AssertInvalidDeteriorationAuthority(
            Invoke(create, new object?[]
            {
                fixture.FileSystem,
                fixture.Lease,
                acceptedState.Binding,
                "another_wound",
                "untreated_infection"
            }),
            "mortal_wound_deterioration_policy_authority_invalid");
    }

    [Theory]
    [InlineData("no_change")]
    [InlineData("add_recovery")]
    public void DeteriorationPolicyAuthority_RejectsRecognizedNonWorseningResults(
        string kind)
    {
        var scenario = DeteriorationScenario(new JsonObject { ["kind"] = kind });
        using var fixture = AcceptedStateFixture.Create(scenario);
        var acceptedState = Assert.IsType<MortalWoundTreatmentAcceptedStateAuthority>(
            fixture.GetAcceptedState());
        var coordinates = CreateDeteriorationCoordinates(
            fixture,
            acceptedState,
            scenario,
            scenario.OperationKey);

        AssertInvalidDeteriorationAuthority(
            Invoke(DeteriorationFactory(3), new object?[]
            {
                acceptedState,
                coordinates,
                "untreated_infection"
            }),
            "mortal_wound_deterioration_policy_not_strictly_worsening");
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

    [Fact]
    public void DeteriorationPolicyAuthority_AcceptsEffectlessComplicationAtFiveRootBoundary()
    {
        var scenario = DeteriorationScenarioWithRootEnvelope(
            DeteriorationComplicationResult(),
            includeExistingMarker: true);
        using var fixture = AcceptedStateFixture.Create(scenario);
        var acceptedState = Assert.IsType<MortalWoundTreatmentAcceptedStateAuthority>(
            fixture.GetAcceptedState());
        var coordinates = CreateDeteriorationCoordinates(
            fixture,
            acceptedState,
            scenario,
            scenario.OperationKey);

        ValidDeteriorationAuthority(Invoke(DeteriorationFactory(3), new object?[]
        {
            acceptedState,
            coordinates,
            "untreated_infection"
        }));
    }

    [Fact]
    public void DeteriorationPolicyAuthority_AcceptsFifthZeroSlotRoot()
    {
        var scenario = DeteriorationScenarioWithRootEnvelope(
            DeteriorationMarkerComplicationResult(),
            includeExistingMarker: false);
        using var fixture = AcceptedStateFixture.Create(scenario);
        var acceptedState = Assert.IsType<MortalWoundTreatmentAcceptedStateAuthority>(
            fixture.GetAcceptedState());
        var coordinates = CreateDeteriorationCoordinates(
            fixture,
            acceptedState,
            scenario,
            scenario.OperationKey);

        ValidDeteriorationAuthority(Invoke(DeteriorationFactory(3), new object?[]
        {
            acceptedState,
            coordinates,
            "untreated_infection"
        }));
    }

    [Fact]
    public void DeteriorationPolicyAuthority_RejectsSixthRoot()
    {
        var scenario = DeteriorationScenarioWithRootEnvelope(
            DeteriorationMarkerComplicationResult(),
            includeExistingMarker: true);
        using var fixture = AcceptedStateFixture.Create(scenario);
        var acceptedState = Assert.IsType<MortalWoundTreatmentAcceptedStateAuthority>(
            fixture.GetAcceptedState());
        var coordinates = CreateDeteriorationCoordinates(
            fixture,
            acceptedState,
            scenario,
            scenario.OperationKey);

        AssertInvalidDeteriorationAuthority(
            Invoke(DeteriorationFactory(3), new object?[]
            {
                acceptedState,
                coordinates,
                "untreated_infection"
            }),
            "mortal_wound_deterioration_policy_not_strictly_worsening");
    }

    [Fact]
    public void DeteriorationPolicyAuthority_FingerprintBindsEveryPolicySemantic()
    {
        var fingerprints = new[]
        {
            DeteriorationAuthorityFingerprint("baseline"),
            DeteriorationAuthorityFingerprint("condition"),
            DeteriorationAuthorityFingerprint("grace"),
            DeteriorationAuthorityFingerprint("cadence"),
            DeteriorationAuthorityFingerprint("result")
        };

        Assert.Equal(fingerprints.Length, fingerprints.Distinct(StringComparer.Ordinal).Count());
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

    private static string DeteriorationAuthorityFingerprint(string mutation)
    {
        var scenario = DeteriorationScenario(
            new JsonObject { ["kind"] = "increase_severity" });
        var before = scenario.Before.DeepClone().AsObject();
        var policy = before["recovery"]!["deteriorationPolicy"]!.AsObject();
        switch (mutation)
        {
            case "baseline":
                break;
            case "condition":
                policy["unmetConditions"] = new JsonArray("awaiting_antibiotics");
                break;
            case "grace":
                policy["graceMinutes"] = 31L;
                break;
            case "cadence":
                policy["cadenceMinutes"] = 11L;
                break;
            case "result":
                policy["result"] = new JsonObject { ["kind"] = "death_contour" };
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null);
        }
        scenario = scenario with
        {
            Before = before,
            OperationKey = scenario.OperationKey + "_fingerprint_" + mutation
        };

        using var fixture = AcceptedStateFixture.Create(scenario);
        var acceptedState = Assert.IsType<MortalWoundTreatmentAcceptedStateAuthority>(
            fixture.GetAcceptedState());
        var coordinates = CreateDeteriorationCoordinates(
            fixture,
            acceptedState,
            scenario,
            scenario.OperationKey);
        var authority = ValidDeteriorationAuthority(Invoke(
            DeteriorationFactory(3),
            new object?[]
            {
                acceptedState,
                coordinates,
                "untreated_infection"
            }));
        return Assert.IsType<string>(ReadRequiredProperty(
            authority,
            "AuthorityFingerprint"));
    }

    private static ResolverScenario DeteriorationScenarioWithRootEnvelope(
        JsonObject result,
        bool includeExistingMarker)
    {
        var scenario = DeteriorationScenario(result, severityRank: 4);
        var before = scenario.Before.DeepClone().AsObject();
        var consequences = before["consequences"]!.AsObject();
        consequences["slotBudget"] = 4;
        consequences["slotsUsed"] = 4;
        consequences["entries"] = WoundContractTestData.Repeat(4, index =>
            new JsonObject
            {
                ["slot"] = index + 1,
                ["profileKey"] = WoundContractTestData.DistinctMortalProfile(index),
                ["effectId"] = $"effect_deterioration_root_{index}",
                ["readableSummary"] = $"Ограничение ухудшения {index + 1}."
            });
        var roots = new List<(string EffectId, string DefinitionKey, string Profile)>
        {
            ("effect_deterioration_root_0", "definition_deterioration_root_0", "action_control"),
            ("effect_deterioration_root_1", "definition_deterioration_root_1", "characteristic_modifier"),
            ("effect_deterioration_root_2", "definition_deterioration_root_2", "resistance_modifier"),
            ("effect_deterioration_root_3", "definition_deterioration_root_3", "periodic_damage")
        };
        if (includeExistingMarker)
        {
            roots.Add((
                "effect_deterioration_marker",
                "definition_deterioration_marker",
                "wound_consequence"));
        }
        consequences["ownedEffectSources"] =
            WoundContractTestData.CreateOwnedEffectSources(
                "wound_test_torn_side",
                "mortal_world",
                roots.ToArray());
        return scenario with
        {
            Before = before,
            OperationKey = scenario.OperationKey +
                           (includeExistingMarker ? "_five_roots" : "_four_roots")
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

    private static JsonObject DeteriorationMarkerComplicationResult()
    {
        var result = DeteriorationComplicationResult();
        var definition = WoundContractTestData.CreateOwnedEffectDefinition(
            "wound_test_torn_side",
            "mortal_world",
            "deterioration_infection_marker",
            "wound_consequence");
        definition["links"] = new JsonArray();
        definition["components"]![0]!["payload"]!.AsObject().Remove("woundId");
        result["complicationDraft"]!["consequenceDefinitions"] = new JsonArray(
            new JsonObject
            {
                ["definitionRef"] = "deterioration_infection_marker",
                ["definition"] = definition,
                ["root"] = new JsonObject
                {
                    ["ownership"] = new JsonObject
                    {
                        ["kind"] = "complication",
                        ["complicationRef"] = "deterioration_infection"
                    },
                    ["slots"] = new JsonArray()
                }
            });
        return result;
    }

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
