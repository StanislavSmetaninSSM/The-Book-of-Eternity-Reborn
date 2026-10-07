using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Reflection;
using Xunit;

namespace BookOfEternityClient.Tests;

// Real original host accept loop/parser and actual packaged observer process;
// controlled CLI bytes only. These are not live OpenCode/provider receipts.
public sealed class GmConnectedMiniTransactionTests
{
    [Theory]
    [InlineData("success", "submission-observed")]
    [InlineData("changed", "draft-uncertain")]
    [InlineData("stale", "draft-uncertain")]
    [InlineData("cancel", "draft-uncertain")]
    [InlineData("manual", "draft-uncertain")]
    [InlineData("unrestored", "draft-uncertain")]
    [InlineData("unknown", "unknown-outcome")]
    public async Task ActualOriginalPipe_OneUseWitnessAndCausalEdges(string mode,string expected)
    {
        await using var h=new GmBridgePromptOperationTests.PromptHostFixture();
        using var screen=GmExternalDraftObservationTests.InstallMini(h);
        screen.Feed(GmSynchronizedTerminalPresentationTests.ActualTranscript("startup"));
        var text=string.Join('\n',Enumerable.Range(0,20).Select(i=>$"line{i:00} Кириллица café UpdateGuardians unchanged"));
        var file=Path.Combine(h.Root,"1234567890123.md");
        var launch="original-launch-"+Guid.NewGuid().ToString("N");
        h.HostType.GetField("_draftLaunchBinding",BindingFlags.Instance|BindingFlags.NonPublic)?.SetValue(h.Host,launch);
        h.HostType.GetField("_draftLaunchInput",BindingFlags.Instance|BindingFlags.NonPublic)?.SetValue(h.Host,h.Binding);
        Task? observer=null;int? observerExit=null;var editor=false;
        object Cancel(string command)=>new {command,operationId="one",operationKind="turn",operationRevision="revision-1",inputBindingId=h.BindingId,text,appendEnter=true};
        void Frame(bool home)
        {
            var lines=text.Split('\n');var visible=(home?lines.Take(6):lines.TakeLast(6)).ToArray();
            var frame=new StringBuilder("\u001b[?2026h\u001b[H\u001b[2J");
            var banner=new[]{"","█▀▀█  OpenCode","█  █  /workspace/qualification-1553-opencode-q1/empty-cli-scratch","▀▀▀▀",""};
            for(var i=0;i<banner.Length;i++)frame.Append($"\u001b[{i+1};1H").Append(banner[i]);
            for(var i=0;i<visible.Length;i++)frame.Append($"\u001b[{i+6};1H\u001b[38;2;226;232;240m").Append(visible[i]);
            frame.Append("\u001b[13;1H").Append(" BUILD  ".PadRight(89)+"ctrl+p cmd ");
            frame.Append(home?"\u001b[6;1H":$"\u001b[11;{visible[^1].Length+1}H").Append("\u001b[?25h\u001b[?2026l");
            screen.Feed(Encoding.UTF8.GetBytes(frame.ToString()));
        }
        h.Input.Written=bytes=>
        {
            if(bytes.StartsWith("\u001b[200~")){Frame(false);return;}
            if(bytes=="\u0018e")
            {
                editor=true;
                observer=Task.Run(async()=>
                {
                    if(mode=="cancel")await h.Rpc(Cancel("cancelPrompt"));
                    if(mode=="manual")await h.Rpc(new {command="addText",text="manual"});
                    await File.WriteAllTextAsync(file,mode=="changed"?text+" altered":text,new UTF8Encoding(false));
                    var configuration=new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
                    var start=new ProcessStartInfo(Path.Combine(Environment.GetEnvironmentVariable("DOTNET_ROOT")!,"dotnet")){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};
                    foreach(var a in new[]{Path.Combine(TestRepoPaths.RepoRoot,"BookOfEternityGMBridge/bin",configuration,"net8.0/BookOfEternityGMBridge.dll"),"--observe-draft",file})start.ArgumentList.Add(a);
                    start.Environment["BOE_DRAFT_DIRECTORY"]=h.Root;start.Environment["BOE_DRAFT_BINDING"]=mode=="stale"?"stale-launch":launch;
                    start.Environment["BOE_DRAFT_PIPE"]=Path.Combine(Path.GetTempPath(),"CoreFxPipe_"+h.Pipe);
                    using var child=Process.Start(start)!;
                    try{await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(7));observerExit=child.ExitCode;}
                    finally{if(!child.HasExited){child.Kill();await child.WaitForExitAsync();}}
                    if(observerExit==0 && mode!="unrestored"){File.Delete(file);Frame(false);}
                });return;
            }
            if(bytes=="\u001b[H")Frame(true);
            if(bytes=="\u001b[F")Frame(false);
            if(bytes=="\r")screen.Feed(Encoding.UTF8.GetBytes(mode=="unknown"?"\u001b[?1049h":"\u001b[?2026h\u001b[13;1H\u001b[K"+" BUILD   esc interrupt".PadRight(89)+"ctrl+p cmd "+"\u001b[6;1H\u001b[?2026l"));
        };
        Assert.True((await h.Rpc(new {command="setReady",ready=true})).GetProperty("ok").GetBoolean());
        var result=await h.Rpc(h.Request("one",text));
        if(observer!=null)await observer;
        Assert.True(editor,"Actual original transaction must reach the standard editor gesture before witness/edge verdict.");
        Assert.Equal(expected,result.GetProperty("promptDelivery").GetProperty("disposition").GetString());
        var delivered=Encoding.UTF8.GetString(h.Input.Bytes);
        Assert.StartsWith("\u001b[200~"+text+"\u001b[201~\u0018e",delivered);
        if(mode is "success" or "unknown")
        {
            Assert.Equal(0,observerExit);Assert.Equal("\u001b[200~"+text+"\u001b[201~\u0018e\u001b[H\u001b[F\r",delivered);
        }
        else Assert.DoesNotContain('\r',delivered);
        var count=h.Input.Bytes.Length;
        Assert.Equal(expected,(await h.Rpc(h.Request("one",text))).GetProperty("promptDelivery").GetProperty("disposition").GetString());
        screen.Feed(GmSynchronizedTerminalPresentationTests.ActualTranscript("startup"));
        Assert.False((await h.Rpc(new {command="setReady",ready=true})).GetProperty("ok").GetBoolean());
        await h.Rpc(h.Request("new-id",text));Assert.Equal(count,h.Input.Bytes.Length);
    }

    [Fact]
    public async Task ActualNativeTerminal_FrozenEnvironmentReachesOriginalHost()
        => await GmOwnedTerminalLinuxTests.RunAsync("terminal-environment");
}
