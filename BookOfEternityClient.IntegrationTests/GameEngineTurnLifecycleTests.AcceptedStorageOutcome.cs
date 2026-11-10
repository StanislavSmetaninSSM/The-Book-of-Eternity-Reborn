using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Fact]
    public async Task ActualAcceptedHandlerPreservesUncertainNormalizerDecision()
    {
        CopyDirectory(TestRepoPaths.BaseSessionRoot, _fs.GameSessionPath);
        _fs.DeleteFile("output/narrative_response.json");
        _fs.DeleteFile("output/interface_updates.json");
        var emptyMap = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["realm"] = "mortal_world",
            ["locations"] = new JsonArray(),
            ["links"] = new JsonArray()
        };
        var pendingCurrent = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["realm"] = "mortal_world",
            ["locationId"] = null,
            ["state"] = "pending_materialization"
        };
        var emptyLocationIndex = MortalLocationIdentityState.CreateEmptyRoot();
        await _fs.WriteFileAtomicAsync(
            MortalLocationMaterializationContract.WorldMapPath,
            emptyMap.ToJsonString());
        await _fs.WriteFileAtomicAsync(
            MortalLocationMaterializationContract.CurrentLocationPath,
            pendingCurrent.ToJsonString());
        await _fs.WriteFileAtomicAsync(
            MortalLocationIdentityState.StatePath,
            emptyLocationIndex.ToJsonString());

        var engine = CreateGameEngine(new QueuedConsoleInputSource([]));
        var rollbackSnapshot = await InvokePrivateTaskResultAsync(
            engine,
            "CreatePreTurnBackup",
            "accepted_location_write_failure");
        var request = new TurnRequest
        {
            SessionId = "session_location_write_failure",
            RequestId = "request_location_write_failure",
            TurnNumber = 42,
            PlayerAction = "Материализовать Чёрный брод.",
            Timestamp = "2026-08-12T00:00:00Z",
            ProgressionControl = new ProgressionControl { CurrentRealm = "Mortal World" }
        };
        await WriteJsonAsync("input/turn_request.json", request);
        await InvokePrivateTaskResultAsync(
            engine,
            "CreateCanonicalBaselineSnapshotAsync",
            request,
            rollbackSnapshot,
            "accepted location write failure test");
        var manifest = await InvokePrivateTaskResultAsync(
            engine,
            "LoadPendingTurnSnapshotManifestAsync");
        var snapshotContext = await InvokePrivateTaskResultAsync(
            engine,
            "LoadValidatedPendingTurnSnapshotContextAsync",
            manifest,
            true);

        var raw = MortalLocationTestFixture.CreateRawLocation("current_scene_creation");
        await WriteJsonAsync(
            MortalLocationMaterializationContract.CurrentLocationPath,
            new JsonObject { ["currentLocationData"] = raw });
        await WriteJsonAsync(
            "output/narrative_response.json",
            new
            {
                response = "Чёрный брод уже принят, хотя запись индекса ещё не завершилась.",
                timestamp = "2026-08-12T00:01:00Z"
            });
        await WriteJsonAsync(
            "output/interface_updates.json",
            new
            {
                dialogueOptions = new[] { new { text = "Перейти мост", category = "travel" } },
                timestamp = "2026-08-12T00:01:00Z"
            });
        var reached = 0;
        byte[] unknown = [0xFF, 0x31];
        byte[]? journal = null;
        _consolePublicationObserver = (phase, index) =>
        {
            if (reached > 0 || phase != TrustedLocalPublicationPhase.MemberPublished) return;
            var journalPath = Path.Combine(_fs.RuntimeRootPath, "trusted-local-publication-v1", "active.json");
            using var document = JsonDocument.Parse(File.ReadAllBytes(journalPath));
            var member = document.RootElement.GetProperty("Members")[index];
            if (member.GetProperty("Path").GetString() != _fs.ResolvePath(MortalLocationIdentityState.StatePath)) return;
            Assert.True(member.GetProperty("After").GetProperty("Exists").GetBoolean());
            Assert.Equal(member.GetProperty("After").GetProperty("Bytes").GetBytesFromBase64(),
                File.ReadAllBytes(_fs.ResolvePath(MortalLocationIdentityState.StatePath)));
            reached++;
            File.WriteAllBytes(_fs.ResolvePath(MortalLocationIdentityState.StatePath), unknown);
            journal = File.ReadAllBytes(journalPath);
            throw new InvalidOperationException("accepted engine index publication cut");
        };
        var failure = await Record.ExceptionAsync(() => InvokePrivateAsync<AcceptedTurnValidationDisposition>(
            engine, "ValidateAcceptedTurnOutcomeWithRepairLoopAsync", "обработки хода",
            snapshotContext, rollbackSnapshot, request.TurnNumber, request.ProgressionControl));
        _consolePublicationObserver = null;
        Assert.True(reached == 1, $"Original accepted handler cut hits={reached}; failure={failure}");
        Assert.Equal(unknown, File.ReadAllBytes(_fs.ResolvePath(MortalLocationIdentityState.StatePath)));
        Assert.Equal(journal, File.ReadAllBytes(Path.Combine(_fs.RuntimeRootPath, "trusted-local-publication-v1", "active.json")));
        Assert.IsType<CoordinatedStatePublicationUncertainException>(failure);
    }
}
