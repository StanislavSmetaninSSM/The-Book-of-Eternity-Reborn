using System.Collections.ObjectModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookOfEternityClient.Services;

internal sealed record MortalWoundTreatmentAcceptedStateAuthorityResult(
    bool IsValid,
    IReadOnlyList<ValidationIssue> Issues,
    MortalWoundTreatmentAcceptedStateAuthority? Authority);

/// <summary>
/// Complete immutable admission authority for one current Mortal wound-treatment
/// attempt. Every persisted value is reconstructed from the active signed pending-turn
/// snapshot before the registry is consulted.
/// </summary>
internal sealed class MortalWoundTreatmentAcceptedStateAuthority
{
    private static readonly object PersistedProcedureClaimRecoveryCapability = new();
    private static readonly ConditionalWeakTable<
        MortalWoundTreatmentAuthority.Context,
        RequirementContextProjectionProvenance>
        RequirementContextProjectionRegistry = new();
    private const string WorldTimePath = "game_state/world/world_time.json";
    private const string CurrentLocationPath = "game_state/world/current_location.json";
    private const string PlayerActiveSkillsPath = "game_state/player/skills_active.json";
    private const string PlayerPassiveSkillsPath = "game_state/player/skills_passive.json";
    private const string PlayerSkillMasteryPath = "game_state/player/skill_mastery.json";
    private const string NpcCorePath = "game_state/npcs/npc_core.json";
    private const string PlayerInventoryPath = "game_state/inventory/items.json";
    private const string ItemIdentityPath = "game_state/inventory/item_identity_index.json";
    private const string RegularQuestsPath = "game_state/quests/regular_quests.json";

    private static readonly string[] OptionalAuthorityPaths =
    new[]
    {
        PlayerInventoryPath,
        ItemIdentityPath,
        RegularQuestsPath,
        WoundCarrierCatalog.PlayerPath,
        WoundCarrierCatalog.NpcPath,
        WoundCarrierCatalog.EnemiesPath,
        WoundCarrierCatalog.AlliesPath,
        WoundCarrierCatalog.AfterlifeProfilesPath,
        EffectCarrierCatalog.PlayerPath,
        EffectCarrierCatalog.NpcPath,
        EffectCarrierCatalog.EnemiesPath,
        EffectCarrierCatalog.AlliesPath,
        EffectCarrierCatalog.AfterlifeProfilesPath,
        EffectCarrierCatalog.SpiritualConflictPath,
        EffectIdentityState.StatePath
    }
    .Concat(CanonicalResourceOwnerAuthorityComposer.SourceAuthorityPaths)
    .Distinct(StringComparer.Ordinal)
    .ToArray();

    private readonly FileSystemManager _fileSystem;
    private readonly FileSystemManager.CanonicalWriteLease _writeLease;
    private readonly CanonicalRootIdentity _rootIdentity;
    private readonly long _rootRevision;
    private readonly WoundAcceptedTurnBinding _binding;
    private readonly MortalWoundTreatmentAuthority.Context _requirementContext;
    private readonly MortalWoundTreatmentAuthority.Snapshot _requirementSnapshot;
    private readonly WoundMaterializationEnvelope _currentWound;
    private readonly string _woundSourcePath;
    private readonly WoundHistoryState _history;
    private readonly EffectMechanicsSnapshot _effectMechanics;
    private readonly int[] _acceptedD20EventValues;
    private readonly string _acceptedD20PoolFingerprint;
    private readonly MortalWoundTreatmentCapabilitySkillSource[] _playerCapabilityCatalog;
    private readonly MortalWoundTreatmentCapabilitySkillSource[] _npcCapabilityCatalog;

    private MortalWoundTreatmentAcceptedStateAuthority(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease,
        CanonicalRootIdentity rootIdentity,
        long rootRevision,
        string sessionGeneration,
        WoundAcceptedTurnBinding binding,
        MortalWoundTreatmentAuthority.Context requirementContext,
        MortalWoundTreatmentAuthority.Snapshot requirementSnapshot,
        WoundMaterializationEnvelope currentWound,
        string woundSourcePath,
        WoundHistoryState history,
        long currentGameMinute,
        EffectMechanicsSnapshot effectMechanics,
        IReadOnlyList<int> acceptedD20EventValues,
        string acceptedD20PoolFingerprint,
        IReadOnlyList<MortalWoundTreatmentCapabilitySkillSource> playerCapabilityCatalog,
        IReadOnlyList<MortalWoundTreatmentCapabilitySkillSource> npcCapabilityCatalog,
        MortalWoundTreatmentDefinition treatmentDefinition,
        string contextFingerprint,
        string bindingFingerprint,
        string woundFingerprint,
        string identityFingerprint,
        string historyFingerprint,
        string clockFingerprint,
        string effectFingerprint,
        string itemResourceFingerprint,
        string actorLocationFingerprint,
        string playerCapabilityCatalogFingerprint,
        string npcCapabilityCatalogFingerprint,
        string skillSourceFingerprint,
        string requirementSnapshotFingerprint,
        string acceptedStateFingerprint)
    {
        _fileSystem = fileSystem;
        _writeLease = writeLease;
        _rootIdentity = rootIdentity;
        _rootRevision = rootRevision;
        SessionGeneration = sessionGeneration;
        _binding = Clone(binding);
        _requirementContext = requirementContext with { };
        _requirementSnapshot = requirementSnapshot with { };
        _currentWound = currentWound;
        _woundSourcePath = woundSourcePath;
        _history = history;
        CurrentGameMinute = currentGameMinute;
        _effectMechanics = effectMechanics;
        _acceptedD20EventValues = acceptedD20EventValues.ToArray();
        _acceptedD20PoolFingerprint = acceptedD20PoolFingerprint;
        _playerCapabilityCatalog = playerCapabilityCatalog.Select(Clone).ToArray();
        _npcCapabilityCatalog = npcCapabilityCatalog.Select(Clone).ToArray();
        TreatmentDefinition = treatmentDefinition;
        ContextFingerprint = contextFingerprint;
        BindingFingerprint = bindingFingerprint;
        WoundFingerprint = woundFingerprint;
        IdentityFingerprint = identityFingerprint;
        HistoryFingerprint = historyFingerprint;
        ClockFingerprint = clockFingerprint;
        EffectFingerprint = effectFingerprint;
        ItemResourceFingerprint = itemResourceFingerprint;
        ActorLocationFingerprint = actorLocationFingerprint;
        PlayerCapabilityCatalogFingerprint = playerCapabilityCatalogFingerprint;
        NpcCapabilityCatalogFingerprint = npcCapabilityCatalogFingerprint;
        SkillSourceFingerprint = skillSourceFingerprint;
        RequirementSnapshotFingerprint = requirementSnapshotFingerprint;
        AcceptedStateFingerprint = acceptedStateFingerprint;
    }

    internal WoundAcceptedTurnBinding Binding => Clone(_binding);
    internal MortalWoundTreatmentAuthority.Context RequirementContext =>
        ExportRequirementContextProjection();
    internal MortalWoundTreatmentAuthority.Snapshot RequirementSnapshot =>
        _requirementSnapshot with { };
    internal WoundMaterializationEnvelope CurrentWound => _currentWound;
    internal string WoundSourcePath => _woundSourcePath;
    internal WoundHistoryState History => _history;

    /// <summary>
    /// Gets detached accepted event values for strict recovery-source comparison.
    /// These values do not grant procedure dice or publication authority.
    /// </summary>
    internal IReadOnlyList<int> RecoveryAcceptedD20EventValues =>
        Array.AsReadOnly(_acceptedD20EventValues.ToArray());
    internal long CurrentGameMinute { get; }
    internal EffectMechanicsSnapshot EffectMechanics => _effectMechanics;
    internal IReadOnlyList<MortalWoundTreatmentCapabilitySkillSource>
        PlayerCapabilityCatalog => Array.AsReadOnly(
            _playerCapabilityCatalog.Select(Clone).ToArray());
    internal IReadOnlyList<MortalWoundTreatmentCapabilitySkillSource>
        NpcCapabilityCatalog => Array.AsReadOnly(
            _npcCapabilityCatalog.Select(Clone).ToArray());
    internal MortalWoundTreatmentDefinition TreatmentDefinition { get; }
    internal string SessionGeneration { get; }
    internal string ContextFingerprint { get; }
    internal string BindingFingerprint { get; }
    internal string WoundFingerprint { get; }
    internal string IdentityFingerprint { get; }
    internal string HistoryFingerprint { get; }
    internal string ClockFingerprint { get; }
    internal string EffectFingerprint { get; }
    internal string ItemResourceFingerprint { get; }
    internal string ActorLocationFingerprint { get; }
    internal string PlayerCapabilityCatalogFingerprint { get; }
    internal string NpcCapabilityCatalogFingerprint { get; }
    internal string SkillSourceFingerprint { get; }
    internal string RequirementSnapshotFingerprint { get; }
    internal string AcceptedStateFingerprint { get; }

    internal static bool IsAcceptedStateContextProjection(
        MortalWoundTreatmentAuthority.Context context)
    {
        if (!RequirementContextProjectionRegistry.TryGetValue(
                context,
                out var provenance) ||
            !string.Equals(
                provenance.Seal,
                ComputeRequirementContextProjectionSeal(context),
                StringComparison.Ordinal) ||
            !provenance.AcceptedState.TryGetTarget(out var acceptedState) ||
            !string.Equals(
                provenance.AcceptedStateFingerprint,
                acceptedState.AcceptedStateFingerprint,
                StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            return acceptedState.HasCurrentAdmissionAuthority();
        }
        catch (Exception exception) when (exception is InvalidOperationException or
                                           ObjectDisposedException or
                                           ArgumentException or
                                           IOException or
                                           UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    /// Reads only the canonical player/NPC capability namespace fixed by the accepted
    /// coordinates. Filesystem and lease authority remain private; callers receive a
    /// detached typed catalog and a closed failure classification only.
    /// </summary>
    internal MortalWoundTreatmentCapabilityCatalogReadResult
        ReadCanonicalCapabilityCatalog(
            MortalWoundTreatmentAttemptCoordinates? coordinates,
            string? actorRole,
            AcceptedMechanicsPlan? publicationPlan)
        => ReadCanonicalCapabilityCatalogCore(
            coordinates,
            actorRole,
            publicationPlan,
            candidateReadCapability: null);

    internal MortalWoundTreatmentCapabilityCatalogReadResult
        ReadCanonicalCapabilityCatalogForCandidate(
            object candidateReadCapability,
            MortalWoundTreatmentAttemptCoordinates? coordinates,
            string? actorRole,
            AcceptedMechanicsPlan publicationPlan) =>
        ReadCanonicalCapabilityCatalogCore(
            coordinates,
            actorRole,
            publicationPlan,
            candidateReadCapability);

    private MortalWoundTreatmentCapabilityCatalogReadResult
        ReadCanonicalCapabilityCatalogCore(
            MortalWoundTreatmentAttemptCoordinates? coordinates,
            string? actorRole,
            AcceptedMechanicsPlan? publicationPlan,
            object? candidateReadCapability)
    {
        if (coordinates is null ||
            !coordinates.MatchesAcceptedState(this) ||
            actorRole is not ("provider" or "target"))
        {
            return new MortalWoundTreatmentCapabilityCatalogReadResult(
                MortalWoundTreatmentCapabilityCatalogReadStatus.CoordinatesInvalid);
        }

        var ownerKind = actorRole == "provider"
            ? coordinates.ProviderKind
            : coordinates.TargetKind;
        var ownerId = actorRole == "provider"
            ? coordinates.ProviderId
            : coordinates.TargetId;
        if (actorRole == "target" &&
            ownerKind is "combatant" or "combatant_member")
        {
            return new MortalWoundTreatmentCapabilityCatalogReadResult(
                MortalWoundTreatmentCapabilityCatalogReadStatus.PromotionRequired);
        }
        if (ownerKind is not ("player" or "npc") ||
            (ownerKind == "player" &&
             !string.Equals(ownerId, "player_current", StringComparison.Ordinal)))
        {
            return new MortalWoundTreatmentCapabilityCatalogReadResult(
                MortalWoundTreatmentCapabilityCatalogReadStatus.SourceOwnerMismatch);
        }

        try
        {
            if (!HasCurrentAdmissionAuthority())
            {
                return new MortalWoundTreatmentCapabilityCatalogReadResult(
                    MortalWoundTreatmentCapabilityCatalogReadStatus.CoordinatesInvalid);
            }
            if (publicationPlan is not null &&
                (candidateReadCapability is null
                    ? !MatchesValidatedPublicationPlan(publicationPlan)
                    : !MortalWoundTreatmentCapabilityAuthority
                        .CandidateAdmissionGate.IsCandidateReadCapability(
                            candidateReadCapability)))
            {
                return new MortalWoundTreatmentCapabilityCatalogReadResult(
                    MortalWoundTreatmentCapabilityCatalogReadStatus.PublicationMismatch);
            }

            return ownerKind == "player"
                ? ReadPlayerCapabilityCatalog(publicationPlan)
                : ReadNpcCapabilityCatalog(ownerId, publicationPlan);
        }
        catch (Exception exception) when (exception is InvalidOperationException or
                                           ObjectDisposedException or
                                           IOException or
                                           UnauthorizedAccessException or
                                           JsonException or
                                           ArgumentException or
                                           OverflowException)
        {
            return new MortalWoundTreatmentCapabilityCatalogReadResult(
                publicationPlan is null
                    ? MortalWoundTreatmentCapabilityCatalogReadStatus.SourceInvalid
                    : MortalWoundTreatmentCapabilityCatalogReadStatus.PublicationMismatch);
        }
    }

    internal static MortalWoundTreatmentAcceptedStateAuthorityResult ExportCurrent(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease,
        MortalWoundTreatmentAuthority.Context context,
        string woundId)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(writeLease);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(woundId);

        MortalWoundTreatmentAcceptedStateAuthorityResult candidate;
        try
        {
            candidate = ExportCurrentCore(fileSystem, writeLease, context, woundId);
        }
        catch (Exception exception) when (exception is InvalidOperationException or
                                           ObjectDisposedException or
                                           IOException or
                                           UnauthorizedAccessException or
                                           JsonException or
                                           ArgumentException or
                                           OverflowException)
        {
            candidate = Failure(new[]
            {
                Issue(
                    LiveTurnPreparationService.PendingTurnSnapshotManifestPath,
                    "mortal_wound_treatment_accepted_state_lease_invalid",
                    "one active canonical lease and complete current accepted snapshot",
                    exception.GetType().Name)
            });
        }

        return AcceptedTurnAuthorityRegistry.BindMortalWoundTreatmentAcceptedState(
            fileSystem,
            writeLease,
            candidate);
    }

    private MortalWoundTreatmentAuthority.Context
        ExportRequirementContextProjection()
    {
        var projection = _requirementContext with { };
        RequirementContextProjectionRegistry.Add(
            projection,
            new RequirementContextProjectionProvenance(
                new WeakReference<MortalWoundTreatmentAcceptedStateAuthority>(this),
                AcceptedStateFingerprint,
                ComputeRequirementContextProjectionSeal(projection)));
        return projection;
    }

    private static string ComputeRequirementContextProjectionSeal(
        MortalWoundTreatmentAuthority.Context context) =>
        WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
        {
            "mortal_wound_treatment.accepted_state.requirement_context_projection",
            "1",
            context.SchemaVersion.ToString(CultureInfo.InvariantCulture),
            context.Realm,
            context.TargetKind,
            context.TargetId,
            context.ProviderKind,
            context.ProviderId,
            context.CurrentLocationId,
            context.SourcePath
        });

    private sealed record RequirementContextProjectionProvenance(
        WeakReference<MortalWoundTreatmentAcceptedStateAuthority> AcceptedState,
        string AcceptedStateFingerprint,
        string Seal);

    private static MortalWoundTreatmentAcceptedStateAuthorityResult ExportCurrentCore(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease,
        MortalWoundTreatmentAuthority.Context context,
        string woundId)
    {
        fileSystem.EnsureCanonicalWriteLeaseActive(writeLease);
        var issues = new List<ValidationIssue>();
        if (!context.HasValidParserProvenance())
        {
            issues.Add(Issue(
                context.SourcePath,
                "mortal_wound_treatment_accepted_state_context_provenance_invalid",
                "exact output of MortalWoundTreatmentAuthority.ParseContext",
                "unsealed, copied, or modified context"));
            return Failure(issues);
        }
        if (context.SchemaVersion != 1)
        {
            issues.Add(Issue(
                context.SourcePath,
                "mortal_wound_treatment_accepted_state_context_schema_invalid",
                "schemaVersion=1 parsed treatment context",
                context.SchemaVersion.ToString(CultureInfo.InvariantCulture)));
            return Failure(issues);
        }
        if (!string.Equals(context.Realm, "mortal_world", StringComparison.Ordinal))
        {
            issues.Add(Issue(
                context.SourcePath,
                "mortal_wound_treatment_accepted_state_realm_invalid",
                "mortal_world",
                context.Realm));
            return Failure(issues);
        }

        var pathSelection = BuildPathSelection(context, issues);
        if (issues.Count != 0)
            return Failure(issues);

        var snapshotRead = PendingTurnSnapshotReader.ReadCurrent(
            fileSystem,
            writeLease,
            pathSelection);
        if (!snapshotRead.Success || snapshotRead.Snapshot is null)
            return Failure(snapshotRead.Issues);
        var signed = snapshotRead.Snapshot;
        var requiredPaths = signed.CoveredLogicalPaths;
        ValidateTargetCarrierCoverage(context, requiredPaths, issues);
        if (issues.Count != 0)
            return Failure(issues);

        if (!string.Equals(signed.Realm, "mortal_world", StringComparison.Ordinal))
        {
            issues.Add(Issue(
                LiveTurnPreparationService.PendingTurnSnapshotManifestPath,
                "mortal_wound_treatment_accepted_state_realm_invalid",
                "mortal_world",
                signed.Realm));
            return Failure(issues);
        }

        var roots = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        foreach (var path in requiredPaths)
        {
            if (string.Equals(
                    path,
                    StorageTransportMoveService.VehiclesPath,
                    StringComparison.Ordinal))
            {
                if (!TryParseProjectionCatalogObject(
                        signed.ReadRequiredBytes(path),
                        path,
                        issues,
                        out var vehicleRoot))
                {
                    continue;
                }

                roots.Add(path, vehicleRoot!);
                continue;
            }

            if (!TryParseObject(signed.ReadRequiredBytes(path), path, issues, out var root))
                continue;
            roots.Add(path, root!);
        }
        if (issues.Count != 0)
            return Failure(issues);

        var sourceValidation = new ValidationService(
                fileSystem,
                NullLogger<ValidationService>.Instance)
            .ValidateMortalWoundTreatmentDetachedSources(roots);
        AddIssues(issues, sourceValidation);
        if (issues.Count != 0)
            return Failure(issues);

        if (!TryComposeBinding(signed, issues, out var binding, out var requestFingerprint) ||
            !TryReadWorldMinute(roots[WorldTimePath], issues, out var currentGameMinute))
        {
            return Failure(issues);
        }

        var woundCarriers = new WoundCarrierCatalogInput(
            Get(roots, WoundCarrierCatalog.PlayerPath),
            Get(roots, WoundCarrierCatalog.NpcPath),
            Get(roots, WoundCarrierCatalog.EnemiesPath),
            Get(roots, WoundCarrierCatalog.AlliesPath),
            Get(roots, WoundCarrierCatalog.AfterlifeProfilesPath));
        var woundCatalog = WoundCarrierCatalog.Build(woundCarriers);
        issues.AddRange(woundCatalog.Issues);
        woundCatalog.TryResolveOne(woundId, out var occurrence);

        var identity = WoundIdentityState.Parse(
            Decode(signed.ReadRequiredBytes(WoundIdentityState.StatePath)),
            WoundIdentityState.StatePath);
        var history = WoundHistoryState.Parse(
            Decode(signed.ReadRequiredBytes(WoundHistoryState.HistoryPath)),
            WoundHistoryState.HistoryPath);
        issues.AddRange(identity.Issues);
        issues.AddRange(history.Issues);
        if (identity.State is not null &&
            history.State is not null &&
            history.State.Transitions.Count != 0)
        {
            issues.AddRange(history.State.ValidateAgreement(identity.State, woundCatalog));
        }
        if (occurrence is null && identity.State is not null && history.State is not null &&
            issues.Count == 0 && woundCatalog.CountExactOccurrences(woundId) == 0)
            occurrence = ResolveHealedRecoveryReplaySource(woundId, identity.State, history.State);
        if (occurrence is null)
            issues.Add(Issue("treatmentSelection.woundId",
                "mortal_wound_treatment_accepted_state_wound_unresolved",
                "one exact active wound or sealed terminal recovery snapshot",
                $"{woundId}: occurrences={woundCatalog.CountExactOccurrences(woundId)}"));
        if (occurrence is not null && identity.State is not null)
        {
            if (!identity.State.TryGetEntry(woundId, out var identityEntry))
            {
                issues.Add(Issue(
                    WoundIdentityState.StatePath,
                    "mortal_wound_treatment_accepted_state_identity_missing",
                    "one exact current identity entry for the selected wound",
                    woundId));
            }
            else if (occurrence.Wound.Lifecycle == "active")
            {
                issues.AddRange(WoundIdentityState.ValidateActiveAgreement(
                    identityEntry,
                    occurrence.Wound,
                    WoundIdentityState.StatePath));
            }
        }
        if (occurrence is null || identity.State is null || history.State is null ||
            issues.Count != 0)
        {
            return Failure(issues);
        }

        if (!string.Equals(occurrence.Coordinate.Realm, context.Realm, StringComparison.Ordinal) ||
            !string.Equals(occurrence.Coordinate.OwnerKind, context.TargetKind, StringComparison.Ordinal) ||
            !string.Equals(occurrence.Coordinate.OwnerId, context.TargetId, StringComparison.Ordinal))
        {
            issues.Add(Issue(
                "treatmentSelection.target",
                "mortal_wound_treatment_accepted_state_target_mismatch",
                $"({context.Realm}, {context.TargetKind}, {context.TargetId})",
                $"({occurrence.Coordinate.Realm}, {occurrence.Coordinate.OwnerKind}, {occurrence.Coordinate.OwnerId})"));
            return Failure(issues);
        }

        var treatment = MortalWoundTreatmentContract.ParseProjection(
            occurrence.Wound.Treatment,
            occurrence.JsonPath + ".treatment",
            occurrence.Wound.Owner.Realm,
            occurrence.Wound.Owner.OwnerKind,
            occurrence.Wound.Severity.Rank,
            occurrence.Wound.Complications,
            occurrence.Wound.Recovery.DeteriorationPolicy);
        issues.AddRange(treatment.Issues);
        if (!treatment.IsValid || treatment.Treatment is null)
            return Failure(issues);

        var playerCapabilities = ParsePlayerCapabilities(roots, issues);
        var npcCapabilities = ParseNpcCapabilities(roots[NpcCorePath], issues);
        if (issues.Count != 0)
            return Failure(issues);

        var canonical = MortalWoundTreatmentAcceptedCanonicalProjection.Compose(
            context,
            signed,
            roots,
            playerCapabilities,
            npcCapabilities);
        AddIssues(issues, canonical.Issues);
        var actors = canonical.Actors;
        var locations = canonical.Locations;
        var items = canonical.Items;
        var resources = canonical.Resources;
        var facilities = canonical.Facilities;
        var quests = canonical.Quests;
        var environments = canonical.Environments;
        var effectMechanics = ComposeEffectMechanics(roots, issues);
        var effects = ComposeRequirementEffects(effectMechanics);
        if (issues.Count != 0)
            return Failure(issues);

        var requirementProjection = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["snapshotToken"] = signed.SnapshotToken,
            ["items"] = new JsonArray(items.Select(ToJson).Cast<JsonNode?>().ToArray()),
            ["resources"] = new JsonArray(resources.Select(ToJson).Cast<JsonNode?>().ToArray()),
            ["actors"] = new JsonArray(actors.Select(ToJson).Cast<JsonNode?>().ToArray()),
            ["facilities"] = new JsonArray(facilities.Select(ToJson).Cast<JsonNode?>().ToArray()),
            ["locations"] = new JsonArray(locations.Select(ToJson).Cast<JsonNode?>().ToArray()),
            ["quests"] = new JsonArray(quests.Select(ToJson).Cast<JsonNode?>().ToArray()),
            ["effects"] = new JsonArray(effects.Select(ToJson).Cast<JsonNode?>().ToArray()),
            ["environments"] = new JsonArray(environments.Select(ToJson).Cast<JsonNode?>().ToArray())
        };
        var parsedSnapshot = MortalWoundTreatmentAuthority.ParseSnapshot(
            requirementProjection.ToJsonString(),
            "mortalWoundTreatment.acceptedSnapshot");
        issues.AddRange(parsedSnapshot.Issues);
        if (!parsedSnapshot.IsValid || parsedSnapshot.Snapshot is null)
            return Failure(issues);

        var generation = fileSystem.GetOrCreateSessionGeneration(writeLease);
        var rootIdentity = fileSystem.CanonicalRootAuthorityIdentity;
        var contextFingerprint = Hash(
            "mortal_wound_treatment_context",
            generation,
            signed.SessionId,
            signed.RequestId,
            signed.SnapshotToken,
            Number(signed.TurnNumber),
            Number(context.SchemaVersion),
            context.Realm,
            context.TargetKind,
            context.TargetId,
            context.ProviderKind,
            context.ProviderId,
            context.CurrentLocationId);
        var bindingFingerprint = HashBinding(binding!);
        var woundFingerprint = WoundIdentityState.ComputeSemanticFingerprint(occurrence.Wound);
        var identityFingerprint = Hash(
            "mortal_wound_treatment_identity",
            WoundIdentityState.SerializeCanonical(identity.State));
        var historyFingerprint = Hash(
            "mortal_wound_treatment_history",
            WoundHistoryState.SerializeCanonical(history.State));
        var clockFingerprint = Hash(
            "mortal_wound_treatment_clock",
            WorldTimePath,
            currentGameMinute.ToString(CultureInfo.InvariantCulture));
        var effectFingerprint = ComputeEffectFingerprint(effectMechanics);
        var itemResourceFingerprint = Hash(
            "mortal_wound_treatment_item_resource",
            WoundAcceptedTurnFingerprintWriter.CanonicalJson(
                new JsonArray(items.Select(ToMechanicalJson).Cast<JsonNode?>().ToArray())),
            WoundAcceptedTurnFingerprintWriter.CanonicalJson(
                new JsonArray(resources.Select(ToMechanicalJson).Cast<JsonNode?>().ToArray())));
        var actorLocationFingerprint = Hash(
            "mortal_wound_treatment_actor_location",
            WoundAcceptedTurnFingerprintWriter.CanonicalJson(
                new JsonArray(actors.Select(ToMechanicalJson).Cast<JsonNode?>().ToArray())),
            WoundAcceptedTurnFingerprintWriter.CanonicalJson(
                new JsonArray(locations.Select(ToMechanicalJson).Cast<JsonNode?>().ToArray())),
            WoundAcceptedTurnFingerprintWriter.CanonicalJson(
                new JsonArray(facilities.Select(ToMechanicalJson).Cast<JsonNode?>().ToArray())),
            WoundAcceptedTurnFingerprintWriter.CanonicalJson(
                new JsonArray(environments.Select(ToMechanicalJson).Cast<JsonNode?>().ToArray())));
        var playerCatalogFingerprint = ComputeCapabilityCatalogFingerprint(playerCapabilities);
        var npcCatalogFingerprint = ComputeCapabilityCatalogFingerprint(npcCapabilities);
        var skillSourceFingerprint = Hash(
            "mortal_wound_treatment_skill_sources",
            playerCatalogFingerprint,
            npcCatalogFingerprint);
        var requirementSnapshotFingerprint = Hash(
            "mortal_wound_treatment_requirement_snapshot",
            WoundAcceptedTurnFingerprintWriter.CanonicalJson(
                BuildMechanicalRequirementProjection(
                    signed.SnapshotToken,
                    items,
                    resources,
                    actors,
                    facilities,
                    locations,
                    quests,
                    effects,
                    environments)));
        var acceptedD20PoolFields = new List<string?>
        {
            "book_of_eternity.mortal_wound_treatment.accepted_d20_pool",
            "1",
            generation,
            signed.SessionId,
            signed.RequestId,
            signed.SnapshotToken,
            Number(signed.TurnNumber),
            Number(signed.AcceptedD20EventValues.Count)
        };
        for (var index = 0; index < signed.AcceptedD20EventValues.Count; index++)
        {
            acceptedD20PoolFields.Add(Number(index));
            acceptedD20PoolFields.Add(Number(signed.AcceptedD20EventValues[index]));
        }
        var acceptedD20PoolFingerprint =
            WoundAcceptedTurnFingerprintWriter.Compute(acceptedD20PoolFields);
        var acceptedStateFingerprint = Hash(
            "mortal_wound_treatment_accepted_state",
            "1",
            generation,
            bindingFingerprint,
            contextFingerprint,
            requestFingerprint,
            woundFingerprint,
            occurrence.JsonPath,
            identityFingerprint,
            historyFingerprint,
            clockFingerprint,
            effectFingerprint,
            itemResourceFingerprint,
            actorLocationFingerprint,
            playerCatalogFingerprint,
            npcCatalogFingerprint,
            skillSourceFingerprint,
            requirementSnapshotFingerprint);

        var authority = new MortalWoundTreatmentAcceptedStateAuthority(
            fileSystem,
            writeLease,
            rootIdentity,
            rootIdentity.SessionGenerationRevision,
            generation,
            binding!,
            context,
            parsedSnapshot.Snapshot,
            occurrence.Wound,
            occurrence.JsonPath,
            history.State,
            currentGameMinute,
            effectMechanics,
            signed.AcceptedD20EventValues,
            acceptedD20PoolFingerprint,
            playerCapabilities,
            npcCapabilities,
            treatment.Treatment,
            contextFingerprint,
            bindingFingerprint,
            woundFingerprint,
            identityFingerprint,
            historyFingerprint,
            clockFingerprint,
            effectFingerprint,
            itemResourceFingerprint,
            actorLocationFingerprint,
            playerCatalogFingerprint,
            npcCatalogFingerprint,
            skillSourceFingerprint,
            requirementSnapshotFingerprint,
            acceptedStateFingerprint);
        return new MortalWoundTreatmentAcceptedStateAuthorityResult(
            true,
            Array.Empty<ValidationIssue>(),
            authority);
    }

    internal bool SemanticallyEquals(MortalWoundTreatmentAcceptedStateAuthority other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return BindingAgrees(_binding, other._binding) &&
               string.Equals(_woundSourcePath, other._woundSourcePath, StringComparison.Ordinal) &&
               _acceptedD20EventValues.SequenceEqual(other._acceptedD20EventValues) &&
               string.Equals(
                   _acceptedD20PoolFingerprint,
                   other._acceptedD20PoolFingerprint,
                   StringComparison.Ordinal) &&
               string.Equals(SessionGeneration, other.SessionGeneration, StringComparison.Ordinal) &&
               string.Equals(ContextFingerprint, other.ContextFingerprint, StringComparison.Ordinal) &&
               string.Equals(BindingFingerprint, other.BindingFingerprint, StringComparison.Ordinal) &&
               string.Equals(WoundFingerprint, other.WoundFingerprint, StringComparison.Ordinal) &&
               string.Equals(IdentityFingerprint, other.IdentityFingerprint, StringComparison.Ordinal) &&
               string.Equals(HistoryFingerprint, other.HistoryFingerprint, StringComparison.Ordinal) &&
               string.Equals(ClockFingerprint, other.ClockFingerprint, StringComparison.Ordinal) &&
               string.Equals(EffectFingerprint, other.EffectFingerprint, StringComparison.Ordinal) &&
               string.Equals(ItemResourceFingerprint, other.ItemResourceFingerprint, StringComparison.Ordinal) &&
               string.Equals(ActorLocationFingerprint, other.ActorLocationFingerprint, StringComparison.Ordinal) &&
               string.Equals(PlayerCapabilityCatalogFingerprint, other.PlayerCapabilityCatalogFingerprint, StringComparison.Ordinal) &&
               string.Equals(NpcCapabilityCatalogFingerprint, other.NpcCapabilityCatalogFingerprint, StringComparison.Ordinal) &&
               string.Equals(SkillSourceFingerprint, other.SkillSourceFingerprint, StringComparison.Ordinal) &&
               string.Equals(RequirementSnapshotFingerprint, other.RequirementSnapshotFingerprint, StringComparison.Ordinal) &&
               string.Equals(AcceptedStateFingerprint, other.AcceptedStateFingerprint, StringComparison.Ordinal);
    }

    internal bool IsLeaseBoundTo(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease)
    {
        try
        {
            fileSystem.EnsureCanonicalWriteLeaseActive(writeLease);
            return ReferenceEquals(_fileSystem, fileSystem) &&
                   ReferenceEquals(_writeLease, writeLease) &&
                   ReferenceEquals(_rootIdentity, fileSystem.CanonicalRootAuthorityIdentity) &&
                   _rootRevision == _rootIdentity.SessionGenerationRevision &&
                   fileSystem.IsCurrentSessionGeneration(writeLease, SessionGeneration);
        }
        catch (Exception exception) when (exception is InvalidOperationException or
                                           ObjectDisposedException or
                                           ArgumentException or
                                           IOException or
                                           UnauthorizedAccessException)
        {
            return false;
        }
    }

    internal bool HasCurrentAdmissionAuthority() =>
        AcceptedTurnAuthorityRegistry.IsCurrentMortalWoundTreatmentAcceptedState(
            _fileSystem,
            _writeLease,
            this);

    internal static bool IsPersistedProcedureClaimRecoveryCapability(
        object capability) => ReferenceEquals(
        capability,
        PersistedProcedureClaimRecoveryCapability);

    internal bool AgreesWithBindingAndWound(
        WoundAcceptedTurnBinding? binding,
        string? woundId) =>
        binding is not null &&
        ResourceMaterializationContract.IsExactIdentifier(woundId) &&
        BindingAgrees(_binding, binding) &&
        string.Equals(_currentWound.WoundId, woundId, StringComparison.Ordinal);

    internal MortalWoundDeteriorationPolicyAuthorityResult
        CreateDeteriorationPolicyAuthority(
            MortalWoundTreatmentAttemptCoordinates? coordinates,
            string? policyRef) =>
        AcceptedTurnAuthorityRegistry.CreateMortalWoundDeteriorationPolicyAuthority(
            _fileSystem,
            _writeLease,
            this,
            coordinates,
            policyRef);

    internal MortalWoundProcedureReservationSetResult ReserveProcedureReservations(
        object reservationCapability,
        MortalWoundTreatmentAttemptCoordinates coordinates,
        string rollMode,
        string rollActorKind,
        string rollActorId) =>
        AcceptedTurnAuthorityRegistry.ReserveMortalWoundProcedureReservations(
            _fileSystem,
            _writeLease,
            this,
            reservationCapability,
            coordinates,
            rollMode,
            rollActorKind,
            rollActorId);

    internal bool ReleaseProcedureReservations(
        MortalWoundProcedureDiceReservation diceReservation,
        MortalWoundCriticalReactionReservation? criticalReactionReservation,
        MortalWoundCriticalReactionReservationAgreement? criticalReactionAgreement) =>
        AcceptedTurnAuthorityRegistry.ReleaseMortalWoundProcedureReservations(
            _fileSystem,
            _writeLease,
            this,
            diceReservation,
            criticalReactionReservation,
            criticalReactionAgreement);

    internal bool HasLiveProcedureReservationAgreement(
        MortalWoundProcedureCheckAuthority procedure) =>
        AcceptedTurnAuthorityRegistry
            .HasLiveMortalWoundProcedureReservationAgreement(
                _fileSystem,
                _writeLease,
                this,
                procedure);

    internal MortalWoundTreatmentPersistedRequestCatalogResult
        RestorePersistedTreatmentRequests(WoundHistoryParseResult history)
    {
        ArgumentNullException.ThrowIfNull(history);
        try
        {
            _fileSystem.EnsureCanonicalWriteLeaseActive(_writeLease);
            if (!HasCurrentAdmissionAuthority() || !MatchesCompleteHistory(history))
            {
                return PersistedRecoveryFailure(
                    "mortal_wound_treatment_procedure_claim_recovery_stale",
                    "the exact current accepted state and complete history",
                    "stale or mismatched recovery boundary");
            }

            var commandRoot = ReadOptionalPersistedRoot(
                AcceptedMechanicsPlan.WoundCommandPath);
            var pendingRoot = ReadOptionalPersistedRoot(
                WoundAcceptedTurnSnapshotContract.PendingResolutionPath);
            var catalog = MortalWoundTreatmentPersistedRequestCatalog.Parse(
                commandRoot,
                pendingRoot,
                history);
            if (!catalog.IsValid)
                return catalog;

            var recovery = AcceptedTurnAuthorityRegistry
                .RestoreMortalWoundProcedureClaims(
                    _fileSystem,
                    _writeLease,
                    this,
                    history,
                    PersistedProcedureClaimRecoveryCapability,
                    catalog.Requests,
                    catalog.HeldRequests,
                    catalog.FinalizedRequests);
            return recovery.IsValid
                ? new MortalWoundTreatmentPersistedRequestCatalogResult(
                    true,
                    Array.Empty<ValidationIssue>(),
                    recovery.Requests,
                    recovery.HeldRequests,
                    recovery.FinalizedRequests)
                : new MortalWoundTreatmentPersistedRequestCatalogResult(
                    false,
                    recovery.Issues,
                    Array.Empty<MortalWoundTreatmentAttemptRequest>(),
                    Array.Empty<MortalWoundTreatmentAttemptRequest>(),
                    Array.Empty<MortalWoundTreatmentAttemptRequest>());
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or ObjectDisposedException or
                ArgumentException or IOException or UnauthorizedAccessException or
                JsonException)
        {
            return PersistedRecoveryFailure(
                "mortal_wound_treatment_procedure_claim_recovery_invalid",
                "strict current durable command, pending, and history roots",
                exception.GetType().Name);
        }
    }

    private JsonElement? ReadOptionalPersistedRoot(string logicalPath)
    {
        var physicalPath = _fileSystem.ResolvePath(logicalPath);
        if (!File.Exists(physicalPath))
            return null;
        using var document = JsonDocument.Parse(File.ReadAllText(physicalPath));
        return document.RootElement.Clone();
    }

    private static MortalWoundTreatmentPersistedRequestCatalogResult
        PersistedRecoveryFailure(
            string code,
            string expected,
            string actual) => new(
            false,
            new[]
            {
                new ValidationIssue(
                    AcceptedMechanicsPlan.WoundCommandPath,
                    IssueSeverity.Error,
                    "The persisted Mortal wound-treatment requests cannot be restored.",
                    code: code,
                    actor: "Client",
                    section: "wound_materialization",
                    expected: expected,
                    actual: actual)
            },
            Array.Empty<MortalWoundTreatmentAttemptRequest>(),
            Array.Empty<MortalWoundTreatmentAttemptRequest>(),
            Array.Empty<MortalWoundTreatmentAttemptRequest>());

    internal bool RollbackNewProcedureReservations(
        MortalWoundProcedureDiceReservation diceReservation,
        MortalWoundCriticalReactionReservation? criticalReactionReservation,
        MortalWoundCriticalReactionReservationAgreement? criticalReactionAgreement,
        MortalWoundProcedureReservationOwnership ownership) =>
        AcceptedTurnAuthorityRegistry.RollbackNewMortalWoundProcedureReservations(
            _fileSystem,
            _writeLease,
            this,
            diceReservation,
            criticalReactionReservation,
            criticalReactionAgreement,
            ownership);

    internal bool RollbackNewProcedureTreatmentReservations(
        object resourceReservationCapability,
        MortalWoundProcedureDiceReservation diceReservation,
        MortalWoundCriticalReactionReservation? criticalReactionReservation,
        MortalWoundCriticalReactionReservationAgreement? criticalReactionAgreement,
        MortalWoundProcedureReservationOwnership procedureOwnership,
        MortalWoundTreatmentResourceReservationOwnership resourceOwnership,
        MortalWoundTreatmentResourceReservationAuthority resourceAuthority) =>
        AcceptedTurnAuthorityRegistry
            .RollbackNewMortalWoundProcedureTreatmentReservations(
                _fileSystem,
                _writeLease,
                this,
                resourceReservationCapability,
                diceReservation,
                criticalReactionReservation,
                criticalReactionAgreement,
                procedureOwnership,
                resourceOwnership,
                resourceAuthority);

    internal MortalWoundTreatmentResourceReservationResult ReserveTreatmentResources(
        object reservationCapability,
        MortalWoundTreatmentAttemptCoordinates coordinates,
        string mode,
        MortalWoundTreatmentModeAuthority modeAuthority,
        MortalWoundTreatmentResourceReservationAuthority candidate) =>
        AcceptedTurnAuthorityRegistry.ReserveMortalWoundTreatmentResources(
            _fileSystem,
            _writeLease,
            this,
            reservationCapability,
            coordinates,
            mode,
            modeAuthority,
            candidate);

    internal bool RollbackNewTreatmentResources(
        object reservationCapability,
        MortalWoundTreatmentResourceReservationOwnership ownership,
        MortalWoundTreatmentResourceReservationAuthority authority) =>
        AcceptedTurnAuthorityRegistry.RollbackNewMortalWoundTreatmentResources(
            _fileSystem,
            _writeLease,
            this,
            reservationCapability,
            ownership,
            authority);

    internal MortalWoundTreatmentResourceLifecycleResult
        ConfirmPersistedTreatmentResources(
            object lifecycleCapability,
            IReadOnlyList<MortalWoundTreatmentAttemptRequest> requests) =>
        AcceptedTurnAuthorityRegistry
            .ConfirmPersistedMortalWoundTreatmentResources(
                _fileSystem,
                _writeLease,
                this,
                lifecycleCapability,
                requests);

    internal MortalWoundTreatmentResourceLifecycleResult ReleaseTreatmentResources(
        object lifecycleCapability,
        IReadOnlyList<MortalWoundTreatmentAttemptRequest> requests,
        string reason) =>
        AcceptedTurnAuthorityRegistry.ReleaseMortalWoundTreatmentResources(
            _fileSystem,
            _writeLease,
            this,
            lifecycleCapability,
            requests,
            reason);

    internal MortalWoundTreatmentResourceLifecycleResult CommitTreatmentResources(
        object lifecycleCapability,
        MortalWoundTreatmentResourceFinalization finalization) =>
        AcceptedTurnAuthorityRegistry.CommitMortalWoundTreatmentResources(
            _fileSystem,
            _writeLease,
            this,
            lifecycleCapability,
            finalization);

    internal bool TryReadProcedureDicePool(
        object readCapability,
        out int[] acceptedD20EventValues,
        out string poolFingerprint)
    {
        if (!AcceptedTurnAuthorityRegistry.IsProcedureDicePoolReadCapability(
                readCapability))
        {
            acceptedD20EventValues = Array.Empty<int>();
            poolFingerprint = string.Empty;
            return false;
        }

        acceptedD20EventValues = _acceptedD20EventValues.ToArray();
        poolFingerprint = _acceptedD20PoolFingerprint;
        return true;
    }

    internal bool MatchesProcedureDiceEvidence(
        IReadOnlyList<int>? sourceIndices,
        IReadOnlyList<int>? sourceRolls)
    {
        if (sourceIndices is null ||
            sourceRolls is null ||
            sourceIndices.Count != sourceRolls.Count ||
            sourceIndices.Count is < 1 or > 2)
        {
            return false;
        }

        for (var offset = 0; offset < sourceIndices.Count; offset++)
        {
            var sourceIndex = sourceIndices[offset];
            if (sourceIndex < 0 ||
                sourceIndex >= _acceptedD20EventValues.Length ||
                sourceRolls[offset] is < 1 or > 20 ||
                _acceptedD20EventValues[sourceIndex] != sourceRolls[offset])
            {
                return false;
            }
        }
        return true;
    }

    internal bool MatchesCurrentWound(WoundMaterializationEnvelope? wound)
    {
        if (wound is null)
            return false;
        try
        {
            return string.Equals(
                       WoundIdentityState.ComputeSemanticFingerprint(wound),
                       WoundFingerprint,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       WoundMaterializationContract.SerializeCanonical(wound),
                       WoundMaterializationContract.SerializeCanonical(_currentWound),
                       StringComparison.Ordinal);
        }
        catch (Exception exception) when (exception is ArgumentException or
                                           InvalidOperationException or
                                           OverflowException)
        {
            return false;
        }
    }

    internal bool MatchesCompleteHistory(WoundHistoryParseResult? history)
    {
        if (history is not { IsValid: true, State: not null })
            return false;
        try
        {
            return string.Equals(
                Hash(
                    "mortal_wound_treatment_history",
                    WoundHistoryState.SerializeCanonical(history.State)),
                HistoryFingerprint,
                StringComparison.Ordinal);
        }
        catch (Exception exception) when (exception is ArgumentException or
                                           InvalidOperationException or
                                           OverflowException)
        {
            return false;
        }
    }

    private MortalWoundTreatmentCapabilityCatalogReadResult
        ReadPlayerCapabilityCatalog(AcceptedMechanicsPlan? publicationPlan)
    {
        if (!TryReadCapabilityRoot(
                PlayerActiveSkillsPath,
                publicationPlan,
                out var activeRoot) ||
            !TryReadCapabilityRoot(
                PlayerPassiveSkillsPath,
                publicationPlan,
                out var passiveRoot))
        {
            return new MortalWoundTreatmentCapabilityCatalogReadResult(
                publicationPlan is null
                    ? MortalWoundTreatmentCapabilityCatalogReadStatus.SourceInvalid
                    : MortalWoundTreatmentCapabilityCatalogReadStatus.PublicationMismatch);
        }

        return ParseLiveCapabilityCatalog(
            "player",
            "player_current",
            activeRoot!,
            PlayerActiveSkillsPath,
            passiveRoot!,
            PlayerPassiveSkillsPath);
    }

    private MortalWoundTreatmentCapabilityCatalogReadResult ReadNpcCapabilityCatalog(
        string ownerId,
        AcceptedMechanicsPlan? publicationPlan)
    {
        if (!TryReadCapabilityRoot(NpcCorePath, publicationPlan, out var npcRoot))
        {
            return new MortalWoundTreatmentCapabilityCatalogReadResult(
                publicationPlan is null
                    ? MortalWoundTreatmentCapabilityCatalogReadStatus.SourceInvalid
                    : MortalWoundTreatmentCapabilityCatalogReadStatus.PublicationMismatch);
        }

        var owners = new List<(string Section, JsonObject Actor)>();
        var ownerConfusableKey = MortalLocationIdentityState.BuildConfusableKey(ownerId);
        var hasConfusableSibling = false;
        foreach (var section in GuardianPolicyContracts.NpcCoreCanonicalNpcObjectSections)
        {
            if (npcRoot![section] is not JsonArray rows)
                continue;
            foreach (var npc in rows.OfType<JsonObject>())
            {
                if (GuardianPolicyContracts.TryResolveStrictPermanentNpcId(
                        npc,
                        out var npcId))
                {
                    if (string.Equals(npcId, ownerId, StringComparison.Ordinal))
                    {
                        owners.Add((section, npc));
                    }
                    else if (string.Equals(
                                 MortalLocationIdentityState.BuildConfusableKey(npcId),
                                 ownerConfusableKey,
                                 StringComparison.Ordinal))
                    {
                        hasConfusableSibling = true;
                    }
                }
            }
        }

        if (hasConfusableSibling)
        {
            return new MortalWoundTreatmentCapabilityCatalogReadResult(
                MortalWoundTreatmentCapabilityCatalogReadStatus.SourceAmbiguous);
        }
        if (owners.Count == 0)
        {
            return new MortalWoundTreatmentCapabilityCatalogReadResult(
                MortalWoundTreatmentCapabilityCatalogReadStatus.SourceOwnerMismatch);
        }
        if (owners.GroupBy(static owner => owner.Section, StringComparer.Ordinal)
                .Any(static group => group.Count() != 1) ||
            owners.Skip(1).Any(owner =>
                !JsonNode.DeepEquals(owners[0].Actor, owner.Actor)))
        {
            return new MortalWoundTreatmentCapabilityCatalogReadResult(
                MortalWoundTreatmentCapabilityCatalogReadStatus.SourceAmbiguous);
        }

        var activeRoot = new JsonObject
        {
            ["activeSkills"] = owners[0].Actor["activeSkills"]?.DeepClone() ?? new JsonArray()
        };
        var passiveRoot = new JsonObject
        {
            ["passiveSkills"] = owners[0].Actor["passiveSkills"]?.DeepClone() ?? new JsonArray()
        };
        return ParseLiveCapabilityCatalog(
            "npc",
            ownerId,
            activeRoot,
            NpcCorePath + $".npc[{ownerId}].activeSkills",
            passiveRoot,
            NpcCorePath + $".npc[{ownerId}].passiveSkills");
    }

    private static MortalWoundTreatmentCapabilityCatalogReadResult
        ParseLiveCapabilityCatalog(
            string ownerKind,
            string ownerId,
            JsonObject activeRoot,
            string activePath,
            JsonObject passiveRoot,
            string passivePath)
    {
        var parsed = MortalWoundTreatmentCapabilityContract.ParseActorCatalog(
            ownerKind,
            ownerId,
            activeRoot,
            activePath,
            passiveRoot,
            passivePath);
        if (!parsed.IsValid)
        {
            var ambiguous = parsed.Issues.Any(issue =>
                issue.Code?.Contains("confusable", StringComparison.Ordinal) == true);
            return new MortalWoundTreatmentCapabilityCatalogReadResult(
                ambiguous
                    ? MortalWoundTreatmentCapabilityCatalogReadStatus.SourceAmbiguous
                    : MortalWoundTreatmentCapabilityCatalogReadStatus.SourceInvalid);
        }

        var sources = parsed.Sources.Select(source =>
        {
            var root = source.SkillKind == "active" ? activeRoot : passiveRoot;
            var preferredArray = source.SkillKind == "active"
                ? "activeSkillChanges"
                : "passiveSkillChanges";
            var alternateArray = source.SkillKind == "active"
                ? "activeSkills"
                : "passiveSkills";
            var arrayName = root[preferredArray] is JsonArray
                ? preferredArray
                : alternateArray;
            var rows = root[arrayName] as JsonArray;
            var exactRows = rows?.OfType<JsonObject>().Where(row =>
                row["skillId"] is JsonValue value &&
                value.TryGetValue<string>(out var skillId) &&
                string.Equals(skillId, source.SkillId, StringComparison.Ordinal))
                .ToArray() ?? Array.Empty<JsonObject>();
            if (exactRows.Length != 1)
                return null;
            var row = exactRows[0];
            var lifecycle = row["lifecycle"] is JsonValue lifecycleValue &&
                            lifecycleValue.TryGetValue<string>(out var lifecycleText)
                ? lifecycleText
                : row.ContainsKey("lifecycle") ? "invalid" : "active";
            var active = row["active"] is JsonValue activeValue &&
                         activeValue.TryGetValue<bool>(out var activeFlag)
                ? activeFlag
                : !row.ContainsKey("active");
            return source with
            {
                Lifecycle = lifecycle,
                Active = active
            };
        }).ToArray();
        if (sources.Any(static source => source is null))
        {
            return new MortalWoundTreatmentCapabilityCatalogReadResult(
                MortalWoundTreatmentCapabilityCatalogReadStatus.SourceInvalid);
        }
        return new MortalWoundTreatmentCapabilityCatalogReadResult(
            MortalWoundTreatmentCapabilityCatalogReadStatus.Success,
            sources.Select(static source => source!));
    }

    private bool TryReadCapabilityRoot(
        string path,
        AcceptedMechanicsPlan? publicationPlan,
        out JsonObject? root)
    {
        root = null;
        if (publicationPlan is not null &&
            publicationPlan.TouchedPaths.Contains(path, StringComparer.Ordinal))
        {
            if (!publicationPlan.OwnerCompanionAfterImages.TryGetValue(
                    path,
                    out var afterImage))
            {
                return false;
            }
            root = afterImage;
            return true;
        }

        var bytes = _fileSystem.ReadFileBytesAsync(_writeLease, path)
            .GetAwaiter()
            .GetResult();
        if (bytes is null)
            return false;
        var issues = new List<ValidationIssue>();
        return TryParseObject(bytes, path, issues, out root) &&
               issues.Count == 0;
    }

    private bool MatchesValidatedPublicationPlan(AcceptedMechanicsPlan publicationPlan)
    {
        if (!AcceptedMechanicsPlanAuthority.TryPeekValidated(
                _fileSystem,
                _writeLease,
                out var binding,
                out var cached) ||
            !cached.Success ||
            !ReferenceEquals(cached.Plan, publicationPlan) ||
            !string.Equals(binding.SessionId, _binding.SessionId, StringComparison.Ordinal) ||
            !string.Equals(binding.RequestId, _binding.RequestId, StringComparison.Ordinal) ||
            !string.Equals(binding.SnapshotToken, _binding.SnapshotToken, StringComparison.Ordinal) ||
            !string.Equals(binding.Realm, _binding.Realm, StringComparison.Ordinal) ||
            binding.Turn != _binding.Turn ||
            binding.WoundInput is not { } woundInput)
        {
            return false;
        }

        return BindingAgrees(_binding, woundInput.Binding);
    }

    private static PendingTurnSnapshotPathSelection BuildPathSelection(
        MortalWoundTreatmentAuthority.Context context,
        ICollection<ValidationIssue> issues)
    {
        var required = new HashSet<string>(StringComparer.Ordinal)
        {
            WorldTimePath,
            CurrentLocationPath,
            MortalLocationMaterializationContract.WorldMapPath,
            MortalLocationIdentityState.StatePath,
            PlayerActiveSkillsPath,
            PlayerPassiveSkillsPath,
            PlayerSkillMasteryPath,
            NpcCorePath,
            ItemIdentityPath,
            ResourceMaterializationContract.DefinitionsPath,
            ResourceMaterializationContract.StatePath,
            ResourceMaterializationContract.HistoryPath,
            CanonicalResourceOwnerAuthorityComposer.AuthorityPath,
            WoundIdentityState.StatePath,
            WoundHistoryState.HistoryPath
        };
        var targetPaths = context.TargetKind switch
        {
            "player" => new[] { WoundCarrierCatalog.PlayerPath },
            "npc" => new[] { WoundCarrierCatalog.NpcPath },
            "combatant" or "combatant_member" => Array.Empty<string>(),
            _ => Array.Empty<string>()
        };
        if (targetPaths.Length == 0 && context.TargetKind is not ("combatant" or "combatant_member"))
        {
            issues.Add(Issue(
                "treatmentSelection.target",
                "mortal_wound_treatment_accepted_state_target_carrier_missing",
                "one signed canonical carrier path for the selected target kind",
                context.TargetKind));
        }
        foreach (var path in targetPaths)
            required.Add(path);
        return new PendingTurnSnapshotPathSelection(
            required.OrderBy(static path => path, StringComparer.Ordinal),
            OptionalAuthorityPaths
                .Where(path => !required.Contains(path))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(static path => path, StringComparer.Ordinal));
    }

    private static void ValidateTargetCarrierCoverage(
        MortalWoundTreatmentAuthority.Context context,
        IReadOnlyCollection<string> covered,
        ICollection<ValidationIssue> issues)
    {
        if (context.TargetKind is not ("combatant" or "combatant_member"))
            return;
        if (covered.Contains(WoundCarrierCatalog.EnemiesPath, StringComparer.Ordinal) ||
            covered.Contains(WoundCarrierCatalog.AlliesPath, StringComparer.Ordinal))
        {
            return;
        }
        issues.Add(Issue(
            "treatmentSelection.target",
            "mortal_wound_treatment_accepted_state_target_carrier_missing",
            "one signed canonical carrier path for the selected target kind",
            context.TargetKind));
    }

    private static bool TryComposeBinding(
        PendingTurnSnapshotReadAuthority signed,
        ICollection<ValidationIssue> issues,
        out WoundAcceptedTurnBinding? binding,
        out string requestFingerprint)
    {
        binding = null;
        requestFingerprint = string.Empty;
        var dice = signed.AcceptedD20EventValues;
        if (dice.Count == 0 || dice.Any(static value => value is < 1 or > 20))
        {
            issues.Add(Issue(
                LiveTurnPreparationService.TurnRequestPath,
                "mortal_wound_treatment_accepted_event_missing",
                "one complete current accepted-turn request/event authority",
                "missing or malformed signed d20 event source"));
            return false;
        }
        requestFingerprint = Hash(
            "mortal_wound_treatment_request_event_source",
            signed.SessionId,
            signed.RequestId,
            Number(signed.TurnNumber),
            string.Join(",", dice.Select(static die =>
                die.ToString(CultureInfo.InvariantCulture))));

        var composed = WoundAcceptedEventAuthorityComposer
            .ComposeDefaultAcceptedTurn(
                signed.SessionId,
                signed.RequestId,
                signed.SnapshotToken,
                signed.TurnNumber);
        AddIssues(issues, composed.Issues);
        if (!composed.Success)
            return false;
        binding = new WoundAcceptedTurnBinding(
            signed.SessionId,
            signed.RequestId,
            signed.SnapshotToken,
            signed.Realm,
            signed.TurnNumber,
            composed.Events,
            composed.EventsFingerprint);
        return true;
    }

    private static bool TryReadWorldMinute(
        JsonObject root,
        ICollection<ValidationIssue> issues,
        out long minute)
    {
        minute = 0;
        if (root["currentTimeInMinutes"] is JsonValue value &&
            value.TryGetValue<long>(out minute) && minute >= 0)
        {
            return true;
        }
        issues.Add(Issue(
            WorldTimePath + ".currentTimeInMinutes",
            "mortal_wound_treatment_accepted_state_clock_invalid",
            "one non-negative canonical world minute",
            Describe(root["currentTimeInMinutes"])));
        return false;
    }

    private static IReadOnlyList<MortalWoundTreatmentCapabilitySkillSource>
        ParsePlayerCapabilities(
            IReadOnlyDictionary<string, JsonObject> roots,
            ICollection<ValidationIssue> issues)
    {
        var result = MortalWoundTreatmentCapabilityContract.ParseActorCatalog(
            "player",
            "player_current",
            roots[PlayerActiveSkillsPath],
            PlayerActiveSkillsPath,
            roots[PlayerPassiveSkillsPath],
            PlayerPassiveSkillsPath);
        AddIssues(issues, result.Issues);
        return result.Sources;
    }

    private static IReadOnlyList<MortalWoundTreatmentCapabilitySkillSource>
        ParseNpcCapabilities(JsonObject npcRoot, ICollection<ValidationIssue> issues)
    {
        var result = new List<MortalWoundTreatmentCapabilitySkillSource>();
        var canonical = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        foreach (var section in GuardianPolicyContracts.NpcCoreCanonicalNpcObjectSections)
        {
            if (npcRoot[section] is not JsonArray rows)
                continue;
            foreach (var npc in rows.OfType<JsonObject>())
            {
                if (!GuardianPolicyContracts.TryResolveStrictPermanentNpcId(npc, out var npcId) ||
                    canonical.ContainsKey(npcId))
                    continue;
                canonical.Add(npcId, npc);
            }
        }
        foreach (var (npcId, npc) in canonical)
        {
            var active = new JsonObject
            {
                ["activeSkills"] = npc["activeSkills"]?.DeepClone() ?? new JsonArray()
            };
            var passive = new JsonObject
            {
                ["passiveSkills"] = npc["passiveSkills"]?.DeepClone() ?? new JsonArray()
            };
            var parsed = MortalWoundTreatmentCapabilityContract.ParseActorCatalog(
                "npc",
                npcId,
                active,
                NpcCorePath + $".npc[{npcId}].activeSkills",
                passive,
                NpcCorePath + $".npc[{npcId}].passiveSkills");
            AddIssues(issues, parsed.Issues);
            result.AddRange(parsed.Sources);
        }
        return result;
    }

    private static EffectMechanicsSnapshot ComposeEffectMechanics(
        IReadOnlyDictionary<string, JsonObject> roots,
        ICollection<ValidationIssue> issues)
    {
        var skillScopeAuthority = EffectRollSkillScopeAuthority.Build(
            new EffectRollSkillScopeAuthorityInput(
                new Dictionary<string, JsonNode?>(StringComparer.Ordinal)
                {
                    [PlayerActiveSkillsPath] = Get(roots, PlayerActiveSkillsPath),
                    [PlayerPassiveSkillsPath] = Get(roots, PlayerPassiveSkillsPath),
                    [NpcCorePath] = Get(roots, NpcCorePath)
                },
                new Dictionary<string, JsonNode?>(StringComparer.Ordinal)
                {
                    [PlayerActiveSkillsPath] = Get(roots, PlayerActiveSkillsPath),
                    [PlayerPassiveSkillsPath] = Get(roots, PlayerPassiveSkillsPath),
                    [NpcCorePath] = Get(roots, NpcCorePath)
                }));
        var snapshot = EffectMechanicsSnapshot.Build(new EffectMechanicsInput(
            new EffectCarrierCatalogInput(
                Get(roots, EffectCarrierCatalog.PlayerPath),
                Get(roots, EffectCarrierCatalog.NpcPath),
                Get(roots, EffectCarrierCatalog.EnemiesPath),
                Get(roots, EffectCarrierCatalog.AlliesPath),
                Get(roots, EffectCarrierCatalog.AfterlifeProfilesPath),
                Get(roots, EffectCarrierCatalog.SpiritualConflictPath)),
            Get(roots, EffectIdentityState.StatePath),
            skillScopeAuthority));
        AddIssues(issues, snapshot.Issues);
        return snapshot;
    }

    private static IReadOnlyList<MortalWoundTreatmentAuthority.Effect>
        ComposeRequirementEffects(EffectMechanicsSnapshot snapshot) =>
        snapshot.Effects.Select(effect =>
        {
            var state = effect.CanonicalEffect.ValueKind == JsonValueKind.Object &&
                        effect.CanonicalEffect.TryGetProperty("state", out var stateNode) &&
                        stateNode.ValueKind == JsonValueKind.String
                ? stateNode.GetString() ?? "active"
                : "active";
            return new MortalWoundTreatmentAuthority.Effect(
                effect.EffectId,
                effect.EffectId,
                effect.Realm,
                effect.TargetKind,
                effect.TargetId,
                state,
                state is "removed" or "retired" ? "retired" : "active",
                string.Equals(state, "active", StringComparison.Ordinal));
        }).ToArray();

    private static JsonObject ToJson(MortalWoundTreatmentAuthority.Item value) => new()
    {
        ["itemId"] = value.ItemId,
        ["displayName"] = value.DisplayName,
        ["realm"] = value.Realm,
        ["ownerKind"] = value.OwnerKind,
        ["ownerId"] = value.OwnerId,
        ["count"] = value.Count,
        ["availableCount"] = value.AvailableCount,
        ["reservationState"] = value.ReservationState,
        ["lifecycle"] = value.Lifecycle,
        ["active"] = value.Active
    };

    private static JsonObject ToJson(MortalWoundTreatmentAuthority.Resource value) => new()
    {
        ["resourceRef"] = value.ResourceRef,
        ["displayName"] = value.DisplayName,
        ["realm"] = value.Realm,
        ["ownerKind"] = value.OwnerKind,
        ["ownerId"] = value.OwnerId,
        ["currentValue"] = value.CurrentValue,
        ["availableValue"] = value.AvailableValue,
        ["reservationState"] = value.ReservationState,
        ["lifecycle"] = value.Lifecycle,
        ["active"] = value.Active
    };

    private static JsonObject ToJson(MortalWoundTreatmentAuthority.Actor value) => new()
    {
        ["actorKind"] = value.ActorKind,
        ["actorId"] = value.ActorId,
        ["displayName"] = value.DisplayName,
        ["realm"] = value.Realm,
        ["currentLocationId"] = value.CurrentLocationId,
        ["lifecycle"] = value.Lifecycle,
        ["active"] = value.Active,
        ["reachable"] = value.Reachable,
        ["skills"] = new JsonArray(value.Skills.Select(skill => (JsonNode)new JsonObject
        {
            ["skillId"] = skill.SkillId,
            ["capabilityRef"] = skill.CapabilityRef,
            ["displayName"] = skill.DisplayName,
            ["tier"] = skill.Tier,
            ["lifecycle"] = skill.Lifecycle,
            ["active"] = skill.Active
        }).ToArray()),
        ["capabilities"] = new JsonArray(value.Capabilities.Select(capability =>
            (JsonNode)new JsonObject
            {
                ["capabilityRef"] = capability.CapabilityRef,
                ["displayName"] = capability.DisplayName,
                ["lifecycle"] = capability.Lifecycle,
                ["active"] = capability.Active
            }).ToArray()),
        ["consents"] = new JsonArray(value.Consents.Select(consent =>
            (JsonNode)new JsonObject
            {
                ["consentRef"] = consent.ConsentRef,
                ["displayName"] = consent.DisplayName,
                ["providerKind"] = consent.ProviderKind,
                ["providerId"] = consent.ProviderId,
                ["targetKind"] = consent.TargetKind,
                ["targetId"] = consent.TargetId,
                ["status"] = consent.Status,
                ["lifecycle"] = consent.Lifecycle,
                ["active"] = consent.Active
            }).ToArray())
    };

    private static JsonObject ToJson(MortalWoundTreatmentAuthority.Facility value) => new()
    {
        ["facilityId"] = value.FacilityId,
        ["displayName"] = value.DisplayName,
        ["realm"] = value.Realm,
        ["locationId"] = value.LocationId,
        ["lifecycle"] = value.Lifecycle,
        ["active"] = value.Active,
        ["available"] = value.Available
    };

    private static JsonObject ToJson(MortalWoundTreatmentAuthority.Location value) => new()
    {
        ["locationId"] = value.LocationId,
        ["displayName"] = value.DisplayName,
        ["realm"] = value.Realm,
        ["lifecycle"] = value.Lifecycle,
        ["active"] = value.Active,
        ["presentActors"] = new JsonArray(value.PresentActors.Select(actor =>
            (JsonNode)new JsonObject
            {
                ["actorKind"] = actor.ActorKind,
                ["actorId"] = actor.ActorId
            }).ToArray())
    };

    private static JsonObject ToJson(MortalWoundTreatmentAuthority.Quest value) => new()
    {
        ["questId"] = value.QuestId,
        ["displayName"] = value.DisplayName,
        ["realm"] = value.Realm,
        ["state"] = value.State,
        ["lifecycle"] = value.Lifecycle,
        ["active"] = value.Active
    };

    private static JsonObject ToJson(MortalWoundTreatmentAuthority.Effect value) => new()
    {
        ["effectId"] = value.EffectId,
        ["displayName"] = value.DisplayName,
        ["realm"] = value.Realm,
        ["targetKind"] = value.TargetKind,
        ["targetId"] = value.TargetId,
        ["state"] = value.State,
        ["lifecycle"] = value.Lifecycle,
        ["active"] = value.Active
    };

    private static JsonObject ToJson(MortalWoundTreatmentAuthority.EnvironmentState value) => new()
    {
        ["environmentId"] = value.EnvironmentId,
        ["displayName"] = value.DisplayName,
        ["realm"] = value.Realm,
        ["locationId"] = value.LocationId,
        ["state"] = value.State,
        ["lifecycle"] = value.Lifecycle,
        ["active"] = value.Active
    };

    private static JsonObject BuildMechanicalRequirementProjection(
        string snapshotToken,
        IReadOnlyList<MortalWoundTreatmentAuthority.Item> items,
        IReadOnlyList<MortalWoundTreatmentAuthority.Resource> resources,
        IReadOnlyList<MortalWoundTreatmentAuthority.Actor> actors,
        IReadOnlyList<MortalWoundTreatmentAuthority.Facility> facilities,
        IReadOnlyList<MortalWoundTreatmentAuthority.Location> locations,
        IReadOnlyList<MortalWoundTreatmentAuthority.Quest> quests,
        IReadOnlyList<MortalWoundTreatmentAuthority.Effect> effects,
        IReadOnlyList<MortalWoundTreatmentAuthority.EnvironmentState> environments) => new()
    {
        ["schemaVersion"] = 1,
        ["snapshotToken"] = snapshotToken,
        ["items"] = new JsonArray(items.Select(ToMechanicalJson).Cast<JsonNode?>().ToArray()),
        ["resources"] = new JsonArray(resources.Select(ToMechanicalJson).Cast<JsonNode?>().ToArray()),
        ["actors"] = new JsonArray(actors.Select(ToMechanicalJson).Cast<JsonNode?>().ToArray()),
        ["facilities"] = new JsonArray(facilities.Select(ToMechanicalJson).Cast<JsonNode?>().ToArray()),
        ["locations"] = new JsonArray(locations.Select(ToMechanicalJson).Cast<JsonNode?>().ToArray()),
        ["quests"] = new JsonArray(quests.Select(ToMechanicalJson).Cast<JsonNode?>().ToArray()),
        ["effects"] = new JsonArray(effects.Select(ToMechanicalJson).Cast<JsonNode?>().ToArray()),
        ["environments"] = new JsonArray(environments.Select(ToMechanicalJson).Cast<JsonNode?>().ToArray())
    };

    private static JsonObject ToMechanicalJson(MortalWoundTreatmentAuthority.Item value) => new()
    {
        ["itemId"] = value.ItemId,
        ["realm"] = value.Realm,
        ["ownerKind"] = value.OwnerKind,
        ["ownerId"] = value.OwnerId,
        ["count"] = value.Count,
        ["availableCount"] = value.AvailableCount,
        ["reservationState"] = value.ReservationState,
        ["lifecycle"] = value.Lifecycle,
        ["active"] = value.Active
    };

    private static JsonObject ToMechanicalJson(MortalWoundTreatmentAuthority.Resource value) => new()
    {
        ["resourceRef"] = value.ResourceRef,
        ["realm"] = value.Realm,
        ["ownerKind"] = value.OwnerKind,
        ["ownerId"] = value.OwnerId,
        ["currentValue"] = value.CurrentValue,
        ["availableValue"] = value.AvailableValue,
        ["reservationState"] = value.ReservationState,
        ["lifecycle"] = value.Lifecycle,
        ["active"] = value.Active
    };

    private static JsonObject ToMechanicalJson(MortalWoundTreatmentAuthority.Actor value) => new()
    {
        ["actorKind"] = value.ActorKind,
        ["actorId"] = value.ActorId,
        ["realm"] = value.Realm,
        ["currentLocationId"] = value.CurrentLocationId,
        ["lifecycle"] = value.Lifecycle,
        ["active"] = value.Active,
        ["reachable"] = value.Reachable,
        ["skills"] = new JsonArray(value.Skills.Select(skill => (JsonNode)new JsonObject
        {
            ["skillId"] = skill.SkillId,
            ["capabilityRef"] = skill.CapabilityRef,
            ["tier"] = skill.Tier,
            ["lifecycle"] = skill.Lifecycle,
            ["active"] = skill.Active
        }).ToArray()),
        ["capabilities"] = new JsonArray(value.Capabilities.Select(capability =>
            (JsonNode)new JsonObject
            {
                ["capabilityRef"] = capability.CapabilityRef,
                ["lifecycle"] = capability.Lifecycle,
                ["active"] = capability.Active
            }).ToArray()),
        ["consents"] = new JsonArray(value.Consents.Select(consent =>
            (JsonNode)new JsonObject
            {
                ["consentRef"] = consent.ConsentRef,
                ["providerKind"] = consent.ProviderKind,
                ["providerId"] = consent.ProviderId,
                ["targetKind"] = consent.TargetKind,
                ["targetId"] = consent.TargetId,
                ["status"] = consent.Status,
                ["lifecycle"] = consent.Lifecycle,
                ["active"] = consent.Active
            }).ToArray())
    };

    private static JsonObject ToMechanicalJson(MortalWoundTreatmentAuthority.Facility value) => new()
    {
        ["facilityId"] = value.FacilityId,
        ["realm"] = value.Realm,
        ["locationId"] = value.LocationId,
        ["lifecycle"] = value.Lifecycle,
        ["active"] = value.Active,
        ["available"] = value.Available
    };

    private static JsonObject ToMechanicalJson(MortalWoundTreatmentAuthority.Location value) => new()
    {
        ["locationId"] = value.LocationId,
        ["realm"] = value.Realm,
        ["lifecycle"] = value.Lifecycle,
        ["active"] = value.Active,
        ["presentActors"] = new JsonArray(value.PresentActors.Select(actor =>
            (JsonNode)new JsonObject
            {
                ["actorKind"] = actor.ActorKind,
                ["actorId"] = actor.ActorId
            }).ToArray())
    };

    private static JsonObject ToMechanicalJson(MortalWoundTreatmentAuthority.Quest value) => new()
    {
        ["questId"] = value.QuestId,
        ["realm"] = value.Realm,
        ["state"] = value.State,
        ["lifecycle"] = value.Lifecycle,
        ["active"] = value.Active
    };

    private static JsonObject ToMechanicalJson(MortalWoundTreatmentAuthority.Effect value) => new()
    {
        ["effectId"] = value.EffectId,
        ["realm"] = value.Realm,
        ["targetKind"] = value.TargetKind,
        ["targetId"] = value.TargetId,
        ["state"] = value.State,
        ["lifecycle"] = value.Lifecycle,
        ["active"] = value.Active
    };

    private static JsonObject ToMechanicalJson(MortalWoundTreatmentAuthority.EnvironmentState value) => new()
    {
        ["environmentId"] = value.EnvironmentId,
        ["realm"] = value.Realm,
        ["locationId"] = value.LocationId,
        ["state"] = value.State,
        ["lifecycle"] = value.Lifecycle,
        ["active"] = value.Active
    };

    private static string ComputeEffectFingerprint(EffectMechanicsSnapshot snapshot)
    {
        var fields = new List<string?>
        {
            "mortal_wound_treatment_effect_mechanics",
            "1",
            Number(snapshot.Effects.Count),
            Number(snapshot.Components.Count)
        };
        foreach (var effect in snapshot.Effects)
        {
            fields.Add(effect.EffectId);
            fields.Add(effect.Realm);
            fields.Add(effect.TargetKind);
            fields.Add(effect.TargetId);
            fields.Add(effect.OwnerKind);
            fields.Add(effect.OwnerId);
            fields.Add(effect.Category);
            fields.Add(ReadMechanicalEffectString(effect.CanonicalEffect, "state"));
            fields.Add(ReadMechanicalEffectString(effect.CanonicalEffect, "lifecycle"));
            fields.Add(ReadMechanicalEffectBoolean(effect.CanonicalEffect, "active"));
        }
        foreach (var component in snapshot.Components)
        {
            fields.Add(component.EffectId);
            fields.Add(component.ComponentId);
            fields.Add(component.Realm);
            fields.Add(component.TargetKind);
            fields.Add(component.TargetId);
            fields.Add(component.IsPlayerVisible ? "true" : "false");
            fields.Add(component.Profile);
            fields.Add(Number(component.Priority));
            fields.Add(Number(component.CurrentStacks));
            fields.Add(WoundAcceptedTurnFingerprintWriter.CanonicalJson(
                JsonNode.Parse(component.Payload.GetRawText())));
        }
        fields.Add(Number(snapshot.FateShieldReactionCandidates.Count));
        foreach (var candidate in snapshot.FateShieldReactionCandidates)
        {
            fields.Add(candidate.EffectId);
            fields.Add(candidate.TriggerId);
            fields.Add(Number(candidate.CreatedAtTurn));
            fields.Add(candidate.AcceptedEffectFingerprint);
        }
        return WoundAcceptedTurnFingerprintWriter.Compute(fields);
    }

    private static string? ReadMechanicalEffectString(JsonElement effect, string field) =>
        effect.ValueKind == JsonValueKind.Object &&
        effect.TryGetProperty(field, out var value) &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string? ReadMechanicalEffectBoolean(JsonElement effect, string field) =>
        effect.ValueKind == JsonValueKind.Object &&
        effect.TryGetProperty(field, out var value) &&
        value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean() ? "true" : "false"
            : null;

    private static string ComputeCapabilityCatalogFingerprint(
        IReadOnlyList<MortalWoundTreatmentCapabilitySkillSource> sources)
    {
        var fields = new List<string?>
        {
            "mortal_wound_treatment_capability_catalog",
            "1",
            Number(sources.Count)
        };
        foreach (var source in sources.OrderBy(static value => value.OwnerKind, StringComparer.Ordinal)
                     .ThenBy(static value => value.OwnerId, StringComparer.Ordinal)
                     .ThenBy(static value => value.SkillKind, StringComparer.Ordinal)
                     .ThenBy(static value => value.SkillId, StringComparer.Ordinal))
        {
            fields.Add(source.OwnerKind);
            fields.Add(source.OwnerId);
            fields.Add(source.SkillKind);
            fields.Add(source.SkillId);
            fields.Add(source.Lifecycle);
            fields.Add(source.Active ? "true" : "false");
            fields.Add(Number(source.Capabilities.Count));
            foreach (var capability in source.Capabilities)
            {
                fields.Add(Number(capability.SchemaVersion));
                fields.Add(capability.CapabilityRef);
                fields.Add(capability.WoundDomain);
                fields.Add(Number(capability.MinimumSeverityRank));
                fields.Add(Number(capability.MaximumSeverityRank));
                fields.Add(capability.OperationLimits.MayStabilize ? "true" : "false");
                fields.Add(Number(capability.OperationLimits.MaximumRecoveryPoints));
                fields.Add(Number(capability.OperationLimits.MaximumSeverityReductionSteps));
                fields.Add(string.Join(",", capability.OperationLimits.RemovableComplicationKinds));
                fields.Add(capability.OperationLimits.MayHealAtSeverityI ? "true" : "false");
                fields.Add(Number(capability.OperationLimits.MaximumCosmeticHealLegacies));
                fields.Add(Number(capability.OperationLimits.MaximumMechanicalEffectHealLegacies));
            }
        }
        return WoundAcceptedTurnFingerprintWriter.Compute(fields);
    }

    private static string HashBinding(WoundAcceptedTurnBinding binding)
    {
        var fields = new List<string?>
        {
            "mortal_wound_treatment_binding",
            "1",
            binding.SessionId,
            binding.RequestId,
            binding.SnapshotToken,
            binding.Realm,
            Number(binding.Turn),
            binding.AcceptedEventsFingerprint,
            Number(binding.AcceptedEvents.Count)
        };
        foreach (var acceptedEvent in binding.AcceptedEvents)
        {
            fields.Add(acceptedEvent.EventRef);
            fields.Add(acceptedEvent.Kind);
            fields.Add(acceptedEvent.AuthorityId);
            fields.Add(acceptedEvent.SemanticFingerprint);
        }
        return WoundAcceptedTurnFingerprintWriter.Compute(fields);
    }

    private static bool BindingAgrees(
        WoundAcceptedTurnBinding left,
        WoundAcceptedTurnBinding right) =>
        string.Equals(left.SessionId, right.SessionId, StringComparison.Ordinal) &&
        string.Equals(left.RequestId, right.RequestId, StringComparison.Ordinal) &&
        string.Equals(left.SnapshotToken, right.SnapshotToken, StringComparison.Ordinal) &&
        string.Equals(left.Realm, right.Realm, StringComparison.Ordinal) &&
        left.Turn == right.Turn &&
        string.Equals(
            left.AcceptedEventsFingerprint,
            right.AcceptedEventsFingerprint,
            StringComparison.Ordinal) &&
        left.AcceptedEvents.SequenceEqual(right.AcceptedEvents);

    private static bool TryParseObject(
        byte[] bytes,
        string path,
        ICollection<ValidationIssue> issues,
        out JsonObject? root)
    {
        root = null;
        try
        {
            using var document = JsonDocument.Parse(
                CanonicalJsonUtf8.DecodeOneOptionalBom(bytes),
                new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 64
            });
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                HasDuplicateProperty(document.RootElement))
            {
                issues.Add(Issue(
                    path,
                    "mortal_wound_treatment_accepted_state_root_invalid",
                    "one strict object root without duplicate properties",
                    document.RootElement.ValueKind.ToString()));
                return false;
            }
            root = JsonNode.Parse(document.RootElement.GetRawText())!.AsObject();
            return true;
        }
        catch (Exception exception) when (exception is JsonException or
                                           InvalidDataException or
                                           InvalidOperationException or
                                           ArgumentException)
        {
            issues.Add(Issue(
                path,
                "mortal_wound_treatment_accepted_state_root_invalid",
                "one strict canonical object root",
                exception.GetType().Name));
            return false;
        }
    }

    /// <summary>
    /// Resolves a healed wound exclusively from the verified recovery result and terminal identity.
    /// The returned source supports exact receipt replay and cannot authorize fresh recovery.
    /// </summary>
    /// <param name="woundId">
    /// The exact selected wound identifier.
    /// </param>
    /// <param name="identity">
    /// The signed current identity whose complete history agreement was checked.
    /// </param>
    /// <param name="history">
    /// The parsed signed history with validated linked recovery stages.
    /// </param>
    /// <returns>
    /// A detached exact terminal snapshot, or <see langword="null"/> without terminal proof.
    /// </returns>
    private static WoundCarrierOccurrence? ResolveHealedRecoveryReplaySource(
        string woundId, WoundIdentityState identity, WoundHistoryState history)
    {
        if (!identity.TryGetEntry(woundId, out var entry) || entry.Status != "healed")
            return null;
        var candidates = history.Transitions
            .Where(row => row.WoundId == woundId)
            .Select(row => row.TransitionResult).OfType<MortalWoundRecoveryPersistedResult>()
            .Select(result => result.Stages.Last()).Where(stage => stage.Terminal && stage.Kind == "heal" &&
                stage.TransitionId == entry.TerminalTransitionId).ToArray();
        if (candidates.Length != 1)
            return null;
        var wound = WoundAcceptedTurnData.CloneWound(candidates[0].AfterWound)!;
        if (wound.WoundId != woundId || wound.Lifecycle != "healed" ||
            wound.LastTransition.TransitionId != entry.TerminalTransitionId ||
            wound.LastTransition.Ordinal != entry.LastTransitionOrdinal ||
            WoundIdentityState.ComputeSemanticFingerprint(wound) != entry.SemanticFingerprint ||
            wound.Owner.Realm != entry.Realm || wound.Owner.OwnerKind != entry.OwnerKind ||
            wound.Owner.OwnerId != entry.OwnerId || wound.Owner.CarrierPath != entry.CarrierPath)
            return null;
        return new WoundCarrierOccurrence(woundId, wound.Owner.CarrierPath,
            WoundHistoryState.HistoryPath + ".transitions.recoveryTerminalSnapshot",
            new WoundCarrierCoordinate(wound.Owner.Realm, wound.Owner.OwnerKind,
                wound.Owner.OwnerId, wound.Owner.CarrierPath), wound);
    }

    private static bool TryParseProjectionCatalogObject(
        byte[] bytes,
        string path,
        ICollection<ValidationIssue> issues,
        out JsonObject? root)
    {
        root = null;
        try
        {
            var parsed = MortalItemProjectionRootParser.Parse(
                CanonicalJsonUtf8.DecodeOneOptionalBom(bytes),
                path);
            if (!parsed.IsValid || parsed.Root is null)
            {
                issues.Add(Issue(
                    path,
                    "mortal_wound_treatment_accepted_state_root_invalid",
                    "one strict object or legacy vehicle array root without duplicate properties",
                    parsed.Issues.FirstOrDefault()?.Actual ?? "invalid topology"));
                return false;
            }

            root = MortalItemProjectionRootParser.ToCarrierCatalogObject(
                parsed.Root,
                path);
            if (root is not null)
                return true;

            throw new InvalidDataException(
                "A required treatment projection root cannot be absent.");
        }
        catch (Exception exception) when (exception is JsonException or
                                           InvalidDataException or
                                           InvalidOperationException or
                                           ArgumentException)
        {
            issues.Add(Issue(
                path,
                "mortal_wound_treatment_accepted_state_root_invalid",
                "one strict object or legacy vehicle array root without duplicate properties",
                exception.GetType().Name));
            return false;
        }
    }

    private static bool HasDuplicateProperty(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
            {
                if (!names.Add(property.Name) || HasDuplicateProperty(property.Value))
                    return true;
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in value.EnumerateArray())
            {
                if (HasDuplicateProperty(item))
                    return true;
            }
        }
        return false;
    }

    private static bool TryString(JsonObject root, string field, out string value)
    {
        value = string.Empty;
        if (root[field] is not JsonValue node ||
            !node.TryGetValue<string>(out var parsed) ||
            !ResourceMaterializationContract.IsExactIdentifier(parsed))
        {
            return false;
        }
        value = parsed;
        return true;
    }

    private static void AddIssues(
        ICollection<ValidationIssue> destination,
        IEnumerable<ValidationIssue> source)
    {
        foreach (var issue in source)
            destination.Add(issue);
    }

    private static JsonObject? Get(
        IReadOnlyDictionary<string, JsonObject> roots,
        string path) => roots.TryGetValue(path, out var root) ? root : null;

    private static string Decode(byte[] bytes) =>
        CanonicalJsonUtf8.DecodeOneOptionalBom(bytes);

    private static string Hash(string domain, params string?[] values)
    {
        var fields = new string?[values.Length + 2];
        fields[0] = domain;
        fields[1] = "1";
        values.CopyTo(fields, 2);
        return WoundAcceptedTurnFingerprintWriter.Compute(fields);
    }

    private static string Number(int value) =>
        value.ToString(CultureInfo.InvariantCulture);

    private static string Describe(JsonNode? node) => node?.ToJsonString() ?? "missing";

    private static MortalWoundTreatmentCapabilitySkillSource Clone(
        MortalWoundTreatmentCapabilitySkillSource source) =>
        MortalWoundTreatmentCapabilityDetachment.CloneSource(source);

    private static WoundAcceptedTurnBinding Clone(WoundAcceptedTurnBinding binding) => new(
        binding.SessionId,
        binding.RequestId,
        binding.SnapshotToken,
        binding.Realm,
        binding.Turn,
        binding.AcceptedEvents,
        binding.AcceptedEventsFingerprint);

    private static MortalWoundTreatmentAcceptedStateAuthorityResult Failure(
        IEnumerable<ValidationIssue> issues) => new(
        false,
        new ReadOnlyCollection<ValidationIssue>(issues.ToArray()),
        null);

    internal static MortalWoundTreatmentAcceptedStateAuthorityResult
        RegistryCandidateBindingFailure(string actual) => Failure(new[]
        {
            Issue(
                LiveTurnPreparationService.PendingTurnSnapshotManifestPath,
                "mortal_wound_treatment_accepted_state_registry_candidate_stale",
                "candidate bound to the exact active manager, lease, root revision, and session generation",
                actual)
        });

    private static ValidationIssue Issue(
        string path,
        string code,
        string expected,
        string actual) => new(
        path,
        IssueSeverity.Error,
        "The current Mortal wound-treatment accepted state cannot be trusted.",
        code: code,
        actor: "Client",
        section: "wound_materialization",
        expected: expected,
        actual: actual,
        repairHint:
        "Rebuild the current pending-turn snapshot from complete canonical sources; never substitute detached authority.");
}
