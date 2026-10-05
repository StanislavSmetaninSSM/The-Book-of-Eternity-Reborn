using System.Diagnostics;
using System.IO.Pipes;
using System.Text.Json;
using System.Text;
using BookOfEternityClient.Services.GmWorkers;
using Xunit;

namespace BookOfEternityClient.Tests;

[Trait("Category", "ProcessIntegration")]
public sealed partial class GmWorkerProcessHostTests
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
            worker.Environment.Clear();
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
    public Task WaitUntilReadyAsync_ForeignNamedPipeClientIsRejectedBeforeLaunch() =>
        AssertForeignChannelsRejectedAsync(foreignControl: true, foreignStatus: true);

    [Fact]
    public Task WaitUntilReadyAsync_ForeignControlClientIsRejectedWhenStatusClientMatchesHost() =>
        AssertForeignChannelsRejectedAsync(foreignControl: true, foreignStatus: false);

    [Fact]
    public Task WaitUntilReadyAsync_ForeignStatusClientIsRejectedWhenControlClientMatchesHost() =>
        AssertForeignChannelsRejectedAsync(foreignControl: false, foreignStatus: true);

    [Fact]
    public async Task WaitUntilReadyAsync_ActualHostReadyThenOwnerCloseNeverStartsWorker()
    {
        var root = CreateTempRoot();
        var marker = Path.Combine(root, "worker-started");
        Process? host = null;
        try
        {
            var worker = CreateWorker(root);
            worker.ArgumentList.Add("-NoProfile");
            worker.ArgumentList.Add("-NonInteractive");
            worker.ArgumentList.Add("-Command");
            worker.ArgumentList.Add("[IO.File]::WriteAllText($env:BOE_START_MARKER, 'started')");
            worker.Environment["BOE_START_MARKER"] = marker;
            await using (var launch = GmWorkerProcessHostLaunch.Create(worker, root))
            {
                host = Process.Start(launch.StartInfo)!;
                Assert.NotEqual(Environment.ProcessId, host.Id);
                using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                await launch.WaitUntilReadyAsync(host, deadline.Token);
                Assert.False(host.HasExited);
                Assert.False(File.Exists(marker));
                // Deliberately never send Release. Closing ownership must end admission.
            }
            await host.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(125, host.ExitCode);
            Assert.False(File.Exists(marker));
            Assert.DoesNotContain(marker, await host.StandardError.ReadToEndAsync(), StringComparison.Ordinal);
        }
        finally
        {
            await StopOwnedProcessAsync(host);
            CleanupTempRoot(root);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WaitUntilReadyAsync_StatusFrameByteLimitIsExact(bool oversized)
    {
        const int maximumBytes = 64 * 1024;
        await WithOwnedStatusPeerAsync(nonce =>
        {
            var json = ReadyJson(nonce);
            return Encoding.UTF8.GetBytes(new string(' ', maximumBytes - Encoding.UTF8.GetByteCount(json) + (oversized ? 1 : 0)) + json + "\n");
        }, rejected: oversized);
    }

    [Fact]
    public async Task WaitUntilReadyAsync_UnterminatedStatusAtEofIsRejected()
    {
        await WithOwnedStatusPeerAsync(nonce => Encoding.UTF8.GetBytes(ReadyJson(nonce)),
            rejected: true, closeStatus: true);
    }

    [Fact]
    public async Task WaitUntilReadyAsync_InvalidUtf8IsRejectedBeforeJsonParsing()
    {
        await WithOwnedStatusPeerAsync(nonce =>
        {
            var prefix = Encoding.UTF8.GetBytes($"{{\"schemaVersion\":1,\"launchNonce\":\"{nonce}\",\"kind\":\"failed\",\"exitCode\":null,\"error\":\"");
            return prefix.Concat(new byte[] { 0xc3, 0x28 }).Concat(Encoding.UTF8.GetBytes("\"}\n")).ToArray();
        }, rejected: true);
    }

    [Theory]
    [InlineData("nonce")]
    [InlineData("schema")]
    public Task WaitUntilReadyAsync_InvalidStatusEnvelopeIsRejected(string invalidField) =>
        WithOwnedStatusPeerAsync(nonce => Encoding.UTF8.GetBytes(
            GmWorkerProcessHostProtocol.SerializeStatus(new(
                invalidField == "schema" ? 2 : 1,
                invalidField == "nonce" ? LaunchNonce : nonce,
                GmWorkerProcessHostStatusKind.Ready, null, null)) + "\n"), rejected: true);

    [Theory]
    [InlineData("malformed")]
    [InlineData("duplicate")]
    [InlineData("unknown")]
    public void ParseControl_DiagnosticsNeverIncludePayloadExcerpts(string shape)
    {
        const string secret = "SECRET_PAYLOAD_SENTINEL";
        var json = shape switch
        {
            "malformed" => $"{{\"schemaVersion\": {secret}}}",
            "duplicate" => $"{{\"{secret}\":1,\"{secret}\":2}}",
            _ => $"{{\"{secret}\":1}}"
        };
        var error = Assert.Throws<InvalidDataException>(() =>
            GmWorkerProcessHostProtocol.ParseControl(json, LaunchNonce, GmWorkerProcessHostControlKind.Launch));
        Assert.DoesNotContain(secret, error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ParseStatus_FailedDiagnosticNeverIncludesPeerText()
    {
        const string secret = "SECRET_ENVIRONMENT_SENTINEL";
        var json = GmWorkerProcessHostProtocol.SerializeStatus(new(1, LaunchNonce,
            GmWorkerProcessHostStatusKind.Failed, null, secret));
        var error = Assert.Throws<InvalidOperationException>(() =>
            GmWorkerProcessHostProtocol.ParseStatus(json, LaunchNonce, GmWorkerProcessHostStatusKind.Ready));
        Assert.DoesNotContain(secret, error.ToString(), StringComparison.Ordinal);
    }

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

        Assert.Contains("malformed", error.Message, StringComparison.OrdinalIgnoreCase);
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

        Assert.Contains("malformed", error.Message, StringComparison.OrdinalIgnoreCase);
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

    private static ProcessStartInfo CreateWorker(string root)
    {
        var worker = new ProcessStartInfo
        {
            FileName = ResolvePowerShellExecutable(),
            WorkingDirectory = root,
            UseShellExecute = false
        };
        // The fixture owns its payload. Inheriting machine/provider environment
        // would add unrelated secrets and Linux case-variant key collisions.
        worker.Environment.Clear();
        return worker;
    }

    private static string ReadyJson(string nonce) => GmWorkerProcessHostProtocol.SerializeStatus(
        new(1, nonce, GmWorkerProcessHostStatusKind.Ready, null, null));

    private static async Task AssertForeignChannelsRejectedAsync(bool foreignControl, bool foreignStatus)
    {
        var root = CreateTempRoot();
        Process? foreign = null;
        NamedPipeClientStream? localControl = null;
        NamedPipeClientStream? localStatus = null;
        try
        {
            await using (var launch = GmWorkerProcessHostLaunch.Create(CreateWorker(root), root))
            {
                var args = launch.StartInfo.ArgumentList.ToArray();
                foreign = StartPipePeer(root, foreignControl ? args[^3] : "", foreignStatus ? args[^2] : "", "foreign");
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                if (!foreignControl)
                {
                    localControl = new NamedPipeClientStream(".", args[^3], PipeDirection.In, PipeOptions.Asynchronous);
                    await localControl.ConnectAsync(timeout.Token);
                }
                if (!foreignStatus)
                {
                    localStatus = new NamedPipeClientStream(".", args[^2], PipeDirection.Out, PipeOptions.Asynchronous);
                    await localStatus.ConnectAsync(timeout.Token);
                }
                using var expectedHost = Process.GetCurrentProcess();
                var error = await Assert.ThrowsAsync<InvalidDataException>(() =>
                    launch.WaitUntilReadyAsync(expectedHost, timeout.Token));
                Assert.Contains(foreignControl ? "control" : "status", error.Message, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("unexpected process", error.Message, StringComparison.OrdinalIgnoreCase);
                await AssertConnectedPeerAsync(foreign, timeout.Token);
                await Assert.ThrowsAnyAsync<Exception>(() => launch.WaitUntilReadyAsync(expectedHost, timeout.Token));
                await Assert.ThrowsAnyAsync<Exception>(() => launch.ReleaseAsync(timeout.Token));
            }
            if (localControl != null)
                Assert.Equal(0, await localControl.ReadAsync(new byte[1]).AsTask().WaitAsync(TimeSpan.FromSeconds(3)));
            if (!foreignControl) await foreign.StandardInput.WriteLineAsync("close");
            await foreign.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(0, foreign.ExitCode);
            Assert.Equal(foreignControl ? "-1" : "no-control", (await foreign.StandardOutput.ReadToEndAsync()).Trim());
        }
        finally
        {
            localControl?.Dispose();
            localStatus?.Dispose();
            await StopOwnedProcessAsync(foreign);
            CleanupTempRoot(root);
        }
    }

    private static async Task StopOwnedProcessAsync(Process? process)
    {
        if (process == null) return;
        try
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally { process.Dispose(); }
    }

    private static void CleanupTempRoot(string root)
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        Assert.False(Directory.Exists(root));
    }
}
