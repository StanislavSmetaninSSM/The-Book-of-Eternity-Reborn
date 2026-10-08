using System.Diagnostics;
using System.Text.Json;
using Xunit;

namespace BookOfEternityClient.Tests;

internal sealed class ClipboardLinuxFixture : IDisposable
{
    internal readonly string Root = Path.Combine(Path.GetTempPath(), "boe-clipboard-" + Guid.NewGuid().ToString("N"));
    private readonly List<string> _cases = [];
    private Process? _host;
    internal ClipboardLinuxFixture()
    {
        Assert.True(OperatingSystem.IsLinux(), "This synthetic executable fixture qualifies Linux only.");
        Directory.CreateDirectory(Path.Combine(Root, "bin"));
    }

    internal async Task<JsonElement> Run(ClipboardConsoleProbe.Request request, string? wayland = "fixture", string? display = null,
        string[]? tools = null)
    {
        _cases.Add(request.Mode + ":" + request.Provider + ":" + request.ReaderMode);
        File.WriteAllText(Path.Combine(Root, "request.json"), JsonSerializer.Serialize(request));
        foreach (var tool in tools ?? ["wl-paste"])
        {
            var path = Path.Combine(Root, "bin", tool);
            File.WriteAllText(path, request.ReaderMode == "bad-start" && tool == "wl-paste" ? "#!/nonexistent-boe-synthetic-reader\n" : "#!/usr/bin/python3\n" + Reader);
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
        var assembly = typeof(ClipboardLinuxFixture).Assembly.Location;
        var host = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? Environment.ProcessPath!;
        Assert.True(Path.IsPathFullyQualified(host) && File.Exists(host), "An absolute existing .NET host is required.");
        var start = new ProcessStartInfo(host) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in new[] { "exec", "--runtimeconfig", Path.ChangeExtension(assembly, ".runtimeconfig.json"),
            "--depsfile", Path.ChangeExtension(assembly, ".deps.json"), typeof(PortableStorageCrashHost.Program).Assembly.Location,
            Root, "synthetic", "clipboard", request.Mode }) start.ArgumentList.Add(arg);
        start.Environment["PATH"] = Path.Combine(Root, "bin");
        start.Environment.Remove("WAYLAND_DISPLAY"); start.Environment.Remove("DISPLAY");
        if (wayland != null) start.Environment["WAYLAND_DISPLAY"] = wayland;
        if (display != null) start.Environment["DISPLAY"] = display;
        var process = _host = Process.Start(start) ?? throw new InvalidOperationException("Owned clipboard probe did not start.");
        var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        // The host has its own independent12s exit timer. Parent sends no numeric PID signals.
        await process.WaitForExitAsync(deadline.Token);
        Assert.True(process.ExitCode == 0, "Probe preparation/execution failed: " + await stdout + await stderr);
        var calls = ReadCalls();
        foreach (var call in calls)
        {
            var stat = "/proc/" + call.GetProperty("Pid").GetInt32() + "/stat";
            Assert.False(File.Exists(stat) && StatIdentity(File.ReadAllText(stat)) == call.GetProperty("Identity").GetString(),
                "Original owned reader identity is still alive/unreaped.");
        }
        var report = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, "probe.json"))).RootElement.Clone();
        var evidence = Environment.GetEnvironmentVariable("BOE_CLIPBOARD_EVIDENCE") ?? Path.Combine(Path.GetTempPath(), "boe-clipboard-evidence");
        Directory.CreateDirectory(evidence);
        File.WriteAllText(Path.Combine(evidence, Path.GetFileName(Root) + ".json"), JsonSerializer.Serialize(new
        {
            FixtureRoot = Root, Report = report, Calls = calls, HostExit = process.ExitCode,
            ReaderIdentitiesRemaining = 0, SyntheticOnly = true, Tools = tools ?? ["wl-paste"]
        }));
        return report;
    }

    internal JsonElement[] ReadCalls() => File.Exists(Path.Combine(Root, "calls.jsonl"))
        ? File.ReadAllLines(Path.Combine(Root, "calls.jsonl")).Select(s => JsonDocument.Parse(s).RootElement.Clone()).ToArray() : [];

    private static string StatIdentity(string stat) => stat[(stat.LastIndexOf(')') + 2)..].Split(' ')[19];

    public void Dispose()
    {
        var settled = _host == null || _host.HasExited;
        if (settled)
        {
            _host?.Dispose();
            if (Directory.Exists(Root)) Directory.Delete(Root, true);
        }
        // On failure retain the original host reference/root and report debt, never claim cleanup.
        var evidence = Environment.GetEnvironmentVariable("BOE_CLIPBOARD_EVIDENCE") ?? Path.Combine(Path.GetTempPath(), "boe-clipboard-evidence");
        Directory.CreateDirectory(evidence);
        File.WriteAllText(Path.Combine(evidence, Path.GetFileName(Root) + "-cleanup.json"), JsonSerializer.Serialize(new
        {
            FixtureRoot = Root, Removed = !Directory.Exists(Root), OwnedHostCleanupConfirmed = settled, Cases = _cases
        }));
    }

    private const string Reader = """
import os,sys,json,signal,time
signal.alarm(8)
root=os.path.dirname(os.path.dirname(os.path.abspath(sys.argv[0])))
request=json.load(open(os.path.join(root,'request.json'),encoding='utf-8'))
stat=open('/proc/self/stat').read()
with open(os.path.join(root,'calls.jsonl'),'a',encoding='utf-8') as f:
    f.write(json.dumps({'Pid':os.getpid(),'Identity':stat[stat.rfind(')')+2:].split()[19],'Tool':os.path.basename(sys.argv[0]),'Args':sys.argv[1:]})+'\n')
mode=request['ReaderMode']
if mode=='timeout':time.sleep(7)
if mode=='capture-debt':time.sleep(4.5)
if mode=='error':sys.stderr.write('synthetic private data must not be displayed');sys.exit(4)
if mode=='invalid':sys.stdout.buffer.write(b'\xff');sys.exit(0)
if mode=='stdout-flood':sys.stdout.buffer.write(b'x'*(1024*1024+1));sys.stdout.flush();time.sleep(7)
if mode=='stderr-flood':sys.stderr.buffer.write(b'x'*(64*1024+1));sys.stderr.flush();time.sleep(7)
if mode=='stderr-limit':sys.stderr.buffer.write(b'x'*(64*1024));sys.stderr.flush()
sys.stdout.buffer.write(request['Text'].encode('utf-8'))
""";
}
