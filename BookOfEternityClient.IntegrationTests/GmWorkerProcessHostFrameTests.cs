using System.Diagnostics;
using System.Text;
using BookOfEternityClient.Services.GmWorkers;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class GmWorkerProcessHostFrameTests
{
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(2);

    [Fact]
    public async Task Read_PreservesBufferedNextFrameAndSplitUnicode()
    {
        using var stream = new FragmentedStream(Encoding.UTF8.GetBytes("я😀\r\nnext\n"), 1);
        var channel = new GmWorkerProcessHostFrameChannel(stream);
        Assert.Equal("я😀", await channel.ReadAsync(64, Deadline, CancellationToken.None));
        Assert.Equal("next", await channel.ReadAsync(64, Deadline, CancellationToken.None));
        Assert.Null(await channel.ReadAsync(64, Deadline, CancellationToken.None));
    }

    [Fact]
    public async Task Read_PreservesTwoFramesInOneRead()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("first\nsecond\n"));
        var channel = new GmWorkerProcessHostFrameChannel(stream);
        Assert.Equal("first", await channel.ReadAsync(64, Deadline, CancellationToken.None));
        Assert.Equal("second", await channel.ReadAsync(64, Deadline, CancellationToken.None));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Read_BufferedPartialFrameKeepsOriginalArrivalDeadline(bool waitForFirstByte)
    {
        // The first physical read also receives "seco", but not the second delimiter.
        using var stream = new FragmentedStream(Encoding.UTF8.GetBytes("first\nsecond\n"), 10);
        var channel = new GmWorkerProcessHostFrameChannel(stream);
        var timeout = TimeSpan.FromMilliseconds(150);
        Assert.Equal("first", await channel.ReadAsync(64, timeout, CancellationToken.None));
        await Task.Delay(TimeSpan.FromMilliseconds(300));
        await Assert.ThrowsAsync<TimeoutException>(() => channel.ReadAsync(64, timeout, CancellationToken.None, waitForFirstByte));
    }

    [Fact]
    public async Task Read_AlreadyCompleteBufferedFrameRemainsReadable()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("first\nsecond\n"));
        var channel = new GmWorkerProcessHostFrameChannel(stream);
        var timeout = TimeSpan.FromMilliseconds(150);
        Assert.Equal("first", await channel.ReadAsync(64, timeout, CancellationToken.None));
        await Task.Delay(TimeSpan.FromMilliseconds(300));
        // Both first byte and LF already arrived together, so this frame is complete.
        Assert.Equal("second", await channel.ReadAsync(64, timeout, CancellationToken.None, waitForFirstByte: true));
    }

    [Theory]
    [InlineData(64 * 1024)]
    [InlineData(1024 * 1024)]
    public async Task WriteAndRead_ExactUtf8ByteBoundaryIsAccepted(int limit)
    {
        var value = new string('я', limit / 2);
        using var stream = new MemoryStream();
        var channel = new GmWorkerProcessHostFrameChannel(stream);
        await channel.WriteAsync(value, limit, Deadline, CancellationToken.None);
        Assert.Equal(limit + 1, stream.Length);
        Assert.Equal((byte)'\n', stream.ToArray()[^1]);
        stream.Position = 0;
        Assert.Equal(value, await channel.ReadAsync(limit, Deadline, CancellationToken.None));
    }

    [Theory]
    [InlineData(64 * 1024)]
    [InlineData(1024 * 1024)]
    public async Task Write_OneByteOverLimitWritesNothing(int limit)
    {
        using var stream = new MemoryStream();
        var channel = new GmWorkerProcessHostFrameChannel(stream);
        await Assert.ThrowsAsync<InvalidDataException>(() => channel.WriteAsync(
            new string('я', limit / 2) + "x", limit, Deadline, CancellationToken.None));
        Assert.Equal(0, stream.Length);
    }

    [Theory]
    [InlineData(64 * 1024)]
    [InlineData(1024 * 1024)]
    public async Task Read_OneByteOverLimitIsRejected(int limit)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(new string('я', limit / 2) + "x\n"));
        var channel = new GmWorkerProcessHostFrameChannel(stream);
        await Assert.ThrowsAsync<InvalidDataException>(() => channel.ReadAsync(limit, Deadline, CancellationToken.None));
    }

    [Fact]
    public async Task Read_CrCountsTowardEncodedLimit()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("1234\r\n"));
        var channel = new GmWorkerProcessHostFrameChannel(stream);
        await Assert.ThrowsAsync<InvalidDataException>(() => channel.ReadAsync(4, Deadline, CancellationToken.None));
    }

    [Theory]
    [InlineData(new byte[] { 0xff, 10 })]
    [InlineData(new byte[] { 0xc3, 0x28, 10 })]
    [InlineData(new byte[] { 0xc3, 10 })]
    public async Task Read_InvalidUtf8IsRejectedWithoutReplacement(byte[] bytes)
    {
        using var stream = new FragmentedStream(bytes, 1);
        var channel = new GmWorkerProcessHostFrameChannel(stream);
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => channel.ReadAsync(64, Deadline, CancellationToken.None));
        Assert.Null(error.InnerException);
    }

    [Fact]
    public async Task Read_TruncatedEofDoesNotReturnPartialFrame()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("partial"));
        var channel = new GmWorkerProcessHostFrameChannel(stream);
        await Assert.ThrowsAsync<InvalidDataException>(() => channel.ReadAsync(64, Deadline, CancellationToken.None));
    }

    [Theory]
    [InlineData("line\nnext")]
    [InlineData("line\rnext")]
    public async Task Write_InvalidTextWritesNothing(string value)
    {
        using var stream = new MemoryStream();
        var channel = new GmWorkerProcessHostFrameChannel(stream);
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => channel.WriteAsync(value, 64, Deadline, CancellationToken.None));
        Assert.Null(error.InnerException);
        Assert.Equal(0, stream.Length);
    }

    [Fact]
    public Task Write_UnpairedSurrogateWritesNothing() => Write_InvalidTextWritesNothing(new string((char)0xd800, 1));

    [Fact]
    public async Task Read_SlowBytesDoNotRenewAbsoluteDeadline()
    {
        using var stream = new DelayedStream(Encoding.UTF8.GetBytes("slow-frame\n"), TimeSpan.FromMilliseconds(60));
        var channel = new GmWorkerProcessHostFrameChannel(stream);
        var clock = Stopwatch.StartNew();
        await Assert.ThrowsAsync<TimeoutException>(() => channel.ReadAsync(64, TimeSpan.FromMilliseconds(160), CancellationToken.None));
        Assert.InRange(stream.CompletedReads, 1, 5);
        Assert.Equal(0, stream.ActiveOperations);
        Assert.Equal(1, stream.CanceledOperations);
        Assert.True(clock.Elapsed < Deadline);
    }

    [Fact]
    public async Task Read_CallerCancellationAwaitsUnderlyingRead()
    {
        using var stream = new DelayedStream([1], TimeSpan.FromSeconds(30));
        var channel = new GmWorkerProcessHostFrameChannel(stream);
        using var cancellation = new CancellationTokenSource();
        var read = channel.ReadAsync(64, Deadline, cancellation.Token);
        await stream.Started.Task.WaitAsync(Deadline);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => read);
        Assert.Equal(0, stream.ActiveOperations);
        Assert.Equal(1, stream.CanceledOperations);
    }

    [Fact]
    public async Task Write_BlockedWriterDeadlineAwaitsUnderlyingWrite()
    {
        using var stream = new DelayedStream([], TimeSpan.FromSeconds(30));
        var channel = new GmWorkerProcessHostFrameChannel(stream);
        await Assert.ThrowsAsync<TimeoutException>(() => channel.WriteAsync("value", 64, TimeSpan.FromMilliseconds(150), CancellationToken.None));
        Assert.Equal(0, stream.ActiveOperations);
        Assert.Equal(1, stream.CanceledOperations);
    }

    [Fact]
    public async Task Write_CallerCancellationAwaitsUnderlyingWrite()
    {
        using var stream = new DelayedStream([], TimeSpan.FromSeconds(30));
        var channel = new GmWorkerProcessHostFrameChannel(stream);
        using var cancellation = new CancellationTokenSource();
        var write = channel.WriteAsync("value", 64, Deadline, cancellation.Token);
        await stream.Started.Task.WaitAsync(Deadline);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => write);
        Assert.Equal(0, stream.ActiveOperations);
        Assert.Equal(1, stream.CanceledOperations);
    }

    [Fact]
    public async Task Read_CompletionWaitMayBeIdleButStartedFrameIsBounded()
    {
        using var stream = new DelayedStream(Encoding.UTF8.GetBytes("value\n"), TimeSpan.FromMilliseconds(60), firstDelay: TimeSpan.FromMilliseconds(220));
        var channel = new GmWorkerProcessHostFrameChannel(stream);
        var clock = Stopwatch.StartNew();
        await Assert.ThrowsAsync<TimeoutException>(() => channel.ReadAsync(64, TimeSpan.FromMilliseconds(160), CancellationToken.None, waitForFirstByte: true));
        Assert.True(clock.Elapsed >= TimeSpan.FromMilliseconds(220));
        Assert.Equal(0, stream.ActiveOperations);
        Assert.Equal(1, stream.CanceledOperations);
    }

    private sealed class FragmentedStream(byte[] bytes, int maximumChunk) : MemoryStream(bytes)
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            base.ReadAsync(buffer[..Math.Min(buffer.Length, maximumChunk)], cancellationToken);
    }

    private sealed class DelayedStream(byte[] bytes, TimeSpan delay, TimeSpan? firstDelay = null) : Stream
    {
        private int _offset;
        public int ActiveOperations { get; private set; }
        public int CompletedReads { get; private set; }
        public int CanceledOperations { get; private set; }
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override bool CanRead => true;
        public override bool CanWrite => true;
        public override bool CanSeek => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await PauseAsync(_offset == 0 ? firstDelay ?? delay : delay, cancellationToken);
            if (_offset == bytes.Length) return 0;
            buffer.Span[0] = bytes[_offset++];
            CompletedReads++;
            return 1;
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
            await PauseAsync(delay, cancellationToken);

        private async Task PauseAsync(TimeSpan wait, CancellationToken token)
        {
            ActiveOperations++;
            Started.TrySetResult();
            try { await Task.Delay(wait, token); }
            catch (OperationCanceledException) { CanceledOperations++; throw; }
            finally { ActiveOperations--; }
        }
    }
}
