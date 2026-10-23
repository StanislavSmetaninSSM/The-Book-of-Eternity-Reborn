namespace BookOfEternityClient.Core;

public partial class FileSystemManager
{
    /// <summary>
    /// Executes one read-only GameState source phase without retaining its validation verdict.
    /// </summary>
    /// <typeparam name="TResult">
    /// The result constructed by the original phase on its current source data.
    /// </typeparam>
    /// <param name="read">
    /// The original read-only phase; its exception remains the primary failure.
    /// </param>
    /// <returns>
    /// The original phase result after its source work settles.
    /// </returns>
    internal Task<TResult> ReadGameStateDataAsync<TResult>(Func<Task<TResult>> read)
    {
        ArgumentNullException.ThrowIfNull(read);
        return read();
    }
}
