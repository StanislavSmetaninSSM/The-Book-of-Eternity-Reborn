using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ExplorerModeSourceGuardTests
{
    private static string ReadGameEngineSource()
    {
        var rootFile = Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient", "Core", "GameEngine.cs");
        var partialDir = Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient", "Core", "GameEngine");

        var files = new List<string> { rootFile };
        if (Directory.Exists(partialDir))
            files.AddRange(Directory.GetFiles(partialDir, "*.cs", SearchOption.TopDirectoryOnly).OrderBy(path => path, StringComparer.OrdinalIgnoreCase));

        return string.Join(Environment.NewLine + Environment.NewLine, files.Select(File.ReadAllText));
    }

    private static string ReadExplorerModeSource()
    {
        var rootFile = Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient", "UI", "ExplorerMode.cs");
        var partialDir = Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient", "UI", "ExplorerMode");

        var files = new List<string> { rootFile };
        if (Directory.Exists(partialDir))
            files.AddRange(Directory.GetFiles(partialDir, "*.cs", SearchOption.TopDirectoryOnly).OrderBy(path => path, StringComparer.OrdinalIgnoreCase));

        return string.Join(Environment.NewLine + Environment.NewLine, files.Select(File.ReadAllText));
    }

    [Fact]
    public void ExplorerMode_MustUseConsoleAdapterInsteadOfDirectAnsiConsoleCalls()
    {
        var source = ReadExplorerModeSource();

        Assert.DoesNotContain("AnsiConsole.", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Console.ReadKey(true)", source, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("BookOfEternityClient/UI/ExplorerMode.cs")]
    [InlineData("BookOfEternityClient/Core/GameEngine.cs")]
    public void DynamicJoinedMarkupBlocks_MustUseSafeMarkup(string relativePath)
    {
        var source = string.Equals(relativePath, "BookOfEternityClient/UI/ExplorerMode.cs", StringComparison.OrdinalIgnoreCase)
            ? ReadExplorerModeSource()
            : ReadGameEngineSource();

        Assert.DoesNotContain("new Markup(string.Join(\"\\n\", ", source, StringComparison.Ordinal);
    }

    [Fact]
    public void LargeWorldDescriptionEditor_MustNotHaveDedicatedClipboardActionOrLegacyDoneSentinel()
    {
        var source = ReadExplorerModeSource();

        Assert.DoesNotContain("📋 Вставить из буфера обмена", source, StringComparison.Ordinal);
        Assert.DoesNotContain("::done", source, StringComparison.Ordinal);
        Assert.Contains("TextComposer.Read(", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ExplorerMode_MustUseSharedReputationDisplayInsteadOfLocalTierHelpers()
    {
        var source = ReadExplorerModeSource();

        Assert.DoesNotContain("private static (string label, string color) GetNpcRelationshipTier", source, StringComparison.Ordinal);
        Assert.DoesNotContain("private static (string label, string color) GetReputationLabel", source, StringComparison.Ordinal);
        Assert.Contains("ReputationDisplay.", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ExplorerMode_MustUseSharedNpcTradeAvailabilityInsteadOfLocalTradeGateHelpers()
    {
        var source = ReadExplorerModeSource();

        Assert.DoesNotContain("private static bool NpcTradeAvailableHere", source, StringComparison.Ordinal);
        Assert.DoesNotContain("private static string? GetNpcTradeBlockedReason", source, StringComparison.Ordinal);
        Assert.DoesNotContain("private static string? GetNpcMerchantProfile(JsonElement", source, StringComparison.Ordinal);
        Assert.Contains("NpcTradeService.EvaluateTradeAvailability", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ExplorerMode_GuardianUi_MustUseSharedManifestationResolver()
    {
        var source = ReadExplorerModeSource();

        Assert.Contains("GuardianManifestation.GetDisplayName", source, StringComparison.Ordinal);
        Assert.Contains("GuardianManifestation.GetCanonicalName", source, StringComparison.Ordinal);
        Assert.DoesNotContain("GetStr(g, \"name\"", source, StringComparison.Ordinal);
    }

    [Fact]
    public void WorldSetupAndWorldRules_UiLabels_MustNotLeakEnglishMenuText()
    {
        var source = ReadExplorerModeSource();

        Assert.DoesNotContain(" 🌍 World Setup ", source, StringComparison.Ordinal);
        Assert.DoesNotContain(" 👁 Pending World Setup ", source, StringComparison.Ordinal);
        Assert.DoesNotContain(" 📜 World Directives ", source, StringComparison.Ordinal);
        Assert.DoesNotContain(" 📚 World Profile ", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Создать / редактировать pending setup", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Очистить pending setup", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Pending world setup", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Текущий pending setup", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Очистить world directives", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Создать / редактировать world directives", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ExplorerMode_RivalSoulArcSignals_MustHavePlayerFacingMarker()
    {
        var source = ReadExplorerModeSource();

        Assert.Contains("🧵 Чужая нить судьбы", source, StringComparison.Ordinal);
        Assert.Contains("relatedRivalArcId", source, StringComparison.Ordinal);
        Assert.Contains("Что уже изменилось в мире", source, StringComparison.Ordinal);
        Assert.Contains("Последнее проявление", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ExplorerMode_GuardianProjects_MustExposeDedicatedJournalCommand()
    {
        var source = ReadExplorerModeSource();

        Assert.Contains("/проекты_хранителей", source, StringComparison.Ordinal);
        Assert.Contains("ShowGuardianProjects", source, StringComparison.Ordinal);
        Assert.Contains("GuardianProjectState.JournalPath", source, StringComparison.Ordinal);
        Assert.Contains("GuardianProjectState.TrackerPath", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ExplorerMode_MustExposeGuardianCorrectionsAndScenarioCoreReview()
    {
        var source = ReadExplorerModeSource();

        Assert.Contains("/коррективы_хранителя", source, StringComparison.Ordinal);
        Assert.Contains("ShowGuardianCorrections", source, StringComparison.Ordinal);
        Assert.Contains("Просмотреть сценарное ядро", source, StringComparison.Ordinal);
        Assert.Contains("Подтвердить извлечённые факты", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ExplorerMode_MustExposeDedicatedAbodePowerJournalCommand()
    {
        var source = ReadExplorerModeSource();

        Assert.Contains("/сила_обители", source, StringComparison.Ordinal);
        Assert.Contains("ShowAbodePower", source, StringComparison.Ordinal);
        Assert.Contains("GuardianPowerEventState.JournalPath", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ExplorerMode_MustExposeDedicatedAbodeOfferingCommand()
    {
        var source = ReadExplorerModeSource();

        Assert.Contains("/подношение_обители", source, StringComparison.Ordinal);
        Assert.Contains("ShowAbodeOffering", source, StringComparison.Ordinal);
        Assert.Contains("GuardianAbodeOfferingState.PendingRequestPath", source, StringComparison.Ordinal);
        Assert.Contains("GuardianAbodeOfferingState.ActionTag", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ExplorerMode_MustExposeAfterlifeArchiveCommand()
    {
        var source = ReadExplorerModeSource();

        Assert.Contains("/архив_души", source, StringComparison.Ordinal);
        Assert.Contains("ShowAfterlifeArchive", source, StringComparison.Ordinal);
        Assert.Contains("afterlifeArchive", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ExplorerMode_MustExposeArchiveCandidateCommand()
    {
        var source = ReadExplorerModeSource();

        Assert.Contains("/архив_кандидаты", source, StringComparison.Ordinal);
        Assert.Contains("ShowAfterlifeArchiveCandidates", source, StringComparison.Ordinal);
        Assert.Contains("AfterlifeArchiveCandidateService", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ExplorerMode_ArchiveInteractions_MustUsePendingRequestFlowInsteadOfExplicitOptionsOrAffinityLogic()
    {
        var source = ReadExplorerModeSource();

        Assert.DoesNotContain("AfterlifeArchiveAffinityService", source, StringComparison.Ordinal);
        Assert.DoesNotContain("AfterlifeArchiveInteractionOptionsService", source, StringComparison.Ordinal);
        Assert.Contains("CreateRequestAsync(", source, StringComparison.Ordinal);
        Assert.Contains("_pendingGmAction = result.PendingGmAction", source, StringComparison.Ordinal);
        Assert.DoesNotContain("fit ", source, StringComparison.Ordinal);
    }
}
