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
    private bool IsRegion { get; init; }
    private long RegionOffset { get; init; }
    private long ContainerLength { get; init; }
    internal const int CopyBufferSize = 64 * 1024;
    internal long Length => IsFileBacked ? FileLength : Bytes?.LongLength ?? 0;
    internal bool IsFileBacked => FileScope != null;

    internal static TrustedLocalFileImage FromRegion(TrustedLocalFileScope scope, string path,
        long offset, long length, long containerLength, string sha256)
    {
        if (offset < 0 || length < 0 || offset > containerLength || length > containerLength - offset)
            throw new InvalidDataException("The image region lies outside its journal.");
        return new()
        {
            Exists = true, Bytes = null, Sha256 = sha256, FileScope = scope, FilePath = path,
            FileLength = length, IsRegion = true, RegionOffset = offset, ContainerLength = containerLength
        };
    }

    internal TrustedLocalFileImage RebindJournal(string path) => IsRegion ? this with { FilePath = path } : this;

    internal void Validate()
    {
        if (!Exists)
        {
            if (Bytes != null || IsFileBacked || Sha256 != null)
                throw new InvalidDataException("An absent publication image contains data.");
            return;
        }
        if (IsFileBacked == (Bytes != null) || Sha256 == null || Length < 0)
            throw new InvalidDataException("A present publication image has invalid storage.");
        CopyTo(Stream.Null);
    }

    internal bool MatchesFile(TrustedLocalFileScope scope, string path)
    {
        var normalized = scope.ValidateFile(path);
        var exists = File.Exists(normalized);
        if (exists != Exists) return false;
        if (!exists) return true;
        using var stream = OpenFile(scope, normalized);
        if (stream.Length != Length) return false;
        var hash = Convert.ToHexString(SHA256.HashData(stream));
        scope.ValidateFile(normalized, allowMissing: false);
        return stream.Position == Length && stream.Length == Length && hash == Sha256;
    }

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

    internal Stream OpenRead()
    {
        if (!Exists) throw new InvalidOperationException("An absent image has no stream.");
        if (!IsFileBacked)
            return new MemoryStream(Bytes ?? throw new InvalidDataException("The present image has no bytes."), writable: false);
        var stream = OpenFile(FileScope!, FilePath!);
        if (stream.Length != (IsRegion ? ContainerLength : FileLength))
        {
            stream.Dispose();
            throw new InvalidDataException("The image source length changed after capture.");
        }
        if (!IsRegion) return stream;
        stream.Position = RegionOffset;
        return new RegionReadStream(stream, FileLength);
    }

    private static FileStream OpenFile(TrustedLocalFileScope scope, string path) => new(
        scope.ValidateFile(path, allowMissing: false), FileMode.Open, FileAccess.Read,
        FileShare.Read | FileShare.Delete, CopyBufferSize, FileOptions.SequentialScan);

    private sealed class RegionReadStream(Stream source, long length) : Stream
    {
        private long _position;
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => length;
        public override long Position { get => _position; set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));
        public override int Read(Span<byte> buffer)
        {
            var read = source.Read(buffer[..(int)Math.Min(buffer.Length, length - _position)]);
            _position += read;
            return read;
        }
        protected override void Dispose(bool disposing) { if (disposing) source.Dispose(); base.Dispose(disposing); }
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
