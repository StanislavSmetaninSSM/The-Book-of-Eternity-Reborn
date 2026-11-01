using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalWoundTreatmentResolverTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GuidedPhysicalHoldFaultSettlesOnlyProvenAbsenceOrRecoversExactDurableClaims(bool afterCommit)
    {
        var armed = false; var committed = false; var cuts = 0;
        var hooks = new FileSystemManagerHooks
        {
            LocalPublicationObserver = (phase, _) =>
            {
                if (!armed) return;
                if (afterCommit && phase == TrustedLocalPublicationPhase.CleanupComplete) committed = true;
                if (!afterCommit && phase == TrustedLocalPublicationPhase.IntentPublished)
                {
                    armed = false; cuts++;
                    throw new IOException("Controlled guided hold failure before member publication.");
                }
            },
            BeforeCanonicalReadOpenAsync = path =>
            {
                if (armed && afterCommit && committed && path == WoundAcceptedTurnSnapshotContract.PendingResolutionPath)
                {
                    armed = false; cuts++;
                    throw new IOException("Controlled guided hold readback failure after durable commit.");
                }
                return Task.CompletedTask;
            }
        };
        await using var fixture = await DiagnosisOwnershipFixture.CreateAsync(20, signedInput: true, physicalMode: "procedure", hooks: hooks);
        var input = fixture.Authority.ReadGuidedTreatmentOriginalInput();
        var frame = MortalWoundGuidedTreatmentInputCodec.Read(input).Frame;
        var before = fixture.CaptureJson();
        await Assert.ThrowsAsync<InvalidDataException>(() => WoundApplicationService.ResolveMortalTreatmentInputAsync(
            fixture.Files, fixture.Lease, frame, requireRetained: true));
        fixture.AssertJson(before); // Missing hold refuses before any new allocation.
        armed = true;
        var failure = await Record.ExceptionAsync(() => WoundApplicationService.ResolveMortalTreatmentInputAsync(fixture.Files, fixture.Lease, frame));
        Assert.NotNull(failure); Assert.Contains("Controlled guided hold", failure!.ToString()); Assert.Equal(1, cuts);
        MortalWoundTreatmentAttemptRequest? durable = null;
        if (afterCommit)
        {
            var pending = JsonNode.Parse(Assert.IsType<string>(await fixture.Files.ReadFileAsync(fixture.Lease,
                WoundAcceptedTurnSnapshotContract.PendingResolutionPath)))!.AsObject();
            var issues = new List<ValidationIssue>();
            Assert.True(MortalWoundTreatmentCommandCodec.TryParseRequest(pending["guidedTreatmentRequest"]!["request"]!.AsObject(),
                "fault.actualDurableRequest", issues, out durable), DescribeIssues(issues));
            Assert.NotNull(durable); Assert.Equal("procedure", durable!.Mode);
        }
        else
        {
            Assert.Null(await fixture.Files.ReadFileBytesAsync(fixture.Lease, WoundAcceptedTurnSnapshotContract.PendingResolutionPath));
            fixture.AssertJson(before);
        }
        await fixture.RebindAsync();
        var recovered = await WoundApplicationService.ResolveMortalTreatmentInputAsync(fixture.Files, fixture.Lease, frame, requireRetained: afterCommit);
        var request = recovered.Request;
        if (durable is not null)
        {
            Assert.NotSame(durable, request);
            Assert.Equal(WoundResponseInputComposer.SerializeMortalWoundTreatmentValue(durable)!.ToJsonString(),
                WoundResponseInputComposer.SerializeMortalWoundTreatmentValue(request)!.ToJsonString());
        }
        var check = Assert.IsType<MortalWoundProcedureCheckAuthority>(request.ModeAuthority);
        Assert.Equal(new[] { 0 }, check.SourceIndices); Assert.Equal(new[] { 20 }, check.SourceRolls);
        Assert.True(recovered.Owner.HasLiveProcedureReservationAgreement(check));
        var held = fixture.CaptureJson();
        var repeated = await WoundApplicationService.ResolveMortalTreatmentInputAsync(fixture.Files, fixture.Lease, frame, requireRetained: true);
        Assert.Equal(request.RequestFingerprint, repeated.Request.RequestFingerprint);
        fixture.AssertJson(held);
    }
}
