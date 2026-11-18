using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services.GmRuntime;
namespace BookOfEternityClient.Tests;

internal static partial class GmBridgeConsumerScenarioDriver
{
    private static bool IsExpectedUnresolvedStop(Exception error, GmSessionRunRecord original)
    {
        if (error is AggregateException aggregate)
            return aggregate.Flatten().InnerExceptions.Count > 0 &&
                aggregate.Flatten().InnerExceptions.All(e => IsExpectedUnresolvedStop(e, original));
        if (error is not TimeoutException ||
            error.Message != "Original terminal owner is retained as Uncertain." ||
            error.InnerException is not InvalidOperationException ||
            error.InnerException.Message != "Original terminal scoped stop is unconfirmed.") return false;
        var report = GmSessionRunCoordinator.GetStopFailureReport(error);
        return report?.OriginalOwnerIdentity == original.Identity &&
            report.FirstFailure?.Stage == "pin_drain_timeout" &&
            report.FirstFailure.State?.Pins == 1 && report.FirstFailure.State.PinDrainCompleted == false &&
            report.CurrentFailure.Stage == "native_scope_stop" &&
            report.CurrentFailure.FirstFailedPredicate == "bridge_terminal_uncertain";
    }

    private static async Task VerifyUnresolvedLossAsync(string mode, string status, GmSessionRunRecord original,
        string root, string output, Dictionary<string, object?> result, CancellationToken token)
    {
        var endpoint = BridgeTransportConnector.DecodeStatus(status);
        if (endpoint.Kind != "linux-fifo-v1") throw new IOException("Loss control requires actual FIFO.");
        MainOperationReply grant;
        await using (var stream = await BridgeTransportConnector.ConnectAsync(endpoint, token))
        {
            var reader = new MainOperationReader(stream);
            var operation = Guid.NewGuid().ToString("N");
            await MainOperationReader.WriteAsync(stream, new { command = "beginMainOperation", rootKey = root,
                operationId = operation, expectedMainIdentity = original.Identity }, token);
            grant = await reader.ReadAsync<MainOperationReply>(token) ?? throw new IOException("No original grant.");
            if (!grant.Ok || grant.State != MainOperationState.PreparedGrant || grant.Identity != original.Identity ||
                grant.OperationId != operation || !Guid.TryParseExact(grant.PinId,"N",out _) || !Guid.TryParseExact(grant.CloseId,"N",out _))
                throw new IOException("Original grant mismatch.");
            if (mode == "terminal-main-loss-active")
            {
                await MainOperationReader.WriteAsync(stream, new MainOperationFrame { Command = "activateMainOperation",
                    PinId = grant.PinId, CloseId = grant.CloseId, OperationId = operation, Identity = original.Identity }, token);
                var active = await reader.ReadAsync<MainOperationReply>(token);
                if (active != (grant with { State = MainOperationState.Active })) throw new IOException("Active control never activated.");
                result["ActiveBeforeLoss"] = true;
            }
            result["OriginalGrant"] = grant;
            // Intentionally lose this exact connection before any close; no stop fence exists yet.
        }
        var identity = new MainOperationClose(grant.PinId!, grant.CloseId!, grant.OperationId!, original.Identity,
            MainOperationOutcome.Completed, false);
        using (var observed = CancellationTokenSource.CreateLinkedTokenSource(token))
        {
            observed.CancelAfter(TimeSpan.FromSeconds(4));
            while (true)
            {
                var raw = await BridgeTransportConnector.RequestAsync(status, JsonSerializer.Serialize(new {
                    command = "mainOperationStatus", mainOperationClose = identity }), 3000, observed.Token);
                var pin = JsonSerializer.Deserialize<MainOperationReply>(raw, MainOperationReader.Json) ?? throw new IOException("Pin lookup absent.");
                if (pin != (grant with { State = pin.State })) throw new IOException("Original pin lookup changed grant identity.");
                if (pin.State == MainOperationState.Unresolved) { result["UnresolvedBeforeStop"] = pin; break; }
                if (pin.State is MainOperationState.ClosedObserved or MainOperationState.RevokedBeforeActivation)
                    throw new IOException("Pre-stop loss incorrectly retired the original pin.");
                await Task.Delay(5, observed.Token);
            }
        }
        await VerifyOriginalUnresolvedStopAsync(status,original,root,output,result,token);
    }
    private static async Task VerifyOriginalUnresolvedStopAsync(string status,GmSessionRunRecord original,
        string root,string output,Dictionary<string,object?> result,CancellationToken token)
    {
        var response = await BridgeTransportConnector.RequestAsync(status, JsonSerializer.Serialize(new {
            command = "shutdown", rootKey = root, expectedMainIdentity = original.Identity }), 10000, token);
        await File.WriteAllTextAsync(Path.Combine(output, "unresolved-original-shutdown.json"), response);
        using var document = JsonDocument.Parse(response);
        var ack = document.RootElement; var state = ack.GetProperty("status"); var proof = state.GetProperty("terminalStop");
        var final = GmSessionRunRecordCodec.Decode(GmSessionRunPersistence.Read(root) ?? throw new IOException("Original retained record absent."));
        result["OriginalFinalRecord"] = final;
        if (ack.GetProperty("ok").GetBoolean() || state.GetProperty("terminalRunId").GetString() != original.Identity.RunId ||
            final.Identity != original.Identity || final.Disposition != GmSessionRunDisposition.Uncertain || final.StopEvidence != null ||
            proof.GetProperty("identity").GetProperty("runId").GetString() != original.Identity.RunId ||
            proof.GetProperty("state").GetString() != "stopped-within-scope" ||
            !proof.GetProperty("cleanupComplete").GetBoolean() || proof.GetProperty("authorityRetained").GetBoolean())
            throw new IOException("Lost pin did not retain logical refusal with independent original physical cleanup.");
        result["ExpectedLogicalRefusalWithPhysicalCleanup"] = true;
    }
}
