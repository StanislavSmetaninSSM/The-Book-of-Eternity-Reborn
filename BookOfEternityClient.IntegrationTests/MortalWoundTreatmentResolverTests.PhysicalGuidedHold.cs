using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalWoundTreatmentResolverTests
{
    [Theory]
    [InlineData("procedure")]
    [InlineData("course")]
    [InlineData("guaranteed")]
    public async Task GuidedPhysicalHoldRebindsExactModeAndKeepsOrdinaryNoWaveContract(string mode)
    {
        await using var fixture = await DiagnosisOwnershipFixture.CreateAsync(20, signedInput: true, physicalMode: mode);
        var files = fixture.Files;
        var input = fixture.Authority.ReadGuidedTreatmentOriginalInput();
        var frame = MortalWoundGuidedTreatmentInputCodec.Read(input).Frame;
        var before = fixture.CaptureJson();
        var admission = await WoundApplicationService.ResolveMortalTreatmentInputAsync(files, fixture.Lease, frame);
        var request = admission.Request;
        Assert.Equal(mode, request.Mode);
        Assert.Equal("treatment", request.Coordinates.Purpose);
        Assert.Equal(mode == "course" ? 1 : (int?)null, request.MilestoneOrdinal);
        Assert.True(MortalWoundTreatmentDetachedSealValidator.IsValid(request));
        var pendingText = Assert.IsType<string>(await files.ReadFileAsync(fixture.Lease, WoundAcceptedTurnSnapshotContract.PendingResolutionPath));
        var pending = JsonNode.Parse(pendingText)!.AsObject();
        Assert.Equal("guided_treatment_hold", pending["guidedTreatmentRequest"]!["kind"]!.GetValue<string>());
        Assert.Equal(request.RequestFingerprint, pending["guidedTreatmentRequest"]!["requestFingerprint"]!.GetValue<string>());
        Assert.False(pending.ContainsKey("submittedTreatmentRequests"));
        Assert.Empty(pending["repairPackets"]!.AsArray()); Assert.Empty(pending["repairReceipts"]!.AsArray());
        var catalog = MortalWoundTreatmentPersistedRequestCatalog.Parse(JsonSerializer.SerializeToElement(input),
            JsonSerializer.SerializeToElement(pending), new(admission.Owner.History, []));
        Assert.True(catalog.IsValid, DescribeIssues(catalog.Issues));
        Assert.Equal(request.RequestFingerprint, Assert.Single(catalog.HeldRequests).RequestFingerprint);
        var missingInput = MortalWoundTreatmentPersistedRequestCatalog.Parse(null, JsonSerializer.SerializeToElement(pending),
            new(admission.Owner.History, []));
        Assert.False(missingInput.IsValid);
        Assert.Contains(missingInput.Issues, issue => issue.Code == "mortal_guided_treatment_hold_invalid");
        var wrong = pending.DeepClone().AsObject(); wrong["guidedTreatmentRequest"]!["requestFingerprint"] =
            WoundAcceptedTurnFingerprintWriter.Compute(new[] { "foreign_request" });
        Assert.False(MortalWoundTreatmentPersistedRequestCatalog.Parse(JsonSerializer.SerializeToElement(input),
            JsonSerializer.SerializeToElement(wrong), new(admission.Owner.History, [])).IsValid);

        var after = fixture.CaptureJson();
        after.Remove(files.ResolvePath(WoundAcceptedTurnSnapshotContract.PendingResolutionPath));
        Assert.Equal(before.Keys.Order(StringComparer.Ordinal), after.Keys.Order(StringComparer.Ordinal));
        foreach (var pair in before) Assert.Equal(pair.Value, after[pair.Key]);
        var heldImages = fixture.CaptureJson();
        var originalRequest = WoundResponseInputComposer.SerializeMortalWoundTreatmentValue(request)!.ToJsonString();
        await fixture.RebindAsync();
        var restored = await WoundApplicationService.ResolveMortalTreatmentInputAsync(files, fixture.Lease, frame, requireRetained: true);
        Assert.NotSame(admission.Owner, restored.Owner);
        Assert.NotSame(request, restored.Request);
        Assert.Equal(originalRequest, WoundResponseInputComposer.SerializeMortalWoundTreatmentValue(restored.Request)!.ToJsonString());
        if (request.ModeAuthority is MortalWoundProcedureCheckAuthority oldCheck)
        {
            var check = Assert.IsType<MortalWoundProcedureCheckAuthority>(restored.Request.ModeAuthority);
            Assert.Equal(oldCheck.SourceIndices, check.SourceIndices); Assert.Equal(oldCheck.SourceRolls, check.SourceRolls);
            Assert.True(restored.Owner.HasLiveProcedureReservationAgreement(check));
        }

        var history = new WoundHistoryParseResult(restored.Owner.History, []);
        var reduced = mode switch
        {
            "procedure" => MortalWoundTreatmentPlanner.CreateProcedureAttempt(restored.Request, history, restored.Owner.CurrentWound, restored.Owner),
            "course" => MortalWoundTreatmentPlanner.CreateCourseMilestoneAttempt(restored.Request, history, restored.Owner.CurrentWound, restored.Owner),
            _ => MortalWoundTreatmentPlanner.CreateGuaranteedAttempt(restored.Request, history, restored.Owner.CurrentWound, restored.Owner)
        };
        Assert.Equal("Resolved", reduced.Disposition);
        var resolution = Assert.IsType<MortalWoundTreatmentResolution>(reduced.Resolution);
        var command = WoundResponseInputComposer.ComposeMortalWoundTreatmentCommandRoot(restored.Owner.Binding, resolution,
            "Участник закончил выбранный этап лечения.");
        var parsedCommand = WoundResponseInputComposer.ParseCommandRoot(JsonSerializer.SerializeToElement(command));
        Assert.True(parsedCommand.Success, DescribeIssues(parsedCommand.Issues));
        // Exact old builder negative, now through actual typed request APIs.
        // No fabricated repair packet and no reflected old fixture/dispatcher.
        var ordinaryPending = WoundRepairPacketBuilder.ComposePendingRoot(restored.Owner.Binding,
            Array.Empty<WoundRepairPacket>(), parsedCommand);
        Assert.False(ordinaryPending.ContainsKey("submittedTreatmentRequests"));
        ordinaryPending["submittedTreatmentRequests"] = new JsonArray(new JsonObject
        {
            ["operationKey"] = restored.Request.Coordinates.OperationKey, ["attemptId"] = restored.Request.Coordinates.AttemptId,
            ["requestFingerprint"] = restored.Request.RequestFingerprint,
            ["request"] = WoundResponseInputComposer.SerializeMortalWoundTreatmentValue(restored.Request)
        });
        var invalidOrdinary = MortalWoundTreatmentPersistedRequestCatalog.Parse(JsonSerializer.SerializeToElement(command),
            JsonSerializer.SerializeToElement(ordinaryPending), history);
        Assert.False(invalidOrdinary.IsValid);
        Assert.Contains(invalidOrdinary.Issues, issue => issue.Code == "mortal_wound_treatment_persisted_pending_invalid");
        fixture.AssertJson(heldImages);
        var replay = await WoundApplicationService.ResolveMortalTreatmentInputAsync(files, fixture.Lease, frame, requireRetained: true);
        Assert.Equal(restored.Request.RequestFingerprint, replay.Request.RequestFingerprint);
        fixture.AssertJson(heldImages);
    }
}
