using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// #1536/T081-D cheap retained DATA checks, deliberately without files, a world, signing or live owners.
/// Desired success of the exact factories comes first. The original bad description is retained
/// separately, and negatives start from a hand-authored canonical effect, never a repaired oracle.
/// </summary>
public sealed class SpiritualPressureRetainedAdmissibilityTests
{
    /// <summary>Proves every new exact factory branch is canonical DATA before signing or either desired-success rank-gate RED.</summary>
    [Theory]
    [InlineData(3, 4, "pair")]
    [InlineData(4, 3, "pair")]
    [InlineData(4, 4, "pair")]
    [InlineData(4, 3, "singleton")]
    [InlineData(3, 4, "selected-cost")]
    [InlineData(3, 4, "pressure-roll")]
    [InlineData(3, 4, "selected-cost-and-roll")]
    [InlineData(4, 4, "other-cost")]
    [InlineData(4, 4, "three-parents")]
    [InlineData(3, 4, "forbidden-art")]
    public void RankDomainExactFactoriesAreCanonicalBeforeSigning(int first, int second, string variant)
    {
        var count = variant == "singleton" ? 1 : variant == "three-parents" ? 3 : 2;
        var data = SpiritualPressureGroupOwnerCaseData.Create(count,
            selectedPressureCost: variant is "selected-cost" or "selected-cost-and-roll",
            otherPressureCost: variant == "other-cost",
            pressureRoll: variant is "pressure-roll" or "selected-cost-and-roll",
            ranks: new(first, second), rankFourPressureArt: variant == "forbidden-art");
        var profiles = SpiritualPressureRetainedDataAdmissibility.CreateProfiles(data);
        var before = Snapshot(data, profiles);
        AssertAccepted(SpiritualPressureRetainedDataAdmissibility.Check(data, profiles));
        Assert.Equal(before, Snapshot(data, profiles));
        for (var index = 0; index < count; index++)
        {
            var rank = index == 0 ? first : index == 1 ? second : 3;
            var parent = data.Parents[index];
            Assert.Equal(rank, parent["severity"]!["rank"]!.GetValue<int>());
            Assert.Equal((rank, rank, rank == 4 ? 8 : 6),
                (parent["consequences"]!["slotBudget"]!.GetValue<int>(), parent["consequences"]!["slotsUsed"]!.GetValue<int>(),
                    parent["recovery"]!["currentStepThreshold"]!.GetValue<int>()));
            Assert.Equal(rank, Definition(data, index)["components"]!.AsArray().Count);
            Assert.Equal(1, Definition(data, index)["components"]![0]!["payload"]!["magnitude"]!.GetValue<int>());
            Assert.Equal(rank == 4 ? 3 : 2,
                Definition(data, index)["components"]![2]!["payload"]!["magnitude"]!.GetValue<int>());
            if (rank == 4)
            {
                var fourth = Definition(data, index)["components"]![3]!;
                if (variant == "forbidden-art" && index == 1)
                    Assert.Equal(("spiritual_art_restriction", "pressure", "forbid"),
                        (fourth["profile"]!.GetValue<string>(), fourth["payload"]!["operation"]!.GetValue<string>(),
                            fourth["payload"]!["magnitude"]!.GetValue<string>()));
                else
                    Assert.Equal(("spiritual_action_cost_burden", "counter", 3),
                        (fourth["profile"]!.GetValue<string>(), fourth["payload"]!["operation"]!.GetValue<string>(),
                            fourth["payload"]!["magnitude"]!.GetValue<int>()));
            }
        }
        var independent = SpiritualPressureGroupOwnerCaseData.Create(count,
            selectedPressureCost: variant is "selected-cost" or "selected-cost-and-roll",
            otherPressureCost: variant == "other-cost",
            pressureRoll: variant is "pressure-roll" or "selected-cost-and-roll",
            ranks: new(first, second), rankFourPressureArt: variant == "forbidden-art");
        var independentBefore = Snapshot(independent, SpiritualPressureRetainedDataAdmissibility.CreateProfiles(independent));
        data.Parents[0]["display"]!["description"] = "Изменена только локальная копия.";
        data.Effects[0]["components"]![0]!["priority"] = 101;
        Assert.Equal(independentBefore, Snapshot(independent, SpiritualPressureRetainedDataAdmissibility.CreateProfiles(independent)));
    }

    /// <summary>Requires canonical admissibility of the exact producers and each affected cheap DATA variant.</summary>
    [Theory]
    [InlineData("retained-one")]
    [InlineData("retained-pair")]
    [InlineData("owner-one")]
    [InlineData("owner-pair")]
    [InlineData("owner-selected-cost")]
    [InlineData("owner-pressure-roll")]
    [InlineData("owner-other-cost")]
    [InlineData("owner-rank-four")]
    [InlineData("owner-three")]
    [InlineData("owner-selected-cost-and-roll")]
    [InlineData("owner-third-guard-turn-end")]
    [InlineData("owner-third-guard-ap-spent")]
    public void ExactFactoriesAreCanonicalBeforeSigning(string factory)
    {
        // This is the desired-success assertion for the actual producers, not a lookalike.
        // At authoring, both exact pair factories contain the observed bad description.
        var data = CreateExact(factory);
        var profiles = SpiritualPressureRetainedDataAdmissibility.CreateProfiles(data);
        var before = Snapshot(data, profiles);
        AssertAccepted(SpiritualPressureRetainedDataAdmissibility.Check(data, profiles));
        Assert.Equal(before, Snapshot(data, profiles));
    }

    /// <summary>Accepts the independently authored complete effect and preserves every supplied DATA byte.</summary>
    [Fact]
    public void IndependentCanonicalGraphIsAcceptedWithoutChangingInputs()
    {
        var data = CreateIndependentCanonicalControl();
        var profiles = SpiritualPressureRetainedDataAdmissibility.CreateProfiles(data);
        var sources = SpiritualPressureRetainedDataAdmissibility.CreateSourceRoots(data, profiles);
        var before = Snapshot(data, profiles);
        AssertAccepted(SpiritualPressureRetainedDataAdmissibility.Check(data, profiles, sources));
        SpiritualPressureRetainedDataAdmissibility.Require(data, profiles, sources);
        Assert.Equal(before, Snapshot(data, profiles));
        // Instance fields are intentionally different from source policy. Raw section equality
        // would reject valid DATA and is not a replacement for the production policy matcher.
        Assert.False(JsonNode.DeepEquals(data.Effects[0]["lifetime"], Definition(data, 0)["lifetime"]));
        Assert.False(JsonNode.DeepEquals(data.Effects[0]["stacking"], Definition(data, 0)["stacking"]));
        Assert.Equal("independent", data.Effects[0]["stacking"]!["policy"]!.GetValue<string>());
    }

    /// <summary>Retains both original description failures with no unrelated binding issue.</summary>
    [Theory]
    [InlineData("retained-pair")]
    [InlineData("owner-pair")]
    public void OriginalInvalidDescriptionIsRejectedForBothParents(string factory)
    {
        var data = CreateExact(factory);
        // Preserve the original-invalid DATA even after the coherent producer correction.
        foreach (var effect in data.Effects)
            effect["display"]!["description"] = "Духовное повреждение ограничивает действия.";
        var profiles = SpiritualPressureRetainedDataAdmissibility.CreateProfiles(data);
        var before = Snapshot(data, profiles);
        var result = SpiritualPressureRetainedDataAdmissibility.Check(data, profiles);
        Assert.Equal(RetainedPressureDataGate.CanonicalBinding, result.Gate);
        Assert.True(result.ReachedCanonicalBinding);
        Assert.Equal(2, result.Issues.Count);
        for (var index = 0; index < 2; index++)
            AssertIssue(result, "effect_materialization_source_definition_mismatch", EffectPath(index) + ".display");
        Assert.Throws<InvalidDataException>(() => SpiritualPressureRetainedDataAdmissibility.Require(data, profiles));
        Assert.Equal(before, Snapshot(data, profiles));
    }

    // Finite explicit rows, not a generated cross-product. Every row has a valid complete
    // baseline first and must pass all preceding shape/catalog/authority/index/History gates.
    /// <summary>Proves shape-valid section drift reaches the real binding fence for either parent.</summary>
    [Theory]
    [InlineData(0, "display-name", "display")]
    [InlineData(1, "display-name", "display")]
    [InlineData(0, "display-description", "display")]
    [InlineData(1, "display-description", "display")]
    [InlineData(0, "display-category", "display")]
    [InlineData(1, "display-category", "display")]
    [InlineData(0, "display-visibility", "display")]
    [InlineData(1, "display-visibility", "display")]
    [InlineData(0, "component-priority", "components")]
    [InlineData(1, "component-priority", "components")]
    [InlineData(0, "component-magnitude", "components")]
    [InlineData(1, "component-magnitude", "components")]
    [InlineData(0, "lifetime-target", "lifetime")]
    [InlineData(1, "lifetime-target", "lifetime")]
    [InlineData(0, "lifetime-link-kind", "lifetime")]
    [InlineData(1, "lifetime-link-kind", "lifetime")]
    [InlineData(0, "lifetime-predicate", "lifetime")]
    [InlineData(1, "lifetime-predicate", "lifetime")]
    [InlineData(0, "lifetime-source-loss", "lifetime")]
    [InlineData(1, "lifetime-source-loss", "lifetime")]
    [InlineData(0, "stacking-policy", "stacking")]
    [InlineData(1, "stacking-policy", "stacking")]
    [InlineData(0, "trigger-priority", "triggers")]
    [InlineData(1, "trigger-priority", "triggers")]
    [InlineData(0, "removal-source-loss", "removal")]
    [InlineData(1, "removal-source-loss", "removal")]
    [InlineData(0, "link-target", "links")]
    [InlineData(1, "link-target", "links")]
    public void DefinitionControlledMutationReachesBinding(int index, string mutation, string section)
    {
        var data = CreateIndependentCanonicalControl();
        AssertAccepted(Check(data));
        var ledgers = LedgerSnapshot(data);
        var sibling = data.Effects[1 - index].ToJsonString();
        var effect = data.Effects[index];
        switch (mutation)
        {
            case "display-name": effect["display"]!["name"] = "Чужое имя"; break;
            case "display-description": effect["display"]!["description"] = "Иное описание."; break;
            case "display-category": effect["display"]!["category"] = "condition"; break;
            case "display-visibility": effect["display"]!["visibility"] = "hidden"; break;
            case "component-priority": effect["components"]![0]!["priority"] = 101; break;
            // Cost burden 3 is shape-valid, unlike an illegal strain magnitude of 2.
            case "component-magnitude": effect["components"]![2]!["payload"]!["magnitude"] = 3; break;
            case "lifetime-target": effect["lifetime"]!["targetId"] = "wound_other_parent"; break;
            case "lifetime-link-kind": effect["lifetime"]!["linkKind"] = "skill"; break;
            case "lifetime-predicate": effect["lifetime"]!["activePredicate"] = "equipped"; break;
            case "lifetime-source-loss": effect["lifetime"]!["onSourceLoss"] = "suspend"; break;
            case "stacking-policy": effect["stacking"]!["policy"] = "stack"; break;
            case "trigger-priority": effect["triggers"]![0]!["priority"] = 101; break;
            case "removal-source-loss": effect["removal"]!["onSourceLoss"] = "suspend"; break;
            case "link-target": effect["links"]![0]!["targetId"] = "wound_other_parent"; break;
            default: throw new ArgumentOutOfRangeException(nameof(mutation));
        }
        var profiles = SpiritualPressureRetainedDataAdmissibility.CreateProfiles(data);
        var beforeCheck = Snapshot(data, profiles);
        var result = SpiritualPressureRetainedDataAdmissibility.Check(data, profiles);
        Assert.Equal(RetainedPressureDataGate.CanonicalBinding, result.Gate);
        Assert.True(result.ReachedCanonicalBinding);
        Assert.Single(result.Issues);
        AssertIssue(result, "effect_materialization_source_definition_mismatch", EffectPath(index) + "." + section);
        Assert.Equal(beforeCheck, Snapshot(data, profiles));
        Assert.Equal(ledgers, LedgerSnapshot(data));
        Assert.Equal(sibling, data.Effects[1 - index].ToJsonString());
    }

    /// <summary>Separates exact source authority failures from independent carrier/index checks.</summary>
    [Theory]
    [InlineData(0, "source-id", "effect_source_selector_unresolved")]
    [InlineData(1, "definition-key", "effect_source_selector_unresolved")]
    [InlineData(0, "source-kind", "effect_source_selector_unresolved")]
    [InlineData(1, "realm", "effect_source_realm_mismatch")]
    public void SourceKeyMutationReachesBinding(int index, string mutation, string expectedCode)
    {
        var data = CreateIndependentCanonicalControl();
        AssertAccepted(Check(data));
        var effect = data.Effects[index];
        var entry = data.EffectIdentities["entries"]![index]!;
        // Independently author both records' explicit selector values to distinguish canonical
        // authority rejection from the separate carrier/index disagreement controls below.
        switch (mutation)
        {
            case "source-id":
                effect["source"]!["sourceId"] = "wound_unregistered";
                entry["source"]!["sourceId"] = "wound_unregistered";
                entry["stackCoordinate"]!["sourceId"] = "wound_unregistered";
                break;
            case "definition-key":
                effect["source"]!["definitionKey"] = "definition_unregistered";
                entry["source"]!["definitionKey"] = "definition_unregistered";
                break;
            case "source-kind":
                effect["source"]!["kind"] = "quest";
                entry["source"]!["kind"] = "quest";
                entry["stackCoordinate"]!["sourceKind"] = "quest";
                break;
            case "realm":
                effect["realm"] = "shining_abode";
                entry["realm"] = "shining_abode";
                entry["stackCoordinate"]!["realm"] = "shining_abode";
                break;
            default: throw new ArgumentOutOfRangeException(nameof(mutation));
        }
        var profiles = SpiritualPressureRetainedDataAdmissibility.CreateProfiles(data);
        var before = Snapshot(data, profiles);
        var result = SpiritualPressureRetainedDataAdmissibility.Check(data, profiles);
        Assert.Equal(RetainedPressureDataGate.CanonicalBinding, result.Gate);
        Assert.True(result.ReachedCanonicalBinding);
        AssertIssue(result, expectedCode, EffectPath(index) + ".source");
        Assert.Equal(before, Snapshot(data, profiles));
    }

    /// <summary>Names the real prerequisite refusing malformed target, bounds, ownership or chronology DATA.</summary>
    [Theory]
    [InlineData(0, "target", "Catalog", "effect_materialization_target_carrier_mismatch", ".target")]
    [InlineData(1, "target", "Catalog", "effect_materialization_target_carrier_mismatch", ".target")]
    [InlineData(0, "parameter-bound", "Catalog", "wound_materialization_owned_source_graph_invalid", ".consequences.ownedEffectSources.definitions[0].parameterBounds")]
    [InlineData(1, "parameter-bound", "Catalog", "wound_materialization_owned_source_graph_invalid", ".consequences.ownedEffectSources.definitions[0].parameterBounds")]
    [InlineData(0, "index-owner", "EffectIndexAgreement", "effect_materialization_index_carrier_mismatch", "")]
    [InlineData(1, "index-carrier", "EffectIndexAgreement", "effect_materialization_index_carrier_mismatch", "")]
    [InlineData(0, "created-event", "EffectIndexAgreement", "effect_materialization_index_carrier_mismatch", "")]
    [InlineData(1, "last-turn", "EffectIndexAgreement", "effect_materialization_index_carrier_mismatch", "")]
    public void EarlierGateRefusesMalformedData(int index, string mutation, string gate,
        string code, string suffix)
    {
        var data = CreateIndependentCanonicalControl();
        AssertAccepted(Check(data));
        switch (mutation)
        {
            case "target": data.Effects[index]["target"]!["targetId"] = "other_soul"; break;
            // Direct wound roots prohibit scalar parameterization. This must fail the real
            // parent graph gate, never be advertised as a reached canonical-binding negative.
            case "parameter-bound":
                Definition(data, index)["parameterBounds"] = new JsonObject
                { ["magnitude"] = new JsonObject { ["kind"] = "integer", ["minimum"] = 1, ["maximum"] = 3 } };
                break;
            case "index-owner": data.EffectIdentities["entries"]![index]!["owner"]!["ownerId"] = "other_soul"; break;
            case "index-carrier": data.EffectIdentities["entries"]![index]!["owner"]!["carrierPath"] = EffectCarrierCatalog.PlayerPath; break;
            case "created-event": data.Effects[index]["chronology"]!["createdEventRef"] = "other_creation_event"; break;
            case "last-turn": data.Effects[index]["chronology"]!["lastTransitionTurn"] = 45; break;
            default: throw new ArgumentOutOfRangeException(nameof(mutation));
        }
        var profiles = SpiritualPressureRetainedDataAdmissibility.CreateProfiles(data);
        var before = Snapshot(data, profiles);
        var result = SpiritualPressureRetainedDataAdmissibility.Check(data, profiles);
        Assert.Equal(gate, result.Gate.ToString());
        Assert.False(result.ReachedCanonicalBinding);
        var path = mutation == "parameter-bound" ? ParentPath(index) : EffectPath(index);
        AssertIssue(result, code, path + suffix);
        Assert.Equal(before, Snapshot(data, profiles));
    }

    /// <summary>Rejects History output disagreement or a changed immutable receipt digest without repair.</summary>
    [Theory]
    [InlineData("history-output")]
    [InlineData("receipt-seal")]
    public void HistoryOrReceiptSealMutationIsRejected(string mutation)
    {
        var data = CreateIndependentCanonicalControl();
        AssertAccepted(Check(data));
        var hash = "sha256:" + new string('a', 64);
        if (mutation == "history-output") data.History["transitions"]![1]!["outputFingerprint"] = hash;
        else data.Receipts["decisions"]![0]!["decisionFingerprint"] = hash;
        var profiles = SpiritualPressureRetainedDataAdmissibility.CreateProfiles(data);
        var before = Snapshot(data, profiles);
        var result = SpiritualPressureRetainedDataAdmissibility.Check(data, profiles);
        if (mutation == "history-output")
        {
            Assert.Equal(RetainedPressureDataGate.RetainedJoins, result.Gate);
            Assert.True(result.ReachedCanonicalBinding);
            AssertIssue(result, "retained_pressure_data_join_mismatch", WoundHistoryState.HistoryPath + ".transitions[1]");
        }
        else
        {
            Assert.Equal(RetainedPressureDataGate.LedgerShape, result.Gate);
            Assert.False(result.ReachedCanonicalBinding);
            AssertIssue(result, "spiritual_wound_receipt_invalid_state", SpiritualWoundOpportunityReceiptState.StatePath);
        }
        Assert.Equal(before, Snapshot(data, profiles));
    }

    /// <summary>Allows optional source labels without weakening any definition-controlled display field.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void OptionalSourceLabelDoesNotChangeBinding(int index)
    {
        var data = CreateIndependentCanonicalControl();
        AssertAccepted(Check(data));
        data.Effects[index]["display"]!.AsObject().Remove("sourceLabel");
        AssertAccepted(Check(data));
        data.Effects[index]["display"]!["sourceLabel"] = "Независимая подпись источника";
        AssertAccepted(Check(data));
    }

    /// <summary>Rejects duplicate detached authored records against unchanged valid profile and ledger images.</summary>
    [Theory]
    [InlineData("parents", 0)]
    [InlineData("parents", 1)]
    [InlineData("effects", 0)]
    [InlineData("effects", 1)]
    public void DetachedAuthoredInventoryMustBijectWithActualCatalog(string family, int duplicatedIndex)
    {
        var data = CreateIndependentCanonicalControl();
        var profiles = SpiritualPressureRetainedDataAdmissibility.CreateProfiles(data);
        AssertAccepted(SpiritualPressureRetainedDataAdmissibility.Check(data, profiles));
        var profilesBefore = profiles.ToJsonString();
        var ledgersBefore = LedgerSnapshot(data);
        var detached = family == "parents"
            ? data with { Parents = new[] { data.Parents[duplicatedIndex].DeepClone().AsObject(),
                data.Parents[duplicatedIndex].DeepClone().AsObject() } }
            : data with { Effects = new[] { data.Effects[duplicatedIndex].DeepClone().AsObject(),
                data.Effects[duplicatedIndex].DeepClone().AsObject() } };
        var beforeCheck = Snapshot(detached, profiles);
        var result = SpiritualPressureRetainedDataAdmissibility.Check(detached, profiles);
        Assert.Equal(RetainedPressureDataGate.RetainedJoins, result.Gate);
        Assert.True(result.ReachedCanonicalBinding);
        AssertIssue(result, "retained_pressure_data_join_mismatch", "retainedPressure.inventory." + family);
        Assert.Single(result.Issues.Where(issue => issue.FilePath.StartsWith(
            "retainedPressure.inventory", StringComparison.Ordinal)));
        Assert.Equal(beforeCheck, Snapshot(detached, profiles));
        Assert.Equal(profilesBefore, profiles.ToJsonString());
        Assert.Equal(ledgersBefore, LedgerSnapshot(data));
    }

    /// <summary>Proves parent, effect, sibling, profile and separate-call mutable graphs are detached.</summary>
    [Theory]
    [InlineData("retained-pair")]
    [InlineData("owner-pair")]
    public void FactoriesReturnIndependentDeepGraphs(string factory)
    {
        var first = CreateExact(factory);
        var second = CreateExact(factory);
        var profiles = SpiritualPressureRetainedDataAdmissibility.CreateProfiles(first);
        var untouched = Snapshot(second, SpiritualPressureRetainedDataAdmissibility.CreateProfiles(second));
        var siblingParent = first.Parents[1].ToJsonString();
        var siblingEffect = first.Effects[1].ToJsonString();
        var definition = Definition(first, 0).ToJsonString();
        var ledgers = LedgerSnapshot(first);
        first.Effects[0]["components"]![0]!["priority"] = 101;
        first.Effects[0]["display"]!["description"] = "Локальная копия.";
        Assert.Equal(definition, Definition(first, 0).ToJsonString());
        Definition(first, 0)["components"]![0]!["priority"] = 102;
        Assert.Equal(101, first.Effects[0]["components"]![0]!["priority"]!.GetValue<int>());
        Assert.Equal(siblingParent, first.Parents[1].ToJsonString());
        Assert.Equal(siblingEffect, first.Effects[1].ToJsonString());
        Assert.Equal(ledgers, LedgerSnapshot(first));
        Assert.Equal(untouched, Snapshot(second, SpiritualPressureRetainedDataAdmissibility.CreateProfiles(second)));
        Assert.NotEqual(first.Effects[0].ToJsonString(), profiles["profiles"]![0]!["activeEffects"]![0]!.ToJsonString());
    }

    private static RetainedPressureGroupData CreateExact(string factory) => factory switch
    {
        "retained-one" => SpiritualRetainedPressureGroupTestData.Create(1),
        "retained-pair" => SpiritualRetainedPressureGroupTestData.Create(2),
        "owner-one" => SpiritualPressureGroupOwnerCaseData.Create(1),
        "owner-pair" => SpiritualPressureGroupOwnerCaseData.Create(2),
        "owner-selected-cost" => SpiritualPressureGroupOwnerCaseData.Create(2, selectedPressureCost: true),
        "owner-pressure-roll" => SpiritualPressureGroupOwnerCaseData.Create(2, pressureRoll: true),
        "owner-other-cost" => SpiritualPressureGroupOwnerCaseData.Create(2, otherPressureCost: true),
        "owner-rank-four" => SpiritualPressureGroupOwnerCaseData.Create(2, ranks: new(3, 4)),
        "owner-three" => SpiritualPressureGroupOwnerCaseData.Create(3),
        "owner-selected-cost-and-roll" => SpiritualPressureGroupOwnerCaseData.Create(2, selectedPressureCost: true, pressureRoll: true),
        "owner-third-guard-turn-end" => SpiritualPressureGroupOwnerCaseData.Create(2, thirdGuard: SpiritualPressureThirdGuardTrigger.TurnEnd),
        "owner-third-guard-ap-spent" => SpiritualPressureGroupOwnerCaseData.Create(2, thirdGuard: SpiritualPressureThirdGuardTrigger.OwnApSpent),
        _ => throw new ArgumentOutOfRangeException(nameof(factory))
    };

    /// <summary>
    /// Keeps the independently checked historical scaffold but hand-authors every complete active
    /// effect section. No active-effect builder, definition→effect clone, normalizer or repair is used.
    /// This is canonical comparison DATA, not evidence that a native birth or signed turn executed.
    /// </summary>
    private static RetainedPressureGroupData CreateIndependentCanonicalControl()
    {
        var scaffold = SpiritualPressureGroupOwnerCaseData.Create(2);
        var effects = Enumerable.Range(0, 2).Select(index =>
        {
            var label = "retained_pressure_owner_" + (index + 1);
            var eventRef = scaffold.Parents[index]["origin"]!["eventRef"]!.GetValue<string>();
            var turn = 41 + index;
            return JsonNode.Parse($$"""
                {
                  "schemaVersion": 1, "entityKind": "active_effect", "effectId": "effect_{{label}}",
                  "state": "active", "realm": "chaos_sea",
                  "target": { "kind": "player", "targetId": "player_soul" },
                  "display": { "name": "Духовная рана", "description": "Духовное повреждение мешает действовать в Посмертии.",
                    "category": "debuff", "visibility": "visible", "sourceLabel": "Независимый источник" },
                  "source": { "kind": "wound", "sourceId": "wound_{{label}}", "definitionKey": "definition_{{label}}" },
                  "components": [
                    { "componentId": "component_001", "profile": "spiritual_strain_burden", "priority": 100,
                      "payload": { "operation": "pressure", "axis": "sideStrain", "magnitude": 1 } },
                    { "componentId": "component_002", "profile": "spiritual_roll_hindrance", "priority": 100,
                      "payload": { "operation": "counter", "axis": "rollMode", "magnitude": "disadvantage" } },
                    { "componentId": "component_003", "profile": "spiritual_action_cost_burden", "priority": 100,
                      "payload": { "operation": "guard", "axis": "actionCostAudit", "magnitude": 2 } }
                  ],
                  "lifetime": { "mode": "source_bound", "linkKind": "wound", "targetId": "wound_{{label}}",
                    "activePredicate": "active", "onSourceLoss": "expire", "displayText": "До исцеления источника" },
                  "stacking": { "stackKey": "stack_definition_{{label}}", "policy": "independent", "maxStacks": 1,
                    "currentStacks": 1, "refreshMode": null, "mergeRule": null },
                  "triggers": [ { "triggerId": "on_owner_turn_end", "eventType": "owner_turn_end", "priority": 100,
                    "componentIds": [ "component_001", "component_002", "component_003" ], "consumeUses": false,
                    "resolutionMode": "deterministic" } ],
                  "removal": { "dispelCategories": [ "physical_treatment" ], "cureKinds": [ "stop_bleeding" ],
                    "onSourceLoss": "expire", "onConditionLoss": null, "manualAuthorities": [] },
                  "links": [ { "kind": "wound", "targetId": "wound_{{label}}", "role": "source" } ],
                  "chronology": { "createdAtTurn": {{turn}}, "createdEventRef": "{{eventRef}}",
                    "lastTransitionId": "effect_transition_{{label}}", "lastTransitionTurn": {{turn}} }
                }
                """)!.AsObject();
        }).ToArray();
        return scaffold with { Effects = effects };
    }

    private static JsonObject Definition(RetainedPressureGroupData data, int index) =>
        data.Parents[index]["consequences"]!["ownedEffectSources"]!["definitions"]![0]!.AsObject();

    private static RetainedPressureDataCheck Check(RetainedPressureGroupData data) =>
        SpiritualPressureRetainedDataAdmissibility.Check(data, SpiritualPressureRetainedDataAdmissibility.CreateProfiles(data));

    private static void AssertAccepted(RetainedPressureDataCheck result)
    {
        Assert.True(result.IsValid, $"{result.Gate}: " + string.Join("\n", result.Issues.Select(
            issue => $"{issue.Code} at {issue.FilePath}: {issue.Message}")));
        Assert.True(result.ReachedCanonicalBinding);
        Assert.Empty(result.Issues);
    }

    private static void AssertIssue(RetainedPressureDataCheck result, string code, string path) =>
        Assert.Contains(result.Issues, issue => issue.Code == code && issue.FilePath == path);

    private static string EffectPath(int index) => EffectCarrierCatalog.AfterlifeProfilesPath +
        ".profiles[0].activeEffects[" + index + "]";

    private static string ParentPath(int index) => WoundCarrierCatalog.AfterlifeProfilesPath +
        ".profiles[0].activeWounds[" + index + "]";

    private static string LedgerSnapshot(RetainedPressureGroupData data) => string.Join("\n",
        data.WoundIdentities.ToJsonString(), data.History.ToJsonString(), data.EffectIdentities.ToJsonString(), data.Receipts.ToJsonString());

    private static string Snapshot(RetainedPressureGroupData data, JsonObject profiles) => string.Join("\n",
        string.Join("\n", data.Parents.Select(row => row.ToJsonString())),
        string.Join("\n", data.Effects.Select(row => row.ToJsonString())), LedgerSnapshot(data), profiles.ToJsonString());
}
