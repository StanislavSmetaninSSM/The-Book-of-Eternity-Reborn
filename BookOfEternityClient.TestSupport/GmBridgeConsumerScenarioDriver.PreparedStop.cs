using System.Runtime.ExceptionServices;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services.GmRuntime;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookOfEternityClient.Tests;

internal static partial class GmBridgeConsumerScenarioDriver
{
    private static async Task VerifyPreparedStopRaceAsync(string status, GmSessionRunRecord original,
        string root, string output, Dictionary<string, object?> result, CancellationToken token)
    {
        var endpoint = BridgeTransportConnector.DecodeStatus(status);
        if (endpoint.Kind != "linux-fifo-v1") throw new IOException("This causal fixture requires actual FIFO transport.");
        var control = await GmMainOperationClient.OpenAsync(new FileSystemManager(root,
            NullLogger<FileSystemManager>.Instance), token, original.Identity);
        Task<string>? stopping = null;
        Exception? primary = null;
        try
        {
            await using var stream = await BridgeTransportConnector.ConnectAsync(endpoint, token);
            var reader = new MainOperationReader(stream);
            var operation = Guid.NewGuid().ToString("N");
            await MainOperationReader.WriteAsync(stream, new { command = "beginMainOperation", rootKey = root,
                operationId = operation, expectedMainIdentity = original.Identity }, token);
            var grant = await reader.ReadAsync<MainOperationReply>(token) ?? throw new IOException("Missing prepared grant.");
            if (!grant.Ok || grant.State != MainOperationState.PreparedGrant || grant.Identity != original.Identity ||
                grant.OperationId != operation || !Guid.TryParseExact(grant.PinId,"N",out _) || !Guid.TryParseExact(grant.CloseId,"N",out _))
                throw new IOException("Prepared-grant prerequisite failed.");
            result["PreparedGrant"] = grant;
            stopping = BridgeTransportConnector.RequestAsync(status, JsonSerializer.Serialize(new {
                command = "shutdown", rootKey = root, expectedMainIdentity = original.Identity }), 10000, CancellationToken.None);
            using var gate = CancellationTokenSource.CreateLinkedTokenSource(token);
            gate.CancelAfter(TimeSpan.FromSeconds(2));
            while (true)
            {
                gate.Token.ThrowIfCancellationRequested();
                var record = GmSessionRunRecordCodec.Decode(GmSessionRunPersistence.Read(root) ?? throw new IOException("Original record missing."));
                if (record.Identity != original.Identity) throw new IOException("Original owner changed.");
                if (record.Disposition == GmSessionRunDisposition.Stopping) break;
                if (stopping.IsCompleted) throw new IOException("Shutdown finished before controlled activation.");
                await Task.Delay(5, gate.Token);
            }
            result["StopAdmissionClosedBeforeActivation"] = true;
            await MainOperationReader.WriteAsync(stream, new MainOperationFrame { Command = "activateMainOperation",
                PinId = grant.PinId, CloseId = grant.CloseId, OperationId = operation, Identity = original.Identity }, token);
            using var bounded = CancellationTokenSource.CreateLinkedTokenSource(token);
            bounded.CancelAfter(TimeSpan.FromSeconds(3));
            var reply = await reader.ReadAsync<MainOperationReply>(bounded.Token);
            result["ActivationReply"] = reply;
            if (reply != null && reply.Ok) throw new IOException("Late activation received success instead of EOF or explicit refusal.");
            result["ActivationRefused"] = true;
            var lookup = new MainOperationClose(grant.PinId!, grant.CloseId!, operation, original.Identity,
                MainOperationOutcome.Completed, false);
            for (var i = 0; i < 2; i++)
            {
                var raw = await BridgeTransportConnector.RequestAsync(status, JsonSerializer.Serialize(new {
                    command = "mainOperationStatus", mainOperationClose = lookup }), 3000, token);
                var observed = JsonSerializer.Deserialize<MainOperationReply>(raw, MainOperationReader.Json)
                    ?? throw new IOException("Missing revoked identity observation.");
                if (!observed.Ok || observed.State != MainOperationState.RevokedBeforeActivation ||
                    observed.PinId != grant.PinId || observed.CloseId != grant.CloseId || observed.Identity != original.Identity)
                    throw new IOException("Stop revocation fabricated a close receipt or lost its original identity.");
                result["RevokedObservation" + i] = observed;
            }
            if (stopping.IsCompleted) throw new IOException("Revoked grant double-release bypassed the real active control pin.");
        }
        catch (Exception error) { primary = error; }
        finally
        {
            // This real active pin keeps Stopping observable even after a correct prepared revocation.
            try
            {
                async Task CloseControl()
                {
                    try
                    {
                        if (control.TerminalClose == null) await control.CompleteAsync(MainOperationOutcome.Completed, false);
                        if (!control.CloseObserved || control.TerminalClose?.Identity != original.Identity)
                            throw new IOException("Control original immutable close not acknowledged.");
                        result["ControlCloseObserved"] = true;
                    }
                    finally { await control.DisposeAsync(); }
                }
                await CloseControl().WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch (Exception error) { result["ControlCleanupFailure"] = error.ToString(); primary ??= error; }
            if (stopping != null)
            {
                try
                {
                    var raw = await stopping.WaitAsync(TimeSpan.FromSeconds(10));
                    await File.WriteAllTextAsync(Path.Combine(output, "prepared-stop-shutdown.json"), raw);
                    using var response = JsonDocument.Parse(raw);
                    result["ShutdownReply"] = response.RootElement.Clone();
                    var reply = response.RootElement;
                    if (!reply.GetProperty("ok").GetBoolean()) throw new IOException("Never-activated prepared grant prevented original clean stop.");
                    var state = reply.GetProperty("status"); var proof = state.GetProperty("terminalStop");
                    if (state.GetProperty("terminalRunId").GetString() != original.Identity.RunId ||
                        proof.GetProperty("identity").GetProperty("runId").GetString() != original.Identity.RunId ||
                        proof.GetProperty("state").GetString() != "stopped-within-scope" ||
                        !proof.GetProperty("cleanupComplete").GetBoolean() || proof.GetProperty("authorityRetained").GetBoolean())
                        throw new IOException("Original scoped stop ACK incomplete.");
                }
                catch (Exception error) { result["ShutdownObservationFailure"] = error.ToString(); primary ??= error; }
            }
        }
        if (primary != null) ExceptionDispatchInfo.Capture(primary).Throw();
        // Common caller checks host exit, complete durable original proof and endpoint removal.
    }
}
