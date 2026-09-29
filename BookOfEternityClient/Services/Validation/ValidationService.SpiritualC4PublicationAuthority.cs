using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    internal sealed partial class SpiritualOriginalTurnCapture
    {
        /// <summary>
        /// Transfers an exact completed C3 plan to one common cache without retaining executable owners.
        /// Current publication inputs remain separate from original planning and signed rollback images.
        /// </summary>
        internal sealed class SpiritualC4PublicationAuthority
        {
            private readonly FileSystemManager _fileSystem;
            private readonly AcceptedMechanicsInput _input;
            private readonly MortalItemAcceptedTurnNormalizationSnapshot _mortalItemSnapshot;
            private readonly MortalLocationAcceptedTurnPlan? _mortalLocationPlan;
            private readonly EffectSourceAuthority? _liveWoundSources;
            private readonly IReadOnlyDictionary<string, CanonicalBeforeImage> _publicationInputs;
            private readonly IReadOnlyDictionary<string, CanonicalBeforeImage> _signedRollbackImages;
            private readonly object _gate = new();
            private object? _cacheOwner;
            private bool _revoked;

            /// <summary>
            /// Retains the exact immutable completion after its original capture proves every input.
            /// </summary>
            /// <param name="issuanceKey">
            /// Private original-capture key; other callers cannot issue publication authority.
            /// </param>
            /// <param name="fileSystem">
            /// Exact filesystem instance whose inputs were authenticated.
            /// </param>
            /// <param name="input">
            /// Original C3 input, including immutable before-images and receipt provenance.
            /// </param>
            /// <param name="plan">
            /// Exact common plan assembled without executing another reduction.
            /// </param>
            /// <param name="publicationInputs">
            /// Latest committed physical draft, private pair, snapshot bytes and immutable witnesses.
            /// </param>
            /// <param name="signedRollbackImages">
            /// Signed original bytes or absence for the complete normalizer and output footprint.
            /// </param>
            /// <param name="mortalItemSnapshot">
            /// Genuine detached item snapshot extracted from the live original owner's scoped cache.
            /// </param>
            /// <param name="mortalLocationPlan">
            /// Genuine completed location plan from the original scoped cache, or null without location work.
            /// </param>
            /// <param name="liveWoundCompletion">
            /// Exact immutable ordered wound insertion completion, or null when no live insertion occurred.
            /// </param>
            /// <param name="completedConflictValidation">
            /// Detached admitted exchange mechanics sealed by the completed original capture.
            /// </param>
            internal SpiritualC4PublicationAuthority(object issuanceKey, FileSystemManager fileSystem,
                AcceptedMechanicsInput input, AcceptedMechanicsPlan plan,
                IReadOnlyDictionary<string, CanonicalBeforeImage> publicationInputs,
                IReadOnlyDictionary<string, CanonicalBeforeImage> signedRollbackImages,
                MortalItemAcceptedTurnNormalizationSnapshot mortalItemSnapshot,
                MortalLocationAcceptedTurnPlan? mortalLocationPlan,
                SpiritualLiveWoundCompletion? liveWoundCompletion,
                SpiritualCompletedConflictValidation completedConflictValidation)
            {
                if (!ReferenceEquals(issuanceKey, C4IssuanceKey))
                    throw new InvalidOperationException("Only the completed original capture can transfer publication.");
                _fileSystem = fileSystem;
                _input = input;
                Plan = plan;
                _mortalItemSnapshot = mortalItemSnapshot.Clone();
                _mortalLocationPlan = CloneLocationPlan(mortalLocationPlan);
                LiveWoundCompletion = liveWoundCompletion;
                CompletedConflictValidation = completedConflictValidation;
                _publicationInputs = AcceptedMechanicsPlanBinding.CloneReadOnlyBeforeImages(publicationInputs);
                _signedRollbackImages = AcceptedMechanicsPlanBinding.CloneReadOnlyBeforeImages(signedRollbackImages);
                if (!HasValidSeal())
                    throw new InvalidOperationException("The transferred completion does not match its original input.");
                _liveWoundSources = liveWoundCompletion is null ? null :
                    EffectSourceAuthority.CaptureCompletedWoundPublication(liveWoundCompletion, plan.EffectPlan!);
            }

            /// <summary>
            /// Gets the exact immutable common plan registered and taken by the publication cache.
            /// </summary>
            internal AcceptedMechanicsPlan Plan { get; }

            /// <summary>
            /// Gets a detached original item normalization snapshot without retaining executable allocations.
            /// </summary>
            internal MortalItemAcceptedTurnNormalizationSnapshot MortalItemSnapshot => _mortalItemSnapshot.Clone();

            /// <summary>
            /// Gets detached completed location outputs with their original permanent identities, or null without location work.
            /// </summary>
            internal MortalLocationAcceptedTurnPlan? MortalLocationPlan => CloneLocationPlan(_mortalLocationPlan);

            /// <summary>
            /// Gets the exact immutable ordered insertion evidence, or null when no live insertion occurred.
            /// </summary>
            internal SpiritualLiveWoundCompletion? LiveWoundCompletion { get; }

            /// <summary>
            /// Gets detached causal comparisons that become usable only after successful cache settlement.
            /// </summary>
            internal SpiritualCompletedConflictValidation CompletedConflictValidation { get; }

            /// <summary>
            /// Enables detached validation evidence only for this capability's exact successful cache owner.
            /// </summary>
            /// <param name="cacheOwner">
            /// Private identity held by the cache settling its current publication receipt.
            /// </param>
            internal void MarkCompletedConflictValidationPublished(object cacheOwner)
            {
                lock (_gate)
                {
                    if (_revoked || !ReferenceEquals(_cacheOwner, cacheOwner) || !HasValidSeal())
                        throw new InvalidOperationException("Only the current publication cache can settle comparison evidence.");
                    CompletedConflictValidation.MarkPublished(C4IssuanceKey);
                }
            }

            /// <summary>
            /// Compares independently reconstructed canonical sources using the transferred completed wound lineage.
            /// </summary>
            /// <param name="canonicalSources">
            /// Source authority built from final composed and unrelated normalized canonical roots.
            /// </param>
            /// <param name="completedEffects">
            /// Exact effect plan retained by this publication.
            /// </param>
            /// <returns>
            /// Source comparison catalog without restoring any disposed execution authority.
            /// </returns>
            internal EffectSourceAuthority ComposePublicationSourceAuthority(
                EffectSourceAuthority canonicalSources, EffectAcceptedTurnPlan completedEffects)
            {
                if (!HasValidSeal() || !ReferenceEquals(Plan.EffectPlan, completedEffects))
                    throw new InvalidDataException("Completed source comparison requires its exact publication owner.");
                return _liveWoundSources is null ? canonicalSources :
                    canonicalSources.WithCompletedWoundPublication(_liveWoundSources);
            }

            /// <summary>
            /// Detaches all mutable roots and collections of an already completed location plan.
            /// </summary>
            /// <param name="plan">
            /// Original completed plan, or null when the original turn has no location work.
            /// </param>
            /// <returns>
            /// Detached plan carrying the same allocated identities and rewrites, or null for absent work.
            /// </returns>
            private static MortalLocationAcceptedTurnPlan? CloneLocationPlan(MortalLocationAcceptedTurnPlan? plan) =>
                plan is null ? null : new(
                    plan.FinalWorldMap.DeepClone().AsObject(), plan.FinalCurrentLocation?.DeepClone().AsObject(),
                    plan.FinalIdentityIndex.DeepClone().AsObject(), plan.FinalStorageContents.DeepClone().AsObject(),
                    plan.LocationIdsByInitialId.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
                    plan.LinkIdsByInitialId.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
                    plan.AcceptedStorageCoordinates.ToArray(), plan.GovernedRewrites.ToArray(),
                    plan.TouchedPaths.ToArray(), plan.RepairContexts.Select(context => context with
                    { RepairableFields = context.RepairableFields.ToArray() }).ToArray(),
                    plan.FinalBootstrapScaffold?.DeepClone().AsObject());

            /// <summary>
            /// Gets a detached original planning binding, distinct from current physical publication inputs.
            /// </summary>
            internal AcceptedMechanicsPlanBinding Binding => _input.CreateBinding();

            /// <summary>
            /// Gets detached exact physical inputs required before common publication begins.
            /// </summary>
            internal IReadOnlyDictionary<string, CanonicalBeforeImage> PublicationInputs =>
                AcceptedMechanicsPlanBinding.CloneReadOnlyBeforeImages(_publicationInputs);

            /// <summary>
            /// Gets detached signed original rollback images, including explicit absence.
            /// </summary>
            internal IReadOnlyDictionary<string, CanonicalBeforeImage> SignedRollbackImages =>
                AcceptedMechanicsPlanBinding.CloneReadOnlyBeforeImages(_signedRollbackImages);

            /// <summary>
            /// Rechecks the complete physical publication binding under the owning canonical lease.
            /// </summary>
            /// <param name="fileSystem">
            /// Exact filesystem that issued this capability; another instance is rejected.
            /// </param>
            /// <param name="lease">
            /// Current canonical lease for that filesystem.
            /// </param>
            /// <returns>
            /// Empty diagnostics only for an unchanged, live publication capability.
            /// </returns>
            internal async Task<IReadOnlyList<ValidationIssue>> ValidateCurrentInputsAsync(
                FileSystemManager fileSystem, FileSystemManager.CanonicalWriteLease lease)
            {
                fileSystem.EnsureCanonicalWriteLeaseActive(lease);
                if (!ReferenceEquals(_fileSystem, fileSystem) || !HasValidSeal())
                    return [C4Issue("spiritual_c4_publication_input_changed")];
                foreach (var pair in _publicationInputs.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                {
                    var bytes = await fileSystem.ReadFileBytesAsync(lease, pair.Key);
                    if (!SameExactImage(pair.Value, new CanonicalBeforeImage(bytes is not null, bytes)))
                        return [C4Issue("spiritual_c4_publication_input_changed", pair.Key)];
                }
                return HasValidSeal() ? Array.Empty<ValidationIssue>() :
                    [C4Issue("spiritual_c4_publication_input_changed")];
            }

            /// <summary>
            /// Matches the exact filesystem instance that authenticated this completion.
            /// </summary>
            /// <param name="fileSystem">
            /// Candidate filesystem, including same-root instances that must not inherit ownership.
            /// </param>
            /// <returns>
            /// True only for the original instance.
            /// </returns>
            internal bool IsOwnedBy(FileSystemManager fileSystem) =>
                ReferenceEquals(_fileSystem, fileSystem);

            /// <summary>
            /// Checks physical inputs synchronously at the cache's atomic take boundary.
            /// </summary>
            /// <param name="fileSystem">
            /// Owning filesystem instance.
            /// </param>
            /// <param name="lease">
            /// Active canonical lease held across cache registration or take.
            /// </param>
            /// <returns>
            /// True only while every exact input and the sealed completion remain current.
            /// </returns>
            internal bool CurrentInputsAgree(FileSystemManager fileSystem, FileSystemManager.CanonicalWriteLease lease)
            {
                fileSystem.EnsureCanonicalWriteLeaseActive(lease);
                if (!ReferenceEquals(_fileSystem, fileSystem) || !HasValidSeal())
                    return false;
                foreach (var pair in _publicationInputs)
                {
                    var bytes = fileSystem.ReadFileBytesSync(pair.Key);
                    if (!SameExactImage(pair.Value, new CanonicalBeforeImage(bytes is not null, bytes)))
                        return false;
                }
                return HasValidSeal();
            }

            /// <summary>
            /// Checks the owner-issued plan and original provenance without granting physical freshness.
            /// </summary>
            /// <returns>
            /// True only while the complete plan, receipt and original input still agree.
            /// </returns>
            internal bool HasValidSeal()
            {
                lock (_gate)
                    return !_revoked &&
                        (LiveWoundCompletion is null ? Plan.LiveWoundProofFingerprint is null :
                            LiveWoundCompletion.MatchesEffects(Plan.EffectPlan) &&
                            LiveWoundCompletion.ProofFingerprint == Plan.LiveWoundProofFingerprint) &&
                        _mortalItemSnapshot.SessionId == _input.SessionId &&
                        _mortalItemSnapshot.SnapshotToken == _input.SnapshotToken &&
                        _mortalItemSnapshot.Turn == _input.Turn &&
                        _mortalItemSnapshot.MatchesAcceptedOwnerAuthority(Plan.OwnerAuthority) &&
                        Plan.InputFingerprint ==
                        AcceptedMechanicsPlanFingerprints.ComputeInput(_input.CreateBinding()) &&
                        Plan.AuthorityFingerprints == _input.AuthorityFingerprints &&
                        Plan.PreparedPlanFingerprint == AcceptedMechanicsPlanFingerprints.ComputePrepared(Plan) &&
                        Plan.BeforeImages.Count == _input.BeforeImages.Count &&
                        Plan.BeforeImages.All(pair => _input.BeforeImages.TryGetValue(pair.Key, out var original) &&
                            SameExactImage(pair.Value, original)) &&
                        Plan.OwnerCompanionAfterImages.ContainsKey(SpiritualWoundOpportunityReceiptState.StatePath) &&
                        new[] { SpiritualWoundCaptureCheckpointState.StatePath,
                            SpiritualWoundDecisionPendingState.StatePath, AcceptedMechanicsPlan.WoundCommandPath }
                        .All(path => Plan.ConsumedPaths.Contains(path, StringComparer.Ordinal) &&
                            Plan.BeforeImages.ContainsKey(path) && _publicationInputs.ContainsKey(path) &&
                            _signedRollbackImages.ContainsKey(path));
            }

            /// <summary>
            /// Transfers this capability to one cache exactly once.
            /// </summary>
            /// <param name="cacheOwner">
            /// Private cache identity retained for the lifetime of this publication attempt.
            /// </param>
            /// <returns>
            /// True for the first live transfer; false after prior registration or revocation.
            /// </returns>
            internal bool TryBindCache(object cacheOwner)
            {
                lock (_gate)
                {
                    if (_revoked || _cacheOwner is not null || !HasValidSeal())
                        return false;
                    _cacheOwner = cacheOwner;
                    return true;
                }
            }

            /// <summary>
            /// Revokes the transferred capability when its owning cache closes or invalidates it.
            /// </summary>
            internal void Revoke()
            {
                lock (_gate) _revoked = true;
            }
        }

        /// <summary>
        /// Identifies one exact cache take; its identity and fence must remain owned by that cache.
        /// </summary>
        internal sealed class SpiritualC4PublicationReceipt
        {
            /// <summary>
            /// Retains the capability and cache fence selected by the dedicated one-shot take.
            /// </summary>
            /// <param name="authority">
            /// Exact owner-issued publication capability taken from the cache.
            /// </param>
            /// <param name="cacheOwner">
            /// Cache identity checked by every subsequent settlement operation.
            /// </param>
            /// <param name="fence">
            /// Cache generation at the moment the validated entry was taken.
            /// </param>
            internal SpiritualC4PublicationReceipt(SpiritualC4PublicationAuthority authority,
                object cacheOwner, object fence)
            {
                Authority = authority;
                CacheOwner = cacheOwner;
                Fence = fence;
            }

            /// <summary>
            /// Gets the exact transferred capability for this publication attempt.
            /// </summary>
            internal SpiritualC4PublicationAuthority Authority { get; }

            /// <summary>
            /// Gets the exact common plan consumed by the dedicated take.
            /// </summary>
            internal AcceptedMechanicsPlan Plan => Authority.Plan;

            /// <summary>
            /// Gets the private cache identity retained at take.
            /// </summary>
            internal object CacheOwner { get; }

            /// <summary>
            /// Gets the invalidation fence retained at take.
            /// </summary>
            internal object Fence { get; }
        }
    }
}
