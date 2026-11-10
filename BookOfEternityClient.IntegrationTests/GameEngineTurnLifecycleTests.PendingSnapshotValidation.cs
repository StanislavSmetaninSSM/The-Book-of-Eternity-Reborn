using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.IO;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    /// <summary>
    /// Observes the real game-state validator inside the actual engine call and checks its exact lifetime boundary.
    /// </summary>
    /// <param name="postMaterialization">
    /// True selects the accepted post-materialization source; false selects an ordinary source.
    /// </param>
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task PendingSnapshotValidation_ActualCallerReservesOnlyPostMaterialization(bool postMaterialization)
    {
        const string path = "game_state/meta/soul_state.json";
        await WriteJsonAsync(path, new { soulName = "Scope", currentRealm = "Chaos Sea", currentIncarnation = 0 });
        await _fs.WriteFileAtomicAsync($"game_state/control/pending_turn_snapshot/{path}", (await _fs.ReadFileAsync(path))!);
        await WritePendingTurnSnapshotManifestAsync("scope-session", "scope-request", 42, path);
        await WriteJsonAsync("input/turn_request.json", new { sessionId = "scope-session", requestId = "scope-request", turnNumber = 42 });
        var engine = CreateGameEngine(new QueuedConsoleInputSource([]));
        var validator = (ValidationService)typeof(GameEngine).GetField("_validator", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(engine)!;
        var selection = typeof(ValidationService).GetField("_activeGameStateValidationSelection", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(validator)!;
        var reservation = typeof(ValidationService).GetField("_pendingSnapshotReservation", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var observed = new List<bool>();
        var writesDuringReservation = new List<string>();
        _consoleCanonicalReadObserver = _ =>
        {
            if (selection.GetType().GetProperty("Value")!.GetValue(selection) != null)
                observed.Add(reservation.GetValue(validator) != null);
            return Task.CompletedTask;
        };
        _consoleMutationObserver = changed => { if (reservation.GetValue(validator) != null) writesDuringReservation.Add(changed); };
        var source = postMaterialization
            ? (string)typeof(GameEngine).GetField("PostAcceptedMaterializedStateValidationSource", BindingFlags.Static | BindingFlags.NonPublic)!.GetRawConstantValue()!
            : "ordinary scope control";
        using var owner = _fs.BeginMainAdmission();
        await owner.AcquireAsync();
        await InvokePrivateAsync<bool>(engine, "ValidateCurrentGameStateOrShowErrorsAsync", source);
        Assert.NotEmpty(observed);
        Assert.All(observed, reserved => Assert.Equal(postMaterialization, reserved));
        Assert.Empty(writesDuringReservation);
        Assert.Null(reservation.GetValue(validator));
        await _fs.WriteFileAtomicAsync("game_state/world/scope-after-close.json", "{}");
    }

    /// <summary>
    /// Drives an actual repair response and observes a new source reservation on the next engine iteration.
    /// </summary>
    [Fact]
    public async Task PendingSnapshotValidation_RepairGetsFreshSnapshotAndWritesOnlyAfterClose()
    {
        const string path = "game_state/meta/soul_state.json";
        const string manifestPath = "game_state/control/pending_turn_snapshot.json";
        await CanonicalResourceQuartetTestFixture.CommitFreshBootstrapAsync(_fs,
            new AfterlifeOwnerResourceAcceptedState(Profiles: AfterlifeEntityProfileState.CreateDefaultRoot()));
        await WriteJsonAsync(path, new { soulName = "Scope repair", currentRealm = "Chaos Sea", currentIncarnation = 0, soulFormDescription = new { invalid = true } });
        await _fs.WriteFileAtomicAsync($"game_state/control/pending_turn_snapshot/{path}", (await _fs.ReadFileAsync(path))!);
        await _fs.WriteFileAtomicAsync($"game_state/control/pending_turn_snapshot/{AfterlifeEntityProfileState.StatePath}",
            (await _fs.ReadFileAsync(AfterlifeEntityProfileState.StatePath))!);
        await WritePendingTurnSnapshotManifestAsync("repair-session", "repair-request", 42, path, AfterlifeEntityProfileState.StatePath);
        await WriteJsonAsync("input/turn_request.json", new { sessionId = "repair-session", requestId = "repair-request", turnNumber = 42 });
        var engine = CreateGameEngine(new QueuedConsoleInputSource([]));
        var validator = (ValidationService)typeof(GameEngine).GetField("_validator", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(engine)!;
        var reservation = typeof(ValidationService).GetField("_pendingSnapshotReservation", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var entries = new List<object>();
        var writesDuringReservation = new List<string>();
        var secondEntry = new IOException("Second repair iteration reached its fresh physical authentication.");
        var repaired = false;
        _consoleCanonicalReadObserver = read =>
        {
            if (read == manifestPath && reservation.GetValue(validator) is { } active && !entries.Contains(active))
            {
                entries.Add(active);
                if (entries.Count == 2) throw secondEntry;
            }
            return Task.CompletedTask;
        };
        _consoleMutationObserver = changed => { if (reservation.GetValue(validator) != null) writesDuringReservation.Add(changed); };
        var source = (string)typeof(GameEngine).GetField("PostAcceptedMaterializedStateValidationSource", BindingFlags.Static | BindingFlags.NonPublic)!.GetRawConstantValue()!;
        using var owner = _fs.BeginMainAdmission();
        await owner.AcquireAsync();
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var gmRepair = Task.Run(async () =>
        {
            // Observing the external request must not create a competing client admission.
            // Actual repair mutations below still use the participating manager and its guards.
            while (!File.Exists(_fs.ResolvePath("game_state/control/validation_repair_request.json")))
                await Task.Delay(25, stop.Token);
            Assert.Null(reservation.GetValue(validator));
            var before = (await _fs.ReadFileAsync(manifestPath))!;
            await _fs.WriteFileAtomicAsync(manifestPath, before + " ");
            await WriteJsonAsync(path, new { soulName = "Scope repair", currentRealm = "Chaos Sea", currentIncarnation = 0, soulFormDescription = "Repaired" });
            repaired = true;
            await WriteJsonAsync("game_state/control/validation_repair_ready.json",
                new { sessionId = "repair-session", requestId = "repair-request", turnNumber = 42, updatedAtUtc = "2026-10-19T00:00:00Z", note = "Scope repair control" });
        });
        try
        {
            var error = await Assert.ThrowsAnyAsync<Exception>(() =>
                InvokePrivateAsync<bool>(engine, "ValidateCurrentGameStateOrShowErrorsAsync", source, null, null, true));
            Assert.Same(secondEntry, error);
            await gmRepair;
            Assert.True(repaired);
            Assert.Equal(2, entries.Count);
            Assert.NotSame(entries[0], entries[1]);
            Assert.Empty(writesDuringReservation);
            Assert.Null(reservation.GetValue(validator));
        }
        finally
        {
            stop.Cancel();
            try { await gmRepair; } catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        }
    }
}
