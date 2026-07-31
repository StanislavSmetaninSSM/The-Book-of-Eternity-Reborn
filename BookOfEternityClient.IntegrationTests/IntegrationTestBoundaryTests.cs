using System.Text.RegularExpressions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class IntegrationTestBoundaryTests
{
    private const int GuardianFullValidationSentinelBudget = 8;
    private const string FastTestsDirectory = "BookOfEternityClient.Tests";
    private const string IntegrationTestsDirectory = "BookOfEternityClient.IntegrationTests";
    private const string TestSupportDirectory = "BookOfEternityClient.TestSupport";
    private const string FullValidationTrait = "[Trait(\"Category\", \"FullValidation\")]";
    private const string ProcessIntegrationTrait = "[Trait(\"Category\", \"ProcessIntegration\")]";
    private const string E2ETrait = "[Trait(\"Category\", \"E2E\")]";
    private const string RegressionIntegrationTrait =
        "[Trait(\"Category\", \"RegressionIntegration\")]";

    private static readonly IReadOnlyDictionary<string, string> GuardianProfiles =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["GuardianSystemRegressionTests.AcceptedAuthority.cs"] = "AcceptedAuthority",
            ["GuardianSystemRegressionTests.IdleValidation.cs"] = "IdleValidation",
            ["GuardianSystemRegressionTests.LifecycleSnapshots.cs"] = "LifecycleSnapshots",
            ["GuardianSystemRegressionTests.PowerJournalOfferings.cs"] = "PowerJournalOfferings",
            ["GuardianSystemRegressionTests.ProjectsPower.cs"] = "ProjectsPower",
            ["GuardianSystemRegressionTests.QuestProgress.cs"] = "QuestProgress",
            ["GuardianSystemRegressionTests.RivalResidents.cs"] = "RivalResidents",
            ["GuardianSystemRegressionTests.TradeOfferingResonance.cs"] = "TradeOfferingResonance"
        };

    private static readonly IReadOnlyDictionary<string, string[]> ProcessAndE2ECategories =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["AgentConsoleLiveSmokeTests.cs"] = [ProcessIntegrationTrait, E2ETrait],
            ["ConsoleE2ESmokeTests.cs"] = [ProcessIntegrationTrait, E2ETrait],
            ["FileSystemManagerTests.cs"] = [ProcessIntegrationTrait],
            ["GmMemorySearchToolTests.cs"] = [ProcessIntegrationTrait],
            ["GmTurnHelperContractTests.cs"] = [ProcessIntegrationTrait],
            ["GmWorkerBridgeLifecycleTests.cs"] = [ProcessIntegrationTrait],
            ["GmWorkerCliRunnerTests.cs"] = [ProcessIntegrationTrait],
            ["GmWorkerProcessHostTests.cs"] = [ProcessIntegrationTrait],
            ["GmWorkerProcessTreeTests.cs"] = [ProcessIntegrationTrait],
            ["GmWorkerProposalStoreTests.cs"] = [ProcessIntegrationTrait],
            ["ImageServiceTests.cs"] = [ProcessIntegrationTrait],
            ["LocalWebUiBuiltFrontendSmokeTests.cs"] = [ProcessIntegrationTrait, E2ETrait],
            ["SaveLoadServiceTests.cs"] = [ProcessIntegrationTrait],
            [Path.Combine("WebUi", "BrowserMediaGenerationServiceTests.cs")] =
                [ProcessIntegrationTrait],
            [Path.Combine("WebUi", "BrowserQteGenerationFencingTests.cs")] =
                [ProcessIntegrationTrait]
        };

    private static readonly string[] RegressionIntegrationSources =
    [
        "BrowserCommandPresentationAuditTests.cs",
        "ExplorerModeCommandTests.cs",
        "ExplorerWebCommandServiceTests.cs",
        "ExplorerWebCommandServiceTestsAfterlifeProfileInboxDrilldowns.cs",
        "ExplorerWebCommandServiceTestsSpiritualConflictArtDrilldowns.cs",
        "GameEngineTurnLifecycleTests.cs",
        "GuardianSystemRegressionTests.cs",
        "LocalWebUiHostTests.cs"
    ];

    private static readonly string[] IndirectFullValidationSources =
    [
        "ValidatorFixtureTests.cs"
    ];

    private static readonly string[] RunnerLaneTokens =
    [
        "Fast",
        "Focused",
        "FullValidation",
        "RegressionIntegration",
        "ProcessIntegration",
        "E2E",
        "Complete"
    ];

    [Fact]
    public void IntegrationAndSupportBroadValidationSources_AreExplicitlyCategorized()
    {
        var broadCall = new Regex(
            @"\.ValidateGameState" + @"Async\s*\(\s*\)",
            RegexOptions.CultureInvariant);
        var supportHarnessPath = SourcePath(TestSupportDirectory, "ValidatorFixtureHarness.cs");
        var uncategorized = EnumerateIntegrationAndSupportSources()
            .Where(candidate => !string.Equals(
                candidate.Path,
                supportHarnessPath,
                StringComparison.OrdinalIgnoreCase))
            .Where(candidate => broadCall.IsMatch(candidate.Source))
            .Where(candidate => !candidate.Source.Contains(
                FullValidationTrait,
                StringComparison.Ordinal))
            .Select(candidate => Path.GetRelativePath(TestRepoPaths.RepoRoot, candidate.Path))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            uncategorized.Length == 0,
            "Direct full-validation integration sources must carry Category=FullValidation:" +
            Environment.NewLine +
            string.Join(Environment.NewLine, uncategorized));
    }

    [Fact]
    public void IndirectFullValidationSources_AreExplicitlyCategorized()
    {
        var violations = IndirectFullValidationSources
            .Where(fileName => !File
                .ReadAllText(SourcePath(IntegrationTestsDirectory, fileName))
                .Contains(FullValidationTrait, StringComparison.Ordinal))
            .ToArray();

        Assert.True(
            violations.Length == 0,
            "Fixture-driven full-validation sources must carry Category=FullValidation:" +
            Environment.NewLine +
            string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void ProcessAndE2ETestSources_MatchReviewedManifest()
    {
        var violations = ProcessAndE2ECategories
            .SelectMany(entry =>
            {
                var source = File.ReadAllText(SourcePath(IntegrationTestsDirectory, entry.Key));
                return entry.Value
                    .Where(trait => !source.Contains(trait, StringComparison.Ordinal))
                    .Select(trait => $"{entry.Key}: missing {trait}");
            })
            .ToArray();

        Assert.True(
            violations.Length == 0,
            "Process/E2E source classification is incomplete:" +
            Environment.NewLine +
            string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void FileBackedRegressionIntegrationSources_MatchReviewedManifest()
    {
        var violations = RegressionIntegrationSources
            .Where(fileName => !File
                .ReadAllText(SourcePath(IntegrationTestsDirectory, fileName))
                .Contains(RegressionIntegrationTrait, StringComparison.Ordinal))
            .ToArray();

        Assert.True(
            violations.Length == 0,
            "File-backed regression integration sources must carry " +
            "Category=RegressionIntegration:" +
            Environment.NewLine +
            string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void PartialTestClasses_AreOwnedByExactlyOneTestSourceRoot()
    {
        var partialClass = new Regex(
            @"partial\s+class\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)",
            RegexOptions.CultureInvariant);
        var roots = new[]
        {
            SourcePath(FastTestsDirectory),
            SourcePath(IntegrationTestsDirectory)
        };
        var declarations = roots
            .SelectMany(root => Directory
                .EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
                .SelectMany(path => partialClass
                    .Matches(File.ReadAllText(path))
                    .Select(match => (
                        Name: match.Groups["name"].Value,
                        Root: root,
                        Path: path))))
            .ToArray();
        var violations = declarations
            .GroupBy(declaration => declaration.Name, StringComparer.Ordinal)
            .Select(group => new
            {
                Name = group.Key,
                Roots = group
                    .Select(declaration => declaration.Root)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                Paths = group
                    .Select(declaration => Path.GetRelativePath(
                        TestRepoPaths.RepoRoot,
                        declaration.Path))
                    .Order(StringComparer.Ordinal)
                    .ToArray()
            })
            .Where(entry => entry.Roots.Length != 1)
            .Select(entry => $"{entry.Name}: {string.Join(", ", entry.Paths)}")
            .ToArray();

        Assert.NotEmpty(declarations);
        Assert.Empty(violations);
    }

    [Fact]
    public void GuardianRegressionTests_UseExactReviewedDomainProfileMapping()
    {
        foreach (var (fileName, profileName) in GuardianProfiles)
        {
            var source = File.ReadAllText(SourcePath(IntegrationTestsDirectory, fileName));

            Assert.Contains(
                $"ValidateGameStateAsync(GuardianValidationProfiles.{profileName})",
                source,
                StringComparison.Ordinal);
        }
    }

    [Fact]
    public void GuardianRegressionTests_BroadValidationCallsStayWithinSentinelBudget()
    {
        var sources = GuardianProfiles.Keys.ToDictionary(
            fileName => fileName,
            fileName => File.ReadAllText(SourcePath(IntegrationTestsDirectory, fileName)),
            StringComparer.Ordinal);

        AssertGuardianBroadCallBudget(sources);
    }

    [Fact]
    public void GuardianRegressionTests_NinthBroadCallFailsWithCountAndRemediation()
    {
        var sources = Enumerable
            .Range(1, GuardianFullValidationSentinelBudget + 1)
            .ToDictionary(
                index => $"SyntheticGuardian{index}.cs",
                _ => FullValidationTrait + Environment.NewLine +
                     "await validator.ValidateGameState" + "Async();",
                StringComparer.Ordinal);

        var exception = Assert.ThrowsAny<Xunit.Sdk.XunitException>(
            () => AssertGuardianBroadCallBudget(sources));

        Assert.Contains("budget is 8", exception.Message, StringComparison.Ordinal);
        Assert.Contains("found 9", exception.Message, StringComparison.Ordinal);
        Assert.Contains(
            "reviewed GuardianValidationProfiles",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void AfterlifeSpiritualConflictValidationTests_UseScopedPhase()
    {
        var source = File.ReadAllText(SourcePath(
            IntegrationTestsDirectory,
            "AfterlifeSpiritualConflictValidationTests.cs"));
        var broadValidationCall = "_validator.ValidateGameStateAsync" + "()";

        Assert.DoesNotContain(
            broadValidationCall,
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "GameStateValidationPhase.AfterlifeSpiritualConflictState",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            FullValidationTrait,
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ScopedValidationEntryPoint_RemainsInternalToValidationServiceAndTests()
    {
        var productionRoot = SourcePath("BookOfEternityClient");
        var scopedDeclaration = new Regex(
            @"internal\s+Task<List<ValidationIssue>>\s+ValidateGameStateAsync\s*\(",
            RegexOptions.CultureInvariant);
        var violations = Directory
            .EnumerateFiles(productionRoot, "*.cs", SearchOption.AllDirectories)
            .Select(path => (Path: path, Source: File.ReadAllText(path)))
            .Where(candidate => scopedDeclaration.IsMatch(candidate.Source))
            .Where(candidate => !string.Equals(
                Path.GetRelativePath(productionRoot, candidate.Path),
                Path.Combine("Services", "ValidationService.cs"),
                StringComparison.Ordinal))
            .Select(candidate => Path.GetRelativePath(productionRoot, candidate.Path))
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void CSharpLaneRunner_DefinesReviewedLanesFiltersArtifactsAndOwnedTreeTimeout()
    {
        var runnerPath = SourcePath("scripts", "test-csharp.ps1");

        Assert.True(File.Exists(runnerPath), $"C# lane runner not found: {runnerPath}");

        var source = File.ReadAllText(runnerPath);
        foreach (var lane in RunnerLaneTokens)
        {
            Assert.Contains($"\"{lane}\"", source, StringComparison.Ordinal);
        }

        Assert.Contains(
            "Category!=FullValidation&Category!=ProcessIntegration&Category!=E2E&" +
            "Category!=RegressionIntegration",
            source,
            StringComparison.Ordinal);
        Assert.Contains("Category=FullValidation", source, StringComparison.Ordinal);
        Assert.Contains("Category=ProcessIntegration", source, StringComparison.Ordinal);
        Assert.Contains("Category=E2E", source, StringComparison.Ordinal);
        Assert.Contains("Category=RegressionIntegration", source, StringComparison.Ordinal);
        Assert.Contains("test-results.trx", source, StringComparison.Ordinal);
        Assert.Contains("dotnet-test.log", source, StringComparison.Ordinal);
        Assert.Contains("Stopwatch", source, StringComparison.Ordinal);
        Assert.Contains("$FastParallelismLimit = 2", source, StringComparison.Ordinal);
        Assert.Contains("$ComposedSmallClassBinCount = 4", source, StringComparison.Ordinal);
        Assert.Contains("$CompleteMixedEstimatedCost = 175", source, StringComparison.Ordinal);
        Assert.Contains("WaitForExit", source, StringComparison.Ordinal);
        Assert.Contains("Kill($true)", source, StringComparison.Ordinal);
        Assert.Contains("ArgumentList.Add", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Get-Process", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Stop-Process", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("taskkill", source, StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<(string Path, string Source)>
        EnumerateIntegrationAndSupportSources()
    {
        foreach (var directory in new[] { IntegrationTestsDirectory, TestSupportDirectory })
        {
            foreach (var path in Directory.EnumerateFiles(
                         SourcePath(directory),
                         "*.cs",
                         SearchOption.AllDirectories))
            {
                yield return (Path.GetFullPath(path), File.ReadAllText(path));
            }
        }
    }

    private static string SourcePath(params string[] relativeParts) =>
        Path.GetFullPath(Path.Combine(
            new[] { TestRepoPaths.RepoRoot }.Concat(relativeParts).ToArray()));

    private static int LineNumber(string source, int characterIndex)
    {
        return source.AsSpan(0, characterIndex).Count('\n') + 1;
    }

    private static void AssertGuardianBroadCallBudget(
        IReadOnlyDictionary<string, string> sources)
    {
        var broadCall = new Regex(
            @"\.ValidateGameState" + @"Async\s*\(\s*\)",
            RegexOptions.CultureInvariant);
        var broadCalls = sources
            .SelectMany(entry => broadCall
                .Matches(entry.Value)
                .Select(match => $"{entry.Key}:{LineNumber(entry.Value, match.Index)}"))
            .ToArray();
        var uncategorizedSources = sources
            .Where(entry =>
                broadCall.IsMatch(entry.Value) &&
                !entry.Value.Contains(FullValidationTrait, StringComparison.Ordinal))
            .Select(entry => entry.Key)
            .ToArray();

        Assert.True(
            broadCalls.Length <= GuardianFullValidationSentinelBudget,
            $"Guardian regression broad-validation budget is " +
            $"{GuardianFullValidationSentinelBudget}, but found {broadCalls.Length}:" +
            Environment.NewLine +
            string.Join(Environment.NewLine, broadCalls) +
            Environment.NewLine +
            "Replace unapproved broad calls with reviewed GuardianValidationProfiles.");
        Assert.True(
            uncategorizedSources.Length == 0,
            "Every retained Guardian broad-validation sentinel must carry " +
            "Category=FullValidation:" +
            Environment.NewLine +
            string.Join(Environment.NewLine, uncategorizedSources));
    }
}
