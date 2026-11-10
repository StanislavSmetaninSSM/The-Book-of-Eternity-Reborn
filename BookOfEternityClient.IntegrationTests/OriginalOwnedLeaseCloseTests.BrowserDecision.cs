using System.Text.Json;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.Services.GmRuntime;
using BookOfEternityClient.UI;
using BookOfEternityClient.WebUi;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class OriginalOwnedLeaseCloseTests
{
    private sealed record OriginalBrowserProjection(BrowserLocalWriteResult Result);

    [Theory]
    [InlineData("committed", false, "browser")]
    [InlineData("committed", true, "browser")]
    [InlineData("rollback", false, "browser")]
    [InlineData("rollback", true, "browser")]
    [InlineData("uncertain", false, "browser")]
    [InlineData("uncertain", true, "browser")]
    [InlineData("committed", true, "projection")]
    [InlineData("rollback", true, "projection")]
    [InlineData("uncertain", true, "projection")]
    [InlineData("committed", true, "nested")]
    [InlineData("committed", true, "settings")]
    [InlineData("committed", true, "incomplete")]
    public async Task OriginalBrowserOwnedClosePreservesEstablishedDecision(string decision, bool failClose, string route)
    {
        Assert.True(OperatingSystem.IsLinux());
        var root = Path.Combine(Path.GetTempPath(), "boe-browser-established-close-" + Guid.NewGuid().ToString("N"));
        using var owned = new CleanupOwnedFixture(root, output.WriteLine);
        using var cut = new CleanupPublicationCut();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allow = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var acquireEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowAcquire = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var launchArmed = false; var acquisitionPauses = 0;
        var ready = false; var settingsCommitted = false; var closing = false;
        var laterAdmissions = 0; var closingAdmissions = 0; var pauses = 0; var writes = 0;
        FileSystemManager? files = null;
        var settings = new GameSettings { MusicEnabled = false, SoundEnabled = false };
        byte[]? actualCommittedConfig = null; byte[]? actualCommittedProjection = null;
        files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
            {
                BeforeCanonicalMutationBoundaryAsync = async path =>
                {
                    await cut.Hooks.BeforeCanonicalMutationBoundaryAsync!(path);
                    if (route == "settings" && settingsCommitted && settings.Language == "en" &&
                        path == LocalUiSessionLockService.LockPath && pauses == 0)
                    {
                        pauses++; ready = true; entered.TrySetResult(); await allow.Task;
                    }
                },
                LocalPublicationObserver = (phase, index) =>
                {
                    if (launchArmed && route == "settings" && phase == TrustedLocalPublicationPhase.Committed)
                    {
                        using var journal = CleanupPublicationCut.Metadata(File.ReadAllBytes(cut.JournalPath));
                        var cohort = journal.RootElement.GetProperty("Members").EnumerateArray()
                            .Select(m => m.GetProperty("Path").GetString()).ToArray();
                        if (cohort.Contains(files!.ResolvePath("config.json"), StringComparer.Ordinal) &&
                            cohort.Contains(files.ResolvePath("game_state/core/game_settings.json"), StringComparer.Ordinal))
                        { settingsCommitted = true; actualCommittedConfig = File.ReadAllBytes(files!.ResolvePath("config.json"));
                          actualCommittedProjection = File.ReadAllBytes(files!.ResolvePath("game_state/core/game_settings.json")); }
                    }
                    cut.Hooks.LocalPublicationObserver!(phase, index);
                },
                AfterCanonicalReadInitialValidationAsync = cut.Hooks.AfterCanonicalReadInitialValidationAsync,
                LocalPublicationRecoveryObserver = cut.Hooks.LocalPublicationRecoveryObserver,
                SessionOperationClosingAsync = async () =>
                { closing = true; await cut.Hooks.SessionOperationClosingAsync!(); },
                BeforeCanonicalWriteLockOpenAsync = async () =>
                {
                    if (ready) { if (closing) closingAdmissions++; else laterAdmissions++; }
                    closing = false; await cut.Hooks.BeforeCanonicalWriteLockOpenAsync!();
                    if (launchArmed && acquisitionPauses == 0)
                    { acquisitionPauses++; acquireEntered.TrySetResult(); await allowAcquire.Task; }
                }
            });
        cut.Attach(files);
        var state = new StateManager(files, settings, NullLogger<StateManager>.Instance);
        var generation = await state.BootstrapLocalStorageAsync();
        settingsCommitted = false; actualCommittedConfig = null; actualCommittedProjection = null;
        const string member = "game_state/world/weather.json";
        byte[] before = [0xEF, 0xBB, 0xBF, 21, 0, 0xFF]; byte[] after = [71, 73, 79];
        await files.WriteFileAtomicBytesAsync(member, before);
        var generationBefore = File.ReadAllBytes(files.SessionGenerationPath);
        var coordinator = new BrowserLocalWriteCoordinator(files, new LocalUiSessionLockService(files));
        await using var audio = new AudioService(files, settings, NullLogger<AudioService>.Instance);
        var service = new BrowserClientSettingsService(files, state, audio, coordinator, new LocalizationManager());
        var request = new BrowserLocalWriteRequest("original-close", "Fixture", "original owner close");
        var writeFailure = new InvalidOperationException("Actual original callback requests exact rollback.");
        var callbackFailure = new IOException("Actual original callback fails after established atomic result.");
        var releaseFailure = new IOException("Actual original browser owning lease close fails.");
        var closer = new ThrowingClose(releaseFailure);
        BrowserLocalWriteResult? actualDecision = null;
        OriginalBrowserProjection? actualProjection = null;
        FileSystemManager.CanonicalWriteLease? original = null;
        string? ownerState = null; object? returned = null; Exception? failure = null;
        cut.Select = (path, _) => path == files.ResolvePath(member);
        cut.Armed = decision == "uncertain";
        async Task<BrowserLocalWriteResult> PublishAndPause(FileSystemManager.CanonicalWriteLease lease)
        {
            actualDecision = await coordinator.ExecuteAtomicWithinTransactionAsync(lease, request, [member], async held =>
            {
                writes++;
                await files.WriteFileAtomicBytesAsync(held, member, after);
                if (decision == "rollback") throw writeFailure;
            });
            Assert.Null(lease.ExternalPublicationContext);
            pauses++; ready = true; entered.TrySetResult(); await allow.Task;
            if (route == "incomplete") throw callbackFailure;
            return actualDecision;
        }
        async Task<object> StartOriginalRoute()
        {
            if (route == "settings") return await service.UpdateAsync(new("en", "hard", false, false, 63, false, 0, 175, 125, true, true));
            if (route == "projection") return await coordinator.RunBoundTransactionAsync(async lease =>
            {
                var value = await PublishAndPause(lease);
                actualProjection = new OriginalBrowserProjection(value); return actualProjection;
            });
            if (route == "nested") return await SessionOperationContext.RunBoundAsync(files, generation,
                () => coordinator.RunBoundTransactionAsync(PublishAndPause));
            return await coordinator.RunBoundTransactionAsync(PublishAndPause);
        }
        launchArmed = true;
        var operation = StartOriginalRoute();
        try
        {
            Assert.Same(acquireEntered.Task, await Task.WhenAny(acquireEntered.Task, operation).WaitAsync(TimeSpan.FromSeconds(12)));
            allowAcquire.TrySetResult();
            Assert.Same(entered.Task, await Task.WhenAny(entered.Task, operation).WaitAsync(TimeSpan.FromSeconds(12)));
            (original, ownerState) = InspectOriginalNestedOwningLease(operation, "RunBoundTransactionAsync");
            Assert.Same(files, original.Owner); Assert.True(original.IsActive); Assert.Null(original.ExternalPublicationContext);
            if (route == "settings")
            {
                Assert.True(settingsCommitted); Assert.NotNull(actualCommittedConfig);
                Assert.Equal("en", settings.Language); Assert.Equal(63, settings.MusicVolume);
            }
            else
            {
                Assert.NotNull(actualDecision);
                Assert.Equal(decision == "committed" ? BrowserPreparedWriteDisposition.Committed : decision == "rollback"
                    ? BrowserPreparedWriteDisposition.RolledBack : BrowserPreparedWriteDisposition.Uncertain, actualDecision.Disposition);
                Assert.Equal(decision == "committed", actualDecision.Success); Assert.False(actualDecision.IsBlocked);
            }
            if (failClose) original.ExternalPublicationContext = closer;
            allow.TrySetResult();
            failure = await Record.ExceptionAsync(async () => returned = await operation);
        }
        finally
        {
            allowAcquire.TrySetResult(); allow.TrySetResult();
            if (!operation.IsCompleted) await Record.ExceptionAsync(async () => await operation);
        }
        bool lockAvailable;
        using (var probe = new FileStream(files.CanonicalWriteLockPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) lockAvailable = true;
        var bytes = File.ReadAllBytes(files.ResolvePath(member));
        output.WriteLine(JsonSerializer.Serialize(new { root, decision, failClose, route, ownerState, pauses, writes, ready, acquisitionPauses,
            actualDecision, actualProjection, returned, failure = failure?.ToString(),
            originalCallbackRetained = ReferenceEquals(failure, callbackFailure),
            secondaryRetained = ReferenceEquals(failure?.Data["CoordinatedLeaseReleaseFailure"], releaseFailure),
            carrierOriginalClose = ReferenceEquals(failure?.InnerException, releaseFailure), closer.Calls,
            actualCommittedConfig, actualCommittedProjection, actualProjectionBytes = CleanupPublicationCut.ReadOptional(files.ResolvePath("game_state/core/game_settings.json")), actualConfig = File.ReadAllBytes(files.ResolvePath("config.json")),
            settings.Language, settings.MusicVolume, bytes, generationBefore,
            generationAfter = File.ReadAllBytes(files.SessionGenerationPath), laterAdmissions, closingAdmissions,
            lockAvailable, activeAfter = original!.IsActive, mainClosed = original.MainAdmission == null,
            ambientClosed = original.AmbientRegistration == null, contextClosed = original.ExternalPublicationContext == null,
            Cut = cut.Evidence() }));
        Assert.Equal(1, acquisitionPauses); Assert.Equal(1, pauses); Assert.Equal(failClose ? 1 : 0, closer.Calls);
        Assert.False(original.IsActive); Assert.Null(original.MainAdmission); Assert.Null(original.AmbientRegistration);
        Assert.Null(original.ExternalPublicationContext); Assert.True(lockAvailable);
        Assert.Equal(generationBefore, File.ReadAllBytes(files.SessionGenerationPath));
        Assert.False(SessionOperationContext.TryGetExpectedGeneration(files.BasePath, out _));
        if (decision == "uncertain") cut.AssertReachedAndStopped();
        else { Assert.Equal(0, cut.Cuts); Assert.False(File.Exists(cut.JournalPath)); }
        Assert.Equal(route == "settings" || decision == "rollback" ? before : decision == "uncertain" ? CleanupPublicationCut.Foreign : after, bytes);
        if (route == "incomplete")
        {
            Assert.Null(returned); Assert.Same(callbackFailure, failure);
            Assert.Same(releaseFailure, failure!.Data["CoordinatedLeaseReleaseFailure"]);
        }
        else if (route == "projection")
        {
            Assert.Null(returned);
            var carrier = Assert.IsType<MainOperationContinuationException<OriginalBrowserProjection>>(failure);
            Assert.Same(actualProjection, carrier.EstablishedResult); Assert.Same(actualDecision, carrier.EstablishedResult.Result);
            Assert.Same(releaseFailure, carrier.InnerException);
            Assert.Same(releaseFailure, carrier.Data["SessionFinalizationFailure"]);
            Assert.Equal(decision == "committed" ? MainOperationOutcome.Committed : decision == "rollback"
                ? MainOperationOutcome.RolledBack : MainOperationOutcome.Uncertain, carrier.EstablishedOutcome);
        }
        else if (route == "settings")
        {
            Assert.Null(failure); var result = Assert.IsType<BrowserClientSettingsUpdateResult>(returned);
            Assert.True(result.Success); Assert.Equal(BrowserPreparedWriteDisposition.Committed, result.Disposition);
            Assert.NotNull(result.Settings!.PersistenceWarning); Assert.Equal("en", result.Settings.Language.Value);
            Assert.Equal(63, result.Settings.Audio.MusicVolume);
            Assert.Equal(actualCommittedConfig, File.ReadAllBytes(files.ResolvePath("config.json")));
            Assert.Equal("en", settings.Language); Assert.Equal(63, settings.MusicVolume);
            Assert.NotNull(actualCommittedProjection);
            Assert.Equal(actualCommittedProjection, File.ReadAllBytes(files.ResolvePath("game_state/core/game_settings.json")));
            using var projection = JsonDocument.Parse(LocalSettingsPreparation.DecodeText(actualCommittedProjection));
            Assert.Equal("hard", projection.RootElement.GetProperty("difficulty").GetString());
            Assert.True(await BrowserAudioService.SettingsWriteGate.WaitAsync(0)); BrowserAudioService.SettingsWriteGate.Release();
        }
        else
        {
            Assert.Null(failure); var result = Assert.IsType<BrowserLocalWriteResult>(returned);
            Assert.Equal(actualDecision!.Disposition, result.Disposition); Assert.Equal(actualDecision.Success, result.Success);
            Assert.Equal(actualDecision.IsBlocked, result.IsBlocked);
            Assert.Equal(failClose || actualDecision.NeedsFollowUp, result.NeedsFollowUp);
            Assert.Equal(failClose || actualDecision.ContinuationBlocked, result.ContinuationBlocked);
            if (!failClose) Assert.Same(actualDecision, result);
        }
        if (route == "nested") { Assert.Equal(0, laterAdmissions); Assert.Equal(1, closingAdmissions); }
    }
}
