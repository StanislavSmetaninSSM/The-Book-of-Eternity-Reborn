using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Core;

public static class GuardianRelationshipRules
{
    public const int MinAttitudeScore = -100;
    public const int MaxAttitudeScore = 100;

    public const string TrustedTier = "trusted";
    public const string AllyTier = "ally";
    public const string NeutralTier = "neutral";
    public const string CompetitiveTier = "competitive";
    public const string RivalTier = "rival";
    public const string EnemyTier = "enemy";

    private const int TrustedThreshold = 75;
    private const int AllyThreshold = 30;
    private const int NeutralFloor = -19;
    private const int CompetitiveFloor = -49;
    private const int RivalFloor = -79;

    public static void EnsureCanonicalNetwork(JsonArray guardians)
    {
        var guardianObjects = guardians.OfType<JsonObject>().ToList();
        foreach (var guardian in guardianObjects)
            EnsureCanonicalGuardianState(guardian, guardianObjects);
    }

    public static void EnsureCanonicalGuardianState(JsonObject guardian, IReadOnlyList<JsonObject> allGuardians)
    {
        NormalizeSocialProfile(guardian);

        var guardianId = GetString(guardian["guardianId"]);
        if (string.IsNullOrWhiteSpace(guardianId))
            return;

        var existingByTarget = new Dictionary<string, JsonObject>(StringComparer.OrdinalIgnoreCase);
        if (guardian["guardianRelationships"] is JsonArray currentRelationships)
        {
            foreach (var entry in currentRelationships.OfType<JsonObject>())
            {
                var targetGuardianId = GetString(entry["targetGuardianId"]);
                if (string.IsNullOrWhiteSpace(targetGuardianId) ||
                    string.Equals(targetGuardianId, guardianId, StringComparison.OrdinalIgnoreCase) ||
                    existingByTarget.ContainsKey(targetGuardianId))
                {
                    continue;
                }

                existingByTarget[targetGuardianId] = entry.DeepClone()!.AsObject();
            }
        }

        var normalized = new JsonArray();
        foreach (var targetGuardian in allGuardians)
        {
            var targetGuardianId = GetString(targetGuardian["guardianId"]);
            if (string.IsNullOrWhiteSpace(targetGuardianId) ||
                string.Equals(targetGuardianId, guardianId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var relationship = existingByTarget.TryGetValue(targetGuardianId, out var existing)
                ? existing
                : new JsonObject();
            NormalizeRelationshipEntry(relationship, guardian, targetGuardian, existing != null);
            normalized.Add(relationship);
        }

        guardian["guardianRelationships"] = normalized;
    }

    public static int GetRelationshipScore(JsonObject guardian, string targetGuardianId)
    {
        if (guardian["guardianRelationships"] is not JsonArray relationships ||
            string.IsNullOrWhiteSpace(targetGuardianId))
        {
            return 0;
        }

        foreach (var entry in relationships.OfType<JsonObject>())
        {
            if (!string.Equals(GetString(entry["targetGuardianId"]), targetGuardianId, StringComparison.OrdinalIgnoreCase))
                continue;

            return ResolveEntryScore(entry);
        }

        return 0;
    }

    public static string GetRelationshipTier(JsonObject guardian, string targetGuardianId) =>
        ResolveAttitudeTier(GetRelationshipScore(guardian, targetGuardianId));

    public static bool IsHostileTo(JsonObject guardian, string targetGuardianId) =>
        GetRelationshipScore(guardian, targetGuardianId) <= -50;

    public static bool HasNonHostileStanding(JsonObject guardian, string targetGuardianId) =>
        GetRelationshipScore(guardian, targetGuardianId) >= NeutralFloor;

    public static bool RequiresBetrayalReason(int score) => score >= AllyThreshold;

    public static bool RequiresBetrayalReason(JsonObject guardian, string targetGuardianId) =>
        RequiresBetrayalReason(GetRelationshipScore(guardian, targetGuardianId));

    public static int ResolvePoliticalTargetWeight(int score) => score switch
    {
        <= -80 => 2,
        <= -50 => 1,
        _ => 0
    };

    public static bool IsWeakPoliticalTarget(int score) =>
        score >= NeutralFloor && score < AllyThreshold;

    public static bool IsPreferredHostileTarget(int score) =>
        ResolvePoliticalTargetWeight(score) > 0;

    public static bool AreCoalitionEligible(JsonObject firstGuardian, JsonObject secondGuardian, string targetGuardianId)
    {
        var firstId = GetString(firstGuardian["guardianId"]);
        var secondId = GetString(secondGuardian["guardianId"]);
        if (string.IsNullOrWhiteSpace(firstId) ||
            string.IsNullOrWhiteSpace(secondId) ||
            string.Equals(firstId, secondId, StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(targetGuardianId))
        {
            return false;
        }

        return IsHostileTo(firstGuardian, targetGuardianId) &&
               IsHostileTo(secondGuardian, targetGuardianId) &&
               HasNonHostileStanding(firstGuardian, secondId) &&
               HasNonHostileStanding(secondGuardian, firstId);
    }

    public static int ResolveCoalitionSupportBonus(
        JsonObject sourceGuardian,
        string targetGuardianId,
        IReadOnlyList<JsonObject> allGuardians,
        JsonObject? trackerRoot)
    {
        var sourceGuardianId = GetString(sourceGuardian["guardianId"]);
        if (string.IsNullOrWhiteSpace(sourceGuardianId) ||
            string.IsNullOrWhiteSpace(targetGuardianId) ||
            trackerRoot?["activeProjects"] is not JsonArray activeProjects)
        {
            return 0;
        }

        return activeProjects.OfType<JsonObject>().Any(entry =>
        {
            var otherId = GetString(entry["guardianId"]);
            if (string.IsNullOrWhiteSpace(otherId) ||
                string.Equals(otherId, sourceGuardianId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(otherId, targetGuardianId, StringComparison.OrdinalIgnoreCase) ||
                entry["project"] is not JsonObject project ||
                !string.Equals(GetString(project["targetGuardianId"]), targetGuardianId, StringComparison.OrdinalIgnoreCase) ||
                !IsPoliticalProjectType(GetString(project["projectType"])))
            {
                return false;
            }

            var other = allGuardians.FirstOrDefault(candidate =>
                string.Equals(GetString(candidate["guardianId"]), otherId, StringComparison.OrdinalIgnoreCase));
            return other != null && AreCoalitionEligible(sourceGuardian, other, targetGuardianId);
        })
            ? 1
            : 0;
    }

    public static int ResolveCorrectionPressureBonus(
        JsonObject sourceGuardian,
        string targetGuardianId,
        IReadOnlyList<JsonObject> allGuardians,
        JsonObject? trackerRoot)
    {
        var sourceGuardianId = GetString(sourceGuardian["guardianId"]);
        if (string.IsNullOrWhiteSpace(sourceGuardianId) || string.IsNullOrWhiteSpace(targetGuardianId))
            return 0;

        var hostilityBonus = GetRelationshipScore(sourceGuardian, targetGuardianId) switch
        {
            <= -80 => 2,
            <= -50 => 1,
            _ => 0
        };

        var coalitionBonus = ResolveCoalitionSupportBonus(sourceGuardian, targetGuardianId, allGuardians, trackerRoot);

        return hostilityBonus + coalitionBonus;
    }

    public static int ResolveCorrectionDefenseSupportBonus(
        IReadOnlyList<JsonObject> guardians,
        string defendedGuardianId,
        string attackerGuardianId,
        JsonObject? trackerRoot)
    {
        if (string.IsNullOrWhiteSpace(defendedGuardianId) ||
            string.IsNullOrWhiteSpace(attackerGuardianId) ||
            trackerRoot?["activeProjects"] is not JsonArray activeProjects)
        {
            return 0;
        }

        var defendedGuardian = guardians.FirstOrDefault(candidate =>
            string.Equals(GetString(candidate["guardianId"]), defendedGuardianId, StringComparison.OrdinalIgnoreCase));
        if (defendedGuardian == null)
            return 0;

        return activeProjects.OfType<JsonObject>().Any(entry =>
        {
            var guardianId = GetString(entry["guardianId"]);
            if (string.IsNullOrWhiteSpace(guardianId) ||
                string.Equals(guardianId, defendedGuardianId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(guardianId, attackerGuardianId, StringComparison.OrdinalIgnoreCase) ||
                entry["project"] is not JsonObject project ||
                !string.Equals(GetString(project["targetGuardianId"]), attackerGuardianId, StringComparison.OrdinalIgnoreCase) ||
                !IsPoliticalProjectType(GetString(project["projectType"])))
            {
                return false;
            }

            var guardian = guardians.FirstOrDefault(candidate =>
                string.Equals(GetString(candidate["guardianId"]), guardianId, StringComparison.OrdinalIgnoreCase));
            return guardian != null &&
                   AreCoalitionEligible(guardian, defendedGuardian, attackerGuardianId);
        })
            ? 1
            : 0;
    }

    public static bool ApplyDirectedDelta(
        JsonObject root,
        string sourceGuardianId,
        string targetGuardianId,
        int scoreDelta,
        string reason,
        string? timestampUtc = null)
    {
        if (scoreDelta == 0 ||
            string.IsNullOrWhiteSpace(sourceGuardianId) ||
            string.IsNullOrWhiteSpace(targetGuardianId) ||
            string.Equals(sourceGuardianId, targetGuardianId, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var guardians = root["guardians"] as JsonArray;
        var guardianObjects = guardians?.OfType<JsonObject>().ToList();
        if (guardianObjects == null || guardianObjects.Count == 0)
            return false;

        foreach (var guardian in guardianObjects)
            EnsureCanonicalGuardianState(guardian, guardianObjects);

        var sourceGuardian = guardianObjects.FirstOrDefault(item =>
            string.Equals(GetString(item["guardianId"]), sourceGuardianId, StringComparison.OrdinalIgnoreCase));
        var targetGuardian = guardianObjects.FirstOrDefault(item =>
            string.Equals(GetString(item["guardianId"]), targetGuardianId, StringComparison.OrdinalIgnoreCase));
        if (sourceGuardian == null || targetGuardian == null)
            return false;

        var changed = ApplyDirectedDeltaInternal(
            sourceGuardian,
            targetGuardian,
            scoreDelta,
            reason,
            timestampUtc ?? DateTime.UtcNow.ToString("o"));

        SyncActiveGuardianMirror(root, sourceGuardianId, sourceGuardian);
        SyncActiveGuardianMirror(root, targetGuardianId, targetGuardian);
        return changed;
    }

    public static bool ApplyMutualDelta(
        JsonObject root,
        string firstGuardianId,
        string secondGuardianId,
        int firstToSecondDelta,
        int secondToFirstDelta,
        string firstReason,
        string secondReason,
        string? timestampUtc = null)
    {
        var changed = ApplyDirectedDelta(root, firstGuardianId, secondGuardianId, firstToSecondDelta, firstReason, timestampUtc);
        changed = ApplyDirectedDelta(root, secondGuardianId, firstGuardianId, secondToFirstDelta, secondReason, timestampUtc) || changed;
        return changed;
    }

    public static string ResolveAttitudeTier(int score)
    {
        var clamped = Math.Clamp(score, MinAttitudeScore, MaxAttitudeScore);
        if (clamped >= TrustedThreshold)
            return TrustedTier;
        if (clamped >= AllyThreshold)
            return AllyTier;
        if (clamped >= NeutralFloor)
            return NeutralTier;
        if (clamped >= CompetitiveFloor)
            return CompetitiveTier;
        if (clamped >= RivalFloor)
            return RivalTier;
        return EnemyTier;
    }

    public static bool IsValidAttitudeTier(string? tier) =>
        string.Equals(tier, TrustedTier, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(tier, AllyTier, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(tier, NeutralTier, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(tier, CompetitiveTier, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(tier, RivalTier, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(tier, EnemyTier, StringComparison.OrdinalIgnoreCase);

    public static int ResolveLegacyScore(string? attitudeOrTier) =>
        attitudeOrTier?.Trim().ToLowerInvariant() switch
        {
            TrustedTier => 85,
            AllyTier => 55,
            "curious" => 10,
            NeutralTier => 0,
            CompetitiveTier => -30,
            RivalTier => -60,
            EnemyTier => -90,
            _ => 0
        };

    private static void NormalizeRelationshipEntry(JsonObject relationship, JsonObject sourceGuardian, JsonObject targetGuardian, bool hasExistingState)
    {
        var targetGuardianId = GetString(targetGuardian["guardianId"]);
        relationship["targetGuardianId"] = targetGuardianId;
        relationship["targetName"] = GuardianManifestation.GetDisplayName(ToJsonElement(targetGuardian));

        var score = hasExistingState
            ? ResolveEntryScore(relationship)
            : ResolveSeedScore(sourceGuardian, targetGuardian);
        score = Math.Clamp(score, MinAttitudeScore, MaxAttitudeScore);
        var tier = ResolveAttitudeTier(score);
        relationship["attitudeScore"] = score;
        relationship["attitudeTier"] = tier;
        relationship["attitude"] = tier;

        if (string.IsNullOrWhiteSpace(GetString(relationship["reason"])))
        {
            relationship["reason"] = hasExistingState && !string.IsNullOrWhiteSpace(GetString(relationship["attitude"]))
                ? "Migrated from legacy inter-guardian standing."
                : "Auto-seeded from structured guardian domain and social profile.";
        }

        if (relationship["lastChangedAt"] != null &&
            !DateTimeOffset.TryParse(GetString(relationship["lastChangedAt"]), out _))
        {
            relationship["lastChangedAt"] = null;
        }

        if (!relationship.ContainsKey("lastChangedAt"))
            relationship["lastChangedAt"] = null;
    }

    private static void NormalizeSocialProfile(JsonObject guardian)
    {
        if (guardian["socialProfile"] is not JsonObject socialProfile)
            return;

        NormalizeFactor(socialProfile, "jealousyFactor");
        NormalizeFactor(socialProfile, "curiosityFactor");
        NormalizeFactor(socialProfile, "competitiveFactor");
        NormalizeFactor(socialProfile, "generosityFactor");
        NormalizeFactor(socialProfile, "isolationistTendency");
    }

    private static void NormalizeFactor(JsonObject socialProfile, string fieldName)
    {
        if (socialProfile[fieldName] == null)
            return;

        socialProfile[fieldName] = Math.Clamp(GetInt(socialProfile[fieldName]), 0, 100);
    }

    private static int ResolveEntryScore(JsonObject relationship)
    {
        if (relationship["attitudeScore"] != null)
            return Math.Clamp(GetInt(relationship["attitudeScore"]), MinAttitudeScore, MaxAttitudeScore);

        var tier = GetString(relationship["attitudeTier"]);
        if (!string.IsNullOrWhiteSpace(tier))
            return ResolveLegacyScore(tier);

        return ResolveLegacyScore(GetString(relationship["attitude"]));
    }

    private static int ResolveSeedScore(JsonObject sourceGuardian, JsonObject targetGuardian)
    {
        var score = 0;
        var generosity = ReadSocialFactor(sourceGuardian, "generosityFactor");
        var curiosity = ReadSocialFactor(sourceGuardian, "curiosityFactor");
        var competitive = ReadSocialFactor(sourceGuardian, "competitiveFactor");
        var jealousy = ReadSocialFactor(sourceGuardian, "jealousyFactor");
        var isolationist = ReadSocialFactor(sourceGuardian, "isolationistTendency");

        if (generosity >= 70) score += 10;
        else if (generosity >= 50) score += 4;

        if (curiosity >= 70) score += 5;
        else if (curiosity >= 50) score += 2;

        if (competitive >= 70) score -= 12;
        else if (competitive >= 50) score -= 5;

        if (jealousy >= 70) score -= 10;
        else if (jealousy >= 50) score -= 4;

        if (isolationist >= 70) score -= 14;
        else if (isolationist >= 50) score -= 6;

        var sourceDomain = GetString(sourceGuardian["domain"]);
        var targetDomain = GetString(targetGuardian["domain"]);
        if (!string.IsNullOrWhiteSpace(sourceDomain) &&
            !string.IsNullOrWhiteSpace(targetDomain) &&
            string.Equals(sourceDomain, targetDomain, StringComparison.OrdinalIgnoreCase))
        {
            score += generosity >= 60 ? 5 : 0;
            score -= competitive >= 50 ? 10 : 3;
        }

        return Math.Clamp(score, -35, 25);
    }

    private static int ReadSocialFactor(JsonObject guardian, string fieldName) =>
        guardian["socialProfile"] is JsonObject socialProfile
            ? Math.Clamp(GetInt(socialProfile[fieldName]), 0, 100)
            : 35;

    private static bool ApplyDirectedDeltaInternal(
        JsonObject sourceGuardian,
        JsonObject targetGuardian,
        int scoreDelta,
        string reason,
        string timestampUtc)
    {
        if (sourceGuardian["guardianRelationships"] is not JsonArray relationships)
            return false;

        var targetGuardianId = GetString(targetGuardian["guardianId"]);
        var relationship = relationships.OfType<JsonObject>().FirstOrDefault(item =>
            string.Equals(GetString(item["targetGuardianId"]), targetGuardianId, StringComparison.OrdinalIgnoreCase));
        if (relationship == null)
            return false;

        var currentScore = ResolveEntryScore(relationship);
        var nextScore = Math.Clamp(currentScore + scoreDelta, MinAttitudeScore, MaxAttitudeScore);
        if (nextScore == currentScore &&
            string.Equals(GetString(relationship["reason"]), reason, StringComparison.Ordinal) &&
            string.Equals(GetString(relationship["lastChangedAt"]), timestampUtc, StringComparison.Ordinal))
        {
            return false;
        }

        relationship["attitudeScore"] = nextScore;
        var nextTier = ResolveAttitudeTier(nextScore);
        relationship["attitudeTier"] = nextTier;
        relationship["attitude"] = nextTier;
        relationship["reason"] = reason;
        relationship["lastChangedAt"] = timestampUtc;
        relationship["targetName"] = GuardianManifestation.GetDisplayName(ToJsonElement(targetGuardian));
        return true;
    }

    private static void SyncActiveGuardianMirror(JsonObject root, string guardianId, JsonObject guardian)
    {
        if (root["activeGuardian"] is not JsonObject activeGuardian)
            return;

        if (string.Equals(GetString(activeGuardian["guardianId"]), guardianId, StringComparison.OrdinalIgnoreCase))
            root["activeGuardian"] = guardian.DeepClone();
    }

    private static bool IsPoliticalProjectType(string? projectType) =>
        string.Equals(projectType, "offensive_intrigue", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(projectType, "counter_rival_operation", StringComparison.OrdinalIgnoreCase);

    private static int GetInt(JsonNode? node)
    {
        if (node is JsonValue value)
        {
            if (value.TryGetValue<int>(out var parsedInt))
                return parsedInt;
            if (value.TryGetValue<long>(out var parsedLong) &&
                parsedLong <= int.MaxValue &&
                parsedLong >= int.MinValue)
            {
                return (int)parsedLong;
            }
            if (value.TryGetValue<string>(out var parsedString) && int.TryParse(parsedString, out var parsedFromString))
                return parsedFromString;
        }

        return 0;
    }

    private static string GetString(JsonNode? node)
    {
        if (node is not JsonValue value)
            return string.Empty;

        try
        {
            return value.GetValue<string>() ?? string.Empty;
        }
        catch
        {
            return node.ToJsonString();
        }
    }

    private static JsonElement ToJsonElement(JsonObject node)
    {
        using var doc = JsonDocument.Parse(node.ToJsonString());
        return doc.RootElement.Clone();
    }
}
