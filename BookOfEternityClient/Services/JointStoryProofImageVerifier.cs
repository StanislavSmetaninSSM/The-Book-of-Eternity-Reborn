namespace BookOfEternityClient.Services;

/// <summary>
/// Compares detached story readback fields against expected proof contents without issuing authority.
/// </summary>
internal static class JointStoryProofImageVerifier
{
    /// <summary>
    /// Requires appended disposition, exact row/file bytes and the complete expected history bindings.
    /// </summary>
    /// <param name="expectedPath">
    /// The exact expected story path.
    /// </param>
    /// <param name="expectedRow">
    /// The exact expected serialized story row.
    /// </param>
    /// <param name="expectedBytes">
    /// The expected complete story bytes, including its retained prefix and appended row.
    /// </param>
    /// <param name="expectedHistories">
    /// The original expected history bindings; its key comparer governs lookup equality.
    /// </param>
    /// <param name="currentAppended">
    /// Whether the authenticated source contains the expected appended row.
    /// </param>
    /// <param name="currentPath">
    /// The authenticated source's current story path.
    /// </param>
    /// <param name="currentRow">
    /// The authenticated source's current serialized story row.
    /// </param>
    /// <param name="currentBytes">
    /// The current complete story bytes; <see langword="null"/> is refused.
    /// </param>
    /// <param name="currentHistories">
    /// All current history bindings; missing, extra or changed entries are refused.
    /// </param>
    internal static void RequireExact(string expectedPath, string expectedRow, byte[] expectedBytes,
        IReadOnlyDictionary<string, string> expectedHistories, bool currentAppended, string currentPath,
        string currentRow, byte[]? currentBytes, IReadOnlyDictionary<string, string> currentHistories)
    {
        if (!currentAppended || currentPath != expectedPath || currentRow != expectedRow ||
            currentBytes is null || !expectedBytes.AsSpan().SequenceEqual(currentBytes) ||
            currentHistories.Count != expectedHistories.Count ||
            currentHistories.Any(pair => !expectedHistories.TryGetValue(pair.Key, out var hash) || hash != pair.Value))
            throw new InvalidDataException("joint_story_proof_current_changed");
    }
}
