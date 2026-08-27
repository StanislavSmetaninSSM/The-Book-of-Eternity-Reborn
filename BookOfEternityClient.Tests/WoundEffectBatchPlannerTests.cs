using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// T011 RED contract for the local wound batch boundary only. T014 owns source-lineage
/// traversal and common accepted-mechanics orchestration; T012/T021/T023 own cache and
/// common-plan fingerprints; T026 owns publication.
/// </summary>
public sealed partial class WoundEffectBatchPlannerTests
{
    private const string SessionId = "session_wound_batch_test";
    private const string RequestId = "request_wound_batch_test";
    private const string SnapshotToken = "snapshot_wound_batch_test";
    private const string Realm = "mortal_world";
    private const int Turn = 42;
    private const string FingerprintVersion = "1";
    private const string AcceptedEventSetDomain =
        "book_of_eternity.wound.accepted_event_set";
    private const string MaterializationDomain =
        "book_of_eternity.wound.effect_materialization";
    private const string SourceExportDomain =
        "book_of_eternity.wound.source_export";
    private const string WoundPreparationDomain =
        "book_of_eternity.wound.preparation";
    private const string TransitionAuthorityDomain =
        "book_of_eternity.wound.prepared_transition_authority";
    private const string BaselineAuthorityDomain =
        "book_of_eternity.wound.prepared_baseline_authority";

    [Fact]
    public void MaterializationFingerprint_BindsExactSourceSchemaParametersAndOrderedComponents()
    {
        var first = EffectMaterializationTestFixture
            .CreateDefinition("periodic_damage")["components"]![0]!
            .DeepClone().AsObject();
        first["componentId"] = "component_materialization_first";
        var second = EffectMaterializationTestFixture
            .CreateDefinition("action_control")["components"]![0]!
            .DeepClone().AsObject();
        second["componentId"] = "component_materialization_second";
        var components = new JsonArray(first, second);
        var sourceKey = new EffectSourceKey(
            Realm,
            "wound",
            "wound_materialization_fingerprint",
            "definition_materialization_root");
        var parameters = new JsonObject();

        var expected = ComputeExpectedMaterializationFingerprint(
            sourceKey,
            schemaVersion: 1,
            parameters,
            components);
        var actual = WoundEffectMaterializationFingerprint.Compute(
            sourceKey,
            schemaVersion: 1,
            parameters,
            components);
        var reorderedComponents = Assert.IsType<JsonArray>(
            ReverseObjectProperties(components));

        Assert.Equal(MaterializationDomain, WoundEffectMaterializationFingerprint.Domain);
        Assert.Equal(FingerprintVersion, WoundEffectMaterializationFingerprint.Version);
        Assert.Equal(expected, actual);
        Assert.Matches("^sha256:[0-9a-f]{64}$", actual);
        Assert.NotEqual(components.ToJsonString(), reorderedComponents.ToJsonString());
        Assert.Equal(
            actual,
            WoundEffectMaterializationFingerprint.Compute(
                sourceKey,
                schemaVersion: 1,
                parameters.DeepClone().AsObject(),
                reorderedComponents));

        var changedComponent = components.DeepClone().AsArray();
        changedComponent[0]!["componentId"] = "component_materialization_changed";
        var reversedComponents = new JsonArray(components
            .Reverse()
            .Select(static value => value!.DeepClone())
            .ToArray());
        var changed = new[]
        {
            WoundEffectMaterializationFingerprint.Compute(
                sourceKey with { Realm = "afterlife" }, 1, parameters, components),
            WoundEffectMaterializationFingerprint.Compute(
                sourceKey with { Kind = "quest" }, 1, parameters, components),
            WoundEffectMaterializationFingerprint.Compute(
                sourceKey with { SourceId = "wound_materialization_changed" },
                1,
                parameters,
                components),
            WoundEffectMaterializationFingerprint.Compute(
                sourceKey with { DefinitionKey = "definition_materialization_changed" },
                1,
                parameters,
                components),
            WoundEffectMaterializationFingerprint.Compute(sourceKey, 2, parameters, components),
            WoundEffectMaterializationFingerprint.Compute(
                sourceKey,
                1,
                new JsonObject { ["futureParameter"] = 1 },
                components),
            WoundEffectMaterializationFingerprint.Compute(
                sourceKey,
                1,
                parameters,
                new JsonArray(components[0]!.DeepClone())),
            WoundEffectMaterializationFingerprint.Compute(
                sourceKey,
                1,
                parameters,
                changedComponent),
            WoundEffectMaterializationFingerprint.Compute(
                sourceKey,
                1,
                parameters,
                reversedComponents)
        };
        Assert.All(changed, value => Assert.NotEqual(actual, value));
        Assert.Equal(changed.Length, changed.Distinct(StringComparer.Ordinal).Count());

        var unicodeSourceKey = sourceKey with
        {
            SourceId = "рана_魂_🔥",
            DefinitionKey = "определение_ожог_炎"
        };
        var unicodeParameters = new JsonObject
        {
            ["ярлык"] = "ожог_🔥"
        };
        var unicodeComponents = new JsonArray(first.DeepClone());
        var unicodeExpected = ComputeExpectedMaterializationFingerprint(
            unicodeSourceKey,
            1,
            unicodeParameters,
            unicodeComponents);
        var unicodeActual = WoundEffectMaterializationFingerprint.Compute(
            unicodeSourceKey,
            1,
            unicodeParameters,
            unicodeComponents);
        Assert.Equal(unicodeExpected, unicodeActual);
        Assert.NotEqual(
            ComputeIncorrectUtf16LengthMaterializationFingerprint(
                unicodeSourceKey,
                1,
                unicodeParameters,
                unicodeComponents),
            unicodeActual);

        var nestedArrayComponent = EffectMaterializationTestFixture
            .CreateDefinition("roll_modifier")["components"]![0]!
            .DeepClone().AsObject();
        nestedArrayComponent["componentId"] = "component_nested_registered_array";
        nestedArrayComponent["payload"]!["operations"] = new JsonArray(
            "attack_roll",
            "defense_roll",
            "skill_check");
        var nestedComponentIssues = new List<ValidationIssue>();
        using (var document = JsonDocument.Parse(nestedArrayComponent.ToJsonString()))
        {
            EffectComponentProfiles.ValidateComponent(
                document.RootElement,
                "$.component",
                nestedComponentIssues);
        }
        Assert.Empty(nestedComponentIssues);

        var nestedArrayComponents = new JsonArray(nestedArrayComponent);
        var nestedArrayExpected = ComputeExpectedMaterializationFingerprint(
            unicodeSourceKey,
            1,
            unicodeParameters,
            nestedArrayComponents);
        var nestedArrayActual = WoundEffectMaterializationFingerprint.Compute(
            unicodeSourceKey,
            1,
            unicodeParameters,
            nestedArrayComponents);
        Assert.Equal(nestedArrayExpected, nestedArrayActual);

        var nestedArrayReordered = nestedArrayComponents.DeepClone().AsArray();
        var nestedOperations = nestedArrayReordered[0]!["payload"]!["operations"]!
            .AsArray();
        Assert.Equal(3, nestedOperations.Count);
        nestedArrayReordered[0]!["payload"]!["operations"] = new JsonArray(nestedOperations
            .Reverse()
            .Select(static value => value!.DeepClone())
            .ToArray());
        var nestedOrderExpected = ComputeExpectedMaterializationFingerprint(
            unicodeSourceKey,
            1,
            unicodeParameters,
            nestedArrayReordered);
        var nestedOrderActual = WoundEffectMaterializationFingerprint.Compute(
            unicodeSourceKey,
            1,
            unicodeParameters,
            nestedArrayReordered);
        Assert.Equal(nestedOrderExpected, nestedOrderActual);
        Assert.NotEqual(nestedArrayActual, nestedOrderActual);
    }

    [Fact]
    public void AcceptedEventSetFingerprint_BindsOrderedFourFieldAuthorities()
    {
        var first = new WoundAcceptedEventAuthority(
            "turn_42:wound:001",
            "accepted_turn",
            "turn_42",
            Fingerprint("event:first"));
        var second = new WoundAcceptedEventAuthority(
            "turn_42:wound:002",
            "combat_outcome",
            "combat_outcome_002",
            Fingerprint("event:second"));
        var baseline = new[] { first, second };
        var variants = new IReadOnlyList<WoundAcceptedEventAuthority>[]
        {
            new[] { first with { EventRef = "turn_42:wound:changed" }, second },
            new[] { first with { Kind = "resource_outcome" }, second },
            new[] { first with { AuthorityId = "turn_42_changed" }, second },
            new[] { first with { SemanticFingerprint = Fingerprint("event:changed") }, second },
            new[] { second, first }
        };

        var actual = WoundAcceptedEventSetFingerprint.Compute(baseline);

        Assert.Equal(ComputeExpectedAcceptedEventSetFingerprint(baseline), actual);
        Assert.All(variants, variant =>
            Assert.NotEqual(actual, WoundAcceptedEventSetFingerprint.Compute(variant)));
        Assert.Equal(
            variants.Length + 1,
            variants.Select(WoundAcceptedEventSetFingerprint.Compute)
                .Append(actual)
                .Distinct(StringComparer.Ordinal)
                .Count());
    }

    [Theory]
    [InlineData("empty", "wound_plan_event_authority_invalid")]
    [InlineData("null_event", "wound_plan_event_authority_invalid")]
    [InlineData("blank_event_ref", "wound_plan_event_authority_invalid")]
    [InlineData("blank_kind", "wound_plan_event_authority_invalid")]
    [InlineData("blank_authority", "wound_plan_event_authority_invalid")]
    [InlineData("invalid_semantic_fingerprint", "wound_plan_event_authority_invalid")]
    [InlineData("duplicate_event_ref", "wound_plan_event_authority_ambiguous")]
    [InlineData("confusable_event_ref", "wound_plan_event_authority_ambiguous")]
    [InlineData("duplicate_authority", "wound_plan_event_authority_ambiguous")]
    [InlineData("confusable_authority", "wound_plan_event_authority_ambiguous")]
    [InlineData("forged_fingerprint", "wound_plan_event_set_fingerprint_mismatch")]
    [InlineData("reordered_with_old_fingerprint", "wound_plan_event_set_fingerprint_mismatch")]
    public void Prepare_RejectsMalformedAmbiguousOrStaleEventSetBeforeAllocation(
        string mutation,
        string expectedCode)
    {
        var input = CreateEventSetMutation(mutation);
        var allocator = new RecordingWoundIdentityAllocator();

        var result = WoundAcceptedTurnPlanner.Prepare(input, allocator);

        AssertInvalidPreparation(result);
        Assert.Single(result.Issues, issue => issue.Code == expectedCode);
        AssertNoWoundAllocations(allocator);
    }

    [Fact]
    public void Prepare_ValidatesWholeSiblingSetAndThirtyTwoLimitBeforeAllocation()
    {
        var atLimit = AssertPrepared(WoundAcceptedTurnPlanner.Prepare(CreateInput(
            WoundContractTestData.TransitionsPerAcceptedTurnLimit,
            CandidateShape.NoMechanics,
            shareCausalEvent: true)));
        Assert.Equal(
            WoundContractTestData.TransitionsPerAcceptedTurnLimit,
            atLimit.EffectOperationBatches.Count);

        var overAllocator = new RecordingWoundIdentityAllocator();
        var over = WoundAcceptedTurnPlanner.Prepare(
            CreateInput(
                WoundContractTestData.TransitionsPerAcceptedTurnLimit + 1,
                CandidateShape.NoMechanics,
                shareCausalEvent: true),
            overAllocator);
        AssertInvalidPreparation(over);
        Assert.Single(over.Issues, static issue =>
            issue.Code == "wound_plan_transition_limit_exceeded");
        AssertNoWoundAllocations(overAllocator);

        var siblingInput = CreateInput(2, CandidateShape.Standard);
        var transitions = siblingInput.Transitions.ToArray();
        transitions[1] = transitions[1] with
        {
            OpportunityId = "opportunity_absent_from_authority"
        };
        var siblingAllocator = new RecordingWoundIdentityAllocator();
        var sibling = WoundAcceptedTurnPlanner.Prepare(
            siblingInput with { Transitions = transitions },
            siblingAllocator);
        AssertInvalidPreparation(sibling);
        Assert.Single(sibling.Issues, static issue =>
            issue.Code == "wound_plan_opportunity_binding_mismatch");
        AssertNoWoundAllocations(siblingAllocator);
    }

    [Fact]
    public void Prepare_NullTopLevelInputOrAllocatorIsAProgrammerError()
    {
        var input = CreateInput();
        Assert.Equal(
            "input",
            Assert.Throws<ArgumentNullException>(() =>
                WoundAcceptedTurnPlanner.Prepare(null!)).ParamName);
        Assert.Equal(
            "identityAllocator",
            Assert.Throws<ArgumentNullException>(() =>
                WoundAcceptedTurnPlanner.Prepare(input, null!)).ParamName);
    }

    [Theory]
    [InlineData("null_binding", "wound_plan_input_invalid")]
    [InlineData("null_opportunities", "wound_plan_input_invalid")]
    [InlineData("null_opportunity", "wound_plan_input_invalid")]
    [InlineData("null_transitions", "wound_plan_input_invalid")]
    [InlineData("null_transition", "wound_plan_input_invalid")]
    [InlineData("null_definitions", "wound_plan_input_invalid")]
    [InlineData("null_definition", "wound_plan_input_invalid")]
    [InlineData("null_root_applications", "wound_plan_input_invalid")]
    [InlineData("null_root_application", "wound_plan_input_invalid")]
    [InlineData("null_slots", "wound_plan_input_invalid")]
    [InlineData("null_slot", "wound_plan_input_invalid")]
    [InlineData("null_definition_facts", "wound_plan_input_invalid")]
    [InlineData("null_definition_fact", "wound_plan_input_invalid")]
    [InlineData("null_carriers", "wound_plan_input_invalid")]
    [InlineData("null_identity", "wound_plan_input_invalid")]
    [InlineData("null_history", "wound_plan_input_invalid")]
    [InlineData("missing_opportunity", "wound_plan_opportunity_binding_mismatch")]
    [InlineData("duplicate_opportunity", "wound_plan_opportunity_binding_mismatch")]
    [InlineData("duplicate_transition", "wound_plan_opportunity_binding_mismatch")]
    public void Prepare_RejectsMalformedNestedOrNonBijectiveTypedInputBeforeAllocation(
        string mutation,
        string expectedCode)
    {
        var allocator = new RecordingWoundIdentityAllocator();
        var result = WoundAcceptedTurnPlanner.Prepare(
            CreateNestedInputMutation(mutation),
            allocator);

        AssertInvalidPreparation(result);
        Assert.Contains(result.Issues, issue => issue.Code == expectedCode);
        AssertNoWoundAllocations(allocator);
    }

    [Theory]
    [InlineData("blank_session")]
    [InlineData("blank_request")]
    [InlineData("blank_snapshot")]
    [InlineData("invalid_realm")]
    [InlineData("zero_turn")]
    [InlineData("opportunity_session")]
    [InlineData("opportunity_request")]
    [InlineData("opportunity_snapshot")]
    [InlineData("opportunity_event")]
    [InlineData("origin_event")]
    [InlineData("origin_turn")]
    public void Prepare_RejectsCrossBindingAuthorityBeforeAllocation(string mutation)
    {
        var allocator = new RecordingWoundIdentityAllocator();
        var result = WoundAcceptedTurnPlanner.Prepare(
            CreateBindingMutation(mutation),
            allocator);

        AssertInvalidPreparation(result);
        Assert.Contains(result.Issues, static issue =>
            issue.Code == "wound_plan_binding_mismatch" ||
            issue.Code == "wound_plan_binding_invalid");
        AssertNoWoundAllocations(allocator);
    }

    [Theory]
    [InlineData(" padded summary ")]
    [InlineData("too_long")]
    public void Prepare_RejectsInvalidTransitionSummaryBeforeAllocation(string mutation)
    {
        var input = CreateInput();
        var transition = input.Transitions[0] with
        {
            ReadableSummary = mutation == "too_long"
                ? new string('x', WoundMaterializationContract.MaxReadableTextLength + 1)
                : mutation
        };
        var allocator = new RecordingWoundIdentityAllocator();

        var result = WoundAcceptedTurnPlanner.Prepare(
            input with { Transitions = new[] { transition } },
            allocator);

        AssertInvalidPreparation(result);
        Assert.Contains(result.Issues, static issue =>
            issue.Code == "wound_plan_input_invalid");
        AssertNoWoundAllocations(allocator);
    }

    [Fact]
    public void Prepare_RejectsUnroutableCombatantDefinitionBeforeAllocation()
    {
        var input = CreateInput(
            1,
            CandidateShape.Standard,
            OwnerFlavor.Combatant);
        var transition = input.Transitions[0];
        var definitions = transition.EffectDefinitions.ToArray();
        var changedDefinition = definitions[0].Definition.DeepClone().AsObject();
        changedDefinition["display"]!["category"] = "condition";
        definitions[0] = definitions[0] with { Definition = changedDefinition };
        var allocator = new RecordingWoundIdentityAllocator();

        var result = WoundAcceptedTurnPlanner.Prepare(
            input with
            {
                Transitions = new[]
                {
                    transition with { EffectDefinitions = definitions }
                }
            },
            allocator);

        AssertInvalidPreparation(result);
        Assert.Contains(result.Issues, static issue =>
            issue.Code == "wound_plan_consequence_envelope_invalid");
        AssertNoWoundAllocations(allocator);
    }

    [Fact]
    public void Prepare_IsPureAndRemapsEveryCallerLocalIdentityWithoutAllocatingAnEffectId()
    {
        var input = CreateInput(1, CandidateShape.TwoRoots);
        var before = CapturePreTurn(input);
        var draft = input.Transitions[0];
        var draftBytes = SerializeDraft(draft);
        var allocator = new RecordingWoundIdentityAllocator("pure");

        var prepared = AssertPrepared(WoundAcceptedTurnPlanner.Prepare(input, allocator));

        AssertPreTurnUnchanged(input, before);
        Assert.Equal(draftBytes, SerializeDraft(input.Transitions[0]));
        Assert.Single(allocator.WoundRequests);
        Assert.Equal(2, allocator.ApplicationRequests.Count);
        Assert.Single(allocator.TransitionRequests);
        var allocatedWoundId = Assert.Single(prepared.AllocatedWoundIds);
        var allocatedTransitionId = Assert.Single(prepared.AllocatedTransitionIds);
        Assert.NotEqual(draft.LocalWoundRef, allocatedWoundId);
        Assert.NotEqual(draft.LocalTransitionRef, allocatedTransitionId);
        Assert.Equal(allocatedWoundId, Assert.Single(prepared.PreparedWounds).WoundId);

        var batch = Assert.Single(prepared.EffectOperationBatches);
        Assert.Equal(draft.LocalWoundRef, batch.LocalWoundRef);
        Assert.Equal(allocatedWoundId, batch.PreparedWoundId);
        Assert.Equal(allocatedWoundId, batch.SourceExport.SourceId);
        Assert.Equal(draft.LocalWoundRef, batch.SourceExport.SourceRef);
        Assert.All(batch.RootApplications, application =>
        {
            Assert.DoesNotContain(
                draft.RootApplications,
                value => string.Equals(
                    value.LocalApplicationRef,
                    application.ApplicationRef,
                    StringComparison.Ordinal));
            Assert.Null(application.SourceSelector.SourceId);
            Assert.Equal(draft.LocalWoundRef, application.SourceSelector.SourceRef);
            Assert.Equal(allocatedWoundId, application.ExpectedSourceKey.SourceId);
            Assert.Empty(application.Parameters);
        });
        Assert.All(batch.SourceExport.Definitions, static definition =>
            Assert.False(definition.Definition.ContainsKey("effectId")));
        Assert.All(allocator.WoundRequests, scope => AssertScopeMatches(input, scope));
        Assert.All(allocator.ApplicationRequests, request =>
            AssertScopeMatches(input, request.Scope));
        Assert.All(allocator.TransitionRequests, request =>
            AssertScopeMatches(input, request.Scope));
    }

    [Fact]
    public void ProductionWoundAllocator_BindsEveryScopeDimensionAndLocalCorrelation()
    {
        var allocator = new WoundAcceptedTurnIdentityAllocator();
        var baseline = ScopeFor(CreateInput().Transitions[0], CreateInput());
        var scopeVariants = new[]
        {
            baseline with { SessionId = "session_wound_batch_changed" },
            baseline with { RequestId = "request_wound_batch_changed" },
            baseline with { SnapshotToken = "snapshot_wound_batch_changed" },
            baseline with { Realm = "chaos_sea" },
            baseline with { Turn = Turn + 1 },
            baseline with { AcceptedEventsFingerprint = Fingerprint("event-set:changed") },
            baseline with { EventRef = "turn_42:wound:changed" },
            baseline with { OpportunityId = "opportunity_wound_changed" },
            baseline with { Owner = baseline.Owner with { OwnerId = "player_changed" } },
            baseline with { Owner = baseline.Owner with { OwnerKind = "npc" } },
            baseline with
            {
                Owner = baseline.Owner with
                {
                    CarrierPath = WoundCarrierCatalog.NpcPath
                }
            },
            baseline with { DraftKind = "complicate" },
            baseline with { OperationKey = "operation_wound_changed" },
            baseline with { LocalWoundRef = "draft_wound_changed" }
        };
        var baselineIds = AllocateScopeTriplet(allocator, baseline);
        Assert.Equal(baselineIds, AllocateScopeTriplet(allocator, baseline));
        Assert.All(scopeVariants, variant =>
        {
            var changed = AllocateScopeTriplet(allocator, variant);
            Assert.NotEqual(baselineIds.WoundId, changed.WoundId);
            Assert.NotEqual(baselineIds.ApplicationRef, changed.ApplicationRef);
            Assert.NotEqual(baselineIds.TransitionId, changed.TransitionId);
        });

        var changedLocalApplication = allocator.CreateApplicationRef(
            baseline,
            "draft_application_changed",
            "wound_definition_001_changed",
            "operation_root_changed");
        var changedDefinition = allocator.CreateApplicationRef(
            baseline,
            "draft_application_001_001",
            "wound_definition_001_changed",
            "operation_root_001_001");
        var changedRootOperation = allocator.CreateApplicationRef(
            baseline,
            "draft_application_001_001",
            "wound_definition_001_001",
            "operation_root_changed");
        var changedLocalTransition = allocator.CreateTransitionId(
            baseline,
            "draft_transition_changed");
        Assert.DoesNotContain(
            baselineIds.ApplicationRef,
            new[] { changedLocalApplication, changedDefinition, changedRootOperation });
        Assert.NotEqual(baselineIds.TransitionId, changedLocalTransition);

        var all = scopeVariants.SelectMany(variant =>
            AllocateScopeTriplet(allocator, variant).Values)
            .Concat(baselineIds.Values)
            .Append(changedLocalApplication)
            .Append(changedDefinition)
            .Append(changedRootOperation)
            .Append(changedLocalTransition)
            .ToArray();
        Assert.Equal(all.Length, all.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(
            all.Length,
            all.Select(MortalLocationIdentityState.BuildConfusableKey)
                .Distinct(StringComparer.Ordinal)
                .Count());
    }

    [Theory]
    [InlineData("sibling_wound_exact")]
    [InlineData("sibling_wound_confusable")]
    [InlineData("sibling_application_exact")]
    [InlineData("sibling_application_confusable")]
    [InlineData("sibling_transition_exact")]
    [InlineData("sibling_transition_confusable")]
    [InlineData("pre_turn_wound")]
    [InlineData("history_transition")]
    public void Prepare_RejectsAllocatedExactOrConfusableCollisionsWithoutPublishing(
        string mutation)
    {
        var input = mutation switch
        {
            "pre_turn_wound" => WithExistingWound(
                CreateInput(),
                "wound_collision_existing"),
            "history_transition" => WithExistingWound(
                CreateInput(),
                "wound_history_existing"),
            _ => CreateInput(2)
        };
        var before = CapturePreTurn(input);

        var result = WoundAcceptedTurnPlanner.Prepare(
            input,
            new CollisionWoundIdentityAllocator(mutation));

        AssertInvalidPreparation(result);
        Assert.Contains(result.Issues, static issue =>
            issue.Code == "wound_plan_allocated_identity_conflict");
        AssertPreTurnUnchanged(input, before);
    }

    [Fact]
    public void Prepare_ExportsDetachedNonPublicCompleteGraphAndExactLineageSeal()
    {
        var input = CreateInput(1, CandidateShape.ReactionWithMarkerLeaf);
        var sourceDraft = input.Transitions[0].EffectDefinitions[0].Definition;
        var prepared = AssertPrepared(WoundAcceptedTurnPlanner.Prepare(input));
        var batch = Assert.Single(prepared.EffectOperationBatches);
        var export = batch.SourceExport;

        Assert.Equal(1, export.SchemaVersion);
        Assert.Equal("wound", export.Kind);
        Assert.Equal(batch.PreparedWoundId, export.SourceId);
        Assert.Equal(batch.LocalWoundRef, export.SourceRef);
        Assert.Equal("active", export.State);
        Assert.False(export.Materializable);
        Assert.Equal(Realm, export.Realm);
        Assert.Equal(input.Transitions[0].ProposedAfter.Owner, export.Owner);
        Assert.Equal(
            input.Transitions[0].ProposedAfter.Origin.EventRef,
            export.CausalEventRef);
        Assert.Equal(
            input.Binding.AcceptedEvents[0].SemanticFingerprint,
            export.EventSemanticFingerprint);
        Assert.Equal(input.Transitions[0].OpportunityId, export.OpportunityId);
        Assert.Equal(
            input.Opportunities[0].AuthorityFingerprint,
            export.OpportunityAuthorityFingerprint);
        AssertBindingEqual(input.Binding, prepared.Binding);
        Assert.Equal(2, export.Definitions.Count);
        Assert.Single(batch.RootApplications);
        Assert.Empty(batch.TerminalOperations);
        Assert.Single(batch.RootLineageAuthority);

        var root = export.Definitions.Single(value =>
            value.DefinitionKey.EndsWith("_reaction", StringComparison.Ordinal));
        var leaf = export.Definitions.Single(value =>
            value.DefinitionKey.EndsWith("_marker_leaf", StringComparison.Ordinal));
        AssertReciprocalWoundLink(root.Definition, batch.PreparedWoundId);
        AssertReciprocalWoundLink(leaf.Definition, batch.PreparedWoundId);
        var payload = root.Definition["components"]![0]!["payload"]!.AsObject();
        Assert.Equal("apply_definition", payload["resultKind"]!.GetValue<string>());
        Assert.Equal(leaf.DefinitionKey, payload["definitionKey"]!.GetValue<string>());
        Assert.Equal(2, payload["maxExpansion"]!.GetValue<int>());
        Assert.Equal(
            batch.PreparedWoundId,
            leaf.Definition["components"]![0]!["payload"]!["woundId"]!
                .GetValue<string>());

        var application = Assert.Single(batch.RootApplications);
        Assert.Equal(root.DefinitionKey, application.DefinitionKey);
        Assert.Null(application.SourceSelector.SourceId);
        Assert.Equal(batch.LocalWoundRef, application.SourceSelector.SourceRef);
        Assert.Equal(batch.PreparedWoundId, application.ExpectedSourceKey.SourceId);
        Assert.Empty(application.Parameters);
        Assert.Single(application.SlotBindings);
        Assert.Equal("event_reaction", application.SlotBindings[0].ProfileKey);
        Assert.DoesNotContain(batch.RootApplications, value =>
            string.Equals(value.DefinitionKey, leaf.DefinitionKey, StringComparison.Ordinal));

        var lineage = Assert.Single(batch.RootLineageAuthority);
        Assert.Equal(application.ApplicationRef, lineage.ApplicationRef);
        Assert.Null(lineage.EffectId);
        Assert.Equal(root.DefinitionKey, lineage.DefinitionKey);
        Assert.Equal("base_wound", lineage.OwnershipDomain.Kind);
        Assert.Null(lineage.OwnershipDomain.ComplicationId);

        Assert.Equal(
            ComputeExpectedSourceExportFingerprint(batch),
            batch.SourceExportFingerprint);
        Assert.Equal(
            ComputeExpectedWoundPreparationFingerprint(prepared),
            prepared.WoundPreparationFingerprint);
        var frozenRoot = root.Definition.ToJsonString();
        sourceDraft["display"]!["name"] = "caller mutation after Prepare";
        Assert.Equal(frozenRoot, root.Definition.ToJsonString());
        Assert.Equal(
            ComputeExpectedSourceExportFingerprint(batch),
            batch.SourceExportFingerprint);
    }

    [Fact]
    public void Prepare_SeparatesFiveRootBoundaryOneEffectTwoSlotsAndOptionalLeaf()
    {
        var five = AssertPrepared(WoundAcceptedTurnPlanner.Prepare(
            CreateInput(1, CandidateShape.FiveRoots)));
        var fiveBatch = Assert.Single(five.EffectOperationBatches);
        Assert.Equal(5, fiveBatch.SourceExport.Definitions.Count);
        Assert.Equal(5, fiveBatch.RootApplications.Count);
        Assert.Equal(5, fiveBatch.RootLineageAuthority.Count);
        Assert.Equal(4, fiveBatch.RootApplications.Sum(value => value.SlotBindings.Count));
        Assert.Single(fiveBatch.RootApplications, static value =>
            value.SlotBindings.Count == 0);

        var twoSlots = AssertPrepared(WoundAcceptedTurnPlanner.Prepare(
            CreateInput(1, CandidateShape.OneRootTwoSlots)));
        var shared = Assert.Single(Assert.Single(twoSlots.EffectOperationBatches)
            .RootApplications);
        Assert.Equal(2, shared.SlotBindings.Count);
        Assert.Equal(2, shared.ExpectedComponentCount);
        var sharedDefinition = Assert.Single(
            Assert.Single(twoSlots.EffectOperationBatches).SourceExport.Definitions,
            value => string.Equals(
                value.DefinitionKey,
                shared.DefinitionKey,
                StringComparison.Ordinal));
        var sharedComponents = sharedDefinition.Definition["components"]!.AsArray();
        Assert.Equal(
            ComputeExpectedMaterializationFingerprint(
                shared.ExpectedSourceKey,
                sharedDefinition.Definition["schemaVersion"]!.GetValue<int>(),
                shared.Parameters,
                sharedComponents),
            shared.ExpectedMaterializationFingerprint);
        Assert.NotEqual(
            shared.ExpectedMaterializationFingerprint,
            ComputeExpectedMaterializationFingerprint(
                shared.ExpectedSourceKey,
                sharedDefinition.Definition["schemaVersion"]!.GetValue<int>(),
                shared.Parameters,
                new JsonArray(sharedComponents
                    .Reverse()
                    .Select(static value => value!.DeepClone())
                    .ToArray())));

        var leaf = AssertPrepared(WoundAcceptedTurnPlanner.Prepare(
            CreateInput(1, CandidateShape.ReactionWithMarkerLeaf)));
        var leafBatch = Assert.Single(leaf.EffectOperationBatches);
        Assert.Equal(2, leafBatch.SourceExport.Definitions.Count);
        Assert.Single(leafBatch.RootApplications);

        var allocator = new RecordingWoundIdentityAllocator();
        var six = WoundAcceptedTurnPlanner.Prepare(
            CreateInput(1, CandidateShape.SixRoots),
            allocator);
        AssertInvalidPreparation(six);
        Assert.Contains(six.Issues, static issue =>
            issue.Code == "wound_plan_root_application_limit_exceeded" ||
            issue.Code == "wound_plan_consequence_envelope_invalid");
        AssertNoWoundAllocations(allocator);
    }

    [Fact]
    public void DirectZeroSlotMarker_IsAllocatedByEffectStageAndPersistedAsRootBinding()
    {
        var input = CreateInput(1, CandidateShape.MarkerRoot);
        var draftBytes = SerializeDraft(input.Transitions[0]);
        var prepared = AssertPrepared(WoundAcceptedTurnPlanner.Prepare(input));
        var batch = Assert.Single(prepared.EffectOperationBatches);
        Assert.Single(batch.SourceExport.Definitions);
        var expectedApplication = Assert.Single(batch.RootApplications);
        Assert.Empty(expectedApplication.SlotBindings);

        var factory = new CountingEffectIdentityFactory();
        var effectStage = BuildEffectStage(prepared, factory);
        var effectPlan = AssertEffectPlan(effectStage.Result);
        var application = Assert.Single(effectPlan.ApplicationResults);
        Assert.Equal(1, factory.EffectCalls);
        Assert.Equal(1, factory.TransitionCalls);
        Assert.Empty(application.Materialization.SlotBindings);
        Assert.Equal(1, application.Materialization.ComponentCount);
        var activeEffect = FindActiveEffect(effectPlan, application.EffectId);
        var activeSourceKey = ReadActiveEffectSourceKey(activeEffect);
        Assert.Equal(expectedApplication.ExpectedSourceKey, activeSourceKey);
        Assert.Equal(application.SourceKey, activeSourceKey);
        var expectedFingerprint = ComputeExpectedMaterializationFingerprint(
            activeSourceKey,
            activeEffect["schemaVersion"]!.GetValue<int>(),
            expectedApplication.Parameters,
            activeEffect["components"]!.AsArray());
        Assert.Matches(
            "^sha256:[0-9a-f]{64}$",
            application.Materialization.MaterializationFingerprint);
        Assert.Equal(
            expectedApplication.ExpectedMaterializationFingerprint,
            expectedFingerprint);
        Assert.Equal(
            expectedFingerprint,
            application.Materialization.MaterializationFingerprint);

        var final = AssertFinalized(WoundAcceptedTurnPlanner.Finalize(
            prepared,
            effectStage.Result));
        var afterWound = Assert.Single(Assert.Single(final.CarrierContributions)
            .Mutations).AfterWound!;
        var consequences = ToJson(afterWound)["consequences"]!.AsObject();
        var owned = consequences["ownedEffectSources"]!.AsObject();
        Assert.Single(owned["definitions"]!.AsArray());
        var binding = Assert.IsType<JsonObject>(
            Assert.Single(owned["rootBindings"]!.AsArray()));
        Assert.Equal(application.EffectId, binding["effectId"]!.GetValue<string>());
        Assert.Empty(consequences["entries"]!.AsArray());
        Assert.DoesNotContain(
            input.Transitions[0].LocalWoundRef,
            WoundMaterializationContract.SerializeCanonical(afterWound),
            StringComparison.Ordinal);
        var effectIntent = Assert.Single(final.TransitionIntents
            .OfType<WoundEffectTransitionIntent>());
        Assert.Contains(application.EffectId, effectIntent.AfterEffectIds);
        Assert.Equal(draftBytes, SerializeDraft(input.Transitions[0]));
    }

    [Fact]
    public void NonMechanicalWound_SealsZeroOperationBatchAndEmptyEffectResult()
    {
        var input = CreateInput(1, CandidateShape.NoMechanics);
        var prepared = AssertPrepared(WoundAcceptedTurnPlanner.Prepare(input));
        var batch = Assert.Single(prepared.EffectOperationBatches);
        Assert.False(batch.SourceExport.Materializable);
        Assert.Empty(batch.SourceExport.Definitions);
        Assert.Empty(batch.RootApplications);
        Assert.Empty(batch.TerminalOperations);
        Assert.Empty(batch.RootLineageAuthority);
        Assert.Equal(
            ComputeExpectedSourceExportFingerprint(batch),
            batch.SourceExportFingerprint);

        var factory = new CountingEffectIdentityFactory();
        var effectStage = BuildEffectStage(prepared, factory);
        var effectPlan = AssertEffectPlan(effectStage.Result);
        Assert.Empty(effectPlan.ApplicationResults);
        Assert.Empty(effectPlan.TerminationResults);
        Assert.Empty(effectPlan.EffectPlan.AllocatedEffectIds);
        Assert.Empty(effectPlan.EffectPlan.CarrierAfterImages);
        Assert.Equal(0, factory.EffectCalls);
        Assert.Equal(0, factory.TransitionCalls);

        var final = AssertFinalized(WoundAcceptedTurnPlanner.Finalize(
            prepared,
            effectStage.Result));
        var wound = Assert.Single(Assert.Single(final.CarrierContributions)
            .Mutations).AfterWound!;
        var consequences = ToJson(wound)["consequences"]!.AsObject();
        Assert.Empty(consequences["entries"]!.AsArray());
        Assert.Empty(consequences["ownedEffectSources"]!["definitions"]!.AsArray());
        Assert.Empty(consequences["ownedEffectSources"]!["rootBindings"]!.AsArray());
        Assert.NotEqual(batch.SourceExportFingerprint, prepared.WoundPreparationFingerprint);
        Assert.NotEqual(prepared.WoundPreparationFingerprint, effectPlan.EffectInputFingerprint);
        Assert.NotEqual(
            effectPlan.EffectInputFingerprint,
            effectPlan.EffectAcceptedTurnPlanFingerprint);
        Assert.NotEqual(
            effectPlan.EffectAcceptedTurnPlanFingerprint,
            final.WoundFinalPlanFingerprint);
    }

    [Fact]
    public void EmptyTypedWork_ReturnsDetachedNoOpPlansAcrossAllThreeStages()
    {
        var input = CreateNoOpInput();
        var prepared = AssertPrepared(WoundAcceptedTurnPlanner.Prepare(input));
        Assert.Empty(prepared.AllocatedWoundIds);
        Assert.Empty(prepared.AllocatedTransitionIds);
        Assert.Empty(prepared.PreparedWounds);
        Assert.Empty(prepared.EffectOperationBatches);

        var effectStage = BuildEffectStage(prepared, new CountingEffectIdentityFactory());
        var effectPlan = AssertEffectPlan(effectStage.Result);
        Assert.Empty(effectPlan.ApplicationResults);
        Assert.Empty(effectPlan.TerminationResults);

        var final = AssertFinalized(WoundAcceptedTurnPlanner.Finalize(
            prepared,
            effectStage.Result));
        Assert.Empty(final.AllocatedWoundIds);
        Assert.Empty(final.AllocatedTransitionIds);
        Assert.Empty(final.CarrierContributions);
        Assert.Empty(final.TransitionIntents);
        Assert.NotEmpty(final.WoundFinalPlanFingerprint);
    }

    [Fact]
    public void EffectStage_OwnsOpaqueRootIdsAndDistinctCreatedEventsForSharedCausality()
    {
        var input = CreateInput(2, CandidateShape.Standard, shareCausalEvent: true);
        var prepared = AssertPrepared(WoundAcceptedTurnPlanner.Prepare(input));
        var factory = new ScriptedEffectIdentityFactory("shared");
        var effectStage = BuildEffectStage(prepared, factory);
        var plan = AssertEffectPlan(effectStage.Result);
        var acceptedAgain = AssertEffectPlan(WoundEffectBatchPlanner.AcceptEffectResult(
            prepared,
            plan.EffectInput,
            new EffectAcceptedTurnPlanningResult(
                plan.EffectPlan,
                Array.Empty<ValidationIssue>())));

        Assert.Equal(2, factory.EffectCalls);
        Assert.Equal(2, factory.TransitionCalls);
        Assert.Equal(2, plan.ApplicationResults.Count);
        Assert.Equal(
            plan.EffectInputFingerprint,
            acceptedAgain.EffectInputFingerprint);
        Assert.Equal(
            plan.EffectAcceptedTurnPlanFingerprint,
            acceptedAgain.EffectAcceptedTurnPlanFingerprint);
        Assert.Equal(
            plan.ApplicationResults.Select(static value => value.EffectId),
            acceptedAgain.ApplicationResults.Select(static value => value.EffectId));
        Assert.Equal(
            2,
            plan.ApplicationResults.Select(static value => value.EffectId)
                .Distinct(StringComparer.Ordinal)
                .Count());
        Assert.Equal(
            2,
            plan.ApplicationResults.Select(static value => value.EffectId)
                .Select(MortalLocationIdentityState.BuildConfusableKey)
                .Distinct(StringComparer.Ordinal)
                .Count());
        Assert.Single(plan.ApplicationResults
            .Select(static value => value.CausalEventRef)
            .Distinct(StringComparer.Ordinal));
        Assert.Equal(
            2,
            plan.ApplicationResults.Select(static value => value.CreatedEventRef)
                .Distinct(StringComparer.Ordinal)
                .Count());

        var applications = prepared.EffectOperationBatches
            .SelectMany(static batch => batch.RootApplications)
            .ToArray();
        for (var index = 0; index < applications.Length; index++)
        {
            var expected = applications[index];
            var actual = plan.ApplicationResults[index];
            Assert.Equal(expected.ApplicationRef, actual.ApplicationRef);
            Assert.Equal("created_new_identity", actual.Disposition);
            Assert.Equal(
                WoundEffectOperationEventRef.Create(
                    expected.CausalEventRef,
                    expected.MechanicsOrdinal,
                    expected.OperationOrdinal,
                    expected.OperationKind),
                actual.CreatedEventRef);
            Assert.Equal(expected.CausalEventRef, actual.CausalEventRef);
            Assert.Equal(expected.ExpectedSourceKey, actual.SourceKey);
            Assert.Equal(expected.ExpectedTargetKey, actual.TargetKey);
            Assert.Equal(expected.ExpectedCarrierCoordinate, actual.CarrierCoordinate);
            Assert.Equal(expected.ExpectedComponentCount, actual.Materialization.ComponentCount);
            var activeEffect = FindActiveEffect(plan, actual.EffectId);
            var activeSourceKey = ReadActiveEffectSourceKey(activeEffect);
            Assert.Equal(expected.ExpectedSourceKey, activeSourceKey);
            Assert.Equal(actual.SourceKey, activeSourceKey);
            var materializationFingerprint = ComputeExpectedMaterializationFingerprint(
                activeSourceKey,
                activeEffect["schemaVersion"]!.GetValue<int>(),
                expected.Parameters,
                activeEffect["components"]!.AsArray());
            Assert.Equal(
                expected.ExpectedMaterializationFingerprint,
                actual.Materialization.MaterializationFingerprint);
            Assert.Equal(
                materializationFingerprint,
                actual.Materialization.MaterializationFingerprint);
            AssertSlotAgreementEqual(expected.SlotBindings, actual.Materialization.SlotBindings);
        }

        Assert.DoesNotContain(
            plan.ApplicationResults.Select(static value => value.EffectId),
            effectId => prepared.EffectOperationBatches.Any(batch =>
                batch.SourceExport.Definitions.Any(definition =>
                    definition.Definition.ToJsonString().Contains(
                        effectId,
                        StringComparison.Ordinal))));
    }

    [Fact]
    public void EffectStage_RejectsTerminalOperationBoundToDifferentAcceptedEvent()
    {
        var input = CreateInput();
        var foreignEvent = new WoundAcceptedEventAuthority(
            "turn_42:wound:foreign_terminal_event",
            "combat_outcome",
            "authority_foreign_terminal_event",
            Fingerprint("accepted-event:foreign-terminal-event"));
        var acceptedEvents = input.Binding.AcceptedEvents
            .Append(foreignEvent)
            .ToArray();
        input = input with
        {
            Binding = input.Binding with
            {
                AcceptedEvents = acceptedEvents,
                AcceptedEventsFingerprint =
                    ComputeExpectedAcceptedEventSetFingerprint(acceptedEvents)
            }
        };
        var prepared = AssertPrepared(WoundAcceptedTurnPlanner.Prepare(
            input,
            new RecordingWoundIdentityAllocator("terminal_causal")));
        var creationStage = BuildEffectStage(
            prepared,
            new ScriptedEffectIdentityFactory("terminal_causal_create"));
        var creationPlan = AssertEffectPlan(creationStage.Result);
        var created = Assert.Single(creationPlan.ApplicationResults);
        var finalized = AssertFinalized(WoundAcceptedTurnPlanner.Finalize(
            prepared,
            creationStage.Result));
        var wound = Assert.IsType<WoundMaterializationEnvelope>(
            Assert.Single(Assert.Single(finalized.CarrierContributions).Mutations)
                .AfterWound);
        var preTurnCarriers = CreateEffectCarriersFromPlan(creationPlan.EffectPlan);
        var identities = ParseEffectIdentityState(
            creationPlan.EffectPlan.IdentityIndexAfterImage);
        var batch = Assert.Single(prepared.EffectOperationBatches);
        var terminal = WoundEffectTerminalOperationPlanner.Plan(
            wound,
            preTurnCarriers,
            identities,
            new[] { created.EffectId },
            prepared.Binding.AcceptedEvents[0].EventRef,
            mechanicsOrdinal: 1,
            operationOrdinalOffset: 0,
            batch.TransitionAuthority.OperationKey);
        Assert.Empty(terminal.Issues);
        var original = Assert.Single(terminal.Operations);
        var forged = new WoundTerminalEffectOperation(
            WoundEffectOperationEventRef.Create(
                foreignEvent.EventRef,
                original.MechanicsOrdinal,
                original.OperationOrdinal,
                original.OperationKind),
            original.OperationKey,
            original.EffectId,
            original.MechanicsOrdinal,
            original.OperationOrdinal,
            original.OperationKind,
            foreignEvent.EventRef,
            original.ExpectedSourceKey,
            original.ExpectedTargetKey,
            original.ExpectedCarrierCoordinate,
            original.ExpectedCarrierFilePath,
            original.ExpectedCarrierJsonPath,
            original.ExpectedIdentityOwner,
            original.ExpectedStackCoordinate,
            original.ExpectedEffectFingerprint,
            original.ExpectedIdentityFingerprint,
            original.OwnershipDomain);
        var terminalPrepared = CreateTerminalPreparedPlan(
            prepared,
            wound,
            forged);
        var terminalInput = CreateEffectInput(
            terminalPrepared,
            preTurnCarriersOverride: preTurnCarriers,
            preTurnIdentityIndexOverride:
                creationPlan.EffectPlan.IdentityIndexAfterImage,
            mutateEventInput: eventInput =>
                eventInput["lifecycleEvents"] = new JsonArray(new JsonObject
                {
                    ["eventRef"] = "turn_42:lifecycle:wound_source_lost_foreign",
                    ["turn"] = Turn,
                    ["phase"] = "owner_turn_end",
                    ["realm"] = Realm,
                    ["target"] = new JsonObject
                    {
                        ["kind"] = "player",
                        ["targetId"] = "player_current"
                    },
                    ["effectId"] = created.EffectId,
                    ["sourceSatisfied"] = false
                }));
        var factory = new CountingEffectIdentityFactory();

        var result = EffectAcceptedTurnPlanner.BuildWoundBatch(
            terminalInput,
            terminalPrepared,
            factory);

        Assert.False(result.Success);
        Assert.Null(result.Plan);
        Assert.Contains(result.Issues, static issue =>
            issue.Code == "wound_plan_effect_handoff_invalid");
        Assert.Equal(0, factory.EffectCalls);
        Assert.Equal(0, factory.TransitionCalls);
    }

    [Fact]
    public void EffectStage_TypedWoundTerminalRemovesExactCarrierAndExpiresIdentity()
    {
        var input = CreateInput(1, CandidateShape.Standard);
        var prepared = AssertPrepared(WoundAcceptedTurnPlanner.Prepare(
            input,
            new RecordingWoundIdentityAllocator("terminal")));
        var creationStage = BuildEffectStage(
            prepared,
            new ScriptedEffectIdentityFactory("terminal_create"));
        var creationPlan = AssertEffectPlan(creationStage.Result);
        var created = Assert.Single(creationPlan.ApplicationResults);
        var finalized = AssertFinalized(WoundAcceptedTurnPlanner.Finalize(
            prepared,
            creationStage.Result));
        var wound = Assert.IsType<WoundMaterializationEnvelope>(
            Assert.Single(Assert.Single(finalized.CarrierContributions).Mutations)
                .AfterWound);
        var preTurnCarriers = CreateEffectCarriersFromPlan(creationPlan.EffectPlan);
        var identities = ParseEffectIdentityState(
            creationPlan.EffectPlan.IdentityIndexAfterImage);
        var batch = Assert.Single(prepared.EffectOperationBatches);
        var terminal = WoundEffectTerminalOperationPlanner.Plan(
            wound,
            preTurnCarriers,
            identities,
            wound.Consequences.OwnedEffectSources.RootBindings
                .Select(static value => value.EffectId)
                .ToArray(),
            prepared.Binding.AcceptedEvents[0].EventRef,
            mechanicsOrdinal: 1,
            operationOrdinalOffset: 0,
            batch.TransitionAuthority.OperationKey);

        Assert.Empty(terminal.Issues);
        var operation = Assert.Single(terminal.Operations);
        Assert.Equal(created.EffectId, operation.EffectId);
        var terminalPrepared = CreateTerminalPreparedPlan(
            prepared,
            wound,
            operation);
        var terminalInput = CreateEffectInput(
            terminalPrepared,
            preTurnCarriersOverride: preTurnCarriers,
            preTurnIdentityIndexOverride:
                creationPlan.EffectPlan.IdentityIndexAfterImage,
            mutateEventInput: eventInput =>
                eventInput["lifecycleEvents"] = new JsonArray(new JsonObject
                {
                    ["eventRef"] = "turn_42:lifecycle:wound_source_lost",
                    ["turn"] = Turn,
                    ["phase"] = "owner_turn_end",
                    ["realm"] = Realm,
                    ["target"] = new JsonObject
                    {
                        ["kind"] = "player",
                        ["targetId"] = "player_current"
                    },
                    ["effectId"] = created.EffectId,
                    ["sourceSatisfied"] = false
                }));
        var tamperedPlayer = terminalInput.PreTurnCarriers!.PlayerEffects!
            .DeepClone().AsObject();
        var tamperedEffect = Assert.Single(
            tamperedPlayer["activeEffects"]!.AsArray().OfType<JsonObject>(),
            effect => string.Equals(
                effect["effectId"]?.GetValue<string>(),
                created.EffectId,
                StringComparison.Ordinal));
        tamperedEffect["chronology"]!["lastTransitionId"] =
            "effect_transition_forged_terminal_before_image";
        var tamperedInput = terminalInput with
        {
            PreTurnCarriers = terminalInput.PreTurnCarriers with
            {
                PlayerEffects = tamperedPlayer
            }
        };
        var rejectedFactory = new ScriptedEffectIdentityFactory(
            "terminal_rejected");
        var rejected = EffectAcceptedTurnPlanner.BuildWoundBatch(
            tamperedInput,
            terminalPrepared,
            rejectedFactory);
        Assert.False(rejected.Success);
        Assert.Contains(rejected.Issues, issue => string.Equals(
            issue.Code,
            "wound_plan_effect_handoff_invalid",
            StringComparison.Ordinal));
        Assert.Equal(0, rejectedFactory.EffectCalls);
        Assert.Equal(0, rejectedFactory.TransitionCalls);

        var terminalFactory = new ScriptedEffectIdentityFactory("terminal_close");

        var result = EffectAcceptedTurnPlanner.BuildWoundBatch(
            terminalInput,
            terminalPrepared,
            terminalFactory);

        Assert.True(
            result.Success,
            string.Join(" | ", result.Issues.Select(static issue =>
                issue.Code + ":" + issue.Message)));
        var plan = Assert.IsType<EffectAcceptedTurnPlan>(result.Plan);
        Assert.DoesNotContain(
            plan.CarrierAfterImages[EffectCarrierCatalog.PlayerPath]
                ["activeEffects"]!.AsArray().OfType<JsonObject>(),
            effect => string.Equals(
                effect["effectId"]?.GetValue<string>(),
                created.EffectId,
                StringComparison.Ordinal));
        var identity = FindIdentityIndexEffect(plan, created.EffectId);
        Assert.Equal("expired", identity["state"]!.GetValue<string>());
        var transition = Assert.IsType<JsonObject>(
            Assert.Single(identity["transitions"]!.AsArray(), value =>
                string.Equals(
                    value?["eventRef"]?.GetValue<string>(),
                    operation.OperationRef,
                    StringComparison.Ordinal)));
        Assert.Equal("expire", transition["kind"]!.GetValue<string>());
        Assert.Equal(created.EffectId,
            Assert.Single(transition["sourceEffectIds"]!.AsArray())!
                .GetValue<string>());
        Assert.Empty(transition["resultEffectIds"]!.AsArray());
        Assert.Equal(0, terminalFactory.EffectCalls);
        Assert.Equal(1, terminalFactory.TransitionCalls);

        var accepted = WoundEffectBatchPlanner.AcceptEffectResult(
            terminalPrepared,
            terminalInput,
            result);
        Assert.True(
            accepted.Success,
            string.Join(" | ", accepted.Issues.Select(static issue =>
                issue.Code + ":" + issue.Message)));
        var acceptedPlan = Assert.IsType<WoundEffectBatchAcceptedPlan>(accepted.Plan);
        Assert.Empty(acceptedPlan.ApplicationResults);
        var termination = Assert.Single(acceptedPlan.TerminationResults);
        Assert.Equal(operation.OperationRef, termination.OperationRef);
        Assert.Equal("expired", termination.Disposition);
        Assert.Equal(operation.EffectId, termination.EffectId);
        Assert.Equal(transition["transitionId"]!.GetValue<string>(),
            termination.TerminalTransitionId);
        Assert.Equal(operation.OperationRef, termination.TransitionEventRef);
        Assert.Equal(operation.CausalEventRef, termination.CausalEventRef);
        Assert.Equal(operation.ExpectedSourceKey, termination.SourceKey);
        Assert.Equal(operation.ExpectedTargetKey, termination.TargetKey);
        Assert.Equal(
            operation.ExpectedCarrierCoordinate,
            termination.CarrierCoordinate);

        var forgedTermination = termination with
        {
            SourceKey = termination.SourceKey with
            {
                DefinitionKey = "forged_terminal_definition"
            }
        };
        var forgedPlan = WoundEffectBatchAcceptedPlan.Create(
            terminalPrepared,
            terminalInput,
            plan,
            acceptedPlan.ApplicationResults,
            new[] { forgedTermination });
        var forgedFinal = WoundAcceptedTurnPlanner.Finalize(
            terminalPrepared,
            new WoundEffectBatchPlanningResult(
                forgedPlan,
                Array.Empty<ValidationIssue>()));
        Assert.False(forgedFinal.Success);
        Assert.Contains(forgedFinal.Issues, issue => string.Equals(
            issue.Code,
            "wound_plan_effect_result_agreement_mismatch",
            StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("carrier")]
    [InlineData("identity")]
    public void DeriveEffectResults_RejectsMutatedUnselectedSurvivingWoundEffect(
        string mutation)
    {
        const string survivorEffectId = "effect_wound_survivor_fixture";
        var input = CreateInput(1, CandidateShape.Standard);
        var prepared = AssertPrepared(WoundAcceptedTurnPlanner.Prepare(
            input,
            new RecordingWoundIdentityAllocator("survivor")));
        var creationStage = BuildEffectStage(
            prepared,
            new ScriptedEffectIdentityFactory("survivor_create"));
        var creationPlan = AssertEffectPlan(creationStage.Result);
        var created = Assert.Single(creationPlan.ApplicationResults);
        var finalized = AssertFinalized(WoundAcceptedTurnPlanner.Finalize(
            prepared,
            creationStage.Result));
        var wound = Assert.IsType<WoundMaterializationEnvelope>(
            Assert.Single(Assert.Single(finalized.CarrierContributions).Mutations)
                .AfterWound);
        var preTurnCarriers = CreateEffectCarriersFromPlan(creationPlan.EffectPlan);
        var identities = ParseEffectIdentityState(
            creationPlan.EffectPlan.IdentityIndexAfterImage);
        var batch = Assert.Single(prepared.EffectOperationBatches);
        var terminal = WoundEffectTerminalOperationPlanner.Plan(
            wound,
            preTurnCarriers,
            identities,
            new[] { created.EffectId },
            prepared.Binding.AcceptedEvents[0].EventRef,
            mechanicsOrdinal: 1,
            operationOrdinalOffset: 0,
            batch.TransitionAuthority.OperationKey);
        Assert.Empty(terminal.Issues);
        var operation = Assert.Single(terminal.Operations);
        var terminalPrepared = CreateTerminalPreparedPlan(
            prepared,
            wound,
            operation);
        var terminalInput = CreateEffectInput(
            terminalPrepared,
            preTurnCarriersOverride: preTurnCarriers,
            preTurnIdentityIndexOverride:
                creationPlan.EffectPlan.IdentityIndexAfterImage,
            mutateEventInput: eventInput =>
                eventInput["lifecycleEvents"] = new JsonArray(new JsonObject
                {
                    ["eventRef"] = "turn_42:lifecycle:wound_source_lost_survivor",
                    ["turn"] = Turn,
                    ["phase"] = "owner_turn_end",
                    ["realm"] = Realm,
                    ["target"] = new JsonObject
                    {
                        ["kind"] = "player",
                        ["targetId"] = "player_current"
                    },
                    ["effectId"] = created.EffectId,
                    ["sourceSatisfied"] = false
                }));
        var effectResult = EffectAcceptedTurnPlanner.BuildWoundBatch(
            terminalInput,
            terminalPrepared,
            new ScriptedEffectIdentityFactory("survivor_terminal"));
        Assert.True(
            effectResult.Success,
            string.Join(" | ", effectResult.Issues.Select(static issue =>
                issue.Code + ":" + issue.Message +
                "; expected=" + issue.Expected +
                "; actual=" + issue.Actual)));
        var accepted = WoundEffectBatchPlanner.AcceptEffectResult(
            terminalPrepared,
            terminalInput,
            effectResult);
        var acceptedPlan = Assert.IsType<WoundEffectBatchAcceptedPlan>(accepted.Plan);
        var forgedEffectPlan = CloneEffectPlanWithInjectedSurvivorMutation(
            acceptedPlan.EffectPlan,
            created.EffectId,
            survivorEffectId,
            mutation);

        var result = WoundAcceptedTurnPlannerCore.DeriveEffectResults(
            terminalPrepared,
            forgedEffectPlan);

        Assert.Contains(result.Issues, static issue =>
            issue.Code == "wound_plan_effect_result_agreement_mismatch");
    }

    [Fact]
    public void Finalize_CanonicallyRenumbersSlotsAfterReverseOrderedOpaqueIds()
    {
        var prepared = AssertPrepared(WoundAcceptedTurnPlanner.Prepare(
            CreateInput(1, CandidateShape.TwoRoots)));
        var batch = Assert.Single(prepared.EffectOperationBatches);
        Assert.Equal(new[] { 1, 2 }, batch.RootApplications
            .SelectMany(static application => application.SlotBindings)
            .Select(static slot => slot.Slot));

        var stage = BuildEffectStage(prepared, new ReverseOrderedEffectIdentityFactory());
        var accepted = AssertEffectPlan(stage.Result);
        Assert.Equal(
            new[] { "effect_reverse_z", "effect_reverse_a" },
            accepted.ApplicationResults.Select(static result => result.EffectId));
        Assert.Equal(
            new[] { 2, 1 },
            accepted.ApplicationResults
                .SelectMany(static result => result.Materialization.SlotBindings)
                .Select(static slot => slot.Slot));

        var final = AssertFinalized(WoundAcceptedTurnPlanner.Finalize(
            prepared,
            stage.Result));
        var contribution = Assert.Single(final.CarrierContributions);
        var wound = Assert.IsType<WoundMaterializationEnvelope>(
            Assert.Single(contribution.Mutations).AfterWound);
        Assert.Equal(
            new[] { 1, 2 },
            wound.Consequences.Entries.Select(static entry => entry.Slot));
        Assert.Equal(
            new[] { "effect_reverse_a", "effect_reverse_z" },
            wound.Consequences.Entries.Select(static entry => entry.EffectId));
    }

    [Fact]
    public void EffectStage_RejectsChangedPreparedSourceUnderOldPreparationSealBeforeAllocation()
    {
        var input = CreateInput();
        var before = CapturePreTurn(input);
        var prepared = AssertPrepared(WoundAcceptedTurnPlanner.Prepare(input));
        var root = Assert.Single(
            Assert.Single(prepared.EffectOperationBatches).RootApplications);
        string? changedMaterializationFingerprint = null;
        var factory = new CountingEffectIdentityFactory();

        var stage = BuildEffectStage(
            prepared,
            factory,
            mutateWoundExports: woundExports =>
            {
                var export = Assert.Single(woundExports);
                var definitions = export.Definitions.DeepClone().AsArray();
                var definition = Assert.Single(definitions.OfType<JsonObject>(), value =>
                    string.Equals(
                        value["definitionKey"]?.GetValue<string>(),
                        root.DefinitionKey,
                        StringComparison.Ordinal));
                definition["components"]![0]!["componentId"] =
                    "component_changed_under_old_preparation_seal";
                changedMaterializationFingerprint =
                    ComputeExpectedMaterializationFingerprint(
                        root.ExpectedSourceKey,
                        definition["schemaVersion"]!.GetValue<int>(),
                        root.Parameters,
                        definition["components"]!.AsArray());
                woundExports[0] = export with { Definitions = definitions };
            });

        Assert.NotNull(changedMaterializationFingerprint);
        Assert.NotEqual(
            root.ExpectedMaterializationFingerprint,
            changedMaterializationFingerprint);
        Assert.False(stage.Result.Success);
        Assert.Null(stage.Result.Plan);
        var issue = Assert.Single(stage.Result.Issues);
        Assert.Equal("wound_plan_prepared_seal_mismatch", issue.Code);
        Assert.Equal(0, factory.EffectCalls);
        Assert.Equal(0, factory.TransitionCalls);
        AssertPreTurnUnchanged(input, before);
    }

    [Fact]
    public void EffectStage_RejectsExtraSameTurnWoundSourceBeforeAllocation()
    {
        const string extraSourceId = "wound_orphan_same_turn";
        const string extraDefinitionKey = "wound_orphan_definition";
        var prepared = AssertPrepared(WoundAcceptedTurnPlanner.Prepare(CreateInput()));
        var batch = Assert.Single(prepared.EffectOperationBatches);
        var effectInput = CreateEffectInput(
            prepared,
            mutateWoundExports: exports =>
            {
                var definition = EffectMaterializationTestFixture.CreateDefinition();
                definition["definitionKey"] = extraDefinitionKey;
                definition["links"] = new JsonArray(new JsonObject
                {
                    ["kind"] = "wound",
                    ["targetId"] = extraSourceId,
                    ["role"] = "source"
                });
                exports.Add(new EffectSourceExport(
                    batch.SourceExport.Realm,
                    "wound",
                    extraSourceId,
                    new JsonArray(definition),
                    Materializable: false,
                    Active: true,
                    SameTurn: true,
                    SourceRef: batch.LocalWoundRef));
            });

        Assert.Empty(effectInput.SourceAuthority.Issues);
        Assert.Empty(effectInput.RawCommands["effectChanges"]!.AsArray());

        var wrapperFactory = new CountingEffectIdentityFactory();
        var wrapper = WoundEffectBatchPlanner.Build(
            prepared,
            effectInput,
            wrapperFactory);
        Assert.False(wrapper.Success);
        Assert.Null(wrapper.Plan);
        var wrapperIssue = Assert.Single(wrapper.Issues);
        Assert.Equal("wound_plan_prepared_seal_mismatch", wrapperIssue.Code);
        Assert.Equal(0, wrapperFactory.EffectCalls);
        Assert.Equal(0, wrapperFactory.TransitionCalls);

        var directFactory = new CountingEffectIdentityFactory();
        var direct = EffectAcceptedTurnPlanner.BuildWoundBatch(
            effectInput,
            prepared,
            directFactory);
        Assert.False(direct.Success);
        Assert.Null(direct.Plan);
        var directIssue = Assert.Single(direct.Issues);
        Assert.Equal("wound_plan_effect_handoff_invalid", directIssue.Code);
        Assert.Equal(0, directFactory.EffectCalls);
        Assert.Equal(0, directFactory.TransitionCalls);
    }

    [Fact]
    public void EffectSourceResolution_DoesNotExposeMutablePredicateAuthority()
    {
        var prepared = AssertPrepared(WoundAcceptedTurnPlanner.Prepare(CreateInput()));
        var root = Assert.Single(
            Assert.Single(prepared.EffectOperationBatches).RootApplications);
        var input = CreateEffectInput(prepared);
        var fingerprint = input.SourceAuthority.Fingerprint;
        var first = input.SourceAuthority.ResolveCanonicalBinding(
            root.ExpectedSourceKey,
            root.ExpectedTargetKey.Kind);
        Assert.True(first.Success);
        Assert.NotNull(first.Source);
        Assert.Contains("active", first.Source.SatisfiedPredicates);

        if (first.Source.SatisfiedPredicates is ICollection<string> mutable)
        {
            try
            {
                mutable.Clear();
            }
            catch (NotSupportedException)
            {
            }
        }

        var second = input.SourceAuthority.ResolveCanonicalBinding(
            root.ExpectedSourceKey,
            root.ExpectedTargetKey.Kind);
        Assert.True(second.Success);
        Assert.NotNull(second.Source);
        Assert.Equal(fingerprint, input.SourceAuthority.Fingerprint);
        Assert.Single(second.Source.SatisfiedPredicates);
        Assert.Contains("active", second.Source.SatisfiedPredicates);
        Assert.True(WoundAcceptedTurnPlannerCore.SameTurnWoundAuthorityAgrees(
            prepared,
            input.SourceAuthority));
    }

    [Fact]
    public void SameTurnWoundAuthorityAgrees_RejectsMissingEmptySourceGroup()
    {
        var prepared = AssertPrepared(WoundAcceptedTurnPlanner.Prepare(
            CreateInput(1, CandidateShape.NoMechanics)));
        var batch = Assert.Single(prepared.EffectOperationBatches);
        var entryOnlyAuthority = EffectSourceAuthority.Build(
            new EffectSourceAuthorityInput(
                Array.Empty<EffectSourceExport>(),
                new[]
                {
                    new EffectSourceExport(
                        batch.SourceExport.Realm,
                        batch.SourceExport.Kind,
                        batch.SourceExport.SourceId,
                        new JsonArray(),
                        Materializable: false,
                        Active: true,
                        SameTurn: true,
                        SourceRef: batch.SourceExport.SourceRef)
                },
                new HashSet<string>(StringComparer.Ordinal)));

        Assert.Empty(entryOnlyAuthority.SnapshotSameTurnWoundEntries());
        Assert.Empty(entryOnlyAuthority.SnapshotWoundGroupAuthorities());
        Assert.False(WoundAcceptedTurnPlannerCore.SameTurnWoundAuthorityAgrees(
            prepared,
            entryOnlyAuthority));
    }

    [Fact]
    public void EffectStage_RejectsDerivedCreatedEventCollidingWithAcceptedAuthority()
    {
        var input = CreateInput();
        var causalEvent = Assert.Single(input.Binding.AcceptedEvents);
        var collidedCreatedEvent = WoundEffectOperationEventRef.Create(
            causalEvent.EventRef,
            mechanicsOrdinal: 1,
            operationOrdinal: 1,
            operationKind: "apply");
        var acceptedEvents = input.Binding.AcceptedEvents
            .Append(new WoundAcceptedEventAuthority(
                collidedCreatedEvent,
                "accepted_collision_probe",
                "authority_collision_probe",
                Fingerprint("accepted-event:collision-probe")))
            .ToArray();
        input = input with
        {
            Binding = input.Binding with
            {
                AcceptedEvents = acceptedEvents,
                AcceptedEventsFingerprint =
                    ComputeExpectedAcceptedEventSetFingerprint(acceptedEvents)
            }
        };
        var prepared = AssertPrepared(WoundAcceptedTurnPlanner.Prepare(input));
        var root = Assert.Single(
            Assert.Single(prepared.EffectOperationBatches).RootApplications);
        Assert.Equal(
            collidedCreatedEvent,
            WoundEffectOperationEventRef.Create(
                root.CausalEventRef,
                root.MechanicsOrdinal,
                root.OperationOrdinal,
                root.OperationKind));
        var effectInput = CreateEffectInput(prepared);

        var wrapperFactory = new CountingEffectIdentityFactory();
        var wrapper = WoundEffectBatchPlanner.Build(
            prepared,
            effectInput,
            wrapperFactory);
        Assert.False(wrapper.Success);
        Assert.Null(wrapper.Plan);
        Assert.Contains(wrapper.Issues, issue => string.Equals(
            issue.Code,
            "wound_plan_effect_handoff_invalid",
            StringComparison.Ordinal));
        Assert.Equal(0, wrapperFactory.EffectCalls);
        Assert.Equal(0, wrapperFactory.TransitionCalls);

        var directFactory = new CountingEffectIdentityFactory();
        var direct = EffectAcceptedTurnPlanner.BuildWoundBatch(
            effectInput,
            prepared,
            directFactory);
        Assert.False(direct.Success);
        Assert.Null(direct.Plan);
        Assert.Contains(direct.Issues, issue => string.Equals(
            issue.Code,
            "wound_plan_effect_handoff_invalid",
            StringComparison.Ordinal));
        Assert.Equal(0, directFactory.EffectCalls);
        Assert.Equal(0, directFactory.TransitionCalls);
    }

    [Fact]
    public void EffectResultCandidate_RejectsChangedPreparedPayloadUnderOldSealWithoutAllocation()
    {
        var input = CreateInput();
        var before = CapturePreTurn(input);
        var oldPrepared = AssertPrepared(WoundAcceptedTurnPlanner.Prepare(
            input,
            new RecordingWoundIdentityAllocator("old_prepared_payload")));
        var changedPrepared = AssertPrepared(WoundAcceptedTurnPlanner.Prepare(
            input,
            new RecordingWoundIdentityAllocator("changed_prepared_payload")));
        Assert.Equal(oldPrepared.InputFingerprint, changedPrepared.InputFingerprint);
        var oldBatch = Assert.Single(oldPrepared.EffectOperationBatches);
        var changedBatch = Assert.Single(changedPrepared.EffectOperationBatches);
        Assert.NotEqual(oldBatch.SourceExport.SourceId, changedBatch.SourceExport.SourceId);
        Assert.NotEqual(
            oldBatch.SourceExportFingerprint,
            changedBatch.SourceExportFingerprint);
        Assert.NotEqual(
            Assert.Single(oldBatch.RootApplications).ExpectedMaterializationFingerprint,
            Assert.Single(changedBatch.RootApplications)
                .ExpectedMaterializationFingerprint);

        var factory = new CountingEffectIdentityFactory();
        var stage = BuildEffectStage(changedPrepared, factory);
        var accepted = AssertEffectPlan(stage.Result);
        var effectCalls = factory.EffectCalls;
        var transitionCalls = factory.TransitionCalls;

        var result = WoundEffectBatchAcceptedPlan.AcceptCandidate(
            changedPrepared,
            accepted.EffectInput,
            accepted.EffectPlan,
            accepted.ApplicationResults,
            accepted.TerminationResults,
            oldPrepared.WoundPreparationFingerprint,
            accepted.EffectInputFingerprint,
            accepted.EffectAcceptedTurnPlanFingerprint);

        Assert.False(result.Success);
        Assert.Null(result.Plan);
        var issue = Assert.Single(result.Issues);
        Assert.Equal("wound_plan_prepared_seal_mismatch", issue.Code);
        Assert.Equal(effectCalls, factory.EffectCalls);
        Assert.Equal(transitionCalls, factory.TransitionCalls);
        AssertPreTurnUnchanged(input, before);
    }

    [Fact]
    public void EffectResultCandidate_RejectsChangedEffectInputUnderOldSealWithoutAllocation()
    {
        var input = CreateInput();
        var before = CapturePreTurn(input);
        var prepared = AssertPrepared(WoundAcceptedTurnPlanner.Prepare(input));
        var factory = new CountingEffectIdentityFactory();
        var stage = BuildEffectStage(prepared, factory);
        var accepted = AssertEffectPlan(stage.Result);
        var effectCalls = factory.EffectCalls;
        var transitionCalls = factory.TransitionCalls;
        var changedInput = CreateEffectInput(
            prepared,
            includeIndependentEffect: true);
        Assert.NotEqual(
            accepted.EffectInput.RawCommands.ToJsonString(),
            changedInput.RawCommands.ToJsonString());

        var result = WoundEffectBatchAcceptedPlan.AcceptCandidate(
            prepared,
            changedInput,
            accepted.EffectPlan,
            accepted.ApplicationResults,
            accepted.TerminationResults,
            accepted.WoundPreparationFingerprint,
            accepted.EffectInputFingerprint,
            accepted.EffectAcceptedTurnPlanFingerprint);

        Assert.False(result.Success);
        Assert.Null(result.Plan);
        var issue = Assert.Single(result.Issues);
        Assert.Equal("wound_plan_effect_handoff_invalid", issue.Code);
        Assert.Equal(effectCalls, factory.EffectCalls);
        Assert.Equal(transitionCalls, factory.TransitionCalls);
        AssertPreTurnUnchanged(input, before);
    }

    [Theory]
    [InlineData("source_same_turn")]
    [InlineData("source_ref")]
    [InlineData("source_grant")]
    [InlineData("target_same_turn")]
    [InlineData("target_ref")]
    public void EffectInputSeal_BindsFullRoutingAuthorityHiddenByCanonicalFingerprint(
        string mutation)
    {
        var prepared = AssertPrepared(WoundAcceptedTurnPlanner.Prepare(CreateInput()));
        var stage = BuildEffectStage(
            prepared,
            new ScriptedEffectIdentityFactory("input_authority"));
        var accepted = AssertEffectPlan(stage.Result);
        var authorityInputs = CreateEffectAuthorityDriftPair(
            accepted.EffectInput,
            prepared,
            mutation);

        Assert.Empty(authorityInputs.Before.SourceAuthority.Issues);
        Assert.Empty(authorityInputs.After.SourceAuthority.Issues);
        Assert.Empty(authorityInputs.Before.TargetAuthority.Issues);
        Assert.Empty(authorityInputs.After.TargetAuthority.Issues);
        Assert.Equal(
            authorityInputs.Before.SourceAuthority.CanonicalFingerprint,
            authorityInputs.After.SourceAuthority.CanonicalFingerprint);
        Assert.Equal(
            authorityInputs.Before.TargetAuthority.CanonicalFingerprint,
            authorityInputs.After.TargetAuthority.CanonicalFingerprint);
        Assert.True(
            !string.Equals(
                authorityInputs.Before.SourceAuthority.Fingerprint,
                authorityInputs.After.SourceAuthority.Fingerprint,
                StringComparison.Ordinal) ||
            !string.Equals(
                authorityInputs.Before.TargetAuthority.Fingerprint,
                authorityInputs.After.TargetAuthority.Fingerprint,
                StringComparison.Ordinal));
        var beforeSeal = WoundAcceptedTurnFingerprints.ComputeEffectInput(
            prepared,
            authorityInputs.Before);
        var afterSeal = WoundAcceptedTurnFingerprints.ComputeEffectInput(
            prepared,
            authorityInputs.After);
        Assert.NotEqual(beforeSeal, afterSeal);

        var result = WoundEffectBatchAcceptedPlan.AcceptCandidate(
            prepared,
            authorityInputs.After,
            accepted.EffectPlan,
            accepted.ApplicationResults,
            accepted.TerminationResults,
            accepted.WoundPreparationFingerprint,
            beforeSeal,
            accepted.EffectAcceptedTurnPlanFingerprint);

        Assert.False(result.Success);
        Assert.Null(result.Plan);
        Assert.Equal(
            "wound_plan_effect_handoff_invalid",
            Assert.Single(result.Issues).Code);
    }

    [Theory]
    [InlineData("source_same_turn")]
    [InlineData("source_ref")]
    [InlineData("source_grant")]
    [InlineData("target_same_turn")]
    [InlineData("target_ref")]
    public void EffectPlanSeal_BindsFullRoutingAuthorityObjectUnderOldScalarSeals(
        string mutation)
    {
        var prepared = AssertPrepared(WoundAcceptedTurnPlanner.Prepare(CreateInput()));
        var stage = BuildEffectStage(
            prepared,
            new ScriptedEffectIdentityFactory("plan_authority"));
        var accepted = AssertEffectPlan(stage.Result);
        var authorityInputs = CreateEffectAuthorityDriftPair(
            accepted.EffectInput,
            prepared,
            mutation);
        var beforePlan = CloneEffectPlanWithAuthorities(
            accepted.EffectPlan,
            authorityInputs.Before.SourceAuthority,
            authorityInputs.Before.TargetAuthority);
        var changedPlan = CloneEffectPlanWithAuthorities(
            accepted.EffectPlan,
            authorityInputs.After.SourceAuthority,
            authorityInputs.After.TargetAuthority);

        Assert.Equal(
            beforePlan.SourceAuthorityFingerprint,
            changedPlan.SourceAuthorityFingerprint);
        Assert.Equal(
            beforePlan.TargetAuthorityFingerprint,
            changedPlan.TargetAuthorityFingerprint);
        Assert.Equal(
            beforePlan.SourceAuthority.CanonicalFingerprint,
            changedPlan.SourceAuthority.CanonicalFingerprint);
        Assert.Equal(
            beforePlan.TargetAuthority.CanonicalFingerprint,
            changedPlan.TargetAuthority.CanonicalFingerprint);
        var beforeInputSeal = WoundAcceptedTurnFingerprints.ComputeEffectInput(
            prepared,
            authorityInputs.Before);
        var beforePlanSeal = WoundAcceptedTurnFingerprints.ComputeEffectPlan(
            prepared,
            authorityInputs.Before,
            beforePlan,
            accepted.ApplicationResults,
            accepted.TerminationResults);
        var afterPlanSeal = WoundAcceptedTurnFingerprints.ComputeEffectPlan(
            prepared,
            authorityInputs.Before,
            changedPlan,
            accepted.ApplicationResults,
            accepted.TerminationResults);
        Assert.NotEqual(beforePlanSeal, afterPlanSeal);

        var result = WoundEffectBatchAcceptedPlan.AcceptCandidate(
            prepared,
            authorityInputs.Before,
            changedPlan,
            accepted.ApplicationResults,
            accepted.TerminationResults,
            accepted.WoundPreparationFingerprint,
            beforeInputSeal,
            beforePlanSeal);

        Assert.False(result.Success);
        Assert.Null(result.Plan);
        Assert.Equal(
            "wound_plan_effect_handoff_invalid",
            Assert.Single(result.Issues).Code);
    }

    [Theory]
    [InlineData("application")]
    [InlineData("termination")]
    public void EffectPlanSeal_BindsExactApplicationAndTerminationResultMaps(
        string mutation)
    {
        var prepared = AssertPrepared(WoundAcceptedTurnPlanner.Prepare(CreateInput()));
        var accepted = AssertEffectPlan(BuildEffectStage(
            prepared,
            new ScriptedEffectIdentityFactory("result_map_seal")).Result);
        var application = Assert.Single(accepted.ApplicationResults);
        IReadOnlyList<EffectAcceptedApplicationResult> applications =
            accepted.ApplicationResults;
        IReadOnlyList<EffectAcceptedTerminationResult> terminations =
            accepted.TerminationResults;

        if (mutation == "application")
        {
            applications = new[]
            {
                application with
                {
                    CreatedEventRef = application.CreatedEventRef + "_changed"
                }
            };
        }
        else
        {
            terminations = new[]
            {
                new EffectAcceptedTerminationResult(
                    "operation_result_map_seal",
                    "terminated",
                    application.EffectId,
                    "transition_result_map_seal",
                    "turn_42:wound:result_map_seal",
                    application.CausalEventRef,
                    application.SourceKey,
                    application.TargetKey,
                    application.CarrierCoordinate)
            };
        }

        var underOldSeal = WoundEffectBatchAcceptedPlan.AcceptCandidate(
            prepared,
            accepted.EffectInput,
            accepted.EffectPlan,
            applications,
            terminations,
            accepted.WoundPreparationFingerprint,
            accepted.EffectInputFingerprint,
            accepted.EffectAcceptedTurnPlanFingerprint);
        Assert.False(underOldSeal.Success);
        Assert.Null(underOldSeal.Plan);
        Assert.Equal(
            "wound_plan_effect_handoff_invalid",
            Assert.Single(underOldSeal.Issues).Code);

        var resealed = WoundEffectBatchAcceptedPlan.Create(
            prepared,
            accepted.EffectInput,
            accepted.EffectPlan,
            applications,
            terminations);
        Assert.NotEqual(
            accepted.EffectAcceptedTurnPlanFingerprint,
            resealed.EffectAcceptedTurnPlanFingerprint);
    }

    [Fact]
    public void EffectStage_RejectsDivergentCreatedEffectMaterializationWithLayeredFailure()
    {
        var input = CreateInput();
        var before = CapturePreTurn(input);
        var prepared = AssertPrepared(WoundAcceptedTurnPlanner.Prepare(input));
        var stage = BuildEffectStage(
            prepared,
            new ScriptedEffectIdentityFactory("divergent_effect_stage"));
        var accepted = AssertEffectPlan(stage.Result);
        var application = Assert.Single(accepted.ApplicationResults);
        var divergentEffectPlan = CloneEffectPlanWithMutatedActiveEffect(
            accepted.EffectPlan,
            application.EffectId,
            "component_divergent_effect_stage");
        AssertActiveEffectComponentsDiffer(
            accepted.EffectPlan,
            divergentEffectPlan,
            application.EffectId);

        var result = WoundEffectBatchPlanner.AcceptEffectResult(
            prepared,
            accepted.EffectInput,
            new EffectAcceptedTurnPlanningResult(
                divergentEffectPlan,
                Array.Empty<ValidationIssue>()));

        Assert.False(result.Success);
        Assert.Null(result.Plan);
        Assert.Equal(2, result.Issues.Count);
        Assert.Equal("wound_plan_effect_result_agreement_mismatch", result.Issues[0].Code);
        Assert.Equal("wound_plan_effect_stage_failed", result.Issues[1].Code);
        AssertPreTurnUnchanged(input, before);
    }

    [Fact]
    public void EffectStage_RejectsCreatedEffectSourceThatDiffersFromCarriedAuthority()
    {
        var input = CreateInput();
        var before = CapturePreTurn(input);
        var prepared = AssertPrepared(WoundAcceptedTurnPlanner.Prepare(input));
        var stage = BuildEffectStage(
            prepared,
            new ScriptedEffectIdentityFactory("divergent_effect_source"));
        var accepted = AssertEffectPlan(stage.Result);
        var expectedApplication = Assert.Single(
            Assert.Single(prepared.EffectOperationBatches).RootApplications);
        var application = Assert.Single(accepted.ApplicationResults);
        var divergentEffectPlan = CloneEffectPlanWithMutatedActiveEffectSource(
            accepted.EffectPlan,
            application.EffectId,
            "wound_divergent_created_source");
        AssertActiveEffectSourcesDiffer(
            accepted.EffectPlan,
            divergentEffectPlan,
            application.EffectId);
        var divergentActiveEffect = FindActiveEffect(
            divergentEffectPlan,
            application.EffectId);
        var divergentSourceKey = ReadActiveEffectSourceKey(divergentActiveEffect);
        Assert.Equal(expectedApplication.ExpectedSourceKey, application.SourceKey);
        Assert.NotEqual(expectedApplication.ExpectedSourceKey, divergentSourceKey);
        Assert.NotEqual(application.SourceKey, divergentSourceKey);
        var divergentFingerprint = ComputeExpectedMaterializationFingerprint(
            divergentSourceKey,
            divergentActiveEffect["schemaVersion"]!.GetValue<int>(),
            expectedApplication.Parameters,
            divergentActiveEffect["components"]!.AsArray());
        Assert.NotEqual(
            application.Materialization.MaterializationFingerprint,
            divergentFingerprint);

        var result = WoundEffectBatchPlanner.AcceptEffectResult(
            prepared,
            accepted.EffectInput,
            new EffectAcceptedTurnPlanningResult(
                divergentEffectPlan,
                Array.Empty<ValidationIssue>()));

        Assert.False(result.Success);
        Assert.Null(result.Plan);
        Assert.Equal(2, result.Issues.Count);
        Assert.Equal("wound_plan_effect_result_agreement_mismatch", result.Issues[0].Code);
        Assert.Equal("wound_plan_effect_stage_failed", result.Issues[1].Code);
        AssertPreTurnUnchanged(input, before);
    }

    [Fact]
    public void EffectResultCandidate_RejectsChangedAfterImageUnderOldPlanFingerprint()
    {
        var input = CreateInput();
        var before = CapturePreTurn(input);
        var prepared = AssertPrepared(WoundAcceptedTurnPlanner.Prepare(input));
        var stage = BuildEffectStage(
            prepared,
            new ScriptedEffectIdentityFactory("stale_effect_plan_seal"));
        var accepted = AssertEffectPlan(stage.Result);
        var application = Assert.Single(accepted.ApplicationResults);
        var divergentEffectPlan = CloneEffectPlanWithMutatedActiveEffect(
            accepted.EffectPlan,
            application.EffectId,
            "component_stale_effect_plan_seal");
        AssertActiveEffectComponentsDiffer(
            accepted.EffectPlan,
            divergentEffectPlan,
            application.EffectId);

        var result = WoundEffectBatchAcceptedPlan.AcceptCandidate(
            prepared,
            accepted.EffectInput,
            divergentEffectPlan,
            accepted.ApplicationResults,
            accepted.TerminationResults,
            accepted.WoundPreparationFingerprint,
            accepted.EffectInputFingerprint,
            accepted.EffectAcceptedTurnPlanFingerprint);

        Assert.False(result.Success);
        Assert.Null(result.Plan);
        var issue = Assert.Single(result.Issues);
        Assert.Equal("wound_plan_effect_handoff_invalid", issue.Code);
        AssertPreTurnUnchanged(input, before);
    }

    [Fact]
    public void Finalize_RejectsResealedPlanWhoseCreatedAfterImageDisagreesWithResult()
    {
        var input = CreateInput();
        var before = CapturePreTurn(input);
        var prepared = AssertPrepared(WoundAcceptedTurnPlanner.Prepare(input));
        var stage = BuildEffectStage(
            prepared,
            new ScriptedEffectIdentityFactory("resealed_effect_plan"));
        var accepted = AssertEffectPlan(stage.Result);
        var expectedApplication = Assert.Single(
            Assert.Single(prepared.EffectOperationBatches).RootApplications);
        var application = Assert.Single(accepted.ApplicationResults);
        var divergentEffectPlan = CloneEffectPlanWithMutatedActiveEffectSource(
            accepted.EffectPlan,
            application.EffectId,
            "wound_resealed_created_source");
        AssertActiveEffectSourcesDiffer(
            accepted.EffectPlan,
            divergentEffectPlan,
            application.EffectId);
        var divergentActiveEffect = FindActiveEffect(
            divergentEffectPlan,
            application.EffectId);
        var divergentSourceKey = ReadActiveEffectSourceKey(divergentActiveEffect);
        Assert.Equal(expectedApplication.ExpectedSourceKey, application.SourceKey);
        Assert.NotEqual(expectedApplication.ExpectedSourceKey, divergentSourceKey);
        Assert.NotEqual(application.SourceKey, divergentSourceKey);
        var divergentFingerprint = ComputeExpectedMaterializationFingerprint(
            divergentSourceKey,
            divergentActiveEffect["schemaVersion"]!.GetValue<int>(),
            expectedApplication.Parameters,
            divergentActiveEffect["components"]!.AsArray());
        Assert.NotEqual(
            application.Materialization.MaterializationFingerprint,
            divergentFingerprint);

        var resealed = WoundEffectBatchAcceptedPlan.Create(
            prepared,
            accepted.EffectInput,
            divergentEffectPlan,
            accepted.ApplicationResults,
            accepted.TerminationResults);
        Assert.NotEqual(
            accepted.EffectAcceptedTurnPlanFingerprint,
            resealed.EffectAcceptedTurnPlanFingerprint);
        Assert.Equal(
            application.Materialization.MaterializationFingerprint,
            Assert.Single(resealed.ApplicationResults)
                .Materialization.MaterializationFingerprint);

        var result = WoundAcceptedTurnPlanner.Finalize(
            prepared,
            new WoundEffectBatchPlanningResult(
                resealed,
                Array.Empty<ValidationIssue>()));

        AssertInvalidFinalization(result);
        var issue = Assert.Single(result.Issues);
        Assert.Equal("wound_plan_effect_result_agreement_mismatch", issue.Code);
        AssertPreTurnUnchanged(input, before);
    }

    [Fact]
    public void Finalize_RejectsResealedPublicationCarrierThatDisagreesWithActiveEffect()
    {
        var input = CreateInput();
        var before = CapturePreTurn(input);
        var prepared = AssertPrepared(WoundAcceptedTurnPlanner.Prepare(input));
        var stage = BuildEffectStage(
            prepared,
            new ScriptedEffectIdentityFactory("publication_carrier"));
        var accepted = AssertEffectPlan(stage.Result);
        var application = Assert.Single(accepted.ApplicationResults);
        var divergentPlan = CloneEffectPlanWithCarrierAfterImageMutation(
            accepted.EffectPlan,
            application.EffectId,
            activeEffect =>
            {
                var component = Assert.IsType<JsonObject>(
                    Assert.Single(activeEffect["components"]!.AsArray()));
                component["componentId"] = "component_publication_only_drift";
            });
        Assert.Equal(
            FindActiveEffect(accepted.EffectPlan, application.EffectId).ToJsonString(),
            FindActiveEffect(divergentPlan, application.EffectId).ToJsonString());
        Assert.NotEqual(
            FindPlayerCarrierEffect(accepted.EffectPlan, application.EffectId)
                .ToJsonString(),
            FindPlayerCarrierEffect(divergentPlan, application.EffectId)
                .ToJsonString());
        var resealed = WoundEffectBatchAcceptedPlan.Create(
            prepared,
            accepted.EffectInput,
            divergentPlan,
            accepted.ApplicationResults,
            accepted.TerminationResults);

        var result = WoundAcceptedTurnPlanner.Finalize(
            prepared,
            new WoundEffectBatchPlanningResult(
                resealed,
                Array.Empty<ValidationIssue>()));

        AssertInvalidFinalization(result);
        Assert.Equal(
            "wound_plan_effect_result_agreement_mismatch",
            Assert.Single(result.Issues).Code);
        AssertPreTurnUnchanged(input, before);
    }

    [Theory]
    [InlineData("owner_collection")]
    [InlineData("stack_coordinate")]
    [InlineData("created_turn")]
    [InlineData("transition_turn")]
    [InlineData("transition_sources")]
    [InlineData("transition_receipt")]
    public void Finalize_RejectsResealedPlanWithDivergentIdentityAuthority(
        string mutation)
    {
        var input = CreateInput();
        var before = CapturePreTurn(input);
        var prepared = AssertPrepared(WoundAcceptedTurnPlanner.Prepare(input));
        var stage = BuildEffectStage(
            prepared,
            new ScriptedEffectIdentityFactory("identity_authority"));
        var accepted = AssertEffectPlan(stage.Result);
        var application = Assert.Single(accepted.ApplicationResults);
        var divergentPlan = CloneEffectPlanWithActiveEffectMutation(
            accepted.EffectPlan,
            application.EffectId,
            static _ => { },
            identityIndex =>
            {
                var identity = Assert.Single(identityIndex["entries"]!
                    .AsArray().OfType<JsonObject>(), entry => string.Equals(
                        entry["effectId"]?.GetValue<string>(),
                        application.EffectId,
                        StringComparison.Ordinal));
                var transition = Assert.IsType<JsonObject>(
                    Assert.Single(identity["transitions"]!.AsArray()));
                switch (mutation)
                {
                    case "owner_collection":
                        identity["owner"]!["collection"] = "tampered_collection";
                        break;
                    case "stack_coordinate":
                        identity["stackCoordinate"]!["stackKey"] =
                            "tampered_exact_stack";
                        break;
                    case "created_turn":
                        identity["createdAtTurn"] = Turn - 1;
                        break;
                    case "transition_turn":
                        transition["turn"] = Turn + 1;
                        break;
                    case "transition_sources":
                        transition["sourceEffectIds"] =
                            new JsonArray("effect_foreign_source");
                        break;
                    case "transition_receipt":
                        transition["receiptId"] = "receipt_foreign_create";
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(
                            nameof(mutation),
                            mutation,
                            null);
                }
            });
        var resealed = WoundEffectBatchAcceptedPlan.Create(
            prepared,
            accepted.EffectInput,
            divergentPlan,
            accepted.ApplicationResults,
            accepted.TerminationResults);

        var result = WoundAcceptedTurnPlanner.Finalize(
            prepared,
            new WoundEffectBatchPlanningResult(
                resealed,
                Array.Empty<ValidationIssue>()));

        AssertInvalidFinalization(result);
        Assert.Equal(
            "wound_plan_effect_result_agreement_mismatch",
            Assert.Single(result.Issues).Code);
        AssertPreTurnUnchanged(input, before);
    }

    [Fact]
    public void Finalize_RejectsResealedCreateTransitionOutsideAllocatorEvidence()
    {
        const string forgedTransitionId = "effect_transition_forged_unallocated";
        var input = CreateInput();
        var before = CapturePreTurn(input);
        var prepared = AssertPrepared(WoundAcceptedTurnPlanner.Prepare(input));
        var stage = BuildEffectStage(
            prepared,
            new ScriptedEffectIdentityFactory("transition_allocation"));
        var accepted = AssertEffectPlan(stage.Result);
        var application = Assert.Single(accepted.ApplicationResults);
        Assert.DoesNotContain(
            forgedTransitionId,
            accepted.EffectPlan.AllocatedTransitionIds);
        var divergentPlan = CloneEffectPlanWithCreateTransitionId(
            accepted.EffectPlan,
            application.EffectId,
            forgedTransitionId);
        var divergentApplications = new[]
        {
            application with { CreateTransitionId = forgedTransitionId }
        };
        var resealed = WoundEffectBatchAcceptedPlan.Create(
            prepared,
            accepted.EffectInput,
            divergentPlan,
            divergentApplications,
            accepted.TerminationResults);

        var result = WoundAcceptedTurnPlanner.Finalize(
            prepared,
            new WoundEffectBatchPlanningResult(
                resealed,
                Array.Empty<ValidationIssue>()));

        AssertInvalidFinalization(result);
        Assert.Equal(
            "wound_plan_effect_result_agreement_mismatch",
            Assert.Single(result.Issues).Code);
        AssertPreTurnUnchanged(input, before);
    }

    [Fact]
    public void LocalStageSeals_AreNonInterchangeableAndBindTheirExactProducerPayloads()
    {
        var input = CreateInput();
        var preparedA = AssertPrepared(WoundAcceptedTurnPlanner.Prepare(
            input,
            new RecordingWoundIdentityAllocator("allocation_a")));
        var preparedB = AssertPrepared(WoundAcceptedTurnPlanner.Prepare(
            input,
            new RecordingWoundIdentityAllocator("allocation_b")));

        Assert.Equal(preparedA.InputFingerprint, preparedB.InputFingerprint);
        Assert.NotEqual(
            Assert.Single(preparedA.EffectOperationBatches).SourceExportFingerprint,
            Assert.Single(preparedB.EffectOperationBatches).SourceExportFingerprint);
        Assert.NotEqual(
            preparedA.WoundPreparationFingerprint,
            preparedB.WoundPreparationFingerprint);
        Assert.Equal(
            ComputeExpectedWoundPreparationFingerprint(preparedA),
            preparedA.WoundPreparationFingerprint);
        Assert.Equal(
            ComputeExpectedWoundPreparationFingerprint(preparedB),
            preparedB.WoundPreparationFingerprint);

        var effectA = BuildEffectStage(
            preparedA,
            new ScriptedEffectIdentityFactory("effect_a"));
        var effectB = BuildEffectStage(
            preparedA,
            new ScriptedEffectIdentityFactory("effect_b"));
        var effectPlanA = AssertEffectPlan(effectA.Result);
        var effectPlanB = AssertEffectPlan(effectB.Result);
        Assert.Equal(effectPlanA.EffectInputFingerprint, effectPlanB.EffectInputFingerprint);
        Assert.NotEqual(
            effectPlanA.EffectAcceptedTurnPlanFingerprint,
            effectPlanB.EffectAcceptedTurnPlanFingerprint);

        var finalA = AssertFinalized(WoundAcceptedTurnPlanner.Finalize(
            preparedA,
            effectA.Result));
        var finalB = AssertFinalized(WoundAcceptedTurnPlanner.Finalize(
            preparedA,
            effectB.Result));
        Assert.NotEqual(finalA.WoundFinalPlanFingerprint, finalB.WoundFinalPlanFingerprint);

        var seals = new[]
        {
            Assert.Single(preparedA.EffectOperationBatches).SourceExportFingerprint,
            preparedA.WoundPreparationFingerprint,
            effectPlanA.EffectInputFingerprint,
            effectPlanA.EffectAcceptedTurnPlanFingerprint,
            finalA.WoundFinalPlanFingerprint
        };
        Assert.Equal(seals.Length, seals.Distinct(StringComparer.Ordinal).Count());

        var resultPreparedForA = BuildEffectStage(
            preparedA,
            new ScriptedEffectIdentityFactory("cross_allocation"));
        var cross = WoundAcceptedTurnPlanner.Finalize(preparedB, resultPreparedForA.Result);
        AssertInvalidFinalization(cross);
        var issue = Assert.Single(cross.Issues);
        Assert.Equal("wound_plan_prepared_seal_mismatch", issue.Code);

        var staleInput = CreateInput();
        staleInput = staleInput with
        {
            Binding = staleInput.Binding with { RequestId = "request_stale" },
            Opportunities = staleInput.Opportunities.Select(static value =>
                value with { RequestId = "request_stale" }).ToArray()
        };
        var stalePrepared = AssertPrepared(WoundAcceptedTurnPlanner.Prepare(
            staleInput,
            new RecordingWoundIdentityAllocator("stale_binding")));
        var staleStage = BuildEffectStage(
            stalePrepared,
            new ScriptedEffectIdentityFactory("stale_binding"));
        var staleCross = WoundAcceptedTurnPlanner.Finalize(preparedA, staleStage.Result);
        AssertInvalidFinalization(staleCross);
        Assert.Equal(
            "wound_plan_prepared_seal_mismatch",
            Assert.Single(staleCross.Issues).Code);
    }

    [Theory]
    [InlineData("transition")]
    [InlineData("baseline")]
    public void Finalize_RejectsChangedPrivatePreparedAuthorityUnderUnchangedPublicSeal(
        string mutation)
    {
        var prepared = AssertPrepared(WoundAcceptedTurnPlanner.Prepare(
            CreateInput(1, CandidateShape.NoMechanics)));
        var stage = BuildEffectStage(
            prepared,
            new ScriptedEffectIdentityFactory("private_authority"));

        var detachedHistory = prepared.BaselineAuthority.PreTurnHistory;
        var storedHistory = prepared.BaselineAuthority.PreTurnHistory.ToJsonString();
        detachedHistory["nextOrdinal"] = 999;
        Assert.Equal(
            storedHistory,
            prepared.BaselineAuthority.PreTurnHistory.ToJsonString());

        var tampered = RewrapPreparedWithPrivateAuthorityMutation(prepared, mutation);
        Assert.Equal(
            prepared.WoundPreparationFingerprint,
            tampered.WoundPreparationFingerprint);

        var result = WoundAcceptedTurnPlanner.Finalize(tampered, stage.Result);

        AssertInvalidFinalization(result);
        Assert.Equal(
            "wound_plan_prepared_seal_mismatch",
            Assert.Single(result.Issues).Code);
    }

    [Fact]
    public void PreparedPrivateAuthoritySeals_UseExactDomainSeparatedUtf8Writers()
    {
        const string inputFingerprint = "sha256:prepared_input_проверка";
        var transitionFields = new string?[]
        {
            TransitionAuthorityDomain,
            FingerprintVersion,
            inputFingerprint,
            "draft_wound_authority",
            "wound_authority",
            "opportunity_authority",
            "sha256:opportunity_authority",
            "operation_authority",
            "Духовная рана получена.",
            "4"
        };
        var transitionSeal = WoundAcceptedTurnFingerprints.ComputeTransitionAuthority(
            inputFingerprint,
            "draft_wound_authority",
            "wound_authority",
            "opportunity_authority",
            "sha256:opportunity_authority",
            "operation_authority",
            "Духовная рана получена.",
            4);

        Assert.Equal(LengthPrefixedFingerprint(transitionFields), transitionSeal);
        Assert.NotEqual(
            CharacterLengthPrefixedFingerprint(transitionFields),
            transitionSeal);

        var carriers = CreateAuthorityCarrierMatrix();
        var identity = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["entries"] = new JsonArray("identity_душа")
        };
        var history = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["nextOrdinal"] = 7,
            ["transitions"] = new JsonArray("history_душа")
        };
        var baselineFields = CreateExpectedBaselineAuthorityFields(
            inputFingerprint,
            carriers,
            identity,
            history);
        var baselineSeal = WoundAcceptedTurnFingerprints.ComputeBaselineAuthority(
            inputFingerprint,
            carriers,
            identity,
            history);

        Assert.Equal(LengthPrefixedFingerprint(baselineFields), baselineSeal);
        Assert.NotEqual(transitionSeal, baselineSeal);
        Assert.NotEqual(
            CharacterLengthPrefixedFingerprint(baselineFields),
            baselineSeal);
    }

    [Theory]
    [InlineData("prepared_input")]
    [InlineData("local_wound")]
    [InlineData("permanent_wound")]
    [InlineData("opportunity_id")]
    [InlineData("opportunity_fingerprint")]
    [InlineData("operation_key")]
    [InlineData("readable_summary")]
    [InlineData("maximum_severity")]
    public void TransitionAuthoritySeal_BindsEveryAcceptedField(string mutation)
    {
        var original = ComputeTransitionAuthoritySeal();
        var changed = ComputeTransitionAuthoritySeal(mutation);

        Assert.NotEqual(original, changed);
    }

    [Theory]
    [InlineData("prepared_input")]
    [InlineData("player")]
    [InlineData("npc")]
    [InlineData("enemy")]
    [InlineData("ally")]
    [InlineData("afterlife")]
    [InlineData("identity")]
    [InlineData("history")]
    [InlineData("player_null_vs_empty")]
    public void BaselineAuthoritySeal_BindsEveryRootAndDistinguishesNullFromEmpty(
        string mutation)
    {
        var carriers = CreateAuthorityCarrierMatrix();
        var identity = new JsonObject { ["identity"] = "before" };
        var history = new JsonObject { ["history"] = "before" };
        var original = WoundAcceptedTurnFingerprints.ComputeBaselineAuthority(
            "sha256:baseline_input",
            carriers,
            identity,
            history);

        var changedCarriers = carriers;
        var changedIdentity = identity.DeepClone().AsObject();
        var changedHistory = history.DeepClone().AsObject();
        var changedInput = "sha256:baseline_input";
        switch (mutation)
        {
            case "prepared_input":
                changedInput = "sha256:baseline_input_changed";
                break;
            case "player":
                changedCarriers = changedCarriers with
                {
                    PlayerWounds = new JsonObject { ["root"] = "player_changed" }
                };
                break;
            case "npc":
                changedCarriers = changedCarriers with
                {
                    NpcWounds = new JsonObject { ["root"] = "npc_changed" }
                };
                break;
            case "enemy":
                changedCarriers = changedCarriers with
                {
                    EnemyCombatants = new JsonObject { ["root"] = "enemy_changed" }
                };
                break;
            case "ally":
                changedCarriers = changedCarriers with
                {
                    AllyCombatants = new JsonObject { ["root"] = "ally_changed" }
                };
                break;
            case "afterlife":
                changedCarriers = changedCarriers with
                {
                    AfterlifeProfiles = new JsonObject { ["root"] = "afterlife_changed" }
                };
                break;
            case "identity":
                changedIdentity["identity"] = "changed";
                break;
            case "history":
                changedHistory["history"] = "changed";
                break;
            case "player_null_vs_empty":
            {
                var nullCarriers = changedCarriers with { PlayerWounds = null };
                var nullSeal = WoundAcceptedTurnFingerprints.ComputeBaselineAuthority(
                    changedInput,
                    nullCarriers,
                    changedIdentity,
                    changedHistory);
                var emptyCarriers = nullCarriers with { PlayerWounds = new JsonObject() };
                var emptySeal = WoundAcceptedTurnFingerprints.ComputeBaselineAuthority(
                    changedInput,
                    emptyCarriers,
                    changedIdentity,
                    changedHistory);
                Assert.NotEqual(nullSeal, emptySeal);
                return;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null);
        }

        var changed = WoundAcceptedTurnFingerprints.ComputeBaselineAuthority(
            changedInput,
            changedCarriers,
            changedIdentity,
            changedHistory);
        Assert.NotEqual(original, changed);
    }

    [Fact]
    public void BaselineAuthority_DeepDetachesCarrierIdentityAndHistoryInputsAndGetters()
    {
        var carriers = CreateAuthorityCarrierMatrix();
        var identity = new JsonObject { ["identity"] = "before" };
        var history = new JsonObject { ["history"] = "before" };
        var seal = WoundAcceptedTurnFingerprints.ComputeBaselineAuthority(
            "sha256:baseline_detachment",
            carriers,
            identity,
            history);
        var authority = new WoundPreparedBaselineAuthority(
            "sha256:baseline_detachment",
            carriers,
            identity,
            history,
            seal);
        var expectedCarriers = SerializeWoundCarriers(authority.PreTurnCarriers);
        var expectedIdentity = authority.PreTurnIdentityIndex.ToJsonString();
        var expectedHistory = authority.PreTurnHistory.ToJsonString();

        carriers.PlayerWounds!["mutatedInput"] = true;
        identity["mutatedInput"] = true;
        history["mutatedInput"] = true;
        var detachedCarriers = authority.PreTurnCarriers;
        var detachedIdentity = authority.PreTurnIdentityIndex;
        var detachedHistory = authority.PreTurnHistory;
        detachedCarriers.NpcWounds!["mutatedGetter"] = true;
        detachedIdentity["mutatedGetter"] = true;
        detachedHistory["mutatedGetter"] = true;

        Assert.Equal(expectedCarriers, SerializeWoundCarriers(authority.PreTurnCarriers));
        Assert.Equal(expectedIdentity, authority.PreTurnIdentityIndex.ToJsonString());
        Assert.Equal(expectedHistory, authority.PreTurnHistory.ToJsonString());
        Assert.Equal(
            seal,
            WoundAcceptedTurnFingerprints.ComputeBaselineAuthority(
                authority.PreparedInputFingerprint,
                authority.PreTurnCarriers,
                authority.PreTurnIdentityIndex,
                authority.PreTurnHistory));
    }

    [Fact]
    public void Finalize_ExactResultBuildsCanonicalGraphBindingsEntriesHistoryAndIntents()
    {
        var input = CreateInput(1, CandidateShape.OneRootTwoSlots);
        var prepared = AssertPrepared(WoundAcceptedTurnPlanner.Prepare(
            input,
            new RecordingWoundIdentityAllocator("exact")));
        var effectStage = BuildEffectStage(
            prepared,
            new ScriptedEffectIdentityFactory("exact"));
        var acceptedEffect = Assert.Single(AssertEffectPlan(effectStage.Result)
            .ApplicationResults);

        var final = AssertFinalized(WoundAcceptedTurnPlanner.Finalize(
            prepared,
            effectStage.Result));

        Assert.Equal(prepared.BindingFingerprint, final.BindingFingerprint);
        Assert.Equal(prepared.InputFingerprint, final.InputFingerprint);
        Assert.Equal(
            prepared.WoundPreparationFingerprint,
            final.WoundPreparationFingerprint);
        Assert.Equal(
            AssertEffectPlan(effectStage.Result).EffectInputFingerprint,
            final.EffectInputFingerprint);
        Assert.Equal(
            AssertEffectPlan(effectStage.Result).EffectAcceptedTurnPlanFingerprint,
            final.EffectAcceptedTurnPlanFingerprint);
        Assert.NotEmpty(final.WoundFinalPlanFingerprint);

        var contribution = Assert.Single(final.CarrierContributions);
        Assert.Equal(input.Transitions[0].ProposedAfter.Owner, contribution.Owner);
        Assert.NotEmpty(contribution.ExpectedWoundCollectionFingerprint);
        var mutation = Assert.Single(contribution.Mutations);
        Assert.Equal("add", mutation.Operation);
        Assert.Null(mutation.BeforeWound);
        var after = Assert.IsType<WoundMaterializationEnvelope>(mutation.AfterWound);
        var canonical = WoundMaterializationContract.SerializeCanonical(after);
        Assert.DoesNotContain(input.Transitions[0].LocalWoundRef, canonical, StringComparison.Ordinal);
        Assert.DoesNotContain(
            input.Transitions[0].RootApplications[0].LocalApplicationRef,
            canonical,
            StringComparison.Ordinal);
        var root = JsonNode.Parse(canonical)!.AsObject();
        var consequences = root["consequences"]!.AsObject();
        Assert.Equal(2, consequences["slotsUsed"]!.GetValue<int>());
        var entries = consequences["entries"]!.AsArray();
        Assert.Equal(2, entries.Count);
        Assert.All(entries.OfType<JsonObject>(), entry =>
            Assert.Equal(acceptedEffect.EffectId, entry["effectId"]!.GetValue<string>()));
        var owned = consequences["ownedEffectSources"]!.AsObject();
        Assert.Single(owned["definitions"]!.AsArray());
        var binding = Assert.IsType<JsonObject>(
            Assert.Single(owned["rootBindings"]!.AsArray()));
        Assert.Equal(acceptedEffect.EffectId, binding["effectId"]!.GetValue<string>());
        Assert.Equal(acceptedEffect.SourceKey.DefinitionKey,
            binding["definitionKey"]!.GetValue<string>());

        var identityEntry = Assert.Single(final.IdentityIndexAfterImage["entries"]!
            .AsArray().OfType<JsonObject>(), value =>
                string.Equals(
                    value["woundId"]?.GetValue<string>(),
                    after.WoundId,
                    StringComparison.Ordinal));
        Assert.Equal(after.Owner.OwnerKind, identityEntry["ownerKind"]!.GetValue<string>());
        var history = Assert.Single(final.HistoryAfterImage["transitions"]!
            .AsArray().OfType<JsonObject>(), value =>
                string.Equals(
                    value["woundId"]?.GetValue<string>(),
                    after.WoundId,
                    StringComparison.Ordinal));
        Assert.Equal(1, history["ordinal"]!.GetValue<int>());
        Assert.Equal("create", history["kind"]!.GetValue<string>());
        Assert.Equal(Turn, history["turn"]!.GetValue<int>());
        Assert.Equal(input.Binding.AcceptedEvents[0].EventRef,
            history["eventRef"]!.GetValue<string>());
        Assert.Equal(input.Transitions[0].OperationKey,
            history["operationKey"]!.GetValue<string>());
        Assert.Equal(
            input.Opportunities[0].AuthorityFingerprint,
            history["sourceFingerprint"]!.GetValue<string>());

        var effectIntent = Assert.Single(final.TransitionIntents
            .OfType<WoundEffectTransitionIntent>());
        Assert.Equal("apply", effectIntent.Operation);
        Assert.Empty(effectIntent.BeforeEffectIds);
        Assert.Equal(new[] { acceptedEffect.EffectId }, effectIntent.AfterEffectIds);
        Assert.Single(final.TransitionIntents.OfType<WoundCarrierTransitionIntent>());
        Assert.Single(final.TransitionIntents.OfType<WoundTransitionHistoryIntent>());
    }

    [Theory]
    [InlineData("missing", "wound_plan_effect_result_set_mismatch")]
    [InlineData("extra", "wound_plan_effect_result_set_mismatch")]
    [InlineData("duplicate_exact", "wound_plan_effect_result_set_mismatch")]
    [InlineData("duplicate_confusable", "wound_plan_effect_result_set_mismatch")]
    [InlineData("reordered", "wound_plan_effect_result_set_mismatch")]
    [InlineData("effect_id_duplicate", "wound_plan_effect_result_set_mismatch")]
    [InlineData("effect_id_confusable", "wound_plan_effect_result_set_mismatch")]
    [InlineData("cross_wound", "wound_plan_effect_result_agreement_mismatch")]
    [InlineData("stack", "wound_plan_effect_result_disposition_mismatch")]
    [InlineData("refresh", "wound_plan_effect_result_disposition_mismatch")]
    [InlineData("merge", "wound_plan_effect_result_disposition_mismatch")]
    [InlineData("replace", "wound_plan_effect_result_disposition_mismatch")]
    public void Finalize_RejectsNonBijectiveConfusableCrossWoundOrNonCreatingResults(
        string mutation,
        string expectedCode)
    {
        var input = CreateInput(2, CandidateShape.Standard, shareCausalEvent: true);
        var before = CapturePreTurn(input);
        var prepared = AssertPrepared(WoundAcceptedTurnPlanner.Prepare(input));
        var stage = BuildEffectStage(
            prepared,
            new ScriptedEffectIdentityFactory("set"));
        var malformed = RewrapEffectStage(
            prepared,
            stage,
            MutateResultSet(AssertEffectPlan(stage.Result).ApplicationResults, mutation));

        var result = WoundAcceptedTurnPlanner.Finalize(prepared, malformed);

        AssertInvalidFinalization(result);
        Assert.Single(result.Issues, issue => issue.Code == expectedCode);
        AssertPreTurnUnchanged(input, before);
    }

    [Theory]
    [InlineData("create_transition")]
    [InlineData("created_event")]
    [InlineData("causal_event")]
    [InlineData("source_realm")]
    [InlineData("source_kind")]
    [InlineData("source_id")]
    [InlineData("source_definition")]
    [InlineData("target_realm")]
    [InlineData("target_kind")]
    [InlineData("target_id")]
    [InlineData("carrier_kind")]
    [InlineData("carrier_owner")]
    [InlineData("carrier_path")]
    [InlineData("carrier_category")]
    [InlineData("slot")]
    [InlineData("profile")]
    [InlineData("summary")]
    [InlineData("component_count")]
    [InlineData("materialization_fingerprint")]
    public void Finalize_RejectsEveryRootAgreementFieldMismatch(string mutation)
    {
        var input = CreateInput();
        var before = CapturePreTurn(input);
        var prepared = AssertPrepared(WoundAcceptedTurnPlanner.Prepare(input));
        var stage = BuildEffectStage(
            prepared,
            new ScriptedEffectIdentityFactory("agreement"));
        var application = Assert.Single(AssertEffectPlan(stage.Result).ApplicationResults);
        var malformed = RewrapEffectStage(
            prepared,
            stage,
            new[] { MutateApplicationAgreement(application, mutation) });

        var result = WoundAcceptedTurnPlanner.Finalize(prepared, malformed);

        AssertInvalidFinalization(result);
        var issue = Assert.Single(result.Issues);
        Assert.Equal("wound_plan_effect_result_agreement_mismatch", issue.Code);
        AssertPreTurnUnchanged(input, before);
    }

    [Theory]
    [InlineData("null_materialization")]
    [InlineData("null_slot_bindings")]
    [InlineData("null_fingerprint")]
    [InlineData("uppercase_fingerprint")]
    [InlineData("short_fingerprint")]
    [InlineData("long_fingerprint")]
    [InlineData("non_hex_fingerprint")]
    [InlineData("component_count_zero")]
    [InlineData("component_count_over_limit")]
    public void Finalize_RejectsMalformedNestedMaterializationWithoutPartialPlan(
        string mutation)
    {
        var input = CreateInput();
        var before = CapturePreTurn(input);
        var prepared = AssertPrepared(WoundAcceptedTurnPlanner.Prepare(input));
        var stage = BuildEffectStage(
            prepared,
            new ScriptedEffectIdentityFactory("malformed_materialization"));
        var application = Assert.Single(AssertEffectPlan(stage.Result).ApplicationResults);
        var malformedApplication = application with
        {
            Materialization = mutation switch
            {
                "null_materialization" => null!,
                "null_slot_bindings" => application.Materialization with
                {
                    SlotBindings = null!
                },
                "null_fingerprint" => application.Materialization with
                {
                    MaterializationFingerprint = null!
                },
                "uppercase_fingerprint" => application.Materialization with
                {
                    MaterializationFingerprint = application.Materialization
                        .MaterializationFingerprint.ToUpperInvariant()
                },
                "short_fingerprint" => application.Materialization with
                {
                    MaterializationFingerprint = "sha256:" + new string('a', 63)
                },
                "long_fingerprint" => application.Materialization with
                {
                    MaterializationFingerprint = "sha256:" + new string('a', 65)
                },
                "non_hex_fingerprint" => application.Materialization with
                {
                    MaterializationFingerprint = "sha256:not-hex"
                },
                "component_count_zero" => application.Materialization with
                {
                    ComponentCount = 0
                },
                "component_count_over_limit" => application.Materialization with
                {
                    ComponentCount = WoundConsequenceEnvelopeCatalog
                        .MaximumComponentsPerEffect + 1
                },
                _ => throw new ArgumentOutOfRangeException(
                    nameof(mutation),
                    mutation,
                    null)
            }
        };
        var malformed = RewrapEffectStage(
            prepared,
            stage,
            new[] { malformedApplication });

        var result = WoundAcceptedTurnPlanner.Finalize(prepared, malformed);

        AssertInvalidFinalization(result);
        var issue = Assert.Single(result.Issues);
        Assert.Equal("wound_plan_effect_handoff_invalid", issue.Code);
        AssertPreTurnUnchanged(input, before);
    }

    [Fact]
    public void Finalize_OneInvalidEffectSiblingMakesTwoWoundFinalizationAtomic()
    {
        var input = CreateInput(2, CandidateShape.Standard, shareCausalEvent: true);
        var before = CapturePreTurn(input);
        var prepared = AssertPrepared(WoundAcceptedTurnPlanner.Prepare(input));
        var stage = BuildEffectStage(
            prepared,
            new ScriptedEffectIdentityFactory("atomic"));
        var results = AssertEffectPlan(stage.Result).ApplicationResults.ToArray();
        results[1] = results[1] with
        {
            CausalEventRef = "turn_42:foreign_causal_event"
        };

        var finalized = WoundAcceptedTurnPlanner.Finalize(
            prepared,
            RewrapEffectStage(prepared, stage, results));

        AssertInvalidFinalization(finalized);
        Assert.Single(finalized.Issues, static issue =>
            issue.Code == "wound_plan_effect_result_agreement_mismatch");
        AssertPreTurnUnchanged(input, before);
    }

    [Fact]
    public void Finalize_NullTopLevelArgumentsAreProgrammerErrors()
    {
        var prepared = AssertPrepared(WoundAcceptedTurnPlanner.Prepare(CreateInput()));
        var stage = BuildEffectStage(prepared, new ScriptedEffectIdentityFactory("null"));
        Assert.Equal(
            "prepared",
            Assert.Throws<ArgumentNullException>(() =>
                WoundAcceptedTurnPlanner.Finalize(null!, stage.Result)).ParamName);
        Assert.Equal(
            "effectResult",
            Assert.Throws<ArgumentNullException>(() =>
                WoundAcceptedTurnPlanner.Finalize(prepared, null!)).ParamName);
    }

    [Theory]
    [InlineData("null_issues")]
    [InlineData("null_issue")]
    [InlineData("null_plan_empty")]
    [InlineData("plan_with_issue")]
    public void Finalize_RejectsMalformedEffectStageWithoutThrowOrPartialPlan(string mutation)
    {
        var input = CreateInput();
        var before = CapturePreTurn(input);
        var prepared = AssertPrepared(WoundAcceptedTurnPlanner.Prepare(input));
        var valid = BuildEffectStage(
            prepared,
            new ScriptedEffectIdentityFactory("malformed")).Result;
        var candidate = mutation switch
        {
            "null_issues" => new WoundEffectBatchPlanningResult(null, null!),
            "null_issue" => new WoundEffectBatchPlanningResult(
                null,
                new ValidationIssue[] { null! }),
            "null_plan_empty" => new WoundEffectBatchPlanningResult(
                null,
                Array.Empty<ValidationIssue>()),
            "plan_with_issue" => new WoundEffectBatchPlanningResult(
                valid.Plan,
                new[] { CreateIssue("upstream_warning") }),
            _ => throw new InvalidOperationException(mutation)
        };

        var result = WoundAcceptedTurnPlanner.Finalize(prepared, candidate);

        AssertInvalidFinalization(result);
        var issue = Assert.Single(result.Issues);
        Assert.Equal("wound_plan_effect_handoff_invalid", issue.Code);
        AssertPreTurnUnchanged(input, before);
    }

    [Fact]
    public void Finalize_PreservesDetachedUpstreamFailureAndAddsStageFailure()
    {
        var input = CreateInput();
        var before = CapturePreTurn(input);
        var prepared = AssertPrepared(WoundAcceptedTurnPlanner.Prepare(input));
        var upstream = CreateIssue("effect_source_authority_failed");
        var failed = new WoundEffectBatchPlanningResult(null, new[] { upstream });

        var result = WoundAcceptedTurnPlanner.Finalize(prepared, failed);

        AssertInvalidFinalization(result);
        Assert.Equal(2, result.Issues.Count);
        AssertValidationIssueEqual(upstream, result.Issues[0]);
        Assert.Equal("wound_plan_effect_stage_failed", result.Issues[1].Code);
        Assert.NotSame(upstream, result.Issues[0]);
        AssertPreTurnUnchanged(input, before);
    }

    [Fact]
    public void Finalize_PreservesAuthorizedIndependentEffectWithoutClaimingItAsWoundRoot()
    {
        var input = CreateInput();
        var prepared = AssertPrepared(WoundAcceptedTurnPlanner.Prepare(input));
        var stage = BuildEffectStage(
            prepared,
            new ScriptedEffectIdentityFactory("independent"),
            includeIndependentEffect: true);
        var accepted = AssertEffectPlan(stage.Result);
        var rootResult = Assert.Single(accepted.ApplicationResults);
        Assert.Equal(2, accepted.EffectPlan.AllocatedEffectIds.Count);
        var independentId = Assert.Single(accepted.EffectPlan.AllocatedEffectIds, value =>
            !string.Equals(value, rootResult.EffectId, StringComparison.Ordinal));
        var effectBytes = accepted.EffectPlan.ActiveEffects
            .Single(value => string.Equals(
                value["effectId"]!.GetValue<string>(),
                independentId,
                StringComparison.Ordinal))
            .ToJsonString();

        var final = AssertFinalized(WoundAcceptedTurnPlanner.Finalize(
            prepared,
            stage.Result));

        var wound = Assert.Single(Assert.Single(final.CarrierContributions)
            .Mutations).AfterWound!;
        var rootBindings = ToJson(wound)["consequences"]!["ownedEffectSources"]!
            ["rootBindings"]!.AsArray();
        var binding = Assert.IsType<JsonObject>(Assert.Single(rootBindings));
        Assert.Equal(rootResult.EffectId, binding["effectId"]!.GetValue<string>());
        Assert.DoesNotContain(independentId, WoundMaterializationContract.SerializeCanonical(wound));
        Assert.DoesNotContain(
            final.TransitionIntents.OfType<WoundEffectTransitionIntent>()
                .SelectMany(static intent => intent.AfterEffectIds),
            value => string.Equals(value, independentId, StringComparison.Ordinal));
        Assert.Equal(
            effectBytes,
            accepted.EffectPlan.ActiveEffects.Single(value => string.Equals(
                value["effectId"]!.GetValue<string>(),
                independentId,
                StringComparison.Ordinal)).ToJsonString());
    }

    [Theory]
    [InlineData("active_effects")]
    [InlineData("resource_trigger_carriers")]
    [InlineData("carrier_after_images")]
    [InlineData("identity_index")]
    public void Finalize_RejectsResealedIndependentEffectThatClaimsPreparedWoundSourceInAnyView(
        string mutatedView)
    {
        var input = CreateInput();
        var before = CapturePreTurn(input);
        var prepared = AssertPrepared(WoundAcceptedTurnPlanner.Prepare(input));
        var stage = BuildEffectStage(
            prepared,
            new ScriptedEffectIdentityFactory("independent_rebound"),
            includeIndependentEffect: true);
        var accepted = AssertEffectPlan(stage.Result);
        var rootResult = Assert.Single(accepted.ApplicationResults);
        var independentId = Assert.Single(accepted.EffectPlan.AllocatedEffectIds, value =>
            !string.Equals(value, rootResult.EffectId, StringComparison.Ordinal));
        var preparedSource = Assert.Single(
            Assert.Single(prepared.EffectOperationBatches).RootApplications)
            .ExpectedSourceKey;
        var divergentPlan = CloneEffectPlanWithSingleViewSourceMutation(
            accepted.EffectPlan,
            independentId,
            preparedSource,
            mutatedView);
        var resealed = WoundEffectBatchAcceptedPlan.Create(
            prepared,
            accepted.EffectInput,
            divergentPlan,
            accepted.ApplicationResults,
            accepted.TerminationResults);

        var result = WoundAcceptedTurnPlanner.Finalize(
            prepared,
            new WoundEffectBatchPlanningResult(
                resealed,
                Array.Empty<ValidationIssue>()));

        AssertInvalidFinalization(result);
        Assert.Contains(result.Issues, issue =>
            string.Equals(
                issue.Code,
                "wound_plan_effect_result_agreement_mismatch",
                StringComparison.Ordinal));
        AssertPreTurnUnchanged(input, before);
    }

    [Theory]
    [InlineData(OwnerFlavor.Npc)]
    [InlineData(OwnerFlavor.AfterlifeGuardian)]
    public void Finalize_ProducesOwnerNeutralComposableContributionAtExactCarrier(
        OwnerFlavor flavor)
    {
        var shape = flavor == OwnerFlavor.AfterlifeGuardian
            ? CandidateShape.SpiritualTwoRoots
            : CandidateShape.NoMechanics;
        var input = CreateInput(1, shape, flavor);
        var before = CapturePreTurn(input);
        var prepared = AssertPrepared(WoundAcceptedTurnPlanner.Prepare(input));
        var stage = BuildEffectStage(prepared, new ScriptedEffectIdentityFactory("owner"));
        var final = AssertFinalized(WoundAcceptedTurnPlanner.Finalize(
            prepared,
            stage.Result));

        var contribution = Assert.Single(final.CarrierContributions);
        Assert.Equal(CreateOwner(flavor), contribution.Owner);
        Assert.NotEmpty(contribution.ExpectedWoundCollectionFingerprint);
        Assert.Single(contribution.Mutations);
        var composed = ApplyContributions(input.PreTurnCarriers, final.CarrierContributions);
        var catalog = WoundCarrierCatalog.Build(composed);
        Assert.Empty(catalog.Issues);
        var occurrence = Assert.Single(catalog.Occurrences);
        Assert.Equal(contribution.Owner.OwnerKind, occurrence.Coordinate.OwnerKind);
        Assert.Equal(contribution.Owner.OwnerId, occurrence.Coordinate.OwnerId);
        Assert.Equal(contribution.Owner.CarrierPath, occurrence.Coordinate.CarrierPath);
        AssertPreTurnUnchanged(input, before);

        if (flavor == OwnerFlavor.AfterlifeGuardian)
        {
            Assert.Equal(
                input.PreTurnCarriers.AfterlifeProfiles!["profileAudit"]!.ToJsonString(),
                composed.AfterlifeProfiles!["profileAudit"]!.ToJsonString());
            Assert.Equal(
                input.PreTurnCarriers.AfterlifeProfiles["profiles"]![0]!["services"]!
                    .ToJsonString(),
                composed.AfterlifeProfiles["profiles"]![0]!["services"]!.ToJsonString());
        }
    }

    [Fact]
    public void Finalize_ExpectedBeforeSealChangesForSameCardinalityDifferentWoundCollection()
    {
        var firstInput = WithExistingWound(
            CreateInput(1, CandidateShape.NoMechanics),
            "wound_existing_alpha");
        var secondInput = WithExistingWound(
            CreateInput(1, CandidateShape.NoMechanics),
            "wound_existing_beta");
        var firstPrepared = AssertPrepared(WoundAcceptedTurnPlanner.Prepare(firstInput));
        var secondPrepared = AssertPrepared(WoundAcceptedTurnPlanner.Prepare(secondInput));
        var firstFinal = AssertFinalized(WoundAcceptedTurnPlanner.Finalize(
            firstPrepared,
            BuildEffectStage(
                firstPrepared,
                new ScriptedEffectIdentityFactory("before_a")).Result));
        var secondFinal = AssertFinalized(WoundAcceptedTurnPlanner.Finalize(
            secondPrepared,
            BuildEffectStage(
                secondPrepared,
                new ScriptedEffectIdentityFactory("before_b")).Result));

        var first = Assert.Single(firstFinal.CarrierContributions);
        var second = Assert.Single(secondFinal.CarrierContributions);
        Assert.Equal(first.Owner, second.Owner);
        Assert.NotEqual(
            first.ExpectedWoundCollectionFingerprint,
            second.ExpectedWoundCollectionFingerprint);

        var afterlife = CreateInput(
            1,
            CandidateShape.SpiritualTwoRoots,
            OwnerFlavor.AfterlifeGuardian);
        var changedAfterlifeRoot = afterlife.PreTurnCarriers.AfterlifeProfiles!
            .DeepClone().AsObject();
        changedAfterlifeRoot["profileAudit"]!["lastTurn"] = 999;
        changedAfterlifeRoot["profiles"]![0]!["services"]!["revision"] = 999;
        var changedAfterlife = afterlife with
        {
            PreTurnCarriers = afterlife.PreTurnCarriers with
            {
                AfterlifeProfiles = changedAfterlifeRoot
            }
        };
        var afterlifePrepared = AssertPrepared(WoundAcceptedTurnPlanner.Prepare(afterlife));
        var changedPrepared = AssertPrepared(WoundAcceptedTurnPlanner.Prepare(changedAfterlife));
        var afterlifeFinal = AssertFinalized(WoundAcceptedTurnPlanner.Finalize(
            afterlifePrepared,
            BuildEffectStage(
                afterlifePrepared,
                new ScriptedEffectIdentityFactory("afterlife_a")).Result));
        var changedFinal = AssertFinalized(WoundAcceptedTurnPlanner.Finalize(
            changedPrepared,
            BuildEffectStage(
                changedPrepared,
                new ScriptedEffectIdentityFactory("afterlife_b")).Result));
        Assert.Equal(
            Assert.Single(afterlifeFinal.CarrierContributions)
                .ExpectedWoundCollectionFingerprint,
            Assert.Single(changedFinal.CarrierContributions)
                .ExpectedWoundCollectionFingerprint);
    }

    [Fact]
    public void PreparedEffectAndFinalPlans_AreDeeplyImmutableDetachedAndDeterministic()
    {
        var firstInput = CreateInput(1, CandidateShape.OneRootTwoSlots);
        var secondInput = CreateInput(1, CandidateShape.OneRootTwoSlots);
        var firstPrepared = AssertPrepared(WoundAcceptedTurnPlanner.Prepare(
            firstInput,
            new RecordingWoundIdentityAllocator("deterministic")));
        var secondPrepared = AssertPrepared(WoundAcceptedTurnPlanner.Prepare(
            secondInput,
            new RecordingWoundIdentityAllocator("deterministic")));
        AssertPreparedEquivalent(firstPrepared, secondPrepared);
        AssertReadOnly(firstPrepared.AllocatedWoundIds);
        AssertReadOnly(firstPrepared.AllocatedTransitionIds);
        AssertReadOnly(firstPrepared.PreparedWounds);
        AssertReadOnly(firstPrepared.EffectOperationBatches);
        var batch = Assert.Single(firstPrepared.EffectOperationBatches);
        AssertReadOnly(batch.SourceExport.Definitions);
        AssertReadOnly(batch.RootApplications);
        AssertReadOnly(batch.RootLineageAuthority);
        var exported = Assert.Single(batch.SourceExport.Definitions).Definition;
        var exportedBytes = exported.ToJsonString();
        exported["display"]!["name"] = "mutated returned definition";
        Assert.Equal(
            exportedBytes,
            Assert.Single(batch.SourceExport.Definitions).Definition.ToJsonString());
        var parameters = Assert.Single(batch.RootApplications).Parameters;
        parameters["forged"] = 1;
        Assert.Empty(Assert.Single(batch.RootApplications).Parameters);

        var firstStage = BuildEffectStage(
            firstPrepared,
            new ScriptedEffectIdentityFactory("deterministic_effect"));
        var secondStage = BuildEffectStage(
            secondPrepared,
            new ScriptedEffectIdentityFactory("deterministic_effect"));
        var firstEffect = AssertEffectPlan(firstStage.Result);
        var secondEffect = AssertEffectPlan(secondStage.Result);
        Assert.Equal(firstEffect.EffectInputFingerprint, secondEffect.EffectInputFingerprint);
        Assert.Equal(
            firstEffect.EffectAcceptedTurnPlanFingerprint,
            secondEffect.EffectAcceptedTurnPlanFingerprint);
        AssertReadOnly(firstEffect.ApplicationResults);
        AssertReadOnly(firstEffect.TerminationResults);
        var returnedSlots = Assert.Single(firstEffect.ApplicationResults)
            .Materialization.SlotBindings;
        AssertReadOnly(returnedSlots);

        var firstFinal = AssertFinalized(WoundAcceptedTurnPlanner.Finalize(
            firstPrepared,
            firstStage.Result));
        var secondFinal = AssertFinalized(WoundAcceptedTurnPlanner.Finalize(
            secondPrepared,
            secondStage.Result));
        Assert.Equal(firstFinal.WoundFinalPlanFingerprint, secondFinal.WoundFinalPlanFingerprint);
        AssertReadOnly(firstFinal.CarrierContributions);
        AssertReadOnly(Assert.Single(firstFinal.CarrierContributions).Mutations);
        AssertReadOnly(firstFinal.TransitionIntents);
        var identity = firstFinal.IdentityIndexAfterImage;
        var identityBytes = identity.ToJsonString();
        identity["entries"] = new JsonArray();
        Assert.Equal(identityBytes, firstFinal.IdentityIndexAfterImage.ToJsonString());
        var history = firstFinal.HistoryAfterImage;
        var historyBytes = history.ToJsonString();
        history["transitions"] = new JsonArray();
        Assert.Equal(historyBytes, firstFinal.HistoryAfterImage.ToJsonString());
    }

    [Fact]
    public void T020WoundPlanSurface_HasNoFilesystemNormalizerOrWriteLeaseDependency()
    {
        var paths = new[]
        {
            ToAbsolutePath("BookOfEternityClient/Services/WoundAcceptedTurnPlan.cs"),
            ToAbsolutePath("BookOfEternityClient/Services/WoundAcceptedTurnPlanner.cs")
        };
        Assert.All(paths, path => Assert.True(
            File.Exists(path),
            $"Expected T020 source file was absent: {path}"));
        var forbidden = new[]
        {
            "CanonicalStateNormalizer",
            "IGameStateFileManager",
            "GameStateFileManager",
            "WriteAllText",
            "WriteAllBytes",
            "File.Write",
            "WriteLease",
            "AcquireWrite"
        };

        foreach (var path in paths)
        {
            var source = File.ReadAllText(path);
            Assert.All(forbidden, token =>
                Assert.DoesNotContain(token, source, StringComparison.Ordinal));
        }
    }

    public enum OwnerFlavor
    {
        Player,
        Npc,
        Combatant,
        AfterlifeGuardian
    }

    private enum CandidateShape
    {
        Standard,
        TwoRoots,
        OneRootTwoSlots,
        MarkerRoot,
        ReactionWithMarkerLeaf,
        FiveRoots,
        SixRoots,
        SpiritualTwoRoots,
        NoMechanics
    }

    private sealed record PreTurnSnapshot(
        string Identity,
        string History,
        string? Player,
        string? Npc,
        string? Enemies,
        string? Allies,
        string? Afterlife);

    private sealed record EffectStageFixture(
        EffectAcceptedTurnInput Input,
        WoundEffectBatchPlanningResult Result);

    private sealed record ApplicationAllocationRequest(
        WoundAcceptedTurnIdentityScope Scope,
        string LocalApplicationRef,
        string DefinitionKey,
        string OperationKey);

    private sealed record TransitionAllocationRequest(
        WoundAcceptedTurnIdentityScope Scope,
        string LocalTransitionRef);

    private sealed record ScopeAllocation(
        string WoundId,
        string ApplicationRef,
        string TransitionId)
    {
        internal IEnumerable<string> Values =>
            new[] { WoundId, ApplicationRef, TransitionId };
    }

    private static WoundAcceptedTurnInput CreateInput(
        int transitionCount = 1,
        CandidateShape shape = CandidateShape.Standard,
        OwnerFlavor flavor = OwnerFlavor.Player,
        bool shareCausalEvent = false,
        string reactionEventType = "owner_damaged")
    {
        if (transitionCount < 1)
            throw new ArgumentOutOfRangeException(nameof(transitionCount));

        var owner = CreateOwner(flavor);
        var acceptedEvents = Enumerable.Range(1, shareCausalEvent ? 1 : transitionCount)
            .Select(ordinal => new WoundAcceptedEventAuthority(
                $"turn_{Turn}:wound:{ordinal:D3}",
                ordinal == 1 ? "accepted_turn" : "combat_outcome",
                $"authority_A_{ordinal:D3}",
                Fingerprint($"accepted-event:{ordinal:D3}")))
            .ToArray();
        var binding = new WoundAcceptedTurnBinding(
            SessionId,
            RequestId,
            SnapshotToken,
            owner.Realm,
            Turn,
            acceptedEvents,
            ComputeExpectedAcceptedEventSetFingerprint(acceptedEvents));
        var opportunities = new List<WoundOpportunityAuthority>(transitionCount);
        var transitions = new List<WoundAcceptedTransitionDraft>(transitionCount);

        for (var index = 0; index < transitionCount; index++)
        {
            var ordinal = index + 1;
            var eventAuthority = acceptedEvents[shareCausalEvent ? 0 : index];
            var opportunityId = $"opportunity_wound_{ordinal:D3}";
            var localWoundRef = $"draft_wound_{ordinal:D3}";
            var localTransitionRef = $"draft_transition_{ordinal:D3}";
            var operationKey = $"operation_wound_{ordinal:D3}";
            var domain = flavor == OwnerFlavor.AfterlifeGuardian
                ? "spiritual"
                : "physical";
            var maximumSeverityRank = shape switch
            {
                CandidateShape.FiveRoots or CandidateShape.SixRoots => 4,
                CandidateShape.ReactionWithMarkerLeaf => 3,
                _ => 2
            };
            opportunities.Add(new WoundOpportunityAuthority(
                SessionId,
                RequestId,
                SnapshotToken,
                opportunityId,
                eventAuthority.EventRef,
                owner,
                domain,
                "combat_action",
                $"combat_action_{ordinal:D3}",
                "active",
                1,
                maximumSeverityRank,
                Fingerprint($"opportunity:{ordinal:D3}:{owner}")));

            var draftGraph = CreateDraftGraph(
                shape,
                ordinal,
                owner,
                reactionEventType);
            var proposed = CreateProposedWound(
                localWoundRef,
                localTransitionRef,
                opportunityId,
                eventAuthority.EventRef,
                ordinal,
                owner,
                domain,
                shape);
            transitions.Add(new WoundAcceptedTransitionDraft(
                "create",
                operationKey,
                localWoundRef,
                localTransitionRef,
                opportunityId,
                $"Materialize wound proposal {ordinal:D3}.",
                proposed,
                draftGraph.Definitions,
                draftGraph.RootApplications,
                draftGraph.SlotBindings));
        }

        return new WoundAcceptedTurnInput(
            binding,
            opportunities.ToArray(),
            transitions.ToArray(),
            CreatePreTurnCarriers(flavor),
            WoundContractTestData.CreateIdentityIndex(),
            WoundContractTestData.CreateHistory());
    }

    private static WoundAcceptedTurnInput CreateNoOpInput()
    {
        var authority = new WoundAcceptedEventAuthority(
            $"turn_{Turn}:accepted:no-op",
            "accepted_turn",
            $"turn_{Turn}",
            Fingerprint("accepted-event:no-op"));
        var events = new[] { authority };
        return new WoundAcceptedTurnInput(
            new WoundAcceptedTurnBinding(
                SessionId,
                RequestId,
                SnapshotToken,
                Realm,
                Turn,
                events,
                ComputeExpectedAcceptedEventSetFingerprint(events)),
            Array.Empty<WoundOpportunityAuthority>(),
            Array.Empty<WoundAcceptedTransitionDraft>(),
            CreatePreTurnCarriers(OwnerFlavor.Player),
            WoundContractTestData.CreateIdentityIndex(),
            WoundContractTestData.CreateHistory());
    }

    internal static WoundAcceptedTurnInput CreateInputForAcceptedCache(
        int transitionCount = 1) =>
        CreateInput(transitionCount);

    internal static WoundAcceptedTurnInput CreateInputForAcceptedCache(
        OwnerFlavor flavor) =>
        CreateInput(
            transitionCount: 1,
            shape: flavor == OwnerFlavor.AfterlifeGuardian
                ? CandidateShape.SpiritualTwoRoots
                : CandidateShape.Standard,
            flavor: flavor);

    internal static WoundAcceptedTurnInput CreateNoMechanicsInputForAcceptedCache() =>
        CreateInput(
            transitionCount: 1,
            shape: CandidateShape.NoMechanics,
            flavor: OwnerFlavor.Player);

    internal static EffectAcceptedTurnInput CreateEffectInputForAcceptedCache(
        WoundPreparedAcceptedTurnPlan prepared) =>
        CreateEffectInput(prepared);

    internal static EffectAcceptedTurnInput CreateEffectInputForAcceptedCache(
        WoundPreparedAcceptedTurnPlan prepared,
        EffectSourceExport additionalPreTurnSource,
        EffectCarrierCatalogInput preTurnCarriers,
        JsonObject preTurnIdentityIndex,
        Action<JsonObject> mutateEventInput) =>
        CreateEffectInput(
            prepared,
            additionalPreTurnSources: new[] { additionalPreTurnSource },
            preTurnCarriersOverride: preTurnCarriers,
            preTurnIdentityIndexOverride: preTurnIdentityIndex,
            mutateEventInput: mutateEventInput);

    private static (
        IReadOnlyList<WoundAcceptedEffectDefinitionDraft> Definitions,
        IReadOnlyList<WoundAcceptedRootApplicationDraft> RootApplications,
        IReadOnlyList<WoundAcceptedConsequenceSlotBinding> SlotBindings)
        CreateDraftGraph(
            CandidateShape shape,
            int woundOrdinal,
            WoundOwnerCoordinate owner,
            string reactionEventType = "owner_damaged")
    {
        var roots = shape switch
        {
            CandidateShape.Standard => new[] { "periodic_damage" },
            CandidateShape.TwoRoots => new[] { "periodic_damage", "action_control" },
            CandidateShape.OneRootTwoSlots => new[] { "periodic_damage" },
            CandidateShape.MarkerRoot => new[] { "wound_consequence" },
            CandidateShape.ReactionWithMarkerLeaf => new[] { "event_reaction" },
            CandidateShape.FiveRoots => new[]
            {
                "characteristic_modifier",
                "resistance_modifier",
                "periodic_damage",
                "action_control",
                "wound_consequence"
            },
            CandidateShape.SixRoots => new[]
            {
                "characteristic_modifier",
                "resistance_modifier",
                "periodic_damage",
                "action_control",
                "roll_modifier",
                "wound_consequence"
            },
            CandidateShape.SpiritualTwoRoots => new[]
            {
                "spiritual_roll_hindrance",
                "spiritual_action_cost_burden"
            },
            CandidateShape.NoMechanics => Array.Empty<string>(),
            _ => throw new ArgumentOutOfRangeException(nameof(shape), shape, null)
        };
        var definitions = new List<WoundAcceptedEffectDefinitionDraft>();
        var applications = new List<WoundAcceptedRootApplicationDraft>();
        var slots = new List<WoundAcceptedConsequenceSlotBinding>();
        string? leafKey = null;

        if (shape == CandidateShape.ReactionWithMarkerLeaf)
            leafKey = $"wound_definition_{woundOrdinal:D3}_marker_leaf";

        var nextSlot = 1;
        for (var rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            var rootOrdinal = rootIndex + 1;
            var profile = roots[rootIndex];
            var suffix = profile == "event_reaction" ? "reaction" : $"root_{rootOrdinal:D3}";
            var definitionKey = $"wound_definition_{woundOrdinal:D3}_{suffix}";
            var localEffectRef = $"draft_effect_{woundOrdinal:D3}_{rootOrdinal:D3}";
            var localApplicationRef =
                $"draft_application_{woundOrdinal:D3}_{rootOrdinal:D3}";
            var definition = CreateDefinition(
                definitionKey,
                profile,
                owner,
                $"component_{woundOrdinal:D3}_{rootOrdinal:D3}_001",
                $"draft_wound_{woundOrdinal:D3}",
                leafKey,
                reactionEventType);
            if (profile == "wound_consequence")
            {
                definition["components"]![0]!["payload"]!["woundId"] =
                    $"draft_wound_{woundOrdinal:D3}";
            }
            var applicationSlots = new List<WoundAcceptedConsequenceSlotBinding>();

            if (shape == CandidateShape.OneRootTwoSlots)
            {
                var secondComponent = EffectMaterializationTestFixture
                    .CreateDefinition("action_control")["components"]![0]!
                    .DeepClone().AsObject();
                secondComponent["componentId"] =
                    $"component_{woundOrdinal:D3}_{rootOrdinal:D3}_002";
                definition["components"]!.AsArray().Add(secondComponent);
                definition["triggers"]![0]!["componentIds"] = new JsonArray(
                    $"component_{woundOrdinal:D3}_{rootOrdinal:D3}_001",
                    $"component_{woundOrdinal:D3}_{rootOrdinal:D3}_002");
                applicationSlots.Add(new WoundAcceptedConsequenceSlotBinding(
                    nextSlot++,
                    "periodic_damage",
                    localApplicationRef,
                    "The wound deals periodic damage."));
                applicationSlots.Add(new WoundAcceptedConsequenceSlotBinding(
                    nextSlot++,
                    "action_control",
                    localApplicationRef,
                    "The wound restricts movement."));
            }
            else if (profile is not "wound_consequence")
            {
                applicationSlots.Add(new WoundAcceptedConsequenceSlotBinding(
                    nextSlot++,
                    profile,
                    localApplicationRef,
                    $"The wound applies {profile}."));
            }

            definitions.Add(new WoundAcceptedEffectDefinitionDraft(
                localEffectRef,
                definition));
            applications.Add(new WoundAcceptedRootApplicationDraft(
                localApplicationRef,
                localEffectRef,
                $"root_application_{woundOrdinal:D3}_{rootOrdinal:D3}",
                WoundRootOwnershipDomain.BaseWound));
            slots.AddRange(applicationSlots);
        }

        if (leafKey is not null)
        {
            var leafDefinition = CreateDefinition(
                leafKey,
                "wound_consequence",
                owner,
                $"component_{woundOrdinal:D3}_marker_leaf",
                $"draft_wound_{woundOrdinal:D3}");
            leafDefinition["components"]![0]!["payload"]!["woundId"] =
                $"draft_wound_{woundOrdinal:D3}";
            definitions.Add(new WoundAcceptedEffectDefinitionDraft(
                $"draft_effect_{woundOrdinal:D3}_marker_leaf",
                leafDefinition));
        }

        return (definitions.ToArray(), applications.ToArray(), slots.ToArray());
    }

    private static JsonObject CreateDefinition(
        string definitionKey,
        string profile,
        WoundOwnerCoordinate owner,
        string componentId,
        string woundId,
        string? applyDefinitionKey = null,
        string reactionEventType = "owner_damaged")
    {
        var definition = profile.StartsWith("spiritual_", StringComparison.Ordinal)
            ? EffectMaterializationTestFixture.CreateSpiritualWoundDefinition(
                profile,
                owner.OwnerKind,
                owner.Realm,
                woundId,
                definitionKey)
            : EffectMaterializationTestFixture.CreateDefinition(profile);
        definition["definitionKey"] = definitionKey;
        definition["allowedRealms"] = new JsonArray(owner.Realm);
        definition["allowedTargetKinds"] = new JsonArray(owner.OwnerKind);
        definition["parameterBounds"] = new JsonObject();
        definition["links"] = new JsonArray();
        definition["stacking"]!["stackKey"] = "stack_" + definitionKey;
        definition["stacking"]!["policy"] = "independent";
        definition["stacking"]!["maxStacks"] = 1;
        definition["stacking"]!["atMaximum"] = "no_change";
        definition["stacking"]!["refreshMode"] = null;
        definition["stacking"]!["mergeRule"] = null;
        var component = definition["components"]![0]!.AsObject();
        component["componentId"] = componentId;
        if (profile == "spiritual_action_cost_burden")
            component["payload"]!["magnitude"] = 1;
        definition["triggers"]![0]!["componentIds"] = new JsonArray(componentId);
        if (applyDefinitionKey is not null)
        {
            var payload = component["payload"]!.AsObject();
            payload["eventType"] = reactionEventType;
            payload["resultKind"] = "apply_definition";
            payload["definitionKey"] = applyDefinitionKey;
            payload["parameters"] = new JsonObject();
            payload["maxExpansion"] = 2;
            definition["triggers"]![0]!["eventType"] = reactionEventType;
            definition["triggers"]![0]!["triggerId"] =
                "on_" + reactionEventType;
        }

        return definition;
    }

    private static WoundMaterializationEnvelope CreateProposedWound(
        string localWoundRef,
        string localTransitionRef,
        string opportunityId,
        string eventRef,
        int ordinal,
        WoundOwnerCoordinate owner,
        string domain,
        CandidateShape shape)
    {
        var spiritual = string.Equals(domain, "spiritual", StringComparison.Ordinal);
        var json = spiritual
            ? WoundContractTestData.CreateSpiritualActiveWound(
                localWoundRef,
                owner.Realm,
                owner.OwnerKind,
                owner.OwnerId,
                owner.CarrierPath)
            : WoundContractTestData.CreateActiveWound(
                localWoundRef,
                owner.Realm,
                owner.OwnerKind,
                owner.OwnerId,
                owner.CarrierPath,
                domain: domain);
        json["origin"]!["eventRef"] = eventRef;
        json["origin"]!["sourceId"] = $"combat_action_{ordinal:D3}";
        json["origin"]!["createdAtTurn"] = Turn;
        json["origin"]!["opportunityId"] = opportunityId;
        json["severity"]!["lastChangeEventRef"] = eventRef;
        json["lastTransition"]!["transitionId"] = localTransitionRef;
        json["lastTransition"]!["turn"] = Turn;
        if (!spiritual)
            json["consequences"] = new JsonObject
        {
            ["slotBudget"] = shape switch
            {
                CandidateShape.FiveRoots or CandidateShape.SixRoots => 4,
                CandidateShape.ReactionWithMarkerLeaf => 3,
                _ => 2
            },
            ["slotsUsed"] = 0,
            ["entries"] = new JsonArray(),
            ["ownedEffectSources"] = new JsonObject
            {
                ["definitions"] = new JsonArray(),
                ["rootBindings"] = new JsonArray()
            }
        };
        if (shape is CandidateShape.FiveRoots or CandidateShape.SixRoots)
        {
            json["severity"]!["value"] = "IV";
            json["severity"]!["rank"] = 4;
            json["severity"]!["maximumAtCreation"] = "IV";
        }
        else if (shape == CandidateShape.ReactionWithMarkerLeaf)
        {
            json["severity"]!["value"] = "III";
            json["severity"]!["rank"] = 3;
            json["severity"]!["maximumAtCreation"] = "III";
        }

        var parsed = WoundMaterializationContract.Parse(
            json.ToJsonString(),
            $"acceptedTurn.transitions[{ordinal - 1}].proposedAfter");
        Assert.True(
            parsed.IsValid,
            string.Join(Environment.NewLine, parsed.Issues.Select(static issue =>
                $"{issue.Code}: {issue.Message}")));
        var wound = Assert.IsType<WoundMaterializationEnvelope>(parsed.Wound);
        return spiritual
            ? wound with
            {
                Consequences = new WoundConsequences(
                    wound.Consequences.SlotBudget,
                    0,
                    Array.Empty<WoundConsequenceEntry>())
                {
                    OwnedEffectSources = WoundOwnedEffectSources.Empty
                }
            }
            : wound;
    }

    private static WoundAcceptedTurnInput CreateEventSetMutation(string mutation)
    {
        var input = CreateInput(2);
        var baseline = input.Binding.AcceptedEvents.ToArray();
        IReadOnlyList<WoundAcceptedEventAuthority> events = mutation switch
        {
            "empty" => Array.Empty<WoundAcceptedEventAuthority>(),
            "null_event" => new WoundAcceptedEventAuthority[] { null! },
            "blank_event_ref" => new[] { baseline[0] with { EventRef = "" }, baseline[1] },
            "blank_kind" => new[] { baseline[0] with { Kind = " " }, baseline[1] },
            "blank_authority" => new[] { baseline[0] with { AuthorityId = "" }, baseline[1] },
            "invalid_semantic_fingerprint" => new[]
            {
                baseline[0] with { SemanticFingerprint = "sha256:not-hex" }, baseline[1]
            },
            "duplicate_event_ref" => new[]
            {
                baseline[0], baseline[1] with { EventRef = baseline[0].EventRef }
            },
            "confusable_event_ref" => new[]
            {
                baseline[0], baseline[1] with
                {
                    EventRef = ConfusableVariant(baseline[0].EventRef)
                }
            },
            "duplicate_authority" => new[]
            {
                baseline[0], baseline[1] with
                {
                    Kind = baseline[0].Kind,
                    AuthorityId = baseline[0].AuthorityId
                }
            },
            "confusable_authority" => new[]
            {
                baseline[0], baseline[1] with
                {
                    Kind = baseline[0].Kind,
                    AuthorityId = ConfusableVariant(baseline[0].AuthorityId)
                }
            },
            "forged_fingerprint" => baseline,
            "reordered_with_old_fingerprint" => new[] { baseline[1], baseline[0] },
            _ => throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null)
        };
        var fingerprint = mutation switch
        {
            "forged_fingerprint" => Fingerprint("forged-event-set"),
            "reordered_with_old_fingerprint" => input.Binding.AcceptedEventsFingerprint,
            "null_event" => input.Binding.AcceptedEventsFingerprint,
            _ => events.All(static value => value is not null)
                ? ComputeExpectedAcceptedEventSetFingerprint(events)
                : input.Binding.AcceptedEventsFingerprint
        };
        return input with
        {
            Binding = input.Binding with
            {
                AcceptedEvents = events,
                AcceptedEventsFingerprint = fingerprint
            }
        };
    }

    private static WoundAcceptedTurnInput CreateNestedInputMutation(string mutation)
    {
        var input = CreateInput();
        var transition = input.Transitions[0];
        return mutation switch
        {
            "null_binding" => input with { Binding = null! },
            "null_opportunities" => input with { Opportunities = null! },
            "null_opportunity" => input with
            {
                Opportunities = new WoundOpportunityAuthority[] { null! }
            },
            "null_transitions" => input with { Transitions = null! },
            "null_transition" => input with
            {
                Transitions = new WoundAcceptedTransitionDraft[] { null! }
            },
            "null_definitions" => ReplaceTransition(input, transition with
            {
                EffectDefinitions = null!
            }),
            "null_definition" => ReplaceTransition(input, transition with
            {
                EffectDefinitions = new WoundAcceptedEffectDefinitionDraft[] { null! }
            }),
            "null_root_applications" => ReplaceTransition(input, transition with
            {
                RootApplications = null!
            }),
            "null_root_application" => ReplaceTransition(input, transition with
            {
                RootApplications = new WoundAcceptedRootApplicationDraft[] { null! }
            }),
            "null_slots" => ReplaceTransition(input, transition with
            {
                SlotBindings = null!
            }),
            "null_slot" => ReplaceTransition(input, transition with
            {
                SlotBindings = new WoundAcceptedConsequenceSlotBinding[] { null! }
            }),
            "null_definition_facts" => ReplaceTransition(
                input,
                transition with
                {
                    ProposedAfter = transition.ProposedAfter with
                    {
                        Consequences = transition.ProposedAfter.Consequences with
                        {
                            OwnedEffectSources = transition.ProposedAfter.Consequences
                                .OwnedEffectSources with
                            {
                                DefinitionFacts = null!
                            }
                        }
                    }
                }),
            "null_definition_fact" => ReplaceTransition(
                input,
                transition with
                {
                    ProposedAfter = transition.ProposedAfter with
                    {
                        Consequences = transition.ProposedAfter.Consequences with
                        {
                            OwnedEffectSources = transition.ProposedAfter.Consequences
                                .OwnedEffectSources with
                            {
                                DefinitionFacts =
                                    new WoundOwnedEffectDefinitionFact[] { null! }
                            }
                        }
                    }
                }),
            "null_carriers" => input with { PreTurnCarriers = null! },
            "null_identity" => input with { PreTurnIdentityIndex = null! },
            "null_history" => input with { PreTurnHistory = null! },
            "missing_opportunity" => input with
            {
                Opportunities = Array.Empty<WoundOpportunityAuthority>()
            },
            "duplicate_opportunity" => input with
            {
                Opportunities = new[] { input.Opportunities[0], input.Opportunities[0] }
            },
            "duplicate_transition" => input with
            {
                Transitions = new[] { transition, transition }
            },
            _ => throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null)
        };
    }

    private static WoundAcceptedTurnInput CreateBindingMutation(string mutation)
    {
        var input = CreateInput();
        var opportunity = input.Opportunities[0];
        var transition = input.Transitions[0];
        return mutation switch
        {
            "blank_session" => input with
            {
                Binding = input.Binding with { SessionId = "" }
            },
            "blank_request" => input with
            {
                Binding = input.Binding with { RequestId = " " }
            },
            "blank_snapshot" => input with
            {
                Binding = input.Binding with { SnapshotToken = "" }
            },
            "invalid_realm" => input with
            {
                Binding = input.Binding with { Realm = "Mortal World" }
            },
            "zero_turn" => input with
            {
                Binding = input.Binding with { Turn = 0 }
            },
            "opportunity_session" => input with
            {
                Opportunities = new[]
                {
                    opportunity with { SessionId = "session_foreign" }
                }
            },
            "opportunity_request" => input with
            {
                Opportunities = new[]
                {
                    opportunity with { RequestId = "request_foreign" }
                }
            },
            "opportunity_snapshot" => input with
            {
                Opportunities = new[]
                {
                    opportunity with { SnapshotToken = "snapshot_foreign" }
                }
            },
            "opportunity_event" => input with
            {
                Opportunities = new[]
                {
                    opportunity with { EventRef = "turn_42:foreign" }
                }
            },
            "origin_event" => ReplaceTransition(input, transition with
            {
                ProposedAfter = transition.ProposedAfter with
                {
                    Origin = transition.ProposedAfter.Origin with
                    {
                        EventRef = "turn_42:foreign"
                    }
                }
            }),
            "origin_turn" => ReplaceTransition(input, transition with
            {
                ProposedAfter = transition.ProposedAfter with
                {
                    Origin = transition.ProposedAfter.Origin with
                    {
                        CreatedAtTurn = Turn - 1
                    }
                }
            }),
            _ => throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null)
        };
    }

    private static WoundAcceptedTurnInput ReplaceTransition(
        WoundAcceptedTurnInput input,
        WoundAcceptedTransitionDraft transition) =>
        input with { Transitions = new[] { transition } };

    private static PreTurnSnapshot CapturePreTurn(WoundAcceptedTurnInput input) =>
        new(
            input.PreTurnIdentityIndex.ToJsonString(),
            input.PreTurnHistory.ToJsonString(),
            input.PreTurnCarriers.PlayerWounds?.ToJsonString(),
            input.PreTurnCarriers.NpcWounds?.ToJsonString(),
            input.PreTurnCarriers.EnemyCombatants?.ToJsonString(),
            input.PreTurnCarriers.AllyCombatants?.ToJsonString(),
            input.PreTurnCarriers.AfterlifeProfiles?.ToJsonString());

    private static void AssertPreTurnUnchanged(
        WoundAcceptedTurnInput input,
        PreTurnSnapshot expected)
    {
        Assert.Equal(expected.Identity, input.PreTurnIdentityIndex.ToJsonString());
        Assert.Equal(expected.History, input.PreTurnHistory.ToJsonString());
        Assert.Equal(expected.Player, input.PreTurnCarriers.PlayerWounds?.ToJsonString());
        Assert.Equal(expected.Npc, input.PreTurnCarriers.NpcWounds?.ToJsonString());
        Assert.Equal(expected.Enemies,
            input.PreTurnCarriers.EnemyCombatants?.ToJsonString());
        Assert.Equal(expected.Allies,
            input.PreTurnCarriers.AllyCombatants?.ToJsonString());
        Assert.Equal(expected.Afterlife,
            input.PreTurnCarriers.AfterlifeProfiles?.ToJsonString());
    }

    private static string SerializeDraft(WoundAcceptedTransitionDraft draft) =>
        JsonSerializer.Serialize(new
        {
            draft.Kind,
            draft.OperationKey,
            draft.LocalWoundRef,
            draft.LocalTransitionRef,
            draft.OpportunityId,
            draft.ReadableSummary,
            ProposedAfter = WoundMaterializationContract.SerializeCanonical(
                draft.ProposedAfter),
            Definitions = draft.EffectDefinitions.Select(static value => new
            {
                value.LocalEffectRef,
                Definition = CanonicalJson(value.Definition)
            }),
            Roots = draft.RootApplications,
            Slots = draft.SlotBindings
        });

    private static WoundAcceptedTurnIdentityScope ScopeFor(
        WoundAcceptedTransitionDraft transition,
        WoundAcceptedTurnInput input) =>
        new(
            input.Binding.SessionId,
            input.Binding.RequestId,
            input.Binding.SnapshotToken,
            input.Binding.Realm,
            input.Binding.Turn,
            input.Binding.AcceptedEventsFingerprint,
            transition.ProposedAfter.Origin.EventRef,
            transition.OpportunityId,
            transition.ProposedAfter.Owner,
            transition.Kind,
            transition.OperationKey,
            transition.LocalWoundRef);

    private static void AssertScopeMatches(
        WoundAcceptedTurnInput input,
        WoundAcceptedTurnIdentityScope scope)
    {
        var transition = Assert.Single(input.Transitions, value =>
            string.Equals(value.LocalWoundRef, scope.LocalWoundRef,
                StringComparison.Ordinal));
        Assert.Equal(ScopeFor(transition, input), scope);
    }

    private static ScopeAllocation AllocateScopeTriplet(
        IWoundAcceptedTurnIdentityAllocator allocator,
        WoundAcceptedTurnIdentityScope scope) =>
        new(
            allocator.CreateWoundId(scope),
            allocator.CreateApplicationRef(
                scope,
                "draft_application_001_001",
                "wound_definition_001_001",
                "operation_root_001_001"),
            allocator.CreateTransitionId(scope, "draft_transition_001"));

    private static void AssertBindingEqual(
        WoundAcceptedTurnBinding expected,
        WoundAcceptedTurnBinding actual)
    {
        Assert.Equal(expected.SessionId, actual.SessionId);
        Assert.Equal(expected.RequestId, actual.RequestId);
        Assert.Equal(expected.SnapshotToken, actual.SnapshotToken);
        Assert.Equal(expected.Realm, actual.Realm);
        Assert.Equal(expected.Turn, actual.Turn);
        Assert.Equal(expected.AcceptedEventsFingerprint,
            actual.AcceptedEventsFingerprint);
        Assert.Equal(expected.AcceptedEvents, actual.AcceptedEvents);
    }

    private static WoundOwnerCoordinate CreateOwner(OwnerFlavor flavor) => flavor switch
    {
        OwnerFlavor.Player => new WoundOwnerCoordinate(
            Realm,
            "player",
            "player_current",
            WoundCarrierCatalog.PlayerPath),
        OwnerFlavor.Npc => new WoundOwnerCoordinate(
            Realm,
            "npc",
            "npc_wound_batch",
            WoundCarrierCatalog.NpcPath),
        OwnerFlavor.Combatant => new WoundOwnerCoordinate(
            Realm,
            "combatant",
            "combatant_wound_batch",
            WoundCarrierCatalog.EnemiesPath),
        OwnerFlavor.AfterlifeGuardian => new WoundOwnerCoordinate(
            "chaos_sea",
            "guardian",
            "guardian_wound_batch",
            WoundCarrierCatalog.AfterlifeProfilesPath),
        _ => throw new ArgumentOutOfRangeException(nameof(flavor), flavor, null)
    };

    private static WoundCarrierCatalogInput CreatePreTurnCarriers(OwnerFlavor flavor) =>
        flavor switch
        {
            OwnerFlavor.Player => new WoundCarrierCatalogInput(
                WoundContractTestData.CreatePlayerCarrier(),
                null,
                null,
                null,
                null),
            OwnerFlavor.Npc => new WoundCarrierCatalogInput(
                null,
                WoundContractTestData.CreateNamedNpcCarrier("npc_wound_batch"),
                null,
                null,
                null),
            OwnerFlavor.Combatant => new WoundCarrierCatalogInput(
                null,
                null,
                CreateCombatantWoundRoot(),
                null,
                null),
            OwnerFlavor.AfterlifeGuardian => new WoundCarrierCatalogInput(
                null,
                null,
                null,
                null,
                CreateAfterlifeWoundRoot()),
            _ => throw new ArgumentOutOfRangeException(nameof(flavor), flavor, null)
        };

    private static JsonObject CreateCombatantWoundRoot() => new()
    {
        ["enemiesData"] = new JsonArray(new JsonObject
        {
            ["combatantId"] = "combatant_wound_batch",
            ["displayName"] = "Exact wounded combatant",
            ["isGroup"] = false,
            ["activeBuffs"] = new JsonArray(),
            ["activeDebuffs"] = new JsonArray(),
            ["activeWounds"] = new JsonArray()
        })
    };

    private static JsonObject CreateAfterlifeWoundRoot() => new()
    {
        ["schemaVersion"] = 1,
        ["profiles"] = new JsonArray(new JsonObject
        {
            ["actorType"] = "guardian",
            ["actorId"] = "guardian_wound_batch",
            ["realm"] = "Chaos Sea",
            ["displayName"] = "Keeper of exact wounds",
            ["activeWounds"] = new JsonArray(),
            ["activeEffects"] = new JsonArray(),
            ["services"] = new JsonObject { ["revision"] = 1 }
        }),
        ["profileAudit"] = new JsonObject { ["lastTurn"] = Turn }
    };

    private static WoundAcceptedTurnInput WithExistingWound(
        WoundAcceptedTurnInput input,
        string woundId)
    {
        var owner = input.Transitions[0].ProposedAfter.Owner;
        var transitionId = "wound_transition_" + woundId["wound_".Length..];
        var eventRef = $"turn_{Turn - 1}:existing:{woundId}";
        var json = WoundContractTestData.CreateActiveWound(
            woundId,
            owner.Realm,
            owner.OwnerKind,
            owner.OwnerId,
            owner.CarrierPath,
            domain: input.Transitions[0].ProposedAfter.Classification.Domain);
        json["origin"]!["eventRef"] = eventRef;
        json["origin"]!["createdAtTurn"] = Turn - 1;
        json["origin"]!["opportunityId"] = "opportunity_" + woundId;
        json["severity"]!["lastChangeEventRef"] = eventRef;
        json["lastTransition"]!["transitionId"] = transitionId;
        json["lastTransition"]!["turn"] = Turn - 1;
        json["consequences"] = new JsonObject
        {
            ["slotBudget"] = 2,
            ["slotsUsed"] = 0,
            ["entries"] = new JsonArray(),
            ["ownedEffectSources"] = new JsonObject
            {
                ["definitions"] = new JsonArray(),
                ["rootBindings"] = new JsonArray()
            }
        };
        var parsed = WoundMaterializationContract.Parse(
            json.ToJsonString(),
            "existingWound");
        Assert.True(parsed.IsValid);
        var wound = Assert.IsType<WoundMaterializationEnvelope>(parsed.Wound);
        var semantic = WoundIdentityState.ComputeSemanticFingerprint(wound);
        var identity = WoundContractTestData.CreateIdentityIndex(
            WoundContractTestData.CreateIdentityEntry(
                woundId,
                owner.Realm,
                owner.OwnerKind,
                owner.OwnerId,
                owner.CarrierPath,
                wound.Classification.Domain,
                createdAtTurn: Turn - 1,
                createdEventRef: eventRef,
                semanticFingerprint: semantic));
        var historyRow = WoundContractTestData.CreateTransition();
        historyRow["transitionId"] = transitionId;
        historyRow["woundId"] = woundId;
        historyRow["turn"] = Turn - 1;
        historyRow["eventRef"] = eventRef;
        historyRow["operationKey"] = "operation_" + woundId;
        historyRow["beforeFingerprint"] =
            WoundHistoryState.ComputeNonexistentBeforeFingerprint(woundId);
        historyRow["afterFingerprint"] = semantic;
        historyRow["sourceFingerprint"] = Fingerprint("existing:" + woundId);
        historyRow["attemptId"] = null;
        var carriers = input.PreTurnCarriers with
        {
            PlayerWounds = WoundContractTestData.CreatePlayerCarrier(
                JsonNode.Parse(WoundMaterializationContract.SerializeCanonical(wound))!
                    .AsObject())
        };
        return input with
        {
            PreTurnCarriers = carriers,
            PreTurnIdentityIndex = identity,
            PreTurnHistory = WoundContractTestData.CreateHistory(historyRow)
        };
    }

    private static EffectStageFixture BuildEffectStage(
        WoundPreparedAcceptedTurnPlan prepared,
        EffectIdentityFactory identityFactory,
        bool includeIndependentEffect = false,
        Action<List<EffectSourceExport>>? mutateWoundExports = null)
    {
        var input = CreateEffectInput(
            prepared,
            includeIndependentEffect,
            mutateWoundExports);
        return new EffectStageFixture(
            input,
            WoundEffectBatchPlanner.Build(prepared, input, identityFactory));
    }

    private static EffectCarrierCatalogInput CreateEffectCarriersFromPlan(
        EffectAcceptedTurnPlan plan) =>
        new(
            plan.CarrierAfterImages.GetValueOrDefault(
                EffectCarrierCatalog.PlayerPath),
            plan.CarrierAfterImages.GetValueOrDefault(
                EffectCarrierCatalog.NpcPath),
            plan.CarrierAfterImages.GetValueOrDefault(
                EffectCarrierCatalog.EnemiesPath),
            plan.CarrierAfterImages.GetValueOrDefault(
                EffectCarrierCatalog.AlliesPath),
            plan.CarrierAfterImages.GetValueOrDefault(
                EffectCarrierCatalog.AfterlifeProfilesPath),
            plan.CarrierAfterImages.GetValueOrDefault(
                EffectCarrierCatalog.SpiritualConflictPath));

    private static EffectIdentityState ParseEffectIdentityState(JsonObject root)
    {
        using var document = JsonDocument.Parse(root.ToJsonString());
        var parsed = EffectIdentityState.Parse(
            document.RootElement,
            EffectAcceptedTurnPlan.IdentityIndexPath);
        Assert.Empty(parsed.Issues);
        return Assert.IsType<EffectIdentityState>(parsed.State);
    }

    private static WoundPreparedAcceptedTurnPlan CreateTerminalPreparedPlan(
        WoundPreparedAcceptedTurnPlan prepared,
        WoundMaterializationEnvelope wound,
        WoundTerminalEffectOperation operation)
    {
        var original = Assert.Single(prepared.EffectOperationBatches);
        var lineage = wound.Consequences.OwnedEffectSources.RootBindings
            .OrderBy(static value => value.EffectId, StringComparer.Ordinal)
            .ThenBy(static value => value.DefinitionKey, StringComparer.Ordinal)
            .Select(static value => new WoundRootLineageAuthorityRow(
                null,
                value.EffectId,
                value.DefinitionKey,
                WoundRootOwnershipDomain.BaseWound))
            .ToArray();
        var provisionalBatch = new WoundEffectOperationBatch(
            original.LocalWoundRef,
            original.PreparedWoundId,
            original.SourceExport,
            Array.Empty<WoundRootEffectApplication>(),
            new[] { operation },
            lineage,
            string.Empty,
            original.TransitionAuthority);
        var batch = new WoundEffectOperationBatch(
            provisionalBatch.LocalWoundRef,
            provisionalBatch.PreparedWoundId,
            provisionalBatch.SourceExport,
            provisionalBatch.RootApplications,
            provisionalBatch.TerminalOperations,
            provisionalBatch.RootLineageAuthority,
            WoundAcceptedTurnFingerprints.ComputeSourceExport(provisionalBatch),
            provisionalBatch.TransitionAuthority);
        var provisional = new WoundPreparedAcceptedTurnPlan(
            prepared.Binding,
            prepared.BindingFingerprint,
            prepared.InputFingerprint,
            string.Empty,
            prepared.AllocatedWoundIds,
            prepared.AllocatedTransitionIds,
            new[] { wound },
            new[] { batch },
            prepared.BaselineAuthority);
        return new WoundPreparedAcceptedTurnPlan(
            provisional.Binding,
            provisional.BindingFingerprint,
            provisional.InputFingerprint,
            WoundAcceptedTurnFingerprints.ComputePreparation(provisional),
            provisional.AllocatedWoundIds,
            provisional.AllocatedTransitionIds,
            provisional.PreparedWounds,
            provisional.EffectOperationBatches,
            provisional.BaselineAuthority);
    }

    private static EffectAcceptedTurnInput CreateEffectInput(
        WoundPreparedAcceptedTurnPlan prepared,
        bool includeIndependentEffect = false,
        Action<List<EffectSourceExport>>? mutateWoundExports = null,
        IReadOnlyList<EffectSourceExport>? additionalPreTurnSources = null,
        EffectCarrierCatalogInput? preTurnCarriersOverride = null,
        JsonObject? preTurnIdentityIndexOverride = null,
        Action<JsonObject>? mutateEventInput = null)
    {
        var woundExports = prepared.EffectOperationBatches.Select(batch =>
            new EffectSourceExport(
                batch.SourceExport.Realm,
                batch.SourceExport.Kind,
                batch.SourceExport.SourceId,
                new JsonArray(batch.SourceExport.Definitions.Select(static value =>
                    (JsonNode)value.Definition.DeepClone()).ToArray()),
                Materializable: false,
                Active: true,
                SameTurn: true,
                SourceRef: batch.SourceExport.SourceRef)).ToList();
        mutateWoundExports?.Invoke(woundExports);
        var preTurnSources = new List<EffectSourceExport>();
        var commands = new List<JsonObject>();
        if (includeIndependentEffect)
        {
            var definition = EffectMaterializationTestFixture.CreateDefinition();
            definition["definitionKey"] = "independent_quest_definition";
            preTurnSources.Add(new EffectSourceExport(
                prepared.Binding.Realm,
                "quest",
                "quest_independent",
                new JsonArray(definition),
                Materializable: true,
                Active: true,
                SameTurn: false));
            var command = EffectMaterializationTestFixture.CreateApplyCommand();
            command["source"] = new JsonObject
            {
                ["kind"] = "quest",
                ["sourceId"] = "quest_independent",
                ["definitionKey"] = "independent_quest_definition"
            };
            var accepted = prepared.Binding.AcceptedEvents[0];
            command["eventRef"] = new JsonObject
            {
                ["kind"] = accepted.Kind,
                ["authorityId"] = accepted.AuthorityId
            };
            commands.Add(command);
        }
        preTurnSources.AddRange(
            additionalPreTurnSources ?? Array.Empty<EffectSourceExport>());

        var woundGroups = prepared.EffectOperationBatches.Select(batch =>
        {
            Assert.True(WoundEffectCarrierAdapter.TryCreateTargetKey(
                batch.SourceExport.Owner,
                out var target));
            return new WoundSourceGroupAuthority(
                new EffectIdentitySourceGroup(
                    batch.SourceExport.Realm,
                    batch.SourceExport.Kind,
                    batch.SourceExport.SourceId),
                batch.SourceExport.Owner,
                target,
                sameTurn: true,
                batch.SourceExport.SourceRef,
                batch.SourceExportFingerprint,
                batch.SourceExport.Definitions
                    .Select(static definition =>
                        new WoundEffectSourceDefinition(
                            definition.DefinitionKey,
                            definition.Definition))
                    .ToArray(),
                batch.RootLineageAuthority
                    .Where(static row => row.ApplicationRef is not null)
                    .ToArray(),
                batch.RootLineageAuthority
                    .Where(static row => row.EffectId is not null)
                    .ToArray());
        }).ToArray();

        var sourceAuthority = EffectSourceAuthority.Build(new EffectSourceAuthorityInput(
            preTurnSources,
            woundExports,
            new HashSet<string>(StringComparer.Ordinal),
            WoundGroups: woundGroups));
        var targets = prepared.EffectOperationBatches
            .SelectMany(static batch => batch.RootApplications)
            .Select(static application => application.ExpectedTargetKey)
            .Distinct()
            .Select(static key => new EffectTargetExport(
                key.Realm,
                key.Kind,
                key.TargetId,
                SameTurn: false))
            .ToArray();
        if (includeIndependentEffect && targets.Length == 0)
        {
            targets = new[]
            {
                new EffectTargetExport(
                    prepared.Binding.Realm,
                    "player",
                    "player_current",
                    SameTurn: false)
            };
        }
        var targetAuthority = EffectTargetAuthority.Build(new EffectTargetAuthorityInput(
            targets,
            Array.Empty<EffectTargetExport>(),
            new HashSet<string>(StringComparer.Ordinal),
            CombatantIdentities: null));
        var eventInput = new JsonObject
        {
            ["turn"] = prepared.Binding.Turn,
            ["events"] = new JsonArray(prepared.Binding.AcceptedEvents.Select(static value =>
                (JsonNode)new JsonObject
                {
                    ["eventRef"] = value.EventRef,
                    ["kind"] = value.Kind,
                    ["authorityId"] = value.AuthorityId
                }).ToArray())
        };
        mutateEventInput?.Invoke(eventInput);
        var woundCarriers = prepared.BaselineAuthority.PreTurnCarriers;
        var hasSharedCarrier = woundCarriers.EnemyCombatants is not null ||
            woundCarriers.AllyCombatants is not null ||
            woundCarriers.AfterlifeProfiles is not null;
        var preTurnCarriers = preTurnCarriersOverride ?? (!hasSharedCarrier
            ? null
            : new EffectCarrierCatalogInput(
                null,
                null,
                woundCarriers.EnemyCombatants,
                woundCarriers.AllyCombatants,
                woundCarriers.AfterlifeProfiles,
                null));
        return new EffectAcceptedTurnInput(
            prepared.Binding.SessionId,
            prepared.Binding.SnapshotToken,
            EffectMaterializationTestFixture.CreateCommandRoot(commands.ToArray()),
            sourceAuthority,
            targetAuthority,
            eventInput,
            prepared.Binding.Realm,
            PreTurnCarriers: preTurnCarriers,
            PreTurnIdentityIndex: preTurnIdentityIndexOverride ?? new JsonObject
            {
                ["schemaVersion"] = 1,
                ["entries"] = new JsonArray()
            });
    }

    private static (
        EffectAcceptedTurnInput Before,
        EffectAcceptedTurnInput After) CreateEffectAuthorityDriftPair(
        EffectAcceptedTurnInput source,
        WoundPreparedAcceptedTurnPlan prepared,
        string mutation)
    {
        var beforeSource = source.SourceAuthority;
        var afterSource = source.SourceAuthority;
        var beforeTarget = source.TargetAuthority;
        var afterTarget = source.TargetAuthority;
        switch (mutation)
        {
            case "source_same_turn":
                afterSource = BuildSourceAuthority(
                    sameTurn: false,
                    sourceRefSuffix: null,
                    grant: false);
                break;
            case "source_ref":
                afterSource = BuildSourceAuthority(
                    sameTurn: true,
                    sourceRefSuffix: "_changed",
                    grant: false);
                break;
            case "source_grant":
                afterSource = BuildSourceAuthority(
                    sameTurn: true,
                    sourceRefSuffix: string.Empty,
                    grant: true);
                break;
            case "target_same_turn":
                afterTarget = BuildTargetAuthority(
                    sameTurn: true,
                    targetRef: "target_ref_same_turn");
                break;
            case "target_ref":
                beforeTarget = BuildTargetAuthority(
                    sameTurn: true,
                    targetRef: "target_ref_before");
                afterTarget = BuildTargetAuthority(
                    sameTurn: true,
                    targetRef: "target_ref_after");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null);
        }

        return (
            source with
            {
                SourceAuthority = beforeSource,
                TargetAuthority = beforeTarget
            },
            source with
            {
                SourceAuthority = afterSource,
                TargetAuthority = afterTarget
            });

        EffectSourceAuthority BuildSourceAuthority(
            bool sameTurn,
            string? sourceRefSuffix,
            bool grant)
        {
            var exports = prepared.EffectOperationBatches.Select(batch =>
                new EffectSourceExport(
                    batch.SourceExport.Realm,
                    batch.SourceExport.Kind,
                    batch.SourceExport.SourceId,
                    new JsonArray(batch.SourceExport.Definitions.Select(static value =>
                        (JsonNode)value.Definition.DeepClone()).ToArray()),
                    Materializable: false,
                    Active: true,
                    SameTurn: sameTurn,
                     SourceRef: sameTurn
                         ? batch.SourceExport.SourceRef + sourceRefSuffix
                         : null)).ToArray();
            var woundGroups = prepared.EffectOperationBatches.Select(batch =>
            {
                Assert.True(WoundEffectCarrierAdapter.TryCreateTargetKey(
                    batch.SourceExport.Owner,
                    out var target));
                return new WoundSourceGroupAuthority(
                    new EffectIdentitySourceGroup(
                        batch.SourceExport.Realm,
                        batch.SourceExport.Kind,
                        batch.SourceExport.SourceId),
                    batch.SourceExport.Owner,
                    target,
                    sameTurn,
                    sameTurn
                        ? batch.SourceExport.SourceRef + sourceRefSuffix
                        : null,
                    sameTurn ? batch.SourceExportFingerprint : null,
                    batch.SourceExport.Definitions
                        .Select(static definition =>
                            new WoundEffectSourceDefinition(
                                definition.DefinitionKey,
                                definition.Definition))
                        .ToArray(),
                    batch.RootLineageAuthority
                        .Where(static row => row.ApplicationRef is not null)
                        .ToArray(),
                    batch.RootLineageAuthority
                        .Where(static row => row.EffectId is not null)
                        .ToArray());
            }).ToArray();
            return EffectSourceAuthority.Build(new EffectSourceAuthorityInput(
                sameTurn ? Array.Empty<EffectSourceExport>() : exports,
                sameTurn ? exports : Array.Empty<EffectSourceExport>(),
                new HashSet<string>(StringComparer.Ordinal),
                grant
                    ? new HashSet<string>(StringComparer.Ordinal)
                    {
                        EffectBuiltInSourceCatalog.FateShieldApplicationAuthority
                    }
                    : new HashSet<string>(StringComparer.Ordinal),
                WoundGroups: woundGroups));
        }

        EffectTargetAuthority BuildTargetAuthority(bool sameTurn, string? targetRef)
        {
            var targets = prepared.EffectOperationBatches
                .SelectMany(static batch => batch.RootApplications)
                .Select(static application => application.ExpectedTargetKey)
                .Distinct()
                .Select(key => new EffectTargetExport(
                    key.Realm,
                    key.Kind,
                    key.TargetId,
                    SameTurn: sameTurn,
                    TargetRef: targetRef))
                .ToArray();
            return EffectTargetAuthority.Build(
                new EffectTargetAuthorityInput(
                    sameTurn ? Array.Empty<EffectTargetExport>() : targets,
                    sameTurn ? targets : Array.Empty<EffectTargetExport>(),
                    new HashSet<string>(StringComparer.Ordinal),
                    CombatantIdentities: null));
        }
    }

    private static WoundEffectBatchAcceptedPlan AssertEffectPlan(
        WoundEffectBatchPlanningResult result)
    {
        Assert.True(
            result.Success,
            string.Join(Environment.NewLine, result.Issues.Select(static issue =>
                $"{issue.Code}: {issue.Message} " +
                $"(expected={issue.Expected}; actual={issue.Actual}; path={issue.FilePath})")));
        Assert.Empty(result.Issues);
        return Assert.IsType<WoundEffectBatchAcceptedPlan>(result.Plan);
    }

    private static WoundPreparedAcceptedTurnPlan AssertPrepared(
        WoundAcceptedTurnPreparationResult result)
    {
        Assert.True(
            result.Success,
            string.Join(Environment.NewLine, result.Issues.Select(static issue =>
                $"{issue.Code}: {issue.Message} " +
                $"(expected={issue.Expected}; actual={issue.Actual}; path={issue.FilePath})")));
        Assert.Empty(result.Issues);
        return Assert.IsType<WoundPreparedAcceptedTurnPlan>(result.Plan);
    }

    private static WoundAcceptedTurnPlan AssertFinalized(
        WoundAcceptedTurnPlanningResult result)
    {
        Assert.True(
            result.Success,
            string.Join(Environment.NewLine, result.Issues.Select(static issue =>
                $"{issue.Code}: {issue.Message}")));
        Assert.Empty(result.Issues);
        return Assert.IsType<WoundAcceptedTurnPlan>(result.Plan);
    }

    private static void AssertInvalidPreparation(
        WoundAcceptedTurnPreparationResult result)
    {
        Assert.False(result.Success);
        Assert.Null(result.Plan);
        Assert.NotNull(result.Issues);
        Assert.NotEmpty(result.Issues);
    }

    private static void AssertInvalidFinalization(
        WoundAcceptedTurnPlanningResult result)
    {
        Assert.False(result.Success);
        Assert.Null(result.Plan);
        Assert.NotNull(result.Issues);
        Assert.NotEmpty(result.Issues);
    }

    private static void AssertNoWoundAllocations(
        RecordingWoundIdentityAllocator allocator)
    {
        Assert.Empty(allocator.WoundRequests);
        Assert.Empty(allocator.ApplicationRequests);
        Assert.Empty(allocator.TransitionRequests);
    }

    private static void AssertReciprocalWoundLink(
        JsonObject definition,
        string woundId)
    {
        var link = Assert.IsType<JsonObject>(Assert.Single(definition["links"]!.AsArray()));
        Assert.Equal("wound", link["kind"]!.GetValue<string>());
        Assert.Equal(woundId, link["targetId"]!.GetValue<string>());
        Assert.Equal("source", link["role"]!.GetValue<string>());
    }

    private static void AssertSlotAgreementEqual(
        IReadOnlyList<WoundEffectSlotAgreement> expected,
        IReadOnlyList<WoundEffectSlotAgreement> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        for (var index = 0; index < expected.Count; index++)
            Assert.Equal(expected[index], actual[index]);
    }

    private static JsonObject FindActiveEffect(
        WoundEffectBatchAcceptedPlan plan,
        string effectId) => FindActiveEffect(plan.EffectPlan, effectId);

    private static JsonObject FindActiveEffect(
        EffectAcceptedTurnPlan plan,
        string effectId) =>
        Assert.Single(plan.ActiveEffects, effect => string.Equals(
            effect["effectId"]?.GetValue<string>(),
            effectId,
            StringComparison.Ordinal));

    private static EffectSourceKey ReadActiveEffectSourceKey(JsonObject activeEffect)
    {
        var source = activeEffect["source"]!.AsObject();
        return new EffectSourceKey(
            activeEffect["realm"]!.GetValue<string>(),
            source["kind"]!.GetValue<string>(),
            source["sourceId"]!.GetValue<string>(),
            source["definitionKey"]!.GetValue<string>());
    }

    private static void AssertActiveEffectComponentsDiffer(
        EffectAcceptedTurnPlan expected,
        EffectAcceptedTurnPlan actual,
        string effectId)
    {
        Assert.NotEqual(
            FindActiveEffect(expected, effectId)["components"]!.ToJsonString(),
            FindActiveEffect(actual, effectId)["components"]!.ToJsonString());
        Assert.NotEqual(
            FindCarrierActiveEffect(expected, effectId)["components"]!.ToJsonString(),
            FindCarrierActiveEffect(actual, effectId)["components"]!.ToJsonString());
        Assert.True(JsonNode.DeepEquals(
            FindActiveEffect(actual, effectId)["components"],
            FindCarrierActiveEffect(actual, effectId)["components"]));
    }

    private static void AssertActiveEffectSourcesDiffer(
        EffectAcceptedTurnPlan expected,
        EffectAcceptedTurnPlan actual,
        string effectId)
    {
        Assert.NotEqual(
            FindActiveEffect(expected, effectId)["source"]!.ToJsonString(),
            FindActiveEffect(actual, effectId)["source"]!.ToJsonString());
        Assert.NotEqual(
            FindCarrierActiveEffect(expected, effectId)["source"]!.ToJsonString(),
            FindCarrierActiveEffect(actual, effectId)["source"]!.ToJsonString());
        Assert.True(JsonNode.DeepEquals(
            FindActiveEffect(actual, effectId)["source"],
            FindCarrierActiveEffect(actual, effectId)["source"]));
        Assert.True(JsonNode.DeepEquals(
            FindActiveEffect(actual, effectId)["source"],
            FindIdentityIndexEffect(actual, effectId)["source"]));
        Assert.Equal(
            FindActiveEffect(actual, effectId)["source"]!["sourceId"]!
                .GetValue<string>(),
            FindIdentityIndexEffect(actual, effectId)["stackCoordinate"]!["sourceId"]!
                .GetValue<string>());
    }

    private static JsonObject FindCarrierActiveEffect(
        EffectAcceptedTurnPlan plan,
        string effectId) =>
        Assert.Single(plan.CarrierAfterImages[EffectCarrierCatalog.PlayerPath]
            ["activeEffects"]!.AsArray().OfType<JsonObject>(), effect => string.Equals(
                effect["effectId"]?.GetValue<string>(),
                effectId,
                StringComparison.Ordinal));

    private static JsonObject FindIdentityIndexEffect(
        EffectAcceptedTurnPlan plan,
        string effectId) =>
        Assert.Single(plan.IdentityIndexAfterImage["entries"]!
            .AsArray().OfType<JsonObject>(), entry => string.Equals(
                entry["effectId"]?.GetValue<string>(),
                effectId,
                StringComparison.Ordinal));

    private static JsonObject FindPlayerCarrierEffect(
        EffectAcceptedTurnPlan plan,
        string effectId) =>
        Assert.Single(plan.CarrierAfterImages[EffectCarrierCatalog.PlayerPath]
            ["activeEffects"]!.AsArray().OfType<JsonObject>(), effect => string.Equals(
                effect["effectId"]?.GetValue<string>(),
                effectId,
                StringComparison.Ordinal));

    private static EffectAcceptedTurnPlan CloneEffectPlanWithMutatedActiveEffect(
        EffectAcceptedTurnPlan source,
        string effectId,
        string changedComponentId) =>
        CloneEffectPlanWithActiveEffectMutation(
            source,
            effectId,
            activeEffect =>
            {
                var component = Assert.IsType<JsonObject>(
                    Assert.Single(activeEffect["components"]!.AsArray()));
                component["componentId"] = changedComponentId;
            });

    private static EffectAcceptedTurnPlan CloneEffectPlanWithMutatedActiveEffectSource(
        EffectAcceptedTurnPlan source,
        string effectId,
        string changedSourceId) =>
        CloneEffectPlanWithActiveEffectMutation(
            source,
            effectId,
            activeEffect =>
                activeEffect["source"]!["sourceId"] = changedSourceId,
            identityIndex =>
            {
                var identity = Assert.Single(identityIndex["entries"]!
                    .AsArray().OfType<JsonObject>(), entry => string.Equals(
                        entry["effectId"]?.GetValue<string>(),
                        effectId,
                        StringComparison.Ordinal));
                identity["source"]!["sourceId"] = changedSourceId;
                identity["stackCoordinate"]!["sourceId"] = changedSourceId;
            });

    private static EffectAcceptedTurnPlan CloneEffectPlanWithActiveEffectMutation(
        EffectAcceptedTurnPlan source,
        string effectId,
        Action<JsonObject> mutateActiveEffect,
        Action<JsonObject>? mutateIdentityIndex = null)
    {
        var activeEffects = source.ActiveEffects
            .Select(static effect => effect.DeepClone().AsObject())
            .ToArray();
        MutateActiveEffect(activeEffects, effectId, mutateActiveEffect);

        var carrierAfterImages = source.CarrierAfterImages.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value.DeepClone().AsObject(),
            StringComparer.Ordinal);
        var playerEffects = carrierAfterImages[EffectCarrierCatalog.PlayerPath]
            ["activeEffects"]!.AsArray()
            .OfType<JsonObject>()
            .ToArray();
        MutateActiveEffect(playerEffects, effectId, mutateActiveEffect);
        var identityIndexAfterImage = source.IdentityIndexAfterImage;
        mutateIdentityIndex?.Invoke(identityIndexAfterImage);

        return new EffectAcceptedTurnPlan(
            source.InputFingerprint,
            source.CarrierAuthorityFingerprint,
            source.SourceAuthorityFingerprint,
            source.TargetAuthorityFingerprint,
            source.AllocatedCombatantIds,
            source.AllocatedEffectIds,
            source.AllocatedTransitionIds,
            source.Sources,
            source.Targets,
            source.SourceBindings,
            source.DeferredReactions,
            source.ReactionExpansionCount,
            source.ReactionExpansionUsage,
            activeEffects,
            source.ResourceTriggerCarriers,
            source.SourceAuthority,
            source.TargetAuthority,
            source.EventInput,
            source.CarrierBeforeImages,
            carrierAfterImages,
            source.IdentityIndexBeforeImage,
            identityIndexAfterImage,
            source.TouchedPaths,
            source.DeletedPaths,
            source.AcceptedCarrierBaselines,
            acceptedBoundaryCompletionProof:
                ReadAcceptedBoundaryCompletionProof(source),
            acceptedBoundaryBasePlanFingerprint:
                source.AcceptedBoundaryBasePlanFingerprint,
            woundApplicationRootEffectBindings:
                source.WoundApplicationRootEffectBindings);
    }

    private static EffectAcceptedTurnPlan CloneEffectPlanWithCarrierAfterImageMutation(
        EffectAcceptedTurnPlan source,
        string effectId,
        Action<JsonObject> mutateActiveEffect)
    {
        var carrierAfterImages = source.CarrierAfterImages.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value.DeepClone().AsObject(),
            StringComparer.Ordinal);
        var playerEffects = carrierAfterImages[EffectCarrierCatalog.PlayerPath]
            ["activeEffects"]!.AsArray()
            .OfType<JsonObject>()
            .ToArray();
        MutateActiveEffect(playerEffects, effectId, mutateActiveEffect);

        return new EffectAcceptedTurnPlan(
            source.InputFingerprint,
            source.CarrierAuthorityFingerprint,
            source.SourceAuthorityFingerprint,
            source.TargetAuthorityFingerprint,
            source.AllocatedCombatantIds,
            source.AllocatedEffectIds,
            source.AllocatedTransitionIds,
            source.Sources,
            source.Targets,
            source.SourceBindings,
            source.DeferredReactions,
            source.ReactionExpansionCount,
            source.ReactionExpansionUsage,
            source.ActiveEffects,
            source.ResourceTriggerCarriers,
            source.SourceAuthority,
            source.TargetAuthority,
            source.EventInput,
            source.CarrierBeforeImages,
            carrierAfterImages,
            source.IdentityIndexBeforeImage,
            source.IdentityIndexAfterImage,
            source.TouchedPaths,
            source.DeletedPaths,
            source.AcceptedCarrierBaselines,
            acceptedBoundaryCompletionProof:
                ReadAcceptedBoundaryCompletionProof(source),
            acceptedBoundaryBasePlanFingerprint:
                source.AcceptedBoundaryBasePlanFingerprint,
            woundApplicationRootEffectBindings:
                source.WoundApplicationRootEffectBindings);
    }

    private static EffectAcceptedTurnPlan CloneEffectPlanWithInjectedSurvivorMutation(
        EffectAcceptedTurnPlan source,
        string terminalEffectId,
        string survivorEffectId,
        string mutation)
    {
        static JsonObject CreateSurvivorEffect(
            JsonObject sourceEffect,
            string effectId)
        {
            var survivor = sourceEffect.DeepClone().AsObject();
            survivor["effectId"] = effectId;
            survivor["chronology"]!["createdEventRef"] =
                "turn_42:wound_survivor_fixture_created";
            survivor["chronology"]!["lastTransitionId"] =
                "effect_transition_wound_survivor_fixture_created";
            return survivor;
        }

        static JsonObject CreateSurvivorIdentity(
            JsonObject sourceIdentity,
            string effectId)
        {
            var survivor = sourceIdentity.DeepClone().AsObject();
            survivor["effectId"] = effectId;
            var create = Assert.IsType<JsonObject>(
                Assert.Single(survivor["transitions"]!.AsArray()));
            create["transitionId"] =
                "effect_transition_wound_survivor_fixture_created";
            create["eventRef"] = "turn_42:wound_survivor_fixture_created";
            create["resultEffectIds"] = new JsonArray(effectId);
            return survivor;
        }

        var carrierBeforeImages = source.CarrierBeforeImages.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value?.DeepClone().AsObject(),
            StringComparer.Ordinal);
        var beforePlayerEffects = Assert.IsType<JsonObject>(
            carrierBeforeImages[EffectCarrierCatalog.PlayerPath]);
        var terminalEffect = Assert.Single(
            beforePlayerEffects["activeEffects"]!.AsArray().OfType<JsonObject>(),
            effect => string.Equals(
                effect["effectId"]?.GetValue<string>(),
                terminalEffectId,
                StringComparison.Ordinal));
        var survivorBeforeEffect = CreateSurvivorEffect(
            terminalEffect,
            survivorEffectId);
        beforePlayerEffects["activeEffects"]!.AsArray().Add(
            survivorBeforeEffect.DeepClone());

        var resourceTriggerCarriers = source.ResourceTriggerCarriers;
        var resourcePlayerEffects = Assert.IsType<JsonObject>(
            resourceTriggerCarriers.PlayerEffects).DeepClone().AsObject();
        var survivorResourceEffect = survivorBeforeEffect.DeepClone().AsObject();
        resourcePlayerEffects["activeEffects"]!.AsArray().Add(
            survivorResourceEffect);
        resourceTriggerCarriers = resourceTriggerCarriers with
        {
            PlayerEffects = resourcePlayerEffects
        };
        var carrierAfterImages = source.CarrierAfterImages.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value.DeepClone().AsObject(),
            StringComparer.Ordinal);
        var survivorPublicationEffect = survivorBeforeEffect.DeepClone().AsObject();
        carrierAfterImages[EffectCarrierCatalog.PlayerPath]
            ["activeEffects"]!.AsArray().Add(survivorPublicationEffect);
        var identityIndexBeforeImage = Assert.IsType<JsonObject>(
            source.IdentityIndexBeforeImage);
        var terminalIdentityBefore = Assert.Single(
            identityIndexBeforeImage["entries"]!.AsArray().OfType<JsonObject>(),
            identity => string.Equals(
                identity["effectId"]?.GetValue<string>(),
                terminalEffectId,
                StringComparison.Ordinal));
        var survivorBeforeIdentity = CreateSurvivorIdentity(
            terminalIdentityBefore,
            survivorEffectId);
        identityIndexBeforeImage["entries"]!.AsArray().Add(
            survivorBeforeIdentity.DeepClone());
        var identityIndexAfterImage = source.IdentityIndexAfterImage;
        var survivorAfterIdentity = survivorBeforeIdentity.DeepClone().AsObject();
        identityIndexAfterImage["entries"]!.AsArray().Add(survivorAfterIdentity);
        var acceptedCarrierBaselines = source.AcceptedCarrierBaselines;
        var acceptedPlayerEffects = Assert.IsType<JsonObject>(
            acceptedCarrierBaselines.PlayerEffects).DeepClone().AsObject();
        acceptedPlayerEffects["activeEffects"]!.AsArray().Add(
            survivorBeforeEffect.DeepClone());
        acceptedCarrierBaselines = acceptedCarrierBaselines with
        {
            PlayerEffects = acceptedPlayerEffects
        };

        switch (mutation)
        {
            case "carrier":
                survivorResourceEffect["display"]!["name"] =
                    "Forged surviving wound effect";
                survivorPublicationEffect["display"]!["name"] =
                    "Forged surviving wound effect";
                break;
            case "identity":
                survivorAfterIdentity["owner"]!["ownerId"] =
                    "player_forged_survivor";
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(mutation),
                    mutation,
                    null);
        }

        return new EffectAcceptedTurnPlan(
            source.InputFingerprint,
            source.CarrierAuthorityFingerprint,
            source.SourceAuthorityFingerprint,
            source.TargetAuthorityFingerprint,
            source.AllocatedCombatantIds,
            source.AllocatedEffectIds,
            source.AllocatedTransitionIds,
            source.Sources,
            source.Targets,
            source.SourceBindings,
            source.DeferredReactions,
            source.ReactionExpansionCount,
            source.ReactionExpansionUsage,
            source.ActiveEffects,
            resourceTriggerCarriers,
            source.SourceAuthority,
            source.TargetAuthority,
            source.EventInput,
            carrierBeforeImages,
            carrierAfterImages,
            identityIndexBeforeImage,
            identityIndexAfterImage,
            source.TouchedPaths,
            source.DeletedPaths,
            acceptedCarrierBaselines,
            acceptedBoundaryCompletionProof:
                ReadAcceptedBoundaryCompletionProof(source),
            acceptedBoundaryBasePlanFingerprint:
                source.AcceptedBoundaryBasePlanFingerprint,
            woundApplicationRootEffectBindings:
                source.WoundApplicationRootEffectBindings);
    }

    private static EffectAcceptedTurnPlan CloneEffectPlanWithSingleViewSourceMutation(
        EffectAcceptedTurnPlan source,
        string effectId,
        EffectSourceKey preparedSource,
        string mutatedView)
    {
        static void RebindSource(JsonObject effect, EffectSourceKey sourceKey)
        {
            var effectSource = Assert.IsType<JsonObject>(effect["source"]);
            effectSource["kind"] = sourceKey.Kind;
            effectSource["sourceId"] = sourceKey.SourceId;
            effectSource["definitionKey"] = sourceKey.DefinitionKey;
        }

        var activeEffects = source.ActiveEffects.ToArray();
        var resourceTriggerCarriers = source.ResourceTriggerCarriers;
        var carrierAfterImages = source.CarrierAfterImages.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value.DeepClone().AsObject(),
            StringComparer.Ordinal);
        var identityIndexAfterImage = source.IdentityIndexAfterImage;

        switch (mutatedView)
        {
            case "active_effects":
                MutateActiveEffect(
                    activeEffects,
                    effectId,
                    effect => RebindSource(effect, preparedSource));
                break;
            case "resource_trigger_carriers":
            {
                var playerEffects = Assert.IsType<JsonObject>(
                    resourceTriggerCarriers.PlayerEffects)["activeEffects"]!
                    .AsArray().OfType<JsonObject>();
                MutateActiveEffect(
                    playerEffects,
                    effectId,
                    effect => RebindSource(effect, preparedSource));
                break;
            }
            case "carrier_after_images":
            {
                var playerEffects = carrierAfterImages[EffectCarrierCatalog.PlayerPath]
                    ["activeEffects"]!.AsArray().OfType<JsonObject>();
                MutateActiveEffect(
                    playerEffects,
                    effectId,
                    effect => RebindSource(effect, preparedSource));
                break;
            }
            case "identity_index":
            {
                var identity = Assert.Single(identityIndexAfterImage["entries"]!
                    .AsArray().OfType<JsonObject>(), entry => string.Equals(
                        entry["effectId"]?.GetValue<string>(),
                        effectId,
                        StringComparison.Ordinal));
                RebindSource(identity, preparedSource);
                var stackCoordinate = Assert.IsType<JsonObject>(
                    identity["stackCoordinate"]);
                stackCoordinate["sourceKind"] = preparedSource.Kind;
                stackCoordinate["sourceId"] = preparedSource.SourceId;
                break;
            }
            default:
                throw new InvalidOperationException(mutatedView);
        }

        return new EffectAcceptedTurnPlan(
            source.InputFingerprint,
            source.CarrierAuthorityFingerprint,
            source.SourceAuthorityFingerprint,
            source.TargetAuthorityFingerprint,
            source.AllocatedCombatantIds,
            source.AllocatedEffectIds,
            source.AllocatedTransitionIds,
            source.Sources,
            source.Targets,
            source.SourceBindings,
            source.DeferredReactions,
            source.ReactionExpansionCount,
            source.ReactionExpansionUsage,
            activeEffects,
            resourceTriggerCarriers,
            source.SourceAuthority,
            source.TargetAuthority,
            source.EventInput,
            source.CarrierBeforeImages,
            carrierAfterImages,
            source.IdentityIndexBeforeImage,
            identityIndexAfterImage,
            source.TouchedPaths,
            source.DeletedPaths,
            source.AcceptedCarrierBaselines,
            acceptedBoundaryCompletionProof:
                ReadAcceptedBoundaryCompletionProof(source),
            acceptedBoundaryBasePlanFingerprint:
                source.AcceptedBoundaryBasePlanFingerprint,
            woundApplicationRootEffectBindings:
                source.WoundApplicationRootEffectBindings);
    }

    private static EffectAcceptedTurnPlan CloneEffectPlanWithCreateTransitionId(
        EffectAcceptedTurnPlan source,
        string effectId,
        string transitionId)
    {
        static void MutateEffect(JsonObject effect, string value) =>
            effect["chronology"]!["lastTransitionId"] = value;

        var activeEffects = source.ActiveEffects
            .Select(static effect => effect.DeepClone().AsObject())
            .ToArray();
        MutateActiveEffect(
            activeEffects,
            effectId,
            effect => MutateEffect(effect, transitionId));

        var resourceTriggerCarriers = source.ResourceTriggerCarriers;
        var resourcePlayerEffects = resourceTriggerCarriers.PlayerEffects!
            ["activeEffects"]!.AsArray().OfType<JsonObject>().ToArray();
        MutateActiveEffect(
            resourcePlayerEffects,
            effectId,
            effect => MutateEffect(effect, transitionId));

        var carrierAfterImages = source.CarrierAfterImages.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value.DeepClone().AsObject(),
            StringComparer.Ordinal);
        var publicationPlayerEffects = carrierAfterImages[EffectCarrierCatalog.PlayerPath]
            ["activeEffects"]!.AsArray().OfType<JsonObject>().ToArray();
        MutateActiveEffect(
            publicationPlayerEffects,
            effectId,
            effect => MutateEffect(effect, transitionId));

        var identityIndexAfterImage = source.IdentityIndexAfterImage;
        var identity = Assert.Single(identityIndexAfterImage["entries"]!
            .AsArray().OfType<JsonObject>(), entry => string.Equals(
                entry["effectId"]?.GetValue<string>(),
                effectId,
                StringComparison.Ordinal));
        var transition = Assert.IsType<JsonObject>(
            Assert.Single(identity["transitions"]!.AsArray()));
        transition["transitionId"] = transitionId;

        return new EffectAcceptedTurnPlan(
            source.InputFingerprint,
            source.CarrierAuthorityFingerprint,
            source.SourceAuthorityFingerprint,
            source.TargetAuthorityFingerprint,
            source.AllocatedCombatantIds,
            source.AllocatedEffectIds,
            source.AllocatedTransitionIds,
            source.Sources,
            source.Targets,
            source.SourceBindings,
            source.DeferredReactions,
            source.ReactionExpansionCount,
            source.ReactionExpansionUsage,
            activeEffects,
            resourceTriggerCarriers,
            source.SourceAuthority,
            source.TargetAuthority,
            source.EventInput,
            source.CarrierBeforeImages,
            carrierAfterImages,
            source.IdentityIndexBeforeImage,
            identityIndexAfterImage,
            source.TouchedPaths,
            source.DeletedPaths,
            source.AcceptedCarrierBaselines,
            acceptedBoundaryCompletionProof:
                ReadAcceptedBoundaryCompletionProof(source),
            acceptedBoundaryBasePlanFingerprint:
                source.AcceptedBoundaryBasePlanFingerprint,
            woundApplicationRootEffectBindings:
                source.WoundApplicationRootEffectBindings);
    }

    private static EffectAcceptedTurnPlan CloneEffectPlanWithAuthorities(
        EffectAcceptedTurnPlan source,
        EffectSourceAuthority sourceAuthority,
        EffectTargetAuthority targetAuthority) =>
        new(
            source.InputFingerprint,
            source.CarrierAuthorityFingerprint,
            source.SourceAuthorityFingerprint,
            source.TargetAuthorityFingerprint,
            source.AllocatedCombatantIds,
            source.AllocatedEffectIds,
            source.AllocatedTransitionIds,
            source.Sources,
            source.Targets,
            source.SourceBindings,
            source.DeferredReactions,
            source.ReactionExpansionCount,
            source.ReactionExpansionUsage,
            source.ActiveEffects,
            source.ResourceTriggerCarriers,
            sourceAuthority,
            targetAuthority,
            source.EventInput,
            source.CarrierBeforeImages,
            source.CarrierAfterImages,
            source.IdentityIndexBeforeImage,
            source.IdentityIndexAfterImage,
            source.TouchedPaths,
            source.DeletedPaths,
            source.AcceptedCarrierBaselines,
            acceptedBoundaryCompletionProof:
                ReadAcceptedBoundaryCompletionProof(source),
            acceptedBoundaryBasePlanFingerprint:
                source.AcceptedBoundaryBasePlanFingerprint,
            woundApplicationRootEffectBindings:
                source.WoundApplicationRootEffectBindings);

    private static void MutateActiveEffect(
        IEnumerable<JsonObject> activeEffects,
        string effectId,
        Action<JsonObject> mutation)
    {
        var activeEffect = Assert.Single(activeEffects, effect => string.Equals(
            effect["effectId"]?.GetValue<string>(),
            effectId,
            StringComparison.Ordinal));
        mutation(activeEffect);
    }

    private static EffectAcceptedTurnPlanner.AcceptedBoundaryCompletionProof?
        ReadAcceptedBoundaryCompletionProof(EffectAcceptedTurnPlan plan) =>
        (EffectAcceptedTurnPlanner.AcceptedBoundaryCompletionProof?)typeof(
                EffectAcceptedTurnPlan)
            .GetField(
                "_acceptedBoundaryCompletionProof",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic)!
            .GetValue(plan);

    private static WoundEffectBatchPlanningResult RewrapEffectStage(
        WoundPreparedAcceptedTurnPlan prepared,
        EffectStageFixture stage,
        IReadOnlyList<EffectAcceptedApplicationResult> applications)
    {
        var accepted = AssertEffectPlan(stage.Result);
        return new WoundEffectBatchPlanningResult(
            WoundEffectBatchAcceptedPlan.Create(
                prepared,
                stage.Input,
                accepted.EffectPlan,
                applications,
                accepted.TerminationResults),
            Array.Empty<ValidationIssue>());
    }

    private static WoundPreparedAcceptedTurnPlan
        RewrapPreparedWithPrivateAuthorityMutation(
            WoundPreparedAcceptedTurnPlan source,
            string mutation)
    {
        var batches = source.EffectOperationBatches.ToArray();
        var baseline = source.BaselineAuthority;
        switch (mutation)
        {
            case "transition":
            {
                var batch = batches[0];
                batches[0] = new WoundEffectOperationBatch(
                    batch.LocalWoundRef,
                    batch.PreparedWoundId,
                    batch.SourceExport,
                    batch.RootApplications,
                    batch.TerminalOperations,
                    batch.RootLineageAuthority,
                    batch.SourceExportFingerprint,
                    batch.TransitionAuthority with
                    {
                        OperationKey = "operation_private_authority_tampered"
                    });
                break;
            }
            case "baseline":
            {
                var history = baseline.PreTurnHistory;
                history["nextOrdinal"] = 999;
                baseline = new WoundPreparedBaselineAuthority(
                    baseline.PreparedInputFingerprint,
                    baseline.PreTurnCarriers,
                    baseline.PreTurnIdentityIndex,
                    history,
                    baseline.AuthoritySeal);
                break;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null);
        }

        return new WoundPreparedAcceptedTurnPlan(
            source.Binding,
            source.BindingFingerprint,
            source.InputFingerprint,
            source.WoundPreparationFingerprint,
            source.AllocatedWoundIds,
            source.AllocatedTransitionIds,
            source.PreparedWounds,
            batches,
            baseline);
    }

    private static IReadOnlyList<EffectAcceptedApplicationResult> MutateResultSet(
        IReadOnlyList<EffectAcceptedApplicationResult> source,
        string mutation)
    {
        var values = source.ToArray();
        if (values.Length < 2)
            throw new InvalidOperationException("Set mutations require two roots.");
        return mutation switch
        {
            "missing" => values.Take(values.Length - 1).ToArray(),
            "extra" => values.Append(values[0] with
            {
                ApplicationRef = "application_extra",
                EffectId = "effect_extra",
                CreateTransitionId = "effect_transition_extra",
                CreatedEventRef = "wound_effect:sha256:" + new string('a', 64)
            }).ToArray(),
            "duplicate_exact" => values.Append(values[0]).ToArray(),
            "duplicate_confusable" => values.Append(values[0] with
            {
                ApplicationRef = ConfusableVariant(values[0].ApplicationRef),
                EffectId = "effect_duplicate_confusable",
                CreateTransitionId = "effect_transition_duplicate_confusable"
            }).ToArray(),
            "reordered" => values.Reverse().ToArray(),
            "effect_id_duplicate" => ReplaceAt(values, 1, values[1] with
            {
                EffectId = values[0].EffectId
            }),
            "effect_id_confusable" => ReplaceAt(values, 1, values[1] with
            {
                EffectId = ConfusableVariant(values[0].EffectId)
            }),
            "cross_wound" => new[]
            {
                values[0] with { SourceKey = values[1].SourceKey },
                values[1] with { SourceKey = values[0].SourceKey }
            },
            "stack" or "refresh" or "merge" or "replace" =>
                ReplaceAt(values, 0, values[0] with { Disposition = mutation }),
            _ => throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null)
        };
    }

    private static IReadOnlyList<EffectAcceptedApplicationResult> ReplaceAt(
        EffectAcceptedApplicationResult[] values,
        int index,
        EffectAcceptedApplicationResult replacement)
    {
        values[index] = replacement;
        return values;
    }

    private static EffectAcceptedApplicationResult MutateApplicationAgreement(
        EffectAcceptedApplicationResult value,
        string mutation)
    {
        var slots = value.Materialization.SlotBindings.ToArray();
        var changedMaterialization = mutation switch
        {
            "slot" => value.Materialization with
            {
                SlotBindings = new[] { slots[0] with { Slot = slots[0].Slot + 1 } }
            },
            "profile" => value.Materialization with
            {
                SlotBindings = new[]
                {
                    slots[0] with { ProfileKey = "action_control" }
                }
            },
            "summary" => value.Materialization with
            {
                SlotBindings = new[]
                {
                    slots[0] with { ReadableSummary = "changed summary" }
                }
            },
            "component_count" => value.Materialization with
            {
                ComponentCount = value.Materialization.ComponentCount + 1
            },
            "materialization_fingerprint" => value.Materialization with
            {
                MaterializationFingerprint = Fingerprint("foreign materialization")
            },
            _ => value.Materialization
        };
        return mutation switch
        {
            "create_transition" => value with
            {
                CreateTransitionId = "effect_transition_foreign"
            },
            "created_event" => value with { CreatedEventRef = "turn_42:foreign" },
            "causal_event" => value with { CausalEventRef = "turn_42:foreign" },
            "source_realm" => value with
            {
                SourceKey = value.SourceKey with { Realm = "chaos_sea" }
            },
            "source_kind" => value with
            {
                SourceKey = value.SourceKey with { Kind = "quest" }
            },
            "source_id" => value with
            {
                SourceKey = value.SourceKey with { SourceId = "wound_foreign" }
            },
            "source_definition" => value with
            {
                SourceKey = value.SourceKey with { DefinitionKey = "definition_foreign" }
            },
            "target_realm" => value with
            {
                TargetKey = value.TargetKey with { Realm = "chaos_sea" }
            },
            "target_kind" => value with
            {
                TargetKey = value.TargetKey with { Kind = "npc" }
            },
            "target_id" => value with
            {
                TargetKey = value.TargetKey with { TargetId = "target_foreign" }
            },
            "carrier_kind" => value with
            {
                CarrierCoordinate = value.CarrierCoordinate with { Kind = "npc" }
            },
            "carrier_owner" => value with
            {
                CarrierCoordinate = value.CarrierCoordinate with
                {
                    OwnerId = "owner_foreign"
                }
            },
            "carrier_path" => value with
            {
                CarrierCoordinate = value.CarrierCoordinate with
                {
                    Path = "game_state/foreign/effects.json"
                }
            },
            "carrier_category" => value with
            {
                CarrierCoordinate = value.CarrierCoordinate with
                {
                    Category = value.CarrierCoordinate.Category is null
                        ? "debuff"
                        : null
                }
            },
            "slot" or "profile" or "summary" or "component_count" or
                "materialization_fingerprint" =>
                value with { Materialization = changedMaterialization },
            _ => throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null)
        };
    }

    private static ValidationIssue CreateIssue(string code) =>
        new(
            "game_state/effects/effect_commands.json",
            IssueSeverity.Error,
            "Upstream effect planning failed.",
            code,
            actor: "effect_test",
            section: "effectChanges",
            expected: "valid effect handoff",
            actual: "invalid handoff",
            repairHint: "Retry the accepted turn.",
            repairTargetFiles: new[]
            {
                "game_state/effects/effect_commands.json"
            });

    private static void AssertValidationIssueEqual(
        ValidationIssue expected,
        ValidationIssue actual)
    {
        Assert.Equal(expected.FilePath, actual.FilePath);
        Assert.Equal(expected.Severity, actual.Severity);
        Assert.Equal(expected.Message, actual.Message);
        Assert.Equal(expected.Category, actual.Category);
        Assert.Equal(expected.Code, actual.Code);
        Assert.Equal(expected.Actor, actual.Actor);
        Assert.Equal(expected.Section, actual.Section);
        Assert.Equal(expected.Expected, actual.Expected);
        Assert.Equal(expected.Actual, actual.Actual);
        Assert.Equal(expected.RepairHint, actual.RepairHint);
        Assert.Equal(expected.RepairTargetFiles, actual.RepairTargetFiles);
    }

    private static WoundCarrierCatalogInput ApplyContributions(
        WoundCarrierCatalogInput source,
        IReadOnlyList<WoundCarrierContribution> contributions)
    {
        var player = source.PlayerWounds?.DeepClone().AsObject();
        var npcs = source.NpcWounds?.DeepClone().AsObject();
        var enemies = source.EnemyCombatants?.DeepClone().AsObject();
        var allies = source.AllyCombatants?.DeepClone().AsObject();
        var afterlife = source.AfterlifeProfiles?.DeepClone().AsObject();
        foreach (var contribution in contributions)
        {
            foreach (var mutation in contribution.Mutations)
            {
                Assert.Equal("add", mutation.Operation);
                var wound = ToJson(Assert.IsType<WoundMaterializationEnvelope>(
                    mutation.AfterWound));
                switch (contribution.Owner.OwnerKind)
                {
                    case "player":
                        player!["activeWounds"]!.AsArray().Add(wound);
                        break;
                    case "npc":
                        var npc = npcs!["entries"]!.AsArray().OfType<JsonObject>()
                            .Single(value => string.Equals(
                                value["npcId"]!.GetValue<string>(),
                                contribution.Owner.OwnerId,
                                StringComparison.Ordinal));
                        npc["activeWounds"]!.AsArray().Add(wound);
                        break;
                    case "guardian":
                        var profile = afterlife!["profiles"]!.AsArray()
                            .OfType<JsonObject>().Single(value => string.Equals(
                                value["actorId"]!.GetValue<string>(),
                                contribution.Owner.OwnerId,
                                StringComparison.Ordinal));
                        profile["activeWounds"]!.AsArray().Add(wound);
                        break;
                    default:
                        throw new InvalidOperationException(
                            "Unexpected contribution owner in bounded T011 fixture.");
                }
            }
        }

        return new WoundCarrierCatalogInput(player, npcs, enemies, allies, afterlife);
    }

    private static JsonObject ToJson(WoundMaterializationEnvelope wound) =>
        JsonNode.Parse(WoundMaterializationContract.SerializeCanonical(wound))!
            .AsObject();

    private static void AssertPreparedEquivalent(
        WoundPreparedAcceptedTurnPlan expected,
        WoundPreparedAcceptedTurnPlan actual)
    {
        AssertBindingEqual(expected.Binding, actual.Binding);
        Assert.Equal(expected.BindingFingerprint, actual.BindingFingerprint);
        Assert.Equal(expected.InputFingerprint, actual.InputFingerprint);
        Assert.Equal(expected.WoundPreparationFingerprint,
            actual.WoundPreparationFingerprint);
        Assert.Equal(expected.AllocatedWoundIds, actual.AllocatedWoundIds);
        Assert.Equal(expected.AllocatedTransitionIds, actual.AllocatedTransitionIds);
        Assert.Equal(
            expected.PreparedWounds.Select(WoundMaterializationContract.SerializeCanonical),
            actual.PreparedWounds.Select(WoundMaterializationContract.SerializeCanonical));
        Assert.Equal(
            expected.EffectOperationBatches.Select(static value =>
                value.SourceExportFingerprint),
            actual.EffectOperationBatches.Select(static value =>
                value.SourceExportFingerprint));
        Assert.Equal(
            expected.EffectOperationBatches.Select(static value =>
                value.RootApplications.Select(static root => root.ApplicationRef)),
            actual.EffectOperationBatches.Select(static value =>
                value.RootApplications.Select(static root => root.ApplicationRef)));
    }

    private static void AssertReadOnly<T>(IReadOnlyList<T> values)
    {
        Assert.False(values is T[], "The immutable plan exposed its backing array.");
        if (values is IList<T> list)
            Assert.Throws<NotSupportedException>(() => list.Add(default!));
    }

    private static string ComputeExpectedAcceptedEventSetFingerprint(
        IReadOnlyList<WoundAcceptedEventAuthority> events)
    {
        var fields = new List<string?>
        {
            AcceptedEventSetDomain,
            FingerprintVersion,
            events.Count.ToString(CultureInfo.InvariantCulture)
        };
        for (var index = 0; index < events.Count; index++)
        {
            var value = events[index];
            fields.Add(index.ToString(CultureInfo.InvariantCulture));
            fields.Add(value.EventRef);
            fields.Add(value.Kind);
            fields.Add(value.AuthorityId);
            fields.Add(value.SemanticFingerprint);
        }
        return LengthPrefixedFingerprint(fields);
    }

    private static string ComputeTransitionAuthoritySeal(string? mutation = null)
    {
        var input = "sha256:transition_input";
        var localWound = "draft_wound_transition";
        var permanentWound = "wound_transition";
        var opportunity = "opportunity_transition";
        var opportunityFingerprint = "sha256:opportunity_transition";
        var operation = "operation_transition";
        var summary = "Transition summary.";
        var maximumSeverity = 4;
        switch (mutation)
        {
            case null:
                break;
            case "prepared_input":
                input += "_changed";
                break;
            case "local_wound":
                localWound += "_changed";
                break;
            case "permanent_wound":
                permanentWound += "_changed";
                break;
            case "opportunity_id":
                opportunity += "_changed";
                break;
            case "opportunity_fingerprint":
                opportunityFingerprint += "_changed";
                break;
            case "operation_key":
                operation += "_changed";
                break;
            case "readable_summary":
                summary += " Changed.";
                break;
            case "maximum_severity":
                maximumSeverity = 3;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null);
        }

        return WoundAcceptedTurnFingerprints.ComputeTransitionAuthority(
            input,
            localWound,
            permanentWound,
            opportunity,
            opportunityFingerprint,
            operation,
            summary,
            maximumSeverity);
    }

    private static WoundCarrierCatalogInput CreateAuthorityCarrierMatrix() => new(
        new JsonObject { ["root"] = "player_душа" },
        new JsonObject { ["root"] = "npc" },
        new JsonObject { ["root"] = "enemy" },
        new JsonObject { ["root"] = "ally" },
        new JsonObject { ["root"] = "afterlife" });

    private static IReadOnlyList<string?> CreateExpectedBaselineAuthorityFields(
        string preparedInputFingerprint,
        WoundCarrierCatalogInput carriers,
        JsonObject identity,
        JsonObject history) => new string?[]
        {
            BaselineAuthorityDomain,
            FingerprintVersion,
            preparedInputFingerprint,
            carriers.PlayerWounds is null ? null : CanonicalJson(carriers.PlayerWounds),
            carriers.NpcWounds is null ? null : CanonicalJson(carriers.NpcWounds),
            carriers.EnemyCombatants is null
                ? null
                : CanonicalJson(carriers.EnemyCombatants),
            carriers.AllyCombatants is null
                ? null
                : CanonicalJson(carriers.AllyCombatants),
            carriers.AfterlifeProfiles is null
                ? null
                : CanonicalJson(carriers.AfterlifeProfiles),
            CanonicalJson(identity),
            CanonicalJson(history)
        };

    private static string SerializeWoundCarriers(WoundCarrierCatalogInput carriers) =>
        string.Join(
            "\u001f",
            new JsonObject?[]
            {
                carriers.PlayerWounds,
                carriers.NpcWounds,
                carriers.EnemyCombatants,
                carriers.AllyCombatants,
                carriers.AfterlifeProfiles
            }.Select(static value => value is null
                ? "<null>"
                : CanonicalJson(value)));

    private static string ComputeExpectedSourceExportFingerprint(
        WoundEffectOperationBatch batch)
    {
        var export = batch.SourceExport;
        var fields = new List<string?>
        {
            SourceExportDomain,
            FingerprintVersion,
            export.SchemaVersion.ToString(CultureInfo.InvariantCulture),
            export.Kind,
            export.SourceId,
            export.SourceRef,
            export.State,
            export.Materializable ? "true" : "false",
            export.Realm,
            export.Owner.Realm,
            export.Owner.OwnerKind,
            export.Owner.OwnerId,
            export.Owner.CarrierPath,
            export.CausalEventRef,
            export.EventSemanticFingerprint,
            export.OpportunityId,
            export.OpportunityAuthorityFingerprint,
            export.Definitions.Count.ToString(CultureInfo.InvariantCulture)
        };
        for (var index = 0; index < export.Definitions.Count; index++)
        {
            var definition = export.Definitions[index];
            fields.Add(index.ToString(CultureInfo.InvariantCulture));
            fields.Add(definition.DefinitionKey);
            fields.Add(CanonicalJson(definition.Definition));
        }
        fields.Add(batch.RootLineageAuthority.Count.ToString(CultureInfo.InvariantCulture));
        for (var index = 0; index < batch.RootLineageAuthority.Count; index++)
        {
            var lineage = batch.RootLineageAuthority[index];
            fields.Add(index.ToString(CultureInfo.InvariantCulture));
            fields.Add(lineage.ApplicationRef);
            fields.Add(lineage.EffectId);
            fields.Add(lineage.DefinitionKey);
            fields.Add(lineage.OwnershipDomain.Kind);
            fields.Add(lineage.OwnershipDomain.ComplicationId);
        }
        fields.Add(batch.RootApplications.Count.ToString(CultureInfo.InvariantCulture));
        for (var index = 0; index < batch.RootApplications.Count; index++)
            AppendRootApplication(fields, index, batch.RootApplications[index]);
        return LengthPrefixedFingerprint(fields);
    }

    private static string ComputeExpectedMaterializationFingerprint(
        EffectSourceKey sourceKey,
        int schemaVersion,
        JsonObject parameters,
        JsonArray materializedComponents) =>
        LengthPrefixedFingerprint(CreateMaterializationFingerprintFields(
            sourceKey,
            schemaVersion,
            parameters,
            materializedComponents));

    private static string ComputeIncorrectUtf16LengthMaterializationFingerprint(
        EffectSourceKey sourceKey,
        int schemaVersion,
        JsonObject parameters,
        JsonArray materializedComponents) =>
        CharacterLengthPrefixedFingerprint(CreateMaterializationFingerprintFields(
            sourceKey,
            schemaVersion,
            parameters,
            materializedComponents));

    private static IReadOnlyList<string?> CreateMaterializationFingerprintFields(
        EffectSourceKey sourceKey,
        int schemaVersion,
        JsonObject parameters,
        JsonArray materializedComponents)
    {
        var fields = new List<string?>
        {
            MaterializationDomain,
            FingerprintVersion,
            sourceKey.Realm,
            sourceKey.Kind,
            sourceKey.SourceId,
            sourceKey.DefinitionKey,
            schemaVersion.ToString(CultureInfo.InvariantCulture),
            CanonicalJson(parameters),
            materializedComponents.Count.ToString(CultureInfo.InvariantCulture)
        };
        for (var index = 0; index < materializedComponents.Count; index++)
        {
            fields.Add(index.ToString(CultureInfo.InvariantCulture));
            fields.Add(CanonicalJson(materializedComponents[index]!));
        }
        return fields;
    }

    private static void AppendRootApplication(
        ICollection<string?> fields,
        int index,
        WoundRootEffectApplication value)
    {
        fields.Add(index.ToString(CultureInfo.InvariantCulture));
        fields.Add(value.ApplicationRef);
        fields.Add(value.MechanicsOrdinal.ToString(CultureInfo.InvariantCulture));
        fields.Add(value.OperationOrdinal.ToString(CultureInfo.InvariantCulture));
        fields.Add(value.OperationKind);
        fields.Add(value.OperationKey);
        fields.Add(value.DefinitionKey);
        fields.Add(value.TargetSelector.Kind);
        fields.Add(value.TargetSelector.TargetId);
        fields.Add(value.TargetSelector.TargetRef);
        fields.Add(value.ExpectedTargetKey.Realm);
        fields.Add(value.ExpectedTargetKey.Kind);
        fields.Add(value.ExpectedTargetKey.TargetId);
        fields.Add(value.SourceSelector.Realm);
        fields.Add(value.SourceSelector.Kind);
        fields.Add(value.SourceSelector.SourceId);
        fields.Add(value.SourceSelector.SourceRef);
        fields.Add(value.SourceSelector.DefinitionKey);
        fields.Add(value.ExpectedSourceKey.Realm);
        fields.Add(value.ExpectedSourceKey.Kind);
        fields.Add(value.ExpectedSourceKey.SourceId);
        fields.Add(value.ExpectedSourceKey.DefinitionKey);
        fields.Add(CanonicalJson(value.Parameters));
        fields.Add(value.SlotBindings.Count.ToString(CultureInfo.InvariantCulture));
        for (var slotIndex = 0; slotIndex < value.SlotBindings.Count; slotIndex++)
        {
            var slot = value.SlotBindings[slotIndex];
            fields.Add(slotIndex.ToString(CultureInfo.InvariantCulture));
            fields.Add(slot.Slot.ToString(CultureInfo.InvariantCulture));
            fields.Add(slot.ProfileKey);
            fields.Add(slot.ReadableSummary);
        }
        fields.Add(value.ExpectedComponentCount.ToString(CultureInfo.InvariantCulture));
        fields.Add(value.ExpectedMaterializationFingerprint);
        fields.Add(value.OwnershipDomain.Kind);
        fields.Add(value.OwnershipDomain.ComplicationId);
        fields.Add(value.CausalEventRef);
        fields.Add(value.ExpectedCarrierCoordinate.Kind);
        fields.Add(value.ExpectedCarrierCoordinate.OwnerId);
        fields.Add(value.ExpectedCarrierCoordinate.Path);
        fields.Add(value.ExpectedCarrierCoordinate.Category);
    }

    private static string ComputeExpectedWoundPreparationFingerprint(
        WoundPreparedAcceptedTurnPlan plan)
    {
        var fields = new List<string?>
        {
            WoundPreparationDomain,
            FingerprintVersion,
            plan.BindingFingerprint,
            plan.InputFingerprint,
            plan.Binding.SessionId,
            plan.Binding.RequestId,
            plan.Binding.SnapshotToken,
            plan.Binding.Realm,
            plan.Binding.Turn.ToString(CultureInfo.InvariantCulture),
            plan.Binding.AcceptedEventsFingerprint,
            plan.Binding.AcceptedEvents.Count.ToString(CultureInfo.InvariantCulture)
        };
        for (var eventIndex = 0;
             eventIndex < plan.Binding.AcceptedEvents.Count;
             eventIndex++)
        {
            var authority = plan.Binding.AcceptedEvents[eventIndex];
            fields.Add(eventIndex.ToString(CultureInfo.InvariantCulture));
            fields.Add(authority.EventRef);
            fields.Add(authority.Kind);
            fields.Add(authority.AuthorityId);
            fields.Add(authority.SemanticFingerprint);
        }
        fields.Add(
            plan.AllocatedWoundIds.Count.ToString(CultureInfo.InvariantCulture)
        );
        AppendOrdered(fields, plan.AllocatedWoundIds);
        fields.Add(plan.AllocatedTransitionIds.Count.ToString(CultureInfo.InvariantCulture));
        AppendOrdered(fields, plan.AllocatedTransitionIds);
        fields.Add(plan.PreparedWounds.Count.ToString(CultureInfo.InvariantCulture));
        AppendOrdered(fields, plan.PreparedWounds.Select(
            WoundMaterializationContract.SerializeCanonical));
        fields.Add(plan.EffectOperationBatches.Count.ToString(CultureInfo.InvariantCulture));
        for (var index = 0; index < plan.EffectOperationBatches.Count; index++)
        {
            var batch = plan.EffectOperationBatches[index];
            fields.Add(index.ToString(CultureInfo.InvariantCulture));
            fields.Add(batch.LocalWoundRef);
            fields.Add(batch.PreparedWoundId);
            fields.Add(ComputeExpectedSourceExportFingerprint(batch));
            fields.Add(batch.TerminalOperations.Count.ToString(CultureInfo.InvariantCulture));
            for (var terminalIndex = 0;
                 terminalIndex < batch.TerminalOperations.Count;
                 terminalIndex++)
            {
                var terminal = batch.TerminalOperations[terminalIndex];
                fields.Add(terminalIndex.ToString(CultureInfo.InvariantCulture));
                fields.Add(terminal.OperationRef);
                fields.Add(terminal.OperationKey);
                fields.Add(terminal.EffectId);
            }
        }
        return LengthPrefixedFingerprint(fields);
    }

    private static void AppendOrdered(
        ICollection<string?> destination,
        IEnumerable<string> values)
    {
        var ordinal = 0;
        foreach (var value in values)
        {
            destination.Add(ordinal.ToString(CultureInfo.InvariantCulture));
            destination.Add(value);
            ordinal++;
        }
    }

    private static string LengthPrefixedFingerprint(IEnumerable<string?> fields)
    {
        var builder = new StringBuilder();
        foreach (var field in fields)
        {
            if (field is null)
            {
                builder.Append("-1:");
                continue;
            }
            builder.Append(Encoding.UTF8.GetByteCount(field)
                .ToString(CultureInfo.InvariantCulture));
            builder.Append(':');
            builder.Append(field);
        }
        return Fingerprint(builder.ToString());
    }

    private static string CharacterLengthPrefixedFingerprint(IEnumerable<string?> fields)
    {
        var builder = new StringBuilder();
        foreach (var field in fields)
        {
            if (field is null)
            {
                builder.Append("-1:");
                continue;
            }
            builder.Append(field.Length.ToString(CultureInfo.InvariantCulture));
            builder.Append(':');
            builder.Append(field);
        }
        return Fingerprint(builder.ToString());
    }

    private static string CanonicalJson(JsonNode node) =>
        CanonicalizeJson(node)!.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = false
        });

    private static JsonNode? CanonicalizeJson(JsonNode? node) => node switch
    {
        null => null,
        JsonObject value => new JsonObject(value
            .OrderBy(static pair => pair.Key, StringComparer.Ordinal)
            .Select(static pair => KeyValuePair.Create(
                pair.Key,
                CanonicalizeJson(pair.Value)))),
        JsonArray value => new JsonArray(value
            .Select(static item => CanonicalizeJson(item)).ToArray()),
        _ => node.DeepClone()
    };

    private static JsonNode? ReverseObjectProperties(JsonNode? node) => node switch
    {
        null => null,
        JsonObject value => new JsonObject(value
            .Reverse()
            .Select(static pair => KeyValuePair.Create(
                pair.Key,
                ReverseObjectProperties(pair.Value)))),
        JsonArray value => new JsonArray(value
            .Select(static item => ReverseObjectProperties(item)).ToArray()),
        _ => node.DeepClone()
    };

    private static string Fingerprint(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return "sha256:" + Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string ConfusableVariant(string value)
    {
        var pairs = new[]
        {
            ('A', '\u0410'), ('a', '\u0430'), ('e', '\u0435'), ('o', '\u043e'), ('p', '\u0440')
        };
        foreach (var (ascii, confusable) in pairs)
        {
            var index = value.IndexOf(ascii, StringComparison.Ordinal);
            if (index >= 0)
                return value[..index] + confusable + value[(index + 1)..];
        }
        throw new InvalidOperationException("Fixture value has no supported confusable glyph.");
    }

    private static string ToAbsolutePath(string relativePath)
    {
        var cursor = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (cursor is not null)
        {
            var candidate = Path.Combine(
                cursor.FullName,
                relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(candidate))
                return candidate;
            cursor = cursor.Parent;
        }
        return Path.GetFullPath(relativePath);
    }

    private class RecordingWoundIdentityAllocator :
        IWoundAcceptedTurnIdentityAllocator
    {
        private readonly string _salt;
        private readonly List<WoundAcceptedTurnIdentityScope> _wounds = new();
        private readonly List<ApplicationAllocationRequest> _applications = new();
        private readonly List<TransitionAllocationRequest> _transitions = new();

        internal RecordingWoundIdentityAllocator(string salt = "recording")
        {
            _salt = salt;
        }

        internal IReadOnlyList<WoundAcceptedTurnIdentityScope> WoundRequests =>
            new ReadOnlyCollection<WoundAcceptedTurnIdentityScope>(_wounds.ToArray());

        internal IReadOnlyList<ApplicationAllocationRequest> ApplicationRequests =>
            new ReadOnlyCollection<ApplicationAllocationRequest>(
                _applications.ToArray());

        internal IReadOnlyList<TransitionAllocationRequest> TransitionRequests =>
            new ReadOnlyCollection<TransitionAllocationRequest>(_transitions.ToArray());

        public virtual string CreateWoundId(WoundAcceptedTurnIdentityScope scope)
        {
            _wounds.Add(scope);
            return StableIdentity("wound", _salt, SerializeScope(scope));
        }

        public virtual string CreateApplicationRef(
            WoundAcceptedTurnIdentityScope scope,
            string localApplicationRef,
            string definitionKey,
            string operationKey)
        {
            _applications.Add(new ApplicationAllocationRequest(
                scope,
                localApplicationRef,
                definitionKey,
                operationKey));
            return StableIdentity(
                "wound_application",
                _salt,
                SerializeScope(scope),
                localApplicationRef,
                definitionKey,
                operationKey);
        }

        public virtual string CreateTransitionId(
            WoundAcceptedTurnIdentityScope scope,
            string localTransitionRef)
        {
            _transitions.Add(new TransitionAllocationRequest(scope, localTransitionRef));
            return StableIdentity(
                "wound_transition",
                _salt,
                SerializeScope(scope),
                localTransitionRef);
        }

        private static string SerializeScope(WoundAcceptedTurnIdentityScope scope) =>
            JsonSerializer.Serialize(new
            {
                scope.SessionId,
                scope.RequestId,
                scope.SnapshotToken,
                scope.Realm,
                scope.Turn,
                scope.AcceptedEventsFingerprint,
                scope.EventRef,
                scope.OpportunityId,
                OwnerRealm = scope.Owner.Realm,
                scope.Owner.OwnerKind,
                scope.Owner.OwnerId,
                scope.Owner.CarrierPath,
                scope.DraftKind,
                scope.OperationKey,
                scope.LocalWoundRef
            });

        protected static string StableIdentity(
            string kind,
            params string[] fields) =>
            kind + "_" + LengthPrefixedFingerprint(fields)["sha256:".Length..38];
    }

    private sealed class CollisionWoundIdentityAllocator :
        RecordingWoundIdentityAllocator
    {
        private readonly string _mutation;
        private int _woundOrdinal;
        private int _applicationOrdinal;
        private int _transitionOrdinal;

        internal CollisionWoundIdentityAllocator(string mutation)
            : base("collision-" + mutation)
        {
            _mutation = mutation;
        }

        public override string CreateWoundId(WoundAcceptedTurnIdentityScope scope)
        {
            var ordinary = base.CreateWoundId(scope);
            _woundOrdinal++;
            return _mutation switch
            {
                "sibling_wound_exact" => "wound_collision_A",
                "sibling_wound_confusable" => _woundOrdinal == 1
                    ? "wound_collision_A"
                    : ConfusableVariant("wound_collision_A"),
                "pre_turn_wound" => "wound_collision_existing",
                _ => ordinary
            };
        }

        public override string CreateApplicationRef(
            WoundAcceptedTurnIdentityScope scope,
            string localApplicationRef,
            string definitionKey,
            string operationKey)
        {
            var ordinary = base.CreateApplicationRef(
                scope,
                localApplicationRef,
                definitionKey,
                operationKey);
            _applicationOrdinal++;
            return _mutation switch
            {
                "sibling_application_exact" => "wound_application_collision_A",
                "sibling_application_confusable" => _applicationOrdinal == 1
                    ? "wound_application_collision_A"
                    : ConfusableVariant("wound_application_collision_A"),
                _ => ordinary
            };
        }

        public override string CreateTransitionId(
            WoundAcceptedTurnIdentityScope scope,
            string localTransitionRef)
        {
            var ordinary = base.CreateTransitionId(scope, localTransitionRef);
            _transitionOrdinal++;
            return _mutation switch
            {
                "sibling_transition_exact" => "wound_transition_collision_A",
                "sibling_transition_confusable" => _transitionOrdinal == 1
                    ? "wound_transition_collision_A"
                    : ConfusableVariant("wound_transition_collision_A"),
                "history_transition" => "wound_transition_history_existing",
                _ => ordinary
            };
        }
    }

    private sealed class CountingEffectIdentityFactory : EffectIdentityFactory
    {
        internal int EffectCalls { get; private set; }
        internal int TransitionCalls { get; private set; }

        internal override string CreateEffectId()
        {
            EffectCalls++;
            return $"effect_counting_{EffectCalls:D3}";
        }

        internal override string CreateTransitionId()
        {
            TransitionCalls++;
            return $"effect_transition_counting_{TransitionCalls:D3}";
        }
    }

    private sealed class ScriptedEffectIdentityFactory : EffectIdentityFactory
    {
        private readonly string _prefix;

        internal ScriptedEffectIdentityFactory(string prefix)
        {
            _prefix = prefix;
        }

        internal int EffectCalls { get; private set; }
        internal int TransitionCalls { get; private set; }

        internal override string CreateEffectId()
        {
            EffectCalls++;
            return $"effect_{_prefix}_{EffectCalls:D3}";
        }

        internal override string CreateTransitionId()
        {
            TransitionCalls++;
            return $"effect_transition_{_prefix}_{TransitionCalls:D3}";
        }
    }

    private sealed class ReverseOrderedEffectIdentityFactory : EffectIdentityFactory
    {
        private int _effectOrdinal;
        private int _transitionOrdinal;

        internal override string CreateEffectId()
        {
            _effectOrdinal++;
            return _effectOrdinal switch
            {
                1 => "effect_reverse_z",
                2 => "effect_reverse_a",
                _ => $"effect_reverse_{_effectOrdinal:D3}"
            };
        }

        internal override string CreateTransitionId()
        {
            _transitionOrdinal++;
            return $"effect_transition_reverse_{_transitionOrdinal:D3}";
        }
    }
}
