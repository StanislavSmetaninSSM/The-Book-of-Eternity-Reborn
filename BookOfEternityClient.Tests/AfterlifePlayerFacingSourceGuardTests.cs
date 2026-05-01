using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class AfterlifePlayerFacingSourceGuardTests
{
    [Fact]
    public void ChaosSeaHighCostActionsExposeFullContractPreviews()
    {
        var mainMenu = ReadSource("Core", "GameEngine", "GameEngine.MainMenu.cs");
        var inkFeathers = ReadSource("UI", "ExplorerMode", "ExplorerMode.Afterlife.InkFeathersAndOfferings.cs");
        var foundation = ReadSource("UI", "ExplorerMode", "ExplorerMode.Afterlife.PlayerGuardianFoundation.cs");

        Assert.Contains("Полный контракт /incarnate", mainMenu, StringComparison.Ordinal);
        Assert.Contains("game_state/control/incarnation_trigger.json", mainMenu, StringComparison.Ordinal);
        Assert.Contains("ConfirmIncarnationContractPreview", mainMenu, StringComparison.Ordinal);
        Assert.Contains("BuildPendingWorldSetupActionSummary", mainMenu, StringComparison.Ordinal);
        Assert.Contains("pending setup exists but is malformed", mainMenu, StringComparison.Ordinal);
        Assert.Contains("WorldDirectiveService.PendingSetupPath", mainMenu, StringComparison.Ordinal);
        Assert.Contains("new Panel(GameInterface.SafeMarkup(string.Join(\"\\n\", new[]", mainMenu, StringComparison.Ordinal);
        Assert.DoesNotContain("new Panel(string.Join(\"\\n\", new[]", mainMenu, StringComparison.Ordinal);
        Assert.DoesNotContain("pending_incarnation_world_setup.json", mainMenu, StringComparison.Ordinal);
        Assert.True(
            mainMenu.IndexOf("if (!ConfirmIncarnationContractPreview", StringComparison.Ordinal) <
            mainMenu.IndexOf("_fs.ClearCurrentWorldLore();", StringComparison.Ordinal));

        Assert.Contains("BuildInkFeatherActionAuditNode", inkFeathers, StringComparison.Ordinal);
        Assert.Contains("output/ink_feather_action_result.json", inkFeathers, StringComparison.Ordinal);
        Assert.Contains("alreadyDeductedByClient", inkFeathers, StringComparison.Ordinal);
        Assert.Contains("stateEvidence обязан содержать affectedFiles", inkFeathers, StringComparison.Ordinal);
        Assert.Contains("BuildMemoryGatesPreviewAuditLines", inkFeathers, StringComparison.Ordinal);
        Assert.Contains("full before payload", inkFeathers, StringComparison.Ordinal);
        Assert.Contains("Canonical after payload schema", inkFeathers, StringComparison.Ordinal);
        Assert.Contains("sourceLifeHint: required non-empty", inkFeathers, StringComparison.Ordinal);
        Assert.Contains("non-empty playerStatBonus", inkFeathers, StringComparison.Ordinal);
        Assert.Contains("group=Knowledge", inkFeathers, StringComparison.Ordinal);
        Assert.Contains("snapshotAuthorityPath", inkFeathers, StringComparison.Ordinal);
        Assert.Contains("acceptedPreTurnAuthority", inkFeathers, StringComparison.Ordinal);
        Assert.Contains("expectedSoulRelicDelta", inkFeathers, StringComparison.Ordinal);
        Assert.Contains("forbiddenSurfaces", inkFeathers, StringComparison.Ordinal);

        Assert.Contains("Полный pending contract основания Хранителя", foundation, StringComparison.Ordinal);
        Assert.Contains("UpdateGuardians.create", foundation, StringComparison.Ordinal);
        Assert.Contains("PlayerGuardianFoundationState.PendingRequestPath", foundation, StringComparison.Ordinal);
    }

    [Fact]
    public void ChaosSeaBlockersHelpAndLocalAuditsStayExplicit()
    {
        var mainMenu = ReadSource("Core", "GameEngine", "GameEngine.MainMenu.cs");
        var help = ReadSource("UI", "ExplorerMode", "ExplorerMode.MetaStoryAndStatus.cs");
        var lifecycle = ReadSource("Core", "GameEngine", "GameEngine.TurnLifecycle.cs");
        var explorerPrivate = ReadSource("UI", "ExplorerMode", "ExplorerMode.PrivateImplementation.cs");
        var trade = ReadSource("UI", "ExplorerMode", "ExplorerMode.Afterlife.GuardiansProjectsTrade.cs");
        var inbox = ReadSource("UI", "ExplorerMode", "ExplorerMode.Afterlife.SoulRelicsArchiveInbox.cs");

        Assert.Contains("BuildPendingFileBlockerAsync", mainMenu, StringComparison.Ordinal);
        Assert.Contains("DescribeBlockingShiningPendingContractAsync", mainMenu, StringComparison.Ordinal);
        Assert.Contains("DescribeShiningPendingClosure", mainMenu, StringComparison.Ordinal);
        Assert.Contains("BuildShiningPendingBlockerIdentitySummary", mainMenu, StringComparison.Ordinal);
        Assert.Contains("missing requests[] array", mainMenu, StringComparison.Ordinal);
        Assert.Contains("requestId=", mainMenu, StringComparison.Ordinal);
        Assert.Contains("закрытие:", mainMenu, StringComparison.Ordinal);
        Assert.Contains("full payload", mainMenu, StringComparison.Ordinal);

        Assert.DoesNotContain("[yellow]/abodes", help, StringComparison.Ordinal);
        Assert.Contains("[blue]/chaos_sea", help, StringComparison.Ordinal);
        Assert.Contains("[blue]/море_хаоса", help, StringComparison.Ordinal);
        Assert.Contains("[blue]/abodes", help, StringComparison.Ordinal);
        Assert.DoesNotContain("/реликвии /хранители /обители /душа", lifecycle, StringComparison.Ordinal);
        Assert.Contains("/статус /реликвии /хранители /обители /гача /перья /архив_души", lifecycle, StringComparison.Ordinal);
        Assert.Contains("CleanupAfterAcceptedChaosSeaMarkerTurn(snapshotContext?.PlayerAction)", lifecycle, StringComparison.Ordinal);
        Assert.Contains("CleanupAfterCancelledChaosSeaMarkerTurn(action)", lifecycle, StringComparison.Ordinal);
        Assert.Contains("[\"/chaos_sea\"] = ShowGuardians", ReadSource("UI", "ExplorerMode.cs"), StringComparison.Ordinal);
        Assert.Contains("string.Equals(command, \"/abodes\"", explorerPrivate, StringComparison.Ordinal);
        Assert.Contains("string.Equals(command, \"/обители\"", explorerPrivate, StringComparison.Ordinal);
        Assert.Contains("string.Equals(command, \"/chaos_sea\"", explorerPrivate, StringComparison.Ordinal);

        Assert.Contains("BuildGuardianSellAuditNode", trade, StringComparison.Ordinal);
        Assert.Contains("BuildGuardianBuyAuditNode", trade, StringComparison.Ordinal);
        Assert.Contains("guardianId", trade, StringComparison.Ordinal);
        Assert.Contains("tradeCycleId", trade, StringComparison.Ordinal);
        Assert.Contains("transactionCorrelationId", trade, StringComparison.Ordinal);
        Assert.Contains("generatedBuybackEntryFields", trade, StringComparison.Ordinal);
        Assert.Contains("GM turn не отправляется: это client-local coordinated write with full audit JSON", trade, StringComparison.Ordinal);

        Assert.Contains("candidateId:", inbox, StringComparison.Ordinal);
        Assert.Contains("archiveId:", inbox, StringComparison.Ordinal);
        Assert.Contains("Полный JSON выбранного archive candidate", inbox, StringComparison.Ordinal);
        Assert.Contains("Полный JSON записи Архива души", inbox, StringComparison.Ordinal);
        Assert.Contains("Полный JSON afterlife notification", inbox, StringComparison.Ordinal);
    }

    [Fact]
    public void SystemGuardianPresetScreensExposeFullDossierAndJson()
    {
        var mainMenu = ReadSource("Core", "GameEngine", "GameEngine.MainMenu.cs");
        var explorerPrivate = ReadSource("UI", "ExplorerMode", "ExplorerMode.PrivateImplementation.cs");

        Assert.Contains("Полный JSON system guardian preset", mainMenu, StringComparison.Ordinal);
        Assert.Contains("Полный JSON system guardian preset", explorerPrivate, StringComparison.Ordinal);
        Assert.Contains("dossierMarkdown", mainMenu, StringComparison.Ordinal);
        Assert.Contains("dossierMarkdown", explorerPrivate, StringComparison.Ordinal);
        Assert.DoesNotContain(".Take(18)", mainMenu, StringComparison.Ordinal);
        Assert.DoesNotContain(".Take(18)", explorerPrivate, StringComparison.Ordinal);
    }

    [Fact]
    public void ShiningPlayerFacingSurfacesDoNotDumpRawBlessingPayloads()
    {
        var lifecycle = ReadSource("Core", "GameEngine", "GameEngine.TurnLifecycle.cs");
        var shiningAbode = ReadSource("UI", "ExplorerMode", "ExplorerMode.Afterlife.ShiningAbode.cs");

        Assert.DoesNotContain("Shining blessing effectPayload", lifecycle, StringComparison.Ordinal);
        Assert.DoesNotContain("effectPayload.ToJsonString", lifecycle, StringComparison.Ordinal);
        Assert.Contains("CloneShiningJsonForPlayerFacingAudit", shiningAbode, StringComparison.Ordinal);
        Assert.Contains("RemoveShiningBlessingRuntimePayloads", shiningAbode, StringComparison.Ordinal);
        Assert.DoesNotContain("WriteJsonAuditPanel(\"Полный JSON coreActionReceipts[]\"", shiningAbode, StringComparison.Ordinal);
        Assert.DoesNotContain("WriteJsonAuditPanel(\"Полный JSON shining_abode_state.json", shiningAbode, StringComparison.Ordinal);
    }

    private static string ReadSource(params string[] pathParts) =>
        File.ReadAllText(Path.Combine(
            new[] { TestRepoPaths.RepoRoot, "BookOfEternityClient" }.Concat(pathParts).ToArray()));
}
