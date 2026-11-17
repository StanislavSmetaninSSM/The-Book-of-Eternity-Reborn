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
            ["quoteDocumentId"] = "narrative", ["quote"] = packet.Documents.Single(x => x.Id == "narrative").Text,
            ["assessment"] = assessment, ["reason"] = "Exact matching canonical vehicle.",
            ["evidence"] = new JsonArray(new JsonObject
            { ["documentId"] = "candidate", ["pointer"] = "/vehicles/0/vehicleId", ["value"] = "cart1" })
        })
    };

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
        var packet = Packet("Вам не подарили повозку. Вы лишь мечтаете о ней.");
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
    public void DuplicatePropertiesAndMalformedRepliesFail()
    {
        var packet = Packet(); var response = Response(packet).ToJsonString();
        Assert.False(MortalTurnAuditContract.Validate(packet, response.Replace("\"verdict\":\"pass\"", "\"verdict\":\"fail\",\"verdict\":\"pass\"")).IsConsistent);
        Assert.False(MortalTurnAuditContract.Validate(packet, "{}").IsConsistent);
        Assert.False(MortalTurnAuditContract.Validate(packet, "not json").IsConsistent);
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
