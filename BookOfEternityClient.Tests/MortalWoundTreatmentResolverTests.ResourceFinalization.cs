using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalWoundTreatmentResolverTests
{
    [Fact]
    public void ResourceFinalization_ProcedureConsumesOnlySelectedSupplyAndReleasesReusableToolWithoutWriting()
    {
        var scenario = CreateScenario(
            "procedure_normal_uses_lowest_free_die",
            "procedure");
        scenario.Before["severity"]!["value"] = "III";
        scenario.Before["severity"]!["rank"] = 3;
        scenario.Before["severity"]!["maximumAtCreation"] = "III";
        scenario.Before["consequences"]!["slotBudget"] = 3;
        scenario.AcceptedState["sterileThreadCount"] = 2;
        scenario.AcceptedState["reusableToolCount"] = 1;
        var route = scenario.Before["treatment"]!["routes"]![0]!.AsObject();
        route["outcomes"]![0]!["result"] = new JsonArray(new JsonObject
        {
            ["kind"] = "reduce_severity",
            ["steps"] = 1
        });
        route["requirements"]!.AsArray().Insert(1, new JsonObject
        {
            ["kind"] = "item_quantity",
            ["itemRef"] = "reusable_field_kit",
            ["quantity"] = 1,
            ["ownerRole"] = "provider"
        });
        route["resolution"]!["modifierSource"]!["requirementIndex"] = 2;
        scenario = scenario with { ExpectedIntentCount = 1 };

        using var fixture = AcceptedStateFixture.Create(scenario);
        var flow = ResolveCurrentTreatment(
            fixture,
            "procedure",
            scenario.OperationKey + "_resource_finalization",
            scenario.RouteId);
        var treeBefore = CaptureResolverFixtureTree(fixture.Root);
        var sterileThreadBefore = fixture.ReadNpcItemCount("sterile_thread");
        var reusableToolBefore = fixture.ReadNpcItemCount("reusable_field_kit");

        var result = Invoke(
            RequireExactResourceFinalizeMethod(),
            new[] { flow.Resolution });

        AssertClosedProperties(result, new[] { "IsValid", "Issues", "Finalization" });
        Assert.True(Convert.ToBoolean(ReadRequiredProperty(result, "IsValid")));
        Assert.Empty(AsObjects(ReadRequiredProperty(result, "Issues")));
        var finalization = ReadRequiredProperty(result, "Finalization");
        AssertClosedProperties(finalization, new[]
        {
            "Disposition", "ReservationId", "RequestFingerprint", "ResultFingerprint",
            "ResourceAuthorityFingerprint", "ConsumptionTrigger", "Consumptions",
            "ReleasedClaimFingerprints", "FinalizationFingerprint"
        });

        var resolution = Assert.IsType<MortalWoundTreatmentResolution>(flow.Resolution);
        var resource = resolution.ResourceAuthority;
        Assert.Equal("consume", ReadRequiredProperty(finalization, "Disposition"));
        Assert.Equal(resource.ReservationId, ReadRequiredProperty(finalization, "ReservationId"));
        Assert.Equal(resolution.RequestFingerprint,
            ReadRequiredProperty(finalization, "RequestFingerprint"));
        Assert.Equal(resolution.ResultFingerprint,
            ReadRequiredProperty(finalization, "ResultFingerprint"));
        Assert.Equal(resource.AuthorityFingerprint,
            ReadRequiredProperty(finalization, "ResourceAuthorityFingerprint"));
        Assert.Equal("success", ReadRequiredProperty(finalization, "ConsumptionTrigger"));

        var consumptionProjection = ReadRequiredProperty(finalization, "Consumptions");
        var consumption = Assert.Single(AsObjects(consumptionProjection));
        AssertClosedProperties(consumption, new[]
        {
            "Scope", "CourseMilestoneOrdinal", "RequirementIndex", "Kind",
            "AuthorityRef", "Realm", "OwnerKind", "OwnerId", "Quantity",
            "ClaimFingerprint", "IntentFingerprint"
        });
        Assert.Equal("common", ReadRequiredProperty(consumption, "Scope"));
        Assert.Null(ReadPropertyAllowingNull(consumption, "CourseMilestoneOrdinal"));
        Assert.Equal(0, ReadRequiredProperty(consumption, "RequirementIndex"));
        Assert.Equal("item_quantity", ReadRequiredProperty(consumption, "Kind"));
        Assert.Equal("sterile_thread", ReadRequiredProperty(consumption, "AuthorityRef"));
        Assert.Equal("mortal_world", ReadRequiredProperty(consumption, "Realm"));
        Assert.Equal("npc", ReadRequiredProperty(consumption, "OwnerKind"));
        Assert.Equal("field_medic_01", ReadRequiredProperty(consumption, "OwnerId"));
        Assert.Equal(1, ReadRequiredProperty(consumption, "Quantity"));
        var sterileClaim = Assert.Single(resource.Claims, claim =>
            claim.AuthorityRef == "sterile_thread");
        Assert.Equal(sterileClaim.ClaimFingerprint,
            ReadRequiredProperty(consumption, "ClaimFingerprint"));
        AssertAuthorityFingerprint(ReadRequiredProperty(consumption, "IntentFingerprint"));

        var releaseProjection = ReadRequiredProperty(
            finalization,
            "ReleasedClaimFingerprints");
        var released = Assert.Single(AsObjects(releaseProjection));
        var reusableClaim = Assert.Single(resource.Claims, claim =>
            claim.AuthorityRef == "reusable_field_kit");
        Assert.Equal(reusableClaim.ClaimFingerprint, released);
        AssertAuthorityFingerprint(ReadRequiredProperty(
            finalization,
            "FinalizationFingerprint"));
        AssertFrozenProjection(consumptionProjection);
        AssertFrozenProjection(releaseProjection);

        AssertResolverFixtureTreeUnchanged(fixture.Root, treeBefore);
        Assert.Equal(sterileThreadBefore, fixture.ReadNpcItemCount("sterile_thread"));
        Assert.Equal(reusableToolBefore, fixture.ReadNpcItemCount("reusable_field_kit"));
    }

    [Fact]
    public void ResourceFinalization_NonConsumingProcedureReleasesAllHeldClaimsAndEmitsNoMutation()
    {
        var scenario = CreateScenario(
            "procedure_disadvantage_uses_two_contiguous_dice",
            "procedure");
        scenario.Before["severity"]!["value"] = "III";
        scenario.Before["severity"]!["rank"] = 3;
        scenario.Before["severity"]!["maximumAtCreation"] = "III";
        scenario.Before["consequences"]!["slotBudget"] = 3;
        scenario.AcceptedState["sterileThreadCount"] = 1;
        scenario.AcceptedState["reusableToolCount"] = 1;
        var route = scenario.Before["treatment"]!["routes"]![0]!.AsObject();
        route["requirements"]!.AsArray().Insert(1, new JsonObject
        {
            ["kind"] = "item_quantity",
            ["itemRef"] = "reusable_field_kit",
            ["quantity"] = 1,
            ["ownerRole"] = "provider"
        });
        route["resolution"]!["modifierSource"]!["requirementIndex"] = 2;
        route["resourcePolicy"]!["consumeOn"] = new JsonArray("success");

        using var fixture = AcceptedStateFixture.Create(scenario);
        var flow = ResolveCurrentTreatment(
            fixture,
            "procedure",
            scenario.OperationKey + "_resource_release_only",
            scenario.RouteId);
        var treeBefore = CaptureResolverFixtureTree(fixture.Root);
        var sterileThreadBefore = fixture.ReadNpcItemCount("sterile_thread");
        var reusableToolBefore = fixture.ReadNpcItemCount("reusable_field_kit");
        var resolution = Assert.IsType<MortalWoundTreatmentResolution>(flow.Resolution);
        Assert.Equal("failed_attempt", resolution.ResultCategory);
        Assert.Equal("none", resolution.ConsumptionTrigger);

        var result = Invoke(
            RequireExactResourceFinalizeMethod(),
            new[] { flow.Resolution });

        Assert.True(Convert.ToBoolean(ReadRequiredProperty(result, "IsValid")));
        Assert.Empty(AsObjects(ReadRequiredProperty(result, "Issues")));
        var finalization = ReadRequiredProperty(result, "Finalization");
        Assert.Equal("release_only", ReadRequiredProperty(finalization, "Disposition"));
        Assert.Equal("none", ReadRequiredProperty(finalization, "ConsumptionTrigger"));
        Assert.Empty(AsObjects(ReadRequiredProperty(finalization, "Consumptions")));
        Assert.Equal(
            resolution.ResourceAuthority.Claims.Select(static claim =>
                claim.ClaimFingerprint),
            AsObjects(ReadRequiredProperty(
                    finalization,
                    "ReleasedClaimFingerprints"))
                .Cast<string>());
        AssertResolverFixtureTreeUnchanged(fixture.Root, treeBefore);
        Assert.Equal(sterileThreadBefore, fixture.ReadNpcItemCount("sterile_thread"));
        Assert.Equal(reusableToolBefore, fixture.ReadNpcItemCount("reusable_field_kit"));
    }

    [Fact]
    public void ResourceFinalization_FirstCourseConsumesOnlyCurrentMilestoneResourceAndReleasesCommonPrecondition()
    {
        var scenario = ConfigureCourseCrossScopeResourceQuantity(
            CreateScenario(
                "course_first_milestone_is_ready_at_inclusive_due_time",
                "course"),
            commonQuantity: 5,
            milestoneQuantity: 5);
        using var fixture = AcceptedStateFixture.Create(scenario);
        fixture.SetCanonicalPlayerHealthForRequirementTest(10);
        var flow = ResolveCurrentTreatment(
            fixture,
            "course",
            scenario.OperationKey + "_current_resource_milestone",
            scenario.RouteId);
        var treeBefore = CaptureResolverFixtureTree(fixture.Root);
        var resolution = Assert.IsType<MortalWoundTreatmentResolution>(flow.Resolution);
        Assert.Equal("success", resolution.ConsumptionTrigger);
        Assert.Equal(1, resolution.CourseMilestoneOrdinal);
        Assert.Equal(
            new[]
            {
                (Scope: "common", Quantity: 5),
                (Scope: "course_milestone", Quantity: 5)
            },
            resolution.ResourceAuthority.Claims.Select(static claim =>
                (claim.Scope, claim.Quantity)));

        var result = Invoke(
            RequireExactResourceFinalizeMethod(),
            new[] { flow.Resolution });

        Assert.True(Convert.ToBoolean(ReadRequiredProperty(result, "IsValid")));
        Assert.Empty(AsObjects(ReadRequiredProperty(result, "Issues")));
        var finalization = ReadRequiredProperty(result, "Finalization");
        Assert.Equal("consume", ReadRequiredProperty(finalization, "Disposition"));
        var consumption = Assert.Single(AsObjects(ReadRequiredProperty(
            finalization,
            "Consumptions")));
        Assert.Equal("course_milestone", ReadRequiredProperty(consumption, "Scope"));
        Assert.Equal(1, ReadRequiredProperty(consumption, "CourseMilestoneOrdinal"));
        Assert.Equal(0, ReadRequiredProperty(consumption, "RequirementIndex"));
        Assert.Equal("resource_quantity", ReadRequiredProperty(consumption, "Kind"));
        Assert.Equal("health", ReadRequiredProperty(consumption, "AuthorityRef"));
        Assert.Equal("player", ReadRequiredProperty(consumption, "OwnerKind"));
        Assert.Equal("player_current", ReadRequiredProperty(consumption, "OwnerId"));
        Assert.Equal(5, ReadRequiredProperty(consumption, "Quantity"));
        var milestoneClaim = Assert.Single(resolution.ResourceAuthority.Claims, claim =>
            claim.Scope == "course_milestone");
        Assert.Equal(milestoneClaim.ClaimFingerprint,
            ReadRequiredProperty(consumption, "ClaimFingerprint"));

        var commonClaim = Assert.Single(resolution.ResourceAuthority.Claims, claim =>
            claim.Scope == "common");
        Assert.Equal(
            new[] { commonClaim.ClaimFingerprint },
            AsObjects(ReadRequiredProperty(
                    finalization,
                    "ReleasedClaimFingerprints"))
                .Cast<string>());
        Assert.DoesNotContain(
            AsObjects(ReadRequiredProperty(finalization, "Consumptions")),
            candidate => Convert.ToInt32(ReadPropertyAllowingNull(
                candidate,
                "CourseMilestoneOrdinal")) > 1);
        AssertResolverFixtureTreeUnchanged(fixture.Root, treeBefore);
    }

    [Fact]
    public void ResourceFinalization_NotRequiredEmitsEmptyStablePlan()
    {
        var scenario = CreateScenario(
            "guaranteed_current_capability_proof_stabilizes",
            "guaranteed");
        using var fixture = AcceptedStateFixture.Create(scenario);
        var flow = ResolveCurrentTreatment(
            fixture,
            "guaranteed",
            scenario.OperationKey + "_not_required",
            scenario.RouteId);
        var treeBefore = CaptureResolverFixtureTree(fixture.Root);
        var resolution = Assert.IsType<MortalWoundTreatmentResolution>(flow.Resolution);
        Assert.Equal("not_required",
            resolution.ResourceAuthority.ReservationDisposition);
        Assert.Null(resolution.ResourceAuthority.ReservationId);
        Assert.Empty(resolution.ResourceAuthority.Claims);

        var first = Invoke(
            RequireExactResourceFinalizeMethod(),
            new[] { flow.Resolution });
        var second = Invoke(
            RequireExactResourceFinalizeMethod(),
            new[] { flow.Resolution });

        Assert.True(Convert.ToBoolean(ReadRequiredProperty(first, "IsValid")));
        Assert.Empty(AsObjects(ReadRequiredProperty(first, "Issues")));
        Assert.Equal(CanonicalValue(first), CanonicalValue(second));
        var finalization = ReadRequiredProperty(first, "Finalization");
        Assert.Equal("not_required", ReadRequiredProperty(finalization, "Disposition"));
        Assert.Null(ReadPropertyAllowingNull(finalization, "ReservationId"));
        Assert.Equal("success", ReadRequiredProperty(finalization, "ConsumptionTrigger"));
        Assert.Empty(AsObjects(ReadRequiredProperty(finalization, "Consumptions")));
        Assert.Empty(AsObjects(ReadRequiredProperty(
            finalization,
            "ReleasedClaimFingerprints")));
        AssertAuthorityFingerprint(ReadRequiredProperty(
            finalization,
            "FinalizationFingerprint"));
        AssertResolverFixtureTreeUnchanged(fixture.Root, treeBefore);
    }

    [Fact]
    public void ResourceFinalization_ResealedSuccessfulProcedureInterruptionRejects()
    {
        var scenario = CreateScenario(
            "procedure_normal_uses_lowest_free_die",
            "procedure");
        using var fixture = AcceptedStateFixture.Create(scenario);
        var flow = ResolveCurrentTreatment(
            fixture,
            "procedure",
            scenario.OperationKey + "_resealed_interruption",
            scenario.RouteId);
        var treeBefore = CaptureResolverFixtureTree(fixture.Root);
        var resolution = Assert.IsType<MortalWoundTreatmentResolution>(flow.Resolution);
        Assert.Equal("success", resolution.ResultCategory);
        Assert.False(resolution.Interruption);

        var resealed = RecreateFinalizationResolution(
            resolution,
            interruption: true,
            consumptionTrigger: "none");
        Assert.True(resealed.Interruption);
        Assert.Equal("none", resealed.ConsumptionTrigger);

        var result = MortalWoundTreatmentResourceComposer.Finalize(resealed);

        Assert.False(result.IsValid);
        var issue = Assert.Single(result.Issues);
        Assert.StartsWith(
            "mortal_wound_treatment_resource_finalization_",
            issue.Code,
            StringComparison.Ordinal);
        Assert.Null(result.Finalization);
        AssertResolverFixtureTreeUnchanged(fixture.Root, treeBefore);
    }

    [Theory]
    [InlineData("request")]
    [InlineData("resource")]
    [InlineData("policy")]
    [InlineData("claim")]
    [InlineData("mode_evidence")]
    [InlineData("resolution")]
    [InlineData("result")]
    [InlineData("resealed_declared_result")]
    public void ResourceFinalization_TamperedNestedOrOuterSealRejects(string axis)
    {
        var scenario = CreateScenario(
            "procedure_normal_uses_lowest_free_die",
            "procedure");
        using var fixture = AcceptedStateFixture.Create(scenario);
        var flow = ResolveCurrentTreatment(
            fixture,
            "procedure",
            scenario.OperationKey + "_tamper_" + axis,
            scenario.RouteId);
        var treeBefore = CaptureResolverFixtureTree(fixture.Root);
        var resolution = Assert.IsType<MortalWoundTreatmentResolution>(flow.Resolution);
        var request = resolution.RequestAuthority;
        var resource = resolution.ResourceAuthority;

        MortalWoundTreatmentResolution tampered;
        switch (axis)
        {
            case "request":
                tampered = RecreateFinalizationResolution(
                    resolution,
                    request: WithInvalidCurrentContextCoordinates(request));
                break;
            case "resource":
            case "policy":
            case "claim":
            {
                var changedResource = TamperFinalizationResourceAuthority(
                    resource,
                    axis);
                var changedRequest = WithFinalizationResourceAuthority(
                    request,
                    changedResource);
                tampered = RecreateFinalizationResolution(
                    resolution,
                    request: changedRequest);
                break;
            }
            case "mode_evidence":
            {
                var evidence = Assert.IsType<MortalWoundProcedureModeEvidence>(
                    resolution.ModeEvidence);
                var changed = TamperFinalizationModeEvidence(evidence);
                tampered = RecreateFinalizationResolution(
                    resolution,
                    modeEvidence: changed);
                break;
            }
            case "resolution":
                tampered = RecreateFinalizationResolution(
                    resolution,
                    attemptDisposition: "Rejected");
                break;
            case "result":
                tampered = RecreateFinalizationResolution(
                    resolution,
                    resultCategory: "partial_success",
                    consumptionTrigger: "partial_success");
                break;
            case "resealed_declared_result":
            {
                var changedDeclared = resolution.DeclaredResult.Reverse().ToArray();
                Assert.NotEqual(
                    resolution.DeclaredResult.Select(static operation => operation.Kind),
                    changedDeclared.Select(static operation => operation.Kind));
                Assert.True(MortalWoundTreatmentOutcomeIntentComposer.TryCompose(
                    request,
                    changedDeclared,
                    Assert.IsType<MortalWoundTreatmentAcceptedStateAuthority>(
                        flow.AcceptedState),
                    out var changedIntents,
                    out var intentIssues),
                    DescribeIssues(intentIssues));
                tampered = RecreateFinalizationResolution(
                    resolution,
                    declaredResult: changedDeclared,
                    outcomeIntents: changedIntents);
                break;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(axis), axis, null);
        }

        var result = MortalWoundTreatmentResourceComposer.Finalize(tampered);

        Assert.False(result.IsValid);
        var issue = Assert.Single(result.Issues);
        Assert.StartsWith(
            "mortal_wound_treatment_resource_finalization_",
            issue.Code,
            StringComparison.Ordinal);
        Assert.Null(result.Finalization);
        AssertResolverFixtureTreeUnchanged(fixture.Root, treeBefore);
    }

    private static MortalWoundTreatmentAttemptRequest WithFinalizationResourceAuthority(
        MortalWoundTreatmentAttemptRequest request,
        MortalWoundTreatmentResourceReservationAuthority resource)
    {
        var requestFingerprint = MortalWoundTreatmentAttemptRequest.ComputeFingerprint(
            request.Mode,
            request.Coordinates,
            request.MilestoneOrdinal,
            request.RouteSourceWoundFingerprint,
            request.ModeAuthority,
            request.RequirementAuthority,
            resource);
        return Assert.IsType<MortalWoundTreatmentAttemptRequest>(
            MortalWoundTreatmentAttemptRequest.RestoreDetached(
                request.Mode,
                request.Coordinates,
                request.MilestoneOrdinal,
                request.RouteSourceWound,
                request.RouteSourceWoundFingerprint,
                request.ModeAuthority,
                request.RequirementAuthority,
                resource,
                requestFingerprint));
    }

    private static MortalWoundTreatmentResourceReservationAuthority
        TamperFinalizationResourceAuthority(
            MortalWoundTreatmentResourceReservationAuthority source,
            string axis)
    {
        var node = Assert.IsType<JsonObject>(JsonSerializer.SerializeToNode(source));
        switch (axis)
        {
            case "resource":
                SetJsonProperty(
                    node,
                    "AuthorityFingerprint",
                    ChangedFinalizationFingerprint(source.AuthorityFingerprint));
                break;
            case "policy":
            {
                var policy = node[FindJsonPropertyName(node, "Policy")]!.AsObject();
                var refundOn = policy[FindJsonPropertyName(policy, "RefundOn")]!.AsArray();
                refundOn[2] = "tampered";
                break;
            }
            case "claim":
            {
                var claims = node[FindJsonPropertyName(node, "Claims")]!.AsArray();
                var claim = Assert.Single(claims)!.AsObject();
                var quantityName = FindJsonPropertyName(claim, "Quantity");
                claim[quantityName] = checked(claim[quantityName]!.GetValue<int>() + 1);
                break;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(axis), axis, null);
        }
        return Assert.IsType<MortalWoundTreatmentResourceReservationAuthority>(
            JsonSerializer.Deserialize<MortalWoundTreatmentResourceReservationAuthority>(
                node.ToJsonString()));
    }

    private static MortalWoundProcedureModeEvidence TamperFinalizationModeEvidence(
        MortalWoundProcedureModeEvidence source)
    {
        var node = Assert.IsType<JsonObject>(JsonSerializer.SerializeToNode(source));
        var totalName = FindJsonPropertyName(node, "Total");
        node[totalName] = checked(node[totalName]!.GetValue<long>() + 1);
        return Assert.IsType<MortalWoundProcedureModeEvidence>(
            JsonSerializer.Deserialize<MortalWoundProcedureModeEvidence>(
                node.ToJsonString()));
    }

    private static MortalWoundTreatmentResolution RecreateFinalizationResolution(
        MortalWoundTreatmentResolution source,
        MortalWoundTreatmentAttemptRequest? request = null,
        string? attemptDisposition = null,
        string? resultCategory = null,
        bool? interruption = null,
        string? consumptionTrigger = null,
        IReadOnlyList<MortalWoundTreatmentOperation>? declaredResult = null,
        IReadOnlyList<MortalWoundTreatmentOutcomeIntent>? outcomeIntents = null,
        MortalWoundTreatmentModeEvidence? modeEvidence = null)
    {
        var selectedRequest = request ?? source.RequestAuthority;
        return MortalWoundTreatmentResolution.Create(
            source.Mode,
            selectedRequest.Coordinates,
            attemptDisposition ?? source.AttemptDisposition,
            resultCategory ?? source.ResultCategory,
            source.SelectedOutcomeIndex,
            interruption ?? source.Interruption,
            declaredResult ?? source.DeclaredResult,
            outcomeIntents ?? source.OutcomeIntents,
            source.CriticalReactionIntent,
            consumptionTrigger ?? source.ConsumptionTrigger,
            source.CourseId,
            source.CourseMilestoneOrdinal,
            source.CourseDisposition,
            selectedRequest,
            modeEvidence ?? source.ModeEvidence,
            source.RouteFingerprint,
            source.RouteCompletion);
    }

    private static string ChangedFinalizationFingerprint(string source) =>
        source[..^1] + (source[^1] == '0' ? "1" : "0");

    private static MethodInfo RequireExactResourceFinalizeMethod()
    {
        var methods = typeof(MortalWoundTreatmentResourceComposer).GetMethods(
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(candidate => candidate.Name == "Finalize")
            .ToArray();
        var method = Assert.Single(methods);
        Assert.Equal(
            "MortalWoundTreatmentResourceFinalizationResult",
            method.ReturnType.Name);
        var parameter = Assert.Single(method.GetParameters());
        Assert.Equal(typeof(MortalWoundTreatmentResolution), parameter.ParameterType);
        return method;
    }

    private static void AssertFrozenProjection(object projection)
    {
        var list = Assert.IsAssignableFrom<IList>(projection);
        Assert.True(list.IsReadOnly);
        Assert.True(list.IsFixedSize);
        Assert.Throws<NotSupportedException>(() => list.Add(null));
    }
}
