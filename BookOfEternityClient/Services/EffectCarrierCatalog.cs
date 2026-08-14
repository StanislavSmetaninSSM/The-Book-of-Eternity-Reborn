using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal sealed record EffectCarrierCoordinate(
    string Kind,
    string OwnerId,
    string Path,
    string? Category);

internal sealed record EffectCarrierOccurrence(
    string EffectId,
    string FilePath,
    string JsonPath,
    EffectCarrierCoordinate Coordinate,
    JsonObject Effect);

internal sealed record EffectCarrierCatalogInput(
    JsonObject? PlayerEffects,
    JsonObject? NpcEffects,
    JsonObject? EnemyCombatants,
    JsonObject? AllyCombatants,
    JsonObject? AfterlifeProfiles,
    JsonObject? SpiritualConflict);

internal sealed class EffectCarrierCatalog
{
    internal const string PlayerPath = "game_state/player/effects.json";
    internal const string NpcPath = "game_state/npcs/npc_effects.json";
    internal const string EnemiesPath = "game_state/combat/enemies.json";
    internal const string AlliesPath = "game_state/combat/allies.json";
    internal const string AfterlifeProfilesPath = "game_state/meta/afterlife_entity_profiles.json";
    internal const string SpiritualConflictPath = "game_state/meta/afterlife_spiritual_conflict_state.json";

    private readonly Dictionary<string, List<EffectCarrierOccurrence>> _byEffectId;
    private readonly HashSet<string> _invalidEffectIds;

    private EffectCarrierCatalog(Builder builder)
    {
        Occurrences = builder.Occurrences.ToArray();
        Issues = builder.Issues.ToArray();
        _invalidEffectIds = new HashSet<string>(builder.InvalidEffectIds, StringComparer.Ordinal);
        _byEffectId = builder.ByEffectId.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value.ToList(),
            StringComparer.Ordinal);
    }

    internal IReadOnlyList<EffectCarrierOccurrence> Occurrences { get; }

    internal IReadOnlyList<ValidationIssue> Issues { get; }

    internal bool TryResolveOne(string effectId, out EffectCarrierOccurrence occurrence)
    {
        occurrence = null!;
        if (_invalidEffectIds.Contains(effectId) ||
            !_byEffectId.TryGetValue(effectId, out var candidates) ||
            candidates.Count != 1)
        {
            return false;
        }
        occurrence = candidates[0];
        return true;
    }

    internal static EffectCarrierCatalog Build(EffectCarrierCatalogInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var builder = new Builder();
        builder.ScanPlayer(input.PlayerEffects);
        builder.ScanNpcs(input.NpcEffects);
        builder.ScanCombatants(input.EnemyCombatants, EnemiesPath, "enemiesData");
        builder.ScanCombatants(input.AllyCombatants, AlliesPath, "alliesData");
        builder.ScanAfterlifeProfiles(input.AfterlifeProfiles);
        builder.ScanSpiritualConflict(input.SpiritualConflict);
        builder.FinalizeIdentityUniqueness();
        return new EffectCarrierCatalog(builder);
    }

    internal static string CreateAuthorityFingerprint(EffectCarrierCatalogInput input)
    {
        var catalog = Build(input);
        var root = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["occurrences"] = new JsonArray(catalog.Occurrences
                .OrderBy(static occurrence => occurrence.FilePath, StringComparer.Ordinal)
                .ThenBy(static occurrence => occurrence.JsonPath, StringComparer.Ordinal)
                .ThenBy(static occurrence => occurrence.EffectId, StringComparer.Ordinal)
                .Select(static occurrence => (JsonNode)new JsonObject
                {
                    ["effectId"] = occurrence.EffectId,
                    ["filePath"] = occurrence.FilePath,
                    ["jsonPath"] = occurrence.JsonPath,
                    ["ownerKind"] = occurrence.Coordinate.Kind,
                    ["ownerId"] = occurrence.Coordinate.OwnerId,
                    ["category"] = occurrence.Coordinate.Category,
                    ["effect"] = occurrence.Effect.DeepClone()
                }).ToArray()),
            ["issues"] = new JsonArray(catalog.Issues
                .OrderBy(static issue => issue.FilePath, StringComparer.Ordinal)
                .ThenBy(static issue => issue.Code, StringComparer.Ordinal)
                .Select(static issue => (JsonNode)new JsonObject
                {
                    ["filePath"] = issue.FilePath,
                    ["code"] = issue.Code,
                    ["expected"] = issue.Expected,
                    ["actual"] = issue.Actual
                }).ToArray())
        };
        return Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(root.ToJsonString())));
    }

    private sealed class Builder
    {
        internal List<EffectCarrierOccurrence> Occurrences { get; } = new();
        internal List<ValidationIssue> Issues { get; } = new();
        internal Dictionary<string, List<EffectCarrierOccurrence>> ByEffectId { get; } = new(StringComparer.Ordinal);
        internal HashSet<string> InvalidEffectIds { get; } = new(StringComparer.Ordinal);

        internal void ScanPlayer(JsonObject? root)
        {
            if (root == null)
                return;
            if (!ValidateCarrierRoot(root, PlayerPath, EffectCarrierKind.Player))
                return;
            if (root["activeEffects"] is not JsonArray effects)
                return;
            ScanCanonicalArray(
                effects,
                PlayerPath,
                PlayerPath + ".activeEffects",
                new EffectCarrierCoordinate("player", "player_current", PlayerPath, null),
                "player",
                "player_current");
        }

        internal void ScanNpcs(JsonObject? root)
        {
            if (root == null)
                return;
            if (!ValidateCarrierRoot(root, NpcPath, EffectCarrierKind.Npc) || root["entries"] is not JsonArray entries)
                return;

            for (var index = 0; index < entries.Count; index++)
            {
                if (entries[index] is not JsonObject entry ||
                    !TryReadExact(entry["NPCId"], out var npcId) ||
                    entry["activeEffects"] is not JsonArray effects)
                {
                    continue;
                }
                ScanCanonicalArray(
                    effects,
                    NpcPath,
                    $"{NpcPath}.entries[{index}].activeEffects",
                    new EffectCarrierCoordinate("npc", npcId, NpcPath, null),
                    "npc",
                    npcId);
            }
        }

        internal void ScanCombatants(JsonObject? root, string filePath, string collection)
        {
            if (root == null)
                return;
            if (root[collection] is not JsonArray combatants)
            {
                if (root.Count > 0)
                    Add(filePath + "." + collection, "effect_materialization_legacy_carrier_unsupported", "combatant array with activeBuffs/activeDebuffs", root.ToJsonString());
                return;
            }

            for (var index = 0; index < combatants.Count; index++)
            {
                if (combatants[index] is not JsonObject combatant)
                    continue;
                if (combatant.ContainsKey("combatantRef"))
                {
                    Add(
                        $"{filePath}.{collection}[{index}].combatantRef",
                        "effect_target_combatant_ref_not_consumed",
                        "field absent after client allocation of combatantId",
                        combatant["combatantRef"]?.ToJsonString() ?? "null");
                }
                if (!TryReadExact(combatant["combatantId"], out var combatantId))
                {
                    if (HasNonEmptyArray(combatant["activeBuffs"]) ||
                        HasNonEmptyArray(combatant["activeDebuffs"]))
                    {
                        Add(
                            $"{filePath}.{collection}[{index}]",
                            "effect_materialization_legacy_carrier_unsupported",
                            "client-owned combatantId before any non-empty activeBuffs/activeDebuffs carrier",
                            combatant.ToJsonString());
                    }
                    continue;
                }
                if (combatant["activeBuffs"] is JsonArray buffs)
                {
                    ScanCanonicalArray(
                        buffs,
                        filePath,
                        $"{filePath}.{collection}[{index}].activeBuffs",
                        new EffectCarrierCoordinate("combatant", combatantId, filePath, "buff"),
                        "combatant",
                        combatantId);
                }
                if (combatant["activeDebuffs"] is JsonArray debuffs)
                {
                    ScanCanonicalArray(
                        debuffs,
                        filePath,
                        $"{filePath}.{collection}[{index}].activeDebuffs",
                        new EffectCarrierCoordinate("combatant", combatantId, filePath, "debuff"),
                        "combatant",
                        combatantId);
                }
            }
        }

        internal void ScanAfterlifeProfiles(JsonObject? root)
        {
            if (root == null)
                return;
            if (root["profiles"] is not JsonArray profiles)
            {
                if (root.Count > 0)
                    Add(AfterlifeProfilesPath + ".profiles", "effect_materialization_legacy_carrier_unsupported", "afterlife profiles array", root.ToJsonString());
                return;
            }

            for (var index = 0; index < profiles.Count; index++)
            {
                if (profiles[index] is not JsonObject profile ||
                    !TryReadExact(profile["actorId"], out var actorId) ||
                    !TryReadExact(profile["actorType"], out var actorType) ||
                    profile["activeEffects"] is not JsonArray effects)
                {
                    continue;
                }
                var targetKind = actorType switch
                {
                    "guardian" => "guardian",
                    "resident" or "shining_resident" => "resident",
                    "radiant_actor" => "radiant_actor",
                    "player_soul" => "player",
                    _ => "afterlife_actor"
                };
                ScanCanonicalArray(
                    effects,
                    AfterlifeProfilesPath,
                    $"{AfterlifeProfilesPath}.profiles[{index}].activeEffects",
                    new EffectCarrierCoordinate("afterlife_profile", actorId, AfterlifeProfilesPath, null),
                    targetKind,
                    actorId);
            }
        }

        internal void ScanSpiritualConflict(JsonObject? root)
        {
            if (root == null)
                return;
            if (root["activeConflict"] is not JsonObject conflict)
                return;
            if (!TryReadExact(conflict["conflictId"], out var conflictId) ||
                conflict["combatConditions"] is not JsonArray conditions)
            {
                return;
            }

            for (var index = 0; index < conditions.Count; index++)
            {
                if (conditions[index] is not JsonObject condition)
                    continue;
                var jsonPath = $"{SpiritualConflictPath}.activeConflict.combatConditions[{index}]";
                if (!TryReadExact(condition["effectId"], out var effectId))
                {
                    Add(jsonPath + ".effectId", "effect_materialization_invalid_field", "client-owned permanent effectId", Describe(condition["effectId"]));
                    continue;
                }
                if (!TryReadExact(condition["targetSide"], out var side))
                {
                    AddInvalid(effectId, jsonPath + ".targetSide", "exact spiritual conflict side", Describe(condition["targetSide"]));
                    continue;
                }
                var expectedTargetId = conflictId + ":" + side;
                if (condition["target"] is not JsonObject target ||
                    !TryReadExact(target["kind"], out var targetKind) ||
                    !TryReadExact(target["targetId"], out var targetId) ||
                    !string.Equals(targetKind, "spiritual_conflict_side", StringComparison.Ordinal) ||
                    !string.Equals(targetId, expectedTargetId, StringComparison.Ordinal))
                {
                    AddInvalid(effectId, jsonPath + ".target", "exact current conflict-side target", condition["target"]?.ToJsonString() ?? "missing");
                }
                if (!TryReadExact(condition["state"], out var state) || state is not ("active" or "suspended"))
                    AddInvalid(effectId, jsonPath + ".state", "active | suspended", Describe(condition["state"]));
                if (!TryReadExact(condition["realm"], out var realm) || realm is not ("chaos_sea" or "shining_abode"))
                    AddInvalid(effectId, jsonPath + ".realm", "chaos_sea | shining_abode", Describe(condition["realm"]));

                AddOccurrence(
                    effectId,
                    SpiritualConflictPath,
                    jsonPath,
                    new EffectCarrierCoordinate("spiritual_conflict", conflictId, SpiritualConflictPath, side),
                    condition);
            }
        }

        internal void FinalizeIdentityUniqueness()
        {
            var aliases = new Dictionary<string, (string Identity, string Path)>(StringComparer.Ordinal);
            foreach (var pair in ByEffectId)
            {
                if (pair.Value.Count > 1)
                {
                    InvalidEffectIds.Add(pair.Key);
                    Add(
                        pair.Value[1].JsonPath + ".effectId",
                        "effect_materialization_duplicate_carrier",
                        "exactly one logical carrier occurrence",
                        $"{pair.Value.Count} occurrences of {pair.Key}");
                }

                var alias = MortalLocationIdentityState.BuildConfusableKey(pair.Key);
                if (aliases.TryGetValue(alias, out var first) && !string.Equals(first.Identity, pair.Key, StringComparison.Ordinal))
                {
                    InvalidEffectIds.Add(pair.Key);
                    InvalidEffectIds.Add(first.Identity);
                    Add(
                        pair.Value[0].JsonPath + ".effectId",
                        "effect_materialization_confusable_effect_id",
                        "globally unique exact/confusable effectId",
                        pair.Key);
                }
                else
                {
                    aliases[alias] = (pair.Key, pair.Value[0].JsonPath);
                }
            }
        }

        private bool ValidateCarrierRoot(JsonObject root, string path, EffectCarrierKind kind)
        {
            using var document = JsonDocument.Parse(root.ToJsonString());
            var carrierIssues = EffectMaterializationContract.ValidateCarrier(document.RootElement, path, kind);
            Issues.AddRange(carrierIssues);
            return carrierIssues.Count == 0;
        }

        private void ScanCanonicalArray(
            JsonArray effects,
            string filePath,
            string arrayPath,
            EffectCarrierCoordinate coordinate,
            string expectedTargetKind,
            string expectedTargetId)
        {
            for (var index = 0; index < effects.Count; index++)
            {
                if (effects[index] is not JsonObject effect)
                {
                    Add($"{arrayPath}[{index}]", "effect_materialization_invalid_field", "canonical active effect object", Describe(effects[index]));
                    continue;
                }
                var jsonPath = $"{arrayPath}[{index}]";
                using var document = JsonDocument.Parse(effect.ToJsonString());
                var contractIssues = EffectMaterializationContract.Validate(
                    document.RootElement,
                    jsonPath,
                    EffectMaterializationPhase.CanonicalActive);
                Issues.AddRange(contractIssues);
                if (!TryReadExact(effect["effectId"], out var effectId))
                    continue;
                if (contractIssues.Count > 0)
                    InvalidEffectIds.Add(effectId);

                if (effect["target"] is not JsonObject target ||
                    !TryReadExact(target["kind"], out var targetKind) ||
                    !TryReadExact(target["targetId"], out var targetId) ||
                    !string.Equals(targetKind, expectedTargetKind, StringComparison.Ordinal) ||
                    !string.Equals(targetId, expectedTargetId, StringComparison.Ordinal))
                {
                    AddInvalid(
                        effectId,
                        jsonPath + ".target",
                        $"target kind/id {expectedTargetKind}/{expectedTargetId} matching logical carrier",
                        effect["target"]?.ToJsonString() ?? "missing");
                }

                if (string.Equals(coordinate.Kind, "combatant", StringComparison.Ordinal) &&
                    (effect["display"] is not JsonObject display ||
                     !TryReadExact(display["category"], out var category) ||
                     !string.Equals(category, coordinate.Category, StringComparison.Ordinal)))
                {
                    InvalidEffectIds.Add(effectId);
                    Add(
                        jsonPath + ".display.category",
                        "effect_materialization_combat_category_collection_mismatch",
                        $"category '{coordinate.Category}' matching the physical combat collection",
                        effect["display"]?["category"]?.ToJsonString() ?? "missing");
                }

                AddOccurrence(effectId, filePath, jsonPath, coordinate, effect);
            }
        }

        private void AddOccurrence(
            string effectId,
            string filePath,
            string jsonPath,
            EffectCarrierCoordinate coordinate,
            JsonObject effect)
        {
            var occurrence = new EffectCarrierOccurrence(
                effectId,
                filePath,
                jsonPath,
                coordinate,
                effect.DeepClone().AsObject());
            Occurrences.Add(occurrence);
            if (!ByEffectId.TryGetValue(effectId, out var list))
            {
                list = new List<EffectCarrierOccurrence>();
                ByEffectId.Add(effectId, list);
            }
            list.Add(occurrence);
        }

        private void AddInvalid(string effectId, string path, string expected, string actual)
        {
            InvalidEffectIds.Add(effectId);
            Add(path, "effect_materialization_target_carrier_mismatch", expected, actual);
        }

        private void Add(string path, string code, string expected, string actual) =>
            Issues.Add(new ValidationIssue(
                path,
                IssueSeverity.Error,
                "Active effect carrier violates exact owner-coordinate authority.",
                code: code,
                section: "effect_materialization",
                expected: expected,
                actual: actual,
                repairHint: "Restore the validated effect carrier or resubmit one source-authorized effect operation; do not copy or retarget active instances."));

        private static bool TryReadExact(JsonNode? node, out string value)
        {
            value = node is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var text)
                ? text
                : string.Empty;
            return value.Length > 0 && string.Equals(value, value.Trim(), StringComparison.Ordinal);
        }

        private static bool HasNonEmptyArray(JsonNode? node) =>
            node is JsonArray { Count: > 0 };

        private static string Describe(JsonNode? node) => node?.ToJsonString() ?? "missing";
    }
}
