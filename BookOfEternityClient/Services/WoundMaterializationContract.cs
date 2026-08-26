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
    IReadOnlyList<WoundConsequenceEntry> Entries);

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
        "slotBudget", "slotsUsed", "entries");
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
            var consequences = ParseConsequences(
                ReadRequiredObject(root, "consequences", path, issues),
                path + ".consequences",
                issues);
            var treatment = ParseTreatment(
                ReadRequiredObject(root, "treatment", path, issues),
                path + ".treatment",
                issues);
            var recovery = ParseRecovery(
                ReadRequiredObject(root, "recovery", path, issues),
                path + ".recovery",
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
        List<ValidationIssue> issues)
    {
        ValidateObjectShape(value, path, ConsequencesFields, ConsequencesFields, issues);
        var slotBudget = ReadInt32(value, "slotBudget", path, 0, MaxConsequences, issues);
        var slotsUsed = ReadInt32(value, "slotsUsed", path, 0, MaxConsequences, issues);
        if (!TryReadArray(value, "entries", path, out var entries, issues))
            return new WoundConsequences(slotBudget, slotsUsed, ImmutableArray<WoundConsequenceEntry>.Empty);
        AddLimitIssue(entries, path + ".entries", MaxConsequences, "consequences", issues);

        var parsed = ImmutableArray.CreateBuilder<WoundConsequenceEntry>();
        var slots = new HashSet<int>();
        var index = 0;
        foreach (var item in entries.EnumerateArray())
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

        if (slotsUsed != entries.GetArrayLength())
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

        return new WoundConsequences(slotBudget, slotsUsed, parsed.ToImmutable());
    }

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
                    FindDuplicateProperties(item, $"{path}[{index++}]", issues);
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
        writer.WritePropertyName("entries");
        writer.WriteStartArray();
        foreach (var entry in consequences.Entries)
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
}
