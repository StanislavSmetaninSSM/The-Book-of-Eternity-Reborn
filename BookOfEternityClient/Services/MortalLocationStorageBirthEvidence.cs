using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

/// <summary>Immutable raw DATA and a slot allocated by the first location metadata plan.</summary>
internal sealed class MortalLocationStorageBirthDraft
{
    private readonly JsonObject _storage;
    internal MortalLocationStorageBirthDraft(string locationId, string storageId, string transitionId,
        string operationRef, JsonObject rawStorage, int sourceTurn,
        string? rawStoragePath = null, string? itemSourceAuthorityId = null)
    { LocationId = locationId; StorageId = storageId; TransitionId = transitionId; OperationRef = operationRef;
      _storage = rawStorage.DeepClone().AsObject();
      if (!_storage.ContainsKey("contents")) _storage["contents"] = new JsonArray();
      SourceTurn = sourceTurn; RawStoragePath = rawStoragePath ?? operationRef;
      ItemSourceAuthorityId = itemSourceAuthorityId ?? locationId + ":" + storageId; }
    internal string LocationId { get; }
    internal string StorageId { get; }
    internal string TransitionId { get; }
    internal string OperationRef { get; }
    internal int SourceTurn { get; }
    /// <summary>Exact GM-authored storage path for diagnostics, never an occurrence fingerprint path.</summary>
    internal string RawStoragePath { get; }
    /// <summary>Exact item source coordinate derived from the validated location command, never catalog presence.</summary>
    internal string ItemSourceAuthorityId { get; }
    /// <summary>Returns detached input; callers cannot mutate a cached plan's declaration.</summary>
    internal JsonObject CloneRawStorage() => _storage.DeepClone().AsObject();
}

/// <summary>Validated request identity DATA, not a physical lease or publication authority.</summary>
internal sealed record MortalStorageBirthSourceBinding(string SessionId, string RequestId, string SnapshotToken,
    int Turn, string ItemProjectionProof);

internal sealed record MortalStorageBirthFinalizationResult(MortalLocationStorageBirthCompletion? Completion,
    IReadOnlyList<ValidationIssue> Issues)
{ internal bool IsValid => Completion is not null && Issues.Count == 0; }

/// <summary>Detached completion tied to exact prepared slots and one item phase. Still requires live publication fences.</summary>
internal sealed class MortalLocationStorageBirthCompletion
{
    private readonly MortalLocationAcceptedTurnPlan _plan;
    private readonly string _preparedIndex;
    private readonly string _completedIndex;
    internal MortalLocationStorageBirthCompletion(MortalLocationAcceptedTurnPlan prepared, JsonObject completedIndex,
        MortalStorageBirthSourceBinding source, string phaseFingerprint)
    { _plan = prepared.WithFinalIdentityIndex(completedIndex); Source = source; PhaseFingerprint = phaseFingerprint;
      _preparedIndex = SpiritualWoundStateJson.Hash(prepared.FinalIdentityIndex, "storage_birth_prepared_index");
      _completedIndex = SpiritualWoundStateJson.Hash(_plan.FinalIdentityIndex, "storage_birth_completed_index"); }
    internal MortalLocationAcceptedTurnPlan Plan => _plan.WithFinalIdentityIndex(_plan.FinalIdentityIndex);
    internal MortalStorageBirthSourceBinding Source { get; }
    internal string PhaseFingerprint { get; }
    /// <summary>Checks completion DATA before the existing writer; never grants a write lease.</summary>
    internal IReadOnlyList<ValidationIssue> ValidateForPublication(MortalLocationAcceptedTurnPlan prepared, MortalStorageBirthSourceBinding source)
    {
        if (source != Source || _preparedIndex != SpiritualWoundStateJson.Hash(prepared.FinalIdentityIndex, "storage_birth_prepared_index") ||
            _completedIndex != SpiritualWoundStateJson.Hash(_plan.FinalIdentityIndex, "storage_birth_completed_index"))
            return new[] { MortalLocationStorageBirthEvidence.Issue(MortalLocationIdentityState.StatePath,
                "mortal_location_storage_birth_completion_changed", "matching source and unchanged prepared/completed history") };
        return MortalLocationStorageBirthEvidence.ValidateExpectedCoverage(prepared, _plan.FinalIdentityIndex, Source);
    }
    /// <summary>Confirms the actual existing writer's closing read contains precisely the completed index.</summary>
    internal IReadOnlyList<ValidationIssue> ValidateReadback(JsonObject? index) => index is null ||
        _completedIndex != SpiritualWoundStateJson.Hash(index, "storage_birth_completed_index")
            ? new[] { MortalLocationStorageBirthEvidence.Issue(MortalLocationIdentityState.StatePath,
                "mortal_location_storage_birth_readback_changed", "exact complete published history") }
            : MortalLocationStorageBirthEvidence.ValidateExpectedCoverage(_plan, index, Source);
}

/// <summary>Pure first-birth completion. Reuses the held plan/catalog and allocates no identities or permissions.</summary>
internal static partial class MortalLocationStorageBirthEvidence
{
    /// <summary>Recognizes only frozen same-turn current-scene declarations, including empty storage births.</summary>
    internal static bool HasCurrentSceneBirths(MortalItemAcceptedTurnNormalizationSnapshot snapshot)
    {
        var root = snapshot.CloneCurrentProjectionRoot(StorageTransportMoveService.CurrentLocationPath);
        return root is JsonObject location && location["materialization"] is JsonObject birth &&
            Text(birth["route"]) == "current_scene_creation" && Integer(birth["sourceTurn"], out var turn) &&
            turn == snapshot.Turn && location["locationStorages"] is JsonArray { Count: > 0 };
    }

    /// <summary>Finalizes only detached index slots; current/map/item roots and cached plan remain unchanged.</summary>
    internal static MortalStorageBirthFinalizationResult Finalize(MortalLocationAcceptedTurnPlan prepared,
        MortalItemCanonicalProjectionResult itemPhase, MortalItemCarrierCatalog catalog,
        IReadOnlyDictionary<string, string> creationIds, MortalStorageBirthSourceBinding source)
    {
        var issues = new List<ValidationIssue>();
        if (!itemPhase.IsValid || !Exact(source.SessionId) || !Exact(source.RequestId) || !Exact(source.SnapshotToken) ||
            !Exact(source.ItemProjectionProof) || source.Turn < 1)
            return Invalid("matching admitted item phase and validated source identity");
        ValidationService.ValidateCanonicalContainerPaths(catalog, issues);
        if (catalog.Issues.Count != 0 || issues.Count != 0) return Invalid("one admitted exact container graph");
        var index = prepared.FinalIdentityIndex.DeepClone().AsObject();
        var current = itemPhase.ItemPhaseAfterImages.GetValueOrDefault(StorageTransportMoveService.CurrentLocationPath) as JsonObject;
        var offscreen = prepared.StorageBirthDrafts.Any(d => d.LocationId != Text(current?["locationId"]))
            ? MortalLocationStorageContentsState.Parse(
                itemPhase.ItemPhaseAfterImages.GetValueOrDefault(MortalLocationStorageContentsState.StatePath) as JsonObject)
            : null;
        if (offscreen?.Issues.Count > 0) return Invalid("one valid projected offscreen contents root");
        var metadataByCoordinate = new Dictionary<MortalLocationStorageKey, JsonObject>();
        if (prepared.FinalWorldMap["locations"] is not JsonArray locations) return Invalid("the same accepted map");
        foreach (var location in locations.OfType<JsonObject>())
            if (Text(location["locationId"]) is { } locationId && location["locationStorages"] is JsonArray storages)
                foreach (var storage in storages.OfType<JsonObject>())
                    if (Text(storage["storageId"]) is not { } storageId || !metadataByCoordinate.TryAdd(new(locationId, storageId), storage))
                        return Invalid("unique exact prepared storage metadata coordinates");
        var seen = new HashSet<(string, string)>();
        foreach (var draft in prepared.StorageBirthDrafts)
        {
            if (draft.SourceTurn != source.Turn || !seen.Add((draft.LocationId, draft.StorageId)))
                return Invalid("unique prepared current birth slots at the source turn");
            var transition = FindSlot(index, draft);
            if (transition is null || transition["afterState"] is not JsonObject after || after.ContainsKey("cargoBirthEvidence"))
                return Invalid("unmodified prepared child birth slot");
            var raw = draft.CloneRawStorage();
            var metadata = Metadata(raw);
            if (!MortalItemMaterializationContract.ImmutableEvidenceEquals(metadata, after))
                return Invalid("exact originally planned storage metadata");
            if (!metadataByCoordinate.TryGetValue(new(draft.LocationId, draft.StorageId), out var mapStorage) ||
                !MortalItemMaterializationContract.ImmutableEvidenceEquals(metadata, Metadata(mapStorage)))
                return Invalid("unchanged exact birth metadata in the same prepared accepted map");
            if (Text(current?["locationId"]) == draft.LocationId)
            {
                var matches = current?["locationStorages"] is JsonArray storages
                    ? storages.OfType<JsonObject>().Where(s => Text(s["storageId"]) == draft.StorageId).ToArray()
                    : Array.Empty<JsonObject>();
                if (matches.Length != 1 || !MortalItemMaterializationContract.ImmutableEvidenceEquals(metadata, Metadata(matches[0])))
                    return Invalid("the exact current projected storage and its unchanged metadata");
            }
            else
            {
                if (offscreen is null || !offscreen.Entries.ContainsKey(new(draft.LocationId, draft.StorageId)))
                    return Invalid("the exact noncurrent projected storage contents coordinate");
            }
            var capacity = MortalCargoBirthCapacity.ValidateStorage(draft.LocationId, draft.StorageId, raw, catalog,
                creationIds, source.Turn, draft.RawStoragePath);
            issues.AddRange(capacity.Issues);
            if (!capacity.IsValid) return new(null, issues.ToArray());
            var evidence = capacity.Evidence!;
            evidence["sourceAuthority"] = new JsonObject { ["sessionId"] = source.SessionId,
                ["requestId"] = source.RequestId, ["snapshotToken"] = source.SnapshotToken };
            after["cargoBirthEvidence"] = evidence;
        }
        issues.AddRange(MortalLocationIdentityState.Parse(index).Issues);
        issues.AddRange(ValidateExpectedCoverage(prepared, index, source));
        if (issues.Count != 0) return new(null, issues.ToArray());
        return new(new(prepared, index, source, itemPhase.Fingerprint), Array.Empty<ValidationIssue>());

        MortalStorageBirthFinalizationResult Invalid(string expected) => new(null,
            new[] { Issue(MortalLocationIdentityState.StatePath, "mortal_location_storage_birth_finalization_invalid", expected) });
    }

    /// <summary>Checks contextual expected slots/source. Payload integrity is validated once by the canonical parser;
    /// immutable completion hashes retain that result through writer/readback without repeating historical graph reconstruction.</summary>
    internal static IReadOnlyList<ValidationIssue> ValidateExpectedCoverage(MortalLocationAcceptedTurnPlan prepared,
        JsonObject index, MortalStorageBirthSourceBinding source)
    {
        var issues = new List<ValidationIssue>();
        foreach (var draft in prepared.StorageBirthDrafts)
        {
            var slot = FindSlot(index, draft);
            if (slot?["afterState"] is not JsonObject after || after["cargoBirthEvidence"] is not JsonObject evidence ||
                evidence["sourceAuthority"] is not JsonObject authority ||
                Text(authority["sessionId"]) != source.SessionId || Text(authority["requestId"]) != source.RequestId ||
                Text(authority["snapshotToken"]) != source.SnapshotToken)
                issues.Add(Issue(draft.OperationRef, "mortal_location_storage_birth_completion_missing", "every exact expected birth finalized"));
        }
        return issues;
    }

    internal static JsonObject Metadata(JsonObject raw)
    { var metadata = raw.DeepClone().AsObject(); metadata.Remove("contents"); metadata.Remove("cargoMeasurements");
      metadata.Remove("cargoBirthEvidence"); return metadata; }

    private static JsonObject? FindSlot(JsonObject index, MortalLocationStorageBirthDraft draft)
    {
        if (index["locationEntries"] is not JsonArray entries) return null;
        var matches = entries.OfType<JsonObject>().Where(e => Text(e["locationId"]) == draft.LocationId)
            .SelectMany(e => e["transitions"] is JsonArray transitions ? transitions.OfType<JsonObject>() : Array.Empty<JsonObject>())
            .Where(t => Text(t["transitionId"]) == draft.TransitionId && Text(t["kind"]) == "storage_addition" &&
                Text(t["childId"]) == draft.StorageId && Text(t["operationRef"]) == draft.OperationRef &&
                Integer(t["turn"], out var turn) && turn == draft.SourceTurn).ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }

    private static bool Exact(string? text) => ResourceMaterializationContract.IsExactIdentifier(text);
    internal static string? Text(JsonNode? node) => node is JsonValue v && v.TryGetValue<string>(out var text) ? text : null;
    internal static bool Integer(JsonNode? node, out int value) { value = 0; return node is JsonValue v && v.TryGetValue(out value); }
    internal static ValidationIssue Issue(string path, string code, string expected) => new(path, IssueSeverity.Error,
        "Storage birth evidence is missing or inconsistent.", code: code, section: "mortal_location_materialization",
        expected: expected, actual: "invalid birth evidence", repairHint: "Preserve client-owned history; repair only the indexed raw birth declaration.");
}
