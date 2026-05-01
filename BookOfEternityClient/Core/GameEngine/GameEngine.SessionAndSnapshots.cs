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
        NpcTradeRequestState.PendingRequestPath,
        ShiningCoreActionRequestState.PendingActionsRequestPath,
        ShiningTradeRequestState.PendingRequestsPath,
        GuardianAbodeResidentRequestState.PendingResidentsRequestPath,
        GuardianAbodeResidentRequestState.PendingInteractionsRequestPath,
        GuardianAbodeResidentRequestState.PendingTransfersRequestPath,
        ActorSocialInteractionRequestState.PendingGuardianRequestPath,
        ActorSocialInteractionRequestState.PendingNpcRequestPath,
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
        var rollbackBaselineFiles = rollbackSnapshot?.BaselineFiles is { Count: > 0 }
            ? new HashSet<string>(rollbackSnapshot.BaselineFiles, StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(EnumerateRollbackTrackedFiles(), StringComparer.OrdinalIgnoreCase);

        foreach (var file in GuardianPolicySnapshotRequestFiles)
            rollbackBaselineFiles.Add(file);

        foreach (var file in rollbackBaselineFiles.OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
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
            RollbackBaselineFiles = rollbackBaselineFiles
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            SourceLabel = sourceLabel
        };
        manifest.ManifestPayloadHash = ComputePendingTurnManifestPayloadHash(manifest);
        var authorityJson = PendingTurnSnapshotAuthority.CreateDetachedAuthorityJson(
            manifest,
            SnapshotHashJsonOpts,
            static snapshotManifest => snapshotManifest.ManifestPayloadHash,
            static (snapshotManifest, hash) => snapshotManifest.ManifestPayloadHash = hash,
            static snapshotManifest => snapshotManifest.SessionId,
            static snapshotManifest => snapshotManifest.RequestId,
            static snapshotManifest => snapshotManifest.TurnNumber,
            static snapshotManifest => snapshotManifest.Files,
            static snapshotManifest => snapshotManifest.SnapshotFileHashes,
            static snapshotManifest => snapshotManifest.ClientOwnedValidationHashes,
            static snapshotManifest => snapshotManifest.RollbackBaselineFiles,
            static snapshotManifest => snapshotManifest.SourceLabel,
            static snapshotManifest => snapshotManifest.RollbackBackups,
            ReadRelativeFileFromWorkspace);

        try
        {
            await _fs.WriteFileAtomicAsync(
                PendingTurnSnapshotManifestPath,
                JsonSerializer.Serialize(manifest, JsonOpts));
            await _fs.WriteFileAtomicAsync(PendingTurnSnapshotAuthority.AuthorityPath, authorityJson);
        }
        catch
        {
            if (_fs.FileExists(PendingTurnSnapshotManifestPath))
                _fs.DeleteFile(PendingTurnSnapshotManifestPath);
            if (_fs.FileExists(PendingTurnSnapshotAuthority.AuthorityPath))
                _fs.DeleteFile(PendingTurnSnapshotAuthority.AuthorityPath);
            throw;
        }

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

        if (manifest.TurnNumber != expectedTurnNumber)
            return null;

        var payload = await LoadValidatedCurrentPendingTurnSnapshotAuthorityPayloadAsync(manifest);
        if (payload == null)
            return null;

        var canonicalFiles = new HashSet<string>(CanonicalStateNormalizer.CanonicalAccumulatedFiles, StringComparer.OrdinalIgnoreCase);
        if (!PendingTurnSnapshotAuthority.HasValidatedSnapshotCoverage(
                payload,
                static authorityPayload => authorityPayload.Files,
                static authorityPayload => authorityPayload.SnapshotFileHashes,
                canonicalFiles,
                out _,
                static authorityPayload => authorityPayload.RollbackBaselineFiles,
                requireRollbackBaselineRegistration: true))
        {
            return null;
        }

        var snapshot = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var relativePath in canonicalFiles)
        {
            if (!payload.Files.TryGetValue(relativePath, out var snapshotPath) ||
                string.IsNullOrWhiteSpace(snapshotPath) ||
                !payload.SnapshotFileHashes.TryGetValue(relativePath, out var expectedSnapshotHash) ||
                string.IsNullOrWhiteSpace(expectedSnapshotHash))
            {
                return null;
            }

            var snapshotContent = await _fs.ReadFileAsync(snapshotPath);
            if (string.IsNullOrWhiteSpace(snapshotContent) ||
                !string.Equals(ComputeSha256(snapshotContent), expectedSnapshotHash, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            snapshot[relativePath] = snapshotPath;
        }

        return snapshot.Count == canonicalFiles.Count ? snapshot : null;
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

    private async Task<PendingTurnSnapshotResolution> ResolveActivePendingTurnSnapshotContextAsync(
        bool requireCurrentContext = true)
    {
        var manifestExists = _fs.FileExists(PendingTurnSnapshotManifestPath);
        var manifest = await LoadPendingTurnSnapshotManifestAsync();
        if (manifest == null)
        {
            return new PendingTurnSnapshotResolution
            {
                Status = manifestExists
                    ? PendingTurnSnapshotResolutionStatus.Unusable
                    : PendingTurnSnapshotResolutionStatus.Missing
            };
        }

        var snapshotContext = await LoadValidatedPendingTurnSnapshotContextAsync(manifest, requireCurrentContext);
        return new PendingTurnSnapshotResolution
        {
            Status = snapshotContext == null
                ? PendingTurnSnapshotResolutionStatus.Unusable
                : PendingTurnSnapshotResolutionStatus.Usable,
            Manifest = manifest,
            Context = snapshotContext
        };
    }

    private sealed record PendingTurnSnapshotRequestContext(
        string SessionId,
        string RequestId,
        int TurnNumber);

    private async Task<bool> IsCurrentPendingTurnSnapshotAsync(PendingTurnSnapshotManifest manifest)
    {
        const string repairRequestPath = "game_state/control/validation_repair_request.json";
        var repairContext = await ReadPendingTurnSnapshotRequestContextAsync(repairRequestPath);
        if (DoesPendingTurnRequestContextMatchManifest(manifest, repairContext))
            return true;

        var turnContext = await ReadPendingTurnSnapshotRequestContextAsync("input/turn_request.json");
        return DoesPendingTurnRequestContextMatchManifest(manifest, turnContext);
    }

    private async Task<PendingTurnSnapshotRequestContext?> ReadPendingTurnSnapshotRequestContextAsync(string relativePath)
    {
        var json = await _fs.ReadFileAsync(relativePath);
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return null;

            var sessionId = doc.RootElement.TryGetProperty("sessionId", out var sessionIdNode) &&
                            sessionIdNode.ValueKind == JsonValueKind.String
                ? sessionIdNode.GetString() ?? string.Empty
                : string.Empty;
            var requestId = doc.RootElement.TryGetProperty("requestId", out var requestIdNode) &&
                            requestIdNode.ValueKind == JsonValueKind.String
                ? requestIdNode.GetString() ?? string.Empty
                : string.Empty;
            var turnNumber = doc.RootElement.TryGetProperty("turnNumber", out var turnNumberNode) &&
                             turnNumberNode.ValueKind == JsonValueKind.Number &&
                             turnNumberNode.TryGetInt32(out var parsedTurnNumber)
                ? parsedTurnNumber
                : 0;

            return new PendingTurnSnapshotRequestContext(sessionId, requestId, turnNumber);
        }
        catch
        {
            return null;
        }
    }

    private static bool DoesPendingTurnRequestContextMatchManifest(
        PendingTurnSnapshotManifest manifest,
        PendingTurnSnapshotRequestContext? context)
    {
        if (context == null)
            return false;

        if (manifest.TurnNumber != context.TurnNumber)
            return false;

        if (!PendingTurnSnapshotAuthority.DoesPendingTurnContextIdMatch(manifest.SessionId, context.SessionId))
            return false;

        return PendingTurnSnapshotAuthority.DoesPendingTurnContextIdMatch(manifest.RequestId, context.RequestId);
    }

    private string ComputePendingTurnManifestPayloadHash(PendingTurnSnapshotManifest manifest)
    {
        return PendingTurnSnapshotAuthority.ComputeManifestPayloadHash(
            manifest,
            SnapshotHashJsonOpts,
            static snapshotManifest => snapshotManifest.ManifestPayloadHash,
            static (snapshotManifest, hash) => snapshotManifest.ManifestPayloadHash = hash);
    }

    private static string ComputeSha256(string content)
    {
        return PendingTurnSnapshotAuthority.ComputeSha256(content);
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
        var manifest = await LoadPendingTurnSnapshotManifestAsync();
        var payload = await LoadValidatedCurrentPendingTurnSnapshotAuthorityPayloadAsync(
            manifest,
            requireCurrentContext: false);

        if (payload != null)
        {
            try
            {
                foreach (var snapshotPath in payload.Files.Values.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    if (!IsValidatedPendingSnapshotArtifactPath(snapshotPath))
                        continue;

                    if (_fs.FileExists(snapshotPath))
                        _fs.DeleteFile(snapshotPath);
                }

                foreach (var rollbackPath in payload.RollbackBackups.Values.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    if (!IsValidatedRollbackBackupArtifactPath(rollbackPath))
                        continue;

                    if (_fs.FileExists(rollbackPath))
                        _fs.DeleteFile(rollbackPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Не удалось очистить pending turn snapshot artifacts.");
            }
        }
        else
        {
            try
            {
                var snapshotDirectoryPath = _fs.ResolvePath(PendingTurnSnapshotDirectory);
                if (Directory.Exists(snapshotDirectoryPath))
                    Directory.Delete(snapshotDirectoryPath, recursive: true);

                foreach (var rollbackFile in Directory.EnumerateFiles(_fs.GameSessionPath, "*.rollback.*", SearchOption.AllDirectories))
                {
                    if (File.Exists(rollbackFile))
                        File.Delete(rollbackFile);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Не удалось безопасно очистить fallback pending turn snapshot artifacts.");
            }
        }

        if (_fs.FileExists(PendingTurnSnapshotManifestPath))
            _fs.DeleteFile(PendingTurnSnapshotManifestPath);
        if (_fs.FileExists(PendingTurnSnapshotAuthority.AuthorityPath))
            _fs.DeleteFile(PendingTurnSnapshotAuthority.AuthorityPath);
    }

    private static bool HasRollbackCapability(RollbackSnapshot? snapshot) =>
        snapshot != null && (snapshot.BackupFiles.Count > 0 || snapshot.BaselineFiles.Count > 0);

    private async Task<RollbackSnapshot?> GetValidatedRollbackSnapshotAsync(PendingTurnSnapshotManifest? manifest)
    {
        return BuildValidatedRollbackSnapshot(await LoadValidatedPendingTurnSnapshotContextAsync(manifest));
    }

    private async Task<PendingTurnSnapshotAuthority.PendingTurnSnapshotAuthorityPayload?> LoadValidatedCurrentPendingTurnSnapshotAuthorityPayloadAsync(
        PendingTurnSnapshotManifest? manifest,
        bool requireCurrentContext = true)
    {
        return (await LoadValidatedPendingTurnSnapshotContextAsync(manifest, requireCurrentContext))?.Payload;
    }

    private async Task<ValidatedPendingTurnSnapshotContext?> LoadValidatedPendingTurnSnapshotContextAsync(
        PendingTurnSnapshotManifest? manifest,
        bool requireCurrentContext = true)
    {
        if (manifest == null)
            return null;

        var authorityJson = await _fs.ReadFileAsync(PendingTurnSnapshotAuthority.AuthorityPath);
        if (!PendingTurnSnapshotAuthority.TryValidateManifestForDestructiveAuthority(
                manifest,
                authorityJson,
                SnapshotHashJsonOpts,
                static snapshotManifest => snapshotManifest.ManifestPayloadHash,
                static (snapshotManifest, hash) => snapshotManifest.ManifestPayloadHash = hash,
                static snapshotManifest => snapshotManifest.SessionId,
                static snapshotManifest => snapshotManifest.RequestId,
                static snapshotManifest => snapshotManifest.TurnNumber,
                static snapshotManifest => snapshotManifest.Files,
                static snapshotManifest => snapshotManifest.SnapshotFileHashes,
                static snapshotManifest => snapshotManifest.ClientOwnedValidationHashes,
                static snapshotManifest => snapshotManifest.RollbackBaselineFiles,
                static snapshotManifest => snapshotManifest.SourceLabel,
                static snapshotManifest => snapshotManifest.RollbackBackups,
                ReadRelativeFileFromWorkspace,
                out var payload,
                out _))
        {
            return null;
        }

        if (requireCurrentContext && !await IsCurrentPendingTurnSnapshotAsync(manifest))
            return null;

        return new ValidatedPendingTurnSnapshotContext
        {
            Manifest = manifest,
            Payload = payload!
        };
    }

    private RollbackSnapshot? BuildValidatedRollbackSnapshot(ValidatedPendingTurnSnapshotContext? snapshotContext)
    {
        if (snapshotContext == null)
            return null;

        var payload = snapshotContext.Payload;
        var snapshot = new RollbackSnapshot
        {
            BackupFiles = payload.RollbackBackups
                .Where(kv => !string.IsNullOrWhiteSpace(kv.Key) &&
                             !string.IsNullOrWhiteSpace(kv.Value) &&
                             IsValidatedRollbackBackupArtifactPath(kv.Value) &&
                             _fs.FileExists(kv.Value))
                .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase),
            BackupHashes = payload.RollbackBackupHashes
                .Where(kv => !string.IsNullOrWhiteSpace(kv.Key) &&
                             !string.IsNullOrWhiteSpace(kv.Value))
                .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase),
            BaselineFiles = new HashSet<string>(payload.RollbackBaselineFiles,
                StringComparer.OrdinalIgnoreCase)
        };

        return HasRollbackCapability(snapshot) ? snapshot : null;
    }

    private string? ReadRelativeFileFromWorkspace(string relativePath)
    {
        if (!PendingTurnSnapshotAuthority.IsSafeRelativePath(relativePath))
            return null;

        var fullPath = _fs.ResolvePath(relativePath);
        if (!File.Exists(fullPath))
            return null;

        try
        {
            return File.ReadAllText(fullPath, Encoding.UTF8);
        }
        catch
        {
            return null;
        }
    }

    private static bool IsValidatedPendingSnapshotArtifactPath(string relativePath)
    {
        return PendingTurnSnapshotAuthority.IsSafeRelativePath(relativePath) &&
               relativePath.StartsWith($"{PendingTurnSnapshotDirectory}/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsValidatedRollbackBackupArtifactPath(string relativePath)
    {
        return PendingTurnSnapshotAuthority.IsSafeRelativePath(relativePath) &&
               relativePath.Contains(".rollback.", StringComparison.OrdinalIgnoreCase);
    }

    private async Task NormalizePendingRepairArtifactsAsync()
    {
        var repairRequestExists = _fs.FileExists(ValidationRepairRequestPath);
        var repairReadyExists = _fs.FileExists(ValidationRepairReadyPath);
        if (!repairRequestExists && !repairReadyExists)
            return;

        var pendingSnapshot = await ResolveActivePendingTurnSnapshotContextAsync();
        if (pendingSnapshot.Status == PendingTurnSnapshotResolutionStatus.Missing)
        {
            _logger.LogWarning("Найдены repair-файлы без pending snapshot manifest. Очистка как stale state.");
            await DeleteValidationRepairFilesAsync();
            return;
        }

        if (pendingSnapshot.Status != PendingTurnSnapshotResolutionStatus.Usable || pendingSnapshot.Context == null)
        {
            _logger.LogWarning("Найдены repair-файлы с unreadable/invalid validated pending snapshot authority. Очистка как stale state.");
            await DeleteValidationRepairFilesAsync();
            return;
        }

        var snapshotContext = pendingSnapshot.Context;

        if (repairReadyExists && !repairRequestExists)
        {
            _logger.LogWarning(
                "Найден orphaned validation_repair_ready для pending turn(session={Session}, request={Request}, turn={Turn}). Удаление ready-файла без затрагивания основного pending turn state.",
                snapshotContext.SessionId,
                snapshotContext.RequestId,
                snapshotContext.TurnNumber);
            await DeleteValidationRepairReadyAsync();
            return;
        }

        if (repairRequestExists)
        {
            if (!_fs.FileExists("ready/turn_complete.json"))
            {
                _logger.LogWarning(
                    "Найден validation_repair_request без correlated ready/turn_complete.json для pending turn(session={Session}, request={Request}, turn={Turn}). Очистка stale repair artifacts.",
                    snapshotContext.SessionId,
                    snapshotContext.RequestId,
                    snapshotContext.TurnNumber);
                await DeleteValidationRepairFilesAsync();
                return;
            }

            var turnCompleteMetadata = await ReadReadySignalMetadataAsync("ready/turn_complete.json");
            if (turnCompleteMetadata == null ||
                !string.Equals(turnCompleteMetadata.SessionId, snapshotContext.SessionId, StringComparison.Ordinal) ||
                !string.Equals(turnCompleteMetadata.RequestId, snapshotContext.RequestId, StringComparison.Ordinal) ||
                turnCompleteMetadata.TurnNumber != snapshotContext.TurnNumber)
            {
                _logger.LogWarning(
                    "Найден validation_repair_request с некоррелированным ready/turn_complete.json. Очистка stale repair artifacts для pending turn(session={Session}, request={Request}, turn={Turn}).",
                    snapshotContext.SessionId,
                    snapshotContext.RequestId,
                    snapshotContext.TurnNumber);
                _fs.DeleteFile("ready/turn_complete.json");
                await DeleteValidationRepairFilesAsync();
                return;
            }

            _logger.LogInformation(
                "Обнаружен активный repair cycle для pending turn(session={Session}, request={Request}, turn={Turn}). Он будет продолжен через correlated late-response validation.",
                snapshotContext.SessionId,
                snapshotContext.RequestId,
                snapshotContext.TurnNumber);
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
        await _stateManager.RefreshGameStateAsync();
        var pendingSnapshot = await ResolveActivePendingTurnSnapshotContextAsync();
        var hasReadySignals = HasTerminalReadySignal();
        var preserveControlFilesForTerminalValidation =
            ShouldPreserveClientOwnedControlFilesForTerminalValidation(hasReadySignals, pendingSnapshot);

        await NormalizePendingRepairArtifactsAsync();
        await NormalizePendingTerminalProtocolFailureArtifactsAsync();
        await AfterlifeNotificationState.SyncFromCurrentStateAsync(_fs);
        await AfterlifeNotificationState.EnsureHealthyAsync(_fs);
        var hasActivePendingSnapshotArtifacts = hasReadySignals ||
                                                pendingSnapshot.Status == PendingTurnSnapshotResolutionStatus.Usable;
        if (!preserveControlFilesForTerminalValidation)
        {
            await _afterlifeReturnGuardService.EnsureHealthyAsync(_stateManager.CurrentState.CurrentRealm);
            await _systemGuardianLibraryService.EnsureAttractionRequestHealthyAsync(_stateManager.CurrentState.CurrentRealm);
            await GuardianAbodeOfferingState.EnsureHealthyAsync(_fs, _stateManager.CurrentState.CurrentRealm);
            await GuardianTradeRequestState.EnsureHealthyAsync(_fs, _stateManager.CurrentState.CurrentRealm);
            await PlayerGuardianFoundationState.EnsureHealthyAsync(_fs, _stateManager.CurrentState.CurrentRealm);
            await NpcTradeRequestState.EnsureHealthyAsync(_fs, _stateManager.CurrentState.CurrentRealm);
            await AfterlifeArchiveActionState.EnsureHealthyAsync(_fs, _stateManager.CurrentState.CurrentRealm);
            await GuardianAbodeResidentRequestState.EnsureHealthyAsync(_fs, _stateManager.CurrentState.CurrentRealm);
            if (!hasActivePendingSnapshotArtifacts)
            {
                await ShiningCoreActionRequestState.EnsureHealthyAsync(_fs, _stateManager.CurrentState.CurrentRealm);
                await ShiningTradeRequestState.EnsureHealthyAsync(_fs, _stateManager.CurrentState.CurrentRealm);
                await ShiningFactionRequestState.EnsureHealthyAsync(_fs, _stateManager.CurrentState.CurrentRealm);
            }
            await ActorSocialInteractionRequestState.EnsureHealthyAsync(_fs, _stateManager.CurrentState.CurrentRealm);
            await GuardianAbodeResidentRequestState.EnsureManifestationRequestForCurrentIncarnationAsync(_fs, _stateManager.CurrentState.CurrentRealm);
        }

        await _qteSceneService.EnsureRuntimeStateHealthyAsync();

        if (pendingSnapshot.Status == PendingTurnSnapshotResolutionStatus.Missing && hasReadySignals)
        {
            _logger.LogWarning("Найдены ready-сигналы без pending snapshot manifest. Очистка как stale runtime artifacts.");
            ClearReadySignals();
        }

        if (pendingSnapshot.Status == PendingTurnSnapshotResolutionStatus.Unusable && hasReadySignals)
        {
            _logger.LogWarning("Найдены ready-сигналы с unreadable/invalid validated pending snapshot authority. Очистка как stale runtime artifacts.");
            ClearReadySignals();
        }

        if (pendingSnapshot.Status == PendingTurnSnapshotResolutionStatus.Usable)
            return;

        if (_fs.FileExists("input/turn_request.json"))
        {
            _logger.LogWarning(
                pendingSnapshot.Status == PendingTurnSnapshotResolutionStatus.Missing
                    ? "Найден orphaned input/turn_request.json без pending snapshot manifest. Удаление как stale runtime artifact."
                    : "Найден input/turn_request.json с unreadable/invalid validated pending snapshot authority. Удаление как stale runtime artifact.");
            _fs.DeleteFile("input/turn_request.json");
        }
    }

    private bool HasTerminalReadySignal() =>
        _fs.FileExists("ready/turn_complete.json") ||
        _fs.FileExists("ready/turn_error.json");

    private async Task CleanupAcceptedTurnTerminalArtifactsAsync()
    {
        _fs.DeleteFile("ready/turn_complete.json");
        _fs.DeleteFile("ready/turn_error.json");
        await CleanupPendingTurnSnapshotAsync();
        await CleanupResolvedAfterlifePendingContractsAfterAcceptedTurnAsync();
    }

    private async Task CleanupResolvedAfterlifePendingContractsAfterAcceptedTurnAsync()
    {
        await _stateManager.RefreshGameStateAsync();
        var currentRealm = _stateManager.CurrentState.CurrentRealm;

        await _afterlifeReturnGuardService.EnsureHealthyAsync(currentRealm);
        await _systemGuardianLibraryService.EnsureAttractionRequestHealthyAsync(currentRealm);
        await GuardianAbodeOfferingState.EnsureHealthyAsync(_fs, currentRealm);
        await GuardianTradeRequestState.EnsureHealthyAsync(_fs, currentRealm);
        await PlayerGuardianFoundationState.EnsureHealthyAsync(_fs, currentRealm);
        await NpcTradeRequestState.EnsureHealthyAsync(_fs, currentRealm);
        await AfterlifeArchiveActionState.EnsureHealthyAsync(_fs, currentRealm);
        await GuardianAbodeResidentRequestState.EnsureHealthyAsync(_fs, currentRealm);
        await ShiningCoreActionRequestState.EnsureHealthyAsync(_fs, currentRealm);
        await ShiningTradeRequestState.EnsureHealthyAsync(_fs, currentRealm);
        await ShiningFactionRequestState.EnsureHealthyAsync(_fs, currentRealm);
        await ActorSocialInteractionRequestState.EnsureHealthyAsync(_fs, currentRealm);
        await GuardianAbodeResidentRequestState.EnsureManifestationRequestForCurrentIncarnationAsync(_fs, currentRealm);
        await AfterlifeNotificationState.SyncFromCurrentStateAsync(_fs);
        await AfterlifeNotificationState.EnsureHealthyAsync(_fs);
    }

    private static bool ShouldPreserveClientOwnedControlFilesForTerminalValidation(
        bool hasReadySignals,
        PendingTurnSnapshotResolution pendingSnapshot) =>
        hasReadySignals &&
        pendingSnapshot.Status == PendingTurnSnapshotResolutionStatus.Usable;

    private async Task<bool> ShouldPreserveClientOwnedControlFilesForTerminalValidationAsync()
    {
        if (!HasTerminalReadySignal())
            return false;

        var pendingSnapshot = await ResolveActivePendingTurnSnapshotContextAsync();
        return ShouldPreserveClientOwnedControlFilesForTerminalValidation(
            hasReadySignals: true,
            pendingSnapshot);
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

    private bool IsMatchingReadySignal(ReadySignalMetadata signal, ValidatedPendingTurnSnapshotContext snapshotContext) =>
        signal.TurnNumber == snapshotContext.TurnNumber &&
        !string.IsNullOrWhiteSpace(signal.RequestId) &&
        string.Equals(signal.RequestId, snapshotContext.RequestId, StringComparison.OrdinalIgnoreCase) &&
        !string.IsNullOrWhiteSpace(signal.SessionId) &&
        string.Equals(signal.SessionId, snapshotContext.SessionId, StringComparison.OrdinalIgnoreCase);

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
        ValidatedPendingTurnSnapshotContext? snapshotContext, bool preservePendingSnapshot = false)
    {
        if (signal == null)
        {
            ClearReadySignals();
            ClearTransientOutputFiles();
            if (!preservePendingSnapshot && snapshotContext != null)
                await CleanupPendingTurnSnapshotAsync();

            AnsiConsole.MarkupLine("[yellow]⚠ Клиент отклонил повреждённый ответ GM и запросил корректную повторную обработку.[/]");
            return true;
        }

        if (snapshotContext == null)
        {
            _logger.LogWarning(
                "Отклонён {SourceLabel}: отсутствует pending snapshot manifest для signal(session={SignalSession}, request={SignalRequest}, turn={SignalTurn})",
                sourceLabel,
                signal.SessionId,
                signal.RequestId,
                signal.TurnNumber);

            ClearReadySignals();
            ClearTransientOutputFiles();
            if (!preservePendingSnapshot)
                await CleanupPendingTurnSnapshotAsync();

            AnsiConsole.MarkupLine("[yellow]⚠ Клиент отклонил несогласованный ответ GM и восстановил безопасное ожидание.[/]");
            return true;
        }

        if (IsMatchingReadySignal(signal, snapshotContext))
            return false;

        _logger.LogWarning(
            "Отклонён {SourceLabel}: signal(session={SignalSession}, request={SignalRequest}, turn={SignalTurn}) ожидался (session={ExpectedSession}, request={ExpectedRequest}, turn={ExpectedTurn})",
            sourceLabel,
            signal.SessionId,
            signal.RequestId,
            signal.TurnNumber,
            snapshotContext.SessionId,
            snapshotContext.RequestId,
            snapshotContext.TurnNumber);

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
                string.Equals(relative, PendingTurnSnapshotAuthority.AuthorityPath, StringComparison.OrdinalIgnoreCase) ||
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
                        snapshot.BackupHashes[file] = ComputeSha256(content);
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
                if (content != null &&
                    snapshot.BackupHashes.TryGetValue(original, out var expectedHash) &&
                    string.Equals(ComputeSha256(content), expectedHash, StringComparison.OrdinalIgnoreCase))
                {
                    await _fs.WriteFileAtomicAsync(original, content);
                }
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

