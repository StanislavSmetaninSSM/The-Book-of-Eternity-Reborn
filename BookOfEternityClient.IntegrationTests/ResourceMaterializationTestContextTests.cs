using System.Text;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ResourceMaterializationTestContextTests
{
    [Fact]
    public async Task Context_CapturesBytesAndPriorAbsenceForEveryResourcePath()
    {
        await using var context = await ResourceMaterializationTestContext.CreateAsync();
        var definitionBytes = Encoding.UTF8.GetBytes(
            "{\"schemaVersion\":1,\"definitions\":[]}\r\n");
        await context.WriteExactBytesAsync(
            ResourceMaterializationTestContext.DefinitionsPath,
            definitionBytes);

        var before = await context.CaptureAsync(
            ResourceMaterializationTestContext.AllResourcePaths);

        Assert.Equal(
            new[]
            {
                "game_state/resources/resource_definitions.json",
                "game_state/resources/resource_state.json",
                "game_state/resources/resource_history.json",
                "game_state/resources/resource_owner_authority.json",
                "game_state/resources/resource_commands.json"
            },
            ResourceMaterializationTestContext.AllResourcePaths);
        Assert.Equal(
            ResourceMaterializationTestContext.AllResourcePaths.Length,
            ResourceMaterializationTestContext.AllResourcePaths
                .Distinct(StringComparer.Ordinal)
                .Count());
        Assert.Equal(
            ResourceMaterializationTestContext.AllResourcePaths,
            before.Keys);
        Assert.True(before[ResourceMaterializationTestContext.DefinitionsPath].Existed);
        Assert.Equal(
            definitionBytes,
            before[ResourceMaterializationTestContext.DefinitionsPath].Bytes);
        Assert.All(
            ResourceMaterializationTestContext.AllResourcePaths.Skip(1),
            path =>
            {
                Assert.False(before[path].Existed);
                Assert.Null(before[path].Bytes);
            });
        Assert.NotNull(context.FileSystem);
        Assert.NotNull(context.Validator);
        Assert.NotNull(context.Normalizer);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task CoordinatedQuartetFailure_RestoresExactBytesAndPriorAbsence(
        int failAfterWriteIndex)
    {
        await using var context = await ResourceMaterializationTestContext.CreateAsync();
        var initial = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ResourceMaterializationTestContext.DefinitionsPath] =
                "{ \"schemaVersion\": 1, \"definitions\": [] }\r\n",
            [ResourceMaterializationTestContext.StatePath] =
                "{ \"schemaVersion\": 1, \"entries\": [] }\n",
            [ResourceMaterializationTestContext.HistoryPath] =
                "{\"schemaVersion\":1,\"entries\":[]}"
        };
        foreach (var pair in initial)
        {
            await context.WriteExactBytesAsync(
                pair.Key,
                new UTF8Encoding(false).GetBytes(pair.Value));
        }
        await context.DeleteAsync(ResourceMaterializationTestContext.AuthorityPath);
        var quartet = new[]
        {
            ResourceMaterializationTestContext.DefinitionsPath,
            ResourceMaterializationTestContext.StatePath,
            ResourceMaterializationTestContext.HistoryPath,
            ResourceMaterializationTestContext.AuthorityPath
        };
        var before = await context.CaptureAsync(quartet);
        var writes = quartet.Select(path =>
            new CoordinatedStateWriteHelper.PlannedWrite(
                path,
                initial.TryGetValue(path, out var previous) ? previous : null,
                path == ResourceMaterializationTestContext.AuthorityPath
                    ? "{\"schemaVersion\":1,\"historicalOwners\":[],\"capacityDrafts\":[]}"
                    : initial[path].Replace("1", "2", StringComparison.Ordinal),
                RequireCurrentBaseline: true)).ToArray();
        var applied = -1;

        var committed = await CoordinatedStateWriteHelper.TryCommitWithHookAsync(
            context.FileSystem,
            _ => ++applied == failAfterWriteIndex
                ? Task.FromException(new IOException("injected quartet failure"))
                : Task.CompletedTask,
            writes);

        Assert.False(committed);
        Assert.Equal(failAfterWriteIndex, applied);
        await context.AssertUnchangedAsync(before);
    }

    [Fact]
    public async Task CoordinatedQuartet_RejectsTriadMutationAfterAuthorityPreflight()
    {
        await using var context = await ResourceMaterializationTestContext.CreateAsync();
        var bootstrap = ResourceBootstrapStateBuilder.BuildPristine();
        Assert.True(bootstrap.IsValid, string.Join(Environment.NewLine, bootstrap.Issues));
        var definitions = Assert.IsType<ResourceDefinitionCatalog>(bootstrap.Definitions);
        var state = Assert.IsType<ResourceStateLedger>(bootstrap.State);
        var history = Assert.IsType<ResourceHistoryState>(bootstrap.History);
        var beforeImages = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [ResourceMaterializationTestContext.DefinitionsPath] =
                definitions.ToCanonicalJson(),
            [ResourceMaterializationTestContext.StatePath] =
                state.ToCanonicalJson(),
            [ResourceMaterializationTestContext.HistoryPath] =
                history.ToCanonicalJson()
        };
        foreach (var (path, json) in beforeImages)
            await context.WriteExactBytesAsync(path, Encoding.UTF8.GetBytes(json!));
        await context.WriteExactBytesAsync(
            ResourceMaterializationTestContext.AuthorityPath,
            Encoding.UTF8.GetBytes(
                "{\"schemaVersion\":1,\"historicalOwners\":[],\"capacityDrafts\":[]}"));

        var quartet = await CanonicalResourceQuartetTransaction.ComposeExistingSessionAsync(
            definitions,
            state,
            history,
            state,
            history,
            context.FileSystem.ReadFileAsync,
            beforeImages,
            new Dictionary<string, string>(StringComparer.Ordinal));
        var projection = Assert.IsType<CanonicalResourceQuartetProjection>(
            quartet.Projection);
        var concurrentState =
            "{\"schemaVersion\":1,\"entries\":[{\"concurrent\":true}]}";
        await context.WriteExactBytesAsync(
            ResourceMaterializationTestContext.StatePath,
            Encoding.UTF8.GetBytes(concurrentState));
        var writes = new List<CoordinatedStateWriteHelper.PlannedWrite>
        {
            new(
                ResourceMaterializationTestContext.DefinitionsPath,
                projection.BeforeImages[ResourceMaterializationTestContext.DefinitionsPath],
                definitions.ToCanonicalJson(),
                RequireCurrentBaseline: true),
            new(
                ResourceMaterializationTestContext.StatePath,
                projection.BeforeImages[ResourceMaterializationTestContext.StatePath],
                state.ToCanonicalJson(),
                RequireCurrentBaseline: true),
            new(
                ResourceMaterializationTestContext.HistoryPath,
                projection.BeforeImages[ResourceMaterializationTestContext.HistoryPath],
                history.ToCanonicalJson(),
                RequireCurrentBaseline: true)
        };
        CanonicalResourceQuartetTransaction.AddAuthorityWriteAndGlobalGuards(
            writes,
            projection);

        var committed = await CoordinatedStateWriteHelper.TryCommitAsync(
            context.FileSystem,
            writes.ToArray());

        Assert.False(committed);
        Assert.Equal(
            concurrentState,
            await context.FileSystem.ReadFileAsync(
                ResourceMaterializationTestContext.StatePath));
    }

    [Fact]
    public async Task PublicationAssertions_DetectMutationAndAcceptExactRestoration()
    {
        await using var context = await ResourceMaterializationTestContext.CreateAsync();
        var original = Encoding.UTF8.GetBytes(
            "{\"schemaVersion\":1,\"entries\":[]}\n");
        await context.WriteExactBytesAsync(
            ResourceMaterializationTestContext.StatePath,
            original);
        var before = await context.CaptureAsync(
            ResourceMaterializationTestContext.StatePath,
            ResourceMaterializationTestContext.HistoryPath);

        await context.WriteExactBytesAsync(
            ResourceMaterializationTestContext.StatePath,
            Encoding.UTF8.GetBytes("{\"schemaVersion\":1,\"entries\":[{}]}"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => context.AssertUnchangedAsync(before));

        await context.WriteExactBytesAsync(
            ResourceMaterializationTestContext.StatePath,
            original);
        await context.AssertUnchangedAsync(before);
    }

    [Fact]
    public async Task PublicationAssertions_ProveCommandConsumptionAndPriorAbsenceCreation()
    {
        await using var context = await ResourceMaterializationTestContext.CreateAsync();
        await context.WriteExactBytesAsync(
            ResourceMaterializationTestContext.CommandsPath,
            Encoding.UTF8.GetBytes("{\"resourceChanges\":[]}"));
        var before = await context.CaptureAsync(
            ResourceMaterializationTestContext.CommandsPath,
            ResourceMaterializationTestContext.HistoryPath);

        await context.DeleteAsync(ResourceMaterializationTestContext.CommandsPath);
        await context.WriteExactBytesAsync(
            ResourceMaterializationTestContext.HistoryPath,
            Encoding.UTF8.GetBytes("{\"schemaVersion\":1,\"entries\":[]}"));

        await context.AssertConsumedAsync(
            before,
            ResourceMaterializationTestContext.CommandsPath);
        await context.AssertCreatedFromPriorAbsenceAsync(
            before,
            ResourceMaterializationTestContext.HistoryPath);
    }

    [Fact]
    public void OwnerFixtures_CoverEveryClosedOwnerAndKeepSameTurnPermanentIdsOutOfRawTargets()
    {
        var fixtures = ResourceMaterializationTestContext.CreateOwnerFixtures();

        Assert.Equal(
            new[]
            {
                "player",
                "npc",
                "combatant",
                "combat_group_member",
                "vehicle",
                "item",
                "afterlife_actor",
                "afterlife_conflict_side",
                "afterlife_scope"
            },
            fixtures.Select(static fixture => fixture.OwnerKind));
        Assert.Equal(
            fixtures.Count,
            fixtures.Select(static fixture => fixture.OwnerKind)
                .Distinct(StringComparer.Ordinal)
                .Count());

        foreach (var fixture in fixtures)
        {
            Assert.Equal(fixture.OwnerKind, fixture.RawTarget["kind"]!.GetValue<string>());
            Assert.DoesNotContain("resourceOwnerId", fixture.RawTarget);

            if (fixture.SameTurn)
            {
                Assert.Null(fixture.RawTarget["targetId"]);
                Assert.Equal(
                    fixture.OwnerRef,
                    fixture.RawTarget["targetRef"]!.GetValue<string>());
            }
            else
            {
                Assert.Null(fixture.RawTarget["targetRef"]);
                Assert.Equal(
                    fixture.ResourceOwnerId,
                    fixture.RawTarget["targetId"]!.GetValue<string>());
            }
        }
    }

    [Fact]
    public void RootBuilders_ReturnFreshStrictResourceRoots()
    {
        var first = ResourceMaterializationTestContext.CreateEmptyResourceRoots();
        var second = ResourceMaterializationTestContext.CreateEmptyResourceRoots();

        Assert.Equal(ResourceMaterializationTestContext.AllResourcePaths, first.Keys);
        Assert.Equal(1, first[ResourceMaterializationTestContext.DefinitionsPath]["schemaVersion"]!.GetValue<int>());
        Assert.Empty(first[ResourceMaterializationTestContext.DefinitionsPath]["definitions"]!.AsArray());
        Assert.Empty(first[ResourceMaterializationTestContext.StatePath]["entries"]!.AsArray());
        Assert.Empty(first[ResourceMaterializationTestContext.HistoryPath]["entries"]!.AsArray());
        Assert.Empty(first[ResourceMaterializationTestContext.AuthorityPath]["historicalOwners"]!.AsArray());
        Assert.Empty(first[ResourceMaterializationTestContext.AuthorityPath]["capacityDrafts"]!.AsArray());
        Assert.Empty(first[ResourceMaterializationTestContext.CommandsPath]["resourceDefinitionCreations"]!.AsArray());
        Assert.Empty(first[ResourceMaterializationTestContext.CommandsPath]["resourceCapacityChanges"]!.AsArray());
        Assert.Empty(first[ResourceMaterializationTestContext.CommandsPath]["resourceChanges"]!.AsArray());

        first[ResourceMaterializationTestContext.StatePath]["entries"]!.AsArray().Add(new System.Text.Json.Nodes.JsonObject());
        Assert.Empty(second[ResourceMaterializationTestContext.StatePath]["entries"]!.AsArray());
    }

    [Fact]
    public async Task DisposeAsync_RemovesOnlyItsOwnedTemporaryRoot()
    {
        var context = await ResourceMaterializationTestContext.CreateAsync();
        var rootPath = context.RootPath;
        Assert.True(Directory.Exists(rootPath));

        await context.DisposeAsync();

        Assert.False(Directory.Exists(rootPath));
    }
}
