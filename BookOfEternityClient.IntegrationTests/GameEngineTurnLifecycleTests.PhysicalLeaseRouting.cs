using System.Text;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services.GmRuntime;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    // Actual public acquisition/disposal, not a mock observer. Temporary sink is outside canonical root.
    [Theory]
    [InlineData("disabled")]
    [InlineData("unknown")]
    [InlineData("stderr")]
    [InlineData("stderr-with-path")]
    [InlineData("external")]
    [InlineData("missing-path")]
    [InlineData("relative-path")]
    [InlineData("inside-root")]
    [InlineData("ancestor-root")]
    [InlineData("missing-directory")]
    [InlineData("noncanonical-path")]
    [InlineData("missing-marker")]
    [InlineData("wrong-marker")]
    [InlineData("long-marker")]
    [InlineData("linked-root")]
    [InlineData("linked-marker")]
    [InlineData("public-directory")]
    [InlineData("public-marker")]
    [InlineData("changed-route")]
    [InlineData("changed-path")]
    public async Task PhysicalLeaseRoutingIsExplicitPrivateAndDoesNotPolluteStderr(string mode)
    {
        if (!OperatingSystem.IsLinux()) return; // Category explicitly Linux; Unix privacy is the contract here.
        const string routeKey = "BOE_TEST_CANONICAL_LEASE_TRACE_ROUTE", pathKey = "BOE_TEST_CANONICAL_LEASE_TRACE_DIR";
        const string nonceKey = "BOE_MAIN_STOP_DIAGNOSTIC_NONCE";
        var previousRoute = Environment.GetEnvironmentVariable(routeKey);
        var previousPath = Environment.GetEnvironmentVariable(pathKey);
        var previousNonce = Environment.GetEnvironmentVariable(nonceKey);
        var previousError = Console.Error;
        var nonce = Guid.NewGuid().ToString("N");
        var external = Path.Combine(Path.GetTempPath(), "boe-physical-route-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(external, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var sink = Path.Combine(external, "trace");
        Directory.CreateDirectory(sink, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var marker = Path.Combine(sink, "owner-nonce");
        File.WriteAllText(marker, nonce, new UTF8Encoding(false));
        File.SetUnixFileMode(marker, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        var path = sink;
        var route = mode == "disabled" ? null : mode == "unknown" ? "EXTERNAL" : mode.StartsWith("stderr", StringComparison.Ordinal) ? "stderr" : "external";
        if (mode == "stderr" || mode == "missing-path") path = null;
        if (mode == "relative-path") path = "relative-trace";
        if (mode == "inside-root") path = _rootPath;
        if (mode == "ancestor-root") path = Path.GetDirectoryName(_rootPath)!;
        if (mode == "missing-directory") path = Path.Combine(external, "absent");
        if (mode == "noncanonical-path") path = sink + "/../trace";
        if (mode == "missing-marker") File.Delete(marker);
        if (mode == "wrong-marker") File.WriteAllText(marker, Guid.NewGuid().ToString("N"));
        if (mode == "long-marker") File.AppendAllText(marker, "x");
        if (mode == "linked-root") { path = Path.Combine(external, "link"); Directory.CreateSymbolicLink(path, sink); }
        if (mode == "linked-marker") { File.Move(marker, Path.Combine(external, "real-marker")); File.CreateSymbolicLink(marker, Path.Combine(external, "real-marker")); }
        if (mode == "public-directory") File.SetUnixFileMode(sink, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.OtherRead);
        if (mode == "public-marker") File.SetUnixFileMode(marker, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.OtherRead);
        var originalMarker = File.Exists(marker) ? File.ReadAllBytes(marker) : null;
        using var stderr = new StringWriter();
        var files = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        FileSystemManager.CanonicalWriteLease? lease = null;
        try
        {
            Environment.SetEnvironmentVariable(routeKey, route);
            Environment.SetEnvironmentVariable(pathKey, path);
            Environment.SetEnvironmentVariable(nonceKey, nonce);
            Console.SetError(TextWriter.Synchronized(stderr));
            lease = await files.AcquireCanonicalWriteLeaseAsync();
            Assert.True(lease.IsActive);
            if (mode == "changed-route") Environment.SetEnvironmentVariable(routeKey, "stderr");
            if (mode == "changed-path") Environment.SetEnvironmentVariable(pathKey, external);
            await lease.DisposeAsync();
            Assert.False(lease.IsActive);
            await lease.DisposeAsync();
            // Existing failure channel remains stderr even when physical routing is disabled/refused.
            MainOperationFailureDiagnostic.Report(nonce, "route-test", null, new IOException("original failure sentinel"));
            Assert.Contains("BOE_MAIN_OPERATION_FAILURE ", stderr.ToString());
            var stderrRows = ReadPhysicalLeaseEvents(stderr.ToString());
            var eventFiles = Directory.GetFiles(sink).Where(x => Path.GetFileName(x) != "owner-nonce").ToArray();
            if (mode == "stderr")
            {
                Assert.Equal(3, stderrRows.Length);
                Assert.Empty(eventFiles);
            }
            else
            {
                Assert.Empty(stderrRows); // Missing default-disable/external routing is the causal RED, not setup.
                var count = mode == "external" ? 3 : mode is "changed-route" or "changed-path" ? 2 : 0;
                Assert.Equal(count, eventFiles.Length);
                var rows = eventFiles.SelectMany(x => ReadPhysicalLeaseEvents(File.ReadAllText(x)))
                    .OrderBy(x => x.GetProperty("CaptureMonotonicTimestamp").GetInt64()).ToArray();
                Assert.Equal(count, rows.Length);
                Assert.All(rows, row => { Assert.Equal(nonce, row.GetProperty("Nonce").GetString()); Assert.Equal(Environment.ProcessId, row.GetProperty("ProcessId").GetInt32()); Assert.Equal(_rootPath, row.GetProperty("Root").GetString()); });
                if (count > 0)
                {
                    Assert.Single(rows.Select(x => x.GetProperty("Occurrence").GetInt64()).Distinct());
                    Assert.Equal("physical-opened", PhysicalStage(rows[0]));
                    Assert.Equal("lease-granted", PhysicalStage(rows[1]));
                    if (count == 3) Assert.Equal("physical-dispose-completed", PhysicalStage(rows[2]));
                }
                foreach (var file in eventFiles)
                {
                    Assert.Null(new FileInfo(file).LinkTarget);
                    Assert.Equal((UnixFileMode)0, File.GetUnixFileMode(file) & (UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute));
                    Assert.EndsWith("\n", File.ReadAllText(file));
                }
            }
            if (originalMarker != null) Assert.Equal(originalMarker, File.ReadAllBytes(marker));
            if (mode == "missing-directory") Assert.False(Directory.Exists(path));
        }
        finally
        {
            try { if (lease?.IsActive == true) await lease.DisposeAsync(); }
            finally
            {
                Console.SetError(previousError);
                Environment.SetEnvironmentVariable(routeKey, previousRoute);
                Environment.SetEnvironmentVariable(pathKey, previousPath);
                Environment.SetEnvironmentVariable(nonceKey, previousNonce);
                Directory.Delete(external, true); // Only test-owned DATA; production may never repair/delete sink files.
            }
        }
        await using var fresh = await files.AcquireCanonicalWriteLeaseAsync();
        Assert.True(fresh.IsActive);
    }
}
