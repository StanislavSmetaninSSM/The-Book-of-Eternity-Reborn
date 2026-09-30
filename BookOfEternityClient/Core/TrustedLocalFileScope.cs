using System.ComponentModel;
using System.Runtime.InteropServices;

namespace BookOfEternityClient.Core;

/// <summary>
/// Constrains ordinary application file operations in a trusted local namespace.
/// Checks do not promise protection against a hostile concurrent computer owner.
/// </summary>
internal sealed class TrustedLocalFileScope
{
    private static readonly StringComparison PathComparison = OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
    private static readonly StringComparer PathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    private readonly string[] _roots;
    private readonly HashSet<string> _exactFiles;

    internal TrustedLocalFileScope(IEnumerable<string> roots, IEnumerable<string>? exactFiles = null)
    {
        ArgumentNullException.ThrowIfNull(roots);
        _roots = roots.Select(Normalize).Distinct(PathComparer).ToArray();
        _exactFiles = new HashSet<string>((exactFiles ?? []).Select(Normalize), PathComparer);
        if (_roots.Length == 0 && _exactFiles.Count == 0)
            throw new ArgumentException("A local file scope requires an explicit root or file.");

        foreach (var root in _roots)
        {
            var parent = Path.GetDirectoryName(root)
                ?? throw new InvalidDataException("A filesystem root is not a local storage scope.");
            ValidateDirectories(parent, allowMissing: false);
            ValidateDirectories(root, allowMissing: true);
        }
        foreach (var file in _exactFiles)
            ValidateDirectories(Path.GetDirectoryName(file)!, allowMissing: false);
    }

    internal string ValidateFile(string path, bool allowMissing = true)
    {
        var normalized = Normalize(path);
        EnsureAllowed(normalized, file: true);
        ValidateDirectories(Path.GetDirectoryName(normalized)!, allowMissing: true);
        var kind = Probe(normalized);
        if (kind == EntryKind.Missing && !allowMissing)
            throw new FileNotFoundException("The required local file is absent.", normalized);
        if (kind is not (EntryKind.RegularFile or EntryKind.Missing))
            throw new InvalidDataException("The local file path is not a regular file: " + normalized);
        return normalized;
    }

    internal string EnsureDirectory(string path)
    {
        var normalized = ValidateDirectory(path);
        Directory.CreateDirectory(normalized);
        ValidateDirectories(normalized, allowMissing: false);
        return normalized;
    }

    internal void DeleteOwnedFile(string path) => File.Delete(ValidateFile(path));

    internal void DeleteOwnedTree(string path)
    {
        var normalized = ValidateDirectory(path);
        if (Probe(normalized) == EntryKind.Missing)
            return;

        // Preflight the complete owned tree before deleting any evidence. Neither
        // enumeration nor removal follows a link to a different namespace.
        var directories = new List<string> { normalized };
        var files = new List<string>();
        for (var index = 0; index < directories.Count; index++)
        {
            var directory = ValidateDirectory(directories[index], allowMissing: false);
            foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
            {
                if (Probe(entry) == EntryKind.Directory)
                    directories.Add(ValidateDirectory(entry, allowMissing: false));
                else
                    files.Add(ValidateFile(entry, allowMissing: false));
            }
        }

        foreach (var file in files)
            DeleteOwnedFile(file);
        for (var index = directories.Count - 1; index >= 0; index--)
            Directory.Delete(ValidateDirectory(directories[index], allowMissing: false), recursive: false);
    }

    private string ValidateDirectory(string path, bool allowMissing = true)
    {
        var normalized = Normalize(path);
        EnsureAllowed(normalized, file: false);
        ValidateDirectories(normalized, allowMissing);
        return normalized;
    }

    private void EnsureAllowed(string path, bool file)
    {
        if ((file && _exactFiles.Contains(path)) || _roots.Any(root =>
                string.Equals(path, root, PathComparison) ||
                path.StartsWith(root + Path.DirectorySeparatorChar, PathComparison)))
            return;
        throw new InvalidDataException("The path is outside the explicitly allowed local storage scope.");
    }

    private static string Normalize(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
            throw new InvalidDataException("A local storage path must be absolute.");
        if (path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Contains(".."))
            throw new InvalidDataException("Parent traversal is not a local storage path.");
        var normalized = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        if (OperatingSystem.IsWindows() &&
            normalized[(Path.GetPathRoot(normalized)?.Length ?? 0)..].Contains(':'))
            throw new InvalidDataException("Alternate streams are not local storage files.");
        return normalized;
    }

    private static void ValidateDirectories(string path, bool allowMissing)
    {
        var ancestors = new Stack<string>();
        for (string? current = path; current != null; current = Path.GetDirectoryName(current))
            ancestors.Push(current);
        foreach (var directory in ancestors)
        {
            var kind = Probe(directory);
            if (kind == EntryKind.Missing && allowMissing)
                return;
            if (kind != EntryKind.Directory)
                throw new InvalidDataException("A local storage ancestor is missing, linked or not a directory: " + directory);
        }
    }

    private enum EntryKind { Missing, RegularFile, Directory, Link, Other }

    private static EntryKind Probe(string path)
    {
        FileAttributes attributes;
        try { attributes = File.GetAttributes(path); }
        catch (FileNotFoundException) { return EntryKind.Missing; }
        catch (DirectoryNotFoundException) { return EntryKind.Missing; }
        if ((attributes & FileAttributes.ReparsePoint) != 0)
            return EntryKind.Link;
        if ((attributes & FileAttributes.Directory) != 0)
            return EntryKind.Directory;
        if ((attributes & FileAttributes.Device) != 0)
            return EntryKind.Other;
        if (!OperatingSystem.IsLinux())
            return EntryKind.RegularFile;

        // .NET 8 Unix FileAttributes maps FIFOs/sockets/devices to Normal. Query
        // only the stable Linux file-type ABI, without opening a potentially
        // blocking special file or inventing physical identity authority.
        const int atCurrentDirectory = -100;
        const int noFollow = 0x100;
        const int noAutomount = 0x800;
        const uint typeMask = 1;
        if (Statx(atCurrentDirectory, path, noFollow | noAutomount, typeMask, out var status) != 0)
        {
            var error = Marshal.GetLastPInvokeError();
            if (error == 2) return EntryKind.Missing;
            throw new IOException("Could not inspect the local file type: " + path, new Win32Exception(error));
        }
        if ((status.Mask & typeMask) == 0)
            throw new IOException("The filesystem did not report a local file type.");
        return (status.Mode & 0xF000) switch
        {
            0x8000 => EntryKind.RegularFile,
            0x4000 => EntryKind.Directory,
            0xA000 => EntryKind.Link,
            _ => EntryKind.Other
        };
    }

    // Linux statx has a fixed 256-byte ABI. Only requested type fields are read.
    [StructLayout(LayoutKind.Explicit, Size = 256)]
    private struct LinuxFileStatus
    {
        [FieldOffset(0)] internal uint Mask;
        [FieldOffset(28)] internal ushort Mode;
    }

    [DllImport("libc", EntryPoint = "statx", SetLastError = true, CharSet = CharSet.Ansi)]
    private static extern int Statx(int directory, string path, int flags, uint mask, out LinuxFileStatus status);
}
