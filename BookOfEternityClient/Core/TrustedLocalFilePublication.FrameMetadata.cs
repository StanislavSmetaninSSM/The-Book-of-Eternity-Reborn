using System.Text.Json;

namespace BookOfEternityClient.Core;

internal sealed partial class TrustedLocalFilePublication
{
    /// <summary>
    /// Streams the existing v2 wire fields directly from the logical inventory, flushing every member.
    /// </summary>
    /// <param name="output">
    /// The private frame stream positioned after the magic and length slot; remains open on return.
    /// </param>
    /// <param name="journal">
    /// The validated member inventory whose images determine contiguous payload offsets.
    /// </param>
    /// <returns>
    /// The checked total payload length in member, before-image, after-image order.
    /// </returns>
    private long WriteFrameMetadata(Stream output, Journal journal)
    {
        using var writer = new Utf8JsonWriter(output);
        writer.WriteStartObject();
        writer.WriteNumber("Format", 2);
        writer.WriteString("TransactionId", journal.TransactionId);
        writer.WriteBoolean("Committed", journal.Committed);
        WriteFrameGeneration(writer, "GenerationBefore", journal.GenerationBefore);
        WriteFrameGeneration(writer, "GenerationAfter", journal.GenerationAfter);
        writer.WriteStartArray("Members");
        FlushFrameMetadata(writer, output, -1);
        long offset = 0;
        for (var index = 0; index < journal.Members.Length; index++)
        {
            var member = journal.Members[index];
            writer.WriteStartObject();
            writer.WriteString("Path", member.Path);
            WriteFrameImage(writer, "Before", member.Before, ref offset);
            WriteFrameImage(writer, "After", member.After, ref offset);
            writer.WriteEndObject();
            FlushFrameMetadata(writer, output, index);
        }
        writer.WriteEndArray();
        writer.WriteEndObject();
        writer.Flush();
        return offset;
    }

    /// <summary>
    /// Flushes pending metadata to the actual destination before reporting the completed encoding boundary.
    /// </summary>
    /// <param name="writer">
    /// The one active writer retaining JSON state for the complete header.
    /// </param>
    /// <param name="output">
    /// The owned stream receiving the flushed bytes.
    /// </param>
    /// <param name="memberIndex">
    /// The completed member index, or minus one for the fixed header opening.
    /// </param>
    private void FlushFrameMetadata(Utf8JsonWriter writer, Stream output, int memberIndex)
    {
        var pending = writer.BytesPending;
        writer.Flush();
        _metadataObserver?.Invoke(new(TrustedLocalFrameMetadataObservationKind.WriterFlushed,
            memberIndex, pending, writer.BytesPending, output.Position, 0, 0));
    }

    /// <summary>
    /// Emits the unchanged generation object fields, including a required nullable ID.
    /// </summary>
    /// <param name="writer">
    /// The current metadata writer.
    /// </param>
    /// <param name="name">
    /// The generation property name in its enclosing object.
    /// </param>
    /// <param name="generation">
    /// The validated presence and identifier binding.
    /// </param>
    private static void WriteFrameGeneration(Utf8JsonWriter writer, string name, TrustedLocalGeneration generation)
    {
        writer.WriteStartObject(name);
        writer.WriteBoolean("Exists", generation.Exists);
        writer.WriteString("Id", generation.Id);
        writer.WriteEndObject();
    }

    /// <summary>
    /// Emits one image descriptor without building a second header graph.
    /// </summary>
    /// <param name="writer">
    /// The current metadata writer.
    /// </param>
    /// <param name="name">
    /// The before-image or after-image property name.
    /// </param>
    /// <param name="image">
    /// The validated exact image or explicit absence.
    /// </param>
    /// <param name="offset">
    /// The checked running payload offset, advanced only for present images.
    /// </param>
    private static void WriteFrameImage(Utf8JsonWriter writer, string name, TrustedLocalFileImage image, ref long offset)
    {
        writer.WriteStartObject(name);
        writer.WriteBoolean("Exists", image.Exists);
        writer.WriteNumber("Length", image.Length);
        writer.WriteString("Sha256", image.Sha256);
        if (image.Exists)
        {
            writer.WriteNumber("Offset", offset);
            offset = checked(offset + image.Length);
        }
        else writer.WriteNull("Offset");
        writer.WriteEndObject();
    }

    /// <summary>
    /// Reads the complete strict v2 schema from the physical metadata region into one logical member inventory.
    /// </summary>
    /// <param name="input">
    /// The owned frame stream positioned at the metadata start.
    /// </param>
    /// <param name="metadataLength">
    /// The positive metadata length already confined to the physical frame.
    /// </param>
    /// <param name="path">
    /// The owned frame path used only for exact payload regions.
    /// </param>
    /// <param name="payloadStart">
    /// The checked first payload position after the metadata region.
    /// </param>
    /// <param name="fileLength">
    /// The complete physical frame length.
    /// </param>
    /// <returns>
    /// The decoded journal, ready for unchanged complete path, hash and generation admission.
    /// </returns>
    private Journal ReadFrameMetadata(Stream input, long metadataLength, string path, long payloadStart, long fileLength)
    {
        var tokens = new FrameMetadataTokenReader(input, metadataLength, _metadataObserver);
        var regions = new FrameRegionBinder(_journalScope, path, payloadStart, fileLength);
        RequireToken(tokens.ReadRequired(), JsonTokenType.StartObject);
        var seen = 0;
        long format = 0;
        string? transaction = null;
        var committed = false;
        TrustedLocalGeneration? before = null;
        TrustedLocalGeneration? after = null;
        List<Member>? members = null;
        while (true)
        {
            var token = tokens.ReadRequired();
            if (token.Type == JsonTokenType.EndObject) break;
            RequireToken(token, JsonTokenType.PropertyName);
            var field = ClaimFrameField(ref seen, token.Text switch
            {
                "Format" => 1, "TransactionId" => 2, "Committed" => 4,
                "GenerationBefore" => 8, "GenerationAfter" => 16, "Members" => 32, _ => 0
            });
            switch (field)
            {
                case 1: format = ReadFrameInteger(tokens.ReadRequired()); break;
                case 2: transaction = ReadFrameString(tokens.ReadRequired()); break;
                case 4: committed = ReadFrameBoolean(tokens.ReadRequired()); break;
                case 8: before = ReadFrameGeneration(tokens); break;
                case 16: after = ReadFrameGeneration(tokens); break;
                case 32: members = ReadFrameMembers(tokens, regions); break;
            }
        }
        RequireFrameFields(seen, 63);
        if (tokens.Read(out _)) throw Conflict("The metadata contains an extra JSON root.");
        if (format != 2 || members is not { Count: > 0 }) throw Conflict("The v2 frame shape is invalid.");
        regions.EnsureComplete();
        return new Journal
        {
            Format = 2, TransactionId = transaction!, Committed = committed,
            GenerationBefore = before!, GenerationAfter = after!, Members = members.ToArray()
        };
    }

    /// <summary>
    /// Accumulates final member descriptors in array order without retaining an encoded header or metadata DOM.
    /// </summary>
    /// <param name="tokens">
    /// The strict metadata-only token cursor.
    /// </param>
    /// <param name="regions">
    /// The shared contiguous payload binder.
    /// </param>
    /// <returns>
    /// The single accumulated logical member inventory.
    /// </returns>
    private static List<Member> ReadFrameMembers(FrameMetadataTokenReader tokens, FrameRegionBinder regions)
    {
        RequireToken(tokens.ReadRequired(), JsonTokenType.StartArray);
        var members = new List<Member>();
        while (true)
        {
            var token = tokens.ReadRequired();
            if (token.Type == JsonTokenType.EndArray) return members;
            RequireToken(token, JsonTokenType.StartObject);
            members.Add(ReadFrameMember(tokens, regions));
        }
    }

    /// <summary>
    /// Parses one member and binds before then after regions irrespective of object property order.
    /// </summary>
    /// <param name="tokens">
    /// The cursor positioned just after the member's opening object token.
    /// </param>
    /// <param name="regions">
    /// The contiguous payload binder in member array order.
    /// </param>
    /// <returns>
    /// A final logical member referencing only the owned frame payload.
    /// </returns>
    private static Member ReadFrameMember(FrameMetadataTokenReader tokens, FrameRegionBinder regions)
    {
        var seen = 0;
        string? path = null;
        FrameImageDescriptor before = default;
        FrameImageDescriptor after = default;
        while (true)
        {
            var token = tokens.ReadRequired();
            if (token.Type == JsonTokenType.EndObject) break;
            RequireToken(token, JsonTokenType.PropertyName);
            switch (ClaimFrameField(ref seen, token.Text switch
            { "Path" => 1, "Before" => 2, "After" => 4, _ => 0 }))
            {
                case 1: path = ReadFrameString(tokens.ReadRequired()); break;
                case 2: before = ReadFrameImage(tokens); break;
                case 4: after = ReadFrameImage(tokens); break;
            }
        }
        RequireFrameFields(seen, 7);
        return new Member { Path = path!, Before = regions.Bind(before), After = regions.Bind(after) };
    }

    /// <summary>
    /// Parses a required generation object with its original nullable identifier semantics.
    /// </summary>
    /// <param name="tokens">
    /// The metadata-only cursor before the generation value.
    /// </param>
    /// <returns>
    /// The validated presence and generation identifier binding.
    /// </returns>
    private static TrustedLocalGeneration ReadFrameGeneration(FrameMetadataTokenReader tokens)
    {
        RequireToken(tokens.ReadRequired(), JsonTokenType.StartObject);
        var seen = 0;
        var exists = false;
        string? id = null;
        while (true)
        {
            var token = tokens.ReadRequired();
            if (token.Type == JsonTokenType.EndObject) break;
            RequireToken(token, JsonTokenType.PropertyName);
            switch (ClaimFrameField(ref seen, token.Text switch { "Exists" => 1, "Id" => 2, _ => 0 }))
            {
                case 1: exists = ReadFrameBoolean(tokens.ReadRequired()); break;
                case 2: id = ReadFrameNullableString(tokens.ReadRequired()); break;
            }
        }
        RequireFrameFields(seen, 3);
        var result = new TrustedLocalGeneration(exists, id);
        ValidateGeneration(result);
        return result;
    }

    /// <summary>
    /// Parses all required image fields into only the current member's temporary value descriptor.
    /// </summary>
    /// <param name="tokens">
    /// The metadata-only cursor before the image value.
    /// </param>
    /// <returns>
    /// The decoded presence, length, hash and nullable offset fields.
    /// </returns>
    private static FrameImageDescriptor ReadFrameImage(FrameMetadataTokenReader tokens)
    {
        RequireToken(tokens.ReadRequired(), JsonTokenType.StartObject);
        var seen = 0;
        var exists = false;
        long length = 0;
        string? hash = null;
        long? offset = null;
        while (true)
        {
            var token = tokens.ReadRequired();
            if (token.Type == JsonTokenType.EndObject) break;
            RequireToken(token, JsonTokenType.PropertyName);
            switch (ClaimFrameField(ref seen, token.Text switch
            { "Exists" => 1, "Length" => 2, "Sha256" => 4, "Offset" => 8, _ => 0 }))
            {
                case 1: exists = ReadFrameBoolean(tokens.ReadRequired()); break;
                case 2: length = ReadFrameInteger(tokens.ReadRequired()); break;
                case 4: hash = ReadFrameNullableString(tokens.ReadRequired()); break;
                case 8:
                    var value = tokens.ReadRequired();
                    offset = value.Type == JsonTokenType.Null ? null : ReadFrameInteger(value);
                    break;
            }
        }
        RequireFrameFields(seen, 15);
        return new(exists, length, hash, offset);
    }

    /// <summary>
    /// Records one decoded property bit, rejecting unknown or duplicate names at the current object level.
    /// </summary>
    /// <param name="seen">
    /// The current object's seen-field bitset, updated for an admitted field.
    /// </param>
    /// <param name="field">
    /// The bit assigned from an ordinal decoded name, or zero for an unknown name.
    /// </param>
    /// <returns>
    /// The newly recorded field bit.
    /// </returns>
    private static int ClaimFrameField(ref int seen, int field)
    {
        if (field == 0 || (seen & field) != 0) throw Conflict("The metadata contains an unknown or duplicate property.");
        seen |= field;
        return field;
    }

    /// <summary>
    /// Requires the complete set of fields, including required nullable fields.
    /// </summary>
    /// <param name="seen">
    /// The object's admitted field bitset.
    /// </param>
    /// <param name="required">
    /// The complete required-field mask for that shape.
    /// </param>
    private static void RequireFrameFields(int seen, int required)
    {
        if (seen != required) throw Conflict("The metadata is missing a required field.");
    }

    /// <summary>
    /// Rejects an unexpected scalar, null or container token before interpreting its value.
    /// </summary>
    /// <param name="token">
    /// The actual completed token.
    /// </param>
    /// <param name="expected">
    /// The schema's exact required token type.
    /// </param>
    private static void RequireToken(FrameMetadataToken token, JsonTokenType expected)
    {
        if (token.Type != expected) throw Conflict("The metadata has an invalid value type.");
    }

    /// <summary>
    /// Reads one required non-null JSON string.
    /// </summary>
    /// <param name="token">
    /// The completed value token.
    /// </param>
    /// <returns>
    /// The decoded string, retaining the original Unicode and escape semantics.
    /// </returns>
    private static string ReadFrameString(FrameMetadataToken token)
    {
        RequireToken(token, JsonTokenType.String);
        return token.Text!;
    }

    /// <summary>
    /// Reads a string or explicit JSON null without making the field optional.
    /// </summary>
    /// <param name="token">
    /// The completed value token.
    /// </param>
    /// <returns>
    /// The decoded string or <see langword="null"/> for an explicit JSON null token.
    /// </returns>
    private static string? ReadFrameNullableString(FrameMetadataToken token) =>
        token.Type == JsonTokenType.Null ? null : ReadFrameString(token);

    /// <summary>
    /// Reads an exact supported signed integer rather than accepting strings, fractions or overflow.
    /// </summary>
    /// <param name="token">
    /// The completed value token.
    /// </param>
    /// <returns>
    /// Its signed 64-bit integer value.
    /// </returns>
    private static long ReadFrameInteger(FrameMetadataToken token)
    {
        if (token.Type != JsonTokenType.Number || !token.HasInteger)
            throw Conflict("The metadata number is not a supported integer.");
        return token.Integer;
    }

    /// <summary>
    /// Reads an exact JSON boolean.
    /// </summary>
    /// <param name="token">
    /// The completed value token.
    /// </param>
    /// <returns>
    /// <see langword="true"/> for a JSON true token and <see langword="false"/> for a JSON false token; other types are refused.
    /// </returns>
    private static bool ReadFrameBoolean(FrameMetadataToken token) => token.Type switch
    {
        JsonTokenType.True => true, JsonTokenType.False => false,
        _ => throw Conflict("The metadata has an invalid boolean type.")
    };

    /// <summary>
    /// Holds only the current image's wire values until member-order region binding.
    /// </summary>
    /// <param name="Exists">
    /// Whether the image is present.
    /// </param>
    /// <param name="Length">
    /// The declared exact byte length.
    /// </param>
    /// <param name="Sha256">
    /// The required nullable exact-byte hash.
    /// </param>
    /// <param name="Offset">
    /// The required nullable payload-relative offset.
    /// </param>
    private readonly record struct FrameImageDescriptor(bool Exists, long Length, string? Sha256, long? Offset);

    /// <summary>
    /// Enforces complete contiguous payload coverage while producing final region-backed images.
    /// </summary>
    private sealed class FrameRegionBinder
    {
        private readonly TrustedLocalFileScope _scope;
        private readonly string _path;
        private readonly long _payloadStart;
        private readonly long _fileLength;
        private long _offset;

        /// <summary>
        /// Creates a region binder for one already confined physical frame.
        /// </summary>
        /// <param name="scope">
        /// The owned journal file scope.
        /// </param>
        /// <param name="path">
        /// The exact frame file to reopen for images.
        /// </param>
        /// <param name="payloadStart">
        /// The checked payload starting position.
        /// </param>
        /// <param name="fileLength">
        /// The exact complete container length.
        /// </param>
        internal FrameRegionBinder(TrustedLocalFileScope scope, string path, long payloadStart, long fileLength)
        {
            if (payloadStart < 0 || payloadStart > fileLength) throw Conflict("The frame payload position is invalid.");
            _scope = scope;
            _path = path;
            _payloadStart = payloadStart;
            _fileLength = fileLength;
        }

        /// <summary>
        /// Binds one descriptor at the next exact offset, including valid empty images.
        /// </summary>
        /// <param name="image">
        /// The fully decoded required image fields.
        /// </param>
        /// <returns>
        /// An explicit absent image or exact owned-frame region descriptor.
        /// </returns>
        internal TrustedLocalFileImage Bind(FrameImageDescriptor image)
        {
            if (image.Length < 0) throw Conflict("The v2 image metadata is invalid.");
            if (!image.Exists)
            {
                if (image.Length != 0 || image.Offset != null || image.Sha256 != null)
                    throw Conflict("An absent v2 image has a region.");
                return TrustedLocalFileImage.FromBytes(null);
            }
            if (image.Offset != _offset || image.Sha256 == null || image.Length > _fileLength - _payloadStart - _offset)
                throw Conflict("The v2 image regions do not exactly cover their payload.");
            var result = TrustedLocalFileImage.FromRegion(_scope, _path, checked(_payloadStart + _offset),
                image.Length, _fileLength, image.Sha256);
            _offset = checked(_offset + image.Length);
            return result;
        }

        /// <summary>
        /// Rebinds an existing logical image after its complete private frame has been closed.
        /// </summary>
        /// <param name="image">
        /// The exact image copied in the existing member order.
        /// </param>
        /// <returns>
        /// The image backed by the closed self-contained frame or explicit absence.
        /// </returns>
        internal TrustedLocalFileImage BindWrittenImage(TrustedLocalFileImage image) =>
            Bind(new(image.Exists, image.Length, image.Sha256, image.Exists ? _offset : null));

        /// <summary>
        /// Rejects any missing or trailing payload after the complete member inventory has been bound.
        /// </summary>
        internal void EnsureComplete()
        {
            if (_offset != _fileLength - _payloadStart) throw Conflict("The v2 frame has missing or trailing image bytes.");
        }
    }

    /// <summary>
    /// Carries a decoded primitive token without retaining the encoded buffer slice.
    /// </summary>
    /// <param name="Type">
    /// The exact JSON token type.
    /// </param>
    /// <param name="Text">
    /// The decoded property name or string value, or <see langword="null"/> for other tokens.
    /// </param>
    /// <param name="Integer">
    /// The integer value when conversion succeeded.
    /// </param>
    /// <param name="HasInteger">
    /// Whether the numeric token is exactly representable as a signed 64-bit integer.
    /// </param>
    private readonly record struct FrameMetadataToken(JsonTokenType Type, string? Text, long Integer, bool HasInteger);

    /// <summary>
    /// Restricts reads to the physical metadata region and carries only an actual incomplete token between reads.
    /// </summary>
    private sealed class FrameMetadataTokenReader
    {
        private const int BufferSize = TrustedLocalFileImage.CopyBufferSize;
        private readonly Stream _input;
        private readonly Action<TrustedLocalFrameMetadataObservation>? _observer;
        private readonly byte[] _baseBuffer = new byte[BufferSize];
        private byte[] _buffer;
        private JsonReaderState _state;
        private long _remaining;
        private int _start;
        private int _end;

        /// <summary>
        /// Creates a metadata-only token cursor with a reusable 64 KiB I/O buffer.
        /// </summary>
        /// <param name="input">
        /// The caller-owned frame stream positioned at the start of its admitted metadata region.
        /// </param>
        /// <param name="length">
        /// The positive metadata region length, already bounded by the physical frame.
        /// </param>
        /// <param name="observer">
        /// The optional owned buffer diagnostic callback, or <see langword="null"/> to disable observations.
        /// </param>
        internal FrameMetadataTokenReader(Stream input, long length, Action<TrustedLocalFrameMetadataObservation>? observer)
        {
            _input = input;
            _remaining = length;
            _observer = observer;
            _buffer = _baseBuffer;
            _state = new JsonReaderState(new JsonReaderOptions
            {
                AllowTrailingCommas = JsonOptions.AllowTrailingCommas,
                CommentHandling = JsonOptions.ReadCommentHandling,
                MaxDepth = JsonOptions.MaxDepth
            });
        }

        /// <summary>
        /// Reads one required token, refusing metadata that ends before its schema is complete.
        /// </summary>
        /// <returns>
        /// The next decoded token.
        /// </returns>
        internal FrameMetadataToken ReadRequired() => Read(out var token)
            ? token : throw Conflict("The metadata ended before its schema was complete.");

        /// <summary>
        /// Advances one complete token without exposing image payload bytes to the JSON reader.
        /// </summary>
        /// <param name="token">
        /// The decoded next token, or its default value after complete metadata exhaustion.
        /// </param>
        /// <returns>
        /// <see langword="true"/> when one complete token is available; <see langword="false"/> only at the end of the metadata region.
        /// </returns>
        internal bool Read(out FrameMetadataToken token)
        {
            while (true)
            {
                var reader = new Utf8JsonReader(_buffer.AsSpan(_start, _end - _start), _remaining == 0, _state);
                if (reader.Read())
                {
                    string? text = null;
                    if (reader.TokenType is JsonTokenType.String or JsonTokenType.PropertyName)
                    {
                        try { text = reader.GetString(); }
                        catch (InvalidOperationException failure)
                        { throw new JsonException("The metadata string is not valid UTF-8.", failure); }
                    }
                    long integer = 0;
                    var hasInteger = reader.TokenType == JsonTokenType.Number && reader.TryGetInt64(out integer);
                    token = new(reader.TokenType, text, integer, hasInteger);
                    var tokenBytes = reader.ValueSpan.Length;
                    _state = reader.CurrentState;
                    _start += checked((int)reader.BytesConsumed);
                    ObserveBuffer(TrustedLocalFrameMetadataObservationKind.ReaderTokenCompleted, tokenBytes);
                    if (_buffer.Length > BufferSize)
                    {
                        var unread = _end - _start;
                        // Refills are at most one base buffer. Once its carried token
                        // completes, any following bytes fit back in the base storage.
                        _buffer.AsSpan(_start, unread).CopyTo(_baseBuffer);
                        _buffer = _baseBuffer;
                        _start = 0;
                        _end = unread;
                        ObserveBuffer(TrustedLocalFrameMetadataObservationKind.ReaderBufferReleased, tokenBytes);
                    }
                    return true;
                }
                _state = reader.CurrentState;
                _start += checked((int)reader.BytesConsumed);
                if (_remaining == 0)
                {
                    token = default;
                    return false;
                }

                var retained = _end - _start;
                if (_start > 0) _buffer.AsSpan(_start, retained).CopyTo(_buffer);
                _start = 0;
                _end = retained;
                if (retained == _buffer.Length)
                {
                    // Grow from the filled actual token carry, never the full declared
                    // header length. The physical region is the only transport bound.
                    var capacity = Math.Min(checked((long)_buffer.Length * 2), checked(retained + _remaining));
                    var grown = new byte[checked((int)capacity)];
                    _buffer.AsSpan(0, retained).CopyTo(grown);
                    _buffer = grown;
                    ObserveBuffer(TrustedLocalFrameMetadataObservationKind.ReaderBufferGrown, retained);
                }
                var count = (int)Math.Min(BufferSize, Math.Min(_buffer.Length - _end, _remaining));
                var read = _input.Read(_buffer.AsSpan(_end, count));
                if (read == 0) throw new EndOfStreamException("The physical metadata region was truncated.");
                _end += read;
                _remaining -= read;
            }
        }

        /// <summary>
        /// Reports actual token transport measurements without retaining string or payload data.
        /// </summary>
        /// <param name="kind">
        /// The completed token, buffer growth or carry release action.
        /// </param>
        /// <param name="tokenBytes">
        /// The actual retained or completed encoded token length.
        /// </param>
        private void ObserveBuffer(TrustedLocalFrameMetadataObservationKind kind, long tokenBytes) =>
            _observer?.Invoke(new(kind, -1, 0, 0, 0, _buffer.Length, tokenBytes));
    }
}
