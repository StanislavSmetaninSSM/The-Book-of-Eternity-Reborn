using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class WoundAlternativeTreatmentRepairTests
{
    [Fact]
    public void CorrectedAuthor_ReplacesRequiredModeAndPreservesSiblings()
    {
        var (request, original) = CreateInvalidModeCandidate();
        var packet = Assert.Single(WoundRepairPacketBuilder.Build(request));
        Assert.Equal("author_alternative_treatment", packet.CandidateKind);
        Assert.Equal("woundTreatmentAuthorings", packet.ResubmissionRoute);
        Assert.Null(packet.ToJsonObject()["preservedProposal"]!["route"]!["mode"]);
        var corrected = original.DeepClone().AsObject();
        corrected["route"]!["mode"] = "procedure";
        Assert.True(packet.MatchesCorrectedAlternativeTreatmentAuthoring(corrected));
        corrected["route"]!["displayName"] = "unrelated rewrite";
        Assert.False(packet.MatchesCorrectedAlternativeTreatmentAuthoring(corrected));
    }

    [Fact]
    public void PublicPacket_HasElevenOrderedFieldsAndStrictTransport()
    {
        var packet = Assert.Single(WoundRepairPacketBuilder.Build(CreateInvalidModeCandidate().Request));
        Assert.Equal(new[] { "kind", "candidateKind", "sessionId", "requestId", "snapshotToken",
            "candidateRef", "semanticFingerprint", "issues", "safeContext", "preservedProposal",
            "requiredResponseShape" }, packet.ToJsonObject().Select(pair => pair.Key));
        Assert.True(ValidWave(packet.ToJsonObject(), packet));
        foreach (var tag in new JsonNode?[] { null, JsonValue.Create("unknown"), JsonValue.Create("construct_wound"),
                     JsonValue.Create("AUTHOR_ALTERNATIVE_TREATMENT"), JsonValue.Create(1) })
        {
            var mutated = packet.ToJsonObject();
            mutated["candidateKind"] = tag;
            Assert.False(ValidWave(mutated, packet));
        }
        var missing = packet.ToJsonObject();
        missing.Remove("candidateKind");
        Assert.False(ValidWave(missing, packet));
    }

    [Fact]
    public void RawDiagnosisDiagnostics_KeepOriginalArrayIndicesAcrossPhases()
    {
        var original = CreateAuthor();
        original["diagnosisPath"]!["reveals"] = new JsonArray(null, "bogus", "route:alternative_hidden_route");
        var parsed = Parse(original);
        Assert.Contains(parsed.Issues, issue => issue.FilePath == Prefix + ".diagnosisPath.reveals[0]");
        Assert.Single(parsed.Issues.Where(issue => issue.Code == "wound_treatment_diagnosis_fact_unknown"));
    }

    [Theory]
    [InlineData("woundTreatmentAuthorings[01].route.mode", "wound_materialization_invalid_field", "wound_treatment_authorings")]
    [InlineData("woundTreatmentAuthorings[0].route.displayName", "wound_materialization_invalid_field", "wound_treatment_authorings")]
    [InlineData("woundTreatmentAuthorings[0].route.mode", "wound_response_invalid_field", "wound_treatment_authorings")]
    [InlineData("woundTreatmentAuthorings[0].authoringRequestRef", "wound_response_invalid_field", "wound_treatment_authorings")]
    [InlineData("woundTreatmentAuthorings[0].route.mode", "wound_materialization_invalid_field", "unrelated")]
    public void CallerDiagnostics_CannotGrantArbitraryEdits(string path, string code, string section)
    {
        var original = CreateAuthor();
        original["route"]!["mode"] = "unsupported_mode";
        var request = Request(original, new[] { new ValidationIssue(path, IssueSeverity.Error,
            "untrusted", code: code, section: section, expected: "rewrite everything", actual: "invented") });
        Assert.Empty(WoundRepairPacketBuilder.Build(request));
        Assert.True(WoundRepairPacketBuilder.RequiresFailClosedRollback(request));
    }

    [Fact]
    public void Decline_HasNoRepairWorkAndCannotBeInventedFromMalformedDecline()
    {
        var original = CreateAuthor();
        original["decision"] = "decline";
        original["route"] = null;
        original["diagnosisPath"] = null;
        var request = Request(original);
        Assert.Empty(WoundRepairPacketBuilder.Build(request));
        Assert.False(WoundRepairPacketBuilder.RequiresFailClosedRollback(request));
        original["route"] = new JsonObject();
        Assert.True(WoundRepairPacketBuilder.RequiresFailClosedRollback(Request(original)));
    }

    private const string Prefix = "woundTreatmentAuthorings[0]";

    private static WoundAlternativeTreatmentResponseParseResult Parse(JsonObject original) =>
        WoundResponseInputComposer.ParseAlternativeTreatmentAuthoring(JsonSerializer.SerializeToElement(original), Prefix);

    private static JsonObject CreateAuthor()
    {
        var route = WoundContractTestData.CreateActiveWound()["treatment"]!["routes"]![0]!.DeepClone().AsObject();
        route["routeId"] = "alternative_hidden_route";
        route["visibility"] = "hidden";
        return new JsonObject
        {
            ["authoringRequestRef"] = "authoring_request_public_001",
            ["decision"] = "author",
            ["route"] = route,
            ["diagnosisPath"] = new JsonObject
            {
                ["diagnosisPathId"] = "diagnosis_alternative_hidden",
                ["displayName"] = "Узнать другой способ лечения",
                ["visibility"] = "known_to_player",
                ["requiresKnownFacts"] = new JsonArray("route:clean_and_suture"),
                ["requirements"] = new JsonArray(),
                ["check"] = new JsonObject(),
                ["reveals"] = new JsonArray("route:alternative_hidden_route"),
                ["failurePolicy"] = "no_reveal"
            }
        };
    }

    private static (WoundRepairBuildRequest Request, JsonObject Original) CreateInvalidModeCandidate()
    {
        var original = CreateAuthor();
        original["route"]!["mode"] = "unsupported_mode";
        Assert.Contains(Parse(original).Issues, issue => issue.FilePath == Prefix + ".route.mode" &&
            issue.Code == "wound_materialization_invalid_field");
        return (Request(original), original);
    }

    private static WoundRepairBuildRequest Request(JsonObject original,
        IReadOnlyList<ValidationIssue>? issues = null, JsonObject? context = null) => new(
        "session_wound_diagnosis", "request_wound_diagnosis", "snapshot_wound_diagnosis",
        new[] { new WoundRepairCandidateInput("author_alternative_treatment",
            "candidate_alternative_treatment_001", "sha256:" + new string('b', 64),
            "authoring_request_public_001", context ?? new JsonObject
            {
                ["event"] = "В найденных записях описан другой способ лечения",
                ["target"] = "игрок", ["realm"] = "Смертный мир"
            }, new[] { "decline", "author" }, "I", "IV", original, issues ?? Parse(original).Issues) });

    private static bool ValidWave(JsonObject value, WoundRepairPacket packet) =>
        WoundRepairPacketBuilder.IsValidPersistedRepairWave(
            JsonSerializer.SerializeToElement(new JsonArray(value)),
            JsonSerializer.SerializeToElement(new[] { packet.CreateReceipt() }, new JsonSerializerOptions
                { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
            packet.SessionId, packet.RequestId, packet.SnapshotToken);
}
