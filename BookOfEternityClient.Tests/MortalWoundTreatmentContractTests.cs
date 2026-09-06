using System.Text.Json;
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
    public void Parse_AllElevenClosedRequirementBranchesShareOneTypedConjunctiveArray()
    {
        var wound = CreateWoundWithStrictRoute("procedure");
        var route = Route(wound);
        route["requirements"] = new JsonArray(
            new JsonObject
            {
                ["kind"] = "item_quantity",
                ["itemRef"] = "sterile_thread",
                ["quantity"] = 1,
                ["ownerRole"] = "provider"
            },
            new JsonObject
            {
                ["kind"] = "resource_quantity",
                ["resourceRef"] = "medical_charge",
                ["quantity"] = 2,
                ["ownerRole"] = "provider"
            },
            new JsonObject
            {
                ["kind"] = "skill_tier",
                ["capabilityRef"] = "field_medicine",
                ["minimumTier"] = 0,
                ["actorRole"] = "provider"
            },
            new JsonObject
            {
                ["kind"] = "source_capability",
                ["capabilityRef"] = "sterile_field",
                ["actorRole"] = "provider"
            },
            new JsonObject { ["kind"] = "provider", ["providerRef"] = "medic_01" },
            new JsonObject
            {
                ["kind"] = "consent",
                ["consentRef"] = "care_consent",
                ["providerRef"] = "medic_01",
                ["targetRef"] = "player_current"
            },
            new JsonObject { ["kind"] = "facility", ["facilityRef"] = "clean_bench" },
            new JsonObject
            {
                ["kind"] = "location",
                ["locationRef"] = "clinic_room",
                ["targetRole"] = "target"
            },
            new JsonObject
            {
                ["kind"] = "quest_state",
                ["questRef"] = "restore_clinic",
                ["requiredState"] = "completed"
            },
            new JsonObject
            {
                ["kind"] = "effect_state",
                ["effectRef"] = "pain_suppressed",
                ["requiredState"] = "active",
                ["targetRole"] = "target"
            },
            new JsonObject
            {
                ["kind"] = "environment",
                ["environmentRef"] = "sterile_air",
                ["requiredState"] = "active"
            });
        route["resourcePolicy"]!["mutations"] = new JsonArray();
        route["resolution"]!["modifierSource"] = new JsonObject
        {
            ["kind"] = "fixed_zero"
        };

        var result = Parse(wound);

        Assert.True(result.IsValid, DescribeIssues(result));
        var parsedRoute = Assert.IsType<MortalWoundProcedureRouteDefinition>(
            Assert.Single(MortalWoundTreatmentContract.ParseProjection(
                result.Wound!.Treatment,
                Path + ".treatment",
                result.Wound.Owner.Realm,
                "player",
                result.Wound.Severity.Rank,
                result.Wound.Complications,
                result.Wound.Recovery.DeteriorationPolicy).Treatment!.Routes));
        Assert.Equal(11, parsedRoute.Requirements.Length);
        Assert.Equal(
            new[]
            {
                "item_quantity", "resource_quantity", "skill_tier", "source_capability",
                "provider", "consent", "facility", "location", "quest_state",
                "effect_state", "environment"
            },
            parsedRoute.Requirements.Select(static requirement => requirement.Kind));
    }

    [Theory]
    [InlineData(int.MinValue)]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    public void Parse_SkillMinimumTierUsesTheCompleteSignedInt32Domain(int minimumTier)
    {
        var wound = CreateWoundWithStrictRoute("procedure");
        Route(wound)["requirements"]![1]!["minimumTier"] = minimumTier;

        var result = Parse(wound);

        Assert.True(result.IsValid, DescribeIssues(result));
    }

    [Theory]
    [InlineData(16, true)]
    [InlineData(17, false)]
    public void Parse_CommonRequirementCountUsesTheExactVersionOneBoundary(
        int count,
        bool expectedValid)
    {
        var wound = CreateWoundWithStrictRoute("procedure");
        var route = Route(wound);
        route["requirements"] = WoundContractTestData.Repeat(
            count,
            index => new JsonObject
            {
                ["kind"] = "provider",
                ["providerRef"] = $"provider_{index}"
            });
        route["resourcePolicy"]!["mutations"] = new JsonArray();
        route["resolution"]!["modifierSource"] = new JsonObject
        {
            ["kind"] = "fixed_zero"
        };

        var result = Parse(wound);

        if (expectedValid)
            Assert.True(result.IsValid, DescribeIssues(result));
        else
            AssertInvalidAt(
                result,
                "wound.treatment.routes[0].requirements",
                "wound_materialization_limit_exceeded");
    }

    [Theory]
    [InlineData("common_out_of_range", "wound.treatment.routes[0].resourcePolicy.mutations[0].requirementIndex")]
    [InlineData("course_unknown_milestone", "wound.treatment.routes[0].resourcePolicy.mutations[0].milestoneOrdinal")]
    [InlineData("course_out_of_range", "wound.treatment.routes[0].resourcePolicy.mutations[0].requirementIndex")]
    [InlineData("course_non_quantity", "wound.treatment.routes[0].resourcePolicy.mutations[0].requirementIndex")]
    [InlineData("course_scope_in_procedure", "wound.treatment.routes[0].resourcePolicy.mutations[0].scope")]
    public void Parse_ResourceSelectorsResolveOneExistingQuantityRequirement(
        string mutation,
        string expectedPath)
    {
        var mode = mutation.StartsWith("course_", StringComparison.Ordinal) &&
                   mutation != "course_scope_in_procedure"
            ? "course"
            : "procedure";
        var wound = CreateWoundWithStrictRoute(mode);
        var route = Route(wound);
        var selector = route["resourcePolicy"]!["mutations"]![0]!.AsObject();
        switch (mutation)
        {
            case "common_out_of_range":
                selector["requirementIndex"] = 15;
                break;
            case "course_unknown_milestone":
                selector["milestoneOrdinal"] = 99;
                break;
            case "course_out_of_range":
                selector["requirementIndex"] = 15;
                break;
            case "course_non_quantity":
                route["outcomes"]![0]!["requirements"]![0] = new JsonObject
                {
                    ["kind"] = "provider",
                    ["providerRef"] = "medic_01"
                };
                break;
            case "course_scope_in_procedure":
                selector["scope"] = "course_milestone";
                selector["milestoneOrdinal"] = 1;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null);
        }

        AssertInvalidAt(Parse(wound), expectedPath, "wound_materialization_invalid_field");
    }

    [Fact]
    public void Parse_CourseAndGuaranteedCannotConsumeFailedAttemptCategory()
    {
        foreach (var mode in new[] { "course", "guaranteed" })
        {
            var wound = CreateWoundWithStrictRoute(mode);
            Route(wound)["resourcePolicy"]!["consumeOn"] =
                new JsonArray("success", "failed_attempt");

            AssertInvalidAt(
                Parse(wound),
                "wound.treatment.routes[0].resourcePolicy.consumeOn[1]",
                "wound_materialization_invalid_field");
        }
    }

    [Theory]
    [InlineData(64, true)]
    [InlineData(65, false)]
    public void Parse_ResourceMutationSelectorsUseTheExactVersionOneBoundary(
        int count,
        bool expectedValid)
    {
        var wound = CreateWoundWithStrictRoute("course");
        ConfigureCourseResourceSelectorCount(wound, count);

        var result = Parse(wound);

        if (expectedValid)
            Assert.True(result.IsValid, DescribeIssues(result));
        else
            AssertInvalidAt(
                result,
                "wound.treatment.routes[0].resourcePolicy.mutations",
                "wound_materialization_limit_exceeded");
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
    [InlineData("procedure")]
    [InlineData("course")]
    [InlineData("guaranteed")]
    public void Parse_ExactVersionOneTreatmentRouteShapeIsAccepted(string mode)
    {
        var wound = CreateWoundWithStrictRoute(mode);

        var result = Parse(wound);

        Assert.True(result.IsValid, DescribeIssues(result));
        var route = Assert.Single(result.Wound!.Treatment.Routes);
        Assert.Equal(mode, route.Mode);
    }

    [Fact]
    public void ParseProjection_ProducesDetachedTypedProcedureWithoutCachingItOnRawRoute()
    {
        var parsed = Parse(CreateWoundWithStrictRoute("procedure"));
        Assert.True(parsed.IsValid, DescribeIssues(parsed));
        var wound = parsed.Wound!;

        var typed = MortalWoundTreatmentContract.ParseProjection(
            wound.Treatment,
            Path + ".treatment",
            wound.Owner.Realm,
            "player",
            wound.Severity.Rank,
            wound.Complications,
            wound.Recovery.DeteriorationPolicy);

        Assert.True(typed.IsValid, DescribeIssues(typed.Issues));
        var treatment = Assert.IsType<MortalWoundTreatmentDefinition>(typed.Treatment);
        var route = Assert.IsType<MortalWoundProcedureRouteDefinition>(
            Assert.Single(treatment.Routes));
        Assert.Collection(
            route.Requirements,
            requirement => Assert.IsType<MortalWoundItemQuantityRequirement>(requirement),
            requirement => Assert.IsType<MortalWoundSkillTierRequirement>(requirement));
        Assert.IsType<MortalWoundStabilizeOperation>(
            route.Bands[0].DeclaredResult[0]);
        Assert.DoesNotContain(
            typeof(WoundTreatmentRoute).GetProperties(),
            property => property.Name.Contains("Typed", StringComparison.Ordinal));
    }

    [Fact]
    public void ParseProjection_TypedCanonicalOutputSurvivesBorrowedRawDocumentDisposal()
    {
        var parsed = Parse(CreateWoundWithStrictRoute("procedure"));
        Assert.True(parsed.IsValid, DescribeIssues(parsed));
        var wound = parsed.Wound!;
        var route = Assert.Single(wound.Treatment.Routes);
        using var borrowedDocument = JsonDocument.Parse(route.Resolution.GetRawText());
        var borrowedTreatment = wound.Treatment with
        {
            Routes = new[] { route with { Resolution = borrowedDocument.RootElement } }
        };
        var projected = MortalWoundTreatmentContract.ParseProjection(
            borrowedTreatment,
            Path + ".treatment",
            wound.Owner.Realm,
            "player",
            wound.Severity.Rank,
            wound.Complications,
            wound.Recovery.DeteriorationPolicy);
        Assert.True(projected.IsValid, DescribeIssues(projected.Issues));
        var typed = Assert.IsType<MortalWoundTreatmentDefinition>(projected.Treatment);
        var beforeDisposal = SerializeTypedTreatment(typed);

        borrowedDocument.Dispose();
        var afterDisposal = SerializeTypedTreatment(typed);

        Assert.Equal(beforeDisposal, afterDisposal);
    }

    [Theory]
    [InlineData("missing_band_id", "wound.treatment.routes[0].outcomes[0].bandId", "wound_materialization_missing_field")]
    [InlineData("duplicate_band_id", "wound.treatment.routes[0].outcomes[1].bandId", "wound_materialization_invalid_field")]
    [InlineData("case_changed_band_id", "wound.treatment.routes[0].outcomes[1].bandId", "wound_materialization_invalid_field")]
    [InlineData("confusable_band_id", "wound.treatment.routes[0].outcomes[1].bandId", "wound_materialization_invalid_field")]
    [InlineData("gap", "wound.treatment.routes[0].outcomes[1].minimumMargin", "wound_materialization_invalid_field")]
    [InlineData("overlap", "wound.treatment.routes[0].outcomes[1].maximumMargin", "wound_materialization_invalid_field")]
    [InlineData("missing_finite_minimum", "wound.treatment.routes[0].outcomes[1].minimumMargin", "wound_materialization_invalid_field")]
    [InlineData("missing_finite_maximum", "wound.treatment.routes[0].outcomes[2].maximumMargin", "wound_materialization_invalid_field")]
    [InlineData("empty_band", "wound.treatment.routes[0].outcomes[1].minimumMargin", "wound_materialization_invalid_field")]
    [InlineData("first_upper_bound", "wound.treatment.routes[0].outcomes[0].maximumMargin", "wound_materialization_invalid_field")]
    [InlineData("last_lower_bound", "wound.treatment.routes[0].outcomes[3].minimumMargin", "wound_materialization_invalid_field")]
    [InlineData("wrong_first_category", "wound.treatment.routes[0].outcomes[0].category", "wound_materialization_invalid_field")]
    [InlineData("category_regression", "wound.treatment.routes[0].outcomes[2].category", "wound_materialization_invalid_field")]
    [InlineData("wrong_last_category", "wound.treatment.routes[0].outcomes[3].category", "wound_materialization_invalid_field")]
    [InlineData("success_without_positive", "wound.treatment.routes[0].outcomes[0].result", "wound_materialization_invalid_field")]
    [InlineData("later_success_without_positive", "wound.treatment.routes[0].outcomes[1].result", "wound_materialization_invalid_field")]
    [InlineData("adverse_success", "wound.treatment.routes[0].outcomes[0].result[0].kind", "wound_materialization_invalid_field")]
    [InlineData("one_band", "wound.treatment.routes[0].outcomes", "wound_materialization_invalid_field")]
    [InlineData("seventeen_bands", "wound.treatment.routes[0].outcomes", "wound_materialization_limit_exceeded")]
    public void Parse_ProcedureBandsAreClosedCategorizedGaplessAndPositive(
        string mutation,
        string expectedPath,
        string expectedCode)
    {
        var wound = CreateWoundWithStrictRoute("procedure");
        var outcomes = Route(wound)["outcomes"]!.AsArray();
        switch (mutation)
        {
            case "missing_band_id":
                outcomes[0]!.AsObject().Remove("bandId");
                break;
            case "duplicate_band_id":
                outcomes[1]!["bandId"] = outcomes[0]!["bandId"]!.GetValue<string>();
                break;
            case "case_changed_band_id":
                outcomes[0]!["bandId"] = "clean-band";
                outcomes[1]!["bandId"] = "CLEAN-BAND";
                break;
            case "confusable_band_id":
                outcomes[0]!["bandId"] = "clean-success";
                outcomes[1]!["bandId"] = "clean‐success";
                break;
            case "gap":
                outcomes[1]!["minimumMargin"] = 1;
                break;
            case "overlap":
                outcomes[1]!["maximumMargin"] = 5;
                break;
            case "missing_finite_minimum":
                outcomes[1]!["minimumMargin"] = null;
                break;
            case "missing_finite_maximum":
                outcomes[2]!["maximumMargin"] = null;
                break;
            case "empty_band":
                outcomes[1]!["minimumMargin"] = 5;
                outcomes[2]!["maximumMargin"] = 4;
                break;
            case "first_upper_bound":
                outcomes[0]!["maximumMargin"] = 100;
                break;
            case "last_lower_bound":
                outcomes[3]!["minimumMargin"] = -100;
                break;
            case "wrong_first_category":
                outcomes[0]!["category"] = "partial_success";
                break;
            case "category_regression":
                outcomes[2]!["category"] = "success";
                outcomes[2]!["result"] =
                    new JsonArray(new JsonObject { ["kind"] = "stabilize" });
                break;
            case "wrong_last_category":
                outcomes[3]!["category"] = "partial_success";
                break;
            case "success_without_positive":
                outcomes[0]!["result"] = new JsonArray(new JsonObject { ["kind"] = "no_improvement" });
                break;
            case "later_success_without_positive":
                outcomes[1]!["category"] = "success";
                outcomes[1]!["result"] = new JsonArray(new JsonObject { ["kind"] = "no_improvement" });
                break;
            case "adverse_success":
                outcomes[0]!["result"] = new JsonArray(CreateEffectlessComplicationOperation());
                break;
            case "one_band":
                Route(wound)["outcomes"] = new JsonArray(outcomes[0]!.DeepClone());
                break;
            case "seventeen_bands":
            {
                var rows = new JsonArray();
                for (var index = 0; index < 17; index++)
                {
                    rows.Add(new JsonObject
                    {
                        ["bandId"] = $"band_{index}",
                        ["minimumMargin"] = index == 16 ? null : 16 - index,
                        ["maximumMargin"] = index == 0 ? null : 16 - index,
                        ["category"] = index == 0 ? "success" : "failed_attempt",
                        ["result"] = index == 0
                            ? new JsonArray(new JsonObject { ["kind"] = "stabilize" })
                            : new JsonArray(new JsonObject { ["kind"] = "no_improvement" })
                    });
                }

                Route(wound)["outcomes"] = rows;
                break;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null);
        }

        AssertInvalidAt(Parse(wound), expectedPath, expectedCode);
    }

    [Theory]
    [InlineData("wrong_formula", "wound.treatment.routes[0].resolution.formulaKey")]
    [InlineData("wrong_roll_source", "wound.treatment.routes[0].resolution.rollSource")]
    [InlineData("wrong_critical_policy", "wound.treatment.routes[0].resolution.criticalPolicy")]
    [InlineData("negative_difficulty", "wound.treatment.routes[0].resolution.difficulty")]
    [InlineData("difficulty_above_int32", "wound.treatment.routes[0].resolution.difficulty")]
    [InlineData("unknown_modifier_kind", "wound.treatment.routes[0].resolution.modifierSource.kind")]
    [InlineData("negative_modifier_index", "wound.treatment.routes[0].resolution.modifierSource.requirementIndex")]
    [InlineData("out_of_range_modifier_index", "wound.treatment.routes[0].resolution.modifierSource.requirementIndex")]
    [InlineData("non_skill_modifier_index", "wound.treatment.routes[0].resolution.modifierSource.requirementIndex")]
    public void Parse_ProcedureResolutionUsesOnlyTheRegisteredVersionOneContour(
        string mutation,
        string expectedPath)
    {
        var wound = CreateWoundWithStrictRoute("procedure");
        var resolution = Route(wound)["resolution"]!;
        switch (mutation)
        {
            case "wrong_formula":
                resolution["formulaKey"] = "gm_custom_formula";
                break;
            case "wrong_roll_source":
                resolution["rollSource"] = "gm_roll";
                break;
            case "wrong_critical_policy":
                resolution["criticalPolicy"] = "natural_20_auto_heal";
                break;
            case "negative_difficulty":
                resolution["difficulty"] = -1;
                break;
            case "difficulty_above_int32":
                resolution["difficulty"] = (long)int.MaxValue + 1L;
                break;
            case "unknown_modifier_kind":
                resolution["modifierSource"]!["kind"] = "freeform_modifier";
                break;
            case "negative_modifier_index":
                resolution["modifierSource"]!["requirementIndex"] = -1;
                break;
            case "out_of_range_modifier_index":
                resolution["modifierSource"]!["requirementIndex"] = 2;
                break;
            case "non_skill_modifier_index":
                resolution["modifierSource"]!["requirementIndex"] = 0;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null);
        }

        AssertInvalidAt(
            Parse(wound),
            expectedPath,
            "wound_materialization_invalid_field");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(int.MaxValue)]
    public void Parse_ProcedureAcceptsInclusiveSignedInt32DifficultyBounds(int difficulty)
    {
        var wound = CreateWoundWithStrictRoute("procedure");
        Route(wound)["resolution"]!["difficulty"] = difficulty;

        var result = Parse(wound);

        Assert.True(result.IsValid, DescribeIssues(result));
    }

    [Fact]
    public void Parse_ProcedureMayUseExactFixedZeroModifierWithoutSkillRequirement()
    {
        var wound = CreateWoundWithStrictRoute("procedure");
        var route = Route(wound);
        route["requirements"] = new JsonArray(route["requirements"]![0]!.DeepClone());
        route["resolution"]!["modifierSource"] =
            new JsonObject { ["kind"] = "fixed_zero" };

        var result = Parse(wound);

        Assert.True(result.IsValid, DescribeIssues(result));
    }

    [Theory]
    [InlineData("first_ordinal", "wound.treatment.routes[0].outcomes[0].ordinal")]
    [InlineData("first_delay", "wound.treatment.routes[0].outcomes[0].afterMinutes")]
    [InlineData("negative_after_minutes", "wound.treatment.routes[0].outcomes[1].afterMinutes")]
    [InlineData("ordinal_gap", "wound.treatment.routes[0].outcomes[1].ordinal")]
    [InlineData("nonincreasing_time", "wound.treatment.routes[0].outcomes[1].afterMinutes")]
    [InlineData("wrong_category", "wound.treatment.routes[0].outcomes[1].category")]
    [InlineData("early_completed", "wound.treatment.routes[0].outcomes[0].completion")]
    [InlineData("final_active", "wound.treatment.routes[0].outcomes[2].completion")]
    [InlineData("final_empty", "wound.treatment.routes[0].outcomes[2].result")]
    [InlineData("no_positive_course", "wound.treatment.routes[0].outcomes")]
    [InlineData("harmful_milestone", "wound.treatment.routes[0].outcomes[1].result[0].kind")]
    [InlineData("beneficial_interruption", "wound.treatment.routes[0].interruption.result[0].kind")]
    [InlineData("wrong_interruption_category", "wound.treatment.routes[0].interruption.category")]
    [InlineData("negative_gap", "wound.treatment.routes[0].resolution.maximumGapMinutes")]
    [InlineData("wrong_clock", "wound.treatment.routes[0].resolution.clockKind")]
    public void Parse_CourseMilestonesAndInterruptionHaveOneClosedContour(
        string mutation,
        string expectedPath)
    {
        var wound = CreateWoundWithStrictRoute("course");
        var route = Route(wound);
        var outcomes = route["outcomes"]!.AsArray();
        switch (mutation)
        {
            case "first_ordinal":
                outcomes[0]!["ordinal"] = 2;
                break;
            case "first_delay":
                outcomes[0]!["afterMinutes"] = 1;
                break;
            case "negative_after_minutes":
                outcomes[1]!["afterMinutes"] = -1;
                break;
            case "ordinal_gap":
                outcomes[1]!["ordinal"] = 3;
                break;
            case "nonincreasing_time":
                outcomes[1]!["afterMinutes"] = 0;
                break;
            case "wrong_category":
                outcomes[1]!["category"] = "partial_success";
                break;
            case "early_completed":
                outcomes[0]!["completion"] = "completed";
                break;
            case "final_active":
                outcomes[2]!["completion"] = "active";
                break;
            case "final_empty":
                outcomes[2]!["result"] = new JsonArray();
                break;
            case "no_positive_course":
                outcomes[2]!["result"] = new JsonArray(new JsonObject { ["kind"] = "no_improvement" });
                break;
            case "harmful_milestone":
                outcomes[1]!["result"] = new JsonArray(CreateEffectlessComplicationOperation());
                break;
            case "beneficial_interruption":
                route["interruption"]!["result"] =
                    new JsonArray(new JsonObject { ["kind"] = "stabilize" });
                break;
            case "wrong_interruption_category":
                route["interruption"]!["category"] = "success";
                break;
            case "negative_gap":
                route["resolution"]!["maximumGapMinutes"] = -1;
                break;
            case "wrong_clock":
                route["resolution"]!["clockKind"] = "wall_clock";
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null);
        }

        AssertInvalidAt(
            Parse(wound),
            expectedPath,
            "wound_materialization_invalid_field");
    }

    [Theory]
    [InlineData("effectful_complication", "wound.treatment.routes[0].interruption.result[0].complicationDraft.consequenceDefinitions")]
    [InlineData("zero_difficulty", "wound.treatment.routes[0].interruption.result[0].complicationDraft.complications[0].treatmentDifficultyModifier")]
    [InlineData("difficulty_too_high", "wound.treatment.routes[0].interruption.result[0].complicationDraft.complications[0].treatmentDifficultyModifier")]
    [InlineData("mixed_no_improvement", "wound.treatment.routes[0].interruption.result")]
    public void Parse_CourseInterruptionCannotHideTreatmentOrNeutralComplication(
        string mutation,
        string expectedPath)
    {
        var wound = CreateWoundWithStrictRoute("course");
        var interruption = Route(wound)["interruption"]!;
        interruption["result"] = new JsonArray(CreateEffectlessComplicationOperation());
        var operation = interruption["result"]![0]!;
        switch (mutation)
        {
            case "effectful_complication":
                operation["complicationDraft"]!["consequenceDefinitions"] =
                    CreateEffectfulComplicationDefinitions();
                break;
            case "zero_difficulty":
                operation["complicationDraft"]!["complications"]![0]!["treatmentDifficultyModifier"] = 0;
                break;
            case "difficulty_too_high":
                operation["complicationDraft"]!["complications"]![0]!["treatmentDifficultyModifier"] = 5;
                break;
            case "mixed_no_improvement":
                interruption["result"]!.AsArray().Add(new JsonObject { ["kind"] = "no_improvement" });
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null);
        }

        AssertInvalidAt(
            Parse(wound),
            expectedPath,
            "wound_materialization_invalid_field");
    }

    [Fact]
    public void Parse_CourseInterruptionMayReferenceOneExactCurrentDeclaredPolicy()
    {
        var wound = CreateWoundWithStrictRoute("course");
        ConfigureCurrentDeteriorationPolicy(wound, "missed-course-dose");
        Route(wound)["interruption"]!["result"] = new JsonArray(new JsonObject
        {
            ["kind"] = "apply_deterioration",
            ["policyRef"] = "missed-course-dose"
        });

        var result = Parse(wound);

        Assert.True(result.IsValid, DescribeIssues(result));
    }

    [Fact]
    public void Parse_CourseInterruptionAcceptsAnOrderedMixedHarmfulSequence()
    {
        var wound = CreateWoundWithStrictRoute("course");
        ConfigureCurrentDeteriorationPolicy(wound, "missed-course-dose");
        var first = CreateEffectlessComplicationOperation();
        var second = CreateEffectlessComplicationOperation();
        second["complicationDraft"]!["complications"]![0]!["complicationRef"] =
            "secondary_inflammation";
        Route(wound)["interruption"]!["result"] = new JsonArray(
            first,
            new JsonObject
            {
                ["kind"] = "apply_deterioration",
                ["policyRef"] = "missed-course-dose"
            },
            second);

        var result = Parse(wound);

        Assert.True(result.IsValid, DescribeIssues(result));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(32)]
    public void Parse_CourseAcceptsInclusiveVersionOneMilestoneCountBounds(int count)
    {
        var wound = CreateWoundWithStrictRoute("course");
        ConfigureCourseMilestoneCount(wound, count);

        var result = Parse(wound);

        Assert.True(result.IsValid, DescribeIssues(result));
    }

    [Fact]
    public void Parse_CourseRejectsMoreThanThirtyTwoMilestones()
    {
        var wound = CreateWoundWithStrictRoute("course");
        ConfigureCourseMilestoneCount(wound, 33);
        Route(wound)["outcomes"]![32] = "untrusted_tail_must_not_be_projected";

        var result = Parse(wound);

        AssertInvalidAt(
            result,
            "wound.treatment.routes[0].outcomes",
            "wound_materialization_limit_exceeded");
        Assert.DoesNotContain(result.Issues, issue => issue.FilePath.StartsWith(
            "wound.treatment.routes[0].outcomes[32]",
            StringComparison.Ordinal));
    }

    [Fact]
    public void Parse_CourseRejectsZeroMilestones()
    {
        var wound = CreateWoundWithStrictRoute("course");
        ConfigureCourseMilestoneCount(wound, 0);

        AssertInvalidAt(
            Parse(wound),
            "wound.treatment.routes[0].outcomes",
            "wound_materialization_invalid_field");
    }

    [Theory]
    [InlineData("two_outcomes", "wound.treatment.routes[0].outcomes")]
    [InlineData("mismatched_capability", "wound.treatment.routes[0].resolution.capabilityRef")]
    [InlineData("confusable_capability", "wound.treatment.routes[0].resolution.capabilityRef")]
    [InlineData("mismatched_role", "wound.treatment.routes[0].resolution.actorRole")]
    [InlineData("wrong_category", "wound.treatment.routes[0].outcomes[0].category")]
    [InlineData("empty_result", "wound.treatment.routes[0].outcomes[0].result")]
    [InlineData("no_improvement", "wound.treatment.routes[0].outcomes[0].result[0].kind")]
    [InlineData("complication", "wound.treatment.routes[0].outcomes[0].result[0].kind")]
    [InlineData("deterioration", "wound.treatment.routes[0].outcomes[0].result[0].kind")]
    public void Parse_GuaranteedRouteHasOneMatchingPositiveOutcome(
        string mutation,
        string expectedPath)
    {
        var wound = CreateWoundWithStrictRoute("guaranteed");
        var route = Route(wound);
        var outcome = route["outcomes"]![0]!;
        switch (mutation)
        {
            case "two_outcomes":
                route["outcomes"]!.AsArray().Add(outcome.DeepClone());
                break;
            case "mismatched_capability":
                route["resolution"]!["capabilityRef"] = "different_capability";
                break;
            case "confusable_capability":
                route["requirements"]![0]!["capabilityRef"] = "healing-source";
                route["resolution"]!["capabilityRef"] = "healing‐source";
                break;
            case "mismatched_role":
                route["resolution"]!["actorRole"] = "target";
                break;
            case "wrong_category":
                outcome["category"] = "partial_success";
                break;
            case "empty_result":
                outcome["result"] = new JsonArray();
                break;
            case "no_improvement":
                outcome["result"] = new JsonArray(new JsonObject { ["kind"] = "no_improvement" });
                break;
            case "complication":
                outcome["result"] = new JsonArray(CreateEffectlessComplicationOperation());
                break;
            case "deterioration":
                outcome["result"] = new JsonArray(new JsonObject
                {
                    ["kind"] = "apply_deterioration",
                    ["policyRef"] = "untreated_infection"
                });
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null);
        }

        AssertInvalidAt(
            Parse(wound),
            expectedPath,
            "wound_materialization_invalid_field");
    }

    [Fact]
    public void Parse_GuaranteedRouteAllowsValidSiblingRequirementsBesideOneMatchingSource()
    {
        var wound = CreateWoundWithStrictRoute("guaranteed");
        Route(wound)["requirements"]!.AsArray().Add(new JsonObject
        {
            ["kind"] = "provider",
            ["providerRef"] = "shrine_healer"
        });

        var result = Parse(wound);

        Assert.True(result.IsValid, DescribeIssues(result));
    }

    [Theory]
    [InlineData("CLEAN-ROUTE")]
    [InlineData("clean‐route")]
    public void Parse_TreatmentRouteIdsAreExactCaseAndUnicodeConfusableUnique(
        string conflictingRouteId)
    {
        var wound = CreateWoundWithStrictRoute("procedure");
        var first = Route(wound);
        first["routeId"] = "clean-route";
        var confusable = first.DeepClone().AsObject();
        confusable["routeId"] = conflictingRouteId;
        wound["treatment"]!["routes"]!.AsArray().Add(confusable);
        wound["treatment"]!["knownRouteIds"] =
            new JsonArray("clean-route", conflictingRouteId);

        AssertInvalidAt(
            Parse(wound),
            "wound.treatment.routes[1].routeId",
            "wound_materialization_invalid_field");
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

    [Theory]
    [InlineData("stabilize")]
    [InlineData("add_recovery")]
    [InlineData("reduce_severity")]
    [InlineData("remove_complication")]
    [InlineData("heal")]
    [InlineData("no_improvement")]
    [InlineData("add_complication")]
    [InlineData("apply_deterioration")]
    public void Parse_OutcomeUnionAcceptsOnlyCompleteTypedVersionOneBranches(string kind)
    {
        var wound = CreateWoundWithStrictRoute("procedure");
        if (string.Equals(kind, "remove_complication", StringComparison.Ordinal))
            AddExistingIrritationComplication(wound);
        if (string.Equals(kind, "apply_deterioration", StringComparison.Ordinal))
            ConfigureCurrentDeteriorationPolicy(wound, "untreated_infection");
        var outcomes = Route(wound)["outcomes"]!;
        if (kind is "no_improvement" or "add_complication" or "apply_deterioration")
        {
            outcomes[3]!["result"] = new JsonArray(CreateTypedOutcomeOperation(kind));
        }
        else
        {
            outcomes[0]!["result"] = new JsonArray(CreateTypedOutcomeOperation(kind));
        }

        var result = Parse(wound);

        Assert.True(result.IsValid, DescribeIssues(result));
    }

    [Theory]
    [InlineData("missing_policy")]
    [InlineData("mismatched_policy")]
    [InlineData("case_confusable_policy")]
    [InlineData("unicode_confusable_policy")]
    public void Parse_ApplyDeteriorationRequiresTheExactCurrentPolicyReference(
        string mutation)
    {
        var wound = CreateWoundWithStrictRoute("procedure");
        ConfigureCurrentDeteriorationPolicy(wound, "untreated-infection");
        var operation = new JsonObject
        {
            ["kind"] = "apply_deterioration",
            ["policyRef"] = "untreated-infection"
        };
        Route(wound)["outcomes"]![3]!["result"] = new JsonArray(operation);
        switch (mutation)
        {
            case "missing_policy":
                wound["recovery"]!["deteriorationPolicy"] = null;
                break;
            case "mismatched_policy":
                operation["policyRef"] = "different-policy";
                break;
            case "case_confusable_policy":
                operation["policyRef"] = "UNTREATED-INFECTION";
                break;
            case "unicode_confusable_policy":
                operation["policyRef"] = "untreated‐infection";
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null);
        }

        AssertInvalidAt(
            Parse(wound),
            "wound.treatment.routes[0].outcomes[3].result[0].policyRef",
            "wound_materialization_invalid_field");
    }

    [Theory]
    [InlineData("string_token", "wound.treatment.routes[0].outcomes[0].result[0]", "wound_materialization_invalid_field")]
    [InlineData("colon_token", "wound.treatment.routes[0].outcomes[0].result[0]", "wound_materialization_invalid_field")]
    [InlineData("unknown_kind", "wound.treatment.routes[0].outcomes[0].result[0].kind", "wound_materialization_invalid_field")]
    [InlineData("unknown_field", "wound.treatment.routes[0].outcomes[0].result[0].gmOverride", "wound_materialization_unknown_field")]
    [InlineData("missing_points", "wound.treatment.routes[0].outcomes[0].result[0].points", "wound_materialization_missing_field")]
    [InlineData("zero_points", "wound.treatment.routes[0].outcomes[0].result[0].points", "wound_materialization_invalid_field")]
    [InlineData("reduction_three", "wound.treatment.routes[0].outcomes[0].result[0].steps", "wound_materialization_invalid_field")]
    [InlineData("nine_operations", "wound.treatment.routes[0].outcomes[0].result", "wound_materialization_limit_exceeded")]
    [InlineData("mixed_no_improvement", "wound.treatment.routes[0].outcomes[0].result", "wound_materialization_invalid_field")]
    [InlineData("two_heals", "wound.treatment.routes[0].outcomes[0].result", "wound_materialization_invalid_field")]
    [InlineData("operation_after_heal", "wound.treatment.routes[0].outcomes[0].result[1]", "wound_materialization_invalid_field")]
    [InlineData("aggregate_reduction_three", "wound.treatment.routes[0].outcomes[0].result", "wound_materialization_invalid_field")]
    [InlineData("aggregate_reduction_four_with_heal", "wound.treatment.routes[0].outcomes[0].result", "wound_materialization_invalid_field")]
    public void Parse_OutcomeOperationsRejectOpenIncompleteOrUnboundedPayloads(
        string mutation,
        string expectedPath,
        string expectedCode)
    {
        var wound = CreateWoundWithStrictRoute("procedure");
        var outcome = Route(wound)["outcomes"]![0]!;
        switch (mutation)
        {
            case "string_token":
                outcome["result"] = new JsonArray("stabilize");
                break;
            case "colon_token":
                outcome["result"] = new JsonArray("add_recovery:1");
                break;
            case "unknown_kind":
                outcome["result"] = new JsonArray(new JsonObject { ["kind"] = "instant_cure" });
                break;
            case "unknown_field":
                outcome["result"]![0]!["gmOverride"] = true;
                break;
            case "missing_points":
                outcome["result"] = new JsonArray(new JsonObject { ["kind"] = "add_recovery" });
                break;
            case "zero_points":
                outcome["result"] = new JsonArray(new JsonObject
                {
                    ["kind"] = "add_recovery",
                    ["points"] = 0
                });
                break;
            case "reduction_three":
                outcome["result"] = new JsonArray(new JsonObject
                {
                    ["kind"] = "reduce_severity",
                    ["steps"] = 3
                });
                break;
            case "nine_operations":
                outcome["result"] = WoundContractTestData.Repeat(
                    9,
                    _ => new JsonObject { ["kind"] = "stabilize" });
                break;
            case "mixed_no_improvement":
                outcome["result"] = new JsonArray(
                    new JsonObject { ["kind"] = "no_improvement" },
                    new JsonObject { ["kind"] = "stabilize" });
                break;
            case "two_heals":
                outcome["result"] = new JsonArray(CreateHealOperation(), CreateHealOperation());
                break;
            case "operation_after_heal":
                outcome["result"] = new JsonArray(
                    CreateHealOperation(),
                    new JsonObject { ["kind"] = "stabilize" });
                break;
            case "aggregate_reduction_three":
                outcome["result"] = new JsonArray(
                    new JsonObject { ["kind"] = "reduce_severity", ["steps"] = 2 },
                    new JsonObject { ["kind"] = "reduce_severity", ["steps"] = 1 });
                break;
            case "aggregate_reduction_four_with_heal":
                outcome["result"] = new JsonArray(
                    new JsonObject { ["kind"] = "reduce_severity", ["steps"] = 2 },
                    new JsonObject { ["kind"] = "reduce_severity", ["steps"] = 2 },
                    CreateHealOperation());
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null);
        }

        AssertInvalidAt(Parse(wound), expectedPath, expectedCode);
    }

    [Fact]
    public void Parse_OutcomeAlgebraAcceptsEveryInclusivePositiveBoundary()
    {
        var cases = new List<JsonArray>
        {
            WoundContractTestData.Repeat(
                8,
                _ => new JsonObject { ["kind"] = "stabilize" }),
            new JsonArray(new JsonObject
            {
                ["kind"] = "add_recovery",
                ["points"] = int.MaxValue
            }),
            new JsonArray(
                new JsonObject { ["kind"] = "reduce_severity", ["steps"] = 2 },
                new JsonObject { ["kind"] = "reduce_severity", ["steps"] = 1 },
                CreateHealOperation()),
            new JsonArray(CreateHealOperation(
                Enumerable.Range(0, 8)
                    .Select(index => CreateCosmeticHealLegacy($"legacy_{index}"))
                    .ToArray()))
        };

        foreach (var declaredResult in cases)
        {
            var wound = CreateWoundWithStrictRoute("procedure");
            Route(wound)["outcomes"]![0]!["result"] = declaredResult;

            var result = Parse(wound);

            Assert.True(result.IsValid, DescribeIssues(result));
        }
    }

    [Fact]
    public void Parse_ProcedureBandsAcceptInclusiveSignedInt64FiniteBounds()
    {
        var wound = CreateWoundWithStrictRoute("procedure");
        var outcomes = Route(wound)["outcomes"]!;
        outcomes[0]!["minimumMargin"] = long.MaxValue;
        outcomes[1]!["minimumMargin"] = 0;
        outcomes[1]!["maximumMargin"] = long.MaxValue - 1;
        outcomes[2]!["minimumMargin"] = long.MinValue + 1;
        outcomes[2]!["maximumMargin"] = -1;
        outcomes[3]!["maximumMargin"] = long.MinValue;

        var result = Parse(wound);

        Assert.True(result.IsValid, DescribeIssues(result));
    }

    [Fact]
    public void Parse_RemoveComplicationRouteRemainsStructurallyValidAfterTargetIsGone()
    {
        var wound = CreateWoundWithStrictRoute("procedure");
        Route(wound)["outcomes"]![0]!["result"] = new JsonArray(new JsonObject
        {
            ["kind"] = "remove_complication",
            ["complicationId"] = "complication_already_removed"
        });

        var result = Parse(wound);

        Assert.True(result.IsValid, DescribeIssues(result));
    }

    [Theory]
    [InlineData("course")]
    [InlineData("guaranteed")]
    [InlineData("complication")]
    [InlineData("legacy")]
    [InlineData("mixed_interruption")]
    public void CanonicalRoundTrip_UsesTypedWriterForEveryTreatmentPayloadFamily(
        string scenario)
    {
        JsonObject wound;
        switch (scenario)
        {
            case "course":
                wound = CreateWoundWithStrictRoute("course");
                break;
            case "guaranteed":
                wound = CreateWoundWithStrictRoute("guaranteed");
                Route(wound)["requirements"]!.AsArray().Add(new JsonObject
                {
                    ["kind"] = "provider",
                    ["providerRef"] = "shrine_healer"
                });
                break;
            case "complication":
                wound = CreateWoundWithStrictRoute("procedure");
                var complication = CreateEffectlessComplicationOperation();
                complication["complicationDraft"]!["consequenceDefinitions"] =
                    CreateEffectfulComplicationDefinitions();
                Route(wound)["outcomes"]![3]!["result"] = new JsonArray(complication);
                break;
            case "legacy":
                wound = CreateWoundWithStrictRoute("procedure");
                Route(wound)["outcomes"]![0]!["result"] = new JsonArray(
                    new JsonObject { ["kind"] = "reduce_severity", ["steps"] = 1 },
                    CreateHealOperation(CreateMechanicalHealLegacy("typed_legacy")));
                break;
            case "mixed_interruption":
                wound = CreateWoundWithStrictRoute("course");
                ConfigureCurrentDeteriorationPolicy(wound, "missed-course-dose");
                Route(wound)["interruption"]!["result"] = new JsonArray(
                    CreateEffectlessComplicationOperation(),
                    new JsonObject
                    {
                        ["kind"] = "apply_deterioration",
                        ["policyRef"] = "missed-course-dose"
                    });
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null);
        }

        var first = Parse(wound);
        Assert.True(first.IsValid, DescribeIssues(first));
        var canonical = WoundMaterializationContract.SerializeCanonical(first.Wound!);
        var reparsed = WoundMaterializationContract.Parse(canonical, Path);
        Assert.True(reparsed.IsValid, DescribeIssues(reparsed));

        Assert.Equal(
            canonical,
            WoundMaterializationContract.SerializeCanonical(reparsed.Wound!));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Parse_AddComplicationReusesTheCurrentGmSafeSubproposal(bool effectful)
    {
        var wound = CreateWoundWithStrictRoute("procedure");
        var operation = CreateEffectlessComplicationOperation();
        if (effectful)
        {
            operation["complicationDraft"]!["consequenceDefinitions"] =
                CreateEffectfulComplicationDefinitions();
        }

        Route(wound)["outcomes"]![3]!["result"] = new JsonArray(operation);

        var result = Parse(wound);

        Assert.True(result.IsValid, DescribeIssues(result));
    }

    [Theory]
    [InlineData("all_roots_null", "wound.treatment.routes[0].outcomes[3].result[0].complicationDraft.consequenceDefinitions")]
    [InlineData("wrong_slot_profile", "wound.treatment.routes[0].outcomes[3].result[0].complicationDraft.consequenceDefinitions[0].root.slots[0].profileKey")]
    [InlineData("root_requires_parameters", "wound.treatment.routes[0].outcomes[3].result[0].complicationDraft.consequenceDefinitions[0].definition.parameterBounds")]
    [InlineData("non_single_stack", "wound.treatment.routes[0].outcomes[3].result[0].complicationDraft.consequenceDefinitions[0].definition.stacking.maxStacks")]
    [InlineData("independent_changes_at_maximum", "wound.treatment.routes[0].outcomes[3].result[0].complicationDraft.consequenceDefinitions[0].definition.stacking.atMaximum")]
    [InlineData("irrelevant_refresh_mode", "wound.treatment.routes[0].outcomes[3].result[0].complicationDraft.consequenceDefinitions[0].definition.stacking.refreshMode")]
    [InlineData("irrelevant_merge_rule", "wound.treatment.routes[0].outcomes[3].result[0].complicationDraft.consequenceDefinitions[0].definition.stacking.mergeRule")]
    [InlineData("duplicate_stack_key", "wound.treatment.routes[0].outcomes[3].result[0].complicationDraft.consequenceDefinitions[1].definition.stacking.stackKey")]
    [InlineData("confusable_stack_key", "wound.treatment.routes[0].outcomes[3].result[0].complicationDraft.consequenceDefinitions[1].definition.stacking.stackKey")]
    [InlineData("non_wound_source_predicate", "wound.treatment.routes[0].outcomes[3].result[0].complicationDraft.consequenceDefinitions[0].definition.lifetime.activePredicate")]
    [InlineData("wrong_owner_target_kind", "wound.treatment.routes[0].outcomes[3].result[0].complicationDraft.consequenceDefinitions[0].definition.allowedTargetKinds")]
    [InlineData("root_bound_reaction_target_not_replace", "wound.treatment.routes[0].outcomes[3].result[0].complicationDraft.consequenceDefinitions[1].definition.stacking.policy")]
    [InlineData("root_bound_reaction_nonempty_parameters", "wound.treatment.routes[0].outcomes[3].result[0].complicationDraft.consequenceDefinitions[0].definition.components[0].payload.parameters")]
    [InlineData("severity_forbid", "wound.treatment.routes[0].outcomes[3].result[0].complicationDraft.consequenceDefinitions[0].definition.components[0].payload.operation")]
    [InlineData("duplicate_mechanical_coordinate", "wound.treatment.routes[0].outcomes[3].result[0].complicationDraft.consequenceDefinitions[1].definition.components[0].payload.action")]
    [InlineData("rooted_plus_orphan", "wound.treatment.routes[0].outcomes[3].result[0].complicationDraft.consequenceDefinitions[1].definition")]
    [InlineData("aggregate_slot_overflow", "wound.treatment.routes[0].outcomes[3].result[0].complicationDraft.consequenceDefinitions")]
    [InlineData("authored_marker_wound_id", "wound.treatment.routes[0].outcomes[3].result[0].complicationDraft.consequenceDefinitions[0].definition.components[0].payload.woundId")]
    public void Parse_EffectfulComplicationRequiresOneCompleteWoundOwnedGraph(
        string mutation,
        string expectedPath)
    {
        var wound = CreateWoundWithStrictRoute("procedure");
        var operation = CreateEffectlessComplicationOperation();
        var definitions = CreateEffectfulComplicationDefinitions();
        operation["complicationDraft"]!["consequenceDefinitions"] = definitions;
        switch (mutation)
        {
            case "all_roots_null":
                definitions[0]!["root"] = null;
                break;
            case "wrong_slot_profile":
                definitions[0]!["root"]!["slots"]![0]!["profileKey"] =
                    "periodic_damage";
                break;
            case "root_requires_parameters":
                definitions[0]!["definition"]!["parameterBounds"] = new JsonObject
                {
                    ["action"] = new JsonObject
                    {
                        ["kind"] = "enum",
                        ["allowedValues"] = new JsonArray("movement"),
                        ["required"] = true
                    }
                };
                break;
            case "non_single_stack":
                definitions[0]!["definition"]!["stacking"]!["maxStacks"] = 2;
                break;
            case "independent_changes_at_maximum":
                definitions[0]!["definition"]!["stacking"]!["atMaximum"] = "refresh";
                break;
            case "irrelevant_refresh_mode":
                definitions[0]!["definition"]!["stacking"]!["refreshMode"] = "reset";
                break;
            case "irrelevant_merge_rule":
                definitions[0]!["definition"]!["stacking"]!["mergeRule"] = "sum";
                break;
            case "duplicate_stack_key":
                AddSecondRootDefinition(definitions, reuseStackKey: true);
                break;
            case "confusable_stack_key":
                definitions[0]!["definition"]!["stacking"]!["stackKey"] =
                    "stack-irritation";
                AddSecondRootDefinition(
                    definitions,
                    reuseStackKey: false,
                    stackKey: "stack‐irritation");
                break;
            case "non_wound_source_predicate":
                definitions[0]!["definition"]!["lifetime"]!["activePredicate"] =
                    "equipped";
                break;
            case "wrong_owner_target_kind":
                definitions[0]!["definition"]!["allowedTargetKinds"] =
                    new JsonArray("npc");
                break;
            case "root_bound_reaction_target_not_replace":
                ReplaceDefinitions(
                    definitions,
                    CreateRootBoundReactionComplicationDefinitions(
                        targetPolicy: "independent"));
                wound["severity"]!["value"] = "III";
                wound["severity"]!["rank"] = 3;
                wound["severity"]!["maximumAtCreation"] = "III";
                break;
            case "root_bound_reaction_nonempty_parameters":
                ReplaceDefinitions(
                    definitions,
                    CreateRootBoundReactionComplicationDefinitions(
                        targetPolicy: "replace"));
                definitions[0]!["definition"]!["components"]![0]!["payload"]!["parameters"] =
                    new JsonObject { ["mode"] = "unsafe" };
                wound["severity"]!["value"] = "III";
                wound["severity"]!["rank"] = 3;
                wound["severity"]!["maximumAtCreation"] = "III";
                break;
            case "severity_forbid":
                definitions[0]!["definition"]!["components"]![0]!["payload"]!["operation"] =
                    "forbid";
                break;
            case "duplicate_mechanical_coordinate":
                AddSecondRootDefinition(definitions, reuseStackKey: false);
                break;
            case "rooted_plus_orphan":
            {
                var orphan = definitions[0]!.DeepClone().AsObject();
                orphan["definitionRef"] = "orphan_irritation_limit";
                orphan["definition"]!["definitionKey"] = "orphan_irritation_limit";
                orphan["definition"]!["stacking"]!["stackKey"] =
                    "stack_orphan_irritation_limit";
                orphan["root"] = null;
                definitions.Add(orphan);
                break;
            }
            case "aggregate_slot_overflow":
            {
                var template = definitions[0]!.DeepClone().AsObject();
                for (var index = 1; index < 5; index++)
                {
                    var wrapper = template.DeepClone().AsObject();
                    wrapper["definitionRef"] = $"irritation_grip_limit_{index}";
                    wrapper["definition"]!["definitionKey"] =
                        $"irritation_grip_limit_{index}";
                    wrapper["definition"]!["stacking"]!["stackKey"] =
                        $"stack_irritation_grip_limit_{index}";
                    definitions.Add(wrapper);
                }
                break;
            }
            case "authored_marker_wound_id":
                definitions[0] = CreateMarkerComplicationDefinition(
                    authoredWoundId: "caller_forged_wound");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null);
        }
        Route(wound)["outcomes"]![3]!["result"] = new JsonArray(operation);

        AssertInvalidAt(
            Parse(wound),
            expectedPath,
            "wound_materialization_invalid_field");
    }

    [Fact]
    public void Parse_EffectfulComplicationMissingRoot_PreservesExactEstablishedDiagnostics()
    {
        var wound = CreateWoundWithStrictRoute("procedure");
        var operation = CreateEffectlessComplicationOperation();
        var definitions = CreateEffectfulComplicationDefinitions();
        AddSecondRootDefinition(definitions, reuseStackKey: false);
        definitions[0]!["definition"] = null;
        operation["complicationDraft"]!["consequenceDefinitions"] = definitions;
        Route(wound)["outcomes"]![3]!["result"] = new JsonArray(operation);

        var result = Parse(wound);

        Assert.Equal(
            new[]
            {
                "wound_materialization_invalid_field@wound.treatment.routes[0].outcomes[3].result[0].complicationDraft.consequenceDefinitions[0].definition",
                "wound_materialization_invalid_field@wound.treatment.routes[0].outcomes[3].result[0].complicationDraft.consequenceDefinitions[0].root",
            },
            IssueCoordinates(result));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Parse_EffectfulComplicationDuplicateOrConfusableDefinitionKey_PreservesExactEstablishedDiagnostics(
        bool confusable)
    {
        var wound = CreateWoundWithStrictRoute("procedure");
        var operation = CreateEffectlessComplicationOperation();
        var definitions = CreateEffectfulComplicationDefinitions();
        AddSecondRootDefinition(definitions, reuseStackKey: false);
        definitions[0]!["definition"]!["definitionKey"] = "irritation-grip-limit";
        definitions[1]!["definition"]!["definitionKey"] = confusable
            ? "irritation‐grip‐limit"
            : "irritation-grip-limit";
        operation["complicationDraft"]!["consequenceDefinitions"] = definitions;
        Route(wound)["outcomes"]![3]!["result"] = new JsonArray(operation);

        var result = Parse(wound);

        var expected = new List<string>
        {
            "wound_materialization_invalid_field@wound.treatment.routes[0].outcomes[3].result[0].complicationDraft.consequenceDefinitions[1].definition.definitionKey"
        };
        if (!confusable)
        {
            expected.Add(
                "wound_materialization_invalid_field@wound.treatment.routes[0].outcomes[3].result[0].complicationDraft.consequenceDefinitions[1].root");
        }
        expected.Add(
            "wound_materialization_invalid_field@wound.treatment.routes[0].outcomes[3].result[0].complicationDraft.consequenceDefinitions[1].definition.components[0].payload.action");
        Assert.Equal(expected, IssueCoordinates(result));
    }

    [Fact]
    public void Parse_EffectfulComplicationMultiErrorGraph_SkipsMissingRootAndValidatesResolvableRoot()
    {
        var wound = CreateWoundWithStrictRoute("procedure");
        var operation = CreateEffectlessComplicationOperation();
        var definitions = CreateEffectfulComplicationDefinitions();
        AddSecondRootDefinition(definitions, reuseStackKey: false);
        definitions[0]!["definition"] = null;
        definitions[1]!["definition"]!["components"]![0]!["payload"]!["operation"] =
            "forbid";
        operation["complicationDraft"]!["consequenceDefinitions"] = definitions;
        Route(wound)["outcomes"]![3]!["result"] = new JsonArray(operation);

        var result = Parse(wound);

        Assert.Equal(
            new[]
            {
                "wound_materialization_invalid_field@wound.treatment.routes[0].outcomes[3].result[0].complicationDraft.consequenceDefinitions[0].definition",
                "wound_materialization_invalid_field@wound.treatment.routes[0].outcomes[3].result[0].complicationDraft.consequenceDefinitions[0].root",
                "wound_materialization_invalid_field@wound.treatment.routes[0].outcomes[3].result[0].complicationDraft.consequenceDefinitions[1].definition.components[0].payload.operation",
            },
            IssueCoordinates(result));
    }

    [Fact]
    public void Parse_EffectfulComplicationAcceptsClientBoundWoundMarker()
    {
        var wound = CreateWoundWithStrictRoute("procedure");
        var operation = CreateEffectlessComplicationOperation();
        operation["complicationDraft"]!["consequenceDefinitions"] =
            new JsonArray(CreateMarkerComplicationDefinition(authoredWoundId: null));
        Route(wound)["outcomes"]![3]!["result"] = new JsonArray(operation);

        var result = Parse(wound);

        Assert.True(result.IsValid, DescribeIssues(result));
    }

    [Fact]
    public void Parse_EffectfulComplicationAcceptsRootBoundReplaceReactionTarget()
    {
        var wound = CreateWoundWithStrictRoute("procedure");
        wound["severity"]!["value"] = "III";
        wound["severity"]!["rank"] = 3;
        wound["severity"]!["maximumAtCreation"] = "III";
        var operation = CreateEffectlessComplicationOperation();
        operation["complicationDraft"]!["consequenceDefinitions"] =
            CreateRootBoundReactionComplicationDefinitions(targetPolicy: "replace");
        Route(wound)["outcomes"]![3]!["result"] = new JsonArray(operation);

        var result = Parse(wound);

        Assert.True(result.IsValid, DescribeIssues(result));
    }

    [Fact]
    public void Parse_EffectfulComplicationValidatesMaterializedReactionChildMagnitude()
    {
        var wound = CreateWoundWithStrictRoute("procedure");
        wound["severity"]!["value"] = "III";
        wound["severity"]!["rank"] = 3;
        wound["severity"]!["maximumAtCreation"] = "III";
        var operation = CreateEffectlessComplicationOperation();
        var producer = WoundContractTestData.CreateApplyDefinitionRoot(
            "wound_test_torn_side",
            "mortal_world",
            "irritation_reaction",
            "irritation_resistance_leaf");
        producer["links"] = new JsonArray();
        producer["components"]![0]!["payload"]!["parameters"] =
            new JsonObject { ["value"] = 21 };
        var leaf = WoundContractTestData.CreateOwnedEffectDefinition(
            "wound_test_torn_side",
            "mortal_world",
            "irritation_resistance_leaf",
            "resistance_modifier");
        leaf["links"] = new JsonArray();
        leaf["components"]![0]!["payload"]!.AsObject().Remove("cap");
        leaf["parameterBounds"] = new JsonObject
        {
            ["value"] = new JsonObject
            {
                ["kind"] = "number",
                ["minimum"] = -100,
                ["maximum"] = 100
            }
        };
        operation["complicationDraft"]!["consequenceDefinitions"] = new JsonArray(
            new JsonObject
            {
                ["definitionRef"] = "irritation_reaction",
                ["definition"] = producer,
                ["root"] = new JsonObject
                {
                    ["ownership"] = new JsonObject
                    {
                        ["kind"] = "complication",
                        ["complicationRef"] = "irritation"
                    },
                    ["slots"] = new JsonArray(
                        new JsonObject
                        {
                            ["profileKey"] = "event_reaction",
                            ["readableSummary"] = "Боль запускает связанное ограничение."
                        },
                        new JsonObject
                        {
                            ["profileKey"] = "resistance_modifier",
                            ["readableSummary"] = "Раздражение резко снижает сопротивление."
                        })
                }
            },
            new JsonObject
            {
                ["definitionRef"] = "irritation_resistance_leaf",
                ["definition"] = leaf,
                ["root"] = null
            });
        Route(wound)["outcomes"]![3]!["result"] = new JsonArray(operation);

        AssertInvalidAt(
            Parse(wound),
            "wound.treatment.routes[0].outcomes[3].result[0].complicationDraft.consequenceDefinitions[1].definition.components[0].payload.value",
            "wound_materialization_invalid_field");
    }

    [Theory]
    [InlineData("parallel_effect_draft", "wound.treatment.routes[0].outcomes[3].result[0].complicationDraft.ownedEffectDraft")]
    [InlineData("two_complications", "wound.treatment.routes[0].outcomes[3].result[0].complicationDraft.complications")]
    [InlineData("wrong_owner_ref", "wound.treatment.routes[0].outcomes[3].result[0].complicationDraft.consequenceDefinitions[0].root.ownership.complicationRef")]
    [InlineData("nonempty_links", "wound.treatment.routes[0].outcomes[3].result[0].complicationDraft.consequenceDefinitions[0].definition.links")]
    [InlineData("negative_difficulty", "wound.treatment.routes[0].outcomes[3].result[0].complicationDraft.complications[0].treatmentDifficultyModifier")]
    [InlineData("difficulty_above_common_maximum", "wound.treatment.routes[0].outcomes[3].result[0].complicationDraft.complications[0].treatmentDifficultyModifier")]
    [InlineData("duplicate_ref", "wound.treatment.routes[0].outcomes[3].result[1].complicationDraft.complications[0].complicationRef")]
    [InlineData("case_changed_ref", "wound.treatment.routes[0].outcomes[3].result[1].complicationDraft.complications[0].complicationRef")]
    [InlineData("confusable_ref", "wound.treatment.routes[0].outcomes[3].result[1].complicationDraft.complications[0].complicationRef")]
    public void Parse_AddComplicationRejectsParallelShapesAndAmbiguousLocalRefs(
        string mutation,
        string expectedPath)
    {
        var wound = CreateWoundWithStrictRoute("procedure");
        var first = CreateEffectlessComplicationOperation();
        var result = new JsonArray(first);
        Route(wound)["outcomes"]![3]!["result"] = result;
        switch (mutation)
        {
            case "parallel_effect_draft":
                first["complicationDraft"]!["ownedEffectDraft"] = new JsonObject();
                break;
            case "two_complications":
            {
                var second = first["complicationDraft"]!["complications"]![0]!
                    .DeepClone().AsObject();
                second["complicationRef"] = "second_irritation";
                first["complicationDraft"]!["complications"]!.AsArray().Add(second);
                break;
            }
            case "wrong_owner_ref":
                first["complicationDraft"]!["consequenceDefinitions"] =
                    CreateEffectfulComplicationDefinitions();
                first["complicationDraft"]!["consequenceDefinitions"]![0]!["root"]!["ownership"]!["complicationRef"] =
                    "another_complication";
                break;
            case "nonempty_links":
                first["complicationDraft"]!["consequenceDefinitions"] =
                    CreateEffectfulComplicationDefinitions();
                first["complicationDraft"]!["consequenceDefinitions"]![0]!["definition"]!["links"] =
                    new JsonArray(new JsonObject { ["kind"] = "wound" });
                break;
            case "negative_difficulty":
                first["complicationDraft"]!["complications"]![0]!["treatmentDifficultyModifier"] = -1;
                break;
            case "difficulty_above_common_maximum":
                first["complicationDraft"]!["complications"]![0]!["treatmentDifficultyModifier"] = 5;
                break;
            case "duplicate_ref":
                result.Add(CreateEffectlessComplicationOperation());
                break;
            case "case_changed_ref":
                first["complicationDraft"]!["complications"]![0]!["complicationRef"] = "irritation-edge";
                var caseChanged = CreateEffectlessComplicationOperation();
                caseChanged["complicationDraft"]!["complications"]![0]!["complicationRef"] = "IRRITATION-EDGE";
                result.Add(caseChanged);
                break;
            case "confusable_ref":
                first["complicationDraft"]!["complications"]![0]!["complicationRef"] = "irritation-edge";
                var confusable = CreateEffectlessComplicationOperation();
                confusable["complicationDraft"]!["complications"]![0]!["complicationRef"] = "irritation‐edge";
                result.Add(confusable);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null);
        }

        AssertInvalidAt(
            Parse(wound),
            expectedPath,
            mutation == "parallel_effect_draft"
                ? "wound_materialization_unknown_field"
                : "wound_materialization_invalid_field");
    }

    [Fact]
    public void Parse_CanonicalRemoveComplicationAcceptsOnlyPermanentComplicationId()
    {
        var canonical = CreateWoundWithStrictRoute("procedure");
        AddExistingIrritationComplication(canonical);
        Route(canonical)["outcomes"]![0]!["result"] = new JsonArray(new JsonObject
        {
            ["kind"] = "remove_complication",
            ["complicationId"] = "complication_wound_test_irritation"
        });

        var canonicalResult = Parse(canonical);
        Assert.True(canonicalResult.IsValid, DescribeIssues(canonicalResult));

        var proposalDialect = canonical.DeepClone().AsObject();
        var operation = Route(proposalDialect)["outcomes"]![0]!["result"]![0]!.AsObject();
        operation.Remove("complicationId");
        operation["complicationRef"] = "irritation";

        AssertInvalidAt(
            Parse(proposalDialect),
            "wound.treatment.routes[0].outcomes[0].result[0].complicationRef",
            "wound_materialization_unknown_field");
    }

    [Theory]
    [InlineData("public")]
    [InlineData("known_to_player")]
    [InlineData("hidden")]
    [InlineData("gm_only")]
    public void Parse_AddComplicationReusesCompleteComplicationVisibilityVocabulary(
        string visibility)
    {
        var wound = CreateWoundWithStrictRoute("procedure");
        var operation = CreateEffectlessComplicationOperation();
        operation["complicationDraft"]!["complications"]![0]!["visibility"] = visibility;
        Route(wound)["outcomes"]![3]!["result"] = new JsonArray(operation);

        var result = Parse(wound);

        Assert.True(result.IsValid, DescribeIssues(result));
    }

    [Fact]
    public void ProposalComposition_RewritesSameProposalComplicationRefToCanonicalId()
    {
        var (binding, opportunity) = CreateTreatmentProposalOpportunity();
        var decision = new JsonObject
        {
            ["opportunityRef"] = opportunity.PublicRef,
            ["decision"] = "materialize",
            ["woundRef"] = "wound_local_treatment_rewrite",
            ["proposal"] = CreateTreatmentProposalWithLocalComplicationRemoval()
        };

        var composition = WoundResponseInputComposer.Compose(
            binding,
            new[] { opportunity },
            new[] { JsonSerializer.SerializeToElement(decision) },
            "Острый край распорол бок в короткой схватке. Рана и способ лечения подтверждены.",
            Array.Empty<WoundOpportunityDecisionReceipt>());

        Assert.True(composition.Success, DescribeIssues(composition.Issues));
        var proposed = Assert.Single(composition.Transitions).ProposedAfter;
        var complicationId = Assert.Single(proposed.Complications).ComplicationId;
        var remove = Assert.Single(proposed.Treatment.Routes)
            .Outcomes[0]
            .GetProperty("result")[0];
        Assert.Equal(
            complicationId,
            remove.GetProperty("complicationId").GetString());
        Assert.False(remove.TryGetProperty("complicationRef", out _));
    }

    [Fact]
    public void ProposalComposition_WorseningPreservesSignedCanonicalComplicationSelector()
    {
        var (binding, createOpportunity) = CreateTreatmentProposalOpportunity();
        var createProposal = CreateTreatmentProposalWithLocalComplicationRemoval();
        createProposal["severity"] = "I";
        var createDecision = new JsonObject
        {
            ["opportunityRef"] = createOpportunity.PublicRef,
            ["decision"] = "materialize",
            ["woundRef"] = "wound_local_before_worsening",
            ["proposal"] = createProposal
        };
        var created = WoundResponseInputComposer.Compose(
            binding,
            new[] { createOpportunity },
            new[] { JsonSerializer.SerializeToElement(createDecision) },
            "Острый край распорол бок в короткой схватке. Рана подтверждена.",
            Array.Empty<WoundOpportunityDecisionReceipt>());
        Assert.True(created.Success, DescribeIssues(created.Issues));
        var before = Assert.Single(created.Transitions).ProposedAfter;
        var complicationId = Assert.Single(before.Complications).ComplicationId;

        var (_, worseningOpportunity) = CreateTreatmentProposalOpportunity(before);
        var worseningProposal = CreateTreatmentProposalWithLocalComplicationRemoval();
        var canonicalBefore = JsonNode.Parse(
            WoundMaterializationContract.SerializeCanonical(before))!.AsObject();
        worseningProposal["treatment"] = canonicalBefore["treatment"]!.DeepClone();
        var worseningDecision = new JsonObject
        {
            ["opportunityRef"] = worseningOpportunity.PublicRef,
            ["decision"] = "materialize",
            ["woundRef"] = "wound_local_after_worsening",
            ["proposal"] = worseningProposal
        };

        var worsened = WoundResponseInputComposer.Compose(
            binding,
            new[] { worseningOpportunity },
            new[] { JsonSerializer.SerializeToElement(worseningDecision) },
            "Острый край распорол бок в короткой схватке. Повторная травма ухудшила рану.",
            Array.Empty<WoundOpportunityDecisionReceipt>());

        Assert.True(worsened.Success, DescribeIssues(worsened.Issues));
        var after = Assert.Single(worsened.Transitions).ProposedAfter;
        var remove = Assert.Single(after.Treatment.Routes).Outcomes[0]
            .GetProperty("result")[0];
        Assert.Equal(complicationId, remove.GetProperty("complicationId").GetString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ProposalComposition_InvalidLinksPreserveDuplicateDefinitionDiagnostic(bool missingLinks)
    {
        var (binding, opportunity) = CreateTreatmentProposalOpportunity();
        var proposal = CreateTreatmentProposalWithLocalComplicationRemoval();
        var first = WoundContractTestData.CreateOwnedEffectDefinition(
            "wound_test_torn_side", "mortal_world", "duplicate_definition_key", "action_control");
        if (missingLinks) first.Remove("links");
        var second = first.DeepClone().AsObject();
        second["links"] = new JsonArray();
        proposal["consequenceDefinitions"] = new JsonArray(
            new JsonObject { ["definitionRef"] = "first_local", ["definition"] = first, ["root"] = null },
            new JsonObject { ["definitionRef"] = "second_local", ["definition"] = second, ["root"] = null });
        var decision = new JsonObject
        {
            ["opportunityRef"] = opportunity.PublicRef, ["decision"] = "materialize",
            ["woundRef"] = "wound_local_definition_diagnostics", ["proposal"] = proposal
        };

        var composition = WoundResponseInputComposer.Compose(binding, new[] { opportunity },
            new[] { JsonSerializer.SerializeToElement(decision) },
            "Острый край распорол бок в короткой схватке. Рана подтверждена.",
            Array.Empty<WoundOpportunityDecisionReceipt>());

        Assert.False(composition.Success);
        Assert.Empty(composition.Transitions);
        Assert.Contains(composition.Issues, issue =>
            issue.FilePath == "woundDecisions[0].proposal.consequenceDefinitions[0].definition.links" &&
            issue.Code == "wound_response_client_authority_forbidden");
        Assert.Contains(composition.Issues, issue =>
            issue.FilePath == "woundDecisions[0].proposal.consequenceDefinitions" &&
            issue.Code == "wound_response_duplicate_definition");
        var diagnostics = composition.Issues.ToList();
        Assert.True(diagnostics.FindIndex(issue => issue.Code == "wound_response_client_authority_forbidden") <
            diagnostics.FindIndex(issue => issue.Code == "wound_response_duplicate_definition"));
    }

    [Fact]
    public void ProposalComposition_RejectsUnicodeDashConfusableComplicationRefs()
    {
        var (binding, opportunity) = CreateTreatmentProposalOpportunity();
        var proposal = CreateTreatmentProposalWithLocalComplicationRemoval();
        var complications = proposal["complications"]!.AsArray();
        complications[0]!["complicationRef"] = "irritation-edge";
        var second = complications[0]!.DeepClone().AsObject();
        second["complicationRef"] = "irritation‐edge";
        complications.Add(second);
        proposal["treatment"]!["routes"]![0]!["outcomes"]![0]!["result"]![0]!["complicationRef"] =
            "irritation-edge";
        var decision = new JsonObject
        {
            ["opportunityRef"] = opportunity.PublicRef,
            ["decision"] = "materialize",
            ["woundRef"] = "wound_local_confusable_complications",
            ["proposal"] = proposal
        };

        var composition = WoundResponseInputComposer.Compose(
            binding,
            new[] { opportunity },
            new[] { JsonSerializer.SerializeToElement(decision) },
            "Острый край распорол бок в короткой схватке. Неоднозначные ссылки отклонены.",
            Array.Empty<WoundOpportunityDecisionReceipt>());

        Assert.False(composition.Success);
        Assert.Contains(composition.Issues, issue =>
            issue.FilePath == "woundDecisions[0].proposal.complications" &&
            issue.Code == "wound_response_duplicate_local_reference");
    }

    [Fact]
    public void ProposalComposition_RejectsCallerAuthoredCanonicalComplicationId()
    {
        var (binding, opportunity) = CreateTreatmentProposalOpportunity();
        var proposal = CreateTreatmentProposalWithLocalComplicationRemoval();
        var operation = proposal["treatment"]!["routes"]![0]!["outcomes"]![0]!["result"]![0]!
            .AsObject();
        operation.Remove("complicationRef");
        operation["complicationId"] = "complication_forged_by_proposal";
        var decision = new JsonObject
        {
            ["opportunityRef"] = opportunity.PublicRef,
            ["decision"] = "materialize",
            ["woundRef"] = "wound_local_treatment_forged_id",
            ["proposal"] = proposal
        };

        var composition = WoundResponseInputComposer.Compose(
            binding,
            new[] { opportunity },
            new[] { JsonSerializer.SerializeToElement(decision) },
            "Попытка передать постоянный ID отклонена.",
            Array.Empty<WoundOpportunityDecisionReceipt>());

        Assert.False(composition.Success);
        Assert.Contains(composition.Issues, issue =>
            issue.FilePath ==
                "woundDecisions[0].proposal.treatment.routes[0].outcomes[0].result[0].complicationId" &&
            issue.Code == "wound_response_unknown_field");
    }

    [Theory]
    [InlineData("recoveryAnchor")]
    [InlineData("deteriorationAnchor")]
    public void ProposalComposition_RejectsClientOwnedRecoveryAnchorsBeforeCanonicalParsing(
        string anchorField)
    {
        var (binding, opportunity) = CreateTreatmentProposalOpportunity();
        var proposal = CreateTreatmentProposalWithLocalComplicationRemoval();
        proposal["recovery"]![anchorField] = new JsonObject
        {
            [anchorField == "recoveryAnchor" ? "anchorKind" : "conditionKey"] =
                anchorField == "recoveryAnchor" ? "creation" : "not_stabilized",
            ["anchorMinute"] = 100,
            ["anchorTransitionId"] = "forged_anchor"
        };
        var decision = new JsonObject
        {
            ["opportunityRef"] = opportunity.PublicRef,
            ["decision"] = "materialize",
            ["woundRef"] = "wound_local_forged_recovery_anchor",
            ["proposal"] = proposal
        };

        var composition = WoundResponseInputComposer.Compose(
            binding,
            new[] { opportunity },
            new[] { JsonSerializer.SerializeToElement(decision) },
            "Острый край распорол бок; поддельная точка восстановления отклонена.",
            Array.Empty<WoundOpportunityDecisionReceipt>());

        Assert.False(composition.Success);
        Assert.Contains(composition.Issues, issue =>
            issue.FilePath == "woundDecisions[0].proposal.recovery." + anchorField &&
            issue.Code == "wound_materialization_client_owned_field");
    }

    [Theory]
    [InlineData("recoveryAnchor")]
    [InlineData("deteriorationAnchor")]
    public void ProposalComposition_StripsNullClientOwnedRecoveryAnchorPlaceholders(
        string anchorField)
    {
        const string scene = "Острый край распорол бок в короткой схватке. Пустой служебный якорь очищен клиентом.";
        var (binding, opportunity) = CreateTreatmentProposalOpportunity();
        var proposal = CreateTreatmentProposalWithLocalComplicationRemoval();
        proposal["recovery"]![anchorField] = null;
        var decision = new JsonObject
        {
            ["opportunityRef"] = opportunity.PublicRef,
            ["decision"] = "materialize",
            ["woundRef"] = "wound_local_null_recovery_anchor",
            ["proposal"] = proposal
        };

        var composition = WoundResponseInputComposer.Compose(
            binding,
            new[] { opportunity },
            new[] { JsonSerializer.SerializeToElement(decision) },
            scene,
            Array.Empty<WoundOpportunityDecisionReceipt>());

        Assert.True(composition.Success, DescribeIssues(composition.Issues));
        var proposed = Assert.Single(composition.Transitions).ProposedAfter;
        Assert.Null(proposed.Recovery.RecoveryAnchor);
        Assert.Null(proposed.Recovery.DeteriorationAnchor);
        var canonicalJson = WoundMaterializationContract.SerializeCanonical(proposed);
        var canonical = JsonNode.Parse(canonicalJson)!.AsObject();
        var recovery = canonical["recovery"]!.AsObject();
        Assert.True(recovery.TryGetPropertyValue("recoveryAnchor", out var recoveryAnchor));
        Assert.Null(recoveryAnchor);
        Assert.True(recovery.TryGetPropertyValue("deteriorationAnchor", out var deteriorationAnchor));
        Assert.Null(deteriorationAnchor);

        var absentDecision = decision.DeepClone().AsObject();
        var absentRecovery = absentDecision["proposal"]!["recovery"]!.AsObject();
        absentRecovery.Remove("recoveryAnchor");
        absentRecovery.Remove("deteriorationAnchor");
        var absentComposition = WoundResponseInputComposer.Compose(
            binding,
            new[] { opportunity },
            new[] { JsonSerializer.SerializeToElement(absentDecision) },
            scene,
            Array.Empty<WoundOpportunityDecisionReceipt>());

        Assert.True(absentComposition.Success, DescribeIssues(absentComposition.Issues));
        var absentProposed = Assert.Single(absentComposition.Transitions).ProposedAfter;
        Assert.Null(absentProposed.Recovery.RecoveryAnchor);
        Assert.Null(absentProposed.Recovery.DeteriorationAnchor);
        Assert.Equal(canonicalJson, WoundMaterializationContract.SerializeCanonical(absentProposed));
    }

    [Fact]
    public void Parse_HealAcceptsClosedCosmeticAndMechanicalEffectLegacyDrafts()
    {
        var wound = CreateWoundWithStrictRoute("procedure");
        Route(wound)["outcomes"]![0]!["result"] = new JsonArray(
            new JsonObject { ["kind"] = "reduce_severity", ["steps"] = 1 },
            CreateHealOperation(
                CreateCosmeticHealLegacy("scar_left_side"),
                CreateMechanicalHealLegacy("nerve_tremor")));

        var result = Parse(wound);

        Assert.True(result.IsValid, DescribeIssues(result));
    }

    [Fact]
    public void Parse_SeparateMechanicalLegaciesMayReuseOnlyTheirDraftLocalRefs()
    {
        var wound = CreateWoundWithStrictRoute("procedure");
        var first = CreateMechanicalHealLegacy("nerve_tremor_left");
        var second = CreateMechanicalHealLegacy("nerve_tremor_right");
        Route(wound)["outcomes"]![0]!["result"] = new JsonArray(
            new JsonObject { ["kind"] = "reduce_severity", ["steps"] = 1 },
            CreateHealOperation(first, second));

        var result = Parse(wound);

        Assert.True(result.IsValid, DescribeIssues(result));
    }

    [Fact]
    public void Parse_MechanicalLegacyAcceptsFiveDefinitionsAndFiveApplications()
    {
        var wound = CreateWoundWithStrictRoute("procedure");
        var legacy = CreateMechanicalHealLegacy("maximum_mechanical_legacy");
        ConfigureMechanicalLegacyDraftCount(legacy, 5);
        Route(wound)["outcomes"]![0]!["result"] = new JsonArray(
            new JsonObject { ["kind"] = "reduce_severity", ["steps"] = 1 },
            CreateHealOperation(legacy));

        var result = Parse(wound);

        Assert.True(result.IsValid, DescribeIssues(result));
    }

    [Fact]
    public void Parse_MechanicalLegacyValidatesDefinitionsAsOneReachableReactionGraph()
    {
        var wound = CreateWoundWithStrictRoute("procedure");
        var legacy = CreateMechanicalHealLegacy("reaction_graph_legacy");
        var draft = legacy["effectDraft"]!;
        var root = EffectMaterializationTestFixture.CreateDefinition("event_reaction");
        root["definitionKey"] = "legacy_reaction_root";
        root["allowedRealms"] = new JsonArray("mortal_world");
        root["allowedTargetKinds"] = new JsonArray("player");
        root["stacking"]!["stackKey"] = "legacy_reaction_root_stack";
        root["links"] = new JsonArray();
        var payload = root["components"]![0]!["payload"]!.AsObject();
        payload["resultKind"] = "apply_definition";
        payload["dependency"] = "before_current_event";
        payload["maxExpansion"] = 2;
        payload["definitionKey"] = "legacy_reaction_child";
        payload["parameters"] = new JsonObject { ["amount"] = 3 };

        var child = EffectMaterializationTestFixture.CreateDefinition();
        child["definitionKey"] = "legacy_reaction_child";
        child["allowedRealms"] = new JsonArray("mortal_world");
        child["allowedTargetKinds"] = new JsonArray("player");
        child["stacking"]!["stackKey"] = "legacy_reaction_child_stack";
        child["links"] = new JsonArray();
        draft["definitions"] = new JsonArray(
            new JsonObject
            {
                ["definitionRef"] = "reaction_root_ref",
                ["definition"] = root
            },
            new JsonObject
            {
                ["definitionRef"] = "reaction_child_ref",
                ["definition"] = child
            });
        draft["applications"]![0]!["definitionRef"] = "reaction_root_ref";
        Route(wound)["outcomes"]![0]!["result"] = new JsonArray(
            new JsonObject { ["kind"] = "reduce_severity", ["steps"] = 1 },
            CreateHealOperation(legacy));

        var result = Parse(wound);

        Assert.True(result.IsValid, DescribeIssues(result));
    }

    [Fact]
    public void Parse_MechanicalLegacyRejectsDanglingReactionDefinition()
    {
        var wound = CreateWoundWithStrictRoute("procedure");
        var legacy = CreateMechanicalHealLegacy("dangling_reaction_legacy");
        var draft = legacy["effectDraft"]!;
        var root = EffectMaterializationTestFixture.CreateDefinition("event_reaction");
        root["definitionKey"] = "legacy_dangling_root";
        root["allowedRealms"] = new JsonArray("mortal_world");
        root["allowedTargetKinds"] = new JsonArray("player");
        root["stacking"]!["stackKey"] = "legacy_dangling_root_stack";
        root["links"] = new JsonArray();
        var payload = root["components"]![0]!["payload"]!.AsObject();
        payload["resultKind"] = "apply_definition";
        payload["dependency"] = "before_current_event";
        payload["maxExpansion"] = 2;
        payload["definitionKey"] = "missing_legacy_child";
        payload["parameters"] = new JsonObject();
        draft["definitions"] = new JsonArray(new JsonObject
        {
            ["definitionRef"] = "dangling_root_ref",
            ["definition"] = root
        });
        draft["applications"]![0]!["definitionRef"] = "dangling_root_ref";
        Route(wound)["outcomes"]![0]!["result"] = new JsonArray(
            new JsonObject { ["kind"] = "reduce_severity", ["steps"] = 1 },
            CreateHealOperation(legacy));

        AssertInvalidAt(
            Parse(wound),
            "wound.treatment.routes[0].outcomes[0].result[1].legacies[0].effectDraft.definitions[0].definition.components[0].payload.definitionKey",
            "wound_materialization_invalid_field");
    }

    [Fact]
    public void Parse_MechanicalLegacyRejectsDefinitionDisconnectedFromEveryApplication()
    {
        var wound = CreateWoundWithStrictRoute("procedure");
        var legacy = CreateMechanicalHealLegacy("orphan_definition_legacy");
        var draft = legacy["effectDraft"]!;
        var orphan = WoundContractTestData.CreateOwnedEffectDefinition(
            "wound_test_torn_side",
            "mortal_world",
            "orphan_legacy_definition",
            "action_control");
        orphan["links"] = new JsonArray();
        draft["definitions"]!.AsArray().Add(new JsonObject
        {
            ["definitionRef"] = "orphan_legacy_definition_ref",
            ["definition"] = orphan
        });
        Route(wound)["outcomes"]![0]!["result"] = new JsonArray(
            new JsonObject { ["kind"] = "reduce_severity", ["steps"] = 1 },
            CreateHealOperation(legacy));

        AssertInvalidAt(
            Parse(wound),
            "wound.treatment.routes[0].outcomes[0].result[1].legacies[0].effectDraft.definitions[1].definition",
            "wound_materialization_invalid_field");
    }

    [Theory]
    [InlineData("missing_required", "effect_source_parameter_required")]
    [InlineData("unknown_parameter", "effect_source_parameter_forbidden")]
    [InlineData("out_of_bounds", "effect_source_parameter_out_of_bounds")]
    public void Parse_MechanicalLegacyApplicationsObeyTheirDefinitionParameterBounds(
        string mutation,
        string expectedCode)
    {
        var wound = CreateWoundWithStrictRoute("procedure");
        var legacy = CreateMechanicalHealLegacy("bounded_mechanical_legacy");
        var effectDraft = legacy["effectDraft"]!;
        var definition = effectDraft["definitions"]![0]!["definition"]!;
        var parameters = effectDraft["applications"]![0]!["parameters"]!.AsObject();
        definition["parameterBounds"] = new JsonObject
        {
            ["action"] = new JsonObject
            {
                ["kind"] = "enum",
                ["allowedValues"] = new JsonArray("use_item"),
                ["required"] = true
            }
        };
        switch (mutation)
        {
            case "missing_required":
                break;
            case "unknown_parameter":
                definition["parameterBounds"] = new JsonObject();
                parameters["unknown"] = 1;
                break;
            case "out_of_bounds":
                parameters["action"] = "attack";
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null);
        }
        Route(wound)["outcomes"]![0]!["result"] = new JsonArray(
            new JsonObject { ["kind"] = "reduce_severity", ["steps"] = 1 },
            CreateHealOperation(legacy));

        var result = Parse(wound);

        AssertInvalidAt(
            result,
            "wound.treatment.routes[0].outcomes[0].result[1].legacies[0].effectDraft.applications[0].parameters" +
            (mutation == "unknown_parameter" ? ".unknown" : ".action"),
            expectedCode);
    }

    [Fact]
    public void WoundLegacySource_SurvivesWithoutActiveWoundButIsNeverPubliclyMaterializable()
    {
        var definition = WoundContractTestData.CreateOwnedEffectDefinition(
            "already_healed_wound",
            "mortal_world",
            "legacy_residual_tremor",
            "action_control");
        definition["links"] = new JsonArray();
        var source = new EffectSourceExport(
            "mortal_world",
            "wound_legacy",
            "wound_legacy_healed_001",
            new JsonArray(definition),
            Materializable: false,
            Active: true,
            SameTurn: false);
        var authority = EffectSourceAuthority.Build(new EffectSourceAuthorityInput(
            new[] { source },
            Array.Empty<EffectSourceExport>(),
            new HashSet<string>(StringComparer.Ordinal)));

        Assert.Empty(authority.Issues);
        var key = new EffectSourceKey(
            "mortal_world",
            "wound_legacy",
            "wound_legacy_healed_001",
            "legacy_residual_tremor");
        Assert.True(authority.ResolveCanonicalBinding(key, "player").Success);
        var publicAttempt = authority.Resolve(key, "player", new JsonObject());
        Assert.False(publicAttempt.Success);
        Assert.Contains(publicAttempt.Issues, issue =>
            issue.Code == "effect_source_not_materializable");

        var forgedPublic = EffectSourceAuthority.Build(new EffectSourceAuthorityInput(
            new[] { source with { Materializable = true } },
            Array.Empty<EffectSourceExport>(),
            new HashSet<string>(StringComparer.Ordinal)));
        Assert.Contains(forgedPublic.Issues, issue =>
            issue.Code == "effect_source_wound_legacy_public_materialization_forbidden");
    }

    [Theory]
    [InlineData("unknown_kind", "wound.treatment.routes[0].outcomes[0].result[1].legacies[0].kind", "wound_materialization_invalid_field")]
    [InlineData("cosmetic_extra_field", "wound.treatment.routes[0].outcomes[0].result[1].legacies[0].effectDraft", "wound_materialization_unknown_field")]
    [InlineData("mechanical_missing_draft", "wound.treatment.routes[0].outcomes[0].result[1].legacies[1].effectDraft", "wound_materialization_missing_field")]
    [InlineData("empty_definitions", "wound.treatment.routes[0].outcomes[0].result[1].legacies[1].effectDraft.definitions", "wound_materialization_invalid_field")]
    [InlineData("empty_applications", "wound.treatment.routes[0].outcomes[0].result[1].legacies[1].effectDraft.applications", "wound_materialization_invalid_field")]
    [InlineData("wrong_effect_schema", "wound.treatment.routes[0].outcomes[0].result[1].legacies[1].effectDraft.schemaVersion", "wound_materialization_invalid_field")]
    [InlineData("unknown_effect_draft_field", "wound.treatment.routes[0].outcomes[0].result[1].legacies[1].effectDraft.rawEffects", "wound_materialization_unknown_field")]
    [InlineData("external_definition", "wound.treatment.routes[0].outcomes[0].result[1].legacies[1].effectDraft.applications[0].definitionRef", "wound_materialization_invalid_field")]
    [InlineData("six_definitions", "wound.treatment.routes[0].outcomes[0].result[1].legacies[1].effectDraft.definitions", "wound_materialization_limit_exceeded")]
    [InlineData("six_applications", "wound.treatment.routes[0].outcomes[0].result[1].legacies[1].effectDraft.applications", "wound_materialization_limit_exceeded")]
    [InlineData("duplicate_definition_ref", "wound.treatment.routes[0].outcomes[0].result[1].legacies[1].effectDraft.definitions[1].definitionRef", "wound_materialization_invalid_field")]
    [InlineData("confusable_definition_key", "wound.treatment.routes[0].outcomes[0].result[1].legacies[1].effectDraft.definitions[1].definition.definitionKey", "wound_materialization_invalid_field")]
    [InlineData("confusable_application_ref", "wound.treatment.routes[0].outcomes[0].result[1].legacies[1].effectDraft.applications[1].applicationRef", "wound_materialization_invalid_field")]
    [InlineData("definition_application_ref_collision", "wound.treatment.routes[0].outcomes[0].result[1].legacies[1].effectDraft.applications[0].applicationRef", "wound_materialization_invalid_field")]
    [InlineData("confusable_definition_application_ref_collision", "wound.treatment.routes[0].outcomes[0].result[1].legacies[1].effectDraft.applications[0].applicationRef", "wound_materialization_invalid_field")]
    [InlineData("wrong_derived_target_kind", "wound.treatment.routes[0].outcomes[0].result[1].legacies[1].effectDraft.definitions[0].definition.allowedTargetKinds", "wound_materialization_invalid_field")]
    [InlineData("invalid_wound_legacy_predicate", "wound.treatment.routes[0].outcomes[0].result[1].legacies[1].effectDraft.definitions[0].definition.lifetime.activePredicate", "wound_materialization_invalid_field")]
    [InlineData("caller_target", "wound.treatment.routes[0].outcomes[0].result[1].legacies[1].effectDraft.applications[0].targetId", "wound_materialization_unknown_field")]
    [InlineData("nonempty_links", "wound.treatment.routes[0].outcomes[0].result[1].legacies[1].effectDraft.definitions[0].definition.links", "wound_materialization_invalid_field")]
    [InlineData("duplicate_legacy_ref", "wound.treatment.routes[0].outcomes[0].result[1].legacies[1].localLegacyRef", "wound_materialization_invalid_field")]
    [InlineData("case_changed_legacy_ref", "wound.treatment.routes[0].outcomes[0].result[1].legacies[1].localLegacyRef", "wound_materialization_invalid_field")]
    [InlineData("confusable_legacy_ref", "wound.treatment.routes[0].outcomes[0].result[1].legacies[1].localLegacyRef", "wound_materialization_invalid_field")]
    [InlineData("nine_legacies", "wound.treatment.routes[0].outcomes[0].result[1].legacies", "wound_materialization_limit_exceeded")]
    public void Parse_HealLegacyUnionRejectsOpenIncompleteAndAmbiguousDrafts(
        string mutation,
        string expectedPath,
        string expectedCode)
    {
        var wound = CreateWoundWithStrictRoute("procedure");
        var heal = CreateHealOperation(
            CreateCosmeticHealLegacy("scar_left_side"),
            CreateMechanicalHealLegacy("nerve_tremor"));
        Route(wound)["outcomes"]![0]!["result"] = new JsonArray(
            new JsonObject { ["kind"] = "reduce_severity", ["steps"] = 1 },
            heal);
        var legacies = heal["legacies"]!.AsArray();
        switch (mutation)
        {
            case "unknown_kind":
                legacies[0]!["kind"] = "skill";
                break;
            case "cosmetic_extra_field":
                legacies[0]!["effectDraft"] = new JsonObject();
                break;
            case "mechanical_missing_draft":
                legacies[1]!.AsObject().Remove("effectDraft");
                break;
            case "empty_definitions":
                legacies[1]!["effectDraft"]!["definitions"] = new JsonArray();
                break;
            case "empty_applications":
                legacies[1]!["effectDraft"]!["applications"] = new JsonArray();
                break;
            case "wrong_effect_schema":
                legacies[1]!["effectDraft"]!["schemaVersion"] = 2;
                break;
            case "unknown_effect_draft_field":
                legacies[1]!["effectDraft"]!["rawEffects"] = new JsonArray();
                break;
            case "external_definition":
                legacies[1]!["effectDraft"]!["applications"]![0]!["definitionRef"] =
                    "external_definition";
                break;
            case "six_definitions":
            {
                var definitions = legacies[1]!["effectDraft"]!["definitions"]!.AsArray();
                var template = definitions[0]!.DeepClone().AsObject();
                for (var index = 1; index < 6; index++)
                {
                    var copy = template.DeepClone().AsObject();
                    copy["definitionRef"] = $"legacy_definition_{index}";
                    copy["definition"]!["definitionKey"] = $"legacy-definition-{index}";
                    definitions.Add(copy);
                }

                break;
            }
            case "six_applications":
            {
                var applications = legacies[1]!["effectDraft"]!["applications"]!.AsArray();
                var template = applications[0]!.DeepClone().AsObject();
                for (var index = 1; index < 6; index++)
                {
                    var copy = template.DeepClone().AsObject();
                    copy["applicationRef"] = $"legacy_application_{index}";
                    applications.Add(copy);
                }

                break;
            }
            case "duplicate_definition_ref":
            {
                var definitions = legacies[1]!["effectDraft"]!["definitions"]!.AsArray();
                definitions.Add(definitions[0]!.DeepClone());
                break;
            }
            case "confusable_definition_key":
            {
                var effectDraft = legacies[1]!["effectDraft"]!;
                var definitions = effectDraft["definitions"]!.AsArray();
                definitions[0]!["definition"]!["definitionKey"] = "legacy-effect";
                var definition = definitions[0]!.DeepClone().AsObject();
                definition["definitionRef"] = "confusable_definition";
                definition["definition"]!["definitionKey"] = "legacy‐effect";
                definitions.Add(definition);
                var application = effectDraft["applications"]![0]!.DeepClone().AsObject();
                application["applicationRef"] = "confusable_definition_application";
                application["definitionRef"] = "confusable_definition";
                effectDraft["applications"]!.AsArray().Add(application);
                break;
            }
            case "confusable_application_ref":
            {
                var applications = legacies[1]!["effectDraft"]!["applications"]!.AsArray();
                applications[0]!["applicationRef"] = "residual-tremor";
                var copy = applications[0]!.DeepClone().AsObject();
                copy["applicationRef"] = "residual‐tremor";
                applications.Add(copy);
                break;
            }
            case "definition_application_ref_collision":
                legacies[1]!["effectDraft"]!["applications"]![0]!["applicationRef"] =
                    "residual_tremor_definition";
                break;
            case "confusable_definition_application_ref_collision":
                legacies[1]!["effectDraft"]!["definitions"]![0]!["definitionRef"] =
                    "residual-tremor";
                legacies[1]!["effectDraft"]!["applications"]![0]!["definitionRef"] =
                    "residual-tremor";
                legacies[1]!["effectDraft"]!["applications"]![0]!["applicationRef"] =
                    "residual‐tremor";
                break;
            case "wrong_derived_target_kind":
                legacies[1]!["effectDraft"]!["definitions"]![0]!["definition"]!["allowedTargetKinds"] =
                    new JsonArray("npc");
                break;
            case "invalid_wound_legacy_predicate":
                legacies[1]!["effectDraft"]!["definitions"]![0]!["definition"]!["lifetime"]!["activePredicate"] =
                    "equipped";
                break;
            case "caller_target":
                legacies[1]!["effectDraft"]!["applications"]![0]!["targetId"] =
                    "player_current";
                break;
            case "nonempty_links":
                legacies[1]!["effectDraft"]!["definitions"]![0]!["definition"]!["links"] =
                    new JsonArray(new JsonObject { ["kind"] = "wound" });
                break;
            case "duplicate_legacy_ref":
                legacies[1]!["localLegacyRef"] = "scar_left_side";
                break;
            case "case_changed_legacy_ref":
                legacies[0]!["localLegacyRef"] = "legacy-edge";
                legacies[1]!["localLegacyRef"] = "LEGACY-EDGE";
                break;
            case "confusable_legacy_ref":
                legacies[0]!["localLegacyRef"] = "legacy-edge";
                legacies[1]!["localLegacyRef"] = "legacy‐edge";
                break;
            case "nine_legacies":
                heal["legacies"] = WoundContractTestData.Repeat(
                    9,
                    index => CreateCosmeticHealLegacy($"legacy_{index}"));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null);
        }

        AssertInvalidAt(Parse(wound), expectedPath, expectedCode);
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

    private static JsonObject CreateWoundWithStrictRoute(string mode)
    {
        var wound = WoundContractTestData.CreateActiveWound();
        var route = mode switch
        {
            "procedure" => CreateStrictProcedureRoute(),
            "course" => CreateStrictCourseRoute(),
            "guaranteed" => CreateStrictGuaranteedRoute(),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
        };
        wound["treatment"]!["routes"] = new JsonArray(route);
        wound["treatment"]!["knownRouteIds"] =
            new JsonArray(route["routeId"]!.GetValue<string>());
        return wound;
    }

    private static (WoundAcceptedTurnBinding Binding, WoundOpportunityAuthority Opportunity)
        CreateTreatmentProposalOpportunity(WoundMaterializationEnvelope? worseningTarget = null)
    {
        var evidence = new WoundOpportunityEventEvidence(
            "formal",
            "combat_resolution",
            "combat_treatment_rewrite_001",
            "harmful",
            2,
            "Осколок стекла оставил рану, которую можно обработать.");
        var acceptedEvent = new WoundAcceptedEventAuthority(
            "turn_42:treatment_rewrite_wound_event",
            "combat_resolution",
            "combat_treatment_rewrite_001",
            WoundOpportunityEventEvidenceFingerprint.Compute(evidence));
        var acceptedEvents = new[] { acceptedEvent };
        var binding = new WoundAcceptedTurnBinding(
            "session_treatment_rewrite",
            "request_treatment_rewrite",
            "snapshot_treatment_rewrite",
            "mortal_world",
            42,
            acceptedEvents,
            WoundAcceptedEventSetFingerprint.Compute(acceptedEvents));
        var result = WoundOpportunityAuthority.Compose(new WoundOpportunityBuildRequest(
            binding,
            "opportunity_treatment_rewrite_001",
            "opportunity-treatment-rewrite-001",
            acceptedEvent.EventRef,
            new WoundOwnerCoordinate(
                "mortal_world",
                "player",
                "player_current",
                "game_state/player/wounds.json"),
            "physical",
            "mortal_narrative_injury_v1",
            "combat_action",
            "combat_treatment_rewrite_001",
            "active",
            evidence,
            HardMaximumSeverityRank: 4,
            GuaranteedTrigger: null,
            new WoundOpportunitySafeContext(
                "вы",
                "осколок стекла",
                new[] { "anatomical", "systemic", "other" }),
            worseningTarget is null
                ? null
                : new WoundOpportunityWorseningTargetEvidence(worseningTarget, "retrauma")));
        Assert.True(result.Success, DescribeIssues(result.Issues));
        return (binding, Assert.IsType<WoundOpportunityAuthority>(result.Opportunity));
    }

    private static JsonObject CreateTreatmentProposalWithLocalComplicationRemoval()
    {
        var wound = CreateWoundWithStrictRoute("procedure");
        var classification = wound["classification"]!.DeepClone().AsObject();
        classification.Remove("domain");
        var route = Route(wound);
        route["outcomes"]![0]!["result"] = new JsonArray(new JsonObject
        {
            ["kind"] = "remove_complication",
            ["complicationRef"] = "irritated_edges"
        });
        return new JsonObject
        {
            ["classification"] = classification,
            ["display"] = wound["display"]!.DeepClone(),
            ["severity"] = "II",
            ["complications"] = new JsonArray(new JsonObject
            {
                ["complicationRef"] = "irritated_edges",
                ["kind"] = "pain",
                ["state"] = "active",
                ["displayName"] = "Раздражённые края раны",
                ["treatmentDifficultyModifier"] = 1,
                ["visibility"] = "known_to_player"
            }),
            ["consequenceDefinitions"] = new JsonArray(),
            ["treatment"] = wound["treatment"]!.DeepClone(),
            ["recovery"] = wound["recovery"]!.DeepClone()
        };
    }

    private static JsonObject CreateStrictProcedureRoute() => new()
    {
        ["routeId"] = "clean_and_suture_v1",
        ["displayName"] = "Очистить и ушить рану",
        ["visibility"] = "known_to_player",
        ["mode"] = "procedure",
        ["requirements"] = new JsonArray(
            new JsonObject
            {
                ["kind"] = "item_quantity",
                ["itemRef"] = "sterile_thread",
                ["quantity"] = 1,
                ["ownerRole"] = "provider"
            },
            new JsonObject
            {
                ["kind"] = "skill_tier",
                ["capabilityRef"] = "field_medicine",
                ["minimumTier"] = 2,
                ["actorRole"] = "provider"
            }),
        ["resourcePolicy"] = new JsonObject
        {
            ["reserveBeforeResolution"] = true,
            ["consumeOn"] = new JsonArray("success", "partial_success", "failed_attempt"),
            ["refundOn"] = new JsonArray("cancelled", "validation_failed", "rolled_back"),
            ["mutations"] = new JsonArray(new JsonObject
            {
                ["kind"] = "consume_requirement",
                ["scope"] = "common",
                ["milestoneOrdinal"] = null,
                ["requirementIndex"] = 0
            })
        },
        ["resolution"] = new JsonObject
        {
            ["formulaKey"] = "mortal_wound_procedure_v1",
            ["difficulty"] = 15,
            ["rollSource"] = "accepted_d20",
            ["criticalPolicy"] = "natural_20_first_natural_1_last",
            ["modifierSource"] = new JsonObject
            {
                ["kind"] = "resolved_skill_tier",
                ["requirementIndex"] = 1
            }
        },
        ["outcomes"] = new JsonArray(
            new JsonObject
            {
                ["bandId"] = "clean_success",
                ["minimumMargin"] = 5,
                ["maximumMargin"] = null,
                ["category"] = "success",
                ["result"] = new JsonArray(
                    new JsonObject { ["kind"] = "stabilize" },
                    new JsonObject { ["kind"] = "reduce_severity", ["steps"] = 1 })
            },
            new JsonObject
            {
                ["bandId"] = "clean_partial",
                ["minimumMargin"] = 0,
                ["maximumMargin"] = 4,
                ["category"] = "partial_success",
                ["result"] = new JsonArray(
                    new JsonObject { ["kind"] = "stabilize" },
                    new JsonObject { ["kind"] = "add_recovery", ["points"] = 1 })
            },
            new JsonObject
            {
                ["bandId"] = "clean_no_improvement",
                ["minimumMargin"] = -4,
                ["maximumMargin"] = -1,
                ["category"] = "failed_attempt",
                ["result"] = new JsonArray(new JsonObject { ["kind"] = "no_improvement" })
            },
            new JsonObject
            {
                ["bandId"] = "clean_complication",
                ["minimumMargin"] = null,
                ["maximumMargin"] = -5,
                ["category"] = "failed_attempt",
                ["result"] = new JsonArray(CreateEffectlessComplicationOperation())
            }),
        ["interruption"] = null
    };

    private static JsonObject CreateStrictCourseRoute() => new()
    {
        ["routeId"] = "antibiotic_course_v1",
        ["displayName"] = "Пройти курс антибиотика",
        ["visibility"] = "known_to_player",
        ["mode"] = "course",
        ["requirements"] = new JsonArray(new JsonObject
        {
            ["kind"] = "provider",
            ["providerRef"] = "field_medic_01"
        }),
        ["resourcePolicy"] = new JsonObject
        {
            ["reserveBeforeResolution"] = true,
            ["consumeOn"] = new JsonArray("success"),
            ["refundOn"] = new JsonArray("cancelled", "validation_failed", "rolled_back"),
            ["mutations"] = new JsonArray(
                CreateCourseResourceMutation(1),
                CreateCourseResourceMutation(2),
                CreateCourseResourceMutation(3))
        },
        ["resolution"] = new JsonObject
        {
            ["clockKind"] = "world_time.currentTimeInMinutes",
            ["maximumGapMinutes"] = 600
        },
        ["outcomes"] = new JsonArray(
            CreateCourseMilestone(1, 0, "active", new JsonArray()),
            CreateCourseMilestone(2, 480, "active", new JsonArray()),
            CreateCourseMilestone(
                3,
                960,
                "completed",
                new JsonArray(CreateHealOperation()))),
        ["interruption"] = new JsonObject
        {
            ["category"] = "failed_attempt",
            ["result"] = new JsonArray(new JsonObject { ["kind"] = "no_improvement" })
        }
    };

    private static void ConfigureCourseMilestoneCount(JsonObject wound, int count)
    {
        var route = Route(wound);
        var outcomes = new JsonArray();
        var mutations = new JsonArray();
        for (var ordinal = 1; ordinal <= count; ordinal++)
        {
            outcomes.Add(CreateCourseMilestone(
                ordinal,
                (ordinal - 1L) * 60L,
                ordinal == count ? "completed" : "active",
                ordinal == count
                    ? new JsonArray(CreateHealOperation())
                    : new JsonArray()));
            mutations.Add(CreateCourseResourceMutation(ordinal));
        }

        route["outcomes"] = outcomes;
        route["resourcePolicy"]!["mutations"] = mutations;
    }

    private static void ConfigureCourseResourceSelectorCount(JsonObject wound, int count)
    {
        ConfigureCourseMilestoneCount(wound, 32);
        var route = Route(wound);
        var mutations = new JsonArray();
        for (var ordinal = 1; ordinal <= 32; ordinal++)
        {
            var requirements = route["outcomes"]![ordinal - 1]!["requirements"]!.AsArray();
            requirements.Add(new JsonObject
            {
                ["kind"] = "resource_quantity",
                ["resourceRef"] = $"course_resource_{ordinal}",
                ["quantity"] = 1,
                ["ownerRole"] = "target"
            });
            mutations.Add(CreateCourseResourceMutation(ordinal));
            var second = CreateCourseResourceMutation(ordinal);
            second["requirementIndex"] = 1;
            mutations.Add(second);
        }

        if (count == 65)
        {
            route["requirements"]!.AsArray().Add(new JsonObject
            {
                ["kind"] = "item_quantity",
                ["itemRef"] = "course_case",
                ["quantity"] = 1,
                ["ownerRole"] = "provider"
            });
            mutations.Add(new JsonObject
            {
                ["kind"] = "consume_requirement",
                ["scope"] = "common",
                ["milestoneOrdinal"] = null,
                ["requirementIndex"] = 1
            });
        }
        else if (count != 64)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, null);
        }

        route["resourcePolicy"]!["mutations"] = mutations;
    }

    private static JsonObject CreateCourseResourceMutation(int ordinal) => new()
    {
        ["kind"] = "consume_requirement",
        ["scope"] = "course_milestone",
        ["milestoneOrdinal"] = ordinal,
        ["requirementIndex"] = 0
    };

    private static JsonObject CreateCourseMilestone(
        int ordinal,
        long afterMinutes,
        string completion,
        JsonArray result) => new()
    {
        ["ordinal"] = ordinal,
        ["afterMinutes"] = afterMinutes,
        ["requirements"] = new JsonArray(new JsonObject
        {
            ["kind"] = "item_quantity",
            ["itemRef"] = "antibiotic_dose",
            ["quantity"] = 1,
            ["ownerRole"] = "target"
        }),
        ["category"] = "success",
        ["completion"] = completion,
        ["result"] = result
    };

    private static JsonObject CreateStrictGuaranteedRoute() => new()
    {
        ["routeId"] = "exact_healing_source_v1",
        ["displayName"] = "Применить закреплённый источник исцеления",
        ["visibility"] = "known_to_player",
        ["mode"] = "guaranteed",
        ["requirements"] = new JsonArray(new JsonObject
        {
            ["kind"] = "source_capability",
            ["capabilityRef"] = "exact_materialized_healing_source",
            ["actorRole"] = "provider"
        }),
        ["resourcePolicy"] = new JsonObject
        {
            ["reserveBeforeResolution"] = true,
            ["consumeOn"] = new JsonArray("success"),
            ["refundOn"] = new JsonArray("cancelled", "validation_failed", "rolled_back"),
            ["mutations"] = new JsonArray()
        },
        ["resolution"] = new JsonObject
        {
            ["capabilityRef"] = "exact_materialized_healing_source",
            ["actorRole"] = "provider"
        },
        ["outcomes"] = new JsonArray(new JsonObject
        {
            ["category"] = "success",
            ["result"] = new JsonArray(new JsonObject { ["kind"] = "stabilize" })
        }),
        ["interruption"] = null
    };

    private static JsonObject CreateTypedOutcomeOperation(string kind) => kind switch
    {
        "no_improvement" => new JsonObject { ["kind"] = kind },
        "stabilize" => new JsonObject { ["kind"] = kind },
        "add_recovery" => new JsonObject { ["kind"] = kind, ["points"] = 1 },
        "reduce_severity" => new JsonObject { ["kind"] = kind, ["steps"] = 1 },
        "remove_complication" => new JsonObject
        {
            ["kind"] = kind,
            ["complicationId"] = "complication_wound_test_irritation"
        },
        "add_complication" => CreateEffectlessComplicationOperation(),
        "apply_deterioration" => new JsonObject
        {
            ["kind"] = kind,
            ["policyRef"] = "untreated_infection"
        },
        "heal" => CreateHealOperation(),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };

    private static void AddExistingIrritationComplication(JsonObject wound)
    {
        wound["complications"]!.AsArray().Add(new JsonObject
        {
            ["complicationId"] = "complication_wound_test_irritation",
            ["kind"] = "pain",
            ["state"] = "active",
            ["displayName"] = "Раздражённые края раны",
            ["treatmentDifficultyModifier"] = 1,
            ["ownedEffectIds"] = new JsonArray(),
            ["visibility"] = "known_to_player"
        });
    }

    private static void ConfigureCurrentDeteriorationPolicy(
        JsonObject wound,
        string policyRef)
    {
        // T065 owns exact reference agreement; T069 now requires the same complete
        // typed policy shape that a canonical Mortal wound persists.
        wound["recovery"]!["deteriorationPolicy"] = new JsonObject
        {
            ["policyRef"] = policyRef,
            ["unmetConditions"] = new JsonArray("not_stabilized"),
            ["graceMinutes"] = 30L,
            ["cadenceMinutes"] = 10L,
            ["result"] = new JsonObject { ["kind"] = "increase_severity" }
        };
    }

    private static JsonObject CreateEffectlessComplicationOperation() => new()
    {
        ["kind"] = "add_complication",
        ["complicationDraft"] = new JsonObject
        {
            ["complications"] = new JsonArray(new JsonObject
            {
                ["complicationRef"] = "irritation",
                ["kind"] = "pain",
                ["state"] = "active",
                ["displayName"] = "Раздражённые края раны",
                ["treatmentDifficultyModifier"] = 1,
                ["visibility"] = "known_to_player"
            }),
            ["consequenceDefinitions"] = new JsonArray()
        }
    };

    private static JsonArray CreateEffectfulComplicationDefinitions()
    {
        var definition = WoundContractTestData.CreateOwnedEffectDefinition(
            "wound_test_torn_side",
            "mortal_world",
            "irritation_grip_limit",
            "action_control");
        definition["links"] = new JsonArray();
        return new JsonArray(new JsonObject
        {
            ["definitionRef"] = "irritation_grip_limit",
            ["definition"] = definition,
            ["root"] = new JsonObject
            {
                ["ownership"] = new JsonObject
                {
                    ["kind"] = "complication",
                    ["complicationRef"] = "irritation"
                },
                ["slots"] = new JsonArray(new JsonObject
                {
                    ["profileKey"] = "action_control",
                    ["readableSummary"] = "Боль мешает крепко удерживать предметы."
                })
            }
        });
    }

    private static void AddSecondRootDefinition(
        JsonArray definitions,
        bool reuseStackKey,
        string? stackKey = null)
    {
        var second = definitions[0]!.DeepClone().AsObject();
        second["definitionRef"] = "second_irritation_limit";
        second["definition"]!["definitionKey"] = "second_irritation_limit";
        if (!reuseStackKey)
        {
            second["definition"]!["stacking"]!["stackKey"] =
                stackKey ?? "stack_second_irritation_limit";
        }
        definitions.Add(second);
    }

    private static JsonArray CreateRootBoundReactionComplicationDefinitions(string targetPolicy) =>
        WoundContractTestData.CreateRootBoundReactionComplicationDefinitions(targetPolicy);

    private static void ReplaceDefinitions(JsonArray target, JsonArray replacement)
    {
        target.Clear();
        foreach (var definition in replacement)
            target.Add(definition?.DeepClone());
    }

    private static JsonObject CreateMarkerComplicationDefinition(string? authoredWoundId)
    {
        var definition = WoundContractTestData.CreateOwnedEffectDefinition(
            "wound_test_torn_side",
            "mortal_world",
            "irritation_wound_marker",
            "wound_consequence");
        definition["links"] = new JsonArray();
        var payload = definition["components"]![0]!["payload"]!.AsObject();
        if (authoredWoundId is null)
            payload.Remove("woundId");
        else
            payload["woundId"] = authoredWoundId;
        return new JsonObject
        {
            ["definitionRef"] = "irritation_wound_marker",
            ["definition"] = definition,
            ["root"] = new JsonObject
            {
                ["ownership"] = new JsonObject
                {
                    ["kind"] = "complication",
                    ["complicationRef"] = "irritation"
                },
                ["slots"] = new JsonArray()
            }
        };
    }

    private static JsonObject CreateHealOperation(params JsonObject[] legacies)
    {
        var rows = new JsonArray();
        foreach (var legacy in legacies)
            rows.Add(legacy);
        return new JsonObject
        {
            ["kind"] = "heal",
            ["legacies"] = rows
        };
    }

    private static JsonObject CreateCosmeticHealLegacy(string localLegacyRef) => new()
    {
        ["localLegacyRef"] = localLegacyRef,
        ["kind"] = "cosmetic",
        ["readableSummary"] = "После исцеления остался тонкий серебристый шрам."
    };

    private static JsonObject CreateMechanicalHealLegacy(string localLegacyRef)
    {
        const string definitionRef = "residual_tremor_definition";
        var definition = WoundContractTestData.CreateOwnedEffectDefinition(
            "wound_test_torn_side",
            "mortal_world",
            "residual-tremor-definition",
            "action_control");
        definition["links"] = new JsonArray();
        return new JsonObject
        {
            ["localLegacyRef"] = localLegacyRef,
            ["kind"] = "mechanical_effect",
            ["readableSummary"] = "Повреждённые нервы иногда вызывают дрожь руки.",
            ["effectDraft"] = new JsonObject
            {
                ["schemaVersion"] = 1,
                ["definitions"] = new JsonArray(new JsonObject
                {
                    ["definitionRef"] = definitionRef,
                    ["definition"] = definition
                }),
                ["applications"] = new JsonArray(new JsonObject
                {
                    ["applicationRef"] = "residual_tremor_application",
                    ["definitionRef"] = definitionRef,
                    ["parameters"] = new JsonObject()
                })
            }
        };
    }

    private static void ConfigureMechanicalLegacyDraftCount(JsonObject legacy, int count)
    {
        var draft = legacy["effectDraft"]!;
        var definitions = draft["definitions"]!.AsArray();
        var applications = draft["applications"]!.AsArray();
        var definitionTemplate = definitions[0]!.DeepClone().AsObject();
        var applicationTemplate = applications[0]!.DeepClone().AsObject();
        for (var index = 1; index < count; index++)
        {
            var definitionRef = $"residual_tremor_definition_{index}";
            var definition = definitionTemplate.DeepClone().AsObject();
            definition["definitionRef"] = definitionRef;
            definition["definition"]!["definitionKey"] =
                $"residual-tremor-definition-{index}";
            definition["definition"]!["stacking"]!["stackKey"] =
                $"stack_residual_tremor_definition_{index}";
            definitions.Add(definition);

            var application = applicationTemplate.DeepClone().AsObject();
            application["applicationRef"] = $"residual_tremor_application_{index}";
            application["definitionRef"] = definitionRef;
            applications.Add(application);
        }
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

    private static string SerializeTypedTreatment(MortalWoundTreatmentDefinition treatment)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            MortalWoundTreatmentContract.WriteCanonical(writer, treatment);
            writer.WriteEndObject();
        }
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

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

    private static string DescribeIssues(IEnumerable<ValidationIssue> issues) =>
        string.Join(
            Environment.NewLine,
            issues.Select(issue =>
                $"{issue.Code} {issue.FilePath}: {issue.Expected} / {issue.Actual}"));

    private static string[] IssueCoordinates(WoundMaterializationParseResult result) =>
        result.Issues
            .Select(static issue => $"{issue.Code}@{issue.FilePath}")
            .ToArray();
}
