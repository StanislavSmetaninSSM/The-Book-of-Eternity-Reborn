using System.Text;
using System.Text.Json;

namespace BookOfEternityClient.Services.GmRuntime;

internal enum MainOperationState { PreparedGrant, Active, Closing, ClosedObserved, Unresolved }
internal enum MainOperationOutcome { Completed, Failed, Cancelled, Committed, RolledBack, Uncertain, NotLoaded }
internal sealed record MainOperationClose(string PinId,string CloseId,string OperationId,GmSessionRunIdentity Identity,
    MainOperationOutcome Outcome,bool ClosingFailed);
internal sealed record MainOperationReply(bool Ok,MainOperationState? State=null,string? PinId=null,string? CloseId=null,
    string? OperationId=null,GmSessionRunIdentity? Identity=null,string? Error=null);
internal sealed class MainOperationFrame
{
    public string? Command {get;set;}
    public MainOperationClose? Close {get;set;}
    public string? PinId {get;set;}
    public string? CloseId {get;set;}
    public string? OperationId {get;set;}
    public GmSessionRunIdentity? Identity {get;set;}
}
// One reader per original connection. No buffering beyond a complete bounded frame,
// strict UTF8, no ReadLineAsync/StreamReader replacement that can discard a second frame.
internal sealed class MainOperationReader(Stream stream)
{
    internal static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web);
    private readonly byte[] _one=new byte[1];
    internal async Task<T?> ReadAsync<T>(CancellationToken token,int maximum=65536)
    {
        using var bytes=new MemoryStream();
        while(true) {
            var n=await stream.ReadAsync(_one,token);
            if(n==0){if(bytes.Length==0)return default;throw new IOException("Incomplete operation frame.");}
            if(_one[0]==10)break;
            if(bytes.Length>=maximum)throw new InvalidDataException("Operation frame exceeds bound.");bytes.WriteByte(_one[0]);
        }
        var text=new UTF8Encoding(false,true).GetString(bytes.ToArray());
        return JsonSerializer.Deserialize<T>(text,Json)??throw new InvalidDataException("Empty operation frame.");
    }
    internal static async Task WriteAsync<T>(Stream stream,T value,CancellationToken token,int maximum=65536)
    {
        var bytes=JsonSerializer.SerializeToUtf8Bytes(value,Json);
        if(bytes.Length>maximum)throw new InvalidDataException("Operation frame exceeds bound.");
        await stream.WriteAsync(bytes,token);await stream.WriteAsync(new byte[]{10},token);await stream.FlushAsync(token);
    }
}
