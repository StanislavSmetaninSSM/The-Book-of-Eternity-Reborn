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
        string? expectedSnapshotToken = null, JsonObject? expectedRequest = null,
        JsonObject? expectedNaturalProjection = null)
    {
        if (!HasResolvedRealm(control.CurrentRealm) || !IsAfterlifeRealm(control.CurrentRealm)) return false;
        if (existingLease is null)
        {
            await using var owned = await _fs.AcquireCanonicalWriteLeaseAsync();
            return await IsPublishedSpiritualCycleAsync(control, owned, expectedSnapshotToken, expectedRequest, expectedNaturalProjection);
        }
        var lease = existingLease;
        _fs.VerifyCurrentSessionOperation(lease);
        var context = await ReadCurrentTurnRequestContextAsync(lease);
        if (context is null) return false;
        return await IsPublishedSpiritualCycleCoreAsync(control, context.Value.SessionId, context.Value.RequestId,
            context.Value.TurnNumber, path => _fs.ReadFileBytesAsync(lease, path), expectedSnapshotToken, expectedRequest, expectedNaturalProjection);
    }

    // Same strict durable agreement for an authenticated original whose physical artifacts were already cleaned.
    // The caller still owns its current lease/receipt/generation and cleanup admission.
    internal static async Task<bool> IsPublishedSpiritualCycleCoreAsync(ProgressionControl control,
        string sessionId, string requestId, int turn, Func<string, Task<byte[]?>> read,
        string? expectedSnapshotToken = null, JsonObject? expectedRequest = null,
        JsonObject? expectedNaturalProjection = null) =>
        await ReadPublishedSpiritualCycleCoreAsync(control, sessionId, requestId, turn, read,
            expectedSnapshotToken, expectedRequest, expectedNaturalProjection) is not null;

    // Verified immutable DATA, not a new mutation or publication capability. The caller owns fresh closing guards.
    internal static async Task<SpiritualHealingPersistedResult?> ReadPublishedSpiritualCycleCoreAsync(ProgressionControl control,
        string sessionId, string requestId, int turn, Func<string, Task<byte[]?>> read,
        string? expectedSnapshotToken = null, JsonObject? expectedRequest = null,
        JsonObject? expectedNaturalProjection = null) =>
        (await ReadPublishedSpiritualCycleDataAsync(control, sessionId, requestId, turn, read,
            expectedSnapshotToken, expectedRequest, expectedNaturalProjection))?.Result;

    // Operation-local parsed DATA export for pre-control aid admission. No caller
    // can pass this object instead of the actual lease/source closing checks.
    internal sealed record PublishedSpiritualCycleData(SpiritualHealingPersistedResult Result,
        JsonObject Root, WoundHistoryState History, WoundIdentityState Identities,
        WoundCarrierCatalog Catalog, IReadOnlyDictionary<string, JsonObject?> CarrierRoots, SpiritualDiagnosisPersistedResult? Diagnosis = null);

    internal static async Task<PublishedSpiritualCycleData?> ReadPublishedSpiritualCycleDataAsync(ProgressionControl control,
        string sessionId, string requestId, int turn, Func<string, Task<byte[]?>> read,
        string? expectedSnapshotToken = null, JsonObject? expectedRequest = null,
        JsonObject? expectedNaturalProjection = null)
    {
        if (!HasResolvedRealm(control.CurrentRealm) || !IsAfterlifeRealm(control.CurrentRealm)) return null;
        // The scheduler's ordinary settlement call supplies no purported original.
        // Any caller claiming an actual original must bind exactly one complete
        // self request or natural cohort/tier/projection, in addition to its token.
        if (expectedSnapshotToken is null ? expectedRequest is not null || expectedNaturalProjection is not null
            : (expectedRequest is null) == (expectedNaturalProjection is null))
            throw new InvalidDataException("Published spiritual original binding is incomplete.");
        var historyBytes = await read(WoundHistoryState.HistoryPath);
        if (historyBytes is null) return null;
        var history = WoundHistoryState.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(historyBytes), WoundHistoryState.HistoryPath);
        if (!history.IsValid) throw new InvalidDataException("Published spiritual cycle history is not canonical.");
        return await ReadPublishedSpiritualCycleDataCoreAsync(control, sessionId, requestId, turn, read, history.State!,
            expectedSnapshotToken, expectedRequest, expectedNaturalProjection);
    }

    // A completed Engine turn cleans its transient request. Select the actual
    // client-owned participation result from current History, then apply exactly
    // the same strict current settlement proof. Its own token is not an
    // independently supplied expectedOriginal claim.
    internal static async Task<PublishedSpiritualCycleData?> ReadPublishedParticipationCycleDataAsync(
        string sessionId, int grantTurn, SpiritualSelfTreatmentSelection selection, Func<string, Task<byte[]?>> read)
    {
        var aid = selection.Aid ?? throw new InvalidDataException("A participation reader requires an exact aid selection.");
        var bytes = await read(WoundHistoryState.HistoryPath);
        if (bytes is null) return null;
        var parsed = WoundHistoryState.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(bytes), WoundHistoryState.HistoryPath);
        if (!parsed.IsValid) throw new InvalidDataException("Published participation History is not canonical.");
        var candidates = parsed.State!.Transitions.Where(t => t.TransitionResult is SpiritualHealingPersistedResult
            { IsNatural: true } result && result.SchemaVersion == aid.FamilyVersion && result.MatchesTurn(sessionId, grantTurn)).Select(t =>
            (Row: t, Root: t.TransitionResult!.ToCanonicalJson())).ToArray();
        if (candidates.Length == 0) return null;
        if (candidates.Length != 1) throw new InvalidDataException("Published participation identity is ambiguous.");
        var candidate = candidates[0];
        var outcomes = candidate.Root["participationOutcomes"]!.AsArray();
        var record = outcomes[0]!["record"]!.AsObject();
        if (outcomes.Count != 1 || record["primaryTransitionId"]!.GetValue<string>() != candidate.Row.TransitionId ||
            !SpiritualTreatmentParticipation.MatchesSelection(record, aid) ||
            record["woundId"]!.GetValue<string>() != selection.WoundId ||
            SpiritualTreatmentParticipation.Fingerprint(record) != aid.ParticipationFingerprint)
            throw new InvalidDataException("Published participation does not match its actual primary row and selected content.");
        var control = candidate.Root["control"]!.Deserialize<ProgressionControl>()!;
        return await ReadPublishedSpiritualCycleDataCoreAsync(control, sessionId,
            candidate.Root["binding"]!["requestId"]!.GetValue<string>(), grantTurn, read, parsed.State);
    }

    private static async Task<PublishedSpiritualCycleData?> ReadPublishedSpiritualCycleDataCoreAsync(ProgressionControl control,
        string sessionId, string requestId, int turn, Func<string, Task<byte[]?>> read, WoundHistoryState history,
        string? expectedSnapshotToken = null, JsonObject? expectedRequest = null, JsonObject? expectedNaturalProjection = null)
    {
        async Task<string?> Text(string path)
        {
            var bytes = await read(path);
            return bytes is null ? null : CanonicalJsonUtf8.DecodeOneOptionalBom(bytes);
        }
        var matching = history.Transitions.Select(t => t.TransitionResult).OfType<SpiritualHealingPersistedResult>()
            .Where(r => r.MatchesBinding(sessionId, requestId, turn))
            .Select(r => (Result: r, Root: r.ToCanonicalJson())).ToArray();
        if (matching.Length == 0) return null;
        if (matching.Length != 1) throw new InvalidDataException("Published spiritual cycle identity is ambiguous.");
        var accepted = matching[0];
        var diagnoses = history.Transitions.Select(t => t.TransitionResult).OfType<SpiritualDiagnosisPersistedResult>()
            .Where(r => r.MatchesBinding(sessionId, requestId, turn)).ToArray();
        if (diagnoses.Length > 1) throw new InvalidDataException("Published diagnosis identity is ambiguous.");
        var diagnosis = diagnoses.SingleOrDefault();
        diagnosis?.RequireNatural(accepted.Result);
        if (expectedSnapshotToken is not null)
        {
            if (accepted.Root["binding"]!["snapshotToken"]!.GetValue<string>() != expectedSnapshotToken ||
                (diagnosis is not null ? expectedRequest is null || expectedNaturalProjection is not null ||
                    !JsonNode.DeepEquals(diagnosis.Request, expectedRequest)
                : accepted.Result.IsNatural ? expectedNaturalProjection is null || expectedRequest is not null ||
                    !JsonNode.DeepEquals(accepted.Root["source"]!["projection"], expectedNaturalProjection)
                    : expectedRequest is null || expectedNaturalProjection is not null ||
                    !JsonNode.DeepEquals(accepted.Root["request"], expectedRequest)))
                throw new InvalidDataException("Published spiritual replay differs from the exact signed original family, cohort and snapshot.");
        }
        var suppliedControl = WoundAcceptedTurnFingerprintWriter.CanonicalJson(JsonSerializer.SerializeToNode(control)!);
        if (suppliedControl != WoundAcceptedTurnFingerprintWriter.CanonicalJson(accepted.Root["control"]!) ||
            await read(ReportPath) is not null ||
            await read(AcceptedMechanicsPlan.WoundCommandPath) is not null ||
            await read(ResourceMaterializationContract.CommandPath) is not null ||
            await read(EffectAcceptedTurnPlan.CommandPath) is not null)
            throw new InvalidDataException("A published spiritual cycle has changed control or a replaced consumed input.");
        if (diagnosis is not null && await read(ResourcePendingResolutionState.PendingPath) is not null)
            throw new InvalidDataException("Published diagnosis cannot retain or replace pending resolution work.");
        var schedule = await read(SchedulePath);
        if (schedule is null || new CanonicalBeforeImage(true, schedule).Fingerprint != accepted.Root["scheduleAfterFingerprint"]!.GetValue<string>())
            throw new InvalidDataException("A published spiritual cycle has changed its complete scheduler after-image.");
        if (accepted.Result.SchemaVersion == 7)
        {
            // This reader proves CURRENT complete settlement, not historical
            // authenticity forever. History parsing remains independent of the
            // present wallet. Never charge or restore anything on this read path.
            var soulJson = await Text(AfterlifeWoundHealingPlanner.Continuation.ServiceSoulPath);
            var soul = soulJson is null ? null : StrictJsonAuthority.Deserialize<JsonObject>(soulJson,
                new JsonSerializerOptions(), "published service Soul settlement");
            if (soul is null || GuardianPolicyContracts.TryDescribeInvalidCanonicalInkFeathersRoot(soul, out _) ||
                WoundAcceptedTurnFingerprintWriter.CanonicalJson(soul["inkFeathers"]) !=
                    WoundAcceptedTurnFingerprintWriter.CanonicalJson(accepted.Root["request"]!["payment"]!["walletAfter"]))
                throw new InvalidDataException("Published service current wallet differs from its exact accepted payment after-image.");
        }
        if (accepted.Root["request"] is JsonObject negotiatedRequest && negotiatedRequest.ContainsKey("currencyInvariant"))
        {
            // Current settlement only; historical result parsing never reads or restores today's wallet.
            var invariant = SpiritualTreatmentWalletInvariant.Parse(negotiatedRequest["currencyInvariant"]);
            var soulJson = await Text(AfterlifeWoundHealingPlanner.Continuation.ServiceSoulPath);
            var soul = soulJson is null ? null : StrictJsonAuthority.Deserialize<JsonObject>(soulJson,
                new JsonSerializerOptions(), "published negotiated Soul settlement");
            invariant.RequirePreserved(soul ?? throw new InvalidDataException("Published negotiated Soul is absent."));
        }
        var roots = new Dictionary<string, JsonObject?>(StringComparer.Ordinal);
        foreach (var path in WoundAcceptedTurnSnapshotContract.CarrierPaths)
        {
            var text = await Text(path);
            roots[path] = text is null ? null : StrictJsonAuthority.Deserialize<JsonObject>(text, new JsonSerializerOptions(), path);
        }
        var catalog = WoundCarrierCatalog.Build(new(roots[WoundCarrierCatalog.PlayerPath], roots[WoundCarrierCatalog.NpcPath],
            roots[WoundCarrierCatalog.EnemiesPath], roots[WoundCarrierCatalog.AlliesPath], roots[WoundCarrierCatalog.AfterlifeProfilesPath]));
        var identities = WoundIdentityState.Parse(await Text(WoundIdentityState.StatePath), WoundIdentityState.StatePath);
        if (catalog.Issues.Count != 0 || !identities.IsValid || history.ValidateAgreement(identities.State!, catalog).Count != 0)
            throw new InvalidDataException("Published spiritual cycle lost current wound/identity/history agreement.");
        RequireCompleteSpiritualSettlementCohort(accepted.Result, history, identities.State!,
            catalog, roots[WoundCarrierCatalog.AfterlifeProfilesPath], accepted.Root["binding"]!["realm"]!.GetValue<string>(), turn, diagnosis);
        if (accepted.Root["participationOutcomes"] is JsonArray participation)
            SpiritualTreatmentParticipation.RequirePublishedRecords(participation, roots[WoundCarrierCatalog.AfterlifeProfilesPath]);
        foreach (var group in accepted.Result.Stages.GroupBy(s => s.After.WoundId))
            if (history.Transitions.Last(t => t.WoundId == group.Key).TransitionId != group.Last().TransitionId)
                throw new InvalidDataException("A published spiritual result no longer describes the current wound cohort.");
        return new(accepted.Result, accepted.Root, history, identities.State!, catalog, roots, diagnosis);
    }

    // Independent current completeness witness, not reconstruction of the historical
    // start tier or permission to replace the required original on source-bound calls.
    private static void RequireCompleteSpiritualSettlementCohort(SpiritualHealingPersistedResult result,
        WoundHistoryState history, WoundIdentityState identities, WoundCarrierCatalog catalog,
        JsonObject? profiles, string realm, int turn, SpiritualDiagnosisPersistedResult? diagnosis = null)
    {
        var stages = result.Stages;
        var eventRef = stages[0].EventRef; // The strict result codec rederives this from the exact turn binding.
        var expected = new HashSet<string>(StringComparer.Ordinal);
        foreach (var identity in identities.Entries.Where(e => e.Domain == "spiritual" && e.Realm == realm &&
                     e.CarrierPath == WoundCarrierCatalog.AfterlifeProfilesPath))
        {
            var terminal = identity.TerminalTransitionId is null ? null : history.Transitions.SingleOrDefault(t =>
                t.TransitionId == identity.TerminalTransitionId);
            if (identity.Status != "active" && terminal?.Turn != turn) continue;
            if (identity.CreatedAtTurn > turn)
                throw new InvalidDataException("Spiritual settlement cannot skip a future or reset wound chronology.");
            if (identity.CreatedAtTurn == turn)
            {
                var creation = history.Transitions.SingleOrDefault(t => t.WoundId == identity.WoundId && t.Kind == "create");
                if (creation is null || creation.Turn != turn || creation.EventRef != identity.CreatedEventRef ||
                    creation.EventRef != eventRef || creation.WoundTransitionOrdinal != 1)
                    throw new InvalidDataException("Newborn exemption lacks this accepted turn's actual creation provenance.");
                continue;
            }
            var owner = new WoundOwnerCoordinate(identity.Realm, identity.OwnerKind, identity.OwnerId, identity.CarrierPath);
            var matching = (profiles?["profiles"] as JsonArray)?.OfType<JsonObject>()
                .Where(p => WoundCarrierCollectionAuthority.AfterlifeProfileMatches(p, owner)).ToArray();
            if (matching is not { Length: 1 } || !AfterlifeEntityProfileState.TryReadRequiredWoundArtTier(matching[0],
                    AfterlifeSpiritualConflictState.SpiritualHealingArtId, out _, out _))
                throw new InvalidDataException("Spiritual settlement lost a persistent original owner or its current canonical art.");
            if (identity.Status == "active" && !catalog.Occurrences.Any(o => o.WoundId == identity.WoundId && o.Wound.Owner == owner))
                throw new InvalidDataException("Spiritual settlement lost an active original wound.");
            expected.Add(identity.WoundId);
        }
        if (!expected.SetEquals(stages.Select(s => s.Before.WoundId)))
            throw new InvalidDataException("Spiritual settlement omitted an independently eligible original wound cohort.");
        var primary = history.Transitions.Single(t => t.TransitionId == stages[0].TransitionId);
        // Select from independent cohort/event/turn first. Filtering by an already
        // matching SourceFingerprint would let altered omitted rows evade this check.
        var actual = history.Transitions.Where(t => expected.Contains(t.WoundId) && t.Turn == turn && t.EventRef == eventRef &&
            (diagnosis is null || t.TransitionId != diagnosis.DiagnosisStage.TransitionId)).ToArray();
        if (actual.Length != stages.Count || !actual.Select(t => t.TransitionId).ToHashSet(StringComparer.Ordinal)
                .SetEquals(stages.Select(s => s.TransitionId)) || actual.Any(t => t.SourceFingerprint != primary.SourceFingerprint))
            throw new InvalidDataException("Spiritual settlement stages do not biject with the independently witnessed cycle rows.");
    }

}
