using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace BookOfEternityClient.Services.GmWorkers;

// Construction is limited to the original owned Process or a checked transferred
// pidfd while its direct-child incarnation remains unreaped behind the exec gate.
internal sealed class GmWorkerHostIdentity
{
    private readonly Process _owner;
    private readonly SafeFileHandle? _pidfd;
    private readonly Func<bool>? _authorityValid;
    private GmWorkerHostIdentity(Process owner, int pid, SafeFileHandle? pidfd, Func<bool>? authorityValid)
    { _owner = owner; ProcessId = pid; _pidfd = pidfd; _authorityValid = authorityValid; UserId = GmWorkerProcessHostPeerIdentity.CaptureEffectiveUserId(); }

    internal int ProcessId { get; }
    internal uint UserId { get; }
    internal static GmWorkerHostIdentity FromOwnedProcess(Process process) => new(process, process.Id, null, null);

    internal static GmWorkerHostIdentity FromTransferredPidfd(Process supervisor, SafeFileHandle descriptor, Func<bool> authorityValid)
    {
        if (supervisor.HasExited || Signal(descriptor, 0, IntPtr.Zero, 0) != 0 || IsReadable(descriptor)) throw Invalid();
        var fd = descriptor.DangerousGetHandle().ToInt32();
        var info = File.ReadAllLines($"/proc/self/fdinfo/{fd}");
        int ReadOne(string name)
        {
            var lines = info.Where(l => l.StartsWith(name + ":", StringComparison.Ordinal)).ToArray();
            if (lines.Length != 1 || !int.TryParse(lines[0][(name.Length + 1)..].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var pid) || pid <= 0) throw Invalid();
            return pid;
        }
        var expected = ReadOne("Pid");
        if (ReadOne("NSpid") != expected || expected == supervisor.Id) throw Invalid();
        var stat = File.ReadAllText($"/proc/{expected}/stat");
        var opening = stat.IndexOf(" (", StringComparison.Ordinal); var closing = stat.LastIndexOf(") ", StringComparison.Ordinal);
        if (opening <= 0 || closing <= opening || !int.TryParse(stat[..opening], out var statPid) || statPid != expected) throw Invalid();
        var rest = stat[(closing + 2)..].Split(' ', 3);
        if (rest.Length != 3 || !int.TryParse(rest[1], out var parent) || parent != supervisor.Id) throw Invalid();
        var identity = new GmWorkerHostIdentity(supervisor, expected, descriptor, authorityValid);
        identity.EnsureLive(); return identity;
    }

    internal void EnsureLive()
    {
        if (_pidfd == null && _owner.HasExited)
            throw new InvalidOperationException("Worker process host exited before launch admission.");
        if (_owner.HasExited || (_authorityValid != null && !_authorityValid()) || (_pidfd != null && IsReadable(_pidfd))) throw Invalid();
    }
    internal string ExitDescription => _pidfd == null && _owner.HasExited ? $" with code {_owner.ExitCode}" : "";

    internal static bool IsReadable(SafeFileHandle handle)
    {
        var held = false;
        try
        {
            handle.DangerousAddRef(ref held);
            var poll = new PollDescriptor { Descriptor = handle.DangerousGetHandle().ToInt32(), Events = 1 };
            var result = Poll(ref poll, 1, 0);
            if (result < 0 || (poll.ReturnedEvents & ~1) != 0) throw Invalid();
            return result > 0;
        }
        finally { if (held) handle.DangerousRelease(); }
    }
    private static InvalidDataException Invalid() => new("Owned host identity or supervisor authority is unavailable.");
    [StructLayout(LayoutKind.Sequential)]
    private struct PollDescriptor { internal int Descriptor; internal short Events; internal short ReturnedEvents; }
    [DllImport("libc", EntryPoint = "poll", SetLastError = true)]
    private static extern int Poll(ref PollDescriptor descriptors, nuint count, int timeout);
    [DllImport("libc", EntryPoint = "pidfd_send_signal", SetLastError = true)]
    private static extern int Signal(SafeFileHandle pidfd, int signal, IntPtr info, uint flags);
}
