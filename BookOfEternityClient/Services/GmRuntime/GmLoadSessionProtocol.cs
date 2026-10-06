using System.Text.Json.Serialization;

namespace BookOfEternityClient.Services.GmRuntime;

[JsonConverter(typeof(JsonStringEnumConverter<GmLoadMainState>))]
public enum GmLoadMainState { NoActiveSession, Stopped, Running, StartedNotReady, Refused, Cancelled, Uncertain }

internal sealed record GmLoadSessionReply(bool Ok,string OperationId,GmLoadMainState State,
    GmSessionRunIdentity? MainIdentity=null,TerminalIdentity? TerminalIdentity=null,string? Error=null);

internal sealed record GmLoadSessionFrame(string Command,string OperationId,
    [property:JsonRequired] bool Committed,[property:JsonRequired] bool RefreshConfirmed,string? EstablishedGeneration);
