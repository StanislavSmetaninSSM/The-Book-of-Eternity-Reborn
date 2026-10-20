using System.Security.Cryptography;
using System.Text.Json;
using BookOfEternityClient.Core;

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
    private const int MaximumFiles = 8192;
    private const long MaximumPayloadBytes = 256L * 1024 * 1024;
    private const int MaximumManifestBytes = 4 * 1024 * 1024;
    private const string GenerationPath = ".boe_runtime/session-generation/current.json";
    private readonly string _artifactPath;
    private readonly Dictionary<string, byte[]> _files;
    private readonly string[] _directories;

    /// <summary>
    /// Retains an already checked snapshot whose byte arrays and directory names are privately owned.
    /// </summary>
    /// <param name="artifactPath">
    /// The normalized source artifact directory, excluded from future import destinations.
    /// </param>
    /// <param name="metadata">
    /// Validated detached source provenance.
    /// </param>
    /// <param name="contractFingerprint">
    /// The validated contract digest required by the consumer.
    /// </param>
    /// <param name="files">
    /// Privately owned checked file bytes, never exposed to callers.
    /// </param>
    /// <param name="directories">
    /// Privately owned complete directory inventory.
    /// </param>
    private JointInterfaceCrashSeed(string artifactPath, JointInterfaceCrashSeedMetadata metadata,
        string contractFingerprint, Dictionary<string, byte[]> files, string[] directories)
    {
        _artifactPath = artifactPath;
        Metadata = metadata;
        ContractFingerprint = contractFingerprint;
        _files = files;
        _directories = directories;
        PayloadBytes = files.Values.Sum(bytes => (long)bytes.Length);
    }

    /// <summary>
    /// Gets the detached source provenance validated when the artifact was opened.
    /// </summary>
    public JointInterfaceCrashSeedMetadata Metadata { get; }

    /// <summary>
    /// Gets the uppercase digest identifying the producing and consuming source contracts.
    /// </summary>
    public string ContractFingerprint { get; }

    /// <summary>
    /// Gets the number of validated durable payload files.
    /// </summary>
    public int FileCount => _files.Count;

    /// <summary>
    /// Gets the total number of validated payload bytes.
    /// </summary>
    public long PayloadBytes { get; }

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
    public static async Task ExportAsync(string artifactPath, JointInterfaceCrashSeedMetadata metadata,
        string contractFingerprint, IReadOnlyDictionary<string, byte[]> files,
        IReadOnlyCollection<string> directories)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(directories);
        ValidateMetadata(metadata);
        ValidateDigest(contractFingerprint);
        if (files.Count > MaximumFiles || directories.Count > MaximumManifestBytes / 3)
            throw new InvalidDataException("The seed inventory exceeds its bounded format.");

        var directorySnapshot = directories.ToArray();
        ValidateNames(files.Keys, directorySnapshot);
        var snapshot = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        long payloadBytes = 0;
        foreach (var file in files)
        {
            if (file.Value is null || (payloadBytes += file.Value.LongLength) > MaximumPayloadBytes)
                throw new InvalidDataException("The seed payload exceeds its bounded format or contains null data.");
            snapshot.Add(file.Key, file.Value.ToArray());
        }
        ValidateGeneration(metadata, snapshot);
        var manifestBytes = JsonSerializer.SerializeToUtf8Bytes(new
        {
            version = 1,
            contractFingerprint,
            metadata = new
            {
                sessionId = metadata.SessionId,
                requestId = metadata.RequestId,
                snapshotToken = metadata.SnapshotToken,
                turnNumber = metadata.TurnNumber,
                opportunityRef = metadata.OpportunityRef,
                generationSha256 = metadata.GenerationSha256,
                producerVersion = metadata.ProducerVersion,
                culture = metadata.Culture
            },
            directories = directorySnapshot.OrderBy(path => path, StringComparer.Ordinal).ToArray(),
            files = snapshot.OrderBy(file => file.Key, StringComparer.Ordinal).Select(file => new
            {
                path = file.Key,
                length = file.Value.LongLength,
                sha256 = Digest(file.Value)
            }).ToArray()
        });
        if (manifestBytes.Length > MaximumManifestBytes)
            throw new InvalidDataException("The seed manifest exceeds its bounded format.");

        var root = NormalizeRoot(artifactPath);
        await WriteNewRootAsync(root, async scope =>
        {
            var payload = scope.EnsureDirectory(Path.Combine(root, "payload"));
            await WritePayloadAsync(scope, payload, snapshot, directorySnapshot);
            await WriteFileAsync(scope, Path.Combine(root, "manifest.json"), manifestBytes);
        });
    }

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
    public static JointInterfaceCrashSeed Open(string artifactPath, string expectedContractFingerprint)
    {
        ValidateDigest(expectedContractFingerprint);
        var root = NormalizeRoot(artifactPath);
        var scope = new TrustedLocalFileScope([root]);
        scope.ValidateDirectory(root, allowMissing: false);
        var topEntries = Directory.GetFileSystemEntries(root)
            .Select(path => Path.GetFileName(path)).OrderBy(path => path, StringComparer.Ordinal).ToArray();
        if (!topEntries.SequenceEqual(new[] { "manifest.json", "payload" }, StringComparer.Ordinal))
            throw new InvalidDataException("The seed artifact must contain exactly manifest.json and payload.");

        var manifestBytes = ReadBoundedFile(scope, Path.Combine(root, "manifest.json"), MaximumManifestBytes);
        JointInterfaceCrashSeedMetadata metadata;
        string fingerprint;
        string[] directories;
        Dictionary<string, (long Length, string Sha256)> entries;
        try
        {
            using var document = JsonDocument.Parse(manifestBytes);
            var manifest = ReadObject(document.RootElement,
                "version", "contractFingerprint", "metadata", "directories", "files");
            if (manifest["version"].ValueKind != JsonValueKind.Number ||
                !manifest["version"].TryGetInt32(out var version) || version != 1)
                throw new InvalidDataException("The seed manifest version is unsupported.");
            fingerprint = ReadString(manifest["contractFingerprint"]);
            ValidateDigest(fingerprint);
            if (!string.Equals(fingerprint, expectedContractFingerprint, StringComparison.Ordinal))
                throw new InvalidDataException("The seed contract fingerprint is stale.");
            metadata = ReadMetadata(manifest["metadata"]);
            if (manifest["directories"].ValueKind != JsonValueKind.Array || manifest["files"].ValueKind != JsonValueKind.Array)
                throw new InvalidDataException("The seed inventory must contain arrays.");
            directories = manifest["directories"].EnumerateArray().Select(ReadString).ToArray();
            entries = new Dictionary<string, (long Length, string Sha256)>(StringComparer.Ordinal);
            long payloadBytes = 0;
            foreach (var element in manifest["files"].EnumerateArray())
            {
                if (entries.Count == MaximumFiles)
                    throw new InvalidDataException("The seed contains too many payload files.");
                var file = ReadObject(element, "path", "length", "sha256");
                var path = ReadString(file["path"]);
                var sha256 = ReadString(file["sha256"]);
                ValidateDigest(sha256);
                if (file["length"].ValueKind != JsonValueKind.Number ||
                    !file["length"].TryGetInt64(out var length) || length < 0 ||
                    length > MaximumPayloadBytes - payloadBytes)
                    throw new InvalidDataException("The seed payload lengths exceed the bounded format.");
                payloadBytes += length;
                if (!entries.TryAdd(path, (length, sha256)))
                    throw new InvalidDataException("The seed contains duplicate payload paths.");
            }
            ValidateNames(entries.Keys, directories);
        }
        catch (JsonException error)
        {
            throw new InvalidDataException("The seed manifest is not valid JSON.", error);
        }

        var payloadRoot = scope.ValidateDirectory(Path.Combine(root, "payload"), allowMissing: false);
        ValidatePhysicalInventory(scope, payloadRoot, entries.Keys, directories);
        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            var bytes = ReadBoundedFile(scope, LocalPath(payloadRoot, entry.Key), entry.Value.Length);
            if (bytes.LongLength != entry.Value.Length || Digest(bytes) != entry.Value.Sha256)
                throw new InvalidDataException("A seed payload length or digest does not match its manifest.");
            files.Add(entry.Key, bytes);
        }
        ValidateGeneration(metadata, files);
        return new JointInterfaceCrashSeed(root, metadata, fingerprint, files, directories);
    }

    /// <summary>
    /// Writes the validated snapshot into a new owned root without changing the artifact.
    /// </summary>
    /// <param name="emptyOwnedRoot">
    /// A nonexistent destination beneath an existing parent and outside the artifact directory.
    /// </param>
    /// <returns>
    /// A task returning the detached source provenance after all exact bytes and directories are copied.
    /// </returns>
    public async Task<JointInterfaceCrashSeedMetadata> MaterializeAsync(string emptyOwnedRoot)
    {
        var root = NormalizeRoot(emptyOwnedRoot);
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (string.Equals(root, _artifactPath, comparison) ||
            root.StartsWith(_artifactPath + Path.DirectorySeparatorChar, comparison))
            throw new IOException("A seed import cannot be created inside its source artifact.");
        await WriteNewRootAsync(root, scope => WritePayloadAsync(scope, root, _files, _directories));
        return Metadata;
    }

    /// <summary>
    /// Reads a closed JSON object without permitting duplicate, unknown or missing property names.
    /// </summary>
    /// <param name="element">
    /// The JSON element required to be an object.
    /// </param>
    /// <param name="names">
    /// The exact case-sensitive property names comprising the complete object schema.
    /// </param>
    /// <returns>
    /// Every required property value indexed by its exact schema name.
    /// </returns>
    private static Dictionary<string, JsonElement> ReadObject(JsonElement element, params string[] names)
    {
        if (element.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("A seed manifest member must be an object.");
        var result = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            if (!names.Contains(property.Name, StringComparer.Ordinal) || !result.TryAdd(property.Name, property.Value))
                throw new InvalidDataException("A seed manifest object contains an unknown or duplicate member.");
        }
        if (result.Count != names.Length)
            throw new InvalidDataException("A seed manifest object is incomplete.");
        return result;
    }

    /// <summary>
    /// Reads an exact JSON string without converting null, numbers or other kinds.
    /// </summary>
    /// <param name="element">
    /// The value required to have the JSON string kind.
    /// </param>
    /// <returns>
    /// The non-null string value, which may still require contract-specific validation.
    /// </returns>
    private static string ReadString(JsonElement element) => element.ValueKind == JsonValueKind.String
        ? element.GetString()! : throw new InvalidDataException("A seed manifest member must be a string.");

    /// <summary>
    /// Reads the closed source provenance schema and validates its source checkpoint contract.
    /// </summary>
    /// <param name="element">
    /// The metadata object containing exactly the eight provenance members.
    /// </param>
    /// <returns>
    /// Validated detached provenance for completed source turn 42.
    /// </returns>
    private static JointInterfaceCrashSeedMetadata ReadMetadata(JsonElement element)
    {
        var properties = ReadObject(element, "sessionId", "requestId", "snapshotToken", "turnNumber",
            "opportunityRef", "generationSha256", "producerVersion", "culture");
        if (properties["turnNumber"].ValueKind != JsonValueKind.Number ||
            !properties["turnNumber"].TryGetInt32(out var turn))
            throw new InvalidDataException("The seed source turn must be an integer.");
        var metadata = new JointInterfaceCrashSeedMetadata(ReadString(properties["sessionId"]),
            ReadString(properties["requestId"]), ReadString(properties["snapshotToken"]), turn,
            ReadString(properties["opportunityRef"]), ReadString(properties["generationSha256"]),
            ReadString(properties["producerVersion"]), ReadString(properties["culture"]));
        ValidateMetadata(metadata);
        return metadata;
    }

    /// <summary>
    /// Requires detached source turn 42 provenance and its exact generation culture.
    /// </summary>
    /// <param name="metadata">
    /// The non-null metadata whose tuple, informational version and digest must be populated.
    /// </param>
    private static void ValidateMetadata(JointInterfaceCrashSeedMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        if (metadata.TurnNumber != 42 || metadata.Culture != "en-GB" ||
            new[] { metadata.SessionId, metadata.RequestId, metadata.SnapshotToken, metadata.OpportunityRef, metadata.ProducerVersion }
                .Any(string.IsNullOrWhiteSpace))
            throw new InvalidDataException("The seed must describe completed source turn 42 in en-GB with a nonblank tuple and producer version.");
        ValidateDigest(metadata.GenerationSha256);
    }

    /// <summary>
    /// Requires the unique uppercase hexadecimal spelling of a SHA256 digest.
    /// </summary>
    /// <param name="digest">
    /// The non-null 64-character uppercase hexadecimal digest.
    /// </param>
    private static void ValidateDigest(string digest)
    {
        if (digest is null || digest.Length != 64 || digest.Any(character =>
                character is not (>= '0' and <= '9') and not (>= 'A' and <= 'F')))
            throw new InvalidDataException("A seed digest must contain exactly 64 uppercase hexadecimal characters.");
    }

    /// <summary>
    /// Validates complete portable names, ancestor directories and the single permitted runtime file.
    /// </summary>
    /// <param name="files">
    /// All relative payload file names, including the required generation data file.
    /// </param>
    /// <param name="directories">
    /// Every relative payload directory, with no duplicates or case aliases.
    /// </param>
    private static void ValidateNames(IEnumerable<string> files, string[] directories)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var directorySet = new HashSet<string>(StringComparer.Ordinal);
        foreach (var directory in directories)
        {
            ValidateRelativePath(directory, directory: true);
            if (!names.Add(directory))
                throw new InvalidDataException("The seed contains duplicate or case-conflicting directory paths.");
            directorySet.Add(directory);
        }
        if (!directorySet.IsSupersetOf(new[] { "game_session", ".boe_runtime", ".boe_runtime/session-generation" }))
            throw new InvalidDataException("The seed is missing a required durable namespace directory.");
        var fileNames = files.ToArray();
        if (!fileNames.Contains(GenerationPath, StringComparer.Ordinal))
            throw new InvalidDataException("The seed is missing generation data.");
        foreach (var file in fileNames)
        {
            ValidateRelativePath(file, directory: false);
            if (!names.Add(file))
                throw new InvalidDataException("The seed contains duplicate, case-conflicting or file/directory-conflicting paths.");
        }
        foreach (var name in fileNames.Concat(directories))
        {
            var slash = name.LastIndexOf('/');
            if (slash >= 0 && !directorySet.Contains(name[..slash]))
                throw new InvalidDataException("The seed inventory omits an ancestor directory.");
        }
    }

    /// <summary>
    /// Restricts manifest names to ordinary portable components within the durable namespace.
    /// </summary>
    /// <param name="path">
    /// A nonempty slash-separated relative name with no device names or ambiguous components.
    /// </param>
    /// <param name="directory">
    /// Whether the name denotes a directory rather than a payload file.
    /// </param>
    private static void ValidateRelativePath(string path, bool directory)
    {
        if (string.IsNullOrEmpty(path) || path.Contains('\\') || path.StartsWith('/') || path.EndsWith('/'))
            throw new InvalidDataException("Seed paths must be ordinary slash-separated relative names.");
        foreach (var component in path.Split('/'))
        {
            if (component.Length is 0 or > 255 || component is "." or ".." ||
                component.EndsWith('.') || component.EndsWith(' ') || component.Any(character =>
                    char.IsControl(character) || character is ':' or '*' or '?' or '"' or '<' or '>' or '|'))
                throw new InvalidDataException("A seed path contains an ambiguous or nonportable component.");
            var stem = component.Split('.')[0].ToUpperInvariant();
            if (stem is "CON" or "PRN" or "AUX" or "NUL" or "CONIN$" or "CONOUT$" ||
                (stem.Length == 4 && "123456789¹²³".Contains(stem[3]) &&
                 (stem.StartsWith("COM", StringComparison.Ordinal) || stem.StartsWith("LPT", StringComparison.Ordinal))))
                throw new InvalidDataException("A seed path cannot name a Windows device.");
        }
        var allowed = directory
            ? path is "game_session" or ".boe_runtime" or ".boe_runtime/session-generation" || path.StartsWith("game_session/", StringComparison.Ordinal)
            : path == GenerationPath || path.StartsWith("game_session/", StringComparison.Ordinal);
        if (!allowed)
            throw new InvalidDataException("A seed path is outside the permitted durable namespace.");
    }

    /// <summary>
    /// Binds the mandatory generation data file to the detached provenance digest.
    /// </summary>
    /// <param name="metadata">
    /// Validated provenance containing the expected generation data digest.
    /// </param>
    /// <param name="files">
    /// The complete private payload snapshot containing the exact generation bytes.
    /// </param>
    private static void ValidateGeneration(JointInterfaceCrashSeedMetadata metadata, IReadOnlyDictionary<string, byte[]> files)
    {
        if (!files.TryGetValue(GenerationPath, out var generation) || Digest(generation) != metadata.GenerationSha256)
            throw new InvalidDataException("The seed generation data does not match its source provenance.");
    }

    /// <summary>
    /// Matches every physical payload entry against the exact manifest inventory before reading payload data.
    /// </summary>
    /// <param name="scope">
    /// The trusted local artifact scope, which rejects links and nonordinary entries.
    /// </param>
    /// <param name="root">
    /// The existing payload directory beneath the artifact.
    /// </param>
    /// <param name="files">
    /// Exact case-sensitive declared relative file names.
    /// </param>
    /// <param name="directories">
    /// Exact case-sensitive declared relative directory names, including empty directories.
    /// </param>
    private static void ValidatePhysicalInventory(TrustedLocalFileScope scope, string root,
        IEnumerable<string> files, string[] directories)
    {
        var remainingFiles = new HashSet<string>(files, StringComparer.Ordinal);
        var remainingDirectories = new HashSet<string>(directories, StringComparer.Ordinal);
        var pending = new Queue<string>();
        pending.Enqueue(root);
        while (pending.TryDequeue(out var directory))
        {
            scope.ValidateDirectory(directory, allowMissing: false);
            foreach (var path in Directory.EnumerateFileSystemEntries(directory))
            {
                var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
                var observation = scope.ObserveNamespace(path);
                if (observation.Kind == TrustedLocalNamespaceKind.Directory)
                {
                    if (!remainingDirectories.Remove(relative))
                        throw new InvalidDataException("The physical seed contains an undeclared directory.");
                    pending.Enqueue(scope.ValidateDirectory(path, allowMissing: false));
                }
                else
                {
                    scope.ValidateFile(path, allowMissing: false);
                    if (!remainingFiles.Remove(relative))
                        throw new InvalidDataException("The physical seed contains an undeclared file.");
                }
            }
        }
        if (remainingFiles.Count != 0 || remainingDirectories.Count != 0)
            throw new InvalidDataException("The physical seed inventory is incomplete.");
    }

    /// <summary>
    /// Reads an ordinary file through a single bounded stream without following links.
    /// </summary>
    /// <param name="scope">
    /// The trusted scope granting the file and validating its ancestors.
    /// </param>
    /// <param name="path">
    /// The absolute ordinary file name to read.
    /// </param>
    /// <param name="maximumBytes">
    /// The largest accepted stream length before allocating its private snapshot.
    /// </param>
    /// <returns>
    /// Privately owned exact bytes from the checked stream.
    /// </returns>
    private static byte[] ReadBoundedFile(TrustedLocalFileScope scope, string path, long maximumBytes)
    {
        using var stream = new FileStream(scope.ValidateFile(path, allowMissing: false), FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > maximumBytes)
            throw new InvalidDataException("A seed file exceeds its declared or bounded length.");
        var bytes = new byte[checked((int)stream.Length)];
        stream.ReadExactly(bytes);
        if (stream.ReadByte() != -1)
            throw new InvalidDataException("A seed file changed length while being read.");
        return bytes;
    }

    /// <summary>
    /// Writes one validated namespace into a new owned root and cleans only that root after failure.
    /// </summary>
    /// <param name="root">
    /// A normalized nonexistent root whose parent already exists.
    /// </param>
    /// <param name="write">
    /// The bounded operation that writes only within the freshly created root.
    /// </param>
    /// <returns>
    /// A task completing after writing, or propagating the original failure with any cleanup failure attached.
    /// </returns>
    internal static async Task WriteNewRootAsync(string root, Func<TrustedLocalFileScope, Task> write)
    {
        if (Directory.Exists(root) || File.Exists(root))
            throw new IOException("The seed destination already exists.");
        var scope = new TrustedLocalFileScope([root]);
        if (scope.ObserveNamespace(root).Kind != TrustedLocalNamespaceKind.Missing)
            throw new IOException("The seed destination already exists.");
        var created = false;
        try
        {
            Directory.CreateDirectory(root);
            created = true;
            scope.ValidateDirectory(root, allowMissing: false);
            await write(scope);
        }
        catch (Exception error)
        {
            if (created)
            {
                try { scope.DeleteOwnedTree(root); }
                catch (Exception cleanupError) { error.Data["JointInterfaceCrashSeedCleanupFailure"] = cleanupError; }
            }
            throw;
        }
    }

    /// <summary>
    /// Recreates all declared directories and writes private file bytes into the owned namespace.
    /// </summary>
    /// <param name="scope">
    /// The trusted scope granting the newly created destination root.
    /// </param>
    /// <param name="root">
    /// The physical root for the payload namespace.
    /// </param>
    /// <param name="files">
    /// Validated private file bytes keyed by portable relative names.
    /// </param>
    /// <param name="directories">
    /// Complete validated directory names, including empty directories.
    /// </param>
    /// <returns>
    /// A task completing after every exact byte array has been independently written.
    /// </returns>
    private static async Task WritePayloadAsync(TrustedLocalFileScope scope, string root,
        IReadOnlyDictionary<string, byte[]> files, string[] directories)
    {
        foreach (var directory in directories.OrderBy(path => path, StringComparer.Ordinal))
            scope.EnsureDirectory(LocalPath(root, directory));
        foreach (var file in files.OrderBy(file => file.Key, StringComparer.Ordinal))
            await WriteFileAsync(scope, LocalPath(root, file.Key), file.Value);
    }

    /// <summary>
    /// Creates one new ordinary file without replacing any existing entry.
    /// </summary>
    /// <param name="scope">
    /// The trusted scope validating the destination and its existing directory ancestors.
    /// </param>
    /// <param name="path">
    /// The absolute destination file name beneath the newly owned root.
    /// </param>
    /// <param name="bytes">
    /// The private immutable bytes to copy into the independent physical file.
    /// </param>
    /// <returns>
    /// A task completing after the bytes have been written and the stream disposed.
    /// </returns>
    private static async Task WriteFileAsync(TrustedLocalFileScope scope, string path, byte[] bytes)
    {
        await using var stream = new FileStream(scope.ValidateFile(path), FileMode.CreateNew, FileAccess.Write, FileShare.None,
            bufferSize: 81920, options: FileOptions.Asynchronous);
        await stream.WriteAsync(bytes);
    }

    /// <summary>
    /// Normalizes an explicitly supplied absolute artifact or owned destination root.
    /// </summary>
    /// <param name="path">
    /// A nonblank fully qualified ordinary directory path.
    /// </param>
    /// <returns>
    /// The absolute path with its optional trailing directory separator removed.
    /// </returns>
    private static string NormalizeRoot(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
            throw new InvalidDataException("A seed artifact or owned root path must be absolute.");
        if (OperatingSystem.IsWindows()) TrustedLocalFileScope.ValidateWindowsPathSpelling(path);
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    }

    /// <summary>
    /// Combines a validated portable relative name with its owning physical root.
    /// </summary>
    /// <param name="root">
    /// The normalized absolute artifact payload or owned import root.
    /// </param>
    /// <param name="relative">
    /// A previously validated slash-separated relative namespace member.
    /// </param>
    /// <returns>
    /// The absolute path using the host's directory separator.
    /// </returns>
    private static string LocalPath(string root, string relative) => Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));

    /// <summary>
    /// Computes the canonical uppercase SHA256 digest for exact payload bytes.
    /// </summary>
    /// <param name="bytes">
    /// The byte array to hash without text decoding or normalization.
    /// </param>
    /// <returns>
    /// The 64-character uppercase hexadecimal digest.
    /// </returns>
    private static string Digest(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
}
