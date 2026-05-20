using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static class SarefMainStoryState
{
    public const string StatePath = "game_state/meta/main_story_saref_state.json";
    public const int SchemaVersion = 1;

    public const string RevealStageUnknown = "unknown";
    public const string RevealStageShadow = "shadow";
    public const string RevealStageNameRevealed = "name_revealed";
    public const string RevealStageWingsRevealed = "wings_revealed";
    public const string RevealStageInfiltrationActive = "infiltration_active";
    public const string RevealStageConfrontationAvailable = "confrontation_available";
    public const string RevealStageCompleted = "completed";

    public const string CategoryIdentity = "identity";
    public const string CategoryMethod = "method";
    public const string CategoryFaction = "faction";
    public const string CategoryPath = "path";

    public const string QuestStateLatent = "latent";
    public const string QuestStateRecognized = "recognized";
    public const string QuestStateActive = "active";
    public const string QuestStateReadyToTurnIn = "ready_to_turn_in";
    public const string QuestStateCompleted = "completed";

    public static readonly HashSet<string> RevealStages = new(StringComparer.OrdinalIgnoreCase)
    {
        RevealStageUnknown,
        RevealStageShadow,
        RevealStageNameRevealed,
        RevealStageWingsRevealed,
        RevealStageInfiltrationActive,
        RevealStageConfrontationAvailable,
        RevealStageCompleted
    };

    public static readonly HashSet<string> RevelationCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        CategoryIdentity,
        CategoryMethod,
        CategoryFaction,
        CategoryPath,
        "oath_break",
        "war_doctrine",
        "structural_weakness",
        "exile_survival",
        "false_light_cut"
    };

    public static readonly HashSet<string> QuestProgressStates = new(StringComparer.OrdinalIgnoreCase)
    {
        QuestStateLatent,
        QuestStateRecognized,
        QuestStateActive,
        QuestStateReadyToTurnIn,
        QuestStateCompleted
    };

    public static readonly HashSet<string> LatentTraceStates = new(StringComparer.OrdinalIgnoreCase)
    {
        QuestStateLatent,
        QuestStateRecognized
    };

    public static readonly HashSet<string> MandatoryWingsCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        CategoryIdentity,
        CategoryMethod,
        CategoryFaction,
        CategoryPath
    };

    public static readonly HashSet<string> AdvantageStates = new(StringComparer.OrdinalIgnoreCase)
    {
        "available",
        "spent",
        "passive",
        "disabled",
        "suppressed"
    };

    public static readonly HashSet<string> FactionVisibilityStates = new(StringComparer.OrdinalIgnoreCase)
    {
        "hidden",
        "rumored",
        "revealed"
    };

    public static readonly HashSet<string> PersonalBondStates = new(StringComparer.OrdinalIgnoreCase)
    {
        "none",
        "intrigued",
        "favored",
        "intimate_oath",
        "rejected",
        "hostile",
        "adversarial_romantic"
    };

    public static readonly HashSet<string> PlayerOathStates = new(StringComparer.OrdinalIgnoreCase)
    {
        "none",
        "offered",
        "oathbound",
        "strained",
        "broken",
        "escaped"
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static JsonObject CreateDefaultRoot() =>
        new()
        {
            ["schemaVersion"] = SchemaVersion,
            ["revealStage"] = RevealStageUnknown,
            ["guardianQuestlines"] = new JsonArray(),
            ["latentTraces"] = new JsonArray(),
            ["sarefRevelations"] = new JsonArray(),
            ["sarefAdvantages"] = new JsonArray(),
            ["wingsInfiltration"] = null,
            ["factionLinks"] = new JsonObject
            {
                ["wingsFactionId"] = null,
                ["visibility"] = "hidden"
            },
            ["finalConfrontation"] = null,
            ["defeatOutcomes"] = new JsonArray(),
            ["endings"] = new JsonArray(),
            ["playerOathState"] = null,
            ["sarefPersonalBond"] = null
        };

    public static string SerializeDefaultRoot() =>
        CreateDefaultRoot().ToJsonString(JsonOptions);
}
