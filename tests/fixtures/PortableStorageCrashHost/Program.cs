namespace PortableStorageCrashHost;

public static class Program
{
    public static Task<int> Main(string[] args) =>
        args.Length == 4 && args[2] == "save-resource"
            ? BookOfEternityClient.Tests.PortableSaveResourceProbe.RunAsync(args)
            : BookOfEternityClient.Tests.TrustedLocalPublicationCrashFixture.RunAsync(args);
}
