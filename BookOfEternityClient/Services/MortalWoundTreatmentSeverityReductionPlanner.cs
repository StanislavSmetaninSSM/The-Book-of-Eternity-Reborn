using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;

namespace BookOfEternityClient.Services;

internal sealed class MortalWoundTreatmentSeverityReductionProjection
{
    private readonly WoundMaterializationEnvelope _before;
    private readonly WoundMaterializationEnvelope _provisionalAfter;
    private readonly ImmutableArray<MortalWoundTreatmentRematerializationRoot> _roots;

    internal MortalWoundTreatmentSeverityReductionProjection(
        WoundMaterializationEnvelope before,
        WoundMaterializationEnvelope provisionalAfter,
        int steps,
        IReadOnlyList<MortalWoundTreatmentRematerializationRoot> roots,
        string fingerprint)
    {
        _before = WoundAcceptedTurnData.CloneWound(before)!;
        _provisionalAfter = WoundAcceptedTurnData.CloneWound(provisionalAfter)!;
        Steps = steps;
        _roots = roots.Select(CloneRoot).ToImmutableArray();
        Fingerprint = fingerprint;
    }

    internal WoundMaterializationEnvelope Before =>
        WoundAcceptedTurnData.CloneWound(_before)!;

    internal WoundMaterializationEnvelope ProvisionalAfter =>
        WoundAcceptedTurnData.CloneWound(_provisionalAfter)!;

    internal int Steps { get; }

    internal IReadOnlyList<MortalWoundTreatmentRematerializationRoot> Roots =>
        _roots.Select(CloneRoot).ToArray();

    internal string Fingerprint { get; }

    private static MortalWoundTreatmentRematerializationRoot CloneRoot(
        MortalWoundTreatmentRematerializationRoot root) => new(
        root.PriorEffectId,
        root.DefinitionKey,
        root.OwnershipDomain with { },
        root.Slots.Select(static slot => slot with { }).ToArray());
}

internal sealed record MortalWoundTreatmentRematerializationRoot
{
    private readonly ImmutableArray<WoundEffectSlotAgreement> _slots;

    internal MortalWoundTreatmentRematerializationRoot(
        string priorEffectId,
        string definitionKey,
        WoundRootOwnershipDomain ownershipDomain,
        IReadOnlyList<WoundEffectSlotAgreement> slots)
    {
        PriorEffectId = priorEffectId;
        DefinitionKey = definitionKey;
        OwnershipDomain = ownershipDomain with { };
        _slots = slots.Select(static slot => slot with { }).ToImmutableArray();
    }

    internal string PriorEffectId { get; }
    internal string DefinitionKey { get; }
    internal WoundRootOwnershipDomain OwnershipDomain { get; }
    internal IReadOnlyList<WoundEffectSlotAgreement> Slots =>
        _slots.Select(static slot => slot with { }).ToArray();
}

internal sealed record MortalWoundTreatmentSeverityReductionProjectionResult(
    MortalWoundTreatmentSeverityReductionProjection? Projection,
    IReadOnlyList<ValidationIssue> Issues)
{
    internal bool IsValid => Projection is not null && Issues.Count == 0;
}

internal sealed record MortalWoundTreatmentReductionScalars(WoundSeverity Severity, int SlotBudget);

internal static class MortalWoundTreatmentSeverityReductionPlanner
{
    private const string FingerprintDomain =
        "book_of_eternity.mortal_wound_treatment.severity_reduction_projection";
    private const string FingerprintVersion = "1";
    private const string ProjectionPath = "mortalWoundTreatment.severityReductionProjection";

    internal static MortalWoundTreatmentSeverityReductionProjectionResult Project(
        WoundMaterializationEnvelope before,
        int steps,
        string resultingLastChangeEventRef)
    {
        var issues = new List<ValidationIssue>();
        if (before is null)
        {
            AddIssue(issues, ProjectionPath + ".before",
                "one complete canonical Mortal wound before-image", "null");
            return Invalid(issues);
        }
        if (!ValidateReductionArguments(steps, resultingLastChangeEventRef, issues))
            return Invalid(issues);

        try
        {
            var canonicalBefore = ParseCanonical(before, ProjectionPath + ".before", issues);
            if (canonicalBefore is null)
                return Invalid(issues);

            if (!TryGetReductionRank(canonicalBefore.Severity.Rank, steps, out var resultingRank, issues))
                return Invalid(issues);

            var roots = ReconstructRoots(canonicalBefore, issues);
            if (issues.Count != 0)
                return Invalid(issues);

            var adapter = ValidateDestinationGraph(canonicalBefore, resultingRank, roots, issues);
            if (!adapter.IsValid || issues.Count != 0)
                return Invalid(issues);

            var scalars = ApplyReductionScalars(canonicalBefore.Severity, resultingRank, resultingLastChangeEventRef);
            var candidate = canonicalBefore with
            {
                Severity = scalars.Severity,
                Consequences = canonicalBefore.Consequences with { SlotBudget = scalars.SlotBudget }
            };
            var canonicalAfter = ParseCanonical(
                candidate, ProjectionPath + ".provisionalAfter", issues);
            if (canonicalAfter is null || issues.Count != 0)
                return Invalid(issues);

            var fingerprint = ComputeFingerprint(
                canonicalBefore, canonicalAfter, steps, resultingLastChangeEventRef, roots);
            return new MortalWoundTreatmentSeverityReductionProjectionResult(
                new MortalWoundTreatmentSeverityReductionProjection(
                    canonicalBefore, canonicalAfter, steps, roots, fingerprint),
                ImmutableArray<ValidationIssue>.Empty);
        }
        catch (Exception exception) when (exception is ArgumentException or
                                           InvalidOperationException or
                                           JsonException or
                                           OverflowException)
        {
            AddIssue(issues, ProjectionPath,
                "one canonical severity-reduction projection", exception.GetType().Name);
            return Invalid(issues);
        }
    }

    internal static bool ValidateReductionArguments(int steps, string resultingLastChangeEventRef, List<ValidationIssue> issues)
    {
        if (steps is < 1 or > 2)
        {
            AddIssue(issues, ProjectionPath + ".steps",
                "severity reduction steps 1 or 2",
                steps.ToString(CultureInfo.InvariantCulture));
            return false;
        }
        if (!ResourceMaterializationContract.IsExactIdentifier(resultingLastChangeEventRef))
        {
            AddIssue(issues, ProjectionPath + ".resultingLastChangeEventRef",
                "one exact accepted event reference", resultingLastChangeEventRef ?? "null");
            return false;
        }
        return true;
    }

    internal static bool TryGetReductionRank(int rank, int steps, out int result, List<ValidationIssue> issues)
    {
        result = checked(rank - steps);
        if (result < 1)
        {
            AddIssue(issues, ProjectionPath + ".steps",
                "a reduction that leaves severity rank at least I",
                result.ToString(CultureInfo.InvariantCulture));
            return false;
        }
        return true;
    }

    internal static MortalWoundTreatmentReductionScalars ApplyReductionScalars(
        WoundSeverity before, int resultingRank, string resultingLastChangeEventRef) =>
        new(before with
        {
            Value = SeverityValue(resultingRank),
            Rank = resultingRank,
            LastChangeEventRef = resultingLastChangeEventRef
        }, resultingRank);

    private static WoundMaterializationEnvelope? ParseCanonical(
        WoundMaterializationEnvelope wound,
        string path,
        List<ValidationIssue> issues)
    {
        var parsed = WoundMaterializationContract.Parse(
            WoundMaterializationContract.SerializeCanonical(wound), path);
        issues.AddRange(parsed.Issues);
        return parsed.IsValid ? parsed.Wound : null;
    }

    private static ImmutableArray<MortalWoundTreatmentRematerializationRoot>
        ReconstructRoots(WoundMaterializationEnvelope wound, List<ValidationIssue> issues)
    {
        var definitions = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        var confusableDefinitions = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0;
             index < wound.Consequences.OwnedEffectSources.Definitions.Count;
             index++)
        {
            var definition = wound.Consequences.OwnedEffectSources.Definitions[index];
            var key = ReadDefinitionKey(definition);
            if (!ResourceMaterializationContract.IsExactIdentifier(key) ||
                !definitions.TryAdd(key, definition.Clone()) ||
                !confusableDefinitions.Add(Confusable(key)))
            {
                AddIssue(issues,
                    ProjectionPath +
                    $".before.consequences.ownedEffectSources.definitions[{index}]",
                    "one exact/confusable-unique definitionKey",
                    key.Length == 0 ? "missing or invalid" : key);
            }
        }

        var orderedBindings = wound.Consequences.OwnedEffectSources.RootBindings
            .OrderBy(static binding => binding.EffectId, StringComparer.Ordinal)
            .ThenBy(static binding => binding.DefinitionKey, StringComparer.Ordinal)
            .ToArray();
        var bindingsByEffect = new Dictionary<string, WoundRootEffectBinding>(StringComparer.Ordinal);
        var rootDefinitions = new HashSet<string>(StringComparer.Ordinal);
        var confusableEffects = new HashSet<string>(StringComparer.Ordinal);
        var confusableRootDefinitions = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < orderedBindings.Length; index++)
        {
            var binding = orderedBindings[index];
            if (!bindingsByEffect.TryAdd(binding.EffectId, binding) ||
                !confusableEffects.Add(Confusable(binding.EffectId)) ||
                !rootDefinitions.Add(binding.DefinitionKey) ||
                !confusableRootDefinitions.Add(Confusable(binding.DefinitionKey)) ||
                !definitions.ContainsKey(binding.DefinitionKey))
            {
                AddIssue(issues,
                    ProjectionPath +
                    $".before.consequences.ownedEffectSources.rootBindings[{index}]",
                    "one exact/confusable-unique root effect/definition binding resolving in the graph",
                    binding.EffectId + ":" + binding.DefinitionKey);
            }
        }

        var ownership = orderedBindings.ToDictionary(
            static binding => binding.EffectId,
            static _ => WoundRootOwnershipDomain.BaseWound,
            StringComparer.Ordinal);
        var ownedExact = new HashSet<string>(StringComparer.Ordinal);
        var ownedConfusable = new HashSet<string>(StringComparer.Ordinal);
        for (var complicationIndex = 0;
             complicationIndex < wound.Complications.Count;
             complicationIndex++)
        {
            var complication = wound.Complications[complicationIndex];
            for (var ownedIndex = 0;
                 ownedIndex < complication.OwnedEffectIds.Count;
                 ownedIndex++)
            {
                var effectId = complication.OwnedEffectIds[ownedIndex];
                if (!ownedExact.Add(effectId) ||
                    !ownedConfusable.Add(Confusable(effectId)) ||
                    !bindingsByEffect.ContainsKey(effectId))
                {
                    AddIssue(issues,
                        ProjectionPath +
                        $".before.complications[{complicationIndex}].ownedEffectIds[{ownedIndex}]",
                        "one pairwise-disjoint exact root binding owned by one complication",
                        effectId);
                    continue;
                }
                ownership[effectId] =
                    WoundRootOwnershipDomain.ForComplication(complication.ComplicationId);
            }
        }

        var entriesByEffect = wound.Consequences.Entries
            .GroupBy(static entry => entry.EffectId, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key,
                static group => group.OrderBy(static entry => entry.Slot).ToArray(),
                StringComparer.Ordinal);
        foreach (var effectId in entriesByEffect.Keys)
        {
            if (bindingsByEffect.ContainsKey(effectId))
                continue;
            AddIssue(issues, ProjectionPath + ".before.consequences.entries",
                "every consequence entry bound to one exact direct root", effectId);
        }
        if (issues.Count != 0)
            return ImmutableArray<MortalWoundTreatmentRematerializationRoot>.Empty;

        return orderedBindings.Select(binding =>
        {
            entriesByEffect.TryGetValue(binding.EffectId, out var entries);
            entries ??= Array.Empty<WoundConsequenceEntry>();
            return new MortalWoundTreatmentRematerializationRoot(
                binding.EffectId,
                binding.DefinitionKey,
                ownership[binding.EffectId],
                entries.Select(static entry => new WoundEffectSlotAgreement(
                    entry.Slot, entry.ProfileKey, entry.ReadableSummary)).ToArray());
        }).ToImmutableArray();
    }

    private static WoundPersistedConsequenceEnvelopeValidationResult ValidateDestinationGraph(
        WoundMaterializationEnvelope wound,
        int resultingRank,
        IReadOnlyList<MortalWoundTreatmentRematerializationRoot> roots,
        List<ValidationIssue> issues)
    {
        var definitions = wound.Consequences.OwnedEffectSources.Definitions
            .Select((definition, index) => new WoundPersistedConsequenceDefinition(
                ReadDefinitionKey(definition),
                ProjectionPath +
                $".before.consequences.ownedEffectSources.definitions[{index}]",
                definition.Clone()))
            .ToArray();
        var persistedRoots = roots.Select((root, index) =>
            new WoundPersistedConsequenceRoot(
                root.PriorEffectId,
                root.DefinitionKey,
                ProjectionPath +
                $".before.consequences.ownedEffectSources.rootBindings[{index}]",
                root.Slots)).ToArray();
        return ValidateDestinationGraph(resultingRank, definitions, persistedRoots, wound.Consequences.SlotsUsed, issues);
    }

    internal static WoundPersistedConsequenceEnvelopeValidationResult ValidateDestinationGraph(
        int resultingRank, IReadOnlyList<WoundPersistedConsequenceDefinition> definitions,
        IReadOnlyList<WoundPersistedConsequenceRoot> roots, int slotsUsed, List<ValidationIssue> issues)
    {
        var validation = WoundPersistedConsequenceEnvelopeAdapter.ValidateDetached(
            resultingRank,
            ProjectionPath + ".before.consequences",
            definitions,
            roots,
            requireExactGlobalSlotAgreement: true,
            persistedSlotsUsed: slotsUsed);
        issues.AddRange(validation.Issues);
        return validation;
    }

    private static string ComputeFingerprint(
        WoundMaterializationEnvelope before,
        WoundMaterializationEnvelope after,
        int steps,
        string resultingLastChangeEventRef,
        IReadOnlyList<MortalWoundTreatmentRematerializationRoot> roots)
    {
        var fields = new List<string?>
        {
            FingerprintDomain,
            FingerprintVersion,
            WoundMaterializationContract.SerializeCanonical(before),
            WoundMaterializationContract.SerializeCanonical(after),
            steps.ToString(CultureInfo.InvariantCulture),
            resultingLastChangeEventRef,
            roots.Count.ToString(CultureInfo.InvariantCulture)
        };
        for (var rootIndex = 0; rootIndex < roots.Count; rootIndex++)
        {
            var root = roots[rootIndex];
            fields.Add(rootIndex.ToString(CultureInfo.InvariantCulture));
            fields.Add(root.PriorEffectId);
            fields.Add(root.DefinitionKey);
            fields.Add(root.OwnershipDomain.Kind);
            fields.Add(root.OwnershipDomain.ComplicationId);
            fields.Add(root.Slots.Count.ToString(CultureInfo.InvariantCulture));
            for (var slotIndex = 0; slotIndex < root.Slots.Count; slotIndex++)
            {
                var slot = root.Slots[slotIndex];
                fields.Add(slotIndex.ToString(CultureInfo.InvariantCulture));
                fields.Add(slot.Slot.ToString(CultureInfo.InvariantCulture));
                fields.Add(slot.ProfileKey);
                fields.Add(slot.ReadableSummary);
            }
        }
        return WoundAcceptedTurnFingerprintWriter.Compute(fields);
    }

    private static string ReadDefinitionKey(JsonElement definition) =>
        definition.ValueKind == JsonValueKind.Object &&
        definition.TryGetProperty("definitionKey", out var key) &&
        key.ValueKind == JsonValueKind.String
            ? key.GetString() ?? string.Empty
            : string.Empty;

    private static string Confusable(string value) =>
        ResourceMaterializationContract.BuildConfusableKey(value);

    private static string SeverityValue(int rank) => rank switch
    {
        1 => "I",
        2 => "II",
        3 => "III",
        4 => "IV",
        _ => throw new InvalidOperationException("Unsupported severity rank.")
    };

    private static MortalWoundTreatmentSeverityReductionProjectionResult Invalid(
        IEnumerable<ValidationIssue> issues) => new(null, issues.ToImmutableArray());

    private static void AddIssue(
        ICollection<ValidationIssue> issues,
        string path,
        string expected,
        string actual) => issues.Add(new ValidationIssue(
        path,
        IssueSeverity.Error,
        "The Mortal wound severity-reduction projection is invalid.",
        code: "mortal_wound_treatment_severity_projection_invalid",
        actor: "Client",
        section: "wound_materialization",
        expected: expected,
        actual: actual,
        repairHint:
        "Choose a legal reduction whose complete unchanged consequence graph fits the destination rank."));
}
