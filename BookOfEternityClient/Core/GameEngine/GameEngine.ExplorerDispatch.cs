using BookOfEternityClient.UI;

namespace BookOfEternityClient.Core;

public partial class GameEngine
{
    /// <summary>
    /// Dispatches the existing local Explorer command before any returned GM action is consumed.
    /// This shared entry currently preserves direct dispatch; it does not grant publication authority.
    /// </summary>
    internal static Task<string?> DispatchExplorerForCurrentSessionAsync(
        FileSystemManager files, ExplorerMode explorer, string command)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(explorer);
        return explorer.TryProcessCommand(command);
    }
}
