using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;

namespace BookOfEternityClient.Services;

public partial class ProgressionScheduleService
{
    // A verified durable result makes the later ordinary scheduler call inert. DATA is re-read under this operation's lease.
    internal async Task<bool> IsPublishedSpiritualCycleAsync(ProgressionControl control,
        FileSystemManager.CanonicalWriteLease? existingLease = null)
    {
        if (!HasResolvedRealm(control.CurrentRealm) || !IsAfterlifeRealm(control.CurrentRealm)) return false;
        if (existingLease is null)
        {
            await using var owned = await _fs.AcquireCanonicalWriteLeaseAsync();
            return await IsPublishedSpiritualCycleAsync(control, owned);
        }
        var lease = existingLease;
        _fs.VerifyCurrentSessionOperation(lease);
        var context = await ReadCurrentTurnRequestContextAsync(lease);
        if (context is null) return false;
        var historyJson = await _fs.ReadFileAsync(lease, WoundHistoryState.HistoryPath);
        if (historyJson is null) return false;
        var history = WoundHistoryState.Parse(historyJson, WoundHistoryState.HistoryPath);
        if (!history.IsValid)
            throw new InvalidDataException("Published spiritual cycle history is not canonical.");
        var matching = history.State!.Transitions.Select(t => t.TransitionResult).OfType<SpiritualHealingPersistedResult>()
            .Select(r => (Result: r, Root: r.ToCanonicalJson())).Where(p =>
                p.Root["binding"]!["sessionId"]!.GetValue<string>() == context.Value.SessionId &&
                p.Root["binding"]!["requestId"]!.GetValue<string>() == context.Value.RequestId &&
                p.Root["binding"]!["turn"]!.GetValue<int>() == context.Value.TurnNumber).ToArray();
        if (matching.Length == 0) return false;
        if (matching.Length != 1) throw new InvalidDataException("Published spiritual cycle identity is ambiguous.");
        var accepted = matching[0];
        var suppliedControl = WoundAcceptedTurnFingerprintWriter.CanonicalJson(JsonSerializer.SerializeToNode(control)!);
        if (suppliedControl != WoundAcceptedTurnFingerprintWriter.CanonicalJson(accepted.Root["control"]!) ||
            await _fs.ReadFileBytesAsync(lease, ReportPath) is not null ||
            await _fs.ReadFileBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath) is not null)
            throw new InvalidDataException("A published spiritual cycle has changed control or a replaced consumed input.");
        var schedule = await _fs.ReadFileBytesAsync(lease, SchedulePath);
        if (schedule is null || new CanonicalBeforeImage(true, schedule).Fingerprint != accepted.Root["scheduleAfterFingerprint"]!.GetValue<string>())
            throw new InvalidDataException("A published spiritual cycle has changed its complete scheduler after-image.");
        var roots = new Dictionary<string, JsonObject?>(StringComparer.Ordinal);
        foreach (var path in WoundAcceptedTurnSnapshotContract.CarrierPaths)
        {
            var text = await _fs.ReadFileAsync(lease, path);
            roots[path] = text is null ? null : StrictJsonAuthority.Deserialize<JsonObject>(text, new JsonSerializerOptions(), path);
        }
        var catalog = WoundCarrierCatalog.Build(new(roots[WoundCarrierCatalog.PlayerPath], roots[WoundCarrierCatalog.NpcPath],
            roots[WoundCarrierCatalog.EnemiesPath], roots[WoundCarrierCatalog.AlliesPath], roots[WoundCarrierCatalog.AfterlifeProfilesPath]));
        var identities = WoundIdentityState.Parse(await _fs.ReadFileAsync(lease, WoundIdentityState.StatePath), WoundIdentityState.StatePath);
        if (catalog.Issues.Count != 0 || !identities.IsValid || history.State.ValidateAgreement(identities.State!, catalog).Count != 0)
            throw new InvalidDataException("Published spiritual cycle lost current wound/identity/history agreement.");
        foreach (var group in accepted.Result.Stages.GroupBy(s => s.After.WoundId))
            if (history.State.Transitions.Last(t => t.WoundId == group.Key).TransitionId != group.Last().TransitionId)
                throw new InvalidDataException("A published spiritual result no longer describes the current wound cohort.");
        return true;
    }
}
