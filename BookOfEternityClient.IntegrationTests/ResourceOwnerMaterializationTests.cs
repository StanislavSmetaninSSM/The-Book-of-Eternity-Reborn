using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ResourceOwnerMaterializationTests
{
    [Fact]
    public async Task NpcStateValidation_RejectsLegacyHealthPercentageAuthority()
    {
        await using var context = await ResourceMaterializationTestContext.CreateAsync();
        var root = MortalActorTestFixtures.CreateNpcCoreRoot();
        var npc = Assert.IsType<JsonObject>(Assert.Single(root["NPCsInScene"]!.AsArray()));
        npc["currentHealthPercentage"] = "75%";
        npc["maxHealthPercentage"] = "100%";
        await context.WriteExactJsonAsync(
            "game_state/npcs/npc_core.json",
            root.ToJsonString());

        var issues = await context.Validator.ValidateGameStateAsync(
            GameStateValidationPhase.NpcStateFiles);

        Assert.Contains(issues, issue =>
            issue.Code == "resource_owner_legacy_value_forbidden" &&
            issue.FilePath.EndsWith(".currentHealthPercentage", StringComparison.Ordinal));
        Assert.Contains(issues, issue =>
            issue.Code == "resource_owner_legacy_value_forbidden" &&
            issue.FilePath.EndsWith(".maxHealthPercentage", StringComparison.Ordinal));
    }

    [Fact]
    public async Task NpcStateValidation_RequiresResourceMaterializationForSameTurnNpc()
    {
        await using var context = await ResourceMaterializationTestContext.CreateAsync();
        var npc = EffectMaterializationTestFixture.CreateSameTurnMortalActor(
            "npc_ref_resource_field_medic");
        await context.WriteExactJsonAsync(
            "game_state/npcs/npc_core.json",
            new JsonObject
            {
                ["UpdateNPCs"] = new JsonArray(npc)
            }.ToJsonString());

        var issues = await context.Validator.ValidateGameStateAsync(
            GameStateValidationPhase.NpcStateFiles);

        Assert.Contains(issues, issue =>
            issue.Code == "resource_owner_materialization_required" &&
            issue.FilePath.EndsWith(".resourceMaterialization", StringComparison.Ordinal));
    }

    [Fact]
    public async Task NpcStateValidation_RejectsResourceMaterializationForExistingNpc()
    {
        await using var context = await ResourceMaterializationTestContext.CreateAsync();
        var root = MortalActorTestFixtures.CreateNpcCoreRoot();
        var npc = Assert.IsType<JsonObject>(Assert.Single(root["NPCsInScene"]!.AsArray()));
        npc["resourceMaterialization"] = Materialization(("health", 60m));
        await context.WriteExactJsonAsync(
            "game_state/npcs/npc_core.json",
            root.ToJsonString());

        var issues = await context.Validator.ValidateGameStateAsync(
            GameStateValidationPhase.NpcStateFiles);

        Assert.Contains(issues, issue =>
            issue.Code == "resource_owner_materialization_existing_forbidden" &&
            issue.FilePath.EndsWith(".resourceMaterialization", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AcceptedTurn_NewNpcPublishesPermanentOwnerAndInitializesHealthAtomically()
    {
        await using var context = await ResourceMaterializationTestContext.CreateAsync();
        var bootstrap = ResourceBootstrapStateBuilder.BuildPristine();
        Assert.True(bootstrap.IsValid, string.Join(Environment.NewLine, bootstrap.Issues));
        await context.WriteExactJsonAsync(
            ResourceMaterializationTestContext.DefinitionsPath,
            bootstrap.Definitions!.ToCanonicalJson());
        await context.WriteExactJsonAsync(
            ResourceMaterializationTestContext.StatePath,
            bootstrap.State!.ToCanonicalJson());
        await context.WriteExactJsonAsync(
            ResourceMaterializationTestContext.HistoryPath,
            bootstrap.History!.ToCanonicalJson());
        await context.WriteExactJsonAsync(
            "game_state/npcs/npc_core.json",
            new JsonObject { ["NPCsInScene"] = new JsonArray() }.ToJsonString());
        await context.CaptureValidatedPendingSnapshotAsync(turn: 42);

        const string npcRef = "npc_ref_resource_field_medic";
        var npc = EffectMaterializationTestFixture.CreateSameTurnMortalActor(npcRef);
        npc["resourceMaterialization"] = Materialization(("health", 60m));
        await context.WriteExactJsonAsync(
            "game_state/npcs/npc_core.json",
            new JsonObject
            {
                ["UpdateNPCs"] = new JsonArray(npc)
            }.ToJsonString());

        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();

        Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);
        Assert.True(AcceptedMechanicsPlanAuthority.TryPeekValidated(
            context.FileSystem,
            out _,
            out var planning));
        var plan = Assert.IsType<AcceptedMechanicsPlan>(planning.Plan);
        var npcOwner = Assert.Single(
            plan.OwnerAuthority.Entries.Values,
            entry => entry.Key.OwnerKind == ResourceOwnerKind.Npc);
        Assert.Equal(npcRef, npcOwner.Key.ResourceOwnerId);
        Assert.True(npcOwner.SameTurn);
        Assert.Equal(npcRef, npcOwner.SameTurnRef);

        var state = ResourceStateContract.ParseCanonical(
            plan.StateAfterImage.ToJsonString(),
            bootstrap.Definitions,
            allowMissingPristine: false);
        Assert.True(state.IsValid, string.Join(Environment.NewLine, state.Issues));
        var health = Assert.Single(state.Ledger!.Entries);
        Assert.Equal(npcRef, health.Coordinate.ResourceOwnerId);
        Assert.Equal("health", health.Coordinate.ResourceKey);
        Assert.Equal(60m, health.Current);
        Assert.Equal(60m, health.Maximum);

        var ownerTransition = Assert.Single(plan.OwnerTransitions);
        Assert.Equal(
            AcceptedMechanicsOwnerTransitionKind.MortalNpcCreation,
            ownerTransition.Kind);
        Assert.Equal("game_state/npcs/npc_core.json", ownerTransition.Path);
        Assert.Equal(npcRef, ownerTransition.OwnerRef);
        Assert.Equal(npcRef, ownerTransition.PermanentOwnerId);

        await using (var writeLease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            var published = await context.Normalizer.BindTo(writeLease)
                .NormalizeAcceptedMechanicsAsync(backups: null);
            Assert.Same(plan, published);
        }

        var canonicalRoot = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            "game_state/npcs/npc_core.json"));
        var canonicalNpc = Assert.IsType<JsonObject>(
            Assert.Single(canonicalRoot["UpdateNPCs"]!.AsArray()));
        Assert.Equal(npcRef, canonicalNpc["NPCId"]!.GetValue<string>());
        Assert.False(canonicalNpc.ContainsKey("initialId"));
        Assert.False(canonicalNpc.ContainsKey("resourceMaterialization"));
    }

    [Fact]
    public async Task AcceptedTurn_NewNpcComposesWithHistoricalNpcNormalization()
    {
        await using var context = await ResourceMaterializationTestContext.CreateAsync();
        var bootstrap = ResourceBootstrapStateBuilder.BuildPristine();
        Assert.True(bootstrap.IsValid, string.Join(Environment.NewLine, bootstrap.Issues));
        await context.WriteExactJsonAsync(
            ResourceMaterializationTestContext.DefinitionsPath,
            bootstrap.Definitions!.ToCanonicalJson());
        await context.WriteExactJsonAsync(
            ResourceMaterializationTestContext.StatePath,
            bootstrap.State!.ToCanonicalJson());
        await context.WriteExactJsonAsync(
            ResourceMaterializationTestContext.HistoryPath,
            bootstrap.History!.ToCanonicalJson());

        const string npcPath = "game_state/npcs/npc_core.json";
        const string existingNpcId = "npc_resource_historical_teacher";
        var historicalNpc = EffectMaterializationTestFixture.CreateSameTurnMortalActor(
            existingNpcId);
        historicalNpc["NPCId"] = existingNpcId;
        historicalNpc.Remove("initialId");
        var preTurnRoot = new JsonObject
        {
            ["NPCsInScene"] = new JsonArray(historicalNpc)
        };
        await context.WriteExactJsonAsync(npcPath, preTurnRoot.ToJsonString());
        await context.CaptureValidatedPendingSnapshotAsync(turn: 42);

        var acceptedHistoricalNpc = historicalNpc.DeepClone().AsObject();
        acceptedHistoricalNpc.Remove(ActorMaterializationContract.PropertyName);
        const string newNpcRef = "npc_ref_resource_apprentice";
        var newNpc = EffectMaterializationTestFixture.CreateSameTurnMortalActor(newNpcRef);
        newNpc["resourceMaterialization"] = Materialization(("health", 45m));
        await context.WriteExactJsonAsync(
            npcPath,
            new JsonObject
            {
                ["NPCsInScene"] = new JsonArray(acceptedHistoricalNpc),
                ["UpdateNPCs"] = new JsonArray(newNpc)
            }.ToJsonString());

        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();
        Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);

        var backupPath = $"game_state/control/pending_turn_snapshot/{npcPath}";
        await using (var writeLease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            await context.Normalizer.BindTo(writeLease).NormalizeAccumulatedStateAsync(
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [npcPath] = backupPath
                });
        }

        var canonicalRoot = Assert.IsType<JsonObject>(await context.ReadJsonAsync(npcPath));
        var canonicalHistoricalNpc = Assert.IsType<JsonObject>(
            Assert.Single(canonicalRoot["NPCsInScene"]!.AsArray()));
        Assert.Equal(
            historicalNpc[ActorMaterializationContract.PropertyName]!["materializationId"]!
                .GetValue<string>(),
            canonicalHistoricalNpc[ActorMaterializationContract.PropertyName]!["materializationId"]!
                .GetValue<string>());
        var canonicalNewNpc = Assert.IsType<JsonObject>(
            Assert.Single(canonicalRoot["UpdateNPCs"]!.AsArray()));
        Assert.Equal(newNpcRef, canonicalNewNpc["NPCId"]!.GetValue<string>());
        Assert.False(canonicalNewNpc.ContainsKey("initialId"));
        Assert.False(canonicalNewNpc.ContainsKey("resourceMaterialization"));
    }

    [Fact]
    public async Task RawValidation_UsesOneSharedCombatantIdentityForResourceAndEffectPlans()
    {
        await using var context = await ResourceMaterializationTestContext.CreateAsync();
        var resources = ResourceBootstrapStateBuilder.BuildPristine();
        Assert.True(resources.IsValid, string.Join(Environment.NewLine, resources.Issues));
        await context.WriteExactJsonAsync(
            ResourceMaterializationTestContext.DefinitionsPath,
            resources.Definitions!.ToCanonicalJson());
        await context.WriteExactJsonAsync(
            ResourceMaterializationTestContext.StatePath,
            resources.State!.ToCanonicalJson());
        await context.WriteExactJsonAsync(
            ResourceMaterializationTestContext.HistoryPath,
            resources.History!.ToCanonicalJson());
        await context.WriteExactJsonAsync(
            EffectCarrierCatalog.EnemiesPath,
            new JsonObject { ["enemiesData"] = new JsonArray() }.ToJsonString());
        await context.CaptureValidatedPendingSnapshotAsync();
        var sameTurnCombatant = EffectMaterializationTestFixture.CreateSameTurnCombatant(
            "combatant_ref_shared_resource_effect");
        foreach (var field in new[] { "maxHealth", "maxPoise", "currentHealth", "currentPoise" })
            sameTurnCombatant.Remove(field);
        sameTurnCombatant["resourceMaterialization"] = Materialization(
            ("health", 75m),
            ("poise", 40m));
        await context.WriteExactJsonAsync(
            EffectCarrierCatalog.EnemiesPath,
            new JsonObject
            {
                ["enemiesData"] = new JsonArray(sameTurnCombatant)
            }.ToJsonString());

        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();

        Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);
        Assert.True(AcceptedMechanicsPlanAuthority.TryPeekValidated(
            context.FileSystem,
            out _,
            out var planning));
        var plan = Assert.IsType<AcceptedMechanicsPlan>(planning.Plan);
        var owner = plan.OwnerAuthority.Resolve(new ResourceOwnerRequest(
            "mortal_world",
            ResourceOwnerKind.Combatant,
            "health",
            ResourceOwnerId: null,
            OwnerRef: "combatant_ref_shared_resource_effect"));
        Assert.True(owner.Success, string.Join(Environment.NewLine, owner.Issues));
        var permanentId = owner.Entry!.Key.ResourceOwnerId;

        var effectPlan = Assert.IsType<EffectAcceptedTurnPlan>(plan.EffectPlan);
        var combatRoot = effectPlan.CarrierAfterImages[EffectCarrierCatalog.EnemiesPath];
        var combatant = Assert.IsType<JsonObject>(
            Assert.Single(combatRoot["enemiesData"]!.AsArray()));
        Assert.Equal(permanentId, combatant["combatantId"]!.GetValue<string>());
        Assert.Null(combatant["combatantRef"]);
        Assert.Equal(new[] { permanentId }, effectPlan.AllocatedCombatantIds);
        Assert.DoesNotContain(
            EffectCarrierCatalog.EnemiesPath,
            plan.OwnerCompanionAfterImages.Keys);
    }

    [Fact]
    public async Task CommandlessGroupMembers_AreAllocatedAndPublishedByTheCommonPlan()
    {
        await using var context = await ResourceMaterializationTestContext.CreateAsync();
        var resources = ResourceBootstrapStateBuilder.BuildPristine();
        Assert.True(resources.IsValid, string.Join(Environment.NewLine, resources.Issues));
        await context.WriteExactJsonAsync(
            ResourceMaterializationTestContext.DefinitionsPath,
            resources.Definitions!.ToCanonicalJson());
        await context.WriteExactJsonAsync(
            ResourceMaterializationTestContext.StatePath,
            resources.State!.ToCanonicalJson());
        await context.WriteExactJsonAsync(
            ResourceMaterializationTestContext.HistoryPath,
            resources.History!.ToCanonicalJson());
        await context.WriteExactJsonAsync(
            EffectCarrierCatalog.EnemiesPath,
            new JsonObject { ["enemiesData"] = new JsonArray() }.ToJsonString());
        await context.CaptureValidatedPendingSnapshotAsync();
        await context.WriteExactJsonAsync(
            EffectCarrierCatalog.EnemiesPath,
            new JsonObject
            {
                ["enemiesData"] = new JsonArray(
                    new JsonObject
                    {
                        ["NPCId"] = null,
                        ["name"] = "Дозор",
                        ["image_prompt"] = "Два дозорных в дорожных плащах",
                        ["description"] = "Малый дорожный дозор",
                        ["type"] = "group",
                        ["isGroup"] = true,
                        ["actions"] = new JsonArray(),
                        ["resistances"] = new JsonArray(),
                        ["activeBuffs"] = new JsonArray(),
                        ["activeDebuffs"] = new JsonArray(),
                        ["count"] = 2,
                        ["unitName"] = "дозорный",
                        ["members"] = new JsonArray(
                            new JsonObject
                            {
                                ["memberRef"] = "member_ref_scout",
                                ["name"] = "Разведчик",
                                ["resourceMaterialization"] = Materialization(
                                    ("health", 30m),
                                    ("poise", 20m))
                            },
                            new JsonObject
                            {
                                ["memberRef"] = "member_ref_archer",
                                ["name"] = "Лучник",
                                ["resourceMaterialization"] = Materialization(
                                    ("health", 25m),
                                    ("poise", 15m))
                            })
                    })
            }.ToJsonString());

        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();

        Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);
        Assert.True(AcceptedMechanicsPlanAuthority.TryPeekValidated(
            context.FileSystem,
            out _,
            out var planning));
        var validatedPlan = Assert.IsType<AcceptedMechanicsPlan>(planning.Plan);
        var memberIds = new[] { "member_ref_scout", "member_ref_archer" }
            .Select(memberRef =>
            {
                var resolution = validatedPlan.OwnerAuthority.Resolve(
                    new ResourceOwnerRequest(
                        "mortal_world",
                        ResourceOwnerKind.CombatGroupMember,
                        "health",
                        ResourceOwnerId: null,
                        OwnerRef: memberRef));
                Assert.True(
                    resolution.Success,
                    string.Join(Environment.NewLine, resolution.Issues));
                return resolution.Entry!.Key.ResourceOwnerId;
            })
            .ToArray();

        await using var writeLease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var publishedPlan = await context.Normalizer.BindTo(writeLease)
            .NormalizeAcceptedMechanicsAsync(backups: null);

        Assert.Same(validatedPlan, publishedPlan);
        var root = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            EffectCarrierCatalog.EnemiesPath));
        var group = Assert.IsType<JsonObject>(Assert.Single(root["enemiesData"]!.AsArray()));
        var members = group["members"]!.AsArray().OfType<JsonObject>().ToArray();
        Assert.Equal(2, members.Length);
        Assert.All(members, member => Assert.False(member.ContainsKey("memberRef")));
        Assert.Equal(
            memberIds.OrderBy(static value => value, StringComparer.Ordinal),
            members
                .Select(member => member["memberId"]!.GetValue<string>())
                .OrderBy(static value => value, StringComparer.Ordinal));
    }

    [Fact]
    public void Compose_NamedNpcInCombatUsesTheSamePermanentResourceOwner()
    {
        var npcCore = new JsonObject
        {
            ["NPCsInScene"] = new JsonArray(
                new JsonObject
                {
                    ["NPCId"] = "npc_guard_captain",
                    ["name"] = "Капитан стражи"
                })
        };
        var enemies = new JsonObject
        {
            ["enemiesData"] = new JsonArray(
                new JsonObject
                {
                    ["NPCId"] = "npc_guard_captain",
                    ["name"] = "Капитан стражи",
                    ["isGroup"] = false
                })
        };

        var accepted = Roots(npcCore, enemies);
        var result = MortalResourceOwnerComposer.Compose(
            new MortalResourceOwnerCompositionInput(
                ResourceDefinitionCatalog.CreateBuiltIn(),
                accepted,
                accepted));

        Assert.Empty(result.Issues);
        var owners = Assert.IsType<ResourceOwnerAuthority>(result.Authority);
        var npcOwner = Assert.Single(
            owners.Entries.Values,
            entry => entry.Key.OwnerKind == ResourceOwnerKind.Npc);

        Assert.Equal("mortal_world", npcOwner.Key.Realm);
        Assert.Equal("npc_guard_captain", npcOwner.Key.ResourceOwnerId);
        Assert.Null(npcOwner.BoundNpcId);
        Assert.Contains("health", npcOwner.ResourceCapabilities);
        Assert.Contains("energy", npcOwner.ResourceCapabilities);
        Assert.DoesNotContain(
            owners.Entries.Values,
            entry => entry.Key.OwnerKind == ResourceOwnerKind.Combatant);
        Assert.Contains(
            owners.Entries.Values,
            entry => entry.Key.OwnerKind == ResourceOwnerKind.Player &&
                     entry.Key.ResourceOwnerId == "player_current");
    }

    [Fact]
    public void Compose_ConsumesEveryAnonymousCombatantAndGroupMemberRefWithoutCommands()
    {
        var enemies = new JsonObject
        {
            ["enemiesData"] = new JsonArray(
                new JsonObject
                {
                    ["combatantRef"] = "combatant_ref_raider",
                    ["name"] = "Налётчик",
                    ["isGroup"] = false,
                    ["resourceMaterialization"] = Materialization(
                        ("health", 75m),
                        ("poise", 40m))
                },
                new JsonObject
                {
                    ["name"] = "Дозор",
                    ["isGroup"] = true,
                    ["members"] = new JsonArray(
                        new JsonObject
                        {
                            ["memberRef"] = "member_ref_scout",
                            ["name"] = "Разведчик",
                            ["resourceMaterialization"] = Materialization(
                                ("health", 30m),
                                ("poise", 20m))
                        },
                        new JsonObject
                        {
                            ["memberRef"] = "member_ref_archer",
                            ["name"] = "Лучник",
                            ["resourceMaterialization"] = Materialization(
                                ("health", 25m),
                                ("poise", 15m))
                        })
                })
        };
        var factory = new DeterministicCombatantIdentityFactory();

        var result = MortalResourceOwnerComposer.Compose(
            new MortalResourceOwnerCompositionInput(
                ResourceDefinitionCatalog.CreateBuiltIn(),
                Roots(),
                Roots(enemies: enemies)),
            factory);

        Assert.Empty(result.Issues);
        Assert.Equal(1, factory.CombatantCalls);
        Assert.Equal(2, factory.MemberCalls);

        var authority = Assert.IsType<ResourceOwnerAuthority>(result.Authority);
        var combatant = Assert.Single(
            authority.Entries.Values,
            entry => entry.Key.OwnerKind == ResourceOwnerKind.Combatant);
        Assert.True(combatant.SameTurn);
        Assert.Equal("combatant_ref_raider", combatant.SameTurnRef);
        Assert.Equal("combatant_resource_test_1", combatant.Key.ResourceOwnerId);

        var members = authority.Entries.Values
            .Where(entry => entry.Key.OwnerKind == ResourceOwnerKind.CombatGroupMember)
            .OrderBy(entry => entry.Key.ResourceOwnerId, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(2, members.Length);
        Assert.All(members, member => Assert.True(member.SameTurn));
        Assert.Equal(
            new[] { "member_resource_test_1", "member_resource_test_2" },
            members.Select(member => member.Key.ResourceOwnerId));

        var afterImage = result.OwnerCompanionAfterImages[EffectCarrierCatalog.EnemiesPath];
        var combatants = afterImage["enemiesData"]!.AsArray();
        Assert.Equal("combatant_resource_test_1", combatants[0]!["combatantId"]!.GetValue<string>());
        Assert.Null(combatants[0]!["combatantRef"]);
        var rewrittenMembers = combatants[1]!["members"]!.AsArray();
        Assert.All(rewrittenMembers, member =>
        {
            Assert.NotNull(member!["memberId"]);
            Assert.Null(member["memberRef"]);
        });
        Assert.Null(enemies["enemiesData"]![0]!["combatantId"]);
        Assert.NotNull(enemies["enemiesData"]![0]!["combatantRef"]);
    }

    [Fact]
    public void Compose_SameTurnNpcUsesInitialIdAsItsOnlyTemporaryResourceAuthority()
    {
        var npcCore = new JsonObject
        {
            ["NPCsInScene"] = new JsonArray(),
            ["UpdateNPCs"] = new JsonArray(
                new JsonObject
                {
                    ["NPCId"] = null,
                    ["initialId"] = "npc_ref_field_medic",
                    ["name"] = "Полевой лекарь",
                    ["materialization"] = new JsonObject
                    {
                        ["actorId"] = "npc_ref_field_medic",
                        ["materializationId"] = "actor_materialization_medic"
                    },
                    ["resourceMaterialization"] = Materialization(("health", 60m))
                })
        };

        var result = MortalResourceOwnerComposer.Compose(
            new MortalResourceOwnerCompositionInput(
                ResourceDefinitionCatalog.CreateBuiltIn(),
                Roots(),
                Roots(npcCore: npcCore)));

        Assert.Empty(result.Issues);
        var authority = Assert.IsType<ResourceOwnerAuthority>(result.Authority);
        var npc = Assert.Single(
            authority.Entries.Values,
            entry => entry.Key.OwnerKind == ResourceOwnerKind.Npc);
        Assert.Equal("npc_ref_field_medic", npc.Key.ResourceOwnerId);
        Assert.True(npc.SameTurn);
        Assert.Equal("npc_ref_field_medic", npc.SameTurnRef);

        var byRef = authority.Resolve(new ResourceOwnerRequest(
            "mortal_world",
            ResourceOwnerKind.Npc,
            "health",
            ResourceOwnerId: null,
            OwnerRef: "npc_ref_field_medic"));
        Assert.True(byRef.Success);
        var byId = authority.Resolve(new ResourceOwnerRequest(
            "mortal_world",
            ResourceOwnerKind.Npc,
            "health",
            ResourceOwnerId: "npc_ref_field_medic",
            OwnerRef: null));
        Assert.Contains(byId.Issues, issue =>
            issue.Code == "resource_owner_same_turn_id_forbidden");
    }

    [Fact]
    public void Compose_SameTurnNamedNpcCombatRowReusesNpcOwnerAndConsumesNpcRef()
    {
        var npcCore = new JsonObject
        {
            ["NPCsInScene"] = new JsonArray(),
            ["UpdateNPCs"] = new JsonArray(
                new JsonObject
                {
                    ["NPCId"] = null,
                    ["initialId"] = "npc_ref_field_medic",
                    ["name"] = "Полевой лекарь",
                    ["materialization"] = new JsonObject
                    {
                        ["actorId"] = "npc_ref_field_medic",
                        ["materializationId"] = "actor_materialization_medic"
                    },
                    ["resourceMaterialization"] = Materialization(("health", 60m))
                })
        };
        var enemies = new JsonObject
        {
            ["enemiesData"] = new JsonArray(
                new JsonObject
                {
                    ["npcRef"] = "npc_ref_field_medic",
                    ["name"] = "Полевой лекарь",
                    ["isGroup"] = false
                })
        };

        var result = MortalResourceOwnerComposer.Compose(
            new MortalResourceOwnerCompositionInput(
                ResourceDefinitionCatalog.CreateBuiltIn(),
                Roots(),
                Roots(npcCore, enemies)));

        Assert.Empty(result.Issues);
        var authority = Assert.IsType<ResourceOwnerAuthority>(result.Authority);
        Assert.Single(
            authority.Entries.Values,
            entry => entry.Key.OwnerKind == ResourceOwnerKind.Npc);
        Assert.DoesNotContain(
            authority.Entries.Values,
            entry => entry.Key.OwnerKind == ResourceOwnerKind.Combatant);

        var afterImage = result.OwnerCompanionAfterImages[EffectCarrierCatalog.EnemiesPath];
        var combatant = Assert.Single(afterImage["enemiesData"]!.AsArray())!.AsObject();
        Assert.Equal("npc_ref_field_medic", combatant["NPCId"]!.GetValue<string>());
        Assert.False(combatant.ContainsKey("npcRef"));
    }

    private sealed class DeterministicCombatantIdentityFactory : CombatantIdentityFactory
    {
        internal int CombatantCalls { get; private set; }

        internal int MemberCalls { get; private set; }

        internal override string CreateCombatantId() =>
            $"combatant_resource_test_{++CombatantCalls}";

        internal override string CreateMemberId() =>
            $"member_resource_test_{++MemberCalls}";
    }

    private static JsonObject Materialization(
        params (string Key, decimal Maximum)[] resources) =>
        new()
        {
            ["resources"] = new JsonArray(resources
                .Select(resource => (JsonNode)new JsonObject
                {
                    ["resourceKey"] = resource.Key,
                    ["maximum"] = resource.Maximum
                })
                .ToArray())
        };

    private static MortalResourceOwnerRoots Roots(
        JsonObject? npcCore = null,
        JsonObject? enemies = null,
        JsonObject? allies = null,
        JsonObject? vehicles = null) =>
        new(
            npcCore ?? new JsonObject { ["NPCsInScene"] = new JsonArray() },
            enemies ?? new JsonObject { ["enemiesData"] = new JsonArray() },
            allies ?? new JsonObject { ["alliesData"] = new JsonArray() },
            vehicles ?? new JsonObject { ["vehicles"] = new JsonArray() });
}
