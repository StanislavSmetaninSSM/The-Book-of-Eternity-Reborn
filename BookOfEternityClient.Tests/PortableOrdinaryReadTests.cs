using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class PortableOrdinaryReadTests : IDisposable
{
    private const string Member = "game_state/core/ordinary-read.bin";
    private readonly string _root = Path.Combine(Path.GetTempPath(), "boe-ordinary-read-" + Guid.NewGuid().ToString("N"));
    private readonly FileSystemManager _files;

    public PortableOrdinaryReadTests()
    {
        _files = Create();
        _files.EnsureDirectoryStructure();
    }

    private FileSystemManager Create(FileSystemManagerHooks? hooks = null) =>
        new(_root, NullLogger<FileSystemManager>.Instance, PhysicalLoadTransactionOperations.Instance, hooks);

    [Theory]
    [InlineData("absent")]
    [InlineData("empty")]
    [InlineData("binary")]
    [InlineData("utf8-bom")]
    [InlineData("utf16-bom")]
    public async Task OrdinarySurfacesPreserveExactBytesAbsenceAndBomDecoding(string value)
    {
        byte[]? bytes = value switch
        {
            "absent" => null,
            "empty" => [],
            "binary" => [0xFF, 0, 0x80, 0x41],
            "utf8-bom" => Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes("  Привет\n")).ToArray(),
            _ => Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes("  Привет\n")).ToArray()
        };
        if (bytes != null) File.WriteAllBytes(_files.ResolvePath(Member), bytes);
        await AssertOrdinarySurfaces(bytes);
    }

    private async Task AssertOrdinarySurfaces(byte[]? bytes)
    {
        string? text = null;
        if (bytes != null)
        {
            using var reader = new StreamReader(new MemoryStream(bytes), Encoding.UTF8, true);
            text = reader.ReadToEnd();
        }
        Assert.Equal(bytes != null, _files.FileExists(Member));
        Assert.Equal(bytes, await _files.ReadFileBytesAsync(Member));
        Assert.Equal(text, await _files.ReadFileAsync(Member));
        Assert.Equal(bytes, _files.ReadFileBytesSync(Member));
        Assert.Equal(text, _files.ReadFileSync(Member));
        await using var lease = await _files.AcquireCanonicalWriteLeaseAsync();
        Assert.Equal(bytes != null, _files.FileExists(lease, Member));
        Assert.Equal(bytes, await _files.ReadFileBytesAsync(lease, Member));
        Assert.Equal(text, await _files.ReadFileAsync(lease, Member));
        Assert.Equal(text, _files.ReadFileSync(lease, Member));
        Assert.Equal(bytes, (await _files.ReadFileSnapshotAsync(lease, Member))?.Content);
    }

    [Fact]
    public async Task HardLinkedOrdinaryInputReachesCoordinatedPublicationWithoutChangingOutsideAlias()
    {
        var before = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes("{\"value\":1}")).ToArray();
        var outside = Path.Combine(_root, "outside.bin");
        File.WriteAllBytes(outside, before);
        MakeHardLink(_files.ResolvePath(Member), outside);
        await AssertOrdinarySurfaces(before);
        Assert.True(await CoordinatedStateWriteHelper.TryCommitAsync(_files,
            new CoordinatedStateWriteHelper.PlannedWrite(Member, "{\"value\":1}", "{\"value\":2}", true)));
        Assert.Equal("{\"value\":2}", await _files.ReadFileAsync(Member));
        Assert.Equal(before, File.ReadAllBytes(outside));
    }

    [Fact]
    public async Task SnapshotTimestampAndBytesBelongToTheOpenedFile()
    {
        var path = _files.ResolvePath(Member);
        File.WriteAllBytes(path, [1, 2]);
        File.SetLastWriteTimeUtc(path, new DateTime(2001, 2, 3, 4, 5, 6, DateTimeKind.Utc));
        var expectedTimestamp = File.GetLastWriteTimeUtc(path);
        var reads = 0;
        var files = Create(new FileSystemManagerHooks
        {
            AfterCanonicalReadInitialValidationAsync = relative =>
            {
                if (relative == Member)
                {
                    reads++;
                    File.Move(path, path + ".opened");
                    File.WriteAllBytes(path, [9]);
                    File.SetLastWriteTimeUtc(path, new DateTime(2020, 2, 3, 4, 5, 6, DateTimeKind.Utc));
                }
                return Task.CompletedTask;
            }
        });
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        var snapshot = await files.ReadFileSnapshotAsync(lease, Member);
        Assert.NotNull(snapshot);
        Assert.Equal(new byte[] { 1, 2 }, snapshot.Content);
        Assert.Equal(expectedTimestamp, snapshot.LastWriteTimeUtc);
        Assert.Equal(1, reads);
        Assert.Equal(new byte[] { 9 }, File.ReadAllBytes(path));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LeasedReadersRejectForeignOrDisposedOwnerBeforeOpening(bool disposed)
    {
        File.WriteAllBytes(_files.ResolvePath(Member), [1]);
        var opens = 0;
        var files = Create(new FileSystemManagerHooks { BeforeCanonicalReadOpenAsync = _ => { opens++; return Task.CompletedTask; } });
        var owner = disposed ? files : _files;
        await using var lease = await owner.AcquireCanonicalWriteLeaseAsync();
        if (disposed) await lease.DisposeAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => files.ReadFileAsync(lease, Member));
        await Assert.ThrowsAsync<InvalidOperationException>(() => files.ReadFileBytesAsync(lease, Member));
        await Assert.ThrowsAsync<InvalidOperationException>(() => files.ReadFileSnapshotAsync(lease, Member));
        Assert.Throws<InvalidOperationException>(() => files.ReadFileSync(lease, Member));
        Assert.Throws<InvalidOperationException>(() => files.FileExists(lease, Member));
        Assert.Equal(0, opens);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationBeforeOrAtOpenDoesNotConsumeBytes(bool atBoundary)
    {
        File.WriteAllBytes(_files.ResolvePath(Member), [1]);
        using var cancellation = new CancellationTokenSource();
        var opens = 0; var reads = 0;
        var files = Create(new FileSystemManagerHooks
        {
            BeforeCanonicalReadOpenAsync = _ => { opens++; cancellation.Cancel(); return Task.CompletedTask; },
            AfterCanonicalReadInitialValidationAsync = _ => { reads++; return Task.CompletedTask; }
        });
        if (!atBoundary) cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => files.ReadFileBytesAsync(Member, cancellation.Token));
        Assert.Equal(atBoundary ? 1 : 0, opens);
        Assert.Equal(0, reads);
    }

    [Theory]
    [InlineData("async")]
    [InlineData("sync")]
    [InlineData("exists")]
    [InlineData("async-boundary")]
    public async Task FifoRejectedBeforeOpenInBoundedOwnedProcess(string operation)
    {
        if (!OperatingSystem.IsLinux()) return; // This body is selected for Linux only.
        var path = _files.ResolvePath(Member);
        if (operation == "async-boundary") File.WriteAllBytes(path, [1]);
        else Assert.Equal(0, MakeFifo(path, Convert.ToUInt32("600", 8)));
        var assembly = typeof(PortableOrdinaryReadTests).Assembly.Location;
        var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
        { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in new[] { "exec", "--runtimeconfig", Path.ChangeExtension(assembly, ".runtimeconfig.json"),
                     "--depsfile", Path.ChangeExtension(assembly, ".deps.json"), typeof(PortableStorageCrashHost.Program).Assembly.Location,
                     _root, "", "ordinary-read", operation }) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Reader probe did not start.");
        var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5)); var timedOut = false;
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException) { timedOut = true; }
        finally { if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); } }
        var output = await stdout + await stderr;
        Assert.False(timedOut, "Ordinary " + operation + " read opened the FIFO and blocked.");
        Assert.True(process.ExitCode == 0, output);
        Assert.True(File.Exists(path));
        Assert.False(File.Exists(_files.SessionGenerationPath));
    }

    [Fact]
    public async Task BrowserStagingBeforeRecorderAttachmentRetainsPhysicalHardLinkRejection()
    {
        if (!OperatingSystem.IsWindows()) return; // Native owner only; not a Linux pass.
        var outside = Path.Combine(_root, "legacy-outside.bin");
        File.WriteAllBytes(outside, [1]);
        MakeHardLink(_files.ResolvePath(Member), outside);
        await using var lease = await _files.AcquireCanonicalWriteLeaseAsync();
        Assert.Null(lease.MutationIntentRecorder);
        await Assert.ThrowsAsync<InvalidDataException>(() => ExplorerLocalTurnRollbackArtifacts.StageFileAsync(_files, lease, Member, "reader-boundary"));
        await Assert.ThrowsAsync<InvalidDataException>(() => ExplorerLocalTurnRollbackArtifacts.StageBrowserWriteTransactionAsync(_files, lease, [Member], "reader-boundary"));
        Assert.Null(lease.MutationIntentRecorder);
        Assert.Equal(new byte[] { 1 }, File.ReadAllBytes(outside));
        Assert.Empty(Directory.EnumerateFiles(_files.GameSessionPath, "*.rollback.*", SearchOption.AllDirectories));
    }

    private static void MakeHardLink(string created, string existing)
    {
        if (OperatingSystem.IsWindows()) Assert.True(CreateHardLink(created, existing, IntPtr.Zero));
        else Assert.Equal(0, Link(existing, created));
    }

    [DllImport("libc", EntryPoint = "mkfifo", SetLastError = true)]
    private static extern int MakeFifo(string path, uint mode);
    [DllImport("libc", EntryPoint = "link", SetLastError = true)]
    private static extern int Link(string existing, string created);
    [DllImport("kernel32.dll", EntryPoint = "CreateHardLinkW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLink(string created, string existing, IntPtr security);

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
