using System.ComponentModel;
using BookOfEternityClient.Services;
using Xunit;
namespace BookOfEternityClient.Tests;

[Collection("desktop-helper-console")]
public sealed class DesktopLaunchContractTests
{
    [Theory]
    [InlineData("requested", DesktopOpenStatus.Requested)]
    [InlineData("unavailable", DesktopOpenStatus.Unavailable)]
    [InlineData("failed", DesktopOpenStatus.Failed)]
    [InlineData("cancelled", DesktopOpenStatus.Cancelled)]
    public void RealAssociationAdapterReturnsExactTypedOutcomeWithoutReplay(string mode, DesktopOpenStatus expected)
    {
        using var f = new DesktopHelpersFixture(mode);
        var path = f.Image(); var bytes = File.ReadAllBytes(path);
        var result = f.Images.OpenImageInViewer(path);
        Assert.Equal(expected, result.Status); Assert.Equal(path, result.Path); f.ExactRequest(path);
        Assert.Equal(bytes, File.ReadAllBytes(path)); Assert.Contains(path, f.Output.ToString());
        Assert.DoesNotContain("изображение показано", f.Output.ToString(), StringComparison.OrdinalIgnoreCase);
        if (expected == DesktopOpenStatus.Requested) Assert.Contains("Если", f.Output.ToString());
        f.Images.OpenImageInViewer(path); Assert.Equal(2, f.Requests.Count); // only a new explicit request retries
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RealViewerHonoursSuppressionAndExplicitForce(bool force)
    {
        using var f = new DesktopHelpersFixture(); f.Settings.GenerateImagesWithoutDisplay = true;
        var path = f.Image(); var result = f.Images.OpenImageInViewer(path, force);
        Assert.Equal(force ? DesktopOpenStatus.Requested : DesktopOpenStatus.Suppressed, result.Status);
        if (force) f.ExactRequest(path); else { Assert.Empty(f.Requests); Assert.Equal("", f.Output.ToString()); }
    }
    [Theory]
    [InlineData("missing.png", false, DesktopOpenStatus.Missing)]
    [InlineData("unknown.exe", true, DesktopOpenStatus.Unsupported)]
    public void RealViewerNeverLaunchesMissingOrUnknownFiles(string name, bool exists, DesktopOpenStatus status)
    {
        using var f = new DesktopHelpersFixture(); var path = Path.Combine(f.Root, name);
        if (exists) File.WriteAllBytes(path, [1]);
        var result = f.Images.OpenImageInViewer(path);
        Assert.Equal(status, result.Status); Assert.Empty(f.Requests); Assert.Contains(path, f.Output.ToString());
    }
    [Theory]
    [InlineData(null, "")]
    [InlineData("npc", "npcs")]
    public void RealGalleryUsesExactDirectoryAndOnlyOneManagedRequest(string? kind, string suffix)
    {
        using var f = new DesktopHelpersFixture(); var expected = Path.Combine(f.Files.ResolvePath("images"), suffix);
        var result = f.Images.OpenImagesFolder(kind);
        Assert.Equal(DesktopOpenStatus.Requested, result.Status); Assert.True(Directory.Exists(expected));
        f.ExactRequest(expected); Assert.Contains(expected, f.Output.ToString());
    }
    [Fact]
    public void InvalidGalleryKindDoesNotCreateOrOpenUnknownFolder()
    {
        using var f = new DesktopHelpersFixture(); var before = Directory.GetFileSystemEntries(f.Files.ResolvePath("images"));
        Assert.Equal(DesktopOpenStatus.Unsupported, f.Images.OpenImagesFolder("unknown").Status);
        Assert.Empty(f.Requests); Assert.Equal(before, Directory.GetFileSystemEntries(f.Files.ResolvePath("images")));
    }
    [Fact]
    public void TypedFolderMissingDoesNotCreateWithoutOriginalCreationPermission()
    {
        using var f = new DesktopHelpersFixture(); var path = Path.Combine(f.Root, "absent 🌌");
        var result = f.Opener.OpenFolder(path, createIfMissing: false);
        Assert.Equal(DesktopOpenStatus.Missing, result.Status); Assert.False(Directory.Exists(path)); Assert.Empty(f.Requests);
    }
    [Fact]
    public void SourceKeepsOriginalConsumersAndFontFallbackWithoutShellOrDesktopFramework()
    {
        var root = TestRepoPaths.RepoRoot;
        var core = File.ReadAllText(Path.Combine(root, "BookOfEternityClient/Services/DesktopPathOpener.cs"));
        Assert.Contains("UseShellExecute = true", core); Assert.Contains("Process.Start", core);
        Assert.DoesNotContain("xdg-open", core); Assert.DoesNotContain("cmd.exe", core); Assert.DoesNotContain("/bin/sh", core);
        foreach (var file in new[] { "Core/GameEngine/GameEngine.OptionsAndSettings.cs", "UI/ExplorerMode/ExplorerMode.MetaStoryAndStatus.cs", "Core/GameEngine/GameEngine.MainMenu.cs", "UI/ExplorerMode/ExplorerMode.PrivateImplementation.cs" })
            Assert.Contains("_desktopPathOpener.OpenFolder", File.ReadAllText(Path.Combine(root, "BookOfEternityClient", file)));
        var font = File.ReadAllText(Path.Combine(root, "BookOfEternityClient/Services/ConsoleAppearanceService.cs"));
        Assert.Contains("!OperatingSystem.IsWindows() || Console.IsOutputRedirected", font);
        Assert.Contains("font_size_apply_note", File.ReadAllText(Path.Combine(root, "BookOfEternityClient/Core/GameEngine/GameEngine.OptionsAndSettings.cs")));
    }
}
