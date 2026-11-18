using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalLivingWorldPublicationTests
{
    [Fact]
    public async Task CompletedLocation_RealPublicationRecognizesAllocatedCurrentReferenceWithoutChangingOriginal()
    {
        var fixture = await PublishLocationComparisonFixtureAsync();
        await using var context = fixture.Context;
        var result = fixture.Result;
        var backups = fixture.Backups;
        var originalMapBytes = fixture.OriginalMap;
        var issues = await context.Validator.ValidateCompletedLocationStateAsync(result,
            new GameStateValidationSelection(GameStateValidationPhase.CrossReferences));
        Assert.DoesNotContain(issues, issue => issue.Code == "current_location_unknown_location_id");
        Assert.Equal(originalMapBytes, await context.FileSystem.ReadFileBytesAsync(backups[MortalLocationMaterializationContract.WorldMapPath]));
    }

    /// <summary>Publishes once through the actual ordinary owner; no synthetic completion issuer.</summary>
    internal static async Task<(EffectMaterializationTestContext Context, AcceptedTurnCanonicalStateRefresh.Result Result,
        IReadOnlyDictionary<string, string> Backups, byte[]? OriginalMap)> PublishLocationComparisonFixtureAsync(FileSystemManagerHooks? hooks = null)
    {
        var prepared = await PrepareLocationComparisonFixtureAsync(hooks);
        var context = prepared.Context;
        var backups = prepared.Backups;
        var originalMapBytes = prepared.OriginalMap;
        try
        {
        var result = await PublishAsync(context, backups);
        Assert.DoesNotContain(result.Issues, issue => issue.Severity == IssueSeverity.Error);
        var current = (await context.ReadJsonAsync(MortalLocationMaterializationContract.CurrentLocationPath))!;
        var allocated = current["locationId"]!.GetValue<string>();
        Assert.NotEqual("loc_comparison_original", allocated);
        Assert.NotNull(current["materializationReceipt"]);
        Assert.Equal(originalMapBytes, await context.FileSystem.ReadFileBytesAsync(backups[MortalLocationMaterializationContract.WorldMapPath]));
        return (context, result, backups, originalMapBytes);
        }
        catch { await context.DisposeAsync(); throw; }
    }
    /// <summary>Creates an actual signed original and raw command, without publishing or issuing comparison DATA.</summary>
    private static async Task<(EffectMaterializationTestContext Context, IReadOnlyDictionary<string, string> Backups,
        byte[]? OriginalMap)> PrepareLocationComparisonFixtureAsync(FileSystemManagerHooks? hooks = null, bool existingHistory = false)
    {
        var context = await EffectMaterializationTestContext.CreateAsync(hooks);
        try
        {
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
            context.FileSystem.GetOrCreateSessionGeneration(lease);
        await context.WriteJsonAsync("game_state/meta/soul_state.json", new JsonObject { ["currentRealm"] = "Mortal World" });
        await context.WriteJsonAsync(MortalLivingWorldSource.WorldTimePath, new JsonObject { ["currentTimeInMinutes"] = 0 });
        // An actual existing history file is required for physical read hooks; observed
        // absence returns before those hooks. Sign this lawful empty DATA with the original.
        if (existingHistory)
            await context.WriteJsonAsync(MortalLivingWorldSource.HistoryPath, MortalLivingWorldReceipt.ValidateHistory(null));
        await context.WriteJsonAsync(MortalItemIdentityState.StatePath, MortalItemIdentityState.CreateEmptyRoot());
        var originalLocation = MortalLocationTestFixture.CreateCanonicalLocationWithIdentity("loc_comparison_original", "Original courtyard");
        var originalMap = MortalLocationTestFixture.CreateWorldMap(originalLocation);
        await context.WriteJsonAsync(MortalLocationMaterializationContract.WorldMapPath, originalMap);
        // Effective access is a required client-owned companion even with no storages.
        // Derive it from the actual original map before signing, never from raw commands.
        var originalAccess = MortalStorageAccessProjection.Build(originalMap, null);
        Assert.True(originalAccess.IsValid, string.Join(", ", originalAccess.Issues.Select(issue => issue.Code)));
        await context.WriteJsonAsync(MortalStorageAccessProjection.StatePath, originalAccess.Root!);
        Assert.Empty(MortalStorageAccessProjection.ValidateCanonical(originalMap, originalAccess.Root));
        await context.WriteJsonAsync(MortalLocationMaterializationContract.CurrentLocationPath, MortalLocationTestFixture.CreateCurrentProjection(originalLocation));
        await context.WriteJsonAsync(MortalLocationIdentityState.StatePath, MortalLocationTestFixture.CreateIdentityIndex(originalLocation));
        var scheduler = new ProgressionScheduleService(context.FileSystem, NullLogger<ProgressionScheduleService>.Instance);
        var control = await scheduler.BuildControlForNextTurnAsync("Mortal World");
        await new LiveTurnPreparationService(context.FileSystem).PrepareAsync(new LiveTurnPreparationOptions
        {
            SessionId = Session, RequestId = Request, TurnNumber = 42, CurrentRealm = "Mortal World",
            PlayerAction = "Discover a courtyard.", ProgressionControl = control, PreGeneratedDices1d20 = [7]
        });
        var backups = await BackupsAsync(context);
        Assert.True(backups.ContainsKey(MortalLocationMaterializationContract.WorldMapPath));
        var originalMapBytes = await context.FileSystem.ReadFileBytesAsync(backups[MortalLocationMaterializationContract.WorldMapPath]);
        Assert.True(backups.ContainsKey(MortalStorageAccessProjection.StatePath));
        var originalAccessBytes = await context.FileSystem.ReadFileBytesAsync(backups[MortalStorageAccessProjection.StatePath]);
        Assert.NotNull(originalAccessBytes);
        Assert.Equal(originalAccessBytes, await context.FileSystem.ReadFileBytesAsync(MortalStorageAccessProjection.StatePath));
        var raw = MortalLocationTestFixture.CreateRawLocation("current_scene_creation");
        raw["coordinates"] = new JsonObject { ["x"] = 10, ["y"] = 0, ["z"] = 0 };
        await context.WriteJsonAsync(MortalLocationMaterializationContract.CurrentLocationPath,
            new JsonObject { ["currentLocationData"] = raw });
        Assert.Empty(raw["locationStorages"]!.AsArray());
        Assert.Equal(originalAccessBytes, await context.FileSystem.ReadFileBytesAsync(MortalStorageAccessProjection.StatePath));
        return (context, backups, originalMapBytes);
        }
        catch { await context.DisposeAsync(); throw; }
    }

}
