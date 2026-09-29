using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    private readonly AsyncLocal<SpiritualOriginalTurnCapture.SpiritualCompletedConflictValidation?>
        _completedSpiritualConflictValidation = new();

    /// <summary>
    /// Supplies detached published exchange comparisons to this logical validation operation.
    /// This scope grants no execution or publication capability.
    /// </summary>
    /// <param name="completion">
    /// Genuine completed publication evidence, or <see langword="null"/> for ordinary validation.
    /// </param>
    /// <returns>
    /// A scope restoring the previous comparison context when disposed.
    /// </returns>
    internal IDisposable UseCompletedSpiritualConflictValidationScope(
        SpiritualOriginalTurnCapture.SpiritualCompletedConflictValidation? completion)
    {
        var previous = _completedSpiritualConflictValidation.Value;
        _completedSpiritualConflictValidation.Value = completion;
        return new CompletedSpiritualConflictScope(this, previous);
    }

    /// <summary>
    /// Restores a nested logical validation context without retaining execution owners.
    /// </summary>
    private sealed class CompletedSpiritualConflictScope : IDisposable
    {
        private readonly ValidationService _owner;
        private readonly SpiritualOriginalTurnCapture.SpiritualCompletedConflictValidation? _previous;
        private bool _disposed;

        /// <summary>
        /// Records the context that preceded this scope.
        /// </summary>
        /// <param name="owner">
        /// Validator whose logical context is restored.
        /// </param>
        /// <param name="previous">
        /// Previous context, including <see langword="null"/> for ordinary validation.
        /// </param>
        internal CompletedSpiritualConflictScope(ValidationService owner,
            SpiritualOriginalTurnCapture.SpiritualCompletedConflictValidation? previous)
        {
            _owner = owner;
            _previous = previous;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed) return;
            _owner._completedSpiritualConflictValidation.Value = _previous;
            _disposed = true;
        }
    }

    /// <summary>
    /// Retains scalar mechanics from one successfully admitted exchange while its execution owner is current.
    /// </summary>
    /// <param name="ConflictId">
    /// Exact original conflict identity.
    /// </param>
    /// <param name="Ordinal">
    /// Zero-based accepted exchange ordinal.
    /// </param>
    /// <param name="ExchangeJson">
    /// Complete admitted exchange, including operations and action actors.
    /// </param>
    /// <param name="PlayerSideJson">
    /// Admitted player membership used to resolve acting identity.
    /// </param>
    /// <param name="OppositionSideJson">
    /// Admitted opposition membership used to resolve acting identity.
    /// </param>
    /// <param name="PlayerBurden">
    /// Actual applicable player cost increment.
    /// </param>
    /// <param name="OppositionBurden">
    /// Actual applicable opposition cost increment.
    /// </param>
    /// <param name="PlayerTempoDenied">
    /// Whether the admitted player's wound prevented tempo gain.
    /// </param>
    /// <param name="EffectivePositionRank">
    /// Admitted causal starting rank; absent only when the exchange has no position snapshot.
    /// </param>
    internal sealed record SpiritualAdmittedExchangeMechanics(string ConflictId, int Ordinal,
        string ExchangeJson, string PlayerSideJson, string OppositionSideJson,
        long PlayerBurden, long OppositionBurden, bool PlayerTempoDenied, int? EffectivePositionRank);

    internal sealed partial class SpiritualWoundSourceSession
    {
        private readonly List<SpiritualAdmittedExchangeMechanics> _admittedExchangeMechanics = [];

        /// <summary>
        /// Returns detached admitted rows only to their actual original capture.
        /// </summary>
        /// <param name="capture">
        /// Capture retaining this source's original resource prefix.
        /// </param>
        /// <returns>
        /// Immutable admitted rows in original execution order; foreign ownership throws.
        /// </returns>
        internal SpiritualAdmittedExchangeMechanics[] ReadAdmittedExchangeMechanics(SpiritualOriginalTurnCapture capture)
        {
            if (!ReferenceEquals(_prefixCapture, capture) || !IsCurrentOwner)
                throw new InvalidOperationException("The original source owner is required for completed comparisons.");
            return _admittedExchangeMechanics.ToArray();
        }

        /// <summary>
        /// Captures comparison-only scalar mechanics before the admitted source frontier advances.
        /// </summary>
        /// <param name="conflict">
        /// Validated original conflict membership.
        /// </param>
        /// <param name="exchange">
        /// Complete exchange that passed source admission.
        /// </param>
        private void RetainAdmittedExchangeMechanics(JsonObject conflict, JsonObject exchange)
        {
            _admittedExchangeMechanics.Add(new(
                ExactString(conflict["conflictId"])!, _checkedExchanges.Count,
                exchange.ToJsonString(), conflict["playerSide"]!.ToJsonString(),
                conflict["oppositionSide"]!.ToJsonString(),
                ReadCurrentActionCostBurden(_mechanicsContext, conflict, exchange, "player"),
                ReadCurrentActionCostBurden(_mechanicsContext, conflict, exchange, "opposition"),
                HasCurrentPlayerTempoBurden(_mechanicsContext, conflict, exchange),
                ReadCurrentEffectivePosition(_mechanicsContext, conflict, exchange)));
        }
    }

    internal sealed partial class SpiritualOriginalTurnCapture
    {
        /// <summary>
        /// Retains immutable causal mechanics for validation of the exact successfully published original turn.
        /// It cannot execute exchanges, allocate identities or authorize another publication.
        /// </summary>
        internal sealed class SpiritualCompletedConflictValidation
        {
            private readonly FileSystemManager _fileSystem;
            private readonly string _sessionId;
            private readonly string _requestId;
            private readonly string _snapshotToken;
            private readonly int _turn;
            private readonly SpiritualAdmittedExchangeMechanics[] _exchanges;
            private readonly string _publishedRootJson;
            private readonly string? _activeConflictJson;
            private bool _published;

            /// <summary>
            /// Seals the admitted intervals against the unique final conflict root and its actual active or completed terminal contour.
            /// </summary>
            /// <param name="issuanceKey">
            /// Private key belonging to the original capture's completed publication handoff.
            /// </param>
            /// <param name="capture">
            /// Genuine completed capture holding its gate during publication handoff.
            /// </param>
            /// <param name="plan">
            /// Completed canonical plan with one conflict producer across owner, effect and wound carrier images.
            /// A terminal root retains the ordinary resolution shape rather than an artificial active exchange log.
            /// </param>
            internal SpiritualCompletedConflictValidation(object issuanceKey, SpiritualOriginalTurnCapture capture,
                AcceptedMechanicsPlan plan)
            {
                if (!ReferenceEquals(issuanceKey, C4IssuanceKey))
                    throw new InvalidOperationException("Only the original capture can seal completed mechanics.");
                _fileSystem = capture._validator._fs;
                _sessionId = capture._input.SessionId;
                _requestId = capture._input.RequestId;
                _snapshotToken = capture._input.SnapshotToken;
                _turn = capture._input.Turn;
                _exchanges = capture._source.ReadAdmittedExchangeMechanics(capture);
                if (_exchanges.Length == 0 || _exchanges.Length != capture._closedExchangeEvidence.Count ||
                    _exchanges.Where((row, index) => row.Ordinal != index ||
                        row.ConflictId != capture._closedExchangeEvidence[index].Interval.ConflictId ||
                        row.ExchangeJson != capture._closedExchangeEvidence[index].ExchangeJson).Any())
                    throw new InvalidOperationException("Completed conflict mechanics must match every closed interval.");
                var roots = new[] { plan.OwnerCompanionAfterImages, plan.EffectCarrierAfterImages,
                        plan.WoundCarrierAfterImages }
                    .SelectMany(images => images.Where(pair => pair.Key == AfterlifeSpiritualConflictState.StatePath))
                    .Select(pair => pair.Value).ToArray();
                if (roots.Length != 1)
                    throw new InvalidOperationException("Completed comparisons require one unique planned conflict image.");
                var root = roots[0];
                _publishedRootJson = root.ToJsonString();
                if (root["activeConflict"] is JsonObject active)
                {
                    if (capture._terminal is not null || active["exchangeLog"] is not JsonArray log ||
                        _exchanges.Any(row => row.ConflictId != AfterlifeSpiritualConflictState.GetNodeString(active["conflictId"]) ||
                            !JsonNode.DeepEquals(JsonNode.Parse(row.PlayerSideJson), active["playerSide"]) ||
                            !JsonNode.DeepEquals(JsonNode.Parse(row.OppositionSideJson), active["oppositionSide"])))
                        throw new InvalidOperationException("Completed comparisons require the exact admitted active roster and log.");
                    var positions = _exchanges.Select(row => log.Select((node, index) => (node, index))
                        .Where(pair => JsonNode.DeepEquals(pair.node, JsonNode.Parse(row.ExchangeJson)))
                        .Select(pair => pair.index).ToArray()).ToArray();
                    if (positions.Any(position => position.Length != 1) ||
                        positions.Select(position => position[0]).Zip(positions.Skip(1).Select(position => position[0]),
                            (first, next) => next == first + 1).Any(adjacent => !adjacent))
                        throw new InvalidOperationException("Published exchange order must match every closed interval.");
                    _activeConflictJson = active.ToJsonString();
                }
                else
                {
                    // PreparedTerminal.CanRetire already requires exactly one closed interval.
                    // Recheck that invariant while sealing detached comparisons, without retaining the owner.
                    if (capture._terminal is not { IsComplete: true } terminal ||
                        !terminal.MatchesCandidate(root) || _exchanges.Length != 1 ||
                        !SpiritualWoundSourceSession.TryReadSingleTerminal(capture._source.ReadOriginalConflict(),
                            root, out var resolution) ||
                        terminal.ExchangeId != capture._closedExchangeEvidence[0].Interval.ExchangeId ||
                        AfterlifeSpiritualConflictState.GetNodeString(resolution!["conflictId"]) != _exchanges[0].ConflictId ||
                        AfterlifeSpiritualConflictState.GetNodeString(resolution["terminalExchange"]?["exchangeId"]) != terminal.ExchangeId ||
                        !JsonNode.DeepEquals(resolution["terminalExchange"], JsonNode.Parse(_exchanges[0].ExchangeJson)))
                        throw new InvalidOperationException("Completed terminal comparisons require the exact finished terminal owner and exchange.");
                }
            }

            /// <summary>
            /// Records successful cache settlement without restoring any execution owner.
            /// </summary>
            /// <param name="issuanceKey">
            /// Original capture's private publication key, supplied by the authenticated cache capability.
            /// </param>
            internal void MarkPublished(object issuanceKey)
            {
                if (!ReferenceEquals(issuanceKey, C4IssuanceKey))
                    throw new InvalidOperationException("Only the publication capability can settle comparisons.");
                _published = true;
            }

            /// <summary>
            /// Verifies successful publication and the same physical filesystem and authenticated original snapshot.
            /// </summary>
            /// <param name="fileSystem">
            /// Filesystem used by the validation pass.
            /// </param>
            /// <param name="sessionId">
            /// Authenticated pending-turn session identity.
            /// </param>
            /// <param name="requestId">
            /// Authenticated original request identity.
            /// </param>
            /// <param name="turn">
            /// Authenticated original turn number.
            /// </param>
            /// <param name="snapshotToken">
            /// Authenticated original manifest payload hash.
            /// </param>
            /// <returns>
            /// <see langword="true"/> only for the published original identity; otherwise <see langword="false"/>.
            /// </returns>
            internal bool Matches(FileSystemManager fileSystem, string sessionId, string requestId, int turn, string snapshotToken) =>
                _published && ReferenceEquals(fileSystem, _fileSystem) &&
                sessionId == _sessionId && requestId == _requestId &&
                turn == _turn && snapshotToken == _snapshotToken;

            /// <summary>
            /// Requires the entire published conflict root before any formula, including retained history and terminal resolution evidence.
            /// </summary>
            /// <param name="root">
            /// Current canonical conflict root; any difference from the unique completed publication is rejected.
            /// </param>
            internal void ValidateRoot(JsonObject root)
            {
                if (!_published || !JsonNode.DeepEquals(JsonNode.Parse(_publishedRootJson), root))
                    throw new InvalidOperationException("The published conflict root no longer matches the completed plan.");
            }

            /// <summary>
            /// Reads one admitted exchange only from the exact planned active conflict, never from a terminal envelope or old same-id history.
            /// </summary>
            /// <param name="conflict">
            /// Complete current active conflict, including its exact roster and full exchange log.
            /// </param>
            /// <param name="exchange">
            /// Full current exchange whose ordinary formula is being checked.
            /// </param>
            /// <returns>
            /// The admitted immutable mechanics; mismatched evidence throws.
            /// </returns>
            internal SpiritualAdmittedExchangeMechanics Read(JsonObject conflict, JsonObject exchange)
            {
                if (!_published || _activeConflictJson is null ||
                    !JsonNode.DeepEquals(JsonNode.Parse(_activeConflictJson), conflict))
                    throw new InvalidOperationException("Published exchange mechanics require the exact planned active conflict.");
                var id = AfterlifeSpiritualConflictState.GetNodeString(conflict["conflictId"]);
                var rows = _exchanges.Where(row => row.ConflictId == id &&
                    JsonNode.DeepEquals(JsonNode.Parse(row.ExchangeJson), exchange)).ToArray();
                if (rows.Length != 1)
                    throw new InvalidOperationException("Published exchange mechanics no longer match the admitted exchange.");
                return rows[0];
            }
        }
    }

    /// <summary>
    /// Reports drift of a published comparison boundary without retrying an unburdened formula.
    /// </summary>
    /// <returns>
    /// The private original-turn boundary validation error.
    /// </returns>
    private static ValidationIssue CompletedSpiritualConflictMismatch() => new(
        AfterlifeSpiritualConflictState.StatePath, IssueSeverity.Error,
        "Published spiritual exchanges no longer match the authenticated original turn.",
        code: "spiritual_completed_conflict_validation_mismatch", section: "AfterlifeSpiritualConflict",
        expected: "exact published original exchange and snapshot", actual: "changed or unavailable comparison evidence");

    /// <summary>
    /// Reads actual live burdens or exact published comparisons while leaving ordinary validation unchanged.
    /// </summary>
    /// <param name="authority">
    /// Original art authority and optional causal mechanics comparison.
    /// </param>
    /// <param name="conflict">
    /// Current conflict membership.
    /// </param>
    /// <param name="exchange">
    /// Current complete exchange.
    /// </param>
    /// <param name="side">
    /// Exact player or opposition side.
    /// </param>
    /// <returns>
    /// The applicable admitted cost increment; mismatched publication evidence throws.
    /// </returns>
    private static long ReadSpiritualValidationCostBurden(AfterlifeActionCostAuthorityContext authority,
        JsonObject conflict, JsonObject exchange, string side) =>
        authority.CompletedConflictValidation is { } completion
            ? side == "player" ? completion.Read(conflict, exchange).PlayerBurden : completion.Read(conflict, exchange).OppositionBurden
            : SpiritualWoundSourceSession.ReadCurrentActionCostBurden(authority.WoundMechanics, conflict, exchange, side);
}
