using Xunit;
namespace BookOfEternityClient.Tests;
public sealed class GmMainLinuxLauncherTests
{
    [Fact]
    public Task OrdinaryLauncher_ShippedLayoutWithoutSourceRunsConfiguredCli()=>ProductionMainLinuxFixture.RunAsync("launcher");
    [Fact]
    public Task ActualDaemonStartup_PortableOwnedBridgeWithoutWindowsDesktop()=>ProductionMainLinuxFixture.RunAsync("daemon");
    [Fact]
    public Task ShippedPublicationContainsRealLauncherAndParticipatingHelper()=>ProductionMainLinuxFixture.RunAsync("package-layout");
}
