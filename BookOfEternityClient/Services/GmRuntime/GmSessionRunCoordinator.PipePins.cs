namespace BookOfEternityClient.Services.GmRuntime;

internal sealed partial class GmSessionRunCoordinator
{
    private readonly Dictionary<string,RemotePin> _remotePins=new(StringComparer.Ordinal);
    private readonly Queue<string> _closedRemotePins=new();
    internal sealed class RemotePin(GmSessionRunCoordinator owner,OperationPin pin,string operation)
    {
        internal readonly string PinId=Guid.NewGuid().ToString("N"),CloseId=Guid.NewGuid().ToString("N"),OperationId=operation;
        internal readonly GmSessionRunIdentity Identity=owner.Identity;
        internal MainOperationState State=MainOperationState.PreparedGrant;
        private MainOperationClose? _close;
        internal MainOperationReply Reply=>new(true,State,PinId,CloseId,OperationId,Identity);
        internal void Activate(MainOperationFrame frame)
        {
            lock(owner._sync) {
                Match(frame.PinId,frame.CloseId,frame.OperationId,frame.Identity);
                if(State!=MainOperationState.PreparedGrant || owner._closed || owner._uncertain)throw GmSessionRunPersistence.Invalid();
                State=MainOperationState.Active;
            }
        }
        private void Match(string? id,string? close,string? operation,GmSessionRunIdentity? identity)
        {
            if(id!=PinId || close!=CloseId || operation!=OperationId || identity==null ||
                !GmSessionRunValidation.IdentityMatches(Identity,identity))throw GmSessionRunPersistence.Invalid();
        }
        internal void ObserveClosed(MainOperationClose close)
        {
            lock(owner._sync) {
                Match(close.PinId,close.CloseId,close.OperationId,close.Identity);
                if(!Enum.IsDefined(close.Outcome))throw GmSessionRunPersistence.Invalid();
                if(State==MainOperationState.ClosedObserved){if(_close!=close)throw GmSessionRunPersistence.Invalid();return;}
                if(State!=MainOperationState.Active)throw GmSessionRunPersistence.Invalid();
                State=MainOperationState.Closing;_close=close;
                // Receipt of the exact immutable terminal frame is the retirement
                // point. Reply write success/peer EOF is not a second authority test.
                pin.Dispose();State=MainOperationState.ClosedObserved;
                owner._closedRemotePins.Enqueue(PinId);
                while(owner._closedRemotePins.Count>32)owner._remotePins.Remove(owner._closedRemotePins.Dequeue());
            }
        }
        internal void Lost()
        {
            lock(owner._sync) {
                if(State==MainOperationState.ClosedObserved)return;
                State=MainOperationState.Unresolved;owner._closed=true;owner._uncertain=true;
                // Original pin remains counted. Never timeout/evict/reconnect it.
            }
        }
    }
    internal RemotePin BeginRemoteOperation(string? root,string? operation)
    {
        lock(_sync) {
            if(root!=Identity.RootKey || !Guid.TryParseExact(operation,"N",out _) ||
                _remotePins.Values.Any(p=>p.OperationId==operation) || _remotePins.Values.Count(p=>p.State!=MainOperationState.ClosedObserved)>=32)
                throw GmSessionRunPersistence.Invalid();
            var pin=CreateOperationPin();var remote=new RemotePin(this,pin,operation!);_remotePins.Add(remote.PinId,remote);return remote;
        }
    }
    internal MainOperationReply QueryRemoteOperation(MainOperationClose identity)
    {
        lock(_sync) {
            if(!_remotePins.TryGetValue(identity.PinId,out var pin) || pin.CloseId!=identity.CloseId || pin.OperationId!=identity.OperationId ||
                !GmSessionRunValidation.IdentityMatches(pin.Identity,identity.Identity))return new(false,Error:"Unknown original operation.");
            return pin.Reply;
        }
    }
    private OperationPin CreateOperationPin()
    {
        lock(_sync) {
            if(_closed || _uncertain || !_released || _retired || _persistence.HasDebt || _record?.Disposition!=GmSessionRunDisposition.Running ||
                _terminal?.AuthorityLost.IsCompleted!=false || _terminal.RootExited.IsCompleted)throw GmSessionRunPersistence.Invalid();
            _guard.Validate();if(_pins++==0)_drained=new(TaskCreationOptions.RunContinuationsAsynchronously);return new(this);
        }
    }
}
