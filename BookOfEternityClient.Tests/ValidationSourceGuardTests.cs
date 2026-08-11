using System.Reflection;
using System.Text.RegularExpressions;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ValidationSourceGuardTests
{
    [Fact]
    public void ActorMaterializationAuthority_MustNotUseProseOrGenreKeywordInference()
    {
        var validationDirectory = Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "Services",
            "Validation");
        var sourceFiles = new[]
            {
                Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient", "Services", "ActorMaterializationContract.cs"),
                Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient", "Services", "MortalBootstrapStateBuilder.cs"),
                Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient", "Services", "NpcTradeService.cs"),
                Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient", "Services", "SystemGuardianLibraryService.cs"),
                Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient", "Services", "CanonicalStateNormalizer", "CanonicalStateNormalizer.Npcs.cs"),
                Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient", "Services", "CanonicalStateNormalizer", "CanonicalStateNormalizer.AfterlifeEntityProfiles.cs"),
                Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient", "Services", "GmWorkers", "ActorMaterializationRepairPreservationGuard.cs"),
                Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient", "Services", "GmWorkers", "GmWorkerApplyGate.cs"),
                Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient", "Services", "GmWorkers", "GmWorkerContractValidator.cs"),
                Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient", "Services", "GmWorkers", "GmWorkerTaskPacketBuilder.cs"),
                Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient", "Services", "GmWorkers", "GmWorkerValidationRepairDelegator.cs")
            }
            .Concat(Directory.EnumerateFiles(
                validationDirectory,
                "ValidationService.ActorMaterialization*.cs",
                SearchOption.TopDirectoryOnly))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        var source = string.Join(Environment.NewLine, sourceFiles.Select(File.ReadAllText));

        Assert.Contains(
            sourceFiles,
            path => path.EndsWith("ValidationService.ActorMaterializationTradeAuthority.cs", StringComparison.Ordinal));
        Assert.Contains(
            sourceFiles,
            path => path.EndsWith("MortalBootstrapStateBuilder.cs", StringComparison.Ordinal));
        Assert.Contains(
            sourceFiles,
            path => path.EndsWith("CanonicalStateNormalizer.AfterlifeEntityProfiles.cs", StringComparison.Ordinal));
        Assert.Contains(
            sourceFiles,
            path => path.EndsWith("ActorMaterializationRepairPreservationGuard.cs", StringComparison.Ordinal));
        Assert.All(sourceFiles, path => Assert.True(File.Exists(path), $"Missing guarded source file: {path}"));

        var proseAuthorityRead = new Regex(
            "(?:TryGetProperty|ReadActorMaterializationString|TryReadExactNonEmptyString)\\s*\\([^\\r\\n;]*\\\"(?:displayName|name|description|occupation|profession|tags|history)\\\"",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var proseStringMatching = new Regex(
            "(?:(?:displayName|name|description|occupation|profession|tags|history|role|genre)\\w*\\s*\\.\\s*(?:Contains|StartsWith|EndsWith|IndexOf)\\s*\\(|(?:Contains|StartsWith|EndsWith|IndexOf)\\s*\\(\\s*(?:displayName|name|description|occupation|profession|tags|history|role|genre)\\w*)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var genreKeywordTable = new Regex(
            "(?:keyword|genre|fantasy|science.?fiction|post.?apoc|occupation|profession)[^\\r\\n]*(?:Dictionary|HashSet)|(?:Dictionary|HashSet)[^\\r\\n]*(?:keyword|genre|fantasy|science.?fiction|post.?apoc|occupation|profession)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        Assert.DoesNotMatch(proseAuthorityRead, source);
        Assert.DoesNotMatch(proseStringMatching, source);
        Assert.DoesNotMatch(genreKeywordTable, source);

        var tradeSource = File.ReadAllText(Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "Services",
            "NpcTradeService.cs"));
        var npcValidationSource = File.ReadAllText(Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "Services",
            "Validation",
            "ValidationService.NpcWorldAndMeta.cs"));
        var trainingValidationSource = File.ReadAllText(Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "Services",
            "Validation",
            "ValidationService.Training.cs"));
        Assert.DoesNotContain("sourceParts", tradeSource, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "GetFirstNonEmptyString(npc, \"occupation\")",
            npcValidationSource,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "GetTrainingString(npc, \"occupation\")",
            trainingValidationSource,
            StringComparison.Ordinal);

        var normalizedProfileStart = npcValidationSource.IndexOf(
            "private static string ResolveNormalizedMerchantProfileForValidation(",
            StringComparison.Ordinal);
        var tradeValidationStart = npcValidationSource.IndexOf(
            "private void ValidateNpcTradeState(",
            normalizedProfileStart,
            StringComparison.Ordinal);
        Assert.True(
            normalizedProfileStart >= 0 && tradeValidationStart > normalizedProfileStart,
            "Expected the explicit merchant-profile normalization helper.");
        var normalizedProfileSource =
            npcValidationSource[normalizedProfileStart..tradeValidationStart];

        var usableTradeStart = trainingValidationSource.IndexOf(
            "private static bool HasUsableMortalTradeState(",
            StringComparison.Ordinal);
        var capabilityStart = trainingValidationSource.IndexOf(
            "private static bool MortalBootstrapScaffoldRequestsTraining(",
            usableTradeStart,
            StringComparison.Ordinal);
        Assert.True(
            usableTradeStart >= 0 && capabilityStart > usableTradeStart,
            "Expected the Mortal bootstrap trade-state helper.");
        var usableTradeSource = trainingValidationSource[usableTradeStart..capabilityStart];

        foreach (var proseField in new[] { "\"role\"", "\"occupation\"", "\"class\"", "\"name\"", "\"description\"" })
        {
            Assert.DoesNotContain(proseField, normalizedProfileSource, StringComparison.Ordinal);
            Assert.DoesNotContain(proseField, usableTradeSource, StringComparison.Ordinal);
        }

        var guardianRelationshipSource = File.ReadAllText(Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "Core",
            "GuardianRelationshipRules.cs"));
        var residentSource = File.ReadAllText(Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "Services",
            "GuardianAbodeResidentState.cs"));
        var equipmentSource = File.ReadAllText(Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "Services",
            "InventoryEquipmentService.cs"));
        var systemGuardianSource = File.ReadAllText(Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "Services",
            "SystemGuardianLibraryService.cs"));

        Assert.DoesNotContain("ContainsAny(archetype", guardianRelationshipSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CollectSeedKeywords", residentSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SeedArchetype(", residentSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SeedCoreValues(", residentSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TypeToSlot", equipmentSource, StringComparison.Ordinal);
        Assert.DoesNotContain("label.Contains(itemSlot", equipmentSource, StringComparison.Ordinal);
        Assert.DoesNotContain("marker.Contains(\"soul_relic\"", equipmentSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ContainsAny(type", tradeSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ContainsAny(group", tradeSource, StringComparison.Ordinal);
        Assert.DoesNotContain("BuildFreeformCoreValues", systemGuardianSource, StringComparison.Ordinal);

        var soulRelicHelperStart = tradeSource.IndexOf(
            "private static bool IsSoulRelicLikeItem(",
            StringComparison.Ordinal);
        var nextTradeHelperStart = tradeSource.IndexOf(
            "private static int FindInventoryItemIndex(",
            soulRelicHelperStart,
            StringComparison.Ordinal);
        Assert.True(
            soulRelicHelperStart >= 0 &&
            nextTradeHelperStart > soulRelicHelperStart,
            "Expected the NPC trade Soul Relic authority helper.");
        var soulRelicHelper =
            tradeSource[soulRelicHelperStart..nextTradeHelperStart];
        Assert.Contains(
            "GetNodeString(item[\"relicId\"])",
            soulRelicHelper,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "soulRelicId",
            soulRelicHelper,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "GetNodeString(item[\"type\"])",
            soulRelicHelper,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "GetNodeString(item[\"group\"])",
            soulRelicHelper,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            ".Contains(",
            soulRelicHelper,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            ".StartsWith(",
            soulRelicHelper,
            StringComparison.Ordinal);

        var questHelperStart = tradeSource.IndexOf(
            "private static bool IsQuestBoundItem(",
            StringComparison.Ordinal);
        Assert.True(
            questHelperStart >= 0 &&
            soulRelicHelperStart > questHelperStart,
            "Expected the NPC trade quest-item authority helper.");
        var questHelper =
            tradeSource[questHelperStart..soulRelicHelperStart];
        Assert.Contains(
            "item[\"isQuestItem\"]",
            questHelper,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "item[\"group\"]",
            questHelper,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "GetNodeString(",
            questHelper,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ResourceAuthorityBoundaries_MustPreflightZipAndRetainLoadLeavesThroughPublication()
    {
        var saveLoadSource = File.ReadAllText(Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "Services",
            "SaveLoadService.cs"));
        var fileSystemSource = File.ReadAllText(Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "Core",
            "FileSystemManager.cs"));

        foreach (var methodName in new[]
                 {
                     "public async Task<bool> LoadGameAsync(",
                     "private static async Task<SaveMetadata?> ReadSaveMetadataAsync("
                 })
        {
            var methodStart = saveLoadSource.IndexOf(
                methodName,
                StringComparison.Ordinal);
            var archiveMaterialization = saveLoadSource.IndexOf(
                "new ZipArchive(",
                methodStart,
                StringComparison.Ordinal);
            var rawPreflight = saveLoadSource.IndexOf(
                "ValidateTrustedArchiveBeforeMaterialization(",
                methodStart,
                StringComparison.Ordinal);
            Assert.True(
                methodStart >= 0 &&
                rawPreflight > methodStart &&
                archiveMaterialization > rawPreflight,
                $"{methodName} must run raw bounded archive preflight before ZipArchive materializes entries.");
        }

        var prepareStart = fileSystemSource.IndexOf(
            "internal void PrepareForDirectoryMove(",
            StringComparison.Ordinal);
        var afterMoveStart = fileSystemSource.IndexOf(
            "internal void EnsureExactAfterDirectoryMove(",
            prepareStart,
            StringComparison.Ordinal);
        var afterMoveEnd = fileSystemSource.IndexOf(
            "internal void EnsurePublishedExactBeforeActivation(",
            afterMoveStart,
            StringComparison.Ordinal);
        Assert.True(
            prepareStart >= 0 &&
            afterMoveStart > prepareStart &&
            afterMoveEnd > afterMoveStart,
            "Expected load-staging publication authority methods.");
        var prepareSource =
            fileSystemSource[prepareStart..afterMoveStart];
        var afterMoveSource =
            fileSystemSource[afterMoveStart..afterMoveEnd];

        Assert.DoesNotContain(
            "ReleaseFileStreams(",
            prepareSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "file.Stream",
            afterMoveSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "file.GuardPath",
            afterMoveSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "expectedNumberOfLinks: 2",
            afterMoveSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "DeleteOpenedFile(",
            afterMoveSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "allowRename: true",
            afterMoveSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "DeleteOpenedDirectory(",
            afterMoveSource,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "Directory.Delete(",
            afterMoveSource,
            StringComparison.Ordinal);
    }

    [Fact]
    public void NpcCoreChangesTests_MustNotUseProductionMortalBootstrapAsFixtureAuthority()
    {
        var source = File.ReadAllText(Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient.IntegrationTests",
            "NpcCoreChangesTests.cs"));

        Assert.DoesNotContain("MortalBootstrapStateBuilder", source, StringComparison.Ordinal);
    }

    [Fact]
    public void MortalBootstrapAuthorityGuidance_MustRequireCanonicalPathAndExactValues()
    {
        var sources = new[]
        {
            Path.Combine(
                TestRepoPaths.RepoRoot,
                "BookOfEternityClient",
                "Core",
                "GameEngine",
                "GameEngine.TurnLifecycle.cs"),
            Path.Combine(
                TestRepoPaths.RepoRoot,
                "BookOfEternityClient",
                "Launcher",
                "Generate_CLI_Launch_Script.ps1"),
            Path.Combine(
                TestRepoPaths.RepoRoot,
                "BookOfEternityClient",
                "Launcher",
                "CLI_Launch_Script.md"),
            Path.Combine(
                TestRepoPaths.RepoRoot,
                "Examples",
                "E_CLI_Step_Main.txt"),
            Path.Combine(
                TestRepoPaths.RepoRoot,
                "TaskGuides",
                "CLI_Step_Main.txt"),
            Path.Combine(
                TestRepoPaths.RepoRoot,
                "CLI_Agent_Daemon_Specification.md"),
            Path.Combine(
                TestRepoPaths.RepoRoot,
                "BookOfEternityClient",
                "game_master_daemon.ps1"),
            Path.Combine(
                TestRepoPaths.RepoRoot,
                "BookOfEternityClient",
                "Core",
                "GameEngine",
                "GameEngine.ValidationAndRepair.cs")
        };

        Assert.All(sources, path =>
        {
            var source = File.ReadAllText(path);
            Assert.Contains("canonicalPath", source, StringComparison.Ordinal);
            Assert.Contains("values", source, StringComparison.Ordinal);
            Assert.DoesNotContain("valid or resolvable merchantProfile", source, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void MortalBootstrapHarness_MustNotHideProseInferenceBehindHelpers()
    {
        var builderSource = File.ReadAllText(Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "Services",
            "MortalBootstrapStateBuilder.cs"));
        var trainingSource = File.ReadAllText(Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "Services",
            "Validation",
            "ValidationService.Training.cs"));
        var scaffoldSource = File.ReadAllText(Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "Core",
            "GameEngine",
            "GameEngine.TurnLifecycle.cs"));

        Assert.DoesNotContain("ContainsAny(", builderSource, StringComparison.Ordinal);
        Assert.DoesNotContain("InferStarter", builderSource, StringComparison.Ordinal);
        Assert.DoesNotContain("BuildStarterCompetency", builderSource, StringComparison.Ordinal);
        Assert.DoesNotContain("BuildStarterTeacher", builderSource, StringComparison.Ordinal);
        Assert.DoesNotContain("BuildStarterActiveSkills", builderSource, StringComparison.Ordinal);
        Assert.DoesNotContain("BuildStarterPassiveSkills", builderSource, StringComparison.Ordinal);
        Assert.Contains("\"structuredGmAuthority\"", scaffoldSource, StringComparison.Ordinal);
        Assert.Contains("\"authoredBy\"", scaffoldSource, StringComparison.Ordinal);
        Assert.Contains("\"GM\"", scaffoldSource, StringComparison.Ordinal);
        Assert.Contains("\"proseIsMechanicalAuthority\"", scaffoldSource, StringComparison.Ordinal);
        Assert.DoesNotContain("client-declared mechanical requirements", scaffoldSource, StringComparison.Ordinal);

        var bootstrapTrainingStart = trainingSource.IndexOf(
            "private static bool MortalBootstrapScaffoldRequestsTraining(",
            StringComparison.Ordinal);
        var trainingSnapshotStart = trainingSource.IndexOf(
            "private static void ValidateTrainingShowcaseSnapshot(",
            bootstrapTrainingStart,
            StringComparison.Ordinal);
        Assert.True(
            bootstrapTrainingStart >= 0 && trainingSnapshotStart > bootstrapTrainingStart,
            "Expected the Mortal bootstrap capability-authority helper block.");
        var bootstrapTrainingSource = trainingSource[bootstrapTrainingStart..trainingSnapshotStart];
        Assert.Contains("structuredGmAuthority", bootstrapTrainingSource, StringComparison.Ordinal);
        Assert.DoesNotContain("playerAuthoredStart", bootstrapTrainingSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ContainsTrainingAnchorKeyword", bootstrapTrainingSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ContainsTradeAnchorKeyword", bootstrapTrainingSource, StringComparison.Ordinal);
    }

    [Fact]
    public void MortalNpcTemplate_MustUseSettingDefinedCharacteristicAuthority()
    {
        var path = Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "game_master_daemon.ps1");
        var source = File.ReadAllText(path);
        var templateStart = source.IndexOf(
            "# Compact Mortal World NPC Update Template",
            StringComparison.Ordinal);
        var templateEnd = source.IndexOf("\n'@", templateStart, StringComparison.Ordinal);

        Assert.True(templateStart >= 0 && templateEnd > templateStart, "Mortal NPC template source block was not found.");
        var template = source[templateStart..templateEnd];
        Assert.Contains("current-world canonical characteristic authority", template, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"setting_defined_characteristic_key\": 0", template, StringComparison.Ordinal);
        foreach (var universalKey in new[]
                 {
                     "strength", "dexterity", "constitution", "intelligence", "wisdom", "faith",
                     "attractiveness", "trade", "persuasion", "perception", "luck", "speed"
                 })
        {
            Assert.DoesNotContain($"\"{universalKey}\":", template, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void MortalNpcAuthoringSources_MustNotMandateOrdinaryExistingUpdateNpcFullObjects()
    {
        var block19 = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "Rules", "Block_19.txt"));
        var block19D = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "Rules", "Block_19.D.txt"));
        var example = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "Examples", "E_CLI_Step_Main.txt"));
        var authoritativeRules = string.Join(Environment.NewLine, block19, block19D);
        var auditedSources = string.Join(Environment.NewLine, authoritativeRules, example);

        foreach (var forbiddenMandate in new[]
                 {
                     "A fundamental property of an existing NPC is being overwritten",
                     "add it to their 'fateCards' array via 'UpdateNPCs'",
                     "Set 'isUnlocked: true' for that card within 'UpdateNPCs'",
                     "Apply 'rewards.statBoosts' to 'characteristics' in 'UpdateNPCs'",
                     "add them to 'activeSkills'/'passiveSkills' in 'UpdateNPCs'",
                     "sending the complete, updated NPC Object in the 'UpdateNPCs' array",
                     "report the complete, updated NPC Object for any NPC whose state changed in the 'UpdateNPCs' array",
                     "All these mechanical changes MUST be reported in 'UpdateNPCs'",
                     "Step B: Update the Mechanical State ('UpdateNPCs')"
                 })
        {
            Assert.DoesNotContain(forbiddenMandate, authoritativeRules, StringComparison.OrdinalIgnoreCase);
        }

        var mandatoryOrdinaryExistingLine = new Regex(
            "^(?=[^\\r\\n]*\\bUpdateNPCs\\b)(?=[^\\r\\n]*(?:MUST|report|send|apply|add|complete|updated))(?=[^\\r\\n]*(?:existing|worldview|rank|location|level|progression|fate|mechanical|state changed))(?![^\\r\\n]*(?:DO NOT|must not|never|not accepted|new NPC|newly significant|genuinely new|legacy[- ]promotion|remove)).*$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Multiline);
        Assert.DoesNotMatch(mandatoryOrdinaryExistingLine, auditedSources);

        Assert.DoesNotContain(
            "unchanged canonical NPC objects that exactly match the validated pre-turn snapshot may remain in `UpdateNPCs` / `NPCsInScene`",
            example,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "If `UpdateNPCs` / `NPCsInScene` contains an existing NPC object",
            example,
            StringComparison.OrdinalIgnoreCase);

        const string mismatchStart = "Concrete structured NPC mismatch example:";
        const string mismatchEnd = "Concrete Mortal relevant NPC without persistence example:";
        var start = example.IndexOf(mismatchStart, StringComparison.Ordinal);
        var end = example.IndexOf(mismatchEnd, start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start, "Structured NPC mismatch worked example was not found.");
        var mismatchExample = example[start..end];
        Assert.Contains("NPCCoreChanges", mismatchExample, StringComparison.Ordinal);
        Assert.DoesNotContain("UpdateNPCs", mismatchExample, StringComparison.Ordinal);
    }

    [Fact]
    public void MortalAcceptedTurnAuthority_MustKeepPromotionAndRequestOwnedDisplaysBounded()
    {
        var block19 = File.ReadAllText(Path.Combine(
            TestRepoPaths.RepoRoot,
            "Rules",
            "Block_19.txt"));
        var daemon = File.ReadAllText(Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "game_master_daemon.ps1"));
        var trainingExample = File.ReadAllText(Path.Combine(
            TestRepoPaths.RepoRoot,
            "Examples",
            "E_CLI_Training_Showcases.txt"));
        var authoritySource = File.ReadAllText(Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "Services",
            "MortalActorAcceptedTurnAuthority.cs"));
        var continuitySource = File.ReadAllText(Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "Services",
            "Validation",
            "ValidationService.ActorMaterializationContinuity.cs"));

        Assert.All(new[] { block19, daemon }, source =>
        {
            Assert.Contains("teacherProfile", source, StringComparison.Ordinal);
            Assert.Contains("tradeState", source, StringComparison.Ordinal);
            Assert.Contains("activeSkills", source, StringComparison.Ordinal);
            Assert.Contains("currentActivity", source, StringComparison.Ordinal);
            Assert.Contains("tradeInventory", source, StringComparison.Ordinal);
            Assert.Contains("trainingShowcase", source, StringComparison.Ordinal);
        });
        Assert.Contains("only exact identity/display fields", daemon, StringComparison.Ordinal);
        Assert.Contains("sourceActorId", trainingExample, StringComparison.Ordinal);
        Assert.Contains("sourceActorName", trainingExample, StringComparison.Ordinal);
        Assert.Contains("AuthorizesDedicatedTrainingPatch", authoritySource, StringComparison.Ordinal);
        Assert.Contains("UpdateReceiptsProperty", authoritySource, StringComparison.Ordinal);
        Assert.Contains(
            "actor_materialization_duplicate_effective_identity",
            continuitySource,
            StringComparison.Ordinal);
    }

    [Fact]
    public void MortalNpcAuthoringSources_MustUseOnlyExplicitSettingCharacteristicAuthority()
    {
        var block19 = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "Rules", "Block_19.txt"));
        var block19D = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "Rules", "Block_19.D.txt"));
        var authoritativeRules = string.Join(Environment.NewLine, block19, block19D);

        var forbiddenMechanicalAssumptions = new Dictionary<string, string>
        {
            ["fixed Strength/Constitution carrying formula"] =
                "universal formula[^\\r\\n]*Strength[^\\r\\n]*Constitution",
            ["fixed standard-characteristic point math"] =
                "standard characteristics[^\\r\\n]*level \\* 2[^\\r\\n]*level \\* 5",
            ["universal five-point level grant"] =
                "EACH level gained[^\\r\\n]*5 new standard characteristic points",
            ["warrior/mage characteristic allocation"] =
                "archetype[^\\r\\n]*(?:warrior|mage|\\u0432\\u043e\\u0438\\u043d|\\u043c\\u0430\\u0433)[^\\r\\n]*(?:strength|intelligence|\\u0441\\u0438\\u043b|\\u0438\\u043d\\u0442\\u0435\\u043b\\u043b)",
            ["fixed Fate Card characteristic example"] =
                "(?:\\+5 strength|\\+1 standardIntelligence)"
        };

        foreach (var (description, pattern) in forbiddenMechanicalAssumptions)
        {
            Assert.False(
                Regex.IsMatch(
                    authoritativeRules,
                    pattern,
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
                $"Authoritative Mortal rules still contain {description}.");
        }

        Assert.Contains("current-world carrying-capacity authority", block19, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("set 'maxWeight' to null", block19, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("current-world progression authority", block19, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("do not invent characteristic points", block19, StringComparison.OrdinalIgnoreCase);

        // Narrative uses of the word are legal; only mandatory mechanical formulas are guarded.
        Assert.Contains("greatest strengths and greatest weaknesses", block19, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PlayerFacingActorViews_MustNotReferencePrivateMaterializationMetadata()
    {
        var sourceRoots = new[]
        {
            Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient", "UI"),
            Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient", "WebUi"),
            Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient.WebFrontend", "src")
        };
        var extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".cs", ".ts", ".tsx", ".js", ".jsx"
        };
        var privateTokens = new[]
        {
            "materializationId",
            "materializedAtTurn",
            "empty_by_design",
            "actor_materialization_"
        };

        foreach (var sourceRoot in sourceRoots.Where(Directory.Exists))
        {
            foreach (var file in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories)
                         .Where(file => extensions.Contains(Path.GetExtension(file))))
            {
                var source = File.ReadAllText(file);
                foreach (var token in privateTokens)
                {
                    Assert.DoesNotContain(token, source, StringComparison.OrdinalIgnoreCase);
                }
            }
        }
    }

    [Fact]
    public void ClientOwnedSurfaceFilter_MustCoverAllValidatedAfterlifePendingContracts()
    {
        var method = typeof(ValidationService).GetMethod(
            "IsClientOwnedSurfaceValidationPath",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var paths = new[]
        {
            GuardianAbodeOfferingState.PendingRequestPath,
            GuardianTradeRequestState.PendingRequestPath,
            PlayerGuardianFoundationState.PendingRequestPath,
            NpcTradeRequestState.PendingRequestPath,
            CraftRequestState.PendingRequestPath,
            AfterlifeArchiveActionState.ConsultationRequestPath,
            AfterlifeArchiveActionState.ProjectFuelRequestPath,
            GuardianAbodeResidentRequestState.PendingResidentsRequestPath,
            GuardianAbodeResidentRequestState.PendingInteractionsRequestPath,
            GuardianAbodeResidentRequestState.PendingTransfersRequestPath,
            GuardianAbodeResidentRequestState.PendingManifestationRequestPath,
            ActorSocialInteractionRequestState.PendingGuardianRequestPath,
            ActorSocialInteractionRequestState.PendingNpcRequestPath,
            SystemGuardianLibraryService.AttractionRequestPath,
            ShiningCoreActionRequestState.PendingActionsRequestPath,
            ShiningTradeRequestState.PendingRequestsPath,
            ShiningFactionRequestState.PendingFoundingsRequestPath,
            ShiningFactionRequestState.PendingRealignmentsRequestPath,
            ShiningFactionRequestState.PendingLeadershipTransitionsRequestPath,
            SourceOfLightCapstoneState.PendingRequestPath
        };

        foreach (var path in paths)
        {
            var isClientOwned = Assert.IsType<bool>(method.Invoke(null, new object[] { path }));
            Assert.True(isClientOwned, $"{path} must be excluded from generic tracked-file validation and handled by the client-owned contract validator.");
        }
    }

    [Fact]
    public void PreTurnRealmResolution_MustNotFallbackToCurrentRealm()
    {
        var path = Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "Services",
            "Validation",
            "ValidationService.NpcWorldAndMeta.cs");
        var source = File.ReadAllText(path);

        Assert.DoesNotContain("return await TryResolveCurrentRealmAsync();", source, StringComparison.Ordinal);
    }

    [Fact]
    public void MemoryGates_PreviousLegacyRead_MustUseValidatedSnapshotInsteadOfConventionalSnapshotCopy()
    {
        var path = Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "Services",
            "Validation",
            "ValidationService.AcceptedTurnAndInkFeathers.cs");
        var source = File.ReadAllText(path);

        Assert.DoesNotContain("const string snapshotPath = \"game_state/control/pending_turn_snapshot/game_state/meta/soul_state.json\";", source, StringComparison.Ordinal);
        Assert.Contains("ReadValidatedCurrentPreTurnTrackedFileAsync(\"game_state/meta/soul_state.json\")", source, StringComparison.Ordinal);
    }

    [Fact]
    public void GenericPreTurnTrackedReads_MustUseValidatedSnapshotInsteadOfRawRollbackBackups()
    {
        var path = Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "Services",
            "Validation",
            "ValidationService.AcceptedTurnAndInkFeathers.cs");
        var source = File.ReadAllText(path);

        Assert.Contains("return await ReadValidatedCurrentPreTurnTrackedFileAsync(relativePath);", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SyncPreTurnTrackedReads_MustUseValidatedSnapshotInsteadOfRawRollbackBackups()
    {
        var path = Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "Services",
            "Validation",
            "ValidationService.AcceptedTurnAndInkFeathers.cs");
        var source = File.ReadAllText(path);

        Assert.Contains("return ReadValidatedCurrentPreTurnTrackedFileSync(relativePath);", source, StringComparison.Ordinal);
    }

    [Fact]
    public void BootstrapAndRealmSegregation_MustUseValidatedPendingSnapshotManifestForSourceLabelAuthority()
    {
        var path = Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "Services",
            "Validation",
            "ValidationService.BootstrapAndProtocol.cs");
        var source = File.ReadAllText(path);

        Assert.Contains("var manifest = await LoadValidatedCurrentPendingTurnSnapshotManifestAsync();", source, StringComparison.Ordinal);
        Assert.Contains("LoadRequiredValidatedCurrentPendingTurnSnapshotManifestAsync(", source, StringComparison.Ordinal);
    }

    [Fact]
    public void DiffAgainstManifest_MustUseValidatedSnapshotFileInsteadOfRawRollbackBackup()
    {
        var path = Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "Services",
            "Validation",
            "ValidationService.AcceptedTurnAndInkFeathers.cs");
        var source = File.ReadAllText(path);

        Assert.Contains("DescribeTrackedFileChangeAgainstManifestAsync", source, StringComparison.Ordinal);
        Assert.Contains("var previous = await ReadValidatedPendingTurnSnapshotFileAsync(manifest, relativePath);", source, StringComparison.Ordinal);
        Assert.DoesNotContain("manifest.RollbackBackups.TryGetValue(relativePath, out var backupPath)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void DiffAgainstManifest_MustNotInferChangedFromMissingValidatedBaselineHeuristic()
    {
        var path = Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "Services",
            "Validation",
            "ValidationService.AcceptedTurnAndInkFeathers.cs");
        var source = File.ReadAllText(path);

        Assert.DoesNotContain("if (previous == null)\r\n            return !string.IsNullOrWhiteSpace(current);", source, StringComparison.Ordinal);
        Assert.Contains("ValidatedTrackedFileChangeStatus.MissingValidatedBaseline", source, StringComparison.Ordinal);
        Assert.Contains("if (IsTrackedByValidatedBaseline(manifest, relativePath))", source, StringComparison.Ordinal);
        Assert.Contains("? ValidatedTrackedFileChangeStatus.Changed", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidatedPendingSnapshotManifest_MustRequireUsableStructure()
    {
        var path = Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "Services",
            "Validation",
            "ValidationService.AcceptedTurnAndInkFeathers.cs");
        var source = File.ReadAllText(path);

        Assert.Contains("PendingTurnSnapshotAuthority.TryValidateManifestForReaderAuthority(", source, StringComparison.Ordinal);
        Assert.Contains("static snapshotManifest => snapshotManifest.RollbackBaselineFiles", source, StringComparison.Ordinal);
        Assert.Contains("static snapshotManifest => snapshotManifest.RollbackBackups", source, StringComparison.Ordinal);
        Assert.Contains("ReadRelativeFileBytesFromWorkspace", source, StringComparison.Ordinal);
        Assert.DoesNotContain("PendingTurnSnapshotAuthority.TryValidateManifestAgainstAuthority(", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SarefMainStoryState_MustBeOptionalCanonicalBaseline()
    {
        var path = Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "Core",
            "GameEngine",
            "GameEngine.SessionAndSnapshots.cs");
        var source = File.ReadAllText(path);

        Assert.Contains(
            "if (_fs.FileExists(writeLease, SarefMainStoryState.StatePath))",
            source,
            StringComparison.Ordinal);
        Assert.Contains("TryAddOptionalCanonicalBaselineSnapshotAsync(", source, StringComparison.Ordinal);
        Assert.Contains("SarefMainStoryState.StatePath))", source, StringComparison.Ordinal);
    }

    [Fact]
    public void PolicySensitiveSnapshotAuthorityConsumers_MustUseRollbackBackedParity()
    {
        var normalizerPath = Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "Services",
            "CanonicalStateNormalizer",
            "CanonicalStateNormalizer.SoulAndMeta.cs");
        var guardianPowerPath = Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "Services",
            "GuardianPowerEventState.cs");

        var normalizerSource = File.ReadAllText(normalizerPath);
        var guardianPowerSource = File.ReadAllText(guardianPowerPath);

        Assert.Contains("PendingTurnSnapshotAuthority.TryValidateManifestForReaderAuthority(", normalizerSource, StringComparison.Ordinal);
        Assert.Contains("JsonObject? GachaBaseResult", normalizerSource, StringComparison.Ordinal);
        Assert.Contains("static snapshotManifest => snapshotManifest.RollbackBackups", normalizerSource, StringComparison.Ordinal);
        Assert.Contains("relativePath => ReadRelativeFileBytesFromWorkspace(fs, relativePath)", normalizerSource, StringComparison.Ordinal);

        Assert.Contains("PendingTurnSnapshotAuthority.TryValidateManifestForReaderAuthority(", guardianPowerSource, StringComparison.Ordinal);
        Assert.Contains("JsonObject? GachaBaseResult", guardianPowerSource, StringComparison.Ordinal);
        Assert.Contains("static snapshotManifest => snapshotManifest.RollbackBackups", guardianPowerSource, StringComparison.Ordinal);
        Assert.Contains("relativePath => ReadRelativeFileBytesFromWorkspace(fs, relativePath)", guardianPowerSource, StringComparison.Ordinal);
    }

    [Fact]
    public void CurrentDetachedAuthority_MustNotFallbackToReadySignalPresence()
    {
        var path = Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "Services",
            "Validation",
            "ValidationService.AcceptedTurnAndInkFeathers.cs");
        var source = File.ReadAllText(path);

        Assert.DoesNotContain("|| _fs.FileExists(\"ready/turn_complete.json\")", source, StringComparison.Ordinal);
    }

    [Fact]
    public void PendingResolutionSnapshotRegistration_MustNotTreatRollbackBackupsAsAuthoritySignal()
    {
        var path = Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "Services",
            "Validation",
            "ValidationService.LifecycleControlAndStateFiles.cs");
        var source = File.ReadAllText(path);

        Assert.DoesNotContain("manifest.RollbackBackups.ContainsKey(relativePath)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void PendingResolutionContractRead_MustNotUseRawSnapshotEvidenceSignals()
    {
        var path = Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "Services",
            "Validation",
            "ValidationService.LifecycleControlAndStateFiles.cs");
        var source = File.ReadAllText(path);

        Assert.DoesNotContain("hasConventionalSnapshotCopy", source, StringComparison.Ordinal);
        Assert.DoesNotContain("hasRawManifestReference", source, StringComparison.Ordinal);
        Assert.DoesNotContain("hasDeletedCurrentRequestSnapshotResidue", source, StringComparison.Ordinal);
        Assert.DoesNotContain("rawManifestJson?.Contains(relativePath", source, StringComparison.Ordinal);
        Assert.DoesNotContain("hasParseableManifestBaselineEvidence", source, StringComparison.Ordinal);
        Assert.DoesNotContain("hasCorroboratedManifestBaselineEvidence", source, StringComparison.Ordinal);
        Assert.DoesNotContain("hasCorroboratedManifestSnapshotRegistration", source, StringComparison.Ordinal);
        Assert.Contains("hasDeletedCurrentRequestRecoveryBridgeCandidate", source, StringComparison.Ordinal);
    }

    [Fact]
    public void RepairReadyValidation_MustHonorStructuredDiagnosticOnlyRepairRequestMetadata()
    {
        var path = Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "Services",
            "Validation",
            "ValidationService.BootstrapAndProtocol.cs");
        var source = File.ReadAllText(path);

        Assert.Contains("metadataDiagnosticOnly", source, StringComparison.Ordinal);
        Assert.Contains("repair_ready_against_diagnostic_only_request", source, StringComparison.Ordinal);
        Assert.Contains("BuildInvalidRepairReadyRepairHint(requestJson, requireJsonObject: true)", source, StringComparison.Ordinal);
        Assert.Contains("BuildInvalidRepairReadyRepairHint(requestJson, requireJsonObject: false)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("gmInstructions.Contains(", source, StringComparison.Ordinal);
    }

    [Fact]
    public void StructuredActorExtraction_MustIncludeResidentUpdatesAndCanonicalResidentDiffs()
    {
        var path = Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "Services",
            "Validation",
            "ValidationService.NpcWorldAndMeta.cs");
        var source = File.ReadAllText(path);

        Assert.Contains("await CollectStructuredResidentUpdatesAsync(result.Updates);", source, StringComparison.Ordinal);
        Assert.Contains("ActorType = \"Resident\"", source, StringComparison.Ordinal);
        Assert.Contains("GuardianAbodeResidentState.UpdateProperty", source, StringComparison.Ordinal);
        Assert.Contains("GuardianAbodeResidentState.UpdateThoughtJournalProperty", source, StringComparison.Ordinal);
        Assert.Contains("GuardianAbodeResidentState.UpdateInteractionLogProperty", source, StringComparison.Ordinal);
        Assert.Contains("CollectResidentCanonicalDiffStructuredActorTouches", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ResidentLifecycleValidation_MustRequireCuratedMemoryForMeaningfulDevotionShifts()
    {
        var path = Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "Services",
            "Validation",
            "ValidationService.LifecycleControlAndStateFiles.cs");
        var source = File.ReadAllText(path);

        Assert.Contains("abode_resident_devotion_shift_missing_memory_update", source, StringComparison.Ordinal);
        Assert.Contains("ResidentHasNewThoughtOrInteractionMemory", source, StringComparison.Ordinal);
        Assert.Contains("Math.Abs(currentDevotionLevel - previousDevotionLevel) >= 8", source, StringComparison.Ordinal);
        Assert.Contains("Math.Abs(currentRestlessness - previousRestlessness) >= 8", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ResidentLifecycleValidation_MustEnforceCanonicalDriftTriggersAndProjection()
    {
        var validationPath = Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "Services",
            "Validation",
            "ValidationService.LifecycleControlAndStateFiles.cs");
        var residentStatePath = Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "Services",
            "GuardianAbodeResidentState.cs");

        var validationSource = File.ReadAllText(validationPath);
        var residentStateSource = File.ReadAllText(residentStatePath);

        Assert.Contains("abode_resident_devotion_shift_missing_canonical_trigger", validationSource, StringComparison.Ordinal);
        Assert.Contains("abode_resident_devotion_projection_mismatch", validationSource, StringComparison.Ordinal);
        Assert.Contains("GuardianAbodeResidentState.BuildCanonicalDriftContext(", validationSource, StringComparison.Ordinal);
        Assert.Contains("GuardianAbodeResidentState.ProjectCanonicalAbodeDrift(", validationSource, StringComparison.Ordinal);

        Assert.Contains("public sealed class ResidentAbodeDriftContext", residentStateSource, StringComparison.Ordinal);
        Assert.Contains("public sealed class ResidentAbodeDriftProjection", residentStateSource, StringComparison.Ordinal);
        Assert.Contains("BuildCanonicalDriftContext(", residentStateSource, StringComparison.Ordinal);
        Assert.Contains("ProjectCanonicalAbodeDrift(", residentStateSource, StringComparison.Ordinal);
    }

    [Fact]
    public void ResidentTransferValidation_MustRequireValidatedPreTurnEligibilityAndAcceptedTransferBypass()
    {
        var path = Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "Services",
            "Validation",
            "ValidationService.LifecycleControlAndStateFiles.cs");
        var source = File.ReadAllText(path);

        Assert.Contains("abode_resident_transfer_invalid_preturn_eligibility", source, StringComparison.Ordinal);
        Assert.Contains("TryResolveEligiblePreTurnTransferResident(", source, StringComparison.Ordinal);
        Assert.Contains("CollectAcceptedTransferArrivalResidentIds(", source, StringComparison.Ordinal);
        Assert.Contains("!acceptedTransferArrivalResidentIds.Contains(residentId)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ResidentTransferCompetitionMetadata_MustBeValidatedAndAllowedByLifecycle()
    {
        var lifecyclePath = Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "Services",
            "Validation",
            "ValidationService.LifecycleControlAndStateFiles.cs");
        var guardiansPath = Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "Services",
            "Validation",
            "ValidationService.GuardiansAndAfterlife.cs");
        var lifecycleSource = File.ReadAllText(lifecyclePath);
        var guardiansSource = File.ReadAllText(guardiansPath);

        Assert.Contains("\"selectionMode\", \"competitionScore\", \"competitionLabel\", \"competitionReason\"", lifecycleSource, StringComparison.Ordinal);
        Assert.Contains("pending_abode_resident_transfer_invalid_selection_mode", guardiansSource, StringComparison.Ordinal);
        Assert.Contains("pending_abode_resident_transfer_invalid_competition_label", guardiansSource, StringComparison.Ordinal);
        Assert.Contains("pending_abode_resident_transfer_inconsistent_selection_metadata", guardiansSource, StringComparison.Ordinal);
    }

    [Fact]
    public void CompanionSeedValidation_MustHonorResidentPersonalityAndAbodeSnapshotFields()
    {
        var path = Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "Services",
            "Validation",
            "ValidationService.QuestsRivalsFactionsAndWorld.cs");
        var source = File.ReadAllText(path);

        Assert.Contains("ValidateResidentCompanionSnapshotFields(companionSeed, $\"{context}.companionSeed\", issues, section);", source, StringComparison.Ordinal);
        Assert.Contains("companion_seed_invalid_power_sensitivity", source, StringComparison.Ordinal);
        Assert.Contains("companion_seed_abode_devotion_tier_mismatch", source, StringComparison.Ordinal);
    }

    [Fact]
    public void PendingManifestationRequestValidation_MustHonorResidentPersonalityAndAbodeSnapshotFields()
    {
        var path = Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "Services",
            "Validation",
            "ValidationService.GuardiansAndAfterlife.cs");
        var source = File.ReadAllText(path);

        Assert.Contains("ValidateResidentCompanionSnapshotFields(request, requestContext, issues);", source, StringComparison.Ordinal);
        Assert.Contains("ValidateResidentPersonalityProfileObject(personalityProfile", source, StringComparison.Ordinal);
        Assert.Contains("ValidateResidentAbodeDispositionObject(abodeDisposition", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ShiningPendingRequestValidation_MustAcceptRussianRealmAlias()
    {
        var path = Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "Services",
            "Validation",
            "ValidationService.ShiningAbode.cs");
        var source = File.ReadAllText(path);

        Assert.Contains("IsSupportedShiningRealm", source, StringComparison.Ordinal);
        Assert.Contains("Сияющая Обитель", source, StringComparison.Ordinal);
        Assert.DoesNotContain("if (!string.Equals(currentRealm, \"Shining Abode\", StringComparison.OrdinalIgnoreCase) ||", source, StringComparison.Ordinal);
    }

    [Fact]
    public void PendingTurnAuthorityConsumers_MustUseHandleBoundFileSystemReads()
    {
        var normalizerSource = File.ReadAllText(Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "Services",
            "CanonicalStateNormalizer",
            "CanonicalStateNormalizer.SoulAndMeta.cs"));
        var guardianSource = File.ReadAllText(Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "Services",
            "GuardianPowerEventState.cs"));
        var acceptedTurnSource = File.ReadAllText(Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "Services",
            "Validation",
            "ValidationService.AcceptedTurnAndInkFeathers.cs"));

        Assert.DoesNotContain(
            "return File.ReadAllBytes(fullPath);",
            normalizerSource,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "return File.ReadAllBytes(fullPath);",
            guardianSource,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "return File.ReadAllBytes(fullPath);",
            acceptedTurnSource,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "File.ReadAllText(manifestPath)",
            acceptedTurnSource,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "File.ReadAllText(authorityPath)",
            acceptedTurnSource,
            StringComparison.Ordinal);
    }

    [Fact]
    public void CanonicalShiningVisibility_MustFailClosedWithoutRevealed()
    {
        var source = File.ReadAllText(Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "Services",
            "SarefMainStoryState.cs"));

        Assert.Contains(
            "public static bool IsPlayerVisibleShiningFaction(JsonObject? faction)",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "FactionVisibilityRevealed",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "return !IsHiddenWingsFaction(faction);",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "if (IsPlayerVisibleShiningFaction(faction))",
            source,
            StringComparison.Ordinal);
    }
}
