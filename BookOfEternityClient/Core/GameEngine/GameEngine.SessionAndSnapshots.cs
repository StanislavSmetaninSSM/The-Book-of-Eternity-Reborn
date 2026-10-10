using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using BookOfEternityClient.Services.GmWorkers;
using BookOfEternityClient.UI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Spectre.Console;

namespace BookOfEternityClient.Core;

public partial class GameEngine
{
    private static readonly string[] GuardianPolicySnapshotRequestFiles =
    {
        GuardianAbodeOfferingState.PendingRequestPath,
        GuardianTradeRequestState.PendingRequestPath,
        NpcTradeRequestState.PendingRequestPath,
        CraftRequestState.PendingRequestPath,
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
        return await SessionOperationContext.RunParticipatingCurrentSessionAsync(_fs, BuildGameResponseFromFilesCoreAsync);
    }

    private async Task<GameResponse> BuildGameResponseFromFilesCoreAsync()
    {
        var response = new GameResponse();
        var spiritualOutput = Interlocked.Exchange(ref _acceptedTurnSpiritualOutput, null);
        IReadOnlyDictionary<string, string?>? acceptedOutputs = null;
        if (spiritualOutput is not null)
        {
            try
            {
                acceptedOutputs = await spiritualOutput.ReadCurrentTextsAsync(_fs);
            }
            catch
            {
                TakeAcceptedTurnWoundNotifications();
                throw;
            }
        }

        // 1. Read narrative from output/narrative_response.json (primary source per API spec)
        var narrativeJson = acceptedOutputs is null
            ? await _fs.ReadFileAsync("output/narrative_response.json")
            : acceptedOutputs["output/narrative_response.json"];
        if (narrativeJson != null)
        {
            try
            {
                using var doc = JsonDocument.Parse(narrativeJson);
                if (doc.RootElement.TryGetProperty("response", out var r))
                    response.Response = PlayerFacingTextNormalizer.NormalizeEscapedLineBreakArtifacts(r.GetString());
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
        var uiJson = acceptedOutputs is null
            ? await _fs.ReadFileAsync("output/interface_updates.json")
            : acceptedOutputs["output/interface_updates.json"];
        if (uiJson != null)
        {
            try
            {
                using var doc = JsonDocument.Parse(uiJson);
                if (doc.RootElement.TryGetProperty("dialogueOptions", out var opts) &&
                    opts.ValueKind == JsonValueKind.Array)
                {
                    response.DialogueOptions = JsonSerializer.Deserialize<DialogueOption[]>(opts.GetRawText());
                    if (response.DialogueOptions != null)
                    {
                        foreach (var option in response.DialogueOptions)
                        {
                            var normalizedText = PlayerFacingTextNormalizer.NormalizeEscapedLineBreakArtifacts(option.Text);
                            var normalizedInputValue = PlayerFacingTextNormalizer.NormalizeEscapedLineBreakArtifacts(option.InputValue);
                            option.InputValue = DialogueOptionControlTagNormalizer.ResolveInputValue(normalizedText, normalizedInputValue);
                            option.Text = DialogueOptionControlTagNormalizer.NormalizeVisibleText(normalizedText);
                        }
                    }
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
        var debugJson = acceptedOutputs is null
            ? await _fs.ReadFileAsync("output/debug_logs.json")
            : acceptedOutputs["output/debug_logs.json"];
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
            CurrentCondition = st.CurrentCondition
        };

        var woundNotifications = TakeAcceptedTurnWoundNotifications();
        if (woundNotifications.Count != 0)
        {
            response.WoundNotifications = woundNotifications
                .Select(static value => value.Text.PlainText)
                .ToArray();
        }

        return response;
    }

    private IReadOnlyList<WoundPlayerNotification> TakeAcceptedTurnWoundNotifications()
    {
        var notifications = Interlocked.Exchange(
            ref _acceptedTurnWoundNotifications,
            Array.Empty<WoundPlayerNotification>());
        return Array.AsReadOnly(notifications.Select(static value => value with
        {
            Text = value.Text with { }
        }).ToArray());
    }

    private GameResponse MergeWithLastResponse(GameResponse? refreshed)
    {
        return GameResponseRefreshMerger.Merge(_lastResponse, refreshed);
    }

    private async Task RefreshRuntimeStateAsync()
    {
        await SessionOperationContext.RunParticipatingCurrentSessionAsync(_fs, RefreshRuntimeStateCoreAsync);
    }

    private async Task RefreshRuntimeStateCoreAsync()
    {
        if (HasBrowserInputWakeHint() && await WithPendingActionLeaseAsync(async lease =>
            {
                var pending = await PendingPlayerActionService.ReadAsync(_fs, lease);
                if (pending?.Phase != "staged") return false;
                ValidateDetachedBrowserBinding(PendingPlayerActionService.ReadStaged(pending));
                await _stateManager.RefreshGameStateReadOnlyAsync(lease);
                return true;
            }))
            return;
        await _stateManager.RefreshGameStateAsync();
        await _progressionSchedule.EnsureInitializedAsync();
    }

    private async Task RebindRuntimeAfterSessionReplacementAsync(string? establishedGeneration = null)
    {
        _lastResponse = null;
        _acceptedTurnWoundNotifications = Array.Empty<WoundPlayerNotification>();
        _acceptedTurnSpiritualOutput = null;
        _acceptedTurnSpiritualConflictValidation = null;
        _acceptedTurnGuardianMusingsValidation = null;
        _pendingImagePrompt = null;
        _pendingMemoryLegacyAwaitingConsumption = false;
        _mainMenuSessionWarning = null;
        _lastConsoleWidth = 0;
        _lastKnownLevel = 1;
        _explorer.ForgetSessionTransientState();

        var replacementGeneration = establishedGeneration ?? await CaptureCurrentSessionGenerationAsync();
        await SessionOperationContext.RunBoundAsync(_fs, replacementGeneration, async () =>
        {
            await RefreshRuntimeStateAsync();
            var replacementState = _stateManager.CurrentState;

            string? replacementSessionId = null;
            var hasSoulState = false;
            await using (var writeLease = await _fs.AcquireCanonicalWriteLeaseAsync())
            {
                hasSoulState = _fs.FileExists(writeLease, "game_state/meta/soul_state.json");
                if (hasSoulState &&
                    (string.IsNullOrWhiteSpace(replacementState.SessionId) &&
                     !string.IsNullOrWhiteSpace(replacementState.SoulName)))
                {
                    replacementSessionId = _fs.GetOrCreateSessionGeneration(writeLease);
                }
            }

            var hasActiveSession = hasSoulState &&
                                   (!string.IsNullOrWhiteSpace(replacementState.SessionId) ||
                                    !string.IsNullOrWhiteSpace(replacementState.SoulName));
            if (!hasActiveSession)
            {
                _gameLoop.SetSession(string.Empty, 0);
                _inGame = false;
                return;
            }

            replacementSessionId ??= replacementState.SessionId;
            _gameLoop.SetSession(replacementSessionId, Math.Max(0, replacementState.TurnNumber));
        });
    }

    private async Task<AcceptedTurnCanonicalStateRefresh.Result> RefreshCanonicalStateAsync(
        IReadOnlyDictionary<string, string> backups)
    {
        var result = await AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(
            _fs,
            _normalizer,
            _validator,
            backups);
        if (result.TreatmentResourcePublicationTransaction is not null)
        {
            try
            {
                if (!result.Issues.Any(issue => issue.Severity == IssueSeverity.Error))
                    await RefreshRuntimeStateAsync();
                return result;
            }
            catch (CompensatedTreatmentPublicationException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw await BuildCompensatedTreatmentPublicationExceptionAsync(
                    result.TreatmentResourcePublicationTransaction,
                    exception);
            }
        }

        if (!result.Issues.Any(issue => issue.Severity == IssueSeverity.Error))
            await RefreshRuntimeStateAsync();
        return result;
    }

    private async Task EnsureAfterlifeSpiritualConflictStateInitializedForSnapshotAsync(
        FileSystemManager.CanonicalWriteLease writeLease)
    {
        if (_fs.FileExists(writeLease, AfterlifeSpiritualConflictState.StatePath))
            return;

        var soulJson = await _fs.ReadFileAsync(writeLease, "game_state/meta/soul_state.json");
        if (string.IsNullOrWhiteSpace(soulJson))
            return;

        try
        {
            var soulRoot = JsonNode.Parse(soulJson) as JsonObject;
            var currentRealm = soulRoot?["currentRealm"]?.GetValue<string>();
            if (!AfterlifeSpiritualConflictState.IsAfterlifeRealm(currentRealm))
                return;

            await _fs.WriteFileAtomicAsync(
                writeLease,
                AfterlifeSpiritualConflictState.StatePath,
                AfterlifeSpiritualConflictState.CreateDefaultRoot().ToJsonString(JsonOpts));
        }
        catch (Exception ex) when (ex is not CoordinatedStatePublicationUncertainException)
        {
            _logger.LogDebug(ex, "Не удалось инициализировать afterlife spiritual conflict state перед snapshot.");
        }
    }

    private async Task EnsureAfterlifeEntityProfileStateInitializedForSnapshotAsync(
        FileSystemManager.CanonicalWriteLease writeLease)
    {
        if (_fs.FileExists(writeLease, AfterlifeEntityProfileState.StatePath))
            return;

        var soulJson = await _fs.ReadFileAsync(writeLease, "game_state/meta/soul_state.json");
        if (string.IsNullOrWhiteSpace(soulJson))
            return;

        try
        {
            var soulRoot = JsonNode.Parse(soulJson) as JsonObject;
            var currentRealm = soulRoot?["currentRealm"]?.GetValue<string>();
            if (!AfterlifeSpiritualConflictState.IsAfterlifeRealm(currentRealm))
                return;

            await _fs.WriteFileAtomicAsync(
                writeLease,
                AfterlifeEntityProfileState.StatePath,
                AfterlifeEntityProfileState.CreateDefaultRoot().ToJsonString(JsonOpts));
        }
        catch (Exception ex) when (ex is not CoordinatedStatePublicationUncertainException)
        {
            _logger.LogDebug(ex, "Не удалось инициализировать afterlife entity profile state перед snapshot.");
        }
    }

    private async Task EnsureAfterlifeChronicleStateInitializedForSnapshotAsync(
        FileSystemManager.CanonicalWriteLease writeLease)
    {
        if (_fs.FileExists(writeLease, AfterlifeChronicleState.StatePath))
            return;

        var soulJson = await _fs.ReadFileAsync(writeLease, "game_state/meta/soul_state.json");
        if (string.IsNullOrWhiteSpace(soulJson))
            return;

        try
        {
            var soulRoot = JsonNode.Parse(soulJson) as JsonObject;
            var currentRealm = soulRoot?["currentRealm"]?.GetValue<string>();
            if (!AfterlifeSpiritualConflictState.IsAfterlifeRealm(currentRealm))
                return;

            await _fs.WriteFileAtomicAsync(
                writeLease,
                AfterlifeChronicleState.StatePath,
                AfterlifeChronicleState.CreateDefaultRoot().ToJsonString(JsonOpts));
        }
        catch (Exception ex) when (ex is not CoordinatedStatePublicationUncertainException)
        {
            _logger.LogDebug(ex, "Не удалось инициализировать afterlife chronicle state перед snapshot.");
        }
    }

    private async Task EnsureAfterlifeGlobalFlagStateInitializedForSnapshotAsync(
        FileSystemManager.CanonicalWriteLease writeLease)
    {
        if (_fs.FileExists(writeLease, AfterlifeGlobalFlagState.StatePath))
            return;

        var soulJson = await _fs.ReadFileAsync(writeLease, "game_state/meta/soul_state.json");
        if (string.IsNullOrWhiteSpace(soulJson))
            return;

        try
        {
            var soulRoot = JsonNode.Parse(soulJson) as JsonObject;
            var currentRealm = soulRoot?["currentRealm"]?.GetValue<string>();
            if (!AfterlifeSpiritualConflictState.IsAfterlifeRealm(currentRealm))
                return;

            await _fs.WriteFileAtomicAsync(
                writeLease,
                AfterlifeGlobalFlagState.StatePath,
                AfterlifeGlobalFlagState.CreateDefaultRoot().ToJsonString(JsonOpts));
        }
        catch (Exception ex) when (ex is not CoordinatedStatePublicationUncertainException)
        {
            _logger.LogDebug(ex, "Не удалось инициализировать afterlife global flag state перед snapshot.");
        }
    }


    private async Task<Dictionary<string, string>> CreateCanonicalBaselineSnapshotAsync(TurnRequest request,
        RollbackSnapshot? rollbackSnapshot = null,
        string? sourceLabel = null,
        PendingPlayerActionService.Binding? browserAction = null)
    {
        var writeLease = await _fs.AcquireCanonicalWriteLeaseAsync();
        CoordinatedStatePublicationUncertainException? uncertainty = null;
        try { return await CreateCanonicalBaselineSnapshotAsync(writeLease, request, rollbackSnapshot, sourceLabel, browserAction); }
        catch (CoordinatedStatePublicationUncertainException failure) { uncertainty = failure; throw; }
        finally { await CoordinatedStateWriteHelper.ReleaseOwnedLeaseAsync(_fs, writeLease, false, uncertainty); }
    }

    private async Task<Dictionary<string, string>> CreateCanonicalBaselineSnapshotAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        TurnRequest request,
        RollbackSnapshot? rollbackSnapshot = null,
        string? sourceLabel = null,
        PendingPlayerActionService.Binding? browserAction = null)
    {
        RequireExactEngineSnapshotInventory(writeLease, rollbackSnapshot);
        await DeleteTerminalProtocolFailureRequestAsync(writeLease);
        CleanupPendingTurnSnapshot(writeLease, rollbackSnapshot?.BackupFiles.Values);
        var conflictStateExistedBeforeInitialization = _fs.FileExists(
            writeLease,
            AfterlifeSpiritualConflictState.StatePath);
        var entityProfileStateExistedBeforeInitialization = _fs.FileExists(
            writeLease,
            AfterlifeEntityProfileState.StatePath);
        var chronicleStateExistedBeforeInitialization = _fs.FileExists(
            writeLease,
            AfterlifeChronicleState.StatePath);
        var globalFlagStateExistedBeforeInitialization = _fs.FileExists(
            writeLease,
            AfterlifeGlobalFlagState.StatePath);
        await EnsureAfterlifeSpiritualConflictStateInitializedForSnapshotAsync(writeLease);
        await EnsureAfterlifeEntityProfileStateInitializedForSnapshotAsync(writeLease);
        await EnsureAfterlifeChronicleStateInitializedForSnapshotAsync(writeLease);
        await EnsureAfterlifeGlobalFlagStateInitializedForSnapshotAsync(writeLease);
        await RegisterAfterlifeSpiritualConflictRollbackBackupIfInitializedAsync(
            writeLease,
            request,
            rollbackSnapshot,
            conflictStateExistedBeforeInitialization);
        await RegisterAfterlifeEntityProfileRollbackBackupIfInitializedAsync(
            writeLease,
            request,
            rollbackSnapshot,
            entityProfileStateExistedBeforeInitialization);
        await RegisterAfterlifeChronicleRollbackBackupIfInitializedAsync(
            writeLease,
            request,
            rollbackSnapshot,
            chronicleStateExistedBeforeInitialization);
        await RegisterAfterlifeGlobalFlagRollbackBackupIfInitializedAsync(
            writeLease,
            request,
            rollbackSnapshot,
            globalFlagStateExistedBeforeInitialization);

        RequireExactEngineSnapshotInventory(writeLease, rollbackSnapshot);
        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var snapshotHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var clientOwnedValidationHashes = await CaptureClientOwnedValidationHashesAsync(writeLease);
        var rollbackBaselineFiles = rollbackSnapshot?.BaselineFiles is { Count: > 0 }
            ? new HashSet<string>(rollbackSnapshot.BaselineFiles, StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(EnumerateRollbackTrackedFiles(writeLease), StringComparer.OrdinalIgnoreCase);

        foreach (var file in GuardianPolicySnapshotRequestFiles)
        {
            if (_fs.FileExists(writeLease, file))
                rollbackBaselineFiles.Add(file);
            else
                rollbackBaselineFiles.Remove(file);
        }
        if (_fs.FileExists(writeLease, SourceOfLightCapstoneState.PendingRequestPath))
            rollbackBaselineFiles.Add(SourceOfLightCapstoneState.PendingRequestPath);
        else
            rollbackBaselineFiles.Remove(SourceOfLightCapstoneState.PendingRequestPath);
        if (_fs.FileExists(writeLease, SarefMainStoryState.PendingWingsInfiltrationPath))
            rollbackBaselineFiles.Add(SarefMainStoryState.PendingWingsInfiltrationPath);
        else
            rollbackBaselineFiles.Remove(SarefMainStoryState.PendingWingsInfiltrationPath);
        if (_fs.FileExists(writeLease, AfterlifeSpiritualConflictState.StatePath))
            rollbackBaselineFiles.Add(AfterlifeSpiritualConflictState.StatePath);
        if (_fs.FileExists(writeLease, AfterlifeEntityProfileState.StatePath))
            rollbackBaselineFiles.Add(AfterlifeEntityProfileState.StatePath);
        if (_fs.FileExists(writeLease, AfterlifeChronicleState.StatePath))
            rollbackBaselineFiles.Add(AfterlifeChronicleState.StatePath);
        if (_fs.FileExists(writeLease, AfterlifeGlobalFlagState.StatePath))
            rollbackBaselineFiles.Add(AfterlifeGlobalFlagState.StatePath);
        if (_fs.FileExists(writeLease, SarefMainStoryState.StatePath))
            rollbackBaselineFiles.Add(SarefMainStoryState.StatePath);

        var snapshotFiles = new HashSet<string>(rollbackBaselineFiles, StringComparer.OrdinalIgnoreCase);
        if (rollbackSnapshot?.ValidationSnapshotFiles is { Count: > 0 })
        {
            foreach (var file in rollbackSnapshot.ValidationSnapshotFiles)
            {
                if (!string.IsNullOrWhiteSpace(file))
                    snapshotFiles.Add(file);
            }
        }
        snapshotFiles.UnionWith(PendingTurnSnapshotPathPresenceV1.LogicalPaths);

        foreach (var file in snapshotFiles.OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            await SnapshotFileOrRollbackBackupIfPresentAsync(
                writeLease,
                file,
                rollbackSnapshot,
                files,
                snapshotHashes);
        }
        var originalPathPresenceV1 =
            PendingTurnSnapshotPathPresenceV1.Create(files, snapshotHashes);

        var manifest = new PendingTurnSnapshotManifest
        {
            SessionId = request.SessionId,
            RequestId = request.RequestId,
            TurnNumber = request.TurnNumber,
            RequestTimestamp = request.Timestamp,
            PlayerAction = request.PlayerAction,
            PreGeneratedDices1d20 = request.PreGeneratedDices1d20,
            GachaBaseResult = request.GachaBaseResult == null
                ? null
                : JsonSerializer.SerializeToNode(request.GachaBaseResult, JsonOpts) as JsonObject,
            ProgressionControl = request.ProgressionControl,
            Files = files,
            SnapshotFileHashes = snapshotHashes,
            OriginalPathPresenceV1 = originalPathPresenceV1,
            ClientOwnedValidationHashes = clientOwnedValidationHashes,
            RollbackBackups = rollbackSnapshot != null
                ? new Dictionary<string, string>(rollbackSnapshot.BackupFiles, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            RollbackBaselineFiles = rollbackBaselineFiles
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            SourceLabel = sourceLabel,
            BrowserActionId = browserAction?.ActionId,
            BrowserSessionGeneration = browserAction?.Generation,
            BrowserOriginalMainCondition = browserAction == null ? null : _fs.CaptureBrowserOriginalCondition(writeLease, browserAction.Generation)
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
            relativePath => ReadRelativeFileBytesFromWorkspace(writeLease, relativePath));

        try
        {
            await _fs.WriteFileAtomicAsync(
                writeLease,
                PendingTurnSnapshotManifestPath,
                JsonSerializer.Serialize(manifest, JsonOpts));
            await _fs.WriteFileAtomicAsync(
                writeLease,
                PendingTurnSnapshotAuthority.AuthorityPath,
                authorityJson);
        }
        catch (Exception failure) when (failure is not CoordinatedStatePublicationUncertainException)
        {
            try
            {
                if (_fs.FileExists(writeLease, PendingTurnSnapshotManifestPath))
                    _fs.DeleteFile(writeLease, PendingTurnSnapshotManifestPath);
                if (_fs.FileExists(writeLease, PendingTurnSnapshotAuthority.AuthorityPath))
                    _fs.DeleteFile(writeLease, PendingTurnSnapshotAuthority.AuthorityPath);
            }
            catch (CoordinatedStatePublicationUncertainException uncertainty)
            {
                uncertainty.Data["PreparationPublicationFailure"] = failure;
                throw;
            }
            throw;
        }

        return files;
    }

    private async Task RegisterAfterlifeSpiritualConflictRollbackBackupIfInitializedAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        TurnRequest request,
        RollbackSnapshot? rollbackSnapshot,
        bool existedBeforeInitialization)
    {
        if (rollbackSnapshot == null ||
            existedBeforeInitialization ||
            !_fs.FileExists(writeLease, AfterlifeSpiritualConflictState.StatePath))
        {
            return;
        }

        var content = await _fs.ReadFileBytesAsync(writeLease, AfterlifeSpiritualConflictState.StatePath) ??
                      throw new IOException(
                          $"Canonical file '{AfterlifeSpiritualConflictState.StatePath}' disappeared during snapshot initialization.");

        var backupPath = AfterlifeSpiritualConflictState.StatePath + $".rollback.{request.RequestId}.initialized";
        await _fs.WriteFileAtomicBytesAsync(writeLease, backupPath, content);
        rollbackSnapshot.BaselineFiles.Add(AfterlifeSpiritualConflictState.StatePath);
        rollbackSnapshot.BackupFiles[AfterlifeSpiritualConflictState.StatePath] = backupPath;
        rollbackSnapshot.BackupHashes[AfterlifeSpiritualConflictState.StatePath] = ComputeSha256(content);
    }

    private async Task RegisterAfterlifeEntityProfileRollbackBackupIfInitializedAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        TurnRequest request,
        RollbackSnapshot? rollbackSnapshot,
        bool existedBeforeInitialization)
    {
        if (rollbackSnapshot == null ||
            existedBeforeInitialization ||
            !_fs.FileExists(writeLease, AfterlifeEntityProfileState.StatePath))
        {
            return;
        }

        var content = await _fs.ReadFileBytesAsync(writeLease, AfterlifeEntityProfileState.StatePath) ??
                      throw new IOException(
                          $"Canonical file '{AfterlifeEntityProfileState.StatePath}' disappeared during snapshot initialization.");

        var backupPath = AfterlifeEntityProfileState.StatePath + $".rollback.{request.RequestId}.initialized";
        await _fs.WriteFileAtomicBytesAsync(writeLease, backupPath, content);
        rollbackSnapshot.BaselineFiles.Add(AfterlifeEntityProfileState.StatePath);
        rollbackSnapshot.BackupFiles[AfterlifeEntityProfileState.StatePath] = backupPath;
        rollbackSnapshot.BackupHashes[AfterlifeEntityProfileState.StatePath] = ComputeSha256(content);
    }

    private async Task RegisterAfterlifeChronicleRollbackBackupIfInitializedAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        TurnRequest request,
        RollbackSnapshot? rollbackSnapshot,
        bool existedBeforeInitialization)
    {
        if (rollbackSnapshot == null ||
            existedBeforeInitialization ||
            !_fs.FileExists(writeLease, AfterlifeChronicleState.StatePath))
        {
            return;
        }

        var content = await _fs.ReadFileBytesAsync(writeLease, AfterlifeChronicleState.StatePath) ??
                      throw new IOException(
                          $"Canonical file '{AfterlifeChronicleState.StatePath}' disappeared during snapshot initialization.");

        var backupPath = AfterlifeChronicleState.StatePath + $".rollback.{request.RequestId}.initialized";
        await _fs.WriteFileAtomicBytesAsync(writeLease, backupPath, content);
        rollbackSnapshot.BaselineFiles.Add(AfterlifeChronicleState.StatePath);
        rollbackSnapshot.BackupFiles[AfterlifeChronicleState.StatePath] = backupPath;
        rollbackSnapshot.BackupHashes[AfterlifeChronicleState.StatePath] = ComputeSha256(content);
    }

    private async Task RegisterAfterlifeGlobalFlagRollbackBackupIfInitializedAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        TurnRequest request,
        RollbackSnapshot? rollbackSnapshot,
        bool existedBeforeInitialization)
    {
        if (rollbackSnapshot == null ||
            existedBeforeInitialization ||
            !_fs.FileExists(writeLease, AfterlifeGlobalFlagState.StatePath))
        {
            return;
        }

        var content = await _fs.ReadFileBytesAsync(writeLease, AfterlifeGlobalFlagState.StatePath) ??
                      throw new IOException(
                          $"Canonical file '{AfterlifeGlobalFlagState.StatePath}' disappeared during snapshot initialization.");

        var backupPath = AfterlifeGlobalFlagState.StatePath + $".rollback.{request.RequestId}.initialized";
        await _fs.WriteFileAtomicBytesAsync(writeLease, backupPath, content);
        rollbackSnapshot.BaselineFiles.Add(AfterlifeGlobalFlagState.StatePath);
        rollbackSnapshot.BackupFiles[AfterlifeGlobalFlagState.StatePath] = backupPath;
        rollbackSnapshot.BackupHashes[AfterlifeGlobalFlagState.StatePath] = ComputeSha256(content);
    }

    private async Task<Dictionary<string, string>> CaptureClientOwnedValidationHashesAsync(
        FileSystemManager.CanonicalWriteLease writeLease)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["game_state/history/chat_log.json"] = await ReadFileHashOrEmptyAsync(
                writeLease,
                "game_state/history/chat_log.json")
        };

        foreach (var storyPath in EnumerateStoryContinuityFiles(writeLease))
            result[storyPath] = await ReadFileHashOrEmptyAsync(writeLease, storyPath);

        return result;
    }

    private async Task<string> ReadFileHashOrEmptyAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        string relativePath)
    {
        var content = await _fs.ReadFileAsync(writeLease, relativePath);
        return content == null ? string.Empty : ComputeSha256(content);
    }

    private IEnumerable<string> EnumerateStoryContinuityFiles(
        FileSystemManager.CanonicalWriteLease writeLease) =>
        SelectOriginalStoryContinuityFiles(_fs.EnumerateFiles(writeLease, "*"));

    // Preserve every physical spelling before exact-path/alias validation. The
    // same raw selector serves producer hashes and cold completed receipts.
    internal static IEnumerable<string> SelectOriginalStoryContinuityFiles(IEnumerable<string> paths) =>
        paths.Where(path => path.StartsWith("stories/", StringComparison.OrdinalIgnoreCase) &&
            path.EndsWith(".jsonl", StringComparison.OrdinalIgnoreCase));

    private async Task<Dictionary<string, string>?> LoadCanonicalBaselineSnapshotAsync(
        int expectedTurnNumber,
        ValidatedPendingTurnSnapshotContext? activeSnapshotContext = null)
    {
        PendingTurnSnapshotAuthority.PendingTurnSnapshotAuthorityPayload? payload;
        if (activeSnapshotContext != null)
        {
            if (activeSnapshotContext.TurnNumber != expectedTurnNumber)
                return null;

            payload = activeSnapshotContext.Payload;
        }
        else
        {
            var manifest = await LoadPendingTurnSnapshotManifestAsync();
            if (manifest == null)
                return null;

            if (manifest.TurnNumber != expectedTurnNumber)
                return null;

            payload = await LoadValidatedCurrentPendingTurnSnapshotAuthorityPayloadAsync(manifest);
            if (payload == null)
                return null;
        }

        var canonicalFiles = new HashSet<string>(CanonicalStateNormalizer.CanonicalAccumulatedFiles, StringComparer.OrdinalIgnoreCase);
        canonicalFiles.UnionWith(
            WoundAcceptedTurnSnapshotContract.PublicationAgreementPaths);
        if (_fs.FileExists(AcceptedMechanicsPlan.WoundCommandPath) &&
            !PendingTurnSnapshotAuthority.HasValidatedRollbackSnapshotCoverage(
                payload,
                static authorityPayload => authorityPayload.Files,
                static authorityPayload => authorityPayload.SnapshotFileHashes,
                static authorityPayload => authorityPayload.RollbackBaselineFiles,
                WoundAcceptedTurnSnapshotContract.PublicationAgreementPaths,
                out _))
        {
            return null;
        }
        var baselineCanonicalFiles = payload.RollbackBaselineFiles
            .Where(path => !string.IsNullOrWhiteSpace(path) && canonicalFiles.Contains(path))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!PendingTurnSnapshotAuthority.HasValidatedSnapshotCoverage(
                payload,
                static authorityPayload => authorityPayload.Files,
                static authorityPayload => authorityPayload.SnapshotFileHashes,
                baselineCanonicalFiles,
                out _,
                static authorityPayload => authorityPayload.RollbackBaselineFiles,
                requireRollbackBaselineRegistration: true))
        {
            return null;
        }

        var snapshot = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var relativePath in baselineCanonicalFiles)
        {
            if (!payload.Files.TryGetValue(relativePath, out var snapshotPath) ||
                string.IsNullOrWhiteSpace(snapshotPath) ||
                !payload.SnapshotFileHashes.TryGetValue(relativePath, out var expectedSnapshotHash) ||
                string.IsNullOrWhiteSpace(expectedSnapshotHash))
            {
                return null;
            }

            var snapshotContent = await _fs.ReadFileBytesAsync(snapshotPath);
            if (snapshotContent == null ||
                !string.Equals(
                    PendingTurnSnapshotAuthority.ComputeSnapshotFileHash(payload, snapshotContent),
                    expectedSnapshotHash,
                    StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            snapshot[relativePath] = snapshotPath;
        }

        if (!await TryAddOptionalCanonicalBaselineSnapshotAsync(
                payload,
                snapshot,
                AfterlifeSpiritualConflictState.StatePath))
        {
            return null;
        }

        if (!await TryAddOptionalCanonicalBaselineSnapshotAsync(
                payload,
                snapshot,
                AfterlifeEntityProfileState.StatePath))
        {
            return null;
        }

        if (!await TryAddOptionalCanonicalBaselineSnapshotAsync(
                payload,
                snapshot,
                AfterlifeChronicleState.StatePath))
        {
            return null;
        }

        if (!await TryAddOptionalCanonicalBaselineSnapshotAsync(
                payload,
                snapshot,
                SarefMainStoryState.StatePath))
        {
            return null;
        }

        if (!await TryAddOptionalCanonicalBaselineSnapshotAsync(
                payload,
                snapshot,
                MortalBootstrapLocationScaffold.StatePath))
        {
            return null;
        }

        return snapshot.Count >= baselineCanonicalFiles.Count ? snapshot : null;
    }

    private async Task<bool> TryAddOptionalCanonicalBaselineSnapshotAsync(
        PendingTurnSnapshotAuthority.PendingTurnSnapshotAuthorityPayload payload,
        IDictionary<string, string> snapshot,
        string relativePath)
    {
        if (!payload.Files.TryGetValue(relativePath, out var snapshotPath))
            return true;

        if (string.IsNullOrWhiteSpace(snapshotPath) ||
            !PendingTurnSnapshotAuthority.IsSafeRelativePath(snapshotPath) ||
            !payload.SnapshotFileHashes.TryGetValue(relativePath, out var expectedSnapshotHash) ||
            string.IsNullOrWhiteSpace(expectedSnapshotHash))
        {
            return false;
        }

        var snapshotContent = await _fs.ReadFileBytesAsync(snapshotPath);
        if (snapshotContent == null ||
            !string.Equals(
                PendingTurnSnapshotAuthority.ComputeSnapshotFileHash(payload, snapshotContent),
                expectedSnapshotHash,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        snapshot[relativePath] = snapshotPath;
        return true;
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
        if (DoesPendingTurnRequestContextMatchManifest(manifest, turnContext))
            return true;

        var completionContext = await ReadPendingTurnSnapshotRequestContextAsync("ready/turn_complete.json");
        return DoesPendingTurnRequestContextMatchManifest(manifest, completionContext);
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

    private static string ComputePendingTurnManifestPayloadHash(PendingTurnSnapshotManifest manifest)
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

    private static string ComputeSha256(byte[] content) =>
        Convert.ToHexString(SHA256.HashData(content));

    private async Task SnapshotFileIfPresentAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        string relativePath,
        IDictionary<string, string> files,
        IDictionary<string, string> snapshotHashes)
    {
        var content = await _fs.ReadFileBytesAsync(writeLease, relativePath);
        if (content == null)
            return;

        var snapshotPath = $"{PendingTurnSnapshotDirectory}/{relativePath}";
        await _fs.WriteFileAtomicBytesAsync(writeLease, snapshotPath, content);
        files[relativePath] = snapshotPath;
        snapshotHashes[relativePath] = ComputeSha256(content);
        await InvokeSnapshotFileCapturedAsync(relativePath);
    }

    private async Task SnapshotFileOrRollbackBackupIfPresentAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        string relativePath,
        RollbackSnapshot? rollbackSnapshot,
        IDictionary<string, string> files,
        IDictionary<string, string> snapshotHashes)
    {
        byte[]? content = null;
        var useCurrentValidationState =
            rollbackSnapshot?.ValidationSnapshotFiles.Contains(relativePath) == true;
        if (!useCurrentValidationState &&
            rollbackSnapshot?.BackupFiles.TryGetValue(relativePath, out var backupPath) == true)
        {
            content = await _fs.ReadFileBytesAsync(writeLease, backupPath);
        }
        else
        {
            content = await _fs.ReadFileBytesAsync(writeLease, relativePath);
        }

        if (content == null)
            return;

        var snapshotPath = $"{PendingTurnSnapshotDirectory}/{relativePath}";
        await _fs.WriteFileAtomicBytesAsync(writeLease, snapshotPath, content);
        files[relativePath] = snapshotPath;
        snapshotHashes[relativePath] = ComputeSha256(content);
        await InvokeSnapshotFileCapturedAsync(relativePath);
    }

    private async Task CleanupPendingTurnSnapshotAsync(IEnumerable<string>? preservedRollbackPaths = null)
    {
        _fs.AllowBrowserOriginalCleanup();
        var writeLease = await _fs.AcquireCanonicalWriteLeaseAsync();
        CoordinatedStatePublicationUncertainException? uncertainty = null;
        try { CleanupPendingTurnSnapshot(writeLease, preservedRollbackPaths); }
        catch (CoordinatedStatePublicationUncertainException failure) { uncertainty = failure; throw; }
        finally { await CoordinatedStateWriteHelper.ReleaseOwnedLeaseAsync(_fs, writeLease, false, uncertainty); }
    }

    private void CleanupPendingTurnSnapshot(
        FileSystemManager.CanonicalWriteLease writeLease,
        IEnumerable<string>? preservedRollbackPaths = null)
    {
        var rawPreservedPaths = preservedRollbackPaths?.ToArray() ?? [];
        PendingTurnSnapshotAuthority.RequireExactSignedPaths(rawPreservedPaths);
        var preservedRollbackSet = new HashSet<string>(
                rawPreservedPaths
                    .Where(path => !string.IsNullOrWhiteSpace(path))
                    .Select(NormalizeArtifactRelativePath),
                StringComparer.OrdinalIgnoreCase);

        bool ShouldPreserveRollbackPath(string relativePath) =>
            preservedRollbackSet.Contains(NormalizeArtifactRelativePath(relativePath));

        _fs.DeleteDirectoryTree(writeLease, PendingTurnSnapshotDirectory);
        foreach (var relativePath in _fs.EnumerateFiles(writeLease, "*.rollback.*"))
        {
            if (ShouldPreserveRollbackPath(relativePath) ||
                IsExplorerLocalTurnRollbackArtifactPath(relativePath))
            {
                continue;
            }

            _fs.DeleteFile(writeLease, relativePath);
        }

        _fs.DeleteFile(writeLease, PendingTurnSnapshotManifestPath);
        _fs.DeleteFile(writeLease, PendingTurnSnapshotAuthority.AuthorityPath);
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
                ReadRelativeFileBytesFromWorkspace,
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
            TechnicalArtifacts = payload.Files.Keys.Where(path => ConsoleLocalTurnRollbackArtifacts.IsMarker(path) &&
                    payload.SnapshotFileHashes.ContainsKey(path)).ToHashSet(StringComparer.OrdinalIgnoreCase),
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
            BackupHashesAreExactBytes = string.Equals(
                payload.RollbackHashMode,
                PendingTurnSnapshotAuthority.ExactRollbackHashMode,
                StringComparison.Ordinal),
            BaselineFiles = new HashSet<string>(payload.RollbackBaselineFiles,
                StringComparer.OrdinalIgnoreCase)
        };

        return HasRollbackCapability(snapshot) ? snapshot : null;
    }

    private byte[]? ReadRelativeFileBytesFromWorkspace(string relativePath)
    {
        if (!PendingTurnSnapshotAuthority.IsSafeRelativePath(relativePath))
            return null;

        try
        {
            return _fs.ReadFileBytesAsync(relativePath).GetAwaiter().GetResult();
        }
        catch
        {
            return null;
        }
    }

    private byte[]? ReadRelativeFileBytesFromWorkspace(
        FileSystemManager.CanonicalWriteLease writeLease,
        string relativePath)
    {
        if (!PendingTurnSnapshotAuthority.IsSafeRelativePath(relativePath))
            return null;

        try
        {
            return _fs.ReadFileBytesAsync(writeLease, relativePath).GetAwaiter().GetResult();
        }
        catch
        {
            return null;
        }
    }

    private string? ReadRelativeTextFromWorkspace(string relativePath)
    {
        var bytes = ReadRelativeFileBytesFromWorkspace(relativePath);
        return bytes == null ? null : DecodeLegacyRollbackText(bytes);
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

    private static string NormalizeArtifactRelativePath(string relativePath) =>
        relativePath.Replace('\\', '/').TrimStart('/');

    internal static bool IsExplorerLocalTurnRollbackArtifactPath(string relativePath)
    {
        var normalized = NormalizeArtifactRelativePath(relativePath);
        return ConsoleLocalTurnRollbackArtifacts.IsArtifact(normalized) || normalized.StartsWith(
            "game_state/control/explorer_local_turn_rollback/",
            StringComparison.OrdinalIgnoreCase);
    }

    private async Task NormalizePendingRepairArtifactsAsync()
    {
        await SessionOperationContext.RunParticipatingCurrentSessionAsync(_fs, NormalizePendingRepairArtifactsCoreAsync);
    }

    private async Task NormalizePendingRepairArtifactsCoreAsync()
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

        if (repairRequestExists && await TryPromoteValidationRepairArtifactStallToTerminalErrorAsync(snapshotContext))
            return;

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

    private async Task<bool> TryPromoteValidationRepairArtifactStallToTerminalErrorAsync(
        ValidatedPendingTurnSnapshotContext snapshotContext,
        string? expectedSessionGeneration = null)
    {
        var writeLease = await _fs.AcquireCanonicalWriteLeaseAsync();
        CoordinatedStatePublicationUncertainException? owningPublicationUncertainty = null;
        try
        {
            if (!string.IsNullOrWhiteSpace(expectedSessionGeneration))
                ThrowIfRepairSessionReplaced(writeLease, expectedSessionGeneration);

            if (!_fs.FileExists(writeLease, ValidationRepairArtifactStallReportPath) ||
                _fs.FileExists(writeLease, "ready/turn_error.json") ||
                !_fs.FileExists(writeLease, ValidationRepairRequestPath))
            {
                return false;
            }

            var requestJson = await _fs.ReadFileAsync(writeLease, ValidationRepairRequestPath);
            if (string.IsNullOrWhiteSpace(requestJson))
                return false;

            ValidationRepairRequest? repairRequest;
            try
            {
                repairRequest = JsonSerializer.Deserialize<ValidationRepairRequest>(requestJson, JsonOpts);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Найден validation repair artifact stall report, но validation_repair_request.json не читается.");
                return false;
            }

            if (repairRequest == null ||
                !string.Equals(repairRequest.SessionId, snapshotContext.SessionId, StringComparison.Ordinal) ||
                !string.Equals(repairRequest.RequestId, snapshotContext.RequestId, StringComparison.Ordinal) ||
                repairRequest.TurnNumber != snapshotContext.TurnNumber)
            {
                return false;
            }

            if (_fs.FileExists(writeLease, "ready/turn_complete.json"))
            {
                var completeMetadata = ParseReadySignalMetadata(
                    await _fs.ReadFileAsync(writeLease, "ready/turn_complete.json"),
                    "ready/turn_complete.json");
                if (completeMetadata == null ||
                    !string.Equals(completeMetadata.SessionId, snapshotContext.SessionId, StringComparison.Ordinal) ||
                    !string.Equals(completeMetadata.RequestId, snapshotContext.RequestId, StringComparison.Ordinal) ||
                    completeMetadata.TurnNumber != snapshotContext.TurnNumber)
                {
                    return false;
                }
            }

            JsonNode? stallReportNode = null;
            var stallReportJson = await _fs.ReadFileAsync(writeLease, ValidationRepairArtifactStallReportPath);
            if (!string.IsNullOrWhiteSpace(stallReportJson))
            {
                try
                {
                    stallReportNode = JsonNode.Parse(stallReportJson);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Найден validation repair artifact stall report, но report JSON не читается.");
                }
            }

            var signal = new JsonObject
            {
                ["sessionId"] = repairRequest.SessionId,
                ["requestId"] = repairRequest.RequestId,
                ["turnNumber"] = repairRequest.TurnNumber,
                ["timestamp"] = DateTimeOffset.UtcNow.ToString("O"),
                ["status"] = "error",
                ["harnessSource"] = "gm_validation_repair_artifact_stall",
                ["error"] = "Validation repair stalled without target artifact progress; GM bridge was stopped by harness cleanup."
            };

            if (stallReportNode != null)
                signal["validationRepairArtifactStall"] = stallReportNode;

            await _fs.WriteFileAtomicAsync(writeLease, "ready/turn_error.json", signal.ToJsonString(JsonOpts));
            _fs.DeleteFile(writeLease, "ready/turn_complete.json");
            _fs.DeleteFile(writeLease, ValidationRepairReadyPath);

            _logger.LogWarning(
                "Validation repair artifact stall promoted to terminal error for pending turn(session={Session}, request={Request}, turn={Turn}).",
                repairRequest.SessionId,
                repairRequest.RequestId,
                repairRequest.TurnNumber);
            return true;
        }
        catch (CoordinatedStatePublicationUncertainException failure) { owningPublicationUncertainty = failure; throw; }
        finally { await CoordinatedStateWriteHelper.ReleaseOwnedLeaseAsync(_fs, writeLease, false, owningPublicationUncertainty); }
    }

    private async Task NormalizePendingTerminalProtocolFailureArtifactsAsync()
    {
        await SessionOperationContext.RunParticipatingCurrentSessionAsync(_fs, NormalizePendingTerminalProtocolFailureArtifactsCoreAsync);
    }

    private async Task NormalizePendingTerminalProtocolFailureArtifactsCoreAsync()
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

    /// <summary>
    /// Normalizes startup UI and control artifacts while preserving usable pending turns and archiving eligible inactive snapshot evidence.
    /// </summary>
    /// <returns>
    /// A task completing after startup normalization and any diagnostic evidence retirement attempt.
    /// </returns>
    private async Task NormalizeRuntimeUiArtifactsAsync()
    {
        await SessionOperationContext.RunParticipatingCurrentSessionAsync(_fs, NormalizeRuntimeUiArtifactsCoreAsync);
    }

    private async Task NormalizeRuntimeUiArtifactsCoreAsync()
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
                await SourceOfLightCapstoneState.EnsureHealthyAsync(_fs, _stateManager.CurrentState.CurrentRealm);
                await SarefMainStoryState.EnsureWingsInfiltrationHealthyAsync(_fs, _stateManager.CurrentState.CurrentRealm);
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

        await RepairStalePreparedShiningPackageAfterMortalBootstrapAsync(pendingSnapshot, hasReadySignals);
        await ArchiveInactivePendingSnapshotEvidenceAsync();
    }

    private bool HasTerminalReadySignal() =>
        _fs.FileExists("ready/turn_complete.json") ||
        _fs.FileExists("ready/turn_error.json");

    private async Task CleanupAcceptedTurnTerminalArtifactsAsync()
    {
        var hasIncarnationTrigger = _fs.FileExists("game_state/control/incarnation_trigger.json");
        _fs.DeleteFile("ready/turn_complete.json");
        _fs.DeleteFile("ready/turn_error.json");
        if (!hasIncarnationTrigger)
        {
            _fs.DeleteFile("input/turn_request.json");
            await CleanupPendingTurnSnapshotAsync();
        }
        await new TrainingService(_fs, NullLogger<TrainingService>.Instance)
            .CleanupSatisfiedMortalSkillEvolutionRequestsAsync();
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
        await SourceOfLightCapstoneState.EnsureHealthyAsync(_fs, currentRealm);
        await SarefMainStoryState.EnsureWingsInfiltrationHealthyAsync(_fs, currentRealm);
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
        return ParseReadySignalMetadata(await _fs.ReadFileAsync(relativePath), relativePath);
    }

    private ReadySignalMetadata? ParseReadySignalMetadata(string? json,string relativePath)
    {
        try { return ParseReadySignalMetadataCore(json); }
        catch(Exception ex) { _logger.LogDebug(ex,"Не удалось прочитать ready signal {RelativePath}.",relativePath);return null; }
    }
    private static ReadySignalMetadata? ParseReadySignalMetadataCore(string? json)
{
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
                    HarnessSource = doc.RootElement.TryGetProperty("harnessSource", out var harnessSource) && harnessSource.ValueKind == JsonValueKind.String
                        ? harnessSource.GetString()
                        : null,
                    HasFilesModified = hasFilesModified,
                    FilesModifiedValid = filesModifiedValid
                };
            }
        }
        catch (JsonException) { return null; }

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

            AnsiConsole.MarkupLine("[yellow]⚠ Ход завершился неустойчиво. Мир ожидает нового согласованного продолжения.[/]");
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

            AnsiConsole.MarkupLine("[yellow]⚠ Ход не удалось связать с текущим течением событий. Мир вернулся к безопасному ожиданию.[/]");
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

        AnsiConsole.MarkupLine("[yellow]⚠ Пришедшее продолжение больше не относится к текущему ходу и было оставлено позади.[/]");
        return true;
    }

    /// <summary>
    /// Removes a rejected turn's remaining input only while its current identity still matches the validated original.
    /// </summary>
    /// <param name="snapshotContext">
    /// Authenticated original context retained by the caller after successful rollback; a missing context preserves input.
    /// </param>
    /// <returns>
    /// A task completing after the correlated input is deleted or an absent, malformed or foreign request is preserved.
    /// </returns>
    private async Task CleanupCorrelatedRejectedTurnRequestAsync(ValidatedPendingTurnSnapshotContext? snapshotContext)
    {
        if (snapshotContext is null || string.IsNullOrWhiteSpace(snapshotContext.SessionId) ||
            string.IsNullOrWhiteSpace(snapshotContext.RequestId) || snapshotContext.TurnNumber <= 0)
            return;

        var lease = await _fs.AcquireCanonicalWriteLeaseAsync();
        CoordinatedStatePublicationUncertainException? owningPublicationUncertainty = null;
        try
        {
            const string path = "input/turn_request.json";
            var json = await _fs.ReadFileAsync(lease, path);
            if (string.IsNullOrWhiteSpace(json))
                return;

            try
            {
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object ||
                    root.EnumerateObject().GroupBy(property => property.Name, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1) ||
                    !root.TryGetProperty("sessionId", out var session) || session.ValueKind != JsonValueKind.String ||
                    !string.Equals(session.GetString(), snapshotContext.SessionId, StringComparison.Ordinal) ||
                    !root.TryGetProperty("requestId", out var request) || request.ValueKind != JsonValueKind.String ||
                    !string.Equals(request.GetString(), snapshotContext.RequestId, StringComparison.Ordinal) ||
                    !root.TryGetProperty("turnNumber", out var turn) || turn.ValueKind != JsonValueKind.Number ||
                    !turn.TryGetInt32(out var number) || number != snapshotContext.TurnNumber)
                    return;

                _fs.DeleteFile(lease, path);
            }
            catch (JsonException)
            {
                // An unreadable request cannot prove it belongs to the rejected turn.
            }
        }
        catch (CoordinatedStatePublicationUncertainException failure) { owningPublicationUncertainty = failure; throw; }
        finally { await CoordinatedStateWriteHelper.ReleaseOwnedLeaseAsync(_fs, lease, false, owningPublicationUncertainty); }
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


    // Producer admission must see raw physical spellings before the shared repair/
    // restoration enumerator folds keys or filters optional fixed paths by FileExists.
    private void RequireExactEngineSnapshotInventory(FileSystemManager.CanonicalWriteLease writeLease,RollbackSnapshot? rollbackSnapshot=null) =>
        RequireExactEngineSnapshotInventoryCore(_fs.EnumerateFiles(writeLease,"*").ToArray(),rollbackSnapshot);
    internal static void RequireExactBrowserPhysicalInventory(string[] physicalInventory) =>
        RequireExactEngineSnapshotInventoryCore(physicalInventory,null);
    private static void RequireExactEngineSnapshotInventoryCore(string[] physicalInventory,RollbackSnapshot? rollbackSnapshot)
{
        var fixedPaths = PendingTurnSnapshotPathPresenceV1.LogicalPaths
            .Concat(GuardianPolicySnapshotRequestFiles)
            .Concat(WoundAcceptedTurnSnapshotContract.OutputPaths)
            .Append(QteSceneService.QteOfferPath)
            .Concat(new[]
            {
                ValidationRepairReadyPath, ValidationRepairRequestPath,
                ValidationDiagnosticFailureReportPath, ValidationRepairArtifactStallReportPath,
                RealmSegregationAutoRollbackService.ReportPath, GmWorkerAuditLog.AuditLogPath,
                "game_state/control/gm_trajectory_ledger.jsonl",
                "game_state/control/gm_artifact_write_stall_report.json",
                "game_state/control/gm_output_without_terminal_report.json",
                "game_state/control/gm_daemon_fatal_error.json",
                "game_state/control/gm_timeout_bridge_cleanup.json",
                "game_state/control/gm_live_test_notes.jsonl",
                "game_state/control/terminal_protocol_failure_request.json",
                "game_state/control/gm_bridge_status.json", "game_state/history/chat_log.json",
                PendingTurnSnapshotManifestPath, PendingTurnSnapshotAuthority.AuthorityPath,
                AfterlifeSpiritualConflictState.StatePath, AfterlifeEntityProfileState.StatePath,
                AfterlifeChronicleState.StatePath, AfterlifeGlobalFlagState.StatePath,
                SourceOfLightCapstoneState.PendingRequestPath,
                SarefMainStoryState.PendingWingsInfiltrationPath, SarefMainStoryState.StatePath
            }).ToArray();
        var rawPaths = physicalInventory
            .Where(path => path.StartsWith("game_state/", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("lore/", StringComparison.OrdinalIgnoreCase) ||
                fixedPaths.Contains(path, StringComparer.OrdinalIgnoreCase));
        PendingTurnSnapshotAuthority.RequireExactSignedPaths(rawPaths
            .Concat(SelectOriginalStoryContinuityFiles(physicalInventory)).Concat(fixedPaths)
            .Concat(rollbackSnapshot?.BaselineFiles ?? [])
            .Concat(rollbackSnapshot?.ValidationSnapshotFiles ?? [])
            .Concat(rollbackSnapshot?.BackupFiles.Keys.AsEnumerable() ?? [])
            .Concat(rollbackSnapshot?.BackupFiles.Values.AsEnumerable() ?? [])
            .Concat(rollbackSnapshot?.BackupHashes.Keys.AsEnumerable() ?? []));
    }

    private IEnumerable<string> EnumerateRollbackTrackedFiles(FileSystemManager.CanonicalWriteLease writeLease) =>
        EnumerateOriginalBrowserRollbackTrackedFiles(_fs.EnumerateFiles(writeLease,"*").ToArray());
    internal static IEnumerable<string> EnumerateOriginalBrowserRollbackTrackedFiles(string[] physicalInventory)
{
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var relative in physicalInventory)
        {
            if (ConsoleLocalTurnRollbackArtifacts.IsArtifact(relative)) continue;
            if (relative.StartsWith("game_state/", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(relative, ValidationRepairReadyPath, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(relative, ValidationRepairRequestPath, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(relative, ValidationDiagnosticFailureReportPath, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(relative, ValidationRepairArtifactStallReportPath, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(relative, RealmSegregationAutoRollbackService.ReportPath, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(relative, GmWorkerAuditLog.AuditLogPath, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(relative, "game_state/control/gm_trajectory_ledger.jsonl", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(relative, "game_state/control/gm_artifact_write_stall_report.json", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(relative, "game_state/control/gm_output_without_terminal_report.json", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(relative, "game_state/control/gm_daemon_fatal_error.json", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(relative, "game_state/control/gm_timeout_bridge_cleanup.json", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(relative, "game_state/control/gm_live_test_notes.jsonl", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(relative, "game_state/control/terminal_protocol_failure_request.json", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(relative, "game_state/control/gm_bridge_status.json", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(relative, "game_state/history/chat_log.json", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(relative, PendingTurnSnapshotManifestPath, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(relative, PendingTurnSnapshotAuthority.AuthorityPath, StringComparison.OrdinalIgnoreCase) ||
                    relative.StartsWith($"{PendingTurnSnapshotDirectory}/", StringComparison.OrdinalIgnoreCase) ||
                    relative.Contains(".rollback.", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                files.Add(relative);
                continue;
            }

            if (relative.StartsWith("lore/", StringComparison.OrdinalIgnoreCase))
            {
                if (relative.Contains(".rollback.", StringComparison.OrdinalIgnoreCase))
                    continue;

                files.Add(relative);
            }
        }

        foreach (var outputFile in WoundAcceptedTurnSnapshotContract.OutputPaths
                     .Append(QteSceneService.QteOfferPath))
        {
            if (physicalInventory.Contains(outputFile,StringComparer.OrdinalIgnoreCase))
                files.Add(outputFile);
        }

        return files;
    }

    private async Task<RollbackSnapshot> CreatePreTurnBackup(string backupId)
    {
        var writeLease = await _fs.AcquireCanonicalWriteLeaseAsync();
        CoordinatedStatePublicationUncertainException? uncertainty = null;
        try { return await CreatePreTurnBackup(writeLease, backupId); }
        catch (CoordinatedStatePublicationUncertainException failure) { uncertainty = failure; throw; }
        finally { await CoordinatedStateWriteHelper.ReleaseOwnedLeaseAsync(_fs, writeLease, false, uncertainty); }
    }

    private async Task<RollbackSnapshot> CreatePreTurnBackup(
        FileSystemManager.CanonicalWriteLease writeLease,
        string backupId)
    {
        RequireExactEngineSnapshotInventory(writeLease);
        var snapshot = new RollbackSnapshot();
        var createdBackupPaths = new List<string>();
        var trackedFiles = EnumerateRollbackTrackedFiles(writeLease).ToArray();
        try
        {
            foreach (var file in trackedFiles)
            {
                var content = await _fs.ReadFileBytesAsync(writeLease, file) ??
                              throw new IOException(
                                  $"Canonical file '{file}' disappeared while its rollback baseline was captured.");
                var backupPath = file + $".rollback.{backupId}";
                await _fs.WriteFileAtomicBytesAsync(writeLease, backupPath, content);
                createdBackupPaths.Add(backupPath);
                snapshot.BaselineFiles.Add(file);
                snapshot.BackupFiles[file] = backupPath;
                snapshot.BackupHashes[file] = ComputeSha256(content);
            }

            await OverlayPersistentExplorerLocalTurnRollbackArtifactsAsync(writeLease, snapshot);
            return snapshot;
        }
        catch (Exception captureException) when (captureException is not CoordinatedStatePublicationUncertainException)
        {
            var cleanupErrors = new List<Exception>();
            foreach (var backupPath in createdBackupPaths)
            {
                try
                {
                    if (_fs.FileExists(writeLease, backupPath))
                        _fs.DeleteFile(writeLease, backupPath);
                }
                catch (CoordinatedStatePublicationUncertainException uncertainty)
                {
                    uncertainty.Data["PreparationCaptureFailure"] = captureException;
                    throw;
                }
                catch (Exception cleanupException)
                {
                    cleanupErrors.Add(new IOException(
                        $"Failed to clean partial rollback evidence '{backupPath}'.",
                        cleanupException));
                }
            }

            if (cleanupErrors.Count > 0)
                throw new AggregateException(
                    "Rollback snapshot capture failed and partial evidence cleanup was incomplete.",
                    [captureException, .. cleanupErrors]);
            throw;
        }
    }

    private async Task OverlayPersistentExplorerLocalTurnRollbackArtifactsAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        RollbackSnapshot snapshot)
    {
        var stagedBackups = ExplorerLocalTurnRollbackArtifacts.DiscoverBackups(
            _fs,
            writeLease,
            snapshot.BaselineFiles);
        foreach (var stagedBackup in stagedBackups)
        {
            var backupContent = await _fs.ReadFileBytesAsync(writeLease, stagedBackup.BackupPath);
            if (backupContent == null)
                throw new FileNotFoundException(
                    $"Staged rollback evidence for '{stagedBackup.TrackedFile}' disappeared during capture.",
                    stagedBackup.BackupPath);

            if (snapshot.BackupFiles.TryGetValue(stagedBackup.TrackedFile, out var staleBackupPath) &&
                !string.Equals(staleBackupPath, stagedBackup.BackupPath, StringComparison.OrdinalIgnoreCase) &&
                _fs.FileExists(writeLease, staleBackupPath))
            {
                _fs.DeleteFile(writeLease, staleBackupPath);
            }

            snapshot.BaselineFiles.Add(stagedBackup.TrackedFile);
            snapshot.BackupFiles[stagedBackup.TrackedFile] = stagedBackup.BackupPath;
            snapshot.BackupHashes[stagedBackup.TrackedFile] = ComputeSha256(backupContent);
        }
    }

    /// <summary>
    /// Restores game state files from pre-turn backups (escape-rollback).
    /// </summary>
    private async Task RestorePreTurnBackup(RollbackSnapshot snapshot)
    {
        var writeLease = await _fs.AcquireCanonicalWriteLeaseAsync();
        CoordinatedStatePublicationUncertainException? uncertainty = null;
        try { await RestorePreTurnBackupAsync(writeLease, snapshot); }
        catch (CoordinatedStatePublicationUncertainException failure) { uncertainty = failure; throw; }
        finally { await CoordinatedStateWriteHelper.ReleaseOwnedLeaseAsync(_fs, writeLease, false, uncertainty); }
        await RefreshRuntimeStateAfterExactRollbackAsync();
    }

    private async Task RestorePreTurnBackupForSessionAsync(
        RollbackSnapshot snapshot,
        string expectedSessionGeneration)
    {
        var writeLease = await _fs.AcquireCanonicalWriteLeaseAsync();
        CoordinatedStatePublicationUncertainException? uncertainty = null;
        var restored = false;
        try
        {
            ThrowIfRepairSessionReplaced(writeLease, expectedSessionGeneration);
            await RestorePreTurnBackupAsync(writeLease, snapshot);
            restored = true;
            CleanupBackup(writeLease, snapshot);
        }
        catch (CoordinatedStatePublicationUncertainException failure)
        {
            uncertainty = failure;
            failure.Data["PreTurnRollbackRestoredBeforeCleanup"] = restored;
            throw;
        }
        finally { await CoordinatedStateWriteHelper.ReleaseOwnedLeaseAsync(_fs, writeLease, false, uncertainty); }

        await RefreshRuntimeStateAfterExactRollbackAsync();
    }

    private async Task RestorePreTurnBaselineForRepairSessionAsync(
        RollbackSnapshot snapshot,
        string expectedSessionGeneration,
        byte[]? pendingResolutionRepairCheckpoint = null)
    {
        {
            var writeLease = await _fs.AcquireCanonicalWriteLeaseAsync();
            CoordinatedStatePublicationUncertainException? owningPublicationUncertainty = null;
            try
            {
                ThrowIfRepairSessionReplaced(writeLease, expectedSessionGeneration);
                await RestorePreTurnBackupAsync(writeLease, snapshot);
                if (pendingResolutionRepairCheckpoint != null)
                {
                    await _fs.WriteFileAtomicBytesAsync(
                        writeLease,
                        ResourcePendingResolutionState.PendingPath,
                        pendingResolutionRepairCheckpoint);
                }
            }
            catch (CoordinatedStatePublicationUncertainException failure) { owningPublicationUncertainty = failure; throw; }
            finally { await CoordinatedStateWriteHelper.ReleaseOwnedLeaseAsync(_fs, writeLease, false, owningPublicationUncertainty); }
        }

        await RefreshRuntimeStateAfterExactRollbackAsync();
    }

    private Task RefreshRuntimeStateAfterExactRollbackAsync() =>
        _stateManager.RefreshGameStateAfterExactRollbackAsync();

    private async Task<IReadOnlyList<RepairResubmissionPathObligation>> CaptureChangedRollbackTrackedPathsForRepairSessionAsync(
        RollbackSnapshot snapshot,
        string expectedSessionGeneration)
    {
        await using var writeLease = await _fs.AcquireCanonicalWriteLeaseAsync();
        ThrowIfRepairSessionReplaced(writeLease, expectedSessionGeneration);
        var currentPaths = EnumerateRollbackTrackedFiles(writeLease)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var candidates = currentPaths
            .Concat(snapshot.BaselineFiles)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);
        var changed = new List<RepairResubmissionPathObligation>();
        foreach (var path in candidates)
        {
            if (IsClientOwnedRepairResubmissionPath(path))
                continue;

            var comparison = await CompareRollbackTrackedPathToBaselineAsync(
                writeLease,
                snapshot,
                currentPaths,
                path);
            if (comparison != RollbackTrackedPathComparison.Unchanged)
            {
                changed.Add(new RepairResubmissionPathObligation(
                    path,
                    currentPaths.Contains(path) && _fs.FileExists(writeLease, path)));
            }
        }

        return changed;
    }

    /// <summary>
    /// Excludes client mechanics evidence from GM repair resubmission obligations.
    /// </summary>
    /// <param name="path">
    /// Repository-relative rollback path to classify.
    /// </param>
    /// <returns>
    /// <see langword="true"/> for a client-owned path that the GM must not resubmit;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    private static bool IsClientOwnedRepairResubmissionPath(string path) =>
        string.Equals(
            path,
            SpiritualWoundCaptureCheckpointState.StatePath,
            StringComparison.OrdinalIgnoreCase) ||
        string.Equals(
            path,
            SpiritualWoundDecisionPendingState.StatePath,
            StringComparison.OrdinalIgnoreCase) ||
        string.Equals(
            path,
            SpiritualWoundOpportunityReceiptState.StatePath,
            StringComparison.OrdinalIgnoreCase) ||
        string.Equals(
            path,
            SystemModService.ManifestPath,
            StringComparison.OrdinalIgnoreCase) ||
        string.Equals(
            path,
            ProgressionScheduleService.SchedulePath,
            StringComparison.OrdinalIgnoreCase) ||
        string.Equals(
            path,
            ResourceMaterializationContract.DefinitionsPath,
            StringComparison.OrdinalIgnoreCase) ||
        string.Equals(
            path,
            ResourceMaterializationContract.StatePath,
            StringComparison.OrdinalIgnoreCase) ||
        string.Equals(
            path,
            ResourceMaterializationContract.HistoryPath,
            StringComparison.OrdinalIgnoreCase) ||
        string.Equals(
            path,
            CanonicalResourceOwnerAuthorityComposer.AuthorityPath,
            StringComparison.OrdinalIgnoreCase) ||
        string.Equals(
            path,
            ResourcePendingResolutionState.PendingPath,
            StringComparison.OrdinalIgnoreCase) ||
        string.Equals(
            path,
            EffectAcceptedTurnPlan.IdentityIndexPath,
            StringComparison.OrdinalIgnoreCase);

    private async Task<bool> AreRollbackTrackedPathsResubmittedForRepairSessionAsync(
        RollbackSnapshot snapshot,
        IReadOnlyList<RepairResubmissionPathObligation> requiredPaths,
        string expectedSessionGeneration)
    {
        if (requiredPaths.Count == 0)
            return false;

        await using var writeLease = await _fs.AcquireCanonicalWriteLeaseAsync();
        ThrowIfRepairSessionReplaced(writeLease, expectedSessionGeneration);
        var currentPaths = EnumerateRollbackTrackedFiles(writeLease)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var requiredPath in requiredPaths)
        {
            var currentlyExists = currentPaths.Contains(requiredPath.Path) &&
                                  _fs.FileExists(writeLease, requiredPath.Path);
            if (currentlyExists != requiredPath.RejectedExists)
                return false;
            if (await CompareRollbackTrackedPathToBaselineAsync(
                    writeLease,
                    snapshot,
                    currentPaths,
                    requiredPath.Path) != RollbackTrackedPathComparison.Changed)
            {
                return false;
            }
        }

        return true;
    }

    private enum RollbackTrackedPathComparison
    {
        Unchanged,
        Changed,
        Unobservable
    }

    private async Task<RollbackTrackedPathComparison> CompareRollbackTrackedPathToBaselineAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        RollbackSnapshot snapshot,
        IReadOnlySet<string> currentPaths,
        string path)
    {
        var currentlyExists = currentPaths.Contains(path) && _fs.FileExists(writeLease, path);
        var existedAtBaseline = snapshot.BaselineFiles.Contains(path);
        if (currentlyExists != existedAtBaseline)
            return RollbackTrackedPathComparison.Changed;
        if (!currentlyExists)
            return RollbackTrackedPathComparison.Unchanged;

        var currentContent = await _fs.ReadFileBytesAsync(writeLease, path);
        if (currentContent == null ||
            !snapshot.BackupFiles.TryGetValue(path, out var backupPath))
        {
            return RollbackTrackedPathComparison.Unobservable;
        }

        var storedBaselineContent = await _fs.ReadFileBytesAsync(writeLease, backupPath);
        if (storedBaselineContent == null)
            return RollbackTrackedPathComparison.Unobservable;
        var baselineContent = snapshot.BackupHashesAreExactBytes
            ? storedBaselineContent
            : Encoding.UTF8.GetBytes(DecodeLegacyRollbackText(storedBaselineContent));

        if (currentContent.AsSpan().SequenceEqual(baselineContent))
            return RollbackTrackedPathComparison.Unchanged;
        if (TryParseSemanticJson(currentContent, out var currentJson) &&
            TryParseSemanticJson(baselineContent, out var baselineJson))
        {
            try
            {
                if (JsonNode.DeepEquals(currentJson, baselineJson))
                    return RollbackTrackedPathComparison.Unchanged;
            }
            catch (Exception exception) when (
                exception is ArgumentException or InvalidOperationException)
            {
                return RollbackTrackedPathComparison.Unobservable;
            }
        }

        return RollbackTrackedPathComparison.Changed;
    }

    private static bool TryParseSemanticJson(byte[] content, out JsonNode? node)
    {
        try
        {
            node = JsonNode.Parse(DecodeLegacyRollbackText(content));
            return true;
        }
        catch (JsonException)
        {
            node = null;
            return false;
        }
    }

    private async Task RestorePreTurnBackupAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        RollbackSnapshot snapshot)
    {
        var failures = new List<Exception>();
        var validatedBackups = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var (original, backup) in snapshot.BackupFiles)
        {
            try
            {
                var content = await _fs.ReadFileBytesAsync(writeLease, backup);
                if (content == null)
                    throw new FileNotFoundException("Rollback evidence is missing.", backup);
                if (!snapshot.BackupHashes.TryGetValue(original, out var expectedHash) ||
                    string.IsNullOrWhiteSpace(expectedHash))
                {
                    throw new InvalidDataException($"Rollback evidence hash is missing for '{original}'.");
                }

                var actualHash = snapshot.BackupHashesAreExactBytes
                    ? ComputeSha256(content)
                    : ComputeSha256(DecodeLegacyRollbackText(content));
                if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException($"Rollback evidence hash mismatch for '{original}'.");
                validatedBackups[original] = content;
            }
            catch (Exception ex)
            {
                failures.Add(new InvalidDataException(
                    $"Rollback evidence for '{original}' could not be validated.",
                    ex));
            }
        }

        if (failures.Count > 0)
        {
            throw new InvalidDataException(
                "Pre-turn rollback evidence could not be validated. Rollback evidence was retained.",
                new AggregateException(failures));
        }

        foreach (var trackedFile in EnumerateRollbackTrackedFiles(writeLease))
        {
            if (snapshot.BaselineFiles.Contains(trackedFile))
                continue;

            try
            {
                if (_fs.FileExists(writeLease, trackedFile))
                    _fs.DeleteFile(writeLease, trackedFile);
            }
            catch (Exception ex) when (ex is not CoordinatedStatePublicationUncertainException)
            {
                failures.Add(new IOException(
                    $"Failed to delete new canonical file '{trackedFile}' during rollback.",
                    ex));
            }
        }

        foreach (var (original, content) in validatedBackups)
        {
            try
            {
                await _fs.WriteFileAtomicBytesAsync(writeLease, original, content);
            }
            catch (Exception ex) when (ex is not CoordinatedStatePublicationUncertainException)
            {
                failures.Add(new IOException(
                    $"Failed to restore canonical file '{original}' from rollback evidence.",
                    ex));
            }
        }

        if (failures.Count > 0)
        {
            throw new InvalidDataException(
                "Pre-turn rollback could not be completed. Rollback evidence was retained.",
                new AggregateException(failures));
        }
    }

    private static string DecodeLegacyRollbackText(byte[] content)
    {
        using var stream = new MemoryStream(content, writable: false);
        using var reader = new StreamReader(
            stream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// Cleans up temporary rollback backup files.
    /// </summary>
    private void CleanupBackup(RollbackSnapshot snapshot)
    {
        _fs.AllowBrowserOriginalCleanup();
        var writeLease = _fs.AcquireCanonicalWriteLeaseAsync().GetAwaiter().GetResult();
        CoordinatedStatePublicationUncertainException? uncertainty = null;
        try { CleanupBackup(writeLease, snapshot); }
        catch (CoordinatedStatePublicationUncertainException failure) { uncertainty = failure; throw; }
        finally { CoordinatedStateWriteHelper.ReleaseOwnedLeaseAsync(_fs, writeLease, false, uncertainty).AsTask().GetAwaiter().GetResult(); }
    }

    private void CleanupBackup(
        FileSystemManager.CanonicalWriteLease writeLease,
        RollbackSnapshot snapshot)
    {
        var cleanupFailed = false;
        foreach (var backup in snapshot.BackupFiles.Values)
        {
            try { _fs.DeleteFile(writeLease, backup); }
            catch (Exception ex) when (ex is not CoordinatedStatePublicationUncertainException and not SessionReplacedException)
            {
                cleanupFailed = true;
                try { _logger.LogDebug(ex, "Не удалось удалить rollback backup {BackupPath}.", backup); }
                catch { /* Best-effort diagnostics cannot change established cleanup refusal. */ }
            }
        }
        if (!cleanupFailed)
            foreach (var artifact in snapshot.TechnicalArtifacts)
                _fs.DeleteFile(writeLease, artifact);
        ExplorerLocalTurnRollbackArtifacts.DeleteEmptyDirectories(_fs, writeLease);
    }

}
