using System.Collections;
using System.Globalization;
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
    private const string AttemptRequestTypeName =
        "BookOfEternityClient.Services.MortalWoundTreatmentAttemptRequest";
    private const string AttemptRequestResultTypeName =
        "BookOfEternityClient.Services.MortalWoundTreatmentAttemptRequestResult";
    private const string CriticalReactionResultTypeName =
        "BookOfEternityClient.Services.MortalWoundCriticalReactionResolutionResult";
    private const string CriticalReactionIntentTypeName =
        "BookOfEternityClient.Services.MortalWoundCriticalReactionIntent";
    private const string TreatmentResolutionResultTypeName =
        "BookOfEternityClient.Services.MortalWoundTreatmentResolutionResult";
    private const string TreatmentResolutionTypeName =
        "BookOfEternityClient.Services.MortalWoundTreatmentResolution";
    private const string TreatmentReceiptTypeName =
        "BookOfEternityClient.Services.MortalWoundTreatmentReceipt";
    private const string TreatmentOutcomeIntentTypeName =
        "BookOfEternityClient.Services.MortalWoundTreatmentOutcomeIntent";
    private const string TreatmentModeAuthorityTypeName =
        "BookOfEternityClient.Services.MortalWoundTreatmentModeAuthority";
    private const string TreatmentModeEvidenceTypeName =
        "BookOfEternityClient.Services.MortalWoundTreatmentModeEvidence";
    private const string ResourceReservationAuthorityTypeName =
        "BookOfEternityClient.Services.MortalWoundTreatmentResourceReservationAuthority";

    [Fact]
    public void ProcedureCheckAuthority_SurfaceIsClosedAndFactoryIsExact()
    {
        var authorityType = RequireProcedureCheckAuthorityType();
        AssertImmutableConcreteSurface(authorityType, new[]
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
        AssertImmutableConcreteSurface(
            create.ReturnType,
            new[] { "IsValid", "Issues", "Authority" });
        Assert.Equal(typeof(bool), create.ReturnType.GetProperty("IsValid")!.PropertyType);
        Assert.True(typeof(IReadOnlyList<ValidationIssue>).IsAssignableFrom(
            create.ReturnType.GetProperty("Issues")!.PropertyType));
        Assert.Equal(authorityType, create.ReturnType.GetProperty("Authority")!.PropertyType);

        var contributionType = SequenceElementType(
            authorityType.GetProperty("RollContributions")!.PropertyType);
        AssertImmutableConcreteSurface(
            contributionType,
            new[] { "EffectId", "ComponentId", "Contribution" });
        var preparedType = authorityType.GetProperty("PreparedCriticalReaction")!.PropertyType;
        AssertImmutableConcreteSurface(preparedType, new[]
        {
            "EffectId", "TriggerId", "AcceptedEffectFingerprint", "PreparedReactionFingerprint"
        });

        AssertNoPublicConstructionFactories(authorityType, "Create");
        AssertNoPublicConstructionFactories(create.ReturnType);
        AssertNoPublicConstructionFactories(contributionType);
        AssertNoPublicConstructionFactories(preparedType);

        var release = ExactInstanceMethod(
            authorityType,
            "ReleaseProvisionalReservations",
            1);
        Assert.Equal(typeof(bool), release.ReturnType);
        Assert.Equal(
            typeof(MortalWoundTreatmentAcceptedStateAuthority),
            Assert.Single(release.GetParameters()).ParameterType);
    }

    [Fact]
    public void ProcedureCheckAuthority_CriticalReactionResultAndIntentSurfacesAreExact()
    {
        var resultType = RequireServiceType(CriticalReactionResultTypeName);
        var intentType = RequireServiceType(CriticalReactionIntentTypeName);

        AssertImmutableConcreteSurface(
            resultType,
            new[] { "IsValid", "Issues", "Intent" });
        Assert.Equal(typeof(bool), resultType.GetProperty("IsValid")!.PropertyType);
        Assert.True(typeof(IReadOnlyList<ValidationIssue>).IsAssignableFrom(
            resultType.GetProperty("Issues")!.PropertyType));
        Assert.Equal(intentType, resultType.GetProperty("Intent")!.PropertyType);
        AssertImmutableConcreteSurface(intentType, new[]
        {
            "EventType", "EventRef", "CausalEventRef", "Turn", "Realm", "TargetKind",
            "TargetId", "EffectId", "TriggerId", "AcceptedEffectFingerprint",
            "PreparedReactionFingerprint", "RequestFingerprint", "IntentFingerprint"
        });
        AssertNoPublicConstructionFactories(resultType);
        AssertNoPublicConstructionFactories(intentType);
    }

    [Fact]
    public void ProcedureCheckAuthority_CriticalReactionTypedSeamIsExact()
    {
        var requestType = RequireServiceType(AttemptRequestTypeName);
        var acceptedStateType = typeof(MortalWoundTreatmentAcceptedStateAuthority);
        var resultType = RequireServiceType(CriticalReactionResultTypeName);
        var catalogType = typeof(EffectAcceptedEventReportCatalog);

        var seam = Assert.Single(catalogType.GetMethods(
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic),
            static method => string.Equals(
                method.Name,
                "ResolvePreparedMortalWoundCriticalReaction",
                StringComparison.Ordinal));
        Assert.Equal(new[] { requestType, acceptedStateType },
            seam.GetParameters().Select(static parameter => parameter.ParameterType));
        Assert.Equal(resultType, seam.ReturnType);
    }

    public static TheoryData<string, string[]> ProcedureCheckAuthorityShellRows => new()
    {
        {
            AttemptRequestResultTypeName,
            new[] { "IsValid", "Issues", "Request" }
        },
        {
            AttemptRequestTypeName,
            new[]
            {
                "Mode", "Coordinates", "MilestoneOrdinal", "ModeAuthority",
                "RequirementAuthority", "ResourceAuthority", "RequestFingerprint"
            }
        },
        {
            TreatmentResolutionResultTypeName,
            new[] { "Disposition", "Issues", "Resolution", "ReplayReceipt" }
        },
        {
            TreatmentResolutionTypeName,
            new[]
            {
                "Mode", "Coordinates", "AttemptDisposition", "ResultCategory",
                "SelectedOutcomeIndex", "Interruption", "DeclaredResult", "OutcomeIntents",
                "CriticalReactionIntent", "ConsumptionTrigger", "CourseId",
                "CourseMilestoneOrdinal", "CourseDisposition", "RequestAuthority",
                "RequirementAuthority", "ResourceAuthority", "ModeEvidence", "RouteFingerprint",
                "ResolutionAuthorityFingerprint", "RequestFingerprint", "ResultFingerprint",
                "RouteCompletion"
            }
        },
        {
            TreatmentReceiptTypeName,
            new[]
            {
                "Mode", "Coordinates", "AttemptDisposition", "ResultCategory",
                "SelectedOutcomeIndex", "Interruption", "DeclaredResult", "ConsumptionTrigger",
                "CourseId", "CourseMilestoneOrdinal", "CourseDisposition",
                "RequirementAuthorityFingerprint", "ResourceAuthorityFingerprint", "ModeEvidence",
                "RouteFingerprint", "ResolutionAuthorityFingerprint", "RequestFingerprint",
                "ResultFingerprint", "RouteCompletion", "ReceiptFingerprint"
            }
        },
        {
            ResourceReservationAuthorityTypeName,
            new[]
            {
                "ReservationDisposition", "ReservationId", "CoordinatesFingerprint",
                "AcceptedStateFingerprint", "RouteFingerprint", "CourseId",
                "CourseMilestoneOrdinal", "CourseCoordinateFingerprint",
                "RequirementAuthorityFingerprint", "Policy", "Claims", "AuthorityFingerprint"
            }
        }
    };

    [Theory]
    [MemberData(nameof(ProcedureCheckAuthorityShellRows))]
    public void ProcedureCheckAuthority_PhaseAShellSurfaceIsClosedImmutableAndProductionOnly(
        string typeName,
        string[] expectedProperties)
    {
        var type = RequireServiceType(typeName);

        AssertImmutableConcreteSurface(type, expectedProperties);
        AssertNoPublicConstructionFactories(type);
    }

    [Fact]
    public void ProcedureCheckAuthority_ModeAuthorityClosedUnionIsExact()
    {
        var modeAuthorityType = RequireServiceType(TreatmentModeAuthorityTypeName);
        var procedureType = RequireProcedureCheckAuthorityType();

        AssertClosedAbstractUnionBase(modeAuthorityType, Array.Empty<string>());
        AssertNoPublicConstructionFactories(modeAuthorityType);

        Assert.True(modeAuthorityType.IsAssignableFrom(procedureType));
        Assert.True(modeAuthorityType.IsAssignableFrom(typeof(MortalWoundCourseModeAuthority)));
        Assert.True(modeAuthorityType.IsAssignableFrom(typeof(MortalWoundTreatmentCapabilityProof)));
        Assert.Equal(
            new[]
            {
                procedureType,
                typeof(MortalWoundCourseModeAuthority),
                typeof(MortalWoundTreatmentCapabilityProof)
            }.OrderBy(static type => type.FullName, StringComparer.Ordinal),
            DirectConcreteSubtypes(modeAuthorityType)
                .OrderBy(static type => type.FullName, StringComparer.Ordinal));
        foreach (var authorityType in DirectConcreteSubtypes(modeAuthorityType))
        {
            AssertImmutableConcreteSurface(
                authorityType,
                authorityType.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                    .Where(static property => property.GetIndexParameters().Length == 0)
                    .Select(static property => property.Name));
            AssertNoPublicConstructionFactories(authorityType, "Create");
        }
    }

    [Fact]
    public void ProcedureCheckAuthority_ModeEvidenceClosedUnionIsExact()
    {
        var modeEvidenceType = RequireServiceType(TreatmentModeEvidenceTypeName);

        AssertClosedAbstractUnionBase(modeEvidenceType, Array.Empty<string>());
        AssertNoPublicConstructionFactories(modeEvidenceType);

        AssertClosedDerivedSurfaceSet(modeEvidenceType, new[]
        {
            new[]
            {
                "RollMode", "RollActorKind", "RollActorId", "SourceIndices", "SourceRolls",
                "SelectedSourceIndex", "NaturalRoll", "Modifier", "Total", "BaseDifficulty",
                "ComplicationDifficultyModifier", "EffectiveDifficulty", "Margin",
                "OriginalOutcome", "ResolvedOutcome", "SelectedBandId", "SelectedOutcomeIndex",
                "ReactionEffectId", "ReactionTriggerId", "ReactionFingerprint",
                "AcceptedRollFingerprint"
            },
            new[]
            {
                "CourseId", "MilestoneOrdinal", "CourseStartedAtGameTimeMinutes",
                "ResolvedAtGameTimeMinutes", "ClockEvidenceFingerprint", "CourseDisposition"
            },
            new[]
            {
                "CapabilityRef", "ActorRole", "SkillId", "SourceSemanticFingerprint",
                "CapabilityProofFingerprint"
            }
        });
    }

    [Fact]
    public void ProcedureCheckAuthority_OutcomeIntentClosedUnionAndPayloadsAreExact()
    {
        var outcomeIntentType = RequireServiceType(TreatmentOutcomeIntentTypeName);

        AssertClosedAbstractUnionBase(outcomeIntentType, new[]
        {
            "OperationOrdinal", "Kind", "DeclaredOperationFingerprint", "IntentFingerprint"
        });
        AssertNoPublicConstructionFactories(outcomeIntentType);

        AssertClosedDerivedSurfaceSet(outcomeIntentType, new[]
        {
            OutcomeIntentProperties(),
            OutcomeIntentProperties(),
            OutcomeIntentProperties("Points"),
            OutcomeIntentProperties("Steps"),
            OutcomeIntentProperties("ComplicationId"),
            OutcomeIntentProperties("PolicyRef", "DeteriorationAuthorityFingerprint"),
            OutcomeIntentProperties(
                "ComplicationRef",
                "ComplicationId",
                "DefinitionReferenceBindings",
                "ApplicationReferenceBindings",
                "PreparationFingerprint"),
            OutcomeIntentProperties(
                "HealChildCoordinates",
                "LegacySeedBindings",
                "PreparationFingerprint")
        });
        AssertOutcomeIntentPayloadSurfaces(outcomeIntentType);
    }

    [Fact]
    public void ProcedureCheckAuthority_ResourceReservationForwardPayloadsAreExact()
    {
        var resourceType = RequireServiceType(ResourceReservationAuthorityTypeName);

        AssertImmutableConcreteSurface(resourceType, new[]
        {
            "ReservationDisposition", "ReservationId", "CoordinatesFingerprint",
            "AcceptedStateFingerprint", "RouteFingerprint", "CourseId",
            "CourseMilestoneOrdinal", "CourseCoordinateFingerprint",
            "RequirementAuthorityFingerprint", "Policy", "Claims", "AuthorityFingerprint"
        });
        AssertNoPublicConstructionFactories(resourceType);
        AssertResourceReservationPayloadSurfaces(resourceType);
    }

    [Fact]
    public void ProcedureCheckAuthority_TypedHandoffPropertiesAreExact()
    {
        var modeAuthorityType = RequireServiceType(TreatmentModeAuthorityTypeName);
        var modeEvidenceType = RequireServiceType(TreatmentModeEvidenceTypeName);
        var outcomeIntentType = RequireServiceType(TreatmentOutcomeIntentTypeName);
        var resourceType = RequireServiceType(ResourceReservationAuthorityTypeName);
        var requestType = RequireServiceType(AttemptRequestTypeName);
        var resolutionType = RequireServiceType(TreatmentResolutionTypeName);
        var receiptType = RequireServiceType(TreatmentReceiptTypeName);

        Assert.Equal(modeAuthorityType, requestType.GetProperty("ModeAuthority")!.PropertyType);
        Assert.Equal(resourceType, requestType.GetProperty("ResourceAuthority")!.PropertyType);
        Assert.Equal(modeEvidenceType, resolutionType.GetProperty("ModeEvidence")!.PropertyType);
        Assert.Equal(resourceType, resolutionType.GetProperty("ResourceAuthority")!.PropertyType);
        Assert.Equal(modeEvidenceType, receiptType.GetProperty("ModeEvidence")!.PropertyType);
        Assert.Equal(
            typeof(IReadOnlyList<>).MakeGenericType(outcomeIntentType),
            resolutionType.GetProperty("OutcomeIntents")!.PropertyType);

        var requestResultType = RequireServiceType(AttemptRequestResultTypeName);
        var resolutionResultType = RequireServiceType(TreatmentResolutionResultTypeName);
        Assert.Equal(typeof(bool), requestResultType.GetProperty("IsValid")!.PropertyType);
        AssertValidationIssueSequence(requestResultType.GetProperty("Issues")!.PropertyType);
        Assert.Equal(requestType, requestResultType.GetProperty("Request")!.PropertyType);
        Assert.Equal(typeof(string), requestType.GetProperty("Mode")!.PropertyType);
        Assert.Equal(
            typeof(MortalWoundTreatmentAttemptCoordinates),
            requestType.GetProperty("Coordinates")!.PropertyType);
        Assert.Equal(typeof(int?), requestType.GetProperty("MilestoneOrdinal")!.PropertyType);
        Assert.Equal(
            typeof(MortalWoundTreatmentRequirementAuthorityBundle),
            requestType.GetProperty("RequirementAuthority")!.PropertyType);
        Assert.Equal(typeof(string), requestType.GetProperty("RequestFingerprint")!.PropertyType);

        Assert.Equal(typeof(string), resolutionResultType.GetProperty("Disposition")!.PropertyType);
        AssertValidationIssueSequence(resolutionResultType.GetProperty("Issues")!.PropertyType);
        Assert.Equal(
            resolutionType,
            resolutionResultType.GetProperty("Resolution")!.PropertyType);
        Assert.Equal(
            receiptType,
            resolutionResultType.GetProperty("ReplayReceipt")!.PropertyType);
        Assert.Equal(
            typeof(MortalWoundTreatmentAttemptCoordinates),
            resolutionType.GetProperty("Coordinates")!.PropertyType);
        Assert.Equal(
            requestType,
            resolutionType.GetProperty("RequestAuthority")!.PropertyType);
        Assert.Equal(
            typeof(MortalWoundTreatmentRequirementAuthorityBundle),
            resolutionType.GetProperty("RequirementAuthority")!.PropertyType);
        Assert.Equal(
            RequireServiceType(CriticalReactionIntentTypeName),
            resolutionType.GetProperty("CriticalReactionIntent")!.PropertyType);
        Assert.Equal(
            typeof(MortalWoundTreatmentAttemptCoordinates),
            receiptType.GetProperty("Coordinates")!.PropertyType);
        AssertNoObjectOrJsonProperties(requestType);
        AssertNoObjectOrJsonProperties(resolutionType);
        AssertNoObjectOrJsonProperties(receiptType);
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
    public void ProcedureCheckAuthority_FingerprintsRecomputeFromCompleteOrderedEvidence()
    {
        var reactionScenario = CreateScenario(
            "procedure_player_natural_one_reserves_oldest_fate_shield",
            "procedure");
        reactionScenario.AcceptedState["acceptedDice"] = new JsonArray(1, 19);
        using var reactionFixture = AcceptedStateFixture.Create(reactionScenario);
        reactionFixture.ReplacePlayerProcedureRollEffectsAndFate(
            "fingerprint_complete_ordered_evidence",
            "disadvantage");
        var reaction = InvokeProcedureCheckAuthority(
            reactionFixture,
            reactionScenario.OperationKey,
            reactionScenario.RouteId);
        var reactionAuthority = AssertValidProcedureCheckAuthority(reaction);
        Assert.NotNull(ReadPropertyAllowingNull(
            reactionAuthority,
            "PreparedCriticalReaction"));
        Assert.NotEmpty(AsObjects(ReadRequiredProperty(
            reactionAuthority,
            "RollContributions")));
        AssertProcedureAuthorityFingerprints(reactionAuthority);

        var noReactionScenario = CreateScenario(
            "procedure_normal_uses_lowest_free_die",
            "procedure");
        using var noReactionFixture = AcceptedStateFixture.Create(noReactionScenario);
        var noReaction = InvokeProcedureCheckAuthority(
            noReactionFixture,
            noReactionScenario.OperationKey,
            noReactionScenario.RouteId);
        var noReactionAuthority = AssertValidProcedureCheckAuthority(noReaction);
        Assert.Null(ReadPropertyAllowingNull(
            noReactionAuthority,
            "PreparedCriticalReaction"));
        AssertProcedureAuthorityFingerprints(noReactionAuthority);
    }

    [Fact]
    public void ProcedureCheckAuthority_ExhaustedOneDiePoolCanReleaseAndReuseProductionClaim()
    {
        var scenario = CreateScenario(
            "procedure_normal_uses_lowest_free_die",
            "procedure");
        scenario.AcceptedState["acceptedDice"] = new JsonArray(17);
        using var fixture = AcceptedStateFixture.Create(scenario);

        var first = InvokeProcedureCheckAuthority(
            fixture,
            scenario.OperationKey + "_held",
            scenario.RouteId);
        var firstAuthority = AssertValidProcedureCheckAuthority(first);
        Assert.Equal(new[] { 0 }, ReadIntSequence(ReadRequiredProperty(
            firstAuthority,
            "SourceIndices")));

        var exhausted = InvokeProcedureCheckAuthority(
            fixture,
            scenario.OperationKey + "_exhausted",
            scenario.RouteId);
        AssertInvalidProcedureCheckAuthority(exhausted);

        var release = ExactInstanceMethod(
            firstAuthority.GetType(),
            "ReleaseProvisionalReservations",
            1);
        Assert.Equal(typeof(bool), release.ReturnType);
        Assert.True(Assert.IsType<bool>(InvokeInstance(
            release,
            firstAuthority,
            new object?[] { first.AcceptedState })));

        var reused = InvokeProcedureCheckAuthority(
            fixture,
            scenario.OperationKey + "_after_release",
            scenario.RouteId);
        var reusedAuthority = AssertValidProcedureCheckAuthority(reused);
        Assert.Equal(new[] { 0 }, ReadIntSequence(ReadRequiredProperty(
            reusedAuthority,
            "SourceIndices")));
    }

    [Fact]
    public void ProcedureCheckAuthority_SameOperationChangedRouteConflictsWithoutConsumingNextDie()
    {
        const string fixedRouteId = "procedure_t067a_fixed_zero_conflict";
        var scenario = CreateScenario(
            "procedure_advantage_uses_two_contiguous_dice",
            "procedure");
        scenario.AcceptedState["acceptedDice"] = new JsonArray(4, 19, 7);
        var fixedRoute = scenario.Before["treatment"]!["routes"]![0]!
            .DeepClone()
            .AsObject();
        fixedRoute["routeId"] = fixedRouteId;
        fixedRoute["resolution"]!["modifierSource"] = new JsonObject
        {
            ["kind"] = "fixed_zero"
        };
        scenario.Before["treatment"]!["routes"]!.AsArray().Add(fixedRoute);
        scenario.Before["treatment"]!["knownRouteIds"]!.AsArray().Add(fixedRouteId);
        using var fixture = AcceptedStateFixture.Create(scenario);

        var firstInputs = PrepareProcedureCheckAuthorityInputs(
            fixture,
            scenario.OperationKey,
            scenario.RouteId);
        var conflictInputs = PrepareProcedureCheckAuthorityInputs(
            fixture,
            scenario.OperationKey,
            fixedRouteId);
        var distinctInputs = PrepareProcedureCheckAuthorityInputs(
            fixture,
            scenario.OperationKey + "_distinct",
            fixedRouteId);

        var first = InvokePreparedProcedureCheckAuthority(firstInputs);
        var firstAuthority = AssertValidProcedureCheckAuthority(first);
        Assert.Equal("advantage", Convert.ToString(ReadRequiredProperty(
            firstAuthority,
            "RollMode")));
        Assert.Equal(new[] { 0, 1 }, ReadIntSequence(ReadRequiredProperty(
            firstAuthority,
            "SourceIndices")));

        var conflict = InvokePreparedProcedureCheckAuthority(conflictInputs);
        Assert.NotEqual(
            first.Coordinates.CoordinatesFingerprint,
            conflict.Coordinates.CoordinatesFingerprint);
        AssertInvalidProcedureCheckAuthority(conflict);

        var distinct = InvokePreparedProcedureCheckAuthority(distinctInputs);
        var distinctAuthority = AssertValidProcedureCheckAuthority(distinct);
        Assert.Equal("normal", Convert.ToString(ReadRequiredProperty(
            distinctAuthority,
            "RollMode")));
        Assert.Equal(new[] { 2 }, ReadIntSequence(ReadRequiredProperty(
            distinctAuthority,
            "SourceIndices")));
        Assert.Equal(new[] { 7 }, ReadIntSequence(ReadRequiredProperty(
            distinctAuthority,
            "SourceRolls")));
    }

    [Fact]
    public async Task ProcedureCheckAuthority_IndependentRootsAndGenerationsBeginAtSourceZero()
    {
        var scenario = CreateScenario(
            "procedure_normal_uses_lowest_free_die",
            "procedure");
        scenario.AcceptedState["acceptedDice"] = new JsonArray(17);
        using var firstRoot = AcceptedStateFixture.Create(scenario);
        using var secondRoot = AcceptedStateFixture.Create(scenario);
        using var rotatedGeneration = AcceptedStateFixture.Create(scenario);

        var firstInputs = PrepareProcedureCheckAuthorityInputs(
            firstRoot,
            scenario.OperationKey + "_first_root",
            scenario.RouteId);
        var secondInputs = PrepareProcedureCheckAuthorityInputs(
            secondRoot,
            scenario.OperationKey + "_second_root",
            scenario.RouteId);
        var beforeRotationInputs = PrepareProcedureCheckAuthorityInputs(
            rotatedGeneration,
            scenario.OperationKey + "_before_rotation",
            scenario.RouteId);
        var beforeRotationAuthority = AssertValidProcedureCheckAuthority(
            InvokePreparedProcedureCheckAuthority(beforeRotationInputs));
        Assert.Equal(new[] { 0 }, ReadIntSequence(ReadRequiredProperty(
            beforeRotationAuthority,
            "SourceIndices")));
        await rotatedGeneration.RotateGenerationAndPrepareFreshSnapshotAsync(
            "procedure_registry_generation_rotation");
        var rotatedInputs = PrepareProcedureCheckAuthorityInputs(
            rotatedGeneration,
            scenario.OperationKey + "_rotated_generation",
            scenario.RouteId);

        var firstAuthority = AssertValidProcedureCheckAuthority(
            InvokePreparedProcedureCheckAuthority(firstInputs));
        var secondAuthority = AssertValidProcedureCheckAuthority(
            InvokePreparedProcedureCheckAuthority(secondInputs));
        Assert.Equal(new[] { 0 }, ReadIntSequence(ReadRequiredProperty(
            firstAuthority,
            "SourceIndices")));
        Assert.Equal(new[] { 0 }, ReadIntSequence(ReadRequiredProperty(
            secondAuthority,
            "SourceIndices")));

        var rotatedAuthority = AssertValidProcedureCheckAuthority(
            InvokePreparedProcedureCheckAuthority(rotatedInputs));
        Assert.Equal(new[] { 0 }, ReadIntSequence(ReadRequiredProperty(
            rotatedAuthority,
            "SourceIndices")));
    }

    [Fact]
    public void ProcedureCheckAuthority_StaleDisposedLeaseRejectsWithoutThrowing()
    {
        var scenario = CreateScenario(
            "procedure_normal_uses_lowest_free_die",
            "procedure");
        using var fixture = AcceptedStateFixture.Create(scenario);
        var prepared = PrepareProcedureCheckAuthorityInputs(
            fixture,
            scenario.OperationKey,
            scenario.RouteId);
        fixture.ReleaseLeaseForExternalDistribution();

        ProcedureAuthorityInvocation? invocation = null;
        var exception = Record.Exception(() =>
            invocation = InvokePreparedProcedureCheckAuthority(prepared));

        Assert.Null(exception);
        AssertInvalidProcedureCheckAuthority(Assert.IsType<ProcedureAuthorityInvocation>(
            invocation));
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
    public void ProcedureCheckAuthority_PlayerNonNaturalOneDoesNotReserveEligibleFateCandidate()
    {
        var scenario = CreateScenario(
            "procedure_player_natural_one_reserves_oldest_fate_shield",
            "procedure");
        scenario.AcceptedState["acceptedDice"] = new JsonArray(2);
        using var fixture = AcceptedStateFixture.Create(scenario);

        var invocation = InvokeProcedureCheckAuthority(
            fixture,
            scenario.OperationKey,
            scenario.RouteId);
        var authority = AssertValidProcedureCheckAuthority(invocation);

        Assert.Equal(2, Convert.ToInt32(ReadRequiredProperty(authority, "NaturalRoll")));
        Assert.Null(ReadPropertyAllowingNull(authority, "PreparedCriticalReaction"));
    }

    [Fact]
    public void ProcedureCheckAuthority_TiedFateChronologySelectsOrdinalEffectId()
    {
        var scenario = CreateScenario(
            "procedure_player_natural_one_reserves_oldest_fate_shield",
            "procedure");
        using var fixture = AcceptedStateFixture.Create(scenario);
        fixture.ReplacePlayerFateEffects(
            "tied_fate_chronology",
            new ProcedureFateEffectSeed("effect_fate_zulu", 30, Eligible: true),
            new ProcedureFateEffectSeed("effect_fate_alpha", 30, Eligible: true));

        var authority = AssertValidProcedureCheckAuthority(
            InvokeProcedureCheckAuthority(
                fixture,
                scenario.OperationKey,
                scenario.RouteId));

        Assert.Equal(
            "effect_fate_alpha",
            ReadPreparedFateEffectIdFromAuthority(authority));
    }

    [Fact]
    public void ProcedureCheckAuthority_IneligibleOlderFateIsSkippedForEligibleCandidate()
    {
        var scenario = CreateScenario(
            "procedure_player_natural_one_reserves_oldest_fate_shield",
            "procedure");
        using var fixture = AcceptedStateFixture.Create(scenario);
        fixture.ReplacePlayerFateEffects(
            "ineligible_older_fate",
            new ProcedureFateEffectSeed("effect_fate_a_ineligible", 29, Eligible: false),
            new ProcedureFateEffectSeed("effect_fate_z_eligible", 30, Eligible: true));

        var authority = AssertValidProcedureCheckAuthority(
            InvokeProcedureCheckAuthority(
                fixture,
                scenario.OperationKey,
                scenario.RouteId));

        Assert.Equal(
            "effect_fate_z_eligible",
            ReadPreparedFateEffectIdFromAuthority(authority));
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

    [Fact]
    public void ProcedureCheckAuthority_LegacyAndTreatmentSourcesDelegateToSharedFateArbiter()
    {
        var legacySource = File.ReadAllText(Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "Services",
            "EffectAcceptedEventReportCatalog.cs"));
        var treatmentPath = Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "Services",
            "MortalWoundProcedureCheckAuthority.cs");

        Assert.DoesNotContain(
            "private static bool IsEligibleFateShield",
            legacySource,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "private static int ReadCreatedAtTurn",
            legacySource,
            StringComparison.Ordinal);
        Assert.Contains(
            "FateShieldReactionArbiter.",
            legacySource,
            StringComparison.Ordinal);
        Assert.True(
            File.Exists(treatmentPath),
            "T067-A treatment authority source is absent.");
        Assert.Contains(
            "FateShieldReactionArbiter.",
            File.ReadAllText(treatmentPath),
            StringComparison.Ordinal);
    }

    private static ProcedureAuthorityInvocation InvokeProcedureCheckAuthority(
        AcceptedStateFixture fixture,
        string operationKey,
        string routeId)
    {
        return InvokePreparedProcedureCheckAuthority(
            PrepareProcedureCheckAuthorityInputs(fixture, operationKey, routeId));
    }

    private static PreparedProcedureAuthorityInputs PrepareProcedureCheckAuthorityInputs(
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

        return new PreparedProcedureAuthorityInputs(
            acceptedState,
            coordinates,
            route,
            before,
            requirementAuthority);
    }

    private static ProcedureAuthorityInvocation InvokePreparedProcedureCheckAuthority(
        PreparedProcedureAuthorityInputs prepared)
    {
        var authorityType = RequireProcedureCheckAuthorityType();
        var create = ExactStaticMethod(authorityType, "Create", 5);
        var result = Invoke(create, new object?[]
        {
            prepared.Coordinates,
            prepared.Route,
            prepared.Before,
            prepared.RequirementAuthority,
            prepared.AcceptedState
        });
        return new ProcedureAuthorityInvocation(
            result,
            prepared.AcceptedState,
            prepared.Coordinates,
            prepared.Route,
            prepared.Before,
            prepared.RequirementAuthority);
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

    private static void AssertInvalidProcedureCheckAuthority(
        ProcedureAuthorityInvocation invocation)
    {
        AssertClosedProperties(invocation.Result, new[] { "IsValid", "Issues", "Authority" });
        Assert.False(Assert.IsType<bool>(ReadRequiredProperty(invocation.Result, "IsValid")));
        Assert.Null(ReadPropertyAllowingNull(invocation.Result, "Authority"));
        var issues = ReadRequiredProperty(invocation.Result, "Issues");
        Assert.NotEmpty(AsObjects(issues));
        AssertFrozenSequence(issues, allowEmptyArray: false);
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

    private static void AssertProcedureAuthorityFingerprints(object authority)
    {
        var prepared = ReadPropertyAllowingNull(authority, "PreparedCriticalReaction");
        string? preparedFingerprint = null;
        if (prepared is not null)
        {
            preparedFingerprint = WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
            {
                "book_of_eternity.mortal_wound_treatment.prepared_critical_reaction",
                "1",
                Invariant(ReadRequiredProperty(prepared, "EffectId")),
                Invariant(ReadRequiredProperty(prepared, "TriggerId")),
                Invariant(ReadRequiredProperty(prepared, "AcceptedEffectFingerprint")),
                Invariant(ReadRequiredProperty(authority, "CoordinatesFingerprint")),
                Invariant(ReadRequiredProperty(authority, "AcceptedStateFingerprint"))
            });
            Assert.Equal(
                preparedFingerprint,
                Invariant(ReadRequiredProperty(prepared, "PreparedReactionFingerprint")));
        }

        var fields = new List<string?>
        {
            "book_of_eternity.mortal_wound_treatment.procedure_check_authority",
            "1",
            Invariant(ReadRequiredProperty(authority, "SourcePath")),
            Invariant(ReadRequiredProperty(authority, "RollMode")),
            Invariant(ReadRequiredProperty(authority, "RollActorKind")),
            Invariant(ReadRequiredProperty(authority, "RollActorId"))
        };
        var contributions = AsObjects(ReadRequiredProperty(authority, "RollContributions"));
        fields.Add(contributions.Length.ToString(CultureInfo.InvariantCulture));
        for (var index = 0; index < contributions.Length; index++)
        {
            fields.Add(index.ToString(CultureInfo.InvariantCulture));
            fields.Add(Invariant(ReadRequiredProperty(contributions[index], "EffectId")));
            fields.Add(Invariant(ReadRequiredProperty(contributions[index], "ComponentId")));
            fields.Add(Invariant(ReadRequiredProperty(contributions[index], "Contribution")));
        }

        var sourceIndices = ReadIntSequence(ReadRequiredProperty(authority, "SourceIndices"));
        var sourceRolls = ReadIntSequence(ReadRequiredProperty(authority, "SourceRolls"));
        Assert.Equal(sourceIndices.Length, sourceRolls.Length);
        fields.Add(sourceIndices.Length.ToString(CultureInfo.InvariantCulture));
        for (var index = 0; index < sourceIndices.Length; index++)
        {
            fields.Add(index.ToString(CultureInfo.InvariantCulture));
            fields.Add(sourceIndices[index].ToString(CultureInfo.InvariantCulture));
            fields.Add(sourceRolls[index].ToString(CultureInfo.InvariantCulture));
        }

        foreach (var property in new[]
                 {
                     "SelectedSourceIndex", "NaturalRoll", "Modifier",
                     "ComplicationDifficultyModifier", "EffectiveDifficulty",
                     "RequirementAuthorityFingerprint", "CoordinatesFingerprint",
                     "AcceptedStateFingerprint"
                 })
        {
            fields.Add(Invariant(ReadRequiredProperty(authority, property)));
        }
        fields.Add(preparedFingerprint);

        Assert.Equal(
            WoundAcceptedTurnFingerprintWriter.Compute(fields),
            Invariant(ReadRequiredProperty(authority, "AuthorityFingerprint")));
    }

    private static string Invariant(object value) =>
        Convert.ToString(value, CultureInfo.InvariantCulture)!;

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

    private static Type RequireServiceType(string fullName)
    {
        var type = typeof(WoundMaterializationContract).Assembly.GetType(
            fullName,
            throwOnError: false,
            ignoreCase: false);
        Assert.True(type is not null, $"T067 production type '{fullName}' is absent.");
        return type!;
    }

    private static void AssertImmutableConcreteSurface(
        Type type,
        IEnumerable<string> expected)
    {
        Assert.True(type.IsSealed, $"Concrete authority type '{type.FullName}' must be sealed.");
        AssertImmutableSurface(type, expected);
    }

    private static void AssertClosedAbstractUnionBase(
        Type type,
        IEnumerable<string> expected)
    {
        Assert.True(type.IsAbstract && !type.IsInterface,
            $"Closed union base '{type.FullName}' must be an abstract class.");
        AssertImmutableSurface(type, expected);
        var constructors = type.GetConstructors(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.DeclaredOnly);
        Assert.NotEmpty(constructors);
        Assert.All(constructors, constructor => Assert.True(
            constructor.IsFamilyAndAssembly,
            $"Closed union base '{type.FullName}' must use only private-protected constructors."));
    }

    private static void AssertImmutableSurface(
        Type type,
        IEnumerable<string> expected)
    {
        var publicProperties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public);
        Assert.DoesNotContain(publicProperties, static property =>
            property.GetIndexParameters().Length != 0);
        Assert.Empty(type.GetFields(BindingFlags.Instance | BindingFlags.Public));
        AssertClosedPublicInstanceProperties(type, expected);
        Assert.Empty(type.GetConstructors(BindingFlags.Instance | BindingFlags.Public));
        Assert.All(
            publicProperties,
            property => Assert.False(
                property.GetSetMethod(nonPublic: true)?.IsPublic == true,
                $"Property '{type.FullName}.{property.Name}' exposes a public setter/init setter."));
    }

    private static void AssertNoPublicConstructionFactories(
        Type type,
        params string[] allowedNames)
    {
        var allowed = allowedNames.ToHashSet(StringComparer.Ordinal);
        var unexpected = type.GetMethods(
                BindingFlags.Static | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(static method => !method.IsSpecialName)
            .Where(method => !allowed.Contains(method.Name))
            .Select(static method => method.Name)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();
        Assert.Empty(unexpected);
    }

    private static Type[] DirectConcreteSubtypes(Type baseType) =>
        baseType.Assembly.GetTypes()
            .Where(type => type.BaseType == baseType && !type.IsAbstract)
            .ToArray();

    private static void AssertClosedDerivedSurfaceSet(
        Type baseType,
        IReadOnlyList<string[]> expectedSurfaces)
    {
        var expected = expectedSurfaces
            .Select(CanonicalPropertySet)
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToArray();
        var derived = DirectConcreteSubtypes(baseType);
        Assert.Equal(expected.Length, derived.Length);
        foreach (var type in derived)
        {
            AssertImmutableConcreteSurface(
                type,
                type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                    .Where(static property => property.GetIndexParameters().Length == 0)
                    .Select(static property => property.Name));
            AssertNoPublicConstructionFactories(type);
        }
        var actual = derived
            .Select(type => CanonicalPropertySet(type
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(static property => property.GetIndexParameters().Length == 0)
                .Select(static property => property.Name)))
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(expected, actual);
    }

    private static string CanonicalPropertySet(IEnumerable<string> properties) =>
        string.Join("\n", properties.OrderBy(static value => value, StringComparer.Ordinal));

    private static string[] OutcomeIntentProperties(params string[] extra) =>
        new[] { "OperationOrdinal", "Kind", "DeclaredOperationFingerprint", "IntentFingerprint" }
            .Concat(extra)
            .ToArray();

    private static void AssertOutcomeIntentPayloadSurfaces(Type baseType)
    {
        var branches = DirectConcreteSubtypes(baseType);
        var addComplication = Assert.Single(branches, type =>
            type.GetProperty("DefinitionReferenceBindings") is not null &&
            type.GetProperty("ComplicationRef") is not null);
        var heal = Assert.Single(branches, type =>
            type.GetProperty("HealChildCoordinates") is not null);

        var definitionBindings = addComplication.GetProperty("DefinitionReferenceBindings")!;
        var applicationBindings = addComplication.GetProperty("ApplicationReferenceBindings")!;
        var referenceBindingType = SequenceElementType(definitionBindings.PropertyType);
        Assert.Equal(referenceBindingType, SequenceElementType(applicationBindings.PropertyType));
        AssertImmutableConcreteSurface(
            referenceBindingType,
            new[] { "LocalRef", "NamespacedRef" });
        AssertNoPublicConstructionFactories(referenceBindingType);

        var childCoordinatesType = heal.GetProperty("HealChildCoordinates")!.PropertyType;
        AssertImmutableConcreteSurface(childCoordinatesType, new[]
        {
            "OperationKey", "TransitionId", "EventRef", "CausalEventRef"
        });
        AssertNoPublicConstructionFactories(childCoordinatesType);

        var seedType = SequenceElementType(heal.GetProperty("LegacySeedBindings")!.PropertyType);
        AssertImmutableConcreteSurface(seedType, new[]
        {
            "LegacyOrdinal", "LocalLegacyRef", "LegacyId", "Kind",
            "DefinitionReferenceBindings", "ApplicationReferenceBindings",
            "DeclaredLegacyFingerprint", "SeedFingerprint"
        });
        Assert.Equal(referenceBindingType, SequenceElementType(
            seedType.GetProperty("DefinitionReferenceBindings")!.PropertyType));
        Assert.Equal(referenceBindingType, SequenceElementType(
            seedType.GetProperty("ApplicationReferenceBindings")!.PropertyType));
        AssertNoPublicConstructionFactories(seedType);
    }

    private static void AssertResourceReservationPayloadSurfaces(Type resourceType)
    {
        var claimType = SequenceElementType(resourceType.GetProperty("Claims")!.PropertyType);
        AssertImmutableConcreteSurface(claimType, new[]
        {
            "Scope", "RequirementIndex", "Kind", "AuthorityRef", "Realm", "OwnerKind",
            "OwnerId", "Quantity", "SuccessWitnessFingerprint", "ClaimFingerprint"
        });
        AssertNoPublicConstructionFactories(claimType);

        var policyType = resourceType.GetProperty("Policy")!.PropertyType;
        AssertImmutableConcreteSurface(policyType, new[]
        {
            "ReserveBeforeResolution", "ConsumeOn", "RefundOn", "Mutations"
        });
        AssertNoPublicConstructionFactories(policyType);
        var mutationType = SequenceElementType(policyType.GetProperty("Mutations")!.PropertyType);
        AssertImmutableConcreteSurface(mutationType, new[]
        {
            "Kind", "Scope", "MilestoneOrdinal", "RequirementIndex"
        });
        AssertNoPublicConstructionFactories(mutationType);
    }

    private static void AssertValidationIssueSequence(Type type) =>
        Assert.True(typeof(IReadOnlyList<ValidationIssue>).IsAssignableFrom(type));

    private static void AssertNoObjectOrJsonProperties(Type type)
    {
        Assert.All(
            type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(static property => property.GetIndexParameters().Length == 0),
            property =>
            {
                Assert.NotEqual(typeof(object), property.PropertyType);
                Assert.False(typeof(JsonNode).IsAssignableFrom(property.PropertyType));
            });
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
        var readOnlyList = valueType.GetInterfaces()
            .Append(valueType)
            .SingleOrDefault(static candidate =>
                candidate.IsGenericType &&
                candidate.GetGenericTypeDefinition() == typeof(IReadOnlyList<>));
        Assert.NotNull(readOnlyList);
        if (value is not IList list)
            return;

        Assert.True(list.IsReadOnly, "Authority collections must reject caller mutation.");
        var elementType = readOnlyList!.GetGenericArguments()[0];
        var candidate = elementType.IsValueType ? Activator.CreateInstance(elementType) : null;
        Assert.Throws<NotSupportedException>(() => list.Add(candidate));
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
        return CreateProcedureEffectState(effects);
    }

    private static (JsonObject Carrier, JsonObject IdentityIndex)
        CreateProcedureRollAndFateEffectState(string contribution)
    {
        var rollState = CreateProcedureRollEffectState(new[] { contribution });
        var effects = rollState.Carrier["activeEffects"]!.AsArray()
            .OfType<JsonObject>()
            .Select(static effect => effect.DeepClone().AsObject())
            .ToList();
        effects.Add(CreateCanonicalFateShield("effect_fate_shield_older"));
        effects.Add(CreateCanonicalFateShield("effect_fate_shield_newer"));
        for (var ordinal = 0; ordinal < effects.Count; ordinal++)
        {
            var turn = 30 + ordinal;
            effects[ordinal]["chronology"]!["createdAtTurn"] = turn;
            effects[ordinal]["chronology"]!["createdEventRef"] =
                $"turn_{turn}:t067a_mixed_effect_seed:{ordinal + 1}";
            effects[ordinal]["chronology"]!["lastTransitionId"] =
                $"effect_transition_t067a_mixed_seed_{ordinal + 1}";
            effects[ordinal]["chronology"]!["lastTransitionTurn"] = turn;
        }
        return CreateProcedureEffectState(effects);
    }

    private static (JsonObject Carrier, JsonObject IdentityIndex)
        CreateProcedureFateEffectState(IReadOnlyList<ProcedureFateEffectSeed> seeds)
    {
        var effects = seeds.Select((seed, ordinal) =>
        {
            var effect = CreateCanonicalFateShield(seed.EffectId);
            if (!seed.Eligible)
            {
                effect["source"] = new JsonObject
                {
                    ["kind"] = "skill",
                    ["sourceId"] = "skill_field_medicine_01",
                    ["definitionKey"] = "t067a-ineligible-fate-source"
                };
            }
            effect["chronology"]!["createdAtTurn"] = seed.CreatedAtTurn;
            effect["chronology"]!["createdEventRef"] =
                $"turn_{seed.CreatedAtTurn}:t067a_fate_seed:{ordinal + 1}";
            effect["chronology"]!["lastTransitionId"] =
                $"effect_transition_t067a_fate_seed_{ordinal + 1}";
            effect["chronology"]!["lastTransitionTurn"] = seed.CreatedAtTurn;
            return effect;
        }).ToArray();
        return CreateProcedureEffectState(effects);
    }

    private static (JsonObject Carrier, JsonObject IdentityIndex)
        CreateProcedureEffectState(IReadOnlyList<JsonObject> effects)
    {
        var index = EffectMaterializationTestFixture.CreateIdentityIndex(effects.ToArray());
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

    private sealed record PreparedProcedureAuthorityInputs(
        MortalWoundTreatmentAcceptedStateAuthority AcceptedState,
        MortalWoundTreatmentAttemptCoordinates Coordinates,
        MortalWoundProcedureRouteDefinition Route,
        WoundMaterializationEnvelope Before,
        MortalWoundTreatmentRequirementAuthorityBundle RequirementAuthority);

    private sealed record ProcedureAuthorityInvocation(
        object Result,
        MortalWoundTreatmentAcceptedStateAuthority AcceptedState,
        MortalWoundTreatmentAttemptCoordinates Coordinates,
        MortalWoundProcedureRouteDefinition Route,
        WoundMaterializationEnvelope Before,
        MortalWoundTreatmentRequirementAuthorityBundle RequirementAuthority);

    private sealed record ProcedureFateEffectSeed(
        string EffectId,
        int CreatedAtTurn,
        bool Eligible);

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

        internal void ReplacePlayerProcedureRollEffectsAndFate(
            string label,
            string contribution)
        {
            var effectState = CreateProcedureRollAndFateEffectState(contribution);
            WriteObject(EffectCarrierCatalog.PlayerPath, effectState.Carrier);
            WriteObject(EffectIdentityState.StatePath, effectState.IdentityIndex);
            PrepareFreshSnapshot(label);
        }

        internal void ReplacePlayerFateEffects(
            string label,
            params ProcedureFateEffectSeed[] seeds)
        {
            var effectState = CreateProcedureFateEffectState(seeds);
            WriteObject(EffectCarrierCatalog.PlayerPath, effectState.Carrier);
            WriteObject(EffectIdentityState.StatePath, effectState.IdentityIndex);
            PrepareFreshSnapshot(label);
        }

        internal async Task RotateGenerationAndPrepareFreshSnapshotAsync(string label)
        {
            await Lease.DisposeAsync();
            await using (var lifecycle =
                         await FileSystem.AcquireSessionLifecycleLeaseAsync())
            await using (var replacement =
                         await FileSystem.AcquireSessionReplacementWriteLeaseAsync(lifecycle))
            {
                FileSystem.RotateSessionGeneration(replacement);
            }

            var request = JsonNode.Parse(File.ReadAllText(FileSystem.ResolvePath(
                LiveTurnPreparationService.TurnRequestPath)))!.AsObject();
            var prepared = await new LiveTurnPreparationService(FileSystem).PrepareAsync(
                new LiveTurnPreparationOptions
                {
                    SessionId = request["sessionId"]!.GetValue<string>(),
                    RequestId = request["requestId"]!.GetValue<string>() + "_" + label,
                    TurnNumber = request["turnNumber"]!.GetValue<int>(),
                    PlayerAction = "Refresh accepted-state authority for " + label + ".",
                    CurrentRealm = "Mortal World",
                    PreGeneratedDices1d20 = request["preGeneratedDices1d20"]!.AsArray()
                        .Select(static value => value!.GetValue<int>())
                        .ToArray()
                });
            Assert.Equal(request["turnNumber"]!.GetValue<int>(), prepared.TurnNumber);
            Lease = await FileSystem.AcquireCanonicalWriteLeaseAsync();
        }

        internal JsonObject ReadPlayerEffectCarrier() =>
            ReadObject(EffectCarrierCatalog.PlayerPath);
    }
}
