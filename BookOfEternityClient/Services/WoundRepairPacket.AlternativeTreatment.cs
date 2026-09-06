using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal enum WoundAlternativeTreatmentCorrectionStatus { Rejected, NeedsAnotherRepair, Complete }
internal sealed record WoundAlternativeTreatmentCorrectionAnalysis(
    WoundAlternativeTreatmentCorrectionStatus Status, ImmutableArray<ValidationIssue> Issues,
    JsonElement? CorrectedAuthor, WoundAlternativeTreatmentResponseDraft? CompleteDraft);

internal enum AlternativeEditKind { ReplaceOrRemove, AppendRequiredRouteFact }
internal sealed class AlternativeEditRule
{
    private readonly JsonArray? _originalFacts;
    internal AlternativeEditRule(string path, AlternativeEditKind kind, string? requiredFact = null, JsonArray? originalFacts = null)
    {
        Path = path; Kind = kind; RequiredFact = requiredFact;
        _originalFacts = originalFacts?.DeepClone().AsArray();
    }
    internal string Path { get; }
    internal AlternativeEditKind Kind { get; }
    internal string? RequiredFact { get; }
    internal JsonArray? OriginalFacts => _originalFacts?.DeepClone().AsArray();
    internal AlternativeEditRule Clone() => new(Path, Kind, RequiredFact, _originalFacts);
}

internal sealed partial class WoundRepairPacket
{
    private readonly JsonObject? _alternativeBaseline;
    private readonly IReadOnlySet<string> _alternativeSecrets = new HashSet<string>(StringComparer.Ordinal);
    private readonly AlternativeEditRule[] _alternativeRules = Array.Empty<AlternativeEditRule>();
    private readonly string? _alternativePrefix;

    internal WoundRepairPacket(WoundRepairBuildRequest request, WoundRepairCandidateInput candidate,
        IReadOnlyList<WoundRepairPacketIssue> issues, JsonObject context, JsonObject preserved, JsonObject shape,
        JsonObject baseline, IReadOnlySet<string> secrets, IReadOnlyList<AlternativeEditRule> rules, string prefix)
        : this(request.SessionId, request.RequestId, request.SnapshotToken, candidate.CandidateRef,
            candidate.SemanticFingerprint, issues, context, preserved, shape, candidate.RejectedDecision, null, candidate.Kind)
    {
        _alternativeBaseline = baseline.DeepClone().AsObject();
        _alternativeSecrets = new HashSet<string>(secrets, StringComparer.Ordinal);
        _alternativeRules = rules.Select(rule => rule.Clone()).ToArray();
        _alternativePrefix = prefix;
    }

    internal bool MatchesCorrectedAlternativeTreatmentAuthoring(JsonObject corrected) =>
        AnalyzeAlternativeTreatmentCorrection(corrected).Status == WoundAlternativeTreatmentCorrectionStatus.Complete;

    internal WoundAlternativeTreatmentCorrectionAnalysis AnalyzeAlternativeTreatmentCorrection(JsonObject corrected)
    {
        static WoundAlternativeTreatmentCorrectionAnalysis Rejected() => new(
            WoundAlternativeTreatmentCorrectionStatus.Rejected, ImmutableArray<ValidationIssue>.Empty, null, null);
        if (CandidateKind != "author_alternative_treatment" || _alternativeBaseline is null ||
            !WoundRepairPacketBuilder.HasAlternativeEnvelope(corrected,
                _rejectedDecision["authoringRequestRef"]!.GetValue<string>(), out var decision) || decision != "author" ||
            WoundRepairPacketBuilder.ContainsAlternativePrivateData(corrected, _alternativeSecrets))
            return Rejected();
        var payload = WoundRepairPacketBuilder.AlternativePayload(corrected);
        if (JsonNode.DeepEquals(payload, _alternativeBaseline))
            return Rejected();
        var masked = payload.DeepClone().AsObject();
        foreach (var rule in _alternativeRules)
        {
            if (rule.Kind == AlternativeEditKind.AppendRequiredRouteFact)
            {
                if (masked["diagnosisPath"] is not JsonObject path || path["reveals"] is not JsonArray facts ||
                    rule.OriginalFacts is not { } original || facts.Count != original.Count + 1 ||
                    facts[^1] is not JsonValue last || !last.TryGetValue<string>(out var fact) || fact != rule.RequiredFact)
                    return Rejected();
                facts.RemoveAt(facts.Count - 1);
                if (!JsonNode.DeepEquals(facts, original))
                    return Rejected();
            }
            else
                WoundRepairPacketBuilder.MaskAlternativePath(masked, rule.Path);
        }
        if (!JsonNode.DeepEquals(masked, _preservedProposal))
            return Rejected();
        var parsed = WoundRepairPacketBuilder.ParseAlternative(corrected, _alternativePrefix!);
        if (parsed.IsValid)
            return new(WoundAlternativeTreatmentCorrectionStatus.Complete, parsed.Issues,
                JsonSerializer.SerializeToElement(corrected), parsed.Drafts.Single());
        if (!WoundRepairPacketBuilder.TryProjectAlternativeDiagnostics(corrected, _alternativePrefix!, _alternativeSecrets,
                out var nextIssues, out _) || nextIssues.Any(issue => _alternativeRules.Any(rule => rule.Path == issue.Path)) ||
            parsed.Issues.Any(issue => _alternativeRules.Any(rule => issue.FilePath == _alternativePrefix + "." + rule.Path)))
            return Rejected();
        return new(WoundAlternativeTreatmentCorrectionStatus.NeedsAnotherRepair, parsed.Issues,
            JsonSerializer.SerializeToElement(corrected), null);
    }
}
