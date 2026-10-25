using System.Collections.Concurrent;

namespace BookOfEternityClient.Core;

/// <summary>
/// Counts canonical source events without invoking caller code or retaining file contents.
/// </summary>
internal sealed class CanonicalReadActivityAccumulator
{
    private readonly ConcurrentDictionary<string, long> _sources = new(StringComparer.Ordinal);

    /// <summary>
    /// Records one actual source event without a diagnostic record quota.
    /// </summary>
    /// <param name="path">
    /// Exact original relative source spelling; <see langword="null"/> is invalid.
    /// </param>
    internal void Record(string path) => _sources.AddOrUpdate(path, 1, static (_, count) => count + 1);

    /// <summary>
    /// Reads the cumulative event count for an exact path.
    /// </summary>
    /// <param name="path">
    /// Exact source spelling; <see langword="null"/> is invalid.
    /// </param>
    /// <returns>
    /// Observed count, or zero before the first event for that spelling.
    /// </returns>
    internal long ReadCount(string path) => _sources.GetValueOrDefault(path);

    /// <summary>
    /// Copies every observed path count without a record or byte limit.
    /// </summary>
    /// <returns>
    /// Detached counters; these are canonical source events, not all filesystem reads.
    /// </returns>
    internal Dictionary<string, long> Capture() => new(_sources, StringComparer.Ordinal);
}
