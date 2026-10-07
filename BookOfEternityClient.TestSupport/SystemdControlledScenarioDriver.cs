using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services.GmRuntime;
using BookOfEternityClient.Services.GmWorkers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Win32.SafeHandles;

namespace BookOfEternityClient.Tests;

internal static class SystemdControlledScenarioDriver
{
    internal static async Task<int> RunAsync(string mode,string package,string folder)
    {
        var launch=NeutralTerminalLaunch.Create(package,folder);
        var files=new FileSystemManager(Directory.GetParent(launch.Scratch)!.FullName,NullLogger<FileSystemManager>.Instance);
        files.EnsureDirectoryStructure();
        var owner=await GmSessionRunCoordinator.OpenNeutralAsync(files);
        var bus=new ControlledBus();var cgroup=new ControlledCgroup();IOwnedTerminalSession? terminal=null;int held=0;
        var facts=new Dictionary<string,object?> { ["Mode"]=mode };
        try {
            terminal=await owner.LaunchSystemdControlledAsync(launch,new(bus,cgroup),CancellationToken.None,p=>held=p);
            facts["Success"]=bus.StartCount==1 && held>0 && terminal.Identity.Backend=="systemd-user" && owner.Record?.Disposition==GmSessionRunDisposition.Running;
        } catch(OwnedTerminalStartException ex) { terminal=ex.Owner;facts["Failure"]=ex.InnerException?.ToString();facts["Success"]=false; }
        finally {
            // Own physical cleanup is deliberately separate from logical success.
            if(terminal!=null) {
                var drain=Task.Run(async()=>{var b=new byte[1024];while(await terminal.OutputReader.ReadAsync(b)!=0){} });
                await terminal.StopAndObserveAsync(CancellationToken.None);await drain.WaitAsync(TimeSpan.FromSeconds(5));
            }
            facts["ActualHeldPid"]=held;facts["StartCount"]=bus.StartCount;
            await File.WriteAllTextAsync(Path.Combine(folder,"scenario.json"),JsonSerializer.Serialize(facts));
        }
        return (bool)facts["Success"]!?0:1;
    }
    internal sealed class ControlledBus : ISystemdBusTransport
    {
        public SystemdManagerBinding Manager {get;}=new("controlled-bus",":1.42",0,Guid.NewGuid().ToString("D"));
        public Task<string> AuthorityLost {get;}=new TaskCompletionSource<string>().Task;
        internal int StartCount;
        public Task SubscribeAsync(CancellationToken token)=>Task.CompletedTask;
        public Task StartScopeAsync(SystemdScopeRequest request,CancellationToken token){StartCount++;return Task.CompletedTask;}
        public Task<SystemdUnitSnapshot> ObserveAsync(string name,SafeFileHandle? fd,CancellationToken token)=>Task.FromResult(new SystemdUnitSnapshot("/org/freedesktop/systemd1/unit/controlled",new string('a',32),"/controlled/"+name,"/org/freedesktop/systemd1/unit/controlled"));
        public Task StopScopeAsync(string name,CancellationToken token)=>Task.CompletedTask;
        public ValueTask DisposeAsync()=>ValueTask.CompletedTask;
    }
    internal sealed class ControlledCgroup : ISystemdCgroupSource
    {
        public void BindOriginal(SystemdUnitSnapshot unit,int pid) { }
        public void ValidateOriginal() { }
        public bool ReadFreshEmpty()=>true;
        public void Dispose() { }
    }
}
