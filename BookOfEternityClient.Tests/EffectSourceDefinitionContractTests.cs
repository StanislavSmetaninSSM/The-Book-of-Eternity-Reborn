using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class EffectSourceDefinitionContractTests
{
    [Fact]
    public void ReactionResultCatalog_SealsEveryExecutionAndDependencyPolicy()
    {
        var actual = EffectReactionResultCatalog.All
            .OrderBy(static descriptor => descriptor.Kind, StringComparer.Ordinal)
            .Select(static descriptor => (
                descriptor.Kind,
                descriptor.ResolutionMode,
                descriptor.Behavior,
                Dependencies: string.Join(
                    "|",
                    descriptor.AllowedDependencies.OrderBy(
                        static value => value,
                        StringComparer.Ordinal))))
            .ToArray();

        Assert.Equal(
            new[]
            {
                ("apply_definition", "deterministic", EffectReactionResultBehavior.ApplyDefinition,
                    "after_component|after_current_event|before_current_event"),
                ("bounded_receipt", "bounded_receipt", EffectReactionResultBehavior.PeriodicComponent,
                    "after_component"),
                ("event_outcome", "deterministic", EffectReactionResultBehavior.EventOutcome,
                    "after_component|after_current_event|before_current_event"),
                ("remove", "deterministic", EffectReactionResultBehavior.Remove,
                    "after_component|after_current_event|before_current_event"),
                ("suspend", "deterministic", EffectReactionResultBehavior.Suspend,
                    "after_component|after_current_event|before_current_event"),
                ("trigger_component", "deterministic", EffectReactionResultBehavior.PeriodicComponent,
                    "after_component")
            },
            actual);
    }

    [Fact]
    public void ValidateArray_CompleteDefinition_ReturnsNoIssues()
    {
        using var document = Parse(new JsonArray(
            EffectMaterializationTestFixture.CreateDefinition()));

        Assert.Empty(EffectSourceDefinitionContract.ValidateArray(
            document.RootElement,
            "source.activeEffectDefinitions",
            "mortal_world"));
    }

    [Theory]
    [InlineData("resource_damaged")]
    [InlineData("resource_restored")]
    [InlineData("resource_spent")]
    [InlineData("resource_gained")]
    [InlineData("resource_depleted")]
    [InlineData("resource_filled")]
    public void ValidateArray_AcceptsClosedCommonResourceTriggerEvents(string eventType)
    {
        var definition = EffectMaterializationTestFixture.CreateDefinition();
        definition["triggers"]![0]!["eventType"] = eventType;
        using var document = Parse(new JsonArray(definition));

        Assert.Empty(EffectSourceDefinitionContract.ValidateArray(
            document.RootElement,
            "source.activeEffectDefinitions",
            "mortal_world"));
    }

    [Theory]
    [InlineData("resource_damaged")]
    [InlineData("resource_restored")]
    [InlineData("resource_spent")]
    [InlineData("resource_gained")]
    [InlineData("resource_depleted")]
    [InlineData("resource_filled")]
    public void ValidateArray_EventReactionAcceptsClosedCommonResourceEvents(
        string eventType)
    {
        var definition = EffectMaterializationTestFixture.CreateDefinition(
            profile: "event_reaction");
        definition["components"]![0]!["payload"]!["eventType"] = eventType;
        definition["triggers"]![0]!["eventType"] = eventType;
        using var document = Parse(new JsonArray(definition));

        Assert.Empty(EffectSourceDefinitionContract.ValidateArray(
            document.RootElement,
            "source.activeEffectDefinitions",
            "mortal_world"));
    }

    [Fact]
    public void ValidateArray_IndependentPolicyAllowsBoundedSimultaneousInstances()
    {
        var definition = EffectMaterializationTestFixture.CreateDefinition();
        definition["stacking"] = new JsonObject
        {
            ["stackKey"] = "bleeding",
            ["policy"] = "independent",
            ["maxStacks"] = 2,
            ["atMaximum"] = "no_change",
            ["refreshMode"] = null,
            ["mergeRule"] = null
        };
        using var document = Parse(new JsonArray(definition));

        Assert.Empty(EffectSourceDefinitionContract.ValidateArray(
            document.RootElement,
            "source.activeEffectDefinitions",
            "mortal_world"));
    }

    [Theory]
    [InlineData("world_time.currentTimeInMinutes", true)]
    [InlineData("gm_clock", false)]
    public void ValidateArray_UntilTimeUsesRegisteredCanonicalAuthority(
        string timeAuthority,
        bool expectedValid)
    {
        var definition = EffectMaterializationTestFixture.CreateDefinition();
        definition["lifetime"] = new JsonObject
        {
            ["mode"] = "until_time",
            ["duration"] = 30,
            ["timeAuthority"] = timeAuthority
        };
        using var document = Parse(new JsonArray(definition));

        var issues = EffectSourceDefinitionContract.ValidateArray(
            document.RootElement,
            "source.activeEffectDefinitions",
            "mortal_world");

        Assert.Equal(expectedValid, issues.Count == 0);
        if (!expectedValid)
        {
            Assert.Contains(issues, issue =>
                issue.Code == "effect_source_definition_invalid_field" &&
                issue.FilePath.EndsWith(".lifetime.timeAuthority", StringComparison.Ordinal));
        }
    }

    [Theory]
    [InlineData("periodic_damage", "sum", true)]
    [InlineData("roll_modifier", "sum", false)]
    [InlineData("event_reaction", "profile_specific", true)]
    [InlineData("event_reaction", "sum", false)]
    public void ValidateArray_MergeReducerMustBeRegisteredForEveryComponentProfile(
        string profile,
        string mergeRule,
        bool expectedValid)
    {
        var definition = EffectMaterializationTestFixture.CreateDefinition(profile);
        definition["stacking"] = new JsonObject
        {
            ["stackKey"] = "effect_merge",
            ["policy"] = "merge",
            ["maxStacks"] = 3,
            ["atMaximum"] = "no_change",
            ["refreshMode"] = null,
            ["mergeRule"] = mergeRule
        };
        using var document = Parse(new JsonArray(definition));

        var issues = EffectSourceDefinitionContract.ValidateArray(
            document.RootElement,
            "source.activeEffectDefinitions",
            "mortal_world");

        Assert.Equal(expectedValid, issues.Count == 0);
        if (!expectedValid)
        {
            Assert.Contains(issues, issue =>
                issue.Code == "effect_source_definition_invalid_merge_rule");
        }
    }

    [Theory]
    [InlineData("characteristic_modifier")]
    [InlineData("roll_modifier")]
    [InlineData("resistance_modifier")]
    [InlineData("periodic_damage")]
    [InlineData("periodic_restore")]
    [InlineData("action_control")]
    [InlineData("event_reaction")]
    [InlineData("wound_consequence")]
    [InlineData("afterlife_combat_condition")]
    public void ValidateArray_EachRegisteredProfileProducesCompleteDefinition(string profile)
    {
        using var document = Parse(new JsonArray(
            EffectMaterializationTestFixture.CreateDefinition(profile)));
        var realm = string.Equals(
            profile,
            "afterlife_combat_condition",
            StringComparison.Ordinal)
            ? "chaos_sea"
            : "mortal_world";

        Assert.Empty(EffectSourceDefinitionContract.ValidateArray(
            document.RootElement,
            "source.activeEffectDefinitions",
            realm));
    }

    [Theory]
    [InlineData("effectId")]
    [InlineData("currentStacks")]
    [InlineData("chronology")]
    [InlineData("materializationReceipt")]
    [InlineData("transitionId")]
    public void ValidateArray_ClientOwnedRuntimeField_IsForbidden(string field)
    {
        var definition = EffectMaterializationTestFixture.CreateDefinition();
        definition[field] = "forged";
        using var document = Parse(new JsonArray(definition));

        Assert.Contains(
            EffectSourceDefinitionContract.ValidateArray(
                document.RootElement,
                "source.activeEffectDefinitions",
                "mortal_world"),
            issue => issue.Code == "effect_source_definition_client_field_forbidden" &&
                     issue.FilePath.EndsWith('.' + field, StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateArray_UnknownDefinitionField_IsRejected()
    {
        var definition = EffectMaterializationTestFixture.CreateDefinition();
        definition["futurePolicy"] = new JsonObject();
        using var document = Parse(new JsonArray(definition));

        Assert.Contains(
            EffectSourceDefinitionContract.ValidateArray(
                document.RootElement,
                "source.activeEffectDefinitions",
                "mortal_world"),
            issue => issue.Code == "effect_source_definition_unknown_field");
    }

    [Theory]
    [InlineData("bleeding_consequence", "bleeding_consequence", "effect_source_definition_duplicate_key")]
    [InlineData("bleeding_consequence", "BLEEDING_CONSEQUENCE", "effect_source_definition_confusable_key")]
    [InlineData("bleeding_consequence", "bleedіng_consequence", "effect_source_definition_confusable_key")]
    public void ValidateArray_DuplicateOrConfusableDefinitionKey_IsRejected(
        string firstKey,
        string secondKey,
        string expectedCode)
    {
        var first = EffectMaterializationTestFixture.CreateDefinition();
        first["definitionKey"] = firstKey;
        var second = EffectMaterializationTestFixture.CreateDefinition();
        second["definitionKey"] = secondKey;
        using var document = Parse(new JsonArray(first, second));

        Assert.Contains(
            EffectSourceDefinitionContract.ValidateArray(
                document.RootElement,
                "source.activeEffectDefinitions",
                "mortal_world"),
            issue => issue.Code == expectedCode);
    }

    [Theory]
    [InlineData(" definition_key ")]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateArray_DefinitionKeyMustBeExactTrimmedNonEmpty(string key)
    {
        var definition = EffectMaterializationTestFixture.CreateDefinition();
        definition["definitionKey"] = key;
        using var document = Parse(new JsonArray(definition));

        Assert.Contains(
            EffectSourceDefinitionContract.ValidateArray(
                document.RootElement,
                "source.activeEffectDefinitions",
                "mortal_world"),
            issue => issue.Code == "effect_source_definition_invalid_field");
    }

    [Fact]
    public void ValidateArray_WrongRealmAndTargetKindAreRejected()
    {
        var definition = EffectMaterializationTestFixture.CreateDefinition();
        definition["allowedRealms"] = new JsonArray("Mortal");
        definition["allowedTargetKinds"] = new JsonArray("PlayerByName");
        using var document = Parse(new JsonArray(definition));

        var issues = EffectSourceDefinitionContract.ValidateArray(
            document.RootElement,
            "source.activeEffectDefinitions",
            "mortal_world");

        Assert.Contains(issues, issue => issue.FilePath.Contains("allowedRealms", StringComparison.Ordinal));
        Assert.Contains(issues, issue => issue.FilePath.Contains("allowedTargetKinds", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("components")]
    [InlineData("stacking")]
    [InlineData("lifetime")]
    [InlineData("triggers")]
    [InlineData("removal")]
    [InlineData("links")]
    public void ValidateArray_MissingCompletePolicySection_IsRejected(string field)
    {
        var definition = EffectMaterializationTestFixture.CreateDefinition();
        definition.Remove(field);
        using var document = Parse(new JsonArray(definition));

        Assert.Contains(
            EffectSourceDefinitionContract.ValidateArray(
                document.RootElement,
                "source.activeEffectDefinitions",
                "mortal_world"),
            issue => issue.Code == "effect_source_definition_missing_field" &&
                     issue.FilePath.EndsWith('.' + field, StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateArray_EmptyComponentsAndOutOfBoundParameterAreRejected()
    {
        var definition = EffectMaterializationTestFixture.CreateDefinition();
        definition["components"] = new JsonArray();
        definition["parameterBounds"]!["amount"]!["minimum"] = 20;
        definition["parameterBounds"]!["amount"]!["maximum"] = 10;
        using var document = Parse(new JsonArray(definition));

        var issues = EffectSourceDefinitionContract.ValidateArray(
            document.RootElement,
            "source.activeEffectDefinitions",
            "mortal_world");

        Assert.Contains(issues, issue => issue.Code == "effect_source_definition_invalid_components");
        Assert.Contains(issues, issue => issue.Code == "effect_source_definition_invalid_parameter_bound");
    }

    [Fact]
    public void ValidateArray_SourceBoundLifetimeRejectsUnregisteredActivePredicate()
    {
        var definition = EffectMaterializationTestFixture.CreateDefinition();
        definition["lifetime"] = new JsonObject
        {
            ["mode"] = "source_bound",
            ["activePredicate"] = "gm_invented_predicate",
            ["onSourceLoss"] = "expire"
        };
        using var document = Parse(new JsonArray(definition));

        Assert.Contains(
            EffectSourceDefinitionContract.ValidateArray(
                document.RootElement,
                "source.activeEffectDefinitions",
                "mortal_world"),
            issue => issue.Code == "effect_source_definition_invalid_active_predicate");
    }

    [Fact]
    public void ValidateArray_TriggerComponentIdsMustResolveWithinDefinition()
    {
        var definition = EffectMaterializationTestFixture.CreateDefinition();
        definition["triggers"]![0]!["componentIds"] = new JsonArray(
            "component_missing",
            "component_missing");
        using var document = Parse(new JsonArray(definition));

        var issues = EffectSourceDefinitionContract.ValidateArray(
            document.RootElement,
            "source.activeEffectDefinitions",
            "mortal_world");

        Assert.Contains(issues, issue =>
            issue.Code == "effect_source_definition_invalid_trigger_component");
    }

    [Fact]
    public void ValidateArray_UsesLifetimeRequiresExactConsumingTriggerSet()
    {
        var definition = EffectMaterializationTestFixture.CreateDefinition();
        definition["lifetime"] = new JsonObject
        {
            ["mode"] = "uses",
            ["initialUses"] = 2,
            ["consumingEventTypes"] = new JsonArray("owner_turn_end")
        };
        definition["triggers"]![0]!["consumeUses"] = false;
        using var document = Parse(new JsonArray(definition));

        var issues = EffectSourceDefinitionContract.ValidateArray(
            document.RootElement,
            "source.activeEffectDefinitions",
            "mortal_world");

        Assert.Contains(issues, issue =>
            issue.Code == "effect_source_definition_consuming_trigger_mismatch");
    }

    [Fact]
    public void ValidateArray_UsesLifetimeRequiresTriggerForEveryConsumingEventType()
    {
        var definition = EffectMaterializationTestFixture.CreateDefinition();
        definition["lifetime"] = new JsonObject
        {
            ["mode"] = "uses",
            ["initialUses"] = 2,
            ["consumingEventTypes"] = new JsonArray("owner_damaged")
        };
        definition["triggers"]![0]!["consumeUses"] = false;
        using var document = Parse(new JsonArray(definition));

        var issues = EffectSourceDefinitionContract.ValidateArray(
            document.RootElement,
            "source.activeEffectDefinitions",
            "mortal_world");

        Assert.Contains(issues, issue =>
            issue.Code == "effect_source_definition_consuming_trigger_mismatch");
    }

    [Fact]
    public void ValidateArray_NonUsesLifetimeRejectsConsumingTrigger()
    {
        var definition = EffectMaterializationTestFixture.CreateDefinition();
        definition["triggers"]![0]!["consumeUses"] = true;
        using var document = Parse(new JsonArray(definition));

        var issues = EffectSourceDefinitionContract.ValidateArray(
            document.RootElement,
            "source.activeEffectDefinitions",
            "mortal_world");

        Assert.Contains(issues, issue =>
            issue.Code == "effect_source_definition_consuming_trigger_mismatch");
    }

    [Fact]
    public void ValidateArray_DuplicateRawProperty_IsRejectedBeforeNodeConversion()
    {
        var json = new JsonArray(EffectMaterializationTestFixture.CreateDefinition()).ToJsonString();
        json = json.Replace(
            "\"definitionKey\":\"bleeding_consequence\"",
            "\"definitionKey\":\"bleeding_consequence\",\"definitionKey\":\"bleeding_consequence\"",
            StringComparison.Ordinal);
        using var document = JsonDocument.Parse(json);

        Assert.Contains(
            EffectSourceDefinitionContract.ValidateArray(
                document.RootElement,
                "source.activeEffectDefinitions",
                "mortal_world"),
            issue => issue.Code == "effect_source_definition_duplicate_property");
    }

    [Fact]
    public void ValidateArray_EventReactionApplyDefinitionResolvesBoundedSameSourceGraph()
    {
        var root = CreateReactionDefinition(
            "reaction_root",
            "apply_definition",
            targetDefinitionKey: "reaction_child",
            maxExpansion: 2);
        root["components"]![0]!["payload"]!["parameters"] = new JsonObject
        {
            ["amount"] = 3
        };
        var child = EffectMaterializationTestFixture.CreateDefinition();
        child["definitionKey"] = "reaction_child";
        child["stacking"]!["stackKey"] = "reaction_child_stack";
        using var document = Parse(new JsonArray(root, child));

        Assert.Empty(EffectSourceDefinitionContract.ValidateArray(
            document.RootElement,
            "source.activeEffectDefinitions",
            "mortal_world"));
    }

    [Theory]
    [InlineData("remove", "deterministic")]
    [InlineData("suspend", "deterministic")]
    [InlineData("event_outcome", "deterministic")]
    [InlineData("trigger_component", "deterministic")]
    [InlineData("bounded_receipt", "bounded_receipt")]
    public void ValidateArray_EventReactionBindsResultKindToExecutionMode(
        string resultKind,
        string resolutionMode)
    {
        var definition = CreateReactionDefinition(
            "reaction_root",
            resultKind,
            componentId: resultKind is "trigger_component" or "bounded_receipt"
                ? "reaction_periodic"
                : null,
            maxExpansion: 1);
        if (resultKind is "trigger_component" or "bounded_receipt")
        {
            var periodic = EffectMaterializationTestFixture.CreateDefinition()["components"]![0]!
                .DeepClone().AsObject();
            periodic["componentId"] = "reaction_periodic";
            definition["components"]!.AsArray().Add(periodic);
            var predecessor = periodic.DeepClone().AsObject();
            predecessor["componentId"] = "reaction_predecessor";
            definition["components"]!.AsArray().Add(predecessor);
            definition["components"]![0]!["payload"]!["dependency"] =
                "after_component";
            definition["components"]![0]!["payload"]!["afterComponentId"] =
                "reaction_predecessor";
            definition["triggers"]![0]!["componentIds"] = new JsonArray(
                "reaction_predecessor",
                "component_001");
        }
        definition["triggers"]![0]!["resolutionMode"] = resolutionMode;
        using var document = Parse(new JsonArray(definition));

        Assert.Empty(EffectSourceDefinitionContract.ValidateArray(
            document.RootElement,
            "source.activeEffectDefinitions",
            "mortal_world"));
    }

    [Theory]
    [InlineData("trigger_component")]
    [InlineData("bounded_receipt")]
    public void ValidateArray_ComponentReactionRequiresExplicitPredecessor(
        string resultKind)
    {
        var definition = CreateReactionDefinition(
            "reaction_root",
            resultKind,
            componentId: "reaction_periodic");
        var periodic = EffectMaterializationTestFixture.CreateDefinition()["components"]![0]!
            .DeepClone().AsObject();
        periodic["componentId"] = "reaction_periodic";
        definition["components"]!.AsArray().Add(periodic);
        definition["triggers"]![0]!["resolutionMode"] =
            resultKind == "bounded_receipt" ? "bounded_receipt" : "deterministic";
        using var document = Parse(new JsonArray(definition));

        var issues = EffectSourceDefinitionContract.ValidateArray(
            document.RootElement,
            "source.activeEffectDefinitions",
            "mortal_world");

        Assert.Contains(issues, issue =>
            issue.Code == "effect_source_definition_reaction_component_dependency_invalid");
    }

    [Fact]
    public void ValidateArray_EventOutcomeRequiresRegisteredSourceOwnedTransition()
    {
        var definition = CreateReactionDefinition(
            "reaction_root",
            "event_outcome");
        definition["components"]![0]!["payload"]!["resolvedOutcome"] =
            "critical_success";
        using var document = Parse(new JsonArray(definition));

        var issues = EffectSourceDefinitionContract.ValidateArray(
            document.RootElement,
            "source.activeEffectDefinitions",
            "mortal_world");

        Assert.Contains(issues, issue =>
            issue.Code == "effect_source_definition_invalid_components");
    }

    [Fact]
    public void ValidateArray_EventReactionRejectsUnresolvedReferenceWrongModeAndTooSmallExpansion()
    {
        var definition = CreateReactionDefinition(
            "reaction_root",
            "apply_definition",
            targetDefinitionKey: "missing_child",
            maxExpansion: 1);
        definition["triggers"]![0]!["resolutionMode"] = "bounded_receipt";
        using var document = Parse(new JsonArray(definition));

        var issues = EffectSourceDefinitionContract.ValidateArray(
            document.RootElement,
            "source.activeEffectDefinitions",
            "mortal_world");

        Assert.Contains(issues, issue =>
            issue.Code == "effect_source_definition_reaction_reference_unresolved");
        Assert.Contains(issues, issue =>
            issue.Code == "effect_source_definition_reaction_resolution_mismatch");
        Assert.Contains(issues, issue =>
            issue.Code == "effect_source_definition_reaction_expansion_invalid");
    }

    [Fact]
    public void ValidateArray_AfterComponentRequiresPredecessorInTheSameOwningTrigger()
    {
        var definition = CreateReactionDefinition(
            "reaction_root",
            "remove");
        var predecessor = EffectMaterializationTestFixture
            .CreateDefinition("periodic_damage")["components"]![0]!
            .DeepClone();
        predecessor!["componentId"] = "reaction_predecessor";
        definition["components"]!.AsArray().Add(predecessor);
        var payload = definition["components"]![0]!["payload"]!.AsObject();
        payload["dependency"] = "after_component";
        payload["afterComponentId"] = "reaction_predecessor";
        using var document = Parse(new JsonArray(definition));

        var issues = EffectSourceDefinitionContract.ValidateArray(
            document.RootElement,
            "source.activeEffectDefinitions",
            "mortal_world");

        Assert.Contains(issues, issue =>
            issue.Code == "effect_source_definition_reaction_predecessor_unselected");
    }

    [Fact]
    public void ValidateArray_EventReactionRejectsAmbiguousDirectAndReactionComponentDispatch()
    {
        var definition = CreateReactionDefinition(
            "reaction_root",
            "trigger_component",
            componentId: "reaction_periodic");
        var periodic = EffectMaterializationTestFixture
            .CreateDefinition("periodic_damage")["components"]![0]!
            .DeepClone();
        periodic!["componentId"] = "reaction_periodic";
        definition["components"]!.AsArray().Add(periodic);
        definition["triggers"]![0]!["componentIds"] = new JsonArray(
            "component_001",
            "reaction_periodic");
        using var document = Parse(new JsonArray(definition));

        var issues = EffectSourceDefinitionContract.ValidateArray(
            document.RootElement,
            "source.activeEffectDefinitions",
            "mortal_world");

        Assert.Contains(issues, issue =>
            issue.Code == "effect_source_definition_reaction_dispatch_ambiguous");
    }

    [Fact]
    public void ValidateArray_EventReactionRejectsAfterComponentCycleAcrossOwningTriggers()
    {
        var definition = CreateReactionDefinition(
            "reaction_root",
            "trigger_component",
            componentId: "periodic_a");
        var reactionA = definition["components"]![0]!.AsObject();
        reactionA["componentId"] = "reaction_a";
        reactionA["payload"]!["dependency"] = "after_component";
        reactionA["payload"]!["afterComponentId"] = "periodic_b";
        var reactionB = reactionA.DeepClone().AsObject();
        reactionB["componentId"] = "reaction_b";
        reactionB["payload"]!["componentId"] = "periodic_b";
        reactionB["payload"]!["afterComponentId"] = "periodic_a";
        var periodicA = EffectMaterializationTestFixture
            .CreateDefinition("periodic_damage")["components"]![0]!
            .DeepClone().AsObject();
        periodicA["componentId"] = "periodic_a";
        var periodicB = periodicA.DeepClone().AsObject();
        periodicB["componentId"] = "periodic_b";
        definition["components"] = new JsonArray(
            reactionA.DeepClone(),
            reactionB.DeepClone(),
            periodicA.DeepClone(),
            periodicB.DeepClone());
        definition["triggers"] = new JsonArray(
            new JsonObject
            {
                ["triggerId"] = "trigger_a",
                ["eventType"] = "owner_turn_end",
                ["priority"] = 100,
                ["componentIds"] = new JsonArray("periodic_b", "reaction_a"),
                ["consumeUses"] = false,
                ["resolutionMode"] = "deterministic"
            },
            new JsonObject
            {
                ["triggerId"] = "trigger_b",
                ["eventType"] = "owner_turn_end",
                ["priority"] = 90,
                ["componentIds"] = new JsonArray("periodic_a", "reaction_b"),
                ["consumeUses"] = false,
                ["resolutionMode"] = "deterministic"
            });
        using var document = Parse(new JsonArray(definition));

        var issues = EffectSourceDefinitionContract.ValidateArray(
            document.RootElement,
            "source.activeEffectDefinitions",
            "mortal_world");

        Assert.Contains(issues, issue =>
            issue.Code == "effect_source_definition_reaction_component_cycle");
    }

    [Fact]
    public void ValidateArray_EventReactionRejectsIndirectDefinitionCycle()
    {
        var first = CreateReactionDefinition(
            "reaction_a",
            "apply_definition",
            targetDefinitionKey: "reaction_b",
            maxExpansion: 4);
        var second = CreateReactionDefinition(
            "reaction_b",
            "apply_definition",
            targetDefinitionKey: "reaction_a",
            maxExpansion: 4);
        using var document = Parse(new JsonArray(first, second));

        Assert.Contains(
            EffectSourceDefinitionContract.ValidateArray(
                document.RootElement,
                "source.activeEffectDefinitions",
                "mortal_world"),
            issue => issue.Code == "effect_source_definition_reaction_cycle");
    }

    [Fact]
    public void ValidateArray_EventReactionRejectsDownstreamDefinitionOnSameStackCoordinate()
    {
        var root = CreateReactionDefinition(
            "reaction_root",
            "apply_definition",
            targetDefinitionKey: "reaction_child",
            maxExpansion: 2);
        var child = EffectMaterializationTestFixture.CreateDefinition();
        child["definitionKey"] = "reaction_child";
        child["stacking"]!["stackKey"] = root["stacking"]!["stackKey"]!.GetValue<string>();
        using var document = Parse(new JsonArray(root, child));

        Assert.Contains(
            EffectSourceDefinitionContract.ValidateArray(
                document.RootElement,
                "source.activeEffectDefinitions",
                "mortal_world"),
            issue => issue.Code == "effect_source_definition_reaction_stack_conflict");
    }

    [Fact]
    public void ValidateArray_EventReactionAllowsSameStackDownstreamWithExplicitReplacement()
    {
        var root = CreateReactionDefinition(
            "reaction_root",
            "apply_definition",
            targetDefinitionKey: "reaction_child",
            maxExpansion: 2);
        var child = EffectMaterializationTestFixture.CreateDefinition();
        child["definitionKey"] = "reaction_child";
        child["stacking"]!["stackKey"] = root["stacking"]!["stackKey"]!.GetValue<string>();
        child["stacking"]!["policy"] = "replace";
        child["stacking"]!["maxStacks"] = 1;
        child["stacking"]!["atMaximum"] = "no_change";
        using var document = Parse(new JsonArray(root, child));

        var issues = EffectSourceDefinitionContract.ValidateArray(
            document.RootElement,
            "source.activeEffectDefinitions",
            "mortal_world");

        Assert.DoesNotContain(issues, issue =>
            issue.Code == "effect_source_definition_reaction_stack_conflict");
        Assert.Empty(issues);
    }

    private static JsonObject CreateReactionDefinition(
        string rootDefinitionKey,
        string resultKind,
        string? targetDefinitionKey = null,
        string? componentId = null,
        int maxExpansion = 1)
    {
        var definition = EffectMaterializationTestFixture.CreateDefinition("event_reaction");
        definition["definitionKey"] = rootDefinitionKey;
        var payload = definition["components"]![0]!["payload"]!.AsObject();
        payload["resultKind"] = resultKind;
        payload["dependency"] = "before_current_event";
        payload["maxExpansion"] = maxExpansion;
        payload.Remove("definitionKey");
        if (targetDefinitionKey != null)
            payload["definitionKey"] = targetDefinitionKey;
        if (componentId != null)
            payload["componentId"] = componentId;
        if (resultKind == "apply_definition")
            payload["parameters"] = new JsonObject();
        if (resultKind == "event_outcome")
        {
            payload["eventType"] = "owner_critical_failure";
            definition["triggers"]![0]!["eventType"] =
                "owner_critical_failure";
            payload["originalOutcome"] = "critical_failure";
            payload["resolvedOutcome"] = "failure";
        }
        return definition;
    }

    private static JsonDocument Parse(JsonNode node) =>
        JsonDocument.Parse(node.ToJsonString());
}
