namespace PortableStorageCrashHost;

/// <summary>
/// Dispatches isolated publication crash and resource scenarios for the owning test process.
/// </summary>
public static class Program
{
    /// <summary>
    /// Runs the selected owned scenario with the arguments supplied by its test.
    /// </summary>
    /// <param name="args">
    /// The owned root, declared byte limit or workload, mode family and scenario.
    /// </param>
    /// <returns>
    /// The selected fixture's ordinary, deliberate crash or recorded failure exit code.
    /// </returns>
    public static Task<int> Main(string[] args) =>
        args.Length == 4 && args[2] == "load-resource"
            ? BookOfEternityClient.Tests.PortableLoadResourceProbe.RunAsync(args)
            : args.Length == 4 && args[2] == "save-resource"
            ? BookOfEternityClient.Tests.PortableSaveResourceProbe.RunAsync(args)
            : args.Length == 4 && args[2] == "stream-resource"
                ? BookOfEternityClient.Tests.TrustedLocalStreamResourceProbe.RunAsync(args)
            : args.Length == 4 && args[2] is "generation-fresh-publish" or "generation-transition-publish" or "generation-recover"
                ? BookOfEternityClient.Tests.TrustedLocalStreamGenerationCrashFixture.RunAsync(args)
            : args.Length == 4 && args[2] is "stream-recover" or "stream-publish" or "stream-recover-cut"
                ? BookOfEternityClient.Tests.TrustedLocalStreamCrashFixture.RunAsync(args)
            : args.Length == 4 && args[2] is "load-publish" or "load-recover" or "load-recover-cut"
                ? BookOfEternityClient.Tests.PortableLoadColdHost.RunAsync(args)
            : args.Length == 4 && args[2] is "worker-publish" or "worker-recover" or "worker-recover-cut" or "worker-conflict"
                ? BookOfEternityClient.Tests.PortableWorkerColdHost.RunAsync(args)
            : BookOfEternityClient.Tests.TrustedLocalPublicationCrashFixture.RunAsync(args);
}
