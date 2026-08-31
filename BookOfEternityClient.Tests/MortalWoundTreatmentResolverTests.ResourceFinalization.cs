using System.Collections;
using System.Reflection;
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
