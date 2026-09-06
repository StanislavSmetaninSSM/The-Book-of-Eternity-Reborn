using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class MortalWoundTreatmentMemberShapeTests
{
    [Theory]
    [InlineData("ParseRouteShape")]
    [InlineData("ParseDiagnosisPathShape")]
    public void StandaloneMemberParser_IsAvailable(string name)
    {
        var method = typeof(MortalWoundTreatmentContract).GetMethod(
            name, BindingFlags.Static | BindingFlags.NonPublic,
            new[] { typeof(JsonElement), typeof(string) });
        Assert.NotNull(method);
    }

    [Theory]
    [InlineData("procedure")]
    [InlineData("course")]
    [InlineData("guaranteed")]
    public void Route_CompleteDetachedCanonicalRoundTrip(string mode)
    {
        var route = Route(mode);
        var original = route.ToJsonString();
        MortalWoundTreatmentRouteDefinition parsed;
        using (var document = JsonDocument.Parse(original))
            parsed = ValidRoute(document.RootElement);
        route["displayName"] = "Changed after parsing";

        Assert.Equal("command.result.route", parsed.SourcePath);
        Assert.Equal(mode, parsed.Mode);
        Assert.IsType(mode switch
        {
            "procedure" => typeof(MortalWoundProcedureRouteDefinition),
            "course" => typeof(MortalWoundCourseRouteDefinition),
            _ => typeof(MortalWoundGuaranteedRouteDefinition)
        }, parsed);
        var canonical = Write(parsed);
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(original), JsonNode.Parse(canonical)));
        Assert.DoesNotContain("SourcePath", canonical);
        Assert.Equal(canonical, Write(ValidRoute(Element(canonical))));
    }

    [Theory]
    [InlineData("no_improvement")]
    [InlineData("stabilize")]
    [InlineData("add_recovery")]
    [InlineData("reduce_severity")]
    [InlineData("remove_complication")]
    [InlineData("add_complication")]
    [InlineData("apply_deterioration")]
    [InlineData("heal")]
    public void Route_AllRegisteredOperationsAreComplete(string kind)
    {
        var route = Route();
        var operation = Operation(kind);
        var index = kind is "no_improvement" or "add_complication" or "apply_deterioration" ? 3 : 0;
        route["outcomes"]![index]!["result"] = new JsonArray(operation);
        var canonical = Write(ValidRoute(Element(route)));
        Assert.True(JsonNode.DeepEquals(route, JsonNode.Parse(canonical)));
    }

    [Theory]
    [InlineData("routeId", "\" bad \"")]
    [InlineData("visibility", "\"gm_only\"")]
    [InlineData("mode", "\"magical_fragment\"")]
    [InlineData("requirements/0/quantity", "1.5")]
    [InlineData("requirements/0/quantity", "2147483648")]
    [InlineData("requirements/0/quantity", "0")]
    [InlineData("requirements/0/extra", "true")]
    [InlineData("requirements/0/kind", "\"invented\"")]
    [InlineData("resourcePolicy/reserveBeforeResolution", "false")]
    [InlineData("resourcePolicy/refundOn", "[\"rolled_back\",\"validation_failed\",\"cancelled\"]")]
    [InlineData("resourcePolicy/mutations/0/requirementIndex", "1")]
    [InlineData("resourcePolicy/mutations/0/requirementIndex", "16")]
    [InlineData("resourcePolicy/mutations/0/milestoneOrdinal", "1")]
    [InlineData("resourcePolicy/mutations/0/scope", "\"course_milestone\"")]
    [InlineData("resolution/modifierSource/requirementIndex", "0")]
    [InlineData("resolution/difficulty", "1.0")]
    [InlineData("resolution/rollSource", "\"gm_roll\"")]
    [InlineData("outcomes/1/maximumMargin", "5")]
    [InlineData("outcomes/1/maximumMargin", "3")]
    [InlineData("outcomes/1/category", "\"invented\"")]
    [InlineData("outcomes/2/category", "\"success\"")]
    [InlineData("outcomes/0/maximumMargin", "100")]
    [InlineData("outcomes/3/minimumMargin", "-100")]
    [InlineData("outcomes/0/result", "[]")]
    [InlineData("outcomes/0/result", "[{\"kind\":\"instant_cure\"}]")]
    [InlineData("outcomes/0/result", "[{\"kind\":\"no_improvement\"},{\"kind\":\"stabilize\"}]")]
    [InlineData("outcomes/0/result", "[{\"kind\":\"heal\",\"legacies\":[]},{\"kind\":\"stabilize\"}]")]
    [InlineData("outcomes/0/result", "[{\"kind\":\"heal\",\"legacies\":[]},{\"kind\":\"heal\",\"legacies\":[]}]")]
    [InlineData("outcomes/0/result", "[{\"kind\":\"reduce_severity\",\"steps\":2},{\"kind\":\"reduce_severity\",\"steps\":1}]")]
    [InlineData("outcomes/3/result", "[{\"kind\":\"apply_deterioration\",\"policyRef\":\" bad \"}]")]
    [InlineData("interruption", "{}")]
    public void Route_RejectsInvalidLocalMembers(string path, string json)
    {
        var route = Route();
        Set(route, path, JsonNode.Parse(json));
        InvalidRoute(route);
    }

    [Theory]
    [InlineData("course", "resolution/clockKind", "\"turns\"")]
    [InlineData("course", "resolution/maximumGapMinutes", "-1")]
    [InlineData("course", "resolution/maximumGapMinutes", "9223372036854775808")]
    [InlineData("course", "outcomes/0/ordinal", "2")]
    [InlineData("course", "outcomes/0/afterMinutes", "1")]
    [InlineData("course", "outcomes/1/afterMinutes", "0")]
    [InlineData("course", "outcomes/0/completion", "\"completed\"")]
    [InlineData("course", "outcomes/1/completion", "\"active\"")]
    [InlineData("course", "outcomes/1/requirements/0/quantity", "1.5")]
    [InlineData("course", "resourcePolicy/mutations/0/milestoneOrdinal", "3")]
    [InlineData("course", "resourcePolicy/mutations/0/requirementIndex", "1")]
    [InlineData("course", "interruption", "null")]
    [InlineData("course", "interruption/result", "[{\"kind\":\"stabilize\"}]")]
    [InlineData("guaranteed", "requirements", "[]")]
    [InlineData("guaranteed", "resolution/capabilityRef", "\"different\"")]
    [InlineData("guaranteed", "resolution/actorRole", "\"target\"")]
    [InlineData("guaranteed", "resolution/actorRole", "\"world\"")]
    [InlineData("guaranteed", "outcomes/0/result", "[{\"kind\":\"no_improvement\"}]")]
    [InlineData("guaranteed", "interruption", "{}")]
    public void Route_ModeRulesRemainLocal(string mode, string path, string json)
    {
        var route = Route(mode);
        Set(route, path, JsonNode.Parse(json));
        InvalidRoute(route);
    }

    [Theory]
    [InlineData("requirements", 17)]
    [InlineData("resourcePolicy/mutations", 65)]
    [InlineData("outcomes", 17)]
    [InlineData("outcomes/0/result", 9)]
    public void Route_RejectsArrayLimits(string path, int count)
    {
        var route = Route();
        var array = At(route, path).AsArray();
        var member = array[0]!.DeepClone();
        array.Clear();
        for (var i = 0; i < count; i++) array.Add(member.DeepClone());
        InvalidRoute(route);
    }

    [Theory]
    [InlineData("routeId")]
    [InlineData("displayName")]
    [InlineData("visibility")]
    [InlineData("mode")]
    [InlineData("requirements")]
    [InlineData("resourcePolicy")]
    [InlineData("resolution")]
    [InlineData("outcomes")]
    [InlineData("interruption")]
    [InlineData("resolution/modifierSource/kind")]
    [InlineData("requirements/0/quantity")]
    public void Route_RejectsMissingMembers(string path)
    {
        var route = Route();
        Remove(route, path);
        InvalidRoute(route);
    }

    [Theory]
    [InlineData("routeId")]
    [InlineData("requirements/0/quantity")]
    [InlineData("resolution/modifierSource/kind")]
    [InlineData("outcomes/3/result/0/complicationDraft/consequenceDefinitions/0/definition/components/0/payload/actionKey")]
    public void Route_RejectsOriginalRecursiveDuplicateProperties(string path)
    {
        var route = EffectfulRoute();
        var (parent, field) = Parent(route, path);
        // Inject into the original JSON, never through JsonNode's duplicate-collapsing parser.
        parent[field] ??= "hold";
        var original = parent.ToJsonString();
        var duplicate = original.Insert(1, JsonSerializer.Serialize(field) + ":" + parent[field]!.ToJsonString() + ",");
        var raw = route.ToJsonString().Replace(original, duplicate, StringComparison.Ordinal);
        var result = ParseRoute(Element(raw));
        Assert.False(result.IsValid);
        Assert.Null(result.Route);
        Assert.Contains(result.Issues, issue => issue.Code == "wound_materialization_duplicate_property" &&
            issue.FilePath == "command.result.route." + path.Replace("/0/", "[0].").Replace("/3/", "[3].").Replace('/', '.'));
    }

    [Fact]
    public void Route_LocalPolicyReferenceDoesNotGrantRealPolicyMembership()
    {
        var route = Route();
        route["outcomes"]![3]!["result"] = new JsonArray(Operation("apply_deterioration"));
        ValidRoute(Element(route));
        var wound = Wound(route);
        wound["recovery"]!["deteriorationPolicy"] = new JsonObject
        {
            ["policyRef"] = "different_policy", ["cadenceMinutes"] = 60,
            ["unmetConditions"] = new JsonArray("not_stabilized"), ["graceMinutes"] = 30,
            ["result"] = new JsonObject { ["kind"] = "increase_severity" }
        };
        AssertFullIssue(wound, ".policyRef");
        wound["recovery"]!["deteriorationPolicy"]!["policyRef"] = "real_policy";
        var full = WoundMaterializationContract.Parse(wound.ToJsonString(), "wound");
        Assert.True(full.IsValid, string.Join("; ", full.Issues.Select(i => i.FilePath + ":" + i.Expected)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Route_LocalTargetShapeDoesNotGrantRealOwnerApplicability(bool legacy)
    {
        var route = legacy ? LegacyRoute() : EffectfulRoute();
        Definition(route, legacy)["allowedTargetKinds"] = new JsonArray("npc");
        ValidRoute(Element(route));
        AssertFullIssue(Wound(route), ".allowedTargetKinds");
    }

    [Fact]
    public void Route_LocalReactionShapeDoesNotGrantRankOrSeverityAuthority()
    {
        var route = ReactionRoute();
        ValidRoute(Element(route));
        AssertFullIssue(Wound(route), ".definitionKey");
        var wound = Wound(route);
        wound["severity"]!["value"] = "III";
        wound["severity"]!["rank"] = 3;
        wound["severity"]!["maximumAtCreation"] = "III";
        var full = WoundMaterializationContract.Parse(wound.ToJsonString(), "wound");
        Assert.True(full.IsValid, string.Join("; ", full.Issues.Select(i => i.FilePath + ":" + i.Expected)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Route_CompleteNestedGraphsAreDetachedAndRoundTrip(bool legacy)
    {
        var route = legacy ? LegacyRoute() : EffectfulRoute();
        var original = route.ToJsonString();
        MortalWoundTreatmentRouteDefinition parsed;
        using (var document = JsonDocument.Parse(original)) parsed = ValidRoute(document.RootElement);
        Definition(route, legacy)["components"] = new JsonArray();
        var canonical = Write(parsed);
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(original), JsonNode.Parse(canonical)));
        Assert.Equal(canonical, Write(ValidRoute(Element(canonical))));
    }

    [Theory]
    [InlineData(false, "links", "[{\"kind\":\"wound\"}]")]
    [InlineData(false, "unknown", "true")]
    [InlineData(false, "parameterBounds", "{\"amount\":{\"type\":\"integer\",\"minimum\":1,\"maximum\":2}}")]
    [InlineData(false, "lifetime/activePredicate", "\"equipped\"")]
    [InlineData(false, "stacking/maxStacks", "2")]
    [InlineData(false, "components/0/profile", "\"invented\"")]
    [InlineData(true, "links", "[{\"kind\":\"wound\"}]")]
    [InlineData(true, "unknown", "true")]
    [InlineData(true, "lifetime/activePredicate", "\"equipped\"")]
    [InlineData(true, "components/0/payload", "{}")]
    public void Route_NestedDefinitionsAreNeverOpaque(bool legacy, string path, string json)
    {
        var route = legacy ? LegacyRoute() : EffectfulRoute();
        Set(Definition(route, legacy), path, JsonNode.Parse(json));
        InvalidRoute(route);
    }

    [Theory]
    [InlineData("complication_owner")]
    [InlineData("slot_profile")]
    [InlineData("root_missing")]
    [InlineData("local_edge")]
    [InlineData("edge_expansion")]
    [InlineData("legacy_ref")]
    [InlineData("legacy_parameters")]
    [InlineData("orphan")]
    public void Route_NestedLocalGraphsRemainClosed(string mutation)
    {
        var legacy = mutation.StartsWith("legacy", StringComparison.Ordinal) || mutation == "orphan";
        var route = legacy ? LegacyRoute() : ReactionRoute();
        var definitions = legacy
            ? At(route, "outcomes/0/result/0/legacies/1/effectDraft/definitions").AsArray()
            : At(route, "outcomes/3/result/0/complicationDraft/consequenceDefinitions").AsArray();
        switch (mutation)
        {
            case "complication_owner": definitions[0]!["root"]!["ownership"]!["complicationRef"] = "other"; break;
            case "slot_profile": definitions[0]!["root"]!["slots"]![0]!["profileKey"] = "action_control"; break;
            case "root_missing": definitions[0]!["root"] = null; break;
            case "local_edge": definitions[0]!["definition"]!["components"]![0]!["payload"]!["definitionKey"] = "external"; break;
            case "edge_expansion": definitions[0]!["definition"]!["components"]![0]!["payload"]!["maxExpansion"] = 3; break;
            case "legacy_ref": Set(route, "outcomes/0/result/0/legacies/1/effectDraft/applications/0/definitionRef", JsonValue.Create("external")); break;
            case "legacy_parameters": Set(route, "outcomes/0/result/0/legacies/1/effectDraft/applications/0/parameters", JsonNode.Parse("{\"unknown\":1}")); break;
            case "orphan":
                var orphan = definitions[0]!.DeepClone();
                orphan["definitionRef"] = "orphan";
                orphan["definition"]!["definitionKey"] = "orphan_key";
                definitions.Add(orphan);
                break;
        }
        InvalidRoute(route);
    }

    [Fact]
    public void Diagnosis_CompleteDetachedRoundTripPreservesExternalFactsAndOrder()
    {
        var path = Diagnosis();
        var original = path.ToJsonString();
        MortalWoundDiagnosisPathDefinition parsed;
        using (var document = JsonDocument.Parse(original)) parsed = ValidDiagnosis(document.RootElement);
        path["reveals"] = new JsonArray();
        Assert.Equal("command.result.diagnosisPath", parsed.SourcePath);
        var canonical = Write(parsed);
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(original), JsonNode.Parse(canonical)));
        Assert.DoesNotContain("SourcePath", canonical);
        Assert.Equal(canonical, Write(ValidDiagnosis(Element(canonical))));
        var wound = Wound(Route());
        wound["treatment"]!["diagnosisPaths"] = new JsonArray(JsonNode.Parse(original));
        AssertFullIssue(wound, ".requiresKnownFacts[0]");
    }

    [Fact]
    public void Diagnosis_LocalFactsDoNotSeedWholeWoundCycles()
    {
        var route = Route();
        route["visibility"] = "hidden";
        var diagnosis = Diagnosis();
        diagnosis["requiresKnownFacts"] = new JsonArray("route:clean_and_suture");
        diagnosis["reveals"] = new JsonArray("route:clean_and_suture");
        ValidDiagnosis(Element(diagnosis));
        var wound = Wound(route);
        wound["treatment"]!["knownRouteIds"] = new JsonArray();
        wound["treatment"]!["diagnosisPaths"] = new JsonArray(diagnosis);
        var result = WoundMaterializationContract.Parse(wound.ToJsonString(), "wound");
        Assert.Contains(result.Issues, i => i.Code == "wound_treatment_discovery_cycle");
    }

    [Theory]
    [InlineData("route:")]
    [InlineData("Route:valid")]
    [InlineData("complication: bad")]
    [InlineData("route:ｆｕｌｌ")]
    [InlineData("route:bad\nname")]
    [InlineData("other:valid")]
    public void Diagnosis_RejectsNonExactFactGrammar(string fact)
    {
        var path = Diagnosis();
        path["requiresKnownFacts"] = new JsonArray(fact);
        InvalidDiagnosis(path);
    }

    [Theory]
    [InlineData("route:setting/specific")]
    [InlineData("route:setting:specific")]
    [InlineData("complication:setting-specific")]
    public void Diagnosis_PreservesExistingExactIdentifierGrammar(string fact)
    {
        var path = Diagnosis();
        path["requiresKnownFacts"] = new JsonArray(fact);
        ValidDiagnosis(Element(path));
    }

    [Theory]
    [InlineData("{\"kind\":\"item_quantity\",\"itemRef\":\"thread\",\"quantity\":2147483647,\"ownerRole\":\"provider\"}")]
    [InlineData("{\"kind\":\"resource_quantity\",\"resourceRef\":\"mana\",\"quantity\":1,\"ownerRole\":\"target\"}")]
    [InlineData("{\"kind\":\"skill_tier\",\"capabilityRef\":\"medicine\",\"minimumTier\":2,\"actorRole\":\"provider\"}")]
    [InlineData("{\"kind\":\"source_capability\",\"capabilityRef\":\"setting_cure\",\"actorRole\":\"provider\"}")]
    [InlineData("{\"kind\":\"provider\",\"providerRef\":\"medic\"}")]
    [InlineData("{\"kind\":\"consent\",\"consentRef\":\"agreement\",\"providerRef\":\"medic\",\"targetRef\":\"patient\"}")]
    [InlineData("{\"kind\":\"facility\",\"facilityRef\":\"clinic\"}")]
    [InlineData("{\"kind\":\"location\",\"locationRef\":\"safe_place\",\"targetRole\":\"target\"}")]
    [InlineData("{\"kind\":\"quest_state\",\"questRef\":\"quest\",\"requiredState\":\"completed\"}")]
    [InlineData("{\"kind\":\"effect_state\",\"effectRef\":\"ward\",\"requiredState\":\"active\",\"targetRole\":\"target\"}")]
    [InlineData("{\"kind\":\"environment\",\"environmentRef\":\"moon\",\"requiredState\":\"full\"}")]
    public void Members_ShareAllRegisteredRequirementsAndCanonicalWriters(string json)
    {
        var route = Route();
        route["requirements"]!.AsArray().Add(JsonNode.Parse(json));
        var parsed = ValidRoute(Element(route));
        Assert.True(JsonNode.DeepEquals(route, JsonNode.Parse(Write(parsed))));

        var path = Diagnosis();
        path["requirements"]!.AsArray().Add(JsonNode.Parse(json));
        var diagnosis = ValidDiagnosis(Element(path));
        Assert.True(JsonNode.DeepEquals(path, JsonNode.Parse(Write(diagnosis))));

        path["requirements"]![0]!["extra"] = true;
        InvalidDiagnosis(path);
    }

    [Fact]
    public void Route_RejectsNonMonotoneBandOrderAndDuplicateSelectors()
    {
        var route = Route();
        route["outcomes"]![1]!["category"] = "failed_attempt";
        route["outcomes"]![2]!["category"] = "partial_success";
        InvalidRoute(route);

        route = Route();
        var mutations = route["resourcePolicy"]!["mutations"]!.AsArray();
        mutations.Add(mutations[0]!.DeepClone());
        InvalidRoute(route);
    }

    [Fact]
    public void Route_LocalPowerDoesNotGrantRealSeverityEnvelope()
    {
        var route = EffectfulRoute();
        var definition = DetachedDefinition("powerful_consequence", "characteristic_modifier");
        definition["components"]![0]!["payload"]!["value"] = -40;
        Set(route, "outcomes/3/result/0/complicationDraft/consequenceDefinitions", new JsonArray(Wrapper(definition, "characteristic_modifier")));
        ValidRoute(Element(route));
        AssertFullIssue(Wound(route), ".payload.value");
    }

    [Fact]
    public void Route_ProposalMarkerNormalizationDoesNotPublishAuthoredWoundIdentity()
    {
        var route = EffectfulRoute();
        var definition = DetachedDefinition("proposal_marker", "wound_consequence");
        definition["components"]![0]!["payload"]!.AsObject().Remove("woundId");
        var wrapper = Wrapper(definition, "wound_consequence");
        wrapper["root"]!["slots"] = new JsonArray();
        Set(route, "outcomes/3/result/0/complicationDraft/consequenceDefinitions", new JsonArray(wrapper));
        var canonical = Write(ValidRoute(Element(route)));
        Assert.DoesNotContain("wound_proposal_local_marker", canonical);
        Assert.DoesNotContain("woundId", canonical);
        definition["components"]![0]!["payload"]!["woundId"] = "authored_identity";
        InvalidRoute(route);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("out_of_bounds")]
    [InlineData("valid")]
    public void Route_MechanicalApplicationBindsDeclaredParameterBounds(string scenario)
    {
        var route = LegacyRoute();
        Definition(route, true)["parameterBounds"] = JsonNode.Parse("{\"action\":{\"kind\":\"enum\",\"allowedValues\":[\"use_item\"],\"required\":true}}");
        var parameters = At(route, "outcomes/0/result/0/legacies/1/effectDraft/applications/0/parameters");
        if (scenario != "missing") parameters["action"] = scenario == "valid" ? "use_item" : "attack";
        if (scenario == "valid") ValidRoute(Element(route)); else InvalidRoute(route);
    }

    [Theory]
    [InlineData(-40, true)]
    [InlineData(0, false)]
    [InlineData(101, false)]
    public void Route_ReactionParametersAreBoundBeforeLocalComponentValidation(int value, bool valid)
    {
        var route = ReactionRoute();
        var definitions = At(route, "outcomes/3/result/0/complicationDraft/consequenceDefinitions");
        var leaf = DetachedDefinition("leaf", "resistance_modifier");
        leaf["components"]![0]!["payload"]!.AsObject().Remove("cap");
        leaf["parameterBounds"] = JsonNode.Parse("{\"value\":{\"kind\":\"number\",\"minimum\":-100,\"maximum\":100}}");
        definitions[1]!["definition"] = leaf;
        definitions[0]!["definition"]!["components"]![0]!["payload"]!["parameters"] = new JsonObject { ["value"] = value };
        definitions[0]!["root"]!["slots"]![1]!["profileKey"] = "resistance_modifier";
        if (valid) ValidRoute(Element(route)); else InvalidRoute(route);
    }

    [Fact]
    public void Members_CanonicalWritersMatchFullTreatmentSerialization()
    {
        var route = Route();
        var diagnosis = Diagnosis();
        diagnosis["requiresKnownFacts"] = new JsonArray("route:clean_and_suture");
        diagnosis["reveals"] = new JsonArray("route:clean_and_suture");
        var wound = Wound(route);
        wound["treatment"]!["diagnosisPaths"] = new JsonArray(diagnosis.DeepClone());
        var full = WoundMaterializationContract.Parse(wound.ToJsonString(), "wound");
        Assert.True(full.IsValid);
        using var canonical = JsonDocument.Parse(WoundMaterializationContract.SerializeCanonical(full.Wound!));
        var treatment = canonical.RootElement.GetProperty("treatment");
        Assert.Equal(treatment.GetProperty("routes")[0].GetRawText(), Write(ValidRoute(Element(route))));
        Assert.Equal(treatment.GetProperty("diagnosisPaths")[0].GetRawText(), Write(ValidDiagnosis(Element(diagnosis))));
    }

    [Fact]
    public void Route_MechanicalLegacyRetainsFiveDefinitionsWithoutWoundSlotBudget()
    {
        var route = LegacyRoute();
        var draft = At(route, "outcomes/0/result/0/legacies/1/effectDraft");
        var definitions = draft["definitions"]!.AsArray();
        var applications = draft["applications"]!.AsArray();
        definitions.Clear();
        applications.Clear();
        for (var index = 0; index < 5; index++)
        {
            var reference = "definition_" + index;
            definitions.Add(new JsonObject { ["definitionRef"] = reference, ["definition"] = DetachedDefinition("key_" + index, "action_control") });
            applications.Add(new JsonObject { ["applicationRef"] = "application_" + index, ["definitionRef"] = reference, ["parameters"] = new JsonObject() });
        }
        ValidRoute(Element(route));
        var full = WoundMaterializationContract.Parse(Wound(route).ToJsonString(), "wound");
        Assert.True(full.IsValid, string.Join("; ", full.Issues.Select(i => i.FilePath + ":" + i.Expected)));
    }

    [Theory]
    [InlineData("diagnosisPathId", "\" bad \"")]
    [InlineData("displayName", "\"\"")]
    [InlineData("visibility", "\"invented\"")]
    [InlineData("check", "{\"difficulty\":10}")]
    [InlineData("check", "null")]
    [InlineData("requiresKnownFacts", "[]")]
    [InlineData("reveals", "[\"route:same\",\"route:same\"]")]
    [InlineData("reveals", "[42]")]
    [InlineData("failurePolicy", "\"reveal_anyway\"")]
    [InlineData("requirements", "[{\"kind\":\"invented\"}]")]
    [InlineData("extra", "true")]
    public void Diagnosis_RejectsInvalidLocalFields(string field, string json)
    {
        var path = Diagnosis();
        path[field] = JsonNode.Parse(json);
        InvalidDiagnosis(path);
    }

    [Fact]
    public void Diagnosis_RejectsEveryMissingMemberAndFactOverflow()
    {
        var original = Diagnosis();
        foreach (var field in original.Select(p => p.Key))
        {
            var path = original.DeepClone().AsObject();
            path.Remove(field);
            InvalidDiagnosis(path);
        }
        original["reveals"] = new JsonArray(Enumerable.Range(0, 17)
            .Select(i => (JsonNode)JsonValue.Create("route:external_" + i)!).ToArray());
        InvalidDiagnosis(original);
    }

    [Fact]
    public void Diagnosis_RejectsOriginalNestedDuplicateProperties()
    {
        var path = Diagnosis();
        path["requirements"] = new JsonArray(JsonNode.Parse("{\"kind\":\"provider\",\"providerRef\":\"medic\"}"));
        var raw = path.ToJsonString().Replace("\"providerRef\":\"medic\"", "\"providerRef\":\"medic\",\"providerRef\":\"other\"");
        var result = ParseDiagnosis(Element(raw));
        Assert.False(result.IsValid);
        Assert.Null(result.DiagnosisPath);
        Assert.Contains(result.Issues, i => i.Code == "wound_materialization_duplicate_property" &&
            i.FilePath == "command.result.diagnosisPath.requirements[0].providerRef");
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("3")]
    [InlineData("{}")]
    public void Members_InvalidInputNeverReturnsPartialSuccess(string json)
    {
        var route = ParseRoute(Element(json));
        Assert.False(route.IsValid);
        Assert.NotEmpty(route.Issues);
        Assert.Null(route.Route);
        var path = ParseDiagnosis(Element(json));
        Assert.False(path.IsValid);
        Assert.NotEmpty(path.Issues);
        Assert.Null(path.DiagnosisPath);
    }

    private static JsonObject Route(string mode = "procedure")
    {
        var route = WoundContractTestData.CreateActiveWound()["treatment"]!["routes"]![0]!.DeepClone().AsObject();
        if (mode == "procedure") return route;
        route["mode"] = mode;
        route["resourcePolicy"]!["consumeOn"] = new JsonArray("success");
        route["resourcePolicy"]!["mutations"] = new JsonArray();
        if (mode == "guaranteed")
        {
            route["requirements"] = new JsonArray(JsonNode.Parse("{\"kind\":\"source_capability\",\"capabilityRef\":\"healing_source\",\"actorRole\":\"provider\"}"));
            route["resolution"] = JsonNode.Parse("{\"capabilityRef\":\"healing_source\",\"actorRole\":\"provider\"}");
            route["outcomes"] = new JsonArray(JsonNode.Parse("{\"category\":\"success\",\"result\":[{\"kind\":\"stabilize\"}]}"));
        }
        else
        {
            route["requirements"] = new JsonArray();
            route["resolution"] = JsonNode.Parse("{\"clockKind\":\"world_time.currentTimeInMinutes\",\"maximumGapMinutes\":600}");
            route["outcomes"] = new JsonArray(Enumerable.Range(1, 2).Select(i => (JsonNode)new JsonObject
            {
                ["ordinal"] = i, ["afterMinutes"] = (i - 1) * 60,
                ["requirements"] = new JsonArray(JsonNode.Parse("{\"kind\":\"item_quantity\",\"itemRef\":\"medicine\",\"quantity\":1,\"ownerRole\":\"target\"}")),
                ["category"] = "success", ["completion"] = i == 2 ? "completed" : "active",
                ["result"] = i == 2 ? new JsonArray(Operation("heal")) : new JsonArray()
            }).ToArray());
            route["resourcePolicy"]!["mutations"] = new JsonArray(JsonNode.Parse("{\"kind\":\"consume_requirement\",\"scope\":\"course_milestone\",\"milestoneOrdinal\":1,\"requirementIndex\":0}"));
            route["interruption"] = JsonNode.Parse("{\"category\":\"failed_attempt\",\"result\":[{\"kind\":\"no_improvement\"}]}");
        }
        return route;
    }

    private static JsonObject Operation(string kind) => kind switch
    {
        "add_recovery" => new() { ["kind"] = kind, ["points"] = 1 },
        "reduce_severity" => new() { ["kind"] = kind, ["steps"] = 1 },
        "remove_complication" => new() { ["kind"] = kind, ["complicationId"] = "external_complication" },
        "apply_deterioration" => new() { ["kind"] = kind, ["policyRef"] = "real_policy" },
        "heal" => new() { ["kind"] = kind, ["legacies"] = new JsonArray() },
        "add_complication" => Route()["outcomes"]![3]!["result"]![0]!.DeepClone().AsObject(),
        _ => new() { ["kind"] = kind }
    };

    private static JsonObject EffectfulRoute()
    {
        var route = Route();
        var definition = DetachedDefinition("grip_limit", "action_control");
        Set(route, "outcomes/3/result/0/complicationDraft/consequenceDefinitions", new JsonArray(Wrapper(definition, "action_control")));
        return route;
    }

    private static JsonObject ReactionRoute()
    {
        var route = EffectfulRoute();
        var producer = WoundContractTestData.CreateApplyDefinitionRoot("unused", "mortal_world", "reaction", "leaf");
        producer["links"] = new JsonArray();
        var leaf = DetachedDefinition("leaf", "action_control");
        var root = Wrapper(producer, "event_reaction");
        root["root"]!["slots"]!.AsArray().Add(JsonNode.Parse("{\"profileKey\":\"action_control\",\"readableSummary\":\"Боль мешает движению.\"}"));
        Set(route, "outcomes/3/result/0/complicationDraft/consequenceDefinitions", new JsonArray(root, new JsonObject
        {
            ["definitionRef"] = "leaf_ref", ["definition"] = leaf, ["root"] = null
        }));
        return route;
    }

    private static JsonObject DetachedDefinition(string key, string profile)
    {
        var definition = WoundContractTestData.CreateOwnedEffectDefinition("unused", "mortal_world", key, profile);
        definition["links"] = new JsonArray();
        return definition;
    }

    private static JsonObject Wrapper(JsonObject definition, string profile) => new()
    {
        ["definitionRef"] = definition["definitionKey"]!.GetValue<string>() + "_ref",
        ["definition"] = definition,
        ["root"] = new JsonObject
        {
            ["ownership"] = new JsonObject { ["kind"] = "complication", ["complicationRef"] = "irritation" },
            ["slots"] = new JsonArray(new JsonObject { ["profileKey"] = profile, ["readableSummary"] = "Боль мешает движению." })
        }
    };

    private static JsonObject LegacyRoute()
    {
        var route = Route();
        var heal = Operation("heal");
        heal["legacies"] = new JsonArray(
            new JsonObject { ["localLegacyRef"] = "scar", ["kind"] = "cosmetic", ["readableSummary"] = "Остался шрам." },
            new JsonObject
            {
                ["localLegacyRef"] = "tremor", ["kind"] = "mechanical_effect", ["readableSummary"] = "Осталась дрожь.",
                ["effectDraft"] = new JsonObject
                {
                    ["schemaVersion"] = 1,
                    ["definitions"] = new JsonArray(new JsonObject { ["definitionRef"] = "tremor_ref", ["definition"] = DetachedDefinition("tremor_key", "action_control") }),
                    ["applications"] = new JsonArray(JsonNode.Parse("{\"applicationRef\":\"tremor_app\",\"definitionRef\":\"tremor_ref\",\"parameters\":{}}"))
                }
            });
        route["outcomes"]![0]!["result"] = new JsonArray(heal);
        return route;
    }

    private static JsonObject Definition(JsonObject route, bool legacy) => At(route, legacy
        ? "outcomes/0/result/0/legacies/1/effectDraft/definitions/0/definition"
        : "outcomes/3/result/0/complicationDraft/consequenceDefinitions/0/definition").AsObject();

    private static JsonObject Diagnosis() => new()
    {
        ["diagnosisPathId"] = "examine_setting_specific", ["displayName"] = "Сопоставить необычные признаки.",
        ["visibility"] = "hidden", ["requiresKnownFacts"] = new JsonArray("route:external_z", "complication:external_a"),
        ["requirements"] = new JsonArray(), ["check"] = new JsonObject(),
        ["reveals"] = new JsonArray("complication:revealed_z", "route:revealed_a"), ["failurePolicy"] = "no_reveal"
    };

    private static JsonObject Wound(JsonObject route)
    {
        var wound = WoundContractTestData.CreateActiveWound();
        wound["treatment"]!["routes"] = new JsonArray(route.DeepClone());
        return wound;
    }

    private static void AssertFullIssue(JsonObject wound, string suffix)
    {
        var result = WoundMaterializationContract.Parse(wound.ToJsonString(), "wound");
        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, i => i.FilePath.EndsWith(suffix, StringComparison.Ordinal));
    }

    private static JsonElement Element(JsonNode value) => JsonSerializer.SerializeToElement(value);
    private static JsonElement Element(string json) { using var doc = JsonDocument.Parse(json); return doc.RootElement.Clone(); }
    private static JsonNode At(JsonNode root, string path)
    {
        foreach (var segment in path.Split('/')) root = root is JsonArray a ? a[int.Parse(segment)]! : root[segment]!;
        return root;
    }
    private static (JsonObject Parent, string Field) Parent(JsonNode root, string path)
    {
        var split = path.LastIndexOf('/');
        return (split < 0 ? root.AsObject() : At(root, path[..split]).AsObject(), path[(split + 1)..]);
    }
    private static void Set(JsonNode root, string path, JsonNode? value) { var (parent, field) = Parent(root, path); parent[field] = value; }
    private static void Remove(JsonNode root, string path) { var (parent, field) = Parent(root, path); parent.Remove(field); }

    private static MortalWoundTreatmentRouteShapeParseResult ParseRoute(JsonElement value) =>
        MortalWoundTreatmentContract.ParseRouteShape(value, "command.result.route");

    private static MortalWoundDiagnosisPathShapeParseResult ParseDiagnosis(JsonElement value) =>
        MortalWoundTreatmentContract.ParseDiagnosisPathShape(value, "command.result.diagnosisPath");
    private static MortalWoundTreatmentRouteDefinition ValidRoute(JsonElement value)
    {
        var result = ParseRoute(value);
        Assert.True(result.IsValid, string.Join("; ", result.Issues.Select(i => i.FilePath + ":" + i.Code + ":" + i.Expected)));
        Assert.Empty(result.Issues);
        return Assert.IsAssignableFrom<MortalWoundTreatmentRouteDefinition>(result.Route);
    }
    private static MortalWoundDiagnosisPathDefinition ValidDiagnosis(JsonElement value)
    {
        var result = ParseDiagnosis(value);
        Assert.True(result.IsValid, string.Join("; ", result.Issues.Select(i => i.Code)));
        Assert.Empty(result.Issues);
        return Assert.IsType<MortalWoundDiagnosisPathDefinition>(result.DiagnosisPath);
    }
    private static void InvalidRoute(JsonObject route)
    {
        var result = ParseRoute(Element(route));
        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Issues);
        Assert.Null(result.Route);
    }
    private static void InvalidDiagnosis(JsonObject path)
    {
        var result = ParseDiagnosis(Element(path));
        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Issues);
        Assert.Null(result.DiagnosisPath);
    }
    private static string Write(MortalWoundTreatmentRouteDefinition route) =>
        Write(writer => MortalWoundTreatmentContract.WriteRouteCanonical(writer, route));

    private static string Write(MortalWoundDiagnosisPathDefinition path) =>
        Write(writer => MortalWoundTreatmentContract.WriteDiagnosisPathCanonical(writer, path));

    private static string Write(Action<Utf8JsonWriter> write)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions
               { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
            write(writer);
        return Encoding.UTF8.GetString(buffer.ToArray());
    }
}
