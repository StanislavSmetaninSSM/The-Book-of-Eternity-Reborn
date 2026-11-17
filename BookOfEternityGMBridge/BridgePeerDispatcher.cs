using System.Runtime.ExceptionServices;
namespace BookOfEternityGMBridge;

internal static class BridgePeerDispatcher
{
    internal static async Task RunAsync(BridgeTransportListener listener, Func<Stream,CancellationToken,Task> handler,
        CancellationToken token, int maximumPeers=48, Action<Exception>? onFatal=null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumPeers,1);
        using var stop=CancellationTokenSource.CreateLinkedTokenSource(token);
        using var capacity=new SemaphoreSlim(maximumPeers,maximumPeers);
        var notified=0;
        var peers=new List<Task>();var errors=new System.Collections.Concurrent.ConcurrentQueue<Exception>();
        var peerFailure=new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        var observing=ObserveFailureAsync();
        try {
            while(true) {
                await capacity.WaitAsync(stop.Token).ConfigureAwait(false);
                Stream? accepted=null;
                try {
                    ThrowIfFailed();
                    accepted=await listener.AcceptAsync(stop.Token).ConfigureAwait(false);
                    ThrowIfFailed();
                    peers.RemoveAll(p=>p.IsCompletedSuccessfully);
                    peers.Add(ServeAsync(accepted));accepted=null;
                } catch {if(accepted!=null)await accepted.DisposeAsync().ConfigureAwait(false);capacity.Release();throw;}
            }
        } catch(OperationCanceledException) when(stop.IsCancellationRequested) { }
        catch(Exception ex){NotifyFatal(ex);errors.Enqueue(ex);}
        finally {
            CancelAndRetainErrors();
            // Independent attempts: a listener cleanup error must not skip joins.
            try{await listener.DisposeAsync().ConfigureAwait(false);}catch(Exception ex){errors.Enqueue(ex);}
            try{await Task.WhenAll(peers).ConfigureAwait(false);}catch(Exception ex){errors.Enqueue(ex);}
            try{await observing.ConfigureAwait(false);}catch(Exception ex){errors.Enqueue(ex);}
        }
        if(listener.Failure.IsCompletedSuccessfully)errors.Enqueue(listener.Failure.Result);
        if(peerFailure.Task.IsCompletedSuccessfully)errors.Enqueue(peerFailure.Task.Result);
        var distinct=errors.SelectMany(e=>e is AggregateException a?a.Flatten().InnerExceptions:(IEnumerable<Exception>)new[]{e}).Distinct().ToArray();
        if(distinct.Length==1)ExceptionDispatchInfo.Capture(distinct[0]).Throw();
        if(distinct.Length>1)throw new AggregateException(distinct);
        return;

        void NotifyFatal(Exception error) {
            if(Interlocked.Exchange(ref notified,1)==0 && onFatal!=null)
                try{onFatal(error);}catch(Exception ex){errors.Enqueue(ex);}
        }
        void CancelAndRetainErrors() {try{stop.Cancel();}catch(Exception ex){errors.Enqueue(ex);}}
        void ThrowIfFailed() {
            if(listener.Failure.IsCompletedSuccessfully)ExceptionDispatchInfo.Capture(listener.Failure.Result).Throw();
            if(peerFailure.Task.IsCompletedSuccessfully)ExceptionDispatchInfo.Capture(peerFailure.Task.Result).Throw();
            stop.Token.ThrowIfCancellationRequested();
        }
        async Task ObserveFailureAsync() {
            try {
                var failed=await Task.WhenAny(listener.Failure,peerFailure.Task).WaitAsync(stop.Token).ConfigureAwait(false);
                NotifyFatal(await failed.ConfigureAwait(false));
                CancelAndRetainErrors();
            } catch(OperationCanceledException) when(stop.IsCancellationRequested) { }
        }
        async Task ServeAsync(Stream stream) {
            try {await using(stream.ConfigureAwait(false)){await handler(stream,stop.Token).ConfigureAwait(false);}}
            catch(OperationCanceledException) when(stop.IsCancellationRequested) { }
            catch(Exception ex){errors.Enqueue(ex);peerFailure.TrySetResult(ex);NotifyFatal(ex);CancelAndRetainErrors();}
            finally{capacity.Release();}
        }
    }
}
