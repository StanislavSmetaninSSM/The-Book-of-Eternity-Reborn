using System.Diagnostics;
using System.Runtime.ExceptionServices;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Exercises real Windows metadata attempts through tiny independent ResolvePath fixtures.
/// </summary>
public sealed class WindowsPathMetadataIntegrationTests : IDisposable
{
    private readonly string _parent = Path.GetFullPath(Path.Combine(
        TestRepoPaths.RepoRoot, "TestResults", "windows-metadata-owned"));
    private readonly ITestOutputHelper _output;
    private readonly string _root;
    private readonly List<string> _directoryLinks = new();
    private readonly List<string> _fileLinks = new();
    private readonly List<PathMetadataQuery> _queries = new();
    private readonly FileSystemManager _manager;
    private static readonly byte[] Payload = [0xEF, 0xBB, 0xBF, 0, 0xFF, 42];
    private static readonly byte[] Sentinel = [0xFE, 19, 0, 87];

    /// <summary>
    /// Creates only this instance's GUID-owned root, canonical root and outside sentinel.
    /// </summary>
    /// <param name="output">
    /// The test's TRX-bound output sink for complete query receipts and the owned-root spelling.
    /// </param>
    public WindowsPathMetadataIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
        Assert.True(OperatingSystem.IsWindows(), "These fixtures require real Windows metadata APIs.");
        _root = Path.Combine(_parent, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _manager = Manager(_queries.Add);
        Directory.CreateDirectory(_manager.GameSessionPath);
        File.WriteAllBytes(Path.Combine(_root, "outside-sentinel.bin"), Sentinel);
    }

    /// <summary>
    /// Requires fewer actual managed metadata attempts after asserting complete stable physical results.
    /// </summary>
    /// <param name="depth">
    /// The number of relative components, selected independently as one, two or four.
    /// </param>
    /// <param name="leafKind">
    /// The final component kind: directory, file or missing.
    /// </param>
    /// <param name="expectedQueries">
    /// The hand-derived folded count including the root directory observation.
    /// </param>
    [Theory]
    [InlineData(1, "directory", 2)]
    [InlineData(2, "directory", 3)]
    [InlineData(4, "directory", 5)]
    [InlineData(1, "file", 3)]
    [InlineData(2, "file", 4)]
    [InlineData(4, "file", 6)]
    [InlineData(1, "missing", 3)]
    [InlineData(2, "missing", 4)]
    [InlineData(4, "missing", 6)]
    public void ResolvePathOrdinaryComponentsUseFoldedManagedQueryCount(
        int depth, string leafKind, int expectedQueries)
    {
        // Mutation caught: retaining the redundant final attributes query fails every count case.
        var relative = string.Join(Path.DirectorySeparatorChar.ToString(),
            Enumerable.Range(1, depth).Select(index => "component" + index));
        var target = Path.Combine(_manager.GameSessionPath, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        if (leafKind == "directory")
            Directory.CreateDirectory(target);
        else if (leafKind == "file")
            File.WriteAllBytes(target, Payload);
        var before = Snapshot();
        _queries.Clear();

        var actual = _manager.ResolvePath(relative);

        Assert.Equal(target, actual);
        Assert.Equal(leafKind == "directory", Directory.Exists(actual));
        Assert.Equal(leafKind == "file", File.Exists(actual));
        if (leafKind == "file")
            Assert.Equal(Payload, File.ReadAllBytes(actual));
        Assert.Equal(Sentinel, File.ReadAllBytes(Path.Combine(_root, "outside-sentinel.bin")));
        Assert.Equal(before, Snapshot());
        Assert.All(_queries, query => Assert.True(
            query.Path.Equals(_manager.GameSessionPath, StringComparison.OrdinalIgnoreCase) ||
            query.Path.StartsWith(_manager.GameSessionPath + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase)));
        Assert.Equal(expectedQueries, _queries.Count);
    }

    /// <summary>
    /// Preserves helper asymmetry when the canonical root is an ordinary file.
    /// </summary>
    [Fact]
    public void ResolvePathRootFileMatchesOriginalDirectoryOnlyDecision()
    {
        Directory.Delete(_manager.GameSessionPath);
        File.WriteAllBytes(_manager.GameSessionPath, Payload);
        AssertSameOutcome(_manager.GameSessionPath, _manager.GameSessionPath,
            () => _manager.ResolvePath(""));
        var child = Path.Combine(_manager.GameSessionPath, "child");
        AssertSameOutcome(_manager.GameSessionPath, child, () => _manager.ResolvePath("child"));
        Assert.Equal(Payload, File.ReadAllBytes(_manager.GameSessionPath));
        Assert.Equal(Sentinel, File.ReadAllBytes(Path.Combine(_root, "outside-sentinel.bin")));
    }

    /// <summary>
    /// Retains root-equals-path behavior and the manager's original invalid-relative-path decisions.
    /// </summary>
    [Fact]
    public void ResolvePathRootAndNormalizationContractsRemainIntact()
    {
        Assert.Equal(_manager.GameSessionPath, _manager.ResolvePath("  "));
        Assert.Equal(Path.Combine(_manager.GameSessionPath, "child"), _manager.ResolvePath(" child "));
        Assert.Throws<ArgumentNullException>(() => _manager.ResolvePath(null!));
        Assert.Throws<InvalidDataException>(() => _manager.ResolvePath(".."));
        Assert.Throws<InvalidDataException>(() => _manager.ResolvePath("child//leaf"));
        Assert.Throws<InvalidDataException>(() => _manager.ResolvePath("child/"));
        Assert.Throws<InvalidDataException>(() => _manager.ResolvePath("C:child"));
        Assert.Throws<InvalidDataException>(() => _manager.ResolvePath(Path.GetFullPath(_root)));
        var ordinaryFile = Path.Combine(_manager.GameSessionPath, "trailing-separator.bin");
        File.WriteAllBytes(ordinaryFile, Payload);
        var trailingSpelling = ordinaryFile + Path.DirectorySeparatorChar;
        var observation = new WindowsPathMetadataProvider(_queries.Add)
            .ObserveFile(trailingSpelling, PathMetadataRole.Segment);
        Assert.False(observation.Exists);
        Assert.Equal(File.Exists(trailingSpelling), observation.Exists);
        Assert.Equal(Payload, File.ReadAllBytes(ordinaryFile));
        Assert.Equal(Sentinel, File.ReadAllBytes(Path.Combine(_root, "outside-sentinel.bin")));
    }

    /// <summary>
    /// Preserves missing-root, missing-ancestor and ordinary-file-blocker decisions without creating data.
    /// </summary>
    /// <param name="shape">
    /// Selects an absent root, absent ancestor or ordinary file blocking the descendant spelling.
    /// </param>
    [Theory]
    [InlineData("missing-root")]
    [InlineData("missing-ancestor")]
    [InlineData("file-blocker")]
    public void ResolvePathMissingAndBlockerMatchOriginalStableOutcome(string shape)
    {
        if (shape == "missing-root")
            Directory.Delete(_manager.GameSessionPath);
        else if (shape == "file-blocker")
            File.WriteAllBytes(Path.Combine(_manager.GameSessionPath, "parent"), Payload);
        var target = Path.Combine(_manager.GameSessionPath, "parent", "child");
        var before = Snapshot();
        AssertSameOutcome(_manager.GameSessionPath, target, () => _manager.ResolvePath("parent/child"));
        Assert.False(File.Exists(target));
        Assert.False(Directory.Exists(target));
        Assert.Equal(before, Snapshot());
    }

    /// <summary>
    /// Compares real stable links against the original helper, including dangling links and root roles.
    /// </summary>
    /// <param name="kind">
    /// Selects a directory symbolic link, file symbolic link or directory junction.
    /// </param>
    /// <param name="dangling">
    /// <see langword="true"/> leaves the link target absent; <see langword="false"/> creates and checks its exact payload.
    /// </param>
    /// <param name="atRoot">
    /// <see langword="true"/> replaces the canonical root; <see langword="false"/> places the link at a relative segment.
    /// </param>
    [Theory]
    [InlineData("directory", false, false)]
    [InlineData("directory", true, false)]
    [InlineData("file", false, false)]
    [InlineData("file", true, false)]
    [InlineData("junction", false, false)]
    [InlineData("junction", true, false)]
    [InlineData("directory", false, true)]
    [InlineData("directory", true, true)]
    [InlineData("file", false, true)]
    [InlineData("file", true, true)]
    public void ResolvePathStableLinksMatchOriginalOutcome(string kind, bool dangling, bool atRoot)
    {
        var target = Path.Combine(_root, "outside-target");
        if (!dangling)
        {
            if (kind == "file")
                File.WriteAllBytes(target, Payload);
            else
            {
                Directory.CreateDirectory(target);
                File.WriteAllBytes(Path.Combine(target, "payload.bin"), Payload);
            }
        }
        var link = atRoot ? _manager.GameSessionPath : Path.Combine(_manager.GameSessionPath, "link");
        if (atRoot)
            Directory.Delete(link);
        CreateLink(kind, link, target);

        AssertSameOutcome(_manager.GameSessionPath, link, () => _manager.ResolvePath(atRoot ? "" : "link"));

        Assert.True(File.GetAttributes(link).HasFlag(FileAttributes.ReparsePoint));
        Assert.Equal(Sentinel, File.ReadAllBytes(Path.Combine(_root, "outside-sentinel.bin")));
        if (!dangling)
            Assert.Equal(Payload, File.ReadAllBytes(kind == "file" ? target : Path.Combine(target, "payload.bin")));
        else
        {
            Assert.False(File.Exists(target));
            Assert.False(Directory.Exists(target));
        }
    }

    /// <summary>
    /// Requires a fresh manager call to detect a persistent normal-directory to junction replacement.
    /// </summary>
    [Fact]
    public void ResolvePathFreshCallRefusesReplacementAndPreservesOutsideData()
    {
        var child = Path.Combine(_manager.GameSessionPath, "child");
        var outside = Path.Combine(_root, "outside-target");
        Directory.CreateDirectory(child);
        Directory.CreateDirectory(outside);
        File.WriteAllBytes(Path.Combine(outside, "payload.bin"), Payload);
        Assert.Equal(child, _manager.ResolvePath("child"));
        Directory.Delete(child);
        CreateLink("junction", child, outside);
        var error = Assert.Throws<InvalidDataException>(() => _manager.ResolvePath("child/payload.bin"));
        Assert.Equal($"Canonical game-session path traverses reparse point '{child}'.", error.Message);
        Assert.Equal(Payload, File.ReadAllBytes(Path.Combine(outside, "payload.bin")));
        Assert.Equal(Sentinel, File.ReadAllBytes(Path.Combine(_root, "outside-sentinel.bin")));
    }

    /// <summary>
    /// Observes same-name directory, ordinary-file and absence replacements on independent manager calls.
    /// </summary>
    [Fact]
    public void ResolvePathFreshCallsPreserveOrdinaryTypeAndAbsenceReplacementSemantics()
    {
        var child = Path.Combine(_manager.GameSessionPath, "child");
        Directory.CreateDirectory(child);
        Assert.Equal(child, _manager.ResolvePath("child"));
        Directory.Delete(child);
        File.WriteAllBytes(child, Payload);
        AssertSameOutcome(_manager.GameSessionPath, child, () => _manager.ResolvePath("child"));
        Assert.Equal(Payload, File.ReadAllBytes(child));
        File.Delete(child);
        AssertSameOutcome(_manager.GameSessionPath, child, () => _manager.ResolvePath("child"));
        Assert.False(File.Exists(child));
        Assert.False(Directory.Exists(child));
        Assert.Equal(Sentinel, File.ReadAllBytes(Path.Combine(_root, "outside-sentinel.bin")));
    }

    /// <summary>
    /// Isolates passive observer failures from both normal results and real reparse refusals.
    /// </summary>
    [Fact]
    public void PassiveObserverFailureCannotAlterResultOrRefusal()
    {
        var files = Manager(query =>
        {
            _queries.Add(query);
            throw new InvalidOperationException("diagnostic only");
        });
        var ordinary = Path.Combine(files.GameSessionPath, "payload.bin");
        File.WriteAllBytes(ordinary, Payload);
        Assert.Equal(ordinary, files.ResolvePath("payload.bin"));
        var outside = Path.Combine(_root, "outside-target");
        Directory.CreateDirectory(outside);
        var link = Path.Combine(files.GameSessionPath, "link");
        CreateLink("junction", link, outside);
        Assert.Throws<InvalidDataException>(() => files.ResolvePath("link"));
        Assert.Equal(Payload, File.ReadAllBytes(ordinary));
        Assert.Equal(Sentinel, File.ReadAllBytes(Path.Combine(_root, "outside-sentinel.bin")));
    }

    /// <summary>
    /// Creates a manager with only a passive invocation-scoped metadata observer.
    /// </summary>
    /// <param name="observer">
    /// The diagnostic callback; <see langword="null"/> disables metadata observations.
    /// </param>
    /// <returns>
    /// A real manager using the physical operations adapter and this fixture's base path.
    /// </returns>
    private FileSystemManager Manager(Action<PathMetadataQuery>? observer) => new(
        _root, NullLogger<FileSystemManager>.Instance, PhysicalLoadTransactionOperations.Instance,
        new FileSystemManagerHooks { PathMetadataQueryObserver = observer });

    /// <summary>
    /// Captures every ordinary fixture directory and file image before an observation-only walk.
    /// </summary>
    /// <returns>
    /// An ordered exact snapshot; this helper is used only before links are introduced.
    /// </returns>
    private string[] Snapshot() => Directory.EnumerateDirectories(_root, "*", SearchOption.AllDirectories)
        .Select(path => "directory:" + Path.GetRelativePath(_root, path))
        .Concat(Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories)
            .Select(path => "file:" + Path.GetRelativePath(_root, path) + ":" + Convert.ToHexString(File.ReadAllBytes(path))))
        .Order(StringComparer.Ordinal).ToArray();

    /// <summary>
    /// Compares the real manager's outcome with the original stable root and segment algorithm.
    /// </summary>
    /// <param name="root">
    /// The canonical root, including file and link spellings under test.
    /// </param>
    /// <param name="path">
    /// The already contained candidate path.
    /// </param>
    /// <param name="resolve">
    /// The actual public manager call for the same spelling.
    /// </param>
    private static void AssertSameOutcome(string root, string path, Func<string> resolve)
    {
        var expected = Record.Exception(() => LegacyEnsure(root, path));
        string? resolved = null;
        var actual = Record.Exception(() => resolved = resolve());
        Assert.Equal(expected?.GetType(), actual?.GetType());
        Assert.Equal(expected?.Message, actual?.Message);
        if (expected == null)
            Assert.Equal(path, resolved);
    }

    /// <summary>
    /// Retains the original stable helper as an independent comparison oracle for real filesystem fixtures.
    /// </summary>
    /// <param name="root">
    /// The directory-only root query path.
    /// </param>
    /// <param name="path">
    /// The root or descendant candidate to inspect in directory-first order.
    /// </param>
    private static void LegacyEnsure(string root, string path)
    {
        var current = root;
        if (Directory.Exists(current) && FileSystemManager.IsReparsePoint(current))
            throw new InvalidDataException("Canonical game-session root cannot be a reparse point.");
        var relative = Path.GetRelativePath(root, path);
        if (relative == ".")
            return;
        foreach (var segment in relative.Split(
                     [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            if (!Directory.Exists(current) && !File.Exists(current))
                break;
            if (FileSystemManager.IsReparsePoint(current))
                throw new InvalidDataException($"Canonical game-session path traverses reparse point '{current}'.");
        }
    }

    /// <summary>
    /// Creates a real contained link and reports unavailable link privileges as an explicit fixture failure.
    /// </summary>
    /// <param name="kind">
    /// The file, directory or junction kind.
    /// </param>
    /// <param name="link">
    /// The owned link path, removed without traversing its target during disposal.
    /// </param>
    /// <param name="target">
    /// The target inside this instance's owned root; it may be absent.
    /// </param>
    private void CreateLink(string kind, string link, string target)
    {
        AssertOwned(link);
        AssertOwned(target);
        if (kind == "junction")
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                Arguments = $"/d /c mklink /J \"{link}\" \"{target}\""
            });
            Assert.NotNull(process);
            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();
            Assert.True(process.ExitCode == 0, $"Junction fixture unavailable: {stdout}\n{stderr}");
            _directoryLinks.Add(link);
            return;
        }
        var error = Record.Exception(() =>
        {
            if (kind == "file")
                File.CreateSymbolicLink(link, target);
            else
                Directory.CreateSymbolicLink(link, target);
        });
        Assert.True(error == null, $"Real {kind} symbolic-link fixture unavailable; not qualified: {error}");
        (kind == "file" ? _fileLinks : _directoryLinks).Add(link);
    }

    /// <summary>
    /// Verifies absolute containment before any owned fixture mutation or deletion.
    /// </summary>
    /// <param name="path">
    /// The candidate, which must be strictly beneath this instance's GUID root.
    /// </param>
    private void AssertOwned(string path) => Assert.True(Path.GetFullPath(path).StartsWith(
        Path.GetFullPath(_root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Attempts the full query receipt, then removes tracked links and the validated GUID-owned root.
    /// A receipt failure is surfaced after cleanup; simultaneous receipt and cleanup failures are aggregated.
    /// </summary>
    public void Dispose()
    {
        Exception? receiptError = null;
        try
        {
            WriteQueryReceipt();
        }
        catch (Exception error)
        {
            receiptError = error;
        }
        try
        {
            var absolute = Path.GetFullPath(_root);
            Assert.True(absolute.StartsWith(_parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
            Assert.True(Guid.TryParseExact(Path.GetFileName(absolute), "N", out _));
            foreach (var link in _fileLinks)
            {
                AssertOwned(link);
                File.Delete(link);
            }
            foreach (var link in _directoryLinks)
            {
                AssertOwned(link);
                Directory.Delete(link, recursive: false);
            }
            Directory.Delete(absolute, recursive: true);
        }
        catch (Exception cleanupError)
        {
            if (receiptError != null)
                throw new AggregateException("Metadata receipt and owned cleanup both failed.", cleanupError, receiptError);
            throw;
        }
        if (receiptError != null)
            ExceptionDispatchInfo.Capture(receiptError).Throw();
    }

    /// <summary>
    /// Writes every actual managed-query receipt outside the fixture before cleanup, including after failed assertions.
    /// Output-sink failures propagate to disposal for reporting after owned cleanup is attempted.
    /// </summary>
    private void WriteQueryReceipt()
    {
        _output.WriteLine($"OwnedRoot: {_root}");
        _output.WriteLine($"ManagedQueryCount: {_queries.Count}");
        for (var index = 0; index < _queries.Count; index++)
        {
            var query = _queries[index];
            _output.WriteLine($"ManagedQuery[{index}]: kind={query.Kind}; role={query.Role}; path={query.Path}");
        }
    }
}
