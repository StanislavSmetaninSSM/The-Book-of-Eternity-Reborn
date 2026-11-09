using System.Text.Json;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>Four separately owned real signed-existing C1 rows, distributing all ordered III/IV parent ranks without a costly Cartesian journey.</summary>
    /// <param name="contour">A=0 III+III, B=1 III+IV, C=2 IV+III, D=3 IV+IV.</param>
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public Task SpiritualPairLow_RealOwnerAdvancesAndComposesC1(int contour)
    {
        var spec = PairLowCase("pair_low_owner_" + contour, SpiritualPairLowPressureData.Boundary(contour));
        return WithPairLowFailureAsync(spec.Name, () => WithPressureGroupCaseAsync(spec, window =>
            AssertPressurePairActualAdmissionAsync(window, ExpectedPressureGroupOrdinary(spec),
                "Exactly two complete applicable parents must add two steps to the independently proved increasing ordinary I–III, with positive actual maximum.")));
    }

    /// <summary>Future GREEN only: B actualIII/maxII and D actualIV/maxI traverse first C2, exactly one cold Open, none/C3, common C4 and signed47.</summary>
    /// <param name="contour">Only the selected B=1 and D=3 normals, each with independent mutable ownership.</param>
    [Theory]
    [InlineData(1)] [InlineData(3)]
    public Task SpiritualPairLow_ActualColdNonePublishesOnceAndValidatesSignedNext(int contour)
    {
        if (contour is not (1 or 3)) throw new ArgumentOutOfRangeException(nameof(contour));
        var spec = PairLowCase("pair_low_publication_" + contour, SpiritualPairLowPressureData.Boundary(contour));
        return WithPairLowFailureAsync(spec.Name, () => WithPressureGroupPublicationAsync(spec, async setup =>
        {
            var ordinary = ExpectedPressureGroupOrdinary(spec);
            var completed = await CompletePressureGroupColdC3Async(setup, setup.Copy.Context.Validator, ordinary);
            await PublishPressureGroupAndReadNextAsync(setup, setup.Copy.Context.Validator, ordinary, completed);
        }));
    }

    /// <summary>Future GREEN only, after BOTH normal rows accepted: one B late cut restores signed origin and retries exact bytes with fresh zero-count owners once.</summary>
    [Fact]
    public Task SpiritualPairLow_LateCutRestoresSignedOriginAndFreshRetryPublishesOnce()
    {
        var spec = PairLowCase("pair_low_b_rollback", PairLowPressureContour.OneZero);
        return WithPairLowFailureAsync(spec.Name, () => AssertPressureGroupLatePublicationCutAsync(spec));
    }

    /// <summary>Future GREEN only: lawful ordinaryI−4/max0 and hypothetical actualIII6/max0 retain the positive-actual guard before actual evaluation or C1 mutation.</summary>
    [Fact]
    public Task SpiritualPairLow_TrueNonpositiveActualMaximumCannotAdvance()
    {
        var spec = PairLowCase("pair_low_true_actual_zero", PairLowPressureContour.ActualZero);
        return WithPairLowFailureAsync(spec.Name, () => WithPressureGroupCaseAsync(spec, async window =>
        {
            var expected = SpiritualPairLowPressureData.Expected(PairLowPressureContour.ActualZero);
            await AssertPressurePairOrdinaryPremiseAsync(window, expected.Ordinary);
            Assert.Equal(expected.Actual, SpiritualWoundOpportunityMath.Calculate(expected.Actual.Input));
            await AssertPressureGroupActualGuardAsync(window,
                "actual existing pressure requires a positive hypothetical actual maximum");
        }));
    }

    /// <summary>Maps only the closed DATA contour to a complete pre-sign pair spec; legacy cheap DATA has no second expensive owner.</summary>
    private static PressureGroupCase PairLowCase(string name, PairLowPressureContour contour)
    {
        var expected = SpiritualPairLowPressureData.Expected(contour);
        var ranks = contour switch
        {
            PairLowPressureContour.OnePositive => new SpiritualPressureParentRanks(3, 3),
            PairLowPressureContour.OneZero or PairLowPressureContour.ActualZero => new SpiritualPressureParentRanks(3, 4),
            PairLowPressureContour.TwoZero => new SpiritualPressureParentRanks(4, 3),
            PairLowPressureContour.ThreeZero => new SpiritualPressureParentRanks(4, 4),
            _ => throw new ArgumentOutOfRangeException(nameof(contour))
        };
        return new(name, After: expected.Ordinary.Input.NewStrain!, PlayerDie: expected.PlayerDie,
            Resilience: expected.Resilience, Ranks: ranks, AdvanceExpected: true, LowContour: contour);
    }

    /// <summary>Closed independent full ordinary arithmetic, preserving every old default and refusal/sibling fixture premise.</summary>
    private static SpiritualWoundCalculation ExpectedPressureGroupOrdinary(PressureGroupCase spec)
    {
        if (spec.CappedOrdinary)
        {
            if (spec.Parents is < 1 or > 3 || spec.After != "broken" || spec.PlayerDie != 4 || spec.Resilience != 0 || spec.LowContour is not null ||
                spec.SelectedCost || spec.OtherCost || spec.PressureRoll || spec.CompletePressureRollAudit || spec.RankFourPressureArt)
                throw new ArgumentException("Unknown capped singleton pre-sign contour.", nameof(spec));
            return SpiritualSingletonIvPlayerDataTests.OrdinaryCalculation(PlayerSingletonPressureContour.CappedBroken);
        }
        if (spec.LowContour is { } contour)
        {
            var expected = SpiritualPairLowPressureData.Expected(contour);
            if (spec != PairLowCase(spec.Name, contour)) throw new ArgumentException("A low pair spec must equal its complete closed pre-sign contour.", nameof(spec));
            return expected.Ordinary;
        }
        return (spec.PlayerDie, spec.Resilience, spec.After) switch
        {
            (4, 0, "strained") => SpiritualPairLowPressureData.Expected(PairLowPressureContour.OnePositive).Ordinary,
            (4, 0, "fractured") => new(new(8, 0, 0, "clear", "fractured", "hostile", 4), 0, 2, 1, 13, 2, 2, 4, true, 2),
            (4, 0, "overwhelmed") => new(new(8, 0, 0, "clear", "overwhelmed", "hostile", 4), 0, 3, 2, 18, 3, 3, 4, true, 3),
            (6, 0, "strained") => SpiritualPairLowPressureData.Expected(PairLowPressureContour.LegacyOneZero).Ordinary,
            (4, 3, "strained") => new(new(8, 0, 3, "clear", "strained", "hostile", 4), 0, 1, 0, 2, 0, 1, 4, true, 0),
            (4, 3, "fractured") => SpiritualPairLowPressureData.Expected(PairLowPressureContour.TwoZero).Ordinary,
            _ => throw new ArgumentException("Unknown independently authored ordinary fixture contour.", nameof(spec))
        };
    }

    /// <summary>Independent positive actual arithmetic for the retained pair or N3/N4 ordinaryI–III test contours.</summary>
    /// <param name="spec">Complete independently authored pre-sign case, including its selected parent count.</param>
    /// <returns>The existing pair oracle or the literal complete N3/N4 actualIV calculation.</returns>
    private static SpiritualWoundCalculation ExpectedPressureOwnedGroupActual(PressureGroupCase spec)
    {
        if (spec.Parents == 2) return ExpectedPressurePairActual(spec);
        if (spec.Parents is not (3 or 4) || spec.After is not ("strained" or "fractured" or "overwhelmed") ||
            spec.PlayerDie != 4 || spec.Resilience != 0 || spec.CappedOrdinary || spec.LowContour is not null)
            throw new ArgumentException("Unknown independent multi-parent actual contour.", nameof(spec));
        return SpiritualPressureMultiParentData.ActualCalculation;
    }

    /// <summary>Exact full pair actual DATA for low contours; unchanged legacy and selected-sibling default is broken23/maxIV.</summary>
    private static SpiritualWoundCalculation ExpectedPressurePairActual(PressureGroupCase spec)
    {
        if (spec.CappedOrdinary) throw new ArgumentException("Capped singleton is not a pair.", nameof(spec));
        if (spec.LowContour is { } contour)
        {
            _ = ExpectedPressureGroupOrdinary(spec);
            return SpiritualPairLowPressureData.Expected(contour).Actual;
        }
        if (spec.Parents != 2 || spec.PlayerDie != 4 || spec.Resilience != 0 || spec.After is not ("fractured" or "overwhelmed"))
            throw new ArgumentException("Unknown independently authored pair actual fixture contour.", nameof(spec));
        return new(new(8, 0, 0, "clear", "broken", "hostile", 4), 0, 4, 3, 23, 4, 4, 4, true, 4);
    }

    /// <summary>Reports unchanged primary and all retained cleanup Data after the existing complete ownership helpers finish.</summary>
    private async Task WithPairLowFailureAsync(string name, Func<Task> work)
    {
        try { await work(); }
        catch (Exception primary)
        {
            try
            {
                var dataCount = primary.Data.Count;
                var entries = primary.Data.Cast<System.Collections.DictionaryEntry>().Select(entry => new
                {
                    Key = entry.Key?.ToString(), KeyType = entry.Key?.GetType().FullName,
                    ValueType = entry.Value?.GetType().FullName,
                    Value = entry.Value is Exception retained ? retained.ToString() : entry.Value?.ToString()
                }).ToArray();
                _storageOutput.WriteLine(JsonSerializer.Serialize(new
                {
                    name, phase = "pair_low_failure_after_cleanup", PrimaryType = primary.GetType().FullName,
                    PrimaryMessage = primary.Message, PrimaryStackTrace = primary.StackTrace, PrimaryException = primary.ToString(),
                    DataCount = dataCount, DataEntries = entries, DataEntriesComplete = entries.Length == dataCount
                }));
            }
            catch (Exception diagnosticError)
            {
                try { RetainPressureFixtureFailure(primary, "pair_low_diagnostics", diagnosticError); }
                catch (Exception) { /* Diagnostic failure cannot replace the unchanged primary failure. */ }
            }
            throw;
        }
    }
}
