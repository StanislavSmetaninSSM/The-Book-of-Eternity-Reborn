using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace BookOfEternityClient.Services.GmWorkers;

internal partial class NativeLineageOwner
{
    private readonly bool _inheritedTerminal;
    private SafeFileHandle? _originalSupervisorPidfd;
    private string? _inheritedNonce;
    // 0 challenge pending; 1 authenticated frame; 2 launch sent; 3 held;
    // 4 release attempted; 5 aborted/closed. Never re-enter an earlier phase.
    private int _inheritedPhase;
    private int _inheritedCaptureStarted;
    private readonly TaskCompletionSource _inheritedAuthenticated = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<(int Root, int Terminal)> _inheritedOffer = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal bool UsesInheritedTerminalBootstrap => _inheritedTerminal;

    private bool HasHeldBootstrap()
    {
        lock (_stateGate) return _inheritedTerminal ? _inheritedPhase == 3 : _bootstrap != null;
    }

    private async Task AuthenticateInheritedSupervisorAsync(CancellationToken token)
    {
        if (Interlocked.Exchange(ref _inheritedCaptureStarted, 1) != 0 || !AuthorityValid())
            throw new InvalidOperationException("Original inherited candidate capture is single-use.");
        if (!OperatingSystem.IsLinux() || RuntimeInformation.ProcessArchitecture != Architecture.X64)
            throw new PlatformNotSupportedException("Inherited terminal package is qualified only for Linux x64.");
        var descriptor = OpenPidfd(434, _supervisor.Id, 0);
        if (descriptor < 0) throw NativeDescriptorFailure("Original supervisor candidate capture failed");
        _originalSupervisorPidfd = new SafeFileHandle(descriptor, true);
        // No candidate signal or root launch is permitted before this proof.
        // The nonce is generated AFTER capture; original pipes have no other
        // writer/reader in the helper before root fork. PID reuse cannot answer it.
        var nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        lock (_stateGate) _inheritedNonce = nonce;
        await _controlGate.WaitAsync(token);
        try
        {
            await _supervisor.StandardInput.BaseStream.WriteAsync(Encoding.ASCII.GetBytes("H" + nonce + "\n"), token);
        }
        finally { _controlGate.Release(); }
        await _inheritedAuthenticated.Task.WaitAsync(token);
        EnsureOriginalInheritedSupervisor();
    }

    private void EnsureOriginalInheritedSupervisor()
    {
        lock (_stateGate)
            if (_inheritedPhase is < 1 or > 4) throw new IOException("Inherited supervisor phase is not admitted.");
        if (_originalSupervisorPidfd == null || _supervisor.HasExited ||
            GmWorkerHostIdentity.IsReadable(_originalSupervisorPidfd) || !AuthorityValid())
            throw new IOException("Original inherited supervisor authority is unavailable.");
    }

    private async Task<SafeFileHandle[]> ReceiveInheritedRightsAsync(CancellationToken token)
    {
        var offer = await _inheritedOffer.Task.WaitAsync(token);
        EnsureOriginalInheritedSupervisor();
        SafeFileHandle? root = null, terminal = null;
        try
        {
            root = DuplicateInheritedDescriptor(offer.Root);
            terminal = DuplicateInheritedDescriptor(offer.Terminal);
            if (IsTerminal(terminal) != 1 || (DescriptorFlags(terminal, 3) & 3) != 2 ||
                GetPtyNumber(terminal, 0x80045430, out _) != 0)
                throw new InvalidDataException("Inherited PTY descriptor has the wrong type or access.");
            token.ThrowIfCancellationRequested();
            EnsureOriginalInheritedSupervisor();
            return [root, terminal];
        }
        catch { root?.Dispose(); terminal?.Dispose(); throw; }
    }

    private SafeFileHandle DuplicateInheritedDescriptor(int locator)
    {
        var descriptor = GetPidfdDescriptor(438, _originalSupervisorPidfd!, locator, 0);
        if (descriptor < 0) throw NativeDescriptorFailure("Inherited descriptor transfer failed");
        return new SafeFileHandle(descriptor, true);
    }

    internal Task LaunchInheritedAsync(CancellationToken token) =>
        SendInheritedControlAsync('L', 1, 2, token);

    internal Task ReleaseInheritedAsync(CancellationToken token) =>
        SendInheritedControlAsync('A', 3, 4, token);

    private async Task SendInheritedControlAsync(char command, int expectedPhase, int nextPhase,
        CancellationToken token)
    {
        // The held limit bounds the gate AND the original pipe write. Never
        // retry a command whose cancellation could follow a partial write.
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        var remaining = HeldTerminalRemaining;
        if (remaining <= TimeSpan.Zero) throw new IOException("Inherited command deadline expired.");
        deadline.CancelAfter(remaining);
        var acquired = false;
        try
        {
            await _controlGate.WaitAsync(deadline.Token).ConfigureAwait(false);
            acquired = true;
            deadline.Token.ThrowIfCancellationRequested();
            EnsureOriginalInheritedSupervisor();
            lock (_stateGate)
            {
                if (Stopwatch.GetTimestamp() >= _heldDeadline || _inheritedPhase != expectedPhase || !AuthorityValid())
                    throw new IOException("Inherited command lost its original phase or deadline.");
                _inheritedPhase = nextPhase;
            }
            await _supervisor.StandardInput.BaseStream.WriteAsync(new byte[] { (byte)command }, deadline.Token).ConfigureAwait(false);
        }
        catch
        {
            Lose("inherited-command-unconfirmed");
            throw;
        }
        finally { if (acquired) _controlGate.Release(); }
        // A keeps control open for ordinary scoped stop; it is not abort.
    }

    private bool AcceptInheritedBootstrap(JsonElement frame)
    {
        if (!frame.TryGetProperty("bootstrap", out var kind)) return false;
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in frame.EnumerateObject())
            if (!names.Add(property.Name)) throw new InvalidDataException("Duplicate bootstrap field.");
        lock (_stateGate)
        {
            if (!_inheritedTerminal || !AuthorityValid() || frame.GetProperty("version").GetInt32() != 1 ||
                frame.GetProperty("runId").GetString() != _run) throw new InvalidDataException("Invalid bootstrap identity.");
            if (kind.GetString() == "authenticated")
            {
                if (_inheritedPhase != 0 || _inheritedNonce == null || names.Count != 4 ||
                    frame.GetProperty("nonce").GetString() != _inheritedNonce)
                    throw new InvalidDataException("Original post-capture challenge mismatch.");
                _inheritedPhase = 1;
                _inheritedAuthenticated.TrySetResult();
            }
            else if (kind.GetString() == "held")
            {
                var root = frame.GetProperty("rootPidfd").GetInt32();
                var terminal = frame.GetProperty("terminalMaster").GetInt32();
                if (_inheritedPhase != 2 || names.Count != 5 || root < 3 || terminal < 3 || root == terminal)
                    throw new InvalidDataException("Invalid inherited descriptor offer.");
                _inheritedPhase = 3;
                _inheritedOffer.TrySetResult((root, terminal));
            }
            else throw new InvalidDataException("Unknown bootstrap frame.");
        }
        return true;
    }

    private bool _inheritedControlClosed;
    private async Task AbortInheritedBootstrapAsync()
    {
        lock (_stateGate) _inheritedPhase = 5;
        if (!_processStarted) { _inheritedControlClosed = true; return; }
        // Never block a synchronization context on a pending async control writer.
        // Timeout is retained uncertainty, not successful closure or disposal.
        if (!await _controlGate.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false))
        {
            Lose("inherited-control-close-unresolved");
            return;
        }
        try
        {
            // No StreamWriter buffer is used by inherited control. Dispose the raw
            // pipe without flushing any cancelled command.
            _supervisor.StandardInput.BaseStream.Dispose();
            _inheritedControlClosed = true;
        }
        catch (IOException) { Lose("inherited-control-close"); }
        finally { _controlGate.Release(); }
    }

    internal bool IsInheritedRootHeld => HasHeldBootstrap();

    // Explicit typed fixture seam: ownership of this lease blocks the same
    // gate used by production writers. It cannot alter phase or deadlines.
    internal async Task<IDisposable> HoldInheritedControlForTestAsync()
    {
        if (!_inheritedTerminal) throw new InvalidOperationException("Inherited transport required.");
        await _controlGate.WaitAsync();
        return new InheritedControlLease(_controlGate);
    }

    private sealed class InheritedControlLease(SemaphoreSlim gate) : IDisposable
    {
        private SemaphoreSlim? _gate = gate;
        public void Dispose() => Interlocked.Exchange(ref _gate, null)?.Release();
    }

    private static IOException NativeDescriptorFailure(string operation) =>
        new(operation, new Win32Exception(Marshal.GetLastPInvokeError()));
    [DllImport("libc", EntryPoint = "syscall", SetLastError = true)]
    private static extern nint OpenPidfd(nint number, int pid, uint flags);
    [DllImport("libc", EntryPoint = "syscall", SetLastError = true)]
    private static extern nint GetPidfdDescriptor(nint number, SafeFileHandle pidfd, int target, uint flags);
    [DllImport("libc", EntryPoint = "isatty", SetLastError = true)]
    private static extern int IsTerminal(SafeFileHandle descriptor);
    [DllImport("libc", EntryPoint = "ioctl", SetLastError = true)]
    private static extern int GetPtyNumber(SafeFileHandle descriptor, nuint request, out uint number);
    [DllImport("libc", EntryPoint = "fcntl", SetLastError = true)]
    private static extern int DescriptorFlags(SafeFileHandle descriptor, int command);
}
