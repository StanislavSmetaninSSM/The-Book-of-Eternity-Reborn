using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Models;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Fact]
    public async Task BrowserOriginalAdmission_RecognizesSameTurnPublishedLocationWithoutResealing()
    {
        var original = MortalLocationTestFixture.CreateCanonicalLocationWithIdentity("loc_browser_comparison_original", "Old room");
        var map = MortalLocationTestFixture.CreateWorldMap(original);
        var access = MortalStorageAccessProjection.Build(map, null);
        Assert.True(access.IsValid);
        await _fs.WriteFileAtomicAsync(MortalLocationMaterializationContract.WorldMapPath, map.ToJsonString());
        await _fs.WriteFileAtomicAsync(MortalLocationMaterializationContract.CurrentLocationPath,
            MortalLocationTestFixture.CreateCurrentProjection(original).ToJsonString());
        await _fs.WriteFileAtomicAsync(MortalLocationIdentityState.StatePath,
            MortalLocationTestFixture.CreateIdentityIndex(original).ToJsonString());
        await _fs.WriteFileAtomicAsync(MortalStorageAccessProjection.StatePath, access.Root!.ToJsonString());
        await _fs.WriteFileAtomicAsync(MortalItemIdentityState.StatePath, MortalItemIdentityState.CreateEmptyRoot().ToJsonString());
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json",
            "{\"soulName\":\"Scene witness\",\"sessionId\":\"browser-recovery-session\",\"currentRealm\":\"Mortal World\",\"currentIncarnation\":1}");
        await _fs.WriteFileAtomicAsync(MortalLivingWorldSource.WorldTimePath, "{\"currentTimeInMinutes\":0}");
        var resources = ResourceBootstrapStateBuilder.BuildMortalPlayer(1, 41, 10, 10, 10, 10, 10);
        Assert.True(resources.IsValid);
        await _fs.WriteFileAtomicAsync(ResourceMaterializationContract.DefinitionsPath, resources.Definitions!.ToCanonicalJson());
        await _fs.WriteFileAtomicAsync(ResourceMaterializationContract.StatePath, resources.State!.ToCanonicalJson());
        await _fs.WriteFileAtomicAsync(ResourceMaterializationContract.HistoryPath, resources.History!.ToCanonicalJson());
        var authority = await CanonicalResourceOwnerAuthorityComposer.ComposeAsync(resources.Definitions, _fs.ReadFileAsync,
            resources.State, resources.History, CanonicalResourceOwnerAuthorityPurpose.ExplicitBootstrap);
        Assert.True(authority.IsValid);
        await _fs.WriteFileAtomicAsync(CanonicalResourceOwnerAuthorityComposer.AuthorityPath, authority.CanonicalAuthorityJson!);
        var scheduler = new ProgressionScheduleService(_fs, NullLogger<ProgressionScheduleService>.Instance);
        var control = await scheduler.BuildControlForNextTurnAsync("Mortal World");
        var binding = await QueueBrowserInputAsync(_fs);
        var engine = CreateGameEngine(new QueuedConsoleInputSource([]), fileSystem: _fs);
        var staged = await SessionOperationContext.RunParticipatingExpectedSessionAsync(_fs, binding.Generation, async () =>
        {
            using var owner = _fs.BeginBrowserOriginalOperation(binding);
            await InvokePrivateTaskAsync(engine, "ClaimBrowserPreparationAsync", binding);
            var turn = new TurnRequest
            {
                SessionId = "browser-recovery-session", RequestId = binding.ActionId, TurnNumber = 42,
                PlayerAction = binding.Action, Timestamp = DateTime.UtcNow.ToString("O"),
                PreGeneratedDices1d20 = [7], ProgressionControl = control
            };
            var backup = await InvokePrivateTaskResultAsync(engine, "CreatePreTurnBackup", binding.ActionId);
            await InvokePrivateTaskAsync(engine, "CreateCanonicalBaselineSnapshotAsync", turn, backup, "обработки хода", binding);
            return await InvokePrivateAsync<PendingPlayerActionService.Staged>(engine, "PublishBrowserStagingAsync", binding,
                JsonSerializer.Serialize(turn, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        });
        var manifest = GameEngine.ValidateDetachedBrowserBinding(staged);
        var originalMap = Assert.IsType<byte[]>(await _fs.ReadFileBytesAsync(manifest.Files[MortalLocationMaterializationContract.WorldMapPath]));
        var raw = MortalLocationTestFixture.CreateRawLocation("current_scene_creation");
        raw["materialization"]!["sourceTurn"] = manifest.TurnNumber;
        raw["materialization"]!["sourceAuthority"]!["authorityId"] = $"turn_{manifest.TurnNumber}";
        raw["coordinates"] = new JsonObject { ["x"] = 10, ["y"] = 0, ["z"] = 0 };
        await _fs.WriteFileAtomicAsync(MortalLocationMaterializationContract.CurrentLocationPath,
            new JsonObject { ["currentLocationData"] = raw }.ToJsonString());

        await SessionOperationContext.RunParticipatingExpectedSessionAsync(_fs, staged.Binding.Generation, async () =>
        {
            using var processing = _fs.BeginBrowserOriginalOperation(staged.Binding, staged);
            _fs.BeginBrowserOriginalProcessing(staged);
            var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
            var result = await AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(_fs,
                new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance), validator, manifest.Files);
            Assert.DoesNotContain(result.Issues, issue => issue.Severity == IssueSeverity.Error);
            var current = JsonNode.Parse((await _fs.ReadFileAsync(MortalLocationMaterializationContract.CurrentLocationPath))!)!;
            var allocated = current["locationId"]!.GetValue<string>();
            Assert.NotEqual("loc_browser_comparison_original", allocated);
            Assert.NotNull(current["materializationReceipt"]);
            var comparison = Assert.IsType<ValidationService.CompletedLocationComparison>(result.LocationComparison);
            // Use the browser's own authenticated snapshot override and existing reader scopes.
            using var bound = validator.UsePrevalidatedPendingTurnSnapshotScope(manifest);
            var selection = new GameStateValidationSelection(GameStateValidationPhase.CrossReferences);
            var accepted = await validator.ValidateCompletedLocationStateAsync(comparison, selection);
            Assert.DoesNotContain(accepted, issue => issue.Code == "current_location_unknown_location_id");
            Assert.Equal(originalMap, await _fs.ReadFileBytesAsync(manifest.Files[MortalLocationMaterializationContract.WorldMapPath]));
            var persistedMap = JsonNode.Parse((await _fs.ReadFileAsync(MortalLocationMaterializationContract.WorldMapPath))!)!;
            persistedMap["locations"]!.AsArray().Last()!["name"] = "Different after-image";
            await _fs.WriteFileAtomicAsync(MortalLocationMaterializationContract.WorldMapPath, persistedMap.ToJsonString());
            await Assert.ThrowsAsync<ValidationService.CompletedLocationComparisonRefusedException>(
                () => validator.ValidateCompletedLocationStateAsync(comparison, selection));
            _fs.DeleteFile(MortalLocationMaterializationContract.WorldMapPath);
            await Assert.ThrowsAsync<ValidationService.CompletedLocationComparisonRefusedException>(
                () => validator.ValidateCompletedLocationStateAsync(comparison, selection));
            Assert.Equal(originalMap, await _fs.ReadFileBytesAsync(manifest.Files[MortalLocationMaterializationContract.WorldMapPath]));
            return true;
        });
    }
}
