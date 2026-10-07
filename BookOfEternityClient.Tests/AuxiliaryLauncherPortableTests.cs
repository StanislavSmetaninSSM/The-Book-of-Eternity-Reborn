using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.Services.GmRuntime;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class AuxiliaryLauncherPortableTests
{
    [Fact]
    public async Task RelocatedRuntimeOnlyPackage_ActualPrepareTurnPublishesLinkedAuthority()
    {
        await using var f = await AuxiliaryPackageFixture.Create();
        var config = File.ReadAllBytes(Path.Combine(f.Files.GameSessionPath, "config.json"));
        var generation = File.ReadAllBytes(f.Files.SessionGenerationPath);
        var result = await f.Run("valid");
        Assert.True(result.Exit == 0, "Causal route failure after successful package preparation: " + result.Output + result.Error);
        var request = JsonDocument.Parse(File.ReadAllText(f.Files.ResolvePath(LiveTurnPreparationService.TurnRequestPath))).RootElement;
        Assert.Equal("action Ж😀 'quoted'\nsecond line", request.GetProperty("playerAction").GetString());
        Assert.Equal("fixture-session", request.GetProperty("sessionId").GetString());
        Assert.Equal("fixture-request", request.GetProperty("requestId").GetString());
        Assert.Equal(7, request.GetProperty("turnNumber").GetInt32());
        Assert.Equal(new[] { 14, 8, 17 }, request.GetProperty("preGeneratedDices1d20").EnumerateArray().Select(x => x.GetInt32()));
        Assert.Equal("Mortal World", request.GetProperty("currentRealm").GetString());
        var manifest = JsonSerializer.Deserialize<LiveTurnPendingSnapshotManifest>(File.ReadAllText(f.Files.ResolvePath(LiveTurnPreparationService.PendingTurnSnapshotManifestPath)), LiveTurnPreparationService.ManifestJsonOptions)!;
        Assert.True(PendingTurnSnapshotAuthority.TryValidateManifestForReaderAuthority(manifest,
            File.ReadAllText(f.Files.ResolvePath(PendingTurnSnapshotAuthority.AuthorityPath)), LiveTurnPreparationService.ManifestHashJsonOptions,
            m => m.ManifestPayloadHash, (m,h) => m.ManifestPayloadHash=h, m => m.SessionId, m => m.RequestId, m => m.TurnNumber,
            m => m.Files, m => m.SnapshotFileHashes, m => m.ClientOwnedValidationHashes, m => m.RollbackBaselineFiles,
            m => m.SourceLabel, m => m.RollbackBackups, p => File.Exists(f.Files.ResolvePath(p)) ? File.ReadAllBytes(f.Files.ResolvePath(p)) : null,
            out _, out var failure), failure);
        Assert.Equal(config, File.ReadAllBytes(Path.Combine(f.Files.GameSessionPath, "config.json")));
        Assert.Equal(generation, File.ReadAllBytes(f.Files.SessionGenerationPath));
        Assert.Contains("fixture-request", result.Output);
    }
    [Theory]
    [InlineData("dll", "BookOfEternityClient.dll")]
    [InlineData("deps", "BookOfEternityClient.deps.json")]
    [InlineData("runtimeconfig", "BookOfEternityClient.runtimeconfig.json")]
    [InlineData("helper", "gm_main_operation.ps1")]
    [InlineData("dotnet", "dotnet executable is unavailable")]
    [InlineData("netcore", "Microsoft.NETCore.App 8")]
    [InlineData("aspnet", "Microsoft.AspNetCore.App 8")]
    [InlineData("action", "--action is required")]
    [InlineData("dice", "--dice must contain integers")]
    [InlineData("main", "prepare-live-turn failed")]
    [InlineData("worker", "prepare-live-turn failed")]
    public Task MissingCapabilityOrAuthority_RealRouteRefusesWithoutCanonicalChanges(string mode,string diagnostic)=>RunRefusal(mode,diagnostic);

    [Fact]
    public Task StorageDebt_RealRouteRetainsTypedPreparationErrorAndExactBytes()=>RunRefusal("storage","prepare-live-turn failed");

    private static async Task RunRefusal(string mode,string diagnostic)
    {
        await using var f=await AuxiliaryPackageFixture.Create();
        var file=mode switch {"dll"=>"BookOfEternityClient.dll","deps"=>"BookOfEternityClient.deps.json","runtimeconfig"=>"BookOfEternityClient.runtimeconfig.json","helper"=>"Launcher/gm_main_operation.ps1",_=>null};
        if(file!=null)File.Move(Path.Combine(f.Package,file),Path.Combine(f.Root,"removed-resource"));
        if(mode=="dotnet")File.Move(Path.Combine(f.Runtime,"dotnet"),Path.Combine(f.Root,"removed-dotnet"));
        if(mode is "netcore" or "aspnet")Directory.Move(Path.Combine(f.Runtime,"shared",mode=="netcore"?"Microsoft.NETCore.App":"Microsoft.AspNetCore.App"),Path.Combine(f.Root,"removed-framework"));
        string? debt=null;
        if(mode=="main")
        {
            var identity=new GmSessionRunIdentity(f.Files.BasePath,Guid.NewGuid().ToString("N"),Guid.Empty.ToString("N"),1,GmSessionRunBackend.LinuxSupervisor,Guid.NewGuid().ToString("N"),"fixture-boot");
            debt=Path.Combine(f.Files.BasePath,".boe_runtime/gm-runs/main.json");Directory.CreateDirectory(Path.GetDirectoryName(debt)!);
            File.WriteAllBytes(debt,GmSessionRunRecordCodec.Encode(new(1,identity,GmSessionRunDisposition.Running,null)));
        }
        if(mode is "worker" or "storage")
        {
            debt=Path.Combine(f.Files.BasePath,".boe_runtime",mode=="worker"?"worker-runs-v1/state.json":"trusted-local-publication-v1/active.json");
            Directory.CreateDirectory(Path.GetDirectoryName(debt)!);File.WriteAllText(debt,"{");
        }
        var debtBytes=debt==null?null:File.ReadAllBytes(debt);
        var before=AuxiliaryLauncherRootBindingTests.Snapshot(f.Files.GameSessionPath);
        var args=mode switch {"action"=>new[]{"--dice","14,8,17"},"dice"=>new[]{"--action","test","--dice","21"},_=>null};
        var result=await f.Run(mode,arguments:args);
        Assert.NotEqual(0,result.Exit);Assert.Contains(diagnostic,result.Output+result.Error,StringComparison.Ordinal);
        if(mode=="storage")
        {
            Assert.DoesNotContain("Unhandled exception",result.Error);
            Assert.Contains("Unknown publication format; evidence retained.",result.Error);
            Assert.Contains("prepare-turn failed with exit code 2",result.Error);
        }
        Assert.Equal(before,AuxiliaryLauncherRootBindingTests.Snapshot(f.Files.GameSessionPath));
        if(debt!=null)Assert.Equal(debtBytes,File.ReadAllBytes(debt));
        Assert.False(File.Exists(f.Files.ResolvePath(LiveTurnPreparationService.TurnRequestPath)));
    }

    [Fact]
    public async Task ExplicitSecondPreparation_ReplacesPendingSnapshotWithinExistingContract()
    {
        await using var f=await AuxiliaryPackageFixture.Create();
        Assert.Equal(0,(await f.Run("first")).Exit);
        var result=await f.Run("second",arguments:["--action","second explicit action", "--session-id","fixture-session","--request-id","second-request","--turn-number","8","--dice","1,2,3"]);
        Assert.Equal(0,result.Exit);
        var manifest=JsonDocument.Parse(File.ReadAllText(f.Files.ResolvePath(LiveTurnPreparationService.PendingTurnSnapshotManifestPath))).RootElement;
        Assert.Equal("second-request",manifest.GetProperty("requestId").GetString());
        var request=JsonDocument.Parse(File.ReadAllText(f.Files.ResolvePath(LiveTurnPreparationService.TurnRequestPath))).RootElement;
        Assert.Equal("second-request",request.GetProperty("requestId").GetString());Assert.Equal(8,request.GetProperty("turnNumber").GetInt32());
        Assert.True(File.Exists(f.Files.ResolvePath(PendingTurnSnapshotAuthority.AuthorityPath)));
    }

    [Fact]
    public async Task OrdinaryPositionalStatusSelector_PreservesExistingDispatcherBoundary()
    {
        await using var f=await AuxiliaryPackageFixture.Create();
        var before=AuxiliaryLauncherRootBindingTests.Snapshot(f.Files.GameSessionPath);
        var result=await f.Run("ordinary-status",arguments:[],command:"status");
        Assert.NotEqual(0,result.Exit);Assert.Contains("GM bridge status file not found",result.Output+result.Error);
        Assert.DoesNotContain("Usage:",result.Output);Assert.Equal(before,AuxiliaryLauncherRootBindingTests.Snapshot(f.Files.GameSessionPath));
    }

    [Fact]
    public void OperationalPrepareInstructions_UseShippedLayoutAndCurrentBoundedLinks()
    {
        var docs=File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot,"BookOfEternityClient/Launcher/CLI_Daemon_Quickstart.md"));
        var section=docs[docs.IndexOf("## Подготовка следующего",StringComparison.Ordinal)..docs.IndexOf("## Самая короткая версия",StringComparison.Ordinal)];
        Assert.Contains("PowerShell 7",section);Assert.Contains("Microsoft.AspNetCore.App 8",section);
        Assert.Contains("BookOfEternityClient.dll",section);Assert.Contains("/opt/boe/",section);Assert.Contains("C:\\Games",section);
        Assert.DoesNotContain("dotnet run",section);Assert.DoesNotContain("Debug",section);
        Assert.Contains("prepare-turn --action",section);Assert.Contains("pending_turn_snapshot.authority.json",section);
        Assert.Contains("legacy Windows/source",docs);
    }

    [Fact]
    public async Task ObservationTimeout_RetainsOriginalGuardianUntilActualExclusiveReap()
    {
        var f=await AuxiliaryPackageFixture.Create();
        try
        {
            await Assert.ThrowsAsync<TimeoutException>(()=>f.RunObservationTimeout());
            var report=f.LastCleanup;
            Assert.True(report.GetProperty("echild").GetBoolean());Assert.True(report.GetProperty("deadline").GetBoolean());
            Assert.Equal(0,report.GetProperty("failures").GetInt32());Assert.Equal(1,report.GetProperty("emergencySignals").GetInt32());
            Assert.True(report.GetProperty("reaped").GetInt32()>=1);
        }
        finally {await f.DisposeAsync();}
        Assert.False(Directory.Exists(f.Root),"Root removal requires actual exclusive cleanup, never observation timeout alone.");
    }

}

internal sealed class AuxiliaryPackageFixture : IAsyncDisposable
{
    internal readonly string Root = Path.Combine(Path.GetTempPath(), "boe-auxiliary-" + Guid.NewGuid().ToString("N"));
    internal string Package => Path.Combine(Root, "relocated package Ж", "BookOfEternityClient");
    internal string Runtime => Path.Combine(Root, "player-bin");
    internal FileSystemManager Files = null!;
    private readonly List<JsonElement> _cleanups = [];
    private readonly HashSet<int> _unsettled = [];
    internal JsonElement LastCleanup=>_cleanups[^1];
    private int _sequence;
    private string Guardian => Path.Combine(Root, "guardian");
    private static string Evidence => Environment.GetEnvironmentVariable("BOE_AUXILIARY_EVIDENCE") ?? Path.Combine(Path.GetTempPath(), "boe-auxiliary-evidence");

    internal static async Task<AuxiliaryPackageFixture> Create()
    {
        Assert.True(OperatingSystem.IsLinux(), "Actual Linux qualification; native Windows remains separate.");
        var f = new AuxiliaryPackageFixture(); Directory.CreateDirectory(f.Root);
        try
        {
            var repo = TestRepoPaths.RepoRoot;
            var cc = Find("cc");
            await f.Prepare(cc, ["--version"], "compiler-version");
            await f.Prepare(cc, ["-std=c11", "-O2", "-Wall", "-Wextra", "-Werror", Path.Combine(repo, "tests/fixtures/LinuxHost/host-guardian.c"), "-o", f.Guardian], "guardian-build");
            var staging = Path.Combine(f.Root, "publisher-staging");
            await f.Prepare(Find("dotnet"), ["publish", Path.Combine(repo, "BookOfEternityClient/BookOfEternityClient.csproj"), "--no-build", "--no-restore", "-c", new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name, "-o", staging], "publish");
            Directory.CreateDirectory(Path.GetDirectoryName(f.Package)!); Directory.Move(staging, f.Package);
            Assert.Empty(Directory.GetFiles(f.Package, "*.cs", SearchOption.AllDirectories));
            Assert.Empty(Directory.GetFiles(f.Package, "*.csproj", SearchOption.AllDirectories));
            Assert.False(Directory.Exists(Path.Combine(f.Package, "bin")));
            foreach (var name in new[] { "Launcher/bookofeternity.ps1", "Launcher/gm_main_operation.ps1", "operational-resources/CLI_Agent_Daemon_Specification.md", "operational-resources/TaskGuides/CLI_Step_Main.txt", "operational-resources/Examples/E_CLI_Step_Main.txt", "operational-resources/OtherGuides/Effect_Materialization_Contract.md" })
            {
                var path=Path.Combine(f.Package,name); Assert.True(File.Exists(path), "Preparation package resource: " + name);
                var original=name.StartsWith("operational-resources/",StringComparison.Ordinal)?Path.Combine(repo,name["operational-resources/".Length..]):Path.Combine(repo,"BookOfEternityClient",name);
                Assert.Equal(File.ReadAllBytes(original),File.ReadAllBytes(path));
            }
            Directory.CreateDirectory(f.Runtime);
            var dotnet = Find("dotnet"); var dotnetRoot=Path.GetDirectoryName(dotnet)!;
            File.Copy(dotnet,Path.Combine(f.Runtime,"dotnet"));
            Directory.CreateSymbolicLink(Path.Combine(f.Runtime,"host"),Path.Combine(dotnetRoot,"host"));
            foreach(var framework in new[]{"Microsoft.NETCore.App","Microsoft.AspNetCore.App"})
            {
                var versions=Directory.GetDirectories(Path.Combine(dotnetRoot,"shared",framework)).Where(p=>Path.GetFileName(p).StartsWith("8.",StringComparison.Ordinal)).OrderBy(p=>Version.Parse(Path.GetFileName(p))).ToArray();
                Assert.NotEmpty(versions); var parent=Path.Combine(f.Runtime,"shared",framework);Directory.CreateDirectory(parent);
                Directory.CreateSymbolicLink(Path.Combine(parent,Path.GetFileName(versions[^1])),versions[^1]);
            }
            File.CreateSymbolicLink(Path.Combine(f.Runtime,"pwsh"),Find("pwsh"));
            f.Files=new FileSystemManager(Path.Combine(f.Root,"game root Ж"),NullLogger<FileSystemManager>.Instance); f.Files.EnsureDirectoryStructure();
            var settings=new GameSettings {GmCliLaunchCommand="configured sentinel --model unchanged-model --arg 'a b'",GmBridgeAutoStart=false,MusicEnabled=false,SoundEnabled=false};
            await new StateManager(f.Files,settings,NullLogger<StateManager>.Instance).BootstrapLocalStorageAsync();
            await f.Files.WriteFileAtomicAsync("game_state/meta/soul_state.json", "{\"currentRealm\":\"Mortal World\"}");
            var sdk=await f.Execute(Path.Combine(f.Runtime,"dotnet"),["--list-sdks"],"runtime-sdks"); Assert.Equal(0,sdk.Exit);Assert.True(string.IsNullOrWhiteSpace(sdk.Output),"Player runtime layout must contain no SDK.");
            return f;
        }
        catch { await f.DisposeAsync(); throw; }
    }

    internal async Task<(int Exit,string Output,string Error)> Run(string label,string? action=null,string[]? arguments=null,string command="prepare-turn")
    {
        var args=new List<string>{"-NoLogo","-NoProfile","-File",Path.Combine(Package,"Launcher/bookofeternity.ps1"),"-SessionPath",Files.GameSessionPath,command};
        args.AddRange(arguments??["--action",action??"action Ж😀 'quoted'\nsecond line","--session-id","fixture-session","--request-id","fixture-request","--turn-number","7","--dice","14,8,17","--current-realm","Mortal World"]);
        return await Execute(Find("pwsh"),args.ToArray(),label);
    }
    internal Task<(int Exit,string Output,string Error)> RunClient(string label,string[] arguments)=>Execute(Path.Combine(Runtime,"dotnet"),new[]{Path.Combine(Package,"BookOfEternityClient.dll")}.Concat(arguments).ToArray(),label);
    internal Task<(int Exit,string Output,string Error)> RunObservationTimeout()=>Execute(Find("pwsh"),["-NoLogo","-NoProfile","-Command","[Threading.Thread]::Sleep(60000)"],"observed-timeout",500,TimeSpan.FromMilliseconds(1));
    private async Task Prepare(string exe,string[] args,string label)
    {
        var start=new ProcessStartInfo(exe){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=TestRepoPaths.RepoRoot};foreach(var a in args)start.ArgumentList.Add(a);
        using var p=Process.Start(start)!;
        var log=await LinuxFallbackSupervisorTests.NativeRun.ObserveBuild(p,TimeSpan.FromSeconds(50),Path.Combine(Root,label+".log"));
        Assert.True(p.ExitCode==0,"Preparation failure, not causal RED: "+log);
    }
    private async Task<(int Exit,string Output,string Error)> Execute(string exe,string[] args,string label,int guardianLimit=20000,TimeSpan? observation=null)
    {
        var n=++_sequence;var report=Path.Combine(Root,$"guardian-{n}.json");
        var start=new ProcessStartInfo(Guardian){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=Root};
        foreach(var a in new[]{report,guardianLimit.ToString(System.Globalization.CultureInfo.InvariantCulture),exe}.Concat(args))start.ArgumentList.Add(a);
        start.Environment["PATH"]=Runtime;start.Environment["DOTNET_ROOT"]=Runtime;start.Environment["DOTNET_MULTILEVEL_LOOKUP"]="0";
        start.Environment.Remove("BOE_GM_MAIN_OPERATION");start.Environment.Remove("BOE_GM_MAIN_BINDING");
        using var p=Process.Start(start)!;_unsettled.Add(n);var output=p.StandardOutput.ReadToEndAsync();var error=p.StandardError.ReadToEndAsync();
        // Independent guardian owns timed descendants and cannot exit until exclusive ECHILD.
        var exit=p.WaitForExitAsync();TimeoutException? observationFailure=null;
        try {await exit.WaitAsync(observation??TimeSpan.FromSeconds(30));}
        catch(TimeoutException e) {observationFailure=e;await exit;} // retain original guardian/drains through its scoped deadline/reap

        var stdout=await output;var stderr=await error;
        Assert.True(File.Exists(report),"Independent guardian report missing; cleanup unqualified.");
        var cleanup=JsonDocument.Parse(File.ReadAllText(report)).RootElement.Clone();_cleanups.Add(cleanup);
        if(p.ExitCode==0&&cleanup.GetProperty("echild").GetBoolean()&&cleanup.GetProperty("failures").GetInt32()==0)_unsettled.Remove(n);
        Directory.CreateDirectory(Evidence);
        File.WriteAllText(Path.Combine(Evidence,Path.GetFileName(Root)+$"-{n}-{label}.json"),JsonSerializer.Serialize(new {Label=label,ObservationTimedOut=observationFailure!=null,GuardianExit=p.ExitCode,Report=cleanup,Output=stdout,Error=stderr,PlayerSdkPresent=Directory.Exists(Path.Combine(Runtime,"sdk")),PlayerPath=Runtime}));
        if(observationFailure!=null)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(observationFailure).Throw();
        Assert.Equal(0,p.ExitCode);Assert.True(cleanup.GetProperty("echild").GetBoolean());Assert.Equal(0,cleanup.GetProperty("failures").GetInt32());Assert.False(cleanup.GetProperty("deadline").GetBoolean());Assert.Equal(0,cleanup.GetProperty("emergencySignals").GetInt32());
        return (cleanup.GetProperty("driverExitCode").GetInt32(),stdout,stderr);
    }
    private static string Find(string name)=>(Environment.GetEnvironmentVariable("PATH")??"").Split(Path.PathSeparator).Select(p=>Path.Combine(p,name)).First(File.Exists);
    public ValueTask DisposeAsync()
    {
        Directory.CreateDirectory(Evidence);
        foreach(var p in Directory.Exists(Root)?Directory.GetFiles(Root,"*.log"):[])File.Copy(p,Path.Combine(Evidence,Path.GetFileName(Root)+"-"+Path.GetFileName(p)),true);
        var settled=_unsettled.Count==0&&_cleanups.All(c=>c.GetProperty("echild").GetBoolean()&&c.GetProperty("failures").GetInt32()==0);
        if(settled&&Directory.Exists(Root))Directory.Delete(Root,true);
        File.WriteAllText(Path.Combine(Evidence,Path.GetFileName(Root)+"-cleanup.json"),JsonSerializer.Serialize(new{Root,Removed=!Directory.Exists(Root),ExclusiveEchild=settled,UnsettledRuns=_unsettled.Count,Runs=_cleanups.Count}));
        return ValueTask.CompletedTask;
    }
}
