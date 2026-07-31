using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class FastTestBoundaryTests
{
    private const string ProductionDirectory = "BookOfEternityClient";
    private const string ProductionProjectFile = "BookOfEternityClient.csproj";
    private const string FastTestsDirectory = "BookOfEternityClient.Tests";
    private const string FastTestsProjectFile = "BookOfEternityClient.Tests.csproj";
    private const string IntegrationTestsDirectory = "BookOfEternityClient.IntegrationTests";
    private const string IntegrationTestsProjectFile = "BookOfEternityClient.IntegrationTests.csproj";
    private const string TestSupportDirectory = "BookOfEternityClient.TestSupport";
    private const string TestSupportProjectFile = "BookOfEternityClient.TestSupport.csproj";

    private static readonly string[] ReviewedHeavySources =
    [
        "AfterlifeSpiritualConflictValidationTests.cs",
        "GameEngineTurnLifecycleTests.cs",
        "GuardianSystemRegressionTests.cs",
        "FileSystemManagerTests.cs",
        "ConsoleE2ESmokeTests.cs",
        "LocalWebUiBuiltFrontendSmokeTests.cs"
    ];

    [Fact]
    public void FastSources_ContainNoIntegrationCategoriesOrBroadValidationCalls()
    {
        var fastRoot = Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient.Tests");
        var broadCall = new Regex(
            @"\.ValidateGameState" + @"Async\s*\(\s*\)",
            RegexOptions.CultureInvariant);
        var forbiddenCategories = new[]
        {
            "FullValidation",
            "RegressionIntegration",
            "ProcessIntegration",
            "E2E"
        };

        var violations = Directory.EnumerateFiles(fastRoot, "*.cs", SearchOption.AllDirectories)
            .Select(path => (Path: path, Source: File.ReadAllText(path)))
            .SelectMany(candidate =>
                forbiddenCategories
                    .Where(category => candidate.Source.Contains(
                        $"[Trait(\"Category\", \"{category}\")]",
                        StringComparison.Ordinal))
                    .Select(category => $"{candidate.Path}: {category}")
                    .Concat(
                        broadCall.IsMatch(candidate.Source)
                            ? new[] { $"{candidate.Path}: parameterless full validation" }
                            : Array.Empty<string>()))
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void ReviewedHeavySources_ExistOnlyUnderIntegrationTests()
    {
        var fastRoot = Path.Combine(TestRepoPaths.RepoRoot, FastTestsDirectory);
        var integrationRoot = Path.Combine(TestRepoPaths.RepoRoot, IntegrationTestsDirectory);
        var supportRoot = Path.Combine(TestRepoPaths.RepoRoot, TestSupportDirectory);

        foreach (var fileName in ReviewedHeavySources)
        {
            var matches = new[] { fastRoot, integrationRoot, supportRoot }
                .SelectMany(root => Directory.EnumerateFiles(
                    root,
                    fileName,
                    SearchOption.AllDirectories))
                .Select(Path.GetFullPath)
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var expectedIntegrationPath = Path.GetFullPath(Path.Combine(integrationRoot, fileName));

            Assert.Equal(new[] { expectedIntegrationPath }, matches);
        }
    }

    [Fact]
    public void TestProjectTopology_SeparatesFastIntegrationAndSupportAssemblies()
    {
        var fastProject = ReadProject(FastTestsDirectory, FastTestsProjectFile);
        var integrationProject = ReadProject(IntegrationTestsDirectory, IntegrationTestsProjectFile);
        var supportProject = ReadProject(TestSupportDirectory, TestSupportProjectFile);
        var productionProject = ReadProject(ProductionDirectory, ProductionProjectFile);

        AssertProjectReferences(
            fastProject,
            FastTestsDirectory,
            "Fast test project",
            ProjectPath(ProductionDirectory, ProductionProjectFile),
            ProjectPath(TestSupportDirectory, TestSupportProjectFile));
        AssertProjectReferences(
            integrationProject,
            IntegrationTestsDirectory,
            "Integration test project",
            ProjectPath(ProductionDirectory, ProductionProjectFile),
            ProjectPath(TestSupportDirectory, TestSupportProjectFile));
        AssertDoesNotReference(
            integrationProject,
            IntegrationTestsDirectory,
            "Integration test project",
            ProjectPath(FastTestsDirectory, FastTestsProjectFile));
        AssertProjectReferences(
            supportProject,
            TestSupportDirectory,
            "Test support project",
            ProjectPath(ProductionDirectory, ProductionProjectFile));

        AssertDoesNotReferencePackage(
            supportProject,
            packageId => string.Equals(
                packageId,
                "Microsoft.NET.Test.Sdk",
                StringComparison.OrdinalIgnoreCase),
            "Microsoft.NET.Test.Sdk");
        AssertDoesNotReferencePackage(
            supportProject,
            IsXunitPackageId,
            "an xUnit-family package");

        AssertFriendAssemblies(
            productionProject,
            "Production project",
            "BookOfEternityClient.Tests",
            "BookOfEternityClient.TestSupport",
            "BookOfEternityClient.IntegrationTests");
        AssertFriendAssemblies(
            supportProject,
            "Test support project",
            "BookOfEternityClient.Tests",
            "BookOfEternityClient.IntegrationTests");

        var solutionProjects = ReadSolutionProjectPaths();
        AssertContainsPath(
            solutionProjects,
            ProjectPath(ProductionDirectory, ProductionProjectFile),
            "Production solution must include the production project");
        AssertDoesNotContainPath(
            solutionProjects,
            ProjectPath(FastTestsDirectory, FastTestsProjectFile),
            "Production solution must not include the fast test project");
        AssertDoesNotContainPath(
            solutionProjects,
            ProjectPath(IntegrationTestsDirectory, IntegrationTestsProjectFile),
            "Production solution must not include the integration test project");
        AssertDoesNotContainPath(
            solutionProjects,
            ProjectPath(TestSupportDirectory, TestSupportProjectFile),
            "Production solution must not include the test support project");
    }

    [Fact]
    public void ProjectReferences_UpdateOnlyItem_DoesNotCreateDirectReference()
    {
        var project = XDocument.Parse(
            """
            <Project>
              <ItemGroup>
                <ProjectReference Update="..\BookOfEternityClient\BookOfEternityClient.csproj" />
              </ItemGroup>
            </Project>
            """);

        Assert.Empty(ProjectReferences(project, FastTestsDirectory));
    }

    private static XDocument ReadProject(string directory, string fileName) =>
        XDocument.Load(ProjectPath(directory, fileName));

    private static string ProjectPath(params string[] relativeParts) =>
        Path.GetFullPath(Path.Combine(new[] { TestRepoPaths.RepoRoot }.Concat(relativeParts).ToArray()));

    private static void AssertProjectReferences(
        XDocument project,
        string projectDirectory,
        string projectDescription,
        params string[] expectedReferences)
    {
        var references = ProjectReferences(project, projectDirectory);

        foreach (var expectedReference in expectedReferences)
        {
            AssertContainsPath(
                references,
                expectedReference,
                $"{projectDescription} is missing a required ProjectReference");
        }
    }

    private static void AssertDoesNotReference(
        XDocument project,
        string projectDirectory,
        string projectDescription,
        string forbiddenReference)
    {
        AssertDoesNotContainPath(
            ProjectReferences(project, projectDirectory),
            forbiddenReference,
            $"{projectDescription} must not reference the fast test assembly");
    }

    private static IReadOnlyCollection<string> ProjectReferences(XDocument project, string projectDirectory) =>
        project
            .Descendants()
            .Where(element => string.Equals(element.Name.LocalName, "ProjectReference", StringComparison.Ordinal))
            .Select(element => AttributeValue(element, "Include"))
            .Where(reference => !string.IsNullOrWhiteSpace(reference))
            .Select(reference => ResolveRelativePath(ProjectPath(projectDirectory), reference!))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static void AssertDoesNotReferencePackage(
        XDocument project,
        Func<string, bool> isForbidden,
        string forbiddenPackageDescription)
    {
        var packageIds = PackageReferenceIds(project);

        Assert.False(
            packageIds.Any(isForbidden),
            $"Test support project must not reference {forbiddenPackageDescription}. " +
            $"Parsed package IDs: {string.Join(", ", packageIds)}");
    }

    private static IReadOnlyCollection<string> PackageReferenceIds(XDocument project) =>
        project
            .Descendants()
            .Where(element => string.Equals(
                element.Name.LocalName,
                "PackageReference",
                StringComparison.Ordinal))
            .Select(element =>
                AttributeValue(element, "Include") ??
                AttributeValue(element, "Update"))
            .Where(packageId => !string.IsNullOrWhiteSpace(packageId))
            .Select(packageId => packageId!.Trim())
            .ToArray();

    private static bool IsXunitPackageId(string packageId) =>
        string.Equals(packageId, "xunit", StringComparison.OrdinalIgnoreCase) ||
        packageId.StartsWith("xunit.", StringComparison.OrdinalIgnoreCase);

    private static void AssertFriendAssemblies(
        XDocument project,
        string projectDescription,
        params string[] requiredFriendAssemblies)
    {
        var friendAssemblies = project
            .Descendants()
            .Where(element =>
                string.Equals(element.Name.LocalName, "AssemblyAttribute", StringComparison.Ordinal) &&
                string.Equals(
                    AttributeValue(element, "Include"),
                    "System.Runtime.CompilerServices.InternalsVisibleTo",
                    StringComparison.Ordinal))
            .SelectMany(element => element.Descendants())
            .Where(element => string.Equals(element.Name.LocalName, "_Parameter1", StringComparison.Ordinal))
            .Select(element => element.Value.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var requiredFriendAssembly in requiredFriendAssemblies)
        {
            Assert.True(
                friendAssemblies.Contains(requiredFriendAssembly),
                $"{projectDescription} is missing InternalsVisibleTo for '{requiredFriendAssembly}'. " +
                $"Parsed friends: {string.Join(", ", friendAssemblies)}");
        }
    }

    private static IReadOnlyCollection<string> ReadSolutionProjectPaths()
    {
        var solutionPath = ProjectPath(ProductionDirectory, "BookOfEternityClient.sln");
        var projectEntry = new Regex(
            "^\\s*Project\\(\\\"[^\\\"]+\\\"\\)\\s*=\\s*\\\"[^\\\"]+\\\",\\s*\\\"(?<path>[^\\\"]+)\\\",\\s*\\\"[^\\\"]+\\\"\\s*$",
            RegexOptions.CultureInvariant);

        return File
            .ReadLines(solutionPath)
            .Select(line => projectEntry.Match(line))
            .Where(match => match.Success)
            .Select(match => ResolveRelativePath(
                Path.GetDirectoryName(solutionPath)!,
                match.Groups["path"].Value))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static string ResolveRelativePath(string baseDirectory, string relativePath)
    {
        var normalizedPath = relativePath
            .Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar);

        return Path.GetFullPath(Path.Combine(baseDirectory, normalizedPath));
    }

    private static string? AttributeValue(XElement element, string localName) =>
        element
            .Attributes()
            .SingleOrDefault(attribute => string.Equals(
                attribute.Name.LocalName,
                localName,
                StringComparison.Ordinal))
            ?.Value;

    private static void AssertContainsPath(
        IEnumerable<string> actualPaths,
        string expectedPath,
        string failureDescription)
    {
        var paths = actualPaths.ToArray();
        Assert.True(
            paths.Contains(expectedPath, StringComparer.OrdinalIgnoreCase),
            $"{failureDescription}: '{expectedPath}'. Parsed paths: {string.Join(", ", paths)}");
    }

    private static void AssertDoesNotContainPath(
        IEnumerable<string> actualPaths,
        string forbiddenPath,
        string failureDescription)
    {
        var paths = actualPaths.ToArray();
        Assert.False(
            paths.Contains(forbiddenPath, StringComparer.OrdinalIgnoreCase),
            $"{failureDescription}: '{forbiddenPath}'. Parsed paths: {string.Join(", ", paths)}");
    }
}
