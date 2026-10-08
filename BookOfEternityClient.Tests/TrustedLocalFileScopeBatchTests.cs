using System.Diagnostics;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using BookOfEternityClient.Core;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Exercises fresh read-only batch admission without widening exact local-file grants.
/// </summary>
public sealed class TrustedLocalFileScopeBatchTests : IDisposable
{
    private readonly string _id = Guid.NewGuid().ToString("N");
    private readonly string _sandbox;
    private readonly string _root;
    private readonly string _outside;
    private readonly List<string> _links = [];

    /// <summary>
    /// Creates one owned GUID sandbox with separate grant and outside directories.
    /// </summary>
    public TrustedLocalFileScopeBatchTests()
    {
        _sandbox = Path.Combine(Path.GetTempPath(), "boe-scope-batch-" + _id);
        _root = Path.Combine(_sandbox, "root");
        _outside = Path.Combine(_sandbox, "outside");
        Directory.CreateDirectory(_root);
        Directory.CreateDirectory(_outside);
    }

    /// <summary>
    /// Refuses sibling, parent-directory and differently spelled names outside an exact file grant.
    /// </summary>
    [Fact]
    public void ValidateFiles_ExactGrantDoesNotAdmitSiblingDirectoryOrCaseAlias()
    {
        var granted = Path.Combine(_outside, "allowed.json");
        File.WriteAllText(granted, "outside preserved");
        var scope = new TrustedLocalFileScope([], [granted]);
        Assert.Equal(new[] { granted, granted }, scope.ValidateFiles([granted, granted], allowMissing: false));
        foreach (var refused in new[] { Path.Combine(_outside, "other.json"), _outside, Path.Combine(_outside, "ALLOWED.json") })
            Assert.Throws<InvalidDataException>(() => scope.ValidateFiles([granted, refused]));
        Assert.Equal("outside preserved", File.ReadAllText(granted));
        Assert.False(File.Exists(Path.Combine(_outside, "other.json")));
    }

    /// <summary>
    /// Consumes and checks the whole request's grants before any file-kind admission is attempted.
    /// </summary>
    [Fact]
    public void ValidateFiles_MaterializesRequestBeforePhysicalAdmission()
    {
        var granted = Path.Combine(_root, "earlier.json");
        File.WriteAllText(granted, "earlier preserved");
        var enumerated = false;
        IEnumerable<string> Request()
        {
            yield return Path.Combine(_root, "missing.json");
            enumerated = true;
            throw new InvalidOperationException("owned request failed during enumeration");
        }
        var scope = new TrustedLocalFileScope([_root]);
        Assert.Throws<InvalidOperationException>(() => scope.ValidateFiles(Request(), allowMissing: false));
        Assert.True(enumerated);
        Assert.Equal("earlier preserved", File.ReadAllText(granted));
    }

    /// <summary>
    /// Normalizes and checks a late unauthorized name before probing an earlier required missing leaf.
    /// </summary>
    [Fact]
    public void ValidateFiles_ChecksAllGrantsBeforeRequiredLeafAdmission()
    {
        var absent = Path.Combine(_root, "missing.json");
        var outside = Path.Combine(_outside, "sentinel.json");
        File.WriteAllText(outside, "outside preserved");
        var scope = new TrustedLocalFileScope([_root]);
        // Scalar-first admission would throw FileNotFoundException and never reach the late grant check.
        Assert.Throws<InvalidDataException>(() => scope.ValidateFiles([absent, outside], allowMissing: false));
        Assert.False(File.Exists(absent));
        Assert.Equal("outside preserved", File.ReadAllText(outside));
    }

    /// <summary>
    /// Accepts optional missing ordinary names without creating their parent or leaf.
    /// </summary>
    [Fact]
    public void ValidateFiles_OptionalAbsenceDoesNotCreateDirectoriesOrFiles()
    {
        var parent = Path.Combine(_root, "not-created");
        var first = Path.Combine(parent, "first.json");
        var second = Path.Combine(parent, "second.json");
        Assert.Equal(new[] { first, second }, new TrustedLocalFileScope([_root]).ValidateFiles([first, second]));
        Assert.False(Directory.Exists(parent));
        Assert.False(File.Exists(first));
        Assert.False(File.Exists(second));
    }

    /// <summary>
    /// Refuses a late required missing file without modifying an earlier admitted file.
    /// </summary>
    [Fact]
    public void ValidateFiles_RequiredLateAbsenceRetainsEarlierBytes()
    {
        var first = Path.Combine(_root, "first.json");
        var missing = Path.Combine(_root, "missing.json");
        File.WriteAllBytes(first, [0, 255, 17, 3]);
        var failure = Assert.Throws<FileNotFoundException>(() =>
            new TrustedLocalFileScope([], [first, missing]).ValidateFiles([first, missing], allowMissing: false));
        Assert.Equal(missing, failure.FileName);
        Assert.Equal(new byte[] { 0, 255, 17, 3 }, File.ReadAllBytes(first));
        Assert.False(File.Exists(missing));
    }

    /// <summary>
    /// Refuses a late directory leaf even when absence is allowed and retains earlier bytes.
    /// </summary>
    [Fact]
    public void ValidateFiles_LateDirectoryLeafRetainsEarlierBytes()
    {
        var first = Path.Combine(_root, "first.json");
        var directory = Path.Combine(_root, "directory");
        File.WriteAllText(first, "earlier preserved");
        Directory.CreateDirectory(directory);
        Assert.Throws<InvalidDataException>(() =>
            new TrustedLocalFileScope([_root]).ValidateFiles([first, directory]));
        Assert.Equal("earlier preserved", File.ReadAllText(first));
        Assert.Empty(Directory.EnumerateFileSystemEntries(directory));
    }

    /// <summary>
    /// Refuses a regular-file ancestor instead of treating its unreachable descendant as optional absence.
    /// </summary>
    [Fact]
    public void ValidateFiles_LateFileBlockerRetainsEarlierAndBlockerBytes()
    {
        var first = Path.Combine(_root, "first.json");
        var blocker = Path.Combine(_root, "blocker");
        File.WriteAllText(first, "earlier preserved");
        File.WriteAllText(blocker, "blocker preserved");
        Assert.Throws<InvalidDataException>(() =>
            new TrustedLocalFileScope([_root]).ValidateFiles([first, Path.Combine(blocker, "missing.json")]));
        Assert.Equal("earlier preserved", File.ReadAllText(first));
        Assert.Equal("blocker preserved", File.ReadAllText(blocker));
    }

    /// <summary>
    /// Revalidates parents on each batch and scalar use after an owned native link replaces the parent.
    /// </summary>
    /// <param name="replaceRoot">
    /// Replaces the grant directory when true, or a nested exact-file parent otherwise.
    /// </param>
    /// <returns>
    /// A task that completes after native link creation and refusal assertions.
    /// </returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ValidateFiles_FreshCallsRefuseParentReplacedByNativeDirectoryLink(bool replaceRoot)
    {
        var parent = replaceRoot ? _root : Path.Combine(_root, "nested");
        Directory.CreateDirectory(parent);
        var file = Path.Combine(parent, "first.json");
        File.WriteAllText(file, "before preserved");
        var scope = replaceRoot ? new TrustedLocalFileScope([_root]) : new TrustedLocalFileScope([], [file]);
        Assert.Equal(new[] { file }, scope.ValidateFiles([file], allowMissing: false));
        var original = Path.Combine(_sandbox, "original-parent");
        AssertOwnedPath(parent);
        AssertOwnedPath(original);
        Directory.Move(parent, original);
        var outsideFile = Path.Combine(_outside, "first.json");
        File.WriteAllText(outsideFile, "outside preserved");
        await CreateOwnedDirectoryLinkAsync(parent, _outside);
        Assert.Throws<InvalidDataException>(() => scope.ValidateFiles([file], allowMissing: false));
        Assert.Throws<InvalidDataException>(() => scope.ValidateFile(file));
        Assert.Throws<InvalidDataException>(() => new TrustedLocalFileScope([], [file]));
        Assert.Equal("outside preserved", File.ReadAllText(outsideFile));
        Assert.Equal("before preserved", File.ReadAllText(Path.Combine(original, "first.json")));
    }

    /// <summary>
    /// Revalidates a formerly safe exact-file parent after it becomes a regular file between batches.
    /// </summary>
    [Fact]
    public void ValidateFiles_FreshCallRefusesParentReplacedByRegularFile()
    {
        var parent = Path.Combine(_root, "parent");
        Directory.CreateDirectory(parent);
        var file = Path.Combine(parent, "first.json");
        File.WriteAllText(file, "before preserved");
        var scope = new TrustedLocalFileScope([], [file]);
        Assert.Equal(new[] { file }, scope.ValidateFiles([file], allowMissing: false));
        var original = Path.Combine(_sandbox, "original-parent");
        AssertOwnedPath(parent);
        AssertOwnedPath(original);
        Directory.Move(parent, original);
        File.WriteAllText(parent, "blocker preserved");
        Assert.Throws<InvalidDataException>(() => scope.ValidateFiles([file]));
        Assert.Throws<InvalidDataException>(() => scope.ValidateFile(file));
        Assert.Equal("blocker preserved", File.ReadAllText(parent));
        Assert.Equal("before preserved", File.ReadAllText(Path.Combine(original, "first.json")));
    }

    /// <summary>
    /// Refuses a late native nonregular leaf without opening it or modifying earlier bytes.
    /// </summary>
    [Fact]
    public void ValidateFiles_LateNonRegularLeafRetainsEarlierBytes()
    {
        Assert.True(OperatingSystem.IsWindows() || OperatingSystem.IsLinux(), "Requires actual native Windows or Linux behavior.");
        var first = Path.Combine(_root, "first.json");
        var special = Path.Combine(_root, "special.sock");
        File.WriteAllText(first, "earlier preserved");
        using var socket = OperatingSystem.IsLinux() ? null : new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        if (OperatingSystem.IsLinux()) Assert.Equal(0, CreateFifo(special, 0x180));
        else socket!.Bind(new UnixDomainSocketEndPoint(special));
        Assert.Throws<InvalidDataException>(() => new TrustedLocalFileScope([_root]).ValidateFiles([first, special]));
        Assert.Equal("earlier preserved", File.ReadAllText(first));
        Assert.True(File.Exists(special));
    }

    /// <summary>
    /// Creates an actual owned Windows junction or Linux symbolic link without an unsupported-platform pass.
    /// </summary>
    /// <param name="path">
    /// The absent link name within this sandbox.
    /// </param>
    /// <param name="target">
    /// The existing owned directory to reference.
    /// </param>
    /// <returns>
    /// A task that completes after the native helper exits and the link is observed.
    /// </returns>
    private async Task CreateOwnedDirectoryLinkAsync(string path, string target)
    {
        AssertOwnedPath(path);
        AssertOwnedPath(target);
        _links.Add(path);
        // Both native bodies are implemented; this source does not claim a completed native Linux run.
        if (OperatingSystem.IsWindows())
        {
            var start = new ProcessStartInfo("cmd.exe", $"/d /c mklink /J \"{path}\" \"{target}\"")
            { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            using var process = Process.Start(start) ?? throw new InvalidOperationException("Owned junction helper did not start.");
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            try { await process.WaitForExitAsync(deadline.Token); }
            finally
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    await process.WaitForExitAsync();
                }
            }
            Assert.True(process.ExitCode == 0, await stdout + await stderr);
        }
        else
        {
            Assert.True(OperatingSystem.IsLinux(), "Requires actual native Windows or Linux behavior.");
            Directory.CreateSymbolicLink(path, target);
        }
        Assert.True(Directory.Exists(path));
        Assert.True(File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint));
    }

    /// <summary>
    /// Checks the resolved exact GUID sandbox before any move, native-link operation or recursive cleanup.
    /// </summary>
    /// <param name="path">
    /// The absolute descendant to verify; the sandbox itself is also accepted.
    /// </param>
    private void AssertOwnedPath(string path)
    {
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(_sandbox));
        var temporary = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
        Assert.True(Guid.TryParseExact(_id, "N", out _));
        Assert.Equal("boe-scope-batch-" + _id, Path.GetFileName(root));
        Assert.Equal(temporary, Path.GetDirectoryName(root));
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        Assert.True(string.Equals(root, full, StringComparison.Ordinal) ||
            full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal));
    }

    /// <summary>
    /// Creates a native Linux FIFO used to prove read-only refusal without opening a blocking stream.
    /// </summary>
    /// <param name="path">
    /// The absent owned FIFO name.
    /// </param>
    /// <param name="mode">
    /// Native permission bits for the owned entry.
    /// </param>
    /// <returns>
    /// Zero on creation, or the native failure result.
    /// </returns>
    [DllImport("libc", EntryPoint = "mkfifo", SetLastError = true, CharSet = CharSet.Ansi)]
    private static extern int CreateFifo(string path, uint mode);

    /// <summary>
    /// Removes exact owned directory links first and then recursively deletes the verified GUID sandbox.
    /// </summary>
    public void Dispose()
    {
        AssertOwnedPath(_sandbox);
        foreach (var link in _links)
        {
            AssertOwnedPath(link);
            if (!Directory.Exists(link)) continue;
            Assert.True(File.GetAttributes(link).HasFlag(FileAttributes.ReparsePoint));
            Directory.Delete(link, recursive: false);
        }
        if (Directory.Exists(_sandbox))
        {
            Assert.False(File.GetAttributes(_sandbox).HasFlag(FileAttributes.ReparsePoint));
            Directory.Delete(_sandbox, recursive: true);
        }
    }
}
