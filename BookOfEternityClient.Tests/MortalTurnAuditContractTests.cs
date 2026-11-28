using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class MortalTurnAuditContractTests
{
    private static MortalTurnAuditPacket Packet(string narrative = "Вам подарили повозку.", string request = "request1") =>
        new("session1", "generation1", request, 42, 1,
        [new("original", "original", "game_state/vehicles.json", "{\"vehicles\":[]}"),
         new("candidate", "candidate", "game_state/vehicles.json", "{\"vehicles\":[{\"vehicleId\":\"cart1\"}]}"),
         new("narrative", "narrative", "output/narrative_response.json#/text", narrative)]);

    private static JsonObject Response(MortalTurnAuditPacket packet, string assessment = "supported") => new()
    {
        ["schemaVersion"] = 1, ["packetDigest"] = packet.Digest, ["verdict"] = "pass",
        ["reviewedDocuments"] = new JsonArray("original", "candidate", "narrative"),
        ["findings"] = new JsonArray(new JsonObject
        {
            ["direction"] = "narrativeToState", ["quoteDocumentId"] = "narrative", ["quote"] = packet.Documents.Single(x => x.Id == "narrative").Text,
            ["assessment"] = assessment, ["reason"] = "Exact matching canonical vehicle.",
            ["evidence"] = new JsonArray(new JsonObject
            { ["documentId"] = "candidate", ["pointer"] = "/vehicles/0/vehicleId", ["value"] = "cart1" })
        })
    };

    [Theory]
    [InlineData("pass", "Pass")]
    [InlineData("fail", "Fail")]
    [InlineData("uncertain", "Uncertain")]
    public void ValidatedVerdictAndDetachedFindingsRemainAvailableToRepair(string verdict, string expected)
    {
        var packet = Packet(); var response = Response(packet);
        response["verdict"] = verdict;
        var result = MortalTurnAuditContract.Validate(packet, response.ToJsonString());
        Assert.Equal(Enum.Parse<MortalTurnAuditVerdict>(expected), result.Verdict);
        Assert.Equal(packet.Digest, result.PacketDigest);
        var finding = Assert.Single(result.Findings);
        Assert.Equal(MortalTurnAuditDirection.NarrativeToState, finding.Direction);
        Assert.Equal(MortalTurnAuditAssessment.Supported, finding.Assessment);
        Assert.Equal("Exact matching canonical vehicle.", finding.Reason);
        Assert.Equal("narrative", finding.QuoteDocumentId);
        Assert.Equal("Вам подарили повозку.", finding.Quote);
        var evidence = Assert.Single(finding.Evidence);
        Assert.Equal("candidate", evidence.DocumentId);
        Assert.Equal("/vehicles/0/vehicleId", evidence.Pointer);
        Assert.Equal("cart1", evidence.Value.GetString());
        Assert.Throws<NotSupportedException>(() => ((IList<MortalTurnAuditFinding>)result.Findings).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<MortalTurnAuditEvidence>)finding.Evidence).Clear());
    }

    [Fact]
    public void InvalidResponseDoesNotLeakEarlierValidatedPartialFindings()
    {
        var packet = Packet(); var response = Response(packet);
        var invalid = response["findings"]![0]!.DeepClone();
        invalid["quote"] = "Invented";
        response["findings"]!.AsArray().Add(invalid);
        var result = MortalTurnAuditContract.Validate(packet, response.ToJsonString());
        Assert.Equal(MortalTurnAuditVerdict.Invalid, result.Verdict);
        Assert.Null(result.PacketDigest);
        Assert.Empty(result.Findings);
        Assert.False(result.IsConsistent);
    }

    [Fact]
    public void AdverseTypedFindingOverridesPassWithoutDiscardingRepairEvidence()
    {
        var packet = Packet(); var response = Response(packet, "contradicted");
        var result = MortalTurnAuditContract.Validate(packet, response.ToJsonString());
        Assert.Equal(MortalTurnAuditVerdict.Fail, result.Verdict);
        Assert.Equal(MortalTurnAuditAssessment.Contradicted, Assert.Single(result.Findings).Assessment);
        Assert.False(result.IsConsistent);
    }

    [Fact]
    public void ExactSupportedEvidenceAndCompleteReviewAreConsistent()
    {
        var packet = Packet();
        var result = MortalTurnAuditContract.Validate(packet, Response(packet).ToJsonString());
        Assert.True(result.IsConsistent, result.Error);
    }

    [Fact]
    public void QuietOrNegatedNarrativeCanPassWithoutInventingMechanicalEvents()
    {
        var source = Packet("Вам не подарили повозку. Вы лишь мечтаете о ней.");
        var packet = new MortalTurnAuditPacket("session1", "generation1", "request1", 42, 1,
            source.Documents.Select(d => d.Id == "candidate" ? d with { Text = "{\"vehicles\":[]}" } : d));
        var response = Response(packet, "nonasserted");
        response["findings"]![0]!["evidence"] = new JsonArray();
        Assert.True(MortalTurnAuditContract.Validate(packet, response.ToJsonString()).IsConsistent);
        response["findings"] = new JsonArray();
        Assert.True(MortalTurnAuditContract.Validate(packet, response.ToJsonString()).IsConsistent);
        // This proves protocol handling, not that a model correctly recognizes the text.
    }

    [Theory]
    [InlineData("missing")][InlineData("contradicted")][InlineData("uncertain")]
    public void AdverseFindingCannotHideBehindPass(string assessment)
    {
        var packet = Packet(); var response = Response(packet, assessment);
        Assert.False(MortalTurnAuditContract.Validate(packet, response.ToJsonString()).IsConsistent);
    }

    [Theory]
    [InlineData("fail")][InlineData("uncertain")][InlineData("PASS")][InlineData("")]
    public void NonPassVerdictNeverMeansAcceptance(string verdict)
    {
        var packet = Packet(); var response = Response(packet); response["verdict"] = verdict;
        Assert.False(MortalTurnAuditContract.Validate(packet, response.ToJsonString()).IsConsistent);
    }

    [Fact]
    public void ForeignRequestOrChangedNarrativeInvalidatesReply()
    {
        var packet = Packet(); var response = Response(packet).ToJsonString();
        Assert.False(MortalTurnAuditContract.Validate(Packet(request:"request2"), response).IsConsistent);
        Assert.False(MortalTurnAuditContract.Validate(Packet("Переправа уничтожена."), response).IsConsistent);
    }

    [Theory]
    [InlineData("missingCoverage")][InlineData("duplicateCoverage")][InlineData("unknownField")]
    [InlineData("foreignQuote")][InlineData("foreignDocument")][InlineData("wrongValue")]
    [InlineData("unresolvedPointer")][InlineData("noEvidence")][InlineData("emptyReason")]
    public void IncompleteOrFabricatedEvidenceFails(string mutation)
    {
        var packet = Packet(); var response = Response(packet); var finding = response["findings"]![0]!;
        var evidence = finding["evidence"]![0]!;
        switch (mutation)
        {
            case "missingCoverage": response["reviewedDocuments"]!.AsArray().RemoveAt(2); break;
            case "duplicateCoverage": response["reviewedDocuments"]!.AsArray().Add("narrative"); break;
            case "unknownField": response["authorization"] = true; break;
            case "foreignQuote": finding["quote"] = "Invented text"; break;
            case "foreignDocument": evidence["documentId"] = "../../secrets"; break;
            case "wrongValue": evidence["value"] = "cart2"; break;
            case "unresolvedPointer": evidence["pointer"] = "/vehicles/1/vehicleId"; break;
            case "noEvidence": finding["evidence"] = new JsonArray(); break;
            case "emptyReason": finding["reason"] = " "; break;
        }
        Assert.False(MortalTurnAuditContract.Validate(packet, response.ToJsonString()).IsConsistent);
    }

    [Fact]
    public void UnexplainedStateChangeCanBeReportedWithoutInventedNarrativeQuote()
    {
        var packet = Packet("Вы спокойно ждете."); var response = Response(packet, "missing");
        response["verdict"] = "fail";
        response["findings"]![0]!["direction"] = "stateToNarrative";
        response["findings"]![0]!["quote"] = "";
        var result = MortalTurnAuditContract.Validate(packet, response.ToJsonString());
        Assert.False(result.IsConsistent);
        Assert.Contains("discrepancy", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void DuplicatePropertiesAndMalformedRepliesFail()
    {
        var packet = Packet(); var response = Response(packet).ToJsonString();
        Assert.False(MortalTurnAuditContract.Validate(packet, response.Replace("\"verdict\":\"pass\"", "\"verdict\":\"fail\",\"verdict\":\"pass\"")).IsConsistent);
        Assert.False(MortalTurnAuditContract.Validate(packet, "{}").IsConsistent);
        Assert.False(MortalTurnAuditContract.Validate(packet, "not json").IsConsistent);
    }

    [Theory]
    [InlineData("/a~1b/~0key/1", "null", true)]
    [InlineData("/", "7", true)]
    [InlineData("", "{\"a/b\":{\"~key\":[3,null]},\"\":7}", true)]
    [InlineData("/a~1b/~0key/01", "null", false)]
    [InlineData("/a~1b/~0key/-1", "null", false)]
    [InlineData("/a~1b/~0key/-", "null", false)]
    [InlineData("/a~2b", "null", false)]
    public void EvidencePointersUseExactJsonPointerSemantics(string pointer, string value, bool expected)
    {
        var original = Packet();
        var packet = new MortalTurnAuditPacket("session1", "generation1", "request1", 42, 1,
            original.Documents.Select(d => d.Id == "candidate" ? d with
            { Text = "{\"a/b\":{\"~key\":[3,null]},\"\":7}" } : d));
        var response = Response(packet); var evidence = response["findings"]![0]!["evidence"]![0]!;
        evidence["pointer"] = pointer; evidence["value"] = JsonNode.Parse(value);
        Assert.Equal(expected, MortalTurnAuditContract.Validate(packet, response.ToJsonString()).IsConsistent);
    }

    [Fact]
    public void DuplicateStateOrNestedResponsePropertiesNeverPass()
    {
        var original = Packet();
        var duplicate = new MortalTurnAuditPacket("session1", "generation1", "request1", 42, 1,
            original.Documents.Select(d => d.Id == "original" ? d with { Text = "{\"x\":1,\"x\":2}" } : d));
        Assert.False(MortalTurnAuditContract.Validate(duplicate, Response(duplicate).ToJsonString()).IsConsistent);
        var response = Response(original);
        response["findings"]![0]!["evidence"]![0]!["writePath"] = "../../outside";
        Assert.False(MortalTurnAuditContract.Validate(original, response.ToJsonString()).IsConsistent);
        var json = Response(original).ToJsonString().Replace("\"value\":\"cart1\"", "\"value\":\"cart1\",\"value\":\"cart1\"");
        Assert.False(MortalTurnAuditContract.Validate(original, json).IsConsistent);
    }

    [Theory]
    [InlineData("session")][InlineData("request")][InlineData("turn")]
    [InlineData("path")][InlineData("text")][InlineData("id")]
    public void ChangingAnyIdentityDimensionChangesDigest(string dimension)
    {
        var packet = Packet();
        var changed = new MortalTurnAuditPacket(dimension == "session" ? "session2" : "session1", "generation1",
            dimension == "request" ? "request2" : "request1", dimension == "turn" ? 43 : 42, 1,
            packet.Documents.Select(d => d.Id == "original" ? d with
            { Path = dimension == "path" ? "other" : d.Path, Text = dimension == "text" ? "{}" : d.Text,
              Id = dimension == "id" ? "original2" : d.Id } : d));
        Assert.NotEqual(packet.Digest, changed.Digest);
    }

    [Theory]
    [InlineData("session")][InlineData("generation")][InlineData("request")]
    [InlineData("id")][InlineData("path")][InlineData("text")]
    public void IllFormedUnicodeCannotCollapseDistinctPacketIdentities(string field)
    {
        var bad = new string((char)0xd800, 1) + "quiet";
        var docs = Packet().Documents.Select(d => d.Id == "narrative" ? d with
        {
            Id = field == "id" ? bad : d.Id,
            Path = field == "path" ? bad : d.Path,
            Text = field == "text" ? bad : d.Text
        } : d).ToArray();
        Assert.Throws<ArgumentException>(() => new MortalTurnAuditPacket(
            field == "session" ? bad : "session1", field == "generation" ? bad : "generation1",
            field == "request" ? bad : "request1", 42, 1, docs));
    }

    [Fact]
    public void PacketCopiesDocumentCollectionAndDigestIncludesEveryInput()
    {
        var baseline = Packet(); var docs = baseline.Documents.ToList();
        var copy = new MortalTurnAuditPacket("session1", "generation1", "request1", 42, 1, docs);
        docs.Clear(); Assert.Equal(3, copy.Documents.Count); Assert.Equal(baseline.Digest, copy.Digest);
        var reordered = new MortalTurnAuditPacket("session1", "generation1", "request1", 42, 1, baseline.Documents.Reverse());
        Assert.Equal(baseline.Digest, reordered.Digest);
        Assert.NotEqual(baseline.Digest, new MortalTurnAuditPacket("session1", "generation2", "request1", 42, 1, baseline.Documents).Digest);
        Assert.NotEqual(baseline.Digest, new MortalTurnAuditPacket("session1", "generation1", "request1", 42, 2, baseline.Documents).Digest);
        Assert.Throws<ArgumentException>(() => new MortalTurnAuditPacket("session1", "generation1", "request1", 42, 1, baseline.Documents.Concat(baseline.Documents)));
    }
}
