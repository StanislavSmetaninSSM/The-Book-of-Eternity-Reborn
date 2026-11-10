using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class SpiritualGuardianGroupPressureReceiptTests
{
    /// <summary>With no materializations to mask v7 presence, publication reads History exactly once and checks the final parent.</summary>
    /// <param name="change">Exact History, a missing final parent, or its independently parseable mismatched after-fingerprint.</param>
    [Theory]
    [InlineData("exact")]
    [InlineData("missing-final-parent")]
    [InlineData("mismatched-final-parent")]
    public async Task GuardianGroupReceipt_PressureOnlyPublicationReadsAllParentHistory(string change)
    {
        var inputs = CreateInputs();
        var frame = CreateFrame(inputs);
        Assert.Single(frame.Root["sources"]!.AsArray());
        Assert.All(frame.Root["decisions"]!.AsArray(), row =>
        {
            Assert.Equal("none", Text(row!["decision"])); Assert.Null(row["woundId"]); Assert.Null(row["transitionId"]);
        });
        var exact = frame.State.HasExactStrainHistory(inputs.History);
        Log("pressure_only_exact_history_premise", new { exact, receipt = frame.Root, history = inputs.Fixture.Retained.History });
        Assert.True(exact);
        var history = inputs.Fixture.Retained.History.DeepClone().AsObject();
        var rows = history["transitions"]!.AsArray();
        switch (change)
        {
            case "exact": break;
            case "missing-final-parent": rows.RemoveAt(rows.Count - 1); history["nextOrdinal"] = rows.Count + 1; break;
            case "mismatched-final-parent": rows.Last()!["afterFingerprint"] = "sha256:" + new string('d', 64); break;
            default: throw new ArgumentOutOfRangeException(nameof(change));
        }
        var historyState = change == "exact" ? inputs.History : ReadHistory(history, "pressure_only_changed_history");
        var joined = frame.State.HasExactStrainHistory(historyState);
        Log("pressure_only_history_join", new { change, joined, receipt = frame.Root, history });
        Assert.Equal(change == "exact", joined);
        var receiptBefore = frame.Root.ToJsonString(); var historyBefore = history.ToJsonString();
        var retained = frame.State.HasRetainedPressureComparisons;
        var reads = 0;
        var error = await Record.ExceptionAsync(() => CanonicalStateNormalizer.ValidateSpiritualReceiptPublicationHistoryAsync(
            frame.State, frame.Root, () => { reads++; return Task.FromResult<string?>(historyBefore); }));
        Log("pressure_only_publication_callback", new { change, retained, reads, error = error?.ToString(), receiptJson = receiptBefore, historyJson = historyBefore });
        Assert.Equal(1, reads);
        Assert.True(retained);
        if (change == "exact") Assert.Null(error);
        else Assert.IsType<InvalidDataException>(error);
        Assert.Equal(receiptBefore, frame.Root.ToJsonString()); Assert.Equal(historyBefore, history.ToJsonString());
    }

    /// <summary>A saturated N3 final parent's lawful full-description mutation passes both intrinsic readers and receipt parsing but fails unchanged History only.</summary>
    [Fact]
    public void GuardianGroupReceipt_RecordedFinalParentHistoryIsExact()
    {
        var inputs = CreateInputs("n3");
        var frame = CreateFrame(inputs);
        var baselineJoined = frame.State.HasExactStrainHistory(inputs.History);
        Log("n3_baseline_recorded_history", new { baselineJoined, receipt = frame.Root, history = inputs.Fixture.Retained.History });
        Assert.True(baselineJoined);
        var historyBefore = inputs.Fixture.Retained.History.ToJsonString();
        var originalReceipt = frame.Root.ToJsonString();
        var candidate = frame.Root.DeepClone().AsObject();
        var source = candidate["sources"]![0]!["witness"]!.AsObject();
        var row = source["strainPreimage"]!["parents"]![2]!.AsObject();
        var parent = Decode(row["parentWoundJsonBase64"]);
        parent["display"]!["description"] = "Иное полное описание последней раны Хранителя после насыщения давления.";
        var parsedParent = WoundMaterializationContract.Parse(parent.ToJsonString(), "unit.guardianGroupReceipt.changedFinalParent");
        Log("n3_lawful_final_parent_mutation", new { parent, parsedParent.IsValid, parsedParent.Issues });
        Assert.True(parsedParent.IsValid); Assert.Empty(parsedParent.Issues);
        Assert.NotEqual(inputs.Comparison.Parents[2].ParentSemanticFingerprint,
            WoundIdentityState.ComputeSemanticFingerprint(parsedParent.Wound!));
        row["parentWoundJsonBase64"] = SpiritualGuardianAttributionTestData.Encode(parent);
        SpiritualStrainReceiptJoinTests.SealSourceAndDecision(candidate, 0);
        var expectedParents = inputs.Fixture.Retained.Parents.Select(value => value.DeepClone().AsObject()).ToArray();
        expectedParents[2] = parent;
        _ = ReadSupportedSource(source, expectedParents, OrdinaryCalculation());
        var changed = ReadReceipt(candidate, "n3_resealed_final_parent_receipt");
        Assert.Equal(source.ToJsonString(), candidate["decisions"]![0]!["sourceWitness"]!.ToJsonString());
        for (var index = 0; index < 2; index++)
            Assert.Equal(inputs.Source["strainPreimage"]!["parents"]![index]!.ToJsonString(), source["strainPreimage"]!["parents"]![index]!.ToJsonString());
        var candidateBefore = candidate.ToJsonString();
        var changedJoined = changed.HasExactStrainHistory(inputs.History);
        Log("n3_final_parent_unchanged_history_join", new { changedJoined, candidate, historyJson = historyBefore });
        Assert.False(changedJoined);
        Assert.True(frame.State.HasExactStrainHistory(inputs.History));
        Assert.Equal(candidateBefore, candidate.ToJsonString()); Assert.Equal(originalReceipt, frame.Root.ToJsonString());
        Assert.Equal(historyBefore, inputs.Fixture.Retained.History.ToJsonString());
    }
}
