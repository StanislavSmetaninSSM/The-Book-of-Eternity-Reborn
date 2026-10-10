using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Fact]
    public async Task BrowserInput_ConflictBeforePublicationPreservesCompetingRequestAndOriginalSlot()
    {
        CopyDirectory(TestRepoPaths.BaseSessionRoot, _fs.GameSessionPath);
        var binding = await QueueBrowserInputAsync();
        const string other = "{\"requestId\":\"competing-at-staging-publication\"}";
        var injected = false;
        _consoleMutationObserver = path =>
        {
            if (injected || path != PendingTurnSnapshotAuthority.AuthorityPath) return;
            injected = true;
            File.WriteAllText(_fs.ResolvePath("input/turn_request.json"), other);
        };
        await Assert.ThrowsAsync<InvalidOperationException>(() => InvokePrivateTaskAsync(CreateGameEngine(),
            "ProcessPlayerTurn", binding.Action, null, null, null, true, binding));
        Assert.True(injected);
        Assert.Equal(other, await _fs.ReadFileAsync("input/turn_request.json"));
        Assert.Equal("preparing", JsonNode.Parse((await _fs.ReadFileAsync(PendingPlayerActionService.PendingPath))!)!["status"]!.GetValue<string>());
        Assert.True(_fs.FileExists(PendingTurnSnapshotAuthority.AuthorityPath));
    }

    [Theory]
    [InlineData("cancel")]
    [InlineData("error")]
    public async Task BrowserInput_KnownRestoredTerminalDispositionReleasesOriginalSlot(string disposition)
    {
        CopyDirectory(TestRepoPaths.BaseSessionRoot, _fs.GameSessionPath);
        var binding = await QueueBrowserInputAsync();
        var input = new QueuedConsoleInputSource([Key(disposition == "cancel" ? ConsoleKey.Escape : ConsoleKey.Enter)]);
        var engine = CreateGameEngine(input);
        var error = disposition == "error" ? Task.Run(async () =>
        {
            var request = await WaitForTurnRequestAsync();
            await _fs.WriteFileAtomicAsync("ready/turn_error.json", JsonSerializer.Serialize(new
            {
                sessionId = request.SessionId, requestId = request.RequestId, turnNumber = request.TurnNumber,
                timestamp = DateTime.UtcNow.ToString("O"), status = "error", error = "Controlled original GM terminal error."
            }, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        }) : Task.CompletedTask;
        await InvokePrivateTaskAsync(engine, "ProcessPlayerTurn", binding.Action, null, null, null, true, binding);
        await error.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(_fs.FileExists(PendingPlayerActionService.PendingPath));
        Assert.False(_fs.FileExists("input/turn_request.json"));
        Assert.False(_fs.FileExists(PendingTurnSnapshotAuthority.AuthorityPath));
        Assert.Empty(Directory.EnumerateFiles(_fs.ResolvePath("stories"), "*.jsonl", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task BrowserInput_ProcessRefusesCompetingRequestBeforeMutatingValidation()
    {
        var binding = await QueueBrowserInputAsync();
        await _fs.WriteFileAtomicAsync("input/turn_request.json", "{\"requestId\":\"competing-before-validation\"}");
        var before = BrowserRecoveryTree();
        await Assert.ThrowsAsync<InvalidOperationException>(() => InvokePrivateTaskAsync(CreateGameEngine(),
            "ProcessPlayerTurn", binding.Action, null, null, null, true, binding));
        AssertBrowserRecoveryTree(before);
    }

    [Fact]
    public async Task BrowserInput_CompetingRequestAfterSelectionCannotBeOverwrittenByClaim()
    {
        var binding = await QueueBrowserInputAsync();
        var pending = File.ReadAllBytes(_fs.ResolvePath(PendingPlayerActionService.PendingPath));
        await _fs.WriteFileAtomicAsync("input/turn_request.json", "{\"requestId\":\"other-original-request\"}");
        var competing = File.ReadAllBytes(_fs.ResolvePath("input/turn_request.json"));
        var engine = CreateGameEngine();
        await Assert.ThrowsAsync<InvalidOperationException>(() => InvokePrivateTaskAsync(engine, "ClaimBrowserPreparationAsync", binding));
        Assert.Equal(pending, File.ReadAllBytes(_fs.ResolvePath(PendingPlayerActionService.PendingPath)));
        Assert.Equal(competing, File.ReadAllBytes(_fs.ResolvePath("input/turn_request.json")));
    }

    [Fact]
    public async Task BrowserInput_PreparingMenuRefusesBeforeHealthMutationAndPreservesEvidence()
    {
        var binding = await QueueBrowserInputAsync();
        var engine = CreateGameEngine();
        await InvokePrivateTaskAsync(engine, "ClaimBrowserPreparationAsync", binding);
        var before = BrowserRecoveryTree();
        Assert.False(await InvokePrivateAsync<bool>(engine, "HasCurrentSessionCoreAsync"));
        AssertBrowserRecoveryTree(before);
    }

    [Fact]
    public async Task BrowserInput_StagedMenuDescriptionDoesNotInitializeOrRepairCanonicalState()
    {
        var (engine, _) = await PrepareBrowserInputStagingAsync();
        var before = BrowserRecoveryTree();
        await InvokePrivateTaskAsync(engine, "BuildContinueDescriptionAsync");
        AssertBrowserRecoveryTree(before);
    }

    [Fact]
    public async Task BrowserInput_ProcessingReentryRefusesBeforeReplayOrEvidenceCleanup()
    {
        var (engine, staged) = await PrepareBrowserInputStagingAsync();
        await InvokePrivateTaskAsync(engine, "ClaimBrowserTerminalAsync", staged);
        var before = BrowserRecoveryTree();
        var restarted = CreateGameEngine();
        await Assert.ThrowsAsync<InvalidOperationException>(() => InvokePrivateTaskAsync(restarted, "ClassifyBrowserRecoveryAsync"));
        AssertBrowserRecoveryTree(before);
    }

    [Fact]
    public async Task BrowserInput_AcceptedRecordReentryOnlyDequeuesOriginalSlotAndKeepsHistory()
    {
        await PrepareAcceptedBrowserRecordAsync();
        var before = BrowserRecoveryTree();
        await InvokePrivateTaskAsync(CreateGameEngine(), "ClassifyBrowserRecoveryAsync");
        Assert.False(File.Exists(_fs.ResolvePath(PendingPlayerActionService.PendingPath)));
        Assert.True(before.Remove(Path.GetRelativePath(_fs.BasePath,
            _fs.ResolvePath(PendingPlayerActionService.PendingPath)).Replace('\\', '/')));
        AssertBrowserRecoveryTree(before);
        await InvokePrivateTaskAsync(CreateGameEngine(), "ClassifyBrowserRecoveryAsync");
        AssertBrowserRecoveryTree(before);
    }

    [Theory]
    [InlineData("history")]
    [InlineData("proof")]
    [InlineData("new-request")]
    [InlineData("generation")]
    public async Task BrowserInput_DamagedAcceptedRecordRefusesAndPreservesAllRemainingEvidence(string damage)
    {
        await PrepareAcceptedBrowserRecordAsync();
        var pendingPath = _fs.ResolvePath(PendingPlayerActionService.PendingPath);
        var pending = JsonNode.Parse(File.ReadAllText(pendingPath))!.AsObject();
        if (damage == "new-request")
            await _fs.WriteFileAtomicAsync("input/turn_request.json", "{\"requestId\":\"newer-original-request\"}");
        else if (damage == "history")
        {
            var proof = JsonNode.Parse(pending["phaseProof"]!.GetValue<string>())!.AsObject();
            var path = proof["story"]!["path"]!.GetValue<string>();
            File.AppendAllText(_fs.ResolvePath(path), proof["story"]!["rowJson"]!.GetValue<string>() + "\n");
        }
        else
        {
            pending[damage == "proof" ? "phaseProofHash" : "sessionGeneration"] = "damaged-original-binding";
            await _fs.WriteFileAtomicAsync(PendingPlayerActionService.PendingPath, pending.ToJsonString());
        }
        var before = BrowserRecoveryTree();
        await Assert.ThrowsAsync<InvalidDataException>(() => InvokePrivateTaskAsync(CreateGameEngine(), "ClassifyBrowserRecoveryAsync"));
        AssertBrowserRecoveryTree(before);
    }

    private Task<PendingPlayerActionService.Binding> QueueBrowserInputAsync() =>
        SessionOperationContext.RunParticipatingCurrentSessionAsync(_fs, async () =>
        {
            await using var lease = await _fs.AcquireCanonicalWriteLeaseAsync();
            var root = PendingPlayerActionService.PrepareQueued(_fs, lease, "Я читаю исходное письмо.", "browser-composer", DateTime.UtcNow.ToString("O"));
            var json = root.ToJsonString();
            await _fs.WriteFileAtomicAsync(lease, PendingPlayerActionService.PendingPath, json);
            return PendingPlayerActionService.Parse(json, _fs.GetOrCreateSessionGeneration(lease)).Binding;
        });

    private async Task<(GameEngine Engine, PendingPlayerActionService.Staged Staged)> PrepareBrowserInputStagingAsync()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json",
            "{\"soulName\":\"Проверочная душа\",\"sessionId\":\"browser-recovery-session\",\"currentRealm\":\"Mortal World\",\"currentIncarnation\":1}");
        var binding = await QueueBrowserInputAsync();
        var engine = CreateGameEngine();
        GetPrivateField<GameLoop>(engine, "_gameLoop").SetSession("browser-recovery-session", 0);
        await InvokePrivateTaskAsync(engine, "ClaimBrowserPreparationAsync", binding);
        var request = new TurnRequest
        {
            SessionId = "browser-recovery-session", RequestId = binding.ActionId, TurnNumber = 1,
            PlayerAction = binding.Action, Timestamp = DateTime.UtcNow.ToString("O"), PreGeneratedDices1d20 = [3, 17]
        };
        await InvokePrivateTaskAsync(engine, "CreateCanonicalBaselineSnapshotAsync", request, null, "обработки хода", binding);
        var staged = await InvokePrivateAsync<PendingPlayerActionService.Staged>(engine, "PublishBrowserStagingAsync", binding,
            JsonSerializer.Serialize(request, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        return (engine, staged);
    }

    private async Task PrepareAcceptedBrowserRecordAsync()
    {
        var (engine, staged) = await PrepareBrowserInputStagingAsync();
        staged = await InvokePrivateAsync<PendingPlayerActionService.Staged>(engine, "ClaimBrowserTerminalAsync", staged);
        var story = await InvokePrivateAsync<PendingPlayerActionService.StoryProof>(engine, "AppendBrowserStoryAsync", staged,
            new GameResponse { Response = "Исходное письмо прочитано." }, "Берег", null);
        // Component fixture establishes completed original artifact inventory explicitly;
        // it does not qualify gameplay's terminal branch, which has its own actual C5 chain.
        _fs.DeleteFile("input/turn_request.json");
        await InvokePrivateTaskAsync(engine, "CleanupPendingTurnSnapshotAsync");
        await InvokePrivateTaskAsync(engine, "FinishAcceptedBrowserActionAsync", staged, story);
    }

    private Dictionary<string, byte[]> BrowserRecoveryTree() => Directory.EnumerateFiles(_fs.BasePath, "*", SearchOption.AllDirectories)
        .Where(path => !Path.GetRelativePath(_fs.BasePath, path).StartsWith(".boe_runtime", StringComparison.Ordinal))
        .ToDictionary(path => Path.GetRelativePath(_fs.BasePath, path).Replace('\\', '/'), File.ReadAllBytes, StringComparer.Ordinal);

    private void AssertBrowserRecoveryTree(Dictionary<string, byte[]> before)
    {
        var after = BrowserRecoveryTree();
        Assert.Equal(before.Keys.Order(), after.Keys.Order());
        foreach (var pair in before) Assert.Equal(pair.Value, after[pair.Key]);
    }
}
