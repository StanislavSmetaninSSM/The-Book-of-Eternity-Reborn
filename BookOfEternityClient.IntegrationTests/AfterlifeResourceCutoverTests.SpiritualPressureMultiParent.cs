using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>Desired N3 success: lawful signed/current retained parents produce ordinary8/I→actual23/IV and complete v6/C1 provenance.</summary>
    [Fact]
    public Task SpiritualPressureMultiParent_ThreeParentsAdvanceOrdinaryOneToActualFourAndComposeC1() =>
        WithPressureMultiParentFailureAsync("multi_parent_three_owner", () =>
            WithPressureGroupCaseAsync(new("multi_parent_three_owner", Parents: 3, After: "strained", AdvanceExpected: true), async window =>
            {
                // The existing fixture already checked every signed/current parent, creation receipt,
                // History, graph and full contribution inventory. Do not prepare another full graph here.
                var raw = window.Files[AfterlifeSpiritualConflictState.StatePath];
                foreach (var calculation in new[] { SpiritualPressureMultiParentData.OrdinaryCalculation, SpiritualPressureMultiParentData.ActualCalculation })
                {
                    var exchange = SpiritualPairLowPressureData.ReadExchange(calculation.Input.NewStrain!);
                    var source = SpiritualPressureMultiParentData.CreateStandaloneSource(exchange, raw);
                    Assert.Equal(calculation, SpiritualWoundSourceWitness.ValidateAndReadStrainData(source, out var comparison));
                    Assert.Null(comparison);
                }
                await AssertPressureOwnedGroupActualAdmissionAsync(window, SpiritualPressureMultiParentData.OrdinaryCalculation,
                    "Three distinct applicable signed retained III parents must admit one actualIV pressure23/maxIV source and complete v6/C1, independently of ordinaryI pressure8/maxI.",
                    multiParent: true);
            }));

    /// <summary>Reports the unchanged primary and every retained cleanup error after all existing owner cleanup stages have run.</summary>
    /// <param name="name">Unique per-case identity, never a shared mutable fixture.</param>
    /// <param name="work">One existing fully owned group preparation and desired-success body.</param>
    private async Task WithPressureMultiParentFailureAsync(string name, Func<Task> work)
    {
        try { await work(); }
        catch (Exception primary)
        {
            try
            {
                var entries = primary.Data.Cast<System.Collections.DictionaryEntry>().Select(entry => new
                {
                    Key = entry.Key?.ToString(), KeyType = entry.Key?.GetType().FullName,
                    ValueType = entry.Value?.GetType().FullName,
                    Value = entry.Value is Exception retained ? retained.ToString() : entry.Value?.ToString()
                }).ToArray();
                _storageOutput.WriteLine(JsonSerializer.Serialize(new
                {
                    name, phase = "multi_parent_failure_after_cleanup", PrimaryType = primary.GetType().FullName,
                    PrimaryMessage = primary.Message, PrimaryStackTrace = primary.StackTrace, PrimaryException = primary.ToString(),
                    DataCount = primary.Data.Count, DataEntries = entries, DataEntriesComplete = entries.Length == primary.Data.Count
                }));
            }
            catch (Exception diagnosticError)
            {
                try { RetainPressureFixtureFailure(primary, "multi_parent_diagnostics", diagnosticError); }
                catch (Exception) { /* Diagnostics must not replace the original failure. */ }
            }
            throw;
        }
    }
}
