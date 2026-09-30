using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class WoundAlternativeTreatmentRepairTests
{
    public static TheoryData<string> PrivateKeys => new()
    {
        "operationKey", "woundId", "effectId", "ownerId", "providerId", "resourceId", "carrierPath",
        "routeSeal", "resourceSeal", "sourceSeal", "providerSeal", "authorityFingerprint", "privateNpcData", "gmPrivateNotes",
        "expectedBeforeFingerprint", "expectedAfterFingerprint", "requestAuthorityFingerprint", "routeFingerprint",
        "diagnosisPathFingerprint", "evidenceAuthorityFingerprint", "requirementAuthorityFingerprint", "checkResultFingerprint", "resultFingerprint"
    };

    [Theory]
    [MemberData(nameof(PrivateKeys))]
    public void Privacy_RemovesKeysAndAliasedValuesAndRejectsTheirReintroduction(string key)
    {
        var original = CreateAuthor();
        original["route"]!["mode"] = "unsupported_mode";
        original["route"]![key] = "private_value_123";
        original["route"]!["displayName"] = "alias private_value_123 inside display";
        original["route"]!["nestedSecrets"] = new JsonArray("private_value_123");
        var packet = Assert.Single(WoundRepairPacketBuilder.Build(Request(original)));
        var json = packet.ToJsonObject().ToJsonString();
        Assert.DoesNotContain(key, json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("private_value_123", json);
        var corrected = CreateAuthor();
        Assert.True(packet.MatchesCorrectedAlternativeTreatmentAuthoring(corrected));
        corrected["route"]!["displayName"] = "private_value_123";
        Assert.False(packet.MatchesCorrectedAlternativeTreatmentAuthoring(corrected));
        corrected = CreateAuthor();
        corrected["route"]![key] = "new_private_value";
        Assert.False(packet.MatchesCorrectedAlternativeTreatmentAuthoring(corrected));
    }

    [Fact]
    public void Privacy_PrivateContextValuesAreCopiedAndUnsafeContextFailsClosed()
    {
        var (request, original) = CreateInvalidModeCandidate();
        request.Candidates[0].SafeContext["gmPrivateNotes"] = "context_only_secret";
        var packet = Assert.Single(WoundRepairPacketBuilder.Build(request));
        request.Candidates[0].SafeContext["gmPrivateNotes"] = "changed_by_caller";
        original["route"]!["mode"] = "procedure";
        original["route"]!["displayName"] = "context_only_secret";
        Assert.False(packet.MatchesCorrectedAlternativeTreatmentAuthoring(original));
        request.Candidates[0].SafeContext["event"] = "changed_by_caller";
        Assert.True(WoundRepairPacketBuilder.RequiresFailClosedRollback(request));
    }

    [Fact]
    public void Privacy_UntrustedExpectedActualNeverBecomeInstructionsOrEvidence()
    {
        var (request, original) = CreateInvalidModeCandidate();
        var source = request.Candidates[0].Issues[0];
        var spoofed = new ValidationIssue(source.FilePath, IssueSeverity.Error, "invented message", code: source.Code,
            section: source.Section, expected: "rewrite siblings", actual: "invented evidence", repairHint: "publish directly");
        var packet = Assert.Single(WoundRepairPacketBuilder.Build(Request(original, new[] { spoofed })));
        Assert.Equal("procedure, course, or guaranteed", Assert.Single(packet.Issues).Expected);
        Assert.DoesNotContain("invented", packet.ToJsonObject().ToJsonString());
    }

    [Fact]
    public void ContextualBuild_PreservesExactKindAndDetachedPublicPacket()
    {
        var (request, _) = CreateInvalidModeCandidate();
        var candidate = request.Candidates[0];
        var context = new WoundRepairContext(request.SessionId, request.RequestId, request.SnapshotToken,
            candidate.Kind, candidate.CandidateRef, candidate.SemanticFingerprint, candidate.OpportunityRef,
            candidate.SafeContext, candidate.AllowedDecisions, candidate.MinimumSeverity, candidate.MaximumSeverity,
            candidate.RejectedDecision);
        var contextual = candidate.Issues.Select(issue => new ValidationIssue(issue.FilePath, issue.Severity,
            issue.Message, code: issue.Code, section: issue.Section) { WoundRepairContext = context }).ToArray();
        var direct = Assert.Single(WoundRepairPacketBuilder.Build(request));
        var packet = Assert.Single(WoundRepairPacketBuilder.Build(contextual));
        Assert.True(JsonNode.DeepEquals(direct.ToJsonObject(), packet.ToJsonObject()));
        packet.PreservedProposal["route"]!["displayName"] = "mutated getter";
        packet.RequiredResponseShape["response"] = "mutated getter";
        Assert.True(JsonNode.DeepEquals(direct.ToJsonObject(), packet.ToJsonObject()));
        var identityError = new ValidationIssue(Prefix + ".authoringRequestRef", IssueSeverity.Error,
            "bad ref", code: "wound_response_invalid_field", section: "wound_treatment_authorings") { WoundRepairContext = context };
        Assert.True(WoundRepairPacketBuilder.RequiresFailClosedRollback(new[] { identityError }));
        Assert.Empty(WoundRepairPacketBuilder.Build(new[] { identityError }));
    }
}
