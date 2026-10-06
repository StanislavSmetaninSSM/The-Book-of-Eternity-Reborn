using BookOfEternityClient.Services.GmWorkers;

namespace BookOfEternityClient.Services.GmRuntime;

// Failure before a terminal identity/master was admitted. Keep the exact native
// owner for cleanup/diagnostics; no usable stream/view or replacement admission.
internal sealed class PartialNativeTerminalSession(NativeLineageOwner owner) : IOwnedTerminalSession
{
    public TerminalIdentity Identity { get; } = new(owner.Identity.RunId,"native-lineage",owner.Identity.Guarantee,owner.AdmittedHostProcessId??0);
    public Stream InputWriter => Stream.Null;
    public Stream OutputReader => Stream.Null;
    public Task<TerminalRootExit> RootExited { get; } = Task.FromResult(new TerminalRootExit(null));
    public ValueTask ResizeAsync(TerminalSize size,CancellationToken token) => throw new InvalidOperationException("Partial original terminal has no resize admission.");
    public async Task<TerminalStopEvidence> StopAndObserveAsync(CancellationToken token)
    {
        var proof=await owner.StopAndObserveAsync().WaitAsync(token);
        return new(Identity,GmWorkerStopState.Uncertain,proof.Reason,proof.CleanupComplete,proof.AuthorityRetained);
    }
    public ValueTask DisposeAsync() => throw new InvalidOperationException("Partial Uncertain original owner is retained.");
}
