using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GmTreatmentRouteDraftTests
{
    [Fact]
    public void Removal_UsesOnlyUnresolvedGmRefAndRejectsCanonicalDialect()
    {
        var route = Route();
        route["outcomes"]![0]!["result"] = new JsonArray(new JsonObject
        {
            ["kind"] = "remove_complication", ["complicationRef"] = "offered_selector"
        });
        var element = Element(route);

        var parsed = MortalWoundTreatmentContract.ParseGmRouteDraftShape(element, "author.route");

        Assert.True(parsed.IsValid, Describe(parsed.Issues));
        var draft = Assert.IsType<GmProcedureRouteDraft>(parsed.Route);
        var removal = Assert.IsType<GmRemoveComplicationDraft>(Assert.Single(draft.Bands[0].DeclaredResult));
        Assert.Equal("offered_selector", removal.ComplicationRef);
        Assert.False(MortalWoundTreatmentContract.ParseRouteShape(element, "canonical.route").IsValid);
        var written = JsonNode.Parse(Write(draft))!;
        Assert.True(JsonNode.DeepEquals(route, written));
        Assert.Null(written["outcomes"]![0]!["result"]![0]!["complicationId"]);

        route["outcomes"]![0]!["result"]![0]!.AsObject().Remove("complicationRef");
        route["outcomes"]![0]!["result"]![0]!["complicationId"] = "existing_complication";
        Assert.False(MortalWoundTreatmentContract.ParseGmRouteDraftShape(
            Element(route), "author.route").IsValid);
        Assert.True(MortalWoundTreatmentContract.ParseRouteShape(
            Element(route), "canonical.route").IsValid);
    }

    [Theory]
    [InlineData("procedure")]
    [InlineData("course")]
    [InlineData("guaranteed")]
    public void CompleteRoutes_AllModesRoundTripWithoutSourcePath(string mode)
    {
        var route = Route(mode);
        var original = route.ToJsonString();
        GmTreatmentRouteDraft draft;
        using (var document = JsonDocument.Parse(original))
            draft = Valid(document.RootElement);
        route["displayName"] = "mutated after parse";

        Assert.Equal(mode, draft.Mode);
        Assert.Equal("author.route", draft.SourcePath);
        Assert.IsType(mode switch
        {
            "procedure" => typeof(GmProcedureRouteDraft),
            "course" => typeof(GmCourseRouteDraft),
            _ => typeof(GmGuaranteedRouteDraft)
        }, draft);
        var written = Write(draft);
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(original), JsonNode.Parse(written)));
        Assert.DoesNotContain("SourcePath", written, StringComparison.Ordinal);
        Assert.Equal(written, Write(Valid(Element(written))));
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
    public void CompleteRoute_AllEightOperationsRoundTrip(string kind)
    {
        var route = Route();
        var index = kind is "no_improvement" or "add_complication" or "apply_deterioration" ? 3 : 0;
        route["outcomes"]![index]!["result"] = new JsonArray(Operation(kind));

        var written = Write(Valid(Element(route)));

        Assert.True(JsonNode.DeepEquals(route, JsonNode.Parse(written)));
    }

    [Theory]
    [InlineData("procedure")]
    [InlineData("course")]
    [InlineData("guaranteed")]
    public void Removal_RoundTripsInEveryLegalModeOutcome(string mode)
    {
        var route = Route(mode);
        route["outcomes"]![0]!["result"] = new JsonArray(Operation("remove_complication"));

        var draft = Valid(Element(route));
        var operations = draft switch
        {
            GmProcedureRouteDraft procedure => procedure.Bands[0].DeclaredResult,
            GmCourseRouteDraft course => course.Milestones[0].DeclaredResult,
            GmGuaranteedRouteDraft guaranteed => guaranteed.Outcome.DeclaredResult,
            _ => throw new InvalidOperationException()
        };

        Assert.IsType<GmRemoveComplicationDraft>(Assert.Single(operations));
        Assert.True(JsonNode.DeepEquals(route, JsonNode.Parse(Write(draft))));
    }

    [Theory]
    [InlineData("{\"kind\":\"item_quantity\",\"itemRef\":\"thread\",\"quantity\":1,\"ownerRole\":\"provider\"}")]
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
    public void CompleteRoute_AllElevenRequirementsRoundTrip(string json)
    {
        var route = Route();
        route["requirements"]!.AsArray().Add(JsonNode.Parse(json));

        Assert.True(JsonNode.DeepEquals(route, JsonNode.Parse(Write(Valid(Element(route))))));
    }

    [Fact]
    public void CompleteNestedComplicationAndHealLegacyGraphsRemainTypedDetachedAndLossless()
    {
        var complicationRoute = EffectfulComplicationRoute();
        var complicationOriginal = complicationRoute.ToJsonString();
        var complication = Assert.IsType<GmAddComplicationDraft>(Assert.Single(
            Assert.IsType<GmProcedureRouteDraft>(Valid(Element(complicationRoute))).Bands[3].DeclaredResult));
        complicationRoute["outcomes"]![3]!["result"]![0]!["complicationDraft"]!["consequenceDefinitions"] = new JsonArray();
        Assert.Single(complication.ComplicationDraft.ConsequenceDefinitions);

        var legacyRoute = LegacyRoute();
        var legacyOriginal = legacyRoute.ToJsonString();
        var legacyDraft = Assert.IsType<GmProcedureRouteDraft>(Valid(Element(legacyRoute)));
        var heal = Assert.IsType<GmHealDraft>(Assert.Single(legacyDraft.Bands[0].DeclaredResult));
        legacyRoute["outcomes"]![0]!["result"]![0]!["legacies"] = new JsonArray();
        Assert.Collection(heal.Legacies,
            item => Assert.IsType<MortalWoundCosmeticLegacyDraft>(item),
            item => Assert.IsType<MortalWoundMechanicalEffectLegacyDraft>(item));
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(complicationOriginal), JsonNode.Parse(Write(
            Valid(Element(complicationOriginal))))));
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(legacyOriginal), JsonNode.Parse(Write(legacyDraft))));
    }

    [Theory]
    [InlineData("missing", "wound_materialization_missing_field", "author.route.outcomes[0].result[0].complicationRef")]
    [InlineData("null", "wound_materialization_invalid_identifier", "author.route.outcomes[0].result[0].complicationRef")]
    [InlineData("number", "wound_materialization_invalid_identifier", "author.route.outcomes[0].result[0].complicationRef")]
    [InlineData("canonical", "wound_materialization_unknown_field", "author.route.outcomes[0].result[0].complicationId")]
    [InlineData("both", "wound_materialization_unknown_field", "author.route.outcomes[0].result[0].complicationId")]
    public void Removal_RejectsMissingWrongTypeAndCanonicalSelectors(
        string mutation, string code, string path)
    {
        var route = Route();
        var operation = Operation("remove_complication");
        route["outcomes"]![0]!["result"] = new JsonArray(operation);
        switch (mutation)
        {
            case "missing": operation.Remove("complicationRef"); break;
            case "null": operation["complicationRef"] = null; break;
            case "number": operation["complicationRef"] = 7; break;
            case "canonical":
                operation.Remove("complicationRef");
                operation["complicationId"] = "existing_complication";
                break;
            case "both": operation["complicationId"] = "existing_complication"; break;
        }

        var parsed = MortalWoundTreatmentContract.ParseGmRouteDraftShape(Element(route), "author.route");

        Assert.False(parsed.IsValid);
        Assert.Null(parsed.Route);
        Assert.Contains(parsed.Issues, issue => issue.Code == code && issue.FilePath == path);
    }

    [Fact]
    public void Removal_RejectsOriginalDuplicateWithoutJsonNodeCollapse()
    {
        var route = Route();
        route["outcomes"]![0]!["result"] = new JsonArray(Operation("remove_complication"));
        var raw = route.ToJsonString().Replace(
            "\"complicationRef\":\"offered_selector\"",
            "\"complicationRef\":\"offered_selector\",\"complicationRef\":\"other\"",
            StringComparison.Ordinal);

        var parsed = MortalWoundTreatmentContract.ParseGmRouteDraftShape(Element(raw), "author.route");

        Assert.False(parsed.IsValid);
        Assert.Null(parsed.Route);
        Assert.Contains(parsed.Issues, issue =>
            issue.Code == "wound_materialization_duplicate_property" &&
            issue.FilePath == "author.route.outcomes[0].result[0].complicationRef");
    }

    [Fact]
    public void NestedNewComplicationRefsRemainDeclarationsAndAreNotRewritten()
    {
        var route = EffectfulComplicationRoute();

        var written = JsonNode.Parse(Write(Valid(Element(route))))!;

        var draft = written["outcomes"]![3]!["result"]![0]!["complicationDraft"]!;
        Assert.Equal("irritation", draft["complications"]![0]!["complicationRef"]!.GetValue<string>());
        Assert.Equal("irritation", draft["consequenceDefinitions"]![0]!["root"]!["ownership"]!["complicationRef"]!.GetValue<string>());
        Assert.Null(draft["complications"]![0]!["complicationId"]);
    }

    [Fact]
    public void Writer_RejectsUnknownRouteSubtypeAndInvalidTypedOperation()
    {
        var parsed = Valid(Element(Route()));
        var unknown = new UnknownRouteDraft(parsed.RouteId, parsed.DisplayName, parsed.Visibility,
            parsed.Requirements, parsed.ResourcePolicy, parsed.SourcePath);
        Assert.Throws<InvalidOperationException>(() => Write(unknown));

        var procedure = Assert.IsType<GmProcedureRouteDraft>(parsed);
        var invalid = procedure with
        {
            Bands = procedure.Bands.SetItem(0, procedure.Bands[0] with
            {
                DeclaredResult = [new UnknownOperationDraft()]
            })
        };
        Assert.Throws<InvalidOperationException>(() => Write(invalid));
    }

    private sealed record UnknownRouteDraft(string Id, string Name, string RouteVisibility,
        System.Collections.Immutable.ImmutableArray<MortalWoundTreatmentRequirement> RouteRequirements,
        MortalWoundTreatmentResourcePolicy Policy, string Path)
        : GmTreatmentRouteDraft(Id, Name, RouteVisibility, RouteRequirements, Policy, Path)
    {
        internal override string Mode => "unknown";
    }

    private sealed record UnknownOperationDraft : GmTreatmentOperationDraft
    {
        internal override string Kind => "unknown";
    }

    private static JsonObject Route(string mode = "procedure")
    {
        var (_, request) = WoundAcceptedTransitionCommandTestData.Create(
            "author_alternative_treatment",
            "gm_route_shape");
        var proposedAfter = JsonNode.Parse(
            WoundMaterializationContract.SerializeCanonical(request.ProposedAfter!))!.AsObject();
        var route = proposedAfter["treatment"]!["routes"]![1]!.DeepClone().AsObject();
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
            route["outcomes"] = new JsonArray(Enumerable.Range(1, 2).Select(index => (JsonNode)new JsonObject
            {
                ["ordinal"] = index,
                ["afterMinutes"] = (index - 1) * 60,
                ["requirements"] = new JsonArray(),
                ["category"] = "success",
                ["completion"] = index == 2 ? "completed" : "active",
                ["result"] = index == 2 ? new JsonArray(Operation("heal")) : new JsonArray()
            }).ToArray());
            route["interruption"] = JsonNode.Parse("{\"category\":\"failed_attempt\",\"result\":[{\"kind\":\"no_improvement\"}]}");
        }
        return route;
    }

    private static JsonObject Operation(string kind) => kind switch
    {
        "add_recovery" => new() { ["kind"] = kind, ["points"] = 1 },
        "reduce_severity" => new() { ["kind"] = kind, ["steps"] = 1 },
        "remove_complication" => new() { ["kind"] = kind, ["complicationRef"] = "offered_selector" },
        "apply_deterioration" => new() { ["kind"] = kind, ["policyRef"] = "real_policy" },
        "heal" => new() { ["kind"] = kind, ["legacies"] = new JsonArray() },
        "add_complication" => Route()["outcomes"]![3]!["result"]![0]!.DeepClone().AsObject(),
        _ => new() { ["kind"] = kind }
    };

    private static JsonObject EffectfulComplicationRoute()
    {
        var route = Route();
        var operation = Operation("add_complication");
        var definition = WoundContractTestData.CreateOwnedEffectDefinition(
            "unused", "mortal_world", "grip_limit", "action_control");
        definition["links"] = new JsonArray();
        operation["complicationDraft"]!["consequenceDefinitions"] = new JsonArray(new JsonObject
        {
            ["definitionRef"] = "grip_limit_ref",
            ["definition"] = definition,
            ["root"] = new JsonObject
            {
                ["ownership"] = new JsonObject { ["kind"] = "complication", ["complicationRef"] = "irritation" },
                ["slots"] = new JsonArray(new JsonObject
                {
                    ["profileKey"] = "action_control", ["readableSummary"] = "Боль мешает движению."
                })
            }
        });
        route["outcomes"]![3]!["result"] = new JsonArray(operation);
        return route;
    }

    private static JsonObject LegacyRoute()
    {
        var route = Route();
        var definition = WoundContractTestData.CreateOwnedEffectDefinition(
            "unused", "mortal_world", "tremor_key", "action_control");
        definition["links"] = new JsonArray();
        route["outcomes"]![0]!["result"] = new JsonArray(new JsonObject
        {
            ["kind"] = "heal",
            ["legacies"] = new JsonArray(
                new JsonObject
                {
                    ["localLegacyRef"] = "scar", ["kind"] = "cosmetic",
                    ["readableSummary"] = "Остался шрам."
                },
                new JsonObject
                {
                    ["localLegacyRef"] = "tremor", ["kind"] = "mechanical_effect",
                    ["readableSummary"] = "Осталась дрожь.",
                    ["effectDraft"] = new JsonObject
                    {
                        ["schemaVersion"] = 1,
                        ["definitions"] = new JsonArray(new JsonObject
                        {
                            ["definitionRef"] = "tremor_ref", ["definition"] = definition
                        }),
                        ["applications"] = new JsonArray(new JsonObject
                        {
                            ["applicationRef"] = "tremor_app", ["definitionRef"] = "tremor_ref",
                            ["parameters"] = new JsonObject()
                        })
                    }
                })
        });
        return route;
    }

    private static GmTreatmentRouteDraft Valid(JsonElement element)
    {
        var result = MortalWoundTreatmentContract.ParseGmRouteDraftShape(element, "author.route");
        Assert.True(result.IsValid, Describe(result.Issues));
        Assert.Empty(result.Issues);
        return Assert.IsAssignableFrom<GmTreatmentRouteDraft>(result.Route);
    }

    private static string Write(GmTreatmentRouteDraft route)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
               { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
            MortalWoundTreatmentContract.WriteGmRouteDraft(writer, route);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static JsonElement Element(JsonNode node) => JsonSerializer.SerializeToElement(node);

    private static JsonElement Element(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static string Describe(IEnumerable<ValidationIssue> issues) => string.Join(
        "; ", issues.Select(issue => issue.FilePath + ":" + issue.Code + ":" + issue.Expected));
}
