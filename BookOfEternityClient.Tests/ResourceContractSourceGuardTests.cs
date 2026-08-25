using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ResourceContractSourceGuardTests
{
    private static readonly string[] RemovedCommandTokens =
    {
        "currentHealthChange",
        "currentEnergyChange",
        "currentPoiseChange",
        "inventoryItemsResources",
        "NPCInventoryResourcesChanges",
        "game_state/inventory/item_resources.json"
    };

    private static readonly string[] RemovedCanonicalMemberNames =
    {
        "healthPercentage",
        "energyPercentage",
        "poisePercentage",
        "currentHealth",
        "maxHealth",
        "currentEnergy",
        "maxEnergy",
        "currentPoise",
        "maxPoise",
        "healthStates",
        "currentHealthPercentage",
        "maxHealthPercentage",
        "maxHp",
        "maxDurability",
        "maximumResource",
        "newResourceValue",
        "chargesPerReturn",
        "chargesUsedThisReturn",
        "actionEconomy"
    };

    private static readonly Regex RemovedBareAuthorityPattern = new(
        "(?<![A-Za-z0-9_])(?:currentHealthChange|currentEnergyChange|currentPoiseChange|" +
        "inventoryItemsResources|NPCInventoryResourcesChanges|healthPercentage|" +
        "energyPercentage|poisePercentage|currentHealth|maxHealth|currentEnergy|maxEnergy|" +
        "currentPoise|maxPoise|" +
        "healthStates|currentHealthPercentage|maxHealthPercentage|maxHp|maxDurability|" +
        "maximumResource|newResourceValue|chargesPerReturn|chargesUsedThisReturn|" +
        "actionEconomy|game_state/inventory/item_resources\\.json)(?![A-Za-z0-9_])",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex RemovedCanonicalWritePattern = new(
        "\\[\\s*[\\\"'](?:" + string.Join('|', RemovedCanonicalMemberNames) +
        ")[\\\"']\\s*\\]\\s*(?:=|\\?\\?=)",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex RemovedCanonicalReadPattern = new(
        "(?:\\[\\s*[\\\"'](?:" + string.Join('|', RemovedCanonicalMemberNames) +
        ")[\\\"']\\s*\\]|TryGetProperty\\(\\s*[\\\"'](?:" +
        string.Join('|', RemovedCanonicalMemberNames) +
        ")[\\\"']|GetNode(?:String|Int|Decimal|Bool|Object|Array)?\\([^\\r\\n;]*[\\\"'](?:" +
        string.Join('|', RemovedCanonicalMemberNames) + ")[\\\"'])",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex RemovedPositiveValidatorPattern = new(
        "(?:Validate|Require)(?:Percentage|Number|NonNegative|Positive|Int|Decimal|Numeric|Field)" +
        "[A-Za-z]*\\([^\\r\\n;]*[\\\"'](?:" + string.Join('|', RemovedCanonicalMemberNames) +
        ")[\\\"']",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex RemovedCompatibilityMutationPattern = new(
        "(?:\\.Remove\\(\\s*[\\\"'](?:" + string.Join('|', RemovedCanonicalMemberNames) +
        ")[\\\"']\\s*\\)|StripLegacyShiningGachaCounters)",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex RemovedPersistedPropertyPattern = new(
        "(?:(?:[\\\"'`])(?:healthPercentage|energyPercentage|poisePercentage|" +
        "currentHealth|maxHealth|currentEnergy|maxEnergy|currentPoise|maxPoise|healthStates|" +
        "currentHealthPercentage|maxHealthPercentage|durability|maxDurability|" +
        "chargesPerReturn|chargesUsedThisReturn|rerolls|rerollsSpent|actionEconomy)" +
        "(?:[\\\"'`])|^\\s*(?:[-*]\\s*)?(?:healthPercentage|energyPercentage|" +
        "poisePercentage|currentHealth|maxHealth|currentEnergy|maxEnergy|currentPoise|maxPoise|healthStates|" +
        "currentHealthPercentage|maxHealthPercentage|durability|maxDurability|" +
        "chargesPerReturn|chargesUsedThisReturn|rerolls|rerollsSpent|actionEconomy))\\s*:",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex RemovedItemMirrorInstructionPattern = new(
        "(?:write\\s+durability\\s+as\\s+a\\s+percentage\\s+string|" +
        "new\\s+[\\\"']?durability[\\\"']?\\s+value|" +
        "durability\\s+(?:becomes|is\\s+set\\s+to))",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex RemovedItemPropertyPattern = new(
        "(?:(?:[\\\"'`])(?:resource|maximumResource|resourceType|newResourceValue)" +
        "(?:[\\\"'`])|^\\s*(?:[-*]\\s*)?(?:resource|maximumResource|resourceType|" +
        "newResourceValue))\\s*:",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly string[] ActiveContractAndExamplePaths =
    {
        "CLI_API_Specification.md",
        "CLI_Agent_Daemon_Specification.md",
        "BookOfEternityClient/Launcher/CLI_Launch_Script.md",
        "TaskGuides/CLI_Step_Main.txt",
        "BookOfEternityClient/game_master_daemon.ps1",
        "Rules/Block_0.txt",
        "Rules/Block_0.5.txt",
        "Rules/Block_0.6.txt",
        "Rules/Block_2.txt",
        "Rules/Block_2.5.txt",
        "Rules/Block_2.6.txt",
        "Rules/Block_5.txt",
        "Rules/Block_6.txt",
        "Rules/Block_7.txt",
        "Rules/Block_9.txt",
        "Rules/Block_9_Universal_Tool_Functions.txt",
        "Rules/Block_10.txt",
        "Rules/Block_11.txt",
        "Rules/Block_12.txt",
        "Rules/Block_13.txt",
        "Rules/Block_14.txt",
        "Rules/Block_15.txt",
        "Rules/Block_15.A.txt",
        "Rules/Block_17.txt",
        "Rules/Block_19.txt",
        "Rules/Block_19.A.txt",
        "Rules/Block_21.txt",
        "Rules/Block_32_Guardians.txt",
        "Rules/Block_CLI_QTE.txt",
        "Rules/Block_CLI_Operations.txt",
        "Rules/Block_FINAL.txt",
        "OtherGuides/Afterlife_Contract_Matrix.md",
        "OtherGuides/Afterlife_Combat_Terminology_Glossary.md",
        "Examples/CLI_Translation_Guide.md",
        "Examples/E_Block_2.5.txt",
        "Examples/E_Block_5.txt",
        "Examples/E_Block_6.txt",
        "Examples/E_Block_9.txt",
        "Examples/E_Block_9_Updated.txt",
        "Examples/E_Block_10.txt",
        "Examples/E_Block_10.V.txt",
        "Examples/E_Block_12.txt",
        "Examples/E_Block_13.txt",
        "Examples/E_Block_15.A.txt",
        "Examples/E_Block_16.txt",
        "Examples/E_Block_17.txt",
        "Examples/E_Block_19.txt",
        "Examples/E_Block_19.A.txt",
        "Examples/E_Block_21.txt",
        "Examples/E_Block_32.txt",
        "Examples/E_CLI_Afterlife_Turns.txt",
        "Examples/E_CLI_Mortal_Resources.txt",
        "Examples/E_CLI_Mortal_Item_Materialization.txt",
        "Examples/E_CLI_NPC_Trade.txt",
        "Examples/E_CLI_QTE_Offer.txt",
        "Examples/E_CLI_Step_Main.txt",
        "Examples/E_Soul_Relic_Integration.txt"
    };

    private static readonly string[] ActiveProductionConsumerPaths =
    {
        "Core/StateManager.cs",
        "Models/GameState/AggregatedGameState.cs",
        "UI/GameInterface.cs",
        "UI/ExplorerUniversalMetaCommandResultBuilder.cs",
        "UI/ExplorerMortalWorldCommandResultBuilder.cs",
        "UI/ExplorerLifecycleLocalTurnCommandResultBuilder.cs",
        "UI/ExplorerAfterlifeCombatCommandResultBuilder.cs",
        "UI/ExplorerShiningAbodeCommandResultBuilder.cs",
        "UI/ExplorerMode/ExplorerMode.WorldAndStatus.cs",
        "UI/ExplorerMode/ExplorerMode.MetaStoryAndStatus.cs",
        "UI/ExplorerMode/ExplorerMode.MetaLoreAndTravel.cs",
        "UI/ExplorerMode/ExplorerMode.Inventory.cs",
        "UI/ExplorerMode/ExplorerMode.Npcs.ListAndDetails.cs",
        "UI/ExplorerMode/ExplorerMode.Npcs.Rendering.cs",
        "UI/ExplorerMode/ExplorerMode.Afterlife.SpiritualConflict.cs",
        "UI/ExplorerMode/ExplorerMode.Afterlife.GuardiansProjectsTrade.cs",
        "UI/ExplorerMode/ExplorerMode.Afterlife.PlayerGuardianFoundation.cs",
        "UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.ActionPreviews.cs",
        "UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.Actions.cs",
        "UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.Gates.cs",
        "UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs",
        "UI/ExplorerMode/ExplorerMode.Afterlife.StatusAudit.cs",
        "WebUi/BrowserGameScreenService.cs",
        "Services/InventoryEquipmentService.cs",
        "Services/NpcTradeService.cs",
        "Services/StorageTransportMoveService.cs",
        "Services/AfterlifeSpiritualConflictTurnPreviewService.cs",
        "Core/GameEngine/GameEngine.AgentConsole.cs",
        "Core/GameEngine/GameEngine.TurnLifecycle.cs"
    };

    [Fact]
    public void ShiningBlessingPersistence_DoesNotReviveNumericRerollMirrors()
    {
        var effectSource = ReadProductionSource(
            "Services",
            "ShiningBlessingEffectState.cs");
        var transientOnly = new[]
        {
            "private static JsonObject BuildPendingEffectsFromPreparedPackage",
            "private static IReadOnlyList<string> BuildActivationSummaryLines",
            "private static List<string> BuildDirectiveLines"
        };
        foreach (var signature in transientOnly)
            effectSource = effectSource.Replace(
                ExtractMethodSource(effectSource, signature),
                string.Empty,
                StringComparison.Ordinal);

        Assert.DoesNotContain("[\"rerolls\"]", effectSource, StringComparison.Ordinal);
        Assert.DoesNotContain("[\"rerollsSpent\"]", effectSource, StringComparison.Ordinal);

        var resourceSource = ReadProductionSource(
            "Services",
            "ShiningBlessingRerollResourceService.cs");
        Assert.Contains(
            "ShiningBlessingRerollAllocationContract.TryConsume(memory",
            resourceSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "ShiningBlessingRerollAllocationContract.TryConsume(relic",
            resourceSource,
            StringComparison.Ordinal);
        Assert.Contains("shining_blessing_reroll_allocation_invalid", resourceSource, StringComparison.Ordinal);
        Assert.DoesNotContain("StripNumericMirror", resourceSource, StringComparison.Ordinal);
        Assert.Contains("rerollResourceBinding", resourceSource, StringComparison.Ordinal);

        var validatorSource = ReadProductionSource(
            "Services",
            "Validation",
            "ValidationService.AfterlifeArchiveTradeAndLifecycle.cs");
        Assert.Contains(
            "pending_shining_blessings_numeric_reroll_mirror_forbidden",
            validatorSource,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("money")]
    [InlineData("ink_feathers")]
    [InlineData("light_sparks")]
    [InlineData("treasury_balance")]
    [InlineData("faction_resource_ledger")]
    [InlineData("experience")]
    [InlineData("mastery")]
    [InlineData("level")]
    [InlineData("reputation")]
    [InlineData("relationship")]
    [InlineData("owner_bond_level")]
    [InlineData("spiritual_power")]
    [InlineData("spiritual_strain")]
    [InlineData("spiritual_shield")]
    [InlineData("effect_stacks")]
    [InlineData("effect_uses")]
    [InlineData("effect_duration")]
    [InlineData("turns_remaining")]
    [InlineData("qte_progress")]
    [InlineData("qte_mistakes")]
    [InlineData("qte_noise")]
    [InlineData("lock_pin_durability")]
    [InlineData("item_stack_count")]
    [InlineData("project_fuel")]
    [InlineData("free_shape")]
    [InlineData("free_retune")]
    public void DefinitionAdmission_RejectsReservedOutOfScopeMechanicalFamilies(
        string resourceKey)
    {
        var proposal = CreateSettingResourceProposal(resourceKey);
        using var document = JsonDocument.Parse(proposal.ToJsonString());

        var result = ResourceDefinitionCatalog.MaterializeProposal(
            document.RootElement,
            ResourceDefinitionCatalog.CreateBuiltIn(),
            createdAtTurn: 42,
            createdEventRef: "turn_42",
            allocateIdentity: static () => new ResourceDefinitionIdentity(
                "resource_definition_out_of_scope",
                "resource_definition_seal_out_of_scope"));

        Assert.Null(result.Definition);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "resource_definition_out_of_scope_key" &&
            issue.FilePath.EndsWith(".resourceKey", StringComparison.Ordinal));
    }

    [Fact]
    public void DefinitionAdmission_RetainsExplicitSettingDefinedReserveRoute()
    {
        var proposal = CreateSettingResourceProposal("mana");
        using var document = JsonDocument.Parse(proposal.ToJsonString());

        var result = ResourceDefinitionCatalog.MaterializeProposal(
            document.RootElement,
            ResourceDefinitionCatalog.CreateBuiltIn(),
            createdAtTurn: 42,
            createdEventRef: "turn_42",
            allocateIdentity: static () => new ResourceDefinitionIdentity(
                "resource_definition_mana_guard",
                "resource_definition_seal_mana_guard"));

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Issues));
        Assert.Equal("mana", result.Definition!.ResourceKey);
    }

    [Fact]
    public void ProductionResponseAndMappingExposeOnlyCommonResourceCommands()
    {
        var response = ReadProductionSource("Models", "GameResponse.cs");
        var mapping = ReadProductionSource("Configuration", "FileMapping.cs");
        var combined = response + Environment.NewLine + mapping;

        foreach (var removed in new[]
                 {
                     "currentHealthChange",
                     "currentEnergyChange",
                     "currentPoiseChange",
                     "inventoryItemsResources",
                     "NPCInventoryResourcesChanges"
                 })
        {
            Assert.DoesNotContain(removed, combined, StringComparison.Ordinal);
        }

        foreach (var accepted in new[]
                 {
                     "resourceDefinitionCreations",
                     "resourceCapacityChanges",
                     "resourceChanges"
                 })
        {
            Assert.Contains(accepted, response, StringComparison.Ordinal);
            Assert.Contains(accepted, mapping, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ProductionContainsNoLegacyShiningAttemptReader()
    {
        var source = ReadProductionSource("Services", "ShiningAbodeState.Gacha.cs");

        Assert.DoesNotContain(
            "GetRemainingShiningGachaCharges",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "GetNodeInt(gachaSystem[\"chargesPerReturn\"]",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "GetNodeInt(gachaSystem[\"chargesUsedThisReturn\"]",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionContainsNoLegacyResourceCompatibilityNormalization()
    {
        var violations = EnumerateProductionSources()
            .SelectMany(path => FindPatternViolations(
                path,
                RemovedCompatibilityMutationPattern))
            .ToArray();

        Assert.True(
            violations.Length == 0,
            "Legacy resource fields are still silently stripped or normalized:" +
            Environment.NewLine + string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void ProductionContainsNoRemovedCanonicalResourceWritersOrPositiveValidators()
    {
        var violations = EnumerateProductionSources()
            .SelectMany(path =>
                FindPatternViolations(path, RemovedCanonicalWritePattern)
                    .Concat(FindPatternViolations(path, RemovedPositiveValidatorPattern)))
            .ToArray();

        Assert.True(
            violations.Length == 0,
            "Removed resource authority is still written or positively validated:" +
            Environment.NewLine + string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void ProductionConsumersContainNoRemovedCanonicalResourceReaders()
    {
        var violations = ActiveProductionConsumerPaths
            .Select(path => Path.Combine("BookOfEternityClient", path))
            .SelectMany(path => FindPatternViolations(path, RemovedCanonicalReadPattern))
            .ToArray();

        Assert.True(
            violations.Length == 0,
            "A player/GM consumer still reads removed resource authority:" +
            Environment.NewLine + string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void LegacyItemResourceSidecarAppearsOnlyInTheIncompatibleSaveDetector()
    {
        var occurrences = EnumerateProductionSources()
            .SelectMany(path => FindLiteralViolations(
                path,
                "game_state/inventory/item_resources.json"))
            .ToArray();

        var occurrence = Assert.Single(occurrences);
        Assert.StartsWith(
            "BookOfEternityClient/Services/Validation/ValidationService.PlayerAndInventory.cs:",
            occurrence,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("CLI_API_Specification.md")]
    [InlineData("CLI_Agent_Daemon_Specification.md")]
    [InlineData("TaskGuides/CLI_Step_Main.txt")]
    [InlineData("Rules/Block_0.5.txt")]
    [InlineData("Rules/Block_0.6.txt")]
    [InlineData("Rules/Block_2.txt")]
    [InlineData("Rules/Block_5.txt")]
    [InlineData("Rules/Block_6.txt")]
    [InlineData("Rules/Block_7.txt")]
    [InlineData("Rules/Block_9.txt")]
    [InlineData("Rules/Block_10.txt")]
    [InlineData("Rules/Block_12.txt")]
    [InlineData("Rules/Block_13.txt")]
    [InlineData("Rules/Block_14.txt")]
    [InlineData("Rules/Block_15.txt")]
    [InlineData("Rules/Block_15.A.txt")]
    [InlineData("Rules/Block_17.txt")]
    [InlineData("Rules/Block_32_Guardians.txt")]
    [InlineData("Rules/Block_CLI_QTE.txt")]
    [InlineData("Rules/Block_CLI_Operations.txt")]
    [InlineData("Rules/Block_FINAL.txt")]
    public void ActiveGmContracts_DoNotAuthorizeRemovedResourceFields(string relativePath)
    {
        var source = File.ReadAllText(Path.Combine(
            TestRepoPaths.RepoRoot,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        var violations = source
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select((line, index) => (Line: line, Number: index + 1))
            .Where(entry => RemovedBareAuthorityPattern.IsMatch(entry.Line))
            .Where(entry => !IsExplicitRejectionLine(entry.Line))
            .Select(entry => $"{relativePath}:{entry.Number}: {entry.Line.Trim()}")
            .ToArray();

        Assert.True(
            violations.Length == 0,
            "Removed resource authority remains active:" + Environment.NewLine +
            string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void CompleteActiveContractsExamplesAndTemplatesContainNoRemovedResourceAuthority()
    {
        var paths = ActiveContractAndExamplePaths
            .Concat(Directory.EnumerateFiles(
                    Path.Combine(TestRepoPaths.RepoRoot, "Rules"),
                    "Block_*.txt",
                    SearchOption.TopDirectoryOnly)
                .Select(path => Path.GetRelativePath(TestRepoPaths.RepoRoot, path)))
            .Concat(Directory.EnumerateFiles(
                    Path.Combine(TestRepoPaths.RepoRoot, "Examples"),
                    "E_*.txt",
                    SearchOption.TopDirectoryOnly)
                .Select(path => Path.GetRelativePath(TestRepoPaths.RepoRoot, path)))
            .Concat(Directory.EnumerateFiles(
                    Path.Combine(TestRepoPaths.RepoRoot, "FileSystemExample", "game_session", "game_state"),
                    "*.json",
                    SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(TestRepoPaths.RepoRoot, path)))
            .Concat(Directory.EnumerateFiles(
                    Path.Combine(TestRepoPaths.RepoRoot, "FileSystemExample", "validator_fixtures"),
                    "*.json",
                    SearchOption.AllDirectories)
                .Where(path => path
                    .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    .Any(segment =>
                        string.Equals(segment, "fixed", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(segment, "shared", StringComparison.OrdinalIgnoreCase)))
                .Select(path => Path.GetRelativePath(TestRepoPaths.RepoRoot, path)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var violations = paths
            .Where(path => File.Exists(Path.Combine(
                TestRepoPaths.RepoRoot,
                path.Replace('/', Path.DirectorySeparatorChar))))
            .SelectMany(FindRemovedAuthorityViolations)
            .ToArray();

        Assert.True(
            violations.Length == 0,
            "Removed resource authority remains in an active contract/example/template:" +
            Environment.NewLine + string.Join(Environment.NewLine, violations));
    }

    private static JsonObject CreateSettingResourceProposal(string resourceKey) => new()
    {
        ["resourceKey"] = resourceKey,
        ["definitionVersion"] = 1,
        ["displayName"] = resourceKey,
        ["numericKind"] = "integer",
        ["unit"] = "point",
        ["quantum"] = 1,
        ["minimumPolicy"] = new JsonObject
        {
            ["kind"] = "definition_fixed",
            ["value"] = 0
        },
        ["capacityPolicy"] = new JsonObject
        {
            ["kind"] = "instance_fixed"
        },
        ["initializationPolicy"] = new JsonObject
        {
            ["kind"] = "maximum"
        },
        ["allowedOwnerKinds"] = new JsonArray("player"),
        ["allowedOperations"] = new JsonArray("spend", "gain"),
        ["defaultFloorPolicy"] = "reject_below_minimum",
        ["defaultCapPolicy"] = "clamp_to_maximum",
        ["visibility"] = "player_visible"
    };

    private static string ReadProductionSource(params string[] relativeParts)
    {
        var parts = new[] { TestRepoPaths.RepoRoot, "BookOfEternityClient" }
            .Concat(relativeParts)
            .ToArray();
        return File.ReadAllText(Path.Combine(parts));
    }

    private static IEnumerable<string> EnumerateProductionSources()
    {
        var productionRoot = Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient");
        return Directory
            .EnumerateFiles(productionRoot, "*", SearchOption.AllDirectories)
            .Where(path =>
                path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase))
            .Where(path =>
                !path.Contains(
                    $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                    StringComparison.OrdinalIgnoreCase) &&
                !path.Contains(
                    $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                    StringComparison.OrdinalIgnoreCase))
            .Select(path => Path
                .GetRelativePath(TestRepoPaths.RepoRoot, path)
                .Replace(Path.DirectorySeparatorChar, '/'));
    }

    private static IEnumerable<string> FindPatternViolations(
        string relativePath,
        Regex pattern)
    {
        var absolutePath = Path.Combine(
            TestRepoPaths.RepoRoot,
            relativePath.Replace('/', Path.DirectorySeparatorChar));
        return File.ReadAllLines(absolutePath)
            .Select((line, index) => (Line: line, Number: index + 1))
            .Where(entry => pattern.IsMatch(entry.Line))
            .Select(entry => $"{relativePath}:{entry.Number}: {entry.Line.Trim()}");
    }

    private static IEnumerable<string> FindLiteralViolations(
        string relativePath,
        string token)
    {
        var absolutePath = Path.Combine(
            TestRepoPaths.RepoRoot,
            relativePath.Replace('/', Path.DirectorySeparatorChar));
        return File.ReadAllLines(absolutePath)
            .Select((line, index) => (Line: line, Number: index + 1))
            .Where(entry => entry.Line.Contains(token, StringComparison.Ordinal))
            .Select(entry => $"{relativePath}:{entry.Number}: {entry.Line.Trim()}");
    }

    private static bool IsExplicitRejectionLine(string line) =>
        line.Contains("forbidden", StringComparison.OrdinalIgnoreCase) ||
        line.Contains("legacy", StringComparison.OrdinalIgnoreCase) ||
        line.Contains("removed resource authority", StringComparison.OrdinalIgnoreCase) ||
        line.Contains("removed resource field", StringComparison.OrdinalIgnoreCase) ||
        line.Contains("removed command field", StringComparison.OrdinalIgnoreCase) ||
        line.Contains("incompatible", StringComparison.OrdinalIgnoreCase) ||
        line.Contains("illegal", StringComparison.OrdinalIgnoreCase) ||
        line.Contains("never author", StringComparison.OrdinalIgnoreCase) ||
        line.Contains("never a legal", StringComparison.OrdinalIgnoreCase) ||
        line.Contains("not a legal", StringComparison.OrdinalIgnoreCase) ||
        line.Contains("do not author", StringComparison.OrdinalIgnoreCase) ||
        line.Contains("запрещ", StringComparison.OrdinalIgnoreCase) ||
        line.Contains("не созда", StringComparison.OrdinalIgnoreCase) ||
        line.Contains("не пиши", StringComparison.OrdinalIgnoreCase) ||
        line.Contains("не автор", StringComparison.OrdinalIgnoreCase) ||
        line.Contains("удали", StringComparison.OrdinalIgnoreCase) ||
        line.Contains("✗", StringComparison.Ordinal);

    private static IEnumerable<string> FindRemovedAuthorityViolations(string relativePath)
    {
        var absolutePath = Path.Combine(
            TestRepoPaths.RepoRoot,
            relativePath.Replace('/', Path.DirectorySeparatorChar));
        return File.ReadAllText(absolutePath)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select((line, index) => (Line: line, Number: index + 1))
            .Where(entry =>
                RemovedCommandTokens.Any(token =>
                    entry.Line.Contains(token, StringComparison.Ordinal)) ||
                RemovedBareAuthorityPattern.IsMatch(entry.Line) ||
                RemovedPersistedPropertyPattern.IsMatch(entry.Line) ||
                RemovedItemMirrorInstructionPattern.IsMatch(entry.Line) ||
                IsItemAuthorityPath(relativePath) &&
                RemovedItemPropertyPattern.IsMatch(entry.Line))
            .Where(entry => !IsExplicitRejectionLine(entry.Line))
            .Select(entry =>
                $"{relativePath}:{entry.Number}: {entry.Line.Trim()}");
    }

    private static bool IsItemAuthorityPath(string relativePath)
    {
        var normalized = relativePath.Replace('\\', '/');
        var fileName = normalized[(normalized.LastIndexOf('/') + 1)..];
        return fileName.Equals("Block_2.txt", StringComparison.OrdinalIgnoreCase) ||
               fileName.Equals("Block_2.5.txt", StringComparison.OrdinalIgnoreCase) ||
               fileName.Equals("Block_5.txt", StringComparison.OrdinalIgnoreCase) ||
               fileName.StartsWith("Block_9", StringComparison.OrdinalIgnoreCase) ||
               fileName.Equals("Block_10.txt", StringComparison.OrdinalIgnoreCase) ||
               fileName.Equals("Block_11.txt", StringComparison.OrdinalIgnoreCase) ||
               fileName.Equals("Block_19.A.txt", StringComparison.OrdinalIgnoreCase) ||
               fileName.Equals("E_Block_2.5.txt", StringComparison.OrdinalIgnoreCase) ||
               fileName.Equals("E_Block_5.txt", StringComparison.OrdinalIgnoreCase) ||
               fileName.StartsWith("E_Block_9", StringComparison.OrdinalIgnoreCase) ||
               fileName.StartsWith("E_Block_10", StringComparison.OrdinalIgnoreCase) ||
               fileName.Equals("E_Block_19.A.txt", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("Mortal_Item", StringComparison.OrdinalIgnoreCase) ||
               normalized.EndsWith(
                   "game_state/inventory/items.json",
                   StringComparison.OrdinalIgnoreCase);
    }

    private static string ExtractMethodSource(string source, string signature)
    {
        var start = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Could not find method signature '{signature}'.");
        var openBrace = source.IndexOf('{', start);
        Assert.True(openBrace >= 0, $"Could not find method body for '{signature}'.");

        var depth = 0;
        for (var index = openBrace; index < source.Length; index++)
        {
            if (source[index] == '{')
                depth++;
            else if (source[index] == '}')
                depth--;

            if (depth == 0)
                return source[start..(index + 1)];
        }

        Assert.Fail($"Could not extract method body for '{signature}'.");
        return string.Empty;
    }
}
