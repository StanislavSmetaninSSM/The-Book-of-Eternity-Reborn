using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using BookOfEternityClient.Core;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Models;
using Microsoft.Extensions.Logging;

namespace BookOfEternityClient.Services;
public partial class ValidationService
{
    private const string InkFeatherActionResultPath = "output/ink_feather_action_result.json";
    private const string PendingInkActionsPath = "game_state/control/pending_ink_actions.json";
    private const string PendingTurnSnapshotManifestPath = "game_state/control/pending_turn_snapshot.json";
    private static readonly Regex InkFeatherActionTagRegex = new(@"\[INK_FEATHER_ACTION:\s*([A-Z_]+)\]", RegexOptions.Compiled);
    private static readonly Regex InkFeatherCostRegex = new(@"(\d+)\s+(?:Чернильных\s+Перьев|Ink\s+Feathers)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex CharacterChronicleEntryPrefixRegex = new(@"^#\[\d+\]\s-\s", RegexOptions.Compiled);
    private static readonly Regex HistoricalEntryTimestampRegex = new(@"^#\[\d+\]\s-\s\d{1,2}\s[^\r\n]+\s\d+\sг\.,\s\d{2}:\d{2}:\s.+$", RegexOptions.Compiled);
    private static readonly Regex LegacyTurnPrefixedEntryRegex = new(@"^#\[\d+\]\.\s.+$", RegexOptions.Compiled);
    private static readonly Regex TimeOfDayRegex = new(@"^(?:[01]\d|2[0-3]):[0-5]\d$", RegexOptions.Compiled);
    private static readonly Regex TempFactionInitialIdRegex = new(@"^temp-faction-.+", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex SnakeCaseIdRegex = new(@"^[a-z0-9]+(?:_[a-z0-9]+)*$", RegexOptions.Compiled);
    private static readonly Regex CyrillicRegex = new(@"\p{IsCyrillic}", RegexOptions.Compiled);
    private static readonly Regex GuardianProvocationTagRegex = new(@"\[GUARDIAN_PROVOCATION(?:\s*:\s*([^\]]+))?\]", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly string[] GuardianProvocationKeywords =
    {
        "оскорб", "насмех", "издева", "унижа", "угрожа", "дерз", "презира", "плюю", "провоц", "вызыва",
        "измыва", "mock", "taunt", "insult", "humiliat", "threaten", "defy", "provoke", "ridicule", "spit"
    };
    private static readonly HashSet<string> ClientSideInkFeatherActions = new(StringComparer.OrdinalIgnoreCase)
    {
        "REVEAL_FATE",
        "REWRITE_FATE"
    };
    private static readonly HashSet<string> GmSideInkFeatherActions = new(StringComparer.OrdinalIgnoreCase)
    {
        "SACRIFICE_TO_CHAOS",
        "ABSORB_FEATHERS",
        "LEARN_SKILL",
        "FATE_SHIELD",
        "SEAL_IN_INK",
        "DONATE_TO_GUARDIAN",
        "CULTIVATE_ENLIGHTENMENT",
        "GUARDIAN_FAVOR",
        GuardianAbodeOfferingState.ActionTag,
        "MEMORY_GATES",
        "SOUL_IMPRINT"
    };
    private static readonly HashSet<string> MortalWorldGmInkFeatherActions = new(StringComparer.OrdinalIgnoreCase)
    {
        "SACRIFICE_TO_CHAOS",
        "ABSORB_FEATHERS",
        "LEARN_SKILL",
        "FATE_SHIELD",
        "SEAL_IN_INK"
    };
    private static readonly HashSet<string> AfterlifeGmInkFeatherActions = new(StringComparer.OrdinalIgnoreCase)
    {
        "DONATE_TO_GUARDIAN",
        "CULTIVATE_ENLIGHTENMENT",
        "GUARDIAN_FAVOR",
        GuardianAbodeOfferingState.ActionTag,
        "MEMORY_GATES",
        "SOUL_IMPRINT"
    };
    private static readonly HashSet<string> AllowedQteOfferOnlyOutputFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        "output/narrative_response.json",
        "output/interface_updates.json",
        "output/debug_logs.json",
        QteSceneService.QteOfferPath
    };
    private static readonly HashSet<string> AllowedQteChoiceGrades = new(StringComparer.Ordinal)
    {
        "success",
        "partial",
        "fail"
    };
    private static readonly HashSet<string> AllowedGuardianQuestOutcomes = new(StringComparer.OrdinalIgnoreCase)
    {
        "success",
        "failure",
        "partial"
    };
    private static readonly HashSet<string> AllowedAchievementCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "combat",
        "exploration",
        "story",
        "social",
        "crafting",
        "meta",
        "death",
        "secret"
    };
    private static readonly HashSet<string> AllowedAchievementRarities = new(StringComparer.OrdinalIgnoreCase)
    {
        "common",
        "uncommon",
        "rare",
        "epic",
        "legendary"
    };
    private static readonly HashSet<string> AllowedAchievementRewardTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "inkFeathers",
        "soulXP",
        "title",
        "none"
    };
    private static readonly HashSet<string> AllowedCodexCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "cosmology",
        "geography",
        "history",
        "cultures",
        "creatures",
        "characters",
        "artifacts",
        "factions",
        "magic",
        "other"
    };
    private static readonly HashSet<string> AllowedQuestStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Active",
        "Completed",
        "Failed",
        "Updated"
    };
    private static readonly HashSet<string> AllowedQuestObjectiveStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Active",
        "Completed",
        "Failed"
    };
    private static readonly HashSet<string> AllowedSoulQuestObjectiveStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Active",
        "Completed",
        "Failed",
        "Pending"
    };
    private static readonly HashSet<string> AllowedSoulQuestStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "active",
        "completed",
        "failed",
        "abandoned"
    };
    private static readonly HashSet<string> AllowedRivalSoulArcScopes = new(StringComparer.OrdinalIgnoreCase)
    {
        "major",
        "minor"
    };
    private static readonly HashSet<string> AllowedRivalSoulArcTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "hostile_hunt",
        "rival_ascension",
        "political_claim",
        "artifact_race",
        "ideological_mission",
        "custom"
    };
    private static readonly HashSet<string> AllowedRivalSoulArcStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "latent",
        "rising",
        "intersecting",
        "resolved",
        "failed"
    };
    private static readonly HashSet<string> AllowedRivalSoulArcSponsorModes = new(StringComparer.OrdinalIgnoreCase)
    {
        "guardianId",
        "eternalPreset"
    };
    private static readonly HashSet<string> AllowedRivalSoulArcResolutionOutcomes = new(StringComparer.OrdinalIgnoreCase)
    {
        "ongoing",
        "player_supported",
        "player_opposed",
        "self_resolved",
        "collapsed",
        "unknown"
    };
    private static readonly HashSet<string> AllowedNpcPersonalQuestStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Active",
        "Completed",
        "Failed",
        "Abandoned"
    };
    private static readonly HashSet<string> AllowedInterNpcRelationshipStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Ally",
        "Friend",
        "Neutral",
        "Rival",
        "Enemy",
        "Subordinate",
        "Superior",
        "Family"
    };
    private static readonly HashSet<string> AllowedNpcCulturalStances = new(StringComparer.OrdinalIgnoreCase)
    {
        "Conformist",
        "Pragmatist",
        "Dissident"
    };
    private static readonly HashSet<string> AllowedNpcFactionMembershipStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Active",
        "Former",
        "Exiled",
        "Undercover",
        "Ally",
        "Enemy"
    };
    private static readonly HashSet<string> AllowedNpcCompletedActivityOutcomes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Success",
        "SuccessWithComplication",
        "Failure"
    };
    private static readonly HashSet<string> AllowedOutdoorBiomes = new(StringComparer.OrdinalIgnoreCase)
    {
        "TemperateForest",
        "Desert",
        "ArcticTundra",
        "Mountains",
        "Swamp",
        "Plains",
        "Urban",
        "Coastal",
        "Unique"
    };
    private static readonly HashSet<string> AllowedIndoorLocationTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Building",
        "Dungeon",
        "CaveSystem",
        "Vehicle",
        "UniqueIndoor"
    };
    private static readonly HashSet<string> AllowedLocationControlTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Military",
        "Economic",
        "Social",
        "Covert"
    };
    private static readonly HashSet<string> AllowedThreatMotivations = new(StringComparer.OrdinalIgnoreCase)
    {
        "Domination",
        "Consumption",
        "Preservation",
        "Corruption",
        "Accumulation",
        "Execution",
        "Custom"
    };
    private static readonly HashSet<string> AllowedThreatMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "Overt",
        "Covert",
        "Deceptive",
        "Opportunistic",
        "Systemic",
        "Custom"
    };
    private static readonly HashSet<string> AllowedThreatPrimaryTargetTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Faction",
        "Location",
        "Resource"
    };
    private static readonly HashSet<string> AllowedThreatPrimaryImpacts = new(StringComparer.OrdinalIgnoreCase)
    {
        "Military",
        "Economic",
        "Social",
        "Covert",
        "Stability",
        "Environment"
    };
    private static readonly HashSet<string> AllowedPassiveSkillTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "KnowledgeBased",
        "CharacteristicBonus",
        "BodyModification",
        "CombatEnhancement",
        "Utility"
    };
    private static readonly HashSet<string> AllowedEquipmentSlots = new(StringComparer.OrdinalIgnoreCase)
    {
        "Head",
        "Chest",
        "Legs",
        "Feet",
        "Hands",
        "Wrists",
        "Neck",
        "Waist",
        "Back",
        "Finger1",
        "Finger2",
        "MainHand",
        "OffHand",
        "Underwear_Top",
        "Underwear_Bottom",
        "Accessory1",
        "Accessory2",
        "Accessory3",
        "Accessory4"
    };
    private static readonly HashSet<string> AllowedItemQualities = new(StringComparer.OrdinalIgnoreCase)
    {
        "Trash",
        "Common",
        "Uncommon",
        "Good",
        "Rare",
        "Epic",
        "Legendary",
        "Unique"
    };
    private static readonly HashSet<string> AllowedGuardianMusingTopics = new(StringComparer.OrdinalIgnoreCase)
    {
        "soul_assessment",
        "domain_insight",
        "guardian_politics",
        "chaos_sea",
        "personal_reflection",
        "quest_planning"
    };
    private static readonly HashSet<string> AllowedGuardianMusingMoods = new(StringComparer.OrdinalIgnoreCase)
    {
        "content",
        "intrigued",
        "concerned",
        "amused",
        "proud",
        "disappointed",
        "wary",
        "nostalgic",
        "determined",
        "melancholic",
        "excited",
        "contemplative",
        "irritated",
        "hopeful"
    };
    private static readonly HashSet<string> AllowedGuardianLoreFragmentCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "personal_history",
        "cosmic_secret",
        "domain_mastery",
        "lost_world",
        "other_guardians",
        "soul_mechanics"
    };
    private static readonly HashSet<int> AllowedGuardianLoreFragmentReputationThresholds = new()
    {
        0,
        50,
        130,
        230
    };
    private static readonly HashSet<string> AllowedGuardianMoodStates = new(StringComparer.OrdinalIgnoreCase)
    {
        "welcoming",
        "contemplative",
        "energized",
        "melancholic",
        "irritated",
        "proud",
        "suspicious",
        "playful",
        "focused",
        "nostalgic"
    };

    private static readonly HashSet<string> AllowedCombatActionCosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "Main",
        "Fast",
        "Free"
    };
    private static readonly HashSet<string> AllowedCombatEffectTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Damage",
        "DamageOverTime",
        "Heal",
        "HealOverTime",
        "Buff",
        "Debuff",
        "Control",
        "DamageReduction"
    };
    private static readonly HashSet<string> AllowedCombatantActiveEffectTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Damage",
        "DamageOverTime",
        "WoundReference",
        "Heal",
        "HealOverTime",
        "Buff",
        "Debuff",
        "Control",
        "DamageReduction"
    };
    private static readonly HashSet<string> AllowedVehicleTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Mount",
        "Vehicle",
        "Summonable"
    };
    private static readonly HashSet<string> AllowedVehicleAvailabilities = new(StringComparer.OrdinalIgnoreCase)
    {
        "Active",
        "Parked",
        "Pocket"
    };
    private static readonly HashSet<string> AllowedWeatherTendencies = new(StringComparer.Ordinal)
    {
        "IMPROVE",
        "WORSEN",
        "JUMP_TO_CLEAR",
        "JUMP_TO_CLOUDY",
        "JUMP_TO_FOGGY",
        "JUMP_TO_LIGHT_RAIN",
        "JUMP_TO_HEAVY_RAIN",
        "JUMP_TO_STORM",
        "JUMP_TO_LIGHT_SNOW",
        "JUMP_TO_HEAVY_SNOW",
        "JUMP_TO_SANDSTORM",
        "JUMP_TO_BLIZZARD",
        "JUMP_TO_SCORCHING_SUN",
        "NO_CHANGE"
    };
    private static readonly Dictionary<string, HashSet<string>> WeatherJumpCommandsByBiome = new(StringComparer.OrdinalIgnoreCase)
    {
        ["TemperateForest"] = new HashSet<string>(StringComparer.Ordinal)
        {
            "JUMP_TO_CLEAR", "JUMP_TO_CLOUDY", "JUMP_TO_FOGGY", "JUMP_TO_LIGHT_RAIN", "JUMP_TO_HEAVY_RAIN", "JUMP_TO_STORM"
        },
        ["Plains"] = new HashSet<string>(StringComparer.Ordinal)
        {
            "JUMP_TO_CLEAR", "JUMP_TO_CLOUDY", "JUMP_TO_FOGGY", "JUMP_TO_LIGHT_RAIN", "JUMP_TO_HEAVY_RAIN", "JUMP_TO_STORM"
        },
        ["Desert"] = new HashSet<string>(StringComparer.Ordinal)
        {
            "JUMP_TO_CLEAR", "JUMP_TO_CLOUDY", "JUMP_TO_SANDSTORM", "JUMP_TO_SCORCHING_SUN"
        },
        ["ArcticTundra"] = new HashSet<string>(StringComparer.Ordinal)
        {
            "JUMP_TO_CLEAR", "JUMP_TO_CLOUDY", "JUMP_TO_LIGHT_SNOW", "JUMP_TO_HEAVY_SNOW", "JUMP_TO_BLIZZARD"
        },
        ["Mountains"] = new HashSet<string>(StringComparer.Ordinal)
        {
            "JUMP_TO_CLEAR", "JUMP_TO_CLOUDY", "JUMP_TO_LIGHT_SNOW", "JUMP_TO_HEAVY_SNOW", "JUMP_TO_BLIZZARD"
        },
        ["Swamp"] = new HashSet<string>(StringComparer.Ordinal)
        {
            "JUMP_TO_CLEAR", "JUMP_TO_CLOUDY", "JUMP_TO_FOGGY", "JUMP_TO_LIGHT_RAIN", "JUMP_TO_HEAVY_RAIN"
        }
    };
    private static readonly JsonSerializerOptions ManifestJsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };
    private static readonly JsonSerializerOptions ManifestHashJsonOpts = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

	    private readonly FileSystemManager _fs;
	    private readonly ILogger<ValidationService> _logger;
	    private HashSet<string>? _knownCanonicalFactionIdsCache;
	    private HashSet<string>? _knownCanonicalFactionNamesCache;

    private sealed class InkFeatherActionContext
    {
        public string ActionTag { get; init; } = string.Empty;
        public int? ParsedCostInFeathers { get; init; }
        public string SessionId { get; init; } = string.Empty;
        public string RequestId { get; init; } = string.Empty;
        public int TurnNumber { get; init; }
    }

    private sealed class ValidationPendingTurnSnapshotManifest
    {
        public string SessionId { get; set; } = string.Empty;
        public string RequestId { get; set; } = string.Empty;
        public int TurnNumber { get; set; }
        public string RequestTimestamp { get; set; } = string.Empty;
        public string PlayerAction { get; set; } = string.Empty;
        public int[]? PreGeneratedDices1d20 { get; set; }
        public JsonObject? GachaBaseResult { get; set; }
        public ProgressionControl? ProgressionControl { get; set; }
        public Dictionary<string, string> Files { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> SnapshotFileHashes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> ClientOwnedValidationHashes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> RollbackBackups { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public List<string> RollbackBaselineFiles { get; set; } = new();
        public string? SourceLabel { get; set; }
        public string ManifestPayloadHash { get; set; } = "";
    }

    private sealed class GuardianSequentialState
    {
        public int? CurrentReputation { get; set; }
        public int CurrentAbodePower { get; set; } = AbodePowerRules.DefaultCurrentPower;
        public int FounderExtraGachaCharges { get; set; }
        public int ChargesUsedThisReturn { get; set; }
        public HashSet<string> AvailableQuestIds { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> ActiveQuestIds { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> QuestDifficultyById { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> ActiveQuestStatusById { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class ForcedGuardianIncarnationContext
    {
        public string GuardianId { get; set; } = "";
        public string ExpectedAbodeId { get; set; } = "";
        public string CurrentAbodeId { get; set; } = "";
        public int CurrentReputation { get; set; }
        public bool IsInCurrentAbode { get; set; }
    }

    private sealed class WorldLocationStateIndex
    {
        public Dictionary<string, HashSet<string>> CoordinateKeysByLocationId { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, HashSet<string>> LinkTargetCoordinateKeysBySourceLocationId { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, HashSet<string>> StorageIdsByLocationId { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, HashSet<string>> ThreatIdsByLocationId { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, HashSet<string>> ThreatIdsWithCurrentActivityByLocationId { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> LocationTypesByLocationId { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> BiomesByLocationId { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class FactionSubEntityStateIndex
    {
        public Dictionary<string, HashSet<string>> BonusIdsByFactionKey { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, HashSet<string>> ProjectIdsByFactionKey { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, HashSet<string>> CustomStateIdsByFactionKey { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    public ValidationService(FileSystemManager fs, ILogger<ValidationService> logger)
    {
        _fs = fs;
        _logger = logger;
    }

    /// <summary>
    /// Run all validations on the current game state. Returns list of issues found.
    /// </summary>
    private List<ValidationIssue> ValidateResponseInternal(JsonElement response)
    {
        var issues = new List<ValidationIssue>();

        // Must have at least a narrative response
        if (!response.TryGetProperty("response", out var resp) ||
            resp.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(resp.GetString()))
        {
            issues.Add(new ValidationIssue(
                "response", IssueSeverity.Warning,
                "GM-ответ не содержит поля 'response' с текстом нарратива"));
        }

        // Validate player status percentages if present
        if (response.TryGetProperty("playerStatus", out var status) &&
            status.ValueKind == JsonValueKind.Object)
        {
            ValidatePercentageField(status, "healthPercentage", issues);
            ValidatePercentageField(status, "energyPercentage", issues);
            ValidatePercentageField(status, "poisePercentage", issues);
        }

        // Validate characteristics are within range if present
        foreach (var charName in Characteristics.All)
        {
            if (response.TryGetProperty(charName, out var charVal))
            {
                if (charVal.ValueKind == JsonValueKind.Number)
                {
                    var val = charVal.GetInt32();
                    if (val < 1 || val > 100)
                    {
                        issues.Add(new ValidationIssue(
                            charName, IssueSeverity.Warning,
                            $"Характеристика '{charName}' = {val} вне диапазона 1-100"));
                    }
                }
            }
        }

        ValidateDialogueOptionsData(response, "response", issues);

        ValidatePlayerContract(response, "response", issues);
        ValidateNpcContract(response, "response", issues);
        ValidateWorldQuestCombatFactionContract(response, "response", issues);
        ValidateMetaMiscContract(response, "response", issues);
        ValidateMathAssistantContractRoot(response, "response", issues);

        return issues;
    }
}

public class ValidationIssue
{
    public string FilePath { get; }
    public IssueSeverity Severity { get; }
    public string Message { get; }
    public IssueCategory Category { get; }
    public string? Code { get; }
    public string? Actor { get; }
    public string? Section { get; }
    public string? Expected { get; }
    public string? Actual { get; }
    public string? RepairHint { get; }

    public ValidationIssue(
        string filePath,
        IssueSeverity severity,
        string message,
        string? code = null,
        string? actor = null,
        string? section = null,
        string? expected = null,
        string? actual = null,
        string? repairHint = null,
        IssueCategory? category = null)
    {
        FilePath = filePath;
        Severity = severity;
        Message = message;
        Category = category ?? InferCategory(filePath, code, section);
        Code = code;
        Actor = actor;
        Section = section;
        Expected = expected;
        Actual = actual;
        RepairHint = repairHint;
    }

    public override string ToString() =>
        $"[{Severity}/{Category}] {FilePath}: {Message}";

    private static IssueCategory InferCategory(string filePath, string? code, string? section)
    {
        var normalizedPath = filePath.Replace('\\', '/');
        var normalizedCode = code ?? string.Empty;
        var normalizedSection = section ?? string.Empty;

        if (normalizedPath.Equals("game_state/control/validation_repair_ready.json", StringComparison.OrdinalIgnoreCase) ||
            normalizedSection.Contains("validation_repair_ready", StringComparison.OrdinalIgnoreCase))
        {
            return IssueCategory.ProtocolViolation;
        }

        if (normalizedPath.Equals("game_state/control/validation_repair_request.json", StringComparison.OrdinalIgnoreCase) ||
            normalizedPath.Equals("game_state/control/terminal_protocol_failure_request.json", StringComparison.OrdinalIgnoreCase))
        {
            return IssueCategory.ProtocolViolation;
        }

        if (normalizedCode.Contains("client_owned", StringComparison.OrdinalIgnoreCase) ||
            normalizedSection.Contains("PendingTurnSnapshot", StringComparison.OrdinalIgnoreCase) ||
            normalizedSection.Contains("WorldSetup", StringComparison.OrdinalIgnoreCase) ||
            normalizedSection.Contains("SystemMods", StringComparison.OrdinalIgnoreCase) ||
            normalizedSection.Contains("ProgressionSchedule", StringComparison.OrdinalIgnoreCase) ||
            normalizedSection.Contains("ControlFiles", StringComparison.OrdinalIgnoreCase) ||
            IsClientOwnedSurfacePath(normalizedPath))
        {
            return IssueCategory.ClientOwnedSurface;
        }

        if (normalizedSection.Contains("Narrative", StringComparison.OrdinalIgnoreCase) ||
            normalizedSection.Contains("Lifecycle", StringComparison.OrdinalIgnoreCase) ||
            normalizedSection.Contains("terminal_ready", StringComparison.OrdinalIgnoreCase) ||
            normalizedSection.Contains("validation_repair_ready", StringComparison.OrdinalIgnoreCase) ||
            normalizedSection.Contains("QTE", StringComparison.OrdinalIgnoreCase) ||
            normalizedSection.Contains("Faction", StringComparison.OrdinalIgnoreCase) ||
            normalizedSection.Contains("Guardian", StringComparison.OrdinalIgnoreCase) ||
            normalizedSection.Contains("Ink", StringComparison.OrdinalIgnoreCase) ||
            normalizedCode.Contains("protocol", StringComparison.OrdinalIgnoreCase) ||
            normalizedCode.Contains("accepted_turn", StringComparison.OrdinalIgnoreCase) ||
            normalizedCode.Contains("trigger", StringComparison.OrdinalIgnoreCase) ||
            normalizedCode.Contains("realm_segregation", StringComparison.OrdinalIgnoreCase))
        {
            return IssueCategory.ProtocolViolation;
        }

        return IssueCategory.StateConsistency;
    }

    private static bool IsClientOwnedSurfacePath(string normalizedPath)
    {
        return normalizedPath.Equals("game_state/control/pending_turn_snapshot.json", StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.Equals(PendingTurnSnapshotAuthority.AuthorityPath, StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.StartsWith("game_state/control/pending_turn_snapshot/", StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.StartsWith(QteSceneService.QteNormalizerBackupDirectory + "/", StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.Equals("game_state/control/validation_repair_request.json", StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.Equals("game_state/control/terminal_protocol_failure_request.json", StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.Equals("game_state/control/progression_schedule.json", StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.Equals("game_state/control/incarnation_world_setup.json", StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.Equals(ScenarioCoreService.ManifestPath, StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.Equals(AfterlifeArchiveCandidateService.ManifestPath, StringComparison.OrdinalIgnoreCase) ||
               AfterlifeContractRegistry.IsKnownClientOwnedSurface(normalizedPath) ||
               normalizedPath.Equals(GuardianAbodeOfferingState.PendingRequestPath, StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.Equals(GuardianTradeRequestState.PendingRequestPath, StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.Equals(PlayerGuardianFoundationState.PendingRequestPath, StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.Equals(ShiningCoreActionRequestState.PendingActionsRequestPath, StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.Equals(ShiningTradeRequestState.PendingRequestsPath, StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.Equals(ShiningFactionRequestState.PendingFoundingsRequestPath, StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.Equals(ShiningFactionRequestState.PendingRealignmentsRequestPath, StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.Equals(ShiningFactionRequestState.PendingLeadershipTransitionsRequestPath, StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.Equals(GuardianAbodeResidentRequestState.PendingResidentsRequestPath, StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.Equals(GuardianAbodeResidentRequestState.PendingInteractionsRequestPath, StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.Equals(GuardianAbodeResidentRequestState.PendingTransfersRequestPath, StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.Equals(ActorSocialInteractionRequestState.PendingGuardianRequestPath, StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.Equals(AfterlifeArchiveActionState.ConsultationRequestPath, StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.Equals(AfterlifeArchiveActionState.ProjectFuelRequestPath, StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.Equals(SystemGuardianLibraryService.AttractionRequestPath, StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.Equals("game_state/control/afterlife_return_guard.json", StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.Equals(GuardianCorrectionService.StatePath, StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.Equals("game_state/control/gm_cli_window_binding.json", StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.Equals("game_state/control/gm_bridge_status.json", StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.Equals("game_state/history/chat_log.json", StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.StartsWith("stories/", StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.Equals("game_state/core/system_mods.json", StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.Equals("lore/current_world/world_directives.json", StringComparison.OrdinalIgnoreCase);
    }
}

public enum IssueSeverity
{
    Info,
    Warning,
    Error
}

public enum IssueCategory
{
    ProtocolViolation,
    StateConsistency,
    ClientOwnedSurface
}
