using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class WoundEffectLineagePlannerTests
{
    private const string WoundId = "wound_lineage_planner";
    private const string RootEffectId = "effect_wound_lineage_root";
    private const string ChildEffectId = "effect_wound_lineage_child";
    private const string RootDefinitionKey = "definition_wound_lineage_root";
    private const string ChildDefinitionKey = "definition_wound_lineage_child";
    private const string ComplicationId = "complication_wound_lineage";

    [Fact]
    public void Plan_TraversesActiveChildFromTerminalComplicationRootOnce()
    {
        var wound = CreateLineageWound();
        var identities = CreateLineageIdentities(
            rootState: "removed",
            childState: "active");

        var result = WoundEffectLineagePlanner.Plan(
            wound,
            identities,
            new[] { RootEffectId });

        Assert.True(
            result.Success,
            string.Join(Environment.NewLine, result.Issues.Select(static issue =>
                $"{issue.Code}: expected={issue.Expected}; actual={issue.Actual}")));
        Assert.Equal(
            new[] { ChildEffectId, RootEffectId },
            result.ClosureEffectIds);
        Assert.Equal(new[] { ChildEffectId }, result.ActiveOrSuspendedEffectIds);
        Assert.Equal(
            WoundRootOwnershipDomain.ForComplication(ComplicationId),
            result.OwnershipByEffectId[RootEffectId]);
        Assert.Equal(
            result.OwnershipByEffectId[RootEffectId],
            result.OwnershipByEffectId[ChildEffectId]);
        Assert.Equal(
            WoundRootOwnershipDomain.ForComplication(ComplicationId),
            result.OwnershipByDefinitionKey[RootDefinitionKey]);
        Assert.Equal(
            result.OwnershipByDefinitionKey[RootDefinitionKey],
            result.OwnershipByDefinitionKey[ChildDefinitionKey]);
        Assert.Equal(2, result.Work.SourceGroupIdentityCount);
        Assert.Equal(2, result.Work.VisitedIdentityCount);
        Assert.True(
            result.Work.VisitedIdentityCount <=
            result.Work.SourceGroupIdentityCount);
    }

    [Fact]
    public void Plan_RejectsReactionChildWithMultipleFirstCreateParents()
    {
        var result = WoundEffectLineagePlanner.Plan(
            CreateLineageWound(),
            CreateLineageIdentities(
                rootState: "removed",
                childState: "active",
                configure: static (_, child) =>
                    child["transitions"]![0]!["sourceEffectIds"] =
                        new JsonArray(RootEffectId, "effect_foreign_parent")),
            new[] { RootEffectId });

        Assert.False(result.Success);
        Assert.Contains(result.Issues, static issue =>
            issue.Code == "accepted_mechanics_wound_lineage_create_invalid");
    }

    [Fact]
    public void Plan_RejectsForeignSourceChildOfCurrentWoundProducer()
    {
        var result = WoundEffectLineagePlanner.Plan(
            CreateLineageWound(),
            CreateLineageIdentities(
                rootState: "removed",
                childState: "active",
                configure: static (_, child) =>
                {
                    child["source"]!["sourceId"] = "wound_foreign_lineage";
                    child["stackCoordinate"]!["sourceId"] =
                        "wound_foreign_lineage";
                }),
            new[] { RootEffectId });

        Assert.False(result.Success);
        Assert.Contains(result.Issues, static issue =>
            issue.Code == "accepted_mechanics_wound_lineage_foreign_child");
    }

    [Fact]
    public void Plan_RejectsActiveChildWhoseDefinitionIsAbsentFromCurrentGraph()
    {
        var result = WoundEffectLineagePlanner.Plan(
            CreateLineageWound(),
            CreateLineageIdentities(
                rootState: "removed",
                childState: "active",
                configure: static (_, child) =>
                    child["source"]!["definitionKey"] =
                        "definition_pruned_from_current_graph"),
            new[] { RootEffectId });

        Assert.False(result.Success);
        Assert.Contains(result.Issues, static issue =>
            issue.Code == "accepted_mechanics_wound_lineage_definition_missing");
    }

    [Fact]
    public void Plan_RejectsDisconnectedActiveDirectIdentity()
    {
        var result = WoundEffectLineagePlanner.Plan(
            CreateLineageWound(),
            CreateLineageIdentities(
                rootState: "removed",
                childState: "active",
                configure: static (_, child) =>
                    child["transitions"]![0]!["sourceEffectIds"] =
                        new JsonArray()),
            new[] { RootEffectId });

        Assert.False(result.Success);
        Assert.Contains(result.Issues, static issue =>
            issue.Code == "accepted_mechanics_wound_lineage_active_unreachable");
    }

    [Fact]
    public void Plan_IgnoresPrunedTerminalHistoryAndLaterReplacementSuccession()
    {
        var result = WoundEffectLineagePlanner.Plan(
            CreateLineageWound(),
            CreateLineageIdentities(
                rootState: "replaced",
                childState: "active",
                configure: static (root, _) =>
                    root["transitions"]![1]!["resultEffectIds"] =
                        new JsonArray("effect_nonownership_successor"),
                includePrunedTerminalHistory: true),
            new[] { RootEffectId });

        Assert.True(
            result.Success,
            string.Join(Environment.NewLine, result.Issues.Select(static issue =>
                $"{issue.Code}: expected={issue.Expected}; actual={issue.Actual}")));
        Assert.Equal(new[] { ChildEffectId, RootEffectId }, result.ClosureEffectIds);
        Assert.Equal(3, result.Work.SourceGroupIdentityCount);
        Assert.Equal(2, result.Work.VisitedIdentityCount);
    }

    [Fact]
    public void Plan_RejectsDefinitionReachableFromBaseAndComplicationRootsBeforeMutation()
    {
        const string secondRootEffectId = "effect_wound_lineage_second_root";
        const string secondRootDefinitionKey = ChildDefinitionKey;
        var wound = CreateSharedDefinitionDomainWound(
            secondRootEffectId,
            secondRootDefinitionKey);
        var firstRoot = CreateEffect(RootEffectId, RootDefinitionKey);
        var secondRoot = CreateEffect(
            secondRootEffectId,
            secondRootDefinitionKey);
        var index = EffectMaterializationTestFixture.CreateIdentityIndex(
            firstRoot,
            secondRoot);
        var entries = index["entries"]!.AsArray().OfType<JsonObject>().ToArray();
        ConfigureCreate(entries[0], 0, Array.Empty<string>());
        ConfigureCreate(entries[1], 1, Array.Empty<string>());

        var result = WoundEffectLineagePlanner.Plan(
            wound,
            ParseIdentityState(index),
            new[] { RootEffectId });

        Assert.False(result.Success);
        Assert.Contains(result.Issues, static issue =>
            issue.Code == "accepted_mechanics_wound_lineage_cross_domain");
        Assert.DoesNotContain(
            ChildDefinitionKey,
            result.OwnershipByDefinitionKey.Keys);
    }

    [Fact]
    public void Plan_RejectsDisconnectedTerminalCycleWithinCurrentDefinitionGraph()
    {
        var result = WoundEffectLineagePlanner.Plan(
            CreateLineageWound(),
            CreateLineageIdentities(
                rootState: "removed",
                childState: "active",
                includeCyclicTerminalHistory: true),
            new[] { RootEffectId });

        Assert.False(result.Success);
        Assert.Contains(result.Issues, static issue =>
            issue.Code == "accepted_mechanics_wound_lineage_cycle");
    }

    [Fact]
    public void Plan_RejectsDuplicateCreateEvidenceInCurrentTerminalHistory()
    {
        var result = WoundEffectLineagePlanner.Plan(
            CreateLineageWound(),
            CreateLineageIdentities(
                rootState: "removed",
                childState: "active",
                includeDuplicateCreateTerminalHistory: true),
            new[] { RootEffectId });

        Assert.False(result.Success);
        Assert.Contains(result.Issues, static issue =>
            issue.Code == "accepted_mechanics_wound_lineage_create_invalid");
    }

    [Fact]
    public void Plan_RejectsMultipleCreateParentsInCurrentTerminalHistory()
    {
        var result = WoundEffectLineagePlanner.Plan(
            CreateLineageWound(),
            CreateLineageIdentities(
                rootState: "removed",
                childState: "active",
                includeMultipleParentTerminalHistory: true),
            new[] { RootEffectId });

        Assert.False(result.Success);
        Assert.Contains(result.Issues, static issue =>
            issue.Code == "accepted_mechanics_wound_lineage_create_invalid");
    }

    [Fact]
    public void Plan_RejectsDisconnectedTerminalHistoryWhoseDefinitionRemainsCurrent()
    {
        var result = WoundEffectLineagePlanner.Plan(
            CreateLineageWound(),
            CreateLineageIdentities(
                rootState: "removed",
                childState: "active",
                includeDisconnectedCurrentTerminalHistory: true),
            new[] { RootEffectId });

        Assert.False(result.Success);
        Assert.Contains(result.Issues, static issue =>
            issue.Code ==
            "accepted_mechanics_wound_lineage_current_unreachable");
    }

    [Fact]
    public void Plan_RejectsTerminalRootWhoseIdentityAuthorityDisagreesWithWound()
    {
        var result = WoundEffectLineagePlanner.Plan(
            CreateLineageWound(),
            CreateLineageIdentities(
                rootState: "removed",
                childState: "active",
                configure: static (root, _) =>
                    root["owner"]!["ownerId"] = "player_foreign"),
            new[] { RootEffectId });

        Assert.False(result.Success);
        Assert.Contains(result.Issues, static issue =>
            issue.Code ==
            "accepted_mechanics_wound_lineage_identity_authority_mismatch");
    }

    [Fact]
    public void TerminalPlan_SealsExactActiveDescendantOccurrenceAndIdentity()
    {
        var wound = CreateLineageWound();
        var identities = CreateLineageIdentities(
            rootState: "removed",
            childState: "active");
        var child = CreateEffect(ChildEffectId, ChildDefinitionKey);
        var carriers = CreatePlayerCarriers(child);

        var result = WoundEffectTerminalOperationPlanner.Plan(
            wound,
            carriers,
            identities,
            new[] { RootEffectId },
            "turn_44:wound_complication_healed",
            mechanicsOrdinal: 1,
            operationOrdinalOffset: 0,
            operationKey: "operation_heal_complication");

        Assert.True(
            result.Success,
            string.Join(Environment.NewLine, result.Issues.Select(static issue =>
                $"{issue.Code}: expected={issue.Expected}; actual={issue.Actual}")));
        var operation = Assert.Single(result.Operations);
        Assert.Equal(ChildEffectId, operation.EffectId);
        Assert.Equal("expire", operation.OperationKind);
        Assert.Equal(1, operation.MechanicsOrdinal);
        Assert.Equal(1, operation.OperationOrdinal);
        Assert.Equal(
            WoundEffectOperationEventRef.Create(
                "turn_44:wound_complication_healed",
                1,
                1,
                "expire"),
            operation.OperationRef);
        Assert.Equal(
            new EffectSourceKey(
                "mortal_world",
                "wound",
                WoundId,
                ChildDefinitionKey),
            operation.ExpectedSourceKey);
        Assert.Equal(
            new EffectTargetKey(
                "mortal_world",
                "player",
                "player_current"),
            operation.ExpectedTargetKey);
        Assert.Equal(
            new EffectCarrierCoordinate(
                "player",
                "player_current",
                EffectCarrierCatalog.PlayerPath,
                null),
            operation.ExpectedCarrierCoordinate);
        Assert.StartsWith("sha256:", operation.ExpectedEffectFingerprint);
        Assert.StartsWith("sha256:", operation.ExpectedIdentityFingerprint);
        Assert.Equal(2, result.LineageWork.SourceGroupIdentityCount);
        Assert.Equal(2, result.LineageWork.VisitedIdentityCount);
    }

    [Fact]
    public void TerminalPlan_RejectsCarrierWhoseSourceDisagreesWithSelectedIdentity()
    {
        var wound = CreateLineageWound();
        var identities = CreateLineageIdentities(
            rootState: "removed",
            childState: "active");
        var child = CreateEffect(ChildEffectId, ChildDefinitionKey);
        child["source"]!["definitionKey"] = "definition_forged_carrier_source";

        var result = WoundEffectTerminalOperationPlanner.Plan(
            wound,
            CreatePlayerCarriers(child),
            identities,
            new[] { RootEffectId },
            "turn_44:wound_complication_healed",
            mechanicsOrdinal: 1,
            operationOrdinalOffset: 0,
            operationKey: "operation_heal_complication");

        Assert.False(result.Success);
        Assert.Empty(result.Operations);
        Assert.Contains(result.Issues, static issue =>
            issue.Code == "accepted_mechanics_wound_terminal_occurrence_mismatch");
    }

    [Fact]
    public void TerminalPlan_RejectsForeignCarrierForUnselectedActiveLineage()
    {
        var child = CreateEffect(ChildEffectId, ChildDefinitionKey);
        child["target"]!["kind"] = "npc";
        child["target"]!["targetId"] = "npc_foreign";

        var result = WoundEffectTerminalOperationPlanner.Plan(
            CreateLineageWound(),
            CreateNpcCarriers("npc_foreign", child),
            CreateLineageIdentities(
                rootState: "removed",
                childState: "active"),
            Array.Empty<string>(),
            "turn_44:wound_complication_healed",
            mechanicsOrdinal: 1,
            operationOrdinalOffset: 0,
            operationKey: "operation_heal_complication");

        Assert.False(result.Success);
        Assert.Empty(result.Operations);
        Assert.Contains(result.Issues, static issue =>
            issue.Code ==
            "accepted_mechanics_wound_terminal_occurrence_mismatch");
    }

    [Theory]
    [InlineData("owner")]
    [InlineData("target")]
    [InlineData("stack")]
    [InlineData("created_event")]
    [InlineData("last_transition")]
    public void TerminalPlan_RejectsAnyIdentityOccurrenceAuthorityMismatch(
        string authority)
    {
        var identities = CreateLineageIdentities(
            rootState: "removed",
            childState: "active",
            configure: (_, childIdentity) =>
                TamperTerminalIdentity(childIdentity, authority));

        var result = WoundEffectTerminalOperationPlanner.Plan(
            CreateLineageWound(),
            CreatePlayerCarriers(CreateEffect(
                ChildEffectId,
                ChildDefinitionKey)),
            identities,
            new[] { RootEffectId },
            "turn_44:wound_complication_healed",
            mechanicsOrdinal: 1,
            operationOrdinalOffset: 0,
            operationKey: "operation_heal_complication");

        Assert.False(result.Success);
        Assert.Empty(result.Operations);
        var expectedCode = authority is "owner" or "target" or "stack"
            ? "accepted_mechanics_wound_lineage_identity_authority_mismatch"
            : "accepted_mechanics_wound_terminal_occurrence_mismatch";
        Assert.Contains(result.Issues, issue => issue.Code == expectedCode);
    }

    private static WoundMaterializationEnvelope CreateLineageWound(
        string eventType = "owner_damaged",
        string triggerId = "on_owner_damaged")
    {
        var wound = WoundContractTestData.CreateActiveWound(WoundId);
        wound["severity"]!["value"] = "III";
        wound["severity"]!["rank"] = 3;
        wound["severity"]!["maximumAtCreation"] = "III";
        wound["consequences"]!["slotBudget"] = 3;
        var root = WoundContractTestData.CreateApplyDefinitionRoot(
            WoundId,
            "mortal_world",
            RootDefinitionKey,
            ChildDefinitionKey);
        root["components"]![0]!["payload"]!["eventType"] = eventType;
        root["triggers"]![0]!["eventType"] = eventType;
        root["triggers"]![0]!["triggerId"] = triggerId;
        var child = WoundContractTestData.CreateOwnedEffectDefinition(
            WoundId,
            "mortal_world",
            ChildDefinitionKey,
            "wound_consequence");
        wound["complications"] = new JsonArray(new JsonObject
        {
            ["complicationId"] = ComplicationId,
            ["kind"] = "impairment",
            ["state"] = "active",
            ["displayName"] = "Нарушенная проводимость",
            ["treatmentDifficultyModifier"] = 2,
            ["ownedEffectIds"] = new JsonArray(RootEffectId),
            ["visibility"] = "known_to_player"
        });
        wound["consequences"]!["slotsUsed"] = 1;
        wound["consequences"]!["ownedEffectSources"] = new JsonObject
        {
            ["definitions"] = new JsonArray(root, child),
            ["rootBindings"] = new JsonArray(
                WoundContractTestData.CreateRootBinding(
                    RootEffectId,
                    RootDefinitionKey))
        };
        wound["consequences"]!["entries"] = new JsonArray(new JsonObject
        {
            ["slot"] = 1,
            ["profileKey"] = "event_reaction",
            ["effectId"] = RootEffectId,
            ["readableSummary"] = "Осложнение порождает связанную реакцию."
        });

        var parsed = WoundMaterializationContract.Parse(
            wound.ToJsonString(),
            "woundLineage");
        Assert.True(
            parsed.IsValid,
            string.Join(Environment.NewLine, parsed.Issues.Select(static issue =>
                $"{issue.Code}@{issue.FilePath}: expected={issue.Expected}; actual={issue.Actual}")));
        return parsed.Wound!;
    }

    private static WoundMaterializationEnvelope CreateSharedDefinitionDomainWound(
        string secondRootEffectId,
        string secondRootDefinitionKey)
    {
        var wound = WoundContractTestData.CreateActiveWound(WoundId);
        wound["severity"]!["value"] = "III";
        wound["severity"]!["rank"] = 3;
        wound["severity"]!["maximumAtCreation"] = "III";
        wound["consequences"] = new JsonObject
        {
            ["slotBudget"] = 3,
            ["slotsUsed"] = 2,
            ["ownedEffectSources"] = new JsonObject
            {
                ["definitions"] = new JsonArray(
                    WoundContractTestData.CreateApplyDefinitionRoot(
                        WoundId,
                        "mortal_world",
                        RootDefinitionKey,
                        ChildDefinitionKey),
                    WoundContractTestData.CreateOwnedEffectDefinition(
                        WoundId,
                        "mortal_world",
                        secondRootDefinitionKey,
                        "periodic_damage")),
                ["rootBindings"] = new JsonArray(
                    WoundContractTestData.CreateRootBinding(
                        RootEffectId,
                        RootDefinitionKey),
                    WoundContractTestData.CreateRootBinding(
                        secondRootEffectId,
                        secondRootDefinitionKey))
            },
            ["entries"] = new JsonArray(
                new JsonObject
                {
                    ["slot"] = 1,
                    ["profileKey"] = "event_reaction",
                    ["effectId"] = RootEffectId,
                    ["readableSummary"] = "Первый корень вызывает общее последствие."
                },
                new JsonObject
                {
                    ["slot"] = 2,
                    ["profileKey"] = "periodic_damage",
                    ["effectId"] = secondRootEffectId,
                    ["readableSummary"] = "Второй корень вызывает общее последствие."
                })
        };
        wound["consequences"]!["ownedEffectSources"]!["definitions"]![1]!
            ["stacking"]!["policy"] = "replace";
        var parsed = WoundMaterializationContract.Parse(
            wound.ToJsonString(),
            "woundSharedDomain");
        Assert.True(
            parsed.IsValid,
            string.Join(Environment.NewLine, parsed.Issues.Select(static issue =>
                $"{issue.Code}@{issue.FilePath}: expected={issue.Expected}; actual={issue.Actual}")));
        var canonical = parsed.Wound!;
        return canonical with
        {
            Complications = new[]
            {
                new WoundComplication(
                    ComplicationId,
                    "impairment",
                    "active",
                    "Нарушенная проводимость",
                    2,
                    new[] { RootEffectId },
                    "known_to_player")
            }
        };
    }

    private static EffectIdentityState CreateLineageIdentities(
        string rootState,
        string childState,
        Action<JsonObject, JsonObject>? configure = null,
        bool includePrunedTerminalHistory = false,
        bool includeCyclicTerminalHistory = false,
        bool includeDuplicateCreateTerminalHistory = false,
        bool includeMultipleParentTerminalHistory = false,
        bool includeDisconnectedCurrentTerminalHistory = false)
    {
        var root = CreateEffect(
            RootEffectId,
            RootDefinitionKey);
        var child = CreateEffect(
            ChildEffectId,
            ChildDefinitionKey);
        var effects = new List<JsonObject> { root, child };
        if (includePrunedTerminalHistory)
        {
            effects.Add(CreateEffect(
                "effect_pruned_terminal_history",
                "definition_pruned_terminal_history"));
        }
        if (includeCyclicTerminalHistory)
        {
            effects.Add(CreateEffect(
                "effect_terminal_cycle_first",
                RootDefinitionKey));
            effects.Add(CreateEffect(
                "effect_terminal_cycle_second",
                ChildDefinitionKey));
        }
        if (includeDuplicateCreateTerminalHistory)
        {
            effects.Add(CreateEffect(
                "effect_terminal_duplicate_create",
                ChildDefinitionKey));
        }
        if (includeMultipleParentTerminalHistory)
        {
            effects.Add(CreateEffect(
                "effect_terminal_multiple_parents",
                ChildDefinitionKey));
        }
        if (includeDisconnectedCurrentTerminalHistory)
        {
            effects.Add(CreateEffect(
                "effect_terminal_disconnected_current",
                ChildDefinitionKey));
        }
        var index = EffectMaterializationTestFixture.CreateIdentityIndex(
            effects.ToArray());
        var entries = index["entries"]!.AsArray().OfType<JsonObject>().ToArray();
        ConfigureCreate(entries[0], 0, Array.Empty<string>());
        ConfigureCreate(entries[1], 1, new[] { RootEffectId });
        SetState(entries[0], rootState, 0);
        SetState(entries[1], childState, 1);
        if (includePrunedTerminalHistory)
        {
            ConfigureCreate(entries[2], 2, Array.Empty<string>());
            SetState(entries[2], "removed", 2);
        }
        if (includeCyclicTerminalHistory)
        {
            var offset = includePrunedTerminalHistory ? 3 : 2;
            ConfigureCreate(
                entries[offset],
                offset,
                new[] { "effect_terminal_cycle_second" });
            ConfigureCreate(
                entries[offset + 1],
                offset + 1,
                new[] { "effect_terminal_cycle_first" });
            SetState(entries[offset], "removed", offset);
            SetState(entries[offset + 1], "removed", offset + 1);
        }
        if (includeDuplicateCreateTerminalHistory)
        {
            var offset = 2 +
                         (includePrunedTerminalHistory ? 1 : 0) +
                         (includeCyclicTerminalHistory ? 2 : 0);
            ConfigureCreate(entries[offset], offset, Array.Empty<string>());
            SetState(entries[offset], "removed", offset);
            var transitions = entries[offset]["transitions"]!.AsArray();
            transitions.Insert(1, new JsonObject
            {
                ["transitionId"] =
                    "effect_transition_lineage_duplicate_create",
                ["kind"] = "create",
                ["turn"] = 42,
                ["eventRef"] = "turn_42:lineage:duplicate_create",
                ["sourceEffectIds"] = new JsonArray(),
                ["resultEffectIds"] = new JsonArray(
                    "effect_terminal_duplicate_create"),
                ["receiptId"] = null
            });
        }
        if (includeMultipleParentTerminalHistory)
        {
            var offset = 2 +
                         (includePrunedTerminalHistory ? 1 : 0) +
                         (includeCyclicTerminalHistory ? 2 : 0) +
                         (includeDuplicateCreateTerminalHistory ? 1 : 0);
            ConfigureCreate(
                entries[offset],
                offset,
                new[]
                {
                    "effect_historical_parent_first",
                    "effect_historical_parent_second"
                });
            SetState(entries[offset], "removed", offset);
        }
        if (includeDisconnectedCurrentTerminalHistory)
        {
            var offset = 2 +
                         (includePrunedTerminalHistory ? 1 : 0) +
                         (includeCyclicTerminalHistory ? 2 : 0) +
                         (includeDuplicateCreateTerminalHistory ? 1 : 0) +
                         (includeMultipleParentTerminalHistory ? 1 : 0);
            ConfigureCreate(entries[offset], offset, Array.Empty<string>());
            SetState(entries[offset], "removed", offset);
        }
        configure?.Invoke(entries[0], entries[1]);

        return ParseIdentityState(index);
    }

    private static EffectIdentityState ParseIdentityState(JsonObject index)
    {
        using var document = JsonDocument.Parse(index.ToJsonString());
        var parsed = EffectIdentityState.Parse(
            document.RootElement,
            EffectIdentityState.StatePath);
        Assert.Empty(parsed.Issues);
        return Assert.IsType<EffectIdentityState>(parsed.State);
    }

    private static JsonObject CreateEffect(
        string effectId,
        string definitionKey)
    {
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect();
        effect["effectId"] = effectId;
        effect["source"] = new JsonObject
        {
            ["kind"] = "wound",
            ["sourceId"] = WoundId,
            ["definitionKey"] = definitionKey
        };
        effect["stacking"]!["stackKey"] = "stack_" + definitionKey;
        var ordinal = string.Equals(
            definitionKey,
            RootDefinitionKey,
            StringComparison.Ordinal)
            ? 0
            : 1;
        effect["chronology"] = new JsonObject
        {
            ["createdAtTurn"] = 42,
            ["createdEventRef"] = $"turn_42:lineage:create:{ordinal}",
            ["lastTransitionId"] =
                $"effect_transition_lineage_create_{ordinal}",
            ["lastTransitionTurn"] = 42
        };
        return effect;
    }

    private static void TamperTerminalIdentity(
        JsonObject identity,
        string authority)
    {
        switch (authority)
        {
            case "owner":
                identity["owner"]!["ownerId"] = "player_forged";
                break;
            case "target":
                identity["target"]!["targetId"] = "player_forged";
                break;
            case "stack":
                identity["stackCoordinate"]!["stackKey"] = "stack_forged";
                break;
            case "created_event":
                identity["transitions"]![0]!["eventRef"] =
                    "turn_42:forged_create";
                break;
            case "last_transition":
                identity["transitions"]![0]!["transitionId"] =
                    "effect_transition_forged_create";
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(authority),
                    authority,
                    null);
        }
    }

    private static EffectCarrierCatalogInput CreatePlayerCarriers(
        params JsonObject[] effects) => new(
        new JsonObject
        {
            ["schemaVersion"] = 1,
            ["activeEffects"] = new JsonArray(
                effects.Select(static effect => (JsonNode)effect.DeepClone())
                    .ToArray())
        },
        null,
        null,
        null,
        null,
        null);

    private static EffectCarrierCatalogInput CreateNpcCarriers(
        string npcId,
        params JsonObject[] effects) => new(
        null,
        new JsonObject
        {
            ["schemaVersion"] = 1,
            ["entries"] = new JsonArray(new JsonObject
            {
                ["NPCId"] = npcId,
                ["activeEffects"] = new JsonArray(
                    effects.Select(static effect => (JsonNode)effect.DeepClone())
                        .ToArray())
            })
        },
        null,
        null,
        null,
        null);

    private static void ConfigureCreate(
        JsonObject entry,
        int ordinal,
        IReadOnlyList<string> parents)
    {
        var create = entry["transitions"]![0]!.AsObject();
        create["transitionId"] = $"effect_transition_lineage_create_{ordinal}";
        create["eventRef"] = $"turn_42:lineage:create:{ordinal}";
        create["sourceEffectIds"] = new JsonArray(
            parents.Select(static parent => (JsonNode)parent).ToArray());
    }

    private static void SetState(
        JsonObject entry,
        string state,
        int ordinal)
    {
        if (string.Equals(state, "active", StringComparison.Ordinal))
            return;
        entry["state"] = state;
        var kind = state switch
        {
            "suspended" => "suspend",
            "expired" => "expire",
            "dispelled" => "dispel",
            "removed" => "remove",
            "replaced" => "replace",
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, null)
        };
        var effectId = entry["effectId"]!.GetValue<string>();
        entry["transitions"]!.AsArray().Add(new JsonObject
        {
            ["transitionId"] = $"effect_transition_lineage_{kind}_{ordinal}",
            ["kind"] = kind,
            ["turn"] = 43,
            ["eventRef"] = $"turn_43:lineage:{kind}:{ordinal}",
            ["sourceEffectIds"] = new JsonArray(effectId),
            ["resultEffectIds"] = string.Equals(
                    state,
                    "suspended",
                    StringComparison.Ordinal)
                ? new JsonArray(effectId)
                : new JsonArray(),
            ["receiptId"] = null
        });
    }
}
