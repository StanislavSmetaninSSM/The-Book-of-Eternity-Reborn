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

        await context.WriteExactJsonAsync(
            ResourceMaterializationTestContext.DefinitionsPath,
            definitions.ToJsonString());
        await context.WriteExactJsonAsync(
            ResourceMaterializationTestContext.StatePath,
            state.ToJsonString());
        await context.WriteExactJsonAsync(
            ResourceMaterializationTestContext.HistoryPath,
            history.ToJsonString());
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
        Assert.True(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem));
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
        Assert.True(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem));
        await context.AssertUnchangedAsync(before);
    }

    [Fact]
    public async Task RawValidation_OrdinaryMutationWithoutValidatedSourceFailsClosed()
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

        Assert.Contains(issues, issue => issue.Code == "resource_source_unknown");
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem));
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
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem));
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
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem));
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
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem));
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
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem));
    }

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
