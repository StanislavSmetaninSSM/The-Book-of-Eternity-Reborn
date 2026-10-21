using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Selects one exact immutable source42 artifact without transporting game authority or live owners.
/// </summary>
/// <param name="ArtifactPath">
/// The absolute ordinary artifact directory selected by the parent.
/// </param>
/// <param name="ManifestSha256">
/// The uppercase SHA256 digest of the exact selected manifest bytes.
/// </param>
/// <param name="Usage">
/// An explicit closed fixture usage, or <see langword="null"/> for the original two-field cut0 selection.
/// The default value is omitted from serialized legacy metadata.
/// </param>
internal sealed record JointInterfaceSeedCrashInput(string ArtifactPath, string ManifestSha256,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Usage = null)
{
    /// <summary>
    /// Identifies the explicit ordinary Windows fixture usage for the original five cuts.
    /// </summary>
    internal const string OrdinaryWindowsFiveCutsUsage = "ordinary-windows-five-cuts-v1";

    /// <summary>
    /// Validates a supported ordinary Windows artifact selection and binds its exact manifest to current source contracts.
    /// </summary>
    /// <param name="artifactPath">
    /// The absolute existing artifact directory; missing or invalid artifacts are refused without generation.
    /// </param>
    /// <param name="repoRoot">
    /// The repository whose current broad source fingerprint must match the artifact.
    /// </param>
    /// <param name="windows">
    /// Whether the requested crash consumer runs on Windows.
    /// </param>
    /// <param name="mode">
    /// The ordinary crash mode, with diagnostics disabled and no integrity fault.
    /// </param>
    /// <param name="usage">
    /// An explicit fixture usage; <see langword="null"/> preserves the legacy cut0-only contract.
    /// </param>
    /// <param name="fixtureAcquisitionOnly">
    /// Whether separate fixture acquisition was requested; the default is <see langword="false"/>.
    /// </param>
    /// <returns>
    /// A detached selection containing the validated artifact path and exact manifest digest.
    /// </returns>
    internal static JointInterfaceSeedCrashInput Bind(string artifactPath, string repoRoot, bool windows,
        JointInterfaceCrashMode mode, string? usage = null, bool fixtureAcquisitionOnly = false)
    {
        ValidateMode(windows, mode, usage, fixtureAcquisitionOnly);
        var path = NormalizeArtifactPath(artifactPath);
        var selection = new JointInterfaceSeedCrashInput(path, ReadManifestDigest(path), usage);
        _ = selection.Open(repoRoot);
        return selection;
    }

    /// <summary>
    /// Reads an optional closed seed selection and refuses malformed or forbidden explicit opt-ins.
    /// </summary>
    /// <param name="parent">
    /// The parent metadata object whose optional Source42Seed property contains the exact selection.
    /// </param>
    /// <param name="windows">
    /// Whether the requested crash consumer runs on Windows.
    /// </param>
    /// <param name="mode">
    /// The ordinary Windows crash mode. Absent usage permits only cut0; explicit five-cut usage permits cuts0 through4.
    /// </param>
    /// <returns>
    /// The validated detached selection, or <see langword="null"/> only when Source42Seed is absent.
    /// </returns>
    internal static JointInterfaceSeedCrashInput? Read(JsonObject parent, bool windows, JointInterfaceCrashMode mode)
    {
        ArgumentNullException.ThrowIfNull(parent);
        if (!parent.TryGetPropertyValue("Source42Seed", out var node))
            return null;
        try
        {
            string? usage = null;
            if (node is JsonObject explicitSelection && explicitSelection.TryGetPropertyValue("Usage", out var usageNode))
            {
                if (usageNode is not JsonValue usageValue || !usageValue.TryGetValue<string>(out usage) || usage is null)
                    throw new InvalidDataException("Source42Seed Usage must be an explicit supported string.");
            }
            var acquisition = false;
            if (usage is not null && parent.TryGetPropertyValue("FixtureAcquisitionOnly", out var acquisitionNode) &&
                (acquisitionNode is not JsonValue acquisitionValue || !acquisitionValue.TryGetValue<bool>(out acquisition)))
                throw new InvalidDataException("FixtureAcquisitionOnly must be a Boolean when present.");
            ValidateMode(windows, mode, usage, acquisition);
            if (node is not JsonObject selection || selection.Count != (usage is null ? 2 : 3) ||
                selection.Any(property => property.Key is not ("ArtifactPath" or "ManifestSha256" or "Usage")) ||
                !selection.TryGetPropertyValue("ArtifactPath", out var pathNode) ||
                !selection.TryGetPropertyValue("ManifestSha256", out var digestNode) ||
                pathNode is not JsonValue pathValue || !pathValue.TryGetValue<string>(out var path) || path is null ||
                digestNode is not JsonValue digestValue || !digestValue.TryGetValue<string>(out var digest) || digest is null)
                throw new InvalidDataException(usage is null
                    ? "Source42Seed must contain exactly ArtifactPath and ManifestSha256 string members."
                    : "Source42Seed must contain exactly ArtifactPath, ManifestSha256 and Usage string members.");
            ValidateDigest(digest);
            return new JointInterfaceSeedCrashInput(NormalizeArtifactPath(path), digest, usage);
        }
        catch (ArgumentException error)
        {
            throw new InvalidDataException("Source42Seed contains ambiguous or invalid object members.", error);
        }
    }

    /// <summary>
    /// Opens the selected artifact only if its exact manifest and current broad source contracts still match.
    /// </summary>
    /// <param name="repoRoot">
    /// The repository whose producing and consuming source closure must match the selected artifact.
    /// </param>
    /// <returns>
    /// A checked immutable codec snapshot ready for independent materialization into a new owned root.
    /// </returns>
    internal JointInterfaceCrashSeed Open(string repoRoot)
    {
        ValidateUsage(Usage);
        ValidateDigest(ManifestSha256);
        var path = NormalizeArtifactPath(ArtifactPath);
        if (ReadManifestDigest(path) != ManifestSha256)
            throw new InvalidDataException("The selected source42 manifest has changed since parent binding.");
        var fingerprint = JointInterfaceSeedContractFingerprint.Compute(repoRoot);
        try
        {
            var seed = JointInterfaceCrashSeed.Open(path, fingerprint);
            if (ReadManifestDigest(path) != ManifestSha256)
                throw new InvalidDataException("The selected source42 manifest changed while its payload was being opened.");
            return seed;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            throw new InvalidDataException("The selected source42 artifact cannot be opened as checked ordinary data.", error);
        }
    }

    /// <summary>
    /// Rejects every explicit usage outside the closed ordinary Windows five-cut fixture contract.
    /// </summary>
    /// <param name="usage">
    /// The exact explicit usage spelling, or <see langword="null"/> for the original legacy selection.
    /// </param>
    private static void ValidateUsage(string? usage)
    {
        if (usage is not null && usage != OrdinaryWindowsFiveCutsUsage)
            throw new InvalidDataException("Source42Seed Usage is unknown or unsupported.");
    }

    /// <summary>
    /// Refuses unsupported seed opt-ins before any artifact or repository access.
    /// </summary>
    /// <param name="windows">
    /// Whether the requested consumer runs on Windows.
    /// </param>
    /// <param name="mode">
    /// The required ordinary mode with no diagnostic or integrity fault.
    /// </param>
    /// <param name="usage">
    /// The explicit five-cut usage, or <see langword="null"/> for the unchanged legacy cut0 contract.
    /// </param>
    /// <param name="fixtureAcquisitionOnly">
    /// Whether acquisition was requested; explicit five-cut usage cannot be combined with it.
    /// </param>
    private static void ValidateMode(bool windows, JointInterfaceCrashMode mode,
        string? usage = null, bool fixtureAcquisitionOnly = false)
    {
        ValidateUsage(usage);
        if (usage is null)
        {
            if (!windows || mode is not { Cut: 0, Diagnostic: false, Fault: null })
                throw new InvalidDataException("Source42Seed is permitted only for ordinary Windows cut0.");
            return;
        }
        if (!windows || fixtureAcquisitionOnly || mode is not { Cut: >= 0 and <= 4, Diagnostic: false, Fault: null })
            throw new InvalidDataException("Explicit five-cut Source42Seed usage requires ordinary Windows cuts0 through4 without fixture acquisition.");
    }

    /// <summary>
    /// Validates an explicit ordinary absolute artifact path without accessing its physical namespace.
    /// </summary>
    /// <param name="path">
    /// The nonblank fully qualified directory name with no traversal or ambiguous Windows components.
    /// </param>
    /// <returns>
    /// The normalized absolute directory path without a trailing separator.
    /// </returns>
    private static string NormalizeArtifactPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path) ||
            path.Split(['\\', '/']).Any(component => component is "." or ".."))
            throw new InvalidDataException("The selected source42 artifact path must be absolute and contain no traversal.");
        try
        {
            if (OperatingSystem.IsWindows())
                path = TrustedLocalFileScope.NormalizeWindowsPathSpelling(path);
            var normalized = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
            if (Path.GetDirectoryName(normalized) is null)
                throw new InvalidDataException("A filesystem root cannot be selected as a source42 artifact.");
            return normalized;
        }
        catch (Exception error) when (error is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new InvalidDataException("The selected source42 artifact path is not an ordinary directory name.", error);
        }
    }

    /// <summary>
    /// Requires the unambiguous uppercase SHA256 spelling used by the parent selection format.
    /// </summary>
    /// <param name="digest">
    /// The non-null 64-character uppercase hexadecimal manifest digest.
    /// </param>
    private static void ValidateDigest(string digest)
    {
        if (digest is null || digest.Length != 64 || digest.Any(character =>
                character is not (>= '0' and <= '9') and not (>= 'A' and <= 'F')))
            throw new InvalidDataException("The source42 manifest digest must contain exactly 64 uppercase hexadecimal characters.");
    }

    /// <summary>
    /// Reads and hashes one bounded ordinary manifest without writing to the artifact or following links.
    /// </summary>
    /// <param name="artifactPath">
    /// The normalized absolute artifact directory with existing ordinary ancestors.
    /// </param>
    /// <returns>
    /// The uppercase SHA256 digest of the exact manifest bytes observed by one read-only stream.
    /// </returns>
    private static string ReadManifestDigest(string artifactPath)
    {
        try
        {
            var scope = new TrustedLocalFileScope([artifactPath]);
            using var stream = new FileStream(scope.ValidateFile(Path.Combine(artifactPath, "manifest.json"), allowMissing: false),
                FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length > 4 * 1024 * 1024)
                throw new InvalidDataException("The selected source42 manifest exceeds its four MiB format bound.");
            var bytes = new byte[checked((int)stream.Length)];
            stream.ReadExactly(bytes);
            if (stream.ReadByte() != -1)
                throw new InvalidDataException("The selected source42 manifest changed length during its read.");
            return Convert.ToHexString(SHA256.HashData(bytes));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            throw new InvalidDataException("The selected source42 manifest is missing or cannot be read as ordinary data.", error);
        }
    }
}
