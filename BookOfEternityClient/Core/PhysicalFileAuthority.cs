using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
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
    private const int NtFileRenameInformation = 10;
    private const int FileDispositionInfo = 4;
    private const int FileStandardInfoClass = 1;
    private const int FileIdInfoClass = 18;
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

        internal string FullPath { get; private set; }
        internal SafeFileHandle? Handle => _handle;

        internal void RebindFullPathAfterRename(
            string expectedPath,
            string authorityName)
        {
            var normalizedPath = Path.GetFullPath(expectedPath);
            if (OperatingSystem.IsWindows())
            {
                var handle = _handle ??
                    throw new ObjectDisposedException(nameof(StableDirectory));
                EnsureHandlePathMatchesExpectedPath(
                    handle,
                    normalizedPath,
                    authorityName);
            }

            FullPath = normalizedPath;
        }

        public void Dispose()
        {
            var handle = _handle;
            _handle = null;
            handle?.Dispose();
        }
    }

    internal sealed record FileIdentity(
        ulong VolumeSerialNumber,
        ulong FileIdLow,
        ulong FileIdHigh,
        bool IsDirectory,
        uint NumberOfLinks);

    internal enum NamespaceEntryKind
    {
        Missing,
        RegularFile,
        Directory,
        ReparsePoint
    }

    internal static NamespaceEntryKind ProbeNamespaceEntry(
        StableDirectory parent,
        string expectedPath,
        string authorityName)
    {
        EnsureDirectChild(parent, expectedPath, authorityName);
        var normalizedPath = Path.GetFullPath(expectedPath);
        if (!OperatingSystem.IsWindows())
        {
            try
            {
                var attributes = File.GetAttributes(normalizedPath);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                    return NamespaceEntryKind.ReparsePoint;
                return (attributes & FileAttributes.Directory) != 0
                    ? NamespaceEntryKind.Directory
                    : NamespaceEntryKind.RegularFile;
            }
            catch (FileNotFoundException)
            {
                return NamespaceEntryKind.Missing;
            }
            catch (DirectoryNotFoundException)
            {
                return NamespaceEntryKind.Missing;
            }
        }

        var handle = CreateFile(
            ToWindowsExtendedPath(normalizedPath),
            FileReadAttributes | SynchronizeAccess,
            FileShareRead | FileShareWrite,
            IntPtr.Zero,
            OpenExisting,
            FileFlagOpenReparsePoint | FileFlagBackupSemantics,
            IntPtr.Zero);
        if (handle.IsInvalid)
        {
            var error = Marshal.GetLastWin32Error();
            handle.Dispose();
            if (error is ErrorFileNotFound or ErrorPathNotFound)
                return NamespaceEntryKind.Missing;
            throw CreateIoException(
                $"Could not inspect {authorityName} namespace entry.",
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
            if ((attributeTag.FileAttributes & FileAttributes.ReparsePoint) != 0)
                return NamespaceEntryKind.ReparsePoint;
            return (attributeTag.FileAttributes & FileAttributes.Directory) != 0
                ? NamespaceEntryKind.Directory
                : NamespaceEntryKind.RegularFile;
        }
    }

    internal static NamespaceEntryKind ProbeNamespaceEntryFromRoot(
        string rootPath,
        string expectedPath,
        string authorityName)
    {
        var root = Path.GetFullPath(rootPath).TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);
        var expected = Path.GetFullPath(expectedPath);
        if (!IsSameOrDescendant(expected, root) ||
            PathsEqual(expected, root))
        {
            throw new InvalidDataException(
                $"{authorityName} entry is outside its physical root.");
        }

        var segments = Path.GetRelativePath(root, expected).Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0 ||
            segments.Any(segment => segment is "." or ".."))
        {
            throw new InvalidDataException(
                $"{authorityName} namespace path is invalid.");
        }

        StableDirectory? current = null;
        try
        {
            var rootParentPath = Path.GetDirectoryName(root);
            if (rootParentPath == null)
            {
                throw new InvalidDataException(
                    $"{authorityName} physical root has no parent directory.");
            }

            using (var rootParent = OpenStableDirectory(
                       rootParentPath,
                       authorityName + " root parent"))
            {
                var rootKind = ProbeNamespaceEntry(
                    rootParent,
                    root,
                    authorityName + " root");
                if (rootKind == NamespaceEntryKind.Missing)
                    return NamespaceEntryKind.Missing;
                if (rootKind != NamespaceEntryKind.Directory)
                {
                    throw new InvalidDataException(
                        $"{authorityName} root is not a physical directory.");
                }

                try
                {
                    current = OpenStableDirectory(root, authorityName);
                }
                catch (DirectoryNotFoundException)
                {
                    rootKind = ProbeNamespaceEntry(
                        rootParent,
                        root,
                        authorityName + " root");
                    if (rootKind == NamespaceEntryKind.Missing)
                        return NamespaceEntryKind.Missing;
                    throw;
                }
            }

            for (var index = 0; index < segments.Length - 1; index++)
            {
                var childPath = Path.Combine(current.FullPath, segments[index]);
                var childKind = ProbeNamespaceEntry(
                    current,
                    childPath,
                    authorityName + " parent");
                if (childKind == NamespaceEntryKind.Missing)
                    return NamespaceEntryKind.Missing;
                if (childKind != NamespaceEntryKind.Directory)
                {
                    throw new InvalidDataException(
                        $"{authorityName} parent is not a physical directory.");
                }

                var child = OpenStableDirectory(
                    childPath,
                    authorityName + " parent");
                current.Dispose();
                current = child;
            }

            return ProbeNamespaceEntry(
                current,
                expected,
                authorityName);
        }
        finally
        {
            current?.Dispose();
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
        string authorityName,
        bool allowRename = false)
    {
        var normalizedPath = Path.GetFullPath(expectedPath);
        if (!Directory.Exists(normalizedPath))
        {
            throw new DirectoryNotFoundException(
                $"{authorityName} directory does not exist: {normalizedPath}");
        }

        if (!OperatingSystem.IsWindows())
            return new StableDirectory(normalizedPath, handle: null);

        var access = FileListDirectory | SynchronizeAccess;
        if (allowRename)
            access |= DeleteAccess;
        var handle = CreateFile(
            ToWindowsExtendedPath(normalizedPath),
            access,
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
            EnsureRegularFileHandleMatchesExpectedPath(
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
        string authorityName,
        bool writable = false)
    {
        EnsureDirectChild(parent, expectedPath, authorityName);
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "Opened-handle rename is available only on Windows.");
        }

        var normalizedPath = Path.GetFullPath(expectedPath);
        var access = DeleteAccess | SynchronizeAccess | FileReadAttributes;
        if (!isDirectory)
            access |= GenericRead;
        if (!isDirectory && writable)
            access |= GenericWrite;
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

    internal static void RenameOpenedObjectRelative(
        SafeFileHandle sourceHandle,
        StableDirectory destinationParent,
        string destinationPath,
        bool replaceExisting,
        string authorityName,
        bool requireSingleLink = true)
    {
        ArgumentNullException.ThrowIfNull(destinationParent);
        EnsureDirectChild(
            destinationParent,
            destinationPath,
            authorityName);
        if (!OperatingSystem.IsWindows() ||
            destinationParent.Handle is not { IsInvalid: false } parentHandle)
        {
            throw new PlatformNotSupportedException(
                "Retained-directory relative rename is available only on Windows.");
        }

        if (requireSingleLink)
            EnsureRegularFileHasSingleLink(sourceHandle, authorityName);
        var normalizedDestination = Path.GetFullPath(destinationPath);
        var fileNameBytes = Encoding.Unicode.GetBytes(
            Path.GetFileName(normalizedDestination));
        var rootDirectoryOffset = IntPtr.Size == 8 ? 8 : 4;
        var fileNameLengthOffset = rootDirectoryOffset + IntPtr.Size;
        var fileNameOffset = fileNameLengthOffset + sizeof(int);
        var unalignedBufferSize = checked(
            fileNameOffset + fileNameBytes.Length);
        var bufferSize = checked(
            (unalignedBufferSize + IntPtr.Size - 1) &
            ~(IntPtr.Size - 1));
        var buffer = Marshal.AllocHGlobal(bufferSize);
        try
        {
            for (var index = 0; index < bufferSize; index++)
                Marshal.WriteByte(buffer, index, 0);
            Marshal.WriteByte(
                buffer,
                0,
                replaceExisting ? (byte)1 : (byte)0);
            Marshal.WriteIntPtr(
                buffer,
                rootDirectoryOffset,
                parentHandle.DangerousGetHandle());
            Marshal.WriteInt32(
                buffer,
                fileNameLengthOffset,
                fileNameBytes.Length);
            Marshal.Copy(
                fileNameBytes,
                0,
                IntPtr.Add(buffer, fileNameOffset),
                fileNameBytes.Length);

            var status = NtSetInformationFile(
                sourceHandle,
                out _,
                buffer,
                (uint)bufferSize,
                NtFileRenameInformation);
            if (status != 0)
            {
                var error = unchecked((int)RtlNtStatusToDosError(status));
                throw CreateIoException(
                    $"Could not publish {authorityName} opened object.",
                    error);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }

        if (requireSingleLink)
        {
            EnsureHandleMatchesExpectedPath(
                sourceHandle,
                normalizedDestination,
                authorityName);
        }
        else
        {
            EnsureHandlePathMatchesExpectedPath(
                sourceHandle,
                normalizedDestination,
                authorityName);
        }
    }

    internal static FileIdentity CaptureFileIdentity(
        SafeFileHandle handle,
        string authorityName)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "Opened-file identity capture is available only on Windows.");
        }

        var standardInfo = GetStandardInfo(handle, authorityName);
        if (!GetFileInformationByHandleEx(
                handle,
                FileIdInfoClass,
                out FileIdInfo fileIdInfo,
                (uint)Marshal.SizeOf<FileIdInfo>()))
        {
            throw CreateIoException(
                $"Could not inspect {authorityName} file identity.",
                Marshal.GetLastWin32Error());
        }

        return new FileIdentity(
            fileIdInfo.VolumeSerialNumber,
            fileIdInfo.FileId.Low,
            fileIdInfo.FileId.High,
            standardInfo.Directory,
            standardInfo.NumberOfLinks);
    }

    internal static FileIdentity EnsureExactFileIdentity(
        SafeFileHandle handle,
        string expectedPath,
        FileIdentity expectedIdentity,
        string authorityName,
        bool requireSingleLink = true)
    {
        if (requireSingleLink)
        {
            EnsureHandleMatchesExpectedPath(
                handle,
                expectedPath,
                authorityName);
        }
        else
        {
            EnsureHandlePathMatchesExpectedPath(
                handle,
                expectedPath,
                authorityName);
        }
        var actual = CaptureFileIdentity(handle, authorityName);
        if (actual.VolumeSerialNumber != expectedIdentity.VolumeSerialNumber ||
            actual.FileIdLow != expectedIdentity.FileIdLow ||
            actual.FileIdHigh != expectedIdentity.FileIdHigh ||
            actual.IsDirectory != expectedIdentity.IsDirectory)
        {
            throw new InvalidDataException(
                $"{authorityName} physical identity changed.");
        }

        if (actual.IsDirectory)
        {
            throw new InvalidDataException(
                $"{authorityName} authority is not a regular file.");
        }

        if (requireSingleLink && actual.NumberOfLinks != 1)
        {
            throw new InvalidDataException(
                $"{authorityName} regular file must have exactly one physical link.");
        }

        return actual;
    }

    internal static FileIdentity EnsureExactDirectoryIdentity(
        StableDirectory directory,
        FileIdentity expectedIdentity,
        string authorityName)
    {
        if (!OperatingSystem.IsWindows() ||
            directory.Handle is not { IsInvalid: false } handle)
        {
            throw new PlatformNotSupportedException(
                "Opened-directory identity validation is available only on Windows.");
        }

        EnsureHandlePathMatchesExpectedPath(
            handle,
            directory.FullPath,
            authorityName);
        var actual = CaptureFileIdentity(handle, authorityName);
        if (actual.VolumeSerialNumber != expectedIdentity.VolumeSerialNumber ||
            actual.FileIdLow != expectedIdentity.FileIdLow ||
            actual.FileIdHigh != expectedIdentity.FileIdHigh ||
            !actual.IsDirectory ||
            !expectedIdentity.IsDirectory ||
            actual.NumberOfLinks != expectedIdentity.NumberOfLinks)
        {
            throw new InvalidDataException(
                $"{authorityName} physical directory identity changed.");
        }

        return actual;
    }

    internal static byte[] ReadOpenedFileBytes(
        SafeFileHandle handle,
        string authorityName)
    {
        try
        {
            var length = RandomAccess.GetLength(handle);
            if (length > int.MaxValue)
            {
                throw new InvalidDataException(
                    $"{authorityName} exceeds the supported in-memory size.");
            }

            var content = new byte[(int)length];
            var offset = 0;
            while (offset < content.Length)
            {
                var read = RandomAccess.Read(
                    handle,
                    content.AsSpan(offset),
                    offset);
                if (read <= 0)
                {
                    throw new EndOfStreamException(
                        $"Could not read all bytes from {authorityName}.");
                }

                offset += read;
            }

            return content;
        }
        catch (Exception ex) when (
            ex is IOException or UnauthorizedAccessException)
        {
            throw new InvalidDataException(
                $"Could not read {authorityName} opened authority.",
                ex);
        }
    }

    internal static void WriteOpenedFileBytes(
        SafeFileHandle handle,
        ReadOnlySpan<byte> content,
        string authorityName)
    {
        try
        {
            RandomAccess.SetLength(handle, content.Length);
            var offset = 0;
            while (offset < content.Length)
            {
                RandomAccess.Write(
                    handle,
                    content[offset..],
                    offset);
                offset = content.Length;
            }

            RandomAccess.FlushToDisk(handle);
        }
        catch (Exception ex) when (
            ex is IOException or UnauthorizedAccessException)
        {
            throw new InvalidDataException(
                $"Could not restore {authorityName} opened authority.",
                ex);
        }
    }

    internal static string ComputeOpenedFileSha256(
        SafeFileHandle handle,
        string authorityName)
    {
        try
        {
            var length = RandomAccess.GetLength(handle);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[64 * 1024];
            long offset = 0;
            while (offset < length)
            {
                var read = RandomAccess.Read(
                    handle,
                    buffer,
                    offset);
                if (read <= 0)
                {
                    throw new EndOfStreamException(
                        $"Could not read all bytes from {authorityName}.");
                }

                hash.AppendData(buffer, 0, read);
                offset += read;
            }

            return Convert.ToHexString(hash.GetHashAndReset())
                .ToLowerInvariant();
        }
        catch (Exception ex) when (
            ex is IOException or UnauthorizedAccessException)
        {
            throw new InvalidDataException(
                $"Could not hash {authorityName} opened authority.",
                ex);
        }
    }

    internal static void DeleteOpenedFile(
        SafeFileHandle handle,
        string authorityName)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "Opened-file deletion is available only on Windows.");
        }

        EnsureOpenedObjectKind(
            handle,
            expectedDirectory: false,
            authorityName);
        MarkOpenedObjectForDeletion(handle, authorityName);
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

    internal static bool TryDeleteDirectoryTree(
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
            if (!File.Exists(normalizedPath) &&
                !Directory.Exists(normalizedPath))
            {
                return false;
            }

            var attributes = File.GetAttributes(normalizedPath);
            if ((attributes & FileAttributes.Directory) == 0 ||
                (attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidDataException(
                    $"{authorityName} target is not a physical directory.");
            }

            DeleteTreeFallback(normalizedPath);
            return true;
        }

        using var parent = OpenStableDirectory(parentPath, authorityName);
        return TryDeleteEntry(
            parent,
            normalizedPath,
            authorityName,
            requirePhysicalDirectory: true);
    }

    internal static bool TryDeleteDirectoryTree(
        StableDirectory parent,
        string expectedPath,
        string authorityName)
    {
        ArgumentNullException.ThrowIfNull(parent);
        return TryDeleteEntry(
            parent,
            Path.GetFullPath(expectedPath),
            authorityName,
            requirePhysicalDirectory: true);
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

        EnsureHandlePathMatchesExpectedPath(
            handle,
            expectedPath,
            authorityName,
            flags);
        EnsureRegularFileHasSingleLink(handle, authorityName);
    }

    internal static void EnsureHandlePathMatchesExpectedPath(
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
    }

    internal static void EnsureRegularFileHandleMatchesExpectedPath(
        SafeFileHandle handle,
        string expectedPath,
        string authorityName,
        uint flags = 0)
    {
        EnsureHandleMatchesExpectedPath(
            handle,
            expectedPath,
            authorityName,
            flags);
        if (OperatingSystem.IsWindows())
        {
            EnsureOpenedObjectKind(
                handle,
                expectedDirectory: false,
                authorityName);
        }
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
        string authorityName,
        bool requirePhysicalDirectory = false)
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
            if (requirePhysicalDirectory &&
                (!isDirectory || isReparsePoint))
            {
                throw new InvalidDataException(
                    $"{authorityName} target is not a physical directory.");
            }

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

    [StructLayout(LayoutKind.Sequential)]
    private struct FileId128
    {
        internal ulong Low;
        internal ulong High;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FileIdInfo
    {
        internal ulong VolumeSerialNumber;
        internal FileId128 FileId;
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

    [DllImport("ntdll.dll")]
    private static extern int NtSetInformationFile(
        SafeFileHandle fileHandle,
        out IoStatusBlock ioStatusBlock,
        IntPtr fileInformation,
        uint length,
        int fileInformationClass);

    [DllImport("ntdll.dll")]
    private static extern uint RtlNtStatusToDosError(int status);

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

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandleEx(
        SafeFileHandle hFile,
        int fileInformationClass,
        out FileIdInfo lpFileInformation,
        uint dwBufferSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct IoStatusBlock
    {
        internal IntPtr Status;
        internal nuint Information;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetFinalPathNameByHandle(
        SafeFileHandle hFile,
        StringBuilder lpszFilePath,
        uint cchFilePath,
        uint dwFlags);
}
