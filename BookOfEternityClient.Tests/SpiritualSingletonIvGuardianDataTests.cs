using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>Complete pre-sign guardian III/IV DATA prerequisites and the desired existing-v5 singleton IV contract.</summary>
public sealed class SpiritualSingletonIvGuardianDataTests(ITestOutputHelper output)
{
    /// <summary>Requires full exact expected-rank graph and independent synthetic source/History/receipt joins for III and IV.</summary>
    /// <param name="rank">Independent expected rank supplied to both fixture and full checker.</param>
    [Theory]
    [InlineData(3)] [InlineData(4)]
    public void GuardianSingletonData_CompleteExpectedRankPrerequisites(int rank)
    {
        var fixture = SpiritualGuardianAttributionTestData.Create(rank: rank);
        AssertPrerequisites(fixture, rank);
        if (rank == 3)
        {
            var unchanged = SpiritualGuardianAttributionTestData.Create();
            Assert.Equal(unchanged.Source.ToJsonString(), fixture.Source.ToJsonString());
            Assert.Equal(unchanged.Profiles.ToJsonString(), fixture.Profiles.ToJsonString());
            Assert.Equal(unchanged.Retained.Receipts.ToJsonString(), fixture.Retained.Receipts.ToJsonString());
            Assert.Equal(unchanged.RawInput, fixture.RawInput);
        }
        var carriers = new EffectCarrierCatalogInput(null, null, null, null, fixture.Profiles, fixture.Conflict);
        var snapshot = EffectMechanicsSnapshot.Build(new(carriers, fixture.Retained.EffectIdentities));
        Assert.True(snapshot.IsAccepted, JsonSerializer.Serialize(snapshot.Issues));
        var roots = SpiritualGuardianAttributionDataCheck.Roots(fixture.Retained, fixture.Profiles, fixture.Conflict);
        var sources = EffectAcceptedTurnInputComposer.BuildCanonicalSourceAuthority(roots);
        var targets = EffectAcceptedTurnInputComposer.BuildCanonicalTargetAuthority(carriers, roots);
        var all = SpiritualWoundConflictContributionProjector.Project(snapshot, sources, targets, fixture.Conflict);
        Assert.True(all.IsAccepted, JsonSerializer.Serialize(all.Issues)); Assert.Equal(rank, all.Contributions.Count);
        var selected = Assert.Single(all.Contributions, row => row.Profile == "spiritual_strain_burden");
        var isolated = snapshot with { Components = snapshot.Components.Where(row => row.EffectId != selected.EffectId || row.ComponentId != selected.ComponentId).ToArray() };
        var ordinary = SpiritualWoundConflictContributionProjector.Project(isolated, sources, targets, fixture.Conflict);
        Assert.True(ordinary.IsAccepted, JsonSerializer.Serialize(ordinary.Issues));
        Assert.Equal(rank - 1, ordinary.Contributions.Count);
        Assert.Equal(all.Contributions.Where(row => row.ComponentId != selected.ComponentId).Select(row => JsonSerializer.Serialize(row)),
            ordinary.Contributions.Select(row => JsonSerializer.Serialize(row)));
    }

    /// <summary>The pre-sign checker rejects a fully lawful other-rank graph instead of treating actual rank as expected rank.</summary>
    /// <param name="authoredRank">Rank of a complete otherwise-valid graph.</param>
    /// <param name="expectedRank">Different independent caller expectation.</param>
    [Theory]
    [InlineData(3, 4)] [InlineData(4, 3)]
    public void GuardianSingletonData_CheckerRejectsDifferentExpectedRank(int authoredRank, int expectedRank)
    {
        var fixture = SpiritualGuardianAttributionTestData.Create(rank: authoredRank); AssertPrerequisites(fixture, authoredRank);
        var issues = SpiritualGuardianAttributionDataCheck.Check(fixture.Retained, fixture.Profiles, fixture.Conflict, expectedRank);
        Assert.Contains(issues, issue => issue.FilePath == "guardianFixture.parent.owner" && issue.Code == "guardian_fixture_join_mismatch");
        Assert.Contains(issues, issue => issue.FilePath == "guardianFixture.receipt.source");
    }

    /// <summary>Desired real witness/direct v5 intrinsic success for complete IV, with the guardian declaration and ordinary arithmetic unchanged.</summary>
    [Fact]
    public void GuardianSingletonIv_VersionFiveIntrinsicSucceeds()
    {
        var fixture = SpiritualGuardianAttributionTestData.Create(rank: 4); AssertPrerequisites(fixture, 4);
        var source = fixture.Source; var original = source.ToJsonString();
        SpiritualStrainPreimageData? retained = null; SpiritualWoundCalculation? actual = null;
        var error = Record.Exception(() => actual = SpiritualWoundSourceWitness.ValidateAndReadStrainData(source, out retained));
        output.WriteLine(JsonSerializer.Serialize(new { phase = "singleton_iv_guardian_intrinsic_v5", error = error?.ToString(), sourceJson = original,
            actual, comparisonReturned = retained is not null }));
        Assert.Equal(original, source.ToJsonString()); Assert.Null(error);
        var data = Assert.IsType<SpiritualStrainPreimageData>(retained);
        Assert.Equal(5, data.Version); Assert.Single(data.Parents); Assert.Null(data.ParentSourceId);
        Assert.Equal((4, "guardian", SpiritualGuardianAttributionTestData.GuardianId), (data.ParentWound.Severity.Rank, data.ParentWound.Owner.OwnerKind, data.ParentWound.Owner.OwnerId));
        Assert.Equal((15L, 2), (actual!.TraumaPressure, actual.MaximumSeverityRank));
        Assert.Equal((10L, 1), (data.OrdinaryCalculation!.TraumaPressure, data.OrdinaryCalculation.MaximumSeverityRank));
        Assert.Same(actual, data.ActualCalculation);
        Assert.True(JsonNode.DeepEquals(fixture.Retained.Parents.Single(), JsonNode.Parse(WoundMaterializationContract.SerializeCanonical(data.ParentWound))));
        Assert.Equal(fixture.Retained.Effects.Single()["effectId"]!.GetValue<string>(), data.EffectId); Assert.Equal("component_001", data.ComponentId);
        Assert.Equal(new CanonicalBeforeImage(true, fixture.RawInput).Fingerprint, data.OriginalInputFingerprint);
        Assert.Equal("/activeConflict/exchangeLog/0", data.OriginalExchangePointer);
        Assert.True(JsonNode.DeepEquals(fixture.Actual, data.ReadActualExchange()));
        Assert.True(JsonNode.DeepEquals(fixture.Ordinary["after"], data.ReadOrdinaryAfter(fixture.Actual)));
        Assert.True(JsonNode.DeepEquals(SpiritualGuardianAttributionTestData.FailedAction(), data.ReadActualExchange()["spiritualWoundFailedAction"]));
        var direct = SpiritualStrainPreimageData.ReadIntrinsic(source, fixture.Actual);
        Assert.Equal(data.Version, direct.Version); Assert.Equal(4, direct.ParentWound.Severity.Rank); Assert.Equal(actual, direct.ActualCalculation);
        data.ReadActualExchange()["after"]!["oppositionSideStrain"] = "broken";
        data.ReadOrdinaryAfter(fixture.Actual)["oppositionSideStrain"] = "clear";
        Assert.True(JsonNode.DeepEquals(fixture.Actual, data.ReadActualExchange()));
        Assert.True(JsonNode.DeepEquals(fixture.Ordinary["after"], data.ReadOrdinaryAfter(fixture.Actual))); Assert.Equal(original, source.ToJsonString());
    }

    /// <summary>Proves full retained graph and both ordinary/actual calculations without invoking the missing IV comparison gate.</summary>
    private static void AssertPrerequisites(SpiritualGuardianAttributionFixture fixture, int rank)
    {
        var issues = SpiritualGuardianAttributionDataCheck.Check(fixture.Retained, fixture.Profiles, fixture.Conflict, rank);
        Assert.True(issues.Count == 0, JsonSerializer.Serialize(issues));
        var checks = new List<ValidationIssue>();
        ValidationService.ValidateRetainedSpiritualExchangeShape(fixture.Ordinary, "singleton.guardian.ordinary", checks);
        ValidationService.ValidateSpiritualWoundSourceActionShape(fixture.Ordinary, "singleton.guardian.ordinary", checks);
        ValidationService.ValidateRetainedSpiritualDiceAudit(fixture.Ordinary, fixture.Source["turnEvidence"]!.AsObject(), checks);
        ValidationService.ValidateRetainedSpiritualExchangeRules(fixture.Ordinary, checks);
        Assert.True(checks.Count == 0, JsonSerializer.Serialize(checks));
        var ordinarySource = SpiritualGuardianAttributionTestData.CreateSource(fixture.Ordinary, "ordinary-session", "ordinary-request", "ordinary-snapshot",
            "ordinary-conflict", new CanonicalBeforeImage(true, fixture.RawInput).Fingerprint);
        var ordinary = SpiritualWoundSourceWitness.ValidateAndReadStrainData(ordinarySource, out var noOrdinary); Assert.Null(noOrdinary);
        Assert.Equal((1, 2, 10L, 1), (ordinary.PreviousStrainRank, ordinary.NewStrainRank, ordinary.TraumaPressure, ordinary.MaximumSeverityRank));
        var actualSource = fixture.Source.DeepClone().AsObject(); Assert.True(actualSource.Remove("strainPreimage")); SpiritualExistingPressureTestData.Seal(actualSource);
        var actual = SpiritualWoundSourceWitness.ValidateAndReadStrainData(actualSource, out var noActual); Assert.Null(noActual);
        Assert.Equal((1, 3, 15L, 2), (actual.PreviousStrainRank, actual.NewStrainRank, actual.TraumaPressure, actual.MaximumSeverityRank));
        Assert.Equal(("pressure", "success", 8), (fixture.Actual["operationType"]!.GetValue<string>(), fixture.Actual["outcome"]!.GetValue<string>(), fixture.Source["harmfulMargin"]!.GetValue<int>()));
        Assert.True(JsonNode.DeepEquals(SpiritualGuardianAttributionTestData.FailedAction(), fixture.Ordinary["spiritualWoundFailedAction"]));
        var expected = fixture.Ordinary.DeepClone(); expected["after"]!["oppositionSideStrain"] = "overwhelmed";
        Assert.True(JsonNode.DeepEquals(expected, fixture.Actual));
        Assert.True(fixture.RawInput.AsSpan().StartsWith(Encoding.UTF8.GetPreamble()));
        Assert.True(JsonNode.DeepEquals(fixture.Conflict, SpiritualWoundStateJson.Parse(SpiritualWoundStateJson.DecodeUtf8JsonText(fixture.RawInput))));
        Assert.Equal(new CanonicalBeforeImage(true, fixture.RawInput).Fingerprint, fixture.Source["strainPreimage"]!["originalInput"]!["contentFingerprint"]!.GetValue<string>());
    }
}
