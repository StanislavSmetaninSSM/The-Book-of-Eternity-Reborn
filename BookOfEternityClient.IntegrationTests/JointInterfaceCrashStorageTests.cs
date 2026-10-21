using System.Diagnostics;
using System.Text.Json;
using BookOfEternityClient.Core;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Checks explicit fixture placement with independent small directories and no game construction.
/// </summary>
public sealed class JointInterfaceCrashStorageTests
{
    private readonly ITestOutputHelper _output;

    /// <summary>
    /// Retains the receiver for complete placement and closing evidence.
    /// </summary>
    /// <param name="output">
    /// The current test's uncapped output receiver.
    /// </param>
    public JointInterfaceCrashStorageTests(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// Selects a fresh strict child for every ordinary Windows package cut without allocating it.
    /// </summary>
    /// <param name="cut">
    /// The original cut index from zero through four.
    /// </param>
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public void ExplicitParent_SelectsFreshChildWithoutAllocation(int cut)
    {
        using var fixture = new StorageFixture(_output);
        var nonce = Guid.NewGuid().ToString("N");
        var selected = Select(fixture, nonce, Eligible(cut));
        Assert.Equal(Path.Combine(fixture.Root, "boe-joint-interface-crash-" + nonce), selected);
        Assert.False(Path.Exists(selected));
        Assert.Equal("untouched", File.ReadAllText(fixture.Marker));
        _output.WriteLine(JsonSerializer.Serialize(new { Parent = fixture.Root, OwnedRoot = selected, Allocated = false }));
    }

    /// <summary>
    /// Leaves original temporary placement available independently of platform and capture selection.
    /// </summary>
    /// <param name="windows">
    /// The existing platform switch.
    /// </param>
    /// <param name="capture">
    /// The existing managed capture switch, which is checked only for an explicit override.
    /// </param>
    [Theory]
    [InlineData(true, false)] [InlineData(false, false)] [InlineData(true, true)] [InlineData(false, true)]
    public void NoOverride_PreservesTemporaryBranch(bool windows, bool capture)
    {
        var nonce = Guid.NewGuid().ToString("N");
        var temporary = Path.GetTempPath();
        var selection = Eligible(0) with { Windows = windows, ManagedCapture = capture, PackageSelected = false };
        Assert.Equal(Path.Combine(temporary, "boe-joint-interface-crash-" + nonce),
            JointInterfaceCrashStorage.SelectOwnedRoot(temporary, null, nonce, selection));
    }

    /// <summary>
    /// Refuses every incompatible explicit selection before allocating a child.
    /// </summary>
    /// <param name="ineligible">
    /// The one incompatible existing switch or cut changed from an eligible request.
    /// </param>
    [Theory]
    [InlineData("linux")] [InlineData("unextended")] [InlineData("no-package")]
    [InlineData("raw")] [InlineData("acquisition")] [InlineData("capture")]
    [InlineData("diagnostic")] [InlineData("fault")] [InlineData("negative-cut")] [InlineData("later-cut")]
    public void ExplicitParent_RejectsIncompatibleModeBeforeAllocation(string ineligible)
    {
        using var fixture = new StorageFixture(_output);
        var selection = ineligible switch
        {
            "linux" => Eligible(0) with { Windows = false },
            "unextended" => Eligible(0) with { ExtendedWindowsCrash = false },
            "no-package" => Eligible(0) with { PackageSelected = false },
            "raw" => Eligible(0) with { RawSeedSelected = true },
            "acquisition" => Eligible(0) with { AcquisitionOnly = true },
            "capture" => Eligible(0) with { ManagedCapture = true },
            "diagnostic" => Eligible(0) with { Mode = new(0, true, null) },
            "fault" => Eligible(0) with { Mode = new(0, false, "Version") },
            "negative-cut" => Eligible(-1),
            "later-cut" => Eligible(5),
            _ => throw new ArgumentOutOfRangeException(nameof(ineligible))
        };
        Assert.Throws<InvalidDataException>(() => Select(fixture, Guid.NewGuid().ToString("N"), selection));
        Assert.Equal(new[] { fixture.Marker }, Directory.GetFileSystemEntries(fixture.Root));
    }

    /// <summary>
    /// Refuses unavailable or malformed parents without creating them or changing existing data.
    /// </summary>
    /// <param name="kind">
    /// The missing, file, relative, empty or whitespace parent case.
    /// </param>
    [Theory]
    [InlineData("missing")] [InlineData("file")] [InlineData("relative")]
    [InlineData("empty")] [InlineData("blank")]
    public void ExplicitParent_RejectsInvalidParentWithoutFallback(string kind)
    {
        using var fixture = new StorageFixture(_output);
        var missing = Path.Combine(fixture.Root, "absent");
        var parent = kind switch
        {
            "missing" => missing, "file" => fixture.Marker,
            "relative" => "relative-placement", "empty" => "", "blank" => " ",
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        Assert.Throws<InvalidDataException>(() => JointInterfaceCrashStorage.SelectOwnedRoot(
            Path.GetTempPath(), parent, Guid.NewGuid().ToString("N"), Eligible(0)));
        Assert.False(Path.Exists(missing));
        Assert.Equal("untouched", File.ReadAllText(fixture.Marker));
        Assert.Equal(new[] { fixture.Marker }, Directory.GetFileSystemEntries(fixture.Root));
    }

    /// <summary>
    /// Refuses existing candidate names without acquiring or modifying their sentinel data.
    /// </summary>
    /// <param name="directory">
    /// Whether the preexisting name is a directory rather than a regular file.
    /// </param>
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ExplicitParent_RefusesExistingCandidate(bool directory)
    {
        using var fixture = new StorageFixture(_output);
        var nonce = Guid.NewGuid().ToString("N");
        var candidate = Path.Combine(fixture.Root, "boe-joint-interface-crash-" + nonce);
        var sentinel = candidate;
        if (directory) { Directory.CreateDirectory(candidate); sentinel = Path.Combine(candidate, "sentinel"); }
        File.WriteAllText(sentinel, "existing");
        Assert.Throws<IOException>(() => Select(fixture, nonce, Eligible(0)));
        Assert.Equal("existing", File.ReadAllText(sentinel));
    }

    /// <summary>
    /// Prevents a malformed suffix from escaping the fresh child naming contract.
    /// </summary>
    /// <param name="nonce">
    /// An empty, traversal, uppercase or wrong-length suffix.
    /// </param>
    [Theory]
    [InlineData("")] [InlineData("../escape")] [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("0123")]
    public void ExplicitParent_RejectsInvalidNonce(string nonce)
    {
        using var fixture = new StorageFixture(_output);
        Assert.Throws<InvalidDataException>(() => Select(fixture, nonce, Eligible(0)));
        Assert.Equal(new[] { fixture.Marker }, Directory.GetFileSystemEntries(fixture.Root));
    }

    /// <summary>
    /// Routes literal child temporary settings beneath the selected container without changing the parent environment.
    /// </summary>
    [Fact]
    public void SelectedParent_PreservesChildOnlyTemporaryEnvironment()
    {
        using var fixture = new StorageFixture(_output);
        var selected = Select(fixture, Guid.NewGuid().ToString("N"), Eligible(0));
        Assert.Equal(fixture.Root, Path.GetDirectoryName(selected));
        Directory.CreateDirectory(Path.Combine(selected, "tmp"));
        var runtime = Path.Combine(fixture.Root, "runtime with spaces");
        Directory.CreateDirectory(runtime);
        File.WriteAllText(Path.Combine(runtime, "dotnet.exe"), "fixture, never executed");
        File.WriteAllText(Path.Combine(runtime, "dotnet"), "fixture, never executed");
        var variables = new[] { "TEMP", "TMP", "TMPDIR" };
        var original = variables.Select(Environment.GetEnvironmentVariable).ToArray();
        var windows = JointInterfaceCrashProcessSupport.CreateChildStartInfo(runtime, ["exec", "literal name.dll"], selected, true);
        Assert.Equal(Path.Combine(selected, "tmp"), windows.Environment["TEMP"]);
        Assert.Equal(Path.Combine(selected, "tmp"), windows.Environment["TMP"]);
        var linux = JointInterfaceCrashProcessSupport.CreateChildStartInfo(runtime, ["exec", "literal name.dll"], selected, false);
        Assert.Equal(Path.Combine(selected, "tmp"), linux.Environment["TMPDIR"]);
        Assert.Equal(original, variables.Select(Environment.GetEnvironmentVariable).ToArray());
        Assert.Equal(selected, windows.WorkingDirectory);
        Assert.Equal(selected, linux.WorkingDirectory);
    }

    /// <summary>
    /// Refuses a real Windows junction at the selected parent or its ancestor without changing the target.
    /// </summary>
    /// <param name="linkedAncestor">
    /// Whether the requested parent is an ordinary child below the junction rather than the junction itself.
    /// </param>
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ExplicitParent_RefusesRealJunctionBoundary(bool linkedAncestor)
    {
        Assert.True(OperatingSystem.IsWindows(), "This explicit Windows placement category requires Windows.");
        using var fixture = new StorageFixture(_output);
        var target = Path.Combine(fixture.Root, "outside-target");
        var targetParent = linkedAncestor ? Path.Combine(target, "ordinary-child") : target;
        Directory.CreateDirectory(targetParent);
        var sentinel = Path.Combine(targetParent, "sentinel");
        File.WriteAllText(sentinel, "outside unchanged");
        var link = Path.Combine(fixture.Root, "junction");
        fixture.CreateJunction(link, target);
        var requested = linkedAncestor ? Path.Combine(link, "ordinary-child") : link;
        var nonce = Guid.NewGuid().ToString("N");
        Assert.Throws<InvalidDataException>(() => JointInterfaceCrashStorage.SelectOwnedRoot(
            Path.GetTempPath(), requested, nonce, Eligible(0)));
        Assert.False(Path.Exists(Path.Combine(targetParent, "boe-joint-interface-crash-" + nonce)));
        Assert.Equal("outside unchanged", File.ReadAllText(sentinel));
        Assert.Equal(new[] { sentinel }, Directory.GetFileSystemEntries(targetParent));
        _output.WriteLine(JsonSerializer.Serialize(new { RequestedParent = requested, TargetParent = targetParent,
            Refused = true, SentinelUnchanged = true, CandidateAllocated = false }));
    }

    /// <summary>
    /// Builds the exact existing eligible package selection for a cut.
    /// </summary>
    /// <param name="cut">
    /// The cut index, including deliberate invalid values in refusal controls.
    /// </param>
    /// <returns>
    /// An ordinary Windows extended package request with no incompatible opt-ins.
    /// </returns>
    private static JointInterfaceCrashStorageSelection Eligible(int cut) => new(true, true, true, false, false, false, new(cut, false, null));

    /// <summary>
    /// Calls the typed placement seam with this fixture as the explicit parent.
    /// </summary>
    /// <param name="fixture">
    /// The independent owned parent directory.
    /// </param>
    /// <param name="nonce">
    /// The requested child suffix.
    /// </param>
    /// <param name="selection">
    /// The scenario switches to validate.
    /// </param>
    /// <returns>
    /// The selected unallocated child name.
    /// </returns>
    private static string Select(StorageFixture fixture, string nonce, JointInterfaceCrashStorageSelection selection) =>
        JointInterfaceCrashStorage.SelectOwnedRoot(Path.GetTempPath(), fixture.Root, nonce, selection);

    /// <summary>
    /// Owns one small marked parent and reports complete non-following cleanup evidence.
    /// </summary>
    private sealed class StorageFixture : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly string _parent;
        private readonly List<string> _junctions = [];
        private bool _unsettledHelper;

        /// <summary>
        /// Gets the exact newly created root.
        /// </summary>
        internal string Root { get; }

        /// <summary>
        /// Gets this root's unchanged ownership sentinel.
        /// </summary>
        internal string Marker => Path.Combine(Root, ".fixture-owner");

        /// <summary>
        /// Creates a fresh small parent beneath this checkout's dedicated test results.
        /// </summary>
        /// <param name="output">
        /// The current test's full diagnostic receiver.
        /// </param>
        internal StorageFixture(ITestOutputHelper output)
        {
            _output = output;
            _parent = Path.Combine(TestRepoPaths.RepoRoot, "TestResults", "crash-storage-placement");
            Root = Path.Combine(_parent, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
            File.WriteAllText(Marker, "untouched");
        }

        /// <summary>
        /// Creates one junction between strict descendants of this fixture using the installed Windows builtin.
        /// </summary>
        /// <param name="path">
        /// The fresh owned link name, removed literally before ordinary fixture cleanup.
        /// </param>
        /// <param name="target">
        /// The existing ordinary owned target. Neither name may contain a quote or percent expansion character.
        /// </param>
        internal void CreateJunction(string path, string target)
        {
            foreach (var name in new[] { path, target })
            {
                Assert.True(TrustedLocalFileScope.IsWithinDirectory(name, Root, Path.DirectorySeparatorChar));
                Assert.DoesNotContain('"', name);
                Assert.DoesNotContain('%', name);
            }
            Assert.False(Path.Exists(path));
            _junctions.Add(path);
            using var process = Process.Start(new ProcessStartInfo(
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe"),
                $"/d /v:off /c mklink /J \"{path}\" \"{target}\"")
            {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true
            }) ?? throw new InvalidOperationException("The owned junction builtin did not start.");
            _unsettledHelper = true;
            using var stdout = new MemoryStream();
            using var stderr = new MemoryStream();
            var stdoutDrain = process.StandardOutput.BaseStream.CopyToAsync(stdout);
            var stderrDrain = process.StandardError.BaseStream.CopyToAsync(stderr);
            try
            {
                if (!process.WaitForExit(10000))
                    throw new TimeoutException("The owned junction builtin did not exit within ten seconds.");
            }
            finally
            {
                if (!process.HasExited) process.Kill();
                if (!process.WaitForExit(10000))
                    throw new TimeoutException("The original junction process retirement remains unconfirmed.");
                _unsettledHelper = false;
                Task.WhenAll(stdoutDrain, stderrDrain).GetAwaiter().GetResult();
                _output.WriteLine(JsonSerializer.Serialize(new { Junction = path, Target = target, process.ExitCode,
                    OriginalProcessJoined = true, StdoutBase64 = Convert.ToBase64String(stdout.ToArray()),
                    StderrBase64 = Convert.ToBase64String(stderr.ToArray()) }));
            }
            Assert.Equal(0, process.ExitCode);
        }

        /// <summary>
        /// Checks the exact owned root before non-following cleanup and emits its final absence.
        /// </summary>
        public void Dispose()
        {
            Assert.Equal(_parent, Path.GetDirectoryName(Root));
            Assert.Equal("untouched", File.ReadAllText(Marker));
            if (_unsettledHelper)
            {
                _output.WriteLine(JsonSerializer.Serialize(new { Root, Closing = false, RetainedForUnsettledJunctionProcess = true }));
                throw new InvalidOperationException("The owned fixture is retained until the original junction helper retires.");
            }
            foreach (var junction in _junctions)
            {
                Assert.True(TrustedLocalFileScope.IsWithinDirectory(junction, Root, Path.DirectorySeparatorChar));
                if (!Path.Exists(junction)) continue;
                Assert.Equal(FileAttributes.ReparsePoint, File.GetAttributes(junction) & FileAttributes.ReparsePoint);
                Directory.Delete(junction, recursive: false);
                Assert.False(Path.Exists(junction));
            }
            new TrustedLocalFileScope([_parent]).DeleteOwnedTree(Root);
            var closed = !Path.Exists(Root);
            _output.WriteLine(JsonSerializer.Serialize(new { Root, Closing = closed }));
            Assert.True(closed);
        }
    }
}
