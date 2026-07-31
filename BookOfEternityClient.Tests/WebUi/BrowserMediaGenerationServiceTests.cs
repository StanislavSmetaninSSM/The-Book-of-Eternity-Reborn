using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;
using BookOfEternityClient.WebUi;
using Microsoft.Extensions.Logging.Abstractions;
using System.Diagnostics;
using Xunit;

namespace BookOfEternityClient.Tests.WebUi;

public sealed class BrowserMediaGenerationServiceTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;

    public BrowserMediaGenerationServiceTests()
    {
        _rootPath = Path.Combine(
            Path.GetTempPath(),
            "boe-browser-media-generation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
    }

    [Fact]
    public async Task GenerateAsync_SessionReplacedWhileRemoteBytesAreStaged_DoesNotOverwriteReplacement()
    {
        const string targetPath = "images/npcs/test_actor__img_generation.png";
        var sessionABytes = Enumerable.Repeat((byte)0xA1, 2048).ToArray();
        var sessionBBytes = Enumerable.Repeat((byte)0xB2, 2048).ToArray();
        var settings = new GameSettings { ImageProvider = "pollinations" };
        var imageService = new ImageService(
            _fs,
            settings,
            new LocalizationManager(),
            NullLogger<ImageService>.Instance);
        var stageStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var allowStageReturn = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var service = new BrowserMediaGenerationService(
            imageService,
            new LocalMediaService(_fs),
            settings,
            _fs,
            async _ =>
            {
                stageStarted.SetResult();
                await allowStageReturn.Task;
                return new StagedEntityImage(sessionABytes, targetPath);
            });

        var generation = service.GenerateAsync(
            new BrowserMediaGenerateRequest(
                "test prompt",
                "npc",
                "test_actor"));

        await stageStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await _fs.ClearGameStateAsync();
        await _fs.WriteFileAtomicBytesAsync(targetPath, sessionBBytes);
        allowStageReturn.SetResult();

        var result = await generation.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.False(result.Success);
        Assert.Contains("сессия", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(sessionBBytes, await _fs.ReadFileBytesAsync(targetPath));
    }

    [Fact]
    public async Task GenerateAsync_CurrentGeneration_AtomicallyCommitsStagedBytes()
    {
        const string targetPath = "images/locations/test_location__img_generation.png";
        var generatedBytes = Enumerable.Repeat((byte)0xC3, 2048).ToArray();
        var settings = new GameSettings { ImageProvider = "pollinations" };
        var imageService = new ImageService(
            _fs,
            settings,
            new LocalizationManager(),
            NullLogger<ImageService>.Instance);
        var service = new BrowserMediaGenerationService(
            imageService,
            new LocalMediaService(_fs),
            settings,
            _fs,
            _ =>
            {
                return Task.FromResult<StagedEntityImage?>(
                    new StagedEntityImage(generatedBytes, targetPath));
            });

        var result = await service.GenerateAsync(
            new BrowserMediaGenerateRequest(
                "test prompt",
                "location",
                "test_location"));

        Assert.True(result.Success, result.ErrorMessage);
        Assert.NotNull(result.MediaId);
        Assert.NotNull(result.Url);
        Assert.Equal(generatedBytes, await _fs.ReadFileBytesAsync(targetPath));
    }

    [Fact]
    public async Task GenerateAsync_StagedPathOutsideImagesFailsClosed()
    {
        const string escapedPath = "game_state/meta/generated_escape.png";
        var generatedBytes = Enumerable.Repeat((byte)0xD4, 512).ToArray();
        var settings = new GameSettings { ImageProvider = "pollinations" };
        var imageService = new ImageService(
            _fs,
            settings,
            new LocalizationManager(),
            NullLogger<ImageService>.Instance);
        var service = new BrowserMediaGenerationService(
            imageService,
            new LocalMediaService(_fs),
            settings,
            _fs,
            _ => Task.FromResult<StagedEntityImage?>(
                new StagedEntityImage(generatedBytes, escapedPath)));

        var result = await service.GenerateAsync(
            new BrowserMediaGenerateRequest(
                "test prompt",
                "npc",
                "escaped_actor"));

        Assert.False(result.Success);
        Assert.False(_fs.FileExists(escapedPath));
    }

    [Fact]
    public async Task GenerateAsync_ImagesJunctionCannotWriteOutsideSession()
    {
        if (!OperatingSystem.IsWindows())
            return;

        const string targetPath = "images/npcs/junction_escape__img_generation.png";
        var outsideRoot = Path.Combine(
            Path.GetTempPath(),
            "boe-browser-media-outside-" + Guid.NewGuid().ToString("N"));
        var junctionPath = Path.Combine(_fs.GameSessionPath, "images", "npcs");
        Directory.CreateDirectory(outsideRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(junctionPath)!);
        if (Directory.Exists(junctionPath))
            Directory.Delete(junctionPath, recursive: true);
        CreateDirectoryJunction(junctionPath, outsideRoot);

        try
        {
            var generatedBytes = Enumerable.Repeat((byte)0xE5, 512).ToArray();
            var settings = new GameSettings { ImageProvider = "pollinations" };
            var imageService = new ImageService(
                _fs,
                settings,
                new LocalizationManager(),
                NullLogger<ImageService>.Instance);
            var service = new BrowserMediaGenerationService(
                imageService,
                new LocalMediaService(_fs),
                settings,
                _fs,
                _ => Task.FromResult<StagedEntityImage?>(
                    new StagedEntityImage(generatedBytes, targetPath)));

            var result = await service.GenerateAsync(
                new BrowserMediaGenerateRequest(
                    "test prompt",
                    "npc",
                    "junction_escape"));

            Assert.False(result.Success);
            Assert.False(File.Exists(Path.Combine(outsideRoot, "junction_escape__img_generation.png")));
        }
        finally
        {
            if (Directory.Exists(junctionPath))
                Directory.Delete(junctionPath);
            if (Directory.Exists(outsideRoot))
                Directory.Delete(outsideRoot, recursive: true);
        }
    }

    private static void CreateDirectoryJunction(string junctionPath, string targetPath)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/d /c mklink /J \"{junctionPath}\" \"{targetPath}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        using var process = Process.Start(startInfo);
        Assert.NotNull(process);
        process!.WaitForExit();
        Assert.Equal(0, process.ExitCode);
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
            // ignored
        }
    }
}
