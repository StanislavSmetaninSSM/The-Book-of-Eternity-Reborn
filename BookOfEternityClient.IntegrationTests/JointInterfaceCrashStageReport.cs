namespace BookOfEternityClient.Tests;

/// <summary>
/// Writes bounded scalar ordinary-test stages without changing game or native verdicts.
/// </summary>
internal sealed class JointInterfaceCrashStageReport
{
    /// <summary>
    /// Creates an independent stage writer for one child.
    /// </summary>
    /// <param name="writer">
    /// The existing child output stream; the reporter neither owns nor flushes it.
    /// </param>
    /// <param name="runId">
    /// The parent-created GUID N run identity, separate from native owner identity.
    /// </param>
    /// <param name="nonce">
    /// The independent GUID N child scenario nonce.
    /// </param>
    /// <param name="identity">
    /// The child's independently read platform identity.
    /// </param>
    /// <param name="cut">
    /// The original ordinary publication cut from zero through four.
    /// </param>
    internal JointInterfaceCrashStageReport(TextWriter writer, string runId, string nonce, JointInterfaceCrashIdentity identity, int cut) { }

    /// <summary>
    /// Gets the first bounded observer error type, or null when writing has not failed.
    /// </summary>
    internal string? OutputFailure { get; private set; }

    /// <summary>
    /// Emits one bounded stage observation without an external observer or flush wait.
    /// </summary>
    /// <param name="stage">
    /// A fixed ordinary-test stage name.
    /// </param>
    /// <param name="transition">
    /// The enter, return or error transition; the writer reserves limit for its terminal marker.
    /// </param>
    /// <param name="errorType">
    /// An optional exception type name, truncated to at most256 characters; no exception messages or game payloads.
    /// </param>
    internal void Observe(string stage, string transition, string? errorType = null) { }
}
