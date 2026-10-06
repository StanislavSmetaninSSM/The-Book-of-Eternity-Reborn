namespace BookOfEternityClient.Services.GmRuntime;

internal static class OwnedTerminalSessionFactory
{
    // Preparation seam only. The accepted implementation will admit the fixed
    // compiled neutral fixture, never a configured GM executable or arguments.
    internal static Task<IOwnedTerminalSession> StartNeutralAsync(string package, string scratch, CancellationToken waitToken) =>
        throw new NotSupportedException("Neutral owned terminal transport is not implemented yet.");
}
