using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    internal sealed partial class SpiritualWoundSourceSession
    {
        internal sealed partial class PreparedContinuation
        {
            /// <summary>Proves the ordinary capped guard and its one new source without advancing any live frontier.</summary>
            internal static async Task<PreparedSpiritualSource?> CheckOriginalCappedGuardExchangeAsync(
                SpiritualWoundSourceSession owner, FileSystemManager.CanonicalWriteLease lease,
                SpiritualOriginalTurnCapture capture, JsonObject frozenRaw, int ordinal,
                ResourceOwnerAuthority owners, ResourceStateLedger state)
            {
                owner._validator._fs.EnsureCanonicalWriteLeaseActive(lease);
                await owner._continuationGate.WaitAsync();
                try
                {
                    if (!owner.IsCurrentOwner || owner._awaitingOriginalPrefix ||
                        owner._projectionClock is not SpiritualWoundProjectionClock clock ||
                        !owner.TryReadInitialActiveExchangeInventory(out var conflictId, out var ids) ||
                        ordinal + 1 != ids.Length || owner._checkedExchanges.Count != ordinal) return null;
                    using var probe = clock.BeginConflictValidationProbe(RetainedEvidence(owner));
                    var prepared = await PrepareCoreAsync(owner, lease, capture);
                    if (prepared.Ticket is not { } ticket || prepared.Issues.Count != 0) return null;
                    var next = ticket._prospective;
                    if (next._checkedExchanges.Count != ordinal + 1 || next._missingActionCostAudit is not null ||
                        next._pending.Count != 0 || next._sources.Count != owner._sources.Count + 1 ||
                        !owner._sources.SequenceEqual(next._sources.Take(owner._sources.Count)) ||
                        next._candidate[AfterlifeSpiritualConflictState.StatePath] is not { } rawText ||
                        !JsonNode.DeepEquals(frozenRaw, SpiritualWoundDependentDraftPolicy.ReadStrictRoot(rawText))) return null;
                    var index = (owner._originalConflict["activeConflict"]!["exchangeLog"] as JsonArray)!.Count + ordinal;
                    if (AfterlifeSpiritualConflictState.ResolveRawExchange(owner._originalConflict, frozenRaw, index) is not { } raw ||
                        raw.Exchange["exchangeId"]?.GetValue<string>() != ids[^1] ||
                        !ConflictTokenEquals(AfterlifeSpiritualConflictState.GetNodeString(raw.Exchange["operationType"]), "guard") ||
                        !ConflictTokenEquals(AfterlifeSpiritualConflictState.GetNodeString(raw.Exchange["outcome"]), "setback") ||
                        AfterlifeSpiritualConflictState.GetNodeString(raw.Exchange["before"]?["playerSideStrain"]) != "overwhelmed" ||
                        AfterlifeSpiritualConflictState.GetNodeString(raw.Exchange["after"]?["playerSideStrain"]) != "broken" ||
                        !JsonNode.DeepEquals(raw.Exchange, JsonNode.Parse(next._checkedExchanges[^1]))) return null;
                    var source = next._sources[^1];
                    if (source.ConflictId != conflictId || source.ExchangeId != ids[^1] ||
                        source.AffectedSide != "player" || source.Operation != "pressure" ||
                        source.StrainPreimageJson is not null ||
                        !JsonNode.DeepEquals(raw.Exchange, JsonNode.Parse(source.ExchangeJson))) return null;
                    var batches = ticket.BuildResourceBatchesCore(owner, lease, owners, state);
                    if (!batches.IsValid) return null;
                    var batch = batches.Exchanges.SingleOrDefault(row => row.Ordinal == ordinal);
                    return batch is not null && batch.ConflictId == conflictId &&
                        batch.ExchangeId == ids[^1] && batch.PendingSides.Count == 0 ? source : null;
                }
                finally { owner._continuationGate.Release(); }
            }
        }
    }
}
