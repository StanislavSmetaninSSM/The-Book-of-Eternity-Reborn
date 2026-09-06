using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class WoundAcceptedTransitionCommandTests
{
    [Theory]
    [InlineData("diagnose")]
    [InlineData("diagnose_failure")]
    [InlineData("author_alternative_treatment")]
    [InlineData("author_alternative_treatment_hidden")]
    public void Codec_RoundTripsActualFactoryAuthorityAndRejectsChangedScene(string variant)
    {
        var (binding, request) = WoundAcceptedTransitionCommandTestData.Create(variant);
        var root = WoundResponseInputComposer.ComposeAcceptedTransitionCommandRoot(binding, request, "The examination is complete.");
        var parsed = WoundResponseInputComposer.ParseCommandRoot(JsonSerializer.SerializeToElement(root));
        Assert.True(parsed.Success, Describe(parsed.Issues));
        Assert.Single(parsed.AcceptedTransitionCommands);
        Assert.Empty(parsed.Commands);
        Assert.Empty(parsed.TreatmentCommands);
        var exact = WoundResponseInputComposer.RecomposeCommandRoot(
            binding, parsed, Array.Empty<WoundOpportunityDecisionReceipt>());
        Assert.True(exact.Success, Describe(exact.Issues));
        Assert.True(JsonNode.DeepEquals(root, exact.CommandRoot));
        root["commands"]![0]!["finalSceneText"] = "A changed scene.";
        var changed = WoundResponseInputComposer.ParseCommandRoot(JsonSerializer.SerializeToElement(root));
        Assert.True(changed.Success);
        var rejected = WoundResponseInputComposer.RecomposeCommandRoot(
            binding, changed, Array.Empty<WoundOpportunityDecisionReceipt>());
        Assert.False(rejected.Success);
        Assert.Contains(rejected.Issues, issue => issue.Code == "wound_command_recomposition_mismatch");
    }

    [Theory]
    [InlineData("diagnose")]
    [InlineData("author_alternative_treatment")]
    public void UnsupportedCapture_CatalogRejectsExplicitly(string variant)
    {
        var (binding, request) = WoundAcceptedTransitionCommandTestData.Create(variant);
        var root = Compose(binding, request, "scene");
        var element = JsonSerializer.SerializeToElement(root);
        var parsed = WoundResponseInputComposer.ParseCommandRoot(element);
        Assert.True(parsed.Success, Describe(parsed.Issues));
        var history = WoundHistoryState.Parse("{\"schemaVersion\":1,\"nextOrdinal\":1,\"transitions\":[]}", WoundHistoryState.HistoryPath);
        Assert.True(history.IsValid, Describe(history.Issues));
        var catalog = MortalWoundTreatmentPersistedRequestCatalog.Parse(element, null, history);
        Assert.False(catalog.IsValid);
        Assert.Contains(catalog.Issues, issue => issue.Code == "wound_command_transition_adapter_unavailable");
    }

    [Theory]
    [InlineData("diagnose")]
    [InlineData("author_alternative_treatment")]
    public void UnsupportedCapture_PendingRootRejectsExplicitly(string variant)
    {
        var (binding, request) = WoundAcceptedTransitionCommandTestData.Create(variant);
        var parsed = Parse(Compose(binding, request, "scene"));
        var exception = Assert.Throws<InvalidOperationException>(() => WoundRepairPacketBuilder.ComposePendingRoot(
            binding, Array.Empty<WoundRepairPacket>(), parsed));
        Assert.Contains("wound_command_transition_adapter_unavailable", exception.Message);
    }

    [Theory]
    [InlineData("diagnose")]
    [InlineData("diagnose_failure")]
    [InlineData("author_alternative_treatment")]
    [InlineData("author_alternative_treatment_hidden")]
    public void Codec_RejectsEveryChangedAuthorityFieldAndSeal(string variant)
    {
        var (binding, request) = WoundAcceptedTransitionCommandTestData.Create(variant);
        var root = Compose(binding, request, "scene");
        foreach (var field in root["commands"]![0]!["authority"]!.AsObject())
        {
            var changed = root.DeepClone().AsObject();
            changed["commands"]![0]!["authority"]![field.Key] = field.Value is null ? "new_value" :
                field.Key.EndsWith("Fingerprint", StringComparison.Ordinal) ? Seal('0') : "changed_identifier";
            AssertRejected(binding, changed);
        }
        root["commands"]![0]!["result"]!["resultFingerprint"] = Seal('0');
        AssertRejected(binding, root);
    }

    [Theory]
    [InlineData("diagnose")]
    [InlineData("author_alternative_treatment_hidden")]
    public void Parser_RequiresEveryClosedFieldAndRejectsUnknownWrongTypeAndNull(string variant)
    {
        var (binding, request) = WoundAcceptedTransitionCommandTestData.Create(variant);
        var root = Compose(binding, request, "scene");
        foreach (var prefix in new[] { "", "commands/0", "commands/0/authority", "commands/0/result" })
        {
            var obj = At(root, prefix).AsObject();
            foreach (var field in obj)
            {
                var changed = root.DeepClone().AsObject();
                At(changed, prefix).AsObject().Remove(field.Key);
                Assert.False(Parse(changed).Success, "Missing " + prefix + "/" + field.Key);
                changed = root.DeepClone().AsObject();
                At(changed, prefix)[field.Key] = true;
                Assert.False(Parse(changed).Success, "Wrong type " + prefix + "/" + field.Key);
                if (field.Value is null || field.Key == "finalSceneText") continue;
                changed = root.DeepClone().AsObject();
                At(changed, prefix)[field.Key] = null;
                Assert.False(Parse(changed).Success, "Null " + prefix + "/" + field.Key);
            }
            var unknown = root.DeepClone().AsObject();
            At(unknown, prefix)["gmOverride"] = true;
            Assert.Contains(Parse(unknown).Issues, issue => issue.Code == "wound_command_unknown_field");
        }
    }

    [Theory]
    [InlineData("SHA256:")]
    [InlineData("sha256:ABC")]
    [InlineData("sha256:fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffF")]
    [InlineData("sha256:fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff")]
    public void Parser_RequiresExactLowercaseHashes(string value)
    {
        var (binding, request) = WoundAcceptedTransitionCommandTestData.Create("diagnose");
        var root = Compose(binding, request, "scene");
        root["commands"]![0]!["authority"]!["pathFingerprint"] = value;
        Assert.False(Parse(root).Success);
    }

    [Theory]
    [InlineData("space ref")]
    [InlineData("slash/ref")]
    [InlineData("back\\ref")]
    [InlineData("colon:ref")]
    public void Parser_RejectsUnsafeOpaqueReferences(string value)
    {
        foreach (var variant in new[] { "diagnose", "author_alternative_treatment" })
        {
            var (binding, request) = WoundAcceptedTransitionCommandTestData.Create(variant);
            var root = Compose(binding, request, "scene");
            root["commands"]![0]!["commandRef"] = value;
            root["commands"]![0]!["authority"]![variant == "diagnose" ? "commandRef" : "authoringRequestRef"] = value;
            Assert.False(Parse(root).Success);
        }
    }

    [Theory]
    [InlineData("session")]
    [InlineData("request")]
    [InlineData("snapshot")]
    [InlineData("realm")]
    [InlineData("turn")]
    [InlineData("zero_turn")]
    [InlineData("event")]
    [InlineData("event_shape")]
    [InlineData("digest")]
    [InlineData("no_events")]
    [InlineData("duplicate_events")]
    public void Recomposition_RejectsChangedBinding(string mutation)
    {
        foreach (var variant in new[] { "diagnose", "author_alternative_treatment" })
        {
            var (binding, request) = WoundAcceptedTransitionCommandTestData.Create(variant);
            var root = Compose(binding, request, "scene");
            var changed = mutation switch
            {
                "session" => binding with { SessionId = "other" },
                "request" => binding with { RequestId = "other" },
                "snapshot" => binding with { SnapshotToken = "other" },
                "realm" => binding with { Realm = "chaos_sea" },
                "turn" => binding with { Turn = 44 },
                "zero_turn" => binding with { Turn = 0 },
                "digest" => binding with { AcceptedEventsFingerprint = Seal('0') },
                "no_events" => BindEvents(binding, Array.Empty<WoundAcceptedEventAuthority>()),
                "duplicate_events" => BindEvents(binding, new[] { binding.AcceptedEvents[0], binding.AcceptedEvents[0] }),
                "event_shape" => BindEvents(binding, new[] { binding.AcceptedEvents[0] with { Kind = "" } }),
                _ => BindEvents(binding, new[] { binding.AcceptedEvents[0] with { EventRef = "turn_43:foreign" } })
            };
            AssertRejected(changed, root);
        }
    }

    [Theory]
    [InlineData("kind")]
    [InlineData("evidence")]
    [InlineData("operation")]
    [InlineData("event")]
    [InlineData("turn")]
    [InlineData("transition")]
    [InlineData("before")]
    [InlineData("after")]
    [InlineData("seal")]
    public void Composer_RejectsChangedFullRequest(string mutation)
    {
        foreach (var variant in new[] { "diagnose", "author_alternative_treatment" })
        {
            var (binding, request) = WoundAcceptedTransitionCommandTestData.Create(variant);
            var changed = mutation switch
            {
                "kind" => request with { Kind = "heal" },
                "evidence" => request with { Evidence = WoundAcceptedTransitionCommandTestData.Create(
                    variant == "diagnose" ? "author_alternative_treatment" : "diagnose").Request.Evidence },
                "operation" => request with { OperationKey = "changed" },
                "event" => request with { EventRef = "turn_43:foreign" },
                "turn" => request with { Turn = 44 },
                "transition" => request with { TransitionId = "changed" },
                "before" => request with { Before = request.Before! with { Display = request.Before!.Display with { Name = "changed" } } },
                "after" => request with { ProposedAfter = request.ProposedAfter! with { Display = request.ProposedAfter!.Display with { Name = "changed" } } },
                _ => request with { Evidence = request.Evidence with { ExpectedBeforeFingerprint = Seal('0') } }
            };
            var exception = Record.Exception(() => WoundResponseInputComposer.ComposeAcceptedTransitionCommandRoot(binding, changed, "scene"));
            Assert.True(exception is ArgumentException or InvalidOperationException, exception?.ToString() ?? "accepted");
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  Exact scene \\n \r\nРусский текст  ")]
    public void Codec_PreservesRawNullableSceneAndDetachedRoots(string? scene)
    {
        var (binding, request) = WoundAcceptedTransitionCommandTestData.Create("author_alternative_treatment_hidden", "ёж");
        var root = WoundResponseInputComposer.ComposeAcceptedTransitionCommandRoot(binding, request, scene!);
        WoundResponseCommandParsingResult parsed;
        using (var document = JsonDocument.Parse(root.ToJsonString()))
            parsed = WoundResponseInputComposer.ParseCommandRoot(document.RootElement);
        var expected = root.DeepClone();
        root["commands"]![0]!["authority"]!["woundId"] = "changed";
        parsed.CommandRoot!["sessionId"] = "changed";
        Assert.Equal(scene, Assert.Single(parsed.AcceptedTransitionCommands).FinalSceneText);
        Assert.True(JsonNode.DeepEquals(expected, Recompose(binding, parsed).CommandRoot));
    }

    [Fact]
    public void Codec_RetainsInterleavedFamilyOrderAndAcceptsMultipleLegitimateEvents()
    {
        var variants = new[] { "diagnose", "author_alternative_treatment", "diagnose_failure" };
        var (binding, _) = WoundAcceptedTransitionCommandTestData.Create("diagnose");
        binding = BindEvents(binding, new[] { binding.AcceptedEvents[0], binding.AcceptedEvents[0] with { EventRef = "turn_43:second", AuthorityId = "second" } });
        JsonObject? root = null;
        for (var i = 0; i < variants.Length; i++)
        {
            var request = WoundAcceptedTransitionCommandTestData.Create(variants[i], "row" + i).Request;
            var composed = Compose(binding, request, "scene");
            if (root is null) root = composed;
            else root["commands"]!.AsArray().Add(composed["commands"]![0]!.DeepClone());
        }
        var parsed = Parse(root!);
        Assert.True(parsed.Success, Describe(parsed.Issues));
        Assert.Equal(new[] { "diagnose", "author_alternative_treatment", "diagnose" }, parsed.AcceptedTransitionCommands.Select(value => value.TransitionKind));
        Assert.True(JsonNode.DeepEquals(root, Recompose(binding, parsed).CommandRoot));
    }

    [Fact]
    public void Parser_BoundsTotalBatchAndRejectsConfusableCoordinatesAndConflictingScene()
    {
        var (binding, _) = WoundAcceptedTransitionCommandTestData.Create("diagnose");
        var root = Compose(binding, WoundAcceptedTransitionCommandTestData.Create("diagnose", "0").Request, "scene");
        for (var i = 1; i < 32; i++)
            root["commands"]!.AsArray().Add(Compose(binding,
                WoundAcceptedTransitionCommandTestData.Create("diagnose", i.ToString()).Request, "scene")["commands"]![0]!.DeepClone());
        Assert.True(Parse(root).Success);
        Assert.True(Recompose(binding, Parse(root)).Success);
        var overflow = root.DeepClone().AsObject();
        overflow["commands"]!.AsArray().Add(root["commands"]![0]!.DeepClone());
        Assert.Contains(Parse(overflow).Issues, issue => issue.Code == "wound_command_limit_exceeded");
        foreach (var field in new[] { "commandRef", "operationKey", "attemptId" })
        {
            var duplicate = root.DeepClone().AsObject();
            var value = duplicate["commands"]![0]!["authority"]![field]!.GetValue<string>().ToUpperInvariant();
            duplicate["commands"]![1]!["authority"]![field] = value;
            if (field != "attemptId") duplicate["commands"]![1]![field] = value;
            Assert.False(Parse(duplicate).Success, field);
        }
        root["commands"]![1]!["finalSceneText"] = null;
        Assert.Contains(Parse(root).Issues, issue => issue.Code == "wound_command_scene_binding_mismatch");
    }

    [Theory]
    [InlineData("commands/0/result/route/displayName", "\"different text\"")]
    [InlineData("commands/0/result/route/resolution/difficulty", "23")]
    [InlineData("commands/0/result/diagnosisPath/displayName", "\"different path\"")]
    [InlineData("commands/0/result/diagnosisPath/reveals", "[\"route:alternative_one\",\"complication:unrepresented\"]")]
    [InlineData("commands/0/result/diagnosisPath/requiresKnownFacts", "[\"route:unrepresented\"]")]
    public void Codec_RehashesCompleteMembersWithoutGuessingFullWoundReachability(string path, string json)
    {
        var (binding, request) = WoundAcceptedTransitionCommandTestData.Create("author_alternative_treatment_hidden");
        var root = Compose(binding, request, "scene");
        SetAt(root, path, JsonNode.Parse(json));
        var parsed = Parse(root);
        Assert.True(parsed.Success, Describe(parsed.Issues));
        Assert.False(Recompose(binding, parsed).Success);
    }

    [Theory]
    [InlineData("route:")]
    [InlineData("route: bad")]
    [InlineData("unknown:route")]
    [InlineData("free form")]
    public void Parser_RejectsInvalidDiagnosisFactGrammar(string fact)
    {
        var (binding, request) = WoundAcceptedTransitionCommandTestData.Create("diagnose");
        var root = Compose(binding, request, "scene");
        root["commands"]![0]!["result"]!["revealedFacts"] = new JsonArray(fact);
        Assert.False(Parse(root).Success);
    }

    [Fact]
    public void Parser_BoundsFactsRejectsConfusablesAndPreservesOrderBeforeVerification()
    {
        var (binding, request) = WoundAcceptedTransitionCommandTestData.Create("diagnose");
        var root = Compose(binding, request, "scene");
        var facts = Enumerable.Range(0, 16).Reverse().Select(i => "route:fact_" + i).ToArray();
        root["commands"]![0]!["result"]!["revealedFacts"] = new JsonArray(facts.Select(value => (JsonNode)value).ToArray());
        var parsed = Parse(root);
        Assert.True(parsed.Success, Describe(parsed.Issues));
        Assert.Equal(facts, Assert.IsType<WoundDiagnosisCommandDraft>(Assert.Single(parsed.AcceptedTransitionCommands)).Result.RevealedFacts);
        Assert.False(Recompose(binding, parsed).Success);
        root["commands"]![0]!["result"]!["revealedFacts"]!.AsArray().Add("route:overflow");
        Assert.False(Parse(root).Success);
        root["commands"]![0]!["result"]!["revealedFacts"] = new JsonArray("route:fact", "route:FACT");
        Assert.False(Parse(root).Success);
    }

    [Theory]
    [InlineData("attemptId", "attempt_one")]
    [InlineData("displayName", "New setting-specific treatment one")]
    [InlineData("kind", "reduce_severity")]
    public void Parser_RejectsRecursiveDuplicatePropertiesBeforeNodeConversion(string field, string value)
    {
        var variant = field == "attemptId" ? "diagnose" : "author_alternative_treatment";
        var (binding, request) = WoundAcceptedTransitionCommandTestData.Create(variant);
        var json = Compose(binding, request, "scene").ToJsonString();
        var token = JsonSerializer.Serialize(field) + ":" + JsonSerializer.Serialize(value);
        Assert.Contains(token, json);
        json = json.Replace(token, token + "," + token, StringComparison.Ordinal);
        using var document = JsonDocument.Parse(json);
        Assert.Contains(WoundResponseInputComposer.ParseCommandRoot(document.RootElement).Issues,
            issue => issue.Code == "wound_command_duplicate_field");
    }

    private static WoundAcceptedTurnBinding BindEvents(WoundAcceptedTurnBinding binding, IReadOnlyList<WoundAcceptedEventAuthority> events) =>
        binding with { AcceptedEvents = events, AcceptedEventsFingerprint = WoundAcceptedEventSetFingerprint.Compute(events) };

    [Theory]
    [InlineData("diagnose")]
    [InlineData("author_alternative_treatment_hidden")]
    public void Recomposition_RejectsContradictoryTypedResultAndUnknownDraftSubtype(string variant)
    {
        var (binding, request) = WoundAcceptedTransitionCommandTestData.Create(variant);
        var parsed = Parse(Compose(binding, request, "scene"));
        var draft = Assert.Single(parsed.AcceptedTransitionCommands);
        var changed = draft switch
        {
            WoundDiagnosisCommandDraft d => (WoundAcceptedTransitionCommandDraft)(d with
            {
                Result = new WoundDiagnosisTransitionResult("wrong_path", d.Result.Result,
                    d.Result.RevealedFacts, d.Result.ResultFingerprint)
            }),
            WoundAlternativeTreatmentCommandDraft a => a with
            {
                Result = new WoundAlternativeTreatmentTransitionResult("wrong_request", a.Result.AddedRouteId,
                    a.Result.AddedDiagnosisPathId, a.Result.RouteFingerprint, a.Result.DiagnosisPathFingerprint,
                    a.Result.ResultFingerprint)
            },
            _ => throw new InvalidOperationException()
        };
        foreach (var invalid in new[] { changed, new UnknownDraft(draft.CommandRef, draft.OperationKey, draft.FinalSceneText) })
        {
            var forged = new WoundResponseCommandParsingResult(parsed.CommandRoot, parsed.Commands,
                parsed.TreatmentCommands, new[] { invalid }, Array.Empty<ValidationIssue>());
            Assert.False(Recompose(binding, forged).Success);
        }
    }

    private sealed record UnknownDraft(string Ref, string Operation, string? Scene) :
        WoundAcceptedTransitionCommandDraft(Ref, Operation, Scene)
    {
        internal override string TransitionKind => "diagnose";
    }

    [Theory]
    [InlineData("procedure")]
    [InlineData("course")]
    [InlineData("guaranteed")]
    public void Codec_FullRouteModesUseFactoryMemberFingerprintAndRoundTrip(string mode)
    {
        var (binding, request) = WoundAcceptedTransitionCommandTestData.Create("author_alternative_treatment");
        var after = JsonNode.Parse(WoundMaterializationContract.SerializeCanonical(request.ProposedAfter!))!.AsObject();
        var route = after["treatment"]!["routes"]![1]!.AsObject();
        route["mode"] = mode;
        if (mode != "procedure")
        {
            route["resourcePolicy"]!["consumeOn"] = new JsonArray("success");
            route["resourcePolicy"]!["mutations"] = new JsonArray();
        }
        if (mode == "guaranteed")
        {
            route["requirements"] = new JsonArray(JsonNode.Parse("{\"kind\":\"source_capability\",\"capabilityRef\":\"healing_source\",\"actorRole\":\"provider\"}"));
            route["resolution"] = JsonNode.Parse("{\"capabilityRef\":\"healing_source\",\"actorRole\":\"provider\"}");
            route["outcomes"] = JsonNode.Parse("[{\"category\":\"success\",\"result\":[{\"kind\":\"stabilize\"}]}]");
        }
        else if (mode == "course")
        {
            route["requirements"] = new JsonArray();
            route["resolution"] = JsonNode.Parse("{\"clockKind\":\"world_time.currentTimeInMinutes\",\"maximumGapMinutes\":600}");
            route["outcomes"] = JsonNode.Parse("[{\"ordinal\":1,\"afterMinutes\":0,\"requirements\":[],\"category\":\"success\",\"completion\":\"active\",\"result\":[]},{\"ordinal\":2,\"afterMinutes\":60,\"requirements\":[],\"category\":\"success\",\"completion\":\"completed\",\"result\":[{\"kind\":\"heal\",\"legacies\":[]}]}]");
            route["interruption"] = JsonNode.Parse("{\"category\":\"failed_attempt\",\"result\":[{\"kind\":\"no_improvement\"}]}");
        }
        request = Reauthor(request, after);
        var evidence = Assert.IsType<WoundAlternativeTreatmentEvidence>(request.Evidence);
        Assert.Equal(evidence.RouteFingerprint, MortalWoundTreatmentMemberFingerprint.ComputeRoute(route));
        var root = Compose(binding, request, "scene");
        Assert.True(JsonNode.DeepEquals(root, Recompose(binding, Parse(root)).CommandRoot));
        var exported = root["commands"]![0]!["result"]!["route"]!;
        Assert.True(JsonNode.DeepEquals(route, exported));
        Assert.DoesNotContain("SourcePath", exported.ToJsonString());
        exported["mode"] = "invented";
        Assert.False(Parse(root).Success);
    }

    [Theory]
    [InlineData("route_text")]
    [InlineData("route_array")]
    [InlineData("route_null")]
    [InlineData("path_text")]
    [InlineData("path_array")]
    public void MemberFingerprints_MatchFactoryAndRetainDisplayArraysAndNulls(string mutation)
    {
        var (binding, originalRequest) = WoundAcceptedTransitionCommandTestData.Create("author_alternative_treatment_hidden");
        var beforeEvidence = Assert.IsType<WoundAlternativeTreatmentEvidence>(originalRequest.Evidence);
        var after = JsonNode.Parse(WoundMaterializationContract.SerializeCanonical(originalRequest.ProposedAfter!))!.AsObject();
        var route = after["treatment"]!["routes"]![1]!.AsObject();
        var path = after["treatment"]!["diagnosisPaths"]![0]!.AsObject();
        switch (mutation)
        {
            case "route_text": route["displayName"] = "Different display"; break;
            case "route_array": route["requirements"]!.AsArray().Add(JsonNode.Parse("{\"kind\":\"provider\",\"providerRef\":\"medic\"}")); break;
            case "route_null": route["outcomes"]![0]!["maximumMargin"] = 100; break;
            case "path_text": path["displayName"] = "Different diagnosis display"; break;
            case "path_array": path["reveals"]!.AsArray().Add("route:clean_and_suture"); break;
        }
        if (mutation == "route_null")
        {
            // Bound-endpoint mutation is intentionally only a member-hash probe:
            // an incomplete procedure partition cannot become factory legality.
            Assert.NotEqual(beforeEvidence.RouteFingerprint, MortalWoundTreatmentMemberFingerprint.ComputeRoute(route));
            return;
        }
        var request = Reauthor(originalRequest, after);
        var evidence = Assert.IsType<WoundAlternativeTreatmentEvidence>(request.Evidence);
        Assert.Equal(evidence.RouteFingerprint, MortalWoundTreatmentMemberFingerprint.ComputeRoute(route));
        Assert.Equal(evidence.DiagnosisPathFingerprint, MortalWoundTreatmentMemberFingerprint.ComputeDiagnosisPath(path));
        Assert.NotEqual(mutation.StartsWith("route", StringComparison.Ordinal) ? beforeEvidence.RouteFingerprint : beforeEvidence.DiagnosisPathFingerprint,
            mutation.StartsWith("route", StringComparison.Ordinal) ? evidence.RouteFingerprint : evidence.DiagnosisPathFingerprint);
        var root = Compose(binding, request, "scene");
        Assert.True(Recompose(binding, Parse(root)).Success);
        var draft = Assert.IsType<WoundAlternativeTreatmentCommandDraft>(Assert.Single(Parse(root).AcceptedTransitionCommands));
        var changedSource = draft with { Route = draft.Route with { SourcePath = "different_source" }, DiagnosisPath = draft.DiagnosisPath! with { SourcePath = "different_source" } };
        var parsed = new WoundResponseCommandParsingResult(root, Array.Empty<WoundResponseCommandDraft>(),
            Array.Empty<MortalWoundTreatmentCommandDraft>(), new[] { changedSource }, Array.Empty<ValidationIssue>());
        Assert.True(Recompose(binding, parsed).Success);
    }

    [Theory]
    [InlineData("mode", "\"invented\"")]
    [InlineData("visibility", "\"gm_only\"")]
    [InlineData("outcomes/3/result/0/complicationDraft", "{\"kind\":\"infection\"}")]
    [InlineData("outcomes/0/result/0", "{\"kind\":\"heal\",\"legacies\":[{\"kind\":\"effect\"}]}")]
    [InlineData("requirements", "[{\"kind\":\"provider\",\"providerRef\":null}]")]
    public void Parser_UsesClosedNestedRouteConstructors(string memberPath, string json)
    {
        var (binding, request) = WoundAcceptedTransitionCommandTestData.Create("author_alternative_treatment");
        var root = Compose(binding, request, "scene");
        SetAt(root, "commands/0/result/route/" + memberPath, JsonNode.Parse(json));
        Assert.False(Parse(root).Success);
    }

    [Theory]
    [InlineData("diagnose")]
    [InlineData("author_alternative_treatment")]
    public void Composer_RunsLocalReducerBeforeProjectingInvalidDelta(string variant)
    {
        var (binding, request) = WoundAcceptedTransitionCommandTestData.Create(variant);
        var after = request.ProposedAfter! with { Display = request.ProposedAfter!.Display with { Name = "Changed unrelated wound" } };
        request = variant == "diagnose" ? MortalWoundTreatmentPlanner.CreateDiagnosisTransition(
            request.TransitionId, request.Evidence.AuthorityRef, request.OperationKey, "attempt_one", request.EventRef,
            request.Turn, request.Before!, after, "diagnosis_one", "success", Seal('6'), Seal('7')) :
            MortalWoundTreatmentPlanner.CreateAlternativeTreatmentTransition(request.TransitionId,
                request.Evidence.AuthorityRef, Seal('1'), request.OperationKey, request.EventRef, request.Turn,
                request.Before!, after, "alternative_one", null, Seal('6'), Seal('7'));
        Assert.Throws<InvalidOperationException>(() =>
            WoundResponseInputComposer.ComposeAcceptedTransitionCommandRoot(binding, request, "scene"));
    }

    private static WoundTransitionRequest Reauthor(WoundTransitionRequest original, JsonObject after)
    {
        var parsed = WoundMaterializationContract.Parse(after.ToJsonString(), "real_after");
        Assert.True(parsed.IsValid, Describe(parsed.Issues));
        var evidence = Assert.IsType<WoundAlternativeTreatmentEvidence>(original.Evidence);
        return MortalWoundTreatmentPlanner.CreateAlternativeTreatmentTransition(original.TransitionId,
            evidence.AuthorityRef, evidence.RequestAuthorityFingerprint, original.OperationKey,
            original.EventRef, original.Turn, original.Before!, parsed.Wound!, evidence.AddedRouteId,
            evidence.AddedDiagnosisPathId, evidence.EvidenceAuthorityFingerprint, evidence.RequirementAuthorityFingerprint);
    }

    [Fact]
    public void Composer_HasExactlyOneFrozenThreeArgumentApiAndKeepsTreatmentSeparate()
    {
        var methods = typeof(WoundResponseInputComposer).GetMethods(BindingFlags.Static | BindingFlags.NonPublic);
        var method = Assert.Single(methods, value => value.Name == "ComposeAcceptedTransitionCommandRoot");
        Assert.Equal(new[] { typeof(WoundAcceptedTurnBinding), typeof(WoundTransitionRequest), typeof(string) },
            method.GetParameters().Select(parameter => parameter.ParameterType));
        Assert.Contains(methods, value => value.Name == "ComposeMortalWoundTreatmentCommandRoot");
    }

    [Fact]
    public void ParsingResult_CopiesTheRetainedDraftCollection()
    {
        var (binding, request) = WoundAcceptedTransitionCommandTestData.Create("diagnose");
        var original = Parse(Compose(binding, request, "scene"));
        var drafts = original.AcceptedTransitionCommands.ToList();
        var copy = new WoundResponseCommandParsingResult(original.CommandRoot, original.Commands,
            original.TreatmentCommands, drafts, original.Issues);
        drafts.Clear();
        Assert.Single(copy.AcceptedTransitionCommands);
        Assert.True(Recompose(binding, copy).Success);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Codec_RoundTripsCompleteNestedComplicationAndHealLegacyDefinitions(bool legacy)
    {
        var (binding, original) = WoundAcceptedTransitionCommandTestData.Create("author_alternative_treatment");
        var after = JsonNode.Parse(WoundMaterializationContract.SerializeCanonical(original.ProposedAfter!))!.AsObject();
        var route = after["treatment"]!["routes"]![1]!.AsObject();
        var definition = WoundContractTestData.CreateOwnedEffectDefinition("unused", "mortal_world", "tremor", "action_control");
        definition["links"] = new JsonArray();
        string nestedPath;
        if (legacy)
        {
            route["outcomes"]![0]!["result"] = new JsonArray(new JsonObject
            {
                ["kind"] = "heal",
                ["legacies"] = new JsonArray(new JsonObject
                {
                    ["localLegacyRef"] = "scar", ["kind"] = "cosmetic", ["readableSummary"] = "A scar remains."
                }, new JsonObject
                {
                    ["localLegacyRef"] = "tremor", ["kind"] = "mechanical_effect", ["readableSummary"] = "A tremor remains.",
                    ["effectDraft"] = new JsonObject
                    {
                        ["schemaVersion"] = 1,
                        ["definitions"] = new JsonArray(new JsonObject { ["definitionRef"] = "tremor_ref", ["definition"] = definition }),
                        ["applications"] = JsonNode.Parse("[{\"applicationRef\":\"tremor_app\",\"definitionRef\":\"tremor_ref\",\"parameters\":{}}]")
                    }
                })
            });
            nestedPath = "outcomes/0/result/0/legacies/1/effectDraft/definitions/0/definition";
        }
        else
        {
            route["outcomes"]![3]!["result"]![0]!["complicationDraft"]!["consequenceDefinitions"] = new JsonArray(new JsonObject
            {
                ["definitionRef"] = "tremor_ref", ["definition"] = definition,
                ["root"] = JsonNode.Parse("{\"ownership\":{\"kind\":\"complication\",\"complicationRef\":\"irritation\"},\"slots\":[{\"profileKey\":\"action_control\",\"readableSummary\":\"Pain limits movement.\"}]}")
            });
            nestedPath = "outcomes/3/result/0/complicationDraft/consequenceDefinitions/0/definition";
        }
        var request = Reauthor(original, after);
        var root = Compose(binding, request, "scene");
        Assert.True(JsonNode.DeepEquals(root, Recompose(binding, Parse(root)).CommandRoot));
        var changed = root.DeepClone().AsObject();
        At(changed, "commands/0/result/route/" + nestedPath)["description"] = "Changed nested display";
        AssertRejected(binding, changed);
    }
    private static string Seal(char digit) => "sha256:" + new string(digit, 64);
    private static WoundResponseCommandParsingResult Parse(JsonObject root) => WoundResponseInputComposer.ParseCommandRoot(JsonSerializer.SerializeToElement(root));
    private static WoundResponseInputCompositionResult Recompose(WoundAcceptedTurnBinding binding, WoundResponseCommandParsingResult parsed) =>
        WoundResponseInputComposer.RecomposeCommandRoot(binding, parsed, Array.Empty<WoundOpportunityDecisionReceipt>());
    private static void AssertRejected(WoundAcceptedTurnBinding binding, JsonObject root)
    {
        var parsed = Parse(root);
        if (!parsed.Success) return;
        var result = Recompose(binding, parsed);
        Assert.False(result.Success);
        Assert.Contains(result.Issues, issue => issue.Code == "wound_command_recomposition_mismatch");
    }
    private static JsonNode At(JsonNode root, string path) => path.Length == 0 ? root :
        path.Split('/').Aggregate(root, (node, key) => node is JsonArray ? node[int.Parse(key)]! : node[key]!);
    private static void SetAt(JsonNode root, string path, JsonNode? value)
    {
        var slash = path.LastIndexOf('/');
        var parent = At(root, path[..slash]);
        if (parent is JsonArray array) array[int.Parse(path[(slash + 1)..])] = value;
        else parent[path[(slash + 1)..]] = value;
    }

    private static JsonObject Compose(WoundAcceptedTurnBinding binding, WoundTransitionRequest request,
        string scene) => Assert.IsType<JsonObject>(Assert.Single(typeof(WoundResponseInputComposer)
        .GetMethods(BindingFlags.Static | BindingFlags.NonPublic), method =>
            method.Name == "ComposeAcceptedTransitionCommandRoot" && method.GetParameters().Length == 3)
        .Invoke(null, new object[] { binding, request, scene }));

    private static string Describe(IReadOnlyList<ValidationIssue> issues) =>
        string.Join("; ", issues.Select(issue => issue.Code + " " + issue.FilePath));
}
