using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    /// <summary>
    /// Retains detached original exchange coordinates and signed dice for comparison.
    /// It does not authorize a pending packet or additional gameplay execution.
    /// </summary>
    /// <param name="ConflictId">
    /// Exact signed active conflict identity, including a direct terminal suffix.
    /// </param>
    /// <param name="ExchangeIds">
    /// Ordered new exchange identities from the original admitted draft, including unexecuted rows.
    /// </param>
    /// <param name="AcceptedD20Values">
    /// Exact signed original D20 pool values in source-index order.
    /// </param>
    internal sealed record SpiritualOriginalActiveExchangeInventory(
        string ConflictId, IReadOnlyList<string> ExchangeIds,
        IReadOnlyList<int> AcceptedD20Values);

    /// <summary>
    /// Carries a same-active or direct terminal inventory, or an explicit unsupported-contour diagnostic.
    /// </summary>
    /// <param name="Inventory">
    /// Detached original inventory, or <see langword="null"/> when this contour is not supported.
    /// </param>
    /// <param name="Issues">
    /// Diagnostics explaining why no inventory was issued.
    /// </param>
    internal sealed record SpiritualOriginalActiveExchangeInventoryResult(
        SpiritualOriginalActiveExchangeInventory? Inventory,
        IReadOnlyList<ValidationIssue> Issues);

    internal sealed partial class SpiritualOriginalTurnCapture
    {
        /// <summary>
        /// Reads the immutable original active or direct terminal exchange inventory under the capture gate.
        /// Later physical drafts and executed-prefix projections cannot increase this bound.
        /// </summary>
        /// <param name="lease">
        /// Active canonical write lease authenticating this current capture.
        /// </param>
        /// <returns>
        /// Detached comparison evidence or an explicit pending-contour diagnostic.
        /// </returns>
        internal SpiritualOriginalActiveExchangeInventoryResult ReadOriginalActiveExchangeInventory(
            FileSystemManager.CanonicalWriteLease lease)
        {
            if (!_gate.Wait(0))
                throw new InvalidOperationException("The original capture is continuing.");
            try
            {
                EnsureCurrent(lease);
                if (!_source.TryReadInitialActiveExchangeInventory(out var conflictId, out var exchangeIds))
                    return new(null, new[]
                    {
                        new ValidationIssue(AfterlifeSpiritualConflictState.StatePath,
                            IssueSeverity.Error,
                            "An original same-active or direct terminal exchange contour is required before C1 evidence export.",
                            code: "spiritual_original_exchange_contour_pending",
                            section: "AcceptedTurnWoundMaterialization")
                    });
                return new(new(conflictId!, Array.AsReadOnly(exchangeIds),
                    Array.AsReadOnly(_source.ReadOriginalAcceptedD20Values())),
                    Array.Empty<ValidationIssue>());
            }
            finally { _gate.Release(); }
        }
    }

    internal sealed partial class SpiritualWoundSourceSession
    {
        /// <summary>
        /// Copies the signed original conflict selected by this current source owner.
        /// </summary>
        /// <returns>
        /// Detached full original conflict root, before any accepted exchange.
        /// </returns>
        internal JsonObject ReadOriginalConflict()
        {
            if (!IsCurrentOwner)
                throw new InvalidOperationException("Current source owner required.");
            return _originalConflict.DeepClone().AsObject();
        }

        /// <summary>
        /// Reads the initial full active or direct terminal suffix before any executed-frontier trimming.
        /// Start, replacement and terminal contours requiring an intermediate prefix remain explicit prerequisites.
        /// </summary>
        /// <param name="conflictId">
        /// Original continuing conflict identity, or <see langword="null"/> on rejection.
        /// </param>
        /// <param name="exchangeIds">
        /// Ordered detached new exchange identities, or an empty array on rejection.
        /// </param>
        /// <returns>
        /// <see langword="true"/> only for a nonempty exact active suffix or one direct terminal exchange.
        /// </returns>
        internal bool TryReadInitialActiveExchangeInventory(out string? conflictId,
            out string[] exchangeIds)
        {
            if (!IsCurrentOwner)
            {
                conflictId = null;
                exchangeIds = [];
                return false;
            }
            return TryProjectInitialActiveExchangeInventory(_originalConflict,
                _initialFullCandidateConflict, out conflictId, out exchangeIds);
        }

        /// <summary>
        /// Projects the exact active or direct terminal suffix from detached original and full draft contours.
        /// Malformed rows and repeated identities fail closed.
        /// </summary>
        /// <param name="originalConflict">
        /// Signed original conflict state.
        /// </param>
        /// <param name="initialFullCandidateConflict">
        /// Full initial candidate before accepted-frontier trimming.
        /// </param>
        /// <param name="conflictId">
        /// Continuing conflict identity, or <see langword="null"/> on rejection.
        /// </param>
        /// <param name="exchangeIds">
        /// Ordered detached new exchange identities, or an empty array on rejection.
        /// </param>
        /// <returns>
        /// <see langword="true"/> only for a nonempty exact active suffix or one direct terminal exchange.
        /// </returns>
        internal static bool TryProjectInitialActiveExchangeInventory(JsonObject originalConflict,
            JsonObject initialFullCandidateConflict, out string? conflictId,
            out string[] exchangeIds)
        {
            conflictId = null;
            exchangeIds = [];
            if (TryReadSingleTerminal(originalConflict, initialFullCandidateConflict, out var terminal))
            {
                conflictId = ExactString(terminal!["conflictId"]);
                exchangeIds = [ExactString(terminal["terminalExchange"]!["exchangeId"])!];
                return true;
            }
            if (originalConflict["activeConflict"] is not JsonObject prior ||
                initialFullCandidateConflict["activeConflict"] is not JsonObject active ||
                !JsonNode.DeepEquals(originalConflict["recentConflicts"],
                    initialFullCandidateConflict["recentConflicts"]))
                return false;
            var id = ExactString(prior["conflictId"]);
            if (id is null || id != ExactString(active["conflictId"]) ||
                prior["exchangeLog"] is not JsonArray historical ||
                active["exchangeLog"] is not JsonArray complete ||
                complete.Count <= historical.Count)
                return false;
            for (var index = 0; index < historical.Count; index++)
                if (!JsonNode.DeepEquals(historical[index], complete[index]))
                    return false;
            var ids = new List<string>();
            var distinct = new HashSet<string>(StringComparer.Ordinal);
            foreach (var earlier in historical)
            {
                if (earlier is not JsonObject earlierExchange)
                    return false;
                var earlierId = ExactString(earlierExchange["exchangeId"]);
                if (earlierId is null || !distinct.Add(earlierId))
                    return false;
            }
            for (var index = historical.Count; index < complete.Count; index++)
            {
                if (complete[index] is not JsonObject exchange)
                    return false;
                var exchangeId = ExactString(exchange["exchangeId"]);
                if (exchangeId is null || !distinct.Add(exchangeId))
                    return false;
                ids.Add(exchangeId);
            }
            conflictId = id;
            exchangeIds = ids.ToArray();
            return true;
        }

        /// <summary>
        /// Copies the signed original D20 values retained by this current source owner.
        /// </summary>
        /// <returns>
        /// Detached D20 pool in original source-index order.
        /// </returns>
        internal int[] ReadOriginalAcceptedD20Values()
        {
            if (!IsCurrentOwner)
                throw new InvalidOperationException("Current source owner required.");
            return _original.AcceptedD20EventValues.ToArray();
        }
    }
}
