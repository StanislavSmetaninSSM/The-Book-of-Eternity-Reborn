using System.Text.Json;
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
    public void Parse_BroadMultiOperationRollComponentRequiresOneReciprocalEntry()
    {
        var wound = WoundContractTestData.CreateActiveWound();
        var sources = WoundContractTestData.CreateOwnedEffectSourcesForTarget(
            wound["woundId"]!.GetValue<string>(),
            "mortal_world",
            "player",
            ("effect_roll", "definition_roll", "roll_modifier"));
        sources["definitions"]![0]!["components"]![0]!["payload"]!["operations"] =
            new JsonArray("attack_roll", "defense_roll", "saving_throw");
        wound["consequences"]!["ownedEffectSources"] = sources;
        wound["consequences"]!["slotsUsed"] = 1;
        wound["consequences"]!["entries"] = new JsonArray(new JsonObject
        {
            ["slot"] = 1,
            ["profileKey"] = "roll_modifier",
            ["effectId"] = "effect_roll",
            ["readableSummary"] = "Несколько проверок затруднены одной раной."
        });

        var result = Parse(wound);

        Assert.True(result.IsValid, DescribeIssues(result));
        Assert.Single(Assert.IsType<WoundMaterializationEnvelope>(result.Wound)
            .Consequences.Entries);
    }

    [Fact]
    public void PersistedAdapter_BroadMultiOperationRollComponentRequiresOneRootSlot()
    {
        var definition = WoundContractTestData.CreateOwnedEffectDefinition(
            "wound_test_roll",
            "mortal_world",
            "definition_roll",
            "roll_modifier");
        definition["components"]![0]!["payload"]!["operations"] =
            new JsonArray("attack_roll", "defense_roll", "saving_throw");
        var result = WoundPersistedConsequenceEnvelopeAdapter.ValidateDetached(
            severityRank: 2,
            "persisted.consequences",
            new[]
            {
                new WoundPersistedConsequenceDefinition(
                    "definition_roll",
                    "persisted.definitions[0]",
                    JsonSerializer.SerializeToElement(definition))
            },
            new[]
            {
                new WoundPersistedConsequenceRoot(
                    "effect_roll",
                    "definition_roll",
                    "persisted.roots[0]",
                    new[]
                    {
                        new WoundEffectSlotAgreement(
                            1,
                            "roll_modifier",
                            "Несколько проверок затруднены одной раной.")
                    })
            },
            requireExactGlobalSlotAgreement: false,
            persistedSlotsUsed: 1);

        Assert.True(
            result.IsValid,
            string.Join(
                Environment.NewLine,
                result.Issues.Select(issue => $"{issue.FilePath}: {issue.Code}")));
        Assert.Single(result.DerivedSlots);
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
        var duplicateDefinition = json.Replace(
            "\"definitionKey\":\"definition_wound_test_bleeding\"",
            "\"definitionKey\":\"definition_wound_test_bleeding\",\"definitionKey\":\"forged\"",
            StringComparison.Ordinal);
        var duplicateBinding = json.Replace(
            "\"effectId\":\"effect_wound_test_bleeding\",\"definitionKey\":\"definition_wound_test_bleeding\"",
            "\"effectId\":\"effect_wound_test_bleeding\",\"effectId\":\"forged\",\"definitionKey\":\"definition_wound_test_bleeding\"",
            StringComparison.Ordinal);

        AssertInvalid(
            WoundMaterializationContract.Parse(duplicateRoot, Path),
            Path + ".woundId",
            "wound_materialization_duplicate_property");
        AssertInvalid(
            WoundMaterializationContract.Parse(duplicateNested, Path),
            Path + ".origin.sourceKind",
            "wound_materialization_duplicate_property");
        AssertInvalid(
            WoundMaterializationContract.Parse(duplicateDefinition, Path),
            Path + ".consequences.ownedEffectSources.definitions[0].definitionKey",
            "wound_materialization_duplicate_property");
        AssertInvalid(
            WoundMaterializationContract.Parse(duplicateBinding, Path),
            Path + ".consequences.ownedEffectSources.rootBindings[0].effectId",
            "wound_materialization_duplicate_property");
    }

    [Fact]
    public void Parse_OwnedEffectSources_IsMandatoryClosedAndNullSafe()
    {
        var missing = WoundContractTestData.CreateActiveWound();
        missing["consequences"]!.AsObject().Remove("ownedEffectSources");
        AssertInvalid(
            Parse(missing),
            Path + ".consequences.ownedEffectSources",
            "wound_materialization_missing_field");

        foreach (var (relativePath, mutate) in new (string, Action<JsonObject>)[]
                 {
                     ("ownedEffectSources", wound =>
                         wound["consequences"]!["ownedEffectSources"] = null),
                     ("ownedEffectSources", wound =>
                         wound["consequences"]!["ownedEffectSources"] = new JsonArray()),
                     ("ownedEffectSources.definitions", wound =>
                         Sources(wound)["definitions"] = null),
                     ("ownedEffectSources.definitions", wound =>
                         Sources(wound)["definitions"] = new JsonObject()),
                     ("ownedEffectSources.rootBindings", wound =>
                         Sources(wound)["rootBindings"] = null),
                     ("ownedEffectSources.rootBindings", wound =>
                         Sources(wound)["rootBindings"] = new JsonObject()),
                     ("ownedEffectSources.definitions[0]", wound =>
                         Definitions(wound)[0] = null),
                     ("ownedEffectSources.definitions[0]", wound =>
                         Definitions(wound)[0] = "not_an_object"),
                     ("ownedEffectSources.rootBindings[0]", wound =>
                         RootBindings(wound)[0] = null),
                     ("ownedEffectSources.rootBindings[0]", wound =>
                         RootBindings(wound)[0] = 17)
                 })
        {
            var wound = WoundContractTestData.CreateActiveWound();
            mutate(wound);
            AssertInvalid(
                Parse(wound),
                Path + ".consequences." + relativePath,
                "wound_materialization_invalid_field");
        }

        var unknown = WoundContractTestData.CreateActiveWound();
        Sources(unknown)["applicationRefs"] = new JsonArray();
        AssertInvalid(
            Parse(unknown),
            Path + ".consequences.ownedEffectSources.applicationRefs",
            "wound_materialization_unknown_field");

        foreach (var field in new[] { "parameters", "applicationRef", "sourceRef" })
        {
            var responseLocal = WoundContractTestData.CreateActiveWound();
            RootBindings(responseLocal)[0]![field] = field == "parameters"
                ? new JsonObject()
                : "response_local_value";
            AssertInvalid(
                Parse(responseLocal),
                $"{Path}.consequences.ownedEffectSources.rootBindings[0].{field}",
                "wound_materialization_unknown_field");
        }
    }

    [Fact]
    public void Parse_OwnedEffectSources_EnforcesExactFiveDefinitionAndRootBounds()
    {
        var atLimit = CreateSeverityFourFiveRootWound();
        Assert.Equal(WoundContractTestData.OwnedEffectDefinitionLimit, Definitions(atLimit).Count);
        Assert.Equal(WoundContractTestData.OwnedEffectRootBindingLimit, RootBindings(atLimit).Count);
        Assert.True(Parse(atLimit).IsValid, DescribeIssues(Parse(atLimit)));

        var legalFiveDefinitionLeafGraph = CreateSingleLeafWound();
        SetSeverity(legalFiveDefinitionLeafGraph, "IV", 4, "IV");
        legalFiveDefinitionLeafGraph["consequences"]!["slotBudget"] = 4;
        legalFiveDefinitionLeafGraph["consequences"]!["slotsUsed"] = 4;
        for (var index = 0; index < 2; index++)
        {
            var effectId = $"effect_wound_legal_leaf_root_{index}";
            var definitionKey = $"definition_wound_legal_leaf_root_{index}";
            var profile = WoundContractTestData.DistinctMortalProfile(index);
            Definitions(legalFiveDefinitionLeafGraph).Add(
                WoundContractTestData.CreateOwnedEffectDefinition(
                    "wound_test_torn_side",
                    "mortal_world",
                    definitionKey,
                    profile));
            RootBindings(legalFiveDefinitionLeafGraph).Add(
                WoundContractTestData.CreateRootBinding(effectId, definitionKey));
            Entries(legalFiveDefinitionLeafGraph).Add(new JsonObject
            {
                ["slot"] = index + 3,
                ["profileKey"] = profile,
                ["effectId"] = effectId,
                ["readableSummary"] = $"Допустимый корень {index + 1}."
            });
        }
        Definitions(legalFiveDefinitionLeafGraph).Add(
            WoundContractTestData.CreateOwnedEffectDefinition(
                "wound_test_torn_side",
                "mortal_world",
                "definition_wound_legal_marker",
                "wound_consequence"));
        RootBindings(legalFiveDefinitionLeafGraph).Add(
            WoundContractTestData.CreateRootBinding(
                "effect_wound_legal_marker",
                "definition_wound_legal_marker"));
        Assert.True(
            Parse(legalFiveDefinitionLeafGraph).IsValid,
            DescribeIssues(Parse(legalFiveDefinitionLeafGraph)));

        var tooManyDefinitions = CreateSeverityFourFiveRootWound();
        Definitions(tooManyDefinitions).Add(
            WoundContractTestData.CreateOwnedEffectDefinition(
                "wound_test_torn_side",
                "mortal_world",
                "definition_wound_poison_tail",
                "wound_consequence"));
        var tooManyDefinitionsOriginal = tooManyDefinitions.ToJsonString();
        var tooManyDefinitionsJson = tooManyDefinitionsOriginal.Replace(
            "\"definitionKey\":\"definition_wound_poison_tail\"",
            "\"definitionKey\":\"definition_wound_poison_tail\",\"definitionKey\":\"forged\"",
            StringComparison.Ordinal);
        Assert.NotEqual(tooManyDefinitionsOriginal, tooManyDefinitionsJson);
        var tooManyDefinitionsResult = WoundMaterializationContract.Parse(
            tooManyDefinitionsJson,
            Path);
        AssertInvalid(
            tooManyDefinitionsResult,
            Path + ".consequences.ownedEffectSources.definitions",
            "wound_materialization_limit_exceeded");
        Assert.Single(tooManyDefinitionsResult.Issues);
        Assert.DoesNotContain(
            tooManyDefinitionsResult.Issues,
            issue => issue.FilePath.StartsWith(
                $"{Path}.consequences.ownedEffectSources.definitions[{WoundContractTestData.OwnedEffectDefinitionLimit}]",
                StringComparison.Ordinal));

        var tooManyRoots = CreateSeverityFourFiveRootWound();
        RootBindings(tooManyRoots).Add(
            WoundContractTestData.CreateRootBinding(
                "effect_wound_poison_tail",
                "definition_wound_marker"));
        var tooManyRootsOriginal = tooManyRoots.ToJsonString();
        var tooManyRootsJson = tooManyRootsOriginal.Replace(
            "\"effectId\":\"effect_wound_poison_tail\",\"definitionKey\":\"definition_wound_marker\"",
            "\"effectId\":\"effect_wound_poison_tail\",\"effectId\":\"forged\",\"definitionKey\":\"definition_wound_marker\"",
            StringComparison.Ordinal);
        Assert.NotEqual(tooManyRootsOriginal, tooManyRootsJson);
        var tooManyRootsResult = WoundMaterializationContract.Parse(tooManyRootsJson, Path);
        AssertInvalid(
            tooManyRootsResult,
            Path + ".consequences.ownedEffectSources.rootBindings",
            "wound_materialization_limit_exceeded");
        Assert.Single(tooManyRootsResult.Issues);
        Assert.DoesNotContain(
            tooManyRootsResult.Issues,
            issue => issue.FilePath.StartsWith(
                $"{Path}.consequences.ownedEffectSources.rootBindings[{WoundContractTestData.OwnedEffectRootBindingLimit}]",
                StringComparison.Ordinal));

        var fourRootsPlusLeaf = CreateSingleLeafWound();
        SetSeverity(fourRootsPlusLeaf, "IV", 4, "IV");
        fourRootsPlusLeaf["consequences"]!["slotBudget"] = 4;
        for (var index = 0; index < 3; index++)
        {
            var effectId = $"effect_wound_boundary_{index}";
            var definitionKey = $"definition_wound_boundary_{index}";
            var profile = WoundContractTestData.DistinctMortalProfile(index);
            Definitions(fourRootsPlusLeaf).Add(
                WoundContractTestData.CreateOwnedEffectDefinition(
                    "wound_test_torn_side",
                    "mortal_world",
                    definitionKey,
                    profile));
            RootBindings(fourRootsPlusLeaf).Add(
                WoundContractTestData.CreateRootBinding(effectId, definitionKey));
            Entries(fourRootsPlusLeaf).Add(new JsonObject
            {
                ["slot"] = index + 3,
                ["profileKey"] = profile,
                ["effectId"] = effectId,
                ["readableSummary"] = $"Граничное следствие {index + 1}."
            });
        }
        fourRootsPlusLeaf["consequences"]!["slotsUsed"] = 5;
        AssertInvalid(
            Parse(fourRootsPlusLeaf),
            Path + ".consequences.slotsUsed",
            "wound_materialization_consequence_slot_invalid");
    }

    [Fact]
    public void Parse_OwnedEffectSources_ValidatesIdentityStackingLinksAndReachability()
    {
        var cases = new (string Path, Action<JsonObject> Mutate)[]
        {
            (
                "wound.consequences.ownedEffectSources.definitions[1].definitionKey",
                wound =>
                {
                    var duplicate = Definitions(wound)[0]!["definitionKey"]!.GetValue<string>();
                    Definitions(wound)[1]!["definitionKey"] = duplicate;
                    RootBindings(wound)[1]!["definitionKey"] = duplicate;
                }),
            (
                "wound.consequences.ownedEffectSources.definitions[1].definitionKey",
                wound =>
                {
                    Definitions(wound)[1]!["definitionKey"] = "definition_wound_test_bl\u0435eding";
                    RootBindings(wound)[1]!["definitionKey"] = "definition_wound_test_bl\u0435eding";
                }),
            (
                "wound.consequences.ownedEffectSources.rootBindings[1].effectId",
                wound => RootBindings(wound)[1]!["effectId"] = "effect_wound_test_bl\u0435eding"),
            (
                "wound.consequences.ownedEffectSources.rootBindings[1].effectId",
                wound =>
                {
                    var duplicate = RootBindings(wound)[0]!["effectId"]!.GetValue<string>();
                    RootBindings(wound)[1]!["effectId"] = duplicate;
                    Entries(wound)[1]!["effectId"] = duplicate;
                }),
            (
                "wound.consequences.ownedEffectSources.definitions[1].stacking.stackKey",
                wound => Definitions(wound)[1]!["stacking"]!["stackKey"] =
                    Definitions(wound)[0]!["stacking"]!["stackKey"]!.GetValue<string>()),
            (
                "wound.consequences.ownedEffectSources.definitions[1].stacking.stackKey",
                wound => Definitions(wound)[1]!["stacking"]!["stackKey"] =
                    "stack_definition_wound_test_bl\u0435eding"),
            (
                "wound.consequences.ownedEffectSources.definitions[0].stacking.maxStacks",
                wound => Definitions(wound)[0]!["stacking"]!["maxStacks"] = 2),
            (
                "wound.consequences.ownedEffectSources.definitions[0].lifetime.activePredicate",
                wound => Definitions(wound)[0]!["lifetime"]!["activePredicate"] = "carried"),
            (
                "wound.consequences.ownedEffectSources.definitions[0].lifetime.activePredicate",
                wound => Definitions(wound)[0]!["lifetime"]!["activePredicate"] = "equipped"),
            (
                "wound.consequences.ownedEffectSources.definitions[0].lifetime.activePredicate",
                wound => Definitions(wound)[0]!["lifetime"]!["activePredicate"] = "unlocked"),
            (
                "wound.consequences.ownedEffectSources.definitions[0].links",
                wound => Definitions(wound)[0]!["links"]![0]!["targetId"] = "wound_other"),
            (
                "wound.consequences.ownedEffectSources.definitions[0].parameterBounds",
                wound => Definitions(wound)[0]!["parameterBounds"] = new JsonObject
                {
                    ["amount"] = new JsonObject
                    {
                        ["kind"] = "number",
                        ["minimum"] = 1,
                        ["maximum"] = 4
                    }
                }),
            (
                "wound.consequences.ownedEffectSources.definitions[2]",
                wound => Definitions(wound).Add(
                    WoundContractTestData.CreateOwnedEffectDefinition(
                        "wound_test_torn_side",
                        "mortal_world",
                        "definition_wound_orphan"))),
            (
                "wound.consequences.ownedEffectSources.rootBindings[0].definitionKey",
                wound => RootBindings(wound)[0]!["definitionKey"] = "definition_missing"),
            (
                "wound.consequences.ownedEffectSources.rootBindings[1].definitionKey",
                wound => RootBindings(wound)[1]!["definitionKey"] =
                    "definition_wound_test_bleeding"),
            (
                "wound.consequences.ownedEffectSources.definitions[0].allowedTargetKinds",
                wound => Definitions(wound)[0]!["allowedTargetKinds"] =
                    new JsonArray("npc"))
        };

        foreach (var (expectedPath, mutate) in cases)
        {
            var wound = WoundContractTestData.CreateActiveWound();
            mutate(wound);
            AssertInvalid(
                Parse(wound),
                expectedPath,
                "wound_materialization_owned_source_graph_invalid");
        }
    }

    [Theory]
    [InlineData("independent", "no_change", null, null)]
    [InlineData("stack", "no_change", null, null)]
    [InlineData("refresh", "refresh", "reset", null)]
    [InlineData("refresh", "refresh", "extend", null)]
    [InlineData("replace", "no_change", null, null)]
    [InlineData("merge", "no_change", null, "sum")]
    public void Parse_OwnedEffectSources_AcceptsEveryLegalDirectRootStackingPolicy(
        string policy,
        string atMaximum,
        string? refreshMode,
        string? mergeRule)
    {
        var wound = WoundContractTestData.CreateActiveWound();
        var stacking = Definitions(wound)[0]!["stacking"]!.AsObject();
        stacking["policy"] = policy;
        stacking["maxStacks"] = 1;
        stacking["atMaximum"] = atMaximum;
        stacking["refreshMode"] = refreshMode;
        stacking["mergeRule"] = mergeRule;

        var result = Parse(wound);

        Assert.True(result.IsValid, DescribeIssues(result));
    }

    [Theory]
    [InlineData("independent", "refresh", null, null, "atMaximum")]
    [InlineData("independent", "component_response", null, null, "atMaximum")]
    [InlineData("independent", "no_change", "reset", null, "refreshMode")]
    [InlineData("independent", "no_change", null, "sum", "mergeRule")]
    [InlineData("stack", "no_change", "reset", null, "refreshMode")]
    [InlineData("stack", "no_change", null, "sum", "mergeRule")]
    [InlineData("refresh", "refresh", "reset", "sum", "mergeRule")]
    [InlineData("replace", "no_change", "reset", null, "refreshMode")]
    [InlineData("replace", "no_change", null, "sum", "mergeRule")]
    [InlineData("merge", "no_change", "reset", "sum", "refreshMode")]
    public void Parse_OwnedEffectSources_RejectsNonCanonicalStackingCombinations(
        string policy,
        string atMaximum,
        string? refreshMode,
        string? mergeRule,
        string offendingField)
    {
        var wound = WoundContractTestData.CreateActiveWound();
        var stacking = Definitions(wound)[0]!["stacking"]!.AsObject();
        stacking["policy"] = policy;
        stacking["maxStacks"] = 1;
        stacking["atMaximum"] = atMaximum;
        stacking["refreshMode"] = refreshMode;
        stacking["mergeRule"] = mergeRule;

        AssertInvalid(
            Parse(wound),
            Path + $".consequences.ownedEffectSources.definitions[0].stacking.{offendingField}",
            "wound_materialization_owned_source_graph_invalid");
    }

    [Fact]
    public void Parse_OwnedEffectSources_RequiresClosedAcyclicExactWoundGraph()
    {
        var unresolved = CreateSingleLeafWound();
        Definitions(unresolved)[0]!["components"]![0]!["payload"]!["definitionKey"] =
            "definition_missing";
        AssertInvalid(
            Parse(unresolved),
            Path + ".consequences.ownedEffectSources.definitions[0].components[0].payload.definitionKey",
            "wound_materialization_owned_source_graph_invalid");

        var cyclic = CreateSingleLeafWound();
        Definitions(cyclic)[1] = WoundContractTestData.CreateApplyDefinitionRoot(
            "wound_test_torn_side",
            "mortal_world",
            "definition_wound_leaf",
            "definition_wound_reaction_root");
        AssertInvalid(
            Parse(cyclic),
            Path + ".consequences.ownedEffectSources.definitions[1].components[0].payload.definitionKey",
            "wound_materialization_owned_source_graph_invalid");

        var missingLink = WoundContractTestData.CreateActiveWound();
        Definitions(missingLink)[0]!["links"] = new JsonArray();
        AssertInvalid(
            Parse(missingLink),
            Path + ".consequences.ownedEffectSources.definitions[0].links",
            "wound_materialization_owned_source_graph_invalid");

        var confusableLink = WoundContractTestData.CreateActiveWound();
        Definitions(confusableLink)[0]!["links"]![0]!["targetId"] =
            "wound_test_torn_s\u0456de";
        AssertInvalid(
            Parse(confusableLink),
            Path + ".consequences.ownedEffectSources.definitions[0].links[0].targetId",
            "wound_materialization_owned_source_graph_invalid");

        var duplicateLink = WoundContractTestData.CreateActiveWound();
        Definitions(duplicateLink)[0]!["links"]!.AsArray().Add(
            Definitions(duplicateLink)[0]!["links"]![0]!.DeepClone());
        AssertInvalid(
            Parse(duplicateLink),
            Path + ".consequences.ownedEffectSources.definitions[0].links[1]",
            "wound_materialization_owned_source_graph_invalid");

        var wrongMarkerWound = CreateNonMechanicalWound(includeMarker: true);
        Definitions(wrongMarkerWound)[0]!["components"]![0]!["payload"]!["woundId"] =
            "wound_other";
        AssertInvalid(
            Parse(wrongMarkerWound),
            Path + ".consequences.ownedEffectSources.definitions[0].components[0].payload.woundId",
            "wound_materialization_owned_source_graph_invalid");
    }

    [Fact]
    public void Parse_OwnedEffectSources_RejectsAdditionalNonWoundSourceAuthority()
    {
        var wound = WoundContractTestData.CreateActiveWound();
        Definitions(wound)[0]!["links"]!.AsArray().Add(new JsonObject
        {
            ["kind"] = "quest",
            ["targetId"] = "quest_unrelated_source",
            ["role"] = "source"
        });

        AssertInvalid(
            Parse(wound),
            Path + ".consequences.ownedEffectSources.definitions[0].links[1].kind",
            "wound_materialization_owned_source_graph_invalid");
    }

    [Fact]
    public void Parse_OwnedEffectSources_AcceptsOneLeafAtMaxTwoAndRejectsNestedOrSecondEdge()
    {
        var legal = CreateSingleLeafWound();
        Assert.True(Parse(legal).IsValid, DescribeIssues(Parse(legal)));

        foreach (var rawMaximum in new[] { "1", "3", "2.0", "2e0" })
        {
            var invalidMaximum = CreateSingleLeafWound();
            Definitions(invalidMaximum)[0]!["components"]![0]!["payload"]!["maxExpansion"] =
                JsonNode.Parse(rawMaximum);
            AssertInvalid(
                Parse(invalidMaximum),
                Path + ".consequences.ownedEffectSources.definitions[0].components[0].payload.maxExpansion",
                "wound_materialization_owned_source_graph_invalid");
        }

        var nested = CreateSingleLeafWound();
        Definitions(nested)[0]!["components"]![0]!["payload"]!["parameters"] =
            new JsonObject();
        var grandchild = WoundContractTestData.CreateOwnedEffectDefinition(
            "wound_test_torn_side",
            "mortal_world",
            "definition_wound_grandchild",
            "wound_consequence");
        var nestedLeaf = WoundContractTestData.CreateApplyDefinitionRoot(
            "wound_test_torn_side",
            "mortal_world",
            "definition_wound_leaf",
            "definition_wound_grandchild");
        Definitions(nested)[1] = nestedLeaf;
        Definitions(nested).Add(grandchild);
        AssertInvalid(
            Parse(nested),
            Path + ".consequences.ownedEffectSources.definitions[1].components[0].payload.definitionKey",
            "wound_materialization_owned_source_graph_invalid");

        var twoEdges = CreateSingleLeafWound();
        SetSeverity(twoEdges, "III", 3, "III");
        twoEdges["consequences"]!["slotBudget"] = 3;
        twoEdges["consequences"]!["slotsUsed"] = 3;
        var secondReaction = Definitions(twoEdges)[0]!["components"]![0]!
            .DeepClone().AsObject();
        secondReaction["componentId"] = "component_reaction_second";
        secondReaction["payload"]!["definitionKey"] = "definition_wound_marker_leaf";
        secondReaction["payload"]!["parameters"] = new JsonObject();
        Definitions(twoEdges)[0]!["components"]!.AsArray().Add(secondReaction);
        Definitions(twoEdges)[0]!["triggers"]![0]!["componentIds"]!
            .AsArray().Add("component_reaction_second");
        Definitions(twoEdges).Add(
            WoundContractTestData.CreateOwnedEffectDefinition(
                "wound_test_torn_side",
                "mortal_world",
                "definition_wound_marker_leaf",
                "wound_consequence"));
        Entries(twoEdges).Add(new JsonObject
        {
            ["slot"] = 3,
            ["profileKey"] = "event_reaction",
            ["effectId"] = "effect_wound_reaction_root",
            ["readableSummary"] = "Вторая реакция запрещена."
        });
        AssertInvalid(
            Parse(twoEdges),
            Path + ".consequences.ownedEffectSources.definitions[0].components[1].payload.definitionKey",
            "wound_materialization_owned_source_graph_invalid");

        var descendantMarker = CreateSingleLeafWound();
        SetSeverity(descendantMarker, "III", 3, "III");
        descendantMarker["consequences"]!["slotBudget"] = 3;
        descendantMarker["consequences"]!["slotsUsed"] = 1;
        Definitions(descendantMarker)[0]!["components"]![0]!["payload"]!["parameters"] =
            new JsonObject();
        Definitions(descendantMarker)[1] =
            WoundContractTestData.CreateOwnedEffectDefinition(
                "wound_test_torn_side",
                "mortal_world",
                "definition_wound_leaf",
                "wound_consequence");
        Entries(descendantMarker).RemoveAt(1);
        Assert.True(Parse(descendantMarker).IsValid, DescribeIssues(Parse(descendantMarker)));

        Definitions(descendantMarker).Add(
            WoundContractTestData.CreateOwnedEffectDefinition(
                "wound_test_torn_side",
                "mortal_world",
                "definition_wound_direct_marker",
                "wound_consequence"));
        RootBindings(descendantMarker).Add(
            WoundContractTestData.CreateRootBinding(
                "effect_wound_direct_marker",
                "definition_wound_direct_marker"));
        AssertInvalid(
            Parse(descendantMarker),
            Path + ".consequences.ownedEffectSources.definitions[2].components[0].profile",
            "wound_materialization_owned_source_graph_invalid");

        var rootBoundTarget = CreateSingleLeafWound(bindLeaf: true, leafPolicy: "replace");
        Assert.True(Parse(rootBoundTarget).IsValid, DescribeIssues(Parse(rootBoundTarget)));

        var parameterizedRootBoundTarget =
            CreateSingleLeafWound(bindLeaf: true, leafPolicy: "replace");
        Definitions(parameterizedRootBoundTarget)[0]!["components"]![0]!["payload"]!["parameters"] =
            new JsonObject { ["amount"] = 3 };
        AssertInvalid(
            Parse(parameterizedRootBoundTarget),
            Path + ".consequences.ownedEffectSources.definitions[0].components[0].payload.parameters",
            "wound_materialization_owned_source_graph_invalid");

        var wrongPolicy = CreateSingleLeafWound(bindLeaf: true, leafPolicy: "independent");
        AssertInvalid(
            Parse(wrongPolicy),
            Path + ".consequences.ownedEffectSources.definitions[1].stacking.policy",
            "wound_materialization_owned_source_graph_invalid");

        var crossDomain = CreateSingleLeafWound(bindLeaf: true, leafPolicy: "replace");
        crossDomain["complications"]!.AsArray().Add(CreateComplication(
            1,
            "effect_wound_reaction_root"));
        AssertInvalid(
            Parse(crossDomain),
            Path + ".consequences.ownedEffectSources.definitions[0].components[0].payload.definitionKey",
            "wound_materialization_owned_source_graph_invalid");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Parse_RootBoundReactionOwnership_DoesNotConfuseLiteralBaseWoundComplication(bool sameComplication)
    {
        var wound = CreateSingleLeafWound(bindLeaf: true, leafPolicy: "replace");
        var complication = CreateComplication(1, "effect_wound_reaction_root");
        complication["complicationId"] = "base_wound";
        if (sameComplication)
            complication["ownedEffectIds"]!.AsArray().Add("effect_wound_reaction_leaf");
        wound["complications"]!.AsArray().Add(complication);

        var result = Parse(wound);
        if (sameComplication)
            Assert.True(result.IsValid, DescribeIssues(result));
        else
            AssertInvalid(result,
                Path + ".consequences.ownedEffectSources.definitions[0].components[0].payload.definitionKey",
                "wound_materialization_owned_source_graph_invalid");
    }

    [Theory]
    [InlineData("I", 1)]
    [InlineData("II", 2)]
    public void Parse_OwnedEffectSources_RejectsLeafExpansionBelowSeverityThree(
        string severity,
        int rank)
    {
        var wound = CreateSingleLeafWound();
        SetSeverity(wound, severity, rank, severity);
        wound["consequences"]!["slotBudget"] = rank;
        wound["consequences"]!["slotsUsed"] = 1;
        Definitions(wound)[0]!["components"]![0]!["payload"]!["parameters"] =
            new JsonObject();
        Definitions(wound)[1] =
            WoundContractTestData.CreateOwnedEffectDefinition(
                "wound_test_torn_side",
                "mortal_world",
                "definition_wound_leaf",
                "wound_consequence");
        Entries(wound).RemoveAt(1);

        AssertInvalid(
            Parse(wound),
            Path + ".consequences.ownedEffectSources.definitions[0].components[0].payload.definitionKey",
            "wound_materialization_owned_source_graph_invalid");
    }

    [Theory]
    [InlineData("independent", null, null)]
    [InlineData("stack", null, null)]
    [InlineData("refresh", "reset", null)]
    [InlineData("merge", null, "sum")]
    public void Parse_RootBoundReactionTarget_RejectsEveryNonReplacePolicy(
        string policy,
        string? refreshMode,
        string? mergeRule)
    {
        var wound = CreateSingleLeafWound(bindLeaf: true, leafPolicy: "replace");
        var stacking = Definitions(wound)[1]!["stacking"]!.AsObject();
        stacking["policy"] = policy;
        stacking["refreshMode"] = refreshMode;
        stacking["mergeRule"] = mergeRule;
        stacking["atMaximum"] = policy == "refresh" ? "refresh" : "no_change";

        AssertInvalid(
            Parse(wound),
            Path + ".consequences.ownedEffectSources.definitions[1].stacking.policy",
            "wound_materialization_owned_source_graph_invalid");
    }

    [Fact]
    public void Parse_OwnedEffectSources_RequiresReciprocalDisjointOwnershipAndExactSlots()
    {
        var sharedRoot = CreateSingleLeafWound();
        Assert.Equal(
            "effect_wound_reaction_root",
            sharedRoot["consequences"]!["entries"]![0]!["effectId"]!.GetValue<string>());
        Assert.Equal(
            "effect_wound_reaction_root",
            sharedRoot["consequences"]!["entries"]![1]!["effectId"]!.GetValue<string>());
        Assert.True(Parse(sharedRoot).IsValid, DescribeIssues(Parse(sharedRoot)));

        var missingRoot = WoundContractTestData.CreateActiveWound();
        RootBindings(missingRoot).RemoveAt(1);
        AssertInvalid(
            Parse(missingRoot),
            Path + ".consequences.entries[1].effectId",
            "wound_materialization_effect_binding_invalid");

        var overlapping = WoundContractTestData.CreateActiveWound();
        overlapping["complications"]!.AsArray().Add(CreateComplication(
            1,
            "effect_wound_test_bleeding"));
        overlapping["complications"]!.AsArray().Add(CreateComplication(
            2,
            "effect_wound_test_bleeding"));
        AssertInvalid(
            Parse(overlapping),
            Path + ".complications[1].ownedEffectIds[0]",
            "wound_materialization_effect_binding_invalid");

        var markerOnly = CreateNonMechanicalWound(includeMarker: true);
        Assert.True(Parse(markerOnly).IsValid, DescribeIssues(Parse(markerOnly)));

        var oldSlotOnly = WoundContractTestData.CreateActiveWound();
        oldSlotOnly["consequences"]!.AsObject().Remove("ownedEffectSources");
        Assert.False(Parse(oldSlotOnly).IsValid);

        Assert.True(Parse(CreateNonMechanicalWound(includeMarker: false)).IsValid);

        var physicalOverRank = WoundContractTestData.CreateActiveWound();
        SetSeverity(physicalOverRank, "I", 1, "II");
        AssertInvalid(
            Parse(physicalOverRank),
            Path + ".consequences.slotsUsed",
            "wound_materialization_consequence_slot_invalid");

        var spiritualUnderRank = WoundContractTestData.CreateSpiritualActiveWound(
            woundId: "wound_spiritual_slots");
        Entries(spiritualUnderRank).RemoveAt(1);
        RootBindings(spiritualUnderRank).RemoveAt(1);
        Definitions(spiritualUnderRank).RemoveAt(1);
        spiritualUnderRank["consequences"]!["slotsUsed"] = 1;
        AssertInvalid(
            Parse(spiritualUnderRank),
            Path + ".consequences.slotsUsed",
            "wound_materialization_consequence_slot_invalid");
    }

    [Fact]
    public void Parse_OwnedEffectSources_ExposesDetachedCanonicalDefinitionFacts()
    {
        var source = CreateSingleLeafWound();
        var result = Parse(source);

        Assert.True(result.IsValid, DescribeIssues(result));
        var facts = result.Wound!.Consequences.OwnedEffectSources.DefinitionFacts;
        Assert.Equal(2, facts.Count);
        var root = Assert.Single(facts, static fact =>
            fact.DefinitionKey == "definition_wound_reaction_root");
        Assert.Equal(
            new[] { "definition_wound_leaf" },
            root.ApplyDefinitionTargets);
        Assert.False(root.ContainsWoundConsequenceMarker);
        var leaf = Assert.Single(facts, static fact =>
            fact.DefinitionKey == "definition_wound_leaf");
        Assert.Empty(leaf.ApplyDefinitionTargets);
        Assert.Contains(
            "\"definitionKey\":\"definition_wound_leaf\"",
            leaf.CanonicalJson,
            StringComparison.Ordinal);

        var retainedCanonical = root.CanonicalJson;
        source["consequences"]!["ownedEffectSources"]!["definitions"]![0]!["display"]!["description"] =
            "Mutation after parsing must not alter the typed fact.";
        Assert.Equal(retainedCanonical, root.CanonicalJson);

        var markerResult = Parse(CreateNonMechanicalWound(includeMarker: true));
        Assert.True(markerResult.IsValid, DescribeIssues(markerResult));
        Assert.True(Assert.Single(
            markerResult.Wound!.Consequences.OwnedEffectSources.DefinitionFacts)
            .ContainsWoundConsequenceMarker);
    }

    [Fact]
    public void Parse_OwnedEffectSources_RejectsOrphanedRootAndComplicationJoins()
    {
        var missingComplicationRoot = WoundContractTestData.CreateActiveWound();
        missingComplicationRoot["complications"]!.AsArray().Add(
            CreateComplication(1, "effect_missing_complication_root"));
        AssertInvalid(
            Parse(missingComplicationRoot),
            Path + ".complications[0].ownedEffectIds[0]",
            "wound_materialization_effect_binding_invalid");

        var confusableComplicationOverlap = WoundContractTestData.CreateActiveWound();
        confusableComplicationOverlap["complications"]!.AsArray().Add(
            CreateComplication(1, "effect_wound_test_bleeding"));
        confusableComplicationOverlap["complications"]!.AsArray().Add(
            CreateComplication(2, "effect_wound_test_bl\u0435eding"));
        AssertInvalid(
            Parse(confusableComplicationOverlap),
            Path + ".complications[1].ownedEffectIds[0]",
            "wound_materialization_effect_binding_invalid");

        var orphanMechanicalRoot = WoundContractTestData.CreateActiveWound();
        Definitions(orphanMechanicalRoot).Add(
            WoundContractTestData.CreateOwnedEffectDefinition(
                "wound_test_torn_side",
                "mortal_world",
                "definition_orphan_mechanical_root",
                "characteristic_modifier"));
        RootBindings(orphanMechanicalRoot).Add(
            WoundContractTestData.CreateRootBinding(
                "effect_orphan_mechanical_root",
                "definition_orphan_mechanical_root"));
        AssertInvalid(
            Parse(orphanMechanicalRoot),
            Path + ".consequences.ownedEffectSources.rootBindings[2].effectId",
            "wound_materialization_effect_binding_invalid");

        var descendantUsedAsSlotAuthority = CreateSingleLeafWound();
        Entries(descendantUsedAsSlotAuthority)[1]!["effectId"] =
            "effect_reaction_descendant_not_a_root";
        AssertInvalid(
            Parse(descendantUsedAsSlotAuthority),
            Path + ".consequences.entries[1].effectId",
            "wound_materialization_effect_binding_invalid");

        var displayOnlyMarker = CreateNonMechanicalWound(includeMarker: true);
        displayOnlyMarker["care"]!["state"] = "stabilized";
        displayOnlyMarker["care"]!["stabilizedAtTurn"] = 42;
        displayOnlyMarker["recovery"]!["blockers"] = new JsonArray();
        AssertInvalid(
            Parse(displayOnlyMarker),
            Path + ".consequences.lifecycleEvidence",
            "wound_consequence_non_display_impact_required");
    }

    [Fact]
    public void Parse_EmptyMortalGraph_AcceptsNoNaturalRecoveryAsLifecycleConstraint()
    {
        var wound = CreateNonMechanicalWound(includeMarker: false);
        wound["care"]!["state"] = "stabilized";
        wound["care"]!["stabilizedAtTurn"] = 42;
        wound["recovery"]!["mode"] = "no_natural_recovery";
        wound["recovery"]!["blockers"] = new JsonArray();

        var result = Parse(wound);

        Assert.True(result.IsValid, DescribeIssues(result));
    }

    [Fact]
    public void Parse_EmptyMortalGraph_DoesNotTrustOpaqueDeteriorationAsLifecycleEvidence()
    {
        var policies = new JsonObject[]
        {
            new(),
            new() { ["kind"] = "unvalidated_future_policy" }
        };

        foreach (var policy in policies)
        {
            var wound = CreateNonMechanicalWound(includeMarker: false);
            wound["care"]!["state"] = "stabilized";
            wound["care"]!["stabilizedAtTurn"] = 42;
            wound["recovery"]!["blockers"] = new JsonArray();
            wound["recovery"]!["deteriorationPolicy"] = policy;

            AssertInvalid(
                Parse(wound),
                Path + ".consequences.lifecycleEvidence",
                "wound_consequence_non_display_impact_required");
        }
    }

    [Fact]
    public void CanonicalSerialization_OrdersDetachesAndFingerprintsOwnedSourceGraph()
    {
        var callerOwned = WoundContractTestData.CreateActiveWound();
        var firstResult = Parse(callerOwned);
        Assert.True(firstResult.IsValid, DescribeIssues(firstResult));
        var firstWound = Assert.IsType<WoundMaterializationEnvelope>(firstResult.Wound);
        var canonical = WoundMaterializationContract.SerializeCanonical(firstWound);

        var reordered = WoundContractTestData.CreateActiveWound();
        Sources(reordered)["definitions"] = ReverseArray(Definitions(reordered));
        Sources(reordered)["rootBindings"] = ReverseArray(RootBindings(reordered));
        reordered["consequences"]!["entries"] = ReverseArray(Entries(reordered));
        var reorderedResult = Parse(reordered);
        Assert.True(reorderedResult.IsValid, DescribeIssues(reorderedResult));
        Assert.Equal(
            canonical,
            WoundMaterializationContract.SerializeCanonical(reorderedResult.Wound!));

        Definitions(callerOwned)[0]!["display"]!["description"] = "Подмена после parse";
        RootBindings(callerOwned)[0]!["effectId"] = "effect_mutated_after_parse";
        Assert.Equal(canonical, WoundMaterializationContract.SerializeCanonical(firstWound));

        var changed = WoundContractTestData.CreateActiveWound();
        Definitions(changed)[0]!["display"]!["description"] = "Иное каноническое следствие";
        var changedResult = Parse(changed);
        Assert.True(changedResult.IsValid, DescribeIssues(changedResult));
        Assert.NotEqual(
            WoundIdentityState.ComputeSemanticFingerprint(firstWound),
            WoundIdentityState.ComputeSemanticFingerprint(changedResult.Wound!));

        var changedBinding = WoundContractTestData.CreateActiveWound();
        RootBindings(changedBinding)[0]!["effectId"] = "effect_wound_rebound";
        Entries(changedBinding)[0]!["effectId"] = "effect_wound_rebound";
        var changedBindingResult = Parse(changedBinding);
        Assert.True(changedBindingResult.IsValid, DescribeIssues(changedBindingResult));
        Assert.NotEqual(
            WoundIdentityState.ComputeSemanticFingerprint(firstWound),
            WoundIdentityState.ComputeSemanticFingerprint(changedBindingResult.Wound!));

        var completeGraphResult = Parse(CreateSingleLeafWound());
        Assert.True(completeGraphResult.IsValid, DescribeIssues(completeGraphResult));
        var completeGraphWound = completeGraphResult.Wound!;
        var completeGraphCanonical =
            WoundMaterializationContract.SerializeCanonical(completeGraphWound);
        var nestedReordered = CreateSingleLeafWound();
        for (var index = 0; index < Definitions(nestedReordered).Count; index++)
        {
            Definitions(nestedReordered)[index] =
                ReverseEveryObject(Definitions(nestedReordered)[index]);
        }
        for (var index = 0; index < RootBindings(nestedReordered).Count; index++)
        {
            RootBindings(nestedReordered)[index] =
                ReverseEveryObject(RootBindings(nestedReordered)[index]);
        }
        var nestedResult = Parse(nestedReordered);
        Assert.True(nestedResult.IsValid, DescribeIssues(nestedResult));
        Assert.Equal(
            completeGraphCanonical,
            WoundMaterializationContract.SerializeCanonical(nestedResult.Wound!));
        Assert.Equal(
            WoundIdentityState.ComputeSemanticFingerprint(completeGraphWound),
            WoundIdentityState.ComputeSemanticFingerprint(nestedResult.Wound!));

        var canonicalNode = JsonNode.Parse(canonical)!.AsObject();
        Assert.Equal(
            new[] { "definitions", "rootBindings" },
            canonicalNode["consequences"]!["ownedEffectSources"]!.AsObject()
                .Select(static property => property.Key));

        var emptyResult = Parse(CreateNonMechanicalWound(includeMarker: false));
        Assert.True(emptyResult.IsValid, DescribeIssues(emptyResult));
        var emptyCanonical = JsonNode.Parse(
            WoundMaterializationContract.SerializeCanonical(emptyResult.Wound!))!.AsObject();
        Assert.Empty(emptyCanonical["consequences"]!["ownedEffectSources"]!["definitions"]!.AsArray());
        Assert.Empty(emptyCanonical["consequences"]!["ownedEffectSources"]!["rootBindings"]!.AsArray());
    }

    [Fact]
    public void CanonicalSerialization_FingerprintsAndRoundTripsRootOnlyMarkerBinding()
    {
        var baselineResult = Parse(CreateNonMechanicalWound(includeMarker: true));
        Assert.True(baselineResult.IsValid, DescribeIssues(baselineResult));
        var baseline = baselineResult.Wound!;
        var baselineCanonical = WoundMaterializationContract.SerializeCanonical(baseline);

        var changed = CreateNonMechanicalWound(includeMarker: true);
        RootBindings(changed)[0]!["effectId"] = "effect_wound_marker_rebound";
        var changedResult = Parse(changed);
        Assert.True(changedResult.IsValid, DescribeIssues(changedResult));
        var changedWound = changedResult.Wound!;
        var changedCanonical = WoundMaterializationContract.SerializeCanonical(changedWound);

        Assert.NotEqual(baselineCanonical, changedCanonical);
        Assert.NotEqual(
            WoundIdentityState.ComputeSemanticFingerprint(baseline),
            WoundIdentityState.ComputeSemanticFingerprint(changedWound));
        var roundTrip = Parse(JsonNode.Parse(changedCanonical)!.AsObject());
        Assert.True(roundTrip.IsValid, DescribeIssues(roundTrip));
        Assert.Equal(
            changedCanonical,
            WoundMaterializationContract.SerializeCanonical(roundTrip.Wound!));
        Assert.Equal(
            WoundIdentityState.ComputeSemanticFingerprint(changedWound),
            WoundIdentityState.ComputeSemanticFingerprint(roundTrip.Wound!));
    }

    [Fact]
    public void Parse_OwnedEffectSources_PreservesLegalNonSourceBoundLifetime()
    {
        var wound = WoundContractTestData.CreateActiveWound();
        Definitions(wound)[0]!["lifetime"] = new JsonObject
        {
            ["mode"] = "turns",
            ["initialTurns"] = 3,
            ["advancePhase"] = "owner_turn_end"
        };

        var result = Parse(wound);

        Assert.True(result.IsValid, DescribeIssues(result));
        var canonical = JsonNode.Parse(
            WoundMaterializationContract.SerializeCanonical(result.Wound!))!.AsObject();
        Assert.Equal(
            "turns",
            canonical["consequences"]!["ownedEffectSources"]!["definitions"]![0]!
                ["lifetime"]!["mode"]!.GetValue<string>());
        Assert.Equal(
            WoundIdentityState.ComputeSemanticFingerprint(result.Wound!),
            WoundIdentityState.ComputeSemanticFingerprint(
                Assert.IsType<WoundMaterializationEnvelope>(Parse(canonical).Wound)));
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

    [Fact]
    public void Parse_InvalidOwnerKindReturnsValidationIssueWithoutThrowing()
    {
        var wound = WoundContractTestData.CreateActiveWound();
        wound["owner"]!["ownerKind"] = "unknown_owner_kind";
        WoundMaterializationParseResult? result = null;

        var exception = Record.Exception(() => result = Parse(wound));

        Assert.Null(exception);
        Assert.NotNull(result);
        AssertInvalid(
            result!,
            Path + ".owner.ownerKind",
            "wound_materialization_invalid_field");
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
                var template = routes[0]!.DeepClone().AsObject();
                routes.Clear();
                for (var index = 0; index < count; index++)
                    routes.Add(CreateRoute(template, index));
                wound["treatment"]!["knownRouteIds"] = new JsonArray(
                    Enumerable.Range(0, count)
                        .Select(static index => (JsonNode?)$"route_{index}")
                        .ToArray());
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
            "\"result\":[{\"kind\":\"stabilize\"},{\"kind\":\"reduce_severity\",\"steps\":1}]",
            first,
            StringComparison.Ordinal);
        Assert.DoesNotContain("\"itemId\":", first, StringComparison.Ordinal);
        Assert.DoesNotContain("\"skillId\":", first, StringComparison.Ordinal);
        Assert.DoesNotContain("\"results\":", first, StringComparison.Ordinal);
    }

    [Fact]
    public void CanonicalSerialization_PhysicalWoundOutsideMortalWorldUsesCompatibilityProjection()
    {
        var source = WoundContractTestData.CreateActiveWound(
            realm: "chaos_sea",
            ownerKind: "player_soul",
            ownerId: "player_soul_current",
            carrierPath: "game_state/meta/afterlife_entity_profiles.json#/playerSoul");
        source["treatment"]!["routes"]![0]!["resolution"]!["formulaKey"] =
            "legacy_physical_afterlife_formula";
        var parsed = Parse(source);
        Assert.True(parsed.IsValid, DescribeIssues(parsed));

        var canonical = WoundMaterializationContract.SerializeCanonical(parsed.Wound!);
        var reparsed = WoundMaterializationContract.Parse(canonical, Path);

        Assert.True(reparsed.IsValid, DescribeIssues(reparsed));
        Assert.Equal(canonical, WoundMaterializationContract.SerializeCanonical(reparsed.Wound!));
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
                ["profileKey"] = WoundContractTestData.DistinctMortalProfile(index),
                ["effectId"] = $"effect_{index}",
                ["readableSummary"] = $"Последствие {index + 1}."
            });
        consequences["ownedEffectSources"] =
            WoundContractTestData.CreateOwnedEffectSources(
                wound["woundId"]!.GetValue<string>(),
                wound["owner"]!["realm"]!.GetValue<string>(),
                Enumerable.Range(0, count)
                    .Select(index => (
                        $"effect_{index}",
                        $"definition_effect_{index}",
                        WoundContractTestData.DistinctMortalProfile(index)))
                    .ToArray());
    }

    private static JsonObject CreateSeverityFourFiveRootWound()
    {
        var wound = WoundContractTestData.CreateActiveWound();
        SetSeverity(wound, "IV", 4, "IV");
        var consequences = wound["consequences"]!.AsObject();
        consequences["slotBudget"] = 4;
        consequences["slotsUsed"] = 4;
        consequences["entries"] = WoundContractTestData.Repeat(4, index =>
            new JsonObject
            {
                ["slot"] = index + 1,
                ["profileKey"] = WoundContractTestData.DistinctMortalProfile(index),
                ["effectId"] = $"effect_wound_root_{index}",
                ["readableSummary"] = $"Ограничение {index + 1}."
            });
        consequences["ownedEffectSources"] =
            WoundContractTestData.CreateOwnedEffectSources(
                "wound_test_torn_side",
                "mortal_world",
                ("effect_wound_root_0", "definition_wound_root_0", "action_control"),
                ("effect_wound_root_1", "definition_wound_root_1", "characteristic_modifier"),
                ("effect_wound_root_2", "definition_wound_root_2", "resistance_modifier"),
                ("effect_wound_root_3", "definition_wound_root_3", "periodic_damage"),
                ("effect_wound_marker", "definition_wound_marker", "wound_consequence"));
        return wound;
    }

    private static JsonObject CreateSingleLeafWound(
        bool bindLeaf = false,
        string leafPolicy = "independent")
    {
        const string woundId = "wound_test_torn_side";
        const string rootEffectId = "effect_wound_reaction_root";
        const string leafEffectId = "effect_wound_reaction_leaf";
        const string rootKey = "definition_wound_reaction_root";
        const string leafKey = "definition_wound_leaf";
        var wound = WoundContractTestData.CreateActiveWound();
        SetSeverity(wound, "III", 3, "III");
        var root = WoundContractTestData.CreateApplyDefinitionRoot(
            woundId,
            "mortal_world",
            rootKey,
            leafKey);
        root["components"]![0]!["payload"]!["parameters"] = bindLeaf
            ? new JsonObject()
            : new JsonObject { ["amount"] = 3 };
        var leaf = WoundContractTestData.CreateOwnedEffectDefinition(
            woundId,
            "mortal_world",
            leafKey,
            "periodic_damage");
        leaf["parameterBounds"] = bindLeaf
            ? new JsonObject()
            : new JsonObject
            {
                ["amount"] = new JsonObject
                {
                    ["kind"] = "number",
                    ["minimum"] = 1,
                    ["maximum"] = 10
                }
            };
        leaf["stacking"]!["policy"] = leafPolicy;

        var definitions = new JsonArray(root, leaf);
        var bindings = new JsonArray(
            WoundContractTestData.CreateRootBinding(rootEffectId, rootKey));
        if (bindLeaf)
            bindings.Add(WoundContractTestData.CreateRootBinding(leafEffectId, leafKey));
        var consequences = wound["consequences"]!.AsObject();
        consequences["slotBudget"] = 3;
        consequences["slotsUsed"] = 2;
        consequences["ownedEffectSources"] = new JsonObject
        {
            ["definitions"] = definitions,
            ["rootBindings"] = bindings
        };
        consequences["entries"] = new JsonArray(
            new JsonObject
            {
                ["slot"] = 1,
                ["profileKey"] = "event_reaction",
                ["effectId"] = rootEffectId,
                ["readableSummary"] = "Рана готова породить следствие."
            },
            new JsonObject
            {
                ["slot"] = 2,
                ["profileKey"] = "periodic_damage",
                ["effectId"] = bindLeaf ? leafEffectId : rootEffectId,
                ["readableSummary"] = "Худший результат реакции учтён заранее."
            });
        return wound;
    }

    private static JsonObject CreateNonMechanicalWound(bool includeMarker)
    {
        var wound = WoundContractTestData.CreateActiveWound();
        SetSeverity(wound, "I", 1, "II");
        var consequences = wound["consequences"]!.AsObject();
        consequences["slotBudget"] = 1;
        consequences["slotsUsed"] = 0;
        consequences["entries"] = new JsonArray();
        consequences["ownedEffectSources"] = includeMarker
            ? WoundContractTestData.CreateOwnedEffectSources(
                "wound_test_torn_side",
                "mortal_world",
                ("effect_wound_marker", "definition_wound_marker", "wound_consequence"))
            : WoundContractTestData.CreateOwnedEffectSources(
                "wound_test_torn_side",
                "mortal_world");
        return wound;
    }

    private static JsonObject Sources(JsonObject wound) =>
        wound["consequences"]!["ownedEffectSources"]!.AsObject();

    private static JsonArray Definitions(JsonObject wound) =>
        Sources(wound)["definitions"]!.AsArray();

    private static JsonArray RootBindings(JsonObject wound) =>
        Sources(wound)["rootBindings"]!.AsArray();

    private static JsonArray Entries(JsonObject wound) =>
        wound["consequences"]!["entries"]!.AsArray();

    private static JsonArray ReverseArray(JsonArray source) => new(
        source.Reverse().Select(static node => node?.DeepClone()).ToArray());

    private static JsonObject CreateComplication(
        int index,
        params string[] ownedEffectIds) => new()
    {
        ["complicationId"] = $"complication_{index}",
        ["kind"] = "infection",
        ["state"] = "active",
        ["displayName"] = $"Осложнение {index}",
        ["treatmentDifficultyModifier"] = 2,
        ["ownedEffectIds"] = new JsonArray(
            ownedEffectIds.Select(static value => (JsonNode?)value).ToArray()),
        ["visibility"] = "known_to_player"
    };

    private static JsonObject CreateRoute(JsonObject template, int index)
    {
        var route = template.DeepClone().AsObject();
        route["routeId"] = $"route_{index}";
        route["displayName"] = $"Маршрут {index}";
        route["visibility"] = "known_to_player";
        return route;
    }

    private static JsonObject CreateDiagnosisPath(int index) => new()
    {
        ["diagnosisPathId"] = $"diagnosis_{index}",
        ["displayName"] = $"Диагностика {index}",
        ["visibility"] = "gm_only",
        ["requiresKnownFacts"] = new JsonArray(),
        ["requirements"] = new JsonArray(),
        ["check"] = new JsonObject(),
        ["reveals"] = new JsonArray(),
        ["failurePolicy"] = "no_reveal"
    };

    private static JsonObject CreateRequirement(int index) => index == 1
        ? new JsonObject
        {
            ["kind"] = "skill_tier",
            ["capabilityRef"] = "field_medicine",
            ["minimumTier"] = 2,
            ["actorRole"] = "provider"
        }
        : new JsonObject
        {
            ["kind"] = "item_quantity",
            ["itemRef"] = $"item_{index}",
            ["quantity"] = 1,
            ["ownerRole"] = "provider"
        };

    private static JsonObject ReverseObject(JsonObject source)
    {
        var result = new JsonObject();
        foreach (var property in source.Reverse())
            result[property.Key] = property.Value?.DeepClone();
        return result;
    }

    private static JsonNode? ReverseEveryObject(JsonNode? source)
    {
        if (source is JsonObject sourceObject)
        {
            var result = new JsonObject();
            foreach (var property in sourceObject.Reverse())
                result[property.Key] = ReverseEveryObject(property.Value);
            return result;
        }
        if (source is JsonArray sourceArray)
        {
            return new JsonArray(sourceArray
                .Select(ReverseEveryObject)
                .ToArray());
        }
        return source?.DeepClone();
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
