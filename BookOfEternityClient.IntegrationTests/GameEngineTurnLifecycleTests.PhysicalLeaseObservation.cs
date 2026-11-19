using System.Text;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services.GmRuntime;
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
        if (mode == "repeated-dispose") VerifyPhysicalObservationWire();
    }

    private void VerifyPhysicalObservationWire()
    {
        var previous = Environment.GetEnvironmentVariable("BOE_MAIN_STOP_DIAGNOSTIC_NONCE");
        var nonce = Guid.NewGuid().ToString("N");
        try
        {
            Environment.SetEnvironmentVariable("BOE_MAIN_STOP_DIAGNOSTIC_NONCE", nonce);
            var identity = new GmSessionRunIdentity(_rootPath, Guid.NewGuid().ToString("N"),
                Guid.NewGuid().ToString("N"), 1, GmSessionRunBackend.LinuxSupervisor,
                Guid.NewGuid().ToString("N"), "test-boot");
            var pin = new MainOperationReply(true, MainOperationState.Active, Guid.NewGuid().ToString("N"),
                Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N"), identity);
            var context = MainOperationFailureDiagnostic.BeginPhysicalLease(nonce, _rootPath, "SessionMutation", pin);
            Assert.NotNull(context);
            Assert.Same(pin, context!.OriginalPin); // Existing immutable DATA, not a newly synthesized close.
            var observation = MainOperationFailureDiagnostic.CapturePhysical(context, "physical-opened", 1);
            Assert.NotNull(observation);
            using var wire = new StringWriter();
            MainOperationFailureDiagnostic.WritePhysical(observation, wire, nonce);
            var row = Assert.Single(ReadPhysicalLeaseEvents(wire.ToString()));
            Assert.Equal(JsonSerializer.SerializeToElement(pin).GetRawText(), row.GetProperty("OriginalPin").GetRawText());
            Assert.Equal(observation!.CapturedAtUtc, row.GetProperty("CapturedAtUtc").GetDateTimeOffset());
            Assert.Equal(observation.CaptureMonotonicTimestamp, row.GetProperty("CaptureMonotonicTimestamp").GetInt64());
            foreach (var bad in new[] {
                pin with { Ok=false }, pin with { PinId="bad" }, pin with { CloseId=null },
                pin with { OperationId=Guid.Empty.ToString("N") }, pin with { Identity=null },
                pin with { State=(MainOperationState)999 }, pin with { Error="unexpected" },
                pin with { Identity=identity with { Epoch=0 } },
                pin with { Identity=identity with { Backend=GmSessionRunBackend.Unspecified } },
                pin with { Identity=identity with { RootKey=_rootPath+"-foreign" } } })
                Assert.Null(MainOperationFailureDiagnostic.BeginPhysicalLease(nonce, _rootPath, "SessionMutation", bad));
            Assert.Null(MainOperationFailureDiagnostic.BeginPhysicalLease("bad", _rootPath, "SessionMutation", pin));
            Assert.Null(MainOperationFailureDiagnostic.CapturePhysical(context, "unknown", 1));
            Assert.Null(MainOperationFailureDiagnostic.CapturePhysical(context, "lease-granted", 0));
            using var refused = new StringWriter();
            MainOperationFailureDiagnostic.WritePhysical(observation, refused, Guid.NewGuid().ToString("N"));
            Assert.Equal("", refused.ToString());
            using var sink = new PhysicalDiagnosticThrowingWriter();
            MainOperationFailureDiagnostic.WritePhysical(observation, sink, nonce);
            Assert.Equal(1, sink.Attempts);
            Environment.SetEnvironmentVariable("BOE_MAIN_STOP_DIAGNOSTIC_NONCE", Guid.NewGuid().ToString("N"));
            Assert.Null(MainOperationFailureDiagnostic.CapturePhysical(context, "physical-dispose-completed", 1));
        }
        finally { Environment.SetEnvironmentVariable("BOE_MAIN_STOP_DIAGNOSTIC_NONCE", previous); }
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
