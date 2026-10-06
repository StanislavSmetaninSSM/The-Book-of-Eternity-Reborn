using System.Text;
using System.Reflection;
using System.IO.Pipes;
using System.Text.Json;
using BookOfEternityClient.Services.GmRuntime;

namespace BookOfEternityClient.Tests;

// Each invocation is a fresh process beneath the unchanged independent guardian.
internal static class OwnedTerminalScenarioDriver
{
    internal static async Task<int> RunAsync(string mode, string package, string output)
    {
        if (mode is "terminal-bridge" or "terminal-uncertain" or "terminal-authority-loss" or "terminal-partial-start") return await RunBridgeAsync(mode, package, output);
        var result = new Dictionary<string, object?>();
        IOwnedTerminalSession? session = null;
        try
        {
            session = await OwnedTerminalSessionFactory.StartNeutralAsync(NeutralTerminalLaunch.Create(package,output), CancellationToken.None,
                mode == "terminal-gated-fds" ? pid => {
                    var descriptors=Directory.GetFiles($"/proc/{pid}/fd").Select(Path.GetFileName).ToArray();
                    result["HeldRootHasNoHelperChannels"] = !descriptors.Any(d=>d is "0" or "1" or "2");
                    if(descriptors.Any(d=>d is "0" or "1" or "2")) throw new InvalidOperationException("Gated terminal root still inherits helper control/status stdio.");
                } : null);
            var text = new StringBuilder();
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(6));
            var decoder = Encoding.UTF8.GetDecoder();
            async Task Until(string marker)
            {
                var buffer = new byte[4096];
                var chars = new char[Encoding.UTF8.GetMaxCharCount(buffer.Length)];
                while (!text.ToString().Contains(marker, StringComparison.Ordinal))
                {
                    var n = await session.OutputReader.ReadAsync(buffer, deadline.Token);
                    if (n == 0) throw new EndOfStreamException("Fixture output ended before required observation.");
                    var count = decoder.GetChars(buffer, 0, n, chars, 0, false); text.Append(chars, 0, count);
                }
            }
            async Task Write(string value) => await session.InputWriter.WriteAsync(Encoding.UTF8.GetBytes(value), deadline.Token);
            await Until("TTY_READY owned=1");
            await Write("one Ж😀\n"); await Until("RESULT1:one Ж😀");
            await Write("two\n"); await Until("RESULT2:two"); result["TwoInputs"] = true;
            await session.ResizeAsync(new(93, 31), deadline.Token); await Until("RESIZE 31x93"); result["Resize"] = true;
            await Write("canonical\n"); await Until("CANONICAL_READY");
            await Write("\u0004"); await Until("CANONICAL_EOF");
            result["EofStillAlive"] = !session.RootExited.IsCompleted;
            if (mode == "terminal-descendants") { await Write("descendants\n"); await Until("DESCENDANTS_READY"); }
            if (mode == "terminal-root-first") { await Write("root-exit\n"); await Until("DESCENDANTS_READY"); await session.RootExited.WaitAsync(deadline.Token); }
            var stop = await session.StopAndObserveAsync(deadline.Token); result["StopState"] = stop.State.ToString();
            if (mode == "terminal-retirement") {
                var owner=typeof(LinuxOwnedTerminalSession).GetField("_owner", BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(session)!;
                var dispose=session.DisposeAsync().AsTask(); await Task.Delay(50);
                if(dispose.IsCompletedSuccessfully || (bool)owner.GetType().GetField("_disposed",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(owner)!)
                    throw new InvalidOperationException("Native owner released before actual PTY EOF/operation settlement.");
                result["OwnerHeldBeforeEof"]=true;
                while(await session.OutputReader.ReadAsync(new byte[1024],deadline.Token)!=0) { }
                await dispose.WaitAsync(deadline.Token); session=null; return 0;
            }
            if (mode == "terminal-late-fault") {
                var owner=typeof(LinuxOwnedTerminalSession).GetField("_owner",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(session)!;
                owner.GetType().GetMethod("ReportTerminalFault", BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(owner,["controlled-late-io-fault"]);
                var late=await session.StopAndObserveAsync(CancellationToken.None);
                if(late.State!=BookOfEternityClient.Services.GmWorkers.GmWorkerStopState.Uncertain) throw new InvalidOperationException("Cached positive stop hid later original I/O uncertainty.");
                result["LateFaultUncertain"]=true; return 0;
            }
            while (await session.OutputReader.ReadAsync(new byte[1024], deadline.Token) != 0) { }
            await session.DisposeAsync(); session = null;
            result["Transcript"] = text.ToString();
            return 0;
        }
        catch (Exception ex)
        {
            result["Failure"] = ex.GetType().Name + ": " + ex.Message;
            if(ex is OwnedTerminalStartException partial) try { result["PartialOwnerCleanup"]=await partial.Owner.StopAndObserveAsync(CancellationToken.None); } catch { }
            if (session != null) try { result["Cleanup"] = await session.StopAndObserveAsync(CancellationToken.None); } catch { }
            return 1;
        }
        finally { await File.WriteAllTextAsync(Path.Combine(output, "scenario.json"), JsonSerializer.Serialize(result)); }
    }
    private static async Task<int> RunBridgeAsync(string mode, string package, string folder)
    {
        var result = new Dictionary<string, object?>(); object? host = null; Task? server = null;
        using var serverCancellation = new CancellationTokenSource();
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var repo = TestRepoPaths.RepoRoot;
        var type = Assembly.LoadFrom(Path.Combine(repo, "BookOfEternityGMBridge/bin", configuration, "net8.0/BookOfEternityGMBridge.dll"))
            .GetType("BookOfEternityGMBridge.BridgeHost", true)!;
        var pipe = "neutral-" + Guid.NewGuid().ToString("N");
        object? Invoke(string name, params object?[] args) => type.GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!.Invoke(host, args);
        async Task<JsonElement> Rpc(object request)
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            using var peer = new NamedPipeClientStream(".", pipe, PipeDirection.InOut, PipeOptions.Asynchronous);
            await peer.ConnectAsync(deadline.Token);
            await peer.WriteAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request) + "\n"), deadline.Token);
            await peer.FlushAsync(deadline.Token);
            using var reader = new StreamReader(peer, Encoding.UTF8, leaveOpen: true);
            using var doc = JsonDocument.Parse(await reader.ReadLineAsync(deadline.Token) ?? throw new EndOfStreamException());
            return doc.RootElement.Clone();
        }
        try
        {
            await File.WriteAllTextAsync(Path.Combine(folder, "config.json"), JsonSerializer.Serialize(new { GmCliInputProfile = new {
                IdleMarker="NEUTRAL READY", PromptPrefix="> ", WorkingMarker="NEUTRAL WORKING", ObservationTimeoutMilliseconds=1500 } }));
            var launch=NeutralTerminalLaunch.Create(package,folder);
            host = Activator.CreateInstance(type, [launch.Scratch, pipe]); Invoke("ConfigureNeutral", launch);
            if(mode=="terminal-partial-start")File.SetUnixFileMode(Path.Combine(package,"neutral-cli"),UnixFileMode.UserRead|UnixFileMode.UserWrite);
            try { await (Task)Invoke("StartShellAsync")!; }
            catch(OwnedTerminalStartException ex) when(mode=="terminal-partial-start") {
                result["PartialExceptionOriginal"]=ReferenceEquals(ex.Owner,type.GetField("_pty",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(host));
            }
            server = (Task)Invoke("RunServerLoopAsync", serverCancellation.Token)!;
            if(mode=="terminal-partial-start") {
                var status=await Rpc(new {command="status"});
                if(!status.GetProperty("status").GetProperty("terminalUncertain").GetBoolean())throw new InvalidOperationException("Partial original owner not visible as Uncertain.");
                if((await Rpc(new {command="addText",text="must-not-write"})).GetProperty("ok").GetBoolean())throw new InvalidOperationException("Partial owner admitted manual input.");
                result["PartialOwnerRetained"]=type.GetField("_pty",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(host)!=null;
                return 0;
            }
            for (var i=0;i<100;i++) {
                var status=(await Rpc(new { command="status" })).GetProperty("status");
                if (status.GetProperty("ready").GetBoolean()) break;
                await Task.Delay(10);
            }
            var ready = await Rpc(new { command="setReady", ready=true });
            result["Ready"] = ready;
            if (!ready.GetProperty("ok").GetBoolean()) throw new InvalidOperationException("Actual session output did not produce a reliable idle view.");
            var binding = ready.GetProperty("status").GetProperty("inputBindingId").GetString(); result["Binding"] = binding;
            object Prompt(string command, string id, string text) => new { command, operationId=id, operationKind="turn", operationRevision="neutral-1", inputBindingId=binding, text, appendEnter=true };
            string? Disposition(JsonElement r) => r.GetProperty("promptDelivery").GetProperty("disposition").GetString();
            void Require(bool condition, string failure) { if (!condition) throw new InvalidOperationException(failure); }
            async Task Idle() {
                for (var i=0;i<150;i++) { if ((await Rpc(new { command="status" })).GetProperty("status").GetProperty("ready").GetBoolean()) return; await Task.Delay(10); }
                throw new TimeoutException("Actual view did not return to empty idle.");
            }
            var originalSession=type.GetField("_pty", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(host)!;
            if(mode=="terminal-authority-loss") {
                var owner=(BookOfEternityClient.Services.GmWorkers.NativeLineageOwner)typeof(LinuxOwnedTerminalSession).GetField("_owner",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(originalSession)!;
                var supervisor=(System.Diagnostics.Process)owner.GetType().GetField("_supervisor",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(owner)!;
                supervisor.StandardOutput.Close();
                // Closing the actual status reader withdraws this owner, while root/master still live.
                for(var i=0;i<100 && owner.Uncertainty==null;i++)await Task.Delay(10);
                Require(owner.Uncertainty!=null,"Actual status close did not retire its observer.");
                Require(!((IOwnedTerminalSession)originalSession).RootExited.IsCompleted,"Loss negative requires live root.");
                Require(!(await Rpc(new {command="setReady",ready=true})).GetProperty("ok").GetBoolean(),"Old view accepted after original authority loss.");
                Require(Disposition(await Rpc(Prompt("dispatchPrompt","lost","must-not-write")))=="not-written","Dispatch admitted after authority loss.");
                Require(!(await Rpc(new {command="addText",text="must-not-write"})).GetProperty("ok").GetBoolean(),"Manual writer admitted after original authority loss.");
                Require(ReferenceEquals(originalSession,type.GetField("_pty",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(host)),"Authority loss removed original session.");
                result["LiveAuthorityLossBlocked"]=true;return 0;
            }
            if (mode == "terminal-uncertain") {
                var owner=typeof(LinuxOwnedTerminalSession).GetField("_owner", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(originalSession)!;
                await (Task)owner.GetType().GetMethod("SendControlAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(owner, ['U'])!;
                try { await (Task)Invoke("StopShellAsync")!; throw new InvalidOperationException("Uncertain original owner was accepted."); }
                catch (TimeoutException) { }
                Require(ReferenceEquals(originalSession,type.GetField("_pty", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(host)),"Original Uncertain owner lost.");
                Require(!(await Rpc(new { command="setReady", ready=true })).GetProperty("ok").GetBoolean(),"Uncertain owner became ready.");
                result["UncertainRetained"]=true; return 0;
            }
            var first=await Rpc(Prompt("dispatchPrompt","first","one Ж😀"));
            Require(Disposition(first)=="submission-observed","First original T042 submission not observed: "+first); await Idle();
            Require(Disposition(await Rpc(Prompt("dispatchPrompt","first","one Ж😀")))=="submission-observed","Retained duplicate changed outcome.");
            var second=await Rpc(Prompt("dispatchPrompt","second","two"));
            Require(Disposition(second)=="submission-observed","Second original T042 submission not observed: "+second); await Idle();
            Require(ReferenceEquals(originalSession,type.GetField("_pty", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(host)),"Two prompts used another process/session.");
            result["TwoDispatchesOneSession"]=true;
            await Rpc(new { command="addText", text="draft" });
            Require(Disposition(await Rpc(Prompt("dispatchPrompt","draft","automatic")))=="not-written","Manual draft was overwritten.");
            Require(!(await Rpc(new { command="setReady", ready=true })).GetProperty("ok").GetBoolean(),"Nonempty draft was accepted as idle.");
            await Rpc(new { command="addText", text="\u007f\u007f\u007f\u007f\u007f" }); await Task.Delay(50);
            Require((await Rpc(new { command="setReady", ready=true })).GetProperty("ok").GetBoolean(),"Explicit manual backspace did not restore idle.");
            result["DraftPreserved"]=true;
            var gate=(SemaphoreSlim)type.GetField("_promptGate",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(host)!;
            async Task Admitted() { for(var i=0;i<100;i++) { if((int)type.GetField("_admittedPrompts",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(host)!>0)return;await Task.Delay(5); } throw new TimeoutException("Queued operation not admitted."); }
            await gate.WaitAsync();
            try {
                var queued=Rpc(Prompt("dispatchPrompt","cancel","cancelled")); await Admitted();
                await Rpc(Prompt("cancelPrompt","cancel","cancelled"));
                Require(Disposition(await queued)=="queued-cancelled","Real pipe cancel reached another operation."); result["CancelledViaPipe"]=true;
            } finally { gate.Release(); }
            await gate.WaitAsync();
            Task<JsonElement>? manual=null;
            try {
                var queued=Rpc(Prompt("dispatchPrompt","takeover","automatic")); await Admitted();
                manual=Rpc(new { command="addText",text="m" });
                Require(Disposition(await queued)=="queued-cancelled","Real pipe manual takeover failed."); result["TakeoverViaPipe"]=true;
            } finally { gate.Release(); }
            await manual!;
            for(var i=0;i<100;i++) { var view=(await Rpc(new {command="diagnostics"})).GetProperty("diagnostics").GetProperty("visibleScreenText").GetString();if(view?.Contains("> m",StringComparison.Ordinal)==true)break;await Task.Delay(5); }
            Require(!(await Rpc(new { command="setReady", ready=true })).GetProperty("ok").GetBoolean(),"Manual takeover draft was discarded.");
            await Rpc(new { command="resize", columns=93, rows=31 });
            result["ActualResizeViaPipe"]=true;
            var stopped=await Rpc(new {command="stopTerminal"});
            var proof=stopped.GetProperty("status").GetProperty("terminalStop");
            Require(proof.GetProperty("state").GetString()=="stopped-within-scope" && proof.GetProperty("cleanupComplete").GetBoolean() && !stopped.GetProperty("status").GetProperty("terminalOwnerRetained").GetBoolean(),"Real scoped-stop RPC did not retire its original owner.");
            result["ScopedStopViaPipe"]=true;
            return 0;
        }
        catch (Exception ex) { result["Failure"] = ex.ToString(); return 1; }
        finally {
            if (host != null) {
                try { await (Task)Invoke("StopShellAsync")!; result["ScopedRetired"] = type.GetField("_pty", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(host) == null; }
                catch (Exception ex) { result["CleanupFailure"] = ex.ToString(); }
            }
            await serverCancellation.CancelAsync(); if (server != null) await server;
            if (host is IDisposable disposable) disposable.Dispose();
            await File.WriteAllTextAsync(Path.Combine(folder, "scenario.json"), JsonSerializer.Serialize(result));
        }
    }

}
