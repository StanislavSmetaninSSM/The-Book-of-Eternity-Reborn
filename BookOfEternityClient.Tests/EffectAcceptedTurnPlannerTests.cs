using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class EffectAcceptedTurnPlannerTests
{
    [Fact]
    public void ReactionEventRef_UsesTypedAuthorityAndUnambiguousTupleHashing()
    {
        var authority = new ResourcePendingAuthorityBinding(
            "accepted_application",
            "turn_42:effect_application:stable");
        var first = EffectReactionExecutor.CreateReactionEventRef(
            "turn_42:accepted_event",
            "effect_random_a",
            authority,
            "trigger:a",
            "component");
        var resubmitted = EffectReactionExecutor.CreateReactionEventRef(
            "turn_42:accepted_event",
            "effect_random_b",
            authority,
            "trigger:a",
            "component");
        var separatorCollision = EffectReactionExecutor.CreateReactionEventRef(
            "turn_42:accepted_event",
            "effect_random_c",
            authority,
            "trigger",
            "a:component");

        Assert.Equal(first, resubmitted);
        Assert.NotEqual(first, separatorCollision);
    }

    [Fact]
    public void LifecycleResourceTriggerEventRef_DistinguishesTypedAuthority()
    {
        const string authorityId = "turn_42:effect_application:stable";
        var accepted = EffectAcceptedTurnPlanner
            .CreateLifecycleResourceTriggerEventRef(
                "turn_42:accepted_event",
                "effect_random",
                new ResourcePendingAuthorityBinding(
                    "accepted_application",
                    authorityId),
                "trigger:a");
        var permanent = EffectAcceptedTurnPlanner
            .CreateLifecycleResourceTriggerEventRef(
                "turn_42:accepted_event",
                authorityId,
                new ResourcePendingAuthorityBinding(
                    "permanent",
                    authorityId),
                "trigger:a");
        var changedTrigger = EffectAcceptedTurnPlanner
            .CreateLifecycleResourceTriggerEventRef(
                "turn_42:accepted_event",
                "effect_other_random",
                new ResourcePendingAuthorityBinding(
                    "accepted_application",
                    authorityId),
                "trigger");

        Assert.NotEqual(accepted, permanent);
        Assert.NotEqual(accepted, changedTrigger);
    }

    [Fact]
    public void Build_SameTurnLifecycleTransitionEventRefRebindsAllocatedEffectIdentity()
    {
        var definition = EffectMaterializationTestFixture.CreateDefinition(
            "event_reaction");
        var command = EffectMaterializationTestFixture.CreateApplyCommand();
        command["parameters"] = new JsonObject();
        var input = CreateInput(command: command, definition: definition);
        input.EventInput["lifecycleEvents"] = new JsonArray(new JsonObject
        {
            ["eventRef"] = "turn_42:lifecycle:owner_turn_end",
            ["turn"] = 42,
            ["phase"] = "owner_turn_end",
            ["realm"] = "mortal_world",
            ["target"] = new JsonObject
            {
                ["kind"] = "player",
                ["targetId"] = "player_current"
            }
        });

        string BuildLifecycleEventRef(string effectId, string suffix)
        {
            var factory = new ScriptedIdentityFactory(effectId, suffix);
            var result = new EffectAcceptedTurnPlanCache(factory)
                .GetOrBuild(input);
            Assert.True(
                result.Success,
                string.Join(Environment.NewLine, result.Issues));
            var pendingPlan = Assert.IsType<EffectAcceptedTurnPlan>(result.Plan);
            var definitions = ResourceDefinitionCatalog.CreateBuiltIn();
            var due = EffectAcceptedTurnPlanner.ResolveDuePeriodicResourceMutations(
                pendingPlan,
                ResourceOwnerAuthority.CreateCurrentPlayerAuthority(definitions),
                definitions);
            Assert.True(due.IsValid, string.Join(Environment.NewLine, due.Issues));
            Assert.Empty(due.Mutations);
            Assert.Empty(due.TriggerCandidates);
            var bootstrap = ResourceBootstrapStateBuilder.BuildPristine();
            Assert.True(
                bootstrap.IsValid,
                string.Join(Environment.NewLine, bootstrap.Issues));
            var sources = ResourceMutationSourceCatalog.Create(due.SourceExports);
            Assert.True(sources.IsValid, string.Join(Environment.NewLine, sources.Issues));
            var resources = AcceptedMechanicsPlanner.BuildResources(
                new AcceptedMechanicsResourceInput(
                    Turn: 42,
                    Definitions: definitions,
                    State: bootstrap.State!,
                    History: bootstrap.History!,
                    Sources: sources.Catalog!,
                    Mutations: due.Mutations,
                    InitialTriggerCandidates: due.TriggerCandidates,
                    InitialEffectResolutionWork: due.Work,
                    EffectPlanAuthority:
                        AcceptedMechanicsPlanner.CreateEffectPlanAuthority(
                            pendingPlan)),
                new AcceptedMechanicsIdentityFactory());
            Assert.True(
                resources.IsValid,
                string.Join(Environment.NewLine, resources.Issues));
            var finalized = EffectAcceptedTurnPlanner
                .CompleteAcceptedBoundaryTranscript(
                    pendingPlan,
                    resources.EffectBoundaryTranscript,
                    factory);
            Assert.True(
                finalized.Success,
                string.Join(Environment.NewLine, finalized.Issues));
            var plan = Assert.IsType<EffectAcceptedTurnPlan>(finalized.Plan);
            var identity = Assert.Single(
                plan.IdentityIndexAfterImage["entries"]!.AsArray())!
                .AsObject();
            var lifecycleTransition = Assert.Single(
                identity["transitions"]!
                    .AsArray()
                    .OfType<JsonObject>(),
                transition => string.Equals(
                    transition["kind"]?.GetValue<string>(),
                    "consume",
                    StringComparison.Ordinal));
            return lifecycleTransition["eventRef"]!.GetValue<string>();
        }

        var first = BuildLifecycleEventRef("effect_random_a", "a");
        var resubmitted = BuildLifecycleEventRef("effect_random_b", "b");

        Assert.Equal(first, resubmitted);
        Assert.DoesNotContain("effect_random_", first, StringComparison.Ordinal);
    }

    [Fact]
    public void LifecycleTransitionEventRef_UsesExactTypedEffectAuthority()
    {
        const string lifecycleEventRef = "turn_42:lifecycle:owner_turn_end";
        const string acceptedAuthorityId = "turn_42:effect_application";
        var acceptedAuthority = new ResourcePendingAuthorityBinding(
            "accepted_application",
            acceptedAuthorityId);
        var first = EffectAcceptedTurnPlanner.CreateLifecycleTransitionEventRef(
            lifecycleEventRef,
            "effect_random_a",
            acceptedAuthority);
        var resubmitted = EffectAcceptedTurnPlanner
            .CreateLifecycleTransitionEventRef(
                lifecycleEventRef,
                "effect_random_b",
                acceptedAuthority);
        var drifted = EffectAcceptedTurnPlanner.CreateLifecycleTransitionEventRef(
            lifecycleEventRef,
            "effect_random_b",
            new ResourcePendingAuthorityBinding(
                "accepted_application",
                "turn_42:effect_application_drifted"));
        var permanentLookalike = EffectAcceptedTurnPlanner
            .CreateLifecycleTransitionEventRef(
                lifecycleEventRef,
                acceptedAuthorityId,
                new ResourcePendingAuthorityBinding(
                    "permanent",
                    acceptedAuthorityId));

        Assert.Equal(first, resubmitted);
        Assert.NotEqual(first, drifted);
        Assert.NotEqual(first, permanentLookalike);
    }

    [Fact]
    public void Build_ApplyCreatesCompletePlayerCarrierIdentityAndTransientDeletion()
    {
        var command = EffectMaterializationTestFixture.CreateApplyCommand();
        command["parameters"]!["amount"] = 4;
        var input = CreateInput(command: command);
        var rawBefore = input.RawCommands.DeepClone();
        var result = new EffectAcceptedTurnPlanCache(new CountingFactory()).GetOrBuild(input);

        var plan = Assert.IsType<EffectAcceptedTurnPlan>(result.Plan);
        var effect = Assert.Single(plan.ActiveEffects);
        using var document = JsonDocument.Parse(effect.ToJsonString());
        Assert.Empty(EffectMaterializationContract.Validate(
            document.RootElement,
            "activeEffect",
            EffectMaterializationPhase.CanonicalActive));
        Assert.Equal(4, effect["components"]![0]!["payload"]!["amount"]!.GetValue<int>());
        Assert.Equal(plan.AllocatedEffectIds.Single(), effect["effectId"]!.GetValue<string>());
        Assert.Equal(plan.AllocatedTransitionIds.Single(), effect["chronology"]!["lastTransitionId"]!.GetValue<string>());

        var carrier = plan.CarrierAfterImages[EffectCarrierCatalog.PlayerPath];
        Assert.True(JsonNode.DeepEquals(effect, Assert.Single(carrier["activeEffects"]!.AsArray())));
        using var identityDocument = JsonDocument.Parse(plan.IdentityIndexAfterImage.ToJsonString());
        var parsedIndex = EffectIdentityState.Parse(
            identityDocument.RootElement,
            EffectAcceptedTurnPlan.IdentityIndexPath);
        Assert.Empty(parsedIndex.Issues);
        var identity = Assert.Single(parsedIndex.State!.Entries);
        Assert.Equal(plan.AllocatedEffectIds.Single(), identity.EffectId);
        Assert.Empty(Assert.Single(identity.Transitions).SourceEffectIds);
        Assert.Equal(
            new[]
            {
                EffectCarrierCatalog.PlayerPath,
                EffectAcceptedTurnPlan.IdentityIndexPath,
                EffectAcceptedTurnPlan.CommandPath
            }.OrderBy(static path => path, StringComparer.Ordinal),
            plan.TouchedPaths.OrderBy(static path => path, StringComparer.Ordinal));
        Assert.Equal(new[] { EffectAcceptedTurnPlan.CommandPath }, plan.DeletedPaths);
        Assert.True(JsonNode.DeepEquals(rawBefore, input.RawCommands));
    }

    [Fact]
    public void Build_NpcApplyPreservesAdjacentNpcState()
    {
        var npcRoot = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["entries"] = new JsonArray(new JsonObject
            {
                ["NPCId"] = "npc_test_healer",
                ["activeEffects"] = new JsonArray(),
                ["wounds"] = new JsonArray(new JsonObject { ["woundId"] = "wound_npc_arm" })
            })
        };
        var input = CreateInput(
            targetKind: "npc",
            carriers: new EffectCarrierCatalogInput(null, npcRoot, null, null, null, null));
        var before = npcRoot.DeepClone();

        var plan = Assert.IsType<EffectAcceptedTurnPlan>(
            new EffectAcceptedTurnPlanCache(new CountingFactory()).GetOrBuild(input).Plan);

        var after = plan.CarrierAfterImages[EffectCarrierCatalog.NpcPath];
        var entry = Assert.IsType<JsonObject>(Assert.Single(after["entries"]!.AsArray()));
        Assert.Single(entry["activeEffects"]!.AsArray());
        Assert.Equal("wound_npc_arm", entry["wounds"]![0]!["woundId"]!.GetValue<string>());
        Assert.Equal("npc", Assert.Single(plan.ActiveEffects)["target"]!["kind"]!.GetValue<string>());
        Assert.True(JsonNode.DeepEquals(before, npcRoot));
    }

    [Theory]
    [InlineData("buff", "activeBuffs")]
    [InlineData("debuff", "activeDebuffs")]
    public void Build_CombatantApplyUsesCategoryCarrierAndPreservesCombatant(string category, string collection)
    {
        var definition = EffectMaterializationTestFixture.CreateDefinition();
        definition["display"]!["category"] = category;
        var combatRoot = new JsonObject
        {
            ["enemiesData"] = new JsonArray(new JsonObject
            {
                ["combatantId"] = EffectMaterializationTestFixture.CombatantId,
                ["initiative"] = 17,
                ["activeBuffs"] = new JsonArray(),
                ["activeDebuffs"] = new JsonArray()
            })
        };
        var input = CreateInput(
            targetKind: "combatant",
            definition: definition,
            carriers: new EffectCarrierCatalogInput(null, null, combatRoot, null, null, null));

        var plan = Assert.IsType<EffectAcceptedTurnPlan>(
            new EffectAcceptedTurnPlanCache(new CountingFactory()).GetOrBuild(input).Plan);

        var after = plan.CarrierAfterImages[EffectCarrierCatalog.EnemiesPath];
        var combatant = Assert.IsType<JsonObject>(Assert.Single(after["enemiesData"]!.AsArray()));
        Assert.Equal(17, combatant["initiative"]!.GetValue<int>());
        Assert.Single(combatant[collection]!.AsArray());
        Assert.Empty(combatant[collection == "activeBuffs" ? "activeDebuffs" : "activeBuffs"]!.AsArray());
    }

    [Theory]
    [InlineData("condition")]
    [InlineData("environmental")]
    [InlineData("mixed")]
    public void Build_CombatantApplyRejectsCategoryWithoutCanonicalCollection(
        string category)
    {
        var definition = EffectMaterializationTestFixture.CreateDefinition();
        definition["display"]!["category"] = category;
        var combatRoot = new JsonObject
        {
            ["enemiesData"] = new JsonArray(new JsonObject
            {
                ["combatantId"] = EffectMaterializationTestFixture.CombatantId,
                ["activeBuffs"] = new JsonArray(),
                ["activeDebuffs"] = new JsonArray()
            })
        };
        var input = CreateInput(
            targetKind: "combatant",
            definition: definition,
            carriers: new EffectCarrierCatalogInput(null, null, combatRoot, null, null, null));

        var result = new EffectAcceptedTurnPlanCache(new CountingFactory()).GetOrBuild(input);

        Assert.Null(result.Plan);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "effect_plan_combat_category_unsupported");
    }

    [Theory]
    [InlineData("condition")]
    [InlineData("environmental")]
    [InlineData("mixed")]
    public void Build_PlayerApplyAllowsRegisteredNonCombatCategory(string category)
    {
        var definition = EffectMaterializationTestFixture.CreateDefinition();
        definition["display"]!["category"] = category;
        var result = new EffectAcceptedTurnPlanCache(new CountingFactory()).GetOrBuild(
            CreateInput(definition: definition));

        var plan = Assert.IsType<EffectAcceptedTurnPlan>(result.Plan);
        Assert.Empty(result.Issues);
        Assert.Equal(
            category,
            plan.ActiveEffects.Single()["display"]!["category"]!.GetValue<string>());
    }

    [Fact]
    public void Build_SourceBoundCombatActionUsesCanonicalCombatLinkKind()
    {
        const string actionId = "combat_action_guarded_thrust";
        var definition = EffectMaterializationTestFixture.CreateDefinition();
        definition["lifetime"] = new JsonObject
        {
            ["mode"] = "source_bound",
            ["activePredicate"] = "active",
            ["onSourceLoss"] = "expire"
        };
        var command = EffectMaterializationTestFixture.CreateApplyCommand();
        command["source"]!["kind"] = "combat_action";
        command["source"]!["sourceId"] = actionId;
        var input = CreateInput(command: command, definition: definition) with
        {
            SourceAuthority = EffectSourceAuthority.Build(new EffectSourceAuthorityInput(
                new[]
                {
                    new EffectSourceExport(
                        "mortal_world",
                        "combat_action",
                        actionId,
                        new JsonArray(definition.DeepClone()),
                        Materializable: true,
                        Active: true,
                        SameTurn: false)
                },
                Array.Empty<EffectSourceExport>(),
                new HashSet<string>(StringComparer.Ordinal)))
        };

        var result = new EffectAcceptedTurnPlanCache(new CountingFactory()).GetOrBuild(input);

        Assert.Empty(result.Issues);
        var plan = Assert.IsType<EffectAcceptedTurnPlan>(result.Plan);
        var effect = Assert.Single(plan.ActiveEffects);
        Assert.Equal("combat", effect["lifetime"]!["linkKind"]!.GetValue<string>());
        using var document = JsonDocument.Parse(effect.ToJsonString());
        Assert.Empty(EffectMaterializationContract.Validate(
            document.RootElement,
            "activeEffect",
            EffectMaterializationPhase.CanonicalActive));
        var bindingIssues = new List<ValidationIssue>();
        ValidationService.ValidateCanonicalEffectBindings(
            EffectCarrierCatalog.Build(new EffectCarrierCatalogInput(
                plan.CarrierAfterImages[EffectCarrierCatalog.PlayerPath],
                null,
                null,
                null,
                null,
                null)),
            input.SourceAuthority,
            input.TargetAuthority,
            bindingIssues);
        Assert.Empty(bindingIssues);
    }

    [Fact]
    public void Build_UntilTimeDerivesCanonicalDeadlineFromAcceptedWorldTime()
    {
        var definition = EffectMaterializationTestFixture.CreateDefinition();
        definition["lifetime"] = new JsonObject
        {
            ["mode"] = "until_time",
            ["duration"] = 30,
            ["timeAuthority"] = "world_time.currentTimeInMinutes"
        };
        var input = CreateInput(definition: definition);
        input.EventInput["currentTime"] = 120L;
        input.EventInput["timeAuthority"] = "world_time.currentTimeInMinutes";

        var result = new EffectAcceptedTurnPlanCache(new CountingFactory()).GetOrBuild(input);

        var effect = Assert.Single(Assert.IsType<EffectAcceptedTurnPlan>(result.Plan).ActiveEffects);
        Assert.Empty(result.Issues);
        Assert.Equal("until_time", effect["lifetime"]!["mode"]!.GetValue<string>());
        Assert.Equal(150L, effect["lifetime"]!["deadline"]!.GetValue<long>());
    }

    [Fact]
    public void Build_UntilTimeRejectsMissingAcceptedWorldTimeBeforeAllocation()
    {
        var definition = EffectMaterializationTestFixture.CreateDefinition();
        definition["lifetime"] = new JsonObject
        {
            ["mode"] = "until_time",
            ["duration"] = 30,
            ["timeAuthority"] = "world_time.currentTimeInMinutes"
        };
        var factory = new CountingFactory();

        var result = new EffectAcceptedTurnPlanCache(factory).GetOrBuild(
            CreateInput(definition: definition));

        Assert.Null(result.Plan);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "effect_plan_lifetime_context_missing");
        Assert.Equal(0, factory.EffectCalls);
    }

    [Theory]
    [InlineData("effectId")]
    [InlineData("currentStacks")]
    [InlineData("remainingTurns")]
    [InlineData("transitionId")]
    [InlineData("receiptId")]
    [InlineData("components")]
    [InlineData("carrierPath")]
    [InlineData("duration")]
    [InlineData("activeEffects")]
    [InlineData("effectIdentityIndex")]
    public void Build_ApplyRejectsSubmittedPostStateAndLegacyFields(string field)
    {
        var command = EffectMaterializationTestFixture.CreateApplyCommand();
        command[field] = field == "duration" ? JsonValue.Create(999) : JsonValue.Create("forged");
        var factory = new CountingFactory();

        var result = new EffectAcceptedTurnPlanCache(factory).GetOrBuild(CreateInput(command: command));

        Assert.Null(result.Plan);
        Assert.Contains(result.Issues, issue => issue.Code == "effect_plan_client_field_forbidden");
        Assert.Equal(0, factory.EffectCalls);
        Assert.Equal(0, factory.TransitionCalls);
    }

    [Fact]
    public void Build_ApplyRejectsMissingRequiredSourceParameterAndWrongTargetKind()
    {
        var definition = EffectMaterializationTestFixture.CreateDefinition();
        definition["parameterBounds"]!["amount"]!["required"] = true;
        var command = EffectMaterializationTestFixture.CreateApplyCommand("npc");
        command["parameters"] = new JsonObject();
        definition["allowedTargetKinds"] = new JsonArray("player");
        var factory = new CountingFactory();

        var result = new EffectAcceptedTurnPlanCache(factory).GetOrBuild(
            CreateInput("npc", command, definition));

        Assert.Null(result.Plan);
        Assert.Contains(result.Issues, issue => issue.Code == "effect_source_parameter_required");
        Assert.Contains(result.Issues, issue => issue.Code == "effect_source_target_kind_forbidden");
        Assert.Equal(0, factory.EffectCalls);
    }

    [Fact]
    public void Build_AttachesExactRepairContextForSingletonRequiredParameter()
    {
        var definition = EffectMaterializationTestFixture.CreateDefinition();
        definition["parameterBounds"]!["amount"]!["required"] = true;
        definition["parameterBounds"]!["amount"]!["minimum"] = 3;
        definition["parameterBounds"]!["amount"]!["maximum"] = 3;
        var command = EffectMaterializationTestFixture.CreateApplyCommand();
        command["parameters"] = new JsonObject();

        var result = new EffectAcceptedTurnPlanCache(new CountingFactory()).GetOrBuild(
            CreateInput(command: command, definition: definition));

        Assert.Null(result.Plan);
        var issue = Assert.Single(result.Issues, issue =>
            issue.Code == "effect_source_parameter_required");
        Assert.Equal("effectChanges[0].parameters.amount", issue.FilePath);
        Assert.Equal("effect-apply:effectChanges[0]", issue.Actor);
        var context = Assert.IsType<EffectRepairContext>(issue.EffectRepairContext);
        Assert.Equal("3", context.ExpectedValueJson);
        Assert.Single(EffectRepairPacketBuilder.Build(
            result.Issues,
            rollbackAvailable: true));
    }

    [Fact]
    public void Build_DoesNotAttachRepairContextForNonSingletonRequiredParameter()
    {
        var definition = EffectMaterializationTestFixture.CreateDefinition();
        definition["parameterBounds"]!["amount"]!["required"] = true;
        var command = EffectMaterializationTestFixture.CreateApplyCommand();
        command["parameters"] = new JsonObject();

        var result = new EffectAcceptedTurnPlanCache(new CountingFactory()).GetOrBuild(
            CreateInput(command: command, definition: definition));

        Assert.Null(result.Plan);
        var issue = Assert.Single(result.Issues, issue =>
            issue.Code == "effect_source_parameter_required");
        Assert.Null(issue.EffectRepairContext);
        Assert.Empty(EffectRepairPacketBuilder.Build(
            result.Issues,
            rollbackAvailable: true));
    }

    [Fact]
    public void Build_DoesNotRoundDistinctLargeBoundsIntoRepairableSingleton()
    {
        var definition = EffectMaterializationTestFixture.CreateDefinition();
        definition["parameterBounds"]!["amount"]!["required"] = true;
        definition["parameterBounds"]!["amount"]!["minimum"] = 9007199254740992m;
        definition["parameterBounds"]!["amount"]!["maximum"] = 9007199254740993m;
        var command = EffectMaterializationTestFixture.CreateApplyCommand();
        command["parameters"] = new JsonObject();

        var result = new EffectAcceptedTurnPlanCache(new CountingFactory()).GetOrBuild(
            CreateInput(command: command, definition: definition));

        Assert.Null(result.Plan);
        var issue = Assert.Single(result.Issues, issue =>
            issue.Code == "effect_source_parameter_required");
        Assert.Null(issue.EffectRepairContext);
        Assert.Empty(EffectRepairPacketBuilder.Build(
            result.Issues,
            rollbackAvailable: true));
    }

    [Fact]
    public void ResponseModelAndMappingExposeOnlyCommonTransientEffectRoutes()
    {
        var responseFields = typeof(GameResponse)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(property => property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name)
            .Where(static name => name != null)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("effectChanges", responseFields);
        Assert.Contains("effectResolutionReceipts", responseFields);
        Assert.Contains("effectEventReports", responseFields);
        Assert.DoesNotContain("playerActiveEffectsChanges", responseFields);
        Assert.DoesNotContain("NPCEffectChanges", responseFields);
        Assert.Equal(EffectAcceptedTurnPlan.CommandPath, FileMapping.FieldToFile["effectChanges"]);
        Assert.Equal(EffectAcceptedTurnPlan.CommandPath, FileMapping.FieldToFile["effectResolutionReceipts"]);
        Assert.Equal(EffectAcceptedTurnPlan.CommandPath, FileMapping.FieldToFile["effectEventReports"]);
        Assert.False(FileMapping.FieldToFile.ContainsKey("playerActiveEffectsChanges"));
        Assert.False(FileMapping.FieldToFile.ContainsKey("NPCEffectChanges"));
    }

    [Fact]
    public void Build_ReceiptsOnlyRootTreatsAbsentEffectChangesAsEmpty()
    {
        var factory = new CountingFactory();
        var input = CreateInput() with
        {
            RawCommands = new JsonObject
            {
                ["effectResolutionReceipts"] = new JsonArray()
            }
        };

        var result = new EffectAcceptedTurnPlanCache(factory).GetOrBuild(input);

        Assert.Empty(result.Issues);
        var plan = Assert.IsType<EffectAcceptedTurnPlan>(result.Plan);
        Assert.Empty(plan.ActiveEffects);
        Assert.Empty(plan.AllocatedEffectIds);
        Assert.Empty(plan.AllocatedTransitionIds);
        Assert.Equal(0, factory.EffectCalls);
        Assert.Equal(0, factory.TransitionCalls);
        Assert.Contains(EffectAcceptedTurnPlan.CommandPath, plan.DeletedPaths);
    }

    [Theory]
    [InlineData("playerActiveEffectsChanges")]
    [InlineData("NPCEffectChanges")]
    public void Build_LegacyRootRouteIsExplicitlyRejectedWithoutAllocation(string legacyField)
    {
        var commands = EffectMaterializationTestFixture.CreateCommandRoot();
        commands[legacyField] = new JsonArray(EffectMaterializationTestFixture.CreateApplyCommand());
        var factory = new CountingFactory();

        var result = new EffectAcceptedTurnPlanCache(factory).GetOrBuild(
            CreateInput() with { RawCommands = commands });

        Assert.Null(result.Plan);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "effect_plan_client_field_forbidden" &&
            string.Equals(issue.FilePath, legacyField, StringComparison.Ordinal));
        Assert.Equal(0, factory.EffectCalls);
        Assert.Equal(0, factory.TransitionCalls);
    }

    [Fact]
    public void Build_CommandEventAuthorityMustMatchAcceptedEventAuthorityBeforeAllocation()
    {
        var command = EffectMaterializationTestFixture.CreateApplyCommand();
        command["eventRef"]!["authorityId"] = "turn_forged";
        var factory = new CountingFactory();

        var result = new EffectAcceptedTurnPlanCache(factory).GetOrBuild(
            CreateInput(command: command));

        Assert.Null(result.Plan);
        Assert.Contains(result.Issues, issue => issue.Code == "effect_plan_event_authority_mismatch");
        Assert.Equal(0, factory.EffectCalls);
        Assert.Equal(0, factory.TransitionCalls);
    }

    [Fact]
    public void Build_DuplicateAcceptedEventCommandsFailBeforeIdentityAllocation()
    {
        var command = EffectMaterializationTestFixture.CreateApplyCommand();
        var factory = new CountingFactory();

        var result = new EffectAcceptedTurnPlanCache(factory).GetOrBuild(
            CreateInput() with
            {
                RawCommands = EffectMaterializationTestFixture.CreateCommandRoot(command, command)
            });

        Assert.Null(result.Plan);
        Assert.Contains(result.Issues, issue => issue.Code == "effect_plan_event_authority_mismatch");
        Assert.Equal(0, factory.EffectCalls);
        Assert.Equal(0, factory.TransitionCalls);
    }

    [Fact]
    public void Build_DistinctAcceptedEventCommandsCreateDistinctTransitions()
    {
        var first = EffectMaterializationTestFixture.CreateApplyCommand();
        var second = EffectMaterializationTestFixture.CreateApplyCommand();
        second["eventRef"]!["authorityId"] = "turn_42_followup";
        second["reason"] = "Рана дала второе независимое осложнение.";
        var input = CreateInput() with
        {
            RawCommands = EffectMaterializationTestFixture.CreateCommandRoot(first, second),
            EventInput = new JsonObject
            {
                ["turn"] = 42,
                ["events"] = new JsonArray(
                    CreateAcceptedEvent("accepted_turn", "turn_42", "turn_42:wound_opened"),
                    CreateAcceptedEvent("accepted_turn", "turn_42_followup", "turn_42:wound_followup"))
            }
        };
        var factory = new CountingFactory();

        var plan = Assert.IsType<EffectAcceptedTurnPlan>(
            new EffectAcceptedTurnPlanCache(factory).GetOrBuild(input).Plan);

        var effect = Assert.Single(plan.ActiveEffects);
        Assert.Equal(2, effect["stacking"]!["currentStacks"]!.GetValue<int>());
        Assert.Single(plan.AllocatedEffectIds);
        Assert.Equal(2, plan.AllocatedTransitionIds.Count);
        Assert.Equal(1, factory.EffectCalls);
        Assert.Equal(2, factory.TransitionCalls);
        Assert.Equal(
            new[] { "turn_42:wound_opened", "turn_42:wound_followup" },
            Assert.Single(plan.IdentityIndexAfterImage["entries"]!.AsArray())!["transitions"]!
                .AsArray()
                .Select(static transition => transition!["eventRef"]!.GetValue<string>())
                .ToArray());
    }

    [Fact]
    public void Build_SwappedOrdinalEventAuthoritiesFailBeforeIdentityAllocation()
    {
        var first = EffectMaterializationTestFixture.CreateApplyCommand();
        var second = EffectMaterializationTestFixture.CreateApplyCommand();
        first["eventRef"]!["authorityId"] = "turn_42_followup";
        second["eventRef"]!["authorityId"] = "turn_42";
        second["reason"] = "Рана дала второе независимое осложнение.";
        var input = CreateInput() with
        {
            RawCommands = EffectMaterializationTestFixture.CreateCommandRoot(first, second),
            EventInput = new JsonObject
            {
                ["turn"] = 42,
                ["events"] = new JsonArray(
                    CreateAcceptedEvent("accepted_turn", "turn_42", "turn_42:wound_opened"),
                    CreateAcceptedEvent("accepted_turn", "turn_42_followup", "turn_42:wound_followup"))
            }
        };
        var factory = new CountingFactory();

        var result = new EffectAcceptedTurnPlanCache(factory).GetOrBuild(input);

        Assert.Null(result.Plan);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "effect_plan_event_authority_mismatch" &&
            string.Equals(issue.FilePath, "effectChanges[0].eventRef", StringComparison.Ordinal));
        Assert.Equal(0, factory.EffectCalls);
        Assert.Equal(0, factory.TransitionCalls);
    }

    [Fact]
    public void Cache_SameAcceptedInputReturnsSamePlanAndAllocatesOnceAcrossCallers()
    {
        var factory = new CountingFactory();
        var cache = new EffectAcceptedTurnPlanCache(factory);
        var input = CreateInput();

        var rawValidation = cache.GetOrBuild(input);
        var companionValidation = cache.GetOrBuild(input);
        var mechanics = cache.GetOrBuild(input);
        var commit = cache.GetOrBuild(input);

        var plan = Assert.IsType<EffectAcceptedTurnPlan>(rawValidation.Plan);
        Assert.Same(plan, companionValidation.Plan);
        Assert.Same(plan, mechanics.Plan);
        Assert.Same(plan, commit.Plan);
        Assert.Equal(1, factory.EffectCalls);
        Assert.Equal(1, factory.TransitionCalls);
        Assert.StartsWith("effect_", plan.AllocatedEffectIds.Single(), StringComparison.Ordinal);
        Assert.StartsWith("effect_transition_", plan.AllocatedTransitionIds.Single(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("session")]
    [InlineData("snapshot")]
    [InlineData("commands")]
    [InlineData("source")]
    [InlineData("target")]
    [InlineData("event")]
    [InlineData("carrier")]
    [InlineData("accepted_carrier")]
    [InlineData("publication_carrier")]
    [InlineData("index")]
    public void Cache_ChangedAuthorityInputInvalidatesPlanWithoutDerivingIds(string changedPart)
    {
        var factory = new CountingFactory();
        var cache = new EffectAcceptedTurnPlanCache(factory);
        var firstInput = CreateInput();
        var first = Assert.IsType<EffectAcceptedTurnPlan>(cache.GetOrBuild(firstInput).Plan);
        var changed = Change(firstInput, changedPart);

        var second = Assert.IsType<EffectAcceptedTurnPlan>(cache.GetOrBuild(changed).Plan);

        Assert.NotSame(first, second);
        Assert.NotEqual(first.InputFingerprint, second.InputFingerprint);
        Assert.NotEqual(first.AllocatedEffectIds.Single(), second.AllocatedEffectIds.Single());
        Assert.Equal(2, factory.EffectCalls);
        Assert.Equal(2, factory.TransitionCalls);
        Assert.DoesNotContain(changed.SessionId, second.AllocatedEffectIds.Single(), StringComparison.Ordinal);
    }

    [Fact]
    public void Cache_InvalidSourceOrTargetAuthorityReturnsIssuesWithoutAllocation()
    {
        var factory = new CountingFactory();
        var cache = new EffectAcceptedTurnPlanCache(factory);
        var input = CreateInput() with
        {
            SourceAuthority = EffectSourceAuthority.Build(new EffectSourceAuthorityInput(
                Array.Empty<EffectSourceExport>(),
                Array.Empty<EffectSourceExport>(),
                new HashSet<string>(StringComparer.Ordinal)))
        };

        var result = cache.GetOrBuild(input);

        Assert.Null(result.Plan);
        Assert.NotEmpty(result.Issues);
        Assert.Equal(0, factory.EffectCalls);
        Assert.Equal(0, factory.TransitionCalls);
    }

    [Fact]
    public void Cache_ReturnedPlanCollectionsCannotBeMutatedBehindReadOnlyInterfaces()
    {
        var plan = Assert.IsType<EffectAcceptedTurnPlan>(
            new EffectAcceptedTurnPlanCache(new CountingFactory()).GetOrBuild(CreateInput()).Plan);

        Assert.True(Assert.IsAssignableFrom<IList<string>>(plan.AllocatedEffectIds).IsReadOnly);
        Assert.True(Assert.IsAssignableFrom<IList<string>>(plan.AllocatedTransitionIds).IsReadOnly);
        Assert.True(Assert.IsAssignableFrom<IList<EffectSourceKey>>(plan.Sources).IsReadOnly);
        Assert.True(Assert.IsAssignableFrom<IList<EffectTargetKey>>(plan.Targets).IsReadOnly);
    }

    [Fact]
    public void Cache_ReturnedJsonAfterImagesCannotMutateTheCachedPlan()
    {
        var cache = new EffectAcceptedTurnPlanCache(new CountingFactory());
        var input = CreateInput() with
        {
            AcceptedCarrierBaselines = CreateChangedCarrierBaselines()
        };
        var plan = Assert.IsType<EffectAcceptedTurnPlan>(cache.GetOrBuild(input).Plan);
        var effectId = Assert.Single(plan.AllocatedEffectIds);

        Assert.Single(plan.ActiveEffects)["effectId"] = "effect_forged";
        plan.CarrierAfterImages[EffectCarrierCatalog.PlayerPath]["activeEffects"] = new JsonArray();
        plan.AcceptedCarrierBaselines.PlayerEffects!["activeEffects"] =
            new JsonArray(new JsonObject { ["effectId"] = "effect_forged" });
        plan.IdentityIndexAfterImage["entries"] = new JsonArray();

        var cached = Assert.IsType<EffectAcceptedTurnPlan>(cache.GetOrBuild(input).Plan);
        Assert.Same(plan, cached);
        Assert.Equal(effectId, Assert.Single(cached.ActiveEffects)["effectId"]!.GetValue<string>());
        Assert.Single(cached.CarrierAfterImages[EffectCarrierCatalog.PlayerPath]["activeEffects"]!.AsArray());
        Assert.Empty(cached.AcceptedCarrierBaselines.PlayerEffects!["activeEffects"]!.AsArray());
        Assert.Single(cached.IdentityIndexAfterImage["entries"]!.AsArray());
    }

    [Theory]
    [InlineData("remove")]
    [InlineData("suspend")]
    public void Build_EventReactionCreatesOnlyPureTerminalCandidates(
        string resultKind)
    {
        var definition = EffectMaterializationTestFixture.CreateDefinition("event_reaction");
        definition["components"]![0]!["payload"]!["resultKind"] = resultKind;
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect(
            profile: "event_reaction");
        effect["components"] = definition["components"]!.DeepClone();
        effect["triggers"] = definition["triggers"]!.DeepClone();
        var input = CreateReactionInput(effect, definition);

        var result = new EffectAcceptedTurnPlanCache(new CountingFactory()).GetOrBuild(input);

        Assert.Empty(result.Issues);
        var plan = Assert.IsType<EffectAcceptedTurnPlan>(result.Plan);
        Assert.Single(
            plan.ResourceTriggerCarriers.PlayerEffects!["activeEffects"]!.AsArray());
        var identity = Assert.Single(plan.IdentityIndexAfterImage["entries"]!.AsArray())!.AsObject();
        Assert.Equal("active", identity["state"]!.GetValue<string>());
        Assert.DoesNotContain(
            identity["transitions"]!.AsArray(),
            transition => transition!["kind"]!.GetValue<string>() == resultKind);
        Assert.Equal(0, plan.ReactionExpansionCount);

        var definitions = ResourceDefinitionCatalog.CreateBuiltIn();
        var due = EffectAcceptedTurnPlanner.ResolveDuePeriodicResourceMutations(
            plan,
            ResourceOwnerAuthority.CreateCurrentPlayerAuthority(definitions),
            definitions);

        Assert.Empty(due.Issues);
        var candidate = Assert.Single(due.TriggerCandidates);
        Assert.Equal(resultKind, Assert.Single(candidate.ReactionOutputs).ResultKind);
    }

    [Fact]
    public void ApplyDefinitionReaction_MaterializesOnlyAfterCurrentBoundaryAndIsEligibleNextTurn()
    {
        var root = EffectMaterializationTestFixture.CreateDefinition("event_reaction");
        root["definitionKey"] = "reaction_root";
        root["components"]![0]!["payload"]!["resultKind"] = "apply_definition";
        root["components"]![0]!["payload"]!["definitionKey"] = "reaction_child";
        root["components"]![0]!["payload"]!["parameters"] = new JsonObject
        {
            ["amount"] = 3
        };
        root["components"]![0]!["payload"]!["maxExpansion"] = 2;
        var child = EffectMaterializationTestFixture.CreateDefinition();
        child["definitionKey"] = "reaction_child";
        child["stacking"]!["stackKey"] = "reaction-child";
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect(
            profile: "event_reaction");
        effect["source"]!["definitionKey"] = "reaction_root";
        effect["components"] = root["components"]!.DeepClone();
        effect["triggers"] = root["triggers"]!.DeepClone();
        var input = CreateReactionInput(effect, root, child);
        var factory = new CountingFactory();

        var result = new EffectAcceptedTurnPlanCache(factory).GetOrBuild(input);

        Assert.Empty(result.Issues);
        var plan = Assert.IsType<EffectAcceptedTurnPlan>(result.Plan);
        var effects = plan.ResourceTriggerCarriers.PlayerEffects!["activeEffects"]!.AsArray();
        Assert.Single(effects);
        Assert.DoesNotContain(effects, candidate =>
            candidate!["source"]!["definitionKey"]!.GetValue<string>() == "reaction_child");
        Assert.Empty(plan.AllocatedEffectIds);
        Assert.Equal(0, plan.ReactionExpansionCount);

        var resourceDefinitions = ResourceDefinitionCatalog.CreateBuiltIn();
        var due = EffectAcceptedTurnPlanner.ResolveDuePeriodicResourceMutations(
            plan,
            ResourceOwnerAuthority.CreateCurrentPlayerAuthority(resourceDefinitions),
            resourceDefinitions);

        Assert.Empty(due.Issues);
        var candidate = Assert.Single(due.TriggerCandidates);
        var reaction = Assert.Single(candidate.ReactionOutputs);
        Assert.Equal("apply_definition", reaction.ResultKind);
        Assert.Equal("reaction_child", reaction.DownstreamSourceKey?.DefinitionKey);

        var bootstrap = ResourceBootstrapStateBuilder.BuildPristine();
        Assert.True(
            bootstrap.IsValid,
            string.Join(Environment.NewLine, bootstrap.Issues));
        var sources = ResourceMutationSourceCatalog.Create(due.SourceExports);
        Assert.True(
            sources.IsValid,
            string.Join(Environment.NewLine, sources.Issues));
        var resources = AcceptedMechanicsPlanner.BuildResources(
            new AcceptedMechanicsResourceInput(
                Turn: 42,
                Definitions: bootstrap.Definitions!,
                State: bootstrap.State!,
                History: bootstrap.History!,
                Sources: sources.Catalog!,
                Mutations: due.Mutations,
                InitialTriggerCandidates: due.TriggerCandidates,
                InitialEffectResolutionWork: due.Work,
                EffectPlanAuthority:
                    AcceptedMechanicsPlanner.CreateEffectPlanAuthority(plan)),
            new AcceptedMechanicsIdentityFactory());

        Assert.True(
            resources.IsValid,
            string.Join(Environment.NewLine, resources.Issues));
        var accepted = Assert.Single(
            resources.EffectBoundaryTranscript.AcceptedActivations);
        Assert.Equal(effect["effectId"]!.GetValue<string>(), accepted.Activation.Stamp.Identity.EffectId);
        var released = Assert.Single(
            resources.EffectBoundaryTranscript.ReleasedReactions);
        Assert.Equal("apply_definition", released.Reaction.ResultKind);
        Assert.DoesNotContain(
            resources.EffectBoundaryTranscript.AcceptedActivations,
            value => value.Activation.Stamp.Identity.EffectId !=
                effect["effectId"]!.GetValue<string>());

        var finalized = EffectAcceptedTurnPlanner.CompleteAcceptedBoundaryTranscript(
            plan,
            resources.EffectBoundaryTranscript,
            factory);

        Assert.True(
            finalized.Success,
            string.Join(Environment.NewLine, finalized.Issues));
        var finalizedPlan = Assert.IsType<EffectAcceptedTurnPlan>(finalized.Plan);
        var childEffect = Assert.Single(
            finalizedPlan.ResourceTriggerCarriers.PlayerEffects!["activeEffects"]!
                .AsArray()
                .OfType<JsonObject>(),
            value => string.Equals(
                value["source"]?["definitionKey"]?.GetValue<string>(),
                "reaction_child",
                StringComparison.Ordinal));
        var childEffectId = childEffect["effectId"]!.GetValue<string>();
        var childIdentity = Assert.Single(
            finalizedPlan.IdentityIndexAfterImage["entries"]!
                .AsArray()
                .OfType<JsonObject>(),
            value => string.Equals(
                value["effectId"]?.GetValue<string>(),
                childEffectId,
                StringComparison.Ordinal));
        var childCreate = Assert.IsType<JsonObject>(
            Assert.Single(childIdentity["transitions"]!.AsArray()));
        Assert.Equal(
            effect["effectId"]!.GetValue<string>(),
            Assert.Single(childCreate["sourceEffectIds"]!.AsArray())!
                .GetValue<string>());

        var nextInput = new EffectAcceptedTurnInput(
            "session_effect_reaction_next",
            "snapshot_effect_reaction_next",
            EffectMaterializationTestFixture.CreateCommandRoot(),
            input.SourceAuthority,
            input.TargetAuthority,
            new JsonObject
            {
                ["turn"] = 43,
                ["events"] = new JsonArray(
                    CreateAcceptedEvent(
                        "accepted_turn",
                        "turn_43",
                        "turn_43:accepted_effect")),
                ["lifecycleEvents"] = new JsonArray(new JsonObject
                {
                    ["eventRef"] = "turn_43:lifecycle:owner_turn_end:1",
                    ["causalEventRef"] = "turn_43:owner_turn_end:1",
                    ["turn"] = 43,
                    ["phase"] = "owner_turn_end",
                    ["realm"] = "mortal_world",
                    ["target"] = new JsonObject
                    {
                        ["kind"] = "player",
                        ["targetId"] = "player_current"
                    },
                    ["effectId"] = childEffectId,
                    ["triggerId"] = "on_owner_turn_end"
                })
            },
            PreTurnCarriers: finalizedPlan.ResourceTriggerCarriers,
            PreTurnIdentityIndex: finalizedPlan.IdentityIndexAfterImage);
        var next = new EffectAcceptedTurnPlanCache(new CountingFactory())
            .GetOrBuild(nextInput);

        Assert.True(
            next.Success,
            string.Join(Environment.NewLine, next.Issues));
        var nextPlan = Assert.IsType<EffectAcceptedTurnPlan>(next.Plan);
        var nextDue = EffectAcceptedTurnPlanner.ResolveDuePeriodicResourceMutations(
            nextPlan,
            ResourceOwnerAuthority.CreateCurrentPlayerAuthority(
                bootstrap.Definitions!),
            bootstrap.Definitions!);

        Assert.True(
            nextDue.IsValid,
            string.Join(Environment.NewLine, nextDue.Issues));
        var nextCandidate = Assert.Single(nextDue.TriggerCandidates);
        Assert.Equal(childEffectId, nextCandidate.Activation.Identity.EffectId);
        Assert.Single(nextDue.Mutations);
    }

    [Theory]
    [InlineData(false, 3)]
    [InlineData(true, 1)]
    [InlineData(true, 2)]
    public void ApplyDefinitionReaction_ExplicitReplaceClosesSameStackIdentityAndDefersReplacementEligibility(
        bool consumesUse,
        int remainingUses)
    {
        const string rootDefinitionKey = "reaction_replace_root";
        const string replacementDefinitionKey = "reaction_replacement";
        const string stackKey = "reaction-replacement-coordinate";

        var root = EffectMaterializationTestFixture.CreateDefinition("event_reaction");
        root["definitionKey"] = rootDefinitionKey;
        root["stacking"]!["stackKey"] = stackKey;
        root["components"]![0]!["payload"]!["resultKind"] = "apply_definition";
        root["components"]![0]!["payload"]!["definitionKey"] =
            replacementDefinitionKey;
        root["components"]![0]!["payload"]!["parameters"] = new JsonObject
        {
            ["amount"] = 3
        };
        root["components"]![0]!["payload"]!["maxExpansion"] = 2;
        if (consumesUse)
        {
            root["triggers"]![0]!["consumeUses"] = true;
            root["lifetime"] = new JsonObject
            {
                ["mode"] = "uses",
                ["initialUses"] = remainingUses,
                ["consumingEventTypes"] = new JsonArray("owner_damaged")
            };
        }

        var replacement = EffectMaterializationTestFixture.CreateDefinition();
        replacement["definitionKey"] = replacementDefinitionKey;
        replacement["stacking"]!["stackKey"] = stackKey;
        replacement["stacking"]!["policy"] = "replace";
        replacement["stacking"]!["maxStacks"] = 1;
        replacement["stacking"]!["atMaximum"] = "no_change";

        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect(
            profile: "event_reaction");
        effect["source"]!["definitionKey"] = rootDefinitionKey;
        effect["stacking"]!["stackKey"] = stackKey;
        effect["components"] = root["components"]!.DeepClone();
        effect["triggers"] = root["triggers"]!.DeepClone();
        if (consumesUse)
        {
            effect["lifetime"] = new JsonObject
            {
                ["mode"] = "uses",
                ["remainingUses"] = remainingUses,
                ["consumingTriggerIds"] = new JsonArray("on_owner_damaged"),
                ["displayText"] = $"{remainingUses} accepted uses remain"
            };
        }
        var oldEffectId = effect["effectId"]!.GetValue<string>();
        var input = CreateReactionInput(effect, root, replacement);
        var factory = new CountingFactory();

        var built = new EffectAcceptedTurnPlanCache(factory).GetOrBuild(input);

        Assert.True(
            built.Success,
            string.Join(Environment.NewLine, built.Issues));
        var plan = Assert.IsType<EffectAcceptedTurnPlan>(built.Plan);
        var currentEffect = Assert.IsType<JsonObject>(Assert.Single(
            plan.ResourceTriggerCarriers.PlayerEffects!["activeEffects"]!
                .AsArray()));
        Assert.Equal(
            oldEffectId,
            currentEffect["effectId"]!.GetValue<string>());
        Assert.Empty(plan.AllocatedEffectIds);
        Assert.Equal(0, plan.ReactionExpansionCount);

        var resourceDefinitions = ResourceDefinitionCatalog.CreateBuiltIn();
        var due = EffectAcceptedTurnPlanner.ResolveDuePeriodicResourceMutations(
            plan,
            ResourceOwnerAuthority.CreateCurrentPlayerAuthority(resourceDefinitions),
            resourceDefinitions);

        Assert.True(
            due.IsValid,
            string.Join(Environment.NewLine, due.Issues));
        var candidate = Assert.Single(due.TriggerCandidates);
        Assert.Equal(oldEffectId, candidate.Activation.Identity.EffectId);
        Assert.Equal(consumesUse, candidate.Activation.ConsumesUse);
        Assert.Equal(
            consumesUse ? (int?)remainingUses : null,
            candidate.UseSeed?.RemainingUses);
        var reaction = Assert.Single(candidate.ReactionOutputs);
        Assert.Equal("apply_definition", reaction.ResultKind);
        Assert.Equal(
            replacementDefinitionKey,
            reaction.DownstreamSourceKey?.DefinitionKey);
        Assert.Equal(oldEffectId, reaction.ReplacementTargetEffectId);

        var bootstrap = ResourceBootstrapStateBuilder.BuildPristine();
        Assert.True(
            bootstrap.IsValid,
            string.Join(Environment.NewLine, bootstrap.Issues));
        var sources = ResourceMutationSourceCatalog.Create(due.SourceExports);
        Assert.True(
            sources.IsValid,
            string.Join(Environment.NewLine, sources.Issues));
        var resources = AcceptedMechanicsPlanner.BuildResources(
            new AcceptedMechanicsResourceInput(
                Turn: 42,
                Definitions: bootstrap.Definitions!,
                State: bootstrap.State!,
                History: bootstrap.History!,
                Sources: sources.Catalog!,
                Mutations: due.Mutations,
                InitialTriggerCandidates: due.TriggerCandidates,
                InitialEffectResolutionWork: due.Work,
                EffectPlanAuthority:
                    AcceptedMechanicsPlanner.CreateEffectPlanAuthority(plan)),
            new AcceptedMechanicsIdentityFactory());

        Assert.True(
            resources.IsValid,
            string.Join(Environment.NewLine, resources.Issues));
        var accepted = Assert.Single(
            resources.EffectBoundaryTranscript.AcceptedActivations);
        Assert.Equal(oldEffectId, accepted.Activation.Stamp.Identity.EffectId);
        Assert.Equal(
            consumesUse ? (int?)remainingUses : null,
            accepted.Activation.Stamp.UsesBefore);
        var released = Assert.Single(
            resources.EffectBoundaryTranscript.ReleasedReactions);
        Assert.Equal("apply_definition", released.Reaction.ResultKind);
        Assert.Equal(oldEffectId, released.Reaction.EffectId);
        Assert.Equal(oldEffectId, released.Reaction.ReplacementTargetEffectId);

        var finalized = EffectAcceptedTurnPlanner.CompleteAcceptedBoundaryTranscript(
            plan,
            resources.EffectBoundaryTranscript,
            factory);

        Assert.True(
            finalized.Success,
            string.Join(Environment.NewLine, finalized.Issues));
        var finalizedPlan = Assert.IsType<EffectAcceptedTurnPlan>(finalized.Plan);
        var replacementEffect = Assert.Single(
            finalizedPlan.ResourceTriggerCarriers.PlayerEffects!["activeEffects"]!
                .AsArray()
                .OfType<JsonObject>());
        var replacementEffectId = replacementEffect["effectId"]!.GetValue<string>();
        Assert.NotEqual(oldEffectId, replacementEffectId);
        Assert.Equal(
            EffectAcceptedTurnPlanner.CreateApplicationTransitionEventRef(
                reaction.EventRef,
                "replacement_result_create"),
            replacementEffect["chronology"]!["createdEventRef"]!
                .GetValue<string>());
        Assert.Equal(
            replacementDefinitionKey,
            replacementEffect["source"]!["definitionKey"]!.GetValue<string>());
        Assert.Equal(
            stackKey,
            replacementEffect["stacking"]!["stackKey"]!.GetValue<string>());
        Assert.Equal(
            "replace",
            replacementEffect["stacking"]!["policy"]!.GetValue<string>());
        Assert.Equal(
            effect["source"]!["sourceId"]!.GetValue<string>(),
            replacementEffect["source"]!["sourceId"]!.GetValue<string>());
        Assert.Equal(replacementEffectId, Assert.Single(finalizedPlan.AllocatedEffectIds));
        Assert.DoesNotContain(
            resources.EffectBoundaryTranscript.AcceptedActivations,
            value => string.Equals(
                value.Activation.Stamp.Identity.EffectId,
                replacementEffectId,
                StringComparison.Ordinal));

        var identities = finalizedPlan.IdentityIndexAfterImage["entries"]!.AsArray()
            .OfType<JsonObject>()
            .ToArray();
        Assert.Equal(2, identities.Length);
        var oldIdentity = Assert.Single(identities, value => string.Equals(
            value["effectId"]!.GetValue<string>(),
            oldEffectId,
            StringComparison.Ordinal));
        Assert.Equal("replaced", oldIdentity["state"]!.GetValue<string>());
        var oldTransitions = oldIdentity["transitions"]!.AsArray();
        var replaceTransition = oldTransitions[^1]!.AsObject();
        Assert.Equal("replace", replaceTransition["kind"]!.GetValue<string>());
        Assert.Equal(
            oldEffectId,
            Assert.Single(replaceTransition["sourceEffectIds"]!.AsArray())!
                .GetValue<string>());
        Assert.Equal(
            replacementEffectId,
            Assert.Single(replaceTransition["resultEffectIds"]!.AsArray())!
                .GetValue<string>());
        if (consumesUse)
        {
            var consumeTransition = oldTransitions[^2]!.AsObject();
            Assert.Equal("consume", consumeTransition["kind"]!.GetValue<string>());
            Assert.Equal(
                accepted.Activation.Stamp.Identity.EventRef,
                consumeTransition["eventRef"]!.GetValue<string>());
        }
        var replacementIdentity = Assert.Single(identities, value => string.Equals(
            value["effectId"]!.GetValue<string>(),
            replacementEffectId,
            StringComparison.Ordinal));
        Assert.Equal("active", replacementIdentity["state"]!.GetValue<string>());
        Assert.Equal(
            stackKey,
            replacementIdentity["stackCoordinate"]!["stackKey"]!.GetValue<string>());
        Assert.True(JsonNode.DeepEquals(
            oldIdentity["stackCoordinate"],
            replacementIdentity["stackCoordinate"]));
        var replacementCreate = Assert.IsType<JsonObject>(
            Assert.Single(replacementIdentity["transitions"]!.AsArray()));
        Assert.Equal(
            oldEffectId,
            Assert.Single(replacementCreate["sourceEffectIds"]!.AsArray())!
                .GetValue<string>());

        var nextInput = new EffectAcceptedTurnInput(
            "session_effect_replacement_next",
            "snapshot_effect_replacement_next",
            EffectMaterializationTestFixture.CreateCommandRoot(),
            input.SourceAuthority,
            input.TargetAuthority,
            new JsonObject
            {
                ["turn"] = 43,
                ["events"] = new JsonArray(
                    CreateAcceptedEvent(
                        "accepted_turn",
                        "turn_43",
                        "turn_43:accepted_replacement")),
                ["lifecycleEvents"] = new JsonArray(new JsonObject
                {
                    ["eventRef"] = "turn_43:lifecycle:replacement_owner_turn_end:1",
                    ["causalEventRef"] = "turn_43:replacement_owner_turn_end:1",
                    ["turn"] = 43,
                    ["phase"] = "owner_turn_end",
                    ["realm"] = "mortal_world",
                    ["target"] = new JsonObject
                    {
                        ["kind"] = "player",
                        ["targetId"] = "player_current"
                    },
                    ["effectId"] = replacementEffectId,
                    ["triggerId"] = "on_owner_turn_end"
                })
            },
            PreTurnCarriers: finalizedPlan.ResourceTriggerCarriers,
            PreTurnIdentityIndex: finalizedPlan.IdentityIndexAfterImage);
        var next = new EffectAcceptedTurnPlanCache(new CountingFactory())
            .GetOrBuild(nextInput);

        Assert.True(
            next.Success,
            string.Join(Environment.NewLine, next.Issues));
        var nextPlan = Assert.IsType<EffectAcceptedTurnPlan>(next.Plan);
        var nextDue = EffectAcceptedTurnPlanner.ResolveDuePeriodicResourceMutations(
            nextPlan,
            ResourceOwnerAuthority.CreateCurrentPlayerAuthority(
                bootstrap.Definitions!),
            bootstrap.Definitions!);

        Assert.True(
            nextDue.IsValid,
            string.Join(Environment.NewLine, nextDue.Issues));
        var nextCandidate = Assert.Single(nextDue.TriggerCandidates);
        Assert.Equal(replacementEffectId, nextCandidate.Activation.Identity.EffectId);
        Assert.DoesNotContain(
            nextDue.TriggerCandidates,
            value => string.Equals(
                value.Activation.Identity.EffectId,
                oldEffectId,
                StringComparison.Ordinal));
        Assert.Single(nextDue.Mutations);
    }

    [Theory]
    [InlineData(
        42,
        "accepted_application",
        "turn_42:distinct_stack_target_created")]
    [InlineData(41, "permanent", "effect_distinct_stack_target")]
    public void ApplyDefinitionReaction_ReplacementTargetUsesTypedExactDistinctStackIdentity(
        int targetCreatedAtTurn,
        string expectedBindingKind,
        string expectedAuthorityId)
    {
        const string rootDefinitionKey = "reaction_distinct_stack_root";
        const string replacementDefinitionKey =
            "reaction_distinct_stack_replacement";
        const string rootStackKey = "reaction-distinct-root-stack";
        const string replacedStackKey = "reaction-distinct-target-stack";

        var root = EffectMaterializationTestFixture.CreateDefinition(
            "event_reaction");
        root["definitionKey"] = rootDefinitionKey;
        root["stacking"]!["stackKey"] = rootStackKey;
        root["components"]![0]!["payload"]!["resultKind"] =
            "apply_definition";
        root["components"]![0]!["payload"]!["definitionKey"] =
            replacementDefinitionKey;
        root["components"]![0]!["payload"]!["parameters"] = new JsonObject
        {
            ["amount"] = 3
        };
        root["components"]![0]!["payload"]!["maxExpansion"] = 2;

        var replacement = EffectMaterializationTestFixture.CreateDefinition(
            "periodic_restore");
        replacement["definitionKey"] = replacementDefinitionKey;
        replacement["stacking"]!["stackKey"] = replacedStackKey;
        replacement["stacking"]!["policy"] = "replace";
        replacement["stacking"]!["maxStacks"] = 1;
        replacement["stacking"]!["atMaximum"] = "no_change";

        var reactor = EffectMaterializationTestFixture.CreateCanonicalEffect(
            profile: "event_reaction");
        reactor["effectId"] = "effect_distinct_stack_reactor";
        reactor["source"]!["definitionKey"] = rootDefinitionKey;
        reactor["stacking"]!["stackKey"] = rootStackKey;
        reactor["components"] = root["components"]!.DeepClone();
        reactor["triggers"] = root["triggers"]!.DeepClone();

        var replaced = EffectMaterializationTestFixture.CreateCanonicalEffect(
            profile: "periodic_restore");
        replaced["effectId"] = "effect_distinct_stack_target";
        replaced["source"]!["definitionKey"] = replacementDefinitionKey;
        replaced["stacking"]!["stackKey"] = replacedStackKey;
        replaced["stacking"]!["policy"] = "replace";
        replaced["chronology"]!["createdAtTurn"] = targetCreatedAtTurn;
        replaced["chronology"]!["createdEventRef"] =
            "turn_42:distinct_stack_target_created";
        var baseline = CreateReactionInput(reactor, root, replacement);
        var carriers = new EffectCarrierCatalogInput(
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["activeEffects"] = new JsonArray(
                    reactor.DeepClone(),
                    replaced.DeepClone())
            },
            null,
            null,
            null,
            null,
            null);

        var planned = EffectReactionExecutor.Plan(
            baseline.EventInput,
            baseline.SourceAuthority,
            carriers);

        Assert.True(
            planned.Success,
            string.Join(Environment.NewLine, planned.Issues));
        var execution = Assert.Single(planned.Executions);
        Assert.Equal(
            reactor["effectId"]!.GetValue<string>(),
            execution.EffectId);
        Assert.Equal(
            replaced["effectId"]!.GetValue<string>(),
            execution.ReplacementTargetEffectId);
        Assert.NotNull(execution.ReplacementTarget);
        Assert.Equal(
            expectedBindingKind,
            execution.ReplacementTarget.Authority.BindingKind);
        Assert.Equal(
            expectedAuthorityId,
            execution.ReplacementTarget.Authority.AuthorityId);
        Assert.NotEqual(execution.EffectId, execution.ReplacementTargetEffectId);
    }

    [Fact]
    public void ApplyDefinitionReaction_DistinctProducerOwnsCreateWhileIncumbentOwnsReplaceSuccession()
    {
        const string producerEffectId = "effect_distinct_lineage_producer";
        const string incumbentEffectId = "effect_distinct_lineage_incumbent";
        const string producerDefinitionKey =
            "reaction_distinct_lineage_producer";
        const string replacementDefinitionKey =
            "reaction_distinct_lineage_replacement";
        const string producerStackKey = "reaction-distinct-lineage-producer";
        const string replacementStackKey =
            "reaction-distinct-lineage-replacement";

        var producerDefinition = EffectMaterializationTestFixture.CreateDefinition(
            "event_reaction");
        producerDefinition["definitionKey"] = producerDefinitionKey;
        producerDefinition["stacking"]!["stackKey"] = producerStackKey;
        producerDefinition["components"]![0]!["payload"]!["resultKind"] =
            "apply_definition";
        producerDefinition["components"]![0]!["payload"]!["definitionKey"] =
            replacementDefinitionKey;
        producerDefinition["components"]![0]!["payload"]!["parameters"] =
            new JsonObject { ["amount"] = 3 };
        producerDefinition["components"]![0]!["payload"]!["maxExpansion"] = 2;

        var replacementDefinition =
            EffectMaterializationTestFixture.CreateDefinition("periodic_restore");
        replacementDefinition["definitionKey"] = replacementDefinitionKey;
        replacementDefinition["stacking"]!["stackKey"] = replacementStackKey;
        replacementDefinition["stacking"]!["policy"] = "replace";
        replacementDefinition["stacking"]!["maxStacks"] = 1;
        replacementDefinition["stacking"]!["atMaximum"] = "no_change";

        var producer = EffectMaterializationTestFixture.CreateCanonicalEffect(
            profile: "event_reaction");
        producer["effectId"] = producerEffectId;
        producer["source"]!["definitionKey"] = producerDefinitionKey;
        producer["stacking"]!["stackKey"] = producerStackKey;
        producer["components"] = producerDefinition["components"]!.DeepClone();
        producer["triggers"] = producerDefinition["triggers"]!.DeepClone();

        var incumbent = EffectMaterializationTestFixture.CreateCanonicalEffect(
            profile: "periodic_restore");
        incumbent["effectId"] = incumbentEffectId;
        incumbent["source"]!["definitionKey"] = replacementDefinitionKey;
        incumbent["stacking"]!["stackKey"] = replacementStackKey;
        incumbent["stacking"]!["policy"] = "replace";

        var baseline = CreateReactionInput(
            producer,
            producerDefinition,
            replacementDefinition);
        var preTurnIdentityIndex =
            EffectMaterializationTestFixture.CreateIdentityIndex(
                producer,
                incumbent);
        preTurnIdentityIndex["entries"]![1]!["transitions"]![0]!
            ["transitionId"] = "effect_transition_distinct_lineage_incumbent";
        preTurnIdentityIndex["entries"]![1]!["transitions"]![0]!
            ["eventRef"] = "turn_42:distinct_lineage_incumbent_created";
        var input = baseline with
        {
            PreTurnCarriers = new EffectCarrierCatalogInput(
                new JsonObject
                {
                    ["schemaVersion"] = 1,
                    ["activeEffects"] = new JsonArray(
                        producer.DeepClone(),
                        incumbent.DeepClone())
                },
                null,
                null,
                null,
                null,
                null),
            PreTurnIdentityIndex = preTurnIdentityIndex
        };
        var factory = new CountingFactory();
        var built = new EffectAcceptedTurnPlanCache(factory).GetOrBuild(input);
        Assert.True(
            built.Success,
            string.Join(Environment.NewLine, built.Issues));
        var plan = Assert.IsType<EffectAcceptedTurnPlan>(built.Plan);

        var resourceDefinitions = ResourceDefinitionCatalog.CreateBuiltIn();
        var due = EffectAcceptedTurnPlanner.ResolveDuePeriodicResourceMutations(
            plan,
            ResourceOwnerAuthority.CreateCurrentPlayerAuthority(
                resourceDefinitions),
            resourceDefinitions);
        Assert.True(due.IsValid, string.Join(Environment.NewLine, due.Issues));
        var reaction = Assert.Single(Assert.Single(
            due.TriggerCandidates).ReactionOutputs);
        Assert.Equal(producerEffectId, reaction.EffectId);
        Assert.Equal(incumbentEffectId, reaction.ReplacementTargetEffectId);

        var bootstrap = ResourceBootstrapStateBuilder.BuildPristine();
        Assert.True(
            bootstrap.IsValid,
            string.Join(Environment.NewLine, bootstrap.Issues));
        var sources = ResourceMutationSourceCatalog.Create(due.SourceExports);
        Assert.True(sources.IsValid, string.Join(Environment.NewLine, sources.Issues));
        var resources = AcceptedMechanicsPlanner.BuildResources(
            new AcceptedMechanicsResourceInput(
                Turn: 42,
                Definitions: bootstrap.Definitions!,
                State: bootstrap.State!,
                History: bootstrap.History!,
                Sources: sources.Catalog!,
                Mutations: due.Mutations,
                InitialTriggerCandidates: due.TriggerCandidates,
                InitialEffectResolutionWork: due.Work,
                EffectPlanAuthority:
                    AcceptedMechanicsPlanner.CreateEffectPlanAuthority(plan)),
            new AcceptedMechanicsIdentityFactory());
        Assert.True(
            resources.IsValid,
            string.Join(Environment.NewLine, resources.Issues));

        var finalized = EffectAcceptedTurnPlanner.CompleteAcceptedBoundaryTranscript(
            plan,
            resources.EffectBoundaryTranscript,
            factory);
        Assert.True(
            finalized.Success,
            string.Join(Environment.NewLine, finalized.Issues.Select(static issue =>
                $"{issue.Code}@{issue.FilePath}: expected={issue.Expected}; actual={issue.Actual}")));
        var identities = Assert.IsType<EffectAcceptedTurnPlan>(finalized.Plan)
            .IdentityIndexAfterImage["entries"]!
            .AsArray()
            .OfType<JsonObject>()
            .ToArray();
        var replacement = Assert.Single(identities, entry =>
            string.Equals(
                entry["source"]?["definitionKey"]?.GetValue<string>(),
                replacementDefinitionKey,
                StringComparison.Ordinal) &&
            string.Equals(
                entry["state"]?.GetValue<string>(),
                "active",
                StringComparison.Ordinal));
        var replacementEffectId = replacement["effectId"]!.GetValue<string>();
        Assert.NotEqual(incumbentEffectId, replacementEffectId);
        var replacementCreate = Assert.IsType<JsonObject>(
            Assert.Single(replacement["transitions"]!.AsArray()));
        Assert.Equal(
            producerEffectId,
            Assert.Single(replacementCreate["sourceEffectIds"]!.AsArray())!
                .GetValue<string>());

        var closedIncumbent = Assert.Single(identities, entry => string.Equals(
            entry["effectId"]?.GetValue<string>(),
            incumbentEffectId,
            StringComparison.Ordinal));
        Assert.Equal("replaced", closedIncumbent["state"]!.GetValue<string>());
        var replace = closedIncumbent["transitions"]!.AsArray()[^1]!.AsObject();
        Assert.Equal(
            incumbentEffectId,
            Assert.Single(replace["sourceEffectIds"]!.AsArray())!
                .GetValue<string>());
        Assert.Equal(
            replacementEffectId,
            Assert.Single(replace["resultEffectIds"]!.AsArray())!
                .GetValue<string>());
    }

    [Fact]
    public void AcceptedBoundary_EventReactionAfterCurrentEventRunsBeforeTerminalProjection()
    {
        var definition = EffectMaterializationTestFixture.CreateDefinition("event_reaction");
        definition["components"]![0]!["payload"]!["dependency"] = "after_current_event";
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect(
            profile: "event_reaction");
        effect["components"] = definition["components"]!.DeepClone();
        effect["triggers"] = definition["triggers"]!.DeepClone();
        var built = new EffectAcceptedTurnPlanCache(new CountingFactory()).GetOrBuild(
            CreateReactionInput(effect, definition));
        var beforeFinalize = Assert.IsType<EffectAcceptedTurnPlan>(built.Plan);

        var definitions = ResourceDefinitionCatalog.CreateBuiltIn();
        var due = EffectAcceptedTurnPlanner.ResolveDuePeriodicResourceMutations(
            beforeFinalize,
            ResourceOwnerAuthority.CreateCurrentPlayerAuthority(definitions),
            definitions);
        Assert.Empty(due.Issues);
        var bootstrap = ResourceBootstrapStateBuilder.BuildPristine();
        Assert.True(
            bootstrap.IsValid,
            string.Join(Environment.NewLine, bootstrap.Issues));
        var sources = ResourceMutationSourceCatalog.Create(due.SourceExports);
        Assert.True(
            sources.IsValid,
            string.Join(Environment.NewLine, sources.Issues));
        var resources = AcceptedMechanicsPlanner.BuildResources(
            new AcceptedMechanicsResourceInput(
                Turn: 42,
                Definitions: definitions,
                State: bootstrap.State!,
                History: bootstrap.History!,
                Sources: sources.Catalog!,
                Mutations: due.Mutations,
                InitialTriggerCandidates: due.TriggerCandidates,
                InitialEffectResolutionWork: due.Work,
                EffectPlanAuthority:
                    AcceptedMechanicsPlanner.CreateEffectPlanAuthority(
                        beforeFinalize)),
            new AcceptedMechanicsIdentityFactory());

        Assert.True(
            resources.IsValid,
            string.Join(Environment.NewLine, resources.Issues));
        Assert.Equal(
            EffectReactionReleaseStage.AfterCurrentEvent,
            Assert.Single(resources.EffectBoundaryTranscript.ReleasedReactions).Stage);

        var finalized = EffectAcceptedTurnPlanner.CompleteAcceptedBoundaryTranscript(
            beforeFinalize,
            resources.EffectBoundaryTranscript,
            new CountingFactory());

        var plan = Assert.IsType<EffectAcceptedTurnPlan>(finalized.Plan);
        Assert.Empty(finalized.Issues);
        Assert.Empty(plan.CarrierAfterImages[EffectCarrierCatalog.PlayerPath]
            ["activeEffects"]!.AsArray());
        Assert.Equal(
            "removed",
            Assert.Single(plan.IdentityIndexAfterImage["entries"]!.AsArray())!
                ["state"]!.GetValue<string>());
    }

    [Fact]
    public void ReactionExecutor_MaterializesPureCandidatesBeyondReleasedExpansionBudget()
    {
        var definition = EffectMaterializationTestFixture.CreateDefinition("event_reaction");
        definition["components"]![0]!["payload"]!["maxExpansion"] = 1;
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect(
            profile: "event_reaction");
        effect["components"] = definition["components"]!.DeepClone();
        effect["triggers"] = definition["triggers"]!.DeepClone();
        var input = CreateReactionInput(effect, definition);
        var secondEvent = input.EventInput["lifecycleEvents"]![0]!
            .DeepClone().AsObject();
        secondEvent["eventRef"] = "turn_42:reaction:owner_damaged:2";
        input.EventInput["lifecycleEvents"]!.AsArray().Add(secondEvent);

        var planned = EffectReactionExecutor.Plan(
            input.EventInput,
            input.SourceAuthority,
            input.PreTurnCarriers!);

        Assert.True(planned.Success);
        Assert.Empty(planned.Issues);
        Assert.Equal(2, planned.Executions.Count);
        Assert.All(
            planned.Executions,
            static execution => Assert.Equal(1, execution.MaxExpansion));
    }

    [Fact]
    public void AcceptedBoundary_AfterComponentReactionRequiresExactAppliedEvidence()
    {
        var definition = EffectMaterializationTestFixture.CreateDefinition("event_reaction");
        definition["components"]![0]!["payload"]!["resultKind"] = "suspend";
        definition["components"]![0]!["payload"]!["dependency"] = "after_component";
        definition["components"]![0]!["payload"]!["afterComponentId"] = "reaction_periodic";
        var periodic = EffectMaterializationTestFixture.CreateDefinition()["components"]![0]!
            .DeepClone().AsObject();
        periodic["componentId"] = "reaction_periodic";
        definition["components"]!.AsArray().Add(periodic);
        definition["triggers"]![0]!["componentIds"] = new JsonArray(
            "component_001",
            "reaction_periodic");
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect(
            profile: "event_reaction");
        effect["components"] = definition["components"]!.DeepClone();
        effect["triggers"] = definition["triggers"]!.DeepClone();
        var built = new EffectAcceptedTurnPlanCache(new CountingFactory()).GetOrBuild(
            CreateReactionInput(effect, definition));
        var beforeFinalize = Assert.IsType<EffectAcceptedTurnPlan>(built.Plan);
        var definitions = ResourceDefinitionCatalog.CreateBuiltIn();
        var due = EffectAcceptedTurnPlanner.ResolveDuePeriodicResourceMutations(
            beforeFinalize,
            ResourceOwnerAuthority.CreateCurrentPlayerAuthority(definitions),
            definitions);
        Assert.Empty(due.Issues);
        var bootstrap = ResourceBootstrapStateBuilder.BuildMortalPlayer(
            incarnationNumber: 1,
            turn: 1,
            permanentStrength: 5,
            permanentConstitution: 5,
            permanentIntelligence: 5,
            permanentWisdom: 5,
            permanentFaith: 5);
        Assert.True(
            bootstrap.IsValid,
            string.Join(Environment.NewLine, bootstrap.Issues));
        var sources = ResourceMutationSourceCatalog.Create(due.SourceExports);
        Assert.True(
            sources.IsValid,
            string.Join(Environment.NewLine, sources.Issues));
        var resources = AcceptedMechanicsPlanner.BuildResources(
            new AcceptedMechanicsResourceInput(
                Turn: 42,
                Definitions: definitions,
                State: bootstrap.State!,
                History: bootstrap.History!,
                Sources: sources.Catalog!,
                Mutations: due.Mutations,
                InitialTriggerCandidates: due.TriggerCandidates,
                InitialEffectResolutionWork: due.Work,
                EffectPlanAuthority:
                    AcceptedMechanicsPlanner.CreateEffectPlanAuthority(
                        beforeFinalize)),
            new AcceptedMechanicsIdentityFactory());

        Assert.True(
            resources.IsValid,
            string.Join(Environment.NewLine, resources.Issues));
        var evidence = Assert.Single(
            resources.EffectBoundaryTranscript.AppliedComponentEvidence);
        Assert.Equal("reaction_periodic", evidence.ComponentId);
        var release = Assert.Single(
            resources.EffectBoundaryTranscript.ReleasedReactions);
        Assert.Equal(EffectReactionReleaseStage.AfterComponent, release.Stage);
        Assert.Equal(evidence.Boundary, release.Boundary);

        var finalized = EffectAcceptedTurnPlanner.CompleteAcceptedBoundaryTranscript(
            beforeFinalize,
            resources.EffectBoundaryTranscript,
            new CountingFactory());

        var plan = Assert.IsType<EffectAcceptedTurnPlan>(finalized.Plan);
        Assert.Empty(finalized.Issues);
        Assert.Equal(
            "suspended",
            Assert.Single(plan.IdentityIndexAfterImage["entries"]!.AsArray())!
                ["state"]!.GetValue<string>());
    }

    [Fact]
    public void ReactionExecutor_ResourceEventBuildsDirectReactionFromExactAppliedEvent()
    {
        var definition = EffectMaterializationTestFixture.CreateDefinition("event_reaction");
        definition["components"]![0]!["payload"]!["eventType"] = "resource_depleted";
        definition["triggers"]![0]!["eventType"] = "resource_depleted";
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect(
            profile: "event_reaction");
        effect["components"] = definition["components"]!.DeepClone();
        effect["triggers"] = definition["triggers"]!.DeepClone();
        var input = CreateReactionInput(effect, definition);
        var occurrence = Assert.Single(EffectCarrierCatalog.Build(
            input.PreTurnCarriers!).Occurrences);
        var producer = new ResourceAppliedEvent(
            "resource_depleted",
            "resource_operation_test",
            "turn_42:resource:test",
            new ResourceCoordinate(
                "mortal_world",
                ResourceOwnerKind.Player,
                "player_current",
                "health"),
            Before: 1m,
            After: 0m,
            AppliedAmount: 1m,
            Turn: 42,
            ExecutionSequence: 1,
            SourceFingerprint: "source_fingerprint_test");

        var planned = EffectReactionExecutor.PlanResourceEvent(
            occurrence,
            "on_owner_damaged",
            producer,
            input.SourceAuthority);

        Assert.Empty(planned.Issues);
        var execution = Assert.Single(planned.Executions);
        Assert.Equal("remove", execution.ResultKind);
        Assert.Equal(producer.EventRef, execution.TriggerEventRef);
        Assert.Equal(producer.EventRef, execution.CausalEventRef);
    }

    [Fact]
    public void ResourceEventTrigger_CombatantAliasUsesAcceptedNpcResourceOwnerBinding()
    {
        const string npcId = "npc_bound_resource_owner";
        var definition = EffectMaterializationTestFixture.CreateDefinition(
            "periodic_restore");
        definition["triggers"]![0]!["triggerId"] = "on_resource_depleted";
        definition["triggers"]![0]!["eventType"] = "resource_depleted";
        var combatRoot = new JsonObject
        {
            ["enemiesData"] = new JsonArray(new JsonObject
            {
                ["combatantId"] = EffectMaterializationTestFixture.CombatantId,
                ["activeBuffs"] = new JsonArray(),
                ["activeDebuffs"] = new JsonArray()
            })
        };
        var input = CreateInput(
            targetKind: "combatant",
            definition: definition,
            carriers: new EffectCarrierCatalogInput(
                null,
                null,
                combatRoot,
                null,
                null,
                null),
            boundNpcId: npcId);
        var built = EffectAcceptedTurnPlanner.Build(
            input,
            "resource-combatant-alias-plan",
            new CountingFactory());
        var plan = Assert.IsType<EffectAcceptedTurnPlan>(built.Plan);
        Assert.Empty(built.Issues);

        var definitions = ResourceDefinitionCatalog.CreateBuiltIn();
        var coordinate = new ResourceCoordinate(
            "mortal_world",
            ResourceOwnerKind.Npc,
            npcId,
            "health");
        var owners = ResourceOwnerAuthority.Build(new ResourceOwnerAuthorityInput(
            new[]
            {
                new ResourceOwnerExport(
                    new ResourceOwnerKey(
                        coordinate.Realm,
                        coordinate.OwnerKind,
                        coordinate.ResourceOwnerId),
                    ResourceOwnerLifecycle.Active,
                    SameTurn: false,
                    OwnerRef: null,
                    BoundNpcId: npcId,
                    new HashSet<string>(StringComparer.Ordinal) { coordinate.ResourceKey },
                    "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")
            },
            Array.Empty<ResourceOwnerExport>(),
            Array.Empty<ResourceOwnerKey>()));
        Assert.Empty(owners.Issues);
        var producerKey = new ResourceOperationKey(
            "turn_42:resource:npc_depleted",
            "combat_outcome",
            "npc_damage",
            coordinate,
            ResourceOperation.Damage);
        var producer = new ResourceAppliedEvent(
            "resource_depleted",
            "resource_operation_npc_depleted",
            producerKey.EventRef,
            coordinate,
            Before: 1m,
            After: 0m,
            AppliedAmount: 1m,
            Turn: 42,
            ExecutionSequence: 1,
            SourceFingerprint:
                "sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb");

        var resolved = EffectAcceptedTurnPlanner.ResolveResourceEventMutations(
            plan,
            producer,
            producerKey,
            owners,
            definitions);
        var repeated = EffectAcceptedTurnPlanner.ResolveResourceEventMutations(
            plan,
            producer,
            producerKey,
            owners,
            definitions);

        Assert.Empty(resolved.Issues);
        Assert.Empty(repeated.Issues);
        Assert.Equal(coordinate, Assert.Single(resolved.Mutations).Coordinate);
        Assert.Equal(coordinate, Assert.Single(repeated.Mutations).Coordinate);
        Assert.Equal(1, plan.ResourceTriggerIndex.Statistics.CatalogBuildCount);
        Assert.Equal(1, plan.ResourceTriggerIndex.Statistics.OccurrenceVisitCount);
        Assert.Equal(1, resolved.Work.IndexLookupCount);
        Assert.Equal(1, resolved.Work.CandidateVisitCount);
        Assert.Equal(1, repeated.Work.IndexLookupCount);
        Assert.Equal(1, repeated.Work.CandidateVisitCount);
    }

    [Fact]
    public void ResourceEventTrigger_CombatantGroupMemberUsesAcceptedResourceOwnerBinding()
    {
        const string memberId = EffectMaterializationTestFixture.CombatantId;
        var definition = EffectMaterializationTestFixture.CreateDefinition(
            "periodic_restore");
        definition["triggers"]![0]!["triggerId"] = "on_resource_depleted";
        definition["triggers"]![0]!["eventType"] = "resource_depleted";
        var combatRoot = new JsonObject
        {
            ["enemiesData"] = new JsonArray(new JsonObject
            {
                ["combatantId"] = "combat_group_resource_owner",
                ["isGroup"] = true,
                ["activeBuffs"] = new JsonArray(),
                ["activeDebuffs"] = new JsonArray(),
                ["members"] = new JsonArray(new JsonObject
                {
                    ["combatantId"] = memberId,
                    ["activeBuffs"] = new JsonArray(),
                    ["activeDebuffs"] = new JsonArray()
                })
            })
        };
        var input = CreateInput(
            targetKind: "combatant",
            definition: definition,
            carriers: new EffectCarrierCatalogInput(
                null,
                null,
                combatRoot,
                null,
                null,
                null),
            boundResourceOwnerKind: ResourceOwnerKind.CombatGroupMember);
        var built = EffectAcceptedTurnPlanner.Build(
            input,
            "resource-combatant-group-member-plan",
            new CountingFactory());
        var plan = Assert.IsType<EffectAcceptedTurnPlan>(built.Plan);
        Assert.Empty(built.Issues);

        var definitions = ResourceDefinitionCatalog.CreateBuiltIn();
        var coordinate = new ResourceCoordinate(
            "mortal_world",
            ResourceOwnerKind.CombatGroupMember,
            memberId,
            "health");
        var owners = CreateActiveResourceOwnerAuthority(coordinate);
        var producerKey = new ResourceOperationKey(
            "turn_42:resource:group_member_depleted",
            "combat_outcome",
            "group_member_damage",
            coordinate,
            ResourceOperation.Damage);
        var producer = new ResourceAppliedEvent(
            "resource_depleted",
            "resource_operation_group_member_depleted",
            producerKey.EventRef,
            coordinate,
            Before: 1m,
            After: 0m,
            AppliedAmount: 1m,
            Turn: 42,
            ExecutionSequence: 1,
            SourceFingerprint:
                "sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb");

        var resolved = EffectAcceptedTurnPlanner.ResolveResourceEventMutations(
            plan,
            producer,
            producerKey,
            owners,
            definitions);

        Assert.Empty(resolved.Issues);
        Assert.Equal(coordinate, Assert.Single(resolved.Mutations).Coordinate);
    }

    [Fact]
    public void ResourceEventReaction_PlayerSoulUsesAcceptedAfterlifeActorResourceOwnerBinding()
    {
        const string sourceId = "art_player_soul_resource_binding";
        var definition = EffectMaterializationTestFixture.CreateDefinition(
            "event_reaction");
        definition["allowedRealms"] = new JsonArray("chaos_sea");
        definition["allowedTargetKinds"] = new JsonArray("player");
        definition["components"]![0]!["payload"]!["eventType"] =
            "resource_depleted";
        definition["triggers"]![0]!["eventType"] = "resource_depleted";
        definition["lifetime"]!["advancePhase"] = "afterlife_exchange_end";
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect(
            profile: "event_reaction");
        effect["realm"] = "chaos_sea";
        effect["target"] = new JsonObject
        {
            ["kind"] = "player",
            ["targetId"] = "player_soul"
        };
        effect["source"] = new JsonObject
        {
            ["kind"] = "spiritual_art",
            ["sourceId"] = sourceId,
            ["definitionKey"] = EffectMaterializationTestFixture.DefinitionKey
        };
        effect["components"] = definition["components"]!.DeepClone();
        effect["triggers"] = definition["triggers"]!.DeepClone();
        effect["lifetime"]!["advancePhase"] = "afterlife_exchange_end";
        var source = EffectSourceAuthority.Build(new EffectSourceAuthorityInput(
            new[]
            {
                new EffectSourceExport(
                    "chaos_sea",
                    "spiritual_art",
                    sourceId,
                    new JsonArray(definition.DeepClone()),
                    Materializable: true,
                    Active: true,
                    SameTurn: false)
            },
            Array.Empty<EffectSourceExport>(),
            new HashSet<string>(StringComparer.Ordinal)));
        Assert.Empty(source.Issues);
        var target = EffectTargetAuthority.Build(new EffectTargetAuthorityInput(
            new[]
            {
                new EffectTargetExport(
                    "chaos_sea",
                    "player",
                    "player_soul",
                    SameTurn: false)
            },
            Array.Empty<EffectTargetExport>(),
            new HashSet<string>(StringComparer.Ordinal),
            CombatantIdentities: null));
        Assert.Empty(target.Issues);
        var identity = EffectMaterializationTestFixture.CreateIdentityIndex(effect);
        var identityEntry = Assert.IsType<JsonObject>(
            Assert.Single(identity["entries"]!.AsArray()));
        var identityOwner = identityEntry["owner"]!.AsObject();
        identityOwner["carrierPath"] = EffectCarrierCatalog.AfterlifeProfilesPath;
        var input = new EffectAcceptedTurnInput(
            "session_player_soul_resource_binding",
            "snapshot_player_soul_resource_binding",
            EffectMaterializationTestFixture.CreateCommandRoot(),
            source,
            target,
            new JsonObject
            {
                ["turn"] = 42,
                ["events"] = new JsonArray(CreateAcceptedEvent(
                    "accepted_turn",
                    "turn_42",
                    "turn_42:accepted_player_soul_effect"))
            },
            Realm: "chaos_sea",
            PreTurnCarriers: new EffectCarrierCatalogInput(
                null,
                null,
                null,
                null,
                new JsonObject
                {
                    ["schemaVersion"] = 1,
                    ["profiles"] = new JsonArray(new JsonObject
                    {
                        ["actorType"] = "player_soul",
                        ["actorId"] = "player_soul",
                        ["realm"] = "Chaos Sea",
                        ["activeEffects"] = new JsonArray(effect.DeepClone())
                    })
                },
                null),
            PreTurnIdentityIndex: identity);
        var built = EffectAcceptedTurnPlanner.Build(
            input,
            "resource-player-soul-plan",
            new CountingFactory());
        Assert.Empty(built.Issues);
        var plan = Assert.IsType<EffectAcceptedTurnPlan>(built.Plan);

        var definitions = ResourceDefinitionCatalog.CreateBuiltIn();
        var coordinate = new ResourceCoordinate(
            "chaos_sea",
            ResourceOwnerKind.AfterlifeActor,
            "player_soul",
            "spiritual_action_points");
        var owners = CreateActiveResourceOwnerAuthority(coordinate);
        var producerKey = new ResourceOperationKey(
            "turn_42:resource:player_soul_depleted",
            "afterlife_exchange",
            "player_soul_spiritual_cost",
            coordinate,
            ResourceOperation.Spend);
        var producer = new ResourceAppliedEvent(
            "resource_depleted",
            "resource_operation_player_soul_depleted",
            producerKey.EventRef,
            coordinate,
            Before: 1m,
            After: 0m,
            AppliedAmount: 1m,
            Turn: 42,
            ExecutionSequence: 1,
            SourceFingerprint:
                "sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb");

        var resolved = EffectAcceptedTurnPlanner.ResolveResourceEventMutations(
            plan,
            producer,
            producerKey,
            owners,
            definitions);

        Assert.Empty(resolved.Issues);
        var acceptedTrigger = AcceptSingleReactionOnlyTrigger(
            resolved,
            out var acceptedReactions);
        Assert.Equal(producerKey, Assert.Single(resolved.TriggerCandidates).Producer);
        Assert.Equal(producer.EventRef, acceptedTrigger.TriggerEventRef);
        var reaction = Assert.Single(acceptedReactions);
        Assert.Equal(producer.EventRef, reaction.TriggerEventRef);
    }

    private static EffectAcceptedTurnInput CreateReactionInput(
        JsonObject effect,
        params JsonObject[] definitions)
    {
        var source = EffectSourceAuthority.Build(new EffectSourceAuthorityInput(
            new[]
            {
                new EffectSourceExport(
                    "mortal_world",
                    "wound",
                    "wound_test_torn_side",
                    new JsonArray(definitions.Select(static value =>
                        (JsonNode)value.DeepClone()).ToArray()),
                    Materializable: true,
                    Active: true,
                    SameTurn: false)
            },
            Array.Empty<EffectSourceExport>(),
            new HashSet<string>(StringComparer.Ordinal)));
        var target = EffectTargetAuthority.Build(new EffectTargetAuthorityInput(
            new[]
            {
                new EffectTargetExport(
                    "mortal_world",
                    "player",
                    "player_current",
                    SameTurn: false)
            },
            Array.Empty<EffectTargetExport>(),
            new HashSet<string>(StringComparer.Ordinal),
            CombatantIdentities: null));
        return new EffectAcceptedTurnInput(
            "session_effect_reaction",
            "snapshot_effect_reaction",
            EffectMaterializationTestFixture.CreateCommandRoot(),
            source,
            target,
            new JsonObject
            {
                ["turn"] = 42,
                ["events"] = new JsonArray(
                    CreateAcceptedEvent(
                        "accepted_turn",
                        "turn_42",
                        "turn_42:accepted_effect")),
                ["lifecycleEvents"] = new JsonArray(new JsonObject
                {
                    ["eventRef"] = "turn_42:reaction:owner_damaged:1",
                    ["causalEventRef"] = "turn_42:owner_damaged:1",
                    ["turn"] = 42,
                    ["phase"] = "owner_damaged",
                    ["realm"] = "mortal_world",
                    ["target"] = new JsonObject
                    {
                        ["kind"] = "player",
                        ["targetId"] = "player_current"
                    },
                    ["effectId"] = effect["effectId"]!.GetValue<string>(),
                    ["triggerId"] = "on_owner_damaged"
                })
            },
            PreTurnCarriers: new EffectCarrierCatalogInput(
                new JsonObject
                {
                    ["schemaVersion"] = 1,
                    ["activeEffects"] = new JsonArray(effect.DeepClone())
                },
                null,
                null,
                null,
                null,
                null),
            PreTurnIdentityIndex: EffectMaterializationTestFixture.CreateIdentityIndex(effect));
    }

    private static EffectAcceptedTurnInput CreateInput(
        string targetKind = "player",
        JsonObject? command = null,
        JsonObject? definition = null,
        EffectCarrierCatalogInput? carriers = null,
        string? boundNpcId = null,
        ResourceOwnerKind? boundResourceOwnerKind = null)
    {
        var targetId = targetKind switch
        {
            "player" => "player_current",
            "npc" => "npc_test_healer",
            "combatant" => EffectMaterializationTestFixture.CombatantId,
            _ => throw new ArgumentOutOfRangeException(nameof(targetKind), targetKind, null)
        };
        var source = EffectSourceAuthority.Build(new EffectSourceAuthorityInput(
            new[]
            {
                new EffectSourceExport(
                    "mortal_world",
                    "wound",
                    "wound_test_torn_side",
                    new JsonArray(definition ?? EffectMaterializationTestFixture.CreateDefinition()),
                    Materializable: true,
                    Active: true,
                    SameTurn: false)
            },
            Array.Empty<EffectSourceExport>(),
            new HashSet<string>(StringComparer.Ordinal)));
        var target = EffectTargetAuthority.Build(new EffectTargetAuthorityInput(
            new[]
            {
                new EffectTargetExport(
                    "mortal_world",
                    targetKind,
                    targetId,
                    SameTurn: false,
                    BoundNpcId: boundNpcId,
                    BoundResourceOwnerKind: boundResourceOwnerKind)
            },
            Array.Empty<EffectTargetExport>(),
            new HashSet<string>(StringComparer.Ordinal),
            null));
        return new EffectAcceptedTurnInput(
            "session_effect_test",
            "snapshot_effect_test",
            EffectMaterializationTestFixture.CreateCommandRoot(
                command ?? EffectMaterializationTestFixture.CreateApplyCommand(targetKind)),
            source,
            target,
            new JsonObject
            {
                ["turn"] = 42,
                ["events"] = new JsonArray(
                    CreateAcceptedEvent("accepted_turn", "turn_42", "turn_42:wound_opened"))
            },
            PreTurnCarriers: carriers,
            PreTurnIdentityIndex: null);
    }

    private static ResourceOwnerAuthority CreateActiveResourceOwnerAuthority(
        ResourceCoordinate coordinate)
    {
        var authority = ResourceOwnerAuthority.Build(new ResourceOwnerAuthorityInput(
            new[]
            {
                new ResourceOwnerExport(
                    new ResourceOwnerKey(
                        coordinate.Realm,
                        coordinate.OwnerKind,
                        coordinate.ResourceOwnerId),
                    ResourceOwnerLifecycle.Active,
                    SameTurn: false,
                    OwnerRef: null,
                    BoundNpcId: null,
                    new HashSet<string>(StringComparer.Ordinal)
                    {
                        coordinate.ResourceKey
                    },
                    "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")
            },
            Array.Empty<ResourceOwnerExport>(),
            Array.Empty<ResourceOwnerKey>()));
        Assert.Empty(authority.Issues);
        return authority;
    }

    private static EffectAcceptedTurnPlanner.EffectResourceTriggerExecution
        AcceptSingleReactionOnlyTrigger(
        EffectAcceptedTurnPlanner.EffectPeriodicResourceResolution resolution,
        out IReadOnlyList<EffectReactionExecution> acceptedReactions)
    {
        Assert.Empty(resolution.Mutations);
        Assert.Empty(resolution.ReactionExecutions);
        var candidate = Assert.Single(resolution.TriggerCandidates);
        Assert.Empty(candidate.PlannedMutationKeys);
        Assert.Empty(candidate.PlannedComponentIdsByMutation);

        var useSeeds = candidate.UseSeed is { } useSeed
            ? new[] { useSeed }
            : Array.Empty<CanonicalEffectUseSeed>();
        var initialized = AcceptedEffectUseArbiter.Initialize(useSeeds);
        Assert.True(
            initialized.IsValid,
            string.Join(
                Environment.NewLine,
                initialized.Issues.Select(static issue =>
                    $"{issue.Code}: {issue.Message}")));
        var arbiter = Assert.IsType<AcceptedEffectUseArbiter>(initialized.Arbiter);
        var arbitration = arbiter.Arbitrate(new[] { candidate.Activation });
        Assert.True(
            arbitration.IsValid,
            string.Join(
                Environment.NewLine,
                arbitration.Issues.Select(static issue =>
                    $"{issue.Code}: {issue.Message}")));
        var accepted = Assert.Single(arbitration.AcceptedActivations);
        acceptedReactions = candidate.ReactionOutputs;
        return new EffectAcceptedTurnPlanner.EffectResourceTriggerExecution(
            accepted.Stamp.Identity.EffectId,
            accepted.Stamp.Identity.TriggerId,
            accepted.Stamp.Identity.EventKind,
            accepted.Stamp.Identity.EventRef,
            Array.Empty<ResourceOperationKey>(),
            accepted.Stamp.UsesBefore,
            Array.Empty<string>(),
            accepted.Stamp.Identity.TriggerEventRef,
            new Dictionary<ResourceOperationKey, string>());
    }

    [Fact]
    public void ValidatedEffectSubplanIsExposedOnlyToTheCommonPlannerByPeek()
    {
        var input = CreateInput();
        var cache = new EffectAcceptedTurnPlanCache(new CountingFactory());
        var validated = cache.GetOrBuildValidated(input);

        Assert.True(cache.TryPeekValidated(out var binding, out var handedOff));
        Assert.Equal(input.SessionId, binding.SessionId);
        Assert.Equal(input.SnapshotToken, binding.SnapshotToken);
        Assert.Same(validated, handedOff);

        cache.InvalidateValidated();
        Assert.False(cache.TryPeekValidated(out _));
    }

    [Fact]
    public void FailedRevalidationClearsPreviouslyValidatedPlanHandoff()
    {
        var input = CreateInput();
        var cache = new EffectAcceptedTurnPlanCache(new CountingFactory());
        Assert.True(cache.GetOrBuildValidated(input).Success);
        var invalidInput = input with
        {
            SourceAuthority = EffectSourceAuthority.Build(
                new EffectSourceAuthorityInput(
                    Array.Empty<EffectSourceExport>(),
                    Array.Empty<EffectSourceExport>(),
                    new HashSet<string>(StringComparer.Ordinal)))
        };

        Assert.False(cache.GetOrBuildValidated(invalidInput).Success);
        Assert.False(cache.TryPeekValidated(out _));
    }

    private static EffectAcceptedTurnInput Change(EffectAcceptedTurnInput input, string part) =>
        part switch
        {
            "session" => input with { SessionId = "session_changed" },
            "snapshot" => input with { SnapshotToken = "snapshot_changed" },
            "commands" => input with
            {
                RawCommands = CreateChangedCommands()
            },
            "source" => input with
            {
                SourceAuthority = EffectSourceAuthority.Build(new EffectSourceAuthorityInput(
                    new[]
                    {
                        new EffectSourceExport(
                            "mortal_world",
                            "wound",
                            "wound_test_torn_side",
                            new JsonArray(EffectMaterializationTestFixture.CreateDefinition()),
                            true,
                            true,
                            false),
                        new EffectSourceExport(
                            "mortal_world",
                            "skill",
                            "skill_changed",
                            new JsonArray(EffectMaterializationTestFixture.CreateDefinition()),
                            true,
                            true,
                            false)
                    },
                    Array.Empty<EffectSourceExport>(),
                    new HashSet<string>(StringComparer.Ordinal)))
            },
            "target" => input with
            {
                TargetAuthority = EffectTargetAuthority.Build(new EffectTargetAuthorityInput(
                    new[]
                    {
                        new EffectTargetExport("mortal_world", "player", "player_current", false),
                        new EffectTargetExport("mortal_world", "npc", "npc_changed", false)
                    },
                    Array.Empty<EffectTargetExport>(),
                    new HashSet<string>(StringComparer.Ordinal),
                    null))
            },
            "event" => input with
            {
                EventInput = new JsonObject
                {
                    ["turn"] = 42,
                    ["events"] = new JsonArray(
                        CreateAcceptedEvent("accepted_turn", "turn_42", "turn_42:changed"))
                }
            },
            "carrier" => input with
            {
                PreTurnCarriers = new EffectCarrierCatalogInput(
                    new JsonObject
                    {
                        ["schemaVersion"] = 1,
                        ["activeEffects"] = new JsonArray()
                    },
                    null,
                    null,
                    null,
                    null,
                    null)
            },
            "accepted_carrier" => input with
            {
                AcceptedCarrierBaselines = CreateChangedCarrierBaselines()
            },
            "publication_carrier" => input with
            {
                PublicationCarrierBaselines = CreateChangedCarrierBaselines()
            },
            "index" => input with
            {
                PreTurnIdentityIndex = new JsonObject
                {
                    ["schemaVersion"] = 1,
                    ["entries"] = new JsonArray()
                }
            },
            _ => throw new ArgumentOutOfRangeException(nameof(part), part, null)
        };

    private static EffectCarrierCatalogInput CreateChangedCarrierBaselines() =>
        new(
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["activeEffects"] = new JsonArray()
            },
            null,
            null,
            null,
            null,
            null);

    private static JsonObject CreateChangedCommands()
    {
        var change = EffectMaterializationTestFixture.CreateApplyCommand();
        change["reason"] = "Изменившееся событие.";
        return EffectMaterializationTestFixture.CreateCommandRoot(change);
    }

    private static JsonObject CreateAcceptedEvent(string kind, string authorityId, string eventRef) =>
        new()
        {
            ["kind"] = kind,
            ["authorityId"] = authorityId,
            ["eventRef"] = eventRef
        };

    private sealed class CountingFactory : EffectIdentityFactory
    {
        internal int EffectCalls { get; private set; }
        internal int TransitionCalls { get; private set; }

        internal override string CreateEffectId()
        {
            EffectCalls++;
            return base.CreateEffectId();
        }

        internal override string CreateTransitionId()
        {
            TransitionCalls++;
            return base.CreateTransitionId();
        }
    }

    private sealed class ScriptedIdentityFactory : EffectIdentityFactory
    {
        private readonly string _effectId;
        private readonly string _suffix;
        private int _transitionOrdinal;

        internal ScriptedIdentityFactory(string effectId, string suffix)
        {
            _effectId = effectId;
            _suffix = suffix;
        }

        internal override string CreateEffectId() => _effectId;

        internal override string CreateTransitionId() =>
            $"effect_transition_{_suffix}_{++_transitionOrdinal}";
    }
}
