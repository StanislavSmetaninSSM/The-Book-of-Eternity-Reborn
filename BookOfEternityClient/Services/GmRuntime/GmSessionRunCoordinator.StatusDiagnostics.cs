using System.Security.Cryptography;
using System.Text.Json;

namespace BookOfEternityClient.Services.GmRuntime;

internal sealed partial class GmSessionRunCoordinator
{
    // Observational DATA only: never signals, retries, admits work or changes owner state.
    internal void ReportStatusPublicationData(byte[] exactSnapshot, bool published,
        bool priorActualPublication, string? decision)
    {
#if DEBUG
        try
        {
            string? nonce;
            GmSessionRunIdentity? identity;
            lock (_sync)
            {
                nonce = _stopDiagnosticNonce;
                identity = _stopDiagnosticIdentity;
                if (nonce == null || identity == null || _record?.Identity != identity) return;
            }
            if (Environment.GetEnvironmentVariable(StopDiagnosticNonceVariable) != nonce) return;
            using var snapshot = JsonDocument.Parse(exactSnapshot);
            var snapshotUpdatedAtUtc = snapshot.RootElement.GetProperty("updatedAtUtc").GetString();
            var report = new
            {
                SchemaVersion = 1, Nonce = nonce, OriginalOwnerIdentity = identity,
                Stage = published ? "Published" : "Deferred", PriorActualPublication = priorActualPublication,
                Decision = decision, SnapshotUpdatedAtUtc = snapshotUpdatedAtUtc, SnapshotSHA256 = Convert.ToHexString(SHA256.HashData(exactSnapshot)).ToLowerInvariant(),
                ObservedAtUtc = DateTimeOffset.UtcNow.ToString("O")
            };
            var line = "BOE_STATUS_PUBLICATION_DATA " + JsonSerializer.Serialize(report);
            // Recheck the captured owner fence before the protected sink, never under its lock.
            lock (_sync)
                if (_stopDiagnosticNonce != nonce || _stopDiagnosticIdentity != identity || _record?.Identity != identity) return;
            if (Environment.GetEnvironmentVariable(StopDiagnosticNonceVariable) != nonce) return;
            Console.Error.WriteLine(line);
        }
        catch { /* Diagnostic failure cannot change publication, refusal or cleanup. */ }
#endif
    }
}
