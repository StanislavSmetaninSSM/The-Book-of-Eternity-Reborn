using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class MortalWoundDiagnosisTests
{
    private const string Path = "wound";
    private static readonly JsonSerializerOptions ReadableJson = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [Fact]
    public void Parse_ReachableHiddenRoutePreservesReadableDiagnosisGraph()
    {
        var wound = CreateReachableHiddenRouteWound();

        var result = Parse(wound);

        Assert.True(result.IsValid, DescribeIssues(result));
        var canonical = JsonNode.Parse(
            WoundMaterializationContract.SerializeCanonical(result.Wound!))!.AsObject();
        var diagnosis = Assert.IsType<JsonObject>(
            canonical["treatment"]!["diagnosisPaths"]![0]);
        Assert.Equal(
            "Осмотреть края раны при ярком свете",
            diagnosis["displayName"]!.GetValue<string>());
        Assert.Equal(
            new[] { "route:clean_and_suture" },
            diagnosis["requiresKnownFacts"]!.AsArray()
                .Select(static value => value!.GetValue<string>()));
        Assert.Equal(
            new[] { "route:hidden_antiseptic_course" },
            diagnosis["reveals"]!.AsArray()
                .Select(static value => value!.GetValue<string>()));
        Assert.DoesNotContain(
            "hidden_antiseptic_course",
            result.Wound!.Treatment.KnownRouteIds);
    }

    [Fact]
    public void Parse_PreservesAuthoredKnownFactAndRevealOrder()
    {
        var wound = WoundContractTestData.CreateActiveWound();
        AddVisibleKnownRoute(wound, "known_seed_zeta");
        AddHiddenRoute(wound, "hidden_route_zeta");
        AddHiddenRoute(wound, "hidden_route_alpha");
        DiagnosisPaths(wound).Add(CreateDiagnosisPath(
            "diagnosis_nonlexical_order",
            "Сопоставить признаки в авторском порядке",
            "hidden",
            new[] { "route:known_seed_zeta", "route:clean_and_suture" },
            new[] { "route:hidden_route_zeta", "route:hidden_route_alpha" }));

        var result = Parse(wound);

        Assert.True(result.IsValid, DescribeIssues(result));
        var canonical = JsonNode.Parse(
            WoundMaterializationContract.SerializeCanonical(result.Wound!))!.AsObject();
        var path = canonical["treatment"]!["diagnosisPaths"]![0]!;
        Assert.Equal(
            new[] { "route:known_seed_zeta", "route:clean_and_suture" },
            path["requiresKnownFacts"]!.AsArray()
                .Select(static value => value!.GetValue<string>()));
        Assert.Equal(
            new[] { "route:hidden_route_zeta", "route:hidden_route_alpha" },
            path["reveals"]!.AsArray()
                .Select(static value => value!.GetValue<string>()));
    }

    [Fact]
    public void Parse_SeededDiagnosisChainReachesEveryHiddenRoute()
    {
        var wound = WoundContractTestData.CreateActiveWound();
        AddHiddenRoute(wound, "hidden_route_alpha");
        AddHiddenRoute(wound, "hidden_route_beta");
        DiagnosisPaths(wound).Add(CreateDiagnosisPath(
            "diagnosis_alpha",
            "Проверить первичный признак",
            "known_to_player",
            new[] { "route:clean_and_suture" },
            new[] { "route:hidden_route_alpha" }));
        DiagnosisPaths(wound).Add(CreateDiagnosisPath(
            "diagnosis_beta",
            "Сопоставить вторичный признак",
            "hidden",
            new[] { "route:hidden_route_alpha" },
            new[] { "route:hidden_route_beta" }));

        var result = Parse(wound);

        Assert.True(result.IsValid, DescribeIssues(result));
        Assert.Equal(2, result.Wound!.Treatment.DiagnosisPaths.Count);
        Assert.Equal(
            new[] { "clean_and_suture" },
            result.Wound.Treatment.KnownRouteIds);
    }

    [Fact]
    public void Parse_ComplicationFactsSeedAndAdvanceDiscoveryWithoutReordering()
    {
        var wound = WoundContractTestData.CreateActiveWound();
        AddHiddenRoute(wound, "hidden_route_from_complication");
        Complications(wound).Add(CreateComplication(
            "complication_visible_swelling",
            "known_to_player"));
        Complications(wound).Add(CreateComplication(
            "complication_hidden_infection",
            "hidden"));
        DiagnosisPaths(wound).Add(CreateDiagnosisPath(
            "diagnosis_complication_zeta",
            "Сопоставить видимый отёк",
            "known_to_player",
            new[] { "complication:complication_visible_swelling" },
            new[] { "complication:complication_hidden_infection" }));
        DiagnosisPaths(wound).Add(CreateDiagnosisPath(
            "diagnosis_complication_alpha",
            "Проверить скрытый признак заражения",
            "hidden",
            new[] { "complication:complication_hidden_infection" },
            new[] { "route:hidden_route_from_complication" }));

        var result = Parse(wound);

        Assert.True(result.IsValid, DescribeIssues(result));
        Assert.Equal(
            new[] { "diagnosis_complication_zeta", "diagnosis_complication_alpha" },
            result.Wound!.Treatment.DiagnosisPaths.Select(
                static value => value.DiagnosisPathId));
        Assert.Equal(
            new[]
            {
                "complication:complication_hidden_infection",
                "route:hidden_route_from_complication"
            },
            result.Wound.Treatment.DiagnosisPaths.SelectMany(
                static value => value.Reveals));
        Assert.Equal(
            new[] { "known_to_player", "hidden" },
            result.Wound.Complications.Select(static value => value.Visibility));
    }

    [Theory]
    [InlineData("missing_path")]
    [InlineData("wrong_reveal")]
    [InlineData("second_route_uncovered")]
    [InlineData("gm_only_path")]
    public void Parse_EveryHiddenRouteRequiresAPlayerReachableRevealingPath(
        string mutation)
    {
        var wound = WoundContractTestData.CreateActiveWound();
        AddHiddenRoute(wound, "hidden_route_target");
        switch (mutation)
        {
            case "missing_path":
                break;
            case "wrong_reveal":
                AddHiddenRoute(wound, "hidden_route_other");
                DiagnosisPaths(wound).Add(CreateDiagnosisPath(
                    "diagnosis_wrong_reveal",
                    "Искать другой признак",
                    "known_to_player",
                    Array.Empty<string>(),
                    new[] { "route:hidden_route_other" }));
                break;
            case "second_route_uncovered":
                AddHiddenRoute(wound, "hidden_route_uncovered");
                DiagnosisPaths(wound).Add(CreateDiagnosisPath(
                    "diagnosis_target_only",
                    "Найти только первый путь",
                    "known_to_player",
                    Array.Empty<string>(),
                    new[] { "route:hidden_route_target" }));
                break;
            case "gm_only_path":
                DiagnosisPaths(wound).Add(CreateDiagnosisPath(
                    "diagnosis_private_only",
                    "Скрытая догадка ГМ",
                    "gm_only",
                    Array.Empty<string>(),
                    new[] { "route:hidden_route_target" }));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null);
        }

        AssertInvalidAt(
            Parse(wound),
            RoutePath(
                wound,
                mutation == "second_route_uncovered"
                    ? "hidden_route_uncovered"
                    : "hidden_route_target") + ".routeId",
            "wound_treatment_hidden_route_undiscoverable");
    }

    [Fact]
    public void Parse_HiddenDiagnosisPathRequiresAtLeastOneKnownFactPrerequisite()
    {
        var wound = WoundContractTestData.CreateActiveWound();
        AddHiddenRoute(wound, "hidden_route_target");
        DiagnosisPaths(wound).Add(CreateDiagnosisPath(
            "diagnosis_hidden_without_seed",
            "Скрытая проверка без доступной зацепки",
            "hidden",
            Array.Empty<string>(),
            new[] { "route:hidden_route_target" }));

        AssertInvalidAt(
            Parse(wound),
            Path + ".treatment.diagnosisPaths[0].requiresKnownFacts",
            "wound_treatment_diagnosis_path_unreachable");
    }

    [Theory]
    [InlineData("two_path_cycle")]
    [InlineData("self_cycle")]
    [InlineData("cycle_with_unrevealed_external_prerequisite")]
    public void Parse_UnseededDiagnosisCyclesFailClosed(string mutation)
    {
        var wound = WoundContractTestData.CreateActiveWound();
        AddHiddenRoute(wound, "hidden_route_alpha");
        if (mutation == "self_cycle")
        {
            DiagnosisPaths(wound).Add(CreateDiagnosisPath(
                "diagnosis_self_cycle",
                "Замкнутая самопроверка",
                "known_to_player",
                new[] { "route:hidden_route_alpha" },
                new[] { "route:hidden_route_alpha" }));
        }
        else
        {
            AddHiddenRoute(wound, "hidden_route_beta");
            if (mutation == "cycle_with_unrevealed_external_prerequisite")
                AddHiddenRoute(wound, "hidden_route_orphan");
            DiagnosisPaths(wound).Add(CreateDiagnosisPath(
                "diagnosis_alpha_cycle",
                "Путь к альфе через бету",
                "known_to_player",
                mutation == "cycle_with_unrevealed_external_prerequisite"
                    ? new[] { "route:hidden_route_beta", "route:hidden_route_orphan" }
                    : new[] { "route:hidden_route_beta" },
                new[] { "route:hidden_route_alpha" }));
            DiagnosisPaths(wound).Add(CreateDiagnosisPath(
                "diagnosis_beta_cycle",
                "Путь к бете через альфу",
                "known_to_player",
                new[] { "route:hidden_route_alpha" },
                new[] { "route:hidden_route_beta" }));
        }

        AssertInvalidAt(
            Parse(wound),
            Path + ".treatment.diagnosisPaths",
            "wound_treatment_discovery_cycle");
    }

    [Theory]
    [InlineData("requires_route", "route:missing_route", "requiresKnownFacts")]
    [InlineData("requires_complication", "complication:missing_complication", "requiresKnownFacts")]
    [InlineData("reveals_route", "route:missing_route", "reveals")]
    [InlineData("reveals_complication", "complication:missing_complication", "reveals")]
    [InlineData("free_form", "hidden cure by name", "reveals")]
    public void Parse_DiagnosisFactsMustBeTypedAndResolveInsideTheSameWound(
        string mutation,
        string fact,
        string field)
    {
        var wound = WoundContractTestData.CreateActiveWound();
        AddHiddenRoute(wound, "hidden_route_target");
        var prerequisites = mutation.StartsWith("requires_", StringComparison.Ordinal)
            ? new[] { fact }
            : Array.Empty<string>();
        var reveals = field == "reveals"
            ? new[] { fact }
            : new[] { "route:hidden_route_target" };
        DiagnosisPaths(wound).Add(CreateDiagnosisPath(
            "diagnosis_invalid_fact",
            "Проверить недопустимую зацепку",
            "known_to_player",
            prerequisites,
            reveals));

        AssertInvalidAt(
            Parse(wound),
            $"{Path}.treatment.diagnosisPaths[0].{field}[0]",
            "wound_treatment_diagnosis_fact_unknown");
    }

    [Theory]
    [InlineData("gm_only_route", "wound.treatment.routes[0].visibility", "wound_treatment_route_visibility_invalid")]
    [InlineData("unknown_known", "wound.treatment.knownRouteIds[0]", "wound_treatment_route_state_invalid")]
    [InlineData("unknown_completed", "wound.treatment.completedRouteIds[0]", "wound_treatment_route_state_invalid")]
    [InlineData("completed_not_known", "wound.treatment.completedRouteIds[0]", "wound_treatment_route_state_invalid")]
    [InlineData("visible_not_known", "wound.treatment.knownRouteIds", "wound_treatment_route_state_invalid")]
    public void Parse_MortalRouteVisibilityAndKnownCompletedSetsMustAgree(
        string mutation,
        string expectedPath,
        string expectedCode)
    {
        var wound = WoundContractTestData.CreateActiveWound();
        switch (mutation)
        {
            case "gm_only_route":
                Routes(wound)[0]!["visibility"] = "gm_only";
                break;
            case "unknown_known":
                Treatment(wound)["knownRouteIds"] = new JsonArray("missing_route");
                break;
            case "unknown_completed":
                Treatment(wound)["completedRouteIds"] = new JsonArray("missing_route");
                break;
            case "completed_not_known":
                Treatment(wound)["knownRouteIds"] = new JsonArray();
                Treatment(wound)["completedRouteIds"] = new JsonArray("clean_and_suture");
                break;
            case "visible_not_known":
                Treatment(wound)["knownRouteIds"] = new JsonArray();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null);
        }

        AssertInvalidAt(Parse(wound), expectedPath, expectedCode);
    }

    [Fact]
    public void Parse_CompletedRouteMayRemainKnownWithoutChangingItsAuthoredVisibility()
    {
        var wound = WoundContractTestData.CreateActiveWound();
        Treatment(wound)["completedRouteIds"] = new JsonArray("clean_and_suture");

        var result = Parse(wound);

        Assert.True(result.IsValid, DescribeIssues(result));
        Assert.Equal(
            new[] { "clean_and_suture" },
            result.Wound!.Treatment.CompletedRouteIds);
    }

    [Fact]
    public void Parse_DiagnosedHiddenRouteRemainsHiddenWhileKnown()
    {
        var wound = CreateReachableHiddenRouteWound();
        KnownRouteIds(wound).Add("hidden_antiseptic_course");

        var result = Parse(wound);

        Assert.True(result.IsValid, DescribeIssues(result));
        var route = Assert.Single(
            result.Wound!.Treatment.Routes,
            value => value.RouteId == "hidden_antiseptic_course");
        Assert.Equal("hidden", route.Visibility);
        Assert.Contains("hidden_antiseptic_course", result.Wound.Treatment.KnownRouteIds);
    }

    [Fact]
    public void Parse_PublicRouteIsKnownWithoutRewritingVisibility()
    {
        var wound = WoundContractTestData.CreateActiveWound();
        Routes(wound)[0]!["visibility"] = "public";

        var result = Parse(wound);

        Assert.True(result.IsValid, DescribeIssues(result));
        Assert.Equal("public", Assert.Single(result.Wound!.Treatment.Routes).Visibility);
        Assert.Equal(
            new[] { "clean_and_suture" },
            result.Wound.Treatment.KnownRouteIds);
    }

    [Fact]
    public void Parse_KnownFactPrerequisiteBoundaryAcceptsSixteenAndRejectsSeventeen()
    {
        var valid = WoundContractTestData.CreateActiveWound();
        var knownFacts = new List<string> { "route:clean_and_suture" };
        for (var index = 1; index < 16; index++)
        {
            var routeId = $"known_seed_route_{index:D2}";
            AddVisibleKnownRoute(valid, routeId);
            knownFacts.Add("route:" + routeId);
        }
        AddHiddenRoute(valid, "hidden_route_target");
        DiagnosisPaths(valid).Add(CreateDiagnosisPath(
            "diagnosis_sixteen_prerequisites",
            "Сопоставить шестнадцать известных признаков",
            "hidden",
            knownFacts,
            new[] { "route:hidden_route_target" }));

        var accepted = Parse(valid);
        Assert.True(accepted.IsValid, DescribeIssues(accepted));

        var overflow = valid.DeepClone().AsObject();
        AddVisibleKnownRoute(overflow, "known_seed_route_16");
        DiagnosisPaths(overflow)[0]!["requiresKnownFacts"]!.AsArray()
            .Add("route:known_seed_route_16");

        AssertInvalidAt(
            Parse(overflow),
            Path + ".treatment.diagnosisPaths[0].requiresKnownFacts",
            "wound_materialization_limit_exceeded");
    }

    [Fact]
    public void Parse_DeclaredRevealBoundaryAcceptsSixteenAndRejectsSeventeen()
    {
        var valid = WoundContractTestData.CreateActiveWound();
        var reveals = new List<string>();
        for (var index = 0; index < 16; index++)
        {
            var routeId = $"hidden_reveal_route_{index:D2}";
            AddHiddenRoute(valid, routeId);
            reveals.Add("route:" + routeId);
        }
        DiagnosisPaths(valid).Add(CreateDiagnosisPath(
            "diagnosis_sixteen_reveals",
            "Открыть шестнадцать вариантов лечения",
            "known_to_player",
            Array.Empty<string>(),
            reveals));

        var accepted = Parse(valid);
        Assert.True(accepted.IsValid, DescribeIssues(accepted));

        var overflow = valid.DeepClone().AsObject();
        AddHiddenRoute(overflow, "hidden_reveal_route_16");
        DiagnosisPaths(overflow)[0]!["reveals"]!.AsArray()
            .Add("route:hidden_reveal_route_16");

        AssertInvalidAt(
            Parse(overflow),
            Path + ".treatment.diagnosisPaths[0].reveals",
            "wound_materialization_limit_exceeded");
    }

    [Theory]
    [InlineData("requiresKnownFacts")]
    [InlineData("reveals")]
    public void Parse_DiagnosisFactCollectionsRejectDuplicates(string field)
    {
        var wound = CreateReachableHiddenRouteWound();
        var array = DiagnosisPaths(wound)[0]![field]!.AsArray();
        array.Add(array[0]!.GetValue<string>());

        AssertInvalidAt(
            Parse(wound),
            $"{Path}.treatment.diagnosisPaths[0].{field}[1]",
            "wound_materialization_duplicate_identifier");
    }

    [Theory]
    [InlineData("missing_display_name", "wound.treatment.diagnosisPaths[0].displayName", "wound_materialization_missing_field")]
    [InlineData("empty_display_name", "wound.treatment.diagnosisPaths[0].displayName", "wound_materialization_invalid_field")]
    [InlineData("missing_prerequisites", "wound.treatment.diagnosisPaths[0].requiresKnownFacts", "wound_materialization_missing_field")]
    [InlineData("unknown_failure_policy", "wound.treatment.diagnosisPaths[0].failurePolicy", "wound_materialization_invalid_field")]
    public void Parse_DiagnosisPathRequiresReadableClosedVersionOneShape(
        string mutation,
        string expectedPath,
        string expectedCode)
    {
        var wound = CreateReachableHiddenRouteWound();
        var path = DiagnosisPaths(wound)[0]!.AsObject();
        switch (mutation)
        {
            case "missing_display_name":
                path.Remove("displayName");
                break;
            case "empty_display_name":
                path["displayName"] = "   ";
                break;
            case "missing_prerequisites":
                path.Remove("requiresKnownFacts");
                break;
            case "unknown_failure_policy":
                path["failurePolicy"] = "partial_hint";
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null);
        }

        AssertInvalidAt(Parse(wound), expectedPath, expectedCode);
    }

    [Theory]
    [InlineData("success")]
    [InlineData("failure")]
    public void CommandParsing_AcceptsExactTypedDiagnosisSuccessOrFailure(string resultKind)
    {
        var revealedFacts = resultKind == "success"
            ? new[] { "route:hidden_antiseptic_course" }
            : Array.Empty<string>();
        var root = CreateAcceptedDiagnosisCommandRoot(resultKind, revealedFacts);

        var parsed = WoundResponseInputComposer.ParseCommandRoot(
            JsonSerializer.SerializeToElement(root));

        Assert.True(parsed.Success, DescribeIssues(parsed.Issues));
        Assert.True(JsonNode.DeepEquals(root, parsed.CommandRoot));
    }

    [Theory]
    [InlineData("success_empty", "result.revealedFacts", "wound_command_invalid_field")]
    [InlineData("failure_reveals", "result.revealedFacts", "wound_command_invalid_field")]
    [InlineData("unknown_result", "result.result", "wound_command_invalid_field")]
    [InlineData("missing_attempt", "authority.attemptId", "wound_command_missing_field")]
    [InlineData("operation_mismatch", "authority.operationKey", "wound_command_invalid_field")]
    [InlineData("unknown_authority_field", "authority.gmOverride", "wound_command_unknown_field")]
    public void CommandParsing_RejectsAmbiguousOrUnboundDiagnosisResult(
        string mutation,
        string relativePath,
        string expectedCode)
    {
        var root = CreateAcceptedDiagnosisCommandRoot(
            "success",
            new[] { "route:hidden_antiseptic_course" });
        var command = root["commands"]![0]!.AsObject();
        var authority = command["authority"]!.AsObject();
        var result = command["result"]!.AsObject();
        switch (mutation)
        {
            case "success_empty":
                result["revealedFacts"] = new JsonArray();
                break;
            case "failure_reveals":
                result["result"] = "failure";
                break;
            case "unknown_result":
                result["result"] = "partial";
                break;
            case "missing_attempt":
                authority.Remove("attemptId");
                break;
            case "operation_mismatch":
                authority["operationKey"] = "diagnosis_operation_other";
                break;
            case "unknown_authority_field":
                authority["gmOverride"] = true;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null);
        }

        var parsed = WoundResponseInputComposer.ParseCommandRoot(
            JsonSerializer.SerializeToElement(root));

        Assert.False(parsed.Success);
        Assert.Contains(parsed.Issues, issue =>
            issue.FilePath == CommandPath(relativePath) && issue.Code == expectedCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CommandParsing_AcceptsOnlyAuthoredAlternativeTreatmentTransition(
        bool hidden)
    {
        var root = CreateAcceptedAlternativeCommandRoot(hidden);

        var parsed = WoundResponseInputComposer.ParseCommandRoot(
            JsonSerializer.SerializeToElement(root));

        Assert.True(parsed.Success, DescribeIssues(parsed.Issues));
        Assert.True(JsonNode.DeepEquals(root, parsed.CommandRoot));
    }

    [Theory]
    [InlineData("hidden_without_path", "result.diagnosisPath", "wound_command_invalid_field")]
    [InlineData("visible_with_path", "result.diagnosisPath", "wound_command_invalid_field")]
    [InlineData("route_id_mismatch", "authority.addedRouteId", "wound_command_invalid_field")]
    [InlineData("path_id_mismatch", "authority.addedDiagnosisPathId", "wound_command_invalid_field")]
    [InlineData("unknown_result_field", "result.gmOverride", "wound_command_unknown_field")]
    [InlineData("unknown_authority_field", "authority.gmOverride", "wound_command_unknown_field")]
    public void CommandParsing_RejectsAlternativeShapeOrBindingMismatch(
        string mutation,
        string relativePath,
        string expectedCode)
    {
        var hidden = mutation is "hidden_without_path" or "visible_with_path" or
            "path_id_mismatch";
        var root = CreateAcceptedAlternativeCommandRoot(hidden);
        var command = root["commands"]![0]!.AsObject();
        var authority = command["authority"]!.AsObject();
        var result = command["result"]!.AsObject();
        switch (mutation)
        {
            case "hidden_without_path":
                authority["addedDiagnosisPathId"] = null;
                authority["diagnosisPathFingerprint"] = null;
                result["diagnosisPath"] = null;
                break;
            case "visible_with_path":
                result["route"]!["visibility"] = "known_to_player";
                break;
            case "route_id_mismatch":
                authority["addedRouteId"] = "alternative_other_route";
                break;
            case "path_id_mismatch":
                authority["addedDiagnosisPathId"] = "diagnosis_other_path";
                break;
            case "unknown_result_field":
                result["gmOverride"] = true;
                break;
            case "unknown_authority_field":
                authority["gmOverride"] = true;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null);
        }

        var parsed = WoundResponseInputComposer.ParseCommandRoot(
            JsonSerializer.SerializeToElement(root));

        Assert.False(parsed.Success);
        Assert.Contains(parsed.Issues, issue =>
            issue.FilePath == CommandPath(relativePath) && issue.Code == expectedCode);
    }

    [Fact]
    public void CommandParsing_DeclinedAlternativeCreatesNoAcceptedTransitionCommand()
    {
        var root = CreateAcceptedAlternativeCommandRoot(hidden: false);
        var result = root["commands"]![0]!["result"]!.AsObject();
        result["decision"] = "decline";
        result["route"] = null;
        result["diagnosisPath"] = null;

        var parsed = WoundResponseInputComposer.ParseCommandRoot(
            JsonSerializer.SerializeToElement(root));

        Assert.False(parsed.Success);
        Assert.Contains(parsed.Issues, issue =>
            issue.FilePath == CommandPath("result.decision") &&
            issue.Code == "wound_command_invalid_field");
    }

    [Theory]
    [InlineData("diagnose")]
    [InlineData("author_alternative_treatment")]
    [InlineData("author_alternative_treatment_hidden")]
    public void CommandRecomposition_AcceptsProductionSealAndRejectsPostSealTampering(
        string transitionKind)
    {
        WoundTransitionRequest request;
        WoundAcceptedTurnBinding binding;
        if (string.Equals(transitionKind, "diagnose", StringComparison.Ordinal))
        {
            var roots = CreateDiagnosisReducerRoots("success");
            request = CreateDiagnosisTransitionRequest(
                roots.Before,
                roots.After,
                "success");
            binding = CreateTreatmentBinding("turn_43:diagnosis_attempt", turn: 43);
        }
        else
        {
            var hidden = string.Equals(
                transitionKind,
                "author_alternative_treatment_hidden",
                StringComparison.Ordinal);
            var roots = CreateAlternativeReducerRoots(hidden);
            request = CreateAlternativeTransitionRequest(
                roots.Before,
                roots.After,
                hidden);
            binding = CreateTreatmentBinding("turn_44:alternative_evidence", turn: 44);
        }

        var commandRoot = ComposeAcceptedTransitionCommandRoot(
            binding,
            request,
            "Результат принят и связан с раной.");
        var parsed = WoundResponseInputComposer.ParseCommandRoot(
            JsonSerializer.SerializeToElement(commandRoot));
        Assert.True(parsed.Success, DescribeIssues(parsed.Issues));
        var exact = WoundResponseInputComposer.RecomposeCommandRoot(
            binding,
            parsed,
            Array.Empty<WoundOpportunityDecisionReceipt>());
        Assert.True(exact.Success, DescribeIssues(exact.Issues));
        Assert.True(JsonNode.DeepEquals(commandRoot, exact.CommandRoot));

        var tamperedRoot = commandRoot.DeepClone().AsObject();
        var tamperedCommand = tamperedRoot["commands"]![0]!.AsObject();
        if (string.Equals(transitionKind, "diagnose", StringComparison.Ordinal))
        {
            tamperedCommand["authority"]!["pathFingerprint"] = Fingerprint('0');
        }
        else if (string.Equals(
                     transitionKind,
                     "author_alternative_treatment_hidden",
                     StringComparison.Ordinal))
        {
            tamperedCommand["result"]!["diagnosisPath"]!["displayName"] =
                "Подменённая после запечатывания диагностика";
        }
        else
        {
            tamperedCommand["result"]!["route"]!["displayName"] =
                "Подменённый после запечатывания маршрут";
        }

        var tamperedParsed = WoundResponseInputComposer.ParseCommandRoot(
            JsonSerializer.SerializeToElement(tamperedRoot));
        if (!tamperedParsed.Success)
        {
            Assert.Contains(tamperedParsed.Issues, issue =>
                issue.Code?.StartsWith("wound_command_", StringComparison.Ordinal) == true);
            return;
        }

        var tampered = WoundResponseInputComposer.RecomposeCommandRoot(
            binding,
            tamperedParsed,
            Array.Empty<WoundOpportunityDecisionReceipt>());
        Assert.False(tampered.Success);
        Assert.Contains(tampered.Issues, issue =>
            issue.Code == "wound_command_recomposition_mismatch");
    }

    [Fact]
    public void GameResponse_RoundTripsAlternativeTreatmentAuthoringWithoutInternalAuthority()
    {
        var source = new JsonObject
        {
            ["response"] = "Новая возможность лечения становится понятна.",
            ["woundTreatmentAuthorings"] = new JsonArray(new JsonObject
            {
                ["authoringRequestRef"] = "authoring_request_public_001",
                ["decision"] = "author",
                ["route"] = CreateRouteNode(
                    "alternative_visible_route",
                    "known_to_player"),
                ["diagnosisPath"] = null
            })
        };

        var response = JsonSerializer.Deserialize<GameResponse>(source.ToJsonString());
        var roundTrip = JsonSerializer.SerializeToNode(response)!.AsObject();

        Assert.NotNull(roundTrip["woundTreatmentAuthorings"]);
        Assert.Null(roundTrip["woundTreatmentAuthorings"]![0]!["authorityFingerprint"]);
        Assert.Equal(
            "authoring_request_public_001",
            roundTrip["woundTreatmentAuthorings"]![0]!["authoringRequestRef"]!
                .GetValue<string>());
    }

    [Theory]
    [InlineData("success")]
    [InlineData("failure")]
    public void History_DiagnosisResultRemainsDurableWhenTheWoundStateDoesNotChange(
        string resultKind)
    {
        var historyRoot = CreateDiagnosisHistory(
            resultKind,
            resultKind == "success"
                ? new[] { "route:hidden_antiseptic_course" }
                : Array.Empty<string>());

        var parsed = WoundHistoryState.Parse(
            historyRoot.ToJsonString(),
            WoundHistoryState.HistoryPath);

        Assert.True(parsed.IsValid, DescribeIssues(parsed.Issues));
        var canonical = JsonNode.Parse(
            WoundHistoryState.SerializeCanonical(parsed.State!))!.AsObject();
        var result = canonical["transitions"]![1]!["transitionResult"]!.AsObject();
        Assert.Equal(resultKind, result["result"]!.GetValue<string>());
        Assert.Equal(
            resultKind == "failure" ? Fingerprint('a') : Fingerprint('b'),
            canonical["transitions"]![1]!["afterFingerprint"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("missing_result", "transitionResult", "wound_history_missing_field")]
    [InlineData("failure_reveals", "transitionResult.revealedFacts", "wound_history_invalid_field")]
    [InlineData("success_empty", "transitionResult.revealedFacts", "wound_history_invalid_field")]
    [InlineData("kind_mismatch", "transitionResult.kind", "wound_history_invalid_field")]
    [InlineData("bad_fingerprint", "transitionResult.resultFingerprint", "wound_history_invalid_fingerprint")]
    [InlineData("mismatched_fingerprint", "transitionResult.resultFingerprint", "wound_history_result_fingerprint_mismatch")]
    public void History_RejectsAmbiguousOrUnsealedDiagnosisResult(
        string mutation,
        string relativePath,
        string expectedCode)
    {
        var history = CreateDiagnosisHistory(
            "failure",
            Array.Empty<string>());
        var transition = history["transitions"]![1]!.AsObject();
        var result = transition["transitionResult"]!.AsObject();
        switch (mutation)
        {
            case "missing_result":
                transition.Remove("transitionResult");
                break;
            case "failure_reveals":
                result["revealedFacts"] = new JsonArray("route:hidden_antiseptic_course");
                break;
            case "success_empty":
                result["result"] = "success";
                break;
            case "kind_mismatch":
                result["kind"] = "author_alternative_treatment";
                break;
            case "bad_fingerprint":
                result["resultFingerprint"] = "not-a-fingerprint";
                break;
            case "mismatched_fingerprint":
                result["resultFingerprint"] = Fingerprint('e');
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null);
        }

        var parsed = WoundHistoryState.Parse(
            history.ToJsonString(),
            WoundHistoryState.HistoryPath);

        Assert.False(parsed.IsValid);
        Assert.Contains(parsed.Issues, issue =>
            issue.FilePath == HistoryPath(relativePath) && issue.Code == expectedCode);
    }

    [Theory]
    [InlineData("kind")]
    [InlineData("path")]
    [InlineData("facts")]
    public void History_ReplayComparesEveryTypedDiagnosisResultCoordinate(
        string mutation)
    {
        var acceptedResultKind = mutation == "facts" ? "success" : "failure";
        var acceptedFacts = mutation == "facts"
            ? new[] { "route:hidden_route_zeta" }
            : Array.Empty<string>();
        var acceptedState = ParseHistory(CreateDiagnosisHistory(
            acceptedResultKind,
            acceptedFacts,
            diagnosisPathId: "diagnosis_path_alpha"));
        var exactRow = Assert.Single(
            acceptedState.Transitions,
            static value => value.Kind == "diagnose");

        var exact = acceptedState.ResolveReplay(
            WoundHistoryState.CreateReplayProbe(exactRow));

        Assert.Equal(WoundHistoryReplayDisposition.Exact, exact.Disposition);
        var receipt = JsonSerializer.SerializeToNode(
            exact.AlreadyAcceptedReceipt,
            ReadableJson)!.AsObject();
        Assert.Equal(
            acceptedResultKind,
            receipt["transitionResult"]!["result"]!.GetValue<string>());
        Assert.Equal(
            acceptedFacts,
            receipt["transitionResult"]!["revealedFacts"]!.AsArray()
                .Select(static value => value!.GetValue<string>()));

        var changedHistory = CreateDiagnosisHistory(
            acceptedResultKind,
            acceptedFacts,
            diagnosisPathId: "diagnosis_path_alpha");
        var changedRowNode = changedHistory["transitions"]![1]!.AsObject();
        var changedResult = changedRowNode["transitionResult"]!.AsObject();
        switch (mutation)
        {
            case "kind":
                changedRowNode["kind"] = "author_alternative_treatment";
                changedRowNode["attemptId"] = null;
                changedResult = new JsonObject
                {
                    ["kind"] = "author_alternative_treatment",
                    ["authoringRequestRef"] = "authoring_request_public_001",
                    ["addedRouteId"] = "alternative_visible_route",
                    ["addedDiagnosisPathId"] = null,
                    ["routeFingerprint"] = Fingerprint('7'),
                    ["diagnosisPathFingerprint"] = null
                };
                changedRowNode["transitionResult"] = SealTransitionResult(changedResult);
                break;
            case "path":
                changedResult["diagnosisPathId"] = "diagnosis_path_beta";
                changedRowNode["transitionResult"] = SealTransitionResult(changedResult);
                break;
            case "facts":
                changedResult["revealedFacts"] = new JsonArray(
                    "route:hidden_route_alpha");
                changedRowNode["transitionResult"] = SealTransitionResult(changedResult);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null);
        }
        var changedState = ParseHistory(changedHistory);
        var changedRow = Assert.Single(
            changedState.Transitions,
            value => value.OperationKey == "diagnosis_operation_001");
        var conflict = acceptedState.ResolveReplay(
            WoundHistoryState.CreateReplayProbe(changedRow));

        Assert.Equal(WoundHistoryReplayDisposition.Conflict, conflict.Disposition);
        Assert.Contains(conflict.Issues, issue =>
            issue.Code == "wound_history_conflicting_replay");
    }

    [Fact]
    public void History_AlternativeAuthoringAppendsOnceAndReplaysExactResult()
    {
        var roots = CreateAlternativeReducerRoots(hidden: false);
        var reduction = ReduceAlternative(roots.Before, roots.After, hidden: false);
        Assert.True(reduction.IsValid, DescribeIssues(reduction.Issues));
        var intent = Assert.Single(
            reduction.Intents.OfType<WoundTransitionHistoryIntent>());
        var before = ParseHistory(CreateBaseHistory(roots.Before));
        var beforeCanonical = ParseCanonicalHistory(before);

        var appended = AppendHistory(
            before,
            intent,
            "Открыт новый способ лечения.");

        var canonical = ParseCanonicalHistory(appended);
        Assert.Equal(before.Transitions.Count + 1, appended.Transitions.Count);
        Assert.Equal(
            beforeCanonical["transitions"]![0]!.ToJsonString(ReadableJson),
            canonical["transitions"]![0]!.ToJsonString(ReadableJson));
        Assert.Equal(
            "alternative_visible_route",
            canonical["transitions"]![1]!["transitionResult"]!["addedRouteId"]!
                .GetValue<string>());
        var appendedRow = appended.Transitions[^1];
        var exact = appended.ResolveReplay(
            WoundHistoryState.CreateReplayProbe(appendedRow));
        Assert.Equal(WoundHistoryReplayDisposition.Exact, exact.Disposition);
        var receipt = JsonSerializer.SerializeToNode(
            exact.AlreadyAcceptedReceipt,
            ReadableJson)!.AsObject();
        Assert.Equal(
            "alternative_visible_route",
            receipt["transitionResult"]!["addedRouteId"]!.GetValue<string>());
        Assert.Equal(
            "authoring_request_public_001",
            receipt["transitionResult"]!["authoringRequestRef"]!.GetValue<string>());
        Assert.Null(receipt["transitionResult"]!["addedDiagnosisPathId"]);
    }

    [Theory]
    [InlineData("authoring_request_ref")]
    [InlineData("route_id")]
    [InlineData("diagnosis_path_id")]
    [InlineData("route_fingerprint")]
    [InlineData("diagnosis_path_fingerprint")]
    public void History_AlternativeReplayComparesEveryTypedResultCoordinate(
        string mutation)
    {
        var acceptedRoots = CreateAlternativeReducerRoots(hidden: true);
        var acceptedReduction = ReduceAlternative(
            acceptedRoots.Before,
            acceptedRoots.After,
            hidden: true);
        Assert.True(acceptedReduction.IsValid, DescribeIssues(acceptedReduction.Issues));
        var baseHistory = ParseHistory(CreateBaseHistory(acceptedRoots.Before));
        var accepted = AppendHistory(
            baseHistory,
            Assert.Single(acceptedReduction.Intents.OfType<WoundTransitionHistoryIntent>()),
            "Открыт скрытый способ лечения.");
        var acceptedCanonical = ParseCanonicalHistory(accepted);
        var candidateRoot = acceptedCanonical.DeepClone().AsObject();
        var candidateRowNode = candidateRoot["transitions"]![1]!.AsObject();
        var candidateResult = candidateRowNode["transitionResult"]!.AsObject();
        switch (mutation)
        {
            case "authoring_request_ref":
                candidateResult["authoringRequestRef"] = "authoring_request_other";
                break;
            case "route_id":
                candidateResult["addedRouteId"] = "alternative_hidden_route_other";
                break;
            case "diagnosis_path_id":
                candidateResult["addedDiagnosisPathId"] = "diagnosis_alternative_other";
                break;
            case "route_fingerprint":
                candidateResult["routeFingerprint"] = Fingerprint('0');
                break;
            case "diagnosis_path_fingerprint":
                candidateResult["diagnosisPathFingerprint"] = Fingerprint('2');
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null);
        }
        candidateRowNode["transitionResult"] = SealTransitionResult(candidateResult);
        var candidate = ParseHistory(candidateRoot);
        var candidateRow = candidate.Transitions[^1];

        var conflict = accepted.ResolveReplay(
            WoundHistoryState.CreateReplayProbe(candidateRow));

        Assert.Equal(WoundHistoryReplayDisposition.Conflict, conflict.Disposition);
        Assert.Equal(2, accepted.Transitions.Count);
        Assert.True(JsonNode.DeepEquals(
            acceptedCanonical,
            ParseCanonicalHistory(accepted)));
        Assert.Contains(conflict.Issues, issue =>
            issue.Code == "wound_history_conflicting_replay");
    }

    [Fact]
    public void Repair_AlternativeCandidateUsesItsOwnResponseShape()
    {
        var packet = Assert.Single(WoundRepairPacketBuilder.Build(
            CreateAlternativeRepairRequest(includeSensitiveInternals: false)));
        var json = packet.ToJsonObject();

        Assert.Equal(
            "author_alternative_treatment",
            json["candidateKind"]!.GetValue<string>());
        Assert.Null(json["requiredResponseShape"]!["woundDecisions"]);
        var authorings = json["requiredResponseShape"]!["woundTreatmentAuthorings"]!
            .AsArray();
        var authoring = Assert.IsType<JsonObject>(Assert.Single(authorings));
        Assert.Equal(
            "authoring_request_public_001",
            authoring["authoringRequestRef"]!.GetValue<string>());
        Assert.Equal("author", authoring["decision"]!.GetValue<string>());
        Assert.NotNull(authoring["route"]);
        Assert.NotNull(authoring["diagnosisPath"]);
        var preserved = json["preservedProposal"]!.AsObject();
        Assert.Equal(
            "alternative_hidden_route",
            preserved["route"]!["routeId"]!.GetValue<string>());
        Assert.Equal(
            "Маршрут alternative_hidden_route",
            preserved["route"]!["displayName"]!.GetValue<string>());
        Assert.Null(preserved["route"]!["mode"]);
        Assert.NotNull(preserved["route"]!["requirements"]);
    }

    [Fact]
    public void Repair_AlternativeCandidateRecursivelyOmitsInternalAuthority()
    {
        var packet = Assert.Single(WoundRepairPacketBuilder.Build(
            CreateAlternativeRepairRequest(includeSensitiveInternals: true)));
        var serialized = packet.ToJsonObject().ToJsonString(ReadableJson);
        var forbidden = new[]
        {
            "operationKey",
            "woundId",
            "ownerId",
            "carrierPath",
            "resourceSeal",
            "sourceSeal",
            "providerSeal",
            "expectedBeforeFingerprint",
            "expectedAfterFingerprint",
            "requestAuthorityFingerprint",
            "routeFingerprint",
            "diagnosisPathFingerprint",
            "evidenceAuthorityFingerprint",
            "requirementAuthorityFingerprint",
            "checkResultFingerprint",
            "resultFingerprint",
            "authorityFingerprint",
            "privateNpcData",
            "gmPrivateNotes"
        };
        foreach (var key in forbidden)
            Assert.DoesNotContain(key, serialized, StringComparison.OrdinalIgnoreCase);
        var forbiddenValues = new[]
        {
            "secret_operation_key_value",
            "secret_wound_id_value",
            "secret_owner_id_value",
            "secret_carrier_path_value",
            "secret_resource_seal_value",
            "secret_source_seal_value",
            "secret_provider_seal_value",
            "secret_private_npc_value",
            "secret_gm_notes_value",
            Fingerprint('0'),
            Fingerprint('1'),
            Fingerprint('2'),
            Fingerprint('3'),
            Fingerprint('4'),
            Fingerprint('5'),
            Fingerprint('6'),
            Fingerprint('7'),
            Fingerprint('8'),
            Fingerprint('9'),
            Fingerprint('c')
        };
        foreach (var value in forbiddenValues)
            Assert.DoesNotContain(value, serialized, StringComparison.Ordinal);
        Assert.Contains("authoring_request_public_001", serialized, StringComparison.Ordinal);
        Assert.Contains("Маршрут alternative_hidden_route", serialized, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("success")]
    [InlineData("failure")]
    public void Reduce_Diagnosis_SealsOneTerminalAttemptAndExactRevealBoundary(
        string resultKind)
    {
        var roots = CreateDiagnosisReducerRoots(resultKind);
        var request = CreateDiagnosisTransitionRequest(
            roots.Before,
            roots.After,
            resultKind);

        var result = WoundTransitionReducer.Reduce(request);

        Assert.True(result.IsValid, DescribeIssues(result.Issues));
        Assert.Equal(
            resultKind == "success"
                ? new[]
                {
                    "clean_and_suture",
                    "hidden_route_zeta",
                    "hidden_route_alpha"
                }
                : new[] { "clean_and_suture" },
            result.ProposedAfter!.Treatment.KnownRouteIds);
        AssertDiagnosisChangesOnlyKnownFactsAndTransition(
            roots.Before,
            result.ProposedAfter);
        Assert.Empty(result.Intents.OfType<WoundEffectTransitionIntent>());

        var terminalAttempt = Assert.Single(
            result.Intents.OfType<WoundAttemptTerminalIntent>());
        Assert.Equal("diagnosis_attempt_001", terminalAttempt.AttemptId);
        Assert.Equal("diagnosis_multi_fact", terminalAttempt.RouteOrGateRef);

        var historyIntent = Assert.Single(
            result.Intents.OfType<WoundTransitionHistoryIntent>());
        Assert.Equal("diagnose", historyIntent.Kind);
        Assert.Equal("diagnosis_attempt_001", historyIntent.AttemptId);
        var transitionResult = ReadTransitionResult(historyIntent);
        Assert.Equal("diagnose", transitionResult["kind"]!.GetValue<string>());
        Assert.Equal(
            "diagnosis_multi_fact",
            transitionResult["diagnosisPathId"]!.GetValue<string>());
        Assert.Equal(resultKind, transitionResult["result"]!.GetValue<string>());
        Assert.Equal(
            resultKind == "success"
                ? new[] { "route:hidden_route_zeta", "route:hidden_route_alpha" }
                : Array.Empty<string>(),
            transitionResult["revealedFacts"]!.AsArray()
                .Select(static value => value!.GetValue<string>()));
    }

    [Theory]
    [InlineData("success")]
    [InlineData("failure")]
    public void History_DiagnosisReducerIntentAppendsOnceAndReplaysExactResult(
        string resultKind)
    {
        var roots = CreateDiagnosisReducerRoots(resultKind);
        var reduction = WoundTransitionReducer.Reduce(
            CreateDiagnosisTransitionRequest(roots.Before, roots.After, resultKind));
        Assert.True(reduction.IsValid, DescribeIssues(reduction.Issues));
        var intent = Assert.Single(
            reduction.Intents.OfType<WoundTransitionHistoryIntent>());
        var beforeHistory = ParseHistory(CreateBaseHistory(roots.Before));
        var beforeCanonical = ParseCanonicalHistory(beforeHistory);

        var appended = AppendHistory(
            beforeHistory,
            intent,
            resultKind == "success"
                ? "Осмотр открыл два признака лечения."
                : "Осмотр не дал новых сведений.");

        var afterCanonical = ParseCanonicalHistory(appended);
        Assert.Equal(beforeHistory.Transitions.Count + 1, appended.Transitions.Count);
        Assert.Equal(
            beforeCanonical["transitions"]![0]!.ToJsonString(ReadableJson),
            afterCanonical["transitions"]![0]!.ToJsonString(ReadableJson));
        var appendedRow = appended.Transitions[^1];
        var exact = appended.ResolveReplay(
            WoundHistoryState.CreateReplayProbe(appendedRow));
        Assert.Equal(WoundHistoryReplayDisposition.Exact, exact.Disposition);
        Assert.Equal(beforeHistory.Transitions.Count + 1, appended.Transitions.Count);
        var receipt = JsonSerializer.SerializeToNode(
            exact.AlreadyAcceptedReceipt,
            ReadableJson)!.AsObject();
        Assert.Equal(
            resultKind,
            receipt["transitionResult"]!["result"]!.GetValue<string>());
        Assert.Equal(
            "diagnosis_multi_fact",
            receipt["transitionResult"]!["diagnosisPathId"]!.GetValue<string>());
        Assert.Equal(
            resultKind == "success" ? 2 : 0,
            receipt["transitionResult"]!["revealedFacts"]!.AsArray().Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Reduce_AuthorAlternative_AppendsExactlyOneBoundRouteAndOptionalPath(
        bool hidden)
    {
        var roots = CreateAlternativeReducerRoots(hidden);

        var result = ReduceAlternative(roots.Before, roots.After, hidden);

        Assert.True(result.IsValid, DescribeIssues(result.Issues));
        Assert.Equal(
            roots.Before["treatment"]!["routes"]!.AsArray().Count + 1,
            result.ProposedAfter!.Treatment.Routes.Count);
        Assert.Equal(
            roots.Before["treatment"]!["diagnosisPaths"]!.AsArray().Count +
            (hidden ? 1 : 0),
            result.ProposedAfter.Treatment.DiagnosisPaths.Count);
        Assert.Equal(
            hidden
                ? new[] { "clean_and_suture", "existing_visible_route" }
                : new[]
                {
                    "clean_and_suture",
                    "existing_visible_route",
                    "alternative_visible_route"
                },
            result.ProposedAfter.Treatment.KnownRouteIds);

        var historyIntent = Assert.Single(
            result.Intents.OfType<WoundTransitionHistoryIntent>());
        Assert.Equal("author_alternative_treatment", historyIntent.Kind);
        var transitionResult = ReadTransitionResult(historyIntent);
        Assert.Equal(
            hidden ? "alternative_hidden_route" : "alternative_visible_route",
            transitionResult["addedRouteId"]!.GetValue<string>());
        Assert.Equal(
            hidden ? "diagnosis_alternative_hidden" : null,
            transitionResult["addedDiagnosisPathId"]?.GetValue<string>());
    }

    [Theory]
    [InlineData("replace_existing")]
    [InlineData("reorder_existing")]
    [InlineData("delete_existing")]
    [InlineData("append_second_route")]
    [InlineData("append_visible_path")]
    [InlineData("rewrite_existing_path")]
    [InlineData("reorder_existing_paths")]
    [InlineData("delete_existing_path")]
    [InlineData("reorder_known")]
    [InlineData("rewrite_completed")]
    [InlineData("rewrite_unrelated_mechanics")]
    public void Reduce_AuthorAlternative_RejectsEveryNonAppendDelta(string mutation)
    {
        var roots = CreateAlternativeReducerRoots(hidden: false);
        var beforeTreatment = Treatment(roots.Before);
        var afterTreatment = Treatment(roots.After);
        switch (mutation)
        {
            case "replace_existing":
                Routes(roots.After)[0]!["displayName"] =
                    "Подменённый старый маршрут";
                break;
            case "reorder_existing":
                afterTreatment["routes"] = new JsonArray(
                    Routes(roots.After)[1]!.DeepClone(),
                    Routes(roots.After)[0]!.DeepClone(),
                    Routes(roots.After)[2]!.DeepClone());
                break;
            case "delete_existing":
                Routes(roots.After).RemoveAt(1);
                KnownRouteIds(roots.After).RemoveAt(1);
                break;
            case "append_second_route":
                AddRoute(roots.After, "alternative_extra_route", "known_to_player");
                KnownRouteIds(roots.After).Add("alternative_extra_route");
                break;
            case "append_visible_path":
                DiagnosisPaths(roots.After).Add(CreateDiagnosisPath(
                    "diagnosis_unneeded_visible_route",
                    "Лишняя диагностика уже видимого лечения",
                    "known_to_player",
                    new[] { "route:clean_and_suture" },
                    new[] { "route:alternative_visible_route" }));
                break;
            case "rewrite_existing_path":
                DiagnosisPaths(roots.After)[0]!["displayName"] =
                    "Подменённая старая диагностика";
                break;
            case "reorder_existing_paths":
                afterTreatment["diagnosisPaths"] = new JsonArray(
                    DiagnosisPaths(roots.After)[1]!.DeepClone(),
                    DiagnosisPaths(roots.After)[0]!.DeepClone());
                break;
            case "delete_existing_path":
                DiagnosisPaths(roots.After).RemoveAt(0);
                break;
            case "reorder_known":
                afterTreatment["knownRouteIds"] = new JsonArray(
                    "existing_visible_route",
                    "clean_and_suture",
                    "alternative_visible_route");
                break;
            case "rewrite_completed":
                beforeTreatment["completedRouteIds"] = new JsonArray(
                    "clean_and_suture",
                    "existing_visible_route");
                afterTreatment["completedRouteIds"] = new JsonArray(
                    "existing_visible_route",
                    "clean_and_suture");
                break;
            case "rewrite_unrelated_mechanics":
                roots.After["display"]!["prognosis"] =
                    "Автор альтернативы не вправе менять прогноз.";
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null);
        }

        var result = ReduceAlternative(roots.Before, roots.After, hidden: false);

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "wound_transition_alternative_append_invalid");
    }

    [Theory]
    [InlineData("operation_key")]
    [InlineData("event_ref")]
    [InlineData("authority_ref")]
    [InlineData("before_fingerprint")]
    [InlineData("after_fingerprint")]
    [InlineData("route_after_image")]
    [InlineData("known_route_after_image")]
    [InlineData("diagnosis_path_after_image")]
    public void Reduce_AuthorAlternative_RejectsUnboundOrForgedEvidence(string mutation)
    {
        var hidden = mutation == "diagnosis_path_after_image";
        var roots = CreateAlternativeReducerRoots(hidden);

        var result = ReduceAlternative(
            roots.Before,
            roots.After,
            hidden,
            evidenceMutation: mutation);

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "wound_transition_alternative_evidence_invalid");
    }

    private static AlternativeReducerRoots CreateAlternativeReducerRoots(bool hidden)
    {
        var before = WoundContractTestData.CreateActiveWound();
        AddVisibleKnownRoute(before, "existing_visible_route");
        DiagnosisPaths(before).Add(CreateDiagnosisPath(
            "diagnosis_existing_zeta",
            "Сверить уже известный маршрут",
            "known_to_player",
            new[] { "route:clean_and_suture" },
            new[] { "route:clean_and_suture" }));
        DiagnosisPaths(before).Add(CreateDiagnosisPath(
            "diagnosis_existing_alpha",
            "Повторно проверить исходный маршрут",
            "known_to_player",
            new[] { "route:clean_and_suture" },
            new[] { "route:clean_and_suture" }));
        var after = before.DeepClone().AsObject();
        var routeId = hidden
            ? "alternative_hidden_route"
            : "alternative_visible_route";
        AddRoute(after, routeId, hidden ? "hidden" : "known_to_player");
        if (hidden)
        {
            DiagnosisPaths(after).Add(CreateDiagnosisPath(
                "diagnosis_alternative_hidden",
                "Проверить признак нового лечения",
                "known_to_player",
                new[] { "route:clean_and_suture" },
                new[] { "route:" + routeId }));
        }
        else
        {
            KnownRouteIds(after).Add(routeId);
        }
        AdvanceLastTransition(after, "author_alternative_treatment");
        return new AlternativeReducerRoots(before, after);
    }

    private static DiagnosisReducerRoots CreateDiagnosisReducerRoots(string resultKind)
    {
        var before = WoundContractTestData.CreateActiveWound();
        AddHiddenRoute(before, "hidden_route_zeta");
        AddHiddenRoute(before, "hidden_route_alpha");
        DiagnosisPaths(before).Add(CreateDiagnosisPath(
            "diagnosis_multi_fact",
            "Сопоставить два независимых признака",
            "known_to_player",
            new[] { "route:clean_and_suture" },
            new[] { "route:hidden_route_zeta", "route:hidden_route_alpha" }));
        var after = before.DeepClone().AsObject();
        if (string.Equals(resultKind, "success", StringComparison.Ordinal))
        {
            KnownRouteIds(after).Add("hidden_route_zeta");
            KnownRouteIds(after).Add("hidden_route_alpha");
        }
        AdvanceLastTransition(
            after,
            "diagnose",
            "transition_diagnosis_001",
            turn: 43);
        return new DiagnosisReducerRoots(before, after);
    }

    private static WoundTransitionRequest CreateDiagnosisTransitionRequest(
        JsonObject beforeRoot,
        JsonObject afterRoot,
        string resultKind) => InvokePlannerFactory(
            "CreateDiagnosisTransition",
            "transition_diagnosis_001",
            "diagnosis_command_public_001",
            "diagnosis_operation_001",
            "diagnosis_attempt_001",
            "turn_43:diagnosis_attempt",
            43,
            ParseValid(beforeRoot),
            ParseValid(afterRoot),
            "diagnosis_multi_fact",
            resultKind,
            Fingerprint('6'),
            Fingerprint('7'));

    private static WoundTransitionReductionResult ReduceAlternative(
        JsonObject beforeRoot,
        JsonObject afterRoot,
        bool hidden,
        string? evidenceMutation = null)
    {
        var routeId = hidden
            ? "alternative_hidden_route"
            : "alternative_visible_route";
        var request = CreateAlternativeTransitionRequest(
            beforeRoot,
            afterRoot,
            hidden);

        switch (evidenceMutation)
        {
            case null:
                break;
            case "operation_key":
                request = request with { OperationKey = "alternative_operation_other" };
                break;
            case "event_ref":
                request = request with { EventRef = "turn_44:other_evidence" };
                break;
            case "authority_ref":
                request = request with
                {
                    Evidence = request.Evidence with
                    {
                        AuthorityRef = "authoring_request_other"
                    }
                };
                break;
            case "before_fingerprint":
                request = request with
                {
                    Evidence = request.Evidence with
                    {
                        ExpectedBeforeFingerprint = Fingerprint('0')
                    }
                };
                break;
            case "after_fingerprint":
                request = request with
                {
                    Evidence = request.Evidence with
                    {
                        ExpectedAfterFingerprint = Fingerprint('2')
                    }
                };
                break;
            case "route_after_image":
            {
                var tampered = afterRoot.DeepClone().AsObject();
                var added = Assert.Single(
                    Routes(tampered),
                    value => value!["routeId"]!.GetValue<string>() == routeId);
                added!["displayName"] = "Подмена после запечатывания";
                request = request with { ProposedAfter = ParseValid(tampered) };
                break;
            }
            case "known_route_after_image":
            {
                var tampered = afterRoot.DeepClone().AsObject();
                tampered["treatment"]!["knownRouteIds"] = new JsonArray(
                    "existing_visible_route",
                    "clean_and_suture",
                    routeId);
                request = request with { ProposedAfter = ParseValid(tampered) };
                break;
            }
            case "diagnosis_path_after_image":
            {
                var tampered = afterRoot.DeepClone().AsObject();
                var addedPath = Assert.Single(
                    DiagnosisPaths(tampered),
                    value => value!["diagnosisPathId"]!.GetValue<string>() ==
                             "diagnosis_alternative_hidden");
                addedPath!["displayName"] = "Подмена диагностики после запечатывания";
                var tamperedAfter = ParseValid(tampered);
                request = request with
                {
                    ProposedAfter = tamperedAfter,
                    Evidence = request.Evidence with
                    {
                        ExpectedAfterFingerprint =
                            WoundIdentityState.ComputeSemanticFingerprint(tamperedAfter)
                    }
                };
                break;
            }
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(evidenceMutation),
                    evidenceMutation,
                    null);
        }

        return WoundTransitionReducer.Reduce(request);
    }

    private static WoundTransitionRequest CreateAlternativeTransitionRequest(
        JsonObject beforeRoot,
        JsonObject afterRoot,
        bool hidden) => InvokePlannerFactory(
            "CreateAlternativeTreatmentTransition",
            "transition_alternative_001",
            "authoring_request_public_001",
            Fingerprint('1'),
            "alternative_operation_001",
            "turn_44:alternative_evidence",
            44,
            ParseValid(beforeRoot),
            ParseValid(afterRoot),
            hidden ? "alternative_hidden_route" : "alternative_visible_route",
            hidden ? "diagnosis_alternative_hidden" : null,
            Fingerprint('6'),
            Fingerprint('7'));

    private static WoundTransitionRequest InvokePlannerFactory(
        string methodName,
        params object?[] arguments)
    {
        var type = typeof(WoundTransitionReducer).Assembly.GetType(
            "BookOfEternityClient.Services.MortalWoundTreatmentPlanner",
            throwOnError: false,
            ignoreCase: false);
        Assert.NotNull(type);
        var method = Assert.Single(
            type.GetMethods(
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic),
            candidate => candidate.Name == methodName &&
                         candidate.GetParameters().Length == arguments.Length);
        try
        {
            return Assert.IsType<WoundTransitionRequest>(
                method.Invoke(null, arguments));
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            throw exception.InnerException;
        }
    }

    private static JsonObject ComposeAcceptedTransitionCommandRoot(
        WoundAcceptedTurnBinding binding,
        WoundTransitionRequest request,
        string finalSceneText)
    {
        var method = Assert.Single(
            typeof(WoundResponseInputComposer).GetMethods(
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic),
            static candidate =>
                candidate.Name == "ComposeAcceptedTransitionCommandRoot" &&
                candidate.GetParameters().Length == 3);
        return Assert.IsType<JsonObject>(method.Invoke(
            null,
            new object[] { binding, request, finalSceneText }));
    }

    private static WoundAcceptedTurnBinding CreateTreatmentBinding(
        string eventRef,
        int turn)
    {
        var acceptedEvents = new[]
        {
            new WoundAcceptedEventAuthority(
                eventRef,
                "mortal_wound_treatment",
                "mortal_wound_treatment_authority_001",
                Fingerprint('6'))
        };
        return new WoundAcceptedTurnBinding(
            "session_wound_diagnosis",
            "request_wound_diagnosis",
            "snapshot_wound_diagnosis",
            "mortal_world",
            turn,
            acceptedEvents,
            WoundAcceptedEventSetFingerprint.Compute(acceptedEvents));
    }

    private static WoundMaterializationEnvelope ParseValid(JsonObject root)
    {
        var parsed = Parse(root);
        Assert.True(parsed.IsValid, DescribeIssues(parsed));
        return parsed.Wound!;
    }

    private static void AssertDiagnosisChangesOnlyKnownFactsAndTransition(
        JsonObject beforeRoot,
        WoundMaterializationEnvelope proposedAfter)
    {
        var before = JsonNode.Parse(WoundMaterializationContract.SerializeCanonical(
            ParseValid(beforeRoot)))!.AsObject();
        var normalizedAfter = JsonNode.Parse(
            WoundMaterializationContract.SerializeCanonical(proposedAfter))!.AsObject();
        normalizedAfter["lastTransition"] = before["lastTransition"]!.DeepClone();
        normalizedAfter["treatment"]!["knownRouteIds"] =
            before["treatment"]!["knownRouteIds"]!.DeepClone();
        Assert.True(JsonNode.DeepEquals(before, normalizedAfter));
    }

    private static void AdvanceLastTransition(
        JsonObject wound,
        string kind,
        string transitionId = "transition_alternative_001",
        int turn = 44)
    {
        var last = wound["lastTransition"]!.AsObject();
        last["transitionId"] = transitionId;
        last["ordinal"] = last["ordinal"]!.GetValue<int>() + 1;
        last["turn"] = turn;
        last["kind"] = kind;
    }

    private sealed record AlternativeReducerRoots(JsonObject Before, JsonObject After);

    private sealed record DiagnosisReducerRoots(JsonObject Before, JsonObject After);

    private static JsonObject CreateAcceptedDiagnosisCommandRoot(
        string result,
        IReadOnlyList<string> revealedFacts)
    {
        const string commandRef = "diagnosis_command_public_001";
        const string operationKey = "diagnosis_operation_001";
        return CreateCommandRoot(new JsonObject
        {
            ["kind"] = "accepted_transition",
            ["commandRef"] = commandRef,
            ["transitionKind"] = "diagnose",
            ["operationKey"] = operationKey,
            ["authority"] = new JsonObject
            {
                ["commandRef"] = commandRef,
                ["operationKey"] = operationKey,
                ["attemptId"] = "diagnosis_attempt_001",
                ["woundId"] = "wound_test_torn_side",
                ["diagnosisPathId"] = "diagnosis_hidden_antiseptic",
                ["expectedBeforeFingerprint"] = Fingerprint('a'),
                ["pathFingerprint"] = Fingerprint('b'),
                ["requirementAuthorityFingerprint"] = Fingerprint('c'),
                ["checkResultFingerprint"] = Fingerprint('d'),
                ["authorityFingerprint"] = Fingerprint('e')
            },
            ["result"] = new JsonObject
            {
                ["result"] = result,
                ["revealedFacts"] = new JsonArray(
                    revealedFacts.Select(static value => (JsonNode)value).ToArray()),
                ["resultFingerprint"] = Fingerprint('f')
            },
            ["finalSceneText"] = result == "success"
                ? "Осмотр выявляет скрытый способ лечения."
                : "Осмотр пока не позволяет поставить диагноз."
        });
    }

    private static JsonObject CreateAcceptedAlternativeCommandRoot(bool hidden)
    {
        const string authoringRef = "authoring_request_public_001";
        const string operationKey = "alternative_operation_001";
        var routeId = hidden
            ? "alternative_hidden_route"
            : "alternative_visible_route";
        var route = CreateRouteNode(
            routeId,
            hidden ? "hidden" : "known_to_player");
        var diagnosisPath = hidden
            ? CreateDiagnosisPath(
                "diagnosis_alternative_hidden",
                "Проверить признак нового лечения",
                "known_to_player",
                new[] { "route:clean_and_suture" },
                new[] { "route:" + routeId })
            : null;
        return CreateCommandRoot(new JsonObject
        {
            ["kind"] = "accepted_transition",
            ["commandRef"] = authoringRef,
            ["transitionKind"] = "author_alternative_treatment",
            ["operationKey"] = operationKey,
            ["authority"] = new JsonObject
            {
                ["authoringRequestRef"] = authoringRef,
                ["requestAuthorityFingerprint"] = Fingerprint('1'),
                ["operationKey"] = operationKey,
                ["woundId"] = "wound_test_torn_side",
                ["eventRef"] = "turn_44:alternative_evidence",
                ["addedRouteId"] = routeId,
                ["addedDiagnosisPathId"] = hidden
                    ? "diagnosis_alternative_hidden"
                    : null,
                ["expectedBeforeFingerprint"] = Fingerprint('2'),
                ["expectedAfterFingerprint"] = Fingerprint('3'),
                ["routeFingerprint"] = Fingerprint('4'),
                ["diagnosisPathFingerprint"] = hidden ? Fingerprint('5') : null,
                ["evidenceAuthorityFingerprint"] = Fingerprint('6'),
                ["requirementAuthorityFingerprint"] = Fingerprint('7'),
                ["authorityFingerprint"] = Fingerprint('8')
            },
            ["result"] = new JsonObject
            {
                ["decision"] = "author",
                ["route"] = route,
                ["diagnosisPath"] = diagnosisPath,
                ["resultFingerprint"] = Fingerprint('9')
            },
            ["finalSceneText"] = "Новая возможность лечения становится понятна."
        });
    }

    private static JsonObject CreateCommandRoot(JsonObject command) => new()
    {
        ["schemaVersion"] = 1,
        ["sessionId"] = "session_wound_diagnosis",
        ["requestId"] = "request_wound_diagnosis",
        ["snapshotToken"] = "snapshot_wound_diagnosis",
        ["commands"] = new JsonArray(command)
    };

    private static string CommandPath(string relativePath) =>
        $"{AcceptedMechanicsPlan.WoundCommandPath}.commands[0].{relativePath}";

    private static JsonObject CreateDiagnosisHistory(
        string result,
        IReadOnlyList<string> revealedFacts,
        string diagnosisPathId = "diagnosis_hidden_antiseptic")
    {
        var afterFingerprint = result == "success"
            ? Fingerprint('b')
            : Fingerprint('a');
        return new JsonObject
        {
            ["schemaVersion"] = 1,
            ["nextOrdinal"] = 3,
            ["transitions"] = new JsonArray(
                CreateHistoryTransition(
                    transitionId: "transition_create_001",
                    ordinal: 1,
                    woundTransitionOrdinal: 1,
                    kind: "create",
                    turn: 42,
                    eventRef: "turn_42:wound_opened",
                    operationKey: "create_operation_001",
                    beforeFingerprint: WoundHistoryState.ComputeNonexistentBeforeFingerprint(
                        "wound_test_torn_side"),
                    afterFingerprint: Fingerprint('a'),
                    attemptId: null,
                    transitionResult: null),
                CreateHistoryTransition(
                    transitionId: "transition_diagnosis_001",
                    ordinal: 2,
                    woundTransitionOrdinal: 2,
                    kind: "diagnose",
                    turn: 43,
                    eventRef: "turn_43:diagnosis_attempt",
                    operationKey: "diagnosis_operation_001",
                    beforeFingerprint: Fingerprint('a'),
                    afterFingerprint: afterFingerprint,
                    attemptId: "diagnosis_attempt_001",
                    transitionResult: SealTransitionResult(new JsonObject
                    {
                        ["kind"] = "diagnose",
                        ["diagnosisPathId"] = diagnosisPathId,
                        ["result"] = result,
                        ["revealedFacts"] = new JsonArray(
                            revealedFacts.Select(static value => (JsonNode)value).ToArray())
                    })))
        };
    }

    private static JsonObject CreateHistoryTransition(
        string transitionId,
        int ordinal,
        int woundTransitionOrdinal,
        string kind,
        int turn,
        string eventRef,
        string operationKey,
        string beforeFingerprint,
        string afterFingerprint,
        string? attemptId,
        JsonObject? transitionResult) => new()
    {
        ["transitionId"] = transitionId,
        ["woundId"] = "wound_test_torn_side",
        ["ordinal"] = ordinal,
        ["woundTransitionOrdinal"] = woundTransitionOrdinal,
        ["kind"] = kind,
        ["turn"] = turn,
        ["eventRef"] = eventRef,
        ["operationKey"] = operationKey,
        ["beforeFingerprint"] = beforeFingerprint,
        ["afterFingerprint"] = afterFingerprint,
        ["sourceFingerprint"] = Fingerprint('c'),
        ["attemptId"] = attemptId,
        ["courseId"] = null,
        ["courseMilestoneOrdinal"] = null,
        ["cycleKey"] = null,
        ["paymentFingerprint"] = null,
        ["outputFingerprint"] = Fingerprint('f'),
        ["transitionResult"] = transitionResult,
        ["readableSummary"] = kind == "diagnose"
            ? "Попытка диагностики завершена."
            : "Рана зафиксирована.",
        ["terminal"] = false
    };

    private static JsonObject CreateBaseHistory(JsonObject woundRoot)
    {
        var wound = ParseValid(woundRoot);
        return new JsonObject
        {
            ["schemaVersion"] = 1,
            ["nextOrdinal"] = 2,
            ["transitions"] = new JsonArray(CreateHistoryTransition(
                transitionId: wound.LastTransition.TransitionId,
                ordinal: 1,
                woundTransitionOrdinal: wound.LastTransition.Ordinal,
                kind: "create",
                turn: wound.LastTransition.Turn,
                eventRef: wound.Origin.EventRef,
                operationKey: "create_operation_001",
                beforeFingerprint: WoundHistoryState.ComputeNonexistentBeforeFingerprint(
                    wound.WoundId),
                afterFingerprint: WoundIdentityState.ComputeSemanticFingerprint(wound),
                attemptId: null,
                transitionResult: null))
        };
    }

    private static JsonObject SealTransitionResult(JsonObject transitionResult)
    {
        var payload = transitionResult.DeepClone().AsObject();
        payload.Remove("resultFingerprint");
        var method = Assert.Single(
            typeof(WoundHistoryState).GetMethods(
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic),
            static candidate =>
                candidate.Name == "ComputeTransitionResultFingerprint" &&
                candidate.GetParameters().Length == 1);
        var fingerprint = Assert.IsType<string>(method.Invoke(null, new object[] { payload }));
        payload["resultFingerprint"] = fingerprint;
        return payload;
    }

    private static WoundHistoryState AppendHistory(
        WoundHistoryState state,
        WoundTransitionHistoryIntent intent,
        string readableSummary)
    {
        var method = Assert.Single(
            typeof(WoundHistoryState).GetMethods(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
            static candidate =>
                candidate.Name == "AppendTransition" &&
                candidate.GetParameters().Length == 4);
        var outputFingerprint = WoundHistoryState.ComputeOutputFingerprint(
            intent.OperationKey,
            intent.EventRef,
            readableSummary);
        var result = Assert.IsType<WoundHistoryParseResult>(method.Invoke(
            state,
            new object[]
            {
                intent,
                Fingerprint('c'),
                outputFingerprint,
                readableSummary
            }));
        Assert.True(result.IsValid, DescribeIssues(result.Issues));
        return result.State!;
    }

    private static JsonObject ParseCanonicalHistory(WoundHistoryState state) =>
        JsonNode.Parse(WoundHistoryState.SerializeCanonical(state))!.AsObject();

    private static JsonObject ReadTransitionResult(WoundTransitionHistoryIntent intent)
    {
        var property = intent.GetType().GetProperty(
            "TransitionResult",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(property);
        var result = property.GetValue(intent);
        Assert.NotNull(result);
        return JsonSerializer.SerializeToNode(result, ReadableJson)!.AsObject();
    }

    private static WoundHistoryState ParseHistory(JsonObject root)
    {
        var parsed = WoundHistoryState.Parse(
            root.ToJsonString(),
            WoundHistoryState.HistoryPath);
        Assert.True(parsed.IsValid, DescribeIssues(parsed.Issues));
        return parsed.State!;
    }

    private static string HistoryPath(string relativePath) =>
        $"{WoundHistoryState.HistoryPath}.transitions[1].{relativePath}";

    private static WoundRepairBuildRequest CreateAlternativeRepairRequest(
        bool includeSensitiveInternals)
    {
        var route = CreateRouteNode("alternative_hidden_route", "hidden");
        route["mode"] = "unsupported_mode";
        var diagnosisPath = CreateDiagnosisPath(
            "diagnosis_alternative_hidden",
            "Проверить признак нового лечения",
            "known_to_player",
            new[] { "route:clean_and_suture" },
            new[] { "route:alternative_hidden_route" });
        if (includeSensitiveInternals)
        {
            route["operationKey"] = "secret_operation_key_value";
            route["woundId"] = "secret_wound_id_value";
            route["expectedBeforeFingerprint"] = Fingerprint('0');
            route["expectedAfterFingerprint"] = Fingerprint('1');
            route["routeFingerprint"] = Fingerprint('2');
            route["authorityFingerprint"] = Fingerprint('3');
            route["nestedSecrets"] = new JsonArray(new JsonObject
            {
                ["ownerId"] = "secret_owner_id_value",
                ["carrierPath"] = "secret_carrier_path_value",
                ["resourceSeal"] = "secret_resource_seal_value",
                ["sourceSeal"] = "secret_source_seal_value",
                ["providerSeal"] = "secret_provider_seal_value",
                ["privateNpcData"] = "secret_private_npc_value"
            });
            diagnosisPath["requestAuthorityFingerprint"] = Fingerprint('4');
            diagnosisPath["diagnosisPathFingerprint"] = Fingerprint('5');
            diagnosisPath["evidenceAuthorityFingerprint"] = Fingerprint('6');
            diagnosisPath["requirementAuthorityFingerprint"] = Fingerprint('7');
            diagnosisPath["checkResultFingerprint"] = Fingerprint('8');
            diagnosisPath["resultFingerprint"] = Fingerprint('9');
            diagnosisPath["gmPrivateNotes"] = "secret_gm_notes_value";
        }

        var issue = new ValidationIssue(
            "woundTreatmentAuthorings[0].route.mode",
            IssueSeverity.Error,
            "Alternative treatment mode must use the closed vocabulary.",
            code: "wound_materialization_invalid_field",
            section: "wound_materialization",
            expected: "procedure, course, or guaranteed",
            actual: "unsupported_mode");
        var candidate = new WoundRepairCandidateInput(
            "author_alternative_treatment",
            "candidate_alternative_treatment_001",
            Fingerprint('b'),
            "authoring_request_public_001",
            new JsonObject
            {
                ["event"] = "обнаруженная запись полевого врача",
                ["target"] = "игрок",
                ["realm"] = "Смертный мир",
                ["authorityFingerprint"] = includeSensitiveInternals
                    ? Fingerprint('c')
                    : null
            },
            new[] { "decline", "author" },
            "I",
            "IV",
            new JsonObject
            {
                ["authoringRequestRef"] = "authoring_request_public_001",
                ["decision"] = "author",
                ["route"] = route,
                ["diagnosisPath"] = diagnosisPath
            },
            new[] { issue });
        return new WoundRepairBuildRequest(
            "session_wound_diagnosis",
            "request_wound_diagnosis",
            "snapshot_wound_diagnosis",
            new[] { candidate });
    }

    private static JsonObject CreateRouteNode(string routeId, string visibility)
    {
        var wound = WoundContractTestData.CreateActiveWound();
        var route = wound["treatment"]!["routes"]![0]!.DeepClone().AsObject();
        route["routeId"] = routeId;
        route["displayName"] = "Маршрут " + routeId;
        route["visibility"] = visibility;
        return route;
    }

    private static string Fingerprint(char value) =>
        "sha256:" + new string(value, 64);

    private static JsonObject CreateReachableHiddenRouteWound()
    {
        var wound = WoundContractTestData.CreateActiveWound();
        AddHiddenRoute(wound, "hidden_antiseptic_course");
        DiagnosisPaths(wound).Add(CreateDiagnosisPath(
            "diagnosis_hidden_antiseptic",
            "Осмотреть края раны при ярком свете",
            "hidden",
            new[] { "route:clean_and_suture" },
            new[] { "route:hidden_antiseptic_course" }));
        return wound;
    }

    private static JsonObject CreateDiagnosisPath(
        string diagnosisPathId,
        string displayName,
        string visibility,
        IReadOnlyList<string> requiresKnownFacts,
        IReadOnlyList<string> reveals) => new()
    {
        ["diagnosisPathId"] = diagnosisPathId,
        ["displayName"] = displayName,
        ["visibility"] = visibility,
        ["requiresKnownFacts"] = new JsonArray(
            requiresKnownFacts.Select(static value => (JsonNode)value).ToArray()),
        ["requirements"] = new JsonArray(),
        ["check"] = new JsonObject(),
        ["reveals"] = new JsonArray(
            reveals.Select(static value => (JsonNode)value).ToArray()),
        ["failurePolicy"] = "no_reveal"
    };

    private static void AddVisibleKnownRoute(JsonObject wound, string routeId)
    {
        AddRoute(wound, routeId, "known_to_player");
        KnownRouteIds(wound).Add(routeId);
    }

    private static void AddHiddenRoute(JsonObject wound, string routeId) =>
        AddRoute(wound, routeId, "hidden");

    private static void AddRoute(JsonObject wound, string routeId, string visibility)
    {
        var route = Routes(wound)[0]!.DeepClone().AsObject();
        route["routeId"] = routeId;
        route["displayName"] = "Маршрут " + routeId;
        route["visibility"] = visibility;
        Routes(wound).Add(route);
    }

    private static JsonObject CreateComplication(
        string complicationId,
        string visibility) => new()
    {
        ["complicationId"] = complicationId,
        ["kind"] = "infection",
        ["state"] = "active",
        ["displayName"] = "Осложнение " + complicationId,
        ["treatmentDifficultyModifier"] = 1,
        ["ownedEffectIds"] = new JsonArray(),
        ["visibility"] = visibility
    };

    private static string RoutePath(JsonObject wound, string routeId)
    {
        var routes = Routes(wound);
        for (var index = 0; index < routes.Count; index++)
        {
            if (routes[index]!["routeId"]!.GetValue<string>() == routeId)
                return $"{Path}.treatment.routes[{index}]";
        }
        throw new InvalidOperationException($"Route '{routeId}' is absent from the fixture.");
    }

    private static JsonObject Treatment(JsonObject wound) =>
        wound["treatment"]!.AsObject();

    private static JsonArray DiagnosisPaths(JsonObject wound) =>
        Treatment(wound)["diagnosisPaths"]!.AsArray();

    private static JsonArray Routes(JsonObject wound) =>
        Treatment(wound)["routes"]!.AsArray();

    private static JsonArray KnownRouteIds(JsonObject wound) =>
        Treatment(wound)["knownRouteIds"]!.AsArray();

    private static JsonArray Complications(JsonObject wound) =>
        wound["complications"]!.AsArray();

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
        DescribeIssues(result.Issues);

    private static string DescribeIssues(IReadOnlyList<ValidationIssue> issues) =>
        string.Join(
            Environment.NewLine,
            issues.Select(issue =>
                $"{issue.Code} {issue.FilePath}: {issue.Expected} / {issue.Actual}"));
}
