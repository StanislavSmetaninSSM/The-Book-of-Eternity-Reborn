using BookOfEternityClient.Services.GmWorkers;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class GmWorkerBackendSelectionTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void LinuxNeutralHost_SelectsDeclaredFallbackPendingActualPreflight(int mode)
    {
        var request = (GmWorkerBackendRequest)mode;
        var result = GmWorkerBackendSelector.Select(request, GmWorkerRequiredCapability.NeutralHost, false, true);
        Assert.True(result.CanStart);
        Assert.Equal(GmWorkerBackend.NativeLineage, result.Backend);
        Assert.Equal(GmWorkerBackendAvailability.PreflightRequired, result.Availability);
        Assert.Equal(GmWorkerBackendSelector.NativeGuarantee, result.Guarantee);
        Assert.Equal(GmWorkerBackendAvailability.NotImplemented, result.SystemdAvailability);
        Assert.Equal(request, result.Requested);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void LinuxWorkerRelease_IsUnavailableBeforeAnyLaunch(int mode)
    {
        var request = (GmWorkerBackendRequest)mode;
        var result = GmWorkerBackendSelector.Select(request, GmWorkerRequiredCapability.WorkerRelease, false, true);
        Assert.False(result.CanStart);
        Assert.Equal(GmWorkerBackend.None, result.Backend);
        Assert.Equal(GmWorkerBackendAvailability.NotQualified, result.Availability);
        Assert.Equal("none", result.Guarantee);
    }

    [Fact]
    public void ExplicitSystemd_DoesNotPretendSuccessOrSilentlyDowngrade()
    {
        var result = GmWorkerBackendSelector.Select(GmWorkerBackendRequest.SystemdUser,
            GmWorkerRequiredCapability.NeutralHost, false, true);
        Assert.False(result.CanStart);
        Assert.Equal(GmWorkerBackend.None, result.Backend);
        Assert.Equal(GmWorkerBackendAvailability.NotImplemented, result.Availability);
    }

    [Fact]
    public void WindowsAuto_PreservesJobCapabilityWithoutNativePreflight()
    {
        var result = GmWorkerBackendSelector.Select(GmWorkerBackendRequest.Auto,
            GmWorkerRequiredCapability.WorkerRelease, true, false);
        Assert.True(result.CanStart);
        Assert.Equal(GmWorkerBackend.WindowsJob, result.Backend);
        Assert.Equal(GmWorkerBackendAvailability.Available, result.Availability);
        Assert.Equal("windows-job", result.Guarantee);
    }

    [Fact]
    public void OtherPlatform_RemainsUnavailable()
    {
        var result = GmWorkerBackendSelector.Select(GmWorkerBackendRequest.Auto,
            GmWorkerRequiredCapability.NeutralHost, false, false);
        Assert.False(result.CanStart);
        Assert.Equal(GmWorkerBackendAvailability.Unsupported, result.Availability);
    }
}
