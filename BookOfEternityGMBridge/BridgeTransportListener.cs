using System.IO.Pipes;
using System.Net.Sockets;
using BookOfEternityClient.Services.GmRuntime;
namespace BookOfEternityGMBridge;

// Coordinates are frozen before child startup; only a fresh construction denial
// selects FIFO. A later bind/listen/rearm failure never changes transport.
internal abstract class BridgeTransportListener : IAsyncDisposable
{
    internal abstract BridgeTransportEndpoint Endpoint { get; }
    internal abstract Task<Exception> Failure { get; }
    internal abstract Task<Stream> AcceptAsync(CancellationToken token);
    public abstract ValueTask DisposeAsync();
    internal static BridgeTransportListener CreateFifo(string parent, Action? beforeReady = null)
        => new FifoListener(LinuxBridgeFifoListener.Create(parent,beforeReadyEnqueue:beforeReady));
    internal static BridgeTransportListener Create(string pipeName, string parent,
        Action? socketProbe = null, Func<BridgeTransportEndpoint, BridgeTransportListener>? namedFactory = null)
    {
        if(OperatingSystem.IsLinux()) {
            try {
                if(socketProbe!=null)socketProbe();
                else {using var probe=new Socket(AddressFamily.Unix,SocketType.Stream,ProtocolType.Unspecified);}
            } catch(SocketException ex) when(ex.NativeErrorCode is 1 or 13) {return CreateFifo(parent);}
        }
        var address=OperatingSystem.IsLinux() && !Path.IsPathFullyQualified(pipeName)
            ? Path.Combine(Path.GetTempPath(),"CoreFxPipe_"+pipeName) : pipeName;
        var endpoint=new BridgeTransportEndpoint(1,"named-pipe-v1",address,null);
        BridgeTransportConnector.Validate(endpoint);
        return namedFactory!=null ? namedFactory(endpoint) : new NamedListener(endpoint);
    }
    private sealed class FifoListener(LinuxBridgeFifoListener listener) : BridgeTransportListener
    {
        internal override BridgeTransportEndpoint Endpoint {get;}=new(1,"linux-fifo-v1",listener.Endpoint.DirectoryPath,listener.Endpoint.RunId);
        internal override Task<Exception> Failure=>listener.Failure;
        internal override Task<Stream> AcceptAsync(CancellationToken token)=>listener.AcceptAsync(token);
        public override ValueTask DisposeAsync()=>listener.DisposeAsync();
    }
    private sealed class NamedListener : BridgeTransportListener
    {
        private NamedPipeServerStream? _next;
        private readonly TaskCompletionSource<Exception> _failure=new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal override BridgeTransportEndpoint Endpoint {get;}
        internal override Task<Exception> Failure=>_failure.Task;
        internal NamedListener(BridgeTransportEndpoint endpoint){Endpoint=endpoint;_next=NewListener();}
        private NamedPipeServerStream NewListener()=>new(Endpoint.Address,PipeDirection.InOut,NamedPipeServerStream.MaxAllowedServerInstances,PipeTransmissionMode.Byte,PipeOptions.Asynchronous);
        internal override async Task<Stream> AcceptAsync(CancellationToken token)
        {
            var accepted=_next??throw new ObjectDisposedException(nameof(NamedListener));
            try {
                await accepted.WaitForConnectionAsync(token).ConfigureAwait(false);
                // Keep the socket listener alive before handing ownership to handler.
                _next=NewListener();return accepted;
            } catch(Exception ex) {
                accepted.Dispose();_next=null;
                if(ex is not OperationCanceledException || !token.IsCancellationRequested)_failure.TrySetResult(ex);
                throw;
            }
        }
        public override ValueTask DisposeAsync(){Interlocked.Exchange(ref _next,null)?.Dispose();return ValueTask.CompletedTask;}
    }
}
