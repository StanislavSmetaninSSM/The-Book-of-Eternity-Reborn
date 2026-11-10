using System.Globalization;
using System.Text.Json;

namespace BookOfEternityClient.Services;

internal static partial class WoundAcceptedTurnFingerprints
{
    /// <summary>Captures complete ordered replay INPUT, including canonical-omitted typed fields.</summary>
    /// <param name="input">One fixed defensively owned input used for admission and source.</param>
    /// <param name="companions">Exact privately owned continuation companions; no authority is issued.</param>
    /// <returns>Structural DATA, or null for unsupported opaque original provenance.</returns>
    internal static OperationStructuralSnapshot? CapturePreparationReplayInput(WoundAcceptedTurnInput input,
        IReadOnlyList<object?>? companions = null)
    {
        var opportunities = input.Opportunities;
        if (opportunities.Any(value => value.OriginalSourceGuarantee is not null)) return null;
        var fields = new List<string?> { "JointSemantic.ExpectedPreparationProjection.Input", "1" };
        AppendBinding(fields, input.Binding); // expand actual fields, not a nested digest
        fields.Add(Count(opportunities));
        for (var i = 0; i < opportunities.Count; i++)
        {
            fields.Add(Number(i)); AppendOpportunity(fields, opportunities[i]);
            fields.Add(opportunities[i].AcceptedEventsFingerprint);
            AppendReplayWound(fields, opportunities[i].WorseningTarget?.Wound);
        }
        var transitions = input.Transitions;
        fields.Add(Count(transitions));
        for (var i = 0; i < transitions.Count; i++)
        {
            fields.Add(Number(i)); AppendTransitionDraft(fields, transitions[i]);
            AppendReplayWound(fields, transitions[i].ProposedAfter);
        }
        AppendWoundCarriers(fields, input.PreTurnCarriers);
        fields.Add(WoundAcceptedTurnFingerprintWriter.CanonicalJson(input.PreTurnIdentityIndex));
        fields.Add(WoundAcceptedTurnFingerprintWriter.CanonicalJson(input.PreTurnHistory));
        AppendEffectCarriers(fields, input.PreTurnEffectCarriers);
        fields.Add(WoundAcceptedTurnFingerprintWriter.CanonicalJson(input.PreTurnEffectIdentityIndex));
        return new(fields, companions ?? Array.Empty<object?>());
    }

    /// <summary>Captures the order and hidden fields omitted by canonical wound serialization.</summary>
    /// <param name="fields">The owned exact ordered input sequence.</param>
    /// <param name="wound">The detached proposed or worsening wound, or null.</param>
    private static void AppendReplayWound(List<string?> fields, WoundMaterializationEnvelope? wound)
    {
        if (wound is null) { fields.Add(null); return; }
        fields.Add("present");
        // Mortal canonical treatment reparses/normalizes projection DATA. Retain the actual typed shape too.
        fields.Add(JsonSerializer.Serialize(wound.Treatment));
        var owned = wound.Consequences.OwnedEffectSources;
        // These sequences are sorted in canonical JSON; INPUT equality must retain their actual order.
        fields.Add(owned.Definitions.Count.ToString(CultureInfo.InvariantCulture));
        foreach (var definition in owned.Definitions) fields.Add(definition.GetRawText());
        fields.Add(owned.RootBindings.Count.ToString(CultureInfo.InvariantCulture));
        foreach (var binding in owned.RootBindings) { fields.Add(binding.EffectId); fields.Add(binding.DefinitionKey); }
        fields.Add(wound.Consequences.Entries.Count.ToString(CultureInfo.InvariantCulture));
        foreach (var entry in wound.Consequences.Entries)
        {
            fields.Add(Number(entry.Slot)); fields.Add(entry.ProfileKey); fields.Add(entry.EffectId); fields.Add(entry.ReadableSummary);
        }
        fields.Add(owned.DefinitionFacts.Count.ToString(CultureInfo.InvariantCulture));
        foreach (var fact in owned.DefinitionFacts)
        {
            fields.Add(fact.DefinitionKey); fields.Add(fact.CanonicalJson);
            fields.Add(fact.ApplyDefinitionTargets.Length.ToString(CultureInfo.InvariantCulture));
            fields.AddRange(fact.ApplyDefinitionTargets); fields.Add(Boolean(fact.ContainsWoundConsequenceMarker));
        }
        fields.Add(wound.Treatment.DiagnosisPaths.Count.ToString(CultureInfo.InvariantCulture));
        foreach (var path in wound.Treatment.DiagnosisPaths) fields.Add(path.SourcePath);
        fields.Add(wound.Treatment.Routes.Count.ToString(CultureInfo.InvariantCulture));
        foreach (var route in wound.Treatment.Routes) fields.Add(route.SourcePath);
    }
}
