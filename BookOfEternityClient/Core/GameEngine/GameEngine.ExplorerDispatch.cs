using BookOfEternityClient.UI;

namespace BookOfEternityClient.Core;

public partial class GameEngine
{
    /// <summary>
    /// Runs local Explorer dispatch within the existing participating session boundary and awaits
    /// its finalization before returning any GM action. Requires an initialized generation without
    /// bootstrapping; an ambient binding retains its existing validation and ownership semantics.
    /// The admission lease is released before the menu executes, not retained across user input.
    /// </summary>
    internal static Task<string?> DispatchExplorerForCurrentSessionAsync(
        FileSystemManager files, ExplorerMode explorer, string command)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(explorer);
        return SessionOperationContext.RunParticipatingCurrentSessionAsync(files,
            () => explorer.TryProcessCommand(command),
            beforeCurrentGeneration: lease =>
            {
                if (files.ReadExistingSessionGeneration(lease) is null)
                    throw new InvalidDataException("Explorer session dispatch requires an existing session generation.");
                return Task.CompletedTask;
            });
    }
}
