using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class WoundPlayerStatusSourceGuardTests
{
    [Fact]
    public void StatusEntryPoints_RetireLooseReadersAndNormalMirrorRepair()
    {
        var browser = Read("BookOfEternityClient/WebUi/ExplorerWebCommandService.cs");
        Assert.Contains("if (descriptor.Id != \"status\" && descriptor.BrowserHandlerKind != ExplorerCommandBrowserHandlerKind.Wounds)\n            await _stateManager.RefreshGameStateAsync();", browser);
        var builder = Read("BookOfEternityClient/UI/ExplorerUniversalMetaCommandResultBuilder.cs");
        Assert.Contains("if (kind != CommandKind.Status)\n            await stateManager.RefreshGameStateAsync();", builder);
        Assert.Contains("WoundPlayerStatusReadService.LoadAsync(stateManager, fs)", builder);
        Assert.Contains("WoundPlayerStatusBlocks.Build(status.Wounds)", builder);
        Assert.DoesNotContain("AddMortalStatusWoundBlocks", builder);
        Assert.DoesNotContain("TranslateWoundSeverity", builder);
        var console = Section(Read("BookOfEternityClient/UI/ExplorerMode/ExplorerMode.WorldAndStatus.cs"),
            "    private async Task ShowDetailedStatus()", "    private async Task ShowSkills()");
        Assert.Contains("WoundPlayerStatusReadService.LoadAsync(_stateManager, _fs)", console);
        Assert.Contains("var state = status.State;", console);
        Assert.Contains("RenderWoundStatus(status.Wounds)", console);
        Assert.DoesNotContain("RefreshGameStateAsync", console);
        Assert.DoesNotContain("game_state/player/wounds.json", console);
        Assert.DoesNotContain("AppendStatusWoundPreview", console);
        var afterlife = Section(Read("BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.StatusAudit.cs"),
            "    private async Task ShowAfterlifeDetailedStatusAsync(", "\n    private ");
        Assert.Contains("WoundPlayerStatusSnapshot status", afterlife);
        Assert.Contains("var state = status.State;", afterlife);
        Assert.Contains("RenderWoundStatus(status.Wounds)", afterlife);
        Assert.DoesNotContain("RefreshGameStateAsync", afterlife);
        Assert.DoesNotContain("_stateManager.CurrentState", afterlife);
        var header = Section(afterlife, "\"[bold]Область и душа:[/]\"", "\"[bold]Ресурсы души:[/]\"");
        Assert.DoesNotContain("soulRoot", header);
        Assert.Contains("state.SoulName", header);
        Assert.Contains("state.SoulFormDescription", header);
        Assert.Contains("state.Incarnation", header);
        var renderer = Read("BookOfEternityClient/UI/ExplorerMode/ExplorerMode.PrivateImplementation.cs");
        Assert.DoesNotContain("AppendStatusWoundPreview", renderer);
        Assert.Contains("WoundPlayerStatusBlocks.Build(projection)", renderer);
    }

    [Fact]
    public void StatusSnapshot_UsesOneReadBoundaryAndExistingTextRenderersOnly()
    {
        var reader = Read("BookOfEternityClient/Services/WoundPlayerStatusReadService.cs");
        Assert.Equal(1, reader.Split("AcquireCanonicalWriteLeaseAsync", StringSplitOptions.None).Length - 1);
        Assert.Contains("CanonicalWritePurpose.PublicationReadQuiescence", reader);
        Assert.Contains("stateManager.RefreshGameStateReadOnlyAsync(lease)", reader);
        Assert.Contains("var state = stateManager.CurrentState;", reader);
        Assert.Contains("WoundPlayerReadService.LoadAsync(fs, lease)", reader);
        Assert.Contains("return new(state, wounds)", reader);
        Assert.DoesNotContain("RefreshGameStateAsync", reader);
        var blocks = Read("BookOfEternityClient/UI/WoundPlayerStatusBlocks.cs");
        Assert.Contains("new UiEntityDossierBlock", blocks);
        Assert.Contains("new UiEntityCard", blocks);
        foreach (var forbidden in new[] { "new UiAction", "UiRawJsonBlock", "JsonNode", "WriteFile", "DeleteFile", "PrimaryAction =" })
            Assert.DoesNotContain(forbidden, blocks);
        var frontend = Read("BookOfEternityClient.WebFrontend/src/components/BlockRenderer.tsx");
        Assert.Contains("<Heading>{card.title}</Heading>", frontend);
        Assert.Contains("{card.summary}", frontend);
        Assert.DoesNotContain("dangerouslySetInnerHTML", frontend);
    }

    private static string Read(string path) => File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, path)).Replace("\r\n", "\n");
    private static string Section(string source, string start, string end)
    {
        var first = source.IndexOf(start, StringComparison.Ordinal);
        Assert.True(first >= 0);
        var last = source.IndexOf(end, first + start.Length, StringComparison.Ordinal);
        Assert.True(last > first);
        return source[first..last];
    }
}
