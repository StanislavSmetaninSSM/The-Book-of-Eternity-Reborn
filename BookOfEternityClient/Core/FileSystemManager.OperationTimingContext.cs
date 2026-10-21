namespace BookOfEternityClient.Core;

public partial class FileSystemManager
{
    /// <summary>
    /// Selects this manager's passive receiver for nested synchronous computations on the current thread.
    /// </summary>
    /// <returns>
    /// A restoring scope when observation context changes; otherwise, <see langword="null"/>.
    /// The scope must remain within synchronous work and must not span an await.
    /// </returns>
    internal IDisposable? EnterOperationTimingContext() => null;

    /// <summary>
    /// Observes a named synchronous computation through the currently selected passive receiver.
    /// </summary>
    /// <param name="name">
    /// The literal operation name emitted without changing the computation's authority.
    /// </param>
    /// <returns>
    /// An idempotent timer when a receiver is selected; otherwise, <see langword="null"/>.
    /// </returns>
    internal static IDisposable? MeasureCurrentOperation(string name) => null;
}
