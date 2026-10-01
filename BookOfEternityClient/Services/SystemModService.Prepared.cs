using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal sealed record PreparedSystemModManifest(byte[] Bytes, IReadOnlyList<string> EnabledFiles,
    IReadOnlyList<SystemModService.SystemModDescriptor> ActiveMods);

public sealed partial class SystemModService
{
    internal async Task<PreparedSystemModManifest> PrepareManifestForGmAsync(
        FileSystemManager.CanonicalWriteLease lease, IReadOnlyCollection<string> enabledFiles, byte[]? currentBytes)
    {
        ArgumentNullException.ThrowIfNull(enabledFiles);
        _fs.EnsureCanonicalWriteLeaseActive(lease);
        _fs.VerifyCurrentSessionOperation(lease);
        var modsDirectory = GetModsDirectoryPath();
        // Construction validates every ancestor and the root itself without
        // creating an absent directory or traversing a linked directory.
        var scope = new TrustedLocalFileScope([modsDirectory]);
        var enabled = new HashSet<string>(enabledFiles, StringComparer.OrdinalIgnoreCase);
        var mods = new List<SystemModDescriptor>();
        if (Directory.Exists(modsDirectory))
        {
            var files = Directory.EnumerateFiles(modsDirectory, "*.*", SearchOption.TopDirectoryOnly)
                .Where(path => SupportedExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
                .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
                .ToArray();
            foreach (var file in files)
            {
                var path = scope.ValidateFile(file, allowMissing: false);
                var fileName = Path.GetFileName(path);
                var bytes = await _fs.ReadLocalFileBytesAsync(lease, $"{ModsDirectory}/{fileName}")
                    ?? throw new FileNotFoundException("A mod input disappeared during settings preparation.", path);
                mods.Add(BuildDescriptor(path, LocalSettingsPreparation.DecodeText(bytes), includeContent: true, enabled.Contains(fileName)));
            }
        }
        var active = mods.Where(mod => mod.Enabled).ToArray();
        var normalizedEnabled = active.Select(mod => mod.FileName).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var manifest = BuildManifestNode(mods, normalizedEnabled);
        var currentJson = currentBytes == null ? null : LocalSettingsPreparation.DecodeText(currentBytes);
        byte[] desired;
        if (currentBytes != null && SemanticallyMatchesExistingManifest(currentJson, manifest))
            desired = currentBytes;
        else
        {
            manifest["_lastUpdated"] = DateTime.UtcNow.ToString("o");
            desired = LocalSettingsPreparation.EncodeText(manifest.ToJsonString(JsonOpts));
        }
        _fs.VerifyCurrentSessionOperation(lease);
        return new(desired, normalizedEnabled, active);
    }
}
