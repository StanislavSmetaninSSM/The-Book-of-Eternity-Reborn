using System.Diagnostics;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Services.GmWorkers;

namespace BookOfEternityClient.Services.GmRuntime;

// Configuration is a value, never launch authority. Validation has no process,
// canonical recovery, configured-directory creation or compilation side effects.
internal sealed record ProductionMainConfiguration(string Supervisor,string Shell,string Command,string Cwd,TerminalSize Size)
{
    internal static ProductionMainConfiguration Resolve(GameSettings settings,string session,TerminalSize size,string? package=null)
    {
        if(!OperatingSystem.IsLinux() || !settings.GmBridgeEnabled || !string.Equals(settings.GmBridgeBackend,"OwnedTerminal",StringComparison.OrdinalIgnoreCase))
            throw new PlatformNotSupportedException("Linux main requires the portable OwnedTerminal transport.");
        if(settings.GmMainOwnerBackend!="NativeLineage")
            throw new PlatformNotSupportedException("Explicit NativeLineage is required; Auto/SystemdUser are not qualified here.");
        if(string.IsNullOrWhiteSpace(settings.GmCliLaunchCommand))throw new InvalidDataException("Configured persistent CLI command is empty.");
        var cwd=string.IsNullOrWhiteSpace(settings.GmBridgeShellWorkingDirectory)?session:
            Path.GetFullPath(settings.GmBridgeShellWorkingDirectory,session);
        if(!Directory.Exists(cwd))throw new DirectoryNotFoundException("Configured CLI working directory must already exist.");
        var shell=(Environment.GetEnvironmentVariable("PATH")??"").Split(Path.PathSeparator)
            .Where(p=>!string.IsNullOrEmpty(p)).Select(p=>Path.GetFullPath(Path.Combine(p,"pwsh"))).FirstOrDefault(File.Exists)
            ??throw new FileNotFoundException("PowerShell 7 is required for the configured command.");
        return new(GmWorkerNativePackage.Validate(package),shell,settings.GmCliLaunchCommand,cwd,size);
    }
    internal ProcessStartInfo CreateStart()
    {
        var start=new ProcessStartInfo(Shell){UseShellExecute=false,WorkingDirectory=Cwd};
        foreach(var arg in new[]{"-NoLogo","-NoProfile","-Command",Command})start.ArgumentList.Add(arg);
        return start;
    }
}

// Bound to the actual original Prepared coordinator. Neither a profile value,
// decoded identity, status, PID nor a neutral/worker permit can consume this.
internal sealed class ProductionMainLaunch(GmSessionRunCoordinator owner,ProductionMainConfiguration configuration)
{
    private int _consumed;
    internal ProductionMainConfiguration Configuration=>configuration;
    internal void Consume()
    {
        owner.ValidateProductionPrepared();
        if(Interlocked.Exchange(ref _consumed,1)!=0)throw new InvalidOperationException("Original production launch was already consumed.");
    }
}
