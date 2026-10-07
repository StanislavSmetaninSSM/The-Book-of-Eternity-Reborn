using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Services.GmRuntime;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class GmExternalDraftObservationTests
{
    [Fact]
    public async Task ActualPipe_MiniEmptyStartupNeedsConsumedProfileAndPlaceholderStyle()
    {
        await using var h=new GmBridgePromptOperationTests.PromptHostFixture();
        using var screen=InstallMini(h);
        screen.Feed(GmSynchronizedTerminalPresentationTests.ActualTranscript("startup"));
        var reply=await h.Rpc(new {command="setReady",ready=true});
        Assert.True(reply.GetProperty("ok").GetBoolean(),"Actual pinned empty composer requires consumed bounded profile, not a supplied Ready override.");
        // Same words as the placeholder, now real typed text colour: preserve draft.
        screen.Feed(Encoding.UTF8.GetBytes("\u001b[?2026h\u001b[6;1H\u001b[38;2;226;232;240mAsk anything... \"Fix a TODO in the codebase\"\u001b[6;1H\u001b[?2026l"));
        Assert.False((await h.Rpc(new {command="setReady",ready=true})).GetProperty("ok").GetBoolean());
        Assert.Empty(h.Input.Bytes);
    }

    internal static GmSynchronizedTerminalPresentationTests.ScreenProbe InstallMini(GmBridgePromptOperationTests.PromptHostFixture h)
    {
        var profile=new {TerminalPresentation="synchronized-mini-v1", DraftObservation="external-editor-v1", DraftDirectory=h.Root,
            IdleMarker=" BUILD",WorkingMarker="esc interrupt",PromptPrefix="",AutomaticSubmissionLimit=1,
            StartupBannerLines=new[]{"","█▀▀█  OpenCode","█  █  /workspace/qualification-1553-opencode-q1/empty-cli-scratch","▀▀▀▀",""},
            ObservationTimeoutMilliseconds=1000,BlockedMarkers=new[]{"trust","authentication","sign in"}};
        File.WriteAllText(Path.Combine(h.Root,"config.json"),JsonSerializer.Serialize(new {GmCliInputProfile=profile}));
        var screen=new GmSynchronizedTerminalPresentationTests.ScreenProbe(h.BindingId);
        h.HostType.GetField("_terminalScreen",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.SetValue(h.Host,screen.Screen);
        h.HostType.GetField("_promptScreenReader",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.SetValue(h.Host,(Func<string>)(()=>GmSynchronizedTerminalPresentationTests.Value<bool>(screen.Capture(),"Reliable")?GmSynchronizedTerminalPresentationTests.Value<string>(screen.Capture(),"Text"):""));
        return screen;
    }

    [Fact]
    public async Task ActualPipe_FirstBlankFrameCannotBorrowStartupReadiness()
    {
        await using var h=new GmBridgePromptOperationTests.PromptHostFixture();using var screen=InstallMini(h);
        screen.Feed(GmSynchronizedTerminalPresentationTests.ActualTranscript("startup"));
        screen.Feed(Encoding.UTF8.GetBytes("\u001b[?2026h\u001b[6;1H\u001b[K\u001b[?2026l"));
        Assert.False((await h.Rpc(new {command="setReady",ready=true})).GetProperty("ok").GetBoolean());
        Assert.Empty(h.Input.Bytes);
    }

    [Fact]
    public async Task ActualPipe_ManualMultilineBlankTailPreservesDraftAndTakeover()
    {
        await using var h=new GmBridgePromptOperationTests.PromptHostFixture();using var screen=InstallMini(h);
        screen.Feed(GmSynchronizedTerminalPresentationTests.ActualTranscript("startup"));
        Assert.True((await h.Rpc(new {command="addText",text="manual draft\n\n"})).GetProperty("ok").GetBoolean());
        var frame="\u001b[?2026h\u001b[H\u001b[2J\u001b[6;1Hmanual draft\u001b[10;1H"+" BUILD  ".PadRight(89)+"ctrl+p cmd "+"\u001b[8;1H\u001b[?25h\u001b[?2026l";
        screen.Feed(Encoding.UTF8.GetBytes(frame));
        Assert.False((await h.Rpc(new {command="setReady",ready=true})).GetProperty("ok").GetBoolean());
        Assert.Equal("manual draft\n\n",Encoding.UTF8.GetString(h.Input.Bytes));
        Assert.True((bool)h.Binding.GetType().GetField("ManualTakeover")!.GetValue(h.Binding)!);
    }

    [Fact]
    public async Task ActualPipe_UnknownPanelCannotBorrowUnchangedComposerFooter()
    {
        await using var h=new GmBridgePromptOperationTests.PromptHostFixture();using var screen=InstallMini(h);
        screen.Feed(GmSynchronizedTerminalPresentationTests.ActualTranscript("startup"));
        screen.Feed(Encoding.UTF8.GetBytes("\u001b[?2026h\u001b[2;1H\u001b[KUnexpected decision needed\u001b[6;1H\u001b[?2026l"));
        Assert.False((await h.Rpc(new {command="setReady",ready=true})).GetProperty("ok").GetBoolean());
        Assert.Empty(h.Input.Bytes);
    }

    [Fact]
    public async Task ActualBridgeObserver_OriginalAbsolutePipe_ReadsActualUnicodeFileWithoutExpectedBytes()
    {
        var dir=Path.Combine(Path.GetTempPath(),"boe-editor-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
        var file=Path.Combine(dir,"1234567890123.md");var text="actual read-only draft\nКириллица café";await File.WriteAllTextAsync(file,text,new UTF8Encoding(false));
        var pipeName="boe-editor-"+Guid.NewGuid().ToString("N");var absolute=Path.Combine(Path.GetTempPath(),"CoreFxPipe_"+pipeName);
        using var pipe=new NamedPipeServerStream(pipeName,PipeDirection.InOut,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous);
        using var lifetime=new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var configuration=new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var bridge=Path.Combine(TestRepoPaths.RepoRoot,"BookOfEternityGMBridge/bin",configuration,"net8.0/BookOfEternityGMBridge.dll");
        var start=new ProcessStartInfo(Path.Combine(Environment.GetEnvironmentVariable("DOTNET_ROOT")!,"dotnet")){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};
        foreach(var a in new[]{bridge,"--observe-draft",file})start.ArgumentList.Add(a);
        start.Environment["BOE_DRAFT_PIPE"]=absolute;start.Environment["BOE_DRAFT_BINDING"]="own-launch-binding";start.Environment["BOE_DRAFT_DIRECTORY"]=dir;
        // Child TMPDIR differs from original bridge endpoint: supplied absolute pipe is essential.
        var childTmp=Path.Combine(dir,"child-tmp");Directory.CreateDirectory(childTmp);start.Environment["TMPDIR"]=childTmp;
        using var child=Process.Start(start)!;
        try {
            var connected=pipe.WaitForConnectionAsync(lifetime.Token);
            if(await Task.WhenAny(connected,child.WaitForExitAsync(lifetime.Token))!=connected)
                Assert.Equal(0,child.ExitCode); // Causal baseline exits usage1 before any witness.
            await connected;
            var reader=new MainOperationReader(pipe);var hello=await reader.ReadAsync<JsonElement>(lifetime.Token);
            Assert.Equal("observeDraft",hello.GetProperty("command").GetString());Assert.Equal("own-launch-binding",hello.GetProperty("binding").GetString());
            await MainOperationReader.WriteAsync(pipe,new {ok=true,nonce="own-connected-operation"},lifetime.Token);
            var proof=await reader.ReadAsync<JsonElement>(lifetime.Token,100000);
            Assert.Equal("own-connected-operation",proof.GetProperty("nonce").GetString());
            Assert.Equal(Encoding.UTF8.GetBytes(text),Convert.FromBase64String(proof.GetProperty("bytes").GetString()!));
            await MainOperationReader.WriteAsync(pipe,new {ok=true},lifetime.Token);
            await child.WaitForExitAsync(lifetime.Token);Assert.Equal(0,child.ExitCode);
            Assert.Equal(text,await File.ReadAllTextAsync(file));
        } finally {
            lifetime.Cancel();pipe.Dispose();if(!child.HasExited){child.Kill();await child.WaitForExitAsync();}
            Directory.Delete(dir,true);
        }
    }
}
