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
        Assert.DoesNotContain("direct_chaos_gacha_result receipt data", inkFeathers, StringComparison.Ordinal);
        Assert.Contains("no separate direct_chaos_gacha_result receipt is supported", inkFeathers, StringComparison.Ordinal);
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
        Assert.Contains("WriteCompletedPlayerGuardianFoundationAuditPanelsAsync", foundation, StringComparison.Ordinal);
        Assert.Contains("foundationHistoryEntry", foundation, StringComparison.Ordinal);
        Assert.Contains("foundedGuardian", foundation, StringComparison.Ordinal);
        Assert.Contains("chaosSeaNavigation", foundation, StringComparison.Ordinal);
    }

    [Fact]
    public void ChaosSeaBlockersHelpAndLocalAuditsStayExplicit()
    {
        var mainMenu = ReadSource("Core", "GameEngine", "GameEngine.MainMenu.cs");
        var help = ReadSource("UI", "ExplorerMode", "ExplorerMode.MetaStoryAndStatus.cs");
        var lifecycle = ReadSource("Core", "GameEngine", "GameEngine.TurnLifecycle.cs");
        var explorerPrivate = ReadSource("UI", "ExplorerMode", "ExplorerMode.PrivateImplementation.cs");
        var trade = ReadSource("UI", "ExplorerMode", "ExplorerMode.Afterlife.GuardiansProjectsTrade.cs");
        var inkFeathers = ReadSource("UI", "ExplorerMode", "ExplorerMode.Afterlife.InkFeathersAndOfferings.cs");
        var inventory = ReadSource("UI", "ExplorerMode", "ExplorerMode.Inventory.cs");
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
        Assert.Contains("ПЕРЕДАЧА ИЗ СИЯЮЩЕЙ ОБИТЕЛИ", help, StringComparison.Ordinal);
        Assert.Contains("Вершина полного Сияния: Источник Света", help, StringComparison.Ordinal);
        Assert.Contains("Старое имя той же безопасной команды", help, StringComparison.Ordinal);
        Assert.DoesNotContain("SHINING ABODE HANDOFF", help, StringComparison.Ordinal);
        Assert.DoesNotContain("Capstone полного Сияния", help, StringComparison.Ordinal);
        Assert.DoesNotContain("Legacy alias", help, StringComparison.Ordinal);
        Assert.DoesNotContain("/реликвии /хранители /обители /душа", lifecycle, StringComparison.Ordinal);
        Assert.Contains("/статус /реликвии /хранители /обители /гача /перья /архив_души", lifecycle, StringComparison.Ordinal);
        Assert.Contains("CleanupAfterAcceptedChaosSeaMarkerTurn(snapshotContext?.PlayerAction)", lifecycle, StringComparison.Ordinal);
        Assert.Contains("CleanupAfterCancelledChaosSeaMarkerTurn(action)", lifecycle, StringComparison.Ordinal);
        Assert.Contains("[\"/chaos_sea\"] = ShowGuardians", ReadSource("UI", "ExplorerMode.cs"), StringComparison.Ordinal);
        Assert.Contains("string.Equals(command, \"/abodes\"", explorerPrivate, StringComparison.Ordinal);
        Assert.Contains("string.Equals(command, \"/обители\"", explorerPrivate, StringComparison.Ordinal);
        Assert.Contains("string.Equals(command, \"/chaos_sea\"", explorerPrivate, StringComparison.Ordinal);
        Assert.DoesNotContain("string.Equals(command, \"/guardians\"", explorerPrivate, StringComparison.Ordinal);
        Assert.DoesNotContain("string.Equals(command, \"/хранители\"", explorerPrivate, StringComparison.Ordinal);
        Assert.DoesNotContain("string.Equals(command, \"/abode_offering\"", explorerPrivate, StringComparison.Ordinal);
        Assert.DoesNotContain("string.Equals(command, \"/подношение_обители\"", explorerPrivate, StringComparison.Ordinal);
        Assert.DoesNotContain("string.Equals(command, \"/guardian_projects\"", explorerPrivate, StringComparison.Ordinal);
        Assert.DoesNotContain("string.Equals(command, \"/проекты_хранителей\"", explorerPrivate, StringComparison.Ordinal);
        Assert.Contains("only canonical TriggerIncarnation / game_state/control/incarnation_trigger.json", lifecycle, StringComparison.Ordinal);
        Assert.DoesNotContain("ALLOWED: only mortal bootstrap / next-life materialization", lifecycle, StringComparison.Ordinal);
        Assert.DoesNotContain("ALLOWED: UpdateGuardians, Soul Relic systems, Ink Feather spending, Gacha, Abode/Guardian interactions, Life Evaluation, Incarnation setup", lifecycle, StringComparison.Ordinal);

        Assert.Contains("BuildGuardianSellAuditNode", trade, StringComparison.Ordinal);
        Assert.Contains("BuildGuardianBuyAuditNode", trade, StringComparison.Ordinal);
        Assert.Contains("BuildGuardianBuybackAuditNode", trade, StringComparison.Ordinal);
        Assert.Contains("BuildChaosSeaTravelAuditNode", trade, StringComparison.Ordinal);
        Assert.Contains("BuildResidentInteractionReceiptsAuditNode", trade, StringComparison.Ordinal);
        Assert.Contains("BuildActorJournalEntriesAuditArray", trade, StringComparison.Ordinal);
        Assert.Contains("Полный JSON interactionReceipts резидента", trade, StringComparison.Ordinal);
        Assert.Contains("Полный JSON thought journal Хранителя", trade, StringComparison.Ordinal);
        Assert.Contains("Полный JSON social journal Хранителя", trade, StringComparison.Ordinal);
        Assert.Contains("eventType:", trade, StringComparison.Ordinal);
        Assert.Contains("historyEntryId", trade, StringComparison.Ordinal);
        Assert.Contains("guardianId", trade, StringComparison.Ordinal);
        Assert.Contains("tradeCycleId", trade, StringComparison.Ordinal);
        Assert.Contains("guardianId=", trade, StringComparison.Ordinal);
        Assert.Contains("projectId=", trade, StringComparison.Ordinal);
        Assert.Contains("choiceIndexByLabel", trade, StringComparison.Ordinal);
        Assert.Contains("transactionCorrelationId", trade, StringComparison.Ordinal);
        Assert.Contains("statusBefore", trade, StringComparison.Ordinal);
        Assert.Contains("statusAfter", trade, StringComparison.Ordinal);
        Assert.Contains("stateTransition", trade, StringComparison.Ordinal);
        Assert.Contains("generatedBuybackEntryFields", trade, StringComparison.Ordinal);
        Assert.Contains("Ход ГМ не отправляется: это согласованная локальная запись клиента с полным JSON-аудитом", trade, StringComparison.Ordinal);
        Assert.DoesNotContain(".Take(3)", trade, StringComparison.Ordinal);

        Assert.Contains("relicId=", inkFeathers, StringComparison.Ordinal);
        Assert.Contains("archiveId=", inkFeathers, StringComparison.Ordinal);
        Assert.DoesNotContain("relicChoices.IndexOf", inkFeathers, StringComparison.Ordinal);
        Assert.DoesNotContain("archiveChoices.IndexOf", inkFeathers, StringComparison.Ordinal);
        Assert.Contains("id=", inventory, StringComparison.Ordinal);
        Assert.DoesNotContain("ShortIdentity", inventory, StringComparison.Ordinal);

        Assert.Contains("candidateId:", inbox, StringComparison.Ordinal);
        Assert.Contains("archiveId:", inbox, StringComparison.Ordinal);
        Assert.Contains("archivedAtUtc", inbox, StringComparison.Ordinal);
        Assert.Contains("skippedAtUtc", inbox, StringComparison.Ordinal);
        Assert.Contains("Сохранено в Архив UTC", inbox, StringComparison.Ordinal);
        Assert.Contains("Пропущено UTC", inbox, StringComparison.Ordinal);
        Assert.Contains("Полный JSON выбранного archive candidate", inbox, StringComparison.Ordinal);
        Assert.Contains("Полный JSON записи Архива души", inbox, StringComparison.Ordinal);
        Assert.Contains("Полный JSON afterlife notification", inbox, StringComparison.Ordinal);
        Assert.DoesNotContain(".Take(3)", inkFeathers, StringComparison.Ordinal);
        Assert.DoesNotContain(".Take(3)", inbox, StringComparison.Ordinal);
        Assert.Contains("FormatAfterlifeNotificationInline", trade, StringComparison.Ordinal);
        Assert.Contains("FormatAfterlifeNotificationInline", inkFeathers, StringComparison.Ordinal);
        Assert.Contains("FormatAfterlifeNotificationInline", inbox, StringComparison.Ordinal);
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
        var statusAudit = ReadSource("UI", "ExplorerMode", "ExplorerMode.Afterlife.StatusAudit.cs");
        var explorerPrivate = ReadSource("UI", "ExplorerMode", "ExplorerMode.PrivateImplementation.cs");

        Assert.DoesNotContain("Shining blessing effectPayload", lifecycle, StringComparison.Ordinal);
        Assert.DoesNotContain("effectPayload.ToJsonString", lifecycle, StringComparison.Ordinal);
        Assert.Contains("CloneShiningJsonForPlayerFacingAudit", shiningAbode, StringComparison.Ordinal);
        Assert.Contains("CloneShiningJsonForPlayerFacingAudit", statusAudit, StringComparison.Ordinal);
        Assert.Contains("ContainsRuntimeEffectPayload", explorerPrivate, StringComparison.Ordinal);
        Assert.Contains("RemoveShiningBlessingRuntimePayloads", shiningAbode, StringComparison.Ordinal);
        Assert.DoesNotContain("WriteJsonAuditPanel(\"Полный JSON coreActionReceipts[]\"", shiningAbode, StringComparison.Ordinal);
        Assert.DoesNotContain("WriteJsonAuditPanel(\"Полный JSON shining_abode_state.json", shiningAbode, StringComparison.Ordinal);
    }

    [Fact]
    public void AfterlifeStatusAuditSurfacesWrongRealmAndProgressionContracts()
    {
        var statusAudit = ReadSource("UI", "ExplorerMode", "ExplorerMode.Afterlife.StatusAudit.cs");

        Assert.Contains("ActorSocialInteractionRequestState.PendingNpcRequestPath", statusAudit, StringComparison.Ordinal);
        Assert.Contains("NpcTradeRequestState.PendingRequestPath", statusAudit, StringComparison.Ordinal);
        Assert.Contains("только ремонт в неверной области: Море Хаоса", statusAudit, StringComparison.Ordinal);
        Assert.Contains("ProgressionScheduleService.SchedulePath", statusAudit, StringComparison.Ordinal);
        Assert.Contains("ProgressionScheduleService.ReportPath", statusAudit, StringComparison.Ordinal);
        Assert.Contains("afterlifeCatchupContours", statusAudit, StringComparison.Ordinal);
        Assert.Contains("progressionControl", statusAudit, StringComparison.Ordinal);
        Assert.Contains("WriteAfterlifeProgressionAuditPanelsAsync", statusAudit, StringComparison.Ordinal);
        Assert.Contains("Полный JSON progression_schedule.json", statusAudit, StringComparison.Ordinal);
        Assert.Contains("Полный JSON input/turn_request.json.progressionControl", statusAudit, StringComparison.Ordinal);
        Assert.Contains("Полный JSON progression_report.progressionProcessingReport", statusAudit, StringComparison.Ordinal);
    }

    [Fact]
    public void SourceOfLightScreensUseRussianPlayerFacingStateLabels()
    {
        var sourceOfLight = ReadSource("UI", "ExplorerMode", "ExplorerMode.Afterlife.SourceOfLight.cs");

        Assert.Contains("Вершинная сцена Сияющей Обители", sourceOfLight, StringComparison.Ordinal);
        Assert.Contains("Источник Света закрыт", sourceOfLight, StringComparison.Ordinal);
        Assert.Contains("Источник Света ожидает закрытия", sourceOfLight, StringComparison.Ordinal);
        Assert.Contains("Источник Света завершён", sourceOfLight, StringComparison.Ordinal);
        Assert.Contains("Награда выдаётся один раз на душу", sourceOfLight, StringComparison.Ordinal);

        Assert.DoesNotContain("Locked Source of Light", sourceOfLight, StringComparison.Ordinal);
        Assert.DoesNotContain("Pending Source of Light", sourceOfLight, StringComparison.Ordinal);
        Assert.DoesNotContain("Completed Source of Light", sourceOfLight, StringComparison.Ordinal);
        Assert.DoesNotContain("capstone-сцена", sourceOfLight, StringComparison.Ordinal);
        Assert.DoesNotContain("one-per-soul", sourceOfLight, StringComparison.Ordinal);
    }

    [Fact]
    public void AfterlifeExplorerPanelsAvoidEnglishContractJargonInVisibleText()
    {
        var sources = string.Join("\n", new[]
        {
            ReadSource("UI", "ExplorerMode", "ExplorerMode.Afterlife.ChaosSea.ActionPreviews.cs"),
            ReadSource("UI", "ExplorerMode", "ExplorerMode.Afterlife.InkFeathersAndOfferings.cs"),
            ReadSource("UI", "ExplorerMode", "ExplorerMode.Afterlife.GuardiansProjectsTrade.cs"),
            ReadSource("UI", "ExplorerMode", "ExplorerMode.PrivateImplementation.cs"),
            ReadSource("UI", "ExplorerMode", "ExplorerMode.Afterlife.PlayerGuardianFoundation.cs"),
            ReadSource("UI", "ExplorerMode", "ExplorerMode.Afterlife.ShiningAbode.cs"),
            ReadSource("UI", "ExplorerMode", "ExplorerMode.Afterlife.ShiningAbode.ActionPreviews.cs"),
            ReadSource("UI", "ExplorerMode", "ExplorerMode.Afterlife.ShiningAbode.Treasury.cs"),
            ReadSource("UI", "ExplorerMode", "ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs"),
            ReadSource("UI", "ExplorerMode", "ExplorerMode.Afterlife.SoulRelicsArchiveInbox.cs"),
            ReadSource("UI", "ExplorerMode", "ExplorerMode.Afterlife.StatusAudit.cs")
        });

        foreach (var leakedPhrase in new[]
        {
            "Lifecycle:",
            "ordinary afterlife-turn contract",
            "Guardian-forced incarnation exception",
            "Mortal NPC/factions",
            "Target guardian:",
            "GM closure contract",
            "Required fields:",
            "Required receipt fields:",
            "Action tag:",
            "GM turn",
            "client-authored contract",
            "GM materialization contract",
            "Return cycle:",
            "Power gain formula:",
            "client-local mutation",
            "client-local coordinated write",
            "afterlife Ink Feather action",
            "fail-closed",
            "before/after"
        })
        {
            Assert.DoesNotContain(leakedPhrase, sources, StringComparison.Ordinal);
        }
    }

    private static string ReadSource(params string[] pathParts) =>
        File.ReadAllText(Path.Combine(
            new[] { TestRepoPaths.RepoRoot, "BookOfEternityClient" }.Concat(pathParts).ToArray()));
}
