using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Verifies real generation-member crash ordering, exact generation bytes and empty-versus-absent decisions.
/// </summary>
/// <param name="output">
/// Receives exact child cut reports and sampled resource measurements.
/// </param>
public sealed class TrustedLocalStreamGenerationRecoveryTests(ITestOutputHelper output) : IDisposable
{
    private const long HeapBytes = 768L * 1024 * 1024;
    private const string BeforeId = "0123456789abcdef0123456789abcdef";
    private const string AfterId = "fedcba9876543210fedcba9876543210";
    private readonly string _root = Path.Combine(Path.GetTempPath(), "boe-stream-generation-" + Guid.NewGuid().ToString("N"));
    private byte[]? _generationBefore;
    private byte[] _generationAfter = [];

    /// <summary>
    /// Resolves this test's independently owned session and runtime paths.
    /// </summary>
    private FileSystemManager Files => new(_root, NullLogger<FileSystemManager>.Instance);

    /// <summary>
    /// Proves fresh adapter generation ordering and existing generation transitions through ordinary cold acquisition.
    /// </summary>
    /// <param name="fresh">
    /// Uses the real adapter's appended fresh generation when <see langword="true"/>, or an explicit existing-generation transition otherwise.
    /// </param>
    /// <param name="cut">
    /// Selects a necessary generation ordering boundary or durable commit.
    /// </param>
    /// <param name="committed">
    /// Whether the durable journal decision must preserve all after-images.
    /// </param>
    [Theory]
    [InlineData(true, "before-generation", false)]
    [InlineData(true, "generation-staged", false)]
    [InlineData(true, "generation-published", false)]
    [InlineData(true, "Committed", true)]
    [InlineData(false, "generation-published", false)]
    [InlineData(false, "Committed", true)]
    public async Task GenerationMemberCrashOrderingPreservesExactBytesAndEmptyImages(bool fresh, string cut, bool committed)
    {
        Seed(fresh);
        var published = await Run(fresh ? "generation-fresh-publish" : "generation-transition-publish", cut, 73);
        Assert.Equal("AbruptExit", published.GetProperty("Phase").GetString());
        var expectedPhase = cut switch { "generation-staged" => "MemberStaged", "Committed" => "Committed", _ => "MemberPublished" };
        var expectedIndex = cut switch { "before-generation" => 2, "Committed" => -1, _ => 3 };
        Assert.Equal(expectedPhase, published.GetProperty("Cut").GetString());
        Assert.Equal(expectedIndex, published.GetProperty("Index").GetInt32());
        Assert.Equal(4, published.GetProperty("MemberCount").GetInt32());
        Assert.Equal(!fresh, published.GetProperty("GenerationBeforeExists").GetBoolean());
        Assert.Equal(fresh ? null : BeforeId, published.GetProperty("GenerationBeforeId").GetString());
        Assert.True(published.GetProperty("CreatedEmptyExists").GetBoolean());
        Assert.Equal(0, published.GetProperty("CreatedEmptyLength").GetInt64());
        Assert.True(published.GetProperty("DeletedEmptyBeforeExists").GetBoolean());
        Assert.Equal(0, published.GetProperty("DeletedEmptyBeforeLength").GetInt64());
        Assert.False(published.GetProperty("DeletedEmptyAfterExists").GetBoolean());
        var generationAfter = Convert.FromBase64String(published.GetProperty("GenerationAfterBytes").GetString()!);
        var afterId = published.GetProperty("GenerationAfterId").GetString()!;
        Assert.True(Guid.TryParseExact(afterId, "N", out var parsed));
        Assert.Equal(parsed.ToString("N"), afterId);
        if (fresh)
        {
            using var document = JsonDocument.Parse(generationAfter);
            Assert.Equal(1, document.RootElement.GetProperty("SchemaVersion").GetInt32());
            Assert.Equal(afterId, document.RootElement.GetProperty("GenerationId").GetString());
        }
        else
        {
            Assert.Equal(AfterId, afterId);
            Assert.Equal(_generationAfter, generationAfter);
        }
        Assert.False(Directory.Exists(Path.Combine(_root, "inputs")));
        Assert.True(File.Exists(Path.Combine(Files.RuntimeRootPath, "trusted-local-publication-v1/active.json")));
        AssertContent(after: true);
        AssertImage(Files.SessionGenerationPath, cut is "before-generation" or "generation-staged" ? _generationBefore : generationAfter);
        AssertSentinels();

        var recovered = await Run("generation-recover", "unused", 0);
        Assert.Equal("CanonicalAcquisitionReturned", recovered.GetProperty("Phase").GetString());
        var expectedGeneration = committed ? generationAfter : _generationBefore;
        AssertImage(Files.SessionGenerationPath, expectedGeneration);
        Assert.Equal(committed ? afterId : fresh ? null : BeforeId, recovered.GetProperty("GenerationId").GetString());
        Assert.Equal(expectedGeneration == null ? null : Convert.ToBase64String(expectedGeneration), recovered.GetProperty("GenerationBytes").GetString());
        AssertContent(after: committed);
        AssertSentinels();
        Assert.Empty(Directory.EnumerateFileSystemEntries(Path.Combine(Files.RuntimeRootPath, "trusted-local-publication-v1")));
        Assert.Empty(Directory.GetFiles(Files.GameSessionPath, ".boe-local-*", SearchOption.AllDirectories));
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(Files.SessionGenerationPath)!, ".boe-local-*", SearchOption.AllDirectories));
    }

    /// <summary>
    /// Creates independent binary, zero-byte, generation and library fixtures without borrowing live state.
    /// </summary>
    /// <param name="fresh">
    /// Leaves generation absent when <see langword="true"/>; otherwise writes exact BOM and mixed-case current-schema bytes.
    /// </param>
    private void Seed(bool fresh)
    {
        var files = Files; files.EnsureDirectoryStructure();
        Directory.CreateDirectory(Path.Combine(_root, "inputs"));
        Directory.CreateDirectory(Path.GetDirectoryName(files.SessionGenerationPath)!);
        File.WriteAllBytes(files.ResolvePath("game_state/core/generation-content.bin"), [9, 0, 255]);
        File.WriteAllBytes(files.ResolvePath("game_state/core/generation-deleted-empty.bin"), []);
        File.WriteAllBytes(Path.Combine(_root, "inputs/after.bin"), [2, 255, 0, 77]);
        File.WriteAllBytes(Path.Combine(_root, "inputs/empty.bin"), []);
        _generationAfter = Encode(new UTF8Encoding(encoderShouldEmitUTF8Identifier: true),
            "{\"sChemaVersion\":1,\"gEnerationId\":\"" + AfterId + "\",\"extension\":{\"after\":\"новое\"}}");
        File.WriteAllBytes(Path.Combine(_root, "inputs/generation-after.json"), _generationAfter);
        if (fresh)
        {
            if (File.Exists(files.SessionGenerationPath)) File.Delete(files.SessionGenerationPath);
        }
        else
        {
            _generationBefore = Encode(new UnicodeEncoding(bigEndian: false, byteOrderMark: true),
                "{\"sChEmAvErSiOn\":1,\"gEnErAtIoNiD\":\"" + BeforeId + "\",\"extension\":{\"before\":\"старое\"}}");
            File.WriteAllBytes(files.SessionGenerationPath, _generationBefore);
        }
        foreach (var dir in new[] { "manual_saves", "autosaves", "checkpoint_saves" })
            File.WriteAllBytes(files.ResolvePath($"saves/{dir}/sentinel.zip"), [7, 0, 255]);
        var outside = Path.Combine(_root, "outside-content-alias.bin");
        var content = files.ResolvePath("game_state/core/generation-content.bin");
        if (OperatingSystem.IsWindows()) Assert.True(CreateHardLink(outside, content, IntPtr.Zero));
        else Assert.Equal(0, Link(content, outside));
    }

    /// <summary>
    /// Verifies all ordinary content decisions, including the difference between absence and present zero bytes.
    /// </summary>
    /// <param name="after">
    /// Selects the complete new decision when <see langword="true"/>, or the exact prior decision otherwise.
    /// </param>
    private void AssertContent(bool after)
    {
        AssertImage(Files.ResolvePath("game_state/core/generation-content.bin"), after ? [2, 255, 0, 77] : [9, 0, 255]);
        AssertImage(Files.ResolvePath("game_state/core/generation-created-empty.bin"), after ? [] : null);
        AssertImage(Files.ResolvePath("game_state/core/generation-deleted-empty.bin"), after ? null : []);
    }

    /// <summary>
    /// Verifies the entire untouched save library and the outside name of the replaced binary before-image.
    /// </summary>
    private void AssertSentinels()
    {
        Assert.Equal(new byte[] { 9, 0, 255 }, File.ReadAllBytes(Path.Combine(_root, "outside-content-alias.bin")));
        Assert.Equal(new[] { "autosaves/sentinel.zip", "checkpoint_saves/sentinel.zip", "manual_saves/sentinel.zip" },
            Directory.GetFiles(Files.ResolvePath("saves"), "*", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(Files.ResolvePath("saves"), path).Replace('\\', '/')).Order(StringComparer.Ordinal));
        foreach (var dir in new[] { "manual_saves", "autosaves", "checkpoint_saves" })
            Assert.Equal(new byte[] { 7, 0, 255 }, File.ReadAllBytes(Files.ResolvePath($"saves/{dir}/sentinel.zip")));
    }

    /// <summary>
    /// Checks exact file presence and bytes without treating an empty array as absence.
    /// </summary>
    /// <param name="path">
    /// The owned member to inspect.
    /// </param>
    /// <param name="expected">
    /// The complete expected bytes, or <see langword="null"/> for absence.
    /// </param>
    private static void AssertImage(string path, byte[]? expected)
    {
        Assert.Equal(expected != null, File.Exists(path));
        if (expected != null) Assert.Equal(expected, File.ReadAllBytes(path));
    }

    /// <summary>
    /// Runs one sequential owned child and checks its exact exit and heap declaration.
    /// </summary>
    /// <param name="mode">
    /// The fresh publication, transition publication or ordinary recovery mode.
    /// </param>
    /// <param name="cut">
    /// The publication boundary requested by this case.
    /// </param>
    /// <param name="expectedExit">
    /// Zero for normal acquisition, or 73 for the deliberate abrupt cut.
    /// </param>
    /// <returns>
    /// The child's exact structured report.
    /// </returns>
    private async Task<JsonElement> Run(string mode, string cut, int expectedExit)
    {
        var assembly = typeof(TrustedLocalStreamGenerationRecoveryTests).Assembly.Location;
        var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
        { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in new[] { "exec", "--runtimeconfig", Path.ChangeExtension(assembly, ".runtimeconfig.json"),
            "--depsfile", Path.ChangeExtension(assembly, ".deps.json"), typeof(PortableStorageCrashHost.Program).Assembly.Location,
            _root, HeapBytes.ToString(), mode, cut }) start.ArgumentList.Add(argument);
        start.Environment["DOTNET_GCHeapHardLimit"] = "0x30000000";
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Owned generation child did not start.");
        var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
        var timer = Stopwatch.StartNew(); long sampledPeak = 0;
        try
        {
            while (!process.HasExited)
            {
                process.Refresh(); sampledPeak = Math.Max(sampledPeak, process.WorkingSet64);
                if (sampledPeak > 1024L * 1024 * 1024 || timer.Elapsed > TimeSpan.FromSeconds(120))
                    throw new InvalidOperationException("Owned generation child exceeded its declared resource bounds.");
                await Task.Delay(25);
            }
            await process.WaitForExitAsync(); var text = await stdout;
            Assert.True(process.ExitCode == expectedExit, text + await stderr);
            using var document = JsonDocument.Parse(text);
            Assert.Equal(HeapBytes, document.RootElement.GetProperty("HeapBytes").GetInt64());
            output.WriteLine("mode={0}; cut={1}; sampledPeakBytes={2}; milliseconds={3}; {4}", mode, cut, sampledPeak, timer.Elapsed.TotalMilliseconds, text);
            return document.RootElement.Clone();
        }
        finally { if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); } }
    }

    /// <summary>
    /// Preserves the chosen encoding's BOM and every supplied generation-document byte.
    /// </summary>
    /// <param name="encoding">
    /// The BOM-emitting encoding to use.
    /// </param>
    /// <param name="json">
    /// The generation document with deliberately mixed-case fields and a supported extension.
    /// </param>
    /// <returns>
    /// The complete BOM-prefixed document.
    /// </returns>
    private static byte[] Encode(Encoding encoding, string json) => [.. encoding.GetPreamble(), .. encoding.GetBytes(json)];

    /// <summary>
    /// Creates the Windows outside hard-link sentinel.
    /// </summary>
    /// <param name="created">
    /// The new outside name.
    /// </param>
    /// <param name="existing">
    /// The original canonical member name.
    /// </param>
    /// <param name="security">
    /// The unused native security argument, passed as zero.
    /// </param>
    /// <returns>
    /// <see langword="true"/> on creation; otherwise, <see langword="false"/>.
    /// </returns>
    [DllImport("kernel32.dll", EntryPoint = "CreateHardLinkW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLink(string created, string existing, IntPtr security);

    /// <summary>
    /// Creates the Linux outside hard-link sentinel.
    /// </summary>
    /// <param name="existing">
    /// The original canonical member name.
    /// </param>
    /// <param name="created">
    /// The new outside name.
    /// </param>
    /// <returns>
    /// Zero on success, or the native failure status.
    /// </returns>
    [DllImport("libc", EntryPoint = "link", SetLastError = true)]
    private static extern int Link(string existing, string created);

    /// <summary>
    /// Removes only this test's mutable root after every owned child has stopped.
    /// </summary>
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
}
