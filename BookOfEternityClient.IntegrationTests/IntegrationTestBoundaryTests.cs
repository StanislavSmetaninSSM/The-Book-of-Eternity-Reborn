using BookOfEternityClient.Services;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class IntegrationTestBoundaryTests
{
    private const int BroadValidationSentinelBudget = 8;
    private const int ReviewedBroadValidationCallCount = 8;
    private const int GuardianFullValidationSentinelBudget = 8;
    private const string FastTestsDirectory = "BookOfEternityClient.Tests";
    private const string IntegrationTestsDirectory = "BookOfEternityClient.IntegrationTests";
    private const string TestSupportDirectory = "BookOfEternityClient.TestSupport";
    private const string FullValidationTrait = "[Trait(\"Category\", \"FullValidation\")]";

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
            ["EffectSkillScopeLifecycleTests.cs"] = "EffectSkillScopeCatalog",
            ["MathAssistantContractValidationTests.cs"] = "ReadableDocument",
            ["MechanicalBonusAuthorityValidationTests.cs"] = "MechanicalBonus",
            ["MortalBootstrapValidationTests.cs"] = "MortalBootstrap",
            ["MortalCommandDisplaySaveTests.cs"] = "CommandDisplaySave",
            ["MortalWoundTreatmentResourcePublicationLifecycleTests.Severity.cs"] =
                "MortalWoundTreatmentLifecycle",
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
            [$"{TestSupportDirectory}/ValidatorFixtureHarness.cs"] = 1,
            [$"{IntegrationTestsDirectory}/BookOfEternityClientGameSessionIntegrityTests.cs"] = 1,
            [$"{IntegrationTestsDirectory}/ExampleDocumentationValidationTests.cs"] = 1,
            [$"{IntegrationTestsDirectory}/FileSystemExampleFixtureIntegrityTests.cs"] = 2,
            [$"{IntegrationTestsDirectory}/FullValidationEquivalenceTests.cs"] = 2,
            [$"{IntegrationTestsDirectory}/MortalWoundTreatmentResourcePublicationLifecycleTests.cs"] = 1
        };

    private static readonly string[] ReviewedPublishedTreatmentFullStateCallers =
    [
        "GuaranteedItemConsumption_ExactPublishedNpcInventoryPassesFullStateAndRetry",
        "GuaranteedItemConsumption_FullStateInventoryIssueFilteringRequiresExactlyOneProvenMatch",
        "GuaranteedItemConsumption_FullStateInventoryIssueRequiresExactOpenPublicationProof",
        "GuaranteedItemConsumption_SameTurnItemNormalizationSurvivesPublication"
    ];

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

    private static readonly string[] ScopedValidationServiceSources =
    [
        Path.Combine("Services", "ValidationService.cs")
    ];

    /// <summary>
    /// Exact reviewed sources admitting scoped calls inside the ValidationService class.
    /// </summary>
    private static readonly string[] ScopedValidationCallerSources =
    [
        Path.Combine("Services", "ValidationService.cs"),
        Path.Combine("Services", "Validation", "ValidationService.EffectMaterialization.cs")
    ];

    /// <summary>
    /// Verifies that lifecycle tests use their own nonparallel xUnit collection.
    /// </summary>
    [Fact]
    public void GameEngineLifecycleTests_RemainSerializedWithinTheirOwnCollection()
    {
        var lifecycleSourcePath = SourcePath(
            IntegrationTestsDirectory,
            "GameEngineTurnLifecycleTests.cs");
        var lifecycleRoot = CSharpSyntaxTree
            .ParseText(File.ReadAllText(lifecycleSourcePath))
            .GetRoot();

        var collectionDefinition = Assert.Single(
            lifecycleRoot.DescendantNodes().OfType<ClassDeclarationSyntax>(),
            declaration =>
                declaration.Identifier.ValueText == "GameEngineTurnLifecycleCollection");
        var collectionDefinitionAttribute = Assert.Single(
            collectionDefinition.AttributeLists
                .SelectMany(attributeList => attributeList.Attributes),
            attribute => attribute.Name.ToString() == "CollectionDefinition");
        Assert.NotNull(collectionDefinitionAttribute.ArgumentList);
        Assert.Contains(
            collectionDefinitionAttribute.ArgumentList!.Arguments,
            argument =>
                argument.Expression.ToString() == "CollectionName");
        Assert.Contains(
            collectionDefinitionAttribute.ArgumentList.Arguments,
            argument =>
                argument.NameEquals?.Name.Identifier.ValueText == "DisableParallelization" &&
                argument.Expression.IsKind(SyntaxKind.TrueLiteralExpression));

        var lifecycleTests = Assert.Single(
            lifecycleRoot.DescendantNodes().OfType<ClassDeclarationSyntax>(),
            declaration => declaration.Identifier.ValueText == "GameEngineTurnLifecycleTests");
        var collectionAttribute = Assert.Single(
            lifecycleTests.AttributeLists.SelectMany(attributeList => attributeList.Attributes),
            attribute => attribute.Name.ToString() == "Collection");
        Assert.NotNull(collectionAttribute.ArgumentList);
        Assert.Contains(
            collectionAttribute.ArgumentList!.Arguments,
            argument =>
                argument.Expression.ToString() ==
                "GameEngineTurnLifecycleCollection.CollectionName");

    }

    [Fact]
    public void BrowserPresentationAudit_UsesSharedLoadedTemplatesWithIsolatedCases()
    {
        var auditSource = File.ReadAllText(Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient.IntegrationTests",
            "BrowserCommandPresentationAuditTests.cs"));
        var paritySource = File.ReadAllText(Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient.IntegrationTests",
            "BrowserCommandPresentationAuditTests.Parity.cs"));
        var fixtureSource = File.ReadAllText(Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient.IntegrationTests",
            "BrowserCommandPresentationAuditFixture.cs"));

        Assert.Contains(
            "IClassFixture<BrowserCommandPresentationAuditFixture>",
            auditSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "BrowserCommandPresentationAuditTests(BrowserCommandPresentationAuditFixture fixture)",
            auditSource,
            StringComparison.Ordinal);
        Assert.Contains("_fixture.ExecuteBrowserCommandAsync", auditSource, StringComparison.Ordinal);
        Assert.DoesNotContain("new SaveLoadService", auditSource, StringComparison.Ordinal);
        Assert.DoesNotContain("File.Copy(sourceArchive", auditSource, StringComparison.Ordinal);
        Assert.Contains("_fixture.ExecuteConsoleCommandAsync", paritySource, StringComparison.Ordinal);
        Assert.DoesNotContain("new SaveLoadService", paritySource, StringComparison.Ordinal);
        Assert.DoesNotContain("File.Copy(sourceArchive", paritySource, StringComparison.Ordinal);
        Assert.Single(Regex.Matches(fixtureSource, "LoadGameAsync\\(").Cast<Match>());

        var requiredFixtureTokens = new[]
        {
            "Lazy<Task<PreparedSaveContext>>",
            "LoadGameAsync(savePath)",
            "CaptureFileHashesAsync",
            "VerifyPreparedRootsUnchangedAsync",
            "CreateStateManagerAsync",
            "CopyDirectory(prepared.RootPath, rootPath)",
            "DeleteOwnedCaseRoot"
        };
        Assert.All(
            requiredFixtureTokens,
            token => Assert.Contains(token, fixtureSource, StringComparison.Ordinal));
    }

    [Fact]
    public void ExplorerWebCommandAudit_ReusesPreparedSeedProfilesWithIsolatedCases()
    {
        var auditSource = File.ReadAllText(Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient.IntegrationTests",
            "ExplorerWebCommandServiceTests.cs"));
        var fixturePath = Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient.IntegrationTests",
            "ExplorerWebCommandSeedTemplateFixture.cs");

        Assert.True(
            File.Exists(fixturePath),
            "The Explorer web command audit must own a reusable prepared-seed fixture.");
        var fixtureSource = File.ReadAllText(fixturePath);

        Assert.Contains(
            "IClassFixture<ExplorerWebCommandSeedTemplateFixture>",
            auditSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "ExplorerWebCommandServiceTests(ExplorerWebCommandSeedTemplateFixture seedFixture)",
            auditSource,
            StringComparison.Ordinal);
        Assert.Contains("seedFixture.CreateIsolatedCaseRoot()", auditSource, StringComparison.Ordinal);
        Assert.DoesNotContain("_fs.EnsureDirectoryStructure()", auditSource, StringComparison.Ordinal);
        Assert.Contains("PreparePlayerDefaultCommandAuditFilesAsync", auditSource, StringComparison.Ordinal);
        Assert.Contains("PrepareRepresentativeMigratedCommandFilesAsync", auditSource, StringComparison.Ordinal);
        Assert.Contains("PrepareMortalReadOnlySummaryFilesAsync", auditSource, StringComparison.Ordinal);
        Assert.Contains("PrepareLifecycleAndLocalTurnFilesAsync", auditSource, StringComparison.Ordinal);
        var repeatedSeedPreparers = new[]
        {
            "PrepareMigratedMortalFilesAsync",
            "PrepareRichMortalReferenceDetailFilesAsync",
            "PrepareIssue1124AfterlifeFilesAsync",
            "PrepareMigratedAfterlifeFilesAsync",
            "PrepareRichAfterlifeRelicArchiveFilesAsync",
            "PrepareMigratedUniversalFilesAsync",
            "PrepareMigratedShiningFilesAsync",
            "PrepareMigratedChaosSeaFilesAsync",
            "PrepareRichChaosSeaGuardianFilesAsync",
            "PrepareMortalActionPromptFilesAsync"
        };
        Assert.All(
            repeatedSeedPreparers,
            preparer => Assert.Contains(preparer, auditSource, StringComparison.Ordinal));
        Assert.True(
            Regex.Matches(auditSource, "PrepareRichAfterlifeRelicArchiveFilesAsync\\(").Count >= 4,
            "All repeated afterlife archive audit theories must reuse their prepared seed profile.");
        Assert.True(
            Regex.Matches(auditSource, "PrepareRichChaosSeaGuardianFilesAsync\\(").Count >= 3,
            "Both repeated Chaos Sea guardian audit theories must reuse their prepared seed profile.");

        var requiredFixtureTokens = new[]
        {
            "Lazy<Task<PreparedSeedProfile>>",
            "SeedFactoryInvocationCounts",
            "PrepareEmptySkeletonAsync",
            "CreateIsolatedCaseRoot",
            "CopyDirectory(emptySkeleton.RootPath, rootPath)",
            "CopyDirectory(prepared.RootPath, destinationRootPath)",
            "CaptureFileHashesAsync",
            "VerifyPreparedRootsUnchangedAsync",
            "DeleteOwnedFixtureRoot"
        };
        Assert.All(
            requiredFixtureTokens,
            token => Assert.Contains(token, fixtureSource, StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExplorerWebCommandSeedTemplateFixture_CachesSeedsAndIsolatesCaseRoots()
    {
        var fixture = new ExplorerWebCommandSeedTemplateFixture();
        try
        {
            var firstRoot = fixture.CreateIsolatedCaseRoot();
            var secondRoot = fixture.CreateIsolatedCaseRoot();
            var relativeMarker = Path.Combine("game_session", "game_state", "meta", "seed-probe.txt");
            var firstMarker = Path.Combine(firstRoot, relativeMarker);
            var secondMarker = Path.Combine(secondRoot, relativeMarker);

            Assert.NotEqual(firstRoot, secondRoot);
            Assert.True(Directory.Exists(Path.Combine(firstRoot, "game_session", "game_state", "control")));
            Assert.True(Directory.Exists(Path.Combine(secondRoot, "game_session", "game_state", "control")));

            await fixture.PrepareSeededRootAsync(
                "probe",
                firstRoot,
                async () => await File.WriteAllTextAsync(firstMarker, "prepared"));
            await File.WriteAllTextAsync(firstMarker, "mutated-case");
            await fixture.PrepareSeededRootAsync(
                "probe",
                secondRoot,
                () => Task.FromException(new InvalidOperationException("seed factory ran twice")));

            Assert.Equal("mutated-case", await File.ReadAllTextAsync(firstMarker));
            Assert.Equal("prepared", await File.ReadAllTextAsync(secondMarker));
            Assert.Equal(1, fixture.SeedFactoryInvocationCounts["probe"]);
        }
        finally
        {
            await fixture.DisposeAsync();
        }
    }

    /// <summary>
    /// Verifies that multiple results for one theory method in one TRX are valid.
    /// </summary>
    /// <returns>
    /// A task that completes after the reader accepts repeated theory rows.
    /// </returns>
    [Fact]
    public async Task CSharpCategoryRunner_RepeatedTheoryRowsWithinOneTrxAreNotDuplicates()
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

    /// <summary>
    /// Verifies that repeated test identities across descriptors fail closed.
    /// </summary>
    /// <returns>
    /// A task that completes after the reader rejects a cross-descriptor duplicate ID.
    /// </returns>
    [Fact]
    public async Task CSharpCategoryRunner_SameTestIdAcrossDescriptorTrxFilesIsDuplicate()
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

    /// <summary>
    /// Verifies that the same test ID in distinct assemblies remains distinguishable.
    /// </summary>
    /// <returns>
    /// A task that completes after the reader accepts assembly-scoped IDs.
    /// </returns>
    [Fact]
    public async Task CSharpCategoryRunner_SameTestIdInDifferentAssembliesIsNotDuplicate()
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

    /// <summary>
    /// Verifies that a TRX result without assembly storage cannot appear complete.
    /// </summary>
    /// <returns>
    /// A task that completes after the reader rejects a result without assembly storage.
    /// </returns>
    [Fact]
    public async Task CSharpCategoryRunner_MissingStorageMappingFailsClosed()
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

    /// <summary>
    /// Verifies that a malformed TRX cannot be reported as a successful selected result.
    /// </summary>
    /// <returns>
    /// A task completing after the runner reports the parsing failure.
    /// </returns>
    [Fact]
    public async Task CSharpCategoryRunner_MalformedTrxFailsClosed()
    {
        var fixtureDirectory = CreateTrxFixtureDirectory();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(fixtureDirectory, "malformed.trx"),
                "<TestRun><Results>");

            var probe = await RunCSharpRunnerTrxSelfTestAsync(fixtureDirectory);

            Assert.NotEqual(0, probe.ExitCode);
            Assert.Contains("TRX parsing failed", probe.StandardOutput + probe.StandardError,
                StringComparison.Ordinal);
            using var summary = JsonDocument.Parse(await File.ReadAllTextAsync(
                Path.Combine(ResultDirectoryFrom(probe.StandardOutput), "self-test-summary.json")));
            Assert.Equal(1, summary.RootElement.GetProperty("ExitCode").GetInt32());
            Assert.True(summary.RootElement.GetProperty("OwnedTreeCleanupSucceeded").GetBoolean());
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
            "EffectSkillScopeCatalog",
            "FactionState",
            "GuardianArchiveTrade",
            "GuardianPolicy",
            "MechanicalBonus",
            "MortalBootstrap",
            "MortalWoundTreatmentLifecycle",
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

        Assert.Equal(
            GameStateValidationPhase.PlayerStateFiles |
            GameStateValidationPhase.SkillContractConsistency,
            IntegrationValidationProfiles.EffectSkillScopeCatalog.Phases);
        Assert.Equal(
            GameStateValidationPhase.RequiredFields |
            GameStateValidationPhase.CrossReferences |
            GameStateValidationPhase.PlayerStateFiles |
            GameStateValidationPhase.NpcStateFiles |
            GameStateValidationPhase.SkillContractConsistency |
            GameStateValidationPhase.WorldQuestCombatFactionStateFiles |
            GameStateValidationPhase.MetaMiscStateFiles |
            GameStateValidationPhase.AcceptedTurnEffectMaterializationCompleteness |
            GameStateValidationPhase.AcceptedTurnWoundMaterializationCompleteness |
            GameStateValidationPhase.ClientOwnedControlFiles,
            IntegrationValidationProfiles.MortalWoundTreatmentLifecycle.Phases);
    }

    [Fact]
    public void FactionValidationProfiles_IncludeMaterializationContinuity()
    {
        Assert.True(
            IntegrationValidationProfiles.FactionMaterialization.HasFlag(
                GameStateValidationPhase.AcceptedTurnFactionMaterializationCompleteness));
        Assert.True(
            IntegrationValidationProfiles.FactionState.Phases.HasFlag(
                GameStateValidationPhase.AcceptedTurnFactionMaterializationCompleteness));
        Assert.True(
            IntegrationValidationProfiles.ShiningState.Phases.HasFlag(
                GameStateValidationPhase.AcceptedTurnFactionMaterializationCompleteness));
    }

    [Fact]
    public void GuardianArchiveTradeProfile_SelectsOnlyAssertionOwningPhasesAndFiles()
    {
        var profile = IntegrationValidationProfiles.GuardianArchiveTrade;

        Assert.Equal(
            GameStateValidationPhase.CrossReferences |
            GameStateValidationPhase.WorldQuestCombatFactionStateFiles |
            GameStateValidationPhase.MetaMiscStateFiles |
            GameStateValidationPhase.ClientOwnedControlFiles,
            profile.Phases);

        Assert.All(
        [
            "game_state/inventory/items.json",
            "game_state/meta/guardians.json",
            "game_state/meta/soul_state.json",
            "game_state/npcs/npc_core.json",
            "game_state/quests/soul_quests.json",
            "game_state/world/world_events.json",
            "lore/codex_entries.json",
            AfterlifeArchiveActionState.ConsultationRequestPath,
            AfterlifeArchiveActionState.ProjectFuelRequestPath,
            GuardianAbodeResidentRequestState.PendingInteractionsRequestPath,
            GuardianAbodeResidentRequestState.PendingManifestationRequestPath,
            GuardianAbodeResidentRequestState.PendingResidentsRequestPath,
            GuardianAbodeResidentRequestState.PendingTransfersRequestPath,
            GuardianAbodeResidentState.StatePath,
            GuardianProjectState.TrackerPath,
            GuardianThoughtJournalState.StatePath,
            GuardianTradeRequestState.PendingRequestPath,
            NpcInteractionJournalState.StatePath,
            RivalSoulArcService.StatePath
        ],
        path => Assert.True(
            profile.IncludesStateFile(path),
            $"Expected GuardianArchiveTrade to include '{path}'."));

        Assert.All(
        [
            AfterlifeActiveThreatState.StatePath,
            AfterlifeEntityProfileState.StatePath,
            MathAssistantContractState.StatePath,
            SarefMainStoryState.StatePath,
            ShiningAbodeState.StatePath,
            "game_state/misc/vehicles.json"
        ],
        path => Assert.False(
            profile.IncludesStateFile(path),
            $"Expected GuardianArchiveTrade to exclude '{path}'."));
    }

    [Fact]
    public void ValidatorFixtureStateOnlySelection_IncludesMappedAndCanonicalSnapshotFiles()
    {
        var definition = new ValidatorFixtureDefinition
        {
            Runner = FixtureRunnerKind.StateOnly,
            Shared =
            [
                new FixtureFileMapping
                {
                    Target =
                        "game_state/control/pending_turn_snapshot/game_state/meta/guardians.json"
                }
            ],
            Broken =
            [
                new FixtureFileMapping
                {
                    Target = "game_state/meta/soul_state.json"
                }
            ],
            Fixed =
            [
                new FixtureFileMapping
                {
                    Target = "lore/codex_entries.json"
                }
            ]
        };

        var selection = ValidatorFixtureHarness.BuildStateOnlySelection(definition);

        Assert.Equal(GameStateValidationPhase.All, selection.Phases);
        Assert.All(
        [
            "game_state/control/pending_turn_snapshot/game_state/meta/guardians.json",
            "game_state/meta/guardians.json",
            "game_state/meta/soul_state.json",
            "lore/codex_entries.json"
        ],
        path => Assert.True(
            selection.IncludesStateFile(path),
            $"Expected StateOnly fixture selection to include '{path}'."));
        Assert.False(selection.IncludesStateFile("game_state/world/world_map.json"));
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
    public void BroadValidationCalls_MatchReviewedEightCallManifest()
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
    public void BroadValidationCalls_MatchReviewedEightCallManifest_RejectsSameTotalPerFileDrift()
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
                    $"{TestSupportDirectory}/ValidatorFixtureHarness.cs: expected 1, found 0",
                    StringComparison.Ordinal) &&
                violation.Contains(
                    $"{IntegrationTestsDirectory}/BookOfEternityClientGameSessionIntegrityTests.cs: expected 1, found 2",
                    StringComparison.Ordinal));
    }

    [Fact]
    public void BroadValidationCalls_MatchReviewedEightCallManifest_RejectsNinthUnreviewedCall()
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
                    "observed 9 exceeds sentinel budget 8",
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
            "Expected reviewed call sites: 8",
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

    /// <summary>
    /// Keeps the published treatment full-state fixture limited to its reviewed callers.
    /// </summary>
    [Fact]
    public void PublishedTreatmentFullStateProbe_HasExactReviewedCallers()
    {
        const string sourceName = "MortalWoundTreatmentResourcePublicationLifecycleTests.cs";
        const string helperName = "CreatePublishedTreatmentFullStateProbeAsync";
        var root = CSharpSyntaxTree.ParseText(File.ReadAllText(
            SourcePath(IntegrationTestsDirectory, sourceName))).GetCompilationUnitRoot();
        var actualCallers = root.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(invocation => string.Equals(
                invocation.Expression is IdentifierNameSyntax identifier
                    ? identifier.Identifier.ValueText
                    : InvokedMemberName(invocation),
                helperName,
                StringComparison.Ordinal))
            .Select(invocation => invocation.Ancestors()
                .OfType<MethodDeclarationSyntax>()
                .First()
                .Identifier.ValueText)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            ReviewedPublishedTreatmentFullStateCallers.Order(StringComparer.Ordinal),
            actualCallers);
    }

    [Fact]
    public void CommandDisplaySaveSources_UseLazyPreparedTemplatesAndPreserveTheoryManifests()
    {
        const string preparedFixtureFileName = "PreparedCommandDisplaySaveFixture.cs";
        var preparedFixturePath = SourcePath(IntegrationTestsDirectory, preparedFixtureFileName);
        Assert.True(
            File.Exists(preparedFixturePath),
            $"Prepared command-display template contract is missing: {preparedFixtureFileName}");

        var preparedFixtureSource = File.ReadAllText(preparedFixturePath);
        var preparedFixtureRoot = CSharpSyntaxTree.ParseText(preparedFixtureSource).GetCompilationUnitRoot();
        Assert.Empty(PreparedCommandDisplayContractViolations(
            preparedFixtureSource,
            CommandDisplayTestSources()));
        var preparedFixtureClass = Assert.Single(
            preparedFixtureRoot.DescendantNodes().OfType<ClassDeclarationSyntax>(),
            static declaration => declaration.Identifier.ValueText == "PreparedCommandDisplaySaveFixture");
        Assert.DoesNotContain(
            preparedFixtureClass.Members.OfType<FieldDeclarationSyntax>(),
            static field => !field.Modifiers.Any(SyntaxKind.ReadOnlyKeyword));

        var lazyTemplateField = Assert.Single(
            preparedFixtureClass.Members.OfType<FieldDeclarationSyntax>(),
            static field => field.Declaration.Type.ToString() == "Lazy<Task<PreparedTemplate>>");
        Assert.Equal("_templateRoot", Assert.Single(lazyTemplateField.Declaration.Variables).Identifier.ValueText);
        Assert.Contains("PrepareTemplateAsync", preparedFixtureClass.ToString(), StringComparison.Ordinal);
        Assert.Contains("ExecutionAndPublication", preparedFixtureClass.ToString(), StringComparison.Ordinal);

        var prepareTemplateMethod = Assert.Single(
            preparedFixtureClass.Members.OfType<MethodDeclarationSyntax>(),
            static method => method.Identifier.ValueText == "PrepareTemplateAsync");
        var prepareTemplateBody = prepareTemplateMethod.ToString();
        Assert.Contains("CopyCleanCheckoutDependencies", prepareTemplateBody, StringComparison.Ordinal);
        Assert.Contains("new SaveLoadService", prepareTemplateBody, StringComparison.Ordinal);
        Assert.Contains(".LoadGameAsync(", prepareTemplateBody, StringComparison.Ordinal);
        Assert.Contains(".LoadSettingsAsync(", prepareTemplateBody, StringComparison.Ordinal);
        Assert.Contains(".RefreshGameStateAsync(", prepareTemplateBody, StringComparison.Ordinal);

        var cloneMethod = Assert.Single(
            preparedFixtureClass.Members.OfType<MethodDeclarationSyntax>(),
            static method => method.Identifier.ValueText == "ClonePreparedTemplateAsync");
        Assert.Contains("_templateRoot.Value", cloneMethod.ToString(), StringComparison.Ordinal);
        Assert.Contains("CopyDirectory", cloneMethod.ToString(), StringComparison.Ordinal);

        var disposeMethod = Assert.Single(
            preparedFixtureClass.Members.OfType<MethodDeclarationSyntax>(),
            static method => method.Identifier.ValueText == "DisposeAsync");
        Assert.Contains("_templateRoot.IsValueCreated", disposeMethod.ToString(), StringComparison.Ordinal);
        Assert.Contains("DeleteOwnedRoot", disposeMethod.ToString(), StringComparison.Ordinal);

        Assert.Equal(
            ["ChaosSeaPreparedCommandDisplaySaveFixture", "MortalPreparedCommandDisplaySaveFixture", "ShiningAbodePreparedCommandDisplaySaveFixture"],
            preparedFixtureRoot.DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .Where(static declaration =>
                    declaration.BaseList?.Types.Any(static type =>
                        type.Type.ToString() == "PreparedCommandDisplaySaveFixture") == true)
                .Select(static declaration => declaration.Identifier.ValueText)
                .OrderBy(static name => name, StringComparer.Ordinal)
                .ToArray());

        var sourceContracts =
            new Dictionary<string, (string Fixture, IReadOnlyDictionary<string, (string MemberData, string[] Categories)> Theories)>(
                StringComparer.Ordinal)
            {
                ["MortalCommandDisplaySaveTests.cs"] =
                    (
                        "MortalPreparedCommandDisplaySaveFixture",
                        new Dictionary<string, (string, string[])>(StringComparer.Ordinal)
                        {
                            ["LoadedMortalCommandDisplaySave_RendersCoveredCommandInBrowserAndConsole"] =
                                ("CoveredMortalCommandInvocations", ["FullValidation"]),
                            ["LoadedMortalCommandDisplaySave_WorldNewsLocalizesVisibilityEnums"] =
                                ("MortalWorldNewsFixtureInvocations", ["FullValidation"])
                        }),
                ["ChaosSeaCommandDisplaySaveTests.cs"] =
                    (
                        "ChaosSeaPreparedCommandDisplaySaveFixture",
                        new Dictionary<string, (string, string[])>(StringComparer.Ordinal)
                        {
                            ["LoadedChaosSeaCommandDisplaySave_RendersAvailableCommandInBrowserAndConsole"] =
                                ("CoveredChaosSeaCommandInvocations", ["FullValidation"]),
                            ["LoadedChaosSeaCommandDisplaySave_RendersRepresentativeDetailTargets"] =
                                ("ChaosSeaDetailInvocations", ["FullValidation"])
                        }),
                ["ShiningAbodeCommandDisplaySaveTests.cs"] =
                    (
                        "ShiningAbodePreparedCommandDisplaySaveFixture",
                        new Dictionary<string, (string, string[])>(StringComparer.Ordinal)
                        {
                            ["LoadedShiningAbodeCommandDisplaySave_RendersAvailableCommandInBrowserAndConsole"] =
                                ("CoveredShiningAbodeCommandInvocations", ["FullValidation"]),
                            ["LoadedShiningAbodeCommandDisplaySave_RendersRepresentativeDetailTargets"] =
                                ("ShiningAbodeDetailInvocations", ["FullValidation"])
                        })
            };

        foreach (var (fileName, contract) in sourceContracts)
        {
            var source = File.ReadAllText(SourcePath(IntegrationTestsDirectory, fileName));
            var root = CSharpSyntaxTree.ParseText(source).GetCompilationUnitRoot();
            var testClass = Assert.Single(
                root.DescendantNodes().OfType<ClassDeclarationSyntax>(),
                declaration => declaration.Identifier.ValueText == Path.GetFileNameWithoutExtension(fileName));

            Assert.Contains(
                testClass.BaseList?.Types ?? [],
                type => type.Type.ToString() == $"IClassFixture<{contract.Fixture}>");
            Assert.Contains(
                testClass.Members.OfType<ConstructorDeclarationSyntax>(),
                constructor => constructor.ParameterList.Parameters.Any(
                    parameter => parameter.Type?.ToString() == contract.Fixture));

            var theories = testClass.Members.OfType<MethodDeclarationSyntax>()
                .Where(static method => method.AttributeLists
                    .SelectMany(static list => list.Attributes)
                    .Any(static attribute => attribute.Name.ToString() == "Theory"))
                .ToArray();
            Assert.Equal(
                contract.Theories.Keys.OrderBy(static name => name, StringComparer.Ordinal),
                theories.Select(static method => method.Identifier.ValueText)
                    .OrderBy(static name => name, StringComparer.Ordinal));

            foreach (var theory in theories)
            {
                var expected = contract.Theories[theory.Identifier.ValueText];
                var memberData = Assert.Single(
                    theory.AttributeLists.SelectMany(static list => list.Attributes),
                    static attribute => attribute.Name.ToString() == "MemberData");
                var memberDataArgument = Assert.Single(memberData.ArgumentList!.Arguments);
                Assert.Equal($"nameof({expected.MemberData})", memberDataArgument.Expression.ToString());
            }

            var executionHelper = Assert.Single(
                testClass.Members.OfType<MethodDeclarationSyntax>(),
                static method => method.Identifier.ValueText == "ExecuteFromLoadedSaveAsync");
            var executionBody = executionHelper.ToString();
            Assert.Contains("CreateIsolatedRoot()", executionBody, StringComparison.Ordinal);
            Assert.Contains("_fixture.ClonePreparedTemplateAsync(loadRoot)", executionBody, StringComparison.Ordinal);
            Assert.Contains("new FileSystemManager", executionBody, StringComparison.Ordinal);
            Assert.Contains("new StateManager", executionBody, StringComparison.Ordinal);
            Assert.Contains("new ValidationService", executionBody, StringComparison.Ordinal);
            Assert.Contains("new LocalizationManager", executionBody, StringComparison.Ordinal);
            Assert.Contains("new ExplorerWebCommandService", executionBody, StringComparison.Ordinal);
            Assert.DoesNotContain("SaveLoadService", executionBody, StringComparison.Ordinal);
            Assert.DoesNotContain(".LoadGameAsync(", executionBody, StringComparison.Ordinal);
            Assert.DoesNotContain("CopyCleanCheckoutDependencies", executionBody, StringComparison.Ordinal);
            Assert.DoesNotContain("File.Copy(", executionBody, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void CommandDisplayPreparedTemplateContract_RejectsEagerConstructorAccess()
    {
        var fixtureSource = PreparedCommandDisplayFixtureSource().Replace(
            "            LazyThreadSafetyMode.ExecutionAndPublication);",
            "            LazyThreadSafetyMode.ExecutionAndPublication);" +
            Environment.NewLine +
            "        _ = _templateRoot.Value;",
            StringComparison.Ordinal);

        var violations = PreparedCommandDisplayContractViolations(
            fixtureSource,
            CommandDisplayTestSources());

        Assert.Contains(
            "Prepared fixture constructor must not access _templateRoot.Value.",
            violations);
    }

    [Fact]
    public void CommandDisplayPreparedTemplateContract_RejectsEagerInitializeAsyncAccess()
    {
        var fixtureSource = PreparedCommandDisplayFixtureSource().Replace(
            "public Task InitializeAsync() => Task.CompletedTask;",
            "public Task InitializeAsync() => _templateRoot.Value;",
            StringComparison.Ordinal);

        var violations = PreparedCommandDisplayContractViolations(
            fixtureSource,
            CommandDisplayTestSources());

        Assert.Contains(
            "Prepared fixture InitializeAsync must not access _templateRoot.Value.",
            violations);
    }

    [Fact]
    public void CommandDisplayPreparedTemplateContract_FalseNegative_RejectsTransitiveConstructorAccess()
    {
        var fixtureSource = PreparedCommandDisplayFixtureSource().Replace(
            "            LazyThreadSafetyMode.ExecutionAndPublication);",
            "            LazyThreadSafetyMode.ExecutionAndPublication);" +
            Environment.NewLine +
            "        _ = IsPreparedSourceLoadedAsync();",
            StringComparison.Ordinal);

        var violations = PreparedCommandDisplayContractViolations(
            fixtureSource,
            CommandDisplayTestSources());

        Assert.Contains(
            "Prepared fixture constructor must not access _templateRoot.Value.",
            violations);
    }

    [Fact]
    public void CommandDisplayPreparedTemplateContract_FalseNegative_RejectsTransitiveInitializeAsyncAccess()
    {
        var fixtureSource = PreparedCommandDisplayFixtureSource().Replace(
            "public Task InitializeAsync() => Task.CompletedTask;",
            "public Task InitializeAsync() => IsPreparedSourceLoadedAsync();",
            StringComparison.Ordinal);

        var violations = PreparedCommandDisplayContractViolations(
            fixtureSource,
            CommandDisplayTestSources());

        Assert.Contains(
            "Prepared fixture InitializeAsync must not access _templateRoot.Value.",
            violations);
    }

    [Fact]
    public void CommandDisplayPreparedTemplateContract_RejectsCloneIntoTemplateRoot()
    {
        var fixtureSource = PreparedCommandDisplayFixtureSource().Replace(
            "CopyDirectory(preparedTemplate.RootPath, caseRoot);",
            "CopyDirectory(preparedTemplate.RootPath, _templateRootPath);",
            StringComparison.Ordinal);

        var violations = PreparedCommandDisplayContractViolations(
            fixtureSource,
            CommandDisplayTestSources());

        Assert.Contains(
            "ClonePreparedTemplateAsync must copy into its case-root parameter.",
            violations);
    }

    [Fact]
    public void CommandDisplayPreparedTemplateContract_FalseNegative_RejectsAdditionalCloneCopy()
    {
        var fixtureSource = PreparedCommandDisplayFixtureSource().Replace(
            "CopyDirectory(preparedTemplate.RootPath, caseRoot);",
            "CopyDirectory(preparedTemplate.RootPath, caseRoot);" +
            Environment.NewLine +
            "        CopyDirectory(preparedTemplate.RootPath, _templateRootPath);",
            StringComparison.Ordinal);

        var violations = PreparedCommandDisplayContractViolations(
            fixtureSource,
            CommandDisplayTestSources());

        Assert.Contains(
            "ClonePreparedTemplateAsync must match the exact write-safe method shape.",
            violations);
    }

    [Fact]
    public void CommandDisplayPreparedTemplateContract_FalseNegative_RejectsAdditionalCloneObjectCreation()
    {
        var fixtureSource = PreparedCommandDisplayFixtureSource().Replace(
            "CopyDirectory(preparedTemplate.RootPath, caseRoot);",
            "using var stream = new FileStream(_templateRootPath, FileMode.Create);" +
            Environment.NewLine +
            "        CopyDirectory(preparedTemplate.RootPath, caseRoot);",
            StringComparison.Ordinal);

        var violations = PreparedCommandDisplayContractViolations(
            fixtureSource,
            CommandDisplayTestSources());

        Assert.Contains(
            "ClonePreparedTemplateAsync must match the exact write-safe method shape.",
            violations);
    }

    [Fact]
    public void CommandDisplayPreparedTemplateContract_FalseNegative_RejectsFailureGuardElseAssignment()
    {
        var fixtureSource = PreparedCommandDisplayFixtureSource().Replace(
            "            throw new InvalidOperationException($\"Could not prepare command-display save '{_saveFileName}'.\");",
            "            throw new InvalidOperationException($\"Could not prepare command-display save '{_saveFileName}'.\");" +
            Environment.NewLine +
            "        else" +
            Environment.NewLine +
            "            caseRoot = _templateRootPath;",
            StringComparison.Ordinal);

        var violations = PreparedCommandDisplayContractViolations(
            fixtureSource,
            CommandDisplayTestSources());

        Assert.Contains(
            "ClonePreparedTemplateAsync must match the exact write-safe method shape.",
            violations);
    }

    [Fact]
    public void CommandDisplayPreparedTemplateContract_RejectsSharedExecutionRoot()
    {
        var sources = CommandDisplayTestSources();
        sources["MortalCommandDisplaySaveTests.cs"] =
            sources["MortalCommandDisplaySaveTests.cs"].Replace(
                "var loadRoot = CreateIsolatedRoot();",
                "var loadRoot = _rootPath;",
                StringComparison.Ordinal);

        var violations = PreparedCommandDisplayContractViolations(
            PreparedCommandDisplayFixtureSource(),
            sources);

        Assert.Contains(
            "MortalCommandDisplaySaveTests.cs: exhaustive helper must obtain its case root from CreateIsolatedRoot.",
            violations);
    }

    [Fact]
    public void CommandDisplayPreparedTemplateContract_RejectsNonUniqueCreatedRoot()
    {
        var sources = CommandDisplayTestSources();
        sources["MortalCommandDisplaySaveTests.cs"] =
            sources["MortalCommandDisplaySaveTests.cs"].Replace(
                "var root = Path.Combine(_rootPath, Guid.NewGuid().ToString(\"N\"));",
                "var root = Path.Combine(_rootPath, \"shared\");",
                StringComparison.Ordinal);

        var violations = PreparedCommandDisplayContractViolations(
            PreparedCommandDisplayFixtureSource(),
            sources);

        Assert.Contains(
            "MortalCommandDisplaySaveTests.cs: CreateIsolatedRoot must include a per-call Guid.NewGuid().",
            violations);
    }

    [Fact]
    public void CommandDisplayPreparedTemplateContract_RequiresSourceAndPreparedLoadAssertions()
    {
        foreach (var source in CommandDisplayTestSources().Values)
        {
            var root = CSharpSyntaxTree.ParseText(source).GetCompilationUnitRoot();
            var executionHelper = Assert.Single(
                root.DescendantNodes().OfType<MethodDeclarationSyntax>(),
                static method => method.Identifier.ValueText == "ExecuteFromLoadedSaveAsync");
            var assertions = executionHelper.DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Where(static invocation => invocation.Expression.ToString() == "Assert.True")
                .Select(static invocation => invocation.ArgumentList.Arguments.First().Expression.ToString())
                .ToArray();

            Assert.Contains("File.Exists(sourceArchive)", assertions);
            Assert.Contains("await _fixture.IsPreparedSourceLoadedAsync()", assertions);
        }
    }

    [Fact]
    public void QteAndDarenSplitSources_PreserveReviewedExecutableInventories()
    {
        var contracts = new[]
        {
            (
                Directory: FastTestsDirectory,
                FileName: "QteDeterministicLogicTests.cs",
                ClassName: "QteDeterministicLogicTests",
                Manifest: FastBoundaryTestInventories.QteDeterministicLogic),
            (
                Directory: IntegrationTestsDirectory,
                FileName: "QteSceneServiceTests.cs",
                ClassName: "QteSceneServiceTests",
                Manifest: FastBoundaryTestInventories.QteSceneService),
            (
                Directory: FastTestsDirectory,
                FileName: "DarenQteDeterministicLogicTests.cs",
                ClassName: "DarenQteDeterministicLogicTests",
                Manifest: FastBoundaryTestInventories.DarenDeterministicLogic),
            (
                Directory: IntegrationTestsDirectory,
                FileName: "DarenQteShowcaseTests.cs",
                ClassName: "DarenQteShowcaseTests",
                Manifest: FastBoundaryTestInventories.DarenQteShowcase)
        };

        foreach (var contract in contracts)
        {
            var violations = ExactTestInventoryViolations(
                SourcePath(contract.Directory, contract.FileName),
                contract.ClassName,
                ManifestLines(contract.Manifest));

            Assert.True(
                violations.Length == 0,
                $"{contract.ClassName} executable inventory differs from the reviewed manifest:" +
                Environment.NewLine +
                string.Join(Environment.NewLine, violations));
        }
    }

    [Fact]
    public void ExactTestInventory_RejectsMissingChangedAndDuplicateRows()
    {
        const string source = """
            public sealed class SampleTests
            {
                [Fact]
                public void Kept() {}

                [Theory]
                [InlineData(1)]
                [InlineData(1)]
                public void Rows(int value) {}

                [Theory]
                [InlineData(2)]
                public void ChangedKind(int value) {}
            }
            """;
        string[] expected =
        [
            "Fact|Kept",
            "Theory|Rows|literal:1",
            "Theory|Rows|literal:2",
            "Fact|ChangedKind"
        ];

        var violations = ExactTestInventoryViolations(
            "Synthetic.cs",
            "SampleTests",
            expected,
            source);

        Assert.Contains("missing (1x): Fact|ChangedKind", violations, StringComparer.Ordinal);
        Assert.Contains("missing (1x): Theory|Rows|literal:2", violations, StringComparer.Ordinal);
        Assert.Contains("unexpected (1x): Theory|ChangedKind|literal:2", violations, StringComparer.Ordinal);
        Assert.Contains("unexpected (1x): Theory|Rows|literal:1", violations, StringComparer.Ordinal);
    }

    [Fact]
    public void DarenQteSources_AreSemanticallySplitAcrossTestAssemblies()
    {
        var fastPath = SourcePath(FastTestsDirectory, "DarenQteDeterministicLogicTests.cs");
        var integrationPath = SourcePath(IntegrationTestsDirectory, "DarenQteShowcaseTests.cs");

        Assert.True(File.Exists(fastPath), "The fixture-free Daren QTE source must exist in Fast.");
        Assert.True(File.Exists(integrationPath), "The file-backed Daren QTE source must remain in Integration.");

        var fastRoot = CSharpSyntaxTree.ParseText(File.ReadAllText(fastPath)).GetCompilationUnitRoot();
        var fastClass = Assert.Single(
            fastRoot.DescendantNodes().OfType<ClassDeclarationSyntax>(),
            declaration => declaration.Identifier.ValueText == "DarenQteDeterministicLogicTests");
        Assert.DoesNotContain(
            fastClass.BaseList?.Types ?? [],
            type => type.Type.ToString() == "IDisposable");
        Assert.Empty(fastClass.Members.OfType<ConstructorDeclarationSyntax>());
        Assert.DoesNotContain(
            fastClass.Members.OfType<FieldDeclarationSyntax>(),
            field =>
                !field.Modifiers.Any(SyntaxKind.StaticKeyword) &&
                !field.Modifiers.Any(SyntaxKind.ConstKeyword));

        var forbiddenFastTypes = new[]
        {
            "FileSystemManager",
            "StateManager",
            "QteWebInteractionService",
            "LocalUiSessionLockService"
        };
        var fastTypes = fastClass.DescendantNodes()
            .OfType<TypeSyntax>()
            .Select(type => type.ToString())
            .ToArray();
        Assert.All(
            forbiddenFastTypes,
            forbidden => Assert.DoesNotContain(forbidden, fastTypes, StringComparer.Ordinal));
        Assert.DoesNotContain(
            fastClass.DescendantNodes().OfType<ObjectCreationExpressionSyntax>(),
            creation => creation.Type.ToString() == "DarenQteRewardProfileService");
        Assert.DoesNotContain("Path.GetTempPath", fastClass.ToString(), StringComparison.Ordinal);

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

    /// <summary>
    /// Verifies that scoped validation declarations and calls remain inside their reviewed ValidationService sources.
    /// </summary>
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
            ScopedValidationCallerSources);

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

    /// <summary>
    /// Verifies that an unreviewed production source cannot declare or call the scoped validation API.
    /// </summary>
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
            ScopedValidationCallerSources);

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

    /// <summary>
    /// Verifies that the reviewed effect partial admits internal owner validation while rejecting a foreign class in that same source.
    /// </summary>
    [Fact]
    public void RuntimeValidationGuard_ReviewedPartialAllowsOnlyValidationServiceCalls()
    {
        var sources = new[]
        {
            (
                RelativePath: Path.Combine("Services", "Validation", "ValidationService.EffectMaterialization.cs"),
                Source:
                    """
                    namespace BookOfEternityClient.Services;
                    public partial class ValidationService
                    {
                        async Task ValidateOwnerAsync() => await ownerValidator.ValidateGameStateAsync(selection);
                    }
                    public class ForeignCaller
                    {
                        async Task ValidateAsync() => await validator.ValidateGameStateAsync(selection);
                    }
                    """)
        };

        var violations = ArgumentBearingValidationCallViolations(sources, ScopedValidationCallerSources);

        var violation = Assert.Single(violations);
        Assert.EndsWith(":8", violation, StringComparison.Ordinal);
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
                "CheckLifeTransitions_GenerationChangesAfterRawAcceptedValidation_AbortsWithoutMutatingReplacement");
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

    private static string[] ManifestLines(string manifest) =>
        manifest.Split(
            ["\r\n", "\n"],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string[] ExactTestInventoryViolations(
        string sourcePath,
        string className,
        IReadOnlyCollection<string> expected)
    {
        if (!File.Exists(sourcePath))
            return [$"missing source: {sourcePath}"];

        return ExactTestInventoryViolations(
            sourcePath,
            className,
            expected,
            File.ReadAllText(sourcePath));
    }

    private static string[] ExactTestInventoryViolations(
        string sourceName,
        string className,
        IReadOnlyCollection<string> expected,
        string source)
    {
        var root = CSharpSyntaxTree.ParseText(source).GetCompilationUnitRoot();
        var violations = root.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Select(diagnostic => $"{sourceName}: parse error {diagnostic}")
            .ToList();
        var classes = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .Where(declaration => declaration.Identifier.ValueText == className)
            .ToArray();
        if (classes.Length != 1)
        {
            violations.Add(
                $"{sourceName}: expected one class '{className}', found {classes.Length}");
            return violations.Order(StringComparer.Ordinal).ToArray();
        }

        var actual = new List<string>();
        foreach (var method in classes[0].Members.OfType<MethodDeclarationSyntax>())
        {
            var attributes = method.AttributeLists
                .SelectMany(static list => list.Attributes)
                .ToArray();
            var facts = attributes
                .Where(attribute => AttributeNameIs(attribute, "Fact"))
                .ToArray();
            var theories = attributes
                .Where(attribute => AttributeNameIs(attribute, "Theory"))
                .ToArray();
            var rows = attributes
                .Where(attribute => AttributeNameIs(attribute, "InlineData"))
                .ToArray();

            if (facts.Length == 0 && theories.Length == 0)
                continue;

            if (facts.Length == 1 && theories.Length == 0)
            {
                actual.Add($"Fact|{method.Identifier.ValueText}");
                if (rows.Length > 0)
                {
                    violations.Add(
                        $"{sourceName}: Fact '{method.Identifier.ValueText}' carries " +
                        $"{rows.Length} InlineData row(s)");
                }

                continue;
            }

            if (facts.Length == 0 && theories.Length == 1)
            {
                if (rows.Length == 0)
                {
                    violations.Add(
                        $"{sourceName}: Theory '{method.Identifier.ValueText}' has no InlineData rows");
                }

                foreach (var row in rows)
                {
                    var arguments = row.ArgumentList?.Arguments
                        .Select(argument => TestArgumentSignature(argument.Expression))
                        .ToArray() ?? [];
                    actual.Add(
                        $"Theory|{method.Identifier.ValueText}|{string.Join('|', arguments)}");
                }

                continue;
            }

            violations.Add(
                $"{sourceName}: test '{method.Identifier.ValueText}' must carry exactly " +
                "one Fact or one Theory attribute");
        }

        var expectedCounts = expected
            .GroupBy(static entry => entry, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.Count(), StringComparer.Ordinal);
        var actualCounts = actual
            .GroupBy(static entry => entry, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.Count(), StringComparer.Ordinal);
        foreach (var entry in expectedCounts.Keys
                     .Concat(actualCounts.Keys)
                     .Distinct(StringComparer.Ordinal)
                     .Order(StringComparer.Ordinal))
        {
            var expectedCount = expectedCounts.GetValueOrDefault(entry);
            var actualCount = actualCounts.GetValueOrDefault(entry);
            if (expectedCount > actualCount)
                violations.Add($"missing ({expectedCount - actualCount}x): {entry}");
            if (actualCount > expectedCount)
                violations.Add($"unexpected ({actualCount - expectedCount}x): {entry}");
        }

        return violations.Order(StringComparer.Ordinal).ToArray();
    }

    private static bool AttributeNameIs(AttributeSyntax attribute, string expectedName) =>
        attribute.Name.ToString() is var name &&
        (string.Equals(name, expectedName, StringComparison.Ordinal) ||
            string.Equals(name, expectedName + "Attribute", StringComparison.Ordinal));

    private static string TestArgumentSignature(ExpressionSyntax expression)
    {
        if (expression is LiteralExpressionSyntax literal)
        {
            var value = literal.Token.ValueText;
            if (literal.IsKind(SyntaxKind.StringLiteralExpression))
            {
                if (value.Length > 80 ||
                    value.IndexOfAny(['\r', '\n']) >= 0 ||
                    string.IsNullOrWhiteSpace(value))
                {
                    var digest = Convert
                        .ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))
                        .ToLowerInvariant();
                    return $"string(len={value.Length},sha256={digest})";
                }

                return $"string:{EscapeTestManifestValue(value)}";
            }

            if (literal.IsKind(SyntaxKind.CharacterLiteralExpression))
            {
                var character = Assert.IsType<char>(literal.Token.Value);
                return $"char:U+{(int)character:X4}";
            }

            if (literal.IsKind(SyntaxKind.NullLiteralExpression))
                return "null";
            if (literal.IsKind(SyntaxKind.TrueLiteralExpression) ||
                literal.IsKind(SyntaxKind.FalseLiteralExpression))
            {
                return $"bool:{value.ToLowerInvariant()}";
            }

            return $"literal:{EscapeTestManifestValue(value)}";
        }

        var syntax = Regex.Replace(expression.ToString(), @"\s+", "");
        return $"syntax:{EscapeTestManifestValue(syntax)}";
    }

    private static string EscapeTestManifestValue(string value) =>
        value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("|", "\\|", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);

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

    /// <summary>
    /// Finds argument-bearing validation calls outside the exact reviewed ValidationService class sources.
    /// </summary>
    /// <param name="sources">
    /// Production relative paths and their complete C# source texts.
    /// </param>
    /// <param name="allowedSources">
    /// Exact source paths where calls inside the ValidationService class are admitted.
    /// </param>
    /// <returns>
    /// File and line coordinates of every unauthorized scoped validation call.
    /// </returns>
    private static string[] ArgumentBearingValidationCallViolations(
        IEnumerable<(string RelativePath, string Source)> sources,
        IReadOnlyCollection<string> allowedSources)
    {
        var allowed = allowedSources.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return sources
            .SelectMany(source => CSharpSyntaxTree.ParseText(source.Source).GetRoot()
                .DescendantNodes().OfType<InvocationExpressionSyntax>()
                .Where(call => call.ArgumentList.Arguments.Count > 0 &&
                    (call.Expression is MemberAccessExpressionSyntax member &&
                     member.Name.Identifier.ValueText == "ValidateGameStateAsync" ||
                     call.Expression is MemberBindingExpressionSyntax binding &&
                     binding.Name.Identifier.ValueText == "ValidateGameStateAsync"))
                .Where(call => !allowed.Contains(source.RelativePath) ||
                    call.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault()
                        ?.Identifier.ValueText != "ValidationService")
                .Select(call =>
                    $"{source.RelativePath}:{LineNumber(source.Source, call.SpanStart)}"))
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

        if (ReviewedBroadValidationCallManifest.Count != 6 ||
            ReviewedBroadValidationCallManifest.Values.Sum() !=
            ReviewedBroadValidationCallCount)
        {
            violations.Add(
                "Reviewed broad-validation manifest must contain exactly " +
                $"6 files and {ReviewedBroadValidationCallCount} call sites.");
        }

        if (callSiteArray.Length > BroadValidationSentinelBudget)
        {
            violations.Add(
                $"observed {callSiteArray.Length} exceeds sentinel budget " +
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

    private static string PreparedCommandDisplayFixtureSource() =>
        File.ReadAllText(SourcePath(
            IntegrationTestsDirectory,
            "PreparedCommandDisplaySaveFixture.cs"));

    private static Dictionary<string, string> CommandDisplayTestSources() =>
        new(
            new[]
            {
                "MortalCommandDisplaySaveTests.cs",
                "ChaosSeaCommandDisplaySaveTests.cs",
                "ShiningAbodeCommandDisplaySaveTests.cs"
            }.ToDictionary(
                static fileName => fileName,
                static fileName => File.ReadAllText(SourcePath(IntegrationTestsDirectory, fileName)),
                StringComparer.Ordinal),
            StringComparer.Ordinal);

    private static IReadOnlyList<string> PreparedCommandDisplayContractViolations(
        string fixtureSource,
        IReadOnlyDictionary<string, string> testSources)
    {
        var violations = new List<string>();
        var fixtureRoot = CSharpSyntaxTree.ParseText(fixtureSource).GetCompilationUnitRoot();
        var fixture = fixtureRoot.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .SingleOrDefault(static declaration =>
                declaration.Identifier.ValueText == "PreparedCommandDisplaySaveFixture");
        if (fixture is null)
        {
            violations.Add("PreparedCommandDisplaySaveFixture declaration is missing.");
            return violations;
        }

        var constructor = fixture.Members.OfType<ConstructorDeclarationSyntax>().SingleOrDefault();
        if (!PreparedFixtureConstructorHasExactShape(constructor))
        {
            violations.Add(
                "Prepared fixture constructor must not access _templateRoot.Value.");
        }

        var initialize = fixture.Members.OfType<MethodDeclarationSyntax>().SingleOrDefault(
            static method => method.Identifier.ValueText == "InitializeAsync");
        if (!PreparedFixtureInitializeHasExactShape(initialize))
        {
            violations.Add(
                "Prepared fixture InitializeAsync must not access _templateRoot.Value.");
        }

        var clone = fixture.Members.OfType<MethodDeclarationSyntax>().SingleOrDefault(
            static method => method.Identifier.ValueText == "ClonePreparedTemplateAsync");
        var caseRootParameter = clone?.ParameterList.Parameters.SingleOrDefault()?.Identifier.ValueText;
        var copiesIntoCaseRoot = clone is not null &&
            !string.IsNullOrWhiteSpace(caseRootParameter) &&
            clone.DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Any(invocation =>
                    CommandDisplayInvokedMemberName(invocation) == "CopyDirectory" &&
                    invocation.ArgumentList.Arguments.Count == 2 &&
                    invocation.ArgumentList.Arguments[1].Expression is IdentifierNameSyntax destination &&
                    destination.Identifier.ValueText == caseRootParameter);
        if (!copiesIntoCaseRoot)
        {
            violations.Add(
                "ClonePreparedTemplateAsync must copy into its case-root parameter.");
        }

        var exactCloneSignature =
            clone is not null &&
            clone.ReturnType.ToString() == "Task" &&
            clone.Modifiers.Count == 2 &&
            clone.Modifiers.Any(SyntaxKind.PublicKeyword) &&
            clone.Modifiers.Any(SyntaxKind.AsyncKeyword) &&
            clone.ParameterList.Parameters.Count == 1 &&
            clone.ParameterList.Parameters[0].Type?.ToString() == "string" &&
            clone.ParameterList.Parameters[0].Identifier.ValueText == "caseRoot" &&
            clone.ExpressionBody is null;

        var exactCloneShape = false;
        if (exactCloneSignature &&
            clone!.Body is { Statements.Count: 3 } cloneBody &&
            string.Equals(caseRootParameter, "caseRoot", StringComparison.Ordinal))
        {
            var localStatement = cloneBody.Statements[0] as LocalDeclarationStatementSyntax;
            var localDeclarationHasExactShape =
                localStatement is not null &&
                localStatement.Modifiers.Count == 0 &&
                localStatement.UsingKeyword.RawKind == 0 &&
                localStatement.AwaitKeyword.RawKind == 0 &&
                localStatement.Declaration.Type.ToString() == "var" &&
                localStatement.Declaration.Variables.Count == 1;
            var preparedTemplateVariable = localDeclarationHasExactShape
                ? localStatement!.Declaration.Variables[0]
                : null;
            var readsPreparedTemplate =
                preparedTemplateVariable?.Identifier.ValueText == "preparedTemplate" &&
                preparedTemplateVariable.Initializer?.Value is AwaitExpressionSyntax
                {
                    Expression: MemberAccessExpressionSyntax
                    {
                        Expression: IdentifierNameSyntax { Identifier.ValueText: "_templateRoot" },
                        Name.Identifier.ValueText: "Value"
                    }
                };

            var failureGuard = cloneBody.Statements[1] as IfStatementSyntax;
            var failureThrow = failureGuard?.Statement as ThrowStatementSyntax;
            var objectCreations = clone.DescendantNodes()
                .OfType<ObjectCreationExpressionSyntax>()
                .ToArray();
            var createsOnlyFailureException =
                failureGuard?.Else is null &&
                failureGuard?.Condition is PrefixUnaryExpressionSyntax
                {
                    RawKind: (int)SyntaxKind.LogicalNotExpression,
                    Operand: MemberAccessExpressionSyntax
                    {
                        Expression: IdentifierNameSyntax { Identifier.ValueText: "preparedTemplate" },
                        Name.Identifier.ValueText: "SourceLoaded"
                    }
                } &&
                objectCreations.Length == 1 &&
                objectCreations[0].Type.ToString() == "InvalidOperationException" &&
                objectCreations[0].Initializer is null &&
                objectCreations[0].ArgumentList?.Arguments.Count == 1 &&
                objectCreations[0].ArgumentList!.Arguments[0].Expression
                    .NormalizeWhitespace()
                    .ToFullString() ==
                    "$\"Could not prepare command-display save '{_saveFileName}'.\"" &&
                failureThrow?.Expression == objectCreations[0];

            var copyStatement = cloneBody.Statements[2] as ExpressionStatementSyntax;
            var invocations = clone.DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .ToArray();
            var copyInvocation = invocations.SingleOrDefault();
            var performsOnlyAllowedCopy =
                invocations.Length == 1 &&
                copyStatement?.Expression == copyInvocation &&
                copyInvocation?.Expression is IdentifierNameSyntax
                {
                    Identifier.ValueText: "CopyDirectory"
                } &&
                copyInvocation.ArgumentList.Arguments.Count == 2 &&
                copyInvocation.ArgumentList.Arguments[0].Expression is MemberAccessExpressionSyntax
                {
                    Expression: IdentifierNameSyntax { Identifier.ValueText: "preparedTemplate" },
                    Name.Identifier.ValueText: "RootPath"
                } &&
                copyInvocation.ArgumentList.Arguments[1].Expression is IdentifierNameSyntax
                {
                    Identifier.ValueText: "caseRoot"
                };
            var containsHiddenControlFlowOrAssignment =
                clone.DescendantNodes().OfType<AssignmentExpressionSyntax>().Any() ||
                clone.DescendantNodes().OfType<LocalFunctionStatementSyntax>().Any() ||
                clone.DescendantNodes().OfType<AnonymousFunctionExpressionSyntax>().Any() ||
                clone.DescendantNodes().OfType<AwaitExpressionSyntax>().Count() != 1;

            exactCloneShape =
                readsPreparedTemplate &&
                createsOnlyFailureException &&
                performsOnlyAllowedCopy &&
                !containsHiddenControlFlowOrAssignment;
        }

        if (!exactCloneShape)
        {
            violations.Add(
                "ClonePreparedTemplateAsync must match the exact write-safe method shape.");
        }

        foreach (var (fileName, source) in testSources)
        {
            var root = CSharpSyntaxTree.ParseText(source).GetCompilationUnitRoot();
            var testClass = root.DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .SingleOrDefault(declaration =>
                    declaration.Identifier.ValueText == Path.GetFileNameWithoutExtension(fileName));
            if (testClass is null)
            {
                violations.Add($"{fileName}: test class declaration is missing.");
                continue;
            }

            var createRoot = testClass.Members.OfType<MethodDeclarationSyntax>().SingleOrDefault(
                static method => method.Identifier.ValueText == "CreateIsolatedRoot");
            var rootVariable = createRoot?.DescendantNodes()
                .OfType<VariableDeclaratorSyntax>()
                .SingleOrDefault(static variable => variable.Identifier.ValueText == "root");
            var rootInitializer = rootVariable?.Initializer?.Value;
            var usesOwnerRoot = rootInitializer is InvocationExpressionSyntax pathCombine &&
                CommandDisplayInvokedMemberName(pathCombine) == "Combine" &&
                pathCombine.ArgumentList.Arguments.Count >= 2 &&
                pathCombine.ArgumentList.Arguments[0].Expression is IdentifierNameSyntax ownerRoot &&
                ownerRoot.Identifier.ValueText == "_rootPath";
            var createsPerCallGuid = rootInitializer?.DescendantNodesAndSelf()
                .OfType<InvocationExpressionSyntax>()
                .Any(invocation =>
                    invocation.Expression is MemberAccessExpressionSyntax
                    {
                        Expression: IdentifierNameSyntax { Identifier.ValueText: "Guid" },
                        Name.Identifier.ValueText: "NewGuid"
                    }) == true;
            var createsOwnedDirectory = createRoot?.DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Any(invocation =>
                    invocation.Expression.ToString() == "Directory.CreateDirectory" &&
                    invocation.ArgumentList.Arguments.Count == 1 &&
                    invocation.ArgumentList.Arguments[0].Expression is IdentifierNameSyntax directory &&
                    directory.Identifier.ValueText == "root") == true;
            var returnsOwnedRoot = createRoot?.DescendantNodes()
                .OfType<ReturnStatementSyntax>()
                .Any(static statement =>
                    statement.Expression is IdentifierNameSyntax { Identifier.ValueText: "root" }) == true;

            if (!usesOwnerRoot)
            {
                violations.Add(
                    $"{fileName}: CreateIsolatedRoot must place the case root under the instance-owned root.");
            }

            if (!createsPerCallGuid)
            {
                violations.Add(
                    $"{fileName}: CreateIsolatedRoot must include a per-call Guid.NewGuid().");
            }

            if (!createsOwnedDirectory || !returnsOwnedRoot)
            {
                violations.Add(
                    $"{fileName}: CreateIsolatedRoot must create and return the same owned root.");
            }

            var execution = testClass.Members.OfType<MethodDeclarationSyntax>().SingleOrDefault(
                static method => method.Identifier.ValueText == "ExecuteFromLoadedSaveAsync");
            var loadRootVariable = execution?.DescendantNodes()
                .OfType<VariableDeclaratorSyntax>()
                .SingleOrDefault(static variable => variable.Identifier.ValueText == "loadRoot");
            var obtainsUniqueRoot = loadRootVariable?.Initializer?.Value is InvocationExpressionSyntax createCall &&
                CommandDisplayInvokedMemberName(createCall) == "CreateIsolatedRoot" &&
                createCall.ArgumentList.Arguments.Count == 0;
            if (!obtainsUniqueRoot)
            {
                violations.Add(
                    $"{fileName}: exhaustive helper must obtain its case root from CreateIsolatedRoot.");
            }

            var clonesIntoUniqueRoot = execution?.DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Any(invocation =>
                    invocation.Expression.ToString() == "_fixture.ClonePreparedTemplateAsync" &&
                    invocation.ArgumentList.Arguments.Count == 1 &&
                    invocation.ArgumentList.Arguments[0].Expression is IdentifierNameSyntax cloneRoot &&
                    cloneRoot.Identifier.ValueText == "loadRoot") == true;
            if (!clonesIntoUniqueRoot)
            {
                violations.Add(
                    $"{fileName}: exhaustive helper must clone into its unique case root.");
            }

            var fileSystemUsesUniqueRoot = execution?.DescendantNodes()
                .OfType<ObjectCreationExpressionSyntax>()
                .Any(creation =>
                    creation.Type.ToString() == "FileSystemManager" &&
                    creation.ArgumentList?.Arguments.FirstOrDefault()?.Expression is IdentifierNameSyntax fsRoot &&
                    fsRoot.Identifier.ValueText == "loadRoot") == true;
            if (!fileSystemUsesUniqueRoot)
            {
                violations.Add(
                    $"{fileName}: per-case FileSystemManager must use the unique case root.");
            }
        }

        return violations;
    }

    private static bool PreparedFixtureConstructorHasExactShape(
        ConstructorDeclarationSyntax? constructor)
    {
        if (constructor?.Body is not { Statements.Count: 4 } body ||
            constructor.ExpressionBody is not null ||
            constructor.Initializer is not null ||
            constructor.Modifiers.Count != 1 ||
            !constructor.Modifiers.Any(SyntaxKind.ProtectedKeyword) ||
            constructor.ParameterList.Parameters.Count != 2 ||
            constructor.ParameterList.Parameters[0].Type?.ToString() != "string" ||
            constructor.ParameterList.Parameters[0].Identifier.ValueText != "saveFileName" ||
            constructor.ParameterList.Parameters[1].Type?.ToString() != "string" ||
            constructor.ParameterList.Parameters[1].Identifier.ValueText != "templateRootPrefix")
        {
            return false;
        }

        var assignments = body.Statements
            .Select(static statement =>
                (statement as ExpressionStatementSyntax)?.Expression as AssignmentExpressionSyntax)
            .ToArray();
        if (assignments.Any(static assignment => assignment is null))
            return false;

        return assignments[0]!.Left.ToString() == "_saveFileName" &&
            assignments[0]!.Right.ToString() == "saveFileName" &&
            assignments[1]!.Left.ToString() == "_saveRelativePath" &&
            assignments[1]!.Right.NormalizeWhitespace().ToFullString() ==
                "\"saves/manual_saves/\" + saveFileName" &&
            assignments[2]!.Left.ToString() == "_templateRootPath" &&
            assignments[2]!.Right.NormalizeWhitespace().ToFullString() ==
                "Path.Combine(Path.GetTempPath(), templateRootPrefix + Guid.NewGuid().ToString(\"N\"))" &&
            assignments[3]!.Left.ToString() == "_templateRoot" &&
            assignments[3]!.Right.NormalizeWhitespace().ToFullString() ==
                "new Lazy<Task<PreparedTemplate>>(PrepareTemplateAsync, LazyThreadSafetyMode.ExecutionAndPublication)";
    }

    private static bool PreparedFixtureInitializeHasExactShape(
        MethodDeclarationSyntax? initialize) =>
        initialize is not null &&
        initialize.ReturnType.ToString() == "Task" &&
        initialize.Modifiers.Count == 1 &&
        initialize.Modifiers.Any(SyntaxKind.PublicKeyword) &&
        initialize.ParameterList.Parameters.Count == 0 &&
        initialize.Body is null &&
        initialize.ExpressionBody?.Expression.NormalizeWhitespace().ToFullString() ==
            "Task.CompletedTask";

    private static string? CommandDisplayInvokedMemberName(InvocationExpressionSyntax invocation) =>
        invocation.Expression switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText,
            MemberBindingExpressionSyntax memberBinding => memberBinding.Name.Identifier.ValueText,
            _ => null
        };

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
        return await RunCSharpRunnerSelfTestAsync("TrxSummary", trxDirectory);
    }

    private static async Task<RunnerSelfTestResult> RunCSharpRunnerSelfTestAsync(
        string selfTest,
        string? trxDirectory = null)
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
            selfTest
        })
        {
            startInfo.ArgumentList.Add(argument);
        }
        if (!string.IsNullOrWhiteSpace(trxDirectory))
        {
            startInfo.ArgumentList.Add("-SelfTestTrxDirectory");
            startInfo.ArgumentList.Add(trxDirectory);
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

    /// <summary>
    /// Creates a minimal TRX with named results for runner parser boundary checks.
    /// </summary>
    /// <param name="testId">
    /// Test identity shared by the result rows and definition.
    /// </param>
    /// <param name="storage">
    /// Test assembly storage, or <see langword="null"/> to exercise missing mapping rejection.
    /// </param>
    /// <param name="resultCount">
    /// Number of result rows and reported cases to generate.
    /// </param>
    /// <returns>
    /// A synthetic TRX document containing the requested result rows and counters.
    /// </returns>
    private static string SyntheticTrx(
        string testId,
        string? storage,
        int resultCount)
    {
        var results = string.Concat(Enumerable.Range(1, resultCount).Select(index =>
            $"""<UnitTestResult testId="{testId}" testName="BookOfEternityClient.Tests.RunnerFixture.Test" executionId="execution-{index}" outcome="Passed" />"""));
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
