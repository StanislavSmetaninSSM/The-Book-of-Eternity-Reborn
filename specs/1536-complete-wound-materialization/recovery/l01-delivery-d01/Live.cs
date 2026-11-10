#nullable enable
using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using D = BookOfEternityClient.Tests.JointInterfaceDiagnosticDelivery;

// Both roles are direct children of the external owner. No Process.Start/game/attach API.
internal static class D01Live
{
    private static ulong Tick()
    {
        var stat=File.ReadAllText("/proc/self/stat");
        return ulong.Parse(stat[(stat.LastIndexOf(')')+2)..].Split(' ',StringSplitOptions.RemoveEmptyEntries)[19],System.Globalization.CultureInfo.InvariantCulture);
    }
    private static string Hash(string text)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
    private static int Child(string root,string nonce,int exit)
    {
        using var watchdog=new Timer(_=>Environment.Exit(124),null,15000,Timeout.Infinite);
        var marker=JsonSerializer.Serialize(new {Boundary=D.Boundary,Stage="finalization-return",Detail=D.Custody,
            Pid=Environment.ProcessId,BirthTick=Tick(),Root=root,Nonce=nonce,Cut=1,Utc=DateTimeOffset.UtcNow,ElapsedMilliseconds=1});
        var text="ordinary 🦉\r\n"+(exit==7?"{"+D.Boundary+"\n":"")+marker+"\n";
        Console.Out.Write(text);Console.Out.Flush();
        Console.Error.WriteLine(JsonSerializer.Serialize(new {Kind="D01_OWN_CHILD",Pid=Environment.ProcessId,
            BirthTick=Tick(),Root=root,Nonce=nonce,ExpectedExit=exit,RawStdoutSha256=Hash(text+"done\n")}));Console.Error.Flush();
        if(Console.ReadLine()!="STOP:"+nonce)return 65;
        Console.Out.Write("done\n");Console.Out.Flush();return exit;
    }
    private static async Task<int> Read(string manifest)
    {
        using var watchdog=new Timer(_=>Environment.Exit(124),null,20000,Timeout.Infinite);
        using var configuration=JsonDocument.Parse(File.ReadAllText(manifest));var e=configuration.RootElement;
        var expected=new D.Expected(e.GetProperty("Pid").GetInt32(),e.GetProperty("BirthTick").GetUInt64(),
            e.GetProperty("Root").GetString()!,e.GetProperty("Nonce").GetString()!,1);
        // StreamReader over the inherited target pipe; Console.In's synchronous wrapper is not used.
        using var input=new StreamReader(Console.OpenStandardInput(),new UTF8Encoding(false),true,4096,leaveOpen:true);
        var raw=D.Start(input,expected,out var delivery,out var constructionFailure);
        string? failure=constructionFailure;string? text=null;
        try
        {
            if(delivery is null)throw new InvalidOperationException("Live delivery unavailable.");
            var item=await delivery.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3));
            // Publication is on the consumer, never the raw reader. No fixture/game control here.
            Console.WriteLine(JsonSerializer.Serialize(new {Kind="D01_LIVE_PULSE",ReaderPid=Environment.ProcessId,
                Event=item,Health=delivery.ReadHealth(),RawPending=!raw.IsCompleted}));Console.Out.Flush();
        }
        catch(Exception error){failure??=error.GetType().Name+": "+error.Message;}
        try{text=await raw.WaitAsync(TimeSpan.FromSeconds(8));}
        catch(Exception error){failure??=error.GetType().Name+": "+error.Message;}
        Console.WriteLine(JsonSerializer.Serialize(new {Kind="D01_READER_TERMINAL",ReaderPid=Environment.ProcessId,
            Health=delivery?.ReadHealth(),Failure=failure,RawStdout=text,RawStdoutSha256=text is null?null:Hash(text),
            Runtime=System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            Mvid=typeof(D01Live).Assembly.ManifestModule.ModuleVersionId,SourceEmissionCompleteness="UNKNOWN",CapturePhase="UNKNOWN"}));Console.Out.Flush();
        return failure is null?0:2;
    }
    private static async Task<int> Main(string[] args)
    {
        if(args.Length==4 && args[0]=="--child" && (args[3]=="0" || args[3]=="7") && Guid.TryParseExact(args[2],"N",out _))
            return Child(args[1],args[2],int.Parse(args[3]));
        if(args.Length==2 && args[0]=="read")return await Read(args[1]);
        return 64;
    }
}
