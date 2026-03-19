using BookOfEternityClient.Services;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class AfterlifeReturnGuardServiceTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly AfterlifeReturnGuardService _service;

    public AfterlifeReturnGuardServiceTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-afterlife-guard-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);

        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
        _service = new AfterlifeReturnGuardService(_fs, NullLogger<AfterlifeReturnGuardService>.Instance);
    }

    [Fact]
    public async Task ActivatePostLifeReturnAsync_WritesGuardAndBuildsReminder()
    {
        await _service.ActivatePostLifeReturnAsync("guard_social_azalia_001", "Азалия", 12);

        var state = await _service.ReadAsync();
        Assert.NotNull(state);
        Assert.Equal(AfterlifeReturnGuardService.PostLifeReturnReason, state!.Reason);
        Assert.Equal(1, state.RemainingProtectedTurns);
        Assert.Equal("guard_social_azalia_001", state.GuardianId);
        Assert.Equal("Азалия", state.GuardianName);
        Assert.Equal(12, state.ActivatedAtTurnNumber);

        var reminder = await _service.BuildSystemReminderFragmentAsync("Chaos Sea");
        Assert.NotNull(reminder);
        Assert.Contains("afterlife_return_guard.json", reminder, StringComparison.Ordinal);
        Assert.Contains("ordinary afterlife turn", reminder, StringComparison.Ordinal);
        Assert.Contains("guardian_forced", reminder, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConsumeAfterAcceptedAfterlifeTurnAsync_RemovesSingleTurnProtection()
    {
        await _service.ActivatePostLifeReturnAsync("guard_social_azalia_001", "Азалия", 7);

        await _service.ConsumeAfterAcceptedAfterlifeTurnAsync(8);

        var state = await _service.ReadAsync();
        Assert.Null(state);
        Assert.False(_fs.FileExists(AfterlifeReturnGuardService.GuardPath));
    }

    [Fact]
    public async Task EnsureHealthyAsync_RemovesGuardOutsideAfterlifeRealm()
    {
        await _service.ActivatePostLifeReturnAsync("guard_social_azalia_001", "Азалия", 9);

        await _service.EnsureHealthyAsync("Mortal World");

        Assert.False(_fs.FileExists(AfterlifeReturnGuardService.GuardPath));
    }

    [Fact]
    public async Task EnsureHealthyAsync_RemovesInvalidGuardFile()
    {
        await _fs.WriteFileAtomicAsync(AfterlifeReturnGuardService.GuardPath, "{ not valid json");

        await _service.EnsureHealthyAsync("Chaos Sea");

        Assert.False(_fs.FileExists(AfterlifeReturnGuardService.GuardPath));
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
