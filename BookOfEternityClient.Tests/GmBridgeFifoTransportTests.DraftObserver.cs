using System.Diagnostics;
using System.Text;
using System.Text.Json;
using BookOfEternityClient.Services.GmRuntime;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GmBridgeFifoTransportTests
{
    [Fact]
    public async Task ActualDraftHelper_FifoDescriptorSurvivesChangedTmpdirAndPreservesActualFile()
    {
        var root = Path.Combine(TestDiagnosticPaths.Root, "bridge-draft", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var file = Path.Combine(root, "1234567890123.md");
            var bytes = Encoding.UTF8.GetBytes("actual read-only draft\nКириллица café 🌌");
            await File.WriteAllBytesAsync(file, bytes);
            var childTemp = Path.Combine(root, "child-tmp");
            Directory.CreateDirectory(childTemp);
            string listenerPath;
            await using (var listener = LinuxBridgeFifoListener.Create(root))
            {
                listenerPath = listener.Endpoint.DirectoryPath;
                using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
                var bridge = Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityGMBridge/bin", configuration, "net8.0/BookOfEternityGMBridge.dll");
                var dotnet = Path.Combine(Environment.GetEnvironmentVariable("DOTNET_ROOT") ?? throw new IOException("DOTNET_ROOT is absent."), "dotnet");
                var start = new ProcessStartInfo(dotnet)
                {
                    UseShellExecute = false, RedirectStandardInput = true,
                    RedirectStandardOutput = true, RedirectStandardError = true,
                    WorkingDirectory = root
                };
                foreach (var argument in new[] { bridge, "--observe-draft", file }) start.ArgumentList.Add(argument);
                start.Environment["BOE_DRAFT_ENDPOINT"] = BridgeTransportConnector.EncodeEndpoint(
                    new(1, "linux-fifo-v1", listener.Endpoint.DirectoryPath, listener.Endpoint.RunId));
                start.Environment.Remove("BOE_DRAFT_PIPE");
                start.Environment["BOE_DRAFT_DIRECTORY"] = root;
                start.Environment["BOE_DRAFT_BINDING"] = "own-original-observer-binding";
                start.Environment["TMPDIR"] = childTemp;
                using var child = Process.Start(start) ?? throw new IOException("Actual observer did not start.");
                child.StandardInput.Close();
                var stdout = child.StandardOutput.ReadToEndAsync();
                var stderr = child.StandardError.ReadToEndAsync();
                var exited = child.WaitForExitAsync(deadline.Token);
                var accepting = listener.AcceptAsync(deadline.Token);
                try
                {
                    if (await Task.WhenAny(accepting, exited) == exited)
                    {
                        await exited;
                        throw new IOException($"Actual observer exited {child.ExitCode} before connection: {await stderr}");
                    }
                    await using var stream = await accepting;
                    var reader = new MainOperationReader(stream);
                    var begin = await reader.ReadAsync<JsonElement>(deadline.Token);
                    Assert.Equal("observeDraft", begin.GetProperty("command").GetString());
                    Assert.Equal("own-original-observer-binding", begin.GetProperty("binding").GetString());
                    Assert.Equal(file, begin.GetProperty("path").GetString());
                    await MainOperationReader.WriteAsync(stream, new { ok = true, nonce = "original-observer-challenge" }, deadline.Token);
                    var proof = await reader.ReadAsync<JsonElement>(deadline.Token, 100000);
                    Assert.Equal("original-observer-challenge", proof.GetProperty("nonce").GetString());
                    Assert.Equal(file, proof.GetProperty("path").GetString());
                    Assert.Equal(bytes, Convert.FromBase64String(proof.GetProperty("bytes").GetString()!));
                    await MainOperationReader.WriteAsync(stream, new { ok = true }, deadline.Token);
                    var completed = await reader.ReadAsync<JsonElement>(deadline.Token);
                    Assert.Equal("draftComplete", completed.GetProperty("command").GetString());
                    Assert.Equal("original-observer-challenge", completed.GetProperty("nonce").GetString());
                    Assert.Equal(file, completed.GetProperty("path").GetString());
                    await MainOperationReader.WriteAsync(stream, new { ok = true, nonce = "original-observer-challenge" }, deadline.Token);
                    await exited;
                    Assert.True(child.ExitCode == 0, $"Actual observer exited {child.ExitCode}: {await stderr}");
                    Assert.Null(await reader.ReadAsync<JsonElement?>(deadline.Token));
                    Assert.Equal(bytes, await File.ReadAllBytesAsync(file, deadline.Token));
                    Assert.Equal("", await stdout);
                }
                finally
                {
                    deadline.Cancel();
                    if (!child.HasExited) { child.Kill(entireProcessTree: true); await child.WaitForExitAsync(); }
                    try { await exited; } catch (OperationCanceledException) { }
                    // Always settle a pending accept and both output drains before
                    // releasing this test's owned directory and process wrapper.
                    try { await using var accepted = await accepting; }
                    catch (OperationCanceledException) { }
                    await stdout;
                    await stderr;
                }
            }
            Assert.False(Directory.Exists(listenerPath));
        }
        finally { Directory.Delete(root, true); }
    }
}
