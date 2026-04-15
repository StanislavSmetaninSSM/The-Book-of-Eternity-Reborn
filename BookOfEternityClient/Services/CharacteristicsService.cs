using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging;

namespace BookOfEternityClient.Services;

/// <summary>
/// Computes permanently modified and fully modified characteristics from base stats,
/// equipment, passive skills, soul relics, and temporary effects.
/// Writes computed results to game_state for GM reference.
/// </summary>
public class CharacteristicsService
{
    private readonly FileSystemManager _fs;
    private readonly StateManager _stateManager;
    private readonly ILogger<CharacteristicsService> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public CharacteristicsService(FileSystemManager fs, StateManager stateManager,
        ILogger<CharacteristicsService> logger)
    {
        _fs = fs;
        _stateManager = stateManager;
        _logger = logger;
    }

    /// <summary>
    /// Full computation result for one characteristic.
    /// </summary>
    public record StatBreakdown(
        string Name,
        int BaseValue,
        int PermanentBonus,
        int TemporaryBonus,
        int PermanentlyModified,  // Base + PermanentBonus
        int Modified,             // PermanentlyModified + TemporaryBonus
        List<BonusSource> PermanentSources,
        List<BonusSource> TemporarySources
    );

    public record BonusSource(string Origin, string Description, int Value);

    /// <summary>
    /// Full result of characteristics computation.
    /// </summary>
    public record ComputedCharacteristics(
        Dictionary<string, StatBreakdown> Stats,
        int PlayerLevel,
        int UnspentStatPoints
    );

    /// <summary>
    /// Computes all modified characteristics from base stats + bonuses.
    /// </summary>
    public async Task<ComputedCharacteristics> ComputeAsync()
    {
        var baseStats = await LoadBaseStats();
        var playerLevel = await GetPlayerLevel();
        var unspentPoints = await GetUnspentStatPoints();

        // Collect all bonus sources
        var permanentBonuses = new Dictionary<string, List<BonusSource>>();
        var temporaryBonuses = new Dictionary<string, List<BonusSource>>();
        foreach (var name in Characteristics.All)
        {
            permanentBonuses[name] = new List<BonusSource>();
            temporaryBonuses[name] = new List<BonusSource>();
        }

        // Source 1: Soul relics
        await CollectSoulRelicBonuses(permanentBonuses);

        // Source 2: Equipped items (structuredBonuses)
        await CollectEquipmentBonuses(permanentBonuses, temporaryBonuses);

        // Source 3: Passive skills (structuredBonuses)
        await CollectPassiveSkillBonuses(permanentBonuses, temporaryBonuses);

        // Source 4: Temporary effects (buffs/debuffs)
        await CollectTemporaryEffects(temporaryBonuses);

        // Build result
        var stats = new Dictionary<string, StatBreakdown>();
        foreach (var name in Characteristics.All)
        {
            var baseVal = baseStats.GetValueOrDefault(name, 1);
            var permBonus = permanentBonuses[name].Sum(b => b.Value);
            var tempBonus = temporaryBonuses[name].Sum(b => b.Value);
            var permMod = baseVal + permBonus;
            var modified = permMod + tempBonus;

            stats[name] = new StatBreakdown(
                name, baseVal, permBonus, tempBonus,
                permMod, modified,
                permanentBonuses[name], temporaryBonuses[name]
            );
        }

        return new ComputedCharacteristics(stats, playerLevel, unspentPoints);
    }

    /// <summary>
    /// Computes and writes the result to game_state/player/computed_characteristics.json
    /// for GM reference. Called after each turn.
    /// </summary>
    public async Task ComputeAndWriteAsync()
    {
        try
        {
            var result = await ComputeAsync();
            await WriteComputedFile(result);
            _logger.LogDebug("Характеристики пересчитаны и записаны");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ошибка при пересчёте характеристик");
        }
    }

    /// <summary>
    /// Applies statsIncreased: +1 to each named stat, respecting Training Cap.
    /// Returns list of applied changes and any blocked stats.
    /// </summary>
    public async Task<(List<string> applied, List<string> blocked)> ApplyStatsIncreasedAsync(
        string[] statsToIncrease)
    {
        var applied = new List<string>();
        var blocked = new List<string>();

        var baseStats = await LoadBaseStats();
        var playerLevel = await GetPlayerLevel();
        var trainingCap = playerLevel * 2;

        foreach (var statName in statsToIncrease)
        {
            var normalized = statName.ToLowerInvariant().Trim();
            if (!baseStats.ContainsKey(normalized))
            {
                _logger.LogWarning("Неизвестная характеристика в statsIncreased: {Stat}", statName);
                continue;
            }

            var current = baseStats[normalized];
            if (current >= trainingCap)
            {
                blocked.Add(normalized);
                _logger.LogInformation(
                    "statsIncreased заблокирован Training Cap: {Stat}={Val} >= {Cap}",
                    normalized, current, trainingCap);
            }
            else if (current >= 100)
            {
                blocked.Add(normalized);
            }
            else
            {
                baseStats[normalized] = Math.Min(current + 1, 100);
                applied.Add(normalized);
            }
        }

        if (applied.Count > 0)
            await SaveBaseStats(baseStats);

        return (applied, blocked);
    }

    /// <summary>
    /// Initializes characteristics for a new incarnation: all stats = 1.
    /// </summary>
    public async Task InitializeForNewIncarnation()
    {
        var stats = new Dictionary<string, int>();
        foreach (var name in Characteristics.All)
            stats[name] = 1;

        await SaveBaseStats(stats);
        await SaveUnspentStatPoints(8);
        _logger.LogInformation("Характеристики инициализированы для новой инкарнации (все=1, 8 очков)");
    }

    /// <summary>
    /// Adds stat points (e.g., 5 on level-up).
    /// </summary>
    public async Task AddStatPoints(int points)
    {
        var current = await GetUnspentStatPoints();
        await SaveUnspentStatPoints(current + points);
    }

    /// <summary>
    /// Distributes points: applies allocations to base stats, decrements unspent.
    /// </summary>
    public async Task<bool> DistributePointsAsync(Dictionary<string, int> allocations)
    {
        var totalToSpend = allocations.Values.Sum();
        var available = await GetUnspentStatPoints();
        if (totalToSpend > available) return false;

        var baseStats = await LoadBaseStats();
        foreach (var (stat, points) in allocations)
        {
            if (!baseStats.ContainsKey(stat)) continue;
            baseStats[stat] = Math.Min(baseStats[stat] + points, 100);
        }

        await SaveBaseStats(baseStats);
        await SaveUnspentStatPoints(available - totalToSpend);
        return true;
    }

    // ═══════════════════════════════════════════
    //  Private: Data loaders
    // ═══════════════════════════════════════════

    private async Task<Dictionary<string, int>> LoadBaseStats()
    {
        var result = new Dictionary<string, int>();
        foreach (var name in Characteristics.All)
            result[name] = 1; // default base

        var json = await _fs.ReadFileAsync("game_state/misc/characteristics.json");
        if (json == null) return result;

        try
        {
            using var doc = JsonDocument.Parse(json);
            foreach (var name in Characteristics.All)
            {
                if (doc.RootElement.TryGetProperty(name, out var val))
                {
                    if (val.ValueKind == JsonValueKind.Number && val.TryGetInt32(out var n))
                        result[name] = n;
                    else if (val.ValueKind == JsonValueKind.String && int.TryParse(val.GetString(), out var p))
                        result[name] = p;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ошибка чтения characteristics.json");
        }

        return result;
    }

    private async Task SaveBaseStats(Dictionary<string, int> stats)
    {
        var obj = new Dictionary<string, object>(stats.Select(kv =>
            new KeyValuePair<string, object>(kv.Key, kv.Value)));
        await _fs.WriteFileAtomicAsync("game_state/misc/characteristics.json",
            JsonSerializer.Serialize(obj, JsonOpts));
    }

    private async Task<int> GetPlayerLevel()
    {
        // Prefer experience.json because local systems such as QTE can advance level there.
        var json = await _fs.ReadFileAsync("game_state/player/experience.json");
        if (json != null)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("level", out var lvl) &&
                    lvl.ValueKind == JsonValueKind.Number)
                    return lvl.GetInt32();
                if (doc.RootElement.TryGetProperty("playerLevel", out var playerLvl) &&
                    playerLvl.ValueKind == JsonValueKind.Number)
                    return playerLvl.GetInt32();
            }
            catch { /* fallthrough */ }
        }

        // Try player_status next, then soul_state
        json = await _fs.ReadFileAsync("game_state/core/player_status.json");
        if (json != null)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("level", out var lvl) &&
                    lvl.ValueKind == JsonValueKind.Number)
                    return lvl.GetInt32();
            }
            catch { /* fallthrough */ }
        }

        // Fallback: check soul_state for level
        json = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        if (json != null)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("level", out var lvl) &&
                    lvl.ValueKind == JsonValueKind.Number)
                    return lvl.GetInt32();
            }
            catch { /* fallthrough */ }
        }

        return 1;
    }

    public async Task<int> GetUnspentStatPoints()
    {
        var json = await _fs.ReadFileAsync("game_state/player/stat_points.json");
        if (json == null) return 0;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("unspentStatPoints", out var pts) &&
                pts.ValueKind == JsonValueKind.Number)
                return pts.GetInt32();
        }
        catch { /* fallthrough */ }

        return 0;
    }

    private async Task SaveUnspentStatPoints(int points)
    {
        var obj = new { unspentStatPoints = points };
        await _fs.WriteFileAtomicAsync("game_state/player/stat_points.json",
            JsonSerializer.Serialize(obj, JsonOpts));
    }

    // ═══════════════════════════════════════════
    //  Private: Bonus collectors
    // ═══════════════════════════════════════════

    /// <summary>
    /// Soul relics: effects.characteristicBonuses = { "attractiveness": 5 }
    /// These are always permanent.
    /// </summary>
    private async Task CollectSoulRelicBonuses(Dictionary<string, List<BonusSource>> permanent)
    {
        var json = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        if (json == null) return;

        try
        {
            if (JsonNode.Parse(json) is not JsonObject soulRoot)
                return;

            var lifeTransitionsJson = await _fs.ReadFileAsync("game_state/control/life_transitions.json");
            var hasCanonicalTriggerLifeEnd = await CanonicalStateNormalizer.HasLifecycleAuthorizedTriggerLifeEndFromPendingSnapshotAsync(
                _fs,
                lifeTransitionsJson,
                soulRoot);

            if (!GuardianPolicyContracts.TryReadStrictCurrentSoulRelicCollections(
                    soulRoot,
                    hasCanonicalTriggerLifeEnd,
                    out var equipped,
                    out _,
                    out var failureDescription))
            {
                _logger.LogWarning(
                    "Пропускаю бонусы реликвий: current soul_state.soulRelics нечитаем для strict guardian-policy path ({FailureDescription})",
                    failureDescription);
                return;
            }

            if (equipped == null)
                return;

            foreach (var relic in equipped.OfType<JsonObject>())
            {
                var relicId = relic["relicId"] is JsonValue ridValue && ridValue.TryGetValue<string>(out var rid)
                    ? rid ?? "?"
                    : relic["name"] is JsonValue rnValue && rnValue.TryGetValue<string>(out var rn) ? rn ?? "?" : "Реликвия";

                if (relic["effects"] is not JsonObject effects) continue;
                if (effects["characteristicBonuses"] is not JsonObject bonuses) continue;

                foreach (var prop in bonuses)
                {
                    var statName = prop.Key.ToLowerInvariant();
                    if (!permanent.ContainsKey(statName)) continue;

                    var value = prop.Value is JsonValue bonusValue &&
                                bonusValue.TryGetValue<int>(out var parsedValue)
                        ? parsedValue
                        : 0;
                    if (value != 0)
                    {
                        permanent[statName].Add(new BonusSource(
                            $"🔮 Реликвия: {relicId}",
                            $"+{value}",
                            value));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ошибка чтения бонусов реликвий");
        }
    }

    /// <summary>
    /// Equipped items: structuredBonuses where bonusType=Characteristic.
    /// Permanent if application=Permanent and condition=null.
    /// </summary>
    private async Task CollectEquipmentBonuses(
        Dictionary<string, List<BonusSource>> permanent,
        Dictionary<string, List<BonusSource>> temporary)
    {
        var json = await _fs.ReadFileAsync("game_state/inventory/items.json");
        if (json == null) return;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var items = FindPreferredArray(doc.RootElement, "UpdateInventory", "items", "inventory");
            if (items == null) return;

            foreach (var item in items.Value.EnumerateArray())
            {
                // Only process equipped items
                var status = GetStr(item, "status", "");
                var equipped = GetStr(item, "equippedSlot", GetStr(item, "slot", ""));
                if (!status.Equals("equipped", StringComparison.OrdinalIgnoreCase) &&
                    string.IsNullOrEmpty(equipped))
                    continue;

                var itemName = GetStr(item, "itemName", GetStr(item, "name", "Предмет"));
                ExtractStructuredBonuses(item, itemName, "⚔️", permanent, temporary);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ошибка чтения бонусов экипировки");
        }
    }

    /// <summary>
    /// Passive skills: structuredBonuses + legacy playerStatBonus fallback.
    /// </summary>
    private async Task CollectPassiveSkillBonuses(
        Dictionary<string, List<BonusSource>> permanent,
        Dictionary<string, List<BonusSource>> temporary)
    {
        var json = await _fs.ReadFileAsync("game_state/player/skills_passive.json");
        if (json == null) return;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var skills = FindPreferredArray(doc.RootElement, "passiveSkillChanges");
            if (skills == null) return;

            foreach (var skill in skills.Value.EnumerateArray())
            {
                var skillName = GetStr(skill, "skillName", GetStr(skill, "name", "Навык"));
                ExtractStructuredBonuses(skill, skillName, "📘", permanent, temporary);

                // Legacy fallback: playerStatBonus string parsing
                if (!skill.TryGetProperty("structuredBonuses", out var sb) ||
                    sb.ValueKind != JsonValueKind.Array || sb.GetArrayLength() == 0)
                {
                    ExtractLegacyStatBonus(skill, skillName, permanent);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ошибка чтения бонусов пассивных навыков");
        }
    }

    /// <summary>
    /// Temporary effects: activeBuffs/activeDebuffs with stat bonuses.
    /// </summary>
    private async Task CollectTemporaryEffects(Dictionary<string, List<BonusSource>> temporary)
    {
        var json = await _fs.ReadFileAsync("game_state/player/effects.json");
        if (json == null) return;

        try
        {
            using var doc = JsonDocument.Parse(json);
            CollectEffectsFromArray(doc.RootElement, "playerActiveEffectsChanges", temporary);
            CollectEffectsFromArray(doc.RootElement, "activeBuffs", temporary);
            CollectEffectsFromArray(doc.RootElement, "activeDebuffs", temporary);

            // Root-level array
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var effect in doc.RootElement.EnumerateArray())
                    ExtractEffectBonuses(effect, temporary);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ошибка чтения временных эффектов");
        }
    }

    // ═══════════════════════════════════════════
    //  Private: Bonus extraction helpers
    // ═══════════════════════════════════════════

    private void ExtractStructuredBonuses(JsonElement parent, string sourceName, string icon,
        Dictionary<string, List<BonusSource>> permanent,
        Dictionary<string, List<BonusSource>> temporary)
    {
        if (!parent.TryGetProperty("structuredBonuses", out var bonuses)) return;
        if (bonuses.ValueKind != JsonValueKind.Array) return;

        foreach (var bonus in bonuses.EnumerateArray())
        {
            var bonusType = GetStr(bonus, "bonusType", "");
            if (!bonusType.Equals("Characteristic", StringComparison.OrdinalIgnoreCase)) continue;

            var valueType = GetStr(bonus, "valueType", "");
            if (!valueType.Equals("Flat", StringComparison.OrdinalIgnoreCase)) continue;

            var target = GetStr(bonus, "target", "").ToLowerInvariant();
            if (!permanent.ContainsKey(target)) continue;

            var value = 0;
            if (bonus.TryGetProperty("value", out var v))
            {
                if (v.ValueKind == JsonValueKind.Number) value = v.GetInt32();
                else if (v.ValueKind == JsonValueKind.String && int.TryParse(v.GetString(), out var pv))
                    value = pv;
            }
            if (value == 0) continue;

            var application = GetStr(bonus, "application", "Permanent");
            var condition = GetStr(bonus, "condition", "");
            var desc = GetStr(bonus, "description", $"{(value > 0 ? "+" : "")}{value}");

            var source = new BonusSource($"{icon} {sourceName}", desc, value);

            if (application.Equals("Permanent", StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrEmpty(condition))
            {
                permanent[target].Add(source);
            }
            else
            {
                temporary[target].Add(source);
            }
        }
    }

    /// <summary>
    /// Legacy fallback: parse playerStatBonus string like "+5 strength" or "+3 к силе".
    /// </summary>
    private void ExtractLegacyStatBonus(JsonElement skill, string skillName,
        Dictionary<string, List<BonusSource>> permanent)
    {
        if (!skill.TryGetProperty("playerStatBonus", out var psb)) return;
        var text = psb.ValueKind == JsonValueKind.String ? psb.GetString() ?? "" : "";
        if (string.IsNullOrWhiteSpace(text)) return;

        // Try to parse patterns like "+5 strength", "+3 perception"
        foreach (var name in Characteristics.All)
        {
            if (text.Contains(name, StringComparison.OrdinalIgnoreCase))
            {
                // Extract number
                var match = System.Text.RegularExpressions.Regex.Match(text, @"[+-]?\d+");
                if (match.Success && int.TryParse(match.Value, out var val) && val != 0)
                {
                    permanent[name].Add(new BonusSource(
                        $"📘 {skillName} (legacy)",
                        text, val));
                }
            }
        }
    }

    private void CollectEffectsFromArray(JsonElement root, string propName,
        Dictionary<string, List<BonusSource>> temporary)
    {
        if (!root.TryGetProperty(propName, out var arr)) return;
        if (arr.ValueKind != JsonValueKind.Array) return;

        foreach (var effect in arr.EnumerateArray())
            ExtractEffectBonuses(effect, temporary);
    }

    private void ExtractEffectBonuses(JsonElement effect,
        Dictionary<string, List<BonusSource>> temporary)
    {
        var effectName = GetStr(effect, "effectName",
            GetStr(effect, "name",
                GetStr(effect, "buffName",
                    GetStr(effect, "debuffName", "Эффект"))));

        // Check structuredBonuses on effects too
        if (effect.TryGetProperty("structuredBonuses", out var sb) &&
            sb.ValueKind == JsonValueKind.Array)
        {
            foreach (var bonus in sb.EnumerateArray())
            {
                var bonusType = GetStr(bonus, "bonusType", "");
                if (!bonusType.Equals("Characteristic", StringComparison.OrdinalIgnoreCase)) continue;

                var target = GetStr(bonus, "target", "").ToLowerInvariant();
                if (!temporary.ContainsKey(target)) continue;

                var value = 0;
                if (bonus.TryGetProperty("value", out var v) && v.ValueKind == JsonValueKind.Number)
                    value = v.GetInt32();
                if (value != 0)
                {
                    temporary[target].Add(new BonusSource(
                        $"✨ {effectName}", GetStr(bonus, "description", $"{value}"), value));
                }
            }
        }

        // Direct stat modification fields on the effect itself
        if (effect.TryGetProperty("statModifications", out var mods) &&
            mods.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in mods.EnumerateObject())
            {
                var statName = prop.Name.ToLowerInvariant();
                if (!temporary.ContainsKey(statName)) continue;
                var value = prop.Value.ValueKind == JsonValueKind.Number ? prop.Value.GetInt32() : 0;
                if (value != 0)
                {
                    temporary[statName].Add(new BonusSource(
                        $"✨ {effectName}", $"{(value > 0 ? "+" : "")}{value}", value));
                }
            }
        }
    }

    // ═══════════════════════════════════════════
    //  Private: Write computed result
    // ═══════════════════════════════════════════

    private async Task WriteComputedFile(ComputedCharacteristics result)
    {
        var standard = new Dictionary<string, int>();
        var permanentlyModified = new Dictionary<string, int>();
        var modified = new Dictionary<string, int>();
        var breakdown = new Dictionary<string, object>();

        foreach (var (name, stat) in result.Stats)
        {
            standard[name] = stat.BaseValue;
            permanentlyModified[name] = stat.PermanentlyModified;
            modified[name] = stat.Modified;

            if (stat.PermanentBonus != 0 || stat.TemporaryBonus != 0)
            {
                var sources = new List<string>();
                foreach (var s in stat.PermanentSources)
                    sources.Add($"[Пост.] {s.Origin}: {s.Description} ({(s.Value > 0 ? "+" : "")}{s.Value})");
                foreach (var s in stat.TemporarySources)
                    sources.Add($"[Врем.] {s.Origin}: {s.Description} ({(s.Value > 0 ? "+" : "")}{s.Value})");

                breakdown[name] = new
                {
                    @base = stat.BaseValue,
                    permanentBonus = stat.PermanentBonus,
                    temporaryBonus = stat.TemporaryBonus,
                    permanentlyModified = stat.PermanentlyModified,
                    modified = stat.Modified,
                    sources
                };
            }
        }

        var output = new
        {
            _note = "Автоматически вычислено клиентом. Используйте эти значения для проверок действий (Block 12).",
            playerLevel = result.PlayerLevel,
            unspentStatPoints = result.UnspentStatPoints,
            characteristics = standard,
            permanentlyModifiedCharacteristics = permanentlyModified,
            modifiedCharacteristics = modified,
            breakdown,
            _lastComputed = DateTime.UtcNow.ToString("o")
        };

        await _fs.WriteFileAtomicAsync("game_state/player/computed_characteristics.json",
            JsonSerializer.Serialize(output, JsonOpts));
    }

    // ═══════════════════════════════════════════
    //  Private: JSON helpers
    // ═══════════════════════════════════════════

    private static string GetStr(JsonElement el, string prop, string fallback)
    {
        if (el.ValueKind == JsonValueKind.Object && el.TryGetProperty(prop, out var v) &&
            v.ValueKind == JsonValueKind.String)
            return v.GetString() ?? fallback;
        return fallback;
    }

    /// <summary>
    /// Finds a contract-aware array by preferred keys, then falls back to the first array.
    /// </summary>
    private static JsonElement? FindPreferredArray(JsonElement root, params string[] preferredKeys)
    {
        if (root.ValueKind == JsonValueKind.Array) return root;
        if (root.ValueKind == JsonValueKind.Object)
        {
            foreach (var key in preferredKeys)
            {
                if (root.TryGetProperty(key, out var preferred) &&
                    preferred.ValueKind == JsonValueKind.Array)
                    return preferred;
            }

            foreach (var prop in root.EnumerateObject())
            {
                if (prop.Value.ValueKind == JsonValueKind.Array)
                    return prop.Value;
            }
        }
        return null;
    }
}
