using System.Diagnostics;
using System.IO.Pipes;
using System.Reflection;
using System.Text;
using System.Text.Json;
using BookOfEternityClient.Services.GmRuntime;
using BookOfEternityClient.Configuration;
using Xunit;

namespace BookOfEternityClient.Tests;

// Shared host lifecycle only. The original controlled process is preseeded through
// the shared helper; constructor injection never enables Windows startup on Linux.
public sealed class GmBridgeStartupObservationTests
{
    [Theory]
    [InlineData("control")]
    [InlineData("dispose")]
    [InlineData("outer-exit")]
    public async Task OriginalProbeDebtRetainsHostUntilActualProcessAndIoSettle(string mode)
    {
        Assert.True(OperatingSystem.IsLinux());
        var root = Path.Combine(Path.GetTempPath(), "boe-host-probe-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var release = Path.Combine(root, "release");
        var session = Path.Combine(root, "game_session");
        Directory.CreateDirectory(session);
        File.WriteAllText(Path.Combine(session, "config.json"), JsonSerializer.Serialize(new {
            GmBridgeEnabled = true, GmBridgeBackend = "OwnedTerminal", GmMainOwnerBackend = "NativeLineage",
            GmCliLaunchCommand = ""
        }));
        var starts = 0;
        var probe = new WindowsStartupObservationProbe(() =>
        {
            starts++;
            var start = new ProcessStartInfo("python3") { UseShellExecute = false,
                RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
            start.ArgumentList.Add("-c");
            start.ArgumentList.Add("import os,sys,time; deadline=time.monotonic()+8\nwhile not os.path.exists(sys.argv[1]) and time.monotonic()<deadline: time.sleep(.01)");
            start.ArgumentList.Add(release);
            return start;
        }, TimeSpan.FromMilliseconds(300), TimeSpan.FromMilliseconds(20), _ => null);
        var type = LoadBridge().GetType("BookOfEternityGMBridge.BridgeHost", true)!;
        var pipe = "boe-probe-" + Guid.NewGuid().ToString("N");
        var host = Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.NonPublic,
            null, [session, pipe, probe], null)!;
        object? Field(string name) => type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(host);
        Task Call(string name) => (Task)type.GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!.Invoke(host, null)!;
        var loaded = (GameSettings)type.GetMethod("LoadBridgeConfig", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(host, null)!;
        Assert.Empty(loaded.GmCliLaunchCommand); // intentional pre-launch refusal, never an environment default
        var cts = (CancellationTokenSource)Field("_cts")!;
        Task? run = null, disposal = null;
        Task<JsonDocument>? pendingRequest = null;
        try
        {
            Assert.NotNull(await Record.ExceptionAsync(() => Call("ReadStartupObservationAsync")));
            Assert.Equal(1, starts);
            Assert.False(probe.TrySettle()); // actual retained original, not a synthetic exception
            Assert.Null(Field("_mainRun")); Assert.Null(Field("_pty"));
            if (mode == "dispose")
            {
                disposal = Task.Run(() => ((IDisposable)host).Dispose());
                var entry = Stopwatch.StartNew();
                while (!(bool)Field("_inputClosed")! && entry.Elapsed < TimeSpan.FromSeconds(2)) await Task.Delay(10);
                Assert.True((bool)Field("_inputClosed")!, "Actual Dispose must enter before its pending-state assertion.");
                Console.WriteLine($"disposeCompleted={disposal.IsCompleted};cancelled={cts.IsCancellationRequested};gateDisposed={Field("_writeGateDisposed")};starts={starts}");
                Assert.False(disposal.IsCompleted);
                Assert.False(cts.IsCancellationRequested);
                Assert.False((bool)Field("_writeGateDisposed")!);
                var restart = Call("StartShellAsync");
                var refusal = await Record.ExceptionAsync(() => restart.WaitAsync(TimeSpan.FromSeconds(1)));
                Assert.NotNull(refusal); Assert.IsNotType<TimeoutException>(refusal);
                Assert.True(restart.IsCompleted);
                Assert.False(probe.TrySettle());
            }
            else
            {
                run = Call("RunAsync");
                var statusTask = pendingRequest = Request(pipe, "status");
                await Task.WhenAny(run, statusTask);
                if (run.IsCompleted) Console.WriteLine("Run completed before control admission: " + await Record.ExceptionAsync(() => run));
                Assert.False(run.IsCompleted);
                using var status = await statusTask;
                Assert.True(status.RootElement.GetProperty("ok").GetBoolean());
                Assert.Equal("StartupObservationUnavailable", status.RootElement.GetProperty("status").GetProperty("state").GetString());
                Assert.False(status.RootElement.GetProperty("status").GetProperty("ready").GetBoolean());
                foreach (var command in new[] { "restartshell", "shutdown" })
                {
                    using var reply = await Request(pipe, command);
                    Assert.False(reply.RootElement.GetProperty("ok").GetBoolean());
                    Assert.False(reply.RootElement.TryGetProperty("shutdownAfterResponse", out var shutdown) && shutdown.GetBoolean());
                    Assert.Equal("StartupObservationUnavailable", reply.RootElement.GetProperty("status").GetProperty("state").GetString());
                }
                Assert.False(run.IsCompleted); Assert.False(cts.IsCancellationRequested);
                Assert.NotNull(await Record.ExceptionAsync(() => Call("StopShellAsync")));
                if (mode == "outer-exit")
                {
                    cts.Cancel(); // real outer loop exit, not a synthetic terminal failure
                    var closing = Stopwatch.StartNew();
                    while (!(bool)Field("_inputClosed")! && closing.Elapsed < TimeSpan.FromSeconds(2)) await Task.Delay(10);
                    Assert.True((bool)Field("_inputClosed")!);
                    Assert.False(run.IsCompleted);
                    Assert.False((bool)Field("_writeGateDisposed")!);
                    Assert.False(probe.TrySettle());
                }
            }
            Assert.Equal(1, starts);
            Assert.Null(Field("_mainRun")); Assert.Null(Field("_pty"));
            Assert.False(File.Exists(Path.Combine(session, "game_state/control/gm_bridge_status.json")));
            Assert.False(Directory.Exists(Path.Combine(root, ".boe_runtime")));
            File.WriteAllText(release, "release original controlled process");
            await Settle(probe);
            if (disposal != null) await disposal.WaitAsync(TimeSpan.FromSeconds(3));
            else
            {
                if (mode != "outer-exit")
                {
                    using var reply = await Request(pipe, "shutdown");
                    Assert.True(reply.RootElement.GetProperty("ok").GetBoolean());
                }
                Assert.Equal(0, await ((Task<int>)run!).WaitAsync(TimeSpan.FromSeconds(3)));
                ((IDisposable)host).Dispose();
            }
            Assert.Equal(1, starts);
        }
        finally
        {
            // Test owns release and retains the probe even on causal RED. No guessed PID kill.
            File.WriteAllText(release, "release in fixture cleanup");
            await Settle(probe);
            cts.Cancel();
            if (pendingRequest != null) { try { using var pending = await pendingRequest; } catch { } }
            if (run != null) { try { await run.WaitAsync(TimeSpan.FromSeconds(4)); } catch { } Assert.True(run.IsCompleted); }
            if (disposal != null) await disposal.WaitAsync(TimeSpan.FromSeconds(4));
            ((IDisposable)host).Dispose();
            Directory.Delete(root, true);
        }
    }

    private static async Task Settle(WindowsStartupObservationProbe probe)
    {
        var watch = Stopwatch.StartNew();
        while (!probe.TrySettle() && watch.Elapsed < TimeSpan.FromSeconds(10)) await Task.Delay(20);
        Assert.True(probe.TrySettle());
    }

    private static async Task<JsonDocument> Request(string pipe, string command)
    {
        using var connection = new NamedPipeClientStream(".", pipe, PipeDirection.InOut, PipeOptions.Asynchronous);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await connection.ConnectAsync(timeout.Token);
        await connection.WriteAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { command }) + "\n"), timeout.Token);
        await connection.FlushAsync(timeout.Token);
        using var reader = new StreamReader(connection, Encoding.UTF8, leaveOpen: true);
        return JsonDocument.Parse((await reader.ReadLineAsync(timeout.Token))!);
    }

    private static Assembly LoadBridge()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "BookOfEternityGMBridge/BookOfEternityGMBridge.csproj")))
                return Assembly.LoadFrom(Path.Combine(directory.FullName, "BookOfEternityGMBridge/bin",
                    new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name, "net8.0/BookOfEternityGMBridge.dll"));
        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
