using System.Collections.ObjectModel;
using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    internal sealed partial class SpiritualOriginalTurnCapture
    {
        /// <summary>
        /// Retains candidate images or the integrity failures that prevent their composition.
        /// </summary>
        /// <param name="Images">
        /// Complete detached candidate images, or <see langword="null"/> when owner state is invalid.
        /// </param>
        /// <param name="Issues">
        /// Wound or shared-carrier integrity failures; empty for a valid candidate.
        /// </param>
        private sealed record C1CandidateOwnerImagesResult(
            IReadOnlyDictionary<string, CanonicalBeforeImage>? Images,
            IReadOnlyList<ValidationIssue> Issues);

        /// <summary>
        /// Projects changed C1 candidate roots from the exact closed source, resource and effect owners.
        /// The result grants no authority to persist a pending packet or publish the turn.
        /// </summary>
        /// <param name="lease">
        /// Active canonical lease authenticating the capture and its retained physical inputs.
        /// </param>
        /// <param name="interval">
        /// Exact latest closed interval owned by this capture's resource executor.
        /// </param>
        /// <returns>
        /// Detached registered candidate images; C1 packet coverage remains the producer's responsibility.
        /// </returns>
        internal async Task<IReadOnlyDictionary<string, CanonicalBeforeImage>> ReadC1CandidateOwnerImagesAsync(
            FileSystemManager.CanonicalWriteLease lease,
            AcceptedMechanicsPlanner.SpiritualExchangeInterval interval)
        {
            ArgumentNullException.ThrowIfNull(interval);
            await _gate.WaitAsync();
            try
            {
                var result = await ReadC1CandidateOwnerImagesCoreAsync(lease, interval);
                return result.Images ?? throw new InvalidOperationException(
                    "The current wound and effect owners cannot be read.");
            }
            finally { _gate.Release(); }
        }

        /// <summary>
        /// Composes current C1 candidate images while the caller owns the capture gate.
        /// </summary>
        /// <param name="lease">
        /// Active canonical lease for retained input checks.
        /// </param>
        /// <param name="interval">
        /// Latest exact closed interval owned by this capture.
        /// </param>
        /// <returns>
        /// Detached owner-composed candidate images, or integrity issues without partial images.
        /// </returns>
        private async Task<C1CandidateOwnerImagesResult>
            ReadC1CandidateOwnerImagesCoreAsync(FileSystemManager.CanonicalWriteLease lease,
                AcceptedMechanicsPlanner.SpiritualExchangeInterval interval)
        {
                EnsureCurrent(lease);
                if (_closedExchangeEvidence.Count != _nextResourceOrdinal ||
                    _closedExchangeEvidence.Count == 0 ||
                    !ReferenceEquals(_closedExchangeEvidence[^1].Interval, interval) ||
                    _resources is null || !_resources.Owns(interval) ||
                    _effects is null || _originalOutputProjection is null)
                    throw new InvalidOperationException("The C1 candidate requires the current closed named capture.");
                var freshness = await CheckRetainedInputsAsync(lease);
                if (freshness.Count != 0)
                    throw new InvalidOperationException("The retained C1 candidate inputs changed.");
                EnsureCurrent(lease);
                var resource = _resources.ReadClosedSpiritualResourceCandidatePrefix(interval);
                var issues = new List<ValidationIssue>();
                var current = _effects.ReadCurrentWoundView(this, _resources, _source,
                    _effects.WoundReadVersion, issues);
                if (current?.WoundCarriers is not { } woundCarriers ||
                    current.EffectCarriers is not { } effectCarriers || issues.Count != 0)
                {
                    if (issues.Count == 0)
                        issues.Add(SourceIssue(AfterlifeEntityProfileState.StatePath,
                            "spiritual_first_offer_owner_state_invalid",
                            "complete current wound and effect owner state"));
                    return new(null, issues);
                }
                var shared = WoundAcceptedOwnerCarrierAuthority.Compose(woundCarriers, effectCarriers);
                if (!shared.Success || shared.Carriers is not { } carriers)
                    return new(null, shared.Issues.Count != 0 ? shared.Issues :
                        [SourceIssue(AfterlifeEntityProfileState.StatePath,
                            "spiritual_first_offer_owner_state_invalid",
                            "agreeing current shared wound and effect carriers")]);
                var conflict = SpiritualWoundCandidateOwnerComposer.MergeExecutedConflict(
                    _source.ReadExecutedConflictPrefix(), effectCarriers.SpiritualConflict, _terminal);
                var registered = _draftInputs.PathInventory
                    .Concat(WoundAcceptedTurnSnapshotContract.RequiredPaths)
                    .ToHashSet(StringComparer.Ordinal);
                var images = new Dictionary<string, CanonicalBeforeImage>(StringComparer.Ordinal);
                void Add(string path, string? json)
                {
                    if (!registered.Contains(path) || !images.TryAdd(path,
                            new CanonicalBeforeImage(json is not null,
                                json is null ? null : Encoding.UTF8.GetBytes(json))))
                        throw new InvalidOperationException(
                            $"The C1 candidate has an unregistered or duplicate path writer for '{path}'.");
                }
                void AddRoot(string path, JsonObject? root) => Add(path, root?.ToJsonString());

                Add(ResourceMaterializationContract.DefinitionsPath, resource.DefinitionsJson);
                Add(ResourceMaterializationContract.StatePath, resource.StateJson);
                Add(ResourceMaterializationContract.HistoryPath, resource.HistoryJson);
                Add(CanonicalResourceOwnerAuthorityComposer.AuthorityPath, resource.OwnerAuthorityJson);
                AddRoot(AfterlifeSpiritualConflictState.StatePath, conflict);
                AddRoot(EffectCarrierCatalog.PlayerPath, effectCarriers.PlayerEffects);
                AddRoot(EffectCarrierCatalog.NpcPath, effectCarriers.NpcEffects);
                AddRoot(EffectAcceptedTurnPlan.IdentityIndexPath, current.EffectIdentity);
                AddRoot(WoundCarrierCatalog.PlayerPath, carriers.PlayerWounds);
                AddRoot(WoundCarrierCatalog.NpcPath, carriers.NpcWounds);
                AddRoot(WoundCarrierCatalog.EnemiesPath, carriers.EnemyCombatants);
                AddRoot(WoundCarrierCatalog.AlliesPath, carriers.AllyCombatants);
                AddRoot(WoundCarrierCatalog.AfterlifeProfilesPath, carriers.AfterlifeProfiles);
                AddRoot(WoundIdentityState.StatePath, current.WoundIdentity);
                AddRoot(WoundHistoryState.HistoryPath, current.WoundHistory);
                foreach (var pair in resource.CompanionAfterImages.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                {
                    if (images.ContainsKey(pair.Key))
                    {
                        if (pair.Key == AfterlifeSpiritualConflictState.StatePath)
                        {
                            if (!JsonNode.DeepEquals(pair.Value,
                                    _source.ReadInitialFullCandidateConflict()))
                                throw new InvalidOperationException(
                                    "The conflict companion does not match the retained projected original draft.");
                            continue;
                        }
                        var effectRoot = pair.Key switch
                        {
                            EffectCarrierCatalog.EnemiesPath => effectCarriers.EnemyCombatants,
                            EffectCarrierCatalog.AlliesPath => effectCarriers.AllyCombatants,
                            EffectCarrierCatalog.AfterlifeProfilesPath => effectCarriers.AfterlifeProfiles,
                            _ => null
                        };
                        SpiritualWoundCandidateOwnerComposer.EnsureCompatibleSharedCompanion(
                            pair.Key, pair.Value, effectRoot);
                        continue;
                    }
                    AddRoot(pair.Key, pair.Value);
                }
                foreach (var group in resource.OwnerTransitions.GroupBy(transition => transition.Path,
                             StringComparer.Ordinal).OrderBy(group => group.Key, StringComparer.Ordinal))
                {
                    var original = _draftInputs.ReadImage(group.Key);
                    if (!original.Existed || images.ContainsKey(group.Key))
                        throw new InvalidOperationException("The typed C1 owner transition has no exclusive original root.");
                    var root = SpiritualWoundCandidateOwnerComposer.ProjectTypedOwnerRoot(
                        _draftInputs.ReadText(group.Key)!, group.ToArray());
                    AddRoot(group.Key, root);
                }
                Add(FixedOriginalOutputPaths[0], _originalOutputProjection.Narrative?.Json);
                Add(FixedOriginalOutputPaths[1], _originalOutputProjection.Interface?.Json);
                return new(new ReadOnlyDictionary<string, CanonicalBeforeImage>(images), []);
        }
    }
}
