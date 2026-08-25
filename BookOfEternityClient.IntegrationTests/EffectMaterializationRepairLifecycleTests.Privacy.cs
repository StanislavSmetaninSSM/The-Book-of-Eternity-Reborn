using System.Collections;
using BookOfEternityClient.AgentConsole;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Spectre.Console;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    private const string EffectFailureDiagnosticPath =
        "game_state/control/validation_diagnostic_failure_report.json";

    [Fact]
    public void EffectMaterializationRepairLifecycleTests_Privacy_AgentConsoleOuterFailureUsesPlayerSafeProjection()
    {
        const string secret = "SECRET_OUTER_EFFECT_EXCEPTION_effect_789";
        var store = new AgentConsoleStateStore();
        using var input = new AgentConsoleLiveInputSource(store, readTimeout: TimeSpan.FromSeconds(5));
        var engine = CreateGameEngine(input);

        InvokePrivate(
            engine,
            "RecordGameLoopErrorObservation",
            new InvalidOperationException(secret));

        var snapshot = store.GetSnapshot();
        Assert.NotNull(snapshot);
        Assert.Equal("world-turn-paused", snapshot!.ScreenId);
        Assert.Equal(AgentConsoleMode.Error, snapshot.Mode);
        Assert.Equal("Ход прервался", snapshot.Title);
        AssertPlayerAgentConsoleSnapshotIsPrivate(
            snapshot,
            store.GetEvents(),
            secret,
            "InvalidOperationException",
            "game_session/error_log.txt");
    }

    [Fact]
    public async Task EffectMaterializationRepairLifecycleTests_Privacy_NonRepairContractFailureUsesPlayerSafeProjection()
    {
        const string secret = "SECRET_CONTRACT_EFFECT_FAILURE_effect_790";
        var issue = CreateProtectedEffectPrivacyIssue(secret);
        var store = new AgentConsoleStateStore();
        using var input = new AgentConsoleLiveInputSource(store, readTimeout: TimeSpan.FromSeconds(5));
        var engine = CreateGameEngine(input);
        using var consoleCapture = new IsolatedAnsiConsoleCapture();

        var projectionTask = Task.Run(() => InvokePrivate(
            engine,
            "ShowContractValidationErrors",
            "effect materialization",
            new List<ValidationIssue> { issue }));
        AgentConsoleSnapshot? snapshot = null;
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (snapshot == null && DateTime.UtcNow < deadline)
        {
            snapshot = store.GetSnapshot();
            if (snapshot == null)
                await Task.Delay(25);
        }

        Assert.NotNull(snapshot);
        input.EnqueueKey(Key(ConsoleKey.Enter));
        await projectionTask.WaitAsync(TimeSpan.FromSeconds(2));

        var playerOutput = consoleCapture.Output;
        AssertPlayerEffectFailureIsPrivate(playerOutput, secret, issue.Code!, issue.FilePath);
        Assert.Equal("world-turn-paused", snapshot!.ScreenId);
        Assert.Equal(AgentConsoleMode.Error, snapshot.Mode);
        AssertPlayerAgentConsoleSnapshotIsPrivate(
            snapshot,
            store.GetEvents(),
            secret,
            issue.Code!,
            issue.FilePath,
            "effect materialization");
    }

    [Fact]
    public async Task EffectMaterializationRepairLifecycleTests_Privacy_ProtectedFailureKeepsExactDetailOperatorOnly()
    {
        const string trackedPath = "game_state/world/weather.json";
        const string baseline = "{\"description\":\"Тихая ночь\"}";
        const string rejected = "{\"description\":\"Непринятая буря\"}";
        const string secret = "SECRET_EFFECT_EXCEPTION_effect_123";
        await _fs.WriteFileAtomicAsync(trackedPath, baseline);
        var engine = CreateGameEngine();
        var rollbackSnapshot = await InvokePrivateTaskResultAsync(
            engine,
            "CreatePreTurnBackup",
            "effect_privacy_protected");
        await _fs.WriteFileAtomicAsync(trackedPath, rejected);
        var generation = await GetOrCreateSessionGenerationAsync();
        var issue = CreateProtectedEffectPrivacyIssue(secret);
        using var consoleCapture = new IsolatedAnsiConsoleCapture();

        var accepted = await InvokePrivateAsync<bool>(
            engine,
            "WaitForContractRepairAsync",
            "effect privacy failure",
            new List<ValidationIssue> { issue },
            1,
            rollbackSnapshot,
            generation);
        var playerOutput = consoleCapture.Output;

        Assert.False(accepted);
        AssertPlayerEffectFailureIsPrivate(playerOutput, secret, issue.Code!, issue.FilePath);
        var diagnostic = await _fs.ReadFileAsync(EffectFailureDiagnosticPath);
        Assert.Contains(secret, diagnostic, StringComparison.Ordinal);
        Assert.Contains(issue.Code!, diagnostic, StringComparison.Ordinal);
        Assert.Contains(issue.FilePath, diagnostic, StringComparison.Ordinal);

        await InvokePrivateTaskAsync(
            engine,
            "RollbackRejectedAcceptedTurnAsync",
            rollbackSnapshot,
            string.Empty);
        Assert.Equal(baseline, await _fs.ReadFileAsync(trackedPath));
        Assert.True(_fs.FileExists(EffectFailureDiagnosticPath));
    }

    [Theory]
    [InlineData("diagnostic")]
    [InlineData("cleanup")]
    public async Task EffectMaterializationRepairLifecycleTests_Privacy_BookkeepingFailureDoesNotEscapeToPlayer(
        string failingOperation)
    {
        const string trackedPath = "game_state/world/weather.json";
        const string baseline = "{\"description\":\"Безветрие\"}";
        const string rejected = "{\"description\":\"Непринятый ветер\"}";
        const string secret = "BOOKKEEPING_SECRET_effect_456";
        await _fs.WriteFileAtomicAsync(trackedPath, baseline);
        var engine = CreateGameEngine();
        var rollbackSnapshot = await InvokePrivateTaskResultAsync(
            engine,
            "CreatePreTurnBackup",
            "effect_privacy_" + failingOperation);
        await _fs.WriteFileAtomicAsync(trackedPath, rejected);
        var generation = await GetOrCreateSessionGenerationAsync();
        var issue = CreateProtectedEffectPrivacyIssue(secret);
        const string repairRequestPath =
            "game_state/control/validation_repair_request.json";
        if (string.Equals(failingOperation, "cleanup", StringComparison.Ordinal))
        {
            await _fs.WriteFileAtomicAsync(repairRequestPath, "{\"stale\":true}");
            ArmCanonicalWriteFailure(repairRequestPath);
        }
        else
        {
            ArmCanonicalWriteFailure(EffectFailureDiagnosticPath);
        }
        using var consoleCapture = new IsolatedAnsiConsoleCapture();

        var accepted = await InvokePrivateAsync<bool>(
            engine,
            "WaitForContractRepairAsync",
            "effect bookkeeping privacy failure",
            new List<ValidationIssue> { issue },
            1,
            rollbackSnapshot,
            generation);
        var playerOutput = consoleCapture.Output;

        Assert.False(accepted);
        AssertPlayerEffectFailureIsPrivate(playerOutput, secret, issue.Code!, issue.FilePath);
        await InvokePrivateTaskAsync(
            engine,
            "RollbackRejectedAcceptedTurnAsync",
            rollbackSnapshot,
            string.Empty);
        Assert.Equal(baseline, await _fs.ReadFileAsync(trackedPath));
    }

    [Fact]
    public async Task EffectMaterializationRepairLifecycleTests_Privacy_CorruptRollbackEvidenceStopsSafelyAndReportsExactDetail()
    {
        const string trackedPath = "game_state/world/weather.json";
        const string baseline = "{\"description\":\"Ясно\"}";
        const string rejected = "{\"description\":\"Непринятый туман\"}";
        const string corruptEvidence = "CORRUPT_EFFECT_ROLLBACK_EVIDENCE";
        await _fs.WriteFileAtomicAsync(trackedPath, baseline);
        var engine = CreateGameEngine();
        var rollbackSnapshot = await InvokePrivateTaskResultAsync(
            engine,
            "CreatePreTurnBackup",
            "effect_privacy_corrupt_evidence");
        var backupPath = ReadRollbackBackupPath(rollbackSnapshot, trackedPath);
        await _fs.WriteFileAtomicAsync(trackedPath, rejected);
        await _fs.WriteFileAtomicAsync(backupPath, corruptEvidence);
        using var consoleCapture = new IsolatedAnsiConsoleCapture();

        await InvokePrivateTaskAsync(
            engine,
            "RollbackRejectedAcceptedTurnAsync",
            rollbackSnapshot,
            "[yellow]↩ Изменения мира не были приняты.[/]");
        var playerOutput = consoleCapture.Output;

        AssertPlayerEffectFailureIsPrivate(
            playerOutput,
            corruptEvidence,
            backupPath,
            trackedPath,
            "InvalidDataException");
        Assert.Equal(rejected, await _fs.ReadFileAsync(trackedPath));
        Assert.True(_fs.FileExists(backupPath));
        var diagnostic = await _fs.ReadFileAsync(EffectFailureDiagnosticPath);
        Assert.Contains("Rollback evidence hash mismatch", diagnostic, StringComparison.Ordinal);
        Assert.Contains(trackedPath, diagnostic, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EffectMaterializationRepairLifecycleTests_Privacy_RestoreWriteFailureStopsSafelyAndRetainsEvidence()
    {
        const string trackedPath = "game_state/world/weather.json";
        const string baseline = "{\"description\":\"Штиль\"}";
        const string rejected = "{\"description\":\"Непринятый ливень\"}";
        await _fs.WriteFileAtomicAsync(trackedPath, baseline);
        var engine = CreateGameEngine();
        var rollbackSnapshot = await InvokePrivateTaskResultAsync(
            engine,
            "CreatePreTurnBackup",
            "effect_privacy_restore_write");
        var backupPath = ReadRollbackBackupPath(rollbackSnapshot, trackedPath);
        await _fs.WriteFileAtomicAsync(trackedPath, rejected);
        ArmCanonicalWriteFailure(trackedPath);
        SetPrivateField(engine, "_inGame", true);
        using var consoleCapture = new IsolatedAnsiConsoleCapture();

        await InvokePrivateTaskAsync(
            engine,
            "RollbackRejectedAcceptedTurnAsync",
            rollbackSnapshot,
            "[yellow]↩ Изменения мира не были приняты.[/]");
        var playerOutput = consoleCapture.Output;

        AssertPlayerEffectFailureIsPrivate(
            playerOutput,
            trackedPath,
            "Injected accepted-turn canonical write failure",
            "IOException");
        Assert.Equal(rejected, await _fs.ReadFileAsync(trackedPath));
        Assert.True(_fs.FileExists(backupPath));
        Assert.False(GetPrivateFieldValue<bool>(engine, "_inGame"));
        var diagnostic = await _fs.ReadFileAsync(EffectFailureDiagnosticPath);
        Assert.Contains(trackedPath, diagnostic, StringComparison.Ordinal);
        Assert.Contains(
            "Injected accepted-turn canonical write failure",
            diagnostic,
            StringComparison.Ordinal);
    }

    private static ValidationIssue CreateProtectedEffectPrivacyIssue(string secret) =>
        new(
            EffectAcceptedTurnPlan.IdentityIndexPath + ".entries[0].effectId",
            IssueSeverity.Error,
            secret,
            code: "effect_identity_duplicate_effect_id",
            actor: "effect-apply:effectChanges[0]",
            section: "effect_materialization",
            expected: "one exact client-owned effectId",
            actual: secret,
            repairHint: "Never expose this exact operator detail to the player.",
            repairTargetFiles: new[] { EffectAcceptedTurnPlan.IdentityIndexPath });

    private static string ReadRollbackBackupPath(object rollbackSnapshot, string trackedPath)
    {
        var value = rollbackSnapshot.GetType()
            .GetProperty("BackupFiles")?
            .GetValue(rollbackSnapshot);
        var backups = Assert.IsAssignableFrom<IDictionary>(value);
        return Assert.IsType<string>(backups[trackedPath]);
    }

    private static void AssertPlayerEffectFailureIsPrivate(
        string playerOutput,
        params string[] exactSecrets)
    {
        Assert.Contains("Изменения мира не были приняты", playerOutput, StringComparison.Ordinal);
        foreach (var secret in exactSecrets.Where(static value => !string.IsNullOrWhiteSpace(value)))
            Assert.DoesNotContain(secret, playerOutput, StringComparison.OrdinalIgnoreCase);
        foreach (var forbidden in new[]
                 {
                     "validation", "contract", "materialization", "repair", "rollback",
                     "backup", "harness", "bridge", "agent", "dto", "exception",
                     "game_state/", ".json", "effect_"
                 })
        {
            Assert.DoesNotContain(forbidden, playerOutput, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static void AssertPlayerAgentConsoleSnapshotIsPrivate(
        AgentConsoleSnapshot snapshot,
        IReadOnlyList<AgentConsoleEvent> events,
        params string[] exactSecrets)
    {
        Assert.Empty(snapshot.Diagnostics);
        var playerProjection = string.Join(
            Environment.NewLine,
            new[] { snapshot.ScreenId, snapshot.Title, snapshot.PlainText }
                .Concat(snapshot.Actions.Select(static action => action.Label))
                .Concat(events.Select(static item => item.Message ?? string.Empty)));
        foreach (var secret in exactSecrets.Where(static value => !string.IsNullOrWhiteSpace(value)))
            Assert.DoesNotContain(secret, playerProjection, StringComparison.OrdinalIgnoreCase);
        foreach (var forbidden in new[]
                 {
                     "validation", "contract", "materialization", "repair", "rollback",
                     "backup", "harness", "bridge", "agent", "dto", "exception",
                     "game_state/", "game_session/", ".json", "effect_", "requestId"
                 })
        {
            Assert.DoesNotContain(forbidden, playerProjection, StringComparison.OrdinalIgnoreCase);
        }
    }

    private sealed class IsolatedAnsiConsoleCapture : IDisposable
    {
        private readonly IAnsiConsole _originalConsole = AnsiConsole.Console;
        private readonly StringWriter _writer = new();

        public IsolatedAnsiConsoleCapture()
        {
            AnsiConsole.Console = AnsiConsole.Create(new AnsiConsoleSettings
            {
                Ansi = AnsiSupport.No,
                ColorSystem = ColorSystemSupport.NoColors,
                Interactive = InteractionSupport.No,
                Out = new AnsiConsoleOutput(_writer)
            });
        }

        public string Output => _writer.ToString();

        public void Dispose() => AnsiConsole.Console = _originalConsole;
    }
}
