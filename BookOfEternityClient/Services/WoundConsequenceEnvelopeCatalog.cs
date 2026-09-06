using System.Collections.Immutable;
using System.Globalization;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal sealed record WoundConsequenceInputFault(
    string PathSuffix,
    string Actual,
    string Code = "wound_consequence_input_invalid",
    string Expected = "non-null typed value or collection with defined JSON elements");

internal sealed record WoundConsequenceLifecycleEvidence(
    bool ActiveComplicationChangesLifecycle,
    bool CareConstraintChangesLifecycle,
    bool RecoveryConstraintChangesLifecycle)
{
    internal bool ChangesLegalLifecycle =>
        ActiveComplicationChangesLifecycle ||
        CareConstraintChangesLifecycle ||
        RecoveryConstraintChangesLifecycle;
}

internal sealed record WoundResourceEnvelopeBound(
    string ResourceKey,
    decimal Maximum,
    decimal Quantum);

internal sealed record WoundPeriodicCadenceEvidence(
    string ComponentId,
    string SourceEvent,
    int MaximumExecutionsPerSourceEvent);

internal sealed class WoundReactionExpansionProposal
{
    internal WoundReactionExpansionProposal(
        string? reactionComponentId,
        IReadOnlyList<JsonElement>? components)
    {
        ReactionComponentId = reactionComponentId ?? string.Empty;
        var faults = ImmutableArray.CreateBuilder<WoundConsequenceInputFault>();
        if (reactionComponentId == null)
            faults.Add(new WoundConsequenceInputFault("reactionComponentId", "null"));
        Components = CloneElements(components, faults);
        InputFaults = faults.ToImmutable();
    }

    internal string ReactionComponentId { get; }

    internal ImmutableArray<JsonElement> Components { get; }

    internal ImmutableArray<WoundConsequenceInputFault> InputFaults { get; }

    private static ImmutableArray<JsonElement> CloneElements(
        IReadOnlyList<JsonElement>? components,
        ImmutableArray<WoundConsequenceInputFault>.Builder faults)
    {
        if (components == null)
        {
            faults.Add(new WoundConsequenceInputFault("components", "null"));
            return ImmutableArray<JsonElement>.Empty;
        }

        if (components.Count > WoundConsequenceEnvelopeCatalog.MaximumReactionExpansionComponents)
        {
            faults.Add(new WoundConsequenceInputFault(
                "components",
                components.Count.ToString(CultureInfo.InvariantCulture),
                "wound_consequence_limit_exceeded",
                $"at most {WoundConsequenceEnvelopeCatalog.MaximumReactionExpansionComponents} flattened components"));
            return ImmutableArray<JsonElement>.Empty;
        }

        var result = ImmutableArray.CreateBuilder<JsonElement>(components.Count);
        for (var index = 0; index < components.Count; index++)
        {
            var component = components[index];
            if (component.ValueKind == JsonValueKind.Undefined)
            {
                faults.Add(new WoundConsequenceInputFault(
                    $"components[{index}]",
                    "undefined JsonElement"));
                result.Add(default);
            }
            else
            {
                result.Add(component.Clone());
            }
        }

        return result.MoveToImmutable();
    }
}

internal sealed class WoundConsequenceEffectProposal
{
    internal WoundConsequenceEffectProposal(
        string? effectId,
        string? sourceKind,
        string? sourceId,
        string? reciprocalWoundId,
        IReadOnlyList<JsonElement>? components,
        IReadOnlyList<WoundPeriodicCadenceEvidence>? cadences,
        IReadOnlyList<WoundReactionExpansionProposal>? expansions)
    {
        EffectId = effectId ?? string.Empty;
        SourceKind = sourceKind ?? string.Empty;
        SourceId = sourceId ?? string.Empty;
        ReciprocalWoundId = reciprocalWoundId;
        var faults = ImmutableArray.CreateBuilder<WoundConsequenceInputFault>();
        Components = CloneComponents(components, faults);
        Cadences = CloneCadences(cadences, faults);
        Expansions = CloneExpansions(expansions, faults);
        InputFaults = faults.ToImmutable();
    }

    internal string EffectId { get; }

    internal string SourceKind { get; }

    internal string SourceId { get; }

    internal string? ReciprocalWoundId { get; }

    internal ImmutableArray<JsonElement> Components { get; }

    internal ImmutableArray<WoundPeriodicCadenceEvidence> Cadences { get; }

    internal ImmutableArray<WoundReactionExpansionProposal> Expansions { get; }

    internal ImmutableArray<WoundConsequenceInputFault> InputFaults { get; }

    internal bool IsMissing { get; private init; }

    internal static WoundConsequenceEffectProposal Missing() =>
        new(null, null, null, null, null, null, null) { IsMissing = true };

    private static ImmutableArray<JsonElement> CloneComponents(
        IReadOnlyList<JsonElement>? components,
        ImmutableArray<WoundConsequenceInputFault>.Builder faults)
    {
        if (components == null)
        {
            faults.Add(new WoundConsequenceInputFault("components", "null"));
            return ImmutableArray<JsonElement>.Empty;
        }

        if (components.Count > WoundConsequenceEnvelopeCatalog.MaximumComponentsPerEffect)
        {
            faults.Add(new WoundConsequenceInputFault(
                "components",
                components.Count.ToString(CultureInfo.InvariantCulture),
                "wound_consequence_limit_exceeded",
                $"at most {WoundConsequenceEnvelopeCatalog.MaximumComponentsPerEffect} components"));
            return ImmutableArray<JsonElement>.Empty;
        }

        var result = ImmutableArray.CreateBuilder<JsonElement>(components.Count);
        for (var index = 0; index < components.Count; index++)
        {
            var component = components[index];
            if (component.ValueKind == JsonValueKind.Undefined)
            {
                faults.Add(new WoundConsequenceInputFault(
                    $"components[{index}]",
                    "undefined JsonElement"));
                result.Add(default);
            }
            else
            {
                result.Add(component.Clone());
            }
        }

        return result.MoveToImmutable();
    }

    private static ImmutableArray<WoundPeriodicCadenceEvidence> CloneCadences(
        IReadOnlyList<WoundPeriodicCadenceEvidence>? cadences,
        ImmutableArray<WoundConsequenceInputFault>.Builder faults)
    {
        if (cadences == null)
        {
            faults.Add(new WoundConsequenceInputFault("cadences", "null"));
            return ImmutableArray<WoundPeriodicCadenceEvidence>.Empty;
        }

        if (cadences.Count > WoundConsequenceEnvelopeCatalog.MaximumCadencesPerEffect)
        {
            faults.Add(new WoundConsequenceInputFault(
                "cadences",
                cadences.Count.ToString(CultureInfo.InvariantCulture),
                "wound_consequence_limit_exceeded",
                $"at most {WoundConsequenceEnvelopeCatalog.MaximumCadencesPerEffect} cadence entries"));
            return ImmutableArray<WoundPeriodicCadenceEvidence>.Empty;
        }

        var result = ImmutableArray.CreateBuilder<WoundPeriodicCadenceEvidence>(cadences.Count);
        for (var index = 0; index < cadences.Count; index++)
        {
            if (cadences[index] is { } cadence)
            {
                result.Add(cadence);
            }
            else
            {
                faults.Add(new WoundConsequenceInputFault($"cadences[{index}]", "null"));
                result.Add(new WoundPeriodicCadenceEvidence(string.Empty, string.Empty, 0));
            }
        }

        return result.MoveToImmutable();
    }

    private static ImmutableArray<WoundReactionExpansionProposal> CloneExpansions(
        IReadOnlyList<WoundReactionExpansionProposal>? expansions,
        ImmutableArray<WoundConsequenceInputFault>.Builder faults)
    {
        if (expansions == null)
        {
            faults.Add(new WoundConsequenceInputFault("expansions", "null"));
            return ImmutableArray<WoundReactionExpansionProposal>.Empty;
        }

        if (expansions.Count >
            WoundConsequenceEnvelopeCatalog.MaximumReactionExpansionsPerEffectProposal)
        {
            faults.Add(new WoundConsequenceInputFault(
                "expansions",
                expansions.Count.ToString(CultureInfo.InvariantCulture),
                "wound_consequence_limit_exceeded",
                $"at most {WoundConsequenceEnvelopeCatalog.MaximumReactionExpansionsPerEffectProposal} flattened reaction expansions per effect proposal"));
            return ImmutableArray<WoundReactionExpansionProposal>.Empty;
        }

        var result = ImmutableArray.CreateBuilder<WoundReactionExpansionProposal>(
            expansions.Count);
        for (var index = 0; index < expansions.Count; index++)
        {
            if (expansions[index] is { } expansion)
            {
                result.Add(expansion);
            }
            else
            {
                faults.Add(new WoundConsequenceInputFault($"expansions[{index}]", "null"));
                result.Add(new WoundReactionExpansionProposal(null, null));
            }
        }

        return result.MoveToImmutable();
    }
}

internal sealed class WoundConsequenceEnvelopeRequest
{
    internal WoundConsequenceEnvelopeRequest(
        string? woundId,
        string? domain,
        string? severity,
        WoundConsequences? declaredConsequences,
        WoundConsequenceLifecycleEvidence? lifecycleEvidence,
        IReadOnlyList<WoundConsequenceEffectProposal>? effects,
        IReadOnlyList<WoundResourceEnvelopeBound>? resourceBounds)
    {
        WoundId = woundId ?? string.Empty;
        Domain = domain ?? string.Empty;
        Severity = severity ?? string.Empty;
        var faults = ImmutableArray.CreateBuilder<WoundConsequenceInputFault>();
        DeclaredConsequences = CloneDeclared(declaredConsequences, faults);
        if (lifecycleEvidence == null)
            faults.Add(new WoundConsequenceInputFault("lifecycleEvidence", "null"));
        LifecycleEvidence = lifecycleEvidence ?? new WoundConsequenceLifecycleEvidence(
            false,
            false,
            false);
        Effects = CloneEffects(effects, faults);
        ResourceBounds = CloneResourceBounds(resourceBounds, faults);
        InputFaults = faults.ToImmutable();
    }

    internal string WoundId { get; }

    internal string Domain { get; }

    internal string Severity { get; }

    internal WoundConsequences DeclaredConsequences { get; }

    internal WoundConsequenceLifecycleEvidence LifecycleEvidence { get; }

    internal ImmutableArray<WoundConsequenceEffectProposal> Effects { get; }

    internal ImmutableArray<WoundResourceEnvelopeBound> ResourceBounds { get; }

    internal ImmutableArray<WoundConsequenceInputFault> InputFaults { get; }

    private static WoundConsequences CloneDeclared(
        WoundConsequences? declared,
        ImmutableArray<WoundConsequenceInputFault>.Builder faults)
    {
        if (declared == null)
        {
            faults.Add(new WoundConsequenceInputFault("declared", "null"));
            return new WoundConsequences(0, 0, ImmutableArray<WoundConsequenceEntry>.Empty);
        }

        if (declared.Entries == null)
        {
            faults.Add(new WoundConsequenceInputFault("declared.entries", "null"));
            return new WoundConsequences(
                declared.SlotBudget,
                declared.SlotsUsed,
                ImmutableArray<WoundConsequenceEntry>.Empty);
        }

        if (declared.Entries.Count > WoundMaterializationContract.MaxConsequences)
        {
            faults.Add(new WoundConsequenceInputFault(
                "declared.entries",
                declared.Entries.Count.ToString(CultureInfo.InvariantCulture),
                "wound_consequence_limit_exceeded",
                $"at most {WoundMaterializationContract.MaxConsequences} consequences"));
            return new WoundConsequences(
                declared.SlotBudget,
                declared.SlotsUsed,
                ImmutableArray<WoundConsequenceEntry>.Empty);
        }

        var entries = ImmutableArray.CreateBuilder<WoundConsequenceEntry>(
            declared.Entries.Count);
        for (var index = 0; index < declared.Entries.Count; index++)
        {
            if (declared.Entries[index] is { } entry)
            {
                entries.Add(entry);
            }
            else
            {
                faults.Add(new WoundConsequenceInputFault(
                    $"declared.entries[{index}]",
                    "null"));
                entries.Add(new WoundConsequenceEntry(0, string.Empty, string.Empty, string.Empty));
            }
        }

        return new WoundConsequences(
            declared.SlotBudget,
            declared.SlotsUsed,
            entries.MoveToImmutable());
    }

    private static ImmutableArray<WoundConsequenceEffectProposal> CloneEffects(
        IReadOnlyList<WoundConsequenceEffectProposal>? effects,
        ImmutableArray<WoundConsequenceInputFault>.Builder faults)
    {
        if (effects == null)
        {
            faults.Add(new WoundConsequenceInputFault("effects", "null"));
            return ImmutableArray<WoundConsequenceEffectProposal>.Empty;
        }

        if (effects.Count > WoundConsequenceEnvelopeCatalog.MaximumEffectProposals)
        {
            faults.Add(new WoundConsequenceInputFault(
                "effects",
                effects.Count.ToString(CultureInfo.InvariantCulture),
                "wound_consequence_limit_exceeded",
                $"at most {WoundConsequenceEnvelopeCatalog.MaximumEffectProposals} effect proposals"));
            return ImmutableArray<WoundConsequenceEffectProposal>.Empty;
        }

        var result = ImmutableArray.CreateBuilder<WoundConsequenceEffectProposal>(effects.Count);
        for (var index = 0; index < effects.Count; index++)
        {
            if (effects[index] is { } effect)
            {
                result.Add(effect);
            }
            else
            {
                faults.Add(new WoundConsequenceInputFault($"effects[{index}]", "null"));
                result.Add(WoundConsequenceEffectProposal.Missing());
            }
        }

        return result.MoveToImmutable();
    }

    private static ImmutableArray<WoundResourceEnvelopeBound> CloneResourceBounds(
        IReadOnlyList<WoundResourceEnvelopeBound>? bounds,
        ImmutableArray<WoundConsequenceInputFault>.Builder faults)
    {
        if (bounds == null)
        {
            faults.Add(new WoundConsequenceInputFault("resourceBounds", "null"));
            return ImmutableArray<WoundResourceEnvelopeBound>.Empty;
        }

        if (bounds.Count > WoundConsequenceEnvelopeCatalog.MaximumResourceBounds)
        {
            faults.Add(new WoundConsequenceInputFault(
                "resourceBounds",
                bounds.Count.ToString(CultureInfo.InvariantCulture),
                "wound_consequence_limit_exceeded",
                $"at most {WoundConsequenceEnvelopeCatalog.MaximumResourceBounds} resource-bound evidence entries"));
            return ImmutableArray<WoundResourceEnvelopeBound>.Empty;
        }

        var result = ImmutableArray.CreateBuilder<WoundResourceEnvelopeBound>(bounds.Count);
        for (var index = 0; index < bounds.Count; index++)
        {
            if (bounds[index] is { } bound)
            {
                result.Add(bound);
            }
            else
            {
                faults.Add(new WoundConsequenceInputFault($"resourceBounds[{index}]", "null"));
                result.Add(new WoundResourceEnvelopeBound(string.Empty, 0m, 0m));
            }
        }

        return result.MoveToImmutable();
    }
}

internal sealed record WoundDerivedConsequenceSlot(
    int Slot,
    string EffectId,
    string ComponentId,
    string ProfileKey,
    string Axis,
    string OperationKey,
    string Coordinate);

internal sealed class WoundConsequenceEnvelope
{
    internal WoundConsequenceEnvelope(
        ImmutableArray<WoundDerivedConsequenceSlot> slots,
        ImmutableArray<WoundConsequenceEffectProposal> ownedEffects,
        bool preservesSafeExit,
        ImmutableArray<string> preservedSafetyOperations)
    {
        Slots = slots;
        OwnedEffects = ownedEffects;
        PreservesSafeExit = preservesSafeExit;
        PreservedSafetyOperations = preservedSafetyOperations;
    }

    internal int SlotsUsed => Slots.Length;

    internal ImmutableArray<WoundDerivedConsequenceSlot> Slots { get; }

    internal ImmutableArray<WoundConsequenceEffectProposal> OwnedEffects { get; }

    internal bool PreservesSafeExit { get; }

    internal ImmutableArray<string> PreservedSafetyOperations { get; }
}

internal sealed class WoundConsequenceEnvelopeValidationResult
{
    internal WoundConsequenceEnvelopeValidationResult(
        WoundConsequenceEnvelope? envelope,
        ImmutableArray<WoundConsequenceEffectProposal> independentEffects,
        ImmutableArray<ValidationIssue> issues)
    {
        Envelope = envelope;
        IndependentEffects = independentEffects;
        Issues = issues;
    }

    internal bool IsValid => Envelope != null && Issues.IsEmpty;

    internal WoundConsequenceEnvelope? Envelope { get; }

    internal ImmutableArray<WoundConsequenceEffectProposal> IndependentEffects { get; }

    internal ImmutableArray<ValidationIssue> Issues { get; }
}

internal sealed class WoundDetachedMortalEffectRef
{
    internal WoundDetachedMortalEffectRef(
        string effectRef,
        string authorPath,
        IReadOnlyList<JsonElement> components,
        IReadOnlyList<WoundDetachedMortalReactionExpansionRef>? reactionExpansions = null)
    {
        EffectRef = effectRef;
        AuthorPath = authorPath;
        Components = components;
        ReactionExpansions = reactionExpansions ??
            Array.Empty<WoundDetachedMortalReactionExpansionRef>();
    }

    internal string EffectRef { get; }

    internal string AuthorPath { get; }

    internal IReadOnlyList<JsonElement> Components { get; }

    internal IReadOnlyList<WoundDetachedMortalReactionExpansionRef> ReactionExpansions { get; }
}

internal sealed class WoundDetachedMortalReactionExpansionRef
{
    internal WoundDetachedMortalReactionExpansionRef(
        string reactionComponentId,
        string authorPath,
        IReadOnlyList<JsonElement> components,
        JsonElement parameters = default)
    {
        ReactionComponentId = reactionComponentId;
        AuthorPath = authorPath;
        Components = EffectComponentParameterBinder.Bind(components, parameters);
    }

    internal string ReactionComponentId { get; }

    internal string AuthorPath { get; }

    internal IReadOnlyList<JsonElement> Components { get; }
}

internal static class EffectComponentParameterBinder
{
    internal static ImmutableArray<JsonElement> Bind(
        IReadOnlyList<JsonElement> components,
        JsonElement parameters)
    {
        if (components == null)
            return ImmutableArray<JsonElement>.Empty;

        JsonObject? parameterObject = null;
        if (parameters.ValueKind == JsonValueKind.Object)
            parameterObject = JsonNode.Parse(parameters.GetRawText()) as JsonObject;

        var bound = ImmutableArray.CreateBuilder<JsonElement>(components.Count);
        for (var index = 0; index < components.Count; index++)
        {
            var component = components[index];
            if (component.ValueKind == JsonValueKind.Undefined)
            {
                bound.Add(default);
                continue;
            }

            var componentNode = JsonNode.Parse(component.GetRawText());
            if (componentNode is JsonObject componentObject &&
                componentObject["payload"] is JsonObject payload &&
                parameterObject != null)
            {
                foreach (var parameter in parameterObject)
                {
                    if (payload.ContainsKey(parameter.Key))
                        payload[parameter.Key] = parameter.Value?.DeepClone();
                }
            }

            bound.Add(JsonSerializer.SerializeToElement(componentNode));
        }

        return bound.MoveToImmutable();
    }
}

internal sealed class WoundDetachedMortalEnvelopeRequest
{
    internal WoundDetachedMortalEnvelopeRequest(
        int severityRank,
        string authorPath,
        IReadOnlyList<WoundDetachedMortalEffectRef> effects)
    {
        SeverityRank = severityRank;
        AuthorPath = authorPath;
        Effects = effects;
    }

    internal int SeverityRank { get; }

    internal string AuthorPath { get; }

    internal IReadOnlyList<WoundDetachedMortalEffectRef> Effects { get; }
}

internal sealed record WoundDetachedMortalEnvelopeSlot(
    int Slot,
    string EffectRef,
    string ComponentId,
    string ProfileKey,
    string Axis,
    string OperationKey,
    string Coordinate,
    string AuthorPath);

internal sealed class WoundDetachedMortalEnvelopeValidationResult
{
    internal WoundDetachedMortalEnvelopeValidationResult(
        ImmutableArray<WoundDetachedMortalEnvelopeSlot> slots,
        ImmutableArray<ValidationIssue> issues)
    {
        Slots = slots;
        Issues = issues;
    }

    internal bool IsValid => Issues.IsEmpty;

    internal ImmutableArray<WoundDetachedMortalEnvelopeSlot> Slots { get; }

    internal bool HasDeferredPeriodicAuthority => Slots.Any(static slot =>
        slot.ProfileKey is "periodic_damage" or "periodic_restore");

    internal ImmutableArray<ValidationIssue> Issues { get; }
}

internal static class WoundConsequenceEnvelopeCatalog
{
    internal const int MaximumEffectProposals = 128;

    internal const int MaximumComponentsPerEffect = 64;

    internal const int MaximumCadencesPerEffect = 64;

    internal const int MaximumReactionExpansionsPerEffectProposal = 64;

    internal const int MaximumReactionExpansionsPerWound = 1;

    internal const int MaximumReactionExpansionComponents = 64;

    internal const int MaximumResourceBounds = 128;

    private const int MaximumConsequenceSlots = 4;

    private static readonly ImmutableHashSet<string> MortalMechanicalProfileSet =
        ImmutableHashSet.Create(
            StringComparer.Ordinal,
            "characteristic_modifier",
            "roll_modifier",
            "resistance_modifier",
            "periodic_damage",
            "periodic_restore",
            "action_control",
            "event_reaction");

    private static readonly ImmutableHashSet<string> MortalZeroSlotProfileSet =
        ImmutableHashSet.Create(StringComparer.Ordinal, "wound_consequence");

    private static readonly ImmutableHashSet<string> MortalForbidActions =
        ImmutableHashSet.Create(StringComparer.Ordinal, "attack", "cast", "movement");

    private static readonly ImmutableArray<string> SafetyOperations =
        ImmutableArray.Create("communication", "exit", "help", "inspection", "treatment");

    internal static IReadOnlySet<string> MortalMechanicalProfiles =>
        MortalMechanicalProfileSet;

    internal static IReadOnlySet<string> MortalZeroSlotProfiles =>
        MortalZeroSlotProfileSet;

    internal static IReadOnlySet<string> SpiritualProfiles =>
        SpiritualWoundEffectProfileCatalog.RegisteredProfiles;

    internal static IReadOnlySet<string> SpiritualOperationKeys =>
        SpiritualWoundEffectProfileCatalog.Operations;

    internal static IReadOnlySet<string> SpiritualArtRestrictionKeys =>
        SpiritualWoundEffectProfileCatalog.ArtOperations;

    /// <summary>
    /// Validates local component shape and coordinates without a real wound rank.
    /// This does not establish severity, periodic, or canonical source authority.
    /// </summary>
    internal static WoundDetachedMortalEnvelopeValidationResult ValidateDetachedMortalShape(
        string authorPath,
        IReadOnlyList<WoundDetachedMortalEffectRef> effects)
    {
        ArgumentNullException.ThrowIfNull(effects);
        return ValidateDetachedMortalCore(null, authorPath, effects, new List<ValidationIssue>());
    }

    internal static WoundDetachedMortalEnvelopeValidationResult ValidateDetachedMortal(
        WoundDetachedMortalEnvelopeRequest request)
    {
        var issues = new List<ValidationIssue>();
        var path = request == null || string.IsNullOrWhiteSpace(request.AuthorPath)
            ? "wound.detachedConsequences"
            : request.AuthorPath;
        if (request == null)
        {
            Add(
                issues,
                path,
                "wound_consequence_input_invalid",
                "one non-null detached Mortal component envelope request",
                "null");
            return new WoundDetachedMortalEnvelopeValidationResult(
                ImmutableArray<WoundDetachedMortalEnvelopeSlot>.Empty,
                issues.ToImmutableArray());
        }

        if (request.SeverityRank is < 1 or > MaximumConsequenceSlots)
        {
            Add(
                issues,
                path,
                "wound_consequence_severity_invalid",
                "severity rank 1 | 2 | 3 | 4",
                request.SeverityRank.ToString(CultureInfo.InvariantCulture));
        }

        return ValidateDetachedMortalCore(request.SeverityRank, path, request.Effects, issues);
    }

    private static WoundDetachedMortalEnvelopeValidationResult ValidateDetachedMortalCore(
        int? severityRank,
        string authorPath,
        IReadOnlyList<WoundDetachedMortalEffectRef> effects,
        List<ValidationIssue> issues)
    {
        var path = string.IsNullOrWhiteSpace(authorPath) ? "wound.detachedConsequences" : authorPath;
        var candidates = new List<SlotCandidate>();
        var coordinates = new HashSet<string>(StringComparer.Ordinal);
        for (var effectIndex = 0; effectIndex < effects.Count; effectIndex++)
        {
            var effect = effects[effectIndex];
            var effectPath = string.IsNullOrWhiteSpace(effect.AuthorPath)
                ? $"{path}[{effectIndex}]"
                : effect.AuthorPath;
            var evidence = BuildDetachedMortalEvidenceIndex(effect, issues);
            var usedExpansions = new HashSet<int>();
            for (var componentIndex = 0;
                 componentIndex < effect.Components.Count;
                 componentIndex++)
            {
                ValidateDetachedMortalComponent(
                    severityRank,
                    effect.EffectRef,
                    evidence,
                    componentIndex,
                    effect.Components[componentIndex],
                    effectPath,
                    originPrefix: "0:",
                    allowReaction: true,
                    candidates,
                    coordinates,
                    usedExpansions,
                    issues);
            }

            for (var expansionIndex = 0;
                 expansionIndex < effect.ReactionExpansions.Count;
                 expansionIndex++)
            {
                if (!usedExpansions.Contains(expansionIndex))
                {
                    Add(
                        issues,
                        effect.ReactionExpansions[expansionIndex].AuthorPath,
                        "wound_consequence_reaction_expansion_invalid",
                        "one expansion bound to exactly one apply_definition component",
                        effect.ReactionExpansions[expansionIndex].ReactionComponentId);
                }
            }
        }

        return new WoundDetachedMortalEnvelopeValidationResult(
            DeriveDetachedSlots(candidates),
            issues.ToImmutableArray());
    }

    private static MortalEvidenceIndex BuildDetachedMortalEvidenceIndex(
        WoundDetachedMortalEffectRef effect,
        List<ValidationIssue> issues)
    {
        var expansions = new Dictionary<
            string,
            ImmutableArray<IndexedExpansion>.Builder>(StringComparer.Ordinal);
        for (var index = 0; index < effect.ReactionExpansions.Count; index++)
        {
            var expansion = effect.ReactionExpansions[index];
            var proposal = new WoundReactionExpansionProposal(
                expansion.ReactionComponentId,
                expansion.Components);
            foreach (var fault in proposal.InputFaults)
                AddInputFault(issues, expansion.AuthorPath + "." + fault.PathSuffix, fault);
            AddEvidence(
                expansions,
                expansion.ReactionComponentId,
                new IndexedExpansion(
                    index,
                    proposal,
                    expansion.AuthorPath));
        }

        return new MortalEvidenceIndex(
            new Dictionary<string, ImmutableArray<IndexedCadence>>(StringComparer.Ordinal),
            expansions.ToDictionary(
                static pair => pair.Key,
                static pair => pair.Value.ToImmutable(),
                StringComparer.Ordinal));
    }

    private static void ValidateDetachedMortalComponent(
        int? rank,
        string effectRef,
        MortalEvidenceIndex evidence,
        int componentIndex,
        JsonElement component,
        string componentContainerPath,
        string originPrefix,
        bool allowReaction,
        List<SlotCandidate> candidates,
        HashSet<string> coordinates,
        HashSet<int> usedExpansions,
        List<ValidationIssue> issues)
    {
        // Revalidate the registered component shape, including bound reaction children.
        // A null rank omits only real-severity predicates. Periodic amount percentage,
        // resource quantum, and cadence still require accepted runtime authority.
        var componentPath = $"{componentContainerPath}.components[{componentIndex}]";
        if (!ValidateNoDuplicateRawProperties(component, componentPath, issues))
            return;

        if (!TryReadString(component, "profile", out var profile) ||
            (!MortalMechanicalProfileSet.Contains(profile) &&
             !MortalZeroSlotProfileSet.Contains(profile)))
        {
            Add(
                issues,
                componentPath + ".profile",
                "wound_consequence_profile_unsupported",
                DescribeMortalProfiles(),
                DescribeProperty(component, "profile"));
            return;
        }

        if (!allowReaction && string.Equals(profile, "event_reaction", StringComparison.Ordinal))
        {
            Add(
                issues,
                componentPath + ".profile",
                "wound_consequence_reaction_expansion_invalid",
                "fully flattened non-reaction mechanical component",
                profile);
            return;
        }

        var genericIssueCount = issues.Count;
        EffectComponentProfiles.ValidateComponent(component, componentPath, issues);
        var genericValid = issues.Count == genericIssueCount;

        if (string.Equals(profile, "event_reaction", StringComparison.Ordinal))
        {
            ValidateReactionComponentCore(
                rank,
                effectRef,
                evidence,
                component,
                componentPath,
                originPrefix,
                genericValid,
                candidates,
                coordinates,
                usedExpansions,
                issues,
                (indexedExpansion, expansionComponentIndex) =>
                    ValidateDetachedMortalComponent(
                        rank,
                        effectRef,
                        evidence,
                        expansionComponentIndex,
                        indexedExpansion.Expansion.Components[expansionComponentIndex],
                        indexedExpansion.AuthorPath,
                        "1:" + component.GetProperty("componentId").GetString() + ":",
                        allowReaction: false,
                        candidates,
                        coordinates,
                        usedExpansions,
                        issues));
            return;
        }

        if ((!genericValid && profile is not ("periodic_damage" or "periodic_restore")) ||
            !TryReadString(component, "componentId", out var componentId) ||
            !component.TryGetProperty("payload", out var payload) ||
            payload.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        var originKey = originPrefix + componentId;
        switch (profile)
        {
            case "characteristic_modifier":
                ValidateScalarModifier(
                    rank,
                    effectRef,
                    componentId,
                    profile,
                    payload,
                    componentPath,
                    "characteristic",
                    originKey,
                    candidates,
                    coordinates,
                    issues);
                break;
            case "resistance_modifier":
                ValidateScalarModifier(
                    rank,
                    effectRef,
                    componentId,
                    profile,
                    payload,
                    componentPath,
                    "resistance",
                    originKey,
                    candidates,
                    coordinates,
                    issues);
                break;
            case "roll_modifier":
                ValidateRollModifier(
                    effectRef,
                    componentId,
                    payload,
                    componentPath,
                    originKey,
                    candidates,
                    coordinates,
                    issues);
                break;
            case "periodic_damage":
            case "periodic_restore":
                ValidatePeriodicCoordinate(
                    effectRef,
                    componentId,
                    profile,
                    payload,
                    componentPath,
                    originKey,
                    candidates,
                    coordinates,
                    issues);
                break;
            case "action_control":
                ValidateAction(
                    rank,
                    effectRef,
                    componentId,
                    payload,
                    componentPath,
                    originKey,
                    candidates,
                    coordinates,
                    issues);
                break;
        }
    }

    internal static WoundConsequenceEnvelopeValidationResult Validate(
        WoundConsequenceEnvelopeRequest request,
        string path)
    {
        path = string.IsNullOrWhiteSpace(path) ? "wound.consequences" : path;
        var issues = new List<ValidationIssue>();
        if (request == null)
        {
            Add(
                issues,
                path,
                "wound_consequence_input_invalid",
                "one non-null wound consequence envelope request",
                "null");
            return new WoundConsequenceEnvelopeValidationResult(
                null,
                ImmutableArray<WoundConsequenceEffectProposal>.Empty,
                issues.ToImmutableArray());
        }

        AddInputFaults(request, path, issues);
        ValidateIdentifiers(request, path, issues);
        var owned = new List<IndexedEffect>();
        var independent = new List<WoundConsequenceEffectProposal>();
        ClassifyEffects(request, path, owned, independent, issues);
        ValidateOwnedExpansionBound(owned, path, issues);

        var rank = SeverityRank(request.Severity);
        if (rank == 0)
        {
            Add(
                issues,
                path + ".severity",
                "wound_consequence_severity_invalid",
                "I | II | III | IV",
                request.Severity);
        }

        var resourceBounds = ValidateResourceBounds(request.ResourceBounds, path, issues);
        var candidates = new List<SlotCandidate>();
        var coordinates = new HashSet<string>(StringComparer.Ordinal);

        if (string.Equals(request.Domain, "physical", StringComparison.Ordinal))
        {
            ValidateMortalMarkerLimit(owned, path, issues);
            ValidateMortalEffects(
                request,
                rank,
                owned,
                resourceBounds,
                candidates,
                coordinates,
                path,
                issues);
        }
        else if (string.Equals(request.Domain, "spiritual", StringComparison.Ordinal))
        {
            ValidateSpiritualEffects(
                request,
                rank,
                owned,
                candidates,
                coordinates,
                path,
                issues);
        }
        else
        {
            Add(
                issues,
                path + ".domain",
                "wound_consequence_domain_invalid",
                "physical | spiritual",
                request.Domain);
        }

        var slots = DeriveSlots(candidates);
        ValidateSlotBudget(request, rank, slots, path, issues);

        var independentResult = independent
            .OrderBy(static effect => effect.EffectId, StringComparer.Ordinal)
            .ToImmutableArray();
        if (issues.Count != 0)
        {
            return new WoundConsequenceEnvelopeValidationResult(
                null,
                independentResult,
                issues.ToImmutableArray());
        }

        var envelope = new WoundConsequenceEnvelope(
            slots,
            owned.Select(static candidate => candidate.Effect)
                .OrderBy(static effect => effect.EffectId, StringComparer.Ordinal)
                .ToImmutableArray(),
            preservesSafeExit: true,
            SafetyOperations);
        return new WoundConsequenceEnvelopeValidationResult(
            envelope,
            independentResult,
            ImmutableArray<ValidationIssue>.Empty);
    }

    private static void AddInputFaults(
        WoundConsequenceEnvelopeRequest request,
        string path,
        List<ValidationIssue> issues)
    {
        foreach (var fault in request.InputFaults)
            AddInputFault(issues, path + "." + fault.PathSuffix, fault);

        for (var effectIndex = 0; effectIndex < request.Effects.Length; effectIndex++)
        {
            var effect = request.Effects[effectIndex];
            if (effect.IsMissing)
                continue;
            var effectPath = $"{path}.effects[{effectIndex}]";
            foreach (var fault in effect.InputFaults)
                AddInputFault(issues, effectPath + "." + fault.PathSuffix, fault);

            for (var expansionIndex = 0;
                 expansionIndex < effect.Expansions.Length;
                 expansionIndex++)
            {
                var expansionPath = $"{effectPath}.expansions[{expansionIndex}]";
                foreach (var fault in effect.Expansions[expansionIndex].InputFaults)
                    AddInputFault(issues, expansionPath + "." + fault.PathSuffix, fault);
            }
        }
    }

    private static void AddInputFault(
        List<ValidationIssue> issues,
        string path,
        WoundConsequenceInputFault fault) =>
        Add(
            issues,
            path,
            fault.Code,
            fault.Expected,
            fault.Actual);

    private static void ValidateIdentifiers(
        WoundConsequenceEnvelopeRequest request,
        string path,
        List<ValidationIssue> issues)
    {
        ValidateIdentifier(request.WoundId, path + ".woundId", issues);
        for (var entryIndex = 0;
             entryIndex < request.DeclaredConsequences.Entries.Count;
             entryIndex++)
        {
            ValidateIdentifier(
                request.DeclaredConsequences.Entries[entryIndex].EffectId,
                $"{path}.declared.entries[{entryIndex}].effectId",
                issues);
        }

        var effectIds = new HashSet<string>(StringComparer.Ordinal);
        var effectAliases = new HashSet<string>(StringComparer.Ordinal);
        for (var effectIndex = 0; effectIndex < request.Effects.Length; effectIndex++)
        {
            var effect = request.Effects[effectIndex];
            if (effect.IsMissing)
                continue;
            var effectPath = $"{path}.effects[{effectIndex}]";
            ValidateUniqueIdentifier(
                effect.EffectId,
                effectPath + ".effectId",
                "wound_consequence_effect_identity_duplicate",
                effectIds,
                effectAliases,
                issues);
            ValidateIdentifier(effect.SourceKind, effectPath + ".sourceKind", issues);
            ValidateIdentifier(effect.SourceId, effectPath + ".sourceId", issues);
            if (effect.ReciprocalWoundId != null)
            {
                ValidateIdentifier(
                    effect.ReciprocalWoundId,
                    effectPath + ".reciprocalWoundId",
                    issues);
            }

            var componentIds = new HashSet<string>(StringComparer.Ordinal);
            var componentAliases = new HashSet<string>(StringComparer.Ordinal);
            ValidateComponentIdentities(
                effect.Components,
                effectPath + ".components",
                componentIds,
                componentAliases,
                issues);

            for (var cadenceIndex = 0;
                 cadenceIndex < effect.Cadences.Length;
                 cadenceIndex++)
            {
                var cadence = effect.Cadences[cadenceIndex];
                var cadencePath = $"{effectPath}.cadences[{cadenceIndex}]";
                ValidateIdentifier(cadence.ComponentId, cadencePath + ".componentId", issues);
                ValidateIdentifier(cadence.SourceEvent, cadencePath + ".sourceEvent", issues);
            }

            for (var expansionIndex = 0;
                 expansionIndex < effect.Expansions.Length;
                 expansionIndex++)
            {
                var expansion = effect.Expansions[expansionIndex];
                var expansionPath = $"{effectPath}.expansions[{expansionIndex}]";
                ValidateIdentifier(
                    expansion.ReactionComponentId,
                    expansionPath + ".reactionComponentId",
                    issues);
                ValidateComponentIdentities(
                    expansion.Components,
                    expansionPath + ".components",
                    componentIds,
                    componentAliases,
                    issues);
            }
        }

        for (var boundIndex = 0;
             boundIndex < request.ResourceBounds.Length;
             boundIndex++)
        {
            ValidateIdentifier(
                request.ResourceBounds[boundIndex].ResourceKey,
                $"{path}.resourceBounds[{boundIndex}].resourceKey",
                issues);
        }
    }

    private static void ValidateUniqueIdentifier(
        string? value,
        string path,
        string duplicateCode,
        HashSet<string> exactValues,
        HashSet<string> aliases,
        List<ValidationIssue> issues)
    {
        if (!ResourceMaterializationContract.IsExactIdentifier(value))
        {
            ValidateIdentifier(value, path, issues);
            return;
        }

        if (!exactValues.Add(value!) ||
            !aliases.Add(ResourceMaterializationContract.BuildConfusableKey(value!)))
        {
            Add(
                issues,
                path,
                duplicateCode,
                "one exact and confusable-unique identifier",
                value!);
        }
    }

    private static void ValidateComponentIdentities(
        ImmutableArray<JsonElement> components,
        string path,
        HashSet<string> exactValues,
        HashSet<string> aliases,
        List<ValidationIssue> issues)
    {
        if (components.IsEmpty)
        {
            Add(
                issues,
                path,
                "wound_consequence_components_required",
                "at least one bounded consequence component",
                "0");
            return;
        }

        for (var index = 0; index < components.Length; index++)
        {
            var component = components[index];
            var componentPath = $"{path}[{index}]";
            ValidateNoDuplicateRawProperties(component, componentPath, issues);

            string? componentId = null;
            if (component.ValueKind == JsonValueKind.Object &&
                component.TryGetProperty("componentId", out var rawComponentId) &&
                rawComponentId.ValueKind == JsonValueKind.String)
            {
                componentId = rawComponentId.GetString();
            }

            if (!ResourceMaterializationContract.IsExactIdentifier(componentId))
            {
                Add(
                    issues,
                    componentPath + ".componentId",
                    "wound_consequence_identifier_invalid",
                    "exact non-empty normalized identifier without surrounding whitespace",
                    DescribeProperty(component, "componentId"));
                continue;
            }

            if (!exactValues.Add(componentId!) ||
                !aliases.Add(ResourceMaterializationContract.BuildConfusableKey(componentId!)))
            {
                Add(
                    issues,
                    componentPath + ".componentId",
                    "wound_consequence_component_identity_duplicate",
                    "one exact and confusable-unique component identifier within the effect",
                    componentId!);
            }
        }
    }

    private static bool ValidateNoDuplicateRawProperties(
        JsonElement value,
        string path,
        List<ValidationIssue> issues)
    {
        var valid = true;
        if (value.ValueKind == JsonValueKind.Object)
        {
            var propertyNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
            {
                var propertyPath = path + "." + property.Name;
                if (!propertyNames.Add(property.Name))
                {
                    Add(
                        issues,
                        propertyPath,
                        "wound_consequence_duplicate_property",
                        "one occurrence of each exact JSON object property",
                        property.Name);
                    valid = false;
                }

                valid = ValidateNoDuplicateRawProperties(
                    property.Value,
                    propertyPath,
                    issues) && valid;
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var item in value.EnumerateArray())
            {
                valid = ValidateNoDuplicateRawProperties(
                    item,
                    $"{path}[{index}]",
                    issues) && valid;
                index++;
            }
        }

        return valid;
    }

    private static bool ContainsDuplicateRawProperties(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var propertyNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
            {
                if (!propertyNames.Add(property.Name) ||
                    ContainsDuplicateRawProperties(property.Value))
                {
                    return true;
                }
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in value.EnumerateArray())
            {
                if (ContainsDuplicateRawProperties(item))
                    return true;
            }
        }

        return false;
    }

    private static void ValidateIdentifier(
        string? value,
        string path,
        List<ValidationIssue> issues)
    {
        if (!ResourceMaterializationContract.IsExactIdentifier(value))
        {
            Add(
                issues,
                path,
                "wound_consequence_identifier_invalid",
                "exact non-empty normalized identifier without surrounding whitespace",
                value ?? "null");
        }
    }

    private static void ClassifyEffects(
        WoundConsequenceEnvelopeRequest request,
        string path,
        List<IndexedEffect> owned,
        List<WoundConsequenceEffectProposal> independent,
        List<ValidationIssue> issues)
    {
        for (var index = 0; index < request.Effects.Length; index++)
        {
            var effect = request.Effects[index];
            if (effect.IsMissing)
                continue;
            var sourceMatches =
                string.Equals(effect.SourceKind, "wound", StringComparison.Ordinal) &&
                string.Equals(effect.SourceId, request.WoundId, StringComparison.Ordinal);
            var reciprocalMatches = string.Equals(
                effect.ReciprocalWoundId,
                request.WoundId,
                StringComparison.Ordinal);

            if (sourceMatches && reciprocalMatches)
            {
                owned.Add(new IndexedEffect(index, effect));
                continue;
            }

            if (sourceMatches || reciprocalMatches)
            {
                var issuePath = sourceMatches
                    ? $"{path}.effects[{index}].reciprocalWoundId"
                    : $"{path}.effects[{index}].sourceId";
                Add(
                    issues,
                    issuePath,
                    "wound_consequence_binding_invalid",
                    "exact sourceKind=wound, sourceId, and reciprocalWoundId agreement",
                    $"{effect.SourceKind}:{effect.SourceId}:{effect.ReciprocalWoundId ?? "null"}");
                continue;
            }

            independent.Add(effect);
        }
    }

    private static void ValidateOwnedExpansionBound(
        IReadOnlyList<IndexedEffect> owned,
        string path,
        List<ValidationIssue> issues)
    {
        var total = 0;
        foreach (var indexed in owned)
            total += indexed.Effect.Expansions.Length;

        if (total > MaximumReactionExpansionsPerWound)
        {
            Add(
                issues,
                path + ".effects",
                "wound_consequence_limit_exceeded",
                $"at most {MaximumReactionExpansionsPerWound} wound-owned flattened reaction expansion",
                total.ToString(CultureInfo.InvariantCulture));
        }
    }

    private static void ValidateMortalMarkerLimit(
        IReadOnlyList<IndexedEffect> owned,
        string path,
        List<ValidationIssue> issues)
    {
        var markerCount = 0;
        foreach (var indexed in owned)
        {
            markerCount += CountMortalMarkers(indexed.Effect.Components);
            foreach (var expansion in indexed.Effect.Expansions)
                markerCount += CountMortalMarkers(expansion.Components);
        }

        if (markerCount > 1)
        {
            Add(
                issues,
                path + ".effects",
                "wound_consequence_marker_limit_exceeded",
                "at most one wound_consequence marker across the wound-owned effect set",
                markerCount.ToString(CultureInfo.InvariantCulture));
        }
    }

    private static int CountMortalMarkers(ImmutableArray<JsonElement> components)
    {
        var count = 0;
        foreach (var component in components)
        {
            if (TryReadString(component, "profile", out var profile) &&
                string.Equals(profile, "wound_consequence", StringComparison.Ordinal))
            {
                count++;
            }
        }

        return count;
    }

    private static Dictionary<string, WoundResourceEnvelopeBound> ValidateResourceBounds(
        ImmutableArray<WoundResourceEnvelopeBound> bounds,
        string path,
        List<ValidationIssue> issues)
    {
        var result = new Dictionary<string, WoundResourceEnvelopeBound>(StringComparer.Ordinal);
        for (var index = 0; index < bounds.Length; index++)
        {
            var bound = bounds[index];
            var boundPath = $"{path}.resourceBounds[{index}]";
            var valid =
                ResourceMaterializationContract.IsExactIdentifier(bound.ResourceKey) &&
                bound.Maximum > 0m &&
                bound.Quantum > 0m &&
                ResourceMaterializationContract.IsQuantumAligned(
                    bound.Maximum,
                    0m,
                    bound.Quantum) &&
                !result.ContainsKey(bound.ResourceKey);
            if (!valid)
            {
                Add(
                    issues,
                    boundPath,
                    "wound_consequence_resource_bound_invalid",
                    "one unique exact resource with positive quantum-aligned accepted maximum",
                    $"{bound.ResourceKey}:{bound.Maximum.ToString(CultureInfo.InvariantCulture)}:{bound.Quantum.ToString(CultureInfo.InvariantCulture)}");
                continue;
            }

            result.Add(bound.ResourceKey, bound);
        }

        return result;
    }

    private static int SeverityRank(string severity) => severity switch
    {
        "I" => 1,
        "II" => 2,
        "III" => 3,
        "IV" => 4,
        _ => 0
    };

    private static decimal PercentageLimit(int rank) => rank switch
    {
        1 => 5m,
        2 => 10m,
        3 => 20m,
        4 => 30m,
        _ => 0m
    };

    private static MortalEvidenceIndex BuildMortalEvidenceIndex(
        WoundConsequenceEffectProposal effect,
        string effectPath)
    {
        var cadences = new Dictionary<
            string,
            ImmutableArray<IndexedCadence>.Builder>(StringComparer.Ordinal);
        for (var index = 0; index < effect.Cadences.Length; index++)
        {
            AddEvidence(
                cadences,
                effect.Cadences[index].ComponentId,
                new IndexedCadence(index, effect.Cadences[index]));
        }

        var expansions = new Dictionary<
            string,
            ImmutableArray<IndexedExpansion>.Builder>(StringComparer.Ordinal);
        for (var index = 0; index < effect.Expansions.Length; index++)
        {
            AddEvidence(
                expansions,
                effect.Expansions[index].ReactionComponentId,
                new IndexedExpansion(
                    index,
                    effect.Expansions[index],
                    $"{effectPath}.expansions[{index}]"));
        }

        return new MortalEvidenceIndex(
            cadences.ToDictionary(
                static pair => pair.Key,
                static pair => pair.Value.ToImmutable(),
                StringComparer.Ordinal),
            expansions.ToDictionary(
                static pair => pair.Key,
                static pair => pair.Value.ToImmutable(),
                StringComparer.Ordinal));
    }

    private static void AddEvidence<T>(
        Dictionary<string, ImmutableArray<T>.Builder> index,
        string? key,
        T value)
    {
        key ??= string.Empty;
        if (!index.TryGetValue(key, out var entries))
        {
            entries = ImmutableArray.CreateBuilder<T>();
            index.Add(key, entries);
        }

        entries.Add(value);
    }

    private static void ValidateMortalEffects(
        WoundConsequenceEnvelopeRequest request,
        int rank,
        IReadOnlyList<IndexedEffect> owned,
        IReadOnlyDictionary<string, WoundResourceEnvelopeBound> resourceBounds,
        List<SlotCandidate> candidates,
        HashSet<string> coordinates,
        string path,
        List<ValidationIssue> issues)
    {
        foreach (var indexed in owned)
        {
            var effect = indexed.Effect;
            var effectPath = $"{path}.effects[{indexed.Index}]";
            var usedExpansions = new HashSet<int>();
            var cadenceUseCounts = new int[effect.Cadences.Length];
            var evidence = BuildMortalEvidenceIndex(effect, effectPath);

            for (var componentIndex = 0;
                 componentIndex < effect.Components.Length;
                 componentIndex++)
            {
                ValidateMortalComponent(
                    request,
                    rank,
                    indexed,
                    evidence,
                    componentIndex,
                    effect.Components[componentIndex],
                    effectPath,
                    effectPath,
                    originPrefix: "0:",
                    allowReaction: true,
                    resourceBounds,
                    candidates,
                    coordinates,
                    usedExpansions,
                    cadenceUseCounts,
                    issues);
            }

            for (var cadenceIndex = 0;
                 cadenceIndex < effect.Cadences.Length;
                 cadenceIndex++)
            {
                if (cadenceUseCounts[cadenceIndex] == 1)
                    continue;

                Add(
                    issues,
                    $"{effectPath}.cadences[{cadenceIndex}]",
                    "wound_consequence_periodic_cadence_invalid",
                    "cadence consumed by exactly one periodic component",
                    cadenceUseCounts[cadenceIndex].ToString(CultureInfo.InvariantCulture));
            }

            for (var expansionIndex = 0;
                 expansionIndex < effect.Expansions.Length;
                 expansionIndex++)
            {
                if (!usedExpansions.Contains(expansionIndex))
                {
                    Add(
                        issues,
                        $"{effectPath}.expansions[{expansionIndex}]",
                        "wound_consequence_reaction_expansion_invalid",
                        "one expansion bound to exactly one apply_definition component",
                        effect.Expansions[expansionIndex].ReactionComponentId);
                }
            }
        }
    }

    private static void ValidateMortalComponent(
        WoundConsequenceEnvelopeRequest request,
        int rank,
        IndexedEffect indexed,
        MortalEvidenceIndex evidence,
        int componentIndex,
        JsonElement component,
        string componentContainerPath,
        string evidenceEffectPath,
        string originPrefix,
        bool allowReaction,
        IReadOnlyDictionary<string, WoundResourceEnvelopeBound> resourceBounds,
        List<SlotCandidate> candidates,
        HashSet<string> coordinates,
        HashSet<int> usedExpansions,
        int[] cadenceUseCounts,
        List<ValidationIssue> issues)
    {
        var componentPath = $"{componentContainerPath}.components[{componentIndex}]";
        if (ContainsDuplicateRawProperties(component))
            return;

        if (!TryReadString(component, "profile", out var profile))
        {
            Add(
                issues,
                componentPath + ".profile",
                "wound_consequence_profile_unsupported",
                DescribeMortalProfiles(),
                DescribeProperty(component, "profile"));
            return;
        }

        if (!MortalMechanicalProfileSet.Contains(profile) &&
            !MortalZeroSlotProfileSet.Contains(profile))
        {
            Add(
                issues,
                componentPath + ".profile",
                "wound_consequence_profile_unsupported",
                DescribeMortalProfiles(),
                profile);
            return;
        }

        if (!allowReaction && string.Equals(profile, "event_reaction", StringComparison.Ordinal))
        {
            Add(
                issues,
                componentPath + ".profile",
                "wound_consequence_reaction_expansion_invalid",
                "fully flattened non-reaction mechanical component",
                profile);
            return;
        }

        var genericIssueCount = issues.Count;
        EffectComponentProfiles.ValidateComponent(component, componentPath, issues);
        var genericValid = issues.Count == genericIssueCount;

        if (string.Equals(profile, "event_reaction", StringComparison.Ordinal))
        {
            ValidateReactionComponent(
                request,
                rank,
                indexed,
                evidence,
                componentIndex,
                component,
                componentPath,
                evidenceEffectPath,
                originPrefix,
                genericValid,
                resourceBounds,
                candidates,
                coordinates,
                usedExpansions,
                cadenceUseCounts,
                issues);
            return;
        }

        if ((!genericValid && profile is not ("periodic_damage" or "periodic_restore")) ||
            !TryReadString(component, "componentId", out var componentId) ||
            !component.TryGetProperty("payload", out var payload) ||
            payload.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        var originKey = originPrefix + componentId;
        switch (profile)
        {
            case "characteristic_modifier":
                ValidateScalarModifier(
                    rank,
                    indexed.Effect.EffectId,
                    componentId,
                    profile,
                    payload,
                    componentPath,
                    "characteristic",
                    originKey,
                    candidates,
                    coordinates,
                    issues);
                break;
            case "resistance_modifier":
                ValidateScalarModifier(
                    rank,
                    indexed.Effect.EffectId,
                    componentId,
                    profile,
                    payload,
                    componentPath,
                    "resistance",
                    originKey,
                    candidates,
                    coordinates,
                    issues);
                break;
            case "roll_modifier":
                ValidateRollModifier(
                    indexed.Effect.EffectId,
                    componentId,
                    payload,
                    componentPath,
                    originKey,
                    candidates,
                    coordinates,
                    issues);
                break;
            case "periodic_damage":
            case "periodic_restore":
                ValidatePeriodic(
                    rank,
                    evidence,
                    indexed.Effect.EffectId,
                    componentId,
                    profile,
                    payload,
                    componentPath,
                    evidenceEffectPath,
                    originKey,
                    resourceBounds,
                    candidates,
                    coordinates,
                    cadenceUseCounts,
                    issues);
                break;
            case "action_control":
                ValidateAction(
                    rank,
                    indexed.Effect.EffectId,
                    componentId,
                    payload,
                    componentPath,
                    originKey,
                    candidates,
                    coordinates,
                    issues);
                break;
            case "wound_consequence":
                if (!TryReadString(payload, "woundId", out var markerWoundId) ||
                    !string.Equals(markerWoundId, request.WoundId, StringComparison.Ordinal))
                {
                    Add(
                        issues,
                        componentPath + ".payload.woundId",
                        "wound_consequence_binding_invalid",
                        request.WoundId,
                        DescribeProperty(payload, "woundId"));
                }
                break;
        }
    }

    private static void ValidateScalarModifier(
        int? rank,
        string effectId,
        string componentId,
        string profile,
        JsonElement payload,
        string componentPath,
        string targetField,
        string originKey,
        List<SlotCandidate> candidates,
        HashSet<string> coordinates,
        List<ValidationIssue> issues)
    {
        if (!TryReadString(payload, targetField, out var target) ||
            !TryReadString(payload, "operation", out var operation))
        {
            return;
        }

        if (!payload.TryGetProperty("value", out var rawValue) ||
            !ResourceMaterializationContract.TryReadExactDecimal(rawValue, out var value))
        {
            Add(
                issues,
                componentPath + ".payload.value",
                "wound_consequence_magnitude_exceeded",
                "one exact nonzero decimal wound modifier",
                DescribeProperty(payload, "value"));
            return;
        }

        decimal? limit = rank is int actualRank
            ? (string.Equals(operation, "percent", StringComparison.Ordinal)
                ? PercentageLimit(actualRank)
                : actualRank)
            : null;
        var effective = value;
        var effectivePath = componentPath + ".payload.value";
        if (payload.TryGetProperty("cap", out var cap) &&
            cap.ValueKind == JsonValueKind.Object)
        {
            var exactCap = true;
            var minimum = 0m;
            if (!cap.TryGetProperty("minimum", out var rawMinimum) ||
                !ResourceMaterializationContract.TryReadExactDecimal(rawMinimum, out minimum))
            {
                Add(
                    issues,
                    componentPath + ".payload.cap.minimum",
                    "wound_consequence_magnitude_exceeded",
                    "one exact decimal cap endpoint",
                    DescribeProperty(cap, "minimum"));
                exactCap = false;
            }

            var maximum = 0m;
            if (!cap.TryGetProperty("maximum", out var rawMaximum) ||
                !ResourceMaterializationContract.TryReadExactDecimal(rawMaximum, out maximum))
            {
                Add(
                    issues,
                    componentPath + ".payload.cap.maximum",
                    "wound_consequence_magnitude_exceeded",
                    "one exact decimal cap endpoint",
                    DescribeProperty(cap, "maximum"));
                exactCap = false;
            }

            if (!exactCap)
                return;

            effective = Math.Max(effective, minimum);
            effective = Math.Min(effective, maximum);
            effectivePath = componentPath + ".payload.cap";
        }

        if (effective == 0m || (limit.HasValue && !WithinAbsoluteLimit(effective, limit.Value)))
        {
            Add(
                issues,
                effectivePath,
                "wound_consequence_magnitude_exceeded",
                limit is decimal actualLimit
                    ? $"nonzero effective absolute {operation} modifier <= {actualLimit.ToString(CultureInfo.InvariantCulture)} at severity rank {rank}"
                    : "one exact nonzero effective decimal wound modifier",
                effective.ToString(CultureInfo.InvariantCulture));
            return;
        }

        AddSlot(
            candidates,
            coordinates,
            new SlotCandidate(
                effectId,
                componentId,
                profile,
                targetField,
                target,
                $"{profile}:{targetField}:{target}",
                originKey,
                componentPath + $".payload.{targetField}"),
            componentPath + $".payload.{targetField}",
            issues);
    }

    private static void ValidateRollModifier(
        string effectId,
        string componentId,
        JsonElement payload,
        string componentPath,
        string originKey,
        List<SlotCandidate> candidates,
        HashSet<string> coordinates,
        List<ValidationIssue> issues)
    {
        if (!payload.TryGetProperty("operations", out var operations) ||
            operations.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        if (!payload.TryGetProperty("scope", out var scope) ||
            scope.ValueKind != JsonValueKind.Object ||
            !TryReadString(scope, "kind", out var scopeKind))
        {
            return;
        }

        string scopeCoordinate;
        if (string.Equals(scopeKind, "all", StringComparison.Ordinal))
        {
            scopeCoordinate = "all";
        }
        else if (string.Equals(scopeKind, "skill", StringComparison.Ordinal) &&
                 TryReadString(scope, "skillId", out var skillId))
        {
            scopeCoordinate = "skill:" + skillId;
        }
        else
        {
            return;
        }

        var operationRows = new List<(string Operation, int Index)>();
        var index = 0;
        foreach (var operationElement in operations.EnumerateArray())
        {
            if (operationElement.ValueKind == JsonValueKind.String &&
                operationElement.GetString() is string operation)
            {
                operationRows.Add((operation, index));
            }

            index++;
        }

        if (operationRows.Count == 0)
            return;

        var operationCoordinates = operationRows
            .Select(row => new
            {
                row.Operation,
                row.Index,
                Coordinate = $"roll_modifier:{row.Operation}:{scopeCoordinate}"
            })
            .ToArray();
        var hasDuplicateCoordinate = false;
        foreach (var row in operationCoordinates)
        {
            if (!coordinates.Contains(row.Coordinate))
                continue;

            Add(
                issues,
                $"{componentPath}.payload.operations[{row.Index}]",
                "wound_consequence_duplicate_coordinate",
                "one independently understandable slot per exact mechanical coordinate",
                row.Coordinate);
            hasDuplicateCoordinate = true;
        }

        if (hasDuplicateCoordinate)
            return;

        foreach (var row in operationCoordinates)
            coordinates.Add(row.Coordinate);

        var operationKey = string.Join(
            "+",
            operationRows.Select(static row => row.Operation));
        candidates.Add(new SlotCandidate(
            effectId,
            componentId,
            "roll_modifier",
            "rollMode",
            operationKey,
            $"roll_modifier:{operationKey}:{scopeCoordinate}",
            originKey,
            componentPath + ".payload.operations"));
    }

    private static void ValidatePeriodic(
        int rank,
        MortalEvidenceIndex evidence,
        string effectId,
        string componentId,
        string profile,
        JsonElement payload,
        string componentPath,
        string effectPath,
        string originKey,
        IReadOnlyDictionary<string, WoundResourceEnvelopeBound> resourceBounds,
        List<SlotCandidate> candidates,
        HashSet<string> coordinates,
        int[] cadenceUseCounts,
        List<ValidationIssue> issues)
    {
        if (!TryReadString(payload, "resource", out var resource))
            return;

        if (!resourceBounds.TryGetValue(resource, out var bound))
        {
            Add(
                issues,
                componentPath + ".payload.resource",
                "wound_consequence_resource_bound_missing",
                "one accepted exact maximum and quantum for this resource",
                resource);
            return;
        }

        if (!payload.TryGetProperty("amount", out var rawAmount) ||
            !ResourceMaterializationContract.TryReadExactDecimal(rawAmount, out var amount) ||
            amount <= 0m ||
            !ResourceMaterializationContract.IsQuantumAligned(amount, 0m, bound.Quantum) ||
            !IsAtMostPercentage(amount, bound.Maximum, PercentageLimit(rank)))
        {
            Add(
                issues,
                componentPath + ".payload.amount",
                "wound_consequence_periodic_amount_invalid",
                $"positive quantum-aligned amount <= {PercentageLimit(rank).ToString(CultureInfo.InvariantCulture)}% of accepted maximum without upward rounding",
                DescribeProperty(payload, "amount"));
            return;
        }

        if (!evidence.CadencesByComponentId.TryGetValue(componentId, out var cadences))
        {
            Add(
                issues,
                componentPath,
                "wound_consequence_periodic_cadence_invalid",
                "one cadence of at most one execution for each accepted source event",
                "missing cadence evidence");
            return;
        }

        var seenEvents = new HashSet<string>(StringComparer.Ordinal);
        var cadenceValid = true;
        foreach (var indexedCadence in cadences)
        {
            cadenceUseCounts[indexedCadence.Index]++;
            var cadence = indexedCadence.Cadence;
            var cadencePath = $"{effectPath}.cadences[{indexedCadence.Index}]";
            if (!EffectEventTypeCatalog.IsRegistered(cadence.SourceEvent))
            {
                Add(
                    issues,
                    cadencePath + ".sourceEvent",
                    "wound_consequence_periodic_cadence_invalid",
                    "one registered source event",
                    cadence.SourceEvent);
                cadenceValid = false;
            }
            else if (!seenEvents.Add(cadence.SourceEvent))
            {
                Add(
                    issues,
                    cadencePath,
                    "wound_consequence_periodic_cadence_invalid",
                    "one unique accepted source event per cadence entry",
                    cadence.SourceEvent);
                cadenceValid = false;
            }

            if (cadence.MaximumExecutionsPerSourceEvent != 1)
            {
                Add(
                    issues,
                    cadencePath + ".maximumExecutionsPerSourceEvent",
                    "wound_consequence_periodic_cadence_invalid",
                    "exactly one maximum execution per accepted source event",
                    cadence.MaximumExecutionsPerSourceEvent.ToString(CultureInfo.InvariantCulture));
                cadenceValid = false;
            }
        }

        if (!cadenceValid)
            return;

        AddPeriodicSlot(
            effectId,
            componentId,
            profile,
            resource,
            componentPath,
            originKey,
            candidates,
            coordinates,
            issues);
    }

    private static void ValidatePeriodicCoordinate(
        string effectRef,
        string componentId,
        string profile,
        JsonElement payload,
        string componentPath,
        string originKey,
        List<SlotCandidate> candidates,
        HashSet<string> coordinates,
        List<ValidationIssue> issues)
    {
        if (!TryReadString(payload, "resource", out var resource))
            return;

        AddPeriodicSlot(
            effectRef,
            componentId,
            profile,
            resource,
            componentPath,
            originKey,
            candidates,
            coordinates,
            issues);
    }

    private static void AddPeriodicSlot(
        string effectRef,
        string componentId,
        string profile,
        string resource,
        string componentPath,
        string originKey,
        List<SlotCandidate> candidates,
        HashSet<string> coordinates,
        List<ValidationIssue> issues) =>
        AddSlot(
            candidates,
            coordinates,
            new SlotCandidate(
                effectRef,
                componentId,
                profile,
                "resource",
                resource,
                $"{profile}:resource:{resource}",
                originKey,
                componentPath + ".payload.resource"),
            componentPath + ".payload.resource",
            issues);

    private static void ValidateAction(
        int? rank,
        string effectId,
        string componentId,
        JsonElement payload,
        string componentPath,
        string originKey,
        List<SlotCandidate> candidates,
        HashSet<string> coordinates,
        List<ValidationIssue> issues)
    {
        if (!TryReadString(payload, "action", out var action) ||
            !TryReadString(payload, "operation", out var operation))
        {
            return;
        }

        if (string.Equals(operation, "cost_modifier", StringComparison.Ordinal))
        {
            if (!payload.TryGetProperty("modifier", out var rawModifier) ||
                !ResourceMaterializationContract.TryReadExactDecimal(
                    rawModifier,
                    out var modifier) ||
                (rank is int actualRank && !WithinAbsoluteLimit(modifier, actualRank)))
            {
                Add(
                    issues,
                    componentPath + ".payload.modifier",
                    "wound_consequence_magnitude_exceeded",
                    rank.HasValue
                        ? $"absolute action cost modifier <= {rank}"
                        : "one exact decimal action cost modifier",
                    DescribeProperty(payload, "modifier"));
                return;
            }
        }
        else if (payload.TryGetProperty("modifier", out var nonCostModifier) &&
                 nonCostModifier.ValueKind != JsonValueKind.Null)
        {
            Add(
                issues,
                componentPath + ".payload.modifier",
                "wound_consequence_magnitude_exceeded",
                "null modifier outside cost_modifier",
                nonCostModifier.GetRawText());
            return;
        }

        if (string.Equals(operation, "forbid", StringComparison.Ordinal))
        {
            if (rank is int forbidRank && forbidRank < 3)
            {
                Add(
                    issues,
                    componentPath + ".payload.operation",
                    "wound_consequence_action_forbid_invalid",
                    "forbid available only at severity III-IV",
                    forbidRank.ToString(CultureInfo.InvariantCulture));
                return;
            }

            if (!MortalForbidActions.Contains(action))
            {
                Add(
                    issues,
                    componentPath + ".payload.action",
                    "wound_consequence_action_forbid_invalid",
                    "attack | cast | movement",
                    action);
                return;
            }
        }

        AddSlot(
            candidates,
            coordinates,
            new SlotCandidate(
                effectId,
                componentId,
                "action_control",
                "action",
                action,
                "action_control:action:" + action,
                originKey,
                componentPath + ".payload.action"),
            componentPath + ".payload.action",
            issues);
    }

    private static void ValidateReactionComponent(
        WoundConsequenceEnvelopeRequest request,
        int rank,
        IndexedEffect indexed,
        MortalEvidenceIndex evidence,
        int componentIndex,
        JsonElement component,
        string componentPath,
        string effectPath,
        string originPrefix,
        bool genericValid,
        IReadOnlyDictionary<string, WoundResourceEnvelopeBound> resourceBounds,
        List<SlotCandidate> candidates,
        HashSet<string> coordinates,
        HashSet<int> usedExpansions,
        int[] cadenceUseCounts,
        List<ValidationIssue> issues)
    {
        ValidateReactionComponentCore(
            rank,
            indexed.Effect.EffectId,
            evidence,
            component,
            componentPath,
            originPrefix,
            genericValid,
            candidates,
            coordinates,
            usedExpansions,
            issues,
            (indexedExpansion, expansionComponentIndex) => ValidateMortalComponent(
                request,
                rank,
                indexed,
                evidence,
                expansionComponentIndex,
                indexedExpansion.Expansion.Components[expansionComponentIndex],
                indexedExpansion.AuthorPath,
                effectPath,
                "1:" + component.GetProperty("componentId").GetString() + ":",
                allowReaction: false,
                resourceBounds,
                candidates,
                coordinates,
                usedExpansions,
                cadenceUseCounts,
                issues));
    }

    private static void ValidateReactionComponentCore(
        int? rank,
        string effectRef,
        MortalEvidenceIndex evidence,
        JsonElement component,
        string componentPath,
        string originPrefix,
        bool genericValid,
        List<SlotCandidate> candidates,
        HashSet<string> coordinates,
        HashSet<int> usedExpansions,
        List<ValidationIssue> issues,
        Action<IndexedExpansion, int> validateExpansionComponent)
    {
        if (!component.TryGetProperty("payload", out var payload) ||
            payload.ValueKind != JsonValueKind.Object ||
            !TryReadString(payload, "resultKind", out var resultKind))
        {
            return;
        }

        if (!EffectReactionResultCatalog.TryResolve(resultKind, out var descriptor) ||
            !string.Equals(descriptor.ResolutionMode, "deterministic", StringComparison.Ordinal))
        {
            Add(
                issues,
                componentPath + ".payload.resultKind",
                "wound_consequence_reaction_result_invalid",
                "one registered deterministic reaction result",
                resultKind);
            return;
        }

        if (!genericValid ||
            !TryReadString(component, "componentId", out var componentId) ||
            !TryReadString(payload, "eventType", out var eventType))
        {
            return;
        }

        AddSlot(
            candidates,
            coordinates,
            new SlotCandidate(
                effectRef,
                componentId,
                "event_reaction",
                "reaction",
                eventType + ":" + resultKind,
                "event_reaction:" + eventType + ":" + resultKind,
                originPrefix + componentId,
                componentPath + ".payload.resultKind"),
            componentPath + ".payload.resultKind",
            issues);

        var expansionMatches = evidence.ExpansionsByReactionComponentId.TryGetValue(
            componentId,
            out var matches)
            ? matches
            : ImmutableArray<IndexedExpansion>.Empty;
        foreach (var match in expansionMatches)
            usedExpansions.Add(match.Index);

        if (descriptor.Behavior != EffectReactionResultBehavior.ApplyDefinition)
        {
            if (expansionMatches.Length != 0)
            {
                Add(
                    issues,
                    componentPath + ".payload.resultKind",
                    "wound_consequence_reaction_expansion_invalid",
                    "no definition expansion for this deterministic result kind",
                    resultKind);
            }

            return;
        }

        if (!payload.TryGetProperty("maxExpansion", out var rawMaximum) ||
            rawMaximum.ValueKind != JsonValueKind.Number ||
            !rawMaximum.TryGetInt32(out var declaredMaximum) ||
            !string.Equals(
                rawMaximum.GetRawText(),
                declaredMaximum.ToString(CultureInfo.InvariantCulture),
                StringComparison.Ordinal) ||
            declaredMaximum != 2)
        {
            Add(
                issues,
                componentPath + ".payload.maxExpansion",
                "wound_consequence_reaction_expansion_invalid",
                "exact integer 2 execution budget for one direct result plus one reachable definition application",
                DescribeProperty(payload, "maxExpansion"));
            return;
        }

        if (rank is int expansionRank && expansionRank < 3)
        {
            Add(
                issues,
                componentPath + ".payload.definitionKey",
                "wound_consequence_reaction_expansion_invalid",
                "definition expansion available only at severity III-IV",
                expansionRank.ToString(CultureInfo.InvariantCulture));
            return;
        }

        if (expansionMatches.Length != 1)
        {
            Add(
                issues,
                componentPath + ".payload.definitionKey",
                "wound_consequence_reaction_expansion_invalid",
                "exactly one complete flattened worst-case expansion",
                expansionMatches.Length.ToString(CultureInfo.InvariantCulture));
            return;
        }

        var indexedExpansion = expansionMatches[0];
        var expansion = indexedExpansion.Expansion;
        var expansionPath = indexedExpansion.AuthorPath;
        if (expansion.Components.Length > MaximumReactionExpansionComponents)
        {
            Add(
                issues,
                expansionPath + ".components",
                "wound_consequence_limit_exceeded",
                $"at most {MaximumReactionExpansionComponents} flattened components",
                expansion.Components.Length.ToString(CultureInfo.InvariantCulture));
            return;
        }

        for (var expansionComponentIndex = 0;
             expansionComponentIndex < expansion.Components.Length;
             expansionComponentIndex++)
        {
            validateExpansionComponent(indexedExpansion, expansionComponentIndex);
        }
    }

    private static void ValidateSpiritualEffects(
        WoundConsequenceEnvelopeRequest request,
        int rank,
        IReadOnlyList<IndexedEffect> owned,
        List<SlotCandidate> candidates,
        HashSet<string> coordinates,
        string path,
        List<ValidationIssue> issues)
    {
        foreach (var indexed in owned)
        {
            var effect = indexed.Effect;
            var effectPath = $"{path}.effects[{indexed.Index}]";
            if (!effect.Cadences.IsEmpty || !effect.Expansions.IsEmpty)
            {
                Add(
                    issues,
                    effectPath,
                    "wound_consequence_spiritual_profile_invalid",
                    "spiritual consequence without Mortal cadence or reaction expansion evidence",
                    $"cadences={effect.Cadences.Length}; expansions={effect.Expansions.Length}");
            }

            for (var componentIndex = 0;
                 componentIndex < effect.Components.Length;
                 componentIndex++)
            {
                ValidateSpiritualComponent(
                    rank,
                    effect.EffectId,
                    componentIndex,
                    effect.Components[componentIndex],
                    effectPath,
                    candidates,
                    coordinates,
                    issues);
            }
        }
    }

    private static void ValidateSpiritualComponent(
        int rank,
        string effectId,
        int componentIndex,
        JsonElement component,
        string effectPath,
        List<SlotCandidate> candidates,
        HashSet<string> coordinates,
        List<ValidationIssue> issues)
    {
        var componentPath = $"{effectPath}.components[{componentIndex}]";
        if (ContainsDuplicateRawProperties(component))
            return;

        if (!TryReadString(component, "profile", out var profile) ||
            !SpiritualWoundEffectProfileCatalog.TryGetProfile(
                profile,
                out var profileDescriptor))
        {
            Add(
                issues,
                componentPath + ".profile",
                "wound_consequence_profile_unsupported",
                string.Join(
                    " | ",
                    SpiritualWoundEffectProfileCatalog.RegisteredProfiles.OrderBy(
                        static value => value,
                        StringComparer.Ordinal)),
                DescribeProperty(component, "profile"));
            return;
        }

        var commonIssues = new List<ValidationIssue>();
        EffectComponentProfiles.ValidateComponent(component, componentPath, commonIssues);

        if (!IsClosedObject(
                component,
                "componentId",
                "profile",
                "priority",
                "payload") ||
            !TryReadString(component, "componentId", out var componentId) ||
            !component.TryGetProperty("priority", out var priority) ||
            !priority.TryGetInt32(out _) ||
            !component.TryGetProperty("payload", out var payload) ||
            payload.ValueKind != JsonValueKind.Object ||
            !IsClosedObject(payload, "operation", "axis", "magnitude"))
        {
            Add(
                issues,
                componentPath,
                "wound_consequence_spiritual_profile_invalid",
                "closed spiritual component with operation, axis, and magnitude",
                component.GetRawText());
            return;
        }

        var allowedOperations = profileDescriptor.LegalOperations;
        if (!TryReadString(payload, "operation", out var operation) ||
            !allowedOperations.Contains(operation))
        {
            Add(
                issues,
                componentPath + ".payload.operation",
                "wound_consequence_spiritual_operation_invalid",
                string.Join(
                    " | ",
                    allowedOperations.OrderBy(static value => value, StringComparer.Ordinal)),
                DescribeProperty(payload, "operation"));
            return;
        }

        var expectedAxis = profileDescriptor.Axis;
        if (!TryReadString(payload, "axis", out var axis) ||
            !string.Equals(axis, expectedAxis, StringComparison.Ordinal))
        {
            Add(
                issues,
                componentPath + ".payload.axis",
                "wound_consequence_spiritual_axis_invalid",
                expectedAxis,
                DescribeProperty(payload, "axis"));
            return;
        }

        if (!IsSpiritualProfileAvailable(profile, rank))
        {
            Add(
                issues,
                componentPath + ".profile",
                "wound_consequence_spiritual_profile_unavailable",
                SpiritualAvailability(profile),
                rank.ToString(CultureInfo.InvariantCulture));
            return;
        }

        if (!payload.TryGetProperty("magnitude", out var magnitude) ||
            !profileDescriptor.IsMagnitudeValid(magnitude) ||
            !IsSpiritualMagnitudeValid(profile, rank, magnitude))
        {
            Add(
                issues,
                componentPath + ".payload.magnitude",
                "wound_consequence_spiritual_magnitude_invalid",
                ExpectedSpiritualMagnitude(profile, rank),
                DescribeProperty(payload, "magnitude"));
            return;
        }

        if (commonIssues.Count != 0)
        {
            var commonIssue = commonIssues[0];
            Add(
                issues,
                commonIssue.FilePath,
                "wound_consequence_spiritual_profile_invalid",
                commonIssue.Expected ?? "component accepted by the common #1535 profile registry",
                commonIssue.Actual ?? commonIssue.Code ?? "invalid component");
            return;
        }

        AddSlot(
            candidates,
            coordinates,
            new SlotCandidate(
                effectId,
                componentId,
                profile,
                axis,
                operation,
                profile + ":" + operation,
                "0:" + componentId),
            componentPath + ".payload.operation",
            issues);
    }

    private static bool IsSpiritualProfileAvailable(string profile, int rank) =>
        profile switch
        {
            "spiritual_control_burden" => rank >= 2,
            "spiritual_strain_burden" => rank >= 3,
            "spiritual_art_restriction" => rank >= 3,
            _ => rank >= 1
        };

    private static string SpiritualAvailability(string profile) => profile switch
    {
        "spiritual_control_burden" => "severity II-IV",
        "spiritual_strain_burden" => "severity III-IV",
        "spiritual_art_restriction" => "severity III-IV",
        _ => "severity I-IV"
    };

    private static bool IsSpiritualMagnitudeValid(
        string profile,
        int rank,
        JsonElement magnitude) => profile switch
    {
        "spiritual_roll_hindrance" => IsExactString(magnitude, "disadvantage"),
        "spiritual_action_cost_burden" => IsExactDecimal(
            magnitude,
            rank <= 2 ? 1m : rank == 3 ? 2m : 3m),
        "spiritual_position_burden" =>
            IsExactDecimal(magnitude, 1m) ||
            (rank >= 3 && IsExactDecimal(magnitude, 2m)),
        "spiritual_control_burden" => IsExactDecimal(magnitude, 1m),
        "spiritual_strain_burden" => IsExactDecimal(magnitude, 1m),
        "spiritual_tempo_burden" => IsExactString(magnitude, "deny_one_gain"),
        "spiritual_counter_burden" => IsExactString(magnitude, "reduce_one_step"),
        "spiritual_art_restriction" => IsExactString(
            magnitude,
            rank == 3 ? "restrict" : "forbid"),
        _ => false
    };

    private static string ExpectedSpiritualMagnitude(string profile, int rank) =>
        profile switch
        {
            "spiritual_roll_hindrance" => "disadvantage",
            "spiritual_action_cost_burden" =>
                (rank <= 2 ? 1 : rank == 3 ? 2 : 3).ToString(CultureInfo.InvariantCulture),
            "spiritual_position_burden" => rank >= 3 ? "1 | 2" : "1",
            "spiritual_control_burden" => "1",
            "spiritual_strain_burden" => "1",
            "spiritual_tempo_burden" => "deny_one_gain",
            "spiritual_counter_burden" => "reduce_one_step",
            "spiritual_art_restriction" => rank == 3 ? "restrict" : "forbid",
            _ => "registered magnitude"
        };

    private static ImmutableArray<WoundDerivedConsequenceSlot> DeriveSlots(
        List<SlotCandidate> candidates)
    {
        var ordered = candidates
            .OrderBy(static candidate => candidate.EffectId, StringComparer.Ordinal)
            .ThenBy(static candidate => candidate.OriginKey, StringComparer.Ordinal)
            .ThenBy(static candidate => candidate.ProfileKey, StringComparer.Ordinal)
            .ThenBy(static candidate => candidate.OperationKey, StringComparer.Ordinal)
            .ToArray();
        var builder = ImmutableArray.CreateBuilder<WoundDerivedConsequenceSlot>(
            ordered.Length);
        for (var index = 0; index < ordered.Length; index++)
        {
            var candidate = ordered[index];
            builder.Add(new WoundDerivedConsequenceSlot(
                index + 1,
                candidate.EffectId,
                candidate.ComponentId,
                candidate.ProfileKey,
                candidate.Axis,
                candidate.OperationKey,
                candidate.Coordinate));
        }

        return builder.MoveToImmutable();
    }

    private static ImmutableArray<WoundDetachedMortalEnvelopeSlot> DeriveDetachedSlots(
        List<SlotCandidate> candidates)
    {
        var ordered = candidates
            .OrderBy(static candidate => candidate.EffectId, StringComparer.Ordinal)
            .ThenBy(static candidate => candidate.OriginKey, StringComparer.Ordinal)
            .ThenBy(static candidate => candidate.ProfileKey, StringComparer.Ordinal)
            .ThenBy(static candidate => candidate.OperationKey, StringComparer.Ordinal)
            .ToArray();
        var builder = ImmutableArray.CreateBuilder<WoundDetachedMortalEnvelopeSlot>(
            ordered.Length);
        for (var index = 0; index < ordered.Length; index++)
        {
            var candidate = ordered[index];
            builder.Add(new WoundDetachedMortalEnvelopeSlot(
                index + 1,
                candidate.EffectId,
                candidate.ComponentId,
                candidate.ProfileKey,
                candidate.Axis,
                candidate.OperationKey,
                candidate.Coordinate,
                candidate.AuthorPath));
        }

        return builder.MoveToImmutable();
    }

    private static void ValidateSlotBudget(
        WoundConsequenceEnvelopeRequest request,
        int rank,
        ImmutableArray<WoundDerivedConsequenceSlot> slots,
        string path,
        List<ValidationIssue> issues)
    {
        if (string.Equals(request.Domain, "physical", StringComparison.Ordinal))
        {
            if (slots.Length > rank || slots.Length > MaximumConsequenceSlots)
            {
                Add(
                    issues,
                    path + ".derivedSlots",
                    "wound_consequence_slot_budget_exceeded",
                    $"at most {rank} independently derived mechanical slots",
                    slots.Length.ToString(CultureInfo.InvariantCulture));
            }

            if (slots.IsEmpty && !request.LifecycleEvidence.ChangesLegalLifecycle)
            {
                Add(
                    issues,
                    path + ".lifecycleEvidence",
                    "wound_consequence_non_display_impact_required",
                    "mechanical slot or proven active-complication/care/recovery lifecycle impact",
                    "display-only or empty consequence envelope");
            }
        }
        else if (string.Equals(request.Domain, "spiritual", StringComparison.Ordinal) &&
                 slots.Length != rank)
        {
            Add(
                issues,
                path + ".derivedSlots",
                "wound_consequence_spiritual_slot_count_invalid",
                $"exactly {rank} independent spiritual slots",
                slots.Length.ToString(CultureInfo.InvariantCulture));
        }

        var declared = request.DeclaredConsequences;
        if (declared.SlotBudget != rank)
        {
            Add(
                issues,
                path + ".declared.slotBudget",
                "wound_consequence_declared_slots_mismatch",
                rank.ToString(CultureInfo.InvariantCulture),
                declared.SlotBudget.ToString(CultureInfo.InvariantCulture));
        }

        if (declared.SlotsUsed != slots.Length)
        {
            Add(
                issues,
                path + ".declared.slotsUsed",
                "wound_consequence_declared_slots_mismatch",
                slots.Length.ToString(CultureInfo.InvariantCulture),
                declared.SlotsUsed.ToString(CultureInfo.InvariantCulture));
        }

        if (declared.Entries.Count != slots.Length)
        {
            Add(
                issues,
                path + ".declared.entries",
                "wound_consequence_declared_slots_mismatch",
                $"exactly {slots.Length} entries derived from mechanics",
                declared.Entries.Count.ToString(CultureInfo.InvariantCulture));
            return;
        }

        for (var index = 0; index < declared.Entries.Count; index++)
        {
            var entry = declared.Entries[index];
            var entryPath = $"{path}.declared.entries[{index}]";
            if (entry.Slot != index + 1)
            {
                Add(
                    issues,
                    entryPath + ".slot",
                    "wound_consequence_declared_slots_mismatch",
                    (index + 1).ToString(CultureInfo.InvariantCulture),
                    entry.Slot.ToString(CultureInfo.InvariantCulture));
                continue;
            }

            var derived = slots[index];
            if (!string.Equals(entry.EffectId, derived.EffectId, StringComparison.Ordinal) ||
                !string.Equals(entry.ProfileKey, derived.ProfileKey, StringComparison.Ordinal))
            {
                Add(
                    issues,
                    entryPath,
                    "wound_consequence_declared_slots_mismatch",
                    derived.EffectId + ":" + derived.ProfileKey,
                    entry.EffectId + ":" + entry.ProfileKey);
            }
        }
    }

    private static void AddSlot(
        List<SlotCandidate> candidates,
        HashSet<string> coordinates,
        SlotCandidate candidate,
        string issuePath,
        List<ValidationIssue> issues)
    {
        if (!coordinates.Add(candidate.Coordinate))
        {
            Add(
                issues,
                issuePath,
                "wound_consequence_duplicate_coordinate",
                "one independently understandable slot per exact mechanical coordinate",
                candidate.Coordinate);
            return;
        }

        candidates.Add(candidate);
    }

    private static bool WithinAbsoluteLimit(decimal value, decimal limit) =>
        value >= -limit && value <= limit;

    private static bool IsAtMostPercentage(
        decimal amount,
        decimal maximum,
        decimal percentage)
    {
        if (amount <= 0m || maximum <= 0m || percentage <= 0m)
            return false;

        GetDecimalRational(amount, out var amountCoefficient, out var amountScale);
        GetDecimalRational(maximum, out var maximumCoefficient, out var maximumScale);
        GetDecimalRational(percentage, out var percentageCoefficient, out var percentageScale);
        var left = amountCoefficient * 100 * BigInteger.Pow(10, maximumScale + percentageScale);
        var right = maximumCoefficient * percentageCoefficient * BigInteger.Pow(10, amountScale);
        return left <= right;
    }

    private static void GetDecimalRational(
        decimal value,
        out BigInteger coefficient,
        out int scale)
    {
        var bits = decimal.GetBits(value);
        coefficient =
            (new BigInteger((uint)bits[2]) << 64) |
            (new BigInteger((uint)bits[1]) << 32) |
            new BigInteger((uint)bits[0]);
        scale = (bits[3] >> 16) & 0x7f;
        if ((bits[3] & int.MinValue) != 0)
            coefficient = -coefficient;
    }

    private static bool IsExactDecimal(JsonElement element, decimal expected) =>
        ResourceMaterializationContract.TryReadExactDecimal(element, out var actual) &&
        actual == expected;

    private static bool IsExactString(JsonElement element, string expected) =>
        element.ValueKind == JsonValueKind.String &&
        string.Equals(element.GetString(), expected, StringComparison.Ordinal);

    private static bool TryReadString(
        JsonElement root,
        string field,
        out string value)
    {
        value = string.Empty;
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty(field, out var element) ||
            element.ValueKind != JsonValueKind.String ||
            element.GetString() is not string candidate ||
            !ResourceMaterializationContract.IsExactIdentifier(candidate))
        {
            return false;
        }

        value = candidate;
        return true;
    }

    private static bool IsClosedObject(JsonElement value, params string[] allowedFields)
    {
        if (value.ValueKind != JsonValueKind.Object)
            return false;

        var allowed = new HashSet<string>(allowedFields, StringComparer.Ordinal);
        return value.EnumerateObject().All(property => allowed.Contains(property.Name)) &&
               allowed.All(field => value.TryGetProperty(field, out _));
    }

    private static string DescribeMortalProfiles() => string.Join(
        " | ",
        MortalMechanicalProfileSet
            .Concat(MortalZeroSlotProfileSet)
            .OrderBy(static value => value, StringComparer.Ordinal));

    private static string DescribeProperty(JsonElement root, string field) =>
        root.ValueKind == JsonValueKind.Object && root.TryGetProperty(field, out var value)
            ? value.GetRawText()
            : "missing";

    private static void Add(
        List<ValidationIssue> issues,
        string path,
        string code,
        string expected,
        string actual) =>
        issues.Add(new ValidationIssue(
            path,
            IssueSeverity.Error,
            "Wound consequence violates the closed version-1 severity envelope.",
            code: code,
            actor: "Client",
            section: "wound_materialization",
            expected: expected,
            actual: actual,
            repairHint: "Use exact wound ownership and one registered, fully budgeted consequence primitive."));

    private sealed record IndexedEffect(
        int Index,
        WoundConsequenceEffectProposal Effect);

    private sealed record IndexedCadence(
        int Index,
        WoundPeriodicCadenceEvidence Cadence);

    private sealed record IndexedExpansion(
        int Index,
        WoundReactionExpansionProposal Expansion,
        string AuthorPath);

    private sealed record MortalEvidenceIndex(
        IReadOnlyDictionary<string, ImmutableArray<IndexedCadence>> CadencesByComponentId,
        IReadOnlyDictionary<string, ImmutableArray<IndexedExpansion>>
            ExpansionsByReactionComponentId);

    private sealed record SlotCandidate(
        string EffectId,
        string ComponentId,
        string ProfileKey,
        string Axis,
        string OperationKey,
        string Coordinate,
        string OriginKey,
        string AuthorPath = "");
}
