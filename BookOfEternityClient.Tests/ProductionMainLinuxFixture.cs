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
        var reusableRelay=mode.StartsWith("production-main-relay-",StringComparison.Ordinal);
        if(reusableRelay) {
            var own=Path.Combine("/tmp","reusable-relay-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(own);
            File.WriteAllText(Path.Combine(folder,"controlled-root.json"),JsonSerializer.Serialize(new{Root=own,OwnFixtureOnly=true}));folder=own;
        }
        if(mode=="driver-provider-refusal") {
            // TMPDIR contains the native private AF_UNIX bootstrap and managed
            // pipe socket. The controlled package needs a bounded byte-length root.
            var own=Path.Combine("/tmp","ld-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(own);
            File.WriteAllText(Path.Combine(folder,"controlled-root.json"),JsonSerializer.Serialize(new {Root=own,OwnFixtureOnly=true}));folder=own;
        }
        var package=Path.Combine(folder,"package");var ship=Path.Combine(folder,"ship");var root=Path.Combine(folder,"root");Directory.CreateDirectory(root);
        async Task Prepare(string exe,string[] args,string name,int seconds=50)
        {
            var start=new ProcessStartInfo(exe){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};foreach(var a in args)start.ArgumentList.Add(a);
            using var p=Process.Start(start)!;var log=await LinuxFallbackSupervisorTests.NativeRun.ObserveBuild(p,TimeSpan.FromSeconds(seconds),Path.Combine(folder,name+".log"));
            Assert.True(p.ExitCode==0,"Preparation failure, not causal RED: "+log);
        }
        await Prepare("pwsh",["-NoProfile","-File",Path.Combine(repo,"scripts/build-linux-supervisor.ps1"),"-OutputDirectory",package,"-IncludeHostGuardian"],"helper-build");
        var fixture=Path.Combine(package,"configured-neutral-cli");var source=Path.Combine(repo,"tests/fixtures/ProductionMain/configured-neutral-cli.c");
        var compiler=(Environment.GetEnvironmentVariable("PATH")??"").Split(Path.PathSeparator).Select(d=>Path.Combine(d,"cc")).First(File.Exists);
        await Prepare(compiler,["--version"],"fixture-compiler-version");
        await Prepare(compiler,["-std=c11","-O2","-g","-Wall","-Wextra","-Werror",source,"-o",fixture],"fixture-build");
        foreach(var name in new[]{"BookOfEternityClient","BookOfEternityGMBridge"}.Concat(mode.StartsWith("production-main-",StringComparison.Ordinal)?new[]{"BookOfEternityClient.TestSupport"}:Array.Empty<string>()))
            await Prepare("dotnet",["publish",Path.Combine(repo,name,name+".csproj"),"--no-build","--no-restore","-c",new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name,"-o",Path.Combine(ship,name),"-p:BoeNativePackageDirectory="+package,"-p:BoeRequireNativePackage=true"],"publish-"+name);
        if(mode=="package-layout") {
            var resources=Path.Combine(ship,"BookOfEternityClient/operational-resources");
            foreach(var relative in new[]{"CLI_Agent_Daemon_Specification.md","TaskGuides/CLI_Step_Main.txt","Examples/E_CLI_Step_Main.txt","Examples/E_CLI_Mortal_Item_Materialization.txt","Examples/E_CLI_Mortal_Resources.txt","OtherGuides/Effect_Materialization_Contract.md","Examples/E_CLI_Effect_Materialization.txt","OtherGuides/Wound_Materialization_Contract.md","Examples/E_CLI_Wound_Materialization.txt","Examples/E_CLI_Afterlife_Turns.txt","Examples/E_CLI_Training_Showcases.txt","OtherGuides/Afterlife_Contract_Matrix.md","OtherGuides/Afterlife_Combat_Terminology_Glossary.md","Examples/E_CLI_Ink_Feather_Actions.txt"})
                Assert.True(File.Exists(Path.Combine(resources,relative)),"Causal RED: shipped daemon context source missing: "+relative);

            Assert.True(File.Exists(Path.Combine(ship,"BookOfEternityClient/Launcher/bookofeternity.ps1")),"Causal RED: ordinary published layout lacks its real launcher.");
            Assert.True(File.Exists(Path.Combine(ship,"BookOfEternityClient/Launcher/gm_main_operation.ps1")),"Causal RED: ordinary published layout lacks participating helper.");return;
        }
        Assert.Empty(Directory.GetFiles(ship,"*.cs",SearchOption.AllDirectories));Assert.Empty(Directory.GetFiles(ship,"*.csproj",SearchOption.AllDirectories));
        if(mode=="production-main-consumers") File.Copy(Path.Combine(repo,"tests/fixtures/ProductionMain/consumer-boundaries.ps1"),Path.Combine(ship,"BookOfEternityClient/m1-consumers.ps1"));
        if(mode.StartsWith("production-main-daemon-",StringComparison.Ordinal)) File.Copy(Path.Combine(repo,"tests/fixtures/ProductionMain/idle-daemon-stop.py"),Path.Combine(ship,"idle-daemon-stop.py"));
        if(mode=="production-main-daemon-stop" && Environment.GetEnvironmentVariable("BOE_TEST_FOREGROUND_CLOSE_TRACE")=="1")
            File.Copy(Path.Combine(repo,"tests/fixtures/ProductionMain/daemon-close-diagnostic.ps1"),Path.Combine(ship,"daemon-close-diagnostic.ps1"));
        var playerBin=Path.Combine(folder,"player-bin");Directory.CreateDirectory(playerBin);
        foreach(var executable in new[]{"dotnet","pwsh"}) {
            var resolved=(Environment.GetEnvironmentVariable("PATH")??"").Split(Path.PathSeparator).Select(d=>Path.Combine(d,executable)).First(File.Exists);
            File.CreateSymbolicLink(Path.Combine(playerBin,executable),resolved);
        }
        if(mode=="driver-provider-refusal") {
            var installed=Path.Combine(folder,"play/game_session/_runtime");Directory.CreateDirectory(Path.GetDirectoryName(installed)!);
            Directory.Move(ship,installed);File.Copy(fixture,Path.Combine(folder,"configured-neutral-cli"));
            File.WriteAllText(Path.Combine(folder,"fixture-preparation.json"),JsonSerializer.Serialize(new {
                Source=source,SourceSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(source))).ToLowerInvariant(),
                DriverSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(repo,"tests/fixtures/ProductionMain/live-game-one-turn.py")))).ToLowerInvariant(),
                BinarySha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(fixture))).ToLowerInvariant(),
                CompilerPath=compiler,CompilerSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(compiler))).ToLowerInvariant(),
                InertOnly=true,ProviderCalls=0,SourceCheckoutAtPlayerStartup=false
            }));
            var controlled=new ProcessStartInfo(Path.Combine(package,"host-guardian")){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};
            controlled.Environment["PATH"]=playerBin;controlled.Environment["TERM"]="dumb";controlled.Environment["BOE_BOOTSTRAP_DIAGNOSTIC"]="1";
            foreach(var arg in new[]{"--live-turn",Path.Combine(folder,"guardian.json"),"300000","/usr/bin/python3",Path.Combine(repo,"tests/fixtures/ProductionMain/live-game-one-turn.py"),folder,"--controlled-provider-refusal"})controlled.ArgumentList.Add(arg);
            using var process=Process.Start(controlled)!;
            await LinuxFallbackSupervisorTests.NativeRun.ObserveBuild(process,TimeSpan.FromSeconds(305),Path.Combine(folder,"guardian.log"));
            using var guardianReport=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(folder,"guardian.json")));
            Assert.True(guardianReport.RootElement.GetProperty("echild").GetBoolean());Assert.Equal(0,guardianReport.RootElement.GetProperty("emergencySignals").GetInt32());
            Assert.Equal(0,guardianReport.RootElement.GetProperty("failures").GetInt32());Assert.False(guardianReport.RootElement.GetProperty("deadline").GetBoolean());
            using var actual=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(folder,"probe-result.json")));
            Assert.True(actual.RootElement.GetProperty("ControlledCleanupVerified").GetBoolean(),actual.RootElement.ToString());
            Assert.False(actual.RootElement.GetProperty("Success").GetBoolean());Assert.Equal(0,actual.RootElement.GetProperty("AcceptedGameTurns").GetInt32());
            Assert.Equal(0,process.ExitCode);return;
        }
        var files=new FileSystemManager(root,NullLogger<FileSystemManager>.Instance);files.EnsureDirectoryStructure();
        var cwd=Path.Combine(root,"work Ж");Directory.CreateDirectory(cwd);
        var command="& '"+fixture.Replace("'","''")+"' '--model' 'gm-model-sentinel' '--arg' 'a b'";
        var settings=new GameSettings{GmBridgeBackend="OwnedTerminal",GmCliLaunchCommand=command,GmBridgeShellWorkingDirectory=cwd,GmBridgeAutoStart=false,MusicEnabled=false,SoundEnabled=false,
            GmCliInputProfile=new(){IdleMarker="NEUTRAL READY",PromptPrefix="> ",WorkingMarker="NEUTRAL WORKING",ObservationTimeoutMilliseconds=1800}};
        if(reusableRelay) {
            var bundle=Path.Combine(ship,"gm-relay");Directory.CreateDirectory(bundle);var shared=Path.Combine(repo,"tools/gm-relay");
            if(Directory.Exists(shared)) {
                foreach(var file in Directory.GetFiles(shared))File.Copy(file,Path.Combine(bundle,Path.GetFileName(file)));
                settings.GmCliInputProfile=JsonSerializer.Deserialize<BookOfEternityClient.Configuration.GmCliInputProfile>(File.ReadAllBytes(Path.Combine(bundle,"input-profile.json")))!;
            } else {
                // Causal baseline: actual accepted persistent relay receives the
                // real Bridge prompt before the missing shared worker fails.
                File.Copy(Path.Combine(repo,"tests/fixtures/ProductionMain/codex-gm-relay.py"),Path.Combine(bundle,"relay_cli.py"));
                File.Copy(Path.Combine(repo,"tests/fixtures/ProductionMain/relay-apply-response.ps1"),Path.Combine(bundle,"relay-apply-response.ps1"));
                settings.GmCliInputProfile.PromptPrefix="RELAY> ";settings.GmCliInputProfile.BlockedMarkers=["RELAY ERROR"];settings.GmCliInputProfile.ObservationTimeoutMilliseconds=15000;
            }
            Directory.CreateDirectory(Path.Combine(folder,"queue"));
            command="& '/usr/bin/python3' '"+Path.Combine(bundle,"relay_cli.py").Replace("'","''")+"' --session '"+files.GameSessionPath.Replace("'","''")+"' --queue '"+Path.Combine(folder,"queue").Replace("'","''")+"' --model gpt-6.1-sol";
            settings.GmCliLaunchCommand=command;settings.GmBridgeShellWorkingDirectory=files.GameSessionPath;
            File.WriteAllText(Path.Combine(folder,"relay-bundle.json"),JsonSerializer.Serialize(new{Baseline=!Directory.Exists(shared),Files=Directory.GetFiles(bundle).ToDictionary(p=>Path.GetFileName(p),p=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p))).ToLowerInvariant()),ModelRequests=0}));
        }
        var config=JsonSerializer.SerializeToUtf8Bytes(settings);File.WriteAllBytes(Path.Combine(files.GameSessionPath,"config.json"),config);
        var initialGeneration=await new StateManager(files,settings,NullLogger<StateManager>.Instance).BootstrapLocalStorageAsync();
        // Proposed setting is carried as ordinary profile JSON before the property exists.
        var json=JsonSerializer.Deserialize<Dictionary<string,JsonElement>>(File.ReadAllText(Path.Combine(files.GameSessionPath,"config.json")))!;
        json["GmMainOwnerBackend"]=JsonSerializer.SerializeToElement("NativeLineage");File.WriteAllText(Path.Combine(files.GameSessionPath,"config.json"),JsonSerializer.Serialize(json));
        if(mode.StartsWith("production-main-console-",StringComparison.Ordinal)) {
            // Controlled current-schema input sufficient for the real session
            // availability consumer, not an accepted turn or live game history.
            await files.WriteFileAtomicAsync("game_state/meta/soul_state.json","{\"soulName\":\"Контрольная душа\",\"currentRealm\":\"Chaos Sea\",\"currentIncarnation\":0}");
            await files.WriteFileAtomicAsync("game_state/history/chat_log.json","{\"sessionId\":\"controlled-console-runtime\",\"messages\":[]}");
            Assert.False(File.Exists(files.ResolvePath("input/turn_request.json")));
        }
        if(mode.StartsWith("production-main-load-",StringComparison.Ordinal)) {
            var archiveState=PortableSaveFixture.Seed(files);
            if(mode.StartsWith("production-main-load-console",StringComparison.Ordinal)) {
                // Complete isolated Mortal fixture; the real required-refresh validator remains active.
                var extra=new Dictionary<string,string> {
                    ["game_state/core/player_status.json"]="{\"currentCondition\":\"neutral fixture\",\"money\":0}",
                    ["game_state/inventory/item_identity_index.json"]="{\"schemaVersion\":1,\"entries\":[]}",
                    ["game_state/meta/achievements.json"]="{\"unlockedAchievements\":[],\"trackedProgress\":[],\"stats\":{\"totalUnlocked\":0,\"byCategory\":{\"combat\":0,\"exploration\":0,\"story\":0,\"social\":0,\"crafting\":0,\"meta\":0,\"death\":0,\"secret\":0},\"byRarity\":{\"common\":0,\"uncommon\":0,\"rare\":0,\"epic\":0,\"legendary\":0}}}",
                    ["lore/codex_entries.json"]="{\"entries\":[],\"categories\":{\"cosmology\":0,\"geography\":0,\"history\":0,\"cultures\":0,\"creatures\":0,\"characters\":0,\"artifacts\":0,\"factions\":0,\"magic\":0,\"other\":0},\"totalEntries\":0}"
                };
                foreach(var name in new[]{"cultures","geography","history","threats","world_setting"})extra["lore/current_world/"+name+".json"]="{\"fixture\":\"neutral\"}";
                foreach(var entry in extra) {
                    var local=files.ResolvePath(entry.Key);Directory.CreateDirectory(Path.GetDirectoryName(local)!);File.WriteAllText(local,entry.Value);
                }
            }
            var saves=new BookOfEternityClient.Services.SaveLoadService(files,archiveState,NullLogger<BookOfEternityClient.Services.SaveLoadService>.Instance);
            Assert.True(await saves.SaveGameAsync("load-neutral","original configured neutral CLI"),"Preparation: real archive creation failed.");
            if(mode.EndsWith("fault-rollback",StringComparison.Ordinal) || mode.EndsWith("fault-uncertain",StringComparison.Ordinal))
                await files.WriteFileAtomicAsync("game_state/world/test_fixture_state.json","{\"state\":\"before-load\"}"); // own quiescent root, before original terminal creation
            if(mode.EndsWith("profile-from-archive",StringComparison.Ordinal)) {
                var activeCwd=Path.Combine(root,"active Ж");Directory.CreateDirectory(activeCwd);
                json["GmCliLaunchCommand"]=JsonSerializer.SerializeToElement(command.Replace("gm-model-sentinel","active-model-sentinel",StringComparison.Ordinal));
                json["GmBridgeShellWorkingDirectory"]=JsonSerializer.SerializeToElement(activeCwd);
                File.WriteAllText(Path.Combine(files.GameSessionPath,"config.json"),JsonSerializer.Serialize(json));
            }
        }
        if(mode=="refuse-worker") {
            await using var ledger=await GmWorkerRunLedger.OpenCoordinatorAsync(new(root));Assert.NotNull(ledger);
            Assert.Equal(WorkerLedgerMutationKind.Applied,await ledger.InitializeAsync());
            var generation=initialGeneration;
            Assert.Equal(WorkerLedgerMutationKind.Applied,(await ledger.PrepareAsync(new(generation,"inert_worker","inert_task",new string('a',64),WorkerRunBackend.LinuxNativeLineage,WorkerRunScope.OrdinarySamePidNamespace,files.GameSessionPath),ledger.Sequence)).Kind);
        }
        if(mode=="refuse-storage") { var evidence=Path.Combine(files.RuntimeRootPath,"load-transactions/unknown-journal");Directory.CreateDirectory(Path.GetDirectoryName(evidence)!);File.WriteAllBytes(evidence,[0xfe,0]); }
        File.WriteAllText(Path.Combine(folder,"fixture-preparation.json"),JsonSerializer.Serialize(new{Source=source,SourceSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(source))).ToLowerInvariant(),Binary=fixture,BinarySha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(fixture))).ToLowerInvariant(),Command=command,Cwd=cwd,ShippedSourceFiles=0,PublisherOnlyCompile=true,CompilerPath=compiler,CompilerSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(compiler))).ToLowerInvariant(),CompilerAbsentAtPlayerStartup=true,PlayerPath=playerBin}));
        var start=new ProcessStartInfo(Path.Combine(package,"host-guardian")){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=ship};
        start.Environment["PATH"]=playerBin;
        var scenarioArgs=mode.StartsWith("production-main-",StringComparison.Ordinal)?new[]{Path.Combine(Environment.GetEnvironmentVariable("DOTNET_ROOT")!,"dotnet"),Path.Combine(ship,"BookOfEternityClient.TestSupport/BookOfEternityClient.TestSupport.dll"),mode,package,folder}:new[]{"/usr/bin/python3",Path.Combine(repo,"tests/fixtures/ProductionMain/ordinary.py"),mode,folder,ship,files.GameSessionPath};
        foreach(var a in new[]{Path.Combine(folder,"guardian.json"),"30000"}.Concat(scenarioArgs))start.ArgumentList.Add(a);
        using var guardian=Process.Start(start)!;await LinuxFallbackSupervisorTests.NativeRun.ObserveBuild(guardian,TimeSpan.FromSeconds(35),Path.Combine(folder,"guardian.log"));
        using var report=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(folder,"guardian.json")));
        Assert.True(report.RootElement.GetProperty("echild").GetBoolean());Assert.Equal(0,report.RootElement.GetProperty("emergencySignals").GetInt32());Assert.Equal(0,report.RootElement.GetProperty("failures").GetInt32());Assert.False(report.RootElement.GetProperty("deadline").GetBoolean());
        using var result=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(folder,"scenario.json")));
        Assert.True(result.RootElement.TryGetProperty("Success",out var succeeded) && succeeded.GetBoolean(),result.RootElement.ToString());Assert.Equal(0,guardian.ExitCode);
        if(mode=="production-main-uncertain") {
            var cold=Path.Combine(folder,"cold-check");Directory.CreateDirectory(cold);
            var retry=new ProcessStartInfo(Path.Combine(package,"host-guardian")){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};retry.Environment["PATH"]=playerBin;
            foreach(var arg in new[]{Path.Combine(cold,"guardian.json"),"10000","/usr/bin/python3",Path.Combine(repo,"tests/fixtures/ProductionMain/ordinary.py"),"refuse-cold",cold,ship,files.GameSessionPath})retry.ArgumentList.Add(arg);
            using var fresh=Process.Start(retry)!;await LinuxFallbackSupervisorTests.NativeRun.ObserveBuild(fresh,TimeSpan.FromSeconds(15),Path.Combine(cold,"guardian.log"));
            using var cleanup=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(cold,"guardian.json")));
            Assert.True(cleanup.RootElement.GetProperty("echild").GetBoolean());Assert.Equal(0,cleanup.RootElement.GetProperty("emergencySignals").GetInt32());Assert.Equal(0,cleanup.RootElement.GetProperty("failures").GetInt32());Assert.False(cleanup.RootElement.GetProperty("deadline").GetBoolean());
            using var refused=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(cold,"scenario.json")));
            Assert.True(refused.RootElement.GetProperty("Success").GetBoolean(),refused.RootElement.ToString());Assert.Equal(0,fresh.ExitCode);
        }
    }
}
