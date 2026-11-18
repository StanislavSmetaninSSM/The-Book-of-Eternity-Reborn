using System.Diagnostics;
using System.Text.Json;
using BookOfEternityClient.Services.GmRuntime;

namespace BookOfEternityClient.Tests;

internal static partial class GmBridgeConsumerScenarioDriver
{
    private static async Task ObserveOriginalSoakAsync(string status, GmSessionRunRecord original,
        string root, Task<int> running, string output, Dictionary<string, object?> result, CancellationToken token)
    {
        result["SoakOriginalIdentity"] = original.Identity;
        var originalEndpoint = BridgeTransportConnector.DecodeStatus(status);
        var clock = Stopwatch.StartNew();
        var count = 0;
        // Every complete reply (including the first rejected reply) survives outside Git.
        await using var samples = new StreamWriter(Path.Combine(output, "soak-samples.jsonl"), false);
        do
        {
            var reply = await BridgeTransportConnector.RequestAsync(status,
                JsonSerializer.Serialize(new { command = "status" }), 8000, token);
            await samples.WriteLineAsync(JsonSerializer.Serialize(new { seconds = clock.Elapsed.TotalSeconds, reply }));
            await samples.FlushAsync(token);
            count++;
            using var json = JsonDocument.Parse(reply);
            var current = json.RootElement.GetProperty("status");
            var record = GmSessionRunRecordCodec.Decode(GmSessionRunPersistence.Read(root)
                ?? throw new IOException("Original soak record disappeared."));
            if (!json.RootElement.GetProperty("ok").GetBoolean() ||
                current.GetProperty("terminalRunId").GetString() != original.Identity.RunId ||
                current.GetProperty("terminalUncertain").GetBoolean() ||
                BridgeTransportConnector.DecodeStatus(current.GetRawText()) != originalEndpoint ||
                record.Identity != original.Identity || record.Disposition != GmSessionRunDisposition.Running || running.IsCompleted)
                throw new IOException("Original soak owner/status changed; raw first bad sample retained.");
            if (clock.Elapsed >= TimeSpan.FromSeconds(120)) break;
            await Task.Delay(TimeSpan.FromSeconds(Math.Max(0, Math.Min(5, 120 - clock.Elapsed.TotalSeconds))), token);
        } while (true);
        result["SoakSampleCount"] = count;
        result["SoakObservedSeconds"] = clock.Elapsed.TotalSeconds;
    }

    private static async Task StopOriginalSoakAsync(string originalStatus, GmSessionRunRecord original, string output)
    {
        // Separate bounded cleanup budget; captured endpoint and identity only, never reread a new owner.
        using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        var reply = await BridgeTransportConnector.RequestAsync(originalStatus, JsonSerializer.Serialize(new
        {
            command = "shutdown", rootKey = original.Identity.RootKey, expectedMainIdentity = original.Identity
        }), 8000, cleanup.Token);
        await File.WriteAllTextAsync(Path.Combine(output, "soak-original-shutdown.json"), reply);
        using var json = JsonDocument.Parse(reply);
        var status = json.RootElement.GetProperty("status");
        var proof = status.GetProperty("terminalStop");
        if (!json.RootElement.GetProperty("ok").GetBoolean() ||
            status.GetProperty("terminalRunId").GetString() != original.Identity.RunId ||
            proof.GetProperty("identity").GetProperty("runId").GetString() != original.Identity.RunId ||
            proof.GetProperty("state").GetString() != "stopped-within-scope" ||
            !proof.GetProperty("cleanupComplete").GetBoolean() || proof.GetProperty("authorityRetained").GetBoolean())
            throw new IOException("Soak original scoped shutdown was not proven; no retry or replacement owner.");
    }
}
