using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>Checks shared current/signed snapshot DATA, never live ownership or historical wound births.</summary>
public sealed class SpiritualPressureGroupViewDataTests
{
    [Fact]
    public void BatchResolvesBothCurrentAndSignedParentsWithOneViewBuildEach()
    {
        var current = SpiritualPressureGroupViewTestData.CreatePair();
        var signed = current.Copy();
        var currentBefore = Image(current);
        var signedBefore = Image(signed);
        var currentCounts = new SpiritualWoundCurrentData.ReadDiagnostics();
        var signedCounts = new SpiritualWoundCurrentData.ReadDiagnostics();

        var currentParents = ReadBatch(current, currentCounts);
        var signedParents = ReadBatch(signed, signedCounts);

        AssertValidPairAndSingularParity(current, currentParents);
        AssertValidPairAndSingularParity(signed, signedParents);
        Assert.Equal(Canonical(currentParents), Canonical(signedParents));
        Assert.Equal(currentBefore, Image(current));
        Assert.Equal(signedBefore, Image(signed));

        // This is deliberately last: real per-parent rebuilds must fail only after all DATA premises pass.
        Assert.Equal(new[] { 1, 1, 1, 1, 2, 1, 1, 1, 1, 2 },
            Counts(currentCounts).Concat(Counts(signedCounts)).ToArray());
    }

    [Fact]
    public void BatchAcceptsPresignParentOrderPermutationWithSingularParity()
    {
        var original = SpiritualPressureGroupViewTestData.CreatePair();
        var permuted = original.Copy();
        var wounds = permuted.Profiles["profiles"]![0]!["activeWounds"]!.AsArray();
        var first = wounds[0]!.DeepClone();
        var second = wounds[1]!.DeepClone();
        wounds.Clear();
        wounds.Add(second);
        wounds.Add(first);
        var entries = permuted.Identity["entries"]!.AsArray();
        first = entries[0]!.DeepClone();
        second = entries[1]!.DeepClone();
        entries.Clear();
        entries.Add(second);
        entries.Add(first);
        // Each basis is authored before its independent copy. Ordered History is deliberately unchanged.
        var signedPermuted = permuted.Copy();
        var before = Image(permuted);

        var expected = ReadBatch(original);
        var actual = ReadBatch(permuted);
        var signed = ReadBatch(signedPermuted);

        AssertValidPairAndSingularParity(permuted, actual);
        AssertValidPairAndSingularParity(signedPermuted, signed);
        Assert.Equal(Canonical(expected), Canonical(actual));
        Assert.Equal(Canonical(actual), Canonical(signed));
        Assert.Equal(before, Image(permuted));
        Assert.Equal(original.History.ToJsonString(), permuted.History.ToJsonString());
    }

    [Fact]
    public void BatchRefusesExactDuplicateWoundIdentityWithSingularParity()
    {
        var data = ValidBaseline();
        data.Identity["entries"]!.AsArray().Add(data.Identity["entries"]![1]!.DeepClone());
        var parsed = WoundIdentityState.Parse(data.Identity.ToJsonString(), WoundIdentityState.StatePath);
        Assert.False(parsed.IsValid);
        Assert.Contains(parsed.Issues, issue => issue.Code == "wound_identity_duplicate_id");

        AssertBothBasesRefuse(data);
    }

    [Fact]
    public void BatchRefusesGlobalCaseAndConfusableIdentityCollisionsWithSingularParity()
    {
        foreach (var collision in new[] { "WOUND_PRESSURE_VIEW_A", "wound_pressure_view_\u0410" })
        {
            var data = ValidBaseline();
            var unrelated = data.Identity["entries"]![0]!.DeepClone();
            unrelated["woundId"] = collision;
            unrelated["ownerId"] = "foreign_soul";
            data.Identity["entries"]!.AsArray().Add(unrelated);
            var parsed = WoundIdentityState.Parse(data.Identity.ToJsonString(), WoundIdentityState.StatePath);
            Assert.False(parsed.IsValid);
            Assert.Contains(parsed.Issues, issue => issue.Code == "wound_identity_confusable_id");

            // Neither colliding row is requested. Global identity validation must precede filtering.
            AssertBothBasesRefuse(data);
        }
    }

    [Fact]
    public void BatchRefusesDuplicateStrainProfileOperationInSecondOwnedGraphWithSingularParity()
    {
        var data = ValidBaseline();
        var parent = data.Parent(SpiritualPressureGroupViewTestData.SecondWoundId);
        var components = parent["consequences"]!["ownedEffectSources"]!["definitions"]![0]!["components"]!.AsArray();
        var duplicate = components[0]!.DeepClone();
        duplicate["componentId"] = "component_002";
        components[1] = duplicate;
        parent["consequences"]!["entries"]![1]!["profileKey"] = "spiritual_strain_burden";
        var parsed = WoundMaterializationContract.Parse(parent.ToJsonString(), "unit.pressureView.duplicateGraph");
        Assert.False(parsed.IsValid);
        Assert.Contains(parsed.Issues, issue => issue.Code == "wound_consequence_duplicate_coordinate");

        AssertBothBasesRefuse(data);
    }

    [Fact]
    public void BatchRefusesMissingSecondIdentityCatalogAgreementWithSingularParity()
    {
        var data = ValidBaseline();
        data.Identity["entries"]!.AsArray().RemoveAt(1);
        AssertValidParsesAndAgreementIssue(data, "wound_history_identity_missing");

        // Both paths must reject the malformed complete view, regardless of which agreement fails first.
        AssertBothBasesRefuse(data);
    }

    [Fact]
    public void BatchRefusesForeignSecondOwnerWithoutReturningFirstParent()
    {
        var data = SpiritualPressureGroupViewTestData.CreatePair(secondOwnerId: "foreign_soul");
        Assert.Equal(SpiritualPressureGroupViewTestData.FirstWoundId,
            ReadSingular(data, SpiritualPressureGroupViewTestData.FirstWoundId).WoundId);
        Assert.Equal("foreign_soul", ReadSingular(data, SpiritualPressureGroupViewTestData.SecondWoundId,
            "player_soul:foreign_soul").Owner.OwnerId);
        foreach (var basis in new[] { data, data.Copy() })
        {
            var before = Image(basis);
            Assert.Throws<InvalidOperationException>(() => ReadBatch(basis));
            Assert.Equal(SpiritualPressureGroupViewTestData.FirstWoundId,
                ReadSingular(basis, SpiritualPressureGroupViewTestData.FirstWoundId).WoundId);
            Assert.Throws<InvalidOperationException>(() =>
                ReadSingular(basis, SpiritualPressureGroupViewTestData.SecondWoundId));
            Assert.Equal("foreign_soul", ReadSingular(basis, SpiritualPressureGroupViewTestData.SecondWoundId,
                "player_soul:foreign_soul").Owner.OwnerId);
            Assert.Equal(before, Image(basis));
        }
    }

    [Fact]
    public void BatchRefusesStaleSecondHistoryWithSingularParity()
    {
        var data = ValidBaseline();
        data.History["transitions"]![1]!["afterFingerprint"] = "sha256:" + new string('c', 64);
        AssertValidParsesAndAgreementIssue(data, "wound_history_latest_fingerprint_mismatch");

        AssertBothBasesRefuse(data);
    }

    [Fact]
    public void BatchResultsRemainDetachedFromCallerMutation()
    {
        var current = ValidBaseline();
        var signed = current.Copy();
        var before = Image(current);
        var results = ReadBatch(current);
        AssertValidPairAndSingularParity(current, results);
        var original = Canonical(results);
        var exported = JsonNode.Parse(original[1])!.AsObject();
        exported["display"]!["name"] = "Caller-mutated detached DATA";
        exported["consequences"]!["ownedEffectSources"]!["definitions"]![0]!["display"]!["description"] = "Caller copy";
        var changed = WoundMaterializationContract.Parse(exported.ToJsonString(), "unit.pressureView.detachedResult");
        Assert.True(changed.IsValid, string.Join("\n", changed.Issues));
        if (results is IList<WoundMaterializationEnvelope> list)
        {
            try { list[1] = changed.Wound!; }
            catch (NotSupportedException) { Assert.Equal(original[1], Canonical(results)[1]); }
        }
        Assert.Equal(before, Image(current));
        Assert.Equal(original, Canonical(ReadBatch(current)));
        Assert.Equal(original, Canonical(ReadBatch(signed)));

        var retained = ReadBatch(current);
        current.Parent(SpiritualPressureGroupViewTestData.SecondWoundId)["display"]!["name"] = "Changed caller input";
        Assert.Equal(original, Canonical(retained));
        Assert.Throws<InvalidOperationException>(() => ReadBatch(current));
        AssertValidPairAndSingularParity(signed, ReadBatch(signed));
        Assert.Equal(before, Image(signed));
    }

    /// <summary>Establishes real singular and batch DATA success before a negative's one deliberate mutation.</summary>
    private static SpiritualPressureGroupViewData ValidBaseline()
    {
        var data = SpiritualPressureGroupViewTestData.CreatePair();
        AssertValidPairAndSingularParity(data, ReadBatch(data));
        return data;
    }

    /// <summary>Calls the proposed shared production operation without substituting its validation algorithm.</summary>
    private static IReadOnlyList<WoundMaterializationEnvelope> ReadBatch(SpiritualPressureGroupViewData data,
        SpiritualWoundCurrentData.ReadDiagnostics? diagnostics = null) => SpiritualWoundCurrentData.ReadActiveBatch(
        data.Carriers, data.Identity.ToJsonString(), data.History.ToJsonString(), "chaos_sea", data.WoundIds,
        "player_soul:player_soul", diagnostics);

    /// <summary>Uses the retained singular production path on the exact same DATA basis.</summary>
    private static WoundMaterializationEnvelope ReadSingular(SpiritualPressureGroupViewData data, string woundId,
        string actor = "player_soul:player_soul") => SpiritualWoundCurrentData.ReadActive(data.Carriers,
        data.Identity.ToJsonString(), data.History.ToJsonString(), "chaos_sea", woundId, actor);

    /// <summary>Compares complete envelopes and legal shared-root siblings, rather than only wound identities.</summary>
    private static void AssertValidPairAndSingularParity(SpiritualPressureGroupViewData data,
        IReadOnlyList<WoundMaterializationEnvelope> actual)
    {
        Assert.Equal(data.WoundIds, actual.Select(wound => wound.WoundId).ToArray());
        for (var index = 0; index < data.WoundIds.Count; index++)
        {
            var expected = WoundMaterializationContract.Parse(data.Parent(data.WoundIds[index]).ToJsonString(),
                "unit.pressureView.expected");
            Assert.True(expected.IsValid, string.Join("\n", expected.Issues));
            Assert.Equal(WoundMaterializationContract.SerializeCanonical(expected.Wound!),
                WoundMaterializationContract.SerializeCanonical(actual[index]));
            Assert.Equal(WoundMaterializationContract.SerializeCanonical(ReadSingular(data, data.WoundIds[index])),
                WoundMaterializationContract.SerializeCanonical(actual[index]));
            Assert.Equal(3, actual[index].Severity.Rank);
            Assert.Equal(3, actual[index].Consequences.Entries.Count);
            Assert.Single(actual[index].Consequences.OwnedEffectSources.RootBindings);
            Assert.Single(actual[index].Consequences.OwnedEffectSources.Definitions);
            Assert.Equal(3, actual[index].Consequences.OwnedEffectSources.Definitions[0].GetProperty("components").GetArrayLength());
            Assert.Single(actual[index].Consequences.Entries.Select(entry => entry.EffectId).Distinct(StringComparer.Ordinal));
        }
    }

    /// <summary>Requires both reader paths to reject independent malformed DATA copies without pinning incidental first-error order.</summary>
    private static void AssertBothBasesRefuse(SpiritualPressureGroupViewData data)
    {
        foreach (var basis in new[] { data, data.Copy() })
        {
            var before = Image(basis);
            Assert.Throws<InvalidOperationException>(() => ReadBatch(basis));
            Assert.Throws<InvalidOperationException>(() =>
                ReadSingular(basis, SpiritualPressureGroupViewTestData.FirstWoundId));
            Assert.Throws<InvalidOperationException>(() =>
                ReadSingular(basis, SpiritualPressureGroupViewTestData.SecondWoundId));
            Assert.Equal(before, Image(basis));
        }
    }

    /// <summary>Establishes the exact global agreement fault directly while excluding an earlier fixture parser failure.</summary>
    private static void AssertValidParsesAndAgreementIssue(SpiritualPressureGroupViewData data, string code)
    {
        var catalog = WoundCarrierCatalog.Build(data.Carriers);
        Assert.Empty(catalog.Issues);
        var identity = WoundIdentityState.Parse(data.Identity.ToJsonString(), WoundIdentityState.StatePath);
        var history = WoundHistoryState.Parse(data.History.ToJsonString(), WoundHistoryState.HistoryPath);
        Assert.True(identity.IsValid, string.Join("\n", identity.Issues));
        Assert.True(history.IsValid, string.Join("\n", history.Issues));
        Assert.Contains(history.State!.ValidateAgreement(identity.State!, catalog), issue => issue.Code == code);
    }

    /// <summary>Captures exact fixture bytes to detect unintended DATA mutation.</summary>
    private static string Image(SpiritualPressureGroupViewData data) =>
        data.Profiles.ToJsonString() + "\n" + data.Identity.ToJsonString() + "\n" + data.History.ToJsonString();

    /// <summary>Uses the real complete-envelope codec for comparison, including every graph and History coordinate.</summary>
    private static string[] Canonical(IReadOnlyList<WoundMaterializationEnvelope> parents) =>
        parents.Select(WoundMaterializationContract.SerializeCanonical).ToArray();

    /// <summary>Separates actual full-source work from exact parent resolution attempts; none denotes physical IO.</summary>
    private static int[] Counts(SpiritualWoundCurrentData.ReadDiagnostics counts) => new[]
    {
        counts.CatalogBuildAttempts, counts.IdentityParseAttempts, counts.HistoryParseAttempts,
        counts.HistoryAgreementAttempts, counts.ParentResolutionAttempts
    };
}
