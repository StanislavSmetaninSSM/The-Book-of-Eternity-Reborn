using System.Text.Json;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookOfEternityClient.Tests;

public static class TrustedLocalStreamCrashFixture
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length != 4 || args[2] != "stream-recover") return 64;
        var available = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
        if (available != long.Parse(args[1])) throw new InvalidOperationException("The owned cold host did not inherit its declared heap limit.");
        try
        {
            var files = new FileSystemManager(args[0], NullLogger<FileSystemManager>.Instance);
            await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
            Console.WriteLine(JsonSerializer.Serialize(new { Phase = "CanonicalAcquisitionReturned", HeapBytes = available }));
            return 0;
        }
        catch (Exception failure)
        {
            Console.WriteLine(JsonSerializer.Serialize(new
            { Phase = "CanonicalAcquisitionFailed", HeapBytes = available, FailureType = failure.GetType().Name,
                failure.Message, Stack = failure.StackTrace }));
            return failure is InvalidDataException ? 78 : 90;
        }
    }
}
