using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>Rejects drift before take without transaction writes or restoring the authored ordinary draft.</summary>
    [Fact]
    public async Task SpiritualExistingPressure_PreTakeRefusalPreservesAuthoredDraft()
    {
        var armed = false;
        var writes = new List<string>();
        var hooks = new FileSystemManagerHooks
        {
            BeforeCanonicalMutationAsync = path =>
            {
                if (armed) writes.Add(path);
                return Task.CompletedTask;
            }
        };
        await WithPressureNegativeCopyAsync("pre_take_drift", async copy =>
        {
            var context = copy.Context;
            await CompletePressureNegativeColdC3Async(copy, context.Validator, "pre_take_drift");
            AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawResourceMaterializationAsync());
            ValidationService.SpiritualOriginalTurnCapture.SpiritualC4PublicationAuthority oldAuthority;
            var backups = await ReadSpiritualC4BackupsAsync(context);
            IReadOnlyDictionary<string, byte[]> edited;
            byte[] generation;
            await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
            {
                Assert.True(AcceptedMechanicsPlanAuthority.TryPeekSpiritualPublication(context.FileSystem, lease, out oldAuthority));
                AssertNoConflictFrameErrors(await oldAuthority.ValidateCurrentInputsAsync(context.FileSystem, lease));
                var before = CaptureExactSpiritualFiles(context.FileSystem.GameSessionPath);
                generation = File.ReadAllBytes(context.FileSystem.SessionGenerationPath);
                await AppendPressureRawWhitespaceAsync(context, lease);
                edited = CaptureExactSpiritualFiles(context.FileSystem.GameSessionPath);
                Assert.Equal(before.Keys.OrderBy(path => path, StringComparer.Ordinal),
                    edited.Keys.OrderBy(path => path, StringComparer.Ordinal));
                foreach (var (path, bytes) in before.Where(pair => pair.Key.Replace('\\', '/') != AfterlifeSpiritualConflictState.StatePath))
                    Assert.Equal(bytes, edited[path]);
            }
            var started = Stopwatch.GetTimestamp();
            armed = true;
            try
            {
                var error = await Assert.ThrowsAsync<InvalidDataException>(() =>
                    AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(
                        context.FileSystem, context.Normalizer, context.Validator, backups));
                Assert.Contains("spiritual_c4_publication_input_changed", error.Message);
                Assert.Contains(AfterlifeSpiritualConflictState.StatePath, error.Message);
            }
            finally { armed = false; }
            Assert.Empty(writes);
            await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
            {
                AssertExistingPressureInputInert(context, lease, edited, generation);
                Assert.False(AcceptedMechanicsPlanAuthority.TryPeekSpiritualPublication(context.FileSystem, lease, out _));
                Assert.False(AcceptedMechanicsPlanAuthority.TryTakeSpiritualPublication(context.FileSystem, lease, oldAuthority, out _));
                AssertExistingPressureInputInert(context, lease, edited, generation);
            }
            WritePressureOwnerTiming("actual_pre_take_drift_refusal", "negative", started);
        }, hooks);
    }

    /// <summary>Restores independently witnessed signed44 originals after a real late cut, then retries from reauthored DATA and fresh owners.</summary>
    [Fact]
    public async Task SpiritualExistingPressure_LatePublicationRollbackRestoresSignedOriginAndFreshRetryPublishesOnce()
    {
        var armed = false;
        var cutEnabled = true;
        var cuts = 0;
        CurrentCommittedPublicationWitness? witness = null;
        PressureSignedRollbackOrigin? signedOrigin = null;
        var injected = new IOException("Existing pressure C4 receipt read-back failure.");
        var hooks = new FileSystemManagerHooks
        {
            LocalPublicationObserver = (phase, index) => { if (armed) witness!.Observe(phase, index); },
            BeforeCanonicalReadOpenAsync = path =>
            {
                if (armed && cutEnabled && path == SpiritualWoundOpportunityReceiptState.StatePath &&
                    witness!.Has(ResourceMaterializationContract.StatePath) &&
                    witness.Has(SpiritualWoundOpportunityReceiptState.StatePath))
                {
                    witness.RequireSingleCurrent(ResourceMaterializationContract.StatePath);
                    witness.RequireSingleCurrent(SpiritualWoundOpportunityReceiptState.StatePath);
                    cuts++;
                    cutEnabled = false;
                    armed = false;
                    throw injected;
                }
                return Task.CompletedTask;
            }
        };
        await WithPressureNegativeCopyAsync("late_cut_and_retry", async copy =>
        {
            var context = copy.Context;
            var origin = Assert.IsType<PressureSignedRollbackOrigin>(signedOrigin);
            var authored = new Dictionary<string, byte[]?>(StringComparer.Ordinal);
            foreach (var path in PressureAuthoredDraftPaths)
                authored[path] = await context.FileSystem.ReadFileBytesAsync(path);
            Assert.NotEqual(origin.Files[AfterlifeSpiritualConflictState.StatePath.Replace('/', Path.DirectorySeparatorChar)],
                authored[AfterlifeSpiritualConflictState.StatePath]);
            Assert.Equal("fractured", copy.Conflict["activeConflict"]!["playerSideStrain"]!.GetValue<string>());
            var firstCompletion = await CompletePressureNegativeColdC3Async(copy, context.Validator, "late_cut_first_attempt");
            AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawResourceMaterializationAsync());
            ValidationService.SpiritualOriginalTurnCapture.SpiritualC4PublicationAuthority oldAuthority;
            await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
            {
                Assert.True(AcceptedMechanicsPlanAuthority.TryPeekSpiritualPublication(context.FileSystem, lease, out oldAuthority));
                AssertNoConflictFrameErrors(await oldAuthority.ValidateCurrentInputsAsync(context.FileSystem, lease));
            }
            var backups = await ReadSpiritualC4BackupsAsync(context);
            witness = new(context.FileSystem, AfterlifeSpiritualConflictState.StatePath,
                ResourceMaterializationContract.StatePath, SpiritualWoundOpportunityReceiptState.StatePath);
            var failedWitness = witness;
            var failureStarted = Stopwatch.GetTimestamp();
            armed = true;
            Exception? failure;
            try
            {
                failure = await Record.ExceptionAsync(() => AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(
                    context.FileSystem, context.Normalizer, context.Validator, backups));
            }
            finally { armed = false; }
            Assert.Equal(1, cuts);
            Assert.Same(injected, failure);
            Assert.True(failedWitness.Has(AfterlifeSpiritualConflictState.StatePath));
            Assert.True(failedWitness.Has(ResourceMaterializationContract.StatePath));
            Assert.True(failedWitness.Has(SpiritualWoundOpportunityReceiptState.StatePath));
            failedWitness.AssertRestored("existing-pressure-signed44-late-cut",
                origin.Tracked.ToDictionary(pair => context.FileSystem.ResolvePath(pair.Key), pair => pair.Value,
                    StringComparer.Ordinal), failure, _storageOutput.WriteLine);
            AssertPressureSignedRollbackOrigin(context, origin);
            await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
            {
                Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
                Assert.False(AcceptedMechanicsPlanAuthority.TryPeekSpiritualPublication(context.FileSystem, lease, out _));
                Assert.False(AcceptedMechanicsPlanAuthority.TryTakeSpiritualPublication(context.FileSystem, lease, oldAuthority, out _));
                AssertPressureNoPrivatePair(context, lease);
            }
            AssertPressureSignedRollbackOrigin(context, origin);
            WritePressureOwnerTiming("actual_late_cut_signed44_rollback", "negative", failureStarted);

            // The failed C2 pair, commands, owners and authority are never restored or reused.
            // Reauthor only the exact retained ordinary conflict/output DATA after proving signed rollback.
            await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
            {
                foreach (var (path, bytes) in authored)
                {
                    Assert.Contains(path, PressureAuthoredDraftPaths);
                    if (bytes is null)
                        Assert.Null(await context.FileSystem.ReadFileBytesAsync(lease, path));
                    else
                        await context.FileSystem.WriteFileAtomicBytesAsync(lease, path, bytes);
                }
                foreach (var (path, bytes) in authored)
                    Assert.Equal(bytes, await context.FileSystem.ReadFileBytesAsync(lease, path));
                AssertPressureNoPrivatePair(context, lease);
                Assert.False(AcceptedMechanicsPlanAuthority.TryTakeSpiritualPublication(context.FileSystem, lease, oldAuthority, out _));
            }
            AssertPressureOnlyReauthoredInputs(context, origin, authored);
            var retryStarted = Stopwatch.GetTimestamp();
            var retryValidator = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);
            AssertNoConflictFrameErrors(await retryValidator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync());
            AssertNoConflictFrameErrors(await retryValidator.ValidateAcceptedTurnRawMortalItemMaterializationAsync());
            var retryCompletion = await CompletePressureNegativeColdC3Async(copy, retryValidator, "late_cut_fresh_retry");
            // Source coordinates/meaning must agree; allocation clocks and identities are intentionally allowed to be fresh.
            Assert.Equal(firstCompletion.ActualRootJson, retryCompletion.ActualRootJson);
            AssertNoConflictFrameErrors(await retryValidator.ValidateAcceptedTurnRawResourceMaterializationAsync());
            var plan = await PeekPlanAsync(context);
            AssertPressureExistingResourcePlan(plan, JsonNode.Parse(retryCompletion.ActualRootJson)!.AsObject());
            ValidationService.SpiritualOriginalTurnCapture.SpiritualC4PublicationAuthority retryAuthority;
            await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
            {
                Assert.True(AcceptedMechanicsPlanAuthority.TryPeekSpiritualPublication(context.FileSystem, lease, out retryAuthority));
                Assert.NotSame(oldAuthority, retryAuthority);
                Assert.False(AcceptedMechanicsPlanAuthority.TryTakeSpiritualPublication(context.FileSystem, lease, oldAuthority, out _));
            }
            witness = new(context.FileSystem, AfterlifeSpiritualConflictState.StatePath,
                ResourceMaterializationContract.StatePath, SpiritualWoundOpportunityReceiptState.StatePath);
            armed = true;
            try
            {
                var published = await AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(
                    context.FileSystem, context.Normalizer, retryValidator, await ReadSpiritualC4BackupsAsync(context));
                AssertNoConflictFrameErrors(published.Issues);
                Assert.Same(plan, published.MechanicsPlan);
            }
            finally { armed = false; }
            Assert.Equal(1, cuts);
            witness.RequireSingleCurrent(AfterlifeSpiritualConflictState.StatePath);
            witness.RequireSingleCurrent(ResourceMaterializationContract.StatePath);
            witness.RequireSingleCurrent(SpiritualWoundOpportunityReceiptState.StatePath);
            await AssertPressureFreshRetryPublishedAsync(context, origin, retryCompletion, plan);
            await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
            {
                Assert.False(AcceptedMechanicsPlanAuthority.TryTakeSpiritualPublication(context.FileSystem, lease, oldAuthority, out _));
                Assert.False(AcceptedMechanicsPlanAuthority.TryTakeSpiritualPublication(context.FileSystem, lease, retryAuthority, out _));
            }
            WritePressureOwnerTiming("actual_fresh_retry_common_c4", "negative", retryStarted);
            await context.CaptureValidatedPendingSnapshotAsync(turn: 45, currentRealm: "Chaos Sea",
                preGeneratedDices1d20: [15, 5], requestId: "request_pressure_rollback_retry_read_45");
            var manifest45 = (await context.ReadJsonAsync(LiveTurnPreparationService.PendingTurnSnapshotManifestPath))!;
            Assert.Equal(45, manifest45["turnNumber"]!.GetValue<int>());
            foreach (var path in PressureOrdinarySignedPaths)
                Assert.Equal(await context.FileSystem.ReadFileBytesAsync(path),
                    await context.FileSystem.ReadFileBytesAsync(manifest45["files"]![path]!.GetValue<string>()));
            AssertNoConflictFrameErrors(await retryValidator.ValidateGameStateAsync(
                new GameStateValidationSelection(GameStateValidationPhase.AfterlifeSpiritualConflictState)));
        }, hooks, beforeOrdinaryAuthorAsync: async context =>
        {
            var started = Stopwatch.GetTimestamp();
            signedOrigin = await ReadPressureSignedRollbackOriginAsync(context);
            WritePressureOwnerTiming("independent_signed44_rollback_capture", "negative", started);
        });
    }

    private static readonly string[] PressureAuthoredDraftPaths =
        [AfterlifeSpiritualConflictState.StatePath, ProjectionNarrativePath, ProjectionInterfacePath];

    /// <summary>Contains independently captured signed-origin DATA only, before any ordinary exchange is authored.</summary>
    private sealed record PressureSignedRollbackOrigin(IReadOnlyDictionary<string, byte[]> Files,
        IReadOnlyDictionary<string, byte[]?> Tracked, byte[] Generation, string ParentJson,
        string ReceiptJson, string EffectIdentityJson);

    /// <summary>Contains completed detached DATA, never a capture, private session, interval, ticket or publication authority.</summary>
    private sealed record PressureNegativeCompletedData(string ActualRootJson, string WitnessJson,
        string ReceiptJson, AcceptedMechanicsResourcePlanningResult Resources);

    /// <summary>Completes one genuine actual/C2/cold-none/C3 workflow with all live owners confined to this call.</summary>
    private async Task<PressureNegativeCompletedData> CompletePressureNegativeColdC3Async(
        PressureOrdinaryOwnerCopy copy, ValidationService validator, string label)
    {
        var context = copy.Context;
        AssertNoConflictFrameErrors(await validator.ValidateGameStateAsync(
            new GameStateValidationSelection(GameStateValidationPhase.AfterlifeSpiritualConflictState)));
        var original = (await context.FileSystem.ReadFileBytesAsync(AfterlifeSpiritualConflictState.StatePath))!;
        var actualRoot = copy.Conflict.DeepClone().AsObject();
        actualRoot["activeConflict"]!["playerSideStrain"] = "overwhelmed";
        actualRoot["activeConflict"]!["exchangeLog"]![2]!["after"]!["playerSideStrain"] = "overwhelmed";
        var files = CaptureExactSpiritualFiles(context.FileSystem.GameSessionPath);
        var generation = File.ReadAllBytes(context.FileSystem.SessionGenerationPath);
        var started = Stopwatch.GetTimestamp();
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var recorded = await validator.CaptureSpiritualOriginalTurnWithIntakeAsync(lease);
        AssertNoConflictFrameErrors(recorded.Issues);
        using var warm = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(recorded.Capture);
        AssertNoConflictFrameErrors(await warm.BeginResourceExecutionAsync(lease));
        var advanced = await warm.AdvanceNextResourceExchangeAsync(lease);
        AssertNoConflictFrameErrors(advanced.Issues);
        var interval = Assert.IsType<AcceptedMechanicsPlanner.SpiritualExchangeInterval>(advanced.Step?.Interval);
        var source = AssertPressureNegativeActualSource(copy, warm, lease);
        var counts = warm.ReadExistingStrainReuseCounts();
        Assert.True(counts.OrdinaryEvaluations >= 1);
        Assert.Equal(1, counts.ActualEvaluations);
        Assert.True(counts.FreshInputChecks > 0);
        Assert.True(counts.FreshDependencyChecks > 0);
        var prefix = warm.ReadClosedResourceCandidatePrefix(lease, interval);
        var candidate = await warm.ReadC1CandidateOwnerImagesAsync(lease, interval);
        Assert.True(JsonNode.DeepEquals(actualRoot, prefix.CompanionAfterImages[AfterlifeSpiritualConflictState.StatePath]));
        Assert.True(JsonNode.DeepEquals(actualRoot, JsonNode.Parse(candidate[AfterlifeSpiritualConflictState.StatePath].Bytes!)));
        AssertExistingPressureInputInert(context, lease, files, generation);
        var committed = await warm.CommitC2FirstTransportAsync(lease, interval);
        var expectedCold = ReadExpectedSpiritualColdOfferData(warm, lease, committed, source);
        Assert.Equal(original, Convert.FromBase64String(expectedCold.OriginalConflictBytesBase64));
        var witness = Assert.Single(JsonNode.Parse(expectedCold.ReconstructedPendingJson)!["pending"]!["sources"]!.AsArray())!.AsObject();
        var ordinaryCalculation = SpiritualWoundOpportunityMath.Calculate(source.Calculation.Input with { NewStrain = "fractured" })!;
        var parent = await ReadCombatHealingWoundAsync(context);
        AssertPressureExistingV3Source(witness, JsonNode.Parse(source.ExchangeJson)!.AsObject(), copy.Ordinary,
            ordinaryCalculation, source.Calculation, WoundIdentityState.ComputeSemanticFingerprint(parent),
            expectedCold.OriginalConflictFingerprint);
        warm.Dispose();
        var opened = await new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance)
            .OpenC2PrivateSessionAsync(lease);
        AssertNoConflictFrameErrors(opened.Issues);
        Assert.Equal("offer", opened.Disposition);
        using var choice = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(opened.Session);
        AssertSpiritualColdOfferEvidence(expectedCold, await choice.ReadOfferReplayEvidenceAsync(lease));
        var declined = await choice.SubmitDecisionAsync(lease, JsonSerializer.SerializeToElement(new
            { opportunityRef = choice.Offer!.OpportunityRef, decision = "none" }), null);
        AssertNoConflictFrameErrors(declined.Issues);
        Assert.Equal("completed_unpublished", declined.Disposition);
        using var completed = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(declined.Session);
        var reduced = await completed.ReduceCompletedDeclinesAsync(lease);
        AssertNoConflictFrameErrors(reduced.Issues);
        Assert.True(reduced.Success);
        Assert.True(JsonNode.DeepEquals(actualRoot,
            reduced.Reduction!.OwnerCompanionAfterImages[AfterlifeSpiritualConflictState.StatePath]));
        Assert.Equal(original, await context.FileSystem.ReadFileBytesAsync(lease, AfterlifeSpiritualConflictState.StatePath));
        AssertPressurePrivateTransportOnly(context, files, generation);
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
        WritePressureOwnerTiming("actual_cold_none_c3_negative_preparation", label, started);
        return new(actualRoot.ToJsonString(), witness.ToJsonString(), reduced.ReceiptAfterImage!.ToJsonString(), reduced.Reduction.Resources);
    }

    /// <summary>Captures complete signed44 physical bytes and explicit rollback absences before ordinary authoring.</summary>
    private static async Task<PressureSignedRollbackOrigin> ReadPressureSignedRollbackOriginAsync(
        ResourceMaterializationTestContext context)
    {
        var files = CaptureExactSpiritualFiles(context.FileSystem.GameSessionPath);
        var tracked = new Dictionary<string, byte[]?>(StringComparer.Ordinal);
        foreach (var path in CanonicalStateNormalizer.NormalizerRollbackTrackedFiles.Concat(new[]
                 { SpiritualWoundCaptureCheckpointState.StatePath, SpiritualWoundDecisionPendingState.StatePath,
                     AcceptedMechanicsPlan.WoundCommandPath, SpiritualWoundOpportunityReceiptState.StatePath,
                     ProjectionNarrativePath, ProjectionInterfacePath }).Distinct(StringComparer.Ordinal))
            tracked[path] = await context.FileSystem.ReadFileBytesAsync(path);
        var conflict = (await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath))!["activeConflict"]!;
        Assert.Equal(2, conflict["exchangeLog"]!.AsArray().Count);
        Assert.Equal("strained", conflict["playerSideStrain"]!.GetValue<string>());
        var balance = AfterlifeConflictActionPointProjectionService.Resolve(
            (await context.ReadJsonAsync(ResourceMaterializationContract.DefinitionsPath))!.ToJsonString(),
            (await context.ReadJsonAsync(ResourceMaterializationContract.StatePath))!.ToJsonString(), conflict.AsObject());
        AssertNoConflictFrameErrors(balance.Issues);
        Assert.Equal((3m, 3m), (balance.Projection!.Player.Current, balance.Projection.Opposition.Current));
        foreach (var path in new[] { SpiritualWoundCaptureCheckpointState.StatePath,
                     SpiritualWoundDecisionPendingState.StatePath, AcceptedMechanicsPlan.WoundCommandPath })
            Assert.Null(tracked[path]);
        return new(files, tracked, File.ReadAllBytes(context.FileSystem.SessionGenerationPath),
            WoundMaterializationContract.SerializeCanonical(await ReadCombatHealingWoundAsync(context)),
            (await context.ReadJsonAsync(SpiritualWoundOpportunityReceiptState.StatePath))!.ToJsonString(),
            (await context.ReadJsonAsync(EffectIdentityState.StatePath))!.ToJsonString());
    }

    /// <summary>Requires complete independently witnessed signed rollback, including bytes, absence, output and generation.</summary>
    private static void AssertPressureSignedRollbackOrigin(ResourceMaterializationTestContext context, PressureSignedRollbackOrigin origin)
    {
        var current = CaptureExactSpiritualFiles(context.FileSystem.GameSessionPath);
        Assert.Equal(origin.Files.Keys.OrderBy(path => path, StringComparer.Ordinal),
            current.Keys.OrderBy(path => path, StringComparer.Ordinal));
        foreach (var (path, bytes) in origin.Files)
            Assert.True(bytes.SequenceEqual(current[path]), path);
        foreach (var (path, bytes) in origin.Tracked)
            Assert.Equal(bytes, CleanupPublicationCut.ReadOptional(context.FileSystem.ResolvePath(path)));
        Assert.Equal(origin.Generation, File.ReadAllBytes(context.FileSystem.SessionGenerationPath));
    }

    /// <summary>Checks that explicit retry authoring changes only the retained ordinary/output DATA, never signed controls or snapshots.</summary>
    private static void AssertPressureOnlyReauthoredInputs(ResourceMaterializationTestContext context,
        PressureSignedRollbackOrigin origin, IReadOnlyDictionary<string, byte[]?> authored)
    {
        var expected = origin.Files.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.Ordinal);
        foreach (var (path, bytes) in authored)
        {
            var relative = path.Replace('/', Path.DirectorySeparatorChar);
            if (bytes is null) expected.Remove(relative);
            else expected[relative] = bytes;
        }
        var current = CaptureExactSpiritualFiles(context.FileSystem.GameSessionPath);
        Assert.Equal(expected.Keys.OrderBy(path => path, StringComparer.Ordinal), current.Keys.OrderBy(path => path, StringComparer.Ordinal));
        foreach (var (path, bytes) in expected)
            Assert.True(bytes.SequenceEqual(current[path]), path);
        Assert.Equal(origin.Generation, File.ReadAllBytes(context.FileSystem.SessionGenerationPath));
    }

    /// <summary>Proves only one actual retry append/payment and the exact original parent/History/receipt prefixes.</summary>
    private static async Task AssertPressureFreshRetryPublishedAsync(ResourceMaterializationTestContext context,
        PressureSignedRollbackOrigin origin, PressureNegativeCompletedData completed, AcceptedMechanicsPlan plan)
    {
        foreach (var pair in plan.WoundCarrierAfterImages.Concat(plan.EffectCarrierAfterImages).Concat(plan.OwnerCompanionAfterImages))
            Assert.True(JsonNode.DeepEquals(pair.Value, await context.ReadJsonAsync(pair.Key)), pair.Key);
        Assert.True(JsonNode.DeepEquals(plan.StateAfterImage, await context.ReadJsonAsync(ResourceMaterializationContract.StatePath)));
        Assert.True(JsonNode.DeepEquals(plan.HistoryAfterImage, await context.ReadJsonAsync(ResourceMaterializationContract.HistoryPath)));
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(completed.ActualRootJson), await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath)));
        var parent = await ReadCombatHealingWoundAsync(context);
        Assert.Equal(origin.ParentJson, WoundMaterializationContract.SerializeCanonical(parent));
        foreach (var path in new[] { WoundIdentityState.StatePath, WoundHistoryState.HistoryPath })
            Assert.Equal(origin.Tracked[path], await context.FileSystem.ReadFileBytesAsync(path));
        AssertPressureGuardRecoveryIdentityAppend(JsonNode.Parse(origin.EffectIdentityJson)!.AsObject(),
            (await context.ReadJsonAsync(EffectIdentityState.StatePath))!.AsObject(),
            parent.Consequences.OwnedEffectSources.RootBindings.Select(row => row.EffectId).ToArray(), completed.Resources, plan, turn: 44);
        var originalReceipt = JsonNode.Parse(origin.ReceiptJson)!;
        var receipt = (await context.ReadJsonAsync(SpiritualWoundOpportunityReceiptState.StatePath))!;
        Assert.Equal(2, receipt["sources"]!.AsArray().Count);
        Assert.Equal(2, receipt["decisions"]!.AsArray().Count);
        Assert.True(JsonNode.DeepEquals(originalReceipt["sources"]![0], receipt["sources"]![0]));
        Assert.True(JsonNode.DeepEquals(originalReceipt["decisions"]![0], receipt["decisions"]![0]));
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(completed.WitnessJson), receipt["sources"]![1]!["witness"]));
        Assert.Equal("none", receipt["decisions"]![1]!["decision"]!.GetValue<string>());
        var history = WoundHistoryState.Parse((await context.ReadJsonAsync(WoundHistoryState.HistoryPath))!.ToJsonString(), WoundHistoryState.HistoryPath);
        var ledger = SpiritualWoundOpportunityReceiptState.Parse(receipt.ToJsonString(), SpiritualWoundOpportunityReceiptState.StatePath);
        Assert.True(history.IsValid, string.Join("\n", history.Issues));
        Assert.True(ledger.IsValid, string.Join("\n", ledger.Issues));
        Assert.True(ledger.State!.HasExactStrainHistory(history.State!));
        foreach (var path in new[] { SpiritualWoundCaptureCheckpointState.StatePath,
                     SpiritualWoundDecisionPendingState.StatePath, AcceptedMechanicsPlan.WoundCommandPath })
            Assert.Null(await context.FileSystem.ReadFileBytesAsync(path));
        // The old signed44 corpus/request remain byte-exact until the explicit next-turn signature below.
        foreach (var (path, bytes) in origin.Files.Where(pair => IsSignedInput(pair.Key)))
            Assert.Equal(bytes, CleanupPublicationCut.ReadOptional(context.FileSystem.ResolvePath(path)));

        static bool IsSignedInput(string path)
        {
            path = path.Replace('\\', '/');
            return path == LiveTurnPreparationService.PendingTurnSnapshotManifestPath ||
                path == PendingTurnSnapshotAuthority.AuthorityPath || path == LiveTurnPreparationService.TurnRequestPath ||
                path.StartsWith(LiveTurnPreparationService.PendingTurnSnapshotDirectory + "/", StringComparison.Ordinal);
        }
    }
}
