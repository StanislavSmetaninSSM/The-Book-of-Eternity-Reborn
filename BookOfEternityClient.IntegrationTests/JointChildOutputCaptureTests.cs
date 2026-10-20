using System.Text;
using BookOfEternityClient.Services.GmWorkers;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Verifies finite output transport without game or native process construction.
/// </summary>
public sealed class JointChildOutputCaptureTests
{
    /// <summary>
    /// Preserves an already read prefix even when the next pipe read fails.
    /// </summary>
    [Fact]
    public async Task ReadFailurePreservesPersistedPrefix()
    {
        var fault = new IOException("pipe read");
        using var reader = new PrefixFaultReader(fault);
        using var writer = new StringWriter();
        var result = await FinitePipeOutput.DrainAsync(reader, writer);
        Assert.Equal("prefix\r\n", writer.ToString());
        Assert.False(result.Eof);
        Assert.Same(fault, result.ReadFailure);
        Assert.Null(result.WriteFailure);
    }

    /// <summary>
    /// Continues consuming a pipe after its storage destination fails.
    /// </summary>
    [Fact]
    public async Task StorageFailureDoesNotManufactureEofOrStopConsumption()
    {
        using var reader = new StringReader(new string('x', 20000));
        using var writer = new FaultWriter();
        var result = await FinitePipeOutput.DrainAsync(reader, writer);
        Assert.True(result.Eof);
        Assert.NotNull(result.WriteFailure);
        Assert.Equal(-1, reader.Read());
    }

    /// <summary>
    /// Emits one prefix before propagating the supplied original read failure.
    /// </summary>
    /// <param name="failure">
    /// The exact read exception required by the test.
    /// </param>
    private sealed class PrefixFaultReader(Exception failure) : TextReader
    {
        private bool _read;
        /// <inheritdoc />
        public override Task<int> ReadAsync(char[] buffer, int index, int count)
        {
            if (_read) throw failure;
            _read = true;
            "prefix\r\n".CopyTo(0, buffer, index, 8);
            return Task.FromResult(8);
        }
        /// <inheritdoc />
        public override ValueTask<int> ReadAsync(Memory<char> buffer, CancellationToken cancellationToken = default)
        {
            if (_read) throw failure;
            _read = true;
            "prefix\r\n".AsSpan().CopyTo(buffer.Span);
            return ValueTask.FromResult(8);
        }
    }

    /// <summary>
    /// Refuses every write without buffering diagnostic content.
    /// </summary>
    private sealed class FaultWriter : TextWriter
    {
        /// <inheritdoc />
        public override Encoding Encoding => Encoding.UTF8;
        /// <inheritdoc />
        public override Task WriteAsync(string? value) => throw new IOException("storage");
        /// <inheritdoc />
        public override ValueTask WriteAsync(ReadOnlyMemory<char> buffer, CancellationToken cancellationToken = default) =>
            ValueTask.FromException(new IOException("storage"));
    }
}
