using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging;

namespace BookOfEternityClient.Services;

/// <summary>
/// Loads built-in and user-authored global system guardian presets.
/// These presets are reusable generation dossiers. Fresh New Game can also
/// derive a client-owned canonical seed from them so the GM does not have to
/// repair the same technical guardian skeleton before narrating the first scene.
/// </summary>
public sealed class SystemGuardianLibraryService
{
    public const string RootDirectoryName = "system_guardians";
    public const string BuiltInDirectoryName = "built_in";
    public const string UserDirectoryName = "user";
    public const string AttractionRequestPath = "game_state/control/system_guardian_attraction.json";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver()
    };

    private readonly FileSystemManager _fs;
    private readonly ILogger<SystemGuardianLibraryService> _logger;

    internal sealed record AttractionRequestReadState(
        bool FilePresent,
        bool IsMalformed,
        SystemGuardianAttractionRequest? Request);

    public sealed class GuardianPresetManifest
    {
        [JsonPropertyName("presetId")]
        public string PresetId { get; set; } = "";

        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; } = "";

        [JsonPropertyName("summary")]
        public string Summary { get; set; } = "";

        [JsonPropertyName("alwaysAvailable")]
        public bool AlwaysAvailable { get; set; } = true;

        [JsonPropertyName("category")]
        public string Category { get; set; } = "system_guardian";

        [JsonPropertyName("identity")]
        public GuardianPresetIdentity Identity { get; set; } = new();

        [JsonPropertyName("nameVariants")]
        public GuardianPresetNameVariants NameVariants { get; set; } = new();

        [JsonPropertyName("manifestationDefaults")]
        public GuardianPresetManifestationDefaults ManifestationDefaults { get; set; } = new();

        [JsonPropertyName("abode")]
        public GuardianPresetAbode Abode { get; set; } = new();

        [JsonPropertyName("generationRules")]
        public GuardianPresetGenerationRules GenerationRules { get; set; } = new();

        [JsonPropertyName("searchAttraction")]
        public GuardianPresetSearchAttraction SearchAttraction { get; set; } = new();

        [JsonPropertyName("authoring")]
        public GuardianPresetAuthoring Authoring { get; set; } = new();
    }

    public sealed class GuardianPresetIdentity
    {
        [JsonPropertyName("domain")]
        public string Domain { get; set; } = "";

        [JsonPropertyName("archetype")]
        public string Archetype { get; set; } = "";

        [JsonPropertyName("tone")]
        public string Tone { get; set; } = "";

        [JsonPropertyName("coreValues")]
        public List<string> CoreValues { get; set; } = new();
    }

    public sealed class GuardianPresetNameVariants
    {
        [JsonPropertyName("default")]
        public string Default { get; set; } = "";

        [JsonPropertyName("feminine")]
        public string? Feminine { get; set; }

        [JsonPropertyName("masculine")]
        public string? Masculine { get; set; }

        [JsonPropertyName("neutral")]
        public string? Neutral { get; set; }
    }

    public sealed class GuardianPresetManifestationDefaults
    {
        [JsonPropertyName("formFlexibility")]
        public string FormFlexibility { get; set; } = GuardianManifestation.FixedFlexibility;

        [JsonPropertyName("defaultPresentationStyle")]
        public string DefaultPresentationStyle { get; set; } = "neutral";

        [JsonPropertyName("defaultPronouns")]
        public string DefaultPronouns { get; set; } = "они/их";

        [JsonPropertyName("appearanceDescription")]
        public string AppearanceDescription { get; set; } = "";
    }

    public sealed class GuardianPresetAbode
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("theme")]
        public string Theme { get; set; } = "";
    }

    public sealed class GuardianPresetGenerationRules
    {
        [JsonPropertyName("mustPreserve")]
        public List<string> MustPreserve { get; set; } = new();

        [JsonPropertyName("canVary")]
        public List<string> CanVary { get; set; } = new();

        [JsonPropertyName("forbidden")]
        public List<string> Forbidden { get; set; } = new();
    }

    public sealed class GuardianPresetSearchAttraction
    {
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = true;

        [JsonPropertyName("label")]
        public string Label { get; set; } = "";

        [JsonPropertyName("keywords")]
        public List<string> Keywords { get; set; } = new();
    }

    public sealed class GuardianPresetAuthoring
    {
        [JsonPropertyName("author")]
        public string Author { get; set; } = "system";

        [JsonPropertyName("version")]
        public string Version { get; set; } = "1.0";
    }

    public sealed record SystemGuardianPresetDescriptor
    {
        public string PresetId { get; init; } = "";
        public string DisplayName { get; init; } = "";
        public string Summary { get; init; } = "";
        public string LibraryKind { get; init; } = "";
        public string Version { get; init; } = "1.0";
        public string Domain { get; init; } = "";
        public string Archetype { get; init; } = "";
        public string Tone { get; init; } = "";
        public IReadOnlyList<string> CoreValues { get; init; } = Array.Empty<string>();
        public string DefaultNameVariant { get; init; } = "";
        public string? FeminineNameVariant { get; init; }
        public string? MasculineNameVariant { get; init; }
        public string? NeutralNameVariant { get; init; }
        public string FormFlexibility { get; init; } = GuardianManifestation.FixedFlexibility;
        public string DefaultPresentationStyle { get; init; } = "neutral";
        public string DefaultPronouns { get; init; } = "они/их";
        public string DefaultAppearanceDescription { get; init; } = "";
        public string AbodeName { get; init; } = "";
        public string AbodeTheme { get; init; } = "";
        public IReadOnlyList<string> MustPreserve { get; init; } = Array.Empty<string>();
        public IReadOnlyList<string> CanVary { get; init; } = Array.Empty<string>();
        public IReadOnlyList<string> Forbidden { get; init; } = Array.Empty<string>();
        public string SearchLabel { get; init; } = "";
        public IReadOnlyList<string> SearchKeywords { get; init; } = Array.Empty<string>();
        public string DirectoryName { get; init; } = "";
        public string DirectoryPath { get; init; } = "";
        public string ManifestPath { get; init; } = "";
        public string DossierPath { get; init; } = "";
        public string? DossierMarkdown { get; init; }
        public string PromptPackage { get; init; } = "";
    }

    public sealed class SystemGuardianAttractionRequest
    {
        [JsonPropertyName("mode")]
        public string Mode { get; set; } = "system_guardian_attraction";

        [JsonPropertyName("targetPresetId")]
        public string TargetPresetId { get; set; } = "";

        [JsonPropertyName("targetPresetDisplayName")]
        public string TargetPresetDisplayName { get; set; } = "";

        [JsonPropertyName("targetPresetVersion")]
        public string TargetPresetVersion { get; set; } = "1.0";

        [JsonPropertyName("sourceLibrary")]
        public string SourceLibrary { get; set; } = "";

        [JsonPropertyName("targetSummary")]
        public string TargetSummary { get; set; } = "";

        [JsonPropertyName("renderedPromptPackage")]
        public string RenderedPromptPackage { get; set; } = "";

        [JsonPropertyName("_lastUpdated")]
        public string LastUpdated { get; set; } = DateTime.UtcNow.ToString("o");
    }

    public SystemGuardianLibraryService(FileSystemManager fs, ILogger<SystemGuardianLibraryService> logger)
    {
        _fs = fs;
        _logger = logger;
    }

    public string GetRootDirectoryPath() => Path.Combine(_fs.BasePath, RootDirectoryName);

    public string GetBuiltInDirectoryPath() => Path.Combine(GetRootDirectoryPath(), BuiltInDirectoryName);

    public string GetUserDirectoryPath() => Path.Combine(GetRootDirectoryPath(), UserDirectoryName);

    public async Task<List<SystemGuardianPresetDescriptor>> GetAvailablePresetsAsync(bool includeDossier = false)
    {
        EnsurePresetDirectories();

        var result = new Dictionary<string, SystemGuardianPresetDescriptor>(StringComparer.OrdinalIgnoreCase);

        var packagedBuiltInPath = GetPackagedBuiltInDirectoryPath();
        if (Directory.Exists(packagedBuiltInPath) &&
            !string.Equals(Path.GetFullPath(packagedBuiltInPath), Path.GetFullPath(GetBuiltInDirectoryPath()), StringComparison.OrdinalIgnoreCase))
        {
            foreach (var descriptor in await LoadDirectoryLayerAsync(packagedBuiltInPath, "built_in", includeDossier))
                result[descriptor.PresetId] = descriptor;
        }

        foreach (var descriptor in await LoadDirectoryLayerAsync(GetBuiltInDirectoryPath(), "built_in", includeDossier))
            result[descriptor.PresetId] = descriptor;

        foreach (var descriptor in await LoadDirectoryLayerAsync(GetUserDirectoryPath(), "user", includeDossier))
        {
            if (result.ContainsKey(descriptor.PresetId))
            {
                _logger.LogWarning("Пользовательский системный хранитель {PresetId} проигнорирован: built-in preset with the same id already exists.", descriptor.PresetId);
                continue;
            }

            result[descriptor.PresetId] = descriptor;
        }

        return result.Values
            .OrderBy(p => p.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static string GetPackagedBuiltInDirectoryPath() =>
        Path.Combine(AppContext.BaseDirectory, RootDirectoryName, BuiltInDirectoryName);

    public async Task<SystemGuardianPresetDescriptor?> FindPresetAsync(string presetId, bool includeDossier = true)
    {
        if (string.IsNullOrWhiteSpace(presetId))
            return null;

        var all = await GetAvailablePresetsAsync(includeDossier);
        return all.FirstOrDefault(p => string.Equals(p.PresetId, presetId, StringComparison.OrdinalIgnoreCase));
    }

    public JsonObject BuildPendingGuardianCreationNode(SystemGuardianPresetDescriptor preset, string soulName)
    {
        return new JsonObject
        {
            ["mode"] = "system_preset",
            ["description"] = preset.Summary,
            ["soulName"] = soulName,
            ["presetId"] = preset.PresetId,
            ["presetDisplayName"] = preset.DisplayName,
            ["presetVersion"] = preset.Version,
            ["sourceLibrary"] = preset.LibraryKind,
            ["renderedPromptPackage"] = preset.PromptPackage,
            ["_lastUpdated"] = DateTime.UtcNow.ToString("o")
        };
    }

    public JsonObject BuildCanonicalGuardianRootForFreshNewGame(
        SystemGuardianPresetDescriptor preset,
        string soulName,
        int turnNumber,
        DateTimeOffset createdAtUtc)
    {
        var guardian = BuildCanonicalGuardianForFreshNewGame(preset, soulName, turnNumber, createdAtUtc);
        var guardianId = GetRequiredString(guardian, "guardianId");
        var abode = guardian["abode"] as JsonObject ?? new JsonObject();
        var abodeId = GetNodeString(abode["abodeId"]) ?? BuildSystemGuardianAbodeId(preset.PresetId);

        return new JsonObject
        {
            ["guardians"] = new JsonArray(guardian.DeepClone()),
            ["activeGuardian"] = guardian.DeepClone(),
            ["chaosSeaNavigation"] = new JsonObject
            {
                ["currentAbodeId"] = abodeId,
                ["currentGuardianId"] = guardianId,
                ["discoveredAbodes"] = new JsonArray(abodeId)
            }
        };
    }

    public JsonObject BuildCanonicalGuardianRootForFreshNewGame(
        string freeformDescription,
        string soulName,
        int turnNumber,
        DateTimeOffset createdAtUtc)
    {
        var guardian = BuildCanonicalFreeformGuardianForFreshNewGame(freeformDescription, soulName, turnNumber, createdAtUtc);
        var guardianId = GetRequiredString(guardian, "guardianId");
        var abode = guardian["abode"] as JsonObject ?? new JsonObject();
        var abodeId = GetNodeString(abode["abodeId"]) ?? BuildFreeformGuardianAbodeId(guardianId);

        return new JsonObject
        {
            ["guardians"] = new JsonArray(guardian.DeepClone()),
            ["activeGuardian"] = guardian.DeepClone(),
            ["chaosSeaNavigation"] = new JsonObject
            {
                ["currentAbodeId"] = abodeId,
                ["currentGuardianId"] = guardianId,
                ["discoveredAbodes"] = new JsonArray(abodeId)
            }
        };
    }

    public JsonObject BuildAfterlifeEntityProfileRootForFreshNewGame(
        SystemGuardianPresetDescriptor preset,
        string soulName,
        int turnNumber,
        DateTimeOffset createdAtUtc)
    {
        var profile = BuildAfterlifeEntityProfileForFreshNewGame(preset, soulName, turnNumber, createdAtUtc);

        return new JsonObject
        {
            ["schemaVersion"] = AfterlifeEntityProfileState.SchemaVersion,
            [AfterlifeEntityProfileState.ProfilesProperty] = new JsonArray(profile)
        };
    }

    public JsonObject BuildAfterlifeEntityProfileRootForFreshNewGame(
        string freeformDescription,
        string soulName,
        int turnNumber,
        DateTimeOffset createdAtUtc)
    {
        var profile = BuildAfterlifeEntityProfileForFreshNewGame(freeformDescription, soulName, turnNumber, createdAtUtc);

        return new JsonObject
        {
            ["schemaVersion"] = AfterlifeEntityProfileState.SchemaVersion,
            [AfterlifeEntityProfileState.ProfilesProperty] = new JsonArray(profile)
        };
    }

    public JsonObject BuildFreeformPendingGuardianCreationNode(string description, string soulName)
    {
        return new JsonObject
        {
            ["mode"] = "freeform",
            ["description"] = description,
            ["soulName"] = soulName,
            ["_lastUpdated"] = DateTime.UtcNow.ToString("o")
        };
    }

    private static JsonObject BuildCanonicalFreeformGuardianForFreshNewGame(
        string freeformDescription,
        string soulName,
        int turnNumber,
        DateTimeOffset createdAtUtc)
    {
        var timestamp = createdAtUtc.ToUniversalTime().ToString("o");
        var normalizedDescription = NormalizeFreeformDescription(freeformDescription);
        var displayName = ExtractFreeformGuardianName(normalizedDescription);
        var domain = InferFreeformDomain(normalizedDescription);
        var guardianId = BuildFreeformGuardianId(displayName, normalizedDescription);
        var abodeId = BuildFreeformGuardianAbodeId(guardianId);
        var abodeName = BuildFreeformAbodeName(displayName, normalizedDescription);
        var currentPower = AbodePowerRules.DefaultCurrentPower;

        return new JsonObject
        {
            ["guardianId"] = guardianId,
            ["canonicalName"] = displayName,
            ["domain"] = domain,
            ["originType"] = "freeform",
            ["freeformSourceDescription"] = normalizedDescription,
            ["nameVariants"] = new JsonObject
            {
                ["default"] = displayName,
                ["feminine"] = displayName,
                ["masculine"] = displayName,
                ["neutral"] = displayName
            },
            ["manifestation"] = new JsonObject
            {
                ["formFlexibility"] = GuardianManifestation.SelectiveFlexibility,
                ["currentDisplayName"] = displayName,
                ["currentPresentationStyle"] = "freeform",
                ["currentPronouns"] = InferFreeformPronouns(displayName, normalizedDescription),
                ["appearanceDescription"] = $"Первичная форма Хранителя взята из описания игрока: {TruncateForSummary(normalizedDescription, 420)}"
            },
            ["manifestationHistory"] = new JsonArray(),
            ["abode"] = new JsonObject
            {
                ["abodeId"] = abodeId,
                ["name"] = abodeName,
                ["description"] = $"{abodeName}: стартовая обитель, созданная из свободного описания игрока. Исходный образ: {TruncateForSummary(normalizedDescription, 520)}",
                ["image_prompt"] = $"dark fantasy afterlife guardian abode, {abodeName}, {domain}, cinematic, readable composition",
                ["isDiscovered"] = true
            },
            ["personalityProfile"] = new JsonObject
            {
                ["archetype"] = "Freeform Guardian",
                ["speechPattern"] = "тон и манера речи заданы свободным описанием игрока",
                ["coreValues"] = BuildFreeformCoreValues(normalizedDescription)
            },
            ["mood"] = new JsonObject
            {
                ["current"] = "focused",
                ["intensity"] = 40,
                ["reason"] = $"Первая встреча с душой «{soulName}» после свободного описания Хранителя.",
                ["since"] = Math.Max(0, turnNumber)
            },
            ["relationshipData"] = new JsonObject
            {
                ["currentReputation"] = 0,
                ["reputationHistory"] = new JsonArray(),
                ["lastInteraction"] = timestamp
            },
            ["abodePower"] = new JsonObject
            {
                ["currentPower"] = currentPower,
                ["tier"] = AbodePowerRules.GetTierLabel(currentPower),
                ["lastUpdatedAt"] = timestamp,
                ["history"] = new JsonArray()
            },
            ["guardianRelationships"] = new JsonArray(),
            ["questManagement"] = new JsonObject
            {
                ["availableQuests"] = new JsonArray(),
                ["activeQuests"] = new JsonArray(),
                ["completedQuests"] = new JsonArray()
            },
            ["gachaSystem"] = new JsonObject
            {
                ["chargesPerReturn"] = GuardianGachaChargeRules.GetChargesPerReturnForReputation(0, currentPower),
                ["chargesUsedThisReturn"] = 0,
                ["gachaHistory"] = new JsonArray()
            },
            ["loreFragments"] = BuildInitialFreeformLoreFragments(guardianId, displayName, domain, abodeName, normalizedDescription),
            ["musings"] = new JsonArray(),
            ["tradeInventoryReceipts"] = new JsonArray(),
            ["displayName"] = displayName
        };
    }

    private static JsonObject BuildCanonicalGuardianForFreshNewGame(
        SystemGuardianPresetDescriptor preset,
        string soulName,
        int turnNumber,
        DateTimeOffset createdAtUtc)
    {
        var timestamp = createdAtUtc.ToUniversalTime().ToString("o");
        var guardianId = BuildSystemGuardianId(preset.PresetId);
        var abodeId = BuildSystemGuardianAbodeId(preset.PresetId);
        var defaultName = FirstNonEmpty(preset.DefaultNameVariant, preset.DisplayName, preset.PresetId);
        var currentPower = AbodePowerRules.DefaultCurrentPower;

        var guardian = new JsonObject
        {
            ["guardianId"] = guardianId,
            ["canonicalName"] = defaultName,
            ["domain"] = FirstNonEmpty(preset.Domain, "Knowledge"),
            ["originType"] = "system_preset",
            ["sourcePreset"] = new JsonObject
            {
                ["presetId"] = preset.PresetId,
                ["displayName"] = preset.DisplayName,
                ["version"] = preset.Version,
                ["library"] = preset.LibraryKind
            },
            ["nameVariants"] = new JsonObject
            {
                ["default"] = defaultName,
                ["feminine"] = FirstNonEmpty(preset.FeminineNameVariant, defaultName),
                ["masculine"] = FirstNonEmpty(preset.MasculineNameVariant, defaultName),
                ["neutral"] = FirstNonEmpty(preset.NeutralNameVariant, defaultName)
            },
            ["manifestation"] = new JsonObject
            {
                ["formFlexibility"] = GuardianManifestation.IsValidFormFlexibility(preset.FormFlexibility)
                    ? preset.FormFlexibility
                    : GuardianManifestation.FixedFlexibility,
                ["currentDisplayName"] = defaultName,
                ["currentPresentationStyle"] = FirstNonEmpty(preset.DefaultPresentationStyle, "neutral"),
                ["currentPronouns"] = FirstNonEmpty(preset.DefaultPronouns, "они/их"),
                ["appearanceDescription"] = FirstNonEmpty(
                    preset.DefaultAppearanceDescription,
                    $"Хранитель {defaultName} проявляется в форме, заданной системным досье.")
            },
            ["manifestationHistory"] = new JsonArray(),
            ["abode"] = new JsonObject
            {
                ["abodeId"] = abodeId,
                ["name"] = FirstNonEmpty(preset.AbodeName, $"{defaultName} — Обитель"),
                ["description"] = BuildInitialAbodeDescription(preset),
                ["image_prompt"] = BuildInitialAbodeImagePrompt(preset),
                ["isDiscovered"] = true
            },
            ["personalityProfile"] = new JsonObject
            {
                ["archetype"] = FirstNonEmpty(preset.Archetype, "Guardian"),
                ["speechPattern"] = FirstNonEmpty(preset.Tone, "спокойная и внимательная речь"),
                ["coreValues"] = ToJsonStringArray(preset.CoreValues)
            },
            ["mood"] = new JsonObject
            {
                ["current"] = "welcoming",
                ["intensity"] = 35,
                ["reason"] = $"Первая встреча с душой «{soulName}» после выбора системного Хранителя.",
                ["since"] = Math.Max(0, turnNumber)
            },
            ["relationshipData"] = new JsonObject
            {
                ["currentReputation"] = 0,
                ["reputationHistory"] = new JsonArray(),
                ["lastInteraction"] = timestamp
            },
            ["abodePower"] = new JsonObject
            {
                ["currentPower"] = currentPower,
                ["tier"] = AbodePowerRules.GetTierLabel(currentPower),
                ["lastUpdatedAt"] = timestamp,
                ["history"] = new JsonArray()
            },
            ["guardianRelationships"] = new JsonArray(),
            ["questManagement"] = new JsonObject
            {
                ["availableQuests"] = new JsonArray(),
                ["activeQuests"] = new JsonArray(),
                ["completedQuests"] = new JsonArray()
            },
            ["gachaSystem"] = new JsonObject
            {
                ["chargesPerReturn"] = GuardianGachaChargeRules.GetChargesPerReturnForReputation(0, currentPower),
                ["chargesUsedThisReturn"] = 0,
                ["gachaHistory"] = new JsonArray()
            },
            ["loreFragments"] = BuildInitialLoreFragments(preset),
            ["musings"] = new JsonArray(),
            ["tradeInventoryReceipts"] = new JsonArray(),
            ["displayName"] = defaultName
        };

        return guardian;
    }

    private static JsonObject BuildAfterlifeEntityProfileForFreshNewGame(
        SystemGuardianPresetDescriptor preset,
        string soulName,
        int turnNumber,
        DateTimeOffset createdAtUtc)
    {
        var timestamp = createdAtUtc.ToUniversalTime().ToString("o");
        var materializationTurn = Math.Max(0, turnNumber);
        var presetId = FirstNonEmpty(preset.PresetId, "system_guardian");
        var guardianId = BuildSystemGuardianId(presetId);
        var defaultName = FirstNonEmpty(preset.DefaultNameVariant, preset.DisplayName, presetId);
        var abodeName = FirstNonEmpty(preset.AbodeName, $"{defaultName} — Обитель");
        var domain = FirstNonEmpty(preset.Domain, "Knowledge");
        const string progressionStrategySummary =
            "Стартовый системный Хранитель держит запас духовных техник для обучения души через витрину наставника.";

        return new JsonObject
        {
            ["actorType"] = "guardian",
            ["actorId"] = guardianId,
            ["displayName"] = defaultName,
            ["realm"] = "Chaos Sea",
            ["locationName"] = abodeName,
            ["sourcePreset"] = new JsonObject
            {
                ["presetId"] = presetId,
                ["displayName"] = FirstNonEmpty(preset.DisplayName, defaultName),
                ["version"] = preset.Version,
                ["library"] = preset.LibraryKind
            },
            ["mentorProfile"] = new JsonObject
            {
                ["canTeach"] = true,
                ["relationshipLevel"] = 0,
                ["summary"] = BuildInitialMentorSummary(defaultName, domain, preset.Summary),
                ["createdBy"] = "fresh_new_game_system_guardian_bootstrap"
            },
            ["currencies"] = new JsonObject
            {
                ["inkFeathers"] = 0,
                ["lightSparks"] = 0
            },
            ["progression"] = new JsonObject
            {
                ["enlightenment"] = new JsonObject
                {
                    ["experience"] = 0,
                    ["tier"] = 0
                },
                ["radiance"] = new JsonObject
                {
                    ["experience"] = 0,
                    ["tier"] = 0
                }
            },
            ["standardArts"] = BuildInitialMentorStandardArts(domain),
            ["specialArts"] = new JsonArray(),
            ["customStates"] = new JsonArray(),
            ["fateCards"] = new JsonArray(),
            ["relationships"] = new JsonArray(),
            ["soulDissipationTier"] = 0,
            ["progressionStrategy"] = new JsonObject
            {
                ["strategyId"] = $"strategy_system_guardian_{SanitizeIdSegment(presetId)}",
                ["summary"] = progressionStrategySummary,
                ["priorityOrder"] = new JsonArray("guard", "maneuver"),
                ["lastUpdatedAtTurn"] = materializationTurn,
                ["resourceReserve"] = new JsonObject
                {
                    ["inkFeathers"] = 0,
                    ["lightSparks"] = 0
                },
                ["allowedSpends"] = new JsonArray("standardArts", "specialArts")
            },
            ["gmThoughtsSummary"] = progressionStrategySummary,
            ["progressionLedger"] = new JsonArray(),
            ["ledger"] = new JsonArray
            {
                new JsonObject
                {
                    ["entryId"] = $"system_guardian_profile_bootstrap_{SanitizeIdSegment(presetId)}",
                    ["turnNumber"] = materializationTurn,
                    ["reason"] = "fresh_new_game_system_guardian_bootstrap",
                    ["summary"] = $"Клиент создал стартовый профиль наставника для системного Хранителя {defaultName}, чтобы команда /обучение могла запросить витрину у ГМ.",
                    ["createdAt"] = timestamp,
                    ["soulName"] = soulName
                }
            },
            [ActorMaterializationContract.PropertyName] = BuildSystemGuardianMaterialization(
                guardianId,
                materializationTurn)
        };
    }

    private static JsonObject BuildAfterlifeEntityProfileForFreshNewGame(
        string freeformDescription,
        string soulName,
        int turnNumber,
        DateTimeOffset createdAtUtc)
    {
        var timestamp = createdAtUtc.ToUniversalTime().ToString("o");
        var materializationTurn = Math.Max(0, turnNumber);
        var normalizedDescription = NormalizeFreeformDescription(freeformDescription);
        var displayName = ExtractFreeformGuardianName(normalizedDescription);
        var domain = InferFreeformDomain(normalizedDescription);
        var guardianId = BuildFreeformGuardianId(displayName, normalizedDescription);
        var abodeName = BuildFreeformAbodeName(displayName, normalizedDescription);
        var seedSegment = SanitizeIdSegment(guardianId);
        const string progressionStrategySummary =
            "Стартовый свободно описанный Хранитель держит безопасный набор духовных техник для первых уроков души.";

        return new JsonObject
        {
            ["actorType"] = "guardian",
            ["actorId"] = guardianId,
            ["displayName"] = displayName,
            ["realm"] = "Chaos Sea",
            ["locationName"] = abodeName,
            ["originType"] = "freeform",
            ["freeformSourceDescription"] = normalizedDescription,
            ["mentorProfile"] = new JsonObject
            {
                ["canTeach"] = true,
                ["relationshipLevel"] = 0,
                ["summary"] = BuildInitialMentorSummary(displayName, domain, normalizedDescription),
                ["createdBy"] = "fresh_new_game_freeform_guardian_bootstrap"
            },
            ["currencies"] = new JsonObject
            {
                ["inkFeathers"] = 0,
                ["lightSparks"] = 0
            },
            ["progression"] = new JsonObject
            {
                ["enlightenment"] = new JsonObject
                {
                    ["experience"] = 0,
                    ["tier"] = 0
                },
                ["radiance"] = new JsonObject
                {
                    ["experience"] = 0,
                    ["tier"] = 0
                }
            },
            ["standardArts"] = BuildInitialMentorStandardArts(domain),
            ["specialArts"] = new JsonArray(),
            ["customStates"] = new JsonArray(),
            ["fateCards"] = new JsonArray(),
            ["relationships"] = new JsonArray(),
            ["soulDissipationTier"] = 0,
            ["progressionStrategy"] = new JsonObject
            {
                ["strategyId"] = $"strategy_{seedSegment}",
                ["summary"] = progressionStrategySummary,
                ["priorityOrder"] = new JsonArray("guard", "maneuver"),
                ["lastUpdatedAtTurn"] = materializationTurn,
                ["resourceReserve"] = new JsonObject
                {
                    ["inkFeathers"] = 0,
                    ["lightSparks"] = 0
                },
                ["allowedSpends"] = new JsonArray("standardArts", "specialArts")
            },
            ["gmThoughtsSummary"] = progressionStrategySummary,
            ["progressionLedger"] = new JsonArray(),
            ["ledger"] = new JsonArray
            {
                new JsonObject
                {
                    ["entryId"] = $"freeform_guardian_profile_bootstrap_{seedSegment}",
                    ["turnNumber"] = materializationTurn,
                    ["reason"] = "fresh_new_game_freeform_guardian_bootstrap",
                    ["summary"] = $"Клиент создал стартовый профиль наставника для свободно описанного Хранителя {displayName}, чтобы первый ход не зависел от технической материализации.",
                    ["createdAt"] = timestamp,
                    ["soulName"] = soulName
                }
            },
            [ActorMaterializationContract.PropertyName] = BuildSystemGuardianMaterialization(
                guardianId,
                materializationTurn)
        };
    }

    private static JsonObject BuildSystemGuardianMaterialization(string guardianId, int materializationTurn) =>
        new()
        {
            ["schemaVersion"] = ActorMaterializationContract.SchemaVersion,
            ["materializationId"] = $"mat_{guardianId}_turn_{materializationTurn}",
            ["actorType"] = "guardian",
            ["actorId"] = guardianId,
            ["materializedAtTurn"] = materializationTurn,
            ["state"] = "complete",
            ["capabilities"] = new JsonObject
            {
                ["canFight"] = true,
                ["canTeach"] = true,
                ["canTrade"] = false
            },
            ["sections"] = new JsonObject
            {
                ["standardArts"] = new JsonObject { ["state"] = "populated" },
                ["specialArts"] = new JsonObject
                {
                    ["state"] = "empty_by_design",
                    ["reason"] = "Хранитель ещё не создал личного особого искусства."
                },
                ["customStates"] = new JsonObject
                {
                    ["state"] = "empty_by_design",
                    ["reason"] = "На Хранителе нет особых духовных состояний."
                },
                ["fateCards"] = new JsonObject
                {
                    ["state"] = "empty_by_design",
                    ["reason"] = "Карта Судьбы Хранителя ещё не открыта."
                },
                ["relationships"] = new JsonObject
                {
                    ["state"] = "empty_by_design",
                    ["reason"] = "Устойчивые связи Хранителя ещё не сформировались."
                },
                ["agency"] = new JsonObject { ["state"] = "populated" },
                ["progressionHistory"] = new JsonObject { ["state"] = "populated" }
            }
        };

    private static JsonObject BuildInitialMentorStandardArts(string domain)
    {
        var arts = new JsonObject
        {
            ["guard"] = 2,
            ["maneuver"] = 1
        };

        switch (domain.Trim().ToLowerInvariant())
        {
            case "combat":
                arts["pressure"] = 2;
                arts["counter"] = 1;
                break;
            case "magic":
            case "knowledge":
                arts["binding"] = 1;
                break;
            case "intrigue":
            case "social":
            case "trade":
                arts["counter"] = 1;
                break;
            case "healing":
            case "survival":
                arts["recover_spiritual_power"] = 1;
                break;
        }

        return arts;
    }

    private static string BuildInitialMentorSummary(string guardianName, string domain, string summary)
    {
        var domainText = FirstNonEmpty(domain, "Knowledge");
        var summaryText = FirstNonEmpty(summary, "готовит душу к первым шагам в Море Хаоса");
        return $"{guardianName} может наставлять душу в домене {domainText}: {summaryText}";
    }

    private static JsonArray BuildInitialLoreFragments(SystemGuardianPresetDescriptor preset)
    {
        var presetId = FirstNonEmpty(preset.PresetId, "system_guardian");
        var displayName = FirstNonEmpty(preset.DisplayName, presetId);
        var domain = FirstNonEmpty(preset.Domain, "Knowledge");
        var abodeName = FirstNonEmpty(preset.AbodeName, "Обитель Хранителя");
        var values = preset.CoreValues.Count > 0
            ? string.Join(", ", preset.CoreValues.Take(3))
            : "личный закон Хранителя";

        var fragments = new[]
        {
            ("identity", "Личность Хранителя", $"{displayName} хранит устойчивую личность и не должен подменяться другим архетипом.", "personal_history", 0),
            ("domain", "Домен влияния", $"Домен Хранителя: {domain}. Он задаёт тон помощи, испытаний и будущих просьб.", "domain_mastery", 0),
            ("abode", "Обитель", $"{abodeName} уже найдена душой и становится первой безопасной точкой в Море Хаоса.", "lost_world", 0),
            ("values", "Ценности", $"Ключевые ценности: {values}. Через них Хранитель оценивает выбор души.", "soul_mechanics", 50),
            ("limits", "Границы помощи", "Хранитель помогает душе, но не проживает жизнь вместо неё и не отменяет цену выбора.", "soul_mechanics", 50),
            ("quest_seed", "Будущий личный квест", "Личный квест Хранителя должен раскрыться через отношения, репутацию и события будущих жизней.", "personal_history", 130),
            ("secret", "Скрытая причина", "У Хранителя есть тайная причина внимательно следить за этой душой; её нельзя раскрывать на старте.", "cosmic_secret", 230)
        };

        var result = new JsonArray();
        foreach (var (suffix, title, summary, category, requiredReputation) in fragments)
        {
            result.Add(new JsonObject
            {
                ["fragmentId"] = $"lore_{SanitizeIdSegment(presetId)}_{suffix}",
                ["title"] = title,
                ["summary"] = summary,
                ["discoveryState"] = "planned",
                ["visibility"] = "hidden",
                ["sourcePresetId"] = presetId,
                ["tags"] = new JsonArray(SanitizeIdSegment(suffix), SanitizeIdSegment(domain)),
                ["category"] = category,
                ["content"] = summary,
                ["requiredReputation"] = requiredReputation
            });
        }

        return result;
    }

    private static JsonArray BuildInitialFreeformLoreFragments(
        string guardianId,
        string displayName,
        string domain,
        string abodeName,
        string description)
    {
        var sourceId = SanitizeIdSegment(guardianId);
        var shortDescription = TruncateForSummary(description, 240);
        var fragments = new[]
        {
            ("identity", "Личность Хранителя", $"{displayName} создан из свободного описания игрока и должен сохранять эту личность в будущих сценах.", "personal_history", 0),
            ("domain", "Домен влияния", $"Домен стартового Хранителя: {domain}. Он задаёт первые уроки, помощь и ограничения.", "domain_mastery", 0),
            ("abode", "Обитель", $"{abodeName} уже признана первой безопасной точкой души в Море Хаоса.", "lost_world", 0),
            ("source", "Исходный образ", $"Ключевой образ из описания игрока: {shortDescription}", "personal_history", 0),
            ("limits", "Границы помощи", "Хранитель может наставлять и предупреждать, но не отменяет цену выбора и не проживает путь вместо души.", "soul_mechanics", 50),
            ("quest_seed", "Будущий личный квест", "Личный квест Хранителя должен раскрыться позже через доверие, репутацию и события новых жизней.", "personal_history", 130),
            ("secret", "Скрытая причина", "У Хранителя есть причина наблюдать за этой душой внимательнее, чем за случайным гостем Моря Хаоса; её нельзя раскрывать на старте.", "cosmic_secret", 230)
        };

        var result = new JsonArray();
        foreach (var (suffix, title, summary, category, requiredReputation) in fragments)
        {
            result.Add(new JsonObject
            {
                ["fragmentId"] = $"lore_{sourceId}_{suffix}",
                ["title"] = title,
                ["summary"] = summary,
                ["discoveryState"] = "planned",
                ["visibility"] = "hidden",
                ["sourcePresetId"] = "freeform",
                ["sourceGuardianId"] = guardianId,
                ["tags"] = new JsonArray(SanitizeIdSegment(suffix), SanitizeIdSegment(domain)),
                ["category"] = category,
                ["content"] = summary,
                ["requiredReputation"] = requiredReputation
            });
        }

        return result;
    }

    private static string BuildInitialAbodeDescription(SystemGuardianPresetDescriptor preset)
    {
        var abodeName = FirstNonEmpty(preset.AbodeName, "Обитель Хранителя");
        var theme = FirstNonEmpty(preset.AbodeTheme, preset.Summary, "личное пространство Хранителя в Море Хаоса");
        return $"{abodeName}: {theme}. Это стартовая обитель, уже открытая душе при выборе системного Хранителя.";
    }

    private static string BuildInitialAbodeImagePrompt(SystemGuardianPresetDescriptor preset) =>
        $"dark fantasy afterlife guardian abode, {FirstNonEmpty(preset.AbodeName, "guardian abode")}, " +
        $"{FirstNonEmpty(preset.AbodeTheme, preset.Summary, preset.Domain, "mystic sanctuary")}, cinematic, readable composition";

    private static JsonArray ToJsonStringArray(IEnumerable<string> values)
    {
        var result = new JsonArray();
        foreach (var value in values.Where(value => !string.IsNullOrWhiteSpace(value)))
            result.Add(value.Trim());

        if (result.Count == 0)
            result.Add("сопровождение души");

        return result;
    }

    private static string BuildSystemGuardianId(string presetId) =>
        $"guard_system_{SanitizeIdSegment(presetId)}_001";

    private static string BuildSystemGuardianAbodeId(string presetId) =>
        $"abode_system_{SanitizeIdSegment(presetId)}_001";

    private static string BuildFreeformGuardianId(string displayName, string description)
    {
        var slug = SanitizeIdSegment(displayName);
        if (string.Equals(slug, "guardian", StringComparison.OrdinalIgnoreCase))
            slug = SanitizeIdSegment(description);

        return $"guard_freeform_{TruncateIdSegment(slug, 48)}_001";
    }

    private static string BuildFreeformGuardianAbodeId(string guardianId) =>
        $"abode_{SanitizeIdSegment(guardianId)}";

    private static string NormalizeFreeformDescription(string description)
    {
        var normalized = string.Join(
            " ",
            (description ?? string.Empty)
                .Replace('\r', ' ')
                .Replace('\n', ' ')
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return string.IsNullOrWhiteSpace(normalized)
            ? "Свободно описанный Хранитель души."
            : normalized;
    }

    private static string ExtractFreeformGuardianName(string description)
    {
        var firstLine = description.Split(new[] { '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? description.Trim();
        foreach (var separator in new[] { ':', '—', '-' })
        {
            var index = firstLine.IndexOf(separator);
            if (index > 2)
            {
                var candidate = firstLine[..index].Trim(' ', '"', '\'', '«', '»');
                if (candidate.Length is >= 3 and <= 80)
                    return candidate;
            }
        }

        var words = firstLine.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (words.Length >= 2)
            return string.Join(' ', words.Take(Math.Min(words.Length, 4))).Trim(' ', '"', '\'', '«', '»');

        return FirstNonEmpty(firstLine, "Свободный Хранитель");
    }

    private static string InferFreeformDomain(string description)
    {
        var source = description.ToLowerInvariant();
        if (ContainsAny(source, "библиот", "архив", "знан", "мудрост", "книг", "тайн"))
            return "Knowledge";
        if (ContainsAny(source, "сдел", "торг", "куп", "долг", "цен", "обмен"))
            return "Trade";
        if (ContainsAny(source, "бой", "меч", "клин", "воин", "битв", "охот"))
            return "Combat";
        if (ContainsAny(source, "исцел", "леч", "милосерд", "забот"))
            return "Healing";
        if (ContainsAny(source, "лес", "дорог", "выжив", "пустош", "след"))
            return "Survival";
        if (ContainsAny(source, "интриг", "лож", "маск", "тайн", "договор"))
            return "Intrigue";

        return "Knowledge";
    }

    private static string BuildFreeformAbodeName(string displayName, string description)
    {
        var source = description.ToLowerInvariant();
        if (ContainsAny(source, "башн") && ContainsAny(source, "архив", "библиот"))
            return "Башня Бесконечных Архивов";
        if (ContainsAny(source, "библиот"))
            return $"Библиотека {displayName}";
        if (ContainsAny(source, "архив"))
            return $"Архив {displayName}";

        return $"Обитель {displayName}";
    }

    private static JsonArray BuildFreeformCoreValues(string description)
    {
        var values = new JsonArray();
        var source = description.ToLowerInvariant();
        if (ContainsAny(source, "мудрост", "осторож"))
            values.Add("осторожная мудрость");
        if (ContainsAny(source, "сдел", "цен", "договор"))
            values.Add("цена любого обещания");
        if (ContainsAny(source, "библиот", "архив", "знан"))
            values.Add("сохранение забытых знаний");
        if (values.Count == 0)
            values.Add("сопровождение души");
        return values;
    }

    private static string InferFreeformPronouns(string displayName, string description)
    {
        var source = $"{displayName} {description}".ToLowerInvariant();
        if (ContainsAny(source, "хранительница", "покровительница", "она", "женск"))
            return "она/её";
        if (ContainsAny(source, "хранитель", "покровитель", "он", "мужск"))
            return "он/его";

        return "они/их";
    }

    private static bool ContainsAny(string source, params string[] fragments) =>
        fragments.Any(fragment => source.Contains(fragment, StringComparison.OrdinalIgnoreCase));

    private static string TruncateForSummary(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length <= maxLength)
            return value;

        return value[..Math.Max(0, maxLength - 1)].TrimEnd() + "…";
    }

    private static string TruncateIdSegment(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length <= maxLength)
            return string.IsNullOrWhiteSpace(value) ? "guardian" : value;

        return value[..maxLength].Trim('_');
    }

    private static string SanitizeIdSegment(string? value)
    {
        var source = string.IsNullOrWhiteSpace(value) ? "guardian" : value.Trim().ToLowerInvariant();
        var builder = new StringBuilder(source.Length);
        var previousWasSeparator = false;

        foreach (var ch in source)
        {
            if (char.IsAsciiLetterOrDigit(ch))
            {
                builder.Append(ch);
                previousWasSeparator = false;
            }
            else if (!previousWasSeparator)
            {
                builder.Append('_');
                previousWasSeparator = true;
            }
        }

        var result = builder.ToString().Trim('_');
        return string.IsNullOrWhiteSpace(result) ? "guardian" : result;
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

    private static string GetRequiredString(JsonObject obj, string propertyName) =>
        GetNodeString(obj[propertyName]) ?? throw new InvalidOperationException($"{propertyName} is required.");

    private static string? GetNodeString(JsonNode? node)
    {
        if (node is null)
            return null;

        try
        {
            return node.GetValue<string>();
        }
        catch
        {
            return null;
        }
    }

    public SystemGuardianAttractionRequest BuildAttractionRequest(SystemGuardianPresetDescriptor preset)
    {
        return new SystemGuardianAttractionRequest
        {
            TargetPresetId = preset.PresetId,
            TargetPresetDisplayName = preset.DisplayName,
            TargetPresetVersion = preset.Version,
            SourceLibrary = preset.LibraryKind,
            TargetSummary = preset.Summary,
            RenderedPromptPackage = preset.PromptPackage,
            LastUpdated = DateTime.UtcNow.ToString("o")
        };
    }

    public async Task WriteAttractionRequestAsync(SystemGuardianPresetDescriptor preset)
    {
        var existingState = await ReadAttractionRequestStateAsync();
        if (existingState.IsMalformed)
            throw new InvalidOperationException("system_guardian_attraction.json повреждён и должен быть исправлен или очищен до записи нового attraction request.");
        if (existingState.Request != null &&
            (!string.Equals(existingState.Request.TargetPresetId, preset.PresetId, StringComparison.OrdinalIgnoreCase) ||
             !string.Equals(existingState.Request.TargetPresetVersion, preset.Version, StringComparison.OrdinalIgnoreCase) ||
             !string.Equals(existingState.Request.SourceLibrary, preset.LibraryKind, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("system_guardian_attraction.json уже содержит живой pending attraction contract и не может быть заменён новым выбором без явной canonical closure.");
        }

        var request = BuildAttractionRequest(preset);
        await _fs.WriteFileAtomicAsync(AttractionRequestPath, JsonSerializer.Serialize(request, JsonOpts));
    }

    public void ClearAttractionRequest() => _fs.DeleteFile(AttractionRequestPath);

    public async Task<SystemGuardianAttractionRequest?> ReadAttractionRequestAsync()
    {
        var state = await ReadAttractionRequestStateAsync();
        return state.IsMalformed ? null : state.Request;
    }

    internal Task<AttractionRequestReadState> ReadAttractionRequestDisplayStateAsync() =>
        ReadAttractionRequestStateAsync();

    public async Task EnsureAttractionRequestHealthyAsync(string? currentRealm)
    {
        if (!_fs.FileExists(AttractionRequestPath))
            return;

        if (!RealmSemantics.HasResolvedRealm(currentRealm))
        {
            _logger.LogWarning("system_guardian_attraction.json найден при unresolved currentRealm. Pending attraction сохраняется fail-closed до восстановления realm authority.");
            return;
        }

        var state = await ReadAttractionRequestStateAsync();
        if (state.IsMalformed)
            return;

        if (!RealmSemantics.IsAfterlifeRealm(currentRealm))
        {
            _fs.DeleteFile(AttractionRequestPath);
            return;
        }

        var request = state.Request;
        if (request == null)
            return;

        if (!string.Equals(request.Mode, "system_guardian_attraction", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(request.TargetPresetId) ||
            string.IsNullOrWhiteSpace(request.TargetPresetDisplayName))
        {
            return;
        }

        if (HasActiveTurnValidationArtifacts())
            return;

        if (await ActiveGuardianMatchesAttractionRequestAsync(request))
            _fs.DeleteFile(AttractionRequestPath);
    }

    private bool HasActiveTurnValidationArtifacts() =>
        _fs.FileExists("ready/turn_complete.json") ||
        _fs.FileExists("ready/turn_error.json") ||
        _fs.FileExists("game_state/control/pending_turn_snapshot.json") ||
        _fs.FileExists(PendingTurnSnapshotAuthority.AuthorityPath);

    private async Task<bool> ActiveGuardianMatchesAttractionRequestAsync(SystemGuardianAttractionRequest request)
    {
        var guardiansJson = await _fs.ReadFileAsync("game_state/meta/guardians.json");
        if (string.IsNullOrWhiteSpace(guardiansJson))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(guardiansJson);
            if (!doc.RootElement.TryGetProperty("activeGuardian", out var activeGuardian) ||
                activeGuardian.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (!activeGuardian.TryGetProperty("sourcePreset", out var sourcePreset) ||
                sourcePreset.ValueKind != JsonValueKind.Object ||
                !sourcePreset.TryGetProperty("presetId", out var presetIdNode) ||
                presetIdNode.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            return string.Equals(
                presetIdNode.GetString(),
                request.TargetPresetId,
                StringComparison.OrdinalIgnoreCase);
        }
        catch (JsonException ex)
        {
            _logger.LogDebug(ex, "Не удалось прочитать guardians.json для проверки resolved system guardian attraction.");
            return false;
        }
    }

    public async Task<string> BuildReminderFragmentAsync(string? currentRealm)
    {
        var isAfterlife = RealmSemantics.IsAfterlifeRealm(currentRealm);
        if (!isAfterlife)
            return string.Empty;

        var isChaosSea = RealmSemantics.IsChaosSea(currentRealm);
        var parts = new List<string>();
        var pendingPreset = await ReadPendingGuardianCreationPresetAsync();
        if (pendingPreset != null)
        {
            parts.Add("ETERNAL GUARDIAN PRESET:");
            parts.Add("  - Player-facing roleplay term: Eternal Guardian. Technical contract term: system guardian preset.");
            parts.Add("  - A client-authored Eternal Guardian preset was selected for current guardian creation.");
            parts.Add("  - Materialize THIS guardian instead of inventing an unrelated archetype.");
            parts.Add("  - Preserve the preset invariants and write guardian.sourcePreset metadata into canonical guardian state.");
            parts.Add("  - Guardian identity must use canonicalName + nameVariants + manifestation + manifestationHistory, not a legacy raw name field.");
            parts.Add($"  - Preset: {pendingPreset.TargetPresetDisplayName} ({pendingPreset.TargetPresetId}, {pendingPreset.SourceLibrary}, v{pendingPreset.TargetPresetVersion})");
            if (!string.IsNullOrWhiteSpace(pendingPreset.TargetSummary))
                parts.Add($"  - Summary: {pendingPreset.TargetSummary}");
            parts.Add("  - Rendered dossier:");
            parts.Add(IndentMultiline(pendingPreset.RenderedPromptPackage, "    "));
        }

        var attractionState = await ReadAttractionRequestStateAsync();
        if (attractionState.IsMalformed)
        {
            if (isChaosSea)
            {
                parts.Add("ETERNAL GUARDIAN ATTRACTION CORRUPTION:");
                parts.Add("  - system_guardian_attraction.json unreadable or structurally invalid.");
                parts.Add("  - Preserve this deterministic attraction contract until validation/repair resolves it.");
            }
            else
            {
                parts.Add("ETERNAL GUARDIAN ATTRACTION WRONG-REALM REPAIR:");
                parts.Add("  - system_guardian_attraction.json is unreadable or structurally invalid and current realm is not Chaos Sea.");
                parts.Add("  - Treat this file as repair-only wrong-realm evidence; do not resolve a Shining Abode turn from it.");
                parts.Add("  - Preserve it for client repair or explicit client cancellation.");
            }
        }
        else if (attractionState.Request != null)
        {
            var attractionRequest = attractionState.Request;
            if (isChaosSea)
            {
                parts.Add("ETERNAL GUARDIAN ATTRACTION:");
                parts.Add("  - Player-facing roleplay term: Eternal Guardian. Technical control-file term: system_guardian_attraction.");
                parts.Add("  - The player is deliberately seeking a specific Eternal Guardian.");
                parts.Add("  - This attraction is deterministic for this turn. Do NOT substitute a different guardian.");
                parts.Add("  - If the guardian is not yet materialized in the session, create and materialize them from this preset now.");
                parts.Add("  - If the guardian already exists, route the soul to that guardian and synchronize activeGuardian/current abode state.");
                parts.Add("  - Canonical closure surfaces: use UpdateGuardians/canonical guardians, set activeGuardian to the requested Guardian, and update chaosSeaNavigation.currentAbodeId to that Guardian's Abode.");
                parts.Add("  - The result must point to the requested guardian, not a nearby approximation.");
                parts.Add("  - Keep the guardian's canonical identity stable even if their visible manifestation changes.");
                parts.Add($"  - Target preset: {attractionRequest.TargetPresetDisplayName} ({attractionRequest.TargetPresetId}, {attractionRequest.SourceLibrary}, v{attractionRequest.TargetPresetVersion})");
                if (!string.IsNullOrWhiteSpace(attractionRequest.TargetSummary))
                    parts.Add($"  - Summary: {attractionRequest.TargetSummary}");
                parts.Add("  - Rendered dossier:");
                parts.Add(IndentMultiline(attractionRequest.RenderedPromptPackage, "    "));
            }
            else
            {
                parts.Add("ETERNAL GUARDIAN ATTRACTION WRONG-REALM REPAIR:");
                parts.Add("  - system_guardian_attraction.json is Chaos Sea-only and current realm is not Chaos Sea.");
                parts.Add("  - Treat this file as repair-only evidence; do not close this Shining Abode turn from it.");
                parts.Add($"  - Target preset retained for audit: {attractionRequest.TargetPresetDisplayName} ({attractionRequest.TargetPresetId}, {attractionRequest.SourceLibrary}, v{attractionRequest.TargetPresetVersion})");
                parts.Add("  - Preserve it for client repair or explicit client cancellation.");
            }
        }

        return string.Join(Environment.NewLine, parts.Where(part => !string.IsNullOrWhiteSpace(part)));
    }

    private async Task<AttractionRequestReadState> ReadAttractionRequestStateAsync()
    {
        var json = await _fs.ReadFileAsync(AttractionRequestPath);
        if (string.IsNullOrWhiteSpace(json))
            return new AttractionRequestReadState(_fs.FileExists(AttractionRequestPath), _fs.FileExists(AttractionRequestPath), null);

        try
        {
            var request = JsonSerializer.Deserialize<SystemGuardianAttractionRequest>(json, JsonOpts);
            if (request == null ||
                !string.Equals(request.Mode, "system_guardian_attraction", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(request.TargetPresetId) ||
                string.IsNullOrWhiteSpace(request.TargetPresetDisplayName))
            {
                return new AttractionRequestReadState(true, true, null);
            }

            return new AttractionRequestReadState(true, false, request);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось прочитать system guardian attraction request");
            return new AttractionRequestReadState(true, true, null);
        }
    }

    public string BuildAttractionActionText(SystemGuardianPresetDescriptor preset)
    {
        return $"[CHAOS_SEA_SYSTEM_GUARDIAN_ATTRACTION: {preset.PresetId}] " +
               $"Душа намеренно тянется к системному Хранителю «{preset.DisplayName}». " +
               "Это целенаправленный поиск конкретной личности, а не свободный запрос по настроению. " +
               "Обработай ход так, чтобы результат привёл именно к этому Хранителю.";
    }

    private async Task<List<SystemGuardianPresetDescriptor>> LoadDirectoryLayerAsync(string rootDir, string libraryKind, bool includeDossier)
    {
        Directory.CreateDirectory(rootDir);

        var directories = Directory
            .EnumerateDirectories(rootDir, "*", SearchOption.TopDirectoryOnly)
            .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var result = new List<SystemGuardianPresetDescriptor>();
        foreach (var dir in directories)
        {
            var descriptor = await TryBuildDescriptorAsync(dir, libraryKind, includeDossier);
            if (descriptor != null)
                result.Add(descriptor);
        }

        return result;
    }

    private async Task<SystemGuardianPresetDescriptor?> TryBuildDescriptorAsync(string directoryPath, string libraryKind, bool includeDossier)
    {
        var manifestPath = Path.Combine(directoryPath, "manifest.json");
        var dossierPath = Path.Combine(directoryPath, "dossier.md");
        if (!File.Exists(manifestPath) || !File.Exists(dossierPath))
        {
            _logger.LogWarning("Системный хранитель в {Directory} пропущен: ожидаются manifest.json и dossier.md.", directoryPath);
            return null;
        }

        GuardianPresetManifest? manifest;
        try
        {
            var manifestJson = await File.ReadAllTextAsync(manifestPath, Encoding.UTF8);
            manifest = JsonSerializer.Deserialize<GuardianPresetManifest>(manifestJson, JsonOpts);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось прочитать manifest.json у системного хранителя {Directory}", directoryPath);
            return null;
        }

        if (manifest == null ||
            string.IsNullOrWhiteSpace(manifest.PresetId) ||
            string.IsNullOrWhiteSpace(manifest.DisplayName) ||
            string.IsNullOrWhiteSpace(manifest.Identity.Domain) ||
            string.IsNullOrWhiteSpace(manifest.Abode.Name))
        {
            _logger.LogWarning("Системный хранитель в {Directory} пропущен: manifest missing required fields.", directoryPath);
            return null;
        }

        string dossierMarkdown;
        try
        {
            dossierMarkdown = await File.ReadAllTextAsync(dossierPath, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось прочитать dossier.md у системного хранителя {Directory}", directoryPath);
            return null;
        }

        if (string.IsNullOrWhiteSpace(dossierMarkdown))
        {
            _logger.LogWarning("Системный хранитель в {Directory} пропущен: dossier.md пуст.", directoryPath);
            return null;
        }

        var descriptor = new SystemGuardianPresetDescriptor
        {
            PresetId = manifest.PresetId.Trim(),
            DisplayName = manifest.DisplayName.Trim(),
            Summary = manifest.Summary?.Trim() ?? "",
            LibraryKind = libraryKind,
            Version = string.IsNullOrWhiteSpace(manifest.Authoring.Version) ? "1.0" : manifest.Authoring.Version.Trim(),
            Domain = manifest.Identity.Domain.Trim(),
            Archetype = manifest.Identity.Archetype?.Trim() ?? "",
            Tone = manifest.Identity.Tone?.Trim() ?? "",
            CoreValues = manifest.Identity.CoreValues.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v.Trim()).ToArray(),
            DefaultNameVariant = string.IsNullOrWhiteSpace(manifest.NameVariants.Default)
                ? manifest.DisplayName.Trim()
                : manifest.NameVariants.Default.Trim(),
            FeminineNameVariant = string.IsNullOrWhiteSpace(manifest.NameVariants.Feminine) ? null : manifest.NameVariants.Feminine.Trim(),
            MasculineNameVariant = string.IsNullOrWhiteSpace(manifest.NameVariants.Masculine) ? null : manifest.NameVariants.Masculine.Trim(),
            NeutralNameVariant = string.IsNullOrWhiteSpace(manifest.NameVariants.Neutral) ? null : manifest.NameVariants.Neutral.Trim(),
            FormFlexibility = GuardianManifestation.IsValidFormFlexibility(manifest.ManifestationDefaults.FormFlexibility)
                ? manifest.ManifestationDefaults.FormFlexibility.Trim().ToLowerInvariant()
                : GuardianManifestation.FixedFlexibility,
            DefaultPresentationStyle = string.IsNullOrWhiteSpace(manifest.ManifestationDefaults.DefaultPresentationStyle)
                ? "neutral"
                : manifest.ManifestationDefaults.DefaultPresentationStyle.Trim(),
            DefaultPronouns = string.IsNullOrWhiteSpace(manifest.ManifestationDefaults.DefaultPronouns)
                ? "они/их"
                : manifest.ManifestationDefaults.DefaultPronouns.Trim(),
            DefaultAppearanceDescription = manifest.ManifestationDefaults.AppearanceDescription?.Trim() ?? "",
            AbodeName = manifest.Abode.Name.Trim(),
            AbodeTheme = manifest.Abode.Theme?.Trim() ?? "",
            MustPreserve = manifest.GenerationRules.MustPreserve.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v.Trim()).ToArray(),
            CanVary = manifest.GenerationRules.CanVary.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v.Trim()).ToArray(),
            Forbidden = manifest.GenerationRules.Forbidden.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v.Trim()).ToArray(),
            SearchLabel = string.IsNullOrWhiteSpace(manifest.SearchAttraction.Label)
                ? $"Притяжение к {manifest.DisplayName.Trim()}"
                : manifest.SearchAttraction.Label.Trim(),
            SearchKeywords = manifest.SearchAttraction.Keywords.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v.Trim()).ToArray(),
            DirectoryName = Path.GetFileName(directoryPath),
            DirectoryPath = directoryPath,
            ManifestPath = manifestPath,
            DossierPath = dossierPath,
            DossierMarkdown = includeDossier ? dossierMarkdown : null,
            PromptPackage = BuildPromptPackage(manifest, dossierMarkdown)
        };

        return descriptor;
    }

    private async Task<SystemGuardianAttractionRequest?> ReadPendingGuardianCreationPresetAsync()
    {
        var json = await _fs.ReadFileAsync("game_state/meta/guardians.json");
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            if (JsonNode.Parse(json) is not JsonObject root ||
                root["pendingGuardianCreation"] is not JsonObject pending)
            {
                return null;
            }

            var mode = pending["mode"]?.GetValue<string>() ?? "";
            if (!string.Equals(mode, "system_preset", StringComparison.OrdinalIgnoreCase))
                return null;

            var presetId = pending["presetId"]?.GetValue<string>() ?? "";
            var displayName = pending["presetDisplayName"]?.GetValue<string>() ?? "";
            var version = pending["presetVersion"]?.GetValue<string>() ?? "1.0";
            var sourceLibrary = pending["sourceLibrary"]?.GetValue<string>() ?? "";
            var summary = pending["description"]?.GetValue<string>() ?? "";
            var rendered = pending["renderedPromptPackage"]?.GetValue<string>() ?? "";

            if (string.IsNullOrWhiteSpace(rendered) && !string.IsNullOrWhiteSpace(presetId))
            {
                var resolved = await FindPresetAsync(presetId, includeDossier: true);
                if (resolved != null)
                {
                    rendered = resolved.PromptPackage;
                    displayName = string.IsNullOrWhiteSpace(displayName) ? resolved.DisplayName : displayName;
                    summary = string.IsNullOrWhiteSpace(summary) ? resolved.Summary : summary;
                    sourceLibrary = string.IsNullOrWhiteSpace(sourceLibrary) ? resolved.LibraryKind : sourceLibrary;
                    version = string.IsNullOrWhiteSpace(version) ? resolved.Version : version;
                }
            }

            if (string.IsNullOrWhiteSpace(presetId) || string.IsNullOrWhiteSpace(displayName))
                return null;

            return new SystemGuardianAttractionRequest
            {
                Mode = "system_preset",
                TargetPresetId = presetId,
                TargetPresetDisplayName = displayName,
                TargetPresetVersion = version,
                SourceLibrary = sourceLibrary,
                TargetSummary = summary,
                RenderedPromptPackage = rendered
            };
        }
        catch
        {
            return null;
        }
    }

    private static string BuildPromptPackage(GuardianPresetManifest manifest, string dossierMarkdown)
    {
        var defaultNameVariant = string.IsNullOrWhiteSpace(manifest.NameVariants.Default)
            ? manifest.DisplayName
            : manifest.NameVariants.Default;
        var formFlexibility = GuardianManifestation.IsValidFormFlexibility(manifest.ManifestationDefaults.FormFlexibility)
            ? manifest.ManifestationDefaults.FormFlexibility
            : GuardianManifestation.FixedFlexibility;
        var defaultPresentationStyle = string.IsNullOrWhiteSpace(manifest.ManifestationDefaults.DefaultPresentationStyle)
            ? "neutral"
            : manifest.ManifestationDefaults.DefaultPresentationStyle;
        var defaultPronouns = string.IsNullOrWhiteSpace(manifest.ManifestationDefaults.DefaultPronouns)
            ? "они/их"
            : manifest.ManifestationDefaults.DefaultPronouns;

        var lines = new List<string>
        {
            $"PresetId: {manifest.PresetId}",
            $"DisplayName: {manifest.DisplayName}",
            $"Summary: {manifest.Summary}",
            $"Domain: {manifest.Identity.Domain}",
            $"Archetype: {manifest.Identity.Archetype}",
            $"Tone: {manifest.Identity.Tone}",
            $"CoreValues: {string.Join(", ", manifest.Identity.CoreValues)}",
            $"CanonicalName: {manifest.DisplayName}",
            $"NameVariant.default: {defaultNameVariant}",
            $"FormFlexibility: {formFlexibility}",
            $"DefaultPresentationStyle: {defaultPresentationStyle}",
            $"DefaultPronouns: {defaultPronouns}",
            $"AbodeName: {manifest.Abode.Name}",
            $"AbodeTheme: {manifest.Abode.Theme}"
        };

        if (!string.IsNullOrWhiteSpace(manifest.NameVariants.Feminine))
            lines.Add($"NameVariant.feminine: {manifest.NameVariants.Feminine}");
        if (!string.IsNullOrWhiteSpace(manifest.NameVariants.Masculine))
            lines.Add($"NameVariant.masculine: {manifest.NameVariants.Masculine}");
        if (!string.IsNullOrWhiteSpace(manifest.NameVariants.Neutral))
            lines.Add($"NameVariant.neutral: {manifest.NameVariants.Neutral}");
        if (!string.IsNullOrWhiteSpace(manifest.ManifestationDefaults.AppearanceDescription))
            lines.Add($"DefaultAppearanceDescription: {manifest.ManifestationDefaults.AppearanceDescription}");
        if (manifest.GenerationRules.MustPreserve.Count > 0)
            lines.Add($"MustPreserve: {string.Join(" | ", manifest.GenerationRules.MustPreserve)}");
        if (manifest.GenerationRules.CanVary.Count > 0)
            lines.Add($"CanVary: {string.Join(" | ", manifest.GenerationRules.CanVary)}");
        if (manifest.GenerationRules.Forbidden.Count > 0)
            lines.Add($"Forbidden: {string.Join(" | ", manifest.GenerationRules.Forbidden)}");
        if (manifest.SearchAttraction.Keywords.Count > 0)
            lines.Add($"SearchKeywords: {string.Join(", ", manifest.SearchAttraction.Keywords)}");

        lines.Add("");
        lines.Add("Guardian dossier:");
        lines.Add(dossierMarkdown.Trim());

        return string.Join(Environment.NewLine, lines.Where(line => line != null));
    }

    private void EnsurePresetDirectories()
    {
        Directory.CreateDirectory(GetBuiltInDirectoryPath());
        Directory.CreateDirectory(GetUserDirectoryPath());
    }

    private static string IndentMultiline(string text, string indent)
    {
        if (string.IsNullOrWhiteSpace(text))
            return indent + "(empty)";

        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        return string.Join(Environment.NewLine, lines.Select(line => indent + line));
    }
}
