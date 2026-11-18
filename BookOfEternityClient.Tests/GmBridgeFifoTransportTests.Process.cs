using System.Diagnostics;
using BookOfEternityClient.Services.GmRuntime;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GmBridgeFifoTransportTests
{
    [Theory]
    [InlineData("echo")]
    [InlineData("epipe")]
    [InlineData("wait")]
    public async Task IndependentProcess_UsesSameConnector_AndClosesOriginalChannel(string mode)
    {
        await InPrivateRoot(async root =>
        {
            await using var listener = LinuxBridgeFifoListener.Create(root);
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var assembly = typeof(GmBridgeFifoTransportTests).Assembly.Location;
            var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
            { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var arg in new[] { "exec", "--runtimeconfig", Path.ChangeExtension(assembly, ".runtimeconfig.json"),
                "--depsfile", Path.ChangeExtension(assembly, ".deps.json"), typeof(GmBridgeFifoPeerHost.Program).Assembly.Location,
                listener.Endpoint.DirectoryPath, listener.Endpoint.RunId, mode }) start.ArgumentList.Add(arg);
            // Same direct-child pattern as existing official crash-host tests; no
            // detached launcher, extra child, production helper or PID-auth claim.
            using var process = Process.Start(start) ?? throw new IOException("Owned FIFO test peer did not start.");
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            try
            {
                Assert.NotEqual(Environment.ProcessId, process.Id);
                await using var server = await listener.AcceptAsync(deadline.Token);
                if (mode == "echo")
                {
                    var bytes = new byte[131072];
                    await server.ReadExactlyAsync(bytes, deadline.Token);
                    Assert.Equal(Enumerable.Range(0, bytes.Length).Select(i => (byte)(i % 251)).ToArray(), bytes);
                    await server.WriteAsync(bytes, deadline.Token);
                }
                else if (mode == "epipe") await server.DisposeAsync();
                else
                {
                    var ready = new byte[1];
                    await server.ReadExactlyAsync(ready, deadline.Token);
                    Assert.Equal(42, ready[0]);
                    process.Kill();
                }
                await process.WaitForExitAsync(deadline.Token);
                if (mode != "wait")
                {
                    Assert.True(process.ExitCode == 0, await error);
                    Assert.Contains(mode == "echo" ? $"FIFO_PEER_OK {process.Id}" : $"FIFO_EPIPE_OK {process.Id}", await output);
                }
                else Assert.NotEqual(0, process.ExitCode);
                if (mode != "epipe") Assert.Equal(0, await server.ReadAsync(new byte[1], deadline.Token));
                await server.DisposeAsync();
                await listener.DisposeAsync();
                Assert.Empty(Directory.EnumerateFileSystemEntries(root));
            }
            finally
            {
                if (!process.HasExited) process.Kill();
                await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
                await Task.WhenAll(output, error).WaitAsync(TimeSpan.FromSeconds(5));
            }
        });
    }
}
