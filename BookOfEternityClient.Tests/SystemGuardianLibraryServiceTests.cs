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
    public async Task GetAvailablePresetsAsync_BuiltInWinsIdConflict_AndUserPresetStillLoadsForUniqueId()
    {
        await SeedPresetAsync(_service.GetBuiltInDirectoryPath(), "azalia", "Азалия", "Social", "built_in");
        await SeedPresetAsync(_service.GetUserDirectoryPath(), "azalia", "Пользовательская Азалия", "Magic", "user");
        await SeedPresetAsync(_service.GetUserDirectoryPath(), "my_user_guardian", "Мой Хранитель", "Knowledge", "user");

        var presets = await _service.GetAvailablePresetsAsync(includeDossier: true);

        Assert.Equal(2, presets.Count);
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
