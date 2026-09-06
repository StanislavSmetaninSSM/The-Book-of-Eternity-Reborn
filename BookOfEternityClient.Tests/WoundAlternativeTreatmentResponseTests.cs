using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class WoundAlternativeTreatmentResponseTests
{
    [Fact]
    public void Decline_RoundTripsFourExplicitFieldsWithoutAnAcceptedResult()
    {
        using var document = JsonDocument.Parse("""
            {"authoringRequestRef":"request_decline","decision":"decline",
             "route":null,"diagnosisPath":null}
            """);
        var parsed = WoundResponseInputComposer.ParseAlternativeTreatmentAuthoring(
            document.RootElement, "woundTreatmentAuthorings[0]");
        Assert.True(parsed.IsValid, string.Join("; ", parsed.Issues.Select(issue => issue.Code)));
        var draft = Assert.Single(parsed.Drafts);
        Assert.Equal("decline", draft.Decision);
        Assert.Null(draft.Route);
        Assert.Null(draft.DiagnosisPath);
        using var roundTrip = JsonDocument.Parse(Write(draft));
        Assert.Equal(4, roundTrip.RootElement.EnumerateObject().Count());
        Assert.Equal(JsonValueKind.Null, roundTrip.RootElement.GetProperty("route").ValueKind);
        Assert.Equal(JsonValueKind.Null, roundTrip.RootElement.GetProperty("diagnosisPath").ValueKind);
        Assert.False(roundTrip.RootElement.TryGetProperty("authorityFingerprint", out _));
        Assert.False(roundTrip.RootElement.TryGetProperty("resultFingerprint", out _));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Author_VisibleAndHiddenMembersDetachAndRoundTrip(bool hidden)
    {
        var route = Route();
        route["routeId"] = hidden ? "setting/specific:hidden" : "setting/specific:visible";
        route["visibility"] = hidden ? "hidden" : "known_to_player";
        var path = hidden ? Diagnosis(route["routeId"]!.GetValue<string>()) : null;
        var entry = Authoring("request_жар-птица", route, path);
        var original = entry.ToJsonString();

        var parsed = WoundResponseInputComposer.ParseAlternativeTreatmentAuthoring(
            Element(entry), "woundTreatmentAuthorings[0]");

        Assert.True(parsed.IsValid, Describe(parsed.Issues));
        var draft = Assert.Single(parsed.Drafts);
        Assert.Equal(hidden ? "hidden" : "known_to_player", draft.Route!.Visibility);
        Assert.Equal(hidden, draft.DiagnosisPath is not null);
        route["displayName"] = "mutated";
        if (path is not null) path["displayName"] = "mutated";
        var written = Write(draft);
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(original), JsonNode.Parse(written)));
        Assert.Equal(written, Write(Assert.Single(WoundResponseInputComposer
            .ParseAlternativeTreatmentAuthoring(Element(written), "woundTreatmentAuthorings[0]").Drafts)));
    }

    [Fact]
    public void Plural_AcceptsZeroAndThirtyTwoAndRejectsThirtyThree()
    {
        var empty = WoundResponseInputComposer.ParseAlternativeTreatmentAuthorings(
            Element("[]"), "woundTreatmentAuthorings");
        Assert.True(empty.IsValid);
        Assert.Empty(empty.Drafts);

        var thirtyTwo = new JsonArray(Enumerable.Range(0, 32)
            .Select(index => (JsonNode)Decline("request_" + index)).ToArray());
        var atLimit = WoundResponseInputComposer.ParseAlternativeTreatmentAuthorings(
            Element(thirtyTwo), "woundTreatmentAuthorings");
        Assert.True(atLimit.IsValid, Describe(atLimit.Issues));
        Assert.Equal(32, atLimit.Drafts.Length);

        thirtyTwo.Add(Decline("request_32"));
        var above = WoundResponseInputComposer.ParseAlternativeTreatmentAuthorings(
            Element(thirtyTwo), "woundTreatmentAuthorings");
        Assert.False(above.IsValid);
        Assert.Empty(above.Drafts);
        Assert.Contains(above.Issues, issue => issue.Code == "wound_response_invalid_field" &&
            issue.FilePath == "woundTreatmentAuthorings");
    }

    [Theory]
    [InlineData("[]", "woundTreatmentAuthorings[0]", "wound_response_invalid_field")]
    [InlineData("{\"authoringRequestRef\":\"request\",\"decision\":\"author\",\"route\":null}", "woundTreatmentAuthorings[0].diagnosisPath", "wound_response_missing_field")]
    [InlineData("{\"authoringRequestRef\":\"request\",\"decision\":7,\"route\":null,\"diagnosisPath\":null}", "woundTreatmentAuthorings[0].decision", "wound_response_invalid_field")]
    [InlineData("{\"authoringRequestRef\":\"request\",\"decision\":\"unknown\",\"route\":null,\"diagnosisPath\":null}", "woundTreatmentAuthorings[0].decision", "wound_response_invalid_field")]
    [InlineData("{\"authoringRequestRef\":\"request\",\"decision\":\"decline\",\"route\":null,\"diagnosisPath\":null,\"authorityFingerprint\":\"sha256:x\"}", "woundTreatmentAuthorings[0].authorityFingerprint", "wound_response_unknown_field")]
    public void Singular_RejectsWrongShapeMissingFieldsUnknownDecisionAndAuthority(
        string json, string path, string code)
    {
        var result = WoundResponseInputComposer.ParseAlternativeTreatmentAuthoring(
            Element(json), "woundTreatmentAuthorings[0]");

        Assert.False(result.IsValid);
        Assert.Empty(result.Drafts);
        Assert.Contains(result.Issues, issue => issue.Code == code && issue.FilePath == path);
        Assert.All(result.Issues, issue =>
        {
            Assert.Equal("wound_treatment_authorings", issue.Section);
            Assert.DoesNotContain("opportunity", issue.Message, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Theory]
    [InlineData("", "woundTreatmentAuthorings[0].authoringRequestRef")]
    [InlineData("bad ref", "woundTreatmentAuthorings[0].authoringRequestRef")]
    [InlineData("bad/ref", "woundTreatmentAuthorings[0].authoringRequestRef")]
    [InlineData("bad\\ref", "woundTreatmentAuthorings[0].authoringRequestRef")]
    [InlineData("bad:ref", "woundTreatmentAuthorings[0].authoringRequestRef")]
    public void OpaqueReference_RejectsUnsafeSpellings(string reference, string path)
    {
        var parsed = WoundResponseInputComposer.ParseAlternativeTreatmentAuthoring(
            Element(Decline(reference)), "woundTreatmentAuthorings[0]");

        Assert.False(parsed.IsValid);
        Assert.Contains(parsed.Issues, issue => issue.Code == "wound_response_invalid_field" &&
            issue.FilePath == path);
    }

    [Theory]
    [InlineData("same", "same")]
    [InlineData("request-one", "request‐one")]
    public void Plural_RejectsExactAndUnicodeConfusableReferences(string first, string second)
    {
        var values = new JsonArray(Decline(first), Decline(second));

        var parsed = WoundResponseInputComposer.ParseAlternativeTreatmentAuthorings(
            Element(values), "woundTreatmentAuthorings");

        Assert.False(parsed.IsValid);
        Assert.Empty(parsed.Drafts);
        Assert.Contains(parsed.Issues, issue =>
            issue.Code == "wound_response_duplicate_reference" &&
            issue.FilePath == "woundTreatmentAuthorings[1].authoringRequestRef");
    }

    [Theory]
    [InlineData("public", false, false)]
    [InlineData("known_to_player", false, false)]
    [InlineData("hidden", true, false)]
    [InlineData("hidden", false, true)]
    [InlineData("gm_only", false, true)]
    public void Author_EnforcesVisibilityAndRevealingPathPair(
        string visibility, bool includeValidPath, bool invalid)
    {
        var route = Route();
        route["visibility"] = visibility;
        var path = includeValidPath ? Diagnosis(route["routeId"]!.GetValue<string>()) : null;
        if (visibility is "public" or "known_to_player" && invalid)
            path = Diagnosis(route["routeId"]!.GetValue<string>());

        var parsed = WoundResponseInputComposer.ParseAlternativeTreatmentAuthoring(
            Element(Authoring("request", route, path)), "woundTreatmentAuthorings[0]");

        Assert.Equal(!invalid, parsed.IsValid);
        if (invalid)
            Assert.Contains(parsed.Issues, issue => issue.FilePath ==
                (visibility == "gm_only" ? "woundTreatmentAuthorings[0].route.visibility" :
                    "woundTreatmentAuthorings[0].diagnosisPath"));
    }

    [Fact]
    public void Author_HiddenRouteRejectsWrongRevealAndGmOnlyPath()
    {
        var route = Route();
        route["visibility"] = "hidden";
        var path = Diagnosis("different_route");
        var wrongReveal = Parse(Authoring("request", route, path));
        AssertIssue(wrongReveal, "woundTreatmentAuthorings[0].diagnosisPath", "wound_response_invalid_field");

        path = Diagnosis(route["routeId"]!.GetValue<string>());
        path["visibility"] = "gm_only";
        var gmOnly = Parse(Authoring("request", route, path));
        AssertIssue(gmOnly, "woundTreatmentAuthorings[0].diagnosisPath", "wound_response_invalid_field");
    }

    [Fact]
    public void Decline_RequiresBothExplicitNullPayloads()
    {
        var route = Route();
        var withRoute = Parse(new JsonObject
        {
            ["authoringRequestRef"] = "request", ["decision"] = "decline",
            ["route"] = route, ["diagnosisPath"] = null
        });
        AssertIssue(withRoute, "woundTreatmentAuthorings[0].route", "wound_response_invalid_field");

        var missing = Decline("request");
        missing.Remove("route");
        AssertIssue(Parse(missing), "woundTreatmentAuthorings[0].route", "wound_response_missing_field");
    }

    [Fact]
    public void NestedDuplicateAndCanonicalRemovalDialectFailAtOriginalMemberPaths()
    {
        var route = Route();
        route["outcomes"]![0]!["result"] = new JsonArray(new JsonObject
        {
            ["kind"] = "remove_complication", ["complicationRef"] = "offered"
        });
        var raw = Authoring("request", route, null).ToJsonString().Replace(
            "\"complicationRef\":\"offered\"",
            "\"complicationRef\":\"offered\",\"complicationRef\":\"other\"",
            StringComparison.Ordinal);
        var duplicate = WoundResponseInputComposer.ParseAlternativeTreatmentAuthoring(
            Element(raw), "woundTreatmentAuthorings[0]");
        Assert.Contains(duplicate.Issues, issue =>
            issue.Code == "wound_response_duplicate_field" &&
            issue.FilePath == "woundTreatmentAuthorings[0].route.outcomes[0].result[0].complicationRef");

        route["outcomes"]![0]!["result"]![0]!.AsObject().Remove("complicationRef");
        route["outcomes"]![0]!["result"]![0]!["complicationId"] = "canonical_forbidden";
        var canonical = Parse(Authoring("request", route, null));
        AssertIssue(canonical,
            "woundTreatmentAuthorings[0].route.outcomes[0].result[0].complicationId",
            "wound_materialization_unknown_field");
    }

    [Fact]
    public void Plural_InvalidMemberReturnsNoPartialDrafts()
    {
        var values = new JsonArray(Decline("valid"), Decline("bad/ref"));

        var parsed = WoundResponseInputComposer.ParseAlternativeTreatmentAuthorings(
            Element(values), "woundTreatmentAuthorings");

        Assert.False(parsed.IsValid);
        Assert.Empty(parsed.Drafts);
        Assert.Contains(parsed.Issues, issue =>
            issue.FilePath == "woundTreatmentAuthorings[1].authoringRequestRef");
    }

    [Fact]
    public void Author_PreservesExactRouteAndHiddenPairDiagnosticsForRepairMatching()
    {
        var invalidMode = Route();
        invalidMode["mode"] = "invented";
        AssertDiagnostic(
            Parse(Authoring("mode_request", invalidMode, null)),
            "woundTreatmentAuthorings[0].route.mode",
            "wound_materialization_invalid_field",
            "one ordinal current-schema value: procedure, course, guaranteed",
            "\"invented\"");

        var unknownField = Route();
        unknownField["unregistered"] = true;
        AssertDiagnostic(
            Parse(Authoring("field_request", unknownField, null)),
            "woundTreatmentAuthorings[0].route.unregistered",
            "wound_materialization_unknown_field",
            "registered current-schema field",
            "unregistered");

        var invalidOperation = Route();
        invalidOperation["outcomes"]![0]!["result"] = new JsonArray(new JsonObject
        {
            ["kind"] = "add_recovery", ["points"] = 0
        });
        AssertDiagnostic(
            Parse(Authoring("operation_request", invalidOperation, null)),
            "woundTreatmentAuthorings[0].route.outcomes[0].result[0].points",
            "wound_materialization_invalid_field",
            "exact integer 1..2147483647",
            "0");

        var hidden = Route();
        hidden["visibility"] = "hidden";
        AssertDiagnostic(
            Parse(Authoring("hidden_request", hidden, null)),
            "woundTreatmentAuthorings[0].diagnosisPath",
            "wound_response_invalid_field",
            "one non-GM-only revealing path only for a hidden route",
            "null");
    }

    [Fact]
    public void Writer_RejectsManuallyInconsistentDecisionPayloadPairs()
    {
        Assert.Throws<InvalidOperationException>(() => Write(new WoundAlternativeTreatmentResponseDraft(
            "request", "author", null, null)));
        var route = Assert.IsAssignableFrom<GmTreatmentRouteDraft>(
            MortalWoundTreatmentContract.ParseGmRouteDraftShape(Element(Route()), "route").Route);
        Assert.Throws<InvalidOperationException>(() => Write(new WoundAlternativeTreatmentResponseDraft(
            "request", "decline", route, null)));
        Assert.Throws<InvalidOperationException>(() => Write(new WoundAlternativeTreatmentResponseDraft(
            "request", "unexpected", null, null)));
    }

    private static WoundAlternativeTreatmentResponseParseResult Parse(JsonObject entry) =>
        WoundResponseInputComposer.ParseAlternativeTreatmentAuthoring(
            Element(entry), "woundTreatmentAuthorings[0]");

    private static JsonObject Route() =>
        WoundContractTestData.CreateActiveWound()["treatment"]!["routes"]![0]!.DeepClone().AsObject();

    private static JsonObject Diagnosis(string routeId) => new()
    {
        ["diagnosisPathId"] = "discover_alternative",
        ["displayName"] = "Найти путь лечения",
        ["visibility"] = "hidden",
        ["requiresKnownFacts"] = new JsonArray("route:existing_route"),
        ["requirements"] = new JsonArray(),
        ["check"] = new JsonObject(),
        ["reveals"] = new JsonArray("route:" + routeId),
        ["failurePolicy"] = "no_reveal"
    };

    private static JsonObject Authoring(string reference, JsonObject route, JsonObject? path) => new()
    {
        ["authoringRequestRef"] = reference,
        ["decision"] = "author",
        ["route"] = route.DeepClone(),
        ["diagnosisPath"] = path?.DeepClone()
    };

    private static JsonObject Decline(string reference) => new()
    {
        ["authoringRequestRef"] = reference,
        ["decision"] = "decline",
        ["route"] = null,
        ["diagnosisPath"] = null
    };

    private static string Write(WoundAlternativeTreatmentResponseDraft draft)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
               { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
            WoundResponseInputComposer.WriteAlternativeTreatmentAuthoringCanonical(writer, draft);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static JsonElement Element(JsonNode node) => JsonSerializer.SerializeToElement(node);

    private static JsonElement Element(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static void AssertIssue(
        WoundAlternativeTreatmentResponseParseResult result, string path, string code)
    {
        Assert.False(result.IsValid);
        Assert.Empty(result.Drafts);
        Assert.Contains(result.Issues, issue => issue.FilePath == path && issue.Code == code);
    }

    private static void AssertDiagnostic(
        WoundAlternativeTreatmentResponseParseResult result,
        string path,
        string code,
        string expected,
        string actual)
    {
        Assert.False(result.IsValid);
        Assert.Empty(result.Drafts);
        var issue = Assert.Single(result.Issues, issue => issue.FilePath == path && issue.Code == code);
        Assert.Equal(expected, issue.Expected);
        Assert.Equal(actual, issue.Actual);
    }

    private static string Describe(IEnumerable<ValidationIssue> issues) => string.Join(
        "; ", issues.Select(issue => issue.FilePath + ":" + issue.Code + ":" + issue.Expected));
}
