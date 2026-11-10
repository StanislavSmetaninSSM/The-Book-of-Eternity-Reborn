#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using BookOfEternityClient.Tests;
using D = BookOfEternityClient.Tests.JointInterfaceDiagnosticDelivery;

internal static partial class D01Checks
{
    private static readonly D.Expected Expected = new(10, 20, "/disposable/d01", "11111111111111111111111111111111", 1);
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static string Line(string stage = "finalization-return", string? detail = D.Custody, long elapsed = 1,
        D.Expected? identity = null, string boundary = D.Boundary, string utc = "2026-10-18T00:00:00Z")
    {
        var e = identity ?? Expected;
        return JsonSerializer.Serialize(new { Boundary = boundary, Stage = stage, Detail = detail, Pid = e.Pid,
            BirthTick = e.BirthTick, Root = e.Root, Nonce = e.Nonce, Cut = e.Cut, Utc = utc, ElapsedMilliseconds = elapsed });
    }
    private static List<D.Event> Take(D delivery)
    {
        var events = new List<D.Event>();
        while (delivery.Reader.TryRead(out var e)) events.Add(e);
        return events;
    }
    private static async Task<D> Drain(string text, int capacity = D.Capacity)
    {
        var delivery = new D(Expected, capacity);
        Require(await delivery.DrainAsync(new StringReader(text)) == text, "decoded raw stdout changed");
        return delivery;
    }
    private sealed class GateReader(string text) : TextReader
    {
        private int _offset;
        internal readonly TaskCompletionSource<bool> First = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource<bool> End = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async Task<int> ReadAsync(char[] buffer, int index, int count)
        {
            if (_offset < text.Length)
            {
                var n = Math.Min(count, text.Length - _offset); text.CopyTo(_offset, buffer, index, n); _offset += n;
                First.TrySetResult(true); return n;
            }
            await End.Task; return 0;
        }
    }
    private sealed class ChunkReader(string text) : TextReader
    {
        private int _offset;
        public override Task<int> ReadAsync(char[] buffer, int index, int count)
        {
            if (_offset == text.Length) return Task.FromResult(0);
            buffer[index] = text[_offset++]; return Task.FromResult(1); // Includes split UTF16 surrogate pair.
        }
    }
    private sealed class FaultReader(IOException failure) : TextReader
    { public override Task<int> ReadAsync(char[] buffer, int index, int count) => Task.FromException<int>(failure); }
    private sealed class CountReader(string text) : StringReader(text)
    {
        internal int WholeReads;
        public override Task<string> ReadToEndAsync() { WholeReads++; return base.ReadToEndAsync(); }
    }

    private sealed record CheckResult(string Name, bool Passed, string? Failure);
    private static async Task<List<CheckResult>> NonLive()
    {
        var results = new List<CheckResult>();
        async Task Check(string name, Func<Task> check)
        {
            try { await check(); results.Add(new(name, true, null)); }
            catch (Exception e) { results.Add(new(name, false, e.ToString())); }
        }
        await Check("live-in-memory-before-EOF", async () =>
        {
            var text = Line() + "\n"; using var reader = new GateReader(text); var d = new D(Expected);
            var raw = d.DrainAsync(reader);
            try
            {
                await reader.First.Task.WaitAsync(TimeSpan.FromSeconds(2));
                var e = await d.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(2));
                Require(e.Control == "CUSTODY_RETURN" && !raw.IsCompleted && !d.ReadHealth().Eof, "no live delivery before EOF");
            }
            finally { reader.End.TrySetResult(true); }
            Require(await raw.WaitAsync(TimeSpan.FromSeconds(2)) == text && d.ReadHealth().Eof, "raw EOF/join changed");
        });
        await Check("decoded-chunks-Unicode-CRLF-offsets", async () =>
        {
            var text = "префикс 🦉\r\n\n" + Line() + "\r\nобычный stdout\n"; var d = new D(Expected);
            Require(await d.DrainAsync(new ChunkReader(text)) == text, "chunk text changed");
            var e = Take(d).Single(); Require(e.LineOrdinal == 3 && e.CharacterOffset == text.IndexOf('{') && !d.ReadHealth().Uncertain, "line/offset changed");
        });
        await Check("identity-five-fields-rejected", async () =>
        {
            var variants = new[] { Expected with { Pid = 11 }, Expected with { BirthTick = 21 },
                Expected with { Root = Expected.Root + "/other" }, Expected with { Nonce = new string('2',32) }, Expected with { Cut = 2 } };
            var d = await Drain(string.Concat(variants.Select(v => Line(identity:v) + "\n")));
            Require(d.ReadHealth().IdentityRejected == 5 && d.ReadHealth().Uncertain && Take(d).Count == 0, "foreign identity admitted");
        });
        await Check("ordinary-and-other-Boundary-no-control", async () =>
        {
            var d = await Drain("ordinary text\n" + Line(detail:D.Boundary, boundary:"other") + "\n");
            Require(Take(d).Count == 0 && !d.ReadHealth().CustodySeen && !d.ReadHealth().AcceptedSeen, "ordinary output triggered control");
        });
        await Check("malformed-candidate-raw-retained", async () =>
        { var d = await Drain("{" + D.Boundary + "\n" + Line() + "\n"); Require(d.ReadHealth().Malformed == 1 && d.ReadHealth().Uncertain && Take(d).Single().Control == "CUSTODY_RETURN", "parse fault stopped raw/live drain"); });
        await Check("oversize-unknown-with-raw-retained", async () =>
        { var d = await Drain(new string('x',D.LineLimit+1) + D.Boundary + "\n"); Require(d.ReadHealth().Oversize == 1 && d.ReadHealth().Uncertain && Take(d).Count == 0, "oversize falsely accepted"); });
        await Check("incomplete-tail-is-not-event", async () =>
        { var d = await Drain(Line()); Require(d.ReadHealth().Eof && d.ReadHealth().IncompleteTail == 1 && d.ReadHealth().ExpectedCustody == "UNOBSERVED" && Take(d).Count == 0, "partial EOF became event"); });
        await Check("exact-duplicate-is-stale-control", async () =>
        { var d = await Drain(Line()+"\n"+Line()+"\n"); var es=Take(d); Require(es.Count==2 && es[1].Disposition=="DUPLICATE" && es[1].Control=="NONE" && d.ReadHealth().Duplicates==1,"duplicate triggered"); });
        await Check("elapsed-regression-rejected", async () =>
        { var d = await Drain(Line(elapsed:2)+"\n"+Line(stage:"engine-enter",elapsed:1)+"\n"); Require(Take(d)[1].Disposition=="STALE_ORDER" && d.ReadHealth().ElapsedRegressions==1,"elapsed regression admitted"); });
        await Check("equal-milliseconds-different-markers-valid", async () =>
        {
            var d=await Drain(Line()+"\n"+Line("finalization-enter",D.Accepted)+"\n"+Line("finalization-return",D.Accepted)+"\n");
            var es=Take(d); Require(es.Count==3 && es[1].Control=="ACCEPTED_MARKER" && es[2].Control=="ACCEPTED_MARKER" && !d.ReadHealth().Uncertain,"equal milliseconds wrongly stale");
        });
        await Check("overflow-loss-and-cancel-health-outside-channel", async () =>
        {
            var d=await Drain(Line("engine-enter",null)+"\n"+Line("finalization-enter",D.Accepted)+"\n",1);
            var es=Take(d);var h=d.ReadHealth();Require(es.Count==1 && h.Dropped==1 && h.Uncertain && h.AcceptedSeen && h.Eof,"full queue hid cancellation/EOF");
        });
        await Check("bounded-fingerprint-tracking-exhaustion-explicit", async () =>
        {
            var d=await Drain(string.Concat(Enumerable.Range(0,D.FingerprintLimit+1).Select(i=>Line("stage"+i,null,i)+"\n")));
            Require(d.ReadHealth().TrackingExhausted==1 && d.ReadHealth().Uncertain,"tracking silently stopped");
        });
        await Check("construction-fallback-single-original-reader", async () =>
        {
            using var reader=new CountReader("whole raw\r\n"); var raw=D.Start(reader,Expected,out var d,out var error,capacity:0);
            Require(await raw=="whole raw\r\n" && d is null && error=="ArgumentOutOfRangeException" && reader.WholeReads==1,"constructor fault changed raw path");
        });
        await Check("raw-reader-failure-not-EOF", async () =>
        {
            var failure=new IOException("own synthetic read fault");var d=new D(Expected);bool exact=false;
            try{await d.DrainAsync(new FaultReader(failure));}catch(IOException e){exact=ReferenceEquals(e,failure);}
            Require(exact && d.ReadHealth().RawReadFault && !d.ReadHealth().Eof && d.ReadHealth().Uncertain,"raw fault disguised");
        });
        await Check("consumer-continuation-not-inline-on-publisher", async () =>
        {
            var d=new D(Expected);int publisher=0;Exception? failure=null;
            var received=d.Reader.ReadAsync().AsTask().ContinueWith(_=>Environment.CurrentManagedThreadId,CancellationToken.None,TaskContinuationOptions.ExecuteSynchronously,TaskScheduler.Default);
            var thread=new Thread(()=>{publisher=Environment.CurrentManagedThreadId;try{d.DrainAsync(new StringReader(Line()+"\n")).GetAwaiter().GetResult();}catch(Exception e){failure=e;}});
            thread.Start();Require(thread.Join(2000),"publisher did not join");
            Require(await received.WaitAsync(TimeSpan.FromSeconds(2))!=publisher && failure is null,"observer continuation executed on dedicated raw publisher");
        });
        await Check("actual-queue-residence-separate-from-source-age", async () =>
        {
            var d=await Drain(Line()+"\n");await Task.Delay(25);var e=Take(d).Single();var t=D.QueueResidence(e,TimeSpan.FromMilliseconds(10));
            Require(t.Delayed && t.ResidenceMilliseconds>=10 && t.SourceToReceiveLatency=="UNKNOWN" && t.CapturePhase=="UNKNOWN","timing fabricated source age/phase");
        });
        await Check("duplicate-reader-never-started", async () =>
        {
            var d=await Drain(Line()+"\n");bool rejected=false;using var second=new CountReader("second");
            try{await d.DrainAsync(second);}catch(InvalidOperationException){rejected=true;}
            Require(rejected && second.WholeReads==0 && d.ReadHealth().Eof,"replacement reader started");
        });
        await Check("typed-control-and-duplicate-properties", async () =>
        {
            var wrongStage=JsonNode.Parse(Line())!;wrongStage["Stage"]=42;
            var wrongDetail=JsonNode.Parse(Line())!;wrongDetail["Detail"]=new JsonArray();
            var duplicate=Line()[..^1]+",\"Pid\":10}";
            var d=await Drain(wrongStage.ToJsonString()+"\n"+wrongDetail.ToJsonString()+"\n"+duplicate+"\n");
            Require(d.ReadHealth().Malformed==3 && Take(d).Count==0 && !d.ReadHealth().CustodySeen,"untyped/ambiguous control accepted");
        });
        await Check("wall-age-does-not-become-monotonic-delay", async () =>
        {
            var d=await Drain(Line(utc:"2000-01-01T00:00:00Z")+"\n");var t=D.QueueResidence(Take(d).Single(),TimeSpan.MaxValue);
            Require(!t.Delayed && t.SourceToReceiveLatency=="UNKNOWN" && d.ReadHealth().SourceEmissionCompleteness=="UNKNOWN","wall clock treated as age proof");
        });
        await Check("consumer-cancel-does-not-cancel-raw-drain", async () =>
        {
            var d=new D(Expected);using var cancellation=new CancellationTokenSource();cancellation.Cancel();bool cancelled=false;
            try{await d.Reader.ReadAsync(cancellation.Token);}catch(OperationCanceledException){cancelled=true;}
            Require(await d.DrainAsync(new StringReader(Line()+"\n"))==Line()+"\n" && cancelled && Take(d).Count==1 && !d.ReadHealth().Uncertain,"consumer cancellation changed raw writer");
        });
        return results;
    }

    private static async Task<int> Main(string[] args)
    {
        if(args.Length!=1 || args[0]!="pure") return 64; // Real child entrypoints are not implemented/released here.
        var checks=await NonLive();Console.WriteLine(JsonSerializer.Serialize(new {Kind="D01_NONLIVE_HELPER_CHECKS",Checks=checks,
            FixtureSubprocessTargets=0,GameProcesses=0,DiagnosticRequests=0,SourceEmissionCompleteness="UNKNOWN",CapturePhase="UNKNOWN"}));
        return checks.Count == 20 && checks.All(x => x.Passed) ? 0 : 2;
    }
}
