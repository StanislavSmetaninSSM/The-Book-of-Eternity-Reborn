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
    /// <summary>Independent retained III/IV owners re-derive source-only DATA cold, publish once and validate signed47.</summary>
    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    public Task SpiritualPressureZero_ColdSourceOnlyPublishesAndValidatesSignedNext(int parentRank) =>
        WithSingletonIvJourneyFailureAsync("zero_publication_" + parentRank, () =>
            WithPressureZeroPublicationAsync("zero_publication_" + parentRank, parentRank, async setup =>
            {
                var completed = await CompletePressureZeroColdC3Async(setup);
                await PublishPressureZeroAndReadNextAsync(setup, completed);
            }));

    /// <summary>Captures signed origins before authoring; reuses detached publication records, not pair admission assumptions.</summary>
    private async Task WithPressureZeroPublicationAsync(string name, int rank,
        Func<PressureGroupPublicationSetup, Task> work, FileSystemManagerHooks? hooks = null)
    {
        var spec = new PressureGroupCase(name, Parents: 1, After: "strained", Resilience: 3,
            Ranks: new(rank, 3), AdvanceExpected: true);
        var data = SpiritualPressureGroupOwnerCaseData.Create(1, ranks: spec.Ranks);
        var parents = Array.AsReadOnly(data.Parents.Select(parent => parent.ToJsonString()).ToArray());
        ResourceMaterializationTestContext? context = null;
        await ExecutePressureGroupFixtureStagesAsync(new(
            Work: async () =>
            {
                var started = Stopwatch.GetTimestamp();
                context = await CreatePressureGroupSignedContextAsync(spec, data, allocated => context = allocated, hooks);
                WritePressureOwnerTiming("zero_signed_setup", name, started);
                PressureGroupPublicationSetup? setup = null;
                await ExecuteGuardianLeaseStagesAsync(context, async lease =>
                {
                    var signed = await AssertPressureGroupSignedInputsAsync(context, lease, spec, data);
                    var files = CaptureExactSpiritualFiles(context.FileSystem.GameSessionPath);
                    var tracked = new Dictionary<string, byte[]?>(StringComparer.Ordinal);
                    foreach (var path in CanonicalStateNormalizer.NormalizerRollbackTrackedFiles.Concat(new[]
                             { SpiritualWoundCaptureCheckpointState.StatePath, SpiritualWoundDecisionPendingState.StatePath,
                                 AcceptedMechanicsPlan.WoundCommandPath, SpiritualWoundOpportunityReceiptState.StatePath,
                                 ProjectionNarrativePath, ProjectionInterfacePath }).Distinct(StringComparer.Ordinal))
                        tracked[path] = await context.FileSystem.ReadFileBytesAsync(lease, path);
                    var receipt = ReadGuardianAttributionJson(tracked[SpiritualWoundOpportunityReceiptState.StatePath]!);
                    foreach (var field in new[] { "instances", "closures", "sources", "decisions" }) Assert.Single(receipt[field]!.AsArray());
                    var origin = new PressureGroupSignedOrigin(files, tracked, File.ReadAllBytes(context.FileSystem.SessionGenerationPath),
                        receipt.ToJsonString(), ReadGuardianAttributionJson(tracked[EffectIdentityState.StatePath]!).ToJsonString());
                    var active = ReadGuardianAttributionJson(tracked[AfterlifeSpiritualConflictState.StatePath]!)["activeConflict"]!;
                    Assert.Empty(active["exchangeLog"]!.AsArray());
                    Assert.Equal("clear", active["playerSideStrain"]!.GetValue<string>());
                    AssertPressureNoPrivatePair(context, lease);
                    var copy = await AuthorPressureGroupOrdinaryAsync(context, lease, spec, signed);
                    var raw = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(" \n" + copy.Conflict.ToJsonString() + "\n")).ToArray();
                    await context.FileSystem.WriteFileAtomicBytesAsync(lease, AfterlifeSpiritualConflictState.StatePath, raw);
                    Assert.True(JsonNode.DeepEquals(copy.Conflict, ReadGuardianAttributionJson(raw)));
                    Assert.NotEqual(new CanonicalBeforeImage(true, raw).Fingerprint,
                        new CanonicalBeforeImage(true, Encoding.UTF8.GetBytes(copy.Conflict.ToJsonString())).Fingerprint);
                    var authored = new Dictionary<string, byte[]?>(StringComparer.Ordinal);
                    foreach (var path in PressureAuthoredDraftPaths)
                        authored[path] = await context.FileSystem.ReadFileBytesAsync(lease, path);
                    setup = new(spec, copy, origin, parents, authored);
                });
                await work(Assert.IsType<PressureGroupPublicationSetup>(setup));
            },
            HasWindow: () => false, HasCapture: () => false, HasLease: () => false, HasContext: () => context is not null,
            AssertWindowInert: () => Task.CompletedTask, CaptureDisposalFiles: () => { }, DisposeCaptureExplicitly: () => { },
            AssertDisposedCaptureInert: () => { }, DisposeCaptureImplicitly: () => { }, AssertNoPrivatePair: () => { },
            DisposeLease: () => ValueTask.CompletedTask,
            DisposeContext: async () =>
            {
                var started = Stopwatch.GetTimestamp();
                await context!.DisposeAsync();
                WritePressureOwnerTiming("zero_owned_cleanup", name, started);
            },
            AssertRootRemoved: () => Assert.False(Directory.Exists(context!.RootPath))));
    }

    /// <summary>Confines one real source owner and lease to a fixed failure-preserving cleanup scope.</summary>
    private async Task WithPressureZeroCaptureAsync(PressureGroupPublicationSetup setup, ValidationService validator,
        string phase, Func<PressureGroupWindow, AcceptedMechanicsPlanner.SpiritualExchangeInterval, Task> work,
        Action<ValidationService.SpiritualOriginalTurnCapture>? retainDisposed = null,
        Func<ValidationService.SpiritualOriginalTurnCapture.SpiritualC4PublicationAuthority?>? expectedPublication = null)
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
                capture = await BeginPressureOrdinaryOwnerAsync(setup.Copy, lease, setup.Spec.Name + "_" + phase, validator);
                var files = CaptureExactSpiritualFiles(context.FileSystem.GameSessionPath);
                generation = File.ReadAllBytes(context.FileSystem.SessionGenerationPath);
                var frontier = await ReadPressureOwnerSnapshotAsync(capture, lease, setup.Spec.Name + "_" + phase);
                window = new PressureGroupWindow(setup.Spec, setup.Copy, lease, capture, files, generation, frontier)
                    { ExpectedParentJson = setup.Parents };
                AssertPressureGroupFrontier(setup.Spec, setup.Copy, capture, lease, frontier);
                var interval = await AssertPressureZeroActualAdmissionAsync(window);
                await work(window, interval);
            },
            HasWindow: () => window is not null, HasCapture: () => capture is not null, HasLease: () => lease is not null, HasContext: () => false,
            AssertWindowInert: () =>
            {
                // Cold C3 may transfer the exact registered authority, but never these physical input images.
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
            DisposeLease: async () => await lease!.DisposeAsync(), DisposeContext: () => Task.CompletedTask, AssertRootRemoved: () => { }));
        retainDisposed?.Invoke(capture!);
        WritePressureOwnerTiming("zero_" + phase, setup.Spec.Name, started);
    }

    /// <summary>Preserves exact physical inputs across disposal while allowing only the explicitly expected cold transfer.</summary>
    /// <param name="context">The independently owned singleton fixture.</param>
    /// <param name="lease">The still-active capture scope lease.</param>
    /// <param name="before">Exact file inventory and bytes captured before work or explicit disposal.</param>
    /// <param name="generation">Original physical generation bytes.</param>
    /// <param name="expectedPublication">Null before registration; otherwise the exact authority registered by this work.</param>
    private static void AssertPressureZeroCaptureInert(ResourceMaterializationTestContext context,
        FileSystemManager.CanonicalWriteLease lease, IReadOnlyDictionary<string, byte[]> before, byte[] generation,
        ValidationService.SpiritualOriginalTurnCapture.SpiritualC4PublicationAuthority? expectedPublication)
    {
        var after = CaptureExactSpiritualFiles(context.FileSystem.GameSessionPath);
        Assert.Equal(before.Keys.OrderBy(path => path, StringComparer.Ordinal), after.Keys.OrderBy(path => path, StringComparer.Ordinal));
        foreach (var (path, bytes) in before)
            Assert.True(bytes.SequenceEqual(after[path]), path);
        Assert.Equal(generation, File.ReadAllBytes(context.FileSystem.SessionGenerationPath));
        AssertPressureZeroCapturePublicationState(context, lease, expectedPublication);
    }

    /// <summary>Checks absent private files and either no authority or the precise untaken cold C4 registration.</summary>
    /// <param name="context">The fixture owning the registry.</param>
    /// <param name="lease">The active scope lease, retained through both disposal checks.</param>
    /// <param name="expectedPublication">The prepared authority saved immediately after successful registration, never inferred from a peek.</param>
    private static void AssertPressureZeroCapturePublicationState(ResourceMaterializationTestContext context,
        FileSystemManager.CanonicalWriteLease lease,
        ValidationService.SpiritualOriginalTurnCapture.SpiritualC4PublicationAuthority? expectedPublication)
    {
        if (expectedPublication is null)
        {
            AssertPressureNoPrivatePair(context, lease);
            Assert.False(AcceptedMechanicsPlanAuthority.TryPeekSpiritualPublication(context.FileSystem, lease, out _));
            return;
        }
        foreach (var path in new[] { SpiritualWoundCaptureCheckpointState.StatePath,
                     SpiritualWoundDecisionPendingState.StatePath, AcceptedMechanicsPlan.WoundCommandPath })
            Assert.False(context.FileSystem.FileExists(lease, path));
        Assert.True(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
        Assert.True(AcceptedMechanicsPlanAuthority.TryPeekSpiritualPublication(context.FileSystem, lease, out var registered));
        Assert.Same(expectedPublication, registered);
    }

    /// <summary>Warm disposal is followed by a fresh validator/capture, not C2 Open; only the cold exhausted owner transfers C4.</summary>
    private async Task<PressureGroupCompletedData> CompletePressureZeroColdC3Async(PressureGroupPublicationSetup setup,
        Action<ValidationService.SpiritualOriginalTurnCapture>? retainDisposed = null,
        Action<string, PressureGroupCompletedData, AcceptedMechanicsPlanner.SpiritualExchangeInterval, EffectAcceptedTurnPlan>? observeCompletedColdC3 = null)
    {
        var context = setup.Copy.Context;
        var warmValidator = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);
        AssertNoConflictFrameErrors(await warmValidator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync());
        AssertNoConflictFrameErrors(await warmValidator.ValidateAcceptedTurnRawMortalItemMaterializationAsync());
        string? warmActual = null;
        string? warmPreimage = null;
        await WithPressureZeroCaptureAsync(setup, warmValidator, "warm_source_only", (window, _) =>
        {
            var source = Assert.Single(Assert.Single(window.Capture.ReadClosedExchangeEvidence(window.Lease)).Sources);
            warmActual = source.ExchangeJson;
            warmPreimage = source.StrainPreimageJson;
            return Task.CompletedTask;
        });
        var coldValidator = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);
        AssertNoConflictFrameErrors(await coldValidator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync());
        AssertNoConflictFrameErrors(await coldValidator.ValidateAcceptedTurnRawMortalItemMaterializationAsync());
        PressureGroupCompletedData? completedData = null;
        ValidationService.SpiritualOriginalTurnCapture.SpiritualC4PublicationAuthority? registeredAuthority = null;
        await WithPressureZeroCaptureAsync(setup, coldValidator, "cold_source_only_c3", async (window, interval) =>
        {
            var capture = window.Capture;
            var lease = window.Lease;
            var source = Assert.Single(Assert.Single(capture.ReadClosedExchangeEvidence(lease)).Sources);
            Assert.Equal(warmActual, source.ExchangeJson);
            Assert.Equal(warmPreimage, source.StrainPreimageJson);
            var prefix = capture.ReadClosedResourceCandidatePrefix(lease, interval);
            var completion = await capture.CompleteOrdinaryReductionAsync(lease);
            RequireGuardianJourneyIssues(setup.Spec.Name, "zero_cold_completion", completion.Issues);
            Assert.True(completion.Success);
            var reduction = Assert.IsType<AcceptedMechanicsPlanner.CompletedOrdinaryMechanicsReduction>(completion.Reduction);
            Assert.True(reduction.RequiresSatisfactionReceipt);
            Assert.False(reduction.HasLiveDecisionReceipt);
            Assert.False(reduction.HasLiveWoundWork);
            Assert.Null(reduction.LiveWoundCompletion);
            Assert.Empty(reduction.Resources.ReplayTransitions);
            Assert.Equal(prefix.AppliedTransitions, reduction.Resources.AppliedTransitions);
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse(prefix.StateJson), JsonNode.Parse(reduction.Resources.StateAfterImage!.ToCanonicalJson())));
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse(prefix.HistoryJson), JsonNode.Parse(reduction.Resources.HistoryAfterImage!.ToCanonicalJson())));
            var actualRoot = reduction.OwnerCompanionAfterImages[AfterlifeSpiritualConflictState.StatePath];
            var journal = capture.ReadAllocationJournal(lease).ToJsonString();
            var cursor = capture.ReadAllocationCursor(lease);
            var counts = capture.ReadExistingStrainReuseCounts();
            var repeated = await capture.CompleteOrdinaryReductionAsync(lease);
            Assert.Empty(repeated.Issues);
            Assert.Same(completion, repeated);
            Assert.Equal(journal, capture.ReadAllocationJournal(lease).ToJsonString());
            Assert.Equal(cursor, capture.ReadAllocationCursor(lease));
            Assert.True(capture.ReadExistingStrainReuseCounts().FreshInputChecks > counts.FreshInputChecks);
            var prepared = await capture.PrepareC4PublicationAsync(lease);
            RequireGuardianJourneyIssues(setup.Spec.Name, "zero_cold_c4_prepare", prepared.Issues);
            var authority = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture.SpiritualC4PublicationAuthority>(prepared.Authority);
            Assert.Null(authority.LiveWoundCompletion);
            var receipt = authority.Plan.OwnerCompanionAfterImages[SpiritualWoundOpportunityReceiptState.StatePath];
            var witness = AssertPressureZeroReceiptAppend(setup, receipt);
            Assert.True(JsonNode.DeepEquals(actualRoot, authority.Plan.OwnerCompanionAfterImages[AfterlifeSpiritualConflictState.StatePath]));
            Assert.Equal(journal, capture.ReadAllocationJournal(lease).ToJsonString());
            Assert.Equal(cursor, capture.ReadAllocationCursor(lease));
            Assert.Empty(await authority.ValidateCurrentInputsAsync(context.FileSystem, lease));
            Assert.Empty(await authority.ValidateCurrentInputsAsync(context.FileSystem, lease));
            Assert.Single(capture.ReadClosedExchangeEvidence(lease));
            Assert.Equal(setup.Authored[AfterlifeSpiritualConflictState.StatePath], await context.FileSystem.ReadFileBytesAsync(lease, AfterlifeSpiritualConflictState.StatePath));
            AssertExistingPressureInputInert(context, lease, window.Files, window.Generation);
            Assert.True(AcceptedMechanicsPlanAuthority.RegisterSpiritualPublication(context.FileSystem, lease, authority));
            registeredAuthority = authority;
            completedData = new(actualRoot.ToJsonString(), witness.ToJsonString(), receipt.ToJsonString(), reduction.Resources,
                WoundAcceptedTurnFingerprints.ComputeAcceptedEffectPlanPayload(reduction.Effects!));
            if (observeCompletedColdC3 is not null)
            {
                // Detached diagnostics only: reuse the already checked post-C3 journal/cursor and completed effect DATA.
                var effects = reduction.Effects!;
                var diagnostic = JsonSerializer.Serialize(new
                {
                    AllocationJournalJson = journal, AllocationCursor = cursor,
                    Interval = new
                    {
                        interval.ConflictId, interval.ExchangeId, interval.Ordinal,
                        interval.AppliedRange, interval.ReplayRange, interval.EventRange,
                        interval.AppliedTransitions, interval.ReplayTransitions, interval.Events,
                        EffectBefore = ReadPressureZeroEffectPrefixDiagnosticData(interval.EffectBefore),
                        EffectAfter = ReadPressureZeroEffectPrefixDiagnosticData(interval.EffectAfter)
                    },
                    effects.InputFingerprint, effects.CarrierAuthorityFingerprint,
                    effects.SourceAuthorityFingerprint, effects.TargetAuthorityFingerprint,
                    SkillScopeFingerprint = effects.SkillScopeAuthority?.Fingerprint,
                    effects.AllocatedCombatantIds, effects.AllocatedEffectIds, effects.AllocatedTransitionIds,
                    effects.WoundApplicationRootEffectBindings, effects.Sources, effects.Targets, effects.SourceBindings,
                    DeferredReactions = effects.DeferredReactions.Select(ReadPressureZeroReactionDiagnosticData).ToArray(),
                    effects.ReactionExpansionCount,
                    ReactionExpansionUsage = effects.ReactionExpansionUsage.Select(row => new { row.Key, row.Value }).ToArray(),
                    effects.ActiveEffects, effects.ResourceTriggerCarriers, effects.AcceptedCarrierBaselines,
                    RetainedSourceAuthorityFingerprint = effects.SourceAuthority.Fingerprint,
                    RetainedTargetAuthorityFingerprint = effects.TargetAuthority.Fingerprint,
                    effects.EventInput, effects.CarrierBeforeImages, effects.CarrierAfterImages,
                    effects.IdentityIndexBeforeImage, effects.IdentityIndexAfterImage, effects.TouchedPaths, effects.DeletedPaths,
                    effects.IsAcceptedBoundaryComplete, effects.AcceptedBoundaryBasePlanFingerprint, effects.AcceptedBoundaryFinalPlanFingerprint,
                    MortalRecoveryExecutionFingerprint = EffectAcceptedTurnPlanner.GetMortalRecoveryExecutionFingerprint(effects),
                    SpiritualHealingExecutionFingerprint = EffectAcceptedTurnPlanner.GetSpiritualHealingExecutionFingerprint(effects),
                    MortalDiagnosisExecutionFingerprint = EffectAcceptedTurnPlanner.GetMortalDiagnosisExecutionFingerprint(effects),
                    MortalTreatmentExecutionFingerprint = EffectAcceptedTurnPlanner.GetMortalTreatmentExecutionFingerprint(effects)
                });
                observeCompletedColdC3(diagnostic, completedData, interval, effects);
            }
            var completedCounts = capture.ReadExistingStrainReuseCounts();
            _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase = "zero_cold_completed_counts", setup.Spec.Name,
                reuse = new { completedCounts.Queries, completedCounts.SignedParentReads, completedCounts.OrdinaryEvaluations,
                    completedCounts.ActualEvaluations, completedCounts.FreshInputChecks, completedCounts.FreshDependencyChecks },
                computations = capture.ReadExistingPressureGroupComputationCounts(),
                sourceCount = 1, comparisonCount = 1, privateDecisionCount = 0, cursor }));
        }, retainDisposed, expectedPublication: () => registeredAuthority);
        return Assert.IsType<PressureGroupCompletedData>(completedData);
    }

    /// <summary>Projects the already retained immutable interval prefix, without querying a live source or draft.</summary>
    /// <param name="prefix">The real interval's closed before or after image; its constructor excludes pending/projection ordinals.</param>
    /// <returns>Explicit semantic vectors and their stored fingerprint, never a fresh permission or computation.</returns>
    private static object ReadPressureZeroEffectPrefixDiagnosticData(AcceptedEffectBoundaryTranscript.ClosedPrefix prefix) => new
    {
        prefix.Fingerprint, prefix.PlanAuthority, prefix.Boundaries, prefix.BoundaryCloses, prefix.CausalClosures,
        AcceptedActivations = prefix.AcceptedActivations.Select(row => new
        {
            row.Boundary, row.Activation, Candidate = AcceptedEffectBoundaryTranscript.ReadCandidateDiagnosticData(row.Candidate),
            row.CandidateFingerprint, row.MechanicsOrdinal
        }).ToArray(),
        RejectedActivations = prefix.RejectedActivations.Select(row => new
        {
            row.Boundary, Candidate = AcceptedEffectBoundaryTranscript.ReadCandidateDiagnosticData(row.Candidate),
            row.CandidateFingerprint, row.Reason, row.BlockedAvailabilityEffectId
        }).ToArray(),
        prefix.AppliedComponentEvidence, prefix.ResourceMutations,
        ReleasedReactions = prefix.ReleasedReactions.Select(row => new
        {
            row.Boundary, row.Activation, Reaction = ReadPressureZeroReactionDiagnosticData(row.Reaction),
            row.Stage, row.MechanicsOrdinal
        }).ToArray(),
        TerminalAvailabilityReservations = prefix.TerminalAvailabilityReservations.Select(row => new
        {
            row.Boundary, row.Activation, Subject = new { row.Subject.EffectId, row.Subject.Authority },
            row.Kind, row.ReactionFingerprint, row.ReplayStableOrderKey, row.MechanicsOrdinal
        }).ToArray(),
        ExpansionUsage = prefix.ExpansionUsage.Select(row => new { row.Key, row.Value }).ToArray()
    };

    /// <summary>Preserves retained reaction fields, including replacement identity whose properties are internal.</summary>
    /// <param name="reaction">Detached existing reaction DATA; no reaction is executed.</param>
    /// <returns>Complete explicit diagnostic fields without resolving a new source.</returns>
    private static object ReadPressureZeroReactionDiagnosticData(EffectReactionExecution reaction) => new
    {
        reaction.EventRef, reaction.TriggerEventRef, reaction.CausalEventRef, reaction.Turn,
        reaction.EventKind, reaction.Target, reaction.EffectId, reaction.TriggerId, reaction.ComponentId,
        reaction.ResultKind, reaction.Dependency, reaction.AfterComponentId, reaction.MaxExpansion,
        reaction.DownstreamSource, reaction.Parameters, reaction.DownstreamSourceKey, reaction.ComponentPriority,
        ReplacementTarget = reaction.ReplacementTarget is { } replacement
            ? new { replacement.EffectId, replacement.Authority } : null
    };

    /// <summary>Checks v3 comparison-only append, immutable old receipt arrays and exact History/input/actual/dice bindings.</summary>
    private static JsonObject AssertPressureZeroReceiptAppend(PressureGroupPublicationSetup setup, JsonObject receipt)
    {
        var before = JsonNode.Parse(setup.Origin.ReceiptJson)!;
        Assert.Equal(3, receipt["schemaVersion"]!.GetValue<int>());
        foreach (var field in new[] { "sources", "decisions", "closures" })
            Assert.True(JsonNode.DeepEquals(before[field], receipt[field]), field);
        Assert.Empty(receipt["healingSources"]!.AsArray());
        Assert.Empty(receipt["healingDecisions"]!.AsArray());
        Assert.Equal(2, receipt["instances"]!.AsArray().Count);
        Assert.True(JsonNode.DeepEquals(before["instances"]![0], receipt["instances"]![0]));
        var row = Assert.Single(receipt["strainComparisons"]!.AsArray())!;
        Assert.Equal((1, 1, 2), (row["ordinal"]!.GetValue<int>(), row["instanceComparisonOrdinal"]!.GetValue<int>(), receipt["nextStrainComparisonOrdinal"]!.GetValue<int>()));
        var witness = row["witness"]!.AsObject();
        Assert.Equal(SpiritualPressureZeroTestData.ActualCalculation(), SpiritualWoundSourceWitness.ValidateAndReadStrainData(witness, out var strain));
        Assert.Equal(3, Assert.IsType<SpiritualStrainPreimageData>(strain).Version);
        Assert.Equal(SpiritualPressureZeroTestData.OrdinaryCalculation(), strain!.OrdinaryCalculation);
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(Assert.Single(setup.Parents)), JsonNode.Parse(WoundMaterializationContract.SerializeCanonical(strain.ParentWound))));
        Assert.Equal(new CanonicalBeforeImage(true, setup.Authored[AfterlifeSpiritualConflictState.StatePath]).Fingerprint, strain.OriginalInputFingerprint);
        Assert.Equal("/activeConflict/exchangeLog/0", strain.OriginalExchangePointer);
        Assert.True(JsonNode.DeepEquals(ReadPressurePairAdmissionExchange("fractured"), strain.ReadActualExchange()));
        Assert.Equal(new[] { 4, 12 }, witness["turnEvidence"]!["acceptedD20Values"]!.AsArray().Select(value => value!.GetValue<int>()));
        Assert.Equal(new[] { 0, 1 }, witness["turnEvidence"]!["diceClaims"]!.AsArray().Select(value => value!["sourceIndex"]!.GetValue<int>()));
        Assert.Equal((46, 0, 0), (witness["turnEvidence"]!["turn"]!.GetValue<int>(), witness["sourceOrdinal"]!.GetValue<int>(), witness["exchangeOrdinal"]!.GetValue<int>()));
        var parsed = SpiritualWoundOpportunityReceiptState.Parse(receipt.ToJsonString(), "test.zero.publication");
        var history = WoundHistoryState.Parse(SpiritualWoundStateJson.DecodeUtf8JsonText(setup.Origin.Tracked[WoundHistoryState.HistoryPath]!), WoundHistoryState.HistoryPath);
        Assert.True(parsed.IsValid, string.Join("\n", parsed.Issues));
        Assert.True(history.IsValid, string.Join("\n", history.Issues));
        Assert.True(parsed.State!.HasRetainedPressureComparisons);
        Assert.True(parsed.State.HasExactStrainHistory(history.State!));
        return witness;
    }

    /// <summary>Common C4 publication occurs only after source owners and leases have been disposed.</summary>
    private async Task PublishPressureZeroAndReadNextAsync(PressureGroupPublicationSetup setup, PressureGroupCompletedData completed)
    {
        var context = setup.Copy.Context;
        var validator = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);
        var started = Stopwatch.GetTimestamp();
        AssertNoConflictFrameErrors(await validator.ValidateAcceptedTurnRawResourceMaterializationAsync());
        var plan = await PeekPlanAsync(context);
        await AssertPressureGroupRegisteredPlanAsync(setup, completed, plan);
        var published = await AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(
            context.FileSystem, context.Normalizer, validator, await ReadSpiritualC4BackupsAsync(context));
        AssertNoConflictFrameErrors(published.Issues);
        Assert.Same(plan, published.MechanicsPlan);
        await AssertPressureZeroPublishedAsync(setup, completed, plan);
        WritePressureOwnerTiming("zero_common_c4", setup.Spec.Name, started);
        await ReadPressureGroupSignedNextAsync(setup, validator);
    }

    /// <summary>Reads complete published owners, unchanged parent/History and one lawful identity expiry, with each AP spend once.</summary>
    private static async Task AssertPressureZeroPublishedAsync(PressureGroupPublicationSetup setup,
        PressureGroupCompletedData completed, AcceptedMechanicsPlan plan)
    {
        var context = setup.Copy.Context;
        AssertPressureExistingResourcePlan(plan, JsonNode.Parse(completed.ActualRootJson)!.AsObject());
        foreach (var row in plan.WoundCarrierAfterImages.Concat(plan.EffectCarrierAfterImages).Concat(plan.OwnerCompanionAfterImages))
            Assert.True(JsonNode.DeepEquals(row.Value, await context.ReadJsonAsync(row.Key)), row.Key);
        Assert.True(JsonNode.DeepEquals(plan.StateAfterImage, await context.ReadJsonAsync(ResourceMaterializationContract.StatePath)));
        Assert.True(JsonNode.DeepEquals(plan.HistoryAfterImage, await context.ReadJsonAsync(ResourceMaterializationContract.HistoryPath)));
        Assert.True(JsonNode.DeepEquals(plan.EffectIdentityAfterImage, await context.ReadJsonAsync(EffectIdentityState.StatePath)));
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(completed.ActualRootJson), await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath)));
        foreach (var path in new[] { WoundIdentityState.StatePath, WoundHistoryState.HistoryPath })
            Assert.Equal(setup.Origin.Tracked[path], await context.FileSystem.ReadFileBytesAsync(path));
        var profiles = (await context.ReadJsonAsync(AfterlifeEntityProfileState.StatePath))![AfterlifeEntityProfileState.ProfilesProperty]!.AsArray();
        var player = Assert.Single(profiles, row => row!["actorType"]!.GetValue<string>() == "player_soul" && row["actorId"]!.GetValue<string>() == "player_soul")!;
        var parent = JsonNode.Parse(Assert.Single(setup.Parents))!;
        Assert.True(JsonNode.DeepEquals(parent, Assert.Single(player["activeWounds"]!.AsArray())));
        var roots = parent["consequences"]!["ownedEffectSources"]!["rootBindings"]!.AsArray().Select(root => root!["effectId"]!.GetValue<string>()).ToArray();
        AssertPressureGuardRecoveryIdentityAppend(JsonNode.Parse(setup.Origin.IdentityJson)!.AsObject(),
            (await context.ReadJsonAsync(EffectIdentityState.StatePath))!.AsObject(), roots, completed.Resources, plan, turn: 46);
        var receipt = (await context.ReadJsonAsync(SpiritualWoundOpportunityReceiptState.StatePath))!.AsObject();
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(completed.ReceiptJson), receipt));
        AssertPressureZeroReceiptAppend(setup, receipt);
        var mechanics = await EffectMechanicsSnapshot.LoadAsync(context.FileSystem);
        Assert.True(mechanics.IsAccepted, string.Join("\n", mechanics.Issues));
        foreach (var root in roots) Assert.Contains(mechanics.Components, component => component.EffectId == root);
        foreach (var path in new[] { SpiritualWoundCaptureCheckpointState.StatePath, SpiritualWoundDecisionPendingState.StatePath, AcceptedMechanicsPlan.WoundCommandPath })
        {
            Assert.Contains(path, plan.ConsumedPaths);
            Assert.Null(await context.FileSystem.ReadFileBytesAsync(path));
        }
        await ExecuteGuardianLeaseStagesAsync(context, lease =>
        {
            AssertPressureNoPrivatePair(context, lease);
            Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
            Assert.False(AcceptedMechanicsPlanAuthority.TryPeekSpiritualPublication(context.FileSystem, lease, out _));
            return Task.CompletedTask;
        });
    }
}
