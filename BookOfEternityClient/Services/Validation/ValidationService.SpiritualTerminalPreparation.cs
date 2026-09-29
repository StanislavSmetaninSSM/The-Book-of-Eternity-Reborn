using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    internal sealed partial class SpiritualOriginalTurnCapture
    {
        private static readonly object TerminalIssuanceKey = new();
        private PreparedTerminal? _terminal;

        /// <summary>
        /// Returns this capture's preparation only for its exact retained source images.
        /// </summary>
        /// <param name="original">
        /// Signed source conflict root.
        /// </param>
        /// <param name="candidate">
        /// Exact terminal source projection.
        /// </param>
        /// <returns>
        /// Current preparation, or <see langword="null"/> for any other images.
        /// </returns>
        internal PreparedTerminal? ReadTerminalPreparation(JsonObject original, JsonObject candidate) =>
            _terminal?.Matches(original, candidate) == true ? _terminal : null;

        /// <summary>
        /// Retains the one signed opposition owner until its verified terminal exchange has executed.
        /// This preparation is not evidence that the exchange or retirement has completed.
        /// </summary>
        internal sealed class PreparedTerminal
        {
            private readonly SpiritualOriginalTurnCapture _capture;
            private readonly JsonObject _original;
            private readonly JsonObject _candidate;
            private readonly JsonObject _resolution;
            private EffectAcceptedTurnPlan? _completion;

            /// <summary>
            /// Constructs a preparation only with the original capture's private issuance key.
            /// </summary>
            /// <param name="key">
            /// Private capture issuance identity; every other object is rejected.
            /// </param>
            /// <param name="capture">
            /// Current capture owning the signed source and original input.
            /// </param>
            /// <param name="original">
            /// Exact signed conflict root.
            /// </param>
            /// <param name="candidate">
            /// Full validated terminal candidate root.
            /// </param>
            /// <param name="resolution">
            /// The single new resolution following the signed active conflict.
            /// </param>
            /// <param name="owners">
            /// Narrow execution authority retaining the signed opposition owner.
            /// </param>
            /// <param name="owner">
            /// The exact opposition owner scheduled for retirement.
            /// </param>
            internal PreparedTerminal(object key, SpiritualOriginalTurnCapture capture,
                JsonObject original, JsonObject candidate, JsonObject resolution,
                ResourceOwnerAuthority owners, ResourceOwnerKey owner)
            {
                if (!ReferenceEquals(key, TerminalIssuanceKey))
                    throw new ArgumentException("Original capture authority required.", nameof(key));
                _capture = capture;
                _original = original.DeepClone().AsObject();
                _candidate = candidate.DeepClone().AsObject();
                _resolution = resolution.DeepClone().AsObject();
                ExecutionOwners = owners;
                Owner = owner;
            }

            /// <summary>
            /// Gets the exact opposition owner whose existing retirement is deferred.
            /// </summary>
            internal ResourceOwnerKey Owner { get; }

            /// <summary>
            /// Gets the resource authority used exclusively by the retained executor.
            /// </summary>
            internal ResourceOwnerAuthority ExecutionOwners { get; }

            /// <summary>
            /// Gets the unchanged final accepted authority with the opposition owner historical.
            /// </summary>
            internal ResourceOwnerAuthority FinalOwners => _capture._input.PlanningContext!.Owners;

            /// <summary>
            /// Gets the exact original accepted input retained for final publication binding.
            /// </summary>
            internal AcceptedMechanicsInput OriginalInput => _capture._input;

            /// <summary>
            /// Gets the final terminal exchange identity from the validated resolution.
            /// </summary>
            internal string ExchangeId => _resolution["terminalExchange"]!["exchangeId"]!.GetValue<string>();

            /// <summary>
            /// Checks that the preparation still belongs to its exact live capture and executor.
            /// </summary>
            /// <param name="session">
            /// Executor expected to be retained by the capture.
            /// </param>
            /// <returns>
            /// <see langword="true"/> for the current exact executor; otherwise <see langword="false"/>.
            /// </returns>
            internal bool Owns(AcceptedMechanicsPlanner.ResourceExecutionSession session) =>
                _capture.IsCurrentOwner && ReferenceEquals(_capture._terminal, this) &&
                ReferenceEquals(_capture._resources, session) && _capture._source.IsCurrentOwner;

            /// <summary>
            /// Checks exact signed and terminal producer images without treating them as completed work.
            /// </summary>
            /// <param name="before">
            /// Signed original conflict root.
            /// </param>
            /// <param name="candidate">
            /// Current terminal projection.
            /// </param>
            /// <returns>
            /// <see langword="true"/> only for the retained exact images and current capture; otherwise <see langword="false"/>.
            /// </returns>
            internal bool Matches(JsonObject before, JsonObject candidate) =>
                _capture.IsCurrentOwner && ReferenceEquals(_capture._terminal, this) &&
                JsonNode.DeepEquals(before, _original) && JsonNode.DeepEquals(candidate, _candidate);

            /// <summary>
            /// Checks the exact terminal candidate retained by this current capture.
            /// </summary>
            /// <param name="candidate">
            /// Source or effect projection to join without creating an active conflict.
            /// </param>
            /// <returns>
            /// <see langword="true"/> only for the exact retained terminal candidate; otherwise <see langword="false"/>.
            /// </returns>
            internal bool MatchesCandidate(JsonObject candidate) => Matches(_original, candidate);

            /// <summary>
            /// Checks an effect execution view against the signed conflict's non-effect fields.
            /// </summary>
            /// <param name="effectConflict">
            /// Current lifecycle view whose combat conditions remain owned by the effect executor.
            /// </param>
            /// <returns>
            /// <see langword="true"/> when only effect-owned fields differ from signed A,
            /// or the effect owner has already finalized the exact terminal B; otherwise <see langword="false"/>.
            /// </returns>
            internal bool MatchesEffectExecution(JsonObject effectConflict) =>
                MatchesCandidate(_candidate) && (JsonNode.DeepEquals(_candidate, effectConflict) ||
                    AcceptedMechanicsCarrierAssembler.NonEffectFieldsAgree(
                        AfterlifeSpiritualConflictState.StatePath, _original, effectConflict));

            /// <summary>
            /// Checks that the actual final exchange and its wound routing have closed before retirement.
            /// </summary>
            /// <param name="session">
            /// Retained executor requesting retirement.
            /// </param>
            /// <returns>
            /// <see langword="true"/> only after the single expected exchange has closed completely; otherwise <see langword="false"/>.
            /// </returns>
            internal bool CanRetire(AcceptedMechanicsPlanner.ResourceExecutionSession session) =>
                Owns(session) && _capture._nextResourceOrdinal == 1 &&
                _capture._closedExchangeEvidence.Count == 1 &&
                _capture._closedExchangeEvidence[0].Interval.ExchangeId == ExchangeId &&
                _capture._effects is { HasPendingWoundIntegration: false };

            /// <summary>
            /// Seals terminal evidence only after the actual retirement and the retained effect completion.
            /// </summary>
            /// <param name="effects">
            /// Effect plan completed from the exact executor's final transcript.
            /// </param>
            internal void Complete(EffectAcceptedTurnPlan effects)
            {
                var session = _capture._resources;
                if (session == null || !CanRetire(session) || _capture._effects == null ||
                    !session.OwnsOriginalEffectCompletion(_capture._effects, effects) ||
                    session.Result is not { IsValid: true } result ||
                    FinalOwners.ValidateCanonicalAgreement(result.StateAfterImage, result.HistoryAfterImage).Count != 0)
                    throw new InvalidOperationException("Actual terminal resources and effects must complete together.");
                var transitions = result.AppliedTransitions.Concat(result.ReplayTransitions).ToArray();
                var retirement = transitions.Where(value => value.Operation == ResourceTransitionOperation.Retire &&
                    value.Coordinate.Realm == Owner.Realm && value.Coordinate.OwnerKind == Owner.OwnerKind &&
                    value.Coordinate.ResourceOwnerId == Owner.ResourceOwnerId).ToArray();
                if (retirement.Length != 1 || transitions.Any(value => value != retirement[0] &&
                    value.ExecutionSequence >= retirement[0].ExecutionSequence))
                    throw new InvalidOperationException("The exact opposition retirement must follow every terminal operation.");
                if (_completion != null && !ReferenceEquals(_completion, effects))
                    throw new InvalidOperationException("A terminal preparation has one completion.");
                _completion = effects;
            }

            /// <summary>
            /// Gets whether this preparation completed through the current retained owners.
            /// </summary>
            internal bool IsComplete => _completion != null && _capture._resources is { } session &&
                Owns(session) && _capture._effects != null &&
                session.OwnsOriginalEffectCompletion(_capture._effects, _completion);

            /// <summary>
            /// Creates the immutable closure row from completed terminal evidence and its accepted instance.
            /// </summary>
            /// <param name="instanceId">
            /// Instance independently resolved from the exact signed original receipt.
            /// </param>
            /// <param name="ordinal">
            /// Next append-only closure ordinal.
            /// </param>
            /// <returns>
            /// Detached closure row; uncompleted or revoked ownership throws.
            /// </returns>
            internal JsonObject CreateClosure(string instanceId, int ordinal)
            {
                if (!IsComplete)
                    throw new InvalidOperationException("Completed terminal ownership required.");
                var row = new JsonObject
                {
                    ["closureId"] = "", ["ordinal"] = ordinal, ["instanceId"] = instanceId,
                    ["terminalTurn"] = _capture._input.Turn,
                    ["terminalEventRef"] = "terminal:" + instanceId + ":" + ExchangeId,
                    ["terminalConflictFingerprint"] = SpiritualWoundStateJson.Hash(_resolution, "terminal_conflict"),
                    ["closureFingerprint"] = ""
                };
                var fingerprint = SpiritualWoundConflictInstanceState.ComputeRowFingerprint(row, "closure");
                row["closureFingerprint"] = fingerprint;
                row["closureId"] = "spiritual_closure_" + fingerprint[7..];
                return row;
            }
        }

        /// <summary>
        /// Validates a retained direct terminal draft and prepares only its signed opposition owner.
        /// Other source contours retain their existing admission and pending behavior.
        /// </summary>
        /// <returns>
        /// Existing conflict validation errors, or an empty list after preparation or a nonterminal contour.
        /// </returns>
        private async Task<IReadOnlyList<ValidationIssue>> PrepareTerminalExecutionAsync()
        {
            var images = _source.ReadTerminalCandidateImages();
            var original = _source.ReadOriginalConflict();
            var candidate = JsonNode.Parse(images.Conflict.Text!)!.AsObject();
            if (!SpiritualWoundSourceSession.TryReadSingleTerminal(original, candidate, out var resolution))
                return [];
            var frame = await _validator.CaptureSpiritualConflictValidationFrameAsync(images.Conflict);
            var validation = _validator.EvaluateSpiritualConflictValidationFrame(frame.WithCandidateImages(images));
            if (validation.Any(value => value.Severity == IssueSeverity.Error))
                return validation;
            var context = _input.PlanningContext!;
            var export = AfterlifeResourceOwnerComposer.ReadTerminalExecutionOwner(context.Definitions, original);
            if (export == null || !context.TerminalOwners.Contains(export.Key))
                throw new InvalidOperationException("Exact signed terminal opposition owner required.");
            var final = context.Owners.ExportInput();
            if (final.PreTurnOwners.Any(value => value.Key == export.Key) ||
                final.SameTurnOwners.Any(value => value.Key == export.Key) ||
                !final.HistoricalOwners.Contains(export.Key))
                throw new InvalidOperationException("Terminal owner must remain historical in final authority.");
            var execution = ResourceOwnerAuthority.Build(new ResourceOwnerAuthorityInput(
                final.PreTurnOwners.Append(export).ToArray(), final.SameTurnOwners,
                final.HistoricalOwners.Where(value => value != export.Key).ToArray()));
            if (execution.Issues.Count != 0)
                throw new InvalidOperationException("Signed terminal execution authority is invalid.");
            _terminal = new PreparedTerminal(TerminalIssuanceKey, this, original, candidate,
                resolution!, execution, export.Key);
            return [];
        }
    }

    internal sealed partial class SpiritualWoundSourceSession
    {
        /// <summary>
        /// Projects retained original draft images for complete terminal resolution validation before execution.
        /// </summary>
        /// <returns>
        /// Exact source-owned candidate images; this read grants no terminal execution authority.
        /// </returns>
        internal SpiritualConflictCandidateImages ReadTerminalCandidateImages()
        {
            if (!IsCurrentOwner)
                throw new InvalidOperationException("Current source owner required.");
            _originalConflict = Parse(AfterlifeSpiritualConflictState.StatePath, original: true, required: false);
            var projected = ProjectFullCandidateConflict();
            SpiritualConflictFileImage Read(string path) => new(_candidate[path] != null, _candidate[path]);
            return new(new(true, projected.ToJsonString()), Read(SoulPath),
                Read(ShiningAbodeState.StatePath), Read(AfterlifeEntityProfileState.StatePath));
        }

        /// <summary>
        /// Gets the current capture's preparation only when both retained source images match exactly.
        /// </summary>
        private SpiritualOriginalTurnCapture.PreparedTerminal? TerminalPreparation =>
            _validator._spiritualOriginalTurnCapture?.ReadTerminalPreparation(_originalConflict, _candidateConflict);

        /// <summary>
        /// Discharges only the closure covered by this exact completed terminal preparation.
        /// </summary>
        /// <param name="terminal">
        /// Current capture-issued proof after resource retirement and effect completion.
        /// </param>
        internal void CompleteTerminal(SpiritualOriginalTurnCapture.PreparedTerminal terminal)
        {
            if (!ReferenceEquals(TerminalPreparation, terminal) || !terminal.IsComplete ||
                _pending.Count != 1 || _pending[0].Kind != SpiritualSourceRequirement.TerminalClosure)
                throw new InvalidOperationException("Exact unresolved terminal closure required.");
            _pending.Clear();
        }

        /// <summary>
        /// Reads the bounded direct terminal contour without synthesizing an active candidate.
        /// </summary>
        /// <param name="original">
        /// Signed original conflict root.
        /// </param>
        /// <param name="candidate">
        /// Full candidate containing one new resolution and no active conflict, with only the
        /// existing bounded recent-history pruning allowed.
        /// </param>
        /// <param name="resolution">
        /// Matching direct terminal resolution, or <see langword="null"/> on rejection.
        /// </param>
        /// <returns>
        /// <see langword="true"/> for one complete terminal exchange directly following the signed state;
        /// otherwise <see langword="false"/>.
        /// </returns>
        internal static bool TryReadSingleTerminal(JsonObject original, JsonObject candidate,
            out JsonObject? resolution)
        {
            resolution = null;
            if (original["activeConflict"] is not JsonObject active || candidate["activeConflict"] != null ||
                original["recentConflicts"] is not JsonArray before || candidate["recentConflicts"] is not JsonArray after ||
                after.Count != Math.Min(20, before.Count + 1) ||
                !before.Skip(Math.Max(0, before.Count + 1 - 20)).Select((row, index) =>
                    JsonNode.DeepEquals(row, after[index])).All(value => value) ||
                after.LastOrDefault() is not JsonObject terminal ||
                ExactString(terminal["conflictId"]) != ExactString(active["conflictId"]) ||
                ExactString(terminal["resolutionState"]) != "resolved" ||
                terminal["terminalExchange"] is not JsonObject exchange ||
                ExactString(exchange["exchangeId"]) is not { } id ||
                (active["exchangeLog"] as JsonArray)?.Any(row => ExactString(row?["exchangeId"]) == id) == true ||
                new[] { "playerSideStrain", "oppositionSideStrain" }.Any(field =>
                    !JsonNode.DeepEquals(active[field], exchange["before"]?[field])) ||
                !JsonNode.DeepEquals(exchange["diceAudit"], terminal["diceAudit"]))
                return false;
            resolution = terminal;
            return true;
        }
    }
}
