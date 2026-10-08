using Xunit;
namespace BookOfEternityClient.Tests;

[Collection("desktop-helper-console")]
public sealed class DesktopImageConsumerTests
{
    [Fact]
    public void RealViewerAssociationFailureGivesVisibleReasonAndManualPath()
    {
        using var f = new DesktopHelpersFixture("unavailable");
        var path = f.Image(); f.Images.OpenImageInViewer(path);
        f.ExactRequest(path);
        Assert.Contains("Не удалось", f.Output.ToString());
        Assert.Contains(path, f.Output.ToString());
    }
    [Fact]
    public void RealGalleryCreationFailureIsHandledWithManualPath()
    {
        using var f = new DesktopHelpersFixture();
        var path = Path.Combine(f.Files.ResolvePath("images"), "npcs"); File.WriteAllBytes(path, [1]);
        var error = Record.Exception(() => f.Images.OpenImagesFolder("npc"));
        Assert.Null(error); Assert.Empty(f.Requests); Assert.Contains(path, f.Output.ToString());
    }
}
