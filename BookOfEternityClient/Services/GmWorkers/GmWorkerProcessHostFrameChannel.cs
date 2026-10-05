using System.Diagnostics;
using System.Text;

namespace BookOfEternityClient.Services.GmWorkers;

/// <summary>Bounded strict UTF-8 LF frames. The caller owns the stream and serializes each direction.</summary>
internal sealed class GmWorkerProcessHostFrameChannel(Stream stream)
{
    internal const int LaunchMaximumBytes = 1024 * 1024;
    internal const int SmallMaximumBytes = 64 * 1024;
    internal static readonly TimeSpan FrameTimeout = TimeSpan.FromSeconds(15);
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private readonly byte[] _readBuffer = new byte[4096];
    private int _offset;
    private int _count;
    private long _bufferReceivedAt;

    internal async Task<string?> ReadAsync(
        int maximumBytes,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        bool waitForFirstByte = false)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumBytes);
        cancellationToken.ThrowIfCancellationRequested();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var buffered = _offset < _count;
        var started = !waitForFirstByte || buffered;
        if (started)
        {
            // A complete queued frame already met its receive deadline. An unfinished
            // buffered frame must keep the deadline from its original first-byte arrival.
            var remaining = buffered && Array.IndexOf(_readBuffer, (byte)'\n', _offset, _count - _offset) < 0
                ? timeout - Stopwatch.GetElapsedTime(_bufferReceivedAt)
                : timeout;
            if (remaining <= TimeSpan.Zero)
                throw new TimeoutException("Worker process host frame did not finish before its deadline.");
            deadline.CancelAfter(remaining);
        }
        using var frame = new MemoryStream(Math.Min(maximumBytes, _readBuffer.Length));
        try
        {
            while (true)
            {
                deadline.Token.ThrowIfCancellationRequested();
                if (_offset == _count)
                {
                    _count = await stream.ReadAsync(_readBuffer, deadline.Token);
                    _bufferReceivedAt = Stopwatch.GetTimestamp();
                    _offset = 0;
                    if (_count == 0)
                    {
                        if (frame.Length == 0) return null;
                        throw new InvalidDataException("Worker process host frame ended before its delimiter.");
                    }
                    if (!started)
                    {
                        started = true;
                        deadline.CancelAfter(timeout);
                    }
                }

                var newline = Array.IndexOf(_readBuffer, (byte)'\n', _offset, _count - _offset);
                var length = (newline < 0 ? _count : newline) - _offset;
                if (length > maximumBytes - frame.Length)
                    throw new InvalidDataException("Worker process host frame exceeds its byte limit.");
                frame.Write(_readBuffer, _offset, length);
                _offset += length;
                if (newline < 0) continue;
                _offset++;
                deadline.Token.ThrowIfCancellationRequested();
                var bytes = frame.GetBuffer();
                var size = checked((int)frame.Length);
                // CRLF remains readable, and the CR has already counted toward the bound.
                if (size > 0 && bytes[size - 1] == (byte)'\r') size--;
                try { return StrictUtf8.GetString(bytes, 0, size); }
                catch (DecoderFallbackException)
                {
                    throw new InvalidDataException("Worker process host frame contains invalid UTF-8.");
                }
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && deadline.IsCancellationRequested)
        {
            throw new TimeoutException("Worker process host frame did not finish before its deadline.");
        }
    }

    internal async Task WriteAsync(string value, int maximumBytes, TimeSpan timeout, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumBytes);
        cancellationToken.ThrowIfCancellationRequested();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        try
        {
            if (value.Contains('\n') || value.Contains('\r'))
                throw new InvalidDataException("Worker process host frame contains an embedded delimiter.");
            byte[] bytes;
            try
            {
                var length = StrictUtf8.GetByteCount(value);
                if (length > maximumBytes)
                    throw new InvalidDataException("Worker process host frame exceeds its byte limit.");
                bytes = new byte[length + 1];
                StrictUtf8.GetBytes(value.AsSpan(), bytes.AsSpan(0, length));
                bytes[length] = (byte)'\n';
            }
            catch (EncoderFallbackException)
            {
                throw new InvalidDataException("Worker process host frame contains invalid Unicode.");
            }
            deadline.Token.ThrowIfCancellationRequested();
            await stream.WriteAsync(bytes, deadline.Token);
            await stream.FlushAsync(deadline.Token);
            deadline.Token.ThrowIfCancellationRequested();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && deadline.IsCancellationRequested)
        {
            throw new TimeoutException("Worker process host frame did not finish before its deadline.");
        }
    }
}
