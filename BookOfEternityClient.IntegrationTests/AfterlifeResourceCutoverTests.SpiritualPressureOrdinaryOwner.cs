using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    private static readonly string[] PressureOrdinarySignedPaths =
    [
        AfterlifeSpiritualConflictState.StatePath, AfterlifeEntityProfileState.StatePath,
        ValidationService.SpiritualWoundSourceSession.SoulPath, WoundIdentityState.StatePath,
        WoundHistoryState.HistoryPath, EffectIdentityState.StatePath,
        ResourceMaterializationContract.StatePath, ResourceMaterializationContract.HistoryPath,
        SpiritualWoundOpportunityReceiptState.StatePath
    ];

    /// <summary>
    /// Requires genuine isolated ordinary comparison, warm DATA reuse and independent current owners.
    /// This source-first fixture is not runtime-qualified until the owned comparison seams exist and pass.
    /// </summary>
    [Fact]
    public async Task SpiritualExistingPressure_OrdinaryComparisonReusesDataAndKeepsIndependentOwners()
    {
        var prepared = await PreparePressureOrdinaryOwnerTemplateAsync();
        var contexts = new List<ResourceMaterializationTestContext>();
        try
        {
            var a = await MaterializePressureOrdinaryOwnerCopyAsync(prepared, "A");
            contexts.Add(a.Context);
            var b = await MaterializePressureOrdinaryOwnerCopyAsync(prepared, "B");
            contexts.Add(b.Context);
            var c = await MaterializePressureOrdinaryOwnerCopyAsync(prepared, "C");
            contexts.Add(c.Context);
            Assert.Equal(4, new[] { prepared.Generation, a.Generation, b.Generation, c.Generation }
                .Distinct(StringComparer.Ordinal).Count());

            await using var leaseA = await a.Context.FileSystem.AcquireCanonicalWriteLeaseAsync();
            await using var leaseB = await b.Context.FileSystem.AcquireCanonicalWriteLeaseAsync();
            await using var leaseC = await c.Context.FileSystem.AcquireCanonicalWriteLeaseAsync();
            using var ownerA = await BeginPressureOrdinaryOwnerAsync(a, leaseA, "A");
            using var ownerB = await BeginPressureOrdinaryOwnerAsync(b, leaseB, "B");
            using var ownerC = await BeginPressureOrdinaryOwnerAsync(c, leaseC, "C");
            var filesA = CaptureExactSpiritualFiles(a.Context.FileSystem.GameSessionPath);
            var filesB = CaptureExactSpiritualFiles(b.Context.FileSystem.GameSessionPath);
            var filesC = CaptureExactSpiritualFiles(c.Context.FileSystem.GameSessionPath);
            var generationA = File.ReadAllBytes(a.Context.FileSystem.SessionGenerationPath);
            var generationB = File.ReadAllBytes(b.Context.FileSystem.SessionGenerationPath);
            var generationC = File.ReadAllBytes(c.Context.FileSystem.SessionGenerationPath);

            // Explicit real prefix preparation has finished before these comparison-only witnesses.
            // Prefix effects or time allocations are legitimate preparation, not a probe mutation.
            var countsA = ownerA.ReadExistingStrainReuseCounts();
            var countsB = ownerB.ReadExistingStrainReuseCounts();
            var beforeA = await ReadPressureOwnerSnapshotAsync(ownerA, leaseA, "A_before");
            var beforeB = await ReadPressureOwnerSnapshotAsync(ownerB, leaseB, "B_before");
            Assert.Equal(countsA, ownerA.ReadExistingStrainReuseCounts());
            Assert.Equal(countsB, ownerB.ReadExistingStrainReuseCounts());
            AssertPressureOrdinaryFrontier(beforeA, a, ownerA, leaseA);
            AssertPressureOrdinaryFrontier(beforeB, b, ownerB, leaseB);

            await MeasurePressureOwnerPhaseAsync("foreign_lease", "A_with_B", async () =>
                await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                    await ownerA.ReadExistingPressureOrdinaryComparisonAsync(leaseB)));
            await MeasurePressureOwnerPhaseAsync("foreign_lease", "B_with_A", async () =>
                await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                    await ownerB.ReadExistingPressureOrdinaryComparisonAsync(leaseA)));
            Assert.True(ownerA.IsCurrentOwner);
            Assert.True(ownerB.IsCurrentOwner);
            Assert.Equal(countsA, ownerA.ReadExistingStrainReuseCounts());
            Assert.Equal(countsB, ownerB.ReadExistingStrainReuseCounts());

            var miss = await MeasurePressureOwnerPhaseAsync("ordinary_miss", "A",
                () => ownerA.ReadExistingPressureOrdinaryComparisonAsync(leaseA));
            AssertNoConflictFrameErrors(miss.Issues);
            Assert.False(miss.PrefixBlocked);
            Assert.NotNull(miss.Comparison);
            AssertPressureOrdinaryComparison(miss.Comparison, a, filesA);
            var missCounts = ownerA.ReadExistingStrainReuseCounts();
            Assert.Equal(countsA.Queries + 1, missCounts.Queries);
            Assert.Equal(countsA.OrdinaryEvaluations + 1, missCounts.OrdinaryEvaluations);
            Assert.Equal(countsA.ActualEvaluations, missCounts.ActualEvaluations);
            Assert.Equal(countsA.SignedParentReads + 1, missCounts.SignedParentReads);
            Assert.True(missCounts.FreshInputChecks > countsA.FreshInputChecks);
            Assert.True(missCounts.FreshDependencyChecks > countsA.FreshDependencyChecks);
            var afterMiss = await ReadPressureOwnerSnapshotAsync(ownerA, leaseA, "A_after_miss");
            AssertPressureOrdinaryFrontiersEqual(beforeA, afterMiss);
            AssertPressureOrdinaryFrontier(afterMiss, a, ownerA, leaseA);

            var warm = await MeasurePressureOwnerPhaseAsync("ordinary_hit", "A",
                () => ownerA.ReadExistingPressureOrdinaryComparisonAsync(leaseA));
            AssertNoConflictFrameErrors(warm.Issues);
            Assert.False(warm.PrefixBlocked);
            Assert.NotNull(warm.Comparison);
            AssertPressureOrdinaryComparison(warm.Comparison, a, filesA);
            var warmCounts = ownerA.ReadExistingStrainReuseCounts();
            Assert.Equal(missCounts.Queries + 1, warmCounts.Queries);
            Assert.Equal(missCounts.OrdinaryEvaluations, warmCounts.OrdinaryEvaluations);
            Assert.Equal(missCounts.ActualEvaluations, warmCounts.ActualEvaluations);
            Assert.Equal(missCounts.SignedParentReads, warmCounts.SignedParentReads);
            Assert.True(warmCounts.FreshInputChecks > missCounts.FreshInputChecks);
            Assert.True(warmCounts.FreshDependencyChecks > missCounts.FreshDependencyChecks);

            var detached = miss.Comparison.ReadOrdinaryExchange();
            detached["after"]!["playerSideStrain"] = "broken";
            var detachedRead = await MeasurePressureOwnerPhaseAsync("ordinary_detached_hit", "A",
                () => ownerA.ReadExistingPressureOrdinaryComparisonAsync(leaseA));
            AssertNoConflictFrameErrors(detachedRead.Issues);
            Assert.False(detachedRead.PrefixBlocked);
            Assert.NotNull(detachedRead.Comparison);
            AssertPressureOrdinaryComparison(detachedRead.Comparison, a, filesA);
            var detachedCounts = ownerA.ReadExistingStrainReuseCounts();
            Assert.Equal(warmCounts.Queries + 1, detachedCounts.Queries);
            Assert.Equal(warmCounts.OrdinaryEvaluations, detachedCounts.OrdinaryEvaluations);
            Assert.Equal(warmCounts.ActualEvaluations, detachedCounts.ActualEvaluations);
            Assert.Equal(warmCounts.SignedParentReads, detachedCounts.SignedParentReads);
            Assert.True(detachedCounts.FreshInputChecks > warmCounts.FreshInputChecks);
            Assert.True(detachedCounts.FreshDependencyChecks > warmCounts.FreshDependencyChecks);
            AssertPressureOrdinaryFrontiersEqual(beforeA,
                await ReadPressureOwnerSnapshotAsync(ownerA, leaseA, "A_after_hits"));
            AssertPressureOrdinaryFrontiersEqual(beforeB,
                await ReadPressureOwnerSnapshotAsync(ownerB, leaseB, "B_after_A_hits"));
            Assert.Equal(countsB, ownerB.ReadExistingStrainReuseCounts());
            AssertExistingPressureInputInert(a.Context, leaseA, filesA, generationA);
            AssertExistingPressureInputInert(b.Context, leaseB, filesB, generationB);

            var disposalStarted = Stopwatch.GetTimestamp();
            ownerA.Dispose();
            WritePressureOwnerTiming("capture_disposal", "A", disposalStarted);
            await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
                await ownerA.ReadExistingPressureOrdinaryComparisonAsync(leaseA));
            await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
                await ownerA.ReadExistingPressureOrdinaryProbeStateAsync(leaseA));
            Assert.False(ownerA.IsCurrentOwner);
            AssertExistingPressureInputInert(a.Context, leaseA, filesA, generationA);
            Assert.True(ownerB.IsCurrentOwner);
            AssertPressureOrdinaryFrontiersEqual(beforeB,
                await ReadPressureOwnerSnapshotAsync(ownerB, leaseB, "B_after_A_disposal"));
            var independent = await MeasurePressureOwnerPhaseAsync("ordinary_miss", "B_after_A_disposal",
                () => ownerB.ReadExistingPressureOrdinaryComparisonAsync(leaseB));
            AssertNoConflictFrameErrors(independent.Issues);
            Assert.False(independent.PrefixBlocked);
            Assert.NotNull(independent.Comparison);
            AssertPressureOrdinaryComparison(independent.Comparison, b, filesB);
            var independentCounts = ownerB.ReadExistingStrainReuseCounts();
            Assert.Equal(countsB.Queries + 1, independentCounts.Queries);
            Assert.Equal(countsB.OrdinaryEvaluations + 1, independentCounts.OrdinaryEvaluations);
            Assert.Equal(countsB.ActualEvaluations, independentCounts.ActualEvaluations);
            Assert.Equal(countsB.SignedParentReads + 1, independentCounts.SignedParentReads);
            AssertPressureOrdinaryFrontiersEqual(beforeB,
                await ReadPressureOwnerSnapshotAsync(ownerB, leaseB, "B_after_miss"));
            AssertExistingPressureInputInert(b.Context, leaseB, filesB, generationB);

            var beforeC = await ReadPressureOwnerSnapshotAsync(ownerC, leaseC, "C_before");
            AssertPressureOrdinaryFrontier(beforeC, c, ownerC, leaseC);
            var warmC = await MeasurePressureOwnerPhaseAsync("ordinary_miss", "C",
                () => ownerC.ReadExistingPressureOrdinaryComparisonAsync(leaseC));
            AssertNoConflictFrameErrors(warmC.Issues);
            Assert.False(warmC.PrefixBlocked);
            Assert.NotNull(warmC.Comparison);
            AssertPressureOrdinaryComparison(warmC.Comparison, c, filesC);
            AssertPressureOrdinaryFrontiersEqual(beforeC,
                await ReadPressureOwnerSnapshotAsync(ownerC, leaseC, "C_after_miss"));
            var countsC = ownerC.ReadExistingStrainReuseCounts();
            var conflictPath = AfterlifeSpiritualConflictState.StatePath;
            var conflictKey = filesC.Keys.Single(path => path.Replace('\\', '/') == conflictPath);
            var changedBytes = filesC[conflictKey].Concat(Encoding.UTF8.GetBytes("\n ")).ToArray();
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse(filesC[conflictKey]), JsonNode.Parse(changedBytes)));
            await c.Context.FileSystem.WriteFileAtomicBytesAsync(leaseC, conflictPath, changedBytes);
            var expectedAfterEdit = filesC.ToDictionary(pair => pair.Key,
                pair => pair.Key == conflictKey ? changedBytes : pair.Value, StringComparer.Ordinal);
            var changed = await MeasurePressureOwnerPhaseAsync("ordinary_changed_raw", "C",
                () => ownerC.ReadExistingPressureOrdinaryComparisonAsync(leaseC));
            Assert.Null(changed.Comparison);
            Assert.False(changed.PrefixBlocked);
            var refusal = Assert.Single(changed.Issues);
            Assert.Equal("spiritual_original_input_changed", refusal.Code);
            Assert.Equal(conflictPath, refusal.FilePath);
            Assert.Equal(IssueSeverity.Error, refusal.Severity);
            Assert.False(ownerC.IsCurrentOwner);
            var rejectedCounts = ownerC.ReadExistingStrainReuseCounts();
            Assert.Equal(countsC.OrdinaryEvaluations, rejectedCounts.OrdinaryEvaluations);
            Assert.Equal(countsC.ActualEvaluations, rejectedCounts.ActualEvaluations);
            Assert.True(rejectedCounts.FreshInputChecks > countsC.FreshInputChecks);
            // Revocation intentionally prevents a live post-rejection in-memory snapshot.
            // Zero evaluator entries and exact external witnesses prove only this bounded early-refusal control.
            await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
                await ownerC.ReadExistingPressureOrdinaryProbeStateAsync(leaseC));
            await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
                await ownerC.ReadExistingPressureOrdinaryComparisonAsync(leaseC));
            AssertExistingPressureInputInert(c.Context, leaseC, expectedAfterEdit, generationC);
            foreach (var (copy, lease) in new[] { (a, leaseA), (b, leaseB), (c, leaseC) })
                foreach (var path in new[] { SpiritualWoundCaptureCheckpointState.StatePath,
                             SpiritualWoundDecisionPendingState.StatePath, AcceptedMechanicsPlan.WoundCommandPath })
                    Assert.Null(await copy.Context.FileSystem.ReadFileBytesAsync(lease, path));
        }
        finally
        {
            var cleanupFailures = new List<string>();
            foreach (var context in contexts.AsEnumerable().Reverse())
            {
                var cleanupStarted = Stopwatch.GetTimestamp();
                try { await context.DisposeAsync(); }
                catch (Exception error) { cleanupFailures.Add(context.RootPath + ": " + error.Message); }
                finally { WritePressureOwnerTiming("owned_cleanup", context.RootPath, cleanupStarted); }
                if (Directory.Exists(context.RootPath)) cleanupFailures.Add(context.RootPath + ": root remains");
            }
            Assert.Empty(cleanupFailures);
        }
    }

    /// <summary>Captures one genuine signed44 tree and returns only after its original owners and root are gone.</summary>
    /// <returns>Private immutable prepared bytes and the original signer/generation facts, never a live owner.</returns>
    private async Task<PressureOrdinaryOwnerTemplate> PreparePressureOrdinaryOwnerTemplateAsync()
    {
        PressureOrdinaryOwnerTemplate? prepared = null;
        var preparationStarted = Stopwatch.GetTimestamp();
        var cleanupStarted = 0L;
        await WithSignedPressureParentBelowCapAsync([4, 12], "request_pressure_ordinary_owner_44", async context =>
        {
            await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
            var signed = PendingTurnSnapshotReader.ReadCurrent(context.FileSystem, lease, PressureOrdinarySignedPaths);
            Assert.True(signed.Success, string.Join("\n", signed.Issues));
            var authority = Assert.IsType<PendingTurnSnapshotReadAuthority>(signed.Snapshot);
            var captureStarted = Stopwatch.GetTimestamp();
            prepared = new(PreparedFixtureTree.Capture(context.FileSystem.GameSessionPath), context.RootPath,
                Assert.IsType<string>(context.FileSystem.ReadExistingSessionGeneration(lease)), authority.SnapshotToken);
            WritePressureOwnerTiming("immutable_capture", "original", captureStarted);
            cleanupStarted = Stopwatch.GetTimestamp();
        });
        WritePressureOwnerTiming("original_owned_cleanup", "original", cleanupStarted);
        WritePressureOwnerTiming("genuine_preparation", "original", preparationStarted);
        Assert.NotNull(prepared);
        Assert.False(Directory.Exists(prepared.OriginalRoot));
        return prepared;
    }

    /// <summary>Restores only game-session bytes into fresh owners, authenticates signed DATA and authors this copy's own ordinary draft.</summary>
    /// <param name="prepared">Immutable genuine template whose source root has already been disposed.</param>
    /// <param name="label">Independent copy name for phase timings.</param>
    /// <param name="hooks">Optional dependencies on this copy's fresh filesystem; null preserves the ordinary hookless fixture.</param>
    /// <param name="beforeOrdinaryAuthorAsync">Optional observation after genuine signer checks and before this copy authors its ordinary draft.</param>
    /// <returns>One owned mutable copy with its fresh generation and independently authored ordinary exchange.</returns>
    private async Task<PressureOrdinaryOwnerCopy> MaterializePressureOrdinaryOwnerCopyAsync(
        PressureOrdinaryOwnerTemplate prepared, string label, FileSystemManagerHooks? hooks = null,
        Func<ResourceMaterializationTestContext, Task>? beforeOrdinaryAuthorAsync = null)
    {
        var started = Stopwatch.GetTimestamp();
        var context = await ResourceMaterializationTestContext.CreateAsync(hooks);
        try
        {
            var freshGenerationBytes = File.ReadAllBytes(context.FileSystem.SessionGenerationPath);
            prepared.Tree.Materialize(context.FileSystem.GameSessionPath);
            Assert.Equal(freshGenerationBytes, File.ReadAllBytes(context.FileSystem.SessionGenerationPath));
            Assert.False(Directory.Exists(Path.Combine(context.FileSystem.GameSessionPath, ".boe_runtime")));
            WritePressureOwnerTiming("materialization", label, started);
            string generation;
            var signedStarted = Stopwatch.GetTimestamp();
            await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
            {
                generation = Assert.IsType<string>(context.FileSystem.ReadExistingSessionGeneration(lease));
                Assert.NotEqual(prepared.Generation, generation);
                var signed = PendingTurnSnapshotReader.ReadCurrent(context.FileSystem, lease, PressureOrdinarySignedPaths);
                Assert.True(signed.Success, string.Join("\n", signed.Issues));
                var authority = Assert.IsType<PendingTurnSnapshotReadAuthority>(signed.Snapshot);
                Assert.Equal(prepared.SnapshotToken, authority.SnapshotToken);
                Assert.Equal((44, "request_pressure_ordinary_owner_44"), (authority.TurnNumber, authority.RequestId));
                Assert.Equal(new[] { 4, 12 }, authority.AcceptedD20EventValues);
                foreach (var path in new[] { LiveTurnPreparationService.PendingTurnSnapshotManifestPath,
                             PendingTurnSnapshotAuthority.AuthorityPath, LiveTurnPreparationService.TurnRequestPath })
                    Assert.Equal(prepared.Tree.ReadFileBytes(path), await context.FileSystem.ReadFileBytesAsync(lease, path));
                var manifest = JsonNode.Parse(prepared.Tree.ReadFileBytes(LiveTurnPreparationService.PendingTurnSnapshotManifestPath))!;
                foreach (var pair in manifest["files"]!.AsObject())
                {
                    var path = pair.Value!.GetValue<string>();
                    Assert.Equal(prepared.Tree.ReadFileBytes(path), await context.FileSystem.ReadFileBytesAsync(lease, path));
                }
                foreach (var path in PressureOrdinarySignedPaths)
                    Assert.Equal(prepared.Tree.ReadFileBytes(path), authority.ReadRequiredBytes(path));
            }
            WritePressureOwnerTiming("signed_read", label, signedStarted);
            if (beforeOrdinaryAuthorAsync is not null)
                await beforeOrdinaryAuthorAsync(context);
            var draftStarted = Stopwatch.GetTimestamp();
            var conflict = (await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath))!.AsObject();
            var ordinary = await AppendExistingPressureOrdinaryDraftAsync(context, conflict);
            var definitions = (await context.ReadJsonAsync(ResourceMaterializationContract.DefinitionsPath))!.ToJsonString();
            WritePressureOwnerTiming("ordinary_draft", label, draftStarted);
            return new(context, generation, ordinary, conflict, definitions);
        }
        catch
        {
            var cleanupStarted = Stopwatch.GetTimestamp();
            await context.DisposeAsync();
            WritePressureOwnerTiming("failed_materialization_cleanup", label, cleanupStarted);
            Assert.False(Directory.Exists(context.RootPath));
            throw;
        }
    }

    /// <summary>Uses real intake, resource owner creation and explicit genuine prefix closure before comparison snapshots.</summary>
    /// <param name="copy">Independent authored ordinary copy.</param>
    /// <param name="lease">This copy's actual active canonical lease.</param>
    /// <param name="label">Independent copy name for phase timings.</param>
    /// <param name="validator">Explicit fresh validator for retry intake; null preserves the existing context-validator path.</param>
    /// <returns>The actual capture; its caller owns disposal.</returns>
    private async Task<ValidationService.SpiritualOriginalTurnCapture> BeginPressureOrdinaryOwnerAsync(
        PressureOrdinaryOwnerCopy copy, FileSystemManager.CanonicalWriteLease lease, string label,
        ValidationService? validator = null)
    {
        var recorded = await MeasurePressureOwnerPhaseAsync("intake", label,
            () => (validator ?? copy.Context.Validator).CaptureSpiritualOriginalTurnWithIntakeAsync(lease));
        AssertNoConflictFrameErrors(recorded.Issues);
        var capture = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(recorded.Capture);
        try
        {
            AssertNoConflictFrameErrors(await MeasurePressureOwnerPhaseAsync("resource_begin", label,
                () => capture.BeginResourceExecutionAsync(lease)));
            var prefix = await MeasurePressureOwnerPhaseAsync("original_prefix", label,
                () => capture.PrepareOriginalResourcePrefixAsync(lease));
            AssertNoConflictFrameErrors(prefix.Issues);
            Assert.True(prefix.Ready);
            Assert.False(prefix.PrefixBlocked);
            return capture;
        }
        catch { capture.Dispose(); throw; }
    }

    /// <summary>Checks complete detached ordinary DATA, independently calculated harm and the exact unpaid expected transitions.</summary>
    /// <param name="comparison">Detached DATA produced by the real current owner's independent comparison.</param>
    /// <param name="copy">Independent ordinary draft and genuine signed basis.</param>
    /// <param name="physicalFiles">Exact raw file witnesses from this copy before the comparison.</param>
    private static void AssertPressureOrdinaryComparison(ValidationService.SpiritualOrdinaryPressureComparisonData comparison,
        PressureOrdinaryOwnerCopy copy, IReadOnlyDictionary<string, byte[]> physicalFiles)
    {
        Assert.Equal("/activeConflict/exchangeLog/2", comparison.ExchangePointer);
        var rawConflict = physicalFiles.Single(pair => pair.Key.Replace('\\', '/') ==
            AfterlifeSpiritualConflictState.StatePath).Value;
        Assert.Equal(new CanonicalBeforeImage(true, rawConflict).Fingerprint,
            comparison.RawInputFingerprint);
        Assert.True(JsonNode.DeepEquals(copy.Ordinary, JsonNode.Parse(comparison.OrdinaryExchangeJson)));
        Assert.True(JsonNode.DeepEquals(copy.Ordinary, comparison.ReadOrdinaryExchange()));
        var expected = SpiritualWoundOpportunityMath.Calculate(new(8, 0, 0, "strained", "fractured",
            copy.Conflict["activeConflict"]!["dangerMode"]!.GetValue<string>(), 4));
        Assert.NotNull(expected);
        Assert.Equal((10L, 1), (expected.TraumaPressure, expected.MaximumSeverityRank));
        Assert.Equal(expected, comparison.OrdinaryCalculation);
        Assert.Equal(2, comparison.ExpectedTransitions.Count);
        var activeConflict = copy.Conflict["activeConflict"]!;
        Assert.True(AfterlifeEntityProfileState.TryNormalizeEffectRealm(
            activeConflict["realm"]!.GetValue<string>(), out var realm));
        var player = activeConflict["playerSide"]!["leadContestant"]!;
        Assert.Equal("player", player["actorType"]!.GetValue<string>());
        Assert.Equal("player_soul:player_soul", ValidationService.SpiritualWoundSourceSession.ActorKey(player.AsObject()));
        var expectedCoordinates = new[]
        {
            new ResourceCoordinate(realm, ResourceOwnerKind.AfterlifeActor,
                player["actorId"]!.GetValue<string>(), "spiritual_action_points"),
            new ResourceCoordinate(realm, ResourceOwnerKind.AfterlifeConflictSide,
                activeConflict[AfterlifeEntityProfileState.ResourceOwnerBindingsProperty]!["opposition"]!["resourceOwnerId"]!
                    .GetValue<string>(), "spiritual_action_points")
        };
        foreach (var coordinate in expectedCoordinates)
        {
            var transition = Assert.Single(comparison.ExpectedTransitions,
                row => row.Coordinate.OwnerKind == coordinate.OwnerKind);
            Assert.Equal(coordinate, transition.Coordinate);
            Assert.Equal(ResourceTransitionOperation.Spend, transition.Operation);
            Assert.Equal((3m, 0m, 3m, 0m), (transition.Before, transition.After, transition.Amount, transition.RecoveryPayment));
            Assert.Equal("exchange_existing_pressure_44", transition.SourceId);
        }
    }

    /// <summary>Checks the closed-prefix, unstaged, ordinal-zero comparison frontier without acquiring or evaluating any proof.</summary>
    /// <param name="state">Immutable snapshot of this bounded comparison frontier after genuine prefix closure.</param>
    /// <param name="copy">Independent genuine copy supplying unchanged resource definitions and conflict bindings.</param>
    /// <param name="capture">Actual current capture used to cross-check existing allocation and exchange DATA accessors.</param>
    /// <param name="lease">This copy's actual active canonical lease.</param>
    private static void AssertPressureOrdinaryFrontier(ValidationService.SpiritualOrdinaryPressureProbeState state,
        PressureOrdinaryOwnerCopy copy, ValidationService.SpiritualOriginalTurnCapture capture,
        FileSystemManager.CanonicalWriteLease lease)
    {
        var source = JsonNode.Parse(state.SourceStateJson)!.AsObject();
        Assert.True(source["revision"]!.GetValue<long>() >= 0);
        foreach (var key in new[] { "sources", "checkedExchanges", "claimedDice", "pendingRequirements" })
            Assert.Empty(source[key]!.AsArray());
        Assert.IsType<JsonObject>(source["frontiers"]);
        Assert.IsType<JsonObject>(source["checkedByCoordinate"]);
        Assert.Equal(0, state.NextOrdinal);
        Assert.Empty(capture.ReadClosedExchangeEvidence(lease));
        Assert.Equal(capture.ReadAllocationCursor(lease), state.AllocationCursor);
        Assert.True(JsonNode.DeepEquals(capture.ReadAllocationJournal(lease), JsonNode.Parse(state.AllocationJournalJson)));
        var resources = AfterlifeConflictActionPointProjectionService.Resolve(copy.ResourceDefinitionsJson,
            state.ResourceStateJson, copy.Conflict["activeConflict"]!.AsObject());
        AssertNoConflictFrameErrors(resources.Issues);
        Assert.Equal((3m, 10m, 3m, 9m), (resources.Projection!.Player.Current, resources.Projection.Player.Maximum,
            resources.Projection.Opposition.Current, resources.Projection.Opposition.Maximum));
    }

    /// <summary>Compares every closed-prefix, unstaged, ordinal-zero comparison image, never an opaque hash or collection reference identity.</summary>
    /// <param name="before">Independent DATA observed before comparison.</param>
    /// <param name="after">Independent DATA observed after comparison and probe disposal.</param>
    private static void AssertPressureOrdinaryFrontiersEqual(ValidationService.SpiritualOrdinaryPressureProbeState before,
        ValidationService.SpiritualOrdinaryPressureProbeState after)
    {
        Assert.Equal(before.SourceStateJson, after.SourceStateJson);
        Assert.Equal(before.ResourceStateJson, after.ResourceStateJson);
        Assert.Equal(before.ResourceHistoryJson, after.ResourceHistoryJson);
        Assert.Equal(before.ResourceTranscriptJson, after.ResourceTranscriptJson);
        Assert.Equal(before.EffectPrefixJson, after.EffectPrefixJson);
        Assert.Equal(before.NextOrdinal, after.NextOrdinal);
        Assert.Equal(before.AllocationCursor, after.AllocationCursor);
        Assert.Equal(before.AllocationJournalJson, after.AllocationJournalJson);
    }

    /// <summary>Times explicit frontier observation and requires it to leave all evaluator and freshness counters untouched.</summary>
    /// <param name="capture">Actual current capture owning the observation.</param>
    /// <param name="lease">This capture's actual active canonical lease.</param>
    /// <param name="label">Independent copy and observation name for diagnostics.</param>
    /// <returns>Detached closed-prefix, unstaged, ordinal-zero comparison DATA without proof evaluation or cache initialization.</returns>
    private async Task<ValidationService.SpiritualOrdinaryPressureProbeState> ReadPressureOwnerSnapshotAsync(
        ValidationService.SpiritualOriginalTurnCapture capture, FileSystemManager.CanonicalWriteLease lease, string label)
    {
        var counts = capture.ReadExistingStrainReuseCounts();
        var state = await MeasurePressureOwnerPhaseAsync("snapshot", label,
            () => capture.ReadExistingPressureOrdinaryProbeStateAsync(lease));
        Assert.Equal(counts, capture.ReadExistingStrainReuseCounts());
        return state;
    }

    /// <summary>Measures a named operation without interpreting nested intervals as additive costs.</summary>
    /// <typeparam name="T">Actual operation result type, retained without reinterpretation.</typeparam>
    /// <param name="phase">Named operation whose elapsed time is measured.</param>
    /// <param name="label">Independent copy or original preparation name.</param>
    /// <param name="operation">The real test operation; this helper does not supply cached work or proof.</param>
    /// <returns>The actual operation result.</returns>
    private async Task<T> MeasurePressureOwnerPhaseAsync<T>(string phase, string label, Func<Task<T>> operation)
    {
        var started = Stopwatch.GetTimestamp();
        try { return await operation(); }
        finally { WritePressureOwnerTiming(phase, label, started); }
    }

    /// <summary>Emits uncapped local phase diagnostics through the existing test output.</summary>
    /// <param name="phase">Named preparation, comparison, observation or cleanup phase.</param>
    /// <param name="label">Independent copy or original preparation name.</param>
    /// <param name="started">Monotonic timestamp captured immediately before the measured phase.</param>
    private void WritePressureOwnerTiming(string phase, string label, long started) =>
        _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase = "pressure_ordinary_owner_" + phase, label,
            elapsedMilliseconds = Stopwatch.GetElapsedTime(started).TotalMilliseconds }));

    /// <summary>Immutable genuine tree and source facts, with no retained lease, filesystem or capture.</summary>
    /// <param name="Tree">Only detached game-session bytes, never runtime generation or lock files.</param>
    /// <param name="OriginalRoot">Disposed original root, used only to assert owned cleanup.</param>
    /// <param name="Generation">Original runtime generation, used only to prove copies differ.</param>
    /// <param name="SnapshotToken">Exact token validated by the original real signed reader.</param>
    private sealed record PressureOrdinaryOwnerTemplate(PreparedFixtureTree Tree, string OriginalRoot,
        string Generation, string SnapshotToken);

    /// <summary>One independently mutable test copy and the ordinary DATA authored only in that copy.</summary>
    /// <param name="Context">Actual isolated mutable fixture context, owned by this Fact.</param>
    /// <param name="Generation">This context's freshly initialized runtime generation.</param>
    /// <param name="Ordinary">Complete exchange authored independently in this copy.</param>
    /// <param name="Conflict">Complete physical ordinary conflict DATA with genuine signed parent frontier.</param>
    /// <param name="ResourceDefinitionsJson">Unchanged genuine definitions for independent resource snapshot assertions.</param>
    private sealed record PressureOrdinaryOwnerCopy(ResourceMaterializationTestContext Context, string Generation,
        JsonObject Ordinary, JsonObject Conflict, string ResourceDefinitionsJson);
}
