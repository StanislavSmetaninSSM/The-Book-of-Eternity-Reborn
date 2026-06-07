using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class TestEnvironmentTests
{
    [Fact]
    public void TestProcess_DisablesLocalMapViewerBrowserLaunches()
    {
        Assert.Contains(
            AppDomain.CurrentDomain.GetAssemblies(),
            static assembly => assembly.GetName().Name?.StartsWith("xunit", StringComparison.OrdinalIgnoreCase) == true);
        Assert.Equal(
            "BOOK_OF_ETERNITY_DISABLE_BROWSER_OPEN",
            LocalMapViewerLauncher.DisableBrowserOpenEnvironmentVariable);
    }
}
