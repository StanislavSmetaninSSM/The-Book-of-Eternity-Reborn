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
    private static readonly string[] GuardianPolicySnapshotRequestFiles =
    {
        GuardianAbodeOfferingState.PendingRequestPath,
        GuardianTradeRequestState.PendingRequestPath,
        GuardianAbodeResidentRequestState.PendingResidentsRequestPath,
        GuardianAbodeResidentRequestState.PendingInteractionsRequestPath,
        ActorSocialInteractionRequestState.PendingGuardianRequestPath,
        AfterlifeArchiveActionState.ConsultationRequestPath,
        AfterlifeArchiveActionState.ProjectFuelRequestPath
    };

    private async Task<GameResponse> BuildGameResponseFromFiles()
    {
        var response = new GameResponse();

        // 1. Read narrative from output/narrative_response.json (primary source per API spec)
        var narrativeJson = await _fs.ReadFileAsync("output/narrative_response.json");
        if (narrativeJson != null)
        {
            try
            {
                using var doc = JsonDocument.Parse(narrativeJson);
                if (doc.RootElement.TryGetProperty("response", out var r))
                    response.Response = r.GetString();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Не удалось прочитать output/narrative_response.json при сборке ответа.");
            }
        }

        // Fallback: use narrative from state if not in output file
        if (string.IsNullOrEmpty(response.Response))
            response.Response = _stateManager.CurrentState.Narrative;

        // 2. Read dialogue options and image prompt from output/interface_updates.json
        var uiJson = await _fs.ReadFileAsync("output/interface_updates.json");
        if (uiJson != null)
        {
            try
            {
                using var doc = JsonDocument.Parse(uiJson);
                if (doc.RootElement.TryGetProperty("dialogueOptions", out var opts) &&
                    opts.ValueKind == JsonValueKind.Array)
                {
                    response.DialogueOptions = JsonSerializer.Deserialize<DialogueOption[]>(opts.GetRawText());
                }
                if (doc.RootElement.TryGetProperty("image_prompt", out var img))
                    response.ImagePrompt = img.GetString();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Не удалось прочитать output/interface_updates.json при сборке ответа.");
            }
        }

        // 3. Read GM thoughts from output/debug_logs.json
        var debugJson = await _fs.ReadFileAsync("output/debug_logs.json");
        if (debugJson != null)
        {
            try
            {
                using var doc = JsonDocument.Parse(debugJson);
                if (doc.RootElement.TryGetProperty("gm_thoughts_markdown", out var gm))
                    response.GmThoughtsMarkdown = gm.GetString();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Не удалось прочитать output/debug_logs.json при сборке ответа.");
            }
        }

        // 4. Read combat log from distributed combat state if exists
        var combatJson = await _fs.ReadFileAsync("game_state/combat/combat_log.json");
        if (combatJson != null)
        {
            try
            {
                using var doc = JsonDocument.Parse(combatJson);
                if (doc.RootElement.TryGetProperty("combat_log_markdown", out var cl))
                    response.CombatLogMarkdown = cl.GetString();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Не удалось прочитать game_state/combat/combat_log.json при сборке ответа.");
            }
        }

        // 5. Populate status from state
        var st = _stateManager.CurrentState.PlayerStatus;
        response.PlayerStatus = new PlayerStatus
        {
            HealthPercentage = st.HealthPercentage,
            EnergyPercentage = st.EnergyPercentage,
            PoisePercentage = st.PoisePercentage,
            CurrentCondition = st.CurrentCondition
        };

        return response;
    }

    private GameResponse MergeWithLastResponse(GameResponse? refreshed)
    {
        return GameResponseRefreshMerger.Merge(_lastResponse, refreshed);
    }

    private async Task RefreshRuntimeStateAsync()
    {
        await _stateManager.RefreshGameStateAsync();
        await _progressionSchedule.EnsureInitializedAsync();
    }

    private async Task RefreshCanonicalStateAsync(IReadOnlyDictionary<string, string> backups)
    {
        await _normalizer.NormalizeAccumulatedStateAsync(backups);
        await RefreshRuntimeStateAsync();
    }


    private async Task<Dictionary<string, string>> CreateCanonicalBaselineSnapshotAsync(TurnRequest request,
        RollbackSnapshot? rollbackSnapshot = null,
        string? sourceLabel = null)
    {
        await DeleteTerminalProtocolFailureRequestAsync();
        await CleanupPendingTurnSnapshotAsync();

        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var snapshotHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var clientOwnedValidationHashes = await CaptureClientOwnedValidationHashesAsync();
        foreach (var file in CanonicalStateNormalizer.CanonicalAccumulatedFiles)
        {
            await SnapshotFileIfPresentAsync(file, files, snapshotHashes);
        }

        foreach (var file in GuardianPolicySnapshotRequestFiles)
        {
            await SnapshotFileIfPresentAsync(file, files, snapshotHashes);
        }

        var manifest = new PendingTurnSnapshotManifest
        {
            SessionId = request.SessionId,
            RequestId = request.RequestId,
            TurnNumber = request.TurnNumber,
            RequestTimestamp = request.Timestamp,
            PlayerAction = request.PlayerAction,
            ProgressionControl = request.ProgressionControl,
            Files = files,
            SnapshotFileHashes = snapshotHashes,
            ClientOwnedValidationHashes = clientOwnedValidationHashes,
            RollbackBackups = rollbackSnapshot != null
                ? new Dictionary<string, string>(rollbackSnapshot.BackupFiles, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            RollbackBaselineFiles = rollbackSnapshot?.BaselineFiles.ToList() ?? new List<string>(),
            SourceLabel = sourceLabel
        };
        manifest.ManifestPayloadHash = ComputePendingTurnManifestPayloadHash(manifest);

        await _fs.WriteFileAtomicAsync(PendingTurnSnapshotManifestPath,
            JsonSerializer.Serialize(manifest, JsonOpts));

        return files;
    }

    private async Task<Dictionary<string, string>> CaptureClientOwnedValidationHashesAsync()
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["game_state/history/chat_log.json"] = await ReadFileHashOrEmptyAsync("game_state/history/chat_log.json")
        };

        foreach (var storyPath in EnumerateStoryContinuityFiles())
            result[storyPath] = await ReadFileHashOrEmptyAsync(storyPath);

        return result;
    }

    private async Task<string> ReadFileHashOrEmptyAsync(string relativePath)
    {
        var content = await _fs.ReadFileAsync(relativePath);
        return content == null ? string.Empty : ComputeSha256(content);
    }

    private IEnumerable<string> EnumerateStoryContinuityFiles()
    {
        var sessionRoot = _fs.ResolvePath("");
        var storiesRoot = _fs.ResolvePath("stories");
        if (!Directory.Exists(storiesRoot))
            yield break;

        foreach (var absoluteFile in Directory.EnumerateFiles(storiesRoot, "*.jsonl", SearchOption.AllDirectories))
            yield return Path.GetRelativePath(sessionRoot, absoluteFile).Replace('\\', '/');
    }

    private async Task<Dictionary<string, string>?> LoadCanonicalBaselineSnapshotAsync(int expectedTurnNumber)
    {
        var manifest = await LoadPendingTurnSnapshotManifestAsync();
        if (manifest == null)
            return null;

        if (!string.Equals(manifest.SessionId, _gameLoop.SessionId, StringComparison.OrdinalIgnoreCase))
            return null;

        if (manifest.TurnNumber != expectedTurnNumber)
            return null;

        var canonicalFiles = new HashSet<string>(CanonicalStateNormalizer.CanonicalAccumulatedFiles, StringComparer.OrdinalIgnoreCase);
        return manifest.Files
            .Where(kv => canonicalFiles.Contains(kv.Key) && _fs.FileExists(kv.Value))
            .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);
    }

    private async Task<PendingTurnSnapshotManifest?> LoadPendingTurnSnapshotManifestAsync()
    {
        var json = await _fs.ReadFileAsync(PendingTurnSnapshotManifestPath);
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<PendingTurnSnapshotManifest>(json, JsonOpts);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Не удалось загрузить pending turn snapshot manifest");
            return null;
        }
    }

    private string ComputePendingTurnManifestPayloadHash(PendingTurnSnapshotManifest manifest)
    {
        var originalHash = manifest.ManifestPayloadHash;
        manifest.ManifestPayloadHash = "";
        var payload = JsonSerializer.Serialize(manifest, SnapshotHashJsonOpts);
        manifest.ManifestPayloadHash = originalHash;
        return ComputeSha256(payload);
    }

    private static string ComputeSha256(string content)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes);
    }

    private async Task SnapshotFileIfPresentAsync(
        string relativePath,
        IDictionary<string, string> files,
        IDictionary<string, string> snapshotHashes)
    {
        var content = await _fs.ReadFileAsync(relativePath);
        if (content == null)
            return;

        var snapshotPath = $"{PendingTurnSnapshotDirectory}/{relativePath}";
        await _fs.WriteFileAtomicAsync(snapshotPath, content);
        files[relativePath] = snapshotPath;
        snapshotHashes[relativePath] = ComputeSha256(content);
    }

    private async Task CleanupPendingTurnSnapshotAsync()
    {
        var json = await _fs.ReadFileAsync(PendingTurnSnapshotManifestPath);
        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                var manifest = JsonSerializer.Deserialize<PendingTurnSnapshotManifest>(json, JsonOpts);
                if (manifest?.Files != null)
                {
                    foreach (var snapshotPath in manifest.Files.Values)
                    {
                        if (_fs.FileExists(snapshotPath))
                            _fs.DeleteFile(snapshotPath);
                    }
                }

                if (manifest?.RollbackBackups != null)
                {
                    foreach (var rollbackPath in manifest.RollbackBackups.Values)
                    {
                        if (_fs.FileExists(rollbackPath))
                            _fs.DeleteFile(rollbackPath);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Не удалось очистить pending turn snapshot artifacts.");
            }
        }

        if (_fs.FileExists(PendingTurnSnapshotManifestPath))
            _fs.DeleteFile(PendingTurnSnapshotManifestPath);
    }

    private static bool HasRollbackCapability(RollbackSnapshot? snapshot) =>
        snapshot != null && (snapshot.BackupFiles.Count > 0 || snapshot.BaselineFiles.Count > 0);

    private RollbackSnapshot? GetRollbackSnapshot(PendingTurnSnapshotManifest? manifest)
    {
        if (manifest == null)
            return null;

        var snapshot = new RollbackSnapshot
        {
            BackupFiles = manifest.RollbackBackups
                .Where(kv => !string.IsNullOrWhiteSpace(kv.Key) &&
                             !string.IsNullOrWhiteSpace(kv.Value) &&
                             _fs.FileExists(kv.Value))
                .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase),
            BaselineFiles = new HashSet<string>(manifest.RollbackBaselineFiles ?? Enumerable.Empty<string>(),
                StringComparer.OrdinalIgnoreCase)
        };

        return HasRollbackCapability(snapshot) ? snapshot : null;
    }

    private async Task NormalizePendingRepairArtifactsAsync()
    {
        var repairRequestExists = _fs.FileExists(ValidationRepairRequestPath);
        var repairReadyExists = _fs.FileExists(ValidationRepairReadyPath);
        if (!repairRequestExists && !repairReadyExists)
            return;

        var manifest = await LoadPendingTurnSnapshotManifestAsync();

        if (manifest == null)
        {
            _logger.LogWarning("Найдены repair-файлы без pending snapshot manifest. Очистка как stale state.");
            await DeleteValidationRepairFilesAsync();
            return;
        }

        if (repairReadyExists && !repairRequestExists)
        {
            _logger.LogWarning(
                "Найден orphaned validation_repair_ready для pending turn(session={Session}, request={Request}, turn={Turn}). Удаление ready-файла без затрагивания основного pending turn state.",
                manifest.SessionId,
                manifest.RequestId,
                manifest.TurnNumber);
            await DeleteValidationRepairReadyAsync();
            return;
        }

        if (repairRequestExists)
        {
            if (!_fs.FileExists("ready/turn_complete.json"))
            {
                _logger.LogWarning(
                    "Найден validation_repair_request без correlated ready/turn_complete.json для pending turn(session={Session}, request={Request}, turn={Turn}). Очистка stale repair artifacts.",
                    manifest.SessionId,
                    manifest.RequestId,
                    manifest.TurnNumber);
                await DeleteValidationRepairFilesAsync();
                return;
            }

            var turnCompleteMetadata = await ReadReadySignalMetadataAsync("ready/turn_complete.json");
            if (turnCompleteMetadata == null ||
                !string.Equals(turnCompleteMetadata.SessionId, manifest.SessionId, StringComparison.Ordinal) ||
                !string.Equals(turnCompleteMetadata.RequestId, manifest.RequestId, StringComparison.Ordinal) ||
                turnCompleteMetadata.TurnNumber != manifest.TurnNumber)
            {
                _logger.LogWarning(
                    "Найден validation_repair_request с некоррелированным ready/turn_complete.json. Очистка stale repair artifacts для pending turn(session={Session}, request={Request}, turn={Turn}).",
                    manifest.SessionId,
                    manifest.RequestId,
                    manifest.TurnNumber);
                _fs.DeleteFile("ready/turn_complete.json");
                await DeleteValidationRepairFilesAsync();
                return;
            }

            _logger.LogInformation(
                "Обнаружен активный repair cycle для pending turn(session={Session}, request={Request}, turn={Turn}). Он будет продолжен через correlated late-response validation.",
                manifest.SessionId,
                manifest.RequestId,
                manifest.TurnNumber);
        }
    }

    private async Task NormalizePendingTerminalProtocolFailureArtifactsAsync()
    {
        if (!_fs.FileExists(TerminalProtocolFailureRequestPath))
            return;

        var json = await _fs.ReadFileAsync(TerminalProtocolFailureRequestPath);
        if (string.IsNullOrWhiteSpace(json))
        {
            _logger.LogWarning("Найден пустой terminal_protocol_failure_request.json. Удаление как невалидного stale artifact.");
            await DeleteTerminalProtocolFailureRequestAsync();
            return;
        }

        try
        {
            JsonSerializer.Deserialize<TerminalProtocolFailureRequest>(json, JsonOpts);
            _logger.LogInformation("Обнаружен сохранённый terminal protocol failure request. Он будет сохранён через рестарт и доступен daemon для повторного пинга GM.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Найден невалидный terminal_protocol_failure_request.json. Удаление как stale artifact.");
            await DeleteTerminalProtocolFailureRequestAsync();
        }
    }

    private async Task NormalizeRuntimeUiArtifactsAsync()
    {
        await NormalizePendingRepairArtifactsAsync();
        await NormalizePendingTerminalProtocolFailureArtifactsAsync();
        await AfterlifeNotificationState.SyncFromCurrentStateAsync(_fs);
        await AfterlifeNotificationState.EnsureHealthyAsync(_fs);
        await _afterlifeReturnGuardService.EnsureHealthyAsync(_stateManager.CurrentState.CurrentRealm);
        await _systemGuardianLibraryService.EnsureAttractionRequestHealthyAsync(_stateManager.CurrentState.CurrentRealm);
        await GuardianAbodeOfferingState.EnsureHealthyAsync(_fs, _stateManager.CurrentState.CurrentRealm);
        await GuardianTradeRequestState.EnsureHealthyAsync(_fs, _stateManager.CurrentState.CurrentRealm);
        await NpcTradeRequestState.EnsureHealthyAsync(_fs, _stateManager.CurrentState.CurrentRealm);
        await AfterlifeArchiveActionState.EnsureHealthyAsync(_fs, _stateManager.CurrentState.CurrentRealm);
        await GuardianAbodeResidentRequestState.EnsureHealthyAsync(_fs, _stateManager.CurrentState.CurrentRealm);
        await ActorSocialInteractionRequestState.EnsureHealthyAsync(_fs, _stateManager.CurrentState.CurrentRealm);
        await GuardianAbodeResidentRequestState.EnsureManifestationRequestForCurrentIncarnationAsync(_fs, _stateManager.CurrentState.CurrentRealm);
        await _qteSceneService.EnsureRuntimeStateHealthyAsync();

        var manifest = await LoadPendingTurnSnapshotManifestAsync();
        var hasReadySignals = _fs.FileExists("ready/turn_complete.json") || _fs.FileExists("ready/turn_error.json");

        if (manifest == null && hasReadySignals)
        {
            _logger.LogWarning("Найдены ready-сигналы без pending snapshot manifest. Очистка как stale runtime artifacts.");
            ClearReadySignals();
        }

        if (manifest != null)
            return;

        if (_fs.FileExists("input/turn_request.json"))
        {
            _logger.LogWarning("Найден orphaned input/turn_request.json без pending snapshot manifest. Удаление как stale runtime artifact.");
            _fs.DeleteFile("input/turn_request.json");
        }
    }

    private async Task<int?> ReadReadySignalTurnNumberAsync()
    {
        var metadata = await ReadReadySignalMetadataAsync("ready/turn_complete.json");
        return metadata?.TurnNumber;
    }

    private async Task<ReadySignalMetadata?> ReadReadySignalMetadataAsync(string relativePath, int maxAttempts = 3, int delayMs = 150)
    {
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            var metadata = await TryReadReadySignalMetadataOnceAsync(relativePath);
            if (metadata != null)
                return metadata;

            if (!_fs.FileExists(relativePath) || attempt == maxAttempts - 1)
                break;

            await Task.Delay(delayMs);
        }

        return null;
    }

    private async Task<ReadySignalMetadata?> TryReadReadySignalMetadataOnceAsync(string relativePath)
    {
        var json = await _fs.ReadFileAsync(relativePath);
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return null;

            if (doc.RootElement.TryGetProperty("turnNumber", out var turnNumber) &&
                turnNumber.ValueKind == JsonValueKind.Number &&
                turnNumber.TryGetInt32(out var parsed))
            {
                var hasFilesModified = doc.RootElement.TryGetProperty("filesModified", out var filesModified);
                var filesModifiedValid = false;
                if (hasFilesModified && filesModified.ValueKind == JsonValueKind.Array)
                {
                    filesModifiedValid = true;
                    foreach (var item in filesModified.EnumerateArray())
                    {
                        if (item.ValueKind != JsonValueKind.String || !IsValidRelativeFilesModifiedEntry(item.GetString()))
                        {
                            filesModifiedValid = false;
                            break;
                        }
                    }
                }

                return new ReadySignalMetadata
                {
                    SessionId = doc.RootElement.TryGetProperty("sessionId", out var sid) && sid.ValueKind == JsonValueKind.String
                        ? sid.GetString() ?? ""
                        : "",
                    RequestId = doc.RootElement.TryGetProperty("requestId", out var rid) && rid.ValueKind == JsonValueKind.String
                        ? rid.GetString() ?? ""
                        : "",
                    TurnNumber = parsed,
                    Status = doc.RootElement.TryGetProperty("status", out var status) && status.ValueKind == JsonValueKind.String
                        ? status.GetString() ?? ""
                        : "",
                    Timestamp = doc.RootElement.TryGetProperty("timestamp", out var timestamp) && timestamp.ValueKind == JsonValueKind.String
                        ? timestamp.GetString() ?? ""
                        : "",
                    Error = doc.RootElement.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.String
                        ? error.GetString()
                        : null,
                    HasFilesModified = hasFilesModified,
                    FilesModifiedValid = filesModifiedValid
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Не удалось прочитать ready signal {RelativePath}.", relativePath);
        }

        return null;
    }

    private static bool IsValidRelativeFilesModifiedEntry(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        var trimmed = path.Trim();
        if (Path.IsPathRooted(trimmed) || trimmed.StartsWith('/') || trimmed.StartsWith('\\'))
            return false;

        var normalized = trimmed.Replace('\\', '/');
        var segments = normalized.Split('/', StringSplitOptions.None);
        foreach (var segment in segments)
        {
            if (string.IsNullOrWhiteSpace(segment) || segment == "." || segment == "..")
                return false;
        }

        return true;
    }

    private bool IsMatchingReadySignal(ReadySignalMetadata signal, PendingTurnSnapshotManifest manifest) =>
        signal.TurnNumber == manifest.TurnNumber &&
        !string.IsNullOrWhiteSpace(signal.RequestId) &&
        string.Equals(signal.RequestId, manifest.RequestId, StringComparison.OrdinalIgnoreCase) &&
        !string.IsNullOrWhiteSpace(signal.SessionId) &&
        string.Equals(signal.SessionId, manifest.SessionId, StringComparison.OrdinalIgnoreCase);

    private static bool HasValidTerminalSignalContract(string sourceLabel, ReadySignalMetadata signal)
    {
        var expectsError = sourceLabel.Contains("error", StringComparison.OrdinalIgnoreCase);
        var expectedStatus = expectsError ? "error" : "success";
        if (!string.Equals(signal.Status, expectedStatus, StringComparison.OrdinalIgnoreCase))
            return false;

        if (string.IsNullOrWhiteSpace(signal.Timestamp) || !DateTimeOffset.TryParse(signal.Timestamp, out _))
            return false;

        if (!expectsError && (!signal.HasFilesModified || !signal.FilesModifiedValid))
            return false;

        return !expectsError || !string.IsNullOrWhiteSpace(signal.Error);
    }

    private async Task<bool> DiscardMismatchedReadySignalAsync(string sourceLabel, ReadySignalMetadata? signal,
        PendingTurnSnapshotManifest? manifest, bool preservePendingSnapshot = false)
    {
        if (signal == null)
        {
            ClearReadySignals();
            ClearTransientOutputFiles();
            if (!preservePendingSnapshot && manifest != null)
                await CleanupPendingTurnSnapshotAsync();

            AnsiConsole.MarkupLine("[yellow]⚠ Клиент отклонил повреждённый ответ GM и запросил корректную повторную обработку.[/]");
            return true;
        }

        if (manifest == null)
        {
            _logger.LogWarning(
                "Отклонён {SourceLabel}: отсутствует pending snapshot manifest для signal(session={SignalSession}, request={SignalRequest}, turn={SignalTurn})",
                sourceLabel,
                signal.SessionId,
                signal.RequestId,
                signal.TurnNumber);

            ClearReadySignals();
            ClearTransientOutputFiles();
            if (!preservePendingSnapshot && manifest != null)
                await CleanupPendingTurnSnapshotAsync();

            AnsiConsole.MarkupLine("[yellow]⚠ Клиент отклонил несогласованный ответ GM и восстановил безопасное ожидание.[/]");
            return true;
        }

        if (IsMatchingReadySignal(signal, manifest))
            return false;

        _logger.LogWarning(
            "Отклонён {SourceLabel}: signal(session={SignalSession}, request={SignalRequest}, turn={SignalTurn}) ожидался (session={ExpectedSession}, request={ExpectedRequest}, turn={ExpectedTurn})",
            sourceLabel,
            signal.SessionId,
            signal.RequestId,
            signal.TurnNumber,
            manifest.SessionId,
            manifest.RequestId,
            manifest.TurnNumber);

        ClearReadySignals();
        ClearTransientOutputFiles();
        if (!preservePendingSnapshot)
            await CleanupPendingTurnSnapshotAsync();

        AnsiConsole.MarkupLine("[yellow]⚠ Клиент проигнорировал устаревший или несвязанный ответ GM.[/]");
        return true;
    }

    private void ClearReadySignals()
    {
        if (_fs.FileExists("ready/turn_complete.json"))
            _fs.DeleteFile("ready/turn_complete.json");
        if (_fs.FileExists("ready/turn_error.json"))
            _fs.DeleteFile("ready/turn_error.json");
    }

    private void ClearTransientOutputFiles()
    {
        foreach (var file in new[]
        {
            "output/narrative_response.json",
            "output/interface_updates.json",
            "output/debug_logs.json",
            "output/ink_feather_action_result.json",
            QteSceneService.QteOfferPath,
            ProgressionScheduleService.ReportPath
        })
        {
            if (_fs.FileExists(file))
                _fs.DeleteFile(file);
        }
    }


    private IEnumerable<string> EnumerateRollbackTrackedFiles()
    {
        var gameSessionRoot = _fs.ResolvePath("");
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var absoluteFile in _fs.GetAllGameStateFiles())
        {
            var relative = Path.GetRelativePath(gameSessionRoot, absoluteFile).Replace('\\', '/');
            if (string.Equals(relative, ValidationRepairReadyPath, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(relative, ValidationRepairRequestPath, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(relative, "game_state/control/terminal_protocol_failure_request.json", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(relative, "game_state/history/chat_log.json", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(relative, PendingTurnSnapshotManifestPath, StringComparison.OrdinalIgnoreCase) ||
                relative.StartsWith($"{PendingTurnSnapshotDirectory}/", StringComparison.OrdinalIgnoreCase) ||
                relative.Contains(".rollback.", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            files.Add(relative);
        }

        foreach (var relativeDir in new[] { "lore" })
        {
            var absoluteDir = _fs.ResolvePath(relativeDir);
            if (!Directory.Exists(absoluteDir))
                continue;

            foreach (var absoluteFile in Directory.GetFiles(absoluteDir, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(gameSessionRoot, absoluteFile).Replace('\\', '/');
                if (relative.Contains(".rollback.", StringComparison.OrdinalIgnoreCase))
                    continue;
                files.Add(relative);
            }
        }

        foreach (var outputFile in new[]
        {
            "output/narrative_response.json",
            "output/interface_updates.json",
            "output/debug_logs.json",
            QteSceneService.QteOfferPath
        })
        {
            if (_fs.FileExists(outputFile))
                files.Add(outputFile);
        }

        return files;
    }

    private async Task<RollbackSnapshot> CreatePreTurnBackup(string backupId)
    {
        var snapshot = new RollbackSnapshot
        {
            BaselineFiles = new HashSet<string>(EnumerateRollbackTrackedFiles(), StringComparer.OrdinalIgnoreCase)
        };

        foreach (var file in snapshot.BaselineFiles)
        {
            if (_fs.FileExists(file))
            {
                var backupPath = file + $".rollback.{backupId}";
                try
                {
                    var content = await _fs.ReadFileAsync(file);
                    if (content != null)
                    {
                        await _fs.WriteFileAtomicAsync(backupPath, content);
                        snapshot.BackupFiles[file] = backupPath;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Не удалось создать backup для {File}", file);
                }
            }
        }

        return snapshot;
    }

    /// <summary>
    /// Restores game state files from pre-turn backups (escape-rollback).
    /// </summary>
    private async Task RestorePreTurnBackup(RollbackSnapshot snapshot)
    {
        foreach (var trackedFile in EnumerateRollbackTrackedFiles())
        {
            if (snapshot.BaselineFiles.Contains(trackedFile))
                continue;

            try
            {
                if (_fs.FileExists(trackedFile))
                    _fs.DeleteFile(trackedFile);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Не удалось удалить новый файл {File} при rollback", trackedFile);
            }
        }

        foreach (var (original, backup) in snapshot.BackupFiles)
        {
            try
            {
                var content = await _fs.ReadFileAsync(backup);
                if (content != null)
                    await _fs.WriteFileAtomicAsync(original, content);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Не удалось восстановить {File}", original);
            }
        }

        await RefreshRuntimeStateAsync();
    }

    /// <summary>
    /// Cleans up temporary rollback backup files.
    /// </summary>
    private void CleanupBackup(RollbackSnapshot snapshot)
    {
        foreach (var backup in snapshot.BackupFiles.Values)
        {
            try
            {
                _fs.DeleteFile(backup);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Не удалось удалить rollback backup {BackupPath}.", backup);
            }
        }
    }
}

