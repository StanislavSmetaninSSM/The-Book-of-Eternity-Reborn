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
        var resources = ResourceBootstrapStateBuilder.BuildPristine();
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
        var resources = ResourceBootstrapStateBuilder.BuildPristine();
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
}
