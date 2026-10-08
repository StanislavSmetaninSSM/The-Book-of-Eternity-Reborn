using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services.GmRuntime;

namespace BookOfEternityClient.Tests;

internal static partial class OwnedTerminalScenarioDriver
{
    private static async Task RunReusableRelayAsync(string mode,string folder,object host,Type type,
        Func<object,Task<JsonElement>> rpc,Dictionary<string,object?> result)
    {
        void Require(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
        var files=(FileSystemManager)type.GetField("_neutralFiles",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(host)!;
        var main=(GmSessionRunCoordinator)type.GetField("_mainRun",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(host)!;
        var original=type.GetField("_pty",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(host);
        var queue=Path.Combine(folder,"queue");var tools=Path.Combine(folder,"ship/gm-relay");
        var turn=new{sessionId=Guid.NewGuid().ToString("N"),requestId=Guid.NewGuid().ToString("N"),turnNumber=1};
        await main.RunOperationAsync(async()=>{
            foreach(var pair in new Dictionary<string,string>{
                ["input/turn_request.json"]=JsonSerializer.Serialize(turn),
                ["game_state/control/pending_turn_snapshot.json"]=JsonSerializer.Serialize(turn),
                ["game_state/control/pending_turn_snapshot.authority.json"]="{}",
                ["game_state/meta/soul_state.json"]="{\"currentRealm\":\"Chaos Sea\"}",
                ["game_state/control/gm_turn_helper.bootstrap.ps1"]=". '"+Path.Combine(folder,"ship/BookOfEternityClient/Launcher/GM_Turn_Helper.ps1").Replace("'","''")+"'\nInitialize-BoeGmTurnHelper -GameSessionPath '"+files.GameSessionPath.Replace("'","''")+"'\n"
            })await files.WriteFileAtomicAsync(pair.Key,pair.Value);
            return true;
        });
        var status=(await rpc(new{command="status"})).GetProperty("status");var binding=status.GetProperty("inputBindingId").GetString();
        var prompt="controlled current request Ж🙂"+(mode=="production-main-relay-multiline"?"\n":" ")+"Read current turn/pending before responding.";
        var frame=new{command="dispatchPrompt",operationId="relay-one",operationKind="turn",operationRevision="neutral-1",inputBindingId=binding,text=prompt,appendEnter=true};
        var sent=await rpc(frame);result["OriginalDispatch"]=sent;
        Require(sent.GetProperty("promptDelivery").GetProperty("disposition").GetString()=="submission-observed","Original relay submission not observed.");
        string? request=null;
        for(var i=0;i<300;i++){request=Directory.GetDirectories(queue,"request-*").SingleOrDefault();if(request!=null && File.Exists(Path.Combine(request,"game-request.json")))break;await Task.Delay(10);}
        Require(request!=null,"Current queue request missing after actual submit.");
        Require(File.ReadAllText(Path.Combine(request!,"prompt.txt"))==prompt,"Prompt bytes changed before worker.");
        async Task<string> Worker(params string[] args){
            var start=new ProcessStartInfo("/usr/bin/python3"){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};
            foreach(var a in new[]{Path.Combine(tools,"relay_worker.py")}.Concat(args))start.ArgumentList.Add(a);
            using var process=Process.Start(start)!;var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(4));var text=await stdout;var error=await stderr;
            await File.AppendAllTextAsync(Path.Combine(folder,"worker.log"),text+error);
            Require(process.ExitCode==0,"Shared worker failed after actual terminal submit: "+error);return text;
        }
        try {
            using(var inspected=JsonDocument.Parse(await Worker("inspect",request!)))Require(inspected.RootElement.GetProperty("Prompt").GetString()==prompt,"Worker did not receive actual prompt.");
            var packet=Path.Combine(folder,"packet.json");
            var failure=mode=="production-main-relay-consumer-error";
            await File.WriteAllTextAsync(packet,JsonSerializer.Serialize(new{Completion=failure?"repair":"turn",Writes=new[]{new{Path="output/response.json",ExpectedSHA256="missing",Data=new{message="controlled reusable packet Ж🙂"}}},FilesModified=new[]{"output/response.json"}}));
            await Worker("answer",request!,packet,"--adapter","inert-no-provider");
            var execution=Path.Combine(request!,"execution.json");for(var i=0;i<600 && !File.Exists(execution);i++)await Task.Delay(10);
            using(var observed=JsonDocument.Parse(await File.ReadAllBytesAsync(execution))){
                Require(observed.RootElement.GetProperty("Executed").GetBoolean(),"Real fixed consumer not invoked.");
                Require((observed.RootElement.GetProperty("ExitCode").GetInt32()==0)==!failure,"Real consumer outcome mismatched.");result["Execution"]=observed.RootElement.Clone();
            }
            Require(File.Exists(files.ResolvePath("ready/turn_complete.json"))==!failure,"Completion signal mismatched actual consumer outcome.");
            Require(File.Exists(files.ResolvePath("output/response.json"))==!failure,"Failed kind admission wrote output.");
            if(!failure){
                using var completion=JsonDocument.Parse(await File.ReadAllBytesAsync(files.ResolvePath("ready/turn_complete.json")));
                Require(completion.RootElement.GetProperty("requestId").GetString()==turn.requestId,"Completion belongs to another request.");
                var duplicate=await rpc(frame);Require(duplicate.GetProperty("promptDelivery").GetProperty("disposition").GetString()=="submission-observed","Original duplicate changed retained receipt.");
                Require(Directory.GetDirectories(queue,"request-*").Length==1,"Duplicate created another queue request.");
            } else {
                Require(!(await rpc(new{command="setReady",ready=true})).GetProperty("ok").GetBoolean(),"Consumer error presentation borrowed idle.");
            }
            Require(ReferenceEquals(original,type.GetField("_pty",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(host)),"Worker switched original terminal.");
            result["RealWorkerAndHelperConsumer"]=true;
        } finally {
            // Controlled baseline/failed worker also closes before original stop.
            await Worker("close",queue);
            for(var i=0;i<300 && !File.Exists(Path.Combine(queue,"closed.json"));i++)await Task.Delay(10);
            using var closed=JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(queue,"closed.json")));
            Require(new[]{"ExecutionDisabled","ChildExited","IoDrained"}.All(k=>closed.RootElement.GetProperty(k).GetBoolean()),"Queue closure/I-O unconfirmed.");
            result["QueueClosedBeforeOriginalStop"]=true;
        }
        var stopped=await rpc(new{command="shutdown",rootKey=main.Identity.RootKey,expectedMainIdentity=main.Identity});
        Require(stopped.GetProperty("ok").GetBoolean() && main.Record!.Disposition==GmSessionRunDisposition.Stopped && !main.RetainsAuthority,"Original stop/I-O/Stopped ACK unconfirmed.");
        result["OriginalStoppedIdentity"]=main.Identity;result["ScopedStopViaPipe"]=true;
    }
}
