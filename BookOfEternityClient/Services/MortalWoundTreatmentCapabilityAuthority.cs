using System.Collections.ObjectModel;
using System.Globalization;

namespace BookOfEternityClient.Services;

internal enum MortalWoundTreatmentCapabilityCatalogReadStatus
{
    Success,
    CoordinatesInvalid,
    PromotionRequired,
    SourceOwnerMismatch,
    SourceAmbiguous,
    SourceInvalid,
    PublicationMismatch
}

internal sealed class MortalWoundTreatmentCapabilityCatalogReadResult
{
    private readonly MortalWoundTreatmentCapabilitySkillSource[] _sources;

    internal MortalWoundTreatmentCapabilityCatalogReadResult(
        MortalWoundTreatmentCapabilityCatalogReadStatus status,
        IEnumerable<MortalWoundTreatmentCapabilitySkillSource>? sources = null)
    {
        Status = status;
        _sources = sources?.Select(
                MortalWoundTreatmentCapabilityDetachment.CloneSource)
            .ToArray() ?? Array.Empty<MortalWoundTreatmentCapabilitySkillSource>();
    }

    internal MortalWoundTreatmentCapabilityCatalogReadStatus Status { get; }

    internal IReadOnlyList<MortalWoundTreatmentCapabilitySkillSource> Sources =>
        Array.AsReadOnly(_sources.Select(
            MortalWoundTreatmentCapabilityDetachment.CloneSource).ToArray());
}

internal static class MortalWoundTreatmentCapabilityDetachment
{
    internal static MortalWoundTreatmentCapabilitySkillSource CloneSource(
        MortalWoundTreatmentCapabilitySkillSource source) => new(
        source.OwnerKind,
        source.OwnerId,
        source.SkillKind,
        source.SkillId,
        source.DisplayName,
        source.Lifecycle,
        source.Active,
        source.SourcePath,
        source.Capabilities.Select(capability => capability with
        {
            OperationLimits = CloneLimits(capability.OperationLimits)
        }).ToArray());

    internal static MortalWoundTreatmentCapabilityOperationLimits CloneLimits(
        MortalWoundTreatmentCapabilityOperationLimits limits) =>
        MortalWoundTreatmentCapabilityOperationLimits.Create(
            limits.MayStabilize,
            limits.MaximumRecoveryPoints,
            limits.MaximumSeverityReductionSteps,
            limits.RemovableComplicationKinds,
            limits.MayHealAtSeverityI,
            limits.MaximumCosmeticHealLegacies,
            limits.MaximumMechanicalEffectHealLegacies);
}

internal sealed class MortalWoundTreatmentCapabilityProofResult
{
    private readonly ReadOnlyCollection<ValidationIssue> _issues;

    private MortalWoundTreatmentCapabilityProofResult(
        bool isValid,
        IEnumerable<ValidationIssue> issues,
        MortalWoundTreatmentCapabilityProof? proof)
    {
        ArgumentNullException.ThrowIfNull(issues);
        IsValid = isValid;
        _issues = new ReadOnlyCollection<ValidationIssue>(issues.ToArray());
        Proof = proof;
    }

    public bool IsValid { get; }
    public IReadOnlyList<ValidationIssue> Issues => _issues;
    public MortalWoundTreatmentCapabilityProof? Proof { get; }

    internal static MortalWoundTreatmentCapabilityProofResult Valid(
        MortalWoundTreatmentCapabilityAuthority.ProofMintCapability mintCapability,
        MortalWoundTreatmentCapabilityProof proof)
    {
        MortalWoundTreatmentCapabilityAuthority.ProofMintCapability.RequireAuthority(
            mintCapability);
        ArgumentNullException.ThrowIfNull(proof);
        return new(true, Array.Empty<ValidationIssue>(), proof);
    }

    internal static MortalWoundTreatmentCapabilityProofResult Invalid(
        ValidationIssue issue) => new(false, new[] { issue }, null);
}

internal sealed class MortalWoundTreatmentCapabilityProof : MortalWoundTreatmentModeAuthority
{
    [System.Text.Json.Serialization.JsonConstructor]
    private MortalWoundTreatmentCapabilityProof(
        string snapshotToken,
        string sourcePath,
        string ownerKind,
        string ownerId,
        string skillKind,
        string skillId,
        string capabilityRef,
        string woundDomain,
        int minimumSeverityRank,
        int maximumSeverityRank,
        MortalWoundTreatmentCapabilityOperationLimits operationLimits,
        string contextFingerprint,
        string acceptedStateFingerprint,
        string coordinatesFingerprint,
        string sourceSemanticFingerprint,
        string proofFingerprint)
    {
        SnapshotToken = snapshotToken;
        SourcePath = sourcePath;
        OwnerKind = ownerKind;
        OwnerId = ownerId;
        SkillKind = skillKind;
        SkillId = skillId;
        CapabilityRef = capabilityRef;
        WoundDomain = woundDomain;
        MinimumSeverityRank = minimumSeverityRank;
        MaximumSeverityRank = maximumSeverityRank;
        OperationLimits = MortalWoundTreatmentCapabilityDetachment.CloneLimits(
            operationLimits);
        ContextFingerprint = contextFingerprint;
        AcceptedStateFingerprint = acceptedStateFingerprint;
        CoordinatesFingerprint = coordinatesFingerprint;
        SourceSemanticFingerprint = sourceSemanticFingerprint;
        ProofFingerprint = proofFingerprint;
    }

    public string SnapshotToken { get; }
    public string SourcePath { get; }
    public string OwnerKind { get; }
    public string OwnerId { get; }
    public string SkillKind { get; }
    public string SkillId { get; }
    public string CapabilityRef { get; }
    public string WoundDomain { get; }
    public int MinimumSeverityRank { get; }
    public int MaximumSeverityRank { get; }
    public MortalWoundTreatmentCapabilityOperationLimits OperationLimits { get; }
    public string ContextFingerprint { get; }
    public string AcceptedStateFingerprint { get; }
    public string CoordinatesFingerprint { get; }
    public string SourceSemanticFingerprint { get; }
    public string ProofFingerprint { get; }

    internal static MortalWoundTreatmentCapabilityProof Create(
        MortalWoundTreatmentCapabilityAuthority.ProofMintCapability mintCapability,
        MortalWoundTreatmentAcceptedStateAuthority acceptedState,
        MortalWoundTreatmentAttemptCoordinates coordinates,
        MortalWoundTreatmentCapabilitySkillSource source,
        MortalWoundTreatmentCapabilityDefinition capability)
    {
        MortalWoundTreatmentCapabilityAuthority.ProofMintCapability.RequireAuthority(
            mintCapability);
        var sourcePath = SourceRootPath(source);
        var limits = MortalWoundTreatmentCapabilityOperationLimits.Create(
            capability.OperationLimits.MayStabilize,
            capability.OperationLimits.MaximumRecoveryPoints,
            capability.OperationLimits.MaximumSeverityReductionSteps,
            capability.OperationLimits.RemovableComplicationKinds
                .OrderBy(static kind => kind, StringComparer.Ordinal)
                .ToArray(),
            capability.OperationLimits.MayHealAtSeverityI,
            capability.OperationLimits.MaximumCosmeticHealLegacies,
            capability.OperationLimits.MaximumMechanicalEffectHealLegacies);
        var sourceFingerprint = ComputeSourceFingerprint(
            sourcePath,
            source,
            capability,
            limits);
        var binding = acceptedState.Binding;
        var proofFields = ProofFields(
            binding.SnapshotToken,
            sourcePath,
            source,
            capability,
            limits,
            acceptedState.ContextFingerprint,
            acceptedState.AcceptedStateFingerprint,
            coordinates.CoordinatesFingerprint,
            sourceFingerprint);
        var proofFingerprint = WoundAcceptedTurnFingerprintWriter.Compute(proofFields);
        return new MortalWoundTreatmentCapabilityProof(
            binding.SnapshotToken,
            sourcePath,
            source.OwnerKind,
            source.OwnerId,
            source.SkillKind,
            source.SkillId,
            capability.CapabilityRef,
            capability.WoundDomain,
            capability.MinimumSeverityRank,
            capability.MaximumSeverityRank,
            limits,
            acceptedState.ContextFingerprint,
            acceptedState.AcceptedStateFingerprint,
            coordinates.CoordinatesFingerprint,
            sourceFingerprint,
            proofFingerprint);
    }

    private static string ComputeSourceFingerprint(
        string sourcePath,
        MortalWoundTreatmentCapabilitySkillSource source,
        MortalWoundTreatmentCapabilityDefinition capability,
        MortalWoundTreatmentCapabilityOperationLimits limits) =>
        WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
        {
            "book_of_eternity.mortal_wound_treatment.capability_source",
            "1",
            sourcePath,
            source.OwnerKind,
            source.OwnerId,
            source.SkillKind,
            source.SkillId,
            Number(capability.SchemaVersion),
            capability.CapabilityRef,
            capability.WoundDomain,
            Number(capability.MinimumSeverityRank),
            Number(capability.MaximumSeverityRank),
            source.Lifecycle,
            Boolean(source.Active),
            Boolean(limits.MayStabilize),
            Number(limits.MaximumRecoveryPoints),
            Number(limits.MaximumSeverityReductionSteps),
            Number(limits.RemovableComplicationKinds.Count),
            string.Join(",", limits.RemovableComplicationKinds),
            Boolean(limits.MayHealAtSeverityI),
            Number(limits.MaximumCosmeticHealLegacies),
            Number(limits.MaximumMechanicalEffectHealLegacies)
        });

    private static IEnumerable<string?> ProofFields(
        string snapshotToken,
        string sourcePath,
        MortalWoundTreatmentCapabilitySkillSource source,
        MortalWoundTreatmentCapabilityDefinition capability,
        MortalWoundTreatmentCapabilityOperationLimits limits,
        string contextFingerprint,
        string acceptedStateFingerprint,
        string coordinatesFingerprint,
        string sourceSemanticFingerprint) => new string?[]
    {
        "book_of_eternity.mortal_wound_treatment.capability_proof",
        "1",
        snapshotToken,
        sourcePath,
        source.OwnerKind,
        source.OwnerId,
        source.SkillKind,
        source.SkillId,
        capability.CapabilityRef,
        capability.WoundDomain,
        Number(capability.MinimumSeverityRank),
        Number(capability.MaximumSeverityRank),
        Boolean(limits.MayStabilize),
        Number(limits.MaximumRecoveryPoints),
        Number(limits.MaximumSeverityReductionSteps),
        Number(limits.RemovableComplicationKinds.Count),
        string.Join(",", limits.RemovableComplicationKinds),
        Boolean(limits.MayHealAtSeverityI),
        Number(limits.MaximumCosmeticHealLegacies),
        Number(limits.MaximumMechanicalEffectHealLegacies),
        contextFingerprint,
        acceptedStateFingerprint,
        coordinatesFingerprint,
        sourceSemanticFingerprint
    };

    private static string SourceRootPath(
        MortalWoundTreatmentCapabilitySkillSource source) =>
        source.OwnerKind switch
        {
            "npc" => "game_state/npcs/npc_core.json",
            "player" when source.SkillKind == "active" =>
                "game_state/player/skills_active.json",
            "player" when source.SkillKind == "passive" =>
                "game_state/player/skills_passive.json",
            _ => throw new InvalidOperationException(
                "A capability proof requires one canonical player or NPC skill root.")
        };

    private static string Number(int value) =>
        value.ToString(CultureInfo.InvariantCulture);

    private static string Boolean(bool value) => value ? "true" : "false";
}

internal static class MortalWoundTreatmentCapabilityAuthority
{
    private static readonly ProofMintCapability ProofMint = new();

    internal sealed class ProofMintCapability
    {
        internal ProofMintCapability()
        {
        }

        internal static void RequireAuthority(
            ProofMintCapability? mintCapability)
        {
            if (!ReferenceEquals(mintCapability, ProofMint))
            {
                throw new InvalidOperationException(
                    "A capability proof can only be minted by the canonical capability authority.");
            }
        }
    }

    internal static MortalWoundTreatmentCapabilityProofResult ExportCurrent(
        MortalWoundTreatmentAcceptedStateAuthority? acceptedState,
        MortalWoundTreatmentAttemptCoordinates? coordinates,
        string? capabilityRef,
        string? actorRole) =>
        Export(
            acceptedState,
            coordinates,
            capabilityRef,
            actorRole,
            publicationPlan: null,
            forPublication: false);

    internal static MortalWoundTreatmentCapabilityProofResult ExportForPublication(
        MortalWoundTreatmentAcceptedStateAuthority? acceptedState,
        MortalWoundTreatmentAttemptCoordinates? coordinates,
        string? capabilityRef,
        string? actorRole,
        AcceptedMechanicsPlan? publicationPlan) =>
        Export(
            acceptedState,
            coordinates,
            capabilityRef,
            actorRole,
            publicationPlan,
            forPublication: true);

    private static MortalWoundTreatmentCapabilityProofResult Export(
        MortalWoundTreatmentAcceptedStateAuthority? acceptedState,
        MortalWoundTreatmentAttemptCoordinates? coordinates,
        string? capabilityRef,
        string? actorRole,
        AcceptedMechanicsPlan? publicationPlan,
        bool forPublication)
    {
        if (acceptedState is null ||
            coordinates is null ||
            !coordinates.MatchesAcceptedState(acceptedState))
        {
            return Invalid(
                "treatmentCapability.actorRole",
                "mortal_wound_treatment_capability_binding_mismatch",
                "The capability export requires the current accepted state and its exact attempt coordinates.");
        }

        if (!ResourceMaterializationContract.IsExactIdentifier(capabilityRef) ||
            actorRole is not ("provider" or "target"))
        {
            return Invalid(
                "treatmentCapability.actorRole",
                "mortal_wound_treatment_capability_binding_mismatch",
                "The capability and actor role must be exact selected route coordinates.");
        }

        var routeMatches = acceptedState.TreatmentDefinition.Routes
            .Where(route => string.Equals(
                route.RouteId,
                coordinates.RouteId,
                StringComparison.Ordinal))
            .ToArray();
        if (routeMatches.Length != 1 ||
            routeMatches[0] is not MortalWoundGuaranteedRouteDefinition route ||
            !string.Equals(
                route.Resolution.CapabilityRef,
                capabilityRef,
                StringComparison.Ordinal) ||
            !string.Equals(
                route.Resolution.ActorRole,
                actorRole,
                StringComparison.Ordinal) ||
            route.Requirements.OfType<MortalWoundSourceCapabilityRequirement>().Count(
                requirement => string.Equals(
                                   requirement.CapabilityRef,
                                   capabilityRef,
                                   StringComparison.Ordinal) &&
                               string.Equals(
                                   requirement.ActorRole,
                                   actorRole,
                                   StringComparison.Ordinal)) != 1)
        {
            return Invalid(
                "treatmentCapability.actorRole",
                "mortal_wound_treatment_capability_binding_mismatch",
                "The selected guaranteed route, resolution, capability, and actor role must agree exactly.");
        }

        var (ownerKind, ownerId) = ActorCoordinate(coordinates, actorRole);
        if (actorRole == "target" &&
            ownerKind is "combatant" or "combatant_member")
        {
            return Invalid(
                "treatmentCapability.target",
                "mortal_wound_treatment_capability_actor_promotion_required",
                "A target-owned guarantee requires an existing canonical player or NPC promotion.");
        }
        if (ownerKind is not ("player" or "npc"))
        {
            return Invalid(
                "treatmentCapability.sourceOwner",
                "mortal_wound_treatment_capability_binding_mismatch",
                "A guaranteed capability source must be owned by the selected canonical player or NPC.");
        }

        var acceptedSelections = SelectCapability(
            AcceptedSources(acceptedState, ownerKind, ownerId),
            capabilityRef!);
        if (acceptedSelections.Length != 1)
        {
            if (acceptedSelections.Length == 0 &&
                HasCapabilityAtOtherRole(
                    acceptedState,
                    coordinates,
                    actorRole!,
                    capabilityRef!))
            {
                return Invalid(
                    "treatmentCapability.actorRole",
                    "mortal_wound_treatment_capability_binding_mismatch",
                    "The selected capability belongs to the other treatment actor role.");
            }
            return acceptedSelections.Length > 1
                ? Invalid(
                    "treatmentCapability.source.skillId",
                    "mortal_wound_treatment_capability_source_ambiguous",
                    "The accepted capability source is ambiguous within one actor namespace.")
                : Invalid(
                    "treatmentCapability.source",
                    "mortal_wound_treatment_capability_source_missing",
                    "The selected accepted capability source is missing.");
        }

        var accepted = acceptedSelections[0];
        if (forPublication && publicationPlan is null)
        {
            return PublicationMismatch(
                "Publication export requires the exact validated accepted mechanics plan.");
        }

        var read = acceptedState.ReadCanonicalCapabilityCatalog(
            coordinates,
            actorRole!,
            forPublication ? publicationPlan : null);
        var readFailure = MapReadFailure(read.Status, forPublication);
        if (readFailure is not null)
            return readFailure;

        var liveSelections = SelectCapability(read.Sources, capabilityRef!);
        if (liveSelections.Length != 1)
        {
            if (liveSelections.Length > 1)
            {
                return Invalid(
                    "treatmentCapability.source.skillId",
                    "mortal_wound_treatment_capability_source_ambiguous",
                    "The canonical capability source is ambiguous within one actor namespace.");
            }

            var hasAcceptedSkill = read.Sources.Any(source =>
                string.Equals(source.OwnerKind, accepted.Source.OwnerKind, StringComparison.Ordinal) &&
                string.Equals(source.OwnerId, accepted.Source.OwnerId, StringComparison.Ordinal) &&
                string.Equals(source.SkillKind, accepted.Source.SkillKind, StringComparison.Ordinal) &&
                string.Equals(source.SkillId, accepted.Source.SkillId, StringComparison.Ordinal));
            var hasCapabilityOnAnotherSkill = read.Sources.Any(source =>
                source.Capabilities.Any(capability => string.Equals(
                    capability.CapabilityRef,
                    capabilityRef,
                    StringComparison.Ordinal)));
            if (forPublication && (hasAcceptedSkill || hasCapabilityOnAnotherSkill))
            {
                return PublicationMismatch(
                    "The final canonical skill or capability identity differs from the accepted source.");
            }
            return Invalid(
                "treatmentCapability.source",
                "mortal_wound_treatment_capability_source_missing",
                "The selected canonical capability source is absent.");
        }

        var live = liveSelections[0];
        if (!string.Equals(live.Source.Lifecycle, "active", StringComparison.Ordinal) ||
            !live.Source.Active)
        {
            return Invalid(
                "treatmentCapability.source.lifecycle",
                "mortal_wound_treatment_capability_source_inactive",
                "The selected canonical capability source is inactive or retired.");
        }

        if (!SourceSemanticsEqual(accepted, live))
        {
            return forPublication
                ? PublicationMismatch(
                    "The final canonical capability mechanics differ from the accepted source.")
                : Invalid(
                    "treatmentCapability.source",
                    "mortal_wound_treatment_capability_source_missing",
                    "The current canonical capability identity or mechanics are stale.");
        }

        if (!CoversGuaranteedOutcome(
                acceptedState.CurrentWound,
                route.Outcome.DeclaredResult,
                live.Capability))
        {
            return forPublication
                ? PublicationMismatch(
                    "The final capability no longer covers the complete guaranteed outcome.")
                : Invalid(
                    "treatmentCapability.source",
                    "mortal_wound_treatment_capability_source_missing",
                    "The current capability does not cover one applicable positive guaranteed outcome.");
        }

        return MortalWoundTreatmentCapabilityProofResult.Valid(
            ProofMint,
            MortalWoundTreatmentCapabilityProof.Create(
                ProofMint,
                acceptedState,
                coordinates,
                live.Source,
                live.Capability));
    }

    private static MortalWoundTreatmentCapabilityProofResult? MapReadFailure(
        MortalWoundTreatmentCapabilityCatalogReadStatus status,
        bool forPublication) => status switch
    {
        MortalWoundTreatmentCapabilityCatalogReadStatus.Success => null,
        MortalWoundTreatmentCapabilityCatalogReadStatus.PromotionRequired => Invalid(
            "treatmentCapability.target",
            "mortal_wound_treatment_capability_actor_promotion_required",
            "A target-owned guarantee requires an existing canonical player or NPC promotion."),
        MortalWoundTreatmentCapabilityCatalogReadStatus.SourceOwnerMismatch => Invalid(
            "treatmentCapability.sourceOwner",
            "mortal_wound_treatment_capability_binding_mismatch",
            "The live canonical skill owner differs from the accepted actor binding."),
        MortalWoundTreatmentCapabilityCatalogReadStatus.SourceAmbiguous => Invalid(
            "treatmentCapability.source.skillId",
            "mortal_wound_treatment_capability_source_ambiguous",
            "The canonical active/passive skill namespace is exact or confusable ambiguous."),
        MortalWoundTreatmentCapabilityCatalogReadStatus.PublicationMismatch =>
            PublicationMismatch("The supplied plan is not the exact validated plan for this accepted binding."),
        MortalWoundTreatmentCapabilityCatalogReadStatus.SourceInvalid when forPublication =>
            PublicationMismatch("The final canonical capability shape or mechanics are invalid."),
        MortalWoundTreatmentCapabilityCatalogReadStatus.SourceInvalid => Invalid(
            "treatmentCapability.source",
            "mortal_wound_treatment_capability_source_missing",
            "The current canonical capability source is invalid."),
        _ => Invalid(
            "treatmentCapability.actorRole",
            "mortal_wound_treatment_capability_binding_mismatch",
            "The accepted state, coordinates, and capability source binding do not agree.")
    };

    private static IReadOnlyList<MortalWoundTreatmentCapabilitySkillSource>
        AcceptedSources(
            MortalWoundTreatmentAcceptedStateAuthority acceptedState,
            string ownerKind,
            string ownerId) =>
        (ownerKind == "player"
            ? acceptedState.PlayerCapabilityCatalog
            : acceptedState.NpcCapabilityCatalog)
        .Where(source => string.Equals(
            source.OwnerKind,
            ownerKind,
            StringComparison.Ordinal) && string.Equals(
            source.OwnerId,
            ownerId,
            StringComparison.Ordinal))
        .ToArray();

    private static bool HasCapabilityAtOtherRole(
        MortalWoundTreatmentAcceptedStateAuthority acceptedState,
        MortalWoundTreatmentAttemptCoordinates coordinates,
        string actorRole,
        string capabilityRef)
    {
        var otherRole = actorRole == "provider" ? "target" : "provider";
        var (ownerKind, ownerId) = ActorCoordinate(coordinates, otherRole);
        return ownerKind is "player" or "npc" &&
               SelectCapability(
                   AcceptedSources(acceptedState, ownerKind, ownerId),
                   capabilityRef).Length != 0;
    }

    private static (string OwnerKind, string OwnerId) ActorCoordinate(
        MortalWoundTreatmentAttemptCoordinates coordinates,
        string actorRole) => actorRole == "provider"
        ? (coordinates.ProviderKind, coordinates.ProviderId)
        : (coordinates.TargetKind, coordinates.TargetId);

    private static SelectedCapability[] SelectCapability(
        IEnumerable<MortalWoundTreatmentCapabilitySkillSource> sources,
        string capabilityRef) => sources
        .SelectMany(source => source.Capabilities
            .Where(capability => string.Equals(
                capability.CapabilityRef,
                capabilityRef,
                StringComparison.Ordinal))
            .Select(capability => new SelectedCapability(source, capability)))
        .ToArray();

    private static bool SourceSemanticsEqual(
        SelectedCapability accepted,
        SelectedCapability live) =>
        string.Equals(accepted.Source.OwnerKind, live.Source.OwnerKind, StringComparison.Ordinal) &&
        string.Equals(accepted.Source.OwnerId, live.Source.OwnerId, StringComparison.Ordinal) &&
        string.Equals(accepted.Source.SkillKind, live.Source.SkillKind, StringComparison.Ordinal) &&
        string.Equals(accepted.Source.SkillId, live.Source.SkillId, StringComparison.Ordinal) &&
        string.Equals(accepted.Source.Lifecycle, live.Source.Lifecycle, StringComparison.Ordinal) &&
        accepted.Source.Active == live.Source.Active &&
        accepted.Capability.SchemaVersion == live.Capability.SchemaVersion &&
        string.Equals(
            accepted.Capability.CapabilityRef,
            live.Capability.CapabilityRef,
            StringComparison.Ordinal) &&
        string.Equals(
            accepted.Capability.WoundDomain,
            live.Capability.WoundDomain,
            StringComparison.Ordinal) &&
        accepted.Capability.MinimumSeverityRank == live.Capability.MinimumSeverityRank &&
        accepted.Capability.MaximumSeverityRank == live.Capability.MaximumSeverityRank &&
        LimitsEqual(
            accepted.Capability.OperationLimits,
            live.Capability.OperationLimits);

    private static bool LimitsEqual(
        MortalWoundTreatmentCapabilityOperationLimits left,
        MortalWoundTreatmentCapabilityOperationLimits right) =>
        left.MayStabilize == right.MayStabilize &&
        left.MaximumRecoveryPoints == right.MaximumRecoveryPoints &&
        left.MaximumSeverityReductionSteps == right.MaximumSeverityReductionSteps &&
        left.RemovableComplicationKinds.OrderBy(static value => value, StringComparer.Ordinal)
            .SequenceEqual(
                right.RemovableComplicationKinds.OrderBy(
                    static value => value,
                    StringComparer.Ordinal),
                StringComparer.Ordinal) &&
        left.MayHealAtSeverityI == right.MayHealAtSeverityI &&
        left.MaximumCosmeticHealLegacies == right.MaximumCosmeticHealLegacies &&
        left.MaximumMechanicalEffectHealLegacies ==
            right.MaximumMechanicalEffectHealLegacies;

    private static bool CoversGuaranteedOutcome(
        WoundMaterializationEnvelope wound,
        IReadOnlyList<MortalWoundTreatmentOperation> operations,
        MortalWoundTreatmentCapabilityDefinition capability)
    {
        // T066 proves the closed capability envelope and current applicability of its
        // typed primitives. T067 owns full working-wound simulation before an attempt.
        if (!string.Equals(wound.Classification.Domain, "physical", StringComparison.Ordinal) ||
            !string.Equals(capability.WoundDomain, "physical", StringComparison.Ordinal) ||
            wound.Severity.Rank < capability.MinimumSeverityRank ||
            wound.Severity.Rank > capability.MaximumSeverityRank ||
            operations.Count == 0)
        {
            return false;
        }

        long recoveryPoints = 0;
        long severityReduction = 0;
        long cosmeticLegacies = 0;
        long mechanicalLegacies = 0;
        var healCount = 0;
        var workingSeverityRank = wound.Severity.Rank;
        var workingRecoveryProgress = wound.Recovery.CurrentStepProgress;
        var workingCareState = wound.Care.State;
        var removedComplicationIds = new HashSet<string>(StringComparer.Ordinal);
        var applicablePositive = false;
        var terminal = false;
        var removableKinds = capability.OperationLimits.RemovableComplicationKinds
            .ToHashSet(StringComparer.Ordinal);
        foreach (var operation in operations)
        {
            if (terminal)
                return false;

            switch (operation)
            {
                case MortalWoundStabilizeOperation:
                    if (!capability.OperationLimits.MayStabilize ||
                        workingCareState is "stabilized" or "healed")
                    {
                        return false;
                    }
                    workingCareState = "stabilized";
                    applicablePositive = true;
                    break;

                case MortalWoundAddRecoveryOperation recovery when recovery.Points > 0:
                    if (recovery.Points > long.MaxValue - recoveryPoints ||
                        recovery.Points > long.MaxValue - workingRecoveryProgress)
                    {
                        return false;
                    }
                    recoveryPoints += recovery.Points;
                    workingRecoveryProgress += recovery.Points;
                    applicablePositive = true;
                    break;

                case MortalWoundReduceSeverityOperation severity when severity.Steps > 0:
                    if (severity.Steps >= workingSeverityRank ||
                        severity.Steps > long.MaxValue - severityReduction)
                    {
                        return false;
                    }
                    severityReduction += severity.Steps;
                    workingSeverityRank -= severity.Steps;
                    applicablePositive = true;
                    break;

                case MortalWoundRemoveComplicationOperation removal:
                    var complications = wound.Complications.Where(complication =>
                        !removedComplicationIds.Contains(complication.ComplicationId) &&
                        string.Equals(
                            complication.ComplicationId,
                            removal.ComplicationId,
                            StringComparison.Ordinal)).ToArray();
                    if (complications.Length != 1 ||
                        !removableKinds.Contains(complications[0].Kind))
                    {
                        return false;
                    }
                    removedComplicationIds.Add(removal.ComplicationId);
                    applicablePositive = true;
                    break;

                case MortalWoundHealOperation heal:
                    if (!capability.OperationLimits.MayHealAtSeverityI ||
                        ++healCount > 1 ||
                        workingSeverityRank != 1)
                    {
                        return false;
                    }
                    cosmeticLegacies += heal.Legacies.Count(
                        static legacy => legacy is MortalWoundCosmeticLegacyDraft);
                    mechanicalLegacies += heal.Legacies.Count(
                        static legacy => legacy is MortalWoundMechanicalEffectLegacyDraft);
                    if (cosmeticLegacies + mechanicalLegacies != heal.Legacies.Length)
                        return false;
                    terminal = true;
                    applicablePositive = true;
                    break;

                case MortalWoundNoImprovementOperation:
                case MortalWoundAddComplicationOperation:
                case MortalWoundApplyDeteriorationOperation:
                default:
                    return false;
            }
        }

        return applicablePositive &&
            recoveryPoints <= capability.OperationLimits.MaximumRecoveryPoints &&
            severityReduction <= capability.OperationLimits.MaximumSeverityReductionSteps &&
            cosmeticLegacies <= capability.OperationLimits.MaximumCosmeticHealLegacies &&
            mechanicalLegacies <=
            capability.OperationLimits.MaximumMechanicalEffectHealLegacies;
    }

    private static MortalWoundTreatmentCapabilityProofResult PublicationMismatch(
        string message) => Invalid(
        "treatmentCapability.publicationPlan",
        "mortal_wound_treatment_capability_publication_mismatch",
        message);

    private static MortalWoundTreatmentCapabilityProofResult Invalid(
        string path,
        string code,
        string message) =>
        MortalWoundTreatmentCapabilityProofResult.Invalid(
            new ValidationIssue(
                path,
                IssueSeverity.Error,
                message,
                code: code,
                section: "MortalWoundTreatmentCapability"));

    private sealed record SelectedCapability(
        MortalWoundTreatmentCapabilitySkillSource Source,
        MortalWoundTreatmentCapabilityDefinition Capability);
}
