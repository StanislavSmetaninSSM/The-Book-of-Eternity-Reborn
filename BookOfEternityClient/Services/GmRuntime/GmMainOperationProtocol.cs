using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BookOfEternityClient.Services.GmRuntime;

internal enum MainOperationState { PreparedGrant, Active, Closing, ClosedObserved, Unresolved }
internal enum MainOperationOutcome { Completed, Failed, Cancelled, Committed, RolledBack, Uncertain, NotLoaded }
internal sealed record MainOperationClose(string PinId,string CloseId,string OperationId,GmSessionRunIdentity Identity,
    [property:JsonRequired] MainOperationOutcome Outcome,[property:JsonRequired] bool ClosingFailed);
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
    private readonly byte[] _buffer=new byte[4096];
    private int _position,_length;
    internal int LastFrameBytes {get;private set;}
    internal async Task<T?> ReadAsync<T>(CancellationToken token,int maximum=65536)
    {
        using var bytes=new MemoryStream();
        while(true) {
            if(_position==_length) {
                _length=await stream.ReadAsync(_buffer,token);_position=0;
                if(_length==0){if(bytes.Length==0)return default;throw new IOException("Incomplete operation frame.");}
            }
            var newline=Array.IndexOf(_buffer,(byte)10,_position,_length-_position);
            var end=newline<0?_length:newline;
            var count=end-_position;
            if(bytes.Length+count>maximum)throw new InvalidDataException("Operation frame exceeds bound.");
            bytes.Write(_buffer,_position,count);_position=end;
            if(newline>=0){_position++;break;}
        }
        LastFrameBytes=checked((int)bytes.Length);
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
