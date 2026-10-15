using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Services;
using BookOfEternityClient.Models;
using BookOfEternityClient.UI;

namespace BookOfEternityClient.Core;

public partial class GameEngine
{
    private enum MortalJointCleanupDisposition { CommittedAndVerified, HeldBlocked }
    private sealed record MortalJointCleanupResult(MortalJointCleanupDisposition Disposition, string? Failure, GameResponse? Response = null);

    // Eligible cleanup only. A serialized marker, original path list, ready or
    // story row alone grants no deletion authority. All owning proofs are freshly
    // reread on this participating lease before capturing the exact fixed cohort.
    private async Task<MortalJointCleanupResult> CleanupEligibleMortalJointTerminalAsync(
        FileSystemManager.CanonicalWriteLease lease, ValidatedPendingTurnSnapshotContext frame)
    {
        try
        {
            _fs.ResolveBackupPublicationRecovery(lease);
            var applied = await MortalQteDecisionPublication.ReadJointProgressionPhaseAsync(_fs, lease,
                frame.SessionId, frame.RequestId, frame.Payload.ManifestPayloadHash, frame.TurnNumber);
            if (applied.Phase != "applied" || _gameLoop.SessionId != frame.SessionId || _gameLoop.TurnNumber != frame.TurnNumber)
                throw new InvalidDataException("joint_cleanup_phase_or_runtime_turn");
            var story = await _storyService.ReadMortalJointStoryAsync(lease, applied);
            if (story.Disposition != StoryService.MortalJointStoryDisposition.AlreadyAppended || story.Proof is not { } suffix)
                throw new InvalidDataException("joint_cleanup_owned_story_missing");
            var projection = await _charService.VerifyMortalJointProjectionAsync(lease, applied);
            if (projection.Disposition != CharacteristicsService.JointDerivedProjectionDisposition.ExistingVerified)
                throw new InvalidDataException("joint_cleanup_projection_refused: " + projection.Failure);
            var ready = ParseReadySignalMetadata(await _fs.ReadFileAsync(lease, "ready/turn_complete.json"), "ready/turn_complete.json");
            if (ready is null || ready.SessionId != frame.SessionId || ready.RequestId != frame.RequestId || ready.TurnNumber != frame.TurnNumber ||
                !HasValidTerminalSignalContract("turn_complete", ready) || await _fs.ReadFileBytesAsync(lease, "ready/turn_error.json") is not null)
                throw new InvalidDataException("joint_cleanup_terminal_not_owned");

            // Use the real destructive original/backup validator, including its
            // hash mode and every declared source image. No path-label shortcut.
            var authorityText = await _fs.ReadFileAsync(lease, PendingTurnSnapshotAuthority.AuthorityPath);
            if (!PendingTurnSnapshotAuthority.TryValidateManifestForDestructiveAuthority(
                frame.Manifest, authorityText, SnapshotHashJsonOpts,
                static manifest => manifest.ManifestPayloadHash,
                static (manifest, hash) => manifest.ManifestPayloadHash = hash,
                static manifest => manifest.SessionId, static manifest => manifest.RequestId, static manifest => manifest.TurnNumber,
                static manifest => manifest.Files, static manifest => manifest.SnapshotFileHashes,
                static manifest => manifest.ClientOwnedValidationHashes, static manifest => manifest.RollbackBaselineFiles,
                static manifest => manifest.SourceLabel, static manifest => manifest.RollbackBackups,
                path => _fs.ReadFileBytesAsync(lease, path).GetAwaiter().GetResult(), out var payload, out var failure) || payload is null ||
                payload.ManifestPayloadHash != frame.Payload.ManifestPayloadHash ||
                (payload.RollbackBackups.Count == 0 && payload.RollbackBaselineFiles.Count == 0))
                throw new InvalidDataException("joint_cleanup_original_backup_authority: " + failure);

            var paths = payload.Files.Values.Concat(payload.RollbackBackups.Values)
                .Concat(payload.Files.Keys.Where(path => ConsoleLocalTurnRollbackArtifacts.IsMarker(path)))
                .Concat(new[] { PendingTurnSnapshotManifestPath, PendingTurnSnapshotAuthority.AuthorityPath,
                    "input/turn_request.json", "ready/turn_complete.json", MortalWoundOpportunityAdapter.DecisionPath,
                    ValidationRepairRequestPath, ValidationRepairReadyPath }).Distinct(StringComparer.Ordinal).ToArray();
            PendingTurnSnapshotAuthority.RequireExactSignedPaths(paths);
            foreach (var path in payload.Files.Values)
                if (!IsValidatedPendingSnapshotArtifactPath(path)) throw new InvalidDataException("joint_cleanup_foreign_snapshot_path");
            foreach (var path in payload.RollbackBackups.Values)
                if (!IsValidatedRollbackBackupArtifactPath(path) || IsExplorerLocalTurnRollbackArtifactPath(path))
                    throw new InvalidDataException("joint_cleanup_unsupported_backup_path");
            if (await _fs.ReadFileBytesAsync(lease, ValidationRepairArtifactStallReportPath) is not null ||
                await _fs.ReadFileBytesAsync(lease, TerminalProtocolFailureRequestPath) is not null)
                throw new InvalidDataException("joint_cleanup_unresolved_protocol_evidence");

            var members = new List<CanonicalLocalFileChange>();
            foreach (var path in paths)
            {
                if (!_fs.UsesTrustedLocalWriter(lease, path)) throw new InvalidDataException("joint_cleanup_unsupported_storage_member: " + path);
                var bytes = await _fs.ReadFileBytesAsync(lease, path);
                // The full joint phase/output proof above authenticates either a
                // consumed absent draft or its exact accepted public payload.
                // Retain the same cleanup member even when its beforeimage is absent.
                if (bytes is null && path is not (ValidationRepairRequestPath or ValidationRepairReadyPath or MortalWoundOpportunityAdapter.DecisionPath))
                    throw new InvalidDataException("joint_cleanup_owned_member_missing: " + path);
                if (bytes is not null && path is "input/turn_request.json" or ValidationRepairRequestPath or ValidationRepairReadyPath)
                    RequireMortalJointCleanupControl(bytes, frame, path);
                members.Add(new(path, bytes?.ToArray(), null));
            }
            var marker = StrictJsonAuthority.Deserialize<JsonObject>(CanonicalJsonUtf8.DecodeOneOptionalBom(applied.MarkerBefore),
                SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed, "validated joint cleanup inventory")!;
            var footprint = marker["publishedFootprint"] as JsonObject ?? throw new InvalidDataException("joint_cleanup_footprint_missing");
            if (footprint.Any(pair => paths.Contains(pair.Key, StringComparer.Ordinal)))
                throw new InvalidDataException("joint_cleanup_mechanical_member_overlap");
            var retainedPaths = footprint.Select(pair => pair.Key).Concat(payload.Files.Keys)
                .Concat(new[] { MortalQteDecisionPublication.StatePath, suffix.StoryPath,
                    CharacteristicsService.JointDerivedProjectionPath, "output/narrative_response.json", "output/interface_updates.json",
                    "output/debug_logs.json", "game_state/history/chat_log.json", "game_state/combat/combat_log.json" })
                .Distinct(StringComparer.Ordinal).Except(paths, StringComparer.Ordinal).ToArray();
            var retained = new Dictionary<string, byte[]?>(StringComparer.Ordinal);
            foreach (var path in retainedPaths) retained[path] = await _fs.ReadFileBytesAsync(lease, path);
            var generation = _fs.ReadLocalGenerationSnapshot(lease).Binding;

            // Aggregation only: do not repair mirrors, award stats or perform any
            // lifecycle action. Full proof brackets this actual read-only API.
            await _stateManager.RefreshGameStateReadOnlyAsync(lease);
            if (_stateManager.CurrentState.TurnNumber != frame.TurnNumber)
                throw new InvalidDataException("joint_cleanup_runtime_story_projection");
            var response = await ReadMortalJointCompletionResponseAsync(lease);
            await RequireMortalJointCleanupImagesAsync(lease, members, retained, after: false);
            await applied.ValidateCurrentAsync(_fs, lease);
            await suffix.ValidateCurrentAsync(_fs, lease);
            var outcome = await _fs.PublishLocalFilesAsync(lease, members,
                validatePreparedNamespace: async () =>
                {
                    if (_fs.ReadLocalGenerationSnapshot(lease).Binding != generation || _gameLoop.SessionId != frame.SessionId ||
                        _gameLoop.TurnNumber != frame.TurnNumber) throw new InvalidDataException("joint_cleanup_owner_changed");
                    await applied.ValidateCurrentAsync(_fs, lease);
                    await suffix.ValidateCurrentAsync(_fs, lease);
                    await RequireMortalJointCleanupImagesAsync(lease, members, retained, after: false);
                });
            if (outcome.Disposition != TrustedLocalPublicationDisposition.Committed)
                return new(MortalJointCleanupDisposition.HeldBlocked, "joint_cleanup_publication_not_committed: " + outcome.Failure?.Message);
            _fs.RequireCommittedLocalPublication(outcome);
            // Resolve retained publisher debt before classifying cleanup success.
            // Original deletion means no later parsed marker can reissue this
            // authority: the actual known batch and complete readback settle it.
            _fs.ResolveBackupPublicationRecovery(lease);
            if (_fs.ReadLocalGenerationSnapshot(lease).Binding != generation)
                throw new InvalidDataException("joint_cleanup_generation_changed_after_commit");
            await RequireMortalJointCleanupImagesAsync(lease, members, retained, after: true);
            return new(MortalJointCleanupDisposition.CommittedAndVerified, null, response);
        }
        catch (SessionReplacedException) { throw; }
        catch (Exception error) when (error is CoordinatedStatePublicationUncertainException or InvalidDataException or IOException or
            UnauthorizedAccessException or InvalidOperationException or ArgumentException or JsonException or FormatException or OperationCanceledException)
        {
            // No sequential fallback, compensation from a missing original or
            // claim rearm/release. Partial/foreign cleanup is NOT qualified as
            // recoverable here; retain all remaining evidence and block gameplay.
            return new(MortalJointCleanupDisposition.HeldBlocked, error.Message);
        }
    }

    private async Task<GameResponse> ReadMortalJointCompletionResponseAsync(FileSystemManager.CanonicalWriteLease lease)
    {
        var response = new GameResponse();
        // 1. Read narrative from output/narrative_response.json (primary source per API spec)
        var narrativeJson = await _fs.ReadFileAsync(lease, "output/narrative_response.json");
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
                throw new InvalidDataException("joint_cleanup_display_invalid: output/narrative_response.json", ex);
            }
        }

        // Fallback: use narrative from state if not in output file
        if (string.IsNullOrEmpty(response.Response))
            response.Response = _stateManager.CurrentState.Narrative;

        // 2. Read dialogue options and image prompt from output/interface_updates.json
        var uiJson = await _fs.ReadFileAsync(lease, "output/interface_updates.json");
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
                throw new InvalidDataException("joint_cleanup_display_invalid: output/interface_updates.json", ex);
            }
        }

        // 3. Read GM thoughts from output/debug_logs.json
        var debugJson = await _fs.ReadFileAsync(lease, "output/debug_logs.json");
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
                throw new InvalidDataException("joint_cleanup_display_invalid: output/debug_logs.json", ex);
            }
        }

        // 4. Read combat log from distributed combat state if exists
        var combatJson = await _fs.ReadFileAsync(lease, "game_state/combat/combat_log.json");
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
                throw new InvalidDataException("joint_cleanup_display_invalid: game_state/combat/combat_log.json", ex);
            }
        }

        // 5. Populate status from state
        var st = _stateManager.CurrentState.PlayerStatus;
        response.PlayerStatus = new PlayerStatus
        {
            CurrentCondition = st.CurrentCondition
        };

        var woundNotifications = _acceptedTurnWoundNotifications;
        if (woundNotifications.Length != 0)
        {
            response.WoundNotifications = woundNotifications
                .Select(static value => value.Text.PlainText)
                .ToArray();
        }

        return response;
    }

    private static void RequireMortalJointCleanupControl(byte[] bytes, ValidatedPendingTurnSnapshotContext frame, string path)
    {
        var root = StrictJsonAuthority.Deserialize<JsonObject>(CanonicalJsonUtf8.DecodeOneOptionalBom(bytes),
            SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed, "joint cleanup control " + path) ?? throw new InvalidDataException("joint_cleanup_null_control");
        if (root["sessionId"]?.GetValue<string>() != frame.SessionId || root["requestId"]?.GetValue<string>() != frame.RequestId ||
            root["turnNumber"]?.GetValue<int>() != frame.TurnNumber)
            throw new InvalidDataException("joint_cleanup_foreign_control: " + path);
        if (path == "input/turn_request.json" && root["playerAction"]?.GetValue<string>() != frame.PlayerAction)
            throw new InvalidDataException("joint_cleanup_action_changed");
        if (path == ValidationRepairRequestPath && root["fullTurnResubmissionRequired"]?.GetValue<bool>() != true)
            throw new InvalidDataException("joint_cleanup_repair_not_joint_resubmission");
    }

    private async Task RequireMortalJointCleanupImagesAsync(FileSystemManager.CanonicalWriteLease lease,
        IReadOnlyList<CanonicalLocalFileChange> members, IReadOnlyDictionary<string, byte[]?> retained, bool after)
    {
        foreach (var member in members)
            if (!JointRestoreBytesEqual(await _fs.ReadFileBytesAsync(lease, member.RelativePath), after ? member.After : member.Before))
                throw new InvalidDataException("joint_cleanup_member_mismatch: " + member.RelativePath);
        foreach (var row in retained)
            if (!JointRestoreBytesEqual(await _fs.ReadFileBytesAsync(lease, row.Key), row.Value))
                throw new InvalidDataException("joint_cleanup_retained_union_changed: " + row.Key);
    }
}
