using System.Text;
using System.Reflection;
using System.IO.Pipes;
using System.Text.Json;
using BookOfEternityClient.Services.GmRuntime;

namespace BookOfEternityClient.Tests;

// Each invocation is a fresh process beneath the unchanged independent guardian.
internal static class OwnedTerminalScenarioDriver
{
    internal static async Task<int> RunAsync(string mode, string package, string output)
    {
        if (mode == "terminal-bridge") return await RunBridgeAsync(package, output);
        var result = new Dictionary<string, object?>();
        IOwnedTerminalSession? session = null;
        try
        {
            session = await OwnedTerminalSessionFactory.StartNeutralAsync(package, output, CancellationToken.None);
            var text = new StringBuilder();
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(6));
            var decoder = Encoding.UTF8.GetDecoder();
            async Task Until(string marker)
            {
                var buffer = new byte[4096];
                var chars = new char[Encoding.UTF8.GetMaxCharCount(buffer.Length)];
                while (!text.ToString().Contains(marker, StringComparison.Ordinal))
                {
                    var n = await session.OutputReader.ReadAsync(buffer, deadline.Token);
                    if (n == 0) throw new EndOfStreamException("Fixture output ended before required observation.");
                    var count = decoder.GetChars(buffer, 0, n, chars, 0, false); text.Append(chars, 0, count);
                }
            }
            async Task Write(string value) => await session.InputWriter.WriteAsync(Encoding.UTF8.GetBytes(value), deadline.Token);
            await Until("TTY_READY owned=1");
            await Write("one Ж😀\n"); await Until("RESULT1:one Ж😀");
            await Write("two\n"); await Until("RESULT2:two"); result["TwoInputs"] = true;
            await session.ResizeAsync(new(93, 31), deadline.Token); await Until("RESIZE 31x93"); result["Resize"] = true;
            await Write("canonical\n"); await Until("CANONICAL_READY");
            await Write("\u0004"); await Until("CANONICAL_EOF");
            result["EofStillAlive"] = !session.RootExited.IsCompleted;
            var stop = await session.StopAndObserveAsync(deadline.Token); result["StopState"] = stop.State.ToString();
            while (await session.OutputReader.ReadAsync(new byte[1024], deadline.Token) != 0) { }
            await session.DisposeAsync(); session = null;
            result["Transcript"] = text.ToString();
            return 0;
        }
        catch (Exception ex)
        {
            result["Failure"] = ex.GetType().Name + ": " + ex.Message;
            if (session != null) try { result["Cleanup"] = await session.StopAndObserveAsync(CancellationToken.None); } catch { }
            return 1;
        }
        finally { await File.WriteAllTextAsync(Path.Combine(output, "scenario.json"), JsonSerializer.Serialize(result)); }
    }
    private static async Task<int> RunBridgeAsync(string package, string folder)
    {
        var result = new Dictionary<string, object?>(); object? host = null; Task? server = null;
        using var serverCancellation = new CancellationTokenSource();
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var repo = TestRepoPaths.RepoRoot;
        var type = Assembly.LoadFrom(Path.Combine(repo, "BookOfEternityGMBridge/bin", configuration, "net8.0/BookOfEternityGMBridge.dll"))
            .GetType("BookOfEternityGMBridge.BridgeHost", true)!;
        var pipe = "neutral-" + Guid.NewGuid().ToString("N");
        object? Invoke(string name, params object?[] args) => type.GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!.Invoke(host, args);
        async Task<JsonElement> Rpc(object request)
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            using var peer = new NamedPipeClientStream(".", pipe, PipeDirection.InOut, PipeOptions.Asynchronous);
            await peer.ConnectAsync(deadline.Token);
            await peer.WriteAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request) + "\n"), deadline.Token);
            await peer.FlushAsync(deadline.Token);
            using var reader = new StreamReader(peer, Encoding.UTF8, leaveOpen: true);
            using var doc = JsonDocument.Parse(await reader.ReadLineAsync(deadline.Token) ?? throw new EndOfStreamException());
            return doc.RootElement.Clone();
        }
        try
        {
            await File.WriteAllTextAsync(Path.Combine(folder, "config.json"), JsonSerializer.Serialize(new { GmCliInputProfile = new {
                IdleMarker="NEUTRAL READY", PromptPrefix="> ", WorkingMarker="NEUTRAL WORKING", ObservationTimeoutMilliseconds=1500 } }));
            host = Activator.CreateInstance(type, [folder, pipe]); Invoke("ConfigureNeutral", package);
            await (Task)Invoke("StartShellAsync")!;
            server = (Task)Invoke("RunServerLoopAsync", serverCancellation.Token)!;
            for (var i=0;i<100;i++) {
                var status=(await Rpc(new { command="status" })).GetProperty("status");
                if (status.GetProperty("ready").GetBoolean()) break;
                await Task.Delay(10);
            }
            var ready = await Rpc(new { command="setReady", ready=true });
            result["Ready"] = ready;
            if (!ready.GetProperty("ok").GetBoolean()) throw new InvalidOperationException("Actual session output did not produce a reliable idle view.");
            result["Binding"] = ready.GetProperty("status").GetProperty("inputBindingId").GetString();
            return 0;
        }
        catch (Exception ex) { result["Failure"] = ex.ToString(); return 1; }
        finally {
            if (host != null) {
                try { await (Task)Invoke("StopShellAsync")!; result["ScopedRetired"] = type.GetField("_pty", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(host) == null; }
                catch (Exception ex) { result["CleanupFailure"] = ex.ToString(); }
            }
            await serverCancellation.CancelAsync(); if (server != null) await server;
            if (host is IDisposable disposable) disposable.Dispose();
            await File.WriteAllTextAsync(Path.Combine(folder, "scenario.json"), JsonSerializer.Serialize(result));
        }
    }

}
