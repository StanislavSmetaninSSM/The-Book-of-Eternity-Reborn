using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class PortableSessionGenerationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "boe-generation-read-" + Guid.NewGuid().ToString("N"));
    private readonly FileSystemManager _files;
    private readonly string _generation = Guid.NewGuid().ToString("N");

    public PortableSessionGenerationTests()
    {
        Directory.CreateDirectory(_root);
        _files = new FileSystemManager(_root, NullLogger<FileSystemManager>.Instance);
    }

    private byte[] Seed(string? json = null, Encoding? encoding = null)
    {
        encoding ??= new UTF8Encoding(false);
        var bytes = encoding.GetPreamble().Concat(encoding.GetBytes(json ??
            " { \"schemaVersion\" : 1, \"generationId\" : \"" + _generation + "\", \"extension\" : true }\n")).ToArray();
        Directory.CreateDirectory(Path.GetDirectoryName(_files.SessionGenerationPath)!);
        File.WriteAllBytes(_files.SessionGenerationPath, bytes);
        return bytes;
    }

    [Fact]
    public async Task Snapshot_AbsenceIsExplicitAndDoesNotCreateGeneration()
    {
        await using var lease = await _files.AcquireCanonicalWriteLeaseAsync();
        var value = _files.ReadLocalGenerationSnapshot(lease);
        Assert.Equal(TrustedLocalGeneration.Absent, value.Binding);
        Assert.Null(value.Bytes);
        Assert.False(File.Exists(_files.SessionGenerationPath));
    }

    [Theory]
    [InlineData("utf8")]
    [InlineData("utf16-le")]
    [InlineData("utf16-be")]
    [InlineData("utf32-le")]
    [InlineData("utf32-be")]
    public async Task Snapshot_PreservesExactBomWhitespaceAndCurrentSchemaExtension(string name)
    {
        Encoding encoding = name switch
        {
            "utf8" => new UTF8Encoding(true),
            "utf16-le" => new UnicodeEncoding(false, true),
            "utf16-be" => new UnicodeEncoding(true, true),
            "utf32-le" => new UTF32Encoding(false, true),
            _ => new UTF32Encoding(true, true)
        };
        var before = Seed(encoding: encoding);
        await using var lease = await _files.AcquireCanonicalWriteLeaseAsync();
        var value = _files.ReadLocalGenerationSnapshot(lease);
        Assert.Equal(TrustedLocalGeneration.Existing(_generation), value.Binding);
        Assert.Equal(before, value.Bytes);
        Assert.Equal(before, File.ReadAllBytes(_files.SessionGenerationPath));
    }

    [Theory]
    [InlineData("")]
    [InlineData("{broken")]
    [InlineData("null")]
    [InlineData("{\"schemaVersion\":2,\"generationId\":\"0123456789abcdef0123456789abcdef\"}")]
    [InlineData("{\"schemaVersion\":1,\"generationId\":\"01234567-89ab-cdef-0123-456789abcdef\"}")]
    [InlineData("{\"schemaVersion\":1,\"generationId\":\"0123456789abcdef0123456789abcdef\",\"GenerationId\":\"0123456789abcdef0123456789abcdef\"}")]
    public async Task Snapshot_InvalidAuthorityRemainsAnErrorAndPreservesBytes(string json)
    {
        var before = Seed(json);
        await using var lease = await _files.AcquireCanonicalWriteLeaseAsync();
        Assert.Throws<InvalidDataException>(() => _files.ReadLocalGenerationSnapshot(lease));
        Assert.Equal(before, File.ReadAllBytes(_files.SessionGenerationPath));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Snapshot_RejectsForeignOrDisposedLeaseBeforeReading(bool disposed)
    {
        var before = Seed();
        Directory.CreateDirectory(Path.Combine(_root, "other"));
        var owner = disposed ? _files : new FileSystemManager(Path.Combine(_root, "other"), NullLogger<FileSystemManager>.Instance);
        await using var lease = await owner.AcquireCanonicalWriteLeaseAsync();
        if (disposed) await lease.DisposeAsync();
        Assert.Throws<InvalidOperationException>(() => _files.ReadLocalGenerationSnapshot(lease));
        Assert.Equal(before, File.ReadAllBytes(_files.SessionGenerationPath));
    }

    [Theory]
    [InlineData("directory")]
    [InlineData("leaf-link")]
    [InlineData("ancestor-link")]
    public async Task Snapshot_RejectsWrongTypeAndLinksWithoutFollowingThem(string kind)
    {
        var outside = Path.Combine(_root, "outside"); Directory.CreateDirectory(outside);
        var outsideFile = Path.Combine(outside, "current.json"); File.WriteAllBytes(outsideFile, [0xFE, 0]);
        var parent = Path.GetDirectoryName(_files.SessionGenerationPath)!;
        Directory.CreateDirectory(Path.GetDirectoryName(parent)!);
        if (kind == "ancestor-link") Directory.CreateSymbolicLink(parent, outside);
        else
        {
            Directory.CreateDirectory(parent);
            if (kind == "directory") Directory.CreateDirectory(_files.SessionGenerationPath);
            else File.CreateSymbolicLink(_files.SessionGenerationPath, outsideFile);
        }
        await using var lease = await _files.AcquireCanonicalWriteLeaseAsync();
        Assert.Throws<InvalidDataException>(() => _files.ReadLocalGenerationSnapshot(lease));
        Assert.Equal(new byte[] { 0xFE, 0 }, File.ReadAllBytes(outsideFile));
        Assert.True(Directory.Exists(parent));
    }

    [Fact]
    public async Task Snapshot_ExistingReadersAndBoundWriteAcceptHardLinkedGeneration()
    {
        var before = Seed();
        var outside = Path.Combine(_root, "outside-generation.json");
        File.Move(_files.SessionGenerationPath, outside);
        if (OperatingSystem.IsWindows()) Assert.True(CreateHardLink(_files.SessionGenerationPath, outside, IntPtr.Zero));
        else Assert.Equal(0, Link(outside, _files.SessionGenerationPath));
        _files.EnsureDirectoryStructure();
        await using (var lease = await _files.AcquireCanonicalWriteLeaseAsync())
        {
            Assert.Equal(before, _files.ReadLocalGenerationSnapshot(lease).Bytes);
            Assert.Equal(_generation, _files.ReadExistingSessionGeneration(lease));
            Assert.Equal(_generation, _files.GetOrCreateSessionGeneration(lease));
            Assert.True(_files.IsCurrentSessionGeneration(lease, _generation));
        }
        await SessionOperationContext.RunBoundAsync(_files, _generation,
            () => _files.WriteFileAtomicBytesAsync("game_state/core/read-proof.json", [1]));
        Assert.Equal(new byte[] { 1 }, File.ReadAllBytes(_files.ResolvePath("game_state/core/read-proof.json")));
        Assert.Equal(before, File.ReadAllBytes(outside));
        Assert.Equal(before, File.ReadAllBytes(_files.SessionGenerationPath));
    }

    [Theory]
    [InlineData("snapshot")]
    [InlineData("existing")]
    [InlineData("current")]
    [InlineData("get-or-create")]
    [InlineData("bound-write")]
    public async Task GenerationFifoIsRejectedBeforeOpenBySnapshotAndWiredConsumers(string operation)
    {
        if (!OperatingSystem.IsLinux()) return;
        _files.EnsureDirectoryStructure();
        Directory.CreateDirectory(Path.GetDirectoryName(_files.SessionGenerationPath)!);
        Assert.Equal(0, MakeFifo(_files.SessionGenerationPath, Convert.ToUInt32("600", 8)));
        await RunChild(operation, "reject");
        Assert.True(File.Exists(_files.SessionGenerationPath));
        Assert.False(File.Exists(_files.ResolvePath("game_state/core/read-proof.json")));
    }

    [Fact]
    public async Task BoundGenerationReadDoesNotReenterItsOwnFenceInColdProcess()
    {
        var before = Seed(); _files.EnsureDirectoryStructure();
        await RunChild("bound-write", "accept");
        Assert.Equal(before, File.ReadAllBytes(_files.SessionGenerationPath));
        Assert.Equal(new byte[] { 1 }, File.ReadAllBytes(_files.ResolvePath("game_state/core/read-proof.json")));
    }

    private async Task RunChild(string operation, string expected)
    {
        var assembly = typeof(PortableSessionGenerationTests).Assembly.Location;
        var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
        { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in new[] { "exec", "--runtimeconfig", Path.ChangeExtension(assembly, ".runtimeconfig.json"),
                     "--depsfile", Path.ChangeExtension(assembly, ".deps.json"), typeof(PortableStorageCrashHost.Program).Assembly.Location,
                     _root, _generation, "generation-read", operation + ":" + expected }) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Generation probe did not start.");
        var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5)); var timedOut = false;
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException) { timedOut = true; }
        finally { if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); } }
        var output = await stdout + await stderr;
        Assert.False(timedOut, "Generation read hung instead of reaching the declared result: " + operation);
        Assert.True(process.ExitCode == 0, output);
    }

    [DllImport("libc", EntryPoint = "mkfifo", SetLastError = true)] private static extern int MakeFifo(string path, uint mode);
    [DllImport("libc", EntryPoint = "link", SetLastError = true)] private static extern int Link(string existing, string created);
    [DllImport("kernel32.dll", EntryPoint = "CreateHardLinkW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CreateHardLink(string path, string existing, IntPtr security);
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
