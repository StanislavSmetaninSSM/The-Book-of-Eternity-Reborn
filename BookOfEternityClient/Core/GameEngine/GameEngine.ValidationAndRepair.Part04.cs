using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using BookOfEternityClient.AgentConsole;
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

    private async Task<IReadOnlyList<WoundRepairRetryObligation>>
        CaptureWoundRepairRetryObligationsAsync(
            IReadOnlyList<WoundRepairPacket> packets)
    {
        if (packets.Any(packet => !WoundRepairPacketBuilder.IsSupportedLiveRepairPacketKind(packet.CandidateKind)))
            return Array.Empty<WoundRepairRetryObligation>();
        var (root, parsed) = await ReadStrictWoundRepairCommandAsync();
        if (root is null || parsed is null || !parsed.Success)
            return Array.Empty<WoundRepairRetryObligation>();

        var result = new List<WoundRepairRetryObligation>(packets.Count);
        foreach (var packet in packets)
        {
            var matchingIndexes = parsed.Commands
                .Select((command, index) => (command, index))
                .Where(value =>
                    packet.MatchesOpportunity(value.command.Opportunity) &&
                    TryReadWoundRepairDecision(
                        value.command.Decision,
                        out var decision) &&
                    packet.MatchesRejectedDecision(decision))
                .Select(static value => value.index)
                .ToArray();
            if (matchingIndexes.Length != 1 ||
                result.Any(obligation =>
                    obligation.CommandOrdinal == matchingIndexes[0]))
            {
                return Array.Empty<WoundRepairRetryObligation>();
            }
            result.Add(new WoundRepairRetryObligation(
                matchingIndexes[0],
                packet,
                root.DeepClone().AsObject()));
        }
        return result;
    }

    private async Task<bool> HasExactWoundRepairResubmissionAsync(
        IReadOnlyList<WoundRepairRetryObligation> obligations)
    {
        if (obligations.Count == 0)
            return true;
        if (obligations.Any(obligation => !WoundRepairPacketBuilder.IsSupportedLiveRepairPacketKind(obligation.Packet.CandidateKind)))
            return false;
        var (root, parsed) = await ReadStrictWoundRepairCommandAsync();
        if (root is null || parsed is null || !parsed.Success ||
            obligations.Any(obligation => !JsonNode.DeepEquals(
                obligation.ExpectedCommandRoot,
                obligations[0].ExpectedCommandRoot)))
        {
            return false;
        }

        var normalized = root.DeepClone().AsObject();
        var expected = obligations[0].ExpectedCommandRoot.DeepClone().AsObject();
        if (normalized["commands"] is not JsonArray normalizedCommands ||
            expected["commands"] is not JsonArray expectedCommands ||
            normalizedCommands.Count != expectedCommands.Count ||
            parsed.Commands.Count != normalizedCommands.Count)
        {
            return false;
        }

        foreach (var obligation in obligations)
        {
            if (!RootMatchesWoundRepairPacket(root, obligation.Packet) ||
                obligation.CommandOrdinal < 0 ||
                obligation.CommandOrdinal >= parsed.Commands.Count ||
                normalizedCommands[obligation.CommandOrdinal] is not JsonObject currentCommand ||
                expectedCommands[obligation.CommandOrdinal] is not JsonObject expectedCommand ||
                !obligation.Packet.MatchesOpportunity(
                    parsed.Commands[obligation.CommandOrdinal].Opportunity) ||
                !TryReadWoundRepairDecision(
                    parsed.Commands[obligation.CommandOrdinal].Decision,
                    out var decision) ||
                !obligation.Packet.MatchesCorrectedDecision(
                    decision,
                    parsed.Commands[obligation.CommandOrdinal].FinalSceneText))
            {
                return false;
            }

            currentCommand["decision"] = expectedCommand["decision"]?.DeepClone();
        }

        if (obligations.Any(static obligation =>
                obligation.Packet.RequiresResponseCorrection))
        {
            for (var index = 0; index < normalizedCommands.Count; index++)
            {
                if (normalizedCommands[index] is not JsonObject currentCommand ||
                    expectedCommands[index] is not JsonObject expectedCommand)
                {
                    return false;
                }
                currentCommand["finalSceneText"] =
                    expectedCommand["finalSceneText"]?.DeepClone();
            }
        }

        return JsonNode.DeepEquals(normalized, expected);
    }

    private async Task<(JsonObject? Root, WoundResponseCommandParsingResult? Parsed)>
        ReadStrictWoundRepairCommandAsync()
    {
        var json = await _fs.ReadFileAsync(AcceptedMechanicsPlan.WoundCommandPath);
        if (string.IsNullOrWhiteSpace(json))
            return (null, null);
        try
        {
            var root = StrictJsonAuthority.Deserialize<JsonObject>(
                json,
                JsonOpts,
                "wound repair command");
            if (root is null)
                return (null, null);
            var parsed = WoundResponseInputComposer.ParseCommandRoot(
                JsonSerializer.SerializeToElement(root, JsonOpts));
            return parsed.Success && parsed.AcceptedTransitionCommands.Count == 0
                ? (root, parsed) : (null, null);
        }
        catch (Exception exception) when (
            exception is JsonException or InvalidDataException or
                InvalidOperationException or NotSupportedException)
        {
            return (null, null);
        }
    }

    private static bool TryReadWoundRepairDecision(
        JsonElement element,
        out JsonObject decision)
    {
        decision = null!;
        try
        {
            decision = JsonNode.Parse(element.GetRawText())?.AsObject()!;
            return decision is not null;
        }
        catch (Exception exception) when (
            exception is JsonException or InvalidOperationException)
        {
            return false;
        }
    }

    private static ValidationIssue BuildWoundRepairRetryAuthorityIssue() => new(
        AcceptedMechanicsPlan.WoundCommandPath,
        IssueSeverity.Error,
        "Wound repair could not bind the rejected proposal to one exact retry authority.",
        code: "wound_repair_retry_authority_unavailable",
        actor: "Client",
        section: "wound_materialization",
        expected:
            "one strict rejected wound command bound to the exact sealed opportunity",
        actual: "missing, malformed, ambiguous, or changed before repair capture",
        category: IssueCategory.StateConsistency);

    private static bool RootMatchesWoundRepairPacket(
        JsonObject root,
        WoundRepairPacket packet) =>
        ReadExactWoundRepairRootString(root, "sessionId") == packet.SessionId &&
        ReadExactWoundRepairRootString(root, "requestId") == packet.RequestId &&
        ReadExactWoundRepairRootString(root, "snapshotToken") == packet.SnapshotToken;

    private static string? ReadExactWoundRepairRootString(
        JsonObject root,
        string property) =>
        root[property] is JsonValue value &&
        value.TryGetValue<string>(out var text) &&
        ResourceMaterializationContract.IsExactIdentifier(text)
            ? text
            : null;

    private static bool IsReadableResourceRepairReason(JsonNode? node) =>
        node is JsonValue value &&
        value.TryGetValue<string>(out var reason) &&
        ResourceMaterializationContract.IsExactIdentifier(reason) &&
        reason.Any(char.IsLetterOrDigit);

    private static bool TryParseEffectRepairActor(
        string actor,
        out int operationIndex)
    {
        operationIndex = -1;
        const string prefix = "effect-apply:effectChanges[";
        if (!actor.StartsWith(prefix, StringComparison.Ordinal) ||
            !actor.EndsWith(']'))
        {
            return false;
        }
        var token = actor[prefix.Length..^1];
        return int.TryParse(token, out operationIndex) &&
               operationIndex >= 0 &&
               string.Equals(token, operationIndex.ToString(), StringComparison.Ordinal);
    }

    private static bool TryReadEffectRepairParameter(
        string rawCoordinate,
        int operationIndex,
        out string parameterName)
    {
        parameterName = string.Empty;
        var prefix = $"effectChanges[{operationIndex}].parameters.";
        if (!rawCoordinate.StartsWith(prefix, StringComparison.Ordinal))
            return false;
        parameterName = rawCoordinate[prefix.Length..];
        return MortalItemIdentityRules.IsExactIdentity(parameterName) &&
               parameterName.IndexOfAny(new[] { '.', '[', ']' }) < 0;
    }

    private static bool TryParseEffectRepairExpectedValue(
        string valueJson,
        out JsonNode? value)
    {
        value = null;
        try
        {
            value = JsonNode.Parse(valueJson);
            return value is JsonValue;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string? ReadExactEffectRepairString(
        JsonObject source,
        string property)
    {
        return source[property] is JsonValue value &&
               value.TryGetValue<string>(out var text) &&
               MortalItemIdentityRules.IsExactIdentity(text)
            ? text
            : null;
    }

    private async Task<bool> ValidatePostAcceptedMaterializedStateWithRepairLoopAsync(
        RollbackSnapshot? rollbackSnapshot)
    {
        var spiritualValidation = Interlocked.Exchange(ref _acceptedTurnSpiritualConflictValidation, null);
        using var completedConflictScope = _validator.UseCompletedSpiritualConflictValidationScope(spiritualValidation);
        var accepted = await ValidateCurrentGameStateOrShowErrorsAsync(
            PostAcceptedMaterializedStateValidationSource,
            rollbackSnapshot,
            progressionControl: null,
            allowRepairLoop: true);

        if (accepted)
            await RefreshRuntimeStateAsync();

        return accepted;
    }

    private async Task<AcceptedTurnCanonicalRefreshResult>
        RefreshAcceptedTurnCanonicalStateForValidationAsync(
        int expectedTurn,
        ValidatedPendingTurnSnapshotContext? activeSnapshotContext)
    {
        var snapshot = await LoadCanonicalBaselineSnapshotAsync(expectedTurn, activeSnapshotContext);
        if (snapshot == null)
            return new AcceptedTurnCanonicalRefreshResult(false, Array.Empty<ValidationIssue>());

        var refresh = await RefreshCanonicalStateAsync(snapshot);
        if (refresh.TreatmentResourcePublicationTransaction is not null)
        {
            try
            {
                var postSealIssues = refresh.Issues.ToList();
                IReadOnlyList<WoundPlayerNotification> woundNotifications =
                    Array.Empty<WoundPlayerNotification>();
                if (refresh.MechanicsPlan?.WoundStageBundle is not null)
                {
                    postSealIssues.AddRange(
                        await ValidateAcceptedWoundPostPublicationAuthorityAsync(
                            refresh.MechanicsPlan,
                            activeSnapshotContext));
                    var output = await BindPublishedAcceptedWoundOutputAsync(
                        refresh.MechanicsPlan);
                    if (output.Issues.Any(static issue =>
                            !IsWoundNarrationRepairIssue(issue)))
                    {
                        throw new InvalidDataException(
                            "The accepted wound output does not agree with its finalized typed stages.");
                    }
                    postSealIssues.AddRange(output.Issues);
                    if (output.Success)
                        woundNotifications = output.Notifications;
                }
                return new AcceptedTurnCanonicalRefreshResult(
                    true,
                    postSealIssues,
                    refresh.MechanicsPlan,
                    refresh.TreatmentResourcePublicationTransaction,
                    woundNotifications);
            }
            catch (CompensatedTreatmentPublicationException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw await BuildCompensatedTreatmentPublicationExceptionAsync(
                    refresh.TreatmentResourcePublicationTransaction,
                    exception);
            }
        }

        var resourceFreePostSealIssues = refresh.Issues.ToList();
        IReadOnlyList<WoundPlayerNotification> resourceFreeWoundNotifications =
            refresh.SpiritualWoundOutput?.Notifications ?? Array.Empty<WoundPlayerNotification>();
        if (refresh.MechanicsPlan?.WoundStageBundle is not null)
        {
            resourceFreePostSealIssues.AddRange(
                await ValidateAcceptedWoundPostPublicationAuthorityAsync(
                    refresh.MechanicsPlan,
                    activeSnapshotContext));
            var resourceFreeOutput = await BindPublishedAcceptedWoundOutputAsync(
                refresh.MechanicsPlan);
            if (resourceFreeOutput.Issues.Any(static issue =>
                    !IsWoundNarrationRepairIssue(issue)))
            {
                throw new InvalidDataException(
                    "The accepted wound output does not agree with its finalized typed stages.");
            }
            resourceFreePostSealIssues.AddRange(resourceFreeOutput.Issues);
            if (resourceFreeOutput.Success)
                resourceFreeWoundNotifications = resourceFreeOutput.Notifications;
        }
        return new AcceptedTurnCanonicalRefreshResult(
            true,
            resourceFreePostSealIssues,
            refresh.MechanicsPlan,
            AcceptedWoundNotifications: resourceFreeWoundNotifications,
            SpiritualWoundOutput: refresh.SpiritualWoundOutput,
            SpiritualConflictValidation: refresh.SpiritualConflictValidation);
    }

    private async Task<WoundAcceptedTurnOutputBindingResult>
        BindPublishedAcceptedWoundOutputAsync(AcceptedMechanicsPlan plan) =>
        await BindPublishedAcceptedWoundOutputAsync(_fs, plan);

    internal static async Task<WoundAcceptedTurnOutputBindingResult>
        BindPublishedAcceptedWoundOutputAsync(
            FileSystemManager fileSystem,
            AcceptedMechanicsPlan plan)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(plan);
        var finalSceneText = string.Empty;
        try
        {
            var json = await fileSystem.ReadFileAsync(WoundNarrativeOutputPath);
            if (!string.IsNullOrWhiteSpace(json))
            {
                var root = StrictJsonAuthority.Deserialize<JsonObject>(
                    json,
                    SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed,
                    "accepted wound narrative output");
                if (root is { } acceptedRoot &&
                    acceptedRoot["response"] is JsonValue responseValue &&
                    responseValue.TryGetValue<string>(out var response))
                {
                    finalSceneText = response ?? string.Empty;
                }
            }
        }
        catch (Exception exception) when (
            exception is JsonException or InvalidDataException or
                InvalidOperationException or NotSupportedException)
        {
            finalSceneText = string.Empty;
        }

        return BindAcceptedWoundOutput(plan, finalSceneText);
    }

    private async Task<IReadOnlyList<ValidationIssue>>
        ValidateAcceptedWoundPostPublicationAuthorityAsync(
            AcceptedMechanicsPlan plan,
            ValidatedPendingTurnSnapshotContext? activeSnapshotContext)
    {
        var issues = new List<ValidationIssue>();
        var woundStages = plan.WoundStageBundle;
        if (woundStages == null)
            return issues;
        var snapshotContext = activeSnapshotContext;
        if (snapshotContext == null)
        {
            var resolution = await ResolveActivePendingTurnSnapshotContextAsync();
            snapshotContext = resolution.Context;
        }

        string? missingSnapshotPath = null;
        var requiredSnapshotPaths =
            WoundAcceptedTurnSnapshotContract.BuildRequiredPaths(
                plan.TouchedPaths);
        if (snapshotContext == null ||
            snapshotContext.TurnNumber != woundStages.Input.Binding.Turn ||
            !PendingTurnSnapshotAuthority.HasValidatedRollbackSnapshotCoverage(
                snapshotContext.Payload,
                static payload => payload.Files,
                static payload => payload.SnapshotFileHashes,
                static payload => payload.RollbackBaselineFiles,
                requiredSnapshotPaths,
                out missingSnapshotPath))
        {
            issues.Add(new ValidationIssue(
                missingSnapshotPath ?? AcceptedMechanicsPlan.WoundCommandPath,
                IssueSeverity.Error,
                "Accepted wound publication lacks exact signed rollback snapshot coverage.",
                code: "wound_materialization_snapshot_before_image_missing",
                section: "AcceptedTurnWoundMaterialization",
                expected: "signed present-or-absent rollback evidence for every wound, scheduler, and output authority path",
                actual: "snapshot context or one exact path registration is missing"));
            return issues;
        }

        issues.AddRange(await WoundAcceptedTurnSnapshotContract
            .ValidatePublishedOutputAuthorityAsync(
                plan,
                path => _fs.ReadFileBytesAsync(path)));

        return issues;
    }

    /// <summary>
    /// Carries accepted refresh diagnostics and publication handoffs into the caller's remaining validation and response steps.
    /// </summary>
    /// <param name="BaselineUsable">
    /// <see langword="true"/> when the signed baseline was available; otherwise <see langword="false"/>.
    /// </param>
    /// <param name="PostSealIssues">
    /// Diagnostics that must be resolved before accepting the refreshed state.
    /// </param>
    /// <param name="MechanicsPlan">
    /// Accepted mechanics plan, or <see langword="null"/> if none was accepted.
    /// </param>
    /// <param name="TreatmentResourcePublicationTransaction">
    /// Treatment transaction requiring settlement, or <see langword="null"/> on other paths.
    /// </param>
    /// <param name="AcceptedWoundNotifications">
    /// Detached ordered presentation; <see langword="null"/> is exposed as an empty notification list.
    /// </param>
    /// <param name="SpiritualWoundOutput">
    /// Detached spiritual output witnesses for response construction, or <see langword="null"/> without successful spiritual publication.
    /// </param>
    /// <param name="SpiritualConflictValidation">
    /// Published causal comparisons for both remaining validation passes, or <see langword="null"/> on ordinary paths.
    /// </param>
    private sealed record AcceptedTurnCanonicalRefreshResult(
        bool BaselineUsable,
        IReadOnlyList<ValidationIssue> PostSealIssues,
        AcceptedMechanicsPlan? MechanicsPlan = null,
        MortalWoundTreatmentResourcePublicationTransaction?
            TreatmentResourcePublicationTransaction = null,
        IReadOnlyList<WoundPlayerNotification>? AcceptedWoundNotifications = null,
        SpiritualWoundPublishedOutput? SpiritualWoundOutput = null,
        ValidationService.SpiritualOriginalTurnCapture.SpiritualCompletedConflictValidation?
            SpiritualConflictValidation = null)
    {
        /// <summary>
        /// Gets the ordered accepted notifications, or an empty list when no presentation was supplied.
        /// </summary>
        internal IReadOnlyList<WoundPlayerNotification> WoundNotifications { get; } =
            AcceptedWoundNotifications ?? Array.Empty<WoundPlayerNotification>();
    }

    private static ValidationIssue BuildBoundedResourceResolutionResubmissionIssue(
        AcceptedMechanicsPlan plan)
    {
        var packet = plan.PendingGmPacket ??
            throw new InvalidOperationException(
                "Pending accepted mechanics plan has no safe GM packet.");
        return new ValidationIssue(
            EffectAcceptedTurnPlan.CommandPath,
            IssueSeverity.Error,
            "A bounded effect/resource result requires one complete same-turn resubmission before any mechanics or player-facing output can be accepted.",
            code: "resource_pending_full_turn_resubmission_required",
            section: "AcceptedMechanicsPendingResolution",
            expected: "effectResolutionReceipts[] containing exactly one closed receipt for every safe request, followed by complete validation of the same accepted turn",
            actual: packet.ToJsonString(),
            repairHint:
                "Не читай и не изменяй технический pending-файл. Сохрани тот же sessionId/requestId/turn и все уже авторизованные изменения хода. " +
                "В game_state/effects/effect_commands.json верни полный корень с прежними effectChanges[], effectEventReports[] и effectResolutionReceipts[]. " +
                "Для каждого requestId из safe packet выбери ровно один resultKind: narrated_no_state_change без amount либо resource_delta с amount в указанном диапазоне; всегда добавь краткую внутриигровую reason. " +
                "Answer the current safe packet only. Resubmit the same complete semantic turn with receipts only for that packet; the client carries earlier-wave terminal bindings. The original candidate, mutation authority, and source authority remain immutable/client-owned. " +
                "Не указывай target/resource/operation/realm/координаты и не пытайся писать resource_state, resource_history или active effect carriers. " +
                "Обнови narrative/interface/debug output под выбранный исход, затем заверши текущий validation-repair без нового хода.",
            repairTargetFiles: new[]
            {
                EffectAcceptedTurnPlan.CommandPath,
                "output/narrative_response.json",
                "output/interface_updates.json",
                "output/debug_logs.json"
            });
    }

    private async Task FailClosedAcceptedTurnCanonicalRefreshAsync(
        string source,
        Exception exception,
        RollbackSnapshot? rollbackSnapshot)
    {
        var sessionGeneration = await CaptureCurrentSessionGenerationAsync();
        var writeFailure = FindCanonicalStateWriteException(exception);
        var filePath = writeFailure?.RelativePath ?? "accepted_turn_canonical_state";
        _logger.LogError(
            exception,
            "Accepted-turn canonical refresh failed after {Source} at {FilePath}; leaving full rollback to the caller.",
            source,
            filePath);

        var report = new
        {
            source,
            detectedAtUtc = DateTime.UtcNow.ToString("o"),
            reason = "Accepted-turn canonical refresh failed during an atomic state write.",
            rollbackAvailable = HasRollbackCapability(rollbackSnapshot),
            errors = new[]
            {
                new
                {
                    code = "accepted_turn_canonical_refresh_failed",
                    filePath,
                    exceptionType = exception.GetType().FullName,
                    message = exception.Message,
                    details = exception.ToString()
                }
            }
        };

        await RunBestEffortFailClosedBookkeepingAsync(
            "write the accepted-turn canonical refresh diagnostic report",
            () => WriteValidationRepairFileForSessionAsync(
                ValidationDiagnosticFailureReportPath,
                JsonSerializer.Serialize(report, JsonOpts),
                sessionGeneration));
        await RunBestEffortFailClosedBookkeepingAsync(
            "clean validation-repair control files after canonical refresh failure",
            () => DeleteValidationRepairFilesForSessionAsync(sessionGeneration));
        AnsiConsole.MarkupLine(
            HasRollbackCapability(rollbackSnapshot)
                ? "[yellow]↩ Изменения мира не были приняты; состояние до хода будет восстановлено.[/]"
                : "[yellow]⚠ Изменения мира не были приняты. Продолжение этого хода остановлено.[/]");
    }

    private async Task RunBestEffortFailClosedBookkeepingAsync(
        string operation,
        Func<Task> operationAsync)
    {
        try
        {
            await operationAsync();
        }
        catch (SessionReplacedException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to {Operation}; caller-owned rollback remains mandatory.",
                operation);
        }
    }

    private static CanonicalStateWriteException? FindCanonicalStateWriteException(
        Exception exception)
    {
        if (exception is CanonicalStateWriteException writeFailure)
            return writeFailure;

        if (exception is AggregateException aggregate)
        {
            foreach (var inner in aggregate.InnerExceptions)
            {
                var match = FindCanonicalStateWriteException(inner);
                if (match != null)
                    return match;
            }
        }

        return exception.InnerException == null
            ? null
            : FindCanonicalStateWriteException(exception.InnerException);
    }

    private async Task CleanupAcceptedTurnCommandSurfacesAsync(string? expectedSessionGeneration = null)
    {
        await RemoveGuardianQuestProgressUpdatesCommandSurfaceAsync(expectedSessionGeneration);
    }

    private async Task RemoveGuardianQuestProgressUpdatesCommandSurfaceAsync(
        string? expectedSessionGeneration = null)
    {
        const string path = "game_state/meta/guardians.json";
        await using var writeLease = await _fs.AcquireCanonicalWriteLeaseAsync();
        if (!string.IsNullOrWhiteSpace(expectedSessionGeneration))
            ThrowIfRepairSessionReplaced(writeLease, expectedSessionGeneration);

        var json = await _fs.ReadFileAsync(writeLease, path);
        if (string.IsNullOrWhiteSpace(json))
            return;

        try
        {
            if (JsonNode.Parse(json) is not JsonObject root ||
                !root.Remove(GuardianProjectState.QuestProgressUpdatesProperty))
            {
                return;
            }

            await _fs.WriteFileAtomicAsync(writeLease, path, root.ToJsonString(JsonOpts));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to remove accepted-turn Guardian quest progress command surface.");
            throw;
        }
    }

    private static bool RequiresFreshNarrativePayload(string source)
    {
        return source is "ответа GM" or "late response GM" or "обработки хода" or "оценки жизни";
    }
}
