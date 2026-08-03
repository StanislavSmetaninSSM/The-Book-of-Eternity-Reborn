using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
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
        var preTurnChronicleIds = await ReadMortalChronicleFactionIdsAsync(
            preTurn: true);

        var sidecars = rawBeforeNormalization
            ? MortalFactionSidecars.Empty
            : await ReadMortalFactionSidecarsAsync(
                effectiveFactionIds,
                issues);
        if (rawBeforeNormalization &&
            snapshotLookup.Status ==
                ValidatedPendingTurnSnapshotStatus.Usable &&
            snapshotLookup.Manifest != null)
        {
            await ValidateRawMortalPromotionHistoryAsync(
                currentRoot.Value,
                full,
                preTurn,
                snapshotLookup.Manifest,
                issues);
        }

        foreach (var factionId in canonical.Keys
                     .Concat(full.Keys)
                     .Distinct(StringComparer.Ordinal))
        {
            canonical.TryGetValue(factionId, out var canonicalTarget);
            full.TryGetValue(factionId, out var fullTarget);
            preTurn.TryGetValue(factionId, out var previous);

            var previousHadReceipt = HasMortalMaterializationReceipt(
                previous?.Faction);
            var currentHasReceipt = HasMortalMaterializationReceipt(
                canonicalTarget?.Faction) ||
                HasMortalMaterializationReceipt(fullTarget?.Faction);
            var fullIsCreationOrPromotion =
                fullTarget != null && !previousHadReceipt;
            var canonicalIsCreationOrPromotion =
                canonicalTarget != null &&
                hasUsablePreTurnAuthority &&
                !previousHadReceipt &&
                (previous == null ||
                 !FactionJsonSemanticallyEqual(
                     canonicalTarget.Faction,
                     previous.Faction));

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

            if (!currentHasReceipt &&
                !fullIsCreationOrPromotion &&
                !canonicalIsCreationOrPromotion)
            {
                continue;
            }

            if (rawBeforeNormalization && fullTarget != null)
            {
                ValidateRawMortalFactionMaterialization(
                    fullTarget,
                    fullIsCreationOrPromotion,
                    fullIsCreationOrPromotion &&
                    previous != null &&
                    preTurnChronicleIds.Contains(factionId),
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
    }

    private bool TryClassifyMortalFullCarrier(
        JsonElement faction,
        out string factionId,
        out bool alreadyMaterialized,
        out bool collidesWithPreTurnId)
    {
        factionId = ReadFirstMortalString(
            faction,
            "factionId",
            "initialId") ?? string.Empty;
        alreadyMaterialized = false;
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

            alreadyMaterialized =
                HasMortalMaterializationReceipt(previous.Faction);
            collidesWithPreTurnId =
                faction.TryGetProperty(
                    "factionId",
                    out var factionIdNode) &&
                factionIdNode.ValueKind == JsonValueKind.Null &&
                ReadMortalString(faction, "initialId") != null;
            return !alreadyMaterialized;
        }
        catch (JsonException)
        {
            return true;
        }
    }

    private bool HasPreTurnMortalChronicle(string factionId)
    {
        var json = ReadPreTurnTrackedFileSync(MortalFactionChroniclesPath);
        if (string.IsNullOrWhiteSpace(json))
            return false;
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty(
                    "entries",
                    out var entries) ||
                entries.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            return entries.EnumerateArray().Any(entry =>
                string.Equals(
                    ReadMortalString(entry, "factionId"),
                    factionId,
                    StringComparison.Ordinal) &&
                ReadFirstMortalString(
                    entry,
                    "entry",
                    "chronicle",
                    "text") != null);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private bool CurrentMortalFactionHasMaterializationReceipt(string factionId)
    {
        var json = ReadCurrentTrackedFileSync(
            MortalFactionMaterializationPath);
        if (string.IsNullOrWhiteSpace(json))
            return false;
        try
        {
            using var document = JsonDocument.Parse(json);
            var factions = ReadMortalFactionTargets(
                document.RootElement,
                "factions",
                useInitialId: false);
            return factions.TryGetValue(factionId, out var faction) &&
                   HasMortalMaterializationReceipt(faction.Faction);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private void ValidateRawMortalFactionMaterialization(
        MortalFactionMaterializationTarget target,
        bool isCreationOrPromotion,
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
            requireEnvelope: isCreationOrPromotion ||
                             HasMortalMaterializationReceipt(target.Faction),
            deferEvidenceConsistency: false));
    }

    private async Task ValidateRawMortalPromotionHistoryAsync(
        JsonElement currentRoot,
        IReadOnlyDictionary<string, MortalFactionMaterializationTarget>
            fullTargets,
        IReadOnlyDictionary<string, MortalFactionMaterializationTarget>
            preTurnTargets,
        ValidationPendingTurnSnapshotManifest manifest,
        List<ValidationIssue> issues)
    {
        var promotionIds = fullTargets
            .Where(entry =>
                preTurnTargets.TryGetValue(entry.Key, out var previous) &&
                !HasMortalMaterializationReceipt(previous.Faction))
            .Select(entry => entry.Key)
            .ToHashSet(StringComparer.Ordinal);
        if (promotionIds.Count == 0)
            return;

        var preTurnRootElement = await ReadRawMortalTouchRootAsync(
            manifest,
            MortalFactionMaterializationPath,
            preTurn: true,
            issues);
        if (preTurnRootElement is not
            { ValueKind: JsonValueKind.Object })
        {
            return;
        }

        var currentRootNode = JsonNode.Parse(
            currentRoot.GetRawText()) as JsonObject;
        var preTurnRootNode = JsonNode.Parse(
            preTurnRootElement.Value.GetRawText()) as JsonObject;
        if (currentRootNode == null || preTurnRootNode == null)
            return;

        FactionCoreChangesContract.Evaluation? coreEvaluation = null;
        var projectedCurrentRoot = currentRootNode
            .DeepClone()
            .AsObject();
        if (FactionCoreChangesContract.HasCommandLikeProperty(
                currentRootNode))
        {
            var authority = await ReadFactionCoreChangesAuthorityAsync(
                currentRootNode,
                preTurnRootNode,
                manifest);
            coreEvaluation = FactionCoreChangesContract.Evaluate(
                currentRootNode,
                preTurnRootNode,
                authority);
            if (coreEvaluation.CanApply)
            {
                FactionCoreChangesContract.Apply(
                    projectedCurrentRoot,
                    coreEvaluation);
            }
        }

        Dictionary<string, MortalFactionMaterializationTarget>
            projectedFullTargets;
        using (var projectedDocument = JsonDocument.Parse(
                   projectedCurrentRoot.ToJsonString()))
        {
            projectedFullTargets = ReadMortalFactionTargets(
                projectedDocument.RootElement,
                "factionDataChanges",
                useInitialId: true);
        }

        var preTurnStructure = await ReadRawMortalTouchRootAsync(
            manifest,
            MortalFactionStructurePath,
            preTurn: true,
            issues);
        var preTurnResources = await ReadRawMortalTouchRootAsync(
            manifest,
            MortalFactionResourcesPath,
            preTurn: true,
            issues);
        var preTurnProjects = await ReadRawMortalTouchRootAsync(
            manifest,
            MortalFactionProjectsPath,
            preTurn: true,
            issues);
        var preTurnCustom = await ReadRawMortalTouchRootAsync(
            manifest,
            MortalFactionCustomPath,
            preTurn: true,
            issues);
        var historyIndex = BuildMortalPromotionHistoryIndex(
            preTurnStructure,
            preTurnResources,
            preTurnProjects,
            preTurnCustom);
        var externalTouches =
            await ReadRawMortalExternalFactionTouchSummaryAsync(issues);

        foreach (var factionId in promotionIds)
        {
            if (!preTurnTargets.TryGetValue(
                    factionId,
                    out var previous) ||
                !projectedFullTargets.TryGetValue(
                    factionId,
                    out var promoted))
            {
                continue;
            }

            var historicalCore = JsonNode.Parse(
                previous.Faction.GetRawText()) as JsonObject;
            var promotedCore = JsonNode.Parse(
                promoted.Faction.GetRawText()) as JsonObject;
            if (historicalCore == null || promotedCore == null)
                continue;

            var expectedCore = historicalCore
                .DeepClone()
                .AsObject();
            if (coreEvaluation?.CanApply == true)
            {
                var expectedRoot = new JsonObject
                {
                    ["factions"] = new JsonArray(expectedCore)
                };
                FactionCoreChangesContract.Apply(
                    expectedRoot,
                    coreEvaluation);
                expectedCore = expectedRoot["factions"]![0]!
                    .AsObject();
            }

            var changedPath = FindMortalPromotionHistoryChange(
                factionId,
                promoted.Context,
                historicalCore,
                expectedCore,
                promotedCore,
                historyIndex,
                coreEvaluation,
                externalTouches);
            if (changedPath == null)
                continue;

            issues.Add(FactionIssue(
                changedPath,
                "faction_materialization_promotion_history_changed",
                factionId,
                "Mortal legacy promotion must preserve every validated pre-turn historical value except a delta proven by a successfully validated narrow command."));
        }
    }

    private static string? FindMortalPromotionHistoryChange(
        string factionId,
        string promotionContext,
        JsonObject historicalCore,
        JsonObject expectedCore,
        JsonObject promotedCore,
        MortalPromotionHistoryIndex historyIndex,
        FactionCoreChangesContract.Evaluation? coreEvaluation,
        RawMortalExternalFactionTouchSummary externalTouches)
    {
        foreach (var property in historicalCore)
        {
            if (string.Equals(
                    property.Key,
                    "scribeChronicle",
                    StringComparison.Ordinal) ||
                !promotedCore.TryGetPropertyValue(
                    property.Key,
                    out var promotedValue) ||
                MortalHistoricalValueIsSubset(
                    expectedCore[property.Key],
                    promotedValue,
                    property.Key))
            {
                continue;
            }

            return $"{promotionContext}.{property.Key}";
        }

        historyIndex.StructureByFaction.TryGetValue(
            factionId,
            out var structure);
        foreach (var field in new[]
                 {
                     "governance",
                     "leadership",
                     "ranks",
                     "structuredBonuses"
                 })
        {
            JsonNode? expected = null;
            if (historicalCore.ContainsKey(field) ||
                ValidatedCoreChangeSuppliesField(
                    coreEvaluation,
                    factionId,
                    field))
            {
                expected = expectedCore[field];
            }
            else if (structure?.TryGetPropertyValue(
                         field,
                         out var historicalValue) == true)
            {
                expected = historicalValue?.DeepClone();
            }

            if (expected == null)
                continue;
            if (!promotedCore.TryGetPropertyValue(
                    field,
                    out var promotedValue) ||
                !MortalHistoricalValueIsSubset(
                    expected,
                    promotedValue,
                    field))
            {
                return $"{promotionContext}.{field}";
            }
        }

        var expectedResources = historicalCore["resources"]?.DeepClone();
        if (expectedResources == null)
        {
            historyIndex.ResourcesByFaction.TryGetValue(
                factionId,
                out var historicalResources);
            expectedResources = BuildMortalHistoricalResources(
                historicalResources);
        }
        if (expectedResources != null &&
            (!promotedCore.TryGetPropertyValue(
                 "resources",
                 out var promotedResourceValue) ||
             !MortalHistoricalValueIsSubset(
                 expectedResources,
                 promotedResourceValue,
                 "resources")))
        {
            return $"{promotionContext}.resources";
        }

        var expectedActiveProjects =
            BuildMortalHistoricalProjectArray(
                historicalCore["activeProjects"],
                historyIndex.ActiveProjectsByFaction.TryGetValue(
                    factionId,
                    out var activeProjects)
                    ? activeProjects
                    : null);
        var expectedCompletedProjects =
            BuildMortalHistoricalProjectArray(
                historicalCore["completedProjects"],
                historyIndex.CompletedProjectsByFaction.TryGetValue(
                    factionId,
                    out var completedProjects)
                    ? completedProjects
                    : null);
        if (!MortalHistoricalProjectRowsPreserved(
                expectedActiveProjects,
                promotedCore["activeProjects"],
                factionId))
        {
            return $"{promotionContext}.activeProjects";
        }

        if (!MortalHistoricalProjectRowsPreserved(
                expectedCompletedProjects,
                promotedCore["completedProjects"],
                factionId))
        {
            return $"{promotionContext}.completedProjects";
        }

        var expectedCustomStates =
            historicalCore["customStates"]?.DeepClone();
        if (expectedCustomStates == null)
        {
            historyIndex.CustomByFaction.TryGetValue(
                factionId,
                out var historicalCustom);
            expectedCustomStates =
                historicalCustom?.TryGetPropertyValue(
                    "customStates",
                    out var customStates) == true
                    ? customStates?.DeepClone()
                    : null;
        }
        if (expectedCustomStates != null &&
            (!promotedCore.TryGetPropertyValue(
                 "customStates",
                 out var promotedCustomStates) ||
             !MortalHistoricalValueIsSubset(
                 expectedCustomStates,
                 promotedCustomStates,
                 "customStates")))
        {
            return $"{promotionContext}.customStates";
        }

        if (externalTouches.RewrittenHistoricalSources.TryGetValue(
                factionId,
                out var changedSource))
        {
            return changedSource;
        }

        return null;
    }

    private static bool ValidatedCoreChangeSuppliesField(
        FactionCoreChangesContract.Evaluation? evaluation,
        string factionId,
        string field) =>
        evaluation?.CanApply == true &&
        evaluation.Plans.Any(plan =>
            string.Equals(
                plan.FactionId,
                factionId,
                StringComparison.Ordinal) &&
            plan.Command["governanceAndLeadership"]
                is JsonObject governance &&
            governance.ContainsKey(field));

    private static MortalPromotionHistoryIndex
        BuildMortalPromotionHistoryIndex(
            JsonElement? structureRoot,
            JsonElement? resourceRoot,
            JsonElement? projectRoot,
            JsonElement? customRoot) =>
        new(
            BuildMortalPromotionSidecarIndex(structureRoot),
            BuildMortalPromotionSidecarIndex(resourceRoot),
            BuildMortalPromotionProjectIndex(
                projectRoot,
                "activeProjects"),
            BuildMortalPromotionProjectIndex(
                projectRoot,
                "completedProjects"),
            BuildMortalPromotionSidecarIndex(customRoot));

    private static IReadOnlyDictionary<string, JsonObject>
        BuildMortalPromotionSidecarIndex(JsonElement? root)
    {
        var result = new Dictionary<string, JsonObject>(
            StringComparer.Ordinal);
        if (root is not { ValueKind: JsonValueKind.Object } ||
            !root.Value.TryGetProperty("entries", out var entries) ||
            entries.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        foreach (var entry in entries.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object)
                continue;
            var factionId = ReadMortalString(entry, "factionId");
            if (string.IsNullOrWhiteSpace(factionId) ||
                JsonNode.Parse(entry.GetRawText())
                    is not JsonObject candidate)
            {
                continue;
            }

            if (result.TryGetValue(factionId, out var existing))
                MergeMortalHistoryObject(existing, candidate);
            else
                result[factionId] = candidate;
        }

        return result;
    }

    private static IReadOnlyDictionary<
            string,
            IReadOnlyDictionary<string, JsonObject>>
        BuildMortalPromotionProjectIndex(
            JsonElement? root,
            string propertyName)
    {
        var mutable = new Dictionary<
            string,
            Dictionary<string, JsonObject>>(
            StringComparer.Ordinal);
        if (root is not { ValueKind: JsonValueKind.Object } ||
            !root.Value.TryGetProperty(propertyName, out var projects) ||
            projects.ValueKind != JsonValueKind.Array)
        {
            return new Dictionary<
                string,
                IReadOnlyDictionary<string, JsonObject>>(
                StringComparer.Ordinal);
        }

        foreach (var project in projects.EnumerateArray())
        {
            if (project.ValueKind != JsonValueKind.Object)
                continue;
            var factionId = ReadMortalString(project, "factionId");
            var projectId = ReadMortalString(project, "projectId");
            if (string.IsNullOrWhiteSpace(factionId) ||
                string.IsNullOrWhiteSpace(projectId) ||
                JsonNode.Parse(project.GetRawText())
                    is not JsonObject candidate)
            {
                continue;
            }

            if (!mutable.TryGetValue(factionId, out var byProject))
            {
                byProject = new Dictionary<string, JsonObject>(
                    StringComparer.OrdinalIgnoreCase);
                mutable[factionId] = byProject;
            }

            if (byProject.TryGetValue(projectId, out var existing))
                MergeMortalHistoryObject(existing, candidate);
            else
                byProject[projectId] = candidate;
        }

        return mutable.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyDictionary<string, JsonObject>)pair.Value,
            StringComparer.Ordinal);
    }

    private static JsonObject? BuildMortalHistoricalResources(
        JsonObject? entry)
    {
        if (entry == null)
            return null;

        var result = new JsonObject
        {
            ["metaResources"] = new JsonArray(),
            ["strategicGoods"] = new JsonArray()
        };
        foreach (var field in new[]
                 {
                     "metaResources",
                     "strategicGoods"
                 })
        {
            if (entry.TryGetPropertyValue(field, out var value))
                result[field] = value?.DeepClone();
        }

        return result;
    }

    private static JsonArray BuildMortalHistoricalProjectArray(
        JsonNode? coreProjects,
        IReadOnlyDictionary<string, JsonObject>? indexedProjects)
    {
        if (coreProjects is JsonArray coreArray)
            return coreArray.DeepClone().AsArray();

        var result = new JsonArray();
        if (indexedProjects == null)
            return result;

        foreach (var project in indexedProjects.Values)
            result.Add(project.DeepClone());
        return result;
    }

    private static bool MortalHistoricalProjectRowsPreserved(
        JsonNode? historical,
        JsonNode? promoted,
        string factionId)
    {
        if (historical is not JsonArray historicalArray ||
            promoted is not JsonArray promotedArray)
        {
            return true;
        }

        var historicalByIdentity =
            ProjectEffectiveMortalProjectHistory(
                historicalArray,
                factionId);
        var promotedByIdentity =
            ProjectEffectiveMortalProjectHistory(
                promotedArray,
                factionId);
        foreach (var (identity, historicalEntry) in historicalByIdentity)
        {
            if (!promotedByIdentity.TryGetValue(
                    identity,
                    out var promotedEntry))
            {
                // Project omission remains snapshot/merge-preserved.
                continue;
            }

            if (!MortalHistoricalValueIsSubset(
                    historicalEntry,
                    promotedEntry))
            {
                return false;
            }
        }

        return true;
    }

    private static IReadOnlyDictionary<string, JsonObject>
        ProjectEffectiveMortalProjectHistory(
            JsonArray projects,
            string factionId)
    {
        var result = new Dictionary<string, JsonObject>(
            StringComparer.OrdinalIgnoreCase);
        foreach (var entry in projects.OfType<JsonObject>())
        {
            var explicitFactionId =
                ReadMortalHistoryString(entry, "factionId") ??
                ReadMortalHistoryString(entry, "initialFactionId");
            if (!string.IsNullOrWhiteSpace(explicitFactionId) &&
                !string.Equals(
                    explicitFactionId,
                    factionId,
                    StringComparison.Ordinal))
            {
                continue;
            }

            var projectId =
                ReadMortalHistoryString(entry, "projectId");
            if (string.IsNullOrWhiteSpace(projectId))
                continue;
            var normalized =
                NormalizeMortalProjectHistoryEntry(entry)
                    as JsonObject;
            if (normalized == null)
                continue;

            if (result.TryGetValue(projectId, out var existing))
                MergeMortalHistoryObject(existing, normalized);
            else
                result[projectId] = normalized;
        }

        return result;
    }

    private static JsonNode? NormalizeMortalProjectHistoryEntry(
        JsonNode? entry)
    {
        var clone = entry?.DeepClone();
        if (clone is not JsonObject value)
            return clone;

        value.Remove("factionId");
        value.Remove("initialFactionId");
        value.Remove("factionName");
        return value;
    }

    private static bool MortalHistoricalValueIsSubset(
        JsonNode? historical,
        JsonNode? promoted,
        string? collectionName = null)
    {
        if (historical == null)
            return true;
        if (promoted == null)
            return false;

        if (historical is JsonObject historicalObject)
        {
            if (promoted is not JsonObject promotedObject)
                return false;
            foreach (var property in historicalObject)
            {
                if (property.Value == null)
                    continue;
                if (!promotedObject.TryGetPropertyValue(
                        property.Key,
                        out var promotedValue) ||
                    !MortalHistoricalValueIsSubset(
                        property.Value,
                        promotedValue,
                        property.Key))
                {
                    return false;
                }
            }

            return true;
        }

        if (historical is JsonArray historicalArray)
        {
            return promoted is JsonArray promotedArray &&
                   MortalHistoricalArrayIsSubset(
                       historicalArray,
                       promotedArray,
                       collectionName);
        }

        return JsonNode.DeepEquals(historical, promoted);
    }

    private static bool MortalHistoricalArrayIsSubset(
        JsonArray historical,
        JsonArray promoted,
        string? collectionName)
    {
        var mergeIdentityFields =
            MortalNormalizerIdentityFields(collectionName);
        if (mergeIdentityFields != null)
        {
            return MortalMergedHistoricalArrayIsSubset(
                historical,
                promoted,
                mergeIdentityFields);
        }

        var exactIdentityField = collectionName switch
        {
            "relations" => "targetFactionId",
            "controlledTerritories" => "locationId",
            _ => null
        };
        return exactIdentityField != null
            ? MortalExactIdentityArrayIsSubset(
                historical,
                promoted,
                exactIdentityField)
            : MortalUnidentifiedHistoryMultisetIsSubset(
                historical,
                promoted);
    }

    private static string[]? MortalNormalizerIdentityFields(
        string? collectionName) =>
        collectionName switch
        {
            "branches" => new[] { "branchId", "displayName" },
            "ranks" => new[]
            {
                "rankNameMale",
                "rankNameFemale",
                "name"
            },
            "structuredBonuses" => new[]
            {
                "bonusId",
                "description",
                "bonusType",
                "target"
            },
            "metaResources" => new[] { "resourceName" },
            "strategicGoods" => new[] { "resourceName" },
            "customStates" => new[] { "stateId", "name", "title" },
            "activeProjects" => new[] { "projectId" },
            "completedProjects" => new[] { "projectId" },
            _ => null
        };

    private static bool MortalMergedHistoricalArrayIsSubset(
        JsonArray historical,
        JsonArray promoted,
        IReadOnlyList<string> identityFields)
    {
        var historicalProjection =
            ProjectEffectiveMortalHistoryArray(
                historical,
                identityFields);
        var promotedProjection =
            ProjectEffectiveMortalHistoryArray(
                promoted,
                identityFields);

        foreach (var historicalEntry
                 in historicalProjection.Identified)
        {
            var promotedEntry =
                promotedProjection.Identified.FirstOrDefault(candidate =>
                    MortalHistoryEntriesShareIdentity(
                        historicalEntry,
                        candidate,
                        identityFields,
                        StringComparison.OrdinalIgnoreCase));
            if (promotedEntry == null ||
                !MortalHistoricalValueIsSubset(
                    historicalEntry,
                    promotedEntry))
            {
                return false;
            }
        }

        return MortalHistoryFingerprintCountsAreSubset(
            historicalProjection.Unidentified,
            promotedProjection.Unidentified);
    }

    private static MortalEffectiveHistoryArray
        ProjectEffectiveMortalHistoryArray(
            JsonArray source,
            IReadOnlyList<string> identityFields)
    {
        var identified = new List<JsonObject>();
        var unidentified =
            new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var entry in source)
        {
            if (entry is not JsonObject candidate ||
                !MortalHistoryHasIdentity(
                    candidate,
                    identityFields))
            {
                IncrementMortalHistoryFingerprint(
                    unidentified,
                    MortalCanonicalHistoryFingerprint(entry));
                continue;
            }

            var existing = identified.FirstOrDefault(item =>
                MortalHistoryEntriesShareIdentity(
                    item,
                    candidate,
                    identityFields,
                    StringComparison.OrdinalIgnoreCase));
            if (existing != null)
                MergeMortalHistoryObject(existing, candidate);
            else
                identified.Add(candidate.DeepClone().AsObject());
        }

        return new MortalEffectiveHistoryArray(
            identified,
            unidentified);
    }

    private static bool MortalExactIdentityArrayIsSubset(
        JsonArray historical,
        JsonArray promoted,
        string identityField)
    {
        var historicalByIdentity =
            BuildMortalExactIdentityHistoryGroups(
                historical,
                identityField);
        var promotedByIdentity =
            BuildMortalExactIdentityHistoryGroups(
                promoted,
                identityField);
        foreach (var (identity, historicalEntries)
                 in historicalByIdentity)
        {
            if (!promotedByIdentity.TryGetValue(
                    identity,
                    out var promotedEntries) ||
                promotedEntries.Count < historicalEntries.Count ||
                promotedEntries.Any(promotedEntry =>
                    historicalEntries.Any(historicalEntry =>
                        !MortalHistoricalValueIsSubset(
                            historicalEntry,
                            promotedEntry))))
            {
                return false;
            }
        }

        var promotedUnidentified =
            BuildMortalUnidentifiedHistoryCounts(
                promoted,
                new[] { identityField });
        var historicalUnidentified =
            BuildMortalUnidentifiedHistoryCounts(
                historical,
                new[] { identityField });

        return MortalHistoryFingerprintCountsAreSubset(
            historicalUnidentified,
            promotedUnidentified);
    }

    private static IReadOnlyDictionary<string, List<JsonObject>>
        BuildMortalExactIdentityHistoryGroups(
            JsonArray source,
            string identityField)
    {
        var result =
            new Dictionary<string, List<JsonObject>>(
                StringComparer.Ordinal);
        foreach (var entry in source.OfType<JsonObject>())
        {
            var identity =
                ReadMortalHistoryString(entry, identityField);
            if (string.IsNullOrWhiteSpace(identity))
                continue;
            if (!result.TryGetValue(identity, out var entries))
            {
                entries = new List<JsonObject>();
                result[identity] = entries;
            }

            entries.Add(entry);
        }

        return result;
    }

    private static bool MortalUnidentifiedHistoryMultisetIsSubset(
        JsonArray historical,
        JsonArray promoted)
    {
        var historicalCounts =
            BuildMortalUnidentifiedHistoryCounts(historical);
        var promotedCounts =
            BuildMortalUnidentifiedHistoryCounts(promoted);
        return MortalHistoryFingerprintCountsAreSubset(
            historicalCounts,
            promotedCounts);
    }

    private static Dictionary<string, int>
        BuildMortalUnidentifiedHistoryCounts(
            JsonArray source,
            IReadOnlyList<string>? excludedIdentityFields = null)
    {
        var counts =
            new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var entry in source)
        {
            if (excludedIdentityFields != null &&
                entry is JsonObject candidate &&
                MortalHistoryHasIdentity(
                    candidate,
                    excludedIdentityFields))
            {
                continue;
            }

            IncrementMortalHistoryFingerprint(
                counts,
                MortalCanonicalHistoryFingerprint(entry));
        }

        return counts;
    }

    private static bool MortalHistoryFingerprintCountsAreSubset(
        IReadOnlyDictionary<string, int> historical,
        IReadOnlyDictionary<string, int> promoted)
    {
        foreach (var (fingerprint, count) in historical)
        {
            if (!promoted.TryGetValue(
                    fingerprint,
                    out var promotedCount) ||
                promotedCount < count)
            {
                return false;
            }
        }

        return true;
    }

    private static bool MortalHistoryHasIdentity(
        JsonObject value,
        IReadOnlyList<string> identityFields) =>
        identityFields.Any(field =>
            !string.IsNullOrWhiteSpace(
                ReadMortalHistoryString(value, field)));

    private static bool MortalHistoryEntriesShareIdentity(
        JsonObject left,
        JsonObject right,
        IReadOnlyList<string> identityFields,
        StringComparison comparison) =>
        identityFields.Any(field =>
        {
            var leftIdentity =
                ReadMortalHistoryString(left, field);
            var rightIdentity =
                ReadMortalHistoryString(right, field);
            return !string.IsNullOrWhiteSpace(leftIdentity) &&
                   !string.IsNullOrWhiteSpace(rightIdentity) &&
                   string.Equals(
                       leftIdentity,
                       rightIdentity,
                       comparison);
        });

    private static string? ReadMortalHistoryString(
        JsonObject value,
        string propertyName) =>
        value[propertyName] is JsonValue identity &&
        identity.TryGetValue<string>(out var result) &&
        !string.IsNullOrWhiteSpace(result)
            ? result
            : null;

    private static void MergeMortalHistoryObject(
        JsonObject target,
        JsonObject source)
    {
        foreach (var property in source)
            target[property.Key] = property.Value?.DeepClone();
    }

    private static void IncrementMortalHistoryFingerprint(
        Dictionary<string, int> counts,
        string fingerprint)
    {
        counts.TryGetValue(fingerprint, out var count);
        counts[fingerprint] = count + 1;
    }

    private static string MortalCanonicalHistoryFingerprint(
        JsonNode? value)
    {
        var builder = new StringBuilder();
        AppendMortalCanonicalHistoryFingerprint(builder, value);
        return builder.ToString();
    }

    private static void AppendMortalCanonicalHistoryFingerprint(
        StringBuilder builder,
        JsonNode? value)
    {
        switch (value)
        {
            case null:
                builder.Append("null");
                return;
            case JsonObject obj:
                builder.Append('{');
                var firstProperty = true;
                foreach (var property in obj.OrderBy(
                             property => property.Key,
                             StringComparer.Ordinal))
                {
                    if (!firstProperty)
                        builder.Append(',');
                    firstProperty = false;
                    builder.Append(JsonSerializer.Serialize(property.Key));
                    builder.Append(':');
                    AppendMortalCanonicalHistoryFingerprint(
                        builder,
                        property.Value);
                }

                builder.Append('}');
                return;
            case JsonArray array:
                builder.Append('[');
                for (var index = 0; index < array.Count; index++)
                {
                    if (index > 0)
                        builder.Append(',');
                    AppendMortalCanonicalHistoryFingerprint(
                        builder,
                        array[index]);
                }

                builder.Append(']');
                return;
            default:
                builder.Append(value.ToJsonString());
                return;
        }
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
                "A new/promoted Mortal faction requires existing history or at least one turn-prefixed creation chronicle entry."));
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

    private async Task<HashSet<string>>
        ReadRawMortalExternalFactionTouchIdsAsync(
            List<ValidationIssue> issues)
    {
        var summary =
            await ReadRawMortalExternalFactionTouchSummaryAsync(issues);
        var touched = new HashSet<string>(
            summary.CommandTargetIds,
            StringComparer.Ordinal);
        touched.UnionWith(summary.ChangedAuthorityIds);
        return touched;
    }

    private async Task<RawMortalExternalFactionTouchSummary>
        ReadRawMortalExternalFactionTouchSummaryAsync(
            List<ValidationIssue> issues)
    {
        var lookup = await LoadValidatedPendingTurnSnapshotLookupAsync();
        if (lookup.Status != ValidatedPendingTurnSnapshotStatus.Usable ||
            lookup.Manifest == null)
        {
            return RawMortalExternalFactionTouchSummary.Empty;
        }

        var currentEvidence =
            new Dictionary<string, List<RawFactionTouchEvidence>>(
                StringComparer.Ordinal);
        var preTurnEvidence =
            new Dictionary<string, List<RawFactionTouchEvidence>>(
                StringComparer.Ordinal);
        var commandTouches = new HashSet<string>(StringComparer.Ordinal);
        var sidecarSurfaces = new[]
        {
            (
                MortalFactionStructurePath,
                CanonicalArrays: new[] { "entries" },
                CommandArrays: new[]
                {
                    "factionRankChanges",
                    "factionBonusChanges"
                }),
            (
                MortalFactionResourcesPath,
                CanonicalArrays: new[] { "entries" },
                CommandArrays: new[] { "factionResourceChanges" }),
            (
                MortalFactionProjectsPath,
                CanonicalArrays: new[]
                {
                    "activeProjects",
                    "completedProjects"
                },
                CommandArrays: new[]
                {
                    "factionProjectUpdates",
                    "completeFactionProjects"
                }),
            (
                MortalFactionCustomPath,
                CanonicalArrays: new[] { "entries" },
                CommandArrays: new[] { "factionCustomStateChanges" }),
            (
                MortalFactionChroniclesPath,
                CanonicalArrays: new[] { "entries" },
                CommandArrays: new[] { "factionChronicleUpdates" })
        };

        foreach (var surface in sidecarSurfaces)
        {
            var currentRoot = await ReadRawMortalTouchRootAsync(
                lookup.Manifest,
                surface.Item1,
                preTurn: false,
                issues);
            var preTurnRoot = await ReadRawMortalTouchRootAsync(
                lookup.Manifest,
                surface.Item1,
                preTurn: true,
                issues);
            CollectRawMortalTargetArrays(
                currentRoot,
                surface.Item1,
                surface.CanonicalArrays,
                currentEvidence);
            CollectRawMortalTargetArrays(
                preTurnRoot,
                surface.Item1,
                surface.CanonicalArrays,
                preTurnEvidence);
            CollectRawMortalCommandTouches(
                currentRoot,
                surface.CommandArrays,
                commandTouches);
        }

        foreach (var path in new[]
                 {
                     "game_state/world/current_location.json",
                     "game_state/world/world_map.json"
                 })
        {
            CollectRawMortalLocationTouchEvidence(
                await ReadRawMortalTouchRootAsync(
                    lookup.Manifest,
                    path,
                    preTurn: false,
                    issues),
                currentEvidence);
            CollectRawMortalLocationTouchEvidence(
                await ReadRawMortalTouchRootAsync(
                    lookup.Manifest,
                    path,
                    preTurn: true,
                    issues),
                preTurnEvidence);
        }

        var currentNpcRoot = await ReadRawMortalTouchRootAsync(
            lookup.Manifest,
            MortalNpcCorePath,
            preTurn: false,
            issues);
        var preTurnNpcRoot = await ReadRawMortalTouchRootAsync(
            lookup.Manifest,
            MortalNpcCorePath,
            preTurn: true,
            issues);
        CollectRawMortalNpcTouchEvidence(
            ProjectRawMortalNpcAffiliationCommands(
                currentNpcRoot,
                preTurnNpcRoot),
            currentEvidence);
        CollectRawMortalNpcTouchEvidence(
            preTurnNpcRoot,
            preTurnEvidence);

        return new RawMortalExternalFactionTouchSummary(
            FindChangedRawFactionTouchIds(
                currentEvidence,
                preTurnEvidence),
            FindRewrittenRawMortalHistorySources(
                currentEvidence,
                preTurnEvidence),
            commandTouches);
    }

    private static Dictionary<string, string>
        FindRewrittenRawMortalHistorySources(
            IReadOnlyDictionary<string, List<RawFactionTouchEvidence>>
                current,
            IReadOnlyDictionary<string, List<RawFactionTouchEvidence>>
                preTurn)
    {
        var result = new Dictionary<string, string>(
            StringComparer.Ordinal);
        foreach (var (factionId, historicalItems) in preTurn)
        {
            current.TryGetValue(factionId, out var currentItems);
            var currentEvidence = currentItems ?? [];
            var processedSidecarSources =
                new HashSet<string>(StringComparer.Ordinal);
            var processedProjects =
                new List<(string SourceIdentity, string ProjectId)>();

            foreach (var historical in historicalItems)
            {
                if (historical.SourceIdentity.StartsWith(
                        MortalFactionChroniclesPath,
                        StringComparison.Ordinal))
                {
                    // Chronicle normalization is additive and always begins
                    // from the validated snapshot, so a raw entry cannot
                    // replace or remove historical text.
                    continue;
                }

                if (MortalRawHistorySourceUsesOrderedSidecarMerge(
                        historical.SourceIdentity))
                {
                    if (!processedSidecarSources.Add(
                            historical.SourceIdentity))
                    {
                        continue;
                    }

                    var historicalPayload =
                        MergeRawMortalHistoryPayloads(
                            historicalItems.Where(item =>
                                string.Equals(
                                    item.SourceIdentity,
                                    historical.SourceIdentity,
                                    StringComparison.Ordinal)));
                    var currentCandidates = currentEvidence
                        .Where(item =>
                            string.Equals(
                                item.SourceIdentity,
                                historical.SourceIdentity,
                                StringComparison.Ordinal))
                        .ToArray();
                    if (currentCandidates.Length == 0)
                        continue;
                    var currentPayload =
                        MergeRawMortalHistoryPayloads(
                            currentCandidates);
                    if (historicalPayload != null &&
                        currentPayload != null &&
                        !MortalHistoricalValueIsSubset(
                            historicalPayload,
                            currentPayload))
                    {
                        result[factionId] =
                            historical.SourceIdentity;
                        break;
                    }

                    continue;
                }

                if (MortalRawHistorySourceIsProject(
                        historical.SourceIdentity))
                {
                    if (historical.Payload is not JsonObject
                            historicalProject ||
                        ReadMortalHistoryString(
                            historicalProject,
                            "projectId") is not { } projectId)
                    {
                        continue;
                    }

                    if (processedProjects.Any(processed =>
                            string.Equals(
                                processed.SourceIdentity,
                                historical.SourceIdentity,
                                StringComparison.Ordinal) &&
                            string.Equals(
                                processed.ProjectId,
                                projectId,
                                StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    processedProjects.Add((
                        historical.SourceIdentity,
                        projectId));
                    var historicalPayload =
                        MergeRawMortalHistoryPayloads(
                            historicalItems.Where(item =>
                                RawMortalProjectHistoryMatches(
                                    item,
                                    historical.SourceIdentity,
                                    projectId)));
                    var currentCandidates = currentEvidence
                        .Where(item =>
                            RawMortalProjectHistoryMatches(
                                item,
                                historical.SourceIdentity,
                                projectId))
                        .ToArray();
                    if (currentCandidates.Length == 0)
                        continue;
                    var currentPayload =
                        MergeRawMortalHistoryPayloads(
                            currentCandidates);
                    if (historicalPayload != null &&
                        currentPayload != null &&
                        !MortalHistoricalValueIsSubset(
                            historicalPayload,
                            currentPayload))
                    {
                        result[factionId] =
                            historical.SourceIdentity;
                        break;
                    }

                    continue;
                }

                var exactCandidates = currentEvidence
                    .Where(item =>
                        string.Equals(
                            item.SourceIdentity,
                            historical.SourceIdentity,
                            StringComparison.Ordinal))
                    .ToArray();
                if (exactCandidates.Length > 0 &&
                    exactCandidates.Any(candidate =>
                        !MortalHistoricalValueIsSubset(
                            historical.Payload,
                            candidate.Payload)))
                {
                    result[factionId] =
                        historical.SourceIdentity;
                    break;
                }
            }
        }

        return result;
    }

    private static bool MortalRawHistorySourceUsesOrderedSidecarMerge(
        string sourceIdentity) =>
        string.Equals(
            sourceIdentity,
            $"{MortalFactionStructurePath}.entries",
            StringComparison.Ordinal) ||
        string.Equals(
            sourceIdentity,
            $"{MortalFactionResourcesPath}.entries",
            StringComparison.Ordinal) ||
        string.Equals(
            sourceIdentity,
            $"{MortalFactionCustomPath}.entries",
            StringComparison.Ordinal);

    private static bool MortalRawHistorySourceIsProject(
        string sourceIdentity) =>
        string.Equals(
            sourceIdentity,
            $"{MortalFactionProjectsPath}.activeProjects",
            StringComparison.Ordinal) ||
        string.Equals(
            sourceIdentity,
            $"{MortalFactionProjectsPath}.completedProjects",
            StringComparison.Ordinal);

    private static bool RawMortalProjectHistoryMatches(
        RawFactionTouchEvidence evidence,
        string sourceIdentity,
        string projectId)
    {
        if (!string.Equals(
                evidence.SourceIdentity,
                sourceIdentity,
                StringComparison.Ordinal) ||
            evidence.Payload is not JsonObject project)
        {
            return false;
        }

        return string.Equals(
            ReadMortalHistoryString(project, "projectId"),
            projectId,
            StringComparison.OrdinalIgnoreCase);
    }

    private static JsonObject? MergeRawMortalHistoryPayloads(
        IEnumerable<RawFactionTouchEvidence> evidence)
    {
        JsonObject? result = null;
        foreach (var item in evidence)
        {
            if (item.Payload is not JsonObject candidate)
                continue;
            if (result == null)
                result = candidate.DeepClone().AsObject();
            else
                MergeMortalHistoryObject(result, candidate);
        }

        return result;
    }

    private async Task<JsonElement?> ReadRawMortalTouchRootAsync(
        ValidationPendingTurnSnapshotManifest manifest,
        string path,
        bool preTurn,
        List<ValidationIssue> issues)
    {
        var json = preTurn
            ? await ReadValidatedPendingTurnSnapshotFileAsync(
                manifest,
                path)
            : await _fs.ReadFileAsync(path);
        if (string.IsNullOrWhiteSpace(json))
            return null;

        using var document = TryParseFactionMaterializationDocument(
            json,
            path,
            currentAuthority: !preTurn,
            issues);
        return document?.RootElement.Clone();
    }

    private static void CollectRawMortalTargetArrays(
        JsonElement? root,
        string path,
        IReadOnlyList<string> arrayNames,
        Dictionary<string, List<RawFactionTouchEvidence>> evidence)
    {
        if (root is not { ValueKind: JsonValueKind.Object })
            return;

        foreach (var arrayName in arrayNames)
        {
            if (!root.Value.TryGetProperty(arrayName, out var rows) ||
                rows.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var row in rows.EnumerateArray())
            {
                if (row.ValueKind != JsonValueKind.Object)
                    continue;
                AddRawFactionTouchEvidence(
                    evidence,
                    ReadMortalString(row, "factionId"),
                    $"{path}.{arrayName}",
                    JsonNode.Parse(row.GetRawText()));
            }
        }
    }

    private static void CollectRawMortalCommandTouches(
        JsonElement? root,
        IReadOnlyList<string> commandArrays,
        HashSet<string> touchedFactionIds)
    {
        if (root is not { ValueKind: JsonValueKind.Object })
            return;

        foreach (var arrayName in commandArrays)
        {
            if (!root.Value.TryGetProperty(arrayName, out var commands) ||
                commands.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var command in commands.EnumerateArray())
            {
                if (command.ValueKind != JsonValueKind.Object)
                    continue;
                var factionId = ReadFirstMortalString(
                    command,
                    "factionId",
                    "initialFactionId");
                if (!string.IsNullOrWhiteSpace(factionId))
                    touchedFactionIds.Add(factionId);
            }
        }
    }

    private static void CollectRawMortalLocationTouchEvidence(
        JsonElement? root,
        Dictionary<string, List<RawFactionTouchEvidence>> evidence)
    {
        if (root == null)
            return;

        foreach (var location in EnumerateLocationLikeObjects(root.Value))
        {
            if (!location.TryGetProperty(
                    "factionControl",
                    out var controls) ||
                controls.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            var locationId =
                ReadMortalString(location, "locationId") ??
                "unknown-location";
            foreach (var control in controls.EnumerateArray())
            {
                if (control.ValueKind != JsonValueKind.Object)
                    continue;
                AddRawFactionTouchEvidence(
                    evidence,
                    ReadMortalString(control, "factionId"),
                    $"location:{locationId}.factionControl",
                    JsonNode.Parse(control.GetRawText()));
            }
        }
    }

    private static void CollectRawMortalNpcTouchEvidence(
        JsonElement? root,
        Dictionary<string, List<RawFactionTouchEvidence>> evidence)
    {
        if (root is not { ValueKind: JsonValueKind.Object })
            return;

        foreach (var property in root.Value.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.Array)
                continue;
            foreach (var npc in property.Value.EnumerateArray())
            {
                if (npc.ValueKind != JsonValueKind.Object ||
                    !npc.TryGetProperty(
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
                foreach (var affiliation in affiliations.EnumerateArray())
                {
                    if (affiliation.ValueKind != JsonValueKind.Object)
                        continue;
                    AddRawFactionTouchEvidence(
                        evidence,
                        ReadMortalString(
                            affiliation,
                            "factionId"),
                        $"npc:{npcId}.factionAffiliations",
                        JsonNode.Parse(affiliation.GetRawText()));
                }
            }
        }
    }

    private static JsonElement? ProjectRawMortalNpcAffiliationCommands(
        JsonElement? currentRoot,
        JsonElement? preTurnRoot)
    {
        if (currentRoot is not { ValueKind: JsonValueKind.Object } ||
            preTurnRoot is not { ValueKind: JsonValueKind.Object })
        {
            return currentRoot;
        }

        var projected = JsonNode.Parse(
            currentRoot.Value.GetRawText()) as JsonObject;
        var preTurn = JsonNode.Parse(
            preTurnRoot.Value.GetRawText()) as JsonObject;
        if (projected == null ||
            preTurn == null ||
            projected[NpcCoreChangesContract.PropertyName]
                is not JsonArray commands)
        {
            return currentRoot;
        }

        var preTurnActors =
            EnumerateRawMortalNpcActors(preTurn).ToArray();
        var currentActors =
            EnumerateRawMortalNpcActors(projected).ToArray();
        foreach (var command in commands.OfType<JsonObject>())
        {
            var npcId = ReadShiningMaterializationString(
                command,
                "NPCId");
            if (npcId == null ||
                !preTurnActors.Any(actor =>
                    string.Equals(
                        actor.NpcId,
                        npcId,
                        StringComparison.Ordinal)))
            {
                continue;
            }

            var targets = currentActors
                .Where(actor => string.Equals(
                    actor.NpcId,
                    npcId,
                    StringComparison.Ordinal))
                .Select(actor => actor.Actor)
                .ToArray();
            if (targets.Length == 0 ||
                command["factionAffiliationsToUpsert"]
                    is not JsonArray upserts)
            {
                continue;
            }

            foreach (var target in targets)
            {
                var affiliations =
                    target["factionAffiliations"] as JsonArray ??
                    new JsonArray();
                target["factionAffiliations"] = affiliations;
                foreach (var upsert in upserts.OfType<JsonObject>())
                {
                    var factionId =
                        ReadShiningMaterializationString(
                            upsert,
                            "factionId");
                    if (factionId == null)
                        continue;

                    var existingIndex = -1;
                    for (var index = 0;
                         index < affiliations.Count;
                         index++)
                    {
                        if (affiliations[index] is JsonObject existing &&
                            string.Equals(
                                ReadShiningMaterializationString(
                                    existing,
                                    "factionId"),
                                factionId,
                                StringComparison.Ordinal))
                        {
                            existingIndex = index;
                            break;
                        }
                    }

                    if (existingIndex >= 0)
                        affiliations[existingIndex] =
                            upsert.DeepClone();
                    else
                        affiliations.Add(upsert.DeepClone());
                }
            }
        }

        return JsonSerializer.SerializeToElement(projected);
    }

    private static IEnumerable<RawMortalNpcActor>
        EnumerateRawMortalNpcActors(JsonObject root)
    {
        foreach (var section in
                 GuardianPolicyContracts
                     .NpcCoreCanonicalNpcObjectSections)
        {
            if (root[section] is not JsonArray actors)
                continue;

            foreach (var actor in actors.OfType<JsonObject>())
            {
                if (!GuardianPolicyContracts
                        .TryResolveStrictPermanentNpcId(
                            actor,
                            out var npcId))
                {
                    continue;
                }

                yield return new RawMortalNpcActor(
                    npcId,
                    actor);
            }
        }
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
        var locationsById =
            new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        var locationsWithoutIds = new List<JsonElement>();
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
                        if (string.IsNullOrWhiteSpace(locationId))
                            locationsWithoutIds.Add(clone);
                        else
                            locationsById[locationId] = clone;
                    }
                }
                catch (JsonException)
                {
                    // Ordinary state-file validation owns malformed JSON.
                }
            }
        }

        var locations = locationsById.Values
            .Concat(locationsWithoutIds)
            .ToArray();
        return new MortalLocationAuthority(
            locationsById.Keys.ToHashSet(StringComparer.Ordinal),
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
        foreach (var location in authority.Locations)
        {
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
                        "Mortal location control must use one exact effective faction ID."));
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
            LocationHasFactionControl(location, factionId));

    private static bool HasExactEmptyMortalTerritory(
        JsonElement faction,
        string factionId,
        MortalLocationAuthority locationAuthority) =>
        HasExactEmptyArray(faction, "controlledTerritories") &&
        !locationAuthority.Locations.Any(location =>
            LocationHasFactionControl(location, factionId));

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
        string message) =>
        new(
            path,
            IssueSeverity.Error,
            message,
            code: code,
            actor: $"mortal_faction:{factionId}",
            section: "FactionMaterialization",
            repairHint:
            "Repair only this Mortal faction's semantic or atomic materialization bundle.");

    private sealed record MortalFactionMaterializationTarget(
        JsonElement Faction,
        string Context,
        string FactionId,
        bool HasExplicitNullFactionId);

    private sealed record MortalLocationAuthority(
        HashSet<string> LocationIds,
        IReadOnlyList<JsonElement> Locations);

    private sealed record MortalNpcAuthority(
        HashSet<string> NpcIds,
        IReadOnlyList<JsonElement> Npcs);

    private sealed record RawMortalNpcActor(
        string NpcId,
        JsonObject Actor);

    private sealed record MortalPromotionHistoryIndex(
        IReadOnlyDictionary<string, JsonObject> StructureByFaction,
        IReadOnlyDictionary<string, JsonObject> ResourcesByFaction,
        IReadOnlyDictionary<
            string,
            IReadOnlyDictionary<string, JsonObject>>
            ActiveProjectsByFaction,
        IReadOnlyDictionary<
            string,
            IReadOnlyDictionary<string, JsonObject>>
            CompletedProjectsByFaction,
        IReadOnlyDictionary<string, JsonObject> CustomByFaction);

    private sealed record MortalEffectiveHistoryArray(
        IReadOnlyList<JsonObject> Identified,
        IReadOnlyDictionary<string, int> Unidentified);

    private sealed record RawMortalExternalFactionTouchSummary(
        IReadOnlySet<string> ChangedAuthorityIds,
        IReadOnlyDictionary<string, string> RewrittenHistoricalSources,
        IReadOnlySet<string> CommandTargetIds)
    {
        internal static RawMortalExternalFactionTouchSummary Empty { get; } =
            new(
                new HashSet<string>(StringComparer.Ordinal),
                new Dictionary<string, string>(StringComparer.Ordinal),
                new HashSet<string>(StringComparer.Ordinal));
    }

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
