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
    /// Reconstructs the complete guided publication comparison, retaining its
    /// independent resource finalization and frozen publication-input semantics.
    /// </summary>
    internal GuidedComparison ReconstructGuidedPublication()
    {
        if (!HasGuidedPublicationWitness)
            throw new InvalidDataException("Historical treatment DATA has no guided publication witness.");
        return ReconstructGuidedPublication(ReconstructHistoricalPublication(), _canonicalResult);
    }

    /// <summary>Completes the guided-only checks after the shared pure outcome reconstruction.</summary>
    /// <param name="historical">The locally reconstructed detached wound outcome.</param>
    /// <param name="root">The exact frozen version2 witness.</param>
    /// <returns>The complete original guided comparison, including resources and publication semantics.</returns>
    private static GuidedComparison ReconstructGuidedPublication(HistoricalComparison historical, JsonObject root)
    {
        var resolution = historical.Resolution;
        var resources = MortalWoundTreatmentResourceComposer.Finalize(resolution);
        if (!resources.IsValid || resources.Finalization is null)
            throw new InvalidDataException("Guided resource finalization does not reconstruct.");
        var inputs = WoundAcceptedTurnPlanner.MortalDiagnosisPublicationWitness.Parse(root["publicationInputs"]!.AsObject());
        return new(resolution, historical.Preparation, resources.Finalization,
            inputs.ComputeTreatmentSemantic(resolution.RequestAuthority.Coordinates.AcceptedStateFingerprint,
                historical.BindingFingerprint, resolution.RequestAuthority, resolution, historical.Preparation.Fingerprint),
            historical.BindingFingerprint, historical.SourcePath, historical.CurrentMinute);
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
                string.IsNullOrWhiteSpace(RequiredText(root, "sourcePath")) ||
                RequiredText(root, "resultFingerprint") != WoundHistoryState.ComputeTransitionResultFingerprint(root))
                throw new InvalidDataException("Unknown, incomplete or changed guided result fields/seal.");
            var currentMinute = RequiredHistoricalMinute(root, "currentMinute");
            var schedule = Convert.FromBase64String(RequiredText(root, "originalScheduleBase64"));
            if (Convert.ToBase64String(schedule) != RequiredText(root, "originalScheduleBase64") ||
                JsonNode.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(schedule)) is not JsonObject)
                throw new InvalidDataException("Guided original schedule is not exact canonical Base64 JSON DATA.");
            var start = issues.Count;
            var historical = Parse(JsonSerializer.SerializeToElement(terminal), path + ".terminal", issues);
            if (historical is null || issues.Count != start) return null;
            var comparison = ReconstructHistoricalPublication(historical.Request, historical.Receipt,
                RequiredText(root, "bindingFingerprint"), RequiredText(root, "sourcePath"), currentMinute);
            _ = ReconstructGuidedPublication(comparison, root);
            return new MortalWoundTreatmentPersistedResult(historical.Request, historical.Receipt, root);
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
        value.TryGetValue<string>(out var text) ? text : throw new InvalidDataException("Missing treatment result text field: " + name);
}
