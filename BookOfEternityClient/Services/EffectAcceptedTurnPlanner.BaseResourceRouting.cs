namespace BookOfEternityClient.Services;

internal static partial class EffectAcceptedTurnPlanner
{
    // Immutable-base routing data only. This owns no draft or completion authority.
    internal sealed partial class BaseResourceRouting
    {
        private readonly EffectAcceptedTurnPlan _plan;
        private readonly ResourceOwnerAuthority _owners;
        private readonly ResourceDefinitionCatalog _definitions;
        private readonly IReadOnlyList<CanonicalEffectUseSeed> _seeds;
        private WoundRoutingPreparation? _currentWoundRouting;
        private EffectAcceptedDraft.EffectDraftWoundInsertion? _currentWoundInsertion;
        private readonly Dictionary<string, CanonicalEffectUseSeed> _registeredSeeds = new(StringComparer.Ordinal);

        private BaseResourceRouting(
            EffectAcceptedTurnPlan plan,
            ResourceOwnerAuthority owners,
            ResourceDefinitionCatalog definitions)
        {
            _plan = plan;
            _owners = owners;
            _definitions = definitions;
            _seeds = plan.ResourceTriggerIndex.CanonicalUseSeeds;
            foreach (var seed in _seeds)
                _registeredSeeds.Add(seed.EffectId, seed);
        }

        internal static BaseResourceRouting Capture(
            EffectAcceptedTurnPlan plan,
            ResourceOwnerAuthority owners,
            ResourceDefinitionCatalog definitions)
        {
            ArgumentNullException.ThrowIfNull(plan);
            ArgumentNullException.ThrowIfNull(owners);
            ArgumentNullException.ThrowIfNull(definitions);
            if (plan.IsAcceptedBoundaryComplete)
                throw new ArgumentException("Live base routing requires the original uncompleted effect plan.", nameof(plan));
            if (plan.ResourceTriggerIndex.Issues.Count != 0)
                throw new ArgumentException("Live base routing requires the valid retained effect index.", nameof(plan));
            return new BaseResourceRouting(plan, owners, definitions);
        }

        internal EffectAcceptedPlanAuthorityStamp PlanAuthority =>
            AcceptedMechanicsPlanner.CreateEffectPlanAuthority(_plan);

        internal IReadOnlyList<CanonicalEffectUseSeed> CanonicalUseSeeds =>
            Array.AsReadOnly(_seeds.ToArray());

        /// <summary>Reads original routing identity and use-registration DATA without initializing mechanics or another epoch.</summary>
        /// <returns>A detached diagnostic image, rejecting any installed wound routing or insertion.</returns>
        internal string ReadExistingPressureOrdinaryDiagnosticJson()
        {
            if (_currentWoundRouting != null || _currentWoundInsertion != null)
                throw new InvalidOperationException("Existing-pressure diagnostics require the original routing epoch.");
            return System.Text.Json.JsonSerializer.Serialize(new
            {
                PlanAuthority,
                Sources = AcceptedEffectBoundaryTranscript.OrderDiagnosticValues(_plan.Sources),
                Targets = AcceptedEffectBoundaryTranscript.OrderDiagnosticValues(_plan.Targets),
                WoundGroups = AcceptedEffectBoundaryTranscript.OrderDiagnosticValues(
                    _plan.SourceAuthority.SnapshotWoundGroupAuthorities().Select(group => new
                    {
                        group.Key, group.Owner, group.Target, group.SameTurn, group.SourceRef,
                        group.PreparedSourceExportFingerprint, group.GraphAuthorityFingerprint,
                        DefinitionKeys = group.Definitions.Select(value => value.DefinitionKey)
                            .OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                        ApplicationRoots = AcceptedEffectBoundaryTranscript.OrderDiagnosticValues(group.ApplicationRootLineage),
                        ExistingRoots = AcceptedEffectBoundaryTranscript.OrderDiagnosticValues(group.ExistingRootLineage)
                    })),
                CanonicalUseSeeds = _seeds.OrderBy(value => value.EffectId, StringComparer.Ordinal).ToArray(),
                RegisteredUseSeeds = _registeredSeeds.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => new { EffectId = pair.Key, Seed = pair.Value }).ToArray()
            });
        }

        /// <summary>Reads the consistent original or installed routing DATA without preparing or initializing a view.</summary>
        /// <returns>Detached full routing images; a mismatched installed pair refuses without changing routing.</returns>
        internal string ReadClosedPressureComparisonDiagnosticJson()
        {
            if ((_currentWoundRouting == null) != (_currentWoundInsertion == null) ||
                _currentWoundInsertion != null && !HasInstalled(_currentWoundInsertion))
                throw new InvalidOperationException("Diagnostics require a consistent original or installed routing pair.");
            var sources = _currentWoundRouting?.Sources ?? _plan.SourceAuthority;
            return System.Text.Json.JsonSerializer.Serialize(new
            {
                PlanAuthority, _plan.InputFingerprint, _plan.CarrierAuthorityFingerprint,
                _plan.SourceAuthorityFingerprint, _plan.TargetAuthorityFingerprint,
                OriginalSources = _plan.Sources, OriginalSourceBindings = _plan.SourceBindings,
                Targets = _plan.Targets, OriginalRoots = _plan.WoundApplicationRootEffectBindings,
                OriginalCarriers = _plan.ResourceTriggerCarriers, OriginalIdentity = _plan.IdentityIndexAfterImage,
                CurrentSourcesFingerprint = sources.Fingerprint,
                CurrentSourcesCanonicalFingerprint = sources.CanonicalFingerprint,
                CurrentWoundGroups = ReadClosedPressureWoundGroupsDiagnosticData(sources),
                CurrentRoots = _currentWoundRouting?.Roots ?? _plan.WoundApplicationRootEffectBindings,
                InstalledVersion = _currentWoundInsertion?.VersionAfter,
                InstalledInterval = _currentWoundInsertion?.Interval is not { } interval ? null : new
                {
                    interval.ConflictId, interval.ExchangeId, interval.Ordinal
                },
                CurrentState = ReadClosedPressureWoundDiagnosticData(_currentWoundRouting?.State),
                OriginalMechanics = ReadClosedPressureMechanicsDiagnosticData(_originalMechanics),
                InstalledMechanics = ReadClosedPressureMechanicsDiagnosticData(_currentWoundRouting?.Mechanics),
                OriginalUseSeeds = _seeds.OrderBy(seed => seed.EffectId, StringComparer.Ordinal).ToArray(),
                CurrentUseSeeds = (_currentWoundRouting?.Index.CanonicalUseSeeds ?? _seeds)
                    .OrderBy(seed => seed.EffectId, StringComparer.Ordinal).ToArray(),
                RegisteredUseSeeds = _registeredSeeds.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => new { EffectId = pair.Key, Seed = pair.Value }).ToArray(),
                Preparations = _woundPreparations.Values.OrderBy(preparation => preparation.Insertion.VersionAfter).Select(preparation => new
                {
                    preparation.Insertion.VersionAfter,
                    State = ReadClosedPressureWoundDiagnosticData(preparation.State),
                    SourcesFingerprint = preparation.Sources.Fingerprint,
                    SourcesCanonicalFingerprint = preparation.Sources.CanonicalFingerprint,
                    WoundGroups = ReadClosedPressureWoundGroupsDiagnosticData(preparation.Sources),
                    preparation.Roots,
                    Mechanics = ReadClosedPressureMechanicsDiagnosticData(preparation.Mechanics),
                    UseSeeds = preparation.Index.CanonicalUseSeeds.OrderBy(seed => seed.EffectId, StringComparer.Ordinal).ToArray()
                }).ToArray()
            });
        }

        /// <summary>Projects complete current source definitions and root lineages from already retained groups.</summary>
        /// <param name="sources">Current immutable source catalog.</param>
        /// <returns>Plain group definitions, owner coordinates and root lineages.</returns>
        private static object[] ReadClosedPressureWoundGroupsDiagnosticData(EffectSourceAuthority sources) =>
            AcceptedEffectBoundaryTranscript.OrderDiagnosticValues(sources.SnapshotWoundGroupAuthorities().Select(group => new
            {
                group.Key, group.Owner, group.Target, group.SameTurn, group.SourceRef,
                group.PreparedSourceExportFingerprint, group.GraphAuthorityFingerprint,
                Definitions = group.Definitions.Select(definition => new { definition.DefinitionKey, definition.Definition }).ToArray(),
                ApplicationRoots = AcceptedEffectBoundaryTranscript.OrderDiagnosticValues(group.ApplicationRootLineage),
                ExistingRoots = AcceptedEffectBoundaryTranscript.OrderDiagnosticValues(group.ExistingRootLineage)
            })).Cast<object>().ToArray();

        /// <summary>Preserves absent memoized mechanics separately from a retained empty projection.</summary>
        /// <param name="mechanics">Existing mechanics DATA, or null before memoization.</param>
        /// <returns>Plain complete mechanics facts, preserving absence.</returns>
        private static object? ReadClosedPressureMechanicsDiagnosticData(EffectMechanicsSnapshot? mechanics) => mechanics == null ? null : new
        {
            mechanics.IsAccepted,
            Components = mechanics.Components.Select(component => new { Component = component, component.Source }).ToArray(),
            mechanics.Audit, mechanics.Issues, mechanics.Effects,
            FateShieldReactionCandidates = mechanics.FateShieldReactionCandidates.Select(candidate => new
            {
                candidate.EffectId, candidate.TriggerId, candidate.CreatedAtTurn, candidate.AcceptedEffectFingerprint
            }).ToArray(),
            SkillScopeFingerprint = mechanics.SkillScopeAuthority.Fingerprint
        };

        internal bool OwnsContext(EffectAcceptedTurnPlan? plan, ResourceOwnerAuthority owners) =>
            ReferenceEquals(_plan, plan) && ReferenceEquals(_owners, owners);

        internal bool OwnsDefinitions(ResourceDefinitionCatalog definitions) =>
            ReferenceEquals(_definitions, definitions);

        /// <summary>Reads pressure-only conflict-carrier participation from this exact retained base plan.</summary>
        /// <returns>True when the bounded pressure projection must exclude the conflict carrier.</returns>
        internal bool HasPressureConflictCarrierCollision() =>
            _plan.HasPressureConflictCarrierCollision();

        internal EffectPeriodicResourceResolution ResolveInitial() =>
            ResolveDuePeriodicResourceMutations(_plan, _owners, _definitions);

        internal EffectPeriodicResourceResolution Resolve(
            ResourceAppliedEvent resourceEvent,
            ResourceOperationKey producer) =>
            ResolveResourceEventMutationCandidates(_plan, resourceEvent, producer, _owners, _definitions,
                _currentWoundRouting);

        /// <summary>
        /// Resolves only the exact inserted roots against a previously prepared future producer.
        /// </summary>
        /// <param name="resourceEvent">
        /// Potential event of the still-unfinished retained producer.
        /// </param>
        /// <param name="producer">
        /// Exact prepared producer key retained by the resource owner.
        /// </param>
        /// <param name="insertion">
        /// Actual currently installed insertion; stale and copied insertions are rejected.
        /// </param>
        /// <returns>
        /// New root candidates and their genuine resource consequences, without repeating old candidates.
        /// </returns>
        internal EffectPeriodicResourceResolution ResolveInsertedWoundRoots(ResourceAppliedEvent resourceEvent,
            ResourceOperationKey producer, EffectAcceptedDraft.EffectDraftWoundInsertion insertion)
        {
            if (!ReferenceEquals(_currentWoundInsertion, insertion))
                throw new InvalidOperationException("Only the exact installed wound can extend future candidates.");
            return ResolveResourceEventMutationCandidates(_plan, resourceEvent, producer, _owners, _definitions,
                _currentWoundRouting, insertion.Applications.Select(value => value.EffectId).ToHashSet(StringComparer.Ordinal));
        }

        internal IReadOnlyList<ValidationIssue> ValidateCandidateSeeds(
            IReadOnlyList<EffectResourceTriggerCandidate> candidates)
        {
            var seeds = _registeredSeeds;
            var issues = new List<ValidationIssue>();
            foreach (var candidate in candidates)
            {
                if (seeds.TryGetValue(candidate.Activation.Identity.EffectId, out var seed)
                    ? candidate.UseSeed != seed
                    : candidate.UseSeed != null || candidate.Activation.ConsumesUse)
                {
                    ResourceMaterializationContract.AddIssue(issues,
                        ResourceMaterializationContract.CommandPath,
                        "effect_live_base_use_seed_mismatch",
                        "the exact original indexed canonical use seed",
                        candidate.Activation.Identity.EffectId);
                }
            }
            return issues;
        }
    }
}
