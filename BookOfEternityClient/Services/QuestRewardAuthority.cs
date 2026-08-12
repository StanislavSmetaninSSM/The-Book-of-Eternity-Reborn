using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal enum QuestRewardKind
{
    Item,
    Skill,
    Relationship
}

internal static class QuestRewardAuthority
{
    public const string MissingItemAuthorityCode = "quest_reward_item_missing_detail_authority";
    public const string MissingSkillAuthorityCode = "quest_reward_skill_missing_detail_authority";
    public const string MissingRelationshipAuthorityCode = "quest_reward_relationship_missing_detail_authority";
    public const string MissingHistoryReasonCode = "quest_reward_history_reason_missing";
    public const string MortalItemTransitionAuthorityMismatchCode =
        "mortal_item_materialization_quest_reward_authority_mismatch";

    private const string QuestHistoryPath = "game_state/quests/quest_history.json";

    private static readonly string[] ItemCollectionNames = ["items", "UpdateInventory"];
    private static readonly string[] SkillCollectionNames = ["activeSkillChanges", "passiveSkillChanges", "skills", "skillHistory"];
    private static readonly string[] RelationshipCollectionNames = ["NPCRelationshipChanges", "relationshipChanges", "relationships", "entries"];
    private static readonly string[] NpcCollectionNames = ["UpdateNPCs", "NPCsInScene", "NPCs", "npcs", "npcDataChanges"];

    private static readonly string[] ItemIdentityFields = ["itemId", "existedId", "inventoryItemId", "authorityId", "detailId", "id"];
    private static readonly string[] ItemNameFields = ["displayName", "itemName", "name", "label"];
    private static readonly string[] ItemLinkNameFields = ["itemName", "name"];

    private static readonly string[] SkillIdentityFields = ["skillId", "authorityId", "detailId", "id"];
    private static readonly string[] SkillNameFields = ["displayName", "skillName", "name", "label"];
    private static readonly string[] SkillLinkNameFields = ["skillName", "name"];

    private static readonly string[] RelationshipIdentityFields =
    [
        "npcId", "NPCId", "actorId", "targetActorId", "relationshipId", "authorityId", "detailId", "id"
    ];
    private static readonly string[] RelationshipNameFields = ["displayName", "npcName", "NPCName", "actorName", "name", "label"];
    private static readonly string[] RelationshipLinkNameFields = ["npcName", "NPCName", "actorName", "name"];

    private static readonly string[] StatusFields =
    [
        "authorityStatus", "availability", "availabilityStatus", "authorityState", "state", "status"
    ];
    private static readonly string[] ReasonFields =
    [
        "reason", "historicalReason", "unavailableReason", "unresolvedReason", "authorityReason", "historyReason"
    ];

    private static readonly HashSet<string> ExplicitUnavailableStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "historicalonly",
        "historical",
        "priorincarnation",
        "unavailable",
        "unavailableincurrentstate",
        "unavailableincurrentincarnation",
        "nolongeravailable",
        "consumed",
        "sold",
        "forgotten",
        "lost",
        "destroyed",
        "legacyonly",
        "archived"
    };

    public static IReadOnlyList<QuestRewardAuthorityIssue> ValidateQuestRewards(
        JsonNode? questHistoryRoot,
        QuestRewardAuthorityContext context)
    {
        var issues = new List<QuestRewardAuthorityIssue>();
        if (questHistoryRoot is not JsonObject root ||
            !TryGetProperty(root, "questRewards", out var rewardsNode) ||
            rewardsNode is not JsonArray rewards)
        {
            return issues;
        }

        var rewardIndex = 0;
        foreach (var rewardNode in rewards)
        {
            var rewardContext = $"{QuestHistoryPath}.questRewards[{rewardIndex++}]";
            if (rewardNode is not JsonObject reward)
                continue;

            ValidateRewardCollection(reward, "itemsReceived", QuestRewardKind.Item, context, $"{rewardContext}.itemsReceived", issues);
            ValidateRewardCollection(reward, "skillsUnlocked", QuestRewardKind.Skill, context, $"{rewardContext}.skillsUnlocked", issues);
            ValidateRewardCollection(reward, "relationshipChanges", QuestRewardKind.Relationship, context, $"{rewardContext}.relationshipChanges", issues);
        }

        return issues;
    }

    internal static IReadOnlyList<QuestRewardAuthorityIssue>
        ValidateMortalItemTransitionAuthorities(
            JsonNode? questHistoryRoot,
            MortalItemIdentityParseResult identityIndex)
    {
        ArgumentNullException.ThrowIfNull(identityIndex);

        var issues = new List<QuestRewardAuthorityIssue>();
        if (questHistoryRoot is not JsonObject root ||
            root["questRewards"] is not JsonArray rewards)
        {
            return issues;
        }

        for (var rewardIndex = 0; rewardIndex < rewards.Count; rewardIndex++)
        {
            if (rewards[rewardIndex] is not JsonObject reward)
                continue;

            var rewardAuthorityId = ReadExactRewardIdentity(reward["rewardId"]);
            if (reward["itemsReceived"] is not JsonArray itemRewards)
            {
                continue;
            }

            for (var itemIndex = 0; itemIndex < itemRewards.Count; itemIndex++)
            {
                var itemReward = itemRewards[itemIndex] as JsonObject;
                if (itemReward != null &&
                    ReadExplicitUnavailableStatus(itemReward) != null)
                {
                    continue;
                }

                var rewardPath =
                    $"{QuestHistoryPath}.questRewards[{rewardIndex}].itemsReceived[{itemIndex}]";
                var itemId = ReadExactRewardIdentity(itemReward?["itemId"]);
                JsonObject? entry = null;
                if (itemId != null)
                    identityIndex.EntriesByItemId.TryGetValue(itemId, out entry);
                var firstTransition = entry?["transitions"] is JsonArray { Count: > 0 } transitions
                    ? transitions[0] as JsonObject
                    : null;
                if (rewardAuthorityId == null ||
                    itemReward == null ||
                    itemId == null ||
                    entry == null ||
                    firstTransition == null ||
                    !string.Equals(
                        ReadExactRewardIdentity(firstTransition["kind"]),
                        "create",
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        ReadExactRewardIdentity(firstTransition["authorityKind"]),
                        "quest_reward",
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        ReadExactRewardIdentity(firstTransition["authorityId"]),
                        rewardAuthorityId,
                        StringComparison.Ordinal))
                {
                    var unresolvedReference = itemId ??
                                              ReadExactRewardIdentity(itemReward?["creationRef"]) ??
                                              ReadExactRewardIdentity(itemRewards[itemIndex]);
                    issues.Add(new QuestRewardAuthorityIssue(
                        rewardPath,
                        MortalItemTransitionAuthorityMismatchCode,
                        "Quest item reward must resolve to its exact accepted Mortal item creation transition.",
                        rewardAuthorityId == null
                            ? "exact rewardId and itemId with matching create/quest_reward transition"
                            : $"itemId with create/quest_reward authority {rewardAuthorityId}",
                        DescribeMortalItemTransitionActual(
                            rewardAuthorityId,
                            itemId,
                            entry),
                        "Исправь exact rewardId/itemId в quest history по принятому ходу; не переигрывай награду и не создавай receipt/index вручную.",
                        itemId != null
                            ? $"mortal_item:existing:{itemId}"
                            : unresolvedReference != null
                                ? $"mortal_item:unresolved:{unresolvedReference}"
                                : "mortal_item:quest_reward:unknown"));
                }
            }
        }

        return issues;
    }

    private static string DescribeMortalItemTransitionActual(
        string? rewardAuthorityId,
        string? itemId,
        JsonObject? entry)
    {
        if (entry?["transitions"] is not JsonArray transitions ||
            transitions.Count == 0 ||
            transitions[0] is not JsonObject transition)
        {
            var itemEvidence = itemId == null
                ? "missing exact itemId"
                : $"{itemId}: missing index transition";
            return rewardAuthorityId == null
                ? $"missing exact rewardId; {itemEvidence}"
                : itemEvidence;
        }

        return $"rewardId={rewardAuthorityId ?? "missing"}; " +
               $"{itemId ?? "missing itemId"}: first=" +
               $"{ReadExactRewardIdentity(transition["kind"]) ?? "missing kind"}/" +
               $"{ReadExactRewardIdentity(transition["authorityKind"]) ?? "missing authorityKind"}/" +
               $"{ReadExactRewardIdentity(transition["authorityId"]) ?? "missing authorityId"}";
    }

    private static string? ReadExactRewardIdentity(JsonNode? node)
    {
        if (node is not JsonValue value ||
            !value.TryGetValue<string>(out var text) ||
            string.IsNullOrWhiteSpace(text) ||
            !string.Equals(text, text.Trim(), StringComparison.Ordinal))
        {
            return null;
        }

        return text;
    }

    public static string DescribePlayerReward(
        QuestRewardKind kind,
        JsonNode? rewardNode,
        QuestRewardAuthorityContext context)
    {
        if (TryReadScalarString(rewardNode, out var scalar) && !string.IsNullOrWhiteSpace(scalar))
        {
            if (kind == QuestRewardKind.Item && context.RequiresExactItemId)
                return $"{FallbackRewardLabel(kind)} — подробности пока не записаны";

            return context.TryResolve(kind, scalar, out var scalarResolvedLabel)
                ? FormatResolvedLabel(kind, scalar, scalarResolvedLabel)
                : $"{FallbackRewardLabel(kind)} — подробности пока не записаны";
        }

        if (rewardNode is not JsonObject reward)
            return $"{FallbackRewardLabel(kind)} — подробности пока не записаны";

        var label = ReadDisplayLabel(kind, reward);
        var status = ReadExplicitUnavailableStatus(reward);
        var reason = FirstNonEmpty(ReadReasonStrings(reward));
        var resolved = TryResolveStructuredReward(kind, reward, context, out var resolvedLabel);
        if (resolved && kind == QuestRewardKind.Item && context.RequiresExactItemId)
            label = resolvedLabel;
        else if (string.IsNullOrWhiteSpace(label))
            label = resolved ? resolvedLabel : FallbackRewardLabel(kind);

        if (kind == QuestRewardKind.Relationship)
            label = AppendRelationshipDelta(label, reward);

        if (!string.IsNullOrWhiteSpace(status))
        {
            var statusLabel = FormatUnavailableStatus(status);
            return !string.IsNullOrWhiteSpace(reason)
                ? $"{label} — {statusLabel}: {reason}"
                : $"{label} — {statusLabel}";
        }

        if (resolved)
            return label;

        if (kind == QuestRewardKind.Item)
            label = FallbackRewardLabel(kind);

        return !string.IsNullOrWhiteSpace(reason)
            ? $"{label} — недоступно: {reason}"
            : $"{label} — подробности пока не записаны";
    }

    private static void ValidateRewardCollection(
        JsonObject reward,
        string propertyName,
        QuestRewardKind kind,
        QuestRewardAuthorityContext context,
        string collectionPath,
        List<QuestRewardAuthorityIssue> issues)
    {
        if (!TryGetProperty(reward, propertyName, out var collection) || collection is not JsonArray rewards)
            return;

        var index = 0;
        foreach (var rewardNode in rewards)
        {
            var rewardPath = $"{collectionPath}[{index++}]";
            ValidateRewardReference(rewardNode, kind, context, rewardPath, issues);
        }
    }

    private static void ValidateRewardReference(
        JsonNode? rewardNode,
        QuestRewardKind kind,
        QuestRewardAuthorityContext context,
        string rewardPath,
        List<QuestRewardAuthorityIssue> issues)
    {
        if (TryReadScalarString(rewardNode, out var scalar) && !string.IsNullOrWhiteSpace(scalar))
        {
            if (!context.TryResolve(kind, scalar, out _))
                issues.Add(MissingAuthorityIssue(kind, rewardPath, scalar));
            return;
        }

        if (rewardNode is not JsonObject reward)
            return;

        var status = ReadExplicitUnavailableStatus(reward);
        if (!string.IsNullOrWhiteSpace(status))
        {
            var reason = FirstNonEmpty(ReadReasonStrings(reward));
            if (string.IsNullOrWhiteSpace(reason))
            {
                issues.Add(new QuestRewardAuthorityIssue(
                    rewardPath,
                    MissingHistoryReasonCode,
                    "Историческая или недоступная награда квеста должна иметь player-facing reason.",
                    "authorityStatus/availability HistoricalOnly or Unavailable with reason",
                    DescribeRewardForActual(kind, reward),
                    "Если награда осталась в прошлой инкарнации, была потрачена, продана, забыта или недоступна, добавь reason/historicalReason/unavailableReason для игрока."));
            }

            return;
        }

        if (!TryResolveStructuredReward(kind, reward, context, out _))
            issues.Add(MissingAuthorityIssue(kind, rewardPath, DescribeRewardForActual(kind, reward)));
    }

    private static QuestRewardAuthorityIssue MissingAuthorityIssue(QuestRewardKind kind, string rewardPath, string actual)
    {
        var (code, message, expected, hint) = kind switch
        {
            QuestRewardKind.Item => (
                MissingItemAuthorityCode,
                "Quest item reward должен ссылаться на canonical inventory/item authority или иметь explicit historical/unavailable reason.",
                "current inventory itemId/existedId/itemName or authorityStatus + reason",
                "Добавь наградный предмет в current inventory/detail authority либо замени reward на structured object с authorityStatus=HistoricalOnly/Unavailable и player-facing reason."),
            QuestRewardKind.Skill => (
                MissingSkillAuthorityCode,
                "Quest skill reward должен ссылаться на canonical active/passive skill authority или иметь explicit historical/unavailable reason.",
                "current active/passive skillName/skillId or authorityStatus + reason",
                "Добавь навык в skills_active.json/skills_passive.json либо замени reward на structured object с authorityStatus=HistoricalOnly/Unavailable и player-facing reason."),
            _ => (
                MissingRelationshipAuthorityCode,
                "Quest relationship reward должен ссылаться на canonical NPC/relationship authority или иметь explicit historical/unavailable reason.",
                "current NPC/NPC relationship id/name or authorityStatus + reason",
                "Добавь NPC/relationship authority либо замени reward на structured object с authorityStatus=HistoricalOnly/Unavailable и player-facing reason.")
        };

        return new QuestRewardAuthorityIssue(rewardPath, code, message, expected, actual, hint);
    }

    private static bool TryResolveStructuredReward(
        QuestRewardKind kind,
        JsonObject reward,
        QuestRewardAuthorityContext context,
        out string resolvedLabel)
    {
        if (kind == QuestRewardKind.Item && context.RequiresExactItemId)
        {
            if (reward["itemId"] is JsonValue itemIdValue &&
                itemIdValue.TryGetValue<string>(out var itemId) &&
                MortalItemIdentityRules.IsExactIdentity(itemId))
            {
                return context.TryResolve(kind, itemId, out resolvedLabel);
            }

            resolvedLabel = string.Empty;
            return false;
        }

        foreach (var candidate in ReadStructuredAuthorityCandidates(kind, reward))
        {
            if (context.TryResolve(kind, candidate, out resolvedLabel))
                return true;
        }

        resolvedLabel = string.Empty;
        return false;
    }

    private static IEnumerable<string> ReadStructuredAuthorityCandidates(QuestRewardKind kind, JsonObject reward)
    {
        var linkFields = kind switch
        {
            QuestRewardKind.Item => ItemIdentityFields.Concat(ItemLinkNameFields).ToArray(),
            QuestRewardKind.Skill => SkillIdentityFields.Concat(SkillLinkNameFields).ToArray(),
            _ => RelationshipIdentityFields.Concat(RelationshipLinkNameFields).ToArray()
        };

        return kind == QuestRewardKind.Item
            ? ReadNodeStringsUntrimmed(reward, linkFields)
            : ReadNodeStrings(reward, linkFields);
    }

    private static string DescribeRewardForActual(QuestRewardKind kind, JsonObject reward)
    {
        var label = ReadDisplayLabel(kind, reward);
        var candidates = ReadStructuredAuthorityCandidates(kind, reward).ToList();
        if (!string.IsNullOrWhiteSpace(label) && candidates.Count > 0)
            return $"{label} ({string.Join(", ", candidates)})";
        if (!string.IsNullOrWhiteSpace(label))
            return label;
        if (candidates.Count > 0)
            return string.Join(", ", candidates);
        return FallbackRewardLabel(kind);
    }

    private static string ReadDisplayLabel(QuestRewardKind kind, JsonObject reward)
    {
        var fields = kind switch
        {
            QuestRewardKind.Item => ItemNameFields,
            QuestRewardKind.Skill => SkillNameFields,
            _ => RelationshipNameFields
        };

        return FirstNonEmpty(ReadNodeStrings(reward, fields)) ?? string.Empty;
    }

    private static string FormatResolvedLabel(QuestRewardKind kind, string rawReference, string resolvedLabel)
    {
        if (kind != QuestRewardKind.Relationship)
            return resolvedLabel;

        var delta = ReadDeltaSuffix(rawReference);
        return string.IsNullOrWhiteSpace(delta)
            ? resolvedLabel
            : $"{resolvedLabel} ({delta})";
    }

    private static string AppendRelationshipDelta(string label, JsonObject reward)
    {
        var delta = FirstNonEmpty(ReadNodeStrings(reward, "change", "delta", "relationshipDelta", "newRelationshipLevel"));
        if (string.IsNullOrWhiteSpace(delta))
            return label;

        if (int.TryParse(delta, out var numericDelta) && numericDelta > 0)
            delta = $"+{numericDelta}";

        return $"{label} ({delta})";
    }

    private static string? ReadExplicitUnavailableStatus(JsonObject reward)
    {
        foreach (var status in ReadNodeStrings(reward, StatusFields))
        {
            var normalized = NormalizeStatus(status);
            if (ExplicitUnavailableStatuses.Contains(normalized))
                return status;
        }

        return null;
    }

    internal static bool IsExplicitlyUnavailableReward(JsonObject reward)
    {
        ArgumentNullException.ThrowIfNull(reward);
        return ReadExplicitUnavailableStatus(reward) != null;
    }

    private static string FormatUnavailableStatus(string status) =>
        NormalizeStatus(status) switch
        {
            "historicalonly" or "historical" or "priorincarnation" or "unavailableincurrentincarnation" => "только в истории",
            "consumed" => "израсходовано",
            "sold" => "продано",
            "forgotten" => "забыто",
            "lost" => "утрачено",
            "destroyed" => "уничтожено",
            "archived" or "legacyonly" => "архивная награда",
            _ => "недоступно"
        };

    private static string NormalizeStatus(string value)
    {
        var result = new char[value.Length];
        var count = 0;
        foreach (var ch in value)
        {
            if (char.IsLetterOrDigit(ch))
                result[count++] = char.ToLowerInvariant(ch);
        }

        return new string(result, 0, count);
    }

    private static string FallbackRewardLabel(QuestRewardKind kind) =>
        kind switch
        {
            QuestRewardKind.Item => "Предмет из истории квеста",
            QuestRewardKind.Skill => "Навык из истории квеста",
            _ => "Изменение отношений из истории квеста"
        };

    private static bool TryGetProperty(JsonObject obj, string propertyName, out JsonNode? value)
    {
        foreach (var property in obj)
        {
            if (string.Equals(property.Key, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = null;
        return false;
    }

    private static IEnumerable<JsonObject> EnumerateObjects(JsonNode? root, params string[] collectionNames)
    {
        if (root is JsonArray rootArray)
        {
            foreach (var item in rootArray.OfType<JsonObject>())
                yield return item;
            yield break;
        }

        if (root is not JsonObject obj)
            yield break;

        foreach (var collectionName in collectionNames)
        {
            if (!TryGetProperty(obj, collectionName, out var collection) || collection is not JsonArray array)
                continue;

            foreach (var item in array.OfType<JsonObject>())
                yield return item;
        }
    }

    private static IEnumerable<string> ReadNodeStrings(JsonObject obj, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (!TryGetProperty(obj, propertyName, out var node))
                continue;

            if (TryReadScalarString(node, out var value) && !string.IsNullOrWhiteSpace(value))
                yield return value.Trim();
        }
    }

    private static IEnumerable<string> ReadReasonStrings(JsonObject obj)
    {
        foreach (var propertyName in ReasonFields)
        {
            if (!TryGetProperty(obj, propertyName, out var node) ||
                node is not JsonValue jsonValue ||
                !jsonValue.TryGetValue<string>(out var value) ||
                string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            yield return value.Trim();
        }
    }

    private static bool TryReadScalarString(JsonNode? node, out string value)
    {
        value = string.Empty;
        if (node is not JsonValue jsonValue)
            return false;

        if (jsonValue.TryGetValue<string>(out var text))
        {
            value = text;
            return true;
        }

        if (jsonValue.TryGetValue<int>(out var intValue))
        {
            value = intValue.ToString();
            return true;
        }

        if (jsonValue.TryGetValue<long>(out var longValue))
        {
            value = longValue.ToString();
            return true;
        }

        if (jsonValue.TryGetValue<double>(out var doubleValue))
        {
            value = doubleValue.ToString("G", System.Globalization.CultureInfo.InvariantCulture);
            return true;
        }

        return false;
    }

    private static string? FirstNonEmpty(IEnumerable<string> values) =>
        values.FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value));

    private static string StripRelationshipDeltaSuffix(string value)
    {
        var trimmed = value.Trim();
        foreach (var separator in new[] { '_', ' ' })
        {
            var separatorIndex = trimmed.LastIndexOf(separator);
            if (separatorIndex <= 0 || separatorIndex >= trimmed.Length - 1)
                continue;

            var suffix = trimmed[(separatorIndex + 1)..];
            if (IsSignedIntegerText(suffix))
                return trimmed[..separatorIndex];
        }

        return trimmed;
    }

    private static string ReadDeltaSuffix(string value)
    {
        var trimmed = value.Trim();
        foreach (var separator in new[] { '_', ' ' })
        {
            var separatorIndex = trimmed.LastIndexOf(separator);
            if (separatorIndex <= 0 || separatorIndex >= trimmed.Length - 1)
                continue;

            var suffix = trimmed[(separatorIndex + 1)..];
            if (IsSignedIntegerText(suffix))
                return suffix;
        }

        return string.Empty;
    }

    private static bool IsSignedIntegerText(string value)
    {
        if (value.Length < 2 || value[0] is not ('+' or '-'))
            return false;

        for (var i = 1; i < value.Length; i++)
        {
            if (!char.IsDigit(value[i]))
                return false;
        }

        return true;
    }

    internal static void RegisterItems(QuestRewardAuthorityContext context, JsonNode? root)
    {
        foreach (var item in EnumerateObjects(root, ItemCollectionNames))
            context.Register(QuestRewardKind.Item, ReadNodeStrings(item, ItemIdentityFields), FirstNonEmpty(ReadNodeStrings(item, ItemNameFields)));
    }

    internal static void RegisterAcceptedItems(QuestRewardAuthorityContext context, JsonNode? root)
    {
        if (root is not JsonObject obj || obj["items"] is not JsonArray items)
            return;

        foreach (var item in items.OfType<JsonObject>())
        {
            if (!MortalItemMaterializationContract.TryReadAcceptedIdentity(item, out var itemId))
                continue;

            context.RegisterAcceptedItem(itemId, FirstNonEmpty(ReadNodeStrings(item, ItemNameFields)));
        }
    }

    private static IEnumerable<string> ReadNodeStringsUntrimmed(JsonObject obj, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (!TryGetProperty(obj, propertyName, out var node))
                continue;

            if (TryReadScalarString(node, out var value) && !string.IsNullOrWhiteSpace(value))
                yield return value;
        }
    }

    internal static void RegisterSkills(QuestRewardAuthorityContext context, JsonNode? root)
    {
        foreach (var skill in EnumerateObjects(root, SkillCollectionNames))
            context.Register(QuestRewardKind.Skill, ReadNodeStrings(skill, SkillIdentityFields.Concat(SkillLinkNameFields).ToArray()), FirstNonEmpty(ReadNodeStrings(skill, SkillNameFields)));
    }

    internal static void RegisterRelationships(QuestRewardAuthorityContext context, JsonNode? relationshipRoot, JsonNode? npcCoreRoot)
    {
        foreach (var relation in EnumerateObjects(relationshipRoot, RelationshipCollectionNames))
            context.Register(QuestRewardKind.Relationship, ReadNodeStrings(relation, RelationshipIdentityFields.Concat(RelationshipLinkNameFields).ToArray()), FirstNonEmpty(ReadNodeStrings(relation, RelationshipNameFields)));

        foreach (var npc in EnumerateObjects(npcCoreRoot, NpcCollectionNames))
            context.Register(QuestRewardKind.Relationship, ReadNodeStrings(npc, RelationshipIdentityFields.Concat(RelationshipLinkNameFields).ToArray()), FirstNonEmpty(ReadNodeStrings(npc, RelationshipNameFields)));
    }

    internal static string NormalizeRelationshipReference(string value) => StripRelationshipDeltaSuffix(value);
}

internal sealed class QuestRewardAuthorityContext
{
    private readonly bool _exactItemAuthority;
    private readonly Dictionary<string, string> _itemLabels;
    private readonly Dictionary<string, string> _skillLabels = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _relationshipLabels = new(StringComparer.OrdinalIgnoreCase);

    private QuestRewardAuthorityContext(bool exactItemAuthority = false)
    {
        _exactItemAuthority = exactItemAuthority;
        _itemLabels = new Dictionary<string, string>(
            exactItemAuthority ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase);
    }

    public static QuestRewardAuthorityContext Create(
        JsonNode? inventoryRoot,
        JsonNode? activeSkillsRoot,
        JsonNode? passiveSkillsRoot,
        JsonNode? npcCoreRoot,
        JsonNode? npcRelationshipsRoot)
    {
        var context = new QuestRewardAuthorityContext();
        QuestRewardAuthority.RegisterItems(context, inventoryRoot);
        QuestRewardAuthority.RegisterSkills(context, activeSkillsRoot);
        QuestRewardAuthority.RegisterSkills(context, passiveSkillsRoot);
        QuestRewardAuthority.RegisterRelationships(context, npcRelationshipsRoot, npcCoreRoot);
        return context;
    }

    public static QuestRewardAuthorityContext CreatePlayerProjection(
        JsonNode? inventoryRoot,
        JsonNode? activeSkillsRoot,
        JsonNode? passiveSkillsRoot,
        JsonNode? npcCoreRoot,
        JsonNode? npcRelationshipsRoot)
    {
        var context = new QuestRewardAuthorityContext(exactItemAuthority: true);
        QuestRewardAuthority.RegisterAcceptedItems(context, inventoryRoot);
        QuestRewardAuthority.RegisterSkills(context, activeSkillsRoot);
        QuestRewardAuthority.RegisterSkills(context, passiveSkillsRoot);
        QuestRewardAuthority.RegisterRelationships(context, npcRelationshipsRoot, npcCoreRoot);
        return context;
    }

    internal void RegisterAcceptedItem(string itemId, string? label)
    {
        if (!_exactItemAuthority ||
            string.IsNullOrWhiteSpace(itemId) ||
            !string.Equals(itemId, itemId.Trim(), StringComparison.Ordinal))
        {
            return;
        }

        _itemLabels[itemId] = !string.IsNullOrWhiteSpace(label) ? label.Trim() : itemId;
    }

    internal bool RequiresExactItemId => _exactItemAuthority;

    public void Register(QuestRewardKind kind, IEnumerable<string> identities, string? label)
    {
        var keys = identities
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!string.IsNullOrWhiteSpace(label))
            keys.Add(label.Trim());

        if (keys.Count == 0)
            return;

        var displayLabel = !string.IsNullOrWhiteSpace(label) ? label.Trim() : keys[0];
        foreach (var key in keys.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(key))
                continue;

            Map(kind)[key] = displayLabel;
            if (kind == QuestRewardKind.Relationship)
            {
                var normalized = QuestRewardAuthority.NormalizeRelationshipReference(key);
                if (!string.IsNullOrWhiteSpace(normalized))
                    Map(kind)[normalized] = displayLabel;
            }
        }
    }

    public bool TryResolve(QuestRewardKind kind, string reference, out string label)
    {
        var map = Map(kind);
        var lookup = kind == QuestRewardKind.Item && _exactItemAuthority
            ? reference
            : reference.Trim();
        if (kind == QuestRewardKind.Item &&
            _exactItemAuthority &&
            !string.Equals(lookup, lookup.Trim(), StringComparison.Ordinal))
        {
            label = string.Empty;
            return false;
        }

        if (map.TryGetValue(lookup, out label!))
            return true;

        if (kind == QuestRewardKind.Relationship)
        {
            var normalized = QuestRewardAuthority.NormalizeRelationshipReference(reference);
            if (map.TryGetValue(normalized, out label!))
                return true;
        }

        label = string.Empty;
        return false;
    }

    private Dictionary<string, string> Map(QuestRewardKind kind) =>
        kind switch
        {
            QuestRewardKind.Item => _itemLabels,
            QuestRewardKind.Skill => _skillLabels,
            _ => _relationshipLabels
        };
}

internal sealed record QuestRewardAuthorityIssue(
    string Path,
    string Code,
    string Message,
    string Expected,
    string Actual,
    string RepairHint,
    string? Actor = null);
