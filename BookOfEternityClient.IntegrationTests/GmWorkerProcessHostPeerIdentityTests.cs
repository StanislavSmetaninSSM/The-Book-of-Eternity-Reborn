using System.ComponentModel;
using BookOfEternityClient.Services.GmWorkers;
using Microsoft.Win32.SafeHandles;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class GmWorkerProcessHostPeerIdentityTests
{
    [Theory]
    [InlineData(0u)]
    [InlineData(1000u)]
    public void LinuxResult_ExactProcessAndEffectiveUserAreAccepted(uint userId) =>
        GmWorkerProcessHostPeerIdentity.ValidateLinuxResult(
            new(0, 0, 12, 123, userId), 123, userId, "control");

    [Theory]
    [InlineData(0, 123)]
    [InlineData(-1, 123)]
    [InlineData(124, 123)]
    [InlineData(123, 0)]
    public void LinuxResult_InvalidOrForeignProcessIsRejected(int actual, int expected)
    {
        var error = Assert.Throws<InvalidDataException>(() =>
            GmWorkerProcessHostPeerIdentity.ValidateLinuxResult(new(0, 0, 12, actual, 1000), expected, 1000, "control"));
        Assert.Equal("Worker process host control channel was connected by an unexpected process.", error.Message);
    }

    [Fact]
    public void LinuxResult_SimulatedDifferentEffectiveUserIsRejected()
    {
        var error = Assert.Throws<InvalidDataException>(() =>
            GmWorkerProcessHostPeerIdentity.ValidateLinuxResult(new(0, 0, 12, 123, 1001), 123, 1000, "status"));
        Assert.Equal("Worker process host status channel was connected by an unexpected user.", error.Message);
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(11u)]
    [InlineData(13u)]
    [InlineData(uint.MaxValue)]
    public void LinuxResult_SimulatedMalformedCredentialLengthIsRejected(uint length)
    {
        var error = Assert.Throws<InvalidDataException>(() =>
            GmWorkerProcessHostPeerIdentity.ValidateLinuxResult(new(0, 0, length, 123, 1000), 123, 1000, "status"));
        Assert.Equal("Worker process host status channel returned an invalid peer identity length.", error.Message);
    }

    [Theory]
    [InlineData(-1, 9)]
    [InlineData(-1, 13)]
    [InlineData(1, 0)]
    public void LinuxResult_SimulatedNativeFailureRejectsEvenMatchingFields(int result, int errno)
    {
        var error = Assert.Throws<Win32Exception>(() =>
            GmWorkerProcessHostPeerIdentity.ValidateLinuxResult(new(result, errno, 12, 123, 1000), 123, 1000, "control"));
        Assert.Equal(errno, error.NativeErrorCode);
        Assert.Equal("Worker process host control channel client identity could not be read.", error.Message);
    }

    [Fact]
    public void Validate_InvalidNativeHandleFailsClosed()
    {
        using var invalid = new SafePipeHandle(new IntPtr(-1), ownsHandle: false);
        var error = Assert.Throws<Win32Exception>(() =>
            GmWorkerProcessHostPeerIdentity.Validate(invalid, 123, 1000, "control"));
        Assert.NotEqual(0, error.NativeErrorCode);
        Assert.Equal("Worker process host control channel client identity could not be read.", error.Message);
    }

    [Fact]
    public void Validate_DisposedHandleFailsClosed()
    {
        var disposed = new SafePipeHandle(new IntPtr(-1), ownsHandle: false);
        disposed.Dispose();
        Assert.Throws<ObjectDisposedException>(() =>
            GmWorkerProcessHostPeerIdentity.Validate(disposed, 123, 1000, "status"));
    }
}
