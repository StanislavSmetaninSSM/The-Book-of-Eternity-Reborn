using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class JointBundleComponentTests
{
    /// <summary>
    /// Runs retained joint-copy contracts with genuinely bound stages in one source DATA operation and normal owned fixture closing.
    /// </summary>
    /// <returns>
    /// Completion after direct-copy, current-byte refusal and restoration, constructor, mutation, history restoration,
    /// abort parity, admission-tree and heavy preparation timing-coverage checks.
    /// </returns>
    [Fact]
    public async Task JointCopy_RetainsAdmissionDetachmentAndRefusalContracts()
    {
        await using var fixture = new Fixture(_output, nameof(JointCopy_RetainsAdmissionDetachmentAndRefusalContracts));
        await fixture.InitializeAsync();
        using var source = fixture.Files.BeginSourceDataOperationIfNeeded();
        Assert.NotNull(source);
        var bound = await fixture.PrepareBoundJointAsync();
        Assert.True(bound.Success, JsonSerializer.Serialize(bound.Issues));
        var directCopy = fixture.AssertDirectJointCopyRetainsAdmissionWithoutFinalReduction();
        await fixture.AssertJointCurrentBytesRefusedAndRestoredAsync();
        fixture.AssertJointCopyContracts(bound.Input!, bound.Authority!);
        fixture.AssertJointAdmissionTimingTree(directCopy);
        fixture.AssertJointHeavyPreparationTimingTree(directCopy);
        await source.CompleteAsync();
    }

    private sealed partial class Fixture
    {
        /// <summary>
        /// Copies the genuinely bound joint bundle in its own complete observation slice.
        /// Requires preparation, effect and final admission while refusing redundant final reduction.
        /// </summary>
        /// <returns>
        /// The half-open observation offsets of this existing direct-copy scope; the end offset is exclusive.
        /// </returns>
        internal (int Start, int End) AssertDirectJointCopyRetainsAdmissionWithoutFinalReduction()
        {
            var bundle = _jointBundle ?? throw new InvalidOperationException("A genuine joint binding is required.");
            var offset = ResetCandidateCapture("DirectJointCopy");
            AcceptedMechanicsWoundStageBundle copy;
            using (Files.EnterOperationTimingContext())
            using (Files.MeasureOperation("JointBundleCandidate.DirectJointCopy"))
                copy = bundle.DetachedCopy();
            var end = _observations.Count;
            var counts = CompleteCandidateCapture("DirectJointCopy", offset);
            Assert.True(counts.GetValueOrDefault("WoundPlanCache.ValidatePreparedResult") > 0);
            Assert.True(counts.GetValueOrDefault("WoundEffect.AcceptEffectResult") > 0);
            Assert.Equal(0, counts.GetValueOrDefault("WoundPlanner.Finalize"));
            Assert.Equal(0, counts.GetValueOrDefault("WoundPlanCache.FinalizationReplay"));
            Assert.True(counts.GetValueOrDefault("WoundPlanner.FinalizationHandoff") > 0);
            Assert.True(counts.GetValueOrDefault("WoundPlanner.JointFinalAdmission") > 0);
            Assert.NotSame(bundle, copy);
            Assert.Equal(bundle.BundleFingerprint, copy.BundleFingerprint);
            Assert.Equal(bundle.WoundFinalPlanFingerprint, copy.WoundFinalPlanFingerprint);
            return (offset, end);
        }

        /// <summary>
        /// Checks retained-copy data isolation, normal-constructor replay and refusal parity using this fixture's real joint source.
        /// Supports a dedicated copy case or execution after candidate oracles, before normal fixture closing.
        /// Checks that recorded prepared/effect refusals stop before final validation.
        /// </summary>
        /// <param name="input">
        /// The genuinely bound common input from this fixture's successful joint preparation.
        /// </param>
        /// <param name="authority">
        /// The genuinely issued common authority paired with <paramref name="input"/>;
        /// it is refused after normal reservation abort.
        /// </param>
        internal void AssertJointCopyContracts(
            AcceptedMechanicsInput input, WoundAcceptedTurnPlanner.MortalJointRepairCommonAuthority authority)
        {
            var bundle = _jointBundle ?? throw new InvalidOperationException("A genuine joint binding is required.");
            using var context = Files.EnterOperationTimingContext();
            var expected = ReadBundleView(bundle);
            var offset = ResetCandidateCapture("InitialConstructorReference");
            AcceptedMechanicsWoundStageBundle reference;
            using (Files.MeasureOperation("JointBundleCandidate.InitialConstructorReference"))
                reference = new(bundle.Input, bundle.PreparedPlan, bundle.EffectBatchPlan, bundle.FinalPlan);
            var referenceCounts = CompleteCandidateCapture("InitialConstructorReference", offset);
            Assert.True(referenceCounts.GetValueOrDefault("WoundPlanner.Finalize") > 0);
            Assert.True(referenceCounts.GetValueOrDefault("WoundPlanCache.FinalizationReplay") > 0);
            Assert.Equal(expected, ReadBundleView(reference));

            using (Files.MeasureOperation("JointBundleCandidate.CopyOfCopyAndReturnedMutation"))
            {
                var first = bundle.DetachedCopy();
                var second = first.DetachedCopy();
                Assert.Equal(expected, ReadBundleView(first));
                Assert.Equal(expected, ReadBundleView(second));
                Assert.Same(bundle.PreparedPlan.JointRepairContinuationAuthority, first.PreparedPlan.JointRepairContinuationAuthority);
                Assert.Same(first.PreparedPlan.JointRepairContinuationAuthority, second.PreparedPlan.JointRepairContinuationAuthority);
                Assert.Same(bundle.EffectBatchPlan.EffectInput.SourceAuthority, second.EffectBatchPlan.EffectInput.SourceAuthority);
                Assert.Same(bundle.EffectBatchPlan.EffectInput.TargetAuthority, second.EffectBatchPlan.EffectInput.TargetAuthority);
                MutateReturnedBundleData(bundle);
                MutateReturnedBundleData(first);
                MutateReturnedBundleData(second);
                Assert.Equal(expected, ReadBundleView(bundle));
                Assert.Equal(expected, ReadBundleView(first));
                Assert.Equal(expected, ReadBundleView(second));
            }

            using (Files.MeasureOperation("JointBundleCandidate.InvalidSuppliedStages"))
            {
                var changed = bundle.Input with
                {
                    Binding = bundle.Input.Binding with { RequestId = "foreign_copy_request_" + _nonce }
                };
                var preparedRefusalStart = _observations.Count;
                var preparedFailure = Assert.Throws<ArgumentException>(() =>
                    new AcceptedMechanicsWoundStageBundle(changed, bundle.PreparedPlan, bundle.EffectBatchPlan, bundle.FinalPlan));
                var preparedRefusalEnd = _observations.Count;
                Assert.Equal("preparedPlan", preparedFailure.ParamName);
                AssertNoFinalValidationInRecordedRange("Prepared", (preparedRefusalStart, preparedRefusalEnd));
                var effect = bundle.EffectBatchPlan;
                var commands = effect.EffectInput.RawCommands.DeepClone().AsObject();
                commands["component_invalid"] = true;
                var changedEffectInput = effect.EffectInput with { RawCommands = commands };
                // This is deliberately invalid DATA with genuine unchanged companions, not a new issuer or guard.
                var changedEffect = WoundEffectBatchAcceptedPlan.Create(bundle.PreparedPlan, changedEffectInput,
                    effect.EffectPlan, effect.ApplicationResults, effect.TerminationResults);
                var effectRefusalStart = _observations.Count;
                var effectFailure = Assert.Throws<ArgumentException>(() =>
                    new AcceptedMechanicsWoundStageBundle(bundle.Input, bundle.PreparedPlan, changedEffect, bundle.FinalPlan));
                var effectRefusalEnd = _observations.Count;
                Assert.Equal("effectBatchPlan", effectFailure.ParamName);
                AssertNoFinalValidationInRecordedRange("Effect", (effectRefusalStart, effectRefusalEnd));
            }

            using (Files.MeasureOperation("JointBundleCandidate.RetainedHistoryCorruption"))
            {
                try
                {
                    using (bundle.DamageRetainedFinalHistoryForComponent())
                    {
                        var referenceFailure = Assert.Throws<ArgumentException>(() =>
                            new AcceptedMechanicsWoundStageBundle(bundle.Input, bundle.PreparedPlan, bundle.EffectBatchPlan, bundle.FinalPlan));
                        var copyFailure = Assert.Throws<ArgumentException>(() => bundle.DetachedCopy());
                        Assert.Equal("finalPlan", referenceFailure.ParamName);
                        Assert.Equal(referenceFailure.ParamName, copyFailure.ParamName);
                        Assert.Equal(referenceFailure.Message, copyFailure.Message);
                    }
                }
                catch { _retained = true; throw; }
                Assert.Equal(expected, ReadBundleView(bundle));
                Assert.Equal(expected, ReadBundleView(bundle.DetachedCopy()));
            }

            using (Files.MeasureOperation("JointBundleCandidate.AbortedReservationParity"))
            {
                CloseJointReservation();
                AcceptedMechanicsWoundStageBundle? fullAfterAbort = null, copyAfterAbort = null;
                var referenceFailure = Record.Exception(() => fullAfterAbort =
                    new(bundle.Input, bundle.PreparedPlan, bundle.EffectBatchPlan, bundle.FinalPlan));
                var copyFailure = Record.Exception(() => copyAfterAbort = bundle.DetachedCopy());
                Assert.Equal(referenceFailure?.GetType(), copyFailure?.GetType());
                Assert.Equal(referenceFailure?.Message, copyFailure?.Message);
                if (referenceFailure is null)
                {
                    Assert.Equal(expected, ReadBundleView(fullAfterAbort!));
                    Assert.Equal(expected, ReadBundleView(copyAfterAbort!));
                }
                var refused = AcceptedTurnAuthorityRegistry.CreateJointRepairCommonCandidate(Files, Lease, input, authority);
                Assert.False(refused.Result.Success);
                Assert.Null(refused.Fence);
                _output.WriteLine(JsonSerializer.Serialize(new
                {
                    Kind = "JointCopyContracts", CopyOfCopy = true, ReturnedMutationIsolated = true,
                    InitialConstructorReplayed = true, SuppliedStagesRefused = true, PrivateHistoryRestored = true,
                    ReservationAborted = true, ReferenceAfterAbort = referenceFailure?.ToString(),
                    CopyAfterAbort = copyFailure?.ToString(), RegistryRefused = true, refused.Result.Issues,
                    BundleView = JsonSerializer.Deserialize<JsonElement>(expected)
                }));
            }
        }

        /// <summary>
        /// Captures recomputed complete stage seals, ordered final data and both result maps.
        /// </summary>
        /// <param name="bundle">
        /// The actual original or detached copy whose returned data is observed.
        /// </param>
        /// <returns>
        /// A deterministic data view that changes if a covered graph, allocation, map or stored seal changes.
        /// </returns>
        private static string ReadBundleView(AcceptedMechanicsWoundStageBundle bundle)
        {
            var input = bundle.Input;
            var prepared = bundle.PreparedPlan;
            var effect = bundle.EffectBatchPlan;
            var final = bundle.FinalPlan;
            return JsonSerializer.Serialize(new
            {
                StoredSeals = new[] { bundle.InputFingerprint, bundle.WoundPreparationFingerprint,
                    bundle.EffectInputFingerprint, bundle.EffectAcceptedTurnPlanFingerprint,
                    bundle.WoundFinalPlanFingerprint, bundle.BundleFingerprint },
                Input = WoundAcceptedTurnFingerprints.ComputeInput(input),
                Prepared = WoundAcceptedTurnFingerprints.ComputePreparation(prepared),
                EffectInput = WoundAcceptedTurnFingerprints.ComputeEffectInput(prepared, effect.EffectInput),
                EffectPlan = WoundAcceptedTurnFingerprints.ComputeEffectPlan(prepared, effect.EffectInput,
                    effect.EffectPlan, effect.ApplicationResults, effect.TerminationResults),
                Final = WoundAcceptedTurnFingerprints.ComputeFinal(prepared, effect, final.CarrierContributions,
                    final.IdentityIndexAfterImage, final.HistoryAfterImage, final.TransitionIntents),
                final.AllocatedWoundIds, final.AllocatedTransitionIds, final.CarrierContributions,
                Identity = final.IdentityIndexAfterImage, History = final.HistoryAfterImage, final.TransitionIntents,
                Applications = bundle.ApplicationResults.OrderBy(row => row.Key, StringComparer.Ordinal).ToArray(),
                Terminations = bundle.TerminationResults.OrderBy(row => row.Key, StringComparer.Ordinal).ToArray()
            });
        }

        /// <summary>
        /// Mutates returned JSON payloads and verifies result-map membership remains read-only.
        /// This never damages the retained private graphs.
        /// </summary>
        /// <param name="bundle">
        /// The actual bundle whose detached views are deliberately changed.
        /// </param>
        private static void MutateReturnedBundleData(AcceptedMechanicsWoundStageBundle bundle)
        {
            bundle.Input.PreTurnIdentityIndex["returned_mutation"] = true;
            bundle.Input.PreTurnHistory["returned_mutation"] = true;
            if (bundle.Input.PreTurnCarriers.PlayerWounds is { } playerWounds)
                playerWounds["returned_mutation"] = true;
            if (bundle.Input.PreTurnEffectIdentityIndex is { } effectIdentity)
                effectIdentity["returned_mutation"] = true;
            var prepared = bundle.PreparedPlan;
            prepared.BaselineAuthority.PreTurnIdentityIndex["returned_mutation"] = true;
            prepared.BaselineAuthority.PreTurnHistory["returned_mutation"] = true;
            var effect = bundle.EffectBatchPlan;
            effect.EffectInput.RawCommands["returned_mutation"] = true;
            effect.EffectPlan.IdentityIndexAfterImage["returned_mutation"] = true;
            foreach (var carrier in effect.EffectPlan.CarrierAfterImages.Values)
                carrier["returned_mutation"] = true;
            var final = bundle.FinalPlan;
            final.IdentityIndexAfterImage["returned_mutation"] = true;
            final.HistoryAfterImage["returned_mutation"] = true;
            Assert.True(((IDictionary<string, EffectAcceptedApplicationResult>)bundle.ApplicationResults).IsReadOnly);
            Assert.True(((IDictionary<string, EffectAcceptedTerminationResult>)bundle.TerminationResults).IsReadOnly);
        }
    }
}
