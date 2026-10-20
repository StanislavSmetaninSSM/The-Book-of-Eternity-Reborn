namespace BookOfEternityClient.Services.GmWorkers;

/// <summary>
/// Observes decoded finite pipe transport without granting process ownership.
/// </summary>
/// <param name="Eof">
/// Whether the reader positively returned EOF.
/// </param>
/// <param name="ReadFailure">
/// The original read failure, or null after EOF.
/// </param>
/// <param name="WriteFailure">
/// The first destination failure, or null when all writes succeeded.
/// </param>
/// <param name="ObserverFailure">
/// The first ancillary observer failure, or null when observation succeeded.
/// </param>
internal sealed record FinitePipeDrainResult(bool Eof, Exception? ReadFailure, Exception? WriteFailure,
    Exception? ObserverFailure = null);

/// <summary>
/// Copies decoded finite child output to a caller-owned destination.
/// </summary>
internal static class FinitePipeOutput
{
    /// <summary>
    /// Drains a reader while leaving its lifetime and the writer lifetime with the caller.
    /// </summary>
    /// <param name="reader">
    /// The exclusive decoded pipe reader.
    /// </param>
    /// <param name="destination">
    /// The continuous decoded-text destination.
    /// </param>
    /// <param name="observe">
    /// Optional synchronous chunk observation; memory is valid only during the callback.
    /// </param>
    /// <returns>
    /// Separate EOF, read failure and destination failure observations.
    /// </returns>
    internal static async Task<FinitePipeDrainResult> DrainAsync(TextReader reader, TextWriter destination,
        Action<ReadOnlyMemory<char>>? observe = null)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(destination);
        var buffer = new char[4096];
        Exception? writeFailure = null, observerFailure = null;
        while (true)
        {
            int count;
            try { count = await reader.ReadAsync(buffer.AsMemory()).ConfigureAwait(false); }
            catch (Exception error) when (Recoverable(error)) { return new(false, error, writeFailure, observerFailure); }
            if (count == 0) return new(true, null, writeFailure, observerFailure);
            if (writeFailure == null)
            {
                try { await destination.WriteAsync(buffer.AsMemory(0, count)).ConfigureAwait(false); }
                catch (Exception error) when (Recoverable(error)) { writeFailure = error; }
            }
            // Observation does not own storage or native evidence. Its adapter records its own failure.
            if (observerFailure == null && observe != null)
            {
                try { observe(buffer.AsMemory(0, count)); }
                catch (Exception error) when (Recoverable(error)) { observerFailure = error; }
            }
        }
    }

    /// <summary>
    /// Identifies failures that can be recorded while pipe consumption continues.
    /// </summary>
    /// <param name="error">
    /// The observed operation failure.
    /// </param>
    /// <returns>
    /// <see langword="true"/> for ordinary exceptions; <see langword="false"/> for failures that cannot be handled reliably.
    /// </returns>
    internal static bool Recoverable(Exception error) => error is not OutOfMemoryException and not StackOverflowException;
}

/// <summary>
/// Supplies continuous finite-pipe destinations before exclusive reads begin.
/// </summary>
/// <param name="StandardOutput">
/// The caller-owned standard output writer.
/// </param>
/// <param name="StandardError">
/// The independent caller-owned standard error writer.
/// </param>
internal sealed record NativeFinitePipeDestinations(TextWriter StandardOutput, TextWriter StandardError);

/// <summary>
/// Returns actual drain observations without asserting artifact persistence.
/// </summary>
/// <param name="StandardOutput">
/// The original output drain result.
/// </param>
/// <param name="StandardError">
/// The original error drain result.
/// </param>
internal sealed record NativeFinitePipeOutputs(FinitePipeDrainResult StandardOutput, FinitePipeDrainResult StandardError);
