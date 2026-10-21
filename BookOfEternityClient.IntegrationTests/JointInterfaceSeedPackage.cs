using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Defines the closed fixture ZIP transport boundary independently of game authority and process ownership.
/// </summary>
internal static class JointInterfaceSeedPackage
{
    /// <summary>
    /// Validates the exact closed carrier before creating a fresh artifact with every explicitly transported directory.
    /// The resulting physical namespace must also pass the original seed codec before this method returns.
    /// </summary>
    /// <param name="packageRoot">
    /// The required absolute ordinary directory containing only package.json and source42.zip.
    /// A <see langword="null"/> or blank value is refused before package IO.
    /// </param>
    /// <param name="destination">
    /// The required absolute fresh artifact directory inside the caller's actual child-owning container, outside its game tmp subtree.
    /// Ownership and cleanup remain entirely with that caller; a <see langword="null"/> or blank value is refused.
    /// </param>
    internal static void Hydrate(string packageRoot, string destination)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(packageRoot) || string.IsNullOrWhiteSpace(destination) ||
                !Path.IsPathFullyQualified(packageRoot) || !Path.IsPathFullyQualified(destination))
                throw new InvalidDataException("The fixture package and destination paths must be explicit absolute directory names.");
            if (OperatingSystem.IsWindows())
            {
                TrustedLocalFileScope.ValidateWindowsPathSpelling(packageRoot);
                TrustedLocalFileScope.ValidateWindowsPathSpelling(destination);
            }
            var package = Path.TrimEndingDirectorySeparator(Path.GetFullPath(packageRoot));
            var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(destination));
            var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (string.Equals(root, package, comparison) || root.StartsWith(package + Path.DirectorySeparatorChar, comparison) ||
                package.StartsWith(root + Path.DirectorySeparatorChar, comparison))
                throw new InvalidDataException("The hydration destination must be separate from the immutable package.");
            var parent = Path.GetDirectoryName(root)
                ?? throw new InvalidDataException("The hydration destination needs an existing owned parent.");
            var destinationScope = new TrustedLocalFileScope([parent]);
            destinationScope.ValidateDirectory(parent, allowMissing: false);
            destinationScope.ValidateDirectory(root, allowMissing: true);
            if (Directory.Exists(root) || File.Exists(root))
                throw new InvalidDataException("The carrier destination must not already exist.");

            var scope = new TrustedLocalFileScope([package]);
            scope.ValidateDirectory(package, allowMissing: false);
            var names = Directory.GetFileSystemEntries(package).Select(Path.GetFileName).Order(StringComparer.Ordinal);
            if (!names.SequenceEqual(new[] { "package.json", "source42.zip" }, StringComparer.Ordinal))
                throw new InvalidDataException("A fixture package must contain exactly package.json and source42.zip.");
            var binding = ReadBinding(ReadFile(scope, Path.Combine(package, "package.json"), JointInterfaceCrashSeed.MaximumManifestBytes));
            using var archiveStream = new FileStream(scope.ValidateFile(Path.Combine(package, "source42.zip"), allowMissing: false),
                FileMode.Open, FileAccess.Read, FileShare.Read);
            if (Convert.ToHexString(SHA256.HashData(archiveStream)) != binding.Archive)
                throw new InvalidDataException("The fixture archive does not match its bound SHA256.");
            archiveStream.Position = 0;
            using var zip = new ZipArchive(archiveStream, ZipArchiveMode.Read, leaveOpen: true);
            var entries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var directories = new HashSet<string>(StringComparer.Ordinal);
            var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            byte[]? manifest = null;
            long payloadBytes = 0;
            foreach (var entry in zip.Entries)
            {
                var name = entry.FullName;
                var directory = ValidateEntry(entry);
                if (!entries.Add(name) || entries.Count > JointInterfaceCrashSeed.MaximumFiles + JointInterfaceCrashSeed.MaximumManifestBytes / 3 + 2)
                    throw new InvalidDataException("The fixture archive contains duplicate entries or exceeds the seed inventory bound.");
                if (directory)
                {
                    if (name == "payload/")
                        directories.Add("");
                    else if (name.StartsWith("payload/", StringComparison.Ordinal))
                        directories.Add(name[8..^1]);
                    else
                        throw new InvalidDataException("A fixture directory entry is outside the payload namespace.");
                    continue;
                }
                if (name == "manifest.json")
                {
                    manifest = ReadEntry(entry, JointInterfaceCrashSeed.MaximumManifestBytes);
                    continue;
                }
                if (!name.StartsWith("payload/", StringComparison.Ordinal) || files.Count == JointInterfaceCrashSeed.MaximumFiles)
                    throw new InvalidDataException("A fixture file entry is outside the declared payload namespace.");
                var bytes = ReadEntry(entry, JointInterfaceCrashSeed.MaximumPayloadBytes - payloadBytes);
                payloadBytes += bytes.LongLength;
                files.Add(name[8..], bytes);
            }
            if (manifest is null || !directories.Remove("") || Convert.ToHexString(SHA256.HashData(manifest)) != binding.Manifest)
                throw new InvalidDataException("The fixture archive lacks its exact bound manifest or explicit payload directory.");
            var fingerprint = JointInterfaceCrashSeed.ValidateTransportSnapshot(manifest, files, directories);
            // The original writer owns only its newly created partial namespace. No child has been launched here.
            JointInterfaceCrashSeed.WriteNewRootAsync(root, writeScope =>
            {
                writeScope.EnsureDirectory(Path.Combine(root, "payload"));
                foreach (var directory in directories.OrderBy(name => name.Count(character => character == '/')).ThenBy(name => name, StringComparer.Ordinal))
                    writeScope.EnsureDirectory(Path.Combine(root, "payload", directory));
                foreach (var file in files)
                    WriteFile(writeScope, Path.Combine(root, "payload", file.Key), file.Value);
                WriteFile(writeScope, Path.Combine(root, "manifest.json"), manifest);
                _ = JointInterfaceCrashSeed.Open(root, fingerprint);
                return Task.CompletedTask;
            }).GetAwaiter().GetResult();
        }
        catch (Exception error) when (error is JsonException or ArgumentException or NotSupportedException ||
            error is IOException and not InvalidDataException || error is UnauthorizedAccessException)
        {
            throw new InvalidDataException("The fixture carrier cannot be hydrated as checked ordinary data.", error);
        }
    }

    /// <summary>
    /// Reads the exact case-sensitive package binding schema without accepting duplicate or unsupported members.
    /// </summary>
    /// <param name="bytes">
    /// The bounded original package.json bytes.
    /// </param>
    /// <returns>
    /// The uppercase archive and manifest SHA256 digests from schema version1.
    /// </returns>
    private static (string Archive, string Manifest) ReadBinding(byte[] bytes)
    {
        using var document = JsonDocument.Parse(bytes);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("The fixture binding must be an object.");
        var properties = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var property in document.RootElement.EnumerateObject())
            if (property.Name is not ("SchemaVersion" or "ArchiveSha256" or "ManifestSha256") ||
                !properties.TryAdd(property.Name, property.Value))
                throw new InvalidDataException("The fixture binding contains unknown or duplicate members.");
        if (properties.Count != 3 || properties["SchemaVersion"].ValueKind != JsonValueKind.Number ||
            !properties["SchemaVersion"].TryGetInt32(out var version) || version != 1)
            throw new InvalidDataException("The fixture binding is incomplete or has an unsupported schema version.");
        return (ReadDigest(properties["ArchiveSha256"]), ReadDigest(properties["ManifestSha256"]));
    }

    /// <summary>
    /// Reads one exact uppercase SHA256 string without coercing other JSON kinds.
    /// </summary>
    /// <param name="value">
    /// The bound digest member.
    /// </param>
    /// <returns>
    /// The validated 64-character uppercase hexadecimal digest.
    /// </returns>
    private static string ReadDigest(JsonElement value)
    {
        var digest = value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        if (digest is null || digest.Length != 64 || digest.Any(character => character is not (>= '0' and <= '9') and not (>= 'A' and <= 'F')))
            throw new InvalidDataException("The fixture binding digest must be uppercase SHA256.");
        return digest;
    }

    /// <summary>
    /// Rejects unsafe entry spellings, unsupported types, links and nonempty directory entries.
    /// </summary>
    /// <param name="entry">
    /// The unmodified archive entry whose name is never normalized into a different spelling.
    /// </param>
    /// <returns>
    /// <see langword="true"/> for an explicit directory entry; otherwise, <see langword="false"/> for an ordinary file.
    /// </returns>
    private static bool ValidateEntry(ZipArchiveEntry entry)
    {
        var name = entry.FullName;
        var directory = name.EndsWith('/');
        var logical = directory ? name[..^1] : name;
        if (string.IsNullOrWhiteSpace(logical) || name.Contains('\\') || name.Contains(':') || name.StartsWith('/') ||
            logical.Split('/').Any(component => string.IsNullOrWhiteSpace(component) || component is "." or ".."))
            throw new InvalidDataException("The fixture archive contains an unsafe path spelling.");
        var type = (entry.ExternalAttributes >> 16) & 0xF000;
        if ((entry.ExternalAttributes & (int)(FileAttributes.ReparsePoint | FileAttributes.Device)) != 0 ||
            (type != 0 && type != (directory ? 0x4000 : 0x8000)) ||
            (!directory && (entry.ExternalAttributes & (int)FileAttributes.Directory) != 0) ||
            (directory && entry.Length != 0))
            throw new InvalidDataException("The fixture archive contains a link, unsupported type or invalid directory entry.");
        return directory;
    }

    /// <summary>
    /// Reads one entry within the original codec's declared uncompressed data bounds.
    /// </summary>
    /// <param name="entry">
    /// The ordinary file entry already checked for safe spelling and type.
    /// </param>
    /// <param name="maximumBytes">
    /// The remaining codec payload bound, or its existing manifest bound.
    /// </param>
    /// <returns>
    /// Every exact uncompressed byte, refusing an oversized or inconsistent stream.
    /// </returns>
    private static byte[] ReadEntry(ZipArchiveEntry entry, long maximumBytes)
    {
        if (entry.Length < 0 || entry.Length > maximumBytes)
            throw new InvalidDataException("The fixture archive entry exceeds the seed format bound.");
        using var stream = entry.Open();
        var bytes = new byte[checked((int)entry.Length)];
        stream.ReadExactly(bytes);
        if (stream.ReadByte() != -1)
            throw new InvalidDataException("The fixture archive entry changed its declared length.");
        return bytes;
    }

    /// <summary>
    /// Reads a bounded ordinary binding file through the existing trusted local path checks.
    /// </summary>
    /// <param name="scope">
    /// The real package scope used to reject physical namespace aliases.
    /// </param>
    /// <param name="path">
    /// The exact binding filename inside that scope.
    /// </param>
    /// <param name="maximumBytes">
    /// The existing codec manifest-size bound reused for this small binding.
    /// </param>
    /// <returns>
    /// The complete original file bytes.
    /// </returns>
    private static byte[] ReadFile(TrustedLocalFileScope scope, string path, long maximumBytes)
    {
        using var stream = new FileStream(scope.ValidateFile(path, allowMissing: false), FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > maximumBytes)
            throw new InvalidDataException("The fixture binding exceeds the seed manifest bound.");
        var bytes = new byte[checked((int)stream.Length)];
        stream.ReadExactly(bytes);
        if (stream.ReadByte() != -1)
            throw new InvalidDataException("The fixture binding changed length during its read.");
        return bytes;
    }

    /// <summary>
    /// Creates one exact file in the original codec's newly owned destination namespace.
    /// </summary>
    /// <param name="scope">
    /// The writer's real trusted local scope for the new artifact.
    /// </param>
    /// <param name="path">
    /// The already validated manifest or payload destination spelling.
    /// </param>
    /// <param name="bytes">
    /// The exact checked carrier bytes to write without normalization.
    /// </param>
    private static void WriteFile(TrustedLocalFileScope scope, string path, byte[] bytes)
    {
        using var stream = new FileStream(scope.ValidateFile(path, allowMissing: true), FileMode.CreateNew, FileAccess.Write, FileShare.None);
        stream.Write(bytes);
    }
}
