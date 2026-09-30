using System.Text;
using System.Text.Json;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
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

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
