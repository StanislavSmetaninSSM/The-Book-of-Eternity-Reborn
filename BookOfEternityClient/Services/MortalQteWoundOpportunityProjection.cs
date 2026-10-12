using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

/// <summary>Projects safe comparison data from the official new ordinary-turn snapshot, without writing authority.</summary>
internal static class MortalQteWoundOpportunityProjection
{
    private static readonly string[] Paths =
    [
        MortalWoundOccurrenceState.StatePath, MortalWoundOpportunityReceiptState.StatePath,
        WoundIdentityState.StatePath, WoundHistoryState.HistoryPath,
        WoundCarrierCatalog.PlayerPath, WoundCarrierCatalog.NpcPath,
        WoundCarrierCatalog.EnemiesPath, WoundCarrierCatalog.AlliesPath,
        WoundCarrierCatalog.AfterlifeProfilesPath
    ];

    internal static JsonArray ReadCurrent(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease) =>
        Project(ReadSnapshot(files, lease));

    internal static PendingTurnSnapshotReadAuthority ReadSnapshot(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease)
    {
        var read = PendingTurnSnapshotReader.ReadCurrent(files, lease,
            PendingTurnSnapshotPathSelection.CreateWithObservedOptionalPaths(
                ["game_state/meta/soul_state.json"], Paths));
        if (!read.Success || read.Snapshot is null) throw Failure(read.Issues);
        return read.Snapshot;
    }

    internal static string? ReadObservedRoot(PendingTurnSnapshotReadAuthority snapshot, string path)
    {
        if (snapshot.CoveredLogicalPaths.Contains(path, StringComparer.Ordinal))
            return new UTF8Encoding(false, true).GetString(snapshot.ReadRequiredBytes(path)).TrimStart('\uFEFF');
        if (snapshot.AbsentLogicalPaths.Contains(path, StringComparer.Ordinal)) return null;
        throw new InvalidDataException("mortal_qte_projection_uncovered_root: " + path);
    }

    internal static JsonArray Project(PendingTurnSnapshotReadAuthority snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.Realm != "mortal_world") return new JsonArray();
        string? Read(string path) => ReadObservedRoot(snapshot, path);
        JsonObject? Object(string path)
        {
            var json = Read(path);
            if (json is null) return null; // Only the official snapshot's observed absence.
            return StrictJsonAuthority.Deserialize<JsonObject>(json, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed,
                "Mortal QTE opportunity signed carrier") ??
                throw new InvalidDataException("mortal_qte_projection_authority_invalid: signed_present_carrier_null@" + path);
        }
        var pending = MortalWoundOccurrenceState.Parse(Read(MortalWoundOccurrenceState.StatePath) ??
            "{\"schemaVersion\":1,\"occurrences\":[]}", MortalWoundOccurrenceState.StatePath);
        var receipts = MortalWoundOpportunityReceiptState.Parse(Read(MortalWoundOpportunityReceiptState.StatePath) ??
            "{\"schemaVersion\":1,\"nextOrdinal\":1,\"receipts\":[]}", MortalWoundOpportunityReceiptState.StatePath);
        var identity = WoundIdentityState.Parse(Read(WoundIdentityState.StatePath) ??
            "{\"schemaVersion\":1,\"entries\":[]}", WoundIdentityState.StatePath);
        var history = WoundHistoryState.Parse(Read(WoundHistoryState.HistoryPath) ??
            "{\"schemaVersion\":1,\"nextOrdinal\":1,\"transitions\":[]}", WoundHistoryState.HistoryPath);
        var carriers = WoundCarrierCatalog.Build(new(Object(WoundCarrierCatalog.PlayerPath), Object(WoundCarrierCatalog.NpcPath),
            Object(WoundCarrierCatalog.EnemiesPath), Object(WoundCarrierCatalog.AlliesPath), Object(WoundCarrierCatalog.AfterlifeProfilesPath)));
        var issues = pending.Issues.Concat(receipts.Issues).Concat(identity.Issues).Concat(history.Issues).Concat(carriers.Issues).ToList();
        if (pending.State is null || receipts.State is null || identity.State is null || history.State is null || issues.Count != 0)
            throw Failure(issues);
        issues.AddRange(history.State.ValidateAgreement(identity.State, carriers));
        issues.AddRange(MortalWoundOpportunityReceiptState.ValidateConsumedOccurrenceAgreement(pending.State, receipts.State));
        issues.AddRange(MortalWoundOpportunityReceiptState.ValidateHistoryAgreement(receipts.State, history.State));
        if (issues.Count != 0) throw Failure(issues);

        // Validate every complete source batch/vector before selecting the single public row.
        var rebound = new Dictionary<string, WoundAcceptedEventAuthorityCompositionResult>(StringComparer.Ordinal);
        foreach (var occurrence in pending.State.Occurrences)
        {
            var events = WoundAcceptedEventAuthorityComposer.RebindMortalOccurrence(occurrence,
                pending.State.Occurrences.Where(row => row.ProducerOperationKey == occurrence.ProducerOperationKey).ToArray(),
                receipts.State.Receipts.Where(row => row.ProducerOperationKey == occurrence.ProducerOperationKey).ToArray(),
                snapshot.SessionId, snapshot.RequestId, snapshot.SnapshotToken, snapshot.TurnNumber);
            if (!events.Success) throw Failure(events.Issues);
            rebound.Add(occurrence.OccurrenceId, events);
        }
        var selected = pending.State.Occurrences.FirstOrDefault(row => row.AdapterKind == "qte" && row.Domain == "physical" &&
            row.ProfileKey == "mortal_qte_terminal_health_damage_v1" && row.Owner.Realm == "mortal_world" &&
            row.Owner.OwnerKind == "player" && row.Owner.OwnerId == "player_current" && row.Owner.CarrierPath == WoundCarrierCatalog.PlayerPath);
        if (selected is null) return new JsonArray();
        if (selected.MinimumSeverityRank is not null || selected.GuaranteedTrigger is not null || selected.WorseningTarget is not null)
            throw new InvalidDataException("mortal_qte_projection_source_invalid: ordinary QTE source cannot declare minimum, guarantee or worsening.");
        var eventsForSelected = rebound[selected.OccurrenceId];
        var binding = new WoundAcceptedTurnBinding(snapshot.SessionId, snapshot.RequestId, snapshot.SnapshotToken,
            snapshot.Realm, snapshot.TurnNumber, eventsForSelected.Events, eventsForSelected.EventsFingerprint);
        var evidence = eventsForSelected.Events[selected.AcceptedEventOrdinal];
        var composed = WoundOpportunityAuthority.Compose(new(binding, selected.OccurrenceId, selected.OpportunityRef, evidence.EventRef,
            selected.Owner with { }, selected.Domain, selected.ProfileKey, selected.Source.Kind, selected.Source.SourceId, selected.Source.State,
            new(selected.AdapterKind, evidence.Kind, evidence.AuthorityId, selected.Outcome.Kind,
                selected.Outcome.MaximumSeverityRank, selected.Outcome.ReadableCause), selected.HardMaximumSeverityRank,
            null, WoundOpportunityAuthority.CloneSafeContext(selected.SafeContext)));
        if (!composed.Success || composed.Opportunity is null) throw Failure(composed.Issues);
        var authority = composed.Opportunity;
        var severity = authority.MaximumSeverityRank switch
        { 1 => "I", 2 => "II", 3 => "III", 4 => "IV", _ => throw new InvalidDataException("mortal_qte_projection_maximum_invalid") };
        return new JsonArray(new JsonObject
        {
            ["opportunityRef"] = authority.PublicRef, ["domain"] = authority.Domain, ["maximumSeverity"] = severity,
            ["safeContext"] = new JsonObject
            {
                ["target"] = authority.SafeContext.Target, ["cause"] = authority.SafeContext.Cause,
                ["allowedLocationKinds"] = new JsonArray(authority.SafeContext.AllowedLocationKinds.Select(kind => (JsonNode)JsonValue.Create(kind)!).ToArray())
            }
        });
    }

    private static InvalidDataException Failure(IEnumerable<ValidationIssue> issues) =>
        new("mortal_qte_projection_authority_invalid: " + string.Join("; ", issues.Select(issue => issue.Code + "@" + issue.FilePath)));
}
