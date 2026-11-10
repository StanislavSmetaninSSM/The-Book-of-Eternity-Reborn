using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class SpiritualPressurePairReceiptTests
{
    /// <summary>Zero materializations still require one History read covering both recorded v4 parents.</summary>
    [Theory]
    [InlineData("exact")]
    [InlineData("missing-history")]
    [InlineData("malformed-history")]
    [InlineData("missing-second-anchor")]
    [InlineData("second-fingerprint")]
    public async Task PairPublicationRequiresOneHistoryReadWithoutMaterializations(string change)
    {
        var source = CreateSupportedSource();
        var root = CreateLedger(source); var state = ReadLedger(root);
        Assert.All(root["decisions"]!.AsArray(), row => Assert.Equal("none", row!["decision"]!.GetValue<string>()));
        Assert.All(root["decisions"]!.AsArray(), row => Assert.Null(row!["woundId"]));
        var history = CreateHistory(source);
        Assert.True(state.HasExactStrainHistory(ReadHistory(history)));
        if (change == "missing-second-anchor")
        {
            history["transitions"]!.AsArray().RemoveAt(1); RenumberHistory(history);
            Assert.False(state.HasExactStrainHistory(ReadHistory(history)));
        }
        if (change == "second-fingerprint")
        {
            history["transitions"]![1]!["afterFingerprint"] = "sha256:" + new string('d', 64);
            Assert.False(state.HasExactStrainHistory(ReadHistory(history)));
        }
        var receiptBefore = root.ToJsonString(); var historyBefore = history.ToJsonString(); var reads = 0;
        Task Act() => CanonicalStateNormalizer.ValidateSpiritualReceiptPublicationHistoryAsync(state, root, () =>
        {
            reads++;
            string? text = change switch
            {
                "missing-history" => null,
                "malformed-history" => "{",
                _ => history.ToJsonString()
            };
            return Task.FromResult(text);
        });
        if (change == "exact") await Act();
        else await Assert.ThrowsAsync<InvalidDataException>(Act);
        Assert.Equal(1, reads);
        Assert.Equal(receiptBefore, root.ToJsonString()); Assert.Equal(historyBefore, history.ToJsonString());
    }

    /// <summary>Ordinary and v2 declined controls introduce no new History callback after supported pair DATA is proved.</summary>
    [Theory]
    [InlineData("ordinary")]
    [InlineData("v2-positive")]
    [InlineData("v2-zero-table")]
    public async Task PairPublicationPreservesNonPressureZeroReadControls(string contour)
    {
        var pair = CreateSupportedSource();
        var pairState = ReadLedger(CreateLedger(pair));
        Assert.True(pairState.HasExactStrainHistory(ReadHistory(CreateHistory(pair))));
        JsonObject root;
        if (contour == "v2-zero-table")
            root = SpiritualZeroStrainReceiptTests.CreateLedger("zero-only").Next;
        else
        {
            var source = contour == "ordinary" ? pair.DeepClone().AsObject()
                : SpiritualExistingStrainPreimageTests.CreateData(3).Source;
            if (contour == "ordinary") { Assert.True(source.Remove("strainPreimage")); Seal(source); }
            Assert.True(SpiritualWoundSourceWitness.Validate(source).MaximumSeverityRank > 0);
            root = CreateLedger(source);
        }
        var state = ReadLedger(root); Assert.False(state.HasRetainedPressureComparisons);
        var before = root.ToJsonString(); var reads = 0;
        await CanonicalStateNormalizer.ValidateSpiritualReceiptPublicationHistoryAsync(state, root, () =>
        {
            reads++; return Task.FromResult<string?>(null);
        });
        Assert.Equal(0, reads); Assert.Equal(before, root.ToJsonString());
    }

    /// <summary>The v2 zero-ceiling table remains valid but cannot accept an otherwise valid positive v4 comparison.</summary>
    [Fact]
    public void PairReceiptKeepsPositiveV4OutOfV2ZeroComparisonTable()
    {
        var pair = CreateSupportedSource();
        var pairState = ReadLedger(CreateLedger(pair));
        Assert.True(pairState.HasExactStrainHistory(ReadHistory(CreateHistory(pair))));
        var root = SpiritualZeroStrainReceiptTests.CreateLedger("zero-only").Next;
        var comparison = root["strainComparisons"]![0]!["witness"]!.AsObject();
        var calculation = SpiritualWoundSourceWitness.ValidateAndReadStrainData(comparison, out var data);
        Assert.Equal((7L, 0, true), (calculation.TraumaPressure, calculation.MaximumSeverityRank, calculation.HasIncreasingStrain));
        Assert.Equal(2, Assert.IsType<SpiritualStrainPreimageData>(data).Version);
        _ = ReadLedger(root);
        Assert.Empty(root["sources"]!.AsArray()); Assert.Empty(root["decisions"]!.AsArray());
        root["strainComparisons"]![0]!["witness"] = pair.DeepClone();
        AssertSupported(root["strainComparisons"]![0]!["witness"]!.AsObject());
        AssertInvalidLedger(root);
    }
}
