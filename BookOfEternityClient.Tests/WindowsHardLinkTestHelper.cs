using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace BookOfEternityClient.Tests;

internal static class WindowsHardLinkTestHelper
{
    internal sealed record FileIdentity(
        ulong VolumeSerialNumber,
        ulong FileIdLow,
        ulong FileIdHigh,
        bool IsDirectory,
        uint NumberOfLinks);

    internal static void Create(string linkPath, string existingPath)
    {
        if (CreateHardLinkNative(linkPath, existingPath, IntPtr.Zero))
            return;

        throw new Win32Exception(
            Marshal.GetLastWin32Error(),
            $"Could not create hard link '{linkPath}'.");
    }

    internal static FileIdentity CaptureIdentity(string path)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        if (!GetFileInformationByHandleEx(
                stream.SafeFileHandle,
                18,
                out FileIdInfo fileId) ||
            !GetFileInformationByHandleEx(
                stream.SafeFileHandle,
                1,
                out FileStandardInfo standard))
        {
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                $"Could not inspect file identity for '{path}'.");
        }

        return new FileIdentity(
            fileId.VolumeSerialNumber,
            fileId.FileId.Low,
            fileId.FileId.High,
            standard.Directory,
            standard.NumberOfLinks);
    }

    [DllImport(
        "kernel32.dll",
        EntryPoint = "CreateHardLinkW",
        CharSet = CharSet.Unicode,
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLinkNative(
        string lpFileName,
        string lpExistingFileName,
        IntPtr lpSecurityAttributes);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandleEx(
        SafeFileHandle hFile,
        int fileInformationClass,
        out FileIdInfo lpFileInformation,
        uint dwBufferSize = 24);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandleEx(
        SafeFileHandle hFile,
        int fileInformationClass,
        out FileStandardInfo lpFileInformation,
        uint dwBufferSize = 24);

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
}
