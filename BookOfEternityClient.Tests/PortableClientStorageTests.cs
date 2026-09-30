using System.Diagnostics;
using System.Text;
using System.Text.Json;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.WebUi;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class PortableClientStorageTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "boe-client-storage-" + Guid.NewGuid().ToString("N"));
    private readonly FileSystemManager _files;
    private readonly GameSettings _settings = new();
    private readonly StateManager _state;

    public PortableClientStorageTests()
    {
        Directory.CreateDirectory(_root);
        _files = new FileSystemManager(_root, NullLogger<FileSystemManager>.Instance);
        _state = new StateManager(_files, _settings, NullLogger<StateManager>.Instance);
    }

    [Fact]
    public async Task Bootstrap_CreatesConfigAndGenerationUnderOneLeaseAndInvalidatesGenerationRevision()
    {
        var revision = _files.CanonicalRootAuthorityIdentity.SessionGenerationRevision;
        var generation = await _state.BootstrapLocalStorageAsync();
        Assert.True(Guid.TryParseExact(generation, "N", out _));
        Assert.True(File.Exists(_files.ResolvePath("config.json")));
        using var json = JsonDocument.Parse(await _files.ReadFileAsync("config.json") ?? "null");
        Assert.Equal("ru", json.RootElement.GetProperty("language").GetString());
        await using var lease = await _files.AcquireCanonicalWriteLeaseAsync();
        Assert.Equal(generation, _files.ReadExistingSessionGeneration(lease));
        Assert.True(_files.CanonicalRootAuthorityIdentity.SessionGenerationRevision > revision);
    }

    [Fact]
    public async Task Bootstrap_RepeatedAndNewManagerLoadsExistingValuesWithoutRewritingExactConfigOrGeneration()
    {
        var generation = await _state.BootstrapLocalStorageAsync();
        var configPath = _files.ResolvePath("config.json");
        byte[] config = new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes("{\"language\":\"en\",\"musicVolume\":37}")).ToArray();
        File.WriteAllBytes(configPath, config);
        var generationBytes = File.ReadAllBytes(_files.SessionGenerationPath);
        var otherSettings = new GameSettings();
        var otherFiles = new FileSystemManager(_root, NullLogger<FileSystemManager>.Instance);
        var otherState = new StateManager(otherFiles, otherSettings, NullLogger<StateManager>.Instance);
        Assert.Equal(generation, await otherState.BootstrapLocalStorageAsync());
        Assert.Equal("en", otherSettings.Language);
        Assert.Equal(37, otherSettings.MusicVolume);
        Assert.Equal(config, File.ReadAllBytes(configPath));
        Assert.Equal(generationBytes, File.ReadAllBytes(_files.SessionGenerationPath));
    }

    [Theory]
    [InlineData("{broken")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{\"language\":\"en\",\"language\":\"ru\"}")]
    public async Task Bootstrap_InvalidExistingConfigPreservesEvidenceAndDoesNotInventGeneration(string content)
    {
        _files.EnsureDirectoryStructure();
        var path = _files.ResolvePath("config.json");
        var before = Encoding.UTF8.GetBytes(content); File.WriteAllBytes(path, before);
        await Assert.ThrowsAsync<InvalidDataException>(() => _state.BootstrapLocalStorageAsync());
        Assert.Equal(before, File.ReadAllBytes(path));
        Assert.False(File.Exists(_files.SessionGenerationPath));
    }

    [Theory]
    [InlineData("file-publication-transactions")]
    [InlineData("load-transactions")]
    [InlineData("worker-apply-transactions")]
    public async Task Bootstrap_UnresolvedLegacyRuntimeEvidenceBlocksAndIsRetained(string area)
    {
        var evidence = Path.Combine(_files.RuntimeRootPath, area, "unknown-journal");
        Directory.CreateDirectory(Path.GetDirectoryName(evidence)!); File.WriteAllBytes(evidence, [0xFE, 0]);
        await Assert.ThrowsAsync<InvalidDataException>(() => _state.BootstrapLocalStorageAsync());
        Assert.Equal(new byte[] { 0xFE, 0 }, File.ReadAllBytes(evidence));
        Assert.False(File.Exists(_files.ResolvePath("config.json")));
        Assert.False(File.Exists(_files.SessionGenerationPath));
    }

    [Fact]
    public async Task AcquireLease_UnresolvedLegacyPublicationCannotSilentlyPassOnLinux()
    {
        var evidence = Path.Combine(_files.PhysicalPublicationTransactionsRootPath, "unknown-journal");
        Directory.CreateDirectory(Path.GetDirectoryName(evidence)!); File.WriteAllBytes(evidence, [0xFE, 0]);
        await Assert.ThrowsAsync<InvalidDataException>(async () => { await using var lease = await _files.AcquireCanonicalWriteLeaseAsync(); });
        Assert.Equal(new byte[] { 0xFE, 0 }, File.ReadAllBytes(evidence));
    }

    [Fact]
    public async Task Bootstrap_EmptyLegacyRootsAreNotUnresolvedEvidence()
    {
        foreach (var area in new[] { "file-publication-transactions", "load-transactions", "worker-apply-transactions" })
            Directory.CreateDirectory(Path.Combine(_files.RuntimeRootPath, area));
        var generation = await _state.BootstrapLocalStorageAsync();
        Assert.True(Guid.TryParseExact(generation, "N", out _));
    }

    [Fact]
    public async Task OrdinaryWriteAppendCompareExchangeAndDeleteUseExactLogicalContent()
    {
        await _state.BootstrapLocalStorageAsync();
        const string path = "game_state/core/portable.bin";
        await _files.WriteFileAtomicBytesAsync(path, [0xFF, 0]);
        await _files.AppendFileAtomicAsync(path, "a");
        Assert.Equal(new byte[] { 0xFF, 0, 97 }, await _files.ReadFileBytesAsync(path));
        Assert.Equal(CanonicalFileMutationResult.Conflict,
            await _files.CompareExchangeFileBytesAsync(path, [0xFF], [1]));
        Assert.Equal(CanonicalFileMutationResult.Applied,
            await _files.CompareExchangeFileBytesAsync(path, [0xFF, 0, 97], []));
        Assert.Equal(Array.Empty<byte>(), await _files.ReadFileBytesAsync(path));
        _files.DeleteFile(path);
        Assert.False(File.Exists(_files.ResolvePath(path)));
    }

    [Fact]
    public async Task PreparedCanonicalSetPublishesOneLogicalResultWithoutPhysicalReceipt()
    {
        await _state.BootstrapLocalStorageAsync();
        await using var lease = await _files.AcquireCanonicalWriteLeaseAsync();
        var result = await _files.PublishLocalFilesAsync(lease,
            [new("game_state/core/a.bin", null, [1]), new("game_state/core/b.bin", null, [2])]);
        Assert.Equal(TrustedLocalPublicationDisposition.Committed, result.Disposition);
        Assert.NotNull(result.Publication);
        Assert.Equal(2, result.Publication.Members.Count);
        Assert.Null(result.Failure);
        Assert.Equal(new byte[] { 1 }, File.ReadAllBytes(_files.ResolvePath("game_state/core/a.bin")));
        Assert.Equal(new byte[] { 2 }, File.ReadAllBytes(_files.ResolvePath("game_state/core/b.bin")));
    }

    [Fact]
    public async Task OrdinaryWriterRejectsStaleBoundGenerationWithoutChangingContent()
    {
        await _state.BootstrapLocalStorageAsync();
        var stale = Guid.NewGuid().ToString("N");
        await Assert.ThrowsAsync<SessionReplacedException>(() => SessionOperationContext.RunBoundAsync(_files, stale,
            () => _files.WriteFileAtomicAsync("game_state/core/stale.json", "{}")));
        Assert.False(File.Exists(_files.ResolvePath("game_state/core/stale.json")));
    }

    [Theory]
    [InlineData("pending")]
    [InlineData("committed")]
    [InlineData("conflict")]
    public async Task UnboundCanonicalReadRecoversNewJournalBeforeReturningAcceptedBytes(string cut)
    {
        _files.EnsureDirectoryStructure();
        var generation = Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(Path.GetDirectoryName(_files.SessionGenerationPath)!);
        File.WriteAllText(_files.SessionGenerationPath, JsonSerializer.Serialize(new { SchemaVersion = 1, GenerationId = generation }));
        const string relative = "game_state/core/read-recovery.bin";
        var path = _files.ResolvePath(relative); File.WriteAllBytes(path, [1]);
        var journal = Path.Combine(_files.RuntimeRootPath, "trusted-local-publication-v1", "active.json");
        await using (var lease = await _files.AcquireCanonicalWriteLeaseAsync())
        {
            var publisher = new TrustedLocalFilePublication(_files, new TrustedLocalFileScope([_root]));
            Assert.Throws<Interrupted>(() => publisher.Publish(lease, TrustedLocalGeneration.Existing(generation),
                [new(path, [1], [2])], (phase, _) =>
                {
                    if (phase == (cut == "committed" ? TrustedLocalPublicationPhase.Committed : TrustedLocalPublicationPhase.MemberPublished))
                        throw new Interrupted();
                }));
        }
        if (cut == "conflict")
        {
            File.WriteAllBytes(path, [99]);
            await Assert.ThrowsAsync<InvalidDataException>(() => _files.ReadFileBytesAsync(relative));
            Assert.Equal(new byte[] { 99 }, File.ReadAllBytes(path)); Assert.True(File.Exists(journal));
        }
        else
        {
            Assert.Equal(cut == "committed" ? new byte[] { 2 } : [1], await _files.ReadFileBytesAsync(relative));
            Assert.False(File.Exists(journal));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OriginalRecoveryCallbackDoesNotCreatePortableJournalOrGenerationAndRestoresLeaseMode(bool fail)
    {
        _files.EnsureDirectoryStructure();
        await using var lease = await _files.AcquireCanonicalWriteLeaseAsync();
        var called = false;
        async Task OriginalHandler()
        {
            called = true;
            Exception? failure = null;
            try { await _files.WriteFileAtomicBytesAsync(lease, "game_state/core/legacy-probe.bin", [1]); }
            catch (PlatformNotSupportedException ex) { failure = ex; }
            if (OperatingSystem.IsWindows()) Assert.Null(failure);
            else Assert.IsType<PlatformNotSupportedException>(failure);
            Assert.False(File.Exists(_files.SessionGenerationPath));
            Assert.False(File.Exists(Path.Combine(_files.RuntimeRootPath, "trusted-local-publication-v1", "active.json")));
            if (fail) throw new Interrupted();
        }
        if (fail) await Assert.ThrowsAsync<Interrupted>(() => _files.RunLegacyStorageRecoveryAsync(lease, OriginalHandler));
        else await _files.RunLegacyStorageRecoveryAsync(lease, OriginalHandler);
        Assert.True(called);
        await _files.WriteFileAtomicBytesAsync(lease, "game_state/core/ordinary-probe.bin", [2]);
        Assert.NotNull(_files.ReadExistingSessionGeneration(lease));
        Assert.Equal(new byte[] { 2 }, File.ReadAllBytes(_files.ResolvePath("game_state/core/ordinary-probe.bin")));
    }

    [Fact]
    public async Task Bootstrap_UsesRealColdProcessAndRetainsConfigAndGenerationAfterRestart()
    {
        await RunBootstrapHost("", "ru");
        string generation;
        await using (var lease = await _files.AcquireCanonicalWriteLeaseAsync())
            generation = _files.ReadExistingSessionGeneration(lease)!;
        var beforeGeneration = File.ReadAllBytes(_files.SessionGenerationPath);
        await _files.WriteFileAtomicAsync("config.json", "{\"language\":\"en\",\"musicVolume\":37}");
        var beforeConfig = File.ReadAllBytes(_files.ResolvePath("config.json"));
        await RunBootstrapHost(generation, "en");
        Assert.Equal(beforeGeneration, File.ReadAllBytes(_files.SessionGenerationPath));
        Assert.Equal(beforeConfig, File.ReadAllBytes(_files.ResolvePath("config.json")));
    }

    private async Task RunBootstrapHost(string generation, string expectedLanguage)
    {
        var assembly = typeof(PortableClientStorageTests).Assembly.Location;
        var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
        {
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (var argument in new[] { "exec", "--runtimeconfig", Path.ChangeExtension(assembly, ".runtimeconfig.json"),
                     "--depsfile", Path.ChangeExtension(assembly, ".deps.json"), typeof(PortableStorageCrashHost.Program).Assembly.Location,
                     _root, generation, "client-bootstrap", expectedLanguage }) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Bootstrap host did not start.");
        var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
            Assert.True(process.ExitCode == 0, await stdout + await stderr);
        }
        finally
        {
            if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); }
        }
    }

    [Fact]
    public async Task OrdinaryWriterPreservesBoundaryHooksAndStopsARevokedBoundOperationBeforeMutation()
    {
        var generation = await _state.BootstrapLocalStorageAsync();
        var trace = new List<string>(); var revoke = false;
        var hooked = new FileSystemManager(_root, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
            {
                BeforeCanonicalMutationBoundaryAsync = _ => { trace.Add("before"); return Task.CompletedTask; },
                AfterCanonicalMutationBoundaryValidatedAsync = relativePath =>
                {
                    trace.Add("after");
                    if (revoke) _ = SessionOperationContext.MarkReplaced(_root, generation, "Test operation revoked at boundary.");
                    return Task.CompletedTask;
                }
            });
        const string path = "game_state/core/boundary.bin";
        await hooked.WriteFileAtomicBytesAsync(path, [1]);
        Assert.Equal(new[] { "before", "after" }, trace);
        trace.Clear(); revoke = true;
        await Assert.ThrowsAsync<SessionReplacedException>(() => SessionOperationContext.RunBoundAsync(hooked, generation,
            () => hooked.WriteFileAtomicBytesAsync(path, [2])));
        Assert.Equal(new byte[] { 1 }, File.ReadAllBytes(_files.ResolvePath(path)));
        Assert.Equal(new[] { "before", "after" }, trace);
    }

    [Fact]
    public async Task ActualWebHostBuildBootstrapsConfigAndGenerationBeforeServingAnything()
    {
        var assets = Path.Combine(_root, "host-assets"); Directory.CreateDirectory(assets);
        File.WriteAllText(Path.Combine(assets, "index.html"), "<!doctype html><title>Fixture</title>");
        await using var app = LocalWebUiHost.Build([], new(_root, "http://127.0.0.1:0", assets));
        Assert.True(File.Exists(_files.ResolvePath("config.json")));
        Assert.True(File.Exists(_files.SessionGenerationPath));
        // Deliberately never StartAsync: this is startup admission, not browser UI evidence.
    }

    [Fact]
    public async Task ActualWebHostBuildRejectsInvalidExistingConfigWithoutCreatingGeneration()
    {
        _files.EnsureDirectoryStructure();
        var path = _files.ResolvePath("config.json"); File.WriteAllBytes(path, [0xFF, 0]);
        var assets = Path.Combine(_root, "host-assets"); Directory.CreateDirectory(assets);
        File.WriteAllText(Path.Combine(assets, "index.html"), "<!doctype html><title>Fixture</title>");
        WebApplication? app = null;
        try { Assert.Throws<InvalidDataException>(() => app = LocalWebUiHost.Build([], new(_root, "http://127.0.0.1:0", assets))); }
        finally { if (app != null) await app.DisposeAsync(); }
        Assert.Equal(new byte[] { 0xFF, 0 }, File.ReadAllBytes(path));
        Assert.False(File.Exists(_files.SessionGenerationPath));
    }

    [Fact]
    public async Task ActualConsoleEntrypointRejectsInvalidConfigBeforeMainMenuAndKeepsEvidence()
    {
        _files.EnsureDirectoryStructure();
        var path = _files.ResolvePath("config.json"); File.WriteAllText(path, "{broken");
        var script = Path.Combine(_root, "input-script.json");
        File.WriteAllText(script, "{\"steps\":[{\"kind\":\"key\",\"key\":\"Up\"},{\"kind\":\"key\",\"key\":\"Enter\"}]}");
        var assembly = typeof(PortableClientStorageTests).Assembly.Location;
        var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
        {
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (var argument in new[] { "exec", "--runtimeconfig", Path.ChangeExtension(assembly, ".runtimeconfig.json"),
                     "--depsfile", Path.ChangeExtension(assembly, ".deps.json"), typeof(PortableStorageCrashHost.Program).Assembly.Location,
                     _root, "", "console-startup", script }) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Console host did not start.");
        var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
            var output = await stdout + await stderr;
            Assert.True(process.ExitCode == 2, output);
            Assert.Contains("config.json", output, StringComparison.Ordinal);
        }
        finally
        {
            if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); }
        }
        Assert.Equal("{broken", File.ReadAllText(path));
        Assert.False(File.Exists(_files.SessionGenerationPath));
    }

    private sealed class Interrupted : Exception { }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
