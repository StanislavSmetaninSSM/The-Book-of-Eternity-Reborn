using System.Reflection;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.WebUi;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ConsoleSettingsSessionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "boe-console-draft-" + Guid.NewGuid().ToString("N"));
    private readonly FileSystemManager _files;
    private readonly GameSettings _live = new() { MusicEnabled = false, SoundEnabled = false };
    private readonly StateManager _state;
    private readonly SystemModService _mods;
    private Action<TrustedLocalPublicationPhase, int>? _observe;
    private Action? _closing;
    private Action<string>? _read;
    private bool _settingsPublication;
    private const string Projection = LocalSettingsPreparation.ProjectionPath;

    public ConsoleSettingsSessionTests()
    {
        Directory.CreateDirectory(_root);
        _files = new FileSystemManager(_root, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
            {
                BeforeCanonicalMutationBoundaryAsync = path =>
                {
                    if (path.Replace('\\', '/') == "config.json") _settingsPublication = true;
                    return Task.CompletedTask;
                },
                LocalPublicationObserver = (phase, index) => { if (_settingsPublication) _observe?.Invoke(phase, index); },
                BeforeCanonicalReadOpenAsync = path => { _read?.Invoke(path.Replace('\\', '/')); return Task.CompletedTask; },
                SessionOperationClosingAsync = () => { _closing?.Invoke(); return Task.CompletedTask; }
            });
        _state = new StateManager(_files, _live, NullLogger<StateManager>.Instance);
        _mods = new SystemModService(_files, _live, NullLogger<SystemModService>.Instance);
    }

    private async Task<ConsoleSettingsSession> Open()
    {
        await _state.BootstrapLocalStorageAsync();
        _settingsPublication = false;
        return await ConsoleSettingsSession.OpenAsync(_files, _state, _mods);
    }

    [Fact]
    public async Task DraftIsDetachedAndDoesNotKeepTheCanonicalLeaseAcrossMenuInput()
    {
        var session = await Open();
        session.Draft.Language = "en";
        Assert.Equal("ru", _live.Language); Assert.NotSame(_live, session.Draft);
        var acquisition = _files.AcquireCanonicalWriteLeaseAsync();
        await using var lease = await acquisition.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.False(session.RequiresReload);
    }

    [Fact]
    public async Task CommitUsesConsoleOwnerAndKeepsLiveSettingsReferenceThenAdvancesBaseline()
    {
        var session = await Open(); var draft = session.Draft; var ownerSeen = false;
        session.Draft.Language = "en";
        var result = await session.SaveAsync(() =>
        {
            using var reader = new StreamReader(_files.ResolvePath(LocalUiSessionLockService.LockPath));
            var owner = JsonNode.Parse(reader.ReadToEnd())!;
            Assert.Equal("console", owner["ownerKind"]!.GetValue<string>());
            Assert.StartsWith("console:", owner["ownerId"]!.GetValue<string>());
            Assert.Equal("en", _live.Language); Assert.Same(_live, _state.Settings);
            Assert.True(File.Exists(_files.ResolvePath(Projection)));
            Assert.True(File.Exists(_files.ResolvePath(SystemModService.ManifestPath)));
            ownerSeen = true; return Task.CompletedTask;
        });
        Assert.True(ownerSeen); Assert.Equal(BrowserPreparedWriteDisposition.Committed, result.Disposition);
        Assert.False(result.NeedsFollowUp); Assert.Same(draft, session.Draft);
        session.Draft.Language = "ru";
        Assert.Equal(BrowserPreparedWriteDisposition.Committed, (await session.SaveAsync()).Disposition);
        Assert.Equal("ru", _live.Language); Assert.False(session.RequiresReload);
    }

    [Theory]
    [InlineData("rollback")]
    [InlineData("uncertain")]
    [InlineData("committed-cleanup")]
    public async Task PublicationOutcomePreservesDraftAndOnlyAcceptsCommittedRuntime(string cut)
    {
        var session = await Open(); var before = File.ReadAllBytes(_files.ResolvePath("config.json"));
        session.Draft.Language = "en"; var injected = false;
        _observe = (phase, _) =>
        {
            var wanted = cut == "committed-cleanup" ? TrustedLocalPublicationPhase.Committed : TrustedLocalPublicationPhase.MemberPublished;
            if (injected || phase != wanted) return;
            injected = true;
            if (cut == "uncertain") File.WriteAllBytes(_files.ResolvePath(Projection), [99]);
            throw new InvalidOperationException("Injected console settings interruption.");
        };
        var result = await session.SaveAsync();
        Assert.True(injected); Assert.Equal("en", session.Draft.Language);
        if (cut == "rollback")
        {
            Assert.Equal(BrowserPreparedWriteDisposition.RolledBack, result.Disposition);
            Assert.Equal(before, File.ReadAllBytes(_files.ResolvePath("config.json")));
            Assert.Equal("ru", _live.Language); Assert.False(session.RequiresReload);
        }
        else if (cut == "uncertain")
        {
            Assert.Equal(BrowserPreparedWriteDisposition.Uncertain, result.Disposition);
            Assert.Equal("ru", _live.Language); Assert.True(session.RequiresReload);
            _observe = null;
            Assert.Equal(BrowserPreparedWriteDisposition.Blocked, (await session.SaveAsync()).Disposition);
            Assert.Equal(new byte[] { 99 }, File.ReadAllBytes(_files.ResolvePath(Projection)));
        }
        else
        {
            Assert.Equal(BrowserPreparedWriteDisposition.Committed, result.Disposition);
            Assert.True(result.NeedsFollowUp); Assert.Equal("en", _live.Language);
            Assert.True(session.RequiresReload);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RuntimeOrClosingFailureCannotRelabelCommittedFiles(bool closing)
    {
        var session = await Open(); session.Draft.Language = "en";
        if (closing) _closing = () => throw new IOException("Injected outer close failure.");
        var result = await session.SaveAsync(() => closing ? Task.CompletedTask : throw new IOException("Runtime refresh failed."));
        Assert.Equal(BrowserPreparedWriteDisposition.Committed, result.Disposition);
        Assert.True(result.NeedsFollowUp); Assert.True(session.RequiresReload);
        Assert.Equal("en", _live.Language);
        using var reader = new StreamReader(_files.ResolvePath("config.json"));
        Assert.Equal("en", JsonNode.Parse(reader.ReadToEnd())!["language"]!.GetValue<string>());
    }

    [Fact]
    public async Task AudioPreviewUsesDraftThenRestoresAcceptedSettingsWhenDisposed()
    {
        var audio = new AudioService(_files, new GameSettings { MusicEnabled = true, MusicVolume = 50 }, NullLogger<AudioService>.Instance);
        var draft = new GameSettings { MusicEnabled = false, MusicVolume = 50 };
        var field = typeof(AudioService).GetField("_musicCts", BindingFlags.NonPublic | BindingFlags.Instance)!;
        using var previewCts = new CancellationTokenSource(); field.SetValue(audio, previewCts);
        var preview = audio.BeginSettingsPreview(draft);
        await audio.ApplySettingsAsync(); Assert.True(previewCts.IsCancellationRequested);
        preview.Dispose(); preview.Dispose();
        using var acceptedCts = new CancellationTokenSource(); field.SetValue(audio, acceptedCts);
        await audio.ApplySettingsAsync(); Assert.False(acceptedCts.IsCancellationRequested);
        await audio.StopAllAsync();
    }

    [Theory]
    [InlineData("pending")]
    [InlineData("owner")]
    public async Task PendingTurnOrOtherOwnerBlocksTheDraftWithoutRuntimeAcceptance(string blocker)
    {
        var session = await Open(); session.Draft.Language = "en";
        var before = File.ReadAllBytes(_files.ResolvePath("config.json"));
        if (blocker == "pending") await _files.WriteFileAtomicAsync("input/turn_request.json", "{}");
        else await new LocalUiSessionLockService(_files).AcquireOrRefreshAsync(
            new("other", "browser", "Other UI", TimeSpan.FromSeconds(120)), "Other operation");
        var result = await session.SaveAsync();
        Assert.Equal(BrowserPreparedWriteDisposition.Blocked, result.Disposition);
        Assert.Equal(before, File.ReadAllBytes(_files.ResolvePath("config.json")));
        Assert.Equal("ru", _live.Language); Assert.Equal("en", session.Draft.Language);
        Assert.DoesNotContain("Browser-write", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StaleConfigRequiresReloadBeforeAnotherSave()
    {
        var session = await Open(); session.Draft.Language = "en";
        var changed = _state.EncodeLocalSettings(new GameSettings { Language = "ru", Difficulty = "hard" });
        File.WriteAllBytes(_files.ResolvePath("config.json"), changed);
        var result = await session.SaveAsync();
        Assert.Equal(BrowserPreparedWriteDisposition.Blocked, result.Disposition);
        Assert.True(session.RequiresReload); Assert.Equal("en", session.Draft.Language);
        Assert.Equal(changed, File.ReadAllBytes(_files.ResolvePath("config.json")));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ChangedOrMissingGenerationCannotBeAdoptedOrBootstrappedBySave(bool absent)
    {
        var session = await Open(); session.Draft.Language = "en";
        var before = File.ReadAllBytes(_files.ResolvePath("config.json"));
        if (absent) File.Delete(_files.SessionGenerationPath);
        else File.WriteAllText(_files.SessionGenerationPath,
            "{\"SchemaVersion\":1,\"GenerationId\":\"11111111111111111111111111111111\"}");
        var result = await session.SaveAsync();
        Assert.Equal(BrowserPreparedWriteDisposition.Blocked, result.Disposition);
        Assert.True(session.RequiresReload); Assert.Equal("ru", _live.Language);
        Assert.Equal(before, File.ReadAllBytes(_files.ResolvePath("config.json")));
        if (absent) Assert.False(File.Exists(_files.SessionGenerationPath));
    }

    [Fact]
    public async Task ExplicitReloadDiscardsDraftAndAcceptsConfirmedConfigWithoutChangingReferences()
    {
        var session = await Open(); var draft = session.Draft; draft.Language = "en";
        File.WriteAllBytes(_files.ResolvePath("config.json"), _state.EncodeLocalSettings(new GameSettings { Difficulty = "hard" }));
        await session.SaveAsync(); var refreshed = false;
        await session.ReloadAsync(() => { refreshed = true; Assert.Equal("hard", _live.Difficulty); return Task.CompletedTask; });
        Assert.True(refreshed); Assert.Same(draft, session.Draft); Assert.Same(_live, _state.Settings);
        Assert.Equal("ru", draft.Language); Assert.Equal("hard", draft.Difficulty); Assert.False(session.RequiresReload);
        draft.Language = "en";
        Assert.Equal(BrowserPreparedWriteDisposition.Committed, (await session.SaveAsync()).Disposition);
    }

    [Fact]
    public async Task ReloadRejectsAReplacedSessionAndRetainsTheUnacceptedDraft()
    {
        var session = await Open(); session.Draft.Language = "en";
        File.WriteAllText(_files.SessionGenerationPath,
            "{\"SchemaVersion\":1,\"GenerationId\":\"11111111111111111111111111111111\"}");
        await Assert.ThrowsAsync<SessionReplacedException>(() => session.ReloadAsync());
        Assert.True(session.RequiresReload); Assert.Equal("en", session.Draft.Language); Assert.Equal("ru", _live.Language);
    }

    [Fact]
    public async Task ReloadCannotClaimUnknownPublicationEvidenceWasDiscarded()
    {
        var session = await Open(); session.Draft.Language = "en"; var injected = false;
        _observe = (phase, _) =>
        {
            if (injected || phase != TrustedLocalPublicationPhase.MemberPublished) return;
            injected = true; File.WriteAllBytes(_files.ResolvePath(Projection), [99]);
            throw new InvalidOperationException("Unknown content after console publication.");
        };
        Assert.Equal(BrowserPreparedWriteDisposition.Uncertain, (await session.SaveAsync()).Disposition);
        _observe = null;
        await Assert.ThrowsAnyAsync<InvalidDataException>(() => session.ReloadAsync());
        Assert.True(session.RequiresReload); Assert.Equal("en", session.Draft.Language); Assert.Equal("ru", _live.Language);
        Assert.Equal(new byte[] { 99 }, File.ReadAllBytes(_files.ResolvePath(Projection)));
    }

    [Fact]
    public async Task ModListingUsesDraftSelectionAndPortableInputValidationWithoutPublication()
    {
        var session = await Open();
        File.WriteAllText(_files.ResolvePath("mods/weather.json"), "{\"modId\":\"weather\"}");
        session.Draft.EnabledSystemMods = ["weather.json"];
        var before = File.ReadAllBytes(_files.ResolvePath("config.json"));
        var mods = await session.ReadModsAsync();
        Assert.True(Assert.Single(mods).Enabled); Assert.Empty(_live.EnabledSystemMods);
        Assert.Equal(before, File.ReadAllBytes(_files.ResolvePath("config.json")));
        File.Delete(_files.ResolvePath("mods/weather.json"));
        File.CreateSymbolicLink(_files.ResolvePath("mods/weather.json"), _files.ResolvePath("config.json"));
        await Assert.ThrowsAnyAsync<InvalidDataException>(() => session.ReadModsAsync());
    }

    [Fact]
    public async Task OneShotPreparationReadFailureKeepsUnchangedDraftRetryable()
    {
        var session = await Open(); session.Draft.Language = "en";
        var before = File.ReadAllBytes(_files.ResolvePath("config.json")); var injected = false;
        _read = path =>
        {
            if (path != "config.json" || injected) return;
            injected = true; throw new IOException("One-shot settings read interruption.");
        };
        var failed = await session.SaveAsync();
        Assert.True(injected); Assert.Equal(BrowserPreparedWriteDisposition.Blocked, failed.Disposition);
        Assert.Equal(before, File.ReadAllBytes(_files.ResolvePath("config.json")));
        Assert.Equal("ru", _live.Language); Assert.Equal("en", session.Draft.Language);
        Assert.False(session.RequiresReload);
        _read = null;
        Assert.Equal(BrowserPreparedWriteDisposition.Committed, (await session.SaveAsync()).Disposition);
        Assert.Equal("en", _live.Language);
    }

    [Theory]
    [InlineData("pending")]
    [InlineData("owner")]
    public async Task SynchronizedEntryIsReadOnlyEvenWhileAnotherWriterBlocksPublication(string blocker)
    {
        var session = await Open(); Assert.Equal(BrowserPreparedWriteDisposition.Committed, (await session.SaveAsync()).Disposition);
        // Encoding/whitespace alone must not manufacture an entrypoint write.
        var text = File.ReadAllText(_files.ResolvePath("config.json"));
        var encoded = System.Text.Encoding.Unicode.GetPreamble().Concat(System.Text.Encoding.Unicode.GetBytes(" \n" + text + "\n")).ToArray();
        File.WriteAllBytes(_files.ResolvePath("config.json"), encoded);
        session = await ConsoleSettingsSession.OpenAsync(_files, _state, _mods);
        if (blocker == "pending") await _files.WriteFileAtomicAsync("input/turn_request.json", "{}");
        else await new LocalUiSessionLockService(_files).AcquireOrRefreshAsync(
            new("other", "browser", "Other UI", TimeSpan.FromSeconds(120)), "Other operation");
        var published = false; _observe = (_, _) => published = true;
        _live.Language = "en"; // A prior runtime snapshot; the complete confirmed set is ru.
        Assert.True(await session.TryAcceptSynchronizedSettingsAsync());
        Assert.Equal("ru", _live.Language);
        Assert.False(published); Assert.Equal(encoded, File.ReadAllBytes(_files.ResolvePath("config.json")));
    }

    [Fact]
    public async Task EntryWithChangedMembersReportsPublicationNeededAndRetainsPendingAdmission()
    {
        var session = await Open();
        await _files.WriteFileAtomicAsync("input/turn_request.json", "{}");
        var before = File.ReadAllBytes(_files.ResolvePath("config.json"));
        session.Draft.Language = "en";
        Assert.False(await session.TryAcceptSynchronizedSettingsAsync());
        Assert.Equal("ru", _live.Language); Assert.Equal("en", session.Draft.Language);
        Assert.Equal(BrowserPreparedWriteDisposition.Blocked, (await session.SaveAsync()).Disposition);
        Assert.False(File.Exists(_files.ResolvePath(Projection)));
        Assert.Equal(before, File.ReadAllBytes(_files.ResolvePath("config.json")));
    }

    [Fact]
    public async Task SynchronizedEntryCannotAcceptAStaleGeneration()
    {
        var session = await Open(); await session.SaveAsync();
        File.WriteAllText(_files.SessionGenerationPath,
            "{\"SchemaVersion\":1,\"GenerationId\":\"11111111111111111111111111111111\"}");
        await Assert.ThrowsAsync<SessionReplacedException>(() => session.TryAcceptSynchronizedSettingsAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PreviewUnwindRestoresLanguageFontAndAudioAfterExceptionOrRejectedReload(bool rejectedReload)
    {
        var session = await Open(); var acceptedFont = _live.ConsoleFontSize;
        session.Draft.Language = "en"; session.Draft.ConsoleFontSize = 30;
        session.Draft.MusicEnabled = true; session.Draft.MusicVolume = 50;
        var loc = new BookOfEternityClient.UI.LocalizationManager { CurrentLanguage = "ru" };
        var audio = new AudioService(_files, _live, NullLogger<AudioService>.Instance);
        var appearance = new ConsoleAppearanceService(_live, NullLogger<ConsoleAppearanceService>.Instance);
        using var cts = new CancellationTokenSource();
        typeof(AudioService).GetField("_musicCts", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(audio, cts);
        var restoredPlayback = false;
        var failure = await Record.ExceptionAsync(async () =>
        {
            await using var preview = new ConsoleSettingsPreview(_live, session.Draft, loc, audio, appearance, settings =>
            {
                if (ReferenceEquals(settings, _live)) restoredPlayback = true;
                return Task.CompletedTask;
            });
            await preview.ApplyAsync();
            Assert.Equal("en", loc.CurrentLanguage); Assert.Equal("ru", _live.Language);
            Assert.Equal(acceptedFont, _live.ConsoleFontSize); Assert.False(cts.IsCancellationRequested);
            if (rejectedReload)
            {
                File.Delete(_files.SessionGenerationPath);
                await session.ReloadAsync();
            }
            else throw new ApplicationException("Scripted input exhausted during preview.");
        });
        if (rejectedReload) Assert.IsType<SessionReplacedException>(failure);
        else Assert.IsType<ApplicationException>(failure);
        Assert.Equal("ru", loc.CurrentLanguage); Assert.Equal(acceptedFont, _live.ConsoleFontSize);
        Assert.True(cts.IsCancellationRequested); Assert.True(restoredPlayback);
    }

    [Fact]
    public async Task UncertainImmediateMenuSaveReachesRecoveryBeforeAnotherModRead()
    {
        await Open();
        var script = Path.Combine(_root, "menu-input.json"); var artifacts = Path.Combine(_root, "menu-artifacts");
        File.WriteAllText(script, "{\"steps\":[{\"kind\":\"key\",\"key\":\"Down\"},{\"kind\":\"key\",\"key\":\"Down\"},{\"kind\":\"key\",\"key\":\"Down\"},{\"kind\":\"key\",\"key\":\"Down\"},{\"kind\":\"key\",\"key\":\"Enter\"}]}");
        var input = ConsoleE2EScriptedInputSource.FromFile(script, artifacts);
        var loc = new BookOfEternityClient.UI.LocalizationManager();
        var audio = new AudioService(_files, _live, NullLogger<AudioService>.Instance);
        // Invoke the actual menu with its real dependencies. Unused gameplay/GM
        // services are absent; this fixture cannot launch a turn or provider.
        var engine = new GameEngine(fs: _files, stateManager: _state, gameLoop: null!, normalizer: null!,
            progressionSchedule: null!, ui: null!, explorer: null!, loc: loc, saveLoad: null!, imageService: null!,
            validator: null!, charService: null!, storyService: null!, actorMemoryService: null!, audioService: audio,
            consoleAppearance: new ConsoleAppearanceService(_live, NullLogger<ConsoleAppearanceService>.Instance),
            systemModService: _mods, systemGuardianLibraryService: null!, criticalStateHealth: null!, worldDirectiveService: null!,
            scenarioCoreService: null!, afterlifeArchiveCandidateService: null!, afterlifeReturnGuardService: null!,
            rivalSoulArcService: null!, guardianCorrectionService: null!, pendingTurnState: null!, qteSceneService: null!,
            clipboardService: null!, logger: NullLogger<GameEngine>.Instance, inputSource: input);
        var injected = false;
        _observe = (phase, _) =>
        {
            if (injected || phase != TrustedLocalPublicationPhase.MemberPublished) return;
            injected = true; File.WriteAllBytes(_files.ResolvePath(Projection), [99]);
            throw new InvalidOperationException("Unknown member after immediate QTE publication.");
        };
        var method = typeof(GameEngine).GetMethod("OptionsMenu", BindingFlags.NonPublic | BindingFlags.Instance)!;
        await Assert.ThrowsAsync<ConsoleE2EScriptInputException>(() => (Task)method.Invoke(engine, null)!);
        Assert.True(injected); Assert.True(_live.EnableQteEvents);
        Assert.Equal(new byte[] { 99 }, File.ReadAllBytes(_files.ResolvePath(Projection)));
        Assert.True(File.Exists(Path.Combine(_files.RuntimeRootPath, "trusted-local-publication-v1", "active.json")));
        var screens = Directory.GetFiles(Path.Combine(artifacts, "screens"), "*.json");
        Assert.Contains(screens, path => JsonNode.Parse(File.ReadAllText(path))!["screenTitle"]!.GetValue<string>() == "Настройки требуют проверки");
    }

    [Fact]
    public async Task LastAcceptedEffectsCanBeRestoredWithoutDiscardingOrCertifyingTheDraft()
    {
        var session = await Open(); var font = _live.ConsoleFontSize;
        session.Draft.Language = "en"; session.Draft.ConsoleFontSize = 30;
        session.Draft.MusicEnabled = true; session.Draft.MusicVolume = 50;
        var loc = new BookOfEternityClient.UI.LocalizationManager();
        var audio = new AudioService(_files, _live, NullLogger<AudioService>.Instance);
        var appearance = new ConsoleAppearanceService(_live, NullLogger<ConsoleAppearanceService>.Instance);
        var field = typeof(AudioService).GetField("_musicCts", BindingFlags.NonPublic | BindingFlags.Instance)!;
        using var first = new CancellationTokenSource(); using var second = new CancellationTokenSource();
        await using (var preview = new ConsoleSettingsPreview(_live, session.Draft, loc, audio, appearance, _ => Task.CompletedTask))
        {
            field.SetValue(audio, first); await preview.ApplyAsync();
            Assert.Equal("en", loc.CurrentLanguage); Assert.False(first.IsCancellationRequested);
            await preview.RestoreLastAcceptedEffectsAsync();
            Assert.Equal("ru", loc.CurrentLanguage); Assert.True(first.IsCancellationRequested);
            Assert.Equal(font, _live.ConsoleFontSize); Assert.Equal("en", session.Draft.Language);
            Assert.True(session.Draft.MusicEnabled); // Restoration is not draft discard or a disk-state proof.
            field.SetValue(audio, second); await preview.ApplyAsync();
            Assert.Equal("en", loc.CurrentLanguage); Assert.False(second.IsCancellationRequested);
        }
        Assert.Equal("ru", loc.CurrentLanguage); Assert.True(second.IsCancellationRequested);
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
}
