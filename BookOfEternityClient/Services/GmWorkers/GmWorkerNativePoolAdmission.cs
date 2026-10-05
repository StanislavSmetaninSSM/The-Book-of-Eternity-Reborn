namespace BookOfEternityClient.Services.GmWorkers;

// Explicit internal fixture admission. The scaffold does not enable Release;
// only the connected typed-owner implementation may consume this capability.
internal sealed class GmWorkerNativePoolAdmission(string packageDirectory, string fixtureRoot)
{
    internal string PackageDirectory { get; } = Path.GetFullPath(packageDirectory);
    internal string FixtureRoot { get; } = Path.GetFullPath(fixtureRoot);
}
