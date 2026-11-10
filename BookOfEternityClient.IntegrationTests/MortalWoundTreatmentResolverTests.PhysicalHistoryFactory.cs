using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalWoundTreatmentResolverTests
{
    /// <summary>
    /// Exercises History capture inside the existing genuine plain publication
    /// fixture. The alternate request shares its actual live owner and has no
    /// resource claims, procedure draws, second filesystem or Engine setup.
    /// </summary>
    /// <param name="files">The fixture's existing canonical filesystem.</param>
    /// <param name="lease">Its existing live write lease.</param>
    /// <param name="owner">The fixture's currently admitted plain accepted owner.</param>
    /// <param name="request">The genuine prepared guaranteed request.</param>
    /// <param name="resolution">Its actual selected terminal resolution before Compose.</param>
    private static void AssertPhysicalHistoryFactoryControls(FileSystemManager files,
        FileSystemManager.CanonicalWriteLease lease, MortalWoundTreatmentAcceptedStateAuthority owner,
        MortalWoundTreatmentAttemptRequest request, MortalWoundTreatmentResolution resolution)
    {
        Assert.True(owner.HasCurrentAdmissionAuthority());
        Assert.False(owner.HasOriginalGuidedTreatmentInput);
        Assert.Equal("guaranteed", request.Mode);
        Assert.Empty(request.ResourceAuthority.Claims);
        Assert.Equal("not_required", request.ResourceAuthority.ReservationDisposition);
        Assert.Null(request.ResourceAuthority.ReservationId);
        var prepared = MortalWoundTreatmentOutcomePublicationPlanner.Prepare(owner, request, resolution,
            owner.CurrentGameMinute);
        Assert.True(prepared.IsValid, DescribeIssues(prepared.Issues));
        var preparation = Assert.IsType<MortalWoundTreatmentOutcomePreparation>(prepared.Preparation);
        Assert.True(preparation.AgreesWith(resolution));
        Assert.Equal(owner.CurrentGameMinute, preparation.CurrentGameMinute);

        var raw = MortalWoundTreatmentPersistedResult.Create(resolution);
        var captured = MortalWoundTreatmentPersistedResult.CreateHistory(owner, resolution, preparation);
        var envelope = captured.ToCanonicalJson();
        Assert.Equal(3, envelope["schemaVersion"]!.GetValue<int>());
        Assert.Equal(owner.BindingFingerprint, envelope["bindingFingerprint"]!.GetValue<string>());
        Assert.Equal(owner.WoundSourcePath, envelope["sourcePath"]!.GetValue<string>());
        Assert.Equal(owner.CurrentGameMinute, envelope["publicationMinute"]!.GetValue<long>());
        Assert.Equal(preparation.Fingerprint, envelope["outcomePreparationFingerprint"]!.GetValue<string>());
        Assert.True(JsonNode.DeepEquals(raw.ToCanonicalJson(), envelope["terminal"]));
        Assert.True(JsonNode.DeepEquals(raw.SerializeRequest(), captured.SerializeRequest()));
        Assert.Equal(raw.Receipt.ReceiptFingerprint, captured.Receipt.ReceiptFingerprint);
        Assert.False(captured.HasGuidedPublicationWitness);
        var parseIssues = new List<ValidationIssue>();
        var roundtrip = MortalWoundTreatmentPersistedResult.Parse(JsonSerializer.SerializeToElement(envelope),
            "plainHistory.factory", parseIssues);
        Assert.Empty(parseIssues);
        Assert.NotNull(roundtrip);
        Assert.True(JsonNode.DeepEquals(envelope, roundtrip.ToCanonicalJson()));
        var comparison = roundtrip.ReconstructHistoricalPublication();
        Assert.Equal(preparation.Fingerprint, comparison.Preparation.Fingerprint);
        Assert.Equal(resolution.RequestFingerprint, comparison.Resolution.RequestFingerprint);
        Assert.Equal(resolution.ResultFingerprint, comparison.Resolution.ResultFingerprint);
        Assert.Equal(owner.BindingFingerprint, comparison.BindingFingerprint);
        Assert.Equal(owner.WoundSourcePath, comparison.SourcePath);
        Assert.Equal(owner.CurrentGameMinute, comparison.CurrentMinute);

        // A different operation is genuinely prepared/resolved under the same
        // owner, not forged by changing a fingerprint or constructor state.
        var history = new WoundHistoryParseResult(owner.History, []);
        var alternative = MortalWoundTreatmentPlanner.PrepareGuaranteedRequest(owner, history, owner.CurrentWound,
            request.Coordinates.OperationKey + "_foreign_preparation", request.Coordinates.RouteId,
            request.Coordinates.EventRef);
        Assert.True(alternative.IsValid, DescribeIssues(alternative.Issues));
        var otherRequest = Assert.IsType<MortalWoundTreatmentAttemptRequest>(alternative.Request);
        try
        {
            Assert.Empty(otherRequest.ResourceAuthority.Claims);
            Assert.Equal("not_required", otherRequest.ResourceAuthority.ReservationDisposition);
            Assert.Null(otherRequest.ResourceAuthority.ReservationId);
            var attempted = MortalWoundTreatmentPlanner.CreateGuaranteedAttempt(otherRequest, history, owner.CurrentWound, owner);
            Assert.Equal("Resolved", attempted.Disposition);
            Assert.Empty(attempted.Issues);
            var otherResolution = Assert.IsType<MortalWoundTreatmentResolution>(attempted.Resolution);
            Assert.NotEqual(resolution.RequestFingerprint, otherResolution.RequestFingerprint);
            var otherPrepared = MortalWoundTreatmentOutcomePublicationPlanner.Prepare(owner, otherRequest, otherResolution,
                owner.CurrentGameMinute);
            Assert.True(otherPrepared.IsValid, DescribeIssues(otherPrepared.Issues));
            var otherPreparation = Assert.IsType<MortalWoundTreatmentOutcomePreparation>(otherPrepared.Preparation);
            Assert.True(otherPreparation.AgreesWith(otherResolution));
            Assert.False(otherPreparation.AgreesWith(resolution));
            Assert.Throws<InvalidDataException>(() =>
                MortalWoundTreatmentPersistedResult.CreateHistory(owner, resolution, otherPreparation));
            Assert.Throws<InvalidDataException>(() =>
                MortalWoundTreatmentPersistedResult.CreateHistory(owner, otherResolution, preparation));

            var changedMinute = owner.CurrentGameMinute == long.MaxValue
                ? owner.CurrentGameMinute - 1 : owner.CurrentGameMinute + 1;
            var invalidLivePreparation = MortalWoundTreatmentOutcomePublicationPlanner.Prepare(owner, request, resolution, changedMinute);
            Assert.False(invalidLivePreparation.IsValid);
            Assert.Null(invalidLivePreparation.Preparation);
            Assert.NotEmpty(invalidLivePreparation.Issues);
            // This deliberately detached wrong-clock DATA has no publication
            // provenance. Its internal consistency must not authorize capture.
            var detachedWrongClock = MortalWoundTreatmentOutcomePublicationPlanner.RecomposeDetached(request, resolution, changedMinute);
            Assert.True(detachedWrongClock.IsValid, DescribeIssues(detachedWrongClock.Issues));
            var wrongClock = Assert.IsType<MortalWoundTreatmentOutcomePreparation>(detachedWrongClock.Preparation);
            Assert.True(wrongClock.AgreesWith(resolution));
            Assert.NotEqual(preparation.CurrentGameMinute, wrongClock.CurrentGameMinute);
            Assert.NotEqual(preparation.Fingerprint, wrongClock.Fingerprint);
            Assert.Throws<InvalidDataException>(() =>
                MortalWoundTreatmentPersistedResult.CreateHistory(owner, resolution, wrongClock));

            Assert.True(owner.HasCurrentAdmissionAuthority());
            Assert.True(request.Coordinates.MatchesAcceptedState(owner));
            Assert.True(preparation.AgreesWith(resolution));
            Assert.True(JsonNode.DeepEquals(envelope, captured.ToCanonicalJson()));
        }
        finally
        {
            var released = MortalWoundTreatmentResourceComposer.ReleaseTreatmentResources(files, lease,
                [otherRequest], "cancelled");
            Assert.True(released.IsValid, DescribeIssues(released.Issues));
        }
    }
}
