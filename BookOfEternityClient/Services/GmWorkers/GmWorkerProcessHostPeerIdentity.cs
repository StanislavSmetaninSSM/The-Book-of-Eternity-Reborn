using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace BookOfEternityClient.Services.GmWorkers;

internal readonly record struct GmWorkerLinuxPeerQueryResult(
    int ReturnCode, int Error, uint Length, int ProcessId, uint EffectiveUserId);

internal static class GmWorkerProcessHostPeerIdentity
{
    private const int LinuxSocketLevel = 1;
    private const int LinuxPeerCredentialsOption = 17;
    private const uint LinuxCredentialsLength = 12;

    internal static uint CaptureEffectiveUserId()
    {
        if (OperatingSystem.IsLinux()) return NativeMethods.GetEffectiveUserId();
        if (OperatingSystem.IsWindows()) return 0; // Windows uses CurrentUserOnly and the client PID.
        throw new PlatformNotSupportedException("Worker process host peer authentication is unsupported on this platform.");
    }

    internal static void Validate(SafeHandle pipe, int expectedProcessId, uint expectedUserId, string channelName)
    {
        if (OperatingSystem.IsWindows())
        {
            if (!NativeMethods.GetNamedPipeClientProcessId(pipe, out var processId))
                throw IdentityReadFailure(Marshal.GetLastPInvokeError(), channelName);
            if (expectedProcessId <= 0 || processId != (uint)expectedProcessId)
                throw UnexpectedProcess(channelName);
            return;
        }
        if (!OperatingSystem.IsLinux())
            throw new PlatformNotSupportedException("Worker process host peer authentication is unsupported on this platform.");

        // SafeHandle marshalling pins the pipe handle (and its retained Unix socket)
        // across getsockopt. Do not wrap this descriptor in another owning Socket.
        var length = (uint)Marshal.SizeOf<LinuxCredentials>();
        var result = NativeMethods.GetSocketOption(pipe, LinuxSocketLevel, LinuxPeerCredentialsOption,
            out var credentials, ref length);
        var error = Marshal.GetLastPInvokeError();
        ValidateLinuxResult(new(result, error, length, credentials.ProcessId, credentials.UserId),
            expectedProcessId, expectedUserId, channelName);
    }

    internal static void ValidateLinuxResult(GmWorkerLinuxPeerQueryResult result,
        int expectedProcessId, uint expectedUserId, string channelName)
    {
        if (result.ReturnCode != 0) throw IdentityReadFailure(result.Error, channelName);
        if (result.Length != LinuxCredentialsLength)
            throw new InvalidDataException($"Worker process host {channelName} channel returned an invalid peer identity length.");
        if (expectedProcessId <= 0 || result.ProcessId <= 0 || result.ProcessId != expectedProcessId)
            throw UnexpectedProcess(channelName);
        if (result.EffectiveUserId != expectedUserId)
            throw new InvalidDataException($"Worker process host {channelName} channel was connected by an unexpected user.");
    }

    private static Win32Exception IdentityReadFailure(int error, string channelName) =>
        new(error, $"Worker process host {channelName} channel client identity could not be read.");

    private static InvalidDataException UnexpectedProcess(string channelName) =>
        new($"Worker process host {channelName} channel was connected by an unexpected process.");

    [StructLayout(LayoutKind.Sequential)]
    private struct LinuxCredentials
    {
        internal int ProcessId;
        internal uint UserId;
        internal uint GroupId;
    }

    private static class NativeMethods
    {
        [DllImport("libc", EntryPoint = "geteuid")]
        internal static extern uint GetEffectiveUserId();

        [DllImport("libc", EntryPoint = "getsockopt", SetLastError = true)]
        internal static extern int GetSocketOption(SafeHandle pipe, int level, int option,
            out LinuxCredentials credentials, ref uint length);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetNamedPipeClientProcessId(SafeHandle pipe, out uint clientProcessId);
    }
}
