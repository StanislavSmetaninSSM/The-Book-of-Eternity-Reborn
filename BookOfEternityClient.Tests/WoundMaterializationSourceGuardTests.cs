using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class WoundMaterializationSourceGuardTests
{
    private enum InventoryCategory
    {
        LegacyLooseWoundSurface,
        AcceptedMechanicsIntegrationSeam
    }

    private sealed record InventoryEntry(
        string Id,
        InventoryCategory Category,
        string RelativePath,
        params string[] RequiredAnchors);

    private sealed record DiscoveryAllowance(
        string Id,
        string InventoryId,
        string Token,
        string StartAnchor,
        string EndAnchor);

    private static readonly InventoryEntry[] Inventory =
    {
        new(
            "game-response-player-wound-command",
            InventoryCategory.LegacyLooseWoundSurface,
            "BookOfEternityClient/Models/GameResponse.cs",
            "[JsonPropertyName(\"playerWoundChanges\")]",
            "public JsonElement[]? PlayerWoundChanges { get; set; }"),
        new(
            "game-response-npc-wound-command",
            InventoryCategory.LegacyLooseWoundSurface,
            "BookOfEternityClient/Models/GameResponse.cs",
            "[JsonPropertyName(\"NPCWoundChanges\")]",
            "public JsonElement[]? NPCWoundChanges { get; set; }"),
        new(
            "player-wound-file-mapping",
            InventoryCategory.LegacyLooseWoundSurface,
            "BookOfEternityClient/Configuration/FileMapping.cs",
            "public static readonly Dictionary<string, string> FieldToFile",
            "[\"playerWoundChanges\"] = \"game_state/player/wounds.json\""),
        new(
            "npc-wound-file-mapping",
            InventoryCategory.LegacyLooseWoundSurface,
            "BookOfEternityClient/Configuration/FileMapping.cs",
            "public static readonly Dictionary<string, string> FieldToFile",
            "[\"NPCWoundChanges\"] = \"game_state/npcs/npc_effects.json\""),
        new(
            "generic-response-file-distribution",
            InventoryCategory.LegacyLooseWoundSurface,
            "BookOfEternityClient/IO/StateDistributor.cs",
            "private Dictionary<string, Dictionary<string, JsonElement>> CollectFileUpdates(GameResponse response)",
            "FileMapping.FieldToFile.TryGetValue(prop.Name, out var targetFile)",
            "result[targetFile][prop.Name] = prop.Value.Clone();",
            "private async Task MergeFieldsIntoFile("),
        new(
            "qte-terminal-response-wound-distribution-and-rollback",
            InventoryCategory.LegacyLooseWoundSurface,
            "BookOfEternityClient/Services/QteSceneService.cs",
            "internal static readonly IReadOnlyCollection<string> BrowserTransactionRollbackPaths",
            ".Concat(FileMapping.FieldToFile.Values)",
            "private GameResponse BuildTerminalOutcomeResponse(",
            "JsonSerializer.Deserialize<GameResponse>(responseFragment.ToJsonString(), JsonOpts)",
            "private async Task ApplyTerminalOutcomeStateChangesCoreAsync(",
            "await _stateDistributor.DistributeAsync(writeLease, response);",
            "private static HashSet<string> CollectQteTrackedPaths(GameResponse response)",
            "FileMapping.FieldToFile.TryGetValue(property.Name, out var targetPath)"),
        new(
            "player-loose-wound-validation",
            InventoryCategory.LegacyLooseWoundSurface,
            "BookOfEternityClient/Services/Validation/ValidationService.PlayerAndInventory.cs",
            "private async Task ValidatePlayerStateFiles(List<ValidationIssue> issues)",
            "private async Task ValidatePlayerFile(string filePath, List<ValidationIssue> issues)",
            "private void ValidateWoundsProperty(",
            "private void ValidateWoundsContainer(",
            "private void ValidateWoundObject(",
            "ValidateArrayItems(root, contextPrefix, issues, ValidateWoundObject);",
            "ValidateArrayItems(prop.Value, $\"{contextPrefix}.{prop.Name}\", issues, ValidateWoundObject);",
            "RequireAnyString(item, context, issues, \"woundName\", \"name\")",
            "item.TryGetProperty(\"generatedEffects\"",
            "item.TryGetProperty(\"healingState\""),
        new(
            "combatant-wound-reference-validation",
            InventoryCategory.LegacyLooseWoundSurface,
            "BookOfEternityClient/Services/Validation/ValidationService.PlayerAndInventory.cs",
            "private void ValidateCombatantActiveEffectObject(",
            "var isWoundReference = string.Equals(effectType, \"WoundReference\"",
            "effect.TryGetProperty(\"sourceWoundId\"",
            "duration != 999"),
        new(
            "npc-wound-identity-validation",
            InventoryCategory.LegacyLooseWoundSurface,
            "BookOfEternityClient/Services/Validation/ValidationService.NpcWorldAndMeta.cs",
            "private static readonly HashSet<string> NpcStructuredSingleActorSections",
            "private void ValidateNpcContract(",
            "ValidateNpcIdentityOnlyArray(root, contextPrefix, issues, \"NPCWoundChanges\")"),
        new(
            "npc-wound-state-file-validation",
            InventoryCategory.LegacyLooseWoundSurface,
            "BookOfEternityClient/Services/Validation/ValidationService.LifecycleControlAndStateFiles.cs",
            "private async Task ValidateNpcStateFiles(List<ValidationIssue> issues)",
            "await ValidateNpcFile(\"game_state/npcs/npc_effects.json\"",
            "\"NPCWoundChanges\", \"schemaVersion\", \"entries\""),
        new(
            "combatant-wound-reference-type-authority",
            InventoryCategory.LegacyLooseWoundSurface,
            "BookOfEternityClient/Services/Validation/ValidationService.PrivateImplementation.cs",
            "private static readonly HashSet<string> AllowedCombatantActiveEffectTypes",
            "\"WoundReference\""),
        new(
            "canonical-wound-effect-source-composition",
            InventoryCategory.AcceptedMechanicsIntegrationSeam,
            "BookOfEternityClient/Services/EffectAcceptedTurnInputComposer.cs",
            "internal const string PlayerWoundsPath = \"game_state/player/wounds.json\"",
            "private static CanonicalWoundSourceComposition CollectCanonicalWoundSources(",
            "var catalog = WoundCarrierCatalog.Build(",
            "Materializable: false",
            "private static CanonicalWoundSourceComposition ComposePreparedWoundSources(",
            "effect_source_wound_external_export_forbidden"),
        new(
            "generic-source-wound-healed-fallback",
            InventoryCategory.LegacyLooseWoundSurface,
            "BookOfEternityClient/Services/EffectAcceptedTurnInputComposer.cs",
            "private static bool IsSourceCurrentlyActive(",
            "case \"wound\":",
            "owner[\"isHealed\"]",
            "case \"fate_card\":"),
        new(
            "effect-source-wound-binding-validation",
            InventoryCategory.LegacyLooseWoundSurface,
            "BookOfEternityClient/Services/EffectSourceAuthority.cs",
            "builder.ValidateWoundBindings();",
            "internal void ValidateWoundBindings()",
            "\"wound_consequence\"",
            "\"effect_source_wound_link_missing\"",
            "\"effect_source_wound_source_bound_mismatch\""),
        new(
            "npc-effect-planner-wound-command-fallback",
            InventoryCategory.LegacyLooseWoundSurface,
            "BookOfEternityClient/Services/EffectAcceptedTurnPlanner.cs",
            "internal bool TryLocate(",
            "_npcs[\"NPCWoundChanges\"] is JsonArray",
            "_npcs[\"entries\"] = new JsonArray();"),
        new(
            "npc-effect-contract-wound-command-acceptance",
            InventoryCategory.LegacyLooseWoundSurface,
            "BookOfEternityClient/Services/EffectMaterializationContract.cs",
            "private static void ValidateNpcCarrier(",
            "Set(\"schemaVersion\", \"entries\", \"NPCWoundChanges\")",
            "adjacent-only NPCWoundChanges command surface"),
        new(
            "universal-status-raw-wound-reader",
            InventoryCategory.LegacyLooseWoundSurface,
            "BookOfEternityClient/UI/ExplorerUniversalMetaCommandResultBuilder.cs",
            "private static async Task<IReadOnlyList<UiAction>> AddMortalStatusDetailBlocks(",
            "await ReadJson(fs, \"game_state/player/wounds.json\")",
            "private static void AddMortalStatusWoundBlocks(",
            "wound[\"healingState\"]",
            "GetString(wound, \"woundName\"",
            "GetOptionalString(wound, \"descriptionOfEffects\")"),
        new(
            "explorer-status-raw-wound-reader",
            InventoryCategory.LegacyLooseWoundSurface,
            "BookOfEternityClient/UI/ExplorerMode/ExplorerMode.WorldAndStatus.cs",
            "private async Task ShowDetailedStatus()",
            "LoadGameStateFileAsync(\"game_state/player/wounds.json\")",
            "AppendStatusWoundPreview(extraText, wndDoc.RootElement)"),
        new(
            "explorer-status-wound-preview-renderer",
            InventoryCategory.LegacyLooseWoundSurface,
            "BookOfEternityClient/UI/ExplorerMode/ExplorerMode.PrivateImplementation.cs",
            "private static void AppendStatusWoundPreview(",
            "GetStr(item, \"woundName\", \"Рана\")",
            "GetStr(item, \"descriptionOfEffects\"",
            "item.TryGetProperty(\"healingState\""),
        new(
            "accepted-turn-authority-registry",
            InventoryCategory.AcceptedMechanicsIntegrationSeam,
            "BookOfEternityClient/Services/AcceptedTurnAuthorityRegistry.cs",
            "internal static class AcceptedTurnAuthorityRegistry",
            "internal static AcceptedMechanicsPlanningResult GetOrBuildCommonValidated(",
            "internal static bool TryTakeCommonValidated("),
        new(
            "accepted-mechanics-plan",
            InventoryCategory.AcceptedMechanicsIntegrationSeam,
            "BookOfEternityClient/Services/AcceptedMechanicsPlan.cs",
            "internal sealed class AcceptedMechanicsPlanBinding",
            "internal sealed class AcceptedMechanicsInput",
            "internal sealed class AcceptedMechanicsPlan",
            "internal AcceptedMechanicsPlan("),
        new(
            "accepted-mechanics-plan-cache",
            InventoryCategory.AcceptedMechanicsIntegrationSeam,
            "BookOfEternityClient/Services/AcceptedMechanicsPlanCache.cs",
            "internal sealed class AcceptedMechanicsPlanCache",
            "internal AcceptedMechanicsPlanningResult GetOrBuildValidated(",
            "internal bool TryTakeValidated(",
            "internal static class AcceptedMechanicsPlanAuthority"),
        new(
            "accepted-wound-plan-cache",
            InventoryCategory.AcceptedMechanicsIntegrationSeam,
            "BookOfEternityClient/Services/WoundAcceptedTurnPlanCache.cs",
            "internal sealed class WoundAcceptedTurnPlanCache",
            "internal WoundAcceptedTurnPreparationResult GetOrBuildPrepared(",
            "internal WoundAcceptedTurnPlanningResult GetOrBuildFinal(",
            "internal static class WoundAcceptedTurnPlanAuthority"),
        new(
            "accepted-mechanics-planner",
            InventoryCategory.AcceptedMechanicsIntegrationSeam,
            "BookOfEternityClient/Services/AcceptedMechanicsPlanner.cs",
            "internal static class AcceptedMechanicsPlanner",
            "internal static AcceptedMechanicsPlanningResult BuildAcceptedPlan(",
            "internal static AcceptedMechanicsResourcePlanningResult BuildResources("),
        new(
            "accepted-mechanics-validation-phase",
            InventoryCategory.AcceptedMechanicsIntegrationSeam,
            "BookOfEternityClient/Services/Validation/GameStateValidationPhase.cs",
            "internal enum GameStateValidationPhase : ulong",
            "AcceptedTurnEffectMaterializationCompleteness",
            "internal sealed class GameStateValidationSelection"),
        new(
            "accepted-mechanics-resource-validation",
            InventoryCategory.AcceptedMechanicsIntegrationSeam,
            "BookOfEternityClient/Services/Validation/ValidationService.ResourceMaterialization.cs",
            "ValidateAcceptedTurnRawResourceMaterializationAsync(",
            "var input = new AcceptedMechanicsInput(",
            "AcceptedMechanicsPlanAuthority.GetOrBuildValidated("),
        new(
            "accepted-mechanics-normalizer-publication",
            InventoryCategory.AcceptedMechanicsIntegrationSeam,
            "BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.AcceptedMechanics.cs",
            "internal async Task<AcceptedMechanicsPlan?> NormalizeAcceptedMechanicsAsync(",
            "private async Task<AcceptedMechanicsPlan?> PublishAcceptedMechanicsAsync(",
            "AcceptedMechanicsPlanAuthority.TryTakeValidated("),
        new(
            "accepted-mechanics-snapshot-and-rollback",
            InventoryCategory.AcceptedMechanicsIntegrationSeam,
            "BookOfEternityClient/Core/GameEngine/GameEngine.SessionAndSnapshots.cs",
            "private async Task<Dictionary<string, string>> CreateCanonicalBaselineSnapshotAsync(",
            "private IEnumerable<string> EnumerateRollbackTrackedFiles("),
        new(
            "accepted-mechanics-validation-and-repair",
            InventoryCategory.AcceptedMechanicsIntegrationSeam,
            "BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs",
            "private async Task<List<ValidationIssue>> CollectAcceptedTurnRawStateIssuesAsync()",
            "ValidateAcceptedTurnRawResourceMaterializationAsync()",
            "RefreshAcceptedTurnCanonicalStateForValidationAsync("),
        new(
            "accepted-effect-input-handshake",
            InventoryCategory.AcceptedMechanicsIntegrationSeam,
            "BookOfEternityClient/Services/EffectAcceptedTurnInputComposer.cs",
            "internal static EffectAcceptedTurnInput Compose(",
            "internal static EffectSourceAuthority BuildCanonicalSourceAuthority(",
            "internal static EffectTargetAuthority BuildCanonicalTargetAuthority("),
        new(
            "accepted-effect-source-authority-handshake",
            InventoryCategory.AcceptedMechanicsIntegrationSeam,
            "BookOfEternityClient/Services/EffectSourceAuthority.cs",
            "internal sealed class EffectSourceAuthority",
            "internal static EffectSourceAuthority Build(",
            "internal EffectSourceResolution ResolveCanonicalBinding(",
            "internal IReadOnlyList<ValidationIssue> ValidateCanonicalParameters("),
        new(
            "accepted-effect-carrier-catalog",
            InventoryCategory.AcceptedMechanicsIntegrationSeam,
            "BookOfEternityClient/Services/EffectCarrierCatalog.cs",
            "internal sealed class EffectCarrierCatalog",
            "internal const string NpcPath = \"game_state/npcs/npc_effects.json\"",
            "internal static EffectCarrierCatalog Build(",
            "internal void ScanNpcs("),
        new(
            "accepted-wound-carrier-catalog",
            InventoryCategory.AcceptedMechanicsIntegrationSeam,
            "BookOfEternityClient/Services/WoundCarrierCatalog.cs",
            "internal sealed class WoundCarrierCatalog",
            "internal const string PlayerPath = \"game_state/player/wounds.json\"",
            "internal const string NpcPath = \"game_state/npcs/npc_wounds.json\"",
            "internal static WoundCarrierCatalog Build(",
            "internal void ScanNpcs("),
        new(
            "accepted-wound-consequence-component-profile",
            InventoryCategory.AcceptedMechanicsIntegrationSeam,
            "BookOfEternityClient/Services/EffectComponentProfiles.cs",
            "internal static class EffectComponentProfiles",
            "[\"wound_consequence\"] = Descriptor(",
            "private static void ValidateWoundConsequence(",
            "RequireExactIdentifier(payload, path, \"woundId\", issues)"),
    };

    private static readonly string[] DiscoveryTokens =
    {
        "playerWoundChanges",
        "PlayerWoundChanges",
        "NPCWoundChanges",
        "WoundReference",
        "sourceWoundId",
        "generatedEffects",
        "game_state/player/wounds.json",
        "game_state/npcs/npc_effects.json",
        "woundName",
        "healingState",
        "descriptionOfEffects",
        "isHealed",
        "FileMapping.FieldToFile",
        "JsonSerializer.Deserialize<GameResponse>",
        "_stateDistributor.DistributeAsync",
        "PlayerWoundsPath"
    };

    private static readonly DiscoveryAllowance[] DiscoveryAllowances =
    {
        Scope("response-player-json-name", "game-response-player-wound-command", "playerWoundChanges", "[JsonPropertyName(\"playerWoundChanges\")]", "[JsonPropertyName(\"customStateChanges\")]"),
        Scope("response-player-property", "game-response-player-wound-command", "PlayerWoundChanges", "[JsonPropertyName(\"playerWoundChanges\")]", "[JsonPropertyName(\"customStateChanges\")]"),
        Scope("response-npc-command", "game-response-npc-wound-command", "NPCWoundChanges", "[JsonPropertyName(\"NPCWoundChanges\")]", "[JsonPropertyName(\"interNPCRelationshipChanges\")]"),

        Scope("mapping-player-command", "player-wound-file-mapping", "playerWoundChanges", "[\"playerWoundChanges\"] =", "[\"customStateChanges\"] ="),
        Scope("mapping-player-path", "player-wound-file-mapping", "game_state/player/wounds.json", "[\"playerWoundChanges\"] =", "[\"customStateChanges\"] ="),
        Scope("mapping-npc-command", "npc-wound-file-mapping", "NPCWoundChanges", "[\"NPCWoundChanges\"] =", "[\"NPCRelationshipChanges\"] ="),
        Scope("mapping-npc-path", "npc-wound-file-mapping", "game_state/npcs/npc_effects.json", "[\"NPCWoundChanges\"] =", "[\"NPCRelationshipChanges\"] ="),

        Scope("state-distributor-field-mapping", "generic-response-file-distribution", "FileMapping.FieldToFile", "private Dictionary<string, Dictionary<string, JsonElement>> CollectFileUpdates(GameResponse response)", "private async Task MergeFieldsIntoFile("),
        Scope("qte-browser-rollback-field-mapping", "qte-terminal-response-wound-distribution-and-rollback", "FileMapping.FieldToFile", "internal static readonly IReadOnlyCollection<string> BrowserTransactionRollbackPaths", "internal static readonly IReadOnlyList<string> RhythmPulsePatternVariations"),
        Scope("qte-tracked-path-field-mapping", "qte-terminal-response-wound-distribution-and-rollback", "FileMapping.FieldToFile", "private static HashSet<string> CollectQteTrackedPaths(GameResponse response)", "private async Task RestoreQteNormalizationBaselineAsync("),
        Scope("qte-terminal-game-response-deserialization", "qte-terminal-response-wound-distribution-and-rollback", "JsonSerializer.Deserialize<GameResponse>", "var response = responseFragment != null", "private static void RejectMaterializationWithoutAcceptedContinuation("),
        Scope("qte-terminal-state-distribution", "qte-terminal-response-wound-distribution-and-rollback", "_stateDistributor.DistributeAsync", "private async Task ApplyTerminalOutcomeStateChangesCoreAsync(", "private async Task<QteNormalizationBaseline> CaptureQteNormalizationBaselineAsync("),

        Scope("player-validation-state-path", "player-loose-wound-validation", "game_state/player/wounds.json", "private async Task ValidatePlayerStateFiles(", "private async Task ValidatePlayerContractFile("),
        Scope("player-validation-file-path", "player-loose-wound-validation", "game_state/player/wounds.json", "private async Task ValidatePlayerFile(", "private void ValidatePlayerContract("),
        Scope("player-validation-command", "player-loose-wound-validation", "playerWoundChanges", "private void ValidatePlayerContract(", "private void ValidatePlayerStatus("),
        Scope("player-validation-container-name", "player-loose-wound-validation", "woundName", "private void ValidateWoundsContainer(", "private void ValidateCustomStatesContainer("),
        Scope("player-validation-object-name", "player-loose-wound-validation", "woundName", "private void ValidateWoundObject(", "private void ValidateCustomStateObject("),
        Scope("player-validation-object-description", "player-loose-wound-validation", "descriptionOfEffects", "private void ValidateWoundObject(", "private void ValidateCustomStateObject("),
        Scope("player-validation-generated-effects", "player-loose-wound-validation", "generatedEffects", "private void ValidateWoundObject(", "private void ValidateCustomStateObject("),
        Scope("player-validation-healing-state", "player-loose-wound-validation", "healingState", "private void ValidateWoundObject(", "private void ValidateCustomStateObject("),
        Scope("combatant-wound-reference", "combatant-wound-reference-validation", "WoundReference", "private void ValidateCombatantActiveEffectObject(", "private void ValidateWoundObject("),
        Scope("combatant-source-wound-id", "combatant-wound-reference-validation", "sourceWoundId", "private void ValidateCombatantActiveEffectObject(", "private void ValidateWoundObject("),

        Scope("npc-structured-wound-section", "npc-wound-identity-validation", "NPCWoundChanges", "private static readonly HashSet<string> NpcStructuredSingleActorSections", "private static readonly HashSet<string> NpcStructuredSpecialSections"),
        Scope("npc-contract-wound-section", "npc-wound-identity-validation", "NPCWoundChanges", "private void ValidateNpcContract(", "private void ValidateNpcSceneArray("),
        Scope("npc-state-file-command", "npc-wound-state-file-validation", "NPCWoundChanges", "private async Task ValidateNpcStateFiles(", "private async Task ValidateWorldQuestCombatFactionStateFiles("),
        Scope("npc-state-file-path", "npc-wound-state-file-validation", "game_state/npcs/npc_effects.json", "private async Task ValidateNpcStateFiles(", "private async Task ValidateWorldQuestCombatFactionStateFiles("),
        Scope("combatant-wound-type-enum", "combatant-wound-reference-type-authority", "WoundReference", "private static readonly HashSet<string> AllowedCombatantActiveEffectTypes", "private static readonly HashSet<string> AllowedVehicleTypes"),

        Scope("composer-player-wound-path", "canonical-wound-effect-source-composition", "game_state/player/wounds.json", "internal const string PlayerWoundsPath", "internal static readonly string[] SameTurnOwnerAuthorityPaths"),
        Scope("composer-player-wound-path-symbol", "canonical-wound-effect-source-composition", "PlayerWoundsPath", "internal const string PlayerWoundsPath", "internal static readonly string[] SameTurnOwnerAuthorityPaths"),
        Scope("composer-source-wound-authority", "canonical-wound-effect-source-composition", "PlayerWoundsPath", "internal static readonly string[] SourceAuthorityPaths", "private static readonly SourceDescriptor[] SkillSourceDescriptors"),
        Scope("composer-healed-fallback", "generic-source-wound-healed-fallback", "isHealed", "case \"wound\":", "case \"fate_card\":"),
        Scope("planner-npc-wound-fallback", "npc-effect-planner-wound-command-fallback", "NPCWoundChanges", "internal bool TryLocate(", "internal EffectCarrierCatalogInput ToInput("),
        Scope("effect-contract-npc-wound-command", "npc-effect-contract-wound-command-acceptance", "NPCWoundChanges", "private static void ValidateNpcCarrier(", "private static void ValidateEmbeddedCarrier("),

        Scope("universal-reader-path", "universal-status-raw-wound-reader", "game_state/player/wounds.json", "private static async Task<IReadOnlyList<UiAction>> AddMortalStatusDetailBlocks(", "private static UiEntityDossierBlock BuildStatusFactDossier("),
        Scope("universal-reader-name", "universal-status-raw-wound-reader", "woundName", "private static void AddMortalStatusWoundBlocks(", "private static void AddMortalStatusCustomStateBlocks("),
        Scope("universal-reader-description", "universal-status-raw-wound-reader", "descriptionOfEffects", "private static void AddMortalStatusWoundBlocks(", "private static void AddMortalStatusCustomStateBlocks("),
        Scope("universal-reader-healing", "universal-status-raw-wound-reader", "healingState", "private static void AddMortalStatusWoundBlocks(", "private static void AddMortalStatusCustomStateBlocks("),
        Scope("explorer-reader-path", "explorer-status-raw-wound-reader", "game_state/player/wounds.json", "private async Task ShowDetailedStatus()", "private async Task ShowSkills()"),
        Scope("explorer-preview-name", "explorer-status-wound-preview-renderer", "woundName", "private static void AppendStatusWoundPreview(", "private static void AppendStatusCustomStatePreview("),
        Scope("explorer-preview-description", "explorer-status-wound-preview-renderer", "descriptionOfEffects", "private static void AppendStatusWoundPreview(", "private static void AppendStatusCustomStatePreview("),
        Scope("explorer-preview-healing", "explorer-status-wound-preview-renderer", "healingState", "private static void AppendStatusWoundPreview(", "private static void AppendStatusCustomStatePreview("),

        Scope("accepted-npc-effect-carrier-path", "accepted-effect-carrier-catalog", "game_state/npcs/npc_effects.json", "internal const string NpcPath", "internal const string EnemiesPath"),
        Scope("accepted-player-wound-carrier-path", "accepted-wound-carrier-catalog", "game_state/player/wounds.json", "internal const string PlayerPath", "internal const string NpcPath"),
    };

    [Fact]
    public void WoundOwnershipInventory_ResolvesEveryDeclaredFileAndSemanticAnchor()
    {
        Assert.Equal(Inventory.Length, Inventory.Select(static entry => entry.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.Contains(Inventory, static entry => entry.Category == InventoryCategory.LegacyLooseWoundSurface);
        Assert.Contains(Inventory, static entry => entry.Category == InventoryCategory.AcceptedMechanicsIntegrationSeam);

        var entriesById = Inventory.ToDictionary(static entry => entry.Id, StringComparer.Ordinal);
        foreach (var entry in Inventory.OrderBy(static item => item.Id, StringComparer.Ordinal))
        {
            Assert.Equal(entry.RelativePath, NormalizeRelativePath(entry.RelativePath));
            var absolutePath = ToAbsolutePath(entry.RelativePath);
            Assert.True(
                File.Exists(absolutePath),
                $"{entry.Category} '{entry.Id}' must remain owned by '{entry.RelativePath}'.");

            var source = File.ReadAllText(absolutePath);
            foreach (var anchor in entry.RequiredAnchors)
            {
                Assert.True(
                    source.Contains(anchor, StringComparison.Ordinal),
                    $"{entry.Category} '{entry.Id}' is missing semantic anchor '{anchor}' in '{entry.RelativePath}'.");
            }
        }

        Assert.Equal(
            DiscoveryAllowances.Length,
            DiscoveryAllowances.Select(static allowance => allowance.Id).Distinct(StringComparer.Ordinal).Count());
        foreach (var allowance in DiscoveryAllowances)
        {
            Assert.True(
                entriesById.TryGetValue(allowance.InventoryId, out var owner),
                $"Discovery allowance '{allowance.Id}' has no inventory owner '{allowance.InventoryId}'.");

            var source = File.ReadAllText(ToAbsolutePath(owner!.RelativePath));
            var start = source.IndexOf(allowance.StartAnchor, StringComparison.Ordinal);
            var end = start < 0
                ? -1
                : source.IndexOf(
                    allowance.EndAnchor,
                    start + allowance.StartAnchor.Length,
                    StringComparison.Ordinal);
            Assert.True(
                start >= 0 && end > start,
                $"{owner.Category} '{owner.Id}' cannot bound discovery token '{allowance.Token}' " +
                $"between '{allowance.StartAnchor}' and '{allowance.EndAnchor}' in '{owner.RelativePath}'.");
        }
    }

    [Fact]
    public void ProductionLegacyWoundOccurrences_AreConfinedToInventoriedSemanticScopes()
    {
        var entriesById = Inventory.ToDictionary(static entry => entry.Id, StringComparer.Ordinal);
        var sources = EnumerateProductionSources()
            .ToDictionary(
                static relativePath => relativePath,
                static relativePath => File.ReadAllText(ToAbsolutePath(relativePath)),
                StringComparer.Ordinal);
        var matchedAllowances = new HashSet<string>(StringComparer.Ordinal);
        var violations = new List<string>();

        foreach (var token in DiscoveryTokens.OrderBy(static value => value, StringComparer.Ordinal))
        {
            var tokenOccurrences = 0;
            foreach (var (relativePath, source) in sources.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
            {
                foreach (var index in FindOccurrences(source, token))
                {
                    tokenOccurrences++;
                    var matchingAllowance = DiscoveryAllowances.FirstOrDefault(allowance =>
                        string.Equals(allowance.Token, token, StringComparison.Ordinal) &&
                        string.Equals(
                            entriesById[allowance.InventoryId].RelativePath,
                            relativePath,
                            StringComparison.Ordinal) &&
                        IsInsideSemanticScope(source, index, allowance));
                    if (matchingAllowance == null)
                    {
                        violations.Add(
                            $"Uninventoried legacy wound token '{token}' in production file '{relativePath}'.");
                        continue;
                    }

                    matchedAllowances.Add(matchingAllowance.Id);
                }
            }

            Assert.True(
                tokenOccurrences > 0,
                $"Discovery token '{token}' no longer identifies a production wound surface; update the explicit inventory during cutover.");
        }

        Assert.True(
            violations.Count == 0,
            "Production wound discovery found loose occurrences outside declared semantic owners:" +
            Environment.NewLine + string.Join(Environment.NewLine, violations));

        var unusedAllowances = DiscoveryAllowances
            .Where(allowance => !matchedAllowances.Contains(allowance.Id))
            .Select(allowance =>
                $"{entriesById[allowance.InventoryId].Category} '{allowance.InventoryId}' token " +
                $"'{allowance.Token}' in '{entriesById[allowance.InventoryId].RelativePath}'")
            .ToArray();
        Assert.True(
            unusedAllowances.Length == 0,
            "Declared wound discovery scopes contain no matching production occurrence:" +
            Environment.NewLine + string.Join(Environment.NewLine, unusedAllowances));
    }

    private static DiscoveryAllowance Scope(
        string id,
        string inventoryId,
        string token,
        string startAnchor,
        string endAnchor) =>
        new(id, inventoryId, token, startAnchor, endAnchor);

    private static IEnumerable<string> EnumerateProductionSources()
    {
        var productionRoot = Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient");
        Assert.True(Directory.Exists(productionRoot), "Production source root must exist.");

        return Directory
            .EnumerateFiles(productionRoot, "*.cs", SearchOption.AllDirectories)
            .Select(path => NormalizeRelativePath(Path.GetRelativePath(TestRepoPaths.RepoRoot, path)))
            .Where(static path => !HasExcludedSegment(path))
            .Where(static path =>
                !path.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase) &&
                !path.EndsWith(".generated.cs", StringComparison.OrdinalIgnoreCase) &&
                !path.EndsWith(".designer.cs", StringComparison.OrdinalIgnoreCase))
            .OrderBy(static path => path, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool HasExcludedSegment(string relativePath)
    {
        var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "bin",
            "obj",
            ".git",
            ".worktrees",
            "worktrees",
            "TestResults",
            "BookOfEternityClient.Tests",
            "BookOfEternityClient.IntegrationTests"
        };
        return relativePath.Split('/').Any(excluded.Contains);
    }

    private static IEnumerable<int> FindOccurrences(string source, string token)
    {
        var offset = 0;
        while ((offset = source.IndexOf(token, offset, StringComparison.Ordinal)) >= 0)
        {
            yield return offset;
            offset += token.Length;
        }
    }

    private static bool IsInsideSemanticScope(
        string source,
        int occurrenceIndex,
        DiscoveryAllowance allowance)
    {
        var start = source.IndexOf(allowance.StartAnchor, StringComparison.Ordinal);
        if (start < 0)
            return false;
        var end = source.IndexOf(
            allowance.EndAnchor,
            start + allowance.StartAnchor.Length,
            StringComparison.Ordinal);
        return end > start && occurrenceIndex >= start && occurrenceIndex < end;
    }

    private static string ToAbsolutePath(string relativePath) =>
        Path.Combine(
            TestRepoPaths.RepoRoot,
            NormalizeRelativePath(relativePath).Replace('/', Path.DirectorySeparatorChar));

    private static string NormalizeRelativePath(string path) =>
        path.Replace('\\', '/');
}
