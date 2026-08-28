using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class MortalWoundTreatmentContractTests
{
    private const string Path = "wound";

    [Fact]
    public void Parse_CompleteMortalProcedureRouteRequiresEveryMechanicalSection()
    {
        var complete = Parse(WoundContractTestData.CreateActiveWound());

        Assert.True(complete.IsValid, DescribeIssues(complete));
        Assert.Single(complete.Wound!.Treatment.Routes);
    }

    [Theory]
    [InlineData(
        "no_routes",
        "wound.treatment.routes",
        "wound_materialization_missing_field")]
    [InlineData(
        "missing_formula",
        "wound.treatment.routes[0].resolution.formulaKey",
        "wound_materialization_missing_field")]
    [InlineData(
        "no_outcomes",
        "wound.treatment.routes[0].outcomes",
        "wound_materialization_missing_field")]
    [InlineData(
        "missing_reservation_policy",
        "wound.treatment.routes[0].resourcePolicy.reserveBeforeResolution",
        "wound_materialization_missing_field")]
    [InlineData(
        "reservation_disabled",
        "wound.treatment.routes[0].resourcePolicy.reserveBeforeResolution",
        "wound_materialization_invalid_field")]
    public void Parse_IncompleteMortalProcedureRouteIsRejected(
        string mutation,
        string expectedPath,
        string expectedCode)
    {
        var wound = WoundContractTestData.CreateActiveWound();
        switch (mutation)
        {
            case "no_routes":
                wound["treatment"]!["routes"] = new JsonArray();
                wound["treatment"]!["knownRouteIds"] = new JsonArray();
                break;
            case "missing_formula":
                Route(wound)["resolution"]!.AsObject().Remove("formulaKey");
                break;
            case "no_outcomes":
                Route(wound)["outcomes"] = new JsonArray();
                break;
            case "missing_reservation_policy":
                Route(wound)["resourcePolicy"]!.AsObject()
                    .Remove("reserveBeforeResolution");
                break;
            case "reservation_disabled":
                Route(wound)["resourcePolicy"]!["reserveBeforeResolution"] = false;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null);
        }

        AssertInvalidAt(
            Parse(wound),
            expectedPath,
            expectedCode);
    }

    [Fact]
    public void Parse_TreatmentTextCannotBeTheOnlyNonDisplayMortalImpact()
    {
        var wound = WoundContractTestData.CreateActiveWound();
        wound["severity"]!["value"] = "I";
        wound["severity"]!["rank"] = 1;
        wound["severity"]!["maximumAtCreation"] = "I";
        wound["care"]!["state"] = "stabilized";
        wound["care"]!["stabilizedAtTurn"] = 42;
        wound["complications"] = new JsonArray();
        wound["consequences"] = new JsonObject
        {
            ["slotBudget"] = 1,
            ["slotsUsed"] = 0,
            ["ownedEffectSources"] = new JsonObject
            {
                ["definitions"] = new JsonArray(),
                ["rootBindings"] = new JsonArray()
            },
            ["entries"] = new JsonArray()
        };
        wound["recovery"]!["mode"] = "progressive";
        wound["recovery"]!["blockers"] = new JsonArray();

        AssertInvalidAt(
            Parse(wound),
            Path + ".consequences.lifecycleEvidence",
            "wound_consequence_non_display_impact_required");
    }

    [Fact]
    public void Parse_RequirementsWithinOneRouteAreConjunctiveNotNestedAlternatives()
    {
        var complete = Parse(WoundContractTestData.CreateActiveWound());

        Assert.True(complete.IsValid, DescribeIssues(complete));
        Assert.Equal(2, Assert.Single(complete.Wound!.Treatment.Routes).Requirements.Count);

        var nestedAlternative = WoundContractTestData.CreateActiveWound();
        Route(nestedAlternative)["requirements"] = new JsonArray(new JsonObject
        {
            ["kind"] = "any_of",
            ["options"] = new JsonArray(
                new JsonObject
                {
                    ["kind"] = "item_quantity",
                    ["itemRef"] = "antiseptic",
                    ["quantity"] = 1
                },
                new JsonObject
                {
                    ["kind"] = "item_quantity",
                    ["itemRef"] = "healing_dust",
                    ["quantity"] = 1
                })
        });

        AssertInvalidAt(
            Parse(nestedAlternative),
            Path + ".treatment.routes[0].requirements[0].kind",
            "wound_materialization_invalid_field");
    }

    [Fact]
    public void Parse_SeparateCompleteRoutesAreAlternativesButInvalidSiblingFailsWholeWound()
    {
        var wound = WoundContractTestData.CreateActiveWound();
        var alternative = CreateAlternativeProcedureRoute(
            wound,
            "resonant_dust_ritual",
            "Погасить ожог резонансной пылью",
            "resonant_crystal_dust",
            "crystal_healing");
        wound["treatment"]!["routes"]!.AsArray().Add(alternative);
        wound["treatment"]!["knownRouteIds"]!.AsArray().Add("resonant_dust_ritual");

        var complete = Parse(wound);

        Assert.True(complete.IsValid, DescribeIssues(complete));
        Assert.Equal(2, complete.Wound!.Treatment.Routes.Count);

        wound["treatment"]!["routes"]![1]!["outcomes"]![0]!["result"]![0] =
            "change_owner";
        AssertInvalidAt(
            Parse(wound),
            Path + ".treatment.routes[1].outcomes[0].result[0]",
            "wound_materialization_invalid_field");
    }

    [Theory]
    [InlineData("course")]
    [InlineData("guaranteed")]
    public void Parse_ClosedModeCannotReuseAnotherModesResolutionShape(string mode)
    {
        var wound = WoundContractTestData.CreateActiveWound();
        Route(wound)["mode"] = mode;

        AssertInvalidAt(
            Parse(wound),
            Path + ".treatment.routes[0].resolution",
            "wound_materialization_invalid_field");
    }

    [Theory]
    [InlineData("improvised")]
    [InlineData("ritual")]
    [InlineData("Procedure")]
    [InlineData("")]
    public void Parse_RouteModeIsClosedAndOrdinal(string mode)
    {
        var wound = WoundContractTestData.CreateActiveWound();
        Route(wound)["mode"] = mode;

        AssertInvalidAt(
            Parse(wound),
            Path + ".treatment.routes[0].mode",
            "wound_materialization_invalid_field");
    }

    [Theory]
    [InlineData(
        "resource_policy",
        "wound.treatment.routes[0].resourcePolicy.directInventoryWrite")]
    [InlineData(
        "resolution",
        "wound.treatment.routes[0].resolution.gmOverride")]
    [InlineData(
        "outcome",
        "wound.treatment.routes[0].outcomes[0].freeFormResult")]
    public void Parse_ProcedureMechanicalObjectsAreClosed(
        string mutation,
        string expectedPath)
    {
        var wound = WoundContractTestData.CreateActiveWound();
        switch (mutation)
        {
            case "resource_policy":
                Route(wound)["resourcePolicy"]!["directInventoryWrite"] = true;
                break;
            case "resolution":
                Route(wound)["resolution"]!["gmOverride"] = "success";
                break;
            case "outcome":
                Route(wound)["outcomes"]![0]!["freeFormResult"] = "heal";
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null);
        }

        AssertInvalidAt(
            Parse(wound),
            expectedPath,
            "wound_materialization_unknown_field");
    }

    [Theory]
    [InlineData("instant_cure")]
    [InlineData("reduce_to_zero")]
    [InlineData("severity_v")]
    [InlineData("change_owner")]
    [InlineData("change_domain:spiritual")]
    [InlineData("reopen")]
    [InlineData("custom_cure_47")]
    public void Parse_OutcomeVocabularyRejectsUnboundedOrIdentityChangingResults(
        string result)
    {
        var wound = WoundContractTestData.CreateActiveWound();
        Route(wound)["outcomes"]![0]!["result"]![0] = result;

        AssertInvalidAt(
            Parse(wound),
            Path + ".treatment.routes[0].outcomes[0].result[0]",
            "wound_materialization_invalid_field");
    }

    [Fact]
    public void Parse_CrossSettingNamesAndSemanticRefsRequireNoTreatmentCatalog()
    {
        const string postWoundType = "Заражённый порез ржавым листом";
        const string postSymptom = "запах машинного масла из раны";
        const string postRouteId = "wasteland_debridement_77";
        const string postRouteName = "Промыть самогоном и иссечь заражённые края";
        const string postItemRef = "moonshine_batch_kappa";
        const string postCapabilityRef = "scrap_camp_surgery";
        var postApocalyptic = CreateSettingSpecificWound(
            postWoundType,
            postSymptom,
            postRouteId,
            postRouteName,
            postItemRef,
            postCapabilityRef);
        const string magicWoundType = "Ожог обратным свечением аметистовой жилы";
        const string magicSymptom = "фиолетовые искры под кожей";
        const string magicRouteId = "amethyst_resonance_quenching";
        const string magicRouteName = "Заземлить свечение пылью немого кварца";
        const string magicItemRef = "silent_quartz_dust";
        const string magicCapabilityRef = "lattice_resonance_healing";
        var magical = CreateSettingSpecificWound(
            magicWoundType,
            magicSymptom,
            magicRouteId,
            magicRouteName,
            magicItemRef,
            magicCapabilityRef);

        var cases = new[]
        {
            (
                Wound: postApocalyptic,
                WoundType: postWoundType,
                Symptom: postSymptom,
                RouteId: postRouteId,
                RouteName: postRouteName,
                ItemRef: postItemRef,
                CapabilityRef: postCapabilityRef),
            (
                Wound: magical,
                WoundType: magicWoundType,
                Symptom: magicSymptom,
                RouteId: magicRouteId,
                RouteName: magicRouteName,
                ItemRef: magicItemRef,
                CapabilityRef: magicCapabilityRef)
        };
        foreach (var candidate in cases)
        {
            var result = Parse(candidate.Wound);

            Assert.True(result.IsValid, DescribeIssues(result));
            Assert.Equal(candidate.WoundType, result.Wound!.Classification.WoundType);
            Assert.Equal(
                new[] { candidate.Symptom },
                result.Wound.Display.VisibleSymptoms);
            var route = Assert.Single(result.Wound.Treatment.Routes);
            Assert.Equal(candidate.RouteId, route.RouteId);
            Assert.Equal(candidate.RouteName, route.DisplayName);
            Assert.Equal(
                candidate.ItemRef,
                route.Requirements[0].GetProperty("itemRef").GetString());
            Assert.Equal(
                candidate.CapabilityRef,
                route.Requirements[1].GetProperty("capabilityRef").GetString());
        }

        Route(magical)["outcomes"]![0]!["result"]![0] = "heal_by_name_lookup";
        AssertInvalidAt(
            Parse(magical),
            Path + ".treatment.routes[0].outcomes[0].result[0]",
            "wound_materialization_invalid_field");
    }

    [Fact]
    public void Parse_IdenticalWoundNamesDoNotSelectOrRewriteTreatmentRoutes()
    {
        const string sharedName = "Ожог неизвестного происхождения";
        var chemical = CreateSettingSpecificWound(
            sharedName,
            "едкий запах",
            "alkaline_neutralization",
            "Нейтрализовать щёлочь слабой кислотой",
            "weak_acid_solution",
            "chemical_first_aid");
        var magical = CreateSettingSpecificWound(
            sharedName,
            "сияющая сетка под кожей",
            "luminous_grounding",
            "Отвести сияние заземляющим кристаллом",
            "grounding_crystal",
            "luminous_trauma_ritual");

        var chemicalResult = Parse(chemical);
        var magicalResult = Parse(magical);
        Assert.True(chemicalResult.IsValid, DescribeIssues(chemicalResult));
        Assert.True(magicalResult.IsValid, DescribeIssues(magicalResult));
        var chemicalRoute = Assert.Single(chemicalResult.Wound!.Treatment.Routes);
        var magicalRoute = Assert.Single(magicalResult.Wound!.Treatment.Routes);

        Assert.Equal("alkaline_neutralization", chemicalRoute.RouteId);
        Assert.Equal("luminous_grounding", magicalRoute.RouteId);
        Assert.NotEqual(chemicalRoute.DisplayName, magicalRoute.DisplayName);
        Assert.NotEqual(
            chemicalRoute.Requirements[0].GetRawText(),
            magicalRoute.Requirements[0].GetRawText());
    }

    private static JsonObject CreateSettingSpecificWound(
        string woundType,
        string symptom,
        string routeId,
        string routeName,
        string itemRef,
        string capabilityRef)
    {
        var wound = WoundContractTestData.CreateActiveWound();
        wound["classification"]!["woundType"] = woundType;
        wound["display"]!["name"] = woundType;
        wound["display"]!["visibleSymptoms"] = new JsonArray(symptom);
        var route = Route(wound);
        route["routeId"] = routeId;
        route["displayName"] = routeName;
        route["requirements"]![0]!["itemRef"] = itemRef;
        route["requirements"]![1]!["capabilityRef"] = capabilityRef;
        wound["treatment"]!["knownRouteIds"] = new JsonArray(routeId);
        return wound;
    }

    private static JsonObject CreateAlternativeProcedureRoute(
        JsonObject wound,
        string routeId,
        string displayName,
        string itemRef,
        string capabilityRef)
    {
        var route = Route(wound).DeepClone().AsObject();
        route["routeId"] = routeId;
        route["displayName"] = displayName;
        route["requirements"]![0]!["itemRef"] = itemRef;
        route["requirements"]![1]!["capabilityRef"] = capabilityRef;
        return route;
    }

    private static JsonObject Route(JsonObject wound) =>
        wound["treatment"]!["routes"]![0]!.AsObject();

    private static WoundMaterializationParseResult Parse(JsonObject wound) =>
        WoundMaterializationContract.Parse(wound.ToJsonString(), Path);

    private static void AssertInvalidAt(
        WoundMaterializationParseResult result,
        string path,
        string code)
    {
        Assert.False(result.IsValid);
        Assert.Null(result.Wound);
        Assert.Contains(result.Issues, issue =>
            issue.FilePath == path && issue.Code == code);
    }

    private static string DescribeIssues(WoundMaterializationParseResult result) =>
        string.Join(
            Environment.NewLine,
            result.Issues.Select(issue =>
                $"{issue.Code} {issue.FilePath}: {issue.Expected} / {issue.Actual}"));
}
