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
    /// <summary>Catches lost v4 zero comparison in genuine source-only C3 after successful warm/cold actual owners, without publication.</summary>
    [Fact]
    public Task SpiritualGroupZero_SourceOnlyC3RetainsComparison()
    {
        var spec = GroupZeroCase("group_zero_source_only_c3", GroupZeroPressureContour.Low);
        return WithPairLowFailureAsync(spec.Name, () => WithPressureGroupPublicationAsync(spec, async setup =>
        {
            _ = await CompleteGroupZeroSourceOnlyC3Async(setup);
        }));
    }

    /// <summary>Confines one genuine group-zero capture and lease, preserving the first failure through every owned cleanup stage.</summary>
    /// <param name="setup">Independent signed origin, unchanged authored ordinary bytes and complete retained parents.</param>
    /// <param name="validator">Fresh validator belonging only to this warm or cold phase.</param>
    /// <param name="phase">Named warm/cold measurement section.</param>
    /// <param name="work">Bounded work after the same accepted actual/C1/no-offer proof as the core owners.</param>
    /// <param name="expectedPublication">Only an authority explicitly registered by this test's work may survive capture disposal.</param>
    /// <param name="retainDisposed">Optional test-only negative witness, invoked only after capture and lease disposal.</param>
    private async Task WithGroupZeroCaptureAsync(PressureGroupPublicationSetup setup, ValidationService validator,
        string phase, Func<PressureGroupWindow, AcceptedMechanicsPlanner.SpiritualExchangeInterval, Task> work,
        Func<ValidationService.SpiritualOriginalTurnCapture.SpiritualC4PublicationAuthority?>? expectedPublication = null,
        Action<ValidationService.SpiritualOriginalTurnCapture>? retainDisposed = null)
    {
        var context = setup.Copy.Context;
        FileSystemManager.CanonicalWriteLease? lease = null;
        ValidationService.SpiritualOriginalTurnCapture? capture = null;
        PressureGroupWindow? window = null;
        IReadOnlyDictionary<string, byte[]>? disposalFiles = null;
        byte[]? generation = null;
        var started = Stopwatch.GetTimestamp();
        await ExecutePressureGroupFixtureStagesAsync(new(
            Work: async () =>
            {
                lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
                foreach (var pair in setup.Authored)
                    Assert.Equal(pair.Value, await context.FileSystem.ReadFileBytesAsync(lease, pair.Key));
                capture = await BeginPressureOrdinaryOwnerAsync(setup.Copy, lease, setup.Spec.Name + "_" + phase, validator);
                var initial = capture.ReadExistingStrainReuseCounts();
                Assert.Equal((0, 0, 0), (initial.SignedParentReads, initial.OrdinaryEvaluations, initial.ActualEvaluations));
                var files = CaptureExactSpiritualFiles(context.FileSystem.GameSessionPath);
                generation = File.ReadAllBytes(context.FileSystem.SessionGenerationPath);
                var frontier = await ReadPressureOwnerSnapshotAsync(capture, lease, setup.Spec.Name + "_" + phase);
                window = new(setup.Spec, setup.Copy, lease, capture, files, generation, frontier)
                    { ExpectedParentJson = setup.Parents };
                AssertPressureGroupFrontier(setup.Spec, setup.Copy, capture, lease, frontier);
                var interval = await AssertGroupZeroActualAdmissionAsync(window);
                await work(window, interval);
            },
            HasWindow: () => window is not null, HasCapture: () => capture is not null,
            HasLease: () => lease is not null, HasContext: () => false,
            AssertWindowInert: () =>
            {
                AssertPressureZeroCaptureInert(context, lease!, window!.Files, window.Generation, expectedPublication?.Invoke());
                return Task.CompletedTask;
            },
            CaptureDisposalFiles: () => disposalFiles = CaptureExactSpiritualFiles(context.FileSystem.GameSessionPath),
            DisposeCaptureExplicitly: () => capture!.Dispose(),
            AssertDisposedCaptureInert: () =>
            {
                Assert.False(capture!.IsCurrentOwner);
                if (disposalFiles is not null && generation is not null)
                    AssertPressureZeroCaptureInert(context, lease!, disposalFiles, generation, expectedPublication?.Invoke());
            },
            DisposeCaptureImplicitly: () => capture!.Dispose(),
            AssertNoPrivatePair: () => AssertPressureZeroCapturePublicationState(context, lease!, expectedPublication?.Invoke()),
            DisposeLease: async () => await lease!.DisposeAsync(), DisposeContext: () => Task.CompletedTask,
            AssertRootRemoved: () => { }));
        retainDisposed?.Invoke(capture!);
        WritePressureOwnerTiming("group_zero_" + phase, setup.Spec.Name, started);
    }

    /// <summary>Disposes warm actual/no-offer, recaptures exact ordinary inputs cold, and inspects genuine C3's unpublished receipt.</summary>
    /// <param name="setup">Existing independently signed group setup; no private packet or injury decision is submitted.</param>
    /// <param name="registerPublication">Only true for an explicitly assigned later consumer or pre-take refusal test; default inspects unpublished C3 only.</param>
    /// <param name="retainDisposed">Optional disposed-capture handle retained solely for later negative calls.</param>
    /// <param name="observeCompletedColdC3">Optional detached, independently checked retry DATA; no owner, key or authority is exported.</param>
    /// <returns>Detached completed DATA; only an explicitly assigned consumer may request registration.</returns>
    private async Task<PressureGroupCompletedData> CompleteGroupZeroSourceOnlyC3Async(PressureGroupPublicationSetup setup,
        bool registerPublication = false,
        Action<ValidationService.SpiritualOriginalTurnCapture>? retainDisposed = null,
        Action<GroupZeroRetryAttemptData>? observeCompletedColdC3 = null)
    {
        var kind = Assert.IsType<GroupZeroPressureContour>(setup.Spec.ZeroContour);
        var expected = SpiritualGroupZeroPressureData.Expected(kind);
        var context = setup.Copy.Context;
        var warmValidator = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);
        AssertNoConflictFrameErrors(await warmValidator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync());
        AssertNoConflictFrameErrors(await warmValidator.ValidateAcceptedTurnRawMortalItemMaterializationAsync());
        string? warmExchange = null; string? warmPreimage = null;
        ValidationService.SpiritualOriginalTurnCapture? disposedWarm = null;
        FileSystemManager.CanonicalWriteLease? disposedWarmLease = null;
        await WithGroupZeroCaptureAsync(setup, warmValidator, "warm_no_offer", (window, _) =>
        {
            var source = Assert.Single(Assert.Single(window.Capture.ReadClosedExchangeEvidence(window.Lease)).Sources);
            warmExchange = source.ExchangeJson; warmPreimage = source.StrainPreimageJson;
            disposedWarm = window.Capture; disposedWarmLease = window.Lease;
            return Task.CompletedTask;
        });
        Assert.NotNull(disposedWarm); Assert.False(disposedWarm!.IsCurrentOwner);
        var coldValidator = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);
        Assert.NotSame(warmValidator, coldValidator);
        AssertNoConflictFrameErrors(await coldValidator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync());
        AssertNoConflictFrameErrors(await coldValidator.ValidateAcceptedTurnRawMortalItemMaterializationAsync());
        PressureGroupCompletedData? completedData = null;
        ValidationService.SpiritualOriginalTurnCapture.SpiritualC4PublicationAuthority? registeredAuthority = null;
        await WithGroupZeroCaptureAsync(setup, coldValidator, "cold_source_only_c3", async (window, interval) =>
        {
            Assert.NotSame(disposedWarm, window.Capture); Assert.NotSame(disposedWarmLease, window.Lease);
            var capture = window.Capture; var lease = window.Lease;
            var source = Assert.Single(Assert.Single(capture.ReadClosedExchangeEvidence(lease)).Sources);
            Assert.Equal(warmExchange, source.ExchangeJson); Assert.Equal(warmPreimage, source.StrainPreimageJson);
            var preimage = JsonNode.Parse(Assert.IsType<string>(source.StrainPreimageJson))!;
            Assert.Equal(expected.Parents == 2 ? 4 : 6, preimage["version"]!.GetValue<int>());
            Assert.Equal(expected.Parents, preimage["parents"]!.AsArray().Count);
            var prefix = capture.ReadClosedResourceCandidatePrefix(lease, interval);
            var completion = await capture.CompleteOrdinaryReductionAsync(lease);
            RequireGuardianJourneyIssues(setup.Spec.Name, "group_zero_source_only_ordinary_completion", completion.Issues);
            Assert.True(completion.Success);
            var reduction = Assert.IsType<AcceptedMechanicsPlanner.CompletedOrdinaryMechanicsReduction>(completion.Reduction);
            Assert.True(reduction.RequiresSatisfactionReceipt);
            Assert.False(reduction.HasLiveDecisionReceipt); Assert.False(reduction.HasLiveWoundWork);
            Assert.Null(reduction.LiveWoundCompletion);
            Assert.Empty(reduction.Resources.ReplayTransitions);
            Assert.Equal(prefix.AppliedTransitions, reduction.Resources.AppliedTransitions);
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse(prefix.StateJson), JsonNode.Parse(reduction.Resources.StateAfterImage!.ToCanonicalJson())));
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse(prefix.HistoryJson), JsonNode.Parse(reduction.Resources.HistoryAfterImage!.ToCanonicalJson())));
            var actualRoot = reduction.OwnerCompanionAfterImages[AfterlifeSpiritualConflictState.StatePath];
            var expectedRoot = setup.Copy.Conflict.DeepClone().AsObject();
            expectedRoot["activeConflict"]!["exchangeLog"]![0] = SpiritualGroupZeroPressureData.ReadExchange(kind, actual: true);
            expectedRoot["activeConflict"]!["playerSideStrain"] = expected.Actual.Input.NewStrain;
            Assert.True(JsonNode.DeepEquals(expectedRoot, actualRoot));
            var journal = capture.ReadAllocationJournal(lease).ToJsonString(); var cursor = capture.ReadAllocationCursor(lease);
            var counts = capture.ReadExistingStrainReuseCounts();
            var repeated = await capture.CompleteOrdinaryReductionAsync(lease);
            Assert.Empty(repeated.Issues); Assert.Same(completion, repeated);
            Assert.Equal(journal, capture.ReadAllocationJournal(lease).ToJsonString()); Assert.Equal(cursor, capture.ReadAllocationCursor(lease));
            Assert.True(capture.ReadExistingStrainReuseCounts().FreshInputChecks > counts.FreshInputChecks);
            _storageOutput.WriteLine(JsonSerializer.Serialize(new
            {
                phase = "group_zero_source_only_preparation_prerequisites", setup.Spec.Name, expected,
                warmDisposed = !disposedWarm.IsCurrentOwner, freshValidatorCaptureAndLease = true,
                originalOrdinaryBytesBase64 = Convert.ToBase64String(setup.Authored[AfterlifeSpiritualConflictState.StatePath]!),
                source.ExchangeJson, source.StrainPreimageJson, version = preimage["version"]!.GetValue<int>(),
                parentCount = preimage["parents"]!.AsArray().Count, allParents = setup.Parents,
                priorReceipt = setup.Origin.ReceiptJson, actualRoot = actualRoot.ToJsonString(),
                resourceState = reduction.Resources.StateAfterImage.ToCanonicalJson(),
                resourceHistory = reduction.Resources.HistoryAfterImage.ToCanonicalJson(), journal, cursor,
                reduction.RequiresSatisfactionReceipt, reduction.HasLiveDecisionReceipt, reduction.HasLiveWoundWork,
                counters = ReadGroupZeroMeasurementCounters(capture)
            }));
            // This existing call internally runs genuine source-only C3. Registration is a separate explicit test option.
            var prepared = await capture.PrepareC4PublicationAsync(lease);
            _storageOutput.WriteLine(JsonSerializer.Serialize(new
            {
                phase = "group_zero_source_only_unpublished_preparation_result", setup.Spec.Name,
                version = preimage["version"]!.GetValue<int>(), parentCount = expected.Parents,
                allPrecedingPrerequisitesAccepted = true, prepared.Issues, authorityReturned = prepared.Authority is not null,
                publicationRegistered = AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease),
                counters = ReadGroupZeroMeasurementCounters(capture)
            }));
            // Initial compose omission, then compose-only reducer refusal, must both remain desired-success RED.
            RequireGuardianJourneyIssues(setup.Spec.Name, "group_zero_source_only_unpublished_preparation_desired_success", prepared.Issues);
            var authority = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture.SpiritualC4PublicationAuthority>(prepared.Authority);
            Assert.Null(authority.LiveWoundCompletion);
            var receipt = authority.Plan.OwnerCompanionAfterImages[SpiritualWoundOpportunityReceiptState.StatePath];
            var witness = AssertGroupZeroSourceOnlyReceipt(setup, receipt);
            Assert.True(JsonNode.DeepEquals(actualRoot, authority.Plan.OwnerCompanionAfterImages[AfterlifeSpiritualConflictState.StatePath]));
            Assert.Equal(journal, capture.ReadAllocationJournal(lease).ToJsonString()); Assert.Equal(cursor, capture.ReadAllocationCursor(lease));
            Assert.Empty(await authority.ValidateCurrentInputsAsync(context.FileSystem, lease));
            Assert.Single(capture.ReadClosedExchangeEvidence(lease));
            Assert.Equal(setup.Authored[AfterlifeSpiritualConflictState.StatePath], await context.FileSystem.ReadFileBytesAsync(lease, AfterlifeSpiritualConflictState.StatePath));
            AssertPressureZeroCaptureInert(context, lease, window.Files, window.Generation, expectedPublication: null);
            completedData = new(actualRoot.ToJsonString(), witness.ToJsonString(), receipt.ToJsonString(), reduction.Resources,
                WoundAcceptedTurnFingerprints.ComputeAcceptedEffectPlanPayload(reduction.Effects!));
            if (observeCompletedColdC3 is not null)
            {
                observeCompletedColdC3(ReadGroupZeroRetryAttempt(setup, completedData, journal, cursor, interval, reduction.Effects!));
                // A test callback is not a freshness proof. Recheck current authority and exact physical inputs afterward.
                Assert.Empty(await authority.ValidateCurrentInputsAsync(context.FileSystem, lease));
                Assert.Equal(journal, capture.ReadAllocationJournal(lease).ToJsonString());
                Assert.Equal(cursor, capture.ReadAllocationCursor(lease));
                AssertPressureZeroCaptureInert(context, lease, window.Files, window.Generation, expectedPublication: null);
                _storageOutput.WriteLine(JsonSerializer.Serialize(new
                {
                    phase = "group_zero_retry_observer_post_fresh_checks", setup.Spec.Name,
                    counters = ReadGroupZeroMeasurementCounters(capture)
                }));
            }
            if (registerPublication)
            {
                Assert.True(AcceptedMechanicsPlanAuthority.RegisterSpiritualPublication(context.FileSystem, lease, authority));
                registeredAuthority = authority;
            }
        }, expectedPublication: () => registeredAuthority, retainDisposed: retainDisposed);
        return Assert.IsType<PressureGroupCompletedData>(completedData);
    }

    /// <summary>Serializes the existing counters explicitly; internal records and tuples otherwise emit empty JSON objects.</summary>
    /// <param name="capture">The existing owner whose diagnostic scalars are read without computation.</param>
    /// <returns>Plain scalar measurement DATA, never authority.</returns>
    private static object ReadGroupZeroMeasurementCounters(ValidationService.SpiritualOriginalTurnCapture capture)
    {
        var reuse = capture.ReadExistingStrainReuseCounts(); var sources = capture.ReadExistingPressureGroupComputationCounts();
        return new
        {
            reuse.Queries, reuse.SignedParentReads, reuse.OrdinaryEvaluations, reuse.ActualEvaluations,
            reuse.FreshInputChecks, reuse.FreshDependencyChecks,
            currentCatalogBuildAttempts = sources.CurrentBatch.CatalogBuildAttempts,
            currentIdentityParseAttempts = sources.CurrentBatch.IdentityParseAttempts,
            currentHistoryParseAttempts = sources.CurrentBatch.HistoryParseAttempts,
            currentHistoryAgreementAttempts = sources.CurrentBatch.HistoryAgreementAttempts,
            currentParentResolutionAttempts = sources.CurrentBatch.ParentResolutionAttempts,
            signedCatalogBuildAttempts = sources.SignedBatch.CatalogBuildAttempts,
            signedIdentityParseAttempts = sources.SignedBatch.IdentityParseAttempts,
            signedHistoryParseAttempts = sources.SignedBatch.HistoryParseAttempts,
            signedHistoryAgreementAttempts = sources.SignedBatch.HistoryAgreementAttempts,
            signedParentResolutionAttempts = sources.SignedBatch.ParentResolutionAttempts,
            sources.ResourceBatchComputations
        };
    }

    /// <summary>Checks the exact unpublished whole receipt, every retained parent anchor and zero current injury rows.</summary>
    /// <param name="setup">Independent signed origin, complete parents and exact authored ordinary input.</param>
    /// <param name="receipt">Actual genuine source-only C3 result carried in the unregistered plan.</param>
    /// <returns>The exact newly appended comparison witness.</returns>
    private static JsonObject AssertGroupZeroSourceOnlyReceipt(PressureGroupPublicationSetup setup, JsonObject receipt)
    {
        var kind = Assert.IsType<GroupZeroPressureContour>(setup.Spec.ZeroContour);
        var expected = SpiritualGroupZeroPressureData.Expected(kind);
        var before = JsonNode.Parse(setup.Origin.ReceiptJson)!;
        Assert.Equal(3, receipt["schemaVersion"]!.GetValue<int>());
        foreach (var field in new[] { "sources", "decisions", "closures" })
            Assert.True(JsonNode.DeepEquals(before[field], receipt[field]), field);
        Assert.Equal(expected.Parents + 1, receipt["instances"]!.AsArray().Count);
        for (var index = 0; index < expected.Parents; index++)
            Assert.True(JsonNode.DeepEquals(before["instances"]![index], receipt["instances"]![index]));
        Assert.Empty(receipt["healingSources"]!.AsArray()); Assert.Empty(receipt["healingDecisions"]!.AsArray());
        Assert.Equal((1, 1), (receipt["nextHealingSourceOrdinal"]!.GetValue<int>(), receipt["nextHealingDecisionOrdinal"]!.GetValue<int>()));
        var row = Assert.Single(receipt["strainComparisons"]!.AsArray())!;
        Assert.Equal((1, 1, 2), (row["ordinal"]!.GetValue<int>(), row["instanceComparisonOrdinal"]!.GetValue<int>(), receipt["nextStrainComparisonOrdinal"]!.GetValue<int>()));
        var witness = row["witness"]!.AsObject();
        Assert.Equal(expected.Actual, SpiritualWoundSourceWitness.ValidateAndReadStrainData(witness, out var data));
        var comparison = Assert.IsType<SpiritualStrainPreimageData>(data);
        Assert.Equal(expected.Parents == 2 ? 4 : 6, comparison.Version); Assert.Equal(expected.Parents, comparison.Parents.Count);
        Assert.Equal(expected.Ordinary, comparison.OrdinaryCalculation);
        Assert.Equal(new CanonicalBeforeImage(true, setup.Authored[AfterlifeSpiritualConflictState.StatePath]).Fingerprint, comparison.OriginalInputFingerprint);
        Assert.Equal("/activeConflict/exchangeLog/0", comparison.OriginalExchangePointer);
        Assert.True(JsonNode.DeepEquals(SpiritualGroupZeroPressureData.ReadExchange(kind, actual: true), comparison.ReadActualExchange()));
        Assert.True(JsonNode.DeepEquals(SpiritualGroupZeroPressureData.ReadExchange(kind)["after"], comparison.ReadOrdinaryAfter(comparison.ReadActualExchange())));
        for (var index = 0; index < expected.Parents; index++)
        {
            Assert.Null(comparison.Parents[index].ParentSourceId);
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse(setup.Parents[index]), JsonNode.Parse(WoundMaterializationContract.SerializeCanonical(comparison.Parents[index].ParentWound))));
            var parent = WoundMaterializationContract.Parse(setup.Parents[index], "groupZero.sourceOnly.parent");
            Assert.True(parent.IsValid, JsonSerializer.Serialize(parent.Issues));
            Assert.Equal((Assert.Single(parent.Wound!.Consequences.OwnedEffectSources.RootBindings).EffectId, "component_001"),
                (comparison.Parents[index].EffectId, comparison.Parents[index].ComponentId));
        }
        Assert.Equal(new[] { expected.PlayerDie, 12 }, witness["turnEvidence"]!["acceptedD20Values"]!.AsArray().Select(value => value!.GetValue<int>()));
        Assert.Equal(new[] { 0, 1 }, witness["turnEvidence"]!["diceClaims"]!.AsArray().Select(value => value!["sourceIndex"]!.GetValue<int>()));
        Assert.Equal((46, 0, 0), (witness["turnEvidence"]!["turn"]!.GetValue<int>(), witness["sourceOrdinal"]!.GetValue<int>(), witness["exchangeOrdinal"]!.GetValue<int>()));
        var evidence = witness["turnEvidence"]!;
        var resolved = SpiritualWoundFirstOfferInstanceResolver.Resolve(
            new CanonicalBeforeImage(true, setup.Origin.Tracked[SpiritualWoundOpportunityReceiptState.StatePath]),
            new CanonicalBeforeImage(true, setup.Origin.Tracked[AfterlifeSpiritualConflictState.StatePath]), "chaos_sea",
            evidence["sessionId"]!.GetValue<string>(), evidence["requestId"]!.GetValue<string>(),
            evidence["snapshotToken"]!.GetValue<string>(), 46);
        Assert.Empty(resolved.Issues); Assert.NotNull(resolved.ProposedRow);
        Assert.True(JsonNode.DeepEquals(resolved.ProposedRow, receipt["instances"]![expected.Parents]));
        Assert.Equal(resolved.InstanceId, row["instanceId"]!.GetValue<string>());
        Assert.DoesNotContain(receipt["sources"]!.AsArray(), item => item!["witness"]!["sourceId"]!.GetValue<string>() == witness["sourceId"]!.GetValue<string>());
        Assert.DoesNotContain(receipt["decisions"]!.AsArray(), item => item!["sourceId"]!.GetValue<string>() == witness["sourceId"]!.GetValue<string>());
        var parsed = SpiritualWoundOpportunityReceiptState.Parse(receipt.ToJsonString(), SpiritualWoundOpportunityReceiptState.StatePath);
        var prior = SpiritualWoundOpportunityReceiptState.Parse(before.ToJsonString(), SpiritualWoundOpportunityReceiptState.StatePath);
        var history = WoundHistoryState.Parse(SpiritualWoundStateJson.DecodeUtf8JsonText(setup.Origin.Tracked[WoundHistoryState.HistoryPath]!), WoundHistoryState.HistoryPath);
        Assert.True(parsed.IsValid, JsonSerializer.Serialize(parsed.Issues)); Assert.True(prior.IsValid, JsonSerializer.Serialize(prior.Issues));
        Assert.True(history.IsValid, JsonSerializer.Serialize(history.Issues));
        Assert.True(parsed.State!.HasRetainedPressureComparisons); Assert.True(parsed.State.HasExactStrainHistory(history.State!));
        Assert.Equal("appended", SpiritualWoundOpportunityReceiptState.PlanAppend(prior.State!, parsed.State).Disposition);
        Assert.Equal("exact_replay", SpiritualWoundOpportunityReceiptState.PlanAppend(parsed.State, parsed.State).Disposition);
        var active = setup.Copy.Conflict["activeConflict"]!.DeepClone().AsObject();
        active["exchangeLog"]![0] = comparison.ReadActualExchange(); active["playerSideStrain"] = expected.Actual.Input.NewStrain;
        Assert.Equal(SpiritualWoundOpportunityReceiptState.RetainedPressureHistoryMatch.Exact, parsed.State.MatchRetainedPressureHistory("chaos_sea", active, 46));
        return witness;
    }
}
