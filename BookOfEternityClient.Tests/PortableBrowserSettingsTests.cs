using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;
using BookOfEternityClient.WebUi;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class PortableBrowserSettingsTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "boe-settings-service-" + Guid.NewGuid().ToString("N"));
    private readonly FileSystemManager _files;
    private readonly GameSettings _settings = new() { MusicEnabled = false, SoundEnabled = false };
    private readonly StateManager _state;
    private readonly LocalizationManager _localization = new();
    private readonly BrowserClientSettingsService _service;
    private readonly BrowserAudioService _audio;
    private Action<TrustedLocalPublicationPhase, int>? _observe;
    private bool _settingsPublication;
    private const string Projection = "game_state/core/game_settings.json";
    private static readonly byte[] ProjectionBefore = [0xFF, 0, 0xFE];

    public PortableBrowserSettingsTests()
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
                LocalPublicationObserver = (phase, index) =>
                {
                    if (_settingsPublication) _observe?.Invoke(phase, index);
                }
            });
        _state = new StateManager(_files, _settings, NullLogger<StateManager>.Instance);
        var audioRuntime = new AudioService(_files, _settings, NullLogger<AudioService>.Instance);
        var coordinator = new BrowserLocalWriteCoordinator(_files, new LocalUiSessionLockService(_files));
        _service = new BrowserClientSettingsService(_files, _state, audioRuntime, coordinator, _localization);
        _audio = new BrowserAudioService(_files, _state, audioRuntime, coordinator);
    }

    private async Task<(string Generation, byte[] Config)> Initialize()
    {
        var generation = await _state.BootstrapLocalStorageAsync();
        var config = Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes(
            "{\"language\":\"ru\",\"difficulty\":\"normal\",\"musicEnabled\":false,\"musicVolume\":17,\"soundEnabled\":false}")).ToArray();
        File.WriteAllBytes(_files.ResolvePath("config.json"), config);
        await _state.BootstrapLocalStorageAsync();
        File.WriteAllBytes(_files.ResolvePath(Projection), ProjectionBefore);
        _settingsPublication = false;
        return (generation, config);
    }

    private static BrowserClientSettingsUpdateRequest Request(string? difficulty = "hard") =>
        new("en", difficulty, true, false, 123, false, -10, 175, 125, true, true);

    private int DeclaredMemberCount()
    {
        using var journal = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(_files.RuntimeRootPath, "trusted-local-publication-v1", "active.json")));
        return journal.RootElement.GetProperty("Members").GetArrayLength();
    }

    [Fact]
    public async Task SettingsUseOnePreparedPairAndApplyTheSameRuntimeInstanceOnlyAfterCommit()
    {
        await Initialize(); var intent = false; var commit = false;
        _observe = (phase, _) =>
        {
            if (phase == TrustedLocalPublicationPhase.IntentPublished && !intent)
            {
                intent = true; Assert.Equal(2, DeclaredMemberCount()); Assert.Equal("ru", _settings.Language);
            }
            if (phase == TrustedLocalPublicationPhase.Committed && !commit)
            {
                commit = true; Assert.Equal("ru", _settings.Language);
                using var config = JsonDocument.Parse(File.ReadAllText(_files.ResolvePath("config.json")));
                Assert.Equal("en", config.RootElement.GetProperty("language").GetString());
            }
        };
        var result = await _service.UpdateAsync(Request());
        Assert.True(result.Success, result.Message); Assert.Equal(BrowserPreparedWriteDisposition.Committed, result.Disposition);
        Assert.True(intent); Assert.True(commit); Assert.Same(_settings, _state.Settings);
        Assert.Equal("en", _settings.Language); Assert.Equal("en", _localization.CurrentLanguage);
        Assert.Equal(100, _settings.MusicVolume); Assert.Equal(0, _settings.SoundVolume);
        Assert.Null(result.Settings!.PersistenceWarning);
        using var projection = JsonDocument.Parse(File.ReadAllText(_files.ResolvePath(Projection)));
        Assert.Equal("hard", projection.RootElement.GetProperty("difficulty").GetString());
        Assert.False(Directory.Exists(_files.ResolvePath(ExplorerLocalTurnRollbackArtifacts.Root)));
    }

    [Fact]
    public async Task SettingsWithoutDifficultyPreserveExactProjectionBytes()
    {
        await Initialize();
        var result = await _service.UpdateAsync(Request(difficulty: null));
        Assert.True(result.Success, result.Message);
        Assert.Equal(ProjectionBefore, File.ReadAllBytes(_files.ResolvePath(Projection)));
    }

    [Fact]
    public async Task AudioUsesOneDeclaredConfigMemberAndKeepsProjectionUntouched()
    {
        await Initialize(); var intent = false;
        _observe = (phase, _) =>
        {
            if (phase != TrustedLocalPublicationPhase.IntentPublished || intent) return;
            intent = true; Assert.Equal(1, DeclaredMemberCount()); Assert.Equal(17, _settings.MusicVolume);
        };
        var result = await _audio.UpdateSettingsAsync(new(false, 77, false, 35));
        Assert.True(intent); Assert.Equal(77, result.MusicVolume); Assert.Equal(77, _settings.MusicVolume);
        Assert.Same(_settings, _state.Settings); Assert.Null(result.PersistenceWarning);
        Assert.Equal(ProjectionBefore, File.ReadAllBytes(_files.ResolvePath(Projection)));
    }

    [Theory]
    [InlineData("rollback")]
    [InlineData("committed-cleanup")]
    [InlineData("unknown")]
    [InlineData("unknown-close")]
    public async Task SettingsExposeDurableOutcomeWithoutPrematureRuntimeApplication(string cut)
    {
        var baseline = await Initialize(); var injected = false;
        _observe = (phase, _) =>
        {
            if (injected || phase != (cut == "committed-cleanup" ? TrustedLocalPublicationPhase.Committed : TrustedLocalPublicationPhase.MemberPublished)) return;
            injected = true; Assert.Equal("ru", _settings.Language);
            if (cut.StartsWith("unknown", StringComparison.Ordinal)) File.WriteAllBytes(_files.ResolvePath(Projection), [99]);
            if (cut == "unknown-close") SessionOperationContext.MarkReplaced(_root, null, "Settings operation revoked after unknown partial publication.");
            throw new InvalidOperationException("Injected prepared settings interruption.");
        };
        var result = await _service.UpdateAsync(Request());
        Assert.True(injected); Assert.Same(_settings, _state.Settings);
        if (cut == "committed-cleanup")
        {
            Assert.True(result.Success, result.Message); Assert.Equal(BrowserPreparedWriteDisposition.Committed, result.Disposition);
            Assert.Equal("en", _settings.Language); Assert.Equal("en", _localization.CurrentLanguage);
            Assert.Contains("сохранены", result.Settings!.PersistenceWarning!, StringComparison.OrdinalIgnoreCase);
            return;
        }
        Assert.False(result.Success); Assert.Null(result.Settings); Assert.Equal("ru", _settings.Language);
        Assert.Equal("ru", _localization.CurrentLanguage);
        if (cut == "rollback")
        {
            Assert.Equal(BrowserPreparedWriteDisposition.RolledBack, result.Disposition);
            Assert.Equal(baseline.Config, File.ReadAllBytes(_files.ResolvePath("config.json")));
            Assert.Equal(ProjectionBefore, File.ReadAllBytes(_files.ResolvePath(Projection)));
        }
        else
        {
            Assert.Equal(BrowserPreparedWriteDisposition.Uncertain, result.Disposition);
            Assert.Equal(new byte[] { 99 }, File.ReadAllBytes(_files.ResolvePath(Projection)));
            Assert.True(File.Exists(Path.Combine(_files.RuntimeRootPath, "trusted-local-publication-v1", "active.json")));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AudioSeparatesRollbackFromCommittedCleanupWarning(bool committed)
    {
        var baseline = await Initialize(); var injected = false;
        _observe = (phase, _) =>
        {
            if (injected || phase != (committed ? TrustedLocalPublicationPhase.Committed : TrustedLocalPublicationPhase.MemberPublished)) return;
            injected = true; Assert.Equal(17, _settings.MusicVolume);
            throw new InvalidOperationException("Injected audio settings interruption.");
        };
        if (committed)
        {
            var result = await _audio.UpdateSettingsAsync(new(false, 77, false, 35));
            Assert.Equal(77, result.MusicVolume); Assert.Equal(77, _settings.MusicVolume);
            Assert.Contains("сохранены", result.PersistenceWarning!, StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            var error = await Assert.ThrowsAsync<BrowserSettingsWriteException>(() => _audio.UpdateSettingsAsync(new(false, 77, false, 35)));
            Assert.Equal(BrowserPreparedWriteDisposition.RolledBack, error.Disposition);
            Assert.Equal(17, _settings.MusicVolume);
            Assert.Equal(baseline.Config, File.ReadAllBytes(_files.ResolvePath("config.json")));
        }
        Assert.True(injected); Assert.Equal(ProjectionBefore, File.ReadAllBytes(_files.ResolvePath(Projection)));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{broken")]
    [InlineData("{\"language\":\"en\",\"language\":\"ru\"}")]
    public async Task SettingsAndAudioGetRejectInvalidConfigRatherThanShowingAcceptedDefaults(string invalid)
    {
        await Initialize(); var bytes = Encoding.UTF8.GetBytes(invalid); File.WriteAllBytes(_files.ResolvePath("config.json"), bytes);
        await Assert.ThrowsAsync<InvalidDataException>(() => _service.BuildAsync());
        await Assert.ThrowsAsync<InvalidDataException>(() => _audio.BuildSettingsAsync());
        Assert.Equal(bytes, File.ReadAllBytes(_files.ResolvePath("config.json"))); Assert.Equal("ru", _settings.Language);
    }

    [Fact]
    public async Task HardLinkedConfigSupportsBootstrapGetAndUpdateWithoutChangingOutsideAlias()
    {
        var baseline = await Initialize(); var path = _files.ResolvePath("config.json"); var outside = Path.Combine(_root, "outside-config-alias");
        File.WriteAllBytes(outside, baseline.Config); File.Delete(path);
        if (OperatingSystem.IsWindows()) Assert.True(CreateHardLink(path, outside, IntPtr.Zero));
        else Assert.Equal(0, Link(outside, path));
        await _state.BootstrapLocalStorageAsync(); _settings.Language = "en";
        Assert.Equal("ru", (await _service.BuildAsync()).Language.Value);
        Assert.Equal(17, (await _audio.BuildSettingsAsync()).MusicVolume);
        var result = await _service.UpdateAsync(Request()); Assert.True(result.Success, result.Message);
        Assert.Equal(baseline.Config, File.ReadAllBytes(outside)); Assert.Equal("en", _settings.Language);
    }

    [Fact]
    public async Task PreparedSettingsSurviveAnActualColdBootstrapProcess()
    {
        var baseline = await Initialize(); var result = await _service.UpdateAsync(Request()); Assert.True(result.Success, result.Message);
        var config = File.ReadAllBytes(_files.ResolvePath("config.json")); var generation = File.ReadAllBytes(_files.SessionGenerationPath);
        var assembly = typeof(PortableBrowserSettingsTests).Assembly.Location;
        var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
        { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in new[] { "exec", "--runtimeconfig", Path.ChangeExtension(assembly, ".runtimeconfig.json"),
                     "--depsfile", Path.ChangeExtension(assembly, ".deps.json"), typeof(PortableStorageCrashHost.Program).Assembly.Location,
                     _root, baseline.Generation, "client-bootstrap", "en" }) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Cold bootstrap did not start.");
        var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try { await process.WaitForExitAsync(timeout.Token); Assert.True(process.ExitCode == 0, await stdout + await stderr); }
        finally { if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); } }
        Assert.Equal(config, File.ReadAllBytes(_files.ResolvePath("config.json")));
        Assert.Equal(generation, File.ReadAllBytes(_files.SessionGenerationPath));
    }

    [DllImport("libc", EntryPoint = "link", SetLastError = true)] private static extern int Link(string existing, string created);
    [DllImport("kernel32.dll", EntryPoint = "CreateHardLinkW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CreateHardLink(string created, string existing, IntPtr security);
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
}
