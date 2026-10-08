using System.Diagnostics;
using System.Text.Json;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Verifies actual typed-load crash decisions through fresh journal-only canonical acquisition processes.
/// </summary>
/// <param name="output">
/// Receives exact child announcements, OS identity and bounded resource observations.
/// </param>
public sealed class PortableLoadColdRecoveryTests(ITestOutputHelper output) : IDisposable
{
    private const long HeapBytes = 768L * 1024 * 1024;
    private readonly string _root = Path.Combine(Path.GetTempPath(), "boe-load-cold-" + Guid.NewGuid().ToString("N"));

    /// <summary>
    /// Resolves the isolated root without preparing or extracting an archive.
    /// </summary>
    private FileSystemManager Files => new(_root, NullLogger<FileSystemManager>.Instance);

    /// <summary>
    /// Proves that each durable decision cut recovers exact old or new bytes with private extraction absent.
    /// </summary>
    /// <param name="cut">
    /// The exact actual publication phase and wire-member selector announced before termination.
    /// </param>
    /// <param name="committed">
    /// Requires the complete After decision when true; otherwise requires complete Before restoration.
    /// </param>
    /// <param name="absentGeneration">
    /// Requires restoration of exact initial generation absence when true.
    /// </param>
    /// <returns>
    /// Completion after fresh acquisition and repeated acquisition preserve the same complete decision.
    /// </returns>
    [Theory]
    [InlineData("IntentStaged|@none", false, false)]
    [InlineData("IntentPublished|@none", false, false)]
    [InlineData("MemberStaged|@first", false, false)]
    [InlineData("MemberPublished|@first", false, false)]
    [InlineData("MemberStaged|@late", false, false)]
    [InlineData("MemberPublished|@late", false, false)]
    [InlineData("MemberStaged|@generation", false, false)]
    [InlineData("MemberPublished|@generation", false, false)]
    [InlineData("CommitStaged|@none", false, false)]
    [InlineData("Committed|@none", true, false)]
    [InlineData("CleanupMember|@first", true, false)]
    [InlineData("MemberPublished|@generation", false, true)]
    [InlineData("Committed|@none", true, true)]
    public async Task FreshAcquisitionRecoversActualLoadWithoutExtractionSources(string cut, bool committed, bool absentGeneration)
    {
        var expected = await PortableLoadFixture.CreateAsync(_root, absentGeneration);
        var announced = await RunChildAsync("load-publish", cut, terminateAtCut: true);
        AssertAnnouncement(announced, cut);
        DeleteExactExtraction(announced);
        var recovered = await RunChildAsync("load-recover", "unused", terminateAtCut: false);
        AssertDecision(expected, announced, recovered, committed);
        var repeated = await RunChildAsync("load-recover", "unused", terminateAtCut: false);
        AssertDecision(expected, announced, repeated, committed);
    }

    /// <summary>
    /// Proves forward conversion and interrupted rollback of both topology directions converge to exact Before state.
    /// </summary>
    /// <param name="cut">
    /// The exact forward or rollback phase and relative member name.
    /// </param>
    /// <param name="duringRecovery">
    /// First publishes through generation then interrupts a fresh rollback process when true.
    /// </param>
    /// <returns>
    /// Completion after another fresh acquisition and its repetition restore all old directories, files and generation.
    /// </returns>
    [Theory]
    [InlineData("MemberPublished|lore/cold-file-to-directory", false)]
    [InlineData("DirectoryCreated|lore/cold-file-to-directory", false)]
    [InlineData("DirectoryRemoved|lore/cold-directory-to-file/old-empty", false)]
    [InlineData("DirectoryRemoved|lore/cold-directory-to-file", false)]
    [InlineData("MemberPublished|lore/cold-directory-to-file", false)]
    [InlineData("RollbackDirectoryRemoved|lore/cold-file-to-directory", true)]
    [InlineData("RollbackDirectoryCreated|lore/cold-directory-to-file", true)]
    [InlineData("RollbackStaged|lore/cold-first.bin", true)]
    [InlineData("MemberRestored|lore/cold-file-to-directory", true)]
    [InlineData("MemberRestored|@generation", true)]
    public async Task InterruptedTopologyRecoveryConvergesWithoutExtraction(string cut, bool duringRecovery)
    {
        var expected = await PortableLoadFixture.CreateAsync(_root);
        var initialCut = duringRecovery ? "MemberPublished|@generation" : cut;
        var announced = await RunChildAsync("load-publish", initialCut, terminateAtCut: true);
        AssertAnnouncement(announced, initialCut);
        DeleteExactExtraction(announced);
        if (duringRecovery)
        {
            var rollback = await RunChildAsync("load-recover-cut", cut, terminateAtCut: true);
            AssertAnnouncement(rollback, cut);
            Assert.False(rollback.GetProperty("Committed").GetBoolean());
            Assert.Equal(announced.GetProperty("TransactionId").GetString(), rollback.GetProperty("TransactionId").GetString());
        }
        var recovered = await RunChildAsync("load-recover", "unused", terminateAtCut: false);
        AssertDecision(expected, announced, recovered, committed: false);
        var repeated = await RunChildAsync("load-recover", "unused", terminateAtCut: false);
        AssertDecision(expected, announced, repeated, committed: false);
    }

    /// <summary>
    /// Refuses pending or committed recovery before any earlier member changes when a late unknown child exists.
    /// </summary>
    /// <param name="committed">
    /// Starts from a durable committed decision when true; otherwise starts pending after all files were published.
    /// </param>
    /// <param name="emptyDirectory">
    /// Introduces an empty directory rather than a file, testing directory-only inventory conflicts.
    /// </param>
    /// <returns>
    /// Completion after blocked acquisition preserves all cut bytes and evidence, then exact child removal permits recovery.
    /// </returns>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ColdUnknownLaterMemberRetainsAllEarlierStateAndEvidence(bool committed, bool emptyDirectory)
    {
        var expected = await PortableLoadFixture.CreateAsync(_root);
        var cut = committed ? "Committed|@none" : "MemberPublished|@generation";
        var announced = await RunChildAsync("load-publish", cut, terminateAtCut: true);
        AssertAnnouncement(announced, cut);
        DeleteExactExtraction(announced);
        var unknown = Files.ResolvePath("lore/zz-cold-unknown");
        if (emptyDirectory) Directory.CreateDirectory(unknown);
        else File.WriteAllBytes(unknown, [0xFF, 0, 99]);
        var atConflict = PortableLoadFixture.Snapshot(Files.GameSessionPath);
        var runtimeEvidence = PortableLoadFixture.Snapshot(Path.Combine(Files.RuntimeRootPath, "trusted-local-publication-v1"));
        var generation = File.ReadAllBytes(Files.SessionGenerationPath);

        var blocked = await RunChildAsync("load-recover", "unused", terminateAtCut: false, expectedExit: 90);

        Assert.Equal("ColdScenarioFailed", blocked.GetProperty("Phase").GetString());
        Assert.Equal(nameof(InvalidDataException), blocked.GetProperty("FailureType").GetString());
        AssertNamespace(atConflict, PortableLoadFixture.Snapshot(Files.GameSessionPath));
        AssertNamespace(runtimeEvidence, PortableLoadFixture.Snapshot(Path.Combine(Files.RuntimeRootPath, "trusted-local-publication-v1")));
        Assert.Equal(generation, File.ReadAllBytes(Files.SessionGenerationPath));
        AssertProtected(expected);
        if (emptyDirectory) Directory.Delete(unknown, recursive: false);
        else File.Delete(unknown);
        var recovered = await RunChildAsync("load-recover", "unused", terminateAtCut: false);
        AssertDecision(expected, announced, recovered, committed);
    }

    /// <summary>
    /// Recovers an incomplete exact transaction-owned stage or undo without treating its bytes as canonical authority.
    /// </summary>
    /// <param name="undo">
    /// Cuts real rollback staging when true, or real forward staging otherwise.
    /// </param>
    /// <returns>
    /// Completion after partial owned scratch is safely replaced/removed and exact Before state is restored.
    /// </returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IncompleteExactOwnedScratchRecoversCompleteBeforeDecision(bool undo)
    {
        var expected = await PortableLoadFixture.CreateAsync(_root);
        var initialCut = undo ? "MemberPublished|@generation" : "MemberStaged|lore/cold-first.bin";
        var announced = await RunChildAsync("load-publish", initialCut, terminateAtCut: true);
        AssertAnnouncement(announced, initialCut);
        DeleteExactExtraction(announced);
        var scratchCut = announced;
        if (undo)
        {
            scratchCut = await RunChildAsync("load-recover-cut", "RollbackStaged|lore/cold-first.bin", terminateAtCut: true);
            AssertAnnouncement(scratchCut, "RollbackStaged|lore/cold-first.bin");
        }
        var scratch = ExactScratchPath(scratchCut, undo);
        Assert.True(File.Exists(scratch), "The announced real stage/undo was not present.");
        var active = announced.GetProperty("ActivePath").GetString()!;
        var evidenceHash = PortableLoadFixture.Hash(active);
        File.WriteAllBytes(scratch, [0xFD]);
        Assert.Equal(evidenceHash, PortableLoadFixture.Hash(active));
        var recovered = await RunChildAsync("load-recover", "unused", terminateAtCut: false);
        AssertDecision(expected, announced, recovered, committed: false);
        Assert.False(File.Exists(scratch));
    }

    /// <summary>
    /// Checks the exact announced phase, stable member identity and durable frame decision before assertions use it.
    /// </summary>
    /// <param name="report">
    /// The real child announcement emitted while its observer was waiting.
    /// </param>
    /// <param name="cut">
    /// The requested phase and member selector.
    /// </param>
    private void AssertAnnouncement(JsonElement report, string cut)
    {
        Assert.Equal("CutReady", report.GetProperty("Phase").GetString());
        var parts = cut.Split('|');
        Assert.Equal(parts[0], report.GetProperty("Cut").GetString());
        var index = report.GetProperty("Index").GetInt32();
        if (parts[1] == "@none") Assert.Equal(-1, index);
        else if (parts[1] == "@first") Assert.Equal(report.GetProperty("FirstFileIndex").GetInt32(), index);
        else if (parts[1] == "@late") Assert.Equal(report.GetProperty("LateFileIndex").GetInt32(), index);
        else Assert.Equal(parts[1], report.GetProperty("MemberPath").GetString());
        var frame = report.GetProperty("FramePath").GetString()!;
        AssertOwnedPath(frame);
        Assert.Equal(report.GetProperty("FrameSha256").GetString(), PortableLoadFixture.Hash(frame));
        Assert.Equal(parts[0] != "IntentStaged", report.GetProperty("ActiveExists").GetBoolean());
        Assert.Equal(parts[0] is "Committed" or "CleanupMember", report.GetProperty("Committed").GetBoolean());
        var expected = PortableLoadFixture.Read(_root);
        Assert.Equal(expected.GenerationBefore, report.GetProperty("GenerationBefore").GetString());
        Assert.Equal(expected.GenerationBefore == null ? null : "0123456789abcdef0123456789abcdef",
            report.GetProperty("GenerationBeforeId").GetString());
        using var generation = JsonDocument.Parse(Convert.FromBase64String(report.GetProperty("GenerationAfter").GetString()!));
        Assert.Equal(report.GetProperty("GenerationAfterId").GetString(), generation.RootElement.GetProperty("GenerationId").GetString());
        Assert.True(Guid.TryParseExact(report.GetProperty("GenerationAfterId").GetString(), "N", out _));
    }

    /// <summary>
    /// Deletes only the exact announced private extraction after the owned child has exited.
    /// </summary>
    /// <param name="report">
    /// The terminated child's actual extraction-root announcement.
    /// </param>
    private void DeleteExactExtraction(JsonElement report)
    {
        var path = report.GetProperty("ExtractionRoot").GetString();
        Assert.False(string.IsNullOrEmpty(path));
        AssertOwnedPath(path!);
        var staging = Path.GetFullPath(Path.Combine(Files.RuntimeRootPath, "load-staging")) + Path.DirectorySeparatorChar;
        Assert.True(Path.GetFullPath(path!).StartsWith(staging,
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal));
        Assert.False(File.GetAttributes(path!).HasFlag(FileAttributes.ReparsePoint));
        for (var parent = Path.GetDirectoryName(path!); parent != null && Path.GetFullPath(parent) != Path.GetFullPath(_root);
             parent = Path.GetDirectoryName(parent))
        {
            AssertOwnedPath(parent);
            Assert.False(File.GetAttributes(parent).HasFlag(FileAttributes.ReparsePoint));
        }
        // Validate every descendant before recursive deletion; do not follow unexpected links.
        PortableLoadFixture.Snapshot(path!);
        Directory.Delete(path!, recursive: true);
        Assert.False(Directory.Exists(path));
        Assert.Empty(Directory.EnumerateFileSystemEntries(staging.TrimEnd(Path.DirectorySeparatorChar)));
    }

    /// <summary>
    /// Requires complete independent namespace, generation and protected-source expectations after ordinary acquisition.
    /// </summary>
    /// <param name="expected">
    /// Original fixture-exported ZIP/before expectations.
    /// </param>
    /// <param name="announced">
    /// Original load cut retaining exact generated After bytes.
    /// </param>
    /// <param name="recovered">
    /// The fresh ordinary acquisition report.
    /// </param>
    /// <param name="committed">
    /// Selects complete After rather than Before expectations.
    /// </param>
    private void AssertDecision(PortableLoadFixtureState expected, JsonElement announced, JsonElement recovered, bool committed)
    {
        Assert.Equal("CanonicalAcquisitionReturned", recovered.GetProperty("Phase").GetString());
        AssertNamespace(committed ? expected.After : expected.Before, PortableLoadFixture.Snapshot(Files.GameSessionPath));
        var generation = committed ? announced.GetProperty("GenerationAfter").GetString() : expected.GenerationBefore;
        Assert.Equal(generation, recovered.GetProperty("GenerationBytes").GetString());
        Assert.Equal(generation != null, File.Exists(Files.SessionGenerationPath));
        if (generation != null) Assert.Equal(Convert.FromBase64String(generation), File.ReadAllBytes(Files.SessionGenerationPath));
        Assert.Equal(committed ? announced.GetProperty("GenerationAfterId").GetString() :
            expected.GenerationBefore == null ? null : "0123456789abcdef0123456789abcdef", recovered.GetProperty("GenerationId").GetString());
        AssertProtected(expected);
        var journalRoot = Path.Combine(Files.RuntimeRootPath, "trusted-local-publication-v1");
        Assert.Empty(Directory.EnumerateFileSystemEntries(journalRoot));
        Assert.Empty(Directory.EnumerateFiles(Files.GameSessionPath, ".boe-local-*", SearchOption.AllDirectories));
        Assert.Empty(Directory.EnumerateFiles(Path.GetDirectoryName(Files.SessionGenerationPath)!, ".boe-local-*", SearchOption.TopDirectoryOnly));
        var staging = Path.Combine(Files.RuntimeRootPath, "load-staging");
        if (Directory.Exists(staging)) Assert.Empty(Directory.EnumerateFileSystemEntries(staging));
    }

    /// <summary>
    /// Requires exact protected directory membership, source and library bytes independently of replacement outcomes.
    /// </summary>
    /// <param name="expected">
    /// Original protected-library snapshot including the selected source.
    /// </param>
    private void AssertProtected(PortableLoadFixtureState expected)
    {
        var actual = PortableLoadFixture.Snapshot(Files.GameSessionPath)
            .Where(pair => pair.Key == "saves" || pair.Key.StartsWith("saves/", StringComparison.Ordinal))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        AssertNamespace(expected.Protected, actual);
    }

    /// <summary>
    /// Compares complete names and markers/hashes without permitting unlisted empty directories.
    /// </summary>
    /// <param name="expected">
    /// The independently expected complete namespace.
    /// </param>
    /// <param name="actual">
    /// The freshly observed complete namespace.
    /// </param>
    private static void AssertNamespace(Dictionary<string, string> expected, Dictionary<string, string> actual) =>
        Assert.Equal(expected.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToArray(), actual.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToArray());

    /// <summary>
    /// Derives a real stage/undo name from the announced transaction/index and stable wire-directory anchor.
    /// </summary>
    /// <param name="report">
    /// The exact file-stage callback report.
    /// </param>
    /// <param name="undo">
    /// Selects the rollback undo suffix when true.
    /// </param>
    /// <returns>
    /// The exact owned scratch path; no broad scratch deletion or target-plan API is used.
    /// </returns>
    private string ExactScratchPath(JsonElement report, bool undo)
    {
        using var frame = PortableLoadFixture.ReadFrame(report.GetProperty("FramePath").GetString()!);
        var members = frame.RootElement.GetProperty("Members").EnumerateArray().ToArray();
        var index = report.GetProperty("Index").GetInt32();
        var parent = Path.GetDirectoryName(members[index].GetProperty("Path").GetString()!)!;
        while (true)
        {
            var node = members.SingleOrDefault(member => member.GetProperty("Path").GetString() == parent);
            if (node.ValueKind == JsonValueKind.Object && node.GetProperty("Before").GetProperty("Kind").GetString() == "Directory" &&
                node.GetProperty("After").GetProperty("Kind").GetString() == "Directory") break;
            parent = Path.GetDirectoryName(parent) ?? throw new InvalidDataException("No stable scratch anchor.");
        }
        var path = Path.Combine(parent, $".boe-local-{report.GetProperty("TransactionId").GetString()}-{index}.{(undo ? "undo" : "stage")}");
        AssertOwnedPath(path);
        return path;
    }

    /// <summary>
    /// Runs one sequential child with exact inherited heap and bounded RSS/time, then kills only an announced waiting child.
    /// </summary>
    /// <param name="mode">
    /// The actual typed load, ordinary recovery or recovery-cut dispatch mode.
    /// </param>
    /// <param name="cut">
    /// The exact phase/member selector, or unused for ordinary recovery.
    /// </param>
    /// <param name="terminateAtCut">
    /// Requires a live CutReady child and terminates that owned process when true.
    /// </param>
    /// <param name="expectedExit">
    /// The required ordinary return code; ignored for deliberate parent termination.
    /// </param>
    /// <returns>
    /// The child's single structured announcement after its owned process has exited.
    /// </returns>
    private async Task<JsonElement> RunChildAsync(string mode, string cut, bool terminateAtCut, int expectedExit = 0)
    {
        var assembly = typeof(PortableLoadColdRecoveryTests).Assembly.Location;
        var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in new[] { "exec", "--runtimeconfig", Path.ChangeExtension(assembly, ".runtimeconfig.json"),
            "--depsfile", Path.ChangeExtension(assembly, ".deps.json"), typeof(PortableStorageCrashHost.Program).Assembly.Location,
            _root, HeapBytes.ToString(), mode, cut }) start.ArgumentList.Add(argument);
        start.Environment["DOTNET_GCHeapHardLimit"] = "0x30000000";
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Owned load child did not start.");
        var line = process.StandardOutput.ReadLineAsync();
        var error = process.StandardError.ReadToEndAsync();
        var timer = Stopwatch.StartNew();
        long peak = 0;
        try
        {
            while (!line.IsCompleted) { CheckChildBounds(process, timer, ref peak); await Task.Delay(25); }
            var text = await line;
            Assert.False(string.IsNullOrEmpty(text), "Owned load child returned without a structured report.");
            using var document = JsonDocument.Parse(text!);
            var report = document.RootElement.Clone();
            Assert.Equal(HeapBytes, report.GetProperty("HeapBytes").GetInt64());
            if (terminateAtCut)
            {
                Assert.Equal("CutReady", report.GetProperty("Phase").GetString());
                Assert.False(process.HasExited, "Requested cut must be waiting for parent termination.");
                CheckChildBounds(process, timer, ref peak);
                process.Kill(entireProcessTree: true);
            }
            while (!process.HasExited) { CheckChildBounds(process, timer, ref peak); await Task.Delay(25); }
            await process.WaitForExitAsync();
            var remainder = await process.StandardOutput.ReadToEndAsync();
            var stderr = await error;
            Assert.True(string.IsNullOrWhiteSpace(remainder), text + remainder + stderr);
            if (!terminateAtCut) Assert.True(process.ExitCode == expectedExit, text + stderr);
            output.WriteLine("mode={0}; requested={1}; parentTerminated={2}; sampledPeakBytes={3}; elapsedMs={4}; report={5}; stderr={6}",
                mode, cut, terminateAtCut, peak, timer.Elapsed.TotalMilliseconds, text, stderr);
            return report;
        }
        finally
        {
            if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); }
        }
    }

    /// <summary>
    /// Samples owned-child RSS and stops when the declared cold-process envelope is exceeded.
    /// </summary>
    /// <param name="process">
    /// The exact owned child, never a discovered unrelated process.
    /// </param>
    /// <param name="timer">
    /// Elapsed time since this child started.
    /// </param>
    /// <param name="peak">
    /// The maximum observed RSS, updated in place.
    /// </param>
    private static void CheckChildBounds(Process process, Stopwatch timer, ref long peak)
    {
        if (!process.HasExited)
        {
            try
            {
                process.Refresh();
                if (!process.HasExited) peak = Math.Max(peak, process.WorkingSet64);
            }
            catch (InvalidOperationException) when (process.HasExited)
            {
                // Ordinary acquisition may finish between the exit check and process metric access.
            }
        }
        if (peak > 1024L * 1024 * 1024 || timer.Elapsed > TimeSpan.FromSeconds(180))
            throw new InvalidOperationException("Owned cold load child exceeded 1 GiB RSS or 180 seconds.");
    }

    /// <summary>
    /// Confirms an exact file/deletion target stays inside the intended owned root before mutation.
    /// </summary>
    /// <param name="path">
    /// The absolute child-reported or independently derived path.
    /// </param>
    private void AssertOwnedPath(string path) => Assert.True(Path.GetFullPath(path).StartsWith(
        Path.GetFullPath(_root) + Path.DirectorySeparatorChar,
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal));

    /// <summary>
    /// Removes only the isolated fixture after every child runner has stopped its owned process.
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
