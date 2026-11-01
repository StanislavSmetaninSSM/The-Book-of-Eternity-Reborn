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

    private async Task<AcceptedTurnValidationDisposition?>
        TryQuarantineFinalizedTreatmentReplayAsync(int expectedTurn)
    {
        // A durable retry may survive process restart after its history receipt was
        // finalized but before the command was quarantined. Recognize only the
        // sealed, history-owned replay here; all incomplete or conflicting input
        // continues through the ordinary admission and repair path below.
        await using var writeLease = await _fs.AcquireCanonicalWriteLeaseAsync();
        var commandBytes = await _fs.ReadFileBytesAsync(
            writeLease,
            AcceptedMechanicsPlan.WoundCommandPath);
        if (commandBytes is null)
            return null;

        var commandJson = Encoding.UTF8.GetString(commandBytes).TrimStart('\uFEFF');
        JsonObject command;
        WoundResponseCommandParsingResult parsedCommand;
        try
        {
            command = JsonNode.Parse(commandJson)?.AsObject() ??
                      throw new JsonException("Treatment command root is not an object.");
            parsedCommand = WoundResponseInputComposer.ParseCommandRoot(
                JsonSerializer.SerializeToElement(command));
        }
        catch (Exception exception) when (exception is JsonException or
                                           InvalidOperationException)
        {
            return null;
        }
        if (!parsedCommand.Success ||
            parsedCommand.Commands.Count != 0 ||
            parsedCommand.AcceptedTransitionCommands.Count != 0 ||
            parsedCommand.TreatmentCommands.Count == 0)
        {
            return null;
        }

        string? finalizedSceneText;
        try
        {
            var narrativeJson = await _fs.ReadFileAsync(
                writeLease,
                WoundNarrativeOutputPath);
            if (string.IsNullOrWhiteSpace(narrativeJson))
                return null;
            var narrative = StrictJsonAuthority.Deserialize<JsonObject>(
                narrativeJson,
                SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed,
                "finalized treatment replay narrative output");
            finalizedSceneText = narrative?["response"] is JsonValue response &&
                                 response.TryGetValue<string>(out var text)
                ? text
                : null;
        }
        catch (Exception exception) when (
            exception is JsonException or InvalidDataException or
                InvalidOperationException or NotSupportedException)
        {
            return null;
        }
        if (string.IsNullOrWhiteSpace(finalizedSceneText) ||
            parsedCommand.TreatmentCommands.Any(draft => !string.Equals(
                draft.FinalSceneText,
                finalizedSceneText,
                StringComparison.Ordinal)))
        {
            return null;
        }

        var historyJson = await _fs.ReadFileAsync(
            writeLease,
            WoundHistoryState.HistoryPath);
        var history = WoundHistoryState.Parse(
            historyJson,
            WoundHistoryState.HistoryPath);
        if (!history.IsValid)
            return null;

        var pendingBytes = await _fs.ReadFileBytesAsync(
            writeLease,
            WoundAcceptedTurnSnapshotContract.PendingResolutionPath);
        var pendingJson = pendingBytes is null
            ? null
            : Encoding.UTF8.GetString(pendingBytes).TrimStart('\uFEFF');
        JsonObject? pending;
        MortalWoundTreatmentPersistedRequestCatalogResult catalog;
        try
        {
            pending = pendingJson is null
                ? null
                : JsonNode.Parse(pendingJson)?.AsObject() ??
                  throw new JsonException("Treatment pending root is not an object.");
            using var commandDocument = JsonDocument.Parse(commandJson);
            if (pendingJson is null)
            {
                catalog = MortalWoundTreatmentPersistedRequestCatalog.Parse(
                    commandDocument.RootElement,
                    pendingRoot: null,
                    history);
            }
            else
            {
                using var pendingDocument = JsonDocument.Parse(pendingJson);
                catalog = MortalWoundTreatmentPersistedRequestCatalog.Parse(
                    commandDocument.RootElement,
                    pendingDocument.RootElement,
                    history);
            }
        }
        catch (Exception exception) when (exception is JsonException or
                                           InvalidOperationException)
        {
            return null;
        }
        if (!catalog.IsValid || catalog.HeldRequests.Count != 0)
            return null;

        var finalized = new List<MortalWoundTreatmentAttemptRequest>();
        foreach (var draft in parsedCommand.TreatmentCommands)
        {
            var matches = catalog.FinalizedRequests.Where(request =>
                    request.Coordinates.Turn == expectedTurn &&
                    string.Equals(
                        request.Coordinates.OperationKey,
                        draft.OperationKey,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        request.Coordinates.AttemptId,
                        draft.AttemptId,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        request.RequestFingerprint,
                        draft.RequestFingerprint,
                        StringComparison.Ordinal))
                .ToArray();
            if (matches.Length != 1)
                return null;
            var request = matches[0];
            var replay = request.Mode switch
            {
                "procedure" => MortalWoundTreatmentPlanner.CreateProcedureAttempt(
                    request,
                    history,
                    before: null,
                    acceptedState: null),
                "course" => MortalWoundTreatmentPlanner
                    .CreateCourseMilestoneAttempt(
                        request,
                        history,
                        before: null,
                        acceptedState: null),
                "guaranteed" => MortalWoundTreatmentPlanner
                    .CreateGuaranteedAttempt(
                        request,
                        history,
                        before: null,
                        acceptedState: null),
                _ => null
            };
            if (replay is null ||
                !string.Equals(
                    replay.Disposition,
                    "ExactReplay",
                    StringComparison.Ordinal) ||
                replay.ReplayReceipt is null ||
                draft.Command["result"] is not JsonObject submittedResult ||
                !JsonNode.DeepEquals(
                    submittedResult,
                    WoundResponseInputComposer
                        .ComposeMortalWoundTreatmentPersistedResult(
                            request,
                            replay.ReplayReceipt)))
            {
                return null;
            }
            finalized.Add(request);
        }
        if (finalized.Select(static request => request.RequestFingerprint)
                .Distinct(StringComparer.Ordinal)
                .Count() != finalized.Count)
        {
            return null;
        }

        foreach (var request in finalized)
        {
            var quarantine = MortalWoundTreatmentDurableSurfaceQuarantine
                .RemoveExactRequestRows(
                    command,
                    pending,
                    request.Coordinates.OperationKey,
                    request.Coordinates.AttemptId,
                    request.RequestFingerprint);
            if (!quarantine.CommandChanged && !quarantine.PendingChanged)
                return null;
        }
        if (finalized.Any(request =>
                MortalWoundTreatmentDurableSurfaceQuarantine.ContainsExactRequestRows(
                    command,
                    pending,
                    request.Coordinates.OperationKey,
                    request.Coordinates.AttemptId,
                    request.RequestFingerprint)))
        {
            return null;
        }

        var writes = new List<CoordinatedStateWriteHelper.PlannedWrite>
        {
            new(
                AcceptedMechanicsPlan.WoundCommandPath,
                commandJson,
                NextJson: null,
                RequireCurrentBaseline: true,
                ExactPrevious: new CanonicalBeforeImage(true, commandBytes))
        };
        if (pendingBytes is not null && pending is not null &&
            !string.Equals(
                pendingJson,
                pending.ToJsonString(
                    SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed),
                StringComparison.Ordinal))
        {
            writes.Add(new CoordinatedStateWriteHelper.PlannedWrite(
                WoundAcceptedTurnSnapshotContract.PendingResolutionPath,
                pendingJson,
                pending.ToJsonString(
                    SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed),
                RequireCurrentBaseline: true,
                ExactPrevious: new CanonicalBeforeImage(true, pendingBytes)));
        }

        var committed = await CoordinatedStateWriteHelper.TryCommitAsync(
            _fs,
            writeLease,
            writes.ToArray());
        return committed
            ? null
            : AcceptedTurnValidationDisposition.TerminalRejected;
    }

    private async Task<bool>
        CompensateTreatmentResourcePublicationBeforeRepairAsync(
            MortalWoundTreatmentResourcePublicationTransaction? transaction)
    {
        if (transaction is null)
            return false;

        var result = await transaction.CompensateAsync(_fs);
        EnsureExactTreatmentResourcePublicationSettlement(
            result,
            MortalWoundTreatmentPublicationTransactionOutcome.Rearmed,
            "prepare accepted-turn repair");
        return true;
    }

    private async Task
        ReleaseTreatmentResourcePublicationBeforeTerminalRollbackAsync(
            MortalWoundTreatmentResourcePublicationTransaction? transaction,
            RollbackSnapshot? rollbackSnapshot,
            string reason)
    {
        if (transaction is null || !HasRollbackCapability(rollbackSnapshot))
            return;

        var result = await transaction.ReleaseTerminalAsync(_fs, reason);
        EnsureExactTreatmentResourcePublicationSettlement(
            result,
            MortalWoundTreatmentPublicationTransactionOutcome.Released,
            "settle terminal accepted-turn rollback");
    }

    private async Task<AcceptedTurnValidationDisposition>
        SettlePreCanonicalTreatmentPublicationBeforeTerminalRollbackAsync(
            RollbackSnapshot? rollbackSnapshot,
            string reason)
    {
        if (!HasRollbackCapability(rollbackSnapshot))
            return AcceptedTurnValidationDisposition.TerminalRejected;

        var result = await AcceptedTurnCanonicalStateRefresh
            .ReleaseValidatedTreatmentPublicationBeforeCanonicalRefreshAsync(
                _fs,
                reason);
        if (result is null)
            return AcceptedTurnValidationDisposition.TerminalRejected;

        if (IsExactSafeTreatmentResourcePublicationReleaseFailure(result))
        {
            _logger.LogError(
                "Pre-canonical terminal treatment cleanup restored the exact durable request and retained its ConfirmedHeld authority behind a restart blocker.");
            return AcceptedTurnValidationDisposition
                .RetryablePublicationHeldBlocked;
        }

        EnsureExactTreatmentResourcePublicationSettlement(
            result,
            MortalWoundTreatmentPublicationTransactionOutcome.Released,
            "settle pre-canonical terminal accepted-turn rollback");
        return AcceptedTurnValidationDisposition.TerminalRejected;
    }

    private static bool IsExactSafeTreatmentResourcePublicationReleaseFailure(
        MortalWoundTreatmentPublicationOperationResult result) =>
        MortalWoundTreatmentResourcePublicationTransaction
            .IsExactProvenTerminalReleaseFailure(result);

    private async Task<MortalWoundTreatmentPublicationTransactionOutcome>
        SettleTreatmentResourcePublicationCompletionFailureAsync(
        MortalWoundTreatmentResourcePublicationTransaction transaction)
    {
        var result = await transaction.CompensateAsync(_fs);
        if (result.IsValid &&
            result.Issues.Count == 0 &&
            result.ChangedCount == 1 &&
            result.Outcome is
                MortalWoundTreatmentPublicationTransactionOutcome.Rearmed or
                MortalWoundTreatmentPublicationTransactionOutcome.HeldBlocked or
                MortalWoundTreatmentPublicationTransactionOutcome.CommandQuarantined)
        {
            return result.Outcome;
        }

        var issueCoordinates = result.Issues.Count == 0
            ? "none"
            : string.Join(
                ", ",
                result.Issues.Select(static issue => issue.Code ?? issue.FilePath));
        throw new InvalidOperationException(
            "Treatment resource publication completion failure could not be " +
            $"safely compensated: outcome={result.Outcome}, " +
            $"changed={result.ChangedCount}, valid={result.IsValid}, " +
            $"issues={issueCoordinates}.");
    }

    private async Task<CompensatedTreatmentPublicationException>
        BuildCompensatedTreatmentPublicationExceptionAsync(
            MortalWoundTreatmentResourcePublicationTransaction transaction,
            Exception originalException)
    {
        try
        {
            var outcome =
                await SettleTreatmentResourcePublicationCompletionFailureAsync(
                    transaction);
            return new CompensatedTreatmentPublicationException(
                originalException,
                outcome);
        }
        catch (Exception settlementException)
        {
            throw new UnsafeTreatmentPublicationSettlementException(
                originalException,
                settlementException);
        }
    }

    private static AcceptedTurnValidationDisposition
        MapTreatmentPublicationSettlementToDisposition(
            MortalWoundTreatmentPublicationTransactionOutcome outcome) =>
        outcome switch
        {
            MortalWoundTreatmentPublicationTransactionOutcome.Rearmed =>
                AcceptedTurnValidationDisposition.RetryablePublicationRearmed,
            MortalWoundTreatmentPublicationTransactionOutcome.HeldBlocked =>
                AcceptedTurnValidationDisposition.RetryablePublicationHeldBlocked,
            MortalWoundTreatmentPublicationTransactionOutcome.CommandQuarantined =>
                AcceptedTurnValidationDisposition.TerminalRejected,
            _ => throw new InvalidOperationException(
                $"Treatment publication settlement outcome '{outcome}' cannot drive accepted-turn lifecycle disposition.")
        };

    private static bool IsExactTreatmentResourcePublicationSettlement(
        MortalWoundTreatmentPublicationOperationResult result,
        MortalWoundTreatmentPublicationTransactionOutcome expectedOutcome) =>
        result.IsValid &&
        result.Issues.Count == 0 &&
        result.ChangedCount == 1 &&
        result.Outcome == expectedOutcome;

    private static void EnsureExactTreatmentResourcePublicationSettlement(
        MortalWoundTreatmentPublicationOperationResult result,
        MortalWoundTreatmentPublicationTransactionOutcome expectedOutcome,
        string operation)
    {
        if (IsExactTreatmentResourcePublicationSettlement(
                result,
                expectedOutcome))
        {
            return;
        }

        var issueCoordinates = result.Issues.Count == 0
            ? "none"
            : string.Join(
                ", ",
                result.Issues.Select(static issue =>
                    issue.Code ?? issue.FilePath));
        throw new InvalidOperationException(
            $"Treatment resource publication could not {operation}: " +
            $"outcome={result.Outcome}, changed={result.ChangedCount}, " +
            $"valid={result.IsValid}, issues={issueCoordinates}.");
    }

    private static AcceptedTurnValidationDisposition
        CompleteAcceptedTurnWithoutTreatmentResourcePublication() =>
        AcceptedTurnValidationDisposition.Accepted;

    /// <summary>
    /// Collects raw-state diagnostics for callers without a continuation gate, stopping on private spiritual authority rejection.
    /// </summary>
    /// <returns>
    /// Upstream diagnostics followed by resource diagnostics only when private spiritual authority permits admission.
    /// </returns>
    private async Task<List<ValidationIssue>> CollectAcceptedTurnRawStateIssuesAsync()
    {
        var upstreamValidation = await CollectAcceptedTurnUpstreamRawStateIssuesAsync();
        var issues = upstreamValidation.Issues.ToList();
        if (!upstreamValidation.RejectSpiritualBoundary)
            issues.AddRange(await _validator.ValidateAcceptedTurnRawResourceMaterializationAsync());
        return issues;
    }

    /// <summary>
    /// Collects upstream raw-state issues before spiritual continuation and resource admission.
    /// </summary>
    /// <returns>
    /// Critical, NPC, faction, location and item diagnostics in the existing admission order,
    /// together with an explicit terminal rejection when private spiritual authority fails admission.
    /// </returns>
    private async Task<UpstreamAcceptedTurnValidation> CollectAcceptedTurnUpstreamRawStateIssuesAsync()
    {
        var issues = await _criticalStateHealth.ValidateAcceptedTurnRawStateAsync();
        issues.AddRange(await _validator.ValidateNpcCoreChangesBeforeNormalizationAsync());
        issues.AddRange(await _validator.ValidateFactionCoreChangesBeforeNormalizationAsync());
        issues.AddRange(await _validator.ValidateAcceptedTurnRawFactionMaterializationAsync());
        issues.AddRange(await _validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync());
        var itemValidation = await ValidateAcceptedTurnUpstreamRawItemsAsync();
        issues.AddRange(itemValidation.Issues);
        return new UpstreamAcceptedTurnValidation(issues, itemValidation.RejectSpiritualBoundary);
    }

    private async Task<bool> HasExactMortalLocationRepairResubmissionAsync(
        IReadOnlyList<MortalLocationRepairRetryObligation> obligations)
    {
        var currentRoot = await ReadMortalLocationRepairRootAsync(
            MortalLocationMaterializationContract.CurrentLocationPath);
        var mapRoot = await ReadMortalLocationRepairRootAsync(
            MortalLocationMaterializationContract.WorldMapPath);

        foreach (var obligation in obligations)
        {
            if (!TryParseMortalLocationRepairActor(
                    obligation.Actor,
                    out var entityKind,
                    out var isCreation,
                    out var identity))
            {
                return false;
            }

            var candidates = EnumerateMortalLocationRepairRouteCandidates(
                obligation.Route,
                currentRoot,
                mapRoot);
            var identityField = isCreation
                ? "initialId"
                : string.Equals(entityKind, "mortal_location", StringComparison.Ordinal)
                    ? "locationId"
                    : "linkId";
            var exactMatches = candidates.Count(candidate =>
            {
                if (!string.Equals(
                        ReadExactMortalLocationRepairString(candidate, identityField),
                        identity,
                        StringComparison.Ordinal))
                {
                    return false;
                }

                if (!isCreation)
                    return true;

                return candidate["materialization"] is JsonObject materialization &&
                       string.Equals(
                           ReadExactMortalLocationRepairString(materialization, "materializationId"),
                           obligation.MaterializationId,
                           StringComparison.Ordinal);
            });
            if (exactMatches != 1)
                return false;
        }

        return true;
    }

    private async Task<JsonObject?> ReadMortalLocationRepairRootAsync(string path)
    {
        var json = await _fs.ReadFileAsync(path);
        if (string.IsNullOrWhiteSpace(json))
            return null;
        try
        {
            return JsonNode.Parse(json) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static IEnumerable<JsonObject> EnumerateMortalLocationRepairRouteCandidates(
        string route,
        JsonObject? currentRoot,
        JsonObject? mapRoot)
    {
        if (route is "current_scene_creation" or "current_selection")
        {
            if (currentRoot?["currentLocationData"] is JsonObject current)
                yield return current;
            yield break;
        }

        var collection = route switch
        {
            "world_map_creation" => "newLocations",
            "world_map_link_creation" => "newLinks",
            "narrow_location_update" => "locationUpdates",
            "location_discovery_transition" => "locationDiscoveryTransitions",
            "link_update" => "linkUpdates",
            "link_removal" => "linkRemovals",
            _ => null
        };
        if (collection == null ||
            mapRoot?["worldMapUpdates"] is not JsonObject updates ||
            updates[collection] is not JsonArray values)
        {
            yield break;
        }

        foreach (var candidate in values.OfType<JsonObject>())
            yield return candidate;
    }

    private static bool TryParseMortalLocationRepairActor(
        string actor,
        out string entityKind,
        out bool isCreation,
        out string identity)
    {
        foreach (var descriptor in new[]
                 {
                     (Prefix: "mortal_location:new:", EntityKind: "mortal_location", IsCreation: true),
                     (Prefix: "mortal_location:existing:", EntityKind: "mortal_location", IsCreation: false),
                     (Prefix: "mortal_location_link:new:", EntityKind: "mortal_location_link", IsCreation: true),
                     (Prefix: "mortal_location_link:existing:", EntityKind: "mortal_location_link", IsCreation: false)
                 })
        {
            if (!actor.StartsWith(descriptor.Prefix, StringComparison.Ordinal))
                continue;

            identity = actor[descriptor.Prefix.Length..];
            entityKind = descriptor.EntityKind;
            isCreation = descriptor.IsCreation;
            return MortalItemIdentityRules.IsExactIdentity(identity);
        }

        entityKind = string.Empty;
        isCreation = false;
        identity = string.Empty;
        return false;
    }

    private static string? ReadExactMortalLocationRepairString(
        JsonObject source,
        string property)
    {
        return source[property] is JsonValue value &&
               value.TryGetValue<string>(out var text) &&
               MortalItemIdentityRules.IsExactIdentity(text)
            ? text
            : null;
    }

    private async Task<bool> HasExactEffectRepairResubmissionAsync(
        IReadOnlyList<EffectRepairRetryObligation> obligations)
    {
        var json = await _fs.ReadFileAsync(EffectAcceptedTurnPlan.CommandPath);
        if (string.IsNullOrWhiteSpace(json))
            return false;
        try
        {
            var root = JsonNode.Parse(json) as JsonObject ?? throw new JsonException();
            if (root["effectChanges"] is not JsonArray changes)
                return false;

            foreach (var obligation in obligations)
            {
                if (!TryParseEffectRepairActor(
                        obligation.Actor,
                        out var operationIndex) ||
                    operationIndex >= changes.Count ||
                    changes[operationIndex] is not JsonObject operation ||
                    !string.Equals(
                        ReadExactEffectRepairString(operation, "operation"),
                        "apply",
                        StringComparison.Ordinal) ||
                    operation["source"] is not JsonObject source ||
                    operation["target"] is not JsonObject target ||
                    operation["eventRef"] is not JsonObject eventRef ||
                    !JsonNode.DeepEquals(source, obligation.ExpectedSource) ||
                    !JsonNode.DeepEquals(target, obligation.ExpectedTarget) ||
                    !JsonNode.DeepEquals(eventRef, obligation.ExpectedEventRef) ||
                    !string.Equals(
                        ReadExactEffectRepairString(source, "definitionKey"),
                        obligation.ExpectedDefinitionKey,
                        StringComparison.Ordinal) ||
                    operation["parameters"] is not JsonObject parameters ||
                    !TryReadEffectRepairParameter(
                        obligation.RawCoordinate,
                        operationIndex,
                        out var parameterName) ||
                    !TryParseEffectRepairExpectedValue(
                        obligation.ExpectedValueJson,
                        out var expectedValue) ||
                    !JsonNode.DeepEquals(parameters[parameterName], expectedValue))
                {
                    return false;
                }
            }

            return true;
        }
        catch (Exception exception) when (
            exception is JsonException or ArgumentException or InvalidOperationException)
        {
            return false;
        }
    }

    private async Task<IReadOnlyList<ResourceRepairRetryObligation>>
        CaptureResourceRepairRetryObligationsAsync(
            IReadOnlyList<ResourceRepairPacket> packets)
    {
        var root = await ReadStrictResourceRepairCommandRootAsync();
        if (root?["resourceChanges"] is not JsonArray changes)
            return Array.Empty<ResourceRepairRetryObligation>();

        var obligations = new List<ResourceRepairRetryObligation>();
        foreach (var packet in packets.OrderBy(packet => packet.CommandOrdinal))
        {
            if (packet.CommandOrdinal < 0 ||
                packet.CommandOrdinal >= changes.Count ||
                changes[packet.CommandOrdinal] is not JsonObject operation ||
                operation.ContainsKey("reason") ||
                obligations.Any(obligation =>
                    obligation.CommandOrdinal == packet.CommandOrdinal))
            {
                return Array.Empty<ResourceRepairRetryObligation>();
            }

            obligations.Add(new ResourceRepairRetryObligation(
                packet.CommandOrdinal,
                root.DeepClone().AsObject()));
        }

        return obligations;
    }

    private async Task<bool> HasExactResourceRepairResubmissionAsync(
        IReadOnlyList<ResourceRepairRetryObligation> obligations)
    {
        var root = await ReadStrictResourceRepairCommandRootAsync();
        if (root?["resourceChanges"] is not JsonArray changes)
            return false;

        foreach (var obligation in obligations)
        {
            if (obligation.CommandOrdinal < 0 ||
                obligation.CommandOrdinal >= changes.Count ||
                changes[obligation.CommandOrdinal] is not JsonObject operation ||
                !IsReadableResourceRepairReason(operation["reason"]))
            {
                return false;
            }

            var comparisonRoot = root.DeepClone().AsObject();
            if (comparisonRoot["resourceChanges"] is not JsonArray comparisonChanges ||
                comparisonChanges[obligation.CommandOrdinal] is not JsonObject comparisonOperation)
            {
                return false;
            }
            comparisonOperation.Remove("reason");
            if (!JsonNode.DeepEquals(
                    comparisonRoot,
                    obligation.ExpectedCommandRootWithoutReason))
            {
                return false;
            }
        }

        return true;
    }

    private async Task<JsonObject?> ReadStrictResourceRepairCommandRootAsync()
    {
        var json = await _fs.ReadFileAsync(ResourceMaterializationContract.CommandPath);
        if (json == null)
            return null;
        try
        {
            return StrictJsonAuthority.Deserialize<JsonObject>(
                json,
                JsonOpts,
                "resource repair command");
        }
        catch (Exception exception) when (
            exception is JsonException or InvalidDataException or NotSupportedException)
        {
            return null;
        }
    }
}
