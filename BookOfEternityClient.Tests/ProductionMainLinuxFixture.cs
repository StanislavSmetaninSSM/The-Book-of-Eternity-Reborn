using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using BookOfEternityClient.Services.GmRuntime;
using BookOfEternityClient.Services.GmWorkers;
namespace BookOfEternityClient.Tests;
internal static class ProductionMainLinuxFixture
{
    internal static async Task RunAsync(string mode)
    {
        Assert.True(OperatingSystem.IsLinux(),"M1 requires actual Linux execution.");
        var repo=TestRepoPaths.RepoRoot;var folder=Path.Combine(repo,"TestResults/production-main",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
        var package=Path.Combine(folder,"package");var ship=Path.Combine(folder,"ship");var root=Path.Combine(folder,"root");Directory.CreateDirectory(root);
        async Task Prepare(string exe,string[] args,string name,int seconds=50)
        {
            var start=new ProcessStartInfo(exe){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};foreach(var a in args)start.ArgumentList.Add(a);
            using var p=Process.Start(start)!;var log=await LinuxFallbackSupervisorTests.NativeRun.ObserveBuild(p,TimeSpan.FromSeconds(seconds),Path.Combine(folder,name+".log"));
            Assert.True(p.ExitCode==0,"Preparation failure, not causal RED: "+log);
        }
        await Prepare("pwsh",["-NoProfile","-File",Path.Combine(repo,"scripts/build-linux-supervisor.ps1"),"-OutputDirectory",package,"-IncludeHostGuardian"],"helper-build");
        var fixture=Path.Combine(package,"configured-neutral-cli");var source=Path.Combine(repo,"tests/fixtures/ProductionMain/configured-neutral-cli.c");
        await Prepare("cc",["-std=c11","-O2","-g","-Wall","-Wextra","-Werror",source,"-o",fixture],"fixture-build");
        foreach(var name in new[]{"BookOfEternityClient","BookOfEternityGMBridge"}.Concat(mode.StartsWith("production-main-",StringComparison.Ordinal)?new[]{"BookOfEternityClient.TestSupport"}:Array.Empty<string>()))
            await Prepare("dotnet",["publish",Path.Combine(repo,name,name+".csproj"),"--no-build","--no-restore","-c",new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name,"-o",Path.Combine(ship,name),"-p:BoeNativePackageDirectory="+package,"-p:BoeRequireNativePackage=true"],"publish-"+name);
        if(mode=="package-layout") {
            Assert.True(File.Exists(Path.Combine(ship,"BookOfEternityClient/Launcher/bookofeternity.ps1")),"Causal RED: ordinary published layout lacks its real launcher.");
            Assert.True(File.Exists(Path.Combine(ship,"BookOfEternityClient/Launcher/gm_main_operation.ps1")),"Causal RED: ordinary published layout lacks participating helper.");return;
        }
        Directory.CreateDirectory(Path.Combine(ship,"BookOfEternityClient/Launcher"));
        foreach(var f in Directory.GetFiles(Path.Combine(repo,"BookOfEternityClient/Launcher"),"*.ps1"))File.Copy(f,Path.Combine(ship,"BookOfEternityClient/Launcher",Path.GetFileName(f)),true);
        Assert.Empty(Directory.GetFiles(ship,"*.cs",SearchOption.AllDirectories));Assert.Empty(Directory.GetFiles(ship,"*.csproj",SearchOption.AllDirectories));
        var files=new FileSystemManager(root,NullLogger<FileSystemManager>.Instance);files.EnsureDirectoryStructure();
        var cwd=Path.Combine(root,"work Ж");Directory.CreateDirectory(cwd);
        var command="& '"+fixture.Replace("'","''")+"' '--model' 'gm-model-sentinel' '--arg' 'a b'";
        var settings=new GameSettings{GmBridgeBackend="OwnedTerminal",GmCliLaunchCommand=command,GmBridgeShellWorkingDirectory=cwd,GmBridgeAutoStart=false,
            GmCliInputProfile=new(){IdleMarker="NEUTRAL READY",PromptPrefix="> ",WorkingMarker="NEUTRAL WORKING",ObservationTimeoutMilliseconds=1800}};
        var config=JsonSerializer.SerializeToUtf8Bytes(settings);File.WriteAllBytes(Path.Combine(files.GameSessionPath,"config.json"),config);
        var initialGeneration=await new StateManager(files,settings,NullLogger<StateManager>.Instance).BootstrapLocalStorageAsync();
        // Proposed setting is carried as ordinary profile JSON before the property exists.
        var json=JsonSerializer.Deserialize<Dictionary<string,JsonElement>>(File.ReadAllText(Path.Combine(files.GameSessionPath,"config.json")))!;
        json["GmMainOwnerBackend"]=JsonSerializer.SerializeToElement("NativeLineage");File.WriteAllText(Path.Combine(files.GameSessionPath,"config.json"),JsonSerializer.Serialize(json));
        if(mode=="refuse-worker") {
            await using var ledger=await GmWorkerRunLedger.OpenCoordinatorAsync(new(root));Assert.NotNull(ledger);
            Assert.Equal(WorkerLedgerMutationKind.Applied,await ledger.InitializeAsync());
            var generation=initialGeneration;
            Assert.Equal(WorkerLedgerMutationKind.Applied,(await ledger.PrepareAsync(new(generation,"inert_worker","inert_task",new string('a',64),WorkerRunBackend.LinuxNativeLineage,WorkerRunScope.OrdinarySamePidNamespace,files.GameSessionPath),ledger.Sequence)).Kind);
        }
        if(mode=="refuse-storage") { var evidence=Path.Combine(files.RuntimeRootPath,"load-transactions/unknown-journal");Directory.CreateDirectory(Path.GetDirectoryName(evidence)!);File.WriteAllBytes(evidence,[0xfe,0]); }
        File.WriteAllText(Path.Combine(folder,"fixture-preparation.json"),JsonSerializer.Serialize(new{Source=source,SourceSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(source))).ToLowerInvariant(),Binary=fixture,BinarySha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(fixture))).ToLowerInvariant(),Command=command,Cwd=cwd,ShippedSourceFiles=0,PublisherOnlyCompile=true}));
        var start=new ProcessStartInfo(Path.Combine(package,"host-guardian")){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};
        var scenarioArgs=mode.StartsWith("production-main-",StringComparison.Ordinal)?new[]{Path.Combine(Environment.GetEnvironmentVariable("DOTNET_ROOT")!,"dotnet"),Path.Combine(ship,"BookOfEternityClient.TestSupport/BookOfEternityClient.TestSupport.dll"),mode,package,folder}:new[]{"/usr/bin/python3",Path.Combine(repo,"tests/fixtures/ProductionMain/ordinary.py"),mode,folder,ship,files.GameSessionPath};
        foreach(var a in new[]{Path.Combine(folder,"guardian.json"),"30000"}.Concat(scenarioArgs))start.ArgumentList.Add(a);
        using var guardian=Process.Start(start)!;await LinuxFallbackSupervisorTests.NativeRun.ObserveBuild(guardian,TimeSpan.FromSeconds(35),Path.Combine(folder,"guardian.log"));
        using var report=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(folder,"guardian.json")));
        Assert.True(report.RootElement.GetProperty("echild").GetBoolean());Assert.Equal(0,report.RootElement.GetProperty("emergencySignals").GetInt32());Assert.Equal(0,report.RootElement.GetProperty("failures").GetInt32());Assert.False(report.RootElement.GetProperty("deadline").GetBoolean());
        using var result=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(folder,"scenario.json")));
        Assert.True(result.RootElement.GetProperty("Success").GetBoolean(),result.RootElement.ToString());Assert.Equal(0,guardian.ExitCode);
    }
}
