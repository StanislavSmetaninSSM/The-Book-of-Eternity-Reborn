using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

/// <summary>
/// Pure version-1 structural validation for the Mortal-world portion of a wound's
/// treatment envelope. Runtime authority and current-state applicability belong to
/// the treatment planner/resolver; this class validates only what may be authored
/// and persisted.
/// </summary>
internal static partial class MortalWoundTreatmentContract
{
    private const int MaxProcedureBands = 16;
    private const int MaxCourseMilestones = WoundMaterializationContract.MaxTreatmentOutcomeMembers;
    private const int MaxResourceSelectors = 64;
    internal const int MaxOutcomeOperations = 8;
    private const int MaxHealLegacies = 8;

    private static readonly IReadOnlySet<string> MortalRouteVisibilities = Set(
        "public", "known_to_player", "hidden");
    private static readonly IReadOnlySet<string> ComplicationVisibilities = Set(
        "public", "known_to_player", "hidden", "gm_only");
    private static readonly IReadOnlySet<string> RouteModes = Set(
        "procedure", "course", "guaranteed");
    private static readonly IReadOnlySet<string> ResultCategories = Set(
        "success", "partial_success", "failed_attempt");
    private static readonly IReadOnlySet<string> ActorRoles = Set("provider", "target");
    private static readonly IReadOnlySet<string> OwnerRoles = Set("provider", "target");
    private static readonly IReadOnlySet<string> ComplicationKinds = Set(
        "bleeding", "infection", "pain", "impairment", "systemic_instability",
        "spiritual_instability", "other");

    private static readonly IReadOnlySet<string> ResourcePolicyFields = Set(
        "reserveBeforeResolution", "consumeOn", "refundOn", "mutations");
    private static readonly IReadOnlySet<string> ResourceMutationFields = Set(
        "kind", "scope", "milestoneOrdinal", "requirementIndex");
    private static readonly IReadOnlySet<string> ProcedureResolutionFields = Set(
        "formulaKey", "difficulty", "rollSource", "criticalPolicy", "modifierSource");
    private static readonly IReadOnlySet<string> ProcedureModifierFixedFields = Set("kind");
    private static readonly IReadOnlySet<string> ProcedureModifierSkillFields = Set(
        "kind", "requirementIndex");
    private static readonly IReadOnlySet<string> ProcedureOutcomeFields = Set(
        "bandId", "minimumMargin", "maximumMargin", "category", "result");
    private static readonly IReadOnlySet<string> CourseResolutionFields = Set(
        "clockKind", "maximumGapMinutes");
    private static readonly IReadOnlySet<string> CourseMilestoneFields = Set(
        "ordinal", "afterMinutes", "requirements", "category", "completion", "result");
    private static readonly IReadOnlySet<string> CourseInterruptionFields = Set(
        "category", "result");
    private static readonly IReadOnlySet<string> GuaranteedResolutionFields = Set(
        "capabilityRef", "actorRole");
    private static readonly IReadOnlySet<string> GuaranteedOutcomeFields = Set(
        "category", "result");

    internal static void ValidateRoutes(
        IReadOnlyList<WoundTreatmentRoute> routes,
        string treatmentPath,
        string realm,
        string ownerTargetKind,
        int severityRank,
        IReadOnlyList<WoundComplication> currentComplications,
        JsonElement? deteriorationPolicy,
        List<ValidationIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(routes);
        ArgumentException.ThrowIfNullOrWhiteSpace(treatmentPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(realm);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerTargetKind);
        ArgumentNullException.ThrowIfNull(currentComplications);
        ArgumentNullException.ThrowIfNull(issues);

        var context = new ValidationContext(
            realm,
            new WoundValidationContext(
                ownerTargetKind,
                severityRank,
                currentComplications,
                ReadDeteriorationPolicyRef(deteriorationPolicy)));

        if (routes.Count == 0)
        {
            AddIssue(
                issues,
                treatmentPath + ".routes",
                "wound_materialization_missing_field",
                "at least one complete Mortal treatment route",
                "empty array");
            return;
        }

        var confusableRouteIds = new HashSet<string>(StringComparer.Ordinal);
        for (var routeIndex = 0; routeIndex < routes.Count; routeIndex++)
        {
            var route = routes[routeIndex];
            var routePath = string.IsNullOrWhiteSpace(route.SourcePath)
                ? $"{treatmentPath}.routes[{routeIndex}]"
                : route.SourcePath;

            if (route.RouteId.Length > 0 &&
                !confusableRouteIds.Add(ConfusableKey(route.RouteId)))
            {
                AddInvalid(
                    issues,
                    routePath + ".routeId",
                    "exact and Unicode-confusable unique route identifier",
                    route.RouteId);
            }

            ValidateRoute(route, routePath, context, issues);
        }
    }

    private static void ValidateRoute(
        WoundTreatmentRoute route,
        string routePath,
        ValidationContext context,
        List<ValidationIssue> issues)
    {
        if (!MortalRouteVisibilities.Contains(route.Visibility))
        {
            AddIssue(
                issues,
                routePath + ".visibility",
                "wound_treatment_route_visibility_invalid",
                "public | known_to_player | hidden",
                route.Visibility);
        }

        ValidateRequirements(route.Requirements, routePath + ".requirements", issues);
        ValidateResourcePolicy(route, routePath, issues);

        switch (route.Mode)
        {
            case "procedure":
                ValidateProcedure(route, routePath, context, issues);
                break;
            case "course":
                ValidateCourse(route, routePath, context, issues);
                break;
            case "guaranteed":
                ValidateGuaranteed(route, routePath, context, issues);
                break;
            default:
                // The shared outer reader owns the closed mode diagnostic.
                break;
        }
    }

    internal static IReadOnlyList<ValidationIssue>
        ValidateDeteriorationComplicationResult(
            JsonElement result,
            string path,
            string realm,
            string ownerTargetKind,
            int severityRank)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(realm);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerTargetKind);
        var issues = new List<ValidationIssue>();
        ValidateAddComplication(
            result,
            path,
            new ValidationContext(
                realm,
                new WoundValidationContext(
                    ownerTargetKind,
                    severityRank,
                    Array.Empty<WoundComplication>(),
                    CurrentPolicyRef: null)),
            new HashSet<string>(StringComparer.Ordinal),
            ComplicationValidationUse.DeteriorationPolicy,
            issues);
        return issues.ToArray();
    }

    internal static void ValidateDiagnosis(
        WoundTreatment treatment,
        string treatmentPath,
        IReadOnlyList<WoundComplication> complications,
        List<ValidationIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(treatment);
        ArgumentException.ThrowIfNullOrWhiteSpace(treatmentPath);
        ArgumentNullException.ThrowIfNull(complications);
        ArgumentNullException.ThrowIfNull(issues);

        var routeIds = treatment.Routes
            .Select(static route => route.RouteId)
            .Where(static routeId => routeId.Length > 0)
            .ToHashSet(StringComparer.Ordinal);
        var complicationIds = complications
            .Select(static complication => complication.ComplicationId)
            .Where(static complicationId => complicationId.Length > 0)
            .ToHashSet(StringComparer.Ordinal);
        var knownRoutes = treatment.KnownRouteIds.ToHashSet(StringComparer.Ordinal);

        for (var index = 0; index < treatment.KnownRouteIds.Count; index++)
        {
            var routeId = treatment.KnownRouteIds[index];
            if (!routeIds.Contains(routeId))
            {
                AddIssue(
                    issues,
                    $"{treatmentPath}.knownRouteIds[{index}]",
                    "wound_treatment_route_state_invalid",
                    "exact routeId declared inside this wound",
                    routeId);
            }
        }
        for (var index = 0; index < treatment.CompletedRouteIds.Count; index++)
        {
            var routeId = treatment.CompletedRouteIds[index];
            if (!routeIds.Contains(routeId) || !knownRoutes.Contains(routeId))
            {
                AddIssue(
                    issues,
                    $"{treatmentPath}.completedRouteIds[{index}]",
                    "wound_treatment_route_state_invalid",
                    "exact declared routeId also present in knownRouteIds",
                    routeId);
            }
        }
        if (treatment.Routes.Any(route =>
                route.Visibility is "public" or "known_to_player" &&
                !knownRoutes.Contains(route.RouteId)))
        {
            AddIssue(
                issues,
                treatmentPath + ".knownRouteIds",
                "wound_treatment_route_state_invalid",
                "every public or known_to_player route listed as known",
                "visible route omitted");
        }

        var validPaths = new List<WoundDiagnosisPath>();
        var confusablePathIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var diagnosis in treatment.DiagnosisPaths)
        {
            var path = string.IsNullOrWhiteSpace(diagnosis.SourcePath)
                ? treatmentPath + ".diagnosisPaths"
                : diagnosis.SourcePath;
            if (diagnosis.DiagnosisPathId.Length > 0 &&
                !confusablePathIds.Add(ConfusableKey(diagnosis.DiagnosisPathId)))
            {
                AddInvalid(
                    issues,
                    path + ".diagnosisPathId",
                    "exact and Unicode-confusable unique diagnosis path identifier",
                    diagnosis.DiagnosisPathId);
            }
            var factsValid = ValidateDiagnosisPath(
                diagnosis, path, routeIds, complicationIds, issues);
            if (factsValid)
                validPaths.Add(diagnosis);
        }

        var facts = new HashSet<string>(StringComparer.Ordinal);
        foreach (var routeId in treatment.KnownRouteIds.Where(routeIds.Contains))
            facts.Add("route:" + routeId);
        foreach (var complication in complications.Where(static complication =>
                     complication.Visibility is "public" or "known_to_player"))
        {
            facts.Add("complication:" + complication.ComplicationId);
        }

        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var diagnosis in validPaths)
            {
                if (diagnosis.Visibility == "gm_only" ||
                    diagnosis.Visibility == "hidden" && diagnosis.RequiresKnownFacts.Count == 0 ||
                    !diagnosis.RequiresKnownFacts.All(facts.Contains))
                {
                    continue;
                }
                foreach (var reveal in diagnosis.Reveals)
                    changed |= facts.Add(reveal);
            }
        }

        var hasUnseededCycle = HasUnseededDiagnosisCycle(validPaths, facts);
        if (hasUnseededCycle)
        {
            AddIssue(
                issues,
                treatmentPath + ".diagnosisPaths",
                "wound_treatment_discovery_cycle",
                "least-fixed-point discovery graph rooted in player-known facts",
                "unseeded diagnosis dependency cycle");
        }

        foreach (var route in treatment.Routes.Where(static route => route.Visibility == "hidden"))
        {
            if (facts.Contains("route:" + route.RouteId))
                continue;
            var routePath = string.IsNullOrWhiteSpace(route.SourcePath)
                ? treatmentPath + ".routes"
                : route.SourcePath;
            AddIssue(
                issues,
                routePath + ".routeId",
                "wound_treatment_hidden_route_undiscoverable",
                "hidden route reachable through the least fixed point of player diagnosis facts",
                route.RouteId);
        }
    }

    private static bool HasUnseededDiagnosisCycle(
        IReadOnlyList<WoundDiagnosisPath> diagnosisPaths,
        IReadOnlySet<string> reachableFacts)
    {
        var graph = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var diagnosis in diagnosisPaths)
        {
            if (diagnosis.Visibility == "gm_only" ||
                diagnosis.RequiresKnownFacts.Count == 0 ||
                diagnosis.RequiresKnownFacts.All(reachableFacts.Contains))
            {
                continue;
            }

            foreach (var prerequisite in diagnosis.RequiresKnownFacts.Where(
                         fact => !reachableFacts.Contains(fact)))
            {
                if (!graph.TryGetValue(prerequisite, out var dependants))
                {
                    dependants = new HashSet<string>(StringComparer.Ordinal);
                    graph.Add(prerequisite, dependants);
                }
                foreach (var reveal in diagnosis.Reveals.Where(
                             fact => !reachableFacts.Contains(fact)))
                {
                    dependants.Add(reveal);
                }
            }
        }

        var states = new Dictionary<string, byte>(StringComparer.Ordinal);
        bool Visit(string fact)
        {
            if (states.TryGetValue(fact, out var state))
                return state == 1;
            states[fact] = 1;
            if (graph.TryGetValue(fact, out var dependants))
            {
                foreach (var dependant in dependants)
                {
                    if (Visit(dependant))
                        return true;
                }
            }
            states[fact] = 2;
            return false;
        }

        return graph.Keys.Any(Visit);
    }

    private static bool ValidateDiagnosisPath(
        WoundDiagnosisPath diagnosis,
        string path,
        IReadOnlySet<string>? routeIds,
        IReadOnlySet<string>? complicationIds,
        List<ValidationIssue> issues)
    {
        ValidateRequirements(diagnosis.Requirements, path + ".requirements", issues);
        if (diagnosis.Check.ValueKind != JsonValueKind.Object)
        {
            AddInvalid(issues, path + ".check", "exact empty version-1 check object",
                diagnosis.Check.ValueKind.ToString());
        }
        else
        {
            foreach (var property in diagnosis.Check.EnumerateObject())
            {
                AddIssue(issues, path + ".check." + property.Name,
                    "wound_materialization_unknown_field", "exact empty version-1 check object", property.Name);
            }
        }
        if (diagnosis.Visibility == "hidden" && diagnosis.RequiresKnownFacts.Count == 0)
        {
            AddIssue(issues, path + ".requiresKnownFacts",
                "wound_treatment_diagnosis_path_unreachable",
                "at least one known-fact prerequisite for a hidden diagnosis path", "empty array");
        }

        var prerequisitesValid = ValidateDiagnosisFacts(
            diagnosis.RequiresKnownFacts, path + ".requiresKnownFacts", routeIds, complicationIds, issues);
        var revealsValid = ValidateDiagnosisFacts(
            diagnosis.Reveals, path + ".reveals", routeIds, complicationIds, issues);
        return prerequisitesValid && revealsValid;
    }

    private static bool ValidateDiagnosisFacts(
        IReadOnlyList<string> facts,
        string path,
        IReadOnlySet<string>? routeIds,
        IReadOnlySet<string>? complicationIds,
        List<ValidationIssue> issues)
    {
        var valid = true;
        for (var index = 0; index < facts.Count; index++)
        {
            var fact = facts[index];
            var resolves = fact.StartsWith("route:", StringComparison.Ordinal)
                ? ResourceMaterializationContract.IsExactIdentifier(fact["route:".Length..]) &&
                  (routeIds is null || routeIds.Contains(fact["route:".Length..]))
                : fact.StartsWith("complication:", StringComparison.Ordinal) &&
                  ResourceMaterializationContract.IsExactIdentifier(fact["complication:".Length..]) &&
                  (complicationIds is null || complicationIds.Contains(fact["complication:".Length..]));
            if (resolves)
                continue;
            valid = false;
            AddIssue(
                issues,
                $"{path}[{index}]",
                "wound_treatment_diagnosis_fact_unknown",
                "route:<routeId> or complication:<complicationId> resolving inside this wound",
                fact);
        }
        return valid;
    }

    private static void ValidateRequirements(
        IReadOnlyList<JsonElement> requirements,
        string path,
        List<ValidationIssue> issues)
    {
        for (var index = 0; index < requirements.Count; index++)
            ValidateRequirement(requirements[index], $"{path}[{index}]", issues);
    }

    private static void ValidateRequirement(
        JsonElement requirement,
        string path,
        List<ValidationIssue> issues)
    {
        if (requirement.ValueKind != JsonValueKind.Object)
        {
            AddInvalid(issues, path, "closed requirement object", requirement.ValueKind.ToString());
            return;
        }

        var kind = ReadString(requirement, "kind");
        switch (kind)
        {
            case "item_quantity":
                ValidateObject(requirement, path, Set("kind", "itemRef", "quantity", "ownerRole"), issues);
                ValidateIdentifier(requirement, "itemRef", path, issues);
                ValidatePositiveInt32(requirement, "quantity", path, issues);
                ValidateClosedString(requirement, "ownerRole", path, OwnerRoles, issues);
                break;
            case "resource_quantity":
                ValidateObject(requirement, path, Set("kind", "resourceRef", "quantity", "ownerRole"), issues);
                ValidateIdentifier(requirement, "resourceRef", path, issues);
                ValidatePositiveInt32(requirement, "quantity", path, issues);
                ValidateClosedString(requirement, "ownerRole", path, OwnerRoles, issues);
                break;
            case "skill_tier":
                ValidateObject(requirement, path, Set("kind", "capabilityRef", "minimumTier", "actorRole"), issues);
                ValidateIdentifier(requirement, "capabilityRef", path, issues);
                ValidateInt32(
                    requirement,
                    "minimumTier",
                    path,
                    int.MinValue,
                    int.MaxValue,
                    issues);
                ValidateClosedString(requirement, "actorRole", path, ActorRoles, issues);
                break;
            case "source_capability":
                ValidateObject(requirement, path, Set("kind", "capabilityRef", "actorRole"), issues);
                ValidateIdentifier(requirement, "capabilityRef", path, issues);
                ValidateClosedString(requirement, "actorRole", path, ActorRoles, issues);
                break;
            case "provider":
                ValidateObject(requirement, path, Set("kind", "providerRef"), issues);
                ValidateIdentifier(requirement, "providerRef", path, issues);
                break;
            case "consent":
                ValidateObject(requirement, path, Set("kind", "consentRef", "providerRef", "targetRef"), issues);
                ValidateIdentifier(requirement, "consentRef", path, issues);
                ValidateIdentifier(requirement, "providerRef", path, issues);
                ValidateIdentifier(requirement, "targetRef", path, issues);
                break;
            case "facility":
                ValidateObject(requirement, path, Set("kind", "facilityRef"), issues);
                ValidateIdentifier(requirement, "facilityRef", path, issues);
                break;
            case "location":
                ValidateObject(requirement, path, Set("kind", "locationRef", "targetRole"), issues);
                ValidateIdentifier(requirement, "locationRef", path, issues);
                ValidateExactString(requirement, "targetRole", path, "target", issues);
                break;
            case "quest_state":
                ValidateObject(requirement, path, Set("kind", "questRef", "requiredState"), issues);
                ValidateIdentifier(requirement, "questRef", path, issues);
                ValidateIdentifier(requirement, "requiredState", path, issues);
                break;
            case "effect_state":
                ValidateObject(requirement, path, Set("kind", "effectRef", "requiredState", "targetRole"), issues);
                ValidateIdentifier(requirement, "effectRef", path, issues);
                ValidateIdentifier(requirement, "requiredState", path, issues);
                ValidateExactString(requirement, "targetRole", path, "target", issues);
                break;
            case "environment":
                ValidateObject(requirement, path, Set("kind", "environmentRef", "requiredState"), issues);
                ValidateIdentifier(requirement, "environmentRef", path, issues);
                ValidateIdentifier(requirement, "requiredState", path, issues);
                break;
            default:
                AddInvalid(
                    issues,
                    path + ".kind",
                    "registered Mortal treatment requirement kind",
                    kind.Length == 0 ? RawProperty(requirement, "kind") : kind);
                break;
        }
    }

    private static void ValidateResourcePolicy(
        WoundTreatmentRoute route,
        string routePath,
        List<ValidationIssue> issues)
    {
        var policy = route.ResourcePolicy;
        var path = routePath + ".resourcePolicy";
        if (!ValidateObject(policy, path, ResourcePolicyFields, issues))
            return;

        if (!TryGetBoolean(policy, "reserveBeforeResolution", out var reserve) || !reserve)
        {
            AddInvalid(
                issues,
                path + ".reserveBeforeResolution",
                "exact boolean true",
                RawProperty(policy, "reserveBeforeResolution"));
        }

        var reachableCategories = route.Mode switch
        {
            "procedure" => Set("success", "partial_success", "failed_attempt"),
            "course" => Set("success"),
            "guaranteed" => Set("success"),
            _ => ResultCategories
        };
        ValidateClosedStringArray(
            policy,
            "consumeOn",
            path,
            reachableCategories,
            exactSequence: null,
            issues);
        ValidateClosedStringArray(
            policy,
            "refundOn",
            path,
            Set("cancelled", "validation_failed", "rolled_back"),
            new[] { "cancelled", "validation_failed", "rolled_back" },
            issues);

        if (!TryGetArray(policy, "mutations", out var mutations))
        {
            AddMissing(issues, path + ".mutations");
            return;
        }

        if (mutations.GetArrayLength() > MaxResourceSelectors)
        {
            AddLimit(issues, path + ".mutations", MaxResourceSelectors, mutations.GetArrayLength());
        }

        var selectors = new HashSet<string>(StringComparer.Ordinal);
        var mutationIndex = 0;
        foreach (var mutation in mutations.EnumerateArray())
        {
            if (mutationIndex >= MaxResourceSelectors)
                break;
            var mutationPath = $"{path}.mutations[{mutationIndex}]";
            if (!ValidateObject(mutation, mutationPath, ResourceMutationFields, issues))
            {
                mutationIndex++;
                continue;
            }

            ValidateExactString(mutation, "kind", mutationPath, "consume_requirement", issues);
            var scope = ValidateClosedString(
                mutation,
                "scope",
                mutationPath,
                Set("common", "course_milestone"),
                issues);
            var requirementIndex = ReadInt32(
                mutation,
                "requirementIndex",
                mutationPath,
                0,
                WoundMaterializationContract.MaxRequirementsPerTreatmentMember - 1,
                issues);
            int? milestoneOrdinal = ReadNullablePositiveInt32(
                mutation,
                "milestoneOrdinal",
                mutationPath,
                issues);

            if (scope == "common")
            {
                if (milestoneOrdinal is not null)
                {
                    AddInvalid(
                        issues,
                        mutationPath + ".milestoneOrdinal",
                        "null for common requirement selector",
                        milestoneOrdinal.Value.ToString(CultureInfo.InvariantCulture));
                }
                if (requirementIndex is { } commonIndex &&
                    (commonIndex >= route.Requirements.Count ||
                     !IsQuantityRequirement(route.Requirements[commonIndex])))
                {
                    AddInvalid(
                        issues,
                        mutationPath + ".requirementIndex",
                        "existing item_quantity or resource_quantity common requirement",
                        commonIndex.ToString(CultureInfo.InvariantCulture));
                }
            }
            else if (scope == "course_milestone")
            {
                if (route.Mode != "course" || milestoneOrdinal is null)
                {
                    AddInvalid(
                        issues,
                        mutationPath + ".scope",
                        "course_milestone only in course mode with positive milestoneOrdinal",
                        scope);
                }
                else if (!TryFindCourseMilestoneRequirements(
                             route,
                             milestoneOrdinal.Value,
                             out var milestoneRequirements))
                {
                    AddInvalid(
                        issues,
                        mutationPath + ".milestoneOrdinal",
                        "exact existing course milestone ordinal",
                        milestoneOrdinal.Value.ToString(CultureInfo.InvariantCulture));
                }
                else if (requirementIndex is { } milestoneIndex &&
                         (milestoneIndex >= milestoneRequirements.GetArrayLength() ||
                          !IsQuantityRequirement(milestoneRequirements[milestoneIndex])))
                {
                    AddInvalid(
                        issues,
                        mutationPath + ".requirementIndex",
                        "existing item_quantity or resource_quantity milestone requirement",
                        milestoneIndex.ToString(CultureInfo.InvariantCulture));
                }
            }

            var selector = string.Join(
                "|",
                scope,
                milestoneOrdinal?.ToString(CultureInfo.InvariantCulture) ?? "null",
                requirementIndex?.ToString(CultureInfo.InvariantCulture) ?? "invalid");
            if (!selectors.Add(selector))
            {
                AddInvalid(
                    issues,
                    mutationPath,
                    "exact-unique consume_requirement selector",
                    selector);
            }
            mutationIndex++;
        }
    }

    private static bool TryFindCourseMilestoneRequirements(
        WoundTreatmentRoute route,
        int milestoneOrdinal,
        out JsonElement requirements)
    {
        foreach (var milestone in route.Outcomes)
        {
            if (milestone.ValueKind != JsonValueKind.Object ||
                !milestone.TryGetProperty("ordinal", out var ordinal) ||
                ordinal.ValueKind != JsonValueKind.Number ||
                !ordinal.TryGetInt32(out var currentOrdinal) ||
                currentOrdinal != milestoneOrdinal ||
                !milestone.TryGetProperty("requirements", out requirements) ||
                requirements.ValueKind != JsonValueKind.Array)
            {
                continue;
            }
            return true;
        }
        requirements = default;
        return false;
    }

    private static void ValidateProcedure(
        WoundTreatmentRoute route,
        string routePath,
        ValidationContext context,
        List<ValidationIssue> issues)
    {
        var resolutionPath = routePath + ".resolution";
        if (ValidateObject(route.Resolution, resolutionPath, ProcedureResolutionFields, issues))
        {
            ValidateExactString(
                route.Resolution,
                "formulaKey",
                resolutionPath,
                "mortal_wound_procedure_v1",
                issues);
            ValidateInt32(route.Resolution, "difficulty", resolutionPath, 0, int.MaxValue, issues);
            ValidateExactString(route.Resolution, "rollSource", resolutionPath, "accepted_d20", issues);
            ValidateExactString(
                route.Resolution,
                "criticalPolicy",
                resolutionPath,
                "natural_20_first_natural_1_last",
                issues);
            ValidateProcedureModifier(route, resolutionPath, issues);
        }

        ValidateProcedureOutcomes(
            route,
            routePath,
            context,
            issues);
        if (route.Interruption is not null)
        {
            AddInvalid(
                issues,
                routePath + ".interruption",
                "null for procedure mode",
                route.Interruption.Value.GetRawText());
        }
    }

    private static void ValidateProcedureModifier(
        WoundTreatmentRoute route,
        string resolutionPath,
        List<ValidationIssue> issues)
    {
        if (!route.Resolution.TryGetProperty("modifierSource", out var modifier))
        {
            AddMissing(issues, resolutionPath + ".modifierSource");
            return;
        }
        var path = resolutionPath + ".modifierSource";
        if (modifier.ValueKind != JsonValueKind.Object)
        {
            AddInvalid(issues, path, "closed modifier source object", modifier.ValueKind.ToString());
            return;
        }

        var kind = ReadString(modifier, "kind");
        if (kind == "fixed_zero")
        {
            ValidateObject(modifier, path, ProcedureModifierFixedFields, issues);
            return;
        }
        if (kind != "resolved_skill_tier")
        {
            AddInvalid(issues, path + ".kind", "fixed_zero | resolved_skill_tier", kind);
            return;
        }

        ValidateObject(modifier, path, ProcedureModifierSkillFields, issues);
        var index = ReadInt32(
            modifier,
            "requirementIndex",
            path,
            0,
            WoundMaterializationContract.MaxRequirementsPerTreatmentMember - 1,
            issues);
        if (index is not >= 0 || index >= route.Requirements.Count ||
            ReadString(route.Requirements[index.Value], "kind") != "skill_tier")
        {
            AddInvalid(
                issues,
                path + ".requirementIndex",
                "zero-based index of one route skill_tier requirement",
                index?.ToString(CultureInfo.InvariantCulture) ?? RawProperty(modifier, "requirementIndex"));
        }
    }

    private static void ValidateProcedureOutcomes(
        WoundTreatmentRoute route,
        string routePath,
        ValidationContext context,
        List<ValidationIssue> issues)
    {
        var path = routePath + ".outcomes";
        if (route.Outcomes.Count is < 2 or > MaxProcedureBands)
        {
            if (route.Outcomes.Count > MaxProcedureBands)
                AddLimit(issues, path, MaxProcedureBands, route.Outcomes.Count);
            else if (route.Outcomes.Count == 0)
                AddMissing(issues, path);
            else
                AddInvalid(issues, path, "2-16 ordered procedure outcome bands", route.Outcomes.Count.ToString(CultureInfo.InvariantCulture));
        }

        var rows = new List<ProcedureBand>();
        var confusableBandIds = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < route.Outcomes.Count && index < MaxProcedureBands; index++)
        {
            var outcome = route.Outcomes[index];
            var outcomePath = $"{path}[{index}]";
            if (!ValidateObject(outcome, outcomePath, ProcedureOutcomeFields, issues))
                continue;

            var bandId = ValidateIdentifier(outcome, "bandId", outcomePath, issues);
            if (bandId.Length > 0 && !confusableBandIds.Add(ConfusableKey(bandId)))
            {
                AddInvalid(
                    issues,
                    outcomePath + ".bandId",
                    "exact and Unicode-confusable unique band identifier",
                    bandId);
            }
            var minimum = ReadNullableInt64(outcome, "minimumMargin", outcomePath, issues);
            var maximum = ReadNullableInt64(outcome, "maximumMargin", outcomePath, issues);
            var category = ValidateClosedString(
                outcome,
                "category",
                outcomePath,
                ResultCategories,
                issues);
            var summary = ValidateResult(
                outcome,
                "result",
                outcomePath,
                allowEmpty: false,
                context,
                issues);
            if (category == "success")
            {
                if (!summary.HasPositive)
                {
                    AddInvalid(
                        issues,
                        outcomePath + ".result",
                        "at least one structurally positive treatment operation in every success band",
                        "no positive operation");
                }
                if (summary.HasNoImprovement || summary.HasAdverse)
                {
                    var adverseIndex = summary.FirstNonPositiveIndex ?? 0;
                    AddInvalid(
                        issues,
                        $"{outcomePath}.result[{adverseIndex}].kind",
                        "success-band operation from stabilize | add_recovery | reduce_severity | remove_complication | heal",
                        summary.FirstNonPositiveKind ?? "non-positive operation");
                }
            }
            rows.Add(new ProcedureBand(outcomePath, minimum, maximum, category));
        }

        if (rows.Count == 0)
            return;

        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            if (index < rows.Count - 1 && row.Minimum is null)
            {
                AddInvalid(
                    issues,
                    row.Path + ".minimumMargin",
                    "finite inclusive lower bound on every band except the last",
                    "null");
            }
            if (index > 0 && row.Maximum is null)
            {
                AddInvalid(
                    issues,
                    row.Path + ".maximumMargin",
                    "finite inclusive upper bound on every band except the first",
                    "null");
            }
            if (row.Minimum is { } minimum &&
                row.Maximum is { } maximum &&
                minimum > maximum)
            {
                AddInvalid(
                    issues,
                    row.Path + ".minimumMargin",
                    "inclusive lower bound not greater than maximumMargin",
                    minimum.ToString(CultureInfo.InvariantCulture));
            }
        }

        if (rows[0].Maximum is { } firstMaximum)
        {
            AddInvalid(
                issues,
                rows[0].Path + ".maximumMargin",
                "null unbounded upper margin on first band",
                firstMaximum.ToString(CultureInfo.InvariantCulture));
        }
        if (rows[^1].Minimum is { } lastMinimum)
        {
            AddInvalid(
                issues,
                rows[^1].Path + ".minimumMargin",
                "null unbounded lower margin on last band",
                lastMinimum.ToString(CultureInfo.InvariantCulture));
        }
        if (rows[0].Category != "success")
            AddInvalid(issues, rows[0].Path + ".category", "success", rows[0].Category);
        if (rows[^1].Category != "failed_attempt")
            AddInvalid(issues, rows[^1].Path + ".category", "failed_attempt", rows[^1].Category);

        for (var index = 1; index < rows.Count; index++)
        {
            var higher = rows[index - 1];
            var lower = rows[index];
            if (CategoryRank(lower.Category) < CategoryRank(higher.Category))
            {
                AddInvalid(
                    issues,
                    lower.Path + ".category",
                    "monotone success -> partial_success -> failed_attempt categories",
                    lower.Category);
            }

            if (higher.Minimum is not { } higherMinimum || lower.Maximum is not { } lowerMaximum)
                continue;
            long expectedHigherMinimum;
            try
            {
                expectedHigherMinimum = checked(lowerMaximum + 1L);
            }
            catch (OverflowException)
            {
                AddInvalid(
                    issues,
                    lower.Path + ".maximumMargin",
                    "finite bound whose checked successor exists",
                    lowerMaximum.ToString(CultureInfo.InvariantCulture));
                continue;
            }

            if (higherMinimum == expectedHigherMinimum)
                continue;
            if (lowerMaximum >= higherMinimum)
            {
                AddInvalid(
                    issues,
                    lower.Path + ".maximumMargin",
                    "one less than preceding minimumMargin",
                    lowerMaximum.ToString(CultureInfo.InvariantCulture));
            }
            else
            {
                AddInvalid(
                    issues,
                    higher.Path + ".minimumMargin",
                    "one more than following maximumMargin",
                    higherMinimum.ToString(CultureInfo.InvariantCulture));
            }
        }
    }

    private static void ValidateCourse(
        WoundTreatmentRoute route,
        string routePath,
        ValidationContext context,
        List<ValidationIssue> issues)
    {
        var resolutionPath = routePath + ".resolution";
        if (route.Resolution.ValueKind == JsonValueKind.Object &&
            route.Resolution.TryGetProperty("formulaKey", out _))
        {
            AddInvalid(
                issues,
                resolutionPath,
                "closed course resolution object",
                route.Resolution.GetRawText());
        }
        if (ValidateObject(route.Resolution, resolutionPath, CourseResolutionFields, issues))
        {
            ValidateExactString(
                route.Resolution,
                "clockKind",
                resolutionPath,
                "world_time.currentTimeInMinutes",
                issues);
            ReadInt64(route.Resolution, "maximumGapMinutes", resolutionPath, 0, long.MaxValue, issues);
        }

        var outcomesPath = routePath + ".outcomes";
        if (route.Outcomes.Count is < 1 or > MaxCourseMilestones)
        {
            if (route.Outcomes.Count > MaxCourseMilestones)
                AddLimit(issues, outcomesPath, MaxCourseMilestones, route.Outcomes.Count);
            else
                AddInvalid(issues, outcomesPath, "1-32 course milestones", route.Outcomes.Count.ToString(CultureInfo.InvariantCulture));
        }

        var hasPositive = false;
        long? previousMinutes = null;
        for (var index = 0; index < route.Outcomes.Count && index < MaxCourseMilestones; index++)
        {
            var outcome = route.Outcomes[index];
            var outcomePath = $"{outcomesPath}[{index}]";
            if (!ValidateObject(outcome, outcomePath, CourseMilestoneFields, issues))
                continue;
            var ordinal = ReadInt32(outcome, "ordinal", outcomePath, 1, MaxCourseMilestones, issues);
            if (ordinal != index + 1)
                AddInvalid(issues, outcomePath + ".ordinal", $"contiguous ordinal {index + 1}", ordinal?.ToString(CultureInfo.InvariantCulture) ?? "invalid");
            var afterMinutes = ReadInt64(outcome, "afterMinutes", outcomePath, 0, long.MaxValue, issues);
            if (index == 0 && afterMinutes != 0)
                AddInvalid(issues, outcomePath + ".afterMinutes", "exact integer 0 for first milestone", afterMinutes?.ToString(CultureInfo.InvariantCulture) ?? "invalid");
            if (index > 0 && previousMinutes is { } previous && afterMinutes is { } current && current <= previous)
                AddInvalid(issues, outcomePath + ".afterMinutes", "strictly increasing canonical minutes", current.ToString(CultureInfo.InvariantCulture));
            previousMinutes = afterMinutes;

            if (outcome.TryGetProperty("requirements", out var milestoneRequirements) &&
                milestoneRequirements.ValueKind == JsonValueKind.Array)
            {
                var parsed = milestoneRequirements.EnumerateArray().Take(
                    WoundMaterializationContract.MaxRequirementsPerTreatmentMember).ToArray();
                if (milestoneRequirements.GetArrayLength() > WoundMaterializationContract.MaxRequirementsPerTreatmentMember)
                    AddLimit(issues, outcomePath + ".requirements", WoundMaterializationContract.MaxRequirementsPerTreatmentMember, milestoneRequirements.GetArrayLength());
                for (var requirementIndex = 0; requirementIndex < parsed.Length; requirementIndex++)
                    ValidateRequirement(parsed[requirementIndex], $"{outcomePath}.requirements[{requirementIndex}]", issues);
            }
            else
            {
                AddInvalid(issues, outcomePath + ".requirements", "array of closed milestone requirements", RawProperty(outcome, "requirements"));
            }

            ValidateExactString(outcome, "category", outcomePath, "success", issues);
            var expectedCompletion = index == route.Outcomes.Count - 1 ? "completed" : "active";
            ValidateExactString(outcome, "completion", outcomePath, expectedCompletion, issues);
            var summary = ValidateResult(
                outcome,
                "result",
                outcomePath,
                allowEmpty: index < route.Outcomes.Count - 1,
                context,
                issues);
            hasPositive |= summary.HasPositive;
            if (summary.HasNoImprovement || summary.HasAdverse)
            {
                var nonPositiveIndex = summary.FirstNonPositiveIndex ?? 0;
                AddInvalid(
                    issues,
                    $"{outcomePath}.result[{nonPositiveIndex}].kind",
                    "positive-only course milestone operation",
                    summary.FirstNonPositiveKind ?? "non-positive operation");
            }
        }
        if (!hasPositive)
            AddInvalid(issues, outcomesPath, "course containing at least one positive treatment operation", "no positive operation");

        ValidateCourseInterruption(
            route.Interruption,
            routePath + ".interruption",
            context,
            issues);
    }

    private static void ValidateCourseInterruption(
        JsonElement? interruption,
        string path,
        ValidationContext context,
        List<ValidationIssue> issues)
    {
        if (interruption is null)
        {
            AddInvalid(issues, path, "closed course interruption object", "null");
            return;
        }
        if (!ValidateObject(interruption.Value, path, CourseInterruptionFields, issues))
            return;
        ValidateExactString(interruption.Value, "category", path, "failed_attempt", issues);
        var summary = ValidateResult(
            interruption.Value,
            "result",
            path,
            allowEmpty: false,
            context,
            issues,
            interruption: true);
        if (summary.HasPositive)
        {
            var positiveIndex = summary.FirstPositiveIndex ?? 0;
            AddInvalid(
                issues,
                $"{path}.result[{positiveIndex}].kind",
                "no beneficial operation in course interruption",
                summary.FirstPositiveKind ?? "positive operation");
        }
    }

    private static void ValidateGuaranteed(
        WoundTreatmentRoute route,
        string routePath,
        ValidationContext context,
        List<ValidationIssue> issues)
    {
        var resolutionPath = routePath + ".resolution";
        if (route.Resolution.ValueKind == JsonValueKind.Object &&
            route.Resolution.TryGetProperty("formulaKey", out _))
        {
            AddInvalid(
                issues,
                resolutionPath,
                "closed guaranteed resolution object",
                route.Resolution.GetRawText());
        }
        string capabilityRef = string.Empty;
        string actorRole = string.Empty;
        if (ValidateObject(route.Resolution, resolutionPath, GuaranteedResolutionFields, issues))
        {
            capabilityRef = ValidateIdentifier(route.Resolution, "capabilityRef", resolutionPath, issues);
            actorRole = ValidateClosedString(route.Resolution, "actorRole", resolutionPath, ActorRoles, issues);
        }

        var matching = route.Requirements
            .Where(static requirement => ReadString(requirement, "kind") == "source_capability")
            .ToArray();
        if (matching.Length != 1)
        {
            AddInvalid(
                issues,
                resolutionPath + ".capabilityRef",
                "one matching source_capability requirement",
                matching.Length.ToString(CultureInfo.InvariantCulture));
        }
        else
        {
            var requirementCapability = ReadString(matching[0], "capabilityRef");
            var requirementRole = ReadString(matching[0], "actorRole");
            if (!string.Equals(capabilityRef, requirementCapability, StringComparison.Ordinal))
                AddInvalid(issues, resolutionPath + ".capabilityRef", "exact matching requirement capabilityRef", capabilityRef);
            if (!string.Equals(actorRole, requirementRole, StringComparison.Ordinal))
                AddInvalid(issues, resolutionPath + ".actorRole", "exact matching requirement actorRole", actorRole);
        }

        var outcomesPath = routePath + ".outcomes";
        if (route.Outcomes.Count != 1)
            AddInvalid(issues, outcomesPath, "exactly one guaranteed outcome", route.Outcomes.Count.ToString(CultureInfo.InvariantCulture));
        if (route.Outcomes.Count > 0)
        {
            var outcomePath = outcomesPath + "[0]";
            var outcome = route.Outcomes[0];
            if (ValidateObject(outcome, outcomePath, GuaranteedOutcomeFields, issues))
            {
                ValidateExactString(outcome, "category", outcomePath, "success", issues);
                var summary = ValidateResult(
                    outcome,
                    "result",
                    outcomePath,
                    allowEmpty: false,
                    context,
                    issues);
                if (!summary.HasPositive)
                    AddInvalid(issues, outcomePath + ".result", "at least one positive guaranteed operation", "no positive operation");
                if (summary.HasNoImprovement || summary.HasAdverse)
                {
                    var nonPositiveIndex = summary.FirstNonPositiveIndex ?? 0;
                    AddInvalid(
                        issues,
                        $"{outcomePath}.result[{nonPositiveIndex}].kind",
                        "positive guaranteed operation",
                        summary.FirstNonPositiveKind ?? "non-positive operation");
                }
            }
        }
        if (route.Interruption is not null)
            AddInvalid(issues, routePath + ".interruption", "null for guaranteed mode", route.Interruption.Value.GetRawText());
    }

    private static ResultSummary ValidateResult(
        JsonElement parent,
        string field,
        string parentPath,
        bool allowEmpty,
        ValidationContext context,
        List<ValidationIssue> issues,
        bool interruption = false)
    {
        var path = parentPath + "." + field;
        if (!TryGetArray(parent, field, out var result))
        {
            AddMissing(issues, path);
            return ResultSummary.Empty;
        }
        if (result.GetArrayLength() == 0 && !allowEmpty)
            AddInvalid(issues, path, "non-empty ordered operation array", "empty array");
        if (result.GetArrayLength() > MaxOutcomeOperations)
            AddLimit(issues, path, MaxOutcomeOperations, result.GetArrayLength());

        var complicationRefs = new HashSet<string>(StringComparer.Ordinal);
        var summary = new ResultSummaryBuilder();
        var operationIndex = 0;
        foreach (var operation in result.EnumerateArray())
        {
            if (operationIndex >= MaxOutcomeOperations)
                break;
            var operationPath = $"{path}[{operationIndex}]";
            var kind = ValidateOperation(
                operation,
                operationPath,
                context,
                complicationRefs,
                interruption,
                issues);
            summary.Add(kind, operationIndex, operation);
            operationIndex++;
        }

        if (summary.HasNoImprovement && operationIndex != 1)
            AddInvalid(issues, path, "no_improvement as the sole operation", $"{operationIndex} operations");
        if (summary.HealCount > 1)
            AddInvalid(issues, path, "at most one heal operation", summary.HealCount.ToString(CultureInfo.InvariantCulture));
        if (summary.HealIndex is { } healIndex && healIndex != operationIndex - 1)
            AddInvalid(issues, $"{path}[{healIndex + 1}]", "no operation after terminal heal", "operation follows heal");
        var maximumReduction = summary.HealCount == 0 ? 2 : 3;
        if (summary.SeverityReduction > maximumReduction)
            AddInvalid(issues, path, $"aggregate severity reduction at most {maximumReduction}", summary.SeverityReduction.ToString(CultureInfo.InvariantCulture));
        return summary.Build();
    }

    private static string ValidateOperation(
        JsonElement operation,
        string path,
        ValidationContext context,
        HashSet<string> complicationRefs,
        bool interruption,
        List<ValidationIssue> issues)
    {
        if (operation.ValueKind != JsonValueKind.Object)
        {
            AddInvalid(issues, path, "closed typed outcome operation object", operation.GetRawText());
            return string.Empty;
        }
        var kind = ReadString(operation, "kind");
        switch (kind)
        {
            case "no_improvement":
            case "stabilize":
                ValidateObject(operation, path, Set("kind"), issues);
                break;
            case "add_recovery":
                ValidateObject(operation, path, Set("kind", "points"), issues);
                ValidatePositiveInt32(operation, "points", path, issues);
                break;
            case "reduce_severity":
                ValidateObject(operation, path, Set("kind", "steps"), issues);
                ReadInt32(operation, "steps", path, 1, 2, issues);
                break;
            case "remove_complication":
                ValidateObject(operation, path, Set("kind", "complicationId"), issues);
                ValidateIdentifier(operation, "complicationId", path, issues);
                break;
            case "add_complication":
                ValidateAddComplication(
                    operation,
                    path,
                    context,
                    complicationRefs,
                    interruption
                        ? ComplicationValidationUse.Interruption
                        : ComplicationValidationUse.Ordinary,
                    issues);
                break;
            case "apply_deterioration":
                ValidateObject(operation, path, Set("kind", "policyRef"), issues);
                var policyRef = ValidateIdentifier(operation, "policyRef", path, issues);
                if (context.Wound is { } wound &&
                    (wound.CurrentPolicyRef is null ||
                     !string.Equals(policyRef, wound.CurrentPolicyRef, StringComparison.Ordinal)))
                {
                    AddInvalid(
                        issues,
                        path + ".policyRef",
                        "exact reference to the current declared deterioration policy",
                        policyRef);
                }
                break;
            case "heal":
                ValidateObject(operation, path, Set("kind", "legacies"), issues);
                ValidateHealLegacies(
                    operation,
                    path,
                    context.Realm,
                    context.Wound?.OwnerTargetKind,
                    issues);
                break;
            default:
                AddInvalid(
                    issues,
                    path + (operation.TryGetProperty("kind", out _) ? ".kind" : string.Empty),
                    "closed version-1 outcome operation kind",
                    kind.Length == 0 ? operation.GetRawText() : kind);
                break;
        }
        return kind;
    }

    private static void ValidateAddComplication(
        JsonElement operation,
        string path,
        ValidationContext context,
        HashSet<string> complicationRefs,
        ComplicationValidationUse use,
        List<ValidationIssue> issues)
    {
        ValidateObject(operation, path, Set("kind", "complicationDraft"), issues);
        if (!operation.TryGetProperty("complicationDraft", out var draft) ||
            !ValidateObject(draft, path + ".complicationDraft", Set("complications", "consequenceDefinitions"), issues))
            return;
        var draftPath = path + ".complicationDraft";
        if (!TryGetArray(draft, "complications", out var complications))
        {
            AddMissing(issues, draftPath + ".complications");
            return;
        }
        if (complications.GetArrayLength() != 1)
            AddInvalid(issues, draftPath + ".complications", "exactly one complication proposal", complications.GetArrayLength().ToString(CultureInfo.InvariantCulture));
        var complicationRef = string.Empty;
        if (complications.GetArrayLength() > 0)
        {
            var complication = complications[0];
            var complicationPath = draftPath + ".complications[0]";
            var fields = Set(
                "complicationRef", "kind", "state", "displayName",
                "treatmentDifficultyModifier", "visibility");
            if (ValidateObject(complication, complicationPath, fields, issues))
            {
                complicationRef = ValidateIdentifier(complication, "complicationRef", complicationPath, issues);
                if (complicationRef.Length > 0 && !complicationRefs.Add(ConfusableKey(complicationRef)))
                {
                    AddInvalid(
                        issues,
                        complicationPath + ".complicationRef",
                        "exact and Unicode-confusable unique complicationRef in selected result",
                        complicationRef);
                }
                ValidateClosedString(complication, "kind", complicationPath, ComplicationKinds, issues);
                ValidateExactString(complication, "state", complicationPath, "active", issues);
                ValidateReadableText(complication, "displayName", complicationPath, issues);
                ReadInt32(
                    complication,
                    "treatmentDifficultyModifier",
                    complicationPath,
                    use == ComplicationValidationUse.Ordinary ? 0 : 1,
                    4,
                    issues);
                ValidateClosedString(
                    complication,
                    "visibility",
                    complicationPath,
                    ComplicationVisibilities,
                    issues);
            }
        }

        if (!TryGetArray(draft, "consequenceDefinitions", out var definitions))
        {
            AddMissing(issues, draftPath + ".consequenceDefinitions");
            return;
        }
        if (use == ComplicationValidationUse.Interruption &&
            definitions.GetArrayLength() != 0)
        {
            AddInvalid(
                issues,
                draftPath + ".consequenceDefinitions",
                "empty array for an effectless interruption complication",
                definitions.GetArrayLength().ToString(CultureInfo.InvariantCulture));
        }
        if (use != ComplicationValidationUse.Interruption)
        {
            ValidateComplicationConsequenceDefinitions(
                definitions,
                draftPath + ".consequenceDefinitions",
                context.Realm,
                context.Wound?.OwnerTargetKind,
                context.Wound?.SeverityRank,
                complicationRef,
                issues);
        }
    }

    private static void ValidateHealLegacies(
        JsonElement operation,
        string path,
        string realm,
        string? ownerTargetKind,
        List<ValidationIssue> issues)
    {
        if (!TryGetArray(operation, "legacies", out var legacies))
        {
            AddMissing(issues, path + ".legacies");
            return;
        }
        if (legacies.GetArrayLength() > MaxHealLegacies)
            AddLimit(issues, path + ".legacies", MaxHealLegacies, legacies.GetArrayLength());

        var legacyRefs = new HashSet<string>(StringComparer.Ordinal);
        var legacyIndex = 0;
        foreach (var legacy in legacies.EnumerateArray())
        {
            if (legacyIndex >= MaxHealLegacies)
                break;
            var legacyPath = $"{path}.legacies[{legacyIndex}]";
            if (legacy.ValueKind != JsonValueKind.Object)
            {
                AddInvalid(issues, legacyPath, "closed heal legacy object", legacy.GetRawText());
                legacyIndex++;
                continue;
            }

            var kind = ReadString(legacy, "kind");
            var fields = kind switch
            {
                "cosmetic" => Set("localLegacyRef", "kind", "readableSummary"),
                "mechanical_effect" => Set(
                    "localLegacyRef", "kind", "readableSummary", "effectDraft"),
                _ => Set("localLegacyRef", "kind", "readableSummary")
            };
            ValidateObject(legacy, legacyPath, fields, issues);
            var localRef = ValidateIdentifier(legacy, "localLegacyRef", legacyPath, issues);
            if (localRef.Length > 0 && !legacyRefs.Add(ConfusableKey(localRef)))
            {
                AddInvalid(
                    issues,
                    legacyPath + ".localLegacyRef",
                    "exact and Unicode-confusable unique localLegacyRef",
                    localRef);
            }
            ValidateReadableText(legacy, "readableSummary", legacyPath, issues);

            switch (kind)
            {
                case "cosmetic":
                    break;
                case "mechanical_effect":
                    ValidateMechanicalLegacyDraft(
                        legacy,
                        legacyPath,
                        realm,
                        ownerTargetKind,
                        issues);
                    break;
                default:
                    AddInvalid(
                        issues,
                        legacyPath + ".kind",
                        "cosmetic | mechanical_effect",
                        kind.Length == 0 ? RawProperty(legacy, "kind") : kind);
                    break;
            }
            legacyIndex++;
        }
    }

    private static void ValidateComplicationConsequenceDefinitions(
        JsonElement definitions,
        string path,
        string realm,
        string? ownerTargetKind,
        int? severityRank,
        string complicationRef,
        List<ValidationIssue> issues)
    {
        if (definitions.GetArrayLength() > WoundMaterializationContract.MaxOwnedEffectDefinitions)
        {
            AddLimit(
                issues,
                path,
                WoundMaterializationContract.MaxOwnedEffectDefinitions,
                definitions.GetArrayLength());
        }

        var definitionRefs = new HashSet<string>(StringComparer.Ordinal);
        var detachedDefinitions = new List<DetachedEffectDefinitionCandidate>();
        var roots = new List<DetachedEffectRootCandidate>();
        var index = 0;
        foreach (var wrapper in definitions.EnumerateArray())
        {
            if (index >= WoundMaterializationContract.MaxOwnedEffectDefinitions)
                break;
            var wrapperPath = $"{path}[{index}]";
            if (!ValidateObject(
                    wrapper,
                    wrapperPath,
                    Set("definitionRef", "definition", "root"),
                    issues))
            {
                index++;
                continue;
            }

            var definitionRef = ValidateIdentifier(wrapper, "definitionRef", wrapperPath, issues);
            if (definitionRef.Length > 0 && !definitionRefs.Add(ConfusableKey(definitionRef)))
            {
                AddInvalid(
                    issues,
                    wrapperPath + ".definitionRef",
                    "exact and Unicode-confusable unique definitionRef",
                    definitionRef);
            }
            if (!wrapper.TryGetProperty("definition", out var definition) ||
                definition.ValueKind != JsonValueKind.Object)
            {
                AddInvalid(
                    issues,
                    wrapperPath + ".definition",
                    "complete detached effect definition object",
                    RawProperty(wrapper, "definition"));
            }
            else
            {
                detachedDefinitions.Add(new DetachedEffectDefinitionCandidate(
                    definitionRef,
                    wrapperPath + ".definition",
                    definition.Clone()));
            }

            if (wrapper.TryGetProperty("root", out var root) && root.ValueKind != JsonValueKind.Null)
            {
                var parsedRoot = ValidateComplicationRoot(
                    root,
                    wrapperPath + ".root",
                    definitionRef,
                    complicationRef,
                    issues);
                if (parsedRoot is not null)
                    roots.Add(parsedRoot);
            }
            index++;
        }
        var graphDefinitions = ValidateDetachedEffectDefinitions(
            detachedDefinitions,
            path,
            realm,
            bindProposalWoundMarkers: true,
            issues);
        ValidateDetachedComplicationGraph(
            graphDefinitions,
            roots,
            path,
            ownerTargetKind,
            severityRank,
            issues);
    }

    private static DetachedEffectRootCandidate? ValidateComplicationRoot(
        JsonElement root,
        string path,
        string definitionRef,
        string complicationRef,
        List<ValidationIssue> issues)
    {
        if (!ValidateObject(root, path, Set("ownership", "slots"), issues))
            return null;
        if (!root.TryGetProperty("ownership", out var ownership) ||
            !ValidateObject(ownership, path + ".ownership", Set("kind", "complicationRef"), issues))
        {
            return null;
        }
        ValidateExactString(ownership, "kind", path + ".ownership", "complication", issues);
        var ownerRef = ValidateIdentifier(ownership, "complicationRef", path + ".ownership", issues);
        if (!string.Equals(ownerRef, complicationRef, StringComparison.Ordinal))
        {
            AddInvalid(
                issues,
                path + ".ownership.complicationRef",
                "the sole complicationRef declared by this operation",
                ownerRef);
        }

        if (!TryGetArray(root, "slots", out var slots))
        {
            AddMissing(issues, path + ".slots");
            return null;
        }
        if (slots.GetArrayLength() > WoundMaterializationContract.MaxConsequences)
            AddLimit(issues, path + ".slots", WoundMaterializationContract.MaxConsequences, slots.GetArrayLength());
        var parsedSlots = new List<DetachedEffectSlotCandidate>();
        var index = 0;
        foreach (var slot in slots.EnumerateArray())
        {
            if (index >= WoundMaterializationContract.MaxConsequences)
                break;
            var slotPath = $"{path}.slots[{index++}]";
            if (ValidateObject(slot, slotPath, Set("profileKey", "readableSummary"), issues))
            {
                var profileKey = ValidateIdentifier(slot, "profileKey", slotPath, issues);
                ValidateReadableText(slot, "readableSummary", slotPath, issues);
                if (profileKey.Length > 0)
                {
                    parsedSlots.Add(new DetachedEffectSlotCandidate(
                        profileKey,
                        slotPath + ".profileKey"));
                }
            }
        }
        return new DetachedEffectRootCandidate(
            definitionRef,
            path,
            parsedSlots);
    }

    private static void ValidateMechanicalLegacyDraft(
        JsonElement legacy,
        string legacyPath,
        string realm,
        string? ownerTargetKind,
        List<ValidationIssue> issues)
    {
        if (!legacy.TryGetProperty("effectDraft", out var draft))
        {
            AddMissing(issues, legacyPath + ".effectDraft");
            return;
        }
        var draftPath = legacyPath + ".effectDraft";
        if (!ValidateObject(draft, draftPath, Set("schemaVersion", "definitions", "applications"), issues))
            return;
        ReadInt32(draft, "schemaVersion", draftPath, 1, 1, issues);

        if (!TryGetArray(draft, "definitions", out var definitions))
        {
            AddMissing(issues, draftPath + ".definitions");
            return;
        }
        if (definitions.GetArrayLength() == 0)
            AddInvalid(issues, draftPath + ".definitions", "1-5 mechanical legacy definitions", "empty array");
        if (definitions.GetArrayLength() > WoundMaterializationContract.MaxOwnedEffectDefinitions)
            AddLimit(issues, draftPath + ".definitions", WoundMaterializationContract.MaxOwnedEffectDefinitions, definitions.GetArrayLength());

        var exactDefinitionRefs = new HashSet<string>(StringComparer.Ordinal);
        var exactDeclarationRefs = new HashSet<string>(StringComparer.Ordinal);
        var confusableDeclarationRefs = new HashSet<string>(StringComparer.Ordinal);
        var definitionsByRef = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        var detachedDefinitions = new List<DetachedEffectDefinitionCandidate>();
        var definitionIndex = 0;
        foreach (var wrapper in definitions.EnumerateArray())
        {
            if (definitionIndex >= WoundMaterializationContract.MaxOwnedEffectDefinitions)
                break;
            var wrapperPath = $"{draftPath}.definitions[{definitionIndex}]";
            if (ValidateObject(wrapper, wrapperPath, Set("definitionRef", "definition"), issues))
            {
                var definitionRef = ValidateIdentifier(wrapper, "definitionRef", wrapperPath, issues);
                if (definitionRef.Length > 0)
                {
                    exactDefinitionRefs.Add(definitionRef);
                    if (!exactDeclarationRefs.Add(definitionRef) ||
                        !confusableDeclarationRefs.Add(ConfusableKey(definitionRef)))
                    {
                        AddInvalid(
                            issues,
                            wrapperPath + ".definitionRef",
                            "exact and Unicode-confusable unique definitionRef in this legacy",
                            definitionRef);
                    }
                }
                if (!wrapper.TryGetProperty("definition", out var definition) ||
                    definition.ValueKind != JsonValueKind.Object)
                {
                    AddInvalid(
                        issues,
                        wrapperPath + ".definition",
                        "complete detached effect definition object",
                        RawProperty(wrapper, "definition"));
                }
                else
                {
                    detachedDefinitions.Add(new DetachedEffectDefinitionCandidate(
                        definitionRef,
                        wrapperPath + ".definition",
                        definition.Clone()));
                    if (definitionRef.Length > 0 &&
                        !definitionsByRef.ContainsKey(definitionRef))
                    {
                        definitionsByRef.Add(definitionRef, definition.Clone());
                    }
                }
            }
            definitionIndex++;
        }
        var graphDefinitions = ValidateDetachedEffectDefinitions(
            detachedDefinitions,
            draftPath + ".definitions",
            realm,
            bindProposalWoundMarkers: false,
            issues);

        if (!TryGetArray(draft, "applications", out var applications))
        {
            AddMissing(issues, draftPath + ".applications");
            return;
        }
        if (applications.GetArrayLength() == 0)
            AddInvalid(issues, draftPath + ".applications", "1-5 mechanical legacy applications", "empty array");
        if (applications.GetArrayLength() > WoundMaterializationContract.MaxOwnedEffectRootBindings)
            AddLimit(issues, draftPath + ".applications", WoundMaterializationContract.MaxOwnedEffectRootBindings, applications.GetArrayLength());

        var applicationRoots = new List<DetachedEffectRootCandidate>();
        var applicationIndex = 0;
        foreach (var application in applications.EnumerateArray())
        {
            if (applicationIndex >= WoundMaterializationContract.MaxOwnedEffectRootBindings)
                break;
            var applicationPath = $"{draftPath}.applications[{applicationIndex}]";
            if (ValidateObject(
                    application,
                    applicationPath,
                    Set("applicationRef", "definitionRef", "parameters"),
                    issues))
            {
                var applicationRef = ValidateIdentifier(application, "applicationRef", applicationPath, issues);
                if (applicationRef.Length > 0 &&
                    (!exactDeclarationRefs.Add(applicationRef) ||
                     !confusableDeclarationRefs.Add(ConfusableKey(applicationRef))))
                {
                    AddInvalid(
                        issues,
                        applicationPath + ".applicationRef",
                        "exact and Unicode-confusable unique definitionRef or applicationRef declaration in this legacy",
                        applicationRef);
                }
                var definitionRef = ValidateIdentifier(application, "definitionRef", applicationPath, issues);
                if (definitionRef.Length > 0 && !exactDefinitionRefs.Contains(definitionRef))
                {
                    AddInvalid(
                        issues,
                        applicationPath + ".definitionRef",
                        "exact definitionRef declared inside this legacy",
                        definitionRef);
                }
                if (!application.TryGetProperty("parameters", out var parameters) ||
                    parameters.ValueKind != JsonValueKind.Object)
                {
                    AddInvalid(
                        issues,
                        applicationPath + ".parameters",
                        "closed parameter object",
                        RawProperty(application, "parameters"));
                }
                else if (definitionRef.Length > 0 &&
                         definitionsByRef.TryGetValue(definitionRef, out var definition))
                {
                    applicationRoots.Add(new DetachedEffectRootCandidate(
                        definitionRef,
                        applicationPath,
                        Slots: null));
                    var definitionNode = JsonNode.Parse(definition.GetRawText()) as JsonObject;
                    var parameterNode = JsonNode.Parse(parameters.GetRawText()) as JsonObject;
                    if (definitionNode is not null && parameterNode is not null)
                    {
                        foreach (var parameterIssue in
                                 EffectSourceAuthority.ValidateDefinitionParameters(
                                     definitionNode,
                                     parameterNode))
                        {
                            var suffix = parameterIssue.FilePath.StartsWith(
                                "parameters",
                                StringComparison.Ordinal)
                                ? parameterIssue.FilePath["parameters".Length..]
                                : string.Empty;
                            AddIssue(
                                issues,
                                applicationPath + ".parameters" + suffix,
                                parameterIssue.Code ?? "effect_source_parameter_out_of_bounds",
                                parameterIssue.Expected ??
                                "parameters inside source-owned definition bounds",
                                parameterIssue.Actual ?? "invalid");
                        }
                    }
                }
            }
            applicationIndex++;
        }
        ValidateDetachedLegacyGraph(
            graphDefinitions,
            applicationRoots,
            draftPath + ".definitions",
            ownerTargetKind,
            issues);
    }

    private static IReadOnlyList<DetachedEffectDefinitionCandidate> ValidateDetachedEffectDefinitions(
        IReadOnlyList<DetachedEffectDefinitionCandidate> definitions,
        string collectionPath,
        string realm,
        bool bindProposalWoundMarkers,
        List<ValidationIssue> issues)
    {
        if (definitions.Count == 0)
            return Array.Empty<DetachedEffectDefinitionCandidate>();

        var exactDefinitionKeys = new HashSet<string>(StringComparer.Ordinal);
        var confusableDefinitionKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var candidate in definitions)
        {
            var definitionKey = ValidateIdentifier(
                candidate.Definition,
                "definitionKey",
                candidate.DefinitionPath,
                issues);
            if (definitionKey.Length > 0 &&
                (!exactDefinitionKeys.Add(definitionKey) ||
                 !confusableDefinitionKeys.Add(ConfusableKey(definitionKey))))
            {
                AddInvalid(
                    issues,
                    candidate.DefinitionPath + ".definitionKey",
                    "exact and Unicode-confusable unique definitionKey",
                    definitionKey);
            }
            if (!candidate.Definition.TryGetProperty("links", out var links) ||
                links.ValueKind != JsonValueKind.Array ||
                links.GetArrayLength() != 0)
            {
                AddInvalid(
                    issues,
                    candidate.DefinitionPath + ".links",
                    "exact empty links array; client binds the wound source",
                    RawProperty(candidate.Definition, "links"));
            }
        }

        const string commonPath = "$mortalWoundDetachedDefinitions";
        var commonDefinitions = definitions.Select(candidate =>
            PrepareDetachedDefinitionForCommonValidation(
                candidate,
                bindProposalWoundMarkers,
                issues)).ToArray();
        var graphDefinitions = definitions.Select((candidate, index) =>
            new DetachedEffectDefinitionCandidate(
                candidate.DefinitionRef,
                candidate.DefinitionPath,
                commonDefinitions[index])).ToArray();
        using var document = JsonDocument.Parse(
            "[" + string.Join(",", commonDefinitions.Select(static definition =>
                definition.GetRawText())) + "]");
        foreach (var commonIssue in EffectSourceDefinitionContract.ValidateArray(
                     document.RootElement,
                     commonPath,
                     realm))
        {
            var rebased = RebaseDetachedDefinitionIssuePath(
                commonIssue.FilePath,
                commonPath,
                collectionPath,
                definitions);
            var directSuffix = DirectDefinitionSuffix(
                commonIssue.FilePath,
                commonPath);
            if (directSuffix is ".links" or ".definitionKey")
            {
                continue;
            }
            AddInvalid(
                issues,
                rebased,
                commonIssue.Expected ?? "valid detached effect definition",
                commonIssue.Actual ?? commonIssue.Code ?? "invalid");
        }
        return graphDefinitions;
    }

    private static string RebaseDetachedDefinitionIssuePath(
        string issuePath,
        string commonPath,
        string collectionPath,
        IReadOnlyList<DetachedEffectDefinitionCandidate> definitions)
    {
        if (!issuePath.StartsWith(commonPath + "[", StringComparison.Ordinal))
            return collectionPath;
        var indexStart = commonPath.Length + 1;
        var indexEnd = issuePath.IndexOf(']', indexStart);
        if (indexEnd < 0 ||
            !int.TryParse(
                issuePath.AsSpan(indexStart, indexEnd - indexStart),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var index) ||
            index < 0 ||
            index >= definitions.Count)
        {
            return collectionPath;
        }
        return definitions[index].DefinitionPath + issuePath[(indexEnd + 1)..];
    }

    private static string? DirectDefinitionSuffix(string issuePath, string commonPath)
    {
        if (!issuePath.StartsWith(commonPath + "[", StringComparison.Ordinal))
            return null;
        var indexEnd = issuePath.IndexOf(']', commonPath.Length + 1);
        return indexEnd < 0 ? null : issuePath[(indexEnd + 1)..];
    }

    private static string? ReadDeteriorationPolicyRef(JsonElement? policy)
    {
        if (policy is not { ValueKind: JsonValueKind.Object } value ||
            !value.TryGetProperty("policyRef", out var policyRef) ||
            policyRef.ValueKind != JsonValueKind.String)
        {
            return null;
        }
        var text = policyRef.GetString();
        return ResourceMaterializationContract.IsExactIdentifier(text) ? text : null;
    }

    private static bool IsQuantityRequirement(JsonElement value)
    {
        var kind = ReadString(value, "kind");
        return kind is "item_quantity" or "resource_quantity";
    }

    private static int CategoryRank(string category) => category switch
    {
        "success" => 0,
        "partial_success" => 1,
        "failed_attempt" => 2,
        _ => int.MaxValue
    };

    private static bool ValidateObject(
        JsonElement value,
        string path,
        IReadOnlySet<string> fields,
        List<ValidationIssue> issues)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            AddInvalid(issues, path, "closed object", value.ValueKind.ToString());
            return false;
        }
        foreach (var property in value.EnumerateObject())
        {
            if (!fields.Contains(property.Name))
            {
                AddIssue(
                    issues,
                    path + "." + property.Name,
                    "wound_materialization_unknown_field",
                    "registered current-schema field",
                    property.Name);
            }
        }
        foreach (var field in fields)
        {
            if (!value.TryGetProperty(field, out _))
                AddMissing(issues, path + "." + field);
        }
        return true;
    }

    private static string ValidateIdentifier(
        JsonElement parent,
        string field,
        string path,
        List<ValidationIssue> issues)
    {
        if (!parent.TryGetProperty(field, out var value))
        {
            AddMissing(issues, path + "." + field);
            return string.Empty;
        }
        var identifier = value.ValueKind == JsonValueKind.String ? value.GetString() : null;
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

    private static void ValidateReadableText(
        JsonElement parent,
        string field,
        string path,
        List<ValidationIssue> issues)
    {
        if (!parent.TryGetProperty(field, out var value))
        {
            AddMissing(issues, path + "." + field);
            return;
        }
        var text = value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;
        if (text.Length is > 0 and <= WoundMaterializationContract.MaxReadableTextLength &&
            !string.IsNullOrWhiteSpace(text) &&
            string.Equals(text, text.Trim(), StringComparison.Ordinal))
            return;
        AddInvalid(issues, path + "." + field, "non-empty trimmed readable text", value.GetRawText());
    }

    private static string ValidateClosedString(
        JsonElement parent,
        string field,
        string path,
        IReadOnlySet<string> allowed,
        List<ValidationIssue> issues)
    {
        var value = ReadString(parent, field);
        if (allowed.Contains(value))
            return value;
        if (!parent.TryGetProperty(field, out _))
            AddMissing(issues, path + "." + field);
        else
            AddInvalid(issues, path + "." + field, string.Join(" | ", allowed), RawProperty(parent, field));
        return value;
    }

    private static void ValidateExactString(
        JsonElement parent,
        string field,
        string path,
        string expected,
        List<ValidationIssue> issues)
    {
        var value = ReadString(parent, field);
        if (!string.Equals(value, expected, StringComparison.Ordinal))
        {
            if (!parent.TryGetProperty(field, out _))
                AddMissing(issues, path + "." + field);
            else
                AddInvalid(issues, path + "." + field, expected, RawProperty(parent, field));
        }
    }

    private static void ValidateClosedStringArray(
        JsonElement parent,
        string field,
        string path,
        IReadOnlySet<string> allowed,
        IReadOnlyList<string>? exactSequence,
        List<ValidationIssue> issues)
    {
        if (!TryGetArray(parent, field, out var array))
        {
            AddMissing(issues, path + "." + field);
            return;
        }
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var values = new List<string>();
        var index = 0;
        foreach (var item in array.EnumerateArray())
        {
            var itemPath = $"{path}.{field}[{index++}]";
            var value = item.ValueKind == JsonValueKind.String ? item.GetString() ?? string.Empty : string.Empty;
            if (!allowed.Contains(value))
                AddInvalid(issues, itemPath, string.Join(" | ", allowed), item.GetRawText());
            else if (!seen.Add(value))
                AddInvalid(issues, itemPath, "exact-unique category", value);
            values.Add(value);
        }
        if (exactSequence is not null && !values.SequenceEqual(exactSequence, StringComparer.Ordinal))
            AddInvalid(issues, path + "." + field, "exact ordered sequence: " + string.Join(", ", exactSequence), string.Join(", ", values));
    }

    private static int? ValidatePositiveInt32(
        JsonElement parent,
        string field,
        string path,
        List<ValidationIssue> issues) =>
        ReadInt32(parent, field, path, 1, int.MaxValue, issues);

    private static int? ValidateInt32(
        JsonElement parent,
        string field,
        string path,
        int minimum,
        int maximum,
        List<ValidationIssue> issues) =>
        ReadInt32(parent, field, path, minimum, maximum, issues);

    private static int? ReadInt32(
        JsonElement parent,
        string field,
        string path,
        int minimum,
        int maximum,
        List<ValidationIssue> issues)
    {
        if (!parent.TryGetProperty(field, out var value))
        {
            AddMissing(issues, path + "." + field);
            return null;
        }
        if (value.ValueKind == JsonValueKind.Number &&
            value.TryGetInt32(out var number) &&
            number >= minimum && number <= maximum &&
            value.GetRawText() == number.ToString(CultureInfo.InvariantCulture))
            return number;
        AddInvalid(issues, path + "." + field, $"exact integer {minimum}..{maximum}", value.GetRawText());
        return null;
    }

    private static long? ReadInt64(
        JsonElement parent,
        string field,
        string path,
        long minimum,
        long maximum,
        List<ValidationIssue> issues)
    {
        if (!parent.TryGetProperty(field, out var value))
        {
            AddMissing(issues, path + "." + field);
            return null;
        }
        if (value.ValueKind == JsonValueKind.Number &&
            value.TryGetInt64(out var number) &&
            number >= minimum && number <= maximum &&
            value.GetRawText() == number.ToString(CultureInfo.InvariantCulture))
            return number;
        AddInvalid(issues, path + "." + field, $"exact integer {minimum}..{maximum}", value.GetRawText());
        return null;
    }

    private static long? ReadNullableInt64(
        JsonElement parent,
        string field,
        string path,
        List<ValidationIssue> issues)
    {
        if (!parent.TryGetProperty(field, out var value))
        {
            AddMissing(issues, path + "." + field);
            return null;
        }
        if (value.ValueKind == JsonValueKind.Null)
            return null;
        if (value.ValueKind == JsonValueKind.Number &&
            value.TryGetInt64(out var number) &&
            value.GetRawText() == number.ToString(CultureInfo.InvariantCulture))
            return number;
        AddInvalid(issues, path + "." + field, "null or exact signed 64-bit integer", value.GetRawText());
        return null;
    }

    private static int? ReadNullablePositiveInt32(
        JsonElement parent,
        string field,
        string path,
        List<ValidationIssue> issues)
    {
        if (!parent.TryGetProperty(field, out var value))
        {
            AddMissing(issues, path + "." + field);
            return null;
        }
        if (value.ValueKind == JsonValueKind.Null)
            return null;
        if (value.ValueKind == JsonValueKind.Number &&
            value.TryGetInt32(out var number) &&
            number > 0 &&
            value.GetRawText() == number.ToString(CultureInfo.InvariantCulture))
            return number;
        AddInvalid(issues, path + "." + field, "null or positive exact integer", value.GetRawText());
        return null;
    }

    private static bool TryGetBoolean(JsonElement parent, string field, out bool value)
    {
        value = false;
        if (!parent.TryGetProperty(field, out var element) ||
            element.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            return false;
        }
        value = element.GetBoolean();
        return true;
    }

    private static bool TryGetArray(JsonElement parent, string field, out JsonElement array)
    {
        if (parent.ValueKind == JsonValueKind.Object &&
            parent.TryGetProperty(field, out array) &&
            array.ValueKind == JsonValueKind.Array)
            return true;
        array = default;
        return false;
    }

    private static string ReadString(JsonElement parent, string field) =>
        parent.ValueKind == JsonValueKind.Object &&
        parent.TryGetProperty(field, out var value) &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static string RawProperty(JsonElement parent, string field) =>
        parent.ValueKind == JsonValueKind.Object && parent.TryGetProperty(field, out var value)
            ? value.GetRawText()
            : "missing";

    private static string ConfusableKey(string value) =>
        ExactIdentifierConfusableKey.Build(value);

    private static void AddMissing(List<ValidationIssue> issues, string path) =>
        AddIssue(
            issues,
            path,
            "wound_materialization_missing_field",
            "required final version-1 field",
            "missing");

    private static void AddInvalid(
        List<ValidationIssue> issues,
        string path,
        string expected,
        string actual) =>
        AddIssue(
            issues,
            path,
            "wound_materialization_invalid_field",
            expected,
            actual);

    private static void AddLimit(
        List<ValidationIssue> issues,
        string path,
        int maximum,
        int actual) =>
        AddIssue(
            issues,
            path,
            "wound_materialization_limit_exceeded",
            $"at most {maximum} entries",
            actual.ToString(CultureInfo.InvariantCulture));

    private static void AddIssue(
        ICollection<ValidationIssue> issues,
        string path,
        string code,
        string expected,
        string actual) =>
        issues.Add(new ValidationIssue(
            path,
            IssueSeverity.Error,
            "Mortal wound treatment violates the complete current materialization contract.",
            code: code,
            actor: "Client",
            section: "wound_materialization",
            expected: expected,
            actual: actual,
            repairHint: "Resubmit one complete closed version-1 diagnosis or treatment route."));

    private static IReadOnlySet<string> Set(params string[] values) =>
        new HashSet<string>(values, StringComparer.Ordinal);

    private sealed record ProcedureBand(
        string Path,
        long? Minimum,
        long? Maximum,
        string Category);

    private sealed record DetachedEffectDefinitionCandidate(
        string DefinitionRef,
        string DefinitionPath,
        JsonElement Definition);

    private sealed record DetachedEffectRootCandidate(
        string DefinitionRef,
        string RootPath,
        IReadOnlyList<DetachedEffectSlotCandidate>? Slots);

    private sealed record DetachedEffectSlotCandidate(
        string ProfileKey,
        string Path);

    private sealed record ValidationContext(
        string Realm,
        WoundValidationContext? Wound);

    private sealed record WoundValidationContext(
        string OwnerTargetKind,
        int SeverityRank,
        IReadOnlyList<WoundComplication> CurrentComplications,
        string? CurrentPolicyRef);

    private enum ComplicationValidationUse
    {
        Ordinary,
        Interruption,
        DeteriorationPolicy
    }

    private sealed class ResultSummaryBuilder
    {
        internal bool HasPositive { get; private set; }
        internal bool HasAdverse { get; private set; }
        internal bool HasNoImprovement { get; private set; }
        internal int? FirstPositiveIndex { get; private set; }
        internal string? FirstPositiveKind { get; private set; }
        internal int? FirstNonPositiveIndex { get; private set; }
        internal string? FirstNonPositiveKind { get; private set; }
        internal int HealCount { get; private set; }
        internal int? HealIndex { get; private set; }
        internal int SeverityReduction { get; private set; }

        internal void Add(string kind, int index, JsonElement operation)
        {
            var positive = kind is "stabilize" or "add_recovery" or "reduce_severity" or
                "remove_complication" or "heal";
            var adverse = kind is "add_complication" or "apply_deterioration";
            if (positive)
            {
                HasPositive = true;
                FirstPositiveIndex ??= index;
                FirstPositiveKind ??= kind;
            }
            if (adverse)
                HasAdverse = true;
            if (kind == "no_improvement")
                HasNoImprovement = true;
            if (!positive)
            {
                FirstNonPositiveIndex ??= index;
                FirstNonPositiveKind ??= kind;
            }
            if (kind == "heal")
            {
                HealCount++;
                HealIndex ??= index;
            }
            if (kind == "reduce_severity" &&
                operation.TryGetProperty("steps", out var steps) &&
                steps.TryGetInt32(out var reduction))
            {
                try
                {
                    SeverityReduction = checked(SeverityReduction + reduction);
                }
                catch (OverflowException)
                {
                    SeverityReduction = int.MaxValue;
                }
            }
        }

        internal ResultSummary Build() => new(
            HasPositive,
            HasAdverse,
            HasNoImprovement,
            FirstPositiveIndex,
            FirstPositiveKind,
            FirstNonPositiveIndex,
            FirstNonPositiveKind,
            HealCount,
            HealIndex,
            SeverityReduction);
    }

    private sealed record ResultSummary(
        bool HasPositive,
        bool HasAdverse,
        bool HasNoImprovement,
        int? FirstPositiveIndex,
        string? FirstPositiveKind,
        int? FirstNonPositiveIndex,
        string? FirstNonPositiveKind,
        int HealCount,
        int? HealIndex,
        int SeverityReduction)
    {
        internal static ResultSummary Empty { get; } = new(
            false, false, false, null, null, null, null, 0, null, 0);
    }
}
