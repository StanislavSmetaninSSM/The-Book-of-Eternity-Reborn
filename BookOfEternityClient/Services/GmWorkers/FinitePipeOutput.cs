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
internal sealed record FinitePipeDrainResult(bool Eof, Exception? ReadFailure, Exception? WriteFailure);

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
        string text;
        try { text = await reader.ReadToEndAsync(); }
        catch (Exception error) { return new(false, error, null); }
        try { await destination.WriteAsync(text); observe?.Invoke(text.AsMemory()); return new(true, null, null); }
        catch (Exception error) { return new(true, null, error); }
    }
}
