using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class JointBundleComponentTests
{
    /// <summary>Requires one actual private source parse per present path across two genuinely issued Compose calls.</summary>
    /// <returns>Completion after all source/current/ownership/capture prerequisites, then the sole source-count oracle.</returns>
    [Fact]
    public async Task JointEffectSources_TwoComposesRequireOneParsePerPresentPath()
    {
        var fixture = new Fixture(_output, nameof(JointEffectSources_TwoComposesRequireOneParsePerPresentPath),
            passiveTimingEvents: true, gzipRecords: true, continuingPassiveTimings: true);
        JointEffectSourceEvidence evidence;
        await using (fixture)
            evidence = await fixture.CaptureGenuineEffectSourceReadsAsync();
        var report = AssertCandidateInvocationClosedCapture(fixture);
        var oracle = Path.Combine(fixture.Output.DiagnosticDirectory, "joint-effect-source-oracle.json");
        File.WriteAllText(oracle, JsonSerializer.Serialize(new
        {
            Kind = "T609GenuineEffectSourceOracle", Evidence = evidence, FixtureClosed = true,
            DiagnosticComplete = true, CompletedTimingRecords = report.GetProperty("TimingStored").GetInt64()
        }, new JsonSerializerOptions { WriteIndented = true }));
        _output.WriteLine(JsonSerializer.Serialize(new
        {
            Kind = "T609GenuineEffectSourceReference", Path = oracle, fixture.Output.ReportPath,
            fixture.Output.RecordsPath, evidence.PresentSources, evidence.ActualParses,
            SuccessfulCalls = evidence.Fingerprints.Length, FixtureClosed = true
        }));
        Assert.Equal(2, evidence.Fingerprints.Length);
        // Sole intended RED: unwired Compose parses 2N; the same genuine owner must reduce it to N.
        Assert.Equal(evidence.PresentSources, evidence.ActualParses);
    }

    private sealed record JointEffectSourceEvidence(int PresentSources, int ActualParses, string[] SourcePaths,
        Dictionary<string, byte[]?> BeforeImages, string ExpectedEffectFingerprint, string ExpectedEffectView,
        string[] Fingerprints, string[] Views, Dictionary<string, int>[] ComposeCounts,
        Dictionary<string, int> OpeningGuards, Dictionary<string, int> ClosingGuards,
        ValidationOperationState EntryState, ValidationOperationState ClosedState, bool SourceCompleted,
        bool SourceOwnerClosed, string BundleView);

    private sealed partial class Fixture
    {
        /// <summary>Uses the existing real stage-only preparation and fresh protocol without a new issuer or warmup.</summary>
        /// <returns>Detached evidence after all actual guards and owner closure; the caller closes capture before the source oracle.</returns>
        internal async Task<JointEffectSourceEvidence> CaptureGenuineEffectSourceReadsAsync()
        {
            try
            {
                await InitializeAsync();
                var bundle = await PrepareJointStagesAsync();
                var original = RequireIssuedCandidateStage(bundle);
                var continuation = _jointContinuation ?? throw new InvalidOperationException("Genuine effect continuation missing.");
                var reservation = _jointReservation ?? throw new InvalidOperationException("Genuine effect reservation missing.");
                var expectedBundle = ReadBundleView(bundle);
                var prepared = bundle.PreparedPlan;
                var expectedInput = bundle.EffectBatchPlan.EffectInput;
                var expectedFingerprint = WoundAcceptedTurnFingerprints.ComputeEffectInput(prepared, expectedInput);
                var expectedView = EffectSourceInputView(expectedInput);
                var paths = EffectAcceptedTurnInputComposer.SourceAuthorityPaths.ToArray();
                var before = await CaptureFreshEffectSourceBytesAsync(paths);
                var present = before.Count(row => row.Value is not null);
                Assert.True(present > 0); Assert.Null(Files.GetCurrentSemanticDataOperation());
                var openingOffset = ResetCandidateCapture("JointEffectSources.OpeningCurrent");
                await WoundAcceptedTurnPlanner.ValidateJointRepairCurrentAsync(Files, Lease, continuation);
                var openingGuards = CompleteCandidateCapture("JointEffectSources.OpeningCurrent", openingOffset);
                Assert.Equal(1, openingGuards.GetValueOrDefault("Continuation.ValidateCurrentAsync"));
                Assert.Equal(1, openingGuards.GetValueOrDefault("Continuation.InspectConfirmedHeldClaim"));
                var fingerprints = new List<string>(); var views = new List<string>();
                var counts = new List<Dictionary<string, int>>();
                ValidationOperation operation; ValidationOperationState entryState;
                Dictionary<string, int> closingGuards; bool completed = false; bool sourceClosed;
                using (var source = Files.BeginSourceDataOperationIfNeeded())
                {
                    Assert.NotNull(source);
                    operation = Assert.IsType<ValidationOperation>(Files.GetCurrentSemanticDataOperation());
                    entryState = operation.CaptureState(); Assert.Equal(new ValidationOperationState(0, 0, false), entryState);
                    for (var index = 0; index < 2; index++)
                    {
                        EffectAcceptedTurnInput composed;
                        using (Files.EnterOperationTimingContext())
                        {
                            var offset = ResetCandidateCapture("JointEffectSources.Compose" + index);
                            composed = WoundAcceptedTurnPlanner.ComposeJointRepairEffectInput(prepared);
                            var observed = CompleteCandidateCapture("JointEffectSources.Compose" + index, offset);
                            Assert.Equal(1, observed.GetValueOrDefault("JointEffectInput.Compose"));
                            Assert.Equal(1, observed.GetValueOrDefault("JointEffectInput.Build"));
                            counts.Add(observed);
                        }
                        var fingerprint = WoundAcceptedTurnFingerprints.ComputeEffectInput(prepared, composed);
                        var view = EffectSourceInputView(composed);
                        Assert.Equal(expectedFingerprint, fingerprint); Assert.Equal(expectedView, view);
                        fingerprints.Add(fingerprint); views.Add(view);
                        Assert.Same(operation, Files.GetCurrentSemanticDataOperation());
                        Assert.Same(continuation, prepared.JointRepairContinuationAuthority);
                        Assert.Same(reservation, _jointReservation);
                        Assert.True(Files.IsCurrentSessionGeneration(Lease, original.Generation));
                    }
                    var closingOffset = ResetCandidateCapture("JointEffectSources.ClosingCurrent");
                    await WoundAcceptedTurnPlanner.ValidateJointRepairCurrentAsync(Files, Lease, continuation);
                    closingGuards = CompleteCandidateCapture("JointEffectSources.ClosingCurrent", closingOffset);
                    Assert.Equal(1, closingGuards.GetValueOrDefault("Continuation.ValidateCurrentAsync"));
                    Assert.Equal(1, closingGuards.GetValueOrDefault("Continuation.InspectConfirmedHeldClaim"));
                    await source.CompleteAsync(); completed = true;
                    source.Dispose(); sourceClosed = source.CaptureState().Closed; Assert.True(sourceClosed);
                }
                Assert.Null(Files.GetCurrentSemanticDataOperation());
                var closed = operation.CaptureState(); Assert.Equal(new ValidationOperationState(0, 0, true), closed);
                var after = await CaptureFreshEffectSourceBytesAsync(paths);
                Assert.Equal(before.Keys, after.Keys);
                foreach (var row in before) Assert.Equal(row.Value, after[row.Key]);
                Assert.Equal(paths, EffectAcceptedTurnInputComposer.SourceAuthorityPaths);
                Assert.Equal(expectedBundle, ReadBundleView(bundle));
                Assert.Same(continuation, _jointContinuation); Assert.Same(reservation, _jointReservation);
                Assert.True(Files.IsCurrentSessionGeneration(Lease, original.Generation));
                return new(present, counts.Sum(row => row.GetValueOrDefault("JointEffectInput.PrivateSourceParse")), paths,
                    before, expectedFingerprint, expectedView, fingerprints.ToArray(), views.ToArray(), counts.ToArray(),
                    openingGuards, closingGuards, entryState, closed, completed, sourceClosed, expectedBundle);
            }
            catch { _retained = true; throw; }
        }

        /// <summary>Reads actual bytes through the existing fresh bypass outside the selected Compose ranges.</summary>
        /// <param name="paths">The declared complete source inventory used by both comparisons.</param>
        /// <returns>Detached current physical bytes/absence, never provisional snapshots.</returns>
        private async Task<Dictionary<string, byte[]?>> CaptureFreshEffectSourceBytesAsync(string[] paths)
        {
            using var fresh = FileSystemManager.SuppressValidationReuse();
            var result = new Dictionary<string, byte[]?>(StringComparer.Ordinal);
            foreach (var path in paths) result.Add(path, await Files.ReadFileBytesAsync(Lease, path));
            return result;
        }

        /// <summary>Compares the entire returned record plus the opaque authorities' own semantic identities.</summary>
        /// <param name="input">The actual detached effect input from the original producer.</param>
        /// <returns>A deterministic view including mutable record DATA and sealed authority fingerprints.</returns>
        private static string EffectSourceInputView(EffectAcceptedTurnInput input) => JsonSerializer.Serialize(new
        {
            Value = input, Source = input.SourceAuthority.Fingerprint, Target = input.TargetAuthority.Fingerprint,
            Preallocated = input.PreallocatedCombatantIdentities?.Fingerprint,
            SkillScope = input.SkillScopeAuthority?.Fingerprint, Locations = input.WoundApplicationLocations?.Fingerprint
        });
    }
}
