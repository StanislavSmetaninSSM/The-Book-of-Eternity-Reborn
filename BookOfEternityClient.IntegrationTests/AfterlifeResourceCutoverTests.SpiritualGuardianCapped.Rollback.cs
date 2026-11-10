using System.Diagnostics;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>One mapped positive capped IV late cut restores signed46, then fresh identical-input owners publish exactly once through signed47.</summary>
    [Fact]
    public Task SpiritualGuardianCapped_LateCutRestoresAndFreshRetryPublishesOnce()
    {
        const string name = "guardian_capped_parent_iv_late_retry";
        var armed = false; var cutEnabled = true; var cuts = 0;
        CurrentCommittedPublicationWitness? witness = null;
        var injected = new IOException("Guardian capped positive receipt committed read-back failure.");
        var hooks = new FileSystemManagerHooks
        {
            LocalPublicationObserver = (phase, index) => { if (armed) witness!.Observe(phase, index); },
            BeforeCanonicalReadOpenAsync = path =>
            {
                if (armed && cutEnabled && path == SpiritualWoundOpportunityReceiptState.StatePath &&
                    witness!.Has(AfterlifeSpiritualConflictState.StatePath) && witness.Has(ResourceMaterializationContract.StatePath) &&
                    witness.Has(SpiritualWoundOpportunityReceiptState.StatePath))
                {
                    witness.RequireSingleCurrent(AfterlifeSpiritualConflictState.StatePath);
                    witness.RequireSingleCurrent(ResourceMaterializationContract.StatePath);
                    witness.RequireSingleCurrent(SpiritualWoundOpportunityReceiptState.StatePath);
                    cuts++; cutEnabled = false; armed = false; throw injected;
                }
                return Task.CompletedTask;
            }
        };
        return WithSingletonIvJourneyFailureAsync(name, () => WithGuardianAttributionJourneyAsync(name, async setup =>
        {
            var context = setup.Copy.Context;
            Assert.Equal(4, setup.ExpectedRank);
            Assert.Equal(GuardianSingletonPressureContour.CappedBroken, setup.GuardianContour);
            ValidationService.SpiritualOriginalTurnCapture? retiredWarm = null;
            ValidationService.SpiritualC2PrivateSession? retiredChoice = null;
            ValidationService.SpiritualC2PrivateSession? retiredCompleted = null;
            var firstCapture = new GuardianCappedAttemptCapture();
            var first = await CompleteGuardianAttributionColdC3Async(setup, context.Validator,
                (warm, choice, completed) => { retiredWarm = warm; retiredChoice = choice; retiredCompleted = completed; }, firstCapture);
            Assert.NotNull(retiredWarm); Assert.NotNull(retiredChoice); Assert.NotNull(retiredCompleted);
            Assert.False(retiredWarm!.IsCurrentOwner);
            RequireGuardianJourneyIssues(name, "capped_rollback_first_common_plan", await context.Validator.ValidateAcceptedTurnRawResourceMaterializationAsync());
            var firstPlan = await PeekPlanAsync(context);
            AssertGuardianAttributionRegisteredPlan(setup, first, firstPlan);
            ValidationService.SpiritualOriginalTurnCapture.SpiritualC4PublicationAuthority oldAuthority = null!;
            await ExecuteGuardianLeaseStagesAsync(context, async lease =>
            {
                Assert.True(AcceptedMechanicsPlanAuthority.TryPeekSpiritualPublication(context.FileSystem, lease, out oldAuthority));
                Assert.Same(firstPlan, oldAuthority.Plan);
                RequireGuardianJourneyIssues(name, "capped_rollback_old_authority_current", await oldAuthority.ValidateCurrentInputsAsync(context.FileSystem, lease));
                await CaptureGuardianCappedPreparedAsync(setup, firstCapture, firstPlan, lease);
            });
            Assert.False(armed); Assert.Equal(0, cuts);
            var readStarted = Stopwatch.GetTimestamp();
            var firstAttempt = ReadGuardianCappedRetryAttempt(setup, Assert.IsType<string>(firstCapture.PreparedJson));
            WritePressureOwnerTiming("guardian_capped_first_retry_reader", name, readStarted);
            var probesStarted = Stopwatch.GetTimestamp();
            var probes = AssertGuardianCappedRetryProbes(setup, firstAttempt, _storageOutput.WriteLine);
            WritePressureOwnerTiming("guardian_capped_first_detached_retry_probes", name, probesStarted);
            WriteGuardianCappedRetryMap("guardian_capped_first_attempt_verified_before_late_hook", name, firstAttempt, probes);
            Assert.False(armed); Assert.Equal(0, cuts);
            await ExecuteGuardianLeaseStagesAsync(context, async lease =>
            {
                Assert.True(AcceptedMechanicsPlanAuthority.TryPeekSpiritualPublication(context.FileSystem, lease, out var current));
                Assert.Same(oldAuthority, current);
                Assert.Empty(await current.ValidateCurrentInputsAsync(context.FileSystem, lease));
            });
            var backups = await ReadSpiritualC4BackupsAsync(context);
            witness = new(context.FileSystem, AfterlifeSpiritualConflictState.StatePath,
                ResourceMaterializationContract.StatePath, SpiritualWoundOpportunityReceiptState.StatePath);
            var failedWitness = witness;
            var epoch = context.FileSystem.CanonicalRootAuthorityIdentity.ValidationEpoch;
            Assert.True(epoch.TryCapture(out var epochBefore)); var activityBefore = epoch.CaptureActivity(); Assert.Equal(0, activityBefore.Active);
            var failureStarted = Stopwatch.GetTimestamp(); Exception? failure;
            armed = true;
            try
            {
                failure = await Record.ExceptionAsync(() => AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(
                    context.FileSystem, context.Normalizer, context.Validator, backups));
            }
            finally { armed = false; }
            Assert.Equal(1, cuts); Assert.Same(injected, failure);
            foreach (var path in new[] { AfterlifeSpiritualConflictState.StatePath, ResourceMaterializationContract.StatePath,
                SpiritualWoundOpportunityReceiptState.StatePath }) Assert.True(failedWitness.Has(path));
            failedWitness.AssertRestored(name + "-signed46-late-cut", setup.Origin.Tracked.ToDictionary(
                row => context.FileSystem.ResolvePath(row.Key), row => row.Value, StringComparer.Ordinal), failure, _storageOutput.WriteLine);
            AssertPressureGroupSignedOrigin(context, setup.Origin);
            Assert.True(epoch.TryCapture(out var epochAfter)); Assert.NotSame(epochBefore, epochAfter);
            var activityAfter = epoch.CaptureActivity(); Assert.Equal(0, activityAfter.Active);
            Assert.True(activityAfter.Begins > activityBefore.Begins);
            Assert.Equal(activityAfter.Begins - activityBefore.Begins, activityAfter.Ends - activityBefore.Ends);
            _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_capped_late_cut_durable_bytes_and_fresh_epoch", name,
                epochChanged = !ReferenceEquals(epochBefore, epochAfter), mutationBegins = activityAfter.Begins - activityBefore.Begins,
                mutationEnds = activityAfter.Ends - activityBefore.Ends, activeMutations = activityAfter.Active,
                durableGenerationBase64 = Convert.ToBase64String(setup.Origin.Generation), privateJournalAbsent = !File.Exists(failedWitness.JournalPath) }));
            await ExecuteGuardianLeaseStagesAsync(context, async lease =>
            {
                Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
                Assert.False(AcceptedMechanicsPlanAuthority.TryPeekSpiritualPublication(context.FileSystem, lease, out _));
                Assert.False(AcceptedMechanicsPlanAuthority.TryTakeSpiritualPublication(context.FileSystem, lease, oldAuthority, out _));
                AssertPressureNoPrivatePair(context, lease);
                await AssertPressureGroupRetiredOwnersAsync(retiredWarm!, retiredChoice!, retiredCompleted!, lease);
                ValidationService.SpiritualC2PrivateSession? unexpectedSession = null;
                await ExecuteGuardianColdSessionStagesAsync(async () =>
                {
                    var reopened = await new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance).OpenC2PrivateSessionAsync(lease);
                    unexpectedSession = reopened.Session;
                    _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_capped_rollback_blocked_cold_reopen", reopened.Disposition,
                        Issues = ReadGuardianCappedIssues(reopened.Issues) }));
                    Assert.Equal("blocked", reopened.Disposition); Assert.Null(reopened.Session);
                    Assert.Equal("spiritual_c2_private_checkpoint_required", Assert.Single(reopened.Issues).Code);
                }, () => unexpectedSession);
            });
            AssertPressureGroupSignedOrigin(context, setup.Origin);
            WritePressureOwnerTiming("guardian_capped_late_cut_signed46_restore", name, failureStarted);
            await ExecuteGuardianLeaseStagesAsync(context, async lease =>
            {
                foreach (var (path, bytes) in setup.Authored)
                {
                    Assert.Contains(path, PressureAuthoredDraftPaths);
                    if (bytes is null) Assert.Null(await context.FileSystem.ReadFileBytesAsync(lease, path));
                    else await context.FileSystem.WriteFileAtomicBytesAsync(lease, path, bytes);
                }
                foreach (var (path, bytes) in setup.Authored) Assert.Equal(bytes, await context.FileSystem.ReadFileBytesAsync(lease, path));
                Assert.False(AcceptedMechanicsPlanAuthority.TryTakeSpiritualPublication(context.FileSystem, lease, oldAuthority, out _));
                AssertPressureNoPrivatePair(context, lease);
            });
            AssertGuardianAttributionOnlyReauthoredInputs(context, setup);
            var retryStarted = Stopwatch.GetTimestamp();
            var retryValidator = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);
            Assert.NotSame(context.Validator, retryValidator);
            RequireGuardianJourneyIssues(name, "capped_retry_location_preflight", await retryValidator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync());
            RequireGuardianJourneyIssues(name, "capped_retry_item_preflight", await retryValidator.ValidateAcceptedTurnRawMortalItemMaterializationAsync());
            ValidationService.SpiritualOriginalTurnCapture? retryWarm = null;
            ValidationService.SpiritualC2PrivateSession? retryChoice = null;
            ValidationService.SpiritualC2PrivateSession? retryCompleted = null;
            var retryCapture = new GuardianCappedAttemptCapture();
            var retry = await CompleteGuardianAttributionColdC3Async(setup, retryValidator, (warm, choice, completed) =>
            {
                retryWarm = warm; retryChoice = choice; retryCompleted = completed;
                Assert.NotSame(retiredWarm, warm); Assert.NotSame(retiredChoice, choice); Assert.NotSame(retiredCompleted, completed);
            }, retryCapture);
            Assert.NotNull(retryWarm); Assert.NotNull(retryChoice); Assert.NotNull(retryCompleted); Assert.False(retryWarm!.IsCurrentOwner);
            Assert.Equal(first.ActualRootJson, retry.ActualRootJson);
            RequireGuardianJourneyIssues(name, "capped_rollback_retry_common_plan", await retryValidator.ValidateAcceptedTurnRawResourceMaterializationAsync());
            var retryPlan = await PeekPlanAsync(context);
            AssertGuardianAttributionRegisteredPlan(setup, retry, retryPlan);
            ValidationService.SpiritualOriginalTurnCapture.SpiritualC4PublicationAuthority retryAuthority = null!;
            await ExecuteGuardianLeaseStagesAsync(context, async lease =>
            {
                Assert.True(AcceptedMechanicsPlanAuthority.TryPeekSpiritualPublication(context.FileSystem, lease, out retryAuthority));
                Assert.NotSame(oldAuthority, retryAuthority); Assert.Same(retryPlan, retryAuthority.Plan);
                await CaptureGuardianCappedPreparedAsync(setup, retryCapture, retryPlan, lease);
            });
            Assert.False(armed); readStarted = Stopwatch.GetTimestamp();
            var retryAttempt = ReadGuardianCappedRetryAttempt(setup, Assert.IsType<string>(retryCapture.PreparedJson), firstAttempt);
            WritePressureOwnerTiming("guardian_capped_fresh_retry_reader", name, readStarted);
            WriteGuardianCappedRetryMap("guardian_capped_fresh_attempt_verified_before_publication", name, retryAttempt, null);
            var compareStarted = Stopwatch.GetTimestamp();
            AssertGuardianCappedFreshRetryEquivalent(setup, firstAttempt, retryAttempt);
            WritePressureOwnerTiming("guardian_capped_complete_fresh_retry_equivalence", name, compareStarted);
            await ExecuteGuardianLeaseStagesAsync(context, async lease =>
            {
                Assert.True(AcceptedMechanicsPlanAuthority.TryPeekSpiritualPublication(context.FileSystem, lease, out var current));
                Assert.Same(retryAuthority, current); Assert.Empty(await retryAuthority.ValidateCurrentInputsAsync(context.FileSystem, lease));
                Assert.False(AcceptedMechanicsPlanAuthority.TryTakeSpiritualPublication(context.FileSystem, lease, oldAuthority, out _));
                Assert.True(AcceptedMechanicsPlanAuthority.TryPeekSpiritualPublication(context.FileSystem, lease, out var stillCurrent));
                Assert.Same(retryAuthority, stillCurrent); Assert.Empty(await retryAuthority.ValidateCurrentInputsAsync(context.FileSystem, lease));
                await AssertPressureGroupRetiredOwnersAsync(retiredWarm!, retiredChoice!, retiredCompleted!, lease);
            });
            witness = new(context.FileSystem, AfterlifeSpiritualConflictState.StatePath,
                ResourceMaterializationContract.StatePath, SpiritualWoundOpportunityReceiptState.StatePath);
            armed = true;
            try
            {
                var published = await AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(
                    context.FileSystem, context.Normalizer, retryValidator, await ReadSpiritualC4BackupsAsync(context));
                RequireGuardianJourneyIssues(name, "capped_rollback_retry_common_c4", published.Issues); Assert.Same(retryPlan, published.MechanicsPlan);
            }
            finally { armed = false; }
            Assert.Equal(1, cuts);
            witness.RequireSingleCurrent(AfterlifeSpiritualConflictState.StatePath);
            witness.RequireSingleCurrent(ResourceMaterializationContract.StatePath);
            witness.RequireSingleCurrent(SpiritualWoundOpportunityReceiptState.StatePath);
            await AssertGuardianAttributionPublishedAsync(setup, retry, retryPlan);
            await ExecuteGuardianLeaseStagesAsync(context, async lease =>
            {
                Assert.False(AcceptedMechanicsPlanAuthority.TryTakeSpiritualPublication(context.FileSystem, lease, oldAuthority, out _));
                Assert.False(AcceptedMechanicsPlanAuthority.TryTakeSpiritualPublication(context.FileSystem, lease, retryAuthority, out _));
                Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease)); AssertPressureNoPrivatePair(context, lease);
                await AssertPressureGroupRetiredOwnersAsync(retiredWarm!, retiredChoice!, retiredCompleted!, lease);
                await AssertPressureGroupRetiredOwnersAsync(retryWarm!, retryChoice!, retryCompleted!, lease);
            });
            Assert.Null(CleanupPublicationCut.ReadOptional(witness.JournalPath));
            await ReadGuardianAttributionSignedNextAsync(setup, retryValidator);
            WritePressureOwnerTiming("guardian_capped_fresh_retry_common_c4_signed47", name, retryStarted);
        }, hooks: hooks, retainedRank: 4, contour: GuardianSingletonPressureContour.CappedBroken));
    }
}
