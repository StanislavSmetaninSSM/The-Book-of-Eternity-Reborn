using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class LocalInteractionScopeServiceTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;

    public LocalInteractionScopeServiceTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-local-scope-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
    }

    [Fact]
    public async Task ResolveAsync_MortalRequiresReceiptBearingMapCurrentAndIndex()
    {
        await SeedAcceptedMortalLocationAsync();

        var scope = await new LocalInteractionScopeService(_fs).ResolveAsync("Mortal World");

        Assert.True(scope.IsResolved);
        Assert.Equal(LocalInteractionRealmKind.Mortal, scope.RealmKind);
        Assert.Equal(MortalLocationTestFixture.LocationId, scope.LocationId);
        Assert.Equal("Чёрный брод", scope.LocationName);
        Assert.Contains(scope.AuthoritySnapshots, snapshot =>
            snapshot.Path == MortalLocationMaterializationContract.WorldMapPath);
        Assert.Contains(scope.AuthoritySnapshots, snapshot =>
            snapshot.Path == MortalLocationIdentityState.StatePath);
    }

    [Fact]
    public async Task ResolveAsync_MortalReceiptlessCurrentFailsClosed()
    {
        await SeedAcceptedMortalLocationAsync();
        var receiptless = MortalLocationTestFixture.CreateCurrentProjection(
            MortalLocationTestFixture.CreateCanonicalLocation());
        receiptless.Remove("materializationReceipt");
        await _fs.WriteFileAtomicAsync(
            MortalLocationMaterializationContract.CurrentLocationPath,
            receiptless.ToJsonString());

        var scope = await new LocalInteractionScopeService(_fs).ResolveAsync("Mortal World");

        Assert.False(scope.IsResolved);
        Assert.Equal(LocalInteractionRealmKind.Mortal, scope.RealmKind);
    }

    [Fact]
    public void IsMortalActorLocal_UsesOnlyExactCanonicalCurrentLocationId()
    {
        var scope = new LocalInteractionScope(
            LocalInteractionRealmKind.Mortal,
            IsResolved: true,
            MortalLocationTestFixture.LocationId,
            "Чёрный брод",
            CurrentGuardianId: string.Empty,
            LocalActorIds: new HashSet<string>(StringComparer.Ordinal),
            LocalFactionIds: new HashSet<string>(StringComparer.Ordinal),
            UnavailableReason: null,
            AuthoritySnapshots: Array.Empty<LocalInteractionAuthoritySnapshot>());

        Assert.True(LocalInteractionScopeService.IsMortalActorLocal(
            scope,
            new JsonObject { ["currentLocationId"] = MortalLocationTestFixture.LocationId }));
        Assert.False(LocalInteractionScopeService.IsMortalActorLocal(
            scope,
            new JsonObject { ["currentLocationId"] = MortalLocationTestFixture.LocationId.ToUpperInvariant() }));
        Assert.False(LocalInteractionScopeService.IsMortalActorLocal(
            scope,
            new JsonObject { ["currentLocation"] = "Чёрный брод" }));
        Assert.False(LocalInteractionScopeService.IsMortalActorLocal(
            scope,
            new JsonObject { ["locationId"] = MortalLocationTestFixture.LocationId }));
        Assert.False(LocalInteractionScopeService.IsMortalActorLocal(
            scope,
            new JsonObject { ["initialLocationId"] = MortalLocationTestFixture.LocationId }));
    }

    [Fact]
    public void IsAfterlifeActorLocal_RetainsSeparateHallNameSemantics()
    {
        var scope = new LocalInteractionScope(
            LocalInteractionRealmKind.ShiningAbode,
            IsResolved: true,
            "hall_dawn",
            "Зал Рассвета",
            CurrentGuardianId: string.Empty,
            LocalActorIds: new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            LocalFactionIds: new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            UnavailableReason: null,
            AuthoritySnapshots: Array.Empty<LocalInteractionAuthoritySnapshot>());

        Assert.True(LocalInteractionScopeService.IsAfterlifeActorLocal(
            scope,
            new JsonObject
            {
                ["realm"] = "Shining Abode",
                ["hallName"] = "зал рассвета"
            }));
    }

    private async Task SeedAcceptedMortalLocationAsync()
    {
        var location = MortalLocationTestFixture.CreateCanonicalLocation();
        await _fs.WriteFileAtomicAsync(
            MortalLocationMaterializationContract.WorldMapPath,
            MortalLocationTestFixture.CreateWorldMap(location).ToJsonString());
        await _fs.WriteFileAtomicAsync(
            MortalLocationMaterializationContract.CurrentLocationPath,
            MortalLocationTestFixture.CreateCurrentProjection(location).ToJsonString());
        await _fs.WriteFileAtomicAsync(
            MortalLocationIdentityState.StatePath,
            MortalLocationTestFixture.CreateIdentityIndex(location).ToJsonString());
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
            Directory.Delete(_rootPath, recursive: true);
    }
}
