using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal static partial class ShiningAbodeState
{
    public readonly record struct ResourceCost(int Feathers, int LightSparks);

    private sealed record GateCandidate(
        JsonObject Card,
        string DedupeKey,
        int EffectiveStrength,
        int SourcePriority,
        int RarityWeight);

    private static readonly ResourceCost NativeDiscoveryCost = new(25, 20);
    private static readonly ResourceCost InvestFactionCost = new(10, 5);

    private static readonly IReadOnlyDictionary<int, ResourceCost> ProjectTierCosts = new Dictionary<int, ResourceCost>
    {
        [1] = new ResourceCost(20, 10),
        [2] = new ResourceCost(30, 15),
        [3] = new ResourceCost(40, 20)
    };

    private static readonly IReadOnlyDictionary<int, string> ProjectTierBaseRarities = new Dictionary<int, string>
    {
        [1] = RarityCommon,
        [2] = RarityUncommon,
        [3] = RarityRare
    };

    private static readonly IReadOnlyDictionary<string, int> SourceTypePriority = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
    {
        [CardSourceTypeHead] = 3,
        [CardSourceTypeProject] = 2,
        [CardSourceTypeResidentDescent] = 1
    };

    private static readonly IReadOnlyDictionary<string, int> RarityWeight = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
    {
        [RarityCommon] = 1,
        [RarityUncommon] = 2,
        [RarityRare] = 3,
        [RarityRadiant] = 4
    };

    private static readonly IReadOnlyDictionary<string, HashSet<string>> ArchetypeFamilyCompatibility = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
    {
        [ProjectArchetypeRevelation] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { EffectFamilyLore, EffectFamilyMemory },
        [ProjectArchetypeAccord] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { EffectFamilySocial, EffectFamilyRoute },
        [ProjectArchetypeProvision] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { EffectFamilyResource, EffectFamilyRoute },
        [ProjectArchetypeRemembrance] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { EffectFamilyMemory, EffectFamilyLore },
        [ProjectArchetypeRefinement] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { EffectFamilyRelic, EffectFamilyResource },
        [ProjectArchetypePassage] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { EffectFamilyDescent, EffectFamilyRoute },
        [ProjectArchetypeWarding] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { EffectFamilySurvival, EffectFamilySocial },
        [ProjectArchetypeSubversion] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { EffectFamilySocial, EffectFamilyLore, EffectFamilyMemory, EffectFamilyDescent }
    };

    public static ResourceCost GetNativeDiscoveryCost() => NativeDiscoveryCost;

    public static ResourceCost GetFactionInvestmentCost() => InvestFactionCost;

    public static JsonObject ActivateForAscension(JsonObject? existingRoot, JsonObject? residentRoot, JsonObject? guardiansRoot)
    {
        var root = CloneObject(existingRoot ?? CreateDefaultState());
        NormalizeStateRoot(root, residentRoot, guardiansRoot);
        root["availability"] = AvailabilityActive;
        root["lightSparks"] = 100;
        root["pendingNativeFactionDiscovery"] = null;
        root["gates"] = BuildDefaultGatesObject();
        root["preparedIncarnationPackage"] = null;

        foreach (var faction in EnsureFactionsArray(root).OfType<JsonObject>())
        {
            faction["investCountThisAscension"] = 0;
            faction["projectArchetypesCountedThisAscension"] = new JsonArray();
        }

        NormalizeStateRoot(root, residentRoot, guardiansRoot);
        return root;
    }

    public static JsonObject ReenterOrdinaryActiveState(JsonObject? existingRoot, JsonObject? residentRoot, JsonObject? guardiansRoot)
    {
        var root = CloneObject(existingRoot ?? CreateDefaultState());
        NormalizeStateRoot(root, residentRoot, guardiansRoot);
        root["availability"] = AvailabilityActive;
        NormalizeStateRoot(root, residentRoot, guardiansRoot);
        return root;
    }

    public static void NormalizeStateRoot(JsonObject root, JsonObject? residentRoot, JsonObject? guardiansRoot)
    {
        NormalizeStateRoot(root, residentRoot);
        HydrateLeadershipReceiptSnapshots(root, residentRoot, guardiansRoot);
        if (EnsureActiveGuardianFactionMaterialized(root, guardiansRoot))
        {
            NormalizeStateRoot(root, residentRoot);
            HydrateLeadershipReceiptSnapshots(root, residentRoot, guardiansRoot);
        }
    }

    public static bool TryQueueNativeFactionDiscovery(JsonObject root, int currentTurnNumber, out string? error)
    {
        if (!EnsureOrdinaryActiveState(root, out error))
            return false;

        if (root["pendingNativeFactionDiscovery"] is JsonObject)
        {
            error = "Нативная фракция уже ожидает открытия.";
            return false;
        }

        var radianceTier = GetNodeInt(root["radiance"]?["tier"], 0);
        if (radianceTier < 1)
        {
            error = "Открытие нативной фракции доступно только с Radiance tier 1 или выше.";
            return false;
        }

        var lightSparks = GetNodeInt(root["lightSparks"], 0);
        if (lightSparks < NativeDiscoveryCost.LightSparks)
        {
            error = $"Недостаточно Искр Света. Нужно {NativeDiscoveryCost.LightSparks}.";
            return false;
        }

        root["lightSparks"] = lightSparks - NativeDiscoveryCost.LightSparks;
        root["pendingNativeFactionDiscovery"] = new JsonObject
        {
            ["requestId"] = $"discover_native_faction:{currentTurnNumber:0000}",
            ["createdAtTurn"] = currentTurnNumber,
            ["createdAtUtc"] = DateTime.UtcNow.ToString("o"),
            ["radianceTierAtRequest"] = radianceTier,
            ["costFeathers"] = NativeDiscoveryCost.Feathers,
            ["costLightSparks"] = NativeDiscoveryCost.LightSparks
        };

        error = null;
        return true;
    }

    public static bool TryInvestInFaction(JsonObject root, JsonObject? residentRoot, string factionId, out string? error)
    {
        if (!EnsureOrdinaryActiveState(root, out error))
            return false;

        if (!TryGetFaction(root, factionId, out var faction, out error))
            return false;

        var investCount = Math.Clamp(GetNodeInt(faction["investCountThisAscension"], 0), 0, 3);
        if (investCount >= 3)
        {
            error = "В эту фракцию уже вложено максимум 3 инвестиции за текущее восхождение.";
            return false;
        }

        var lightSparks = GetNodeInt(root["lightSparks"], 0);
        if (lightSparks < InvestFactionCost.LightSparks)
        {
            error = $"Недостаточно Искр Света. Нужно {InvestFactionCost.LightSparks}.";
            return false;
        }

        root["lightSparks"] = lightSparks - InvestFactionCost.LightSparks;
        faction["investCountThisAscension"] = investCount + 1;
        RecomputeFactionStrengths(root, residentRoot);
        MarkOpenGatesStale(root);
        error = null;
        return true;
    }

    public static bool TryQuoteProjectCompletion(
        JsonObject root,
        JsonObject? residentRoot,
        string factionId,
        JsonObject projectDraft,
        out ResourceCost cost,
        out string? error)
    {
        cost = default;
        if (!EnsureOrdinaryActiveState(root, out error))
            return false;

        if (!TryGetFaction(root, factionId, out var faction, out error))
            return false;

        if (!TryValidateProjectDraft(root, faction, projectDraft, out var projectArchetype, out _, out var tier, out _, out error))
            return false;

        cost = ResolveProjectCompletionCost(faction, residentRoot, projectArchetype, tier);
        if (GetNodeInt(root["lightSparks"], 0) < cost.LightSparks)
        {
            error = $"Недостаточно Искр Света. Нужно {cost.LightSparks}.";
            return false;
        }

        error = null;
        return true;
    }

    public static bool TryCompleteProject(
        JsonObject root,
        JsonObject? residentRoot,
        string factionId,
        JsonObject projectDraft,
        int currentTurnNumber,
        string? projectIdOverride,
        string? completedAtUtc,
        out ResourceCost cost,
        out string? error)
    {
        cost = default;
        if (!TryQuoteProjectCompletion(root, residentRoot, factionId, projectDraft, out cost, out error))
            return false;

        var faction = FindFaction(root, factionId)!;
        var projectArchetype = GetNodeString(projectDraft["projectArchetype"])?.Trim() ?? string.Empty;
        var outputEffectFamily = GetNodeString(projectDraft["outputEffectFamily"]) ?? ResolveDefaultOutputFamily(projectArchetype);
        var tier = Math.Clamp(GetNodeInt(projectDraft["tier"], 1), 1, 3);
        root["lightSparks"] = Math.Max(0, GetNodeInt(root["lightSparks"], 0) - cost.LightSparks);

        var project = new JsonObject
        {
            ["projectId"] = string.IsNullOrWhiteSpace(projectIdOverride)
                ? BuildProjectId(factionId, projectDraft, currentTurnNumber)
                : projectIdOverride,
            ["displayName"] = GetNodeString(projectDraft["displayName"]) ?? string.Empty,
            ["summary"] = GetNodeString(projectDraft["summary"]) ?? string.Empty,
            ["toneTags"] = CloneArray(projectDraft["toneTags"] as JsonArray),
            ["targetFactionIds"] = CloneArray(projectDraft["targetFactionIds"] as JsonArray),
            ["projectArchetype"] = projectArchetype,
            ["outputEffectFamily"] = outputEffectFamily,
            ["tier"] = tier,
            ["status"] = ProjectStatusCompleted,
            ["isSupported"] = false,
            ["strengthReward"] = ResolveProjectStrengthReward(
                tier,
                string.Equals(GetNodeString(faction["charter"]?["favoredArchetype"]), projectArchetype, StringComparison.OrdinalIgnoreCase)),
            ["completedAtTurn"] = currentTurnNumber,
            ["completedAtUtc"] = string.IsNullOrWhiteSpace(completedAtUtc) ? DateTime.UtcNow.ToString("o") : completedAtUtc
        };

        EnsureArray(faction, "projects").Add(project);
        var countedArchetypes = EnsureArray(faction, "projectArchetypesCountedThisAscension");
        if (!countedArchetypes.OfType<JsonValue>().Any(node => node.TryGetValue<string>(out var value) &&
                                                               string.Equals(value, projectArchetype, StringComparison.OrdinalIgnoreCase)))
        {
            countedArchetypes.Add(projectArchetype);
            var radiance = root["radiance"] as JsonObject ?? new JsonObject();
            var experience = GetNodeInt(radiance["experience"], 0) + 10;
            radiance["experience"] = experience;
            radiance["tier"] = ResolveRadianceTier(experience);
            root["radiance"] = radiance;
        }

        RecomputeFactionStrengths(root, residentRoot);
        MarkOpenGatesStale(root);
        error = null;
        return true;
    }

    public static bool TrySupportProject(JsonObject root, string factionId, string projectId, out string? error)
    {
        if (!EnsureOrdinaryActiveState(root, out error))
            return false;

        if (!TryGetProject(root, factionId, projectId, out var project, out error))
            return false;

        if (!string.Equals(GetNodeString(project["status"]), ProjectStatusCompleted, StringComparison.OrdinalIgnoreCase))
        {
            error = "Поддержка доступна только для completed projects.";
            return false;
        }

        if (GetNodeBool(project["isSupported"]))
        {
            error = "Проект уже поддерживается; support_project не должен быть no-op.";
            return false;
        }

        if (CountSupportedProjectsAcrossState(root) >= GetSupportedProjectCap(GetNodeInt(root["radiance"]?["tier"], 0)))
        {
            error = "Глобальный лимит поддерживаемых проектов уже достигнут.";
            return false;
        }

        project["isSupported"] = true;
        MarkOpenGatesStale(root);
        error = null;
        return true;
    }

    public static bool TryUnsupportProject(JsonObject root, string factionId, string projectId, out string? error)
    {
        if (!EnsureOrdinaryActiveState(root, out error))
            return false;

        if (!TryGetProject(root, factionId, projectId, out var project, out error))
            return false;

        if (!GetNodeBool(project["isSupported"]))
        {
            error = "Проект уже не поддерживается; unsupport_project не должен быть no-op.";
            return false;
        }

        project["isSupported"] = false;
        MarkOpenGatesStale(root);
        error = null;
        return true;
    }

    public static bool TryRetireProject(JsonObject root, JsonObject? residentRoot, string factionId, string projectId, out string? error)
    {
        if (!EnsureOrdinaryActiveState(root, out error))
            return false;

        if (!TryGetProject(root, factionId, projectId, out var project, out error))
            return false;

        if (!string.Equals(GetNodeString(project["status"]), ProjectStatusCompleted, StringComparison.OrdinalIgnoreCase))
        {
            error = "В историю можно отправить только completed project.";
            return false;
        }

        project["status"] = ProjectStatusRetired;
        project["isSupported"] = false;
        RecomputeFactionStrengths(root, residentRoot);
        MarkOpenGatesStale(root);
        error = null;
        return true;
    }

    public static bool IsProjectFamilyCompatible(string? projectArchetype, string? outputEffectFamily)
    {
        var normalizedArchetype = NormalizeProjectArchetype(projectArchetype);
        if (!IsSupportedEffectFamily(outputEffectFamily))
            return false;

        return ArchetypeFamilyCompatibility.TryGetValue(normalizedArchetype, out var families) &&
               families.Contains(outputEffectFamily!);
    }

    public static ResourceCost ResolveProjectCompletionCost(JsonObject faction, JsonObject? residentRoot, string projectArchetype, int tier)
    {
        var baseCost = ProjectTierCosts[Math.Clamp(tier, 1, 3)];
        var feathers = baseCost.Feathers;
        var lightSparks = baseCost.LightSparks;
        if (string.Equals(GetNodeString(faction["charter"]?["favoredArchetype"]), projectArchetype, StringComparison.OrdinalIgnoreCase))
        {
            feathers = Math.Max(1, feathers - 5);
            lightSparks = Math.Max(1, lightSparks - 5);
        }

        if (string.Equals(projectArchetype, ProjectArchetypeRevelation, StringComparison.OrdinalIgnoreCase) &&
            HasFactionRole(residentRoot, GetNodeString(faction["factionId"]), ResidentRoleArchiveSupport))
        {
            feathers = Math.Max(1, feathers - 5);
        }

        return new ResourceCost(feathers, lightSparks);
    }

    public static int CountSupportedProjectsAcrossState(JsonObject root)
    {
        var total = 0;
        foreach (var faction in EnsureFactionsArray(root).OfType<JsonObject>())
            total += CountSupportedProjects(faction);
        return total;
    }

    public static int CountSupportedProjectsByArchetype(JsonObject root, string archetype)
    {
        var total = 0;
        foreach (var faction in EnsureFactionsArray(root).OfType<JsonObject>())
        {
            if (faction["projects"] is not JsonArray projects)
                continue;

            total += projects.OfType<JsonObject>().Count(project =>
                string.Equals(GetNodeString(project["status"]), ProjectStatusCompleted, StringComparison.OrdinalIgnoreCase) &&
                GetNodeBool(project["isSupported"]) &&
                string.Equals(GetNodeString(project["projectArchetype"]), archetype, StringComparison.OrdinalIgnoreCase));
        }

        return total;
    }

    public static bool TryOpenGates(JsonObject root, JsonObject? residentRoot, out string? error)
    {
        if (!EnsureOrdinaryActiveState(root, out error))
            return false;

        var gates = root["gates"] as JsonObject ?? BuildDefaultGatesObject();
        var candidates = BuildGateCandidates(root, residentRoot);
        var draftSize = GetDraftSize(GetNodeInt(root["radiance"]?["tier"], 0));
        var available = candidates.Take(Math.Min(draftSize, candidates.Count)).Select(candidate => CloneObject(candidate.Card)).ToList();
        var shownIds = available.Select(card => GetNodeString(card["cardId"])).Where(id => !string.IsNullOrWhiteSpace(id)).Cast<string>().ToList();

        gates["draftVersion"] = GetNodeInt(gates["draftVersion"], 0) + 1;
        gates["hasOpenDraft"] = true;
        gates["isStale"] = false;
        gates["allCandidateBlessingCards"] = new JsonArray(candidates.Select(candidate => CloneObject(candidate.Card)).ToArray<JsonNode?>());
        gates["availableBlessingCards"] = new JsonArray(available.ToArray<JsonNode?>());
        gates["shownBlessingCardIds"] = new JsonArray(shownIds.Select(id => (JsonNode?)id).ToArray());
        gates["selectedBlessingCardIds"] = new JsonArray();
        gates["nextCandidateCursor"] = shownIds.Count;
        gates["rerollsRemaining"] = CountSupportedProjectsByArchetype(root, ProjectArchetypeRemembrance);
        root["gates"] = gates;

        error = null;
        return true;
    }

    public static bool TrySelectBlessingCard(JsonObject root, string cardId, out string? error)
    {
        if (!TryGetOpenFreshGates(root, out var gates, out error))
            return false;

        var availableCards = EnsureArray(gates, "availableBlessingCards");
        if (!availableCards.OfType<JsonObject>().Any(card => string.Equals(GetNodeString(card["cardId"]), cardId, StringComparison.OrdinalIgnoreCase)))
        {
            error = "Эта карта сейчас не находится в открытом draft.";
            return false;
        }

        var selected = EnsureArray(gates, "selectedBlessingCardIds");
        if (selected.OfType<JsonValue>().Any(node => node.TryGetValue<string>(out var value) && string.Equals(value, cardId, StringComparison.OrdinalIgnoreCase)))
        {
            error = null;
            return true;
        }

        if (selected.Count >= GetPickCap(GetNodeInt(root["radiance"]?["tier"], 0)))
        {
            error = "Лимит выбранных благословений уже достигнут.";
            return false;
        }

        selected.Add(cardId);
        error = null;
        return true;
    }

    public static bool TryDeselectBlessingCard(JsonObject root, string cardId, out string? error)
    {
        if (!TryGetOpenFreshGates(root, out var gates, out error))
            return false;

        var selected = EnsureArray(gates, "selectedBlessingCardIds");
        for (var i = selected.Count - 1; i >= 0; i--)
        {
            if (selected[i] is JsonValue node &&
                node.TryGetValue<string>(out var value) &&
                string.Equals(value, cardId, StringComparison.OrdinalIgnoreCase))
            {
                selected.RemoveAt(i);
            }
        }

        error = null;
        return true;
    }

    public static bool TryRerollGatesDraft(JsonObject root, out string? error)
    {
        if (!TryGetOpenFreshGates(root, out var gates, out error))
            return false;

        var rerollsRemaining = GetNodeInt(gates["rerollsRemaining"], 0);
        if (rerollsRemaining <= 0)
        {
            error = "Для этого draft больше нет reroll.";
            return false;
        }

        var availableCards = EnsureArray(gates, "availableBlessingCards");
        var selectedIds = EnsureArray(gates, "selectedBlessingCardIds")
            .OfType<JsonValue>()
            .Where(node => node.TryGetValue<string>(out _))
            .Select(node => node.GetValue<string>())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var removable = availableCards
            .OfType<JsonObject>()
            .Where(card => !selectedIds.Contains(GetNodeString(card["cardId"]) ?? string.Empty))
            .OrderBy(card => GetRarityWeight(GetNodeString(card["rarity"])))
            .ThenBy(card => GetNodeString(card["cardId"]), StringComparer.OrdinalIgnoreCase)
            .Take(2)
            .ToList();
        if (removable.Count < 2)
        {
            error = "Для reroll нужны минимум две невыбранные карты.";
            return false;
        }

        var allCandidates = EnsureArray(gates, "allCandidateBlessingCards").OfType<JsonObject>().ToList();
        var shown = EnsureArray(gates, "shownBlessingCardIds");
        var shownIds = shown.OfType<JsonValue>()
            .Where(node => node.TryGetValue<string>(out _))
            .Select(node => node.GetValue<string>())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var nextCursor = Math.Clamp(GetNodeInt(gates["nextCandidateCursor"], 0), 0, allCandidates.Count);

        var replacements = new List<JsonObject>();
        while (nextCursor < allCandidates.Count && replacements.Count < 2)
        {
            var candidate = allCandidates[nextCursor++];
            var candidateId = GetNodeString(candidate["cardId"]);
            if (string.IsNullOrWhiteSpace(candidateId) || shownIds.Contains(candidateId))
                continue;

            replacements.Add(CloneObject(candidate));
            shownIds.Add(candidateId);
            shown.Add(candidateId);
        }

        if (replacements.Count < 2)
        {
            error = "Во frozen snapshot больше нет двух невиденных replacement-карт.";
            return false;
        }

        var removableIds = removable
            .Select(card => GetNodeString(card["cardId"]))
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Cast<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        for (var i = availableCards.Count - 1; i >= 0; i--)
        {
            if (availableCards[i] is JsonObject existing &&
                removableIds.Contains(GetNodeString(existing["cardId"]) ?? string.Empty))
            {
                availableCards.RemoveAt(i);
            }
        }

        foreach (var replacement in replacements)
            availableCards.Add(replacement);

        var sorted = availableCards.OfType<JsonObject>()
            .OrderByDescending(card => GetRarityWeight(GetNodeString(card["rarity"])))
            .ThenByDescending(card => GetNodeInt(card["_effectiveStrength"], 0))
            .ThenByDescending(card => GetSourceTypePriority(GetNodeString(card["sourceType"])))
            .ThenBy(card => GetNodeString(card["cardId"]), StringComparer.OrdinalIgnoreCase)
            .Select(CloneObject)
            .ToList();

        gates["availableBlessingCards"] = new JsonArray(sorted.ToArray<JsonNode?>());
        gates["nextCandidateCursor"] = nextCursor;
        gates["rerollsRemaining"] = rerollsRemaining - 1;
        error = null;
        return true;
    }

    public static bool TryPrepareIncarnationPackage(JsonObject root, int currentTurnNumber, out string? error, string? preparedAtUtc = null)
    {
        if (!TryGetOpenFreshGates(root, out var gates, out error))
            return false;

        var selectedIds = EnsureArray(gates, "selectedBlessingCardIds")
            .OfType<JsonValue>()
            .Where(node => node.TryGetValue<string>(out _))
            .Select(node => node.GetValue<string>())
            .ToList();
        var pickCap = GetPickCap(GetNodeInt(root["radiance"]?["tier"], 0));
        if (selectedIds.Count < 1)
        {
            error = "Нужно выбрать хотя бы одно благословение.";
            return false;
        }

        if (selectedIds.Count > pickCap)
        {
            error = "Число выбранных благословений превышает текущий pick cap.";
            return false;
        }

        var availableCards = EnsureArray(gates, "availableBlessingCards").OfType<JsonObject>().ToList();
        var selectedCards = new JsonArray();
        foreach (var selectedId in selectedIds)
        {
            var card = availableCards.FirstOrDefault(item => string.Equals(GetNodeString(item["cardId"]), selectedId, StringComparison.OrdinalIgnoreCase));
            if (card == null)
            {
                error = "Подготовка пакета требует, чтобы все выбранные карты были частью текущего draft.";
                return false;
            }

            selectedCards.Add(CloneCardForPersistence(card));
        }

        root["preparedIncarnationPackage"] = new JsonObject
        {
            ["selectedCardIds"] = new JsonArray(selectedIds.Select(id => (JsonNode?)id).ToArray()),
            ["selectedCards"] = selectedCards,
            ["generatedFromDraftVersion"] = GetNodeInt(gates["draftVersion"], 0),
            ["preparedAtTurn"] = currentTurnNumber,
            ["preparedAtUtc"] = string.IsNullOrWhiteSpace(preparedAtUtc) ? DateTime.UtcNow.ToString("o") : preparedAtUtc
        };
        root["gates"] = BuildDefaultGatesObject();
        error = null;
        return true;
    }

    private static bool TryValidateProjectDraft(
        JsonObject root,
        JsonObject faction,
        JsonObject projectDraft,
        out string projectArchetype,
        out string outputEffectFamily,
        out int tier,
        out List<string> targetFactionIds,
        out string? error)
    {
        projectArchetype = GetNodeString(projectDraft["projectArchetype"])?.Trim() ?? string.Empty;
        outputEffectFamily = GetNodeString(projectDraft["outputEffectFamily"]) ?? string.Empty;
        tier = Math.Clamp(GetNodeInt(projectDraft["tier"], 1), 1, 3);
        targetFactionIds = new List<string>();

        if (string.IsNullOrWhiteSpace(GetNodeString(projectDraft["displayName"])))
        {
            error = "Проекту нужно имя.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(GetNodeString(projectDraft["summary"])))
        {
            error = "Проекту нужно краткое описание.";
            return false;
        }

        if (projectDraft["toneTags"] is not JsonArray toneTags || toneTags.Count == 0)
        {
            error = "Проекту нужен хотя бы один tone tag.";
            return false;
        }

        if (!IsSupportedProjectArchetype(projectArchetype))
        {
            error = "Неподдерживаемый archetype проекта.";
            return false;
        }

        if (!IsSupportedEffectFamily(outputEffectFamily) || !IsProjectFamilyCompatible(projectArchetype, outputEffectFamily))
        {
            error = "Эта output family не совместима с выбранным archetype.";
            return false;
        }

        if (projectDraft["targetFactionIds"] is JsonArray targetArray)
        {
            targetFactionIds = targetArray.OfType<JsonValue>()
                .Where(node => node.TryGetValue<string>(out _))
                .Select(node => node.GetValue<string>().Trim())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        if (string.Equals(projectArchetype, ProjectArchetypeSubversion, StringComparison.OrdinalIgnoreCase))
        {
            var sourceFactionId = GetNodeString(faction["factionId"]);
            if (targetFactionIds.Count != 1 || string.Equals(targetFactionIds[0], sourceFactionId, StringComparison.OrdinalIgnoreCase) || !FactionExists(root, targetFactionIds[0]))
            {
                error = "Subversion project должен ссылаться ровно на одну существующую чужую фракцию.";
                return false;
            }
        }
        else if (targetFactionIds.Count > 0)
        {
            targetFactionIds = targetFactionIds.Where(targetId => FactionExists(root, targetId)).ToList();
        }

        error = null;
        return true;
    }

    private static bool TryGetFaction(JsonObject root, string factionId, out JsonObject faction, out string? error)
    {
        faction = FindFaction(root, factionId)!;
        if (faction == null)
        {
            error = "Фракция не найдена.";
            return false;
        }

        error = null;
        return true;
    }

    private static bool TryGetProject(JsonObject root, string factionId, string projectId, out JsonObject project, out string? error)
    {
        project = null!;
        if (!TryGetFaction(root, factionId, out var faction, out error))
            return false;

        if (faction["projects"] is not JsonArray projects)
        {
            error = "У фракции нет проектов.";
            return false;
        }

        project = projects.OfType<JsonObject>().FirstOrDefault(item =>
            string.Equals(GetNodeString(item["projectId"]), projectId, StringComparison.OrdinalIgnoreCase))!;
        if (project == null)
        {
            error = "Проект не найден в указанной фракции.";
            return false;
        }

        error = null;
        return true;
    }

    private static bool EnsureOrdinaryActiveState(JsonObject root, out string? error)
    {
        if (!string.Equals(GetNodeString(root["availability"]), AvailabilityActive, StringComparison.OrdinalIgnoreCase))
        {
            error = "Сияющая Обитель сейчас не активна.";
            return false;
        }

        if (root["preparedIncarnationPackage"] is JsonObject)
        {
            error = "Сейчас активен frozen handoff к следующей жизни; обычные действия Сияющей Обители заблокированы.";
            return false;
        }

        error = null;
        return true;
    }

    private static void RecomputeFactionStrengths(JsonObject root, JsonObject? residentRoot)
    {
        var radianceTier = GetNodeInt(root["radiance"]?["tier"], 0);
        foreach (var faction in EnsureFactionsArray(root).OfType<JsonObject>())
            faction["factionStrength"] = ComputeFactionStrength(faction, residentRoot, radianceTier);
    }

    private static void MarkOpenGatesStale(JsonObject root)
    {
        if (root["gates"] is JsonObject gates && GetNodeBool(gates["hasOpenDraft"]))
            gates["isStale"] = true;
    }

    private static bool HasFactionRole(JsonObject? residentRoot, string? factionId, string residentRole)
    {
        if (residentRoot?["entries"] is not JsonArray entries || string.IsNullOrWhiteSpace(factionId))
            return false;

        return entries.OfType<JsonObject>().Any(resident =>
            string.Equals(GetNodeString(resident["ascensionState"]), AscensionStateAscended, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(GetNodeString(resident["shiningFactionId"]), factionId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(GetNodeString(resident["residentRole"]), residentRole, StringComparison.OrdinalIgnoreCase));
    }

    private static string BuildProjectId(string factionId, JsonObject projectDraft, int currentTurnNumber)
    {
        var nameSeed = Slugify(GetNodeString(projectDraft["displayName"]) ?? "project");
        return $"project:{Slugify(factionId)}:{nameSeed}:{currentTurnNumber}:{Guid.NewGuid():N}".ToLowerInvariant();
    }

    private static JsonArray CloneArray(JsonArray? source) =>
        source == null ? new JsonArray() : JsonNode.Parse(source.ToJsonString())!.AsArray();

    private static string Slugify(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "item";

        var buffer = new char[value.Length];
        var index = 0;
        foreach (var ch in value.Trim())
        {
            if (char.IsLetterOrDigit(ch))
                buffer[index++] = char.ToLowerInvariant(ch);
            else if (index == 0 || buffer[index - 1] != '_')
                buffer[index++] = '_';
        }

        var result = new string(buffer, 0, index).Trim('_');
        return string.IsNullOrWhiteSpace(result) ? "item" : result;
    }

    private static bool EnsureActiveGuardianFactionMaterialized(JsonObject root, JsonObject? guardiansRoot)
    {
        if (guardiansRoot?["activeGuardian"] is not JsonObject activeGuardian)
            return false;

        var guardianId = GetNodeString(activeGuardian["guardianId"]);
        if (string.IsNullOrWhiteSpace(guardianId))
            return false;

        var factionId = $"faction_{Slugify(guardianId)}";
        var hallId = $"hall_{Slugify(guardianId)}";
        var guardianName = GuardianManifestation.GetDisplayName(activeGuardian) ??
                           GetNodeString(activeGuardian["canonicalName"]) ??
                           GetNodeString(activeGuardian["name"]) ??
                           guardianId;
        var abodeName = GetNodeString(activeGuardian["abode"]?["abodeName"]) ??
                        GetNodeString(activeGuardian["abode"]?["name"]) ??
                        $"Зал {guardianName}";
        var isFoundedGuardian = PlayerGuardianFoundationState.IsPlayerFoundedGuardian(activeGuardian);
        var derivedOriginType = isFoundedGuardian ? OriginTypePlayerFounded : OriginTypeAscendedGuardian;
        var favoredArchetype = DeriveGuardianFavoredArchetype(activeGuardian);
        var patronEffectFamily = DeriveGuardianPatronEffectFamily(activeGuardian);
        var hallDescription = isFoundedGuardian
            ? $"Обитель основанного Хранителя {guardianName} внутри Сияющей Обители."
            : $"Обитель Хранителя {guardianName} внутри Сияющей Обители.";
        var charterSummary = isFoundedGuardian
            ? $"Фракция, восходящая к основанному Хранителю {guardianName}."
            : $"Фракция, восходящая к Хранителю {guardianName}.";

        var changed = false;
        var halls = EnsureHallsArray(root);
        var hall = halls.OfType<JsonObject>().FirstOrDefault(item => string.Equals(GetNodeString(item["hallId"]), hallId, StringComparison.OrdinalIgnoreCase));
        if (hall == null)
        {
            hall = new JsonObject
            {
                ["hallId"] = hallId,
                ["hallName"] = abodeName,
                ["description"] = hallDescription,
                ["serviceTags"] = new JsonArray(GetPrimaryServiceTagForFamily(patronEffectFamily), GetSecondaryServiceTagForArchetype(favoredArchetype))
            };
            halls.Add(hall);
            changed = true;
        }
        else
        {
            if (!string.Equals(GetNodeString(hall["hallName"]), abodeName, StringComparison.Ordinal))
            {
                hall["hallName"] = abodeName;
                changed = true;
            }

            if (!string.Equals(GetNodeString(hall["description"]), hallDescription, StringComparison.Ordinal))
            {
                hall["description"] = hallDescription;
                changed = true;
            }

            var expectedServiceTags = new[]
            {
                GetPrimaryServiceTagForFamily(patronEffectFamily),
                GetSecondaryServiceTagForArchetype(favoredArchetype)
            };
            if (hall["serviceTags"] is not JsonArray serviceTags ||
                !serviceTags.OfType<JsonValue>()
                    .Select(value => value.TryGetValue<string>(out var tag) ? tag : string.Empty)
                    .Where(tag => !string.IsNullOrWhiteSpace(tag))
                    .SequenceEqual(expectedServiceTags, StringComparer.OrdinalIgnoreCase))
            {
                hall["serviceTags"] = new JsonArray(expectedServiceTags[0], expectedServiceTags[1]);
                changed = true;
            }
        }

        var faction = FindFaction(root, factionId);
        if (faction == null)
        {
            faction = new JsonObject
            {
                ["factionId"] = factionId,
                ["originType"] = derivedOriginType,
                ["hallId"] = hallId,
                ["charter"] = new JsonObject
                {
                    ["factionName"] = guardianName,
                    ["favoredArchetype"] = favoredArchetype,
                    ["patronEffectFamily"] = patronEffectFamily,
                    ["summary"] = charterSummary
                },
                ["leadership"] = new JsonObject
                {
                    ["leadershipState"] = LeadershipStateSecure,
                    ["headActorType"] = HeadActorTypeGuardian,
                    ["headActorId"] = guardianId
                },
                ["baseStrength"] = 35,
                ["factionStrength"] = 35,
                ["investCountThisAscension"] = 0,
                ["projectArchetypesCountedThisAscension"] = new JsonArray(),
                ["projects"] = new JsonArray(),
                ["leadershipReceipts"] = new JsonArray(),
                ["leadershipHistory"] = new JsonArray()
            };
            EnsureFactionsArray(root).Add(faction);
            changed = true;
        }
        else
        {
            if (!string.Equals(GetNodeString(faction["originType"]), derivedOriginType, StringComparison.OrdinalIgnoreCase))
            {
                faction["originType"] = derivedOriginType;
                changed = true;
            }

            if (!string.Equals(GetNodeString(faction["hallId"]), hallId, StringComparison.OrdinalIgnoreCase))
            {
                faction["hallId"] = hallId;
                changed = true;
            }

            if (faction["charter"] is not JsonObject charter)
            {
                charter = new JsonObject();
                faction["charter"] = charter;
                changed = true;
            }

            if (!string.Equals(GetNodeString(charter["factionName"]), guardianName, StringComparison.Ordinal))
            {
                charter["factionName"] = guardianName;
                changed = true;
            }

            if (!string.Equals(GetNodeString(charter["favoredArchetype"]), favoredArchetype, StringComparison.OrdinalIgnoreCase))
            {
                charter["favoredArchetype"] = favoredArchetype;
                changed = true;
            }

            if (!string.Equals(GetNodeString(charter["patronEffectFamily"]), patronEffectFamily, StringComparison.OrdinalIgnoreCase))
            {
                charter["patronEffectFamily"] = patronEffectFamily;
                changed = true;
            }

            if (!string.Equals(GetNodeString(charter["summary"]), charterSummary, StringComparison.Ordinal))
            {
                charter["summary"] = charterSummary;
                changed = true;
            }

            if (faction["leadership"] is not JsonObject leadership)
            {
                leadership = new JsonObject();
                faction["leadership"] = leadership;
                changed = true;
            }

            if (!string.Equals(GetNodeString(leadership["leadershipState"]), LeadershipStateVacant, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(GetNodeString(leadership["headActorType"]), HeadActorTypeGuardian, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(GetNodeString(leadership["headActorId"]), guardianId, StringComparison.OrdinalIgnoreCase))
            {
                leadership["headActorId"] = guardianId;
                changed = true;
            }
        }

        return changed;
    }

    private static string DeriveGuardianFavoredArchetype(JsonObject guardian)
    {
        var signature = $"{GetNodeString(guardian["domain"])} {GetNodeString(guardian["personalityProfile"]?["archetype"])}".ToLowerInvariant();
        if (signature.Contains("memory") || signature.Contains("archive") || signature.Contains("remembrance")) return ProjectArchetypeRemembrance;
        if (signature.Contains("lore") || signature.Contains("knowledge") || signature.Contains("revelation")) return ProjectArchetypeRevelation;
        if (signature.Contains("trade") || signature.Contains("resource") || signature.Contains("wealth")) return ProjectArchetypeProvision;
        if (signature.Contains("forge") || signature.Contains("craft") || signature.Contains("relic")) return ProjectArchetypeRefinement;
        if (signature.Contains("road") || signature.Contains("journey") || signature.Contains("passage") || signature.Contains("gate")) return ProjectArchetypePassage;
        if (signature.Contains("ward") || signature.Contains("protect") || signature.Contains("shield")) return ProjectArchetypeWarding;
        if (signature.Contains("shadow") || signature.Contains("subversion") || signature.Contains("intrigue")) return ProjectArchetypeSubversion;
        return ProjectArchetypeAccord;
    }

    private static string DeriveGuardianPatronEffectFamily(JsonObject guardian)
    {
        var signature = $"{GetNodeString(guardian["domain"])} {GetNodeString(guardian["personalityProfile"]?["archetype"])}".ToLowerInvariant();
        if (signature.Contains("memory") || signature.Contains("archive")) return EffectFamilyMemory;
        if (signature.Contains("lore") || signature.Contains("knowledge")) return EffectFamilyLore;
        if (signature.Contains("trade") || signature.Contains("resource") || signature.Contains("wealth")) return EffectFamilyResource;
        if (signature.Contains("forge") || signature.Contains("relic")) return EffectFamilyRelic;
        if (signature.Contains("road") || signature.Contains("journey") || signature.Contains("passage")) return EffectFamilyDescent;
        if (signature.Contains("ward") || signature.Contains("protect")) return EffectFamilySurvival;
        if (signature.Contains("route")) return EffectFamilyRoute;
        return EffectFamilySocial;
    }

    private static string GetPrimaryServiceTagForFamily(string effectFamily) => effectFamily switch
    {
        EffectFamilyLore => HallServiceTagLore,
        EffectFamilyMemory => HallServiceTagMemory,
        EffectFamilyResource => HallServiceTagResource,
        EffectFamilyRelic => HallServiceTagRelic,
        EffectFamilyDescent or EffectFamilyRoute => HallServiceTagDescent,
        _ => HallServiceTagSocial
    };

    private static string GetSecondaryServiceTagForArchetype(string archetype) => archetype switch
    {
        ProjectArchetypeRevelation => HallServiceTagLore,
        ProjectArchetypeProvision => HallServiceTagResource,
        ProjectArchetypeRemembrance => HallServiceTagMemory,
        ProjectArchetypeRefinement => HallServiceTagRelic,
        ProjectArchetypePassage => HallServiceTagDescent,
        _ => HallServiceTagSocial
    };

    private static bool TryGetOpenFreshGates(JsonObject root, out JsonObject gates, out string? error)
    {
        gates = root["gates"] as JsonObject ?? BuildDefaultGatesObject();
        if (!EnsureOrdinaryActiveState(root, out error))
            return false;

        if (!GetNodeBool(gates["hasOpenDraft"]))
        {
            error = "Сначала открой Врата.";
            return false;
        }

        if (GetNodeBool(gates["isStale"]))
        {
            error = "Текущий draft устарел. Открой Врата заново.";
            return false;
        }

        root["gates"] = gates;
        error = null;
        return true;
    }

    private static List<GateCandidate> BuildGateCandidates(JsonObject root, JsonObject? residentRoot)
    {
        var candidates = new List<GateCandidate>();
        var radianceTier = GetNodeInt(root["radiance"]?["tier"], 0);
        var radianceCeiling = GetRadianceRarityCeiling(radianceTier);
        var subversionPenalties = BuildSubversionPenaltyLookup(root);

        foreach (var faction in EnsureFactionsArray(root).OfType<JsonObject>())
        {
            var factionId = GetNodeString(faction["factionId"]) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(factionId))
                continue;

            var effectiveStrength = Math.Max(0, GetNodeInt(faction["factionStrength"], 0) + subversionPenalties.GetValueOrDefault(factionId));
            var effectiveCeiling = GetFactionRarityCeiling(effectiveStrength);
            var effectiveBand = GetFactionStrengthBand(effectiveStrength);
            var factionName = GetNodeString(faction["charter"]?["factionName"]) ?? factionId;
            var factionRoles = CollectFactionRoles(residentRoot, factionId);

            var patronFamily = GetNodeString(faction["charter"]?["patronEffectFamily"]) ?? EffectFamilySocial;
            var headRarity = MinRarity(radianceCeiling, effectiveCeiling);
            var headPayload = BuildFamilyPayload(patronFamily, headRarity);
            ApplyResidentRoleModifiers(headPayload, patronFamily, factionRoles);
            candidates.Add(CreateGateCandidate(BuildBlessingCard(
                CardSourceTypeHead,
                factionId,
                GetNodeString(faction["leadership"]?["headActorId"]) ?? string.Empty,
                factionName,
                GetNodeString(faction["leadership"]?["headActorId"]) ?? string.Empty,
                patronFamily,
                headRarity,
                $"Дар фракции «{factionName}»",
                BuildCardSummary(patronFamily, headRarity, headPayload),
                headPayload,
                effectiveStrength,
                BuildPayloadDedupeKey(patronFamily, headPayload)), effectiveStrength));

            var supportedPassage = false;
            if (faction["projects"] is JsonArray projects)
            {
                foreach (var project in projects.OfType<JsonObject>())
                {
                    if (!IsSupportedCompletedProject(project))
                        continue;

                    var projectArchetype = GetNodeString(project["projectArchetype"]) ?? ProjectArchetypeAccord;
                    if (string.Equals(projectArchetype, ProjectArchetypePassage, StringComparison.OrdinalIgnoreCase))
                        supportedPassage = true;

                    var projectFamily = GetNodeString(project["outputEffectFamily"]) ?? ResolveDefaultOutputFamily(projectArchetype);
                    var projectRarity = ResolveProjectCardRarity(project, effectiveBand, radianceCeiling, effectiveCeiling);
                    var projectPayload = BuildFamilyPayload(projectFamily, projectRarity);
                    ApplyResidentRoleModifiers(projectPayload, projectFamily, factionRoles);
                    ApplyProjectArchetypeModifier(projectPayload, projectArchetype, projectFamily);
                    candidates.Add(CreateGateCandidate(BuildBlessingCard(
                        CardSourceTypeProject,
                        factionId,
                        GetNodeString(project["projectId"]) ?? string.Empty,
                        factionName,
                        GetNodeString(project["displayName"]) ?? $"Проект {projectArchetype}",
                        projectFamily,
                        projectRarity,
                        GetNodeString(project["displayName"]) ?? $"Проект {projectArchetype}",
                        GetNodeString(project["summary"]) ?? BuildCardSummary(projectFamily, projectRarity, projectPayload),
                        projectPayload,
                        effectiveStrength,
                        BuildPayloadDedupeKey(projectFamily, projectPayload)), effectiveStrength));
                }
            }

            if (!supportedPassage || residentRoot?["entries"] is not JsonArray residentEntries)
                continue;

            foreach (var resident in residentEntries.OfType<JsonObject>())
            {
                if (!string.Equals(GetNodeString(resident["ascensionState"]), AscensionStateAscended, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(GetNodeString(resident["shiningFactionId"]), factionId, StringComparison.OrdinalIgnoreCase) ||
                    string.IsNullOrWhiteSpace(GetNodeString(resident["grantedRelicId"])))
                {
                    continue;
                }

                var residentRarity = MinRarity(radianceCeiling, effectiveCeiling);
                var descentPayload = BuildFamilyPayload(EffectFamilyDescent, residentRarity);
                ApplyResidentRoleModifiers(descentPayload, EffectFamilyDescent, factionRoles);
                var residentName = GetNodeString(resident["displayName"]) ?? GetNodeString(resident["residentId"]) ?? "resident";
                var residentId = GetNodeString(resident["residentId"]) ?? residentName;
                candidates.Add(CreateGateCandidate(BuildBlessingCard(
                    CardSourceTypeResidentDescent,
                    factionId,
                    residentId,
                    factionName,
                    residentName,
                    EffectFamilyDescent,
                    residentRarity,
                    $"Нисхождение {residentName}",
                    BuildCardSummary(EffectFamilyDescent, residentRarity, descentPayload),
                    descentPayload,
                    effectiveStrength,
                    BuildPayloadDedupeKey(EffectFamilyDescent, descentPayload, residentId)), effectiveStrength));
            }
        }

        return candidates
            .GroupBy(candidate => candidate.DedupeKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderByDescending(candidate => candidate.RarityWeight)
                .ThenByDescending(candidate => candidate.EffectiveStrength)
                .ThenByDescending(candidate => candidate.SourcePriority)
                .ThenBy(candidate => GetNodeString(candidate.Card["cardId"]), StringComparer.OrdinalIgnoreCase)
                .First())
            .OrderByDescending(candidate => candidate.RarityWeight)
            .ThenByDescending(candidate => candidate.EffectiveStrength)
            .ThenByDescending(candidate => candidate.SourcePriority)
            .ThenBy(candidate => GetNodeString(candidate.Card["cardId"]), StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static Dictionary<string, int> BuildSubversionPenaltyLookup(JsonObject root)
    {
        var penalties = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var faction in EnsureFactionsArray(root).OfType<JsonObject>())
        {
            if (faction["projects"] is not JsonArray projects)
                continue;

            foreach (var project in projects.OfType<JsonObject>())
            {
                if (!IsSupportedCompletedProject(project) ||
                    !string.Equals(GetNodeString(project["projectArchetype"]), ProjectArchetypeSubversion, StringComparison.OrdinalIgnoreCase) ||
                    project["targetFactionIds"] is not JsonArray targets ||
                    targets.Count == 0 ||
                    targets[0] is not JsonValue targetNode ||
                    !targetNode.TryGetValue<string>(out var targetFactionId) ||
                    string.IsNullOrWhiteSpace(targetFactionId))
                {
                    continue;
                }

                penalties[targetFactionId] = -5;
            }
        }

        return penalties;
    }

    private static HashSet<string> CollectFactionRoles(JsonObject? residentRoot, string factionId)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (residentRoot?["entries"] is not JsonArray entries)
            return result;

        foreach (var resident in entries.OfType<JsonObject>())
        {
            if (!string.Equals(GetNodeString(resident["ascensionState"]), AscensionStateAscended, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(GetNodeString(resident["shiningFactionId"]), factionId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var role = GetNodeString(resident["residentRole"]);
            if (IsSupportedResidentRole(role))
                result.Add(role!);
        }

        return result;
    }

    private static void ApplyResidentRoleModifiers(JsonObject payload, string effectFamily, HashSet<string> factionRoles)
    {
        if (factionRoles.Contains(ResidentRoleArchiveSupport) &&
            string.Equals(effectFamily, EffectFamilyMemory, StringComparison.OrdinalIgnoreCase))
        {
            payload["rerolls"] = GetNodeInt(payload["rerolls"], 0) + 1;
        }

        if (factionRoles.Contains(ResidentRoleSocialSupport))
        {
            if (string.Equals(effectFamily, EffectFamilySocial, StringComparison.OrdinalIgnoreCase))
                payload["delta"] = GetNodeInt(payload["delta"], 0) + 5;
            else if (string.Equals(effectFamily, EffectFamilyRoute, StringComparison.OrdinalIgnoreCase))
                payload["latestTurn"] = Math.Max(1, GetNodeInt(payload["latestTurn"], 1) - 1);
        }

        if (factionRoles.Contains(ResidentRoleResourceSupport) &&
            string.Equals(effectFamily, EffectFamilyResource, StringComparison.OrdinalIgnoreCase))
        {
            payload["money"] = GetNodeInt(payload["money"], 0) + 50;
            payload["common"] = GetNodeInt(payload["common"], 0) + 1;
        }

        if (factionRoles.Contains(ResidentRoleDescentSupport) &&
            string.Equals(effectFamily, EffectFamilyDescent, StringComparison.OrdinalIgnoreCase))
        {
            payload["latestTurn"] = Math.Max(1, GetNodeInt(payload["latestTurn"], 1) - 3);
            payload["quality"] = GetNodeInt(payload["quality"], 0) + 15;
        }
    }

    private static void ApplyProjectArchetypeModifier(JsonObject payload, string projectArchetype, string effectFamily)
    {
        if (string.Equals(projectArchetype, ProjectArchetypeRevelation, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(effectFamily, EffectFamilyLore, StringComparison.OrdinalIgnoreCase))
        {
            payload["latestTurn"] = Math.Max(1, GetNodeInt(payload["latestTurn"], 1) - 2);
        }
        else if (string.Equals(projectArchetype, ProjectArchetypeAccord, StringComparison.OrdinalIgnoreCase) &&
                 string.Equals(effectFamily, EffectFamilySocial, StringComparison.OrdinalIgnoreCase))
        {
            payload["delta"] = GetNodeInt(payload["delta"], 0) + 5;
        }
        else if (string.Equals(projectArchetype, ProjectArchetypeWarding, StringComparison.OrdinalIgnoreCase) &&
                 string.Equals(effectFamily, EffectFamilySurvival, StringComparison.OrdinalIgnoreCase))
        {
            payload["recovery"] = GetNodeInt(payload["recovery"], 0) + 10;
        }
    }

    private static JsonObject BuildBlessingCard(
        string sourceType,
        string sourceFactionId,
        string sourceActorId,
        string sourceFactionName,
        string sourceActorName,
        string effectFamily,
        string rarity,
        string displayName,
        string displaySummary,
        JsonObject effectPayload,
        int effectiveStrength,
        string dedupeKey)
    {
        return new JsonObject
        {
            ["cardId"] = BuildCardId(sourceFactionId, sourceType, sourceActorId, effectFamily, rarity),
            ["dedupeKey"] = dedupeKey,
            ["sourceType"] = sourceType,
            ["sourceFactionId"] = sourceFactionId,
            ["sourceActorId"] = sourceActorId,
            ["sourceFactionName"] = sourceFactionName,
            ["sourceActorName"] = sourceActorName,
            ["effectFamily"] = effectFamily,
            ["rarity"] = rarity,
            ["displayName"] = displayName,
            ["displaySummary"] = displaySummary,
            ["effectPayload"] = effectPayload,
            ["_effectiveStrength"] = effectiveStrength
        };
    }

    private static GateCandidate CreateGateCandidate(JsonObject card, int effectiveStrength)
    {
        var sourceType = GetNodeString(card["sourceType"]) ?? CardSourceTypeHead;
        var rarity = GetNodeString(card["rarity"]) ?? RarityCommon;
        var dedupeKey = GetNodeString(card["dedupeKey"]) ?? string.Empty;
        return new GateCandidate(
            card,
            dedupeKey,
            effectiveStrength,
            GetSourceTypePriority(sourceType),
            GetRarityWeight(rarity));
    }

    private static JsonObject BuildFamilyPayload(string effectFamily, string rarity)
    {
        return effectFamily switch
        {
            EffectFamilyLore => new JsonObject { ["type"] = "insert_lore_clues", ["clueCount"] = string.Equals(rarity, RarityRadiant, StringComparison.OrdinalIgnoreCase) ? 2 : 1, ["latestTurn"] = rarity switch { RarityCommon => 12, RarityUncommon => 10, _ => 8 } },
            EffectFamilySocial => new JsonObject { ["type"] = "modify_first_ally_relation", ["delta"] = rarity switch { RarityCommon => 10, RarityUncommon => 15, RarityRare => 20, _ => 25 } },
            EffectFamilyResource => new JsonObject { ["type"] = "grant_starting_resources", ["money"] = rarity switch { RarityCommon => 100, RarityUncommon => 150, RarityRare => 225, _ => 300 }, ["common"] = rarity switch { RarityCommon => 1, RarityUncommon => 2, RarityRare => 3, _ => 4 }, ["uncommon"] = rarity switch { RarityCommon => 0, RarityUncommon => 1, RarityRare => 1, _ => 2 } },
            EffectFamilyMemory => new JsonObject { ["type"] = "expand_memory_selection", ["options"] = rarity is RarityCommon or RarityUncommon ? 1 : 2, ["rerolls"] = rarity switch { RarityCommon => 0, RarityUncommon => 1, RarityRare => 1, _ => 2 } },
            EffectFamilyDescent => new JsonObject { ["type"] = "guide_resident_descent", ["latestTurn"] = rarity switch { RarityCommon => 12, RarityUncommon => 10, RarityRare => 8, _ => 6 }, ["quality"] = rarity switch { RarityCommon => 5, RarityUncommon => 10, RarityRare => 15, _ => 20 } },
            EffectFamilySurvival => new JsonObject { ["type"] = "downgrade_ruinous_failure", ["downgrade"] = 1, ["recovery"] = rarity switch { RarityCommon => 0, RarityUncommon => 10, RarityRare => 20, _ => 30 } },
            EffectFamilyRelic => new JsonObject { ["type"] = "grant_relic_refinement", ["rerolls"] = rarity switch { RarityCommon => 1, RarityUncommon => 2, RarityRare => 2, _ => 3 }, ["freeShape"] = rarity is RarityRare or RarityRadiant, ["freeRetune"] = string.Equals(rarity, RarityRadiant, StringComparison.OrdinalIgnoreCase) },
            EffectFamilyRoute => new JsonObject { ["type"] = "seed_early_routes", ["routeOptions"] = string.Equals(rarity, RarityRadiant, StringComparison.OrdinalIgnoreCase) ? 2 : 1, ["latestTurn"] = rarity switch { RarityCommon => 10, RarityUncommon => 8, _ => 6 } },
            _ => new JsonObject { ["type"] = "unknown" }
        };
    }

    private static string BuildCardSummary(string effectFamily, string rarity, JsonObject payload)
    {
        _ = rarity;
        return effectFamily switch
        {
            EffectFamilyLore => $"К {GetNodeInt(payload["latestTurn"], 12)} ходу явит {GetNodeInt(payload["clueCount"], 1)} lore clue.",
            EffectFamilySocial => $"Первый союзник начнёт ближе к доверию (+{GetNodeInt(payload["delta"], 0)}).",
            EffectFamilyResource => $"+{GetNodeInt(payload["money"], 0)} денег, common x{GetNodeInt(payload["common"], 0)}, uncommon x{GetNodeInt(payload["uncommon"], 0)}.",
            EffectFamilyMemory => $"+{GetNodeInt(payload["options"], 0)} вариантов памяти, rerolls {GetNodeInt(payload["rerolls"], 0)}.",
            EffectFamilyDescent => $"Эхо спутника придёт не позже {GetNodeInt(payload["latestTurn"], 0)} хода с quality +{GetNodeInt(payload["quality"], 0)}.",
            EffectFamilySurvival => $"Ослабит первый ruinous failure и восстановит {GetNodeInt(payload["recovery"], 0)}% потерь.",
            EffectFamilyRelic => $"Relic rerolls {GetNodeInt(payload["rerolls"], 0)}, reshape={GetNodeBool(payload["freeShape"])}, retune={GetNodeBool(payload["freeRetune"])}.",
            EffectFamilyRoute => $"Откроет {GetNodeInt(payload["routeOptions"], 0)} ранних route option к {GetNodeInt(payload["latestTurn"], 0)} ходу.",
            _ => effectFamily
        };
    }

    private static string BuildPayloadDedupeKey(string effectFamily, JsonObject payload, string? sourceActorId = null)
    {
        var clone = CloneObject(payload);
        clone.Remove("type");
        var key = $"{effectFamily}:{clone.ToJsonString()}";
        if (!string.IsNullOrWhiteSpace(sourceActorId))
            key += $":{sourceActorId}";
        return key;
    }

    private static JsonObject CloneCardForPersistence(JsonObject card)
    {
        var clone = CloneObject(card);
        clone.Remove("_effectiveStrength");
        return clone;
    }

    private static string ResolveProjectCardRarity(JsonObject project, string effectiveBand, string radianceCeiling, string effectiveCeiling)
    {
        var rarity = ProjectTierBaseRarities[Math.Clamp(GetNodeInt(project["tier"], 1), 1, 3)];
        if (string.Equals(effectiveBand, "Radiant", StringComparison.OrdinalIgnoreCase))
            rarity = UpgradeRarity(rarity);
        return MinRarity(MinRarity(rarity, radianceCeiling), effectiveCeiling);
    }

    private static string UpgradeRarity(string rarity) => rarity switch
    {
        RarityCommon => RarityUncommon,
        RarityUncommon => RarityRare,
        RarityRare => RarityRadiant,
        _ => RarityRadiant
    };

    private static string MinRarity(string left, string right) => GetRarityWeight(left) <= GetRarityWeight(right) ? left : right;

    private static int GetRarityWeight(string? rarity) => !string.IsNullOrWhiteSpace(rarity) && RarityWeight.TryGetValue(rarity, out var weight) ? weight : 0;

    private static int GetSourceTypePriority(string? sourceType) => !string.IsNullOrWhiteSpace(sourceType) && SourceTypePriority.TryGetValue(sourceType, out var priority) ? priority : 0;

    private static bool IsSupportedCompletedProject(JsonObject project) =>
        string.Equals(GetNodeString(project["status"]), ProjectStatusCompleted, StringComparison.OrdinalIgnoreCase) &&
        GetNodeBool(project["isSupported"]);

    private static string BuildCardId(string factionId, string sourceType, string sourceActorId, string effectFamily, string rarity) =>
        $"card:{Slugify(factionId)}:{Slugify(sourceType)}:{Slugify(sourceActorId)}:{Slugify(effectFamily)}:{Slugify(rarity)}".ToLowerInvariant();
}
