using BookOfEternityClient.Configuration;
using BookOfEternityClient.Services;
using BookOfEternityClient.WebUi;

namespace BookOfEternityClient.Core;

// A menu draft owns no filesystem lease between operations. Publication and
// runtime acceptance use the existing local-write coordinator and journal.
internal sealed class ConsoleSettingsSession
{
    internal GameSettings Draft => throw new NotImplementedException();
    internal bool RequiresReload => throw new NotImplementedException();

    internal static Task<ConsoleSettingsSession> OpenAsync(FileSystemManager files,
        StateManager state, SystemModService mods) => throw new NotImplementedException();

    internal Task<BrowserPreparedWriteResult> SaveAsync(Func<Task>? refreshRuntime = null)
        => throw new NotImplementedException();
}
