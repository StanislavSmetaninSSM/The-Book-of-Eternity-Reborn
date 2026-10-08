using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using BookOfEternityClient.Services.GmRuntime;
using BookOfEternityClient.Services.GmWorkers;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class GmRelayWindowsTests
{
    private static string Fixture => Path.Combine(TestRepoPaths.RepoRoot, "tests/fixtures/ProductionMain/relay-windows-checks.py");
    private static string Tools => Path.Combine(TestRepoPaths.RepoRoot, "tools/gm-relay");
    private static string Own()
    {
        Assert.True(OperatingSystem.IsWindows(), "Native Windows required; never report a Linux no-op as PASS.");
        var path = Path.Combine(TestRepoPaths.RepoRoot, "TestResults/relay-windows", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static async Task<string> Python(string own, params string[] args)
    {
        var start = new ProcessStartInfo("python") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add("-B");
        foreach (var arg in args) start.ArgumentList.Add(arg);
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        try
        {
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(12));
            var text = await stdout;
            var error = await stderr;
            await File.AppendAllTextAsync(Path.Combine(own, "worker.log"), text + error);
            Assert.True(process.ExitCode == 0, $"Python exit {process.ExitCode}: {error}");
            return text;
        }
        finally
        {
            if (!process.HasExited) { process.Kill(); await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(3)); }
        }
    }

    [Theory]
    [InlineData("help")]
    [InlineData("gate")]
    [InlineData("pipe")]
    public async Task NativeRuntime_HelpIndependentGateAndPipe(string mode)
    {
        var own = Own();
        await Python(own, Fixture, mode, own);
    }

    [Theory]
    [InlineData("success")]
    [InlineData("consumer-error")]
    [InlineData("close-child")]
    [InlineData("invalid-input")]
    public async Task OriginalConPty_UnicodeWorkerCloseAndRestoration(string mode)
    {
        var own = Own();
        var session = Path.Combine(own, "session");
        var queue = Path.Combine(own, "queue");
        Directory.CreateDirectory(queue);
        var turn = new { sessionId = "native-session", requestId = "native-request", turnNumber = 1 };
        void Put(string path, string text)
        {
            var full = Path.Combine(session, path);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, text, new UTF8Encoding(false));
        }
        Put("input/turn_request.json", JsonSerializer.Serialize(turn));
        Put("game_state/control/pending_turn_snapshot.json", JsonSerializer.Serialize(turn));
        Put("game_state/control/pending_turn_snapshot.authority.json", "{}");
        Put("game_state/meta/soul_state.json", "{\"currentRealm\":\"Chaos Sea\"}");
        var helper = Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient/Launcher/GM_Turn_Helper.ps1");
        var barrier = mode == "close-child"
            ? $"[IO.File]::WriteAllText('{own.Replace("'", "''")}/consumer-entered','entered')\n$deadline=[DateTime]::UtcNow.AddSeconds(8)\nwhile(-not [IO.File]::Exists('{own.Replace("'", "''")}/release-consumer')) {{ if([DateTime]::UtcNow -gt $deadline) {{ throw 'Test barrier deadline' }}; Start-Sleep -Milliseconds 20 }}\n"
            : "";
        // Actual helper, not fake Write/Complete functions. The optional fixture
        // barrier delays bootstrap and leaves all consumer semantics unchanged.
        Put("game_state/control/gm_turn_helper.bootstrap.ps1", barrier + $". '{helper.Replace("'", "''")}'\nInitialize-BoeGmTurnHelper -GameSessionPath '{session.Replace("'", "''")}'\n");
        var python = (await Python(own, "-c", "import sys; print(sys.executable)")).Trim();
        var launch = Path.Combine(own, "launch.ps1");
        await File.WriteAllTextAsync(launch, $"& '{python.Replace("'", "''")}' -B '{Fixture.Replace("'", "''")}' console '{own.Replace("'", "''")}'\nexit $LASTEXITCODE\n");
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var assembly = Assembly.LoadFrom(Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityGMBridge/bin", configuration, "net8.0/BookOfEternityGMBridge.dll"));
        var type = assembly.GetType("BookOfEternityGMBridge.ConPtySession", true)!;
        IOwnedTerminalSession? terminal = null;
        var output = new MemoryStream();
        Task? pump = null;
        try
        {
            terminal = (IOwnedTerminalSession)type.GetMethod("Start", BindingFlags.Public | BindingFlags.Static)!.Invoke(null,
                ["pwsh.exe", $"-NoLogo -NoProfile -File \"{launch}\"", own, (short)120, (short)40])!;
            var retained = terminal;
            pump = Task.Run(async () =>
            {
                var buffer = new byte[8192];
                while (true)
                {
                    var count = await retained.OutputReader.ReadAsync(buffer);
                    if (count == 0) return;
                    lock (output)
                    {
                        if (output.Length + count > 1048576) throw new IOException("Test output bound exceeded");
                        output.Write(buffer, 0, count);
                    }
                }
            });
            string View() { lock (output) return Encoding.UTF8.GetString(output.ToArray()); }
            async Task Until(Func<bool> predicate, string label)
            {
                var timer = Stopwatch.StartNew();
                while (!predicate())
                {
                    Assert.False(retained.RootExited.IsCompleted, $"Root exited before {label}: {View()}");
                    Assert.True(timer.Elapsed < TimeSpan.FromSeconds(10), $"Timed out: {label}: {View()}");
                    await Task.Delay(20);
                }
            }
            async Task Send(string text) { await retained.InputWriter.WriteAsync(Encoding.UTF8.GetBytes(text)); await retained.InputWriter.FlushAsync(); }
            await Until(() => View().Contains("NEUTRAL READY"), "initial relay presentation");
            if (mode == "invalid-input")
            {
                await Send("x");
                await Until(() => File.Exists(Path.Combine(queue, "failure.json")), "unsupported input rejected");
                Assert.Empty(Directory.GetDirectories(queue, "request-*"));
                Assert.False(File.Exists(Path.Combine(queue, "closed.json")));
            }
            else
            {
                var prompt = "controlled Ж🙂\nsecond line > exact";
                await Send("\u001b[200~" + prompt + "\u001b[201~");
                await Until(() => View().Contains("second line > exact"), "multiline draft view");
                Assert.Empty(Directory.GetDirectories(queue, "request-*"));
                await Send("\r");
                await Until(() => Directory.GetFiles(queue, "game-request.json", SearchOption.AllDirectories).Length == 1, "actual submit");
                var request = Assert.Single(Directory.GetDirectories(queue, "request-*"));
                Assert.Equal(Encoding.UTF8.GetBytes(prompt), await File.ReadAllBytesAsync(Path.Combine(request, "prompt.txt")));
                using (var inspected = JsonDocument.Parse(await Python(own, Path.Combine(Tools, "relay_worker.py"), "inspect", request)))
                    Assert.Equal(prompt, inspected.RootElement.GetProperty("Prompt").GetString());
                var packet = Path.Combine(own, "packet.json");
                await File.WriteAllTextAsync(packet, JsonSerializer.Serialize(new
                {
                    Completion = mode == "consumer-error" ? "repair" : "turn",
                    Writes = new[] { new { Path = "output/response.json", ExpectedSHA256 = "missing", Data = new { message = "synthetic Ж🙂" } } },
                    FilesModified = new[] { "output/response.json" }
                }));
                await Python(own, Path.Combine(Tools, "relay_worker.py"), "answer", request, packet, "--adapter", "inert-native-no-provider");
                if (mode == "close-child")
                {
                    await Until(() => File.Exists(Path.Combine(own, "consumer-entered")), "actual consumer entered barrier");
                    await Python(own, Path.Combine(Tools, "relay_worker.py"), "close", queue);
                    Assert.False(File.Exists(Path.Combine(queue, "closed.json")), "Cannot ACK a held original child");
                    await File.WriteAllTextAsync(Path.Combine(own, "release-consumer"), "release");
                }
                await Until(() => File.Exists(Path.Combine(request, "execution.json")), "child exit and output drained");
                using (var execution = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(request, "execution.json"))))
                {
                    Assert.True(execution.RootElement.GetProperty("Executed").GetBoolean());
                    Assert.True(execution.RootElement.GetProperty("ChildExited").GetBoolean());
                    Assert.True(execution.RootElement.GetProperty("IoDrained").GetBoolean());
                    Assert.Equal(mode != "consumer-error", execution.RootElement.GetProperty("ExitCode").GetInt32() == 0);
                }
                Assert.Equal(mode != "consumer-error", File.Exists(Path.Combine(session, "ready/turn_complete.json")));
                Assert.Equal(mode != "consumer-error", File.Exists(Path.Combine(session, "output/response.json")));
                if (mode == "consumer-error") Assert.Contains("Response kind/count mismatch", await File.ReadAllTextAsync(Path.Combine(request, "consumer.log")));
                else
                {
                    using var completion = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(session, "ready/turn_complete.json")));
                    Assert.Equal(turn.requestId, completion.RootElement.GetProperty("requestId").GetString());
                }
                await Python(own, Path.Combine(Tools, "relay_worker.py"), "close", queue);
                await Until(() => File.Exists(Path.Combine(queue, "closed.json")), "execution closure");
                using var closed = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(queue, "closed.json")));
                foreach (var key in new[] { "ExecutionDisabled", "ChildExited", "IoDrained" }) Assert.True(closed.RootElement.GetProperty(key).GetBoolean());
                Assert.Single(Directory.GetDirectories(queue, "request-*"));
            }
            await File.WriteAllTextAsync(Path.Combine(own, "stop-relay"), "stop");
            var exited = await retained.RootExited.WaitAsync(TimeSpan.FromSeconds(4));
            Assert.Equal(0, exited.ExitCode);
            using var modes = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(own, "console-modes.json")));
            Assert.Equal(modes.RootElement.GetProperty("Before").GetRawText(), modes.RootElement.GetProperty("After").GetRawText());
        }
        finally
        {
            if (terminal != null)
            {
                var proof = await terminal.StopAndObserveAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(8));
                await File.WriteAllTextAsync(Path.Combine(own, "cleanup.json"), JsonSerializer.Serialize(proof));
                Assert.Equal(GmWorkerStopState.StoppedWithinScope, proof.State);
                Assert.True(proof.CleanupComplete);
                Assert.False(proof.AuthorityRetained);
                if (pump != null) await pump.WaitAsync(TimeSpan.FromSeconds(8));
                await terminal.DisposeAsync();
            }
            await File.WriteAllBytesAsync(Path.Combine(own, "terminal.bin"), output.ToArray());
            output.Dispose();
        }
    }
}
