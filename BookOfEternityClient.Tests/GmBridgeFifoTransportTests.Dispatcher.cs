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
        BridgeTransportListener? owned=null;Task? run=null;using var ownerStop=new CancellationTokenSource();
        try {
            var failure=new InvalidOperationException("admission sentinel");var admissions=0;var handled=0;
            var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var cancelled=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var listener=owned=BridgeTransportListener.CreateFifo(root,()=>{if(Interlocked.Increment(ref admissions)==3)throw failure;});
            using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(8));
            var running=run=BridgePeerDispatcher.RunAsync(listener,async(_,token)=>{
                Interlocked.Increment(ref handled);entered.TrySetResult();
                try {await Task.Delay(Timeout.Infinite,token);} catch(OperationCanceledException) when(token.IsCancellationRequested){cancelled.TrySetResult();}
            },ownerStop.Token,1);
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
        } finally {
            ownerStop.Cancel();
            if(run!=null)try{await run.WaitAsync(TimeSpan.FromSeconds(5));}catch(InvalidOperationException){}catch(AggregateException){}
            if(owned!=null)try{await owned.DisposeAsync();}catch(InvalidOperationException){}catch(AggregateException){}
            Directory.Delete(root,true);
        }
    }
    [Fact]
    public async Task Dispatcher_NormalCancellationJoinsOriginalHandlerAndLeavesUnrelatedFile()
    {
        var root=Path.Combine(TestDiagnosticPaths.Root,"bridge-dispatch",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        BridgeTransportListener? owned=null;Task? run=null;using var stop=new CancellationTokenSource();
        try {
            var sentinel=Path.Combine(root,"not-owned");File.WriteAllText(sentinel,"unchanged");
            var listener=owned=BridgeTransportListener.CreateFifo(root);
            using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(8));
            var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var exited=false;
            var running=run=BridgePeerDispatcher.RunAsync(listener,async(_,token)=>{entered.TrySetResult();try{await Task.Delay(Timeout.Infinite,token);}finally{exited=true;}},stop.Token,1);
            await using var peer=await BridgeTransportConnector.ConnectAsync(listener.Endpoint,deadline.Token);
            await entered.Task.WaitAsync(deadline.Token);stop.Cancel();await running.WaitAsync(deadline.Token);
            Assert.True(exited);Assert.False(Directory.Exists(listener.Endpoint.Address));Assert.Equal("unchanged",File.ReadAllText(sentinel));
            Assert.Equal(0,await peer.ReadAsync(new byte[1],deadline.Token));
        }finally{
            stop.Cancel();if(run!=null)await run.WaitAsync(TimeSpan.FromSeconds(5));
            if(owned!=null)await owned.DisposeAsync();Directory.Delete(root,true);
        }
    }
}
