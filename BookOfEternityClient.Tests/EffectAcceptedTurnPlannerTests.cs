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
        Assert.Equal(plan.AllocatedEffectIds.Single(), Assert.Single(parsedIndex.State!.Entries).EffectId);
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

        var plan = Assert.IsType<EffectAcceptedTurnPlan>(result.Plan);
        Assert.Empty(result.Issues);
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
    public void ResponseModelAndMappingExposeOnlyCommonTransientEffectRoutes()
    {
        var responseFields = typeof(GameResponse)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(property => property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name)
            .Where(static name => name != null)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("effectChanges", responseFields);
        Assert.Contains("effectResolutionReceipts", responseFields);
        Assert.DoesNotContain("playerActiveEffectsChanges", responseFields);
        Assert.DoesNotContain("NPCEffectChanges", responseFields);
        Assert.Equal(EffectAcceptedTurnPlan.CommandPath, FileMapping.FieldToFile["effectChanges"]);
        Assert.Equal(EffectAcceptedTurnPlan.CommandPath, FileMapping.FieldToFile["effectResolutionReceipts"]);
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

        var plan = Assert.IsType<EffectAcceptedTurnPlan>(result.Plan);
        Assert.Empty(result.Issues);
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
        var input = CreateInput();
        var plan = Assert.IsType<EffectAcceptedTurnPlan>(cache.GetOrBuild(input).Plan);
        var effectId = Assert.Single(plan.AllocatedEffectIds);

        Assert.Single(plan.ActiveEffects)["effectId"] = "effect_forged";
        plan.CarrierAfterImages[EffectCarrierCatalog.PlayerPath]["activeEffects"] = new JsonArray();
        plan.IdentityIndexAfterImage["entries"] = new JsonArray();

        var cached = Assert.IsType<EffectAcceptedTurnPlan>(cache.GetOrBuild(input).Plan);
        Assert.Same(plan, cached);
        Assert.Equal(effectId, Assert.Single(cached.ActiveEffects)["effectId"]!.GetValue<string>());
        Assert.Single(cached.CarrierAfterImages[EffectCarrierCatalog.PlayerPath]["activeEffects"]!.AsArray());
        Assert.Single(cached.IdentityIndexAfterImage["entries"]!.AsArray());
    }

    private static EffectAcceptedTurnInput CreateInput(
        string targetKind = "player",
        JsonObject? command = null,
        JsonObject? definition = null,
        EffectCarrierCatalogInput? carriers = null)
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
            new[] { new EffectTargetExport("mortal_world", targetKind, targetId, SameTurn: false) },
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

    [Fact]
    public void ValidatedEffectSubplanIsExposedOnlyToTheCommonPlannerByPeek()
    {
        var input = CreateInput();
        var cache = new EffectAcceptedTurnPlanCache(new CountingFactory());
        var validated = cache.GetOrBuildValidated(input);

        Assert.True(cache.TryPeekValidated(out var handedOff));
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
}
