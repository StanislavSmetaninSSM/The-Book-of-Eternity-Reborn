using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

[Trait("Category", "ProcessIntegration")]
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

    [Fact]
    public void GetEntityImagePath_TraversalEntityTypeCannotReadOutsideImages()
    {
        var root = CreateTempRoot();
        try
        {
            var service = CreateService(root);
            var escapedDir = Path.Combine(root, "game_session", "game_state", "meta");
            Directory.CreateDirectory(escapedDir);
            File.WriteAllText(Path.Combine(escapedDir, "secret.png"), "outside-images");

            var result = service.GetEntityImagePath("../game_state/meta", "secret");

            Assert.Null(result);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public void GetEntityImagePath_PermanentJunctionCannotExposeOutsideImage()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var root = CreateTempRoot();
        var outsideRoot = CreateTempRoot();
        var junctionPath = Path.Combine(root, "game_session", "images", "npcs");
        try
        {
            var service = CreateService(root);
            Directory.CreateDirectory(outsideRoot);
            File.WriteAllText(
                Path.Combine(outsideRoot, "npc_alpha__img_20260520_010101001.png"),
                "outside-image");
            if (Directory.Exists(junctionPath))
                Directory.Delete(junctionPath, recursive: true);
            CreateDirectoryJunction(junctionPath, outsideRoot);

            Assert.Throws<InvalidDataException>(
                () => service.GetEntityImagePath("npc", "npc_alpha"));
        }
        finally
        {
            if (Directory.Exists(junctionPath) &&
                (File.GetAttributes(junctionPath) & FileAttributes.ReparsePoint) != 0)
            {
                Directory.Delete(junctionPath, recursive: false);
            }

            CleanupTempRoot(root);
            CleanupTempRoot(outsideRoot);
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

    private static void CreateDirectoryJunction(string junctionPath, string targetPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(junctionPath)!);
        using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/d /c mklink /J \"{junctionPath}\" \"{targetPath}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        }) ?? throw new InvalidOperationException("Failed to start junction helper.");
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Failed to create test junction: exit code {process.ExitCode}.");
        }
    }
}
