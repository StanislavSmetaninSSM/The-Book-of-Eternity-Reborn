namespace BookOfEternityClient.Services.GmRuntime;
internal sealed partial class GmSessionRunCoordinator
{
    internal void ReportOperationFailure(Exception failure,string stage)
    {
#if DEBUG
        string? nonce=null;GmSessionRunIdentity? identity=null;
        try {lock(_sync){nonce=_stopDiagnosticNonce;identity=_stopDiagnosticIdentity;}}
        catch { }
        if(identity!=null)MainOperationFailureDiagnostic.Report(nonce,stage,null,failure,owner:identity);
#endif
    }
}
