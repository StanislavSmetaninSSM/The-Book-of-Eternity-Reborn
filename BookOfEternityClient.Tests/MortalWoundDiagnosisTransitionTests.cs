using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class MortalWoundDiagnosisTransitionTests
{
    private const string PathId = "diagnosis_zeta";
    private static readonly string[] Facts =
    {
        "route:clean_and_suture", "route:route_zeta", "complication:hidden",
        "complication:public", "route:route_alpha", "complication:known"
    };

    [Theory]
    [InlineData("success")]
    [InlineData("failure")]
    public void Reduce_EmitsOnlyCarrierTerminalAttemptAndSealedNonterminalHistory(string outcome)
    {
        var (before, after) = Roots(outcome);
        var request = Create(Parse(before), Parse(after), outcome);
        var reduced = WoundTransitionReducer.Reduce(request);
        Assert.True(reduced.IsValid, Issues(reduced));
        Assert.Equal(3, reduced.Intents.Count);
        var carrier = Assert.Single(reduced.Intents.OfType<WoundCarrierTransitionIntent>());
        Assert.Equal("replace", carrier.Operation);
        Assert.Equal(request.Before!.Owner, carrier.Owner);
        Assert.Equal(request.Before.WoundId, carrier.WoundId);
        var attempt = Assert.Single(reduced.Intents.OfType<WoundAttemptTerminalIntent>());
        Assert.Equal("attempt_diagnosis", attempt.AttemptId);
        Assert.Equal(PathId, attempt.RouteOrGateRef);
        Assert.Equal(carrier.WoundId, attempt.WoundId);
        var history = Assert.Single(reduced.Intents.OfType<WoundTransitionHistoryIntent>());
        Assert.False(history.Terminal);
        Assert.Null(history.TickKey);
        Assert.Equal(attempt.AttemptId, history.AttemptId);
        Assert.Equal(request.TransitionId, history.TransitionId);
        Assert.Equal(request.OperationKey, history.OperationKey);
        Assert.Equal(request.EventRef, history.EventRef);
        Assert.Equal(request.Turn, history.Turn);
        var result = Assert.IsType<WoundDiagnosisTransitionResult>(history.TransitionResult);
        Assert.Equal(PathId, result.DiagnosisPathId);
        Assert.Equal(outcome, result.Result);
        Assert.Equal(outcome == "success" ? Facts : Array.Empty<string>(), result.RevealedFacts);
        Assert.Equal(WoundHistoryState.ComputeTransitionResultFingerprint(result.ToCanonicalJson()),
            result.ResultFingerprint);
        Assert.Equal(WoundMaterializationContract.SerializeCanonical(Parse(after)),
            WoundMaterializationContract.SerializeCanonical(reduced.ProposedAfter!));
        Assert.Equal("public", reduced.ProposedAfter!.Complications[1].Visibility);
        Assert.Equal(request.Before.Care, reduced.ProposedAfter.Care);
    }

    [Theory]
    [InlineData("public", true)]
    [InlineData("known_to_player", true)]
    [InlineData("hidden", true)]
    [InlineData("gm_only", false)]
    [InlineData("missing", false)]
    [InlineData("case_confusable", false)]
    [InlineData("second_step", false)]
    [InlineData("public_second_step", false)]
    [InlineData("known_second_step", false)]
    public void Reduce_UsesExactCurrentlyKnownPathAvailability(string selection, bool accepted)
    {
        var (before, _) = Roots("failure");
        var paths = before["treatment"]!["diagnosisPaths"]!.AsArray();
        var pathId = PathId;
        if (selection is "missing" or "case_confusable")
            pathId = selection == "missing" ? "diagnosis_missing" : "DIAGNOSIS_ZETA";
        else if (selection.EndsWith("second_step", StringComparison.Ordinal))
        {
            paths[0]!["reveals"] = new JsonArray("route:route_zeta");
            paths[1]!["requiresKnownFacts"] = new JsonArray("route:route_zeta");
            paths[1]!["reveals"] = new JsonArray("route:route_alpha", "route:route_spare");
            paths[1]!["visibility"] = selection switch
            {
                "public_second_step" => "public",
                "known_second_step" => "known_to_player",
                _ => "hidden"
            };
            pathId = "diagnosis_other";
        }
        else
            paths[0]!["visibility"] = selection;
        var after = Advance(before);
        var reduced = WoundTransitionReducer.Reduce(Create(Parse(before), Parse(after), "failure", pathId));
        if (accepted)
            Assert.True(reduced.IsValid, Issues(reduced));
        else
            AssertRejected(reduced, "wound_transition_diagnosis_path_unavailable");
    }

    [Theory]
    [InlineData("success", "missing_route", "fact_unauthorized")]
    [InlineData("success", "reordered_routes", "fact_unauthorized")]
    [InlineData("success", "extra_route", "fact_unauthorized")]
    [InlineData("success", "private_reveal", "fact_unauthorized")]
    [InlineData("success", "missing_complication", "fact_unauthorized")]
    [InlineData("success", "public_downgrade", "fact_unauthorized")]
    [InlineData("success", "complication_text", "fact_unauthorized")]
    [InlineData("success", "display", "display_changed")]
    [InlineData("success", "route_content", "mechanics_changed")]
    [InlineData("success", "path_content", "mechanics_changed")]
    [InlineData("success", "care", "mechanics_changed")]
    [InlineData("success", "severity", "mechanics_changed")]
    [InlineData("success", "recovery", "mechanics_changed")]
    [InlineData("failure", "extra_route", "fact_unauthorized")]
    [InlineData("failure", "private_reveal", "fact_unauthorized")]
    [InlineData("failure", "display", "display_changed")]
    [InlineData("failure", "care", "mechanics_changed")]
    [InlineData("failure", "path_content", "mechanics_changed")]
    public void Reduce_RejectsParsedSemanticAfterImageViolations(string outcome, string mutation, string code)
    {
        var (before, after) = Roots(outcome);
        var known = after["treatment"]!["knownRouteIds"]!.AsArray();
        switch (mutation)
        {
            case "missing_route": known.RemoveAt(2); break;
            case "reordered_routes": known[1] = "route_alpha"; known[2] = "route_zeta"; break;
            case "extra_route": known.Add("route_spare"); break;
            case "private_reveal": after["complications"]![3]!["visibility"] = "known_to_player"; break;
            case "missing_complication": after["complications"]![0]!["visibility"] = "hidden"; break;
            case "public_downgrade": after["complications"]![1]!["visibility"] = "known_to_player"; break;
            case "complication_text": after["complications"]![3]!["displayName"] = "Changed private text"; break;
            case "display": after["display"]!["prognosis"] = "Private prognosis"; break;
            case "route_content": after["treatment"]!["routes"]![0]!["displayName"] = "Changed route"; break;
            case "path_content": after["treatment"]!["diagnosisPaths"]![0]!["displayName"] = "Changed path"; break;
            case "care": after["care"]!["lastAttemptId"] = "attempt_diagnosis"; break;
            case "severity":
                after["severity"]!["rank"] = 3;
                after["severity"]!["value"] = "III";
                after["severity"]!["lastChangeEventRef"] = "turn_43:diagnosis";
                break;
            case "recovery": after["recovery"]!["currentStepProgress"] = 1; break;
        }
        // Both images must parse before the factory; semantic rejection belongs to Reduce.
        var request = Create(Parse(before), Parse(after), outcome);
        AssertRejected(WoundTransitionReducer.Reduce(request), "wound_transition_diagnosis_" + code);
    }

    [Theory]
    [InlineData("operation")]
    [InlineData("event")]
    [InlineData("turn")]
    [InlineData("transition")]
    [InlineData("AuthorityRef")]
    [InlineData("AttemptId")]
    [InlineData("WoundId")]
    [InlineData("DiagnosisPathId")]
    [InlineData("ResultKind")]
    [InlineData("RequirementAuthorityFingerprint")]
    [InlineData("CheckResultFingerprint")]
    [InlineData("ExpectedBeforeFingerprint")]
    [InlineData("ExpectedAfterFingerprint")]
    [InlineData("DiagnosisPathFingerprint")]
    [InlineData("RevealedFacts")]
    [InlineData("TransitionResult")]
    [InlineData("before_image")]
    [InlineData("after_image")]
    [InlineData("after_image_resealed")]
    [InlineData("path_after_image")]
    [InlineData("path_check")]
    [InlineData("path_failure_policy")]
    public void Reduce_RejectsPostFactoryTransplantationWithoutIntents(string mutation)
    {
        var (before, after) = Roots("success");
        var request = Create(Parse(before), Parse(after));
        switch (mutation)
        {
            case "operation": request = request with { OperationKey = "operation_other" }; break;
            case "event": request = request with { EventRef = "turn_43:other" }; break;
            case "turn": request = request with { Turn = 44 }; break;
            case "transition": request = request with { TransitionId = "transition_other" }; break;
            case "TransitionResult":
                var other = Create(Parse(before), Parse(Advance(before)), "failure");
                request = Tamper(request, mutation, Property(other.Evidence, "TransitionResult"));
                break;
            case "path_check":
            case "path_failure_policy":
                var proposed = request.ProposedAfter!;
                var paths = proposed.Treatment.DiagnosisPaths.ToArray();
                paths[0] = mutation == "path_check"
                    ? paths[0] with { Check = JsonSerializer.SerializeToElement(new { unauthorized = true }) }
                    : paths[0] with { FailurePolicy = "reveal_on_failure" };
                request = request with { ProposedAfter = proposed with
                {
                    Treatment = proposed.Treatment with { DiagnosisPaths = paths }
                } };
                break;
            case "before_image":
                before["display"]!["prognosis"] = "Changed before";
                request = request with { Before = Parse(before) };
                break;
            case "after_image":
            case "after_image_resealed":
            case "path_after_image":
                if (mutation == "path_after_image")
                    after["treatment"]!["diagnosisPaths"]![0]!["displayName"] = "Changed path";
                else
                    after["display"]!["prognosis"] = "Changed after";
                request = request with { ProposedAfter = Parse(after) };
                if (mutation == "after_image_resealed")
                    request = Tamper(request, "ExpectedAfterFingerprint",
                        WoundIdentityState.ComputeSemanticFingerprint(request.ProposedAfter!));
                break;
            default:
                request = Tamper(request, mutation, mutation switch
                {
                    "ResultKind" => "failure",
                    "RevealedFacts" => new[] { "route:route_spare" },
                    var name when name.EndsWith("Fingerprint", StringComparison.Ordinal) => Seal('a'),
                    _ => "identity_other"
                });
                break;
        }
        AssertRejected(WoundTransitionReducer.Reduce(request), "wound_transition_diagnosis_evidence_mismatch");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("untyped")]
    [InlineData("route:missing")]
    [InlineData("null_element")]
    public void Reduce_MalformedEvidenceFactsRejectWithoutThrowing(string? fact)
    {
        var (before, after) = Roots("success");
        var request = Create(Parse(before), Parse(after));
        request = Tamper(request, "RevealedFacts", fact is null ? null :
            fact == "null_element" ? new string[] { null! } : new[] { fact });
        var exception = Record.Exception(() => AssertRejected(WoundTransitionReducer.Reduce(request)));
        Assert.Null(exception);
    }

    [Theory]
    [InlineData("RequirementAuthorityFingerprint", "not_a_seal")]
    [InlineData("RequirementAuthorityFingerprint", "SHA256")]
    [InlineData("CheckResultFingerprint", "not_a_seal")]
    [InlineData("CheckResultFingerprint", "SHA256")]
    public void Reduce_RequiresLowercaseExternalAuthoritySeals(string field, string invalid)
    {
        var (before, after) = Roots("failure");
        var bad = invalid == "SHA256" ? Seal('A') : invalid;
        var request = Create(Parse(before), Parse(after), "failure", requirement:
            field == "RequirementAuthorityFingerprint" ? bad : Seal('6'), check:
            field == "CheckResultFingerprint" ? bad : Seal('7'));
        AssertRejected(WoundTransitionReducer.Reduce(request), "wound_transition_evidence_invalid");
    }

    [Theory]
    [InlineData("success", false, false)]
    [InlineData("failure", false, true)]
    [InlineData("success", true, true)]
    public void Reduce_DiagnosisResultCardinalityMatchesDurableHistory(
        string outcome, bool declaresKnownFact, bool accepted)
    {
        var (beforeRoot, _) = Roots("failure");
        beforeRoot["treatment"]!["diagnosisPaths"]![0]!["reveals"] = declaresKnownFact
            ? new JsonArray("route:clean_and_suture")
            : new JsonArray();
        var before = Parse(beforeRoot);
        var request = Create(before, Parse(Advance(beforeRoot)), outcome);
        var reduced = WoundTransitionReducer.Reduce(request);
        if (!accepted)
        {
            AssertRejected(reduced, "wound_transition_diagnosis_fact_unauthorized");
            return;
        }

        Assert.True(reduced.IsValid, Issues(reduced));
        Assert.Equal(3, reduced.Intents.Count);
        Assert.Single(reduced.Intents.OfType<WoundCarrierTransitionIntent>());
        Assert.Single(reduced.Intents.OfType<WoundAttemptTerminalIntent>());
        var intent = Assert.Single(reduced.Intents.OfType<WoundTransitionHistoryIntent>());
        var result = Assert.IsType<WoundDiagnosisTransitionResult>(intent.TransitionResult);
        Assert.Equal(outcome, result.Result);
        Assert.Equal(declaresKnownFact ? new[] { "route:clean_and_suture" } : Array.Empty<string>(),
            result.RevealedFacts);
        Assert.Equal(before.Treatment.KnownRouteIds, reduced.ProposedAfter!.Treatment.KnownRouteIds);
        Assert.Equal(before.Care, reduced.ProposedAfter.Care);

        var empty = WoundHistoryState.CreateValidated(1, Array.Empty<WoundHistoryTransition>());
        Assert.True(empty.IsValid);
        var creation = new WoundTransitionHistoryIntent(before.LastTransition.TransitionId,
            before.WoundId, "create", "operation_create_cardinality", before.Origin.EventRef,
            before.LastTransition.Turn, WoundHistoryState.ComputeNonexistentBeforeFingerprint(before.WoundId),
            WoundIdentityState.ComputeSemanticFingerprint(before), null, null, false);
        var created = empty.State!.AppendTransition(creation, Seal('8'),
            WoundHistoryState.ComputeOutputFingerprint(creation.OperationKey, creation.EventRef, "Created"),
            "Created");
        Assert.True(created.IsValid, string.Join("; ", created.Issues.Select(issue => issue.Code)));
        var appended = created.State!.AppendTransition(intent, Seal('8'),
            WoundHistoryState.ComputeOutputFingerprint(intent.OperationKey, intent.EventRef, "Diagnosed"),
            "Diagnosed");
        Assert.True(appended.IsValid, string.Join("; ", appended.Issues.Select(issue => issue.Code)));
        Assert.Equal(2, appended.State!.Transitions.Count);
        var replay = appended.State.ResolveReplay(
            WoundHistoryState.CreateReplayProbe(appended.State.Transitions[^1]));
        Assert.Equal(WoundHistoryReplayDisposition.Exact, replay.Disposition);
        var durable = Assert.IsType<WoundDiagnosisTransitionResult>(replay.AlreadyAcceptedReceipt!.TransitionResult);
        Assert.True(JsonNode.DeepEquals(result.ToCanonicalJson(), durable.ToCanonicalJson()));
    }

    [Fact]
    public void Factory_DetachesMutableInputsFactsAndCanonicalProjections()
    {
        var (beforeRoot, afterRoot) = Roots("success");
        var before = Parse(beforeRoot);
        var facts = Facts.ToList();
        var known = before.Treatment.KnownRouteIds.ToList();
        using var json = JsonDocument.Parse("{}");
        var paths = before.Treatment.DiagnosisPaths.ToList();
        paths[0] = paths[0] with { Reveals = facts, Check = json.RootElement };
        before = before with { Treatment = before.Treatment with { DiagnosisPaths = paths, KnownRouteIds = known } };
        var after = Parse(afterRoot);
        var afterKnown = after.Treatment.KnownRouteIds.ToList();
        after = after with { Treatment = after.Treatment with { KnownRouteIds = afterKnown } };
        var request = Create(before, after);
        facts.Clear(); known.Clear(); paths.Clear(); afterKnown.Clear(); json.Dispose();
        beforeRoot["display"]!["prognosis"] = "Changed source JSON";
        var reduced = WoundTransitionReducer.Reduce(request);
        Assert.True(reduced.IsValid, Issues(reduced));
        var result = Assert.IsType<WoundDiagnosisTransitionResult>(
            Assert.Single(reduced.Intents.OfType<WoundTransitionHistoryIntent>()).TransitionResult);
        var projection = result.ToCanonicalJson();
        projection["revealedFacts"]!.AsArray().Clear();
        var canonical = JsonNode.Parse(WoundMaterializationContract.SerializeCanonical(request.Before!))!;
        canonical["treatment"]!["knownRouteIds"]!.AsArray().Clear();
        Assert.Equal(Facts, result.RevealedFacts);
        Assert.True(WoundTransitionReducer.Reduce(request).IsValid);
    }

    [Theory]
    [InlineData("displayName")]
    [InlineData("requirements")]
    [InlineData("requiresKnownFacts")]
    [InlineData("reveals")]
    [InlineData("visibility")]
    public void Factory_PathSealBindsCompleteAuthoredContent(string field)
    {
        var (before, after) = Roots("failure");
        var original = Create(Parse(before), Parse(after), "failure");
        var path = before["treatment"]!["diagnosisPaths"]![0]!;
        switch (field)
        {
            case "displayName": path[field] = "Different readable name"; break;
            case "visibility": path[field] = "public"; break;
            case "requirements": path[field] = new JsonArray(before["treatment"]!["routes"]![0]!["requirements"]![0]!.DeepClone()); break;
            case "requiresKnownFacts": path[field]!.AsArray().Add("complication:known"); break;
            case "reveals": path[field] = new JsonArray(Facts.Reverse().Select(value => (JsonNode)value).ToArray()); break;
        }
        var changed = Create(Parse(before), Parse(Advance(before)), "failure");
        Assert.NotEqual(Property(original.Evidence, "DiagnosisPathFingerprint"),
            Property(changed.Evidence, "DiagnosisPathFingerprint"));
    }

    [Fact]
    public void Factory_PathSealIgnoresParserSourcePath()
    {
        var (before, after) = Roots("failure");
        var first = Create(Parse(before), Parse(after), "failure");
        var parsedElsewhere = WoundMaterializationContract.Parse(before.ToJsonString(), "another.source");
        Assert.True(parsedElsewhere.IsValid);
        var second = Create(parsedElsewhere.Wound!, Parse(after), "failure");
        Assert.Equal(Property(first.Evidence, "DiagnosisPathFingerprint"),
            Property(second.Evidence, "DiagnosisPathFingerprint"));
    }

    [Theory]
    [InlineData("public")]
    [InlineData("known_to_player")]
    public void Reduce_ComplicationFactsAloneCanUnlockAHiddenPath(string visibility)
    {
        var (before, _) = Roots("failure");
        before["complications"]![1]!["visibility"] = visibility;
        var path = before["treatment"]!["diagnosisPaths"]![0]!;
        path["visibility"] = "hidden";
        path["requiresKnownFacts"] = new JsonArray("complication:public");
        var request = Create(Parse(before), Parse(Advance(before)), "failure");
        var reduced = WoundTransitionReducer.Reduce(request);
        Assert.True(reduced.IsValid, Issues(reduced));
    }

    [Fact]
    public void Reduce_SpiritualWoundCannotUseMortalDiagnosisAuthority()
    {
        var before = WoundContractTestData.CreateSpiritualActiveWound();
        var request = Create(Parse(before), Parse(Advance(before)), "failure");
        AssertRejected(WoundTransitionReducer.Reduce(request), "wound_transition_active_source_invalid");
    }

    private static (JsonObject Before, JsonObject After) Roots(string outcome)
    {
        var before = WoundContractTestData.CreateActiveWound();
        var treatment = before["treatment"]!;
        foreach (var id in new[] { "route_zeta", "route_alpha", "route_spare" })
        {
            var route = treatment["routes"]![0]!.DeepClone();
            route["routeId"] = id;
            route["visibility"] = "hidden";
            treatment["routes"]!.AsArray().Add(route);
        }
        before["complications"] = new JsonArray(new[] { "hidden", "public", "known", "private" }
            .Select(id => (JsonNode)new JsonObject
            {
                ["complicationId"] = id, ["kind"] = "infection", ["state"] = "active",
                ["displayName"] = id, ["treatmentDifficultyModifier"] = 1,
                ["ownedEffectIds"] = new JsonArray(),
                ["visibility"] = id == "known" ? "known_to_player" : id == "private" ? "gm_only" : id
            }).ToArray());
        var path = new JsonObject
        {
            ["diagnosisPathId"] = PathId, ["displayName"] = "Examine the wound",
            ["visibility"] = "known_to_player", ["requiresKnownFacts"] = new JsonArray("route:clean_and_suture"),
            ["requirements"] = new JsonArray(), ["check"] = new JsonObject(),
            ["reveals"] = new JsonArray(Facts.Select(fact => (JsonNode)fact).ToArray()),
            ["failurePolicy"] = "no_reveal"
        };
        var other = path.DeepClone();
        other["diagnosisPathId"] = "diagnosis_other";
        other["reveals"]!.AsArray().Add("route:route_spare");
        treatment["diagnosisPaths"] = new JsonArray(path, other);
        var after = Advance(before);
        if (outcome == "success")
        {
            after["treatment"]!["knownRouteIds"]!.AsArray().Add("route_zeta");
            after["treatment"]!["knownRouteIds"]!.AsArray().Add("route_alpha");
            after["complications"]![0]!["visibility"] = "known_to_player";
        }
        return (before, after);
    }

    private static JsonObject Advance(JsonObject before)
    {
        var after = before.DeepClone().AsObject();
        after["lastTransition"] = new JsonObject
        {
            ["transitionId"] = "transition_diagnosis", ["ordinal"] = 2, ["turn"] = 43, ["kind"] = "diagnose"
        };
        return after;
    }

    private static WoundMaterializationEnvelope Parse(JsonObject root)
    {
        var parsed = WoundMaterializationContract.Parse(root.ToJsonString(), "wound");
        Assert.True(parsed.IsValid, string.Join("; ", parsed.Issues.Select(issue => $"{issue.Code}: {issue.Actual}")));
        return parsed.Wound!;
    }

    private static WoundTransitionRequest Create(WoundMaterializationEnvelope before,
        WoundMaterializationEnvelope after, string outcome = "success", string path = PathId,
        string? requirement = null, string? check = null)
    {
        var factory = Assert.Single(typeof(MortalWoundTreatmentPlanner).GetMethods(BindingFlags.Static | BindingFlags.NonPublic),
            method => method.Name == "CreateDiagnosisTransition" && method.GetParameters().Length == 12);
        return Assert.IsType<WoundTransitionRequest>(factory.Invoke(null, new object[]
        {
            "transition_diagnosis", "command_diagnosis", "operation_diagnosis", "attempt_diagnosis",
            "turn_43:diagnosis", 43, before, after, path, outcome, requirement ?? Seal('6'), check ?? Seal('7')
        }));
    }

    private static WoundTransitionRequest Tamper(WoundTransitionRequest request, string property, object? value)
    {
        var evidence = request.Evidence with { };
        var member = evidence.GetType().GetProperty(property, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(member);
        member.SetValue(evidence, value);
        return request with { Evidence = evidence };
    }

    private static object? Property(object value, string name) => value.GetType().GetProperty(name)!.GetValue(value);
    private static string Seal(char value) => "sha256:" + new string(value, 64);
    private static string Issues(WoundTransitionReductionResult result) => string.Join("; ", result.Issues.Select(issue => issue.Code));
    private static void AssertRejected(WoundTransitionReductionResult result, string? code = null)
    {
        Assert.False(result.IsValid);
        Assert.Null(result.ProposedAfter);
        Assert.Empty(result.Intents);
        Assert.NotEmpty(result.Issues);
        if (code is not null)
            Assert.True(result.Issues.Any(issue => issue.Code == code), $"Expected {code}; got {Issues(result)}");
    }
}
