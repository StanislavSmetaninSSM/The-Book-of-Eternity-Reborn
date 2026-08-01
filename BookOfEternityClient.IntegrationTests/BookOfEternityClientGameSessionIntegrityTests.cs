using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

[Trait("Category", "FullValidation")]
public sealed class BookOfEternityClientGameSessionIntegrityTests
{
    [Fact]
    public async Task LocalWorkingGameSession_WhenPresent_ValidatorHasNoBlockingFixtureErrors()
    {
        var sourceRoot = Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient", "game_session");
        if (!Directory.Exists(sourceRoot))
            return;

        var rootPath = Path.Combine(Path.GetTempPath(), "boe-local-working-game-session-validation-" + Guid.NewGuid().ToString("N"));
        try
        {
            CopyDirectory(sourceRoot, Path.Combine(rootPath, "game_session"));
            CopyDirectory(
                Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient", "system_guardians"),
                Path.Combine(rootPath, "system_guardians"));
            var fs = new FileSystemManager(rootPath, NullLogger<FileSystemManager>.Instance);
            var validator = new ValidationService(fs, NullLogger<ValidationService>.Instance);

            var issues = await validator.ValidateGameStateAsync();

            var errors = issues
                .Where(issue => issue.Severity == IssueSeverity.Error)
                .Select(issue => issue.ToString())
                .ToArray();

            Assert.True(
                errors.Length == 0,
                "Local BookOfEternityClient/game_session must be directly usable for live tests when present. Validation errors:" +
                Environment.NewLine + string.Join(Environment.NewLine, errors));
        }
        finally
        {
            if (Directory.Exists(rootPath))
                Directory.Delete(rootPath, recursive: true);
        }
    }

    private static void CopyDirectory(string sourceDir, string destinationDir)
    {
        Directory.CreateDirectory(destinationDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var destFile = Path.Combine(destinationDir, Path.GetFileName(file));
            File.Copy(file, destFile, overwrite: true);
        }

        foreach (var directory in Directory.GetDirectories(sourceDir))
        {
            var destSubDir = Path.Combine(destinationDir, Path.GetFileName(directory));
            CopyDirectory(directory, destSubDir);
        }
    }
}
