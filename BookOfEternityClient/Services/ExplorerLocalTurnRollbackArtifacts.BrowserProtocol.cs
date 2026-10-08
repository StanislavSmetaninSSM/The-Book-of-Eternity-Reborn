using System.Text.Json;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public static partial class ExplorerLocalTurnRollbackArtifacts
{
    internal enum BrowserStorageProtocol { None, Original, Current }

    // Read-only routing, never recovery/adoption authority. Validate the WHOLE
    // namespace before either handler or the common journal may have effects.
    internal static BrowserStorageProtocol ClassifyBrowserStorageEvidence(
        FileSystemManager fs, FileSystemManager.CanonicalWriteLease lease)
    {
        var files = fs.ListBrowserStorageEvidence(lease);
        if (files.Count == 0) return BrowserStorageProtocol.None;
        var scratch = fs.ReadPendingBrowserPublicationScratch(lease);
        var protocol = BrowserStorageProtocol.None;
        var originalRoots = new List<(string Root, HashSet<string> Files)>();
        foreach (var root in files.Select(path => path[..path.LastIndexOf('/')]).Distinct(BrowserDestinationComparer))
        {
            var own = files.Where(path => path.StartsWith(root + "/", BrowserDestinationComparison)).ToHashSet(BrowserDestinationComparer);
            if (root.StartsWith(DirectGachaRoot + "/", StringComparison.Ordinal))
            {
                if (!IsLocalDirectGachaPath(root) || own.Any(path =>
                        !IsLocalDirectGachaBackup(path) && !scratch.Contains(path, BrowserDestinationComparer)))
                    throw new InvalidDataException("Unrecognized direct-gacha storage evidence; bytes retained.");
                continue; // Neutral retained before-images, never signed adoption authority.
            }
            var parts = root.Split('/');
            if (parts.Length != Root.Split('/').Length + 2 || !root.StartsWith(Root + "/", BrowserDestinationComparison) ||
                parts[^2] != SafeSegment(parts[^2]) || !IsValidTransactionDirectoryName(parts[^1]))
                throw new InvalidDataException("Unrecognized browser evidence namespace; bytes retained.");
            var manifest = root + "/" + BrowserWriteManifestFileName;
            var committed = CleanupPath(root, BrowserWriteCleanupOutcome.Committed);
            var restored = CleanupPath(root, BrowserWriteCleanupOutcome.Restored);
            if (own.Contains(committed) && own.Contains(restored))
                throw new InvalidDataException("Conflicting browser cleanup protocols; bytes retained.");
            var selected = BrowserStorageProtocol.None;
            if (own.Contains(manifest)) Select(ReadSchema(fs.ReadBrowserStorageEvidence(lease, manifest)!, cleanup: false));
            foreach (var intent in new[] { committed, restored }.Where(own.Contains))
                Select(ReadSchema(fs.ReadBrowserStorageEvidence(lease, intent)!, cleanup: true));
            if (selected == BrowserStorageProtocol.None)
                throw new InvalidDataException("Browser evidence has no recognized protocol owner; bytes retained.");
            if (protocol != BrowserStorageProtocol.None && protocol != selected)
                throw new InvalidDataException("Mixed original/current browser protocols; bytes retained.");
            protocol = selected;
            if (selected == BrowserStorageProtocol.Original) originalRoots.Add((root, own));

            void Select(BrowserStorageProtocol value)
            {
                if (selected != BrowserStorageProtocol.None && selected != value)
                    throw new InvalidDataException("Mixed original/current browser metadata; bytes retained.");
                selected = value;
            }
        }
        if (protocol == BrowserStorageProtocol.Original)
        {
            // Preserve original semantic validation and declared backup names;
            // do not let unknown siblings survive until after original effects.
            foreach (var (root, own) in originalRoots)
            {
                var manifestPath = root + "/" + BrowserWriteManifestFileName;
                var marker = root + "/" + BrowserWriteCommittedMarkerFileName;
                var committed = CleanupPath(root, BrowserWriteCleanupOutcome.Committed);
                var restored = CleanupPath(root, BrowserWriteCleanupOutcome.Restored);
                var allowed = new HashSet<string>(BrowserDestinationComparer) { manifestPath, marker, committed, restored };
                if (own.Contains(manifestPath))
                {
                    BrowserWriteRollbackManifest manifest;
                    try { manifest = StrictJsonAuthority.Deserialize<BrowserWriteRollbackManifest>(
                        DecodeUtf8(fs.ReadBrowserStorageEvidence(lease, manifestPath)!), ManifestJsonOptions, "Original browser evidence")
                        ?? throw new InvalidDataException("Empty original browser manifest."); }
                    catch (JsonException failure) { throw new InvalidDataException("Malformed original browser evidence; bytes retained.", failure); }
                    var transaction = ValidateBrowserWriteManifest(fs, manifestPath, manifest).Transaction;
                    foreach (var entry in transaction.Entries) if (entry.BackupPath != null) allowed.Add(entry.BackupPath);
                    foreach (var entry in transaction.ExternalEntries) if (entry.BackupPath != null) allowed.Add(entry.BackupPath);
                }
                else if (own.Count != 1 || (!own.Contains(committed) && !own.Contains(restored)))
                    throw new InvalidDataException("Original cleanup-only evidence contains unknown members.");
                foreach (var path in new[] { marker, committed, restored }.Where(own.Contains))
                    EnsureEmptyBrowserWriteMarker(fs.ReadBrowserStorageEvidence(lease, path)!, "Original browser marker");
                if (own.Any(path => !allowed.Contains(path)))
                    throw new InvalidDataException("Original browser evidence contains unknown members; bytes retained.");
            }
        }
        else PreflightLocalBrowserEvidence(fs, lease); // Full current binding/scratch validation is also read-only.
        return protocol;
    }

    private static BrowserStorageProtocol ReadSchema(byte[] bytes, bool cleanup)
    {
        if (cleanup && bytes.Length == 0) return BrowserStorageProtocol.Original;
        try
        {
            var document = StrictJsonAuthority.Deserialize<JsonElement>(DecodeUtf8(bytes), ManifestJsonOptions, "Browser protocol discriminator");
            if (document.ValueKind != JsonValueKind.Object || !document.TryGetProperty("schemaVersion", out var schema) ||
                schema.ValueKind != JsonValueKind.Number || !schema.TryGetInt32(out var version)) throw new InvalidDataException("Browser protocol schema is missing.");
            if (version == LocalBrowserSchema) return BrowserStorageProtocol.Current;
            if (!cleanup && version is >= 1 and <= 6) return BrowserStorageProtocol.Original;
            throw new InvalidDataException("Unsupported browser protocol; original bytes retained.");
        }
        catch (JsonException failure) { throw new InvalidDataException("Malformed browser protocol; original bytes retained.", failure); }
    }
}
