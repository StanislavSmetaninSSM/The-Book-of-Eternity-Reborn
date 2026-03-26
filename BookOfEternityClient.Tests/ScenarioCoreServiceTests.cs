using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ScenarioCoreServiceTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly WorldDirectiveService _worldDirectiveService;
    private readonly ScenarioCoreService _scenarioCoreService;

    public ScenarioCoreServiceTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-scenario-core-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);

        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
        _worldDirectiveService = new WorldDirectiveService(_fs, NullLogger<WorldDirectiveService>.Instance);
        _scenarioCoreService = new ScenarioCoreService(_fs, NullLogger<ScenarioCoreService>.Instance);
    }

    [Fact]
    public async Task RefreshFromPendingSetup_CreatesStructuredCoreAndCandidates()
    {
        await _worldDirectiveService.WritePendingSetupAsync(new WorldDirectiveService.PendingWorldSetup
        {
            Mode = "manual",
            CharacterDescription = "Я король древнего королевства и храню клятву своему роду.",
            StartingCircumstances = "Начинаю во дворце среди верных советников.",
            WorldDirectives = new WorldDirectiveService.WorldDirectives
            {
                WorldTitle = "Этернум",
                Genre = "Dark fantasy",
                DetailedWorldDescription = "Королевство процветает, но в тени растёт культ затмения."
            }
        });

        await _scenarioCoreService.RefreshFromPendingSetupAsync();
        var manifest = await _scenarioCoreService.ReadAsync();

        Assert.NotNull(manifest);
        Assert.Contains(manifest!.ScenarioCoreAssertions, item => item.Category == "identity_anchor");
        Assert.Contains(manifest.ScenarioCoreAssertions, item => item.Category == "role_status");
        Assert.Contains(manifest.ScenarioCoreAssertions, item => item.Category == "start_location");
        Assert.Contains(manifest.CandidateAssertions, item => item.Text.Contains("культ затмения", StringComparison.OrdinalIgnoreCase));
        Assert.NotEmpty(manifest.OpenCorrectionSlots);
    }

    [Fact]
    public async Task SetCandidateConfirmedAsync_PromotesCandidateIntoScenarioCore()
    {
        await _worldDirectiveService.WritePendingSetupAsync(new WorldDirectiveService.PendingWorldSetup
        {
            Mode = "manual",
            WorldDirectives = new WorldDirectiveService.WorldDirectives
            {
                DetailedWorldDescription = "Королевство процветает и город безопасен."
            }
        });

        await _scenarioCoreService.RefreshFromPendingSetupAsync();
        var initial = await _scenarioCoreService.ReadAsync();
        Assert.NotNull(initial);
        var candidate = initial!.CandidateAssertions.First(item => item.Text.Contains("процветает", StringComparison.OrdinalIgnoreCase));

        await _scenarioCoreService.SetCandidateConfirmedAsync(candidate.CandidateId, true);
        var refreshed = await _scenarioCoreService.ReadAsync();

        Assert.NotNull(refreshed);
        Assert.Contains(refreshed!.ScenarioCoreAssertions, item => string.Equals(item.CandidateId, candidate.CandidateId, StringComparison.OrdinalIgnoreCase));
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
            // Ignore cleanup failures in temp test directories.
        }
    }
}
