using System.Text.Json;
using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests.WebUi;

public sealed partial class BrowserShiningRelicForgeParityTests
{
    [Fact]
    public async Task StorageUnknown_OriginalBrowserForgeSubmitDoesNotCompensateSanitizedFailure()
    {
        void Output(string line) => _storageOutcomeOutput?.WriteLine(line);
        using var outerRoot = new CleanupOwnedFixture(_rootPath, Output);
        Assert.True(OperatingSystem.IsLinux());
        var probe = new ExplorerStorageProbe();
        using var fixture = new BrowserShiningRelicForgeParityTests(probe.Hooks, null);
        using var owned = new CleanupOwnedFixture(fixture._rootPath, Output);
        using var observer = probe;
        probe.Attach(fixture._fs);
        await fixture.SeedShiningRelicForgeStateAsync();
        var prompt = await fixture.ExecuteCommandAsync("/shining_relic_forge");
        Assert.Equal(CommandExecutionState.RequiresInput, prompt.State);
        var lockBefore = File.ReadAllBytes(fixture._fs.ResolvePath(LocalUiSessionLockService.LockPath));
        Dictionary<string, byte[]?>? priorAtCut = null;
        probe.Cut.Select = (path, _) => probe.IsForgeMember(path, fixture._fs);
        probe.Cut.BeforeCut = () =>
        {
            using var journal = CleanupPublicationCut.Metadata(File.ReadAllBytes(probe.Cut.JournalPath));
            var target = journal.RootElement.GetProperty("Members")[0].GetProperty("Path").GetString();
            priorAtCut = probe.Committed.Where(x => x.Key != target)
                .ToDictionary(x => x.Key, x => CleanupPublicationCut.ReadOptional(x.Key), StringComparer.Ordinal);
            foreach (var pair in priorAtCut) Assert.Equal(probe.Committed[pair.Key], pair.Value);
        };
        probe.Cut.Armed = true;
        ExplorerCommandResult? result = null;
        var failure = await Record.ExceptionAsync(async () => result = await fixture.SubmitPromptAsync(prompt,
            Answers(("faction_id", "faction_lanterns"), ("forge_action_type", ShiningCoreActionRequestState.ActionTypeForgeRelicReshape),
                ("relic_id", "relic_blade"), ("target_form_tag", "lance"), ("relic_rerolls_to_commit", 1),
                ("confirm_shining_relic_forge_write", true))));
        var lockAfter = CleanupPublicationCut.ReadOptional(fixture._fs.ResolvePath(LocalUiSessionLockService.LockPath));
        var afterImages = priorAtCut?.ToDictionary(x => x.Key, x => CleanupPublicationCut.ReadOptional(x.Key), StringComparer.Ordinal);
        Output(JsonSerializer.Serialize(new { result, Failure = failure?.ToString(), lockBefore, lockAfter,
            priorAtCut, afterImages, probe.RequestAttempts, Cut = probe.Cut.Evidence() }));
        probe.Cut.AssertReachedAndStopped();
        Assert.Null(failure); Assert.NotNull(result); Assert.Equal(CommandExecutionState.Failed, result!.State);
        Assert.Equal(lockBefore, lockAfter); Assert.Equal(0, probe.RequestAttempts);
        Assert.NotNull(priorAtCut);
        foreach (var pair in priorAtCut!) Assert.Equal(pair.Value, afterImages![pair.Key]);
    }
}
