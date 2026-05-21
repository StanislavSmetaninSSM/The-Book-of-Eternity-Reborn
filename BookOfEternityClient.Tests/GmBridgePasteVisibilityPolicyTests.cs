using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class GmBridgePasteVisibilityPolicyTests
{
    [Fact]
    public void IsPromptVisible_DefaultConfig_AcceptsCodexLargePasteMarker()
    {
        var settings = new GameSettings();
        var visibleText = "codex> [Pasted Content 6740 chars]";

        Assert.True(GmBridgePasteVisibilityPolicy.IsPromptVisible("Process turn #42 and read input/turn_request.json", visibleText, settings));
    }

    [Fact]
    public void IsPromptVisible_DefaultConfig_AcceptsGeminiPasteMarker()
    {
        var settings = new GameSettings();
        var visibleText = "Gemini CLI\nPasted Text:\n";

        Assert.True(GmBridgePasteVisibilityPolicy.IsPromptVisible("Process turn #42 and read input/turn_request.json", visibleText, settings));
    }

    [Fact]
    public void IsPromptVisible_ExactTextOnlyPolicy_AcceptsRenderedPromptNeedle()
    {
        var settings = new GameSettings
        {
            GmBridgePasteVisibilityPolicy = GmBridgePasteVisibilityPolicy.ExactTextOnly
        };

        var visibleText = "Process turn #42 and read input/turn_request.json";

        Assert.True(GmBridgePasteVisibilityPolicy.IsPromptVisible("Process turn #42 and read input/turn_request.json", visibleText, settings));
    }

    [Fact]
    public async Task LoadSettingsAsync_CustomPasteMarker_AcceptsConfiguredContainsPattern()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();
            await fs.WriteFileAtomicAsync("config.json", """
            {
              "gmBridgePasteVisibilityPolicy": "ExactTextOrConfiguredMarker",
              "gmBridgePasteVisibilityMarkers": [
                { "name": "CustomCli", "kind": "contains", "pattern": "[large input accepted]" }
              ]
            }
            """);

            var settings = new GameSettings();
            var manager = new StateManager(fs, settings, NullLogger<StateManager>.Instance);

            await manager.LoadSettingsAsync();

            Assert.True(GmBridgePasteVisibilityPolicy.IsPromptVisible("Process turn #42", "CustomCli: [large input accepted]", settings));
            Assert.Contains(settings.GmBridgePasteVisibilityMarkers, marker => string.Equals(marker.Name, "CustomCli", StringComparison.Ordinal));
            Assert.Contains(settings.GmBridgePasteVisibilityMarkers, marker => string.Equals(marker.Name, "Codex", StringComparison.Ordinal));
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public void IsPromptVisible_InvalidRegexMarker_DoesNotThrowAndDoesNotMatch()
    {
        var settings = new GameSettings
        {
            GmBridgePasteVisibilityMarkers =
            [
                new GmBridgePasteVisibilityMarker
                {
                    Name = "BrokenRegex",
                    Kind = "regex",
                    Pattern = "["
                }
            ]
        };

        var visibleText = "CLI rendered unrelated output";

        Assert.False(GmBridgePasteVisibilityPolicy.IsPromptVisible("Process turn #42", visibleText, settings));
    }

    [Fact]
    public void IsPromptVisible_ExactTextOnlyPolicy_RejectsConfiguredMarkers()
    {
        var settings = new GameSettings
        {
            GmBridgePasteVisibilityPolicy = GmBridgePasteVisibilityPolicy.ExactTextOnly
        };

        var visibleText = "[Pasted Content 6740 chars]";

        Assert.False(GmBridgePasteVisibilityPolicy.IsPromptVisible("Process turn #42", visibleText, settings));
    }

    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "boe_gm_bridge_paste_policy_" + Guid.NewGuid().ToString("N"));
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
            // best-effort cleanup
        }
    }
}
