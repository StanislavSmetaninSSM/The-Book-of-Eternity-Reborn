using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalLivingWorldPublicationTests
{
    [Fact]
    public async Task CompletedLocation_CaptureCannotMintOrResealEditedStateWithoutPublicationIssuer()
    {
        var fixture = await PublishLocationComparisonFixtureAsync();
        await using var context = fixture.Context;
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var capture = await ValidationService.CaptureLocationComparisonAsync(context.FileSystem, lease);
        Assert.NotNull(capture);
        var mapPath = context.FileSystem.ResolvePath(MortalLocationMaterializationContract.WorldMapPath);
        File.AppendAllText(mapPath, "\n ");
        for (var i = 0; i < 2; i++)
        {
            var refusal = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                capture!.CompleteAsync(context.FileSystem, lease, new object()));
            Assert.Equal("Only successful ordinary publication can issue location comparison.", refusal.Message);
        }
    }

    [Theory]
    [InlineData("request")]
    [InlineData("map")]
    [InlineData("index")]
    [InlineData("original")]
    [InlineData("generation")]
    public async Task CompletedLocation_ChangedEvidenceCannotResealOrFallback(string mutation)
    {
        var fixture = await PublishLocationComparisonFixtureAsync();
        await using var context = fixture.Context;
        var path = mutation switch
        {
            "request" => LiveTurnPreparationService.TurnRequestPath,
            "map" => MortalLocationMaterializationContract.WorldMapPath,
            "index" => MortalLocationIdentityState.StatePath,
            "original" => fixture.Backups[MortalLocationMaterializationContract.WorldMapPath],
            _ => context.FileSystem.SessionGenerationPath
        };
        // Deliberate external drift is not an owned repair or a new accepted publication.
        var full = Path.IsPathRooted(path) ? path : context.FileSystem.ResolvePath(path);
        var bytes = await File.ReadAllBytesAsync(full);
        if (mutation == "generation") await File.WriteAllTextAsync(full, Guid.NewGuid().ToString("N"));
        else await File.WriteAllTextAsync(full, System.Text.Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF') + "\n ");
        Assert.NotNull(await Record.ExceptionAsync(() => context.Validator.ValidateCompletedLocationStateAsync(
            fixture.Result, new GameStateValidationSelection(GameStateValidationPhase.CrossReferences))));
        // The comparison never repairs the external modification or mints a replacement receipt.
        Assert.NotEqual(bytes, await File.ReadAllBytesAsync(full));
    }

    [Fact]
    public async Task CompletedLocation_ForeignFilesystemAndConstructedResultDoNotGrantComparison()
    {
        var fixture = await PublishLocationComparisonFixtureAsync();
        await using var context = fixture.Context;
        await using var foreign = await EffectMaterializationTestContext.CreateAsync();
        var selection = new GameStateValidationSelection(GameStateValidationPhase.CrossReferences);
        await Assert.ThrowsAsync<ValidationService.CompletedLocationComparisonRefusedException>(() => foreign.Validator.ValidateCompletedLocationStateAsync(fixture.Result, selection));
        await Assert.ThrowsAsync<InvalidDataException>(() => context.Validator.ValidateCompletedLocationStateAsync(
            new AcceptedTurnCanonicalStateRefresh.Result([], null), selection));
    }

    [Fact]
    public async Task CompletedLocation_TwoNonlocationCorrectionsRecheckSamePublicationAndDefaultRemainsStrict()
    {
        var fixture = await PublishLocationComparisonFixtureAsync();
        await using var context = fixture.Context;
        var map = await context.FileSystem.ReadFileBytesAsync(MortalLocationMaterializationContract.WorldMapPath);
        var selection = new GameStateValidationSelection(GameStateValidationPhase.CrossReferences);
        for (var i = 0; i < 3; i++)
        {
            if (i != 0) await context.FileSystem.WriteFileAtomicAsync("output/narrative.txt", "Corrected narrative " + i);
            var issues = await context.Validator.ValidateCompletedLocationStateAsync(fixture.Result, selection);
            Assert.DoesNotContain(issues, issue => issue.Code == "current_location_unknown_location_id");
            Assert.Equal(map, await context.FileSystem.ReadFileBytesAsync(MortalLocationMaterializationContract.WorldMapPath));
        }
        var strict = await context.Validator.ValidateGameStateAsync(selection);
        Assert.Contains(strict, issue => issue.Code == "current_location_unknown_location_id");
    }

    [Fact]
    public async Task CompletedLocation_OverlapAndInheritedReentryAreRefusedAndReservationCloses()
    {
        var armed = false;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        EffectMaterializationTestContext? current = null;
        var hooks = new FileSystemManagerHooks { BeforeCanonicalReadOpenAsync = async path =>
        {
            if (!armed || path != MortalLocationMaterializationContract.WorldMapPath) return;
            armed = false;
            Assert.Throws<InvalidOperationException>(() => { _ = current!.Validator.ValidateGameStateAsync(
                new GameStateValidationSelection(GameStateValidationPhase.CrossReferences)); });
            entered.TrySetResult();
            await release.Task.WaitAsync(TimeSpan.FromSeconds(5));
        } };
        var fixture = await PublishLocationComparisonFixtureAsync(hooks);
        await using var context = fixture.Context;
        current = context; armed = true;
        var selection = new GameStateValidationSelection(GameStateValidationPhase.CrossReferences);
        var first = context.Validator.ValidateCompletedLocationStateAsync(fixture.Result, selection);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Throws<InvalidOperationException>(() => { _ = context.Validator.ValidateCompletedLocationStateAsync(fixture.Result, selection); });
        }
        finally { release.TrySetResult(); await first.WaitAsync(TimeSpan.FromSeconds(10)); }
        var next = await context.Validator.ValidateCompletedLocationStateAsync(fixture.Result, selection);
        Assert.DoesNotContain(next, issue => issue.Code == "current_location_unknown_location_id");
    }

    [Fact]
    public async Task CompletedLocation_PhysicalCloseFailureCannotReturnSuccessfulIssues()
    {
        var armed = false;
        var reached = false;
        var failure = new InvalidOperationException("completed-location-original-close");
        var hooks = new FileSystemManagerHooks { OrdinaryWriteLeaseCloseContextFactory = () =>
            armed ? new LocationFailingClose(() => { reached = true; throw failure; }) : null };
        var fixture = await PublishLocationComparisonFixtureAsync(hooks);
        await using var context = fixture.Context;
        armed = true;
        var error = await Record.ExceptionAsync(() => context.Validator.ValidateCompletedLocationStateAsync(
            fixture.Result, new GameStateValidationSelection(GameStateValidationPhase.CrossReferences)));
        armed = false;
        Assert.True(reached);
        Assert.NotNull(error);
        Assert.Contains(failure.Message, error!.ToString());
    }

    [Fact]
    public async Task CompletedLocation_ClosingMapDriftRefusesBeforeReturningIssues()
    {
        var armed = false;
        var reads = 0;
        EffectMaterializationTestContext? current = null;
        var hooks = new FileSystemManagerHooks { BeforeCanonicalReadOpenAsync = path =>
        {
            if (armed && path == MortalLocationMaterializationContract.WorldMapPath && ++reads == 2)
                File.AppendAllText(current!.FileSystem.ResolvePath(path), "\n ");
            return Task.CompletedTask;
        } };
        var fixture = await PublishLocationComparisonFixtureAsync(hooks);
        await using var context = fixture.Context;
        current = context; armed = true;
        // No consumer reads: the two live-map reads belong to opening/closing evidence.
        var error = await Assert.ThrowsAsync<ValidationService.CompletedLocationComparisonRefusedException>(() => context.Validator.ValidateCompletedLocationStateAsync(
            fixture.Result, new GameStateValidationSelection(GameStateValidationPhase.None)));
        armed = false;
        Assert.Equal(2, reads);
        Assert.Equal("Completed location published image changed.", error.Message);
    }

    [Fact]
    public async Task CompletedLocation_ExistingPendingEvidenceAvoidsPerConsumerOriginalAuthentication()
    {
        var armed = false;
        var manifestReads = 0;
        var hooks = new FileSystemManagerHooks { BeforeCanonicalReadOpenAsync = path =>
        {
            if (armed && path == LiveTurnPreparationService.PendingTurnSnapshotManifestPath) manifestReads++;
            return Task.CompletedTask;
        } };
        var fixture = await PublishLocationComparisonFixtureAsync(hooks);
        await using var context = fixture.Context;
        armed = true;
        await context.Validator.ValidateCompletedLocationStateAsync(fixture.Result,
            new GameStateValidationSelection(GameStateValidationPhase.None));
        var proofReads = manifestReads;
        Assert.True(proofReads > 0);
        manifestReads = 0;
        var issues = await context.Validator.ValidateCompletedLocationStateAsync(fixture.Result,
            new GameStateValidationSelection(GameStateValidationPhase.CrossReferences | GameStateValidationPhase.NpcStateFiles));
        armed = false;
        Assert.Equal(proofReads, manifestReads);
        Assert.DoesNotContain(issues, issue => issue.Code == "current_location_unknown_location_id");
    }

    /// <summary>Injected close failure for the actual lease, not a fabricated owner.</summary>
    private sealed class LocationFailingClose(Action close) : IDisposable
    {
        public void Dispose() => close();
    }
}
