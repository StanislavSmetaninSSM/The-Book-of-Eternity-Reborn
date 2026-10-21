using System.Text.Json;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Prepares a fresh real runtime from existing fixture DATA without generating a world or granting authority.
/// Root ownership and cleanup remain with the calling test.
/// </summary>
internal static class JointLateTerminalFixture
{
    /// <summary>
    /// Creates an uncapped engine logger whose storage failures remain in the deferred diagnostic ledger.
    /// </summary>
    /// <param name="writer">
    /// The caller-owned synchronized external JSONL writer, kept open through all engine and closing operations.
    /// </param>
    /// <param name="capture">
    /// The existing deferred ledger merged after the actual engine or owner outcome is known.
    /// </param>
    /// <returns>
    /// A logger retaining full messages and exceptions at every level without granting game authority.
    /// </returns>
    internal static ILogger<GameEngine> CreateLogger(TextWriter writer, JointCleanupProfileDiagnosticCapture capture) =>
        new FixtureLogger(writer, capture);

    /// <summary>
    /// Refreshes real state and adopts its observed session and story turn under the exact existing generation.
    /// </summary>
    /// <param name="runtime">
    /// The engine and real services constructed together for <paramref name="files"/>.
    /// </param>
    /// <param name="files">
    /// The manager for this test's independent root; no canonical lease may be held across this call.
    /// </param>
    /// <param name="generation">
    /// The exact existing generation, never created or changed by this adapter.
    /// </param>
    /// <returns>
    /// Completion after read-only refresh and actual production adoption. Missing observed session DATA refuses
    /// before adoption rather than using production's random-session fallback.
    /// </returns>
    internal static async Task RefreshAndAdoptAsync(
        GameEngineTestRuntime runtime, FileSystemManager files, string generation)
    {
        await SessionOperationContext.RunParticipatingExpectedSessionAsync(files, generation, async () =>
        {
            using (files.MeasureOperation("LateProfile.RefreshReadOnlyState"))
            {
                await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
                await runtime.StateManager.RefreshGameStateReadOnlyAsync(lease);
            }
            if (string.IsNullOrWhiteSpace(runtime.StateManager.CurrentState.SessionId))
                throw new InvalidDataException("Observed cleanup DATA has no session.");
            using (files.MeasureOperation("LateProfile.AdoptRuntime"))
                await runtime.Engine.BindCurrentSessionRuntimeFromRefreshedStateAsync();
            return true;
        });
    }

    /// <summary>
    /// Writes complete engine messages without letting diagnostic failures alter the engine's result.
    /// </summary>
    /// <param name="writer">
    /// The external synchronized writer owned by the current profile.
    /// </param>
    /// <param name="capture">
    /// The current profile's deferred diagnostic error ledger.
    /// </param>
    private sealed class FixtureLogger(TextWriter writer, JointCleanupProfileDiagnosticCapture capture) : ILogger<GameEngine>
    {
        /// <inheritdoc/>
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        /// <inheritdoc/>
        public bool IsEnabled(LogLevel logLevel) => true;

        /// <inheritdoc/>
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            try
            {
                writer.WriteLine(JsonSerializer.Serialize(new { Boundary = "joint_late_engine_log",
                    Level = logLevel.ToString(), EventId = eventId.Id,
                    Message = formatter(state, exception), Exception = exception?.ToString() }));
            }
            catch (Exception error) { capture.RecordDiagnosticFailure(error, "engine-log"); }
        }
    }
}
