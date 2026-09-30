using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>
    /// Publishes a genuinely completed training exchange and its instance receipt without inventing a wound decision.
    /// </summary>
    /// <param name="terminal">
    /// Whether the final exchange resolves the conflict and requires its owned retirement and closure.
    /// </param>
    /// <param name="emptyWrappers">
    /// Whether signed empty private roots must be preserved until common publication consumes them.
    /// </param>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task OriginalSpiritualC4SourceOnly_PublishesZeroCeilingWithoutDecision(
        bool terminal, bool emptyWrappers)
    {
        await using var context = await CreateSourceOnlyPublicationContextAsync(terminal, emptyWrappers);
        var signedManifest = await context.FileSystem.ReadFileBytesAsync(
            "game_state/control/pending_turn_snapshot.json");
        var originalCheckpoint = await context.FileSystem.ReadFileBytesAsync(SpiritualWoundCaptureCheckpointState.StatePath);
        var originalPending = await context.FileSystem.ReadFileBytesAsync(SpiritualWoundDecisionPendingState.StatePath);
        var resourcesBefore = await context.FileSystem.ReadFileBytesAsync(ResourceMaterializationContract.StatePath);
        AcceptedMechanicsPlan expected;
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            var recorded = await context.Validator.CaptureSpiritualOriginalTurnWithIntakeAsync(lease);
            AssertNoConflictFrameErrors(recorded.Issues);
            using var capture = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(recorded.Capture);
            AssertNoConflictFrameErrors(await capture.BeginResourceExecutionAsync(lease));
            var advanced = await capture.AdvanceNextResourceExchangeAsync(lease);
            AssertNoConflictFrameErrors(advanced.Issues);
            var interval = Assert.IsType<AcceptedMechanicsPlanner.SpiritualExchangeInterval>(advanced.Step?.Interval);
            var noOffer = await capture.CommitC2FirstTransportAsync(lease, interval);
            AssertNoConflictFrameErrors(noOffer.Issues);
            Assert.Equal("no_offer", noOffer.Disposition);
            Assert.Null(noOffer.Checkpoint);
            Assert.Null(noOffer.Pending);

            var completed = await capture.CompleteOrdinaryReductionAsync(lease);
            AssertNoConflictFrameErrors(completed.Issues);
            Assert.True(completed.Success);
            Assert.NotNull(completed.Reduction);
            var costs = completed.Reduction.Resources.AppliedTransitions.Where(value =>
                value.OriginId == "exchange_conflict_frame_42" &&
                value.Operation == ResourceTransitionOperation.Spend).ToArray();
            Assert.Equal(2, costs.Length);
            Assert.All(costs, value => Assert.Equal(3m, value.AfterState!.Current));
            var retirements = completed.Reduction.Resources.AppliedTransitions.Where(value =>
                value.Operation == ResourceTransitionOperation.Retire &&
                value.Coordinate.OwnerKind == ResourceOwnerKind.AfterlifeConflictSide).ToArray();
            if (terminal)
            {
                var retirement = Assert.Single(retirements);
                Assert.All(costs, value => Assert.True(value.ExecutionSequence < retirement.ExecutionSequence));
            }
            else
                Assert.Empty(retirements);
            var allocations = capture.ReadAllocationJournal(lease).ToJsonString();
            var cursor = capture.ReadAllocationCursor(lease);

            var prepared = await capture.PrepareC4PublicationAsync(lease);

            Assert.True(prepared.Authority is not null,
                string.Join(Environment.NewLine, prepared.Issues.Select(issue => $"{issue.Code}: {issue}")));
            AssertNoConflictFrameErrors(prepared.Issues);
            var authority = prepared.Authority!;
            expected = authority.Plan;
            Assert.Equal(allocations, capture.ReadAllocationJournal(lease).ToJsonString());
            Assert.Equal(cursor, capture.ReadAllocationCursor(lease));
            Assert.Null(authority.LiveWoundCompletion);
            var receipt = expected.OwnerCompanionAfterImages[SpiritualWoundOpportunityReceiptState.StatePath];
            var instance = Assert.Single(receipt["instances"]!.AsArray())!;
            Assert.Empty(receipt["sources"]!.AsArray());
            Assert.Empty(receipt["decisions"]!.AsArray());
            if (terminal)
            {
                var closure = Assert.Single(receipt["closures"]!.AsArray())!;
                Assert.Equal(instance["instanceId"]!.GetValue<string>(), closure["instanceId"]!.GetValue<string>());
                Assert.Equal(42, closure["terminalTurn"]!.GetValue<int>());
            }
            else
                Assert.Empty(receipt["closures"]!.AsArray());
            Assert.Empty(await authority.ValidateCurrentInputsAsync(context.FileSystem, lease));
            Assert.True(AcceptedMechanicsPlanAuthority.RegisterSpiritualPublication(context.FileSystem, lease, authority));
            Assert.Equal(resourcesBefore, await context.FileSystem.ReadFileBytesAsync(lease,
                ResourceMaterializationContract.StatePath));
            Assert.Equal(originalCheckpoint, await context.FileSystem.ReadFileBytesAsync(lease,
                SpiritualWoundCaptureCheckpointState.StatePath));
            Assert.Equal(originalPending, await context.FileSystem.ReadFileBytesAsync(lease,
                SpiritualWoundDecisionPendingState.StatePath));
            Assert.Equal(originalCheckpoint, authority.PublicationInputs[SpiritualWoundCaptureCheckpointState.StatePath].Bytes);
            Assert.Equal(originalPending, authority.PublicationInputs[SpiritualWoundDecisionPendingState.StatePath].Bytes);
            Assert.Null(await context.FileSystem.ReadFileBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath));
        }

        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawResourceMaterializationAsync());
        var retained = await PeekPlanAsync(context);
        Assert.Same(expected, retained);
        var fingerprint = expected.PreparedPlanFingerprint;
        var backups = await ReadSpiritualC4BackupsAsync(context);
        var published = await AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(
            context.FileSystem, context.Normalizer, context.Validator, backups);
        AssertNoConflictFrameErrors(published.Issues);
        Assert.Same(expected, published.MechanicsPlan);
        Assert.Equal(fingerprint, expected.PreparedPlanFingerprint);
        Assert.True(JsonNode.DeepEquals(expected.StateAfterImage,
            await context.ReadJsonAsync(ResourceMaterializationContract.StatePath)));
        Assert.True(JsonNode.DeepEquals(expected.HistoryAfterImage,
            await context.ReadJsonAsync(ResourceMaterializationContract.HistoryPath)));
        Assert.True(JsonNode.DeepEquals(expected.EffectIdentityAfterImage,
            await context.ReadJsonAsync(EffectAcceptedTurnPlan.IdentityIndexPath)));
        foreach (var pair in expected.OwnerCompanionAfterImages)
            Assert.True(JsonNode.DeepEquals(pair.Value, await context.ReadJsonAsync(pair.Key)), pair.Key);
        foreach (var pair in expected.EffectCarrierAfterImages)
            Assert.True(JsonNode.DeepEquals(pair.Value, await context.ReadJsonAsync(pair.Key)), pair.Key);
        Assert.Equal(signedManifest, await context.FileSystem.ReadFileBytesAsync(
            "game_state/control/pending_turn_snapshot.json"));
        foreach (var path in new[] { SpiritualWoundCaptureCheckpointState.StatePath,
                     SpiritualWoundDecisionPendingState.StatePath, AcceptedMechanicsPlan.WoundCommandPath })
        {
            Assert.Contains(path, expected.ConsumedPaths);
            Assert.Null(await context.FileSystem.ReadFileBytesAsync(path));
        }
    }

    /// <summary>
    /// Rejects publication when an input changes after real source-only completion.
    /// </summary>
    /// <param name="path">
    /// Frozen command, conflict or private-control image changed before handoff.
    /// </param>
    [Theory]
    [InlineData(AcceptedMechanicsPlan.WoundCommandPath)]
    [InlineData(AfterlifeSpiritualConflictState.StatePath)]
    [InlineData(SpiritualWoundCaptureCheckpointState.StatePath)]
    public async Task OriginalSpiritualC4SourceOnly_RejectsChangedInput(string path)
    {
        await using var context = await CreateSourceOnlyPublicationContextAsync(terminal: false, emptyWrappers: false);
        var resources = await context.FileSystem.ReadFileBytesAsync(ResourceMaterializationContract.StatePath);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        using var capture = await CompleteSourceOnlyPublicationCaptureAsync(context, lease);
        var prior = await context.FileSystem.ReadFileBytesAsync(lease, path);
        var changed = prior is null ? Encoding.UTF8.GetBytes(path == SpiritualWoundCaptureCheckpointState.StatePath
            ? "{\"schemaVersion\":1,\"checkpoint\":null}" : "{}") : [.. prior, (byte)' '];
        await context.FileSystem.WriteFileAtomicBytesAsync(lease, path, changed);

        var prepared = await capture.PrepareC4PublicationAsync(lease);

        Assert.Null(prepared.Authority);
        Assert.NotEmpty(prepared.Issues);
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
        Assert.Equal(changed, await context.FileSystem.ReadFileBytesAsync(lease, path));
        Assert.Equal(resources, await context.FileSystem.ReadFileBytesAsync(lease, ResourceMaterializationContract.StatePath));
    }

    /// <summary>
    /// Requires actual source exhaustion and rejects a positive opportunity without inventing its decline.
    /// </summary>
    /// <param name="positiveSource">
    /// Whether to execute a dangerous exchange with an unresolved positive source instead of leaving training unexecuted.
    /// </param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OriginalSpiritualC4SourceOnly_RejectsUncompletedCapture(bool positiveSource)
    {
        await using var context = await CreateSourceOnlyPublicationContextAsync(terminal: false,
            emptyWrappers: false, training: !positiveSource);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var recorded = await context.Validator.CaptureSpiritualOriginalTurnWithIntakeAsync(lease);
        AssertNoConflictFrameErrors(recorded.Issues);
        using var capture = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(recorded.Capture);
        AssertNoConflictFrameErrors(await capture.BeginResourceExecutionAsync(lease));
        if (positiveSource)
            AssertNoConflictFrameErrors((await capture.AdvanceNextResourceExchangeAsync(lease)).Issues);

        var prepared = await capture.PrepareC4PublicationAsync(lease);

        Assert.Null(prepared.Authority);
        Assert.Contains(prepared.Issues, issue => issue.Code == "spiritual_c3_source_only_completion_required");
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
        Assert.Null(await context.FileSystem.ReadFileBytesAsync(lease, SpiritualWoundCaptureCheckpointState.StatePath));
        Assert.Null(await context.FileSystem.ReadFileBytesAsync(lease, SpiritualWoundDecisionPendingState.StatePath));
        Assert.Null(await context.FileSystem.ReadFileBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath));
    }

    /// <summary>
    /// Restores signed original images, including empty or absent private roots, after a source-only publication failure.
    /// </summary>
    /// <param name="emptyWrappers">
    /// Whether rollback must restore exact signed empty private roots instead of their absence.
    /// </param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OriginalSpiritualC4SourceOnly_PublicationFailureRestoresOriginal(bool emptyWrappers)
    {
        var armed = false;
        var injected = false;
        var hooks = new FileSystemManagerHooks
        {
            BeforeCanonicalMutationAsync = path =>
            {
                if (armed && path == ResourceMaterializationContract.StatePath)
                {
                    injected = true;
                    armed = false;
                    throw new IOException("Source-only common publication failure.");
                }
                return Task.CompletedTask;
            }
        };
        await using var context = await CreateSourceOnlyPublicationContextAsync(terminal: true,
            emptyWrappers, hooks: hooks);
        IReadOnlyDictionary<string, CanonicalBeforeImage> originals;
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            using var capture = await CompleteSourceOnlyPublicationCaptureAsync(context, lease);
            var prepared = await capture.PrepareC4PublicationAsync(lease);
            AssertNoConflictFrameErrors(prepared.Issues);
            Assert.NotNull(prepared.Authority);
            originals = prepared.Authority.SignedRollbackImages;
            Assert.True(AcceptedMechanicsPlanAuthority.RegisterSpiritualPublication(
                context.FileSystem, lease, prepared.Authority));
        }
        var backups = await ReadSpiritualC4BackupsAsync(context);
        armed = true;

        await Assert.ThrowsAnyAsync<IOException>(() => AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(
            context.FileSystem, context.Normalizer, context.Validator, backups));

        Assert.True(injected);
        foreach (var pair in originals)
            Assert.Equal(pair.Value.Bytes, await context.FileSystem.ReadFileBytesAsync(pair.Key));
        await using var finalLease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, finalLease));
    }

    /// <summary>
    /// Creates the real original signed fixture and admitted drafts without a private decision transport.
    /// </summary>
    /// <param name="terminal">
    /// Whether the authored exchange resolves the conflict through the existing terminal fixture.
    /// </param>
    /// <param name="emptyWrappers">
    /// Whether to sign exact empty checkpoint and pending wrappers before authoring the exchange.
    /// </param>
    /// <param name="training">
    /// Whether the signed conflict has zero-ceiling training harm; false preserves the positive fixture.
    /// </param>
    /// <param name="hooks">
    /// Optional filesystem fault hooks, inactive while preparing the fixture.
    /// </param>
    /// <returns>
    /// A disposable fixture with validated location and item intake ready for genuine original capture.
    /// </returns>
    private static async Task<ResourceMaterializationTestContext> CreateSourceOnlyPublicationContextAsync(
        bool terminal, bool emptyWrappers, bool training = true, FileSystemManagerHooks? hooks = null)
    {
        var context = await CreateCompleteConflictFrameContextAsync(hooks,
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync);
        try
        {
            var conflict = Assert.IsType<JsonObject>(await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath));
            if (training)
                conflict["activeConflict"]!["dangerMode"] = "training";
            await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, conflict.ToJsonString());
            if (emptyWrappers)
            {
                await context.WriteExactJsonAsync(SpiritualWoundCaptureCheckpointState.StatePath,
                    "{ \"schemaVersion\": 1, \"checkpoint\": null }\n");
                await context.WriteExactJsonAsync(SpiritualWoundDecisionPendingState.StatePath,
                    "{ \"schemaVersion\": 1, \"pending\": null }\n");
            }
            await context.CaptureValidatedPendingSnapshotAsync(turn: 42, currentRealm: "Chaos Sea",
                preGeneratedDices1d20: [15, 5, 12, 8]);
            await WriteCompleteConflictFrameExchangeAsync(context);
            if (terminal)
            {
                var candidate = await ReadProjectedSourceContinuationCandidateAsync(context);
                ResolveTerminalFixture(candidate);
                await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, candidate.ToJsonString());
            }
            await WriteOriginalIntakeDraftAsync(context);
            await context.WriteExactBytesAsync(ProjectionNarrativePath,
                Encoding.UTF8.GetBytes("{\"response\":\"Тренировочный обмен завершён.\"}"));
            AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync());
            AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync());
            return context;
        }
        catch
        {
            await context.DisposeAsync();
            throw;
        }
    }

    /// <summary>
    /// Completes the finite signed exchange through its actual owners without producing a C2 packet.
    /// </summary>
    /// <param name="context">
    /// Admitted zero-ceiling fixture with one original exchange.
    /// </param>
    /// <param name="lease">
    /// Active canonical lease held through capture and ordinary completion.
    /// </param>
    /// <returns>
    /// Completed disposable capture retaining its original execution and allocation identities.
    /// </returns>
    private static async Task<ValidationService.SpiritualOriginalTurnCapture> CompleteSourceOnlyPublicationCaptureAsync(
        ResourceMaterializationTestContext context, FileSystemManager.CanonicalWriteLease lease)
    {
        var recorded = await context.Validator.CaptureSpiritualOriginalTurnWithIntakeAsync(lease);
        AssertNoConflictFrameErrors(recorded.Issues);
        var capture = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(recorded.Capture);
        try
        {
            AssertNoConflictFrameErrors(await capture.BeginResourceExecutionAsync(lease));
            AssertNoConflictFrameErrors((await capture.AdvanceNextResourceExchangeAsync(lease)).Issues);
            var completed = await capture.CompleteOrdinaryReductionAsync(lease);
            AssertNoConflictFrameErrors(completed.Issues);
            Assert.True(completed.Success);
            return capture;
        }
        catch
        {
            capture.Dispose();
            throw;
        }
    }
}
