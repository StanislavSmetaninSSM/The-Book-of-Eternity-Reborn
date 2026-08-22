using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class CanonicalStateNormalizerResourceTests
{
    [Fact]
    public void ResourceAuthorityPathsAreTrackedForSnapshotPublicationAndRollback()
    {
        foreach (var path in ResourceMaterializationTestContext.AllResourcePaths)
        {
            if (!string.Equals(
                    path,
                    ResourceMaterializationTestContext.CommandsPath,
                    StringComparison.Ordinal))
            {
                Assert.Contains(path, CanonicalStateNormalizer.CanonicalAccumulatedFiles);
                Assert.Contains(path, CanonicalStateNormalizer.NormalizerBackupInputFiles);
            }
            Assert.Contains(path, CanonicalStateNormalizer.NormalizerRollbackTrackedFiles);
        }
    }

    [Fact]
    public async Task DefinitionCreation_PublishesCanonicalCatalogAndConsumesCommand()
    {
        await using var context = await ResourceMaterializationTestContext.CreateAsync();
        await ResourceMaterializationValidationTests.SeedEmptyRootsAsync(context);
        await context.CaptureValidatedPendingSnapshotAsync();
        await context.WriteExactJsonAsync(
            ResourceMaterializationTestContext.CommandsPath,
            ResourceMaterializationValidationTests.DefinitionCreationCommand().ToJsonString());

        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();
        Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);

        await using var writeLease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var plan = await context.Normalizer.BindTo(writeLease)
            .NormalizeAcceptedMechanicsAsync(backups: null);

        Assert.NotNull(plan);
        var definitions = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            ResourceMaterializationTestContext.DefinitionsPath));
        var definition = Assert.IsType<JsonObject>(
            Assert.Single(definitions["definitions"]!.AsArray()));
        Assert.Equal("mana", definition["resourceKey"]!.GetValue<string>());
        Assert.NotNull(definition["materialization"]);
        Assert.Null(await context.ReadJsonAsync(ResourceMaterializationTestContext.CommandsPath));
    }

    [Theory]
    [InlineData(ResourceMaterializationTestContext.DefinitionsPath)]
    [InlineData(ResourceMaterializationTestContext.StatePath)]
    [InlineData(ResourceMaterializationTestContext.HistoryPath)]
    [InlineData(ResourceMaterializationTestContext.CommandsPath)]
    public async Task DefinitionCreation_LateLiteralNullAuthorityFailsBeforeEveryWrite(
        string changedPath)
    {
        await using var context = await ResourceMaterializationTestContext.CreateAsync();
        await ResourceMaterializationValidationTests.SeedEmptyRootsAsync(context);
        await context.CaptureValidatedPendingSnapshotAsync();
        await context.WriteExactJsonAsync(
            ResourceMaterializationTestContext.CommandsPath,
            ResourceMaterializationValidationTests.DefinitionCreationCommand().ToJsonString());
        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();
        Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);

        await context.WriteExactJsonAsync(changedPath, "null");
        var before = await context.CaptureAsync(ResourceMaterializationTestContext.AllResourcePaths);
        await using var writeLease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();

        await Assert.ThrowsAsync<InvalidDataException>(() => context.Normalizer
            .BindTo(writeLease)
            .NormalizeAcceptedMechanicsAsync(backups: null));

        await context.AssertUnchangedAsync(before);
    }

    [Theory]
    [InlineData("definition", ResourceMaterializationContract.DefinitionsPath)]
    [InlineData("state", ResourceMaterializationContract.StatePath)]
    [InlineData("history", ResourceMaterializationContract.HistoryPath)]
    [InlineData("source", EffectMaterializationTestContext.PlayerWoundsPath)]
    [InlineData("owner", "game_state/npcs/npc_core.json")]
    [InlineData("target", "game_state/npcs/npc_core.json")]
    [InlineData("carrier", EffectMaterializationTestContext.PlayerEffectsPath)]
    [InlineData("index", EffectMaterializationTestContext.IdentityIndexPath)]
    [InlineData("event", "input/turn_request.json")]
    [InlineData("command", EffectMaterializationTestContext.CommandPath)]
    [InlineData("pending", ResourcePendingResolutionState.PendingPath)]
    [InlineData("internal_adapter", "game_state/control/pending_turn_snapshot.json")]
    public async Task CommonPlan_LateAuthorityMutationFailsBeforeEveryWrite(
        string authorityKind,
        string changedPath)
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        await context.SeedPlayerWoundSourceAsync();
        var usesNpcAuthority = authorityKind is "owner" or "target";
        if (usesNpcAuthority)
            await MaterializeNpcHealthAsync(context, "npc_resource_toctou_target", 60m);
        await context.CaptureValidatedPendingSnapshotAsync();
        var backups = await context.ReadPendingSnapshotBackupsAsync();
        var command = EffectMaterializationTestFixture.CreateApplyCommand();
        if (usesNpcAuthority)
        {
            command["target"] = new JsonObject
            {
                ["kind"] = "npc",
                ["targetId"] = "npc_resource_toctou_target"
            };
        }
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(command));

        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();
        Assert.True(
            issues.All(issue => issue.Severity != IssueSeverity.Error),
            string.Join(
                Environment.NewLine,
                issues.Select(issue =>
                    $"{issue.Code}: {issue.FilePath} expected={issue.Expected} actual={issue.Actual}")));
        Assert.True(AcceptedMechanicsPlanAuthority.TryPeekValidated(
            context.FileSystem,
            out _,
            out var planning));
        var plan = Assert.IsType<AcceptedMechanicsPlan>(planning.Plan);

        await ApplyLateMutationAsync(context, changedPath, authorityKind);
        var protectedPaths = CanonicalStateNormalizer.NormalizerRollbackTrackedFiles
            .Concat(plan.BeforeImages.Keys)
            .Append(changedPath)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var before = await context.CaptureBytesAsync(protectedPaths);
        await using var writeLease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();

        await Assert.ThrowsAsync<InvalidDataException>(() => context.Normalizer
            .BindTo(writeLease)
            .NormalizeAcceptedMechanicsAsync(backups));

        Assert.Equal(before, await context.CaptureBytesAsync(protectedPaths));
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem));
    }

    [Fact]
    public async Task SameTurnDefinitionInitialization_PublishesExactStateAndHistoryAtomically()
    {
        await using var context = await ResourceMaterializationTestContext.CreateAsync();
        await ResourceMaterializationValidationTests.SeedEmptyRootsAsync(context);
        await context.CaptureValidatedPendingSnapshotAsync();
        await context.WriteExactJsonAsync(
            ResourceMaterializationTestContext.CommandsPath,
            ResourceMaterializationValidationTests.DefinitionAndInitializationCommand()
                .ToJsonString());

        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();
        Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);

        await using var writeLease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var plan = await context.Normalizer.BindTo(writeLease)
            .NormalizeAcceptedMechanicsAsync(backups: null);

        Assert.NotNull(plan);
        var state = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            ResourceMaterializationTestContext.StatePath));
        var stateEntry = Assert.IsType<JsonObject>(Assert.Single(state["entries"]!.AsArray()));
        Assert.Equal("mana", stateEntry["resourceKey"]!.GetValue<string>());
        Assert.Equal("player_current", stateEntry["resourceOwnerId"]!.GetValue<string>());
        Assert.Equal(40m, stateEntry["current"]!.GetValue<decimal>());
        Assert.Equal(40m, stateEntry["maximum"]!.GetValue<decimal>());

        var history = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            ResourceMaterializationTestContext.HistoryPath));
        var transition = Assert.IsType<JsonObject>(
            Assert.Single(history["entries"]!.AsArray()));
        Assert.Equal("initialize", transition["operation"]!.GetValue<string>());
        Assert.Equal("turn_42:resource:2", transition["eventRef"]!.GetValue<string>());
        Assert.Equal("initialize_from_definition",
            transition["capacityDisposition"]!.GetValue<string>());
        Assert.Null(await context.ReadJsonAsync(ResourceMaterializationTestContext.CommandsPath));
    }

    [Fact]
    public async Task MidPublicationFailure_RestoresEveryResourceByteAndCommand()
    {
        var armed = false;
        var injected = false;
        var hooks = new FileSystemManagerHooks
        {
            BeforeCanonicalMutationAsync = path =>
            {
                if (armed &&
                    !injected &&
                    string.Equals(
                        path,
                        ResourceMaterializationContract.HistoryPath,
                        StringComparison.Ordinal))
                {
                    injected = true;
                    throw new IOException("Injected resource history publication failure.");
                }
                return Task.CompletedTask;
            }
        };
        await using var context = await ResourceMaterializationTestContext.CreateAsync(hooks);
        await ResourceMaterializationValidationTests.SeedEmptyRootsAsync(context);
        await context.CaptureValidatedPendingSnapshotAsync();
        await context.WriteExactJsonAsync(
            ResourceMaterializationTestContext.CommandsPath,
            ResourceMaterializationValidationTests.DefinitionAndInitializationCommand()
                .ToJsonString());
        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();
        Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);
        var before = await context.CaptureAsync(
            ResourceMaterializationTestContext.AllResourcePaths);

        armed = true;
        await Assert.ThrowsAsync<CanonicalStateWriteException>(() =>
            AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateAsync(
                context.FileSystem,
                context.Normalizer,
                context.Validator,
                new Dictionary<string, string>(StringComparer.Ordinal)));

        Assert.True(injected);
        await context.AssertUnchangedAsync(before);
    }

    [Fact]
    public async Task EffectOnlyTurn_PublishesThroughOneAcceptedMechanicsPlan()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        await context.SeedPlayerWoundSourceAsync();
        await context.CaptureValidatedPendingSnapshotAsync();
        var backups = await context.ReadPendingSnapshotBackupsAsync();
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(
                EffectMaterializationTestFixture.CreateApplyCommand()));

        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();
        Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);

        await using var writeLease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var plan = await context.Normalizer.BindTo(writeLease)
            .NormalizeAcceptedMechanicsAsync(backups);

        Assert.NotNull(plan);
        Assert.NotNull(plan.EffectPlan);
        var player = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.PlayerEffectsPath))!.AsObject();
        Assert.Single(player["activeEffects"]!.AsArray());
        Assert.Null(await context.ReadJsonAsync(
            EffectMaterializationTestContext.CommandPath));
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem));
    }

    [Fact]
    public async Task EffectOnlyTurn_EffectPreflightFailureInvalidatesCommonHandoff()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        await context.SeedPlayerWoundSourceAsync();
        await context.CaptureValidatedPendingSnapshotAsync();
        var backups = await context.ReadPendingSnapshotBackupsAsync();
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(
                EffectMaterializationTestFixture.CreateApplyCommand()));

        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();
        Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);
        Assert.True(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem));

        var changedDefinition = EffectMaterializationTestFixture.CreateDefinition();
        changedDefinition["display"]!["name"] = "Подменённый источник";
        await context.SeedPlayerWoundSourceAsync(changedDefinition);
        await using var writeLease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();

        await Assert.ThrowsAsync<InvalidDataException>(() => context.Normalizer
            .BindTo(writeLease)
            .NormalizeAcceptedMechanicsAsync(backups));

        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem));
    }

    private static async Task ApplyLateMutationAsync(
        EffectMaterializationTestContext context,
        string path,
        string authorityKind)
    {
        var root = await context.ReadJsonAsync(path);
        if (authorityKind == "source" && root is JsonArray wounds)
        {
            wounds[0]!["activeEffectDefinitions"]![0]!["display"]!["name"] =
                "Поздно изменённый источник";
            await context.WriteJsonAsync(path, wounds);
            return;
        }
        if (authorityKind is "owner" or "target" && root is JsonObject npcRoot)
        {
            var npc = FindFirstObjectWithProperty(npcRoot, "NPCId")
                ?? throw new InvalidOperationException(
                    "Materialized NPC target is missing from its canonical owner root.");
            npc["NPCId"] = authorityKind == "owner"
                ? "npc_resource_toctou_owner_changed"
                : "npc_resource_toctou_target_changed";
            await context.WriteJsonAsync(path, npcRoot);
            return;
        }
        if (root is JsonObject objectRoot)
        {
            objectRoot["_lateMutation"] = authorityKind;
            await context.WriteJsonAsync(path, objectRoot);
            return;
        }
        if (root is JsonArray arrayRoot)
        {
            arrayRoot.Add(new JsonObject { ["_lateMutation"] = authorityKind });
            await context.WriteJsonAsync(path, arrayRoot);
            return;
        }

        await context.WriteJsonAsync(
            path,
            new JsonObject { ["_lateMutation"] = authorityKind });
    }

    private static JsonObject? FindFirstObjectWithProperty(JsonNode? node, string propertyName)
    {
        if (node is JsonObject objectNode)
        {
            if (objectNode.ContainsKey(propertyName))
                return objectNode;
            foreach (var child in objectNode)
            {
                var match = FindFirstObjectWithProperty(child.Value, propertyName);
                if (match != null)
                    return match;
            }
        }
        else if (node is JsonArray arrayNode)
        {
            foreach (var child in arrayNode)
            {
                var match = FindFirstObjectWithProperty(child, propertyName);
                if (match != null)
                    return match;
            }
        }
        return null;
    }

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
            "game_state/npcs/npc_core.json",
            new JsonObject { ["NPCsInScene"] = new JsonArray() });
        await context.CaptureValidatedPendingSnapshotAsync(turn: 41);

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
            "game_state/npcs/npc_core.json",
            new JsonObject { ["UpdateNPCs"] = new JsonArray(npc) });

        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();
        Assert.True(
            issues.All(issue => issue.Severity != IssueSeverity.Error),
            string.Join(
                Environment.NewLine,
                issues.Select(issue =>
                    $"{issue.Code}: {issue.FilePath} expected={issue.Expected} actual={issue.Actual}")));
        await using var writeLease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        Assert.NotNull(await context.Normalizer.BindTo(writeLease)
            .NormalizeAcceptedMechanicsAsync(backups: null));
    }
}
