using System.Globalization;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class JointBundleComponentTests
{
    /// <summary>Counts the real richer expected preparation source after genuine semantic eligibility is established.</summary>
    /// <returns>Completion after the actual source fence and fixture closing, before the strict source-reduction oracle.</returns>
    [Fact]
    public async Task JointPreparedReplay_RequiresOneExpectedSourceWithinEligibleOwner()
    {
        int sources;
        await using (var fixture = new Fixture(_output,
            nameof(JointPreparedReplay_RequiresOneExpectedSourceWithinEligibleOwner), passiveTimingEvents: true))
        {
            sources = await fixture.CapturePreparedExpectedReplaySourcesAsync();
        }
        _output.WriteLine(JsonSerializer.Serialize(new
        {
            Kind = "PreparedExpectedReplayFinalOracle", ActualSources = sources,
            LogicalSuccessfulCalls = 8, SourceFenceCompleted = true, FixtureDisposed = true
        }));
        // RED is only this final count mismatch: all DATA, eligibility, fence and closing prerequisites precede it.
        Assert.Equal(1, sources);
    }

    private sealed partial class Fixture
    {
        /// <summary>Exercises the unchanged prepared-result validator eight times in the same genuinely eligible source owner.</summary>
        /// <returns>The completed actual expected-source count; this method does not enforce the reduction oracle.</returns>
        internal async Task<int> CapturePreparedExpectedReplaySourcesAsync()
        {
            try
            {
                await InitializeAsync();
                var bundle = await PrepareJointStagesAsync();
                var original = _jointOriginal ?? throw new InvalidOperationException("A genuine original is required.");
                var continuation = _jointContinuation ?? throw new InvalidOperationException("The actual continuation is required.");
                var reservation = _jointReservation ?? throw new InvalidOperationException("The live reservation is required.");
                var expectedInput = WoundAcceptedTurnFingerprints.ComputeInput(bundle.Input);
                var expectedProjection = PreparedExpectedReplayProjection(bundle.PreparedPlan);
                var expectedBundle = ReadBundleView(bundle);
                var binding = bundle.Input.Binding;
                Assert.Equal(original.SessionId, binding.SessionId);
                Assert.Equal(original.RequestId, binding.RequestId);
                Assert.Equal(original.SnapshotToken, binding.SnapshotToken);
                Assert.Equal(original.Turn, binding.Turn);
                Assert.Same(continuation, bundle.PreparedPlan.JointRepairContinuationAuthority);
                Assert.True(Files.IsCurrentSessionGeneration(Lease, original.Generation));
                using var source = Files.BeginSourceDataOperationIfNeeded();
                Assert.NotNull(source); // a new owner, not a borrowed/null contour
                var operation = Assert.IsType<ValidationOperation>(Files.GetCurrentSemanticDataOperation());
                var paths = AcceptedMechanicsWoundCommonInputComposer.TreatmentOrdinaryGuardPaths.Concat(new[]
                {
                    WoundCarrierCatalog.PlayerPath, WoundCarrierCatalog.NpcPath, WoundCarrierCatalog.EnemiesPath,
                    WoundCarrierCatalog.AlliesPath, WoundCarrierCatalog.AfterlifeProfilesPath,
                    AcceptedMechanicsPlan.WoundCommandPath, WoundAcceptedTurnSnapshotContract.PendingResolutionPath,
                    LiveTurnPreparationService.PendingTurnSnapshotManifestPath, PendingTurnSnapshotAuthority.AuthorityPath,
                    "output/narrative_response.json", MortalJointInterfacePublication.InterfacePath
                }).Distinct(StringComparer.Ordinal).OrderBy(path => path, StringComparer.Ordinal).ToArray();
                var before = new Dictionary<string, byte[]?>(StringComparer.Ordinal);
                foreach (var path in paths) before.Add(path, await Files.ReadFileBytesAsync(Lease, path));
                Dictionary<string, int> eligibilityCounts;
                Dictionary<string, int> replayCounts;
                var projections = new List<string[]>();
                using (Files.EnterOperationTimingContext())
                {
                    var eligibilityOffset = ResetCandidateCapture("PreparedExpectedReplayEligibility");
                    for (var i = 0; i < 2; i++)
                    {
                        Assert.True(WoundAcceptedTurnPlanner.JointRepairPreparedAgrees(bundle.PreparedPlan));
                        Assert.Same(operation, Files.GetCurrentSemanticDataOperation());
                    }
                    eligibilityCounts = CompleteCandidateCapture("PreparedExpectedReplayEligibility", eligibilityOffset);
                    Assert.Equal(1, eligibilityCounts.GetValueOrDefault("JointPreparation.Replay"));
                    Assert.Equal(0, eligibilityCounts.GetValueOrDefault("WoundPlanCache.PreparedExpectedReplay.Source"));
                    var replayOffset = ResetCandidateCapture("PreparedExpectedReplayEightCalls");
                    for (var i = 0; i < 8; i++)
                    {
                        var input = bundle.Input;
                        var prepared = bundle.PreparedPlan;
                        Assert.NotSame(input, bundle.Input);
                        Assert.NotSame(prepared, bundle.PreparedPlan);
                        Assert.Equal(expectedInput, WoundAcceptedTurnFingerprints.ComputeInput(input));
                        Assert.Equal(expectedProjection, PreparedExpectedReplayProjection(prepared));
                        Assert.Same(continuation, prepared.JointRepairContinuationAuthority);
                        var result = WoundAcceptedTurnPlanCache.ValidatePreparedResult(input, expectedInput,
                            new WoundAcceptedTurnPreparationResult(prepared, Array.Empty<ValidationIssue>()));
                        Assert.True(result.Success, JsonSerializer.Serialize(result.Issues));
                        Assert.Empty(result.Issues);
                        var accepted = Assert.IsType<WoundPreparedAcceptedTurnPlan>(result.Plan);
                        var projection = PreparedExpectedReplayProjection(accepted);
                        Assert.Equal(expectedProjection, projection);
                        Assert.Same(continuation, accepted.JointRepairContinuationAuthority);
                        Assert.Equal(expectedInput, WoundAcceptedTurnFingerprints.ComputeInput(input));
                        Assert.Equal(expectedProjection, PreparedExpectedReplayProjection(prepared));
                        Assert.Equal(prepared.WoundPreparationFingerprint,
                            WoundAcceptedTurnFingerprints.ComputePreparation(accepted));
                        Assert.Same(operation, Files.GetCurrentSemanticDataOperation());
                        Assert.Same(reservation, _jointReservation);
                        projections.Add(projection);
                    }
                    replayCounts = CompleteCandidateCapture("PreparedExpectedReplayEightCalls", replayOffset);
                    Assert.Equal(8, projections.Count);
                    Assert.Equal(8, replayCounts.GetValueOrDefault("WoundPlanCache.ValidatePreparedResult"));
                    Assert.True(replayCounts.GetValueOrDefault("JointContinuation.ValidateSeal") >= 8);
                    Assert.Equal(0, replayCounts.GetValueOrDefault("JointPreparation.Replay"));
                } // thread-static timing context ends before any await
                Assert.Equal(expectedBundle, ReadBundleView(bundle));
                Assert.Equal(expectedInput, WoundAcceptedTurnFingerprints.ComputeInput(bundle.Input));
                Assert.Same(continuation, _jointContinuation);
                Assert.Same(reservation, _jointReservation);
                Assert.True(Files.IsCurrentSessionGeneration(Lease, original.Generation));
                await source.CompleteAsync();
                foreach (var row in before)
                {
                    var fullPath = Files.ResolvePath(row.Key);
                    Assert.Equal(row.Value, File.Exists(fullPath) ? File.ReadAllBytes(fullPath) : null);
                }
                var sources = replayCounts.GetValueOrDefault("WoundPlanCache.PreparedExpectedReplay.Source");
                source.Dispose();
                Assert.True(source.CaptureState().Closed);
                Output.WriteLine(JsonSerializer.Serialize(new
                {
                    Kind = "PreparedExpectedReplaySources", original.SessionId, original.RequestId,
                    original.SnapshotToken, original.Turn, original.Generation,
                    LogicalSuccessfulCalls = projections.Count, ActualSources = sources,
                    SourceFenceCompleted = true, OwnerClosed = source.CaptureState().Closed,
                    Eligibility = eligibilityCounts, Replay = replayCounts,
                    ExpectedInputFingerprint = expectedInput, ExpectedProjection = expectedProjection,
                    ActualProjections = projections, BeforeImages = before, BundleView = expectedBundle
                }));
                return sources;
            }
            catch
            {
                _retained = true; // preserve the actual partial namespace on an unexpected prerequisite/fence failure
                throw;
            }
        }

        /// <summary>Reads exactly the complete five-scalar and ordered batch-pair comparison contract of PreparedReplayAgrees.</summary>
        /// <param name="plan">The independently exported genuine prepared result.</param>
        /// <returns>Detached ordered values including the explicit batch count.</returns>
        private static string[] PreparedExpectedReplayProjection(WoundPreparedAcceptedTurnPlan plan)
        {
            var baseline = plan.BaselineAuthority;
            var batches = plan.EffectOperationBatches;
            var values = new List<string>
            {
                plan.BindingFingerprint, plan.InputFingerprint, plan.WoundPreparationFingerprint,
                baseline.PreparedInputFingerprint, baseline.AuthoritySeal,
                batches.Count.ToString(CultureInfo.InvariantCulture)
            };
            foreach (var batch in batches)
            {
                values.Add(batch.SourceExportFingerprint);
                values.Add(batch.TransitionAuthority.AuthoritySeal);
            }
            return values.ToArray();
        }
    }
}
