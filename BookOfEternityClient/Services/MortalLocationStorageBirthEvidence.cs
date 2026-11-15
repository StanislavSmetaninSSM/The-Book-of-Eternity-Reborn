using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

/// <summary>Immutable raw DATA and a slot allocated by the first location metadata plan.</summary>
internal sealed class MortalLocationStorageBirthDraft
{
    private readonly JsonObject _storage;
    internal MortalLocationStorageBirthDraft(string locationId, string storageId, string transitionId,
        string operationRef, JsonObject rawStorage, int sourceTurn)
    { LocationId = locationId; StorageId = storageId; TransitionId = transitionId; OperationRef = operationRef;
      _storage = rawStorage.DeepClone().AsObject(); SourceTurn = sourceTurn; }
    internal string LocationId { get; }
    internal string StorageId { get; }
    internal string TransitionId { get; }
    internal string OperationRef { get; }
    internal int SourceTurn { get; }
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
    private readonly string _preparedIndex;
    private readonly string _completedIndex;
    internal MortalLocationStorageBirthCompletion(MortalLocationAcceptedTurnPlan prepared, MortalLocationAcceptedTurnPlan completed,
        MortalStorageBirthSourceBinding source, string phaseFingerprint)
    { Plan = completed; Source = source; PhaseFingerprint = phaseFingerprint;
      _preparedIndex = SpiritualWoundStateJson.Hash(prepared.FinalIdentityIndex, "storage_birth_prepared_index");
      _completedIndex = SpiritualWoundStateJson.Hash(completed.FinalIdentityIndex, "storage_birth_completed_index"); }
    internal MortalLocationAcceptedTurnPlan Plan { get; }
    internal MortalStorageBirthSourceBinding Source { get; }
    internal string PhaseFingerprint { get; }
    /// <summary>Checks completion DATA before the existing writer; never grants a write lease.</summary>
    internal IReadOnlyList<ValidationIssue> ValidateForPublication(MortalLocationAcceptedTurnPlan prepared, MortalStorageBirthSourceBinding source)
    {
        if (source != Source || _preparedIndex != SpiritualWoundStateJson.Hash(prepared.FinalIdentityIndex, "storage_birth_prepared_index") ||
            _completedIndex != SpiritualWoundStateJson.Hash(Plan.FinalIdentityIndex, "storage_birth_completed_index"))
            return new[] { MortalLocationStorageBirthEvidence.Issue(MortalLocationIdentityState.StatePath,
                "mortal_location_storage_birth_completion_changed", "matching source and unchanged prepared/completed history") };
        return MortalLocationStorageBirthEvidence.ValidateExpectedCoverage(prepared, Plan.FinalIdentityIndex, Source);
    }
    /// <summary>Confirms the actual existing writer's closing read contains precisely the completed index.</summary>
    internal IReadOnlyList<ValidationIssue> ValidateReadback(JsonObject? index) => index is null ||
        _completedIndex != SpiritualWoundStateJson.Hash(index, "storage_birth_completed_index")
            ? new[] { MortalLocationStorageBirthEvidence.Issue(MortalLocationIdentityState.StatePath,
                "mortal_location_storage_birth_readback_changed", "exact complete published history") }
            : MortalLocationStorageBirthEvidence.ValidateExpectedCoverage(Plan, index, Source);
}

/// <summary>Pure first-birth completion. Reuses the held plan/catalog and allocates no identities or permissions.</summary>
internal static partial class MortalLocationStorageBirthEvidence
{
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
            if (itemPhase.ItemPhaseAfterImages.GetValueOrDefault(StorageTransportMoveService.CurrentLocationPath) is not JsonObject current ||
                Text(current["locationId"]) != draft.LocationId || current["locationStorages"] is not JsonArray storages ||
                storages.OfType<JsonObject>().Count(s => Text(s["storageId"]) == draft.StorageId) != 1)
                return Invalid("the same permanent current-scene storage in admitted item projection");
            var projected = storages.OfType<JsonObject>().Single(s => Text(s["storageId"]) == draft.StorageId);
            if (!MortalItemMaterializationContract.ImmutableEvidenceEquals(metadata, Metadata(projected)))
                return Invalid("unchanged birth limits and metadata in admitted item projection");
            var capacity = MortalCargoBirthCapacity.ValidateStorage(draft.LocationId, draft.StorageId, raw, catalog,
                creationIds, source.Turn, draft.OperationRef);
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
        var completed = prepared.WithFinalIdentityIndex(index);
        return new(new(prepared, completed, source, itemPhase.Fingerprint), Array.Empty<ValidationIssue>());

        MortalStorageBirthFinalizationResult Invalid(string expected) => new(null,
            new[] { Issue(MortalLocationIdentityState.StatePath, "mortal_location_storage_birth_finalization_invalid", expected) });
    }

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
            else issues.AddRange(ValidateHistorical(slot!, draft.LocationId, draft.StorageId, draft.OperationRef));
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
