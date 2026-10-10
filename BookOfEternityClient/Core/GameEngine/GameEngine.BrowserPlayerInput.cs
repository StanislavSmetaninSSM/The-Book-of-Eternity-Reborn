using System.Text;
using System.Text.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Spectre.Console;

namespace BookOfEternityClient.Core;

public partial class GameEngine
{
    private PendingPlayerActionService.Binding? _selectedBrowserAction;
    private string? _preservedPlayerDraft;
    private bool _preservedPlayerDraftMultiline;

    private const string BrowserRecoveryMessage =
        "Предыдущее действие требует восстановления. Повторная отправка остановлена; исходные данные сохранены.";

    private bool HasOriginalBrowserTerminalProvenance(PendingPlayerActionService.Staged staged, TerminalSignalSnapshot captured)
    {
        // Decide against immutable captured originals before ordinary resolvers can
        // synthesize recovery, discard competing signals or perform rollback.
        if (captured.CompletionExists == captured.ErrorExists) return false;
        var json = captured.CompletionExists ? captured.CompletionJson : captured.ErrorJson;
        if (string.IsNullOrWhiteSpace(json)) return false;
        try { _ = StrictJsonAuthority.Deserialize<JsonObject>(json, JsonOpts, "original browser terminal signal"); }
        catch (JsonException) { return false; }
        catch (InvalidDataException) { return false; }
        var signal = ParseReadySignalMetadata(json, "original browser terminal signal");
        var manifest = ValidateDetachedBrowserBinding(staged);
        return signal != null && string.IsNullOrWhiteSpace(signal.HarnessSource) &&
            signal.SessionId == manifest.SessionId && signal.RequestId == manifest.RequestId && signal.TurnNumber == manifest.TurnNumber &&
            HasValidTerminalSignalContract(captured.CompletionExists ? "turn_complete" : "turn_error", signal);
    }

    private void StopBrowserTerminalRecovery()
    {
        _inGame = false;
        AnsiConsole.MarkupLine($"[yellow]{BrowserRecoveryMessage}[/]");
    }

    private async Task<T> WithPendingActionLeaseAsync<T>(Func<FileSystemManager.CanonicalWriteLease, Task<T>> operation)
    {
        return await SessionOperationContext.RunParticipatingCurrentSessionAsync(_fs, async () =>
        {
            var lease = await _fs.AcquireCanonicalWriteLeaseAsync();
            Exception? failure = null;
            try { return await operation(lease); }
            catch (Exception caught) { failure = caught; throw; }
            finally { await CoordinatedStateWriteHelper.ReleaseOwnedLeaseAsync(_fs, lease, false, failure); }
        });
    }

    // Presence only: it does not read state, recover publications or grant permission.
    private bool HasBrowserInputWakeHint() => File.Exists(_fs.ResolvePath(PendingPlayerActionService.PendingPath));

    private async Task<string> ReadCooperativePlayerDraftAsync(bool multiline)
    {
#if DEBUG
        await HoldQueuedBrowserIdleCutAsync();
#endif
        var selected = await SelectQueuedBrowserActionAsync();
        if (selected != null)
        {
            _selectedBrowserAction = selected;
            return selected.Action;
        }
        var preserved = _preservedPlayerDraft;
        if (!string.IsNullOrEmpty(preserved))
        {
            var preview = new string(preserved.Take(512).Select(c => char.IsControl(c) && c != '\n' && c != '\t' ? '�' : c).ToArray());
            AnsiConsole.MarkupLine($"[dim]Сохранённый черновик (Enter = подтвердить):[/] {Markup.Escape(preview)}");
        }
        var console = ReferenceEquals(_inputSource, SystemConsoleInputSource.Instance) && !Console.IsInputRedirected
            ? new StandardTextComposerConsole(new CooperativePlayerInputSource(_inputSource, HasBrowserInputWakeHint, Console.Write))
            : _textComposerConsole;
        string value;
        try
        {
            value = TextComposer.Read(console, _clipboardService, new TextComposerOptions
            {
                PromptMarkup = multiline ? "[cyan]│[/]" : "[bold green3] > [/]",
                PreserveNewlines = true, DefaultValue = preserved,
                Mode = multiline ? TextComposerMode.MultilineEditor : TextComposerMode.Immediate,
                HelpMarkup = multiline ? "[dim]Две пустые строки подряд = отправить. \\p = вставка из буфера.[/]" : null
            });
        }
        catch (TextComposerInputClosedException interrupted) when (interrupted.Interrupted)
        {
            _preservedPlayerDraft = interrupted.Draft;
            _preservedPlayerDraftMultiline = multiline;
            selected = await SelectQueuedBrowserActionAsync();
            if (selected == null) throw new InvalidOperationException(BrowserRecoveryMessage);
            _selectedBrowserAction = selected;
            return selected.Action;
        }
        // Re-arbitrate after the bounded paste drain. If browser wins, keep all
        // completed console text as a draft for the next ordinary prompt.
        _preservedPlayerDraft = value;
        _preservedPlayerDraftMultiline = multiline;
        selected = await SelectQueuedBrowserActionAsync();
        if (selected != null)
        {
            _selectedBrowserAction = selected;
            return selected.Action;
        }
        _preservedPlayerDraft = null;
        _preservedPlayerDraftMultiline = false;
        return value;
    }

    private async Task<PendingPlayerActionService.Binding?> SelectQueuedBrowserActionAsync()
    {
        if (!HasBrowserInputWakeHint()) return null;
        return await WithPendingActionLeaseAsync(async lease =>
        {
            var state = await PendingPlayerActionService.ReadAsync(_fs, lease);
            if (state == null) return null;
            if (state.Phase != "queued") throw new InvalidOperationException(BrowserRecoveryMessage);
            if (_fs.FileExists(lease, "input/turn_request.json"))
                throw new InvalidOperationException(BrowserRecoveryMessage);
            return state.Binding;
        });
    }

    private async Task<PendingPlayerActionService.State?> ClassifyBrowserRecoveryAsync()
    {
        if (!HasBrowserInputWakeHint()) return null;
        return await WithPendingActionLeaseAsync(async lease =>
        {
            var state = await PendingPlayerActionService.ReadAsync(_fs, lease);
            if (state == null || state.Phase == "queued") return state;
            if (state.Phase is "preparing" or "terminalProcessing")
                throw new InvalidOperationException(BrowserRecoveryMessage);
            var staged = PendingPlayerActionService.ReadStaged(state);
            var manifest = ValidateDetachedBrowserBinding(staged);
            if (state.Phase == "settled")
            {
                await ValidateRestoredBrowserSettlementAsync(lease, staged, manifest);
                await PendingPlayerActionService.PublishAsync(_fs, lease, state.Json, null);
                return null;
            }
            if (state.Phase == "accepted")
            {
                await ValidateAcceptedBrowserRecordAsync(lease, state, manifest);
                await PendingPlayerActionService.PublishAsync(_fs, lease, state.Json, null);
                return null;
            }
            if (await _fs.ReadFileAsync(lease, "input/turn_request.json") != staged.RequestJson ||
                await _fs.ReadFileAsync(lease, PendingTurnSnapshotManifestPath) != staged.ManifestJson ||
                await _fs.ReadFileAsync(lease, PendingTurnSnapshotAuthority.AuthorityPath) != staged.AuthorityJson ||
                await LoadValidatedPendingTurnSnapshotContextAsync(manifest, requireCurrentContext: false) == null)
                throw new InvalidOperationException(BrowserRecoveryMessage);
            return state;
        });
    }

    internal static PendingTurnSnapshotManifest ValidateDetachedBrowserBinding(PendingPlayerActionService.Staged staged)
    {
        var manifest = StrictJsonAuthority.Deserialize<PendingTurnSnapshotManifest>(staged.ManifestJson, JsonOpts, "original browser snapshot")
            ?? throw new InvalidDataException(BrowserRecoveryMessage);
        var request = StrictJsonAuthority.Deserialize<TurnRequest>(staged.RequestJson, JsonOpts, "original browser request")
            ?? throw new InvalidDataException(BrowserRecoveryMessage);
        if (!PendingTurnSnapshotAuthority.TryReadDetachedAuthorityPayload(staged.AuthorityJson, out var authority) ||
            authority == null || ComputePendingTurnManifestPayloadHash(manifest) != manifest.ManifestPayloadHash ||
            authority.ManifestPayloadHash != manifest.ManifestPayloadHash || authority.RequestId != manifest.RequestId ||
            authority.SessionId != manifest.SessionId || authority.TurnNumber != manifest.TurnNumber ||
            manifest.BrowserActionId != staged.Binding.ActionId || manifest.BrowserSessionGeneration != staged.Binding.Generation ||
            manifest.RequestId != staged.Binding.ActionId || request.RequestId != manifest.RequestId ||
            request.SessionId != manifest.SessionId || request.TurnNumber != manifest.TurnNumber ||
            request.Timestamp != manifest.RequestTimestamp || request.PlayerAction != staged.Binding.Action ||
            manifest.PlayerAction != staged.Binding.Action || manifest.SourceLabel != OrdinaryPlayerTurnSourceLabel || manifest.BrowserOriginalMainCondition == null)
            throw new InvalidDataException(BrowserRecoveryMessage);
        return manifest;
    }

    private Task<bool> ClaimBrowserPreparationAsync(PendingPlayerActionService.Binding binding) =>
        WithPendingActionLeaseAsync(async lease =>
        {
            using var original = _fs.HasBrowserOriginalOperation ? null : _fs.BeginBrowserOriginalOperation(binding);
            var state = await PendingPlayerActionService.ReadAsync(_fs, lease);
            if (state?.Phase != "queued" || state.Json != binding.Json)
                throw new InvalidOperationException(BrowserRecoveryMessage);
            if (_fs.FileExists(lease, "input/turn_request.json") ||
                _fs.FileExists(lease, PendingTurnSnapshotManifestPath) ||
                _fs.FileExists(lease, PendingTurnSnapshotAuthority.AuthorityPath) ||
                _fs.FileExists(lease, "ready/turn_complete.json") ||
                _fs.FileExists(lease, "ready/turn_error.json"))
                throw new InvalidOperationException(BrowserRecoveryMessage);
            await PendingPlayerActionService.PublishAsync(_fs, lease, state.Json,
                PendingPlayerActionService.CreatePhase(state, "preparing", new JsonObject
                {
                    ["historyBeforeJson"] = await CaptureBrowserHistoryAsync(lease)
                }));
            return true;
        });

    private Task<PendingPlayerActionService.Staged> PublishBrowserStagingAsync(
        PendingPlayerActionService.Binding binding, string requestJson) => WithPendingActionLeaseAsync(async lease =>
    {
        var state = await PendingPlayerActionService.ReadAsync(_fs, lease);
        if (state?.Phase != "preparing" || state.Binding.ActionId != binding.ActionId)
            throw new InvalidOperationException(BrowserRecoveryMessage);
        var captured = new PendingPlayerActionService.Staged(binding, requestJson,
            await _fs.ReadFileAsync(lease, PendingTurnSnapshotManifestPath) ?? throw new InvalidDataException(BrowserRecoveryMessage),
            await _fs.ReadFileAsync(lease, PendingTurnSnapshotAuthority.AuthorityPath) ?? throw new InvalidDataException(BrowserRecoveryMessage), "",
            JsonNode.Parse(state.ProofJson!)!["historyBeforeJson"]?.GetValue<string>() ?? throw new InvalidDataException(BrowserRecoveryMessage));
        ValidateDetachedBrowserBinding(captured);
        var stagedJson = PendingPlayerActionService.CreatePhase(state, "staged", PendingPlayerActionService.StagingProof(captured));
        await PendingPlayerActionService.PublishAsync(_fs, lease, state.Json, stagedJson,
            new CoordinatedStateWriteHelper.PlannedWrite("input/turn_request.json",
                PreviousJson: null, requestJson, RequireCurrentBaseline: true));
        var staged = captured with { Json = stagedJson };
        _fs.SealBrowserOriginalPreparation(staged);
        return staged;
    });

    private Task<PendingPlayerActionService.Staged> ClaimBrowserTerminalAsync(PendingPlayerActionService.Staged staged) =>
        WithPendingActionLeaseAsync(async lease =>
        {
            using var original = _fs.HasBrowserOriginalOperation ? null : _fs.BeginBrowserOriginalOperation(staged.Binding, staged);
            var state = await PendingPlayerActionService.ReadAsync(_fs, lease);
            if (state?.Phase != "staged" || state.Json != staged.Json)
                throw new InvalidOperationException(BrowserRecoveryMessage);
            ValidateDetachedBrowserBinding(staged);
            _fs.BeginBrowserOriginalProcessing(staged);
            var processingJson = PendingPlayerActionService.CreatePhase(state, "terminalProcessing", PendingPlayerActionService.StagingProof(staged));
            await PendingPlayerActionService.PublishAsync(_fs, lease, state.Json, processingJson);
            return staged with { Json = processingJson };
        });

    private Task<PendingPlayerActionService.StoryProof> AppendBrowserStoryAsync(
        PendingPlayerActionService.Staged staged, GameResponse? response, string? location,
        IReadOnlyCollection<StoryEntityRef>? refs) => WithPendingActionLeaseAsync(lease =>
            _storyService.AppendOriginalBrowserTurnAsync(lease, staged.Binding,
                ValidateDetachedBrowserBinding(staged).TurnNumber,
                _stateManager.CurrentState.CurrentRealm ?? "Chaos Sea", _stateManager.CurrentState.Incarnation,
                response?.Response, location, refs));

    private static IEnumerable<string> OriginalBrowserArtifactInventory(PendingTurnSnapshotManifest manifest) =>
        manifest.Files.Values.Concat(manifest.RollbackBackups.Values).Concat([
            PendingTurnSnapshotManifestPath, PendingTurnSnapshotAuthority.AuthorityPath,
            "input/turn_request.json", "ready/turn_complete.json", "ready/turn_error.json"])
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal);

    private Task<bool> FinishAcceptedBrowserActionAsync(PendingPlayerActionService.Staged staged,
        PendingPlayerActionService.StoryProof story) => WithPendingActionLeaseAsync(async lease =>
    {
        var state = await PendingPlayerActionService.ReadAsync(_fs, lease);
        if (state?.Phase != "terminalProcessing" || state.Json != staged.Json)
            throw new InvalidOperationException(BrowserRecoveryMessage);
        var manifest = ValidateDetachedBrowserBinding(staged);
        await ValidateBrowserHistoryAsync(lease, staged.HistoryJson, exact: false);
        var inventory = new JsonObject();
        foreach (var path in OriginalBrowserArtifactInventory(manifest))
        {
            // Completed ordinary tail must have removed only its original artifacts.
            // A child/current request or partial cleanup cannot certify completion.
            if (_fs.FileExists(lease, path)) throw new InvalidOperationException(BrowserRecoveryMessage);
            inventory[path] = false;
        }
        var proof = PendingPlayerActionService.StagingProof(staged);
        proof["terminalDisposition"] = "ordinaryAcceptedAndCleanupComplete";
        proof["cleanupInventory"] = inventory;
        proof["story"] = JsonSerializer.SerializeToNode(story, JsonOpts);
        var acceptedJson = PendingPlayerActionService.CreatePhase(state, "accepted", proof);
        await PendingPlayerActionService.PublishAsync(_fs, lease, state.Json, acceptedJson);
        return true;
    });

    private async Task<string> CaptureBrowserHistoryAsync(FileSystemManager.CanonicalWriteLease lease)
    {
        var paths = EnumerateStoryContinuityFiles(lease).Append("game_state/history/chat_log.json").ToArray();
        PendingTurnSnapshotAuthority.RequireExactSignedPaths(paths);
        var witness = new JsonObject();
        foreach (var path in paths.Order(StringComparer.Ordinal))
        {
            var bytes = await _fs.ReadFileBytesAsync(lease, path);
            witness[path] = bytes == null ? null : new JsonObject
            {
                ["bytes"] = bytes.Length, ["hash"] = PendingPlayerActionService.Hash(bytes)
            };
        }
        return witness.ToJsonString();
    }

    private Task ValidateBrowserHistoryAsync(FileSystemManager.CanonicalWriteLease lease,string json,bool exact) =>
        ValidateBrowserHistoryCoreAsync(json,exact,path=>_fs.ReadFileBytesAsync(lease,path),_fs.EnumerateFiles(lease,"*").ToArray());

    private static async Task ValidateBrowserHistoryCoreAsync(string json,bool exact,Func<string,Task<byte[]?>> read,string[] physicalInventory)
{
        var expected = StrictJsonAuthority.Deserialize<JsonObject>(json, JsonOpts, "original browser history")
            ?? throw new InvalidDataException(BrowserRecoveryMessage);
        var currentPaths = SelectOriginalStoryContinuityFiles(physicalInventory).Append("game_state/history/chat_log.json").ToArray();
        PendingTurnSnapshotAuthority.RequireExactSignedPaths(currentPaths.Concat(expected.Select(pair => pair.Key)));
        if (exact && !currentPaths.Order(StringComparer.Ordinal).SequenceEqual(expected.Select(pair => pair.Key).Order(StringComparer.Ordinal)))
            throw new InvalidDataException(BrowserRecoveryMessage);
        foreach (var (path, witness) in expected)
        {
            if (!exact && path == "game_state/history/chat_log.json") continue;
            if (path != "game_state/history/chat_log.json" &&
                (!path.StartsWith("stories/", StringComparison.Ordinal) || !path.EndsWith(".jsonl", StringComparison.Ordinal)))
                throw new InvalidDataException(BrowserRecoveryMessage);
            var bytes = await read(path);
            if (witness == null) { if (bytes != null) throw new InvalidDataException(BrowserRecoveryMessage); continue; }
            var count = witness["bytes"]!.GetValue<int>();
            if (bytes == null || count < 0 || bytes.Length < count || (exact && bytes.Length != count) ||
                PendingPlayerActionService.Hash(bytes[..count]) != witness["hash"]?.GetValue<string>())
                throw new InvalidDataException(BrowserRecoveryMessage);
        }
    }

    private static readonly string[] BrowserSettlementWorkPaths =
    [ValidationRepairRequestPath, ValidationRepairReadyPath, "game_state/control/terminal_protocol_failure_request.json"];

    private Task<bool> FinishRestoredBrowserActionAsync(PendingPlayerActionService.Staged staged, string disposition, string terminalSignalJson) =>
        WithPendingActionLeaseAsync(async lease =>
        {
            var state = await PendingPlayerActionService.ReadAsync(_fs, lease);
            if (state?.Phase != "terminalProcessing" || state.Json != staged.Json ||
                disposition is not ("originalTerminalErrorRestored" or "originalTerminalRejectedRestored"))
                throw new InvalidOperationException(BrowserRecoveryMessage);
            var proof = PendingPlayerActionService.StagingProof(staged);
            proof["terminalDisposition"] = disposition;
            proof["terminalSignalJson"] = terminalSignalJson;
            var settled = PendingPlayerActionService.CreatePhase(state, "settled", proof);
            var candidate = PendingPlayerActionService.ReadStaged(PendingPlayerActionService.Parse(settled, staged.Binding.Generation));
            await ValidateRestoredBrowserSettlementAsync(lease, candidate, ValidateDetachedBrowserBinding(staged));
            await PendingPlayerActionService.PublishAsync(_fs, lease, state.Json, settled);
            return true;
        });

    private Task ValidateRestoredBrowserSettlementAsync(FileSystemManager.CanonicalWriteLease lease,
        PendingPlayerActionService.Staged staged,PendingTurnSnapshotManifest manifest) =>
        ValidateRestoredBrowserSettlementCoreAsync(staged,manifest,path=>_fs.ReadFileBytesAsync(lease,path),
            path=>_fs.FileExists(lease,path),_fs.EnumerateFiles(lease,"*").ToArray());

    private static async Task ValidateRestoredBrowserSettlementCoreAsync(PendingPlayerActionService.Staged staged,
        PendingTurnSnapshotManifest manifest,Func<string,Task<byte[]?>> read,Func<string,bool> exists,string[] physicalInventory)
{
        var state = PendingPlayerActionService.Parse(staged.Json, staged.Binding.Generation);
        var disposition = JsonNode.Parse(state.ProofJson!)!["terminalDisposition"]?.GetValue<string>();
        var terminalJson = JsonNode.Parse(state.ProofJson!)!["terminalSignalJson"]?.GetValue<string>()
            ?? throw new InvalidDataException(BrowserRecoveryMessage);
        _ = StrictJsonAuthority.Deserialize<JsonObject>(terminalJson, JsonOpts, "original browser terminal disposition");
        var signal = ParseReadySignalMetadataCore(terminalJson);
        var terminalKind = disposition == "originalTerminalErrorRestored" ? "turn_error" : "turn_complete";
        if (disposition is not ("originalTerminalErrorRestored" or "originalTerminalRejectedRestored") ||
            signal == null || !string.IsNullOrWhiteSpace(signal.HarnessSource) ||
            signal.SessionId != manifest.SessionId || signal.RequestId != manifest.RequestId || signal.TurnNumber != manifest.TurnNumber ||
            !HasValidTerminalSignalContract(terminalKind, signal) ||
            !PendingTurnSnapshotAuthority.TryReadDetachedAuthorityPayload(staged.AuthorityJson, out var original) ||
            original == null || original.RollbackHashMode != PendingTurnSnapshotAuthority.ExactRollbackHashMode)
            throw new InvalidDataException(BrowserRecoveryMessage);
        RequireExactBrowserPhysicalInventory(physicalInventory);
        PendingTurnSnapshotAuthority.RequireExactSignedPaths(original.RollbackBaselineFiles
            .Concat(original.RollbackBackups.Keys).Concat(original.RollbackBackupHashes.Keys));
        var expected = original.RollbackBackupHashes.Keys.Order(StringComparer.Ordinal).ToArray();
        if (expected.Length == 0 ||
            !expected.SequenceEqual(original.RollbackBackups.Keys.Order(StringComparer.Ordinal)) ||
            !expected.SequenceEqual(original.RollbackBaselineFiles.Order(StringComparer.Ordinal)) ||
            !expected.SequenceEqual(EnumerateOriginalBrowserRollbackTrackedFiles(physicalInventory).Order(StringComparer.Ordinal)))
            throw new InvalidDataException(BrowserRecoveryMessage);
        foreach (var path in expected)
        {
            var bytes = await read(path);
            if (bytes == null || PendingPlayerActionService.Hash(bytes) != original.RollbackBackupHashes[path])
                throw new InvalidDataException(BrowserRecoveryMessage);
        }
        foreach (var path in OriginalBrowserArtifactInventory(manifest).Concat(BrowserSettlementWorkPaths))
            if (exists(path)) throw new InvalidDataException(BrowserRecoveryMessage);
        await ValidateBrowserHistoryCoreAsync(staged.HistoryJson,true,read,physicalInventory);
    }

    private Task ValidateAcceptedBrowserRecordAsync(FileSystemManager.CanonicalWriteLease lease,
        PendingPlayerActionService.State state,PendingTurnSnapshotManifest manifest) =>
        ValidateAcceptedBrowserRecordCoreAsync(state,manifest,path=>_fs.ReadFileBytesAsync(lease,path),
            path=>_fs.FileExists(lease,path),_fs.EnumerateFiles(lease,"*").ToArray());

    private static async Task ValidateAcceptedBrowserRecordCoreAsync(PendingPlayerActionService.State state,
        PendingTurnSnapshotManifest manifest,Func<string,Task<byte[]?>> read,Func<string,bool> exists,string[] physicalInventory)
{
        await ValidateBrowserHistoryCoreAsync(PendingPlayerActionService.ReadStaged(state).HistoryJson,false,read,physicalInventory);
        var proof = JsonNode.Parse(state.ProofJson!)!.AsObject();
        if (proof["terminalDisposition"]?.GetValue<string>() != "ordinaryAcceptedAndCleanupComplete")
            throw new InvalidDataException(BrowserRecoveryMessage);
        var inventory = proof["cleanupInventory"]?.AsObject() ?? throw new InvalidDataException(BrowserRecoveryMessage);
        var expectedInventory = OriginalBrowserArtifactInventory(manifest).ToArray();
        if (inventory.Count != expectedInventory.Length) throw new InvalidDataException(BrowserRecoveryMessage);
        foreach (var path in expectedInventory)
            if (inventory[path]?.GetValue<bool>() != false || exists(path))
                throw new InvalidDataException(BrowserRecoveryMessage);
        var story = proof["story"]?.Deserialize<PendingPlayerActionService.StoryProof>(JsonOpts)
            ?? throw new InvalidDataException(BrowserRecoveryMessage);
        if (!story.Path.StartsWith("stories/", StringComparison.Ordinal) || story.Path.Contains("..") ||
            story.Path.Contains('\\') || !story.Path.EndsWith(".jsonl", StringComparison.Ordinal) || story.PrefixBytes <= 0)
            throw new InvalidDataException(BrowserRecoveryMessage);
        var bytes = await read(story.Path);
        if (bytes == null || bytes.Length < story.PrefixBytes ||
            PendingPlayerActionService.Hash(bytes[..story.PrefixBytes]) != story.PrefixHash)
            throw new InvalidDataException(BrowserRecoveryMessage);
        var row = StrictJsonAuthority.Deserialize<StoryEntry>(story.RowJson, JsonOpts, "accepted browser row")!;
        var prefixLines = Encoding.UTF8.GetString(bytes[..story.PrefixBytes]).TrimStart('\uFEFF')
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var lines = Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF').Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (row.RequestId != state.Binding.ActionId || row.SessionGeneration != state.Binding.Generation ||
            row.Player != state.Binding.Action || row.Turn != manifest.TurnNumber ||
            prefixLines.LastOrDefault() != story.RowJson ||
            lines.Count(line => line == story.RowJson) != 1 ||
            lines.Select(line => StrictJsonAuthority.Deserialize<StoryEntry>(line, JsonOpts, "browser story history")!)
                .Count(entry => entry.RequestId == row.RequestId && entry.SessionGeneration == row.SessionGeneration) != 1)
            throw new InvalidDataException(BrowserRecoveryMessage);
    }
}
