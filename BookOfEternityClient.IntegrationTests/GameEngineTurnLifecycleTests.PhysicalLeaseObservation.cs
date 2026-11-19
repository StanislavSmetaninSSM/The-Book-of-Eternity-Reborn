using System.Text;
using System.Text.Json;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Theory]
    [InlineData("missing-nonce")]
    [InlineData("malformed-nonce")]
    [InlineData("changed-nonce")]
    [InlineData("throwing-sink")]
    [InlineData("repeated-dispose")]
    public async Task PhysicalLeaseObservationPreservesNonceAndActualDisposal(string mode)
    {
        var files = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        var nonce = Guid.NewGuid().ToString("N");
        var previousNonce = Environment.GetEnvironmentVariable("BOE_MAIN_STOP_DIAGNOSTIC_NONCE");
        var previousError = Console.Error;
        using var captured = new StringWriter();
        using var failing = new PhysicalDiagnosticThrowingWriter();
        FileSystemManager.CanonicalWriteLease? lease = null;
        Exception? failure = null;
        try
        {
            Environment.SetEnvironmentVariable("BOE_MAIN_STOP_DIAGNOSTIC_NONCE",
                mode == "missing-nonce" ? null : mode == "malformed-nonce" ? "not-a-nonce" : nonce);
            Console.SetError(mode == "throwing-sink" ? failing : TextWriter.Synchronized(captured));
            lease = await files.AcquireCanonicalWriteLeaseAsync();
            Assert.True(lease.IsActive);
            if (mode == "changed-nonce") Environment.SetEnvironmentVariable("BOE_MAIN_STOP_DIAGNOSTIC_NONCE", Guid.NewGuid().ToString("N"));
            await lease.DisposeAsync();
            Assert.False(lease.IsActive);
            await lease.DisposeAsync(); // No second physical close or second completion fact.
        }
        catch (Exception error) { failure = error; }
        finally
        {
            try { if (lease?.IsActive == true) await lease.DisposeAsync(); }
            finally
            {
                Console.SetError(previousError);
                Environment.SetEnvironmentVariable("BOE_MAIN_STOP_DIAGNOSTIC_NONCE", previousNonce);
            }
        }
        Assert.Null(failure);
        await using (var fresh = await files.AcquireCanonicalWriteLeaseAsync()) Assert.True(fresh.IsActive);
        var rows = ReadPhysicalLeaseEvents(captured.ToString());
        if (mode is "missing-nonce" or "malformed-nonce")
        {
            Assert.DoesNotContain("BOE_CANONICAL_LEASE_OBSERVATION ", captured.ToString());
            return;
        }
        if (mode == "throwing-sink")
        {
            Assert.True(failing.Attempts > 0); // Missing observer, not a throwing acquisition, is initial RED.
            return;
        }
        Assert.Equal(mode == "changed-nonce" ? 2 : 3, rows.Length);
        Assert.All(rows, row => Assert.Equal(nonce, row.GetProperty("Nonce").GetString()));
        if (mode == "changed-nonce") Assert.DoesNotContain(rows, row => PhysicalStage(row) == "physical-dispose-completed");
        Assert.Equal("physical-opened", PhysicalStage(rows[0]));
        Assert.Equal("lease-granted", PhysicalStage(rows[1]));
        if (mode == "repeated-dispose") Assert.Equal("physical-dispose-completed", PhysicalStage(rows[2]));
        Assert.Single(rows.Select(x => x.GetProperty("Occurrence").GetInt64()).Distinct());
        Assert.All(rows, row => Assert.Equal(JsonValueKind.Null, row.GetProperty("OriginalPin").ValueKind));
        // Local test has no remote operation. It must never manufacture a pin/close tuple.
    }

    private sealed class PhysicalDiagnosticThrowingWriter : TextWriter
    {
        public override Encoding Encoding => Encoding.UTF8;
        public int Attempts { get; private set; }
        public override void WriteLine(string? value)
        {
            if (value?.StartsWith("BOE_CANONICAL_LEASE_OBSERVATION ", StringComparison.Ordinal) == true)
            {
                Attempts++;
                throw new IOException("diagnostic sink sentinel");
            }
        }
    }
}
