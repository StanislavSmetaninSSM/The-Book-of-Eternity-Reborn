using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Configuration;

namespace BookOfEternityClient.Services;

internal static partial class WoundAcceptedTurnPlanner
{
    private static readonly object JointResourcePublicationIssuer = new();

    // The distinct joint family holds the actual common baseline. It cannot be
    // passed as a singleton treatment continuation or a public mixed permission.
    private sealed class JointResourcePublicationAuthority : IResourceRegisteredSystemOutcomeDraft,
        IResourceRegisteredSystemCapacityDraft
    {
        private readonly MortalWoundTreatmentResourcePublicationDraft _resources;
        private readonly TreatmentItemConsumptionContribution? _items;
        private readonly ResourceDefinitionCatalog _definitions;
        private readonly ResourceStateLedger _state;
        private readonly ResourceHistoryState _history;
        private readonly ResourceOwnerAuthority _baselineOwners;
        private readonly Dictionary<string, JsonNode?> _finalItemRoots;
        private readonly Dictionary<string, JsonObject> _companions;
        private readonly string _identitySeed;
        internal JointResourcePublicationAuthority(object issuer, JointCommonBaselineAuthority baseline,
            ResourceDefinitionCatalog definitions, ResourceStateLedger state, ResourceHistoryState history,
            ResourceOwnerAuthority baselineOwners, ResourceOwnerAuthority finalOwners, string identitySeed,
            MortalWoundTreatmentResourcePublicationDraft resources, TreatmentItemConsumptionContribution? items,
            IReadOnlyDictionary<string, JsonNode?> finalItemRoots, IReadOnlyDictionary<string, JsonObject> companions,
            string effectBefore, string effectAfter)
        {
            if (!ReferenceEquals(issuer, JointResourcePublicationIssuer))
                throw new InvalidOperationException("Joint resources require the private union issuer.");
            Baseline = baseline; _definitions = definitions; _state = state; _history = history;
            _baselineOwners = baselineOwners; FinalOwners = finalOwners; _identitySeed = identitySeed;
            _resources = resources; _items = items;
            _finalItemRoots = MortalItemAcceptedTurnNormalizationSnapshot.CloneRoots(finalItemRoots);
            _companions = companions.ToDictionary(row => row.Key, row => row.Value.DeepClone().AsObject(), StringComparer.Ordinal);
            EffectBeforeFingerprint = effectBefore; EffectAfterFingerprint = effectAfter;
            Fingerprint = ComputeFingerprint();
        }
        internal JointCommonBaselineAuthority Baseline { get; }
        internal ResourceOwnerAuthority FinalOwners { get; }
        internal string EffectBeforeFingerprint { get; }
        internal string EffectAfterFingerprint { get; }
        public string Fingerprint { get; }
        public IReadOnlyList<ResourceMutationSourceExport> SourceExports => _resources.SourceExports;
        public IReadOnlyList<ResourceMutationIntent> Mutations => _resources.Mutations;
        public IReadOnlyDictionary<string, CanonicalBeforeImage> ExpectedBeforeImages => _resources.ExpectedBeforeImages;
        public IReadOnlyList<ResourceCapacityIntent> CapacityTransitions => _items?.Consumption.CapacityTransitions.ToArray()
            ?? Array.Empty<ResourceCapacityIntent>();
        internal ResourceOwnerKey[] TerminalOwners => _items?.Consumption.TerminalOwners.ToArray() ?? Array.Empty<ResourceOwnerKey>();
        internal AcceptedMechanicsIdentityFactory CreateIdentityFactory() =>
            MortalWoundTreatmentResourcePublicationAuthority.CreateRegisteredIdentityFactory(_identitySeed);
        internal IReadOnlyDictionary<string, JsonNode?> ExactFinalItemRoots =>
            MortalItemAcceptedTurnNormalizationSnapshot.CloneRoots(_finalItemRoots);
        private string ComputeFingerprint()
        {
            var fields = new List<string?> { "book_of_eternity.mortal_joint_repair.resource_publication.v1",
                Baseline.Fingerprint, WoundAcceptedTurnFingerprintWriter.CanonicalJson(_definitions.ToCanonicalRoot()), _state.Fingerprint, _history.Fingerprint,
                _baselineOwners.Fingerprint, FinalOwners.Fingerprint, _identitySeed, _resources.Fingerprint,
                _items?.Consumption.Fingerprint, EffectBeforeFingerprint, EffectAfterFingerprint };
            foreach (var row in _finalItemRoots.OrderBy(row => row.Key, StringComparer.Ordinal))
            { fields.Add(row.Key); fields.Add(WoundAcceptedTurnFingerprintWriter.CanonicalJson(row.Value)); }
            foreach (var row in _companions.OrderBy(row => row.Key, StringComparer.Ordinal))
            { fields.Add(row.Key); fields.Add(WoundAcceptedTurnFingerprintWriter.CanonicalJson(row.Value)); }
            return WoundAcceptedTurnFingerprintWriter.Compute(fields);
        }
        internal bool HasValidSeal()
        {
            if (!Baseline.HasValidSeal || !_resources.HasValidSeal() || Fingerprint != ComputeFingerprint() ||
                !TryReadJointRepair(Baseline.Continuation, out var joint) ||
                MortalWoundTreatmentResourceComposer.Finalize(joint.Resolution).Finalization?.FinalizationFingerprint != joint.Finalization.FinalizationFingerprint)
                return false;
            if (_items is not null)
            {
                var replay = MortalItemConsumptionPlanner.Plan(_items.Input);
                if (!replay.IsValid || replay.Fingerprint != _items.Consumption.Fingerprint ||
                    !MortalWoundTreatmentItemOwnerSuccessor.IsExactSuccessor(_baselineOwners, TerminalOwners, FinalOwners)) return false;
            }
            else if (FinalOwners.Fingerprint != _baselineOwners.Fingerprint) return false;
            var sources = MortalItemAcceptedTurnNormalizationSnapshot.CloneRoots(joint.ReadEffectSourceRoots());
            var before = EffectAcceptedTurnInputComposer.BuildCanonicalSourceAuthority(sources, Baseline.Bundle.PreparedPlan);
            foreach (var path in EffectAcceptedTurnInputComposer.SourceAuthorityPaths)
                if (_finalItemRoots.TryGetValue(path, out var root)) sources[path] = root?.DeepClone();
            var after = EffectAcceptedTurnInputComposer.BuildCanonicalSourceAuthority(sources, Baseline.Bundle.PreparedPlan);
            return before.Issues.Count == 0 && after.Issues.Count == 0 &&
                before.CanonicalFingerprint == EffectBeforeFingerprint && after.CanonicalFingerprint == EffectAfterFingerprint;
        }
        public ResourceRegisteredSystemOutcomeProjectionResult Project(AcceptedMechanicsResourcePlanningResult result)
        {
            if (!HasValidSeal()) return new(new Dictionary<string, JsonObject>(StringComparer.Ordinal),
                Array.Empty<AcceptedMechanicsOwnerTransition>(), JointPreparationFailure("resource_seal_invalid", "Changed joint resource authority.").Issues);
            var projected = _resources.Project(result);
            var issues = projected.Issues.Concat(_items is null ? Array.Empty<ValidationIssue>() :
                MortalWoundTreatmentItemPublicationAuthority.ValidateConsumptionResourceProjection(result,
                    _items.Consumption.CapacityTransitions.ToArray(), _items.Input, _identitySeed, TerminalOwners)).ToArray();
            return new(_companions.ToDictionary(row => row.Key, row => row.Value.DeepClone().AsObject(), StringComparer.Ordinal),
                projected.OwnerTransitions, issues);
        }
        internal IReadOnlyList<ValidationIssue> ValidateCandidate(AcceptedMechanicsPlan candidate)
        {
            if (!HasValidSeal() || candidate.WoundStageBundle?.BundleFingerprint != Baseline.Bundle.BundleFingerprint ||
                candidate.OwnerAuthority.Fingerprint != FinalOwners.Fingerprint)
                return JointPreparationFailure("resource_candidate_invalid", "Changed joint resource candidate owner.").Issues;
            foreach (var row in ExpectedBeforeImages)
                if (!candidate.BeforeImages.TryGetValue(row.Key, out var image) || image.Existed != row.Value.Existed ||
                    !(image.Bytes ?? Array.Empty<byte>()).AsSpan().SequenceEqual(row.Value.Bytes ?? Array.Empty<byte>()))
                    return JointPreparationFailure("resource_candidate_before_image_changed", row.Key).Issues;
            var companions = candidate.OwnerCompanionAfterImages;
            foreach (var row in _companions)
                if (!companions.TryGetValue(row.Key, out var actual) || !JsonNode.DeepEquals(row.Value, actual) ||
                    !candidate.TouchedPaths.Contains(row.Key, StringComparer.Ordinal))
                    return JointPreparationFailure("resource_candidate_item_projection_changed", row.Key).Issues;
            return _resources.ValidateCandidate(candidate);
        }
    }

    internal sealed record JointResourcePublicationBuildResult(object? Authority, IReadOnlyList<ValidationIssue> Issues)
    { internal bool IsValid => Authority is not null && Issues.Count == 0; }

    internal static async Task<JointResourcePublicationBuildResult> CreateJointRepairResourcePublicationAsync(
        FileSystemManager files, FileSystemManager.CanonicalWriteLease lease, object commonBaseline)
    {
        try
        {
            await ValidateJointRepairCommonBaselineAsync(files, lease, commonBaseline);
            if (commonBaseline is not JointCommonBaselineAuthority baseline ||
                !TryReadJointRepair(baseline.Continuation, out var joint))
                throw new InvalidDataException("Joint resources require the exact private common baseline.");
            var guarded = baseline.Mechanical.BeforeImages;
            string? Read(string path) => guarded.TryGetValue(path, out var image) && image.Existed
                ? CanonicalJsonUtf8.DecodeOneOptionalBom(image.Bytes!) : null;
            var definitionResult = ResourceDefinitionCatalog.ParseCanonical(Read(ResourceMaterializationContract.DefinitionsPath), allowMissingPristine: false);
            var definitions = definitionResult.Catalog ?? throw new InvalidDataException("Joint resource definitions are invalid.");
            var stateResult = ResourceStateContract.ParseCanonical(Read(ResourceMaterializationContract.StatePath), definitions, allowMissingPristine: false);
            var historyResult = ResourceHistoryState.ParseCanonical(Read(ResourceMaterializationContract.HistoryPath), definitions, allowMissingPristine: false);
            var issues = definitionResult.Issues.Concat(stateResult.Issues).Concat(historyResult.Issues).ToList();
            if (stateResult.Ledger is not { } state || historyResult.History is not { } history)
                throw new InvalidDataException("Joint resource state/history are invalid.");
            issues.AddRange(history.ValidateStateAgreement(state));
            if (issues.Count != 0) return new(null, issues);
            var itemBaseline = baseline.Items.CloneFinalBaseline() ?? throw new InvalidDataException("Missing real sealed joint item baseline.");
            var ownerResult = await CanonicalResourceOwnerAuthorityComposer.ComposeAsync(definitions,
                path => Task.FromResult(itemBaseline.FinalCarrierRoots.TryGetValue(path, out var root) ? root?.ToJsonString() : Read(path)),
                state, history, CanonicalResourceOwnerAuthorityPurpose.FinalAfterImage);
            issues.AddRange(ownerResult.Issues);
            if (ownerResult.Authority is not { } owners || issues.Count != 0) return new(null, issues);
            if (!baseline.Skills.CommandEnvelope.IsEmpty || baseline.Skills.AfterImages.Count != 0 ||
                baseline.Skills.Baselines.Count != 0 || !baseline.ItemEnvelope.IsEmpty)
                throw new InvalidDataException("Joint current contour requires the owner-proved empty concurrent envelope.");
            var identitySeed = WoundAcceptedTurnFingerprintWriter.Compute(new string?[] {
                "book_of_eternity.mortal_joint_repair.resource_publication_identity.v1", baseline.Fingerprint,
                joint.Accepted.AcceptedStateFingerprint, joint.Request.RequestFingerprint, joint.Resolution.ResultFingerprint,
                joint.Finalization.FinalizationFingerprint, joint.Fingerprint });
            var claims = joint.Request.ResourceAuthority.Claims.ToDictionary(row => row.ClaimFingerprint, StringComparer.Ordinal);
            TreatmentItemConsumptionContribution? items = null;
            var finalRoots = MortalItemAcceptedTurnNormalizationSnapshot.CloneRoots(itemBaseline.FinalCarrierRoots);
            finalRoots[MortalItemIdentityState.StatePath] = itemBaseline.IdentityIndexAfterImage.DeepClone();
            var companions = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
            if (joint.Finalization.Consumptions.Any(row => row.Kind == "item_quantity"))
            {
                var calculated = CalculateTreatmentItemConsumption(joint.Accepted.AcceptedStateFingerprint,
                    joint.Request.RequestFingerprint, joint.Resolution.ResultFingerprint, joint.Resolution.Coordinates.Turn,
                    joint.Finalization, claims, definitions, state, owners, identitySeed, itemBaseline, finalRoots, baseline.Skills.AfterImages);
                if (calculated.Contribution is not { } contribution || calculated.Issues.Count != 0) return new(null, calculated.Issues);
                items = contribution; finalRoots = MortalItemAcceptedTurnNormalizationSnapshot.CloneRoots(contribution.FinalRoots);
                foreach (var row in contribution.PublicationAfterImages) companions.Add(row.Key, row.Value.DeepClone().AsObject());
            }
            var finalOwners = items?.FinalOwners ?? owners;
            // Include actual normalization successors in the same future common batch.
            // Exact array/object roots are retained separately for publication readback.
            foreach (var row in finalRoots)
            {
                if (row.Value is null || companions.ContainsKey(row.Key)) continue;
                var after = MortalItemProjectionRootParser.ToCarrierCatalogObject(row.Value, row.Key) ??
                    throw new InvalidDataException("Missing joint item publication root: " + row.Key);
                var before = Read(row.Key);
                var beforeNode = before is null ? null : StrictJsonAuthority.Deserialize<JsonNode>(before,
                    SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed, row.Key);
                if (!JsonNode.DeepEquals(beforeNode, row.Value)) companions.Add(row.Key, after.DeepClone().AsObject());
            }
            var contributionResources = CalculateTreatmentRegisteredResources(joint.Accepted.AcceptedStateFingerprint,
                joint.Request.RequestFingerprint, joint.Resolution.ResultFingerprint, joint.Resolution.Coordinates.Turn,
                joint.Fingerprint, joint.Finalization, claims, definitions, state, finalOwners, identitySeed);
            if (contributionResources.Issues.Count != 0) return new(null, contributionResources.Issues);
            var quartet = new[] { ResourceMaterializationContract.DefinitionsPath, ResourceMaterializationContract.StatePath,
                ResourceMaterializationContract.HistoryPath, CanonicalResourceOwnerAuthorityComposer.AuthorityPath }
                .ToDictionary(path => path, path => guarded.TryGetValue(path, out var image) ? image :
                    throw new InvalidDataException("Joint quartet before-image missing: " + path), StringComparer.Ordinal);
            var resources = new MortalWoundTreatmentResourcePublicationDraft(joint.Accepted.AcceptedStateFingerprint,
                joint.Request.RequestFingerprint, joint.Resolution.ResultFingerprint, joint.Finalization.FinalizationFingerprint,
                contributionResources.Sources, contributionResources.Mutations, contributionResources.ExpectedTransitions, quartet, null);
            var effectSources = MortalItemAcceptedTurnNormalizationSnapshot.CloneRoots(joint.ReadEffectSourceRoots());
            var effectBefore = EffectAcceptedTurnInputComposer.BuildCanonicalSourceAuthority(effectSources, baseline.Bundle.PreparedPlan);
            foreach (var path in EffectAcceptedTurnInputComposer.SourceAuthorityPaths)
                if (finalRoots.TryGetValue(path, out var root)) effectSources[path] = root?.DeepClone();
            var effectAfter = EffectAcceptedTurnInputComposer.BuildCanonicalSourceAuthority(effectSources, baseline.Bundle.PreparedPlan);
            issues.AddRange(effectBefore.Issues); issues.AddRange(effectAfter.Issues);
            if (issues.Count != 0) return new(null, issues);
            if (effectBefore.CanonicalFingerprint != baseline.Bundle.EffectBatchPlan.EffectPlan.SourceAuthorityFingerprint)
                throw new InvalidDataException("Joint effect-source before-authority changed.");
            var authority = new JointResourcePublicationAuthority(JointResourcePublicationIssuer, baseline, definitions, state, history,
                owners, finalOwners, identitySeed, resources, items, finalRoots, companions,
                effectBefore.CanonicalFingerprint, effectAfter.CanonicalFingerprint);
            await ValidateJointRepairCommonBaselineAsync(files, lease, baseline);
            if (!authority.HasValidSeal()) throw new InvalidDataException("Joint registered contribution did not reproduce its seal.");
            return new(authority, Array.Empty<ValidationIssue>());
        }
        catch (Exception error) when (error is InvalidDataException or InvalidOperationException or ArgumentException or
            System.Text.Json.JsonException or IOException or OverflowException)
        { return new(null, JointPreparationFailure("resource_publication_invalid", error.Message).Issues); }
    }

    internal static async Task ValidateJointRepairResourcePublicationAsync(FileSystemManager files,
        FileSystemManager.CanonicalWriteLease lease, object value)
    {
        if (value is not JointResourcePublicationAuthority authority || !authority.HasValidSeal())
            throw new InvalidDataException("Invalid private joint resource publication authority.");
        await ValidateJointRepairCommonBaselineAsync(files, lease, authority.Baseline);
    }
}
