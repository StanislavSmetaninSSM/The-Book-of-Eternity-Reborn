using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class CraftRequestStateTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;

    public CraftRequestStateTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-craft-request-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
    }

    [Fact]
    public async Task BuildSystemReminderFragmentAsync_MortalWorld_IncludesPendingCraftRequest()
    {
        await _fs.WriteFileAtomicAsync(CraftRequestState.PendingRequestPath, """
        {
          "schemaVersion": 1,
          "requestId": "craft_req_1",
          "createdAtUtc": "2026-05-21T10:00:00Z",
          "source": "browser",
          "status": "pending_gm_resolution",
          "recipeId": "healing_salve",
          "craftIntent": "Сделать припарку из трав."
        }
        """);

        var reminder = await CraftRequestState.BuildSystemReminderFragmentAsync(_fs, "Mortal World");

        Assert.NotNull(reminder);
        Assert.Contains(CraftRequestState.PendingRequestPath, reminder!, StringComparison.Ordinal);
        Assert.Contains("healing_salve", reminder, StringComparison.Ordinal);
        Assert.Contains("Сделать припарку", reminder, StringComparison.Ordinal);
        Assert.Contains("UpdateInventory", reminder, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildSystemReminderFragmentAsync_NoRequest_ReturnsNull()
    {
        var reminder = await CraftRequestState.BuildSystemReminderFragmentAsync(_fs, "Mortal World");

        Assert.Null(reminder);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
            Directory.Delete(_rootPath, recursive: true);
    }
}
