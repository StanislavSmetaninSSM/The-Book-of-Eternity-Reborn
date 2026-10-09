using System.Text.Json;
using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Core;
using BookOfEternityClient.WebUi;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests.WebUi;

public sealed partial class BrowserShiningRelicForgeParityTests
{
    [Fact]
    public Task StorageUnknown_OriginalBrowserForgeSubmitDoesNotCompensateSanitizedFailure() =>
        RunOriginalBrowserForgeStorageOutcomeAsync(committedRelease: false);

    [Fact]
    public Task StorageUnknown_OriginalBrowserForgeSubmitRetainsCommittedFollowUp() =>
        RunOriginalBrowserForgeStorageOutcomeAsync(committedRelease: true);

    private async Task RunOriginalBrowserForgeStorageOutcomeAsync(bool committedRelease)
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
        byte[]? committedMarker = null;
        Dictionary<string, byte[]?>? domainCommitted = null;
        probe.AfterCommitted = path =>
        {
            if (!committedRelease) return;
            if (probe.IsForgeMember(path, fixture._fs))
            {
                probe.AssertForgeJournal(fixture._fs);
                using var journal = CleanupPublicationCut.Metadata(File.ReadAllBytes(probe.Cut.JournalPath));
                Assert.True(journal.RootElement.GetProperty("Committed").GetBoolean());
                domainCommitted = journal.RootElement.GetProperty("Members").EnumerateArray()
                    .ToDictionary(x => x.GetProperty("Path").GetString()!,
                        x => CleanupPublicationCut.ReadOptional(x.GetProperty("Path").GetString()!), StringComparer.Ordinal);
            }
            if (path.EndsWith("browser_write_committed.marker", StringComparison.Ordinal) && File.Exists(path))
                committedMarker = File.ReadAllBytes(path);
        };
        probe.Cut.Select = (path, member) => committedRelease
            ? path == fixture._fs.ResolvePath(LocalUiSessionLockService.LockPath) &&
                !member.GetProperty("After").GetProperty("Exists").GetBoolean()
            : probe.IsForgeMember(path, fixture._fs);
        probe.Cut.BeforeCut = () =>
        {
            if (committedRelease)
            {
                Assert.NotNull(committedMarker);
                Assert.NotNull(domainCommitted);
                Assert.Contains(fixture._fs.ResolvePath(ShiningCoreActionRequestState.PendingActionsRequestPath), domainCommitted!.Keys);
                Assert.Contains(fixture._fs.ResolvePath(ResourceMaterializationContract.StatePath), domainCommitted.Keys);
                foreach (var pair in domainCommitted) Assert.Equal(pair.Value, CleanupPublicationCut.ReadOptional(pair.Key));
            }
            else probe.AssertForgeJournal(fixture._fs);
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
        var domainAfter = domainCommitted?.ToDictionary(x => x.Key, x => CleanupPublicationCut.ReadOptional(x.Key), StringComparer.Ordinal);
        var afterImages = priorAtCut?.ToDictionary(x => x.Key, x => CleanupPublicationCut.ReadOptional(x.Key), StringComparer.Ordinal);
        var sessions = (System.Collections.IDictionary)typeof(ExplorerWebPromptSessionService)
            .GetField("_sessions", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(fixture._promptSessions)!;
        var formRetired = !sessions.Contains(prompt.InteractiveSession!.SessionId);
        var notificationText = result == null ? null : string.Join("\n", result.Notifications.Select(x => x.Title + "\n" + x.Message));
        Output(JsonSerializer.Serialize(new { committedRelease, committedMarker, domainCommitted, domainAfter, result, formRetired, notificationText, Failure = failure?.ToString(), lockBefore, lockAfter,
            priorAtCut, afterImages, probe.RequestAttempts, Cut = probe.Cut.Evidence() }));
        probe.Cut.AssertReachedAndStopped();
        Assert.Null(failure); Assert.NotNull(result); Assert.Equal(committedRelease ? CommandExecutionState.Completed : CommandExecutionState.Failed, result!.State);
        Assert.True(formRetired); Assert.Null(result.InteractiveSession); Assert.Empty(result.Prompts);
        Assert.NotNull(notificationText);
        Assert.Contains(committedRelease ? "Локальная запись подтверждена" : "Результат локальной записи не подтверждён", notificationText, StringComparison.Ordinal);
        Assert.Contains(result.Notifications, x => x.Severity == UiNotificationSeverity.Warning);
        Assert.Contains("не повторяйте", notificationText, StringComparison.OrdinalIgnoreCase);
        AssertNoRawShiningDiagnosticText(notificationText!);
        Assert.DoesNotContain(fixture._rootPath, notificationText, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(CoordinatedStatePublicationUncertainException), notificationText, StringComparison.Ordinal);
        Assert.DoesNotContain("actual cleanup MemberPublished cut", notificationText, StringComparison.Ordinal);
        if (committedRelease)
        {
            Assert.NotNull(committedMarker); Assert.NotNull(domainCommitted);
            foreach (var pair in domainCommitted!) Assert.Equal(pair.Value, domainAfter![pair.Key]);
            Assert.NotEqual(lockBefore, lockAfter);
        }
        else Assert.Equal(lockBefore, lockAfter);
        Assert.Equal(0, probe.RequestAttempts);
        Assert.NotNull(priorAtCut);
        foreach (var pair in priorAtCut!) Assert.Equal(pair.Value, afterImages![pair.Key]);
    }
}
