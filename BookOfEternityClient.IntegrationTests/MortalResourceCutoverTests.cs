using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class MortalResourceCutoverTests
{
    private const string SourceFingerprint =
        "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    [Fact]
    public void MortalBootstrap_PlayerStatusIsNarrativeOnlyAndLedgerOwnsAllThreeGauges()
    {
        var status = MortalBootstrapStateBuilder.BuildFreshPlayerStatus();

        Assert.Equal("Здоров", status["currentCondition"]!.GetValue<string>());
        Assert.Empty(status["activeConditions"]!.AsArray());
        Assert.Equal(0, status["money"]!.GetValue<int>());
        Assert.False(status.ContainsKey("healthPercentage"));
        Assert.False(status.ContainsKey("energyPercentage"));
        Assert.False(status.ContainsKey("poisePercentage"));

        var resources = ResourceBootstrapStateBuilder.BuildMortalPlayer(
            incarnationNumber: 3,
            turn: 42,
            permanentStrength: 10,
            permanentConstitution: 20,
            permanentIntelligence: 30,
            permanentWisdom: 40,
            permanentFaith: 50);

        Assert.True(resources.IsValid, string.Join(Environment.NewLine, resources.Issues));
        Assert.Equal(
            new[] { "energy", "health", "poise" },
            resources.State!.Entries
                .Select(static entry => entry.Coordinate.ResourceKey)
                .OrderBy(static key => key, StringComparer.Ordinal));
    }

    [Fact]
    public async Task PlayerStateValidation_RejectsEveryPersistedLegacyGauge()
    {
        await using var context = await ResourceMaterializationTestContext.CreateAsync();
        await context.WriteExactJsonAsync(
            "game_state/core/player_status.json",
            new JsonObject
            {
                ["healthPercentage"] = "100%",
                ["energyPercentage"] = "100%",
                ["poisePercentage"] = "100%",
                ["currentCondition"] = "Здоров",
                ["activeConditions"] = new JsonArray(),
                ["money"] = 0
            }.ToJsonString());

        var issues = await context.Validator.ValidateGameStateAsync(
            GameStateValidationPhase.PlayerStateFiles);

        Assert.Equal(
            3,
            issues.Count(issue =>
                issue.Code == "resource_legacy_player_gauge_forbidden"));
    }

    [Fact]
    public async Task ResponseValidation_RejectsLegacyGaugeAndDeltaRoutesAndMappingsAreAbsent()
    {
        using var response = JsonDocument.Parse("""
        {
          "response": "Техническая проверка cutover.",
          "playerStatus": {
            "healthPercentage": "90%",
            "energyPercentage": "80%",
            "poisePercentage": "70%",
            "currentCondition": "Устал"
          },
          "currentHealthChange": -10,
          "currentEnergyChange": -5,
          "currentPoiseChange": -3
        }
        """);
        await using var context = await ResourceMaterializationTestContext.CreateAsync();

        var issues = context.Validator.ValidateResponse(response.RootElement);

        Assert.Equal(
            6,
            issues.Count(issue =>
                issue.Code == "resource_legacy_player_gauge_forbidden"));
        Assert.False(FileMapping.FieldToFile.ContainsKey("currentHealthChange"));
        Assert.False(FileMapping.FieldToFile.ContainsKey("currentEnergyChange"));
        Assert.False(FileMapping.FieldToFile.ContainsKey("currentPoiseChange"));
        Assert.Null(typeof(GameResponse).GetProperty("CurrentHealthChange"));
        Assert.Null(typeof(GameResponse).GetProperty("CurrentEnergyChange"));
        Assert.Null(typeof(GameResponse).GetProperty("CurrentPoiseChange"));
    }

    [Fact]
    public async Task ItemResourceCutover_RejectsLegacyCommandsAndSidecarAndRemovesMappings()
    {
        using var response = JsonDocument.Parse("""
        {
          "response": "Техническая проверка item-resource cutover.",
          "inventoryItemsResources": [],
          "NPCInventoryResourcesChanges": []
        }
        """);
        await using var context = await ResourceMaterializationTestContext.CreateAsync();

        var responseIssues = context.Validator.ValidateResponse(response.RootElement);

        Assert.Equal(
            2,
            responseIssues.Count(issue =>
                issue.Code == "resource_legacy_item_authority_forbidden"));
        Assert.False(FileMapping.FieldToFile.ContainsKey("inventoryItemsResources"));
        Assert.False(FileMapping.FieldToFile.ContainsKey("NPCInventoryResourcesChanges"));
        Assert.Null(typeof(GameResponse).GetProperty("InventoryItemsResources"));
        Assert.Null(typeof(GameResponse).GetProperty("NPCInventoryResourcesChanges"));

        var bootstrap = MortalBootstrapStateBuilder.BuildFreshMortalBootstrapFiles(
            incarnationNumber: 1,
            turnNumber: 1,
            characterDescription: "Технический персонаж.",
            worldDescription: "Технический мир.",
            startingCircumstances: "Начало технической проверки.",
            createdAtUtc: DateTimeOffset.Parse("2026-08-16T00:00:00Z"));
        Assert.DoesNotContain("game_state/inventory/item_resources.json", bootstrap.Keys);
        Assert.Contains(ResourceMaterializationContract.DefinitionsPath, bootstrap.Keys);
        Assert.Contains(ResourceMaterializationContract.StatePath, bootstrap.Keys);
        Assert.Contains(ResourceMaterializationContract.HistoryPath, bootstrap.Keys);

        await context.WriteExactJsonAsync(
            "game_state/inventory/item_resources.json",
            new JsonObject { ["entries"] = new JsonArray() }.ToJsonString());

        var stateIssues = await context.Validator.ValidateGameStateAsync(
            GameStateValidationPhase.PlayerStateFiles);

        Assert.Contains(
            stateIssues,
            issue => issue.Code == "resource_legacy_item_authority_forbidden" &&
                     issue.FilePath == "game_state/inventory/item_resources.json");
    }

    [Fact]
    public void OrdinaryPlayerHealthEnergyAndPoiseChangesUseTheCommonResourceReducer()
    {
        var bootstrap = ResourceBootstrapStateBuilder.BuildMortalPlayer(
            incarnationNumber: 3,
            turn: 42,
            permanentStrength: 10,
            permanentConstitution: 20,
            permanentIntelligence: 30,
            permanentWisdom: 40,
            permanentFaith: 50);
        Assert.True(bootstrap.IsValid, string.Join(Environment.NewLine, bootstrap.Issues));
        var sources = ResourceMutationSourceCatalog.Create(
            new[]
            {
                new ResourceMutationSourceExport(
                    "narrative_outcome",
                    "mortal_cutover_outcome",
                    SourceFingerprint,
                    ResourceMutationSourceState.Active,
                    SameTurn: true,
                    BoundOwner: new ResourceOwnerKey(
                        "mortal_world",
                        ResourceOwnerKind.Player,
                        "player_current"))
            });
        Assert.True(sources.IsValid, string.Join(Environment.NewLine, sources.Issues));

        var mutations = new[]
        {
            Mutation("turn_43:resource:1", "health", ResourceOperation.Damage, 10m),
            Mutation("turn_43:resource:2", "energy", ResourceOperation.Spend, 4m),
            Mutation("turn_43:resource:3", "poise", ResourceOperation.Damage, 5m)
        };
        var planned = AcceptedMechanicsPlanner.BuildResources(
            new AcceptedMechanicsResourceInput(
                43,
                bootstrap.Definitions!,
                bootstrap.State!,
                bootstrap.History!,
                sources.Catalog!,
                mutations),
            new AcceptedMechanicsIdentityFactory());

        Assert.True(planned.IsValid, string.Join(Environment.NewLine, planned.Issues));
        var entries = planned.StateAfterImage!.Entries.ToDictionary(
            static entry => entry.Coordinate.ResourceKey,
            StringComparer.Ordinal);
        Assert.Equal(140m, entries["health"].Current);
        Assert.Equal(200m, entries["energy"].Current);
        Assert.Equal(245m, entries["poise"].Current);
        Assert.Empty(planned.HistoryAfterImage!.ValidateStateAgreement(planned.StateAfterImage));
    }

    [Fact]
    public async Task ShiningSurvivalRecovery_UsesRegisteredResourceMutationsAndNeverRestoresStatusPercentages()
    {
        await using var context = await ResourceMaterializationTestContext.CreateAsync();
        var scenario = await PrepareShiningSurvivalScenarioAsync(context);
        var bootstrap = scenario.Bootstrap;

        var rawIssues = await ValidateAcceptedTurnRawMechanicsAsync(context);
        Assert.False(
            rawIssues.Any(issue => issue.Severity == IssueSeverity.Error),
            string.Join(Environment.NewLine, rawIssues.Select(static issue =>
                $"{issue.Code}: {issue.Message} expected={issue.Expected} actual={issue.Actual}")));
        var canonicalIssues = await AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateAsync(
            context.FileSystem,
            context.Normalizer,
            context.Validator,
            scenario.Backups);
        Assert.False(
            canonicalIssues.Any(issue => issue.Severity == IssueSeverity.Error),
            string.Join(Environment.NewLine, canonicalIssues.Select(static issue =>
                $"{issue.Code}: {issue.Message} expected={issue.Expected} actual={issue.Actual}")));

        var stateResult = ResourceStateContract.ParseCanonical(
            await context.FileSystem.ReadFileAsync(ResourceMaterializationTestContext.StatePath),
            bootstrap.Definitions!,
            allowMissingPristine: false);
        var historyResult = ResourceHistoryState.ParseCanonical(
            await context.FileSystem.ReadFileAsync(ResourceMaterializationTestContext.HistoryPath),
            bootstrap.Definitions!,
            allowMissingPristine: false);
        Assert.True(stateResult.IsValid, string.Join(Environment.NewLine, stateResult.Issues));
        Assert.True(historyResult.IsValid, string.Join(Environment.NewLine, historyResult.Issues));
        var entries = stateResult.Ledger!.Entries.ToDictionary(
            static entry => entry.Coordinate.ResourceKey,
            StringComparer.Ordinal);
        Assert.Equal(118m, entries["health"].Current);
        Assert.Equal(172m, entries["energy"].Current);
        Assert.Equal(218m, entries["poise"].Current);
        Assert.Empty(historyResult.History!.ValidateStateAgreement(stateResult.Ledger));
        var turnTransitions = historyResult.History.Transitions
            .Where(static transition => transition.Turn == 5)
            .OrderBy(static transition => transition.ExecutionSequence)
            .ToArray();
        Assert.Equal(6, turnTransitions.Length);
        Assert.Equal(
            new[]
            {
                ResourceMutationPhase.DirectOutcome,
                ResourceMutationPhase.DirectOutcome,
                ResourceMutationPhase.DirectOutcome,
                ResourceMutationPhase.RegisteredSystemOutcome,
                ResourceMutationPhase.RegisteredSystemOutcome,
                ResourceMutationPhase.RegisteredSystemOutcome
            },
            turnTransitions.Select(static transition => transition.Phase));
        Assert.Equal(Enumerable.Range(0, 6), turnTransitions.Select(static transition => transition.ExecutionSequence));

        var status = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            "game_state/core/player_status.json"));
        Assert.False(status.ContainsKey("healthPercentage"));
        Assert.False(status.ContainsKey("energyPercentage"));
        Assert.False(status.ContainsKey("poisePercentage"));
        var soul = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            "game_state/meta/soul_state.json"));
        var survival = soul[ShiningBlessingEffectState.SoulStateProperty]![
            "pendingSurvivalEffects"]![0]!.AsObject();
        Assert.Equal(ShiningBlessingEffectState.GenericStatusConsumed, survival["status"]!.GetValue<string>());
        Assert.True(JsonNode.DeepEquals(
            new JsonObject
            {
                ["energy"] = 8,
                ["health"] = 8,
                ["poise"] = 8
            },
            survival["restoredResourceAmounts"]));
        var world = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            "game_state/world/world_events.json"));
        Assert.Equal(
            "severe",
            world["events"]![0]!["severity"]!.GetValue<string>());
        Assert.Null(await context.ReadJsonAsync(ResourceMaterializationTestContext.CommandsPath));

        var afterPublication = await context.CaptureAsync(
            ResourceMaterializationTestContext.AllResourcePaths.Concat(
                new[]
                {
                    "game_state/core/player_status.json",
                    "game_state/meta/soul_state.json",
                    "game_state/world/world_events.json"
                }));
        var runtimeResult = await ShiningBlessingEffectState.ApplyAcceptedTurnRuntimeEffectsAsync(
            context.FileSystem,
            currentTurnNumber: 5,
            preTurnShiningJson: null,
            preTurnNpcCoreJson: null,
            preTurnWorldEventsJson: scenario.PreTurnWorldEventsJson,
            preTurnNpcRelationshipsJson: null,
            preTurnFactionCoreJson: null);

        Assert.True(runtimeResult.Success, runtimeResult.ErrorMessage);
        Assert.False(runtimeResult.StateChanged);
        Assert.Empty(runtimeResult.SummaryLines);
        await context.AssertUnchangedAsync(afterPublication);
    }

    [Theory]
    [InlineData("game_state/meta/soul_state.json")]
    [InlineData("game_state/world/world_events.json")]
    public async Task ShiningSurvivalRecovery_LateCompanionMutationFailsBeforePublicationAndRollsBack(
        string changedPath)
    {
        await using var context = await ResourceMaterializationTestContext.CreateAsync();
        var scenario = await PrepareShiningSurvivalScenarioAsync(context);
        var rawIssues = await ValidateAcceptedTurnRawMechanicsAsync(context);
        Assert.False(
            rawIssues.Any(issue => issue.Severity == IssueSeverity.Error),
            string.Join(Environment.NewLine, rawIssues.Select(static issue =>
                $"{issue.Code}: {issue.Message} expected={issue.Expected} actual={issue.Actual}")));

        var changed = Assert.IsType<JsonObject>(await context.ReadJsonAsync(changedPath));
        if (string.Equals(
                changedPath,
                "game_state/meta/soul_state.json",
                StringComparison.Ordinal))
        {
            changed[ShiningBlessingEffectState.SoulStateProperty]![
                "pendingSurvivalEffects"]![0]!["recovery"] = 25;
        }
        else
        {
            changed["events"]![0]!["severity"] = "catastrophic";
        }
        await context.WriteExactJsonAsync(changedPath, changed.ToJsonString());
        var beforePublication = await context.CaptureAsync(
            CanonicalStateNormalizer.NormalizerRollbackTrackedFiles);

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateAsync(
                context.FileSystem,
                context.Normalizer,
                context.Validator,
                scenario.Backups));

        Assert.Contains(changedPath, exception.Message, StringComparison.Ordinal);
        Assert.Contains("changed after validation", exception.Message, StringComparison.Ordinal);
        await context.AssertUnchangedAsync(beforePublication);
    }

    private static async Task<ShiningSurvivalScenario> PrepareShiningSurvivalScenarioAsync(
        ResourceMaterializationTestContext context)
    {
        var bootstrap = ResourceBootstrapStateBuilder.BuildMortalPlayer(
            incarnationNumber: 4,
            turn: 4,
            permanentStrength: 10,
            permanentConstitution: 20,
            permanentIntelligence: 30,
            permanentWisdom: 40,
            permanentFaith: 50);
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
            MortalItemIdentityState.StatePath,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["entries"] = new JsonArray()
            }.ToJsonString());
        await context.WriteExactJsonAsync(
            "game_state/core/player_status.json",
            MortalBootstrapStateBuilder.BuildFreshPlayerStatus().ToJsonString());
        await context.WriteExactJsonAsync(
            "game_state/meta/soul_state.json",
            new JsonObject
            {
                ["soulName"] = "Soul",
                ["currentRealm"] = "Mortal World",
                [ShiningBlessingEffectState.SoulStateProperty] = new JsonObject
                {
                    ["applicationState"] = "active",
                    ["pendingSurvivalEffects"] = new JsonArray(
                        new JsonObject
                        {
                            ["sourceCardId"] = "card_survival",
                            ["status"] = ShiningBlessingEffectState.SurvivalStatusPendingFirstRuinousFailure,
                            ["recovery"] = 20,
                            ["downgrade"] = 1
                        })
                }
            }.ToJsonString());
        var preTurnEvents = new JsonObject { ["events"] = new JsonArray() };
        await context.WriteExactJsonAsync(
            "game_state/world/world_events.json",
            preTurnEvents.ToJsonString());
        var ownerAuthority = await CanonicalResourceOwnerAuthorityComposer.ComposeAsync(
            bootstrap.Definitions!,
            context.FileSystem.ReadFileAsync,
            bootstrap.State!,
            bootstrap.History!,
            CanonicalResourceOwnerAuthorityPurpose.ExplicitBootstrap);
        Assert.True(
            ownerAuthority.IsValid &&
            !string.IsNullOrWhiteSpace(ownerAuthority.CanonicalAuthorityJson),
            string.Join(Environment.NewLine, ownerAuthority.Issues));
        await context.WriteExactJsonAsync(
            CanonicalResourceOwnerAuthorityComposer.AuthorityPath,
            ownerAuthority.CanonicalAuthorityJson!);
        await context.CaptureValidatedPendingSnapshotAsync(turn: 5);
        var manifest = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            "game_state/control/pending_turn_snapshot.json"));
        var backups = manifest["files"]!.AsObject().ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value!.GetValue<string>(),
            StringComparer.Ordinal);

        await context.WriteExactJsonAsync(
            "game_state/world/world_events.json",
            new JsonObject
            {
                ["events"] = new JsonArray(
                    new JsonObject
                    {
                        ["eventId"] = "evt_ruinous",
                        ["visibility"] = "player_known",
                        ["severity"] = "ruinous"
                    })
            }.ToJsonString());
        await context.WriteExactJsonAsync(
            ResourceMaterializationTestContext.CommandsPath,
            new JsonObject
            {
                ["resourceDefinitionCreations"] = new JsonArray(),
                ["resourceCapacityChanges"] = new JsonArray(),
                ["resourceChanges"] = new JsonArray(
                    ResourceChange("damage", "health", 40m, 1),
                    ResourceChange("spend", "energy", 40m, 2),
                    ResourceChange("damage", "poise", 40m, 3))
            }.ToJsonString());

        return new ShiningSurvivalScenario(
            bootstrap,
            backups,
            preTurnEvents.ToJsonString());
    }

    private sealed record ShiningSurvivalScenario(
        ResourceBootstrapStateResult Bootstrap,
        IReadOnlyDictionary<string, string> Backups,
        string PreTurnWorldEventsJson);

    private static async Task<IReadOnlyList<ValidationIssue>>
        ValidateAcceptedTurnRawMechanicsAsync(
            ResourceMaterializationTestContext context)
    {
        var issues = new List<ValidationIssue>();
        issues.AddRange(await context.Validator
            .ValidateAcceptedTurnRawMortalItemMaterializationAsync());
        issues.AddRange(await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync());
        return issues;
    }

    private static JsonObject ResourceChange(
        string operation,
        string resourceKey,
        decimal amount,
        int ordinal) =>
        new()
        {
            ["operation"] = operation,
            ["target"] = new JsonObject
            {
                ["kind"] = "player",
                ["targetId"] = "player_current"
            },
            ["resourceKey"] = resourceKey,
            ["amount"] = amount,
            ["source"] = new JsonObject
            {
                ["kind"] = "narrative_outcome"
            },
            ["eventRef"] = $"turn_5:resource:{ordinal}",
            ["reason"] = "Ruinous outcome resolved through unified resource authority"
        };

    private static ResourceMutationIntent Mutation(
        string eventRef,
        string resourceKey,
        ResourceOperation operation,
        decimal amount,
        string sourceId = "mortal_cutover_outcome") =>
        new(
            eventRef,
            new ResourceCoordinate(
                "mortal_world",
                ResourceOwnerKind.Player,
                "player_current",
                resourceKey),
            amount,
            new ResourceMutationSourceRequest(
                "narrative_outcome",
                sourceId,
                operation),
            Array.Empty<ResourceOperationKey>(),
            Array.Empty<ResourceMutationEventRequirement>(),
            ReceiptId: null);
}
