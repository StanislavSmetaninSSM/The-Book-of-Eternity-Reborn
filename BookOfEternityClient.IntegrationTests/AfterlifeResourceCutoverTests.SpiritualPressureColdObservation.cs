using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>
    /// Observes only DATA already produced by each genuine cold Open, without another classification or replay.
    /// This source-first observer contract is unqualified until its lease-only API exists and passes.
    /// </summary>
    [Fact]
    public async Task SpiritualPressureColdObservation_ReadsOnlyActualOpenedOfferData()
    {
        var prepared = await PrepareSpiritualColdObservationTemplateAsync();
        var readsA = new PassiveReadActivityAccumulator();
        var readsB = new PassiveReadActivityAccumulator();
        var readsC = new PassiveReadActivityAccumulator();
        var roots = new List<string>();
        try
        {
            await using var a = await MaterializeSpiritualColdObservationCopyAsync(prepared.Tree, readsA);
            roots.Add(a.RootPath);
            await using var b = await MaterializeSpiritualColdObservationCopyAsync(prepared.Tree, readsB);
            roots.Add(b.RootPath);
            await using var c = await MaterializeSpiritualColdObservationCopyAsync(prepared.Tree, readsC);
            roots.Add(c.RootPath);
            await using var leaseA = await a.FileSystem.AcquireCanonicalWriteLeaseAsync();
            await using var leaseB = await b.FileSystem.AcquireCanonicalWriteLeaseAsync();
            await using var leaseC = await c.FileSystem.AcquireCanonicalWriteLeaseAsync();
            Assert.Equal(4, new[] { prepared.Generation, a.FileSystem.ReadExistingSessionGeneration(leaseA),
                b.FileSystem.ReadExistingSessionGeneration(leaseB), c.FileSystem.ReadExistingSessionGeneration(leaseC) }.Distinct(StringComparer.Ordinal).Count());

            var openStarted = Stopwatch.GetTimestamp();
            var openedA = await new ValidationService(a.FileSystem, NullLogger<ValidationService>.Instance)
                .OpenC2PrivateSessionAsync(leaseA);
            AssertNoConflictFrameErrors(openedA.Issues);
            Assert.Equal("offer", openedA.Disposition);
            using var offerA = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(openedA.Session);
            var openedB = await new ValidationService(b.FileSystem, NullLogger<ValidationService>.Instance)
                .OpenC2PrivateSessionAsync(leaseB);
            AssertNoConflictFrameErrors(openedB.Issues);
            Assert.Equal("offer", openedB.Disposition);
            using var offerB = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(openedB.Session);
            var openedC = await new ValidationService(c.FileSystem, NullLogger<ValidationService>.Instance)
                .OpenC2PrivateSessionAsync(leaseC);
            AssertNoConflictFrameErrors(openedC.Issues);
            Assert.Equal("offer", openedC.Disposition);
            using var offerC = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(openedC.Session);
            WritePressureOwnerTiming("cold_observer_three_independent_opens", "ordinary", openStarted);
            var filesA = CaptureExactSpiritualFiles(a.FileSystem.GameSessionPath);
            var filesB = CaptureExactSpiritualFiles(b.FileSystem.GameSessionPath);
            var generationA = File.ReadAllBytes(a.FileSystem.SessionGenerationPath);
            var generationB = File.ReadAllBytes(b.FileSystem.SessionGenerationPath);

            readsA.Drain();
            readsB.Drain();
            var readStarted = Stopwatch.GetTimestamp();
            var first = await offerA.ReadOfferReplayEvidenceAsync(leaseA);
            AssertSpiritualColdOfferEvidence(prepared.Expected, first);
            Assert.Equal(first, await offerA.ReadOfferReplayEvidenceAsync(leaseA));
            Assert.Empty(readsA.Drain());
            WritePressureOwnerTiming("cold_observer_repeat", "A", readStarted);
            AssertExistingPressureInputInert(a, leaseA, filesA, generationA);

            // A foreign current lease cannot turn detached DATA into a different owner's observation.
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await offerA.ReadOfferReplayEvidenceAsync(leaseB));
            Assert.Equal(first, await offerA.ReadOfferReplayEvidenceAsync(leaseA));
            AssertSpiritualColdOfferEvidence(prepared.Expected, await offerB.ReadOfferReplayEvidenceAsync(leaseB));
            Assert.Empty(readsA.Drain());
            Assert.Empty(readsB.Drain());
            AssertExistingPressureInputInert(a, leaseA, filesA, generationA);
            AssertExistingPressureInputInert(b, leaseB, filesB, generationB);

            // A separate, independently opened live offer proves explicit Dispose, not merely a used flag.
            offerB.Dispose();
            await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
                await offerB.ReadOfferReplayEvidenceAsync(leaseB));
            Assert.Empty(readsB.Drain());
            AssertExistingPressureInputInert(b, leaseB, filesB, generationB);

            var declined = await offerA.SubmitDecisionAsync(leaseA, JsonSerializer.SerializeToElement(new
                { opportunityRef = offerA.Offer!.OpportunityRef, decision = "none" }), null);
            AssertNoConflictFrameErrors(declined.Issues);
            Assert.Equal("completed_unpublished", declined.Disposition);
            using var completed = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(declined.Session);
            Assert.Null(completed.Offer);
            var completedFiles = CaptureExactSpiritualFiles(a.FileSystem.GameSessionPath);
            var completedGeneration = File.ReadAllBytes(a.FileSystem.SessionGenerationPath);
            readsA.Drain();
            // Submit consumes and disposes the old offer; the returned live completed owner is not an offer.
            await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
                await offerA.ReadOfferReplayEvidenceAsync(leaseA));
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await completed.ReadOfferReplayEvidenceAsync(leaseA));
            completed.Dispose();
            await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
                await completed.ReadOfferReplayEvidenceAsync(leaseA));
            Assert.Empty(readsA.Drain());
            AssertExistingPressureInputInert(a, leaseA, completedFiles, completedGeneration);
            Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(a.FileSystem, leaseA));

            // A real post-command callback failure leaves _used true before Submit reaches its Dispose call.
            // The observer must reject this consumed offer independently of the physical disposed flag.
            var interruption = new InvalidOperationException("cold_observer_after_command_interruption");
            var interrupted = await Record.ExceptionAsync(() => offerC.SubmitDecisionAsync(leaseC,
                JsonSerializer.SerializeToElement(new { opportunityRef = offerC.Offer!.OpportunityRef, decision = "none" }),
                null, afterCommandWriteAsync: _ => Task.FromException(interruption)));
            Assert.Same(interruption, interrupted);
            var interruptedFiles = CaptureExactSpiritualFiles(c.FileSystem.GameSessionPath);
            var interruptedGeneration = File.ReadAllBytes(c.FileSystem.SessionGenerationPath);
            readsC.Drain();
            await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
                await offerC.ReadOfferReplayEvidenceAsync(leaseC));
            Assert.Empty(readsC.Drain());
            AssertExistingPressureInputInert(c, leaseC, interruptedFiles, interruptedGeneration);
        }
        finally
        {
            foreach (var root in roots)
                Assert.False(Directory.Exists(root), "The observer must remove its own isolated fixture root: " + root);
        }
    }

    /// <summary>Retains only immutable expected replay DATA, never a session, source owner, interval or ticket.</summary>
    private sealed record SpiritualColdExpectedOfferData(string ReconstructedPendingJson,
        string ActualSourceDataJson, int AllocationCursor, string AllocationJournalJson,
        string OriginalConflictBytesBase64, string OriginalConflictFingerprint);

    /// <summary>Retains a disposed genuine first-pair corpus and its detached warm evidence.</summary>
    private sealed record SpiritualColdObservationTemplate(PreparedFixtureTree Tree,
        SpiritualColdExpectedOfferData Expected, string Generation);

    /// <summary>Prepares the smallest supported ordinary one-source C2 contour exactly once.</summary>
    private async Task<SpiritualColdObservationTemplate> PrepareSpiritualColdObservationTemplateAsync()
    {
        var started = Stopwatch.GetTimestamp();
        var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync);
        try
        {
            await WriteCompleteConflictFrameExchangeAsync(context);
            await WriteOriginalIntakeDraftAsync(context);
            await context.WriteExactBytesAsync(ProjectionNarrativePath,
                Encoding.UTF8.GetBytes("{\"response\":\"Духовный обмен завершён.\"}"));
            AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync());
            AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync());
            SpiritualColdExpectedOfferData expected;
            string generation;
            await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
            {
                generation = Assert.IsType<string>(context.FileSystem.ReadExistingSessionGeneration(lease));
                var recorded = await context.Validator.CaptureSpiritualOriginalTurnWithIntakeAsync(lease);
                AssertNoConflictFrameErrors(recorded.Issues);
                using var capture = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(recorded.Capture);
                AssertNoConflictFrameErrors(await capture.BeginResourceExecutionAsync(lease));
                var advanced = await capture.AdvanceNextResourceExchangeAsync(lease);
                AssertNoConflictFrameErrors(advanced.Issues);
                var interval = Assert.IsType<AcceptedMechanicsPlanner.SpiritualExchangeInterval>(advanced.Step?.Interval);
                var source = Assert.Single(Assert.Single(capture.ReadClosedExchangeEvidence(lease)).Sources);
                var committed = await capture.CommitC2FirstTransportAsync(lease, interval);
                expected = ReadExpectedSpiritualColdOfferData(capture, lease, committed, source);
            }
            // GameSessionPath excludes physical generations and locks. Materialization happens only after return/disposal.
            var tree = PreparedFixtureTree.Capture(context.FileSystem.GameSessionPath);
            WritePressureOwnerTiming("cold_observer_template", "ordinary", started);
            return new(tree, expected, generation);
        }
        finally
        {
            await context.DisposeAsync();
            Assert.False(Directory.Exists(context.RootPath));
        }
    }

    /// <summary>Materializes immutable first-pair bytes into a fresh filesystem with passive read evidence only.</summary>
    private static async Task<ResourceMaterializationTestContext> MaterializeSpiritualColdObservationCopyAsync(
        PreparedFixtureTree tree, PassiveReadActivityAccumulator reads)
    {
        var context = await ResourceMaterializationTestContext.CreateAsync(new FileSystemManagerHooks
            { PassiveReadActivities = reads });
        try
        {
            var generation = File.ReadAllBytes(context.FileSystem.SessionGenerationPath);
            tree.Materialize(context.FileSystem.GameSessionPath);
            Assert.Equal(generation, File.ReadAllBytes(context.FileSystem.SessionGenerationPath));
            Assert.False(Directory.Exists(Path.Combine(context.FileSystem.GameSessionPath, ".boe_runtime")));
            return context;
        }
        catch
        {
            await context.DisposeAsync();
            Assert.False(Directory.Exists(context.RootPath));
            throw;
        }
    }

    /// <summary>Reads warm already-produced DATA independently of the proposed cold observer.</summary>
    private static SpiritualColdExpectedOfferData ReadExpectedSpiritualColdOfferData(
        ValidationService.SpiritualOriginalTurnCapture capture, FileSystemManager.CanonicalWriteLease lease,
        ValidationService.SpiritualC2InitialTransportResult committed, ValidationService.PreparedSpiritualSource source)
    {
        AssertNoConflictFrameErrors(committed.Issues);
        Assert.Equal("committed", committed.Disposition);
        var checkpoint = Assert.IsType<SpiritualWoundCaptureCheckpointState>(committed.Checkpoint);
        var pending = Assert.IsType<SpiritualWoundDecisionPendingState>(committed.Pending);
        var original = checkpoint.ReadOriginalDraftInputs().ReadImage(AfterlifeSpiritualConflictState.StatePath);
        Assert.True(original.Existed);
        var journal = capture.ReadAllocationJournal(lease);
        Assert.Equal(checkpoint.InitialAllocationCount, journal.Count);
        Assert.Equal(journal.Count, capture.ReadAllocationCursor(lease));
        return new(SpiritualWoundDecisionPendingState.SerializeCanonical(pending),
            SerializeSpiritualColdSourceData(source), capture.ReadAllocationCursor(lease), journal.ToJsonString(),
            Convert.ToBase64String(original.Bytes!), original.Fingerprint);
    }

    /// <summary>Explicitly projects every immutable source field, without serializing any internal owner object.</summary>
    private static string SerializeSpiritualColdSourceData(ValidationService.PreparedSpiritualSource source) =>
        JsonSerializer.Serialize(new { source.Coordinate, source.ConflictId, source.ExchangeId, source.AffectedSide,
            source.ActingActor, source.AffectedActor, source.Operation, source.OriginalSpecialArtJson,
            source.ExchangeJson, source.Calculation, source.GuaranteedSeverityRank, source.RetraumaWoundId,
            source.RetraumaWoundJson, source.StrainPreimageJson });

    /// <summary>Compares all six detached observer fields against independent genuine warm evidence.</summary>
    private static void AssertSpiritualColdOfferEvidence(SpiritualColdExpectedOfferData expected,
        ValidationService.SpiritualC2OfferReplayEvidence actual)
    {
        Assert.Equal(expected.ReconstructedPendingJson, actual.ReconstructedPendingJson);
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(expected.ActualSourceDataJson),
            JsonNode.Parse(actual.ActualSourceDataJson)));
        Assert.Equal(expected.AllocationCursor, actual.AllocationCursor);
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(expected.AllocationJournalJson),
            JsonNode.Parse(actual.AllocationJournalJson)));
        Assert.Equal(expected.OriginalConflictBytesBase64, actual.OriginalConflictBytesBase64);
        Assert.Equal(expected.OriginalConflictFingerprint, actual.OriginalConflictFingerprint);
    }
}
