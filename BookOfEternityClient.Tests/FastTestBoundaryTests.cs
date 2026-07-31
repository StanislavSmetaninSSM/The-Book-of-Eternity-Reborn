using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class FastTestBoundaryTests
{
    [Fact]
    public void TestProjectTopology_SeparatesFastIntegrationAndSupportAssemblies()
    {
        var fastProject = ReadProject("BookOfEternityClient.Tests", "BookOfEternityClient.Tests.csproj");
        var integrationProject = ReadProject(
            "BookOfEternityClient.IntegrationTests",
            "BookOfEternityClient.IntegrationTests.csproj");
        var supportProject = ReadProject(
            "BookOfEternityClient.TestSupport",
            "BookOfEternityClient.TestSupport.csproj");

        Assert.Contains(@"..\BookOfEternityClient.TestSupport\BookOfEternityClient.TestSupport.csproj", fastProject);
        Assert.Contains(@"..\BookOfEternityClient.TestSupport\BookOfEternityClient.TestSupport.csproj", integrationProject);
        Assert.Contains(@"..\BookOfEternityClient\BookOfEternityClient.csproj", integrationProject);
        Assert.DoesNotContain("BookOfEternityClient.Tests.csproj", integrationProject);
        Assert.DoesNotContain("Microsoft.NET.Test.Sdk", supportProject);
        Assert.DoesNotContain("xunit", supportProject, StringComparison.OrdinalIgnoreCase);
    }

    private static string ReadProject(string directory, string fileName) =>
        File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, directory, fileName));
}
