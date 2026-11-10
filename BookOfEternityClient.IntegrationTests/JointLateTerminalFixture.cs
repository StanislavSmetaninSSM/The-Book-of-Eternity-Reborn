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
    /// Imports immutable cleanup DATA into the caller's independent profile layout.
    /// </summary>
    /// <param name="ownedRoot">
    /// The ordinary caller-owned directory containing a nonexistent <c>copy</c> child.
    /// Missing owned-directory ancestors are created before strict DATA import.
    /// The caller retains responsibility for this directory and its cleanup.
    /// </param>
    internal static void MaterializeObservedCleanupData(string ownedRoot)
    {
        Directory.CreateDirectory(ownedRoot);
        JointCleanupDataTemplate.Materialize(JointCleanupValidatorTestData.ArchivePath,
            JointCleanupValidatorTestData.ArchiveSha256, Path.Combine(ownedRoot, "copy"));
    }

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
        new JointComponentDiagnosticLogger<GameEngine>(writer, capture, "joint_late_engine_log");

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
}
