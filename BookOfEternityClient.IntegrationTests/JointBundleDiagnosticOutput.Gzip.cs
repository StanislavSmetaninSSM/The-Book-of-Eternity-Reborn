using System.IO.Compression;
using System.Security.Cryptography;

namespace BookOfEternityClient.Tests;

internal sealed partial class JointBundleDiagnosticOutput
{
    // Header (10), minimum DEFLATE payload (2), trailer (8). This lower
    // bound excludes no-member output; it is not a full-container verdict.
    private const long MinimumGzipMemberBytes = 20;
    private bool GzipFooterClosed => _gzip && _writerClosed && _bytes is >= MinimumGzipMemberBytes;
    private string RecordsEncoding => _gzip ? "gzip-jsonl-utf8-v1" : "jsonl-utf8-v1";
    private sealed record DecodedRecords(long Bytes, long Rows, string Sha256);

    private object FlushCostData()
    {
        var cost = Timings.GetFlushCosts();
        return new { cost.Attempted, cost.Stored, cost.Elapsed, cost.GateWait };
    }

    // Owning close can return without creating a member. Decoder EOF alone
    // must not qualify that output. This extra read remains explicitly measured.
    private static DecodedRecords DecodeRecords(string path, long? actualStoredBytes)
    {
        if (actualStoredBytes is null or < MinimumGzipMemberBytes)
            throw new InvalidDataException("No complete gzip member exists in stored records.");
        using var stored = File.OpenRead(path);
        using var decoded = new GZipStream(stored, CompressionMode.Decompress);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[65536];
        long bytes = 0, rows = 0;
        var last = -1;
        int count;
        while ((count = decoded.Read(buffer)) != 0)
        {
            hash.AppendData(buffer, 0, count);
            bytes = checked(bytes + count);
            var remaining = buffer.AsSpan(0, count);
            int newline;
            while ((newline = remaining.IndexOf((byte)'\n')) >= 0)
            {
                rows++;
                remaining = remaining[(newline + 1)..];
            }
            last = buffer[count - 1];
        }
        if (bytes != 0 && last != '\n') throw new InvalidDataException("Closed JSONL has an unfinished final row.");
        return new(bytes, rows, Convert.ToHexString(hash.GetHashAndReset()));
    }

    /// <summary>
    /// Revalidates exact closed stored identity and decoded identity. Decoder EOF
    /// alone never certifies a footer; expected identity comes from the owning close.
    /// </summary>
    /// <param name="path">The actual compressed artifact.</param>
    /// <param name="storedBytes">Length recorded after successful owning footer close.</param>
    /// <param name="storedSha256">Exact compressed bytes recorded by that close.</param>
    /// <param name="decodedBytes">Independent 64-bit decoded byte count.</param>
    /// <param name="decodedSha256">Independent streaming decoded byte digest.</param>
    internal static void ValidateClosedGzipIdentity(string path, long storedBytes, string storedSha256,
        long decodedBytes, string decodedSha256)
    {
        using (var input = File.OpenRead(path))
            if (input.Length != storedBytes || Convert.ToHexString(SHA256.HashData(input)) != storedSha256)
                throw new InvalidDataException("Closed gzip stored identity changed; container completeness is unproven.");
        var decoded = DecodeRecords(path, storedBytes);
        if (decoded.Bytes != decodedBytes || decoded.Sha256 != decodedSha256)
            throw new InvalidDataException("Closed gzip decoded identity changed.");
    }
}
