using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ExplorerModeSourceGuardTests
{
    [Fact]
    public void ExplorerMode_MustUseConsoleAdapterInsteadOfDirectAnsiConsoleCalls()
    {
        var path = Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient", "UI", "ExplorerMode.cs");
        var source = File.ReadAllText(path);

        Assert.DoesNotContain("AnsiConsole.", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Console.ReadKey(true)", source, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("BookOfEternityClient/UI/ExplorerMode.cs")]
    [InlineData("BookOfEternityClient/Core/GameEngine.cs")]
    public void DynamicJoinedMarkupBlocks_MustUseSafeMarkup(string relativePath)
    {
        var path = Path.Combine(TestRepoPaths.RepoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        var source = File.ReadAllText(path);

        Assert.DoesNotContain("new Markup(string.Join(\"\\n\", ", source, StringComparison.Ordinal);
    }
}
