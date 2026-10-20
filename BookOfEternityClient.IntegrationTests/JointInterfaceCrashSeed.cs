namespace BookOfEternityClient.Tests;

/// <summary>
/// Records detached provenance for the durable source checkpoint, without granting game authority.
/// </summary>
/// <param name="SessionId">
/// The nonblank source session identifier.
/// </param>
/// <param name="RequestId">
/// The nonblank source request identifier.
/// </param>
/// <param name="SnapshotToken">
/// The nonblank source snapshot token.
/// </param>
/// <param name="TurnNumber">
/// The completed source turn, which must be 42.
/// </param>
/// <param name="OpportunityRef">
/// The nonblank source opportunity reference.
/// </param>
/// <param name="GenerationSha256">
/// The uppercase SHA256 digest of the exact generation data bytes.
/// </param>
/// <param name="ProducerVersion">
/// The nonblank informational generator source version.
/// </param>
/// <param name="Culture">
/// The source generation culture, which must be en-GB.
/// </param>
internal sealed record JointInterfaceCrashSeedMetadata(
    string SessionId,
    string RequestId,
    string SnapshotToken,
    int TurnNumber,
    string OpportunityRef,
    string GenerationSha256,
    string ProducerVersion,
    string Culture);

/// <summary>
/// Validates a bounded durable data artifact and creates independent owned copies of its bytes.
/// </summary>
internal sealed class JointInterfaceCrashSeed
{
    /// <summary>
    /// Gets the detached source provenance validated when the artifact was opened.
    /// </summary>
    public JointInterfaceCrashSeedMetadata Metadata => throw new NotSupportedException();

    /// <summary>
    /// Gets the uppercase digest identifying the producing and consuming source contracts.
    /// </summary>
    public string ContractFingerprint => throw new NotSupportedException();

    /// <summary>
    /// Gets the number of validated durable payload files.
    /// </summary>
    public int FileCount => throw new NotSupportedException();

    /// <summary>
    /// Gets the total number of validated payload bytes.
    /// </summary>
    public long PayloadBytes => throw new NotSupportedException();

    /// <summary>
    /// Exports a checked closed namespace snapshot into a new artifact directory.
    /// </summary>
    /// <param name="artifactPath">
    /// A nonexistent artifact directory beneath an existing parent directory.
    /// </param>
    /// <param name="metadata">
    /// Detached source provenance for the completed turn 42 snapshot.
    /// </param>
    /// <param name="contractFingerprint">
    /// The uppercase SHA256 contract fingerprint bound to the snapshot.
    /// </param>
    /// <param name="files">
    /// Exact durable file bytes keyed by portable relative paths, including generation data.
    /// </param>
    /// <param name="directories">
    /// All payload directories, including empty directories, as portable relative paths.
    /// </param>
    /// <returns>
    /// A task that completes when the new artifact has been written and validated.
    /// </returns>
    public static Task ExportAsync(string artifactPath, JointInterfaceCrashSeedMetadata metadata,
        string contractFingerprint, IReadOnlyDictionary<string, byte[]> files,
        IReadOnlyCollection<string> directories) => throw new NotSupportedException();

    /// <summary>
    /// Validates an artifact and retains a private defensive snapshot of all durable bytes.
    /// </summary>
    /// <param name="artifactPath">
    /// The existing artifact directory containing manifest.json and its exact payload inventory.
    /// </param>
    /// <param name="expectedContractFingerprint">
    /// The uppercase SHA256 contract fingerprint required by the current consumer.
    /// </param>
    /// <returns>
    /// An immutable validated data snapshot independent of later artifact changes.
    /// </returns>
    public static JointInterfaceCrashSeed Open(string artifactPath, string expectedContractFingerprint) =>
        throw new NotSupportedException();

    /// <summary>
    /// Writes the validated snapshot into a new owned root without changing the artifact.
    /// </summary>
    /// <param name="emptyOwnedRoot">
    /// A nonexistent destination beneath an existing parent and outside the artifact directory.
    /// </param>
    /// <returns>
    /// A task returning the detached source provenance after all exact bytes and directories are copied.
    /// </returns>
    public Task<JointInterfaceCrashSeedMetadata> MaterializeAsync(string emptyOwnedRoot) =>
        throw new NotSupportedException();
}
