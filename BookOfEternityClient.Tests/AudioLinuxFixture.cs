using System.Diagnostics;
using System.Text.Json;
using BookOfEternityClient.Services;
using Microsoft.Win32.SafeHandles;
using Xunit;
namespace BookOfEternityClient.Tests;

internal sealed class AudioLinuxFixture : IDisposable
{
    internal readonly string Root = Path.Combine(Path.GetTempPath(), "boe-audio-" + Guid.NewGuid().ToString("N"));
    private Process? _host;
    private SafeFileHandle? _authority;
    private bool _timeoutStopped;
    internal bool OwnedCleanupConfirmed => _host == null || _host.HasExited;
    internal async Task<JsonElement> Run(string mode, TimeSpan? timeout = null)
    {
        Assert.True(OperatingSystem.IsLinux()); Directory.CreateDirectory(Root);
        var assembly = typeof(AudioLinuxFixture).Assembly.Location;
        var executable = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? Environment.ProcessPath!;
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in new[] { "exec", "--runtimeconfig", Path.ChangeExtension(assembly, ".runtimeconfig.json"),
            "--depsfile", Path.ChangeExtension(assembly, ".deps.json"), typeof(PortableStorageCrashHost.Program).Assembly.Location,
            Root, "synthetic", "audio", mode }) start.ArgumentList.Add(arg);
        _host = Process.Start(start)!;
        _authority = LinuxClipboardReaderIdentity.Capture(_host);
        Assert.True(_authority != null || _host.HasExited, "Cannot capture original fixture cleanup authority.");
        var stdout = _host.StandardOutput.ReadToEndAsync(); var stderr = _host.StandardError.ReadToEndAsync();
        using var deadline = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(15));
        try { await _host.WaitForExitAsync(deadline.Token); }
        catch (OperationCanceledException)
        {
            if (!_host.HasExited && _authority != null)
            { LinuxClipboardReaderIdentity.Stop(_authority); _timeoutStopped = true; }
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await _host.WaitForExitAsync(cleanup.Token);
            throw;
        }
        Assert.True(_host.ExitCode == 0, "Fixture preparation/execution: " + await stdout + await stderr);
        var report = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, "probe.json"))).RootElement.Clone();
        Directory.CreateDirectory(Evidence);
        File.WriteAllText(Path.Combine(Evidence, Path.GetFileName(Root) + ".json"), JsonSerializer.Serialize(new { Report = report, HostExit = _host.ExitCode }));
        return report;
    }
    private static string Evidence => Environment.GetEnvironmentVariable("BOE_AUDIO_EVIDENCE") ?? Path.Combine(Path.GetTempPath(), "boe-audio-evidence");
    public void Dispose()
    {
        if (_host != null && !_host.HasExited && _authority != null)
        { LinuxClipboardReaderIdentity.Stop(_authority); _host.WaitForExit(3000); }
        var settled = _host == null || _host.HasExited;
        var pid = _host?.Id;
        if (settled) { _authority?.Dispose(); _host?.Dispose(); if (Directory.Exists(Root)) Directory.Delete(Root, true); }
        Directory.CreateDirectory(Evidence);
        File.WriteAllText(Path.Combine(Evidence, Path.GetFileName(Root) + "-cleanup.json"), JsonSerializer.Serialize(new
        { FixtureRoot = Root, HostPid = pid, Removed = !Directory.Exists(Root), OwnedHostCleanupConfirmed = settled,
            TimeoutStoppedByOriginalPidfd = _timeoutStopped, NumericSignals = 0 }));
    }
}
