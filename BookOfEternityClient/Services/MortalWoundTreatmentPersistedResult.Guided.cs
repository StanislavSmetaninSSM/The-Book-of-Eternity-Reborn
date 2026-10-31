using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal sealed partial class MortalWoundTreatmentPersistedResult
{
    /// <summary>
    /// Detached comparison of the actual publication. It carries no admission,
    /// prepared reservation, effect execution or permission to mutate state.
    /// </summary>
    internal sealed record GuidedComparison(MortalWoundTreatmentResolution Resolution,
        MortalWoundTreatmentOutcomePreparation Preparation,
        MortalWoundTreatmentResourceFinalization Finalization, string SemanticFingerprint,
        string BindingFingerprint, string SourcePath, long CurrentMinute);

    /// <summary>
    /// Copies the actual signed prepared schedule, retained solely to compare
    /// ordinary progression after cleanup against its independent original hash.
    /// </summary>
    internal byte[] ReadGuidedOriginalScheduleBytes() => Convert.FromBase64String(
        RequiredText(_canonicalResult, "originalScheduleBase64"));

    /// <summary>
    /// Distinguishes actual guided publication comparison DATA from historical
    /// terminal receipts, which never receive an implicit empty witness.
    /// </summary>
    internal bool HasGuidedPublicationWitness => _canonicalResult["schemaVersion"]?.GetValue<int>() == 2;

    /// <summary>
    /// Captures only the genuine guided continuation's frozen comparison inputs.
    /// The terminal result remains the existing strict historical representation.
    /// </summary>
    internal static MortalWoundTreatmentPersistedResult CreateGuided(MortalWoundTreatmentResolution resolution,
        MortalWoundTreatmentAcceptedStateAuthority owner,
        WoundAcceptedTurnPlanner.MortalDiagnosisPublicationWitness inputs)
    {
        if (!owner.HasOriginalGuidedTreatmentInput || !owner.HasCurrentAdmissionAuthority() ||
            !resolution.Coordinates.MatchesAcceptedState(owner))
            throw new InvalidDataException("Guided result requires its actual current original continuation.");
        owner.RequireOriginalGuidedTreatmentRequest(resolution.RequestAuthority);
        var root = new JsonObject
        {
            ["kind"] = "treat", ["schemaVersion"] = 2,
            ["terminal"] = Create(resolution).ToCanonicalJson(),
            ["bindingFingerprint"] = owner.BindingFingerprint,
            ["sourcePath"] = owner.WoundSourcePath,
            ["currentMinute"] = owner.CurrentGameMinute,
            ["originalScheduleBase64"] = Convert.ToBase64String(owner.ReadDiagnosisOriginalBytes(ProgressionScheduleService.SchedulePath)),
            ["publicationInputs"] = inputs.ToCanonicalJson()
        };
        root["resultFingerprint"] = WoundHistoryState.ComputeTransitionResultFingerprint(root);
        var issues = new List<ValidationIssue>();
        return ParseGuided(JsonSerializer.SerializeToElement(root), "guidedTreatment.result", issues) ??
            throw new InvalidDataException(string.Join("; ", issues.Select(issue => issue.Code + ": " + issue.Actual)));
    }

    /// <summary>
    /// Reconstructs all three existing mode outcomes and resource finalization
    /// through their shared pure calculators. Historical results have no witness.
    /// </summary>
    internal GuidedComparison ReconstructGuidedPublication()
    {
        if (!HasGuidedPublicationWitness)
            throw new InvalidDataException("Historical treatment DATA has no guided publication witness.");
        var binding = RequiredText(_canonicalResult, "bindingFingerprint");
        var source = RequiredText(_canonicalResult, "sourcePath");
        var minute = _canonicalResult["currentMinute"]!.GetValue<long>();
        if (!MortalWoundTreatmentOutcomeIntentComposer.TryComposeDetached(Request, Receipt.DeclaredResult,
                binding, source, out var intents, out var issues) || issues.Count != 0)
            throw new InvalidDataException("The complete declared treatment intents do not reconstruct.");
        var critical = Request.ModeAuthority is MortalWoundProcedureCheckAuthority { PreparedCriticalReaction: { } prepared }
            ? MortalWoundCriticalReactionIntent.Create(Request, prepared) : null;
        var resolution = MortalWoundTreatmentResolution.Create(Request.Mode, Request.Coordinates,
            Receipt.AttemptDisposition, Receipt.ResultCategory, Receipt.SelectedOutcomeIndex, Receipt.Interruption,
            Receipt.DeclaredResult, intents, critical, Receipt.ConsumptionTrigger, Receipt.CourseId,
            Receipt.CourseMilestoneOrdinal, Receipt.CourseDisposition, Request, Receipt.ModeEvidence,
            Receipt.RouteFingerprint, Receipt.RouteCompletion);
        if (MortalWoundTreatmentReceipt.Create(resolution).ReceiptFingerprint != Receipt.ReceiptFingerprint)
            throw new InvalidDataException("Guided receipt differs from the independently reconstructed complete result.");
        var outcome = MortalWoundTreatmentOutcomePublicationPlanner.RecomposeDetached(Request, resolution, minute);
        var resources = MortalWoundTreatmentResourceComposer.Finalize(resolution);
        if (!outcome.IsValid || outcome.Preparation is null || !resources.IsValid || resources.Finalization is null)
            throw new InvalidDataException("Guided outcome/resource finalization does not reconstruct.");
        var inputs = WoundAcceptedTurnPlanner.MortalDiagnosisPublicationWitness.Parse(_canonicalResult["publicationInputs"]!.AsObject());
        return new(resolution, outcome.Preparation, resources.Finalization,
            inputs.ComputeTreatmentSemantic(Request.Coordinates.AcceptedStateFingerprint, binding,
                Request, resolution, outcome.Preparation.Fingerprint), binding, source, minute);
    }

    private static MortalWoundTreatmentPersistedResult? ParseGuided(JsonElement element, string path,
        ICollection<ValidationIssue> issues)
    {
        try
        {
            if (!MortalWoundTreatmentCommandCodec.HasNoDuplicateProperties(element, path, issues))
                throw new InvalidDataException("Duplicate guided result fields.");
            var root = JsonNode.Parse(element.GetRawText())?.AsObject() ?? throw new InvalidDataException("Missing guided result.");
            var names = new[] { "kind", "schemaVersion", "terminal", "bindingFingerprint", "sourcePath",
                "currentMinute", "originalScheduleBase64", "publicationInputs", "resultFingerprint" };
            if (root.Count != names.Length || names.Any(name => !root.ContainsKey(name)) ||
                RequiredText(root, "kind") != "treat" || root["schemaVersion"]!.GetValue<int>() != 2 ||
                root["terminal"] is not JsonObject terminal || terminal.ContainsKey("schemaVersion") ||
                root["publicationInputs"] is not JsonObject input ||
                !ResourceMaterializationContract.IsAuthorityFingerprint(RequiredText(root, "bindingFingerprint")) ||
                root["currentMinute"]!.GetValue<long>() < 0 || string.IsNullOrWhiteSpace(RequiredText(root, "sourcePath")) ||
                RequiredText(root, "resultFingerprint") != WoundHistoryState.ComputeTransitionResultFingerprint(root))
                throw new InvalidDataException("Unknown, incomplete or changed guided result fields/seal.");
            var schedule = Convert.FromBase64String(RequiredText(root, "originalScheduleBase64"));
            if (Convert.ToBase64String(schedule) != RequiredText(root, "originalScheduleBase64") ||
                JsonNode.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(schedule)) is not JsonObject)
                throw new InvalidDataException("Guided original schedule is not exact canonical Base64 JSON DATA.");
            var start = issues.Count;
            var historical = Parse(JsonSerializer.SerializeToElement(terminal), path + ".terminal", issues);
            if (historical is null || issues.Count != start) return null;
            _ = WoundAcceptedTurnPlanner.MortalDiagnosisPublicationWitness.Parse(input);
            var result = new MortalWoundTreatmentPersistedResult(historical.Request, historical.Receipt, root);
            _ = result.ReconstructGuidedPublication();
            return result;
        }
        catch (Exception failure) when (failure is InvalidDataException or InvalidOperationException or
                                       ArgumentException or JsonException or OverflowException or FormatException)
        {
            issues.Add(new ValidationIssue(path, IssueSeverity.Error,
                "The complete guided treatment publication DATA is invalid.",
                code: "mortal_wound_guided_publication_result_invalid", actor: "Client", section: "wound_materialization",
                expected: "one closed version2 witness and independently reconstructed terminal publication", actual: failure.Message));
            return null;
        }
    }

    private static string RequiredText(JsonObject root, string name) => root[name] is JsonValue value &&
        value.TryGetValue<string>(out var text) ? text : throw new InvalidDataException("Missing guided result field: " + name);
}
