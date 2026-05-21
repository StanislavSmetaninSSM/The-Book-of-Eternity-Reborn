using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ImageServiceTests
{
    [Fact]
    public void ExportEntityImage_CopiesLatestVersionToTargetDirectory()
    {
        var root = CreateTempRoot();
        var exportDir = Path.Combine(root, "exports");
        try
        {
            var service = CreateService(root);
            var older = WriteEntityImage(root, "npc", "npc_alpha__img_20260520_010101001.png", "older");
            var newest = WriteEntityImage(root, "npc", "npc_alpha__img_20260520_010101002.png", "newest");
            File.SetLastWriteTimeUtc(older, DateTime.UtcNow.AddMinutes(-10));
            File.SetLastWriteTimeUtc(newest, DateTime.UtcNow);

            var result = service.ExportEntityImage("npc", "npc_alpha", exportDir);

            Assert.True(result.Success, result.ErrorMessage);
            Assert.Equal(newest, result.SourcePath);
            Assert.True(File.Exists(result.DestinationPath));
            Assert.Equal("newest", File.ReadAllText(result.DestinationPath!));
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public void ExportEntityImage_DoesNotOverwriteExistingFileUnlessAllowed()
    {
        var root = CreateTempRoot();
        var exportDir = Path.Combine(root, "exports");
        try
        {
            var service = CreateService(root);
            WriteEntityImage(root, "item", "relic_01.png", "source");
            Directory.CreateDirectory(exportDir);
            var destination = Path.Combine(exportDir, "relic_01.png");
            File.WriteAllText(destination, "existing");

            var blocked = service.ExportEntityImage("item", "relic_01", exportDir, overwrite: false);
            var overwritten = service.ExportEntityImage("item", "relic_01", exportDir, overwrite: true);

            Assert.False(blocked.Success);
            Assert.Equal(ImageExportFailureReason.DestinationExists, blocked.FailureReason);
            Assert.True(overwritten.Success, overwritten.ErrorMessage);
            Assert.Equal("source", File.ReadAllText(destination));
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public void ExportEntityImage_MissingSource_ReturnsClearFailure()
    {
        var root = CreateTempRoot();
        try
        {
            var service = CreateService(root);

            var result = service.ExportEntityImage("guardian", "missing_guardian", Path.Combine(root, "exports"));

            Assert.False(result.Success);
            Assert.Equal(ImageExportFailureReason.SourceMissing, result.FailureReason);
            Assert.Contains("не найдено", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    private static ImageService CreateService(string root)
    {
        var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
        fs.EnsureDirectoryStructure();
        return new ImageService(
            fs,
            new GameSettings { GenerateImagesWithoutDisplay = true },
            new LocalizationManager(),
            NullLogger<ImageService>.Instance);
    }

    private static string WriteEntityImage(string root, string entityType, string fileName, string contents)
    {
        var subDir = entityType switch
        {
            "npc" => "npcs",
            "item" => "items",
            "guardian" => "guardians",
            _ => entityType
        };
        var dir = Path.Combine(root, "game_session", "images", subDir);
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, fileName);
        File.WriteAllText(path, contents);
        return path;
    }

    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "boe_image_service_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void CleanupTempRoot(string root)
    {
        try
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
        catch
        {
            // best-effort cleanup
        }
    }
}
