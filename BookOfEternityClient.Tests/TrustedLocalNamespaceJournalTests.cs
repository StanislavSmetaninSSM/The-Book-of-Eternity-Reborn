using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Consumes independently encoded v3 authority through the normal lease and recovery entrypoints.
/// </summary>
public sealed class TrustedLocalNamespaceJournalTests : IDisposable
{
    private const string BeforeId = "11111111111111111111111111111111";
    private const string AfterId = "22222222222222222222222222222222";
    private const string Transaction = "33333333333333333333333333333333";
    private static readonly byte[] Before = [0xEF, 0xBB, 0xBF, 0xFF, 1];
    private static readonly byte[] After = [0xFE, 42, 0];
    private static readonly byte[] DeletedBefore = [11, 12, 255];
    private static readonly byte[] Sentinel = [99, 0, 0xFF];
    private static readonly byte[] BeforeGeneration = [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(
        "{\"schemaVersion\":1,\"generationId\":\"" + BeforeId + "\",\"extension\":true}")];
    private static readonly byte[] AfterGeneration = [.. Encoding.Unicode.GetPreamble(), .. Encoding.Unicode.GetBytes(
        "{\"SchemaVersion\":1,\"GenerationId\":\"" + AfterId + "\"}")];
    private readonly string _sandbox = Path.Combine(Path.GetTempPath(), "boe-v3-journal-" + Guid.NewGuid().ToString("N"));
    private readonly string _root;
    /// <summary>
    /// Names the covered replacement whose exact bytes prove earlier recovery ordering.
    /// </summary>
    private string Target => Path.Combine(_root, "game_session", "game_state", "core", "v3-replace.bin");
    /// <summary>
    /// Names the valid zero-length after-state-only file.
    /// </summary>
    private string Created => Path.Combine(Path.GetDirectoryName(Target)!, "v3-created-empty.bin");
    /// <summary>
    /// Names the before-state-only file restored during pending recovery.
    /// </summary>
    private string Deleted => Path.Combine(Path.GetDirectoryName(Target)!, "v3-deleted.bin");
    /// <summary>
    /// Names the unchanged exact-byte member within the covered namespace.
    /// </summary>
    private string SessionSentinel => Path.Combine(_root, "game_session", "v3-unchanged-sentinel.bin");
    /// <summary>
    /// Names owned fixture bytes outside the publication grant.
    /// </summary>
    private string OutsideSentinel => Path.Combine(_sandbox, "outside-sentinel.bin");
    /// <summary>
    /// Names the single existing publication authority file.
    /// </summary>
    private string Active => Path.Combine(_root, ".boe_runtime", "trusted-local-publication-v1", "active.json");
    /// <summary>
    /// Names the canonical opaque library boundary.
    /// </summary>
    private string Library => Manager().ResolvePath("saves");
    /// <summary>
    /// Names the immutable selected-source fixture nested inside the opaque library.
    /// </summary>
    private string Source => Path.Combine(Library, "manual_saves", "selected-source.zip");

    /// <summary>
    /// Creates an independently owned session already at the declared after state.
    /// </summary>
    public TrustedLocalNamespaceJournalTests()
    {
        _root = Path.Combine(_sandbox, "root");
        var files = Manager();
        files.EnsureDirectoryStructure();
        Directory.CreateDirectory(Path.GetDirectoryName(files.SessionGenerationPath)!);
        File.WriteAllBytes(files.SessionGenerationPath, AfterGeneration);
        File.WriteAllBytes(Target, After);
        File.WriteAllBytes(Created, []);
        File.WriteAllBytes(SessionSentinel, Sentinel);
        File.WriteAllBytes(OutsideSentinel, Sentinel);
        Directory.CreateDirectory(Path.GetDirectoryName(Source)!);
        File.WriteAllBytes(Source, Sentinel);
        File.WriteAllBytes(Path.Combine(Library, "library-sentinel.bin"), Sentinel);
    }

    /// <summary>
    /// Recovers independent v3 bytes, preserving exact decision, directories, generation and protected boundaries.
    /// </summary>
    /// <param name="committed">
    /// Selects committed after-state cleanup or pending exact before-state rollback.
    /// </param>
    /// <param name="reordered">
    /// Writes members before root headers and after-image fields before before-image fields.
    /// </param>
    /// <param name="libraryPresent">
    /// Uses the canonical opaque directory when true, or its admitted missing state when false.
    /// </param>
    /// <param name="sourceBoundary">
    /// Declares the exact selected file inside the opaque library as a nested read-only boundary.
    /// </param>
    /// <returns>
    /// A task that completes after normal lease recovery and exact supported namespace assertions.
    /// </returns>
    [Theory]
    [InlineData(false, false, true, false)]
    [InlineData(true, false, true, false)]
    [InlineData(false, true, true, true)]
    [InlineData(true, true, true, true)]
    [InlineData(false, true, false, false)]
    [InlineData(true, true, false, false)]
    public async Task IndependentV3FrameKeepsCompletePendingOrCommittedDecision(
        bool committed, bool reordered, bool libraryPresent, bool sourceBoundary)
    {
        if (!libraryPresent) Directory.Delete(Library, recursive: true);
        var directories = Directory.EnumerateDirectories(Manager().GameSessionPath, "*", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal).ToArray();
        var (header, payload) = Header(committed, reordered, libraryPresent, sourceBoundary);
        PutActive(Frame(header.ToJsonString(), payload));
        var files = Manager();
        await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
            Assert.Equal(committed ? AfterId : BeforeId, files.ReadExistingSessionGeneration(lease));
        AssertDecision(committed, libraryPresent);
        Assert.Equal(directories, Directory.EnumerateDirectories(files.GameSessionPath, "*", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal).ToArray());
        Assert.False(File.Exists(Active));
        Assert.Empty(Directory.EnumerateFileSystemEntries(Path.GetDirectoryName(Active)!));
        Assert.Empty(Directory.EnumerateFiles(_root, ".boe-local-*", SearchOption.AllDirectories));
    }

    /// <summary>
    /// Rejects decoded duplicate fields and late region damage before restoring any earlier supported member.
    /// </summary>
    /// <param name="mutation">
    /// The independently applied field, shape or region corruption.
    /// </param>
    /// <returns>
    /// A task that completes after v3 admission refusal and exact earlier-state/evidence preservation.
    /// </returns>
    [Theory]
    [InlineData("duplicate-root")]
    [InlineData("duplicate-generation")]
    [InlineData("duplicate-member")]
    [InlineData("duplicate-image")]
    [InlineData("duplicate-boundary")]
    [InlineData("unknown-boundary")]
    [InlineData("unknown-kind")]
    [InlineData("directory-has-region")]
    [InlineData("missing-has-region")]
    [InlineData("negative-length")]
    [InlineData("region-overlap")]
    [InlineData("region-gap")]
    [InlineData("region-overflow")]
    [InlineData("late-hash")]
    [InlineData("truncated-region")]
    [InlineData("trailing-region")]
    [InlineData("huge-declared-metadata")]
    [InlineData("extra-root")]
    public async Task LateInvalidV3MetadataRetainsEveryEarlierMemberAndEvidence(string mutation)
    {
        var files = Manager();
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        var (header, payload) = Header(false, true, true, true);
        var members = header["Members"]!.AsArray();
        var generation = members.Single(value => value!["Path"]!.GetValue<string>() == files.SessionGenerationPath)!.AsObject();
        var image = generation["After"]!.AsObject();
        var offset = image["Offset"]!.GetValue<long>();
        var boundary = header["Boundaries"]![1]!.AsObject();
        switch (mutation)
        {
            case "unknown-boundary": boundary["Unexpected"] = true; break;
            case "unknown-kind": image["Kind"] = "file"; break;
            case "directory-has-region": members[0]!["After"]!["Offset"] = 0; break;
            case "missing-has-region": members.Single(value => value!["Path"]!.GetValue<string>() == Deleted)!["After"]!["Offset"] = 0; break;
            case "negative-length": image["Length"] = -1; break;
            case "region-overlap": image["Offset"] = offset - 1; break;
            case "region-gap": image["Offset"] = offset + 1; break;
            case "region-overflow": image["Length"] = long.MaxValue; break;
            case "late-hash": image["Sha256"] = new string('0', 64); break;
        }
        var json = header.ToJsonString();
        json = mutation switch
        {
            "duplicate-root" => json.Insert(json.Length - 1, ",\"For\\u006dat\":3"),
            "duplicate-generation" => json.Replace("\"Id\":\"" + AfterId + "\"", "\"Id\":\"" + AfterId + "\",\"I\\u0064\":\"" + AfterId + "\"", StringComparison.Ordinal),
            "duplicate-member" => json.Replace(JsonSerializer.Serialize(files.SessionGenerationPath), JsonSerializer.Serialize(files.SessionGenerationPath) + ",\"Pa\\u0074h\":" + JsonSerializer.Serialize(files.SessionGenerationPath), StringComparison.Ordinal),
            "duplicate-image" => json.Replace("\"Offset\":" + offset, "\"Offset\":" + offset + ",\"Off\\u0073et\":" + offset, StringComparison.Ordinal),
            "duplicate-boundary" => json.Replace(JsonSerializer.Serialize(Source), JsonSerializer.Serialize(Source) + ",\"Pa\\u0074h\":" + JsonSerializer.Serialize(Source), StringComparison.Ordinal),
            "extra-root" => json + " {}",
            _ => json
        };
        var evidence = Frame(json, payload);
        if (mutation == "truncated-region") evidence = evidence[..^1];
        if (mutation == "trailing-region") evidence = [.. evidence, 42];
        if (mutation == "huge-declared-metadata") BinaryPrimitives.WriteInt64LittleEndian(evidence.AsSpan(8, 8), long.MaxValue);
        PutActive(evidence);
        var publication = new TrustedLocalFilePublication(files, new TrustedLocalFileScope([_root]));
        var failure = Assert.Throws<InvalidDataException>(() => publication.Recover(lease));
        Assert.Contains("v3", failure.Message, StringComparison.OrdinalIgnoreCase);
        AssertDecision(true, true);
        Assert.Equal(evidence, File.ReadAllBytes(Active));
    }

    /// <summary>
    /// Requires each v3 wire field and its exact type independently, including required nullable fields.
    /// </summary>
    /// <returns>
    /// A task that completes after each isolated field mutation retains earlier after-state and exact authority bytes.
    /// </returns>
    [Fact]
    public async Task EveryV3FieldRejectsOmissionWrongTypeAndForbiddenNull()
    {
        var shapes = new (string Shape, string[] Fields)[]
        {
            ("root", ["Format", "TransactionId", "Committed", "GenerationBefore", "GenerationAfter", "NamespaceRoot", "Members", "Boundaries"]),
            ("generation", ["Exists", "Id"]),
            ("member", ["Path", "Before", "After"]),
            ("file-image", ["Kind", "Length", "Sha256", "Offset"]),
            ("directory-image", ["Kind", "Length", "Sha256", "Offset"]),
            ("missing-image", ["Kind", "Length", "Sha256", "Offset"]),
            ("directory-boundary", ["Path", "Kind", "Length", "Sha256"]),
            ("file-boundary", ["Path", "Kind", "Length", "Sha256"])
        };
        var files = Manager();
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        var publication = new TrustedLocalFilePublication(files, new TrustedLocalFileScope([_root]));
        foreach (var (shape, fields) in shapes)
        foreach (var field in fields)
        foreach (var mutation in new[] { "missing", "wrong-type", "null" })
        {
            if (mutation == "null" &&
                ((shape is "directory-image" or "missing-image") && (field is "Sha256" or "Offset") ||
                 shape == "directory-boundary" && field == "Sha256")) continue;
            var (header, payload) = Header(false, true, true, true);
            var members = header["Members"]!.AsArray();
            var generation = members.Single(value => value!["Path"]!.GetValue<string>() == files.SessionGenerationPath)!.AsObject();
            var target = shape switch
            {
                "root" => header,
                "generation" => header["GenerationAfter"]!.AsObject(),
                "member" => generation,
                "file-image" => generation["After"]!.AsObject(),
                "directory-image" => members[0]!["After"]!.AsObject(),
                "missing-image" => members.Single(value => value!["Path"]!.GetValue<string>() == Deleted)!["After"]!.AsObject(),
                "directory-boundary" => header["Boundaries"]![0]!.AsObject(),
                "file-boundary" => header["Boundaries"]![1]!.AsObject(),
                _ => throw new InvalidOperationException("Unknown independent v3 shape.")
            };
            if (mutation == "missing") target.Remove(field);
            else if (mutation == "null") target[field] = null;
            else target[field] = field switch
            {
                "Format" or "Length" or "Offset" or "Exists" or "Committed" => JsonValue.Create("wrong-type"),
                "TransactionId" or "NamespaceRoot" or "Path" or "Id" or "Sha256" or "Kind" => JsonValue.Create(0),
                "Members" or "Boundaries" => new JsonObject(),
                _ => new JsonArray()
            };
            var evidence = Frame(header.ToJsonString(), payload);
            PutActive(evidence);
            var failure = Record.Exception(() => publication.Recover(lease));
            Assert.True(failure is InvalidDataException && failure.Message.Contains("v3", StringComparison.OrdinalIgnoreCase),
                $"{shape}.{field}/{mutation}: {failure}");
            AssertDecision(true, true);
            Assert.Equal(evidence, File.ReadAllBytes(Active));
        }
    }

    /// <summary>
    /// Constructs the existing production manager for only this fixture root.
    /// </summary>
    /// <returns>
    /// A manager with ordinary current production lease and recovery entrypoints.
    /// </returns>
    private FileSystemManager Manager() => new(_root, NullLogger<FileSystemManager>.Instance);

    /// <summary>
    /// Builds complete independent v3 metadata and literal payload images without production namespace types.
    /// </summary>
    /// <param name="committed">
    /// The declared durable decision.
    /// </param>
    /// <param name="reordered">
    /// Emits root and member fields in a different supported order.
    /// </param>
    /// <param name="libraryPresent">
    /// Declares the canonical library directory or missing state.
    /// </param>
    /// <param name="sourceBoundary">
    /// Adds the exact immutable file boundary inside the opaque library.
    /// </param>
    /// <param name="beforeGenerationAbsent">
    /// Uses an exact missing generation before-image when true; the default retains the independent BOM-bearing old bytes.
    /// </param>
    /// <returns>
    /// The independent JSON header and its contiguous member-before-after payload.
    /// </returns>
    private (JsonObject Header, byte[] Payload) Header(bool committed, bool reordered, bool libraryPresent, bool sourceBoundary,
        bool beforeGenerationAbsent = false)
    {
        var files = Manager();
        using var payload = new MemoryStream();

        var members = new JsonArray(LiteralMember(payload, reordered, files.GameSessionPath, "Directory", null, "Directory", null));
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var directories = new Queue<string>();
        directories.Enqueue(files.GameSessionPath);
        while (directories.TryDequeue(out var directory))
        foreach (var path in Directory.EnumerateFileSystemEntries(directory).Order(StringComparer.Ordinal))
        {
            if (path.Equals(Library, comparison)) continue; // Exact admitted library is opaque.
            if (Directory.Exists(path))
            {
                members.Add(LiteralMember(payload, reordered, path, "Directory", null, "Directory", null));
                directories.Enqueue(path);
            }
            else if (path == Target) members.Add(LiteralMember(payload, reordered, path, "File", Before, "File", After));
            else if (path == Created) members.Add(LiteralMember(payload, reordered, path, "Missing", null, "File", []));
            else if (path == SessionSentinel) members.Add(LiteralMember(payload, reordered, path, "File", Sentinel, "File", Sentinel));
            else throw new InvalidOperationException("Unexpected file in the independent fixture: " + path);
        }
        members.Add(LiteralMember(payload, reordered, Deleted, "File", DeletedBefore, "Missing", null));
        members.Add(LiteralMember(payload, reordered, files.SessionGenerationPath,
            beforeGenerationAbsent ? "Missing" : "File", beforeGenerationAbsent ? null : BeforeGeneration, "File", AfterGeneration));
        var boundaries = new JsonArray(new JsonObject
        {
            ["Path"] = Library, ["Kind"] = libraryPresent ? "Directory" : "Missing", ["Length"] = 0, ["Sha256"] = null
        });
        if (sourceBoundary) boundaries.Add(new JsonObject
        { ["Path"] = Source, ["Kind"] = "File", ["Length"] = Sentinel.LongLength, ["Sha256"] = Hash(Sentinel) });
        var root = new JsonObject();
        if (reordered) root["Members"] = members;
        root["Format"] = 3;
        root["TransactionId"] = Transaction;
        root["Committed"] = committed;
        root["GenerationBefore"] = new JsonObject { ["Id"] = beforeGenerationAbsent ? null : BeforeId, ["Exists"] = !beforeGenerationAbsent };
        root["GenerationAfter"] = new JsonObject { ["Id"] = AfterId, ["Exists"] = true };
        root["NamespaceRoot"] = files.GameSessionPath;
        if (!reordered) root["Members"] = members;
        root["Boundaries"] = boundaries;
        return (root, payload.ToArray());
    }

    /// <summary>
    /// Encodes known literal member images in payload order independently of JSON property order.
    /// </summary>
    /// <param name="payload">
    /// The independently owned accumulated fixture payload.
    /// </param>
    /// <param name="reordered">
    /// Emits the after property first when true.
    /// </param>
    /// <param name="path">
    /// The exact normalized fixture member path.
    /// </param>
    /// <param name="beforeKind">
    /// The literal before-state wire kind.
    /// </param>
    /// <param name="beforeBytes">
    /// Exact before file bytes, or null for a non-file state.
    /// </param>
    /// <param name="afterKind">
    /// The literal after-state wire kind.
    /// </param>
    /// <param name="afterBytes">
    /// Exact after file bytes, or null for a non-file state.
    /// </param>
    /// <returns>
    /// A complete independent member descriptor with both image properties.
    /// </returns>
    private static JsonObject LiteralMember(MemoryStream payload, bool reordered, string path,
        string beforeKind, byte[]? beforeBytes, string afterKind, byte[]? afterBytes)
    {
        var before = LiteralImage(payload, beforeKind, beforeBytes);
        var after = LiteralImage(payload, afterKind, afterBytes);
        return reordered
            ? new JsonObject { ["After"] = after, ["Path"] = path, ["Before"] = before }
            : new JsonObject { ["Path"] = path, ["Before"] = before, ["After"] = after };
    }

    /// <summary>
    /// Encodes a literal wire image and appends only its known file bytes to the independent payload.
    /// </summary>
    /// <param name="payload">
    /// The independently owned accumulated fixture payload.
    /// </param>
    /// <param name="kind">
    /// The literal supported kind written from the specification.
    /// </param>
    /// <param name="bytes">
    /// The known file bytes, including an empty file, or null for a non-file kind.
    /// </param>
    /// <returns>
    /// The complete required kind, length, hash and nullable offset descriptor.
    /// </returns>
    private static JsonObject LiteralImage(MemoryStream payload, string kind, byte[]? bytes)
    {
        var present = kind == "File";
        var offset = payload.Position;
        if (present) payload.Write(bytes ?? throw new InvalidOperationException("A literal fixture file needs bytes."));
        return new JsonObject
        {
            ["Kind"] = kind, ["Length"] = present ? bytes!.LongLength : 0,
            ["Sha256"] = present ? Hash(bytes!) : null,
            ["Offset"] = present ? JsonValue.Create(offset) : null
        };
    }

    /// <summary>
    /// Encodes exact BOELP3 magic, a signed little-endian metadata length, JSON bytes and independent payload.
    /// </summary>
    /// <param name="json">
    /// The complete valid or deliberately corrupted metadata text.
    /// </param>
    /// <param name="payload">
    /// The independently built contiguous image bytes.
    /// </param>
    /// <returns>
    /// The complete physical v3 fixture bytes.
    /// </returns>
    private static byte[] Frame(string json, byte[] payload)
    {
        var metadata = Encoding.UTF8.GetBytes(json);
        var bytes = new byte[16 + metadata.Length + payload.Length];
        Encoding.ASCII.GetBytes("BOELP3\r\n").CopyTo(bytes, 0);
        BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(8, 8), metadata.LongLength);
        metadata.CopyTo(bytes, 16);
        payload.CopyTo(bytes, 16 + metadata.Length);
        return bytes;
    }

    /// <summary>
    /// Installs independent authority bytes into the fixture's one existing publication root.
    /// </summary>
    /// <param name="bytes">
    /// The exact v3 frame bytes to inspect through ordinary recovery.
    /// </param>
    private void PutActive(byte[] bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Active)!);
        File.WriteAllBytes(Active, bytes);
    }

    /// <summary>
    /// Checks the independently specified decision plus unmodified library, source and inside/outside sentinels.
    /// </summary>
    /// <param name="committed">
    /// Selects exact after state or exact before state.
    /// </param>
    /// <param name="libraryPresent">
    /// Requires the library directory and exact untouched sentinels when true, or its preserved absence when false.
    /// </param>
    private void AssertDecision(bool committed, bool libraryPresent)
    {
        Assert.Equal(committed ? After : Before, File.ReadAllBytes(Target));
        Assert.Equal(committed, File.Exists(Created));
        if (committed) Assert.Empty(File.ReadAllBytes(Created));
        Assert.Equal(!committed, File.Exists(Deleted));
        if (!committed) Assert.Equal(DeletedBefore, File.ReadAllBytes(Deleted));
        Assert.Equal(committed ? AfterGeneration : BeforeGeneration, File.ReadAllBytes(Manager().SessionGenerationPath));
        Assert.Equal(Sentinel, File.ReadAllBytes(SessionSentinel));
        Assert.Equal(Sentinel, File.ReadAllBytes(OutsideSentinel));
        Assert.Equal(libraryPresent, Directory.Exists(Library));
        if (libraryPresent)
        {
            Assert.Equal(Sentinel, File.ReadAllBytes(Source));
            Assert.Equal(Sentinel, File.ReadAllBytes(Path.Combine(Library, "library-sentinel.bin")));
        }
    }

    /// <summary>
    /// Recovers an independent new-generation frame to exact prior absence or committed presence.
    /// </summary>
    /// <param name="committed">
    /// Retains the new generation when true; otherwise removes it after all original namespace files are restored.
    /// </param>
    /// <returns>
    /// Completion after normal fresh acquisition and exact generation bytes or absence.
    /// </returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IndependentNewGenerationFramePreservesExactAbsence(bool committed)
    {
        var (header, payload) = Header(committed, true, true, true, beforeGenerationAbsent: true);
        PutActive(Frame(header.ToJsonString(), payload));
        var files = Manager();

        await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
            Assert.Equal(committed ? AfterId : null, files.ReadExistingSessionGeneration(lease));

        Assert.Equal(committed ? After : Before, File.ReadAllBytes(Target));
        Assert.Equal(committed, File.Exists(Created));
        Assert.Equal(!committed, File.Exists(Deleted));
        Assert.Equal(committed, File.Exists(files.SessionGenerationPath));
        if (committed) Assert.Equal(AfterGeneration, File.ReadAllBytes(files.SessionGenerationPath));
        Assert.Equal(Sentinel, File.ReadAllBytes(Source));
        Assert.Equal(Sentinel, File.ReadAllBytes(OutsideSentinel));
        Assert.False(File.Exists(Active));
        Assert.Empty(Directory.EnumerateFileSystemEntries(Path.GetDirectoryName(Active)!));
    }

    /// <summary>
    /// Refuses an unrecorded late file or empty directory before restoring any earlier member.
    /// </summary>
    /// <param name="directory">
    /// Creates an empty directory when true, or unknown binary file when false.
    /// </param>
    /// <returns>
    /// Completion after exact refusal, retaining earlier after-images, generation and authority bytes.
    /// </returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnknownLaterChildRetainsEveryEarlierAfterImage(bool directory)
    {
        var files = Manager();
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        var (header, payload) = Header(false, true, true, true);
        var evidence = Frame(header.ToJsonString(), payload);
        var path = Path.Combine(files.GameSessionPath, "stories", "unknown-late");
        if (directory) Directory.CreateDirectory(path);
        else File.WriteAllBytes(path, [0xFF, 0, 65]);
        PutActive(evidence);
        var publication = new TrustedLocalFilePublication(files, new TrustedLocalFileScope([_root]));

        Assert.Throws<InvalidDataException>(() => publication.Recover(lease));

        AssertDecision(true, true);
        Assert.Equal(evidence, File.ReadAllBytes(Active));
        if (directory) Assert.Empty(Directory.EnumerateFileSystemEntries(path));
        else Assert.Equal(new byte[] { 0xFF, 0, 65 }, File.ReadAllBytes(path));
    }

    /// <summary>
    /// Rejects a structurally decoded namespace that would broaden roots, protected boundaries or required directories.
    /// </summary>
    /// <param name="mutation">
    /// The independent authority conflict introduced after constructing otherwise supported evidence.
    /// </param>
    /// <returns>
    /// Completion after refusal without earlier restoration or outside mutation.
    /// </returns>
    [Theory]
    [InlineData("outside-member")]
    [InlineData("root-missing")]
    [InlineData("required-directory-missing")]
    [InlineData("extra-directory-boundary")]
    [InlineData("library-file")]
    [InlineData("unknown-directory-blocker")]
    public async Task UnsupportedNamespaceAuthorityRetainsExactEvidence(string mutation)
    {
        var files = Manager();
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        var (header, payload) = Header(false, true, true, true);
        var members = header["Members"]!.AsArray();
        var root = members.Single(value => value!["Path"]!.GetValue<string>() == files.GameSessionPath)!.AsObject();
        var storiesPath = Path.Combine(files.GameSessionPath, "stories");
        var stories = members.Single(value => value!["Path"]!.GetValue<string>() == storiesPath)!.AsObject();
        switch (mutation)
        {
            case "outside-member":
                members.Single(value => value!["Path"]!.GetValue<string>() == SessionSentinel)!["Path"] = OutsideSentinel;
                break;
            case "root-missing": root["Before"]!["Kind"] = "Missing"; break;
            case "required-directory-missing":
                var output = members.Single(value => value!["Path"]!.GetValue<string>() == Path.Combine(files.GameSessionPath, "output"))!;
                output["After"]!["Kind"] = "Missing";
                break;
            case "extra-directory-boundary":
                header["Boundaries"]!.AsArray().Add(new JsonObject
                    { ["Path"] = storiesPath, ["Kind"] = "Directory", ["Length"] = 0, ["Sha256"] = null });
                break;
            case "library-file":
                var library = header["Boundaries"]![0]!;
                library["Kind"] = "File"; library["Length"] = Sentinel.LongLength; library["Sha256"] = Hash(Sentinel);
                break;
            case "unknown-directory-blocker":
                Assert.Equal("Directory", stories["Before"]!["Kind"]!.GetValue<string>());
                Directory.Delete(storiesPath, recursive: false);
                File.WriteAllBytes(storiesPath, [77]);
                break;
        }
        var evidence = Frame(header.ToJsonString(), payload);
        PutActive(evidence);
        var publication = new TrustedLocalFilePublication(files, new TrustedLocalFileScope([_root]));

        Assert.Throws<InvalidDataException>(() => publication.Recover(lease));

        AssertDecision(true, true);
        Assert.Equal(evidence, File.ReadAllBytes(Active));
        Assert.Equal(Sentinel, File.ReadAllBytes(OutsideSentinel));
        if (mutation == "unknown-directory-blocker") Assert.Equal(new byte[] { 77 }, File.ReadAllBytes(storiesPath));
    }

    /// <summary>
    /// Computes an independent expected SHA-256 from exact literal fixture bytes.
    /// </summary>
    /// <param name="bytes">
    /// The known image or boundary bytes.
    /// </param>
    /// <returns>
    /// The uppercase SHA-256 hexadecimal value required by exact-byte admission.
    /// </returns>
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    /// <summary>
    /// Removes only this independently owned sandbox, including its outside sentinel.
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(_sandbox)) Directory.Delete(_sandbox, recursive: true);
    }
}
