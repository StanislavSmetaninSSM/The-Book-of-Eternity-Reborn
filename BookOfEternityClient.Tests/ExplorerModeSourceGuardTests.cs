using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ExplorerModeSourceGuardTests
{
    [Fact]
    public void ExplorerMode_MustUseConsoleAdapterInsteadOfDirectAnsiConsoleCalls()
    {
        var path = Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient", "UI", "ExplorerMode.cs");
        var source = File.ReadAllText(path);

        Assert.DoesNotContain("AnsiConsole.", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Console.ReadKey(true)", source, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("BookOfEternityClient/UI/ExplorerMode.cs")]
    [InlineData("BookOfEternityClient/Core/GameEngine.cs")]
    public void DynamicJoinedMarkupBlocks_MustUseSafeMarkup(string relativePath)
    {
        var path = Path.Combine(TestRepoPaths.RepoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        var source = File.ReadAllText(path);

        Assert.DoesNotContain("new Markup(string.Join(\"\\n\", ", source, StringComparison.Ordinal);
    }

    [Fact]
    public void LargeWorldDescriptionEditor_MustNotHaveDedicatedClipboardActionOrLegacyDoneSentinel()
    {
        var path = Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient", "UI", "ExplorerMode.cs");
        var source = File.ReadAllText(path);

        Assert.DoesNotContain("📋 Вставить из буфера обмена", source, StringComparison.Ordinal);
        Assert.DoesNotContain("::done", source, StringComparison.Ordinal);
        Assert.Contains("TextComposer.Read(", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ExplorerMode_MustUseSharedReputationDisplayInsteadOfLocalTierHelpers()
    {
        var path = Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient", "UI", "ExplorerMode.cs");
        var source = File.ReadAllText(path);

        Assert.DoesNotContain("private static (string label, string color) GetNpcRelationshipTier", source, StringComparison.Ordinal);
        Assert.DoesNotContain("private static (string label, string color) GetReputationLabel", source, StringComparison.Ordinal);
        Assert.Contains("ReputationDisplay.", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ExplorerMode_MustUseSharedNpcTradeAvailabilityInsteadOfLocalTradeGateHelpers()
    {
        var path = Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient", "UI", "ExplorerMode.cs");
        var source = File.ReadAllText(path);

        Assert.DoesNotContain("private static bool NpcTradeAvailableHere", source, StringComparison.Ordinal);
        Assert.DoesNotContain("private static string? GetNpcTradeBlockedReason", source, StringComparison.Ordinal);
        Assert.DoesNotContain("private static string? GetNpcMerchantProfile(JsonElement", source, StringComparison.Ordinal);
        Assert.Contains("NpcTradeService.EvaluateTradeAvailability", source, StringComparison.Ordinal);
    }

    [Fact]
    public void WorldSetupAndWorldRules_UiLabels_MustNotLeakEnglishMenuText()
    {
        var path = Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient", "UI", "ExplorerMode.cs");
        var source = File.ReadAllText(path);

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
}
