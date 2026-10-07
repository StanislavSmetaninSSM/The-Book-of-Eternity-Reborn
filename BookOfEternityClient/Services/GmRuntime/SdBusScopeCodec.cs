using Microsoft.Win32.SafeHandles;
namespace BookOfEternityClient.Services.GmRuntime;

internal interface ISdBusMessageWriter
{
    void Open(char type,string signature);void Close();
    void String(string value);void Boolean(bool value);void UInt64(ulong value);void UnixFd(SafeFileHandle value);
}
internal static class SdBusScopeCodec
{
    internal static void Write(SystemdScopeRequest request,ISdBusMessageWriter writer)
    {
        if(!request.Name.StartsWith("boe-main-",StringComparison.Ordinal) || !request.Name.EndsWith(".scope",StringComparison.Ordinal) ||
            !Guid.TryParseExact(request.Name[9..^6],"N",out var run) || run==Guid.Empty || request.Pidfd.IsClosed || request.Pidfd.IsInvalid)
            throw new InvalidDataException("Only one original run scope and retained FD are encodable.");
        writer.String(request.Name);writer.String(request.Mode);writer.Open('a',"(sv)");
        Property("PIDFDs","ah",()=>{writer.Open('a',"h");writer.UnixFd(request.Pidfd);writer.Close();});
        Property("AddRef","b",()=>writer.Boolean(true));
        Property("KillMode","s",()=>writer.String("control-group"));
        Property("SendSIGKILL","b",()=>writer.Boolean(true));
        Property("TimeoutStopUSec","t",()=>writer.UInt64(2_000_000));
        writer.Close();writer.Open('a',"(sa(sv))");writer.Close();
        void Property(string name,string signature,Action value) {
            writer.Open('r',"sv");writer.String(name);writer.Open('v',signature);value();writer.Close();writer.Close();
        }
    }
}
// One original scope's bounded matching receipts, including signal-before-reply.
internal sealed class SystemdJobReceipts(string owner,string scope)
{
    private readonly Dictionary<string,string> _jobs=new(StringComparer.Ordinal);
    internal void Observe(string sender,string unit,string path,uint id,string result)
    {
        if(unit!=scope)return;
        if(sender!=owner || path!="/org/freedesktop/systemd1/job/"+id || _jobs.Count>=8 ||
            (_jobs.TryGetValue(path,out var previous) && previous!=result))throw new IOException("Original systemd job signal identity invalid.");
        _jobs[path]=result;
    }
    internal bool Complete(string path) {
        if(!_jobs.TryGetValue(path,out var result))return false;
        if(result!="done")throw new IOException("Original systemd job did not complete successfully.");return true;
    }
}
