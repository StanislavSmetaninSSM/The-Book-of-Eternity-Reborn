using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    private readonly ITestOutputHelper _storageOutput;
    public AfterlifeResourceCutoverTests(ITestOutputHelper output) => _storageOutput = output;

    [Theory]
    [InlineData("initial_checkpoint_unknown")]
    [InlineData("initial_pending_unknown")]
    [InlineData("initial_success")]
    [InlineData("submission_unknown")]
    [InlineData("saved_checkpoint_unknown")]
    [InlineData("saved_pending_unknown")]
    [InlineData("saved_success")]
    [InlineData("repair_unknown")]
    [InlineData("repair_success")]
    [InlineData("dependent_unknown")]
    [InlineData("dependent_success")]
    public async Task OriginalSpiritualPrivateTransportPreservesCanonicalPublicationOutcome(string mode)
    {
        Assert.True(OperatingSystem.IsLinux());
        using var probe = new WorkerStorageProbe();
        ResourceMaterializationTestContext? context = null;
        ValidationService.SpiritualOriginalTurnCapture? capture = null;
        FileSystemManager.CanonicalWriteLease? lease = null;
        Exception? failure = null;
        Exception? closeFailure = null;
        string? disposition = null;
        bool? captureCurrentAfter = null;
        string? nextContinuation = null;
        string? originalContinuation = null;
        object? atCut = null;
        var uncertain = mode.EndsWith("_unknown", StringComparison.Ordinal);
        try
        {
            if (mode.StartsWith("dependent_", StringComparison.Ordinal))
                context = (await CreatePreparedSpiritualStagedContextAsync(false, probe.Hooks)).Context;
            else
                context = await CreateCompleteConflictFrameContextAsync(probe.Hooks,
                    seedOriginalInputs: SeedOriginalIntakeBaselinesAsync);
            var fs = context.FileSystem;
            probe.Attach(fs);
            probe.Cut.Armed = true;
            foreach (var path in Directory.EnumerateFiles(fs.GameSessionPath, "*", SearchOption.AllDirectories))
                if (!path.EndsWith(".lock", StringComparison.Ordinal))
                    probe.Committed[path] = File.ReadAllBytes(path);
            if (mode.StartsWith("initial_", StringComparison.Ordinal))
            {
                await WriteCompleteConflictFrameExchangeAsync(context);
                await WriteOriginalIntakeDraftAsync(context);
                await context.WriteExactBytesAsync(ProjectionNarrativePath,
                    Encoding.UTF8.GetBytes("{\"response\":\"Духовный обмен завершён.\"}"));
                AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync());
                AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync());
            }
            else if (!mode.StartsWith("dependent_", StringComparison.Ordinal))
                await CommitInitialC2PairAsync(context);

            lease = await fs.AcquireCanonicalWriteLeaseAsync();
            var generation = fs.ReadExistingSessionGeneration(lease);
            Assert.False(string.IsNullOrWhiteSpace(generation));
            AcceptedMechanicsPlanner.SpiritualExchangeInterval? interval = null;
            SpiritualWoundContinuationRequest? dependent = null;
            if (mode.StartsWith("initial_", StringComparison.Ordinal))
            {
                var recorded = await context.Validator.CaptureSpiritualOriginalTurnWithIntakeAsync(lease);
                AssertNoConflictFrameErrors(recorded.Issues);
                capture = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(recorded.Capture);
                AssertNoConflictFrameErrors(await capture.BeginResourceExecutionAsync(lease));
                var advanced = await capture.AdvanceNextResourceExchangeAsync(lease);
                AssertNoConflictFrameErrors(advanced.Issues);
                interval = Assert.IsType<AcceptedMechanicsPlanner.SpiritualExchangeInterval>(advanced.Step?.Interval);
            }
            else if (mode.StartsWith("saved_", StringComparison.Ordinal) || mode == "submission_unknown")
            {
                _ = await StageC2SavedDecisionAsync(context, lease, materialize: false);
                var classified = await new ValidationService(fs, NullLogger<ValidationService>.Instance)
                    .ClassifyInitialSpiritualPendingAsync(lease);
                Assert.Equal("match", classified.Disposition);
                capture = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(classified.Capture);
            }
            else if (mode.StartsWith("repair_", StringComparison.Ordinal))
                fs.DeleteFile(lease, SpiritualWoundDecisionPendingState.StatePath);
            else
            {
                var projection = await context.Validator.ReadSpiritualWoundContinuationAsync(lease);
                Assert.Equal("dependent_draft", projection.Disposition);
                dependent = Assert.IsType<SpiritualWoundContinuationRequest>(projection.Request);
                originalContinuation = dependent.ContinuationId;
            }
            var target = fs.ResolvePath(mode.Contains("pending", StringComparison.Ordinal) || mode.StartsWith("repair_", StringComparison.Ordinal)
                ? SpiritualWoundDecisionPendingState.StatePath : SpiritualWoundCaptureCheckpointState.StatePath);
            probe.Target = uncertain ? target : null;
            probe.Cut.Select = (path, _) =>
            {
                if (!uncertain || path != target) return false;
                if (mode.StartsWith("saved_", StringComparison.Ordinal))
                {
                    var checkpoint = JsonNode.Parse(File.ReadAllBytes(fs.ResolvePath(SpiritualWoundCaptureCheckpointState.StatePath)))!;
                    return checkpoint["checkpoint"]?["committedAdvance"]?.GetValue<int>() == 1;
                }
                if (mode == "submission_unknown")
                    return JsonNode.Parse(File.ReadAllBytes(target))!["checkpoint"]?["pendingSubmission"] is JsonObject;
                return true;
            };
            var retainPrior = probe.Cut.BeforeCut;
            probe.Cut.BeforeCut = () =>
            {
                retainPrior!();
                atCut = new
                {
                    generation, captureCurrent = capture?.IsCurrentOwner,
                    checkpoint = CleanupPublicationCut.ReadOptional(fs.ResolvePath(SpiritualWoundCaptureCheckpointState.StatePath)),
                    pending = CleanupPublicationCut.ReadOptional(fs.ResolvePath(SpiritualWoundDecisionPendingState.StatePath)),
                    request = CleanupPublicationCut.ReadOptional(fs.ResolvePath("input/turn_request.json")),
                    command = CleanupPublicationCut.ReadOptional(fs.ResolvePath(AcceptedMechanicsPlan.WoundCommandPath)),
                    originalContinuation
                };
            };
            failure = await Record.ExceptionAsync(async () =>
            {
                if (interval != null)
                    disposition = (await capture!.CommitC2FirstTransportAsync(lease, interval)).Disposition;
                else if (capture != null)
                    disposition = (await capture.CommitC2SavedTransportAsync(lease)).Disposition;
                else if (dependent != null)
                {
                    var result = await context.Validator.EvaluateAndCommitSpiritualWoundDependentResponseAsync(
                        lease, dependent, CreateSpiritualStagedEmptyResponse(dependent));
                    disposition = result.Progress.Disposition;
                    nextContinuation = result.Progress.NextRequest?.ContinuationId;
                }
                else
                {
                    var repaired = await context.Validator.RepairInitialSpiritualPendingAsync(lease);
                    using var repairedCapture = repaired.Capture;
                    disposition = repaired.Disposition;
                }
            });
            captureCurrentAfter = capture?.IsCurrentOwner;
            // The service APIs borrow this exact lease. The caller records its
            // actual body failure before closing; it supplies no synthetic outcome.
            if (failure is CoordinatedStatePublicationUncertainException actual)
                lease.CaptureOperationFailure(actual);
            capture?.Dispose();
            closeFailure = await Record.ExceptionAsync(async () => await lease.DisposeAsync());
            lease = null;
            var afterImages = probe.PriorAtCut?.ToDictionary(pair => pair.Key,
                pair => CleanupPublicationCut.ReadOptional(pair.Key), StringComparer.Ordinal);
            _storageOutput.WriteLine(JsonSerializer.Serialize(new
            {
                mode, uncertain, failure = failure?.ToString(), closeFailure = closeFailure?.ToString(),
                SameOriginalUncertainty = probe.Cut.OriginalUncertainty != null && ReferenceEquals(probe.Cut.OriginalUncertainty, failure),
                disposition, captureCurrentAfter, captureCurrentFinal = capture?.IsCurrentOwner,
                originalContinuation, nextContinuation, atCut, probe.PriorAtCut, afterImages, Cut = probe.Cut.Evidence()
            }));
            Assert.Null(closeFailure);
            if (uncertain)
            {
                probe.Cut.AssertReachedAndStopped();
                Assert.Same(probe.Cut.OriginalUncertainty, failure);
                Assert.Null(disposition); Assert.Null(nextContinuation); Assert.NotNull(atCut);
                Assert.NotNull(probe.PriorAtCut);
                foreach (var pair in probe.PriorAtCut!) Assert.Equal(pair.Value, afterImages![pair.Key]);
                if (mode == "submission_unknown") Assert.False(captureCurrentAfter);
            }
            else
            {
                Assert.Null(failure); Assert.Equal(0, probe.Cut.Cuts);
                Assert.Equal(mode == "repair_success" ? "repaired" : "committed", disposition);
                Assert.False(File.Exists(probe.Cut.JournalPath));
                if (dependent != null)
                {
                    Assert.NotNull(nextContinuation);
                    Assert.NotEqual(originalContinuation, nextContinuation);
                }
            }
            if (capture != null) Assert.False(capture.IsCurrentOwner);
        }
        finally
        {
            try
            {
                try { capture?.Dispose(); }
                finally { if (lease != null) await lease.DisposeAsync(); }
            }
            finally
            {
                if (context != null)
                {
                    await context.DisposeAsync();
                    _storageOutput.WriteLine(JsonSerializer.Serialize(new
                    {
                        CleanupOwnedRoot = context.RootPath, OwnedFixtureRemoved = !Directory.Exists(context.RootPath), CleanupFailure = (string?)null
                    }));
                    Assert.False(Directory.Exists(context.RootPath));
                }
            }
        }
    }
}
