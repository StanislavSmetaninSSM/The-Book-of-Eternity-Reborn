using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Creates isolated current-producer archives and independent byte/namespace expectations for load probes.
/// </summary>
internal static class PortableLoadFixture
{
    /// <summary>
    /// Identifies the selected source inside the opaque canonical library.
    /// </summary>
    internal const string SourceRelative = "saves/manual_saves/selected-cold-source.zip";
    /// <summary>
    /// Identifies the old file converted into a directory containing an incoming file.
    /// </summary>
    internal const string FileToDirectory = "lore/cold-file-to-directory";
    /// <summary>
    /// Identifies the old nonempty directory converted into an incoming file.
    /// </summary>
    internal const string DirectoryToFile = "lore/cold-directory-to-file";

    /// <summary>
    /// Creates a real manifested archive, then authors a disjoint old live namespace and exports independent expectations.
    /// </summary>
    /// <param name="root">
    /// An absent isolated root owned by the calling test.
    /// </param>
    /// <param name="absentGeneration">
    /// Removes the producer's generation before load when <see langword="true"/>.
    /// </param>
    /// <param name="incomingFiles">
    /// Additional small incoming file names and exact bytes; null selects only the cold fixture.
    /// Future bulk probes can stream authored files at the insertion point before the current producer closes its archive.
    /// </param>
    /// <param name="oldFiles">
    /// Additional small old file names and bytes, authored after archive close; null adds none.
    /// Future bulk probes can remove additional incoming live files and stream disjoint old files at this insertion point.
    /// </param>
    /// <returns>
    /// Independent namespace hashes, exact old generation and protected library/source expectations.
    /// </returns>
    internal static async Task<PortableLoadFixtureState> CreateAsync(string root, bool absentGeneration = false,
        IReadOnlyDictionary<string, byte[]>? incomingFiles = null, IReadOnlyDictionary<string, byte[]>? oldFiles = null)
    {
        if (Directory.Exists(root)) throw new InvalidOperationException("Cold fixture root must be absent.");
        var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
        var state = PortableSaveFixture.Seed(files);
        Put(files, "game_state/meta/soul_state.json", Encoding.UTF8.GetBytes(
            """{"soulName":"Cold new soul","currentRealm":"Mortal World","currentIncarnation":1}"""));
        Put(files, "config.json", Encode(new UTF8Encoding(true), """{"language":"en","consoleFontSize":20,"musicVolume":31}"""));
        var historyPath = files.ResolvePath(ResourceMaterializationContract.HistoryPath);
        File.AppendAllText(historyPath, "\n ", new UTF8Encoding(false));
        Put(files, FileToDirectory + "/child.bin", [0xEF, 0xBB, 0xBF, 0, 0xFF, 1]);
        Put(files, DirectoryToFile, [0xFF, 0, 2]);
        Put(files, "lore/cold-first.bin", [3, 0, 0xFF]);
        Put(files, "lore/cold-late.bin", [4, 0, 0xFE]);
        if (incomingFiles != null) foreach (var (name, bytes) in incomingFiles) Put(files, name, bytes);
        var service = new SaveLoadService(files, state, NullLogger<SaveLoadService>.Instance);
        if (!await service.SaveGameAsync("cold-source", "actual current producer cold-load fixture"))
            throw new InvalidOperationException("Current producer failed to create the cold archive.");
        var produced = Directory.GetFiles(files.ResolvePath("saves/manual_saves"), "*.zip").Single();
        var source = files.ResolvePath(SourceRelative);
        File.Copy(produced, source);
        var incoming = ReadArchiveHashes(source);
        // Canonical fixture names have no ephemeral inputs or profile projection.
        Directory.Delete(files.ResolvePath(FileToDirectory), recursive: true);
        Put(files, FileToDirectory, [0xFE, 0, 9]);
        File.Delete(files.ResolvePath(DirectoryToFile));
        Put(files, DirectoryToFile + "/old-child.bin", [0xFD, 0, 8]);
        Directory.CreateDirectory(files.ResolvePath(DirectoryToFile + "/old-empty"));
        Directory.CreateDirectory(files.ResolvePath("lore/cold-preserved-empty"));
        Put(files, "lore/cold-first.bin", [0xFA, 0, 7]);
        Put(files, "lore/cold-late.bin", [0xFB, 0, 6]);
        Put(files, "game_state/world/cold-old-delete.bin", []);
        Put(files, "game_state/meta/soul_state.json", Encode(new UTF8Encoding(true),
            """{ "soulName":"Cold old soul", "currentRealm":"Mortal World", "currentIncarnation":1 }"""));
        Put(files, "config.json", Encoding.UTF8.GetBytes("""{ "language":"ru", "consoleFontSize":26, "musicVolume":17 }"""));
        File.AppendAllText(historyPath, "\n\n", new UTF8Encoding(false));
        File.AppendAllText(files.ResolvePath(ResourceMaterializationContract.StatePath), "\n  ", new UTF8Encoding(false));
        if (oldFiles != null) foreach (var (name, bytes) in oldFiles) Put(files, name, bytes);
        foreach (var name in new[] { "manual_saves", "autosaves", "checkpoint_saves" })
            Put(files, $"saves/{name}/cold-sentinel.zip", [0xFF, 0, 31]);
        Directory.CreateDirectory(files.ResolvePath("saves/manual_saves/opaque-empty"));
        byte[]? generation = absentGeneration ? null : Encode(new UnicodeEncoding(false, true),
            """{ "sChemaVersion":1, "gEnerationId":"0123456789abcdef0123456789abcdef", "extension":{"cold":"old"} }""");
        if (generation == null) File.Delete(files.SessionGenerationPath);
        else File.WriteAllBytes(files.SessionGenerationPath, generation);
        var before = Snapshot(files.GameSessionPath);
        var after = new Dictionary<string, string>(StringComparer.Ordinal) { [""] = "Directory" };
        // Required baseline and old empty directories remain unless an incoming file blocks their name.
        foreach (var (name, value) in before.Where(pair => pair.Value == "Directory"))
            if (!incoming.Keys.Any(file => name == file || name.StartsWith(file + "/", StringComparison.Ordinal))) after[name] = value;
        foreach (var (name, hash) in incoming) { after[name] = hash; AddParents(after, name); }
        foreach (var (name, hash) in before.Where(pair => pair.Key == "saves" || pair.Key.StartsWith("saves/", StringComparison.Ordinal)))
            after[name] = hash;
        var result = new PortableLoadFixtureState
        {
            Before = before, After = after, GenerationBefore = generation == null ? null : Convert.ToBase64String(generation),
            Protected = before.Where(pair => pair.Key == "saves" || pair.Key.StartsWith("saves/", StringComparison.Ordinal))
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal)
        };
        File.WriteAllText(Path.Combine(root, "cold-fixture.json"), JsonSerializer.Serialize(result));
        return result;
    }

    /// <summary>
    /// Reads original expectations without rebuilding or extracting the archive.
    /// </summary>
    /// <param name="root">
    /// The exact owned fixture root.
    /// </param>
    /// <returns>
    /// Independent expectations exported before the load child started.
    /// </returns>
    internal static PortableLoadFixtureState Read(string root) => JsonSerializer.Deserialize<PortableLoadFixtureState>(
        File.ReadAllText(Path.Combine(root, "cold-fixture.json"))) ?? throw new InvalidDataException("Missing cold expectations.");

    /// <summary>
    /// Captures ordinary directory names and streaming file hashes without following links.
    /// </summary>
    /// <param name="root">
    /// The ordinary namespace root.
    /// </param>
    /// <returns>
    /// Ordinal relative slash-separated names mapped to Directory or SHA256, including the root.
    /// </returns>
    internal static Dictionary<string, string> Snapshot(string root)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal) { [""] = "Directory" };
        var pending = new Stack<string>(); pending.Push(root);
        while (pending.TryPop(out var directory))
            foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
            {
                var attributes = File.GetAttributes(entry);
                if (attributes.HasFlag(FileAttributes.ReparsePoint)) throw new InvalidDataException("Fixture snapshot encountered a link.");
                var name = Path.GetRelativePath(root, entry).Replace('\\', '/');
                if (attributes.HasFlag(FileAttributes.Directory)) { result.Add(name, "Directory"); pending.Push(entry); }
                else result.Add(name, Hash(entry));
            }
        return result;
    }

    /// <summary>
    /// Computes a streaming hash of one complete closed file.
    /// </summary>
    /// <param name="path">
    /// The ordinary owned file.
    /// </param>
    /// <returns>
    /// Its uppercase SHA256 digest.
    /// </returns>
    internal static string Hash(string path) { using var input = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(input)); }

    /// <summary>
    /// Opens the small actual v3 header without production parser or target-plan APIs.
    /// </summary>
    /// <param name="path">
    /// The actual active, intent or commit frame announced by the child.
    /// </param>
    /// <returns>
    /// Caller-owned JSON metadata with exact wire identities and region descriptors.
    /// </returns>
    internal static JsonDocument ReadFrame(string path)
    {
        using var input = File.OpenRead(path);
        Span<byte> prefix = stackalloc byte[16]; input.ReadExactly(prefix);
        if (!prefix[..8].SequenceEqual("BOELP3\r\n"u8)) throw new InvalidDataException("Cold load did not produce v3.");
        var length = BinaryPrimitives.ReadInt64LittleEndian(prefix[8..]);
        if (length <= 0 || length > input.Length - 16) throw new InvalidDataException("Invalid fixture metadata region.");
        var bytes = new byte[checked((int)length)]; input.ReadExactly(bytes);
        return JsonDocument.Parse(bytes);
    }

    /// <summary>
    /// Reads exact After bytes from the sole runtime generation member.
    /// </summary>
    /// <param name="path">
    /// A complete v3 frame owned by this fixture.
    /// </param>
    /// <param name="generationPath">
    /// The manager's exact runtime-generation path.
    /// </param>
    /// <returns>
    /// Complete bytes independent of serializer or binding reconstruction.
    /// </returns>
    internal static byte[] GenerationAfter(string path, string generationPath)
        => GenerationRegion(path, generationPath, after: true) ?? throw new InvalidDataException("After generation is absent.");

    /// <summary>
    /// Reads exact Before bytes or explicit absence from the actual frame's runtime generation member.
    /// </summary>
    /// <param name="path">
    /// A complete v3 frame owned by this fixture.
    /// </param>
    /// <param name="generationPath">
    /// The exact manager-owned runtime generation path.
    /// </param>
    /// <returns>
    /// Complete old bytes, or <see langword="null"/> for explicit Missing.
    /// </returns>
    internal static byte[]? GenerationBefore(string path, string generationPath) => GenerationRegion(path, generationPath, after: false);

    /// <summary>
    /// Reads one exact wire generation region without recreating its schema or encoding.
    /// </summary>
    /// <param name="path">
    /// The complete frame containing independently addressed payload bytes.
    /// </param>
    /// <param name="generationPath">
    /// The exact runtime generation member.
    /// </param>
    /// <param name="after">
    /// Selects After when <see langword="true"/> or Before otherwise.
    /// </param>
    /// <returns>
    /// Complete File bytes or <see langword="null"/> for a Missing region.
    /// </returns>
    private static byte[]? GenerationRegion(string path, string generationPath, bool after)
    {
        using var header = ReadFrame(path);
        var region = header.RootElement.GetProperty("Members").EnumerateArray()
            .Single(member => member.GetProperty("Path").GetString() == generationPath).GetProperty(after ? "After" : "Before");
        if (region.GetProperty("Kind").GetString() == "Missing") return null;
        if (region.GetProperty("Kind").GetString() != "File") throw new InvalidDataException("Generation region is not File or Missing.");
        using var input = File.OpenRead(path);
        input.Position = 8; Span<byte> length = stackalloc byte[8]; input.ReadExactly(length);
        input.Position = checked(16 + BinaryPrimitives.ReadInt64LittleEndian(length) + region.GetProperty("Offset").GetInt64());
        var bytes = new byte[checked((int)region.GetProperty("Length").GetInt64())]; input.ReadExactly(bytes);
        return bytes;
    }

    /// <summary>
    /// Hashes original ZIP payload entries while excluding its manifest document.
    /// </summary>
    /// <param name="source">
    /// The unmodified current-producer archive.
    /// </param>
    /// <returns>
    /// Payload names and streaming hashes for the independent After oracle.
    /// </returns>
    private static Dictionary<string, string> ReadArchiveHashes(string source)
    {
        using var archive = ZipFile.OpenRead(source);
        if (archive.GetEntry("save_manifest.json") == null) throw new InvalidDataException("Cold source must retain its manifest.");
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in archive.Entries)
        {
            if (entry.Name.Length == 0 || entry.FullName == "save_manifest.json") continue;
            using var input = entry.Open(); result.Add(entry.FullName, Convert.ToHexString(SHA256.HashData(input)));
        }
        return result;
    }

    /// <summary>
    /// Adds parent names implied independently by an authored payload.
    /// </summary>
    /// <param name="nodes">
    /// The expected namespace receiving Directory markers.
    /// </param>
    /// <param name="name">
    /// A relative slash-separated payload name.
    /// </param>
    private static void AddParents(Dictionary<string, string> nodes, string name)
    {
        var end = name.LastIndexOf('/');
        while (end >= 0) { nodes[name[..end]] = "Directory"; end = name.LastIndexOf('/', end - 1); }
    }

    /// <summary>
    /// Writes an owned session file for setup without constructing a replacement plan.
    /// </summary>
    /// <param name="files">
    /// Resolves the isolated session.
    /// </param>
    /// <param name="name">
    /// A normalized relative file name remaining inside GameSession.
    /// </param>
    /// <param name="bytes">
    /// Exact authored bytes, including a present empty file when empty.
    /// </param>
    private static void Put(FileSystemManager files, string name, byte[] bytes)
    {
        var path = files.ResolvePath(name);
        if (!Path.GetFullPath(path).StartsWith(files.GameSessionPath + Path.DirectorySeparatorChar,
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new InvalidOperationException("Fixture file escaped GameSession.");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllBytes(path, bytes);
    }

    /// <summary>
    /// Encodes a supported document without dropping its deliberate BOM.
    /// </summary>
    /// <param name="encoding">
    /// The encoding and preamble to preserve.
    /// </param>
    /// <param name="text">
    /// Valid authored JSON with deliberate whitespace.
    /// </param>
    /// <returns>
    /// Exact preamble followed by encoded text.
    /// </returns>
    private static byte[] Encode(Encoding encoding, string text) => [.. encoding.GetPreamble(), .. encoding.GetBytes(text)];
}

/// <summary>
/// Exports scenario expectations without sharing mutable cached fixtures.
/// </summary>
internal sealed class PortableLoadFixtureState
{
    /// <summary>
    /// Contains every old session file and directory, including opaque library entries.
    /// </summary>
    public Dictionary<string, string> Before { get; init; } = [];
    /// <summary>
    /// Contains archive-derived hashes, preserved directories and exact opaque library.
    /// </summary>
    public Dictionary<string, string> After { get; init; } = [];
    /// <summary>
    /// Contains exact old generation as base64, or <see langword="null"/> for absence.
    /// </summary>
    public string? GenerationBefore { get; init; }
    /// <summary>
    /// Contains the complete protected library and selected source.
    /// </summary>
    public Dictionary<string, string> Protected { get; init; } = [];
}
