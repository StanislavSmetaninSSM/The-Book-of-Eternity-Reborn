using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class WorldDirectiveServiceTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly WorldDirectiveService _service;

    public WorldDirectiveServiceTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-world-directives-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
        _service = new WorldDirectiveService(_fs, NullLogger<WorldDirectiveService>.Instance);
    }

    [Fact]
    public async Task GetAvailableProfilesAsync_TextProfile_MapsFullBodyToDetailedWorldDescription()
    {
        var profilePath = _fs.ResolvePath($"{WorldDirectiveService.ProfilesDirectory}/deep_world.md");
        Directory.CreateDirectory(Path.GetDirectoryName(profilePath)!);
        await File.WriteAllTextAsync(profilePath, """
        # Этернум
        Мир бесконечных башен и подземных океанов.

        Это первый большой абзац с подробным описанием мира.

        Это второй абзац, где перечислены свободные правила, исключения и особые культурные договорённости.
        """);

        var profiles = await _service.GetAvailableProfilesAsync();
        var profile = Assert.Single(profiles);

        Assert.Equal("Этернум", profile.Directives.WorldTitle);
        Assert.Equal("Мир бесконечных башен и подземных океанов.", profile.Directives.SettingSummary);
        Assert.Contains("первый большой абзац", profile.Directives.DetailedWorldDescription, StringComparison.Ordinal);
        Assert.Contains("второй абзац", profile.Directives.DetailedWorldDescription, StringComparison.Ordinal);
    }

    [Fact]
    public void CloneDirectives_PreservesDetailedWorldDescription()
    {
        var source = new WorldDirectiveService.WorldDirectives
        {
            WorldTitle = "Этернум",
            SettingSummary = "Краткая сводка",
            DetailedWorldDescription = "Большой\nподробный\nтекст",
            HardRules = new() { "Никакой телепортации" }
        };

        var clone = WorldDirectiveService.CloneDirectives(source);

        Assert.Equal("Большой\nподробный\nтекст", clone.DetailedWorldDescription);
        Assert.Equal("Краткая сводка", clone.SettingSummary);
        Assert.Equal(source.HardRules, clone.HardRules);
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
