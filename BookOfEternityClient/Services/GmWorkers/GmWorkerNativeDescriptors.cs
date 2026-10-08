using System.ComponentModel;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace BookOfEternityClient.Services.GmWorkers;

// Linux x64 ABI only, guarded by the package/platform preflight. SOCK_SEQPACKET
// keeps one bounded envelope and its rights together. Never accept extra rights.
internal static class GmWorkerNativeDescriptors
{
    [StructLayout(LayoutKind.Sequential)]
    private struct IoVector { internal IntPtr Address; internal nuint Length; }
    [StructLayout(LayoutKind.Sequential)]
    private struct Message
    {
        internal IntPtr Name; internal uint NameLength;
        internal IntPtr Vectors; internal nuint VectorCount;
        internal IntPtr Control; internal nuint ControlLength; internal int Flags;
    }

    internal static async Task<SafeFileHandle[]> ReceiveAsync(Socket socket, string expected, int expectedCount, CancellationToken token)
    {
        var bytes = new byte[80]; var control = new byte[48]; // at most eight descriptors
        while (true)
        {
            token.ThrowIfCancellationRequested();
            var n = Call(socket, bytes, control, false, 0x40000040, out var flags, out var controlLength);
            if (n < 0)
            {
                var error = Marshal.GetLastPInvokeError();
                if (error is 4 or 11) { await Task.Delay(10, token); continue; }
                throw new Win32Exception(error, "Native bootstrap receive failed.");
            }
            var handles = new List<SafeFileHandle>();
            try
            {
                var valid = (flags & (8 | 32)) == 0 && n > 0;
                for (var offset = 0; offset + 16 <= controlLength;)
                {
                    var length = checked((int)BitConverter.ToUInt64(control, offset));
                    if (length < 16 || length > controlLength - offset) { valid = false; break; }
                    if (BitConverter.ToInt32(control, offset + 8) != 1 || BitConverter.ToInt32(control, offset + 12) != 1 || (length - 16) % 4 != 0) valid = false;
                    else for (var i = offset + 16; i < offset + length; i += 4)
                        handles.Add(new SafeFileHandle((IntPtr)BitConverter.ToInt32(control, i), ownsHandle: true));
                    offset += (length + 7) & ~7;
                }
                if (!valid || handles.Count != expectedCount || n != Encoding.ASCII.GetByteCount(expected) ||
                    !bytes.AsSpan(0, (int)n).SequenceEqual(Encoding.ASCII.GetBytes(expected)))
                    throw new InvalidDataException("Native bootstrap envelope or descriptor count is invalid.");
                var result = handles.ToArray(); handles.Clear(); return result;
            }
            finally { foreach (var handle in handles) handle.Dispose(); }
        }
    }

    internal static void Send(Socket socket, string envelope, params SafeHandle[] descriptors)
    {
        var bytes = Encoding.ASCII.GetBytes(envelope);
        if (bytes.Length > 80 || descriptors.Length > 2) throw new InvalidDataException("Native bootstrap send exceeds bounds.");
        var control = descriptors.Length == 0 ? [] : new byte[(16 + descriptors.Length * 4 + 7) & ~7];
        var held = new bool[descriptors.Length];
        try
        {
            if (descriptors.Length > 0)
            {
                BitConverter.GetBytes((ulong)(16 + descriptors.Length * 4)).CopyTo(control, 0);
                BitConverter.GetBytes(1).CopyTo(control, 8); BitConverter.GetBytes(1).CopyTo(control, 12);
                for (var i = 0; i < descriptors.Length; i++)
                {
                    descriptors[i].DangerousAddRef(ref held[i]);
                    BitConverter.GetBytes(descriptors[i].DangerousGetHandle().ToInt32()).CopyTo(control, 16 + i * 4);
                }
            }
            if (Call(socket, bytes, control, true, 0x4040, out _, out _) != bytes.Length)
                throw new Win32Exception(Marshal.GetLastPInvokeError(), "Native bootstrap send failed.");
        }
        finally { for (var i = 0; i < held.Length; i++) if (held[i]) descriptors[i].DangerousRelease(); }
    }

    private static long Call(Socket socket, byte[] bytes, byte[] control, bool send, int flags, out int returnedFlags, out int controlLength)
    {
        var dataPin = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        var controlPin = GCHandle.Alloc(control, GCHandleType.Pinned);
        var vectorMemory = Marshal.AllocHGlobal(Marshal.SizeOf<IoVector>());
        try
        {
            Marshal.StructureToPtr(new IoVector { Address = dataPin.AddrOfPinnedObject(), Length = (nuint)bytes.Length }, vectorMemory, false);
            var message = new Message { Vectors = vectorMemory, VectorCount = 1, Control = control.Length == 0 ? IntPtr.Zero : controlPin.AddrOfPinnedObject(), ControlLength = (nuint)control.Length };
            var result = send ? SendMessage(socket.SafeHandle, ref message, flags) : ReceiveMessage(socket.SafeHandle, ref message, flags);
            var error = Marshal.GetLastPInvokeError();
            returnedFlags = message.Flags; controlLength = checked((int)message.ControlLength);
            Marshal.SetLastPInvokeError(error); return result;
        }
        finally { Marshal.FreeHGlobal(vectorMemory); controlPin.Free(); dataPin.Free(); }
    }

    [DllImport("libc", EntryPoint = "recvmsg", SetLastError = true)]
    private static extern long ReceiveMessage(SafeSocketHandle socket, ref Message message, int flags);
    [DllImport("libc", EntryPoint = "sendmsg", SetLastError = true)]
    private static extern long SendMessage(SafeSocketHandle socket, ref Message message, int flags);
}
