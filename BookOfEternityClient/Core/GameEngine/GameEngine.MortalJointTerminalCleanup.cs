using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
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
    /// <summary>
    /// Revalidates terminal ownership and proofs, then publishes and verifies the fixed cleanup cohort.
    /// </summary>
    /// <param name="lease">
    /// The active participating canonical lease used for every proof, read and publication.
    /// </param>
    /// <param name="frame">
    /// The original snapshot context whose authority and runtime tuple must still match.
    /// </param>
    /// <returns>
    /// Verified committed cleanup and its response, or a blocked result retaining the failure reason.
    /// Session replacement propagates to the caller.
    /// </returns>
    private async Task<MortalJointCleanupResult> CleanupEligibleMortalJointTerminalAsync(
        FileSystemManager.CanonicalWriteLease lease, ValidatedPendingTurnSnapshotContext frame)
    {
        try
        {
            using var cleanupTimer1 = _fs.MeasureOperation("JointCleanup.ResolveInitialRecovery");
            _fs.ResolveBackupPublicationRecovery(lease);
            cleanupTimer1?.Dispose();
            using var cleanupTimer2 = _fs.MeasureOperation("JointCleanup.ReadAppliedPhaseAsync");
            var applied = await MortalQteDecisionPublication.ReadJointProgressionPhaseAsync(_fs, lease,
                frame.SessionId, frame.RequestId, frame.Payload.ManifestPayloadHash, frame.TurnNumber);
            if (applied.Phase != "applied" || _gameLoop.SessionId != frame.SessionId || _gameLoop.TurnNumber != frame.TurnNumber)
                throw new InvalidDataException("joint_cleanup_phase_or_runtime_turn");
            cleanupTimer2?.Dispose();
            using var cleanupTimer3 = _fs.MeasureOperation("JointCleanup.ReadStoryAsync");
            var story = await _storyService.ReadMortalJointStoryAsync(lease, applied);
            if (story.Disposition != StoryService.MortalJointStoryDisposition.AlreadyAppended || story.Proof is not { } suffix)
                throw new InvalidDataException("joint_cleanup_owned_story_missing");
            cleanupTimer3?.Dispose();
            using var cleanupTimer4 = _fs.MeasureOperation("JointCleanup.VerifyProjectionAsync");
            var projection = await _charService.VerifyMortalJointProjectionAsync(lease, applied);
            if (projection.Disposition != CharacteristicsService.JointDerivedProjectionDisposition.ExistingVerified)
                throw new InvalidDataException("joint_cleanup_projection_refused: " + projection.Failure);
            cleanupTimer4?.Dispose();
            using var cleanupTimer5 = _fs.MeasureOperation("JointCleanup.VerifyTerminalSignalAsync");
            await VerifyMortalJointTerminalSignalAsync(_fs, lease, frame.SessionId, frame.RequestId, frame.TurnNumber, _logger);

            cleanupTimer5?.Dispose();
            // Use the real destructive original/backup validator, including its
            // hash mode and every declared source image. No path-label shortcut.
            using var cleanupTimer6 = _fs.MeasureOperation("JointCleanup.VerifyDestructiveOriginal");
            var payload = await VerifyMortalJointDestructiveOriginalAsync(_fs, lease, frame.Manifest, frame.Payload.ManifestPayloadHash);

            cleanupTimer6?.Dispose();
            using var cleanupTimer7 = _fs.MeasureOperation("JointCleanup.PlanCohortAndProtocolEvidence");
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

            cleanupTimer7?.Dispose();
            using var cleanupTimer8 = _fs.MeasureOperation("JointCleanup.CaptureMembersAsync");
            var members = new List<CanonicalLocalFileChange>();
            foreach (var path in paths)
            {
                if (!_fs.UsesTrustedLocalWriter(lease, path)) throw new InvalidDataException("joint_cleanup_unsupported_storage_member: " + path);
                using var cleanupTimer9 = _fs.MeasureOperation("JointCleanup.CaptureMember.ReadAsync");
                var bytes = await _fs.ReadFileBytesAsync(lease, path);
                cleanupTimer9?.Dispose();
                // The full joint phase/output proof above authenticates either a
                // consumed absent draft or its exact accepted public payload.
                // Retain the same cleanup member even when its beforeimage is absent.
                if (bytes is null && path is not (ValidationRepairRequestPath or ValidationRepairReadyPath or MortalWoundOpportunityAdapter.DecisionPath))
                    throw new InvalidDataException("joint_cleanup_owned_member_missing: " + path);
                if (bytes is not null && path is "input/turn_request.json" or ValidationRepairRequestPath or ValidationRepairReadyPath)
                    RequireMortalJointCleanupControl(bytes, frame, path);
                members.Add(new(path, bytes?.ToArray(), null));
            }
            cleanupTimer8?.Dispose();
            using var cleanupTimer10 = _fs.MeasureOperation("JointCleanup.PlanRetainedCohort");
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
            cleanupTimer10?.Dispose();
            using var cleanupTimer11 = _fs.MeasureOperation("JointCleanup.CaptureRetainedAsync");
            var retained = new Dictionary<string, byte[]?>(StringComparer.Ordinal);
            foreach (var path in retainedPaths)
            {
                using var retainedReadTiming = _fs.MeasureOperation("JointCleanup.CaptureRetained.ReadAsync");
                retained[path] = await _fs.ReadFileBytesAsync(lease, path);
            }
            cleanupTimer11?.Dispose();
            using var cleanupTimer12 = _fs.MeasureOperation("JointCleanup.ReadGeneration");
            var generation = _fs.ReadLocalGenerationSnapshot(lease).Binding;
            cleanupTimer12?.Dispose();

            // Aggregation only: do not repair mirrors, award stats or perform any
            // lifecycle action. Full proof brackets this actual read-only API.
            using var cleanupTimer13 = _fs.MeasureOperation("JointCleanup.RefreshReadOnlyStateAsync");
            await _stateManager.RefreshGameStateReadOnlyAsync(lease);
            if (_stateManager.CurrentState.TurnNumber != frame.TurnNumber)
                throw new InvalidDataException("joint_cleanup_runtime_story_projection");
            cleanupTimer13?.Dispose();
            using var cleanupTimer14 = _fs.MeasureOperation("JointCleanup.ReadResponseAsync");
            var response = await ReadMortalJointCompletionResponseAsync(lease);
            cleanupTimer14?.Dispose();
            using var cleanupTimer15 = _fs.MeasureOperation("JointCleanup.VerifyBeforeImagesAsync");
            await RequireMortalJointCleanupImagesAsync(lease, members, retained, after: false);
            cleanupTimer15?.Dispose();
            using var cleanupTimer16 = _fs.MeasureOperation("JointCleanup.BeforePublish.ValidateAppliedAsync");
            await applied.ValidateCurrentAsync(_fs, lease);
            cleanupTimer16?.Dispose();
            using var cleanupTimer17 = _fs.MeasureOperation("JointCleanup.BeforePublish.ValidateStoryAsync");
            await suffix.ValidateCurrentAsync(_fs, lease);
            cleanupTimer17?.Dispose();
            using var cleanupTimer18 = _fs.MeasureOperation("JointCleanup.PublishLocalFilesAsync");
            var outcome = await _fs.PublishLocalFilesAsync(lease, members,
                validatePreparedNamespace: async () =>
                {
                    using var cleanupTimer19 = _fs.MeasureOperation("JointCleanup.PreparedNamespaceAsync");
                    using var cleanupTimer20 = _fs.MeasureOperation("JointCleanup.PreparedNamespace.VerifyOwner");
                    if (_fs.ReadLocalGenerationSnapshot(lease).Binding != generation || _gameLoop.SessionId != frame.SessionId ||
                        _gameLoop.TurnNumber != frame.TurnNumber) throw new InvalidDataException("joint_cleanup_owner_changed");
                    cleanupTimer20?.Dispose();
                    using var cleanupTimer21 = _fs.MeasureOperation("JointCleanup.PreparedNamespace.ValidateAppliedAsync");
                    await applied.ValidateCurrentAsync(_fs, lease);
                    cleanupTimer21?.Dispose();
                    using var cleanupTimer22 = _fs.MeasureOperation("JointCleanup.PreparedNamespace.ValidateStoryAsync");
                    await suffix.ValidateCurrentAsync(_fs, lease);
                    cleanupTimer22?.Dispose();
                    using var cleanupTimer23 = _fs.MeasureOperation("JointCleanup.PreparedNamespace.VerifyImagesAsync");
                    await RequireMortalJointCleanupImagesAsync(lease, members, retained, after: false);
                    cleanupTimer23?.Dispose();
                    cleanupTimer19?.Dispose();
                });
            cleanupTimer18?.Dispose();
            using var cleanupTimer24 = _fs.MeasureOperation("JointCleanup.ClassifyCommittedPublication");
            if (outcome.Disposition != TrustedLocalPublicationDisposition.Committed)
                return new(MortalJointCleanupDisposition.HeldBlocked, "joint_cleanup_publication_not_committed: " + outcome.Failure?.Message);
            _fs.RequireCommittedLocalPublication(outcome);
            cleanupTimer24?.Dispose();
            // Resolve retained publisher debt before classifying cleanup success.
            // Original deletion means no later parsed marker can reissue this
            // authority: the actual known batch and complete readback settle it.
            using var cleanupTimer25 = _fs.MeasureOperation("JointCleanup.ReconcileCommittedGeneration");
            _fs.ResolveBackupPublicationRecovery(lease);
            if (_fs.ReadLocalGenerationSnapshot(lease).Binding != generation)
                throw new InvalidDataException("joint_cleanup_generation_changed_after_commit");
            cleanupTimer25?.Dispose();
            using var cleanupTimer26 = _fs.MeasureOperation("JointCleanup.VerifyAfterImagesAsync");
            await RequireMortalJointCleanupImagesAsync(lease, members, retained, after: true);
            cleanupTimer26?.Dispose();
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

    /// <summary>
    /// Verifies the existing owned completion signal and error absence without issuing cleanup authority.
    /// </summary>
    /// <param name="files">
    /// The current participating file manager.
    /// </param>
    /// <param name="lease">
    /// Its active canonical lease, validated by each original file read.
    /// </param>
    /// <param name="sessionId">
    /// Expected session identifier from the current original.
    /// </param>
    /// <param name="requestId">
    /// Expected request identifier from the current original.
    /// </param>
    /// <param name="turnNumber">
    /// Expected original turn number.
    /// </param>
    /// <param name="logger">
    /// The original engine logger; component callers may explicitly substitute their own logger.
    /// </param>
    /// <returns>
    /// Completion after the exact terminal contract succeeds; original read, logging and refusal failures propagate.
    /// </returns>
    internal static async Task VerifyMortalJointTerminalSignalAsync(FileSystemManager files,
        FileSystemManager.CanonicalWriteLease lease, string sessionId, string requestId, int turnNumber,
        ILogger<GameEngine> logger)
    {
        var ready = ParseReadySignalMetadataWithLogger(await files.ReadFileAsync(lease, "ready/turn_complete.json"), "ready/turn_complete.json", logger);
        if (ready is null || ready.SessionId != sessionId || ready.RequestId != requestId || ready.TurnNumber != turnNumber ||
            !HasValidTerminalSignalContract("turn_complete", ready) || await files.ReadFileBytesAsync(lease, "ready/turn_error.json") is not null)
            throw new InvalidDataException("joint_cleanup_terminal_not_owned");
    }

    /// <summary>
    /// Revalidates declared original and backup DATA through the existing destructive validator.
    /// This result alone grants no cleanup permission.
    /// </summary>
    /// <param name="files">
    /// The participating manager used for actual authority and rollback-byte reads.
    /// </param>
    /// <param name="lease">
    /// Its active canonical lease; every original read retains normal ownership validation.
    /// </param>
    /// <param name="manifest">
    /// Parsed original manifest DATA, not a private validated snapshot context.
    /// </param>
    /// <param name="expectedManifestHash">
    /// The exact manifest hash from the caller's current original proof.
    /// </param>
    /// <returns>
    /// Validated payload DATA after exact hash and nonempty rollback conditions; existing failures propagate.
    /// </returns>
    internal static async Task<PendingTurnSnapshotAuthority.PendingTurnSnapshotAuthorityPayload> VerifyMortalJointDestructiveOriginalAsync(
        FileSystemManager files, FileSystemManager.CanonicalWriteLease lease, PendingTurnSnapshotManifest manifest,
        string expectedManifestHash)
    {
        var authorityText = await files.ReadFileAsync(lease, PendingTurnSnapshotAuthority.AuthorityPath);
        if (!PendingTurnSnapshotAuthority.TryValidateManifestForDestructiveAuthority(
            manifest, authorityText, SnapshotHashJsonOpts,
            static manifest => manifest.ManifestPayloadHash,
            static (manifest, hash) => manifest.ManifestPayloadHash = hash,
            static manifest => manifest.SessionId, static manifest => manifest.RequestId, static manifest => manifest.TurnNumber,
            static manifest => manifest.Files, static manifest => manifest.SnapshotFileHashes,
            static manifest => manifest.ClientOwnedValidationHashes, static manifest => manifest.RollbackBaselineFiles,
            static manifest => manifest.SourceLabel, static manifest => manifest.RollbackBackups,
            path =>
            {
                using var rollbackReadTiming = files.MeasureOperation("JointCleanup.Authority.ReadRollbackBytes");
                return files.ReadFileBytesAsync(lease, path).GetAwaiter().GetResult();
            }, out var payload, out var failure) || payload is null ||
            payload.ManifestPayloadHash != expectedManifestHash ||
            (payload.RollbackBackups.Count == 0 && payload.RollbackBaselineFiles.Count == 0))
            throw new InvalidDataException("joint_cleanup_original_backup_authority: " + failure);
        return payload;
    }

    /// <summary>
    /// Aggregates terminal display output and the already refreshed state without changing game files.
    /// </summary>
    /// <param name="lease">
    /// The active canonical lease used to read narrative, interface, debug and combat output.
    /// </param>
    /// <returns>
    /// The response assembled from accepted output, current state and accepted wound notifications.
    /// Malformed display output causes an invalid-data failure.
    /// </returns>
    private async Task<GameResponse> ReadMortalJointCompletionResponseAsync(FileSystemManager.CanonicalWriteLease lease)
    {
        var response = new GameResponse();
        // 1. Read narrative from output/narrative_response.json (primary source per API spec)
        using var cleanupTimer27 = _fs.MeasureOperation("JointCleanup.Response.ReadNarrativeAsync");
        var narrativeJson = await _fs.ReadFileAsync(lease, "output/narrative_response.json");
        cleanupTimer27?.Dispose();
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
        using var cleanupTimer28 = _fs.MeasureOperation("JointCleanup.Response.ReadInterfaceAsync");
        var uiJson = await _fs.ReadFileAsync(lease, "output/interface_updates.json");
        cleanupTimer28?.Dispose();
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
        using var cleanupTimer29 = _fs.MeasureOperation("JointCleanup.Response.ReadDebugAsync");
        var debugJson = await _fs.ReadFileAsync(lease, "output/debug_logs.json");
        cleanupTimer29?.Dispose();
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
        using var cleanupTimer30 = _fs.MeasureOperation("JointCleanup.Response.ReadCombatAsync");
        var combatJson = await _fs.ReadFileAsync(lease, "game_state/combat/combat_log.json");
        cleanupTimer30?.Dispose();
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

    /// <summary>
    /// Verifies the actual cleanup images through the current leased reader.
    /// </summary>
    /// <param name="lease">
    /// The current canonical lease required by every physical read.
    /// </param>
    /// <param name="members">
    /// The exact cleanup cohort with its before and after images.
    /// </param>
    /// <param name="retained">
    /// Retained exact bytes or required absences.
    /// </param>
    /// <param name="after">
    /// True verifies committed after-images; false verifies before-images.
    /// </param>
    /// <returns>
    /// Completion after every image matches, propagating the first refusal or read failure.
    /// </returns>
    private Task RequireMortalJointCleanupImagesAsync(FileSystemManager.CanonicalWriteLease lease,
        IReadOnlyList<CanonicalLocalFileChange> members, IReadOnlyDictionary<string, byte[]?> retained, bool after) =>
        JointCleanupImageVerifier.RequireImagesAsync(members, retained, after, async path =>
        {
            using var readTiming = _fs.MeasureOperation("JointCleanup.VerifyImage.ReadAsync");
            return await _fs.ReadFileBytesAsync(lease, path);
        });
}
