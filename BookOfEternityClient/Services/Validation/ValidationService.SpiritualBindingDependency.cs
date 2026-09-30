using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    /// <summary>
    /// Recognizes binding prerequisite diagnostics without granting permission to change their result.
    /// </summary>
    /// <param name="issue">
    /// Ordinary validation diagnostic at an exact exchange operation coordinate.
    /// </param>
    /// <returns>
    /// The projected exchange index, or <see langword="null"/> for any other diagnostic.
    /// </returns>
    internal static int? BindingDependencyIndex(ValidationIssue issue)
    {
        if (issue.Code is not ("afterlife_conflict_binding_without_leverage" or
            "afterlife_conflict_force_binding_without_strong_leverage")) return null;
        var match = Regex.Match(issue.FilePath, @"^activeConflict\.exchangeLog\[(\d+)\]\.operationType$",
            RegexOptions.CultureInvariant);
        return match.Success && int.TryParse(match.Groups[1].Value, NumberStyles.None,
            CultureInfo.InvariantCulture, out var index) ? index : null;
    }

    /// <summary>
    /// Compares one original successful binding with its exact failed result, without selecting an outcome for the GM.
    /// </summary>
    internal sealed class SpiritualBindingDraftCorrection
    {
        private readonly JsonObject _original;

        /// <summary>
        /// Freezes the independently proved raw binding and its two permitted result coordinates.
        /// </summary>
        /// <param name="pointer">
        /// Exact effective raw exchange pointer.
        /// </param>
        /// <param name="original">
        /// Ordinary-valid successful binding whose only consequence is its control delta.
        /// </param>
        internal SpiritualBindingDraftCorrection(string pointer, JsonObject original)
        {
            Pointer = pointer;
            _original = original.DeepClone().AsObject();
            Fields = [pointer + "/after/controlState", pointer + "/outcome"];
        }

        /// <summary>
        /// Gets the exact raw exchange pointer.
        /// </summary>
        internal string Pointer { get; }

        /// <summary>
        /// Gets only the result and control fields, independently of arithmetic and cost permissions.
        /// </summary>
        internal IReadOnlyList<string> Fields { get; }

        /// <summary>
        /// Requires the original result group or a failed outcome with the exact original before-control presence and value.
        /// </summary>
        /// <param name="candidate">
        /// Actual raw exchange; other fields are constrained by the enclosing frozen-draft comparison.
        /// </param>
        /// <returns>
        /// <see langword="true"/> for one complete bounded result group; otherwise <see langword="false"/>.
        /// </returns>
        internal bool Allows(JsonNode? candidate)
        {
            if (candidate is not JsonObject exchange || exchange["after"] is not JsonObject after) return false;
            var originalAfter = _original["after"]!.AsObject();
            if (JsonNode.DeepEquals(_original["outcome"], exchange["outcome"]) &&
                SameControl(originalAfter, after)) return true;
            return exchange["outcome"] is JsonValue value && value.TryGetValue<string>(out var outcome) &&
                outcome is "no_effect" or "blocked" && SameControl(_original["before"]!.AsObject(), after);
        }

        /// <summary>
        /// Identifies an actual failed-result response rather than the still-unchanged successful original.
        /// </summary>
        /// <param name="candidate">
        /// Raw exchange already constrained by the complete dependent policy.
        /// </param>
        /// <returns>
        /// <see langword="true"/> only for a permitted authored failed result.
        /// </returns>
        internal bool IsCorrected(JsonNode? candidate) => Allows(candidate) &&
            !JsonNode.DeepEquals(_original["outcome"], candidate?["outcome"]);

        /// <summary>
        /// Distinguishes absent, null and present control rather than conflating their raw contracts.
        /// </summary>
        /// <param name="before">
        /// Required original snapshot.
        /// </param>
        /// <param name="after">
        /// Proposed snapshot to compare.
        /// </param>
        /// <returns>
        /// <see langword="true"/> when presence and the complete value agree; otherwise <see langword="false"/>.
        /// </returns>
        private static bool SameControl(JsonObject before, JsonObject after) =>
            before.ContainsKey("controlState") == after.ContainsKey("controlState") &&
            JsonNode.DeepEquals(before["controlState"], after["controlState"]);
    }

    internal sealed partial class SpiritualOriginalTurnCapture
    {
        /// <summary>
        /// Permits inspection of a remaining terminal echo only for an independently proved original last binding.
        /// The dependent walk must still prove the complete actual correction before issuing any permissions.
        /// </summary>
        /// <param name="issues">
        /// Ordinary diagnostics from the current physical draft after replaying its saved wound selection.
        /// </param>
        /// <returns>
        /// <see langword="true"/> only for the isolated final-control diagnostic and the retained original proof.
        /// </returns>
        internal bool CanInspectBindingTerminalControl(IReadOnlyList<ValidationIssue> issues) =>
            _c2SelectedDecision?.BindingOriginal is not null && issues.Count != 0 &&
            issues.All(issue => issue.Code == "afterlife_conflict_control_snapshot_missing" &&
                issue.FilePath == "activeConflict.controlState");

        /// <summary>
        /// Retains pre-selection comparison inputs after a discarded ordinary validation succeeds; it grants no execution authority.
        /// </summary>
        /// <param name="Ordinal">
        /// Exact next resource ordinal before this wound was selected.
        /// </param>
        /// <param name="Pointer">
        /// Effective raw carrier of the original last exchange.
        /// </param>
        /// <param name="ExchangeJson">
        /// Frozen pre-selection exchange, never the proposed corrected image.
        /// </param>
        /// <param name="PositionRank">
        /// Effective rank read from the real pre-selection mechanics, including older wounds.
        /// </param>
        private sealed record OriginalBindingComparison(int Ordinal, string Pointer, string ExchangeJson, int PositionRank);

        /// <summary>
        /// Checks the original last binding before the real wound changes its mechanics, without consuming its source or resources.
        /// </summary>
        /// <param name="lease">
        /// Active canonical lease held with the capture gate.
        /// </param>
        /// <param name="checkpoint">
        /// Matched checkpoint whose committed input layer precedes this selection.
        /// </param>
        /// <returns>
        /// A bounded comparison, or <see langword="null"/> when original legality cannot be proved.
        /// </returns>
        private async Task<OriginalBindingComparison?> ReadOriginalBindingComparisonAsync(
            FileSystemManager.CanonicalWriteLease lease, SpiritualWoundCaptureCheckpointState checkpoint)
        {
            if (!_source.TryReadInitialActiveExchangeInventory(out _, out var ids) ||
                _closedExchangeEvidence.Count + 1 != ids.Length || _coldProposedInputs is not null) return null;
            var original = _source.ReadMechanicsConflict(this);
            if (original["activeConflict"] is not JsonObject conflict ||
                conflict["exchangeLog"] is not JsonArray prior ||
                ReadC2CommittedInputLayer(checkpoint)[AfterlifeSpiritualConflictState.StatePath].Bytes is not { } bytes)
                return null;
            var baseline = SpiritualWoundDependentDraftPolicy.ReadStrictRoot(SpiritualWoundStateJson.DecodeUtf8JsonText(bytes));
            if (AfterlifeSpiritualConflictState.ResolveRawExchange(original, baseline,
                    prior.Count + _closedExchangeEvidence.Count) is not { } raw ||
                raw.Exchange["exchangeId"]?.GetValue<string>() != ids[^1] || !IsControlOnlySuccessfulBinding(raw.Exchange))
                return null;
            var issues = new List<ValidationIssue>();
            var mechanics = PrepareSourceMechanics(_source, issues);
            if (mechanics is null || issues.Count != 0 || mechanics.Ordinal != _nextResourceOrdinal ||
                SpiritualWoundSourceSession.ReadCurrentEffectivePosition(mechanics, conflict, raw.Exchange) is not { } rank)
                return null;
            var lawful = await SpiritualWoundSourceSession.PreparedContinuation.CheckOriginalBindingAsync(
                _source, lease, this, baseline, _nextResourceOrdinal,
                _terminal?.ExecutionOwners ?? _input.PlanningContext!.Owners, SourceResourceBaseline);
            return lawful ? new(_nextResourceOrdinal, raw.Pointer, raw.Exchange.ToJsonString(), rank) : null;
        }

        /// <summary>
        /// Derives failed-result fields only when the same originally lawful last binding has lost its genuine effective leverage.
        /// </summary>
        /// <param name="exchange">
        /// Exact frozen raw exchange at the current correction frontier.
        /// </param>
        /// <param name="pointer">
        /// Effective raw carrier resolved from the signed original.
        /// </param>
        /// <param name="rank">
        /// Effective rank under the actual selected wound.
        /// </param>
        /// <param name="position">
        /// Exact owner-derived arithmetic comparison for this same exchange.
        /// </param>
        /// <returns>
        /// Bounded comparison fields, or <see langword="null"/> when an independent prerequisite remains or provenance differs.
        /// </returns>
        private SpiritualBindingDraftCorrection? CreateBindingCorrection(JsonObject exchange, string pointer, int rank,
            SpiritualPositionDraftCorrection position)
        {
            var original = _c2SelectedDecision?.BindingOriginal;
            if (original is null || original.Ordinal != _closedExchangeEvidence.Count ||
                original.Ordinal != _nextResourceOrdinal || original.Pointer != pointer || rank >= original.PositionRank ||
                !JsonNode.DeepEquals(JsonNode.Parse(original.ExchangeJson), exchange)) return null;
            var corrected = exchange.DeepClone().AsObject();
            corrected["diceAudit"]!["outcomeBand"] = position.CorrectedOutcomeBand;
            var strong = ConflictTokenEquals(AfterlifeSpiritualConflictState.GetNodeString(exchange["operationType"]), "force_binding");
            if (strong ? HasStrongBindingLeverage(corrected, rank) : HasBindingLeverage(corrected, rank)) return null;
            return new(pointer, exchange);
        }

        /// <summary>
        /// Restricts this refinement to successful binding whose snapshots differ only in control.
        /// </summary>
        /// <param name="exchange">
        /// Original frozen exchange, before its ordinary eligibility probe.
        /// </param>
        /// <returns>
        /// <see langword="true"/> for the bounded consequence shape; ordinary legality still requires the real source probe.
        /// </returns>
        private static bool IsControlOnlySuccessfulBinding(JsonObject exchange)
        {
            if (!ConflictTokenEquals(AfterlifeSpiritualConflictState.GetNodeString(exchange["operationType"]), "binding", "force_binding") ||
                !IsSuccessfulArtOutcome(AfterlifeSpiritualConflictState.GetNodeString(exchange["outcome"])) ||
                exchange["before"] is not JsonObject before || exchange["after"] is not JsonObject after) return false;
            var left = before.DeepClone().AsObject();
            var right = after.DeepClone().AsObject();
            left.Remove("controlState");
            right.Remove("controlState");
            return JsonNode.DeepEquals(left, right);
        }
    }

    internal sealed partial class SpiritualWoundSourceSession
    {
        internal sealed partial class PreparedContinuation
        {
            /// <summary>
            /// Checks one frozen original last exchange in a discarded source probe, returning no provisional authority or timestamp.
            /// </summary>
            /// <param name="owner">
            /// Current source bound to the original capture.
            /// </param>
            /// <param name="lease">
            /// Active canonical lease covering the entire probe.
            /// </param>
            /// <param name="capture">
            /// Exact capture whose next source frontier and pre-selection mechanics are checked.
            /// </param>
            /// <param name="frozenRaw">
            /// Complete committed raw conflict layer before the selected wound.
            /// </param>
            /// <param name="ordinal">
            /// Current next resource ordinal, which must identify the last original exchange.
            /// </param>
            /// <param name="owners">
            /// Actual original resource-owner authority used by normal advancement.
            /// </param>
            /// <param name="state">
            /// Actual retained prefix ledger used by normal advancement.
            /// </param>
            /// <returns>
            /// <see langword="true"/> only for a completely validated original exchange and matching resource batch.
            /// </returns>
            internal static async Task<bool> CheckOriginalBindingAsync(SpiritualWoundSourceSession owner,
                FileSystemManager.CanonicalWriteLease lease, SpiritualOriginalTurnCapture capture, JsonObject frozenRaw,
                int ordinal, ResourceOwnerAuthority owners, ResourceStateLedger state)
            {
                owner._validator._fs.EnsureCanonicalWriteLeaseActive(lease);
                await owner._continuationGate.WaitAsync();
                try
                {
                    if (!owner.IsCurrentOwner || owner._awaitingOriginalPrefix ||
                        owner._projectionClock is not SpiritualWoundProjectionClock clock ||
                        !owner.TryReadInitialActiveExchangeInventory(out var conflictId, out var ids) ||
                        ordinal + 1 != ids.Length || owner._checkedExchanges.Count != ordinal) return false;
                    using var probe = clock.BeginConflictValidationProbe(RetainedEvidence(owner));
                    var prepared = await PrepareCoreAsync(owner, lease, capture);
                    if (prepared.Ticket is not { } ticket || prepared.Issues.Count != 0) return false;
                    var next = ticket._prospective;
                    if (next._checkedExchanges.Count != ordinal + 1 || next._missingActionCostAudit is not null ||
                        next._pending.Count != 0 || next._sources.Count != owner._sources.Count ||
                        next._candidate[AfterlifeSpiritualConflictState.StatePath] is not { } rawText ||
                        !JsonNode.DeepEquals(frozenRaw, SpiritualWoundDependentDraftPolicy.ReadStrictRoot(rawText))) return false;
                    var index = (owner._originalConflict["activeConflict"]!["exchangeLog"] as JsonArray)!.Count + ordinal;
                    if (AfterlifeSpiritualConflictState.ResolveRawExchange(owner._originalConflict, frozenRaw, index) is not { } raw ||
                        raw.Exchange["exchangeId"]?.GetValue<string>() != ids[^1] ||
                        !JsonNode.DeepEquals(raw.Exchange, JsonNode.Parse(next._checkedExchanges[^1]))) return false;
                    var batches = ticket.BuildResourceBatchesCore(owner, lease, owners, state);
                    if (!batches.IsValid) return false;
                    var batch = batches.Exchanges.SingleOrDefault(row => row.Ordinal == ordinal);
                    return batch is not null && batch.ConflictId == conflictId &&
                        batch.ExchangeId == ids[^1] && batch.PendingSides.Count == 0;
                }
                finally { owner._continuationGate.Release(); }
            }
        }
    }
}
