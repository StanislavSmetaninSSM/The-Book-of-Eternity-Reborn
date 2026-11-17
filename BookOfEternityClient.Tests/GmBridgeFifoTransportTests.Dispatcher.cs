using BookOfEternityClient.Services.GmRuntime;
using BookOfEternityGMBridge;
using Xunit;
namespace BookOfEternityClient.Tests;
public sealed partial class GmBridgeFifoTransportTests
{
    [Fact]
    public async Task Dispatcher_FatalSignalCancelsOccupiedCapacityBeforeQueuedDispatch()
    {
        var root=Path.Combine(TestDiagnosticPaths.Root,"bridge-dispatch",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try {
            var failure=new InvalidOperationException("admission sentinel");var admissions=0;var handled=0;
            var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var cancelled=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var listener=BridgeTransportListener.CreateFifo(root,()=>{if(Interlocked.Increment(ref admissions)==3)throw failure;});
            using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(8));
            var running=BridgePeerDispatcher.RunAsync(listener,async(_,token)=>{
                Interlocked.Increment(ref handled);entered.TrySetResult();
                try {await Task.Delay(Timeout.Infinite,token);} catch(OperationCanceledException) when(token.IsCancellationRequested){cancelled.TrySetResult();}
            },deadline.Token,1);
            await using var first=await BridgeTransportConnector.ConnectAsync(listener.Endpoint,deadline.Token);
            await entered.Task.WaitAsync(deadline.Token);
            await using var queued=await BridgeTransportConnector.ConnectAsync(listener.Endpoint,deadline.Token);
            await using var faulting=await BridgeTransportConnector.ConnectAsync(listener.Endpoint,deadline.Token);
            await cancelled.Task.WaitAsync(deadline.Token);
            var error=await Assert.ThrowsAnyAsync<Exception>(()=>running.WaitAsync(deadline.Token));
            Assert.True(ReferenceEquals(error,failure) || error is AggregateException a && a.Flatten().InnerExceptions.Contains(failure));
            Assert.Equal(1,handled);Assert.False(Directory.Exists(listener.Endpoint.Address));
            Assert.Equal(0,await first.ReadAsync(new byte[1],deadline.Token));
            Assert.Equal(0,await queued.ReadAsync(new byte[1],deadline.Token));
        } finally {Directory.Delete(root,true);}
    }
    [Fact]
    public async Task Dispatcher_NormalCancellationJoinsOriginalHandlerAndLeavesUnrelatedFile()
    {
        var root=Path.Combine(TestDiagnosticPaths.Root,"bridge-dispatch",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try {
            var sentinel=Path.Combine(root,"not-owned");File.WriteAllText(sentinel,"unchanged");
            var listener=BridgeTransportListener.CreateFifo(root);
            using var stop=new CancellationTokenSource();using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(8));
            var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var exited=false;
            var running=BridgePeerDispatcher.RunAsync(listener,async(_,token)=>{entered.TrySetResult();try{await Task.Delay(Timeout.Infinite,token);}finally{exited=true;}},stop.Token,1);
            await using var peer=await BridgeTransportConnector.ConnectAsync(listener.Endpoint,deadline.Token);
            await entered.Task.WaitAsync(deadline.Token);stop.Cancel();await running.WaitAsync(deadline.Token);
            Assert.True(exited);Assert.False(Directory.Exists(listener.Endpoint.Address));Assert.Equal("unchanged",File.ReadAllText(sentinel));
            Assert.Equal(0,await peer.ReadAsync(new byte[1],deadline.Token));
        }finally{Directory.Delete(root,true);}
    }
}
