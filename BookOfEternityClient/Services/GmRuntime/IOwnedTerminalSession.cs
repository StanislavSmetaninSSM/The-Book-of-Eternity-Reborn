using BookOfEternityClient.Services.GmWorkers;

namespace BookOfEternityClient.Services.GmRuntime;

internal readonly record struct TerminalSize(int Columns, int Rows);
internal sealed record TerminalIdentity(string RunId, string Backend, string Guarantee, int RootPid);
internal sealed record TerminalRootExit(int? ExitCode);
internal sealed record TerminalStopEvidence(TerminalIdentity Identity, GmWorkerStopState State,
    string Reason, bool CleanupComplete, bool AuthorityRetained);

internal interface IOwnedTerminalSession : IAsyncDisposable
{
    TerminalIdentity Identity { get; }
    Stream InputWriter { get; }
    Stream OutputReader { get; }
    Task<TerminalRootExit> RootExited { get; }
    ValueTask ResizeAsync(TerminalSize size, CancellationToken waitToken);
    Task<TerminalStopEvidence> StopAndObserveAsync(CancellationToken waitToken);
}
