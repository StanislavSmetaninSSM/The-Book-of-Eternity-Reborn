using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalWoundRecoveryTests(Xunit.Abstractions.ITestOutputHelper output)
{
    /// <summary>
    /// Restores every canonical byte after a later recovery write fails, then
    /// retries the same recovery resolution and replays its receipt after cold reopen.
    /// </summary>
    [Fact]
    public void Publication_LaterWriteFailureRollsBackAndExactRetryHeals()
    {
        var scenario = Scenario.AtDueBoundary();
        var authored = scenario.Wound.DeepClone().AsObject();
        authored["recovery"]!["currentStepThreshold"] = 1;
        scenario = scenario with { Wound = authored };
        byte[]? originalPlayer = null;
        var armed = false;
        var fired = false;
        var observedEarlierWrite = false;
        CurrentCommittedPublicationWitness? witness = null;
        var injected = new IOException("Injected recovery history write failure after carrier publication.");
        var failureObservedBeforeReadmission = false;
        var hooks = new FileSystemManagerHooks
        {
            LocalPublicationObserver = (phase, index) =>
            {
                if (armed) witness!.Observe(phase, index);
            },
            BeforeCanonicalMutationAsync = path =>
            {
                if (armed && path == WoundHistoryState.HistoryPath)
                {
                    var actualPlayer = witness!.RequireSingleCurrent(WoundCarrierCatalog.PlayerPath);
                    observedEarlierWrite = !originalPlayer!.AsSpan().SequenceEqual(actualPlayer);
                    fired = true;
                    armed = false;
                    throw injected;
                }
                return Task.CompletedTask;
            }
        };
        var fixture = Fixture.Create(scenario, hooks);
        using var owned = new OriginalFixtureCompletion(fixture.FileSystem.BasePath, fixture.Dispose, output.WriteLine);
        fixture.PrepareCanonicalRefreshItemAuthority();
        fixture.ReadmitCurrentRecoveryBinding();
        var before = fixture.CaptureCanonicalTreeBytes();
        originalPlayer = File.ReadAllBytes(fixture.FileSystem.ResolvePath(WoundCarrierCatalog.PlayerPath));
        var resolution = Assert.IsType<MortalWoundRecoveryResolution>(
            Required(InvokePlan(fixture), "Resolution"));
        var composed = MortalWoundRecoveryAcceptedPlanComposer.Compose(
            fixture.FileSystem, fixture.Lease, fixture.Binding, resolution);
        Assert.Equal(MortalWoundRecoveryAcceptedPlanCompositionDisposition.Composed, composed.Disposition);
        Assert.Empty(composed.Issues);
        Assert.NotNull(composed.AcceptedPlan);
        Assert.NotNull(composed.Receipt);
        fixture.AssertCanonicalTreeBytesUnchanged(before);

        witness = new(fixture.FileSystem, WoundCarrierCatalog.PlayerPath);
        armed = true;
        var failure = Assert.Throws<CanonicalStateWriteException>(() => fixture.PublishWithCanonicalRefresh(error =>
        {
            witness.AssertRestored("wound-recovery-original-committed-rollback",
                before.ToDictionary(pair => Path.Combine(fixture.FileSystem.BasePath, pair.Key), pair => (byte[]?)pair.Value,
                    StringComparer.Ordinal), error, output.WriteLine);
            fixture.AssertCanonicalTreeBytesUnchanged(before);
            failureObservedBeforeReadmission = true;
        }));
        Assert.True(failureObservedBeforeReadmission);
        Assert.Same(injected, failure.InnerException);
        Assert.Equal(WoundHistoryState.HistoryPath, failure.RelativePath);
        Assert.True(fired);
        Assert.True(observedEarlierWrite);
        fixture.AssertCanonicalTreeBytesUnchanged(before);
        fixture.AssertCarrierIdentityHistoryAgreement();

        fixture.PrepareCanonicalRefreshItemAuthority();
        fixture.ReadmitCurrentRecoveryBinding();
        var retry = MortalWoundRecoveryAcceptedPlanComposer.Compose(
            fixture.FileSystem, fixture.Lease, fixture.Binding, resolution);
        Assert.Equal(MortalWoundRecoveryAcceptedPlanCompositionDisposition.Composed, retry.Disposition);
        Assert.Empty(retry.Issues);
        AssertReceiptEqual(composed.Receipt, Assert.IsType<MortalWoundRecoveryReceipt>(retry.Receipt));
        var published = fixture.PublishWithCanonicalRefresh();
        Assert.DoesNotContain(published.Issues, issue => issue.Severity == IssueSeverity.Error);
        Assert.Same(retry.AcceptedPlan, published.MechanicsPlan);
        Assert.Equal("healed", ReadActiveOrTerminalRecoveryWound(
            fixture.FileSystem, fixture.WoundId)["lifecycle"]!.GetValue<string>());
        fixture.AssertCarrierIdentityHistoryAgreement();
        fixture.PrepareFreshContinuationTurn(45, "rollback_retry_same_minute");
        fixture.RestartForReplay();
        var replayBefore = fixture.CaptureCanonicalTreeBytes();
        AssertExactReplay(InvokePlan(fixture), composed.Receipt);
        fixture.AssertCanonicalTreeBytesUnchanged(replayBefore);
    }

    private sealed partial class Fixture
    {
        /// <summary>
        /// Runs the existing raw item owner before the complete canonical refresh,
        /// retaining its genuine accepted allocation map even when no item is created.
        /// </summary>
        internal void PrepareCanonicalRefreshItemAuthority()
        {
            var issues = new ValidationService(FileSystem, NullLogger<ValidationService>.Instance)
                .ValidateAcceptedTurnRawMortalItemMaterializationAsync(Lease)
                .GetAwaiter().GetResult();
            Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);
        }

        /// <summary>
        /// Runs the existing outer canonical refresh coordinator that owns exact
        /// compensation when common publication or its canonical validation fails.
        /// </summary>
        /// <returns>
        /// The refreshed common plan and canonical diagnostics on success.
        /// </returns>
        internal AcceptedTurnCanonicalStateRefresh.Result PublishWithCanonicalRefresh(Action<Exception>? beforeReadmission = null)
        {
            Lease.DisposeAsync().AsTask().GetAwaiter().GetResult();
            try
            {
                return AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(
                    FileSystem,
                    new CanonicalStateNormalizer(FileSystem, NullLogger<CanonicalStateNormalizer>.Instance),
                    new ValidationService(FileSystem, NullLogger<ValidationService>.Instance),
                    new Dictionary<string, string>(StringComparer.Ordinal))
                    .GetAwaiter().GetResult();
            }
            catch (Exception failure)
            {
                beforeReadmission?.Invoke(failure);
                throw;
            }
            finally
            {
                Lease = FileSystem.AcquireCanonicalWriteLeaseAsync().GetAwaiter().GetResult();
            }
        }

        /// <summary>
        /// Authenticates the restored source against its unchanged signed turn after rollback.
        /// </summary>
        internal void ReadmitCurrentRecoveryBinding() =>
            Binding = ExportRecoveryBinding(FileSystem, Lease, WoundId);
    }
}
