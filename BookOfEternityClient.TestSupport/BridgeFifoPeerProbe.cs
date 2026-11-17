using BookOfEternityClient.Services.GmRuntime;

namespace BookOfEternityClient.Tests;

/// <summary>Independent test-only client using the actual bridge FIFO connector.</summary>
public static class BridgeFifoPeerProbe
{
    /// <summary>Runs a bounded peer without launching further processes or changing signal policy.</summary>
    /// <param name="args">Endpoint directory, run identity and echo/epipe/wait scenario.</param>
    /// <returns>Zero only after the scenario's actual transport observation.</returns>
    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length != 3 || args[2] is not ("echo" or "epipe" or "wait")) return 64;
        try
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            await using var stream = await LinuxBridgeFifoClient.ConnectAsync(new(args[0], args[1]), deadline.Token);
            if (args[2] == "echo")
            {
                var payload = Enumerable.Range(0, 131072).Select(i => (byte)(i % 251)).ToArray();
                await stream.WriteAsync(payload, deadline.Token);
                var reply = new byte[payload.Length];
                await stream.ReadExactlyAsync(reply, deadline.Token);
                if (!payload.AsSpan().SequenceEqual(reply)) return 65;
                Console.WriteLine($"FIFO_PEER_OK {Environment.ProcessId}");
                return 0;
            }
            if (args[2] == "epipe")
            {
                if (await stream.ReadAsync(new byte[1], deadline.Token) != 0) return 66;
                try { await stream.WriteAsync(new byte[1], deadline.Token); return 67; }
                catch (LinuxBridgeFifoNative.NativeFailure ex) when (ex.ErrorNumber == 32)
                {
                    Console.WriteLine($"FIFO_EPIPE_OK {Environment.ProcessId}");
                    return 0;
                }
            }
            await stream.WriteAsync(new byte[] { 42 }, deadline.Token);
            Console.WriteLine($"FIFO_WAITING {Environment.ProcessId}");
            await Task.Delay(Timeout.InfiniteTimeSpan, deadline.Token);
            return 68;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 69; }
    }
}
