using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class EffectResourceMaterializationTests
{
    private const string NpcCorePath = "game_state/npcs/npc_core.json";
    private const string SnapshotManifestPath =
        "game_state/control/pending_turn_snapshot.json";

    /// <summary>
    /// Publishes skill-sourced periodic damage, resource history and effect lifetime together.
    /// </summary>
    /// <returns>
    /// A task completing after resource damage, its history transition and the reduced lifetime are confirmed.
    /// </returns>
    [Fact]
    public async Task PlayerTurnEndPeriodicDamage_PublishesResourceHistoryAndLifetimeTogether()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        var resources = ResourceBootstrapStateBuilder.BuildMortalPlayer(
            incarnationNumber: 1,
            turn: 42,
            permanentStrength: 10,
            permanentConstitution: 10,
            permanentIntelligence: 10,
            permanentWisdom: 10,
            permanentFaith: 10);
        Assert.True(resources.IsValid, string.Join(Environment.NewLine, resources.Issues));
        await context.WriteJsonAsync(
            ResourceMaterializationContract.DefinitionsPath,
            JsonNode.Parse(resources.Definitions!.ToCanonicalJson())!);
        await context.WriteJsonAsync(
            ResourceMaterializationContract.StatePath,
            JsonNode.Parse(resources.State!.ToCanonicalJson())!);
        await context.WriteJsonAsync(
            ResourceMaterializationContract.HistoryPath,
            JsonNode.Parse(resources.History!.ToCanonicalJson())!);

        var definition = EffectMaterializationTestFixture.CreateDefinition("periodic_damage");
        var effect = EffectMaterializationTestContext.CreateSkillCanonicalEffect(
            ownerKind: "player",
            profile: "periodic_damage");
        effect["lifetime"]!["remainingTurns"] = 2;
        await context.SeedPlayerSkillSourceAsync(definition);
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.PlayerEffectsPath,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["activeEffects"] = new JsonArray(effect.DeepClone())
            });
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.IdentityIndexPath,
            EffectMaterializationTestFixture.CreateIdentityIndex(effect));
        await context.CaptureValidatedPendingSnapshotAsync(turn: 43);
        var backups = await context.ReadPendingSnapshotBackupsAsync();

        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();

        Assert.True(
            issues.All(issue => issue.Severity != IssueSeverity.Error),
            string.Join(
                Environment.NewLine,
                issues.Select(issue =>
                    $"{issue.Code}: expected={issue.Expected}; actual={issue.Actual}")));
        await using var writeLease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var plan = await context.Normalizer.BindTo(writeLease)
            .NormalizeAcceptedMechanicsAsync(backups);

        Assert.NotNull(plan);
        var definitions = ResourceDefinitionCatalog.ParseCanonical(
            (await context.ReadJsonAsync(ResourceMaterializationContract.DefinitionsPath))!
                .ToJsonString(),
            allowMissingPristine: false);
        Assert.NotNull(definitions.Catalog);
        Assert.Empty(definitions.Issues);
        var state = ResourceStateContract.ParseCanonical(
            (await context.ReadJsonAsync(ResourceMaterializationContract.StatePath))!
                .ToJsonString(),
            definitions.Catalog!,
            allowMissingPristine: false);
        Assert.NotNull(state.Ledger);
        Assert.Empty(state.Issues);
        var health = Assert.Single(
            state.Ledger!.Entries,
            entry => entry.Coordinate.ResourceKey == "health");
        Assert.Equal(health.Maximum - 3m, health.Current);

        var history = ResourceHistoryState.ParseCanonical(
            (await context.ReadJsonAsync(ResourceMaterializationContract.HistoryPath))!
                .ToJsonString(),
            definitions.Catalog!,
            allowMissingPristine: false);
        Assert.NotNull(history.History);
        Assert.Empty(history.Issues);
        var periodicTransition = Assert.Single(
            history.History!.Transitions,
            transition => transition.Phase == ResourceMutationPhase.EffectTrigger);
        Assert.Equal("effect_component", periodicTransition.OriginKind);
        Assert.Equal(3m, periodicTransition.AppliedAmount);

        var playerEffects = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.PlayerEffectsPath))!.AsObject();
        var remainingEffect = Assert.IsType<JsonObject>(
            Assert.Single(playerEffects["activeEffects"]!.AsArray()));
        Assert.Equal(1, remainingEffect["lifetime"]!["remainingTurns"]!.GetValue<int>());
    }

    /// <summary>
    /// Keeps nested skill-effect planning bound to the already validated original snapshot.
    /// </summary>
    /// <returns>
    /// A task completing after the nested plan fingerprint and original snapshot binding are confirmed.
    /// </returns>
    [Fact]
    public async Task ResourceValidation_NestedEffectPlanUsesTheAlreadyValidatedSnapshot()
    {
        FileSystemManager? fileSystem = null;
        byte[]? replacementManifestBytes = null;
        byte[]? replacementAuthorityBytes = null;
        var armed = false;
        var effectCommandReads = 0;
        var swapped = false;
        var hooks = new FileSystemManagerHooks
        {
            BeforeCanonicalReadOpenAsync = path =>
            {
                if (!armed ||
                    !string.Equals(
                        path,
                        EffectAcceptedTurnPlan.CommandPath,
                        StringComparison.OrdinalIgnoreCase) ||
                    Interlocked.Increment(ref effectCommandReads) != 2)
                {
                    return Task.CompletedTask;
                }

                File.WriteAllBytes(
                    fileSystem!.ResolvePath(SnapshotManifestPath),
                    replacementManifestBytes!);
                File.WriteAllBytes(
                    fileSystem.ResolvePath(PendingTurnSnapshotAuthority.AuthorityPath),
                    replacementAuthorityBytes!);
                swapped = true;
                return Task.CompletedTask;
            }
        };
        await using var context = await EffectMaterializationTestContext.CreateAsync(hooks);
        fileSystem = context.FileSystem;
        await context.SeedPlayerSkillSourceAsync();
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.PlayerEffectsPath,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["activeEffects"] = new JsonArray()
            });
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.IdentityIndexPath,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["entries"] = new JsonArray()
            });
        await SeedCurrentResourceOwnerAuthorityAsync(context);
        await context.CaptureValidatedPendingSnapshotAsync(turn: 42);
        await context.WriteJsonAsync(
            EffectAcceptedTurnPlan.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(
                EffectMaterializationTestContext.CreateSkillApplyCommand()));

        var baselineIssues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();
        Assert.DoesNotContain(baselineIssues, issue => issue.Severity == IssueSeverity.Error);
        var baselineHandoff = await AcceptedMechanicsAuthorityTestProbe.PeekCommonAsync(
            fileSystem);
        Assert.NotNull(baselineHandoff);
        var baselineResult = baselineHandoff!.Result;
        var baselineCommonPlan = Assert.IsType<AcceptedMechanicsPlan>(baselineResult.Plan);
        var baselinePlan = Assert.IsType<EffectAcceptedTurnPlan>(
            baselineCommonPlan.EffectPlan);

        var manifestA = Assert.IsType<JsonObject>(
            await context.ReadJsonAsync(SnapshotManifestPath));
        var manifestABytes = (await fileSystem.ReadFileBytesAsync(SnapshotManifestPath))!;
        var authorityABytes = (await fileSystem.ReadFileBytesAsync(
            PendingTurnSnapshotAuthority.AuthorityPath))!;
        var manifestB = manifestA.DeepClone().AsObject();
        manifestB["sourceLabel"] = "Nested effect swapped snapshot B";
        manifestB["manifestPayloadHash"] = string.Empty;
        manifestB["manifestPayloadHash"] =
            PendingTurnSnapshotTestAuthority.ComputeManifestPayloadHash(manifestB);
        await context.WriteJsonAsync(SnapshotManifestPath, manifestB);
        await PendingTurnSnapshotTestAuthority.SyncAuthorityForCurrentManifestAsync(fileSystem);
        replacementManifestBytes = (await fileSystem.ReadFileBytesAsync(SnapshotManifestPath))!;
        replacementAuthorityBytes = (await fileSystem.ReadFileBytesAsync(
            PendingTurnSnapshotAuthority.AuthorityPath))!;
        await fileSystem.WriteFileAtomicBytesAsync(SnapshotManifestPath, manifestABytes);
        await fileSystem.WriteFileAtomicBytesAsync(
            PendingTurnSnapshotAuthority.AuthorityPath,
            authorityABytes);
        armed = true;

        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();

        Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);
        Assert.True(swapped);
        var commonHandoff = await AcceptedMechanicsAuthorityTestProbe.PeekCommonAsync(
            fileSystem);
        Assert.NotNull(commonHandoff);
        var commonBinding = commonHandoff!.Binding;
        var commonResult = commonHandoff.Result;
        Assert.Equal(
            manifestA["manifestPayloadHash"]!.GetValue<string>(),
            commonBinding.SnapshotToken);
        var commonPlan = Assert.IsType<AcceptedMechanicsPlan>(commonResult.Plan);
        var nestedPlan = Assert.IsType<EffectAcceptedTurnPlan>(commonPlan.EffectPlan);
        Assert.Equal(baselinePlan.InputFingerprint, nestedPlan.InputFingerprint);
    }

    /// <summary>
    /// Publishes skill-sourced periodic damage through the permanent NPC resource owner.
    /// </summary>
    /// <returns>
    /// A task completing after the NPC damage transition and reduced effect lifetime are confirmed.
    /// </returns>
    [Fact]
    public async Task NpcTurnEndPeriodicDamage_PublishesThroughItsPermanentResourceOwner()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        const string npcId = "npc_ref_effect_resource_healer";
        await MaterializeNpcHealthAsync(context, npcId, maximum: 60m);

        var definition = EffectMaterializationTestFixture.CreateDefinition("periodic_damage");
        var effect = EffectMaterializationTestContext.CreateSkillCanonicalEffect(
            ownerKind: "npc",
            profile: "periodic_damage");
        effect["target"]!["targetId"] = npcId;
        effect["lifetime"]!["remainingTurns"] = 2;
        await context.SeedPlayerSkillSourceAsync(definition);
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.NpcEffectsPath,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["entries"] = new JsonArray(new JsonObject
                {
                    ["NPCId"] = npcId,
                    ["activeEffects"] = new JsonArray(effect.DeepClone())
                })
            });
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.IdentityIndexPath,
            EffectMaterializationTestFixture.CreateIdentityIndex(effect));
        await context.CaptureValidatedPendingSnapshotAsync(turn: 43);
        var backups = await context.ReadPendingSnapshotBackupsAsync();

        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();

        Assert.True(
            issues.All(issue => issue.Severity != IssueSeverity.Error),
            DescribeIssues(issues));
        await using var writeLease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var plan = await context.Normalizer.BindTo(writeLease)
            .NormalizeAcceptedMechanicsAsync(backups);

        Assert.NotNull(plan);
        var definitions = ResourceDefinitionCatalog.ParseCanonical(
            (await context.ReadJsonAsync(ResourceMaterializationContract.DefinitionsPath))!
                .ToJsonString(),
            allowMissingPristine: false);
        Assert.NotNull(definitions.Catalog);
        Assert.Empty(definitions.Issues);
        var state = ResourceStateContract.ParseCanonical(
            (await context.ReadJsonAsync(ResourceMaterializationContract.StatePath))!
                .ToJsonString(),
            definitions.Catalog!,
            allowMissingPristine: false);
        Assert.NotNull(state.Ledger);
        Assert.Empty(state.Issues);
        var health = Assert.Single(
            state.Ledger!.Entries,
            entry => entry.Coordinate.OwnerKind == ResourceOwnerKind.Npc &&
                     entry.Coordinate.ResourceOwnerId == npcId &&
                     entry.Coordinate.ResourceKey == "health");
        Assert.Equal(57m, health.Current);

        var history = ResourceHistoryState.ParseCanonical(
            (await context.ReadJsonAsync(ResourceMaterializationContract.HistoryPath))!
                .ToJsonString(),
            definitions.Catalog!,
            allowMissingPristine: false);
        Assert.NotNull(history.History);
        Assert.Empty(history.Issues);
        var transition = Assert.Single(
            history.History!.Transitions,
            candidate => candidate.Turn == 43 &&
                         candidate.Phase == ResourceMutationPhase.EffectTrigger);
        Assert.Equal(npcId, transition.Coordinate.ResourceOwnerId);
        Assert.Equal(3m, transition.AppliedAmount);

        var npcEffects = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.NpcEffectsPath))!.AsObject();
        var entry = Assert.IsType<JsonObject>(Assert.Single(npcEffects["entries"]!.AsArray()));
        var remainingEffect = Assert.IsType<JsonObject>(
            Assert.Single(entry["activeEffects"]!.AsArray()));
        Assert.Equal(1, remainingEffect["lifetime"]!["remainingTurns"]!.GetValue<int>());
    }

    /// <summary>
    /// Publishes skill-sourced periodic damage through the client-owned combatant resource owner.
    /// </summary>
    /// <returns>
    /// A task completing after the combatant health reduction and reduced effect lifetime are confirmed.
    /// </returns>
    [Fact]
    public async Task CombatantTurnEndPeriodicDamage_PublishesThroughItsClientOwnedResourceOwner()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        var combatantId = await MaterializeCombatantHealthAsync(
            context,
            "combatant_ref_effect_resource_raider",
            maximum: 75m);

        var definition = EffectMaterializationTestFixture.CreateDefinition("periodic_damage");
        var effect = EffectMaterializationTestContext.CreateSkillCanonicalEffect(
            ownerKind: "combatant",
            profile: "periodic_damage");
        effect["target"]!["targetId"] = combatantId;
        effect["lifetime"]!["remainingTurns"] = 2;
        await context.SeedPlayerSkillSourceAsync(definition);
        var enemies = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.EnemyCombatantsPath))!.AsObject();
        var combatant = Assert.IsType<JsonObject>(
            Assert.Single(enemies["enemiesData"]!.AsArray()));
        combatant["activeDebuffs"] = new JsonArray(effect.DeepClone());
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.EnemyCombatantsPath,
            enemies);
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.IdentityIndexPath,
            EffectMaterializationTestFixture.CreateIdentityIndex(effect));
        await context.CaptureValidatedPendingSnapshotAsync(turn: 43);
        var backups = await context.ReadPendingSnapshotBackupsAsync();

        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();

        Assert.True(
            issues.All(issue => issue.Severity != IssueSeverity.Error),
            DescribeIssues(issues));
        await using var writeLease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var plan = await context.Normalizer.BindTo(writeLease)
            .NormalizeAcceptedMechanicsAsync(backups);

        Assert.NotNull(plan);
        var definitions = ResourceDefinitionCatalog.ParseCanonical(
            (await context.ReadJsonAsync(ResourceMaterializationContract.DefinitionsPath))!
                .ToJsonString(),
            allowMissingPristine: false);
        Assert.NotNull(definitions.Catalog);
        Assert.Empty(definitions.Issues);
        var state = ResourceStateContract.ParseCanonical(
            (await context.ReadJsonAsync(ResourceMaterializationContract.StatePath))!
                .ToJsonString(),
            definitions.Catalog!,
            allowMissingPristine: false);
        Assert.NotNull(state.Ledger);
        Assert.Empty(state.Issues);
        var health = Assert.Single(
            state.Ledger!.Entries,
            entry => entry.Coordinate.OwnerKind == ResourceOwnerKind.Combatant &&
                     entry.Coordinate.ResourceOwnerId == combatantId &&
                     entry.Coordinate.ResourceKey == "health");
        Assert.Equal(72m, health.Current);

        var publishedEnemies = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.EnemyCombatantsPath))!.AsObject();
        var publishedCombatant = Assert.IsType<JsonObject>(
            Assert.Single(publishedEnemies["enemiesData"]!.AsArray()));
        var remainingEffect = Assert.IsType<JsonObject>(
            Assert.Single(publishedCombatant["activeDebuffs"]!.AsArray()));
        Assert.Equal(1, remainingEffect["lifetime"]!["remainingTurns"]!.GetValue<int>());
    }

    /// <summary>
    /// Publishes resident periodic damage through its setting-defined integrity owner in the original realm.
    /// </summary>
    /// <returns>
    /// A task completing after exact integrity damage, history publication and effect lifetime checks.
    /// </returns>
    [Fact]
    public async Task AfterlifeActorTurnEndPeriodicDamage_PublishesThroughSettingDefinedResourceOwner()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        const string actorId = "resident_effect_resource_keeper";
        var materializedActorId = await MaterializeAfterlifeIntegrityAsync(
            context,
            actorId,
            maximum: 10m);
        Assert.Equal(actorId, materializedActorId);

        var definition = EffectMaterializationTestFixture.CreateDefinition("periodic_damage");
        definition["allowedRealms"] = new JsonArray("shining_abode");
        definition["allowedTargetKinds"] = new JsonArray("resident");
        definition["components"]![0]!["payload"]!["resource"] = "soul_integrity";
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect(
            ownerKind: "resident",
            profile: "periodic_damage");
        effect["realm"] = "shining_abode";
        effect["target"]!["targetId"] = actorId;
        effect["source"] = new JsonObject
        {
            ["kind"] = "spiritual_art",
            ["sourceId"] = "art_soul_integrity_bleed",
            ["definitionKey"] = EffectMaterializationTestFixture.DefinitionKey
        };
        effect["components"]![0]!["payload"]!["resource"] = "soul_integrity";
        effect["lifetime"]!["remainingTurns"] = 2;

        var profilesRoot = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.AfterlifeProfilesPath))!.AsObject();
        var profile = Assert.IsType<JsonObject>(
            Assert.Single(profilesRoot[AfterlifeEntityProfileState.ProfilesProperty]!.AsArray()));
        profile["activeEffects"] = new JsonArray(effect.DeepClone());
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.AfterlifeProfilesPath,
            profilesRoot);
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.IdentityIndexPath,
            EffectMaterializationTestFixture.CreateIdentityIndex(effect));
        await context.CaptureValidatedPendingSnapshotAsync(
            turn: 43,
            currentRealm: "Shining Abode");
        var backups = await context.ReadPendingSnapshotBackupsAsync();

        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();

        Assert.True(
            issues.All(issue => issue.Severity != IssueSeverity.Error),
            DescribeIssues(issues));
        await using var writeLease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var plan = await context.Normalizer.BindTo(writeLease)
            .NormalizeAcceptedMechanicsAsync(backups);

        Assert.NotNull(plan);
        var definitions = ResourceDefinitionCatalog.ParseCanonical(
            (await context.ReadJsonAsync(ResourceMaterializationContract.DefinitionsPath))!
                .ToJsonString(),
            allowMissingPristine: false);
        Assert.NotNull(definitions.Catalog);
        Assert.Empty(definitions.Issues);
        var state = ResourceStateContract.ParseCanonical(
            (await context.ReadJsonAsync(ResourceMaterializationContract.StatePath))!
                .ToJsonString(),
            definitions.Catalog!,
            allowMissingPristine: false);
        Assert.NotNull(state.Ledger);
        Assert.Empty(state.Issues);
        var integrity = Assert.Single(
            state.Ledger!.Entries,
            entry => entry.Coordinate.OwnerKind == ResourceOwnerKind.AfterlifeActor &&
                     entry.Coordinate.ResourceOwnerId == actorId &&
                     entry.Coordinate.ResourceKey == "soul_integrity");
        Assert.Equal(7m, integrity.Current);

        var publishedProfiles = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.AfterlifeProfilesPath))!.AsObject();
        var publishedProfile = Assert.IsType<JsonObject>(
            Assert.Single(publishedProfiles[AfterlifeEntityProfileState.ProfilesProperty]!
                .AsArray()));
        var remainingEffect = Assert.IsType<JsonObject>(
            Assert.Single(publishedProfile["activeEffects"]!.AsArray()));
        Assert.Equal(1, remainingEffect["lifetime"]!["remainingTurns"]!.GetValue<int>());
    }

    /// <summary>
    /// Rejects an unsupported vehicle target without publishing the skill effect or resource writes.
    /// </summary>
    /// <returns>
    /// A task completing after target rejection and unchanged governed bytes are confirmed.
    /// </returns>
    [Fact]
    public async Task VehicleEffectTarget_IsRejectedWithoutResourceOrEffectWrites()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        var resources = ResourceBootstrapStateBuilder.BuildMortalPlayer(
            incarnationNumber: 1,
            turn: 42,
            permanentStrength: 10,
            permanentConstitution: 10,
            permanentIntelligence: 10,
            permanentWisdom: 10,
            permanentFaith: 10);
        Assert.True(resources.IsValid, string.Join(Environment.NewLine, resources.Issues));
        await context.WriteJsonAsync(
            ResourceMaterializationContract.DefinitionsPath,
            JsonNode.Parse(resources.Definitions!.ToCanonicalJson())!);
        await context.WriteJsonAsync(
            ResourceMaterializationContract.StatePath,
            JsonNode.Parse(resources.State!.ToCanonicalJson())!);
        await context.WriteJsonAsync(
            ResourceMaterializationContract.HistoryPath,
            JsonNode.Parse(resources.History!.ToCanonicalJson())!);
        await context.SeedPlayerSkillSourceAsync(
            EffectMaterializationTestFixture.CreateDefinition("periodic_damage"));
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.PlayerEffectsPath,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["activeEffects"] = new JsonArray()
            });
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.IdentityIndexPath,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["entries"] = new JsonArray()
            });
        await context.CaptureValidatedPendingSnapshotAsync(turn: 43);

        var command = EffectMaterializationTestContext.CreateSkillApplyCommand();
        command["target"] = new JsonObject
        {
            ["kind"] = "vehicle",
            ["targetId"] = "vehicle_effect_resource_cart"
        };
        command["eventRef"] = new JsonObject
        {
            ["kind"] = "accepted_turn",
            ["authorityId"] = "turn_43"
        };
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(command));
        var paths = new[]
        {
            ResourceMaterializationContract.StatePath,
            ResourceMaterializationContract.HistoryPath,
            EffectMaterializationTestContext.PlayerEffectsPath,
            EffectMaterializationTestContext.IdentityIndexPath,
            EffectMaterializationTestContext.CommandPath
        };
        var before = await context.CaptureBytesAsync(paths);

        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();

        Assert.Contains(
            issues,
            issue => issue.Severity == IssueSeverity.Error &&
                     issue.Code == "effect_target_selector_invalid");
        var after = await context.CaptureBytesAsync(paths);
        Assert.Equal(before, after);
    }

    /// <summary>
    /// Restores a depleted resource and expires the skill effect's last use in one publication.
    /// </summary>
    /// <returns>
    /// A task completing after ordered resource transitions and expiration of the last effect use are confirmed.
    /// </returns>
    [Fact]
    public async Task ResourceDepletedTrigger_RestoresThroughSameReducerAndExpiresLastUseAtomically()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        var resources = ResourceBootstrapStateBuilder.BuildMortalPlayer(
            incarnationNumber: 1,
            turn: 42,
            permanentStrength: 10,
            permanentConstitution: 10,
            permanentIntelligence: 10,
            permanentWisdom: 10,
            permanentFaith: 10);
        Assert.True(resources.IsValid, string.Join(Environment.NewLine, resources.Issues));
        await context.WriteJsonAsync(
            ResourceMaterializationContract.DefinitionsPath,
            JsonNode.Parse(resources.Definitions!.ToCanonicalJson())!);
        await context.WriteJsonAsync(
            ResourceMaterializationContract.StatePath,
            JsonNode.Parse(resources.State!.ToCanonicalJson())!);
        await context.WriteJsonAsync(
            ResourceMaterializationContract.HistoryPath,
            JsonNode.Parse(resources.History!.ToCanonicalJson())!);

        var definition = CreateResourceEventDefinition();
        var effect = CreateResourceEventEffect();
        await context.SeedPlayerSkillSourceAsync(definition);
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.PlayerEffectsPath,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["activeEffects"] = new JsonArray(effect.DeepClone())
            });
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.IdentityIndexPath,
            EffectMaterializationTestFixture.CreateIdentityIndex(effect));
        await context.WriteJsonAsync(
            "game_state/world/world_events.json",
            new JsonObject { ["worldEventsLog"] = new JsonArray() });
        await context.WriteJsonAsync(
            "game_state/meta/soul_state.json",
            new JsonObject
            {
                ["soulName"] = "Soul",
                ["currentRealm"] = "Mortal World",
                [ShiningBlessingEffectState.SoulStateProperty] = new JsonObject
                {
                    ["applicationState"] = "active",
                    ["pendingSurvivalEffects"] = new JsonArray(new JsonObject
                    {
                        ["sourceCardId"] = "card_resource_event_authority",
                        ["status"] = ShiningBlessingEffectState
                            .SurvivalStatusPendingFirstRuinousFailure,
                        ["recovery"] = 20,
                        ["downgrade"] = 1
                    })
                }
            });
        await context.CaptureValidatedPendingSnapshotAsync(turn: 43);
        var backups = await context.ReadPendingSnapshotBackupsAsync();

        await context.WriteJsonAsync(
            "game_state/world/world_events.json",
            new JsonObject
            {
                ["worldEventsLog"] = new JsonArray(new JsonObject
                {
                    ["eventId"] = "event_health_depleted",
                    ["summary"] = "A ruinous accepted event depletes health.",
                    ["isActive"] = true,
                    ["visibility"] = "player_known",
                    ["severity"] = "ruinous"
                })
            });
        var initialHealth = Assert.Single(
            resources.State!.Entries,
            entry => entry.Coordinate.ResourceKey == "health").Current;
        await context.WriteJsonAsync(
            ResourceMaterializationContract.CommandPath,
            new JsonObject
            {
                ["resourceDefinitionCreations"] = new JsonArray(),
                ["resourceCapacityChanges"] = new JsonArray(),
                ["resourceChanges"] = new JsonArray(new JsonObject
                {
                    ["operation"] = "damage",
                    ["target"] = new JsonObject
                    {
                        ["kind"] = "player",
                        ["targetId"] = "player_current"
                    },
                    ["resourceKey"] = "health",
                    ["amount"] = initialHealth,
                    ["source"] = new JsonObject
                    {
                        ["kind"] = "narrative_outcome"
                    },
                    ["eventRef"] = "turn_43:resource:1",
                    ["reason"] = "Accepted damage depletes health."
                })
            });

        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();

        Assert.True(
            issues.All(issue => issue.Severity != IssueSeverity.Error),
            string.Join(
                Environment.NewLine,
                issues.Select(issue =>
                    $"{issue.Code}: expected={issue.Expected}; actual={issue.Actual}")));
        await using var writeLease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var plan = await context.Normalizer.BindTo(writeLease)
            .NormalizeAcceptedMechanicsAsync(backups);

        Assert.NotNull(plan);
        var definitions = ResourceDefinitionCatalog.ParseCanonical(
            (await context.ReadJsonAsync(ResourceMaterializationContract.DefinitionsPath))!
                .ToJsonString(),
            allowMissingPristine: false);
        Assert.NotNull(definitions.Catalog);
        Assert.Empty(definitions.Issues);
        var state = ResourceStateContract.ParseCanonical(
            (await context.ReadJsonAsync(ResourceMaterializationContract.StatePath))!
                .ToJsonString(),
            definitions.Catalog!,
            allowMissingPristine: false);
        Assert.NotNull(state.Ledger);
        Assert.Empty(state.Issues);
        var health = Assert.Single(
            state.Ledger!.Entries,
            entry => entry.Coordinate.ResourceKey == "health");
        Assert.Equal(29m, health.Current);

        var history = ResourceHistoryState.ParseCanonical(
            (await context.ReadJsonAsync(ResourceMaterializationContract.HistoryPath))!
                .ToJsonString(),
            definitions.Catalog!,
            allowMissingPristine: false);
        Assert.NotNull(history.History);
        Assert.Empty(history.Issues);
        var turnTransitions = history.History!.Transitions
            .Where(transition => transition.Turn == 43)
            .ToArray();
        Assert.Equal(3, turnTransitions.Length);
        Assert.Equal(ResourceMutationPhase.DirectOutcome, turnTransitions[0].Phase);
        Assert.Equal(ResourceMutationPhase.EffectTrigger, turnTransitions[1].Phase);
        Assert.Equal("effect_component", turnTransitions[1].OriginKind);
        Assert.Equal(3m, turnTransitions[1].AppliedAmount);
        Assert.Equal(ResourceMutationPhase.RegisteredSystemOutcome, turnTransitions[2].Phase);

        var playerEffects = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.PlayerEffectsPath))!.AsObject();
        Assert.Empty(playerEffects["activeEffects"]!.AsArray());
        var identity = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.IdentityIndexPath))!.AsObject();
        var entry = Assert.IsType<JsonObject>(Assert.Single(identity["entries"]!.AsArray()));
        Assert.Equal("expired", entry["state"]!.GetValue<string>());
        Assert.Equal(
            "expire",
            entry["transitions"]!.AsArray()[^1]!["kind"]!.GetValue<string>());
    }

    /// <summary>
    /// Requests bounded resolution only when the accepted damage crosses the resource boundary.
    /// </summary>
    /// <param name="crossesBoundary">
    /// Whether the accepted resource damage reaches the depletion boundary.
    /// </param>
    /// <returns>
    /// A task completing after pending state, health and the unspent effect use match the selected boundary case.
    /// </returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BoundedResourceDepletedTrigger_PendsOnlyForActualBoundaryCrossing(
        bool crossesBoundary)
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        var resources = ResourceBootstrapStateBuilder.BuildMortalPlayer(
            incarnationNumber: 1,
            turn: 42,
            permanentStrength: 10,
            permanentConstitution: 10,
            permanentIntelligence: 10,
            permanentWisdom: 10,
            permanentFaith: 10);
        Assert.True(resources.IsValid, string.Join(Environment.NewLine, resources.Issues));
        await context.WriteJsonAsync(
            ResourceMaterializationContract.DefinitionsPath,
            JsonNode.Parse(resources.Definitions!.ToCanonicalJson())!);
        await context.WriteJsonAsync(
            ResourceMaterializationContract.StatePath,
            JsonNode.Parse(resources.State!.ToCanonicalJson())!);
        await context.WriteJsonAsync(
            ResourceMaterializationContract.HistoryPath,
            JsonNode.Parse(resources.History!.ToCanonicalJson())!);

        var definition = CreateResourceEventDefinition();
        definition["triggers"]![0]!["resolutionMode"] = "bounded_receipt";
        var effect = CreateResourceEventEffect();
        effect["triggers"]![0]!["resolutionMode"] = "bounded_receipt";
        await context.SeedPlayerSkillSourceAsync(definition);
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.PlayerEffectsPath,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["activeEffects"] = new JsonArray(effect.DeepClone())
            });
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.IdentityIndexPath,
            EffectMaterializationTestFixture.CreateIdentityIndex(effect));
        await context.WriteJsonAsync(
            "game_state/world/world_events.json",
            new JsonObject { ["worldEventsLog"] = new JsonArray() });
        await context.WriteJsonAsync(
            "game_state/meta/soul_state.json",
            new JsonObject
            {
                ["soulName"] = "Soul",
                ["currentRealm"] = "Mortal World",
                [ShiningBlessingEffectState.SoulStateProperty] = new JsonObject
                {
                    ["applicationState"] = "active",
                    ["pendingSurvivalEffects"] = new JsonArray()
                }
            });
        await context.CaptureValidatedPendingSnapshotAsync(turn: 43);
        var backups = await context.ReadPendingSnapshotBackupsAsync();

        await context.WriteJsonAsync(
            "game_state/world/world_events.json",
            new JsonObject
            {
                ["worldEventsLog"] = new JsonArray(new JsonObject
                {
                    ["eventId"] = "event_boundary_damage",
                    ["summary"] = "Accepted damage tests an exact depletion boundary.",
                    ["isActive"] = true,
                    ["visibility"] = "player_known",
                    ["severity"] = "minor"
                })
            });
        var initialHealth = Assert.Single(
            resources.State!.Entries,
            entry => entry.Coordinate.ResourceKey == "health").Current;
        await context.WriteJsonAsync(
            ResourceMaterializationContract.CommandPath,
            new JsonObject
            {
                ["resourceDefinitionCreations"] = new JsonArray(),
                ["resourceCapacityChanges"] = new JsonArray(),
                ["resourceChanges"] = new JsonArray(new JsonObject
                {
                    ["operation"] = "damage",
                    ["target"] = new JsonObject
                    {
                        ["kind"] = "player",
                        ["targetId"] = "player_current"
                    },
                    ["resourceKey"] = "health",
                    ["amount"] = crossesBoundary ? initialHealth : 1m,
                    ["source"] = new JsonObject
                    {
                        ["kind"] = "narrative_outcome"
                    },
                    ["eventRef"] = "turn_43:resource:1",
                    ["reason"] = "Accepted damage may or may not cross the minimum."
                })
            });

        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();
        Assert.True(
            issues.All(issue => issue.Severity != IssueSeverity.Error),
            string.Join(Environment.NewLine, issues.Select(issue =>
                $"{issue.Code}: expected={issue.Expected}; actual={issue.Actual}")));
        await using var writeLease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var plan = await context.Normalizer.BindTo(writeLease)
            .NormalizeAcceptedMechanicsAsync(backups);

        Assert.NotNull(plan);
        Assert.Equal(crossesBoundary, plan!.AwaitsPendingResolution);
        Assert.Equal(
            crossesBoundary,
            context.FileSystem.FileExists(ResourcePendingResolutionState.PendingPath));
        var state = ResourceStateContract.ParseCanonical(
            (await context.ReadJsonAsync(ResourceMaterializationContract.StatePath))!
                .ToJsonString(),
            resources.Definitions!,
            allowMissingPristine: false);
        Assert.NotNull(state.Ledger);
        Assert.Empty(state.Issues);
        var health = Assert.Single(
            state.Ledger!.Entries,
            entry => entry.Coordinate.ResourceKey == "health");
        Assert.Equal(
            crossesBoundary ? initialHealth : initialHealth - 1m,
            health.Current);
        var playerEffects = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.PlayerEffectsPath))!.AsObject();
        var activeEffect = Assert.IsType<JsonObject>(
            Assert.Single(playerEffects["activeEffects"]!.AsArray()));
        Assert.Equal(1, activeEffect["lifetime"]!["remainingUses"]!.GetValue<int>());
    }

    /// <summary>
    /// Prevents two matching resource events from consuming more than the skill effect's remaining use.
    /// </summary>
    /// <returns>
    /// A task completing after one effect-trigger transition and exhaustion of the sole effect use are confirmed.
    /// </returns>
    [Fact]
    public async Task TwoMatchingResourceEvents_CannotExecuteBeyondRemainingUses()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        var resources = ResourceBootstrapStateBuilder.BuildMortalPlayer(
            incarnationNumber: 1,
            turn: 42,
            permanentStrength: 10,
            permanentConstitution: 10,
            permanentIntelligence: 10,
            permanentWisdom: 10,
            permanentFaith: 10);
        Assert.True(resources.IsValid, string.Join(Environment.NewLine, resources.Issues));
        await context.WriteJsonAsync(
            ResourceMaterializationContract.DefinitionsPath,
            JsonNode.Parse(resources.Definitions!.ToCanonicalJson())!);
        await context.WriteJsonAsync(
            ResourceMaterializationContract.StatePath,
            JsonNode.Parse(resources.State!.ToCanonicalJson())!);
        await context.WriteJsonAsync(
            ResourceMaterializationContract.HistoryPath,
            JsonNode.Parse(resources.History!.ToCanonicalJson())!);

        var definition = CreateResourceEventDefinition(
            "resource_damaged",
            "on_resource_damaged");
        var effect = CreateResourceEventEffect(
            "resource_damaged",
            "on_resource_damaged");
        effect["components"]![0]!["payload"]!["amount"] = 1;
        await context.SeedPlayerSkillSourceAsync(definition);
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.PlayerEffectsPath,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["activeEffects"] = new JsonArray(effect.DeepClone())
            });
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.IdentityIndexPath,
            EffectMaterializationTestFixture.CreateIdentityIndex(effect));
        await context.WriteJsonAsync(
            "game_state/world/world_events.json",
            new JsonObject { ["worldEventsLog"] = new JsonArray() });
        await context.WriteJsonAsync(
            "game_state/meta/soul_state.json",
            new JsonObject
            {
                ["soulName"] = "Soul",
                ["currentRealm"] = "Mortal World",
                [ShiningBlessingEffectState.SoulStateProperty] = new JsonObject
                {
                    ["applicationState"] = "active",
                    ["pendingSurvivalEffects"] = new JsonArray(new JsonObject
                    {
                        ["sourceCardId"] = "card_two_hits_authority",
                        ["status"] = ShiningBlessingEffectState
                            .SurvivalStatusPendingFirstRuinousFailure,
                        ["recovery"] = 20,
                        ["downgrade"] = 1
                    })
                }
            });
        await context.CaptureValidatedPendingSnapshotAsync(turn: 43);
        var backups = await context.ReadPendingSnapshotBackupsAsync();

        await context.WriteJsonAsync(
            "game_state/world/world_events.json",
            new JsonObject
            {
                ["worldEventsLog"] = new JsonArray(
                    CreateWorldEvent("event_two_hits", "Two accepted hits land."))
            });
        await context.WriteJsonAsync(
            ResourceMaterializationContract.CommandPath,
            new JsonObject
            {
                ["resourceDefinitionCreations"] = new JsonArray(),
                ["resourceCapacityChanges"] = new JsonArray(),
                ["resourceChanges"] = new JsonArray(
                    CreateDamageCommand("turn_43:resource:1"),
                    CreateDamageCommand("turn_43:resource:2"))
            });

        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();

        Assert.True(
            issues.All(issue => issue.Severity != IssueSeverity.Error),
            string.Join(
                Environment.NewLine,
                issues.Select(issue =>
                    $"{issue.Code}: expected={issue.Expected}; actual={issue.Actual}")));
        await using var writeLease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var plan = await context.Normalizer.BindTo(writeLease)
            .NormalizeAcceptedMechanicsAsync(backups);

        Assert.NotNull(plan);
        var definitions = ResourceDefinitionCatalog.ParseCanonical(
            (await context.ReadJsonAsync(ResourceMaterializationContract.DefinitionsPath))!
                .ToJsonString(),
            allowMissingPristine: false);
        Assert.NotNull(definitions.Catalog);
        Assert.Empty(definitions.Issues);
        var state = ResourceStateContract.ParseCanonical(
            (await context.ReadJsonAsync(ResourceMaterializationContract.StatePath))!
                .ToJsonString(),
            definitions.Catalog!,
            allowMissingPristine: false);
        Assert.NotNull(state.Ledger);
        Assert.Empty(state.Issues);
        var health = Assert.Single(
            state.Ledger!.Entries,
            entry => entry.Coordinate.ResourceKey == "health");
        Assert.Equal(health.Maximum - 1m, health.Current);

        var history = ResourceHistoryState.ParseCanonical(
            (await context.ReadJsonAsync(ResourceMaterializationContract.HistoryPath))!
                .ToJsonString(),
            definitions.Catalog!,
            allowMissingPristine: false);
        Assert.NotNull(history.History);
        Assert.Empty(history.Issues);
        var turnTransitions = history.History!.Transitions
            .Where(transition => transition.Turn == 43)
            .ToArray();
        Assert.Equal(3, turnTransitions.Length);
        Assert.Equal(
            1,
            turnTransitions.Count(transition =>
                transition.Phase == ResourceMutationPhase.EffectTrigger));

        var playerEffects = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.PlayerEffectsPath))!.AsObject();
        Assert.Empty(playerEffects["activeEffects"]!.AsArray());

        static JsonObject CreateWorldEvent(string eventId, string summary) => new()
        {
            ["eventId"] = eventId,
            ["summary"] = summary,
            ["isActive"] = true,
            ["visibility"] = "player_known",
            ["severity"] = "ruinous"
        };

        static JsonObject CreateDamageCommand(string eventRef) => new()
        {
            ["operation"] = "damage",
            ["target"] = new JsonObject
            {
                ["kind"] = "player",
                ["targetId"] = "player_current"
            },
            ["resourceKey"] = "health",
            ["amount"] = 1,
            ["source"] = new JsonObject
            {
                ["kind"] = "narrative_outcome"
            },
            ["eventRef"] = eventRef,
            ["reason"] = "Accepted hit."
        };
    }

    /// <summary>
    /// Rejects publication after the registered skill source disappears without effect or resource writes.
    /// </summary>
    /// <returns>
    /// A task completing after source-change rejection and preserved governed beforeimages are confirmed.
    /// </returns>
    [Fact]
    public async Task SourceDisappearsAfterValidation_PublicationFailsWithoutResourceOrEffectWrites()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        var resources = ResourceBootstrapStateBuilder.BuildMortalPlayer(
            incarnationNumber: 1,
            turn: 42,
            permanentStrength: 10,
            permanentConstitution: 10,
            permanentIntelligence: 10,
            permanentWisdom: 10,
            permanentFaith: 10);
        Assert.True(resources.IsValid, string.Join(Environment.NewLine, resources.Issues));
        await context.WriteJsonAsync(
            ResourceMaterializationContract.DefinitionsPath,
            JsonNode.Parse(resources.Definitions!.ToCanonicalJson())!);
        await context.WriteJsonAsync(
            ResourceMaterializationContract.StatePath,
            JsonNode.Parse(resources.State!.ToCanonicalJson())!);
        await context.WriteJsonAsync(
            ResourceMaterializationContract.HistoryPath,
            JsonNode.Parse(resources.History!.ToCanonicalJson())!);

        var definition = EffectMaterializationTestFixture.CreateDefinition("periodic_damage");
        var effect = EffectMaterializationTestContext.CreateSkillCanonicalEffect(
            ownerKind: "player",
            profile: "periodic_damage");
        effect["lifetime"]!["remainingTurns"] = 2;
        await context.SeedPlayerSkillSourceAsync(definition);
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.PlayerEffectsPath,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["activeEffects"] = new JsonArray(effect.DeepClone())
            });
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.IdentityIndexPath,
            EffectMaterializationTestFixture.CreateIdentityIndex(effect));
        await context.CaptureValidatedPendingSnapshotAsync(turn: 43);
        var backups = await context.ReadPendingSnapshotBackupsAsync();

        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();

        Assert.True(
            issues.All(issue => issue.Severity != IssueSeverity.Error),
            DescribeIssues(issues));
        context.FileSystem.DeleteFile(EffectMaterializationTestContext.MaterializableSkillPath);
        var before = await context.CaptureBytesAsync(
            ResourceMaterializationContract.StatePath,
            ResourceMaterializationContract.HistoryPath,
            EffectMaterializationTestContext.PlayerEffectsPath,
            EffectMaterializationTestContext.IdentityIndexPath,
            EffectMaterializationTestContext.MaterializableSkillPath);

        await using (var writeLease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            var exception = await Assert.ThrowsAsync<InvalidDataException>(
                () => context.Normalizer.BindTo(writeLease)
                    .NormalizeAcceptedMechanicsAsync(backups));
            Assert.Contains(
                "changed after effect validation",
                exception.Message,
                StringComparison.Ordinal);
        }

        var after = await context.CaptureBytesAsync(
            ResourceMaterializationContract.StatePath,
            ResourceMaterializationContract.HistoryPath,
            EffectMaterializationTestContext.PlayerEffectsPath,
            EffectMaterializationTestContext.IdentityIndexPath,
            EffectMaterializationTestContext.MaterializableSkillPath);
        Assert.Equal(before, after);
    }

    /// <summary>
    /// Rejects publication after the skill effect's target disappears without effect or resource writes.
    /// </summary>
    /// <returns>
    /// A task completing after target-change rejection and preserved governed beforeimages are confirmed.
    /// </returns>
    [Fact]
    public async Task TargetDisappearsAfterValidation_PublicationFailsWithoutResourceOrEffectWrites()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        const string npcId = "npc_ref_effect_resource_late_target";
        await MaterializeNpcHealthAsync(context, npcId, maximum: 60m);

        var definition = EffectMaterializationTestFixture.CreateDefinition("periodic_damage");
        var effect = EffectMaterializationTestContext.CreateSkillCanonicalEffect(
            ownerKind: "npc",
            profile: "periodic_damage");
        effect["target"]!["targetId"] = npcId;
        effect["lifetime"]!["remainingTurns"] = 2;
        await context.SeedPlayerSkillSourceAsync(definition);
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.NpcEffectsPath,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["entries"] = new JsonArray(new JsonObject
                {
                    ["NPCId"] = npcId,
                    ["activeEffects"] = new JsonArray(effect.DeepClone())
                })
            });
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.IdentityIndexPath,
            EffectMaterializationTestFixture.CreateIdentityIndex(effect));
        await context.CaptureValidatedPendingSnapshotAsync(turn: 43);
        var backups = await context.ReadPendingSnapshotBackupsAsync();

        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();

        Assert.True(
            issues.All(issue => issue.Severity != IssueSeverity.Error),
            DescribeIssues(issues));
        await context.WriteJsonAsync(
            NpcCorePath,
            new JsonObject { ["NPCsInScene"] = new JsonArray() });
        var before = await context.CaptureBytesAsync(
            ResourceMaterializationContract.StatePath,
            ResourceMaterializationContract.HistoryPath,
            EffectMaterializationTestContext.NpcEffectsPath,
            EffectMaterializationTestContext.IdentityIndexPath,
            NpcCorePath);

        await using (var writeLease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            var exception = await Assert.ThrowsAsync<InvalidDataException>(
                () => context.Normalizer.BindTo(writeLease)
                    .NormalizeAcceptedMechanicsAsync(backups));
            Assert.Contains(
                "changed after effect validation",
                exception.Message,
                StringComparison.Ordinal);
        }

        var after = await context.CaptureBytesAsync(
            ResourceMaterializationContract.StatePath,
            ResourceMaterializationContract.HistoryPath,
            EffectMaterializationTestContext.NpcEffectsPath,
            EffectMaterializationTestContext.IdentityIndexPath,
            NpcCorePath);
        Assert.Equal(before, after);
    }

    private static async Task SeedCurrentResourceOwnerAuthorityAsync(
        EffectMaterializationTestContext context)
    {
        var definitions = ResourceDefinitionCatalog.ParseCanonical(
            await context.FileSystem.ReadFileAsync(
                ResourceMaterializationContract.DefinitionsPath),
            allowMissingPristine: false);
        Assert.True(definitions.IsValid, DescribeIssues(definitions.Issues));
        var state = ResourceStateContract.ParseCanonical(
            await context.FileSystem.ReadFileAsync(ResourceMaterializationContract.StatePath),
            definitions.Catalog!,
            allowMissingPristine: false);
        Assert.True(state.IsValid, DescribeIssues(state.Issues));
        var history = ResourceHistoryState.ParseCanonical(
            await context.FileSystem.ReadFileAsync(ResourceMaterializationContract.HistoryPath),
            definitions.Catalog!,
            allowMissingPristine: false);
        Assert.True(history.IsValid, DescribeIssues(history.Issues));
        var authority = await CanonicalResourceOwnerAuthorityComposer.ComposeAsync(
            definitions.Catalog!,
            context.FileSystem.ReadFileAsync,
            state.Ledger,
            history.History,
            CanonicalResourceOwnerAuthorityPurpose.ExplicitBootstrap);
        Assert.True(authority.IsValid, DescribeIssues(authority.Issues));
        await context.WriteJsonAsync(
            CanonicalResourceOwnerAuthorityComposer.AuthorityPath,
            JsonNode.Parse(authority.CanonicalAuthorityJson!)!);
    }

    private static JsonObject CreateResourceEventDefinition(
        string eventType = "resource_depleted",
        string triggerId = "on_resource_depleted")
    {
        var definition = EffectMaterializationTestFixture.CreateDefinition("periodic_restore");
        definition["lifetime"] = new JsonObject
        {
            ["mode"] = "uses",
            ["initialUses"] = 1,
            ["consumingEventTypes"] = new JsonArray(eventType)
        };
        definition["triggers"] = new JsonArray(
            CreateResourceEventTrigger(eventType, triggerId));
        return definition;
    }

    /// <summary>
    /// Creates a registered skill effect whose last use is consumed by the selected resource event.
    /// </summary>
    /// <param name="eventType">
    /// Exact resource event type to consume; defaults to resource_depleted.
    /// </param>
    /// <param name="triggerId">
    /// Trigger identity shared by the event declaration and remaining-use lifetime.
    /// </param>
    /// <returns>
    /// A fresh periodic-restore effect with one use and the requested resource trigger.
    /// </returns>
    private static JsonObject CreateResourceEventEffect(
        string eventType = "resource_depleted",
        string triggerId = "on_resource_depleted")
    {
        var effect = EffectMaterializationTestContext.CreateSkillCanonicalEffect(
            profile: "periodic_restore");
        effect["lifetime"] = new JsonObject
        {
            ["mode"] = "uses",
            ["remainingUses"] = 1,
            ["consumingTriggerIds"] = new JsonArray(triggerId)
        };
        effect["triggers"] = new JsonArray(
            CreateResourceEventTrigger(eventType, triggerId));
        return effect;
    }

    private static JsonObject CreateResourceEventTrigger(
        string eventType,
        string triggerId) => new()
    {
        ["triggerId"] = triggerId,
        ["eventType"] = eventType,
        ["priority"] = 100,
        ["componentIds"] = new JsonArray("component_001"),
        ["consumeUses"] = true,
        ["resolutionMode"] = "deterministic"
    };

    private static async Task MaterializeNpcHealthAsync(
        EffectMaterializationTestContext context,
        string npcId,
        decimal maximum)
    {
        var bootstrap = ResourceBootstrapStateBuilder.BuildPristine();
        Assert.True(bootstrap.IsValid, string.Join(Environment.NewLine, bootstrap.Issues));
        await context.WriteJsonAsync(
            ResourceMaterializationContract.DefinitionsPath,
            JsonNode.Parse(bootstrap.Definitions!.ToCanonicalJson())!);
        await context.WriteJsonAsync(
            ResourceMaterializationContract.StatePath,
            JsonNode.Parse(bootstrap.State!.ToCanonicalJson())!);
        await context.WriteJsonAsync(
            ResourceMaterializationContract.HistoryPath,
            JsonNode.Parse(bootstrap.History!.ToCanonicalJson())!);
        await context.WriteJsonAsync(
            NpcCorePath,
            new JsonObject { ["NPCsInScene"] = new JsonArray() });
        await SeedCurrentResourceOwnerAuthorityAsync(context);
        await context.CaptureValidatedPendingSnapshotAsync(turn: 42);

        var npc = EffectMaterializationTestFixture.CreateSameTurnMortalActor(npcId);
        npc["resourceMaterialization"] = new JsonObject
        {
            ["resources"] = new JsonArray(new JsonObject
            {
                ["resourceKey"] = "health",
                ["maximum"] = maximum
            })
        };
        await context.WriteJsonAsync(
            NpcCorePath,
            new JsonObject { ["UpdateNPCs"] = new JsonArray(npc) });

        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();
        Assert.True(
            issues.All(issue => issue.Severity != IssueSeverity.Error),
            DescribeIssues(issues));
        await using var writeLease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        Assert.NotNull(await context.Normalizer.BindTo(writeLease)
            .NormalizeAcceptedMechanicsAsync(backups: null));
    }

    private static async Task<string> MaterializeCombatantHealthAsync(
        EffectMaterializationTestContext context,
        string combatantRef,
        decimal maximum)
    {
        var bootstrap = ResourceBootstrapStateBuilder.BuildPristine();
        Assert.True(bootstrap.IsValid, string.Join(Environment.NewLine, bootstrap.Issues));
        await context.WriteJsonAsync(
            ResourceMaterializationContract.DefinitionsPath,
            JsonNode.Parse(bootstrap.Definitions!.ToCanonicalJson())!);
        await context.WriteJsonAsync(
            ResourceMaterializationContract.StatePath,
            JsonNode.Parse(bootstrap.State!.ToCanonicalJson())!);
        await context.WriteJsonAsync(
            ResourceMaterializationContract.HistoryPath,
            JsonNode.Parse(bootstrap.History!.ToCanonicalJson())!);
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.EnemyCombatantsPath,
            new JsonObject { ["enemiesData"] = new JsonArray() });
        await SeedCurrentResourceOwnerAuthorityAsync(context);
        await context.CaptureValidatedPendingSnapshotAsync(turn: 42);

        var combatant = EffectMaterializationTestFixture.CreateSameTurnCombatant(
            combatantRef);
        foreach (var field in new[]
                 {
                     "currentHealth", "maxHealth", "currentPoise", "maxPoise"
                 })
        {
            combatant.Remove(field);
        }
        combatant["resourceMaterialization"] = new JsonObject
        {
            ["resources"] = new JsonArray(
                new JsonObject
                {
                    ["resourceKey"] = "health",
                    ["maximum"] = maximum
                },
                new JsonObject
                {
                    ["resourceKey"] = "poise",
                    ["maximum"] = 40
                })
        };
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.EnemyCombatantsPath,
            new JsonObject { ["enemiesData"] = new JsonArray(combatant) });

        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();
        Assert.True(
            issues.All(issue => issue.Severity != IssueSeverity.Error),
            DescribeIssues(issues));
        await using (var writeLease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            Assert.NotNull(await context.Normalizer.BindTo(writeLease)
                .NormalizeAcceptedMechanicsAsync(backups: null));
        }
        var canonicalEnemies = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.EnemyCombatantsPath))!.AsObject();
        var canonicalCombatant = Assert.IsType<JsonObject>(
            Assert.Single(canonicalEnemies["enemiesData"]!.AsArray()));
        return canonicalCombatant["combatantId"]!.GetValue<string>();
    }

    /// <summary>
    /// Materializes the resident and its setting-defined integrity resource from matching signed realm authority.
    /// </summary>
    /// <param name="context">
    /// Fresh context receiving the original bootstrap, signed snapshot and ordinary accepted publication.
    /// </param>
    /// <param name="actorId">
    /// Exact resident identity retained by its profile, source art and resource owner.
    /// </param>
    /// <param name="maximum">
    /// Integrity maximum used to initialize the resident resource at full capacity.
    /// </param>
    /// <returns>
    /// The materialized resident ID after raw validation and common plan publication.
    /// </returns>
    private static async Task<string> MaterializeAfterlifeIntegrityAsync(
        EffectMaterializationTestContext context,
        string actorId,
        decimal maximum)
    {
        var bootstrap = ResourceBootstrapStateBuilder.BuildPristine();
        Assert.True(bootstrap.IsValid, string.Join(Environment.NewLine, bootstrap.Issues));
        var definitions = WithAfterlifeIntegrity(bootstrap.Definitions!);
        await context.WriteJsonAsync(
            ResourceMaterializationContract.DefinitionsPath,
            JsonNode.Parse(definitions.ToCanonicalJson())!);
        await context.WriteJsonAsync(
            ResourceMaterializationContract.StatePath,
            JsonNode.Parse(bootstrap.State!.ToCanonicalJson())!);
        await context.WriteJsonAsync(
            ResourceMaterializationContract.HistoryPath,
            JsonNode.Parse(bootstrap.History!.ToCanonicalJson())!);
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.AfterlifeProfilesPath,
            AfterlifeEntityProfileState.CreateDefaultRoot());
        await context.WriteJsonAsync(
            "game_state/meta/soul_state.json",
            new JsonObject
            {
                ["currentRealm"] = "Shining Abode",
                [AfterlifeSpiritualConflictState.SoulStateProfileProperty] = new JsonObject
                {
                    [AfterlifeSpiritualConflictState.SpiritFocusTierProperty] = 0
                }
            });
        await SeedCurrentResourceOwnerAuthorityAsync(context);
        await context.CaptureValidatedPendingSnapshotAsync(
            turn: 42,
            currentRealm: "Shining Abode");

        var acceptedRoot = AfterlifeEntityProfileState.CreateDefaultRoot();
        var sourceDefinition = EffectMaterializationTestFixture.CreateDefinition(
            "periodic_damage");
        sourceDefinition["allowedRealms"] = new JsonArray("shining_abode");
        sourceDefinition["allowedTargetKinds"] = new JsonArray("resident");
        sourceDefinition["components"]![0]!["payload"]!["resource"] =
            "soul_integrity";
        var profile = AfterlifeActorMaterializationTestFixture.CreateCompleteProfile(
            "resident",
            actorId,
            "Shining Abode",
            materializedAtTurn: 42,
            specialArts: new JsonArray(new JsonObject
            {
                ["artId"] = "art_soul_integrity_bleed",
                ["displayName"] = "Кровотечение целостности",
                ["effectDescription"] = "Ослабляет целостность души.",
                ["owner"] = actorId,
                ["baseOperation"] = "damage",
                ["activeEffectDefinitions"] = new JsonArray(sourceDefinition)
            }));
        profile["resourceMaterialization"] = new JsonObject
        {
            ["resources"] = new JsonArray(new JsonObject
            {
                ["resourceKey"] = "soul_integrity",
                ["maximum"] = maximum
            })
        };
        acceptedRoot[AfterlifeEntityProfileState.ResponseProfilesProperty] = new JsonArray(
            profile);
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.AfterlifeProfilesPath,
            acceptedRoot);

        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();
        Assert.True(
            issues.All(issue => issue.Severity != IssueSeverity.Error),
            DescribeIssues(issues));
        await using (var writeLease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            Assert.NotNull(await context.Normalizer.BindTo(writeLease)
                .NormalizeAcceptedMechanicsAsync(backups: null));
        }

        var canonicalRoot = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.AfterlifeProfilesPath))!.AsObject();
        var canonicalProfile = Assert.IsType<JsonObject>(
            Assert.Single(canonicalRoot[AfterlifeEntityProfileState.ProfilesProperty]!.AsArray()));
        Assert.False(canonicalProfile.ContainsKey("resourceMaterialization"));
        Assert.Equal(actorId, canonicalProfile["actorId"]!.GetValue<string>());
        return actorId;
    }

    private static ResourceDefinitionCatalog WithAfterlifeIntegrity(
        ResourceDefinitionCatalog definitions)
    {
        var proposal = new JsonObject
        {
            ["resourceKey"] = "soul_integrity",
            ["definitionVersion"] = 1,
            ["displayName"] = "Целостность души",
            ["numericKind"] = "integer",
            ["unit"] = "point",
            ["quantum"] = 1,
            ["minimumPolicy"] = new JsonObject
            {
                ["kind"] = "definition_fixed",
                ["value"] = 0
            },
            ["capacityPolicy"] = new JsonObject
            {
                ["kind"] = "instance_fixed"
            },
            ["initializationPolicy"] = new JsonObject
            {
                ["kind"] = "maximum"
            },
            ["allowedOwnerKinds"] = new JsonArray("afterlife_actor"),
            ["allowedOperations"] = new JsonArray("damage", "restore"),
            ["defaultFloorPolicy"] = "clamp_to_minimum",
            ["defaultCapPolicy"] = "clamp_to_maximum",
            ["visibility"] = "owner_visible"
        };
        using var document = JsonDocument.Parse(proposal.ToJsonString());
        var materialized = ResourceDefinitionCatalog.MaterializeProposal(
            document.RootElement,
            definitions,
            createdAtTurn: 1,
            createdEventRef: "turn_1:resource_definition:1",
            static () => new ResourceDefinitionIdentity(
                "resource_definition_soul_integrity",
                "resource_definition_seal_soul_integrity"));
        Assert.True(
            materialized.IsValid,
            string.Join(Environment.NewLine, materialized.Issues));
        return definitions.With(materialized.Definition!);
    }

    private static string DescribeIssues(IEnumerable<ValidationIssue> issues) =>
        string.Join(
            Environment.NewLine,
            issues.Select(issue =>
                $"{issue.Code}: expected={issue.Expected}; actual={issue.Actual}"));
}
