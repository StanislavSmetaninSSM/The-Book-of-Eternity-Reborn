using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class WoundMaterializationContractTests
{
    private const string Path = "wound";

    [Fact]
    public void Parse_CompleteFinalVersionOnePhysicalActiveWound_ReturnsTypedEnvelope()
    {
        var result = Parse(WoundContractTestData.CreateActiveWound());

        Assert.True(result.IsValid);
        var wound = Assert.IsType<WoundMaterializationEnvelope>(result.Wound);
        Assert.Equal(1, wound.SchemaVersion);
        Assert.Equal("wound_test_torn_side", wound.WoundId);
        Assert.Equal("active", wound.Lifecycle);
        Assert.Equal("mortal_world", wound.Owner.Realm);
        Assert.Equal("game_state/player/wounds.json", wound.Owner.CarrierPath);
        Assert.Equal("physical", wound.Classification.Domain);
        Assert.Equal("anatomical", wound.Classification.LocationProfile.Kind);
        Assert.Equal("II", wound.Severity.Value);
        Assert.Equal(2, wound.Severity.Rank);
        Assert.Equal(2, wound.Consequences.Entries.Count);
        Assert.Single(wound.Treatment.Routes);
        Assert.Empty(wound.Treatment.DiagnosisPaths);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void Parse_RequiresObjectExactSchemaAndEveryRootSection()
    {
        var arrayResult = WoundMaterializationContract.Parse("[]", Path);
        AssertInvalid(arrayResult, Path, "wound_materialization_invalid_root");

        foreach (var invalidVersion in new JsonNode?[] { 0, 2, "1", null })
        {
            var wound = WoundContractTestData.CreateActiveWound();
            wound["schemaVersion"] = invalidVersion?.DeepClone();
            AssertInvalid(
                Parse(wound),
                Path + ".schemaVersion",
                "wound_materialization_invalid_field");
        }

        var nonIntegerVersion = WoundContractTestData.CreateActiveWound()
            .ToJsonString()
            .Replace("\"schemaVersion\":1", "\"schemaVersion\":1.0", StringComparison.Ordinal);
        AssertInvalid(
            WoundMaterializationContract.Parse(nonIntegerVersion, Path),
            Path + ".schemaVersion",
            "wound_materialization_invalid_field");

        var required = new[]
        {
            "schemaVersion", "woundId", "lifecycle", "owner", "origin",
            "classification", "display", "severity", "care", "complications",
            "consequences", "treatment", "recovery", "relations", "lastTransition"
        };
        foreach (var field in required)
        {
            var wound = WoundContractTestData.CreateActiveWound();
            wound.Remove(field);
            AssertInvalid(
                Parse(wound),
                Path + "." + field,
                "wound_materialization_missing_field");
        }
    }

    [Fact]
    public void Parse_RequiresEveryFinalNestedFieldAndRejectsUnknownNestedFields()
    {
        var requiredFields = new (string Section, string Field)[]
        {
            ("owner", "carrierPath"),
            ("origin", "sourceId"),
            ("origin", "createdAtCycleId"),
            ("classification", "woundType"),
            ("display", "description"),
            ("display", "visibleSymptoms"),
            ("severity", "lastChangeEventRef"),
            ("care", "activeCourseId"),
            ("consequences", "entries"),
            ("treatment", "diagnosisPaths"),
            ("treatment", "knownRouteIds"),
            ("recovery", "deteriorationPolicy"),
            ("relations", "legacyRefs"),
            ("lastTransition", "kind")
        };

        foreach (var (section, field) in requiredFields)
        {
            var wound = WoundContractTestData.CreateActiveWound();
            wound[section]!.AsObject().Remove(field);
            AssertInvalid(
                Parse(wound),
                $"{Path}.{section}.{field}",
                "wound_materialization_missing_field");
        }

        var unknownRoot = WoundContractTestData.CreateActiveWound();
        unknownRoot["legacyWounds"] = new JsonArray();
        AssertInvalid(
            Parse(unknownRoot),
            Path + ".legacyWounds",
            "wound_materialization_unknown_field");

        var unknownNested = WoundContractTestData.CreateActiveWound();
        unknownNested["origin"]!.AsObject()["source"] = "accepted_turn";
        AssertInvalid(
            Parse(unknownNested),
            Path + ".origin.source",
            "wound_materialization_unknown_field");
    }

    [Fact]
    public void Parse_RejectsDuplicateRawPropertiesRecursivelyBeforeBuildingModel()
    {
        var json = WoundContractTestData.CreateActiveWound().ToJsonString();
        var duplicateRoot = json.Replace(
            "\"woundId\":\"wound_test_torn_side\"",
            "\"woundId\":\"wound_test_torn_side\",\"woundId\":\"forged\"",
            StringComparison.Ordinal);
        var duplicateNested = json.Replace(
            "\"sourceKind\":\"combat_action\"",
            "\"sourceKind\":\"combat_action\",\"sourceKind\":\"hazard\"",
            StringComparison.Ordinal);

        AssertInvalid(
            WoundMaterializationContract.Parse(duplicateRoot, Path),
            Path + ".woundId",
            "wound_materialization_duplicate_property");
        AssertInvalid(
            WoundMaterializationContract.Parse(duplicateNested, Path),
            Path + ".origin.sourceKind",
            "wound_materialization_duplicate_property");
    }

    [Theory]
    [InlineData(" wound_test", "wound.woundId")]
    [InlineData("wound_test ", "wound.woundId")]
    [InlineData("e\u0301", "wound.woundId")]
    [InlineData("wound\nref", "wound.origin.eventRef")]
    [InlineData("wound\u202Eref", "wound.origin.sourceId")]
    [InlineData("wound\u2028ref", "wound.severity.lastChangeEventRef")]
    [InlineData("wound\u2029ref", "wound.lastTransition.transitionId")]
    public void Parse_RejectsNonExactPermanentOrReferenceIdentifiers(
        string invalid,
        string expectedPath)
    {
        var wound = WoundContractTestData.CreateActiveWound();
        SetPath(wound, expectedPath[(Path.Length + 1)..], invalid);

        AssertInvalid(
            Parse(wound),
            expectedPath,
            "wound_materialization_invalid_identifier");
    }

    [Fact]
    public void Parse_UsesOrdinalClosedRealmDomainLifecycleCareVisibilityAndRecoveryModes()
    {
        var cases = new (string RelativePath, string Invalid)[]
        {
            ("owner.realm", "Mortal_World"),
            ("classification.domain", "Physical"),
            ("lifecycle", "Active"),
            ("care.state", "Untreated"),
            ("display.visibility", "visible"),
            ("recovery.mode", "Requires_Stabilization")
        };

        foreach (var (relativePath, invalid) in cases)
        {
            var wound = WoundContractTestData.CreateActiveWound();
            SetPath(wound, relativePath, invalid);
            AssertInvalid(
                Parse(wound),
                Path + "." + relativePath,
                "wound_materialization_invalid_field");
        }
    }

    [Theory]
    [InlineData("I", 1)]
    [InlineData("II", 2)]
    [InlineData("III", 3)]
    [InlineData("IV", 4)]
    public void Parse_AcceptsEverySeverityOnlyWithMatchingRank(string value, int rank)
    {
        var wound = WoundContractTestData.CreateActiveWound();
        SetSeverity(wound, value, rank, value);
        SetConsequences(wound, Math.Min(rank, 2));

        Assert.True(Parse(wound).IsValid);
    }

    [Theory]
    [InlineData("V", 5, "IV", "wound.severity.value")]
    [InlineData("I", 0, "I", "wound.severity.rank")]
    [InlineData("IV", 5, "IV", "wound.severity.rank")]
    [InlineData("II", 3, "II", "wound.severity.rank")]
    [InlineData("II", 2, "V", "wound.severity.maximumAtCreation")]
    public void Parse_RejectsSeverityFiveOutOfRangeRankMismatchOrInvalidCreationMaximum(
        string value,
        int rank,
        string maximum,
        string expectedPath)
    {
        var wound = WoundContractTestData.CreateActiveWound();
        SetSeverity(wound, value, rank, maximum);

        AssertInvalid(
            Parse(wound),
            expectedPath,
            "wound_materialization_invalid_field");
    }

    [Theory]
    [InlineData("anatomical")]
    [InlineData("systemic")]
    [InlineData("mental")]
    [InlineData("spiritual_axis")]
    [InlineData("other")]
    public void Parse_AcceptsOnlyFinalLocationProfileKinds(string kind)
    {
        var wound = WoundContractTestData.CreateActiveWound();
        wound["classification"]!["locationProfile"]!["kind"] = kind;

        Assert.True(Parse(wound).IsValid);
    }

    [Fact]
    public void Parse_RequiresReadableLocusAndTreatsAuthorityFieldsAsOptionalExactFields()
    {
        var withoutAuthority = WoundContractTestData.CreateActiveWound();
        var location = withoutAuthority["classification"]!["locationProfile"]!.AsObject();
        location.Remove("authorityKind");
        location.Remove("authorityRef");
        location.Remove("affectedSide");
        Assert.True(Parse(withoutAuthority).IsValid);

        var blankLocus = WoundContractTestData.CreateActiveWound();
        blankLocus["classification"]!["locationProfile"]!["readableLocus"] = "   ";
        AssertInvalid(
            Parse(blankLocus),
            Path + ".classification.locationProfile.readableLocus",
            "wound_materialization_invalid_field");

        var invalidAuthority = WoundContractTestData.CreateActiveWound();
        invalidAuthority["classification"]!["locationProfile"]!["authorityRef"] = 42;
        AssertInvalid(
            Parse(invalidAuthority),
            Path + ".classification.locationProfile.authorityRef",
            "wound_materialization_invalid_identifier");

        var inferredAlias = WoundContractTestData.CreateActiveWound();
        inferredAlias["classification"]!["locationProfile"]!["anatomy"] = "torso";
        AssertInvalid(
            Parse(inferredAlias),
            Path + ".classification.locationProfile.anatomy",
            "wound_materialization_unknown_field");
    }

    [Fact]
    public void VersionOneEnvelopeLimits_AreExactNormativeConstants()
    {
        Assert.Equal(
            WoundContractTestData.TreatmentPathLimit,
            WoundMaterializationContract.MaxTreatmentRoutes);
        Assert.Equal(
            WoundContractTestData.DiagnosisPathLimit,
            WoundMaterializationContract.MaxDiagnosisPaths);
        Assert.Equal(
            WoundContractTestData.RequirementLimit,
            WoundMaterializationContract.MaxRequirementsPerTreatmentMember);
        Assert.Equal(
            WoundContractTestData.ComplicationLimit,
            WoundMaterializationContract.MaxComplications);
        Assert.Equal(
            WoundContractTestData.ConsequenceLimit,
            WoundMaterializationContract.MaxConsequences);
    }

    [Fact]
    public void Parse_EnforcesTreatmentRouteAndDiagnosisPathLimitsAtBoundary()
    {
        AssertCollectionLimit(
            WoundContractTestData.TreatmentPathLimit,
            static (wound, count) =>
            {
                var routes = wound["treatment"]!["routes"]!.AsArray();
                routes.Clear();
                for (var index = 0; index < count; index++)
                    routes.Add(CreateRoute(index));
            },
            Path + ".treatment.routes");

        AssertCollectionLimit(
            WoundContractTestData.DiagnosisPathLimit,
            static (wound, count) =>
            {
                var paths = wound["treatment"]!["diagnosisPaths"]!.AsArray();
                paths.Clear();
                for (var index = 0; index < count; index++)
                    paths.Add(CreateDiagnosisPath(index));
            },
            Path + ".treatment.diagnosisPaths");
    }

    [Fact]
    public void Parse_EnforcesRequirementLimitsForEveryRouteAndDiagnosisPath()
    {
        AssertCollectionLimit(
            WoundContractTestData.RequirementLimit,
            static (wound, count) =>
            {
                var requirements = wound["treatment"]!["routes"]![0]!["requirements"]!.AsArray();
                requirements.Clear();
                for (var index = 0; index < count; index++)
                    requirements.Add(CreateRequirement(index));
            },
            Path + ".treatment.routes[0].requirements");

        AssertCollectionLimit(
            WoundContractTestData.RequirementLimit,
            static (wound, count) =>
            {
                var paths = wound["treatment"]!["diagnosisPaths"]!.AsArray();
                paths.Clear();
                paths.Add(CreateDiagnosisPath(0));
                var requirements = paths[0]!["requirements"]!.AsArray();
                for (var index = 0; index < count; index++)
                    requirements.Add(CreateRequirement(index));
            },
            Path + ".treatment.diagnosisPaths[0].requirements");
    }

    [Fact]
    public void Parse_EnforcesComplicationAndConsequenceLimitsAtBoundary()
    {
        AssertCollectionLimit(
            WoundContractTestData.ComplicationLimit,
            static (wound, count) =>
            {
                var complications = wound["complications"]!.AsArray();
                complications.Clear();
                for (var index = 0; index < count; index++)
                    complications.Add(CreateComplication(index));
            },
            Path + ".complications");

        AssertCollectionLimit(
            WoundContractTestData.ConsequenceLimit,
            static (wound, count) =>
            {
                SetSeverity(wound, "IV", 4, "IV");
                SetConsequences(wound, count);
            },
            Path + ".consequences.entries");
    }

    [Fact]
    public void Parse_RejectsWrongCollectionMemberKindsWithExactPaths()
    {
        var cases = new (string RelativePath, Action<JsonObject> Mutate)[]
        {
            ("complications[0]", wound => wound["complications"]!.AsArray().Add("not-an-object")),
            ("consequences.entries[0]", wound => wound["consequences"]!["entries"]![0] = "not-an-object"),
            ("treatment.routes[0]", wound => wound["treatment"]!["routes"]![0] = "not-an-object"),
            ("treatment.diagnosisPaths[0]", wound => wound["treatment"]!["diagnosisPaths"]!.AsArray().Add("not-an-object")),
            ("treatment.routes[0].requirements[0]", wound => wound["treatment"]!["routes"]![0]!["requirements"]![0] = "not-an-object"),
            ("treatment.knownRouteIds[0]", wound => wound["treatment"]!["knownRouteIds"]![0] = new JsonObject())
        };

        foreach (var (relativePath, mutate) in cases)
        {
            var wound = WoundContractTestData.CreateActiveWound();
            mutate(wound);
            AssertInvalid(
                Parse(wound),
                Path + "." + relativePath,
                "wound_materialization_invalid_field");
        }
    }

    [Fact]
    public void Parse_RejectsDuplicateExactIdsAndConsequenceSlotsWithinOneEnvelope()
    {
        var cases = new (string ExpectedPath, Action<JsonObject> Mutate)[]
        {
            ("wound.complications[1].complicationId", wound =>
            {
                wound["complications"]!.AsArray().Add(CreateComplication(0));
                wound["complications"]!.AsArray().Add(CreateComplication(0));
            }),
            ("wound.complications[0].ownedEffectIds[1]", wound =>
            {
                var complication = CreateComplication(0);
                complication["ownedEffectIds"] = new JsonArray("effect_duplicate", "effect_duplicate");
                wound["complications"]!.AsArray().Add(complication);
            }),
            ("wound.consequences.entries[1].slot", wound =>
                wound["consequences"]!["entries"]![1]!["slot"] = 1),
            ("wound.treatment.routes[1].routeId", wound =>
                wound["treatment"]!["routes"]!.AsArray().Add(
                    wound["treatment"]!["routes"]![0]!.DeepClone())),
            ("wound.treatment.diagnosisPaths[1].diagnosisPathId", wound =>
            {
                wound["treatment"]!["diagnosisPaths"]!.AsArray().Add(CreateDiagnosisPath(0));
                wound["treatment"]!["diagnosisPaths"]!.AsArray().Add(CreateDiagnosisPath(0));
            }),
            ("wound.treatment.knownRouteIds[1]", wound =>
                wound["treatment"]!["knownRouteIds"]!.AsArray().Add("clean_and_suture")),
            ("wound.relations.legacyRefs[1]", wound =>
                wound["relations"]!["legacyRefs"] = new JsonArray("legacy_1", "legacy_1"))
        };

        foreach (var (expectedPath, mutate) in cases)
        {
            var wound = WoundContractTestData.CreateActiveWound();
            mutate(wound);
            AssertInvalid(
                Parse(wound),
                expectedPath,
                expectedPath.EndsWith(".slot", StringComparison.Ordinal)
                    ? "wound_materialization_duplicate_coordinate"
                    : "wound_materialization_duplicate_identifier");
        }
    }

    [Fact]
    public void Parse_RejectsLegacyRootsWrappersAndDraftAliasesThroughClosedSchema()
    {
        var legacyArray = "[{\"woundId\":\"legacy\"}]";
        var wrapper = "{\"schemaVersion\":1,\"wounds\":[],\"playerWoundChanges\":[]}";
        var oldDraft = WoundContractTestData.CreateActiveWound();
        var origin = oldDraft["origin"]!.AsObject();
        origin.Remove("sourceKind");
        origin["source"] = "accepted_turn";
        var severity = oldDraft["severity"]!.AsObject();
        severity.Remove("value");
        severity["level"] = "II";

        foreach (var result in new[]
                 {
                     WoundMaterializationContract.Parse(legacyArray, Path),
                     WoundMaterializationContract.Parse(wrapper, Path),
                     Parse(oldDraft)
                 })
        {
            Assert.False(result.IsValid);
            Assert.Null(result.Wound);
            Assert.All(result.Issues, issue =>
                Assert.StartsWith("wound_materialization_", issue.Code, StringComparison.Ordinal));
            Assert.DoesNotContain(result.Issues, issue =>
                issue.Code?.Contains("legacy", StringComparison.OrdinalIgnoreCase) == true);
        }
    }

    [Fact]
    public void Parse_InvalidInputNeverReturnsPartiallyUsableWound()
    {
        var wound = WoundContractTestData.CreateActiveWound();
        wound["severity"]!["rank"] = 5;
        wound["display"]!["visibility"] = "Visible";

        var result = Parse(wound);

        Assert.False(result.IsValid);
        Assert.Null(result.Wound);
        Assert.True(result.Issues.Count >= 2);
    }

    [Theory]
    [InlineData("createdAtTurn", "42", "42.5", "wound.origin.createdAtTurn")]
    [InlineData("createdAtTurn", "42", "4.2e1", "wound.origin.createdAtTurn")]
    [InlineData("createdAtTurn", "42", "2147483648", "wound.origin.createdAtTurn")]
    [InlineData("cadence", "86400", "86400.5", "wound.recovery.cadence")]
    [InlineData("cadence", "86400", "8.64e4", "wound.recovery.cadence")]
    [InlineData("cadence", "86400", "9223372036854775808", "wound.recovery.cadence")]
    [InlineData("currentStepThreshold", "3", "3.5", "wound.recovery.currentStepThreshold")]
    [InlineData("currentStepThreshold", "3", "3e0", "wound.recovery.currentStepThreshold")]
    [InlineData("currentStepThreshold", "3", "9223372036854775808", "wound.recovery.currentStepThreshold")]
    [InlineData("ordinal", "1", "1.5", "wound.lastTransition.ordinal")]
    [InlineData("ordinal", "1", "1e0", "wound.lastTransition.ordinal")]
    [InlineData("ordinal", "1", "2147483648", "wound.lastTransition.ordinal")]
    public void Parse_CommonIntegersRejectFractionExponentAndOverflowRawNumbers(
        string field,
        string validLexeme,
        string invalidLexeme,
        string expectedPath)
    {
        var json = WoundContractTestData.CreateActiveWound().ToJsonString();
        var validToken = $"\"{field}\":{validLexeme}";
        var invalidToken = $"\"{field}\":{invalidLexeme}";
        Assert.Contains(validToken, json, StringComparison.Ordinal);

        var result = WoundMaterializationContract.Parse(
            json.Replace(validToken, invalidToken, StringComparison.Ordinal),
            Path);

        AssertInvalid(
            result,
            expectedPath,
            "wound_materialization_invalid_field");
    }

    [Fact]
    public void CanonicalSerialization_IsStableDetachedAndEmitsOnlyFinalVersionOneFields()
    {
        var callerOwned = WoundContractTestData.CreateActiveWound();
        var result = Parse(callerOwned);
        var wound = Assert.IsType<WoundMaterializationEnvelope>(result.Wound);
        var first = WoundMaterializationContract.SerializeCanonical(wound);

        callerOwned["display"]!["name"] = "Подменённое имя";
        callerOwned["treatment"]!["routes"]![0]!["resolution"]!["formulaKey"] = "forged";
        Assert.Equal(first, WoundMaterializationContract.SerializeCanonical(wound));

        var reversed = new JsonObject();
        foreach (var property in WoundContractTestData.CreateActiveWound().Reverse())
            reversed[property.Key] = property.Value?.DeepClone();
        var reversedResult = Parse(reversed);
        Assert.True(reversedResult.IsValid);
        Assert.Equal(
            first,
            WoundMaterializationContract.SerializeCanonical(reversedResult.Wound!));

        var opaqueReordered = WoundContractTestData.CreateActiveWound();
        var reorderedRoute = opaqueReordered["treatment"]!["routes"]![0]!.AsObject();
        reorderedRoute["resourcePolicy"] = ReverseObject(
            reorderedRoute["resourcePolicy"]!.AsObject());
        reorderedRoute["resolution"] = ReverseObject(
            reorderedRoute["resolution"]!.AsObject());
        reorderedRoute["requirements"]![0] = ReverseObject(
            reorderedRoute["requirements"]![0]!.AsObject());
        reorderedRoute["outcomes"]![0] = ReverseObject(
            reorderedRoute["outcomes"]![0]!.AsObject());
        var opaqueReorderedResult = Parse(opaqueReordered);
        Assert.True(opaqueReorderedResult.IsValid, DescribeIssues(opaqueReorderedResult));
        Assert.Equal(
            first,
            WoundMaterializationContract.SerializeCanonical(opaqueReorderedResult.Wound!));

        var reparsed = WoundMaterializationContract.Parse(first, Path);
        Assert.True(reparsed.IsValid);
        Assert.Equal(first, WoundMaterializationContract.SerializeCanonical(reparsed.Wound!));

        var canonical = JsonNode.Parse(first)!.AsObject();
        Assert.Equal(
            new[]
            {
                "schemaVersion", "woundId", "lifecycle", "owner", "origin",
                "classification", "display", "severity", "care", "complications",
                "consequences", "treatment", "recovery", "relations", "lastTransition"
            },
            canonical.Select(static property => property.Key));
        Assert.DoesNotContain("\"source\":", first, StringComparison.Ordinal);
        Assert.DoesNotContain("\"opportunityRef\":", first, StringComparison.Ordinal);
        Assert.DoesNotContain("\"level\":", first, StringComparison.Ordinal);
        Assert.DoesNotContain("\"maximum\":", first, StringComparison.Ordinal);
        Assert.DoesNotContain("\"lastCareTurn\":", first, StringComparison.Ordinal);
        Assert.Contains("\"itemRef\":\"sterile_thread\"", first, StringComparison.Ordinal);
        Assert.Contains("\"capabilityRef\":\"field_medicine\"", first, StringComparison.Ordinal);
        Assert.Contains(
            "\"result\":[\"stabilize\",\"reduce_one\"]",
            first,
            StringComparison.Ordinal);
        Assert.DoesNotContain("\"itemId\":", first, StringComparison.Ordinal);
        Assert.DoesNotContain("\"skillId\":", first, StringComparison.Ordinal);
        Assert.DoesNotContain("\"results\":", first, StringComparison.Ordinal);
    }

    private static WoundMaterializationParseResult Parse(JsonObject wound) =>
        WoundMaterializationContract.Parse(wound.ToJsonString(), Path);

    private static void AssertInvalid(
        WoundMaterializationParseResult result,
        string path,
        string code)
    {
        Assert.False(result.IsValid);
        Assert.Null(result.Wound);
        Assert.Contains(result.Issues, issue =>
            issue.FilePath == path && issue.Code == code);
    }

    private static void AssertCollectionLimit(
        int limit,
        Action<JsonObject, int> populate,
        string expectedPath)
    {
        var atLimit = WoundContractTestData.CreateActiveWound();
        populate(atLimit, limit);
        var atLimitResult = Parse(atLimit);
        Assert.True(atLimitResult.IsValid, DescribeIssues(atLimitResult));

        var aboveLimit = WoundContractTestData.CreateActiveWound();
        populate(aboveLimit, limit + 1);
        AssertInvalid(
            Parse(aboveLimit),
            expectedPath,
            "wound_materialization_limit_exceeded");
    }

    private static string DescribeIssues(WoundMaterializationParseResult result) =>
        string.Join(
            Environment.NewLine,
            result.Issues.Select(issue =>
                $"{issue.Code} {issue.FilePath}: {issue.Expected} / {issue.Actual}"));

    private static void SetSeverity(JsonObject wound, string value, int rank, string maximum)
    {
        var severity = wound["severity"]!.AsObject();
        severity["value"] = value;
        severity["rank"] = rank;
        severity["maximumAtCreation"] = maximum;
    }

    private static void SetConsequences(JsonObject wound, int count)
    {
        var consequences = wound["consequences"]!.AsObject();
        consequences["slotBudget"] = count;
        consequences["slotsUsed"] = count;
        consequences["entries"] = WoundContractTestData.Repeat(count, index =>
            new JsonObject
            {
                ["slot"] = index + 1,
                ["profileKey"] = $"profile_{index}",
                ["effectId"] = $"effect_{index}",
                ["readableSummary"] = $"Последствие {index + 1}."
            });
    }

    private static JsonObject CreateComplication(int index) => new()
    {
        ["complicationId"] = $"complication_{index}",
        ["kind"] = "infection",
        ["state"] = "active",
        ["displayName"] = $"Осложнение {index}",
        ["treatmentDifficultyModifier"] = 2,
        ["ownedEffectIds"] = new JsonArray($"complication_effect_{index}"),
        ["visibility"] = "known_to_player"
    };

    private static JsonObject CreateRoute(int index) => new()
    {
        ["routeId"] = $"route_{index}",
        ["displayName"] = $"Маршрут {index}",
        ["visibility"] = "known_to_player",
        ["mode"] = "procedure",
        ["requirements"] = new JsonArray(),
        ["resourcePolicy"] = new JsonObject(),
        ["resolution"] = new JsonObject(),
        ["outcomes"] = new JsonArray(),
        ["interruption"] = null
    };

    private static JsonObject CreateDiagnosisPath(int index) => new()
    {
        ["diagnosisPathId"] = $"diagnosis_{index}",
        ["visibility"] = "known_to_player",
        ["requirements"] = new JsonArray(),
        ["check"] = new JsonObject(),
        ["reveals"] = new JsonArray(),
        ["failurePolicy"] = "no_reveal"
    };

    private static JsonObject CreateRequirement(int index) => new()
    {
        ["kind"] = "item_quantity",
        ["itemRef"] = $"item_{index}",
        ["quantity"] = 1
    };

    private static JsonObject ReverseObject(JsonObject source)
    {
        var result = new JsonObject();
        foreach (var property in source.Reverse())
            result[property.Key] = property.Value?.DeepClone();
        return result;
    }

    private static void SetPath(JsonObject root, string relativePath, JsonNode? value)
    {
        var segments = relativePath.Split('.');
        JsonObject current = root;
        for (var index = 0; index < segments.Length - 1; index++)
            current = current[segments[index]]!.AsObject();
        current[segments[^1]] = value?.DeepClone();
    }
}
