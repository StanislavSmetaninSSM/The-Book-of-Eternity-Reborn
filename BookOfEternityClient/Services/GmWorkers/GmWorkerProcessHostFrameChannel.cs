using System.Text;

namespace BookOfEternityClient.Services.GmWorkers;

// Initial roundtrip implementation: matches the existing host's StreamReader/Writer behavior.
// Boundary/deadline tests must establish RED before this candidate is hardened or wired into the host.
internal sealed class GmWorkerProcessHostFrameChannel(Stream stream)
{
    private readonly StreamReader _reader = new(stream, Encoding.UTF8, false, 1024, leaveOpen: true);

    internal async Task<string?> ReadAsync(int maximumBytes, TimeSpan timeout, CancellationToken cancellationToken, bool waitForFirstByte = false) =>
        await _reader.ReadLineAsync(cancellationToken);

    internal async Task WriteAsync(string value, int maximumBytes, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var bytes = Encoding.UTF8.GetBytes(value + "\n");
        await stream.WriteAsync(bytes, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }
}
