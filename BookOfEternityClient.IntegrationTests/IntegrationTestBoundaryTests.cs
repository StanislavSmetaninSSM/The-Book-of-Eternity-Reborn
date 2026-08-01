using BookOfEternityClient.Services;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Text.Json;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class IntegrationTestBoundaryTests
{
    private const int BroadValidationSentinelBudget = 8;
    private const int ReviewedBroadValidationCallCount = 7;
    private const int GuardianFullValidationSentinelBudget = 8;
    private const string FastTestsDirectory = "BookOfEternityClient.Tests";
    private const string IntegrationTestsDirectory = "BookOfEternityClient.IntegrationTests";
    private const string TestSupportDirectory = "BookOfEternityClient.TestSupport";
    private const string FullValidationTrait = "[Trait(\"Category\", \"FullValidation\")]";
    private const string DeepValidationTrait =
        "[Trait(\"Category\", \"DeepValidation\")]";
    private const string PreMergeSentinelTrait =
        "[Trait(\"Category\", \"PreMergeSentinel\")]";
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

    private static readonly IReadOnlyDictionary<string, string> GuardianNpcAndCanonicalScopedSources =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["GuardianArchiveAndTradeRequestValidationTests.cs"] = "GuardianArchiveTrade",
            ["GuardianPolicyKernelTests.cs"] = "GuardianPolicy",
            ["GuardianTradeServiceTests.cs"] = "GuardianArchiveTrade",
            ["ChaosSeaGuardianPoliticsStateTests.cs"] = "GuardianPolicy",
            ["ChaosSeaPendingRequestHygieneTests.cs"] = "AfterlifeRealm",
            ["PlayerGuardianFoundationValidationTests.cs"] = "PlayerGuardian",
            ["QuestRewardAuthorityValidationTests.cs"] = "QuestReward",
            ["NpcCoreChangesTests.cs"] = "NpcState",
            ["NpcStateFileValidationTests.cs"] = "NpcState",
            ["NpcTradeRequestValidationTests.cs"] = "NpcState",
            ["CanonicalStateNormalizerTests.AfterlifeChronicles.cs"] = "AfterlifeChronicle",
            ["CanonicalStateNormalizerTests.GuardianProjects.cs"] = "CanonicalGuardian",
            ["CanonicalStateNormalizerTests.Inventory.cs"] = "CanonicalInventory",
            ["CanonicalStateNormalizerTests.Npcs.cs"] = "CanonicalNpc"
        };

    private static readonly IReadOnlyDictionary<string, string> MortalShiningStoryAndMiscScopedSources =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ChaosSeaCommandDisplaySaveTests.cs"] = "CommandDisplaySave",
            ["MathAssistantContractValidationTests.cs"] = "ReadableDocument",
            ["MechanicalBonusAuthorityValidationTests.cs"] = "MechanicalBonus",
            ["MortalBootstrapValidationTests.cs"] = "MortalBootstrap",
            ["MortalCommandDisplaySaveTests.cs"] = "CommandDisplaySave",
            ["ReadableDocumentAuthorityValidationTests.cs"] = "ReadableDocument",
            ["SarefMainStoryStateValidationTests.cs"] = "SarefStory",
            ["ShiningAbodeCommandDisplaySaveTests.cs"] = "CommandDisplaySave",
            ["ShiningPoliticalResolutionValidationTests.cs"] = "ShiningState",
            ["ShiningStateValidationTests.cs"] = "ShiningState",
            ["SourceOfLightCapstoneValidationTests.cs"] = "SourceOfLight",
            ["SystemGuardianLibraryServiceTests.cs"] = "SystemGuardianLibrary",
            ["TrainingValidationTests.cs"] = "Training",
            ["ValidationServiceQteTests.cs"] = "Qte",
            ["WeatherValidationTests.cs"] = "Weather"
        };

    private static readonly IReadOnlyDictionary<string, int> ReviewedBroadValidationCallManifest =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [$"{TestSupportDirectory}/ValidatorFixtureHarness.cs"] = 2,
            [$"{IntegrationTestsDirectory}/BookOfEternityClientGameSessionIntegrityTests.cs"] = 1,
            [$"{IntegrationTestsDirectory}/ExampleDocumentationValidationTests.cs"] = 1,
            [$"{IntegrationTestsDirectory}/FileSystemExampleFixtureIntegrityTests.cs"] = 2,
            [$"{IntegrationTestsDirectory}/FullValidationEquivalenceTests.cs"] = 1
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

    private static readonly IReadOnlyDictionary<string, string[]>
        RegressionIntegrationCategories =
            RegressionIntegrationSources.ToDictionary(
                fileName => fileName,
                fileName => string.Equals(
                    fileName,
                    "GuardianSystemRegressionTests.cs",
                    StringComparison.Ordinal)
                        ? new[] { RegressionIntegrationTrait, DeepValidationTrait }
                        : new[] { RegressionIntegrationTrait },
                StringComparer.Ordinal);

    private static readonly string[] BroadValidationPreMergeSentinelSources =
    [
        "FullValidationEquivalenceTests.cs"
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
    public void CSharpLaneRunner_DefinesNonOverlappingProjectRoutedPreMergeSchedule()
    {
        var runnerPath = Path.Combine(TestRepoPaths.RepoRoot, "scripts", "test-csharp.ps1");
        var source = File.ReadAllText(runnerPath);
        var normalized = Regex.Replace(source, @"\s+", " ");

        var lanes = new[]
        {
            "Fast",
            "Focused",
            "FullValidation",
            "RegressionIntegration",
            "ProcessIntegration",
            "E2E",
            "Complete",
            "PreMerge"
        };
        Assert.All(lanes, lane =>
            Assert.Contains($"\"{lane}\"", source, StringComparison.Ordinal));

        var diagnosticDefinitions = new[]
        {
            "FullValidation = @{ Project = \"Integration\" " +
            "Filter = \"Category=FullValidation\" TimeoutMinutes = 15 }",
            "RegressionIntegration = @{ Project = \"Integration\" " +
            "Filter = \"Category=RegressionIntegration\" TimeoutMinutes = 15 }",
            "ProcessIntegration = @{ Project = \"Integration\" " +
            "Filter = \"Category=ProcessIntegration\" TimeoutMinutes = 15 }",
            "E2E = @{ Project = \"Integration\" " +
            "Filter = \"Category=E2E\" TimeoutMinutes = 15 }",
            "PreMerge = @{ Project = \"Both\" Filter = $null TimeoutMinutes = 15 }"
        };
        Assert.All(diagnosticDefinitions, definition =>
            Assert.Contains(definition, normalized, StringComparison.Ordinal));

        var requiredTokens = new[]
        {
            "$effectiveLane = if ($Lane -eq \"Complete\") { \"PreMerge\" } else { $Lane }",
            "if ($TimeoutMinutes -gt [int]$laneDefinition.TimeoutMinutes)",
            "hard limit of $($laneDefinition.TimeoutMinutes) minute(s)",
            "Category=FullValidation",
            "Category=RegressionIntegration",
            "Category=ProcessIntegration",
            "Category=E2E",
            "Category!=ProcessIntegration&Category!=E2E",
            "Category=ProcessIntegration&Category!=E2E",
            "$PreMergeParallelism = 4",
            "$PreMergeFastParallelismLimit = 2",
            "Build-Fast",
            "Build-Integration",
            "Frontend-verify",
            "-FileName $npmCommandPath",
            "@(\"run\", \"verify\", \"--prefix\", " +
            "\"BookOfEternityClient.WebFrontend\")",
            "Select-Object Phase, Name, Project, Filter, EstimatedCases, EstimatedCost",
            "//*[local-name()='UnitTestResult']",
            "//*[local-name()='UnitTest']",
            "GetAttribute(\"testId\")",
            "GetAttribute(\"storage\")",
            "$seenInTrx",
            "has no UnitTest storage mapping",
            "Group-Object Key",
            "Select-Object -ExpandProperty TestId",
            "Where-Object Count -gt 1",
            "$initialCleanupSucceeded",
            "FinalizerRetried",
            "$OwnedCleanupPassLimit = 2",
            "Get-OwnedCleanupDisposition",
            "Live owned process retained after bounded cleanup retries",
            "Owned cleanup diagnostics",
            "DuplicateTests",
            "summary.json",
            "ConvertTo-Json -Depth 4",
            "6560"
        };
        Assert.All(requiredTokens, token =>
            Assert.Contains(token, source, StringComparison.Ordinal));

        Assert.Single(Regex.Matches(source, @"\$deadlineUtc\s*="));

        var forbiddenBroadProcessCommands = new[]
        {
            "Get-" + "Process",
            "Stop-" + "Process",
            "task" + "kill"
        };
        Assert.All(forbiddenBroadProcessCommands, command =>
            Assert.DoesNotContain(command, source, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CSharpLaneRunner_RepeatedTheoryRowsWithinOneTrxAreNotDuplicates()
    {
        var fixtureDirectory = CreateTrxFixtureDirectory();
        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(fixtureDirectory, "theory.trx"),
                SyntheticTrx("theory-id", "integration-tests.dll", resultCount: 2));

            var probe = await RunCSharpRunnerTrxSelfTestAsync(fixtureDirectory);

            Assert.True(
                probe.ExitCode == 0,
                $"Theory-row probe failed.{Environment.NewLine}" +
                $"stdout:{Environment.NewLine}{probe.StandardOutput}{Environment.NewLine}" +
                $"stderr:{Environment.NewLine}{probe.StandardError}");
            using var summary = JsonDocument.Parse(await File.ReadAllTextAsync(
                Path.Combine(
                    ResultDirectoryFrom(probe.StandardOutput),
                    "self-test-summary.json")));
            var duplicateTests = summary.RootElement.GetProperty("DuplicateTests");
            Assert.Equal(JsonValueKind.Array, duplicateTests.ValueKind);
            Assert.Empty(duplicateTests.EnumerateArray());
        }
        finally
        {
            Directory.Delete(fixtureDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task CSharpLaneRunner_SameTestIdAcrossDescriptorTrxFilesIsDuplicate()
    {
        var fixtureDirectory = CreateTrxFixtureDirectory();
        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(fixtureDirectory, "descriptor-01.trx"),
                SyntheticTrx("shared-id", "integration-tests.dll", resultCount: 1));
            await File.WriteAllTextAsync(
                Path.Combine(fixtureDirectory, "descriptor-02.trx"),
                SyntheticTrx("shared-id", "integration-tests.dll", resultCount: 1));

            var probe = await RunCSharpRunnerTrxSelfTestAsync(fixtureDirectory);

            Assert.Equal(1, probe.ExitCode);
            Assert.Contains(
                "duplicate TRX test IDs: shared-id",
                probe.StandardOutput,
                StringComparison.Ordinal);
            using var summary = JsonDocument.Parse(await File.ReadAllTextAsync(
                Path.Combine(
                    ResultDirectoryFrom(probe.StandardOutput),
                    "self-test-summary.json")));
            var duplicateTests = summary.RootElement.GetProperty("DuplicateTests");
            Assert.Equal(JsonValueKind.Array, duplicateTests.ValueKind);
            Assert.Equal(
                "shared-id",
                Assert.Single(duplicateTests.EnumerateArray()).GetString());
        }
        finally
        {
            Directory.Delete(fixtureDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task CSharpLaneRunner_SameTestIdInDifferentAssembliesIsNotDuplicate()
    {
        var fixtureDirectory = CreateTrxFixtureDirectory();
        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(fixtureDirectory, "fast-descriptor.trx"),
                SyntheticTrx("shared-id", "fast-tests.dll", resultCount: 1));
            await File.WriteAllTextAsync(
                Path.Combine(fixtureDirectory, "integration-descriptor.trx"),
                SyntheticTrx("shared-id", "integration-tests.dll", resultCount: 1));

            var probe = await RunCSharpRunnerTrxSelfTestAsync(fixtureDirectory);

            Assert.True(
                probe.ExitCode == 0,
                $"Assembly-scoped probe failed.{Environment.NewLine}" +
                $"stdout:{Environment.NewLine}{probe.StandardOutput}{Environment.NewLine}" +
                $"stderr:{Environment.NewLine}{probe.StandardError}");
            using var summary = JsonDocument.Parse(await File.ReadAllTextAsync(
                Path.Combine(
                    ResultDirectoryFrom(probe.StandardOutput),
                    "self-test-summary.json")));
            Assert.Empty(
                summary.RootElement
                    .GetProperty("DuplicateTests")
                    .EnumerateArray());
        }
        finally
        {
            Directory.Delete(fixtureDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task CSharpLaneRunner_MissingStorageMappingFailsClosed()
    {
        var fixtureDirectory = CreateTrxFixtureDirectory();
        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(fixtureDirectory, "missing-storage.trx"),
                SyntheticTrx("unmapped-id", storage: null, resultCount: 1));

            var probe = await RunCSharpRunnerTrxSelfTestAsync(fixtureDirectory);

            Assert.Equal(1, probe.ExitCode);
            Assert.Contains(
                "has no UnitTest storage mapping",
                probe.StandardOutput,
                StringComparison.Ordinal);
            Assert.DoesNotContain(
                "duplicate TRX test IDs",
                probe.StandardOutput,
                StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(fixtureDirectory, recursive: true);
        }
    }

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
    public void GuardianNpcAndCanonicalValidationSources_UseScopedProfiles()
    {
        var violations = ActorAndAfterlifeScopedProfileViolations(
            GuardianNpcAndCanonicalScopedSources.Select(mapping => (
                FileName: mapping.Key,
                ProfileName: mapping.Value,
                Source: File.ReadAllText(
                    SourcePath(IntegrationTestsDirectory, mapping.Key)))));

        Assert.True(
            violations.Length == 0,
            "Guardian, NPC, and canonical validation source violations:" +
            Environment.NewLine +
            string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void MortalShiningStoryAndMiscValidationSources_UseScopedProfiles()
    {
        var violations = ActorAndAfterlifeScopedProfileViolations(
            MortalShiningStoryAndMiscScopedSources.Select(mapping => (
                FileName: mapping.Key,
                ProfileName: mapping.Value,
                Source: File.ReadAllText(
                    SourcePath(IntegrationTestsDirectory, mapping.Key)))));

        Assert.True(
            violations.Length == 0,
            "Mortal, Shining, story, and miscellaneous validation source violations:" +
            Environment.NewLine +
            string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void BroadValidationCalls_MatchReviewedSevenCallManifest()
    {
        var callSites = EnumerateIntegrationAndSupportSources()
            .SelectMany(candidate =>
                ParameterlessValidationCallLocations(candidate.Path, candidate.Source))
            .Order(StringComparer.Ordinal)
            .ToArray();
        var violations = BroadValidationCallManifestViolations(callSites);

        Assert.True(
            violations.Length == 0,
            BroadValidationCallManifestFailure(callSites, violations));
    }

    [Fact]
    public void BroadValidationCalls_MatchReviewedSevenCallManifest_RejectsSameTotalPerFileDrift()
    {
        var callSites = ReviewedBroadValidationCallLocations().ToList();
        callSites.Remove($"{TestSupportDirectory}/ValidatorFixtureHarness.cs:1");
        callSites.Add(
            $"{IntegrationTestsDirectory}/BookOfEternityClientGameSessionIntegrityTests.cs:2");

        var violations = BroadValidationCallManifestViolations(callSites);

        Assert.Contains(
            violations,
            violation =>
                violation.Contains(
                    $"{TestSupportDirectory}/ValidatorFixtureHarness.cs: expected 2, found 1",
                    StringComparison.Ordinal) &&
                violation.Contains(
                    $"{IntegrationTestsDirectory}/BookOfEternityClientGameSessionIntegrityTests.cs: expected 1, found 2",
                    StringComparison.Ordinal));
    }

    [Fact]
    public void BroadValidationCalls_MatchReviewedSevenCallManifest_RejectsEighthUnreviewedCall()
    {
        var callSites = ReviewedBroadValidationCallLocations()
            .Append($"{IntegrationTestsDirectory}/UnreviewedBroadValidationTests.cs:42")
            .ToArray();
        var violations = BroadValidationCallManifestViolations(callSites);
        var message = BroadValidationCallManifestFailure(callSites, violations);

        Assert.Contains(
            violations,
            violation =>
                violation.Contains(
                    "observed 8 reaches sentinel budget 8",
                    StringComparison.Ordinal));
        Assert.Contains(
            $"{IntegrationTestsDirectory}/UnreviewedBroadValidationTests.cs: expected 0, found 1",
            message,
            StringComparison.Ordinal);
        Assert.Contains(
            "Broad-validation sentinel budget is 8",
            message,
            StringComparison.Ordinal);
        Assert.Contains(
            "Expected reviewed call sites: 7",
            message,
            StringComparison.Ordinal);
        Assert.Contains(
            "Replace repeated calls with IntegrationValidationProfiles or a narrower state-file selection.",
            message,
            StringComparison.Ordinal);
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
    public void ActorAndAfterlifeValidationSources_UseScopedProfiles_IgnoresCommentedInvocation()
    {
        var methodName = "ValidateGameState" + "Async";
        var profileType = "IntegrationValidation" + "Profiles";
        var invocation = $"await validator.{methodName}({profileType}.Expected);";
        var source = $"// {invocation}";

        var violation = Assert.Single(ActorAndAfterlifeScopedProfileViolations(
        [
            ("CommentedInvocation.cs", "Expected", source)
        ]));

        Assert.Contains("no member-call", violation, StringComparison.Ordinal);
    }

    [Fact]
    public void ActorAndAfterlifeValidationSources_UseScopedProfiles_IgnoresInvocationInString()
    {
        var methodName = "ValidateGameState" + "Async";
        var profileType = "IntegrationValidation" + "Profiles";
        var source =
            $"var text = \"validator.{methodName}({profileType}.Expected)\";";

        var violation = Assert.Single(ActorAndAfterlifeScopedProfileViolations(
        [
            ("StringInvocation.cs", "Expected", source)
        ]));

        Assert.Contains("no member-call", violation, StringComparison.Ordinal);
    }

    [Fact]
    public void ActorAndAfterlifeValidationSources_UseScopedProfiles_AcceptsTriviaAfterDot()
    {
        var methodName = "ValidateGameState" + "Async";
        var profileType = "IntegrationValidation" + "Profiles";
        var source =
            $"await validator. /* scope */ {methodName}({profileType}.Expected);";

        var violations = ActorAndAfterlifeScopedProfileViolations(
        [
            ("Trivia.cs", "Expected", source)
        ]);

        Assert.Empty(violations);
    }

    [Fact]
    public void ActorAndAfterlifeValidationSources_UseScopedProfiles_RejectsWrongProfileAfterDotTrivia()
    {
        var methodName = "ValidateGameState" + "Async";
        var profileType = "IntegrationValidation" + "Profiles";
        var source =
            $"await validator.{methodName}({profileType}.Expected);" +
            Environment.NewLine +
            $"await validator. /* scope */ {methodName}({profileType}.Wrong);";

        var violation = Assert.Single(ActorAndAfterlifeScopedProfileViolations(
        [
            ("WrongTrivia.cs", "Expected", source)
        ]));

        Assert.DoesNotContain("no member-call", violation, StringComparison.Ordinal);
        Assert.Contains($"{profileType}.Expected", violation, StringComparison.Ordinal);
        Assert.Contains($"{profileType}.Wrong", violation, StringComparison.Ordinal);
    }

    [Fact]
    public void IntegrationAndSupportBroadValidationSources_AreExplicitlyCategorized()
    {
        var broadCall = new Regex(
            @"\.ValidateGameState" + @"Async\s*\(\s*\)",
            RegexOptions.CultureInvariant);
        var supportHarnessPath = SourcePath(TestSupportDirectory, "ValidatorFixtureHarness.cs");
        var broadSentinelPaths = BroadValidationPreMergeSentinelSources
            .Select(fileName => SourcePath(IntegrationTestsDirectory, fileName))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var uncategorized = EnumerateIntegrationAndSupportSources()
            .Where(candidate => !string.Equals(
                candidate.Path,
                supportHarnessPath,
                StringComparison.OrdinalIgnoreCase))
            .Where(candidate => !broadSentinelPaths.Contains(candidate.Path))
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

        Assert.All(BroadValidationPreMergeSentinelSources, fileName =>
            Assert.Contains(
                PreMergeSentinelTrait,
                File.ReadAllText(SourcePath(IntegrationTestsDirectory, fileName)),
                StringComparison.Ordinal));
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
        AssertExactCategoryManifest(
            RegressionIntegrationCategories,
            [RegressionIntegrationTrait, DeepValidationTrait],
            "RegressionIntegration/DeepValidation");

        Assert.Equal(
            ["PreMergeSentinel"],
            CategoryTraits("FullValidationEquivalenceTests.cs"));
        Assert.Equal(
            ["PreMergeSentinel"],
            CategoryTraits("GuardianTradeServiceTests.cs"));
        Assert.Equal(
            ["DeepValidation", "RegressionIntegration"],
            CategoryTraits("GuardianSystemRegressionTests.cs"));

        var commandDisplayCategories =
            new Dictionary<string, IReadOnlyDictionary<string, string[]>>(StringComparer.Ordinal)
            {
                ["MortalCommandDisplaySaveTests.cs"] =
                    new Dictionary<string, string[]>(StringComparer.Ordinal)
                    {
                        ["NamedMortalCommandDisplaySave_IsDiscoverableLoadableValidAndRepeatable"] =
                            ["PreMergeSentinel"],
                        ["LoadedMortalCommandDisplaySave_RendersCoveredCommandInBrowserAndConsole"] =
                            ["FullValidation"],
                        ["LoadedMortalCommandDisplaySave_WorldNewsLocalizesVisibilityEnums"] =
                            ["FullValidation"]
                    },
                ["ChaosSeaCommandDisplaySaveTests.cs"] =
                    new Dictionary<string, string[]>(StringComparer.Ordinal)
                    {
                        ["NamedChaosSeaCommandDisplaySave_IsDiscoverableLoadableValidAndRepeatable"] =
                            ["PreMergeSentinel"],
                        ["NamedChaosSeaCommandDisplaySave_HasCleanAcceptedTurnBaselineForLiveE2E"] =
                            ["PreMergeSentinel"],
                        ["LoadedChaosSeaCommandDisplaySave_RendersAvailableCommandInBrowserAndConsole"] =
                            ["FullValidation"],
                        ["LoadedChaosSeaCommandDisplaySave_RendersRepresentativeDetailTargets"] =
                            ["FullValidation"]
                    },
                ["ShiningAbodeCommandDisplaySaveTests.cs"] =
                    new Dictionary<string, string[]>(StringComparer.Ordinal)
                    {
                        ["NamedShiningAbodeCommandDisplaySave_IsDiscoverableLoadableValidAndRepeatable"] =
                            ["PreMergeSentinel"],
                        ["LoadedShiningAbodeCommandDisplaySave_RendersAvailableCommandInBrowserAndConsole"] =
                            ["FullValidation"],
                        ["LoadedShiningAbodeCommandDisplaySave_RendersRepresentativeDetailTargets"] =
                            ["FullValidation"]
                    }
            };

        foreach (var (fileName, methodManifest) in commandDisplayCategories)
        {
            Assert.Empty(CategoryTraits(fileName));
            foreach (var (methodName, categories) in methodManifest)
                Assert.Equal(categories, CategoryTraits(fileName, methodName));
        }
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

    [Fact]
    public void GameEngineLifeTransitionReplacementTest_UsesCheckpointInsteadOfPolling()
    {
        var sourcePath = SourcePath(
            IntegrationTestsDirectory,
            "GameEngineTurnLifecycleTests.cs");
        var root = CSharpSyntaxTree.ParseText(File.ReadAllText(sourcePath)).GetRoot();
        var method = Assert.Single(
            root.DescendantNodes()
                .OfType<MethodDeclarationSyntax>(),
            candidate =>
                candidate.Identifier.ValueText ==
                "CheckLifeTransitions_LoadAfterRawAcceptedValidation_AbortsWithoutMutatingReplacement");
        var methodBody = Assert.IsType<BlockSyntax>(method.Body);
        var methodBodySource = methodBody.ToFullString();

        Assert.DoesNotContain(
            "while (DateTime.UtcNow < deadline)",
            methodBodySource,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "await Task.Delay(50);",
            methodBodySource,
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

    private static string[] CategoryTraits(
        string fileName,
        string? methodName = null)
    {
        var source = File.ReadAllText(SourcePath(IntegrationTestsDirectory, fileName));
        var root = CSharpSyntaxTree.ParseText(source).GetCompilationUnitRoot();
        MemberDeclarationSyntax node = methodName is null
            ? Assert.Single(
                root.DescendantNodes().OfType<ClassDeclarationSyntax>(),
                declaration => declaration.Identifier.ValueText ==
                    Path.GetFileNameWithoutExtension(fileName))
            : Assert.Single(
                root.DescendantNodes().OfType<MethodDeclarationSyntax>(),
                method => method.Identifier.ValueText == methodName);

        return node.AttributeLists
            .SelectMany(list => list.Attributes)
            .Where(attribute =>
                attribute.Name.ToString() is "Trait" or "TraitAttribute")
            .Select(attribute => attribute.ArgumentList?.Arguments
                .Select(argument => argument.Expression)
                .OfType<LiteralExpressionSyntax>()
                .Select(literal => literal.Token.ValueText)
                .ToArray() ?? [])
            .Where(arguments =>
                arguments.Length == 2 &&
                arguments[0] == "Category")
            .Select(arguments => arguments[1])
            .Order(StringComparer.Ordinal)
            .ToArray();
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
            var sourceRoot = SourcePath(directory);
            foreach (var path in Directory.EnumerateFiles(
                         sourceRoot,
                         "*.cs",
                         SearchOption.AllDirectories)
                     .Where(path => !IsGeneratedBuildSource(sourceRoot, path)))
            {
                yield return (Path.GetFullPath(path), File.ReadAllText(path));
            }
        }
    }

    private static bool IsGeneratedBuildSource(string sourceRoot, string path)
    {
        var segments = Path.GetRelativePath(sourceRoot, path)
            .Split(
                [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                StringSplitOptions.RemoveEmptyEntries);

        return segments.Any(segment =>
            string.Equals(segment, "bin", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, "obj", StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<string> ParameterlessValidationCallLocations(
        string path,
        string source)
    {
        var methodName = "ValidateGameState" + "Async";
        var normalizedRelativePath = Path
            .GetRelativePath(TestRepoPaths.RepoRoot, path)
            .Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/');
        var root = CSharpSyntaxTree.ParseText(source).GetRoot();

        return root
            .DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(invocation =>
                invocation.ArgumentList.Arguments.Count == 0 &&
                string.Equals(
                    InvokedMemberName(invocation),
                    methodName,
                    StringComparison.Ordinal))
            .Select(invocation =>
                $"{normalizedRelativePath}:{LineNumber(invocation)}")
            .ToArray();
    }

    private static IEnumerable<string> ReviewedBroadValidationCallLocations()
    {
        return ReviewedBroadValidationCallManifest
            .OrderBy(mapping => mapping.Key, StringComparer.Ordinal)
            .SelectMany(mapping =>
                Enumerable.Range(1, mapping.Value)
                    .Select(line => $"{mapping.Key}:{line}"));
    }

    private static string[] BroadValidationCallManifestViolations(
        IEnumerable<string> callSites)
    {
        var callSiteArray = callSites.ToArray();
        var observedCounts = callSiteArray
            .Select(callSite =>
            {
                var lineSeparator = callSite.LastIndexOf(':');
                return lineSeparator >= 0
                    ? callSite[..lineSeparator]
                    : callSite;
            })
            .GroupBy(path => path, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        var paths = ReviewedBroadValidationCallManifest.Keys
            .Concat(observedCounts.Keys)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var countDrift = paths
            .Select(path => (
                Path: path,
                Expected: ReviewedBroadValidationCallManifest.GetValueOrDefault(path),
                Found: observedCounts.GetValueOrDefault(path)))
            .Where(count => count.Expected != count.Found)
            .Select(count =>
                $"{count.Path}: expected {count.Expected}, found {count.Found}")
            .ToArray();
        var violations = new List<string>();

        if (ReviewedBroadValidationCallManifest.Count != 5 ||
            ReviewedBroadValidationCallManifest.Values.Sum() !=
            ReviewedBroadValidationCallCount)
        {
            violations.Add(
                "Reviewed broad-validation manifest must contain exactly " +
                $"5 files and {ReviewedBroadValidationCallCount} call sites.");
        }

        if (callSiteArray.Length >= BroadValidationSentinelBudget)
        {
            violations.Add(
                $"observed {callSiteArray.Length} reaches sentinel budget " +
                $"{BroadValidationSentinelBudget}");
        }

        if (countDrift.Length > 0)
            violations.Add(string.Join(Environment.NewLine, countDrift));

        return violations.ToArray();
    }

    private static string BroadValidationCallManifestFailure(
        IReadOnlyCollection<string> callSites,
        IReadOnlyCollection<string> violations)
    {
        return string.Join(
            Environment.NewLine,
            $"Broad-validation sentinel budget is {BroadValidationSentinelBudget}",
            $"Expected reviewed call sites: {ReviewedBroadValidationCallCount}",
            "Replace repeated calls with IntegrationValidationProfiles or a narrower state-file selection.",
            "Manifest violations:",
            string.Join(Environment.NewLine, violations),
            "Observed zero-argument call sites:",
            string.Join(Environment.NewLine, callSites.Order(StringComparer.Ordinal)));
    }

    private static string SourcePath(params string[] relativeParts) =>
        Path.GetFullPath(Path.Combine(
            new[] { TestRepoPaths.RepoRoot }.Concat(relativeParts).ToArray()));

    private static int LineNumber(string source, int characterIndex)
    {
        return source.AsSpan(0, characterIndex).Count('\n') + 1;
    }

    private static int LineNumber(InvocationExpressionSyntax syntax)
    {
        return syntax.GetLocation()
            .GetLineSpan()
            .StartLinePosition
            .Line + 1;
    }

    private static string[] ActorAndAfterlifeScopedProfileViolations(
        IEnumerable<(string FileName, string ProfileName, string Source)> sources)
    {
        var methodName = "ValidateGameState" + "Async";
        var profileType = "IntegrationValidation" + "Profiles";
        var violations = new List<string>();

        foreach (var (fileName, profileName, source) in sources)
        {
            var expectedArgument = $"{profileType}.{profileName}";
            var root = CSharpSyntaxTree.ParseText(source).GetRoot();
            var invocations = root
                .DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Where(invocation => string.Equals(
                    InvokedMemberName(invocation),
                    methodName,
                    StringComparison.Ordinal))
                .ToArray();

            if (invocations.Length == 0)
            {
                violations.Add(
                    $"{fileName}: contains no member-call to {methodName}");
                continue;
            }

            foreach (var invocation in invocations)
            {
                var arguments = invocation.ArgumentList.Arguments;

                if (arguments.Count == 1 &&
                    IsExpectedIntegrationProfile(
                        arguments[0].Expression,
                        profileType,
                        profileName))
                {
                    continue;
                }

                var displayedArgument = arguments.Count switch
                {
                    0 => "<empty>",
                    1 => arguments[0].Expression.ToString(),
                    _ => $"<{arguments.Count} arguments: {arguments}>"
                };
                var line = invocation.GetLocation()
                    .GetLineSpan()
                    .StartLinePosition
                    .Line + 1;
                violations.Add(
                    $"{fileName}:{line}: expected exactly one structural argument " +
                    $"{expectedArgument}; found {displayedArgument}");
            }
        }

        return violations.ToArray();
    }

    private static string? InvokedMemberName(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression switch
        {
            MemberAccessExpressionSyntax memberAccess =>
                memberAccess.Name.Identifier.ValueText,
            MemberBindingExpressionSyntax memberBinding =>
                memberBinding.Name.Identifier.ValueText,
            _ => null
        };
    }

    private static bool IsExpectedIntegrationProfile(
        ExpressionSyntax argument,
        string profileType,
        string profileName)
    {
        return argument is MemberAccessExpressionSyntax
        {
            Expression: IdentifierNameSyntax profileTypeSyntax,
            Name: IdentifierNameSyntax profileNameSyntax
        } &&
            string.Equals(
                profileTypeSyntax.Identifier.ValueText,
                profileType,
                StringComparison.Ordinal) &&
            string.Equals(
                profileNameSyntax.Identifier.ValueText,
                profileName,
                StringComparison.Ordinal);
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

    private static async Task<RunnerSelfTestResult> RunCSharpRunnerTrxSelfTestAsync(
        string trxDirectory)
    {
        var startInfo = new ProcessStartInfo("pwsh")
        {
            WorkingDirectory = TestRepoPaths.RepoRoot,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var argument in new[]
        {
            "-NoProfile",
            "-File",
            Path.Combine(TestRepoPaths.RepoRoot, "scripts", "test-csharp.ps1"),
            "-SelfTest",
            "TrxSummary",
            "-SelfTestTrxDirectory",
            trxDirectory
        })
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo) ??
            throw new InvalidOperationException("PowerShell runner TRX self-test did not start.");
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            throw new TimeoutException("PowerShell runner TRX self-test exceeded 30 seconds.");
        }

        return new RunnerSelfTestResult(
            process.ExitCode,
            await standardOutput,
            await standardError);
    }

    private static string CreateTrxFixtureDirectory()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            "boe-runner-trx-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static string SyntheticTrx(
        string testId,
        string? storage,
        int resultCount)
    {
        var results = string.Concat(Enumerable.Range(1, resultCount).Select(index =>
            $"""<UnitTestResult testId="{testId}" executionId="execution-{index}" />"""));
        var storageAttribute = storage is null ? string.Empty : $" storage=\"{storage}\"";
        return $$"""
            <?xml version="1.0" encoding="utf-8"?>
            <TestRun>
              <Results>{{results}}</Results>
              <TestDefinitions>
                <UnitTest id="{{testId}}"{{storageAttribute}} />
              </TestDefinitions>
              <ResultSummary>
                <Counters total="{{resultCount}}" executed="{{resultCount}}" passed="{{resultCount}}" failed="0" />
              </ResultSummary>
            </TestRun>
            """;
    }

    private static string ResultDirectoryFrom(string standardOutput)
    {
        var match = Regex.Match(
            standardOutput,
            @"(?m)^  Self-test results: (?<path>.+?)\r?$");
        Assert.True(match.Success, standardOutput);
        return match.Groups["path"].Value;
    }

    private sealed record RunnerSelfTestResult(
        int ExitCode,
        string StandardOutput,
        string StandardError);
}
