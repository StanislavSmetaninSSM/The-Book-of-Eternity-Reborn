using System.Diagnostics;
using System.Text.Json;
using Xunit;
namespace BookOfEternityClient.Tests;

internal sealed class AudioLinuxFixture : IDisposable
{
    internal readonly string Root = Path.Combine(Path.GetTempPath(), "boe-audio-" + Guid.NewGuid().ToString("N"));
    private Process? _host;
    internal async Task<JsonElement> Run(string mode)
    {
        Assert.True(OperatingSystem.IsLinux()); Directory.CreateDirectory(Root);
        var assembly = typeof(AudioLinuxFixture).Assembly.Location;
        var executable = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? Environment.ProcessPath!;
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in new[] { "exec", "--runtimeconfig", Path.ChangeExtension(assembly, ".runtimeconfig.json"),
            "--depsfile", Path.ChangeExtension(assembly, ".deps.json"), typeof(PortableStorageCrashHost.Program).Assembly.Location,
            Root, "synthetic", "audio", mode }) start.ArgumentList.Add(arg);
        _host = Process.Start(start)!;
        var stdout = _host.StandardOutput.ReadToEndAsync(); var stderr = _host.StandardError.ReadToEndAsync();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await _host.WaitForExitAsync(deadline.Token);
        Assert.True(_host.ExitCode == 0, "Fixture preparation/execution: " + await stdout + await stderr);
        var report = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, "probe.json"))).RootElement.Clone();
        Directory.CreateDirectory(Evidence);
        File.WriteAllText(Path.Combine(Evidence, Path.GetFileName(Root) + ".json"), JsonSerializer.Serialize(new { Report = report, HostExit = _host.ExitCode }));
        return report;
    }
    private static string Evidence => Environment.GetEnvironmentVariable("BOE_AUDIO_EVIDENCE") ?? Path.Combine(Path.GetTempPath(), "boe-audio-evidence");
    public void Dispose()
    {
        var settled = _host == null || _host.HasExited;
        if (settled) { _host?.Dispose(); if (Directory.Exists(Root)) Directory.Delete(Root, true); }
        Directory.CreateDirectory(Evidence);
        File.WriteAllText(Path.Combine(Evidence, Path.GetFileName(Root) + "-cleanup.json"), JsonSerializer.Serialize(new
        { Removed = !Directory.Exists(Root), OwnedHostCleanupConfirmed = settled }));
    }
}
