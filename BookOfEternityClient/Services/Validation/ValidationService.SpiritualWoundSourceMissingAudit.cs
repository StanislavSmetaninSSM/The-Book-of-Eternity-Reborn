using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    // Explicit retained live path only. The ordinary Begin/fixed reducer stays strict
    // and cannot acquire a partial source session or publish a known-side-only cost.
    /// <summary>
    /// Replaces the current source session with a retained live session supporting missing-side audit completion.
    /// </summary>
    /// <param name="lease">
    /// Active canonical write lease used to invalidate the prior session and acquire signed inputs.
    /// </param>
    /// <param name="awaitOriginalPrefix">
    /// Defers source preparation until exact original-prefix binding when <see langword="true"/>; defaults to immediate preparation.
    /// </param>
    /// <param name="projectionClock">
    /// Shared source projection clock, or <see langword="null"/> for ordinary time reads.
    /// </param>
    /// <param name="currentInputs">
    /// Retained current source images from the named original intake, or <see langword="null"/>
    /// to read current files from the physical session.
    /// </param>
    /// <returns>
    /// The newly retained source session or acquisition issues; outside afterlife, an empty result without issues.
    /// </returns>
    internal async Task<SpiritualWoundSourcePreparation> BeginLiveSpiritualWoundSourceSessionAsync(
        FileSystemManager.CanonicalWriteLease lease, bool awaitOriginalPrefix = false,
        AcceptedTurnProjectionClock? projectionClock = null,
        SpiritualOriginalDraftInputs? currentInputs = null)
    {
        _fs.EnsureCanonicalWriteLeaseActive(lease);
        InvalidateSpiritualWoundSourceSession();
        var result = await SpiritualWoundSourceSession.AcquireAsync(this, lease,
            allowMissingActionCostAudit: true, awaitOriginalPrefix: awaitOriginalPrefix,
            projectionClock: projectionClock, currentInputs: currentInputs);
        if (result.Session is not null)
            _spiritualWoundSourceSession = result.Session;
        return result;
    }

    internal sealed partial class SpiritualWoundSourceSession
    {
        // Immutable retained raw evidence. This is neither a source nor an admission seal.
        private sealed record MissingActionCostAuditLeaf(
            string ConflictId, string ExchangeId, string Coordinate, string ExchangeJson,
            IReadOnlyList<string> MissingSides);

        private MissingActionCostAuditLeaf? _missingActionCostAudit;
        private bool _allowMissingActionCostAudit;

        /// <summary>
        /// Gets whether this source, its capture clock and any bound original prefix remain current.
        /// </summary>
        internal bool IsCurrentOwner =>
            !_revoked && ReferenceEquals(_validator._spiritualWoundSourceSession, this) &&
            (_projectionClock is not SpiritualWoundProjectionClock clock || clock.IsHealthy) &&
            (_resourcePrefix == null || _prefixCapture!.OwnsOriginalPrefix(this, _resourcePrefix));

        // Detached comparison data from this owner's capture, not caller-defined authority.
        internal JsonObject? GetMissingActionCostAuditCandidate()
        {
            if (!IsCurrentOwner)
                throw new InvalidOperationException("current source owner required");
            return _missingActionCostAudit is null ? null :
                JsonNode.Parse(_missingActionCostAudit.ExchangeJson)!.AsObject();
        }

        // The common owner must retain and verify actual source/effect/resource owners.
        // These batches prove only resource-local construction from our captured images.
        internal AfterlifeSpiritualConflictResourceOutcome.BuildResult BuildResourceBatches(
            ResourceOwnerAuthority owners, ResourceStateLedger state)
        {
            if (!IsCurrentOwner)
                throw new InvalidOperationException("current source owner required");
            if (_awaitingOriginalPrefix || _resourcePrefix != null && !ReferenceEquals(state, _resourcePrefix.State))
                throw new InvalidOperationException("the exact accepted original resource prefix is required");
            return AfterlifeSpiritualConflictResourceOutcome.TryCreate(TurnNumber,
                _originalConflict.DeepClone().AsObject(),
                _candidateConflict.DeepClone().AsObject(), owners, state, terminal: TerminalPreparation);
        }

        private static string[] ReadTrulyMissingAuditSides(JsonObject exchange)
        {
            JsonObject? audit = null;
            if (exchange.ContainsKey("actionCostAudit"))
            {
                audit = exchange["actionCostAudit"] as JsonObject ??
                    throw new InvalidOperationException("actionCostAudit must be an object; null is not missing evidence");
                if (audit.Any(pair => pair.Key is not ("player" or "opposition") ||
                                      pair.Value is not JsonObject))
                    throw new InvalidOperationException("actionCostAudit has malformed or unknown side evidence");
            }
            var result = new List<string>();
            if (ExchangeExpectsPlayerActionCostAudit(exchange) && (audit is null || !audit.ContainsKey("player")))
                result.Add("player");
            if (ExchangeExpectsOppositionActionCostAudit(exchange) && (audit is null || !audit.ContainsKey("opposition")))
                result.Add("opposition");
            return result.ToArray();
        }

        private static bool IsExpectedMissingAuditIssue(ValidationIssue issue,
            string coordinate, IReadOnlyList<string> missingSides) =>
            missingSides.Any(side => issue.FilePath == coordinate + ".actionCostAudit." + side &&
                issue.Code == (side == "player" ? "afterlife_conflict_action_cost_audit_missing" :
                    "afterlife_conflict_opposition_action_cost_audit_missing"));

        private void RetainMissingActionCostAudit(JsonObject conflict, JsonObject exchange,
            string coordinate, string[] missingSides)
        {
            if (_missingActionCostAudit is not null)
                throw new InvalidOperationException("only one unresolved active audit leaf can be retained");
            _missingActionCostAudit = new(ExactString(conflict["conflictId"])!,
                ExactString(exchange["exchangeId"])!, coordinate, exchange.ToJsonString(),
                Array.AsReadOnly(missingSides.ToArray()));
            _pending.Add(new(SpiritualSourceRequirement.MissingActionCostAudit,
                coordinate, exchange.ToJsonString()));
        }

        private static void ValidateIncompleteActiveProjection(JsonObject active,
            JsonObject exchange, SourceFrontier frontier, List<ValidationIssue> issues)
        {
            foreach (var field in new[] { "playerSideStrain", "oppositionSideStrain" })
                if (!JsonNode.DeepEquals(exchange["after"]?[field], active[field]))
                    throw new InvalidOperationException("incomplete leaf must retain exact final candidate strain");
            var nextControl = ResolveNextPriorControlState(
                frontier.ControlJson is null ? null : JsonNode.Parse(frontier.ControlJson), exchange);
            ValidateFinalActiveControlStateMatchesExchangeSnapshots(active, nextControl,
                "activeConflict", issues, requiredForCurrentTurn: true);
        }

        private void ContinueMissingActionCostAudit(JsonObject previous, JsonObject active,
            List<ValidationIssue> issues)
        {
            var leaf = _missingActionCostAudit!;
            RequireSameExcept(previous, active, "exchangeLog");
            if (previous["exchangeLog"] is not JsonArray oldLog ||
                active["exchangeLog"] is not JsonArray log || log.Count != oldLog.Count || log.Count == 0)
                throw new InvalidOperationException("missing-side completion cannot append or drop an exchange");
            for (var index = 0; index < log.Count - 1; index++)
                if (!JsonNode.DeepEquals(oldLog[index], log[index]))
                    throw new InvalidOperationException("checked source prefix is immutable during missing-side completion");
            var retained = JsonNode.Parse(leaf.ExchangeJson)!.AsObject();
            if (!JsonNode.DeepEquals(oldLog.Last(), retained) ||
                log.Last() is not JsonObject completed ||
                leaf.Coordinate != $"activeConflict.exchangeLog[{log.Count - 1}]")
                throw new InvalidOperationException("exact owned incomplete active leaf required");
            RequireSameExcept(retained, completed, "actionCostAudit");
            var beforeAudit = retained["actionCostAudit"] as JsonObject ?? new JsonObject();
            if (completed["actionCostAudit"] is not JsonObject afterAudit)
                throw new InvalidOperationException("missing-side completion requires an actual audit object");
            // Every observed side/property remains immutable. Only genuinely absent slots
            // may be filled. Filling both absent slots over separate resumes is supported.
            foreach (var pair in beforeAudit)
                if (!afterAudit.ContainsKey(pair.Key) || !JsonNode.DeepEquals(pair.Value, afterAudit[pair.Key]))
                    throw new InvalidOperationException("known audit side is immutable");
            if (afterAudit.Any(pair => !beforeAudit.ContainsKey(pair.Key) &&
                    !leaf.MissingSides.Contains(pair.Key, StringComparer.Ordinal)))
                throw new InvalidOperationException("completion added evidence outside the owned missing slots");
            var remaining = ReadTrulyMissingAuditSides(completed);
            if (remaining.Length >= leaf.MissingSides.Count)
                throw new InvalidOperationException("completion must fill at least one owned missing side");
            if (_originalConflict["activeConflict"] is not JsonObject original ||
                ExactString(original["conflictId"]) != leaf.ConflictId ||
                ExactString(completed["exchangeId"]) != leaf.ExchangeId)
                throw new InvalidOperationException("original incomplete source identity required");
            IndexMembers(original, active, issues);
            if (issues.Any(issue => issue.Severity == IssueSeverity.Error))
                return;
            var frontier = RestoreSourceFrontier(original);
            var pending = _pending.Single(value => value.Kind == SpiritualSourceRequirement.MissingActionCostAudit &&
                value.Coordinate == leaf.Coordinate && value.CandidateJson == leaf.ExchangeJson);
            _pending.Remove(pending);
            _missingActionCostAudit = null;
            // This is the only full evidence path. No known exchange is replayed, and
            // partial validation never claimed this exchange's dice or prepared its harm.
            CheckExchange(original, completed, JsonNode.Parse(frontier.StateJson)!.AsObject(),
                frontier.ControlJson is null ? null : JsonNode.Parse(frontier.ControlJson),
                leaf.Coordinate, issues, allowMissingAuditLeaf: true);
            if (issues.Any(issue => issue.Severity == IssueSeverity.Error))
                return;
            ValidateIncompleteActiveProjection(active, completed, frontier, issues);
        }
    }
}
