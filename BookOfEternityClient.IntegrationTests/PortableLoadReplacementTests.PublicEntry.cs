using System.Reflection;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class PortableLoadReplacementTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PublicLoadCompatibilityReportsCommitEvenWhenRequiredRefreshFails(bool failRefresh)
    {
        var source = await PrepareCurrentArchiveAsync();
        var protectedFiles = SnapshotLibraryAndSource(source);
        var generation = File.ReadAllBytes(_files.SessionGenerationPath);
        var refreshes = 0;
        _afterPublication = () =>
        {
            refreshes++;
            if (failRefresh) throw new IOException("public load committed refresh cut");
        };

        var committed = await _service.LoadGameAsync(source);

        Assert.True(committed);
        Assert.Equal(1, refreshes);
        Assert.Single(_phases.Where(value => value.Phase == TrustedLocalPublicationPhase.Committed));
        Assert.False(generation.SequenceEqual(File.ReadAllBytes(_files.SessionGenerationPath)));
        Assert.Equal(_loadedSoul, File.ReadAllBytes(_files.ResolvePath(SoulPath)));
        Assert.Equal(failRefresh ? "Old live soul" : "Loaded soul", _state.CurrentState.SoulName);
        AssertPreserved(protectedFiles);
        AssertOwnedScratchEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PublicLoadCompatibilityDoesNotReportRollbackOrUncertaintyAsCommit(bool conflict)
    {
        var source = await PrepareCurrentArchiveAsync();
        var before = Snapshot(_files.GameSessionPath);
        var cuts = 0;
        _fault = (phase, _) =>
        {
            if (phase != TrustedLocalPublicationPhase.CommitStaged) return;
            cuts++;
            if (conflict) Put(MarkerPath, [91, 92, 93]);
            throw new IOException("public load precommit cut");
        };

        Assert.False(await _service.LoadGameAsync(source));

        Assert.Equal(1, cuts);
        Assert.DoesNotContain(_phases, value => value.Phase == TrustedLocalPublicationPhase.Committed);
        Assert.Equal("Old live soul", _state.CurrentState.SoulName);
        if (!conflict) AssertPreserved(before);
        else Assert.True(File.Exists(Path.Combine(_files.RuntimeRootPath, "trusted-local-publication-v1", "active.json")));
    }

    [Fact]
    public async Task PublicLoadCompatibilityRejectsInvalidInputWithoutPublication()
    {
        var source = await PrepareCurrentArchiveAsync();
        File.WriteAllBytes(source, [1, 2, 3]);
        var before = Snapshot(_files.GameSessionPath);

        Assert.False(await _service.LoadGameAsync(source));

        Assert.Equal(0, _prepared);
        Assert.Empty(_phases);
        AssertPreserved(before);
    }

    [Fact]
    public void TypedLoadContractIsPublicAndRetainsFourDistinctDecisions()
    {
        Assert.True(typeof(LoadReplacementDisposition).IsPublic);
        Assert.True(typeof(LoadReplacementResult).IsPublic);
        var method = typeof(SaveLoadService).GetMethod(nameof(SaveLoadService.LoadGameWithOutcomeAsync), BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(method);
        Assert.Equal(typeof(Task<LoadReplacementResult>), method!.ReturnType);
        Assert.Equal(new[] { "NotLoaded", "Committed", "RolledBack", "Uncertain" }, Enum.GetNames<LoadReplacementDisposition>());
    }
}
