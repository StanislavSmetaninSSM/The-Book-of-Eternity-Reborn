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
}
