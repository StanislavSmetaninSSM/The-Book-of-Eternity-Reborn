using System.Text.Json;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Exercises direct runtime adoption and refusal boundaries with independent minimal fixtures.
/// </summary>
public sealed class JointLateTerminalControlsTests
{
    /// <summary>
    /// Retains a complete long engine diagnostic and its exception instead of the historical short message tail.
    /// </summary>
    [Fact]
    public void EngineLoggerKeepsCompleteMessageAndException()
    {
        using var writer = new StringWriter();
        var capture = new JointCleanupProfileDiagnosticCapture(writer);
        var logger = JointLateTerminalFixture.CreateLogger(writer, capture);
        var message = new string('x', 4097) + "-complete-tail";
        var error = new InvalidDataException("complete-engine-failure");
        logger.LogError(error, "{Message}", message);
        using var row = JsonDocument.Parse(writer.ToString());
        Assert.Equal(message, row.RootElement.GetProperty("Message").GetString());
        Assert.Equal(error.ToString(), row.RootElement.GetProperty("Exception").GetString());
        Assert.True(capture.Complete);
    }

    /// <summary>
    /// Makes engine-log storage failure explicit without throwing it into the actual game operation.
    /// </summary>
    [Fact]
    public void EngineLoggerDefersStorageFailureWithoutReplacingGameOutcome()
    {
        using var writer = new StringWriter();
        var capture = new JointCleanupProfileDiagnosticCapture(writer);
        var logger = JointLateTerminalFixture.CreateLogger(writer, capture);
        writer.Dispose();
        logger.LogWarning("lost-engine-log");
        Assert.False(capture.Complete);
        var failure = Assert.Single(capture.GetFailures());
        Assert.Equal("engine-log", failure.Stage);
        Assert.IsType<ObjectDisposedException>(failure.Error);
    }

    /// <summary>
    /// Derives the runtime session and maximum story turn through the same entry used by production loading.
    /// </summary>
    /// <returns>
    /// Completion after actual read-only refresh and adoption without constructing a game world.
    /// </returns>
    [Fact]
    public async Task MinimalFixtureAdoptsRefreshedSessionAndActualStoryMaximum()
    {
        var root = NewMinimalRoot("minimal-session");
        using var cleanup = new CleanupOwnedFixture(root, Console.WriteLine);
        var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
        var generation = await CreateSyntheticGenerationAsync(files);
        var before = JointCleanupValidatorTestData.CaptureImages(root);
        var runtime = GameEngineTurnLifecycleTests.CreateGameEngineRuntime(files);
        await JointLateTerminalFixture.RefreshAndAdoptAsync(runtime, files, generation);
        Assert.Equal("minimal-session", runtime.GameLoop.SessionId);
        Assert.Equal(13, runtime.GameLoop.TurnNumber);
        Assert.Null(runtime.Engine.CaptureCurrentResponseForTesting());
        Assert.Equal(before.OrderBy(pair => pair.Key), JointCleanupValidatorTestData.CaptureImages(root).OrderBy(pair => pair.Key));
    }

    /// <summary>
    /// Preserves the production fallback when the already refreshed state has no session identifier.
    /// </summary>
    /// <returns>
    /// Completion after direct adoption produces a new session and retains the story-derived turn.
    /// </returns>
    [Fact]
    public async Task DirectProductionAdoptionPreservesEmptySessionFallback()
    {
        var root = NewMinimalRoot(null);
        using var cleanup = new CleanupOwnedFixture(root, Console.WriteLine);
        var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
        var generation = await CreateSyntheticGenerationAsync(files);
        var runtime = GameEngineTurnLifecycleTests.CreateGameEngineRuntime(files);
        await SessionOperationContext.RunParticipatingExpectedSessionAsync(files, generation, async () =>
        {
            await runtime.Engine.BindCurrentSessionRuntimeFromRefreshedStateAsync();
            return true;
        });
        Assert.True(Guid.TryParse(runtime.GameLoop.SessionId, out _));
        Assert.Equal(13, runtime.GameLoop.TurnNumber);
    }

    /// <summary>
    /// Prevents observed-DATA preparation from using the production random-session fallback.
    /// </summary>
    /// <param name="session">
    /// The absent or blank session value in the minimal stored chat DATA.
    /// </param>
    /// <returns>
    /// Completion after preparation refuses before runtime adoption and preserves stored game images.
    /// </returns>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task ObservedDataPreparationRefusesMissingSession(string? session)
    {
        var root = NewMinimalRoot(session);
        using var cleanup = new CleanupOwnedFixture(root, Console.WriteLine);
        var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
        var generation = await CreateSyntheticGenerationAsync(files);
        var runtime = GameEngineTurnLifecycleTests.CreateGameEngineRuntime(files);
        var originalSession = runtime.GameLoop.SessionId;
        var before = JointCleanupValidatorTestData.CaptureImages(root);
        var error = await Assert.ThrowsAsync<InvalidDataException>(() =>
            JointLateTerminalFixture.RefreshAndAdoptAsync(runtime, files, generation));
        Assert.Equal("Observed cleanup DATA has no session.", error.Message);
        Assert.Equal(originalSession, runtime.GameLoop.SessionId);
        Assert.Equal(0, runtime.GameLoop.TurnNumber);
        Assert.Equal(before.OrderBy(pair => pair.Key), JointCleanupValidatorTestData.CaptureImages(root).OrderBy(pair => pair.Key));
    }

    /// <summary>
    /// Refuses absent or replaced generation before adopting the stored session.
    /// </summary>
    /// <param name="fault">
    /// Missing deletes the synthetic generation; replaced installs an unrelated valid generation DATA value.
    /// </param>
    /// <returns>
    /// Completion after the real expected-session boundary refuses without bootstrap or runtime adoption.
    /// </returns>
    [Theory]
    [InlineData("missing")]
    [InlineData("replaced")]
    public async Task ObservedDataPreparationRefusesGenerationDrift(string fault)
    {
        var root = NewMinimalRoot("minimal-session");
        using var cleanup = new CleanupOwnedFixture(root, Console.WriteLine);
        var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
        var generation = await CreateSyntheticGenerationAsync(files);
        var runtime = GameEngineTurnLifecycleTests.CreateGameEngineRuntime(files);
        var originalSession = runtime.GameLoop.SessionId;
        if (fault == "missing") File.Delete(files.SessionGenerationPath);
        else File.WriteAllText(files.SessionGenerationPath,
            JsonSerializer.Serialize(new { schemaVersion = 1, generationId = Guid.NewGuid().ToString("N") }));
        await Assert.ThrowsAsync<SessionReplacedException>(() =>
            JointLateTerminalFixture.RefreshAndAdoptAsync(runtime, files, generation));
        Assert.Equal(originalSession, runtime.GameLoop.SessionId);
        Assert.Equal(0, runtime.GameLoop.TurnNumber);
        if (fault == "missing") Assert.False(File.Exists(files.SessionGenerationPath));
    }

    /// <summary>
    /// Refuses an unrelated runtime through the actual late-terminal caller without deleting original DATA.
    /// </summary>
    /// <returns>
    /// Completion after held disposition, exact game preservation and genuine outer operation closing.
    /// </returns>
    [Fact]
    public async Task ActualLateOwnerHoldsForeignRuntimeWithoutCleanup()
    {
        var root = NewRoot();
        JointCleanupDataTemplate.Materialize(JointCleanupValidatorTestData.ArchivePath,
            JointCleanupValidatorTestData.ArchiveSha256, root);
        using var cleanup = new CleanupOwnedFixture(root, Console.WriteLine);
        var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
        string generation;
        await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
            generation = files.ReadExistingSessionGeneration(lease)!;
        var before = JointCleanupValidatorTestData.CaptureImages(root);
        var directories = JointCleanupValidatorTestData.CaptureDirectories(root);
        var runtime = GameEngineTurnLifecycleTests.CreateGameEngineRuntime(files);
        await SessionOperationContext.RunParticipatingExpectedSessionAsync(files, generation, async () =>
        {
            await JointLateTerminalFixture.RefreshAndAdoptAsync(runtime, files, generation);
            runtime.GameLoop.SetSession("foreign-runtime", runtime.GameLoop.TurnNumber);
            Assert.True(await runtime.Engine.ProcessLateTerminalAndIdleTransitionsForCurrentSessionAsync());
            return true;
        });
        Assert.Null(runtime.Engine.CaptureCurrentResponseForTesting());
        Assert.Equal(before.OrderBy(pair => pair.Key), JointCleanupValidatorTestData.CaptureImages(root).OrderBy(pair => pair.Key));
        Assert.Equal(directories, JointCleanupValidatorTestData.CaptureDirectories(root));
    }

    /// <summary>
    /// Creates only the small history and two story files needed to distinguish actual maximum-turn adoption.
    /// </summary>
    /// <param name="session">
    /// The stored session identifier, including null or blank DATA for refusal cases.
    /// </param>
    /// <returns>
    /// A new independently owned root without a generated world or prepared gameplay turns.
    /// </returns>
    private static string NewMinimalRoot(string? session)
    {
        var root = NewRoot();
        var game = Path.Combine(root, "game_session");
        Directory.CreateDirectory(Path.Combine(game, "game_state", "history"));
        Directory.CreateDirectory(Path.Combine(game, "stories"));
        File.WriteAllText(Path.Combine(game, "game_state", "history", "chat_log.json"), JsonSerializer.Serialize(new { sessionId = session }));
        File.WriteAllText(Path.Combine(game, "stories", "mortal_life_1.jsonl"), "{\"turn\":2}\n{\"turn\":7}\n");
        File.WriteAllText(Path.Combine(game, "stories", "chaos_sea.jsonl"), "{\"turn\":13}\n");
        return root;
    }

    /// <summary>
    /// Establishes a real generation only for the explicitly synthetic control fixture.
    /// </summary>
    /// <param name="files">
    /// The manager owning the independent synthetic root.
    /// </param>
    /// <returns>
    /// The existing-or-created synthetic generation after its real lease closes.
    /// </returns>
    private static async Task<string> CreateSyntheticGenerationAsync(FileSystemManager files)
    {
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        return files.GetOrCreateSessionGeneration(lease);
    }

    /// <summary>
    /// Selects an independent control root without creating or adopting another test's state.
    /// </summary>
    /// <returns>
    /// A unique nonexistent path under the repository's test-result directory.
    /// </returns>
    private static string NewRoot() => Path.Combine(TestRepoPaths.RepoRoot, "TestResults", "joint-late-controls", Guid.NewGuid().ToString("N"));
}
