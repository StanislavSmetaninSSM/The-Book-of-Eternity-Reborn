using System.Buffers.Binary;
using System.Text;
using System.Text.Json;

namespace BookOfEternityClient.Core;

internal sealed partial class TrustedLocalFilePublication
{
    private static readonly byte[] FrameMagic = Encoding.ASCII.GetBytes("BOELP2\r\n");
    // Only metadata is bounded here, never image/ZIP bytes. The ordinary save
    // producer has one archive member plus at most one generation member. Even
    // two 32,767-character Windows paths with six-byte JSON escaping fit well
    // below this budget. A larger future member-set producer needs its own
    // metadata qualification; existing v1 producers keep their original codec.
    internal const int MaximumFrameMetadataBytes = 1024 * 1024;

    private Journal WriteJournal(string path, Journal journal)
    {
        if (journal.Format == 1)
        {
            WriteNew(_journalScope, path, JsonSerializer.SerializeToUtf8Bytes(journal, JsonOptions));
            return journal;
        }

        long offset = 0;
        FrameImage Describe(TrustedLocalFileImage image)
        {
            var value = new FrameImage { Exists = image.Exists, Length = image.Length,
                Sha256 = image.Sha256, Offset = image.Exists ? offset : null };
            if (image.Exists) offset = checked(offset + image.Length);
            return value;
        }
        var header = new FrameHeader
        {
            Format = 2, TransactionId = journal.TransactionId, Committed = journal.Committed,
            GenerationBefore = journal.GenerationBefore, GenerationAfter = journal.GenerationAfter,
            Members = journal.Members.Select(member => new FrameMember
            { Path = member.Path, Before = Describe(member.Before), After = Describe(member.After) }).ToArray()
        };

        long payloadStart;
        long fileLength;
        using (var output = new FileStream(_journalScope.ValidateFile(path), FileMode.CreateNew,
                   FileAccess.ReadWrite, FileShare.None, TrustedLocalFileImage.CopyBufferSize, FileOptions.SequentialScan))
        {
            output.Write(FrameMagic);
            Span<byte> lengthBytes = stackalloc byte[8];
            lengthBytes.Clear(); output.Write(lengthBytes);
            using (var metadata = new BoundedMetadataWriter(output, MaximumFrameMetadataBytes))
                JsonSerializer.Serialize(metadata, header, JsonOptions);
            payloadStart = output.Position;
            BinaryPrimitives.WriteInt64LittleEndian(lengthBytes, payloadStart - 16);
            output.Position = 8; output.Write(lengthBytes); output.Position = payloadStart;
            foreach (var member in journal.Members)
            {
                if (member.Before.Exists) member.Before.CopyTo(output);
                if (member.After.Exists) member.After.CopyTo(output);
            }
            fileLength = checked(payloadStart + offset);
            if (output.Position != fileLength) throw Conflict("The v2 publication payload length is inconsistent.");
            output.Flush(flushToDisk: true);
        }
        return BindFrame(header, path, payloadStart, fileLength);
    }

    private Journal ReadJournal(string path)
    {
        using var stream = new FileStream(_journalScope.ValidateFile(path, allowMissing: false),
            FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete,
            TrustedLocalFileImage.CopyBufferSize, FileOptions.SequentialScan);
        Span<byte> magic = stackalloc byte[8];
        var count = stream.ReadAtLeast(magic, FrameMagic.Length, throwOnEndOfStream: false);
        stream.Position = 0;
        if (count != FrameMagic.Length || !magic.SequenceEqual(FrameMagic))
        {
            if (!HasPlausibleV1Prefix(stream))
                throw Conflict("Unknown publication format; evidence retained.");
            Journal journal;
            try
            {
                // Supported v1 JSON keeps its original decoding and required
                // fields. A new frame is never interpreted as legacy byte data.
                journal = StrictJsonAuthority.Deserialize<Journal>(File.ReadAllBytes(path), JsonOptions, "Trusted-local publication")
                    ?? throw Conflict("The publication journal is null.");
            }
            catch (JsonException ex) { throw new InvalidDataException("The publication journal is invalid; evidence retained.", ex); }
            if (journal.Format != 1) throw Conflict("Unknown publication JSON format; evidence retained.");
            ValidateJournal(journal);
            return journal;
        }

        try
        {
            stream.Position = FrameMagic.Length;
            Span<byte> lengthBytes = stackalloc byte[8]; stream.ReadExactly(lengthBytes);
            var length = BinaryPrimitives.ReadInt64LittleEndian(lengthBytes);
            if (length <= 0 || length > MaximumFrameMetadataBytes || length > stream.Length - 16)
                throw Conflict("The v2 metadata length is invalid.");
            var bytes = new byte[(int)length]; stream.ReadExactly(bytes);
            var header = StrictJsonAuthority.Deserialize<FrameHeader>(bytes, JsonOptions, "Trusted-local publication v2")
                ?? throw Conflict("The v2 metadata is null.");
            var journal = BindFrame(header, path, stream.Position, stream.Length);
            ValidateJournal(journal); // Every exact region hash/path validates before member recovery.
            return journal;
        }
        catch (Exception failure) when (failure is JsonException or InvalidDataException or EndOfStreamException or OverflowException)
        {
            throw new InvalidDataException("The v2 publication frame is invalid; evidence retained.", failure);
        }
    }

    private static bool HasPlausibleV1Prefix(Stream stream)
    {
        // Original v1 fields can be reordered/escaped and JSON whitespace can
        // be arbitrarily long. Inspect it incrementally, without reading an
        // unknown binary frame as one byte array. The longest legal first
        // property is GenerationBefore (16 characters, at most six encoded
        // bytes per character). Unknown properties were never admitted by v1.
        int NextNonWhitespace()
        {
            int value;
            do { value = stream.ReadByte(); } while (value is ' ' or '\t' or '\r' or '\n');
            return value;
        }
        if (NextNonWhitespace() != '{' || NextNonWhitespace() != '"') return false;
        Span<byte> property = stackalloc byte[2 + 6 * 16];
        property[0] = (byte)'"';
        var length = 1; var escaped = false; var closed = false;
        while (length < property.Length)
        {
            var value = stream.ReadByte();
            if (value < 0x20) return false;
            property[length++] = (byte)value;
            if (escaped) { escaped = false; continue; }
            if (value == '\\') { escaped = true; continue; }
            if (value == '"') { closed = true; break; }
        }
        if (!closed) return false;
        string? name;
        try { name = JsonSerializer.Deserialize<string>(property[..length]); }
        catch (JsonException) { return false; }
        if (NextNonWhitespace() != ':') return false;
        var firstValueByte = NextNonWhitespace();
        return name switch
        {
            "Format" => firstValueByte == '1',
            "TransactionId" => firstValueByte == '"',
            "Committed" => firstValueByte is 't' or 'f',
            "GenerationBefore" or "GenerationAfter" => firstValueByte == '{',
            "Members" => firstValueByte == '[',
            _ => false
        };
    }

    private Journal BindFrame(FrameHeader header, string path, long payloadStart, long fileLength)
    {
        if (header.Format != 2 || header.Members is not { Length: > 0 } || payloadStart > fileLength)
            throw Conflict("The v2 frame shape is invalid.");
        long offset = 0;
        TrustedLocalFileImage Bind(FrameImage image)
        {
            if (image == null || image.Length < 0) throw Conflict("The v2 image metadata is invalid.");
            if (!image.Exists)
            {
                if (image.Length != 0 || image.Offset != null || image.Sha256 != null)
                    throw Conflict("An absent v2 image has a region.");
                return TrustedLocalFileImage.FromBytes(null);
            }
            if (image.Offset != offset || image.Sha256 == null || image.Length > fileLength - payloadStart - offset)
                throw Conflict("The v2 image regions do not exactly cover their payload.");
            var result = TrustedLocalFileImage.FromRegion(_journalScope, path, checked(payloadStart + offset),
                image.Length, fileLength, image.Sha256);
            offset = checked(offset + image.Length);
            return result;
        }
        var members = header.Members.Select(member => member == null
            ? throw Conflict("A v2 member is null.")
            : new Member { Path = member.Path, Before = Bind(member.Before), After = Bind(member.After) }).ToArray();
        if (offset != fileLength - payloadStart) throw Conflict("The v2 frame has missing or trailing image bytes.");
        return new Journal
        {
            Format = 2, TransactionId = header.TransactionId, Committed = header.Committed,
            GenerationBefore = header.GenerationBefore, GenerationAfter = header.GenerationAfter, Members = members
        };
    }

    private static Journal RebindJournal(Journal journal, string path) => journal.Format == 1 ? journal : journal with
    {
        Members = journal.Members.Select(member => member with
        { Before = member.Before.RebindJournal(path), After = member.After.RebindJournal(path) }).ToArray()
    };

    private sealed record FrameHeader
    {
        public required int Format { get; init; }
        public required string TransactionId { get; init; }
        public required bool Committed { get; init; }
        public required TrustedLocalGeneration GenerationBefore { get; init; }
        public required TrustedLocalGeneration GenerationAfter { get; init; }
        public required FrameMember[] Members { get; init; }
    }
    private sealed record FrameMember
    {
        public required string Path { get; init; }
        public required FrameImage Before { get; init; }
        public required FrameImage After { get; init; }
    }
    private sealed record FrameImage
    {
        public required bool Exists { get; init; }
        public required long Length { get; init; }
        public required string? Sha256 { get; init; }
        public required long? Offset { get; init; }
    }

    private sealed class BoundedMetadataWriter(Stream destination, long limit) : Stream
    {
        private long _written;
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => _written;
        public override long Position { get => _written; set => throw new NotSupportedException(); }
        public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            if (buffer.Length > limit - _written) throw new InvalidDataException("The v2 publication metadata exceeds its supported producer budget.");
            destination.Write(buffer); _written += buffer.Length;
        }
        public override void Flush() => destination.Flush();
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
