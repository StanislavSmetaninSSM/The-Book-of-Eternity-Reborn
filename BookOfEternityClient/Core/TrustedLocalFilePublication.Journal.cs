using System.Buffers.Binary;
using System.Text;
using System.Text.Json;

namespace BookOfEternityClient.Core;

internal sealed partial class TrustedLocalFilePublication
{
    private static readonly byte[] FrameMagic = Encoding.ASCII.GetBytes("BOELP2\r\n");
    /// <summary>
    /// Writes one complete self-contained journal before its private stage can become authority.
    /// </summary>
    /// <param name="path">
    /// The absent private journal stage in the runtime scope.
    /// </param>
    /// <param name="journal">
    /// The validated logical member inventory and decision to encode.
    /// </param>
    /// <returns>
    /// The original v1 journal or a v2 journal whose images reopen the completed frame.
    /// </returns>
    private Journal WriteJournal(string path, Journal journal)
    {
        if (journal.Format == 1)
        {
            WriteNew(_journalScope, path, JsonSerializer.SerializeToUtf8Bytes(journal, JsonOptions));
            return journal;
        }

        long offset;
        long payloadStart;
        long fileLength;
        using (var output = new FileStream(_journalScope.ValidateFile(path), FileMode.CreateNew,
                   FileAccess.ReadWrite, FileShare.None, TrustedLocalFileImage.CopyBufferSize, FileOptions.SequentialScan))
        {
            output.Write(FrameMagic);
            Span<byte> lengthBytes = stackalloc byte[8];
            lengthBytes.Clear(); output.Write(lengthBytes);
            offset = WriteFrameMetadata(output, journal);
            payloadStart = output.Position;
            BinaryPrimitives.WriteInt64LittleEndian(lengthBytes, checked(payloadStart - 16));
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
        var regions = new FrameRegionBinder(_journalScope, path, payloadStart, fileLength);
        var rebound = journal with
        {
            Members = journal.Members.Select(member => new Member
            {
                Path = member.Path, Before = regions.BindWrittenImage(member.Before),
                After = regions.BindWrittenImage(member.After)
            }).ToArray()
        };
        regions.EnsureComplete();
        return rebound;
    }

    /// <summary>
    /// Dispatches supported authority formats and validates the complete journal before recovery.
    /// </summary>
    /// <param name="path">
    /// The existing owned authority journal to read.
    /// </param>
    /// <returns>
    /// A fully admitted v1 or v2 journal with exact image sources.
    /// </returns>
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
            if (length <= 0 || length > stream.Length - 16)
                throw Conflict("The v2 metadata length is invalid.");
            var journal = ReadFrameMetadata(stream, length, path, checked(16 + length), stream.Length);
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

    private static Journal RebindJournal(Journal journal, string path) => journal.Format == 1 ? journal : journal with
    {
        Members = journal.Members.Select(member => member with
        { Before = member.Before.RebindJournal(path), After = member.After.RebindJournal(path) }).ToArray()
    };

}
