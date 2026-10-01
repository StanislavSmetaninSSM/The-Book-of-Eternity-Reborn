using BookOfEternityClient.Core;

namespace BookOfEternityClient.WebUi;

internal enum BrowserPreparedWriteDisposition { Blocked, Committed, RolledBack, Uncertain }
internal sealed record BrowserPreparedWriteResult(BrowserPreparedWriteDisposition Disposition,
    bool NeedsFollowUp, string Message);
internal sealed record PreparedBrowserLocalWrite(IReadOnlyList<CanonicalLocalFileChange> Changes,
    Func<Task> ApplyCommittedRuntime);

public sealed partial class BrowserLocalWriteCoordinator
{
    // Prepared member images and runtime application are deliberately separate:
    // after a durable commit no callback/cleanup failure can authorize rollback.
    internal Task<BrowserPreparedWriteResult> ExecutePreparedAsync(BrowserLocalWriteRequest request,
        Func<FileSystemManager.CanonicalWriteLease, Task<PreparedBrowserLocalWrite>> prepare) =>
        throw new NotImplementedException();
}
