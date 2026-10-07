using BookOfEternityClient.Services.GmRuntime;
using Microsoft.Win32.SafeHandles;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class GmMainSystemdContractTests
{
    [Fact]
    public void TransientScope_UsesFdTransferAndExactBoundedProperties()
    {
        using var synthetic=new SafeFileHandle((IntPtr)42,false); // codec bytes only; never owner authority
        var request=new SystemdScopeRequest("boe-main-"+Guid.NewGuid().ToString("N")+".scope",synthetic);
        Assert.Equal("fail",request.Mode);
        Assert.Same(synthetic,request.Properties["PIDFDs"]);
        Assert.Equal(true,request.Properties["AddRef"]);
        Assert.Equal("control-group",request.Properties["KillMode"]);
        Assert.Equal(true,request.Properties["SendSIGKILL"]);
        Assert.Equal((ulong)2_000_000,request.Properties["TimeoutStopUSec"]);
        Assert.Equal(5,request.Properties.Count);
    }
}
