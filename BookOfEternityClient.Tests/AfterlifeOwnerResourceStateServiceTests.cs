using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class AfterlifeOwnerResourceStateServiceTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;

    public AfterlifeOwnerResourceStateServiceTests()
    {
        _rootPath = Path.Combine(
            Path.GetTempPath(),
            "boe-afterlife-owner-resource-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
        _fs = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
    }

    [Theory]
    [InlineData(@"game_state\resources\resource_state.json")]
    [InlineData("  game_state/resources/resource_state.json  ")]
    public async Task TryCommitAsync_RejectsResolvedAliasOfProtectedPath(
        string protectedAlias)
    {
        var plan = await SeedValidPlanAsync();
        var stateBefore = await _fs.ReadFileAsync(
            ResourceMaterializationContract.StatePath);
        var aliasGuard = new CoordinatedStateWriteHelper.PlannedWrite(
            protectedAlias,
            stateBefore,
            stateBefore,
            RequireCurrentBaseline: true,
            GuardOnly: true);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            AfterlifeOwnerResourceStateService.TryCommitAsync(
                _fs,
                plan,
                aliasGuard));

        Assert.Equal(
            stateBefore,
            await _fs.ReadFileAsync(ResourceMaterializationContract.StatePath));
    }

    [Fact]
    public async Task TryCommitAsync_RejectsMixedSeparatorDuplicateAdditionalWrites()
    {
        var plan = await SeedValidPlanAsync();
        var first = new CoordinatedStateWriteHelper.PlannedWrite(
            "game_state/world/extra_receipt.json",
            PreviousJson: null,
            NextJson: null,
            RequireCurrentBaseline: true,
            GuardOnly: true);
        var alias = first with
        {
            Path = @"game_state\world/extra_receipt.json"
        };

        await Assert.ThrowsAsync<ArgumentException>(() =>
            AfterlifeOwnerResourceStateService.TryCommitAsync(
                _fs,
                plan,
                first,
                alias));

        Assert.False(_fs.FileExists("game_state/world/extra_receipt.json"));
    }

    [Fact]
    public async Task TryCommitAsync_InvalidAdditionalPathFailsClosedBeforeCommit()
    {
        var plan = await SeedValidPlanAsync();
        var before = await CaptureQuartetBytesAsync();
        var invalid = new CoordinatedStateWriteHelper.PlannedWrite(
            "../escape.json",
            PreviousJson: null,
            NextJson: null,
            RequireCurrentBaseline: true,
            GuardOnly: true);

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            AfterlifeOwnerResourceStateService.TryCommitAsync(
                _fs,
                plan,
                invalid));

        foreach (var (path, expectedBytes) in before)
            Assert.Equal(expectedBytes, await _fs.ReadFileBytesAsync(path));
    }

    private async Task<AfterlifeOwnerResourceStateFilePlan> SeedValidPlanAsync()
    {
        await CanonicalResourceQuartetTestFixture.CommitFreshBootstrapAsync(_fs);
        var plan = await AfterlifeOwnerResourceStateService.BuildAsync(
            _fs,
            new AfterlifeOwnerResourceAcceptedState(),
            turn: 2);
        Assert.True(
            plan.IsValid,
            string.Join(Environment.NewLine, plan.Issues));
        return plan;
    }

    private async Task<IReadOnlyDictionary<string, byte[]?>> CaptureQuartetBytesAsync()
    {
        var paths = new[]
        {
            ResourceMaterializationContract.DefinitionsPath,
            ResourceMaterializationContract.StatePath,
            ResourceMaterializationContract.HistoryPath,
            CanonicalResourceOwnerAuthorityComposer.AuthorityPath
        };
        var result = new Dictionary<string, byte[]?>(StringComparer.Ordinal);
        foreach (var path in paths)
            result[path] = await _fs.ReadFileBytesAsync(path);
        return result;
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_rootPath))
                Directory.Delete(_rootPath, recursive: true);
        }
        catch
        {
            // Ignore temp cleanup failures.
        }
    }
}
