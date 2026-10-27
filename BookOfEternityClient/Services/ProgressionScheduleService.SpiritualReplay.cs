using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;

namespace BookOfEternityClient.Services;

public partial class ProgressionScheduleService
{
    // A verified durable result makes the later ordinary scheduler call inert. DATA is re-read under this operation's lease.
    internal async Task<bool> IsPublishedSpiritualCycleAsync(ProgressionControl control,
        FileSystemManager.CanonicalWriteLease? existingLease = null,
        string? expectedSnapshotToken = null, JsonObject? expectedRequest = null)
    {
        if (!HasResolvedRealm(control.CurrentRealm) || !IsAfterlifeRealm(control.CurrentRealm)) return false;
        if (existingLease is null)
        {
            await using var owned = await _fs.AcquireCanonicalWriteLeaseAsync();
            return await IsPublishedSpiritualCycleAsync(control, owned, expectedSnapshotToken, expectedRequest);
        }
        var lease = existingLease;
        _fs.VerifyCurrentSessionOperation(lease);
        var context = await ReadCurrentTurnRequestContextAsync(lease);
        if (context is null) return false;
        return await IsPublishedSpiritualCycleCoreAsync(control, context.Value.SessionId, context.Value.RequestId,
            context.Value.TurnNumber, path => _fs.ReadFileBytesAsync(lease, path), expectedSnapshotToken, expectedRequest);
    }

    // Same strict durable agreement for an authenticated original whose physical artifacts were already cleaned.
    // The caller still owns its current lease/receipt/generation and cleanup admission.
    internal static async Task<bool> IsPublishedSpiritualCycleCoreAsync(ProgressionControl control,
        string sessionId, string requestId, int turn, Func<string, Task<byte[]?>> read,
        string? expectedSnapshotToken = null, JsonObject? expectedRequest = null)
    {
        if (!HasResolvedRealm(control.CurrentRealm) || !IsAfterlifeRealm(control.CurrentRealm)) return false;
        if ((expectedSnapshotToken is null) != (expectedRequest is null))
            throw new InvalidDataException("Published spiritual original binding is incomplete.");
        async Task<string?> Text(string path)
        {
            var bytes = await read(path);
            return bytes is null ? null : CanonicalJsonUtf8.DecodeOneOptionalBom(bytes);
        }
        var historyJson = await Text(WoundHistoryState.HistoryPath);
        if (historyJson is null) return false;
        var history = WoundHistoryState.Parse(historyJson, WoundHistoryState.HistoryPath);
        if (!history.IsValid)
            throw new InvalidDataException("Published spiritual cycle history is not canonical.");
        var matching = history.State!.Transitions.Select(t => t.TransitionResult).OfType<SpiritualHealingPersistedResult>()
            .Select(r => (Result: r, Root: r.ToCanonicalJson())).Where(p =>
                p.Root["binding"]!["sessionId"]!.GetValue<string>() == sessionId &&
                p.Root["binding"]!["requestId"]!.GetValue<string>() == requestId &&
                p.Root["binding"]!["turn"]!.GetValue<int>() == turn).ToArray();
        if (matching.Length == 0) return false;
        if (matching.Length != 1) throw new InvalidDataException("Published spiritual cycle identity is ambiguous.");
        var accepted = matching[0];
        if (expectedSnapshotToken is not null &&
            (accepted.Root["binding"]!["snapshotToken"]!.GetValue<string>() != expectedSnapshotToken || expectedRequest is null ||
             WoundAcceptedTurnFingerprintWriter.CanonicalJson(accepted.Root["request"]!) != WoundAcceptedTurnFingerprintWriter.CanonicalJson(expectedRequest)))
            throw new InvalidDataException("Published spiritual replay differs from the exact signed original request and snapshot.");
        var suppliedControl = WoundAcceptedTurnFingerprintWriter.CanonicalJson(JsonSerializer.SerializeToNode(control)!);
        if (suppliedControl != WoundAcceptedTurnFingerprintWriter.CanonicalJson(accepted.Root["control"]!) ||
            await read(ReportPath) is not null ||
            await read(AcceptedMechanicsPlan.WoundCommandPath) is not null)
            throw new InvalidDataException("A published spiritual cycle has changed control or a replaced consumed input.");
        var schedule = await read(SchedulePath);
        if (schedule is null || new CanonicalBeforeImage(true, schedule).Fingerprint != accepted.Root["scheduleAfterFingerprint"]!.GetValue<string>())
            throw new InvalidDataException("A published spiritual cycle has changed its complete scheduler after-image.");
        var roots = new Dictionary<string, JsonObject?>(StringComparer.Ordinal);
        foreach (var path in WoundAcceptedTurnSnapshotContract.CarrierPaths)
        {
            var text = await Text(path);
            roots[path] = text is null ? null : StrictJsonAuthority.Deserialize<JsonObject>(text, new JsonSerializerOptions(), path);
        }
        var catalog = WoundCarrierCatalog.Build(new(roots[WoundCarrierCatalog.PlayerPath], roots[WoundCarrierCatalog.NpcPath],
            roots[WoundCarrierCatalog.EnemiesPath], roots[WoundCarrierCatalog.AlliesPath], roots[WoundCarrierCatalog.AfterlifeProfilesPath]));
        var identities = WoundIdentityState.Parse(await Text(WoundIdentityState.StatePath), WoundIdentityState.StatePath);
        if (catalog.Issues.Count != 0 || !identities.IsValid || history.State.ValidateAgreement(identities.State!, catalog).Count != 0)
            throw new InvalidDataException("Published spiritual cycle lost current wound/identity/history agreement.");
        foreach (var group in accepted.Result.Stages.GroupBy(s => s.After.WoundId))
            if (history.State.Transitions.Last(t => t.WoundId == group.Key).TransitionId != group.Last().TransitionId)
                throw new InvalidDataException("A published spiritual result no longer describes the current wound cohort.");
        return true;
    }
}
