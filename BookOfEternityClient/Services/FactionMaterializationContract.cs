using System.Text.Json;

namespace BookOfEternityClient.Services;

internal enum FactionMaterializationFamily
{
    Mortal,
    Shining
}

internal sealed record FactionMaterializationEvidence(
    string FactionType,
    string FactionId,
    IReadOnlyDictionary<string, bool> SectionHasContent,
    IReadOnlyDictionary<string, bool> CapabilityEvidence,
    IReadOnlyDictionary<string, bool> SectionHasCanonicalEmptySurface);

internal static class FactionMaterializationContract
{
    internal const string PropertyName = "materialization";
    internal const int SchemaVersion = 1;

    private static readonly HashSet<string> EnvelopeFields = new(StringComparer.Ordinal)
    {
        "schemaVersion", "materializationId", "factionType", "factionId", "materializedAtTurn", "state", "capabilities", "sections"
    };

    private static readonly HashSet<string> DispositionFields = new(StringComparer.Ordinal) { "state", "reason" };

    private static readonly HashSet<string> MortalCapabilities = new(StringComparer.Ordinal)
    {
        "hasFormalHierarchy", "usesFactionResources", "maintainsRelations", "runsProjects", "holdsTerritoryOrInfluence", "supportsPlayerMembership", "usesCustomMechanics"
    };

    private static readonly HashSet<string> MortalSections = new(StringComparer.Ordinal)
    {
        "hierarchy", "resources", "relations", "projects", "territoryAndInfluence", "playerMembership", "customStates"
    };

    private static readonly HashSet<string> ShiningCapabilities = new(StringComparer.Ordinal)
    {
        "runsProjects", "holdsTerritorialInfluence", "usesResourceLedger", "hasResidentAffiliations", "canTrade", "hasLeadershipHistory", "usesStoryState"
    };

    private static readonly HashSet<string> ShiningSections = new(StringComparer.Ordinal)
    {
        "projects", "territorialInfluence", "resourceLedger", "residentAffiliations", "trade", "leadershipHistory", "storyState"
    };

    private static readonly IReadOnlyDictionary<string, string> MortalCapabilitySections =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["hasFormalHierarchy"] = "hierarchy",
            ["usesFactionResources"] = "resources",
            ["maintainsRelations"] = "relations",
            ["runsProjects"] = "projects",
            ["holdsTerritoryOrInfluence"] = "territoryAndInfluence",
            ["supportsPlayerMembership"] = "playerMembership",
            ["usesCustomMechanics"] = "customStates"
        };

    private static readonly IReadOnlyDictionary<string, string> ShiningDirectCapabilitySections =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["runsProjects"] = "projects",
            ["holdsTerritorialInfluence"] = "territorialInfluence",
            ["usesResourceLedger"] = "resourceLedger",
            ["hasResidentAffiliations"] = "residentAffiliations",
            ["hasLeadershipHistory"] = "leadershipHistory",
            ["usesStoryState"] = "storyState"
        };

    internal static IReadOnlyList<ValidationIssue> Validate(
        JsonElement faction,
        string context,
        FactionMaterializationFamily family,
        FactionMaterializationEvidence evidence,
        bool requireEnvelope,
        bool deferEvidenceConsistency = false)
    {
        var issues = new List<ValidationIssue>();
        if (faction.ValueKind != JsonValueKind.Object)
        {
            issues.Add(Invalid(context, evidence, "object faction carrier", faction.ValueKind.ToString()));
            return issues;
        }

        if (!TryGetCanonicalFactionType(family, out var canonicalFactionType))
        {
            issues.Add(Invalid($"{context}.{PropertyName}.factionType", evidence, "Mortal or Shining faction materialization family", family.ToString()));
            return issues;
        }

        if (!string.Equals(evidence.FactionType, canonicalFactionType, StringComparison.Ordinal))
        {
            issues.Add(Invalid($"{context}.factionType", evidence, canonicalFactionType, evidence.FactionType));
        }

        ValidateDuplicateEnvelopeProperty(faction, context, evidence, issues);
        if (!faction.TryGetProperty(PropertyName, out var envelope))
        {
            if (requireEnvelope)
                issues.Add(Missing(context, evidence));
            return issues;
        }

        if (envelope.ValueKind != JsonValueKind.Object)
        {
            issues.Add(Invalid($"{context}.{PropertyName}", evidence, "object", envelope.ValueKind.ToString()));
            return issues;
        }

        ValidateExactFields(envelope, EnvelopeFields, $"{context}.{PropertyName}", evidence, issues);
        ValidateScalars(envelope, context, evidence, canonicalFactionType, issues);
        ValidateCapabilities(envelope, context, family, evidence, issues, deferEvidenceConsistency);
        ValidateSections(envelope, context, family, evidence, issues, deferEvidenceConsistency);
        return issues;
    }

    internal static IReadOnlyList<ValidationIssue> ValidateUniqueMaterializationIds(
        IReadOnlyList<(JsonElement Faction, string Context, string FactionType, string FactionId)> factions)
    {
        var issues = new List<ValidationIssue>();
        var firstContextById = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var item in factions)
        {
            if (item.Faction.ValueKind != JsonValueKind.Object ||
                !item.Faction.TryGetProperty(PropertyName, out var envelope) ||
                envelope.ValueKind != JsonValueKind.Object ||
                !envelope.TryGetProperty("materializationId", out var idNode) ||
                idNode.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(idNode.GetString()))
            {
                continue;
            }

            var id = idNode.GetString()!;
            if (firstContextById.TryAdd(id, item.Context))
                continue;

            issues.Add(new ValidationIssue(
                $"{item.Context}.{PropertyName}.materializationId",
                IssueSeverity.Error,
                "Faction materializationId must be unique.",
                code: "faction_materialization_duplicate_id",
                actor: $"{item.FactionType}:{item.FactionId}",
                section: "FactionMaterialization",
                expected: $"unique value; first declared at {firstContextById[id]}",
                actual: id,
                repairHint: "Assign a new stable ID only to the unaccepted materialization; preserve accepted historical receipts."));
        }

        return issues;
    }

    private static void ValidateDuplicateEnvelopeProperty(
        JsonElement faction,
        string context,
        FactionMaterializationEvidence evidence,
        List<ValidationIssue> issues)
    {
        if (faction.EnumerateObject().Count(property => string.Equals(property.Name, PropertyName, StringComparison.Ordinal)) <= 1)
            return;

        issues.Add(Issue(
            $"{context}.{PropertyName}",
            "faction_materialization_duplicate_property",
            "Faction must contain exactly one materialization envelope.",
            evidence,
            expected: "exactly one materialization property",
            actual: "duplicate materialization properties"));
    }

    private static void ValidateScalars(
        JsonElement envelope,
        string context,
        FactionMaterializationEvidence evidence,
        string canonicalFactionType,
        List<ValidationIssue> issues)
    {
        var envelopeContext = $"{context}.{PropertyName}";
        if (!envelope.TryGetProperty("schemaVersion", out var versionNode) ||
            versionNode.ValueKind != JsonValueKind.Number ||
            !versionNode.TryGetInt32(out var version) || version != SchemaVersion)
        {
            issues.Add(Invalid($"{envelopeContext}.schemaVersion", evidence, SchemaVersion.ToString(), Describe(envelope, "schemaVersion")));
        }

        ValidateNonEmptyString(envelope, "materializationId", envelopeContext, evidence, issues);
        var factionType = ReadNonEmptyString(envelope, "factionType");
        var factionId = ReadNonEmptyString(envelope, "factionId");
        if (!string.Equals(factionType, canonicalFactionType, StringComparison.Ordinal))
            issues.Add(Invalid($"{envelopeContext}.factionType", evidence, canonicalFactionType, factionType ?? "missing"));

        if (!string.Equals(factionType, evidence.FactionType, StringComparison.Ordinal) ||
            !string.Equals(factionId, evidence.FactionId, StringComparison.Ordinal))
        {
            issues.Add(Issue(
                envelopeContext,
                "faction_materialization_identity_mismatch",
                "Faction materialization is bound to a different faction identity.",
                evidence,
                expected: $"{evidence.FactionType}:{evidence.FactionId}",
                actual: $"{factionType ?? "missing"}:{factionId ?? "missing"}"));
        }

        if (!envelope.TryGetProperty("materializedAtTurn", out var turnNode) ||
            turnNode.ValueKind != JsonValueKind.Number || !turnNode.TryGetInt32(out var turn) || turn < 0)
        {
            issues.Add(Invalid($"{envelopeContext}.materializedAtTurn", evidence, "integer >= 0", Describe(envelope, "materializedAtTurn")));
        }

        if (!string.Equals(ReadNonEmptyString(envelope, "state"), "complete", StringComparison.Ordinal))
            issues.Add(Invalid($"{envelopeContext}.state", evidence, "complete", Describe(envelope, "state")));
    }

    private static void ValidateCapabilities(
        JsonElement envelope,
        string context,
        FactionMaterializationFamily family,
        FactionMaterializationEvidence evidence,
        List<ValidationIssue> issues,
        bool deferEvidenceConsistency)
    {
        var capabilityContext = $"{context}.{PropertyName}.capabilities";
        var requiredCapabilities = family == FactionMaterializationFamily.Mortal ? MortalCapabilities : ShiningCapabilities;
        if (!envelope.TryGetProperty("capabilities", out var capabilities) || capabilities.ValueKind != JsonValueKind.Object)
        {
            issues.Add(Invalid(capabilityContext, evidence, "object with exact capability flags", Describe(envelope, "capabilities")));
            return;
        }

        ValidateExactFields(capabilities, requiredCapabilities, capabilityContext, evidence, issues);
        foreach (var capability in requiredCapabilities)
        {
            if (!capabilities.TryGetProperty(capability, out var declared) ||
                declared.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                issues.Add(Invalid($"{capabilityContext}.{capability}", evidence, "true or false", Describe(capabilities, capability)));
            }
        }

        if (deferEvidenceConsistency)
            return;

        var mappings = family == FactionMaterializationFamily.Mortal
            ? MortalCapabilitySections
            : ShiningDirectCapabilitySections;
        foreach (var (capability, section) in mappings)
        {
            ValidateCapabilityEvidence(capabilities, capabilityContext, capability, evidence, issues);
            ValidateCapabilitySectionConsistency(capabilities, capabilityContext, capability, section, evidence, issues);
        }

        if (family == FactionMaterializationFamily.Shining)
            ValidateCapabilityEvidence(capabilities, capabilityContext, "canTrade", evidence, issues);
    }

    private static void ValidateCapabilityEvidence(
        JsonElement capabilities,
        string capabilityContext,
        string capability,
        FactionMaterializationEvidence evidence,
        List<ValidationIssue> issues)
    {
        if (!capabilities.TryGetProperty(capability, out var declared) ||
            declared.ValueKind is not (JsonValueKind.True or JsonValueKind.False) ||
            !evidence.CapabilityEvidence.TryGetValue(capability, out var actual) ||
            declared.GetBoolean() == actual)
        {
            return;
        }

        issues.Add(Issue(
            $"{capabilityContext}.{capability}",
            "faction_materialization_capability_mismatch",
            $"Declared capability {capability} contradicts faction evidence.",
            evidence,
            section: capability,
            expected: actual.ToString(),
            actual: declared.GetBoolean().ToString()));
    }

    private static void ValidateCapabilitySectionConsistency(
        JsonElement capabilities,
        string capabilityContext,
        string capability,
        string section,
        FactionMaterializationEvidence evidence,
        List<ValidationIssue> issues)
    {
        if (!capabilities.TryGetProperty(capability, out var declared) ||
            declared.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            return;
        }

        var hasSectionContent = evidence.SectionHasContent.TryGetValue(section, out var content) && content;
        if (declared.GetBoolean() == hasSectionContent)
            return;

        issues.Add(Issue(
            $"{capabilityContext}.{capability}",
            "faction_materialization_capability_mismatch",
            $"Declared capability {capability} contradicts mapped section {section} evidence.",
            evidence,
            section: section,
            expected: hasSectionContent.ToString(),
            actual: declared.GetBoolean().ToString()));
    }

    private static void ValidateSections(
        JsonElement envelope,
        string context,
        FactionMaterializationFamily family,
        FactionMaterializationEvidence evidence,
        List<ValidationIssue> issues,
        bool deferEvidenceConsistency)
    {
        var sectionsContext = $"{context}.{PropertyName}.sections";
        var requiredSections = family == FactionMaterializationFamily.Mortal ? MortalSections : ShiningSections;
        if (!envelope.TryGetProperty("sections", out var sections) || sections.ValueKind != JsonValueKind.Object)
        {
            issues.Add(Invalid(sectionsContext, evidence, "object with exact faction sections", Describe(envelope, "sections")));
            return;
        }

        ValidateExactFields(sections, requiredSections, sectionsContext, evidence, issues);
        foreach (var section in requiredSections)
        {
            if (!sections.TryGetProperty(section, out var disposition))
            {
                issues.Add(Issue(
                    $"{sectionsContext}.{section}",
                    "faction_materialization_section_missing",
                    $"Faction materialization does not explain section {section}.",
                    evidence,
                    section: section,
                    expected: "populated or empty_by_design with reason",
                    actual: "missing"));
                continue;
            }

            ValidateDisposition(disposition, sectionsContext, section, evidence, issues, deferEvidenceConsistency);
        }
    }

    private static void ValidateDisposition(
        JsonElement disposition,
        string sectionsContext,
        string section,
        FactionMaterializationEvidence evidence,
        List<ValidationIssue> issues,
        bool deferEvidenceConsistency)
    {
        var sectionContext = $"{sectionsContext}.{section}";
        if (disposition.ValueKind != JsonValueKind.Object)
        {
            issues.Add(Invalid(sectionContext, evidence, "disposition object", disposition.ValueKind.ToString()));
            return;
        }

        var state = ReadNonEmptyString(disposition, "state");
        var populated = string.Equals(state, "populated", StringComparison.Ordinal);
        var emptyByDesign = string.Equals(state, "empty_by_design", StringComparison.Ordinal);
        ValidateExactFields(disposition, emptyByDesign ? DispositionFields : new HashSet<string>(StringComparer.Ordinal) { "state" }, sectionContext, evidence, issues);

        if (!populated && !emptyByDesign)
        {
            issues.Add(Invalid($"{sectionContext}.state", evidence, "populated or empty_by_design", state ?? "missing"));
            return;
        }

        if (emptyByDesign && string.IsNullOrWhiteSpace(ReadNonEmptyString(disposition, "reason")))
            issues.Add(Invalid($"{sectionContext}.reason", evidence, "non-empty in-world reason", "missing or blank"));

        if (deferEvidenceConsistency)
            return;

        var hasContent = evidence.SectionHasContent.TryGetValue(section, out var content) && content;
        var hasCanonicalEmptySurface = evidence.SectionHasCanonicalEmptySurface.TryGetValue(section, out var emptySurface) && emptySurface;
        if ((emptyByDesign && (!hasCanonicalEmptySurface || hasContent)) || (populated && !hasContent))
        {
            issues.Add(Issue(
                sectionContext,
                "faction_materialization_disposition_mismatch",
                $"Disposition for section {section} contradicts faction evidence.",
                evidence,
                section: section,
                expected: hasContent ? "populated" : "empty_by_design with canonical empty surface and reason",
                actual: state));
        }
    }

    private static void ValidateExactFields(
        JsonElement value,
        HashSet<string> allowedFields,
        string context,
        FactionMaterializationEvidence evidence,
        List<ValidationIssue> issues)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in value.EnumerateObject())
        {
            if (!seen.Add(property.Name))
            {
                issues.Add(Issue(
                    $"{context}.{property.Name}",
                    "faction_materialization_duplicate_property",
                    "Closed faction materialization contract forbids duplicate JSON properties.",
                    evidence,
                    expected: "each property exactly once",
                    actual: property.Name));
            }

            if (!allowedFields.Contains(property.Name))
                issues.Add(Invalid($"{context}.{property.Name}", evidence, string.Join(", ", allowedFields), property.Name));
        }
    }

    private static void ValidateNonEmptyString(
        JsonElement value,
        string propertyName,
        string context,
        FactionMaterializationEvidence evidence,
        List<ValidationIssue> issues)
    {
        if (ReadNonEmptyString(value, propertyName) == null)
            issues.Add(Invalid($"{context}.{propertyName}", evidence, "non-empty string", Describe(value, propertyName)));
    }

    private static string? ReadNonEmptyString(JsonElement value, string propertyName)
    {
        if (!value.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
            return null;
        var text = property.GetString();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static bool TryGetCanonicalFactionType(FactionMaterializationFamily family, out string factionType)
    {
        switch (family)
        {
            case FactionMaterializationFamily.Mortal:
                factionType = "mortal_faction";
                return true;
            case FactionMaterializationFamily.Shining:
                factionType = "shining_faction";
                return true;
            default:
                factionType = string.Empty;
                return false;
        }
    }

    private static string Describe(JsonElement value, string propertyName) =>
        value.TryGetProperty(propertyName, out var property) ? property.ToString() : "missing";

    private static ValidationIssue Missing(string context, FactionMaterializationEvidence evidence) =>
        Issue(
            $"{context}.{PropertyName}",
            "faction_materialization_missing",
            "Faction requires a closed materialization envelope.",
            evidence,
            expected: "complete faction-bound materialization envelope",
            actual: "missing");

    private static ValidationIssue Invalid(
        string path,
        FactionMaterializationEvidence evidence,
        string expected,
        string actual) =>
        Issue(
            path,
            "faction_materialization_invalid",
            "Faction materialization envelope is invalid.",
            evidence,
            expected: expected,
            actual: actual);

    private static ValidationIssue Issue(
        string path,
        string code,
        string message,
        FactionMaterializationEvidence evidence,
        string? section = null,
        string? expected = null,
        string? actual = null) =>
        new(
            path,
            IssueSeverity.Error,
            message,
            code: code,
            actor: $"{evidence.FactionType}:{evidence.FactionId}",
            section: section ?? "FactionMaterialization",
            expected: expected,
            actual: actual,
            repairHint: "Repair only this faction's materialization envelope; preserve accepted historical receipts.");
}
