using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace BookOfEternityClient.Core;

internal static class PhysicalFileAuthority
{
    private const uint DeleteAccess = 0x00010000;
    private const uint SynchronizeAccess = 0x00100000;
    private const uint FileListDirectory = 0x00000001;
    private const uint FileReadAttributes = 0x00000080;
    private const uint GenericRead = 0x80000000;
    private const uint GenericWrite = 0x40000000;
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint CreateNew = 1;
    private const uint OpenExisting = 3;
    private const uint FileAttributeNormal = 0x00000080;
    private const uint FileFlagWriteThrough = 0x80000000;
    private const uint FileFlagOverlapped = 0x40000000;
    private const uint FileFlagOpenReparsePoint = 0x00200000;
    private const uint FileFlagBackupSemantics = 0x02000000;
    private const int FileRenameInfo = 3;
    private const int FileDispositionInfo = 4;
    private const int FileStandardInfoClass = 1;
    private const int FileAttributeTagInfo = 9;
    private const uint FileNameOpened = 0x00000008;
    private const int ErrorFileNotFound = 2;
    private const int ErrorPathNotFound = 3;
    private const int ErrorAccessDenied = 5;

    internal sealed class StableDirectory : IDisposable
    {
        private SafeFileHandle? _handle;

        internal StableDirectory(string fullPath, SafeFileHandle? handle)
        {
            FullPath = Path.GetFullPath(fullPath);
            _handle = handle;
        }

        internal string FullPath { get; }
        internal SafeFileHandle? Handle => _handle;

        public void Dispose()
        {
            var handle = _handle;
            _handle = null;
            handle?.Dispose();
        }
    }

    internal static StableDirectory EnsureStableDirectory(
        string rootPath,
        string targetPath,
        string authorityName)
    {
        var root = Path.GetFullPath(rootPath).TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);
        var target = Path.GetFullPath(targetPath).TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);
        if (!IsSameOrDescendant(target, root))
        {
            throw new InvalidDataException(
                $"{authorityName} directory is outside its physical root.");
        }

        if (!Directory.Exists(root))
            throw new DirectoryNotFoundException(
                $"{authorityName} physical root does not exist: {root}");

        var current = OpenStableDirectory(root, authorityName);
        if (PathsEqual(root, target))
            return current;

        try
        {
            var relative = Path.GetRelativePath(root, target);
            foreach (var segment in relative.Split(
                         [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                         StringSplitOptions.RemoveEmptyEntries))
            {
                if (segment is "." or "..")
                {
                    throw new InvalidDataException(
                        $"{authorityName} directory traversal is forbidden.");
                }

                var childPath = Path.Combine(current.FullPath, segment);
                Directory.CreateDirectory(childPath);
                var child = OpenStableDirectory(childPath, authorityName);
                current.Dispose();
                current = child;
            }

            return current;
        }
        catch
        {
            current.Dispose();
            throw;
        }
    }

    internal static StableDirectory OpenStableDirectory(
        string expectedPath,
        string authorityName)
    {
        var normalizedPath = Path.GetFullPath(expectedPath);
        if (!Directory.Exists(normalizedPath))
        {
            throw new DirectoryNotFoundException(
                $"{authorityName} directory does not exist: {normalizedPath}");
        }

        if (!OperatingSystem.IsWindows())
            return new StableDirectory(normalizedPath, handle: null);

        var handle = CreateFile(
            ToWindowsExtendedPath(normalizedPath),
            FileListDirectory,
            FileShareRead | FileShareWrite,
            IntPtr.Zero,
            OpenExisting,
            FileFlagBackupSemantics,
            IntPtr.Zero);
        if (handle.IsInvalid)
        {
            var error = Marshal.GetLastWin32Error();
            handle.Dispose();
            throw CreateIoException(
                $"Could not open {authorityName} directory authority.",
                error);
        }

        try
        {
            EnsureHandleMatchesExpectedPath(
                handle,
                normalizedPath,
                authorityName);
            return new StableDirectory(normalizedPath, handle);
        }
        catch
        {
            handle.Dispose();
            throw;
        }
    }

    internal static FileStream CreateNewWritableFile(
        StableDirectory parent,
        string expectedPath,
        string authorityName,
        bool asynchronous)
    {
        EnsureDirectChild(parent, expectedPath, authorityName);
        var normalizedPath = Path.GetFullPath(expectedPath);
        if (!OperatingSystem.IsWindows())
        {
            return new FileStream(
                normalizedPath,
                FileMode.CreateNew,
                FileAccess.ReadWrite,
                FileShare.Read,
                bufferSize: 4096,
                asynchronous
                    ? FileOptions.Asynchronous | FileOptions.WriteThrough
                    : FileOptions.WriteThrough);
        }

        var flags = FileAttributeNormal | FileFlagWriteThrough;
        if (asynchronous)
            flags |= FileFlagOverlapped;
        var handle = CreateFile(
            ToWindowsExtendedPath(normalizedPath),
            GenericRead | GenericWrite | DeleteAccess | SynchronizeAccess,
            FileShareRead,
            IntPtr.Zero,
            CreateNew,
            flags,
            IntPtr.Zero);
        if (handle.IsInvalid)
        {
            var error = Marshal.GetLastWin32Error();
            handle.Dispose();
            throw CreateIoException(
                $"Could not create {authorityName} file.",
                error);
        }

        try
        {
            EnsureHandleMatchesExpectedPath(
                handle,
                normalizedPath,
                authorityName);
            return new FileStream(
                handle,
                FileAccess.ReadWrite,
                bufferSize: 4096,
                isAsync: asynchronous);
        }
        catch
        {
            handle.Dispose();
            throw;
        }
    }

    internal static FileStream? OpenReadFile(
        StableDirectory parent,
        string expectedPath,
        string authorityName,
        bool asynchronous,
        Action? afterOpenedBeforeValidation = null)
    {
        EnsureDirectChild(parent, expectedPath, authorityName);
        var normalizedPath = Path.GetFullPath(expectedPath);
        FileStream stream;
        try
        {
            stream = new FileStream(
                normalizedPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                asynchronous
                    ? FileOptions.Asynchronous | FileOptions.SequentialScan
                    : FileOptions.SequentialScan);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }

        try
        {
            afterOpenedBeforeValidation?.Invoke();
            EnsureHandleMatchesExpectedPath(
                stream.SafeFileHandle,
                normalizedPath,
                authorityName);
            return stream;
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    internal static SafeFileHandle OpenForRename(
        StableDirectory parent,
        string expectedPath,
        bool isDirectory,
        string authorityName)
    {
        EnsureDirectChild(parent, expectedPath, authorityName);
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "Opened-handle rename is available only on Windows.");
        }

        var normalizedPath = Path.GetFullPath(expectedPath);
        var access = DeleteAccess | SynchronizeAccess | FileReadAttributes;
        if (isDirectory)
            access |= FileListDirectory;
        var flags = isDirectory
            ? FileFlagBackupSemantics
            : FileAttributeNormal;
        var handle = CreateFile(
            ToWindowsExtendedPath(normalizedPath),
            access,
            FileShareRead | FileShareWrite,
            IntPtr.Zero,
            OpenExisting,
            flags,
            IntPtr.Zero);
        if (handle.IsInvalid)
        {
            var error = Marshal.GetLastWin32Error();
            handle.Dispose();
            throw CreateIoException(
                $"Could not open {authorityName} source.",
                error);
        }

        try
        {
            EnsureHandleMatchesExpectedPath(
                handle,
                normalizedPath,
                authorityName);
            EnsureOpenedObjectKind(
                handle,
                isDirectory,
                authorityName);
            return handle;
        }
        catch
        {
            handle.Dispose();
            throw;
        }
    }

    internal static void RenameOpenedObject(
        SafeFileHandle sourceHandle,
        string destinationPath,
        bool replaceExisting,
        string authorityName)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "Opened-handle rename is available only on Windows.");
        }

        EnsureRegularFileHasSingleLink(sourceHandle, authorityName);
        var normalizedDestination = Path.GetFullPath(destinationPath);
        var fileNameBytes = Encoding.Unicode.GetBytes(
            ToWindowsExtendedPath(normalizedDestination));
        var rootDirectoryOffset = IntPtr.Size == 8 ? 8 : 4;
        var fileNameLengthOffset = rootDirectoryOffset + IntPtr.Size;
        var fileNameOffset = fileNameLengthOffset + sizeof(int);
        var bufferSize = checked(fileNameOffset + fileNameBytes.Length + sizeof(char));
        var buffer = Marshal.AllocHGlobal(bufferSize);
        try
        {
            for (var index = 0; index < bufferSize; index++)
                Marshal.WriteByte(buffer, index, 0);
            Marshal.WriteByte(buffer, 0, replaceExisting ? (byte)1 : (byte)0);
            Marshal.WriteIntPtr(buffer, rootDirectoryOffset, IntPtr.Zero);
            Marshal.WriteInt32(
                buffer,
                fileNameLengthOffset,
                fileNameBytes.Length);
            Marshal.Copy(
                fileNameBytes,
                0,
                IntPtr.Add(buffer, fileNameOffset),
                fileNameBytes.Length);

            if (!SetFileInformationByHandle(
                    sourceHandle,
                    FileRenameInfo,
                    buffer,
                    (uint)bufferSize))
            {
                throw CreateIoException(
                    $"Could not publish {authorityName} opened object.",
                    Marshal.GetLastWin32Error());
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }

        EnsureHandleMatchesExpectedPath(
            sourceHandle,
            normalizedDestination,
            authorityName);
    }

    internal static void ValidateExistingReplacementTarget(
        StableDirectory parent,
        string expectedPath,
        string authorityName)
    {
        EnsureDirectChild(parent, expectedPath, authorityName);
        if (!OperatingSystem.IsWindows())
            return;

        var normalizedPath = Path.GetFullPath(expectedPath);
        var handle = CreateFile(
            ToWindowsExtendedPath(normalizedPath),
            FileReadAttributes | SynchronizeAccess,
            FileShareRead | FileShareWrite,
            IntPtr.Zero,
            OpenExisting,
            FileAttributeNormal,
            IntPtr.Zero);
        if (handle.IsInvalid)
        {
            var error = Marshal.GetLastWin32Error();
            handle.Dispose();
            if (error is ErrorFileNotFound or ErrorPathNotFound)
                return;

            throw CreateIoException(
                $"Could not inspect {authorityName} replacement target.",
                error);
        }

        using (handle)
        {
            EnsureHandleMatchesExpectedPath(
                handle,
                normalizedPath,
                authorityName);
            EnsureOpenedObjectKind(
                handle,
                expectedDirectory: false,
                authorityName);
        }
    }

    internal static bool TryDeleteFile(
        StableDirectory parent,
        string expectedPath,
        string authorityName)
    {
        EnsureDirectChild(parent, expectedPath, authorityName);
        var normalizedPath = Path.GetFullPath(expectedPath);
        if (!OperatingSystem.IsWindows())
        {
            if (!File.Exists(normalizedPath))
                return false;
            File.Delete(normalizedPath);
            return true;
        }

        var handle = CreateFile(
            ToWindowsExtendedPath(normalizedPath),
            DeleteAccess | FileReadAttributes | SynchronizeAccess,
            FileShareRead | FileShareWrite,
            IntPtr.Zero,
            OpenExisting,
            FileAttributeNormal | FileFlagOpenReparsePoint,
            IntPtr.Zero);
        if (handle.IsInvalid)
        {
            var error = Marshal.GetLastWin32Error();
            handle.Dispose();
            if (error is ErrorFileNotFound or ErrorPathNotFound)
                return false;
            throw CreateIoException(
                $"Could not open {authorityName} deletion target.",
                error);
        }

        using (handle)
        {
            EnsureHandleMatchesExpectedPath(
                handle,
                normalizedPath,
                authorityName,
                FileNameOpened);
            MarkOpenedObjectForDeletion(handle, authorityName);
        }

        return true;
    }

    internal static bool TryDeleteTree(
        string expectedPath,
        string authorityName)
    {
        var normalizedPath = Path.GetFullPath(expectedPath).TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);
        var parentPath = Path.GetDirectoryName(normalizedPath);
        if (parentPath == null)
        {
            throw new InvalidDataException(
                $"{authorityName} tree has no parent directory.");
        }

        if (!OperatingSystem.IsWindows())
        {
            if (!Directory.Exists(normalizedPath))
                return false;
            DeleteTreeFallback(normalizedPath);
            return true;
        }

        using var parent = OpenStableDirectory(parentPath, authorityName);
        return TryDeleteEntry(parent, normalizedPath, authorityName);
    }

    internal static bool TryDeleteTree(
        StableDirectory parent,
        string expectedPath,
        string authorityName)
    {
        ArgumentNullException.ThrowIfNull(parent);
        return TryDeleteEntry(
            parent,
            Path.GetFullPath(expectedPath),
            authorityName);
    }

    internal static bool TryDeleteEmptyDirectory(
        StableDirectory parent,
        string expectedPath,
        string authorityName)
    {
        EnsureDirectChild(parent, expectedPath, authorityName);
        var normalizedPath = Path.GetFullPath(expectedPath);
        if (!OperatingSystem.IsWindows())
        {
            if (!Directory.Exists(normalizedPath))
                return false;
            Directory.Delete(normalizedPath, recursive: false);
            return true;
        }

        var handle = CreateFile(
            ToWindowsExtendedPath(normalizedPath),
            DeleteAccess | FileReadAttributes | SynchronizeAccess |
            FileListDirectory,
            FileShareRead | FileShareWrite,
            IntPtr.Zero,
            OpenExisting,
            FileFlagBackupSemantics | FileFlagOpenReparsePoint,
            IntPtr.Zero);
        if (handle.IsInvalid)
        {
            var error = Marshal.GetLastWin32Error();
            handle.Dispose();
            if (error is ErrorFileNotFound or ErrorPathNotFound)
                return false;
            throw CreateIoException(
                $"Could not open {authorityName} empty directory.",
                error);
        }

        using (handle)
        {
            EnsureHandleMatchesExpectedPath(
                handle,
                normalizedPath,
                authorityName,
                FileNameOpened);
            var attributeTag = GetAttributeTag(handle, authorityName);
            if ((attributeTag.FileAttributes & FileAttributes.Directory) == 0 ||
                (attributeTag.FileAttributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidDataException(
                    $"{authorityName} target is not a physical directory.");
            }

            MarkOpenedObjectForDeletion(handle, authorityName);
        }

        return true;
    }

    internal static void EnsureHandleMatchesExpectedPath(
        SafeFileHandle handle,
        string expectedPath,
        string authorityName,
        uint flags = 0)
    {
        if (!OperatingSystem.IsWindows())
            return;

        var openedPath = NormalizeWindowsHandlePath(GetFinalPath(handle, flags));
        var normalizedExpectedPath = Path.GetFullPath(expectedPath);
        if (!PathsEqual(openedPath, normalizedExpectedPath))
        {
            throw new InvalidDataException(
                $"{authorityName} handle resolved outside its physical authority path.");
        }

        EnsureRegularFileHasSingleLink(handle, authorityName);
    }

    internal static string GetFinalPath(
        SafeFileHandle handle,
        uint flags = 0)
    {
        var capacity = 512;
        while (true)
        {
            var buffer = new StringBuilder(capacity);
            var length = GetFinalPathNameByHandle(
                handle,
                buffer,
                (uint)buffer.Capacity,
                flags);
            if (length == 0)
            {
                throw new IOException(
                    "Could not resolve an opened physical file handle.",
                    new Win32Exception(Marshal.GetLastWin32Error()));
            }

            if (length < buffer.Capacity)
                return buffer.ToString();
            capacity = checked((int)length + 1);
        }
    }

    internal static string NormalizeWindowsHandlePath(string path)
    {
        if (path.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase))
            return Path.GetFullPath(@"\\" + path[8..]);
        if (path.StartsWith(@"\\?\", StringComparison.OrdinalIgnoreCase))
            return Path.GetFullPath(path[4..]);
        return Path.GetFullPath(path);
    }

    private static void MarkOpenedObjectForDeletion(
        SafeFileHandle handle,
        string authorityName)
    {
        var disposition = new FileDisposition
        {
            DeleteFile = true
        };
        if (!SetFileInformationByHandle(
                handle,
                FileDispositionInfo,
                ref disposition,
                (uint)Marshal.SizeOf<FileDisposition>()))
        {
            throw CreateIoException(
                $"Could not delete {authorityName} opened object.",
                Marshal.GetLastWin32Error());
        }
    }

    private static bool TryDeleteEntry(
        StableDirectory parent,
        string expectedPath,
        string authorityName)
    {
        EnsureDirectChild(parent, expectedPath, authorityName);
        var normalizedPath = Path.GetFullPath(expectedPath);
        SafeFileHandle? handle = null;
        FileAttributeTag attributeTag = default;
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var expectedDirectory = Directory.Exists(normalizedPath);
            var access = DeleteAccess | FileReadAttributes | SynchronizeAccess;
            if (expectedDirectory)
                access |= FileListDirectory;
            var flags = FileFlagOpenReparsePoint |
                        (expectedDirectory
                            ? FileFlagBackupSemantics
                            : FileAttributeNormal);
            handle = CreateFile(
                ToWindowsExtendedPath(normalizedPath),
                access,
                FileShareRead | FileShareWrite,
                IntPtr.Zero,
                OpenExisting,
                flags,
                IntPtr.Zero);
            if (handle.IsInvalid)
            {
                var error = Marshal.GetLastWin32Error();
                handle.Dispose();
                handle = null;
                if (error is ErrorFileNotFound or ErrorPathNotFound)
                    return false;
                throw CreateIoException(
                    $"Could not open {authorityName} tree entry.",
                    error);
            }

            try
            {
                EnsureHandleMatchesExpectedPath(
                    handle,
                    normalizedPath,
                    authorityName,
                    FileNameOpened);
                attributeTag = GetAttributeTag(handle, authorityName);
                var actualDirectory =
                    (attributeTag.FileAttributes & FileAttributes.Directory) != 0;
                if (actualDirectory == expectedDirectory)
                    break;
            }
            catch
            {
                handle.Dispose();
                throw;
            }

            handle.Dispose();
            handle = null;
        }

        if (handle == null)
        {
            throw new IOException(
                $"{authorityName} tree entry changed type repeatedly.");
        }

        using (handle)
        {
            var isDirectory =
                (attributeTag.FileAttributes & FileAttributes.Directory) != 0;
            var isReparsePoint =
                (attributeTag.FileAttributes & FileAttributes.ReparsePoint) != 0;
            if (isDirectory && !isReparsePoint)
            {
                using var directory = new StableDirectory(
                    normalizedPath,
                    new SafeFileHandle(
                        handle.DangerousGetHandle(),
                        ownsHandle: false));
                foreach (var child in Directory.EnumerateFileSystemEntries(
                             normalizedPath,
                             "*",
                             SearchOption.TopDirectoryOnly).ToArray())
                {
                    TryDeleteEntry(directory, child, authorityName);
                }
            }

            if ((attributeTag.FileAttributes & FileAttributes.ReadOnly) != 0)
            {
                File.SetAttributes(
                    normalizedPath,
                    attributeTag.FileAttributes & ~FileAttributes.ReadOnly);
            }

            MarkOpenedObjectForDeletion(handle, authorityName);
        }

        return true;
    }

    private static FileAttributeTag GetAttributeTag(
        SafeFileHandle handle,
        string authorityName)
    {
        if (!GetFileInformationByHandleEx(
                handle,
                FileAttributeTagInfo,
                out FileAttributeTag attributeTag,
                (uint)Marshal.SizeOf<FileAttributeTag>()))
        {
            throw CreateIoException(
                $"Could not inspect {authorityName} opened object.",
                Marshal.GetLastWin32Error());
        }

        return attributeTag;
    }

    private static void EnsureRegularFileHasSingleLink(
        SafeFileHandle handle,
        string authorityName)
    {
        var standardInfo = GetStandardInfo(handle, authorityName);

        if (!standardInfo.Directory && standardInfo.NumberOfLinks != 1)
        {
            throw new InvalidDataException(
                $"{authorityName} regular file must have exactly one physical link.");
        }
    }

    private static void EnsureOpenedObjectKind(
        SafeFileHandle handle,
        bool expectedDirectory,
        string authorityName)
    {
        var standardInfo = GetStandardInfo(handle, authorityName);
        if (standardInfo.Directory != expectedDirectory)
        {
            throw new InvalidDataException(
                expectedDirectory
                    ? $"{authorityName} source is not a physical directory."
                    : $"{authorityName} source is not a regular file.");
        }
    }

    private static FileStandardInfo GetStandardInfo(
        SafeFileHandle handle,
        string authorityName)
    {
        if (!GetFileInformationByHandleEx(
                handle,
                FileStandardInfoClass,
                out FileStandardInfo standardInfo,
                (uint)Marshal.SizeOf<FileStandardInfo>()))
        {
            throw CreateIoException(
                $"Could not inspect {authorityName} file authority.",
                Marshal.GetLastWin32Error());
        }

        return standardInfo;
    }

    private static void DeleteTreeFallback(string path)
    {
        var attributes = File.GetAttributes(path);
        if ((attributes & FileAttributes.ReparsePoint) != 0)
        {
            if ((attributes & FileAttributes.Directory) != 0)
                Directory.Delete(path, recursive: false);
            else
                File.Delete(path);
            return;
        }

        foreach (var child in Directory.EnumerateFileSystemEntries(
                     path,
                     "*",
                     SearchOption.TopDirectoryOnly))
        {
            var childAttributes = File.GetAttributes(child);
            if ((childAttributes & FileAttributes.Directory) != 0 &&
                (childAttributes & FileAttributes.ReparsePoint) == 0)
            {
                DeleteTreeFallback(child);
            }
            else if ((childAttributes & FileAttributes.Directory) != 0)
            {
                Directory.Delete(child, recursive: false);
            }
            else
            {
                if ((childAttributes & FileAttributes.ReadOnly) != 0)
                {
                    File.SetAttributes(
                        child,
                        childAttributes & ~FileAttributes.ReadOnly);
                }

                File.Delete(child);
            }
        }

        if ((attributes & FileAttributes.ReadOnly) != 0)
            File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
        Directory.Delete(path, recursive: false);
    }

    private static void EnsureDirectChild(
        StableDirectory parent,
        string expectedPath,
        string authorityName)
    {
        var normalizedParent = Path.GetFullPath(
            Path.GetDirectoryName(expectedPath)
            ?? throw new InvalidDataException(
                $"{authorityName} file has no parent directory."));
        if (!PathsEqual(parent.FullPath, normalizedParent))
        {
            throw new InvalidDataException(
                $"{authorityName} operation does not match its directory authority.");
        }
    }

    private static bool IsSameOrDescendant(
        string candidatePath,
        string rootPath)
    {
        if (PathsEqual(candidatePath, rootPath))
            return true;
        var rootWithSeparator = rootPath.EndsWith(Path.DirectorySeparatorChar) ||
                                rootPath.EndsWith(Path.AltDirectorySeparatorChar)
            ? rootPath
            : rootPath + Path.DirectorySeparatorChar;
        return candidatePath.StartsWith(
            rootWithSeparator,
            OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal);
    }

    private static bool PathsEqual(string left, string right) =>
        string.Equals(
            Path.GetFullPath(left).TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar),
            Path.GetFullPath(right).TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar),
            OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal);

    private static string ToWindowsExtendedPath(string path)
    {
        var normalized = Path.GetFullPath(path);
        if (normalized.StartsWith(@"\\?\", StringComparison.Ordinal))
            return normalized;
        if (normalized.StartsWith(@"\\", StringComparison.Ordinal))
            return @"\\?\UNC\" + normalized[2..];
        return @"\\?\" + normalized;
    }

    private static Exception CreateIoException(
        string message,
        int error) =>
        error == ErrorAccessDenied
            ? new UnauthorizedAccessException(message, new Win32Exception(error))
            : new IOException(message, new Win32Exception(error));

    [StructLayout(LayoutKind.Sequential)]
    private struct FileDisposition
    {
        [MarshalAs(UnmanagedType.Bool)]
        internal bool DeleteFile;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FileAttributeTag
    {
        internal FileAttributes FileAttributes;
        internal uint ReparseTag;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FileStandardInfo
    {
        internal long AllocationSize;
        internal long EndOfFile;
        internal uint NumberOfLinks;

        [MarshalAs(UnmanagedType.U1)]
        internal bool DeletePending;

        [MarshalAs(UnmanagedType.U1)]
        internal bool Directory;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetFileInformationByHandle(
        SafeFileHandle hFile,
        int fileInformationClass,
        IntPtr lpFileInformation,
        uint dwBufferSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetFileInformationByHandle(
        SafeFileHandle hFile,
        int fileInformationClass,
        ref FileDisposition lpFileInformation,
        uint dwBufferSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetFileInformationByHandleEx(
        SafeFileHandle hFile,
        int fileInformationClass,
        out FileAttributeTag lpFileInformation,
        uint dwBufferSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetFileInformationByHandleEx(
        SafeFileHandle hFile,
        int fileInformationClass,
        out FileStandardInfo lpFileInformation,
        uint dwBufferSize);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetFinalPathNameByHandle(
        SafeFileHandle hFile,
        StringBuilder lpszFilePath,
        uint cchFilePath,
        uint dwFlags);
}
