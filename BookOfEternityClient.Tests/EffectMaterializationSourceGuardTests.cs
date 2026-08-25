using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class EffectMaterializationSourceGuardTests
{
    [Fact]
    public void EffectPublication_MustOnlyRunThroughTheCommonAcceptedMechanicsPlan()
    {
        var normalizerRoot = Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "Services",
            "CanonicalStateNormalizer");
        var effects = File.ReadAllText(Path.Combine(
            normalizerRoot,
            "CanonicalStateNormalizer.Effects.cs"));
        var accumulated = File.ReadAllText(Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "Services",
            "CanonicalStateNormalizer.cs"));
        var acceptedMechanics = File.ReadAllText(Path.Combine(
            normalizerRoot,
            "CanonicalStateNormalizer.AcceptedMechanics.cs"));
        var effectCache = File.ReadAllText(Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "Services",
            "EffectAcceptedTurnPlanCache.cs"));

        Assert.DoesNotContain("NormalizeEffectsAsync(", effects, StringComparison.Ordinal);
        Assert.DoesNotContain("TryGetValidated(", effectCache, StringComparison.Ordinal);
        Assert.DoesNotContain("HasValidated(", effectCache, StringComparison.Ordinal);
        Assert.Contains(
            "ValidateEffectPlanPublicationBindingAsync(",
            effects,
            StringComparison.Ordinal);
        Assert.Contains(
            "PublishAcceptedMechanicsAsync(",
            accumulated,
            StringComparison.Ordinal);
        Assert.Contains(
            "AcceptedMechanicsPlanAuthority.TryTakeValidated(",
            acceptedMechanics,
            StringComparison.Ordinal);
        Assert.Contains(
            "writes[EffectAcceptedTurnPlan.IdentityIndexPath] = plan.EffectIdentityAfterImage",
            acceptedMechanics,
            StringComparison.Ordinal);
        Assert.Contains(
            "foreach (var pair in plan.EffectCarrierAfterImages)",
            acceptedMechanics,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ActiveGmEffectGuidance_UsesOnlyCanonicalRuntimeAuthority()
    {
        var retiredCarrierDocuments = new[]
        {
            "Rules/Block_6.txt",
            "Rules/Block_12.txt",
            "Rules/Block_13.txt",
            "Rules/Block_14.txt",
            "Examples/E_Block_6.txt",
            "BookOfEternityClient/game_master_daemon.ps1"
        };

        foreach (var relativePath in retiredCarrierDocuments)
        {
            var source = ReadRepoFile(relativePath);
            Assert.DoesNotContain("activeBuffs", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("activeDebuffs", source, StringComparison.OrdinalIgnoreCase);
        }

        var legacyLifetimeGuidance = ReadRepoFile("Rules/Block_5.txt") + '\n' +
                                     ReadRepoFile("Rules/Block_6.txt");
        Assert.DoesNotContain("999", legacyLifetimeGuidance, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "decrease their duration by 1",
            ReadRepoFile("Rules/Block_13.txt"),
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "Sum ALL percentage 'value's from effects",
            ReadRepoFile("Rules/Block_14.txt"),
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "{effectType:",
            ReadRepoFile("Examples/E_Block_16.txt"),
            StringComparison.OrdinalIgnoreCase);

        var mortalExample = ReadRepoFile("Examples/E_CLI_Effect_Materialization.txt");
        Assert.Contains("\"effectChanges\": [", mortalExample, StringComparison.Ordinal);
        Assert.Contains("there is no numeric sentinel", mortalExample, StringComparison.OrdinalIgnoreCase);

        foreach (var afterlifeDocument in new[]
                 {
                     "OtherGuides/Afterlife_Contract_Matrix.md",
                     "Examples/E_CLI_Afterlife_Turns.txt",
                     "Examples/example_validation_manifest.json"
                 })
        {
            Assert.Contains(
                "remaining lifetime is client-owned",
                ReadRepoFile(afterlifeDocument),
                StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void FullTurnEffectResourceRepairGuidance_UsesOnlyGmAuthoredReplayPaths()
    {
        var daemon = ReadRepoFile("BookOfEternityClient/game_master_daemon.ps1");
        var validationAndRepair = ReadRepoFile(
            "BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs");
        var entrypoints = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["TaskGuides/CLI_Step_Main.txt"] =
                ReadRepoFile("TaskGuides/CLI_Step_Main.txt"),
            ["CLI_API_Specification.md"] =
                ReadRepoFile("CLI_API_Specification.md"),
            ["CLI_Agent_Daemon_Specification.md"] =
                ReadRepoFile("CLI_Agent_Daemon_Specification.md"),
            ["OtherGuides/Effect_Materialization_Contract.md"] =
                ReadRepoFile("OtherGuides/Effect_Materialization_Contract.md"),
            ["effect atomic repair contract"] =
                ReadRepoFile(
                    "specs/1535-complete-effect-materialization/contracts/" +
                    "effect-atomic-repair-and-rollback.md"),
            ["accepted mechanics publication contract"] =
                ReadRepoFile(
                    "specs/1543-unified-resource-authority/contracts/" +
                    "accepted-mechanics-publication.md"),
            ["embedded VALIDATION_REPAIR_TEMPLATE.md"] =
                ExtractRequiredSection(
                    daemon,
                    "$templates += Write-GmContextPackTemplate -RelativePath \"Templates\\VALIDATION_REPAIR_TEMPLATE.md\"",
                    "$templates += Write-GmContextPackTemplate -RelativePath \"Templates\\PROGRESSION_REPORT_TEMPLATE.json\""),
            ["runtime repair request instructions"] =
                ExtractRequiredSection(
                    validationAndRepair,
                    "private static string BuildValidationRepairRequestInstructions(",
                    "private static (string SessionId, string RequestId, int TurnNumber) BuildProtocolRequestMetadata(")
        };

        foreach (var (entrypoint, guidance) in entrypoints)
        {
            foreach (var stablePhrase in new[]
                     {
                         "fullTurnResubmissionRequired",
                         "requiredResubmissionPaths",
                         "GM-authored",
                         "client-owned"
                     })
            {
                Assert.True(
                    guidance.Contains(stablePhrase, StringComparison.OrdinalIgnoreCase),
                    $"{entrypoint} must contain full-turn ownership guidance '{stablePhrase}'.");
            }
        }

        foreach (var packetKind in new[]
                 {
                     "effect_materialization_repair",
                     "resource_semantic_omission_repair"
                 })
        {
            Assert.Contains(packetKind, daemon, StringComparison.Ordinal);
            Assert.Contains(packetKind, validationAndRepair, StringComparison.Ordinal);
            Assert.Contains(packetKind, entrypoints["TaskGuides/CLI_Step_Main.txt"], StringComparison.Ordinal);
            Assert.Contains(packetKind, entrypoints["CLI_API_Specification.md"], StringComparison.Ordinal);
            Assert.Contains(packetKind, entrypoints["CLI_Agent_Daemon_Specification.md"], StringComparison.Ordinal);
        }

        Assert.Contains("$requiresFullTurnResubmission", daemon, StringComparison.Ordinal);
        Assert.Contains("$repair.fullTurnResubmissionRequired", daemon, StringComparison.Ordinal);
    }

    [Fact]
    public void PendingEffectResourceWaveGuidance_IsCompleteInEveryActiveGmEntrypoint()
    {
        var daemon = ReadRepoFile("BookOfEternityClient/game_master_daemon.ps1");
        var validationAndRepair = ReadRepoFile(
            "BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs");
        var entrypoints = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["TaskGuides/CLI_Step_Main.txt"] =
                ReadRepoFile("TaskGuides/CLI_Step_Main.txt"),
            ["CLI_Agent_Daemon_Specification.md"] =
                ReadRepoFile("CLI_Agent_Daemon_Specification.md"),
            ["OtherGuides/Effect_Materialization_Contract.md"] =
                ReadRepoFile("OtherGuides/Effect_Materialization_Contract.md"),
            ["game_master_daemon.ps1 injected resource/effect directives"] =
                ExtractRequiredSection(
                    daemon,
                    "$script:UnifiedResourceAuthorityDirective =",
                    "$script:LastRepairRequestWrite",
                    useLastStart: true),
            ["game_master_daemon.ps1 embedded TURN_OUTPUT_TEMPLATE.md"] =
                ExtractRequiredSection(
                    daemon,
                    "$turnOutputTemplate = @'",
                    "$turnOutputTemplate = $turnOutputTemplate.Replace"),
            ["resource_pending_full_turn_resubmission_required repair hint"] =
                ExtractRequiredSection(
                    validationAndRepair,
                    "private static ValidationIssue BuildBoundedResourceResolutionResubmissionIssue(",
                    "private async Task FailClosedAcceptedTurnCanonicalRefreshAsync("),
            ["bounded pending BuildValidationRepairRequestInstructions branch"] =
                ExtractRequiredSection(
                    validationAndRepair,
                    "private static string BuildValidationRepairRequestInstructions(",
                    "private static (string SessionId, string RequestId, int TurnNumber) BuildProtocolRequestMetadata(")
        };

        foreach (var (entrypoint, guidance) in entrypoints)
            AssertCompletePendingWaveOriginGuidance(entrypoint, guidance);
    }

    [Fact]
    public void QteDeferredReceiptDaemonDispatch_IsDedicatedAndPrecedesOrdinaryTurns()
    {
        var daemon = ReadRepoFile("BookOfEternityClient/game_master_daemon.ps1");
        var processor = ExtractRequiredSection(
            daemon,
            "function Process-QteEffectResolutionRequest",
            "function Process-Turn");
        var promptBuilder = ExtractRequiredSection(
            daemon,
            "function Build-QteEffectResolutionDispatchMessage",
            "function Process-QteEffectResolutionRequest");

        foreach (var path in new[]
                 {
                     "input\\qte_effect_resolution_request.json",
                     "output\\qte_effect_resolution_receipts.json",
                     "ready\\qte_effect_resolution_complete.json"
                 })
        {
            Assert.Contains(path, daemon, StringComparison.Ordinal);
        }

        Assert.Contains(
            "Complete-BoeQteEffectResolution -Receipts",
            promptBuilder,
            StringComparison.Ordinal);
        Assert.Contains("receipt-only", promptBuilder, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("NOT an ordinary turn", promptBuilder, StringComparison.Ordinal);
        Assert.Contains("safePacket", promptBuilder, StringComparison.Ordinal);
        Assert.Contains("LAST action", promptBuilder, StringComparison.Ordinal);
        Assert.Contains("Do NOT call Complete-BoeTurn", promptBuilder, StringComparison.Ordinal);
        Assert.Contains("Do NOT write game state", promptBuilder, StringComparison.Ordinal);

        Assert.Contains(
            "Test-QteEffectResolutionReadyMatchesRequest",
            processor,
            StringComparison.Ordinal);
        Assert.Contains("Dispatch-WithRetry", processor, StringComparison.Ordinal);
        Assert.Contains("$QteEffectResolutionReadyFile", processor, StringComparison.Ordinal);
        Assert.DoesNotContain("$TurnRequestFile", processor, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "Test-TurnRequestHasPendingSnapshotContext",
            processor,
            StringComparison.Ordinal);

        Assert.Contains(
            "$qteEffectResolutionWatcher.Filter = \"qte_effect_resolution_request.json\"",
            daemon,
            StringComparison.Ordinal);

        var startupQteIndex = daemon.IndexOf(
            "Process-QteEffectResolutionRequest -RequestPath $QteEffectResolutionRequestFile",
            StringComparison.Ordinal);
        var startupTurnIndex = daemon.IndexOf(
            "Process-Turn -RequestPath $TurnRequestFile",
            startupQteIndex,
            StringComparison.Ordinal);
        Assert.True(
            startupQteIndex >= 0 && startupTurnIndex > startupQteIndex,
            "Daemon startup must dispatch deferred QTE receipt authority before any ordinary turn.");

        var mainLoopIndex = daemon.IndexOf("# Main loop", StringComparison.Ordinal);
        var pollingQteIndex = daemon.IndexOf(
            "Process-QteEffectResolutionRequest -RequestPath $QteEffectResolutionRequestFile",
            Math.Max(0, mainLoopIndex),
            StringComparison.Ordinal);
        var pollingTurnIndex = daemon.IndexOf(
            "Process-Turn -RequestPath $TurnRequestFile",
            Math.Max(0, pollingQteIndex),
            StringComparison.Ordinal);
        Assert.True(
            mainLoopIndex >= 0 && pollingQteIndex > mainLoopIndex &&
            pollingTurnIndex > pollingQteIndex,
            "Daemon polling must prioritize deferred QTE receipt authority over ordinary turns.");
    }

    private static void AssertCompletePendingWaveOriginGuidance(
        string entrypoint,
        string guidance)
    {
        foreach (var stablePhrase in new[]
                 {
                     "current safe packet only",
                     "same complete semantic turn",
                     "client carries earlier-wave terminal bindings",
                     "original candidate, mutation authority, and source authority remain immutable/client-owned"
                 })
        {
            Assert.True(
                guidance.Contains(stablePhrase, StringComparison.OrdinalIgnoreCase),
                $"{entrypoint} must contain stable pending-wave guidance '{stablePhrase}'.");
        }
    }

    private static string ExtractRequiredSection(
        string source,
        string startMarker,
        string endMarker,
        bool useLastStart = false)
    {
        var startIndex = useLastStart
            ? source.LastIndexOf(startMarker, StringComparison.Ordinal)
            : source.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(startIndex >= 0, $"Missing source-guard start marker '{startMarker}'.");

        var endIndex = source.IndexOf(endMarker, startIndex, StringComparison.Ordinal);
        Assert.True(endIndex > startIndex, $"Missing source-guard end marker '{endMarker}'.");
        return source[startIndex..endIndex];
    }

    private static string ReadRepoFile(string relativePath) =>
        File.ReadAllText(Path.Combine(
            TestRepoPaths.RepoRoot,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
}
