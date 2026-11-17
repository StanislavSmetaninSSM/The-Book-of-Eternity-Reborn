using System.IO.Pipes;

namespace BookOfEternityClient.Services.GmWorkers;

// One original byte pipe, no managed write buffer, flush, retry or second gate.
// Admission and serialization remain solely with NativeLineageOwner.
internal sealed class InheritedTerminalControl(PipeStream pipe) : IDisposable
{
    private bool _aborted;

    internal ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken token)
    {
        ObjectDisposedException.ThrowIf(_aborted, this);
        return pipe.WriteAsync(bytes, token);
    }

    internal void Abort()
    {
        if (_aborted) return;
        pipe.Dispose();
        _aborted = true;
    }

    public void Dispose() => Abort();
}
