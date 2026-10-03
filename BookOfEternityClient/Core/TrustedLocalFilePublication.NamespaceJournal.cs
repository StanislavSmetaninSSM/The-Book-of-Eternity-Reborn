using System.Buffers.Binary;
using System.Text;
using System.Text.Json;

namespace BookOfEternityClient.Core;

internal sealed partial class TrustedLocalFilePublication
{
    private static readonly byte[] NamespaceFrameMagic = Encoding.ASCII.GetBytes("BOELP3\r\n");

    /// <summary>
    /// Recognizes the namespace frame version before the unchanged legacy journal decoder runs.
    /// </summary>
    /// <param name="path">
    /// The existing owned authority file whose prefix is inspected.
    /// </param>
    /// <returns>
    /// <see langword="true"/> for exact v3 magic; otherwise <see langword="false"/>.
    /// </returns>
    private bool HasNamespaceJournalMagic(string path)
    {
        using var input = new FileStream(_journalScope.ValidateFile(path, allowMissing: false),
            FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
        Span<byte> magic = stackalloc byte[8];
        var count = input.ReadAtLeast(magic, magic.Length, throwOnEndOfStream: false);
        return count == magic.Length && magic.SequenceEqual(NamespaceFrameMagic);
    }

    /// <summary>
    /// Writes complete private v3 authority with streamed metadata and self-contained exact file images.
    /// </summary>
    /// <param name="path">
    /// The absent owned intent or commit stage path.
    /// </param>
    /// <param name="journal">
    /// The complete validated namespace evidence and decision to encode.
    /// </param>
    /// <returns>
    /// The same decision with every file image backed by the closed complete frame.
    /// </returns>
    private NamespaceJournal WriteNamespaceJournal(string path, NamespaceJournal journal)
    {
        long payloadStart;
        long fileLength;
        using (var output = new FileStream(_journalScope.ValidateFile(path), FileMode.CreateNew,
                   FileAccess.ReadWrite, FileShare.None, TrustedLocalFileImage.CopyBufferSize, FileOptions.SequentialScan))
        {
            output.Write(NamespaceFrameMagic);
            Span<byte> lengthBytes = stackalloc byte[8];
            lengthBytes.Clear();
            output.Write(lengthBytes);
            var payloadLength = WriteNamespaceFrameMetadata(output, journal);
            payloadStart = output.Position;
            BinaryPrimitives.WriteInt64LittleEndian(lengthBytes, checked(payloadStart - 16));
            output.Position = 8;
            output.Write(lengthBytes);
            output.Position = payloadStart;
            foreach (var member in journal.Members)
            {
                if (member.Before.Kind == TrustedLocalNamespaceKind.File) member.Before.FileImage!.CopyTo(output);
                if (member.After.Kind == TrustedLocalNamespaceKind.File) member.After.FileImage!.CopyTo(output);
            }
            fileLength = checked(payloadStart + payloadLength);
            if (output.Position != fileLength) throw Conflict("The v3 publication payload length is inconsistent.");
            output.Flush(flushToDisk: true);
        }
        var regions = new FrameRegionBinder(_journalScope, path, payloadStart, fileLength);
        var rebound = journal with
        {
            Members = journal.Members.Select(member => member with
            {
                Before = BindWrittenNamespaceFrameImage(member.Before, regions),
                After = BindWrittenNamespaceFrameImage(member.After, regions)
            }).ToArray()
        };
        regions.EnsureComplete();
        return rebound;
    }

    /// <summary>
    /// Reads strict v3 metadata and applies complete namespace validation before returning recovery authority.
    /// </summary>
    /// <param name="path">
    /// The existing owned frame whose complete metadata and payload regions are admitted.
    /// </param>
    /// <returns>
    /// Fully validated v3 evidence referencing only its self-contained owned frame.
    /// </returns>
    private NamespaceJournal ReadNamespaceJournal(string path)
    {
        using var input = new FileStream(_journalScope.ValidateFile(path, allowMissing: false),
            FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete,
            TrustedLocalFileImage.CopyBufferSize, FileOptions.SequentialScan);
        try
        {
            Span<byte> prefix = stackalloc byte[16];
            input.ReadExactly(prefix);
            if (!prefix[..8].SequenceEqual(NamespaceFrameMagic)) throw Conflict("The v3 frame magic is invalid.");
            var length = BinaryPrimitives.ReadInt64LittleEndian(prefix[8..]);
            if (length <= 0 || length > input.Length - 16) throw Conflict("The v3 metadata length is invalid.");
            var journal = ReadNamespaceFrameMetadata(input, length, path, checked(16 + length), input.Length);
            ValidateNamespaceJournal(journal);
            return journal;
        }
        catch (Exception failure) when (failure is JsonException or InvalidDataException or EndOfStreamException or OverflowException)
        {
            throw new InvalidDataException("The v3 publication frame is invalid; evidence retained.", failure);
        }
    }

    /// <summary>
    /// Rebinds exact region sources after a completed private frame becomes active authority by name.
    /// </summary>
    /// <param name="journal">
    /// The self-contained completed namespace evidence.
    /// </param>
    /// <param name="path">
    /// The new owned frame path after its atomic rename.
    /// </param>
    /// <returns>
    /// The same decision and boundaries with file images reopening the renamed frame.
    /// </returns>
    private static NamespaceJournal RebindNamespaceJournal(NamespaceJournal journal, string path) => journal with
    {
        Members = journal.Members.Select(member => member with
        {
            Before = RebindNamespaceFrameImage(member.Before, path),
            After = RebindNamespaceFrameImage(member.After, path)
        }).ToArray()
    };

    /// <summary>
    /// Streams the exact v3 root, member and boundary fields while retaining only one entry's encoded bytes.
    /// </summary>
    /// <param name="output">
    /// The owned frame stream positioned after its prefix and left open on return.
    /// </param>
    /// <param name="journal">
    /// The complete validated namespace evidence.
    /// </param>
    /// <returns>
    /// The checked total file-image payload length.
    /// </returns>
    private long WriteNamespaceFrameMetadata(Stream output, NamespaceJournal journal)
    {
        using var writer = new Utf8JsonWriter(output);
        writer.WriteStartObject();
        writer.WriteNumber("Format", 3);
        writer.WriteString("TransactionId", journal.TransactionId);
        writer.WriteBoolean("Committed", journal.Committed);
        WriteFrameGeneration(writer, "GenerationBefore", journal.GenerationBefore);
        WriteFrameGeneration(writer, "GenerationAfter", journal.GenerationAfter);
        writer.WriteString("NamespaceRoot", journal.NamespaceRoot);
        writer.WriteStartArray("Members");
        FlushFrameMetadata(writer, output, -1);
        long offset = 0;
        for (var index = 0; index < journal.Members.Length; index++)
        {
            var member = journal.Members[index];
            writer.WriteStartObject();
            writer.WriteString("Path", member.Path);
            WriteNamespaceFrameImage(writer, "Before", member.Before, ref offset);
            WriteNamespaceFrameImage(writer, "After", member.After, ref offset);
            writer.WriteEndObject();
            FlushFrameMetadata(writer, output, index);
        }
        writer.WriteEndArray();
        writer.WriteStartArray("Boundaries");
        foreach (var boundary in journal.Boundaries)
        {
            writer.WriteStartObject();
            writer.WriteString("Path", boundary.Path);
            writer.WriteString("Kind", NamespaceFrameKindText(boundary.Kind));
            writer.WriteNumber("Length", boundary.Length);
            writer.WriteString("Sha256", boundary.Sha256);
            writer.WriteEndObject();
            // Boundaries carry no payload and are independently flushed entries.
            // The existing member-index observer retains its original domain.
            writer.Flush();
        }
        writer.WriteEndArray();
        writer.WriteEndObject();
        writer.Flush();
        return offset;
    }

    /// <summary>
    /// Writes required kind, length, hash and offset fields without creating a second metadata graph.
    /// </summary>
    /// <param name="writer">
    /// The active metadata writer.
    /// </param>
    /// <param name="name">
    /// The before-image or after-image property name.
    /// </param>
    /// <param name="image">
    /// The validated namespace image whose file state owns exact bytes.
    /// </param>
    /// <param name="offset">
    /// The checked running payload offset, advanced only for a file state.
    /// </param>
    private static void WriteNamespaceFrameImage(Utf8JsonWriter writer, string name,
        TrustedLocalNamespaceImage image, ref long offset)
    {
        writer.WriteStartObject(name);
        writer.WriteString("Kind", NamespaceFrameKindText(image.Kind));
        if (image.Kind == TrustedLocalNamespaceKind.File)
        {
            var file = image.FileImage ?? throw Conflict("The v3 file image is missing.");
            if (!file.Exists) throw Conflict("The v3 file image is absent.");
            writer.WriteNumber("Length", file.Length);
            writer.WriteString("Sha256", file.Sha256);
            writer.WriteNumber("Offset", offset);
            offset = checked(offset + file.Length);
        }
        else
        {
            if (image.FileImage != null) throw Conflict("A non-file v3 image owns file bytes.");
            writer.WriteNumber("Length", 0);
            writer.WriteNull("Sha256");
            writer.WriteNull("Offset");
        }
        writer.WriteEndObject();
    }

    /// <summary>
    /// Converts only the admitted namespace kinds to their exact case-sensitive wire spellings.
    /// </summary>
    /// <param name="kind">
    /// The namespace kind to encode.
    /// </param>
    /// <returns>
    /// The exact Missing, Directory or File wire value.
    /// </returns>
    private static string NamespaceFrameKindText(TrustedLocalNamespaceKind kind) => kind switch
    {
        TrustedLocalNamespaceKind.Missing => "Missing",
        TrustedLocalNamespaceKind.Directory => "Directory",
        TrustedLocalNamespaceKind.File => "File",
        _ => throw Conflict("The v3 namespace kind is invalid.")
    };

    /// <summary>
    /// Parses v3 root fields in any supported order using the existing physical-region token cursor.
    /// </summary>
    /// <param name="input">
    /// The owned frame stream positioned at its metadata start.
    /// </param>
    /// <param name="length">
    /// The positive metadata length already confined to the complete physical frame.
    /// </param>
    /// <param name="path">
    /// The owned frame path for exact file-image sources.
    /// </param>
    /// <param name="payloadStart">
    /// The checked starting position of the contiguous file-image payload.
    /// </param>
    /// <param name="fileLength">
    /// The complete physical frame length.
    /// </param>
    /// <returns>
    /// Structurally admitted evidence ready for the complete namespace validator.
    /// </returns>
    private NamespaceJournal ReadNamespaceFrameMetadata(Stream input, long length, string path, long payloadStart, long fileLength)
    {
        var tokens = new FrameMetadataTokenReader(input, length, _metadataObserver);
        var regions = new FrameRegionBinder(_journalScope, path, payloadStart, fileLength);
        RequireToken(tokens.ReadRequired(), JsonTokenType.StartObject);
        var seen = 0;
        long format = 0;
        string? transaction = null;
        var committed = false;
        TrustedLocalGeneration? before = null;
        TrustedLocalGeneration? after = null;
        string? root = null;
        List<TrustedLocalNamespaceChange>? members = null;
        List<TrustedLocalNamespaceBoundary>? boundaries = null;
        while (true)
        {
            var token = tokens.ReadRequired();
            if (token.Type == JsonTokenType.EndObject) break;
            RequireToken(token, JsonTokenType.PropertyName);
            switch (ClaimFrameField(ref seen, token.Text switch
            {
                "Format" => 1, "TransactionId" => 2, "Committed" => 4,
                "GenerationBefore" => 8, "GenerationAfter" => 16, "NamespaceRoot" => 32,
                "Members" => 64, "Boundaries" => 128, _ => 0
            }))
            {
                case 1: format = ReadFrameInteger(tokens.ReadRequired()); break;
                case 2: transaction = ReadFrameString(tokens.ReadRequired()); break;
                case 4: committed = ReadFrameBoolean(tokens.ReadRequired()); break;
                case 8: before = ReadFrameGeneration(tokens); break;
                case 16: after = ReadFrameGeneration(tokens); break;
                case 32: root = ReadFrameString(tokens.ReadRequired()); break;
                case 64: members = ReadNamespaceFrameMembers(tokens, regions); break;
                case 128: boundaries = ReadNamespaceFrameBoundaries(tokens); break;
            }
        }
        RequireFrameFields(seen, 255);
        if (tokens.Read(out _)) throw Conflict("The v3 metadata contains an extra JSON root.");
        if (format != 3 || members is not { Count: > 0 }) throw Conflict("The v3 frame shape is invalid.");
        regions.EnsureComplete();
        return new NamespaceJournal
        {
            Format = 3, TransactionId = transaction!, Committed = committed,
            GenerationBefore = before!, GenerationAfter = after!, NamespaceRoot = root!,
            Members = members.ToArray(), Boundaries = boundaries!.ToArray()
        };
    }

    /// <summary>
    /// Produces one logical member inventory in array order while discarding each encoded entry.
    /// </summary>
    /// <param name="tokens">
    /// The strict metadata-only token cursor before the member array.
    /// </param>
    /// <param name="regions">
    /// The shared checked contiguous file-region binder.
    /// </param>
    /// <returns>
    /// The complete accumulated final namespace members.
    /// </returns>
    private static List<TrustedLocalNamespaceChange> ReadNamespaceFrameMembers(FrameMetadataTokenReader tokens, FrameRegionBinder regions)
    {
        RequireToken(tokens.ReadRequired(), JsonTokenType.StartArray);
        var members = new List<TrustedLocalNamespaceChange>();
        while (true)
        {
            var token = tokens.ReadRequired();
            if (token.Type == JsonTokenType.EndArray) return members;
            RequireToken(token, JsonTokenType.StartObject);
            var seen = 0;
            string? path = null;
            NamespaceFrameImageDescriptor before = default;
            NamespaceFrameImageDescriptor after = default;
            while (true)
            {
                token = tokens.ReadRequired();
                if (token.Type == JsonTokenType.EndObject) break;
                RequireToken(token, JsonTokenType.PropertyName);
                switch (ClaimFrameField(ref seen, token.Text switch { "Path" => 1, "Before" => 2, "After" => 4, _ => 0 }))
                {
                    case 1: path = ReadFrameString(tokens.ReadRequired()); break;
                    case 2: before = ReadNamespaceFrameImage(tokens); break;
                    case 4: after = ReadNamespaceFrameImage(tokens); break;
                }
            }
            RequireFrameFields(seen, 7);
            members.Add(new(path!, BindNamespaceFrameImage(before, regions), BindNamespaceFrameImage(after, regions)));
        }
    }

    /// <summary>
    /// Parses every required namespace image field before member-order payload binding.
    /// </summary>
    /// <param name="tokens">
    /// The metadata-only token cursor before an image object.
    /// </param>
    /// <returns>
    /// One temporary decoded image descriptor, with exact non-file semantics already checked.
    /// </returns>
    private static NamespaceFrameImageDescriptor ReadNamespaceFrameImage(FrameMetadataTokenReader tokens)
    {
        RequireToken(tokens.ReadRequired(), JsonTokenType.StartObject);
        var seen = 0;
        var kind = TrustedLocalNamespaceKind.Missing;
        long length = 0;
        string? hash = null;
        long? offset = null;
        while (true)
        {
            var token = tokens.ReadRequired();
            if (token.Type == JsonTokenType.EndObject) break;
            RequireToken(token, JsonTokenType.PropertyName);
            switch (ClaimFrameField(ref seen, token.Text switch
            { "Kind" => 1, "Length" => 2, "Sha256" => 4, "Offset" => 8, _ => 0 }))
            {
                case 1: kind = ReadNamespaceFrameKind(tokens.ReadRequired()); break;
                case 2: length = ReadFrameInteger(tokens.ReadRequired()); break;
                case 4: hash = ReadFrameNullableString(tokens.ReadRequired()); break;
                case 8:
                    var value = tokens.ReadRequired();
                    offset = value.Type == JsonTokenType.Null ? null : ReadFrameInteger(value);
                    break;
            }
        }
        RequireFrameFields(seen, 15);
        if (length < 0 || (kind != TrustedLocalNamespaceKind.File && (length != 0 || hash != null || offset != null)))
            throw Conflict("The v3 namespace image fields are inconsistent with its kind.");
        return new(kind, length, hash, offset);
    }

    /// <summary>
    /// Produces complete immutable boundary descriptors without payload offsets or filesystem authority.
    /// </summary>
    /// <param name="tokens">
    /// The metadata-only cursor before the boundary array.
    /// </param>
    /// <returns>
    /// The complete structurally admitted boundary inventory.
    /// </returns>
    private static List<TrustedLocalNamespaceBoundary> ReadNamespaceFrameBoundaries(FrameMetadataTokenReader tokens)
    {
        RequireToken(tokens.ReadRequired(), JsonTokenType.StartArray);
        var boundaries = new List<TrustedLocalNamespaceBoundary>();
        while (true)
        {
            var token = tokens.ReadRequired();
            if (token.Type == JsonTokenType.EndArray) return boundaries;
            RequireToken(token, JsonTokenType.StartObject);
            var seen = 0;
            string? path = null;
            var kind = TrustedLocalNamespaceKind.Missing;
            long length = 0;
            string? hash = null;
            while (true)
            {
                token = tokens.ReadRequired();
                if (token.Type == JsonTokenType.EndObject) break;
                RequireToken(token, JsonTokenType.PropertyName);
                switch (ClaimFrameField(ref seen, token.Text switch
                { "Path" => 1, "Kind" => 2, "Length" => 4, "Sha256" => 8, _ => 0 }))
                {
                    case 1: path = ReadFrameString(tokens.ReadRequired()); break;
                    case 2: kind = ReadNamespaceFrameKind(tokens.ReadRequired()); break;
                    case 4: length = ReadFrameInteger(tokens.ReadRequired()); break;
                    case 8: hash = ReadFrameNullableString(tokens.ReadRequired()); break;
                }
            }
            RequireFrameFields(seen, 15);
            if (length < 0 || (kind == TrustedLocalNamespaceKind.File ? hash == null : length != 0 || hash != null))
                throw Conflict("The v3 boundary fields are inconsistent with its kind.");
            boundaries.Add(new(path!, kind, length, hash));
        }
    }

    /// <summary>
    /// Decodes only the exact case-sensitive namespace kind strings supported by v3.
    /// </summary>
    /// <param name="token">
    /// The completed kind value token.
    /// </param>
    /// <returns>
    /// Its supported namespace kind; other spellings and value types are refused.
    /// </returns>
    private static TrustedLocalNamespaceKind ReadNamespaceFrameKind(FrameMetadataToken token) => ReadFrameString(token) switch
    {
        "Missing" => TrustedLocalNamespaceKind.Missing,
        "Directory" => TrustedLocalNamespaceKind.Directory,
        "File" => TrustedLocalNamespaceKind.File,
        _ => throw Conflict("The v3 namespace kind is invalid.")
    };

    /// <summary>
    /// Binds only file-kind descriptors into the checked contiguous payload inventory.
    /// </summary>
    /// <param name="image">
    /// The temporary image fields already admitted for their kind.
    /// </param>
    /// <param name="regions">
    /// The checked payload binder in member, before-image, after-image order.
    /// </param>
    /// <returns>
    /// The final namespace image with exact owned-frame bytes only for file states.
    /// </returns>
    private static TrustedLocalNamespaceImage BindNamespaceFrameImage(NamespaceFrameImageDescriptor image, FrameRegionBinder regions) =>
        new(image.Kind, image.Kind == TrustedLocalNamespaceKind.File
            ? regions.Bind(new(true, image.Length, image.Sha256, image.Offset)) : null);

    /// <summary>
    /// Rebinds a written file image to a closed frame while preserving missing and directory descriptors.
    /// </summary>
    /// <param name="image">
    /// The validated namespace image copied into the frame.
    /// </param>
    /// <param name="regions">
    /// The checked complete payload binder.
    /// </param>
    /// <returns>
    /// The same non-file image or a final region-backed file image.
    /// </returns>
    private static TrustedLocalNamespaceImage BindWrittenNamespaceFrameImage(TrustedLocalNamespaceImage image, FrameRegionBinder regions) =>
        image.Kind == TrustedLocalNamespaceKind.File
            ? new(image.Kind, regions.BindWrittenImage(image.FileImage!)) : image;

    /// <summary>
    /// Changes only a file image's owned journal source after frame name publication.
    /// </summary>
    /// <param name="image">
    /// The namespace image to preserve or rebind.
    /// </param>
    /// <param name="path">
    /// The new exact owned frame path.
    /// </param>
    /// <returns>
    /// The unchanged non-file descriptor or equivalent file descriptor reopening the new path.
    /// </returns>
    private static TrustedLocalNamespaceImage RebindNamespaceFrameImage(TrustedLocalNamespaceImage image, string path) =>
        image.Kind == TrustedLocalNamespaceKind.File ? image with { FileImage = image.FileImage!.RebindJournal(path) } : image;

    /// <summary>
    /// Holds only the current member's temporary wire image values before exact region binding.
    /// </summary>
    /// <param name="Kind">
    /// The exact decoded namespace kind.
    /// </param>
    /// <param name="Length">
    /// The declared nonnegative byte length, zero for non-file kinds.
    /// </param>
    /// <param name="Sha256">
    /// The required nullable exact file hash.
    /// </param>
    /// <param name="Offset">
    /// The required nullable payload-relative offset.
    /// </param>
    private readonly record struct NamespaceFrameImageDescriptor(TrustedLocalNamespaceKind Kind, long Length, string? Sha256, long? Offset);

    /// <summary>
    /// Records v3 namespace authority independently from the unchanged legacy v1 and v2 serialized shapes.
    /// </summary>
    private sealed record NamespaceJournal
    {
        /// <summary>
        /// Gets the namespace frame version, which must be three.
        /// </summary>
        public required int Format { get; init; }
        /// <summary>
        /// Gets the canonical transaction identifier owning the frame and derived scratch names.
        /// </summary>
        public required string TransactionId { get; init; }
        /// <summary>
        /// Gets whether this frame establishes the committed after-state decision.
        /// </summary>
        public required bool Committed { get; init; }
        /// <summary>
        /// Gets the exact logical generation binding required for rollback.
        /// </summary>
        public required TrustedLocalGeneration GenerationBefore { get; init; }
        /// <summary>
        /// Gets the exact logical generation binding established by commit.
        /// </summary>
        public required TrustedLocalGeneration GenerationAfter { get; init; }
        /// <summary>
        /// Gets the exact normalized immutable game-session directory root.
        /// </summary>
        public required string NamespaceRoot { get; init; }
        /// <summary>
        /// Gets the complete unique namespace member inventory in stable frame order.
        /// </summary>
        public required TrustedLocalNamespaceChange[] Members { get; init; }
        /// <summary>
        /// Gets the immutable manager-admitted canonical library and selected-source boundaries.
        /// </summary>
        public required TrustedLocalNamespaceBoundary[] Boundaries { get; init; }
    }
}
