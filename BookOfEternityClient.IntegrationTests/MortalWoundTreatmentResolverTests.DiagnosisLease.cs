using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalWoundTreatmentResolverTests
{
    [Fact]
    public async Task DiagnosisRawResourceValidationUsesFactoryLeaseThroughRealCommonAndRetainedAdmission()
    {
        // The lease originates in another async method. Its caller supplies it
        // explicitly; ambient propagation is not part of this API's contract.
        await using var fixture = await DiagnosisOwnershipFixture.CreateAsync(20, signedInput: true, duration: 0);
        var files = fixture.Files;
        var lease = fixture.Lease;
        var input = fixture.Authority.ReadDiagnosisOriginalInput();
        var admission = await WoundApplicationService.ResolveMortalDiagnosisInputAsync(files, lease,
            MortalWoundDiagnosisInputCodec.Read(input).Frame);
        var request = admission.Request;
        var check = Assert.IsType<MortalWoundProcedureCheckAuthority>(request.ModeAuthority);
        var exactRequest = Assert.IsType<System.Text.Json.Nodes.JsonObject>(
            WoundResponseInputComposer.SerializeMortalWoundTreatmentValue(request));
        var before = fixture.CaptureJson();
        var validator = new ValidationService(files, NullLogger<ValidationService>.Instance);
        Assert.Empty(await validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync(lease));
        Assert.Empty(await validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync(lease));

        var refused = await validator.ValidateAcceptedTurnRawResourceMaterializationAsync(lease);
        Assert.Equal("mortal_diagnosis_live_owner_unavailable", Assert.Single(refused).Code);
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(files, lease));
        Assert.True(admission.Owner.HasLiveProcedureReservationAgreement(check));
        fixture.AssertJson(before);

        var callbacks = 0;
        var issues = await validator.ValidateAcceptedTurnRawResourceMaterializationAsync(lease,
            prepareMortalDiagnosis: async (actualLease, actual) =>
            {
                callbacks++;
                Assert.Same(lease, actualLease);
                Assert.True(MortalWoundTreatmentCommandCodec.SemanticJsonEquals(exactRequest,
                    Assert.IsType<System.Text.Json.Nodes.JsonObject>(WoundResponseInputComposer.SerializeMortalWoundTreatmentValue(actual.Request))));
                var prepared = await WoundAcceptedTurnPlanner.ComposeMortalDiagnosisPublicationAsync(files, actualLease,
                    new BookOfEternityClient.Models.GameResponse(), actual.Owner, actual.Request);
                Assert.True(prepared.IsValid, JsonSerializer.Serialize(prepared.Issues));
                return prepared.Issues;
            });
        Assert.Empty(issues);
        Assert.Equal(1, callbacks);
        Assert.True(AcceptedMechanicsPlanAuthority.TryPeekValidated(files, lease, out _, out var validated));
        Assert.True(validated.Success, JsonSerializer.Serialize(validated.Issues));
        var plan = Assert.IsType<AcceptedMechanicsPlan>(validated.Plan);
        var publication = Assert.IsType<MortalWoundTreatmentResourcePublicationAuthority>(plan.TreatmentResourcePublicationAuthority);
        Assert.Equal(request.RequestFingerprint, publication.RequestFingerprint);
        fixture.AssertJson(before);

        // This is actual retained-plan validation, including its dynamic signed
        // coverage lookup, without publication or another preparation callback.
        var retained = await validator.ValidateAcceptedTurnRawResourceMaterializationAsync(lease);
        Assert.Empty(retained);
        Assert.True(AcceptedMechanicsPlanAuthority.TryPeekValidated(files, lease, out _, out var again));
        Assert.Same(plan, again.Plan);
        Assert.True(admission.Owner.HasLiveProcedureReservationAgreement(check));
        var finalization = MortalWoundTreatmentResourceComposer.Finalize(publication.ResolutionAuthority);
        Assert.True(finalization.IsValid, JsonSerializer.Serialize(finalization.Issues));
        var held = AcceptedTurnAuthorityRegistry.InspectConfirmedMortalWoundTreatmentResourceClaim(files, lease,
            request, Assert.IsType<MortalWoundTreatmentResourceFinalization>(finalization.Finalization));
        Assert.True(held.IsValid, JsonSerializer.Serialize(held.Issues));
        Assert.Equal(MortalWoundTreatmentResourceReservationState.ConfirmedHeld, held.State);
        Assert.Equal(request.RequestFingerprint, held.RequestFingerprint);
        Assert.True(MortalWoundTreatmentCommandCodec.SemanticJsonEquals(exactRequest,
            Assert.IsType<System.Text.Json.Nodes.JsonObject>(WoundResponseInputComposer.SerializeMortalWoundTreatmentValue(publication.RequestAuthority))));
        fixture.AssertJson(before);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OrdinaryResourceValidationUsesFactoryLeaseAndKeepsDirectMutationRefusal(bool mutateDefinitions)
    {
        var owned = await CreateExplicitResourceLeaseFixtureAsync(mutateDefinitions);
        await using var context = owned.Context;
        await using var lease = owned.Lease;
        var files = context.FileSystem;
        var before = CaptureDiagnosisMenuJson(files);
        Assert.Empty(await context.Validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync(lease));
        Assert.Empty(await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync(lease));
        var issues = await context.Validator.ValidateAcceptedTurnRawResourceMaterializationAsync(lease);
        if (mutateDefinitions)
        {
            Assert.Contains(issues, issue => issue.Code == "resource_materialization_direct_definition_mutation");
            Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(files, lease));
        }
        else
        {
            Assert.Empty(issues);
            Assert.True(AcceptedMechanicsPlanAuthority.TryPeekValidated(files, lease, out _, out var validated));
            Assert.True(validated.Success, JsonSerializer.Serialize(validated.Issues));
            var plan = Assert.IsType<AcceptedMechanicsPlan>(validated.Plan);
            Assert.NotEmpty(plan.TouchedPaths);
            Assert.Contains(ResourceMaterializationContract.DefinitionsPath, plan.BeforeImages.Keys);
            Assert.Contains(ResourceMaterializationContract.StatePath, plan.BeforeImages.Keys);
        }
        AssertDiagnosisMenuJson(before, files);
    }

    private static async Task<(ResourceMaterializationTestContext Context, FileSystemManager.CanonicalWriteLease Lease)>
        CreateExplicitResourceLeaseFixtureAsync(bool mutateDefinitions)
    {
        var context = await ResourceMaterializationTestContext.CreateAsync();
        try
        {
            await ResourceMaterializationValidationTests.SeedEmptyRootsAsync(context);
            await context.WriteExactJsonAsync(MortalItemIdentityState.StatePath, MortalItemIdentityState.CreateEmptyRoot().ToJsonString());
            await context.CaptureValidatedPendingSnapshotAsync();
            await context.WriteExactJsonAsync(ResourceMaterializationContract.CommandPath,
                ResourceMaterializationValidationTests.DefinitionAndInitializationCommand().ToJsonString());
            if (mutateDefinitions)
            {
                var changed = ResourceBootstrapStateBuilder.BuildPristine();
                Assert.True(changed.IsValid, JsonSerializer.Serialize(changed.Issues));
                await context.WriteExactJsonAsync(ResourceMaterializationContract.DefinitionsPath, changed.Definitions!.ToCanonicalJson());
            }
            var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
            return (context, lease);
        }
        catch
        {
            await context.DisposeAsync();
            throw;
        }
    }
}
