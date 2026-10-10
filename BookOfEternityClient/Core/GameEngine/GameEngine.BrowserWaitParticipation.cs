using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using BookOfEternityClient.Services.GmRuntime;

namespace BookOfEternityClient.Core;

public partial class GameEngine
{
    private Task<T> InspectOriginalBrowserWaitAsync<T>(
        PendingPlayerActionService.Staged? staged, Func<Task<T>> inspection)
    {
        if (staged == null) return inspection();
        return SessionOperationContext.RunParticipatingExpectedSessionAsync(_fs, staged.Binding.Generation, async () =>
        {
            // The privately issued scope rereads the complete original tuple and
            // signed snapshot/rollback bytes under this actual original owner.
            using var original = _fs.BeginBrowserOriginalOperation(staged.Binding, staged);
            return await inspection();
        });
    }

#if DEBUG
    private async Task HoldOriginalBrowserIdleCutAsync(PendingPlayerActionService.Staged staged,
        MainOperationClose? close, bool observed, bool remote)
    {
        var path = Environment.GetEnvironmentVariable("BOE_TEST_BROWSER_IDLE_CUT_PATH");
        if (string.IsNullOrWhiteSpace(path)) return;
        var nonce = Environment.GetEnvironmentVariable("BOE_TEST_BROWSER_IDLE_CUT_NONCE");
        if (!Guid.TryParseExact(nonce, "N", out var parsed) || parsed.ToString("N") != nonce)
            throw new InvalidDataException("Original browser idle test nonce is invalid.");

        // This is called only after the complete top-level staging await returned.
        // Neither the close callback nor this read-only ACK grants recovery.
        _fs.RequireLoadIpcOutsideFileScopes();
        var proof = _fs.InspectOriginalBrowserClosedIdle(staged);
        if (!remote || !observed || close == null || close.ClosingFailed ||
            close.Outcome != MainOperationOutcome.Completed || proof.Condition.ActiveIdentity == null ||
            !GmSessionRunValidation.IdentityMatches(close.Identity, proof.Condition.ActiveIdentity))
            throw new InvalidDataException("Original staging did not establish a successfully closed idle boundary.");

        var ackPath = Path.GetFullPath(path);
        var scope = new TrustedLocalFileScope([Path.GetDirectoryName(ackPath)!]);
        var requestPath = ackPath + ".request";
        var watch = Stopwatch.StartNew();
        while (!File.Exists(scope.ValidateFile(requestPath)))
        {
            if (watch.Elapsed > TimeSpan.FromSeconds(10))
                throw new TimeoutException("Original browser idle test request was not published.");
            await Task.Delay(25);
        }
        var request = StrictJsonAuthority.Deserialize<JsonObject>(
            File.ReadAllText(scope.ValidateFile(requestPath, false)), JsonOpts, "browser idle test request")!;
        if (request["schemaVersion"]?.GetValue<int>() != 1 || request["nonce"]?.GetValue<string>() != nonce ||
            request["pid"]?.GetValue<int>() != Environment.ProcessId ||
            request["actionId"]?.GetValue<string>() != staged.Binding.ActionId ||
            request["requestId"]?.GetValue<string>() != staged.Binding.ActionId ||
            request["generation"]?.GetValue<string>() != staged.Binding.Generation)
            throw new InvalidDataException("Original browser idle test request does not match the actual staged turn.");
        var ack = JsonSerializer.Serialize(new
        {
            schemaVersion = 1, nonce, pid = Environment.ProcessId,
            actionId = staged.Binding.ActionId, requestId = staged.Binding.ActionId,
            generation = staged.Binding.Generation, close, observed, remote,
            topLevelStagingAwaitReturned = true, outsideParticipation = true,
            artifactHashes = proof.Hashes
        }, MainOperationReader.Json);
        await using (var output = new FileStream(scope.ValidateFile(ackPath), FileMode.CreateNew,
                         FileAccess.Write, FileShare.Read))
        {
            await output.WriteAsync(Encoding.UTF8.GetBytes(ack));
            await output.FlushAsync();
        }
        watch.Restart();
        var releasePath = ackPath + ".release";
        while (!File.Exists(scope.ValidateFile(releasePath)))
        {
            if (watch.Elapsed > TimeSpan.FromSeconds(20))
                throw new TimeoutException("Original browser idle test cut was not completed.");
            await Task.Delay(25);
        }
        if (File.ReadAllText(scope.ValidateFile(releasePath, false)) != nonce)
            throw new InvalidDataException("Original browser idle test release does not match its nonce.");
    }
#endif
}
