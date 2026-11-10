using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal sealed partial class MortalWoundTreatmentPersistedResult
{
    /// <summary>
    /// Detached historical outcome DATA only. It carries no admission, resource
    /// reservation, effect execution or permission to publish or mutate state.
    /// </summary>
    internal sealed record HistoricalComparison(MortalWoundTreatmentResolution Resolution,
        MortalWoundTreatmentOutcomePreparation Preparation, string BindingFingerprint,
        string SourcePath, long CurrentMinute);

    /// <summary>
    /// Captures the exact preparation context from its current accepted owner.
    /// The nested raw request/receipt remains unchanged and independently sealed.
    /// </summary>
    /// <param name="owner">The genuine current accepted-state owner.</param>
    /// <param name="resolution">The actual complete selected treatment resolution.</param>
    /// <param name="preparation">Its agreeing outcome preparation and exact publication minute.</param>
    /// <returns>A closed version3 History envelope, never a publication authority.</returns>
    internal static MortalWoundTreatmentPersistedResult CreateHistory(
        MortalWoundTreatmentAcceptedStateAuthority owner, MortalWoundTreatmentResolution resolution,
        MortalWoundTreatmentOutcomePreparation preparation)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(resolution);
        ArgumentNullException.ThrowIfNull(preparation);
        if (!resolution.Coordinates.MatchesAcceptedState(owner) ||
            preparation.CurrentGameMinute != owner.CurrentGameMinute ||
            !preparation.AgreesWith(resolution) ||
            !ResourceMaterializationContract.IsAuthorityFingerprint(owner.BindingFingerprint) ||
            string.IsNullOrWhiteSpace(owner.WoundSourcePath) ||
            !MortalWoundTreatmentOutcomeIntentComposer.TryComposeDetached(resolution.RequestAuthority,
                resolution.DeclaredResult, owner.BindingFingerprint, owner.WoundSourcePath,
                out var expectedIntents, out var issues) || issues.Count != 0 ||
            !MortalWoundTreatmentOutcomePublicationPlanner.OrderedIntentsAgree(resolution.RequestAuthority,
                resolution.DeclaredResult, resolution.OutcomeIntents, expectedIntents))
            throw new InvalidDataException("History requires the actual owner's exact preparation, clock and outcome context.");
        var terminal = Create(resolution);
        var root = new JsonObject
        {
            ["kind"] = "treat", ["schemaVersion"] = 3, ["terminal"] = terminal.ToCanonicalJson(),
            ["bindingFingerprint"] = owner.BindingFingerprint, ["sourcePath"] = owner.WoundSourcePath,
            ["publicationMinute"] = owner.CurrentGameMinute,
            ["outcomePreparationFingerprint"] = preparation.Fingerprint
        };
        root["resultFingerprint"] = WoundHistoryState.ComputeTransitionResultFingerprint(root);
        // DATA work above grants no permission and cannot replace this closing owner fence.
        if (!resolution.Coordinates.MatchesAcceptedState(owner))
            throw new InvalidDataException("History capture lost its current accepted-state owner.");
        return new MortalWoundTreatmentPersistedResult(terminal.Request, terminal.Receipt, root);
    }

    /// <summary>
    /// Exports independently parsed inner companions from this private frozen
    /// canonical value. No selected-graph or resource pipeline is rerun merely
    /// to copy already-validated wrapper DATA, and no live authority is restored.
    /// </summary>
    /// <returns>A separate canonical value with fresh detached request and receipt graphs.</returns>
    internal MortalWoundTreatmentPersistedResult DetachedCopy()
    {
        var (request, receipt) = ReadFrozenTerminal();
        return new MortalWoundTreatmentPersistedResult(request, receipt, _canonicalResult);
    }

    /// <summary>
    /// Reconstructs only the pure wound outcome for guided version2 or History
    /// version3. Fresh inner companions isolate the caller from exposed aliases;
    /// raw terminal DATA cannot invent a historical clock or binding context.
    /// </summary>
    /// <returns>One fresh detached historical comparison, without payment or publication witness processing.</returns>
    internal HistoricalComparison ReconstructHistoricalPublication()
    {
        if (_canonicalResult["schemaVersion"] is not JsonValue versionNode ||
            !versionNode.TryGetValue<int>(out var version) || version is not (2 or 3))
            throw new InvalidDataException("Raw treatment DATA has no historical preparation context.");
        var (request, receipt) = ReadFrozenTerminal();
        return version == 3 ? ReconstructHistoryVersion3(request, receipt, _canonicalResult) :
            ReconstructHistoricalPublication(request, receipt,
                RequiredText(_canonicalResult, "bindingFingerprint"), RequiredText(_canonicalResult, "sourcePath"),
                RequiredHistoricalMinute(_canonicalResult, "currentMinute"));
    }

    /// <summary>Decodes only the exact private frozen terminal, never exposed request/receipt aliases.</summary>
    /// <returns>Fresh strict raw request and receipt DATA.</returns>
    private (MortalWoundTreatmentAttemptRequest Request, MortalWoundTreatmentReceipt Receipt) ReadFrozenTerminal()
    {
        var issues = new List<ValidationIssue>();
        if (!MortalWoundTreatmentCommandCodec.TryParseHistoryResult(
                JsonSerializer.SerializeToElement(_canonicalResult["terminal"] ?? _canonicalResult),
                "treatmentResult.terminal", issues, out var request, out var receipt, out _) ||
            request is null || receipt is null || issues.Count != 0)
            throw new InvalidDataException("The frozen treatment terminal DATA no longer reconstructs.");
        return (request, receipt);
    }

    /// <summary>Shares pure resolution/outcome reconstruction without resource or guided semantic processing.</summary>
    /// <param name="request">The locally decoded strict request.</param>
    /// <param name="receipt">Its independently sealed terminal receipt.</param>
    /// <param name="binding">The exact historical binding fingerprint.</param>
    /// <param name="source">The exact historical source path.</param>
    /// <param name="minute">The exact captured publication minute.</param>
    /// <returns>The complete detached outcome comparison.</returns>
    private static HistoricalComparison ReconstructHistoricalPublication(MortalWoundTreatmentAttemptRequest request,
        MortalWoundTreatmentReceipt receipt, string binding, string source, long minute)
    {
        if (!MortalWoundTreatmentOutcomeIntentComposer.TryComposeDetached(request, receipt.DeclaredResult,
                binding, source, out var intents, out var issues) || issues.Count != 0)
            throw new InvalidDataException("The complete declared treatment intents do not reconstruct.");
        var critical = request.ModeAuthority is MortalWoundProcedureCheckAuthority { PreparedCriticalReaction: { } prepared }
            ? MortalWoundCriticalReactionIntent.Create(request, prepared) : null;
        var resolution = MortalWoundTreatmentResolution.Create(request.Mode, request.Coordinates,
            receipt.AttemptDisposition, receipt.ResultCategory, receipt.SelectedOutcomeIndex, receipt.Interruption,
            receipt.DeclaredResult, intents, critical, receipt.ConsumptionTrigger, receipt.CourseId,
            receipt.CourseMilestoneOrdinal, receipt.CourseDisposition, request, receipt.ModeEvidence,
            receipt.RouteFingerprint, receipt.RouteCompletion);
        if (MortalWoundTreatmentReceipt.Create(resolution).ReceiptFingerprint != receipt.ReceiptFingerprint)
            throw new InvalidDataException("Treatment receipt differs from the independently reconstructed complete result.");
        var outcome = MortalWoundTreatmentOutcomePublicationPlanner.RecomposeDetached(request, resolution, minute);
        if (!outcome.IsValid || outcome.Preparation is null)
            throw new InvalidDataException("Historical treatment outcome preparation does not reconstruct.");
        return new HistoricalComparison(resolution, outcome.Preparation, binding, source, minute);
    }

    /// <summary>
    /// Compares retained version3 preparation DATA and its deterministic default
    /// binding. This is consistency checking, not authentication of a fully re-sealed graph.
    /// </summary>
    private static HistoricalComparison ReconstructHistoryVersion3(MortalWoundTreatmentAttemptRequest request,
        MortalWoundTreatmentReceipt receipt, JsonObject root)
    {
        var binding = RequiredText(root, "bindingFingerprint");
        var coordinates = request.Coordinates;
        var events = WoundAcceptedEventAuthorityComposer.ComposeDefaultAcceptedTurn(coordinates.SessionId,
            coordinates.RequestId, coordinates.SnapshotToken, coordinates.Turn);
        if (!events.Success || events.Issues.Count != 0 || events.Events.Count != 1 ||
            events.Events[0].EventRef != coordinates.EventRef || events.Events[0].Kind != coordinates.EventKind ||
            events.Events[0].AuthorityId != coordinates.EventAuthorityId ||
            events.Events[0].SemanticFingerprint != coordinates.EventSemanticFingerprint ||
            binding != MortalWoundTreatmentAcceptedStateAuthority.HashBinding(new WoundAcceptedTurnBinding(
                coordinates.SessionId, coordinates.RequestId, coordinates.SnapshotToken, coordinates.Realm,
                coordinates.Turn, events.Events, events.EventsFingerprint)))
            throw new InvalidDataException("History binding differs from the exact default accepted event DATA.");
        var expectedPreparation = RequiredText(root, "outcomePreparationFingerprint");
        if (!ResourceMaterializationContract.IsAuthorityFingerprint(expectedPreparation))
            throw new InvalidDataException("History lacks its retained outcome preparation fingerprint.");
        var comparison = ReconstructHistoricalPublication(request, receipt, binding,
            RequiredText(root, "sourcePath"), RequiredHistoricalMinute(root, "publicationMinute"));
        if (comparison.Preparation.Fingerprint != expectedPreparation)
            throw new InvalidDataException("History context differs from its retained outcome preparation.");
        return comparison;
    }

    /// <summary>Parses only the closed version3 History shape and its complete independent outcome.</summary>
    /// <param name="element">The external History result JSON.</param>
    /// <param name="path">The diagnostic result path.</param>
    /// <param name="issues">The owning validation issue collection.</param>
    /// <returns>A validated frozen result, or null with a refusal issue.</returns>
    private static MortalWoundTreatmentPersistedResult? ParseHistory(JsonElement element, string path,
        ICollection<ValidationIssue> issues)
    {
        try
        {
            if (!MortalWoundTreatmentCommandCodec.HasNoDuplicateProperties(element, path, issues))
                throw new InvalidDataException("Duplicate History result fields.");
            var root = JsonNode.Parse(element.GetRawText())?.AsObject() ?? throw new InvalidDataException("Missing History result.");
            var names = new[] { "kind", "schemaVersion", "terminal", "bindingFingerprint", "sourcePath",
                "publicationMinute", "outcomePreparationFingerprint", "resultFingerprint" };
            if (root.Count != names.Length || names.Any(name => !root.ContainsKey(name)) ||
                RequiredText(root, "kind") != "treat" || root["schemaVersion"] is not JsonValue version ||
                !version.TryGetValue<int>(out var number) || number != 3 ||
                root["terminal"] is not JsonObject terminal || terminal.ContainsKey("schemaVersion") ||
                !ResourceMaterializationContract.IsAuthorityFingerprint(RequiredText(root, "bindingFingerprint")) ||
                string.IsNullOrWhiteSpace(RequiredText(root, "sourcePath")) ||
                RequiredText(root, "resultFingerprint") != WoundHistoryState.ComputeTransitionResultFingerprint(root))
                throw new InvalidDataException("Unknown, incomplete or changed History result fields/seal.");
            var start = issues.Count;
            var historical = Parse(JsonSerializer.SerializeToElement(terminal), path + ".terminal", issues);
            if (historical is null || issues.Count != start) return null;
            _ = ReconstructHistoryVersion3(historical.Request, historical.Receipt, root);
            return new MortalWoundTreatmentPersistedResult(historical.Request, historical.Receipt, root);
        }
        catch (Exception failure) when (failure is InvalidDataException or InvalidOperationException or
                                       ArgumentException or JsonException or OverflowException or FormatException)
        {
            issues.Add(new ValidationIssue(path, IssueSeverity.Error,
                "The complete physical treatment History DATA is invalid.",
                code: "mortal_wound_treatment_history_result_invalid", actor: "Client", section: "wound_materialization",
                expected: "one closed version3 historical context and independently reconstructed terminal outcome", actual: failure.Message));
            return null;
        }
    }

    /// <summary>Reads a mandatory exact nonnegative Int64 minute without defaulting or coercion.</summary>
    /// <param name="root">The closed frozen result.</param>
    /// <param name="name">The version-specific minute field.</param>
    /// <returns>The exact historical minute.</returns>
    private static long RequiredHistoricalMinute(JsonObject root, string name) =>
        root[name] is JsonValue value && value.TryGetValue<long>(out var minute) && minute >= 0
            ? minute : throw new InvalidDataException("Missing or invalid treatment result minute: " + name);
}
