using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Core;

internal static class AbodePowerRules
{
    public const int MinPower = 0;
    public const int MaxPower = 100;
    public const int DefaultCurrentPower = 35;

    public static int ClampCurrentPower(int value) => Math.Clamp(value, MinPower, MaxPower);

    public static int GetCurrentPower(JsonElement guardian)
    {
        if (guardian.TryGetProperty("abodePower", out var abodePower) &&
            abodePower.ValueKind == JsonValueKind.Object &&
            abodePower.TryGetProperty("currentPower", out var currentPower) &&
            currentPower.ValueKind == JsonValueKind.Number &&
            currentPower.TryGetInt32(out var parsed))
        {
            return ClampCurrentPower(parsed);
        }

        return DefaultCurrentPower;
    }

    public static int GetCurrentPower(JsonObject guardian)
    {
        if (guardian["abodePower"] is JsonObject abodePower &&
            abodePower["currentPower"] is JsonNode currentPower &&
            TryGetInt(currentPower, out var parsed))
        {
            return ClampCurrentPower(parsed);
        }

        return DefaultCurrentPower;
    }

    public static string GetTierLabel(int currentPower) => ClampCurrentPower(currentPower) switch
    {
        <= 19 => "Угасающая",
        <= 39 => "Хрупкая",
        <= 59 => "Стабильная",
        <= 79 => "Могущественная",
        _ => "Сияющая"
    };

    public static string GetTierColor(int currentPower) => ClampCurrentPower(currentPower) switch
    {
        <= 19 => "grey",
        <= 39 => "yellow",
        <= 59 => "cyan",
        <= 79 => "green",
        _ => "gold1"
    };

    public static int GetTradeSlotCount(int currentPower) => ClampCurrentPower(currentPower) switch
    {
        <= 19 => 4,
        <= 39 => 5,
        <= 59 => 6,
        <= 79 => 7,
        _ => 8
    };

    public static int GetGuardianQuestCap(int currentPower) => ClampCurrentPower(currentPower) switch
    {
        <= 19 => 2,
        <= 59 => 3,
        _ => 4
    };

    public static string GetGuardianQuestDifficultyCeiling(int currentPower) => ClampCurrentPower(currentPower) switch
    {
        <= 39 => "normal",
        <= 79 => "hard",
        _ => "epic"
    };

    public static bool IsGuardianQuestDifficultyAllowed(int currentPower, string? difficultyTier) =>
        GetGuardianQuestDifficultyRank(difficultyTier) <= GetGuardianQuestDifficultyRank(GetGuardianQuestDifficultyCeiling(currentPower));

    public static int GetBonusGachaCharges(int currentPower) => ClampCurrentPower(currentPower) switch
    {
        <= 39 => 0,
        <= 79 => 1,
        _ => 2
    };

    public static int GetGuardianRarityCeilingBonusSteps(int currentPower) => ClampCurrentPower(currentPower) switch
    {
        <= 59 => 0,
        _ => 1
    };

    public static int GetNextLifeCorrectionBudgetPoints(int currentPower) => ClampCurrentPower(currentPower) switch
    {
        <= 19 => 0,
        <= 39 => 1,
        <= 59 => 2,
        <= 79 => 3,
        _ => 4
    };

    public static int GetRivalArcDefenseClues(int currentPower) => ClampCurrentPower(currentPower) switch
    {
        <= 19 => 0,
        <= 59 => 1,
        <= 79 => 2,
        _ => 3
    };

    public static int GetRivalArcClarityTier(int currentPower) => ClampCurrentPower(currentPower) switch
    {
        <= 39 => 0,
        <= 59 => 1,
        <= 79 => 2,
        _ => 3
    };

    public static bool GetRivalArcCounterQuestAccess(int currentPower) => ClampCurrentPower(currentPower) >= 60;

    public static int GetRivalArcWarningTier(int currentPower) => ClampCurrentPower(currentPower) >= 80 ? 1 : 0;

    public static string GetRivalArcOffenseCap(int currentPower) => ClampCurrentPower(currentPower) switch
    {
        <= 19 => "no_formal_hostile_arc_sponsorship",
        <= 39 => "background_pressure_only",
        <= 59 => "one_minor_hostile_arc",
        <= 79 => "one_major_or_direct_minor",
        _ => "one_major_with_early_signal_privilege"
    };

    public static int GetCorrectionClaimPowerBand(int currentPower) => ClampCurrentPower(currentPower) switch
    {
        <= 19 => 0,
        <= 39 => 1,
        <= 59 => 2,
        <= 79 => 3,
        _ => 4
    };

    public static int GetCorrectionSeverityBudgetCost(string? severity) =>
        (severity ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "strong" => 3,
            "medium" => 2,
            _ => 1
        };

    public static int GetCorrectionSeverityAbodePowerCost(string? severity) =>
        (severity ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "strong" => 20,
            "medium" => 12,
            _ => 5
        };

    public static int ResolveGuardianQuestBasePowerDelta(string? difficultyTier, string? outcome)
    {
        var difficulty = NormalizeGuardianQuestDifficulty(difficultyTier);
        var normalizedOutcome = (outcome ?? string.Empty).Trim().ToLowerInvariant();
        return normalizedOutcome switch
        {
            "success" => difficulty switch
            {
                "easy" => 2,
                "hard" => 5,
                "epic" => 7,
                _ => 3
            },
            "failure" => difficulty switch
            {
                "easy" => -1,
                "hard" => -4,
                "epic" => -6,
                _ => -2
            },
            "partial" => difficulty switch
            {
                "easy" => 1,
                "hard" => 3,
                "epic" => 4,
                _ => 2
            },
            _ => 0
        };
    }

    public static int ResolveGuardianQuestBonusPowerDelta(int baseDelta, bool supportsCurrentProject, bool defendsAgainstRivalPressure)
    {
        if (baseDelta <= 0)
            return 0;

        var bonus = 0;
        if (supportsCurrentProject)
            bonus += Math.Max(1, (int)Math.Ceiling(baseDelta / 3.0));
        if (defendsAgainstRivalPressure)
            bonus += 1;
        return bonus;
    }

    public static int ResolveGuardianProjectAssistPowerDelta(string? classification) =>
        (classification ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "minor assist" or "minor defensive help" => 1,
            "meaningful assist" or "meaningful protection" => 2,
            "major breakthrough" or "major defensive breakthrough" => 3,
            _ => 0
        };

    public static int ResolveGuardianProjectSabotagePowerDelta(string? classification) =>
        (classification ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "minor interference" => -1,
            "major sabotage" => -2,
            "grand strike" => -4,
            _ => 0
        };

    public static int ResolvePowerGainForInkFeatherOffering(int inkFeathersOffered)
    {
        if (inkFeathersOffered <= 0)
            return 0;

        return Math.Clamp(inkFeathersOffered / 50, 0, 3);
    }

    public static int ResolvePowerGainForSoulRelicOffering(string? relicRarity) =>
        (relicRarity ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "legendary" or "mythic" or "divine" => 4,
            "epic" => 3,
            "rare" => 2,
            "common" or "uncommon" => 1,
            _ => 1
        };

    public static int ResolvePowerGainForArchiveRarity(string? rarity) =>
        (rarity ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "common" => 1,
            "uncommon" => 1,
            "rare" => 2,
            "epic" or "legendary" or "unique" => 3,
            _ => 1
        };

    public static JsonObject EnsureCanonicalState(JsonObject guardian)
    {
        var currentPower = GetCurrentPower(guardian);
        var abodePower = guardian["abodePower"] as JsonObject ?? new JsonObject();
        abodePower["currentPower"] = currentPower;
        abodePower["tier"] = GetTierLabel(currentPower);
        abodePower["lastUpdatedAt"] ??= DateTime.UtcNow.ToString("o");
        abodePower["history"] ??= new JsonArray();
        guardian["abodePower"] = abodePower;
        return abodePower;
    }

    public static string NormalizeGuardianQuestDifficulty(string? difficultyTier) =>
        (difficultyTier ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "easy" or "легкий" or "лёгкий" => "easy",
            "hard" or "тяжелый" or "тяжёлый" => "hard",
            "epic" or "эпический" => "epic",
            _ => "normal"
        };

    public static int GetGuardianQuestDifficultyRank(string? difficultyTier) =>
        NormalizeGuardianQuestDifficulty(difficultyTier) switch
        {
            "easy" => 1,
            "hard" => 3,
            "epic" => 4,
            _ => 2
        };

    private static bool TryGetInt(JsonNode node, out int value)
    {
        value = 0;
        if (node is not JsonValue jsonValue)
            return false;

        try
        {
            value = jsonValue.GetValue<int>();
            return true;
        }
        catch
        {
            return false;
        }
    }

}
