using System.IO.Pipes;
using System.Reflection;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services.GmRuntime;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookOfEternityClient.Tests;

internal static partial class MainOperationScenarioDriver
{
    private static async Task VerifyOriginalRunExpectationAsync(string coordinate, string root, string pipeName,
        object host, Type type, Dictionary<string, object?> result, bool actualClient = false)
    {
        var owner = (GmSessionRunCoordinator)type.GetField("_mainRun", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(host)!;
        if(owner.Record?.Disposition != GmSessionRunDisposition.Running)throw new InvalidOperationException("Original live owner prerequisite failed.");
        var original = owner.Identity;
        var expected = coordinate switch
        {
            "same" => original,
            "run" => original with { RunId = Guid.NewGuid().ToString("N") },
            "generation" => original with { GenerationId = Guid.NewGuid().ToString("N") },
            "epoch" => original with { Epoch = original.Epoch + 1 },
            "host" => original with { HostInstanceId = Guid.NewGuid().ToString("N") },
            "boot" => original with { BootId = "different-original-boot" },
            "backend" => original with { Backend = GmSessionRunBackend.WindowsJob },
            "root" => original with { RootKey = root + "-different" },
            _ => throw new ArgumentOutOfRangeException(nameof(coordinate))
        };
        var pins = (System.Collections.IDictionary)typeof(GmSessionRunCoordinator)
            .GetField("_remotePins", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!;
        var before = pins.Count;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        if(actualClient)
        {
            Exception? refusal = null;
            GmMainOperationClient? client = null;
            try { client = await GmMainOperationClient.OpenAsync(new FileSystemManager(root,
                NullLogger<FileSystemManager>.Instance), deadline.Token, expected); }
            catch(IOException failure) { refusal = failure; }
            if(client != null)
            {
                await using(client)
                {
                    await client.CompleteAsync(MainOperationOutcome.Completed, false);
                    result["ActualOriginalClose"] = client.TerminalClose;
                    result["CloseObserved"] = client.CloseObserved;
                }
            }
            while(pins.Count != before && !owner.IsUncertain)
                await Task.Delay(5, deadline.Token);
            result["OriginalIdentity"] = original;
            result["ExpectedIdentity"] = expected;
            result["RefusalPhase"] = refusal?.Data["ParticipatingAdmissionPhase"];
            result["PinsBefore"] = before;
            result["PinsAfter"] = pins.Count;
            result["OriginalUncertain"] = owner.IsUncertain;
            result["ExpectationMatchedBehavior"] = !owner.IsUncertain && pins.Count == before &&
                (coordinate == "same" ? client != null && client.CloseObserved :
                    client == null && (string?)refusal?.Data["ParticipatingAdmissionPhase"] == "read-record");
            return;
        }
        using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await pipe.ConnectAsync(deadline.Token);
        var operation = Guid.NewGuid().ToString("N");
        await MainOperationReader.WriteAsync(pipe, new
        {
            command = "beginMainOperation", rootKey = root, operationId = operation, expectedMainIdentity = expected
        }, deadline.Token);
        var reader = new MainOperationReader(pipe);
        var grant = await reader.ReadAsync<MainOperationReply>(deadline.Token) ?? throw new IOException("Missing actual admission reply.");
        result["OriginalIdentity"] = original;
        result["ExpectedIdentity"] = expected;
        result["Grant"] = grant;
        result["PinsBefore"] = before;
        // Even a causal RED owns its genuinely granted original connection. Close
        // it correctly before asserting, so fixture cleanup cannot hide Unresolved.
        if(grant.Ok)
        {
            if(grant.State != MainOperationState.PreparedGrant || grant.Identity != original || grant.OperationId != operation)
                throw new InvalidOperationException("Actual grant prerequisite mismatch.");
            await MainOperationReader.WriteAsync(pipe, new MainOperationFrame
            {
                Command = "activateMainOperation", PinId = grant.PinId, CloseId = grant.CloseId,
                OperationId = operation, Identity = grant.Identity
            }, deadline.Token);
            var active = await reader.ReadAsync<MainOperationReply>(deadline.Token);
            if(active != (grant with { State = MainOperationState.Active }))throw new InvalidOperationException("Actual grant did not activate.");
            var close = new MainOperationClose(grant.PinId!, grant.CloseId!, operation, original, MainOperationOutcome.Completed, false);
            await MainOperationReader.WriteAsync(pipe, new MainOperationFrame { Command = "closeMainOperation", Close = close }, deadline.Token);
            var closed = await reader.ReadAsync<MainOperationReply>(deadline.Token);
            if(closed != (grant with { State = MainOperationState.ClosedObserved }))throw new InvalidOperationException("Actual original close not observed.");
            result["ActualOriginalClose"] = close;
            result["CloseObserved"] = true;
        }
        result["PinsAfter"] = pins.Count;
        result["OriginalUncertain"] = owner.IsUncertain;
        result["ExpectationMatchedBehavior"] = coordinate == "same"
            ? grant.Ok && !owner.IsUncertain
            : !grant.Ok && pins.Count == before && !owner.IsUncertain;
    }
}
