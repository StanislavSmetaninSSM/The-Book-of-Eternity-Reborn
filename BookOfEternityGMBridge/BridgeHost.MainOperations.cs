using BookOfEternityClient.Services.GmRuntime;
namespace BookOfEternityGMBridge;
internal sealed partial class BridgeHost
{
    private async Task ServeMainOperationAsync(Stream stream,MainOperationReader reader,BridgeRequest request,CancellationToken token)
    {
        GmSessionRunCoordinator.RemotePin? pin=null;
        try {
            var owner=_mainRun??throw new InvalidDataException("No original main owner.");
            pin=owner.BeginRemoteOperation(request.RootKey,request.OperationId);
            using(var bounded=CancellationTokenSource.CreateLinkedTokenSource(token)) {
                bounded.CancelAfter(TimeSpan.FromSeconds(3));await MainOperationReader.WriteAsync(stream,pin.Reply,bounded.Token);
                var activate=await reader.ReadAsync<MainOperationFrame>(bounded.Token)??throw new IOException("Grant connection lost.");
                if(activate.Command!="activateMainOperation")throw new InvalidDataException("Expected activation.");pin.Activate(activate);
                await MainOperationReader.WriteAsync(stream,pin.Reply,bounded.Token);
            }
            // One original connection; resources are bounded by the owner's32 pins.
            // Actual owner stop bounds drain; lost closure keeps an unresolved pin.
            var close=await reader.ReadAsync<MainOperationFrame>(token)??throw new IOException("Operation connection lost.");
            if(close.Command!="closeMainOperation" || close.Close==null)throw new InvalidDataException("Expected immutable close.");
            pin.ObserveClosed(close.Close);
            if(BeforeMainCloseReply!=null)await BeforeMainCloseReply(close.Close);
            using var response=CancellationTokenSource.CreateLinkedTokenSource(token);response.CancelAfter(TimeSpan.FromSeconds(3));
            await MainOperationReader.WriteAsync(stream,pin.Reply,response.Token);
        } catch {
            pin?.Lost(); // ClosedObserved is absorbing even when reply/peer is lost.
            if(pin==null)try{using var refused=new CancellationTokenSource(TimeSpan.FromSeconds(1));await MainOperationReader.WriteAsync(stream,new MainOperationReply(false,Error:"Main operation admission refused."),refused.Token);}catch{}
        }
    }
}
