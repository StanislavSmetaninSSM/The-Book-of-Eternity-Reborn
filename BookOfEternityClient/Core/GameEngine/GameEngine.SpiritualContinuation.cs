using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using BookOfEternityClient.Services.GmWorkers;
using Microsoft.Extensions.Logging;

namespace BookOfEternityClient.Core;

public partial class GameEngine
{
    /// <summary>
    /// Distinguishes a usable spiritual boundary from original-input diagnostics, rejection and retained retry state.
    /// </summary>
    private enum SpiritualContinuationAdmission
    {
        /// <summary>
        /// Allows ordinary resource admission for an ordinary turn or completed unpublished spiritual authority.
        /// </summary>
        Admitted,

        /// <summary>
        /// Requires actual raw resource errors before a failed capture without a checkpoint may enter ordinary repair.
        /// </summary>
        RequiresInitialResourceValidation,

        /// <summary>
        /// Rejects a cancelled continuation or a boundary whose authority cannot be used.
        /// </summary>
        Rejected,

        /// <summary>
        /// Identifies private continuation state that must be retained for a later recovery attempt.
        /// </summary>
        RetryableHeld
    }

    /// <summary>
    /// Carries upstream diagnostics and an explicit private spiritual authority rejection across validation scopes.
    /// </summary>
    /// <param name="Issues">
    /// Diagnostics produced by the upstream validation pass, including ordinary repairable input errors.
    /// </param>
    /// <param name="RejectSpiritualBoundary">
    /// Whether private spiritual authority failed admission and must stop validation before resource admission or GM repair.
    /// </param>
    private sealed record UpstreamAcceptedTurnValidation(
        IReadOnlyList<ValidationIssue> Issues, bool RejectSpiritualBoundary);

    /// <summary>
    /// Validates upstream item diagnostics while preserving retained spiritual publication and foreign owner claims.
    /// </summary>
    /// <returns>
    /// Ordinary item diagnostics without terminal rejection, or retained publication freshness failures and
    /// checkpoint ownership conflicts marked for terminal rejection before ordinary resource admission.
    /// A vacant checkpoint route releases only the advisory item registration created by this validation pass.
    /// </returns>
    private async Task<UpstreamAcceptedTurnValidation> ValidateAcceptedTurnUpstreamRawItemsAsync()
    {
        await using var lease = await _fs.AcquireCanonicalWriteLeaseAsync();
        if (AcceptedMechanicsPlanAuthority.TryPeekSpiritualPublication(_fs, lease, out var retained))
        {
            var retainedIssues = await retained.ValidateCurrentInputsAsync(_fs, lease);
            return new UpstreamAcceptedTurnValidation(retainedIssues, retainedIssues.Count > 0);
        }
        if (!_fs.FileExists(lease, SpiritualWoundCaptureCheckpointState.StatePath))
            return new UpstreamAcceptedTurnValidation(
                await _validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync(lease), false);

        // This fresh factory is only a vacancy probe: it is never registered or used to allocate.
        // Reference matching therefore rejects every existing ordinary or private item owner.
        var vacancyProbe = new MortalItemIdentityFactory();
        if (!AcceptedTurnAuthorityRegistry.CanBeginSpiritualOriginalIntake(
                _fs, lease, vacancyProbe, checkPlansAndItems: true))
            return new UpstreamAcceptedTurnValidation([new ValidationIssue(SpiritualWoundCaptureCheckpointState.StatePath, IssueSeverity.Error,
                "Не удалось доказать исходное духовное действие.",
                code: "spiritual_original_intake_claim_conflict", section: "AfterlifeSpiritualConflict",
                expected: "exact original-turn spiritual source authority", actual: "a vacant physical generation")], true);

        try
        {
            return new UpstreamAcceptedTurnValidation(
                await _validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync(lease), false);
        }
        finally
        {
            // Vacancy was proved before this ordinary pass and the same lease still excludes other owners.
            // Its advisory registration must not block the subsequent authenticated cold C2 intake.
            MortalItemAcceptedTurnAuthority.InvalidateValidatedItems(_fs, lease);
        }
    }

    /// <summary>
    /// Completes private spiritual continuation before ordinary resource admission and the common publisher.
    /// </summary>
    /// <param name="source">
    /// Accepted-turn caller label included in bounded continuation requests.
    /// </param>
    /// <param name="expectedTurn">
    /// Original turn number that must survive every continuation boundary.
    /// </param>
    /// <param name="originalContext">
    /// Original signed snapshot identity, or <see langword="null"/> when the caller has no retained context.
    /// </param>
    /// <returns>
    /// Admission for an ordinary turn or completed unpublished spiritual authority, a requirement to diagnose
    /// failed initial resource input without a checkpoint, retained retry state for unresolved pending repair,
    /// or rejection when continuation cannot proceed.
    /// </returns>
    private async Task<SpiritualContinuationAdmission> ContinueAcceptedSpiritualTurnAsync(string source, int expectedTurn,
        ValidatedPendingTurnSnapshotContext? originalContext)
    {
        var generation = await CaptureCurrentSessionGenerationAsync();
        try
        {
            while (true)
            {
                SpiritualWoundContinuationRequest? request;
                IReadOnlyList<ValidationIssue> issues;
                ValidatedPendingTurnSnapshotContext context;
                await using (var lease = await _fs.AcquireCanonicalWriteLeaseAsync())
                {
                    ThrowIfRepairSessionReplaced(lease, generation);
                    if (AcceptedMechanicsPlanAuthority.TryPeekSpiritualPublication(_fs, lease, out var retained))
                        return LogSpiritualContinuationIssues(await retained.ValidateCurrentInputsAsync(_fs, lease))
                            ? SpiritualContinuationAdmission.Admitted : SpiritualContinuationAdmission.Rejected;

                    var current = await _validator.ReadSpiritualWoundContinuationAsync(lease);
                    if (current.Disposition == "no_checkpoint")
                    {
                        foreach (var transportPath in new[] { ValidationRepairRequestPath, ValidationRepairReadyPath })
                        {
                            var previous = await _fs.ReadFileAsync(lease, transportPath);
                            if (HasSpiritualContinuationEnvelope(previous))
                                throw new InvalidDataException("Public continuation transport cannot restore missing C2 authority.");
                        }
                        var resolution = await ResolveActivePendingTurnSnapshotContextAsync();
                        if (resolution.Context is not { } fresh)
                            return SpiritualContinuationAdmission.Admitted; // Ordinary admission diagnoses an unusable baseline.
                        context = fresh;
                        if (!await HasSignedSpiritualExchangeIntentAsync(lease, context))
                            return SpiritualContinuationAdmission.Admitted;
                        EnsureSpiritualOriginalContext(context, originalContext, expectedTurn);
                        await RequireSpiritualOriginalRequestAsync(lease, context);
                        if (_fs.FileExists(lease, AcceptedMechanicsPlan.WoundCommandPath))
                            throw new InvalidDataException("A spiritual command has no private checkpoint authority.");
                        if (!await BeginAcceptedSpiritualCaptureAsync(lease))
                        {
                            // Begin has disposed its capture. Only genuinely absent private state may return
                            // to ordinary diagnostics; a partially written or damaged checkpoint never does.
                            var afterCapture = await _validator.ReadSpiritualWoundContinuationAsync(lease);
                            return afterCapture.Disposition == "no_checkpoint"
                                ? SpiritualContinuationAdmission.RequiresInitialResourceValidation
                                : SpiritualContinuationAdmission.Rejected;
                        }
                        if (AcceptedMechanicsPlanAuthority.TryPeekSpiritualPublication(_fs, lease, out _))
                            return SpiritualContinuationAdmission.Admitted;
                        // The first capture has been disposed before the cold owner is reopened.
                        current = await _validator.ReadSpiritualWoundContinuationAsync(lease);
                    }

                    var snapshot = await ResolveActivePendingTurnSnapshotContextAsync();
                    context = snapshot.Context ?? throw new InvalidDataException("The spiritual snapshot is unavailable.");
                    EnsureSpiritualOriginalContext(context, originalContext, expectedTurn);
                    await RequireSpiritualOriginalRequestAsync(lease, context);
                    if (current.Disposition is "blocked" or "automatic_continuation")
                    {
                        // Only the actual adapter may repair pending images or save an automatic guarantee.
                        var opened = await _validator.OpenC2PrivateSessionAsync(lease);
                        var boundary = await ResolveSpiritualPrivateBoundaryAsync(lease, opened);
                        if (boundary != SpiritualContinuationAdmission.Admitted)
                            return boundary;
                        current = await _validator.ReadSpiritualWoundContinuationAsync(lease);
                    }
                    var outstandingDependentRequest = false;
                    if (current.Disposition == "dependent_draft")
                    {
                        var transport = await TryReconcileDependentSpiritualTransportAsync(lease, context, current);
                        if (!transport.Allowed)
                            return SpiritualContinuationAdmission.RetryableHeld;
                        current = transport.Current;
                        outstandingDependentRequest = transport.OutstandingRequest;
                    }
                    if (current.Disposition == "completed_unpublished")
                    {
                        await CleanupObsoleteSpiritualTransportAsync(lease, context);
                        // The completed owner must be reopened with physical snapshot reads. Register after
                        // cleanup, before ordinary validation starts its cached snapshot scope.
                        var publicationIssues = await _validator.TryPrepareSpiritualC4PublicationAsync(lease);
                        if (publicationIssues is null || !LogSpiritualContinuationIssues(publicationIssues) ||
                            !AcceptedMechanicsPlanAuthority.TryPeekSpiritualPublication(_fs, lease, out _))
                        {
                            _logger.LogWarning("Completed spiritual continuation did not register publication authority.");
                            return SpiritualContinuationAdmission.Rejected;
                        }
                        return SpiritualContinuationAdmission.Admitted;
                    }
                    if (current.Disposition == "dependent_draft" && current.Issues.Count == 0 &&
                        !outstandingDependentRequest && current.AcceptedRequest is null)
                    {
                        var resumed = await _validator.OpenC2PrivateSessionAsync(lease);
                        using var owner = resumed.Session;
                        if (resumed.Disposition != "dependent_continuation" || owner is null)
                        {
                            LogSpiritualContinuationIssues(resumed.Issues, "Saved decision is unavailable.");
                            return SpiritualContinuationAdmission.Rejected;
                        }
                        var next = await owner.ResumeDependentContinuationAsync(lease);
                        owner.Dispose();
                        var boundary = await ResolveSpiritualPrivateBoundaryAsync(lease, next);
                        if (boundary != SpiritualContinuationAdmission.Admitted)
                            return boundary;
                        await CleanupObsoleteSpiritualTransportAsync(lease, context);
                        continue;
                    }
                    if (current.Disposition is not ("decision" or "dependent_draft") || current.Request is null)
                    {
                        LogSpiritualContinuationIssues(current.Issues, "No current spiritual response can be requested.");
                        return SpiritualContinuationAdmission.Rejected;
                    }
                    request = current.Request;
                    issues = current.Issues;
                }

                // Neither a private owner, snapshot override nor canonical lease crosses this external wait.
                var response = await WaitForSpiritualContinuationAsync(source, generation, context, request, issues);
                if (response != SpiritualContinuationAdmission.Admitted)
                    return response;
            }
        }
        catch (SessionReplacedException) { throw; }
        catch (OperationCanceledException) { throw; }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or
            JsonException or InvalidOperationException or FormatException or OverflowException or DecoderFallbackException)
        {
            _logger.LogWarning(error, "The accepted spiritual continuation was rejected before publication.");
            return SpiritualContinuationAdmission.Rejected;
        }
    }

    /// <summary>
    /// Holds a freshly reconstructed dependent boundary when its existing transport cannot be reconciled.
    /// </summary>
    /// <param name="lease">
    /// Active canonical lease covering the exact transport reads and permitted obsolete cleanup.
    /// </param>
    /// <param name="context">
    /// Authenticated original turn identity required for each observed transport envelope.
    /// </param>
    /// <param name="current">
    /// Fresh private projection authenticated before examining the public transport.
    /// </param>
    /// <returns>
    /// The reconciled projection and request status, or a disallowed result that retains the original turn
    /// instead of allowing terminal rejection cleanup to destroy its recovery inputs.
    /// </returns>
    private async Task<(ValidationService.SpiritualWoundContinuationReadResult Current,
        bool OutstandingRequest, bool Allowed)> TryReconcileDependentSpiritualTransportAsync(
        FileSystemManager.CanonicalWriteLease lease, ValidatedPendingTurnSnapshotContext context,
        ValidationService.SpiritualWoundContinuationReadResult current)
    {
        try
        {
            return await ReconcileDependentSpiritualTransportAsync(lease, context, current);
        }
        catch (Exception error) when (error is InvalidDataException or JsonException or InvalidOperationException or
            KeyNotFoundException or FormatException or OverflowException or DecoderFallbackException)
        {
            _logger.LogWarning(error, "Dependent spiritual transport remains held without replacing its recovery inputs.");
            return (current, false, false);
        }
    }

    /// <summary>
    /// Reconciles existing dependent transport against a freshly reconstructed private boundary.
    /// </summary>
    /// <param name="lease">
    /// Active canonical lease covering exact transport comparison, validation and obsolete cleanup.
    /// </param>
    /// <param name="context">
    /// Authenticated original turn identity required for each observed transport envelope.
    /// </param>
    /// <param name="current">
    /// Fresh detached dependent projection, including any replay-proved obsolete accepted request.
    /// </param>
    /// <returns>
    /// The current projection, whether an issued request remains and whether transport reconciliation allows continuation.
    /// An orphan or mismatching obsolete Ready is rejected before transport replacement.
    /// </returns>
    private async Task<(ValidationService.SpiritualWoundContinuationReadResult Current,
        bool OutstandingRequest, bool Allowed)> ReconcileDependentSpiritualTransportAsync(
        FileSystemManager.CanonicalWriteLease lease, ValidatedPendingTurnSnapshotContext context,
        ValidationService.SpiritualWoundContinuationReadResult current)
    {
        var outstanding = await _fs.ReadFileBytesAsync(lease, ValidationRepairRequestPath);
        if (outstanding is not null)
        {
            var root = ReadSpiritualTransportRoot(DecodeSpiritualTransport(outstanding));
            RequireSpiritualTransportIdentity(root, context);
            if (root[SpiritualWoundContinuationProtocol.EnvelopeName] is { } envelope)
            {
                var issued = SpiritualWoundContinuationProtocol.ReadRequest(JsonSerializer.SerializeToElement(envelope));
                if (issued.Phase == "dependent_draft" && current.AcceptedRequest is { } accepted &&
                    JsonSerializer.Serialize(issued) == JsonSerializer.Serialize(accepted))
                {
                    // This old A is cleanup-only after owner replay proved its durable journal row.
                    var oldReady = await _fs.ReadFileBytesAsync(lease, ValidationRepairReadyPath);
                    if (oldReady is not null)
                    {
                        var readyRoot = ReadSpiritualTransportRoot(DecodeSpiritualTransport(oldReady));
                        RequireSpiritualTransportIdentity(readyRoot, context);
                        var acceptedResponse = SpiritualWoundContinuationProtocol.ReadResponse(
                            JsonSerializer.SerializeToElement(readyRoot[SpiritualWoundContinuationProtocol.EnvelopeName]));
                        if (SpiritualWoundContinuationProtocol.ValidateResponse(accepted, acceptedResponse).Count != 0)
                            throw new InvalidDataException("Obsolete dependent Ready does not match private progress.");
                        await DeleteExactSpiritualTransportAsync(lease, ValidationRepairReadyPath, oldReady);
                    }
                    await DeleteExactSpiritualTransportAsync(lease, ValidationRepairRequestPath, outstanding);
                }
                else if (issued.Phase == "dependent_draft")
                {
                    var preserved = await _validator.ValidateSpiritualWoundContinuationDraftAsync(lease, issued,
                        new SpiritualWoundContinuationResponse { ContinuationId = issued.ContinuationId, WoundDecisions = [] },
                        new Dictionary<string, byte[]?>());
                    if (!LogSpiritualContinuationIssues(preserved))
                        return (current, false, false);
                    current = await _validator.ReadSpiritualWoundContinuationAsync(lease, issued);
                    RequireSameSpiritualContinuation(current.Request, issued);
                    return (current, true, true);
                }
                else
                    throw new InvalidDataException("An unrelated spiritual phase cannot replace the dependent request.");
            }
            else
                throw new InvalidDataException("An unrelated repair request cannot replace the dependent request.");
        }
        else if (_fs.FileExists(lease, ValidationRepairReadyPath))
            throw new InvalidDataException("A dependent Ready has no exact outstanding request.");
        return (current, false, true);
    }

    /// <summary>
    /// Releases private owners and attempts one genuine pending repair after a confirmed checkpoint advancement.
    /// </summary>
    /// <param name="lease">
    /// Active canonical lease covering owner disposal, cold reopening and the resulting boundary checks.
    /// </param>
    /// <param name="boundary">
    /// Actual private adapter result; any retained session is disposed before recovery or public transport cleanup.
    /// </param>
    /// <returns>
    /// Admission for a verified private boundary, retained retry state when pending repair still cannot commit,
    /// or rejection for other unavailable authority.
    /// </returns>
    private async Task<SpiritualContinuationAdmission> ResolveSpiritualPrivateBoundaryAsync(
        FileSystemManager.CanonicalWriteLease lease, ValidationService.SpiritualC2PrivateOpenResult boundary)
    {
        boundary.Session?.Dispose();
        if (boundary.Disposition == "repair_required")
        {
            boundary = await _validator.OpenC2PrivateSessionAsync(lease);
            boundary.Session?.Dispose();
        }
        if (boundary.Disposition is "offer" or "dependent_continuation" or "completed_unpublished")
            return SpiritualContinuationAdmission.Admitted;
        LogSpiritualContinuationIssues(boundary.Issues, "The private spiritual boundary could not be recovered.");
        return boundary.Disposition == "repair_required"
            ? SpiritualContinuationAdmission.RetryableHeld : SpiritualContinuationAdmission.Rejected;
    }

    /// <summary>
    /// Detects exchange intent from signed original and current conflict images without admitting future mechanics.
    /// </summary>
    /// <param name="lease">
    /// Active lease covering the signed baseline and current draft reads.
    /// </param>
    /// <param name="context">
    /// Freshly authenticated original snapshot supplying the baseline path and hash.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the draft introduces an exchange;
    /// unchanged state and start-only updates return <see langword="false"/>.
    /// </returns>
    private async Task<bool> HasSignedSpiritualExchangeIntentAsync(FileSystemManager.CanonicalWriteLease lease,
        ValidatedPendingTurnSnapshotContext context)
    {
        var rawJson = await _fs.ReadFileAsync(lease, AfterlifeSpiritualConflictState.StatePath);
        if (rawJson is null) return false;
        var raw = ReadSpiritualTransportRoot(rawJson);
        var payload = context.Payload;
        var original = AfterlifeSpiritualConflictState.CreateDefaultRoot();
        if (payload.Files.TryGetValue(AfterlifeSpiritualConflictState.StatePath, out var path))
        {
            if (!payload.SnapshotFileHashes.TryGetValue(AfterlifeSpiritualConflictState.StatePath, out var expectedHash))
                throw new InvalidDataException("The signed original conflict hash is missing.");
            var originalBytes = await _fs.ReadFileBytesAsync(lease, path);
            if (originalBytes is null || !string.Equals(expectedHash,
                    PendingTurnSnapshotAuthority.ComputeSnapshotFileHash(payload, originalBytes), StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The signed original conflict image changed.");
            original = ReadSpiritualTransportRoot(DecodeSpiritualTransport(originalBytes));
        }
        if (raw[AfterlifeSpiritualConflictState.ResponseField] is JsonObject update)
        {
            var mode = update["mode"]?.GetValue<string>();
            if (mode == AfterlifeSpiritualConflictState.ModeExchange || update["terminalExchange"] is not null ||
                update["resolution"]?["terminalExchange"] is not null)
                return true;
            if (mode == AfterlifeSpiritualConflictState.ModeStart)
            {
                var start = update["conflictState"] as JsonObject ?? update["activeConflict"] as JsonObject ??
                    update["conflictSeed"] as JsonObject;
                if (start?["exchangeLog"] is JsonArray started && started.Count > 0)
                    return true;
            }
        }
        var candidate = raw[AfterlifeSpiritualConflictState.ResponseField] is JsonObject wrapper
            ? AfterlifeSpiritualConflictState.ApplyUpdate(original, wrapper) : raw;
        candidate.Remove(AfterlifeSpiritualConflictState.ResponseField);
        if (ValidationService.SpiritualWoundSourceSession.TryProjectInitialActiveExchangeInventory(
                original, candidate, out _, out _))
            return true;
        var oldActive = original["activeConflict"] as JsonObject;
        var active = candidate["activeConflict"] as JsonObject;
        if (active?["exchangeLog"] is JsonArray exchanges && exchanges.Count > 0 &&
            !JsonNode.DeepEquals(oldActive?["exchangeLog"], exchanges))
            return true;
        var priorRecent = original["recentConflicts"] as JsonArray ?? [];
        if (candidate["recentConflicts"] is JsonArray recent && recent.OfType<JsonObject>().Any(row =>
                row["terminalExchange"] is not null && !priorRecent.Any(old => JsonNode.DeepEquals(old, row))))
            return true;
        if (_fs.FileExists(lease, AcceptedMechanicsPlan.WoundCommandPath) &&
            original["activeConflict"] is JsonObject)
            throw new InvalidDataException("An unexplained wound command cannot bypass the spiritual checkpoint.");
        return false;
    }

    /// <summary>
    /// Executes the finite original exchange inventory until a real offer or source-only completion exists.
    /// </summary>
    /// <param name="lease">
    /// Active lease retained until the initial capture is disposed.
    /// </param>
    /// <returns>
    /// <see langword="true"/> for a committed private boundary or registered completed source-only publication authority;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    private async Task<bool> BeginAcceptedSpiritualCaptureAsync(FileSystemManager.CanonicalWriteLease lease)
    {
        var recorded = await _validator.CaptureSpiritualOriginalTurnWithIntakeAsync(lease);
        using var capture = recorded.Capture;
        if (capture is null || recorded.Issues.Count != 0)
            return LogSpiritualContinuationIssues(recorded.Issues, "Original spiritual capture is unavailable.");
        if (!LogSpiritualContinuationIssues(await capture.BeginResourceExecutionAsync(lease)))
            return false;
        var advanced = await capture.AdvanceNextResourceExchangeAsync(lease);
        if (!LogSpiritualContinuationIssues(advanced.Issues)) return false;
        var inventory = capture.ReadOriginalActiveExchangeInventory(lease);
        if (inventory.Inventory is not { } original || original.ExchangeIds.Count == 0)
            return LogSpiritualContinuationIssues(inventory.Issues, "Original exchange inventory is unavailable.");
        for (var ordinal = 0; ordinal < original.ExchangeIds.Count; ordinal++)
        {
            if (advanced.Step?.Interval is not { } interval || interval.Ordinal != ordinal ||
                interval.ExchangeId != original.ExchangeIds[ordinal])
                throw new InvalidDataException("The spiritual exchange escaped its original finite inventory.");
            var transported = await capture.CommitC2FirstTransportAsync(lease, interval);
            if (transported.Disposition is "committed" or "repair_required")
                return true;
            if (transported.Disposition != "no_offer" || transported.Issues.Count != 0)
                return LogSpiritualContinuationIssues(transported.Issues, "The initial spiritual checkpoint was not committed.");
            if (ordinal + 1 < original.ExchangeIds.Count)
            {
                advanced = await capture.AdvanceNextResourceExchangeAsync(lease);
                if (!LogSpiritualContinuationIssues(advanced.Issues)) return false;
            }
        }
        var completed = await capture.CompleteOrdinaryReductionAsync(lease);
        if (!completed.Success)
            return LogSpiritualContinuationIssues(completed.Issues, "Source-only reduction did not complete.");
        var prepared = await capture.PrepareC4PublicationAsync(lease);
        if (prepared.Authority is null || prepared.Issues.Count != 0)
            return LogSpiritualContinuationIssues(prepared.Issues, "Source-only publication authority is unavailable.");
        if (!AcceptedMechanicsPlanAuthority.RegisterSpiritualPublication(_fs, lease, prepared.Authority))
            throw new InvalidDataException("Source-only publication registration was rejected.");
        return true;
    }

    /// <summary>
    /// Publishes and consumes bounded continuation requests without retaining execution authority during GM work.
    /// </summary>
    /// <param name="source">
    /// Accepted-turn caller label for the request.
    /// </param>
    /// <param name="generation">
    /// Original session generation checked under each fresh lease.
    /// </param>
    /// <param name="context">
    /// Original signed turn identity retained solely for comparison.
    /// </param>
    /// <param name="request">
    /// Initial owner-derived phase, correlation, offer and exact dependent permissions.
    /// </param>
    /// <param name="initialIssues">
    /// Actual dependent diagnostics; an ordinary offer has an empty list.
    /// </param>
    /// <returns>
    /// Admission after actual client consumption and exact transport cleanup, retained retry state for unresolved
    /// pending repair, or rejection on cancellation or lost authority.
    /// </returns>
    private async Task<SpiritualContinuationAdmission> WaitForSpiritualContinuationAsync(string source, string generation,
        ValidatedPendingTurnSnapshotContext context, SpiritualWoundContinuationRequest request,
        IReadOnlyList<ValidationIssue> initialIssues)
    {
        var issues = initialIssues;
        var attempt = 0;
        var firstSuccessorPublication = false;
        using var inputBlock = BeginAgentConsoleInputBlockFromCurrentSnapshot(PlayerSafeInputBlockedText);
        while (true)
        {
            byte[] requestBytes;
            bool hasReady;
            var created = DateTime.UtcNow.ToString("O");
            await using (var lease = await _fs.AcquireCanonicalWriteLeaseAsync())
            {
                ThrowIfRepairSessionReplaced(lease, generation);
                await RequireSpiritualOriginalRequestAsync(lease, context);
                var current = await _validator.ReadSpiritualWoundContinuationAsync(lease, request);
                if (firstSuccessorPublication)
                {
                    var transport = await TryReconcileDependentSpiritualTransportAsync(lease, context, current);
                    if (!transport.Allowed)
                        return SpiritualContinuationAdmission.RetryableHeld;
                    current = transport.Current;
                    issues = current.Issues;
                }
                RequireSameSpiritualContinuation(current.Request, request);
                var existing = await _fs.ReadFileBytesAsync(lease, ValidationRepairRequestPath);
                if (existing is not null)
                {
                    var existingRoot = ReadSpiritualTransportRoot(DecodeSpiritualTransport(existing));
                    RequireSpiritualTransportIdentity(existingRoot, context);
                    if (existingRoot[SpiritualWoundContinuationProtocol.EnvelopeName] is not { } envelope)
                        throw new InvalidDataException("An unrelated repair request cannot be replaced by continuation.");
                    RequireSameSpiritualContinuation(SpiritualWoundContinuationProtocol.ReadRequest(
                        JsonSerializer.SerializeToElement(envelope)), request);
                }
                var report = new ValidationRepairRequest
                {
                    SessionId = context.SessionId, RequestId = context.RequestId, TurnNumber = context.TurnNumber,
                    Source = source, DetectedAtUtc = created, RevalidationAttempt = ++attempt,
                    FullTurnResubmissionRequired = false, SpiritualWoundContinuation = request,
                    GmInstructions = "Продолжи исходный ход по spiritualWoundContinuation. Верни закрытый конверт через " +
                        "validation_repair_ready.json с исходными sessionId, requestId и turnNumber. Не повторяй ход, " +
                        "не меняй действия или кубики. Текст сцены остаётся в output/narrative_response.json.response. " +
                        "В dependent_draft исправляй только выданные JSON pointers и верни пустой woundDecisions. " +
                        "Ready завершает только текущий выданный набор исправлений. Клиент может выдать следующий " +
                        "dependent_draft: ответь отдельно с его новым continuationId, сохраняя уже выбранную рану.",
                    Errors = issues.Select(issue => new ValidationRepairIssue
                    {
                        Code = issue.Code ?? "spiritual_continuation_invalid", FilePath = issue.FilePath,
                        Severity = issue.Severity.ToString(), Category = issue.Category.ToString(),
                        Message = issue.Message, Actor = issue.Actor, Section = issue.Section,
                        Expected = issue.Expected, Actual = issue.Actual, RepairHint = issue.RepairHint
                    }).ToList()
                };
                requestBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(report, JsonOpts));
                if (!SameSpiritualBytes(existing, await _fs.ReadFileBytesAsync(lease, ValidationRepairRequestPath)))
                    throw new InvalidDataException("The spiritual repair request changed before publication.");
                await _fs.WriteFileAtomicBytesAsync(lease, ValidationRepairRequestPath, requestBytes);
                firstSuccessorPublication = false;
                hasReady = _fs.FileExists(lease, ValidationRepairReadyPath);
            }
            PublishAgentConsoleValidationRepairSnapshot();
            if (!hasReady)
            {
                var dispatched = await RunWorkerValidationRepairIfAvailableAsync(issues,
                    (context.SessionId, context.RequestId, context.TurnNumber), created, attempt, generation, request);
                if (dispatched.Outcome == GmWorkerValidationRepairOutcome.SessionReplaced)
                    throw new GmWorkerSessionReplacedException("The spiritual continuation session was replaced.");
                // Worker application without Ready never stands in for an explicit GM response.
            }

            while (true)
            {
                await EnsureRepairSessionCurrentAsync(generation);
                if (_inputSource.KeyAvailable && _inputSource.ReadKey(intercept: true).Key == ConsoleKey.Escape)
                {
                    await using var cancelledLease = await _fs.AcquireCanonicalWriteLeaseAsync();
                    ThrowIfRepairSessionReplaced(cancelledLease, generation);
                    await RequireSpiritualOriginalRequestAsync(cancelledLease, context);
                    await DeleteExactSpiritualTransportAsync(cancelledLease, ValidationRepairRequestPath, requestBytes);
                    return SpiritualContinuationAdmission.Rejected;
                }
                if (!_fs.FileExists(ValidationRepairReadyPath))
                {
                    await Task.Delay(100);
                    continue;
                }
                await using var lease = await _fs.AcquireCanonicalWriteLeaseAsync();
                ThrowIfRepairSessionReplaced(lease, generation);
                await RequireSpiritualOriginalRequestAsync(lease, context);
                if (!SameSpiritualBytes(requestBytes, await _fs.ReadFileBytesAsync(lease, ValidationRepairRequestPath)))
                    throw new InvalidDataException("The spiritual request was replaced while awaiting Ready.");
                var readyBytes = await _fs.ReadFileBytesAsync(lease, ValidationRepairReadyPath);
                if (readyBytes is null) continue;
                SpiritualWoundContinuationResponse? response = null;
                ValidationService.SpiritualWoundContinuationEvaluation? evaluation = null;
                ValidationService.SpiritualWoundDependentProgressCommitResult? committedProgress = null;
                try
                {
                    using var document = JsonDocument.Parse(DecodeSpiritualTransport(readyBytes));
                    ValidateSpiritualTransportOuterKeys(document.RootElement);
                    var root = JsonNode.Parse(document.RootElement.GetRawText())!.AsObject();
                    if (root["sessionId"] is JsonValue session && session.TryGetValue<string>(out var sessionId) &&
                        sessionId != context.SessionId)
                        return SpiritualContinuationAdmission.Rejected;
                    RequireSpiritualTransportIdentity(root, context);
                    response = SpiritualWoundContinuationProtocol.ReadResponse(
                        document.RootElement.GetProperty(SpiritualWoundContinuationProtocol.EnvelopeName));
                    // A Ready completes its exact issued frontier, which may expose a separate request.
                    if (request.Phase == "dependent_draft")
                    {
                        var processed = await _validator.EvaluateAndCommitSpiritualWoundDependentResponseAsync(
                            lease, request, response, expectedRequestBytes: requestBytes, expectedReadyBytes: readyBytes);
                        evaluation = processed.Evaluation;
                        committedProgress = processed.Progress;
                        if (evaluation is null)
                        {
                            LogSpiritualContinuationIssues(committedProgress.Issues, "The response witness remains held for recovery.");
                            return SpiritualContinuationAdmission.RetryableHeld;
                        }
                    }
                    else
                        evaluation = await _validator.EvaluateSpiritualWoundContinuationDraftAsync(lease, request, response);
                    issues = evaluation.Disposition == ValidationService.SpiritualWoundContinuationDisposition.Rejected
                        ? evaluation.Issues : [];
                }
                catch (Exception error) when (error is JsonException or InvalidOperationException or
                    InvalidDataException or KeyNotFoundException or FormatException)
                {
                    issues = [new ValidationIssue(ValidationRepairReadyPath, IssueSeverity.Error,
                        error.Message, code: "spiritual_continuation_ready_invalid")];
                }
                if (issues.Count != 0 || response is null)
                {
                    await DeleteExactSpiritualTransportAsync(lease, ValidationRepairReadyPath, readyBytes);
                    break;
                }
                if (evaluation?.Disposition == ValidationService.SpiritualWoundContinuationDisposition.Advanced)
                {
                    var committed = committedProgress!;
                    if (committed.Disposition != "committed" || committed.NextRequest is null)
                    {
                        LogSpiritualContinuationIssues(committed.Issues, "The completed frontier remains held for recovery.");
                        return SpiritualContinuationAdmission.RetryableHeld;
                    }
                    // Confirmed private progress activates B before exact old A transport cleanup.
                    // No saved wound decision or ordinary advancement is consumed here.
                    await DeleteExactSpiritualTransportAsync(lease, ValidationRepairReadyPath, readyBytes);
                    await DeleteExactSpiritualTransportAsync(lease, ValidationRepairRequestPath, requestBytes);
                    // Reuse only detached comparison data. The next publication loop releases this
                    // lease and freshly authenticates B instead of first rebuilding it in the caller.
                    request = committed.NextRequest;
                    issues = committed.Issues;
                    attempt = 0;
                    firstSuccessorPublication = true;
                    break;
                }
                var narrative = ReadSpiritualTransportRoot(await _fs.ReadFileAsync(lease, "output/narrative_response.json"));
                var scene = narrative["response"]?.GetValue<string>();
                var opened = await _validator.OpenC2PrivateSessionAsync(lease);
                using var owner = opened.Session;
                if (owner is null || (request.Phase == "decision" ? opened.Disposition != "offer" :
                        opened.Disposition != "dependent_continuation"))
                {
                    LogSpiritualContinuationIssues(opened.Issues, "The current response owner is unavailable.");
                    return SpiritualContinuationAdmission.Rejected;
                }
                var next = request.Phase == "decision"
                    ? await owner.SubmitDecisionAsync(lease, response.WoundDecisions.Single(), scene)
                    : await owner.ResumeDependentContinuationAsync(lease);
                owner.Dispose();
                var boundary = await ResolveSpiritualPrivateBoundaryAsync(lease, next);
                if (boundary != SpiritualContinuationAdmission.Admitted)
                    return boundary;
                await DeleteExactSpiritualTransportAsync(lease, ValidationRepairReadyPath, readyBytes);
                await DeleteExactSpiritualTransportAsync(lease, ValidationRepairRequestPath, requestBytes);
                return SpiritualContinuationAdmission.Admitted;
            }
        }
    }

    /// <summary>
    /// Clears only original-turn continuation transport after disposing every private owner.
    /// </summary>
    /// <param name="lease">
    /// Active lease covering identity checks and exact deletions.
    /// </param>
    /// <param name="context">
    /// Original signed turn that owns the transport artifacts.
    /// </param>
    /// <returns>
    /// A task completing after current original-turn artifacts are removed.
    /// </returns>
    private async Task CleanupObsoleteSpiritualTransportAsync(FileSystemManager.CanonicalWriteLease lease,
        ValidatedPendingTurnSnapshotContext context)
    {
        var removals = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var path in new[] { ValidationRepairReadyPath, ValidationRepairRequestPath })
        {
            var bytes = await _fs.ReadFileBytesAsync(lease, path);
            if (bytes is null) continue;
            var root = ReadSpiritualTransportRoot(DecodeSpiritualTransport(bytes));
            RequireSpiritualTransportIdentity(root, context);
            if (!root.ContainsKey(SpiritualWoundContinuationProtocol.EnvelopeName))
                throw new InvalidDataException("An unrelated repair artifact cannot be removed by spiritual continuation.");
            removals[path] = bytes;
        }
        foreach (var removal in removals)
            await DeleteExactSpiritualTransportAsync(lease, removal.Key, removal.Value);
    }

    /// <summary>
    /// Removes one transport file only while its complete captured bytes still match.
    /// </summary>
    /// <param name="lease">
    /// Active canonical lease covering the compare and deletion.
    /// </param>
    /// <param name="path">
    /// Exact request or Ready path.
    /// </param>
    /// <param name="expected">
    /// Complete bytes observed by this consumer.
    /// </param>
    /// <returns>
    /// A task completing after deletion, or throwing when another writer changed the file.
    /// </returns>
    private async Task DeleteExactSpiritualTransportAsync(FileSystemManager.CanonicalWriteLease lease,
        string path, byte[] expected)
    {
        if (!SameSpiritualBytes(expected, await _fs.ReadFileBytesAsync(lease, path)))
            throw new InvalidDataException("The spiritual transport changed before exact cleanup.");
        _fs.DeleteFile(lease, path);
    }

    /// <summary>
    /// Revalidates the signed snapshot and actual original request metadata inside a continuation lease.
    /// </summary>
    /// <param name="lease">
    /// Active lease held through physical input validation.
    /// </param>
    /// <param name="context">
    /// Original signed turn identity and payload hash.
    /// </param>
    /// <returns>
    /// A task completing only for the same physical original turn.
    /// </returns>
    private async Task RequireSpiritualOriginalRequestAsync(FileSystemManager.CanonicalWriteLease lease,
        ValidatedPendingTurnSnapshotContext context)
    {
        var current = (await ResolveActivePendingTurnSnapshotContextAsync()).Context ??
            throw new InvalidDataException("The original signed spiritual turn is unavailable.");
        EnsureSpiritualOriginalContext(current, context, context.TurnNumber);
        RequireSpiritualTransportIdentity(ReadSpiritualTransportRoot(
            await _fs.ReadFileAsync(lease, "input/turn_request.json")), context);
    }

    /// <summary>
    /// Compares freshly validated snapshot identity with the original accepted turn.
    /// </summary>
    /// <param name="current">
    /// Freshly validated snapshot.
    /// </param>
    /// <param name="expected">
    /// Retained comparison context, or <see langword="null"/> to compare only the required turn.
    /// </param>
    /// <param name="turn">
    /// Exact original turn number.
    /// </param>
    private static void EnsureSpiritualOriginalContext(ValidatedPendingTurnSnapshotContext current,
        ValidatedPendingTurnSnapshotContext? expected, int turn)
    {
        if (current.TurnNumber != turn || expected is not null &&
            (current.SessionId != expected.SessionId || current.RequestId != expected.RequestId ||
             current.Manifest.ManifestPayloadHash != expected.Manifest.ManifestPayloadHash))
            throw new InvalidDataException("The signed spiritual turn was replaced.");
    }

    /// <summary>
    /// Requires exact original identity without case-insensitive metadata coercion.
    /// </summary>
    /// <param name="root">
    /// Duplicate-free physical request or Ready root.
    /// </param>
    /// <param name="context">
    /// Original signed identity to compare.
    /// </param>
    private static void RequireSpiritualTransportIdentity(JsonObject root, ValidatedPendingTurnSnapshotContext context)
    {
        if (root["sessionId"]?.GetValue<string>() != context.SessionId ||
            root["requestId"]?.GetValue<string>() != context.RequestId ||
            root["turnNumber"]?.GetValue<int>() != context.TurnNumber)
            throw new InvalidDataException("The spiritual transport does not match the original turn.");
    }

    /// <summary>
    /// Compares the complete current phase, offer and field permissions with the issued request.
    /// </summary>
    /// <param name="current">
    /// Fresh owner-derived projection, or <see langword="null"/> when no continuation remains.
    /// </param>
    /// <param name="expected">
    /// Previously issued comparison envelope.
    /// </param>
    private static void RequireSameSpiritualContinuation(SpiritualWoundContinuationRequest? current,
        SpiritualWoundContinuationRequest expected)
    {
        if (current is null || JsonSerializer.Serialize(current) != JsonSerializer.Serialize(expected))
            throw new InvalidDataException("The spiritual continuation phase or exact offer changed.");
    }

    /// <summary>
    /// Detects an explicit continuation envelope without taking over ordinary malformed repair diagnostics.
    /// </summary>
    /// <param name="json">
    /// Physical transport JSON, or <see langword="null"/> when the file is absent.
    /// </param>
    /// <returns>
    /// <see langword="true"/> for an object containing an exact or case-aliased continuation property,
    /// including a <see langword="null"/> value;
    /// <see langword="false"/> for absent, malformed, nonobject or ordinary repair input.
    /// </returns>
    private static bool HasSpiritualContinuationEnvelope(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return false;
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind == JsonValueKind.Object &&
                document.RootElement.EnumerateObject().Any(property => string.Equals(
                    property.Name, SpiritualWoundContinuationProtocol.EnvelopeName, StringComparison.OrdinalIgnoreCase));
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// Rejects duplicate outer keys and case aliases before metadata or envelope conversion.
    /// </summary>
    /// <param name="root">
    /// Raw outer transport object.
    /// </param>
    private static void ValidateSpiritualTransportOuterKeys(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object) throw new JsonException("A transport object is required.");
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string[] exactNames = ["sessionId", "requestId", "turnNumber", SpiritualWoundContinuationProtocol.EnvelopeName];
        foreach (var property in root.EnumerateObject())
        {
            if (!names.Add(property.Name) || exactNames.Any(name =>
                    string.Equals(name, property.Name, StringComparison.OrdinalIgnoreCase) && name != property.Name))
                throw new JsonException("Duplicate or case-aliased transport key.");
        }
    }

    /// <summary>
    /// Parses a complete physical object before any permissive DTO conversion.
    /// </summary>
    /// <param name="json">
    /// Required JSON object text; missing input is rejected.
    /// </param>
    /// <returns>
    /// A detached duplicate-free root with exact transport metadata spelling.
    /// </returns>
    private static JsonObject ReadSpiritualTransportRoot(string? json)
    {
        if (json is null) throw new InvalidDataException("The spiritual transport input is missing.");
        using var document = JsonDocument.Parse(json);
        ValidateSpiritualTransportOuterKeys(document.RootElement);
        return SpiritualWoundDependentDraftPolicy.ReadStrictRoot(json);
    }

    /// <summary>
    /// Decodes exact physical UTF-8, allowing only its standard leading preamble.
    /// </summary>
    /// <param name="bytes">
    /// Original file bytes retained separately for exact comparison.
    /// </param>
    /// <returns>
    /// Strictly decoded JSON text.
    /// </returns>
    private static string DecodeSpiritualTransport(byte[] bytes) =>
        new UTF8Encoding(false, true).GetString(bytes.AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF })
            ? bytes.AsSpan(3) : bytes.AsSpan());

    /// <summary>
    /// Compares complete physical images, keeping absence distinct from an empty file.
    /// </summary>
    /// <param name="left">
    /// First physical image, or <see langword="null"/> for absence.
    /// </param>
    /// <param name="right">
    /// Second physical image, or <see langword="null"/> for absence.
    /// </param>
    /// <returns>
    /// <see langword="true"/> only for equal bytes or two absent images; otherwise, <see langword="false"/>.
    /// </returns>
    private static bool SameSpiritualBytes(byte[]? left, byte[]? right) =>
        left is null ? right is null : right is not null && left.AsSpan().SequenceEqual(right);

    /// <summary>
    /// Logs concrete owner diagnostics while preserving fail-closed outcomes lacking diagnostics.
    /// </summary>
    /// <param name="issues">
    /// Actual owner or transport diagnostics.
    /// </param>
    /// <param name="failure">
    /// Optional failure when no usable authority was returned, even if diagnostics are empty.
    /// </param>
    /// <returns>
    /// <see langword="true"/> only when there are no diagnostics and no explicit authority failure;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    private bool LogSpiritualContinuationIssues(IReadOnlyList<ValidationIssue> issues, string? failure = null)
    {
        foreach (var issue in issues)
            _logger.LogWarning("Spiritual continuation rejected: {Issue}; code: {Code}; actual: {Actual}",
                issue, issue.Code, issue.Actual);
        if (failure is not null)
            _logger.LogWarning("Spiritual continuation rejected: {Reason}", failure);
        return issues.Count == 0 && failure is null;
    }
}
