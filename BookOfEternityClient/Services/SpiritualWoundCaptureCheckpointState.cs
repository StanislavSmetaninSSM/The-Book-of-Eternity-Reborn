using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using static BookOfEternityClient.Services.SpiritualWoundStateJson;

namespace BookOfEternityClient.Services;

/// <summary>
/// Checks private original-capture checkpoint shape without granting origin or publication authority.
/// Original exchange/source and allocation bounds require later replay by real owners.
/// </summary>
internal sealed class SpiritualWoundCaptureCheckpointState
{
    internal const string StatePath = "game_state/control/spiritual_wound_capture_checkpoint.json";

    private static readonly string[] PhysicalWitnessPaths =
    [
        PendingTurnSnapshotAuthority.AuthorityPath,
        LiveTurnPreparationService.PendingTurnSnapshotManifestPath,
        LiveTurnPreparationService.TurnRequestPath
    ];

    private readonly JsonObject _root;
    private readonly HashSet<string> _registeredDraftPaths;

    private SpiritualWoundCaptureCheckpointState(JsonObject root, IEnumerable<string> registeredDraftPaths)
    {
        _root = root.DeepClone().AsObject();
        _registeredDraftPaths = registeredDraftPaths.ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>
    /// Parses closed checkpoint comparison evidence without authenticating its origin or owner bounds.
    /// </summary>
    /// <param name="json">
    /// Serialized checkpoint root; missing or malformed text fails closed.
    /// </param>
    /// <param name="path">
    /// Owning path used for diagnostics.
    /// </param>
    /// <param name="registeredDraftPaths">
    /// Trusted original draft inventory; declaration alone grants no origin authority.
    /// </param>
    /// <returns>
    /// Detached shape result or diagnostics without partial state.
    /// </returns>
    internal static SpiritualWoundCaptureCheckpointParseResult Parse(
        string? json, string path, IEnumerable<string> registeredDraftPaths)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(registeredDraftPaths);
        var paths = registeredDraftPaths.ToArray();
        try
        {
            var root = SpiritualWoundStateJson.Parse(json);
            Closed(root, "schemaVersion checkpoint");
            Integer(root["schemaVersion"], 1, 1);
            if (root["checkpoint"] is not null)
                ValidateCheckpoint(Object(root["checkpoint"]), paths);
            return new(true, new(root, paths), Array.Empty<ValidationIssue>());
        }
        catch (Exception error) when (error is JsonException or FormatException or InvalidOperationException or OverflowException)
        {
            return new(false, null, Array.AsReadOnly(new[]
            {
                new ValidationIssue(path, IssueSeverity.Error, "Invalid spiritual capture checkpoint.",
                    code: "spiritual_wound_checkpoint_invalid_state", section: "AcceptedTurnWoundMaterialization")
            }));
        }
    }

    /// <summary>
    /// Serializes a detached closed checkpoint without changing retained input bytes.
    /// </summary>
    /// <param name="state">
    /// Previously validated checkpoint state.
    /// </param>
    /// <returns>
    /// Deterministic wrapper JSON for comparison or later owner-controlled persistence.
    /// </returns>
    internal static string SerializeCanonical(SpiritualWoundCaptureCheckpointState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return Canonical(state._root);
    }

    /// <summary>
    /// Rebuilds exact detached original draft images for later signed owner reconstruction.
    /// The returned view alone does not authenticate the checkpoint or authorize replay.
    /// </summary>
    /// <returns>
    /// A fresh immutable original draft view with distinct absent and present-empty images.
    /// </returns>
    internal SpiritualOriginalDraftInputs ReadOriginalDraftInputs()
    {
        if (_root["checkpoint"] is not JsonObject checkpoint)
            throw new InvalidOperationException("An empty checkpoint has no retained original draft.");
        var images = new Dictionary<string, CanonicalBeforeImage>(StringComparer.Ordinal);
        foreach (var row in Rows(checkpoint["originalDraftImages"]))
        {
            var path = Text(row["path"]);
            var existed = row["existed"]!.GetValue<bool>();
            images.Add(path, new CanonicalBeforeImage(
                existed, existed ? DecodeBase64(row["contentBase64"]) : null));
        }
        return SpiritualOriginalDraftInputs.Create(
            Text(checkpoint["sessionId"]), Text(checkpoint["requestId"]),
            Text(checkpoint["snapshotToken"]), (int)Integer(checkpoint["turn"], 1),
            _registeredDraftPaths.ToArray(), images);
    }

    /// <summary>
    /// Reads comparison fingerprints for the three immutable physical origin witnesses.
    /// The caller must read and verify the real files under the active lease.
    /// </summary>
    /// <returns>
    /// A detached read-only map without mutable transport controls or witness bytes.
    /// </returns>
    internal IReadOnlyDictionary<string, string> ReadOriginalPhysicalWitnessFingerprints()
    {
        if (_root["checkpoint"] is not JsonObject checkpoint)
            throw new InvalidOperationException("An empty checkpoint has no original witnesses.");
        var witnesses = Rows(checkpoint["physicalWitnesses"])
            .ToDictionary(row => Text(row["path"]),
                row => Fingerprint(row["contentFingerprint"]), StringComparer.Ordinal);
        return new ReadOnlyDictionary<string, string>(witnesses);
    }

    /// <summary>
    /// Exports retained allocation rows for strict replay by the real original owners.
    /// This detached JSON does not itself establish an allocation bound.
    /// </summary>
    /// <returns>
    /// A fresh serialized copy of the checkpoint's complete allocation journal.
    /// </returns>
    internal string ReadAllocationJournalJson()
    {
        if (_root["checkpoint"] is not JsonObject checkpoint)
            throw new InvalidOperationException("An empty checkpoint has no allocation journal.");
        var allocations = checkpoint["allocations"]!.DeepClone().AsArray();
        if (checkpoint["pendingSubmission"] is JsonObject submission)
            foreach (var row in submission["allocations"]!.AsArray())
                allocations.Add(row!.DeepClone());
        return Canonical(allocations);
    }

    /// <summary>
    /// Gets whether an exact selected decision awaits dependent continuation.
    /// </summary>
    internal bool HasPendingSubmission => _root["checkpoint"] is JsonObject checkpoint &&
        checkpoint["pendingSubmission"] is JsonObject;

    /// <summary>
    /// Reads the detached selected-decision comparison evidence, if present.
    /// </summary>
    /// <returns>
    /// A fresh submission object, or <see langword="null"/> when no choice is retained.
    /// </returns>
    internal JsonObject? ReadPendingSubmission() =>
        _root["checkpoint"]?["pendingSubmission"]?.DeepClone().AsObject();

    /// <summary>
    /// Reconstructs the canonical private checkpoint preceding a retained dependent progress row.
    /// </summary>
    /// <param name="count">
    /// Number of accepted rows to retain, from zero through the current journal length.
    /// </param>
    /// <returns>
    /// A detached checkpoint prefix with an absent journal at zero; invalid bounds throw.
    /// </returns>
    internal SpiritualWoundCaptureCheckpointState ReadDependentProgressPrefix(int count)
    {
        var root = _root.DeepClone().AsObject();
        var body = root["checkpoint"]!.AsObject();
        var submission = body["pendingSubmission"]!.AsObject();
        var rows = submission["dependentDraftProgress"] as JsonArray ?? new JsonArray();
        if (count < 0 || count > rows.Count) throw new ArgumentOutOfRangeException(nameof(count));
        if (count == 0) submission.Remove("dependentDraftProgress");
        else submission["dependentDraftProgress"] = new JsonArray(rows.Take(count).Select(row => row!.DeepClone()).ToArray());
        body["checkpointFingerprint"] = Hash(body, "spiritual_capture_checkpoint_v1", "checkpointFingerprint");
        return new(root, _registeredDraftPaths);
    }

    /// <summary>
    /// Compares an exact submission staging without advancing any committed prefix.
    /// </summary>
    /// <param name="before">
    /// Previously parsed checkpoint with its immutable committed prefix.
    /// </param>
    /// <param name="candidate">
    /// Parsed checkpoint containing the proposed selected decision.
    /// </param>
    /// <returns>
    /// Staged or exact replay for a preserved prefix; conflict for replacement or advancement.
    /// </returns>
    internal static SpiritualWoundCaptureCheckpointAdvanceResult PlanPendingSubmission(
        SpiritualWoundCaptureCheckpointState before, SpiritualWoundCaptureCheckpointState candidate)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(candidate);
        if (!before._registeredDraftPaths.SetEquals(candidate._registeredDraftPaths) ||
            !before.HasCheckpoint || !candidate.HasPendingSubmission)
            return new("conflict", null);
        if (JsonNode.DeepEquals(before._root, candidate._root))
            return new("exact_replay", new(candidate._root, candidate._registeredDraftPaths));
        if (before.HasPendingSubmission)
            return new("conflict", null);
        var prior = before._root.DeepClone();
        var next = candidate._root.DeepClone();
        foreach (var node in new[] { prior, next })
        {
            node["checkpoint"]!.AsObject().Remove("pendingSubmission");
            node["checkpoint"]!.AsObject().Remove("checkpointFingerprint");
        }
        return JsonNode.DeepEquals(prior, next)
            ? new("staged", new(candidate._root, candidate._registeredDraftPaths))
            : new("conflict", null);
    }

    /// <summary>
    /// Gets the number of committed continuation advances for owner-controlled replay.
    /// </summary>
    internal int CommittedAdvance => _root["checkpoint"] is JsonObject checkpoint
        ? (int)Integer(checkpoint["committedAdvance"])
        : throw new InvalidOperationException("An empty checkpoint has no committed advance.");

    /// <summary>
    /// Gets whether this parsed root contains an unfinished original capture.
    /// </summary>
    internal bool HasCheckpoint => _root["checkpoint"] is JsonObject;

    /// <summary>
    /// Gets the number of allocations produced through the original first offer.
    /// </summary>
    internal int InitialAllocationCount => _root["checkpoint"] is JsonObject checkpoint
        ? (int)Integer(checkpoint["initialAllocationCount"])
        : throw new InvalidOperationException("An empty checkpoint has no initial allocation count.");

    /// <summary>
    /// Gets the sealed fingerprint of the first owner-derived C1 packet.
    /// </summary>
    internal string InitialPendingPacketFingerprint => _root["checkpoint"] is JsonObject checkpoint
        ? Fingerprint(checkpoint["initialPendingPacketFingerprint"])
        : throw new InvalidOperationException("An empty checkpoint has no first packet fingerprint.");

    /// <summary>
    /// Gets the realm declared by the structurally parsed original checkpoint.
    /// The caller must compare it with the reopened signed snapshot.
    /// </summary>
    internal string OriginalRealm => _root["checkpoint"] is JsonObject checkpoint
        ? Text(checkpoint["realm"])
        : throw new InvalidOperationException("An empty checkpoint has no original realm.");

    /// <summary>
    /// Compares one detached checkpoint replacement against its immutable original and retained prefix.
    /// A successful comparison does not authorize a file write or game-state advance.
    /// </summary>
    /// <param name="before">
    /// Previously parsed checkpoint root.
    /// </param>
    /// <param name="candidate">
    /// Independently parsed candidate root.
    /// </param>
    /// <returns>
    /// Started, advanced, exact replay or conflict, with no after-state for a conflict.
    /// </returns>
    internal static SpiritualWoundCaptureCheckpointAdvanceResult PlanAdvance(
        SpiritualWoundCaptureCheckpointState before, SpiritualWoundCaptureCheckpointState candidate)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(candidate);
        if (!before._registeredDraftPaths.SetEquals(candidate._registeredDraftPaths))
            return new("conflict", null);
        if (JsonNode.DeepEquals(before._root, candidate._root))
            return new("exact_replay", new(candidate._root, candidate._registeredDraftPaths));
        if (candidate._root["checkpoint"] is not JsonObject next)
            return new("conflict", null);
        if (before._root["checkpoint"] is not JsonObject prior)
            return Integer(next["committedAdvance"]) == 0
                ? new("started", new(candidate._root, candidate._registeredDraftPaths))
                : new("conflict", null);
        foreach (var field in new[] { "sessionId", "requestId", "snapshotToken", "turn", "realm",
            "originalSnapshotFingerprint", "originalDraftImages", "physicalWitnesses",
            "initialAllocationCount", "initialPendingPacketFingerprint" })
            if (!JsonNode.DeepEquals(prior[field], next[field])) return new("conflict", null);
        var priorAdvance = (int)Integer(prior["committedAdvance"]);
        if (Integer(next["committedAdvance"]) != priorAdvance + 1)
            return new("conflict", null);
        if (next.ContainsKey("pendingSubmission"))
            return new("conflict", null);
        foreach (var field in new[] { "advances", "allocations" })
        {
            var oldRows = prior[field]!.AsArray();
            var newRows = next[field]!.AsArray();
            if (oldRows.Count > newRows.Count ||
                oldRows.Where((row, index) => !JsonNode.DeepEquals(row, newRows[index])).Any())
                return new("conflict", null);
        }
        if (prior["pendingSubmission"] is JsonObject submission)
        {
            var committedCount = prior["allocations"]!.AsArray().Count;
            var suffix = submission["allocations"]!.AsArray();
            var allocations = next["allocations"]!.AsArray();
            var advance = next["advances"]![priorAdvance]!;
            if (allocations.Count < committedCount + suffix.Count || suffix.Where((row, index) =>
                    !JsonNode.DeepEquals(row, allocations[committedCount + index])).Any() ||
                advance["newDecisionFingerprints"]!.AsArray().Count != 1 ||
                !JsonNode.DeepEquals(advance["newDecisionFingerprints"]![0],
                    submission["stagedDecision"]!["decisionFingerprint"]))
                return new("conflict", null);
            var commandChanges = advance["inputChanges"]!.AsArray().Where(row =>
                Text(row!["path"]) == AcceptedMechanicsPlan.WoundCommandPath).ToArray();
            if (submission["command"] is JsonObject command
                ? commandChanges.Length != 1 ||
                    !JsonNode.DeepEquals(commandChanges[0]!["contentBase64"], command["contentBase64"])
                : commandChanges.Length != 0)
                return new("conflict", null);
        }
        return new("advanced", new(candidate._root, candidate._registeredDraftPaths));
    }

    /// <summary>
    /// Validates one nonempty closed checkpoint against its trusted draft inventory.
    /// </summary>
    /// <param name="checkpoint">
    /// Detached duplicate-checked checkpoint object.
    /// </param>
    /// <param name="registeredDraftPaths">
    /// Complete owner-supplied original draft inventory.
    /// </param>
    private static void ValidateCheckpoint(JsonObject checkpoint, IReadOnlyList<string> registeredDraftPaths)
    {
        Closed(checkpoint, "sessionId requestId snapshotToken turn realm originalSnapshotFingerprint originalDraftImages physicalWitnesses initialAllocationCount initialPendingPacketFingerprint advances committedAdvance allocations expectedPendingPacketFingerprint checkpointFingerprint" +
            (checkpoint.ContainsKey("pendingSubmission") ? " pendingSubmission" : ""));
        Text(checkpoint["sessionId"]);
        Text(checkpoint["requestId"]);
        var token = Text(checkpoint["snapshotToken"]);
        Require(token.Length == 64 && token.All(static c => c is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F'),
            "Validated snapshot hash required.");
        Integer(checkpoint["turn"], 1);
        Require(Text(checkpoint["realm"]) is "chaos_sea" or "shining_abode", "Unknown spiritual realm.");
        Require(Fingerprint(checkpoint["originalSnapshotFingerprint"]) == "sha256:" + token.ToLowerInvariant(),
            "Original snapshot fingerprint differs from its validated token.");

        var registered = new HashSet<string>(StringComparer.Ordinal);
        var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in registeredDraftPaths)
            Require(SpiritualOriginalDraftInputs.IsDraftPath(path) && registered.Add(path) && aliases.Add(path),
                "Invalid trusted draft inventory.");
        var draftRows = Rows(checkpoint["originalDraftImages"]);
        Require(draftRows.Length == registered.Count, "Complete original draft inventory required.");
        ValidateImages(draftRows, registered, requireComplete: true);

        var witnesses = Rows(checkpoint["physicalWitnesses"]);
        Require(witnesses.Length == PhysicalWitnessPaths.Length, "Exact immutable witness inventory required.");
        for (var index = 0; index < witnesses.Length; index++)
        {
            var witness = witnesses[index];
            Closed(witness, "path existed contentFingerprint");
            Require(Text(witness["path"]) == PhysicalWitnessPaths[index], "Immutable witness path differs.");
            Require(witness["existed"] is JsonValue value && value.TryGetValue<bool>(out var existed) && existed,
                "Immutable original witness must exist.");
            Fingerprint(witness["contentFingerprint"]);
        }

        var allocations = checkpoint["allocations"] as JsonArray ?? throw new FormatException("Allocation array required.");
        SpiritualWoundReplayJournal.CreateReplay(allocations.ToJsonString());
        var initialCount = Integer(checkpoint["initialAllocationCount"], 0, allocations.Count);
        var previousCount = initialCount;
        var previousPacketFingerprint = Fingerprint(checkpoint["initialPendingPacketFingerprint"]);
        var advances = Rows(checkpoint["advances"]);
        Integer(checkpoint["committedAdvance"], advances.Length, advances.Length);
        var seenDecisions = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < advances.Length; index++)
        {
            var advance = advances[index];
            Closed(advance, "ordinal priorPendingPacketFingerprint inputChanges newDecisionFingerprints allocationCount resultPendingPacketFingerprint");
            Integer(advance["ordinal"], index + 1, index + 1);
            Require(Fingerprint(advance["priorPendingPacketFingerprint"]) == previousPacketFingerprint,
                "Advance prior packet fingerprint differs.");
            ValidateImages(Rows(advance["inputChanges"]), registered, requireComplete: false);
            var decisions = advance["newDecisionFingerprints"] as JsonArray ??
                throw new FormatException("Decision fingerprint array required.");
            Require(decisions.Count > 0, "An advance requires a staged decision suffix.");
            foreach (var decision in decisions)
                Require(seenDecisions.Add(Fingerprint(decision)), "Decision fingerprint was already staged.");
            previousCount = Integer(advance["allocationCount"], previousCount, allocations.Count);
            previousPacketFingerprint = Fingerprint(advance["resultPendingPacketFingerprint"]);
        }
        Require(previousCount == allocations.Count, "Final allocation count differs from journal length.");
        Require(Fingerprint(checkpoint["expectedPendingPacketFingerprint"]) == previousPacketFingerprint,
            "Expected packet fingerprint differs from last committed step.");
        if (checkpoint.ContainsKey("pendingSubmission"))
            ValidatePendingSubmission(Object(checkpoint["pendingSubmission"]), allocations,
                advances.Length, previousPacketFingerprint);
        Require(Fingerprint(checkpoint["checkpointFingerprint"]) ==
            Hash(checkpoint, "spiritual_capture_checkpoint_v1", "checkpointFingerprint"),
            "Checkpoint digest differs.");
    }

    /// <summary>
    /// Validates closed selection evidence while leaving source admission to original owner replay.
    /// </summary>
    /// <param name="submission">
    /// Selected decision and its exact command and allocation suffix.
    /// </param>
    /// <param name="committedAllocations">
    /// Unchanged committed journal prefix.
    /// </param>
    /// <param name="committedAdvance">
    /// Exact current committed advancement number.
    /// </param>
    /// <param name="pendingFingerprint">
    /// Exact current committed pending packet fingerprint.
    /// </param>
    private static void ValidatePendingSubmission(JsonObject submission, JsonArray committedAllocations,
        int committedAdvance, string pendingFingerprint)
    {
        Closed(submission, "priorCommittedAdvance priorPendingPacketFingerprint stagedDecision command allocations" +
            (submission.ContainsKey("dependentDraftProgress") ? " dependentDraftProgress" : ""));
        if (submission.ContainsKey("dependentDraftProgress"))
        {
            var progress = Rows(submission["dependentDraftProgress"]);
            Require(progress.Length > 0, "An empty progress journal must be absent.");
            var paths = new HashSet<string>(StringComparer.Ordinal)
                { AfterlifeSpiritualConflictState.StatePath, "output/narrative_response.json" };
            for (var index = 0; index < progress.Length; index++)
            {
                var row = progress[index];
                Closed(row, "ordinal acceptedContinuationId dependentDraftFields inputChanges");
                Integer(row["ordinal"], index + 1, index + 1);
                Text(row["acceptedContinuationId"]);
                var fields = Rows(row["dependentDraftFields"]);
                Require(fields.Length > 0, "A progress row must identify a nonempty frontier.");
                string? previous = null;
                foreach (var field in fields)
                {
                    Closed(field, "path jsonPointer");
                    Require(Text(field["path"]) == AfterlifeSpiritualConflictState.StatePath,
                        "Progress permission has an invalid path.");
                    var pointer = Text(field["jsonPointer"]);
                    Require(pointer.StartsWith('/') && (previous is null || string.CompareOrdinal(previous, pointer) < 0),
                        "Progress permissions must be unique and sorted.");
                    previous = pointer;
                }
                var images = Rows(row["inputChanges"]);
                ValidateImages(images, paths, requireComplete: false);
                Require(images.Any(image => Text(image["path"]) == AfterlifeSpiritualConflictState.StatePath) &&
                    images.All(image => image["existed"]!.GetValue<bool>()),
                    "Progress requires an actual conflict correction without deletions.");
            }
        }
        Integer(submission["priorCommittedAdvance"], committedAdvance, committedAdvance);
        Require(Fingerprint(submission["priorPendingPacketFingerprint"]) == pendingFingerprint,
            "Submission prior packet differs.");
        var decision = Object(submission["stagedDecision"]);
        var kind = Text(decision["decision"]);
        Require(kind is "none" or "materialize" or "guarantee_satisfied", "Unknown selected decision.");
        Closed(decision, "opportunityRef sourceId decisionFingerprint sourceOrdinal waveOrdinal decision selectedSeverityRank woundDraftBase64 woundDraftFingerprint" +
            (kind == "guarantee_satisfied" ? " satisfiedWoundId satisfiedSeverityRank" : ""));
        Text(decision["opportunityRef"]);
        Text(decision["sourceId"]);
        Integer(decision["sourceOrdinal"], 0);
        Integer(decision["waveOrdinal"], 0);
        Require(Fingerprint(decision["decisionFingerprint"]) ==
            Hash(decision, "staged_decision", "decisionFingerprint"), "Selected decision digest differs.");
        if (kind == "materialize")
        {
            Integer(decision["selectedSeverityRank"], 1, 4);
            ValidateSubmissionBytes(decision["woundDraftBase64"], decision["woundDraftFingerprint"]);
        }
        else
            Require(decision["selectedSeverityRank"] is null && decision["woundDraftBase64"] is null &&
                decision["woundDraftFingerprint"] is null, "A no-transition choice contains a wound draft.");
        if (kind == "guarantee_satisfied")
        {
            Require(submission["command"] is null, "Satisfied guarantee cannot carry a command.");
            Text(decision["satisfiedWoundId"]);
            Integer(decision["satisfiedSeverityRank"], 1, 4);
        }
        else
        {
            var command = Object(submission["command"]);
            Closed(command, "contentBase64 contentFingerprint");
            ValidateSubmissionBytes(command["contentBase64"], command["contentFingerprint"]);
        }
        var combined = committedAllocations.DeepClone().AsArray();
        foreach (var row in Rows(submission["allocations"]))
            combined.Add(row.DeepClone());
        SpiritualWoundReplayJournal.CreateReplay(combined.ToJsonString());
    }

    /// <summary>
    /// Checks exact selected-command or proposal bytes and their digest.
    /// </summary>
    /// <param name="content">
    /// Base64-encoded JSON object bytes.
    /// </param>
    /// <param name="fingerprint">
    /// Raw SHA-256 digest of the exact bytes.
    /// </param>
    private static void ValidateSubmissionBytes(JsonNode? content, JsonNode? fingerprint)
    {
        var bytes = DecodeJsonObjectBytes(content);
        Require(Fingerprint(fingerprint) == "sha256:" + Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant(),
            "Selected payload digest differs.");
    }

    /// <summary>
    /// Validates ordinally sorted exact draft images without treating their bytes as replay authority.
    /// </summary>
    /// <param name="rows">
    /// Closed original or continuation image rows.
    /// </param>
    /// <param name="registeredDraftPaths">
    /// Trusted original draft inventory.
    /// </param>
    /// <param name="requireComplete">
    /// Whether every registered path must appear exactly once.
    /// </param>
    private static void ValidateImages(JsonObject[] rows, IReadOnlySet<string> registeredDraftPaths,
        bool requireComplete)
    {
        string? previous = null;
        foreach (var image in rows)
        {
            Closed(image, "path existed contentBase64 contentFingerprint");
            var path = Text(image["path"]);
            Require(SpiritualOriginalDraftInputs.IsDraftPath(path) && registeredDraftPaths.Contains(path),
                "Image path is not registered draft input.");
            Require(previous is null || string.CompareOrdinal(previous, path) < 0,
                "Image paths must be unique and ordinally sorted.");
            previous = path;
            if (image["existed"] is not JsonValue value || !value.TryGetValue<bool>(out var existed))
                throw new FormatException("Exact image existence flag required.");
            var bytes = existed ? DecodeBase64(image["contentBase64"]) : null;
            if (!existed) Require(image["contentBase64"] is null, "Absent image cannot contain bytes.");
            Require(Fingerprint(image["contentFingerprint"]) == new CanonicalBeforeImage(existed, bytes).Fingerprint,
                "Image byte fingerprint differs.");
        }
        if (requireComplete)
            Require(rows.Length == registeredDraftPaths.Count, "Complete original draft inventory required.");
    }

    /// <summary>
    /// Reads a required array of object rows without coercion.
    /// </summary>
    /// <param name="node">
    /// Required JSON array.
    /// </param>
    /// <returns>
    /// Detached object-row references from the parsed tree.
    /// </returns>
    private static JsonObject[] Rows(JsonNode? node) =>
        node is JsonArray rows
            ? rows.Select(Object).ToArray()
            : throw new FormatException("Object-row array required.");

    /// <summary>
    /// Requires a JSON object for a closed checkpoint component.
    /// </summary>
    /// <param name="node">
    /// Required object node.
    /// </param>
    /// <returns>
    /// The object, or a format failure for another node kind.
    /// </returns>
    private static JsonObject Object(JsonNode? node) =>
        node as JsonObject ?? throw new FormatException("Object required.");
}

/// <summary>
/// Reports detached checkpoint shape validation without granting original-turn authority or owner bounds.
/// </summary>
/// <param name="IsValid">
/// <see langword="true"/> if shape validation succeeded; otherwise, <see langword="false"/>.
/// </param>
/// <param name="State">
/// Parsed state, or <see langword="null"/> on failure.
/// </param>
/// <param name="Issues">
/// Diagnostics, empty on success.
/// </param>
internal sealed record SpiritualWoundCaptureCheckpointParseResult(
    bool IsValid, SpiritualWoundCaptureCheckpointState? State, IReadOnlyList<ValidationIssue> Issues);

/// <summary>
/// Reports a pure checkpoint prefix comparison without committing either state.
/// </summary>
/// <param name="Disposition">
/// Started, advanced, exact_replay or conflict.
/// </param>
/// <param name="After">
/// Detached candidate, or <see langword="null"/> for conflict.
/// </param>
internal sealed record SpiritualWoundCaptureCheckpointAdvanceResult(
    string Disposition, SpiritualWoundCaptureCheckpointState? After);
