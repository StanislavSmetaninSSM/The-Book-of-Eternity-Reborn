using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class SystemGuardianLibraryServiceTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly SystemGuardianLibraryService _service;

    public SystemGuardianLibraryServiceTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-system-guardians-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
        _service = new SystemGuardianLibraryService(_fs, NullLogger<SystemGuardianLibraryService>.Instance);
    }

    [Fact]
    public async Task GetAvailablePresetsAsync_WithCleanSession_LoadsRepoShippedBuiltInPresets()
    {
        var builtInDir = _service.GetBuiltInDirectoryPath();
        Assert.False(Directory.Exists(builtInDir) && Directory.EnumerateDirectories(builtInDir).Any());

        var presets = await _service.GetAvailablePresetsAsync(includeDossier: true);

        Assert.NotEmpty(presets);
        Assert.Contains(presets, preset =>
            string.Equals(preset.PresetId, "azalia", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(preset.LibraryKind, "built_in", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(preset.DossierMarkdown));
    }

    [Fact]
    public async Task GetAvailablePresetsAsync_BuiltInWinsIdConflict_AndUserPresetStillLoadsForUniqueId()
    {
        await SeedPresetAsync(_service.GetBuiltInDirectoryPath(), "azalia", "Азалия", "Social", "built_in");
        await SeedPresetAsync(_service.GetUserDirectoryPath(), "azalia", "Пользовательская Азалия", "Magic", "user");
        await SeedPresetAsync(_service.GetUserDirectoryPath(), "my_user_guardian", "Мой Хранитель", "Knowledge", "user");

        var presets = await _service.GetAvailablePresetsAsync(includeDossier: true);

        var azalia = Assert.Single(presets, p => p.PresetId == "azalia");
        Assert.Equal("Азалия", azalia.DisplayName);
        Assert.Equal("built_in", azalia.LibraryKind);
        Assert.Equal("Азалия", azalia.DefaultNameVariant);
        Assert.Equal("selective", azalia.FormFlexibility);
        Assert.Contains("CanonicalName: Азалия", azalia.PromptPackage, StringComparison.Ordinal);
        Assert.Contains("DefaultPresentationStyle: feminine", azalia.PromptPackage, StringComparison.Ordinal);

        var userGuardian = Assert.Single(presets, p => p.PresetId == "my_user_guardian");
        Assert.Equal("user", userGuardian.LibraryKind);
        Assert.Contains("Guardian dossier:", userGuardian.PromptPackage, StringComparison.Ordinal);
        Assert.DoesNotContain(presets, p =>
            p.PresetId == "azalia" &&
            string.Equals(p.LibraryKind, "user", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task BuildReminderFragmentAsync_IncludesPendingPresetAndAttractionRequest()
    {
        await SeedPresetAsync(_service.GetBuiltInDirectoryPath(), "azalia", "Азалия", "Social", "built_in");
        var preset = await _service.FindPresetAsync("azalia", includeDossier: true);
        Assert.NotNull(preset);

        await _fs.WriteFileAtomicAsync("game_state/meta/guardians.json", JsonSerializer.Serialize(new
        {
            guardians = Array.Empty<object>(),
            pendingGuardianCreation = _service.BuildPendingGuardianCreationNode(preset!, "Тестовая Душа")
        }, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        }));

        await _service.WriteAttractionRequestAsync(preset!);

        var reminder = await _service.BuildReminderFragmentAsync("Chaos Sea");

        Assert.Contains("ETERNAL GUARDIAN PRESET:", reminder, StringComparison.Ordinal);
        Assert.Contains("ETERNAL GUARDIAN ATTRACTION:", reminder, StringComparison.Ordinal);
        Assert.Contains("Азалия", reminder, StringComparison.Ordinal);
        Assert.Contains("guardian.sourcePreset", reminder, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildCanonicalGuardianRootForFreshNewGame_SystemPreset_UsesCompleteCanonicalShapeWithoutPendingCreation()
    {
        await SeedPresetAsync(_service.GetBuiltInDirectoryPath(), "azalia", "Азалия", "Social", "built_in");
        var preset = await _service.FindPresetAsync("azalia", includeDossier: true);
        Assert.NotNull(preset);

        var root = _service.BuildCanonicalGuardianRootForFreshNewGame(
            preset!,
            "Тестовая Душа",
            turnNumber: 1,
            createdAtUtc: DateTimeOffset.Parse("2026-06-29T00:00:00Z"));

        Assert.Null(root["pendingGuardianCreation"]);

        var guardians = Assert.IsType<JsonArray>(root["guardians"]);
        var guardian = Assert.IsType<JsonObject>(Assert.Single(guardians));
        var activeGuardian = Assert.IsType<JsonObject>(root["activeGuardian"]);

        Assert.Equal("guard_system_azalia_001", guardian["guardianId"]?.GetValue<string>());
        Assert.Equal("guard_system_azalia_001", activeGuardian["guardianId"]?.GetValue<string>());
        Assert.Equal("Азалия", guardian["canonicalName"]?.GetValue<string>());
        Assert.Equal("system_preset", guardian["originType"]?.GetValue<string>());
        Assert.Equal("azalia", guardian["sourcePreset"]?["presetId"]?.GetValue<string>());
        Assert.Equal("built_in", guardian["sourcePreset"]?["library"]?.GetValue<string>());
        var nameVariants = Assert.IsType<JsonObject>(guardian["nameVariants"]);
        Assert.Equal("Азалия", nameVariants["default"]?.GetValue<string>());
        Assert.Equal("Азалия", nameVariants["feminine"]?.GetValue<string>());
        Assert.Equal("Азалия", nameVariants["masculine"]?.GetValue<string>());
        Assert.Equal("Азалия", nameVariants["neutral"]?.GetValue<string>());

        var manifestation = Assert.IsType<JsonObject>(guardian["manifestation"]);
        Assert.Equal("selective", manifestation["formFlexibility"]?.GetValue<string>());
        Assert.Equal("Азалия", manifestation["currentDisplayName"]?.GetValue<string>());
        Assert.Equal("она/её", manifestation["currentPronouns"]?.GetValue<string>());
        Assert.IsType<JsonArray>(guardian["manifestationHistory"]);

        var abode = Assert.IsType<JsonObject>(guardian["abode"]);
        Assert.Equal("abode_system_azalia_001", abode["abodeId"]?.GetValue<string>());
        Assert.Equal("Тестовая Обитель", abode["name"]?.GetValue<string>());
        Assert.True(abode["isDiscovered"]?.GetValue<bool>());

        Assert.IsType<JsonObject>(guardian["personalityProfile"]);
        Assert.IsType<JsonObject>(guardian["mood"]);
        Assert.IsType<JsonObject>(guardian["relationshipData"]);
        Assert.IsType<JsonObject>(guardian["abodePower"]);
        Assert.IsType<JsonArray>(guardian["guardianRelationships"]);
        Assert.IsType<JsonObject>(guardian["questManagement"]);
        Assert.IsType<JsonObject>(guardian["gachaSystem"]);

        var loreFragments = Assert.IsType<JsonArray>(guardian["loreFragments"]);
        Assert.True(loreFragments.Count >= 7);
        foreach (var fragmentNode in loreFragments)
        {
            var fragment = Assert.IsType<JsonObject>(fragmentNode);
            Assert.False(string.IsNullOrWhiteSpace(fragment["fragmentId"]?.GetValue<string>()));
            Assert.False(string.IsNullOrWhiteSpace(fragment["title"]?.GetValue<string>()));
            Assert.False(string.IsNullOrWhiteSpace(fragment["summary"]?.GetValue<string>()));
            Assert.False(string.IsNullOrWhiteSpace(fragment["category"]?.GetValue<string>()));
            Assert.Equal("planned", fragment["discoveryState"]?.GetValue<string>());
            Assert.Equal("hidden", fragment["visibility"]?.GetValue<string>());
            Assert.Equal("azalia", fragment["sourcePresetId"]?.GetValue<string>());
        }

        var navigation = Assert.IsType<JsonObject>(root["chaosSeaNavigation"]);
        Assert.Equal("abode_system_azalia_001", navigation["currentAbodeId"]?.GetValue<string>());
        Assert.Equal("guard_system_azalia_001", navigation["currentGuardianId"]?.GetValue<string>());
        var discoveredAbodes = Assert.IsType<JsonArray>(navigation["discoveredAbodes"]);
        Assert.Equal("abode_system_azalia_001", Assert.Single(discoveredAbodes)?.GetValue<string>());
    }

    [Fact]
    public async Task BuildAfterlifeEntityProfileRootForFreshNewGame_SystemPreset_CreatesValidMentorProfile()
    {
        await SeedPresetAsync(_service.GetBuiltInDirectoryPath(), "azalia", "Азалия", "Social", "built_in");
        var preset = await _service.FindPresetAsync("azalia", includeDossier: true);
        Assert.NotNull(preset);

        const int turnNumber = 1;
        var createdAtUtc = DateTimeOffset.Parse("2026-06-29T00:00:00Z");

        var root = _service.BuildAfterlifeEntityProfileRootForFreshNewGame(
            preset!,
            "Тестовая Душа",
            turnNumber,
            createdAtUtc);

        Assert.Equal(1, root["schemaVersion"]?.GetValue<int>());
        var profiles = Assert.IsType<JsonArray>(root["profiles"]);
        var profile = Assert.IsType<JsonObject>(Assert.Single(profiles));
        Assert.Equal("guardian", profile["actorType"]?.GetValue<string>());
        Assert.Equal("guard_system_azalia_001", profile["actorId"]?.GetValue<string>());
        Assert.Equal("Азалия", profile["displayName"]?.GetValue<string>());
        Assert.Equal("Chaos Sea", profile["realm"]?.GetValue<string>());
        Assert.Equal("Тестовая Обитель", profile["locationName"]?.GetValue<string>());

        var mentorProfile = Assert.IsType<JsonObject>(profile["mentorProfile"]);
        Assert.True(mentorProfile["canTeach"]?.GetValue<bool>());
        Assert.Equal(0, mentorProfile["relationshipLevel"]?.GetValue<int>());

        var standardArts = Assert.IsType<JsonObject>(profile["standardArts"]);
        Assert.Equal(2, standardArts["guard"]?.GetValue<int>());
        Assert.Equal(1, standardArts["maneuver"]?.GetValue<int>());
        Assert.IsType<JsonArray>(profile["specialArts"]);
        Assert.Equal(0, profile["soulDissipationTier"]?.GetValue<int>());

        var progressionStrategy = Assert.IsType<JsonObject>(profile["progressionStrategy"]);
        Assert.Equal("strategy_system_guardian_azalia", progressionStrategy["strategyId"]?.GetValue<string>());
        var priorityOrder = Assert.IsType<JsonArray>(progressionStrategy["priorityOrder"]);
        Assert.Contains(priorityOrder, item => item?.GetValue<string>() == "guard");

        var ledger = Assert.IsType<JsonArray>(profile["ledger"]);
        var entry = Assert.IsType<JsonObject>(Assert.Single(ledger));
        Assert.Equal("system_guardian_profile_bootstrap_azalia", entry["entryId"]?.GetValue<string>());

        AssertCompleteSystemGuardianMaterialization(profile, "guard_system_azalia_001", turnNumber);

        var repeatedRoot = _service.BuildAfterlifeEntityProfileRootForFreshNewGame(
            preset,
            "Тестовая Душа",
            turnNumber,
            createdAtUtc);
        var repeatedProfile = GetOnlyAfterlifeProfile(repeatedRoot);
        Assert.True(
            JsonNode.DeepEquals(profile, repeatedProfile),
            "Identical preset builder inputs must produce a semantically identical profile.");

        var sameActorAndTurnRoot = _service.BuildAfterlifeEntityProfileRootForFreshNewGame(
            preset,
            "Другая Душа",
            turnNumber,
            createdAtUtc.AddDays(1));
        Assert.Equal(
            profile["materialization"]?["materializationId"]?.GetValue<string>(),
            GetOnlyAfterlifeProfile(sameActorAndTurnRoot)["materialization"]?["materializationId"]?.GetValue<string>());

        AssertDirectSystemGuardianMaterializationValidationPasses(profile);

        await _fs.WriteFileAtomicAsync(AfterlifeEntityProfileState.StatePath, root.ToJsonString());
        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            issue.Code?.StartsWith("afterlife_entity_profile_", StringComparison.OrdinalIgnoreCase) == true);
        Assert.DoesNotContain(issues, IsActorMaterializationIssue);
    }

    [Fact]
    public void BuildCanonicalGuardianRootForFreshNewGame_Freeform_UsesClientOwnedCanonicalSeed()
    {
        const string description = "Хранительница Селена Теневая: покровительница забытых библиотек, тайных сделок и осторожной мудрости.";

        var root = _service.BuildCanonicalGuardianRootForFreshNewGame(
            description,
            "Искра Перед Рассветом",
            turnNumber: 1,
            createdAtUtc: DateTimeOffset.Parse("2026-06-29T00:00:00Z"));

        Assert.Null(root["pendingGuardianCreation"]);

        var guardians = Assert.IsType<JsonArray>(root["guardians"]);
        var guardian = Assert.IsType<JsonObject>(Assert.Single(guardians));
        var activeGuardian = Assert.IsType<JsonObject>(root["activeGuardian"]);

        var guardianId = guardian["guardianId"]?.GetValue<string>();
        Assert.False(string.IsNullOrWhiteSpace(guardianId));
        Assert.Equal(guardianId, activeGuardian["guardianId"]?.GetValue<string>());
        Assert.Equal("freeform", guardian["originType"]?.GetValue<string>());
        Assert.Equal("Хранительница Селена Теневая", guardian["canonicalName"]?.GetValue<string>());
        Assert.Equal(description, guardian["freeformSourceDescription"]?.GetValue<string>());

        var loreFragments = Assert.IsType<JsonArray>(guardian["loreFragments"]);
        Assert.True(loreFragments.Count >= 7);
        foreach (var fragmentNode in loreFragments)
        {
            var fragment = Assert.IsType<JsonObject>(fragmentNode);
            Assert.False(string.IsNullOrWhiteSpace(fragment["fragmentId"]?.GetValue<string>()));
            Assert.False(string.IsNullOrWhiteSpace(fragment["summary"]?.GetValue<string>()));
            Assert.False(string.IsNullOrWhiteSpace(fragment["category"]?.GetValue<string>()));
        }

        var navigation = Assert.IsType<JsonObject>(root["chaosSeaNavigation"]);
        Assert.Equal(guardian["abode"]?["abodeId"]?.GetValue<string>(), navigation["currentAbodeId"]?.GetValue<string>());
        Assert.Equal(guardianId, navigation["currentGuardianId"]?.GetValue<string>());
    }

    [Fact]
    public void FreshFreeformGuardianMaterialization_UsesNeutralSemanticSeedIndependentOfDescription()
    {
        const string knowledgeTradeDescription =
            "Хранительница Селена Теневая: покровительница библиотек, архивов, мудрости и торговых сделок.";
        const string combatHealingDescription =
            "Хранительница Селена Теневая: воительница клинков, битв, охоты и исцеления.";
        const int turnNumber = 7;
        var createdAtUtc = DateTimeOffset.Parse("2026-06-29T00:00:00Z");

        var knowledgeGuardianRoot = _service.BuildCanonicalGuardianRootForFreshNewGame(
            knowledgeTradeDescription,
            "Искра Перед Рассветом",
            turnNumber,
            createdAtUtc);
        var combatGuardianRoot = _service.BuildCanonicalGuardianRootForFreshNewGame(
            combatHealingDescription,
            "Искра Перед Рассветом",
            turnNumber,
            createdAtUtc);
        var knowledgeGuardian = Assert.IsType<JsonObject>(
            Assert.Single(Assert.IsType<JsonArray>(knowledgeGuardianRoot["guardians"])));
        var combatGuardian = Assert.IsType<JsonObject>(
            Assert.Single(Assert.IsType<JsonArray>(combatGuardianRoot["guardians"])));

        Assert.Equal(knowledgeTradeDescription, knowledgeGuardian["freeformSourceDescription"]?.GetValue<string>());
        Assert.Equal(combatHealingDescription, combatGuardian["freeformSourceDescription"]?.GetValue<string>());
        Assert.Equal("General", knowledgeGuardian["domain"]?.GetValue<string>());
        Assert.Equal(
            knowledgeGuardian["domain"]?.GetValue<string>(),
            combatGuardian["domain"]?.GetValue<string>());

        var knowledgeProfile = GetOnlyAfterlifeProfile(
            _service.BuildAfterlifeEntityProfileRootForFreshNewGame(
                knowledgeTradeDescription,
                "Искра Перед Рассветом",
                turnNumber,
                createdAtUtc));
        var combatProfile = GetOnlyAfterlifeProfile(
            _service.BuildAfterlifeEntityProfileRootForFreshNewGame(
                combatHealingDescription,
                "Искра Перед Рассветом",
                turnNumber,
                createdAtUtc));

        Assert.Equal(knowledgeTradeDescription, knowledgeProfile["freeformSourceDescription"]?.GetValue<string>());
        Assert.Equal(combatHealingDescription, combatProfile["freeformSourceDescription"]?.GetValue<string>());
        var expectedStandardArts = new JsonObject
        {
            ["guard"] = 2,
            ["maneuver"] = 1
        };
        Assert.True(JsonNode.DeepEquals(expectedStandardArts, knowledgeProfile["standardArts"]));
        Assert.True(JsonNode.DeepEquals(knowledgeProfile["standardArts"], combatProfile["standardArts"]));

        var knowledgeCapabilities = Assert.IsType<JsonObject>(
            knowledgeProfile[ActorMaterializationContract.PropertyName]?["capabilities"]);
        var combatCapabilities = Assert.IsType<JsonObject>(
            combatProfile[ActorMaterializationContract.PropertyName]?["capabilities"]);
        var expectedCapabilities = new JsonObject
        {
            ["canFight"] = true,
            ["canTeach"] = true,
            ["canTrade"] = false
        };
        Assert.True(JsonNode.DeepEquals(expectedCapabilities, knowledgeCapabilities));
        Assert.True(JsonNode.DeepEquals(knowledgeCapabilities, combatCapabilities));
    }

    [Fact]
    public async Task BuildAfterlifeEntityProfileRootForFreshNewGame_Freeform_CreatesValidMentorProfile()
    {
        const string description = "Хранительница Селена Теневая: покровительница забытых библиотек, тайных сделок и осторожной мудрости.";
        const int turnNumber = 7;
        var createdAtUtc = DateTimeOffset.Parse("2026-06-29T00:00:00Z");

        var root = _service.BuildAfterlifeEntityProfileRootForFreshNewGame(
            description,
            "Искра Перед Рассветом",
            turnNumber,
            createdAtUtc);

        Assert.Equal(1, root["schemaVersion"]?.GetValue<int>());
        var profiles = Assert.IsType<JsonArray>(root["profiles"]);
        var profile = Assert.IsType<JsonObject>(Assert.Single(profiles));
        Assert.Equal("guardian", profile["actorType"]?.GetValue<string>());
        Assert.Equal("Хранительница Селена Теневая", profile["displayName"]?.GetValue<string>());
        Assert.Equal("Chaos Sea", profile["realm"]?.GetValue<string>());
        Assert.Equal("freeform", profile["originType"]?.GetValue<string>());
        Assert.Equal(description, profile["freeformSourceDescription"]?.GetValue<string>());

        var mentorProfile = Assert.IsType<JsonObject>(profile["mentorProfile"]);
        Assert.True(mentorProfile["canTeach"]?.GetValue<bool>());
        var standardArts = Assert.IsType<JsonObject>(profile["standardArts"]);
        Assert.Equal(2, standardArts["guard"]?.GetValue<int>());
        Assert.IsType<JsonArray>(profile["relationships"]);

        AssertCompleteSystemGuardianMaterialization(profile, "guard_freeform_guardian_001", turnNumber);

        var repeatedRoot = _service.BuildAfterlifeEntityProfileRootForFreshNewGame(
            description,
            "Искра Перед Рассветом",
            turnNumber,
            createdAtUtc);
        var repeatedProfile = GetOnlyAfterlifeProfile(repeatedRoot);
        Assert.True(
            JsonNode.DeepEquals(profile, repeatedProfile),
            "Identical freeform builder inputs must produce a semantically identical profile.");

        var sameActorAndTurnRoot = _service.BuildAfterlifeEntityProfileRootForFreshNewGame(
            description,
            "Другая Душа",
            turnNumber,
            createdAtUtc.AddDays(1));
        Assert.Equal(
            profile["materialization"]?["materializationId"]?.GetValue<string>(),
            GetOnlyAfterlifeProfile(sameActorAndTurnRoot)["materialization"]?["materializationId"]?.GetValue<string>());

        AssertDirectSystemGuardianMaterializationValidationPasses(profile);

        await _fs.WriteFileAtomicAsync(AfterlifeEntityProfileState.StatePath, root.ToJsonString());
        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            issue.Code?.StartsWith("afterlife_entity_profile_", StringComparison.OrdinalIgnoreCase) == true);
        Assert.DoesNotContain(issues, IsActorMaterializationIssue);
    }

    [Fact]
    public async Task BuildReminderFragmentAsync_ShiningAbodeTreatsAttractionAsRepairOnly()
    {
        await SeedPresetAsync(_service.GetBuiltInDirectoryPath(), "azalia", "Азалия", "Social", "built_in");
        var preset = await _service.FindPresetAsync("azalia", includeDossier: true);
        Assert.NotNull(preset);
        await _service.WriteAttractionRequestAsync(preset!);

        var reminder = await _service.BuildReminderFragmentAsync("Shining Abode");

        Assert.Contains("WRONG-REALM REPAIR", reminder, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Chaos Sea-only", reminder, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("create and materialize", reminder, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("route the soul", reminder, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BuildReminderFragmentAsync_ChaosSeaKeepsAttractionClosureInstructions()
    {
        await SeedPresetAsync(_service.GetBuiltInDirectoryPath(), "azalia", "Азалия", "Social", "built_in");
        var preset = await _service.FindPresetAsync("azalia", includeDossier: true);
        Assert.NotNull(preset);
        await _service.WriteAttractionRequestAsync(preset!);

        var reminder = await _service.BuildReminderFragmentAsync("Chaos Sea");

        Assert.Contains("ETERNAL GUARDIAN ATTRACTION:", reminder, StringComparison.Ordinal);
        Assert.Contains("create and materialize", reminder, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("route the soul", reminder, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("UpdateGuardians", reminder, StringComparison.Ordinal);
        Assert.Contains("guardians", reminder, StringComparison.Ordinal);
        Assert.Contains("activeGuardian", reminder, StringComparison.Ordinal);
        Assert.Contains("chaosSeaNavigation", reminder, StringComparison.Ordinal);
        Assert.DoesNotContain("WRONG-REALM REPAIR", reminder, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EnsureAttractionRequestHealthyAsync_MalformedFile_IsPreservedAndSurfaced()
    {
        await _fs.WriteFileAtomicAsync(SystemGuardianLibraryService.AttractionRequestPath, "{ not valid json");

        await _service.EnsureAttractionRequestHealthyAsync("Chaos Sea");
        var reminder = await _service.BuildReminderFragmentAsync("Chaos Sea");

        Assert.True(_fs.FileExists(SystemGuardianLibraryService.AttractionRequestPath));
        Assert.Contains("CORRUPTION", reminder, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EnsureAttractionRequestHealthyAsync_UnresolvedRealm_PreservesPendingAttraction()
    {
        await SeedPresetAsync(_service.GetBuiltInDirectoryPath(), "azalia", "Азалия", "Social", "built_in");
        var preset = await _service.FindPresetAsync("azalia", includeDossier: true);
        Assert.NotNull(preset);
        await _service.WriteAttractionRequestAsync(preset!);

        await _service.EnsureAttractionRequestHealthyAsync("");

        Assert.True(_fs.FileExists(SystemGuardianLibraryService.AttractionRequestPath));
    }

    [Fact]
    public async Task EnsureAttractionRequestHealthyAsync_ResolvedActiveGuardianClearsAttractionOutsideActiveTurn()
    {
        await SeedPresetAsync(_service.GetBuiltInDirectoryPath(), "azalia", "Азалия", "Social", "built_in");
        var preset = await _service.FindPresetAsync("azalia", includeDossier: true);
        Assert.NotNull(preset);
        await _service.WriteAttractionRequestAsync(preset!);
        await _fs.WriteFileAtomicAsync("game_state/meta/guardians.json", """
        {
          "guardians": [],
          "activeGuardian": {
            "guardianId": "guardian_azalia",
            "canonicalName": "Азалия",
            "sourcePreset": { "presetId": "azalia", "displayName": "Азалия", "version": "1.0", "library": "built_in" }
          }
        }
        """);

        await _service.EnsureAttractionRequestHealthyAsync("Chaos Sea");

        Assert.False(_fs.FileExists(SystemGuardianLibraryService.AttractionRequestPath));
    }

    [Fact]
    public async Task EnsureAttractionRequestHealthyAsync_ActiveReadyPreservesResolvedAttractionUntilValidation()
    {
        await SeedPresetAsync(_service.GetBuiltInDirectoryPath(), "azalia", "Азалия", "Social", "built_in");
        var preset = await _service.FindPresetAsync("azalia", includeDossier: true);
        Assert.NotNull(preset);
        await _service.WriteAttractionRequestAsync(preset!);
        await _fs.WriteFileAtomicAsync("ready/turn_complete.json", """{ "accepted": true }""");
        await _fs.WriteFileAtomicAsync("game_state/meta/guardians.json", """
        {
          "guardians": [],
          "activeGuardian": {
            "guardianId": "guardian_azalia",
            "canonicalName": "Азалия",
            "sourcePreset": { "presetId": "azalia", "displayName": "Азалия", "version": "1.0", "library": "built_in" }
          }
        }
        """);

        await _service.EnsureAttractionRequestHealthyAsync("Chaos Sea");

        Assert.True(_fs.FileExists(SystemGuardianLibraryService.AttractionRequestPath));
    }

    [Fact]
    public async Task WriteAttractionRequestAsync_ExistingLiveRequest_BlocksReplacement()
    {
        await SeedPresetAsync(_service.GetBuiltInDirectoryPath(), "azalia", "Азалия", "Social", "built_in");
        await SeedPresetAsync(_service.GetBuiltInDirectoryPath(), "myriel", "Мириэль", "Lore", "built_in");
        var azalia = await _service.FindPresetAsync("azalia", includeDossier: true);
        var myriel = await _service.FindPresetAsync("myriel", includeDossier: true);
        Assert.NotNull(azalia);
        Assert.NotNull(myriel);

        await _service.WriteAttractionRequestAsync(azalia!);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.WriteAttractionRequestAsync(myriel!));
        Assert.Contains("не может быть заменён", ex.Message, StringComparison.OrdinalIgnoreCase);

        var request = await _service.ReadAttractionRequestAsync();
        Assert.NotNull(request);
        Assert.Equal("azalia", request!.TargetPresetId);
    }

    [Fact]
    public async Task BuiltInVeyraPreset_IsMaterializableAndUsesRussianPlayerFacingIntrigue()
    {
        var sourcePresetDir = GetRepoBuiltInPresetDirectory("veyra");

        Assert.True(Directory.Exists(sourcePresetDir), "Built-in Veyra preset directory must exist.");

        CopyDirectory(sourcePresetDir, Path.Combine(_service.GetBuiltInDirectoryPath(), "veyra"));

        var preset = await _service.FindPresetAsync("veyra", includeDossier: true);

        Assert.NotNull(preset);
        Assert.Equal("Вейра Серебряная Улыбка", preset!.DisplayName);
        Assert.Equal("built_in", preset.LibraryKind);
        Assert.Equal("Вейра Серебряная Улыбка", preset.DefaultNameVariant);
        Assert.Equal("она/её", preset.DefaultPronouns);
        Assert.Equal("Зеркальный Двор Без Имени", preset.AbodeName);
        Assert.Contains("мас", preset.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ложн", preset.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Притяжение к Вейре", preset.SearchLabel, StringComparison.Ordinal);
        Assert.Contains("маски", preset.SearchKeywords);
        Assert.Contains("двойные клятвы", preset.PromptPackage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("дублир", preset.PromptPackage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Passion", preset.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Devotion", preset.Summary, StringComparison.OrdinalIgnoreCase);

        var creation = _service.BuildPendingGuardianCreationNode(preset, "Тестовая Душа");

        Assert.Equal("veyra", creation["presetId"]?.GetValue<string>());
        Assert.Equal("Вейра Серебряная Улыбка", creation["presetDisplayName"]?.GetValue<string>());
        Assert.Equal("built_in", creation["sourceLibrary"]?.GetValue<string>());
    }

    [Fact]
    public async Task BuiltInLucianPreset_IsMaterializableAndUsesRussianPlayerFacingBladeMagic()
    {
        var sourcePresetDir = GetRepoBuiltInPresetDirectory("lucian");

        Assert.True(Directory.Exists(sourcePresetDir), "Built-in Lucian preset directory must exist.");

        CopyDirectory(sourcePresetDir, Path.Combine(_service.GetBuiltInDirectoryPath(), "lucian"));

        var preset = await _service.FindPresetAsync("lucian", includeDossier: true);

        Assert.NotNull(preset);
        Assert.Equal("Люциан Лунный Клинок", preset!.DisplayName);
        Assert.Equal("built_in", preset.LibraryKind);
        Assert.Equal("Люциан Лунный Клинок", preset.DefaultNameVariant);
        Assert.Equal("он/его", preset.DefaultPronouns);
        Assert.Equal("Чертог Лунного Клинка", preset.AbodeName);
        Assert.Contains("клин", preset.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("долг", preset.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Притяжение к Люциану", preset.SearchLabel, StringComparison.Ordinal);
        Assert.Contains("лунный клинок", preset.SearchKeywords);
        Assert.Contains("одинокий трагический воитель-маг", preset.PromptPackage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("не военный командир", preset.PromptPackage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("магия через движение клинка", preset.PromptPackage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Warmaster", preset.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ritual research", preset.Summary, StringComparison.OrdinalIgnoreCase);

        var creation = _service.BuildPendingGuardianCreationNode(preset, "Тестовая Душа");

        Assert.Equal("lucian", creation["presetId"]?.GetValue<string>());
        Assert.Equal("Люциан Лунный Клинок", creation["presetDisplayName"]?.GetValue<string>());
        Assert.Equal("built_in", creation["sourceLibrary"]?.GetValue<string>());
    }

    [Fact]
    public async Task BuiltInElyaraPreset_IsMaterializableAndUsesRussianPlayerFacingHealing()
    {
        var sourcePresetDir = GetRepoBuiltInPresetDirectory("elyara");

        Assert.True(Directory.Exists(sourcePresetDir), "Built-in Elyara preset directory must exist.");

        CopyDirectory(sourcePresetDir, Path.Combine(_service.GetBuiltInDirectoryPath(), "elyara"));

        var preset = await _service.FindPresetAsync("elyara", includeDossier: true);

        Assert.NotNull(preset);
        Assert.Equal("Элиара Последней Раны", preset!.DisplayName);
        Assert.Equal("built_in", preset.LibraryKind);
        Assert.Equal("Элиара Последней Раны", preset.DefaultNameVariant);
        Assert.Equal("она/её", preset.DefaultPronouns);
        Assert.Equal("Лазарет Незаживающего Света", preset.AbodeName);
        Assert.Contains("исцел", preset.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("шрам", preset.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Притяжение к Элиаре", preset.SearchLabel, StringComparison.Ordinal);
        Assert.Contains("исцеление", preset.SearchKeywords);
        Assert.Contains("Милость Незаживающей Раны", preset.PromptPackage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("не делать её наивной", preset.PromptPackage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("исцеление не стирает цену", preset.PromptPackage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("safe paradise", preset.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("free healing", preset.Summary, StringComparison.OrdinalIgnoreCase);

        var creation = _service.BuildPendingGuardianCreationNode(preset, "Тестовая Душа");

        Assert.Equal("elyara", creation["presetId"]?.GetValue<string>());
        Assert.Equal("Элиара Последней Раны", creation["presetDisplayName"]?.GetValue<string>());
        Assert.Equal("built_in", creation["sourceLibrary"]?.GetValue<string>());
    }

    [Fact]
    public async Task BuiltInExpandedDossier_IsPreservedInPromptPackageAndAttractionRequest()
    {
        var sourcePresetDir = GetRepoBuiltInPresetDirectory("elyara");

        Assert.True(Directory.Exists(sourcePresetDir), "Built-in Elyara preset directory must exist.");

        CopyDirectory(sourcePresetDir, Path.Combine(_service.GetBuiltInDirectoryPath(), "elyara"));

        var preset = await _service.FindPresetAsync("elyara", includeDossier: true);

        Assert.NotNull(preset);
        foreach (var requiredSection in new[]
                 {
                     "### 4. Манера речи",
                     "### 6. Романтический профиль",
                     "### 9. Библия Обители",
                     "### 13. Не играть как",
                     "Особое духовное искусство:",
                     "Полные четыре квеста находятся"
                 })
        {
            Assert.Contains(requiredSection, preset!.PromptPackage, StringComparison.Ordinal);
            Assert.Contains(requiredSection, preset.DossierMarkdown, StringComparison.Ordinal);
        }

        var request = _service.BuildAttractionRequest(preset!);

        Assert.Contains("Guardian dossier:", request.RenderedPromptPackage, StringComparison.Ordinal);
        Assert.Contains("### 6. Романтический профиль", request.RenderedPromptPackage, StringComparison.Ordinal);
        Assert.Contains("### 13. Не играть как", request.RenderedPromptPackage, StringComparison.Ordinal);
        Assert.Contains("Милость Незаживающей Раны", request.RenderedPromptPackage, StringComparison.Ordinal);
    }

    [Fact]
    public void BuiltInPermanentGuardianDossiers_FollowExpandedStandard()
    {
        var builtInRoot = Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            SystemGuardianLibraryService.RootDirectoryName,
            SystemGuardianLibraryService.BuiltInDirectoryName);

        Assert.True(Directory.Exists(builtInRoot), "Built-in system guardian library must exist.");

        var dossierPaths = Directory.EnumerateDirectories(builtInRoot)
            .Select(directory => Path.Combine(directory, "dossier.md"))
            .Where(File.Exists)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.True(dossierPaths.Length >= 10, "Expected all permanent Guardian dossiers to be present.");

        var requiredHeadings = new[]
        {
            "### 1. Ядро личности",
            "### 2. Визуальное проявление",
            "### 3. Личность и ценности",
            "### 4. Манера речи",
            "### 5. Модель отношений",
            "### 6. Романтический профиль",
            "### 7. Наставничество и испытания",
            "### 8. Поведение в конфликте",
            "### 9. Библия Обители",
            "### 10. Духовно-боевой образ",
            "### 11. Рана Сарефа",
            "### 12. Обычные крючки сцен",
            "### 13. Не играть как"
        };

        foreach (var dossierPath in dossierPaths)
        {
            var dossier = File.ReadAllText(dossierPath);

            foreach (var heading in requiredHeadings)
            {
                Assert.Contains(heading, dossier, StringComparison.Ordinal);
            }

            Assert.Contains("Примерные реплики:", dossier, StringComparison.Ordinal);
            Assert.Contains("Особое духовное искусство:", dossier, StringComparison.Ordinal);
            Assert.Contains("Полные четыре квеста находятся", dossier, StringComparison.Ordinal);
            Assert.Contains("Неромантический маршрут", dossier, StringComparison.Ordinal);
            Assert.Contains("Соперничество", dossier, StringComparison.Ordinal);
            Assert.Contains("Не играть", dossier, StringComparison.Ordinal);
            Assert.DoesNotContain("TBD", dossier, StringComparison.OrdinalIgnoreCase);
            Assert.True(dossier.Length > 6500, $"{Path.GetFileName(Path.GetDirectoryName(dossierPath))} dossier is too thin for the expanded standard.");
        }
    }

    [Fact]
    public void BuiltInPermanentGuardianDossiers_DescribeDistinctSpecialArtCombatEffects()
    {
        var builtInRoot = Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            SystemGuardianLibraryService.RootDirectoryName,
            SystemGuardianLibraryService.BuiltInDirectoryName);
        var standardPath = Path.Combine(
            TestRepoPaths.RepoRoot,
            "OtherGuides",
            "System_Guardian_Dossier_Standard.md");
        var expectedDossiers = new[]
        {
            new
            {
                PresetId = "azalia",
                ArtName = "Пламя Избранной Клятвы",
                BaseOperation = "Базовое действие - наложение оков.",
                Fragments = new[]
                {
                    "добровольной преданности",
                    "назвать реальное обещание",
                    "боевое условие",
                    "преимущество к броску",
                    "честный отказ"
                }
            },
            new
            {
                PresetId = "brann",
                ArtName = "Клеймо Честной Трещины",
                BaseOperation = "Базовое действие - давление.",
                Fragments = new[]
                {
                    "дефект опоры",
                    "назвать трещину",
                    "позицию конфликта",
                    "боевое условие",
                    "ремонт"
                }
            },
            new
            {
                PresetId = "elyara",
                ArtName = "Милость Незаживающей Раны",
                BaseOperation = "Базовое действие - защита.",
                Fragments = new[]
                {
                    "тяжелое последствие",
                    "одному союзнику",
                    "нагрузку стороны",
                    "ответную цену",
                    "нельзя стереть цену"
                }
            },
            new
            {
                PresetId = "ilarion",
                ArtName = "Якорь Невытравленного Имени",
                BaseOperation = "Базовое действие - защита.",
                Fragments = new[]
                {
                    "одно названное свидетельство",
                    "стирание",
                    "боевое условие-защиту",
                    "границу контроля",
                    "контрдоказательство"
                }
            },
            new
            {
                PresetId = "lissara",
                ArtName = "След, Которого Не Было",
                BaseOperation = "Базовое действие - маневр.",
                Fragments = new[]
                {
                    "ложный след",
                    "преследователя",
                    "темп",
                    "позицию конфликта",
                    "повторение учит врага"
                }
            },
            new
            {
                PresetId = "lucian",
                ArtName = "Лунный Разрез Клятвы",
                BaseOperation = "Базовое действие - прорыв оков.",
                Fragments = new[]
                {
                    "один слой клятвы",
                    "названной печати",
                    "боевое условие",
                    "ответную цену",
                    "один слой за применение"
                }
            },
            new
            {
                PresetId = "myriel",
                ArtName = "Пепельная Формула Чужого Мира",
                BaseOperation = "Базовое действие - давление.",
                Fragments = new[]
                {
                    "чужого закона",
                    "несовместимость",
                    "преимущество к броску",
                    "нагрузку стороны",
                    "местная адаптация"
                }
            },
            new
            {
                PresetId = "seret",
                ArtName = "Разомкнутый Договор",
                BaseOperation = "Базовое действие - прорыв оков.",
                Fragments = new[]
                {
                    "юридическую лазейку",
                    "скрытое условие",
                    "ответную цену",
                    "экономию действия",
                    "цена не исчезает"
                }
            },
            new
            {
                PresetId = "varak",
                ArtName = "Трещина в Строю",
                BaseOperation = "Базовое действие - давление.",
                Fragments = new[]
                {
                    "подавленную волю",
                    "одного боевого узла",
                    "позицию конфликта",
                    "преимущество темпа",
                    "обновленный приказ"
                }
            },
            new
            {
                PresetId = "veyra",
                ArtName = "Маска Среди Крыльев",
                BaseOperation = "Базовое действие - маневр.",
                Fragments = new[]
                {
                    "временную роль",
                    "первую проверку доступа",
                    "экономию действия",
                    "преимущество к броску",
                    "противоречие"
                }
            }
        };
        var forbiddenRawDossierTerms = new[]
        {
            "combatEffect",
            "combatCondition",
            "rollMode",
            "conflictPosition",
            "controlState",
            "sideStrain",
            "tempoAdvantage",
            "counterPayoff",
            "actionEconomy",
            "actionCostAudit",
            "DTO",
            "JSON",
            "game_state"
        };
        var errors = new List<string>();
        var combatClauses = new List<string>();

        Assert.True(Directory.Exists(builtInRoot), "Built-in system guardian library must exist.");
        Assert.True(File.Exists(standardPath), "System Guardian dossier standard must exist.");

        var standard = File.ReadAllText(standardPath);
        foreach (var standardFragment in new[]
                 {
                     "Боевой эффект:",
                     "боевую нишу",
                     "триггер",
                     "цель",
                     "разрешенную ось",
                     "контригру",
                     "GM-заметку"
                 })
        {
            if (!standard.Contains(standardFragment, StringComparison.Ordinal))
                errors.Add($"Dossier standard is missing required special-art combat guidance fragment: {standardFragment}");
        }

        foreach (var expected in expectedDossiers)
        {
            var dossierPath = Path.Combine(builtInRoot, expected.PresetId, "dossier.md");
            if (!File.Exists(dossierPath))
            {
                errors.Add($"{expected.PresetId}: dossier.md is missing.");
                continue;
            }

            var dossier = File.ReadAllText(dossierPath);
            var paragraph = ExtractParagraphContaining(dossier, $"Особое духовное искусство: \"{expected.ArtName}\"");
            if (paragraph.Length == 0)
            {
                errors.Add($"{expected.PresetId}: special-art paragraph for {expected.ArtName} is missing.");
                continue;
            }

            foreach (var requiredFragment in new[]
                     {
                         $"Особое духовное искусство: \"{expected.ArtName}\"",
                         expected.BaseOperation,
                         "Художественный эффект:",
                         "При применении ГМ обязан"
                     })
            {
                if (!paragraph.Contains(requiredFragment, StringComparison.Ordinal))
                    errors.Add($"{expected.PresetId}: special-art paragraph no longer preserves required fragment: {requiredFragment}");
            }

            if (!paragraph.Contains("Боевой эффект:", StringComparison.Ordinal))
                errors.Add($"{expected.PresetId}: {expected.ArtName} is missing a Боевой эффект clause.");

            foreach (var fragment in expected.Fragments)
            {
                if (!paragraph.Contains(fragment, StringComparison.OrdinalIgnoreCase))
                    errors.Add($"{expected.PresetId}: combat-effect clause is missing required fragment: {fragment}");
            }

            foreach (var forbiddenTerm in forbiddenRawDossierTerms)
            {
                if (paragraph.Contains(forbiddenTerm, StringComparison.Ordinal))
                    errors.Add($"{expected.PresetId}: dossier combat-effect paragraph exposes raw/debug term: {forbiddenTerm}");
            }

            combatClauses.Add(paragraph);
        }

        if (combatClauses.Distinct(StringComparer.Ordinal).Count() != expectedDossiers.Length)
            errors.Add("Guardian combat-effect paragraphs must be distinct per dossier.");

        Assert.True(errors.Count == 0, string.Join(Environment.NewLine, errors));
    }

    private static JsonObject GetOnlyAfterlifeProfile(JsonObject root)
    {
        var profiles = Assert.IsType<JsonArray>(root[AfterlifeEntityProfileState.ProfilesProperty]);
        return Assert.IsType<JsonObject>(Assert.Single(profiles));
    }

    private static void AssertCompleteSystemGuardianMaterialization(
        JsonObject profile,
        string expectedGuardianId,
        int expectedTurn)
    {
        Assert.Equal("guardian", profile["actorType"]?.GetValue<string>());
        Assert.Equal(expectedGuardianId, profile["actorId"]?.GetValue<string>());

        var expectedMaterialization = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["materializationId"] = $"mat_{expectedGuardianId}_turn_{expectedTurn}",
            ["actorType"] = "guardian",
            ["actorId"] = expectedGuardianId,
            ["materializedAtTurn"] = expectedTurn,
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

        Assert.True(
            JsonNode.DeepEquals(expectedMaterialization, profile[ActorMaterializationContract.PropertyName]),
            $"Unexpected System Guardian materialization: {profile[ActorMaterializationContract.PropertyName]?.ToJsonString()}");

        Assert.Empty(Assert.IsType<JsonArray>(profile["relationships"]));
        Assert.IsType<JsonArray>(profile[AfterlifeEntityProfileState.ProgressionLedgerProperty]);
        Assert.NotEmpty(Assert.IsType<JsonArray>(profile["ledger"]));

        var goals = Assert.IsType<JsonObject>(profile["goals"]);
        Assert.False(string.IsNullOrWhiteSpace(goals["goalId"]?.GetValue<string>()));
        Assert.False(string.IsNullOrWhiteSpace(goals["shortTermGoal"]?.GetValue<string>()));
        Assert.False(string.IsNullOrWhiteSpace(goals["longTermGoal"]?.GetValue<string>()));
        Assert.False(string.IsNullOrWhiteSpace(goals["plan"]?.GetValue<string>()));
        Assert.False(string.IsNullOrWhiteSpace(goals["gmThoughtsSummary"]?.GetValue<string>()));
        Assert.Equal(expectedTurn, goals["updatedAtTurn"]?.GetValue<int>());
        Assert.Empty(Assert.IsType<JsonArray>(profile["personalQuests"]));
        Assert.True(profile.ContainsKey("currentActivity"));
        Assert.Null(profile["currentActivity"]);
        Assert.Empty(Assert.IsType<JsonArray>(profile["completedActivities"]));

        var strategy = Assert.IsType<JsonObject>(profile["progressionStrategy"]);
        var strategySummary = strategy["summary"]?.GetValue<string>();
        var gmThoughtsSummary = profile["gmThoughtsSummary"]?.GetValue<string>();
        Assert.False(string.IsNullOrWhiteSpace(gmThoughtsSummary));
        Assert.Equal(strategySummary, gmThoughtsSummary);
    }

    private static void AssertDirectSystemGuardianMaterializationValidationPasses(JsonObject profile)
    {
        using var document = JsonDocument.Parse(profile.ToJsonString());

        var issues = ActorMaterializationContract.ValidateAfterlifeProfile(
            document.RootElement,
            "systemGuardianProfile",
            requireEnvelope: true,
            canTradeEvidence: false);

        Assert.Empty(issues);
    }

    private static bool IsActorMaterializationIssue(ValidationIssue issue) =>
        issue.Code?.Contains("actor_materialization", StringComparison.OrdinalIgnoreCase) == true;

    private static async Task SeedPresetAsync(string rootDir, string presetId, string displayName, string domain, string author)
    {
        var presetDir = Path.Combine(rootDir, presetId);
        Directory.CreateDirectory(presetDir);

        await File.WriteAllTextAsync(Path.Combine(presetDir, "manifest.json"), $$"""
        {
          "presetId": "{{presetId}}",
          "displayName": "{{displayName}}",
          "summary": "Тестовый системный хранитель.",
          "alwaysAvailable": true,
          "category": "system_guardian",
          "identity": {
            "domain": "{{domain}}",
            "archetype": "Test Archetype",
            "tone": "Measured",
            "coreValues": ["ценность 1", "ценность 2", "ценность 3"]
          },
          "nameVariants": {
            "default": "{{displayName}}",
            "feminine": "{{displayName}}",
            "masculine": null,
            "neutral": null
          },
          "manifestationDefaults": {
            "formFlexibility": "selective",
            "defaultPresentationStyle": "feminine",
            "defaultPronouns": "она/её",
            "appearanceDescription": "Тестовая текущая форма проявления."
          },
          "abode": {
            "name": "Тестовая Обитель",
            "theme": "тест"
          },
          "generationRules": {
            "mustPreserve": ["имя"],
            "canVary": ["детали"],
            "forbidden": ["подмену"]
          },
          "searchAttraction": {
            "enabled": true,
            "label": "Притяжение",
            "keywords": ["тест"]
          },
          "authoring": {
            "author": "{{author}}",
            "version": "1.0"
          }
        }
        """);
        await File.WriteAllTextAsync(Path.Combine(presetDir, "dossier.md"), $"# {displayName}\n\nТестовое досье.");
    }

    private static string GetRepoBuiltInPresetDirectory(string presetId) =>
        Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            SystemGuardianLibraryService.RootDirectoryName,
            SystemGuardianLibraryService.BuiltInDirectoryName,
            presetId);

    private static string ExtractParagraphContaining(string text, string marker)
    {
        var markerIndex = text.IndexOf(marker, StringComparison.Ordinal);
        if (markerIndex < 0)
            return string.Empty;

        var paragraphStart = text.LastIndexOf("\n\n", markerIndex, StringComparison.Ordinal);
        paragraphStart = paragraphStart < 0 ? 0 : paragraphStart + 2;

        var paragraphEnd = text.IndexOf("\n\n", markerIndex, StringComparison.Ordinal);
        if (paragraphEnd < 0)
            paragraphEnd = text.Length;

        return text.Substring(paragraphStart, paragraphEnd - paragraphStart).Trim();
    }

    private static void CopyDirectory(string sourceDirectory, string targetDirectory)
    {
        Directory.CreateDirectory(targetDirectory);

        foreach (var sourceFile in Directory.EnumerateFiles(sourceDirectory))
        {
            var targetFile = Path.Combine(targetDirectory, Path.GetFileName(sourceFile));
            File.Copy(sourceFile, targetFile, overwrite: true);
        }
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_rootPath))
                Directory.Delete(_rootPath, recursive: true);
        }
        catch
        {
            // ignored
        }
    }
}
