using System.Diagnostics;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>One parent-IV committed-write cut restores signed46 bytes while invalidating freshness, then genuine fresh same-byte owners publish once through signed47.</summary>
    [Fact]
    public Task SpiritualGuardianZero_LateCutRestoresSignedOriginAndFreshRetryPublishesOnce()
    {
        var armed = false; var cutEnabled = true; var cuts = 0;
        CurrentCommittedPublicationWitness? witness = null;
        PressureGroupSignedOrigin? signedOrigin = null;
        var injected = new IOException("Guardian maximum-zero comparison receipt committed read-back failure.");
        var hooks = new FileSystemManagerHooks
        {
            LocalPublicationObserver = (phase, index) => { if (armed) witness!.Observe(phase, index); },
            BeforeCanonicalReadOpenAsync = path =>
            {
                if (armed && cutEnabled && path == SpiritualWoundOpportunityReceiptState.StatePath &&
                    witness!.Has(AfterlifeSpiritualConflictState.StatePath) && witness.Has(ResourceMaterializationContract.StatePath) && witness.Has(SpiritualWoundOpportunityReceiptState.StatePath))
                {
                    witness.RequireSingleCurrent(AfterlifeSpiritualConflictState.StatePath); witness.RequireSingleCurrent(ResourceMaterializationContract.StatePath);
                    witness.RequireSingleCurrent(SpiritualWoundOpportunityReceiptState.StatePath);
                    cuts++; cutEnabled = false; armed = false; throw injected;
                }
                return Task.CompletedTask;
            }
        };
        return WithGuardianZeroPressureSetupAsync("guardian_zero_parent_iv_late_retry", SpiritualGuardianZeroPressureTestData.Create(4), async setup =>
        {
            var origin = Assert.IsType<PressureGroupSignedOrigin>(signedOrigin); var context = setup.Context;
            var beforeProfiles = origin.Tracked[AfterlifeEntityProfileState.StatePath]!;
            var beforeIdentity = origin.Tracked[EffectIdentityState.StatePath]!;
            var beforeWoundIdentity = origin.Tracked[WoundIdentityState.StatePath]!;
            ValidationService.SpiritualOriginalTurnCapture? oldCapture = null;
            ValidationService.SpiritualOriginalTurnCapture.SpiritualC4PublicationAuthority? oldAuthority = null;
            GuardianZeroRetryAttemptData? firstAttempt = null;
            var first = await CompleteGuardianZeroSourceOnlyC3Async(setup, (authority, lease) =>
            {
                Assert.True(AcceptedMechanicsPlanAuthority.RegisterSpiritualPublication(context.FileSystem, lease, authority));
                oldAuthority = authority; return Task.CompletedTask;
            }, observePrepared: json =>
            {
                _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_zero_first_retry_reader_start", setup.Name, hookArmed = armed }));
                Assert.False(armed); var readerStarted = Stopwatch.GetTimestamp();
                firstAttempt = ReadGuardianZeroRetryAttempt(setup, origin, json);
                WritePressureOwnerTiming("guardian_zero_first_retry_reader", setup.Name, readerStarted);
                var probesStarted = Stopwatch.GetTimestamp();
                var probes = AssertGuardianZeroRetryProbes(setup, origin, firstAttempt, _storageOutput.WriteLine);
                WritePressureOwnerTiming("guardian_zero_first_retry_detached_probes", setup.Name, probesStarted);
                WriteGuardianZeroRetryMap("guardian_zero_first_attempt_verified_before_late_hook", setup.Name, firstAttempt, probes);
            }, retainColdCapture: capture => oldCapture = capture);
            Assert.NotNull(oldCapture); Assert.False(oldCapture!.IsCurrentOwner); Assert.NotNull(oldAuthority);
            var checkedFirst = Assert.IsType<GuardianZeroRetryAttemptData>(firstAttempt);
            await WithGuardianZeroControlLeaseAsync(context, setup.Name, "first_registered_before_late_hook", async lease =>
            {
                Assert.True(AcceptedMechanicsPlanAuthority.TryPeekSpiritualPublication(context.FileSystem, lease, out var current)); Assert.Same(oldAuthority, current);
                Assert.Empty(await oldAuthority!.ValidateCurrentInputsAsync(context.FileSystem, lease));
            }, expectedPublication: oldAuthority);
            var backups = await ReadSpiritualC4BackupsAsync(context);
            witness = new(context.FileSystem, AfterlifeSpiritualConflictState.StatePath, ResourceMaterializationContract.StatePath, SpiritualWoundOpportunityReceiptState.StatePath);
            var firstWitness = witness;
            var epoch = context.FileSystem.CanonicalRootAuthorityIdentity.ValidationEpoch;
            Assert.True(epoch.TryCapture(out var epochBefore)); var activityBefore = epoch.CaptureActivity(); Assert.Equal(0, activityBefore.Active);
            var started = Stopwatch.GetTimestamp(); Exception? failure; armed = true;
            try
            {
                failure = await Record.ExceptionAsync(() => AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(
                    context.FileSystem, context.Normalizer, context.Validator, backups));
            }
            finally { armed = false; }
            Assert.Equal(1, cuts); Assert.Same(injected, failure);
            firstWitness.AssertRestored("guardian-zero-parent-IV-signed46-late-cut", origin.Tracked.ToDictionary(
                row => context.FileSystem.ResolvePath(row.Key), row => row.Value, StringComparer.Ordinal), failure, _storageOutput.WriteLine);
            AssertGuardianZeroSignedOrigin(context, origin);
            Assert.True(epoch.TryCapture(out var epochAfter)); Assert.NotSame(epochBefore, epochAfter);
            var activityAfter = epoch.CaptureActivity(); Assert.Equal(0, activityAfter.Active);
            Assert.True(activityAfter.Begins > activityBefore.Begins);
            Assert.Equal(activityAfter.Begins - activityBefore.Begins, activityAfter.Ends - activityBefore.Ends);
            _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_zero_late_cut_durable_bytes_and_fresh_epoch", setup.Name,
                epochChanged = !ReferenceEquals(epochBefore, epochAfter), mutationBegins = activityAfter.Begins - activityBefore.Begins,
                mutationEnds = activityAfter.Ends - activityBefore.Ends, activeMutations = activityAfter.Active,
                durableGenerationBase64 = Convert.ToBase64String(origin.Generation), privateJournalAbsent = !File.Exists(firstWitness.JournalPath) }));
            await WithGuardianZeroControlLeaseAsync(context, setup.Name, "late_cut_retired_capabilities", async lease =>
            {
                Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
                Assert.False(AcceptedMechanicsPlanAuthority.TryPeekSpiritualPublication(context.FileSystem, lease, out _));
                Assert.False(AcceptedMechanicsPlanAuthority.TryTakeSpiritualPublication(context.FileSystem, lease, oldAuthority!, out _));
                Assert.False(oldCapture.IsCurrentOwner);
                await Assert.ThrowsAsync<ObjectDisposedException>(async () => await oldCapture.ReadExistingPressureOrdinaryProbeStateAsync(lease));
                AssertPressureNoPrivatePair(context, lease);
            });
            AssertGuardianZeroSignedOrigin(context, origin);
            WritePressureOwnerTiming("guardian_zero_late_cut_full_signed46_restore", setup.Name, started);
            await WithGuardianZeroControlLeaseAsync(context, setup.Name, "late_retry_identical_reauthoring", async lease =>
            {
                foreach (var row in setup.Authored)
                {
                    Assert.Contains(row.Key, PressureAuthoredDraftPaths);
                    if (row.Value is null) Assert.Null(await context.FileSystem.ReadFileBytesAsync(lease, row.Key));
                    else await context.FileSystem.WriteFileAtomicBytesAsync(lease, row.Key, row.Value);
                }
                foreach (var row in setup.Authored) Assert.Equal(row.Value, await context.FileSystem.ReadFileBytesAsync(lease, row.Key));
                Assert.False(AcceptedMechanicsPlanAuthority.TryTakeSpiritualPublication(context.FileSystem, lease, oldAuthority!, out _)); AssertPressureNoPrivatePair(context, lease);
            });
            AssertGuardianZeroOnlyAuthored(context, setup, origin);
            GuardianZeroRetryAttemptData? retryAttempt = null;
            ValidationService.SpiritualOriginalTurnCapture? retryCapture = null;
            ValidationService.SpiritualOriginalTurnCapture.SpiritualC4PublicationAuthority? retryAuthority = null;
            started = Stopwatch.GetTimestamp();
            var retry = await CompleteGuardianZeroSourceOnlyC3Async(setup, (authority, lease) =>
            {
                Assert.True(AcceptedMechanicsPlanAuthority.RegisterSpiritualPublication(context.FileSystem, lease, authority));
                retryAuthority = authority; return Task.CompletedTask;
            }, observePrepared: json =>
            {
                Assert.False(armed); var readStarted = Stopwatch.GetTimestamp();
                retryAttempt = ReadGuardianZeroRetryAttempt(setup, origin, json);
                WritePressureOwnerTiming("guardian_zero_fresh_retry_reader", setup.Name, readStarted);
                WriteGuardianZeroRetryMap("guardian_zero_fresh_attempt_verified_before_publication", setup.Name, retryAttempt, null);
            }, retainColdCapture: capture => retryCapture = capture);
            Assert.Equal(first.ActualRootJson, retry.ActualRootJson); Assert.NotNull(retryCapture); Assert.False(retryCapture!.IsCurrentOwner); Assert.NotSame(oldCapture, retryCapture);
            var checkedRetry = Assert.IsType<GuardianZeroRetryAttemptData>(retryAttempt);
            var compareStarted = Stopwatch.GetTimestamp(); AssertGuardianZeroFreshRetryEquivalent(setup, origin, checkedFirst, checkedRetry);
            WritePressureOwnerTiming("guardian_zero_complete_fresh_retry_equivalence", setup.Name, compareStarted);
            Assert.NotNull(retryAuthority); Assert.NotSame(oldAuthority, retryAuthority);
            await WithGuardianZeroControlLeaseAsync(context, setup.Name, "fresh_authority_survives_stale_refusal", async lease =>
            {
                Assert.True(AcceptedMechanicsPlanAuthority.TryPeekSpiritualPublication(context.FileSystem, lease, out var current)); Assert.Same(retryAuthority, current);
                Assert.Empty(await retryAuthority!.ValidateCurrentInputsAsync(context.FileSystem, lease));
                Assert.False(AcceptedMechanicsPlanAuthority.TryTakeSpiritualPublication(context.FileSystem, lease, oldAuthority!, out _));
                Assert.True(AcceptedMechanicsPlanAuthority.TryPeekSpiritualPublication(context.FileSystem, lease, out var stillCurrent)); Assert.Same(retryAuthority, stillCurrent);
                Assert.Empty(await retryAuthority.ValidateCurrentInputsAsync(context.FileSystem, lease));
                Assert.False(oldCapture.IsCurrentOwner); await Assert.ThrowsAsync<ObjectDisposedException>(async () => await oldCapture.ReadExistingPressureOrdinaryProbeStateAsync(lease));
            }, expectedPublication: retryAuthority);
            witness = new(context.FileSystem, AfterlifeSpiritualConflictState.StatePath, ResourceMaterializationContract.StatePath, SpiritualWoundOpportunityReceiptState.StatePath);
            armed = true;
            try { await PublishGuardianZeroAndReadNextAsync(setup, retry, beforeProfiles, beforeIdentity, beforeWoundIdentity); }
            finally { armed = false; }
            Assert.Equal(1, cuts); witness.RequireSingleCurrent(AfterlifeSpiritualConflictState.StatePath);
            witness.RequireSingleCurrent(ResourceMaterializationContract.StatePath); witness.RequireSingleCurrent(SpiritualWoundOpportunityReceiptState.StatePath);
            await WithGuardianZeroControlLeaseAsync(context, setup.Name, "fresh_retry_once_only_consumption", async lease =>
            {
                Assert.False(AcceptedMechanicsPlanAuthority.TryTakeSpiritualPublication(context.FileSystem, lease, oldAuthority!, out _));
                Assert.False(AcceptedMechanicsPlanAuthority.TryTakeSpiritualPublication(context.FileSystem, lease, retryAuthority!, out _));
                Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease)); Assert.False(oldCapture.IsCurrentOwner); Assert.False(retryCapture.IsCurrentOwner);
                await Assert.ThrowsAsync<ObjectDisposedException>(async () => await oldCapture.ReadExistingPressureOrdinaryProbeStateAsync(lease)); AssertPressureNoPrivatePair(context, lease);
            });
            Assert.Null(CleanupPublicationCut.ReadOptional(witness.JournalPath));
            WritePressureOwnerTiming("guardian_zero_fresh_retry_common_c4_signed47", setup.Name, started);
        }, hooks: hooks, observeSignedOrigin: async (context, lease) => { signedOrigin = await ReadGuardianZeroSignedOriginAsync(context, lease); });
    }

    /// <summary>Captures the genuine signed physical origin before any ordinary/output authoring, without player-specific assumptions.</summary>
    /// <param name="context">The real independent guardian fixture.</param>
    /// <param name="lease">Its current original authoring lease.</param>
    /// <returns>All signed session bytes, tracked absences and durable generation.</returns>
    private static async Task<PressureGroupSignedOrigin> ReadGuardianZeroSignedOriginAsync(ResourceMaterializationTestContext context,
        FileSystemManager.CanonicalWriteLease lease)
    {
        var files = CaptureExactSpiritualFiles(context.FileSystem.GameSessionPath); var tracked = new Dictionary<string, byte[]?>(StringComparer.Ordinal);
        foreach (var path in CanonicalStateNormalizer.NormalizerRollbackTrackedFiles.Concat(new[] { SpiritualWoundCaptureCheckpointState.StatePath,
            SpiritualWoundDecisionPendingState.StatePath, AcceptedMechanicsPlan.WoundCommandPath, SpiritualWoundOpportunityReceiptState.StatePath,
            ProjectionNarrativePath, ProjectionInterfacePath }).Distinct(StringComparer.Ordinal)) tracked[path] = await context.FileSystem.ReadFileBytesAsync(lease, path);
        Assert.Empty(ReadGuardianAttributionJson(tracked[AfterlifeSpiritualConflictState.StatePath]!)["activeConflict"]!["exchangeLog"]!.AsArray()); AssertPressureNoPrivatePair(context, lease);
        return new(files, tracked, File.ReadAllBytes(context.FileSystem.SessionGenerationPath),
            ReadGuardianAttributionJson(tracked[SpiritualWoundOpportunityReceiptState.StatePath]!).ToJsonString(), ReadGuardianAttributionJson(tracked[EffectIdentityState.StatePath]!).ToJsonString());
    }

    /// <summary>Checks exact persistent signed bytes/absences and durable generation, without restoring an old validation epoch.</summary>
    /// <param name="context">The real fixture after complete recovery.</param>
    /// <param name="origin">Independently captured pre-authoring physical origin.</param>
    private static void AssertGuardianZeroSignedOrigin(ResourceMaterializationTestContext context, PressureGroupSignedOrigin origin)
    {
        var current = CaptureExactSpiritualFiles(context.FileSystem.GameSessionPath); Assert.Equal(origin.Files.Keys.Order(StringComparer.Ordinal), current.Keys.Order(StringComparer.Ordinal));
        foreach (var row in origin.Files) Assert.True(row.Value.SequenceEqual(current[row.Key]), row.Key);
        foreach (var row in origin.Tracked) Assert.Equal(row.Value, CleanupPublicationCut.ReadOptional(context.FileSystem.ResolvePath(row.Key)));
        Assert.Equal(origin.Generation, File.ReadAllBytes(context.FileSystem.SessionGenerationPath));
    }

    /// <summary>Proves only identical original authored inputs changed after recovery, retaining signature/snapshot and durable generation.</summary>
    /// <param name="context">The real restored fixture.</param>
    /// <param name="setup">The original same-Name byte inventory.</param>
    /// <param name="origin">Complete signed origin.</param>
    private static void AssertGuardianZeroOnlyAuthored(ResourceMaterializationTestContext context, GuardianZeroPressureSetup setup, PressureGroupSignedOrigin origin)
    {
        var expected = origin.Files.ToDictionary(row => row.Key, row => row.Value.ToArray(), StringComparer.Ordinal);
        foreach (var row in setup.Authored)
        { var path = row.Key.Replace('/', Path.DirectorySeparatorChar); if (row.Value is null) expected.Remove(path); else expected[path] = row.Value; }
        var current = CaptureExactSpiritualFiles(context.FileSystem.GameSessionPath); Assert.Equal(expected.Keys.Order(StringComparer.Ordinal), current.Keys.Order(StringComparer.Ordinal));
        foreach (var row in expected) Assert.True(row.Value.SequenceEqual(current[row.Key]), row.Key);
        Assert.Equal(origin.Generation, File.ReadAllBytes(context.FileSystem.SessionGenerationPath));
    }

    /// <summary>Retains exact derived bindings and slot values outside the comparable semantic envelope.</summary>
    /// <param name="phase">External first/retry diagnostic label.</param>
    /// <param name="name">One fixed fixture Name, unchanged between attempts.</param>
    /// <param name="attempt">Complete validated immutable DATA.</param>
    /// <param name="probes">Actual completed probe count, when probes ran.</param>
    private void WriteGuardianZeroRetryMap(string phase, string name, GuardianZeroRetryAttemptData attempt, int? probes) =>
        _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase, name, opaqueIds = attempt.Allocations.Length, mappedScalarPositions = attempt.Bindings.Count,
            allocations = attempt.Allocations, bindings = attempt.Bindings.Select(row => new { row.Key.Outer, row.Key.Inner, row.Value }).ToArray(), detachedProbes = probes }));
}
