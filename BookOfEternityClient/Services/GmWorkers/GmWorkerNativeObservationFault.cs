using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services.GmWorkers;

internal enum GmWorkerNativeObservationFaultKind
{
    WrongRun, WrongScope, MalformedTerminal, ReportUncertain, HoldTerminal, HoldOutputs, DisposeOnce, MalformedStarted
}

// Internal synthetic-fixture faults only. This object can delay or invalidate an
// original observation, never provide positive stop/output evidence or a new owner.
internal sealed class GmWorkerNativeObservationFault(GmWorkerNativeObservationFaultKind kind)
{
    private readonly TaskCompletionSource _reached = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _disposeAttempts;
    internal Task Reached => _reached.Task;
    internal void ReleaseObservation() => _released.TrySetResult();
    internal int DisposeAttempts => Volatile.Read(ref _disposeAttempts);

    internal async Task<ReadOnlyMemory<byte>> ObserveFrameAsync(ReadOnlyMemory<byte> bytes)
    {
        using var frame = JsonDocument.Parse(bytes);
        if(kind==GmWorkerNativeObservationFaultKind.MalformedStarted)
        {
            if(frame.RootElement.GetProperty("state").GetString()!="Started")return bytes;
            _reached.TrySetResult();return Encoding.UTF8.GetBytes("{");
        }
        if (frame.RootElement.GetProperty("state").GetString() != "StoppedWithinScope" ||
            kind is GmWorkerNativeObservationFaultKind.HoldOutputs or GmWorkerNativeObservationFaultKind.DisposeOnce)
            return bytes;
        _reached.TrySetResult();
        if (kind == GmWorkerNativeObservationFaultKind.HoldTerminal)
        {
            await _released.Task;
            return bytes; // Original helper bytes only, even when released late.
        }
        if (kind == GmWorkerNativeObservationFaultKind.MalformedTerminal) return Encoding.UTF8.GetBytes("{");
        var altered = JsonNode.Parse(bytes.Span)!;
        switch (kind)
        {
            case GmWorkerNativeObservationFaultKind.WrongRun: altered["runId"] = new string('0', 32); break;
            case GmWorkerNativeObservationFaultKind.WrongScope: altered["guarantee"] = "wrong-scope"; break;
            case GmWorkerNativeObservationFaultKind.ReportUncertain:
                altered["state"] = "Uncertain"; altered["reason"] = "synthetic-negative-terminal"; break;
            default: throw new InvalidOperationException("Unknown negative observation fault.");
        }
        return Encoding.UTF8.GetBytes(altered.ToJsonString());
    }

    internal async Task ObserveOutputsAsync()
    {
        if (kind != GmWorkerNativeObservationFaultKind.HoldOutputs) return;
        _reached.TrySetResult();
        await _released.Task;
    }

    internal void BeforeDispose()
    {
        if (kind == GmWorkerNativeObservationFaultKind.DisposeOnce && Interlocked.Increment(ref _disposeAttempts) == 1)
        {
            _reached.TrySetResult();
            throw new IOException("Synthetic one-shot disposal failure before releasing original resources.");
        }
    }
}
