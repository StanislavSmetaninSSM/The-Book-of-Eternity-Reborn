using BookOfEternityClient.Core;
using BookOfEternityClient.Services.GmRuntime;
using BookOfEternityClient.Services.GmWorkers;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookOfEternityGMBridge;

internal sealed partial class BridgeHost
{
    private async Task StartWindowsShellAsync(string? expectedGeneration)
    {
        if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException("Windows original shell requires Windows.");
        var config=LoadBridgeConfig();
        var shellExe=ResolveWindowsShellExecutable(Environment.GetEnvironmentVariable("PATH"),
            Environment.GetFolderPath(Environment.SpecialFolder.System));
        var shellArgs=BuildShellArguments(shellExe);
        var workingDirectory=ResolveGmBridgeShellWorkingDirectory(config.GmBridgeShellWorkingDirectory);
        var (width,height)=GetConsoleSize();
        _terminalLaunchSize=new(width,height);
        // The foreground probe settles before any main guard/Prepared or ConPTY.
        var observation=await ReadStartupObservationAsync();
        _windowsProductionConfig=config;
        _windowsProductionConfig.GmCliInputProfile=config.GmCliInputProfile.Snapshot();
        _neutralFiles=new FileSystemManager(_clientRoot,NullLogger<FileSystemManager>.Instance);
        IOwnedTerminalSession session;
        try {
            _mainRun=await GmSessionRunCoordinator.OpenWindowsProductionAsync(_neutralFiles,observation,ObserveMainMetadata);
            session=await _mainRun.LaunchWindowsPreparedAsync(runId=> {
                var original=ConPtySession.Prepare(shellExe,shellArgs,workingDirectory,width,height,runId);
                return Task.FromResult(new OwnedTerminalSessionFactory.PreparedTerminal(original,token=> {
                    token.ThrowIfCancellationRequested();
                    original.ReleaseOriginal();
                    return Task.CompletedTask;
                }));
            },_cts.Token,expectedGeneration);
        }
        catch(OwnedTerminalStartException failure) {
            AttachOwnedTerminalCore(failure.Owner,Console.OpenStandardOutput(),false);
            MarkTerminalUncertain();throw;
        }
        catch {
            if(_mainRun?.RetainsAuthority==true)MarkTerminalUncertain();
            else {_mainRun=null;_windowsProductionConfig=null;}
            throw;
        }
        lock(_sync) {
            _status.ShellPid=session.Identity.RootPid;
            _status.CliProcessId=null;
            _status.CliLaunchCommand=config.GmCliLaunchCommand;
            _status.ShellWorkingDirectory=workingDirectory;
            _status.WorkerStatuses=GmWorkerBridgePool.BuildInitialStatuses(config.GmWorkerBridgeProfiles).ToList();
            _status.Ready=false;_status.State="OperatorNotReady";_status.LastError=null;
        }
        OpenOriginalStatusPublication();
        var input=AttachOwnedTerminal(session,Console.OpenStandardOutput());
        await _firstStatus.Task;
        Console.WriteLine();
        Console.WriteLine($"[Bridge] Hosted PTY shell started (pid={session.Identity.RootPid}).");
        Console.WriteLine($"[Bridge] Working directory: {workingDirectory}");
        Console.WriteLine($"[Bridge] Shell command: {shellExe} {shellArgs}");
        if(string.IsNullOrWhiteSpace(config.GmCliLaunchCommand))
            Console.WriteLine("[Bridge] GmCliLaunchCommand is empty. Type your CLI launch command manually, then mark bridge ready.");
        else {
            Console.WriteLine($"[Bridge] Launch command: {config.GmCliLaunchCommand}");
            var bootstrap=BuildShellBootstrap(config.GmCliLaunchCommand);
            await Task.Delay(250,input.Token);
            await WriteShellBootstrapAsync(input,bootstrap,input.Token);
        }
    }

    // This only resolves configuration. Executable health is established by the
    // original suspended ConPTY preparation; no fallback after that decision.
    internal static string ResolveWindowsShellExecutable(string? searchPath,string systemDirectory)
    {
        foreach(var entry in (searchPath??"").Split(Path.PathSeparator)) {
            var directory=entry.Trim().Trim('"');
            if(!Path.IsPathFullyQualified(directory))continue;
            var candidate=Path.GetFullPath(Path.Combine(directory,"pwsh.exe"));
            if(File.Exists(candidate))return candidate;
        }
        if(Path.IsPathFullyQualified(systemDirectory)) {
            var fallback=Path.GetFullPath(Path.Combine(systemDirectory,"WindowsPowerShell","v1.0","powershell.exe"));
            if(File.Exists(fallback))return fallback;
        }
        throw new FileNotFoundException("A resolved PowerShell shell is required for original Windows preparation.");
    }
}
