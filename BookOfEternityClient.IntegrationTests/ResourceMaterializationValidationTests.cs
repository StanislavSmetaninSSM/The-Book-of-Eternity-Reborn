using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ResourceMaterializationValidationTests
{
    [Fact]
    public void FreshNewGameBootstrap_SeedsBuiltInCatalogWithEmptyAgreedLedger()
    {
        var result = ResourceBootstrapStateBuilder.BuildPristine();

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Issues));
        Assert.NotEmpty(result.Definitions!.Definitions);
        Assert.Empty(result.State!.Entries);
        Assert.Empty(result.History!.Transitions);
        Assert.Empty(result.History.ValidateStateAgreement(result.State));
    }

    [Fact]
    public async Task CanonicalValidation_RejectsExistingQuartetWithMissingOwnerAuthorityRoot()
    {
        await using var context = await ResourceMaterializationTestContext.CreateAsync();
        var bootstrap = ResourceBootstrapStateBuilder.BuildPristine();
        Assert.True(bootstrap.IsValid, string.Join(Environment.NewLine, bootstrap.Issues));
        await context.WriteExactJsonAsync(
            ResourceMaterializationContract.DefinitionsPath,
            bootstrap.Definitions!.ToCanonicalJson());
        await context.WriteExactJsonAsync(
            ResourceMaterializationContract.StatePath,
            bootstrap.State!.ToCanonicalJson());
        await context.WriteExactJsonAsync(
            ResourceMaterializationContract.HistoryPath,
            bootstrap.History!.ToCanonicalJson());

        var issues = await context.Validator
            .ValidateAcceptedTurnCanonicalResourceMaterializationAsync();

        Assert.Contains(
            issues,
            issue => issue.Code == "resource_owner_authority_root_stale");
        Assert.False(context.FileSystem.FileExists(
            CanonicalResourceOwnerAuthorityComposer.AuthorityPath));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RawValidation_MissingOrStaleAuthorityCachesNoPlanAndCannotSelfHeal(
        bool staleInsteadOfMissing)
    {
        await using var context = await ResourceMaterializationTestContext.CreateAsync();
        await SeedEmptyRootsAsync(context);
        await context.CaptureValidatedPendingSnapshotAsync();
        if (staleInsteadOfMissing)
        {
            await context.WriteExactJsonAsync(
                CanonicalResourceOwnerAuthorityComposer.AuthorityPath,
                "{\"schemaVersion\":1,\"historicalOwners\":[{\"realm\":\"mortal_world\",\"ownerKind\":\"player\",\"resourceOwnerId\":\"forged\"}],\"capacityDrafts\":[]}");
        }
        else
        {
            await context.DeleteAsync(
                CanonicalResourceOwnerAuthorityComposer.AuthorityPath);
        }
        var before = await context.CaptureAsync(
            ResourceMaterializationTestContext.AllResourcePaths);

        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();

        Assert.Contains(
            issues,
            issue => issue.Code ==
                     "resource_materialization_direct_owner_authority_mutation");
        Assert.Null(await AcceptedMechanicsAuthorityTestProbe.PeekCommonAsync(
            context.FileSystem));
        await using var writeLease = await context.FileSystem
            .AcquireCanonicalWriteLeaseAsync();
        Assert.Null(await context.Normalizer.BindTo(writeLease)
            .NormalizeAcceptedMechanicsAsync(backups: null));
        await context.AssertUnchangedAsync(before);
    }

    [Fact]
    public async Task RequiredStateValidation_MissingUnifiedResourceRootsIsIncompatible()
    {
        await using var context = await ResourceMaterializationTestContext.CreateAsync();
        await context.WriteExactJsonAsync(
            "game_state/meta/soul_state.json",
            new JsonObject
            {
                ["soulName"] = "Technical save without unified resources",
                ["currentRealm"] = "Chaos Sea"
            }.ToJsonString());

        var issues = await context.Validator.ValidateGameStateAsync(
            GameStateValidationPhase.RequiredFields);

        Assert.Contains(issues, issue => issue.Code == "resource_materialization_root_missing");
        Assert.Contains(issues, issue => issue.Code == "resource_state_root_missing");
        Assert.Contains(issues, issue => issue.Code == "resource_history_root_missing");
    }

    [Theory]
    [InlineData("player_gauges")]
    [InlineData("player_deltas")]
    [InlineData("npc_health")]
    [InlineData("vehicle_health")]
    [InlineData("combat_health_and_poise")]
    [InlineData("item_durability")]
    [InlineData("item_resource_sidecar")]
    [InlineData("afterlife_action_economy")]
    [InlineData("guardian_gacha_counters")]
    [InlineData("shining_gacha_counters")]
    [InlineData("blessing_reroll_mirrors")]
    public async Task OldTechnicalSave_RemovedResourceAuthorityFailsClosedWithoutMigration(
        string scenario)
    {
        await using var context = await ResourceMaterializationTestContext.CreateAsync();
        await SeedEmptyRootsAsync(context);
        var testCase = await SeedOldTechnicalSaveScenarioAsync(context, scenario);
        var before = await context.CaptureAsync(
            ResourceMaterializationTestContext.AllResourcePaths.Concat(testCase.Paths));

        IReadOnlyList<ValidationIssue> issues = testCase.UseCanonicalResourceValidation
            ? await context.Validator
                .ValidateAcceptedTurnCanonicalResourceMaterializationAsync()
            : await context.Validator.ValidateGameStateAsync(testCase.Phase!.Value);

        foreach (var expectation in testCase.Expectations)
        {
            Assert.Contains(issues, issue =>
                issue.Code == expectation.Code &&
                issue.FilePath.EndsWith(expectation.PathSuffix, StringComparison.Ordinal));
        }
        await context.AssertUnchangedAsync(before);
    }

    [Fact]
    public void MortalBootstrap_UsesPermanentCharacteristicsForThreeInitialResources()
    {
        var result = ResourceBootstrapStateBuilder.BuildMortalPlayer(
            incarnationNumber: 3,
            turn: 42,
            permanentStrength: 10,
            permanentConstitution: 20,
            permanentIntelligence: 30,
            permanentWisdom: 40,
            permanentFaith: 50);

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Issues));
        Assert.NotNull(result.Definitions);
        Assert.NotNull(result.State);
        Assert.NotNull(result.History);
        var entries = result.State!.Entries.ToDictionary(
            static entry => entry.Coordinate.ResourceKey,
            StringComparer.Ordinal);
        Assert.Equal(3, entries.Count);
        Assert.Equal((150m, 150m), (entries["health"].Current, entries["health"].Maximum));
        Assert.Equal((204m, 204m), (entries["energy"].Current, entries["energy"].Maximum));
        Assert.Equal((250m, 250m), (entries["poise"].Current, entries["poise"].Maximum));
        Assert.All(entries.Values, entry =>
        {
            Assert.Equal("mortal_world", entry.Coordinate.Realm);
            Assert.Equal(ResourceOwnerKind.Player, entry.Coordinate.OwnerKind);
            Assert.Equal("player_current", entry.Coordinate.ResourceOwnerId);
            Assert.Equal(ResourceLifecycleState.Active, entry.State);
            Assert.Equal(42, entry.Chronology.CreatedAtTurn);
        });
        Assert.Equal(3, result.History!.Transitions.Count);
        Assert.Empty(result.History.ValidateStateAgreement(result.State));
    }

    [Fact]
    public void MortalBootstrap_PreservesExistingSealedDefinitionsAndLedgerAuthority()
    {
        var pristine = ResourceBootstrapStateBuilder.BuildPristine();
        Assert.True(pristine.IsValid, string.Join(Environment.NewLine, pristine.Issues));
        var proposal = DefinitionCreationCommand()["resourceDefinitionCreations"]![0]![
            "definition"]!.AsObject();
        using var proposalDocument = JsonDocument.Parse(proposal.ToJsonString());
        var materialized = ResourceDefinitionCatalog.MaterializeProposal(
            proposalDocument.RootElement,
            pristine.Definitions!,
            createdAtTurn: 1,
            createdEventRef: "turn_1:resource:1",
            static () => new ResourceDefinitionIdentity(
                "resource_definition_preserved",
                "resource_definition_seal_preserved"));
        Assert.True(materialized.IsValid, string.Join(Environment.NewLine, materialized.Issues));
        var existingDefinitions = pristine.Definitions!.With(materialized.Definition!);

        var result = ResourceBootstrapStateBuilder.BuildMortalPlayer(
            existingDefinitions,
            pristine.State!,
            pristine.History!,
            incarnationNumber: 3,
            turn: 42,
            permanentStrength: 10,
            permanentConstitution: 20,
            permanentIntelligence: 30,
            permanentWisdom: 40,
            permanentFaith: 50);

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Issues));
        Assert.True(result.Definitions!.TryResolveExact("mana", out _));
        Assert.Equal(existingDefinitions.Definitions.Count, result.Definitions.Definitions.Count);
        Assert.Equal(3, result.State!.Entries.Count);
        Assert.Equal(3, result.History!.Transitions.Count);
    }

    [Fact]
    public async Task RawAndCanonicalValidation_SelfConsistentUnknownOwnerFailsClosed()
    {
        await using var context = await ResourceMaterializationTestContext.CreateAsync();
        var result = ResourceBootstrapStateBuilder.BuildMortalPlayer(
            incarnationNumber: 3,
            turn: 42,
            permanentStrength: 10,
            permanentConstitution: 20,
            permanentIntelligence: 30,
            permanentWisdom: 40,
            permanentFaith: 50);
        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Issues));

        var definitions = JsonNode.Parse(result.Definitions!.ToCanonicalJson())!.AsObject();
        var state = JsonNode.Parse(result.State!.ToCanonicalJson())!.AsObject();
        var history = JsonNode.Parse(result.History!.ToCanonicalJson())!.AsObject();
        foreach (var entry in state["entries"]!.AsArray())
            entry!["resourceOwnerId"] = "player_forged";
        foreach (var transition in history["entries"]!.AsArray())
            transition!["coordinate"]!["resourceOwnerId"] = "player_forged";

        var forgedState = ResourceStateContract.ParseCanonical(
            state.ToJsonString(),
            result.Definitions,
            allowMissingPristine: false);
        var forgedHistory = ResourceHistoryState.ParseCanonical(
            history.ToJsonString(),
            result.Definitions,
            allowMissingPristine: false);
        Assert.NotNull(forgedState.Ledger);
        Assert.NotNull(forgedHistory.History);
        var ownerAuthority = await CanonicalResourceOwnerAuthorityComposer.ComposeAsync(
            result.Definitions,
            context.FileSystem.ReadFileAsync,
            forgedState.Ledger,
            forgedHistory.History,
            CanonicalResourceOwnerAuthorityPurpose.ExplicitBootstrap);
        Assert.True(
            ownerAuthority.IsValid &&
            !string.IsNullOrWhiteSpace(ownerAuthority.CanonicalAuthorityJson),
            string.Join(Environment.NewLine, ownerAuthority.Issues.Select(issue =>
                $"{issue.Code}: {issue.FilePath}; expected={issue.Expected}; actual={issue.Actual}")));

        await context.WriteExactJsonAsync(
            ResourceMaterializationTestContext.DefinitionsPath,
            definitions.ToJsonString());
        await context.WriteExactJsonAsync(
            ResourceMaterializationTestContext.StatePath,
            state.ToJsonString());
        await context.WriteExactJsonAsync(
            ResourceMaterializationTestContext.HistoryPath,
            history.ToJsonString());
        await context.WriteExactJsonAsync(
            CanonicalResourceOwnerAuthorityComposer.AuthorityPath,
            ownerAuthority.CanonicalAuthorityJson!);
        await context.CaptureValidatedPendingSnapshotAsync();

        var rawIssues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();

        var canonicalIssues = await context.Validator
            .ValidateAcceptedTurnCanonicalResourceMaterializationAsync();

        Assert.Contains(rawIssues, issue => issue.Code == "resource_owner_unresolved");
        Assert.Contains(canonicalIssues, issue => issue.Code == "resource_owner_unresolved");
    }

    [Theory]
    [InlineData(ResourceMaterializationTestContext.DefinitionsPath, "null")]
    [InlineData(ResourceMaterializationTestContext.StatePath, "[]")]
    [InlineData(ResourceMaterializationTestContext.HistoryPath, " ")]
    public async Task RawValidation_PresentInvalidResourceRootFailsClosed(
        string path,
        string invalidJson)
    {
        await using var context = await ResourceMaterializationTestContext.CreateAsync();
        await SeedEmptyRootsAsync(context);
        await context.CaptureValidatedPendingSnapshotAsync();
        await context.WriteExactJsonAsync(path, invalidJson);

        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Severity == IssueSeverity.Error &&
            issue.FilePath.StartsWith(path, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(
        ResourceMaterializationTestContext.DefinitionsPath,
        "{ \"definitions\": [], \"schemaVersion\": 1 }",
        "resource_materialization_direct_definition_mutation")]
    [InlineData(
        ResourceMaterializationTestContext.StatePath,
        "{ \"entries\": [], \"schemaVersion\": 1 }",
        "resource_materialization_direct_state_mutation")]
    [InlineData(
        ResourceMaterializationTestContext.HistoryPath,
        "{ \"entries\": [], \"schemaVersion\": 1 }",
        "resource_materialization_direct_history_mutation")]
    public async Task RawValidation_SemanticallyEquivalentDirectRootRewriteFailsClosedWithoutWrites(
        string path,
        string rewrittenJson,
        string expectedIssueCode)
    {
        await using var context = await ResourceMaterializationTestContext.CreateAsync();
        await SeedEmptyRootsAsync(context);
        await context.CaptureValidatedPendingSnapshotAsync();
        await context.WriteExactJsonAsync(
            path,
            rewrittenJson);
        var before = await context.CaptureAsync(ResourceMaterializationTestContext.AllResourcePaths);

        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == expectedIssueCode);
        await context.AssertUnchangedAsync(before);
    }

    [Fact]
    public async Task RawValidation_ByteOrderMarkOnlyRewriteFailsExactContinuity()
    {
        await using var context = await ResourceMaterializationTestContext.CreateAsync();
        await SeedEmptyRootsAsync(context);
        await context.CaptureValidatedPendingSnapshotAsync();
        var original = await context.FileSystem.ReadFileBytesAsync(
            ResourceMaterializationTestContext.StatePath);
        Assert.NotNull(original);
        await context.WriteExactBytesAsync(
            ResourceMaterializationTestContext.StatePath,
            new byte[] { 0xEF, 0xBB, 0xBF }.Concat(original!).ToArray());

        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "resource_materialization_direct_state_mutation");
    }

    [Fact]
    public async Task RawValidation_DefinitionCreationBuildsOneValidatedPlanWithoutWriting()
    {
        await using var context = await ResourceMaterializationTestContext.CreateAsync();
        await SeedEmptyRootsAsync(context);
        await context.CaptureValidatedPendingSnapshotAsync();
        await context.WriteExactJsonAsync(
            ResourceMaterializationTestContext.CommandsPath,
            DefinitionCreationCommand().ToJsonString());
        var before = await context.CaptureAsync(ResourceMaterializationTestContext.AllResourcePaths);

        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();

        Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);
        Assert.True(await AcceptedMechanicsAuthorityTestProbe.HasCommonAsync(
            context.FileSystem));
        var manifest = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            "game_state/control/pending_turn_snapshot.json"));
        var snapshotToken = manifest["manifestPayloadHash"]!.GetValue<string>();
        var validatedHandoff = await AcceptedMechanicsAuthorityTestProbe
            .PeekCommonAsync(context.FileSystem);
        Assert.NotNull(validatedHandoff);
        var binding = validatedHandoff.Binding;
        Assert.Equal(snapshotToken, binding.SnapshotToken);
        await context.AssertUnchangedAsync(before);
    }

    [Fact]
    public async Task RawValidation_SameTurnDefinitionInitializationBuildsValidatedStatePlanWithoutWriting()
    {
        await using var context = await ResourceMaterializationTestContext.CreateAsync();
        await SeedEmptyRootsAsync(context);
        await context.CaptureValidatedPendingSnapshotAsync();
        await context.WriteExactJsonAsync(
            ResourceMaterializationTestContext.CommandsPath,
            DefinitionAndInitializationCommand().ToJsonString());
        var before = await context.CaptureAsync(ResourceMaterializationTestContext.AllResourcePaths);

        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();

        Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);
        Assert.True(await AcceptedMechanicsAuthorityTestProbe.HasCommonAsync(
            context.FileSystem));
        await context.AssertUnchangedAsync(before);
    }

    [Fact]
    public async Task RawValidation_OrdinaryMutationCannotSubmitClientDerivedSourceId()
    {
        await using var context = await ResourceMaterializationTestContext.CreateAsync();
        await SeedEmptyRootsAsync(context);
        await context.CaptureValidatedPendingSnapshotAsync();
        var command = DefinitionAndInitializationCommand();
        command["resourceChanges"] = new JsonArray(new JsonObject
        {
            ["operation"] = "spend",
            ["target"] = new JsonObject
            {
                ["kind"] = "player",
                ["targetId"] = "player_current"
            },
            ["resourceKey"] = "mana",
            ["amount"] = 3,
            ["source"] = new JsonObject
            {
                ["kind"] = "narrative_outcome",
                ["sourceId"] = "unvalidated_exchange"
            },
            ["eventRef"] = "turn_42:resource:3",
            ["reason"] = "Untrusted source must not become authority"
        });
        await context.WriteExactJsonAsync(
            ResourceMaterializationTestContext.CommandsPath,
            command.ToJsonString());
        var before = await context.CaptureAsync(ResourceMaterializationTestContext.AllResourcePaths);

        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "resource_command_unknown_field" &&
            issue.FilePath.Contains("source", StringComparison.Ordinal));
        Assert.False(await AcceptedMechanicsAuthorityTestProbe.HasCommonAsync(
            context.FileSystem));
        await context.AssertUnchangedAsync(before);
    }

    [Fact]
    public async Task RawValidation_ClientDerivedOrdinarySourceBuildsTargetBoundValidatedPlan()
    {
        await using var context = await ResourceMaterializationTestContext.CreateAsync();
        await SeedEmptyRootsAsync(context);
        await context.CaptureValidatedPendingSnapshotAsync();
        var command = DefinitionAndInitializationCommand();
        command["resourceChanges"] = new JsonArray(new JsonObject
        {
            ["operation"] = "spend",
            ["target"] = new JsonObject
            {
                ["kind"] = "player",
                ["targetId"] = "player_current"
            },
            ["resourceKey"] = "mana",
            ["amount"] = 3,
            ["source"] = new JsonObject
            {
                ["kind"] = "narrative_outcome"
            },
            ["eventRef"] = "turn_42:resource:3",
            ["reason"] = "Accepted authored outcome"
        });
        await context.WriteExactJsonAsync(
            ResourceMaterializationTestContext.CommandsPath,
            command.ToJsonString());
        var before = await context.CaptureAsync(ResourceMaterializationTestContext.AllResourcePaths);

        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();

        Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);
        Assert.True(await AcceptedMechanicsAuthorityTestProbe.HasCommonAsync(
            context.FileSystem));
        await context.AssertUnchangedAsync(before);
    }

    [Fact]
    public async Task RawValidation_LegacyCurrentFieldInCapacityCommandFailsClosed()
    {
        await using var context = await ResourceMaterializationTestContext.CreateAsync();
        await SeedEmptyRootsAsync(context);
        await context.CaptureValidatedPendingSnapshotAsync();
        var command = DefinitionAndInitializationCommand();
        command["resourceCapacityChanges"]![0]!["current"] = 40;
        await context.WriteExactJsonAsync(
            ResourceMaterializationTestContext.CommandsPath,
            command.ToJsonString());

        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();

        Assert.Contains(issues, issue => issue.Code == "resource_command_unknown_field");
        Assert.False(await AcceptedMechanicsAuthorityTestProbe.HasCommonAsync(
            context.FileSystem));
    }

    [Fact]
    public async Task RawValidation_UnknownSameTurnOwnerRefFailsClosed()
    {
        await using var context = await ResourceMaterializationTestContext.CreateAsync();
        await SeedEmptyRootsAsync(context);
        await context.CaptureValidatedPendingSnapshotAsync();
        var command = DefinitionAndInitializationCommand();
        var target = command["resourceCapacityChanges"]![0]!["target"]!.AsObject();
        target.Remove("targetId");
        target["targetRef"] = "new_player_ref";
        await context.WriteExactJsonAsync(
            ResourceMaterializationTestContext.CommandsPath,
            command.ToJsonString());

        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();

        Assert.Contains(issues, issue => issue.Code == "resource_owner_unresolved");
        Assert.False(await AcceptedMechanicsAuthorityTestProbe.HasCommonAsync(
            context.FileSystem));
    }

    [Fact]
    public async Task RawValidation_SameTurnDefinitionDoesNotGrantUnlistedPlayerCapability()
    {
        await using var context = await ResourceMaterializationTestContext.CreateAsync();
        await SeedEmptyRootsAsync(context);
        await context.CaptureValidatedPendingSnapshotAsync();
        var command = DefinitionAndInitializationCommand();
        command["resourceDefinitionCreations"]![0]!["definition"]![
            "allowedOwnerKinds"] = new JsonArray("item");
        await context.WriteExactJsonAsync(
            ResourceMaterializationTestContext.CommandsPath,
            command.ToJsonString());

        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();

        Assert.Contains(issues, issue => issue.Code == "resource_owner_capability_missing");
        Assert.False(await AcceptedMechanicsAuthorityTestProbe.HasCommonAsync(
            context.FileSystem));
    }

    [Fact]
    public async Task RawValidation_MortalOwnerInChaosSeaTurnFailsWrongRealm()
    {
        await using var context = await ResourceMaterializationTestContext.CreateAsync();
        await SeedEmptyRootsAsync(context);
        await context.CaptureValidatedPendingSnapshotAsync(
            currentRealm: "Chaos Sea");
        await context.WriteExactJsonAsync(
            ResourceMaterializationTestContext.CommandsPath,
            DefinitionAndInitializationCommand().ToJsonString());

        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();

        Assert.Contains(issues, issue => issue.Code == "resource_owner_selector_invalid");
        Assert.False(await AcceptedMechanicsAuthorityTestProbe.HasCommonAsync(
            context.FileSystem));
    }

    private static async Task<OldTechnicalSaveScenario> SeedOldTechnicalSaveScenarioAsync(
        ResourceMaterializationTestContext context,
        string scenario)
    {
        switch (scenario)
        {
            case "player_gauges":
            {
                const string path = "game_state/core/player_status.json";
                await context.WriteExactJsonAsync(
                    path,
                    new JsonObject
                    {
                        ["healthPercentage"] = "75%",
                        ["energyPercentage"] = "60%",
                        ["poisePercentage"] = "45%",
                        ["currentCondition"] = "Устал",
                        ["activeConditions"] = new JsonArray(),
                        ["money"] = 0
                    }.ToJsonString());
                return GameStateCase(
                    GameStateValidationPhase.PlayerStateFiles,
                    path,
                    Legacy("resource_legacy_player_gauge_forbidden", ".healthPercentage"),
                    Legacy("resource_legacy_player_gauge_forbidden", ".energyPercentage"),
                    Legacy("resource_legacy_player_gauge_forbidden", ".poisePercentage"));
            }
            case "player_deltas":
            {
                const string path = "game_state/player/status_changes.json";
                await context.WriteExactJsonAsync(
                    path,
                    new JsonObject
                    {
                        ["currentHealthChange"] = -7,
                        ["currentEnergyChange"] = -5,
                        ["currentPoiseChange"] = -3
                    }.ToJsonString());
                return GameStateCase(
                    GameStateValidationPhase.PlayerStateFiles,
                    path,
                    Legacy("resource_legacy_player_gauge_forbidden", ".currentHealthChange"),
                    Legacy("resource_legacy_player_gauge_forbidden", ".currentEnergyChange"),
                    Legacy("resource_legacy_player_gauge_forbidden", ".currentPoiseChange"));
            }
            case "npc_health":
            {
                const string path = "game_state/npcs/npc_core.json";
                var root = MortalActorTestFixtures.CreateNpcCoreRoot();
                var npc = root["NPCsInScene"]!.AsArray()[0]!.AsObject();
                npc["currentHealthPercentage"] = "35%";
                npc["maxHealthPercentage"] = "100%";
                await context.WriteExactJsonAsync(path, root.ToJsonString());
                return GameStateCase(
                    GameStateValidationPhase.NpcStateFiles,
                    path,
                    Legacy("resource_owner_legacy_value_forbidden", ".currentHealthPercentage"),
                    Legacy("resource_owner_legacy_value_forbidden", ".maxHealthPercentage"));
            }
            case "vehicle_health":
            {
                var path = StorageTransportMoveService.VehiclesPath;
                var vehicle = CreateLegacyVehicle();
                vehicle["currentHealth"] = "40%";
                vehicle["maxHealth"] = "100%";
                await context.WriteExactJsonAsync(
                    path,
                    new JsonObject
                    {
                        ["vehicles"] = new JsonArray(vehicle)
                    }.ToJsonString());
                return GameStateCase(
                    GameStateValidationPhase.MetaMiscStateFiles,
                    path,
                    Legacy("resource_owner_legacy_value_forbidden", ".currentHealth"),
                    Legacy("resource_owner_legacy_value_forbidden", ".maxHealth"));
            }
            case "combat_health_and_poise":
            {
                var path = EffectCarrierCatalog.EnemiesPath;
                var combatant = CreateLegacyCombatant();
                combatant["currentHealth"] = "50%";
                combatant["maxHealth"] = "100%";
                combatant["currentPoise"] = "25%";
                combatant["maxPoise"] = "100%";
                var group = CreateLegacyCombatGroup();
                group["healthStates"] = new JsonArray(
                    new JsonObject
                    {
                        ["name"] = "Разведчик",
                        ["currentHealth"] = "30%"
                    });
                await context.WriteExactJsonAsync(
                    path,
                    new JsonObject
                    {
                        ["enemiesData"] = new JsonArray(combatant, group)
                    }.ToJsonString());
                return GameStateCase(
                    GameStateValidationPhase.WorldQuestCombatFactionStateFiles,
                    path,
                    Legacy("resource_owner_legacy_value_forbidden", ".currentHealth"),
                    Legacy("resource_owner_legacy_value_forbidden", ".maxHealth"),
                    Legacy("resource_owner_legacy_value_forbidden", ".currentPoise"),
                    Legacy("resource_owner_legacy_value_forbidden", ".maxPoise"),
                    Legacy("resource_owner_legacy_value_forbidden", ".healthStates"));
            }
            case "item_durability":
            {
                const string path = "game_state/inventory/items.json";
                var item = MortalItemTestFixture.CreateCanonicalRoot(
                    "itm_legacy_resource_authority");
                item["durability"] = 12;
                item["maxDurability"] = 20;
                MortalItemTestFixture.ResealCanonical(item);
                await context.WriteExactJsonAsync(
                    path,
                    new JsonObject
                    {
                        ["items"] = new JsonArray(item)
                    }.ToJsonString());
                return GameStateCase(
                    GameStateValidationPhase.PlayerStateFiles,
                    path,
                    Legacy("resource_owner_legacy_value_forbidden", ".durability"),
                    Legacy("resource_owner_legacy_value_forbidden", ".maxDurability"));
            }
            case "item_resource_sidecar":
            {
                const string path = "game_state/inventory/item_resources.json";
                await context.WriteExactJsonAsync(
                    path,
                    new JsonObject
                    {
                        ["items"] = new JsonArray(
                            new JsonObject
                            {
                                ["itemId"] = "itm_legacy_sidecar",
                                ["resourceType"] = "durability",
                                ["current"] = 3,
                                ["maximum"] = 10
                            })
                    }.ToJsonString());
                return GameStateCase(
                    GameStateValidationPhase.PlayerStateFiles,
                    path,
                    Legacy("resource_legacy_item_authority_forbidden", path));
            }
            case "afterlife_action_economy":
            {
                var path = AfterlifeSpiritualConflictState.StatePath;
                var conflict = CreateLegacySpiritualConflict();
                conflict["actionEconomy"] = new JsonObject
                {
                    ["player"] = new JsonObject
                    {
                        ["current"] = 2,
                        ["max"] = 5
                    },
                    ["opposition"] = new JsonObject
                    {
                        ["current"] = 3,
                        ["max"] = 6
                    }
                };
                await context.WriteExactJsonAsync(
                    path,
                    new JsonObject
                    {
                        ["schemaVersion"] = 1,
                        ["activeConflict"] = conflict,
                        ["recentConflicts"] = new JsonArray()
                    }.ToJsonString());
                return GameStateCase(
                    GameStateValidationPhase.AfterlifeSpiritualConflictState,
                    path,
                    Legacy("afterlife_conflict_legacy_action_economy_forbidden", ".actionEconomy"));
            }
            case "guardian_gacha_counters":
            {
                const string path = "game_state/meta/guardians.json";
                await context.WriteExactJsonAsync(
                    path,
                    new JsonObject
                    {
                        ["guardians"] = new JsonArray(
                            new JsonObject
                            {
                                ["guardianId"] = "guardian_legacy_gacha",
                                ["gachaSystem"] = new JsonObject
                                {
                                    ["currentReturnCycleId"] = "chaos_return_legacy",
                                    ["chargesPerReturn"] = 3,
                                    ["chargesUsedThisReturn"] = 1,
                                    ["gachaHistory"] = new JsonArray()
                                }
                            })
                    }.ToJsonString());
                return CanonicalResourceCase(
                    path,
                    Legacy(
                        "resource_owner_guardian_legacy_gacha_counters_forbidden",
                        ".gachaSystem"));
            }
            case "shining_gacha_counters":
            {
                var path = ShiningAbodeState.StatePath;
                var shining = ShiningAbodeState.CreateDefaultState();
                var gacha = shining["gachaSystem"]!.AsObject();
                gacha["chargesPerReturn"] = 4;
                gacha["chargesUsedThisReturn"] = 2;
                await context.WriteExactJsonAsync(path, shining.ToJsonString());
                return GameStateCase(
                    GameStateValidationPhase.MetaMiscStateFiles,
                    path,
                    Legacy("shining_gacha_legacy_counter_forbidden", ".chargesPerReturn"),
                    Legacy("shining_gacha_legacy_counter_forbidden", ".chargesUsedThisReturn"));
            }
            case "blessing_reroll_mirrors":
            {
                const string path = "game_state/meta/soul_state.json";
                await context.WriteExactJsonAsync(
                    path,
                    CreateLegacyBlessingSoulState().ToJsonString());
                return GameStateCase(
                    GameStateValidationPhase.MetaMiscStateFiles,
                    path,
                    Legacy(
                        "pending_shining_blessings_numeric_reroll_mirror_forbidden",
                        ".memorySelection.rerolls"),
                    Legacy(
                        "pending_shining_blessings_numeric_reroll_mirror_forbidden",
                        ".relicRefinementEntitlements.rerollsSpent"));
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null);
        }
    }

    private static OldTechnicalSaveScenario GameStateCase(
        GameStateValidationPhase phase,
        string path,
        params OldTechnicalSaveExpectation[] expectations) =>
        new(phase, false, new[] { path }, expectations);

    private static OldTechnicalSaveScenario CanonicalResourceCase(
        string path,
        params OldTechnicalSaveExpectation[] expectations) =>
        new(null, true, new[] { path }, expectations);

    private static OldTechnicalSaveExpectation Legacy(string code, string pathSuffix) =>
        new(code, pathSuffix);

    private static JsonObject CreateLegacyVehicle() =>
        new()
        {
            ["vehicleId"] = "vehicle_legacy_health",
            ["name"] = "Старая телега",
            ["description"] = "Технический несовместимый транспорт.",
            ["image_prompt"] = "old wooden cart",
            ["type"] = "Vehicle",
            ["isSentient"] = false,
            ["availability"] = "Parked",
            ["currentLocationId"] = "location_legacy",
            ["speedBonus"] = 0,
            ["actions"] = new JsonArray(),
            ["resistances"] = new JsonArray(),
            ["inventory"] = new JsonArray()
        };

    private static JsonObject CreateLegacyCombatant() =>
        new()
        {
            ["NPCId"] = null,
            ["combatantId"] = "combatant_legacy_health",
            ["name"] = "Старый противник",
            ["image_prompt"] = "dark fantasy raider",
            ["description"] = "Технический несовместимый противник.",
            ["type"] = "individual",
            ["isGroup"] = false,
            ["initiative"] = 10,
            ["actions"] = new JsonArray(),
            ["resistances"] = new JsonArray(),
            ["activeBuffs"] = new JsonArray(),
            ["activeDebuffs"] = new JsonArray()
        };

    private static JsonObject CreateLegacyCombatGroup() =>
        new()
        {
            ["NPCId"] = null,
            ["name"] = "Старый дозор",
            ["image_prompt"] = "dark fantasy road watch",
            ["description"] = "Техническая несовместимая группа.",
            ["type"] = "group",
            ["isGroup"] = true,
            ["initiative"] = 12,
            ["actions"] = new JsonArray(),
            ["resistances"] = new JsonArray(),
            ["activeBuffs"] = new JsonArray(),
            ["activeDebuffs"] = new JsonArray(),
            ["count"] = 1,
            ["unitName"] = "дозорный",
            ["members"] = new JsonArray(
                new JsonObject
                {
                    ["memberId"] = "member_legacy_scout",
                    ["name"] = "Разведчик"
                })
        };

    private static JsonObject CreateLegacySpiritualConflict() =>
        new()
        {
            ["conflictId"] = "conflict_legacy_action_economy",
            ["realm"] = "Chaos Sea",
            ["status"] = "active",
            ["resolutionState"] = "active",
            ["playerSide"] = new JsonObject(),
            ["oppositionSide"] = new JsonObject(),
            ["exchangeLog"] = new JsonArray(),
            ["combatConditions"] = new JsonArray(),
            ["resourceOwnerBindings"] = new JsonObject
            {
                ["opposition"] = new JsonObject
                {
                    ["resourceOwnerId"] = "afterlife_conflict_side_legacy"
                }
            }
        };

    private static JsonObject CreateLegacyBlessingSoulState() =>
        new()
        {
            ["soulName"] = "Legacy Soul",
            ["currentRealm"] = "Mortal World",
            ["currentIncarnation"] = 3,
            [ShiningBlessingEffectState.SoulStateProperty] = new JsonObject
            {
                ["applicationState"] = ShiningBlessingEffectState.ApplicationStateActive,
                ["materializedAtUtc"] = "2026-08-22T00:00:00Z",
                ["sourcePackagePreparedAtTurn"] = 2,
                ["currentIncarnation"] = 3,
                ["sourceCardCount"] = 1,
                ["sourceCardIds"] = new JsonArray("card_legacy_rerolls"),
                ["memorySelection"] = new JsonObject
                {
                    ["options"] = 1,
                    ["rerolls"] = 2,
                    ["status"] = ShiningBlessingEffectState.MemoryStatusPendingPreTurnOneSelection,
                    ["sourceCardIds"] = new JsonArray("card_legacy_rerolls")
                },
                ["relicRefinementEntitlements"] = new JsonObject
                {
                    ["rerollsSpent"] = 1,
                    ["freeShape"] = true,
                    ["freeRetune"] = false,
                    ["status"] = ShiningBlessingEffectState.RelicStatusPendingEntitlement,
                    ["sourceCardIds"] = new JsonArray("card_legacy_rerolls")
                }
            }
        };

    private sealed record OldTechnicalSaveExpectation(
        string Code,
        string PathSuffix);

    private sealed record OldTechnicalSaveScenario(
        GameStateValidationPhase? Phase,
        bool UseCanonicalResourceValidation,
        IReadOnlyList<string> Paths,
        IReadOnlyList<OldTechnicalSaveExpectation> Expectations);

    internal static async Task SeedEmptyRootsAsync(ResourceMaterializationTestContext context)
    {
        foreach (var pair in ResourceMaterializationTestContext.CreateEmptyResourceRoots())
        {
            if (string.Equals(
                    pair.Key,
                    ResourceMaterializationTestContext.CommandsPath,
                    StringComparison.Ordinal))
            {
                continue;
            }
            await context.WriteExactJsonAsync(pair.Key, pair.Value.ToJsonString());
        }
    }

    internal static JsonObject DefinitionCreationCommand() => new()
    {
        ["resourceDefinitionCreations"] = new JsonArray(new JsonObject
        {
            ["definitionRef"] = "mana_v1",
            ["definition"] = new JsonObject
            {
                ["resourceKey"] = "mana",
                ["definitionVersion"] = 1,
                ["displayName"] = "Мана",
                ["numericKind"] = "integer",
                ["unit"] = "point",
                ["quantum"] = 1,
                ["minimumPolicy"] = new JsonObject
                {
                    ["kind"] = "definition_fixed",
                    ["value"] = 0
                },
                ["capacityPolicy"] = new JsonObject { ["kind"] = "instance_fixed" },
                ["initializationPolicy"] = new JsonObject { ["kind"] = "maximum" },
                ["allowedOwnerKinds"] = new JsonArray("player"),
                ["allowedOperations"] = new JsonArray("spend", "gain"),
                ["defaultFloorPolicy"] = "reject_below_minimum",
                ["defaultCapPolicy"] = "clamp_to_maximum",
                ["visibility"] = "player_visible"
            },
            ["eventRef"] = "turn_42:resource:1",
            ["reason"] = "Setting materialization"
        }),
        ["resourceCapacityChanges"] = new JsonArray(),
        ["resourceChanges"] = new JsonArray()
    };

    internal static JsonObject DefinitionAndInitializationCommand()
    {
        var root = DefinitionCreationCommand();
        root["resourceCapacityChanges"] = new JsonArray(new JsonObject
        {
            ["operation"] = "initialize",
            ["target"] = new JsonObject
            {
                ["kind"] = "player",
                ["targetId"] = "player_current"
            },
            ["resourceDefinitionRef"] = "mana_v1",
            ["capacity"] = new JsonObject
            {
                ["kind"] = "instance_fixed",
                ["maximum"] = 40
            },
            ["currentDisposition"] = "initialize_from_definition",
            ["source"] = new JsonObject
            {
                ["kind"] = "setting_materialization",
                ["sourceId"] = "mana_v1"
            },
            ["eventRef"] = "turn_42:resource:2",
            ["reason"] = "Initialize accepted setting resource"
        });
        return root;
    }
}
