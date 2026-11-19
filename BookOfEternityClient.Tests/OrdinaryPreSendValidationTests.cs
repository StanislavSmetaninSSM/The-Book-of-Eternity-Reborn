using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>Exercises the existing physical read admission; no generated response or game turn.</summary>
public sealed class OrdinaryPreSendValidationTests
{
    [Theory]
    [InlineData("input/turn_request.json")]
    [InlineData("game_state/control/pending_turn_snapshot.json")]
    [InlineData("game_state/control/pending_turn_snapshot.authority.json")]
    [InlineData("game_state/control/validation_repair_request.json")]
    [InlineData("game_state/control/validation_repair_ready.json")]
    [InlineData("ready/turn_complete.json")]
    [InlineData("ready/turn_error.json")]
    public async Task PreHealth_ActiveOrMalformedMarkerRefusesWithoutRemoval(string path)
    {
        using var f = new Fixture();
        await f.InitializeAsync();
        var full = f.Files.ResolvePath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await File.WriteAllTextAsync(full, "{}");
        using var owner = f.Files.BeginMainAdmission();
        await owner.AcquireAsync();
        await Assert.ThrowsAnyAsync<Exception>(() => f.Validator.RequireOrdinaryPreSendIdleAsync());
        Assert.Equal("{}", await File.ReadAllTextAsync(full));
    }

    [Fact]
    public async Task PreHealth_OrphanSnapshotDirectoryRefuses()
    {
        using var f = new Fixture();
        await f.InitializeAsync();
        var path = f.Files.ResolvePath(LiveTurnPreparationService.PendingTurnSnapshotDirectory);
        Directory.CreateDirectory(path);
        using var owner = f.Files.BeginMainAdmission();
        await owner.AcquireAsync();
        await Assert.ThrowsAnyAsync<Exception>(() => f.Validator.RequireOrdinaryPreSendIdleAsync());
        Assert.True(Directory.Exists(path));
    }

    [Fact]
    public async Task PreHealth_InitializedFirstTurnNeedsNoRetiredOriginal()
    {
        using var f = new Fixture();
        await f.InitializeAsync();
        using var owner = f.Files.BeginMainAdmission();
        await owner.AcquireAsync();
        await f.Validator.RequireOrdinaryPreSendIdleAsync();
    }

    [Fact]
    public async Task PreHealth_MissingGenerationRefusesWithoutCreatingIt()
    {
        using var f = new Fixture();
        using var owner = f.Files.BeginMainAdmission();
        await owner.AcquireAsync();
        await Assert.ThrowsAnyAsync<Exception>(() => f.Validator.RequireOrdinaryPreSendIdleAsync());
        await using var lease = await f.Files.AcquireCanonicalWriteLeaseAsync();
        Assert.Null(f.Files.ReadExistingSessionGeneration(lease));
    }

    [Fact]
    public async Task PreHealth_NoAmbientOwnerRefuses()
    {
        using var f = new Fixture();
        await f.InitializeAsync();
        await Assert.ThrowsAnyAsync<Exception>(() => f.Validator.RequireOrdinaryPreSendIdleAsync());
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "boe-pre-send-" + Guid.NewGuid().ToString("N"));
        internal readonly FileSystemManager Files;
        internal readonly ValidationService Validator;
        internal Fixture()
        {
            Directory.CreateDirectory(_root);
            Files = new(_root, NullLogger<FileSystemManager>.Instance);
            Files.EnsureDirectoryStructure();
            Validator = new(Files, NullLogger<ValidationService>.Instance);
        }
        internal async Task InitializeAsync()
        {
            await using var lease = await Files.AcquireCanonicalWriteLeaseAsync();
            _ = Files.GetOrCreateSessionGeneration(lease);
        }
        public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
    }
}
