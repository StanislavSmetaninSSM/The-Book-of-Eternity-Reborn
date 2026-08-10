using System.Text.Json;
using System.Text.RegularExpressions;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    private const string MortalFactionStructurePath =
        "game_state/factions/faction_structure.json";
    private const string MortalFactionResourcesPath =
        "game_state/factions/faction_resources.json";
    private const string MortalFactionProjectsPath =
        "game_state/factions/faction_projects.json";
    private const string MortalFactionCustomPath =
        "game_state/factions/faction_custom.json";
    private const string MortalFactionChroniclesPath =
        "game_state/factions/faction_chronicles.json";
    private const string MortalNpcCorePath =
        "game_state/npcs/npc_core.json";

    private static readonly HashSet<string> MortalLeadershipStates =
        new(StringComparer.Ordinal)
        {
            "headed",
            "collective",
            "vacant"
        };

    private static readonly Regex MortalFactionColorRegex =
        new(
            "^#[0-9A-Fa-f]{6}$",
            RegexOptions.Compiled |
            RegexOptions.CultureInvariant);

    private static readonly Regex MortalChronicleEntryRegex =
        new(
            "^#[0-9]+\\s+-\\s+\\S",
            RegexOptions.Compiled |
            RegexOptions.CultureInvariant);

    private static readonly HashSet<string> MortalMemoryFields =
        new(StringComparer.Ordinal)
        {
            "summary",
            "lastUpdatedTurn",
            "enduringFacts",
            "openThreads"
        };

    private static readonly HashSet<string> MortalGovernanceFields =
        new(StringComparer.Ordinal)
        {
            "model",
            "decisionProcess"
        };

    private static readonly HashSet<string> MortalLeadershipFields =
        new(StringComparer.Ordinal)
        {
            "leadershipState",
            "summary",
            "leaderNpcIds"
        };

    private async Task ValidateAcceptedTurnMortalFactionMaterializationCompletenessAsync(
        bool rawBeforeNormalization,
        List<ValidationIssue> issues)
    {
        var currentRoot = await ReadMortalMaterializationRootAsync(
            MortalFactionMaterializationPath);
        if (currentRoot == null)
            return;

        var canonical = ReadMortalFactionTargets(
            currentRoot.Value,
            "factions",
            useInitialId: false);
        var full = rawBeforeNormalization
            ? ReadMortalFactionTargets(
                currentRoot.Value,
                "factionDataChanges",
                useInitialId: true)
            : new Dictionary<string, MortalFactionMaterializationTarget>(
                StringComparer.Ordinal);
        var preTurn = await ReadPreTurnMortalFactionTargetsAsync();
        var snapshotLookup = await LoadValidatedPendingTurnSnapshotLookupAsync();
        var hasUsablePreTurnAuthority =
            snapshotLookup.Status ==
            ValidatedPendingTurnSnapshotStatus.Usable;

        var effectiveFactionIds = new HashSet<string>(
            canonical.Keys,
            StringComparer.Ordinal);
        effectiveFactionIds.UnionWith(full.Keys);
        effectiveFactionIds.UnionWith(preTurn.Keys);

        var locationAuthority = await ReadMortalLocationAuthorityAsync();
        var npcAuthority = await ReadMortalNpcAuthorityAsync();
        ValidateMortalLocationFactionReferences(
            locationAuthority,
            effectiveFactionIds,
            issues);
        ValidateMortalNpcFactionAffiliations(
            npcAuthority,
            effectiveFactionIds,
            issues);

        var currentChronicleIds = await ReadMortalChronicleFactionIdsAsync(
            preTurn: false,
            effectiveFactionIds,
            issues);
        var sidecars = rawBeforeNormalization
            ? MortalFactionSidecars.Empty
            : await ReadMortalFactionSidecarsAsync(
                effectiveFactionIds,
                issues);
        foreach (var factionId in canonical.Keys
                     .Concat(full.Keys)
                     .Distinct(StringComparer.Ordinal))
        {
            canonical.TryGetValue(factionId, out var canonicalTarget);
            full.TryGetValue(factionId, out var fullTarget);
            preTurn.TryGetValue(factionId, out var previous);

            var currentHasReceipt = HasMortalMaterializationReceipt(
                canonicalTarget?.Faction) ||
                HasMortalMaterializationReceipt(fullTarget?.Faction);
            var previousHadReceipt = HasMortalMaterializationReceipt(
                previous?.Faction);
            var touchKind = hasUsablePreTurnAuthority
                ? FactionTouchClassifier.Classify(
                    existedPreTurn: previous != null,
                    hadReceiptPreTurn: previousHadReceipt)
                : currentHasReceipt
                    ? FactionTouchKind.AlreadyMaterialized
                    : FactionTouchKind.InvalidReceiptless;
            var fullIsCreation =
                fullTarget != null &&
                touchKind == FactionTouchKind.New;
            var factionIssueStart = issues.Count;
            try
            {
                if (fullTarget != null &&
                    fullTarget.HasExplicitNullFactionId &&
                    previous != null)
                {
                    issues.Add(FactionIssue(
                        $"{fullTarget.Context}.initialId",
                        "faction_materialization_mortal_initial_id_collision",
                        factionId,
                        "A Mortal creation initialId must not collide with a pre-turn permanent factionId."));
                }

                if (touchKind == FactionTouchKind.InvalidReceiptless)
                    continue;

                if (rawBeforeNormalization && fullTarget != null)
                {
                    ValidateRawMortalFactionMaterialization(
                        fullTarget,
                        fullIsCreation,
                        hasExistingChronicle: false,
                        effectiveFactionIds,
                        locationAuthority,
                        npcAuthority,
                        issues);
                    continue;
                }

                if (canonicalTarget == null)
                    continue;

                if (rawBeforeNormalization)
                {
                    ValidateCanonicalMortalSemanticCore(
                        canonicalTarget.Faction,
                        canonicalTarget.Context,
                        factionId,
                        issues);
                    ValidateMortalCrossReferences(
                        canonicalTarget.Faction,
                        canonicalTarget.Context,
                        factionId,
                        effectiveFactionIds,
                        locationAuthority,
                        npcAuthority,
                        issues);
                    continue;
                }

                ValidateCanonicalMortalFactionMaterialization(
                    canonicalTarget,
                    sidecars,
                    currentChronicleIds,
                    effectiveFactionIds,
                    locationAuthority,
                    npcAuthority,
                    issues);
            }
            finally
            {
                ApplyFactionRepairClassification(
                    issues,
                    factionIssueStart,
                    $"mortal_faction:{factionId}",
                    touchKind);
            }
        }
    }

    private bool TryClassifyMortalFullCarrier(
        JsonElement faction,
        out string factionId,
        out bool existingPreTurn,
        out bool existingPreTurnHadReceipt,
        out bool collidesWithPreTurnId)
    {
        factionId = ReadFirstMortalString(
            faction,
            "factionId",
            "initialId") ?? string.Empty;
        existingPreTurn = false;
        existingPreTurnHadReceipt = false;
        collidesWithPreTurnId = false;
        if (string.IsNullOrWhiteSpace(factionId))
            return false;

        var preTurnJson = ReadPreTurnTrackedFileSync(
            MortalFactionMaterializationPath);
        if (string.IsNullOrWhiteSpace(preTurnJson))
            return true;

        try
        {
            using var document = JsonDocument.Parse(preTurnJson);
            var preTurn = ReadMortalFactionTargets(
                document.RootElement,
                "factions",
                useInitialId: false);
            if (!preTurn.TryGetValue(factionId, out var previous))
                return true;

            existingPreTurn = true;
            existingPreTurnHadReceipt =
                HasMortalMaterializationReceipt(previous.Faction);
            collidesWithPreTurnId =
                faction.TryGetProperty(
                    "factionId",
                    out var factionIdNode) &&
                factionIdNode.ValueKind == JsonValueKind.Null &&
                ReadMortalString(faction, "initialId") != null;
            return false;
        }
        catch (JsonException)
        {
            return true;
        }
    }

    private void ValidateRawMortalFactionMaterialization(
        MortalFactionMaterializationTarget target,
        bool isCreation,
        bool hasExistingChronicle,
        IReadOnlySet<string> effectiveFactionIds,
        MortalLocationAuthority locationAuthority,
        MortalNpcAuthority npcAuthority,
        List<ValidationIssue> issues)
    {
        ValidateMortalSemanticCore(
            target.Faction,
            target.Context,
            target.FactionId,
            hasExistingChronicle,
            issues);
        ValidateMortalCrossReferences(
            target.Faction,
            target.Context,
            target.FactionId,
            effectiveFactionIds,
            locationAuthority,
            npcAuthority,
            issues);
        ValidateExactMortalPlayerMembershipCarrier(
            target.Faction,
            target.Context,
            target.FactionId,
            issues);

        var structure = target.Faction;
        var resources = target.Faction.TryGetProperty(
            "resources",
            out var resourceObject)
            ? resourceObject
            : default;
        var activeProjects = ReadTargetProjectRows(
            target.Faction,
            "activeProjects",
            target.FactionId,
            carrierOwnsRows: true);
        var completedProjects = ReadTargetProjectRows(
            target.Faction,
            "completedProjects",
            target.FactionId,
            carrierOwnsRows: true);

        var evidence = BuildMortalFactionEvidence(
            target.Faction,
            target.FactionId,
            structure,
            resources,
            target.Faction,
            activeProjects,
            completedProjects,
            locationAuthority,
            requireExplicitProjectCarrier: true);
        issues.AddRange(FactionMaterializationContract.Validate(
            target.Faction,
            target.Context,
            FactionMaterializationFamily.Mortal,
            evidence,
            requireEnvelope: isCreation ||
                             HasMortalMaterializationReceipt(target.Faction),
            deferEvidenceConsistency: false));
    }

    private void ValidateCanonicalMortalFactionMaterialization(
        MortalFactionMaterializationTarget target,
        MortalFactionSidecars sidecars,
        IReadOnlySet<string> currentChronicleIds,
        IReadOnlySet<string> effectiveFactionIds,
        MortalLocationAuthority locationAuthority,
        MortalNpcAuthority npcAuthority,
        List<ValidationIssue> issues)
    {
        var factionId = target.FactionId;
        ValidateCanonicalMortalSemanticCore(
            target.Faction,
            target.Context,
            factionId,
            issues);
        ValidateMortalCrossReferences(
            target.Faction,
            target.Context,
            factionId,
            effectiveFactionIds,
            locationAuthority,
            npcAuthority,
            issues);
        ValidateExactMortalPlayerMembershipCarrier(
            target.Faction,
            target.Context,
            factionId,
            issues);

        sidecars.Structure.TryGetValue(factionId, out var structure);
        sidecars.Resources.TryGetValue(factionId, out var resources);
        sidecars.Custom.TryGetValue(factionId, out var custom);
        if (structure.ValueKind != JsonValueKind.Object)
        {
            issues.Add(FactionIssue(
                MortalFactionStructurePath,
                "faction_materialization_mortal_structure_missing",
                factionId,
                "A materialized Mortal faction requires an exact structure entry, including deliberate emptiness."));
        }
        else
        {
            ValidateMortalFactionGovernance(
                structure,
                sidecars.StructureContexts[factionId],
                factionId,
                issues);
            ValidateMortalFactionLeadership(
                structure,
                sidecars.StructureContexts[factionId],
                factionId,
                issues);
            ValidateMortalLeaderNpcReferences(
                structure,
                sidecars.StructureContexts[factionId],
                factionId,
                npcAuthority,
                issues);
            if (!structure.TryGetProperty("ranks", out var ranks) ||
                ranks.ValueKind != JsonValueKind.Object ||
                !ranks.TryGetProperty("branches", out var branches) ||
                branches.ValueKind != JsonValueKind.Array ||
                !structure.TryGetProperty(
                    "structuredBonuses",
                    out var structuredBonuses) ||
                structuredBonuses.ValueKind != JsonValueKind.Array)
            {
                issues.Add(FactionIssue(
                    sidecars.StructureContexts[factionId],
                    "faction_materialization_mortal_structure_invalid",
                    factionId,
                    "A materialized Mortal structure entry requires ranks.branches and structuredBonuses arrays."));
            }
        }

        if (resources.ValueKind != JsonValueKind.Object)
        {
            issues.Add(FactionIssue(
                MortalFactionResourcesPath,
                "faction_materialization_mortal_resources_missing",
                factionId,
                "A materialized Mortal faction requires an exact resource entry, including deliberate emptiness."));
        }
        else if (!resources.TryGetProperty("metaResources", out var metaResources) ||
                 metaResources.ValueKind != JsonValueKind.Array ||
                 !resources.TryGetProperty(
                     "strategicGoods",
                     out var strategicGoods) ||
                 strategicGoods.ValueKind != JsonValueKind.Array)
        {
            issues.Add(FactionIssue(
                sidecars.ResourceContexts[factionId],
                "faction_materialization_mortal_resources_invalid",
                factionId,
                "A materialized Mortal resource entry requires metaResources and strategicGoods arrays."));
        }

        if (custom.ValueKind != JsonValueKind.Object)
        {
            issues.Add(FactionIssue(
                MortalFactionCustomPath,
                "faction_materialization_mortal_custom_missing",
                factionId,
                "A materialized Mortal faction requires an exact custom-state entry, including deliberate emptiness."));
        }
        else if (!custom.TryGetProperty("customStates", out var customStates) ||
                 customStates.ValueKind != JsonValueKind.Array)
        {
            issues.Add(FactionIssue(
                sidecars.CustomContexts[factionId],
                "faction_materialization_mortal_custom_invalid",
                factionId,
                "A materialized Mortal custom-state entry requires a customStates array."));
        }

        if (!target.Faction.TryGetProperty("relations", out var relations) ||
            relations.ValueKind != JsonValueKind.Array)
        {
            issues.Add(FactionIssue(
                $"{target.Context}.relations",
                "faction_materialization_mortal_relations_invalid",
                factionId,
                "A materialized Mortal faction requires an explicit relations array."));
        }

        if (!target.Faction.TryGetProperty(
                "controlledTerritories",
                out var controlledTerritories) ||
            controlledTerritories.ValueKind != JsonValueKind.Array)
        {
            issues.Add(FactionIssue(
                $"{target.Context}.controlledTerritories",
                "faction_materialization_mortal_territory_invalid",
                factionId,
                "A materialized Mortal faction requires an explicit controlledTerritories array."));
        }

        if (!currentChronicleIds.Contains(factionId))
        {
            issues.Add(FactionIssue(
                MortalFactionChroniclesPath,
                "faction_materialization_mortal_chronicle_missing",
                factionId,
                "A materialized Mortal faction requires at least one exact target-bound chronicle entry."));
        }

        var activeProjects = sidecars.ActiveProjects
            .Where(project => string.Equals(
                ReadMortalString(project, "factionId"),
                factionId,
                StringComparison.Ordinal))
            .ToArray();
        var completedProjects = sidecars.CompletedProjects
            .Where(project => string.Equals(
                ReadMortalString(project, "factionId"),
                factionId,
                StringComparison.Ordinal))
            .ToArray();
        var evidence = BuildMortalFactionEvidence(
            target.Faction,
            factionId,
            structure,
            resources,
            custom,
            activeProjects,
            completedProjects,
            locationAuthority,
            requireExplicitProjectCarrier: false);

        // Task 2 owns immutable receipt continuity. Canonical evidence may
        // evolve after materialization, so only the complete live bundle is
        // checked here rather than replaying snapshot dispositions.
        issues.AddRange(FactionMaterializationContract.Validate(
            target.Faction,
            target.Context,
            FactionMaterializationFamily.Mortal,
            evidence,
            requireEnvelope: true,
            deferEvidenceConsistency: true));
    }

    private static FactionMaterializationEvidence BuildMortalFactionEvidence(
        JsonElement faction,
        string factionId,
        JsonElement structure,
        JsonElement resources,
        JsonElement custom,
        IReadOnlyCollection<JsonElement> activeProjects,
        IReadOnlyCollection<JsonElement> completedProjects,
        MortalLocationAuthority locationAuthority,
        bool requireExplicitProjectCarrier)
    {
        var hierarchy = HasFormalMortalHierarchy(structure);
        var factionResources = HasMortalResources(resources);
        var relations = HasObjectArrayEntries(faction, "relations");
        var projects = activeProjects.Count + completedProjects.Count > 0;
        var territory = HasMortalTerritoryOrInfluence(
            faction,
            factionId,
            locationAuthority);
        var membership = HasMortalPlayerMembership(faction);
        var customStates = HasMortalCustomStates(custom);

        var explicitEmptyProjects =
            (!requireExplicitProjectCarrier ||
             HasExplicitEmptyProjectCarrier(faction)) &&
            activeProjects.Count == 0 &&
            completedProjects.Count == 0;

        return new FactionMaterializationEvidence(
            "mortal_faction",
            factionId,
            new Dictionary<string, bool>(StringComparer.Ordinal)
            {
                ["hierarchy"] = hierarchy,
                ["resources"] = factionResources,
                ["relations"] = relations,
                ["projects"] = projects,
                ["territoryAndInfluence"] = territory,
                ["playerMembership"] = membership,
                ["customStates"] = customStates
            },
            new Dictionary<string, bool>(StringComparer.Ordinal)
            {
                ["hasFormalHierarchy"] = hierarchy,
                ["usesFactionResources"] = factionResources,
                ["maintainsRelations"] = relations,
                ["runsProjects"] = projects,
                ["holdsTerritoryOrInfluence"] = territory,
                ["supportsPlayerMembership"] = membership,
                ["usesCustomMechanics"] = customStates
            },
            new Dictionary<string, bool>(StringComparer.Ordinal)
            {
                ["hierarchy"] = HasExactEmptyMortalHierarchy(structure),
                ["resources"] = HasExactEmptyMortalResources(resources),
                ["relations"] = HasExactEmptyArray(faction, "relations"),
                ["projects"] = explicitEmptyProjects,
                ["territoryAndInfluence"] = HasExactEmptyMortalTerritory(
                    faction,
                    factionId,
                    locationAuthority),
                ["playerMembership"] = HasExactEmptyMortalPlayerMembership(
                    faction),
                ["customStates"] = HasExactEmptyMortalCustomStates(custom)
            });
    }

    private static void ValidateMortalSemanticCore(
        JsonElement faction,
        string context,
        string factionId,
        bool hasExistingChronicle,
        List<ValidationIssue> issues)
    {
        ValidateRequiredMortalImagePrompt(
            faction,
            context,
            factionId,
            issues);
        var factionColor = RequireMortalString(
            faction,
            context,
            factionId,
            issues,
            "factionColor",
            "faction_materialization_mortal_color_missing");
        if (!string.IsNullOrWhiteSpace(factionColor) &&
            !MortalFactionColorRegex.IsMatch(factionColor))
        {
            issues.Add(FactionIssue(
                $"{context}.factionColor",
                "faction_materialization_mortal_color_invalid",
                factionId,
                "A materialized Mortal faction requires exact #RRGGBB visual identity."));
        }

        RequireMortalString(
            faction,
            context,
            factionId,
            issues,
            "purpose",
            "faction_materialization_mortal_purpose_missing");
        RequireMortalString(
            faction,
            context,
            factionId,
            issues,
            "currentAgenda",
            "faction_materialization_mortal_agenda_missing");
        ValidateMortalPrinciples(faction, context, factionId, issues);
        ValidateMortalFactionMemory(faction, context, factionId, issues);
        ValidateMortalFactionGovernance(faction, context, factionId, issues);
        ValidateMortalFactionLeadership(faction, context, factionId, issues);

        var hasCreationChronicle =
            faction.TryGetProperty("scribeChronicle", out var chronicle) &&
            chronicle.ValueKind == JsonValueKind.Array &&
            chronicle.EnumerateArray().Any(entry =>
                entry.ValueKind == JsonValueKind.String &&
                MortalChronicleEntryRegex.IsMatch(entry.GetString() ?? string.Empty));
        if (!hasExistingChronicle && !hasCreationChronicle)
        {
            issues.Add(FactionIssue(
                $"{context}.scribeChronicle",
                "faction_materialization_mortal_chronicle_missing",
                factionId,
                "A new Mortal faction requires at least one turn-prefixed creation chronicle entry."));
        }
    }

    private static void ValidateCanonicalMortalSemanticCore(
        JsonElement faction,
        string context,
        string factionId,
        List<ValidationIssue> issues)
    {
        ValidateRequiredMortalImagePrompt(
            faction,
            context,
            factionId,
            issues);
        var factionColor = RequireMortalString(
            faction,
            context,
            factionId,
            issues,
            "factionColor",
            "faction_materialization_mortal_color_missing");
        if (!string.IsNullOrWhiteSpace(factionColor) &&
            !MortalFactionColorRegex.IsMatch(factionColor))
        {
            issues.Add(FactionIssue(
                $"{context}.factionColor",
                "faction_materialization_mortal_color_invalid",
                factionId,
                "A materialized Mortal faction requires exact #RRGGBB visual identity."));
        }

        RequireMortalString(
            faction,
            context,
            factionId,
            issues,
            "purpose",
            "faction_materialization_mortal_purpose_missing");
        RequireMortalString(
            faction,
            context,
            factionId,
            issues,
            "currentAgenda",
            "faction_materialization_mortal_agenda_missing");
        ValidateMortalPrinciples(faction, context, factionId, issues);
        ValidateMortalFactionMemory(faction, context, factionId, issues);
    }

    private static void ValidateRequiredMortalImagePrompt(
        JsonElement faction,
        string context,
        string factionId,
        List<ValidationIssue> issues)
    {
        var imagePrompt = RequireMortalString(
            faction,
            context,
            factionId,
            issues,
            "image_prompt",
            "faction_materialization_mortal_image_prompt_missing");
        if (!string.IsNullOrWhiteSpace(imagePrompt) &&
            !LooksLikeEnglishImagePrompt(imagePrompt))
        {
            issues.Add(FactionIssue(
                $"{context}.image_prompt",
                "faction_materialization_mortal_image_prompt_invalid",
                factionId,
                "A materialized Mortal faction requires an English-only image_prompt no longer than 150 characters."));
        }
    }

    private static void ValidateMortalPrinciples(
        JsonElement faction,
        string context,
        string factionId,
        List<ValidationIssue> issues)
    {
        if (!faction.TryGetProperty("principles", out var principles))
        {
            issues.Add(FactionIssue(
                $"{context}.principles",
                "faction_materialization_mortal_principles_missing",
                factionId,
                "A materialized Mortal faction requires explicit principles."));
            return;
        }

        if (principles.ValueKind != JsonValueKind.Array ||
            principles.GetArrayLength() == 0)
        {
            issues.Add(FactionIssue(
                $"{context}.principles",
                "faction_materialization_mortal_principles_invalid",
                factionId,
                "Mortal faction principles must be a non-empty array of unique non-whitespace strings."));
            return;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var index = 0;
        foreach (var principle in principles.EnumerateArray())
        {
            var valid = principle.ValueKind == JsonValueKind.String &&
                        !string.IsNullOrWhiteSpace(principle.GetString()) &&
                        seen.Add(principle.GetString()!);
            if (!valid)
            {
                issues.Add(FactionIssue(
                    $"{context}.principles[{index}]",
                    "faction_materialization_mortal_principles_invalid",
                    factionId,
                    "Mortal faction principles must be unique non-whitespace strings."));
            }

            index++;
        }
    }

    private static void ValidateMortalFactionMemory(
        JsonElement faction,
        string context,
        string factionId,
        List<ValidationIssue> issues)
    {
        if (!faction.TryGetProperty("memory", out var memory) ||
            memory.ValueKind != JsonValueKind.Object)
        {
            issues.Add(FactionIssue(
                $"{context}.memory",
                "faction_materialization_mortal_memory_missing",
                factionId,
                "A materialized Mortal faction requires complete structured memory."));
            return;
        }

        ValidateClosedMortalObject(
            memory,
            MortalMemoryFields,
            $"{context}.memory",
            factionId,
            "faction_materialization_mortal_memory_invalid",
            issues);
        RequireMortalString(
            memory,
            $"{context}.memory",
            factionId,
            issues,
            "summary",
            "faction_materialization_mortal_memory_invalid");
        if (!memory.TryGetProperty("lastUpdatedTurn", out var lastUpdatedTurn) ||
            lastUpdatedTurn.ValueKind != JsonValueKind.Number ||
            !lastUpdatedTurn.TryGetInt32(out var turn) ||
            turn < 0)
        {
            issues.Add(FactionIssue(
                $"{context}.memory.lastUpdatedTurn",
                "faction_materialization_mortal_memory_invalid",
                factionId,
                "Mortal faction memory.lastUpdatedTurn must be an integer greater than or equal to zero."));
        }

        ValidateMortalStringArray(
            memory,
            $"{context}.memory",
            "enduringFacts",
            factionId,
            "faction_materialization_mortal_memory_invalid",
            allowEmpty: true,
            issues);
        ValidateMortalStringArray(
            memory,
            $"{context}.memory",
            "openThreads",
            factionId,
            "faction_materialization_mortal_memory_invalid",
            allowEmpty: true,
            issues);
    }

    private static void ValidateMortalFactionGovernance(
        JsonElement factionOrStructure,
        string context,
        string factionId,
        List<ValidationIssue> issues)
    {
        if (!factionOrStructure.TryGetProperty("governance", out var governance) ||
            governance.ValueKind != JsonValueKind.Object)
        {
            issues.Add(FactionIssue(
                $"{context}.governance",
                "faction_materialization_mortal_governance_missing",
                factionId,
                "A materialized Mortal faction requires complete governance authority."));
            return;
        }

        ValidateClosedMortalObject(
            governance,
            MortalGovernanceFields,
            $"{context}.governance",
            factionId,
            "faction_materialization_mortal_governance_invalid",
            issues);
        RequireMortalString(
            governance,
            $"{context}.governance",
            factionId,
            issues,
            "model",
            "faction_materialization_mortal_governance_invalid");
        RequireMortalString(
            governance,
            $"{context}.governance",
            factionId,
            issues,
            "decisionProcess",
            "faction_materialization_mortal_governance_invalid");
    }

    private static void ValidateMortalFactionLeadership(
        JsonElement factionOrStructure,
        string context,
        string factionId,
        List<ValidationIssue> issues)
    {
        if (!factionOrStructure.TryGetProperty("leadership", out var leadership) ||
            leadership.ValueKind != JsonValueKind.Object)
        {
            issues.Add(FactionIssue(
                $"{context}.leadership",
                "faction_materialization_mortal_leadership_missing",
                factionId,
                "A materialized Mortal faction requires complete leadership authority."));
            return;
        }

        ValidateClosedMortalObject(
            leadership,
            MortalLeadershipFields,
            $"{context}.leadership",
            factionId,
            "faction_materialization_mortal_leadership_invalid",
            issues);
        var leadershipState = RequireMortalString(
            leadership,
            $"{context}.leadership",
            factionId,
            issues,
            "leadershipState",
            "faction_materialization_mortal_leadership_invalid");
        RequireMortalString(
            leadership,
            $"{context}.leadership",
            factionId,
            issues,
            "summary",
            "faction_materialization_mortal_leadership_invalid");

        if (!string.IsNullOrWhiteSpace(leadershipState) &&
            !MortalLeadershipStates.Contains(leadershipState))
        {
            issues.Add(FactionIssue(
                $"{context}.leadership.leadershipState",
                "faction_materialization_mortal_leadership_invalid",
                factionId,
                "Mortal leadershipState must be headed, collective, or vacant."));
        }

        if (!leadership.TryGetProperty("leaderNpcIds", out var leaderNpcIds) ||
            leaderNpcIds.ValueKind != JsonValueKind.Array)
        {
            issues.Add(FactionIssue(
                $"{context}.leadership.leaderNpcIds",
                "faction_materialization_mortal_leadership_invalid",
                factionId,
                "Mortal leadership requires an explicit array of exact leader NPC IDs."));
            return;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var validIds = 0;
        var index = 0;
        foreach (var leaderNpcId in leaderNpcIds.EnumerateArray())
        {
            var id = leaderNpcId.ValueKind == JsonValueKind.String
                ? leaderNpcId.GetString()
                : null;
            if (string.IsNullOrWhiteSpace(id) || !seen.Add(id))
            {
                issues.Add(FactionIssue(
                    $"{context}.leadership.leaderNpcIds[{index}]",
                    "faction_materialization_mortal_leadership_invalid",
                    factionId,
                    "Mortal leader NPC IDs must be unique non-whitespace exact IDs."));
            }
            else
            {
                validIds++;
            }

            index++;
        }

        if ((string.Equals(
                 leadershipState,
                 "vacant",
                 StringComparison.Ordinal) &&
             validIds != 0) ||
            (string.Equals(
                 leadershipState,
                 "headed",
                 StringComparison.Ordinal) &&
             validIds == 0))
        {
            issues.Add(FactionIssue(
                $"{context}.leadership.leaderNpcIds",
                "faction_materialization_mortal_leadership_invalid",
                factionId,
                "Vacant leadership requires no leader IDs; headed leadership requires at least one."));
        }
    }

    private static void ValidateMortalCrossReferences(
        JsonElement faction,
        string context,
        string factionId,
        IReadOnlySet<string> effectiveFactionIds,
        MortalLocationAuthority locationAuthority,
        MortalNpcAuthority npcAuthority,
        List<ValidationIssue> issues)
    {
        if (faction.TryGetProperty("relations", out var relations) &&
            relations.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var relation in relations.EnumerateArray())
            {
                var targetFactionId = ReadMortalString(
                    relation,
                    "targetFactionId");
                if (!string.IsNullOrWhiteSpace(targetFactionId) &&
                    !effectiveFactionIds.Contains(targetFactionId))
                {
                    issues.Add(FactionIssue(
                        $"{context}.relations[{index}].targetFactionId",
                        "faction_materialization_mortal_relation_unknown_target",
                        factionId,
                        "Mortal relation targets must resolve to an exact effective faction ID."));
                }

                index++;
            }
        }

        if (faction.TryGetProperty(
                "controlledTerritories",
                out var territories) &&
            territories.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var territory in territories.EnumerateArray())
            {
                var locationId = ReadMortalString(territory, "locationId");
                if (!string.IsNullOrWhiteSpace(locationId) &&
                    !locationAuthority.LocationIds.Contains(locationId))
                {
                    issues.Add(FactionIssue(
                        $"{context}.controlledTerritories[{index}].locationId",
                        "faction_materialization_mortal_territory_unknown_location",
                        factionId,
                        "Mortal territory references must resolve to an exact location ID."));
                }

                index++;
            }
        }

        ValidateMortalLeaderNpcReferences(
            faction,
            context,
            factionId,
            npcAuthority,
            issues);
    }

    private static void ValidateMortalLeaderNpcReferences(
        JsonElement factionOrStructure,
        string context,
        string factionId,
        MortalNpcAuthority npcAuthority,
        List<ValidationIssue> issues)
    {
        if (!factionOrStructure.TryGetProperty(
                "leadership",
                out var leadership) ||
            leadership.ValueKind != JsonValueKind.Object ||
            !leadership.TryGetProperty(
                "leaderNpcIds",
                out var leaderNpcIds) ||
            leaderNpcIds.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var leaderIndex = 0;
        foreach (var leaderNpcId in leaderNpcIds.EnumerateArray())
        {
            var id = leaderNpcId.ValueKind == JsonValueKind.String
                ? leaderNpcId.GetString()
                : null;
            if (!string.IsNullOrWhiteSpace(id) &&
                !npcAuthority.NpcIds.Contains(id))
            {
                issues.Add(FactionIssue(
                    $"{context}.leadership.leaderNpcIds[{leaderIndex}]",
                    "faction_materialization_mortal_leader_unknown_npc",
                    factionId,
                    "Every Mortal leader ID must resolve to exact Mortal NPC authority."));
            }

            leaderIndex++;
        }
    }

    private static void ValidateExactMortalPlayerMembershipCarrier(
        JsonElement faction,
        string context,
        string factionId,
        List<ValidationIssue> issues)
    {
        if (HasMortalPlayerMembership(faction) ||
            HasExactEmptyMortalPlayerMembership(faction))
        {
            return;
        }

        issues.Add(FactionIssue(
            $"{context}.playerMembership",
            "faction_materialization_mortal_player_membership_incomplete",
            factionId,
            "Mortal player membership must be populated consistently or carry every exact non-member value."));
    }

    private async Task<MortalFactionSidecars> ReadMortalFactionSidecarsAsync(
        IReadOnlySet<string> effectiveFactionIds,
        List<ValidationIssue> issues)
    {
        var structureRoot = await ReadMortalMaterializationRootAsync(
            MortalFactionStructurePath);
        var resourceRoot = await ReadMortalMaterializationRootAsync(
            MortalFactionResourcesPath);
        var projectRoot = await ReadMortalMaterializationRootAsync(
            MortalFactionProjectsPath);
        var customRoot = await ReadMortalMaterializationRootAsync(
            MortalFactionCustomPath);

        var structure = ReadExactMortalSidecarEntries(
            structureRoot,
            MortalFactionStructurePath,
            effectiveFactionIds,
            issues,
            out var structureContexts);
        var resources = ReadExactMortalSidecarEntries(
            resourceRoot,
            MortalFactionResourcesPath,
            effectiveFactionIds,
            issues,
            out var resourceContexts);
        var custom = ReadExactMortalSidecarEntries(
            customRoot,
            MortalFactionCustomPath,
            effectiveFactionIds,
            issues,
            out var customContexts);
        var activeProjects = ReadMortalProjectRows(
            projectRoot,
            "activeProjects",
            effectiveFactionIds,
            issues);
        var completedProjects = ReadMortalProjectRows(
            projectRoot,
            "completedProjects",
            effectiveFactionIds,
            issues);
        return new MortalFactionSidecars(
            structure,
            structureContexts,
            resources,
            resourceContexts,
            custom,
            customContexts,
            activeProjects,
            completedProjects);
    }

    private static Dictionary<string, JsonElement> ReadExactMortalSidecarEntries(
        JsonElement? root,
        string path,
        IReadOnlySet<string> effectiveFactionIds,
        List<ValidationIssue> issues,
        out Dictionary<string, string> contexts)
    {
        var result = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        contexts = new Dictionary<string, string>(StringComparer.Ordinal);
        if (root is not { ValueKind: JsonValueKind.Object } ||
            !root.Value.TryGetProperty("entries", out var entries) ||
            entries.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        var index = 0;
        foreach (var entry in entries.EnumerateArray())
        {
            var context = $"{path}.entries[{index++}]";
            if (entry.ValueKind != JsonValueKind.Object)
                continue;
            var factionId = ReadMortalString(entry, "factionId");
            if (string.IsNullOrWhiteSpace(factionId))
                continue;
            if (!effectiveFactionIds.Contains(factionId))
            {
                issues.Add(FactionIssue(
                    $"{context}.factionId",
                    "faction_materialization_mortal_orphaned_sidecar",
                    factionId,
                    "Mortal sidecar entries must resolve to one exact effective faction ID."));
                continue;
            }

            if (!result.TryAdd(factionId, entry.Clone()))
            {
                issues.Add(FactionIssue(
                    $"{context}.factionId",
                    "faction_materialization_mortal_duplicate_sidecar",
                    factionId,
                    "A materialized Mortal faction may have only one exact target record per sidecar."));
                continue;
            }

            contexts[factionId] = context;
        }

        return result;
    }

    private static IReadOnlyList<JsonElement> ReadMortalProjectRows(
        JsonElement? root,
        string propertyName,
        IReadOnlySet<string> effectiveFactionIds,
        List<ValidationIssue> issues)
    {
        var result = new List<JsonElement>();
        if (root is not { ValueKind: JsonValueKind.Object } ||
            !root.Value.TryGetProperty(propertyName, out var rows) ||
            rows.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        var index = 0;
        foreach (var row in rows.EnumerateArray())
        {
            if (row.ValueKind != JsonValueKind.Object)
            {
                index++;
                continue;
            }

            var factionId = ReadMortalString(row, "factionId");
            if (!string.IsNullOrWhiteSpace(factionId) &&
                !effectiveFactionIds.Contains(factionId))
            {
                issues.Add(FactionIssue(
                    $"{MortalFactionProjectsPath}.{propertyName}[{index}].factionId",
                    "faction_materialization_mortal_orphaned_project",
                    factionId,
                    "Mortal project rows must resolve to one exact effective faction ID."));
            }

            result.Add(row.Clone());
            index++;
        }

        return result;
    }

    private async Task<MortalLocationAuthority> ReadMortalLocationAuthorityAsync()
    {
        var locationsBySourceAndId =
            new Dictionary<
                (string RootPath, string LocationId),
                MortalLocationAuthorityEntry>();
        var locationsWithoutIds =
            new List<MortalLocationAuthorityEntry>();
        foreach (var path in new[]
                 {
                     "game_state/world/current_location.json",
                     "game_state/world/world_map.json"
                 })
        {
            foreach (var json in new[]
                     {
                         await ReadPreTurnTrackedFileAsync(path),
                         await _fs.ReadFileAsync(path)
                     })
            {
                if (string.IsNullOrWhiteSpace(json))
                    continue;
                try
                {
                    using var document = JsonDocument.Parse(json);
                    foreach (var location in EnumerateLocationLikeObjects(
                                 document.RootElement))
                    {
                        var clone = location.Clone();
                        var locationId = ReadMortalString(
                            clone,
                            "locationId");
                        var entry = new MortalLocationAuthorityEntry(
                            clone,
                            path);
                        if (string.IsNullOrWhiteSpace(locationId))
                            locationsWithoutIds.Add(entry);
                        else
                            locationsBySourceAndId[(path, locationId)] =
                                entry;
                    }
                }
                catch (JsonException)
                {
                    // Ordinary state-file validation owns malformed JSON.
                }
            }
        }

        var locations = locationsBySourceAndId.Values
            .Concat(locationsWithoutIds)
            .ToArray();
        return new MortalLocationAuthority(
            locationsBySourceAndId.Keys
                .Select(key => key.LocationId)
                .ToHashSet(StringComparer.Ordinal),
            locations);
    }

    private async Task<MortalNpcAuthority> ReadMortalNpcAuthorityAsync()
    {
        var npcs = new List<JsonElement>();
        foreach (var json in new[]
                 {
                     await _fs.ReadFileAsync(MortalNpcCorePath),
                     await ReadPreTurnTrackedFileAsync(MortalNpcCorePath)
                 })
        {
            if (string.IsNullOrWhiteSpace(json))
                continue;
            try
            {
                using var document = JsonDocument.Parse(json);
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                    continue;
                foreach (var property in document.RootElement.EnumerateObject())
                {
                    if (property.Value.ValueKind != JsonValueKind.Array)
                        continue;
                    foreach (var npc in property.Value.EnumerateArray())
                    {
                        if (npc.ValueKind == JsonValueKind.Object)
                            npcs.Add(npc.Clone());
                    }
                }
            }
            catch (JsonException)
            {
                // Ordinary state-file validation owns malformed JSON.
            }
        }

        return new MortalNpcAuthority(
            npcs
                .Select(npc => ReadFirstMortalString(
                    npc,
                    "NPCId",
                    "npcId",
                    "id",
                    "initialId"))
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Cast<string>()
                .ToHashSet(StringComparer.Ordinal),
            npcs);
    }

    private static void ValidateMortalLocationFactionReferences(
        MortalLocationAuthority authority,
        IReadOnlySet<string> effectiveFactionIds,
        List<ValidationIssue> issues)
    {
        foreach (var authorityEntry in authority.Locations)
        {
            var location = authorityEntry.Location;
            if (!location.TryGetProperty("factionControl", out var controls) ||
                controls.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            var locationId = ReadMortalString(location, "locationId") ??
                             "unknown-location";
            var index = 0;
            foreach (var control in controls.EnumerateArray())
            {
                var factionId = ReadMortalString(control, "factionId");
                if (!string.IsNullOrWhiteSpace(factionId) &&
                    !effectiveFactionIds.Contains(factionId))
                {
                    issues.Add(FactionIssue(
                        $"location:{locationId}.factionControl[{index}].factionId",
                        "faction_materialization_mortal_location_control_unknown_faction",
                        factionId,
                        "Mortal location control must use one exact effective faction ID.",
                        repairTargetFiles:
                        [
                            authorityEntry.RootPath
                        ]));
                }

                index++;
            }
        }
    }

    private static void ValidateMortalNpcFactionAffiliations(
        MortalNpcAuthority authority,
        IReadOnlySet<string> effectiveFactionIds,
        List<ValidationIssue> issues)
    {
        foreach (var npc in authority.Npcs)
        {
            if (!npc.TryGetProperty(
                    "factionAffiliations",
                    out var affiliations) ||
                affiliations.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            var npcId = ReadFirstMortalString(
                npc,
                "NPCId",
                "npcId",
                "id",
                "initialId") ?? "unknown-npc";
            var index = 0;
            foreach (var affiliation in affiliations.EnumerateArray())
            {
                var factionId = ReadMortalString(
                    affiliation,
                    "factionId");
                if (!string.IsNullOrWhiteSpace(factionId) &&
                    !effectiveFactionIds.Contains(factionId))
                {
                    issues.Add(FactionIssue(
                        $"{MortalNpcCorePath}:{npcId}.factionAffiliations[{index}].factionId",
                        "faction_materialization_mortal_npc_affiliation_unknown_faction",
                        factionId,
                        "Mortal NPC affiliations must use one exact effective faction ID."));
                }

                index++;
            }
        }
    }

    private async Task<HashSet<string>> ReadMortalChronicleFactionIdsAsync(
        bool preTurn,
        IReadOnlySet<string>? effectiveFactionIds = null,
        List<ValidationIssue>? issues = null)
    {
        var json = preTurn
            ? await ReadPreTurnTrackedFileAsync(MortalFactionChroniclesPath)
            : await _fs.ReadFileAsync(MortalFactionChroniclesPath);
        var result = new HashSet<string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(json))
            return result;
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty(
                    "entries",
                    out var entries) ||
                entries.ValueKind != JsonValueKind.Array)
            {
                return result;
            }

            var index = 0;
            foreach (var entry in entries.EnumerateArray())
            {
                if (entry.ValueKind != JsonValueKind.Object)
                {
                    index++;
                    continue;
                }
                var factionId = ReadMortalString(entry, "factionId");
                var text = ReadFirstMortalString(
                    entry,
                    "entry",
                    "chronicle",
                    "text");
                if (!string.IsNullOrWhiteSpace(factionId) &&
                    effectiveFactionIds != null &&
                    !effectiveFactionIds.Contains(factionId))
                {
                    issues?.Add(FactionIssue(
                        $"{MortalFactionChroniclesPath}.entries[{index}].factionId",
                        "faction_materialization_mortal_orphaned_chronicle",
                        factionId,
                        "Mortal chronicle entries must use one exact effective faction ID."));
                    index++;
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(factionId) &&
                    !string.IsNullOrWhiteSpace(text))
                {
                    result.Add(factionId);
                }

                index++;
            }
        }
        catch (JsonException)
        {
            // Ordinary state-file validation owns malformed JSON.
        }

        return result;
    }

    private async Task<Dictionary<string, MortalFactionMaterializationTarget>>
        ReadPreTurnMortalFactionTargetsAsync()
    {
        var json = await ReadPreTurnTrackedFileAsync(
            MortalFactionMaterializationPath);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, MortalFactionMaterializationTarget>(
                StringComparer.Ordinal);
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            return ReadMortalFactionTargets(
                document.RootElement,
                "factions",
                useInitialId: false);
        }
        catch (JsonException)
        {
            return new Dictionary<string, MortalFactionMaterializationTarget>(
                StringComparer.Ordinal);
        }
    }

    private async Task<JsonElement?> ReadMortalMaterializationRootAsync(
        string path)
    {
        var json = await _fs.ReadFileAsync(path);
        if (string.IsNullOrWhiteSpace(json))
            return null;
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind == JsonValueKind.Object
                ? document.RootElement.Clone()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static Dictionary<string, MortalFactionMaterializationTarget>
        ReadMortalFactionTargets(
            JsonElement root,
            string propertyName,
            bool useInitialId)
    {
        var result =
            new Dictionary<string, MortalFactionMaterializationTarget>(
                StringComparer.Ordinal);
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty(propertyName, out var factions) ||
            factions.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        var index = 0;
        foreach (var faction in factions.EnumerateArray())
        {
            var context =
                $"{MortalFactionMaterializationPath}.{propertyName}[{index++}]";
            if (faction.ValueKind != JsonValueKind.Object)
                continue;
            var factionId = useInitialId
                ? ReadFirstMortalString(faction, "factionId", "initialId")
                : ReadMortalString(faction, "factionId");
            if (string.IsNullOrWhiteSpace(factionId))
                continue;

            var hasExplicitNullFactionId =
                faction.TryGetProperty("factionId", out var factionIdNode) &&
                factionIdNode.ValueKind == JsonValueKind.Null;
            result.TryAdd(
                factionId,
                new MortalFactionMaterializationTarget(
                    faction.Clone(),
                    context,
                    factionId,
                    hasExplicitNullFactionId));
        }

        return result;
    }

    private static bool HasMortalMaterializationReceipt(JsonElement? faction) =>
        faction is { ValueKind: JsonValueKind.Object } &&
        faction.Value.TryGetProperty(
            FactionMaterializationContract.PropertyName,
            out _);

    private static string RequireMortalString(
        JsonElement value,
        string context,
        string factionId,
        List<ValidationIssue> issues,
        string propertyName,
        string code)
    {
        var result = ReadMortalString(value, propertyName);
        if (!string.IsNullOrWhiteSpace(result))
            return result;
        issues.Add(FactionIssue(
            $"{context}.{propertyName}",
            code,
            factionId,
            $"Mortal faction field {propertyName} must be a non-empty string."));
        return string.Empty;
    }

    private static void ValidateClosedMortalObject(
        JsonElement value,
        IReadOnlySet<string> fields,
        string context,
        string factionId,
        string code,
        List<ValidationIssue> issues)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in value.EnumerateObject())
        {
            if (!seen.Add(property.Name) || !fields.Contains(property.Name))
            {
                issues.Add(FactionIssue(
                    $"{context}.{property.Name}",
                    code,
                    factionId,
                    "Mortal semantic objects are closed and duplicate-sensitive."));
            }
        }

        foreach (var field in fields)
        {
            if (!seen.Contains(field))
            {
                issues.Add(FactionIssue(
                    $"{context}.{field}",
                    code,
                    factionId,
                    "Mortal semantic objects require every exact member."));
            }
        }
    }

    private static void ValidateMortalStringArray(
        JsonElement value,
        string context,
        string propertyName,
        string factionId,
        string code,
        bool allowEmpty,
        List<ValidationIssue> issues)
    {
        if (!value.TryGetProperty(propertyName, out var array) ||
            array.ValueKind != JsonValueKind.Array ||
            (!allowEmpty && array.GetArrayLength() == 0))
        {
            issues.Add(FactionIssue(
                $"{context}.{propertyName}",
                code,
                factionId,
                "Mortal semantic array fields must be explicit arrays of non-whitespace strings."));
            return;
        }

        var index = 0;
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(item.GetString()))
            {
                issues.Add(FactionIssue(
                    $"{context}.{propertyName}[{index}]",
                    code,
                    factionId,
                    "Mortal semantic array fields may contain only non-whitespace strings."));
            }

            index++;
        }
    }

    private static string? ReadMortalString(
        JsonElement value,
        string propertyName)
    {
        if (value.ValueKind != JsonValueKind.Object ||
            !value.TryGetProperty(propertyName, out var property) ||
            property.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var result = property.GetString();
        return string.IsNullOrWhiteSpace(result) ? null : result;
    }

    private static string? ReadFirstMortalString(
        JsonElement value,
        params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            var result = ReadMortalString(value, propertyName);
            if (!string.IsNullOrWhiteSpace(result))
                return result;
        }

        return null;
    }

    private static bool HasFormalMortalHierarchy(JsonElement structure) =>
        HasNestedArrayEntries(structure, "ranks", "branches") ||
        HasObjectArrayEntries(structure, "structuredBonuses");

    private static bool HasMortalResources(JsonElement resources) =>
        HasObjectArrayEntries(resources, "metaResources") ||
        HasObjectArrayEntries(resources, "strategicGoods");

    private static bool HasMortalCustomStates(JsonElement custom) =>
        HasObjectArrayEntries(custom, "customStates");

    private static bool HasObjectArrayEntries(
        JsonElement value,
        string propertyName) =>
        value.ValueKind == JsonValueKind.Object &&
        value.TryGetProperty(propertyName, out var array) &&
        array.ValueKind == JsonValueKind.Array &&
        array.GetArrayLength() > 0;

    private static bool HasNestedArrayEntries(
        JsonElement value,
        string objectName,
        string arrayName) =>
        value.ValueKind == JsonValueKind.Object &&
        value.TryGetProperty(objectName, out var nested) &&
        HasObjectArrayEntries(nested, arrayName);

    private static bool HasExactEmptyMortalHierarchy(JsonElement structure) =>
        structure.ValueKind == JsonValueKind.Object &&
        structure.TryGetProperty("governance", out var governance) &&
        governance.ValueKind == JsonValueKind.Object &&
        structure.TryGetProperty("leadership", out var leadership) &&
        leadership.ValueKind == JsonValueKind.Object &&
        HasExactEmptyNestedArray(structure, "ranks", "branches") &&
        HasExactEmptyArray(structure, "structuredBonuses");

    private static bool HasExactEmptyMortalResources(JsonElement resources) =>
        HasExactEmptyArray(resources, "metaResources") &&
        HasExactEmptyArray(resources, "strategicGoods");

    private static bool HasExactEmptyMortalCustomStates(JsonElement custom) =>
        HasExactEmptyArray(custom, "customStates");

    private static bool HasExactEmptyNestedArray(
        JsonElement value,
        string objectName,
        string arrayName) =>
        value.ValueKind == JsonValueKind.Object &&
        value.TryGetProperty(objectName, out var nested) &&
        HasExactEmptyArray(nested, arrayName);

    private static bool HasExactEmptyArray(
        JsonElement value,
        string propertyName) =>
        value.ValueKind == JsonValueKind.Object &&
        value.TryGetProperty(propertyName, out var array) &&
        array.ValueKind == JsonValueKind.Array &&
        array.GetArrayLength() == 0;

    private static bool HasExplicitEmptyProjectCarrier(JsonElement faction) =>
        HasExactEmptyArray(faction, "activeProjects") &&
        HasExactEmptyArray(faction, "completedProjects");

    private static bool HasMortalTerritoryOrInfluence(
        JsonElement faction,
        string factionId,
        MortalLocationAuthority locationAuthority) =>
        HasObjectArrayEntries(faction, "controlledTerritories") ||
        locationAuthority.Locations.Any(location =>
            LocationHasFactionControl(location.Location, factionId));

    private static bool HasExactEmptyMortalTerritory(
        JsonElement faction,
        string factionId,
        MortalLocationAuthority locationAuthority) =>
        HasExactEmptyArray(faction, "controlledTerritories") &&
        !locationAuthority.Locations.Any(location =>
            LocationHasFactionControl(location.Location, factionId));

    private static bool LocationHasFactionControl(
        JsonElement location,
        string factionId)
    {
        if (!location.TryGetProperty("factionControl", out var controls) ||
            controls.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        return controls.EnumerateArray().Any(control =>
            string.Equals(
                ReadMortalString(control, "factionId"),
                factionId,
                StringComparison.Ordinal));
    }

    private static bool HasMortalPlayerMembership(JsonElement faction)
    {
        return ReadMortalBoolean(faction, "isPlayerFaction") == true ||
               ReadMortalBoolean(faction, "isPlayerMember") == true ||
               ReadMortalString(faction, "playerRank") != null ||
               ReadMortalString(faction, "playerBranch") != null ||
               ReadMortalString(faction, "playerStrategyDirective") != null ||
               ReadMortalNumber(faction, "reputation") is { } reputation &&
               reputation != 0 ||
               ReadMortalString(faction, "reputationDescription") != null;
    }

    private static bool HasExactEmptyMortalPlayerMembership(JsonElement faction)
    {
        return ReadMortalBoolean(faction, "isPlayerFaction") == false &&
               ReadMortalBoolean(faction, "isPlayerMember") == false &&
               HasExactNull(faction, "playerRank") &&
               HasExactNull(faction, "playerBranch") &&
               HasExactNull(faction, "playerStrategyDirective") &&
               ReadMortalNumber(faction, "reputation") == 0 &&
               HasExactNull(faction, "reputationDescription");
    }

    private static bool? ReadMortalBoolean(
        JsonElement value,
        string propertyName)
    {
        if (!value.TryGetProperty(propertyName, out var property) ||
            property.ValueKind is not (
                JsonValueKind.True or JsonValueKind.False))
        {
            return null;
        }

        return property.GetBoolean();
    }

    private static decimal? ReadMortalNumber(
        JsonElement value,
        string propertyName)
    {
        if (!value.TryGetProperty(propertyName, out var property) ||
            property.ValueKind != JsonValueKind.Number ||
            !property.TryGetDecimal(out var result))
        {
            return null;
        }

        return result;
    }

    private static bool HasExactNull(
        JsonElement value,
        string propertyName) =>
        value.TryGetProperty(propertyName, out var property) &&
        property.ValueKind == JsonValueKind.Null;

    private static IReadOnlyList<JsonElement> ReadTargetProjectRows(
        JsonElement carrier,
        string propertyName,
        string factionId,
        bool carrierOwnsRows)
    {
        var result = new List<JsonElement>();
        if (!carrier.TryGetProperty(propertyName, out var rows) ||
            rows.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        foreach (var row in rows.EnumerateArray())
        {
            if (row.ValueKind != JsonValueKind.Object)
                continue;
            if (carrierOwnsRows ||
                string.Equals(
                    ReadMortalString(row, "factionId"),
                    factionId,
                    StringComparison.Ordinal))
            {
                result.Add(row.Clone());
            }
        }

        return result;
    }

    private static ValidationIssue FactionIssue(
        string path,
        string code,
        string factionId,
        string message,
        IReadOnlyList<string>? repairTargetFiles = null) =>
        new(
            path,
            IssueSeverity.Error,
            message,
            code: code,
            actor: $"mortal_faction:{factionId}",
            section: "FactionMaterialization",
            repairHint:
            "Repair only this Mortal faction's semantic or atomic materialization bundle.",
            repairTargetFiles: repairTargetFiles);

    private sealed record MortalFactionMaterializationTarget(
        JsonElement Faction,
        string Context,
        string FactionId,
        bool HasExplicitNullFactionId);

    private sealed record MortalLocationAuthority(
        HashSet<string> LocationIds,
        IReadOnlyList<MortalLocationAuthorityEntry> Locations);

    private sealed record MortalLocationAuthorityEntry(
        JsonElement Location,
        string RootPath);

    private sealed record MortalNpcAuthority(
        HashSet<string> NpcIds,
        IReadOnlyList<JsonElement> Npcs);

    private sealed record MortalFactionSidecars(
        IReadOnlyDictionary<string, JsonElement> Structure,
        IReadOnlyDictionary<string, string> StructureContexts,
        IReadOnlyDictionary<string, JsonElement> Resources,
        IReadOnlyDictionary<string, string> ResourceContexts,
        IReadOnlyDictionary<string, JsonElement> Custom,
        IReadOnlyDictionary<string, string> CustomContexts,
        IReadOnlyList<JsonElement> ActiveProjects,
        IReadOnlyList<JsonElement> CompletedProjects)
    {
        internal static MortalFactionSidecars Empty { get; } =
            new(
                new Dictionary<string, JsonElement>(StringComparer.Ordinal),
                new Dictionary<string, string>(StringComparer.Ordinal),
                new Dictionary<string, JsonElement>(StringComparer.Ordinal),
                new Dictionary<string, string>(StringComparer.Ordinal),
                new Dictionary<string, JsonElement>(StringComparer.Ordinal),
                new Dictionary<string, string>(StringComparer.Ordinal),
                Array.Empty<JsonElement>(),
                Array.Empty<JsonElement>());
    }
}
