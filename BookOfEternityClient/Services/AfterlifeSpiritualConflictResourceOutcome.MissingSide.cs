using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static partial class AfterlifeSpiritualConflictResourceOutcome
{
    // Exactly one private snapshot per TryCreate call. Batch evidence stores an
    // index into these inaccessible, never-mutated roots instead of cloning the
    // complete accepted log once per exchange.
    private sealed record ProducerContext(int Turn, JsonObject Before, JsonObject Candidate,
        string Owners, string State,
        AcceptedMechanicsPlanner.ResourceExecutionSession.SpiritualMechanicsContext? Frontier);
    private sealed record ProducerImage(ProducerContext Context, int Index);
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ExchangeBatch, ProducerImage>
        ProducerImages = new();

    internal sealed record MissingSideExtension(ExchangeBatch Merged, ExchangeBatch Appended,
        IReadOnlyDictionary<ResourceOperationKey, ResourceOperationKey> Aliases,
        IReadOnlyList<ResourceMutationSourceExport> CheckedSources);

    internal sealed partial class ExchangeBatch
    {
        internal bool IsRawProducerBatch => ProducerImages.TryGetValue(this, out _);

        // This returns execution data only. An arbitrary remap must never inherit
        // raw producer evidence, even when called on a genuine producer batch.
        internal ExchangeBatch RemapDependencies(
            IReadOnlyDictionary<ResourceOperationKey, ResourceOperationKey> aliases)
        {
            if (aliases.Count == 0)
                return this;
            var mapped = _mutations.Select(m => m with
            {
                Dependencies = m.Dependencies.Select(k => aliases.TryGetValue(k, out var a) ? a : k).ToArray(),
                EventRequirements = m.EventRequirements.Select(r => aliases.TryGetValue(r.Producer, out var a)
                    ? r with { Producer = a } : r).ToArray()
            });
            return new ExchangeBatch(ConflictId, ExchangeId, Ordinal, Player, Opposition,
                _sources, mapped, _expected);
        }

        // Both this and checkedCompletion must be original TryCreate outputs.
        // retainedExecution may contain previously aliased operations, but the
        // result is deliberately not producer evidence. The session owns and
        // checks the preparation that can authorize consuming that result.
        internal MissingSideExtension? PrepareMissingSide(ExchangeBatch checkedCompletion,
            ExchangeBatch retainedExecution,
            IReadOnlyDictionary<ResourceOperationKey, ResourceOperationKey> retainedAliases)
        {
            if (!ProducerImages.TryGetValue(this, out var original) ||
                !ProducerImages.TryGetValue(checkedCompletion, out var next) || PendingSides.Count == 0 ||
                ConflictId != checkedCompletion.ConflictId || ExchangeId != checkedCompletion.ExchangeId ||
                Ordinal != checkedCompletion.Ordinal || original.Context.Turn != next.Context.Turn ||
                original.Index != next.Index || original.Context.Owners != next.Context.Owners ||
                original.Context.State != next.Context.State ||
                !ReferenceEquals(original.Context.Frontier, next.Context.Frontier) ||
                !JsonNode.DeepEquals(original.Context.Before, next.Context.Before) ||
                retainedExecution.ConflictId != ConflictId || retainedExecution.ExchangeId != ExchangeId ||
                retainedExecution.Ordinal != Ordinal || retainedExecution.Player != Player ||
                retainedExecution.Opposition != Opposition)
                return null;
            var oldContext = MaskMissing(original.Context.Candidate, original.Index, PendingSides);
            var newContext = MaskMissing(next.Context.Candidate, next.Index, PendingSides);
            if (!JsonNode.DeepEquals(oldContext, newContext))
                return null;
            var supplied = PendingSides.Where(side => !checkedCompletion.PendingSides.Contains(side)).ToArray();
            if (supplied.Length == 0)
                return null;

            var completed = checkedCompletion.RemapDependencies(retainedAliases);
            var newSources = new List<ResourceMutationSourceExport>();
            var newMutations = new List<ResourceMutationIntent>();
            var newExpected = new List<ExpectedTransition>();
            var aliases = new Dictionary<ResourceOperationKey, ResourceOperationKey>();
            foreach (var side in supplied)
            {
                var eventRef = $"turn_{original.Context.Turn}:afterlife_conflict:{ExchangeId}:{side}";
                foreach (var mutation in completed._mutations.Where(m => m.EventRef == eventRef))
                {
                    var source = completed._sources.Single(s => s.SourceKind == mutation.Source.SourceKind &&
                        s.SourceId == mutation.Source.SourceId);
                    using var hash = new ResourceFingerprintBuilder("spiritual-missing-side-source-v1");
                    hash.Append(source.SourceKind);
                    hash.Append(source.SourceId);
                    hash.Append(source.AuthorityFingerprint);
                    hash.Append(side);
                    hash.Append(Ordinal);
                    var fingerprint = hash.Build();
                    var alias = "spiritual_resume_" + fingerprint.Replace("sha256:", "", StringComparison.Ordinal);
                    var exported = source with { SourceId = alias };
                    var appended = mutation with { Source = mutation.Source with { SourceId = alias } };
                    if (retainedAliases.ContainsKey(mutation.Key) || !aliases.TryAdd(mutation.Key, appended.Key))
                        return null;
                    newSources.Add(exported);
                    newMutations.Add(appended);
                    newExpected.AddRange(completed._expected.Where(e => e.EventRef == eventRef)
                        .Select(e => e with { SourceId = alias }));
                }
            }
            var merged = new ExchangeBatch(ConflictId, ExchangeId, Ordinal,
                completed.Player, completed.Opposition, retainedExecution._sources.Concat(newSources),
                retainedExecution._mutations.Concat(newMutations), retainedExecution._expected.Concat(newExpected));
            var appendedBatch = new ExchangeBatch(ConflictId, ExchangeId, Ordinal,
                completed.Player, completed.Opposition, newSources, newMutations, newExpected);
            return new MissingSideExtension(merged, appendedBatch,
                new System.Collections.ObjectModel.ReadOnlyDictionary<ResourceOperationKey, ResourceOperationKey>(aliases),
                completed.Sources);
        }

        private static JsonObject MaskMissing(JsonObject root, int index, IReadOnlyList<string> missing)
        {
            var copy = root.DeepClone().AsObject();
            var exchange = copy["activeConflict"]!["exchangeLog"]![index]!.AsObject();
            if (exchange["actionCostAudit"] is JsonObject audit)
            {
                foreach (var side in missing)
                    audit.Remove(side);
                if (audit.Count == 0)
                    exchange.Remove("actionCostAudit");
            }
            return copy;
        }
    }
}
