using System.Diagnostics;
using System.Text;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GmWorkerProcessHostTests
{
    [Fact]
    public async Task WaitUntilReadyAsync_PartialConnectionExpiresAbsoluteReadyDeadline()
    {
        var root = CreateTempRoot();
        Process? peer = null;
        try
        {
            await using var launch = Services.GmWorkers.GmWorkerProcessHostLaunch.Create(CreateWorker(root), root);
            var args = launch.StartInfo.ArgumentList.ToArray();
            peer = StartPipePeer(root, args[^3], "", "foreign");
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var elapsed = Stopwatch.StartNew();
            var ready = launch.WaitUntilReadyAsync(peer, deadline.Token);
            await AssertConnectedPeerAsync(peer, deadline.Token);
            await Assert.ThrowsAsync<TimeoutException>(() => ready);
            Assert.InRange(elapsed.Elapsed.TotalSeconds, 12, 20);
            await Assert.ThrowsAnyAsync<Exception>(() => launch.ReleaseAsync(deadline.Token));
            await AssertPeerExitAsync(peer, "-1");
        }
        finally
        {
            await StopOwnedProcessAsync(peer);
            CleanupTempRoot(root);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task WaitUntilReadyAsync_CanceledPartialConnectionClosesWithoutLaunch(bool controlConnected)
    {
        var root = CreateTempRoot();
        Process? peer = null;
        try
        {
            await using var launch = Services.GmWorkers.GmWorkerProcessHostLaunch.Create(CreateWorker(root), root);
            var args = launch.StartInfo.ArgumentList.ToArray();
            peer = StartPipePeer(root, controlConnected ? args[^3] : "", controlConnected ? "" : args[^2], "foreign");
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
            var ready = launch.WaitUntilReadyAsync(peer, cancellation.Token);
            await AssertConnectedPeerAsync(peer, deadline.Token);
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ready);
            await Assert.ThrowsAnyAsync<Exception>(() => launch.WaitUntilReadyAsync(peer, deadline.Token));
            await Assert.ThrowsAnyAsync<Exception>(() => launch.ReleaseAsync(deadline.Token));
            if (!controlConnected) await peer.StandardInput.WriteLineAsync("close");
            await AssertPeerExitAsync(peer, controlConnected ? "-1" : "no-control");
        }
        finally
        {
            await StopOwnedProcessAsync(peer);
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task WaitUntilReadyAsync_ExitedExpectedHostCannotBeAdmitted()
    {
        var root = CreateTempRoot();
        Process? peer = null;
        try
        {
            await using var launch = Services.GmWorkers.GmWorkerProcessHostLaunch.Create(CreateWorker(root), root);
            var args = launch.StartInfo.ArgumentList.ToArray();
            peer = StartPipePeer(root, args[^3], args[^2], "exit");
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await AssertConnectedPeerAsync(peer, deadline.Token);
            await peer.StandardInput.WriteLineAsync("exit");
            await AssertPeerExitAsync(peer, "");
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                launch.WaitUntilReadyAsync(peer, deadline.Token));
            Assert.Contains("exited", error.Message, StringComparison.OrdinalIgnoreCase);
            await Assert.ThrowsAnyAsync<Exception>(() => launch.ReleaseAsync(deadline.Token));
        }
        finally
        {
            await StopOwnedProcessAsync(peer);
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task WaitUntilReadyAsync_CanceledBlockedLaunchAwaitsIoAndClosesChannels()
    {
        var root = CreateTempRoot();
        Process? peer = null;
        try
        {
            var worker = CreateWorker(root);
            worker.Environment["BOE_LARGE_TEST_PAYLOAD"] = new string('x', 900_000);
            await using var launch = Services.GmWorkers.GmWorkerProcessHostLaunch.Create(worker, root);
            var args = launch.StartInfo.ArgumentList.ToArray();
            peer = StartPipePeer(root, args[^3], args[^2], "blocked");
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
            var ready = launch.WaitUntilReadyAsync(peer, cancellation.Token);
            await AssertConnectedPeerAsync(peer, deadline.Token);
            using var progressCancellation = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
            var firstByte = peer.StandardOutput.ReadLineAsync(progressCancellation.Token).AsTask();
            if (await Task.WhenAny(firstByte, ready) == ready)
            {
                progressCancellation.Cancel();
                try { await firstByte; } catch (OperationCanceledException) { }
                await ready;
            }
            Assert.Equal("first-byte", await firstByte);
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ready);
            await Assert.ThrowsAnyAsync<Exception>(() => launch.ReleaseAsync(deadline.Token));
            await peer.StandardInput.WriteLineAsync("drain");
            await peer.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(0, peer.ExitCode);
            var received = int.Parse((await peer.StandardOutput.ReadToEndAsync()).Trim(), System.Globalization.CultureInfo.InvariantCulture);
            Assert.InRange(received, 1, 899_999);
            Assert.Empty(await peer.StandardError.ReadToEndAsync());
        }
        finally
        {
            await StopOwnedProcessAsync(peer);
            CleanupTempRoot(root);
        }
    }

    private static async Task WithOwnedStatusPeerAsync(
        Func<string, byte[]> statusBytes, bool rejected, bool closeStatus = false)
    {
        var root = CreateTempRoot();
        Process? peer = null;
        try
        {
            await using (var launch = Services.GmWorkers.GmWorkerProcessHostLaunch.Create(CreateWorker(root), root))
            {
                var args = launch.StartInfo.ArgumentList.ToArray();
                var statusPath = Path.Combine(root, "status-frame");
                await File.WriteAllBytesAsync(statusPath, statusBytes(args[^1]));
                peer = StartPipePeer(root, args[^3], args[^2], closeStatus ? "status-eof" : "status",
                    args[^1], statusPath);
                using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                var ready = launch.WaitUntilReadyAsync(peer, deadline.Token);
                await AssertConnectedPeerAsync(peer, deadline.Token);
                if (rejected) await Assert.ThrowsAsync<InvalidDataException>(() => ready);
                else await ready;
                Assert.Equal("launch", await peer.StandardOutput.ReadLineAsync(deadline.Token));
            }
            // Closing the owner is the only exit signal. No fixture ever receives Release.
            await AssertPeerExitAsync(peer, "-1");
        }
        finally
        {
            await StopOwnedProcessAsync(peer);
            CleanupTempRoot(root);
        }
    }

    private static async Task AssertConnectedPeerAsync(Process peer, CancellationToken token)
    {
        var line = await peer.StandardOutput.ReadLineAsync(token);
        Assert.NotNull(line);
        var fields = line.Split('|');
        Assert.Equal(3, fields.Length);
        Assert.Equal("connected", fields[0]);
        Assert.Equal(peer.Id.ToString(System.Globalization.CultureInfo.InvariantCulture), fields[1]);
        Assert.NotEqual(Environment.ProcessId, peer.Id);
        if (OperatingSystem.IsLinux())
        {
            var uid = File.ReadLines("/proc/self/status").Single(line => line.StartsWith("Uid:", StringComparison.Ordinal))
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)[2];
            Assert.Equal(uid, fields[2]);
        }
    }

    private static async Task AssertPeerExitAsync(Process peer, string expectedOutput)
    {
        await peer.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(0, peer.ExitCode);
        Assert.Equal(expectedOutput, (await peer.StandardOutput.ReadToEndAsync()).Trim());
        Assert.Empty(await peer.StandardError.ReadToEndAsync());
    }

    private static string ResolvePowerShellExecutable()
    {
        var executable = OperatingSystem.IsWindows() ? "pwsh.exe" : "pwsh";
        var paths = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator);
        return paths.Select(path => Path.GetFullPath(Path.Combine(path, executable)))
            .FirstOrDefault(File.Exists) ?? throw new InvalidOperationException("PowerShell 7 fixture executable is unavailable.");
    }

    private static Process StartPipePeer(string root, string controlEndpoint, string statusEndpoint,
        string mode, string nonce = "", string statusPath = "")
    {
        const string script = """
            $ErrorActionPreference = 'Stop'
            $control = $null
            $status = $null
            try {
                if ($env:BOE_CONTROL_PIPE) {
                    $control = [IO.Pipes.NamedPipeClientStream]::new('.', $env:BOE_CONTROL_PIPE, [IO.Pipes.PipeDirection]::In)
                    $control.Connect(10000)
                }
                if ($env:BOE_STATUS_PIPE) {
                    $status = [IO.Pipes.NamedPipeClientStream]::new('.', $env:BOE_STATUS_PIPE, [IO.Pipes.PipeDirection]::Out)
                    $status.Connect(10000)
                }
                $uid = 'windows'
                if ($IsLinux) { $uid = (([IO.File]::ReadLines('/proc/self/status') | Where-Object { $_.StartsWith('Uid:') }) -split '\s+')[2] }
                [Console]::WriteLine("connected|$PID|$uid")
                if ($env:BOE_PEER_MODE -eq 'exit') { $null = [Console]::ReadLine(); return }
                if ($env:BOE_PEER_MODE -eq 'blocked') {
                    $first = $control.ReadByte()
                    if ($first -lt 0) { throw 'Launch ended before first byte' }
                    [Console]::WriteLine('first-byte')
                    $null = [Console]::ReadLine()
                    $count = 1
                    $buffer = [byte[]]::new(8192)
                    while (($read = $control.Read($buffer, 0, $buffer.Length)) -gt 0) { $count += $read }
                    [Console]::WriteLine($count)
                    return
                }
                if ($env:BOE_PEER_MODE.StartsWith('status')) {
                    $reader = [IO.StreamReader]::new($control, [Text.Encoding]::UTF8, $false, 1024, $true)
                    $line = $reader.ReadLine()
                    if (-not $line) { throw 'Launch was not received' }
                    $launch = $line | ConvertFrom-Json
                    if ($launch.schemaVersion -ne 1 -or $launch.launchNonce -cne $env:BOE_PEER_NONCE -or $launch.kind -ne 'launch') {
                        throw 'Invalid Launch envelope'
                    }
                    [Console]::WriteLine('launch')
                    $bytes = [IO.File]::ReadAllBytes($env:BOE_STATUS_PATH)
                    $status.Write($bytes, 0, $bytes.Length)
                    if ($env:BOE_PEER_MODE -eq 'status-eof') { $status.Dispose(); $status = $null }
                }
                if ($control) { [Console]::WriteLine($control.ReadByte()) }
                else { [Console]::WriteLine('no-control'); $null = [Console]::ReadLine() }
            } finally {
                if ($control) { $control.Dispose() }
                if ($status) { $status.Dispose() }
            }
            """;
        var startInfo = new ProcessStartInfo
        {
            FileName = ResolvePowerShellExecutable(), WorkingDirectory = root, UseShellExecute = false,
            CreateNoWindow = true, RedirectStandardInput = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-NonInteractive");
        startInfo.ArgumentList.Add("-Command");
        startInfo.ArgumentList.Add(script);
        startInfo.Environment["BOE_CONTROL_PIPE"] = controlEndpoint;
        startInfo.Environment["BOE_STATUS_PIPE"] = statusEndpoint;
        startInfo.Environment["BOE_PEER_MODE"] = mode;
        startInfo.Environment["BOE_PEER_NONCE"] = nonce;
        startInfo.Environment["BOE_STATUS_PATH"] = statusPath;
        return Process.Start(startInfo) ?? throw new InvalidOperationException("Owned pipe fixture did not start.");
    }
}
