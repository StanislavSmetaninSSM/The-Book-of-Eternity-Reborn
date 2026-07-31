using BookOfEternityClient.Services;
using System.Reflection;
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

    private static readonly IReadOnlyDictionary<string, string> ActorAndAfterlifeScopedSources =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ActorMaterializationValidationTests.cs"] = "ActorMaterialization",
            ["AfterlifeActiveThreatValidationTests.cs"] = "AfterlifeActiveThreat",
            ["AfterlifeArchiveActionStateTests.cs"] = "AfterlifeArchive",
            ["AfterlifeChronicleValidationTests.cs"] = "AfterlifeChronicle",
            ["AfterlifeEntityProfileValidationTests.cs"] = "AfterlifeEntityProfile",
            ["AfterlifeGlobalFlagValidationTests.cs"] = "AfterlifeGlobalFlag",
            ["AfterlifeRealmSegregationValidationTests.cs"] = "AfterlifeRealm",
            ["AfterlifeSpiritualConflictBalanceTests.cs"] = "AfterlifeConflict",
            ["AfterlifeStoryOutlineValidationTests.cs"] = "AfterlifeStory",
            ["FactionIdentityValidationTests.cs"] = "FactionState",
            ["RealmSemanticsValidationTests.cs"] = "RealmSemantics",
            ["SoulIdentityValidationTests.cs"] = "SoulIdentity"
        };

    private static readonly string[] GuardianPartialSources =
    [
        "GuardianSystemRegressionTests.AcceptedAuthority.cs",
        "GuardianSystemRegressionTests.ActorBrain.cs",
        "GuardianSystemRegressionTests.cs",
        "GuardianSystemRegressionTests.IdleValidation.cs",
        "GuardianSystemRegressionTests.LifecycleSnapshots.cs",
        "GuardianSystemRegressionTests.PowerJournalOfferings.cs",
        "GuardianSystemRegressionTests.ProjectsPower.cs",
        "GuardianSystemRegressionTests.QuestProgress.cs",
        "GuardianSystemRegressionTests.RivalResidents.cs",
        "GuardianSystemRegressionTests.TradeOfferingResonance.cs",
        "MortalFactPersistenceValidationTests.cs"
    ];

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
        "AfterlifeSpiritualConflictValidationTests.cs",
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

    private static readonly string[] ScopedValidationServiceSources =
    [
        Path.Combine("Services", "ValidationService.cs")
    ];

    [Fact]
    public void IntegrationValidationProfiles_AreNonEmptyAndSelectable()
    {
        var profiles = typeof(IntegrationValidationProfiles)
            .GetFields(BindingFlags.Static | BindingFlags.NonPublic)
            .Where(field => field.FieldType == typeof(GameStateValidationSelection))
            .OrderBy(field => field.Name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
        [
            "ActorMaterialization",
            "AfterlifeActiveThreat",
            "AfterlifeArchive",
            "AfterlifeChronicle",
            "AfterlifeConflict",
            "AfterlifeEntityProfile",
            "AfterlifeGlobalFlag",
            "AfterlifeRealm",
            "AfterlifeStory",
            "CanonicalGuardian",
            "CanonicalInventory",
            "CanonicalNpc",
            "CommandDisplaySave",
            "FactionState",
            "GuardianArchiveTrade",
            "GuardianPolicy",
            "MechanicalBonus",
            "MortalBootstrap",
            "NpcState",
            "PlayerGuardian",
            "Qte",
            "QuestReward",
            "ReadableDocument",
            "RealmSemantics",
            "SarefStory",
            "ShiningState",
            "SoulIdentity",
            "SourceOfLight",
            "SystemGuardianLibrary",
            "Training",
            "Weather"
        ],
        profiles.Select(field => field.Name));

        Assert.NotEmpty(profiles);
        Assert.All(profiles, field =>
        {
            var profile = (GameStateValidationSelection)field.GetValue(null)!;
            Assert.NotEqual(GameStateValidationPhase.None, profile.Phases);
            Assert.Equal(
                GameStateValidationPhase.None,
                profile.Phases & ~GameStateValidationPhase.Selectable);
        });
    }

    [Fact]
    public void ActorAndAfterlifeValidationSources_UseScopedProfiles()
    {
        var violations = ActorAndAfterlifeScopedProfileViolations(
            ActorAndAfterlifeScopedSources.Select(mapping => (
                FileName: mapping.Key,
                ProfileName: mapping.Value,
                Source: File.ReadAllText(
                    SourcePath(IntegrationTestsDirectory, mapping.Key)))));

        Assert.True(
            violations.Length == 0,
            "Actor and afterlife validation source violations:" +
            Environment.NewLine +
            string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void ActorAndAfterlifeValidationSources_UseScopedProfiles_RejectsWhitespaceOnlyArgument()
    {
        var methodName = "ValidateGameState" + "Async";
        var source = $"await validator.{methodName} ({Environment.NewLine}    );";

        var violation = Assert.Single(ActorAndAfterlifeScopedProfileViolations(
        [
            ("Whitespace.cs", "Expected", source)
        ]));

        Assert.Contains("<empty>", violation, StringComparison.Ordinal);
    }

    [Fact]
    public void ActorAndAfterlifeValidationSources_UseScopedProfiles_RejectsWrongProfile()
    {
        var methodName = "ValidateGameState" + "Async";
        var profileType = "IntegrationValidation" + "Profiles";
        var source = $"await validator.{methodName}({profileType}.Wrong);";

        var violation = Assert.Single(ActorAndAfterlifeScopedProfileViolations(
        [
            ("Wrong.cs", "Expected", source)
        ]));

        Assert.Contains($"{profileType}.Expected", violation, StringComparison.Ordinal);
        Assert.Contains($"{profileType}.Wrong", violation, StringComparison.Ordinal);
    }

    [Fact]
    public void ActorAndAfterlifeValidationSources_UseScopedProfiles_RejectsMixedProfiles()
    {
        var methodName = "ValidateGameState" + "Async";
        var profileType = "IntegrationValidation" + "Profiles";
        var source =
            $"await validator.{methodName}({profileType}.Expected);" +
            Environment.NewLine +
            $"await validator.{methodName}({profileType}.Wrong);";

        var violation = Assert.Single(ActorAndAfterlifeScopedProfileViolations(
        [
            ("Mixed.cs", "Expected", source)
        ]));

        Assert.Contains($"{profileType}.Wrong", violation, StringComparison.Ordinal);
    }

    [Fact]
    public void ActorAndAfterlifeValidationSources_UseScopedProfiles_RejectsCommentOnlyProfileToken()
    {
        var profileType = "IntegrationValidation" + "Profiles";
        var source = $"// {profileType}.Expected";

        var violation = Assert.Single(ActorAndAfterlifeScopedProfileViolations(
        [
            ("CommentOnly.cs", "Expected", source)
        ]));

        Assert.Contains("no member-call", violation, StringComparison.Ordinal);
    }

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
        AssertExactCategoryManifest(
            ProcessAndE2ECategories,
            [ProcessIntegrationTrait, E2ETrait],
            "Process/E2E");
    }

    [Fact]
    public void FileBackedRegressionIntegrationSources_MatchReviewedManifest()
    {
        var expected = RegressionIntegrationSources.ToDictionary(
            fileName => fileName,
            _ => new[] { RegressionIntegrationTrait },
            StringComparer.Ordinal);

        AssertExactCategoryManifest(
            expected,
            [RegressionIntegrationTrait],
            "RegressionIntegration");
    }

    [Fact]
    public void ExactCategoryManifest_RejectsUnlistedSourcesAndTraitDrift()
    {
        var expected = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["Expected.cs"] = [ProcessIntegrationTrait],
            ["Missing.cs"] = [E2ETrait]
        };
        var sources = new[]
        {
            (
                RelativePath: "Expected.cs",
                Source: ProcessIntegrationTrait + Environment.NewLine + E2ETrait),
            (
                RelativePath: "Unexpected.cs",
                Source: ProcessIntegrationTrait)
        };

        var violations = ExactCategoryManifestViolations(
            expected,
            sources,
            [ProcessIntegrationTrait, E2ETrait]);

        Assert.Contains(
            $"Expected.cs: unexpected {E2ETrait}",
            violations,
            StringComparer.Ordinal);
        Assert.Contains(
            $"Missing.cs: missing {E2ETrait}",
            violations,
            StringComparer.Ordinal);
        Assert.Contains(
            $"Unexpected.cs: unreviewed classification {ProcessIntegrationTrait}",
            violations,
            StringComparer.Ordinal);
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
    public void GuardianRegressionTests_MatchCompleteReviewedPartialSourceSet()
    {
        var guardianPartial = new Regex(
            @"partial\s+class\s+GuardianSystemRegressionTests\b",
            RegexOptions.CultureInvariant);
        var integrationRoot = SourcePath(IntegrationTestsDirectory);
        var discovered = Directory
            .EnumerateFiles(integrationRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => guardianPartial.IsMatch(File.ReadAllText(path)))
            .Select(path => Path.GetRelativePath(integrationRoot, path))
            .Order(StringComparer.Ordinal)
            .ToArray();
        var expected = GuardianPartialSources
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expected, discovered);
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
        var sources = GuardianPartialSources.ToDictionary(
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
    public void RuntimeValidationCallers_UseParameterlessFacadeOutsideValidationService()
    {
        var productionRoot = SourcePath("BookOfEternityClient");
        var sources = Directory
            .EnumerateFiles(productionRoot, "*.cs", SearchOption.AllDirectories)
            .Select(path => (
                RelativePath: Path.GetRelativePath(productionRoot, path),
                Source: File.ReadAllText(path)))
            .ToArray();
        var declarationLocations = ScopedValidationDeclarationLocations(sources);
        var declarationViolations = ScopedValidationDeclarationViolations(
            sources,
            ScopedValidationServiceSources);
        var callViolations = ArgumentBearingValidationCallViolations(
            sources,
            ScopedValidationServiceSources);

        Assert.NotEmpty(declarationLocations);
        Assert.True(
            declarationViolations.Length == 0,
            "Scoped validation declarations must stay in the reviewed ValidationService source:" +
            Environment.NewLine +
            string.Join(Environment.NewLine, declarationViolations));
        Assert.True(
            callViolations.Length == 0,
            "Production callers outside ValidationService must use the parameterless facade:" +
            Environment.NewLine +
            string.Join(Environment.NewLine, callViolations));
    }

    [Fact]
    public void RuntimeValidationGuard_RejectsScopedDeclarationAndCallOutsideAllowedSource()
    {
        var sources = new[]
        {
            (
                RelativePath: Path.Combine("Services", "ValidationService.cs"),
                Source:
                    """
                    internal Task < List < ValidationIssue > >
                        ValidateGameStateAsync (
                            GameStateValidationSelection selection)
                    """),
            (
                RelativePath: Path.Combine("Services", "UnexpectedValidator.cs"),
                Source:
                    """
                    internal Task<List<ValidationIssue>>
                        ValidateGameStateAsync(GameStateValidationSelection selection);
                    """ +
                    Environment.NewLine +
                    "await validator.ValidateGameState" + "Async(selection);" +
                    Environment.NewLine +
                    "await validator.ValidateGameState" + "Async();")
        };

        var declarationViolations = ScopedValidationDeclarationViolations(
            sources,
            ScopedValidationServiceSources);
        var callViolations = ArgumentBearingValidationCallViolations(
            sources,
            ScopedValidationServiceSources);

        Assert.Single(declarationViolations);
        Assert.Contains(
            "UnexpectedValidator.cs",
            declarationViolations[0],
            StringComparison.Ordinal);
        Assert.Single(callViolations);
        Assert.Contains(
            "UnexpectedValidator.cs",
            callViolations[0],
            StringComparison.Ordinal);
    }

    private static void AssertExactCategoryManifest(
        IReadOnlyDictionary<string, string[]> expected,
        string[] classifiedTraits,
        string manifestDescription)
    {
        var violations = ExactCategoryManifestViolations(
            expected,
            EnumerateSourceFiles(IntegrationTestsDirectory),
            classifiedTraits);

        Assert.True(
            violations.Length == 0,
            $"{manifestDescription} source classification differs from the reviewed manifest:" +
            Environment.NewLine +
            string.Join(Environment.NewLine, violations));
    }

    private static string[] ExactCategoryManifestViolations(
        IReadOnlyDictionary<string, string[]> expected,
        IEnumerable<(string RelativePath, string Source)> sources,
        string[] classifiedTraits)
    {
        var actual = sources
            .Select(source => (
                source.RelativePath,
                Traits: classifiedTraits
                    .Where(trait => source.Source.Contains(trait, StringComparison.Ordinal))
                    .ToArray()))
            .Where(source => source.Traits.Length > 0)
            .ToDictionary(
                source => source.RelativePath,
                source => source.Traits,
                StringComparer.Ordinal);
        var violations = new List<string>();

        foreach (var (relativePath, expectedTraits) in expected)
        {
            actual.TryGetValue(relativePath, out var actualTraits);
            actualTraits ??= [];

            violations.AddRange(expectedTraits
                .Except(actualTraits, StringComparer.Ordinal)
                .Select(trait => $"{relativePath}: missing {trait}"));
            violations.AddRange(actualTraits
                .Except(expectedTraits, StringComparer.Ordinal)
                .Select(trait => $"{relativePath}: unexpected {trait}"));
        }

        violations.AddRange(actual
            .Where(entry => !expected.ContainsKey(entry.Key))
            .Select(entry =>
                $"{entry.Key}: unreviewed classification {string.Join(", ", entry.Value)}"));

        return violations
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static string[] ScopedValidationDeclarationLocations(
        IEnumerable<(string RelativePath, string Source)> sources)
    {
        var scopedDeclaration = new Regex(
            @"\binternal\s+(?:async\s+)?Task\s*<\s*List\s*<\s*" +
            @"ValidationIssue\s*>\s*>\s+ValidateGameStateAsync\s*\(",
            RegexOptions.CultureInvariant);

        return sources
            .SelectMany(source => scopedDeclaration
                .Matches(source.Source)
                .Select(match =>
                    $"{source.RelativePath}:{LineNumber(source.Source, match.Index)}"))
            .ToArray();
    }

    private static string[] ScopedValidationDeclarationViolations(
        IEnumerable<(string RelativePath, string Source)> sources,
        IReadOnlyCollection<string> allowedSources)
    {
        var allowed = allowedSources.ToHashSet(StringComparer.OrdinalIgnoreCase);

        return ScopedValidationDeclarationLocations(sources)
            .Where(location =>
            {
                var separatorIndex = location.LastIndexOf(':');
                var relativePath = separatorIndex < 0
                    ? location
                    : location[..separatorIndex];
                return !allowed.Contains(relativePath);
            })
            .ToArray();
    }

    private static string[] ArgumentBearingValidationCallViolations(
        IEnumerable<(string RelativePath, string Source)> sources,
        IReadOnlyCollection<string> allowedSources)
    {
        var allowed = allowedSources.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var scopedCall = new Regex(
            @"\.ValidateGameStateAsync\s*\(\s*(?<argument>[^\s\)])",
            RegexOptions.CultureInvariant);

        return sources
            .Where(source => !allowed.Contains(source.RelativePath))
            .SelectMany(source => scopedCall
                .Matches(source.Source)
                .Select(match =>
                    $"{source.RelativePath}:{LineNumber(source.Source, match.Index)}"))
            .ToArray();
    }

    private static IEnumerable<(string RelativePath, string Source)> EnumerateSourceFiles(
        string directory)
    {
        var root = SourcePath(directory);

        return Directory
            .EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Select(path => (
                RelativePath: Path.GetRelativePath(root, path),
                Source: File.ReadAllText(path)));
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

    private static string[] ActorAndAfterlifeScopedProfileViolations(
        IEnumerable<(string FileName, string ProfileName, string Source)> sources)
    {
        var methodName = "ValidateGameState" + "Async";
        var profileType = "IntegrationValidation" + "Profiles";
        var invocation = new Regex(
            @"\." + Regex.Escape(methodName) + @"\s*\((?<argument>[^)]*)\)",
            RegexOptions.CultureInvariant);
        var violations = new List<string>();

        foreach (var (fileName, profileName, source) in sources)
        {
            var expectedArgument = $"{profileType}.{profileName}";
            var matches = invocation.Matches(source);

            if (matches.Count == 0)
            {
                violations.Add(
                    $"{fileName}: contains no member-call to {methodName}");
                continue;
            }

            foreach (Match match in matches)
            {
                var argument = match.Groups["argument"].Value.Trim();

                if (string.Equals(argument, expectedArgument, StringComparison.Ordinal))
                {
                    continue;
                }

                var displayedArgument = argument.Length == 0 ? "<empty>" : argument;
                violations.Add(
                    $"{fileName}:{LineNumber(source, match.Index)}: expected " +
                    $"{expectedArgument}; found {displayedArgument}");
            }
        }

        return violations.ToArray();
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
