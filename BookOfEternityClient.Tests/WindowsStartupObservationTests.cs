using System.Diagnostics;
using System.Text;
using BookOfEternityClient.Services;
using BookOfEternityClient.Services.GmRuntime;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class WindowsStartupObservationTests
{
    private const string Observation = "wmi-lastboot-v1:2026-10-08T10:11:12.1234567Z";

    [Fact]
    public void ExactProviderObservationFitsIdentityWithoutChangingItsValue()
    {
        var observation = WindowsStartupObservation.Parse(Encoding.UTF8.GetBytes(Observation));
        Assert.Equal(Observation, observation.Value);
        var root = Path.GetFullPath(Path.GetTempPath());
        var identity = new GmSessionRunIdentity(root, Guid.NewGuid().ToString("N"),
            Guid.NewGuid().ToString("N"), 1, GmSessionRunBackend.WindowsJob,
            Guid.NewGuid().ToString("N"), observation.Value);
        GmSessionRunValidation.ValidateIdentity(identity);
    }

    [Theory]
    [InlineData("")]
    [InlineData("2026-10-08T10:11:12.1234567Z")]
    [InlineData("wmi-lastboot-v1:2026-10-08T10:11:12.1234567+00:00")]
    [InlineData("wmi-lastboot-v1:2026-02-30T10:11:12.1234567Z")]
    [InlineData("wmi-lastboot-v1:2026-10-08T10:11:12.1234567Z\n")]
    [InlineData("wmi-lastboot-v1:2026-10-08T10:11:12.1234567Z\nwmi-lastboot-v1:2026-10-08T10:11:12.1234567Z")]
    public void MissingAmbiguousOrNoncanonicalObservationRefuses(string value) =>
        Assert.Throws<InvalidDataException>(() => WindowsStartupObservation.Parse(Encoding.UTF8.GetBytes(value)));

    [Theory]
    [InlineData("valid")]
    [InlineData("exit-failure")]
    [InlineData("too-large")]
    [InlineData("stderr-too-large")]
    [InlineData("cancel")]
    [InlineData("pipes-eof-alive")]
    [InlineData("timeout")]
    public async Task ControlledForegroundOwnerRequiresExitPipesAndBoundedObservation(string mode)
    {
        Assert.True(OperatingSystem.IsLinux()); // controlled process evidence, not Windows WMI
        var acknowledgement = Path.Combine(Path.GetTempPath(), "boe-boot-pipes-" + Guid.NewGuid().ToString("N"));
        var script = mode switch
        {
            "valid" => $"import sys;sys.stdout.write('{Observation}')",
            "exit-failure" => $"import sys;sys.stdout.write('{Observation}');sys.exit(7)",
            "too-large" => "import sys;sys.stdout.write('x'*5000)",
            "stderr-too-large" => "import sys;sys.stderr.write('x'*9000)",
            "pipes-eof-alive" => $"import sys,os,time;sys.stdout.write('{Observation}');sys.stdout.flush();os.close(1);os.close(2);open(sys.argv[1],'w').write('closed');time.sleep(2)",
            _ => "import time;time.sleep(2)"
        };
        var starts = 0;
        var probe = new WindowsStartupObservationProbe(() =>
        {
            starts++;
            var start = ControlledStart(script);
            start.ArgumentList.Add(acknowledgement);
            return start;
        }, TimeSpan.FromMilliseconds(300), TimeSpan.FromSeconds(1));
        using var cancellation = new CancellationTokenSource();
        if (mode == "cancel") cancellation.CancelAfter(TimeSpan.FromMilliseconds(50));
        var result = default(WindowsStartupObservation);
        var failure = await Record.ExceptionAsync(async () => result = await probe.ReadAsync(cancellation.Token));
        var acknowledged = File.Exists(acknowledgement) ? File.ReadAllText(acknowledgement) : null;
        File.Delete(acknowledgement);
        Assert.Equal(1, starts);
        Assert.True(probe.TrySettle());
        if (mode == "valid") { Assert.Null(failure); Assert.Equal(Observation, result!.Value); }
        else if (mode is "timeout" or "pipes-eof-alive")
        {
            Assert.IsType<TimeoutException>(failure);
            if (mode == "pipes-eof-alive")
            {
                Assert.Equal("closed", acknowledged);
            }
        }
        else if (mode == "cancel") Assert.IsAssignableFrom<OperationCanceledException>(failure);
        else
        {
            var refused = Assert.IsType<IOException>(failure);
            Assert.Contains(mode == "exit-failure" ? "provider failed" : "output exceeded its bound", refused.Message);
        }
    }

    [Fact]
    public async Task UnsettledOriginalOwnerBlocksAnotherProbeUntilActualExitAndPipes()
    {
        Assert.True(OperatingSystem.IsLinux());
        var starts = 0;
        var probe = new WindowsStartupObservationProbe(() =>
        {
            starts++;
            return ControlledStart(starts == 1 ? "import time;time.sleep(1)" :
                $"import sys;sys.stdout.write('{Observation}')");
        }, TimeSpan.FromMilliseconds(300), TimeSpan.FromMilliseconds(20),
            captureLinuxIdentity: process => starts == 1 ? null : LinuxClipboardReaderIdentity.Capture(process));
        try
        {
            Assert.NotNull(await Record.ExceptionAsync(() => probe.ReadAsync(CancellationToken.None)));
            Assert.False(probe.TrySettle());
            Assert.NotNull(await Record.ExceptionAsync(() => probe.ReadAsync(CancellationToken.None)));
            Assert.Equal(1, starts);
        }
        finally
        {
            // The controlled original self-terminates. No guessed PID/tree cleanup.
            var deadline = Stopwatch.StartNew();
            while (!probe.TrySettle() && deadline.Elapsed < TimeSpan.FromSeconds(5))
                await Task.Delay(20);
            Assert.True(probe.TrySettle());
        }
        var next = await probe.ReadAsync(CancellationToken.None);
        Assert.Equal(Observation, next.Value);
        Assert.Equal(2, starts);
        Assert.True(probe.TrySettle());
    }

    [Fact]
    public async Task OriginalExitWithoutPipeEofCannotAcceptValidObservation()
    {
        Assert.True(OperatingSystem.IsLinux());
        var socketPath = Path.Combine(Path.GetTempPath(), "boe-boot-" + Guid.NewGuid().ToString("N"));
        // A separately test-owned process holds the original pipe descriptors
        // via SCM_RIGHTS. No PID lookup, guessed descendant or process-tree kill.
        const string holderScript = """
            import socket,array,sys,os
            server=socket.socket(socket.AF_UNIX);server.bind(sys.argv[1]);server.listen(1);server.settimeout(3)
            print('ready',flush=True)
            connection,_=server.accept()
            data,ancillary,flags,address=connection.recvmsg(1,socket.CMSG_SPACE(8))
            descriptors=array.array('i')
            for level,kind,value in ancillary:
                if level==socket.SOL_SOCKET and kind==socket.SCM_RIGHTS: descriptors.frombytes(value)
            assert len(descriptors)==2
            print('held',flush=True)
            sys.stdin.readline()
            for descriptor in descriptors: os.close(descriptor)
            connection.close();server.close()
            """;
        var start = ControlledStart(holderScript); start.ArgumentList.Add(socketPath);
        using var holder = Process.Start(start)!;
        using var identity = LinuxClipboardReaderIdentity.Capture(holder);
        var holderExit = holder.WaitForExitAsync();
        var errors = holder.StandardError.ReadToEndAsync();
        WindowsStartupObservationProbe? probe = null;
        try
        {
            Assert.Equal("ready", await holder.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(3)));
            Task? originalExit = null;
            probe = new WindowsStartupObservationProbe(() =>
            {
                var producer = ControlledStart($"import socket,array,sys;s=socket.socket(socket.AF_UNIX);s.connect(sys.argv[1]);s.sendmsg([b'x'],[(socket.SOL_SOCKET,socket.SCM_RIGHTS,array.array('i',[1,2]))]);s.close();sys.stdout.write('{Observation}');sys.stdout.flush()");
                producer.ArgumentList.Add(socketPath); return producer;
            }, TimeSpan.FromMilliseconds(300), TimeSpan.FromSeconds(1), process =>
            {
                originalExit = process.WaitForExitAsync();
                return LinuxClipboardReaderIdentity.Capture(process);
            });
            var failure = await Record.ExceptionAsync(() => probe.ReadAsync(CancellationToken.None));
            Assert.Equal("held", await holder.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(3)));
            Assert.NotNull(originalExit);
            Assert.True(originalExit.IsCompletedSuccessfully);
            Assert.False(holder.HasExited);
            Assert.IsType<TimeoutException>(failure);
        }
        finally
        {
            if (!holder.HasExited) { await holder.StandardInput.WriteLineAsync("release"); holder.StandardInput.Close(); }
            if (await Task.WhenAny(holderExit, Task.Delay(TimeSpan.FromSeconds(4))) != holderExit)
            {
                Assert.NotNull(identity);
                LinuxClipboardReaderIdentity.Stop(identity);
            }
            await holderExit.WaitAsync(TimeSpan.FromSeconds(3));
            await errors.WaitAsync(TimeSpan.FromSeconds(3));
            await holder.StandardOutput.ReadToEndAsync().WaitAsync(TimeSpan.FromSeconds(3));
            if (probe != null)
            {
                var deadline = Stopwatch.StartNew();
                while (!probe.TrySettle() && deadline.Elapsed < TimeSpan.FromSeconds(3)) await Task.Delay(20);
                Assert.True(probe.TrySettle());
            }
            File.Delete(socketPath);
        }
        Assert.Equal(0, holder.ExitCode);
    }

    private static ProcessStartInfo ControlledStart(string script)
    {
        var start = new ProcessStartInfo("python3")
        {
            UseShellExecute = false, RedirectStandardInput = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        start.ArgumentList.Add("-c"); start.ArgumentList.Add(script);
        return start;
    }
}
