using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
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
        Assert.True(result.Exit == 0, "Causal route failure after successful package preparation: " + result.Error);
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
}

internal sealed class AuxiliaryPackageFixture : IAsyncDisposable
{
    internal readonly string Root = Path.Combine(Path.GetTempPath(), "boe-auxiliary-" + Guid.NewGuid().ToString("N"));
    internal string Package => Path.Combine(Root, "relocated package Ж", "BookOfEternityClient");
    internal string Runtime => Path.Combine(Root, "player-bin");
    internal FileSystemManager Files = null!;
    private readonly List<JsonElement> _cleanups = [];
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

    internal async Task<(int Exit,string Output,string Error)> Run(string label,string? action=null,string[]? arguments=null)
    {
        var args=new List<string>{"-NoLogo","-NoProfile","-File",Path.Combine(Package,"Launcher/bookofeternity.ps1"),"-SessionPath",Files.GameSessionPath,"prepare-turn"};
        args.AddRange(arguments??["--action",action??"action Ж😀 'quoted'\nsecond line","--session-id","fixture-session","--request-id","fixture-request","--turn-number","7","--dice","14,8,17","--current-realm","Mortal World"]);
        return await Execute(Find("pwsh"),args.ToArray(),label);
    }
    private async Task Prepare(string exe,string[] args,string label)
    {
        var start=new ProcessStartInfo(exe){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=TestRepoPaths.RepoRoot};foreach(var a in args)start.ArgumentList.Add(a);
        using var p=Process.Start(start)!;
        var log=await LinuxFallbackSupervisorTests.NativeRun.ObserveBuild(p,TimeSpan.FromSeconds(50),Path.Combine(Root,label+".log"));
        Assert.True(p.ExitCode==0,"Preparation failure, not causal RED: "+log);
    }
    private async Task<(int Exit,string Output,string Error)> Execute(string exe,string[] args,string label)
    {
        var n=++_sequence;var report=Path.Combine(Root,$"guardian-{n}.json");
        var start=new ProcessStartInfo(Guardian){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=Root};
        foreach(var a in new[]{report,"20000",exe}.Concat(args))start.ArgumentList.Add(a);
        start.Environment["PATH"]=Runtime;start.Environment["DOTNET_ROOT"]=Runtime;start.Environment["DOTNET_MULTILEVEL_LOOKUP"]="0";
        start.Environment.Remove("BOE_GM_MAIN_OPERATION");start.Environment.Remove("BOE_GM_MAIN_BINDING");
        using var p=Process.Start(start)!;var output=p.StandardOutput.ReadToEndAsync();var error=p.StandardError.ReadToEndAsync();
        // Independent guardian owns timed descendants and cannot exit until exclusive ECHILD.
        await p.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
        var stdout=await output;var stderr=await error;
        Assert.True(File.Exists(report),"Independent guardian report missing; cleanup unqualified.");
        var cleanup=JsonDocument.Parse(File.ReadAllText(report)).RootElement.Clone();_cleanups.Add(cleanup);
        Directory.CreateDirectory(Evidence);
        File.WriteAllText(Path.Combine(Evidence,Path.GetFileName(Root)+$"-{n}-{label}.json"),JsonSerializer.Serialize(new {Label=label,GuardianExit=p.ExitCode,Report=cleanup,Output=stdout,Error=stderr,PlayerSdkPresent=Directory.Exists(Path.Combine(Runtime,"sdk")),PlayerPath=Runtime}));
        Assert.Equal(0,p.ExitCode);Assert.True(cleanup.GetProperty("echild").GetBoolean());Assert.Equal(0,cleanup.GetProperty("failures").GetInt32());Assert.False(cleanup.GetProperty("deadline").GetBoolean());Assert.Equal(0,cleanup.GetProperty("emergencySignals").GetInt32());
        return (cleanup.GetProperty("driverExitCode").GetInt32(),stdout,stderr);
    }
    private static string Find(string name)=>(Environment.GetEnvironmentVariable("PATH")??"").Split(Path.PathSeparator).Select(p=>Path.Combine(p,name)).First(File.Exists);
    public ValueTask DisposeAsync()
    {
        Directory.CreateDirectory(Evidence);
        foreach(var p in Directory.Exists(Root)?Directory.GetFiles(Root,"*.log"):[])File.Copy(p,Path.Combine(Evidence,Path.GetFileName(Root)+"-"+Path.GetFileName(p)),true);
        var settled=_cleanups.All(c=>c.GetProperty("echild").GetBoolean()&&c.GetProperty("failures").GetInt32()==0);
        if(settled&&Directory.Exists(Root))Directory.Delete(Root,true);
        File.WriteAllText(Path.Combine(Evidence,Path.GetFileName(Root)+"-cleanup.json"),JsonSerializer.Serialize(new{Root,Removed=!Directory.Exists(Root),ExclusiveEchild=settled,Runs=_cleanups.Count}));
        return ValueTask.CompletedTask;
    }
}
