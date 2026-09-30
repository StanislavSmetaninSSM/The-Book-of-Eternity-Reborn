using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class MortalWoundAlternativeTransitionTests
{
    private const string RouteId = "moon_silver_treatment";
    private const string PathId = "read_silver_resonance";

    [Theory]
    [InlineData("public", "procedure")]
    [InlineData("known_to_player", "procedure")]
    [InlineData("hidden", "procedure")]
    [InlineData("public", "course")]
    [InlineData("known_to_player", "course")]
    [InlineData("hidden", "course")]
    [InlineData("public", "guaranteed")]
    [InlineData("known_to_player", "guaranteed")]
    [InlineData("hidden", "guaranteed")]
    public void Reduce_AppendsCompleteSettingSpecificOptionAndOnlyNonterminalHistory(string visibility, string mode)
    {
        var (before, after) = Roots(visibility, mode);
        var request = Create(Parse(before), Parse(after), visibility == "hidden" ? PathId : null);
        var reduced = WoundTransitionReducer.Reduce(request);
        Assert.True(reduced.IsValid, Issues(reduced));
        Assert.Equal(2, reduced.Intents.Count);
        var carrier = Assert.Single(reduced.Intents.OfType<WoundCarrierTransitionIntent>());
        Assert.Equal("replace", carrier.Operation);
        Assert.Equal(request.Before!.Owner, carrier.Owner);
        Assert.Equal(request.Before.WoundId, carrier.WoundId);
        var history = Assert.Single(reduced.Intents.OfType<WoundTransitionHistoryIntent>());
        Assert.False(history.Terminal);
        Assert.Null(history.AttemptId);
        Assert.Null(history.TickKey);
        Assert.Equal(request.TransitionId, history.TransitionId);
        Assert.Equal(request.OperationKey, history.OperationKey);
        Assert.Equal(request.EventRef, history.EventRef);
        Assert.Equal(request.Turn, history.Turn);
        var result = Assert.IsType<WoundAlternativeTreatmentTransitionResult>(history.TransitionResult);
        Assert.Equal("author_alternative_treatment", result.Kind);
        Assert.Equal("request_silver_authoring", result.AuthoringRequestRef);
        Assert.Equal(RouteId, result.AddedRouteId);
        Assert.Equal(visibility == "hidden" ? PathId : null, result.AddedDiagnosisPathId);
        Assert.True(ResourceMaterializationContract.IsAuthorityFingerprint(result.RouteFingerprint));
        if (visibility == "hidden")
            Assert.True(ResourceMaterializationContract.IsAuthorityFingerprint(result.DiagnosisPathFingerprint));
        else
            Assert.Null(result.DiagnosisPathFingerprint);
        Assert.Equal(WoundHistoryState.ComputeTransitionResultFingerprint(result.ToCanonicalJson()), result.ResultFingerprint);
        Assert.Equal(WoundMaterializationContract.SerializeCanonical(Parse(after)),
            WoundMaterializationContract.SerializeCanonical(reduced.ProposedAfter!));
        Assert.Equal(visibility == "hidden" ? new[] { "clean_and_suture", "old_zeta" } :
            new[] { "clean_and_suture", "old_zeta", RouteId }, reduced.ProposedAfter!.Treatment.KnownRouteIds);
        Assert.Equal(new[] { "old_zeta", "clean_and_suture" }, reduced.ProposedAfter.Treatment.CompletedRouteIds);
    }

    [Theory]
    [InlineData("operation")]
    [InlineData("event")]
    [InlineData("turn")]
    [InlineData("transition")]
    [InlineData("AuthorityRef")]
    [InlineData("WoundId")]
    [InlineData("AddedRouteId")]
    [InlineData("AddedDiagnosisPathId")]
    [InlineData("RequestAuthorityFingerprint")]
    [InlineData("EvidenceAuthorityFingerprint")]
    [InlineData("RequirementAuthorityFingerprint")]
    [InlineData("ExpectedBeforeFingerprint")]
    [InlineData("ExpectedAfterFingerprint")]
    [InlineData("RouteFingerprint")]
    [InlineData("DiagnosisPathFingerprint")]
    [InlineData("TransitionResult")]
    [InlineData("before_image")]
    [InlineData("after_image_resealed")]
    [InlineData("route_name")]
    [InlineData("route_requirements")]
    [InlineData("route_outcomes")]
    [InlineData("path_name")]
    [InlineData("path_requirements")]
    [InlineData("path_facts")]
    public void Reduce_RejectsEveryPostFactoryTransplantation(string mutation)
    {
        var (before, after) = Roots("hidden");
        var request = Create(Parse(before), Parse(after), PathId);
        switch (mutation)
        {
            case "operation": request = request with { OperationKey = "operation_other" }; break;
            case "event": request = request with { EventRef = "turn_44:other" }; break;
            case "turn": request = request with { Turn = 45 }; break;
            case "transition": request = request with { TransitionId = "transition_other" }; break;
            case "TransitionResult":
                var otherRoots = Roots("public");
                var other = WoundTransitionReducer.Reduce(Create(Parse(otherRoots.Before), Parse(otherRoots.After)));
                Assert.True(other.IsValid, Issues(other));
                request = Tamper(request, mutation, Assert.Single(other.Intents.OfType<WoundTransitionHistoryIntent>()).TransitionResult);
                break;
            case "before_image":
                before["display"]!["prognosis"] = "Changed before";
                request = request with { Before = Parse(before) };
                break;
            case "after_image_resealed":
                after["display"]!["prognosis"] = "Changed after";
                request = request with { ProposedAfter = Parse(after) };
                request = Tamper(request, "ExpectedAfterFingerprint", WoundIdentityState.ComputeSemanticFingerprint(request.ProposedAfter!));
                break;
            case "route_name":
            case "route_requirements":
            case "route_outcomes":
            case "path_name":
            case "path_requirements":
            case "path_facts":
                if (mutation == "route_name") after["treatment"]!["routes"]![2]!["displayName"] = "Changed silver ceremony";
                if (mutation == "route_requirements") after["treatment"]!["routes"]![2]!["requirements"]![0]!["itemRef"] = "different_thread";
                if (mutation == "route_outcomes") after["treatment"]!["routes"]![2]!["outcomes"]![0]!["minimumMargin"] = 6;
                if (mutation == "route_outcomes") after["treatment"]!["routes"]![2]!["outcomes"]![1]!["maximumMargin"] = 5;
                if (mutation == "path_name") after["treatment"]!["diagnosisPaths"]![1]!["displayName"] = "Changed rune reading";
                if (mutation == "path_requirements") after["treatment"]!["diagnosisPaths"]![1]!["requirements"]!.AsArray().Add(new JsonObject { ["kind"] = "provider", ["providerRef"] = "silver_monk" });
                if (mutation == "path_facts") after["treatment"]!["diagnosisPaths"]![1]!["reveals"]!.AsArray().Add("route:old_zeta");
                request = request with { ProposedAfter = Parse(after) };
                break;
            default:
                request = Tamper(request, mutation, mutation.EndsWith("Fingerprint", StringComparison.Ordinal) ? Seal('a') : "other_exact_id");
                break;
        }
        Reject(WoundTransitionReducer.Reduce(request), "wound_transition_alternative_evidence_invalid");
    }

    [Theory]
    [InlineData("RequestAuthorityFingerprint")]
    [InlineData("EvidenceAuthorityFingerprint")]
    [InlineData("RequirementAuthorityFingerprint")]
    public void Reduce_RequiresExactLowercaseExternalAuthoritySeals(string field)
    {
        var (before, after) = Roots();
        foreach (var invalid in new[] { "not_a_seal", Seal('A'), " " + Seal('1') })
        {
            var request = Create(Parse(before), Parse(after), requestSeal: field == "RequestAuthorityFingerprint" ? invalid : null,
                evidenceSeal: field == "EvidenceAuthorityFingerprint" ? invalid : null,
                requirementSeal: field == "RequirementAuthorityFingerprint" ? invalid : null);
            Reject(WoundTransitionReducer.Reduce(request), "wound_transition_alternative_evidence_invalid");
        }
    }

    [Theory]
    [InlineData("null_evidence")]
    [InlineData("wrong_evidence")]
    [InlineData("null_result")]
    [InlineData("missing_route")]
    [InlineData("nonexact_route")]
    [InlineData("old_route")]
    [InlineData("missing_path")]
    [InlineData("nonexact_path")]
    [InlineData("old_path")]
    [InlineData("hidden_no_path")]
    [InlineData("visible_path")]
    [InlineData("unsafe_request")]
    public void Reduce_RejectsMissingWrongOrUnboundSelection(string mutation)
    {
        var hidden = mutation is "missing_path" or "nonexact_path" or "old_path" or "hidden_no_path";
        var (before, after) = Roots(hidden ? "hidden" : "public");
        var path = hidden ? PathId : null;
        var route = mutation switch { "missing_route" => "missing", "nonexact_route" => RouteId.ToUpperInvariant(), "old_route" => "old_zeta", _ => RouteId };
        if (mutation == "missing_path") path = "missing";
        if (mutation == "nonexact_path") path = PathId.ToUpperInvariant();
        if (mutation is "old_path" or "visible_path") path = "old_path";
        if (mutation == "hidden_no_path") path = null;
        var request = Create(Parse(before), Parse(after), path, route, requestRef: mutation == "unsafe_request" ? "../private/request" : null);
        if (mutation == "null_evidence") request = request with { Evidence = null! };
        if (mutation == "wrong_evidence") request = request with
        {
            Evidence = MortalWoundTreatmentPlanner.CreateDiagnosisTransition("other", "command_other",
                "operation_other", "attempt_other", "event_other", 44, request.Before!, request.ProposedAfter!,
                "old_path", "failure", Seal('6'), Seal('7')).Evidence
        };
        if (mutation == "null_result") request = Tamper(request, "TransitionResult", null);
        Reject(WoundTransitionReducer.Reduce(request));
    }

    [Theory]
    [InlineData("old_requirement")]
    [InlineData("old_outcome")]
    [InlineData("old_path_text")]
    [InlineData("known_reorder")]
    [InlineData("completed_reorder")]
    [InlineData("care")]
    [InlineData("severity")]
    [InlineData("recovery")]
    [InlineData("display")]
    [InlineData("known_hidden_route")]
    public void Reduce_ParsedIllegalDeltaIsRejectedWithoutFactoryException(string mutation)
    {
        var (before, after) = Roots(mutation == "known_hidden_route" ? "hidden" : "public");
        switch (mutation)
        {
            case "old_requirement": after["treatment"]!["routes"]![0]!["requirements"]![0]!["itemRef"] = "different_old_item"; break;
            case "old_outcome": after["treatment"]!["routes"]![0]!["outcomes"]![1]!["result"]![1]!["points"] = 2; break;
            case "old_path_text": after["treatment"]!["diagnosisPaths"]![0]!["displayName"] = "Changed old reading"; break;
            case "known_reorder": after["treatment"]!["knownRouteIds"] = new JsonArray("old_zeta", "clean_and_suture", RouteId); break;
            case "completed_reorder": after["treatment"]!["completedRouteIds"] = new JsonArray("clean_and_suture", "old_zeta"); break;
            case "care": after["care"]!["lastAttemptId"] = "fake_attempt"; break;
            case "severity":
                after["severity"]!["rank"] = 3; after["severity"]!["value"] = "III";
                after["severity"]!["lastChangeEventRef"] = "turn_44:silver";
                break;
            case "recovery": after["recovery"]!["currentStepProgress"] = 1; break;
            case "display": after["display"]!["prognosis"] = "Changed prognosis"; break;
            case "known_hidden_route": after["treatment"]!["knownRouteIds"]!.AsArray().Add(RouteId); break;
        }
        var request = Create(Parse(before), Parse(after), mutation == "known_hidden_route" ? PathId : null);
        Reject(WoundTransitionReducer.Reduce(request), "wound_transition_alternative_append_invalid");
    }

    [Theory]
    [InlineData("gm_only")]
    [InlineData("irrelevant")]
    [InlineData("self_dependent")]
    [InlineData("unseeded")]
    [InlineData("confusable_route")]
    [InlineData("confusable_path")]
    [InlineData("unicode_confusable_path")]
    public void Parse_RejectsInvalidNewDiscoveryGraphAndConfusableMembers(string mutation)
    {
        var (_, after) = Roots("hidden");
        var path = after["treatment"]!["diagnosisPaths"]![1]!;
        switch (mutation)
        {
            case "gm_only": path["visibility"] = "gm_only"; break;
            case "irrelevant": path["reveals"] = new JsonArray("route:old_zeta"); break;
            case "self_dependent": path["requiresKnownFacts"] = new JsonArray("route:" + RouteId); break;
            case "unseeded": path["visibility"] = "hidden"; path["requiresKnownFacts"] = new JsonArray(); break;
            case "confusable_route": after["treatment"]!["routes"]![2]!["routeId"] = "OLD_ZETA"; path["reveals"] = new JsonArray("route:OLD_ZETA"); break;
            case "confusable_path": path["diagnosisPathId"] = "OLD_PATH"; break;
            case "unicode_confusable_path": path["diagnosisPathId"] = "old_p\u0430th"; break;
        }
        // Use a currently registered metadata kind so this assertion specifically owns structure.
        after["lastTransition"]!["kind"] = "diagnose";
        var parsed = WoundMaterializationContract.Parse(after.ToJsonString(), "wound");
        Assert.False(parsed.IsValid);
        Assert.Contains(parsed.Issues, issue => issue.FilePath.Contains("treatment", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("../request")]
    [InlineData("private\\request")]
    [InlineData("urn:request")]
    [InlineData("request id")]
    [InlineData("request\tref")]
    [InlineData("request\u00a0ref")]
    public void Reduce_RejectsNonOpaqueAuthoringRequestReferences(string reference)
    {
        var (before, after) = Roots();
        Reject(WoundTransitionReducer.Reduce(Create(Parse(before), Parse(after), requestRef: reference)),
            "wound_transition_alternative_evidence_invalid");
    }

    [Fact]
    public void Reduce_PreservesDistinctUnicodeIdsAndOpaqueReference()
    {
        var (before, after) = Roots("hidden");
        after["treatment"]!["diagnosisPaths"]![1]!["diagnosisPathId"] = "чтение_серебра";
        var reduced = WoundTransitionReducer.Reduce(Create(Parse(before), Parse(after), "чтение_серебра", requestRef: "запрос_серебра"));
        Assert.True(reduced.IsValid, Issues(reduced));
    }

    [Fact]
    public void Factory_DetachesCallerListsJsonDocumentsAndResultProjection()
    {
        var (beforeRoot, afterRoot) = Roots("hidden");
        var before = Parse(beforeRoot);
        var after = Parse(afterRoot);
        var routes = after.Treatment.Routes.ToList();
        var paths = after.Treatment.DiagnosisPaths.ToList();
        var facts = paths[^1].Reveals.ToList();
        var known = after.Treatment.KnownRouteIds.ToList();
        using var document = JsonDocument.Parse(routes[^1].Requirements[0].GetRawText());
        var requirements = new[] { document.RootElement, routes[^1].Requirements[1] };
        routes[^1] = routes[^1] with { Requirements = requirements };
        paths[^1] = paths[^1] with { Reveals = facts };
        after = after with { Treatment = after.Treatment with { Routes = routes, DiagnosisPaths = paths, KnownRouteIds = known } };
        var request = Create(before, after, PathId);
        var expected = WoundMaterializationContract.SerializeCanonical(request.ProposedAfter!);
        routes.Clear(); paths.Clear(); facts.Clear(); known.Clear(); requirements[0] = default;
        document.Dispose();
        afterRoot["display"]!["prognosis"] = "Mutated caller JSON";
        Assert.Equal(expected, WoundMaterializationContract.SerializeCanonical(request.ProposedAfter!));
        var reduced = WoundTransitionReducer.Reduce(request);
        Assert.True(reduced.IsValid, Issues(reduced));
        var result = Assert.IsType<WoundAlternativeTreatmentTransitionResult>(Assert.Single(reduced.Intents.OfType<WoundTransitionHistoryIntent>()).TransitionResult);
        var canonical = result.ToCanonicalJson().ToJsonString();
        var mutable = result.ToCanonicalJson();
        mutable["addedRouteId"] = "different_route";
        Assert.Equal(canonical, result.ToCanonicalJson().ToJsonString());
        Assert.True(WoundTransitionReducer.Reduce(request).IsValid);
    }

    [Fact]
    public void Factory_FullLocalSealsIncludeDisplayButExcludeParserSourceLocations()
    {
        var (before, after) = Roots("hidden");
        var first = Create(Parse(before), Parse(after), PathId);
        var relocated = WoundMaterializationContract.Parse(after.ToJsonString(), "different.source");
        Assert.True(relocated.IsValid);
        var second = Create(Parse(before), relocated.Wound!, PathId);
        Assert.Equal(Result(first).ToCanonicalJson().ToJsonString(), Result(second).ToCanonicalJson().ToJsonString());
        after["treatment"]!["routes"]![2]!["displayName"] = "Different silver ritual";
        after["treatment"]!["diagnosisPaths"]![1]!["displayName"] = "Different silver reading";
        var changed = Result(Create(Parse(before), Parse(after), PathId));
        Assert.NotEqual(Result(first).RouteFingerprint, changed.RouteFingerprint);
        Assert.NotEqual(Result(first).DiagnosisPathFingerprint, changed.DiagnosisPathFingerprint);
    }

    private static WoundAlternativeTreatmentTransitionResult Result(WoundTransitionRequest request)
    {
        var reduced = WoundTransitionReducer.Reduce(request);
        Assert.True(reduced.IsValid, Issues(reduced));
        return Assert.IsType<WoundAlternativeTreatmentTransitionResult>(Assert.Single(reduced.Intents.OfType<WoundTransitionHistoryIntent>()).TransitionResult);
    }

    private static (JsonObject Before, JsonObject After) Roots(string visibility = "public", string mode = "procedure")
    {
        var before = WoundContractTestData.CreateActiveWound();
        var old = before["treatment"]!["routes"]![0]!.DeepClone();
        old["routeId"] = "old_zeta";
        before["treatment"]!["routes"]!.AsArray().Add(old);
        before["treatment"]!["knownRouteIds"]!.AsArray().Add("old_zeta");
        before["treatment"]!["completedRouteIds"] = new JsonArray("old_zeta", "clean_and_suture");
        before["treatment"]!["diagnosisPaths"] = new JsonArray(Path("old_path", "route:old_zeta"));
        var after = before.DeepClone().AsObject();
        var route = Route(mode, visibility);
        after["treatment"]!["routes"]!.AsArray().Add(route);
        if (visibility == "hidden") after["treatment"]!["diagnosisPaths"]!.AsArray().Add(Path(PathId, "route:" + RouteId));
        else after["treatment"]!["knownRouteIds"]!.AsArray().Add(RouteId);
        after["lastTransition"] = new JsonObject { ["transitionId"] = "transition_silver", ["ordinal"] = 2, ["turn"] = 44, ["kind"] = "author_alternative_treatment" };
        return (before, after);
    }

    private static JsonObject Path(string id, string reveals) => new()
    {
        ["diagnosisPathId"] = id, ["displayName"] = "Read the moon-silver resonance",
        ["visibility"] = "known_to_player", ["requiresKnownFacts"] = new JsonArray("route:clean_and_suture"),
        ["requirements"] = new JsonArray(), ["check"] = new JsonObject(),
        ["reveals"] = new JsonArray(reveals), ["failurePolicy"] = "no_reveal"
    };

    private static JsonObject Route(string mode, string visibility)
    {
        var route = WoundContractTestData.CreateActiveWound()["treatment"]!["routes"]![0]!.DeepClone().AsObject();
        route["routeId"] = RouteId; route["displayName"] = "Bind the wound with moon-silver";
        route["visibility"] = visibility; route["mode"] = mode;
        if (mode == "procedure") return route;
        route["requirements"] = mode == "course" ? new JsonArray(new JsonObject { ["kind"] = "provider", ["providerRef"] = "silver_monk" }) :
            new JsonArray(new JsonObject { ["kind"] = "source_capability", ["capabilityRef"] = "moon_silver_blessing", ["actorRole"] = "provider" });
        route["resourcePolicy"] = new JsonObject
        {
            ["reserveBeforeResolution"] = true, ["consumeOn"] = new JsonArray("success"),
            ["refundOn"] = new JsonArray("cancelled", "validation_failed", "rolled_back"), ["mutations"] = new JsonArray()
        };
        if (mode == "guaranteed")
        {
            route["resolution"] = new JsonObject { ["capabilityRef"] = "moon_silver_blessing", ["actorRole"] = "provider" };
            route["outcomes"] = new JsonArray(new JsonObject { ["category"] = "success", ["result"] = new JsonArray(new JsonObject { ["kind"] = "stabilize" }) });
            return route;
        }
        route["resolution"] = new JsonObject { ["clockKind"] = "world_time.currentTimeInMinutes", ["maximumGapMinutes"] = 600 };
        route["outcomes"] = new JsonArray(Enumerable.Range(1, 2).Select(ordinal => (JsonNode)new JsonObject
        {
            ["ordinal"] = ordinal, ["afterMinutes"] = (ordinal - 1) * 480, ["requirements"] = new JsonArray(),
            ["category"] = "success", ["completion"] = ordinal == 2 ? "completed" : "active",
            ["result"] = ordinal == 2 ? new JsonArray(new JsonObject { ["kind"] = "stabilize" }) : new JsonArray()
        }).ToArray());
        route["interruption"] = new JsonObject { ["category"] = "failed_attempt", ["result"] = new JsonArray(new JsonObject { ["kind"] = "no_improvement" }) };
        return route;
    }

    private static WoundMaterializationEnvelope Parse(JsonObject root)
    {
        var parsed = WoundMaterializationContract.Parse(root.ToJsonString(), "wound");
        Assert.True(parsed.IsValid, string.Join("; ", parsed.Issues.Select(issue => $"{issue.Code}: {issue.Actual}")));
        return parsed.Wound!;
    }

    private static WoundTransitionRequest Create(WoundMaterializationEnvelope before, WoundMaterializationEnvelope after,
        string? path = null, string route = RouteId, string? requestSeal = null, string? evidenceSeal = null,
        string? requirementSeal = null, string? requestRef = null)
    {
        var factory = Assert.Single(typeof(MortalWoundTreatmentPlanner).GetMethods(BindingFlags.Static | BindingFlags.NonPublic),
            method => method.Name == "CreateAlternativeTreatmentTransition" && method.GetParameters().Length == 12);
        return Assert.IsType<WoundTransitionRequest>(factory.Invoke(null, new object?[]
        {
            "transition_silver", requestRef ?? "request_silver_authoring", requestSeal ?? Seal('1'), "operation_silver", "turn_44:silver", 44,
            before, after, route, path, evidenceSeal ?? Seal('6'), requirementSeal ?? Seal('7')
        }));
    }

    private static WoundTransitionRequest Tamper(WoundTransitionRequest request, string property, object? value)
    {
        var evidence = request.Evidence with { };
        var member = evidence.GetType().GetProperty(property, BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(member);
        member.SetValue(evidence, value);
        return request with { Evidence = evidence };
    }

    private static string Seal(char value) => "sha256:" + new string(value, 64);
    private static string Issues(WoundTransitionReductionResult result) => string.Join("; ", result.Issues.Select(issue => issue.Code));
    private static void Reject(WoundTransitionReductionResult result, string? code = null)
    {
        Assert.False(result.IsValid); Assert.Null(result.ProposedAfter); Assert.Empty(result.Intents); Assert.NotEmpty(result.Issues);
        if (code is not null) Assert.True(result.Issues.Any(issue => issue.Code == code), $"Expected {code}; got {Issues(result)}");
    }
}
