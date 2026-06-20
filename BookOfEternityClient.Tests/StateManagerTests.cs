using System.Text.Json;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services.GmWorkers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class StateManagerTests
{
    [Fact]
    public async Task EnsureSettingsFileExistsAsync_CreatesConfigWithCurrentDefaults_WhenMissing()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();

            var settings = new GameSettings
            {
                Language = "ru",
                GmBridgeBackend = "ConPTYBridge",
                GmCliLaunchCommand = "custom-gm-cli --resume"
            };
            var manager = new StateManager(fs, settings, NullLogger<StateManager>.Instance);

            Assert.False(fs.FileExists("config.json"));

            await manager.EnsureSettingsFileExistsAsync();

            Assert.True(fs.FileExists("config.json"));
            var json = await fs.ReadFileAsync("config.json");
            Assert.False(string.IsNullOrWhiteSpace(json));

            using var doc = JsonDocument.Parse(json!);
            Assert.Equal("ru", doc.RootElement.GetProperty("language").GetString());
            Assert.Equal("ConPTYBridge", doc.RootElement.GetProperty("gmBridgeBackend").GetString());
            Assert.Equal("custom-gm-cli --resume", doc.RootElement.GetProperty("gmCliLaunchCommand").GetString());
            Assert.Equal("ExactTextOrConfiguredMarker", doc.RootElement.GetProperty("gmBridgePasteVisibilityPolicy").GetString());
            Assert.Equal(15, doc.RootElement.GetProperty("gmBridgePromptVisibilityTimeoutSeconds").GetDouble());
            Assert.Contains(
                doc.RootElement.GetProperty("gmBridgePasteVisibilityMarkers").EnumerateArray(),
                marker => string.Equals(marker.GetProperty("name").GetString(), "Codex", StringComparison.OrdinalIgnoreCase) &&
                          string.Equals(marker.GetProperty("kind").GetString(), "regex", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task EnsureSettingsFileExistsAsync_DoesNotRewriteExistingConfig()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();

            await fs.WriteFileAtomicAsync("config.json", """
            {
              "language": "en",
              "gmBridgeBackend": "Clipboard",
              "gmCliLaunchCommand": "claude"
            }
            """);

            var before = await fs.ReadFileAsync("config.json");
            var manager = new StateManager(fs, new GameSettings(), NullLogger<StateManager>.Instance);

            await manager.EnsureSettingsFileExistsAsync();

            var after = await fs.ReadFileAsync("config.json");
            Assert.Equal(before, after);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Theory]
    [InlineData(-1, 15)]
    [InlineData(0, 15)]
    [InlineData(0.5, 1)]
    [InlineData(45, 45)]
    [InlineData(120, 60)]
    public async Task LoadSettingsAsync_GmBridgePromptVisibilityTimeoutSeconds_ClampsToSafeRange(
        double configuredValue,
        double expectedValue)
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();
            await fs.WriteFileAtomicAsync("config.json", $$"""
            {
              "gmBridgePromptVisibilityTimeoutSeconds": {{configuredValue.ToString(System.Globalization.CultureInfo.InvariantCulture)}}
            }
            """);
            var settings = new GameSettings();
            var manager = new StateManager(fs, settings, NullLogger<StateManager>.Instance);

            await manager.LoadSettingsAsync();

            Assert.Equal(expectedValue, settings.GmBridgePromptVisibilityTimeoutSeconds);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task LoadAndSaveSettingsAsync_WorkerBridgeProfiles_RoundTripHiddenProfiles()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();
            var settings = new GameSettings
            {
                GmWorkerBridgeProfiles =
                [
                    GmWorkerBridgeTestFixtures.ValidationRepairCodexProfile(),
                    GmWorkerBridgeTestFixtures.NarrativeDraftCodexProfile()
                ]
            };
            var manager = new StateManager(fs, settings, NullLogger<StateManager>.Instance);

            await manager.SaveSettingsAsync();

            var reloadedSettings = new GameSettings();
            var reloaded = new StateManager(fs, reloadedSettings, NullLogger<StateManager>.Instance);
            await reloaded.LoadSettingsAsync();

            Assert.Equal(2, reloadedSettings.GmWorkerBridgeProfiles.Count);
            Assert.All(reloadedSettings.GmWorkerBridgeProfiles, profile =>
                Assert.Equal(WorkerLaunchVisibility.Hidden, profile.LaunchVisibility));
            Assert.Contains(reloadedSettings.GmWorkerBridgeProfiles, profile =>
                profile.WorkerId == "validation_repair_codex" &&
                profile.Permissions.TaskTypes.Contains(WorkerTaskType.ValidationRepair));
            Assert.Contains(reloadedSettings.GmWorkerBridgeProfiles, profile =>
                profile.WorkerId == "narrative_draft_codex" &&
                profile.Permissions.ProposalOnly);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task RefreshGameStateAsync_MissingCurrentRealm_DoesNotReusePreviousChaosSeaRealm()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();
            var manager = new StateManager(fs, new GameSettings(), NullLogger<StateManager>.Instance);

            await fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
            {
              "soulName": "Тестовая душа",
              "currentRealm": "Chaos Sea",
              "currentIncarnation": 7
            }
            """);

            await manager.RefreshGameStateAsync();
            Assert.Equal("Chaos Sea", manager.CurrentState.CurrentRealm);
            Assert.True(manager.CurrentState.IsInChaosSea);

            await fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
            {
              "soulName": "Тестовая душа",
              "currentIncarnation": 7
            }
            """);

            await manager.RefreshGameStateAsync();

            Assert.Equal(string.Empty, manager.CurrentState.CurrentRealm);
            Assert.False(manager.CurrentState.IsInChaosSea);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task RefreshGameStateAsync_LegacyNumericInkFeathers_LoadsBalance()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();
            var manager = new StateManager(fs, new GameSettings(), NullLogger<StateManager>.Instance);

            await fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
            {
              "soulName": "Тестовая душа",
              "currentRealm": "Chaos Sea",
              "currentIncarnation": 7,
              "inkFeathers": 64
            }
            """);

            await manager.RefreshGameStateAsync();

            Assert.Equal(64, manager.CurrentState.InkFeathers);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task RefreshGameStateAsync_MalformedPreparedPackage_FailsClosedInsteadOfOrdinaryShining()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();
            var manager = new StateManager(fs, new GameSettings(), NullLogger<StateManager>.Instance);

            await fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
            {
              "soulName": "Тестовая душа",
              "currentRealm": "Shining Abode",
              "currentIncarnation": 7
            }
            """);
            await fs.WriteFileAtomicAsync("game_state/meta/shining_abode_state.json", """
            {
              "availability": "active",
              "preparedIncarnationPackage": "broken package"
            }
            """);

            await manager.RefreshGameStateAsync();

            Assert.True(manager.CurrentState.HasInvalidShiningAbodeBootstrapPackage);
            Assert.False(manager.CurrentState.HasPendingShiningAbodeBootstrapPackage);
            Assert.False(manager.CurrentState.IsInShiningAbode);
            Assert.False(manager.CurrentState.IsInShiningAbodePendingBootstrap);
            Assert.True(manager.CurrentState.IsInAnyShiningAbodeState);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task RefreshGameStateAsync_ActiveGuardianName_UsesManifestationDisplayName()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();
            var manager = new StateManager(fs, new GameSettings(), NullLogger<StateManager>.Instance);

            await fs.WriteFileAtomicAsync("game_state/meta/guardians.json", """
            {
              "activeGuardian": {
                "guardianId": "guard_social_azalia_001",
                "canonicalName": "Азалия",
                "manifestation": {
                  "currentDisplayName": "Госпожа Шёлковых Нитей"
                }
              }
            }
            """);

            await manager.RefreshGameStateAsync();

            Assert.Equal("Госпожа Шёлковых Нитей", manager.CurrentState.ActiveGuardianName);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task RefreshGameStateAsync_ActiveGuardianName_FallsBackToCanonicalName()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();
            var manager = new StateManager(fs, new GameSettings(), NullLogger<StateManager>.Instance);

            await fs.WriteFileAtomicAsync("game_state/meta/guardians.json", """
            {
              "activeGuardian": {
                "guardianId": "guard_social_azalia_001",
                "canonicalName": "Азалия"
              }
            }
            """);

            await manager.RefreshGameStateAsync();

            Assert.Equal("Азалия", manager.CurrentState.ActiveGuardianName);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task RefreshGameStateAsync_ActiveGuardianName_PreservesLegacyNameFallback()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();
            var manager = new StateManager(fs, new GameSettings(), NullLogger<StateManager>.Instance);

            await fs.WriteFileAtomicAsync("game_state/meta/guardians.json", """
            {
              "activeGuardian": {
                "guardianId": "guard_legacy_001",
                "name": "Старое Имя Хранителя"
              }
            }
            """);

            await manager.RefreshGameStateAsync();

            Assert.Equal("Старое Имя Хранителя", manager.CurrentState.ActiveGuardianName);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-state-manager-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void CleanupTempRoot(string root)
    {
        try
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
        catch
        {
            // ignored
        }
    }
}
