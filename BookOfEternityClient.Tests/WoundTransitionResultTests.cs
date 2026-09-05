using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class WoundTransitionResultTests
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private const string ResultPath = "history.transitions[1].transitionResult";

    [Theory]
    [InlineData("unknown", "extra", "wound_history_unknown_field")]
    [InlineData("path_type", "diagnosisPathId", "wound_history_invalid_identifier")]
    [InlineData("path_id", "diagnosisPathId", "wound_history_invalid_identifier")]
    [InlineData("kind", "kind", "wound_history_invalid_field")]
    [InlineData("category", "result", "wound_history_invalid_field")]
    [InlineData("facts_type", "revealedFacts", "wound_history_invalid_field")]
    [InlineData("fact_type", "revealedFacts[0]", "wound_history_invalid_field")]
    [InlineData("fact_vocabulary", "revealedFacts[0]", "wound_history_invalid_field")]
    [InlineData("fact_id", "revealedFacts[0]", "wound_history_invalid_identifier")]
    [InlineData("duplicate_fact", "revealedFacts[1]", "wound_history_invalid_field")]
    [InlineData("confusable_fact", "revealedFacts[1]", "wound_history_invalid_field")]
    [InlineData("too_many", "revealedFacts", "wound_history_limit_exceeded")]
    [InlineData("seal_type", "resultFingerprint", "wound_history_invalid_fingerprint")]
    public void Diagnosis_RejectsClosedBoundaryViolations(string mutation, string field, string code)
    {
        var result = Diagnosis("route:route_a");
        switch (mutation)
        {
            case "unknown": result["extra"] = true; break;
            case "path_type": result["diagnosisPathId"] = 1; break;
            case "path_id": result["diagnosisPathId"] = " bad "; break;
            case "kind": result["kind"] = "unknown"; break;
            case "category": result["result"] = true; break;
            case "facts_type": result["revealedFacts"] = new JsonObject(); break;
            case "fact_type": result["revealedFacts"] = new JsonArray(new JsonObject()); break;
            case "fact_vocabulary": result["revealedFacts"] = new JsonArray("secret:route_a"); break;
            case "fact_id": result["revealedFacts"] = new JsonArray("route: bad "); break;
            case "duplicate_fact": result["revealedFacts"] = new JsonArray("route:route_a", "route:route_a"); break;
            case "confusable_fact": result["revealedFacts"] = new JsonArray("route:route_a", "route:ROUTE_A"); break;
            case "too_many": result["revealedFacts"] = new JsonArray(Enumerable.Range(0, 17).Select(i => (JsonNode)$"route:r_{i}").ToArray()); break;
            case "seal_type": result["resultFingerprint"] = 1; break;
        }
        if (mutation != "seal_type") result = Seal(result);
        AssertIssue(History(result), ResultPath + "." + field, code);
    }

    [Theory]
    [InlineData("authoringRequestRef")]
    [InlineData("addedRouteId")]
    [InlineData("routeFingerprint")]
    [InlineData("addedDiagnosisPathId")]
    [InlineData("diagnosisPathFingerprint")]
    [InlineData("resultFingerprint")]
    public void Alternative_RequiresEveryClosedField(string field)
    {
        var result = Alternative();
        result.Remove(field);
        AssertIssue(History(result, "author_alternative_treatment"), ResultPath + "." + field, "wound_history_missing_field");
    }

    [Theory]
    [InlineData("authoringRequestRef", "wound_history_invalid_identifier")]
    [InlineData("addedRouteId", "wound_history_invalid_identifier")]
    [InlineData("addedDiagnosisPathId", "wound_history_invalid_identifier")]
    [InlineData("routeFingerprint", "wound_history_invalid_fingerprint")]
    [InlineData("diagnosisPathFingerprint", "wound_history_invalid_fingerprint")]
    public void Alternative_RejectsWrongScalarTypes(string field, string code)
    {
        var result = Alternative();
        result[field] = new JsonObject();
        AssertIssue(History(Seal(result), "author_alternative_treatment"), ResultPath + "." + field, code);
    }

    [Fact]
    public void Treatment_ExplicitNullStillUsesStrictTreatmentCodec()
    {
        var root = History(Diagnosis(), "treat");
        root["transitions"]![1]!["transitionResult"] = null;
        var parsed = WoundHistoryState.Parse(root.ToJsonString(), "history");
        Assert.False(parsed.IsValid);
        Assert.Contains(parsed.Issues, i => i.FilePath == ResultPath);
    }

    [Fact]
    public void Diagnosis_ConstructorAndCanonicalProjectionDetachCallerArrays()
    {
        var facts = new[] { "route:z", "complication:a" };
        var canonical = Diagnosis(facts);
        var result = new WoundDiagnosisTransitionResult("path_exact", "success", facts,
            canonical["resultFingerprint"]!.GetValue<string>());
        facts[0] = "route:changed";
        var exported = result.ToCanonicalJson();
        exported["revealedFacts"]![0] = "route:changed_again";
        Assert.Equal(new[] { "route:z", "complication:a" }, result.RevealedFacts);
        Assert.True(JsonNode.DeepEquals(canonical, result.ToCanonicalJson()));
    }

    [Fact]
    public void Alternative_PresentPathPairRoundTripsAndAllCoordinatesConflict()
    {
        var result = Alternative();
        result["addedDiagnosisPathId"] = "path_exact";
        result["diagnosisPathFingerprint"] = Fingerprint('b');
        result = Seal(result);
        var before = Parse(History(result, "author_alternative_treatment"));
        foreach (var field in new[] { "authoringRequestRef", "addedRouteId", "addedDiagnosisPathId", "routeFingerprint", "diagnosisPathFingerprint" })
        {
            var changed = result.DeepClone().AsObject();
            changed[field] = field.EndsWith("Fingerprint", StringComparison.Ordinal) ? Fingerprint('c') : "changed_exact";
            var candidate = Parse(History(Seal(changed), "author_alternative_treatment"));
            Assert.Equal(WoundHistoryReplayDisposition.Conflict,
                before.ResolveReplay(WoundHistoryState.CreateReplayProbe(candidate.Transitions[1])).Disposition);
        }
    }

    [Theory]
    [InlineData("addedDiagnosisPathId", "path_a")]
    [InlineData("diagnosisPathFingerprint", "fingerprint")]
    public void Alternative_RejectsUnpairedPath(string field, string value)
    {
        var result = Alternative();
        result[field] = value == "fingerprint" ? Fingerprint('b') : value;
        AssertIssue(History(Seal(result), "author_alternative_treatment"), ResultPath + ".addedDiagnosisPathId", "wound_history_invalid_field");
    }

    [Theory]
    [InlineData("diagnose")]
    [InlineData("author_alternative_treatment")]
    public void RequiredResults_RejectNullAndDuplicates(string kind)
    {
        var history = History(kind == "diagnose" ? Diagnosis() : Alternative(), kind);
        history["transitions"]![1]!["transitionResult"] = null;
        AssertIssue(history, ResultPath, "wound_history_missing_field");
        history = History(kind == "diagnose" ? Diagnosis() : Alternative(), kind);
        var json = history.ToJsonString().Replace("\"resultFingerprint\":", "\"kind\":\"" + kind + "\",\"resultFingerprint\":", StringComparison.Ordinal);
        var parsed = WoundHistoryState.Parse(json, "history");
        Assert.Contains(parsed.Issues, i => i.FilePath == ResultPath + ".kind" && i.Code == "wound_history_duplicate_property");
    }

    [Theory]
    [InlineData("create")]
    [InlineData("treat")]
    [InlineData("recover")]
    public void OtherKinds_RejectDiagnosisResult(string kind)
    {
        var parsed = WoundHistoryState.Parse(History(Diagnosis(), kind).ToJsonString(), "history");
        Assert.False(parsed.IsValid);
        Assert.Contains(parsed.Issues, i => i.FilePath.StartsWith(ResultPath, StringComparison.Ordinal));
    }

    [Fact]
    public void Seal_IsPropertyOrderIndependentButArrayOrderSensitiveAndDoesNotTrustOrMutateInput()
    {
        var input = Diagnosis("route:z", "complication:a");
        var before = input.ToJsonString();
        var reverseFields = new JsonObject(input.Reverse().Select(p => KeyValuePair.Create(p.Key, p.Value?.DeepClone())));
        Assert.Equal(FingerprintOf(input), FingerprintOf(reverseFields));
        Assert.Equal(before, input.ToJsonString());
        reverseFields["resultFingerprint"] = Fingerprint('f');
        Assert.Equal(FingerprintOf(input), FingerprintOf(reverseFields));
        reverseFields["revealedFacts"] = new JsonArray("complication:a", "route:z");
        Assert.NotEqual(FingerprintOf(input), FingerprintOf(reverseFields));
        var alternative = Alternative();
        var nullSeal = FingerprintOf(alternative);
        alternative.Remove("addedDiagnosisPathId");
        Assert.NotEqual(nullSeal, FingerprintOf(alternative));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ParsedResult_IsDetachedAndSerializesIdenticallyThroughObjectBaseAndReceipt(bool alternative)
    {
        var input = alternative ? Alternative() : Diagnosis("route:z", "complication:a");
        var state = Parse(History(input, alternative ? "author_alternative_treatment" : "diagnose"));
        var transition = state.Transitions[1];
        var result = Result(transition);
        var expected = input.ToJsonString();
        input["kind"] = "tampered";
        var serialized = JsonSerializer.SerializeToNode(result, Options)!;
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(expected), serialized));
        var baseType = result.GetType().BaseType!;
        Assert.Equal("WoundTransitionResult", baseType.Name);
        Assert.True(JsonNode.DeepEquals(serialized, JsonSerializer.SerializeToNode(result, baseType, Options)));
        serialized["kind"] = "mutated_return";
        var probe = WoundHistoryState.CreateReplayProbe(transition);
        var replay = state.ResolveReplay(probe);
        Assert.Equal(WoundHistoryReplayDisposition.Exact, replay.Disposition);
        var receipt = JsonSerializer.SerializeToNode(replay.AlreadyAcceptedReceipt, Options)!;
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(expected), receipt["transitionResult"]));
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(expected), JsonSerializer.SerializeToNode(Result(probe), Options)));
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(expected), JsonNode.Parse(WoundHistoryState.SerializeCanonical(state))!["transitions"]![1]!["transitionResult"]));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AcceptedReplayWrapper_RetainsOriginalTypedReceipt(bool alternative)
    {
        var state = Parse(History(alternative ? Alternative() : Diagnosis(),
            alternative ? "author_alternative_treatment" : "diagnose"));
        var row = state.Transitions[1];
        var replay = WoundAcceptedTurnPlanner.ResolveAcceptedReplay(state, WoundHistoryState.CreateReplayProbe(row));
        Assert.Equal(WoundHistoryReplayDisposition.Exact, replay.Disposition);
        Assert.Same(row.TransitionResult, replay.AlreadyAcceptedReceipt!.TransitionResult);
    }

    [Theory]
    [InlineData("path")]
    [InlineData("order")]
    [InlineData("category")]
    [InlineData("alternative")]
    public void ReplayAndPlanInput_CompareFullResult(string mutation)
    {
        var input = mutation == "alternative" ? Alternative() : Diagnosis("route:z", "complication:a");
        var kind = mutation == "alternative" ? "author_alternative_treatment" : "diagnose";
        var before = Parse(History(input, kind));
        switch (mutation)
        {
            case "path": input["diagnosisPathId"] = "path_changed"; break;
            case "order": input["revealedFacts"] = new JsonArray("complication:a", "route:z"); break;
            case "category": input["result"] = "failure"; input["revealedFacts"] = new JsonArray(); break;
            case "alternative": input["routeFingerprint"] = Fingerprint('c'); break;
        }
        var changed = Parse(History(Seal(input), kind));
        var replay = before.ResolveReplay(WoundHistoryState.CreateReplayProbe(changed.Transitions[1]));
        Assert.Equal(WoundHistoryReplayDisposition.Conflict, replay.Disposition);
        Assert.Contains(replay.Issues, i => i.Code == "wound_history_conflicting_replay");
        Assert.NotEqual(PlanInput(Intent(before.Transitions[1])), PlanInput(Intent(changed.Transitions[1])));
    }

    [Fact]
    public void Append_AssignsOrdinalsPreservesPriorRowsAndReturnsOriginalResult()
    {
        var parsed = Parse(History(Diagnosis()));
        var before = Parse(new JsonObject { ["schemaVersion"] = 1, ["nextOrdinal"] = 2, ["transitions"] = new JsonArray(JsonNode.Parse(WoundHistoryState.SerializeCanonical(parsed))!["transitions"]![0]!.DeepClone()) });
        var previousRows = JsonNode.Parse(WoundHistoryState.SerializeCanonical(before))!["transitions"]!.AsArray();
        var intent = Intent(parsed.Transitions[1]);
        var method = typeof(WoundHistoryState).GetMethod("AppendTransition", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var output = WoundHistoryState.ComputeOutputFingerprint(intent.OperationKey, intent.EventRef, "Accepted diagnosis.");
        var appended = Assert.IsType<WoundHistoryParseResult>(method.Invoke(before, new object[] { intent, Fingerprint('d'), output, "Accepted diagnosis." }));
        Assert.True(appended.IsValid, string.Join(",", appended.Issues.Select(i => i.Code)));
        Assert.Equal(before.NextOrdinal + 1, appended.State!.NextOrdinal);
        var appendedRows = JsonNode.Parse(WoundHistoryState.SerializeCanonical(appended.State))!["transitions"]!.AsArray();
        Assert.True(JsonNode.DeepEquals(previousRows[0], appendedRows[0]));
        Assert.Equal(previousRows[0]!.ToJsonString(), appendedRows[0]!.ToJsonString());
        var replay = appended.State.ResolveReplay(WoundHistoryState.CreateReplayProbe(appended.State.Transitions[1]));
        Assert.Equal(WoundHistoryReplayDisposition.Exact, replay.Disposition);
        Assert.NotNull(Result(replay.AlreadyAcceptedReceipt!));
        Assert.Equal(2, before.NextOrdinal);
    }

    [Theory]
    [InlineData("output", "wound_history_output_fingerprint_mismatch")]
    [InlineData("ordinal_reuse", "wound_history_duplicate_operation_key")]
    [InlineData("chain", "wound_history_chain_mismatch")]
    [InlineData("turn", "wound_history_turn_regression")]
    public void Append_RejectsInvalidAuthorityWithoutChangingHistory(string mutation, string expectedCode)
    {
        var before = Parse(History(Diagnosis()));
        var canonicalBefore = WoundHistoryState.SerializeCanonical(before);
        var intent = Intent(before.Transitions[1]) with
        {
            TransitionId = "transition_3", OperationKey = "operation_3"
        };
        switch (mutation)
        {
            case "ordinal_reuse": intent = intent with { OperationKey = "operation_2" }; break;
            case "chain": intent = intent with { BeforeFingerprint = Fingerprint('f') }; break;
            case "turn": intent = intent with { Turn = 0 }; break;
        }
        var output = mutation == "output" ? Fingerprint('e') :
            WoundHistoryState.ComputeOutputFingerprint(intent.OperationKey, intent.EventRef, "Accepted diagnosis.");
        var appended = before.AppendTransition(intent, Fingerprint('d'), output, "Accepted diagnosis.");
        Assert.False(appended.IsValid);
        Assert.Contains(appended.Issues, i => i.Code == expectedCode);
        Assert.Equal(canonicalBefore, WoundHistoryState.SerializeCanonical(before));
    }

    [Fact]
    public void Append_ResealsDetachedResultAndIntentCloneRetainsIt()
    {
        var before = Parse(History(Diagnosis()));
        var incoming = new WoundDiagnosisTransitionResult("path_exact", "failure", Array.Empty<string>(), Fingerprint('f'));
        var intent = Intent(before.Transitions[1]) with
        {
            TransitionId = "transition_3", OperationKey = "operation_3", TransitionResult = incoming
        };
        var cloned = Assert.IsType<WoundTransitionHistoryIntent>(WoundAcceptedTurnData.CloneTransitionIntent(intent));
        Assert.Same(incoming, cloned.TransitionResult);
        Assert.NotEqual(PlanInput(intent), PlanInput(intent with { TransitionResult = null }));
        var output = WoundHistoryState.ComputeOutputFingerprint(intent.OperationKey, intent.EventRef, "Accepted diagnosis.");
        var appended = before.AppendTransition(intent, Fingerprint('d'), output, "Accepted diagnosis.");
        Assert.True(appended.IsValid, string.Join(",", appended.Issues.Select(i => i.Code)));
        var accepted = Assert.IsType<WoundDiagnosisTransitionResult>(appended.State!.Transitions[2].TransitionResult);
        Assert.NotSame(incoming, accepted);
        Assert.Equal(FingerprintOf(Diagnosis()), accepted.ResultFingerprint);
        Assert.Equal(Fingerprint('f'), incoming.ResultFingerprint);
    }

    private static WoundTransitionHistoryIntent Intent(WoundHistoryTransition row)
    {
        var intent = new WoundTransitionHistoryIntent(row.TransitionId, row.WoundId, row.Kind, row.OperationKey, row.EventRef, row.Turn, row.BeforeFingerprint, row.AfterFingerprint, row.AttemptId, row.CycleKey, row.Terminal);
        var property = typeof(WoundTransitionHistoryIntent).GetProperty("TransitionResult");
        Assert.NotNull(property);
        property.SetValue(intent, Result(row));
        return intent;
    }

    private static string PlanInput(WoundTransitionHistoryIntent intent)
    {
        var fields = new List<string?>();
        var method = typeof(WoundAcceptedTurnFingerprints).GetMethod("AppendTransitionIntent", BindingFlags.Static | BindingFlags.NonPublic)!;
        method.Invoke(null, new object[] { fields, intent });
        return WoundAcceptedTurnFingerprintWriter.Compute(fields);
    }

    private static object Result(object owner)
    {
        var property = owner.GetType().GetProperty("TransitionResult");
        Assert.NotNull(property);
        return Assert.IsAssignableFrom<object>(property.GetValue(owner));
    }

    internal static JsonObject Diagnosis(params string[] facts) => Seal(new JsonObject
    {
        ["kind"] = "diagnose", ["diagnosisPathId"] = "path_exact",
        ["result"] = facts.Length == 0 ? "failure" : "success",
        ["revealedFacts"] = new JsonArray(facts.Select(f => (JsonNode)f).ToArray())
    });

    internal static JsonObject Alternative() => Seal(new JsonObject
    {
        ["kind"] = "author_alternative_treatment", ["authoringRequestRef"] = "authoring_public",
        ["addedRouteId"] = "route_exact", ["addedDiagnosisPathId"] = null,
        ["routeFingerprint"] = Fingerprint('a'), ["diagnosisPathFingerprint"] = null
    });

    private static JsonObject Seal(JsonObject input)
    {
        var result = input.DeepClone().AsObject();
        result["resultFingerprint"] = FingerprintOf(result);
        return result;
    }

    private static string FingerprintOf(JsonObject input)
    {
        var method = typeof(WoundHistoryState).GetMethod("ComputeTransitionResultFingerprint", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return Assert.IsType<string>(method.Invoke(null, new object[] { input }));
    }

    private static JsonObject History(JsonObject result, string kind = "diagnose")
    {
        var create = new WoundHistoryTransition("transition_1", "wound_exact", 1, 1, "create", 1, "event_1", "operation_1", WoundHistoryState.ComputeNonexistentBeforeFingerprint("wound_exact"), Fingerprint('a'), Fingerprint('b'), null, null, null, null, null, Fingerprint('c'), "Created wound.", false);
        var root = JsonNode.Parse(WoundHistoryState.SerializeCanonical(WoundHistoryState.CreateValidated(2, new[] { create }).State!))!.AsObject();
        var row = root["transitions"]![0]!.DeepClone().AsObject();
        row["transitionId"] = "transition_2"; row["ordinal"] = 2; row["woundTransitionOrdinal"] = 2;
        row["operationKey"] = "operation_2"; row["kind"] = kind; row["beforeFingerprint"] = Fingerprint('a');
        row["transitionResult"] = result.DeepClone();
        root["transitions"]!.AsArray().Add(row); root["nextOrdinal"] = 3;
        return root;
    }

    private static WoundHistoryState Parse(JsonObject root)
    {
        var parsed = WoundHistoryState.Parse(root.ToJsonString(), "history");
        Assert.True(parsed.IsValid, string.Join(",", parsed.Issues.Select(i => i.Code + "@" + i.FilePath)));
        return parsed.State!;
    }

    private static void AssertIssue(JsonObject root, string path, string code)
    {
        var parsed = WoundHistoryState.Parse(root.ToJsonString(), "history");
        Assert.False(parsed.IsValid);
        Assert.Contains(parsed.Issues, i => i.FilePath == path && i.Code == code);
    }

    private static string Fingerprint(char value) => "sha256:" + new string(value, 64);
}
