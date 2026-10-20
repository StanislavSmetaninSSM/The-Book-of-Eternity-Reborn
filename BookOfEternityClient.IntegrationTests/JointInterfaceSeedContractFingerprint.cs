namespace BookOfEternityClient.Tests;

/// <summary>
/// Identifies the source and base-world inputs of a generated seed independently of checkout location.
/// </summary>
internal static class JointInterfaceSeedContractFingerprint
{
    /// <summary>
    /// Hashes the declared generator and game input closure without build outputs or documentation.
    /// </summary>
    /// <param name="repoRoot">
    /// The repository containing the three C# projects and base game-session assets.
    /// </param>
    /// <returns>
    /// A canonical uppercase SHA256 digest of relative names and exact input bytes.
    /// </returns>
    internal static string Compute(string repoRoot) => throw new NotSupportedException("T270 seed fingerprint is not implemented.");
}
