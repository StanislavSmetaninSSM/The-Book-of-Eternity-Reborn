using System.Collections;
using System.Reflection;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// T067-A RED coverage for the production procedure-check authority. These tests stop
/// before request sealing, resources, semantic outcome resolution, persistence, or
/// publication and never construct an accepted state, claim, authority, or fingerprint.
/// </summary>
public sealed partial class MortalWoundTreatmentResolverTests
{
    private const string ProcedureCheckAuthorityTypeName =
        "BookOfEternityClient.Services.MortalWoundProcedureCheckAuthority";
    private const string FateShieldReactionArbiterTypeName =
        "BookOfEternityClient.Services.FateShieldReactionArbiter";

    [Fact]
    public void ProcedureCheckAuthority_SurfaceIsClosedAndFactoryIsExact()
    {
        var authorityType = RequireProcedureCheckAuthorityType();
        AssertClosedPublicInstanceProperties(authorityType, new[]
        {
            "SourcePath", "RollMode", "RollActorKind", "RollActorId", "RollContributions",
            "SourceIndices", "SourceRolls", "SelectedSourceIndex", "NaturalRoll", "Modifier",
            "ComplicationDifficultyModifier", "EffectiveDifficulty", "RequirementAuthorityFingerprint",
            "CoordinatesFingerprint", "AcceptedStateFingerprint", "PreparedCriticalReaction",
            "AuthorityFingerprint"
        });

        var create = Assert.Single(authorityType.GetMethods(
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic),
            static method => string.Equals(method.Name, "Create", StringComparison.Ordinal));
        Assert.Equal(
            new[]
            {
                typeof(MortalWoundTreatmentAttemptCoordinates),
                typeof(MortalWoundProcedureRouteDefinition),
                typeof(WoundMaterializationEnvelope),
                typeof(MortalWoundTreatmentRequirementAuthorityBundle),
                typeof(MortalWoundTreatmentAcceptedStateAuthority)
            },
            create.GetParameters().Select(static parameter => parameter.ParameterType));
        Assert.Equal("MortalWoundProcedureCheckAuthorityResult", create.ReturnType.Name);
        AssertClosedPublicInstanceProperties(create.ReturnType, new[] { "IsValid", "Issues", "Authority" });
        Assert.Equal(typeof(bool), create.ReturnType.GetProperty("IsValid")!.PropertyType);
        Assert.True(typeof(IReadOnlyList<ValidationIssue>).IsAssignableFrom(
            create.ReturnType.GetProperty("Issues")!.PropertyType));
        Assert.Equal(authorityType, create.ReturnType.GetProperty("Authority")!.PropertyType);

        var contributionType = SequenceElementType(
            authorityType.GetProperty("RollContributions")!.PropertyType);
        AssertClosedPublicInstanceProperties(
            contributionType,
            new[] { "EffectId", "ComponentId", "Contribution" });
        var preparedType = authorityType.GetProperty("PreparedCriticalReaction")!.PropertyType;
        AssertClosedPublicInstanceProperties(preparedType, new[]
        {
            "EffectId", "TriggerId", "AcceptedEffectFingerprint", "PreparedReactionFingerprint"
        });
    }

    [Theory]
    [InlineData(
        "procedure_normal_uses_lowest_free_die",
        "normal",
        new[] { 0 },
        new[] { 17 },
        0,
        17)]
    [InlineData(
        "procedure_advantage_uses_two_contiguous_dice",
        "advantage",
        new[] { 0, 1 },
        new[] { 4, 19 },
        1,
        19)]
    [InlineData(
        "procedure_disadvantage_uses_two_contiguous_dice",
        "disadvantage",
        new[] { 0, 1 },
        new[] { 4, 19 },
        0,
        4)]
    public void ProcedureCheckAuthority_DerivesSignedDiceSpanAndAcceptedRollMode(
        string scenarioName,
        string expectedRollMode,
        int[] expectedSourceIndices,
        int[] expectedSourceRolls,
        int expectedSelectedSourceIndex,
        int expectedNaturalRoll)
    {
        var scenario = CreateScenario(scenarioName, "procedure");
        using var fixture = AcceptedStateFixture.Create(scenario);

        var invocation = InvokeProcedureCheckAuthority(
            fixture,
            scenario.OperationKey,
            scenario.RouteId);
        var authority = AssertValidProcedureCheckAuthority(invocation);

        AssertProcedureAuthorityMechanics(
            invocation,
            authority,
            expectedRollMode,
            "player",
            "player_current",
            expectedSourceIndices,
            expectedSourceRolls,
            expectedSelectedSourceIndex,
            expectedNaturalRoll,
            expectedModifier: 3);
        var contributions = AsObjects(ReadRequiredProperty(authority, "RollContributions"));
        if (string.Equals(expectedRollMode, "normal", StringComparison.Ordinal))
        {
            Assert.Empty(contributions);
        }
        else
        {
            var contribution = Assert.Single(contributions);
            AssertClosedProperties(contribution, new[] { "EffectId", "ComponentId", "Contribution" });
            Assert.Equal(
                "effect_roll_modifier_" + expectedRollMode,
                Convert.ToString(ReadRequiredProperty(contribution, "EffectId")));
            Assert.Equal("component_001", Convert.ToString(
                ReadRequiredProperty(contribution, "ComponentId")));
            Assert.Equal(expectedRollMode, Convert.ToString(
                ReadRequiredProperty(contribution, "Contribution")));
        }
    }

    [Fact]
    public void ProcedureCheckAuthority_CheckedDifficultyFailureDoesNotAdvanceDiceReservation()
    {
        var scenario = CreateProcedureBoundaryScenario(
            "effective_difficulty_overflow_phase_a",
            naturalRoll: 1,
            difficulty: int.MaxValue,
            fixedZero: false,
            seedPlayerFate: true,
            expectedCategory: "failed_attempt",
            expectedBandIndex: 3);
        const string safeRouteId = "procedure_t067a_after_difficulty_overflow";
        var safeRoute = scenario.Before["treatment"]!["routes"]![0]!
            .DeepClone()
            .AsObject();
        safeRoute["routeId"] = safeRouteId;
        safeRoute["resolution"]!["difficulty"] = 10;
        scenario.Before["treatment"]!["routes"]!.AsArray().Add(safeRoute);
        scenario.Before["treatment"]!["knownRouteIds"]!.AsArray().Add(safeRouteId);
        scenario.Before["complications"] = new JsonArray(new JsonObject
        {
            ["complicationId"] = "overflow_pressure_t067a",
            ["kind"] = "pain",
            ["state"] = "active",
            ["displayName"] = "Overflow pressure",
            ["treatmentDifficultyModifier"] = 1,
            ["ownedEffectIds"] = new JsonArray(),
            ["visibility"] = "known_to_player"
        });
        using var fixture = AcceptedStateFixture.Create(scenario);

        var rejected = InvokeProcedureCheckAuthority(
            fixture,
            scenario.OperationKey,
            scenario.RouteId);
        AssertClosedProperties(rejected.Result, new[] { "IsValid", "Issues", "Authority" });
        Assert.False(Assert.IsType<bool>(ReadRequiredProperty(rejected.Result, "IsValid")));
        Assert.Null(ReadPropertyAllowingNull(rejected.Result, "Authority"));
        var issues = ReadRequiredProperty(rejected.Result, "Issues");
        Assert.NotEmpty(AsObjects(issues));
        AssertFrozenSequence(issues, allowEmptyArray: false);

        var recovered = InvokeProcedureCheckAuthority(
            fixture,
            scenario.OperationKey + "_safe",
            safeRouteId);
        var recoveredAuthority = AssertValidProcedureCheckAuthority(recovered);
        Assert.Equal(new[] { 0 }, ReadIntSequence(
            ReadRequiredProperty(recoveredAuthority, "SourceIndices")));
        Assert.Equal("effect_fate_shield_older", ReadPreparedFateEffectIdFromAuthority(
            recoveredAuthority));
    }

    [Fact]
    public void ProcedureCheckAuthority_ExactRetryReusesClaimAndDistinctCoordinatesAdvance()
    {
        var scenario = CreateScenario(
            "procedure_player_natural_one_reserves_oldest_fate_shield",
            "procedure");
        scenario.AcceptedState["acceptedDice"] = new JsonArray(1, 1, 17);
        using var fixture = AcceptedStateFixture.Create(scenario);

        var first = InvokeProcedureCheckAuthority(
            fixture,
            scenario.OperationKey + "_first",
            scenario.RouteId);
        var firstAuthority = AssertValidProcedureCheckAuthority(first);
        var retry = InvokeProcedureCheckAuthority(
            fixture,
            scenario.OperationKey + "_first",
            scenario.RouteId);
        var retryAuthority = AssertValidProcedureCheckAuthority(retry);
        Assert.Equal(CanonicalValue(firstAuthority), CanonicalValue(retryAuthority));
        Assert.Equal(
            first.Coordinates.CoordinatesFingerprint,
            retry.Coordinates.CoordinatesFingerprint);

        var second = InvokeProcedureCheckAuthority(
            fixture,
            scenario.OperationKey + "_second",
            scenario.RouteId);
        var secondAuthority = AssertValidProcedureCheckAuthority(second);
        Assert.Equal(new[] { 0 }, ReadIntSequence(ReadRequiredProperty(
            firstAuthority,
            "SourceIndices")));
        Assert.Equal(new[] { 1 }, ReadIntSequence(ReadRequiredProperty(
            secondAuthority,
            "SourceIndices")));
        Assert.Equal("effect_fate_shield_older", ReadPreparedFateEffectIdFromAuthority(
            firstAuthority));
        Assert.Equal("effect_fate_shield_newer", ReadPreparedFateEffectIdFromAuthority(
            secondAuthority));
        Assert.NotEqual(
            first.Coordinates.CoordinatesFingerprint,
            second.Coordinates.CoordinatesFingerprint);
        Assert.NotEqual(
            ReadRequiredProperty(firstAuthority, "AuthorityFingerprint"),
            ReadRequiredProperty(secondAuthority, "AuthorityFingerprint"));
    }

    [Fact]
    public void ProcedureCheckAuthority_PlayerNaturalOneReservesOldestFateCandidate()
    {
        var scenario = CreateScenario(
            "procedure_player_natural_one_reserves_oldest_fate_shield",
            "procedure");
        using var fixture = AcceptedStateFixture.Create(scenario);

        var invocation = InvokeProcedureCheckAuthority(
            fixture,
            scenario.OperationKey,
            scenario.RouteId);
        var authority = AssertValidProcedureCheckAuthority(invocation);
        var prepared = ReadRequiredProperty(authority, "PreparedCriticalReaction");

        AssertClosedProperties(prepared, new[]
        {
            "EffectId", "TriggerId", "AcceptedEffectFingerprint", "PreparedReactionFingerprint"
        });
        Assert.Equal("effect_fate_shield_older", Convert.ToString(
            ReadRequiredProperty(prepared, "EffectId")));
        Assert.Equal("fate_shield_on_critical_failure", Convert.ToString(
            ReadRequiredProperty(prepared, "TriggerId")));
        AssertAuthorityFingerprint(ReadRequiredProperty(prepared, "AcceptedEffectFingerprint"));
        AssertAuthorityFingerprint(ReadRequiredProperty(prepared, "PreparedReactionFingerprint"));
    }

    [Fact]
    public void ProcedureCheckAuthority_NpcNaturalOneDoesNotReservePlayerFate()
    {
        var scenario = CreateProcedureBoundaryScenario(
            "npc_natural_one_ignores_player_fate_phase_a",
            naturalRoll: 1,
            difficulty: 1,
            fixedZero: true,
            seedPlayerFate: true,
            expectedCategory: "failed_attempt",
            expectedBandIndex: 3);
        using var fixture = AcceptedStateFixture.Create(scenario);

        var invocation = InvokeProcedureCheckAuthority(
            fixture,
            scenario.OperationKey,
            scenario.RouteId);
        var authority = AssertValidProcedureCheckAuthority(invocation);

        Assert.Equal("npc", Convert.ToString(ReadRequiredProperty(authority, "RollActorKind")));
        Assert.Equal("field_medic_01", Convert.ToString(
            ReadRequiredProperty(authority, "RollActorId")));
        Assert.Equal(0, Convert.ToInt32(ReadRequiredProperty(authority, "Modifier")));
        Assert.Equal(1, Convert.ToInt32(ReadRequiredProperty(authority, "NaturalRoll")));
        Assert.Null(ReadPropertyAllowingNull(authority, "PreparedCriticalReaction"));
    }

    [Fact]
    public void ProcedureCheckAuthority_OpposingEffectsCancelAndSameDirectionDoesNotEscalate()
    {
        var scenario = CreateScenario(
            "procedure_normal_uses_lowest_free_die",
            "procedure");
        using var opposingFixture = AcceptedStateFixture.Create(scenario);
        opposingFixture.ReplacePlayerProcedureRollEffects(
            "opposing_roll_directions",
            "advantage",
            "disadvantage");
        var opposing = InvokeProcedureCheckAuthority(
            opposingFixture,
            scenario.OperationKey + "_opposing",
            scenario.RouteId);
        var opposingAuthority = AssertValidProcedureCheckAuthority(opposing);
        Assert.Equal("normal", Convert.ToString(ReadRequiredProperty(
            opposingAuthority,
            "RollMode")));
        Assert.Equal(new[] { 0 }, ReadIntSequence(ReadRequiredProperty(
            opposingAuthority,
            "SourceIndices")));
        Assert.Equal(
            new[] { "advantage", "disadvantage" },
            AsObjects(ReadRequiredProperty(opposingAuthority, "RollContributions"))
                .Select(contribution => Convert.ToString(
                    ReadRequiredProperty(contribution, "Contribution"))));

        using var sameDirectionFixture = AcceptedStateFixture.Create(scenario);
        sameDirectionFixture.ReplacePlayerProcedureRollEffects(
            "same_roll_direction",
            "advantage",
            "advantage");
        var sameDirection = InvokeProcedureCheckAuthority(
            sameDirectionFixture,
            scenario.OperationKey + "_same_direction",
            scenario.RouteId);
        var sameDirectionAuthority = AssertValidProcedureCheckAuthority(sameDirection);
        Assert.Equal("advantage", Convert.ToString(ReadRequiredProperty(
            sameDirectionAuthority,
            "RollMode")));
        Assert.Equal(new[] { 0, 1 }, ReadIntSequence(ReadRequiredProperty(
            sameDirectionAuthority,
            "SourceIndices")));
        Assert.Equal(2, AsObjects(ReadRequiredProperty(
            sameDirectionAuthority,
            "RollContributions")).Length);
    }

    [Fact]
    public void ProcedureCheckAuthority_FateShieldArbiterLegacyAndTreatmentSelectionAgree()
    {
        var scenario = CreateScenario(
            "procedure_player_natural_one_reserves_oldest_fate_shield",
            "procedure");
        using var fixture = AcceptedStateFixture.Create(scenario);
        var carriers = new EffectCarrierCatalogInput(
            fixture.ReadPlayerEffectCarrier(),
            null,
            null,
            null,
            null,
            null);
        var legacy = EffectAcceptedEventReportCatalog.Compose(
            new JsonArray(CreateProcedureCriticalFailureReport()),
            turn: 42,
            realm: "mortal_world",
            authoritativeDice: new[] { 1, 17 },
            carriers);
        Assert.Empty(legacy.Issues);
        var legacyEvent = Assert.Single(legacy.LifecycleEvents);
        Assert.Equal("effect_fate_shield_older", legacyEvent["effectId"]!.GetValue<string>());
        Assert.Equal(
            "fate_shield_on_critical_failure",
            legacyEvent["triggerId"]!.GetValue<string>());
        var legacyCompose = Assert.Single(typeof(EffectAcceptedEventReportCatalog).GetMethods(
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic),
            static method => string.Equals(method.Name, "Compose", StringComparison.Ordinal));
        Assert.Equal(
            new[]
            {
                typeof(JsonNode),
                typeof(int),
                typeof(string),
                typeof(IReadOnlyList<int>),
                typeof(EffectCarrierCatalogInput)
            },
            legacyCompose.GetParameters().Select(static parameter => parameter.ParameterType));

        var arbiterType = typeof(WoundMaterializationContract).Assembly.GetType(
            FateShieldReactionArbiterTypeName,
            throwOnError: false,
            ignoreCase: false);
        Assert.True(
            arbiterType is not null,
            "T067-A must extract one FateShieldReactionArbiter shared by legacy and typed treatment selection.");

        var treatment = InvokeProcedureCheckAuthority(
            fixture,
            scenario.OperationKey,
            scenario.RouteId);
        var treatmentAuthority = AssertValidProcedureCheckAuthority(treatment);
        var prepared = ReadRequiredProperty(treatmentAuthority, "PreparedCriticalReaction");
        Assert.Equal(
            legacyEvent["effectId"]!.GetValue<string>(),
            Convert.ToString(ReadRequiredProperty(prepared, "EffectId")));
        Assert.Equal(
            legacyEvent["triggerId"]!.GetValue<string>(),
            Convert.ToString(ReadRequiredProperty(prepared, "TriggerId")));
    }

    private static ProcedureAuthorityInvocation InvokeProcedureCheckAuthority(
        AcceptedStateFixture fixture,
        string operationKey,
        string routeId)
    {
        var acceptedState = Assert.IsType<MortalWoundTreatmentAcceptedStateAuthority>(
            fixture.GetAcceptedState());
        var before = fixture.ReadCurrentWound();
        var route = Assert.IsType<MortalWoundProcedureRouteDefinition>(Assert.Single(
            acceptedState.TreatmentDefinition.Routes,
            candidate => string.Equals(candidate.RouteId, routeId, StringComparison.Ordinal)));
        var coordinatesResult = MortalWoundTreatmentPlanner.CreateAttemptCoordinates(
            acceptedState,
            before,
            operationKey,
            routeId,
            fixture.AcceptedEventRef(acceptedState));
        Assert.True(coordinatesResult.IsValid, DescribeIssues(coordinatesResult.Issues));
        Assert.NotNull(coordinatesResult.Coordinates);
        var coordinates = coordinatesResult.Coordinates!;
        var requirementResult = MortalWoundTreatmentRequirementAuthorityBundle.CreateForProcedure(
            acceptedState,
            coordinates,
            before);
        Assert.True(requirementResult.IsValid, DescribeIssues(requirementResult.Issues));
        Assert.NotNull(requirementResult.Authority);
        var requirementAuthority = requirementResult.Authority!;

        var authorityType = RequireProcedureCheckAuthorityType();
        var create = ExactStaticMethod(authorityType, "Create", 5);
        var result = Invoke(create, new object?[]
        {
            coordinates,
            route,
            before,
            requirementAuthority,
            acceptedState
        });
        return new ProcedureAuthorityInvocation(
            result,
            acceptedState,
            coordinates,
            route,
            before,
            requirementAuthority);
    }

    private static object AssertValidProcedureCheckAuthority(
        ProcedureAuthorityInvocation invocation)
    {
        AssertClosedProperties(invocation.Result, new[] { "IsValid", "Issues", "Authority" });
        Assert.True(Assert.IsType<bool>(ReadRequiredProperty(invocation.Result, "IsValid")));
        var issues = ReadRequiredProperty(invocation.Result, "Issues");
        Assert.Empty(AsObjects(issues));
        AssertFrozenSequence(issues, allowEmptyArray: true);
        return ReadRequiredProperty(invocation.Result, "Authority");
    }

    private static void AssertProcedureAuthorityMechanics(
        ProcedureAuthorityInvocation invocation,
        object authority,
        string expectedRollMode,
        string expectedRollActorKind,
        string expectedRollActorId,
        int[] expectedSourceIndices,
        int[] expectedSourceRolls,
        int expectedSelectedSourceIndex,
        int expectedNaturalRoll,
        int expectedModifier)
    {
        AssertClosedProperties(authority, new[]
        {
            "SourcePath", "RollMode", "RollActorKind", "RollActorId", "RollContributions",
            "SourceIndices", "SourceRolls", "SelectedSourceIndex", "NaturalRoll", "Modifier",
            "ComplicationDifficultyModifier", "EffectiveDifficulty", "RequirementAuthorityFingerprint",
            "CoordinatesFingerprint", "AcceptedStateFingerprint", "PreparedCriticalReaction",
            "AuthorityFingerprint"
        });
        Assert.Equal(LiveTurnPreparationService.TurnRequestPath, Convert.ToString(
            ReadRequiredProperty(authority, "SourcePath")));
        Assert.Equal(expectedRollMode, Convert.ToString(ReadRequiredProperty(authority, "RollMode")));
        Assert.Equal(expectedRollActorKind, Convert.ToString(
            ReadRequiredProperty(authority, "RollActorKind")));
        Assert.Equal(expectedRollActorId, Convert.ToString(
            ReadRequiredProperty(authority, "RollActorId")));
        Assert.Equal(expectedSourceIndices, ReadIntSequence(
            ReadRequiredProperty(authority, "SourceIndices")));
        Assert.Equal(expectedSourceRolls, ReadIntSequence(
            ReadRequiredProperty(authority, "SourceRolls")));
        Assert.Equal(expectedSelectedSourceIndex, Convert.ToInt32(
            ReadRequiredProperty(authority, "SelectedSourceIndex")));
        Assert.Equal(expectedNaturalRoll, Convert.ToInt32(
            ReadRequiredProperty(authority, "NaturalRoll")));
        Assert.Equal(expectedModifier, Convert.ToInt32(ReadRequiredProperty(authority, "Modifier")));
        var complicationDifficulty = invocation.Before.Complications
            .Where(static complication => string.Equals(
                complication.State,
                "active",
                StringComparison.Ordinal))
            .Aggregate(0, static (sum, complication) => checked(
                sum + complication.TreatmentDifficultyModifier));
        Assert.Equal(complicationDifficulty, Convert.ToInt32(
            ReadRequiredProperty(authority, "ComplicationDifficultyModifier")));
        Assert.Equal(
            checked(invocation.Route.Resolution.Difficulty + complicationDifficulty),
            Convert.ToInt32(ReadRequiredProperty(authority, "EffectiveDifficulty")));
        Assert.Equal(
            invocation.RequirementAuthority.AuthorityFingerprint,
            Convert.ToString(ReadRequiredProperty(authority, "RequirementAuthorityFingerprint")));
        Assert.Equal(
            invocation.Coordinates.CoordinatesFingerprint,
            Convert.ToString(ReadRequiredProperty(authority, "CoordinatesFingerprint")));
        Assert.Equal(
            invocation.AcceptedState.AcceptedStateFingerprint,
            Convert.ToString(ReadRequiredProperty(authority, "AcceptedStateFingerprint")));
        AssertAuthorityFingerprint(ReadRequiredProperty(authority, "AuthorityFingerprint"));
        AssertFrozenSequence(ReadRequiredProperty(authority, "RollContributions"), allowEmptyArray: true);
        AssertFrozenSequence(ReadRequiredProperty(authority, "SourceIndices"), allowEmptyArray: false);
        AssertFrozenSequence(ReadRequiredProperty(authority, "SourceRolls"), allowEmptyArray: false);
    }

    private static string? ReadPreparedFateEffectIdFromAuthority(object authority)
    {
        var prepared = ReadPropertyAllowingNull(authority, "PreparedCriticalReaction");
        return prepared is null
            ? null
            : Convert.ToString(ReadRequiredProperty(prepared, "EffectId"));
    }

    private static Type RequireProcedureCheckAuthorityType()
    {
        var type = typeof(WoundMaterializationContract).Assembly.GetType(
            ProcedureCheckAuthorityTypeName,
            throwOnError: false,
            ignoreCase: false);
        Assert.True(
            type is not null,
            "T067-A production MortalWoundProcedureCheckAuthority is absent.");
        return type!;
    }

    private static void AssertClosedPublicInstanceProperties(
        Type type,
        IEnumerable<string> expected)
    {
        var properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(static property => property.GetIndexParameters().Length == 0)
            .Select(static property => property.Name)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(
            expected.OrderBy(static name => name, StringComparer.Ordinal),
            properties);
    }

    private static Type SequenceElementType(Type sequenceType)
    {
        var enumerableTypes = sequenceType.GetInterfaces()
            .Append(sequenceType)
            .Where(static candidate => candidate.IsGenericType &&
                candidate.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            .Select(static candidate => candidate.GetGenericArguments()[0])
            .Distinct()
            .ToArray();
        return Assert.Single(enumerableTypes);
    }

    private static void AssertFrozenSequence(object value, bool allowEmptyArray)
    {
        if (value is Array array)
        {
            Assert.True(
                allowEmptyArray && array.Length == 0,
                "Only the immutable empty-array singleton is accepted as array-backed evidence.");
            return;
        }

        var valueType = value.GetType();
        if (valueType.IsGenericType &&
            string.Equals(
                valueType.GetGenericTypeDefinition().FullName,
                "System.Collections.Immutable.ImmutableArray`1",
                StringComparison.Ordinal))
        {
            return;
        }

        var list = Assert.IsAssignableFrom<IList>(value);
        Assert.True(list.IsReadOnly, "Authority collections must reject caller mutation.");
        Assert.Throws<NotSupportedException>(() => list.Add(new object()));
    }

    private static JsonObject CreateProcedureCriticalFailureReport() => new()
    {
        ["eventType"] = "owner_critical_failure",
        ["target"] = new JsonObject
        {
            ["kind"] = "player",
            ["targetId"] = "player_current"
        },
        ["evidence"] = new JsonObject
        {
            ["kind"] = "mortal_action_roll",
            ["rollMode"] = "normal",
            ["diceIndexes"] = new JsonArray(0),
            ["selectedIndex"] = 0,
            ["selectedValue"] = 1,
            ["originalOutcome"] = "critical_failure",
            ["resolvedOutcome"] = "failure"
        },
        ["reason"] = "The accepted natural one is mitigated by the oldest Fate Shield."
    };

    private static (JsonObject Carrier, JsonObject IdentityIndex)
        CreateProcedureRollEffectState(IReadOnlyList<string> contributions)
    {
        var effects = contributions.Select((contribution, ordinal) =>
        {
            var effect = EffectMaterializationTestFixture.CreateCanonicalEffect(
                "player",
                "roll_modifier");
            var suffix = $"{contribution}_{ordinal + 1}";
            effect["effectId"] = "effect_roll_modifier_" + suffix;
            effect["display"]!["name"] = "T067-A " + contribution;
            effect["source"] = new JsonObject
            {
                ["kind"] = "skill",
                ["sourceId"] = "skill_field_medicine_01",
                ["definitionKey"] = "t067a-treatment-roll-" + suffix
            };
            effect["components"]![0]!["payload"] = new JsonObject
            {
                ["operations"] = new JsonArray("skill_check"),
                ["contribution"] = contribution
            };
            var createdAtTurn = 30 + ordinal;
            var eventRef = $"turn_{createdAtTurn}:t067a_effect_seed:{ordinal + 1}";
            var transitionId = $"effect_transition_t067a_seed_{ordinal + 1}";
            effect["chronology"]!["createdAtTurn"] = createdAtTurn;
            effect["chronology"]!["createdEventRef"] = eventRef;
            effect["chronology"]!["lastTransitionId"] = transitionId;
            effect["chronology"]!["lastTransitionTurn"] = createdAtTurn;
            return effect;
        }).ToArray();
        var index = EffectMaterializationTestFixture.CreateIdentityIndex(effects);
        var entries = index["entries"]!.AsArray().OfType<JsonObject>().ToArray();
        for (var ordinal = 0; ordinal < entries.Length; ordinal++)
        {
            var effect = effects[ordinal];
            var transition = entries[ordinal]["transitions"]![0]!.AsObject();
            entries[ordinal]["createdAtTurn"] = effect["chronology"]!["createdAtTurn"]!.DeepClone();
            transition["transitionId"] = effect["chronology"]!["lastTransitionId"]!.DeepClone();
            transition["turn"] = effect["chronology"]!["createdAtTurn"]!.DeepClone();
            transition["eventRef"] = effect["chronology"]!["createdEventRef"]!.DeepClone();
        }
        return (
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["activeEffects"] = new JsonArray(
                    effects.Select(static effect => (JsonNode)effect).ToArray())
            },
            index);
    }

    private sealed record ProcedureAuthorityInvocation(
        object Result,
        MortalWoundTreatmentAcceptedStateAuthority AcceptedState,
        MortalWoundTreatmentAttemptCoordinates Coordinates,
        MortalWoundProcedureRouteDefinition Route,
        WoundMaterializationEnvelope Before,
        MortalWoundTreatmentRequirementAuthorityBundle RequirementAuthority);

    private sealed partial class AcceptedStateFixture
    {
        internal void ReplacePlayerProcedureRollEffects(
            string label,
            params string[] contributions)
        {
            var effectState = CreateProcedureRollEffectState(contributions);
            WriteObject(EffectCarrierCatalog.PlayerPath, effectState.Carrier);
            WriteObject(EffectIdentityState.StatePath, effectState.IdentityIndex);
            PrepareFreshSnapshot(label);
        }

        internal JsonObject ReadPlayerEffectCarrier() =>
            ReadObject(EffectCarrierCatalog.PlayerPath);
    }
}
