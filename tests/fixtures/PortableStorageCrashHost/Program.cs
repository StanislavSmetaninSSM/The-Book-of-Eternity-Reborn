namespace PortableStorageCrashHost;

public static class Program
{
    public static Task<int> Main(string[] args) =>
        args.Length == 4 && args[2] == "save-resource"
            ? BookOfEternityClient.Tests.PortableSaveResourceProbe.RunAsync(args)
            : args.Length == 4 && args[2] is "stream-recover" or "stream-publish" or "stream-recover-cut"
                ? BookOfEternityClient.Tests.TrustedLocalStreamCrashFixture.RunAsync(args)
            : BookOfEternityClient.Tests.TrustedLocalPublicationCrashFixture.RunAsync(args);
}
