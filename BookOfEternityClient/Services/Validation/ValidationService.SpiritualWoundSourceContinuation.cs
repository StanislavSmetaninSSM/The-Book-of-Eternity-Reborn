using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    private void InvalidateSpiritualWoundSourceSession()
    {
        _spiritualWoundSourceSession?.Revoke();
        _spiritualWoundSourceSession = null;
    }

    internal sealed partial class SpiritualWoundSourceSession
    {
        private volatile bool _revoked;
        private readonly SortedDictionary<int, AfterlifeSpiritualConflictResourceOutcome.ExchangeBatch> _resourceBatches = new();
        private readonly SemaphoreSlim _continuationGate = new(1, 1);
        private byte[]? _originalRequestBytes;
        private readonly Dictionary<string, SourceFrontier> _frontiers = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _checkedByCoordinate = new(StringComparer.Ordinal);

        private sealed record SourceFrontier(
            string ConflictJson, string StateJson, string? ControlJson,
            int? PlayerActionPoints, int? OppositionActionPoints, string? LastExchangeId);

        internal void Revoke() => _revoked = true;

        // Both APIs use the ticket's same signed validation and commit implementation.
        internal Task<SpiritualWoundSourcePreparation> ContinueAsync(
            FileSystemManager.CanonicalWriteLease lease) =>
            PreparedContinuation.ContinueAsync(this, lease);

        internal Task<SpiritualWoundSourceContinuationPreparation> PrepareContinuationAsync(
            FileSystemManager.CanonicalWriteLease lease) =>
            PreparedContinuation.PrepareAsync(this, lease);

        internal SpiritualWoundSourcePreparation CommitPreparedContinuation(
            FileSystemManager.CanonicalWriteLease lease, PreparedContinuation ticket) =>
            ticket.Commit(this, lease);

        internal AfterlifeSpiritualConflictResourceOutcome.BuildResult BuildPreparedResourceBatches(
            FileSystemManager.CanonicalWriteLease lease, PreparedContinuation ticket,
            ResourceOwnerAuthority owners, ResourceStateLedger state) =>
            ticket.BuildResourceBatches(this, lease, owners, state);

        private void RevokeCurrent()
        {
            Revoke();
            if (ReferenceEquals(_validator._spiritualWoundSourceSession, this))
                _validator._spiritualWoundSourceSession = null;
        }

        private static bool SameBytes(byte[]? left, byte[]? right) =>
            left is null ? right is null : right is not null && left.AsSpan().SequenceEqual(right);

        private bool SameOriginal(PendingTurnSnapshotReadAuthority current)
        {
            if (SessionId != current.SessionId || RequestId != current.RequestId ||
                SnapshotToken != current.SnapshotToken || TurnNumber != current.TurnNumber ||
                Realm != current.Realm ||
                !_original.AcceptedD20EventValues.SequenceEqual(current.AcceptedD20EventValues) ||
                !_original.CoveredLogicalPaths.SequenceEqual(current.CoveredLogicalPaths) ||
                !_original.AbsentLogicalPaths.SequenceEqual(current.AbsentLogicalPaths))
                return false;
            return _original.CoveredLogicalPaths.All(path =>
                SameBytes(_original.ReadRequiredBytes(path), current.ReadRequiredBytes(path)));
        }

        /// <summary>
        /// Creates a private continuation workspace retaining accepted source objects and the full observed candidate.
        /// </summary>
        /// <param name="candidate">
        /// Fresh candidate texts, with null values for absent paths.
        /// </param>
        /// <param name="acceptedExchangeLimit">
        /// Capture-owned active exchange limit, or null for the strict legacy continuation.
        /// </param>
        /// <returns>
        /// An uncommitted workspace that cannot independently own source admission.
        /// </returns>
        private SpiritualWoundSourceSession CreateProspective(Dictionary<string, string?> candidate,
            int? acceptedExchangeLimit)
        {
            // This private workspace never becomes the validator's current session and
            // cannot own a source. It does not call Acquire, Prepare or replay old exchanges.
            var next = new SpiritualWoundSourceSession(_validator, _original,
                new Dictionary<string, string?>(_before, StringComparer.Ordinal), candidate, _projectionClock)
            {
                _originalRequestBytes = _originalRequestBytes?.ToArray(),
                _coldOriginalInputs = _coldOriginalInputs,
                _allowMissingActionCostAudit = _allowMissingActionCostAudit,
                _resourcePrefix = _resourcePrefix,
                _prefixCapture = _prefixCapture,
                _acceptedExchangeLimit = acceptedExchangeLimit,
                _mechanicsContext = _mechanicsContext,
                _soul = _soul.DeepClone().AsObject(),
                _profileRoot = _profileRoot.DeepClone().AsObject(),
                _originalConflict = _originalConflict.DeepClone().AsObject(),
                _initialFullCandidateConflict = _initialFullCandidateConflict.DeepClone().AsObject()
            };
            next._candidateConflict = next.ProjectCandidateConflict();
            next._missingActionCostAudit = _missingActionCostAudit;
            next._sources.AddRange(_sources);
            next._pending.AddRange(_pending);
            next._claimedDice.UnionWith(_claimedDice);
            next._checkedExchanges.AddRange(_checkedExchanges);
            next._admittedExchangeMechanics.AddRange(_admittedExchangeMechanics);
            next._profileAliases.UnionWith(_profileAliases);
            next._coordinates.UnionWith(_coordinates);
            foreach (var pair in _profiles)
                next._profiles.Add(pair.Key, pair.Value.DeepClone().AsObject());
            foreach (var pair in _members)
                next._members.Add(pair.Key, pair.Value);
            foreach (var pair in _expectedActionPoints)
                next._expectedActionPoints.Add(pair.Key, pair.Value);
            foreach (var pair in _frontiers)
                next._frontiers.Add(pair.Key, pair.Value);
            foreach (var pair in _checkedByCoordinate)
                next._checkedByCoordinate.Add(pair.Key, pair.Value);
            foreach (var pair in _exchangeMechanics)
                next._exchangeMechanics.Add(pair.Key, pair.Value);
            foreach (var pair in _resourceBatches)
                next._resourceBatches.Add(pair.Key, pair.Value);
            return next;
        }

        /// <summary>
        /// Installs a fully validated prospective frontier while retaining the original admitted object identities and scalar comparisons.
        /// </summary>
        /// <param name="next">
        /// Prospective workspace whose source, resource and mechanics checks already succeeded.
        /// </param>
        private void CommitContinuation(SpiritualWoundSourceSession next)
        {
            // All validation has finished. Existing source objects are retained verbatim.
            _coldOriginalInputs = next._coldOriginalInputs;
            _candidate.Clear();
            foreach (var pair in next._candidate)
                _candidate.Add(pair.Key, pair.Value);
            _candidateConflict = next._candidateConflict;
            _acceptedExchangeLimit = next._acceptedExchangeLimit;
            _mechanicsContext = next._mechanicsContext;
            _resourceBatches.Clear();
            foreach (var pair in next._resourceBatches)
                _resourceBatches.Add(pair.Key, pair.Value);
            _exchangeMechanics.Clear();
            foreach (var pair in next._exchangeMechanics)
                _exchangeMechanics.Add(pair.Key, pair.Value);
            _missingActionCostAudit = next._missingActionCostAudit;
            _sources.Clear();
            _sources.AddRange(next._sources);
            _pending.Clear();
            _pending.AddRange(next._pending);
            _claimedDice.Clear();
            _claimedDice.UnionWith(next._claimedDice);
            _admittedExchangeMechanics.Clear();
            _admittedExchangeMechanics.AddRange(next._admittedExchangeMechanics);
            _checkedExchanges.Clear();
            _checkedExchanges.AddRange(next._checkedExchanges);
            _coordinates.Clear();
            _coordinates.UnionWith(next._coordinates);
            _members.Clear();
            foreach (var pair in next._members)
                _members.Add(pair.Key, pair.Value);
            _expectedActionPoints.Clear();
            foreach (var pair in next._expectedActionPoints)
                _expectedActionPoints.Add(pair.Key, pair.Value);
            _frontiers.Clear();
            foreach (var pair in next._frontiers)
                _frontiers.Add(pair.Key, pair.Value);
            _checkedByCoordinate.Clear();
            foreach (var pair in next._checkedByCoordinate)
                _checkedByCoordinate.Add(pair.Key, pair.Value);
        }

        /// <summary>
        /// Projects the fresh candidate through the capture-owned accepted exchange frontier.
        /// </summary>
        /// <returns>
        /// The candidate conflict image visible to the current source execution step.
        /// </returns>
        private JsonObject ProjectCandidateConflict()
        {
            return ProjectAcceptedExchangeFrontier(ProjectFullCandidateConflict());
        }

        /// <summary>
        /// Projects the complete fresh candidate without removing exchanges that the
        /// original capture has not executed yet.
        /// </summary>
        /// <returns>
        /// The complete candidate conflict image after applying its response wrapper.
        /// </returns>
        private JsonObject ProjectFullCandidateConflict()
        {
            var raw = Parse(AfterlifeSpiritualConflictState.StatePath, original: false, required: false);
            var projected = raw[AfterlifeSpiritualConflictState.ResponseField] is JsonObject update
                ? AfterlifeSpiritualConflictState.ApplyUpdate(_originalConflict, update, _projectionClock)
                : raw.DeepClone().AsObject();
            projected.Remove(AfterlifeSpiritualConflictState.ResponseField);
            if (projected.ContainsKey("lastInvalidUpdate"))
                throw new InvalidOperationException("invalid source conflict update");
            return projected;
        }

        private void CaptureInitialSourceFrontier(JsonObject conflict)
        {
            var id = ExactString(conflict["conflictId"]) ??
                throw new InvalidOperationException("exact source conflict identity");
            _frontiers[id] = new(conflict.ToJsonString(), conflict.ToJsonString(),
                conflict["controlState"]?.ToJsonString(),
                _expectedActionPoints["player"], _expectedActionPoints["opposition"], null);
        }

        private void CaptureCheckedSourceFrontier(JsonObject conflict, JsonObject exchange,
            JsonNode? previousControl)
        {
            var id = ExactString(conflict["conflictId"])!;
            var exchangeId = ExactString(exchange["exchangeId"])!;
            _checkedByCoordinate.Add(id + "/" + exchangeId, exchange.ToJsonString());
            _frontiers[id] = new(conflict.ToJsonString(), exchange["after"]!.ToJsonString(),
                ResolveNextPriorControlState(previousControl, exchange)?.ToJsonString(),
                _expectedActionPoints["player"], _expectedActionPoints["opposition"], exchangeId);
        }

        private SourceFrontier RestoreSourceFrontier(JsonObject originalConflict)
        {
            var id = ExactString(originalConflict["conflictId"])!;
            if (!_frontiers.TryGetValue(id, out var frontier))
            {
                InitializeResourceSequence(originalConflict);
                frontier = _frontiers[id];
            }
            _expectedActionPoints["player"] = frontier.PlayerActionPoints;
            _expectedActionPoints["opposition"] = frontier.OppositionActionPoints;
            return frontier;
        }

        private void EvaluateContinuation(SpiritualWoundSourceSession previous, List<ValidationIssue> issues)
        {
            var oldRoot = previous._candidateConflict;
            RequireSameExcept(oldRoot, _candidateConflict, "activeConflict", "recentConflicts");
            var oldActive = oldRoot["activeConflict"] as JsonObject;
            var active = _candidateConflict["activeConflict"] as JsonObject;
            if (_candidateConflict["activeConflict"] is not null && active is null)
                throw new InvalidOperationException("activeConflict must be an object or null");
            if (_missingActionCostAudit is not null &&
                (active is null || oldActive is null ||
                 ExactString(active["conflictId"]) != ExactString(oldActive["conflictId"])))
                throw new InvalidOperationException("incomplete active audit cannot become terminal or a new conflict");
            if (_missingActionCostAudit is not null)
                RequireSameExcept(oldRoot, _candidateConflict, "activeConflict");
            var oldRecent = ReadRecent(oldRoot);
            var recent = ReadRecent(_candidateConflict);

            // Existing source-local coordinates are stable. A bounded history drop is
            // permitted only for exact old historical rows, never a row tracked this turn.
            var dropped = FindRetainedRecentPrefix(oldRecent, recent);
            for (var oldIndex = 0; oldIndex < dropped; oldIndex++)
                if (HasCurrentResolution(oldRecent[oldIndex]!.AsObject()))
                    throw new InvalidOperationException("cannot truncate this source owner's current terminal evidence");

            var retainedCount = oldRecent.Count - dropped;
            for (var index = 0; index < retainedCount; index++)
            {
                var oldResolution = oldRecent[index + dropped]!.AsObject();
                var resolution = recent[index]!.AsObject();
                var terminalToExecute = TerminalPreparation is { } terminal &&
                    _acceptedExchangeLimit == 1 && _checkedExchanges.Count == 0 &&
                    index == recent.Count - 1 &&
                    ExactString(resolution["terminalExchange"]?["exchangeId"]) == terminal.ExchangeId;
                if (JsonNode.DeepEquals(oldResolution, resolution) &&
                    !terminalToExecute)
                    continue;
                if (JsonNode.DeepEquals(oldResolution, resolution) && terminalToExecute)
                {
                    PrepareContinuedTerminal(_originalConflict["activeConflict"]!.AsObject(), resolution,
                        resolution["terminalExchange"]!.AsObject(), $"recentConflicts[{index}]", issues);
                    if (issues.Any(issue => issue.Severity == IssueSeverity.Error))
                        return;
                    continue;
                }
                ContinueMissingTerminalWitness(oldResolution, resolution, issues);
                if (issues.Any(issue => issue.Severity == IssueSeverity.Error))
                    return;
            }

            var oldId = oldActive is null ? null : ExactString(oldActive["conflictId"]);
            var newId = active is null ? null : ExactString(active["conflictId"]);
            var appended = recent.Skip(retainedCount).Select(value => value!.AsObject()).ToArray();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var resolution in appended)
            {
                var id = ExactString(resolution["conflictId"]) ??
                    throw new InvalidOperationException("exact terminal conflict identity");
                if (!ids.Add(id) || id == newId || oldRecent.OfType<JsonObject>()
                        .Any(value => ExactString(value["conflictId"]) == id))
                    throw new InvalidOperationException("one non-active terminal resolution per conflict");
            }
            if (oldId is not null && oldId != newId && !appended.Any(value =>
                    ExactString(value["conflictId"]) == oldId))
                throw new InvalidOperationException("retained active conflict needs its terminal evidence");

            if (active is not null)
            {
                if (oldId == newId && oldActive is not null)
                    ContinueActive(oldActive, active, issues);
                else
                    AddPendingOnce(SpiritualSourceRequirement.PriorStartDeclaration,
                        "activeConflict", active);
            }
            if (issues.Any(issue => issue.Severity == IssueSeverity.Error))
                return;

            for (var index = 0; index < appended.Length; index++)
            {
                var resolution = appended[index];
                var coordinate = $"recentConflicts[{retainedCount + index}]";
                var id = ExactString(resolution["conflictId"]);
                if (_originalConflict["activeConflict"] is not JsonObject original ||
                    ExactString(original["conflictId"]) != id ||
                    _pending.Any(value => (value.Kind is SpiritualSourceRequirement.PriorStartDeclaration or
                        SpiritualSourceRequirement.PriorEscalationDeclaration) &&
                        ExactString(JsonNode.Parse(value.CandidateJson)?["conflictId"]) == id))
                {
                    AddPendingOnce(SpiritualSourceRequirement.PriorStartDeclaration, coordinate, resolution);
                    continue;
                }
                if (resolution["terminalExchange"] is not JsonObject exchange)
                {
                    AddPendingOnce(SpiritualSourceRequirement.TerminalExchangeAudit, coordinate, resolution);
                    continue;
                }
                PrepareContinuedTerminal(original, resolution, exchange, coordinate, issues);
                if (issues.Any(issue => issue.Severity == IssueSeverity.Error))
                    return;
            }
        }

        private void ContinueActive(JsonObject previous, JsonObject active, List<ValidationIssue> issues)
        {
            if (JsonNode.DeepEquals(previous, active))
                return;
            if (_missingActionCostAudit is not null)
            {
                ContinueMissingActionCostAudit(previous, active, issues);
                return;
            }
            if (_pending.Any(value => value.Coordinate == "activeConflict" &&
                    (value.Kind is SpiritualSourceRequirement.PriorStartDeclaration or
                        SpiritualSourceRequirement.PriorEscalationDeclaration) &&
                    ExactString(JsonNode.Parse(value.CandidateJson)?["conflictId"]) ==
                        ExactString(active["conflictId"])))
                throw new InvalidOperationException("pending active declaration needs its actual owned producer");
            if (previous["exchangeLog"] is not JsonArray oldLog || active["exchangeLog"] is not JsonArray log ||
                log.Count < oldLog.Count)
                throw new InvalidOperationException("complete retained source exchange prefix required");
            for (var index = 0; index < oldLog.Count; index++)
                if (!JsonNode.DeepEquals(oldLog[index], log[index]))
                    throw new InvalidOperationException("known exchange and side evidence cannot change");
            RequireSameExcept(previous, active, "exchangeLog", "dangerMode",
                "playerSideStrain", "oppositionSideStrain", "conflictPosition",
                "controlState", "tempoAdvantage", "status", "resolutionState");
            if (!CheckDanger(previous, active, "activeConflict"))
                return; // Retained unresolved declaration, never a higher-cap source.
            if (log.Count == oldLog.Count)
                throw new InvalidOperationException("active source state changed without a new exchange");
            if (_originalConflict["activeConflict"] is not JsonObject original ||
                ExactString(original["conflictId"]) != ExactString(active["conflictId"]))
                throw new InvalidOperationException("current active source lacks its signed original");
            IndexMembers(original, active, issues);
            if (issues.Any(issue => issue.Severity == IssueSeverity.Error))
                return;
            var frontier = RestoreSourceFrontier(original);
            for (var index = oldLog.Count; index < log.Count; index++)
            {
                var exchange = log[index] as JsonObject ??
                    throw new InvalidOperationException("source exchange must be an object");
                CheckExchange(original, exchange, JsonNode.Parse(frontier.StateJson)!.AsObject(),
                    frontier.ControlJson is null ? null : JsonNode.Parse(frontier.ControlJson),
                    $"activeConflict.exchangeLog[{index}]", issues,
                    allowMissingAuditLeaf: index == log.Count - 1);
                if (issues.Any(issue => issue.Severity == IssueSeverity.Error))
                    return;
                if (_missingActionCostAudit is not null)
                {
                    ValidateIncompleteActiveProjection(active, exchange, frontier, issues);
                    return;
                }
                frontier = _frontiers[ExactString(original["conflictId"])!];
            }
            var final = JsonNode.Parse(frontier.StateJson)!.AsObject();
            foreach (var key in new[] { "playerSideStrain", "oppositionSideStrain" })
                if (!JsonNode.DeepEquals(final[key], active[key]))
                    throw new InvalidOperationException("final strain differs from appended checked exchange");
            ValidateFinalActiveControlStateMatchesExchangeSnapshots(active,
                frontier.ControlJson is null ? null : JsonNode.Parse(frontier.ControlJson),
                "activeConflict", issues, requiredForCurrentTurn: true);
        }

        private void ContinueMissingTerminalWitness(JsonObject previous, JsonObject resolution,
            List<ValidationIssue> issues)
        {
            RequireSameExcept(previous, resolution, "terminalExchange");
            var requirement = _pending.SingleOrDefault(value =>
                value.Kind == SpiritualSourceRequirement.TerminalExchangeAudit &&
                JsonNode.DeepEquals(JsonNode.Parse(value.CandidateJson), previous));
            if (previous["terminalExchange"] is not null || requirement is null ||
                resolution["terminalExchange"] is not JsonObject exchange)
                throw new InvalidOperationException("terminal change needs an exact unresolved audit slot");
            if (_originalConflict["activeConflict"] is not JsonObject original ||
                ExactString(original["conflictId"]) != ExactString(resolution["conflictId"]))
                throw new InvalidOperationException("terminal source lacks its signed original conflict");
            _pending.Remove(requirement);
            PrepareContinuedTerminal(original, resolution, exchange, requirement.Coordinate, issues);
        }

        private void PrepareContinuedTerminal(JsonObject original, JsonObject resolution,
            JsonObject exchange, string coordinate, List<ValidationIssue> issues)
        {
            if (!CheckDanger(original, resolution, coordinate))
                return;
            IndexMembers(original, original, issues);
            if (issues.Any(issue => issue.Severity == IssueSeverity.Error))
                return;
            var frontier = RestoreSourceFrontier(original);
            var id = ExactString(exchange["exchangeId"]) ??
                throw new InvalidOperationException("exact terminal exchange identity");
            var key = ExactString(original["conflictId"]) + "/" + id;
            if (_checkedByCoordinate.TryGetValue(key, out var checkedJson))
            {
                if (frontier.LastExchangeId != id ||
                    !JsonNode.DeepEquals(JsonNode.Parse(checkedJson), exchange))
                    throw new InvalidOperationException("terminal witness rewrites or reuses an earlier checked exchange");
                // The exact last checked current exchange can become terminal evidence.
                // Its existing sources, coordinates and dice claims are not created again.
            }
            else
            {
                var before = JsonNode.Parse(frontier.StateJson)!.AsObject();
                if (new[] { "playerSideStrain", "oppositionSideStrain" }.Any(field =>
                        !JsonNode.DeepEquals(before[field], exchange["before"]?[field])))
                {
                    AddPendingOnce(SpiritualSourceRequirement.TerminalExchangePrefix, coordinate, resolution);
                    return;
                }
                CheckExchange(original, exchange, before,
                    frontier.ControlJson is null ? null : JsonNode.Parse(frontier.ControlJson),
                    coordinate + ".terminalExchange", issues);
                if (issues.Any(issue => issue.Severity == IssueSeverity.Error))
                    return;
            }
            if (exchange["diceAudit"] is JsonObject dice &&
                !JsonNode.DeepEquals(dice, resolution["diceAudit"]))
                throw new InvalidOperationException("terminal proof must retain the same terminal exchange dice");
            AddPendingOnce(SpiritualSourceRequirement.TerminalClosure, coordinate, resolution);
        }

        private bool HasCurrentResolution(JsonObject resolution) =>
            _pending.Any(value => JsonNode.DeepEquals(JsonNode.Parse(value.CandidateJson), resolution)) ||
            _checkedByCoordinate.Keys.Any(key => key.StartsWith(
                (ExactString(resolution["conflictId"]) ?? "") + "/", StringComparison.Ordinal));

        private static JsonArray ReadRecent(JsonObject root)
        {
            if (!root.ContainsKey("recentConflicts"))
                return new JsonArray();
            if (root["recentConflicts"] is not JsonArray array || array.Any(value => value is not JsonObject))
                throw new InvalidOperationException("recentConflicts must contain objects");
            return array;
        }

        private static int FindRetainedRecentPrefix(JsonArray oldRows, JsonArray rows)
        {
            // Identity aligns only the slot to compare below, never authorizes changed data.
            for (var dropped = 0; dropped <= oldRows.Count; dropped++)
            {
                var retained = oldRows.Count - dropped;
                if (rows.Count < retained || (dropped > 0 && rows.Count != 20))
                    continue;
                var additions = rows.Count - retained;
                if (dropped > 0 && dropped != Math.Max(0, oldRows.Count + additions - 20))
                    continue;
                if (Enumerable.Range(0, retained).All(index =>
                        ExactString(oldRows[index + dropped]?["conflictId"]) ==
                        ExactString(rows[index]?["conflictId"])))
                    return dropped;
            }
            throw new InvalidOperationException("retained terminal evidence must remain an ordered prefix");
        }

        private void AddPendingOnce(SpiritualSourceRequirement kind, string coordinate, JsonObject candidate)
        {
            var existing = _pending.SingleOrDefault(value => value.Kind == kind && value.Coordinate == coordinate &&
                ExactString(JsonNode.Parse(value.CandidateJson)?["conflictId"]) ==
                    ExactString(candidate["conflictId"]));
            if (existing is not null)
            {
                if (!JsonNode.DeepEquals(JsonNode.Parse(existing.CandidateJson), candidate))
                    throw new InvalidOperationException("known pending source evidence cannot be replaced");
                return;
            }
            _pending.Add(new(kind, coordinate, candidate.ToJsonString()));
        }

        private static void RequireSameExcept(JsonObject previous, JsonObject candidate, params string[] omitted)
        {
            var before = previous.DeepClone().AsObject();
            var after = candidate.DeepClone().AsObject();
            foreach (var field in omitted)
            {
                before.Remove(field);
                after.Remove(field);
            }
            if (!JsonNode.DeepEquals(before, after))
                throw new InvalidOperationException("retained source context cannot change");
        }
    }
}
