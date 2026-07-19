using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class FileSystemManagerTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;

    public FileSystemManagerTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-filesystem-manager-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
    }

    [Fact]
    public async Task ReadFileAsync_WhenFileIsBrieflyLocked_RetriesUntilContentIsReadable()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", "stable content");
        var fullPath = _fs.ResolvePath("input/turn_request.json");
        using var lockStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.None);

        var readTask = _fs.ReadFileAsync("input/turn_request.json");
        await Task.Delay(100);
        await lockStream.DisposeAsync();

        var content = await readTask;

        Assert.Equal("stable content", content);
    }

    [Fact]
    public async Task WriteFileAtomicAsync_WhenTargetIsBrieflyLocked_RetriesUntilReplacementSucceeds()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", "old content");
        var fullPath = _fs.ResolvePath("input/turn_request.json");
        using var lockStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);

        var writeTask = _fs.WriteFileAtomicAsync("input/turn_request.json", "new content");
        await Task.Delay(100);
        await lockStream.DisposeAsync();

        await writeTask;

        Assert.Equal("new content", await _fs.ReadFileAsync("input/turn_request.json"));
    }

    [Fact]
    public async Task WriteFileAtomicAsync_PreservesUtf8BomContract()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", "content");

        var bytes = await File.ReadAllBytesAsync(_fs.ResolvePath("input/turn_request.json"));
        var preamble = System.Text.Encoding.UTF8.GetPreamble();

        Assert.Equal(preamble, bytes.Take(preamble.Length).ToArray());
    }

    [Fact]
    public async Task WriteFileAtomicBytesAsync_PreservesExactBytes()
    {
        byte[] expected = [0xEF, 0xBB, 0xBF, 0x00, 0xFF, 0x41];

        await _fs.WriteFileAtomicBytesAsync("input/turn_request.json", expected);

        var actual = await File.ReadAllBytesAsync(_fs.ResolvePath("input/turn_request.json"));
        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task CompareExchangeFileBytesAsync_RejectsStaleExpectedBytesWithoutWriting()
    {
        byte[] baseline = [0x01, 0x02, 0x03];
        byte[] concurrent = [0x04, 0x05, 0x06];
        byte[] proposed = [0x07, 0x08, 0x09];
        await _fs.WriteFileAtomicBytesAsync("game_state/world/weather.json", baseline);
        await _fs.WriteFileAtomicBytesAsync("game_state/world/weather.json", concurrent);

        var result = await _fs.CompareExchangeFileBytesAsync(
            "game_state/world/weather.json",
            baseline,
            proposed);

        Assert.Equal(CanonicalFileMutationResult.Conflict, result);
        Assert.Equal(concurrent, await _fs.ReadFileBytesAsync("game_state/world/weather.json"));
    }

    [Fact]
    public async Task CompareExchangeFileBytesAsync_ConcurrentReplacementsAllowExactlyOneWinner()
    {
        byte[] baseline = [0x10];
        byte[] first = [0x20];
        byte[] second = [0x30];
        await _fs.WriteFileAtomicBytesAsync("game_state/world/weather.json", baseline);

        var results = await Task.WhenAll(
            _fs.CompareExchangeFileBytesAsync("game_state/world/weather.json", baseline, first),
            _fs.CompareExchangeFileBytesAsync("game_state/world/weather.json", baseline, second));

        Assert.Equal(1, results.Count(result => result == CanonicalFileMutationResult.Applied));
        Assert.Equal(1, results.Count(result => result == CanonicalFileMutationResult.Conflict));
        var actual = await _fs.ReadFileBytesAsync("game_state/world/weather.json");
        Assert.NotNull(actual);
        Assert.True(actual.SequenceEqual(first) || actual.SequenceEqual(second));
    }

    [Fact]
    public async Task CompareExchangeFileBytesAsync_RollbackDoesNotOverwriteNewerBytes()
    {
        byte[] baseline = [0x41];
        byte[] worker = [0x42];
        byte[] newer = [0x43];
        await _fs.WriteFileAtomicBytesAsync("game_state/world/weather.json", worker);
        await _fs.WriteFileAtomicBytesAsync("game_state/world/weather.json", newer);

        var result = await _fs.CompareExchangeFileBytesAsync(
            "game_state/world/weather.json",
            worker,
            baseline);

        Assert.Equal(CanonicalFileMutationResult.Conflict, result);
        Assert.Equal(newer, await _fs.ReadFileBytesAsync("game_state/world/weather.json"));
    }

    [Fact]
    public async Task DeleteFile_WhenTargetIsBrieflyLocked_RetriesUntilDeleteSucceeds()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", "content");
        var fullPath = _fs.ResolvePath("input/turn_request.json");
        await using var lockStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);

        var deleteTask = Task.Run(() => _fs.DeleteFile("input/turn_request.json"));
        await Task.Delay(100);
        await lockStream.DisposeAsync();

        await deleteTask;

        Assert.False(File.Exists(fullPath));
    }

    [Fact]
    public async Task ClearGameState_PreservesGmBridgeRuntimeStatus()
    {
        await _fs.WriteFileAtomicAsync("game_state/control/gm_bridge_status.json", """{"ready":true}""");
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """{"currentRealm":"Chaos Sea"}""");

        _fs.ClearGameState();

        Assert.Equal("""{"ready":true}""", await _fs.ReadFileAsync("game_state/control/gm_bridge_status.json"));
        Assert.False(_fs.FileExists("game_state/meta/soul_state.json"));
    }

    [Fact]
    public async Task ClearGameState_PreservesGmContextPackJsonArtifacts()
    {
        await _fs.WriteFileAtomicAsync("game_state/control/gm_context_pack/context_pack_manifest.json", """{"schemaVersion":1}""");
        await _fs.WriteFileAtomicAsync("game_state/control/gm_context_pack/Templates/PROGRESSION_REPORT_TEMPLATE.json", """{"progressionProcessingReport":{}}""");
        await _fs.WriteFileAtomicAsync("game_state/control/gm_context_pack/Templates/AFTERLIFE_TEMPO_ADVANTAGE_TEMPLATE.json", """{"tempoAdvantage":{}}""");
        await _fs.WriteFileAtomicAsync("game_state/control/gm_context_pack/Probes/GM_SAFE_PROBES.json", """{"probes":[]}""");
        await _fs.WriteFileAtomicAsync("game_state/control/gm_context_pack/Rubrics/GM_LIVE_TEST_RUBRIC.json", """{"dimensions":[]}""");
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """{"currentRealm":"Chaos Sea"}""");

        _fs.ClearGameState();

        Assert.True(_fs.FileExists("game_state/control/gm_context_pack/context_pack_manifest.json"));
        Assert.True(_fs.FileExists("game_state/control/gm_context_pack/Templates/PROGRESSION_REPORT_TEMPLATE.json"));
        Assert.True(_fs.FileExists("game_state/control/gm_context_pack/Templates/AFTERLIFE_TEMPO_ADVANTAGE_TEMPLATE.json"));
        Assert.True(_fs.FileExists("game_state/control/gm_context_pack/Probes/GM_SAFE_PROBES.json"));
        Assert.True(_fs.FileExists("game_state/control/gm_context_pack/Rubrics/GM_LIVE_TEST_RUBRIC.json"));
        Assert.False(_fs.FileExists("game_state/meta/soul_state.json"));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_rootPath))
                Directory.Delete(_rootPath, recursive: true);
        }
        catch
        {
            // Best effort cleanup for temp test data.
        }
    }
}
