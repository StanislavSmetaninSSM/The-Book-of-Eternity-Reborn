using System.Buffers;
using System.Security.Cryptography;

namespace BookOfEternityClient.Core;

/// <summary>Internal exact image transport; file-backed images do not grant source mutation authority.</summary>
internal sealed record TrustedLocalFileImage
{
    public required bool Exists { get; init; }
    public required byte[]? Bytes { get; init; }
    public required string? Sha256 { get; init; }

    private TrustedLocalFileScope? FileScope { get; init; }
    private string? FilePath { get; init; }
    private long FileLength { get; init; }
    internal const int CopyBufferSize = 64 * 1024;
    internal long Length => IsFileBacked ? FileLength : Bytes?.LongLength ?? 0;
    internal bool IsFileBacked => FileScope != null;

    internal static TrustedLocalFileImage FromBytes(byte[]? bytes)
    {
        var snapshot = bytes?.ToArray();
        return new()
        {
            Exists = snapshot != null, Bytes = snapshot,
            Sha256 = snapshot == null ? null : Convert.ToHexString(SHA256.HashData(snapshot))
        };
    }

    internal static TrustedLocalFileImage CaptureFile(TrustedLocalFileScope scope, string path)
    {
        ArgumentNullException.ThrowIfNull(scope);
        var normalized = scope.ValidateFile(path, allowMissing: false);
        using var source = OpenFile(scope, normalized);
        var length = source.Length;
        var hash = Convert.ToHexString(SHA256.HashData(source));
        if (source.Position != length || source.Length != length)
            throw new InvalidDataException("The image source length changed during capture.");
        scope.ValidateFile(normalized, allowMissing: false);
        return new()
        {
            Exists = true, Bytes = null, Sha256 = hash,
            FileScope = scope, FilePath = normalized, FileLength = length
        };
    }

    internal void CopyTo(Stream destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        if (!Exists) throw new InvalidOperationException("An absent image has no stream.");
        using var source = OpenRead();
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = ArrayPool<byte>.Shared.Rent(CopyBufferSize);
        try
        {
            var remaining = Length;
            while (remaining > 0)
            {
                var count = source.Read(buffer, 0, (int)Math.Min(CopyBufferSize, remaining));
                if (count == 0) throw new InvalidDataException("The image source was truncated.");
                hash.AppendData(buffer, 0, count);
                destination.Write(buffer, 0, count);
                remaining -= count;
            }
            if (source.ReadByte() != -1 || Convert.ToHexString(hash.GetHashAndReset()) != Sha256)
                throw new InvalidDataException("The image source no longer matches its captured bytes.");
            FileScope?.ValidateFile(FilePath!, allowMissing: false);
        }
        finally { ArrayPool<byte>.Shared.Return(buffer, clearArray: true); }
    }

    private Stream OpenRead()
    {
        if (!IsFileBacked)
            return new MemoryStream(Bytes ?? throw new InvalidDataException("The present image has no bytes."), writable: false);
        var stream = OpenFile(FileScope!, FilePath!);
        if (stream.Length == FileLength) return stream;
        stream.Dispose();
        throw new InvalidDataException("The image source length changed after capture.");
    }

    private static FileStream OpenFile(TrustedLocalFileScope scope, string path) => new(
        scope.ValidateFile(path, allowMissing: false), FileMode.Open, FileAccess.Read,
        FileShare.Read | FileShare.Delete, CopyBufferSize, FileOptions.SequentialScan);
}
