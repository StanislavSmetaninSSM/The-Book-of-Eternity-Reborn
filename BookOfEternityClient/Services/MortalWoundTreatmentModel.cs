using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BookOfEternityClient.Services;

internal sealed record MortalWoundTreatmentParseResult(
    bool IsValid,
    ImmutableArray<ValidationIssue> Issues,
    MortalWoundTreatmentDefinition? Treatment);

internal sealed record MortalWoundTreatmentDefinition(
    ImmutableArray<MortalWoundDiagnosisPathDefinition> DiagnosisPaths,
    ImmutableArray<MortalWoundTreatmentRouteDefinition> Routes,
    ImmutableArray<string> KnownRouteIds,
    ImmutableArray<string> CompletedRouteIds);

internal sealed record MortalWoundDiagnosisPathDefinition(
    string DiagnosisPathId,
    string DisplayName,
    string Visibility,
    ImmutableArray<MortalWoundKnownFact> RequiresKnownFacts,
    ImmutableArray<MortalWoundTreatmentRequirement> Requirements,
    ImmutableArray<MortalWoundKnownFact> Reveals,
    string FailurePolicy,
    string SourcePath);

internal abstract record MortalWoundKnownFact(string Kind, string Identifier)
{
    internal string CanonicalValue => Kind + ":" + Identifier;
}

internal sealed record MortalWoundRouteKnownFact(string RouteId) :
    MortalWoundKnownFact("route", RouteId);

internal sealed record MortalWoundComplicationKnownFact(string ComplicationId) :
    MortalWoundKnownFact("complication", ComplicationId);

internal abstract record MortalWoundTreatmentRouteDefinition(
    string RouteId,
    string DisplayName,
    string Visibility,
    string Mode,
    ImmutableArray<MortalWoundTreatmentRequirement> Requirements,
    MortalWoundTreatmentResourcePolicy ResourcePolicy,
    string SourcePath);

internal sealed record MortalWoundProcedureRouteDefinition(
    string RouteId,
    string DisplayName,
    string Visibility,
    ImmutableArray<MortalWoundTreatmentRequirement> Requirements,
    MortalWoundTreatmentResourcePolicy ResourcePolicy,
    MortalWoundProcedureResolution Resolution,
    ImmutableArray<MortalWoundProcedureBand> Bands,
    string SourcePath) : MortalWoundTreatmentRouteDefinition(
        RouteId,
        DisplayName,
        Visibility,
        "procedure",
        Requirements,
        ResourcePolicy,
        SourcePath);

internal sealed record MortalWoundCourseRouteDefinition(
    string RouteId,
    string DisplayName,
    string Visibility,
    ImmutableArray<MortalWoundTreatmentRequirement> Requirements,
    MortalWoundTreatmentResourcePolicy ResourcePolicy,
    MortalWoundCourseResolution Resolution,
    ImmutableArray<MortalWoundCourseMilestone> Milestones,
    MortalWoundCourseInterruption Interruption,
    string SourcePath) : MortalWoundTreatmentRouteDefinition(
        RouteId,
        DisplayName,
        Visibility,
        "course",
        Requirements,
        ResourcePolicy,
        SourcePath);

internal sealed record MortalWoundGuaranteedRouteDefinition(
    string RouteId,
    string DisplayName,
    string Visibility,
    ImmutableArray<MortalWoundTreatmentRequirement> Requirements,
    MortalWoundTreatmentResourcePolicy ResourcePolicy,
    MortalWoundGuaranteedResolution Resolution,
    MortalWoundGuaranteedOutcome Outcome,
    string SourcePath) : MortalWoundTreatmentRouteDefinition(
        RouteId,
        DisplayName,
        Visibility,
        "guaranteed",
        Requirements,
        ResourcePolicy,
        SourcePath);

internal abstract record MortalWoundTreatmentRequirement(string Kind);

internal sealed record MortalWoundItemQuantityRequirement(
    string ItemRef,
    int Quantity,
    string OwnerRole) : MortalWoundTreatmentRequirement("item_quantity");

internal sealed record MortalWoundResourceQuantityRequirement(
    string ResourceRef,
    int Quantity,
    string OwnerRole) : MortalWoundTreatmentRequirement("resource_quantity");

internal sealed record MortalWoundSkillTierRequirement(
    string CapabilityRef,
    int MinimumTier,
    string ActorRole) : MortalWoundTreatmentRequirement("skill_tier");

internal sealed record MortalWoundSourceCapabilityRequirement(
    string CapabilityRef,
    string ActorRole) : MortalWoundTreatmentRequirement("source_capability");

internal sealed record MortalWoundProviderRequirement(string ProviderRef) :
    MortalWoundTreatmentRequirement("provider");

internal sealed record MortalWoundConsentRequirement(
    string ConsentRef,
    string ProviderRef,
    string TargetRef) : MortalWoundTreatmentRequirement("consent");

internal sealed record MortalWoundFacilityRequirement(string FacilityRef) :
    MortalWoundTreatmentRequirement("facility");

internal sealed record MortalWoundLocationRequirement(
    string LocationRef,
    string TargetRole) : MortalWoundTreatmentRequirement("location");

internal sealed record MortalWoundQuestStateRequirement(
    string QuestRef,
    string RequiredState) : MortalWoundTreatmentRequirement("quest_state");

internal sealed record MortalWoundEffectStateRequirement(
    string EffectRef,
    string RequiredState,
    string TargetRole) : MortalWoundTreatmentRequirement("effect_state");

internal sealed record MortalWoundEnvironmentRequirement(
    string EnvironmentRef,
    string RequiredState) : MortalWoundTreatmentRequirement("environment");

internal sealed record MortalWoundTreatmentResourcePolicy(
    bool ReserveBeforeResolution,
    ImmutableArray<string> ConsumeOn,
    ImmutableArray<string> RefundOn,
    ImmutableArray<MortalWoundTreatmentResourceMutation> Mutations);

internal sealed record MortalWoundTreatmentResourceMutation(
    string Kind,
    string Scope,
    int? MilestoneOrdinal,
    int RequirementIndex);

internal sealed record MortalWoundProcedureResolution(
    string FormulaKey,
    int Difficulty,
    string RollSource,
    string CriticalPolicy,
    MortalWoundProcedureModifierSource ModifierSource);

internal abstract record MortalWoundProcedureModifierSource(string Kind);

internal sealed record MortalWoundFixedZeroModifierSource() :
    MortalWoundProcedureModifierSource("fixed_zero");

internal sealed record MortalWoundResolvedSkillTierModifierSource(int RequirementIndex) :
    MortalWoundProcedureModifierSource("resolved_skill_tier");

internal sealed record MortalWoundProcedureBand(
    string BandId,
    long? MinimumMargin,
    long? MaximumMargin,
    string Category,
    ImmutableArray<MortalWoundTreatmentOperation> DeclaredResult);

internal sealed record MortalWoundCourseResolution(
    string ClockKind,
    long MaximumGapMinutes);

internal sealed record MortalWoundCourseMilestone(
    int Ordinal,
    long AfterMinutes,
    ImmutableArray<MortalWoundTreatmentRequirement> Requirements,
    string Category,
    string Completion,
    ImmutableArray<MortalWoundTreatmentOperation> DeclaredResult);

internal sealed record MortalWoundCourseInterruption(
    string Category,
    ImmutableArray<MortalWoundTreatmentOperation> DeclaredResult);

internal sealed record MortalWoundGuaranteedResolution(
    string CapabilityRef,
    string ActorRole);

internal sealed record MortalWoundGuaranteedOutcome(
    string Category,
    ImmutableArray<MortalWoundTreatmentOperation> DeclaredResult);

[JsonPolymorphic]
[JsonDerivedType(typeof(MortalWoundNoImprovementOperation))]
[JsonDerivedType(typeof(MortalWoundStabilizeOperation))]
[JsonDerivedType(typeof(MortalWoundAddRecoveryOperation))]
[JsonDerivedType(typeof(MortalWoundReduceSeverityOperation))]
[JsonDerivedType(typeof(MortalWoundRemoveComplicationOperation))]
[JsonDerivedType(typeof(MortalWoundAddComplicationOperation))]
[JsonDerivedType(typeof(MortalWoundApplyDeteriorationOperation))]
[JsonDerivedType(typeof(MortalWoundHealOperation))]
internal abstract record MortalWoundTreatmentOperation(string Kind);

internal sealed record MortalWoundNoImprovementOperation() :
    MortalWoundTreatmentOperation("no_improvement");

internal sealed record MortalWoundStabilizeOperation() :
    MortalWoundTreatmentOperation("stabilize");

internal sealed record MortalWoundAddRecoveryOperation(int Points) :
    MortalWoundTreatmentOperation("add_recovery");

internal sealed record MortalWoundReduceSeverityOperation(int Steps) :
    MortalWoundTreatmentOperation("reduce_severity");

internal sealed record MortalWoundRemoveComplicationOperation(string ComplicationId) :
    MortalWoundTreatmentOperation("remove_complication");

internal sealed record MortalWoundAddComplicationOperation(
    MortalWoundComplicationProposalDraft ComplicationDraft) :
    MortalWoundTreatmentOperation("add_complication");

internal sealed record MortalWoundApplyDeteriorationOperation(string PolicyRef) :
    MortalWoundTreatmentOperation("apply_deterioration");

internal sealed record MortalWoundHealOperation(
    ImmutableArray<MortalWoundHealLegacyDraft> Legacies) :
    MortalWoundTreatmentOperation("heal");

internal sealed record MortalWoundComplicationProposalDraft(
    WoundComplicationProposalDraft Complication,
    ImmutableArray<WoundConsequenceDefinitionProposalDraft> ConsequenceDefinitions);

internal sealed record WoundComplicationProposalDraft(
    string ComplicationRef,
    string Kind,
    string State,
    string DisplayName,
    int TreatmentDifficultyModifier,
    string Visibility);

internal sealed record WoundConsequenceDefinitionProposalDraft(
    string DefinitionRef,
    JsonElement Definition,
    WoundConsequenceRootProposalDraft? Root);

internal sealed record WoundConsequenceRootProposalDraft(
    string OwnershipKind,
    string ComplicationRef,
    ImmutableArray<WoundConsequenceSlotProposalDraft> Slots);

internal sealed record WoundConsequenceSlotProposalDraft(
    string ProfileKey,
    string ReadableSummary);

internal abstract record MortalWoundHealLegacyDraft(
    string LocalLegacyRef,
    string Kind,
    string ReadableSummary);

internal sealed record MortalWoundCosmeticLegacyDraft(
    string LocalLegacyRef,
    string ReadableSummary) : MortalWoundHealLegacyDraft(
        LocalLegacyRef,
        "cosmetic",
        ReadableSummary);

internal sealed record MortalWoundMechanicalEffectLegacyDraft(
    string LocalLegacyRef,
    string ReadableSummary,
    MortalWoundMechanicalEffectDraft EffectDraft) : MortalWoundHealLegacyDraft(
        LocalLegacyRef,
        "mechanical_effect",
        ReadableSummary);

internal sealed record MortalWoundMechanicalEffectDraft(
    int SchemaVersion,
    ImmutableArray<MortalWoundMechanicalDefinitionDraft> Definitions,
    ImmutableArray<MortalWoundMechanicalApplicationDraft> Applications);

internal sealed record MortalWoundMechanicalDefinitionDraft(
    string DefinitionRef,
    JsonElement Definition);

internal sealed record MortalWoundMechanicalApplicationDraft(
    string ApplicationRef,
    string DefinitionRef,
    JsonElement Parameters);

internal static partial class MortalWoundTreatmentContract
{
    internal static MortalWoundTreatmentParseResult ParseProjection(
        WoundTreatment treatment,
        string treatmentPath,
        string realm,
        string ownerTargetKind,
        int severityRank,
        IReadOnlyList<WoundComplication> complications,
        JsonElement? deteriorationPolicy)
    {
        ArgumentNullException.ThrowIfNull(treatment);
        ArgumentException.ThrowIfNullOrWhiteSpace(treatmentPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(realm);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerTargetKind);
        ArgumentNullException.ThrowIfNull(complications);

        var issues = new List<ValidationIssue>();
        ValidateRoutes(
            treatment.Routes,
            treatmentPath,
            realm,
            ownerTargetKind,
            severityRank,
            complications,
            deteriorationPolicy,
            issues);
        ValidateDiagnosis(treatment, treatmentPath, complications, issues);
        if (issues.Count != 0)
        {
            return new MortalWoundTreatmentParseResult(
                false,
                issues.ToImmutableArray(),
                null);
        }

        try
        {
            return new MortalWoundTreatmentParseResult(
                true,
                ImmutableArray<ValidationIssue>.Empty,
                BuildTreatmentDefinition(treatment));
        }
        catch (Exception exception) when (exception is JsonException or
                                          InvalidOperationException or
                                          FormatException or
                                          OverflowException)
        {
            AddInvalid(
                issues,
                treatmentPath,
                "one complete typed Mortal wound treatment projection",
                exception.GetType().Name);
            return new MortalWoundTreatmentParseResult(
                false,
                issues.ToImmutableArray(),
                null);
        }
    }

    private static MortalWoundTreatmentDefinition BuildTreatmentDefinition(
        WoundTreatment treatment) => new(
        treatment.DiagnosisPaths.Select(BuildDiagnosisPath).ToImmutableArray(),
        treatment.Routes.Select(BuildRoute).ToImmutableArray(),
        treatment.KnownRouteIds.ToImmutableArray(),
        treatment.CompletedRouteIds.ToImmutableArray());

    private static MortalWoundDiagnosisPathDefinition BuildDiagnosisPath(
        WoundDiagnosisPath path) => new(
        path.DiagnosisPathId,
        path.DisplayName,
        path.Visibility,
        path.RequiresKnownFacts.Select(BuildKnownFact).ToImmutableArray(),
        BuildRequirements(path.Requirements),
        path.Reveals.Select(BuildKnownFact).ToImmutableArray(),
        path.FailurePolicy,
        path.SourcePath);

    private static MortalWoundKnownFact BuildKnownFact(string value)
    {
        if (value.StartsWith("route:", StringComparison.Ordinal))
            return new MortalWoundRouteKnownFact(value["route:".Length..]);
        if (value.StartsWith("complication:", StringComparison.Ordinal))
            return new MortalWoundComplicationKnownFact(value["complication:".Length..]);
        throw new InvalidOperationException("Validated known fact has an unknown prefix.");
    }

    private static MortalWoundTreatmentRouteDefinition BuildRoute(WoundTreatmentRoute route)
    {
        var requirements = BuildRequirements(route.Requirements);
        var policy = BuildResourcePolicy(route.ResourcePolicy);
        return route.Mode switch
        {
            "procedure" => new MortalWoundProcedureRouteDefinition(
                route.RouteId,
                route.DisplayName,
                route.Visibility,
                requirements,
                policy,
                BuildProcedureResolution(route.Resolution),
                route.Outcomes.Select(BuildProcedureBand).ToImmutableArray(),
                route.SourcePath),
            "course" => new MortalWoundCourseRouteDefinition(
                route.RouteId,
                route.DisplayName,
                route.Visibility,
                requirements,
                policy,
                BuildCourseResolution(route.Resolution),
                route.Outcomes.Select(BuildCourseMilestone).ToImmutableArray(),
                BuildCourseInterruption(route.Interruption!.Value),
                route.SourcePath),
            "guaranteed" => new MortalWoundGuaranteedRouteDefinition(
                route.RouteId,
                route.DisplayName,
                route.Visibility,
                requirements,
                policy,
                BuildGuaranteedResolution(route.Resolution),
                BuildGuaranteedOutcome(route.Outcomes[0]),
                route.SourcePath),
            _ => throw new InvalidOperationException("Validated route has an unknown mode.")
        };
    }

    private static ImmutableArray<MortalWoundTreatmentRequirement> BuildRequirements(
        IReadOnlyList<JsonElement> requirements) => requirements
        .Select(BuildRequirement)
        .ToImmutableArray();

    private static MortalWoundTreatmentRequirement BuildRequirement(JsonElement value) =>
        value.GetProperty("kind").GetString() switch
        {
            "item_quantity" => new MortalWoundItemQuantityRequirement(
                Text(value, "itemRef"),
                value.GetProperty("quantity").GetInt32(),
                Text(value, "ownerRole")),
            "resource_quantity" => new MortalWoundResourceQuantityRequirement(
                Text(value, "resourceRef"),
                value.GetProperty("quantity").GetInt32(),
                Text(value, "ownerRole")),
            "skill_tier" => new MortalWoundSkillTierRequirement(
                Text(value, "capabilityRef"),
                value.GetProperty("minimumTier").GetInt32(),
                Text(value, "actorRole")),
            "source_capability" => new MortalWoundSourceCapabilityRequirement(
                Text(value, "capabilityRef"),
                Text(value, "actorRole")),
            "provider" => new MortalWoundProviderRequirement(Text(value, "providerRef")),
            "consent" => new MortalWoundConsentRequirement(
                Text(value, "consentRef"),
                Text(value, "providerRef"),
                Text(value, "targetRef")),
            "facility" => new MortalWoundFacilityRequirement(Text(value, "facilityRef")),
            "location" => new MortalWoundLocationRequirement(
                Text(value, "locationRef"),
                Text(value, "targetRole")),
            "quest_state" => new MortalWoundQuestStateRequirement(
                Text(value, "questRef"),
                Text(value, "requiredState")),
            "effect_state" => new MortalWoundEffectStateRequirement(
                Text(value, "effectRef"),
                Text(value, "requiredState"),
                Text(value, "targetRole")),
            "environment" => new MortalWoundEnvironmentRequirement(
                Text(value, "environmentRef"),
                Text(value, "requiredState")),
            _ => throw new InvalidOperationException("Validated requirement has an unknown kind.")
        };

    private static MortalWoundTreatmentResourcePolicy BuildResourcePolicy(JsonElement value) =>
        new(
            value.GetProperty("reserveBeforeResolution").GetBoolean(),
            Strings(value.GetProperty("consumeOn")),
            Strings(value.GetProperty("refundOn")),
            value.GetProperty("mutations")
                .EnumerateArray()
                .Select(mutation => new MortalWoundTreatmentResourceMutation(
                    Text(mutation, "kind"),
                    Text(mutation, "scope"),
                    NullableInt32(mutation.GetProperty("milestoneOrdinal")),
                    mutation.GetProperty("requirementIndex").GetInt32()))
                .ToImmutableArray());

    private static MortalWoundProcedureResolution BuildProcedureResolution(JsonElement value)
    {
        var modifier = value.GetProperty("modifierSource");
        MortalWoundProcedureModifierSource typedModifier = Text(modifier, "kind") switch
        {
            "fixed_zero" => new MortalWoundFixedZeroModifierSource(),
            "resolved_skill_tier" => new MortalWoundResolvedSkillTierModifierSource(
                modifier.GetProperty("requirementIndex").GetInt32()),
            _ => throw new InvalidOperationException("Validated modifier has an unknown kind.")
        };
        return new MortalWoundProcedureResolution(
            Text(value, "formulaKey"),
            value.GetProperty("difficulty").GetInt32(),
            Text(value, "rollSource"),
            Text(value, "criticalPolicy"),
            typedModifier);
    }

    private static MortalWoundProcedureBand BuildProcedureBand(JsonElement value) => new(
        Text(value, "bandId"),
        NullableInt64(value.GetProperty("minimumMargin")),
        NullableInt64(value.GetProperty("maximumMargin")),
        Text(value, "category"),
        BuildOperations(value.GetProperty("result")));

    private static MortalWoundCourseResolution BuildCourseResolution(JsonElement value) => new(
        Text(value, "clockKind"),
        value.GetProperty("maximumGapMinutes").GetInt64());

    private static MortalWoundCourseMilestone BuildCourseMilestone(JsonElement value) => new(
        value.GetProperty("ordinal").GetInt32(),
        value.GetProperty("afterMinutes").GetInt64(),
        BuildRequirements(value.GetProperty("requirements").EnumerateArray().ToArray()),
        Text(value, "category"),
        Text(value, "completion"),
        BuildOperations(value.GetProperty("result")));

    private static MortalWoundCourseInterruption BuildCourseInterruption(JsonElement value) =>
        new(Text(value, "category"), BuildOperations(value.GetProperty("result")));

    private static MortalWoundGuaranteedResolution BuildGuaranteedResolution(JsonElement value) =>
        new(Text(value, "capabilityRef"), Text(value, "actorRole"));

    private static MortalWoundGuaranteedOutcome BuildGuaranteedOutcome(JsonElement value) =>
        new(Text(value, "category"), BuildOperations(value.GetProperty("result")));

    private static ImmutableArray<MortalWoundTreatmentOperation> BuildOperations(
        JsonElement values) => values.EnumerateArray().Select(BuildOperation).ToImmutableArray();

    private static MortalWoundTreatmentOperation BuildOperation(JsonElement value) =>
        Text(value, "kind") switch
        {
            "no_improvement" => new MortalWoundNoImprovementOperation(),
            "stabilize" => new MortalWoundStabilizeOperation(),
            "add_recovery" => new MortalWoundAddRecoveryOperation(
                value.GetProperty("points").GetInt32()),
            "reduce_severity" => new MortalWoundReduceSeverityOperation(
                value.GetProperty("steps").GetInt32()),
            "remove_complication" => new MortalWoundRemoveComplicationOperation(
                Text(value, "complicationId")),
            "add_complication" => new MortalWoundAddComplicationOperation(
                BuildComplicationDraft(value.GetProperty("complicationDraft"))),
            "apply_deterioration" => new MortalWoundApplyDeteriorationOperation(
                Text(value, "policyRef")),
            "heal" => new MortalWoundHealOperation(
                value.GetProperty("legacies")
                    .EnumerateArray()
                    .Select(BuildLegacy)
                    .ToImmutableArray()),
            _ => throw new InvalidOperationException("Validated operation has an unknown kind.")
        };

    private static MortalWoundComplicationProposalDraft BuildComplicationDraft(JsonElement draft)
    {
        var complication = draft.GetProperty("complications")[0];
        return new MortalWoundComplicationProposalDraft(
            new WoundComplicationProposalDraft(
                Text(complication, "complicationRef"),
                Text(complication, "kind"),
                Text(complication, "state"),
                Text(complication, "displayName"),
                complication.GetProperty("treatmentDifficultyModifier").GetInt32(),
                Text(complication, "visibility")),
            draft.GetProperty("consequenceDefinitions")
                .EnumerateArray()
                .Select(BuildConsequenceDefinition)
                .ToImmutableArray());
    }

    private static WoundConsequenceDefinitionProposalDraft BuildConsequenceDefinition(
        JsonElement wrapper)
    {
        var root = wrapper.GetProperty("root");
        return new WoundConsequenceDefinitionProposalDraft(
            Text(wrapper, "definitionRef"),
            wrapper.GetProperty("definition").Clone(),
            root.ValueKind == JsonValueKind.Null ? null : BuildConsequenceRoot(root));
    }

    private static WoundConsequenceRootProposalDraft BuildConsequenceRoot(JsonElement root)
    {
        var ownership = root.GetProperty("ownership");
        return new WoundConsequenceRootProposalDraft(
            Text(ownership, "kind"),
            Text(ownership, "complicationRef"),
            root.GetProperty("slots")
                .EnumerateArray()
                .Select(slot => new WoundConsequenceSlotProposalDraft(
                    Text(slot, "profileKey"),
                    Text(slot, "readableSummary")))
                .ToImmutableArray());
    }

    private static MortalWoundHealLegacyDraft BuildLegacy(JsonElement legacy) =>
        Text(legacy, "kind") switch
        {
            "cosmetic" => new MortalWoundCosmeticLegacyDraft(
                Text(legacy, "localLegacyRef"),
                Text(legacy, "readableSummary")),
            "mechanical_effect" => new MortalWoundMechanicalEffectLegacyDraft(
                Text(legacy, "localLegacyRef"),
                Text(legacy, "readableSummary"),
                BuildMechanicalDraft(legacy.GetProperty("effectDraft"))),
            _ => throw new InvalidOperationException("Validated legacy has an unknown kind.")
        };

    private static MortalWoundMechanicalEffectDraft BuildMechanicalDraft(JsonElement draft) => new(
        draft.GetProperty("schemaVersion").GetInt32(),
        draft.GetProperty("definitions")
            .EnumerateArray()
            .Select(definition => new MortalWoundMechanicalDefinitionDraft(
                Text(definition, "definitionRef"),
                definition.GetProperty("definition").Clone()))
            .ToImmutableArray(),
        draft.GetProperty("applications")
            .EnumerateArray()
            .Select(application => new MortalWoundMechanicalApplicationDraft(
                Text(application, "applicationRef"),
                Text(application, "definitionRef"),
                application.GetProperty("parameters").Clone()))
            .ToImmutableArray());

    private static string Text(JsonElement parent, string field) =>
        parent.GetProperty(field).GetString() ??
        throw new InvalidOperationException("Validated string field became null.");

    private static ImmutableArray<string> Strings(JsonElement array) => array
        .EnumerateArray()
        .Select(value => value.GetString() ??
            throw new InvalidOperationException("Validated string array contains null."))
        .ToImmutableArray();

    private static int? NullableInt32(JsonElement value) =>
        value.ValueKind == JsonValueKind.Null ? null : value.GetInt32();

    private static long? NullableInt64(JsonElement value) =>
        value.ValueKind == JsonValueKind.Null ? null : value.GetInt64();

    internal static void WriteCanonical(
        Utf8JsonWriter writer,
        MortalWoundTreatmentDefinition treatment)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(treatment);

        writer.WritePropertyName("treatment");
        writer.WriteStartObject();
        writer.WritePropertyName("diagnosisPaths");
        writer.WriteStartArray();
        foreach (var diagnosis in treatment.DiagnosisPaths)
            WriteDiagnosisPathCanonical(writer, diagnosis);
        writer.WriteEndArray();

        writer.WritePropertyName("routes");
        writer.WriteStartArray();
        foreach (var route in treatment.Routes)
            WriteRouteCanonical(writer, route);
        writer.WriteEndArray();
        WriteStrings(writer, "knownRouteIds", treatment.KnownRouteIds);
        WriteStrings(writer, "completedRouteIds", treatment.CompletedRouteIds);
        writer.WriteEndObject();
    }

    internal static void WriteDiagnosisPathCanonical(
        Utf8JsonWriter writer,
        MortalWoundDiagnosisPathDefinition diagnosisPath)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(diagnosisPath);
        writer.WriteStartObject();
        writer.WriteString("diagnosisPathId", diagnosisPath.DiagnosisPathId);
        writer.WriteString("displayName", diagnosisPath.DisplayName);
        writer.WriteString("visibility", diagnosisPath.Visibility);
        WriteFacts(writer, "requiresKnownFacts", diagnosisPath.RequiresKnownFacts);
        WriteRequirements(writer, diagnosisPath.Requirements);
        writer.WritePropertyName("check");
        writer.WriteStartObject();
        writer.WriteEndObject();
        WriteFacts(writer, "reveals", diagnosisPath.Reveals);
        writer.WriteString("failurePolicy", diagnosisPath.FailurePolicy);
        writer.WriteEndObject();
    }

    internal static void WriteRouteCanonical(
        Utf8JsonWriter writer,
        MortalWoundTreatmentRouteDefinition route)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(route);
        WriteRouteHeader(
            writer,
            route.RouteId,
            route.DisplayName,
            route.Visibility,
            route.Mode,
            route.Requirements,
            route.ResourcePolicy);
        switch (route)
        {
            case MortalWoundProcedureRouteDefinition procedure:
                WriteProcedure(writer, procedure);
                break;
            case MortalWoundCourseRouteDefinition course:
                WriteCourse(writer, course);
                break;
            case MortalWoundGuaranteedRouteDefinition guaranteed:
                WriteGuaranteed(writer, guaranteed);
                break;
            default:
                throw new InvalidOperationException("Unknown typed Mortal treatment route.");
        }
        writer.WriteEndObject();
    }

    private static void WriteRequirements(
        Utf8JsonWriter writer,
        ImmutableArray<MortalWoundTreatmentRequirement> requirements)
    {
        writer.WritePropertyName("requirements");
        writer.WriteStartArray();
        foreach (var requirement in requirements)
        {
            writer.WriteStartObject();
            writer.WriteString("kind", requirement.Kind);
            switch (requirement)
            {
                case MortalWoundItemQuantityRequirement item:
                    writer.WriteString("itemRef", item.ItemRef);
                    writer.WriteNumber("quantity", item.Quantity);
                    writer.WriteString("ownerRole", item.OwnerRole);
                    break;
                case MortalWoundResourceQuantityRequirement resource:
                    writer.WriteString("resourceRef", resource.ResourceRef);
                    writer.WriteNumber("quantity", resource.Quantity);
                    writer.WriteString("ownerRole", resource.OwnerRole);
                    break;
                case MortalWoundSkillTierRequirement skill:
                    writer.WriteString("capabilityRef", skill.CapabilityRef);
                    writer.WriteNumber("minimumTier", skill.MinimumTier);
                    writer.WriteString("actorRole", skill.ActorRole);
                    break;
                case MortalWoundSourceCapabilityRequirement source:
                    writer.WriteString("capabilityRef", source.CapabilityRef);
                    writer.WriteString("actorRole", source.ActorRole);
                    break;
                case MortalWoundProviderRequirement provider:
                    writer.WriteString("providerRef", provider.ProviderRef);
                    break;
                case MortalWoundConsentRequirement consent:
                    writer.WriteString("consentRef", consent.ConsentRef);
                    writer.WriteString("providerRef", consent.ProviderRef);
                    writer.WriteString("targetRef", consent.TargetRef);
                    break;
                case MortalWoundFacilityRequirement facility:
                    writer.WriteString("facilityRef", facility.FacilityRef);
                    break;
                case MortalWoundLocationRequirement location:
                    writer.WriteString("locationRef", location.LocationRef);
                    writer.WriteString("targetRole", location.TargetRole);
                    break;
                case MortalWoundQuestStateRequirement quest:
                    writer.WriteString("questRef", quest.QuestRef);
                    writer.WriteString("requiredState", quest.RequiredState);
                    break;
                case MortalWoundEffectStateRequirement effect:
                    writer.WriteString("effectRef", effect.EffectRef);
                    writer.WriteString("requiredState", effect.RequiredState);
                    writer.WriteString("targetRole", effect.TargetRole);
                    break;
                case MortalWoundEnvironmentRequirement environment:
                    writer.WriteString("environmentRef", environment.EnvironmentRef);
                    writer.WriteString("requiredState", environment.RequiredState);
                    break;
                default:
                    throw new InvalidOperationException("Unknown typed treatment requirement.");
            }
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
    }

    private static void WriteResourcePolicy(
        Utf8JsonWriter writer,
        MortalWoundTreatmentResourcePolicy policy)
    {
        writer.WritePropertyName("resourcePolicy");
        writer.WriteStartObject();
        writer.WriteBoolean("reserveBeforeResolution", policy.ReserveBeforeResolution);
        WriteStrings(writer, "consumeOn", policy.ConsumeOn);
        WriteStrings(writer, "refundOn", policy.RefundOn);
        writer.WritePropertyName("mutations");
        writer.WriteStartArray();
        foreach (var mutation in policy.Mutations)
        {
            writer.WriteStartObject();
            writer.WriteString("kind", mutation.Kind);
            writer.WriteString("scope", mutation.Scope);
            if (mutation.MilestoneOrdinal is { } ordinal)
                writer.WriteNumber("milestoneOrdinal", ordinal);
            else
                writer.WriteNull("milestoneOrdinal");
            writer.WriteNumber("requirementIndex", mutation.RequirementIndex);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void WriteProcedure(
        Utf8JsonWriter writer,
        MortalWoundProcedureRouteDefinition route)
    {
        WriteProcedureResolutionPayload(writer, route.Resolution);

        writer.WritePropertyName("outcomes");
        writer.WriteStartArray();
        foreach (var band in route.Bands)
        {
            WriteProcedureBandPayload(
                writer,
                band.BandId,
                band.MinimumMargin,
                band.MaximumMargin,
                band.Category,
                output => WriteOperations(output, band.DeclaredResult));
        }
        writer.WriteEndArray();
        writer.WriteNull("interruption");
    }

    private static void WriteCourse(
        Utf8JsonWriter writer,
        MortalWoundCourseRouteDefinition route)
    {
        WriteCourseResolutionPayload(writer, route.Resolution);

        writer.WritePropertyName("outcomes");
        writer.WriteStartArray();
        foreach (var milestone in route.Milestones)
        {
            WriteCourseMilestonePayload(
                writer,
                milestone.Ordinal,
                milestone.AfterMinutes,
                milestone.Requirements,
                milestone.Category,
                milestone.Completion,
                output => WriteOperations(output, milestone.DeclaredResult));
        }
        writer.WriteEndArray();
        writer.WritePropertyName("interruption");
        WriteCategoryResultPayload(
            writer,
            route.Interruption.Category,
            output => WriteOperations(output, route.Interruption.DeclaredResult));
    }

    private static void WriteGuaranteed(
        Utf8JsonWriter writer,
        MortalWoundGuaranteedRouteDefinition route)
    {
        WriteGuaranteedResolutionPayload(writer, route.Resolution);
        writer.WritePropertyName("outcomes");
        writer.WriteStartArray();
        WriteCategoryResultPayload(
            writer,
            route.Outcome.Category,
            output => WriteOperations(output, route.Outcome.DeclaredResult));
        writer.WriteEndArray();
        writer.WriteNull("interruption");
    }

    private static void WriteOperations(
        Utf8JsonWriter writer,
        ImmutableArray<MortalWoundTreatmentOperation> operations)
    {
        WriteResultArray(writer, operations, WriteCanonicalOperation);
    }

    private static void WriteCanonicalOperation(
        Utf8JsonWriter writer,
        MortalWoundTreatmentOperation operation)
    {
        writer.WriteStartObject();
        writer.WriteString("kind", operation.Kind);
        switch (operation)
        {
            case MortalWoundNoImprovementOperation:
            case MortalWoundStabilizeOperation:
                break;
            case MortalWoundAddRecoveryOperation recovery:
                writer.WriteNumber("points", recovery.Points);
                break;
            case MortalWoundReduceSeverityOperation severity:
                writer.WriteNumber("steps", severity.Steps);
                break;
            case MortalWoundRemoveComplicationOperation remove:
                writer.WriteString("complicationId", remove.ComplicationId);
                break;
            case MortalWoundAddComplicationOperation add:
                WriteComplicationDraft(writer, add.ComplicationDraft);
                break;
            case MortalWoundApplyDeteriorationOperation deterioration:
                writer.WriteString("policyRef", deterioration.PolicyRef);
                break;
            case MortalWoundHealOperation heal:
                WriteLegacies(writer, heal.Legacies);
                break;
            default:
                throw new InvalidOperationException("Unknown typed treatment operation.");
        }
        writer.WriteEndObject();
    }

    private static void WriteComplicationDraft(
        Utf8JsonWriter writer,
        MortalWoundComplicationProposalDraft draft)
    {
        writer.WritePropertyName("complicationDraft");
        writer.WriteStartObject();
        writer.WritePropertyName("complications");
        writer.WriteStartArray();
        writer.WriteStartObject();
        writer.WriteString("complicationRef", draft.Complication.ComplicationRef);
        writer.WriteString("kind", draft.Complication.Kind);
        writer.WriteString("state", draft.Complication.State);
        writer.WriteString("displayName", draft.Complication.DisplayName);
        writer.WriteNumber(
            "treatmentDifficultyModifier",
            draft.Complication.TreatmentDifficultyModifier);
        writer.WriteString("visibility", draft.Complication.Visibility);
        writer.WriteEndObject();
        writer.WriteEndArray();
        writer.WritePropertyName("consequenceDefinitions");
        writer.WriteStartArray();
        foreach (var definition in draft.ConsequenceDefinitions)
        {
            writer.WriteStartObject();
            writer.WriteString("definitionRef", definition.DefinitionRef);
            writer.WritePropertyName("definition");
            WoundMaterializationContract.WriteCanonicalElement(writer, definition.Definition);
            writer.WritePropertyName("root");
            if (definition.Root is null)
            {
                writer.WriteNullValue();
            }
            else
            {
                writer.WriteStartObject();
                writer.WritePropertyName("ownership");
                writer.WriteStartObject();
                writer.WriteString("kind", definition.Root.OwnershipKind);
                writer.WriteString("complicationRef", definition.Root.ComplicationRef);
                writer.WriteEndObject();
                writer.WritePropertyName("slots");
                writer.WriteStartArray();
                foreach (var slot in definition.Root.Slots)
                {
                    writer.WriteStartObject();
                    writer.WriteString("profileKey", slot.ProfileKey);
                    writer.WriteString("readableSummary", slot.ReadableSummary);
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();
                writer.WriteEndObject();
            }
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void WriteLegacies(
        Utf8JsonWriter writer,
        ImmutableArray<MortalWoundHealLegacyDraft> legacies)
    {
        writer.WritePropertyName("legacies");
        writer.WriteStartArray();
        foreach (var legacy in legacies)
        {
            writer.WriteStartObject();
            writer.WriteString("localLegacyRef", legacy.LocalLegacyRef);
            writer.WriteString("kind", legacy.Kind);
            writer.WriteString("readableSummary", legacy.ReadableSummary);
            if (legacy is MortalWoundMechanicalEffectLegacyDraft mechanical)
            {
                writer.WritePropertyName("effectDraft");
                writer.WriteStartObject();
                writer.WriteNumber("schemaVersion", mechanical.EffectDraft.SchemaVersion);
                writer.WritePropertyName("definitions");
                writer.WriteStartArray();
                foreach (var definition in mechanical.EffectDraft.Definitions)
                {
                    writer.WriteStartObject();
                    writer.WriteString("definitionRef", definition.DefinitionRef);
                    writer.WritePropertyName("definition");
                    WoundMaterializationContract.WriteCanonicalElement(
                        writer,
                        definition.Definition);
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();
                writer.WritePropertyName("applications");
                writer.WriteStartArray();
                foreach (var application in mechanical.EffectDraft.Applications)
                {
                    writer.WriteStartObject();
                    writer.WriteString("applicationRef", application.ApplicationRef);
                    writer.WriteString("definitionRef", application.DefinitionRef);
                    writer.WritePropertyName("parameters");
                    WoundMaterializationContract.WriteCanonicalElement(
                        writer,
                        application.Parameters);
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();
                writer.WriteEndObject();
            }
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
    }

    private static void WriteFacts(
        Utf8JsonWriter writer,
        string propertyName,
        ImmutableArray<MortalWoundKnownFact> facts)
    {
        writer.WritePropertyName(propertyName);
        writer.WriteStartArray();
        foreach (var fact in facts)
            writer.WriteStringValue(fact.CanonicalValue);
        writer.WriteEndArray();
    }

    private static void WriteStrings(
        Utf8JsonWriter writer,
        string propertyName,
        ImmutableArray<string> values)
    {
        writer.WritePropertyName(propertyName);
        writer.WriteStartArray();
        foreach (var value in values)
            writer.WriteStringValue(value);
        writer.WriteEndArray();
    }

    private static void WriteNullableInt64(
        Utf8JsonWriter writer,
        string propertyName,
        long? value)
    {
        if (value is { } number)
            writer.WriteNumber(propertyName, number);
        else
            writer.WriteNull(propertyName);
    }
}
