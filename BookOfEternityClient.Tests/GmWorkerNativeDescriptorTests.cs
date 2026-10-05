using System.Diagnostics;
using System.IO.Pipes;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using BookOfEternityClient.Services.GmWorkers;
using Microsoft.Win32.SafeHandles;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class GmWorkerNativeDescriptorTests
{
    private const string Envelope = "B2:fixture-run";

    [Fact]
    public async Task SupervisorPidfd_CannotBecomeHostIdentity()
    {
        await WithSockets(async (sender, receiver, _, _) =>
        {
            using var supervisor = Process.GetCurrentProcess();
            using var original = OpenPidfd(supervisor.Id, 0);
            Assert.False(original.IsInvalid);
            Assert.False(GmWorkerHostIdentity.IsReadable(original));
            GmWorkerNativeDescriptors.Send(sender, Envelope, original);
            using var transferred = Assert.Single(await GmWorkerNativeDescriptors.ReceiveAsync(receiver, Envelope, 1, CancellationToken.None));
            Assert.Throws<InvalidDataException>(() => GmWorkerHostIdentity.FromTransferredPidfd(supervisor, transferred, () => true));
            Assert.True(transferred.IsClosed);
            Assert.False(original.IsClosed);
            Assert.False(GmWorkerHostIdentity.IsReadable(original));
        });
    }

    [Fact]
    public async Task ReceivedRight_IsCloseOnExecAndHasOneDeterministicOwner()
    {
        await WithSockets(async (sender, receiver, pipe, target) =>
        {
            var before = CountTarget(target);
            GmWorkerNativeDescriptors.Send(sender, Envelope, pipe.ClientSafePipeHandle);
            var received = await GmWorkerNativeDescriptors.ReceiveAsync(receiver, Envelope, 1, CancellationToken.None);
            using (var descriptor = Assert.Single(received))
            {
                Assert.Equal(1, GetDescriptorFlags(descriptor, 1) & 1);
                Assert.Equal(before + 1, CountTarget(target));
            }
            Assert.Equal(before, CountTarget(target));
        });
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("extra")]
    [InlineData("wrong-run")]
    [InlineData("payload-truncated")]
    [InlineData("control-truncated")]
    public async Task RejectedEnvelope_ClosesEveryDeliveredDescriptor(string mode)
    {
        await WithSockets(async (sender, receiver, pipe, target) =>
        {
            var before = CountTarget(target);
            if (mode == "missing") GmWorkerNativeDescriptors.Send(sender, Envelope);
            else if (mode == "extra") GmWorkerNativeDescriptors.Send(sender, Envelope, pipe.ClientSafePipeHandle, pipe.ClientSafePipeHandle);
            else if (mode == "wrong-run") GmWorkerNativeDescriptors.Send(sender, "B2:another-run", pipe.ClientSafePipeHandle);
            else RawSend(sender, pipe.ClientSafePipeHandle, mode == "control-truncated" ? 9 : 1,
                mode == "payload-truncated" ? new string('x', 100) : Envelope);
            await Assert.ThrowsAsync<InvalidDataException>(() =>
                GmWorkerNativeDescriptors.ReceiveAsync(receiver, Envelope, 1, CancellationToken.None));
            Assert.Equal(before, CountTarget(target));
        });
    }

    [Fact]
    public async Task IdentityRejection_ClosesUnadmittedTransferredDescriptor()
    {
        await WithSockets(async (sender, receiver, pipe, target) =>
        {
            var before = CountTarget(target);
            GmWorkerNativeDescriptors.Send(sender, Envelope, pipe.ClientSafePipeHandle);
            var received = await GmWorkerNativeDescriptors.ReceiveAsync(receiver, Envelope, 1, CancellationToken.None);
            using var descriptor = Assert.Single(received);
            using var current = Process.GetCurrentProcess();
            Assert.Throws<InvalidDataException>(() => GmWorkerHostIdentity.FromTransferredPidfd(current, descriptor, () => true));
            Assert.True(descriptor.IsClosed, "Rejected transfer must close before it can become retained host authority.");
            Assert.Equal(before, CountTarget(target));
        });
    }

    private static async Task WithSockets(Func<Socket, Socket, AnonymousPipeServerStream, string, Task> action)
    {
        Assert.True(OperatingSystem.IsLinux() && RuntimeInformation.ProcessArchitecture == Architecture.X64);
        var root = Path.Combine(Path.GetTempPath(), "boe-fd-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        try
        {
            var endpoint = new UnixDomainSocketEndPoint(Path.Combine(root, "s"));
            using var listener = new Socket(AddressFamily.Unix, SocketType.Seqpacket, ProtocolType.Unspecified);
            listener.Bind(endpoint); listener.Listen(1);
            using var sender = new Socket(AddressFamily.Unix, SocketType.Seqpacket, ProtocolType.Unspecified);
            await sender.ConnectAsync(endpoint);
            using var receiver = await listener.AcceptAsync();
            using var pipe = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.None);
            var target = new FileInfo($"/proc/self/fd/{pipe.ClientSafePipeHandle.DangerousGetHandle().ToInt32()}").LinkTarget!;
            Assert.StartsWith("pipe:[", target);
            await action(sender, receiver, pipe, target);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    private static int CountTarget(string target)
    {
        var count = 0;
        foreach (var path in Directory.EnumerateFileSystemEntries("/proc/self/fd"))
        {
            try { if (new FileInfo(path).LinkTarget == target) count++; }
            catch (FileNotFoundException) { }
        }
        return count;
    }

    // Independent malformed sender bypasses only the production sender's bounds;
    // the kernel still constructs the actual SCM_RIGHTS records received by code.
    private static void RawSend(Socket socket, SafeHandle descriptor, int count, string text)
    {
        var data = Encoding.ASCII.GetBytes(text);
        var control = new byte[(16 + count * 4 + 7) & ~7];
        var held = false;
        var dataPin = GCHandle.Alloc(data, GCHandleType.Pinned);
        var controlPin = GCHandle.Alloc(control, GCHandleType.Pinned);
        var vector = Marshal.AllocHGlobal(16);
        try
        {
            descriptor.DangerousAddRef(ref held);
            BitConverter.GetBytes((ulong)(16 + count * 4)).CopyTo(control, 0);
            BitConverter.GetBytes(1).CopyTo(control, 8); BitConverter.GetBytes(1).CopyTo(control, 12);
            for (var i = 0; i < count; i++) BitConverter.GetBytes(descriptor.DangerousGetHandle().ToInt32()).CopyTo(control, 16 + i * 4);
            Marshal.WriteIntPtr(vector, dataPin.AddrOfPinnedObject()); Marshal.WriteInt64(vector, 8, data.Length);
            var message = new Message { Vectors = vector, VectorCount = 1, Control = controlPin.AddrOfPinnedObject(), ControlLength = (nuint)control.Length };
            Assert.Equal(data.Length, SendMessage(socket.SafeHandle, ref message, 0x4040));
        }
        finally
        {
            if (held) descriptor.DangerousRelease();
            Marshal.FreeHGlobal(vector); dataPin.Free(); controlPin.Free();
        }
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct Message
    {
        internal IntPtr Name; internal uint NameLength; internal IntPtr Vectors; internal nuint VectorCount;
        internal IntPtr Control; internal nuint ControlLength; internal int Flags;
    }
    [DllImport("libc", EntryPoint = "sendmsg", SetLastError = true)]
    private static extern long SendMessage(SafeSocketHandle socket, ref Message message, int flags);
    [DllImport("libc", EntryPoint = "fcntl", SetLastError = true)]
    private static extern int GetDescriptorFlags(SafeFileHandle handle, int command);
    [DllImport("libc", EntryPoint = "pidfd_open", SetLastError = true)]
    private static extern SafeFileHandle OpenPidfd(int pid, uint flags);
}
