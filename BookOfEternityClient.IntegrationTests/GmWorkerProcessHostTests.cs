using System.Diagnostics;
using System.IO.Pipes;
using System.Text.Json;
using BookOfEternityClient.Services.GmWorkers;
using Xunit;

namespace BookOfEternityClient.Tests;

[Trait("Category", "ProcessIntegration")]
public sealed class GmWorkerProcessHostTests
{
    private const string LaunchNonce = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    [Fact]
    public async Task Create_UsesPidAuthenticatedNamedChannelsInsteadOfInheritedHandlesOrMarkerPaths()
    {
        var root = CreateTempRoot();
        try
        {
            var secretMarker = "secret-" + Guid.NewGuid().ToString("N");
            var worker = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                WorkingDirectory = root,
                UseShellExecute = false
            };
            worker.ArgumentList.Add("-NoProfile");
            worker.ArgumentList.Add("-Command");
            worker.ArgumentList.Add("exit 0");
            worker.Environment["BOE_TEST_SECRET"] = secretMarker;
            worker.Environment["BOE_TEST_LARGE_PAYLOAD"] = new string('x', 40_000);

            await using var launch = GmWorkerProcessHostLaunch.Create(worker, root);
            var arguments = launch.StartInfo.ArgumentList.ToArray();
            Assert.Equal(4, arguments.Length);
            var controlEndpoint = arguments[1];
            var statusEndpoint = arguments[2];

            Assert.DoesNotContain(arguments, argument =>
                argument.EndsWith(".ready", StringComparison.OrdinalIgnoreCase) ||
                argument.EndsWith(".release", StringComparison.OrdinalIgnoreCase) ||
                argument.EndsWith(".completed", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(arguments, argument => argument.Contains(secretMarker, StringComparison.Ordinal));
            Assert.All(arguments, argument => Assert.True(argument.Length < 512));
            Assert.StartsWith("boe-gm-worker-control-", controlEndpoint, StringComparison.Ordinal);
            Assert.StartsWith("boe-gm-worker-status-", statusEndpoint, StringComparison.Ordinal);
            Assert.False(controlEndpoint.All(char.IsDigit));
            Assert.False(statusEndpoint.All(char.IsDigit));
            Assert.Empty(Directory.EnumerateFiles(root, "worker-host-*", SearchOption.TopDirectoryOnly));
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task WaitUntilReadyAsync_ForeignNamedPipeClientIsRejectedBeforeLaunch()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var root = CreateTempRoot();
        Process? foreignClient = null;
        try
        {
            var worker = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                WorkingDirectory = root,
                UseShellExecute = false
            };
            worker.ArgumentList.Add("-NoProfile");
            worker.ArgumentList.Add("-Command");
            worker.ArgumentList.Add("exit 0");

            await using var launch = GmWorkerProcessHostLaunch.Create(worker, root);
            var arguments = launch.StartInfo.ArgumentList.ToArray();
            var controlEndpoint = arguments[1];
            var statusEndpoint = arguments[2];
            foreignClient = StartForeignPipeClient(root, controlEndpoint, statusEndpoint);

            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var expectedHost = Process.GetCurrentProcess();
            var error = await Assert.ThrowsAsync<InvalidDataException>(() =>
                launch.WaitUntilReadyAsync(expectedHost, cancellation.Token));

            Assert.Contains("unexpected process", error.Message, StringComparison.OrdinalIgnoreCase);
            Assert.False(foreignClient.HasExited);
        }
        finally
        {
            if (foreignClient is { HasExited: false })
            {
                foreignClient.Kill(entireProcessTree: true);
                await foreignClient.WaitForExitAsync();
            }
            foreignClient?.Dispose();
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public Task WaitUntilReadyAsync_ForeignControlClientIsRejectedWhenStatusClientMatchesHost() =>
        AssertSingleForeignChannelRejectedAsync(foreignControlChannel: true);

    [Fact]
    public Task WaitUntilReadyAsync_ForeignStatusClientIsRejectedWhenControlClientMatchesHost() =>
        AssertSingleForeignChannelRejectedAsync(foreignControlChannel: false);

    [Fact]
    public void ParseStatus_WrongLaunchNonceIsRejected()
    {
        var json = GmWorkerProcessHostProtocol.SerializeStatus(
            new GmWorkerProcessHostStatusFrame(
                1,
                "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
                GmWorkerProcessHostStatusKind.Ready,
                ExitCode: null,
                Error: null));

        var error = Assert.Throws<InvalidDataException>(() =>
            GmWorkerProcessHostProtocol.ParseStatus(
                json,
                LaunchNonce,
                GmWorkerProcessHostStatusKind.Ready));

        Assert.Contains("nonce", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParseStatus_CompletedFrameWithoutExitCodeIsRejected()
    {
        var json = GmWorkerProcessHostProtocol.SerializeStatus(
            new GmWorkerProcessHostStatusFrame(
                1,
                LaunchNonce,
                GmWorkerProcessHostStatusKind.Completed,
                ExitCode: null,
                Error: null));

        var error = Assert.Throws<InvalidDataException>(() =>
            GmWorkerProcessHostProtocol.ParseStatus(
                json,
                LaunchNonce,
                GmWorkerProcessHostStatusKind.Completed));

        Assert.Contains("exitCode", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ParseControl_WrongLaunchNonceCannotReleaseWorker()
    {
        var json = GmWorkerProcessHostProtocol.SerializeControl(
            new GmWorkerProcessHostControlFrame(
                1,
                "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
                GmWorkerProcessHostControlKind.Release));

        var error = Assert.Throws<InvalidDataException>(() =>
            GmWorkerProcessHostProtocol.ParseControl(
                json,
                LaunchNonce,
                GmWorkerProcessHostControlKind.Release));

        Assert.Contains("nonce", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParseStatus_MissingKindCannotBecomeReadyByEnumDefault()
    {
        var json = $$"""{"schemaVersion":1,"launchNonce":"{{LaunchNonce}}","exitCode":null,"error":null}""";

        var error = Assert.Throws<InvalidDataException>(() =>
            GmWorkerProcessHostProtocol.ParseStatus(
                json,
                LaunchNonce,
                GmWorkerProcessHostStatusKind.Ready));

        Assert.Contains("kind", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("ready", "exitCode")]
    [InlineData("ready", "error")]
    [InlineData("completed", "error")]
    [InlineData("outputDrained", "exitCode")]
    [InlineData("outputDrained", "error")]
    public void ParseStatus_MissingRequiredNullableFieldIsRejected(
        string kind,
        string missingProperty)
    {
        var values = new Dictionary<string, object?>
        {
            ["schemaVersion"] = 1,
            ["launchNonce"] = LaunchNonce,
            ["kind"] = kind,
            ["exitCode"] = string.Equals(kind, "completed", StringComparison.Ordinal) ? 0 : null,
            ["error"] = null
        };
        values.Remove(missingProperty);
        var json = JsonSerializer.Serialize(values);
        var expectedKind = Enum.Parse<GmWorkerProcessHostStatusKind>(kind, ignoreCase: true);

        Assert.Throws<InvalidDataException>(() =>
            GmWorkerProcessHostProtocol.ParseStatus(json, LaunchNonce, expectedKind));
    }

    [Fact]
    public void ParseControl_MissingKindCannotReleaseWorkerByEnumDefault()
    {
        var json = $$"""{"schemaVersion":1,"launchNonce":"{{LaunchNonce}}"}""";

        var error = Assert.Throws<InvalidDataException>(() =>
            GmWorkerProcessHostProtocol.ParseControl(
                json,
                LaunchNonce,
                GmWorkerProcessHostControlKind.Release));

        Assert.Contains("kind", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParseControl_ReleaseFrameMissingRequiredPayloadFieldIsRejected()
    {
        var json = $$"""{"schemaVersion":1,"launchNonce":"{{LaunchNonce}}","kind":"release"}""";

        Assert.Throws<InvalidDataException>(() =>
            GmWorkerProcessHostProtocol.ParseControl(
                json,
                LaunchNonce,
                GmWorkerProcessHostControlKind.Release));
    }

    [Fact]
    public void ValidateLaunchNonce_MissingNonceProducesTypedProtocolFailure()
    {
        var error = Assert.Throws<InvalidDataException>(() =>
            GmWorkerProcessHostProtocol.ValidateLaunchNonce(null!));

        Assert.Contains("nonce", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParseStatus_OutputDrainedFrameMustNotCarryExitCode()
    {
        var json = GmWorkerProcessHostProtocol.SerializeStatus(
            new GmWorkerProcessHostStatusFrame(
                1,
                LaunchNonce,
                GmWorkerProcessHostStatusKind.OutputDrained,
                ExitCode: 0,
                Error: null));

        var error = Assert.Throws<InvalidDataException>(() =>
            GmWorkerProcessHostProtocol.ParseStatus(
                json,
                LaunchNonce,
                GmWorkerProcessHostStatusKind.OutputDrained));

        Assert.Contains("exitCode", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ParseStatus_UnknownPropertyIsRejected()
    {
        var json = $$"""{"schemaVersion":1,"launchNonce":"{{LaunchNonce}}","kind":"ready","exitCode":null,"error":null,"forged":true}""";

        Assert.Throws<InvalidDataException>(() =>
            GmWorkerProcessHostProtocol.ParseStatus(
                json,
                LaunchNonce,
                GmWorkerProcessHostStatusKind.Ready));
    }

    [Fact]
    public void ParseStatus_DuplicatePropertyIsRejected()
    {
        var json = $$"""{"schemaVersion":1,"schemaVersion":1,"launchNonce":"{{LaunchNonce}}","kind":"ready","exitCode":null,"error":null}""";

        var error = Assert.Throws<InvalidDataException>(() =>
            GmWorkerProcessHostProtocol.ParseStatus(
                json,
                LaunchNonce,
                GmWorkerProcessHostStatusKind.Ready));

        Assert.Contains("duplicate", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParseStatus_ReadyFrameMustNotCarryError()
    {
        var json = GmWorkerProcessHostProtocol.SerializeStatus(
            new GmWorkerProcessHostStatusFrame(
                1,
                LaunchNonce,
                GmWorkerProcessHostStatusKind.Ready,
                ExitCode: null,
                Error: "forged warning"));

        var error = Assert.Throws<InvalidDataException>(() =>
            GmWorkerProcessHostProtocol.ParseStatus(
                json,
                LaunchNonce,
                GmWorkerProcessHostStatusKind.Ready));

        Assert.Contains("error", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParseStatus_CompletedFrameMustNotCarryError()
    {
        var json = GmWorkerProcessHostProtocol.SerializeStatus(
            new GmWorkerProcessHostStatusFrame(
                1,
                LaunchNonce,
                GmWorkerProcessHostStatusKind.Completed,
                ExitCode: 0,
                Error: "forged warning"));

        var error = Assert.Throws<InvalidDataException>(() =>
            GmWorkerProcessHostProtocol.ParseStatus(
                json,
                LaunchNonce,
                GmWorkerProcessHostStatusKind.Completed));

        Assert.Contains("error", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParseStatus_FailedFrameMustNotCarryExitCode()
    {
        var json = GmWorkerProcessHostProtocol.SerializeStatus(
            new GmWorkerProcessHostStatusFrame(
                1,
                LaunchNonce,
                GmWorkerProcessHostStatusKind.Failed,
                ExitCode: 125,
                Error: "failed"));

        var error = Assert.Throws<InvalidDataException>(() =>
            GmWorkerProcessHostProtocol.ParseStatus(
                json,
                LaunchNonce,
                GmWorkerProcessHostStatusKind.Ready));

        Assert.Contains("exitCode", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ParseControl_LaunchFrameRequiresPayload()
    {
        var json = GmWorkerProcessHostProtocol.SerializeControl(
            new GmWorkerProcessHostControlFrame(
                1,
                LaunchNonce,
                GmWorkerProcessHostControlKind.Launch));

        var error = Assert.Throws<InvalidDataException>(() =>
            GmWorkerProcessHostProtocol.ParseControl(
                json,
                LaunchNonce,
                GmWorkerProcessHostControlKind.Launch));

        Assert.Contains("payload", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParseControl_ReleaseFrameMustNotCarryPayload()
    {
        var json = GmWorkerProcessHostProtocol.SerializeControl(
            new GmWorkerProcessHostControlFrame(
                1,
                LaunchNonce,
                GmWorkerProcessHostControlKind.Release,
                new GmWorkerProcessHostPayload(
                    "powershell.exe",
                    ["-NoProfile"],
                    string.Empty,
                    new Dictionary<string, string?>())));

        var error = Assert.Throws<InvalidDataException>(() =>
            GmWorkerProcessHostProtocol.ParseControl(
                json,
                LaunchNonce,
                GmWorkerProcessHostControlKind.Release));

        Assert.Contains("payload", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CompletionArbiter_AlreadySignaledTimeoutOverridesBufferedCompletion()
    {
        using var timeout = new CancellationTokenSource();
        timeout.Cancel();

        var result = await GmWorkerProcessCompletionArbiter.WaitAsync(
            Task.FromResult(0),
            _ => Task.CompletedTask,
            Task.Delay(Timeout.InfiniteTimeSpan),
            timeout.Token,
            CancellationToken.None);

        Assert.Equal(GmWorkerProcessCompletionOutcomeKind.TimedOut, result.Kind);
    }

    [Fact]
    public async Task CompletionArbiter_CancellationDuringOutputDrainOverridesCompletion()
    {
        using var cancellation = new CancellationTokenSource();
        var drainStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var drainRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var wait = GmWorkerProcessCompletionArbiter.WaitAsync(
            Task.FromResult(0),
            async token =>
            {
                drainStarted.TrySetResult();
                await drainRelease.Task.WaitAsync(token);
            },
            Task.Delay(Timeout.InfiniteTimeSpan),
            CancellationToken.None,
            cancellation.Token);

        await drainStarted.Task;
        cancellation.Cancel();
        var result = await wait;

        Assert.Equal(GmWorkerProcessCompletionOutcomeKind.Canceled, result.Kind);
    }

    private static string CreateTempRoot()
    {
        var root = Path.Combine(
            AppContext.BaseDirectory,
            "boe-test-artifacts",
            "gm-worker-host-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static Process StartForeignPipeClient(
        string workingDirectory,
        string controlEndpoint,
        string statusEndpoint)
    {
        const string script = """
            $control = [System.IO.Pipes.NamedPipeClientStream]::new(
                '.',
                $env:BOE_CONTROL_PIPE,
                [System.IO.Pipes.PipeDirection]::In)
            $status = [System.IO.Pipes.NamedPipeClientStream]::new(
                '.',
                $env:BOE_STATUS_PIPE,
                [System.IO.Pipes.PipeDirection]::Out)
            try {
                $control.Connect(10000)
                $status.Connect(10000)
                Start-Sleep -Seconds 30
            }
            finally {
                $control.Dispose()
                $status.Dispose()
            }
            """;
        var startInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-NonInteractive");
        startInfo.ArgumentList.Add("-Command");
        startInfo.ArgumentList.Add(script);
        startInfo.Environment["BOE_CONTROL_PIPE"] = controlEndpoint;
        startInfo.Environment["BOE_STATUS_PIPE"] = statusEndpoint;

        var process = new Process { StartInfo = startInfo };
        if (!process.Start())
        {
            process.Dispose();
            throw new InvalidOperationException("Foreign named-pipe client did not start.");
        }

        return process;
    }

    private static async Task AssertSingleForeignChannelRejectedAsync(bool foreignControlChannel)
    {
        if (!OperatingSystem.IsWindows())
            return;

        var root = CreateTempRoot();
        Process? foreignClient = null;
        try
        {
            var worker = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                WorkingDirectory = root,
                UseShellExecute = false
            };
            worker.ArgumentList.Add("-NoProfile");
            worker.ArgumentList.Add("-Command");
            worker.ArgumentList.Add("exit 0");

            await using var launch = GmWorkerProcessHostLaunch.Create(worker, root);
            var arguments = launch.StartInfo.ArgumentList.ToArray();
            var controlEndpoint = arguments[1];
            var statusEndpoint = arguments[2];
            var foreignEndpoint = foreignControlChannel ? controlEndpoint : statusEndpoint;
            var foreignDirection = foreignControlChannel ? PipeDirection.In : PipeDirection.Out;
            foreignClient = StartForeignPipeClient(root, foreignEndpoint, foreignDirection);

            var localEndpoint = foreignControlChannel ? statusEndpoint : controlEndpoint;
            var localDirection = foreignControlChannel ? PipeDirection.Out : PipeDirection.In;
            await using var localClient = new NamedPipeClientStream(
                ".",
                localEndpoint,
                localDirection,
                PipeOptions.Asynchronous);
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var localConnect = localClient.ConnectAsync(cancellation.Token);
            using var expectedHost = Process.GetCurrentProcess();

            var error = await Assert.ThrowsAsync<InvalidDataException>(() =>
                launch.WaitUntilReadyAsync(expectedHost, cancellation.Token));
            await localConnect;

            Assert.Contains(
                foreignControlChannel ? "control" : "status",
                error.Message,
                StringComparison.OrdinalIgnoreCase);
            Assert.False(foreignClient.HasExited);
        }
        finally
        {
            if (foreignClient is { HasExited: false })
            {
                foreignClient.Kill(entireProcessTree: true);
                await foreignClient.WaitForExitAsync();
            }
            foreignClient?.Dispose();
            CleanupTempRoot(root);
        }
    }

    private static Process StartForeignPipeClient(
        string workingDirectory,
        string endpoint,
        PipeDirection direction)
    {
        const string script = """
            $pipe = [System.IO.Pipes.NamedPipeClientStream]::new(
                '.',
                $env:BOE_PIPE,
                [System.IO.Pipes.PipeDirection]::$($env:BOE_PIPE_DIRECTION))
            try {
                $pipe.Connect(10000)
                Start-Sleep -Seconds 30
            }
            finally {
                $pipe.Dispose()
            }
            """;
        var startInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-NonInteractive");
        startInfo.ArgumentList.Add("-Command");
        startInfo.ArgumentList.Add(script);
        startInfo.Environment["BOE_PIPE"] = endpoint;
        startInfo.Environment["BOE_PIPE_DIRECTION"] = direction.ToString();

        var process = new Process { StartInfo = startInfo };
        if (!process.Start())
        {
            process.Dispose();
            throw new InvalidOperationException("Foreign named-pipe client did not start.");
        }

        return process;
    }

    private static void CleanupTempRoot(string root)
    {
        try
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
        catch
        {
            // ignored
        }
    }
}
