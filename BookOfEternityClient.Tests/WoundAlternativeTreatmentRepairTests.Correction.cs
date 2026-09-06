using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class WoundAlternativeTreatmentRepairTests
{
    [Theory]
    [InlineData("requirements", false)]
    [InlineData("requirements", true)]
    [InlineData("outcomes", false)]
    [InlineData("outcomes", true)]
    public void Correction_RawAndSanitizedMemberArraysKeepOriginalCoordinates(string member, bool redacted)
    {
        var original = CreateAuthor();
        var array = original["route"]![member]!.AsArray();
        if (member == "requirements")
            array.Add(new JsonObject { ["kind"] = "facility", ["facilityRef"] = "field_table" });
        var first = array[0]!.DeepClone();
        var third = array[2]!.DeepClone();
        array[0] = redacted ? JsonValue.Create("private_array_value") : null;
        if (redacted) original["route"]!["gmPrivateNotes"] = "private_array_value";
        if (member == "requirements") array[1]!["capabilityRef"] = " bad_id ";
        else array[1]!["bandId"] = " bad_id ";
        var packet = Assert.Single(WoundRepairPacketBuilder.Build(Request(original)));
        Assert.Equal("route." + member + "[0]", Assert.Single(packet.Issues).Path);
        var corrected = original.DeepClone().AsObject();
        corrected["route"]!.AsObject().Remove("gmPrivateNotes");
        corrected["route"]![member]![0] = first;
        var progress = packet.AnalyzeAlternativeTreatmentCorrection(corrected);
        Assert.Equal(WoundAlternativeTreatmentCorrectionStatus.NeedsAnotherRepair, progress.Status);
        Assert.Null(progress.CompleteDraft);
        var next = Assert.Single(WoundRepairPacketBuilder.Build(Request(corrected)));
        Assert.Contains(next.Issues, issue => issue.Path.StartsWith("route." + member + "[1]", StringComparison.Ordinal));
        if (member == "requirements") corrected["route"]![member]![1]!["capabilityRef"] = "field_medicine";
        else corrected["route"]![member]![1]!["bandId"] = "clean_partial";
        Assert.True(next.MatchesCorrectedAlternativeTreatmentAuthoring(corrected));
        Assert.True(JsonNode.DeepEquals(third, corrected["route"]![member]![2]));
        corrected["route"]![member]!.AsArray().RemoveAt(2);
        Assert.False(next.MatchesCorrectedAlternativeTreatmentAuthoring(corrected));
    }

    [Fact]
    public void Correction_WholeArrayFaultPermitsOnlyThatArrayAndRejectsCanonicalComplicationSelector()
    {
        var original = CreateAuthor();
        original["diagnosisPath"]!["requiresKnownFacts"] = "not an array";
        var packet = Assert.Single(WoundRepairPacketBuilder.Build(Request(original)));
        Assert.Equal("diagnosisPath.requiresKnownFacts", Assert.Single(packet.Issues).Path);
        var corrected = original.DeepClone().AsObject();
        corrected["diagnosisPath"]!["requiresKnownFacts"] = new JsonArray("route:clean_and_suture");
        Assert.True(packet.MatchesCorrectedAlternativeTreatmentAuthoring(corrected));
        corrected["route"]!["outcomes"]![0]!["result"] = new JsonArray(new JsonObject
            { ["kind"] = "remove_complication", ["complicationId"] = "canonical_not_gm_ref" });
        Assert.False(packet.MatchesCorrectedAlternativeTreatmentAuthoring(corrected));
    }

    [Theory]
    [InlineData("difficulty")]
    [InlineData("hidden_null")]
    public void Correction_StagesModeDependentErrorsWithoutAcceptingIntermediate(string axis)
    {
        var original = CreateAuthor();
        original["route"]!["mode"] = "unsupported_mode";
        if (axis == "difficulty") original["route"]!["resolution"]!["difficulty"] = -1;
        else original["diagnosisPath"] = null;
        var packet = Assert.Single(WoundRepairPacketBuilder.Build(Request(original)));
        Assert.Single(packet.Issues);
        var corrected = original.DeepClone().AsObject();
        corrected["route"]!["mode"] = "procedure";
        var analysis = packet.AnalyzeAlternativeTreatmentCorrection(corrected);
        Assert.Equal(WoundAlternativeTreatmentCorrectionStatus.NeedsAnotherRepair, analysis.Status);
        Assert.Null(analysis.CompleteDraft);
        Assert.NotNull(analysis.CorrectedAuthor);
        Assert.False(packet.MatchesCorrectedAlternativeTreatmentAuthoring(corrected));
        var next = Assert.Single(WoundRepairPacketBuilder.Build(Request(corrected)));
        if (axis == "difficulty") corrected["route"]!["resolution"]!["difficulty"] = 15;
        else corrected["diagnosisPath"] = CreateAuthor()["diagnosisPath"]!.DeepClone();
        Assert.True(next.MatchesCorrectedAlternativeTreatmentAuthoring(corrected));
    }

    [Fact]
    public void Correction_RawFactsKeepNullHolesAndRevealLaterBadFactAtOriginalIndex()
    {
        var original = CreateAuthor();
        original["diagnosisPath"]!["reveals"] = new JsonArray(null, "bogus", "route:alternative_hidden_route");
        var packet = Assert.Single(WoundRepairPacketBuilder.Build(Request(original)));
        Assert.Equal("diagnosisPath.reveals[0]", Assert.Single(packet.Issues).Path);
        Assert.Equal(3, packet.PreservedProposal["diagnosisPath"]!["reveals"]!.AsArray().Count);
        var corrected = original.DeepClone().AsObject();
        corrected["diagnosisPath"]!["reveals"]![0] = "route:old_fact";
        var analysis = packet.AnalyzeAlternativeTreatmentCorrection(corrected);
        Assert.Equal(WoundAlternativeTreatmentCorrectionStatus.NeedsAnotherRepair, analysis.Status);
        Assert.Contains(analysis.Issues, issue => issue.FilePath == Prefix + ".diagnosisPath.reveals[1]");
        var next = Assert.Single(WoundRepairPacketBuilder.Build(Request(corrected)));
        corrected["diagnosisPath"]!["reveals"]![1] = "route:other_fact";
        Assert.True(next.MatchesCorrectedAlternativeTreatmentAuthoring(corrected));
        corrected["diagnosisPath"]!["reveals"]![2] = "route:unrelated";
        Assert.False(next.MatchesCorrectedAlternativeTreatmentAuthoring(corrected));
    }

    [Theory]
    [InlineData("displayName")]
    [InlineData("visibility")]
    [InlineData("reveals")]
    public void Correction_NarrowsPairingToMemberOrAppendOnlyFact(string axis)
    {
        var original = CreateAuthor();
        var path = original["diagnosisPath"]!.AsObject();
        if (axis == "displayName") path.Remove(axis);
        if (axis == "visibility") path[axis] = "gm_only";
        if (axis == "reveals") path[axis] = new JsonArray("route:old_fact", "complication:old_complication");
        var packet = Assert.Single(WoundRepairPacketBuilder.Build(Request(original)));
        Assert.DoesNotContain(packet.Issues, issue => issue.Path == "diagnosisPath");
        var corrected = original.DeepClone().AsObject();
        if (axis == "reveals") corrected["diagnosisPath"]![axis]!.AsArray().Add("route:alternative_hidden_route");
        else corrected["diagnosisPath"]![axis] = CreateAuthor()["diagnosisPath"]![axis]!.DeepClone();
        Assert.True(packet.MatchesCorrectedAlternativeTreatmentAuthoring(corrected));
        foreach (var sibling in new[] { "diagnosisPathId", "requiresKnownFacts", "requirements" })
        {
            var wrong = corrected.DeepClone().AsObject();
            wrong["diagnosisPath"]![sibling] = null;
            Assert.False(packet.MatchesCorrectedAlternativeTreatmentAuthoring(wrong));
        }
        if (axis == "reveals")
        {
            var facts = corrected["diagnosisPath"]![axis]!.AsArray();
            facts[0] = "route:changed_prefix";
            Assert.False(packet.MatchesCorrectedAlternativeTreatmentAuthoring(corrected));
        }
    }

    [Fact]
    public void Correction_RequiredReplacementAndUnknownOmissionPreserveOrderedSiblings()
    {
        var original = CreateAuthor();
        original["route"]!.AsObject().Remove("displayName");
        original["route"]!["unknownAuthoredField"] = "remove this";
        var packet = Assert.Single(WoundRepairPacketBuilder.Build(Request(original)));
        var corrected = original.DeepClone().AsObject();
        corrected["route"]!.AsObject().Remove("unknownAuthoredField");
        corrected["route"]!["displayName"] = CreateAuthor()["route"]!["displayName"]!.DeepClone();
        Assert.True(packet.MatchesCorrectedAlternativeTreatmentAuthoring(corrected));
        corrected["route"]!.AsObject().Remove("displayName");
        Assert.False(packet.MatchesCorrectedAlternativeTreatmentAuthoring(corrected));
    }

    [Fact]
    public void Correction_TwoLeafErrorsCannotShiftOrRewriteThirdArraySibling()
    {
        var original = CreateAuthor();
        original["diagnosisPath"]!["reveals"] = new JsonArray("bad_first", "bad_second", "route:alternative_hidden_route");
        var packet = Assert.Single(WoundRepairPacketBuilder.Build(Request(original)));
        Assert.Equal(new[] { "diagnosisPath.reveals[0]", "diagnosisPath.reveals[1]" }, packet.Issues.Select(issue => issue.Path));
        var preserved = packet.PreservedProposal["diagnosisPath"]!["reveals"]!.AsArray();
        Assert.Null(preserved[0]); Assert.Null(preserved[1]); Assert.Equal(3, preserved.Count);
        var corrected = original.DeepClone().AsObject();
        corrected["diagnosisPath"]!["reveals"]![0] = "route:first";
        corrected["diagnosisPath"]!["reveals"]![1] = "route:second";
        Assert.True(packet.MatchesCorrectedAlternativeTreatmentAuthoring(corrected));
        corrected["diagnosisPath"]!["reveals"]!.AsArray().RemoveAt(0);
        Assert.False(packet.MatchesCorrectedAlternativeTreatmentAuthoring(corrected));
    }
}
