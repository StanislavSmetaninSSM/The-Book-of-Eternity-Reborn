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
    [InlineData("unknown", "unknown-outcome")]
    [InlineData("restored-cursor", "submission-observed")]
    [InlineData("wrapped-edge", "submission-observed")]
    public Task ActualOriginalPipe_PositiveEdgesAndUnknownSubmit(string mode,string expected)=>RunConnectedAsync(mode,expected);

    [Theory]
    [InlineData("changed", "draft-uncertain")]
    [InlineData("stale", "draft-uncertain")]
    [InlineData("cancel", "draft-uncertain")]
    [InlineData("manual", "draft-uncertain")]
    [InlineData("unrestored", "draft-uncertain")]
    public Task ActualOriginalPipe_WitnessRefusals(string mode,string expected)=>RunConnectedAsync(mode,expected);

    [Theory]
    [InlineData("eof", "draft-uncertain")]
    [InlineData("busy-composer", "unknown-outcome")]
    [InlineData("busy-panel", "unknown-outcome")]
    [InlineData("symlink-removal", "draft-uncertain")]
    [InlineData("queued-enter", "draft-uncertain")]
    public Task ActualOriginalPipe_ReviewBoundaries(string mode,string expected)=>RunConnectedAsync(mode,expected);

    [Theory]
    [InlineData("queued-home", "draft-uncertain")]
    [InlineData("blocks-spinner", "submission-observed")]
    [InlineData("held-spinner", "submission-observed")]
    [InlineData("braille-spinner", "unknown-outcome")]
    public Task ActualOriginalPipe_ReservationAndPinnedSpinner(string mode,string expected)=>RunConnectedAsync(mode,expected);

    [Fact]
    public Task ActualOriginalPipe_DeniedRemovalIsNotAbsence()=>RunConnectedAsync("denied-removal","draft-uncertain");

    [Theory]
    [InlineData("soft\u00adhyphen")]
    [InlineData("combining\u0483mark")]
    public async Task ActualOriginalPipe_UnsupportedGlyphIsZeroWrite(string text)
    {
        await using var h=new GmBridgePromptOperationTests.PromptHostFixture();using var screen=GmExternalDraftObservationTests.InstallMini(h);
        screen.Feed(GmSynchronizedTerminalPresentationTests.ActualTranscript("startup"));
        h.HostType.GetField("_draftLaunchBinding",BindingFlags.Instance|BindingFlags.NonPublic)?.SetValue(h.Host,"own-launch");
        h.HostType.GetField("_draftLaunchInput",BindingFlags.Instance|BindingFlags.NonPublic)?.SetValue(h.Host,h.Binding);
        var result=await h.Rpc(h.Request("glyph",text));
        Assert.Equal("not-written",result.GetProperty("promptDelivery").GetProperty("disposition").GetString());Assert.Empty(h.Input.Bytes);
    }

    private static async Task RunConnectedAsync(string mode,string expected)
    {
        await using var h=new GmBridgePromptOperationTests.PromptHostFixture();
        using var screen=GmExternalDraftObservationTests.InstallMini(h);
        screen.Feed(GmSynchronizedTerminalPresentationTests.ActualTranscript("startup"));
        var text=string.Join('\n',Enumerable.Range(0,20).Select(i=>$"line{i:00} Кириллица café UpdateGuardians unchanged"));
        var file=Path.Combine(h.Root,"1234567890123.md");
        var launch="original-launch-"+Guid.NewGuid().ToString("N");
        h.HostType.GetField("_draftLaunchBinding",BindingFlags.Instance|BindingFlags.NonPublic)?.SetValue(h.Host,launch);
        h.HostType.GetField("_draftLaunchInput",BindingFlags.Instance|BindingFlags.NonPublic)?.SetValue(h.Host,h.Binding);
        Task? observer=null,queued=null;int? observerExit=null;var editor=false;
        object Cancel(string command)=>new {command,operationId="one",operationKind="turn",operationRevision="revision-1",inputBindingId=h.BindingId,text,appendEnter=true};
        void Frame(bool home)
        {
            var lines=text.Split('\n');var visible=(home?lines.Take(6):lines.TakeLast(6)).ToArray();
            if(mode=="wrapped-edge" && !home)visible=lines.TakeLast(5).Take(4).Concat(new[]{lines[^1][..^9].TrimEnd(' '),"unchanged"}).ToArray();
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
                    if(mode=="eof"){
                        using var pipe=await h.Connect();var reader=new BookOfEternityClient.Services.GmRuntime.MainOperationReader(pipe);
                        await BookOfEternityClient.Services.GmRuntime.MainOperationReader.WriteAsync(pipe,new {command="observeDraft",binding=launch,path=file},CancellationToken.None);
                        var hello=await reader.ReadAsync<JsonElement>(CancellationToken.None);
                        var actual=h.HostType.Assembly.GetType("BookOfEternityGMBridge.DraftObservation")!.GetMethod("Read",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,new object[]{h.Root,file});
                        var fields=JsonSerializer.SerializeToElement(actual).EnumerateObject().ToDictionary(p=>p.Name,p=>p.Value.Clone());
                        fields["nonce"]=JsonSerializer.SerializeToElement(hello.GetProperty("nonce").GetString());
                        await BookOfEternityClient.Services.GmRuntime.MainOperationReader.WriteAsync(pipe,fields,CancellationToken.None);
                        Assert.True((await reader.ReadAsync<JsonElement>(CancellationToken.None)).GetProperty("ok").GetBoolean());
                        // Actual helper failure closes exactly like EOF; it must never be success evidence.
                        pipe.Dispose();observerExit=2;File.Delete(file);Frame(false);return;
                    }
                    var configuration=new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
                    var start=new ProcessStartInfo(Path.Combine(Environment.GetEnvironmentVariable("DOTNET_ROOT")!,"dotnet")){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};
                    foreach(var a in new[]{Path.Combine(TestRepoPaths.RepoRoot,"BookOfEternityGMBridge/bin",configuration,"net8.0/BookOfEternityGMBridge.dll"),"--observe-draft",file})start.ArgumentList.Add(a);
                    start.Environment["BOE_DRAFT_DIRECTORY"]=h.Root;start.Environment["BOE_DRAFT_BINDING"]=mode=="stale"?"stale-launch":launch;
                    start.Environment["BOE_DRAFT_PIPE"]=Path.Combine(Path.GetTempPath(),"CoreFxPipe_"+h.Pipe);
                    using var child=Process.Start(start)!;
                    try{await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(7));observerExit=child.ExitCode;}
                    finally{if(!child.HasExited){child.Kill();await child.WaitForExitAsync();}}
                    if(observerExit==0 && mode!="unrestored"){
                        File.Delete(file);
                        if(mode=="queued-home"){
                            var gate=(SemaphoreSlim)h.Get("_ptyWriteLock")!;await gate.WaitAsync();
                            Frame(true);
                            queued=Task.Run(async()=>{
                                try{await Task.Delay(100);Frame(true);}finally{gate.Release();}
                            });
                        }
                        if(mode=="symlink-removal")File.CreateSymbolicLink(file,Path.Combine(h.Root,"missing-own-target"));
                        if(mode=="denied-removal")File.SetUnixFileMode(h.Root,UnixFileMode.None);
                        if(mode!="queued-home")Frame(false);
                        if(mode=="restored-cursor")screen.Feed(Encoding.UTF8.GetBytes("\u001b[?2026h\u001b[11;13H\u001b[?2026l"));
                    }
                });return;
            }
            if(bytes=="\u001b[H" && mode!="queued-home")Frame(true);
            if(bytes=="\u001b[F"){
                Frame(false);
                if(mode=="queued-enter"){
                    var gate=(SemaphoreSlim)h.Get("_ptyWriteLock")!;var held=gate.WaitAsync();
                    queued=Task.Run(async()=>{
                        await held;
                        try{
                            await Task.Delay(100);
                            screen.Feed(Encoding.UTF8.GetBytes("\u001b[?2026h\u001b[2;1H\u001b[KUnexpected decision needed\u001b[11;46H\u001b[?2026l"));
                        }finally{gate.Release();}
                    });
                }
            }
            var busySpinner=mode=="braille-spinner"?"⠋":mode=="held-spinner"?"⬝⬝⬝⬝⬝⬝⬝■":"■⬝⬝⬝⬝⬝⬝⬝";
            if(bytes=="\r" && mode=="busy-composer"){
                screen.Feed(Encoding.UTF8.GetBytes("\u001b[?2026h\u001b[6;1H\u001b[K"+" BUILD  ■⬝⬝⬝⬝⬝⬝⬝ esc interrupt".PadRight(89)+"ctrl+p cmd "+"\u001b[11;46H\u001b[?2026l"));return;
            }
            if(bytes=="\r" && mode=="busy-panel"){
                screen.Feed(Encoding.UTF8.GetBytes("\u001b[?2026h\u001b[2;1H\u001b[KUnexpected decision needed\u001b[13;1H\u001b[K"+(" BUILD  "+busySpinner+" esc interrupt").PadRight(89)+"ctrl+p cmd "+"\u001b[6;1H\u001b[?2026l"));return;
            }
            if(bytes=="\r")screen.Feed(Encoding.UTF8.GetBytes(mode=="unknown"?"\u001b[?1049h":"\u001b[?2026h\u001b[H\u001b[2J\u001b[2;1H█▀▀█  OpenCode\u001b[3;1H█  █  /workspace/qualification-1553-opencode-q1/empty-cli-scratch\u001b[4;1H▀▀▀▀\u001b[8;1H"+(" BUILD  "+busySpinner+" esc interrupt").PadRight(89)+"ctrl+p cmd "+"\u001b[6;1H\u001b[?2026l"));
        };
        Assert.True((await h.Rpc(new {command="setReady",ready=true})).GetProperty("ok").GetBoolean());
        JsonElement result;
        try{result=await h.Rpc(h.Request("one",text));}
        finally{if(mode=="denied-removal")File.SetUnixFileMode(h.Root,UnixFileMode.UserRead|UnixFileMode.UserWrite|UnixFileMode.UserExecute);}
        if(observer!=null)await observer;if(queued!=null)await queued;
        Assert.True(editor,"Actual original transaction must reach the standard editor gesture before witness/edge verdict.");
        if(mode is "success" or "unknown" or "restored-cursor" or "wrapped-edge" or "blocks-spinner" or "held-spinner" or "braille-spinner"){
            var operations=(System.Collections.IDictionary)h.Get("_promptOperations")!;
            var operation=operations["one"]!;var proof=operation.GetType().GetField("DraftProof")?.GetValue(operation);
            var proofTask=(Task?)proof?.GetType().GetProperty("Task")?.GetValue(proof);
            Assert.True(observerExit==0,$"Actual observer exit={observerExit}; reason={result.GetProperty("promptDelivery").GetProperty("reason")}; witness={proofTask?.Exception}");
        }
        Assert.Equal(expected,result.GetProperty("promptDelivery").GetProperty("disposition").GetString());
        var delivered=Encoding.UTF8.GetString(h.Input.Bytes);
        Assert.StartsWith("\u001b[200~"+text+"\u001b[201~\u0018e",delivered);
        if(mode is "success" or "unknown" or "restored-cursor" or "wrapped-edge" or "blocks-spinner" or "held-spinner" or "braille-spinner")
        {
            Assert.Equal(0,observerExit);Assert.Equal("\u001b[200~"+text+"\u001b[201~\u0018e\u001b[H\u001b[F\r",delivered);
        }
        else if(expected=="draft-uncertain")Assert.DoesNotContain('\r',delivered);
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
