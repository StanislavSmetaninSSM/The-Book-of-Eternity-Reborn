using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalWoundRecoveryTests
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
        Fixture? current = null;
        byte[]? originalPlayer = null;
        var armed = false;
        var fired = false;
        var observedEarlierWrite = false;
        var hooks = new FileSystemManagerHooks
        {
            BeforeCanonicalMutationAsync = path =>
            {
                if (armed && path == WoundHistoryState.HistoryPath)
                {
                    var actualPlayer = File.ReadAllBytes(current!.FileSystem.ResolvePath(
                        WoundCarrierCatalog.PlayerPath));
                    observedEarlierWrite = !originalPlayer!.AsSpan().SequenceEqual(actualPlayer);
                    fired = true;
                    armed = false;
                    throw new IOException("Injected recovery history write failure after carrier publication.");
                }
                return Task.CompletedTask;
            }
        };
        using var fixture = Fixture.Create(scenario, hooks);
        current = fixture;
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

        armed = true;
        var failure = Assert.Throws<CanonicalStateWriteException>(() => fixture.PublishWithCanonicalRefresh());
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
        internal AcceptedTurnCanonicalStateRefresh.Result PublishWithCanonicalRefresh()
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
