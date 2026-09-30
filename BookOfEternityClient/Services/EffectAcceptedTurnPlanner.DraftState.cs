using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static partial class EffectAcceptedTurnPlanner
{
    internal enum EffectDraftPhase
    {
        NonConsumingTrigger,
        NonterminalReaction,
        ReplacementAgreement,
        ConsumingTrigger,
        FinalLifetime,
        FinalTerminal
    }

    internal enum EffectDraftApplicationProvenanceKind
    {
        Direct,
        Reaction,
        SeverityGeneration
    }

    // Immutable retained material, not a spiritual source or insertion capability.
    internal sealed record EffectDraftCarrierEdit(
        int Ordinal,
        string EffectId,
        EffectCarrierCoordinate Carrier,
        string? BeforeJson,
        string? AfterJson)
    {
        internal JsonObject? ReadBefore() => BeforeJson == null
            ? null : JsonNode.Parse(BeforeJson)!.AsObject();
        internal JsonObject? ReadAfter() => AfterJson == null
            ? null : JsonNode.Parse(AfterJson)!.AsObject();
    }

    internal sealed class EffectDraftSourceBinding
    {
        private readonly string _definitionJson;
        private readonly string[] _predicates;

        internal EffectDraftSourceBinding(EffectSourceAuthorityEntry source)
        {
            Key = source.Key;
            _definitionJson = source.Definition.ToJsonString();
            Materializable = source.Materializable;
            Active = source.Active;
            SameTurn = source.SameTurn;
            SourceRef = source.SourceRef;
            RequiredApplicationAuthority = source.RequiredApplicationAuthority;
            _predicates = source.SatisfiedPredicates.OrderBy(value => value, StringComparer.Ordinal).ToArray();
        }

        internal EffectSourceKey Key { get; }
        internal bool Materializable { get; }
        internal bool Active { get; }
        internal bool SameTurn { get; }
        internal string? SourceRef { get; }
        internal string? RequiredApplicationAuthority { get; }
        internal IReadOnlyList<string> SatisfiedPredicates => Array.AsReadOnly(_predicates);
        internal JsonObject ReadDefinition() => JsonNode.Parse(_definitionJson)!.AsObject();
    }

    internal sealed class EffectDraftApplicationReceipt
    {
        private readonly EffectDraftCarrierEdit[] _carrierEdits;
        private readonly EffectIdentityWriteReceipt[] _identityWrites;
        private readonly EffectIdentityAllocationReceipt[] _allocations;
        private readonly string? _createdEffect;
        private readonly string? _createdIdentity;
        private readonly string? _parameters;

        internal EffectDraftApplicationReceipt(
            string disposition, string effectId, string transitionId,
            string eventRef, string? createdEventRef, string causalEventRef,
            EffectDraftSourceBinding source, EffectTargetKey target,
            EffectCarrierCoordinate carrier, string? createdEffect, string? createdIdentity,
            EffectReplayIdentity? resultIdentity, EffectReplayIdentity? replacedIdentity,
            string? skillScopeFingerprint, string? parameters,
            EffectDraftApplicationProvenanceKind provenanceKind, string? producerEffectId,
            IReadOnlyList<EffectDraftCarrierEdit> carrierEdits,
            IReadOnlyList<EffectIdentityWriteReceipt> identityWrites,
            IReadOnlyList<EffectIdentityAllocationReceipt> allocations)
        {
            Disposition = disposition;
            EffectId = effectId;
            TransitionId = transitionId;
            EventRef = eventRef;
            CreatedEventRef = createdEventRef;
            CausalEventRef = causalEventRef;
            Source = source;
            Target = target;
            Carrier = carrier;
            _createdEffect = createdEffect;
            _createdIdentity = createdIdentity;
            ResultIdentity = resultIdentity;
            ReplacedIdentity = replacedIdentity;
            SkillScopeFingerprint = skillScopeFingerprint;
            _parameters = parameters;
            ProvenanceKind = provenanceKind;
            ProducerEffectId = producerEffectId;
            _carrierEdits = carrierEdits.ToArray();
            _identityWrites = identityWrites.ToArray();
            _allocations = allocations.ToArray();
        }

        internal string Disposition { get; }
        internal string EffectId { get; }
        internal string TransitionId { get; }
        internal string EventRef { get; }
        internal string? CreatedEventRef { get; }
        internal string CausalEventRef { get; }
        internal EffectDraftSourceBinding Source { get; }
        internal EffectTargetKey Target { get; }
        internal EffectCarrierCoordinate Carrier { get; }
        internal EffectReplayIdentity? ResultIdentity { get; }
        internal EffectReplayIdentity? ReplacedIdentity { get; }
        internal string? SkillScopeFingerprint { get; }
        internal EffectDraftApplicationProvenanceKind ProvenanceKind { get; }
        internal string? ProducerEffectId { get; }
        internal JsonObject? ReadParameters() => _parameters == null
            ? null : JsonNode.Parse(_parameters)!.AsObject();
        internal IReadOnlyList<EffectDraftCarrierEdit> CarrierEdits => Array.AsReadOnly(_carrierEdits);
        internal IReadOnlyList<EffectIdentityWriteReceipt> IdentityWrites => Array.AsReadOnly(_identityWrites);
        internal IReadOnlyList<EffectIdentityAllocationReceipt> Allocations => Array.AsReadOnly(_allocations);
        internal JsonObject? ReadCreatedEffect() => _createdEffect == null
            ? null : JsonNode.Parse(_createdEffect)!.AsObject();
        internal JsonObject? ReadCreatedIdentity() => _createdIdentity == null
            ? null : JsonNode.Parse(_createdIdentity)!.AsObject();
    }

    internal sealed class EffectDraftPhaseReceipt
    {
        internal EffectDraftPhaseReceipt(
            EffectDraftPhase phase,
            IReadOnlyList<EffectDraftCarrierEdit> carrierEdits,
            IReadOnlyList<EffectDraftApplicationReceipt> applications,
            IReadOnlyList<EffectIdentityWriteReceipt> writes,
            IReadOnlyList<EffectIdentityAllocationReceipt> allocations,
            IReadOnlyList<EffectIdentityReplacementAgreement> agreements,
            IEnumerable<string> processedEventRefs,
            IEnumerable<EffectSourceAuthorityEntry> addedSources,
            IEnumerable<EffectTargetKey> addedTargets,
            string? skillScopeFingerprint)
        {
            Phase = phase;
            CarrierEdits = Array.AsReadOnly(carrierEdits.ToArray());
            Applications = Array.AsReadOnly(applications.ToArray());
            IdentityWrites = Array.AsReadOnly(writes.ToArray());
            Allocations = Array.AsReadOnly(allocations.ToArray());
            ReplacementAgreements = Array.AsReadOnly(agreements.ToArray());
            ProcessedEventRefs = Array.AsReadOnly(
                processedEventRefs.OrderBy(value => value, StringComparer.Ordinal).ToArray());
            AddedSources = Array.AsReadOnly(addedSources.Select(value => new EffectDraftSourceBinding(value)).ToArray());
            AddedTargets = Array.AsReadOnly(addedTargets.ToArray());
            SkillScopeFingerprint = skillScopeFingerprint;
        }

        internal EffectDraftPhase Phase { get; }
        internal IReadOnlyList<EffectDraftCarrierEdit> CarrierEdits { get; }
        internal IReadOnlyList<EffectDraftApplicationReceipt> Applications { get; }
        internal IReadOnlyList<EffectIdentityWriteReceipt> IdentityWrites { get; }
        internal IReadOnlyList<EffectIdentityAllocationReceipt> Allocations { get; }
        internal IReadOnlyList<EffectIdentityReplacementAgreement> ReplacementAgreements { get; }
        internal IReadOnlyList<string> ProcessedEventRefs { get; }
        internal IReadOnlyList<EffectDraftSourceBinding> AddedSources { get; }
        internal IReadOnlyList<EffectTargetKey> AddedTargets { get; }
        internal string? SkillScopeFingerprint { get; }
    }
}
