using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class SoulIdentityValidationTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly ValidationService _validator;

    public SoulIdentityValidationTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-soul-identity-validation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);

        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
        _validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
    }

    [Fact]
    public async Task ValidateGameStateAsync_SoulFormDescriptionString_IsAccepted()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Асуран",
          "soulFormDescription": "Мужчина из тихого серебряного света",
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 2
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "soul_form_description_invalid_shape", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(issue.Code, "soul_form_description_empty", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_SoulFormDescriptionObject_IsRejected()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Асуран",
          "soulFormDescription": {
            "gender": "male",
            "form": "silver light"
          },
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 2
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "soul_form_description_invalid_shape", StringComparison.OrdinalIgnoreCase) &&
            issue.Severity == IssueSeverity.Error);
    }

    [Fact]
    public async Task ValidateGameStateAsync_EmptySoulFormDescription_Warns()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Асуран",
          "soulFormDescription": "   ",
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 2
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "soul_form_description_empty", StringComparison.OrdinalIgnoreCase) &&
            issue.Severity == IssueSeverity.Warning);
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
            // Ignore temp cleanup failures on Windows.
        }
    }
}
