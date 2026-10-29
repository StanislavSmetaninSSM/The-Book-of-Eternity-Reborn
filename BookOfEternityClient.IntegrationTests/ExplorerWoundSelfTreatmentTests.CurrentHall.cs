using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class ExplorerWoundSelfTreatmentTests
{
    [Theory]
    [InlineData("chaos")] [InlineData("shining")] [InlineData("shining_unknown_key")]
    public async Task CompleteElyaraEngineFixturePassesRealStateValidationBeforeAnyTurn(string kind)
    {
        var shining = kind != "chaos";
        await using var fixture = await AfterlifeHealingPaymentLifecycleTests.CreateServiceFixtureAsync(1, 20,
            engine: true, realm: shining ? "Shining Abode" : "Chaos Sea", remoteElyara: !shining);
        var files = fixture.Files;
        if (kind == "shining_unknown_key")
        {
            var root = JsonNode.Parse((await files.ReadFileAsync(ShiningAbodeState.StatePath))!)!;
            root["unrelatedUnknownKey"] = true;
            await files.WriteFileAtomicAsync(ShiningAbodeState.StatePath, root.ToJsonString());
        }
        var before = fixture.CaptureMutationRoots([ShiningAbodeState.StatePath,
            AfterlifeWoundHealingPlanner.Continuation.ServiceSoulPath,
            AfterlifeWoundTreatmentAuthority.AidGuardiansPath, GuardianThoughtJournalState.StatePath,
            PendingPlayerActionService.PendingPath, EffectAcceptedTurnInputComposer.WorldTimePath,
            PendingTurnStateService.PendingDiceStatePath]);
        var errors = (await new ValidationService(files, NullLogger<ValidationService>.Instance).ValidateGameStateAsync())
            .Where(i => i.Severity == IssueSeverity.Error).ToArray();
        if (kind == "shining_unknown_key")
        {
            Assert.NotEmpty(errors);
            Assert.All(errors, i => Assert.True(i.FilePath.EndsWith(".unrelatedUnknownKey", StringComparison.Ordinal) &&
                i.Code is "flexible_state_unknown_top_level_key" or "strict_state_unknown_top_level_key", i.Code + ":" + i.Message));
        }
        else Assert.True(errors.Length == 0, string.Join("\n", errors.Select(i => i.Code + ":" + i.FilePath + ":" + i.Message)));
        if (kind == "shining")
        {
            var commands = CreateCommands(files);
            var opened = await commands.ExecuteAsync(new("/элиара", Owner));
            Assert.Equal(CommandExecutionState.RequiresInput, opened.State);
            Assert.Null(opened.WoundCommandState); // Private coordinator state is not a public UI contract.
            Assert.DoesNotContain(opened.Prompts, p => p.Id == "wound_target");
            var selection = Assert.IsType<UiSelectionPrompt>(Assert.Single(opened.Prompts, p => p.Id == "wound_selection"));
            var paid = Assert.Single(selection.Options, o => !o.Disabled && o.Label.Contains("Элиара", StringComparison.Ordinal));
            Assert.Contains("25", paid.Label);
            Assert.True(opened.InteractiveSession!.RequiresLocalUiLock);
            await commands.CancelPromptSessionAsync(new(opened.InteractiveSession.SessionId, Owner));
        }
        fixture.AssertMutationRootsUnchanged(before); fixture.AssertResourcesUnchanged();
    }

    [Theory]
    [InlineData("same")] [InlineData("reencoded")] [InlineData("moved")]
    [InlineData("added")] [InlineData("removed")] [InlineData("corrupt_original")] [InlineData("invalid_manifest")] [InlineData("many_halls")]
    public async Task ActualOriginalAndValidProgressionReportNeverAuthorizeHallMovement(string change)
    {
        var armed = false; var currentOpens = 0;
        await using var fixture = await AfterlifeWoundHealingLifecycleTests.Fixture.CreateAsync(1, 0, 20,
            stageRequest: false, realm: "Shining Abode", hooks: new FileSystemManagerHooks
            { BeforeCanonicalReadOpenAsync = path => { if (armed && path == ShiningAbodeState.StatePath) currentOpens++; return Task.CompletedTask; } },
            beforeResourceBootstrap: async (files, profiles) =>
            {
                await AfterlifeHealingPaymentLifecycleTests.SeedServiceAsync(files, profiles, "Shining Abode");
                var root = JsonNode.Parse((await files.ReadFileAsync(ShiningAbodeState.StatePath))!)!;
                root["halls"]!.AsArray().Add(new JsonObject { ["hallId"] = "hall_other", ["hallName"] = "Другой зал",
                    ["description"] = "Известный другой зал, не разрешение на переход.", ["serviceTags"] = new JsonArray() });
                if (change == "many_halls")
                    for (var index = 2; index < 20; index++) root["halls"]!.AsArray().Add(new JsonObject
                    { ["hallId"] = "hall_" + index, ["hallName"] = "Зал " + index,
                        ["description"] = "Отдельный известный зал.", ["serviceTags"] = new JsonArray() });
                if (change == "added") root.AsObject().Remove(ShiningCurrentHallContract.Property);
                await files.WriteFileAtomicAsync(ShiningAbodeState.StatePath, root.ToJsonString());
                await SeedAidJourneyLoreAsync(files);
            });
        var fs = fixture.Files; ProgressionControl control;
        await using (var lease = await fs.AcquireCanonicalWriteLeaseAsync())
            control = await new ProgressionScheduleService(fs, NullLogger<ProgressionScheduleService>.Instance)
                .BuildControlForNextTurnAsync(lease, activeTurnRealm: null);
        await new LiveTurnPreparationService(fs).PrepareAsync(new LiveTurnPreparationOptions
        {
            SessionId = "session_hall_preservation", RequestId = "request_hall_preservation", TurnNumber = 43,
            CurrentRealm = "Shining Abode", PlayerAction = "Спокойно подождать в текущем зале.",
            ProgressionControl = control, PreGeneratedDices1d20 = [7, 11, 16]
        });
        await using (var lease = await fs.AcquireCanonicalWriteLeaseAsync())
        {
            var turn = JsonSerializer.Deserialize<TurnRequest>((await fs.ReadFileAsync(lease, LiveTurnPreparationService.TurnRequestPath))!,
                SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed)!;
            await WriteNativeCompletionAsync(fs, lease, turn, narrative: "Душа ждёт в текущем зале.");
        }
        Assert.True(await new ValidationService(fs, NullLogger<ValidationService>.Instance)
            .HasVerifiedAfterlifeProgressionReportForCompositeAsync(control));
        await using (var lease = await fs.AcquireCanonicalWriteLeaseAsync())
        {
            var path = ShiningAbodeState.StatePath;
            var root = JsonNode.Parse((await fs.ReadFileAsync(lease, path))!)!.AsObject();
            if (change == "moved") root[ShiningCurrentHallContract.Property] = "hall_other";
            if (change == "added") root[ShiningCurrentHallContract.Property] = "hall_elyara_companion";
            if (change == "removed") root.Remove(ShiningCurrentHallContract.Property);
            if (change is "moved" or "added" or "removed" or "reencoded")
                await fs.WriteFileAtomicAsync(lease, path, root.ToJsonString(new() { WriteIndented = change == "reencoded" }));
            if (change == "corrupt_original")
            {
                var manifest = await fixture.ReadManifestAsync(lease);
                await fs.WriteFileAtomicAsync(lease, manifest.Files[path], "{}");
            }
            if (change == "invalid_manifest")
                await fs.WriteFileAtomicAsync(lease, LiveTurnPreparationService.PendingTurnSnapshotManifestPath, "{}");
        }
        var before = fixture.CaptureMutationRoots([ShiningAbodeState.StatePath]);
        var issues = new List<ValidationIssue>();
        armed = true;
        await new ValidationService(fs, NullLogger<ValidationService>.Instance).ValidateShiningCurrentHallContinuityAsync(issues);
        armed = false;
        Assert.Equal(change is "corrupt_original" or "invalid_manifest" ? 0 : 1, currentOpens);
        if (change is "same" or "reencoded" or "many_halls") Assert.Empty(issues);
        else Assert.Contains(issues, i => i.Code == "shining_current_hall_changed_without_authority");
        // Exercise the real ready-turn hook as well as the typed owner API.
        var all = await new ValidationService(fs, NullLogger<ValidationService>.Instance).ValidateGameStateAsync();
        Assert.Equal(change is not ("same" or "reencoded" or "many_halls"), all.Any(i => i.Code == "shining_current_hall_changed_without_authority"));
        fixture.AssertMutationRootsUnchanged(before); fixture.AssertResourcesUnchanged();
    }
}
