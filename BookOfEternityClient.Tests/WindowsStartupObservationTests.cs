using System.Diagnostics;
using System.Text;
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
    [InlineData("timeout")]
    public async Task ControlledForegroundOwnerRequiresExitPipesAndBoundedObservation(string mode)
    {
        Assert.True(OperatingSystem.IsLinux()); // controlled process evidence, not Windows WMI
        var script = mode switch
        {
            "valid" => $"import sys;sys.stdout.write('{Observation}')",
            "exit-failure" => $"import sys;sys.stdout.write('{Observation}');sys.exit(7)",
            "too-large" => "import sys;sys.stdout.write('x'*5000)",
            "stderr-too-large" => "import sys;sys.stderr.write('x'*9000)",
            _ => "import time;time.sleep(2)"
        };
        var starts = 0;
        var probe = new WindowsStartupObservationProbe(() =>
        {
            starts++;
            return ControlledStart(script);
        }, TimeSpan.FromMilliseconds(300), TimeSpan.FromSeconds(1));
        using var cancellation = new CancellationTokenSource();
        if (mode == "cancel") cancellation.CancelAfter(TimeSpan.FromMilliseconds(50));
        var result = default(WindowsStartupObservation);
        var failure = await Record.ExceptionAsync(async () => result = await probe.ReadAsync(cancellation.Token));
        Assert.Equal(1, starts);
        Assert.True(probe.TrySettle());
        if (mode == "valid") { Assert.Null(failure); Assert.Equal(Observation, result!.Value); }
        else if (mode == "timeout") Assert.IsType<TimeoutException>(failure);
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
        }, TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(20), captureLinuxIdentity: _ => null);
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
