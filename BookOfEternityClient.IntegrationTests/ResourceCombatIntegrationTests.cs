using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ResourceCombatIntegrationTests
{
    [Fact]
    public async Task CombatStateValidation_RejectsLegacyIndividualAndGroupResourceAuthority()
    {
        await using var context = await ResourceMaterializationTestContext.CreateAsync();
        var individual = ValidIndividualCombatant();
        individual.Remove("combatantRef");
        individual["combatantId"] = "combatant_existing_raider";
        individual["currentHealth"] = "50%";
        individual["maxHealth"] = "100%";
        var group = ValidCombatGroup(
            new JsonObject
            {
                ["memberId"] = "member_existing_scout",
                ["name"] = "Разведчик"
            });
        group["healthStates"] = new JsonArray(
            new JsonObject
            {
                ["name"] = "Разведчик",
                ["currentHealth"] = "30%"
            });
        await context.WriteExactJsonAsync(
            EffectCarrierCatalog.EnemiesPath,
            new JsonObject
            {
                ["enemiesData"] = new JsonArray(individual, group)
            }.ToJsonString());

        var issues = await context.Validator.ValidateGameStateAsync(
            GameStateValidationPhase.WorldQuestCombatFactionStateFiles);

        Assert.Contains(issues, issue =>
            issue.Code == "resource_owner_legacy_value_forbidden" &&
            issue.FilePath.EndsWith(".currentHealth", StringComparison.Ordinal));
        Assert.Contains(issues, issue =>
            issue.Code == "resource_owner_legacy_value_forbidden" &&
            issue.FilePath.EndsWith(".healthStates", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CombatStateValidation_RequiresEnvelopeOnlyForNewAnonymousOwners()
    {
        await using var context = await ResourceMaterializationTestContext.CreateAsync();
        var missingEnvelope = ValidIndividualCombatant("combatant_ref_missing_envelope");
        var existingWithEnvelope = ValidIndividualCombatant();
        existingWithEnvelope.Remove("combatantRef");
        existingWithEnvelope["combatantId"] = "combatant_existing_envelope";
        existingWithEnvelope["resourceMaterialization"] = Materialization(("health", 75m));
        await context.WriteExactJsonAsync(
            EffectCarrierCatalog.EnemiesPath,
            new JsonObject
            {
                ["enemiesData"] = new JsonArray(missingEnvelope, existingWithEnvelope)
            }.ToJsonString());

        var issues = await context.Validator.ValidateGameStateAsync(
            GameStateValidationPhase.WorldQuestCombatFactionStateFiles);

        Assert.Contains(issues, issue =>
            issue.Code == "resource_owner_materialization_required" &&
            issue.FilePath.EndsWith(".resourceMaterialization", StringComparison.Ordinal));
        Assert.Contains(issues, issue =>
            issue.Code == "resource_owner_materialization_existing_forbidden" &&
            issue.FilePath.EndsWith(".resourceMaterialization", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CombatStateValidation_RejectsAmbiguousNamedNpcSelectors()
    {
        await using var context = await ResourceMaterializationTestContext.CreateAsync();
        var combatant = ValidIndividualCombatant();
        combatant.Remove("combatantRef");
        combatant["NPCId"] = "npc_guard_captain";
        combatant["npcRef"] = "npc_ref_guard_captain";
        await context.WriteExactJsonAsync(
            EffectCarrierCatalog.EnemiesPath,
            new JsonObject
            {
                ["enemiesData"] = new JsonArray(combatant)
            }.ToJsonString());

        var issues = await context.Validator.ValidateGameStateAsync(
            GameStateValidationPhase.WorldQuestCombatFactionStateFiles);

        Assert.Contains(issues, issue =>
            issue.Code == "resource_owner_identity_selector_invalid" &&
            issue.FilePath.EndsWith(".enemiesData[0]", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AcceptedTurn_NewCombatantSharesIdentityAndPublishesHealthAndPoiseOnce()
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
            EffectCarrierCatalog.EnemiesPath,
            new JsonObject { ["enemiesData"] = new JsonArray() }.ToJsonString());
        await context.CaptureValidatedPendingSnapshotAsync(turn: 42);
        await context.WriteExactJsonAsync(
            EffectCarrierCatalog.EnemiesPath,
            new JsonObject
            {
                ["enemiesData"] = new JsonArray(
                    new JsonObject
                    {
                        ["combatantRef"] = "combatant_ref_raider",
                        ["NPCId"] = null,
                        ["name"] = "Налётчик",
                        ["image_prompt"] = "dark fantasy roadside raider",
                        ["description"] = "Одинокий налётчик.",
                        ["type"] = "humanoid",
                        ["isGroup"] = false,
                        ["initiative"] = 17,
                        ["actions"] = new JsonArray(),
                        ["resistances"] = new JsonArray(),
                        ["activeBuffs"] = new JsonArray(),
                        ["activeDebuffs"] = new JsonArray(),
                        ["resourceMaterialization"] = Materialization(
                            ("health", 75m),
                            ("poise", 40m))
                    })
            }.ToJsonString());

        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();

        Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);
        Assert.True(AcceptedMechanicsPlanAuthority.TryPeekValidated(
            context.FileSystem,
            out _,
            out var planning));
        var plan = Assert.IsType<AcceptedMechanicsPlan>(planning.Plan);
        var owner = Assert.Single(
            plan.OwnerAuthority.Entries.Values,
            entry => entry.Key.OwnerKind == ResourceOwnerKind.Combatant);
        var plannedState = ResourceStateContract.ParseCanonical(
            plan.StateAfterImage.ToJsonString(),
            bootstrap.Definitions,
            allowMissingPristine: false);
        Assert.True(plannedState.IsValid, string.Join(Environment.NewLine, plannedState.Issues));
        Assert.Equal(
            new[] { ("health", 75m), ("poise", 40m) },
            plannedState.Ledger!.Entries
                .OrderBy(static entry => entry.Coordinate.ResourceKey, StringComparer.Ordinal)
                .Select(entry => (entry.Coordinate.ResourceKey, entry.Maximum)));
        Assert.All(plannedState.Ledger.Entries, entry =>
        {
            Assert.Equal(owner.Key.ResourceOwnerId, entry.Coordinate.ResourceOwnerId);
            Assert.Equal(entry.Maximum, entry.Current);
        });

        await using var writeLease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var published = await context.Normalizer.BindTo(writeLease)
            .NormalizeAcceptedMechanicsAsync(backups: null);

        Assert.Same(plan, published);
        var combatRoot = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            EffectCarrierCatalog.EnemiesPath));
        var combatant = Assert.IsType<JsonObject>(
            Assert.Single(combatRoot["enemiesData"]!.AsArray()));
        Assert.Equal(owner.Key.ResourceOwnerId, combatant["combatantId"]!.GetValue<string>());
        Assert.False(combatant.ContainsKey("combatantRef"));
        Assert.False(combatant.ContainsKey("resourceMaterialization"));
    }

    [Fact]
    public async Task AcceptedTurn_SameTurnNamedNpcCombatRowReusesOneNpcHealthOwner()
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
        await context.WriteExactJsonAsync(
            EffectCarrierCatalog.EnemiesPath,
            new JsonObject { ["enemiesData"] = new JsonArray() }.ToJsonString());
        await context.CaptureValidatedPendingSnapshotAsync(turn: 42);

        const string npcRef = "npc_ref_field_medic_combat";
        var npc = EffectMaterializationTestFixture.CreateSameTurnMortalActor(npcRef);
        npc["resourceMaterialization"] = Materialization(("health", 60m));
        await context.WriteExactJsonAsync(
            "game_state/npcs/npc_core.json",
            new JsonObject { ["UpdateNPCs"] = new JsonArray(npc) }.ToJsonString());
        var combatant = ValidIndividualCombatant();
        combatant.Remove("combatantRef");
        combatant["npcRef"] = npcRef;
        await context.WriteExactJsonAsync(
            EffectCarrierCatalog.EnemiesPath,
            new JsonObject { ["enemiesData"] = new JsonArray(combatant) }.ToJsonString());

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
        Assert.DoesNotContain(
            plan.OwnerAuthority.Entries.Values,
            entry => entry.Key.OwnerKind == ResourceOwnerKind.Combatant);
        var state = ResourceStateContract.ParseCanonical(
            plan.StateAfterImage.ToJsonString(),
            bootstrap.Definitions,
            allowMissingPristine: false);
        Assert.True(state.IsValid, string.Join(Environment.NewLine, state.Issues));
        var health = Assert.Single(state.Ledger!.Entries);
        Assert.Equal(ResourceOwnerKind.Npc, health.Coordinate.OwnerKind);
        Assert.Equal(npcRef, health.Coordinate.ResourceOwnerId);
        Assert.Equal("health", health.Coordinate.ResourceKey);
        Assert.Equal(60m, health.Current);
        Assert.Equal(60m, health.Maximum);

        await using (var writeLease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            var published = await context.Normalizer.BindTo(writeLease)
                .NormalizeAcceptedMechanicsAsync(backups: null);
            Assert.Same(plan, published);
        }

        var canonicalNpcRoot = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            "game_state/npcs/npc_core.json"));
        var canonicalNpc = Assert.IsType<JsonObject>(
            Assert.Single(canonicalNpcRoot["UpdateNPCs"]!.AsArray()));
        Assert.Equal(npcRef, canonicalNpc["NPCId"]!.GetValue<string>());
        Assert.False(canonicalNpc.ContainsKey("initialId"));
        Assert.False(canonicalNpc.ContainsKey("resourceMaterialization"));
        var canonicalCombatRoot = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            EffectCarrierCatalog.EnemiesPath));
        var canonicalCombatant = Assert.IsType<JsonObject>(
            Assert.Single(canonicalCombatRoot["enemiesData"]!.AsArray()));
        Assert.Equal(npcRef, canonicalCombatant["NPCId"]!.GetValue<string>());
        Assert.False(canonicalCombatant.ContainsKey("npcRef"));
        Assert.False(canonicalCombatant.ContainsKey("combatantId"));
        Assert.False(canonicalCombatant.ContainsKey("resourceMaterialization"));
    }

    [Fact]
    public async Task AcceptedTurns_GroupMemberRemovalRetiresOnlyThatStableOwner()
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
            EffectCarrierCatalog.EnemiesPath,
            new JsonObject { ["enemiesData"] = new JsonArray() }.ToJsonString());
        await context.CaptureValidatedPendingSnapshotAsync(turn: 42);
        await context.WriteExactJsonAsync(
            EffectCarrierCatalog.EnemiesPath,
            new JsonObject
            {
                ["enemiesData"] = new JsonArray(
                    ValidCombatGroup(
                        NewMember("member_ref_scout", "Разведчик", 30m, 20m),
                        NewMember("member_ref_archer", "Лучник", 25m, 15m)))
            }.ToJsonString());

        var firstIssues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();
        Assert.DoesNotContain(firstIssues, issue => issue.Severity == IssueSeverity.Error);
        await using (var firstLease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            Assert.NotNull(await context.Normalizer.BindTo(firstLease)
                .NormalizeAcceptedMechanicsAsync(backups: null));
        }

        var firstCombatRoot = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            EffectCarrierCatalog.EnemiesPath));
        var firstGroup = Assert.IsType<JsonObject>(
            Assert.Single(firstCombatRoot["enemiesData"]!.AsArray()));
        var firstMembers = firstGroup["members"]!.AsArray()
            .OfType<JsonObject>()
            .ToArray();
        Assert.Equal(2, firstMembers.Length);
        var survivingMember = firstMembers[0].DeepClone().AsObject();
        var survivingId = survivingMember["memberId"]!.GetValue<string>();
        var removedId = firstMembers[1]["memberId"]!.GetValue<string>();

        await context.CaptureValidatedPendingSnapshotAsync(turn: 43);
        var secondCombatRoot = firstCombatRoot.DeepClone().AsObject();
        var secondGroup = Assert.IsType<JsonObject>(
            Assert.Single(secondCombatRoot["enemiesData"]!.AsArray()));
        secondGroup["count"] = 1;
        secondGroup["members"] = new JsonArray(survivingMember);
        await context.WriteExactJsonAsync(
            EffectCarrierCatalog.EnemiesPath,
            secondCombatRoot.ToJsonString());

        var secondIssues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();

        Assert.DoesNotContain(secondIssues, issue => issue.Severity == IssueSeverity.Error);
        Assert.True(AcceptedMechanicsPlanAuthority.TryPeekValidated(
            context.FileSystem,
            out _,
            out var secondPlanning));
        var secondPlan = Assert.IsType<AcceptedMechanicsPlan>(secondPlanning.Plan);

        await using (var secondLease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            var published = await context.Normalizer.BindTo(secondLease)
                .NormalizeAcceptedMechanicsAsync(backups: null);
            Assert.Same(secondPlan, published);
        }

        var state = ResourceStateContract.ParseCanonical(
            await context.FileSystem.ReadFileAsync(ResourceMaterializationTestContext.StatePath),
            bootstrap.Definitions,
            allowMissingPristine: false);
        Assert.True(state.IsValid, string.Join(Environment.NewLine, state.Issues));
        Assert.DoesNotContain(state.Ledger!.Entries, entry =>
            entry.Coordinate.ResourceOwnerId == removedId);
        Assert.Equal(2, state.Ledger.Entries.Count(entry =>
            entry.Coordinate.ResourceOwnerId == survivingId));
        var history = ResourceHistoryState.ParseCanonical(
            await context.FileSystem.ReadFileAsync(ResourceMaterializationTestContext.HistoryPath),
            bootstrap.Definitions,
            allowMissingPristine: false);
        Assert.True(history.IsValid, string.Join(Environment.NewLine, history.Issues));
        Assert.Equal(2, history.History!.Transitions.Count(transition =>
            transition.Coordinate.ResourceOwnerId == removedId &&
            transition.Operation == ResourceTransitionOperation.Retire &&
            transition.AfterState == null));
        var canonicalCombatRoot = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            EffectCarrierCatalog.EnemiesPath));
        var canonicalGroup = Assert.IsType<JsonObject>(
            Assert.Single(canonicalCombatRoot["enemiesData"]!.AsArray()));
        var canonicalMember = Assert.IsType<JsonObject>(
            Assert.Single(canonicalGroup["members"]!.AsArray()));
        Assert.Equal(survivingId, canonicalMember["memberId"]!.GetValue<string>());
    }

    [Fact]
    public async Task AcceptedTurns_GroupMemberDetachesAndRejoinsWithoutChangingResourceOwner()
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
            EffectCarrierCatalog.EnemiesPath,
            new JsonObject { ["enemiesData"] = new JsonArray() }.ToJsonString());

        await context.CaptureValidatedPendingSnapshotAsync(turn: 42);
        await context.WriteExactJsonAsync(
            EffectCarrierCatalog.EnemiesPath,
            new JsonObject
            {
                ["enemiesData"] = new JsonArray(
                    ValidCombatGroup(
                        NewMember("member_ref_scout", "Разведчик", 30m, 20m),
                        NewMember("member_ref_archer", "Лучник", 25m, 15m)))
            }.ToJsonString());
        var createIssues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();
        Assert.DoesNotContain(createIssues, issue => issue.Severity == IssueSeverity.Error);
        await using (var createLease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            Assert.NotNull(await context.Normalizer.BindTo(createLease)
                .NormalizeAcceptedMechanicsAsync(backups: null));
        }

        var createdRoot = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            EffectCarrierCatalog.EnemiesPath));
        var createdGroup = Assert.IsType<JsonObject>(
            Assert.Single(createdRoot["enemiesData"]!.AsArray()));
        var createdMembers = createdGroup["members"]!.AsArray()
            .Select(node => Assert.IsType<JsonObject>(node))
            .ToArray();
        var detachedMember = createdMembers[0].DeepClone().AsObject();
        var retainedMember = createdMembers[1].DeepClone().AsObject();
        var memberId = detachedMember["memberId"]!.GetValue<string>();
        var historyBeforeMove = ResourceHistoryState.ParseCanonical(
            await context.FileSystem.ReadFileAsync(ResourceMaterializationTestContext.HistoryPath),
            bootstrap.Definitions,
            allowMissingPristine: false).History!;
        var memberTransitionCount = historyBeforeMove.Transitions.Count(transition =>
            transition.Coordinate.ResourceOwnerId == memberId);

        await context.CaptureValidatedPendingSnapshotAsync(turn: 43);
        var splitGroup = createdGroup.DeepClone().AsObject();
        splitGroup["count"] = 1;
        splitGroup["members"] = new JsonArray(retainedMember.DeepClone());
        var detachedRow = ValidIndividualCombatant();
        detachedRow.Remove("combatantRef");
        detachedRow["memberId"] = memberId;
        detachedRow["name"] = "Отделившийся разведчик";
        await context.WriteExactJsonAsync(
            EffectCarrierCatalog.EnemiesPath,
            new JsonObject
            {
                ["enemiesData"] = new JsonArray(splitGroup, detachedRow)
            }.ToJsonString());

        var splitIssues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();
        Assert.DoesNotContain(splitIssues, issue => issue.Severity == IssueSeverity.Error);
        await using (var splitLease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            Assert.NotNull(await context.Normalizer.BindTo(splitLease)
                .NormalizeAcceptedMechanicsAsync(backups: null));
        }

        await AssertStableMemberStateAsync(memberId, memberTransitionCount);
        var splitRoot = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            EffectCarrierCatalog.EnemiesPath));
        var splitRows = splitRoot["enemiesData"]!.AsArray()
            .Select(node => Assert.IsType<JsonObject>(node))
            .ToArray();
        var canonicalDetached = Assert.Single(splitRows, row =>
            row["memberId"]?.GetValue<string>() == memberId);
        Assert.False(canonicalDetached.ContainsKey("combatantId"));
        Assert.False(canonicalDetached.ContainsKey("combatantRef"));
        Assert.False(canonicalDetached.ContainsKey("memberRef"));
        Assert.False(canonicalDetached.ContainsKey("resourceMaterialization"));

        await context.CaptureValidatedPendingSnapshotAsync(turn: 44);
        var rejoinedGroup = splitRows.Single(row =>
            row["isGroup"]?.GetValue<bool>() == true).DeepClone().AsObject();
        rejoinedGroup["count"] = 2;
        rejoinedGroup["members"] = new JsonArray(
            retainedMember.DeepClone(),
            detachedMember.DeepClone());
        await context.WriteExactJsonAsync(
            EffectCarrierCatalog.EnemiesPath,
            new JsonObject
            {
                ["enemiesData"] = new JsonArray(rejoinedGroup)
            }.ToJsonString());

        var rejoinIssues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();
        Assert.DoesNotContain(rejoinIssues, issue => issue.Severity == IssueSeverity.Error);
        await using (var rejoinLease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            Assert.NotNull(await context.Normalizer.BindTo(rejoinLease)
                .NormalizeAcceptedMechanicsAsync(backups: null));
        }

        await AssertStableMemberStateAsync(memberId, memberTransitionCount);
        var rejoinedRoot = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            EffectCarrierCatalog.EnemiesPath));
        var canonicalGroup = Assert.IsType<JsonObject>(
            Assert.Single(rejoinedRoot["enemiesData"]!.AsArray()));
        Assert.Contains(canonicalGroup["members"]!.AsArray().OfType<JsonObject>(), member =>
            member["memberId"]?.GetValue<string>() == memberId);

        async Task AssertStableMemberStateAsync(
            string expectedMemberId,
            int expectedTransitionCount)
        {
            var state = ResourceStateContract.ParseCanonical(
                await context.FileSystem.ReadFileAsync(ResourceMaterializationTestContext.StatePath),
                bootstrap.Definitions,
                allowMissingPristine: false);
            Assert.True(state.IsValid, string.Join(Environment.NewLine, state.Issues));
            var entries = state.Ledger!.Entries
                .Where(entry => entry.Coordinate.ResourceOwnerId == expectedMemberId)
                .OrderBy(entry => entry.Coordinate.ResourceKey, StringComparer.Ordinal)
                .ToArray();
            Assert.Equal(new[] { "health", "poise" }, entries.Select(entry =>
                entry.Coordinate.ResourceKey));
            Assert.All(entries, entry => Assert.Equal(ResourceLifecycleState.Active, entry.Snapshot.State));

            var history = ResourceHistoryState.ParseCanonical(
                await context.FileSystem.ReadFileAsync(ResourceMaterializationTestContext.HistoryPath),
                bootstrap.Definitions,
                allowMissingPristine: false);
            Assert.True(history.IsValid, string.Join(Environment.NewLine, history.Issues));
            Assert.Equal(expectedTransitionCount, history.History!.Transitions.Count(transition =>
                transition.Coordinate.ResourceOwnerId == expectedMemberId));
            Assert.DoesNotContain(history.History.Transitions, transition =>
                transition.Coordinate.ResourceOwnerId == expectedMemberId &&
                transition.Operation == ResourceTransitionOperation.Retire);
        }
    }

    [Theory]
    [InlineData("combatantId")]
    [InlineData("combatantRef")]
    [InlineData("memberRef")]
    [InlineData("npcRef")]
    [InlineData("NPCId")]
    public async Task CombatStateValidation_RejectsDetachedMemberWithConflictingIdentityFamily(
        string conflictingField)
    {
        await using var context = await ResourceMaterializationTestContext.CreateAsync();
        var detached = ValidIndividualCombatant();
        detached.Remove("combatantRef");
        detached["memberId"] = "member_detached_exact";
        detached[conflictingField] = conflictingField.EndsWith("Ref", StringComparison.Ordinal)
            ? "conflicting_ref"
            : "conflicting_id";
        await context.WriteExactJsonAsync(
            EffectCarrierCatalog.EnemiesPath,
            new JsonObject { ["enemiesData"] = new JsonArray(detached) }.ToJsonString());

        var issues = await context.Validator.ValidateGameStateAsync(
            GameStateValidationPhase.WorldQuestCombatFactionStateFiles);

        Assert.Contains(issues, issue =>
            issue.Code == "resource_owner_identity_selector_invalid" &&
            issue.FilePath.EndsWith(".enemiesData[0]", StringComparison.Ordinal));
    }

    [Fact]
    public void NewIndividualCombatant_MaterializesHealthAndPoiseWithoutLegacyValues()
    {
        var acceptedEnemies = new JsonObject
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
                })
        };
        var result = MortalResourceOwnerComposer.Compose(
            new MortalResourceOwnerCompositionInput(
                ResourceDefinitionCatalog.CreateBuiltIn(),
                EmptyRoots(),
                Roots(acceptedEnemies)),
            new FixedCombatantIdentityFactory());

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Issues));
        Assert.Collection(
            result.CapacityDrafts.OrderBy(value => value.Coordinate.ResourceKey),
            health =>
            {
                Assert.Equal("health", health.Coordinate.ResourceKey);
                Assert.Equal(75m, health.AcceptedMaximum);
            },
            poise =>
            {
                Assert.Equal("poise", poise.Coordinate.ResourceKey);
                Assert.Equal(40m, poise.AcceptedMaximum);
            });
        Assert.All(result.CapacityDrafts, draft =>
        {
            Assert.Equal(ResourceOwnerKind.Combatant, draft.Coordinate.OwnerKind);
            Assert.Equal("combatant_cutover_001", draft.Coordinate.ResourceOwnerId);
            Assert.True(draft.ResolvedCapacity.IsValid);
        });

        var afterImage = result.OwnerCompanionAfterImages[EffectCarrierCatalog.EnemiesPath];
        var combatant = Assert.IsType<JsonObject>(
            Assert.Single(afterImage["enemiesData"]!.AsArray()));
        Assert.Equal("combatant_cutover_001", combatant["combatantId"]!.GetValue<string>());
        Assert.False(combatant.ContainsKey("combatantRef"));
        Assert.False(combatant.ContainsKey("resourceMaterialization"));
        Assert.DoesNotContain(
            new[] { "currentHealth", "maxHealth", "currentPoise", "maxPoise" },
            combatant.ContainsKey);
    }

    [Fact]
    public void GroupMembers_ReorderByStableIdAndRemovedMemberBecomesTerminal()
    {
        var preTurn = Group(
            Member("member_alpha", "Разведчик"),
            Member("member_beta", "Лучник"));
        var accepted = Group(Member("member_alpha", "Разведчик"));
        var result = MortalResourceOwnerComposer.Compose(
            new MortalResourceOwnerCompositionInput(
                ResourceDefinitionCatalog.CreateBuiltIn(),
                Roots(preTurn),
                Roots(accepted)));

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Issues));
        Assert.Empty(result.CapacityDrafts);
        var terminal = Assert.Single(result.TerminalOwners);
        Assert.Equal(ResourceOwnerKind.CombatGroupMember, terminal.OwnerKind);
        Assert.Equal("member_beta", terminal.ResourceOwnerId);
        var active = Assert.Single(
            result.Authority!.Entries.Values,
            value => value.Key.OwnerKind == ResourceOwnerKind.CombatGroupMember);
        Assert.Equal("member_alpha", active.Key.ResourceOwnerId);
    }

    [Fact]
    public void NewGroupMembers_MaterializeByMemberRefAndNeverUsePositionalHealthArrays()
    {
        var accepted = new JsonObject
        {
            ["enemiesData"] = new JsonArray(
                new JsonObject
                {
                    ["name"] = "Дозор",
                    ["isGroup"] = true,
                    ["members"] = new JsonArray(
                        NewMember("member_ref_scout", "Разведчик", 30m, 20m),
                        NewMember("member_ref_archer", "Лучник", 25m, 15m))
                })
        };
        var result = MortalResourceOwnerComposer.Compose(
            new MortalResourceOwnerCompositionInput(
                ResourceDefinitionCatalog.CreateBuiltIn(),
                EmptyRoots(),
                Roots(accepted)),
            new FixedCombatantIdentityFactory());

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Issues));
        Assert.Equal(4, result.CapacityDrafts.Count);
        Assert.All(result.CapacityDrafts, draft =>
            Assert.Equal(ResourceOwnerKind.CombatGroupMember, draft.Coordinate.OwnerKind));
        var afterImage = result.OwnerCompanionAfterImages[EffectCarrierCatalog.EnemiesPath];
        var group = Assert.IsType<JsonObject>(Assert.Single(afterImage["enemiesData"]!.AsArray()));
        Assert.False(group.ContainsKey("healthStates"));
        Assert.False(group.ContainsKey("currentHealth"));
        Assert.False(group.ContainsKey("maxHealth"));
        foreach (var member in group["members"]!.AsArray().OfType<JsonObject>())
        {
            Assert.True(member.ContainsKey("memberId"));
            Assert.False(member.ContainsKey("memberRef"));
            Assert.False(member.ContainsKey("resourceMaterialization"));
        }
    }

    [Fact]
    public void CombatAndNpcLegacyHealthOrPoiseAuthorityIsRejected()
    {
        var combatant = new JsonObject
        {
            ["combatantId"] = "combatant_existing",
            ["name"] = "Налётчик",
            ["isGroup"] = false,
            ["maxHealth"] = "100%",
            ["currentHealth"] = "50%",
            ["maxPoise"] = "100%",
            ["currentPoise"] = "25%"
        };
        var npc = new JsonObject
        {
            ["NPCId"] = "npc_existing",
            ["name"] = "Страж",
            ["maxHealthPercentage"] = "100%",
            ["currentHealthPercentage"] = "75%"
        };
        var roots = new MortalResourceOwnerRoots(
            new JsonObject { ["NPCsInScene"] = new JsonArray(npc) },
            new JsonObject { ["enemiesData"] = new JsonArray(combatant) },
            new JsonObject { ["alliesData"] = new JsonArray() },
            new JsonObject { ["vehicles"] = new JsonArray() });

        var result = MortalResourceOwnerComposer.Compose(
            new MortalResourceOwnerCompositionInput(
                ResourceDefinitionCatalog.CreateBuiltIn(),
                roots,
                roots));

        Assert.False(result.IsValid);
        Assert.True(result.Issues.Count(issue =>
            issue.Code == "resource_owner_legacy_value_forbidden") >= 2);
    }

    private static JsonObject Materialization(params (string Key, decimal Maximum)[] values) =>
        new()
        {
            ["resources"] = new JsonArray(values
                .Select(value => (JsonNode)new JsonObject
                {
                    ["resourceKey"] = value.Key,
                    ["maximum"] = value.Maximum
                })
                .ToArray())
        };

    private static JsonObject ValidIndividualCombatant(
        string combatantRef = "combatant_ref_raider")
    {
        var combatant = EffectMaterializationTestFixture.CreateSameTurnCombatant(combatantRef);
        foreach (var field in new[] { "currentHealth", "maxHealth", "currentPoise", "maxPoise" })
            combatant.Remove(field);
        return combatant;
    }

    private static JsonObject ValidCombatGroup(params JsonObject[] members) =>
        new()
        {
            ["NPCId"] = null,
            ["name"] = "Дозор",
            ["image_prompt"] = "dark fantasy road watch",
            ["description"] = "Малый дорожный дозор.",
            ["type"] = "group",
            ["isGroup"] = true,
            ["initiative"] = 12,
            ["actions"] = new JsonArray(),
            ["resistances"] = new JsonArray(),
            ["activeBuffs"] = new JsonArray(),
            ["activeDebuffs"] = new JsonArray(),
            ["count"] = members.Length,
            ["unitName"] = "дозорный",
            ["members"] = new JsonArray(members.Select(static member => (JsonNode)member).ToArray())
        };

    private static JsonObject NewMember(
        string memberRef,
        string name,
        decimal health,
        decimal poise) =>
        new()
        {
            ["memberRef"] = memberRef,
            ["name"] = name,
            ["resourceMaterialization"] = Materialization(
                ("health", health),
                ("poise", poise))
        };

    private static JsonObject Member(string memberId, string name) =>
        new()
        {
            ["memberId"] = memberId,
            ["name"] = name
        };

    private static JsonObject Group(params JsonObject[] members) =>
        new()
        {
            ["enemiesData"] = new JsonArray(
                new JsonObject
                {
                    ["name"] = "Дозор",
                    ["isGroup"] = true,
                    ["members"] = new JsonArray(members
                        .Select(static member => (JsonNode)member)
                        .ToArray())
                })
        };

    private static MortalResourceOwnerRoots EmptyRoots() =>
        Roots(new JsonObject { ["enemiesData"] = new JsonArray() });

    private static MortalResourceOwnerRoots Roots(JsonObject enemies) =>
        new(
            new JsonObject { ["NPCsInScene"] = new JsonArray() },
            enemies,
            new JsonObject { ["alliesData"] = new JsonArray() },
            new JsonObject { ["vehicles"] = new JsonArray() });

    private sealed class FixedCombatantIdentityFactory : CombatantIdentityFactory
    {
        private int _combatants;
        private int _members;

        internal override string CreateCombatantId() =>
            $"combatant_cutover_{++_combatants:000}";

        internal override string CreateMemberId() =>
            $"member_cutover_{++_members:000}";
    }
}
