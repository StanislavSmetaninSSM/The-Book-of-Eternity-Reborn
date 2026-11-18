namespace GmBridgeFifoPeerHost;

/// <summary>Test-only independent FIFO peer, owned by the calling official test run.</summary>
public static class Program
{
    /// <summary>Dispatches one bounded connector scenario without any further child launch.</summary>
    /// <param name="args">Endpoint directory, run identity and named scenario.</param>
    /// <returns>The scenario's observed exit status.</returns>
    public static Task<int> Main(string[] args) => BookOfEternityClient.Tests.BridgeFifoPeerProbe.RunAsync(args);
}
