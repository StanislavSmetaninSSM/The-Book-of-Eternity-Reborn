using System.Text.Json;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;
using Microsoft.Extensions.Logging.Abstractions;
using Spectre.Console;
using Spectre.Console.Rendering;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

[Collection("desktop-helper-console")]
public sealed class WorldProfileFolderConsumerTests(ITestOutputHelper output)
{
    [Fact]
    public void OriginalWorldSetupFolderRouteUsesManagedAssociation()
    {
        var path = Path.Combine(TestRepoPaths.RepoRoot,
            "BookOfEternityClient/UI/ExplorerMode/ExplorerMode.MetaWorldSetupAndDebug.cs");
        var source = File.ReadAllText(path);
        var start = source.IndexOf("private async Task ShowWorldSetup()", StringComparison.Ordinal);
        var end = source.IndexOf("private async Task ShowWorldRules()", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var body = source[start..end];
        Assert.DoesNotContain("Process.Start", body, StringComparison.Ordinal);
        Assert.Contains("OpenFolderOrPrintPath(profilesDir);", body, StringComparison.Ordinal);
    }

}
