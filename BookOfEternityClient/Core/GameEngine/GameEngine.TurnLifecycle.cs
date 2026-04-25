using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace BookOfEternityClient.Core;

public partial class GameEngine
{
    private enum TerminalSignalWaitOutcome
    {
        Cancelled,
        Completed
    }

    private async Task<bool> WaitForGmResponse()
    {
        var manifest = await LoadPendingTurnSnapshotManifestAsync();
        var snapshotContext = await LoadValidatedPendingTurnSnapshotContextAsync(manifest);
        var rollbackSnapshot = BuildValidatedRollbackSnapshot(snapshotContext);
        if (await WaitForTerminalSignalAsync() == TerminalSignalWaitOutcome.Cancelled)
        {
            _pendingMemoryLegacyAwaitingConsumption = false;
            AnsiConsole.MarkupLine($"[yellow]{_loc.T("turn_cancelled")}[/]");
            _fs.DeleteFile("input/turn_request.json");
            _fs.DeleteFile("ready/turn_complete.json");
            _fs.DeleteFile("ready/turn_error.json");
            if (HasRollbackCapability(rollbackSnapshot))
            {
                await RestorePreTurnBackup(rollbackSnapshot!);
                CleanupBackup(rollbackSnapshot!);
                AnsiConsole.MarkupLine("[dim]Переходный ход локально отменён, состояние восстановлено из rollback backup. Если GM завершит уже отправленный ход позже, он будет обработан как отложенный ответ.[/]");
            }
            else
            {
                AnsiConsole.MarkupLine("[dim]Переходный ход локально отменён. Rollback backup для этого режима недоступен; если GM завершит уже отправленный ход позже, он всё равно придёт как отложенный ответ.[/]");
            }
            return false;
        }

        var terminalOutcome = await ResolveFinalActiveTerminalOutcomeAsync(snapshotContext, rollbackSnapshot);
        if (terminalOutcome.Kind == "failure")
        {
            _pendingMemoryLegacyAwaitingConsumption = false;
            return false;
        }

        if (terminalOutcome.Kind == "success")
        {
            var signal = terminalOutcome.Signal;
            var expectedTurn = signal?.TurnNumber ?? snapshotContext?.TurnNumber ?? (_gameLoop.TurnNumber + 1);
            if (!await ValidateAcceptedTurnOutcomeWithRepairLoopAsync(
                    "ответа GM",
                    rollbackSnapshot,
                    expectedTurn,
                    snapshotContext?.ProgressionControl))
            {
                _pendingMemoryLegacyAwaitingConsumption = false;
                _fs.DeleteFile("ready/turn_complete.json");
                _fs.DeleteFile("ready/turn_error.json");
                await CleanupPendingTurnSnapshotAsync();
                return false;
            }

            _audioService.PlayCue(AudioCue.TurnReady);
            var response = await BuildGameResponseFromFiles();
            _gameLoop.IncrementTurn();

            // Debug: log narrative length to help diagnose rendering issues
            if (string.IsNullOrEmpty(response?.Response))
                AnsiConsole.MarkupLine("[yellow dim]⚠ Нарратив пуст в ответе GM[/]");

            _lastResponse = response;
            _pendingImagePrompt = null;

            await CheckLifeTransitions();
            await CheckAscensionTrigger();

            await ConsumeAfterlifeReturnProtectionIfNeededAsync(snapshotContext);

            if (await HasPendingMemoryLegacyAwaitingConsumptionAsync())
                await FinalizePendingMemoryLegacyConsumptionAsync();

            _pendingMemoryLegacyAwaitingConsumption = false;

            var qteHandling = await HandleAcceptedQteOfferAsync(response, snapshotContext);
            if (qteHandling.EarlyExit)
            {
                await ApplyPendingShiningBlessingRuntimeEffectsAsync(snapshotContext);
                _fs.DeleteFile("ready/turn_complete.json");
                await CleanupPendingTurnSnapshotAsync();
                return true;
            }

            _lastResponse = qteHandling.Response;
            _pendingImagePrompt = qteHandling.Response?.ImagePrompt;

            if (IsIncarnationSourceLabel(snapshotContext?.SourceLabel))
                await _worldDirectiveService.MaterializePendingToActiveAsync();

            await ApplyPendingShiningBlessingRuntimeEffectsAsync(snapshotContext);

            _fs.DeleteFile("ready/turn_complete.json");
            await CleanupPendingTurnSnapshotAsync();
            return true;
        }

        _pendingMemoryLegacyAwaitingConsumption = false;
        await ShowTurnErrorMessageAsync("ready/turn_error.json");
        _fs.DeleteFile("ready/turn_error.json");

        if (HasRollbackCapability(rollbackSnapshot))
        {
            await RestorePreTurnBackup(rollbackSnapshot!);
            CleanupBackup(rollbackSnapshot!);
            AnsiConsole.MarkupLine("[yellow]↩ Переходный ход завершился ошибкой GM. Состояние откатилось к последней стабильной версии.[/]");
        }

        await CleanupPendingTurnSnapshotAsync();
        return false;
    }

    /// <summary>
    /// Waits for GM response without side effects (no turn increment, no CheckLifeTransitions).
    /// Used by transition methods that manage their own state.
    /// Returns true if response received, false if cancelled/error.
    /// </summary>
    private async Task<bool> WaitForGmResponseRaw()
    {
        var manifest = await LoadPendingTurnSnapshotManifestAsync();
        var snapshotContext = await LoadValidatedPendingTurnSnapshotContextAsync(manifest);
        var rollbackSnapshot = BuildValidatedRollbackSnapshot(snapshotContext);
        if (await WaitForTerminalSignalAsync() == TerminalSignalWaitOutcome.Cancelled)
        {
            AnsiConsole.MarkupLine($"[yellow]{_loc.T("turn_cancelled")}[/]");
            _fs.DeleteFile("input/turn_request.json");
            _fs.DeleteFile("ready/turn_complete.json");
            _fs.DeleteFile("ready/turn_error.json");
            if (HasRollbackCapability(rollbackSnapshot))
            {
                await RestorePreTurnBackup(rollbackSnapshot!);
                CleanupBackup(rollbackSnapshot!);
                AnsiConsole.MarkupLine("[dim]Переходный ход локально отменён, состояние восстановлено из rollback backup. Если GM завершит уже отправленный ход позже, он будет обработан как отложенный ответ.[/]");
            }
            else
            {
                AnsiConsole.MarkupLine("[dim]Переходный ход локально отменён. Rollback backup для этого режима недоступен; если GM завершит уже отправленный ход позже, он всё равно придёт как отложенный ответ.[/]");
            }
            return false;
        }

        var terminalOutcome = await ResolveFinalActiveTerminalOutcomeAsync(snapshotContext, rollbackSnapshot);
        if (terminalOutcome.Kind == "failure")
            return false;

        if (terminalOutcome.Kind == "error")
        {
            await ShowTurnErrorMessageAsync("ready/turn_error.json");
            _fs.DeleteFile("ready/turn_error.json");

            if (HasRollbackCapability(rollbackSnapshot))
            {
                await RestorePreTurnBackup(rollbackSnapshot!);
                CleanupBackup(rollbackSnapshot!);
                AnsiConsole.MarkupLine("[yellow]↩ Переходный ход завершился ошибкой GM. Состояние откатилось к последней стабильной версии.[/]");
            }

            await CleanupPendingTurnSnapshotAsync();
            return false;
        }

        _audioService.PlayCue(AudioCue.TurnReady);

        return true;
    }

    private async Task ApplyPendingShiningBlessingRuntimeEffectsAsync(ValidatedPendingTurnSnapshotContext? snapshotContext)
    {
        try
        {
            var refreshedManifest = await LoadPendingTurnSnapshotManifestAsync();
            var refreshedSnapshotContext = await LoadValidatedPendingTurnSnapshotContextAsync(refreshedManifest)
                                          ?? snapshotContext;
            var result = await ShiningBlessingEffectState.ApplyAcceptedTurnRuntimeEffectsAsync(
                _fs,
                _gameLoop.TurnNumber,
                ReadPreTurnSnapshotFile(refreshedSnapshotContext, ShiningAbodeState.StatePath),
                ReadPreTurnSnapshotFile(refreshedSnapshotContext, "game_state/npcs/npc_core.json"),
                ReadPreTurnSnapshotFile(refreshedSnapshotContext, "game_state/world/world_events.json"),
                ReadPreTurnSnapshotFile(refreshedSnapshotContext, "game_state/npcs/npc_relationships.json"),
                ReadPreTurnSnapshotFile(refreshedSnapshotContext, "game_state/core/player_status.json"),
                ReadPreTurnSnapshotFile(refreshedSnapshotContext, "game_state/factions/faction_core.json"));
            if (!result.Success)
            {
                _logger.LogWarning("Не удалось обработать pendingShiningBlessingEffects после accepted turn: {ErrorMessage}", result.ErrorMessage);
                return;
            }

            if (!result.StateChanged)
                return;

            await RefreshRuntimeStateAsync();
            if (result.SummaryLines.Count > 0)
            {
                AnsiConsole.MarkupLine("[gold1]✨ Shining blessing runtime effects:[/]");
                foreach (var line in result.SummaryLines)
                    AnsiConsole.MarkupLine($"  [gold1]•[/] {Markup.Escape(line)}");
                AnsiConsole.WriteLine();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось применить mortal-life consumers для pendingShiningBlessingEffects");
        }
    }

    private string? ReadPreTurnSnapshotFile(ValidatedPendingTurnSnapshotContext? snapshotContext, string relativePath)
    {
        if (snapshotContext?.Payload?.Files == null ||
            !snapshotContext.Payload.Files.TryGetValue(relativePath, out var snapshotPath) ||
            string.IsNullOrWhiteSpace(snapshotPath))
        {
            return null;
        }

        return ReadRelativeFileFromWorkspace(snapshotPath);
    }

    private async Task<TerminalSignalWaitOutcome> WaitForTerminalSignalAsync()
    {
        using var cts = new CancellationTokenSource();
        var startTime = DateTime.UtcNow;

        var waitTask = Task.Run(async () =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                if (_fs.FileExists("ready/turn_complete.json") || _fs.FileExists("ready/turn_error.json"))
                    return TerminalSignalWaitOutcome.Completed;
                await Task.Delay(500, cts.Token);
            }

            return TerminalSignalWaitOutcome.Cancelled;
        }, cts.Token);

        var result = await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots12)
            .SpinnerStyle(Style.Parse("cyan"))
            .StartAsync(_loc.T("thinking"), async ctx =>
            {
                _ = Task.Run(() =>
                {
                    while (!cts.Token.IsCancellationRequested)
                    {
                        if (Console.KeyAvailable)
                        {
                            var key = Console.ReadKey(true);
                            if (key.Key == ConsoleKey.Escape)
                            {
                                cts.Cancel();
                                return;
                            }
                        }

                        Thread.Sleep(100);
                    }
                });

                while (!waitTask.IsCompleted && !cts.IsCancellationRequested)
                {
                    var elapsed = (int)(DateTime.UtcNow - startTime).TotalSeconds;
                    if (elapsed < 15)
                        ctx.Status($"[cyan]{_loc.T("thinking")}[/]");
                    else if (elapsed < 120)
                        ctx.Status($"[yellow]⏳ Ожидание GM-демона... ({elapsed}с) (Escape = отменить)[/]");
                    else
                        ctx.Status($"[yellow]⏳ GM обрабатывает ход... ({elapsed / 60}мин {elapsed % 60}с) (Escape = отменить)[/]");

                    await Task.Delay(1000);
                }

                try
                {
                    return await waitTask;
                }
                catch (OperationCanceledException)
                {
                    return TerminalSignalWaitOutcome.Cancelled;
                }
            });

        return cts.IsCancellationRequested ? TerminalSignalWaitOutcome.Cancelled : result;
    }

    // ═══════════════════════════════════════════════
    // GAME LOOP
    // ═══════════════════════════════════════════════

    private async Task EnterGameLoop()
    {
        _inGame = true;
        await _audioService.PlayInGameMusicAsync();
        await NormalizePendingRepairArtifactsAsync();
        await NormalizePendingTerminalProtocolFailureArtifactsAsync();

        // Check if there's already a correlated completion signal waiting
        if (_fs.FileExists("ready/turn_complete.json"))
        {
            await RefreshRuntimeStateAsync();
        }

        while (_inGame)
        {
            try
            {
            // Pick up late responses (agent finished after cancel/timeout, or response from previous turn)
            var manifest = await LoadPendingTurnSnapshotManifestAsync();
            var snapshotContext = await LoadValidatedPendingTurnSnapshotContextAsync(manifest);
            var rollbackSnapshot = BuildValidatedRollbackSnapshot(snapshotContext);
            if (await ResolveConcurrentActiveTerminalSignalsAsync(snapshotContext, rollbackSnapshot))
                continue;

            if (_fs.FileExists("ready/turn_error.json"))
            {
                var signal = await ReadReadySignalMetadataAsync("ready/turn_error.json");
                if (await DiscardMismatchedReadySignalAsync("late turn_error", signal, snapshotContext))
                    continue;

                var signalTurn = signal?.TurnNumber;
                var expectedTurn = _gameLoop.TurnNumber + 1;
                if (signalTurn.HasValue && signalTurn.Value != expectedTurn)
                {
                    _logger.LogWarning("Игнорируется late error для хода {Turn}, ожидался ход {ExpectedTurn}", signalTurn.Value, expectedTurn);
                    _fs.DeleteFile("ready/turn_error.json");
                    ClearTransientOutputFiles();
                    await CleanupPendingTurnSnapshotAsync();
                    continue;
                }

                await ShowTurnErrorMessageAsync("ready/turn_error.json");
                if (HasRollbackCapability(rollbackSnapshot))
                {
                    await RestorePreTurnBackup(rollbackSnapshot!);
                    CleanupBackup(rollbackSnapshot!);
                    AnsiConsole.MarkupLine("[yellow]↩ Поздний сигнал ошибки GM восстановил последнюю стабильную версию состояния.[/]");
                }

                _fs.DeleteFile("ready/turn_error.json");
                await CleanupPendingTurnSnapshotAsync();
                continue;
            }

        if (_fs.FileExists("ready/turn_complete.json"))
        {
            var signal = await ReadReadySignalMetadataAsync("ready/turn_complete.json");
                if (await DiscardMismatchedReadySignalAsync("late turn_complete", signal, snapshotContext))
                    continue;

                var signalTurn = signal?.TurnNumber;
                var expectedTurn = _gameLoop.TurnNumber + 1;
                if (signalTurn.HasValue && signalTurn.Value != expectedTurn)
                {
                    _logger.LogWarning("Игнорируется late response для хода {Turn}, ожидался ход {ExpectedTurn}", signalTurn.Value, expectedTurn);
                    _fs.DeleteFile("ready/turn_complete.json");
                    ClearTransientOutputFiles();
                    await CleanupPendingTurnSnapshotAsync();
                    continue;
                }

                if (await ValidateAcceptedTurnOutcomeWithRepairLoopAsync(
                        "late response GM",
                        rollbackSnapshot,
                        signalTurn ?? expectedTurn,
                        snapshotContext?.ProgressionControl))
                {
                    var lateResponse = await BuildGameResponseFromFiles();
                    if (lateResponse == null || string.IsNullOrEmpty(lateResponse.Response))
                        AnsiConsole.MarkupLine("[yellow dim]⚠ Нарратив пуст в late response GM[/]");
                    else
                    _lastResponse = lateResponse;
                    _pendingImagePrompt = null;
                    _gameLoop.IncrementTurn();
                    await CheckLifeTransitions();
                    await CheckAscensionTrigger();
                    if (await HasPendingMemoryLegacyAwaitingConsumptionAsync())
                        await FinalizePendingMemoryLegacyConsumptionAsync();

                    var qteHandling = await HandleAcceptedQteOfferAsync(lateResponse, snapshotContext);
                    if (!qteHandling.EarlyExit)
                    {
                        _lastResponse = qteHandling.Response;
                        _pendingImagePrompt = qteHandling.Response?.ImagePrompt;
                    }
                    else
                    {
                        await ApplyPendingShiningBlessingRuntimeEffectsAsync(snapshotContext);
                    }

                    if (IsIncarnationSourceLabel(snapshotContext?.SourceLabel))
                        await _worldDirectiveService.MaterializePendingToActiveAsync();

                    await ConsumeAfterlifeReturnProtectionIfNeededAsync(snapshotContext);
                    await ApplyPendingShiningBlessingRuntimeEffectsAsync(snapshotContext);
                }
                _fs.DeleteFile("ready/turn_complete.json");
                await CleanupPendingTurnSnapshotAsync();
            }

            // Check for GM-initiated incarnation (GM sends player to Mortal World)
            await CheckAscensionTrigger();
            await CheckGmIncarnationTrigger();

            var resumedQte = await _qteSceneService.ResumeActiveSceneIfAnyAsync(_gameLoop.TurnNumber);
            if (resumedQte != null)
            {
                _lastResponse = resumedQte.Response;
                _pendingImagePrompt = resumedQte.Response?.ImagePrompt;
                await ProcessMortalProgressionAfterAcceptedTurnAsync();
                await CheckLifeTransitions();
                await CheckAscensionTrigger();
                continue;
            }

            // Detect console resize — if width changed, just re-render (loop continues)
            try
            {
                var currentWidth = Console.WindowWidth;
                if (_lastConsoleWidth > 0 && currentWidth != _lastConsoleWidth)
                {
                    await NormalizeRuntimeUiArtifactsAsync();
                    await RefreshRuntimeStateAsync();
                }
                _lastConsoleWidth = currentWidth;
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or PlatformNotSupportedException)
            {
                // Some console hosts cannot report window size reliably; resize detection is best-effort.
            }

            // Render current state (preserve last response for dialogue options etc.)
            _ui.RenderGameScreen(_stateManager.CurrentState, _lastResponse, _gameLoop.TurnNumber);

            // Show scene image if pending (after game screen so it stays visible during input)
            if (!string.IsNullOrEmpty(_pendingImagePrompt))
            {
                await _imageService.ProcessSceneImagePrompt(_pendingImagePrompt);
                _pendingImagePrompt = null;
            }

            // Get player input (with Shift+Enter for multiline)
            var input = await GetPlayerInput();

            if (string.IsNullOrWhiteSpace(input))
                continue;

            // Check for in-game menu commands
            if (input.Equals("/refresh", StringComparison.OrdinalIgnoreCase) ||
                input.Equals("/обновить", StringComparison.OrdinalIgnoreCase))
            {
                await NormalizeRuntimeUiArtifactsAsync();
                await RefreshRuntimeStateAsync();
                var refreshedResponse = MergeWithLastResponse(await BuildGameResponseFromFiles());
                if (!await ValidateCurrentGameStateOrShowErrorsAsync("ручного обновления"))
                    continue;
                _lastResponse = refreshedResponse;
                _pendingImagePrompt = null; // Don't re-trigger image on refresh
                AnsiConsole.MarkupLine("[green]✔ Состояние игры обновлено из файлов[/]");
                await Task.Delay(600);
                continue; // Re-renders on next loop iteration
            }

            if (input.Equals("/options", StringComparison.OrdinalIgnoreCase) ||
                input.Equals("/опции", StringComparison.OrdinalIgnoreCase))
            {
                var shouldContinue = await InGameOptionsMenu();
                if (!shouldContinue)
                {
                    _inGame = false;
                    continue;
                }
                continue;
            }

            // Check for incarnation command (Chaos Sea → Mortal Life)
            if ((input.Equals("/incarnate", StringComparison.OrdinalIgnoreCase) ||
                 input.Equals("/воплотиться", StringComparison.OrdinalIgnoreCase)) &&
                _stateManager.CurrentState.IsInChaosSea)
            {
                await HandleIncarnation();
                continue;
            }

            if ((input.Equals("/reenter_shining_abode", StringComparison.OrdinalIgnoreCase) ||
                 input.Equals("/вернуться_в_обитель", StringComparison.OrdinalIgnoreCase)) &&
                _stateManager.CurrentState.IsInChaosSea)
            {
                await HandleReenterShiningAbode();
                continue;
            }

            if ((input.Equals("/new_game_plus", StringComparison.OrdinalIgnoreCase) ||
                 input.Equals("/новая_игра+", StringComparison.OrdinalIgnoreCase)) &&
                _stateManager.CurrentState.IsInShiningAbode)
            {
                await HandleNewGamePlus();
                continue;
            }

            if ((input.Equals("/return_to_chaos_sea", StringComparison.OrdinalIgnoreCase) ||
                 input.Equals("/вернуться_в_море_хаоса", StringComparison.OrdinalIgnoreCase)) &&
                _stateManager.CurrentState.IsInShiningAbode)
            {
                await HandleReturnToChaosSeaFromShiningAbode();
                continue;
            }

            // Check for end of life command (Mortal Life → Chaos Sea)
            if ((input.Equals("/end_of_life", StringComparison.OrdinalIgnoreCase) ||
                 input.Equals("/конец_жизни", StringComparison.OrdinalIgnoreCase)) &&
                !_stateManager.CurrentState.IsInAfterlifeRealm)
            {
                await HandleEndOfLife();
                continue;
            }

            // Check for local explorer commands
            if (_explorer.IsCommand(input))
            {
                var result = await _explorer.TryProcessCommand(input);
                if (result != null)
                {
                    // If the command produced a GM action (e.g., equip/unequip), send it
                    if (result.Length > 0)
                        await ProcessPlayerTurn(result);
                    continue;
                }

                // Recognized slash prefix but unknown command
                var cmd = input.Trim().Split(' ')[0];
                AnsiConsole.MarkupLine($"[yellow]⚠️ Неизвестная команда: {GameInterface.EscapeMarkup(cmd)}[/]");
                AnsiConsole.MarkupLine("[dim]Введите /help для списка доступных команд.[/]");
                continue;
            }

            // Send to GM
            await ProcessPlayerTurn(input);

            }
            catch (Exception ex)
            {
                LogError(ex);
                AnsiConsole.MarkupLine($"\n[red]❌ Ошибка в игровом цикле: {GameInterface.EscapeMarkup(ex.Message)}[/]");
                AnsiConsole.MarkupLine("[dim]Ошибка сохранена в game_session/error_log.txt. Данные не потеряны.[/]");
                AnsiConsole.MarkupLine($"[grey]{_loc.T("press_any_key")}[/]");
                Console.ReadKey(true);
            }
        }
    }

    private async Task ProcessPlayerTurn(string action, string? extraSystemReminder = null)
    {
        var clearsSystemGuardianAttraction = action.Contains("[CHAOS_SEA_SYSTEM_GUARDIAN_ATTRACTION:", StringComparison.OrdinalIgnoreCase);
        var clearsPendingAbodeOffering = action.Contains($"[INK_FEATHER_ACTION: {GuardianAbodeOfferingState.ActionTag}]", StringComparison.OrdinalIgnoreCase);
        var stagedExplorerRollback = _explorer.ConsumePendingLocalTurnRollbackSnapshot();

        // Create backup of game state files before sending turn (for escape-rollback)
        var backupId = DateTime.UtcNow.Ticks.ToString();
        var backedUpFiles = await CreatePreTurnBackup(backupId);
        OverlayExplorerLocalRollbackSnapshot(backedUpFiles, stagedExplorerRollback);

        // Write turn request
        var request = new TurnRequest
        {
            SessionId = _gameLoop.SessionId,
            TurnNumber = _gameLoop.TurnNumber + 1,
            PlayerAction = action,
            Timestamp = DateTime.UtcNow.ToString("o"),
            GameMode = _stateManager.Settings.AllowHistoryManipulation ? "debug" : "normal",
            SystemReminder = await BuildTurnSystemReminderAsync(extraSystemReminder)
        };
        await AttachPendingDiceAndGachaAsync(request);
        request.ProgressionControl = await _progressionSchedule.BuildControlForNextTurnAsync();
        var canonicalSnapshot = await CreateCanonicalBaselineSnapshotAsync(request, backedUpFiles, OrdinaryPlayerTurnSourceLabel);

        // Attach computed characteristics for GM reference
        try
        {
            var computed = await _charService.ComputeAsync();
            var charContext = new Dictionary<string, object>();
            foreach (var (name, stat) in computed.Stats)
            {
                charContext[name] = new
                {
                    standard = stat.BaseValue,
                    permanentlyModified = stat.PermanentlyModified,
                    modified = stat.Modified
                };
            }
            request.ComputedCharacteristics = new
            {
                playerLevel = computed.PlayerLevel,
                unspentStatPoints = computed.UnspentStatPoints,
                stats = charContext
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось вычислить характеристики для контекста");
        }

        ClearTransientOutputFiles();
        await _fs.WriteFileAtomicAsync("input/turn_request.json",
            JsonSerializer.Serialize(request, JsonOpts));

        if (await WaitForTerminalSignalAsync() == TerminalSignalWaitOutcome.Cancelled)
        {
            AnsiConsole.MarkupLine($"[yellow]{_loc.T("turn_cancelled")}[/]");
            // Delete the turn request, clean ready signals, and rollback game state
            _fs.DeleteFile("input/turn_request.json");
            _fs.DeleteFile("ready/turn_complete.json");
            _fs.DeleteFile("ready/turn_error.json");
            _fs.DeleteFile("output/ink_feather_action_result.json");
            _qteSceneService.ClearOfferFile();
            await RestorePreTurnBackup(backedUpFiles);
            AnsiConsole.MarkupLine("[dim]Изменения локально отменены, состояние восстановлено. Если GM завершит уже отправленный ход позже, он будет обработан как отложенный ответ.[/]");
            CleanupBackup(backedUpFiles);
            if (clearsSystemGuardianAttraction)
                _systemGuardianLibraryService.ClearAttractionRequest();
            if (clearsPendingAbodeOffering)
                GuardianAbodeOfferingState.Clear(_fs);
            return;
        }

        var activeManifest = await LoadPendingTurnSnapshotManifestAsync();
        var activeSnapshotContext = await LoadValidatedPendingTurnSnapshotContextAsync(activeManifest);
        var terminalOutcome = await ResolveFinalActiveTerminalOutcomeAsync(activeSnapshotContext, backedUpFiles);
        if (terminalOutcome.Kind == "failure")
            return;

        if (terminalOutcome.Kind == "error")
        {
            await ShowTurnErrorMessageAsync("ready/turn_error.json");
            _fs.DeleteFile("ready/turn_error.json");
            _fs.DeleteFile("output/ink_feather_action_result.json");
            _qteSceneService.ClearOfferFile();
            await CleanupPendingTurnSnapshotAsync();
            CleanupBackup(backedUpFiles);
            if (clearsPendingAbodeOffering)
                GuardianAbodeOfferingState.Clear(_fs);
            return;
        }

        // Read and validate the response before accepting the turn
        if (!await ValidateAcceptedTurnOutcomeWithRepairLoopAsync(
                "обработки хода",
                backedUpFiles,
                request.TurnNumber,
                request.ProgressionControl))
        {
            _fs.DeleteFile("ready/turn_complete.json");
            _fs.DeleteFile("ready/turn_error.json");
            _fs.DeleteFile("output/ink_feather_action_result.json");
            _qteSceneService.ClearOfferFile();
            _fs.DeleteFile("input/turn_request.json");
            await CleanupPendingTurnSnapshotAsync();
            if (clearsPendingAbodeOffering)
                GuardianAbodeOfferingState.Clear(_fs);
            return;
        }
        var response = await BuildGameResponseFromFiles();

        // Turn accepted — backup no longer needed
        CleanupBackup(backedUpFiles);
        if (clearsSystemGuardianAttraction)
            _systemGuardianLibraryService.ClearAttractionRequest();
        if (clearsPendingAbodeOffering)
            GuardianAbodeOfferingState.Clear(_fs);

        _gameLoop.IncrementTurn();
        await _pendingTurnState.RotateAfterAcceptedTurnAsync();
        _lastResponse = response;
        _pendingImagePrompt = null;

        // Persist turn to story file
        var state = _stateManager.CurrentState;
        await _storyService.AppendTurnAsync(
            _gameLoop.TurnNumber,
            state.CurrentRealm ?? "Chaos Sea",
            state.Incarnation,
            action,
            response?.Response,
            state.CurrentLocation,
            await ExtractStoryEntityRefsAsync(action));

        await ProcessMortalProgressionAfterAcceptedTurnAsync();

        // Check for GM-triggered life transitions
        await CheckLifeTransitions();
        await CheckAscensionTrigger();

        await ConsumeAfterlifeReturnProtectionIfNeededAsync(activeSnapshotContext);

        var qteHandling = await HandleAcceptedQteOfferAsync(response, activeSnapshotContext);
        if (qteHandling.EarlyExit)
            return;

        _lastResponse = qteHandling.Response;
        _pendingImagePrompt = qteHandling.Response?.ImagePrompt;

        await CleanupPendingTurnSnapshotAsync();

        // Autosave
        if (_stateManager.Settings.AutosaveIntervalTurns > 0 &&
            _gameLoop.TurnNumber % _stateManager.Settings.AutosaveIntervalTurns == 0)
        {
            await _saveLoad.AutosaveAsync(_gameLoop.TurnNumber);
        }

        // Cleanup ready signal
        _fs.DeleteFile("ready/turn_complete.json");
    }

    private void OverlayExplorerLocalRollbackSnapshot(
        RollbackSnapshot targetSnapshot,
        ExplorerMode.PendingLocalTurnRollbackSnapshot? stagedSnapshot)
    {
        if (stagedSnapshot == null)
            return;

        foreach (var trackedFile in stagedSnapshot.TrackedFiles)
        {
            if (stagedSnapshot.BaselineFiles.Contains(trackedFile))
            {
                targetSnapshot.BaselineFiles.Add(trackedFile);
                continue;
            }

            targetSnapshot.BaselineFiles.Remove(trackedFile);
            if (targetSnapshot.BackupFiles.TryGetValue(trackedFile, out var staleBackupPath))
            {
                if (_fs.FileExists(staleBackupPath))
                    _fs.DeleteFile(staleBackupPath);
                targetSnapshot.BackupFiles.Remove(trackedFile);
                targetSnapshot.BackupHashes.Remove(trackedFile);
            }
        }

        foreach (var (originalPath, explorerBackupPath) in stagedSnapshot.BackupFiles)
        {
            if (targetSnapshot.BackupFiles.TryGetValue(originalPath, out var staleBackupPath) &&
                !string.Equals(staleBackupPath, explorerBackupPath, StringComparison.OrdinalIgnoreCase) &&
                _fs.FileExists(staleBackupPath))
            {
                _fs.DeleteFile(staleBackupPath);
            }

            targetSnapshot.BackupFiles[originalPath] = explorerBackupPath;
            if (stagedSnapshot.BackupHashes.TryGetValue(originalPath, out var explorerBackupHash))
                targetSnapshot.BackupHashes[originalPath] = explorerBackupHash;
        }
    }

    private async Task<IReadOnlyCollection<StoryEntityRef>?> ExtractStoryEntityRefsAsync(
        string? action,
        IReadOnlyCollection<StoryEntityRef>? extraRefs = null)
    {
        var refs = new List<StoryEntityRef>();

        if (!string.IsNullOrWhiteSpace(action))
        {
            var guardianId = TryExtractTaggedValue(action, "guardianId")
                ?? TryExtractTaggedValue(action, "sourceGuardianId");
            if (!string.IsNullOrWhiteSpace(guardianId))
                refs.Add(await BuildStoryEntityRefAsync("guardian", guardianId));

            var residentId = TryExtractTaggedValue(action, "residentId")
                ?? TryExtractTaggedValue(action, "sourceResidentId");
            if (!string.IsNullOrWhiteSpace(residentId))
                refs.Add(await BuildStoryEntityRefAsync("resident", residentId));

            var npcId = TryExtractTaggedValue(action, "npcId")
                ?? TryExtractTaggedValue(action, "NPCId")
                ?? TryExtractTaggedValue(action, "sourceNpcId");
            if (!string.IsNullOrWhiteSpace(npcId))
                refs.Add(await BuildStoryEntityRefAsync("npc", npcId));
        }

        if (extraRefs != null)
            refs.AddRange(extraRefs.Where(reference => reference != null));

        var hasGuardianRef = refs.Any(reference =>
            string.Equals(reference.EntityType, "guardian", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(reference.EntityId));
        var hasResidentRef = refs.Any(reference =>
            string.Equals(reference.EntityType, "resident", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(reference.EntityId));
        if (!hasGuardianRef &&
            hasResidentRef &&
            _stateManager.CurrentState.IsInAfterlifeRealm)
        {
            var activeGuardianRefs = await BuildActiveGuardianStoryEntityRefsAsync();
            if (activeGuardianRefs != null)
                refs.AddRange(activeGuardianRefs);
        }

        if (refs.Count == 0)
            return null;

        return refs
            .GroupBy(reference => $"{reference.EntityType}:{reference.EntityId}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
    }

    private async Task<IReadOnlyCollection<StoryEntityRef>?> BuildActiveGuardianStoryEntityRefsAsync()
    {
        var guardiansJson = await _fs.ReadFileAsync("game_state/meta/guardians.json");
        if (string.IsNullOrWhiteSpace(guardiansJson))
            return null;

        try
        {
            using var guardiansDoc = JsonDocument.Parse(guardiansJson);
            if (!guardiansDoc.RootElement.TryGetProperty("activeGuardian", out var activeGuardian) ||
                activeGuardian.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var guardianId = TryGetString(activeGuardian, "guardianId");
            if (string.IsNullOrWhiteSpace(guardianId))
                return null;

            return new[] { await BuildStoryEntityRefAsync("guardian", guardianId) };
        }
        catch
        {
            return null;
        }
    }

    private async Task<IReadOnlyCollection<StoryEntityRef>?> BuildGuardianStoryEntityRefsAsync(string? guardianId)
    {
        if (string.IsNullOrWhiteSpace(guardianId))
            return null;

        return new[] { await BuildStoryEntityRefAsync("guardian", guardianId) };
    }

    private async Task<StoryEntityRef> BuildStoryEntityRefAsync(string entityType, string entityId)
    {
        return new StoryEntityRef
        {
            EntityType = entityType,
            EntityId = entityId,
            DisplayName = await ResolveStoryEntityDisplayNameAsync(entityType, entityId)
        };
    }

    private async Task<string?> ResolveStoryEntityDisplayNameAsync(string entityType, string entityId)
    {
        if (string.IsNullOrWhiteSpace(entityId))
            return null;

        return entityType switch
        {
            "guardian" => await ResolveGuardianDisplayNameAsync(entityId),
            "resident" => await ResolveResidentDisplayNameAsync(entityId),
            "npc" => await ResolveNpcDisplayNameAsync(entityId),
            _ => null
        };
    }

    private async Task<string?> ResolveGuardianDisplayNameAsync(string guardianId)
    {
        var guardiansJson = await _fs.ReadFileAsync("game_state/meta/guardians.json");
        if (string.IsNullOrWhiteSpace(guardiansJson))
            return null;

        try
        {
            if (JsonNode.Parse(guardiansJson) is not JsonObject guardiansRoot)
                return null;

            if (guardiansRoot["activeGuardian"] is JsonObject activeGuardian &&
                string.Equals(activeGuardian["guardianId"]?.GetValue<string>(), guardianId, StringComparison.OrdinalIgnoreCase))
            {
                return GuardianManifestation.GetDisplayName(activeGuardian) ??
                       activeGuardian["canonicalName"]?.GetValue<string>() ??
                       activeGuardian["name"]?.GetValue<string>();
            }

            if (guardiansRoot["guardians"] is JsonArray guardians)
            {
                var guardian = guardians.OfType<JsonObject>()
                    .FirstOrDefault(item => string.Equals(item["guardianId"]?.GetValue<string>(), guardianId, StringComparison.OrdinalIgnoreCase));
                if (guardian != null)
                {
                    return GuardianManifestation.GetDisplayName(guardian) ??
                           guardian["canonicalName"]?.GetValue<string>() ??
                           guardian["name"]?.GetValue<string>();
                }
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    private async Task<string?> ResolveResidentDisplayNameAsync(string residentId)
    {
        var residentsJson = await _fs.ReadFileAsync(GuardianAbodeResidentState.StatePath);
        if (string.IsNullOrWhiteSpace(residentsJson))
            return null;

        try
        {
            using var residentsDoc = JsonDocument.Parse(residentsJson);
            foreach (var resident in EnumerateStoryResidentObjects(residentsDoc.RootElement))
            {
                if (string.Equals(TryGetString(resident, "residentId"), residentId, StringComparison.OrdinalIgnoreCase))
                    return TryGetString(resident, "displayName");
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    private async Task<string?> ResolveNpcDisplayNameAsync(string npcId)
    {
        var npcJson = await _fs.ReadFileAsync("game_state/npcs/npc_core.json");
        if (string.IsNullOrWhiteSpace(npcJson))
            return null;

        try
        {
            using var npcDoc = JsonDocument.Parse(npcJson);
            foreach (var npc in EnumerateStoryNpcObjects(npcDoc.RootElement))
            {
                var currentNpcId = TryGetString(npc, "NPCId") ?? TryGetString(npc, "npcId") ?? TryGetString(npc, "id");
                if (!string.Equals(currentNpcId, npcId, StringComparison.OrdinalIgnoreCase))
                    continue;

                return TryGetString(npc, "NPCName") ??
                       TryGetString(npc, "npcName") ??
                       TryGetString(npc, "name") ??
                       TryGetString(npc, "displayName");
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    private static IEnumerable<JsonElement> EnumerateStoryResidentObjects(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
            yield break;

        if (!root.TryGetProperty("entries", out var entries) || entries.ValueKind != JsonValueKind.Array)
            yield break;

        foreach (var resident in entries.EnumerateArray())
        {
            if (resident.ValueKind == JsonValueKind.Object)
                yield return resident;
        }
    }

    private static IEnumerable<JsonElement> EnumerateStoryNpcObjects(JsonElement root)
    {
        foreach (var npc in GuardianPolicyContracts.EnumerateCanonicalNpcObjects(root))
            yield return npc;
    }

    private static string? TryGetString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var node) || node.ValueKind != JsonValueKind.String)
            return null;

        var text = node.GetString();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static string? TryExtractTaggedValue(string text, string key)
    {
        if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(key))
            return null;

        var needle = key + "=";
        var start = text.IndexOf(needle, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
            return null;

        start += needle.Length;
        var end = start;
        while (end < text.Length)
        {
            var ch = text[end];
            if (ch is ',' or ')' or ']' or ' ' or '\r' or '\n' or '\t')
                break;
            end++;
        }

        if (end <= start)
            return null;

        return text[start..end].Trim();
    }

    private async Task<(bool EarlyExit, GameResponse Response)> HandleAcceptedQteOfferAsync(
        GameResponse? response,
        ValidatedPendingTurnSnapshotContext? snapshotContext)
    {
        response ??= new GameResponse();
        var offer = await _qteSceneService.TryReadOfferAsync();
        if (offer == null)
        {
            await _qteSceneService.ClearDeclineMarkerAsync();
            return (false, response);
        }

        if (!QteSceneService.IsEligibleOfferSourceLabel(snapshotContext?.SourceLabel))
        {
            _logger.LogError(
                "QTE offer {QteId} получен вне обычного игрокского хода (SourceLabel={SourceLabel}) и будет проигнорирован.",
                offer.QteId,
                snapshotContext?.SourceLabel ?? "<missing>");
            _qteSceneService.ClearOfferFile();
            return (false, response);
        }

        var decision = await _qteSceneService.PromptOfferDecisionAsync(offer);
        if (decision == QteSceneService.QteOfferDecision.Decline)
        {
            await _qteSceneService.RecordDeclineAsync(offer, _gameLoop.TurnNumber);
            _fs.DeleteFile("ready/turn_complete.json");
            _fs.DeleteFile("ready/turn_error.json");

            var originalAction = snapshotContext?.PlayerAction;
            if (!string.IsNullOrWhiteSpace(originalAction) &&
                QteSceneService.IsEligibleOfferSourceLabel(snapshotContext?.SourceLabel))
            {
                var declineReminder =
                    $"[QTE_DECLINED:{offer.QteId}] Игрок отклонил QTE-сценарий. Разреши ту же ситуацию обычными игровыми механиками. Повторно предлагать этот qteId запрещено.";
                await ProcessPlayerTurn(originalAction, declineReminder);
            }

            return (true, response);
        }

        var completion = await _qteSceneService.StartAcceptedSceneAsync(offer, _gameLoop.TurnNumber);
        await ProcessMortalProgressionAfterAcceptedTurnAsync();
        await CheckLifeTransitions();
        await CheckAscensionTrigger();
        return (false, completion.Response);
    }

    private async Task ProcessMortalProgressionAfterAcceptedTurnAsync()
    {
        if (_stateManager.CurrentState.IsInAfterlifeRealm)
            return;

        await ProcessStatsIncreasedAsync();
        await _charService.ComputeAndWriteAsync();
        await CheckLevelUpAsync();
    }

    /// <summary>
    /// Reads statsIncreased from status_changes.json, applies +1 with Training Cap,
    /// awards XP compensation if blocked, then clears the statsIncreased field.
    /// </summary>
    private async Task ProcessStatsIncreasedAsync()
    {
        try
        {
            var json = await _fs.ReadFileAsync("game_state/player/status_changes.json");
            if (json == null) return;

            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("statsIncreased", out var si)) return;

            // Parse stats array
            var statsToIncrease = new List<string>();
            if (si.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in si.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                        statsToIncrease.Add(item.GetString() ?? "");
                }
            }

            if (statsToIncrease.Count == 0) return;

            var (applied, blocked) = await _charService.ApplyStatsIncreasedAsync(statsToIncrease.ToArray());

            // Award XP compensation for blocked stats
            if (blocked.Count > 0)
            {
                var expForNext = 100; // default
                var expJson = await _fs.ReadFileAsync("game_state/player/experience.json");
                if (expJson != null)
                {
                    try
                    {
                        using var expDoc = JsonDocument.Parse(expJson);
                        if (expDoc.RootElement.TryGetProperty("experienceForNextLevel", out var efn) &&
                            efn.ValueKind == JsonValueKind.Number)
                            expForNext = efn.GetInt32();
                    }
                    catch { /* use default */ }
                }
                var xpComp = Math.Max(25, (int)Math.Round(expForNext * 0.05));

                // Write compensation XP (will be picked up by state refresh)
                var compObj = new { experienceCompensation = xpComp * blocked.Count, reason = "Training Cap compensation" };
                _logger.LogInformation("Training Cap: {Count} стат заблокировано, XP компенсация: {XP}",
                    blocked.Count, xpComp * blocked.Count);
            }

            // Show notifications
            foreach (var stat in applied)
            {
                var ruName = Characteristics.RussianNames.GetValueOrDefault(stat, stat);
                AnsiConsole.MarkupLine($"  [green]📈 {Markup.Escape(ruName)} +1 (тренировка)[/]");
            }
            foreach (var stat in blocked)
            {
                var ruName = Characteristics.RussianNames.GetValueOrDefault(stat, stat);
                AnsiConsole.MarkupLine($"  [yellow]⚠ {Markup.Escape(ruName)}: Training Cap достигнут[/]");
            }

            // Clear statsIncreased after processing
            var dict = new Dictionary<string, object?>();
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.Name == "statsIncreased") continue;
                dict[prop.Name] = JsonSerializer.Deserialize<object>(prop.Value.GetRawText());
            }

            await _fs.WriteFileAtomicAsync("game_state/player/status_changes.json",
                JsonSerializer.Serialize(dict, JsonOpts));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ошибка обработки statsIncreased");
        }
    }

    /// <summary>
    /// Detects level-up by reading current level from player_status or experience.json
    /// and comparing with last known level. Grants 5 stat points on level-up.
    /// </summary>
    private async Task CheckLevelUpAsync()
    {
        try
        {
            var currentLevel = 1;

            // Check experience.json for level info (GM writes this)
            var expJson = await _fs.ReadFileAsync("game_state/player/experience.json");
            if (expJson != null)
            {
                using var doc = JsonDocument.Parse(expJson);
                if (doc.RootElement.TryGetProperty("level", out var lvl) &&
                    lvl.ValueKind == JsonValueKind.Number)
                    currentLevel = lvl.GetInt32();
                else if (doc.RootElement.TryGetProperty("playerLevel", out var pl) &&
                    pl.ValueKind == JsonValueKind.Number)
                    currentLevel = pl.GetInt32();
            }

            // Also check player_status for level
            if (currentLevel <= 1)
            {
                var statusJson = await _fs.ReadFileAsync("game_state/core/player_status.json");
                if (statusJson != null)
                {
                    using var doc = JsonDocument.Parse(statusJson);
                    if (doc.RootElement.TryGetProperty("level", out var lvl) &&
                        lvl.ValueKind == JsonValueKind.Number)
                        currentLevel = lvl.GetInt32();
                }
            }

            if (currentLevel > _lastKnownLevel)
            {
                var levelsGained = currentLevel - _lastKnownLevel;
                var totalPoints = levelsGained * 5;

                AnsiConsole.WriteLine();
                AnsiConsole.Write(new Rule("[gold1]⭐ ПОВЫШЕНИЕ УРОВНЯ![/]").RuleStyle("gold1"));
                AnsiConsole.MarkupLine($"  [bold yellow]Уровень {_lastKnownLevel} → {currentLevel}[/]");
                AnsiConsole.MarkupLine($"  [green]+{totalPoints} очков характеристик![/]");
                AnsiConsole.WriteLine();

                await _charService.AddStatPoints(totalPoints);
                _lastKnownLevel = currentLevel;

                // Offer stat distribution
                await ShowStatDistribution($"Повышение уровня! +{totalPoints} очков характеристик");
            }
            else
            {
                _lastKnownLevel = currentLevel;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ошибка проверки уровня");
        }
    }

    /// <summary>
    /// Checks for GM-triggered life transitions (death in mortal world → Chaos Sea).
    /// Sends life evaluation request to GM, waits for response with rewards, shows reward screen.
    /// </summary>
    private async Task CheckLifeTransitions()
    {
        var transJson = await _fs.ReadFileAsync("game_state/control/life_transitions.json");
        if (transJson == null) return;

        RollbackSnapshot? rollbackBackups = null;
        var localStateMutated = false;
        var manifestCreated = false;
        var requestDispatched = false;

        try
        {
            using var doc = JsonDocument.Parse(transJson);
            var root = doc.RootElement;

            // If TriggerLifeEnd is present, transition back to Chaos Sea
            if (!CanonicalStateNormalizer.TryReadCanonicalTriggerLifeEnd(root, out var reason, out var summary))
                return;

            JsonObject? currentSoulStateRoot = null;
            var currentSoulStateJson = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
            if (!string.IsNullOrWhiteSpace(currentSoulStateJson))
            {
                try
                {
                    currentSoulStateRoot = JsonNode.Parse(currentSoulStateJson) as JsonObject;
                }
                catch
                {
                    currentSoulStateRoot = null;
                }
            }

            var runtimeTriggerAuthority = await CanonicalStateNormalizer.ResolveLifecycleAuthorizedTriggerLifeEndFromPendingSnapshotAsync(
                _fs,
                transJson,
                currentSoulStateRoot);
            if (!runtimeTriggerAuthority.IsAuthorized)
            {
                throw new TriggerLifeEndRuntimeContextException(runtimeTriggerAuthority.Description);
            }

            rollbackBackups = await CreatePreTurnBackup(DateTime.UtcNow.Ticks.ToString());

            // === PHASE 1: Death screen ===
            AnsiConsole.WriteLine();
            AnsiConsole.Write(new FigletText("Death").Color(Color.DarkRed).Centered());
            AnsiConsole.Write(new Rule("[yellow]💀 Конец смертной жизни[/]").RuleStyle("yellow"));

            if (!string.IsNullOrEmpty(reason))
                AnsiConsole.MarkupLine($"[yellow]Причина: {GameInterface.EscapeMarkup(reason)}[/]");
            if (!string.IsNullOrEmpty(summary))
                AnsiConsole.MarkupLine($"[dim]{GameInterface.EscapeMarkup(summary)}[/]");

            AnsiConsole.MarkupLine($"\n[grey]{_loc.T("press_any_key")}[/]");
            Console.ReadKey(true);

            // === PHASE 2: Capture pre-death state for reward comparison ===
            var preDeathInkFeathers = _stateManager.CurrentState.InkFeathers;
            var preDeathEnlightenment = _stateManager.CurrentState.EnlightenmentTier;
            var preDeathSoulStateJson = await _fs.ReadFileAsync("game_state/meta/soul_state.json");

            // Build life summary for Guardian knowledge persistence
            var lifeSummary = BuildLifeSummary(summary);

            // Mark end of mortal life in story
            localStateMutated = true;
            var deathState = _stateManager.CurrentState;
            var lifecycleMarker = string.Equals(reason, "Voluntary", StringComparison.OrdinalIgnoreCase)
                ? "VOLUNTARY_END"
                : "DEATH";
            await _storyService.AppendMarkerAsync(
                "Mortal World", deathState.Incarnation,
                lifecycleMarker, $"Конец смертной жизни. Причина: {reason}. {summary}");

            // === PHASE 3: Update realm and send life evaluation to GM ===
            if (!await UpdateSoulStateRealm("Chaos Sea", lifeSummary))
                throw new InvalidOperationException("Не удалось безопасно обновить soul_state.currentRealm для перехода в Море Хаоса после завершения смертной жизни.");
            _fs.ClearCurrentWorldLore();

            // Clean up transition signal BEFORE sending turn (avoid re-trigger)
            _fs.DeleteFile("game_state/control/life_transitions.json");

            // Send life evaluation request to GM
            var evalRequest = new TurnRequest
            {
                SessionId = _gameLoop.SessionId,
                TurnNumber = _gameLoop.TurnNumber + 1,
                PlayerAction = "Душа покидает смертную оболочку. Начинается Оценка Жизни (Block 31.1). " +
                               "Рассчитай награду за прожитую жизнь: Чернильные Перья (формула из Block 31.1.2), " +
                               "обнови просветление (Block 31.1.3), запиши завершённую инкарнацию в metaStateUpdates. " +
                               "Создай Реликвии Души из значимых моментов жизни (Block 31.2). " +
                               "После оценки опиши возвращение в Море Хаоса к Хранителю. " +
                               $"Краткий итог жизни: {lifeSummary}",
                Timestamp = DateTime.UtcNow.ToString("o"),
                GameMode = "normal",
                SystemReminder = await BuildTurnSystemReminderAsync()
            };
            AttachFreshDiceAndGacha(evalRequest);
            evalRequest.ProgressionControl = await _progressionSchedule.BuildControlForNextTurnAsync("Chaos Sea");
            await CreateCanonicalBaselineSnapshotAsync(evalRequest, rollbackBackups, LifeEvaluationRewardAnalyzer.AutomaticLifeEvaluationSourceLabel);
            manifestCreated = true;

            ClearTransientOutputFiles();
            await _fs.WriteFileAtomicAsync("input/turn_request.json",
                JsonSerializer.Serialize(evalRequest, JsonOpts));
            requestDispatched = true;

            // Visual transition to Chaos Sea
            GameInterface.RenderRealmTransition(true);

            // === PHASE 4: Wait for GM response with life evaluation ===
            // Use raw wait — no turn increment, no recursive CheckLifeTransitions
            if (await WaitForGmResponseRaw())
            {
                var manifest = await LoadPendingTurnSnapshotManifestAsync();
                var snapshotContext = await LoadValidatedPendingTurnSnapshotContextAsync(manifest);
                if (!await ValidateAcceptedTurnOutcomeWithRepairLoopAsync(
                        "оценки жизни",
                        BuildValidatedRollbackSnapshot(snapshotContext),
                        _gameLoop.TurnNumber + 1,
                        evalRequest.ProgressionControl))
                {
                    _fs.DeleteFile("ready/turn_complete.json");
                    await CleanupPendingTurnSnapshotAsync();
                    return;
                }

                var evalResponse = await BuildGameResponseFromFiles();
                _gameLoop.IncrementTurn();
                _lastResponse = evalResponse;
                _pendingImagePrompt = evalResponse?.ImagePrompt;

                // Log the evaluation turn to story
                await _storyService.AppendTurnAsync(
                    _gameLoop.TurnNumber,
                    "Chaos Sea", 0,
                    "[LIFE_EVALUATION] Оценка прожитой жизни",
                    evalResponse?.Response,
                    "Море Хаоса",
                    await ExtractStoryEntityRefsAsync(
                        evalRequest.PlayerAction,
                        await BuildActiveGuardianStoryEntityRefsAsync()));

                // === PHASE 5: Show reward screen ===
                await ShowLifeEvaluationRewards(
                    preDeathInkFeathers,
                    preDeathEnlightenment,
                    preDeathSoulStateJson,
                    hasLifecycleAuthorizedTriggerLifeEnd: true);
                var guardianContext = await _afterlifeReturnGuardService.ReadActiveGuardianContextAsync();
                await _afterlifeReturnGuardService.ActivatePostLifeReturnAsync(
                    guardianContext.GuardianId,
                    guardianContext.GuardianName,
                    _gameLoop.TurnNumber);

                _fs.DeleteFile("ready/turn_complete.json");
                await CleanupPendingTurnSnapshotAsync();
            }
        }
        catch (TriggerLifeEndRuntimeContextException ex)
        {
            if (!requestDispatched)
                await CleanupUndispatchedTransitionPrepAsync(rollbackBackups, localStateMutated, manifestCreated);

            HandleInvalidTriggerLifeEndRuntimeFailure(_fs, ex);
            AnsiConsole.MarkupLine("[red]⚠ Клиент отклонил некорректный TriggerLifeEnd и очистил game_state/control/life_transitions.json.[/]");
            AnsiConsole.MarkupLine($"[dim]{GameInterface.EscapeMarkup(ex.Message)}[/]");
        }
        catch (Exception ex)
        {
            if (!requestDispatched)
                await CleanupUndispatchedTransitionPrepAsync(rollbackBackups, localStateMutated, manifestCreated);
            _logger.LogWarning(ex, "Ошибка обработки перехода жизни");
        }
    }

    internal static bool TryDescribeInvalidTriggerLifeEndRuntimeContext(
        string? preTriggerRealm,
        string? currentRealm,
        out string failureDescription)
    {
        if (!RealmSemantics.IsMortalRealm(preTriggerRealm))
        {
            failureDescription = string.IsNullOrWhiteSpace(preTriggerRealm)
                ? "Canonical TriggerLifeEnd runtime flow requires readable pre-turn mortal realm authority from pending snapshot soul_state."
                : $"Canonical TriggerLifeEnd runtime flow requires mortal pre-turn realm authority, but pending snapshot soul_state.currentRealm is '{preTriggerRealm}'.";
            return true;
        }

        if (RealmSemantics.IsAfterlifeRealm(currentRealm))
        {
            failureDescription =
                $"Canonical TriggerLifeEnd is present, but current soul_state.currentRealm is already '{currentRealm}' before runtime transition flow. Same-turn manual realm switch is invalid.";
            return true;
        }

        failureDescription = string.Empty;
        return false;
    }

    internal static void HandleInvalidTriggerLifeEndRuntimeFailure(FileSystemManager fs, Exception ex)
    {
        AppendErrorLogEntry(fs, ex);
        if (fs.FileExists("game_state/control/life_transitions.json"))
            fs.DeleteFile("game_state/control/life_transitions.json");
    }

    private static bool TryReadStrictSoulStateCurrentRealm(JsonElement soulStateRoot, out string currentRealm)
    {
        currentRealm = string.Empty;

        if (soulStateRoot.ValueKind != JsonValueKind.Object ||
            !soulStateRoot.TryGetProperty("currentRealm", out var currentRealmNode) ||
            currentRealmNode.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        currentRealm = (currentRealmNode.GetString() ?? string.Empty).Trim();
        return !string.IsNullOrWhiteSpace(currentRealm);
    }

    /// <summary>
    /// Displays life evaluation rewards — comparing before/after soul state.
    /// </summary>
    private async Task ShowLifeEvaluationRewards(
        int preDeathInkFeathers,
        string preDeathEnlightenment,
        string? preDeathSoulStateJson,
        bool hasLifecycleAuthorizedTriggerLifeEnd)
    {
        // Re-read soul state for latest values (GM should have updated it)
        await RefreshRuntimeStateAsync();
        await _afterlifeArchiveCandidateService.RefreshFromCurrentStateAsync();
        var state = _stateManager.CurrentState;

        var newInkFeathers = state.InkFeathers;
        var newEnlightenment = state.EnlightenmentTier;
        var feathersEarned = newInkFeathers - preDeathInkFeathers;

        var relicCount = 0;
        var newRelics = new List<string>();
        var soulJson = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        if (LifeEvaluationRewardAnalyzer.TryComputeDelta(
                preDeathSoulStateJson,
                soulJson,
                hasLifecycleAuthorizedTriggerLifeEnd,
                out var rewardDelta,
                out _) &&
            rewardDelta != null)
        {
            feathersEarned = rewardDelta.InkFeathersEarned;
            newRelics = rewardDelta.NewRelics
                .Select(relic => string.IsNullOrWhiteSpace(relic.Rarity)
                    ? relic.Name
                    : $"{relic.Name} ({relic.Rarity})")
                .ToList();
            relicCount = rewardDelta.NewRelics.Count;
        }

        // Build reward panel
        AnsiConsole.Clear();
        AnsiConsole.Write(new FigletText("Life Evaluation").Color(Color.Gold1).Centered());
        AnsiConsole.Write(new Rule("[gold1]✦ Оценка Прожитой Жизни ✦[/]").RuleStyle("gold1"));
        AnsiConsole.WriteLine();

        // Show narrative response first (GM's evaluation text)
        if (_lastResponse != null && !string.IsNullOrEmpty(_lastResponse.Response))
        {
            AnsiConsole.Write(new Panel(new Markup(GameInterface.EscapeMarkup(_lastResponse.Response)))
            {
                Header = new PanelHeader(" 📜 Слова Высших Сил ", Justify.Center),
                Border = BoxBorder.Double,
                BorderStyle = new Style(Color.Gold1),
                Padding = new Padding(2, 1),
                Expand = true
            });
            AnsiConsole.WriteLine();
        }

        // Rewards table
        var rewardsTable = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Gold1)
            .Expand()
            .AddColumn(new TableColumn("[bold gold1]Награда[/]").NoWrap())
            .AddColumn(new TableColumn("[bold gold1]Значение[/]"));

        // Ink Feathers
        var featherColor = feathersEarned > 0 ? "green" : "yellow";
        var featherSign = feathersEarned > 0 ? "+" : "";
        rewardsTable.AddRow(
            "🪶 Чернильные Перья",
            $"[{featherColor}]{featherSign}{feathersEarned}[/]  [dim]({preDeathInkFeathers} → {newInkFeathers})[/]");

        // Enlightenment
        var enlChanged = !string.Equals(preDeathEnlightenment, newEnlightenment, StringComparison.OrdinalIgnoreCase);
        rewardsTable.AddRow(
            "✨ Просветление",
            enlChanged
                ? $"[green]{GameInterface.EscapeMarkup(preDeathEnlightenment)} → {GameInterface.EscapeMarkup(newEnlightenment)}[/]"
                : $"[dim]{GameInterface.EscapeMarkup(newEnlightenment)}[/]");

        // Soul Relics
        rewardsTable.AddRow(
            "💎 Реликвии Души",
            relicCount > 0 ? $"[cyan]+{relicCount} новых[/]" : "[dim]Новых реликвий нет[/]");

        // Lives lived
        rewardsTable.AddRow(
            "🔄 Инкарнация",
            $"[white]#{state.Incarnation}[/]  [dim]({_gameLoop.TurnNumber} ходов прожито)[/]");

        AnsiConsole.Write(rewardsTable);

        // Show new relics if any
        if (newRelics.Count > 0)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.Write(new Rule("[cyan]💎 Реликвии[/]").RuleStyle("cyan"));
            foreach (var relic in newRelics)
                AnsiConsole.MarkupLine($"  [cyan]✦[/] {GameInterface.EscapeMarkup(relic)}");
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[dim]Вы вернулись в Море Хаоса. Ваш путь продолжается...[/]");
        AnsiConsole.MarkupLine($"\n[grey]{_loc.T("press_any_key")}[/]");
        Console.ReadKey(true);
    }

    /// <summary>
    /// Checks for GM-initiated incarnation trigger.
    /// GM can write game_state/control/incarnation_trigger.json to send the player to Mortal World.
    /// </summary>
    private async Task CheckGmIncarnationTrigger()
    {
        var triggerJson = await _fs.ReadFileAsync("game_state/control/incarnation_trigger.json");
        if (triggerJson == null) return;
        var isShiningBootstrapHandoff = _stateManager.CurrentState.IsInShiningAbodePendingBootstrap;
        if (!_stateManager.CurrentState.IsInChaosSea && !isShiningBootstrapHandoff)
        {
            _fs.DeleteFile("game_state/control/incarnation_trigger.json");
            return;
        }

        RollbackSnapshot? rollbackBackups = null;
        var localStateMutated = false;
        var manifestCreated = false;
        var requestDispatched = false;

        try
        {
            if (!IncarnationTriggerContract.TryParse(triggerJson, out var payload))
            {
                _fs.DeleteFile("game_state/control/incarnation_trigger.json");
                return;
            }

            JsonObject? preparedShiningPackage = null;
            if (isShiningBootstrapHandoff)
            {
                preparedShiningPackage = await TryReadPreparedShiningPackageAsync();
                if (preparedShiningPackage == null)
                {
                    _logger.LogWarning("Shining pending-bootstrap handoff detected, but preparedIncarnationPackage is unreadable or invalid. Deleting stale incarnation trigger and preserving the package for repair.");
                    _fs.DeleteFile("game_state/control/incarnation_trigger.json");
                    return;
                }
            }

            var rawReturnGuard = await _fs.ReadFileAsync(AfterlifeReturnGuardService.GuardPath);
            if (!isShiningBootstrapHandoff &&
                payload.IsGuardianForced &&
                !string.IsNullOrWhiteSpace(rawReturnGuard))
            {
                var guardSemanticState = AfterlifeReturnGuardService.Classify(rawReturnGuard, out var activeReturnGuard);
                if (guardSemanticState == AfterlifeReturnGuardSemanticState.BlockingInvalid)
                {
                    _logger.LogWarning(
                        "guardian_forced incarnation trigger ignored because afterlife_return_guard is invalid. Failing closed to preserve the protected return turn.");
                    _fs.DeleteFile("game_state/control/incarnation_trigger.json");
                    return;
                }

                if (guardSemanticState == AfterlifeReturnGuardSemanticState.ActiveValid && activeReturnGuard != null)
                {
                    _logger.LogWarning(
                        "guardian_forced incarnation trigger ignored because afterlife_return_guard is still active (remainingProtectedTurns={Turns}).",
                        activeReturnGuard.RemainingProtectedTurns);
                    _fs.DeleteFile("game_state/control/incarnation_trigger.json");
                    return;
                }
            }
            var worldDesc = payload.WorldDescription;
            var charDesc = payload.CharacterDescription;
            var circumstances = payload.Circumstances;

            // Show the GM-initiated incarnation banner
            AnsiConsole.Clear();
            AnsiConsole.Write(new FigletText("Soul Gates").Color(Color.Gold1).Centered());
            AnsiConsole.Write(payload.IsGuardianForced
                ? new Rule("[darkred]✦ Хранитель насильно распахивает Врата Души ✦[/]").RuleStyle("darkred")
                : new Rule("[gold1]✦ Врата Души открываются ✦[/]").RuleStyle("gold1"));
            AnsiConsole.WriteLine();
            if (payload.IsGuardianForced)
            {
                AnsiConsole.MarkupLine("[red]Враждебный Хранитель навязывает душе новое смертное воплощение.[/]");
                if (!string.IsNullOrWhiteSpace(payload.Reason))
                    AnsiConsole.MarkupLine($"[yellow]Причина санкции:[/] {GameInterface.EscapeMarkup(payload.Reason)}");
                if (!string.IsNullOrWhiteSpace(payload.ProvocationSummary))
                    AnsiConsole.MarkupLine($"[dim]Повод: {GameInterface.EscapeMarkup(payload.ProvocationSummary)}[/]");
            }
            else
            {
                AnsiConsole.MarkupLine("[yellow]Хранитель направляет вас через Врата Души в мир смертных...[/]");
            }

            if (!string.IsNullOrWhiteSpace(worldDesc))
                AnsiConsole.MarkupLine($"[dim]Мир: {GameInterface.EscapeMarkup(worldDesc)}[/]");
            if (!string.IsNullOrWhiteSpace(charDesc))
                AnsiConsole.MarkupLine($"[dim]Персонаж: {GameInterface.EscapeMarkup(charDesc)}[/]");
            if (!string.IsNullOrWhiteSpace(circumstances))
                AnsiConsole.MarkupLine($"[dim]Обстоятельства: {GameInterface.EscapeMarkup(circumstances)}[/]");

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[grey]{_loc.T("press_any_key")}[/]");
            Console.ReadKey(true);

            // Build incarnation action from GM-provided data
            var parts = new List<string>
            {
                payload.IsGuardianForced
                    ? "Враждебный Хранитель насильно отправляет душу через Врата Души в тяжёлую смертную жизнь."
                    : isShiningBootstrapHandoff
                        ? "Сияющая Обитель передаёт frozen blessing package в mortal bootstrap следующей жизни."
                        : "Хранитель направляет душу через Врата Души в мир смертных."
            };
            if (payload.IsGuardianForced)
            {
                if (!string.IsNullOrWhiteSpace(payload.GuardianId))
                    parts.Add($"Источник санкции: guardianId={payload.GuardianId}.");
                if (!string.IsNullOrWhiteSpace(payload.Reason))
                    parts.Add($"Причина: {payload.Reason}.");
                if (!string.IsNullOrWhiteSpace(payload.ProvocationSummary))
                    parts.Add($"Провокация игрока: {payload.ProvocationSummary}.");
                if (!string.IsNullOrWhiteSpace(payload.SeverityBand))
                    parts.Add($"Тяжесть старта: {payload.SeverityBand}.");
            }
            if (!string.IsNullOrWhiteSpace(charDesc))
                parts.Add($"Персонаж: {charDesc}.");
            if (!string.IsNullOrWhiteSpace(worldDesc))
                parts.Add($"Мир: {worldDesc}.");
            if (!string.IsNullOrWhiteSpace(circumstances))
                parts.Add($"Обстоятельства: {circumstances}.");

            if (preparedShiningPackage?["selectedCards"] is JsonArray selectedCards && selectedCards.Count > 0)
            {
                parts.Add($"Frozen Shining package несёт {selectedCards.Count} blessing card(s) в следующий mortal bootstrap.");
                foreach (var card in selectedCards.OfType<JsonObject>().Take(4))
                {
                    var displayName = GetNodeString(card["displayName"]);
                    var displaySummary = GetNodeString(card["displaySummary"]);
                    if (!string.IsNullOrWhiteSpace(displayName) || !string.IsNullOrWhiteSpace(displaySummary))
                        parts.Add($"Shining blessing: {displayName} — {displaySummary}".TrimEnd(' ', '—'));
                }
            }

            rollbackBackups = await CreatePreTurnBackup(DateTime.UtcNow.Ticks.ToString());

            // Each incarnation must create a fresh mortal-world lore set.
            _fs.ClearCurrentWorldLore();
            await _afterlifeReturnGuardService.ClearAsync();
            if (preparedShiningPackage != null)
                await ApplyPreparedShiningPackageToPendingWorldSetupAsync(preparedShiningPackage);

            // Update soul state: switch realm to Mortal World and increment incarnation
            localStateMutated = true;
            if (!await UpdateSoulStateRealm("Mortal World", incrementIncarnation: true))
                throw new InvalidOperationException("Не удалось безопасно обновить soul_state.currentRealm для начала новой смертной жизни.");
            await _rivalSoulArcService.ResetForNewLifeAsync();
            await _guardianCorrectionService.ApplyForNewLifeAsync(_stateManager.CurrentState.Incarnation + 1);
            await GuardianAbodeResidentRequestState.EnsureManifestationRequestForCurrentIncarnationAsync(_fs, "Mortal World");

            // Initialize fresh mortal status
            var status = new
            {
                healthPercentage = "100%",
                energyPercentage = "100%",
                poisePercentage = "100%",
                currentCondition = "Здоров",
                activeConditions = Array.Empty<string>(),
                money = 0
            };
            await _fs.WriteFileAtomicAsync("game_state/core/player_status.json",
                JsonSerializer.Serialize(status, JsonOpts));

            // Initialize empty mortal inventory
            var inventory = new
            {
                items = Array.Empty<object>(),
                equipment = new
                {
                    head = (object?)null, body = (object?)null, hands = (object?)null,
                    feet = (object?)null, mainHand = (object?)null, offHand = (object?)null,
                    neck = (object?)null, ring1 = (object?)null, ring2 = (object?)null
                },
                totalWeight = 0,
                maxWeight = 45
            };
            await _fs.WriteFileAtomicAsync("game_state/inventory/items.json",
                JsonSerializer.Serialize(inventory, JsonOpts));

            if (preparedShiningPackage != null)
            {
                var blessingResult = await ShiningBlessingEffectState.MaterializeForBootstrapAsync(
                    _fs,
                    preparedShiningPackage,
                    _stateManager.CurrentState.Incarnation + 1);
                if (!blessingResult.Success)
                {
                    _logger.LogWarning("Не удалось materialize pendingShiningBlessingEffects during bootstrap: {ErrorMessage}", blessingResult.ErrorMessage);
                }
                else if (blessingResult.SummaryLines.Count > 0)
                {
                    foreach (var line in blessingResult.SummaryLines)
                        parts.Add($"Blessing effect: {line}");

                    AnsiConsole.MarkupLine("[gold1]✨ Благословения Сияющей Обители активированы:[/]");
                    foreach (var line in blessingResult.SummaryLines)
                        AnsiConsole.MarkupLine($"  [gold1]•[/] {Markup.Escape(line)}");
                    AnsiConsole.WriteLine();
                }
            }

            // Mark new incarnation in story
            await _storyService.AppendMarkerAsync(
                "Chaos Sea", 0,
                "INCARNATION", $"Душа воплощается в новую смертную жизнь. Инкарнация #{_stateManager.CurrentState.Incarnation + 1}.",
                await BuildGuardianStoryEntityRefsAsync(payload.GuardianId));

            // Initialize characteristics for new incarnation
            await _charService.InitializeForNewIncarnation();
            var memoryLegacySummary = await ApplyPendingMemoryLegacyForIncarnationAsync();
            if (!string.IsNullOrWhiteSpace(memoryLegacySummary))
            {
                AnsiConsole.MarkupLine($"[magenta]🧠 Наследие Памяти:[/] {Markup.Escape(memoryLegacySummary)}");
                AnsiConsole.WriteLine();
                parts.Add($"Активировано Наследие Памяти: {memoryLegacySummary}.");
            }
            var shiningMemorySelectionSummary = await ConsumePendingShiningMemorySelectionAsync();
            if (!string.IsNullOrWhiteSpace(shiningMemorySelectionSummary))
                parts.Add($"Выбрана эхо-память Сияющей Обители: {shiningMemorySelectionSummary}.");
            await ShowStatDistribution("Новая инкарнация — распределите начальные очки характеристик");
            await CapturePendingMemoryLegacyApplicationAuditAsync();

            // Send incarnation turn to GM
            var request = new TurnRequest
            {
                SessionId = _gameLoop.SessionId,
                TurnNumber = _gameLoop.TurnNumber + 1,
                PlayerAction = string.Join(" ", parts),
                Timestamp = DateTime.UtcNow.ToString("o"),
                GameMode = "normal",
                SystemReminder = await BuildTurnSystemReminderAsync()
            };
            AttachFreshDiceAndGacha(request);
            request.ProgressionControl = await _progressionSchedule.BuildControlForNextTurnAsync("Mortal World");
            await CreateCanonicalBaselineSnapshotAsync(request, rollbackBackups, "GM-инициированного воплощения");
            manifestCreated = true;
            ClearTransientOutputFiles();
            await _fs.WriteFileAtomicAsync("input/turn_request.json",
                JsonSerializer.Serialize(request, JsonOpts));
            requestDispatched = true;

            // Clean up trigger file
            _fs.DeleteFile("game_state/control/incarnation_trigger.json");

            // Visual transition
            GameInterface.RenderRealmTransition(false);

            // Wait for GM response describing the new mortal world
            if (await WaitForGmResponse())
            {
                await RefreshRuntimeStateAsync();
                await _worldDirectiveService.MaterializePendingToActiveAsync(worldDesc, circumstances);
                if (preparedShiningPackage != null)
                    await ClearPreparedShiningPackageAfterBootstrapAsync();
            }
        }
        catch (Exception ex)
        {
            _pendingMemoryLegacyAwaitingConsumption = false;
            if (!requestDispatched)
                await CleanupUndispatchedTransitionPrepAsync(rollbackBackups, localStateMutated, manifestCreated);
            LogError(ex);
            _fs.DeleteFile("game_state/control/incarnation_trigger.json");
        }
    }

    private async Task CheckAscensionTrigger()
    {
        var ascensionJson = await _fs.ReadFileAsync("game_state/control/ascension.json");
        if (string.IsNullOrWhiteSpace(ascensionJson))
            return;

        if (!_stateManager.CurrentState.IsInAfterlifeRealm || _stateManager.CurrentState.IsInAnyShiningAbodeState)
        {
            _fs.DeleteFile("game_state/control/ascension.json");
            return;
        }

        try
        {
            using var doc = JsonDocument.Parse(ascensionJson);
            var root = doc.RootElement;
            var triggered =
                root.TryGetProperty("AscensionTrigger", out var legacyTrigger) &&
                legacyTrigger.ValueKind == JsonValueKind.True;
            var playerChoice = root.TryGetProperty("playerChoice", out var playerChoiceProp) &&
                               playerChoiceProp.ValueKind == JsonValueKind.String
                ? playerChoiceProp.GetString() ?? ""
                : "";

            if (!triggered || !string.Equals(playerChoice, "Ascension", StringComparison.OrdinalIgnoreCase))
            {
                _fs.DeleteFile("game_state/control/ascension.json");
                return;
            }

            if (_fs.FileExists("game_state/control/life_transitions.json") ||
                !await HasMaximumEnlightenmentAsync())
            {
                _fs.DeleteFile("game_state/control/ascension.json");
                return;
            }

            JsonObject? existingShiningRoot = null;
            var existingShiningJson = await _fs.ReadFileAsync(ShiningAbodeState.StatePath);
            if (!string.IsNullOrWhiteSpace(existingShiningJson))
            {
                try
                {
                    existingShiningRoot = JsonNode.Parse(existingShiningJson) as JsonObject;
                }
                catch
                {
                    existingShiningRoot = null;
                }
            }

            JsonObject? residentRoot = null;
            var residentJson = await _fs.ReadFileAsync(GuardianAbodeResidentState.StatePath);
            if (!string.IsNullOrWhiteSpace(residentJson))
            {
                try
                {
                    residentRoot = JsonNode.Parse(residentJson) as JsonObject;
                }
                catch
                {
                    residentRoot = null;
                }
            }

            JsonObject? guardiansRoot = null;
            var guardiansJson = await _fs.ReadFileAsync("game_state/meta/guardians.json");
            if (!string.IsNullOrWhiteSpace(guardiansJson))
            {
                try
                {
                    guardiansRoot = JsonNode.Parse(guardiansJson) as JsonObject;
                }
                catch
                {
                    guardiansRoot = null;
                }
            }

            var previousShiningJson = existingShiningJson;
            var previousSoulJson = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
            if (string.IsNullOrWhiteSpace(previousSoulJson))
                throw new InvalidOperationException("Не удалось прочитать soul_state.json для безопасного вознесения в Сияющую Обитель.");

            JsonObject soulRoot;
            try
            {
                soulRoot = JsonNode.Parse(previousSoulJson) as JsonObject
                    ?? throw new InvalidOperationException("soul_state.json должен быть object root для ascension flow.");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("soul_state.json повреждён и не позволяет безопасно завершить ascension flow.", ex);
            }

            var activatedShiningRoot = ShiningAbodeState.ActivateForAscension(existingShiningRoot, residentRoot, guardiansRoot);
            soulRoot["currentRealm"] = "Shining Abode";
            var nextSoulJson = GuardianPolicyContracts.CreateCanonicalSoulStateWriteRoot(soulRoot).ToJsonString(JsonOpts);
            if (!await TryCommitCoordinatedGameStateWritesAsync(
                    new CoordinatedGameStateWrite(ShiningAbodeState.StatePath, previousShiningJson, activatedShiningRoot.ToJsonString(JsonOpts)),
                    new CoordinatedGameStateWrite("game_state/meta/soul_state.json", previousSoulJson, nextSoulJson)))
            {
                throw new InvalidOperationException("Не удалось безопасно зафиксировать ascension handoff между shining_abode_state.json и soul_state.json.");
            }

            await SyncShiningReturnCycleLocalStateAsync();
            await _storyService.AppendMarkerAsync(
                "Shining Abode",
                _stateManager.CurrentState.Incarnation,
                "ASCENSION",
                "Душа достигла Сияющей Обители.",
                await BuildActiveGuardianStoryEntityRefsAsync());

            var shiningLorePath = "lore/shining_abode/realm_lore.json";
            if (!_fs.FileExists(shiningLorePath))
            {
                var defaultLore = new
                {
                    title = "Сияющая Обитель",
                    description = "Обитель вознесённых над Морем Хаоса. Место покоя, свободного ролеплея и встреч с Хранителями после завершения великого цикла."
                };
                await _fs.WriteFileAtomicAsync(shiningLorePath, JsonSerializer.Serialize(defaultLore, JsonOpts));
            }

            _fs.DeleteFile("game_state/control/ascension.json");
            GameInterface.RenderAscensionTransition();
            await RefreshRuntimeStateAsync();
        }
        catch (Exception ex)
        {
            LogError(ex);
            _fs.DeleteFile("game_state/control/ascension.json");
        }
    }

    /// <summary>
     /// Logs an error to game_session/error_log.txt for diagnostics.
     /// </summary>
    private void LogError(Exception ex)
    {
        AppendErrorLogEntry(_fs, ex);
    }

    internal static void AppendErrorLogEntry(FileSystemManager fs, Exception ex)
    {
        try
        {
            var logPath = Path.Combine(fs.GameSessionPath, "error_log.txt");
            var entry = $"[{DateTime.UtcNow:O}] {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}\n\n";
            File.AppendAllText(logPath, entry, System.Text.Encoding.UTF8);
        }
        catch
        {
            // Avoid recursive failures while attempting to record the original exception.
        }
    }

    internal sealed class TriggerLifeEndRuntimeContextException : InvalidOperationException
    {
        public TriggerLifeEndRuntimeContextException(string message)
            : base(message)
        {
        }
    }

    private async Task<bool> HasMaximumEnlightenmentAsync()
    {
        var soulJson = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        if (string.IsNullOrWhiteSpace(soulJson))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(soulJson);
            var root = doc.RootElement;

            if (root.TryGetProperty("soulProgression", out var progression) &&
                progression.ValueKind == JsonValueKind.Object)
            {
                if (progression.TryGetProperty("progressPercent", out var progressPercent) &&
                    progressPercent.ValueKind == JsonValueKind.Number &&
                    progressPercent.TryGetDouble(out var parsedPercent) &&
                    parsedPercent >= 100)
                {
                    return true;
                }

                if (progression.TryGetProperty("tier", out var tier) &&
                    tier.ValueKind == JsonValueKind.Number &&
                    tier.TryGetInt32(out var parsedTier) &&
                    parsedTier >= 4)
                {
                    return true;
                }

                if (progression.TryGetProperty("tierName", out var tierNameProp) &&
                    tierNameProp.ValueKind == JsonValueKind.String &&
                    IsTranscendenceTierName(tierNameProp.GetString()))
                {
                    return true;
                }
            }

            if (root.TryGetProperty("enlightenment", out var enlightenment))
            {
                if (enlightenment.ValueKind == JsonValueKind.Object)
                {
                    if (enlightenment.TryGetProperty("currentTier", out var currentTierProp) &&
                        currentTierProp.ValueKind == JsonValueKind.String &&
                        IsTranscendenceTierName(currentTierProp.GetString()))
                    {
                        return true;
                    }

                    if (enlightenment.TryGetProperty("level", out var levelProp) &&
                        levelProp.ValueKind == JsonValueKind.Number &&
                        levelProp.TryGetInt32(out var parsedLevel) &&
                        parsedLevel >= 4)
                    {
                        return true;
                    }

                    if (enlightenment.TryGetProperty("progressPercent", out var progressPercent) &&
                        progressPercent.ValueKind == JsonValueKind.Number &&
                        progressPercent.TryGetDouble(out var parsedPercent) &&
                        parsedPercent >= 100)
                    {
                        return true;
                    }
                }
                else if (enlightenment.ValueKind == JsonValueKind.Number &&
                         enlightenment.TryGetDouble(out var numericEnlightenment) &&
                         numericEnlightenment >= 100)
                {
                    return true;
                }
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    private static bool IsTranscendenceTierName(string? tierName)
    {
        return string.Equals(tierName, "Transcendence", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(tierName, "Трансценденция", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<string> GetPlayerInput()
    {
        AnsiConsole.WriteLine();

        // Show realm-aware prompt
        var isChaosSea = _stateManager.CurrentState.IsInChaosSea;
        var isShiningAbode = _stateManager.CurrentState.IsInShiningAbode;
        var isPendingShiningAbodeBootstrap = _stateManager.CurrentState.IsInShiningAbodePendingBootstrap;
        var isAfterlife = _stateManager.CurrentState.IsInAfterlifeRealm;
        var accentColor = isPendingShiningAbodeBootstrap
            ? "khaki1"
            : (isShiningAbode ? "yellow" : (isAfterlife ? "blue" : "green3"));
        var realmLabel = isPendingShiningAbodeBootstrap
            ? "Shining Abode Handoff"
            : (isShiningAbode ? _loc.T("realm_shining_abode") : (isAfterlife ? _loc.T("realm_chaos_sea") : _loc.T("realm_mortal")));
        AnsiConsole.Write(new Rule($"[bold {accentColor}]✦ Ваш ход ✦[/]").RuleStyle(accentColor));

        if (isPendingShiningAbodeBootstrap)
        {
            AnsiConsole.MarkupLine("[dim]  Подготовка следующей жизни уже передана в bootstrap.[/]");
            AnsiConsole.MarkupLine("[dim]  Обычные действия Мира Хаоса и Сияющей Обители здесь недоступны.[/]");
        }
        else if (isShiningAbode)
        {
            AnsiConsole.MarkupLine("[dim]  Свободный ролеплей с Хранителями в Сияющей Обители[/]");
            AnsiConsole.MarkupLine("[dim]  /реликвии /хранители /душа │ /вернуться_в_море_хаоса /новая_игра+ │ /help[/]");
        }
        else if (isChaosSea)
        {
            AnsiConsole.MarkupLine("[dim]  Говорите с Хранителем: торговать, квесты, реликвии души, вытягивание реликвий, сменить хранителя[/]");
            if (_stateManager.CurrentState.CanReenterShiningAbode)
                AnsiConsole.MarkupLine("[dim]  /реликвии /хранители /гача /душа │ /воплотиться /вернуться_в_обитель │ /help[/]");
            else
                AnsiConsole.MarkupLine("[dim]  /реликвии /хранители /гача /душа │ /воплотиться │ /help[/]");
        }
        else
        {
            AnsiConsole.MarkupLine("[dim]  /инв /квесты /карта /статус │ /конец_жизни │ /help[/]");
        }

        // Show option hint if dialogue options are available
        if (_lastResponse?.DialogueOptions != null && _lastResponse.DialogueOptions.Length > 0)
            AnsiConsole.MarkupLine("[dim]  Введите [cyan]номер[/] опции или свой текст. Большую вставку можно вставить прямо сюда; [cyan]\\m[/] открывает текстовый редактор, [cyan]\\p[/] остаётся fallback-вставкой[/]");
        else
            AnsiConsole.MarkupLine("[dim]  Enter = отправить │ большую вставку можно вставить прямо сюда │ \\m = текстовый редактор │ \\p = fallback-вставка[/]");

        // Single-line mode by default: Enter sends immediately
        var promptChar = isChaosSea ? "🌊" : "⚔️";
        var firstLine = TextComposer.Read(
            StandardTextComposerConsole.Instance,
            _clipboardService,
            new TextComposerOptions
            {
                PromptMarkup = $"[bold {accentColor}] {promptChar} > [/]",
                PreserveNewlines = true
            });

        if (IsClipboardPasteShortcut(firstLine))
            return ResolveClipboardPlayerInput();

        // Check for slash commands — always single-line, send immediately
        if (!firstLine.Contains('\n') && firstLine.TrimStart().StartsWith('/'))
            return firstLine.Trim();

        // Check for multiline trigger (Ctrl+M marker)
        if (firstLine.Equals("\\m", StringComparison.OrdinalIgnoreCase) ||
            firstLine.Equals("/multiline", StringComparison.OrdinalIgnoreCase) ||
            firstLine.Equals("/мульти", StringComparison.OrdinalIgnoreCase))
        {
            return await GetMultilineInput();
        }

        // Check for dialogue option number shortcuts
        if (!firstLine.Contains('\n') && int.TryParse(firstLine.Trim(), out var optNum))
        {
            // Try to resolve to actual dialogue option text
            if (_lastResponse?.DialogueOptions != null && optNum >= 1 && optNum <= _lastResponse.DialogueOptions.Length)
            {
                var optionText = _lastResponse.DialogueOptions[optNum - 1].Text;
                if (!string.IsNullOrEmpty(optionText))
                    return optionText;
            }
            // If no matching option, send as-is
            return firstLine.Trim();
        }

        // Regular single-line input — send directly on Enter
        return firstLine.Trim();
    }

    /// <summary>
    /// Multiline input mode: type multiple lines, empty line sends.
    /// Activated by typing \m or /multiline.
    /// </summary>
    private Task<string> GetMultilineInput()
    {
        var value = TextComposer.Read(
            StandardTextComposerConsole.Instance,
            _clipboardService,
            new TextComposerOptions
            {
                PromptMarkup = "[cyan]│[/]",
                PreserveNewlines = true,
                Mode = TextComposerMode.MultilineEditor,
                HelpMarkup = "[dim](Многострочный режим. Вставка из буфера работает напрямую. Две пустые строки подряд = отправить. \\p = fallback из буфера.)[/]"
            });

        return Task.FromResult(value);
    }

    private static bool IsClipboardPasteShortcut(string input)
    {
        var trimmed = input.Trim();
        return trimmed.Equals("\\p", StringComparison.OrdinalIgnoreCase) ||
               trimmed.Equals("/paste", StringComparison.OrdinalIgnoreCase) ||
               trimmed.Equals("/вставить", StringComparison.OrdinalIgnoreCase);
    }

    private string ResolveClipboardPlayerInput()
    {
        var result = _clipboardService.TryReadText();
        if (!result.Success || string.IsNullOrWhiteSpace(result.Text))
        {
            AnsiConsole.MarkupLine($"[yellow]{GameInterface.EscapeMarkup(result.Error ?? "Не удалось прочитать буфер обмена.")}[/]");
            return string.Empty;
        }

        return result.Text!;
    }
    private static int[] GenerateSecureDice() => GameLoop.GenerateSecureRandomDice();

    /// <summary>
    /// Generates a fresh GM-facing dice pool and a separate client-computed gacha base.
    /// </summary>
    private static void AttachFreshDiceAndGacha(TurnRequest request)
    {
        var visibleDice = GenerateSecureDice();
        var hiddenGachaDice = GameLoop.GenerateSecureRandomDice(4);
        request.PreGeneratedDices1d20 = visibleDice;
        request.GachaBaseResult = GameLoop.ComputeGachaBase(hiddenGachaDice);
    }

    private async Task AttachPendingDiceAndGachaAsync(TurnRequest request)
    {
        var pending = await _pendingTurnState.GetOrCreateAsync();
        request.PreGeneratedDices1d20 = pending.PreGeneratedDices1d20;
        request.GachaBaseResult = pending.GachaBaseResult;
    }

    /// <summary>
    /// Builds a system reminder for the GM, reinforcing game-specific rules.
    /// </summary>
    private string BuildSystemReminder()
    {
        return @"CRITICAL SYSTEM REMINDER — This is NOT a D&D system!

CHARACTERISTICS: Range 1-100 (not 3-18). Base stats start at 1 per characteristic. Players distribute points (8 at incarnation + 5 per level). Do NOT use D&D-style modifiers like (stat-10)/2.

ACTION CHECK FORMULA (Block 12):
  StatModificator = CappedStatValue + LevelScaling
  where CappedStatValue = min(CharacterLevel*0.5+20, StatValueWithBonuses)
  and LevelScaling = floor(CharacterLevel * 0.8)
  Difference = (PlayerDiceResult + StatModificator) - (GMDice + ActionDifficultModificator)

The 'computedCharacteristics' field in this request contains the client-computed standard, permanently modified, and fully modified values for each characteristic. USE THESE VALUES for action checks — do not recalculate from scratch.

statsIncreased: The client automatically applies +1 to base stats with Training Cap enforcement (stat < PlayerLevel*2). You do NOT need to use setCharacteristics for training increases.

setCharacteristics: Use ONLY for extraordinary events (divine intervention, meta-commands). It bypasses the Training Cap.

REALM SEGREGATION — ABSOLUTE LAW:
Read Context.worldState.currentRealm (projected from game_state/meta/soul_state.json.currentRealm) BEFORE applying any mechanic.

IF Context.worldState.currentRealm = Shining Abode AND game_state/meta/shining_abode_state.json.preparedIncarnationPackage != null:
  TREAT THIS AS Shining Abode pending-bootstrap handoff, NOT as ordinary active Shining Abode.
  ALLOWED: only mortal bootstrap / next-life materialization. GM MUST preserve game_state/meta/shining_abode_state.json.preparedIncarnationPackage exactly as provided; do not remove, clear, rename, or mutate it. The client runtime consumes and clears the frozen package only after successful Mortal World bootstrap.
  FORBIDDEN: ordinary Guardian interactions, ordinary Abode interactions, ordinary afterlife interactions, archive/relic/world-setup meta flows, Mortal World systems.

ELSE IF REALM = Chaos Sea:
  FORBIDDEN: experienceGained, statsIncreased, statsDecreased, currentPoiseChange, currentEnergyChange, currentHealthChange, moneyChange, activeSkillChanges, passiveSkillChanges, skillMasteryChanges, UpdateInventory, UpdateNPCs, NPCsInScene, UpdateQuests, worldEventsLog, factionDataChanges, currentLocationData, timeChange, setWorldTime, weatherChange, enemiesData, alliesData, combat_log_markdown.
  ALLOWED: UpdateGuardians, Soul Relic systems, Ink Feather spending, Gacha, guardian/abode afterlife interactions, Life Evaluation, Incarnation setup.
  AFTERLIFE INK FEATHER EXCEPTIONS: Donate to Guardian, Cultivate Enlightenment, Guardian Favor, Memory Gates, Soul Imprint, ABODE_OFFERING.
  Sell Relic is a separate guardian trade interaction, not an Ink Feather action.
  If game_state/meta/shining_abode_state.json.availability = active and afterlife_return_guard is absent, or semantic-valid (`reason=post_life_return`) and inactive, the player MAY use the client-owned local command /reenter_shining_abode to re-enter the already-active Shining Abode. A malformed guard or a parsed guard with the wrong reason still blocks re-entry until client normalization clears it. This is an ordinary return route, not Ascension, and not a GM-authored turn.
  LIFE EVALUATION REWARD GUARANTEE:
    - Every completed mortal life MUST grant at least 10 Ink Feathers.
    - Every completed mortal life MUST grant at least one NEW Soul Relic with a new relicId.
    - Reward quality may vary by achievements, but zero-reward life evaluation is a protocol violation.
    - If the completed life clearly strengthened a Guardian's domain, you MAY emit guardianPowerEvents with reasonType=resonance and full resonanceAudit on the dedicated Life Evaluation turn only.

ELSE IF REALM = Shining Abode:
  FORBIDDEN: experienceGained, statsIncreased, statsDecreased, currentPoiseChange, currentEnergyChange, currentHealthChange, moneyChange, activeSkillChanges, passiveSkillChanges, skillMasteryChanges, UpdateInventory, UpdateNPCs, NPCsInScene, UpdateQuests, worldEventsLog, factionDataChanges, currentLocationData, timeChange, setWorldTime, weatherChange, enemiesData, alliesData, combat_log_markdown.
  ALLOWED: UpdateGuardians, Soul Relic systems, Ink Feather spending, Gacha, Abode/Guardian interactions, Life Evaluation, Incarnation setup.
  AFTERLIFE INK FEATHER EXCEPTIONS: Donate to Guardian, Cultivate Enlightenment, Guardian Favor, Memory Gates, Soul Imprint, ABODE_OFFERING.
  Shining Abode is the ascended endgame free-roleplay zone above the Chaos Sea. It still uses afterlife/guardian systems, not Mortal World systems.
  The player may use the client-owned local command /return_to_chaos_sea to return to Chaos Sea and seal the Shining Abode without triggering destructive New Game+ reset.
  Optional New Game+ from Shining Abode is the separate destructive global reset path: it returns to Chaos Sea with Enlightenment and Ink Feathers reset while Soul Relics and Guardians are preserved.

IF REALM = Mortal World:
  FORBIDDEN: UpdateGuardians, Guardian-specific reputation/project/musings/lore commands, Abode navigation, Soul Relic Gacha, afterlife-only spending of Ink Feathers.
  ALLOWED: combat, NPCs, quests, inventory, factions, weather, time, world progression.
  MORTAL-WORLD INK FEATHER EXCEPTIONS: Reveal Fate, Rewrite Fate, Sacrifice to Chaos, Absorb Feathers, Learn Skill, Fate Shield, Seal in Ink.
  LOCAL NPC TRADE: Some NPCs may have a client-side Buy/Sell panel for mortal-world goods only. This panel does NOT create turn_request.json, does NOT use Ink Feathers, and does NOT trade Soul Relics.
  If the player later asks a merchant NPC about an item just bought from that merchant's local stock, treat the item as known to that merchant and do not act surprised by its existence.
  QTE OFFERS: game_state/core/game_settings.json.qteEventsEnabled controls whether QTE is allowed.
    - If qteEventsEnabled = false, DO NOT write output/qte_offer.json.
    - QTE is a rare cinematic tool, not a replacement for normal action checks.
    - QTE is allowed only in Mortal World and only on an ordinary player-driven turn (not incarnation, life evaluation, repair, transition, or other system flow).
    - QTE offer turn MUST NOT also resolve ordinary state changes for the same situation; leave game_state/lore/stories untouched and write only output/qte_offer.json plus narrative/interface/debug outputs.
    - QTE offer is delivered through output/qte_offer.json and then resolved locally by the client after player Accept/Decline.
    - qte_offer.json MUST define startChapterId; chapter array order does not define the scene start.
    - QTE primaryCharacteristic MUST use canonical lowercase stat ids (strength, dexterity, constitution, intelligence, wisdom, faith, attractiveness, trade, persuasion, perception, luck, speed).
    - For BranchChoice, check.config.choiceGrade MUST be exactly success, partial, or fail.
    - Every terminal outcome MUST carry a complete responseFragment for local application.
    - declineHint and cinematicJustification, if provided, are shown to the player in the offer prompt; keep them concise.
    - responseFragment MUST NOT use ordinary image_prompt; use sceneImagePrompt / chapterImagePrompt / outcomeImagePrompt instead.
    - Successful QTE terminal outcomes MUST grant positive experienceGained at minimum; the client will locally add it to the authoritative XP counter in experience.json.
    - If experience.json already contains level/progress metadata (level or playerLevel, experience or currentExperience, experienceForNextLevel), the client will also process the local level-up transition.

The Mortal-World and Chaos-Sea Ink Feather whitelists are mutually exclusive.

LORE / META BOOTSTRAP — HARD REQUIREMENT:
  - On the first Chaos Sea turn of a new game, create:
    lore/chaos_sea/cosmology.json
    lore/chaos_sea/soul_system_lore.json
    lore/chaos_sea/guardians_lore.json
    lore/codex_entries.json
    game_state/meta/achievements.json
  - On every new Mortal World incarnation, create:
    lore/current_world/world_setting.json
    lore/current_world/geography.json
    lore/current_world/history.json
    lore/current_world/cultures.json
    lore/current_world/threats.json
  - Optional supplemental Mortal-World lore: lore/current_world/npcs_lore.json when this life needs persistent NPC backstory/world-lore support.
  - Missing bootstrap lore/codex/achievement files will cause client validation failure.

QUEST UPDATE PROTOCOL — HARD REQUIREMENT:
  - On quest creation, send the full quest object with detailsLog.
  - On quest-log updates, send questId + newDetailsLogEntry instead of resending the whole detailsLog array.
  - quest_history.json is canonically stored as questHistory + questRewards + questChains; legacy questLog is only shorthand input.

PROGRESSION CONTROL — CLIENT-AUTHORITATIVE SCHEDULER:
This request contains a 'progressionControl' object. Treat it as authoritative system control, not optional advice.
  - In Mortal World, it defines the baseline world time and mandatory 240-minute world cycles / 1440-minute faction cycles.
  - In Chaos Sea, it defines mandatory bounded hub / guardian-project / resident-agency cycles for this turn.
  - In Shining Abode, it defines mandatory bounded Shining Abode / Shining faction / Shining trade / guardian-project / resident-agency cycles for this turn.
  - If afterlifeCatchupRequired=true, process only afterlifeCatchupSummaryEventsRequired bounded summary outcomes. Do NOT simulate every raw elapsed afterlife cycle.
  - If a mustEvaluate* flag is true, that contour MUST be processed this turn.
  - If a mustEvaluate* flag is false, there is no mandatory progression debt for that contour this turn.
You MUST evaluate and process all required cycles for the active realm.
If progression is processed, you MUST write progressionProcessingReport to game_state/control/progression_report.json and report the exact processed cycle counts and new last-* markers.
If no cycles are due, you may write zero counts or omit the report.

If a forbidden key appears in your draft response for the active realm, REMOVE it before finalizing.

NPC AGENCY — HARD REQUIREMENT:
You MUST declare NPC reasoning scope BEFORE narration instead of silently skipping or guessing it.
Your gm_thoughts_markdown MUST contain:
## Охват NPC-анализа
- Режим / Mode: [Scene-local | World-progression | Guardian-centric | Mixed]
- Релевантные акторы / Relevant actors: [...]
- Почему они релевантны / Why they are relevant: ...
- Акторы вне охвата / Actors outside scope: [...]
- Почему они вне охвата / Why they are outside scope: ...
Scene-local MAY use `Relevant actors: нет` only when the turn truly has no actor that must reason or react with agency.
Then, for every declared relevant actor, you MUST provide a reasoning block:
### [Actor Name]
- Текущая локация / Current location
- Ситуация / Current situation
- Мысли / Internal thoughts
- Действия / Intended actions
For EVERY relevant NPC block, the current-location line is mandatory: explicitly state where the NPC is now and whether they stay there or relocate this turn.
Missing scope declaration or missing/empty actor reasoning blocks will cause client rejection.
If you narrate a meaningful NPC reaction or introduce a new named NPC, you MUST also register/update the relevant NPC state. Narrative-only NPCs without state consequences are protocol violations.
If you emit structured actor updates such as UpdateNPCs, NPCGoalUpdates, NPCActivityUpdates, or UpdateGuardians, those actors MUST appear in Relevant actors and MUST have full reasoning blocks. Scene-local with `Relevant actors: нет` is valid only when no structured actor updates are emitted.
The same scope discipline applies to afterlife residents when you change them with UpdateGuardianAbodeResidents, residentThoughtJournalUpdates, residentInteractionLogUpdates, or UpdateGuardianAbodeResidentHistoryLog.
If a turn changes a resident's abode devotion, restlessness, migration state, or other resident-facing social state, that resident MUST appear in Relevant actors and MUST have a reasoning block.

GUARDIAN AGENCY — HARD REQUIREMENT:
In Chaos Sea, use the same declared-scope model for relevant Guardians.
For Guardian-centric turns, the active Guardian MUST appear in the declared relevant actors and MUST have a full reasoning block before narration if activeGuardian is explicitly set in state.
Do NOT skip Guardian reasoning just because the player is the current conversational focus.

ETERNAL GUARDIAN PRESETS:
For player-facing roleplay, these are called Eternal Guardians. In technical files/contracts, they are still named system guardian presets.
If the client-selected guardian request references an Eternal Guardian preset, you MUST materialize that exact named guardian rather than inventing a nearby substitute.
When you create or rematerialize a guardian from an Eternal Guardian preset, write guardian.sourcePreset with:
  - presetId
  - displayName
  - version
  - library
Do NOT drop sourcePreset metadata for client-selected Eternal Guardians.
Canonical guardian identity now uses:
  - canonicalName
  - nameVariants { default, optional feminine/masculine/neutral }
  - manifestation { currentDisplayName, formFlexibility, currentPresentationStyle, currentPronouns, appearanceDescription, optional presentationReason }
  - manifestationHistory (past forms only; may be empty)
Do NOT rely on legacy guardian.name as the primary identity field.
If the Guardian changes visible form, update manifestation and move the previous form into manifestationHistory.

GUARDIAN PROJECT TRACKER — AFTERLIFE ONLY:
Guardian project lifecycle no longer uses UpdateGuardians.updateProject.
Use these dedicated top-level surfaces instead:
  - startGuardianProjects
  - guardianProjectUpdates
  - completeGuardianProjects
  - guardianPowerEvents
  - afterlifeArchiveUpdates
Authoritative tracker lives in game_state/meta/guardian_projects.json.
Player-facing readable chronology lives in game_state/meta/guardian_project_journal.json.
Player-facing power history lives in game_state/meta/abode_power_journal.json.
Afterlife-owned lore/secret offerings live in game_state/meta/soul_state.json.afterlifeArchive.stored.
If the player initiates archive consultation or archive project fuel, read the matching pending request in game_state/control/ and materialize a canonical result; the client does NOT derive archive compatibility from guardian domain.
Keep at most one active guardian project per guardian in v1.
Any Abode Power change must go through guardianPowerEvents or be materialized by the client from project completion into guardianPowerEvents-compatible history.
Guardian project progress belongs to the tracker surfaces, not to raw guardian.currentProject.
Do NOT mutate guardian.abodePower.currentPower directly in narrative surfaces without a matching power event and audit trail.
If guardianProjectUpdates carries meaningful project help or sabotage, include structured assistAudit / sabotageAudit:
  - assistAudit uses DomainRelevance, RiskOrCost, ScarcityOrUniqueness, DirectProjectImpact, assistScore, classification
  - sabotageAudit uses HostileReach, ProjectExposure, DamageIntent, DamageAchieved, PlayerComplicity, sabotageSeverityScore, classification
The client may materialize project_assist / rival_defense / rival_strike power events from those audits.
For political project terminals:
  - completed offensive_intrigue must point at targetGuardianId; the client will deterministically compute targetLoss, pressureDelta, and stabilityDamage from current powers and political shields
  - completed counter_rival_operation must point at targetGuardianId; the client will deterministically apply pressure/stability relief to the rival active pressure project if it exists
  - guardians use guardianRelationships as the canonical directed inter-guardian standing network with attitudeScore/attitudeTier; prefer rival/enemy targets, treat competitive targets as lower-priority valid pressure, treat neutral targets as valid but weakly motivated pressure, and require an explicit betrayal reason before targeting ally/trusted guardians
  - temporary anti-target coordination is valid only as derived coalition behavior: two Guardians may align against the same third Guardian only when they are non-hostile toward each other, both mark that third Guardian as rival/enemy, and there is an explicit current political project trace against that same target
  - completed abode_fortification materializes persistent safePressure / defenseRating bonuses
  - sabotaged abode_fortification may leave a temporaryProjectModifier for the next internal project start

NEXT-LIFE SCENARIO CORE / GUARDIAN CORRECTIONS:
The client may maintain a machine-readable next-life scenario manifest at game_state/control/next_life_scenario_core.json.
Treat scenarioCoreAssertions in that file as hard confirmed start facts.
candidateAssertions in that file are NOT binding unless the client already promoted them into scenarioCoreAssertions.
The client may also maintain game_state/control/archive_candidate_manifest.json during Life Evaluation.
Treat archive_candidate_manifest.json as client-authored intake state for codex-derived discoveries that the player may preserve into the afterlife archive.
The client may maintain:
  - game_state/control/pending_archive_consultation_request.json
  - game_state/control/pending_archive_project_fuel_request.json
Treat them as client-authored requests over a reserved archive entry. The entry is locked client-side but not yet consumed.
Do NOT overwrite the pending files in GM output.
Resolve them by:
  - materializing the canonical result in guardian project tracker/journal state when accepted
  - and returning archiveActionResolutions in soul_state with requestId, archiveId, requestedMode and status=accepted|rejected|cancelled.
For accepted archive consultation, also include machine-readable whitelist outcome fields in archiveActionResolutions: guaranteedArchiveQuestCount, questHookCount, specialQuestLineUnlocks, visibleRivalClueBonus, archiveWarningTierBonus.
For accepted archive project fuel, also include machine-readable resultMode = project_work | pressure_relief and resultAmount > 0 in archiveActionResolutions.
Accepted archive actions consume the reserved entry; rejected/cancelled actions release it back into the archive.
If game_state/world/guardian_corrections.json exists for the current mortal life, treat it as a client-authored explanation of which compatible Guardian corrections were applied and why.
Guardian corrections are additions around the player's confirmed scenario core. They are not permission to negate, rewrite, or silently downgrade explicit player-authored start facts.
Do NOT edit next_life_scenario_core.json, archive_candidate_manifest.json, or guardian_corrections.json in GM output; read them as input contracts only.

ABODE OFFERINGS — AFTERLIFE ONLY:
If game_state/control/pending_abode_offering.json exists, treat it as a client-authored request for a whitelisted offering to a specific Guardian's Abode.
Do NOT edit pending_abode_offering.json in GM output; read it as input contract only.
Resolve the offering through guardianPowerEvents with reasonType=offering and a full offering audit.
Whitelisted offering types may include:
  - ink_feathers
  - soul_relic
  - archive_lore_fragment
  - archive_secret_record
Use afterlifeArchiveUpdates only for exceptional/system archive rewards.
Ordinary codex-derived archive intake is client-driven through archive_candidate_manifest.json and soul_state.afterlifeArchive.stored; do not improvise it from mortal inventory/items.json.
Do NOT mutate guardian.abodePower.currentPower directly for an offering without the matching power event.

LOCAL GUARDIAN TRADE REQUESTS:
If game_state/control/pending_guardian_trade_request.json exists, treat it as a client-authored request to materialize guardian.tradeInventory for the current return cycle.
Do NOT derive trade stock from guardian.domain in the client or assume the client will do it for you.
Answer the request by writing an explicit guardian.tradeInventory into guardians.json / activeGuardian mirror with matching tradeCycleId and a valid items array.
Close the request canonically through UpdateGuardianTradeInventoryReceipts in guardians.json with matching requestId, tradeCycleId, itemCount, resolvedAtTurn, and resolvedAtUtc.

PLAYER-FOUNDED GUARDIAN FOUNDATION:
If game_state/control/pending_player_guardian_foundation.json exists, treat it as a client-authored late-game Chaos Sea ritual to found a new guardian mantle after Shining return.
The player remains player_soul. Do NOT rewrite soul_state or narration as if the soul directly became an ordinary guardian actor.
Resolve the ritual by:
  - creating a NEW guardian through UpdateGuardians.create with full canonical guardian shape,
  - setting originType=player_founded_ascended_soul,
  - setting founderLoyaltyTier=soulbound,
  - setting foundationSource=shining_return and foundationRequestId,
  - keeping the previous guardian in guardians[],
  - making the new guardian the current activeGuardian,
  - binding chaosSeaNavigation.currentAbodeId to the new guardian abode,
  - writing soul_state.playerFoundedGuardianId,
  - appending guardians.json.playerGuardianFoundationHistory[] receipt.
In v1 this route is single-use per save. Do NOT create a second player-founded guardian if one already exists.

LOCAL NPC TRADE REQUESTS:
If game_state/control/pending_npc_trade_inventory_requests.json exists, treat it as a client-authored request to materialize explicit npc.tradeInventory for the current world-time trade cycle.
Do NOT generate or infer NPC stock on the client.
Answer each request by writing explicit npc.tradeInventory into npc_core.json with matching tradeCycleId, refreshAfterWorldDate, and a valid items array.
Close each request canonically through UpdateNpcTradeInventoryReceipts in npc_core.json with matching requestId, npcId, tradeCycleId, merchantProfile, itemCount, resolvedAtTurn, and resolvedAtUtc.

LOCAL SHINING TRADE REQUESTS:
If game_state/control/pending_shining_trade_inventory_requests.json exists, treat it as a client-authored request to materialize explicit shining faction tradeInventory for the current return cycle.
These requests may be created automatically by the client when the Soul returns to the active Shining Abode after a new mortal life.
Do NOT infer or generate Shining stock on the client.
Answer each request by writing explicit faction.tradeInventory into shining_abode_state.json with matching tradeCycleId, generationTradeTier, generationRarityCeiling, serviceMultiplierSnapshot and a valid items array.
Close each request canonically through faction.tradeInventoryReceipts[] with matching requestId, factionId, tradeCycleId, itemCount, soldOutCount, resolvedAtTurn and resolvedAtUtc.

RIVAL SOUL ARCS — MORTAL WORLD ONLY:
Use UpdateRivalSoulArcs to track parallel destiny lines for OTHER souls in the current mortal life.
These arcs are milestone-based world pressures, not a full second-protagonist simulation.
Keep at most:
  - 1 active major arc
  - 1 active minor arc
Each rival arc must include:
  - arcId
  - scope = major | minor
  - arcType
  - status = latent | rising | intersecting | resolved | failed
  - sponsorGuardianRef
  - rivalSoul
  - objective
  - playerIntersection
  - milestones
  - currentStage
  - publicSignals
  - resolution
If a hostile rival arc directly targets the player, you MUST surface at least two visible clues before direct collision or terminal harm.
If the arc becomes personal for the player, create or update a normal player-facing soul quest through UpdateSoulQuests and link it with relatedRivalArcId.
If you surface a rival arc clue through worldEventsLog, mark that world event with relatedRivalArcId too, so the player can recognize it as part of a parallel destiny line instead of random background noise.
Every publicSignals[] item must include visibleToPlayer=true|false explicitly; do not omit the field.
For rival-thread clue logic, linked worldEventsLog entries count as player-visible only when visibility is Public, Regional, or player_known.
If a Secret or Faction-Internal world event becomes known to the player through actual play, convert the linked player-facing world event entry to visibility=player_known so it can count as a visible clue without pretending it was always public.
Hidden Secret/Faction-Internal linked world events do NOT spend visible lore_research clue budget until they actually become Public, Regional, or player_known.
When practical, add turn/timestamp/date information to publicSignals or linked world events, and describe consequences/impact/follow-up in those world events, so the player's rival-thread journal can show a clearer chronology and visible world changes.
If a new player-visible rival clue is revealed specifically through completed lore_research support, include bonusClueSourceProjectId, bonusClueRevealId, and optional bonusClueCost on that reveal surface so the client can spend life-bound clue budget deterministically.
If the SAME extra clue is mirrored through both publicSignals and linked worldEventsLog, reuse the same bonusClueRevealId on both surfaces so the client spends clue budget only once.
Do NOT use rival arcs in Chaos Sea or Shining Abode.

GUARDIAN-FORCED INCARNATION — HARD REQUIREMENT:
If game_state/control/afterlife_return_guard.json is semantic-valid (`reason = post_life_return`) and has remainingProtectedTurns > 0, the soul has just returned from a mortal life and MUST receive at least one ordinary afterlife turn before any Guardian-forced incarnation.
If afterlife_return_guard.json is malformed, unreadable, or parsed with the wrong reason, Guardian-forced incarnation is ALSO forbidden fail-closed until client normalization clears that invalid guard state.
Do NOT immediately kick the soul back into a new life on that protected return turn.
Do NOT immediately kick the soul back into a new life while afterlife_return_guard.json remains invalid or unreadable either; fail closed until client normalization clears that guard state.
Guardian-forced incarnation is legal only on an ordinary player-driven Chaos Sea turn as a response to explicit player provocation against the current active Guardian.
If you write game_state/control/incarnation_trigger.json in this forced mode, include:
  - source = guardian_forced
  - guardianId
  - severityBand = harsh | severe
  - reason
  - provocationSummary
  - worldDescription, characterDescription, circumstances
The resulting start must be harsh but survivable. Do NOT create an unwinnable deathtrap.

SOUL IDENTITY CONTINUITY:
If game_state/meta/soul_state.json contains previousSoulNames, they are former names of the SAME soul.
Do NOT treat a renamed soul as a different person and do NOT reset Guardian continuity because of a soul rename.

SOUL RELIC GACHA — ANTI-CHEAT PROTOCOL:
The 'preGeneratedDices1d20' field is the authoritative dice pool for your normal checks. Start from the FIRST die in that list.
The 'gachaBaseResult' field is a SEPARATE client-computed gacha outcome. Do NOT assume any dice were consumed from preGeneratedDices1d20 to produce it.
Its thresholds remain: 4-48=Common, 49-67=Uncommon, 68-75=Rare, 76-79=Epic, 80=Legendary.
If playerAction contains [CHAOS_SEA_DIRECT_GACHA], this is a DIRECT pull from the Chaos Sea, not a Guardian-mediated pull.
  - Do NOT apply Guardian reputation bonuses, penalties, discounts, jealousy/social effects, or other Guardian modifiers.
  - Treat gachaBaseResult.baseRarity as the neutral final rarity baseline with NO extra modifiers.
  - Add the relic directly to soul state via metaStateUpdates.soulRelicOperations.addRelic.
If the pull is Guardian-mediated, the 'baseRarity' from gachaBaseResult is the MINIMUM rarity. You may ONLY upgrade it using documented modifiers:
  - Guardian reputation bonus (Block 32): Friendly(50-129) +15%, Devoted(130-229) +30%, Legendary(230-300) +50% better rates
  - Hard Mode (Block 0.5): +1 tier upgrade at 50% chance
  - Impossible Mode (Block 0.6): +1 tier guaranteed, +1 more at 25% chance
Guardian-mediated pulls are LIMITED per Guardian per return from mortal life:
  - Hostile(-100..-51): blocked
  - Wary/Neutral(-50..49): 1 attempt
  - Friendly(50..129): 2 attempts
  - Devoted/Legendary(130..300): 3 attempts
  - If chargesUsedThisReturn already equals chargesPerReturn for that Guardian, DO NOT emit processGacha for them.
If a Guardian-mediated pull finishes above baseRarity, include gachaBonusAudit with:
  - baseRarity
  - abodePowerBonusSteps
  - relicForgingBonusSteps
  - finalRarity
  - sourceProjectId if relic forging bonus was actually spent
Completed recipe-driven guardian projects may be TEMPORARY:
  - relic_forging lasts only until the next local trade refresh and one guardian-mediated gacha use
  - lore_research life-bound hook/clue bonuses last only for the target incarnation
  - soul_preparation applies only to the next life and is consumed at correction resolution
Direct /gacha remains neutral and does NOT consume Guardian charges.
You MUST NOT downgrade or ignore the client-computed baseRarity. Log the full calculation in gm_thoughts_markdown.

SHINING RELIC GACHA:
If game_state/control/pending_shining_abode_actions.json exists with actionType=pull_relic_gacha, treat it as a faction-banner relic pull inside the active Shining Abode.
Use turn_request.gachaBaseResult.baseRarity as the MINIMUM rarity floor for the pull.
Shining banner modifiers may only increase or preserve that base rarity; they must not downgrade it.
The client-authored request includes projectedGachaBonusSteps and returnCycleId. Do NOT exceed that projected bonus ceiling.
Resolve the pull by:
  - adding exactly one Soul Relic result to soul state,
  - updating shining_abode_state.json.gachaSystem.chargesUsedThisReturn and gachaHistory[],
  - writing a matching coreActionReceipts[] entry with requestId, actionType=pull_relic_gacha, factionId, returnCycleId, relicId, relicName, baseRarity, finalRarity, resolvedAtTurn and resolvedAtUtc.
Shining relic gacha consumes the quoted Ink Feather cost from the request and does NOT use Light Sparks.

" + _storyService.BuildStoryContext();
    }

    private async Task<string> BuildTurnSystemReminderAsync(string? extraReminder = null)
    {
        if (await _systemModService.WriteManifestForGmAsync())
            await _stateManager.SaveSettingsAsync();

        if (_fs.FileExists(WorldDirectiveService.PendingSetupPath))
            await _scenarioCoreService.RefreshFromPendingSetupAsync();

        var parts = new List<string> { BuildSystemReminder() };
        var modReminder = await _systemModService.BuildSystemReminderFragmentAsync();
        if (!string.IsNullOrWhiteSpace(modReminder))
            parts.Add(modReminder);
        var worldReminder = _worldDirectiveService.BuildReminderFragment(
            _stateManager.CurrentState.CurrentRealm,
            await _worldDirectiveService.ReadPendingSetupAsync(),
            await _worldDirectiveService.ReadActiveWorldDirectivesAsync());
        if (!string.IsNullOrWhiteSpace(worldReminder))
            parts.Add(worldReminder);
        var scenarioCoreReminder = await _scenarioCoreService.BuildSystemReminderFragmentAsync(_stateManager.CurrentState.CurrentRealm);
        if (!string.IsNullOrWhiteSpace(scenarioCoreReminder))
            parts.Add(scenarioCoreReminder);
        var afterlifeGuardReminder = await _afterlifeReturnGuardService.BuildSystemReminderFragmentAsync(_stateManager.CurrentState.CurrentRealm);
        if (!string.IsNullOrWhiteSpace(afterlifeGuardReminder))
            parts.Add(afterlifeGuardReminder);
        var playerGuardianFoundationReminder = await PlayerGuardianFoundationState.BuildSystemReminderFragmentAsync(_fs, _stateManager.CurrentState.CurrentRealm);
        if (!string.IsNullOrWhiteSpace(playerGuardianFoundationReminder))
            parts.Add(playerGuardianFoundationReminder);
        var rivalArcReminder = await _rivalSoulArcService.BuildSystemReminderFragmentAsync(_stateManager.CurrentState.CurrentRealm, _gameLoop.TurnNumber);
        if (!string.IsNullOrWhiteSpace(rivalArcReminder))
            parts.Add(rivalArcReminder);
        var guardianCorrectionReminder = await _guardianCorrectionService.BuildSystemReminderFragmentAsync(_stateManager.CurrentState.CurrentRealm);
        if (!string.IsNullOrWhiteSpace(guardianCorrectionReminder))
            parts.Add(guardianCorrectionReminder);
        var actorMemoryReminder = await _actorMemoryService.BuildSystemReminderFragmentAsync(_stateManager.CurrentState.CurrentRealm, _gameLoop.TurnNumber);
        if (!string.IsNullOrWhiteSpace(actorMemoryReminder))
            parts.Add(actorMemoryReminder);
        var actorSocialReminder = await ActorSocialInteractionRequestState.BuildSystemReminderFragmentAsync(_fs, _stateManager.CurrentState.CurrentRealm);
        if (!string.IsNullOrWhiteSpace(actorSocialReminder))
            parts.Add(actorSocialReminder);
        var shiningBlessingReminder = await ShiningBlessingEffectState.BuildSystemReminderFragmentAsync(_fs, _stateManager.CurrentState.CurrentRealm, _gameLoop.TurnNumber);
        if (!string.IsNullOrWhiteSpace(shiningBlessingReminder))
            parts.Add(shiningBlessingReminder);
        var npcTradeReminder = await NpcTradeRequestState.BuildSystemReminderFragmentAsync(_fs, _stateManager.CurrentState.CurrentRealm);
        if (!string.IsNullOrWhiteSpace(npcTradeReminder))
            parts.Add(npcTradeReminder);
        var abodeResidentReminder = await GuardianAbodeResidentRequestState.BuildSystemReminderFragmentAsync(_fs, _stateManager.CurrentState.CurrentRealm);
        if (!string.IsNullOrWhiteSpace(abodeResidentReminder))
            parts.Add(abodeResidentReminder);
        var shiningCoreReminder = await ShiningCoreActionRequestState.BuildSystemReminderFragmentAsync(_fs, _stateManager.CurrentState.CurrentRealm);
        if (!string.IsNullOrWhiteSpace(shiningCoreReminder))
            parts.Add(shiningCoreReminder);
        var shiningTradeReminder = await ShiningTradeRequestState.BuildSystemReminderFragmentAsync(_fs, _stateManager.CurrentState.CurrentRealm);
        if (!string.IsNullOrWhiteSpace(shiningTradeReminder))
            parts.Add(shiningTradeReminder);
        var shiningPoliticsReminder = await ShiningFactionRequestState.BuildSystemReminderFragmentAsync(_fs, _stateManager.CurrentState.CurrentRealm);
        if (!string.IsNullOrWhiteSpace(shiningPoliticsReminder))
            parts.Add(shiningPoliticsReminder);
        var systemGuardianReminder = await _systemGuardianLibraryService.BuildReminderFragmentAsync(_stateManager.CurrentState.CurrentRealm);
        if (!string.IsNullOrWhiteSpace(systemGuardianReminder))
            parts.Add(systemGuardianReminder);
        var qteReminder = await _qteSceneService.ConsumePendingReminderAsync();
        if (!string.IsNullOrWhiteSpace(qteReminder))
            parts.Add($"QTE SUMMARY FROM PREVIOUS LOCAL SCENE: {qteReminder}");
        if (!string.IsNullOrWhiteSpace(extraReminder))
            parts.Add(extraReminder);

        return string.Join("\n\n", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
    }

    private static bool IsIncarnationSourceLabel(string? sourceLabel)
    {
        if (string.IsNullOrWhiteSpace(sourceLabel))
            return false;

        return sourceLabel.Contains("воплощ", StringComparison.OrdinalIgnoreCase);
    }


    private async Task ShowStatDistribution(string title)
    {
        var available = await _charService.GetUnspentStatPoints();
        if (available <= 0) return;

        var baseStats = new Dictionary<string, int>();
        var json = await _fs.ReadFileAsync("game_state/misc/characteristics.json");
        if (json != null)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                foreach (var name in Characteristics.All)
                {
                    if (doc.RootElement.TryGetProperty(name, out var val) &&
                        val.ValueKind == JsonValueKind.Number)
                        baseStats[name] = val.GetInt32();
                    else
                        baseStats[name] = 1;
                }
            }
            catch { foreach (var n in Characteristics.All) baseStats[n] = 1; }
        }
        else
        {
            foreach (var n in Characteristics.All) baseStats[n] = 1;
        }

        var allocations = new Dictionary<string, int>();
        foreach (var n in Characteristics.All) allocations[n] = 0;
        var remaining = available;
        var statList = Characteristics.All;
        var selectedIdx = 0;

        while (remaining > 0)
        {
            AnsiConsole.Clear();
            AnsiConsole.Write(new Rule($"[gold1]⭐ {title}[/]").RuleStyle("gold1"));
            AnsiConsole.MarkupLine($"\n  [bold yellow]Доступно очков: {remaining}[/]  [dim](↑↓ выбрать, → добавить, ← убрать, Enter подтвердить)[/]\n");

            var table = new Table()
                .Border(TableBorder.Rounded)
                .BorderColor(Color.Gold1)
                .Expand()
                .AddColumn(new TableColumn("").NoWrap().Width(3))
                .AddColumn(new TableColumn("[bold]Характеристика[/]").NoWrap())
                .AddColumn(new TableColumn("[bold]Текущая[/]").Centered().NoWrap())
                .AddColumn(new TableColumn("[bold]+ Очки[/]").Centered().NoWrap())
                .AddColumn(new TableColumn("[bold]= Итог[/]").Centered().NoWrap())
                .AddColumn(new TableColumn("[bold]Шкала[/]").NoWrap());

            for (int i = 0; i < statList.Length; i++)
            {
                var name = statList[i];
                var ruName = Characteristics.RussianNames[name];
                var baseVal = baseStats[name];
                var alloc = allocations[name];
                var total = baseVal + alloc;
                var cursor = i == selectedIdx ? "[bold cyan]►[/]" : " ";

                int filled = Math.Clamp(total / 5, 0, 20);
                int empty = 20 - filled;
                var barColor = total switch { >= 80 => "gold1", >= 50 => "green", >= 25 => "yellow", _ => "grey" };
                var bar = $"[{barColor}]{new string('█', filled)}[/][dim]{new string('░', empty)}[/]";

                var allocStr = alloc > 0 ? $"[green]+{alloc}[/]" : "[dim]—[/]";
                var totalColor = alloc > 0 ? "green" : "white";
                var nameColor = i == selectedIdx ? "cyan bold" : "white";

                table.AddRow(cursor, $"[{nameColor}]{ruName}[/]",
                    $"{baseVal}", allocStr, $"[{totalColor}]{total}[/]", bar);
            }

            AnsiConsole.Write(table);
            AnsiConsole.MarkupLine(remaining > 0
                ? $"\n  [dim]Осталось распределить: [yellow]{remaining}[/] очков[/]"
                : "\n  [green]✅ Все очки распределены![/]");

            var key = Console.ReadKey(true);
            switch (key.Key)
            {
                case ConsoleKey.UpArrow:
                    selectedIdx = (selectedIdx - 1 + statList.Length) % statList.Length;
                    break;
                case ConsoleKey.DownArrow:
                    selectedIdx = (selectedIdx + 1) % statList.Length;
                    break;
                case ConsoleKey.RightArrow:
                case ConsoleKey.OemPlus:
                case ConsoleKey.Add:
                    if (remaining > 0 && baseStats[statList[selectedIdx]] + allocations[statList[selectedIdx]] < 100)
                    {
                        allocations[statList[selectedIdx]]++;
                        remaining--;
                    }
                    break;
                case ConsoleKey.LeftArrow:
                case ConsoleKey.OemMinus:
                case ConsoleKey.Subtract:
                    if (allocations[statList[selectedIdx]] > 0)
                    {
                        allocations[statList[selectedIdx]]--;
                        remaining++;
                    }
                    break;
                case ConsoleKey.Enter:
                    if (remaining == 0)
                        goto done;
                    // If some points remain, ask for confirmation
                    if (AnsiConsole.Confirm($"[yellow]У вас ещё {remaining} нераспределённых очков. Подтвердить?[/]", false))
                        goto done;
                    break;
            }
        }

        done:
        // Apply allocations
        var toApply = allocations.Where(kv => kv.Value > 0).ToDictionary(kv => kv.Key, kv => kv.Value);
        if (toApply.Count > 0)
        {
            await _charService.DistributePointsAsync(toApply);
            AnsiConsole.MarkupLine("[green]✅ Очки характеристик распределены![/]");
        }
        else
        {
            // Save remaining points for later
            await _charService.AddStatPoints(0); // no-op if nothing to add, just ensures file exists
        }

        AnsiConsole.MarkupLine($"[grey]{_loc.T("press_any_key")}[/]");
        Console.ReadKey(true);
    }
}

