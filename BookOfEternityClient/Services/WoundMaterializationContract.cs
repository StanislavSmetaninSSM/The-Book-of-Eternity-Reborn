using System.Buffers;
using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace BookOfEternityClient.Services;

internal sealed record WoundMaterializationParseResult(
    WoundMaterializationEnvelope? Wound,
    IReadOnlyList<ValidationIssue> Issues)
{
    internal bool IsValid => Wound is not null && Issues.Count == 0;
}

internal sealed record WoundMaterializationEnvelope(
    int SchemaVersion,
    string WoundId,
    string Lifecycle,
    WoundOwnerCoordinate Owner,
    WoundOrigin Origin,
    WoundClassification Classification,
    WoundDisplay Display,
    WoundSeverity Severity,
    WoundCare Care,
    IReadOnlyList<WoundComplication> Complications,
    WoundConsequences Consequences,
    WoundTreatment Treatment,
    WoundRecovery Recovery,
    WoundRelations Relations,
    WoundLastTransition LastTransition);

internal sealed record WoundOwnerCoordinate(
    string Realm,
    string OwnerKind,
    string OwnerId,
    string CarrierPath);

internal sealed record WoundOrigin(
    string EventRef,
    string SourceKind,
    string SourceId,
    string SourceState,
    int CreatedAtTurn,
    string? CreatedAtCycleId,
    string OpportunityId,
    string? GuaranteedTriggerId,
    string ReadableCause);

internal sealed record WoundClassification(
    string Domain,
    string WoundType,
    WoundLocationProfile LocationProfile);

internal sealed record WoundLocationProfile(
    string Kind,
    string ReadableLocus,
    string? AuthorityKind,
    string? AuthorityRef,
    string? AffectedSide);

internal sealed record WoundDisplay(
    string Name,
    string Description,
    IReadOnlyList<string> VisibleSymptoms,
    string Prognosis,
    string Visibility,
    string AcquisitionNarration);

internal sealed record WoundSeverity(
    string Value,
    int Rank,
    string MaximumAtCreation,
    string LastChangeEventRef);

internal sealed record WoundCare(
    string State,
    int? StabilizedAtTurn,
    string? ActiveCourseId,
    string? LastAttemptId);

internal sealed record WoundComplication(
    string ComplicationId,
    string Kind,
    string State,
    string DisplayName,
    int TreatmentDifficultyModifier,
    IReadOnlyList<string> OwnedEffectIds,
    string Visibility);

internal sealed record WoundConsequences(
    int SlotBudget,
    int SlotsUsed,
    IReadOnlyList<WoundConsequenceEntry> Entries)
{
    internal WoundOwnedEffectSources OwnedEffectSources { get; init; } =
        WoundOwnedEffectSources.Empty;
}

internal sealed record WoundOwnedEffectSources(
    IReadOnlyList<JsonElement> Definitions,
    IReadOnlyList<WoundRootEffectBinding> RootBindings)
{
    internal static WoundOwnedEffectSources Empty { get; } = new(
        ImmutableArray<JsonElement>.Empty,
        ImmutableArray<WoundRootEffectBinding>.Empty);
}

internal sealed record WoundRootEffectBinding(
    string EffectId,
    string DefinitionKey);

internal sealed record WoundConsequenceEntry(
    int Slot,
    string ProfileKey,
    string EffectId,
    string ReadableSummary);

internal sealed record WoundTreatment(
    IReadOnlyList<WoundDiagnosisPath> DiagnosisPaths,
    IReadOnlyList<WoundTreatmentRoute> Routes,
    IReadOnlyList<string> KnownRouteIds,
    IReadOnlyList<string> CompletedRouteIds);

internal sealed record WoundDiagnosisPath(
    string DiagnosisPathId,
    string Visibility,
    IReadOnlyList<JsonElement> Requirements,
    JsonElement Check,
    IReadOnlyList<string> Reveals,
    string FailurePolicy);

internal sealed record WoundTreatmentRoute(
    string RouteId,
    string DisplayName,
    string Visibility,
    string Mode,
    IReadOnlyList<JsonElement> Requirements,
    JsonElement ResourcePolicy,
    JsonElement Resolution,
    IReadOnlyList<JsonElement> Outcomes,
    JsonElement? Interruption);

internal sealed record WoundRecovery(
    string Mode,
    string ClockKind,
    long Cadence,
    long CurrentStepProgress,
    long CurrentStepThreshold,
    string? LastTickKey,
    IReadOnlyList<string> Blockers,
    bool CarryOverflow,
    JsonElement? DeteriorationPolicy);

internal sealed record WoundRelations(
    string? PriorWoundId,
    IReadOnlyList<string> LegacyRefs,
    IReadOnlyList<string> IndependentEffectRefs);

internal sealed record WoundLastTransition(
    string TransitionId,
    int Ordinal,
    int Turn,
    string Kind);

internal static class WoundMaterializationContract
{
    internal const int SchemaVersion = 1;
    internal const int MaxTreatmentRoutes = 32;
    internal const int MaxDiagnosisPaths = 32;
    internal const int MaxRequirementsPerTreatmentMember = 16;
    internal const int MaxComplications = 16;
    internal const int MaxConsequences = 4;
    internal const int MaxOwnedEffectDefinitions = 5;
    internal const int MaxOwnedEffectRootBindings = 5;
    internal const int MaxReadableTextLength = 2_000;

    private static readonly IReadOnlySet<string> RootFields = Set(
        "schemaVersion", "woundId", "lifecycle", "owner", "origin", "classification",
        "display", "severity", "care", "complications", "consequences", "treatment",
        "recovery", "relations", "lastTransition");
    private static readonly IReadOnlySet<string> OwnerFields = Set(
        "realm", "ownerKind", "ownerId", "carrierPath");
    private static readonly IReadOnlySet<string> OriginFields = Set(
        "eventRef", "sourceKind", "sourceId", "sourceState", "createdAtTurn",
        "createdAtCycleId", "opportunityId", "guaranteedTriggerId", "readableCause");
    private static readonly IReadOnlySet<string> ClassificationFields = Set(
        "domain", "woundType", "locationProfile");
    private static readonly IReadOnlySet<string> LocationProfileFields = Set(
        "kind", "readableLocus", "authorityKind", "authorityRef", "affectedSide");
    private static readonly IReadOnlySet<string> LocationProfileRequiredFields = Set(
        "kind", "readableLocus");
    private static readonly IReadOnlySet<string> DisplayFields = Set(
        "name", "description", "visibleSymptoms", "prognosis", "visibility",
        "acquisitionNarration");
    private static readonly IReadOnlySet<string> SeverityFields = Set(
        "value", "rank", "maximumAtCreation", "lastChangeEventRef");
    private static readonly IReadOnlySet<string> CareFields = Set(
        "state", "stabilizedAtTurn", "activeCourseId", "lastAttemptId");
    private static readonly IReadOnlySet<string> ComplicationFields = Set(
        "complicationId", "kind", "state", "displayName", "treatmentDifficultyModifier",
        "ownedEffectIds", "visibility");
    private static readonly IReadOnlySet<string> ConsequencesFields = Set(
        "slotBudget", "slotsUsed", "ownedEffectSources", "entries");
    private static readonly IReadOnlySet<string> OwnedEffectSourcesFields = Set(
        "definitions", "rootBindings");
    private static readonly IReadOnlySet<string> RootEffectBindingFields = Set(
        "effectId", "definitionKey");
    private static readonly IReadOnlySet<string> ConsequenceEntryFields = Set(
        "slot", "profileKey", "effectId", "readableSummary");
    private static readonly IReadOnlySet<string> TreatmentFields = Set(
        "diagnosisPaths", "routes", "knownRouteIds", "completedRouteIds");
    private static readonly IReadOnlySet<string> DiagnosisPathFields = Set(
        "diagnosisPathId", "visibility", "requirements", "check", "reveals",
        "failurePolicy");
    private static readonly IReadOnlySet<string> TreatmentRouteFields = Set(
        "routeId", "displayName", "visibility", "mode", "requirements",
        "resourcePolicy", "resolution", "outcomes", "interruption");
    private static readonly IReadOnlySet<string> RecoveryFields = Set(
        "mode", "clockKind", "cadence", "currentStepProgress", "currentStepThreshold",
        "lastTickKey", "blockers", "carryOverflow", "deteriorationPolicy");
    private static readonly IReadOnlySet<string> RelationsFields = Set(
        "priorWoundId", "legacyRefs", "independentEffectRefs");
    private static readonly IReadOnlySet<string> LastTransitionFields = Set(
        "transitionId", "ordinal", "turn", "kind");

    private static readonly IReadOnlySet<string> Realms = Set(
        "mortal_world", "chaos_sea", "shining_abode");
    private static readonly IReadOnlySet<string> OwnerKinds = Set(
        "player", "npc", "combatant", "combatant_member", "player_soul", "guardian",
        "resident", "radiant_actor", "afterlife_actor");
    private static readonly IReadOnlySet<string> Domains = Set("physical", "spiritual");
    private static readonly IReadOnlySet<string> Lifecycles = Set("active", "healed");
    private static readonly IReadOnlySet<string> CareStates = Set(
        "fresh", "untreated", "stabilized", "recovering", "healed");
    private static readonly IReadOnlySet<string> Visibilities = Set(
        "public", "known_to_player", "hidden", "gm_only");
    private static readonly IReadOnlySet<string> LocationKinds = Set(
        "anatomical", "systemic", "mental", "spiritual_axis", "other");
    private static readonly IReadOnlySet<string> ComplicationKinds = Set(
        "bleeding", "infection", "pain", "impairment", "systemic_instability",
        "spiritual_instability", "other");
    private static readonly IReadOnlySet<string> RouteModes = Set(
        "procedure", "course", "guaranteed");
    private static readonly IReadOnlySet<string> RecoveryModes = Set(
        "progressive", "requires_stabilization", "no_natural_recovery");
    private static readonly IReadOnlySet<string> TransitionKinds = Set(
        "create", "worsen", "complicate", "diagnose", "stabilize", "treat",
        "recover", "heal", "legacy", "archive");
    private static readonly IReadOnlyDictionary<string, int> SeverityRanks =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["I"] = 1,
            ["II"] = 2,
            ["III"] = 3,
            ["IV"] = 4
        };

    internal static WoundMaterializationParseResult Parse(string? json, string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (string.IsNullOrWhiteSpace(json))
        {
            return InvalidResult(
                path,
                "wound_materialization_invalid_root",
                "non-empty strict JSON object",
                json is null ? "missing" : "empty or whitespace-only input");
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException exception)
        {
            return InvalidResult(
                path,
                "wound_materialization_invalid_json",
                "well-formed strict JSON object",
                exception.GetType().Name);
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return InvalidResult(
                    path,
                    "wound_materialization_invalid_root",
                    "strict JSON object",
                    root.ValueKind.ToString());
            }

            var issues = new List<ValidationIssue>();
            FindDuplicateProperties(root, path, issues);
            ValidateClosedObject(root, path, RootFields, issues);
            RequireFields(root, path, RootFields, issues);

            var schemaVersion = ReadExactSchemaVersion(root, path, issues);
            var woundId = ReadExactIdentifier(root, "woundId", path, issues);
            var lifecycle = ReadClosedString(root, "lifecycle", path, Lifecycles, issues);
            var owner = ParseOwner(ReadRequiredObject(root, "owner", path, issues), path + ".owner", issues);
            var origin = ParseOrigin(ReadRequiredObject(root, "origin", path, issues), path + ".origin", issues);
            var classification = ParseClassification(
                ReadRequiredObject(root, "classification", path, issues),
                path + ".classification",
                issues);
            var display = ParseDisplay(ReadRequiredObject(root, "display", path, issues), path + ".display", issues);
            var severity = ParseSeverity(ReadRequiredObject(root, "severity", path, issues), path + ".severity", issues);
            var care = ParseCare(ReadRequiredObject(root, "care", path, issues), path + ".care", issues);
            var complications = ParseComplications(root, path, issues);
            var treatment = ParseTreatment(
                ReadRequiredObject(root, "treatment", path, issues),
                path + ".treatment",
                issues);
            var recovery = ParseRecovery(
                ReadRequiredObject(root, "recovery", path, issues),
                path + ".recovery",
                issues);
            var consequences = ParseConsequences(
                ReadRequiredObject(root, "consequences", path, issues),
                path + ".consequences",
                woundId,
                lifecycle,
                owner,
                classification,
                severity,
                care,
                complications,
                recovery,
                issues);
            var relations = ParseRelations(
                ReadRequiredObject(root, "relations", path, issues),
                path + ".relations",
                issues);
            var lastTransition = ParseLastTransition(
                ReadRequiredObject(root, "lastTransition", path, issues),
                path + ".lastTransition",
                issues);

            if (issues.Count != 0)
                return new WoundMaterializationParseResult(null, issues.ToImmutableArray());

            return new WoundMaterializationParseResult(
                new WoundMaterializationEnvelope(
                    schemaVersion,
                    woundId,
                    lifecycle,
                    owner,
                    origin,
                    classification,
                    display,
                    severity,
                    care,
                    complications,
                    consequences,
                    treatment,
                    recovery,
                    relations,
                    lastTransition),
                ImmutableArray<ValidationIssue>.Empty);
        }
    }

    internal static string SerializeCanonical(WoundMaterializationEnvelope wound)
    {
        ArgumentNullException.ThrowIfNull(wound);
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(
                   buffer,
                   new JsonWriterOptions
                   {
                       Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                       Indented = false
                   }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", wound.SchemaVersion);
            writer.WriteString("woundId", wound.WoundId);
            writer.WriteString("lifecycle", wound.Lifecycle);
            WriteOwner(writer, wound.Owner);
            WriteOrigin(writer, wound.Origin);
            WriteClassification(writer, wound.Classification);
            WriteDisplay(writer, wound.Display);
            WriteSeverity(writer, wound.Severity);
            WriteCare(writer, wound.Care);
            WriteComplications(writer, wound.Complications);
            WriteConsequences(writer, wound.Consequences);
            WriteTreatment(writer, wound.Treatment);
            WriteRecovery(writer, wound.Recovery);
            WriteRelations(writer, wound.Relations);
            WriteLastTransition(writer, wound.LastTransition);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    private static WoundOwnerCoordinate ParseOwner(
        JsonElement value,
        string path,
        List<ValidationIssue> issues)
    {
        ValidateObjectShape(value, path, OwnerFields, OwnerFields, issues);
        return new WoundOwnerCoordinate(
            ReadClosedString(value, "realm", path, Realms, issues),
            ReadClosedString(value, "ownerKind", path, OwnerKinds, issues),
            ReadExactIdentifier(value, "ownerId", path, issues),
            ReadExactIdentifier(value, "carrierPath", path, issues));
    }

    private static WoundOrigin ParseOrigin(
        JsonElement value,
        string path,
        List<ValidationIssue> issues)
    {
        ValidateObjectShape(value, path, OriginFields, OriginFields, issues);
        return new WoundOrigin(
            ReadExactIdentifier(value, "eventRef", path, issues),
            ReadExactIdentifier(value, "sourceKind", path, issues),
            ReadExactIdentifier(value, "sourceId", path, issues),
            ReadExactIdentifier(value, "sourceState", path, issues),
            ReadInt32(value, "createdAtTurn", path, 0, int.MaxValue, issues),
            ReadNullableExactIdentifier(value, "createdAtCycleId", path, issues),
            ReadExactIdentifier(value, "opportunityId", path, issues),
            ReadNullableExactIdentifier(value, "guaranteedTriggerId", path, issues),
            ReadText(value, "readableCause", path, issues));
    }

    private static WoundClassification ParseClassification(
        JsonElement value,
        string path,
        List<ValidationIssue> issues)
    {
        ValidateObjectShape(value, path, ClassificationFields, ClassificationFields, issues);
        var locationPath = path + ".locationProfile";
        var location = ReadRequiredObject(value, "locationProfile", path, issues);
        ValidateObjectShape(
            location,
            locationPath,
            LocationProfileFields,
            LocationProfileRequiredFields,
            issues);
        var locationProfile = new WoundLocationProfile(
            ReadClosedString(location, "kind", locationPath, LocationKinds, issues),
            ReadText(location, "readableLocus", locationPath, issues),
            ReadOptionalExactIdentifier(location, "authorityKind", locationPath, issues),
            ReadOptionalExactIdentifier(location, "authorityRef", locationPath, issues),
            ReadOptionalExactIdentifier(location, "affectedSide", locationPath, issues));
        return new WoundClassification(
            ReadClosedString(value, "domain", path, Domains, issues),
            ReadText(value, "woundType", path, issues),
            locationProfile);
    }

    private static WoundDisplay ParseDisplay(
        JsonElement value,
        string path,
        List<ValidationIssue> issues)
    {
        ValidateObjectShape(value, path, DisplayFields, DisplayFields, issues);
        return new WoundDisplay(
            ReadText(value, "name", path, issues),
            ReadText(value, "description", path, issues),
            ReadTextArray(value, "visibleSymptoms", path, issues),
            ReadText(value, "prognosis", path, issues),
            ReadClosedString(value, "visibility", path, Visibilities, issues),
            ReadText(value, "acquisitionNarration", path, issues));
    }

    private static WoundSeverity ParseSeverity(
        JsonElement value,
        string path,
        List<ValidationIssue> issues)
    {
        ValidateObjectShape(value, path, SeverityFields, SeverityFields, issues);
        var severityValue = ReadClosedString(value, "value", path, SeverityRanks.Keys, issues);
        var rank = ReadInt32(value, "rank", path, 1, 4, issues);
        var maximum = ReadClosedString(
            value,
            "maximumAtCreation",
            path,
            SeverityRanks.Keys,
            issues);
        if (SeverityRanks.TryGetValue(severityValue, out var expectedRank) && rank != expectedRank)
        {
            AddIssue(
                issues,
                path + ".rank",
                "wound_materialization_invalid_field",
                expectedRank.ToString(CultureInfo.InvariantCulture),
                rank.ToString(CultureInfo.InvariantCulture));
        }

        return new WoundSeverity(
            severityValue,
            rank,
            maximum,
            ReadExactIdentifier(value, "lastChangeEventRef", path, issues));
    }

    private static WoundCare ParseCare(
        JsonElement value,
        string path,
        List<ValidationIssue> issues)
    {
        ValidateObjectShape(value, path, CareFields, CareFields, issues);
        return new WoundCare(
            ReadClosedString(value, "state", path, CareStates, issues),
            ReadNullableInt32(value, "stabilizedAtTurn", path, 0, int.MaxValue, issues),
            ReadNullableExactIdentifier(value, "activeCourseId", path, issues),
            ReadNullableExactIdentifier(value, "lastAttemptId", path, issues));
    }

    private static IReadOnlyList<WoundComplication> ParseComplications(
        JsonElement root,
        string path,
        List<ValidationIssue> issues)
    {
        var arrayPath = path + ".complications";
        if (!TryReadArray(root, "complications", path, out var array, issues))
            return ImmutableArray<WoundComplication>.Empty;
        AddLimitIssue(array, arrayPath, MaxComplications, "complications", issues);

        var parsed = ImmutableArray.CreateBuilder<WoundComplication>();
        var identifiers = new HashSet<string>(StringComparer.Ordinal);
        var index = 0;
        foreach (var item in array.EnumerateArray())
        {
            if (index >= MaxComplications)
                break;
            var itemPath = $"{arrayPath}[{index}]";
            if (item.ValueKind != JsonValueKind.Object)
            {
                AddIssue(
                    issues,
                    itemPath,
                    "wound_materialization_invalid_field",
                    "complication object",
                    item.ValueKind.ToString());
                index++;
                continue;
            }

            ValidateObjectShape(item, itemPath, ComplicationFields, ComplicationFields, issues);
            var complicationId = ReadExactIdentifier(item, "complicationId", itemPath, issues);
            AddUniqueIdentifier(identifiers, complicationId, itemPath + ".complicationId", issues);
            parsed.Add(new WoundComplication(
                complicationId,
                ReadClosedString(item, "kind", itemPath, ComplicationKinds, issues),
                ReadExactIdentifier(item, "state", itemPath, issues),
                ReadText(item, "displayName", itemPath, issues),
                ReadInt32(item, "treatmentDifficultyModifier", itemPath, 0, 4, issues),
                ReadIdentifierArray(item, "ownedEffectIds", itemPath, unique: true, issues),
                ReadClosedString(item, "visibility", itemPath, Visibilities, issues)));
            index++;
        }

        return parsed.ToImmutable();
    }

    private static WoundConsequences ParseConsequences(
        JsonElement value,
        string path,
        string woundId,
        string lifecycle,
        WoundOwnerCoordinate owner,
        WoundClassification classification,
        WoundSeverity severity,
        WoundCare care,
        IReadOnlyList<WoundComplication> complications,
        WoundRecovery recovery,
        List<ValidationIssue> issues)
    {
        ValidateObjectShape(value, path, ConsequencesFields, ConsequencesFields, issues);
        var slotBudget = ReadInt32(value, "slotBudget", path, 0, MaxConsequences, issues);
        var slotsUsed = ReadInt32(value, "slotsUsed", path, 0, MaxConsequences, issues);
        var hasEntries = TryReadArray(value, "entries", path, out var entries, issues);
        if (hasEntries)
            AddLimitIssue(entries, path + ".entries", MaxConsequences, "consequences", issues);

        var parsed = ImmutableArray.CreateBuilder<WoundConsequenceEntry>();
        var slots = new HashSet<int>();
        var index = 0;
        foreach (var item in hasEntries
                     ? entries.EnumerateArray()
                     : Enumerable.Empty<JsonElement>())
        {
            if (index >= MaxConsequences)
                break;
            var itemPath = $"{path}.entries[{index}]";
            if (item.ValueKind != JsonValueKind.Object)
            {
                AddIssue(
                    issues,
                    itemPath,
                    "wound_materialization_invalid_field",
                    "consequence object",
                    item.ValueKind.ToString());
                index++;
                continue;
            }

            ValidateObjectShape(item, itemPath, ConsequenceEntryFields, ConsequenceEntryFields, issues);
            var slot = ReadInt32(item, "slot", itemPath, 1, MaxConsequences, issues);
            if (slot is >= 1 and <= MaxConsequences && !slots.Add(slot))
            {
                AddIssue(
                    issues,
                    itemPath + ".slot",
                    "wound_materialization_duplicate_coordinate",
                    "one unique consequence slot in this wound",
                    slot.ToString(CultureInfo.InvariantCulture));
            }
            parsed.Add(new WoundConsequenceEntry(
                slot,
                ReadExactIdentifier(item, "profileKey", itemPath, issues),
                ReadExactIdentifier(item, "effectId", itemPath, issues),
                ReadText(item, "readableSummary", itemPath, issues)));
            index++;
        }

        var entryCount = hasEntries ? entries.GetArrayLength() : 0;
        if (slotsUsed != entryCount)
        {
            AddIssue(
                issues,
                path + ".slotsUsed",
                "wound_materialization_invalid_field",
                "exact number of consequence entries",
                slotsUsed.ToString(CultureInfo.InvariantCulture));
        }
        if (slotBudget < slotsUsed)
        {
            AddIssue(
                issues,
                path + ".slotBudget",
                "wound_materialization_invalid_field",
                "slotBudget greater than or equal to slotsUsed",
                slotBudget.ToString(CultureInfo.InvariantCulture));
        }

        var ownedEffectSources = ParseOwnedEffectSources(
            value,
            path,
            owner.Realm,
            issues);
        var parsedEntries = parsed.ToImmutable();
        ValidateOwnedEffectSources(
            ownedEffectSources,
            path + ".ownedEffectSources",
            woundId,
            lifecycle,
            owner,
            classification,
            severity,
            care,
            complications,
            recovery,
            slotBudget,
            slotsUsed,
            parsedEntries,
            value,
            issues);

        return new WoundConsequences(slotBudget, slotsUsed, parsedEntries)
        {
            OwnedEffectSources = ownedEffectSources
        };
    }

    private static WoundOwnedEffectSources ParseOwnedEffectSources(
        JsonElement consequences,
        string path,
        string realm,
        List<ValidationIssue> issues)
    {
        var sourcesPath = path + ".ownedEffectSources";
        var value = ReadRequiredObject(consequences, "ownedEffectSources", path, issues);
        ValidateObjectShape(
            value,
            sourcesPath,
            OwnedEffectSourcesFields,
            OwnedEffectSourcesFields,
            issues);

        var hasDefinitions = TryReadArray(
            value,
            "definitions",
            sourcesPath,
            out var definitions,
            issues);
        var hasRootBindings = TryReadArray(
            value,
            "rootBindings",
            sourcesPath,
            out var rootBindings,
            issues);

        var definitionsWithinLimit = hasDefinitions &&
                                     definitions.GetArrayLength() <= MaxOwnedEffectDefinitions;
        var rootsWithinLimit = hasRootBindings &&
                               rootBindings.GetArrayLength() <= MaxOwnedEffectRootBindings;
        if (hasDefinitions)
        {
            AddLimitIssue(
                definitions,
                sourcesPath + ".definitions",
                MaxOwnedEffectDefinitions,
                "wound-owned effect definitions",
                issues);
        }
        if (hasRootBindings)
        {
            AddLimitIssue(
                rootBindings,
                sourcesPath + ".rootBindings",
                MaxOwnedEffectRootBindings,
                "wound-owned root bindings",
                issues);
        }

        var parsedDefinitions = ImmutableArray.CreateBuilder<JsonElement>();
        if (definitionsWithinLimit)
        {
            var definitionIndex = 0;
            foreach (var definition in definitions.EnumerateArray())
            {
                var definitionPath = $"{sourcesPath}.definitions[{definitionIndex++}]";
                if (definition.ValueKind != JsonValueKind.Object)
                {
                    AddIssue(
                        issues,
                        definitionPath,
                        "wound_materialization_invalid_field",
                        "complete wound-owned effect definition object",
                        definition.ValueKind.ToString());
                    continue;
                }
                parsedDefinitions.Add(definition.Clone());
            }

            foreach (var commonIssue in EffectSourceDefinitionContract.ValidateArray(
                         definitions,
                         sourcesPath + ".definitions",
                         realm))
            {
                AddIssue(
                    issues,
                    commonIssue.FilePath,
                    "wound_materialization_owned_source_graph_invalid",
                    commonIssue.Expected ?? "valid common effect source definition graph",
                    commonIssue.Actual ?? commonIssue.Code ?? "invalid");
            }
        }

        var parsedRoots = ImmutableArray.CreateBuilder<WoundRootEffectBinding>();
        if (rootsWithinLimit)
        {
            var rootIndex = 0;
            foreach (var root in rootBindings.EnumerateArray())
            {
                var rootPath = $"{sourcesPath}.rootBindings[{rootIndex++}]";
                if (root.ValueKind != JsonValueKind.Object)
                {
                    AddIssue(
                        issues,
                        rootPath,
                        "wound_materialization_invalid_field",
                        "closed wound root binding object",
                        root.ValueKind.ToString());
                    continue;
                }
                ValidateObjectShape(
                    root,
                    rootPath,
                    RootEffectBindingFields,
                    RootEffectBindingFields,
                    issues);
                parsedRoots.Add(new WoundRootEffectBinding(
                    ReadExactIdentifier(root, "effectId", rootPath, issues),
                    ReadExactIdentifier(root, "definitionKey", rootPath, issues)));
            }
        }

        return new WoundOwnedEffectSources(
            parsedDefinitions.ToImmutable(),
            parsedRoots.ToImmutable());
    }

    private static void ValidateOwnedEffectSources(
        WoundOwnedEffectSources sources,
        string path,
        string woundId,
        string lifecycle,
        WoundOwnerCoordinate owner,
        WoundClassification classification,
        WoundSeverity severity,
        WoundCare care,
        IReadOnlyList<WoundComplication> complications,
        WoundRecovery recovery,
        int slotBudget,
        int slotsUsed,
        IReadOnlyList<WoundConsequenceEntry> entries,
        JsonElement rawConsequences,
        List<ValidationIssue> issues)
    {
        var definitions = ReadOwnedDefinitionNodes(sources.Definitions, path);
        var definitionsByKey = new Dictionary<string, OwnedDefinitionNode>(StringComparer.Ordinal);
        var exactDefinitionKeys = new HashSet<string>(StringComparer.Ordinal);
        var confusableDefinitionKeys = new HashSet<string>(StringComparer.Ordinal);
        var exactStackKeys = new HashSet<string>(StringComparer.Ordinal);
        var confusableStackKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var definition in definitions)
        {
            AddUniqueOwnedIdentifier(
                exactDefinitionKeys,
                confusableDefinitionKeys,
                definition.DefinitionKey,
                definition.Path + ".definitionKey",
                "definitionKey",
                issues);
            AddUniqueOwnedIdentifier(
                exactStackKeys,
                confusableStackKeys,
                definition.StackKey,
                definition.Path + ".stacking.stackKey",
                "stacking.stackKey",
                issues);
            if (definition.DefinitionKey.Length > 0 &&
                !definitionsByKey.ContainsKey(definition.DefinitionKey))
            {
                definitionsByKey.Add(definition.DefinitionKey, definition);
            }
            if (definition.MaxStacks != 1)
            {
                AddOwnedGraphIssue(
                    issues,
                    definition.Path + ".stacking.maxStacks",
                    "exact integer 1 for a wound-owned definition",
                    definition.MaxStacks?.ToString(CultureInfo.InvariantCulture) ?? "missing");
            }
            ValidateOwnedDefinitionAuthority(definition, woundId, owner, classification, issues);
        }

        var rootsByEffect = new Dictionary<string, OwnedRootNode>(StringComparer.Ordinal);
        var rootsByDefinition = new Dictionary<string, OwnedRootNode>(StringComparer.Ordinal);
        var exactEffectIds = new HashSet<string>(StringComparer.Ordinal);
        var confusableEffectIds = new HashSet<string>(StringComparer.Ordinal);
        var exactRootDefinitionKeys = new HashSet<string>(StringComparer.Ordinal);
        var confusableRootDefinitionKeys = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < sources.RootBindings.Count; index++)
        {
            var binding = sources.RootBindings[index];
            var root = new OwnedRootNode(
                binding,
                $"{path}.rootBindings[{index}]");
            AddUniqueOwnedIdentifier(
                exactEffectIds,
                confusableEffectIds,
                binding.EffectId,
                root.Path + ".effectId",
                "root effectId",
                issues);
            AddUniqueOwnedIdentifier(
                exactRootDefinitionKeys,
                confusableRootDefinitionKeys,
                binding.DefinitionKey,
                root.Path + ".definitionKey",
                "root definitionKey",
                issues);
            if (binding.EffectId.Length > 0 && !rootsByEffect.ContainsKey(binding.EffectId))
                rootsByEffect.Add(binding.EffectId, root);
            if (binding.DefinitionKey.Length > 0 &&
                !rootsByDefinition.ContainsKey(binding.DefinitionKey))
            {
                rootsByDefinition.Add(binding.DefinitionKey, root);
            }
            if (binding.DefinitionKey.Length > 0 &&
                !definitionsByKey.ContainsKey(binding.DefinitionKey))
            {
                AddOwnedGraphIssue(
                    issues,
                    root.Path + ".definitionKey",
                    "definitionKey resolving to one definition in this wound-owned graph",
                    binding.DefinitionKey);
            }
        }

        ValidateComplicationRootOwnership(
            complications,
            rootsByEffect,
            path,
            issues);
        ValidateOwnedDefinitionGraph(
            definitions,
            definitionsByKey,
            rootsByDefinition,
            complications,
            severity,
            issues);
        ValidateOwnedSlotReciprocity(
            definitionsByKey,
            rootsByEffect,
            rootsByDefinition,
            entries,
            classification,
            severity,
            lifecycle,
            care,
            complications,
            recovery,
            slotBudget,
            slotsUsed,
            rawConsequences,
            path,
            issues);
    }

    private static ImmutableArray<OwnedDefinitionNode> ReadOwnedDefinitionNodes(
        IReadOnlyList<JsonElement> definitions,
        string path)
    {
        var result = ImmutableArray.CreateBuilder<OwnedDefinitionNode>();
        for (var index = 0; index < definitions.Count; index++)
        {
            var definition = definitions[index];
            if (definition.ValueKind != JsonValueKind.Object)
                continue;
            var definitionPath = $"{path}.definitions[{index}]";
            TryReadExactIdentifier(definition, "definitionKey", out var definitionKey);

            var stackKey = string.Empty;
            var stackPolicy = string.Empty;
            int? maxStacks = null;
            if (definition.TryGetProperty("stacking", out var stacking) &&
                stacking.ValueKind == JsonValueKind.Object)
            {
                TryReadExactIdentifier(stacking, "stackKey", out stackKey);
                TryReadStringValue(stacking, "policy", out stackPolicy);
                if (stacking.TryGetProperty("maxStacks", out var maximum) &&
                    TryReadExactInt32(maximum, out var parsedMaximum))
                {
                    maxStacks = parsedMaximum;
                }
            }

            var components = ImmutableArray.CreateBuilder<OwnedComponentNode>();
            var edges = ImmutableArray.CreateBuilder<OwnedApplyEdge>();
            if (definition.TryGetProperty("components", out var rawComponents) &&
                rawComponents.ValueKind == JsonValueKind.Array)
            {
                var componentIndex = 0;
                foreach (var component in rawComponents.EnumerateArray())
                {
                    var componentPath = $"{definitionPath}.components[{componentIndex++}]";
                    if (component.ValueKind != JsonValueKind.Object ||
                        !TryReadExactIdentifier(component, "profile", out var profile))
                    {
                        continue;
                    }
                    components.Add(new OwnedComponentNode(profile, componentPath, component));
                    if (!string.Equals(profile, "event_reaction", StringComparison.Ordinal) ||
                        !component.TryGetProperty("payload", out var payload) ||
                        payload.ValueKind != JsonValueKind.Object ||
                        !TryReadStringValue(payload, "resultKind", out var resultKind) ||
                        !string.Equals(resultKind, "apply_definition", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    TryReadExactIdentifier(payload, "definitionKey", out var targetDefinitionKey);
                    int? maximumExpansion = null;
                    if (payload.TryGetProperty("maxExpansion", out var rawMaximumExpansion) &&
                        TryReadExactInt32(rawMaximumExpansion, out var parsedExpansion))
                    {
                        maximumExpansion = parsedExpansion;
                    }
                    var parameters = payload.TryGetProperty("parameters", out var rawParameters)
                        ? rawParameters
                        : default;
                    edges.Add(new OwnedApplyEdge(
                        targetDefinitionKey,
                        componentPath + ".payload",
                        maximumExpansion,
                        parameters));
                }
            }

            var parameterBounds = definition.TryGetProperty("parameterBounds", out var bounds)
                ? bounds
                : default;
            result.Add(new OwnedDefinitionNode(
                definitionKey,
                stackKey,
                stackPolicy,
                maxStacks,
                definitionPath,
                definition,
                parameterBounds,
                components.ToImmutable(),
                edges.ToImmutable()));
        }
        return result.ToImmutable();
    }

    private static void ValidateOwnedDefinitionAuthority(
        OwnedDefinitionNode definition,
        string woundId,
        WoundOwnerCoordinate owner,
        WoundClassification classification,
        List<ValidationIssue> issues)
    {
        if (!ContainsExactString(definition.Element, "allowedRealms", owner.Realm))
        {
            AddOwnedGraphIssue(
                issues,
                definition.Path + ".allowedRealms",
                "array containing the wound's exact current realm",
                owner.Realm);
        }

        var targetKind = ResolveEffectTargetKind(owner.OwnerKind);
        if (targetKind.Length > 0 &&
            !ContainsExactString(definition.Element, "allowedTargetKinds", targetKind))
        {
            AddOwnedGraphIssue(
                issues,
                definition.Path + ".allowedTargetKinds",
                "array containing the wound owner's exact effect target kind",
                targetKind);
        }

        var sourceLinkCount = 0;
        if (definition.Element.TryGetProperty("links", out var links) &&
            links.ValueKind == JsonValueKind.Array)
        {
            var linkIndex = 0;
            foreach (var link in links.EnumerateArray())
            {
                var linkPath = $"{definition.Path}.links[{linkIndex++}]";
                if (link.ValueKind != JsonValueKind.Object ||
                    !TryReadStringValue(link, "role", out var role) ||
                    !string.Equals(role, "source", StringComparison.Ordinal))
                {
                    continue;
                }

                if (!TryReadStringValue(link, "kind", out var kind) ||
                    !string.Equals(kind, "wound", StringComparison.Ordinal))
                {
                    AddOwnedGraphIssue(
                        issues,
                        linkPath + ".kind",
                        "wound for every source-role link in a wound-owned definition",
                        kind.Length == 0 ? "missing or invalid" : kind);
                    continue;
                }

                sourceLinkCount++;
                if (sourceLinkCount > 1)
                {
                    AddOwnedGraphIssue(
                        issues,
                        linkPath,
                        "exactly one wound/source link per wound-owned definition",
                        "additional wound/source link");
                }

                if (!TryReadExactIdentifier(link, "targetId", out var targetId) ||
                    !string.Equals(targetId, woundId, StringComparison.Ordinal))
                {
                    AddOwnedGraphIssue(
                        issues,
                        definition.Path + ".links",
                        "one wound/source link to the exact owning wound",
                        targetId.Length == 0 ? "missing or invalid" : targetId);
                    AddOwnedGraphIssue(
                        issues,
                        linkPath + ".targetId",
                        "the exact owning woundId",
                        targetId.Length == 0 ? "missing or invalid" : targetId);
                }
            }
        }
        if (sourceLinkCount == 0)
        {
            AddOwnedGraphIssue(
                issues,
                definition.Path + ".links",
                "exactly one wound/source link to the owning wound",
                "none");
        }

        foreach (var component in definition.Components)
        {
            var allowedProfile = string.Equals(
                    classification.Domain,
                    "physical",
                    StringComparison.Ordinal)
                ? WoundConsequenceEnvelopeCatalog.MortalMechanicalProfiles.Contains(component.Profile) ||
                  WoundConsequenceEnvelopeCatalog.MortalZeroSlotProfiles.Contains(component.Profile)
                : string.Equals(classification.Domain, "spiritual", StringComparison.Ordinal) &&
                  (WoundConsequenceEnvelopeCatalog.SpiritualProfiles.Contains(component.Profile) ||
                   string.Equals(component.Profile, "wound_consequence", StringComparison.Ordinal));
            if (!allowedProfile)
            {
                AddOwnedGraphIssue(
                    issues,
                    component.Path + ".profile",
                    "registered consequence profile for the wound domain",
                    component.Profile);
            }

            if (!string.Equals(component.Profile, "wound_consequence", StringComparison.Ordinal) ||
                !component.Element.TryGetProperty("payload", out var payload) ||
                payload.ValueKind != JsonValueKind.Object)
            {
                continue;
            }
            if (!TryReadExactIdentifier(payload, "woundId", out var markerWoundId) ||
                !string.Equals(markerWoundId, woundId, StringComparison.Ordinal))
            {
                AddOwnedGraphIssue(
                    issues,
                    component.Path + ".payload.woundId",
                    "the exact owning woundId",
                    markerWoundId.Length == 0 ? "missing or invalid" : markerWoundId);
            }
        }
    }

    private static void ValidateComplicationRootOwnership(
        IReadOnlyList<WoundComplication> complications,
        IReadOnlyDictionary<string, OwnedRootNode> rootsByEffect,
        string path,
        List<ValidationIssue> issues)
    {
        var exactOwnedIds = new HashSet<string>(StringComparer.Ordinal);
        var confusableOwnedIds = new HashSet<string>(StringComparer.Ordinal);
        for (var complicationIndex = 0;
             complicationIndex < complications.Count;
             complicationIndex++)
        {
            var complication = complications[complicationIndex];
            for (var ownedIndex = 0;
                 ownedIndex < complication.OwnedEffectIds.Count;
                 ownedIndex++)
            {
                var effectId = complication.OwnedEffectIds[ownedIndex];
                var ownedPath = WoundRootPath(path) +
                                $".complications[{complicationIndex}].ownedEffectIds[{ownedIndex}]";
                if (effectId.Length == 0)
                    continue;
                if (!exactOwnedIds.Add(effectId) ||
                    !confusableOwnedIds.Add(MortalLocationIdentityState.BuildConfusableKey(effectId)))
                {
                    AddEffectBindingIssue(
                        issues,
                        ownedPath,
                        "pairwise-disjoint exact/confusable complication root ownership",
                        effectId);
                }
                if (!rootsByEffect.ContainsKey(effectId))
                {
                    AddEffectBindingIssue(
                        issues,
                        ownedPath,
                        "effectId resolving to one persisted wound root binding",
                        effectId);
                }
            }
        }
    }

    private static void ValidateOwnedDefinitionGraph(
        IReadOnlyList<OwnedDefinitionNode> definitions,
        IReadOnlyDictionary<string, OwnedDefinitionNode> definitionsByKey,
        IReadOnlyDictionary<string, OwnedRootNode> rootsByDefinition,
        IReadOnlyList<WoundComplication> complications,
        WoundSeverity severity,
        List<ValidationIssue> issues)
    {
        var markerCount = 0;
        foreach (var definition in definitions)
        {
            foreach (var component in definition.Components)
            {
                if (!string.Equals(component.Profile, "wound_consequence", StringComparison.Ordinal))
                    continue;
                markerCount++;
                if (markerCount > 1)
                {
                    AddOwnedGraphIssue(
                        issues,
                        component.Path + ".profile",
                        "at most one wound_consequence marker in the complete graph",
                        markerCount.ToString(CultureInfo.InvariantCulture));
                }
            }

            if (rootsByDefinition.ContainsKey(definition.DefinitionKey) &&
                !IsEmptyObject(definition.ParameterBounds))
            {
                AddOwnedGraphIssue(
                    issues,
                    definition.Path + ".parameterBounds",
                    "exact empty object for a directly bound root definition",
                    DescribeElement(definition.ParameterBounds));
            }
        }

        var allEdges = definitions
            .SelectMany(static definition => definition.ApplyEdges.Select(edge =>
                new OwnedGraphEdge(definition, edge)))
            .ToArray();
        for (var edgeIndex = 1; edgeIndex < allEdges.Length; edgeIndex++)
        {
            AddOwnedGraphIssue(
                issues,
                allEdges[edgeIndex].Edge.Path + ".definitionKey",
                "at most one wound-owned apply_definition edge",
                allEdges[edgeIndex].Edge.TargetDefinitionKey);
        }

        var inboundCounts = definitionsByKey.Keys.ToDictionary(
            static key => key,
            static _ => 0,
            StringComparer.Ordinal);
        foreach (var graphEdge in allEdges)
        {
            var source = graphEdge.Source;
            var edge = graphEdge.Edge;
            if (edge.MaximumExpansion != 2)
            {
                AddOwnedGraphIssue(
                    issues,
                    edge.Path + ".maxExpansion",
                    "exact integer 2 for the one wound-owned reaction and leaf",
                    edge.MaximumExpansion?.ToString(CultureInfo.InvariantCulture) ?? "missing");
            }
            if (severity.Rank < 3)
            {
                AddOwnedGraphIssue(
                    issues,
                    edge.Path + ".definitionKey",
                    "apply_definition only for severity rank III or IV",
                    severity.Rank.ToString(CultureInfo.InvariantCulture));
            }
            if (!definitionsByKey.TryGetValue(edge.TargetDefinitionKey, out var target))
            {
                AddOwnedGraphIssue(
                    issues,
                    edge.Path + ".definitionKey",
                    "one exact downstream definition in the same wound graph",
                    edge.TargetDefinitionKey.Length == 0 ? "missing" : edge.TargetDefinitionKey);
                continue;
            }

            inboundCounts[target.DefinitionKey]++;
            if (target.ApplyEdges.Length != 0)
            {
                AddOwnedGraphIssue(
                    issues,
                    target.ApplyEdges[0].Path + ".definitionKey",
                    "no nested wound-owned apply_definition edge",
                    target.ApplyEdges[0].TargetDefinitionKey);
            }

            if (!rootsByDefinition.TryGetValue(target.DefinitionKey, out var targetRoot))
                continue;
            if (!string.Equals(target.StackPolicy, "replace", StringComparison.Ordinal))
            {
                AddOwnedGraphIssue(
                    issues,
                    target.Path + ".stacking.policy",
                    "replace for a root-bound reaction target",
                    target.StackPolicy);
            }
            if (!IsEmptyObject(edge.Parameters))
            {
                AddOwnedGraphIssue(
                    issues,
                    edge.Path + ".parameters",
                    "exact empty parameters for a root-bound reaction target",
                    DescribeElement(edge.Parameters));
            }
            if (rootsByDefinition.TryGetValue(source.DefinitionKey, out var sourceRoot) &&
                !string.Equals(
                    ResolveOwnershipDomain(sourceRoot.Binding.EffectId, complications),
                    ResolveOwnershipDomain(targetRoot.Binding.EffectId, complications),
                    StringComparison.Ordinal))
            {
                AddOwnedGraphIssue(
                    issues,
                    edge.Path + ".definitionKey",
                    "reaction target in the same reconstructed root-ownership domain",
                    edge.TargetDefinitionKey);
            }
        }

        foreach (var definition in definitions)
        {
            if (rootsByDefinition.ContainsKey(definition.DefinitionKey))
                continue;
            inboundCounts.TryGetValue(definition.DefinitionKey, out var inboundCount);
            if (inboundCount != 1)
            {
                AddOwnedGraphIssue(
                    issues,
                    definition.Path,
                    "unbound definition reachable exactly once from a direct root",
                    inboundCount.ToString(CultureInfo.InvariantCulture));
            }
        }
    }

    private static void ValidateOwnedSlotReciprocity(
        IReadOnlyDictionary<string, OwnedDefinitionNode> definitionsByKey,
        IReadOnlyDictionary<string, OwnedRootNode> rootsByEffect,
        IReadOnlyDictionary<string, OwnedRootNode> rootsByDefinition,
        IReadOnlyList<WoundConsequenceEntry> entries,
        WoundClassification classification,
        WoundSeverity severity,
        string lifecycle,
        WoundCare care,
        IReadOnlyList<WoundComplication> complications,
        WoundRecovery recovery,
        int slotBudget,
        int slotsUsed,
        JsonElement rawConsequences,
        string path,
        List<ValidationIssue> issues)
    {
        var entriesByEffect = entries
            .Select((entry, index) => new OwnedEntryNode(
                entry,
                $"{WoundConsequencesPath(path)}.entries[{index}]"))
            .GroupBy(static item => item.Entry.EffectId, StringComparer.Ordinal)
            .ToDictionary(
                static group => group.Key,
                static group => group.ToArray(),
                StringComparer.Ordinal);

        foreach (var pair in entriesByEffect)
        {
            if (rootsByEffect.ContainsKey(pair.Key))
                continue;
            foreach (var entry in pair.Value)
            {
                AddEffectBindingIssue(
                    issues,
                    entry.Path + ".effectId",
                    "effectId resolving to one direct wound root binding",
                    entry.Entry.EffectId);
            }
        }

        var derivedSlots = 0;
        foreach (var root in rootsByEffect.Values)
        {
            if (!definitionsByKey.TryGetValue(root.Binding.DefinitionKey, out var definition))
                continue;
            var expectedProfiles = GetDirectSlotProfiles(definition);
            foreach (var edge in definition.ApplyEdges)
            {
                if (rootsByDefinition.ContainsKey(edge.TargetDefinitionKey) ||
                    !definitionsByKey.TryGetValue(edge.TargetDefinitionKey, out var leaf))
                {
                    continue;
                }
                expectedProfiles.AddRange(GetDirectSlotProfiles(leaf));
            }
            derivedSlots += expectedProfiles.Count;

            entriesByEffect.TryGetValue(root.Binding.EffectId, out var ownedEntries);
            ownedEntries ??= Array.Empty<OwnedEntryNode>();
            var remaining = new List<string>(expectedProfiles);
            foreach (var entry in ownedEntries)
            {
                var matchIndex = remaining.FindIndex(profile => string.Equals(
                    profile,
                    entry.Entry.ProfileKey,
                    StringComparison.Ordinal));
                if (matchIndex >= 0)
                {
                    remaining.RemoveAt(matchIndex);
                    continue;
                }
                AddConsequenceSlotIssue(
                    issues,
                    entry.Path + ".profileKey",
                    "profile matching one derived component slot of the bound root",
                    entry.Entry.ProfileKey);
            }
            if (remaining.Count != 0)
            {
                AddEffectBindingIssue(
                    issues,
                    root.Path + ".effectId",
                    "one reciprocal consequence entry for every derived mechanical slot",
                    string.Join(",", remaining));
            }
        }

        var consequencesPath = WoundConsequencesPath(path);
        var rawSlotsUsed = slotsUsed;
        if (rawConsequences.ValueKind == JsonValueKind.Object &&
            rawConsequences.TryGetProperty("slotsUsed", out var rawSlots) &&
            TryReadExactInt32(rawSlots, out var exactSlotsUsed))
        {
            rawSlotsUsed = exactSlotsUsed;
        }
        if (rawSlotsUsed != derivedSlots)
        {
            AddConsequenceSlotIssue(
                issues,
                consequencesPath + ".slotsUsed",
                "exact number of derived wound consequence slots",
                rawSlotsUsed.ToString(CultureInfo.InvariantCulture));
        }
        if (string.Equals(classification.Domain, "physical", StringComparison.Ordinal) &&
            rawSlotsUsed > severity.Rank)
        {
            AddConsequenceSlotIssue(
                issues,
                consequencesPath + ".slotsUsed",
                $"at most {severity.Rank} physical consequence slots",
                rawSlotsUsed.ToString(CultureInfo.InvariantCulture));
        }
        if (string.Equals(classification.Domain, "spiritual", StringComparison.Ordinal) &&
            rawSlotsUsed != severity.Rank)
        {
            AddConsequenceSlotIssue(
                issues,
                consequencesPath + ".slotsUsed",
                $"exactly {severity.Rank} spiritual consequence slots",
                rawSlotsUsed.ToString(CultureInfo.InvariantCulture));
        }
        if (slotBudget < rawSlotsUsed)
        {
            AddConsequenceSlotIssue(
                issues,
                consequencesPath + ".slotBudget",
                "slotBudget greater than or equal to derived slotsUsed",
                slotBudget.ToString(CultureInfo.InvariantCulture));
        }

        var orderedSlots = entries.Select(static entry => entry.Slot).Order().ToArray();
        for (var index = 0; index < orderedSlots.Length; index++)
        {
            if (orderedSlots[index] == index + 1)
                continue;
            var entryIndex = entries
                .Select((entry, originalIndex) => (entry, originalIndex))
                .First(pair => pair.entry.Slot == orderedSlots[index])
                .originalIndex;
            AddConsequenceSlotIssue(
                issues,
                $"{consequencesPath}.entries[{entryIndex}].slot",
                "contiguous one-based consequence slots",
                orderedSlots[index].ToString(CultureInfo.InvariantCulture));
        }

        var changesLegalLifecycle = complications.Any(static complication =>
                                        string.Equals(
                                            complication.State,
                                            "active",
                                            StringComparison.Ordinal)) ||
                                    recovery.Blockers.Count != 0 ||
                                    string.Equals(
                                        recovery.Mode,
                                        "no_natural_recovery",
                                        StringComparison.Ordinal) ||
                                    recovery.DeteriorationPolicy.HasValue ||
                                    care.ActiveCourseId is not null ||
                                    care.State is "fresh" or "untreated" or "recovering";
        if (string.Equals(lifecycle, "active", StringComparison.Ordinal) &&
            string.Equals(classification.Domain, "physical", StringComparison.Ordinal) &&
            derivedSlots == 0 &&
            !changesLegalLifecycle)
        {
            AddIssue(
                issues,
                consequencesPath + ".lifecycleEvidence",
                "wound_consequence_non_display_impact_required",
                "mechanical slot or proven complication/care/recovery lifecycle impact",
                "display-only or empty consequence graph");
        }
    }

    private static List<string> GetDirectSlotProfiles(OwnedDefinitionNode definition)
    {
        var profiles = new List<string>();
        foreach (var component in definition.Components)
        {
            if (string.Equals(component.Profile, "wound_consequence", StringComparison.Ordinal))
                continue;
            if (string.Equals(component.Profile, "roll_modifier", StringComparison.Ordinal) &&
                component.Element.TryGetProperty("payload", out var payload) &&
                payload.ValueKind == JsonValueKind.Object &&
                payload.TryGetProperty("operations", out var operations) &&
                operations.ValueKind == JsonValueKind.Array)
            {
                for (var index = 0; index < operations.GetArrayLength(); index++)
                    profiles.Add(component.Profile);
                continue;
            }
            profiles.Add(component.Profile);
        }
        return profiles;
    }

    private static string ResolveOwnershipDomain(
        string effectId,
        IReadOnlyList<WoundComplication> complications)
    {
        foreach (var complication in complications)
        {
            if (complication.OwnedEffectIds.Contains(effectId, StringComparer.Ordinal))
                return complication.ComplicationId;
        }
        return "base_wound";
    }

    private static string ResolveEffectTargetKind(string ownerKind) => ownerKind switch
    {
        "player" or "player_soul" => "player",
        "combatant" or "combatant_member" => "combatant",
        "npc" or "guardian" or "resident" or "radiant_actor" or "afterlife_actor" => ownerKind,
        _ => string.Empty
    };

    private static bool ContainsExactString(JsonElement root, string field, string expected)
    {
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty(field, out var array) ||
            array.ValueKind != JsonValueKind.Array)
        {
            return false;
        }
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String &&
                string.Equals(item.GetString(), expected, StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    private static bool TryReadExactIdentifier(
        JsonElement root,
        string field,
        out string value)
    {
        value = string.Empty;
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty(field, out var element) ||
            element.ValueKind != JsonValueKind.String)
        {
            return false;
        }
        var candidate = element.GetString();
        if (!ResourceMaterializationContract.IsExactIdentifier(candidate))
            return false;
        value = candidate!;
        return true;
    }

    private static bool TryReadStringValue(
        JsonElement root,
        string field,
        out string value)
    {
        value = string.Empty;
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty(field, out var element) ||
            element.ValueKind != JsonValueKind.String)
        {
            return false;
        }
        value = element.GetString() ?? string.Empty;
        return true;
    }

    private static bool IsEmptyObject(JsonElement value) =>
        value.ValueKind == JsonValueKind.Object && !value.EnumerateObject().Any();

    private static string DescribeElement(JsonElement value) =>
        value.ValueKind == JsonValueKind.Undefined ? "missing" : value.GetRawText();

    private static string WoundConsequencesPath(string ownedSourcesPath) =>
        ownedSourcesPath.EndsWith(".ownedEffectSources", StringComparison.Ordinal)
            ? ownedSourcesPath[..^".ownedEffectSources".Length]
            : ownedSourcesPath;

    private static string WoundRootPath(string ownedSourcesPath)
    {
        var consequencesPath = WoundConsequencesPath(ownedSourcesPath);
        return consequencesPath.EndsWith(".consequences", StringComparison.Ordinal)
            ? consequencesPath[..^".consequences".Length]
            : consequencesPath;
    }

    private static void AddUniqueOwnedIdentifier(
        HashSet<string> exact,
        HashSet<string> confusable,
        string value,
        string path,
        string label,
        List<ValidationIssue> issues)
    {
        if (value.Length == 0)
            return;
        if (!exact.Add(value) ||
            !confusable.Add(MortalLocationIdentityState.BuildConfusableKey(value)))
        {
            AddOwnedGraphIssue(
                issues,
                path,
                $"one exact/confusable-unique {label}",
                value);
        }
    }

    private static void AddOwnedGraphIssue(
        ICollection<ValidationIssue> issues,
        string path,
        string expected,
        string actual) =>
        AddIssue(
            issues,
            path,
            "wound_materialization_owned_source_graph_invalid",
            expected,
            actual);

    private static void AddEffectBindingIssue(
        ICollection<ValidationIssue> issues,
        string path,
        string expected,
        string actual) =>
        AddIssue(
            issues,
            path,
            "wound_materialization_effect_binding_invalid",
            expected,
            actual);

    private static void AddConsequenceSlotIssue(
        ICollection<ValidationIssue> issues,
        string path,
        string expected,
        string actual) =>
        AddIssue(
            issues,
            path,
            "wound_materialization_consequence_slot_invalid",
            expected,
            actual);

    private static WoundTreatment ParseTreatment(
        JsonElement value,
        string path,
        List<ValidationIssue> issues)
    {
        ValidateObjectShape(value, path, TreatmentFields, TreatmentFields, issues);
        var diagnosisPaths = ParseDiagnosisPaths(value, path, issues);
        var routes = ParseTreatmentRoutes(value, path, issues);
        return new WoundTreatment(
            diagnosisPaths,
            routes,
            ReadIdentifierArray(value, "knownRouteIds", path, unique: true, issues),
            ReadIdentifierArray(value, "completedRouteIds", path, unique: true, issues));
    }

    private static IReadOnlyList<WoundDiagnosisPath> ParseDiagnosisPaths(
        JsonElement treatment,
        string path,
        List<ValidationIssue> issues)
    {
        var arrayPath = path + ".diagnosisPaths";
        if (!TryReadArray(treatment, "diagnosisPaths", path, out var array, issues))
            return ImmutableArray<WoundDiagnosisPath>.Empty;
        AddLimitIssue(array, arrayPath, MaxDiagnosisPaths, "diagnosis paths", issues);

        var parsed = ImmutableArray.CreateBuilder<WoundDiagnosisPath>();
        var identifiers = new HashSet<string>(StringComparer.Ordinal);
        var index = 0;
        foreach (var item in array.EnumerateArray())
        {
            if (index >= MaxDiagnosisPaths)
                break;
            var itemPath = $"{arrayPath}[{index}]";
            if (item.ValueKind != JsonValueKind.Object)
            {
                AddIssue(
                    issues,
                    itemPath,
                    "wound_materialization_invalid_field",
                    "diagnosis path object",
                    item.ValueKind.ToString());
                index++;
                continue;
            }

            ValidateObjectShape(item, itemPath, DiagnosisPathFields, DiagnosisPathFields, issues);
            var diagnosisPathId = ReadExactIdentifier(item, "diagnosisPathId", itemPath, issues);
            AddUniqueIdentifier(identifiers, diagnosisPathId, itemPath + ".diagnosisPathId", issues);
            parsed.Add(new WoundDiagnosisPath(
                diagnosisPathId,
                ReadClosedString(item, "visibility", itemPath, Visibilities, issues),
                ReadRequirementArray(item, itemPath, issues),
                ReadOpaqueObject(item, "check", itemPath, issues),
                ReadIdentifierArray(item, "reveals", itemPath, unique: true, issues),
                ReadExactIdentifier(item, "failurePolicy", itemPath, issues)));
            index++;
        }

        return parsed.ToImmutable();
    }

    private static IReadOnlyList<WoundTreatmentRoute> ParseTreatmentRoutes(
        JsonElement treatment,
        string path,
        List<ValidationIssue> issues)
    {
        var arrayPath = path + ".routes";
        if (!TryReadArray(treatment, "routes", path, out var array, issues))
            return ImmutableArray<WoundTreatmentRoute>.Empty;
        AddLimitIssue(array, arrayPath, MaxTreatmentRoutes, "treatment routes", issues);

        var parsed = ImmutableArray.CreateBuilder<WoundTreatmentRoute>();
        var identifiers = new HashSet<string>(StringComparer.Ordinal);
        var index = 0;
        foreach (var item in array.EnumerateArray())
        {
            if (index >= MaxTreatmentRoutes)
                break;
            var itemPath = $"{arrayPath}[{index}]";
            if (item.ValueKind != JsonValueKind.Object)
            {
                AddIssue(
                    issues,
                    itemPath,
                    "wound_materialization_invalid_field",
                    "treatment route object",
                    item.ValueKind.ToString());
                index++;
                continue;
            }

            ValidateObjectShape(item, itemPath, TreatmentRouteFields, TreatmentRouteFields, issues);
            var routeId = ReadExactIdentifier(item, "routeId", itemPath, issues);
            AddUniqueIdentifier(identifiers, routeId, itemPath + ".routeId", issues);
            parsed.Add(new WoundTreatmentRoute(
                routeId,
                ReadText(item, "displayName", itemPath, issues),
                ReadClosedString(item, "visibility", itemPath, Visibilities, issues),
                ReadClosedString(item, "mode", itemPath, RouteModes, issues),
                ReadRequirementArray(item, itemPath, issues),
                ReadOpaqueObject(item, "resourcePolicy", itemPath, issues),
                ReadOpaqueObject(item, "resolution", itemPath, issues),
                ReadOpaqueObjectArray(item, "outcomes", itemPath, issues),
                ReadNullableOpaqueObject(item, "interruption", itemPath, issues)));
            index++;
        }

        return parsed.ToImmutable();
    }

    private static WoundRecovery ParseRecovery(
        JsonElement value,
        string path,
        List<ValidationIssue> issues)
    {
        ValidateObjectShape(value, path, RecoveryFields, RecoveryFields, issues);
        return new WoundRecovery(
            ReadClosedString(value, "mode", path, RecoveryModes, issues),
            ReadExactIdentifier(value, "clockKind", path, issues),
            ReadInt64(value, "cadence", path, 1, long.MaxValue, issues),
            ReadInt64(value, "currentStepProgress", path, 0, long.MaxValue, issues),
            ReadInt64(value, "currentStepThreshold", path, 1, long.MaxValue, issues),
            ReadNullableExactIdentifier(value, "lastTickKey", path, issues),
            ReadIdentifierArray(value, "blockers", path, unique: true, issues),
            ReadBoolean(value, "carryOverflow", path, issues),
            ReadNullableOpaqueObject(value, "deteriorationPolicy", path, issues));
    }

    private static WoundRelations ParseRelations(
        JsonElement value,
        string path,
        List<ValidationIssue> issues)
    {
        ValidateObjectShape(value, path, RelationsFields, RelationsFields, issues);
        return new WoundRelations(
            ReadNullableExactIdentifier(value, "priorWoundId", path, issues),
            ReadIdentifierArray(value, "legacyRefs", path, unique: true, issues),
            ReadIdentifierArray(value, "independentEffectRefs", path, unique: true, issues));
    }

    private static WoundLastTransition ParseLastTransition(
        JsonElement value,
        string path,
        List<ValidationIssue> issues)
    {
        ValidateObjectShape(value, path, LastTransitionFields, LastTransitionFields, issues);
        return new WoundLastTransition(
            ReadExactIdentifier(value, "transitionId", path, issues),
            ReadInt32(value, "ordinal", path, 1, int.MaxValue, issues),
            ReadInt32(value, "turn", path, 0, int.MaxValue, issues),
            ReadClosedString(value, "kind", path, TransitionKinds, issues));
    }

    private static IReadOnlyList<JsonElement> ReadRequirementArray(
        JsonElement parent,
        string path,
        List<ValidationIssue> issues)
    {
        var arrayPath = path + ".requirements";
        if (!TryReadArray(parent, "requirements", path, out var array, issues))
            return ImmutableArray<JsonElement>.Empty;
        AddLimitIssue(
            array,
            arrayPath,
            MaxRequirementsPerTreatmentMember,
            "requirements",
            issues);

        var result = ImmutableArray.CreateBuilder<JsonElement>();
        var index = 0;
        foreach (var item in array.EnumerateArray())
        {
            if (index >= MaxRequirementsPerTreatmentMember)
                break;
            var itemPath = $"{arrayPath}[{index}]";
            if (item.ValueKind != JsonValueKind.Object)
            {
                AddIssue(
                    issues,
                    itemPath,
                    "wound_materialization_invalid_field",
                    "requirement object",
                    item.ValueKind.ToString());
                index++;
                continue;
            }
            if (!item.TryGetProperty("kind", out var kind))
            {
                AddIssue(
                    issues,
                    itemPath + ".kind",
                    "wound_materialization_missing_field",
                    "required requirement discriminator",
                    "missing");
            }
            else if (kind.ValueKind != JsonValueKind.String ||
                     !ResourceMaterializationContract.IsExactIdentifier(kind.GetString()))
            {
                AddIssue(
                    issues,
                    itemPath + ".kind",
                    "wound_materialization_invalid_identifier",
                    "exact requirement discriminator",
                    kind.GetRawText());
            }
            result.Add(item.Clone());
            index++;
        }
        return result.ToImmutable();
    }

    private static IReadOnlyList<JsonElement> ReadOpaqueObjectArray(
        JsonElement parent,
        string field,
        string path,
        List<ValidationIssue> issues)
    {
        if (!TryReadArray(parent, field, path, out var array, issues))
            return ImmutableArray<JsonElement>.Empty;
        var result = ImmutableArray.CreateBuilder<JsonElement>();
        var index = 0;
        foreach (var item in array.EnumerateArray())
        {
            var itemPath = $"{path}.{field}[{index++}]";
            if (item.ValueKind != JsonValueKind.Object)
            {
                AddIssue(
                    issues,
                    itemPath,
                    "wound_materialization_invalid_field",
                    "object",
                    item.ValueKind.ToString());
                continue;
            }
            result.Add(item.Clone());
        }
        return result.ToImmutable();
    }

    private static JsonElement ReadOpaqueObject(
        JsonElement parent,
        string field,
        string path,
        List<ValidationIssue> issues)
    {
        if (parent.ValueKind != JsonValueKind.Object ||
            !parent.TryGetProperty(field, out var value))
            return default;
        if (value.ValueKind != JsonValueKind.Object)
        {
            AddIssue(
                issues,
                path + "." + field,
                "wound_materialization_invalid_field",
                "object",
                value.ValueKind.ToString());
            return default;
        }
        return value.Clone();
    }

    private static JsonElement? ReadNullableOpaqueObject(
        JsonElement parent,
        string field,
        string path,
        List<ValidationIssue> issues)
    {
        if (parent.ValueKind != JsonValueKind.Object ||
            !parent.TryGetProperty(field, out var value))
            return null;
        if (value.ValueKind == JsonValueKind.Null)
            return null;
        if (value.ValueKind != JsonValueKind.Object)
        {
            AddIssue(
                issues,
                path + "." + field,
                "wound_materialization_invalid_field",
                "object or null",
                value.ValueKind.ToString());
            return null;
        }
        return value.Clone();
    }

    private static IReadOnlyList<string> ReadTextArray(
        JsonElement parent,
        string field,
        string path,
        List<ValidationIssue> issues)
    {
        if (!TryReadArray(parent, field, path, out var array, issues))
            return ImmutableArray<string>.Empty;
        var result = ImmutableArray.CreateBuilder<string>();
        var index = 0;
        foreach (var item in array.EnumerateArray())
        {
            var itemPath = $"{path}.{field}[{index++}]";
            if (!TryReadText(item, out var text))
            {
                AddIssue(
                    issues,
                    itemPath,
                    "wound_materialization_invalid_field",
                    $"non-empty trimmed readable text up to {MaxReadableTextLength} characters",
                    item.GetRawText());
                continue;
            }
            result.Add(text);
        }
        return result.ToImmutable();
    }

    private static IReadOnlyList<string> ReadIdentifierArray(
        JsonElement parent,
        string field,
        string path,
        bool unique,
        List<ValidationIssue> issues)
    {
        if (!TryReadArray(parent, field, path, out var array, issues))
            return ImmutableArray<string>.Empty;
        var result = ImmutableArray.CreateBuilder<string>();
        var identifiers = new HashSet<string>(StringComparer.Ordinal);
        var index = 0;
        foreach (var item in array.EnumerateArray())
        {
            var itemPath = $"{path}.{field}[{index++}]";
            if (item.ValueKind != JsonValueKind.String)
            {
                AddIssue(
                    issues,
                    itemPath,
                    "wound_materialization_invalid_field",
                    "exact identifier string",
                    item.ValueKind.ToString());
                continue;
            }
            var identifier = item.GetString() ?? string.Empty;
            if (!ResourceMaterializationContract.IsExactIdentifier(identifier))
            {
                AddIssue(
                    issues,
                    itemPath,
                    "wound_materialization_invalid_identifier",
                    "non-empty trimmed NFKC exact identifier without control or separator characters",
                    item.GetRawText());
                continue;
            }
            if (unique && !identifiers.Add(identifier))
            {
                AddIssue(
                    issues,
                    itemPath,
                    "wound_materialization_duplicate_identifier",
                    "one occurrence of each exact identifier",
                    identifier);
                continue;
            }
            result.Add(identifier);
        }
        return result.ToImmutable();
    }

    private static string ReadText(
        JsonElement parent,
        string field,
        string path,
        List<ValidationIssue> issues)
    {
        if (parent.ValueKind != JsonValueKind.Object ||
            !parent.TryGetProperty(field, out var value))
            return string.Empty;
        if (TryReadText(value, out var text))
            return text;
        AddIssue(
            issues,
            path + "." + field,
            "wound_materialization_invalid_field",
            $"non-empty trimmed readable text up to {MaxReadableTextLength} characters",
            value.GetRawText());
        return string.Empty;
    }

    private static bool TryReadText(JsonElement value, out string text)
    {
        text = value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
        return text.Length is > 0 and <= MaxReadableTextLength &&
               !string.IsNullOrWhiteSpace(text) &&
               string.Equals(text, text.Trim(), StringComparison.Ordinal);
    }

    private static string ReadExactIdentifier(
        JsonElement parent,
        string field,
        string path,
        List<ValidationIssue> issues)
    {
        if (parent.ValueKind != JsonValueKind.Object ||
            !parent.TryGetProperty(field, out var value))
            return string.Empty;
        var identifier = value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
        if (ResourceMaterializationContract.IsExactIdentifier(identifier))
            return identifier!;
        AddIssue(
            issues,
            path + "." + field,
            "wound_materialization_invalid_identifier",
            "non-empty trimmed NFKC exact identifier without control or separator characters",
            value.GetRawText());
        return string.Empty;
    }

    private static string? ReadNullableExactIdentifier(
        JsonElement parent,
        string field,
        string path,
        List<ValidationIssue> issues)
    {
        if (parent.ValueKind != JsonValueKind.Object ||
            !parent.TryGetProperty(field, out var value) ||
            value.ValueKind == JsonValueKind.Null)
            return null;
        var identifier = value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
        if (ResourceMaterializationContract.IsExactIdentifier(identifier))
            return identifier;
        AddIssue(
            issues,
            path + "." + field,
            "wound_materialization_invalid_identifier",
            "null or non-empty trimmed NFKC exact identifier without control or separator characters",
            value.GetRawText());
        return null;
    }

    private static string? ReadOptionalExactIdentifier(
        JsonElement parent,
        string field,
        string path,
        List<ValidationIssue> issues)
    {
        if (parent.ValueKind != JsonValueKind.Object ||
            !parent.TryGetProperty(field, out var value))
            return null;
        var identifier = value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
        if (ResourceMaterializationContract.IsExactIdentifier(identifier))
            return identifier;
        AddIssue(
            issues,
            path + "." + field,
            "wound_materialization_invalid_identifier",
            "non-empty trimmed NFKC exact identifier when present",
            value.GetRawText());
        return null;
    }

    private static string ReadClosedString(
        JsonElement parent,
        string field,
        string path,
        IEnumerable<string> allowed,
        List<ValidationIssue> issues)
    {
        if (parent.ValueKind != JsonValueKind.Object ||
            !parent.TryGetProperty(field, out var value))
            return string.Empty;
        var text = value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
        if (allowed.Contains(text, StringComparer.Ordinal))
            return text;
        AddIssue(
            issues,
            path + "." + field,
            "wound_materialization_invalid_field",
            "one ordinal current-schema value: " + string.Join(", ", allowed),
            value.GetRawText());
        return string.Empty;
    }

    private static int ReadExactSchemaVersion(
        JsonElement root,
        string path,
        List<ValidationIssue> issues)
    {
        if (!root.TryGetProperty("schemaVersion", out var value))
            return 0;
        if (value.ValueKind == JsonValueKind.Number &&
            value.GetRawText() == SchemaVersion.ToString(CultureInfo.InvariantCulture))
        {
            return SchemaVersion;
        }
        AddIssue(
            issues,
            path + ".schemaVersion",
            "wound_materialization_invalid_field",
            "exact integer 1",
            value.GetRawText());
        return 0;
    }

    private static int ReadInt32(
        JsonElement parent,
        string field,
        string path,
        int minimum,
        int maximum,
        List<ValidationIssue> issues)
    {
        if (parent.ValueKind != JsonValueKind.Object ||
            !parent.TryGetProperty(field, out var value))
            return 0;
        if (TryReadExactInt32(value, out var result) && result >= minimum && result <= maximum)
            return result;
        AddIssue(
            issues,
            path + "." + field,
            "wound_materialization_invalid_field",
            $"exact integer {minimum}..{maximum}",
            value.GetRawText());
        return 0;
    }

    private static int? ReadNullableInt32(
        JsonElement parent,
        string field,
        string path,
        int minimum,
        int maximum,
        List<ValidationIssue> issues)
    {
        if (parent.ValueKind != JsonValueKind.Object ||
            !parent.TryGetProperty(field, out var value) ||
            value.ValueKind == JsonValueKind.Null)
            return null;
        if (TryReadExactInt32(value, out var result) && result >= minimum && result <= maximum)
            return result;
        AddIssue(
            issues,
            path + "." + field,
            "wound_materialization_invalid_field",
            $"null or exact integer {minimum}..{maximum}",
            value.GetRawText());
        return null;
    }

    private static long ReadInt64(
        JsonElement parent,
        string field,
        string path,
        long minimum,
        long maximum,
        List<ValidationIssue> issues)
    {
        if (parent.ValueKind != JsonValueKind.Object ||
            !parent.TryGetProperty(field, out var value))
            return 0;
        if (TryReadExactInt64(value, out var result) && result >= minimum && result <= maximum)
            return result;
        AddIssue(
            issues,
            path + "." + field,
            "wound_materialization_invalid_field",
            $"exact integer {minimum}..{maximum}",
            value.GetRawText());
        return 0;
    }

    private static bool ReadBoolean(
        JsonElement parent,
        string field,
        string path,
        List<ValidationIssue> issues)
    {
        if (parent.ValueKind != JsonValueKind.Object ||
            !parent.TryGetProperty(field, out var value))
            return false;
        if (value.ValueKind is JsonValueKind.True or JsonValueKind.False)
            return value.GetBoolean();
        AddIssue(
            issues,
            path + "." + field,
            "wound_materialization_invalid_field",
            "boolean",
            value.GetRawText());
        return false;
    }

    private static bool TryReadExactInt32(JsonElement value, out int result)
    {
        result = 0;
        if (value.ValueKind != JsonValueKind.Number ||
            !value.TryGetInt32(out result))
        {
            return false;
        }
        return IsIntegerLexeme(value.GetRawText());
    }

    private static bool TryReadExactInt64(JsonElement value, out long result)
    {
        result = 0;
        if (value.ValueKind != JsonValueKind.Number ||
            !value.TryGetInt64(out result))
        {
            return false;
        }
        return IsIntegerLexeme(value.GetRawText());
    }

    private static bool IsIntegerLexeme(string raw) =>
        raw.IndexOfAny(new[] { '.', 'e', 'E' }) < 0;

    private static JsonElement ReadRequiredObject(
        JsonElement parent,
        string field,
        string path,
        List<ValidationIssue> issues)
    {
        if (parent.ValueKind != JsonValueKind.Object ||
            !parent.TryGetProperty(field, out var value))
            return default;
        if (value.ValueKind == JsonValueKind.Object)
            return value;
        AddIssue(
            issues,
            path + "." + field,
            "wound_materialization_invalid_field",
            "object",
            value.ValueKind.ToString());
        return default;
    }

    private static bool TryReadArray(
        JsonElement parent,
        string field,
        string path,
        out JsonElement array,
        List<ValidationIssue> issues)
    {
        array = default;
        if (parent.ValueKind != JsonValueKind.Object ||
            !parent.TryGetProperty(field, out var value))
            return false;
        if (value.ValueKind == JsonValueKind.Array)
        {
            array = value;
            return true;
        }
        AddIssue(
            issues,
            path + "." + field,
            "wound_materialization_invalid_field",
            "array",
            value.ValueKind.ToString());
        return false;
    }

    private static void AddLimitIssue(
        JsonElement array,
        string path,
        int maximum,
        string label,
        List<ValidationIssue> issues)
    {
        if (array.GetArrayLength() <= maximum)
            return;
        AddIssue(
            issues,
            path,
            "wound_materialization_limit_exceeded",
            $"at most {maximum} {label}",
            array.GetArrayLength().ToString(CultureInfo.InvariantCulture));
    }

    private static void AddUniqueIdentifier(
        HashSet<string> identifiers,
        string identifier,
        string path,
        List<ValidationIssue> issues)
    {
        if (identifier.Length == 0 || identifiers.Add(identifier))
            return;
        AddIssue(
            issues,
            path,
            "wound_materialization_duplicate_identifier",
            "one occurrence of each exact identifier",
            identifier);
    }

    private static void ValidateObjectShape(
        JsonElement value,
        string path,
        IReadOnlySet<string> allowed,
        IReadOnlySet<string> required,
        List<ValidationIssue> issues)
    {
        if (value.ValueKind != JsonValueKind.Object)
            return;
        ValidateClosedObject(value, path, allowed, issues);
        RequireFields(value, path, required, issues);
    }

    private static void ValidateClosedObject(
        JsonElement value,
        string path,
        IReadOnlySet<string> allowed,
        List<ValidationIssue> issues)
    {
        if (value.ValueKind != JsonValueKind.Object)
            return;
        foreach (var property in value.EnumerateObject())
        {
            if (!allowed.Contains(property.Name))
            {
                AddIssue(
                    issues,
                    path + "." + property.Name,
                    "wound_materialization_unknown_field",
                    "registered current-schema field",
                    property.Name);
            }
        }
    }

    private static void RequireFields(
        JsonElement value,
        string path,
        IReadOnlySet<string> required,
        List<ValidationIssue> issues)
    {
        if (value.ValueKind != JsonValueKind.Object)
            return;
        foreach (var field in required)
        {
            if (!value.TryGetProperty(field, out _))
            {
                AddIssue(
                    issues,
                    path + "." + field,
                    "wound_materialization_missing_field",
                    "required final version-1 field",
                    "missing");
            }
        }
    }

    private static void FindDuplicateProperties(
        JsonElement value,
        string path,
        List<ValidationIssue> issues)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (var property in value.EnumerateObject())
                {
                    var propertyPath = path + "." + property.Name;
                    if (!names.Add(property.Name))
                    {
                        AddIssue(
                            issues,
                            propertyPath,
                            "wound_materialization_duplicate_property",
                            "one occurrence of each exact property",
                            property.Name);
                    }
                    FindDuplicateProperties(property.Value, propertyPath, issues);
                }
                break;
            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in value.EnumerateArray())
                {
                    var maximum = path.EndsWith(
                            ".consequences.ownedEffectSources.definitions",
                            StringComparison.Ordinal)
                        ? MaxOwnedEffectDefinitions
                        : path.EndsWith(
                            ".consequences.ownedEffectSources.rootBindings",
                            StringComparison.Ordinal)
                            ? MaxOwnedEffectRootBindings
                            : int.MaxValue;
                    if (index >= maximum)
                        break;
                    FindDuplicateProperties(item, $"{path}[{index++}]", issues);
                }
                break;
        }
    }

    private static WoundMaterializationParseResult InvalidResult(
        string path,
        string code,
        string expected,
        string actual)
    {
        var issues = ImmutableArray.CreateBuilder<ValidationIssue>();
        AddIssue(issues, path, code, expected, actual);
        return new WoundMaterializationParseResult(null, issues.ToImmutable());
    }

    private static void AddIssue(
        ICollection<ValidationIssue> issues,
        string path,
        string code,
        string expected,
        string actual) =>
        issues.Add(new ValidationIssue(
            path,
            IssueSeverity.Error,
            "Active wound violates the complete current materialization contract.",
            code: code,
            actor: "Client",
            section: "wound_materialization",
            expected: expected,
            actual: actual,
            repairHint: "Resubmit one complete final version-1 wound envelope without draft aliases, unknown fields, or invalid client references."));

    private static IReadOnlySet<string> Set(params string[] values) =>
        new HashSet<string>(values, StringComparer.Ordinal);

    private static void WriteOwner(Utf8JsonWriter writer, WoundOwnerCoordinate owner)
    {
        writer.WritePropertyName("owner");
        writer.WriteStartObject();
        writer.WriteString("realm", owner.Realm);
        writer.WriteString("ownerKind", owner.OwnerKind);
        writer.WriteString("ownerId", owner.OwnerId);
        writer.WriteString("carrierPath", owner.CarrierPath);
        writer.WriteEndObject();
    }

    private static void WriteOrigin(Utf8JsonWriter writer, WoundOrigin origin)
    {
        writer.WritePropertyName("origin");
        writer.WriteStartObject();
        writer.WriteString("eventRef", origin.EventRef);
        writer.WriteString("sourceKind", origin.SourceKind);
        writer.WriteString("sourceId", origin.SourceId);
        writer.WriteString("sourceState", origin.SourceState);
        writer.WriteNumber("createdAtTurn", origin.CreatedAtTurn);
        WriteNullableString(writer, "createdAtCycleId", origin.CreatedAtCycleId);
        writer.WriteString("opportunityId", origin.OpportunityId);
        WriteNullableString(writer, "guaranteedTriggerId", origin.GuaranteedTriggerId);
        writer.WriteString("readableCause", origin.ReadableCause);
        writer.WriteEndObject();
    }

    private static void WriteClassification(Utf8JsonWriter writer, WoundClassification classification)
    {
        writer.WritePropertyName("classification");
        writer.WriteStartObject();
        writer.WriteString("domain", classification.Domain);
        writer.WriteString("woundType", classification.WoundType);
        writer.WritePropertyName("locationProfile");
        writer.WriteStartObject();
        writer.WriteString("kind", classification.LocationProfile.Kind);
        writer.WriteString("readableLocus", classification.LocationProfile.ReadableLocus);
        WriteOptionalString(writer, "authorityKind", classification.LocationProfile.AuthorityKind);
        WriteOptionalString(writer, "authorityRef", classification.LocationProfile.AuthorityRef);
        WriteOptionalString(writer, "affectedSide", classification.LocationProfile.AffectedSide);
        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    private static void WriteDisplay(Utf8JsonWriter writer, WoundDisplay display)
    {
        writer.WritePropertyName("display");
        writer.WriteStartObject();
        writer.WriteString("name", display.Name);
        writer.WriteString("description", display.Description);
        WriteStringArray(writer, "visibleSymptoms", display.VisibleSymptoms);
        writer.WriteString("prognosis", display.Prognosis);
        writer.WriteString("visibility", display.Visibility);
        writer.WriteString("acquisitionNarration", display.AcquisitionNarration);
        writer.WriteEndObject();
    }

    private static void WriteSeverity(Utf8JsonWriter writer, WoundSeverity severity)
    {
        writer.WritePropertyName("severity");
        writer.WriteStartObject();
        writer.WriteString("value", severity.Value);
        writer.WriteNumber("rank", severity.Rank);
        writer.WriteString("maximumAtCreation", severity.MaximumAtCreation);
        writer.WriteString("lastChangeEventRef", severity.LastChangeEventRef);
        writer.WriteEndObject();
    }

    private static void WriteCare(Utf8JsonWriter writer, WoundCare care)
    {
        writer.WritePropertyName("care");
        writer.WriteStartObject();
        writer.WriteString("state", care.State);
        if (care.StabilizedAtTurn.HasValue)
            writer.WriteNumber("stabilizedAtTurn", care.StabilizedAtTurn.Value);
        else
            writer.WriteNull("stabilizedAtTurn");
        WriteNullableString(writer, "activeCourseId", care.ActiveCourseId);
        WriteNullableString(writer, "lastAttemptId", care.LastAttemptId);
        writer.WriteEndObject();
    }

    private static void WriteComplications(
        Utf8JsonWriter writer,
        IReadOnlyList<WoundComplication> complications)
    {
        writer.WritePropertyName("complications");
        writer.WriteStartArray();
        foreach (var complication in complications)
        {
            writer.WriteStartObject();
            writer.WriteString("complicationId", complication.ComplicationId);
            writer.WriteString("kind", complication.Kind);
            writer.WriteString("state", complication.State);
            writer.WriteString("displayName", complication.DisplayName);
            writer.WriteNumber("treatmentDifficultyModifier", complication.TreatmentDifficultyModifier);
            WriteStringArray(writer, "ownedEffectIds", complication.OwnedEffectIds);
            writer.WriteString("visibility", complication.Visibility);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
    }

    private static void WriteConsequences(Utf8JsonWriter writer, WoundConsequences consequences)
    {
        writer.WritePropertyName("consequences");
        writer.WriteStartObject();
        writer.WriteNumber("slotBudget", consequences.SlotBudget);
        writer.WriteNumber("slotsUsed", consequences.SlotsUsed);
        writer.WritePropertyName("ownedEffectSources");
        writer.WriteStartObject();
        writer.WritePropertyName("definitions");
        writer.WriteStartArray();
        foreach (var definition in consequences.OwnedEffectSources.Definitions.OrderBy(
                     static definition => ReadDefinitionKeyForOrdering(definition),
                     StringComparer.Ordinal))
        {
            WriteCanonicalElement(writer, definition);
        }
        writer.WriteEndArray();
        writer.WritePropertyName("rootBindings");
        writer.WriteStartArray();
        foreach (var binding in consequences.OwnedEffectSources.RootBindings
                     .OrderBy(static binding => binding.EffectId, StringComparer.Ordinal)
                     .ThenBy(static binding => binding.DefinitionKey, StringComparer.Ordinal))
        {
            writer.WriteStartObject();
            writer.WriteString("effectId", binding.EffectId);
            writer.WriteString("definitionKey", binding.DefinitionKey);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        writer.WriteEndObject();
        writer.WritePropertyName("entries");
        writer.WriteStartArray();
        foreach (var entry in consequences.Entries.OrderBy(static entry => entry.Slot))
        {
            writer.WriteStartObject();
            writer.WriteNumber("slot", entry.Slot);
            writer.WriteString("profileKey", entry.ProfileKey);
            writer.WriteString("effectId", entry.EffectId);
            writer.WriteString("readableSummary", entry.ReadableSummary);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static string ReadDefinitionKeyForOrdering(JsonElement definition) =>
        TryReadExactIdentifier(definition, "definitionKey", out var definitionKey)
            ? definitionKey
            : string.Empty;

    private static void WriteTreatment(Utf8JsonWriter writer, WoundTreatment treatment)
    {
        writer.WritePropertyName("treatment");
        writer.WriteStartObject();
        writer.WritePropertyName("diagnosisPaths");
        writer.WriteStartArray();
        foreach (var diagnosis in treatment.DiagnosisPaths)
        {
            writer.WriteStartObject();
            writer.WriteString("diagnosisPathId", diagnosis.DiagnosisPathId);
            writer.WriteString("visibility", diagnosis.Visibility);
            WriteJsonArray(writer, "requirements", diagnosis.Requirements);
            writer.WritePropertyName("check");
            WriteCanonicalElement(writer, diagnosis.Check);
            WriteStringArray(writer, "reveals", diagnosis.Reveals);
            writer.WriteString("failurePolicy", diagnosis.FailurePolicy);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();

        writer.WritePropertyName("routes");
        writer.WriteStartArray();
        foreach (var route in treatment.Routes)
        {
            writer.WriteStartObject();
            writer.WriteString("routeId", route.RouteId);
            writer.WriteString("displayName", route.DisplayName);
            writer.WriteString("visibility", route.Visibility);
            writer.WriteString("mode", route.Mode);
            WriteJsonArray(writer, "requirements", route.Requirements);
            writer.WritePropertyName("resourcePolicy");
            WriteCanonicalElement(writer, route.ResourcePolicy);
            writer.WritePropertyName("resolution");
            WriteCanonicalElement(writer, route.Resolution);
            WriteJsonArray(writer, "outcomes", route.Outcomes);
            writer.WritePropertyName("interruption");
            if (route.Interruption.HasValue)
                WriteCanonicalElement(writer, route.Interruption.Value);
            else
                writer.WriteNullValue();
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        WriteStringArray(writer, "knownRouteIds", treatment.KnownRouteIds);
        WriteStringArray(writer, "completedRouteIds", treatment.CompletedRouteIds);
        writer.WriteEndObject();
    }

    private static void WriteRecovery(Utf8JsonWriter writer, WoundRecovery recovery)
    {
        writer.WritePropertyName("recovery");
        writer.WriteStartObject();
        writer.WriteString("mode", recovery.Mode);
        writer.WriteString("clockKind", recovery.ClockKind);
        writer.WriteNumber("cadence", recovery.Cadence);
        writer.WriteNumber("currentStepProgress", recovery.CurrentStepProgress);
        writer.WriteNumber("currentStepThreshold", recovery.CurrentStepThreshold);
        WriteNullableString(writer, "lastTickKey", recovery.LastTickKey);
        WriteStringArray(writer, "blockers", recovery.Blockers);
        writer.WriteBoolean("carryOverflow", recovery.CarryOverflow);
        writer.WritePropertyName("deteriorationPolicy");
        if (recovery.DeteriorationPolicy.HasValue)
            WriteCanonicalElement(writer, recovery.DeteriorationPolicy.Value);
        else
            writer.WriteNullValue();
        writer.WriteEndObject();
    }

    private static void WriteRelations(Utf8JsonWriter writer, WoundRelations relations)
    {
        writer.WritePropertyName("relations");
        writer.WriteStartObject();
        WriteNullableString(writer, "priorWoundId", relations.PriorWoundId);
        WriteStringArray(writer, "legacyRefs", relations.LegacyRefs);
        WriteStringArray(writer, "independentEffectRefs", relations.IndependentEffectRefs);
        writer.WriteEndObject();
    }

    private static void WriteLastTransition(Utf8JsonWriter writer, WoundLastTransition transition)
    {
        writer.WritePropertyName("lastTransition");
        writer.WriteStartObject();
        writer.WriteString("transitionId", transition.TransitionId);
        writer.WriteNumber("ordinal", transition.Ordinal);
        writer.WriteNumber("turn", transition.Turn);
        writer.WriteString("kind", transition.Kind);
        writer.WriteEndObject();
    }

    private static void WriteStringArray(
        Utf8JsonWriter writer,
        string propertyName,
        IReadOnlyList<string> values)
    {
        writer.WritePropertyName(propertyName);
        writer.WriteStartArray();
        foreach (var value in values)
            writer.WriteStringValue(value);
        writer.WriteEndArray();
    }

    private static void WriteJsonArray(
        Utf8JsonWriter writer,
        string propertyName,
        IReadOnlyList<JsonElement> values)
    {
        writer.WritePropertyName(propertyName);
        writer.WriteStartArray();
        foreach (var value in values)
            WriteCanonicalElement(writer, value);
        writer.WriteEndArray();
    }

    private static void WriteNullableString(Utf8JsonWriter writer, string propertyName, string? value)
    {
        if (value is null)
            writer.WriteNull(propertyName);
        else
            writer.WriteString(propertyName, value);
    }

    private static void WriteOptionalString(Utf8JsonWriter writer, string propertyName, string? value)
    {
        if (value is not null)
            writer.WriteString(propertyName, value);
    }

    private static void WriteCanonicalElement(Utf8JsonWriter writer, JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in value.EnumerateObject().OrderBy(
                             static property => property.Name,
                             StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonicalElement(writer, property.Value);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in value.EnumerateArray())
                    WriteCanonicalElement(writer, item);
                writer.WriteEndArray();
                break;
            case JsonValueKind.String:
                writer.WriteStringValue(value.GetString());
                break;
            case JsonValueKind.Number:
                writer.WriteRawValue(value.GetRawText(), skipInputValidation: true);
                break;
            case JsonValueKind.True:
                writer.WriteBooleanValue(true);
                break;
            case JsonValueKind.False:
                writer.WriteBooleanValue(false);
                break;
            case JsonValueKind.Null:
                writer.WriteNullValue();
                break;
            default:
                throw new InvalidOperationException("Only detached valid JSON values can be serialized canonically.");
        }
    }

    private sealed record OwnedComponentNode(
        string Profile,
        string Path,
        JsonElement Element);

    private sealed record OwnedApplyEdge(
        string TargetDefinitionKey,
        string Path,
        int? MaximumExpansion,
        JsonElement Parameters);

    private sealed record OwnedDefinitionNode(
        string DefinitionKey,
        string StackKey,
        string StackPolicy,
        int? MaxStacks,
        string Path,
        JsonElement Element,
        JsonElement ParameterBounds,
        ImmutableArray<OwnedComponentNode> Components,
        ImmutableArray<OwnedApplyEdge> ApplyEdges);

    private sealed record OwnedRootNode(
        WoundRootEffectBinding Binding,
        string Path);

    private sealed record OwnedGraphEdge(
        OwnedDefinitionNode Source,
        OwnedApplyEdge Edge);

    private sealed record OwnedEntryNode(
        WoundConsequenceEntry Entry,
        string Path);
}
