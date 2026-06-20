using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;

namespace BookOfEternityClient.Services;

internal static class GuardianPolicyContracts
{
    internal const string InvalidMetaStateUpdatesMessage =
        "game_state/meta/soul_state.json current metaStateUpdates must be a JsonObject if present.";

    internal const string InvalidMetaStateInkFeatherChangesMessage =
        "game_state/meta/soul_state.json current metaStateUpdates.inkFeatherChanges must contain only visible add/spend buckets as non-negative integer JSON numbers.";

    internal const string InvalidMetaStateEnlightenmentProgressionMessage =
        "game_state/meta/soul_state.json current metaStateUpdates.enlightenmentProgression must contain only canonical visible keys newTier/experience with required non-negative integer experience and optional non-negative integer newTier.";

    internal const string InvalidMetaStateSoulRelicOperationsMessage =
        "game_state/meta/soul_state.json current metaStateUpdates.soulRelicOperations must contain only canonical visible ops as JsonObject payloads with required identifiers.";

    internal const string InvalidMetaStateLifeTransitionsMessage =
        "game_state/meta/soul_state.json current metaStateUpdates.lifeTransitions must contain only canonical recordLifeCompletion payload with required fields.";

    internal const string InvalidMetaStateLifeTransitionsTriggerContextMessage =
        "game_state/meta/soul_state.json current metaStateUpdates.lifeTransitions.recordLifeCompletion requires canonical TriggerLifeEnd authority in game_state/control/life_transitions.json during normalization.";

    internal const string InvalidMetaStateMemoryLegacyGrantMessage =
        "game_state/meta/soul_state.json current metaStateUpdates.memoryLegacyGrant must contain canonical structured grant fields for supported legacy types.";

    internal const string InvalidCanonicalInkFeathersRootMessage =
        "game_state/meta/soul_state.json current inkFeathers must already be a canonical object with required non-negative integer current and optional non-negative integer total.";

    internal const string InvalidCanonicalSoulRelicsRootMessage =
        "game_state/meta/soul_state.json current soulRelics must already be a canonical object with equipped/stored JsonArray collections when present.";

    internal const string InvalidManifestationCurrentIncarnationMessage =
        "game_state/meta/soul_state.json current currentIncarnation must be a positive integer for companion manifestation authority reads.";

    internal const string InvalidAfterlifeArchiveUpdatesMessage =
        "game_state/meta/soul_state.json current afterlifeArchiveUpdates must be a JsonArray of canonical archive update objects if present.";

    internal const string InvalidArchiveActionResolutionsMessage =
        "game_state/meta/soul_state.json current archiveActionResolutions must be a JsonArray of canonical archive resolution objects if present.";

    [Flags]
    internal enum SoulStatePatchTouchedDomains
    {
        None = 0,
        InkFeathers = 1 << 0,
        SoulRelics = 1 << 1,
        AfterlifeArchive = 1 << 2,
        LivesHistory = 1 << 3,
        PendingMemoryLegacy = 1 << 4,
        Enlightenment = 1 << 5,
        SoulProgression = 1 << 6,
        PendingShiningBlessingEffects = 1 << 7
    }

    internal sealed class SoulStatePatchConflictContext
    {
        internal static readonly SoulStatePatchConflictContext None = new(SoulStatePatchTouchedDomains.None);

        internal SoulStatePatchConflictContext(
            SoulStatePatchTouchedDomains touchedDomains,
            IEnumerable<string>? upsertedSoulRelicIds = null,
            IEnumerable<string>? unsafeToReplayAddedSoulRelicIds = null,
            IEnumerable<string>? removedSoulRelicIds = null,
            IEnumerable<string>? equipStateChangedSoulRelicIds = null,
            IReadOnlyDictionary<string, IEnumerable<string>>? updatedSoulRelicFieldsById = null,
            IEnumerable<string>? affectedArchiveIds = null,
            IEnumerable<string>? affectedArchiveRequestIds = null)
        {
            TouchedDomains = touchedDomains;
            UpsertedSoulRelicIds = CreateCaseInsensitiveSet(upsertedSoulRelicIds);
            UnsafeToReplayAddedSoulRelicIds = CreateCaseInsensitiveSet(unsafeToReplayAddedSoulRelicIds);
            RemovedSoulRelicIds = CreateCaseInsensitiveSet(removedSoulRelicIds);
            EquipStateChangedSoulRelicIds = CreateCaseInsensitiveSet(equipStateChangedSoulRelicIds);
            UpdatedSoulRelicFieldsById = CreateCaseInsensitiveNestedSet(updatedSoulRelicFieldsById);
            AffectedArchiveIds = CreateCaseInsensitiveSet(affectedArchiveIds);
            AffectedArchiveRequestIds = CreateCaseInsensitiveSet(affectedArchiveRequestIds);
        }

        internal SoulStatePatchTouchedDomains TouchedDomains { get; }

        internal HashSet<string> UpsertedSoulRelicIds { get; }

        internal HashSet<string> UnsafeToReplayAddedSoulRelicIds { get; }

        internal HashSet<string> RemovedSoulRelicIds { get; }

        internal HashSet<string> EquipStateChangedSoulRelicIds { get; }

        internal Dictionary<string, HashSet<string>> UpdatedSoulRelicFieldsById { get; }

        internal HashSet<string> AffectedArchiveIds { get; }

        internal HashSet<string> AffectedArchiveRequestIds { get; }
    }

    internal const string NpcCoreUpdateSectionName = "UpdateNPCs";
    internal const string NpcCoreSceneSectionName = "NPCsInScene";
    internal const string NpcCoreRenameSectionName = "NPCsRenameData";

    internal static readonly HashSet<string> SoulStateLifecycleCompatibilityOnlyTopLevelKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "crossIncarnationData"
    };

    internal static readonly HashSet<string> MetaStateVisibleTopLevelCommandKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "inkFeatherChanges",
        "enlightenmentProgression",
        "soulRelicOperations",
        "lifeTransitions",
        "memoryLegacyGrant"
    };

    internal static readonly HashSet<string> SoulStateStrictAuthorityTopLevelKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "metaStateUpdates",
        "afterlifeArchiveUpdates",
        "archiveActionResolutions",
        "soulName",
        "previousSoulNames",
        "soulFormDescription",
        "currentRealm",
        "currentIncarnation",
        "enlightenment",
        "soulProgression",
        "inkFeathers",
        "soulRelics",
        "afterlifeArchive",
        "livesHistory",
        "soulImprint",
        "pendingMemoryLegacy",
        AfterlifeSpiritualConflictState.SoulStateProfileProperty,
        ShiningBlessingEffectState.SoulStateProperty,
        PlayerGuardianFoundationState.SoulStateGuardianIdProperty,
        PlayerGuardianFoundationState.SoulStateFoundationStatusProperty
    };

    internal static readonly HashSet<string> SoulStatePatchWriteTopLevelKeys =
        CreateSoulStatePatchWriteTopLevelKeys();

    internal static readonly HashSet<string> SoulStateCanonicalWriteTopLevelKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "soulName",
        "previousSoulNames",
        "soulFormDescription",
        "currentRealm",
        "currentIncarnation",
        "enlightenment",
        "soulProgression",
        "inkFeathers",
        "soulRelics",
        "afterlifeArchive",
        "livesHistory",
        "soulImprint",
        "pendingMemoryLegacy",
        AfterlifeSpiritualConflictState.SoulStateProfileProperty,
        ShiningBlessingEffectState.SoulStateProperty,
        PlayerGuardianFoundationState.SoulStateGuardianIdProperty,
        PlayerGuardianFoundationState.SoulStateFoundationStatusProperty
    };

    internal static readonly HashSet<string> SoulStateLifecycleTopLevelKeys =
        CreateSoulStateLifecycleTopLevelKeys();

    internal static readonly HashSet<string> NpcCoreLifecycleNonCarrierTopLevelSections = new(StringComparer.OrdinalIgnoreCase)
    {
        NpcCoreRenameSectionName,
        NpcTradeRequestState.UpdateReceiptsProperty
    };

    internal static readonly string[] NpcCoreCanonicalNpcObjectSections =
    {
        NpcCoreUpdateSectionName,
        NpcCoreSceneSectionName
    };

    internal static readonly HashSet<string> NpcCoreLifecycleTopLevelSections =
        CreateNpcCoreLifecycleTopLevelSections();

    internal static readonly string[] ManifestedCompanionNpcCarrierSections =
    {
        NpcCoreUpdateSectionName,
        NpcCoreSceneSectionName
    };

    internal static readonly string[] NpcCoreLegacyAliasSections =
    {
        "NPCs",
        "npcs",
        "npcDataChanges"
    };

    private static readonly string[] ManifestedCompanionNpcSourceFieldNames =
    {
        "sourceCompanionRelicId",
        "sourceAfterlifeResidentId",
        "sourceSoulImprintId"
    };

    private static HashSet<string> CreateSoulStateLifecycleTopLevelKeys()
    {
        var keys = new HashSet<string>(SoulStateStrictAuthorityTopLevelKeys, StringComparer.OrdinalIgnoreCase);
        keys.UnionWith(SoulStateLifecycleCompatibilityOnlyTopLevelKeys);
        return keys;
    }

    private static HashSet<string> CreateSoulStatePatchWriteTopLevelKeys()
    {
        return new HashSet<string>(SoulStateStrictAuthorityTopLevelKeys, StringComparer.OrdinalIgnoreCase);
    }

    private static HashSet<string> CreateNpcCoreLifecycleTopLevelSections()
    {
        var sections = new HashSet<string>(NpcCoreCanonicalNpcObjectSections, StringComparer.OrdinalIgnoreCase);
        sections.UnionWith(NpcCoreLifecycleNonCarrierTopLevelSections);
        return sections;
    }

    internal static bool TryDescribeUnsupportedGuardianPolicySoulStateTopLevelKeys(
        JsonElement root,
        out string? failureDescription)
    {
        return TryDescribeUnsupportedSoulStateTopLevelKeys(
            root,
            SoulStateStrictAuthorityTopLevelKeys,
            out failureDescription);
    }

    internal static bool TryDescribeUnsupportedGuardianPolicySoulStateTopLevelKeys(
        JsonObject root,
        out string? failureDescription)
    {
        return TryDescribeUnsupportedSoulStateTopLevelKeys(
            root,
            SoulStateStrictAuthorityTopLevelKeys,
            out failureDescription);
    }

    internal static bool TryDescribeUnsupportedGuardianPolicySoulStateTopLevelKeys(
        string? soulStateJson,
        out string? failureDescription)
    {
        failureDescription = null;
        if (string.IsNullOrWhiteSpace(soulStateJson))
            return false;

        try
        {
            using var soulStateDoc = JsonDocument.Parse(soulStateJson);
            return TryDescribeUnsupportedGuardianPolicySoulStateTopLevelKeys(
                soulStateDoc.RootElement,
                out failureDescription);
        }
        catch
        {
            return false;
        }
    }

    internal static bool TryDescribeUnsupportedCanonicalSoulStateTopLevelKeys(
        JsonElement root,
        out string? failureDescription)
    {
        return TryDescribeUnsupportedSoulStateTopLevelKeys(
            root,
            SoulStateCanonicalWriteTopLevelKeys,
            out failureDescription);
    }

    internal static bool TryDescribeUnsupportedCanonicalSoulStateTopLevelKeys(
        JsonObject root,
        out string? failureDescription)
    {
        return TryDescribeUnsupportedSoulStateTopLevelKeys(
            root,
            SoulStateCanonicalWriteTopLevelKeys,
            out failureDescription);
    }

    internal static bool SanitizeSoulStateForPatchWrite(
        JsonObject? root,
        SoulStatePatchTouchedDomains touchedDomains)
    {
        return SanitizeSoulStateForPatchWrite(
            root,
            new SoulStatePatchConflictContext(touchedDomains));
    }

    internal static bool SanitizeSoulStateForPatchWrite(
        JsonObject? root,
        SoulStatePatchConflictContext? conflictContext)
    {
        var context = conflictContext ?? SoulStatePatchConflictContext.None;
        var removedAny = SanitizeSoulStateTopLevelKeys(root, SoulStatePatchWriteTopLevelKeys);
        if (root == null)
            return removedAny;

        EnsureStrictCanonicalSoulStateRootsForPolicySensitiveWrite(root);
        ValidateArchiveTransientRootsForPatchWrite(root);
        removedAny |= PruneConflictingMetaStateUpdates(root, context);
        removedAny |= PruneConflictingArchiveTransientRoots(root, context);

        return removedAny;
    }

    internal static bool SanitizeSoulStateForCanonicalWrite(JsonObject? root)
    {
        return SanitizeSoulStateTopLevelKeys(root, SoulStateCanonicalWriteTopLevelKeys);
    }

    internal static JsonObject CreatePatchedSoulStateWriteRoot(
        JsonObject? root,
        SoulStatePatchTouchedDomains touchedDomains)
    {
        return CreatePatchedSoulStateWriteRoot(
            root,
            new SoulStatePatchConflictContext(touchedDomains));
    }

    internal static JsonObject CreatePatchedSoulStateWriteRoot(
        JsonObject? root,
        SoulStatePatchConflictContext? conflictContext)
    {
        var clone = root?.DeepClone() as JsonObject ?? new JsonObject();
        SanitizeSoulStateForPatchWrite(clone, conflictContext);
        return clone;
    }

    internal static JsonObject CreateCanonicalSoulStateWriteRoot(JsonObject? root)
    {
        var clone = root?.DeepClone() as JsonObject ?? new JsonObject();
        SanitizeSoulStateForCanonicalWrite(clone);
        return clone;
    }

    internal static void EnsureStrictCanonicalSoulStateRootsForPolicySensitiveWrite(JsonObject root)
    {
        if (TryDescribeInvalidCanonicalSoulStateRoots(root, out var failureDescription))
            throw new InvalidOperationException(failureDescription);
    }

    internal static bool TryDescribeInvalidCanonicalSoulStateRoots(
        JsonObject root,
        out string failureDescription)
    {
        if (TryDescribeInvalidCanonicalInkFeathersRoot(root, out failureDescription))
            return true;

        if (TryDescribeInvalidCanonicalSoulRelicsRoot(root, out failureDescription))
            return true;

        if (AfterlifeArchiveState.TryDescribeInvalidCanonicalArchiveRoot(root, out failureDescription))
            return true;

        failureDescription = string.Empty;
        return false;
    }

    internal static bool HasManifestedCompanionNpcDependencySurface(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
            return false;

        foreach (var sectionName in ManifestedCompanionNpcCarrierSections)
        {
            if (!root.TryGetProperty(sectionName, out var section))
                continue;

            if (SectionMayContainManifestedCompanionNpcDependencySurface(section))
                return true;
        }

        return false;
    }

    internal static bool ProbeManifestedCompanionNpcDependencySurface(string? npcCoreJson)
    {
        if (string.IsNullOrWhiteSpace(npcCoreJson))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(npcCoreJson);
            return HasManifestedCompanionNpcDependencySurface(doc.RootElement);
        }
        catch
        {
            foreach (var sectionName in ManifestedCompanionNpcCarrierSections)
            {
                if (!TryExtractTopLevelJsonContainer(npcCoreJson, sectionName, out var containerText))
                    continue;

                if (TextMayContainManifestedCompanionNpcDependencySurface(containerText))
                    return true;
            }

            return false;
        }
    }

    internal static IEnumerable<JsonArray> EnumerateCanonicalNpcObjectArrays(JsonObject? root)
    {
        if (root == null)
            yield break;

        foreach (var sectionName in NpcCoreCanonicalNpcObjectSections)
        {
            if (root[sectionName] is JsonArray array)
                yield return array;
        }
    }

    internal static IEnumerable<JsonObject> EnumerateCanonicalNpcObjects(JsonObject? root)
    {
        foreach (var array in EnumerateCanonicalNpcObjectArrays(root))
        {
            foreach (var npc in array.OfType<JsonObject>())
                yield return npc;
        }
    }

    internal static IEnumerable<JsonElement> EnumerateCanonicalNpcObjects(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
            yield break;

        foreach (var sectionName in NpcCoreCanonicalNpcObjectSections)
        {
            if (!root.TryGetProperty(sectionName, out var npcs) || npcs.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var npc in npcs.EnumerateArray())
            {
                if (npc.ValueKind == JsonValueKind.Object)
                    yield return npc;
            }
        }
    }

    internal static JsonObject? FindCanonicalNpcObject(JsonObject? root, string npcId)
    {
        if (root == null || string.IsNullOrWhiteSpace(npcId))
            return null;

        return EnumerateCanonicalNpcObjects(root).FirstOrDefault(npc =>
            string.Equals(GetNpcId(npc), npcId, StringComparison.OrdinalIgnoreCase));
    }

    internal static bool ContainsCanonicalNpcObject(JsonObject? root, string npcId)
    {
        return FindCanonicalNpcObject(root, npcId) != null;
    }

    private static bool SectionMayContainManifestedCompanionNpcDependencySurface(JsonElement section)
    {
        return section.ValueKind switch
        {
            JsonValueKind.Array => section.EnumerateArray().Any(ItemMayContainManifestedCompanionNpcDependencySurface),
            JsonValueKind.Object => ItemMayContainManifestedCompanionNpcDependencySurface(section),
            _ => false
        };
    }

    private static bool ItemMayContainManifestedCompanionNpcDependencySurface(JsonElement item)
    {
        if (item.ValueKind != JsonValueKind.Object)
            return false;

        foreach (var prop in item.EnumerateObject())
        {
            if (ManifestedCompanionNpcSourceFieldNames.Contains(prop.Name, StringComparer.OrdinalIgnoreCase))
                return true;

            if (prop.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array &&
                NodeMayContainManifestedCompanionNpcDependencySurface(prop.Value))
            {
                return true;
            }
        }

        return false;
    }

    private static bool NodeMayContainManifestedCompanionNpcDependencySurface(JsonElement node)
    {
        return node.ValueKind switch
        {
            JsonValueKind.Object => ItemMayContainManifestedCompanionNpcDependencySurface(node),
            JsonValueKind.Array => node.EnumerateArray().Any(NodeMayContainManifestedCompanionNpcDependencySurface),
            _ => false
        };
    }

    private static bool TryDescribeUnsupportedSoulStateTopLevelKeys(
        JsonElement root,
        HashSet<string> allowedTopLevelKeys,
        out string? failureDescription)
    {
        failureDescription = null;
        if (root.ValueKind != JsonValueKind.Object)
            return false;

        var unsupportedVisibleTopLevelKeys = root.EnumerateObject()
            .Where(prop => !prop.Name.StartsWith("_", StringComparison.OrdinalIgnoreCase) &&
                           !allowedTopLevelKeys.Contains(prop.Name))
            .Select(prop => prop.Name)
            .ToList();
        if (unsupportedVisibleTopLevelKeys.Count == 0)
            return false;

        failureDescription = $"unsupported visible top-level keys: {string.Join(", ", unsupportedVisibleTopLevelKeys)}";
        return true;
    }

    private static bool TryDescribeUnsupportedSoulStateTopLevelKeys(
        JsonObject root,
        HashSet<string> allowedTopLevelKeys,
        out string? failureDescription)
    {
        failureDescription = null;

        var unsupportedVisibleTopLevelKeys = root
            .Where(prop => !prop.Key.StartsWith("_", StringComparison.OrdinalIgnoreCase) &&
                           !allowedTopLevelKeys.Contains(prop.Key))
            .Select(prop => prop.Key)
            .ToList();
        if (unsupportedVisibleTopLevelKeys.Count == 0)
            return false;

        failureDescription = $"unsupported visible top-level keys: {string.Join(", ", unsupportedVisibleTopLevelKeys)}";
        return true;
    }

    private static bool SanitizeSoulStateTopLevelKeys(
        JsonObject? root,
        HashSet<string> allowedTopLevelKeys)
    {
        if (root == null)
            return false;

        var removedAny = false;
        foreach (var propertyName in root
                     .Where(prop => !prop.Key.StartsWith("_", StringComparison.OrdinalIgnoreCase) &&
                                    !allowedTopLevelKeys.Contains(prop.Key))
                     .Select(prop => prop.Key)
                     .ToArray())
        {
            root.Remove(propertyName);
            removedAny = true;
        }

        return removedAny;
    }

    private static HashSet<string> CreateCaseInsensitiveSet(IEnumerable<string>? values)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (values == null)
            return result;

        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                result.Add(value);
        }

        return result;
    }

    private static Dictionary<string, HashSet<string>> CreateCaseInsensitiveNestedSet(
        IReadOnlyDictionary<string, IEnumerable<string>>? values)
    {
        var result = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        if (values == null)
            return result;

        foreach (var pair in values)
        {
            if (string.IsNullOrWhiteSpace(pair.Key))
                continue;

            result[pair.Key] = CreateCaseInsensitiveSet(pair.Value);
        }

        return result;
    }

    private static bool PruneConflictingMetaStateUpdates(
        JsonObject root,
        SoulStatePatchConflictContext context)
    {
        if (root["metaStateUpdates"] == null)
            return false;

        if (root["metaStateUpdates"] is not JsonObject metaStateUpdates)
            throw new InvalidOperationException(InvalidMetaStateUpdatesMessage);

        if (TryDescribeInvalidMetaStateUpdates(metaStateUpdates, out var failureDescription))
            throw new InvalidOperationException(failureDescription);

        var removedAny = false;
        if ((context.TouchedDomains & SoulStatePatchTouchedDomains.InkFeathers) != 0)
            removedAny |= PruneConflictingInkFeatherChanges(metaStateUpdates, context);

        if ((context.TouchedDomains & SoulStatePatchTouchedDomains.SoulRelics) != 0)
            removedAny |= PruneConflictingSoulRelicOperations(metaStateUpdates, context);

        if ((context.TouchedDomains & SoulStatePatchTouchedDomains.LivesHistory) != 0)
            removedAny |= metaStateUpdates.Remove("lifeTransitions");

        if ((context.TouchedDomains & SoulStatePatchTouchedDomains.PendingMemoryLegacy) != 0)
            removedAny |= metaStateUpdates.Remove("memoryLegacyGrant");

        if ((context.TouchedDomains & (SoulStatePatchTouchedDomains.Enlightenment | SoulStatePatchTouchedDomains.SoulProgression)) != 0)
            removedAny |= metaStateUpdates.Remove("enlightenmentProgression");

        if (removedAny && metaStateUpdates.Count == 0)
            removedAny |= root.Remove("metaStateUpdates");

        return removedAny;
    }

    private static bool PruneConflictingInkFeatherChanges(
        JsonObject metaStateUpdates,
        SoulStatePatchConflictContext context)
    {
        if (metaStateUpdates["inkFeatherChanges"] == null)
            return false;

        if (metaStateUpdates["inkFeatherChanges"] is not JsonObject inkFeatherChanges ||
            !TryReadStrictInkFeatherChanges(inkFeatherChanges, out _, out _))
        {
            throw new InvalidOperationException(InvalidMetaStateInkFeatherChangesMessage);
        }

        return metaStateUpdates.Remove("inkFeatherChanges");
    }

    private static bool PruneConflictingSoulRelicOperations(
        JsonObject metaStateUpdates,
        SoulStatePatchConflictContext context)
    {
        if (metaStateUpdates["soulRelicOperations"] == null)
            return false;

        if (metaStateUpdates["soulRelicOperations"] is not JsonObject soulRelicOperations ||
            !HasStrictMetaSoulRelicOperationsShape(soulRelicOperations))
        {
            throw new InvalidOperationException(InvalidMetaStateSoulRelicOperationsMessage);
        }

        var hasSpecificConflictMetadata =
            context.UpsertedSoulRelicIds.Count > 0 ||
            context.UnsafeToReplayAddedSoulRelicIds.Count > 0 ||
            context.RemovedSoulRelicIds.Count > 0 ||
            context.EquipStateChangedSoulRelicIds.Count > 0 ||
            context.UpdatedSoulRelicFieldsById.Count > 0;
        if (!hasSpecificConflictMetadata)
            return metaStateUpdates.Remove("soulRelicOperations");

        var removedAny = false;
        removedAny |= RemoveRelicOperationIfMatching(
            soulRelicOperations,
            "addRelic",
            relicId => context.UpsertedSoulRelicIds.Contains(relicId) ||
                       context.UnsafeToReplayAddedSoulRelicIds.Contains(relicId));
        removedAny |= RemoveRelicOperationIfMatching(
            soulRelicOperations,
            "removeRelic",
            relicId => context.RemovedSoulRelicIds.Contains(relicId));
        removedAny |= RemoveRelicOperationIfMatching(
            soulRelicOperations,
            "equipRelic",
            relicId => context.EquipStateChangedSoulRelicIds.Contains(relicId));
        removedAny |= RemoveRelicOperationIfMatching(
            soulRelicOperations,
            "unequipRelic",
            relicId => context.EquipStateChangedSoulRelicIds.Contains(relicId));
        removedAny |= RemoveRelicFieldUpdateIfMatching(
            soulRelicOperations,
            context.UpdatedSoulRelicFieldsById);

        if (removedAny && soulRelicOperations.Count == 0)
            removedAny |= metaStateUpdates.Remove("soulRelicOperations");

        return removedAny;
    }

    private static bool RemoveRelicOperationIfMatching(
        JsonObject soulRelicOperations,
        string operationPropertyName,
        Func<string, bool> shouldRemove)
    {
        if (soulRelicOperations[operationPropertyName] is not JsonObject operation)
            return false;

        var relicId = GetStringValue(operation["relicId"]);
        return !string.IsNullOrWhiteSpace(relicId) &&
               shouldRemove(relicId) &&
               soulRelicOperations.Remove(operationPropertyName);
    }

    private static bool RemoveRelicFieldUpdateIfMatching(
        JsonObject soulRelicOperations,
        Dictionary<string, HashSet<string>> updatedSoulRelicFieldsById)
    {
        if (soulRelicOperations["updateRelicField"] is not JsonObject updateRelicField)
            return false;

        var relicId = GetStringValue(updateRelicField["relicId"]);
        var field = GetStringValue(updateRelicField["field"]);
        return !string.IsNullOrWhiteSpace(relicId) &&
               !string.IsNullOrWhiteSpace(field) &&
               updatedSoulRelicFieldsById.TryGetValue(relicId, out var updatedFields) &&
               updatedFields.Contains(field) &&
               soulRelicOperations.Remove("updateRelicField");
    }

    private static bool PruneConflictingArchiveTransientRoots(
        JsonObject root,
        SoulStatePatchConflictContext context)
    {
        if ((context.TouchedDomains & SoulStatePatchTouchedDomains.AfterlifeArchive) == 0)
            return false;

        var removedAny = false;
        removedAny |= PruneAfterlifeArchiveUpdates(root, context.AffectedArchiveIds);
        removedAny |= PruneArchiveActionResolutions(
            root,
            context.AffectedArchiveIds,
            context.AffectedArchiveRequestIds);
        return removedAny;
    }

    private static bool PruneAfterlifeArchiveUpdates(
        JsonObject root,
        HashSet<string> affectedArchiveIds)
    {
        if (!root.TryGetPropertyValue("afterlifeArchiveUpdates", out var updatesNode))
            return false;

        if (updatesNode is not JsonArray updates)
            throw new InvalidOperationException(InvalidAfterlifeArchiveUpdatesMessage);

        if (AfterlifeArchiveState.TryDescribeInvalidArchiveUpdates(updates, out var failureDescription))
            throw new InvalidOperationException(failureDescription);

        if (affectedArchiveIds.Count == 0)
            return root.Remove("afterlifeArchiveUpdates");

        var removedAny = RemoveMatchingArrayItems(
            updates,
            updateNode => ArchiveUpdateTargetsAffectedArchive(updateNode, affectedArchiveIds));

        if (removedAny && updates.Count == 0)
            removedAny |= root.Remove("afterlifeArchiveUpdates");

        return removedAny;
    }

    private static bool PruneArchiveActionResolutions(
        JsonObject root,
        HashSet<string> affectedArchiveIds,
        HashSet<string> affectedArchiveRequestIds)
    {
        if (!root.TryGetPropertyValue("archiveActionResolutions", out var resolutionsNode))
            return false;

        if (resolutionsNode is not JsonArray resolutions)
            throw new InvalidOperationException(InvalidArchiveActionResolutionsMessage);

        if (AfterlifeArchiveState.TryDescribeInvalidArchiveActionResolutions(resolutions, out var failureDescription))
            throw new InvalidOperationException(failureDescription);

        if (affectedArchiveIds.Count == 0 && affectedArchiveRequestIds.Count == 0)
            return root.Remove("archiveActionResolutions");

        var removedAny = RemoveMatchingArrayItems(
            resolutions,
            resolutionNode => ArchiveResolutionTargetsAffectedArchiveOrRequest(
                resolutionNode,
                affectedArchiveIds,
                affectedArchiveRequestIds));

        if (removedAny && resolutions.Count == 0)
            removedAny |= root.Remove("archiveActionResolutions");

        return removedAny;
    }

    private static bool RemoveMatchingArrayItems(
        JsonArray array,
        Func<JsonNode?, bool> shouldRemove)
    {
        var removedAny = false;
        for (var index = array.Count - 1; index >= 0; index--)
        {
            if (!shouldRemove(array[index]))
                continue;

            array.RemoveAt(index);
            removedAny = true;
        }

        return removedAny;
    }

    internal static bool TryReadStrictInkFeatherChanges(
        JsonObject feathers,
        out int add,
        out int spend)
    {
        add = 0;
        spend = 0;

        foreach (var property in feathers)
        {
            if (property.Key.StartsWith("_", StringComparison.OrdinalIgnoreCase))
                continue;

            if (!string.Equals(property.Key, "add", StringComparison.Ordinal) &&
                !string.Equals(property.Key, "spend", StringComparison.Ordinal))
            {
                return false;
            }

            if (!TryGetStrictInkFeatherBucket(property.Value, out var amount))
                return false;

            if (string.Equals(property.Key, "add", StringComparison.Ordinal))
                add = amount;
            else
                spend = amount;
        }

        return true;
    }

    internal static bool HasStrictMetaSoulRelicOperationsShape(JsonObject soulRelicOperations)
    {
        foreach (var property in soulRelicOperations)
        {
            if (property.Key.StartsWith("_", StringComparison.OrdinalIgnoreCase))
                continue;

            switch (property.Key)
            {
                case "addRelic":
                case "removeRelic":
                case "equipRelic":
                case "unequipRelic":
                    if (property.Value is not JsonObject operation ||
                        string.IsNullOrWhiteSpace(GetStringValue(operation["relicId"])))
                    {
                        return false;
                    }

                    break;
                case "updateRelicField":
                    if (property.Value is not JsonObject updateRelicField ||
                        string.IsNullOrWhiteSpace(GetStringValue(updateRelicField["relicId"])) ||
                        string.IsNullOrWhiteSpace(GetStringValue(updateRelicField["field"])))
                    {
                        return false;
                    }

                    break;
                default:
                    return false;
            }
        }

        return true;
    }

    internal static bool TryGetStrictInkFeatherBucket(JsonNode? node, out int amount)
    {
        amount = 0;
        if (node is not JsonValue value)
            return false;

        if (value.TryGetValue<int>(out var intValue) && intValue >= 0)
        {
            amount = intValue;
            return true;
        }

        if (value.TryGetValue<long>(out var longValue) &&
            longValue is >= 0 and <= int.MaxValue)
        {
            amount = (int)longValue;
            return true;
        }

        return false;
    }

    private static bool ArchiveUpdateTargetsAffectedArchive(
        JsonNode? updateNode,
        HashSet<string> affectedArchiveIds)
    {
        if (updateNode is not JsonObject update)
            return false;

        var archiveId = GetStringValue(update["archiveId"]);
        if (string.IsNullOrWhiteSpace(archiveId) &&
            update["entry"] is JsonObject entry)
        {
            archiveId = GetStringValue(entry["archiveId"]);
        }

        return !string.IsNullOrWhiteSpace(archiveId) &&
               affectedArchiveIds.Contains(archiveId);
    }

    private static bool ArchiveResolutionTargetsAffectedArchiveOrRequest(
        JsonNode? resolutionNode,
        HashSet<string> affectedArchiveIds,
        HashSet<string> affectedArchiveRequestIds)
    {
        if (resolutionNode is not JsonObject resolution)
            return false;

        var requestId = GetStringValue(resolution["requestId"]);
        if (!string.IsNullOrWhiteSpace(requestId) &&
            affectedArchiveRequestIds.Contains(requestId))
        {
            return true;
        }

        var archiveId = GetStringValue(resolution["archiveId"]);
        return !string.IsNullOrWhiteSpace(archiveId) &&
               affectedArchiveIds.Contains(archiveId);
    }

    internal static bool TryDescribeInvalidMetaStateUpdates(
        JsonObject updates,
        out string failureDescription)
    {
        foreach (var property in updates)
        {
            if (property.Key.StartsWith("_", StringComparison.OrdinalIgnoreCase))
                continue;

            switch (property.Key)
            {
                case "inkFeatherChanges":
                    if (property.Value is not JsonObject feathers ||
                        !TryReadStrictInkFeatherChanges(feathers, out _, out _))
                    {
                        failureDescription = InvalidMetaStateInkFeatherChangesMessage;
                        return true;
                    }

                    break;
                case "enlightenmentProgression":
                    if (property.Value is not JsonObject enlightenmentProgression ||
                        !TryReadStrictMetaEnlightenmentProgression(enlightenmentProgression, out _, out _))
                    {
                        failureDescription = InvalidMetaStateEnlightenmentProgressionMessage;
                        return true;
                    }

                    break;
                case "lifeTransitions":
                    if (property.Value is not JsonObject lifeTransitions)
                    {
                        failureDescription = DescribeInvalidMetaStateUpdatesObjectCommand(property.Key);
                        return true;
                    }

                    if (TryDescribeInvalidMetaLifeTransitions(lifeTransitions, out failureDescription))
                        return true;

                    break;
                case "memoryLegacyGrant":
                    if (property.Value is not JsonObject memoryLegacyGrant)
                    {
                        failureDescription = DescribeInvalidMetaStateUpdatesObjectCommand(property.Key);
                        return true;
                    }

                    if (TryDescribeInvalidMemoryLegacyGrant(memoryLegacyGrant, out failureDescription))
                        return true;

                    break;
                case "soulRelicOperations":
                    if (property.Value is not JsonObject soulRelicOperations ||
                        !HasStrictMetaSoulRelicOperationsShape(soulRelicOperations))
                    {
                        failureDescription = InvalidMetaStateSoulRelicOperationsMessage;
                        return true;
                    }

                    break;
                default:
                    failureDescription = DescribeInvalidMetaStateUpdatesUnknownVisibleKey(property.Key);
                    return true;
            }
        }

        failureDescription = string.Empty;
        return false;
    }

    internal static bool TryDescribeInvalidCanonicalInkFeathersRoot(
        JsonObject root,
        out string failureDescription)
    {
        if (!root.TryGetPropertyValue("inkFeathers", out var inkFeathersNode))
        {
            failureDescription = string.Empty;
            return false;
        }

        if (inkFeathersNode is not JsonObject inkFeathers ||
            HasUnsupportedVisibleKeys(inkFeathers, "current", "total") ||
            !TryGetNonNegativeIntegerValue(inkFeathers["current"], out var current))
        {
            failureDescription = InvalidCanonicalInkFeathersRootMessage;
            return true;
        }

        if (inkFeathers.TryGetPropertyValue("total", out var totalNode))
        {
            if (!TryGetNonNegativeIntegerValue(totalNode, out var total) || total < current)
            {
                failureDescription = InvalidCanonicalInkFeathersRootMessage;
                return true;
            }
        }

        failureDescription = string.Empty;
        return false;
    }

    internal static bool TryDescribeInvalidCanonicalSoulRelicsRoot(
        JsonObject root,
        out string failureDescription)
    {
        if (!root.TryGetPropertyValue("soulRelics", out var soulRelicsNode))
        {
            failureDescription = string.Empty;
            return false;
        }

        if (soulRelicsNode is not JsonObject soulRelics ||
            HasUnsupportedVisibleKeys(soulRelics, "equipped", "stored") ||
            !soulRelics.TryGetPropertyValue("equipped", out var equippedNode) ||
            equippedNode is not JsonArray equipped ||
            !soulRelics.TryGetPropertyValue("stored", out var storedNode) ||
            storedNode is not JsonArray stored ||
            !AreCanonicalSoulRelicArrays(equipped, stored))
        {
            failureDescription = InvalidCanonicalSoulRelicsRootMessage;
            return true;
        }

        failureDescription = string.Empty;
        return false;
    }

    internal static bool TryReadStrictCurrentSoulRelicCollections(
        JsonObject root,
        out JsonArray? equipped,
        out JsonArray? stored,
        out string failureDescription)
    {
        return TryReadStrictCurrentSoulRelicCollections(
            root,
            hasCanonicalTriggerLifeEnd: false,
            out equipped,
            out stored,
            out failureDescription);
    }

    internal static bool TryReadStrictCurrentSoulRelicCollections(
        JsonObject root,
        bool hasCanonicalTriggerLifeEnd,
        out JsonArray? equipped,
        out JsonArray? stored,
        out string failureDescription)
    {
        equipped = null;
        stored = null;

        if (TryDescribeInvalidPolicySensitiveReadableSoulStateRoot(
                root,
                hasCanonicalTriggerLifeEnd,
                out failureDescription))
        {
            return false;
        }

        if (root["soulRelics"] is not JsonObject soulRelics)
        {
            failureDescription = string.Empty;
            return true;
        }

        equipped = soulRelics["equipped"] as JsonArray;
        stored = soulRelics["stored"] as JsonArray;
        failureDescription = string.Empty;
        return true;
    }

    internal static bool TryReadStrictCurrentManifestationSoulRelicCollections(
        JsonObject root,
        out int currentIncarnation,
        out JsonArray? equipped,
        out JsonArray? stored,
        out string failureDescription)
    {
        return TryReadStrictCurrentManifestationSoulRelicCollections(
            root,
            hasCanonicalTriggerLifeEnd: false,
            out currentIncarnation,
            out equipped,
            out stored,
            out failureDescription);
    }

    internal static bool TryReadStrictCurrentManifestationSoulRelicCollections(
        JsonObject root,
        bool hasCanonicalTriggerLifeEnd,
        out int currentIncarnation,
        out JsonArray? equipped,
        out JsonArray? stored,
        out string failureDescription)
    {
        currentIncarnation = 0;
        if (!TryReadStrictCurrentSoulRelicCollections(
                root,
                hasCanonicalTriggerLifeEnd,
                out equipped,
                out stored,
                out failureDescription))
        {
            return false;
        }

        if (!TryGetPositiveIntegerValue(root["currentIncarnation"], out currentIncarnation))
        {
            equipped = null;
            stored = null;
            failureDescription = InvalidManifestationCurrentIncarnationMessage;
            return false;
        }

        failureDescription = string.Empty;
        return true;
    }

    internal static bool TryDescribeInvalidPolicySensitiveReadableSoulStateRoot(
        JsonObject root,
        out string failureDescription)
    {
        return TryDescribeInvalidPolicySensitiveReadableSoulStateRoot(
            root,
            hasCanonicalTriggerLifeEnd: false,
            out failureDescription);
    }

    internal static bool TryDescribeInvalidPolicySensitiveReadableSoulStateRoot(
        JsonObject root,
        bool hasCanonicalTriggerLifeEnd,
        out string failureDescription)
    {
        if (TryDescribeUnsupportedSoulStateTopLevelKeys(root, SoulStateLifecycleTopLevelKeys, out var unsupportedTopLevelDescription))
        {
            failureDescription = unsupportedTopLevelDescription ?? string.Empty;
            return true;
        }

        if (TryDescribeInvalidCanonicalSoulStateRoots(root, out failureDescription))
            return true;

        if (root.TryGetPropertyValue("metaStateUpdates", out var metaStateUpdatesNode))
        {
            if (metaStateUpdatesNode is not JsonObject metaStateUpdates)
            {
                failureDescription = InvalidMetaStateUpdatesMessage;
                return true;
            }

            if (TryDescribeInvalidMetaStateUpdates(metaStateUpdates, out failureDescription))
                return true;

            if (!hasCanonicalTriggerLifeEnd &&
                metaStateUpdates["lifeTransitions"] is JsonObject lifeTransitions &&
                lifeTransitions["recordLifeCompletion"] is JsonObject)
            {
                failureDescription = InvalidMetaStateLifeTransitionsTriggerContextMessage;
                return true;
            }
        }

        if (root.TryGetPropertyValue("afterlifeArchiveUpdates", out var archiveUpdatesNode))
        {
            if (archiveUpdatesNode is not JsonArray archiveUpdates)
            {
                failureDescription = InvalidAfterlifeArchiveUpdatesMessage;
                return true;
            }

            if (AfterlifeArchiveState.TryDescribeInvalidArchiveUpdates(archiveUpdates, out failureDescription))
                return true;
        }

        if (root.TryGetPropertyValue("archiveActionResolutions", out var archiveActionResolutionsNode))
        {
            if (archiveActionResolutionsNode is not JsonArray archiveActionResolutions)
            {
                failureDescription = InvalidArchiveActionResolutionsMessage;
                return true;
            }

            if (AfterlifeArchiveState.TryDescribeInvalidArchiveActionResolutions(
                    archiveActionResolutions,
                    out failureDescription))
            {
                return true;
            }
        }

        failureDescription = string.Empty;
        return false;
    }

    private static void ValidateArchiveTransientRootsForPatchWrite(JsonObject root)
    {
        if (root.TryGetPropertyValue("afterlifeArchiveUpdates", out var archiveUpdatesNode))
        {
            if (archiveUpdatesNode is not JsonArray archiveUpdates)
                throw new InvalidOperationException(InvalidAfterlifeArchiveUpdatesMessage);

            if (AfterlifeArchiveState.TryDescribeInvalidArchiveUpdates(archiveUpdates, out var failureDescription))
                throw new InvalidOperationException(failureDescription);
        }

        if (root.TryGetPropertyValue("archiveActionResolutions", out var archiveActionResolutionsNode))
        {
            if (archiveActionResolutionsNode is not JsonArray archiveActionResolutions)
                throw new InvalidOperationException(InvalidArchiveActionResolutionsMessage);

            if (AfterlifeArchiveState.TryDescribeInvalidArchiveActionResolutions(
                    archiveActionResolutions,
                    out var failureDescription))
            {
                throw new InvalidOperationException(failureDescription);
            }
        }
    }

    private static bool TryDescribeInvalidMetaLifeTransitions(
        JsonObject lifeTransitions,
        out string failureDescription)
    {
        if (!lifeTransitions.TryGetPropertyValue("recordLifeCompletion", out var recordLifeCompletionNode))
        {
            failureDescription = string.Empty;
            return false;
        }

        if (recordLifeCompletionNode is not JsonObject recordLifeCompletion ||
            recordLifeCompletion["characterFinalState"] is not JsonObject ||
            !IsArrayOfStrings(recordLifeCompletion["majorAchievements"]) ||
            !IsArrayOfObjects(recordLifeCompletion["relationshipsFormed"]) ||
            !IsArrayOfObjects(recordLifeCompletion["moralChoices"]) ||
            !IsArrayOfStrings(recordLifeCompletion["skillsLearned"]) ||
            !TryGetNonNegativeNumberValue(recordLifeCompletion["enlightenmentGained"], out _))
        {
            failureDescription = InvalidMetaStateLifeTransitionsMessage;
            return true;
        }

        failureDescription = string.Empty;
        return false;
    }

    private static bool AreCanonicalSoulRelicArrays(JsonArray equipped, JsonArray stored)
    {
        return equipped.All(IsCanonicalSoulRelicObject) &&
               stored.All(IsCanonicalSoulRelicObject);
    }

    private static bool IsCanonicalSoulRelicObject(JsonNode? node)
    {
        if (node is not JsonObject relic)
            return false;

        if (!TryGetRequiredNonEmptyStringValue(relic["relicId"], out _) ||
            !TryGetRequiredNonEmptyStringValue(relic["name"], out _))
        {
            return false;
        }

        var rarity = TryGetRequiredNonEmptyStringValue(relic["rarity"], out var explicitRarity)
            ? explicitRarity
            : TryGetRequiredNonEmptyStringValue(relic["quality"], out var quality)
                ? quality
                : null;
        if (string.IsNullOrWhiteSpace(rarity) ||
            !GuardianAbodeOfferingState.IsCanonicalSoulRelicRarity(rarity))
        {
            return false;
        }

        var relicType = GetStringValue(relic["relicType"]) ?? GetStringValue(relic["type"]);
        var hasCompanionSeed = relic.TryGetPropertyValue("companionSeed", out var companionSeedNode);
        if ((string.Equals(relicType, GuardianAbodeResidentState.RelicTypeCompanionEcho, StringComparison.OrdinalIgnoreCase) ||
             hasCompanionSeed) &&
            !HasCanonicalCompanionSeedPayload(companionSeedNode))
        {
            return false;
        }

        if (relic.TryGetPropertyValue("soulImprint", out var soulImprintNode) &&
            !HasCanonicalEmbeddedImprintPayload(soulImprintNode))
        {
            return false;
        }

        if (relic.TryGetPropertyValue("npcSoulImprint", out var npcSoulImprintNode) &&
            !HasCanonicalEmbeddedImprintPayload(npcSoulImprintNode))
        {
            return false;
        }

        return true;
    }

    private static bool HasCanonicalCompanionSeedPayload(JsonNode? node)
    {
        if (node is not JsonObject companionSeed)
            return false;

        if (!TryGetRequiredNonEmptyStringValue(companionSeed["sourceResidentId"], out _) ||
            !TryGetRequiredNonEmptyStringValue(companionSeed["sourceGuardianId"], out _) ||
            !TryGetRequiredNonEmptyStringValue(companionSeed["companionNameHint"], out _) ||
            !TryGetRequiredNonEmptyStringValue(companionSeed["originWorldSummary"], out _) ||
            !TryGetRequiredNonEmptyStringValue(companionSeed["futureCompanionPrompt"], out _))
        {
            return false;
        }

        if (companionSeed.TryGetPropertyValue("bondReason", out var bondReasonNode) &&
            !IsNullableStringNode(bondReasonNode))
        {
            return false;
        }

        if (companionSeed.TryGetPropertyValue("coreTraits", out var coreTraitsNode) &&
            !IsArrayOfStrings(coreTraitsNode))
        {
            return false;
        }

        if (companionSeed.TryGetPropertyValue("archetypeHints", out var archetypeHintsNode) &&
            !IsArrayOfStrings(archetypeHintsNode))
        {
            return false;
        }

        if (companionSeed.TryGetPropertyValue("appearanceMotifs", out var appearanceMotifsNode) &&
            !IsArrayOfStrings(appearanceMotifsNode))
        {
            return false;
        }

        if (companionSeed.TryGetPropertyValue("personalityProfile", out var personalityProfileNode) &&
            !HasCanonicalResidentPersonalityProfilePayload(personalityProfileNode))
        {
            return false;
        }

        if (companionSeed.TryGetPropertyValue("abodeDisposition", out var abodeDispositionNode) &&
            !HasCanonicalResidentAbodeDispositionPayload(abodeDispositionNode))
        {
            return false;
        }

        if (HasAnyResidentAbodeRelationField(companionSeed) &&
            !HasCanonicalResidentAbodeRelationPayload(companionSeed))
        {
            return false;
        }

        return true;
    }

    private static bool HasCanonicalResidentPersonalityProfilePayload(JsonNode? node)
    {
        if (node is not JsonObject personalityProfile)
            return false;

        if (!TryGetRequiredNonEmptyStringValue(personalityProfile["archetype"], out _) ||
            !TryGetRequiredNonEmptyStringValue(personalityProfile["worldview"], out _) ||
            !TryGetRequiredNonEmptyStringValue(personalityProfile["culturalLayer"], out _) ||
            !HasNonEmptyArrayOfStrings(personalityProfile["coreValues"]))
        {
            return false;
        }

        if (personalityProfile["personalityTraits"] is not JsonArray personalityTraits ||
            personalityTraits.Count == 0)
        {
            return false;
        }

        return personalityTraits.All(HasCanonicalResidentPersonalityTraitPayload);
    }

    private static bool HasCanonicalResidentPersonalityTraitPayload(JsonNode? node)
    {
        if (node is not JsonObject trait)
            return false;

        return TryGetRequiredNonEmptyStringValue(trait["traitName"], out _) &&
               TryGetRequiredNonEmptyStringValue(trait["valueDescription"], out _) &&
               IsNullableStringNode(trait["description"]) &&
               TryGetNonNegativeIntegerValue(trait["value"], out var value) &&
               value is >= 1 and <= 10;
    }

    private static bool HasCanonicalResidentAbodeDispositionPayload(JsonNode? node)
    {
        if (node is not JsonObject abodeDisposition)
            return false;

        return TryGetRequiredNonEmptyStringValue(abodeDisposition["powerSensitivity"], out var powerSensitivity) &&
               GuardianAbodeResidentState.IsSupportedPowerSensitivity(powerSensitivity) &&
               TryGetRequiredNonEmptyStringValue(abodeDisposition["migrationDisposition"], out var migrationDisposition) &&
               GuardianAbodeResidentState.IsSupportedMigrationDisposition(migrationDisposition) &&
               TryGetRequiredNonEmptyStringValue(abodeDisposition["communalOrientation"], out var communalOrientation) &&
               GuardianAbodeResidentState.IsSupportedCommunalOrientation(communalOrientation) &&
               TryGetRequiredNonEmptyStringValue(abodeDisposition["stabilityNeed"], out var stabilityNeed) &&
               GuardianAbodeResidentState.IsSupportedStabilityNeed(stabilityNeed);
    }

    private static bool HasAnyResidentAbodeRelationField(JsonObject node)
    {
        return node.ContainsKey("abodeDevotionLevel") ||
               node.ContainsKey("abodeDevotionTier") ||
               node.ContainsKey("restlessness") ||
               node.ContainsKey("migrationState");
    }

    private static bool HasCanonicalResidentAbodeRelationPayload(JsonObject node)
    {
        if (!TryGetNonNegativeIntegerValue(node["abodeDevotionLevel"], out var abodeDevotionLevel) ||
            abodeDevotionLevel > 100 ||
            !TryGetNonNegativeIntegerValue(node["restlessness"], out var restlessness) ||
            restlessness > 100 ||
            !TryGetRequiredNonEmptyStringValue(node["abodeDevotionTier"], out var abodeDevotionTier) ||
            !GuardianAbodeResidentState.IsSupportedAbodeDevotionTier(abodeDevotionTier) ||
            !TryGetRequiredNonEmptyStringValue(node["migrationState"], out var migrationState) ||
            !GuardianAbodeResidentState.IsSupportedMigrationState(migrationState))
        {
            return false;
        }

        return string.Equals(
                   GuardianAbodeResidentState.ResolveAbodeDevotionTier(abodeDevotionLevel),
                   abodeDevotionTier,
                   StringComparison.OrdinalIgnoreCase) &&
               string.Equals(
                   GuardianAbodeResidentState.ResolveMigrationState(abodeDevotionLevel, restlessness),
                   migrationState,
                   StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasCanonicalEmbeddedImprintPayload(JsonNode? node)
    {
        if (node is not JsonObject imprint)
            return false;

        var hasIdentity =
            TryGetRequiredNonEmptyStringValue(imprint["NPCName"], out _) ||
            TryGetRequiredNonEmptyStringValue(imprint["npcName"], out _) ||
            TryGetRequiredNonEmptyStringValue(imprint["name"], out _) ||
            TryGetRequiredNonEmptyStringValue(imprint["companionName"], out _) ||
            TryGetRequiredNonEmptyStringValue(imprint["originalName"], out _) ||
            TryGetRequiredNonEmptyStringValue(imprint["imprintId"], out _) ||
            TryGetRequiredNonEmptyStringValue(imprint["id"], out _);
        if (!hasIdentity)
            return false;

        var hasSummary =
            TryGetRequiredNonEmptyStringValue(imprint["description"], out _) ||
            TryGetRequiredNonEmptyStringValue(imprint["summary"], out _) ||
            TryGetRequiredNonEmptyStringValue(imprint["backgroundStory"], out _) ||
            TryGetRequiredNonEmptyStringValue(imprint["history"], out _);
        if (!hasSummary)
            return false;

        return HasNonEmptyArrayOfStrings(imprint["coreTraitsPreserved"]) ||
               HasNonEmptyArrayOfStrings(imprint["coreTraits"]) ||
               HasNonEmptyArrayOfStrings(imprint["personalityTraits"]);
    }

    internal static bool TryReadStrictMetaEnlightenmentProgression(
        JsonObject enlightenmentProgression,
        out int? newTier,
        out int experience)
    {
        newTier = null;
        experience = 0;

        foreach (var property in enlightenmentProgression)
        {
            if (property.Key.StartsWith("_", StringComparison.OrdinalIgnoreCase))
                continue;

            if (!string.Equals(property.Key, "newTier", StringComparison.Ordinal) &&
                !string.Equals(property.Key, "experience", StringComparison.Ordinal))
            {
                return false;
            }
        }

        if (!TryGetNonNegativeIntegerValue(enlightenmentProgression["experience"], out experience))
            return false;

        if (enlightenmentProgression.TryGetPropertyValue("newTier", out var newTierNode))
        {
            if (!TryGetNonNegativeIntegerValue(newTierNode, out var parsedNewTier))
                return false;

            newTier = parsedNewTier;
        }

        return true;
    }

    private static bool TryDescribeInvalidMemoryLegacyGrant(
        JsonObject memoryLegacyGrant,
        out string failureDescription)
    {
        if (!TryGetRequiredNonEmptyStringValue(memoryLegacyGrant["legacyId"], out _) ||
            !TryGetRequiredNonEmptyStringValue(memoryLegacyGrant["legacyType"], out var legacyType) ||
            !TryGetRequiredNonEmptyStringValue(memoryLegacyGrant["sourceLifeHint"], out _))
        {
            failureDescription = InvalidMetaStateMemoryLegacyGrantMessage;
            return true;
        }

        if (string.Equals(legacyType, "startingCharacteristicBonus", StringComparison.OrdinalIgnoreCase))
        {
            if (!TryGetRequiredNonEmptyStringValue(memoryLegacyGrant["characteristic"], out var characteristic) ||
                !Characteristics.All.Contains(characteristic, StringComparer.OrdinalIgnoreCase) ||
                !TryGetPositiveIntegerValue(memoryLegacyGrant["bonus"], out var bonus) ||
                bonus != 2)
            {
                failureDescription = InvalidMetaStateMemoryLegacyGrantMessage;
                return true;
            }

            failureDescription = string.Empty;
            return false;
        }

        if (string.Equals(legacyType, "startingPassiveKnowledgeSkill", StringComparison.OrdinalIgnoreCase))
        {
            if (!TryGetRequiredNonEmptyStringValue(memoryLegacyGrant["skillName"], out _) ||
                !TryGetRequiredNonEmptyStringValue(memoryLegacyGrant["skillDescription"], out _) ||
                !TryGetRequiredNonEmptyStringValue(memoryLegacyGrant["group"], out var group) ||
                !string.Equals(group, "Knowledge", StringComparison.OrdinalIgnoreCase) ||
                !TryGetRequiredNonEmptyStringValue(memoryLegacyGrant["playerStatBonus"], out _) ||
                !IsNullableStringNode(memoryLegacyGrant["rarity"]) ||
                !IsNullableStringNode(memoryLegacyGrant["type"]) ||
                !IsOptionalPositiveIntegerNode(memoryLegacyGrant["masteryLevel"]) ||
                !IsOptionalPositiveIntegerNode(memoryLegacyGrant["maxMasteryLevel"]) ||
                !IsNonEmptyArrayOfObjects(memoryLegacyGrant["structuredBonuses"]))
            {
                failureDescription = InvalidMetaStateMemoryLegacyGrantMessage;
                return true;
            }

            failureDescription = string.Empty;
            return false;
        }

        failureDescription = InvalidMetaStateMemoryLegacyGrantMessage;
        return true;
    }

    private static string DescribeInvalidMetaStateUpdatesObjectCommand(string propertyName)
    {
        return $"game_state/meta/soul_state.json current metaStateUpdates.{propertyName} must be a JsonObject if present.";
    }

    private static string DescribeInvalidMetaStateUpdatesUnknownVisibleKey(string propertyName)
    {
        return $"game_state/meta/soul_state.json current metaStateUpdates contains unsupported visible key '{propertyName}'. Supported visible keys: {string.Join(", ", MetaStateVisibleTopLevelCommandKeys.OrderBy(key => key, StringComparer.OrdinalIgnoreCase))}.";
    }

    private static bool HasUnsupportedVisibleKeys(JsonObject obj, params string[] allowedVisibleKeys)
    {
        var allowed = allowedVisibleKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var property in obj)
        {
            if (property.Key.StartsWith("_", StringComparison.OrdinalIgnoreCase))
                continue;

            if (!allowed.Contains(property.Key))
                return true;
        }

        return false;
    }

    private static bool TryGetRequiredNonEmptyStringValue(JsonNode? node, out string value)
    {
        value = string.Empty;
        if (node is not JsonValue jsonValue ||
            !jsonValue.TryGetValue<string>(out var text) ||
            string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        value = text;
        return true;
    }

    private static bool IsNullableStringNode(JsonNode? node)
    {
        if (node == null)
            return true;

        return node is JsonValue value && value.TryGetValue<string>(out _);
    }

    private static bool IsOptionalPositiveIntegerNode(JsonNode? node)
    {
        if (node == null)
            return true;

        return TryGetPositiveIntegerValue(node, out _);
    }

    private static bool TryGetPositiveIntegerValue(JsonNode? node, out int value)
    {
        value = 0;
        return TryGetNonNegativeIntegerValue(node, out value) && value > 0;
    }

    private static bool TryGetNonNegativeIntegerValue(JsonNode? node, out int value)
    {
        value = 0;
        if (node is not JsonValue jsonValue)
            return false;

        if (jsonValue.TryGetValue<int>(out var intValue) && intValue >= 0)
        {
            value = intValue;
            return true;
        }

        if (jsonValue.TryGetValue<long>(out var longValue) &&
            longValue is >= 0 and <= int.MaxValue)
        {
            value = (int)longValue;
            return true;
        }

        return false;
    }

    private static bool TryGetNonNegativeNumberValue(JsonNode? node, out double value)
    {
        value = 0;
        if (node is not JsonValue jsonValue ||
            !jsonValue.TryGetValue<double>(out var number) ||
            number < 0)
        {
            return false;
        }

        value = number;
        return true;
    }

    private static bool IsArrayOfStrings(JsonNode? node)
    {
        return node is JsonArray array &&
               array.All(item => item is JsonValue value && value.TryGetValue<string>(out _));
    }

    private static bool HasNonEmptyArrayOfStrings(JsonNode? node)
    {
        return node is JsonArray array &&
               array.Count > 0 &&
               array.All(item => item is JsonValue value && value.TryGetValue<string>(out _));
    }

    private static bool IsArrayOfObjects(JsonNode? node)
    {
        return node is JsonArray array &&
               array.All(item => item is JsonObject);
    }

    private static bool IsNonEmptyArrayOfObjects(JsonNode? node)
    {
        return node is JsonArray array &&
               array.Count > 0 &&
               array.All(item => item is JsonObject);
    }

    private static bool TextMayContainManifestedCompanionNpcDependencySurface(string containerText)
    {
        if (string.IsNullOrWhiteSpace(containerText))
            return false;

        var containerStack = new Stack<char>();
        var expectsObjectPropertyName = false;

        for (var index = 0; index < containerText.Length;)
        {
            var current = containerText[index];
            if (current == '"')
            {
                if (!TryReadJsonStringLiteral(containerText, index, out var token, out var nextIndex))
                    return false;

                var separatorIndex = SkipWhitespace(containerText, nextIndex);
                if (expectsObjectPropertyName &&
                    containerStack.TryPeek(out var containerType) &&
                    containerType == '{' &&
                    separatorIndex < containerText.Length &&
                    containerText[separatorIndex] == ':' &&
                    ManifestedCompanionNpcSourceFieldNames.Contains(token, StringComparer.OrdinalIgnoreCase))
                {
                    return true;
                }

                index = nextIndex;
                continue;
            }

            switch (current)
            {
                case '{':
                    containerStack.Push(current);
                    expectsObjectPropertyName = true;
                    break;
                case '[':
                    containerStack.Push(current);
                    expectsObjectPropertyName = false;
                    break;
                case '}':
                case ']':
                    if (containerStack.Count > 0)
                        containerStack.Pop();

                    expectsObjectPropertyName =
                        containerStack.TryPeek(out var parentAfterClose) &&
                        parentAfterClose == '{';
                    break;
                case ':':
                    expectsObjectPropertyName = false;
                    break;
                case ',':
                    expectsObjectPropertyName =
                        containerStack.TryPeek(out var parentAfterComma) &&
                        parentAfterComma == '{';
                    break;
            }

            index++;
        }

        return false;
    }

    private static bool TryExtractTopLevelJsonContainer(
        string json,
        string propertyName,
        out string containerText)
    {
        containerText = string.Empty;
        if (string.IsNullOrWhiteSpace(json))
            return false;

        var objectDepth = 0;
        var arrayDepth = 0;

        for (var index = 0; index < json.Length;)
        {
            var current = json[index];
            if (current == '"')
            {
                if (!TryReadJsonStringLiteral(json, index, out var token, out var nextIndex))
                    return false;

                if (objectDepth == 1 &&
                    arrayDepth == 0 &&
                    string.Equals(token, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    var separatorIndex = SkipWhitespace(json, nextIndex);
                    if (separatorIndex >= json.Length || json[separatorIndex] != ':')
                        return false;

                    var valueStartIndex = SkipWhitespace(json, separatorIndex + 1);
                    if (valueStartIndex >= json.Length)
                        return false;

                    var valueStart = json[valueStartIndex];
                    if (valueStart is not ('[' or '{'))
                        return false;

                    return TryExtractBalancedJsonContainer(json, valueStartIndex, out containerText, out _);
                }

                index = nextIndex;
                continue;
            }

            if (current == '{')
                objectDepth++;
            else if (current == '}')
                objectDepth = Math.Max(0, objectDepth - 1);
            else if (current == '[')
                arrayDepth++;
            else if (current == ']')
                arrayDepth = Math.Max(0, arrayDepth - 1);

            index++;
        }

        return false;
    }

    private static bool TryExtractBalancedJsonContainer(
        string json,
        int startIndex,
        out string containerText,
        out int nextIndex)
    {
        containerText = string.Empty;
        nextIndex = startIndex;
        if (startIndex < 0 || startIndex >= json.Length)
            return false;

        var open = json[startIndex];
        if (open is not ('[' or '{'))
            return false;

        var close = open == '[' ? ']' : '}';
        var depth = 0;
        var inString = false;
        var escaped = false;

        for (var index = startIndex; index < json.Length; index++)
        {
            var current = json[index];
            if (inString)
            {
                if (escaped)
                {
                    escaped = false;
                    continue;
                }

                if (current == '\\')
                {
                    escaped = true;
                    continue;
                }

                if (current == '"')
                    inString = false;

                continue;
            }

            if (current == '"')
            {
                inString = true;
                continue;
            }

            if (current == open)
                depth++;
            else if (current == close)
            {
                depth--;
                if (depth == 0)
                {
                    containerText = json.Substring(startIndex, index - startIndex + 1);
                    nextIndex = index + 1;
                    return true;
                }
            }
        }

        return false;
    }

    private static bool TryReadJsonStringLiteral(
        string json,
        int quoteIndex,
        out string token,
        out int nextIndex)
    {
        token = string.Empty;
        nextIndex = quoteIndex;
        if (quoteIndex < 0 || quoteIndex >= json.Length || json[quoteIndex] != '"')
            return false;

        var builder = new StringBuilder();
        for (var index = quoteIndex + 1; index < json.Length; index++)
        {
            var current = json[index];
            if (current == '"')
            {
                token = builder.ToString();
                nextIndex = index + 1;
                return true;
            }

            if (current != '\\')
            {
                builder.Append(current);
                continue;
            }

            if (index + 1 >= json.Length)
                return false;

            var escaped = json[++index];
            switch (escaped)
            {
                case '"':
                case '\\':
                case '/':
                    builder.Append(escaped);
                    break;
                case 'b':
                    builder.Append('\b');
                    break;
                case 'f':
                    builder.Append('\f');
                    break;
                case 'n':
                    builder.Append('\n');
                    break;
                case 'r':
                    builder.Append('\r');
                    break;
                case 't':
                    builder.Append('\t');
                    break;
                case 'u':
                    if (index + 4 >= json.Length)
                        return false;

                    if (!ushort.TryParse(
                            json.Substring(index + 1, 4),
                            System.Globalization.NumberStyles.AllowHexSpecifier,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out var codePoint))
                    {
                        return false;
                    }

                    builder.Append((char)codePoint);
                    index += 4;
                    break;
                default:
                    return false;
            }
        }

        return false;
    }

    private static int SkipWhitespace(string text, int index)
    {
        while (index < text.Length && char.IsWhiteSpace(text[index]))
            index++;

        return index;
    }

    private static string? GetNpcId(JsonObject npc)
    {
        return GetStringValue(npc["NPCId"]) ??
               GetStringValue(npc["npcId"]) ??
               GetStringValue(npc["id"]);
    }

    private static string? GetStringValue(JsonNode? node)
    {
        if (node is JsonValue value)
        {
            if (value.TryGetValue<string>(out var text))
                return text;
            if (value.TryGetValue<int>(out var number))
                return number.ToString();
        }

        return null;
    }

}
