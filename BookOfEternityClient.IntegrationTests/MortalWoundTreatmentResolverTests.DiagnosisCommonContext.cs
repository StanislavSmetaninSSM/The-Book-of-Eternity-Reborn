using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalWoundTreatmentResolverTests
{
    [Fact]
    public async Task DiagnosisCommonConstructorsBindExactResourceAuthorityAndKeepOriginalClockUntilPublication()
    {
        await using var foreign = await DiagnosisOwnershipFixture.CreateAsync(20, signedInput: true, duration: 30);
        var foreignResult = await ComposeDiagnosisCommonContextAsync(foreign);
        var foreignPlan = Assert.IsType<AcceptedMechanicsPlan>(foreignResult.Plan);
        var foreignAuthority = Assert.IsType<MortalWoundTreatmentResourcePublicationAuthority>(foreignPlan.TreatmentResourcePublicationAuthority);
        Assert.True(foreignAuthority.HasValidSeal());
        var foreignHold = AcceptedTurnAuthorityRegistry.InspectConfirmedMortalWoundTreatmentResourceClaim(foreign.Files,
            foreign.Lease, foreignAuthority.RequestAuthority, foreignAuthority.Finalization);
        Assert.True(foreignHold.IsValid, JsonSerializer.Serialize(foreignHold.Issues));
        var foreignBytes = foreign.CaptureJson();

        await using var fixture = await DiagnosisOwnershipFixture.CreateAsync(20, signedInput: true, duration: 30);
        var result = await ComposeDiagnosisCommonContextAsync(fixture);
        var plan = Assert.IsType<AcceptedMechanicsPlan>(result.Plan);
        var input = Assert.IsType<AcceptedMechanicsInput>(result.OriginalComposedInput);
        var context = Assert.IsType<AcceptedMechanicsPlanningContext>(input.PlanningContext);
        var bundle = Assert.IsType<AcceptedMechanicsWoundStageBundle>(context.WoundStageBundle);
        var authority = Assert.IsType<MortalWoundTreatmentResourcePublicationAuthority>(context.TreatmentResourcePublicationAuthority);
        var diagnosis = Assert.IsType<WoundAcceptedTurnPlanner.MortalDiagnosisContinuation>(bundle.MortalDiagnosisContinuation);
        var before = fixture.CaptureJson();
        var fingerprint = AcceptedMechanicsPlanFingerprints.ComputePlanningInput(input);
        var planFingerprint = plan.PreparedPlanFingerprint;
        var check = Assert.IsType<MortalWoundProcedureCheckAuthority>(authority.RequestAuthority.ModeAuthority);
        var held = AcceptedTurnAuthorityRegistry.InspectConfirmedMortalWoundTreatmentResourceClaim(fixture.Files,
            fixture.Lease, authority.RequestAuthority, authority.Finalization);
        Assert.True(held.IsValid, JsonSerializer.Serialize(held.Issues));
        Assert.Equal(MortalWoundTreatmentResourceReservationState.ConfirmedHeld, held.State);
        Assert.NotSame(authority, foreignAuthority);
        Assert.True(authority.HasValidSeal());
        Assert.Same(authority, plan.TreatmentResourcePublicationAuthority);
        Assert.Same(diagnosis, authority.ContinuationAuthority);
        Assert.Null(context.WoundAnchorPlan);
        Assert.Null(context.DirectWoundPublicationAuthority);
        Assert.Null(plan.LiveWoundProofFingerprint);

        var clockPath = EffectAcceptedTurnInputComposer.WorldTimePath;
        var originalClock = Assert.IsType<byte[]>(input.BeforeImages[clockPath].Bytes);
        Assert.Equal(diagnosis.Working.ReadOriginalBytes(clockPath), originalClock);
        var originalMinute = JsonNode.Parse(FileSystemManager.DecodeFileBytes(originalClock))!["currentTimeInMinutes"]!.GetValue<long>();
        Assert.Equal(diagnosis.Working.OriginalMinute, originalMinute);
        Assert.Equal(originalMinute + 30, diagnosis.Working.ResultingMinute);
        Assert.Equal(originalClock, Assert.IsType<byte[]>(await fixture.Files.ReadFileBytesAsync(fixture.Lease, clockPath)));
        var companions = plan.OwnerCompanionAfterImages;
        Assert.True(JsonNode.DeepEquals(diagnosis.Working.ClockAfter, companions[clockPath]));
        Assert.Equal(originalMinute + 30, companions[clockPath]["currentTimeInMinutes"]!.GetValue<long>());
        diagnosis.RequireComposedClock(companions); // Exact schedule/report after-images, not merely the minute scalar.
        Assert.Equal(diagnosis.Working.ResultingMinute, context.EffectPlan!.EventInput["currentTime"]!.GetValue<long>());
        foreach (var path in new[] { clockPath, ProgressionScheduleService.SchedulePath, ProgressionScheduleService.ReportPath,
                     WoundAcceptedTurnSnapshotContract.PendingResolutionPath })
            Assert.Equal(input.BeforeImages[path].Fingerprint, plan.BeforeImages[path].Fingerprint);

        // These are the actual original context and private family, not a context
        // reconstructed from the final resource/clock after-images.
        var copiedContext = CopyDiagnosisCommonContext(context, bundle, authority);
        Assert.Equal(fingerprint, AcceptedMechanicsPlanFingerprints.ComputePlanningInput(input.WithPlanningContext(copiedContext)));
        foreach (var invalid in new MortalWoundTreatmentResourcePublicationAuthority?[] { null, foreignAuthority })
        {
            var failure = Assert.Throws<ArgumentException>(() => CopyDiagnosisCommonContext(context, bundle, invalid));
            Assert.Equal("treatmentResourcePublicationAuthority", failure.ParamName);
        }

        // One real carrier assembly is reused by the actual completed-plan
        // constructor probes. It does not reload or rebuild the common input.
        var composition = AcceptedMechanicsCarrierAssembler.Compose(plan.EffectPlan, plan.OwnerCompanionAfterImages, bundle);
        Assert.True(composition.Success, JsonSerializer.Serialize(composition.Issues));
        Assert.Equal(planFingerprint, CopyDiagnosisCommonPlan(plan, bundle, composition, authority).PreparedPlanFingerprint);
        foreach (var invalid in new MortalWoundTreatmentResourcePublicationAuthority?[] { null, foreignAuthority })
        {
            var failure = Assert.Throws<ArgumentException>(() => CopyDiagnosisCommonPlan(plan, bundle, composition, invalid));
            Assert.Equal("treatmentResourcePublicationAuthority", failure.ParamName);
        }

        // DATA exports are copies, while the readonly context intentionally
        // retains the actual operation's authorities and identity factories.
        input.AcceptedEvents["detached_probe"] = true;
        var exportedClock = Assert.IsType<byte[]>(input.BeforeImages[clockPath].Bytes);
        exportedClock[0] ^= 1;
        Assert.Equal(fingerprint, AcceptedMechanicsPlanFingerprints.ComputePlanningInput(input));
        fixture.AssertJson(before);
        foreign.AssertJson(foreignBytes);

        var cached = await WoundAcceptedTurnPlanner.ComposeMortalDiagnosisPublicationAsync(fixture.Files, fixture.Lease,
            new GameResponse(), authority.AcceptedStateAuthority, authority.RequestAuthority);
        Assert.True(cached.IsValid, JsonSerializer.Serialize(cached.Issues));
        Assert.Same(plan, cached.Plan);
        Assert.Null(cached.OriginalComposedInput);

        var changedClock = JsonNode.Parse(FileSystemManager.DecodeFileBytes(originalClock))!.AsObject();
        changedClock["currentTimeInMinutes"] = originalMinute + 1;
        try
        {
            await fixture.Files.WriteFileAtomicAsync(fixture.Lease, clockPath, changedClock.ToJsonString());
            Assert.Throws<InvalidDataException>(() => AcceptedMechanicsWoundCommonInputComposer.ComposeMortalDiagnosisContinuation(
                fixture.Files, fixture.Lease, bundle));
        }
        finally { await fixture.Files.WriteFileAtomicBytesAsync(fixture.Lease, clockPath, originalClock); }
        fixture.AssertJson(before);
        foreign.AssertJson(foreignBytes);
        Assert.Equal(fingerprint, AcceptedMechanicsPlanFingerprints.ComputePlanningInput(input));
        Assert.Equal(planFingerprint, plan.PreparedPlanFingerprint);
        Assert.True(authority.AcceptedStateAuthority.HasLiveProcedureReservationAgreement(check));
        var unchangedHold = AcceptedTurnAuthorityRegistry.InspectConfirmedMortalWoundTreatmentResourceClaim(fixture.Files,
            fixture.Lease, authority.RequestAuthority, authority.Finalization);
        Assert.True(unchangedHold.IsValid, JsonSerializer.Serialize(unchangedHold.Issues));
        Assert.Equal(held.State, unchangedHold.State);
        Assert.Equal(held.RequestFingerprint, unchangedHold.RequestFingerprint);
        var unchangedForeignHold = AcceptedTurnAuthorityRegistry.InspectConfirmedMortalWoundTreatmentResourceClaim(foreign.Files,
            foreign.Lease, foreignAuthority.RequestAuthority, foreignAuthority.Finalization);
        Assert.True(unchangedForeignHold.IsValid, JsonSerializer.Serialize(unchangedForeignHold.Issues));
        Assert.Equal(foreignHold.State, unchangedForeignHold.State);
        Assert.Equal(foreignHold.RequestFingerprint, unchangedForeignHold.RequestFingerprint);
    }

    private static async Task<WoundAcceptedTurnPlanner.MortalWoundTreatmentPublicationResult> ComposeDiagnosisCommonContextAsync(
        DiagnosisOwnershipFixture fixture)
    {
        var input = fixture.Authority.ReadDiagnosisOriginalInput();
        var admitted = await WoundApplicationService.ResolveMortalDiagnosisInputAsync(fixture.Files, fixture.Lease,
            MortalWoundDiagnosisInputCodec.Read(input).Frame);
        var validator = new ValidationService(fixture.Files, NullLogger<ValidationService>.Instance);
        Assert.Empty(await validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync(fixture.Lease));
        Assert.Empty(await validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync(fixture.Lease));
        var before = fixture.CaptureJson();
        var result = await WoundAcceptedTurnPlanner.ComposeMortalDiagnosisPublicationAsync(fixture.Files, fixture.Lease,
            new GameResponse(), admitted.Owner, admitted.Request);
        Assert.True(result.IsValid, JsonSerializer.Serialize(result.Issues));
        Assert.NotNull(result.OriginalComposedInput);
        fixture.AssertJson(before);
        return result;
    }

    private static AcceptedMechanicsPlanningContext CopyDiagnosisCommonContext(AcceptedMechanicsPlanningContext source,
        AcceptedMechanicsWoundStageBundle bundle, MortalWoundTreatmentResourcePublicationAuthority? resource) => new(
        source.DefinitionRoot, source.Definitions, source.State, source.History, source.Owners, source.Sources, source.Commands,
        source.EffectIdentityRoot, source.EffectPlan, source.CapacityTransitions, source.OwnerCapacityDrafts, source.TerminalOwners,
        source.OwnerCompanionAfterImages, source.OwnerTransitions, source.RegisteredSystemOutcomes, source.PendingResolutionState,
        source.ResourceIdentityFactory, source.EffectIdentityFactory, source.ExecutionSequenceOffset, bundle, source.WoundAnchorPlan,
        source.DirectWoundPublicationAuthority, resource, source.JointRepairCommonAuthority);

    private static AcceptedMechanicsPlan CopyDiagnosisCommonPlan(AcceptedMechanicsPlan source,
        AcceptedMechanicsWoundStageBundle bundle, AcceptedMechanicsCarrierCompositionResult composition,
        MortalWoundTreatmentResourcePublicationAuthority? resource) => new(
        source.InputFingerprint, source.DefinitionAfterImage, source.StateAfterImage, source.HistoryAfterImage,
        source.EffectCarrierAfterImages, source.EffectIdentityAfterImage, source.PendingAfterImages, source.OwnerCompanionAfterImages,
        source.BeforeImages, source.TouchedPaths, source.ConsumedPaths, source.AuthorityFingerprints, source.ResourceEvents,
        source.ProjectionInput, source.OwnerAuthority, source.EffectPlan, source.OwnerTransitions, source.PendingGmPacket,
        source.PendingPublicationAuthority, bundle, composition, liveWoundCompletion: null,
        directWoundPublicationAuthority: source.DirectWoundPublicationAuthority, treatmentResourcePublicationAuthority: resource,
        jointRepairCommonAuthority: source.JointRepairCommonAuthority);
}
