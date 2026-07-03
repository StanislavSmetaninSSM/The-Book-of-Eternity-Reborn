using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal sealed class AfterlifeSpiritualConflictTurnPreviewService
{
    internal const string Source = "client_pre_turn_afterlife_spiritual_conflict_preview_v1";

    private readonly FileSystemManager _fs;

    public AfterlifeSpiritualConflictTurnPreviewService(FileSystemManager fs)
    {
        _fs = fs;
    }

    public async Task<JsonObject?> BuildAsync(int turnNumber, int[]? preGeneratedDices1d20, string? currentRealm)
    {
        if (!IsAfterlifeRealm(currentRealm))
            return null;

        var conflictRoot = await ReadJsonObjectAsync(AfterlifeSpiritualConflictState.StatePath);
        if (conflictRoot?["activeConflict"] is not JsonObject activeConflict)
            return null;

        var soulRoot = await ReadJsonObjectAsync("game_state/meta/soul_state.json");
        var profilesRoot = await ReadJsonObjectAsync(AfterlifeEntityProfileState.StatePath);
        var settingsRoot = await ReadJsonObjectAsync(AfterlifeSpiritualConflictState.DifficultySettingsPath);

        var playerTiers = ReadTierMap(soulRoot?[AfterlifeSpiritualConflictState.SoulStateProfileProperty]?["artTiers"] as JsonObject);
        var oppositionLead = activeConflict["oppositionSide"]?["leadContestant"] as JsonObject;
        var oppositionActorKey = BuildActorKey(oppositionLead);
        var oppositionProfileTiers = ReadProfileTierMap(profilesRoot, oppositionActorKey);
        var oppositionSnapshotTiers = ReadTierMap(oppositionLead?["actorArtTierSnapshot"] as JsonObject);

        var conflictPosition = AfterlifeSpiritualConflictState.GetNodeString(activeConflict["conflictPosition"]) ?? "contested";
        var difficulty = AfterlifeSpiritualConflictState.GetNodeString(settingsRoot?["difficulty"]);
        AfterlifeSpiritualConflictState.DifficultyDefinitions.TryGetValue(
            string.IsNullOrWhiteSpace(difficulty) ? "normal" : difficulty,
            out var difficultyDefinition);

        var preview = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["source"] = Source,
            ["turnNumber"] = turnNumber,
            ["conflictId"] = AfterlifeSpiritualConflictState.GetNodeString(activeConflict["conflictId"]) ??
                             AfterlifeSpiritualConflictState.GetNodeString(activeConflict["id"]) ??
                             "unknown",
            ["realm"] = AfterlifeSpiritualConflictState.GetNodeString(activeConflict["realm"]) ?? currentRealm ?? "unknown",
            ["sideModel"] = AfterlifeSpiritualConflictState.GetNodeString(activeConflict["sideModel"]) ?? "unknown",
            ["conflictPosition"] = conflictPosition,
            ["resolutionState"] = AfterlifeSpiritualConflictState.GetNodeString(activeConflict["resolutionState"]) ?? "active",
            ["playerActionEconomy"] = CloneObject(activeConflict["actionEconomy"]?["player"] as JsonObject),
            ["oppositionActionEconomy"] = CloneObject(activeConflict["actionEconomy"]?["opposition"] as JsonObject),
            ["playerActionCosts"] = BuildActionCosts(playerTiers, null, "game_state/meta/soul_state.json.afterlifeCombatProfile.artTiers"),
            ["opposition"] = new JsonObject
            {
                ["actorType"] = AfterlifeSpiritualConflictState.GetNodeString(oppositionLead?["actorType"]) ?? "unknown",
                ["actorId"] = AfterlifeSpiritualConflictState.GetNodeString(oppositionLead?["actorId"]) ??
                              AfterlifeSpiritualConflictState.GetNodeString(oppositionLead?["actorRef"]) ??
                              AfterlifeSpiritualConflictState.GetNodeString(oppositionLead?["id"]) ??
                              "unknown",
                ["displayName"] = AfterlifeSpiritualConflictState.GetNodeString(oppositionLead?["displayName"]) ??
                                  AfterlifeSpiritualConflictState.GetNodeString(oppositionLead?["name"]) ??
                                  "Противник",
                ["actionCosts"] = BuildActionCosts(
                    oppositionProfileTiers,
                    oppositionSnapshotTiers,
                    "game_state/meta/afterlife_entity_profiles.json.standardArts")
            },
            ["dicePreview"] = BuildDicePreview(preGeneratedDices1d20, conflictPosition, difficultyDefinition),
            ["authoringReminders"] = new JsonArray
            {
                "Copy actionCostAudit artTier/baseCost/minCost/effectiveCost from this preview for current/new exchanges; do not guess spiritual art tiers.",
                "Use dicePreview.withMandatoryModifiers.outcomeBand as the expected outcomeBand when the first opposed dice pair and listed mandatory modifiers are used.",
                "If you choose different valid modifiers, recompute playerTotal/oppositionTotal/margin before writing outcomeBand; validators recompute this deterministically.",
                "Do not rewrite historical exchangeLog entries from earlier turns to fit the current preGeneratedDices1d20 pool."
            }
        };

        return preview;
    }

    private async Task<JsonObject?> ReadJsonObjectAsync(string relativePath)
    {
        var json = await _fs.ReadFileAsync(relativePath);
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonNode.Parse(json) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool IsAfterlifeRealm(string? currentRealm) =>
        string.Equals(currentRealm, "Chaos Sea", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(currentRealm, "Море Хаоса", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(currentRealm, "Shining Abode", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(currentRealm, "Сияющая Обитель", StringComparison.OrdinalIgnoreCase);

    private static Dictionary<string, int> ReadTierMap(JsonObject? root)
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (root == null)
            return result;

        foreach (var property in root)
        {
            if (!AfterlifeEntityProfileState.StandardArtIds.Contains(property.Key))
                continue;

            if (TryGetInt(property.Value, out var tier))
                result[property.Key] = Math.Clamp(tier, 0, AfterlifeEntityProfileState.MaxProfileTier);
        }

        return result;
    }

    private static Dictionary<string, int> ReadProfileTierMap(JsonObject? profilesRoot, string? actorKey)
    {
        if (string.IsNullOrWhiteSpace(actorKey) ||
            profilesRoot?[AfterlifeEntityProfileState.ProfilesProperty] is not JsonArray profiles)
        {
            return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }

        foreach (var profile in profiles.OfType<JsonObject>())
        {
            if (string.Equals(AfterlifeEntityProfileState.BuildIdentityKey(profile), actorKey, StringComparison.OrdinalIgnoreCase))
                return ReadTierMap(profile["standardArts"] as JsonObject);
        }

        return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    }

    private static JsonObject BuildActionCosts(
        IReadOnlyDictionary<string, int> primaryTiers,
        IReadOnlyDictionary<string, int>? fallbackTiers,
        string primaryAuthoritySource)
    {
        var costs = new JsonObject();
        foreach (var (operationType, definition) in AfterlifeActionCostRules.Definitions.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            var artTier = 0;
            var authoritySource = "default_zero";
            if (primaryTiers.TryGetValue(operationType, out var primaryTier))
            {
                artTier = primaryTier;
                authoritySource = primaryAuthoritySource;
            }
            else if (fallbackTiers != null && fallbackTiers.TryGetValue(operationType, out var fallbackTier))
            {
                artTier = fallbackTier;
                authoritySource = "activeConflict.leadContestant.actorArtTierSnapshot";
            }

            costs[operationType] = new JsonObject
            {
                ["operationType"] = operationType,
                ["baseCost"] = definition.BaseCost,
                ["minCost"] = definition.MinCost,
                ["artTier"] = artTier,
                ["effectiveCost"] = AfterlifeActionCostRules.ResolveStandardEffectiveCost(definition, artTier),
                ["authoritySource"] = authoritySource
            };
        }

        return costs;
    }

    private static JsonObject? BuildDicePreview(
        int[]? dice,
        string conflictPosition,
        AfterlifeSpiritualConflictState.DifficultyDefinition? difficultyDefinition)
    {
        if (dice is not { Length: >= 2 })
            return null;

        var playerDie = dice[0];
        var oppositionDie = dice[1];
        var mandatoryModifiers = new JsonArray();
        var playerModifier = 0;
        var oppositionModifier = 0;

        var positionModifier = ResolvePositionModifier(conflictPosition);
        if (positionModifier != null)
        {
            var (side, value) = positionModifier.Value;
            if (string.Equals(side, "player", StringComparison.OrdinalIgnoreCase))
                playerModifier += value;
            else
                oppositionModifier += value;

            mandatoryModifiers.Add(new JsonObject
            {
                ["modifierType"] = "conflict_position",
                ["source"] = "conflictPosition",
                ["side"] = side,
                ["position"] = conflictPosition,
                ["value"] = value
            });
        }

        if (difficultyDefinition is { OppositionDiceModifier: > 0 })
        {
            oppositionModifier += difficultyDefinition.OppositionDiceModifier;
            mandatoryModifiers.Add(new JsonObject
            {
                ["modifierType"] = "game_difficulty",
                ["source"] = "game_state/core/game_settings.json.difficulty",
                ["side"] = "opposition",
                ["difficulty"] = difficultyDefinition.Difficulty,
                ["value"] = difficultyDefinition.OppositionDiceModifier
            });
        }

        return new JsonObject
        {
            ["diceSource"] = "input/turn_request.json.preGeneratedDices1d20",
            ["firstOpposedPair"] = new JsonObject
            {
                ["player"] = new JsonObject
                {
                    ["sourceIndex"] = 0,
                    ["sides"] = 20,
                    ["value"] = playerDie
                },
                ["opposition"] = new JsonObject
                {
                    ["sourceIndex"] = 1,
                    ["sides"] = 20,
                    ["value"] = oppositionDie
                },
                ["raw"] = BuildOutcomePreview(playerDie, oppositionDie, 0, 0),
                ["mandatoryModifiers"] = mandatoryModifiers,
                ["withMandatoryModifiers"] = BuildOutcomePreview(playerDie, oppositionDie, playerModifier, oppositionModifier)
            },
            ["outcomeBands"] = new JsonArray
            {
                "margin >= 8 => decisive_player_success",
                "margin 3..7 => player_success",
                "margin -2..2 => mixed_or_no_effect",
                "margin -7..-3 => opposition_success",
                "margin <= -8 => decisive_opposition_success",
                "natural 20/1 may normalize only one step to player_success/opposition_success unless opposed criticals cancel"
            }
        };
    }

    private static (string Side, int Value)? ResolvePositionModifier(string? conflictPosition) =>
        conflictPosition?.Trim().ToLowerInvariant() switch
        {
            "player_advantaged" => ("player", 2),
            "player_dominant" => ("player", 4),
            "opposition_advantaged" => ("opposition", 2),
            "opposition_dominant" => ("opposition", 4),
            _ => null
        };

    private static JsonObject BuildOutcomePreview(int playerDie, int oppositionDie, int playerModifier, int oppositionModifier)
    {
        var playerTotal = playerDie + playerModifier;
        var oppositionTotal = oppositionDie + oppositionModifier;
        var margin = playerTotal - oppositionTotal;
        var marginOutcomeBand = ExpectedOutcomeBand(margin);
        var outcomeBand = ExpectedOutcomeBand(margin, playerDie, oppositionDie);
        var result = new JsonObject
        {
            ["playerTotal"] = playerTotal,
            ["oppositionTotal"] = oppositionTotal,
            ["margin"] = margin,
            ["marginOutcomeBand"] = marginOutcomeBand,
            ["outcomeBand"] = outcomeBand
        };

        if (!string.Equals(marginOutcomeBand, outcomeBand, StringComparison.Ordinal))
        {
            result["criticalAdjustment"] = new JsonObject
            {
                ["playerNaturalRoll"] = playerDie,
                ["oppositionNaturalRoll"] = oppositionDie,
                ["normalizedOutcomeBand"] = outcomeBand
            };
        }

        return result;
    }

    private static string ExpectedOutcomeBand(int margin, int? playerDie = null, int? oppositionDie = null)
    {
        var marginBand = margin switch
        {
            >= 8 => "decisive_player_success",
            >= 3 => "player_success",
            >= -2 => "mixed_or_no_effect",
            >= -7 => "opposition_success",
            _ => "decisive_opposition_success"
        };

        if (playerDie == null || oppositionDie == null)
            return marginBand;

        var playerCriticalSuccess = (playerDie.Value == 20 ? 1 : 0) + (oppositionDie.Value == 1 ? 1 : 0);
        var playerCriticalFailure = (playerDie.Value == 1 ? 1 : 0) + (oppositionDie.Value == 20 ? 1 : 0);

        if (playerCriticalSuccess > playerCriticalFailure)
            return OutcomeBandRank(marginBand) < 1 ? "player_success" : marginBand;

        if (playerCriticalFailure > playerCriticalSuccess)
            return OutcomeBandRank(marginBand) > -1 ? "opposition_success" : marginBand;

        return marginBand;
    }

    private static int OutcomeBandRank(string band) =>
        band switch
        {
            "decisive_player_success" => 2,
            "player_success" => 1,
            "mixed_or_no_effect" => 0,
            "opposition_success" => -1,
            "decisive_opposition_success" => -2,
            _ => 0
        };

    private static string? BuildActorKey(JsonObject? actor)
    {
        var actorType = AfterlifeSpiritualConflictState.GetNodeString(actor?["actorType"]);
        var actorId = AfterlifeSpiritualConflictState.GetNodeString(actor?["actorId"]) ??
                      AfterlifeSpiritualConflictState.GetNodeString(actor?["actorRef"]) ??
                      AfterlifeSpiritualConflictState.GetNodeString(actor?["id"]);
        return string.IsNullOrWhiteSpace(actorType) || string.IsNullOrWhiteSpace(actorId)
            ? null
            : $"{actorType.Trim()}:{actorId.Trim()}";
    }

    private static JsonObject? CloneObject(JsonObject? source) =>
        source?.DeepClone() as JsonObject;

    private static bool TryGetInt(JsonNode? node, out int value)
    {
        if (node is JsonValue jsonValue && jsonValue.TryGetValue<int>(out value))
            return true;

        value = 0;
        return false;
    }
}
