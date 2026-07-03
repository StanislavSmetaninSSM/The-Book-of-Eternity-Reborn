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
