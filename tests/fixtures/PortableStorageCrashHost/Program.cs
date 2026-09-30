namespace PortableStorageCrashHost;

public static class Program
{
    public static Task<int> Main(string[] args) =>
        BookOfEternityClient.Tests.TrustedLocalPublicationCrashFixture.RunAsync(args);
}
