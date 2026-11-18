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
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
            context.FileSystem.GetOrCreateSessionGeneration(lease);
        await context.WriteJsonAsync("game_state/meta/soul_state.json", new JsonObject { ["currentRealm"] = "Mortal World" });
        await context.WriteJsonAsync(MortalLivingWorldSource.WorldTimePath, new JsonObject { ["currentTimeInMinutes"] = 0 });
        await context.WriteJsonAsync(MortalItemIdentityState.StatePath, MortalItemIdentityState.CreateEmptyRoot());
        var originalLocation = MortalLocationTestFixture.CreateCanonicalLocationWithIdentity("loc_comparison_original", "Original courtyard");
        await context.WriteJsonAsync(MortalLocationMaterializationContract.WorldMapPath, MortalLocationTestFixture.CreateWorldMap(originalLocation));
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
        var raw = MortalLocationTestFixture.CreateRawLocation("current_scene_creation");
        raw["coordinates"] = new JsonObject { ["x"] = 10, ["y"] = 0, ["z"] = 0 };
        await context.WriteJsonAsync(MortalLocationMaterializationContract.CurrentLocationPath,
            new JsonObject { ["currentLocationData"] = raw });
        var result = await PublishAsync(context, backups);
        Assert.DoesNotContain(result.Issues, issue => issue.Severity == IssueSeverity.Error);
        var current = (await context.ReadJsonAsync(MortalLocationMaterializationContract.CurrentLocationPath))!;
        var allocated = current["locationId"]!.GetValue<string>();
        Assert.NotEqual("loc_comparison_original", allocated);
        Assert.NotNull(current["materializationReceipt"]);
        Assert.Equal(originalMapBytes, await context.FileSystem.ReadFileBytesAsync(backups[MortalLocationMaterializationContract.WorldMapPath]));
        var issues = await context.Validator.ValidateCompletedLocationStateAsync(result,
            new GameStateValidationSelection(GameStateValidationPhase.CrossReferences));
        Assert.DoesNotContain(issues, issue => issue.Code == "current_location_unknown_location_id");
        Assert.Equal(originalMapBytes, await context.FileSystem.ReadFileBytesAsync(backups[MortalLocationMaterializationContract.WorldMapPath]));
    }
}
