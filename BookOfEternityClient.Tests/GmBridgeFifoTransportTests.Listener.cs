using System.Net.Sockets;
using BookOfEternityClient.Services.GmRuntime;
using BookOfEternityGMBridge;
using Xunit;
namespace BookOfEternityClient.Tests;
public sealed partial class GmBridgeFifoTransportTests
{
    [Fact]
    public async Task ListenerFactory_ActualCapabilityUsesMatchingTransportAndCleansOriginalRun()
    {
        var root=Path.Combine(TestDiagnosticPaths.Root,"bridge-adapter",Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try {
            var expected="named-pipe-v1";
            try {using var probe=new Socket(AddressFamily.Unix,SocketType.Stream,ProtocolType.Unspecified);}
            catch(SocketException ex) when(ex.NativeErrorCode is 1 or 13){expected="linux-fifo-v1";}
            string address;
            await using(var listener=BridgeTransportListener.Create("adapter-"+Guid.NewGuid().ToString("N"),root)) {
                Assert.Equal(expected,listener.Endpoint.Kind);address=listener.Endpoint.Address;
                using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var accepting=listener.AcceptAsync(deadline.Token);
                await using var client=await BridgeTransportConnector.ConnectAsync(listener.Endpoint,deadline.Token);
                await using var server=await accepting;
                await server.WriteAsync(new byte[]{37},deadline.Token);
                var bytes=new byte[1];await client.ReadExactlyAsync(bytes,deadline.Token);Assert.Equal(37,bytes[0]);
                await server.DisposeAsync();Assert.Equal(0,await client.ReadAsync(bytes,deadline.Token));
            }
            Assert.False(Directory.Exists(address));Assert.Empty(Directory.GetFileSystemEntries(root));
        } finally {Directory.Delete(root,true);}
    }
    [Theory]
    [InlineData(1)] [InlineData(13)]
    public async Task ListenerFactory_ConstructorDenialOnlySelectsFifo(int error)
    {
        var root=Path.Combine(TestDiagnosticPaths.Root,"bridge-adapter",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try {
            await using var listener=BridgeTransportListener.Create("adapter",root,()=>throw new SocketException(error),_=>throw new InvalidOperationException("Named factory must not run"));
            Assert.Equal("linux-fifo-v1",listener.Endpoint.Kind);
        } finally {Directory.Delete(root,true);}
    }
    [Fact]
    public void ListenerFactory_PostProbeFailureNeverFallsBack()
    {
        var root=Path.Combine(TestDiagnosticPaths.Root,"bridge-adapter",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try {
            var original=new SocketException(13);
            var actual=Assert.Throws<SocketException>(()=>BridgeTransportListener.Create("adapter",root,()=>{},_=>throw original));
            Assert.Same(original,actual);Assert.Empty(Directory.GetFileSystemEntries(root));
        } finally {Directory.Delete(root,true);}
    }
    [Fact]
    public void ListenerFactory_OtherConstructorErrorNeverSelectsTransport()
    {
        var original=new SocketException(22);
        var actual=Assert.Throws<SocketException>(()=>BridgeTransportListener.Create("adapter",Path.GetTempPath(),()=>throw original,_=>throw new InvalidOperationException("Unexpected named factory")));
        Assert.Same(original,actual);
    }
    [Fact]
    public async Task ListenerFailure_IsObservedWhileReadyPeerRemainsBuffered()
    {
        var root=Path.Combine(TestDiagnosticPaths.Root,"bridge-adapter",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try {
            var sentinel=new InvalidOperationException("original admission failure");var count=0;
            var listener=LinuxBridgeFifoListener.Create(root,beforeReadyEnqueue:()=>{if(Interlocked.Increment(ref count)==2)throw sentinel;});
            using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try {
                await using var first=await LinuxBridgeFifoClient.ConnectAsync(listener.Endpoint,deadline.Token);
                await using var second=await LinuxBridgeFifoClient.ConnectAsync(listener.Endpoint,deadline.Token);
                Assert.Same(sentinel,await listener.Failure.WaitAsync(deadline.Token));
            } finally {await Assert.ThrowsAsync<InvalidOperationException>(async()=>await listener.DisposeAsync());}
        } finally {Directory.Delete(root,true);}
    }
}
