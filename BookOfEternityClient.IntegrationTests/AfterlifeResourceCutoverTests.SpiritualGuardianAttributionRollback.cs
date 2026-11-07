using System.Diagnostics;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>Witnesses late v5 publication, exact signed-origin rollback and old-owner refusal, then one fresh retry with identical raw bytes through signed-next.</summary>
    [Fact]
    public Task SpiritualGuardianAttribution_LatePublicationCutRestoresSignedOriginAndFreshRetryPublishesOnce() =>
        AssertGuardianAttributionLatePublicationCutAsync("guardian_attribution_rollback");

    /// <summary>Runs the same witnessed late-cut oracle for a pre-sign singleton role/rank, never inferred from issued DATA.</summary>
    /// <param name="name">Independent fixture identity.</param>
    /// <param name="retainedRank">Explicit expected III/IV before creation/signing.</param>
    /// <param name="role">One of the two existing singleton lanes.</param>
    private async Task AssertGuardianAttributionLatePublicationCutAsync(string name, int retainedRank = 3,
        PressureSingletonJourneyRole role = PressureSingletonJourneyRole.DeclaredGuardian)
    {
        var armed = false;
        var cutEnabled = true;
        var cuts = 0;
        CurrentCommittedPublicationWitness? witness = null;
        var injected = new IOException(name + " singleton C4 receipt read-back failure.");
        var hooks = new FileSystemManagerHooks
        {
            LocalPublicationObserver = (phase, index) => { if (armed) witness!.Observe(phase, index); },
            BeforeCanonicalReadOpenAsync = path =>
            {
                if (armed && cutEnabled && path == SpiritualWoundOpportunityReceiptState.StatePath &&
                    witness!.Has(AfterlifeSpiritualConflictState.StatePath) &&
                    witness.Has(ResourceMaterializationContract.StatePath) &&
                    witness.Has(SpiritualWoundOpportunityReceiptState.StatePath))
                {
                    witness.RequireSingleCurrent(AfterlifeSpiritualConflictState.StatePath);
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
        async Task RunAsync(GuardianAttributionJourney setup)
        {
            var context = setup.Copy.Context;
            ValidationService.SpiritualOriginalTurnCapture? retiredWarm = null;
            ValidationService.SpiritualC2PrivateSession? retiredChoice = null;
            ValidationService.SpiritualC2PrivateSession? retiredCompleted = null;
            var first = await CompleteGuardianAttributionColdC3Async(setup, context.Validator,
                (warm, choice, completed) =>
                {
                    // Retain only disposed handles to demonstrate they cannot revive after rollback/retry.
                    retiredWarm = warm;
                    retiredChoice = choice;
                    retiredCompleted = completed;
                });
            RequireGuardianJourneyIssues(setup.Name, "rollback_first_common_plan", await context.Validator.ValidateAcceptedTurnRawResourceMaterializationAsync());
            var firstPlan = await PeekPlanAsync(context);
            AssertGuardianAttributionRegisteredPlan(setup, first, firstPlan);
            ValidationService.SpiritualOriginalTurnCapture.SpiritualC4PublicationAuthority oldAuthority = null!;
            await ExecuteGuardianLeaseStagesAsync(context, async lease =>
            {
                Assert.True(AcceptedMechanicsPlanAuthority.TryPeekSpiritualPublication(context.FileSystem, lease, out oldAuthority));
                RequireGuardianJourneyIssues(setup.Name, "rollback_old_authority_current", await oldAuthority.ValidateCurrentInputsAsync(context.FileSystem, lease));
            });
            var backups = await ReadSpiritualC4BackupsAsync(context);
            witness = new(context.FileSystem, AfterlifeSpiritualConflictState.StatePath,
                ResourceMaterializationContract.StatePath, SpiritualWoundOpportunityReceiptState.StatePath);
            var failedWitness = witness;
            var failureStarted = Stopwatch.GetTimestamp();
            Exception? failure;
            armed = true;
            try
            {
                failure = await Record.ExceptionAsync(() => AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(
                    context.FileSystem, context.Normalizer, context.Validator, backups));
            }
            finally { armed = false; }
            Assert.Equal(1, cuts);
            Assert.Same(injected, failure);
            foreach (var path in new[] { AfterlifeSpiritualConflictState.StatePath,
                         ResourceMaterializationContract.StatePath, SpiritualWoundOpportunityReceiptState.StatePath })
                Assert.True(failedWitness.Has(path), path);
            failedWitness.AssertRestored(name + "-signed46-late-cut",
                setup.Origin.Tracked.ToDictionary(row => context.FileSystem.ResolvePath(row.Key), row => row.Value,
                    StringComparer.Ordinal), failure, _storageOutput.WriteLine);
            AssertPressurePairSignedOrigin(context, setup.Origin);
            await ExecuteGuardianLeaseStagesAsync(context, async lease =>
            {
                Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
                Assert.False(AcceptedMechanicsPlanAuthority.TryPeekSpiritualPublication(context.FileSystem, lease, out _));
                Assert.False(AcceptedMechanicsPlanAuthority.TryTakeSpiritualPublication(context.FileSystem, lease, oldAuthority, out _));
                AssertPressureNoPrivatePair(context, lease);
                await AssertPressurePairRetiredOwnersAsync(retiredWarm!, retiredChoice!, retiredCompleted!, lease);
                ValidationService.SpiritualC2PrivateSession? unexpectedSession = null;
                await ExecuteGuardianColdSessionStagesAsync(async () =>
                {
                    var reopened = await new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance)
                        .OpenC2PrivateSessionAsync(lease);
                    unexpectedSession = reopened.Session;
                    _storageOutput.WriteLine(System.Text.Json.JsonSerializer.Serialize(new
                    {
                        phase = "guardian_rollback_blocked_cold_reopen", reopened.Disposition,
                        issues = reopened.Issues.Select(diagnosticIssue => new
                        {
                            diagnosticIssue.FilePath, diagnosticIssue.Severity, diagnosticIssue.Message, diagnosticIssue.Category,
                            diagnosticIssue.Code, diagnosticIssue.Actor, diagnosticIssue.Section, diagnosticIssue.Expected,
                            diagnosticIssue.Actual, diagnosticIssue.RepairHint, diagnosticIssue.RepairTargetFiles
                        })
                    }));
                    Assert.Equal("blocked", reopened.Disposition);
                    Assert.Null(reopened.Session);
                    Assert.Equal("spiritual_c2_private_checkpoint_required", Assert.Single(reopened.Issues).Code);
                }, () => unexpectedSession);
            });
            AssertPressurePairSignedOrigin(context, setup.Origin);
            WritePressureOwnerTiming("guardian_attribution_late_cut_signed46_rollback", setup.Name, failureStarted);

            // Reauthor exactly the retained ordinary/output bytes. Never sign again or restore a failed pair/owner.
            await ExecuteGuardianLeaseStagesAsync(context, async lease =>
            {
                foreach (var (path, bytes) in setup.Authored)
                {
                    Assert.Contains(path, PressureAuthoredDraftPaths);
                    if (bytes is null) Assert.Null(await context.FileSystem.ReadFileBytesAsync(lease, path));
                    else await context.FileSystem.WriteFileAtomicBytesAsync(lease, path, bytes);
                }
                foreach (var (path, bytes) in setup.Authored)
                    Assert.Equal(bytes, await context.FileSystem.ReadFileBytesAsync(lease, path));
                Assert.False(AcceptedMechanicsPlanAuthority.TryTakeSpiritualPublication(context.FileSystem, lease, oldAuthority, out _));
                AssertPressureNoPrivatePair(context, lease);
            });
            AssertGuardianAttributionOnlyReauthoredInputs(context, setup);
            var retryStarted = Stopwatch.GetTimestamp();
            var retryValidator = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);
            RequireGuardianJourneyIssues(setup.Name, "retry_location_preflight", await retryValidator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync());
            RequireGuardianJourneyIssues(setup.Name, "retry_item_preflight", await retryValidator.ValidateAcceptedTurnRawMortalItemMaterializationAsync());
            var retry = await CompleteGuardianAttributionColdC3Async(setup, retryValidator, (warm, choice, completed) =>
            {
                Assert.NotSame(retiredWarm, warm); Assert.NotSame(retiredChoice, choice); Assert.NotSame(retiredCompleted, completed);
            });
            Assert.Equal(first.ActualRootJson, retry.ActualRootJson);
            RequireGuardianJourneyIssues(setup.Name, "rollback_retry_common_plan", await retryValidator.ValidateAcceptedTurnRawResourceMaterializationAsync());
            var retryPlan = await PeekPlanAsync(context);
            AssertGuardianAttributionRegisteredPlan(setup, retry, retryPlan);
            ValidationService.SpiritualOriginalTurnCapture.SpiritualC4PublicationAuthority retryAuthority = null!;
            await ExecuteGuardianLeaseStagesAsync(context, async lease =>
            {
                Assert.True(AcceptedMechanicsPlanAuthority.TryPeekSpiritualPublication(context.FileSystem, lease, out retryAuthority));
                Assert.NotSame(oldAuthority, retryAuthority);
                Assert.False(AcceptedMechanicsPlanAuthority.TryTakeSpiritualPublication(context.FileSystem, lease, oldAuthority, out _));
                await AssertPressurePairRetiredOwnersAsync(retiredWarm!, retiredChoice!, retiredCompleted!, lease);
            });
            witness = new(context.FileSystem, AfterlifeSpiritualConflictState.StatePath,
                ResourceMaterializationContract.StatePath, SpiritualWoundOpportunityReceiptState.StatePath);
            armed = true;
            try
            {
                var published = await AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(
                    context.FileSystem, context.Normalizer, retryValidator, await ReadSpiritualC4BackupsAsync(context));
                RequireGuardianJourneyIssues(setup.Name, "rollback_retry_common_c4", published.Issues);
                Assert.Same(retryPlan, published.MechanicsPlan);
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
                await AssertPressurePairRetiredOwnersAsync(retiredWarm!, retiredChoice!, retiredCompleted!, lease);
            });
            WritePressureOwnerTiming("guardian_attribution_fresh_retry_common_c4", setup.Name, retryStarted);
            await ReadGuardianAttributionSignedNextAsync(setup, retryValidator);
        }
        if (role == PressureSingletonJourneyRole.Player)
        {
            Assert.Equal(4, retainedRank);
            await WithSingletonIvPlayerJourneyAsync(name, RunAsync, hooks);
        }
        else
        {
            Assert.Equal(PressureSingletonJourneyRole.DeclaredGuardian, role);
            await WithGuardianAttributionJourneyAsync(name, RunAsync, hooks, retainedRank);
        }
    }

    /// <summary>Proves reauthoring changed only the original ordinary/output DATA, keeping signature and snapshot files byte-exact.</summary>
    private static void AssertGuardianAttributionOnlyReauthoredInputs(ResourceMaterializationTestContext context, GuardianAttributionJourney setup)
    {
        var expected = setup.Origin.Files.ToDictionary(row => row.Key, row => row.Value.ToArray(), StringComparer.Ordinal);
        foreach (var (path, bytes) in setup.Authored)
        {
            var relative = path.Replace('/', Path.DirectorySeparatorChar);
            if (bytes is null) expected.Remove(relative);
            else expected[relative] = bytes;
        }
        var current = CaptureExactSpiritualFiles(context.FileSystem.GameSessionPath);
        Assert.Equal(expected.Keys.OrderBy(path => path, StringComparer.Ordinal), current.Keys.OrderBy(path => path, StringComparer.Ordinal));
        foreach (var (path, bytes) in expected) Assert.True(bytes.SequenceEqual(current[path]), path);
        Assert.Equal(setup.Origin.Generation, File.ReadAllBytes(context.FileSystem.SessionGenerationPath));
    }
}
