using System.Text;
using System.Text.Json;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;
using BookOfEternityClient.WebUi;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

// Original supported nested services: losing the absorbing prepared outcome would
// reopen ordinary admission and recover/read after retained unknown evidence.
public sealed class PreparedNestedSettingsOutcomeTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("settings", "config-unknown")]
    [InlineData("audio", "config-unknown")]
    [InlineData("settings", "committed-release-unknown")]
    [InlineData("audio", "committed-release-unknown")]
    [InlineData("settings", "healthy")]
    [InlineData("audio", "healthy")]
    [InlineData("settings", "rollback")]
    [InlineData("audio", "rollback")]
    [InlineData("settings", "rollback-release-unknown")]
    [InlineData("settings", "acquire-unknown")]
    public async Task OriginalServicesInNestedBindingPreservePreparedDecision(string consumer, string scenario)
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-prepared-nested-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        using var owned = new CleanupOwnedFixture(root, output.WriteLine);
        using var cut = new CleanupPublicationCut();
        FileSystemManager? files = null;
        var armed = false;
        var serviceExited = false;
        var closing = false;
        var laterAdmissions = 0;
        var closingAdmissions = 0;
        var knownCuts = 0;
        var knownFailure = new InvalidOperationException("known original prepared config publication refusal");
        byte[]? committedConfig = null;
        byte[]? committedJournal = null;
        byte[]? knownJournal = null;
        var hooks = new FileSystemManagerHooks
        {
            BeforeCanonicalMutationBoundaryAsync = cut.Hooks.BeforeCanonicalMutationBoundaryAsync,
            AfterCanonicalReadInitialValidationAsync = cut.Hooks.AfterCanonicalReadInitialValidationAsync,
            LocalPublicationRecoveryObserver = cut.Hooks.LocalPublicationRecoveryObserver,
            SessionOperationClosingAsync = async () =>
            {
                closing = true;
                await cut.Hooks.SessionOperationClosingAsync!();
            },
            BeforeCanonicalWriteLockOpenAsync = async () =>
            {
                if (serviceExited) { if (closing) closingAdmissions++; else laterAdmissions++; }
                closing = false;
                await cut.Hooks.BeforeCanonicalWriteLockOpenAsync!();
            },
            LocalPublicationObserver = (phase, index) =>
            {
                if (!armed) return;
                if (phase is TrustedLocalPublicationPhase.Committed or TrustedLocalPublicationPhase.MemberPublished)
                {
                    var bytes = File.ReadAllBytes(cut.JournalPath);
                    using var journal = CleanupPublicationCut.Metadata(bytes);
                    var members = journal.RootElement.GetProperty("Members");
                    if (phase == TrustedLocalPublicationPhase.Committed && members.EnumerateArray()
                        .Any(member => member.GetProperty("Path").GetString() == files!.ResolvePath("config.json")))
                    {
                        committedJournal = bytes;
                        committedConfig = File.ReadAllBytes(files!.ResolvePath("config.json"));
                    }
                    if (phase == TrustedLocalPublicationPhase.MemberPublished && scenario.StartsWith("rollback", StringComparison.Ordinal)
                        && knownCuts == 0 && members[index].GetProperty("Path").GetString() == files!.ResolvePath("config.json"))
                    {
                        knownJournal = bytes;
                        knownCuts++;
                        throw knownFailure;
                    }
                }
                cut.Hooks.LocalPublicationObserver!(phase, index);
            }
        };
        files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, hooks);
        cut.Attach(files);
        cut.Select = (path, member) => scenario switch
        {
            "config-unknown" => path == files.ResolvePath("config.json"),
            "acquire-unknown" => path == files.ResolvePath(LocalUiSessionLockService.LockPath) &&
                member.GetProperty("After").GetProperty("Exists").GetBoolean(),
            "committed-release-unknown" or "rollback-release-unknown" => path == files.ResolvePath(LocalUiSessionLockService.LockPath) &&
                !member.GetProperty("After").GetProperty("Exists").GetBoolean(),
            _ => false
        };
        cut.BeforeCut = () =>
        {
            if (scenario == "committed-release-unknown") { Assert.NotNull(committedJournal); Assert.NotNull(committedConfig); }
            if (scenario == "rollback-release-unknown") Assert.Equal(1, knownCuts);
        };
        var settings = new GameSettings { MusicEnabled = false, SoundEnabled = false };
        var state = new StateManager(files, settings, NullLogger<StateManager>.Instance);
        var generation = await state.BootstrapLocalStorageAsync();
        var originalConfig = Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes(
            "{\"language\":\"ru\",\"difficulty\":\"normal\",\"musicEnabled\":false,\"musicVolume\":17,\"soundEnabled\":false}")).ToArray();
        File.WriteAllBytes(files.ResolvePath("config.json"), originalConfig);
        await state.BootstrapLocalStorageAsync();
        var projectionPath = files.ResolvePath("game_state/core/game_settings.json");
        byte[] originalProjection = [0xFF, 0, 0xFE];
        File.WriteAllBytes(projectionPath, originalProjection);
        var originalGeneration = File.ReadAllBytes(files.SessionGenerationPath);
        await using var audioRuntime = new AudioService(files, settings, NullLogger<AudioService>.Instance);
        var coordinator = new BrowserLocalWriteCoordinator(files, new LocalUiSessionLockService(files));
        var service = new BrowserClientSettingsService(files, state, audioRuntime, coordinator, new LocalizationManager());
        var audio = new BrowserAudioService(files, state, audioRuntime, coordinator);
        object? originalResult = null;
        Exception? originalError = null;
        object? returned = null;
        Exception? failure = null;
        armed = cut.Armed = true;
        try
        {
            returned = await SessionOperationContext.RunBoundAsync(files, generation,
                () => coordinator.RunBoundAsync<object>(async () =>
                {
                    try
                    {
                        originalResult = consumer == "settings"
                            ? await service.UpdateAsync(new("en", "hard", true, false, 63, false, 0, 175, 125, true, true))
                            : await audio.UpdateSettingsAsync(new(false, 63, false, 0));
                        return originalResult;
                    }
                    catch (Exception error) { originalError = error; throw; }
                    finally { serviceExited = true; }
                }));
        }
        catch (Exception error) { failure = error; }
        armed = cut.Armed = false;
        var actualConfig = File.ReadAllBytes(files.ResolvePath("config.json"));
        var actualProjection = CleanupPublicationCut.ReadOptional(projectionPath);
        output.WriteLine(JsonSerializer.Serialize(new { consumer, scenario, root, originalResult,
            originalError = originalError?.ToString(), returned, failure = failure?.ToString(),
            beforeConfig = originalConfig, actualConfig, originalProjection, actualProjection,
            originalGeneration, generationAfter = File.ReadAllBytes(files.SessionGenerationPath),
            committedConfig, committedJournal, knownJournal, knownCuts, laterAdmissions, closingAdmissions,
            language = settings.Language, volume = settings.MusicVolume, cut = cut.Evidence() }));
        Assert.True(serviceExited);
        Assert.Equal(originalGeneration, File.ReadAllBytes(files.SessionGenerationPath));
        Assert.True(await BrowserAudioService.SettingsWriteGate.WaitAsync(0));
        BrowserAudioService.SettingsWriteGate.Release();
        var unknown = scenario.EndsWith("unknown", StringComparison.Ordinal);
        if (unknown)
        {
            cut.AssertReachedAndStopped(requireTypedUncertainty: scenario != "config-unknown");
            Assert.Equal(0, laterAdmissions);
        }
        else
        {
            Assert.Equal(0, cut.Cuts);
            Assert.False(File.Exists(cut.JournalPath));
            Assert.False(File.Exists(files.ResolvePath(LocalUiSessionLockService.LockPath)));
        }
        var committed = scenario is "healthy" or "committed-release-unknown";
        if (committed)
        {
            Assert.Null(failure); Assert.Same(originalResult, returned);
            Assert.NotNull(committedJournal); Assert.Equal(committedConfig, actualConfig);
            Assert.Equal(63, settings.MusicVolume);
            if (consumer == "settings")
            {
                var result = Assert.IsType<BrowserClientSettingsUpdateResult>(returned);
                Assert.True(result.Success); Assert.Equal(BrowserPreparedWriteDisposition.Committed, result.Disposition);
                Assert.Equal("en", settings.Language);
                Assert.Equal(unknown, result.Settings!.PersistenceWarning != null);
            }
            else Assert.Equal(unknown, Assert.IsType<BrowserAudioSettingsDto>(returned).PersistenceWarning != null);
            if (!unknown) Assert.True(laterAdmissions > 0);
        }
        else
        {
            Assert.Equal("ru", settings.Language); Assert.Equal(17, settings.MusicVolume);
            var expected = scenario == "config-unknown" ? BrowserPreparedWriteDisposition.Uncertain
                : scenario == "acquire-unknown" ? BrowserPreparedWriteDisposition.Blocked : BrowserPreparedWriteDisposition.RolledBack;
            if (consumer == "settings")
            {
                Assert.Null(failure); Assert.Same(originalResult, returned);
                var result = Assert.IsType<BrowserClientSettingsUpdateResult>(returned);
                Assert.False(result.Success); Assert.Equal(expected, result.Disposition); Assert.Null(result.Settings);
                if (!unknown) Assert.True(laterAdmissions > 0);
            }
            else
            {
                Assert.Same(originalError, failure);
                Assert.Equal(expected, Assert.IsType<BrowserSettingsWriteException>(failure).Disposition);
            }
            if (scenario != "config-unknown")
            {
                Assert.Equal(originalConfig, actualConfig); Assert.Equal(originalProjection, actualProjection);
            }
            if (scenario.StartsWith("rollback", StringComparison.Ordinal)) Assert.Equal(1, knownCuts);
        }
    }
}
