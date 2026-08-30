using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

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
    private const string WorldTimePath = "game_state/world/world_time.json";
    private const string CurrentLocationPath = "game_state/world/current_location.json";
    private const string PlayerActiveSkillsPath = "game_state/player/skills_active.json";
    private const string PlayerPassiveSkillsPath = "game_state/player/skills_passive.json";
    private const string NpcCorePath = "game_state/npcs/npc_core.json";
    private const string PlayerInventoryPath = "game_state/inventory/items.json";
    private const string ItemIdentityPath = "game_state/inventory/item_identity_index.json";
    private const string RegularQuestsPath = "game_state/quests/regular_quests.json";
    private const string SoulQuestsPath = "game_state/quests/soul_quests.json";

    private static readonly string[] OptionalAuthorityPaths =
    {
        PlayerInventoryPath,
        ItemIdentityPath,
        RegularQuestsPath,
        SoulQuestsPath,
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
    };

    private readonly FileSystemManager _fileSystem;
    private readonly FileSystemManager.CanonicalWriteLease _writeLease;
    private readonly CanonicalRootIdentity _rootIdentity;
    private readonly long _rootRevision;
    private readonly WoundAcceptedTurnBinding _binding;
    private readonly MortalWoundTreatmentAuthority.Context _requirementContext;
    private readonly MortalWoundTreatmentAuthority.Snapshot _requirementSnapshot;
    private readonly WoundMaterializationEnvelope _currentWound;
    private readonly WoundHistoryState _history;
    private readonly EffectMechanicsSnapshot _effectMechanics;
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
        WoundHistoryState history,
        long currentGameMinute,
        EffectMechanicsSnapshot effectMechanics,
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
        _history = history;
        CurrentGameMinute = currentGameMinute;
        _effectMechanics = effectMechanics;
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
        _requirementContext with { };
    internal MortalWoundTreatmentAuthority.Snapshot RequirementSnapshot =>
        _requirementSnapshot with { };
    internal WoundMaterializationEnvelope CurrentWound => _currentWound;
    internal WoundHistoryState History => _history;
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

    private static MortalWoundTreatmentAcceptedStateAuthorityResult ExportCurrentCore(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease,
        MortalWoundTreatmentAuthority.Context context,
        string woundId)
    {
        fileSystem.EnsureCanonicalWriteLeaseActive(writeLease);
        var issues = new List<ValidationIssue>();
        if (!string.Equals(context.Realm, "mortal_world", StringComparison.Ordinal))
        {
            issues.Add(Issue(
                context.SourcePath,
                "mortal_wound_treatment_accepted_state_realm_invalid",
                "mortal_world",
                context.Realm));
            return Failure(issues);
        }

        var coveredPaths = ReadManifestCoveredPaths(fileSystem);
        var requiredPaths = BuildRequiredPaths(context, coveredPaths, issues);
        if (issues.Count != 0)
            return Failure(issues);

        var snapshotRead = PendingTurnSnapshotReader.ReadCurrent(
            fileSystem,
            writeLease,
            requiredPaths);
        if (!snapshotRead.Success || snapshotRead.Snapshot is null)
            return Failure(snapshotRead.Issues);
        var signed = snapshotRead.Snapshot;

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
            if (!TryParseObject(signed.ReadRequiredBytes(path), path, issues, out var root))
                continue;
            roots.Add(path, root!);
        }
        if (issues.Count != 0)
            return Failure(issues);

        if (!TryComposeBinding(fileSystem, signed, issues, out var binding, out var requestFingerprint) ||
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
        if (!woundCatalog.TryResolveOne(woundId, out var occurrence))
        {
            issues.Add(Issue(
                "treatmentSelection.woundId",
                "mortal_wound_treatment_accepted_state_wound_unresolved",
                "one exact current wound across the accepted carrier set",
                $"{woundId}: occurrences={woundCatalog.CountExactOccurrences(woundId)}"));
        }

        var identity = WoundIdentityState.Parse(
            Decode(signed.ReadRequiredBytes(WoundIdentityState.StatePath)),
            WoundIdentityState.StatePath);
        var history = WoundHistoryState.Parse(
            Decode(signed.ReadRequiredBytes(WoundHistoryState.HistoryPath)),
            WoundHistoryState.HistoryPath);
        issues.AddRange(identity.Issues);
        issues.AddRange(history.Issues);
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
            else
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

        var actors = ComposeActors(
            context,
            roots,
            woundCatalog,
            playerCapabilities,
            npcCapabilities,
            issues);
        var locations = ComposeLocations(roots[CurrentLocationPath], issues);
        ValidateSelectedCoordinates(context, actors, locations, issues);

        var items = ComposeItems(roots, issues);
        var resources = ComposeResources(roots[CurrentLocationPath], issues);
        var facilities = ComposeFacilities(roots[CurrentLocationPath], issues);
        var quests = ComposeQuests(roots, issues);
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
            ["environments"] = new JsonArray()
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
                new JsonArray(items.Select(ToJson).Cast<JsonNode?>().ToArray())),
            WoundAcceptedTurnFingerprintWriter.CanonicalJson(
                new JsonArray(resources.Select(ToJson).Cast<JsonNode?>().ToArray())));
        var actorLocationFingerprint = Hash(
            "mortal_wound_treatment_actor_location",
            WoundAcceptedTurnFingerprintWriter.CanonicalJson(
                new JsonArray(actors.Select(ToJson).Cast<JsonNode?>().ToArray())),
            WoundAcceptedTurnFingerprintWriter.CanonicalJson(
                new JsonArray(locations.Select(ToJson).Cast<JsonNode?>().ToArray())),
            WoundAcceptedTurnFingerprintWriter.CanonicalJson(
                new JsonArray(facilities.Select(ToJson).Cast<JsonNode?>().ToArray())));
        var playerCatalogFingerprint = ComputeCapabilityCatalogFingerprint(playerCapabilities);
        var npcCatalogFingerprint = ComputeCapabilityCatalogFingerprint(npcCapabilities);
        var skillSourceFingerprint = Hash(
            "mortal_wound_treatment_skill_sources",
            playerCatalogFingerprint,
            npcCatalogFingerprint);
        var requirementSnapshotFingerprint = Hash(
            "mortal_wound_treatment_requirement_snapshot",
            WoundAcceptedTurnFingerprintWriter.CanonicalJson(requirementProjection));
        var requiredRootFingerprint = Hash(
            "mortal_wound_treatment_required_roots",
            requiredPaths.OrderBy(static path => path, StringComparer.Ordinal)
                .SelectMany(path => new[]
                {
                    path,
                    Hash("canonical_root", WoundAcceptedTurnFingerprintWriter.CanonicalJson(roots[path]))
                })
                .ToArray());
        var acceptedStateFingerprint = Hash(
            "mortal_wound_treatment_accepted_state",
            "1",
            generation,
            bindingFingerprint,
            contextFingerprint,
            requestFingerprint,
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
            requiredRootFingerprint);

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
            history.State,
            currentGameMinute,
            effectMechanics,
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
                   _rootRevision == _rootIdentity.SessionGenerationRevision;
        }
        catch (Exception exception) when (exception is InvalidOperationException or
                                           ObjectDisposedException or
                                           ArgumentException)
        {
            return false;
        }
    }

    private static IReadOnlySet<string> ReadManifestCoveredPaths(FileSystemManager fileSystem)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        try
        {
            var json = fileSystem.ReadFileSync(
                LiveTurnPreparationService.PendingTurnSnapshotManifestPath);
            using var document = JsonDocument.Parse(json ?? string.Empty);
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty("files", out var files) ||
                files.ValueKind != JsonValueKind.Object)
            {
                return result;
            }
            foreach (var property in files.EnumerateObject())
                result.Add(property.Name);
        }
        catch (JsonException)
        {
            // PendingTurnSnapshotReader owns the authoritative diagnostic.
        }
        return result;
    }

    private static IReadOnlyList<string> BuildRequiredPaths(
        MortalWoundTreatmentAuthority.Context context,
        IReadOnlySet<string> covered,
        ICollection<ValidationIssue> issues)
    {
        var required = new HashSet<string>(StringComparer.Ordinal)
        {
            WorldTimePath,
            CurrentLocationPath,
            PlayerActiveSkillsPath,
            PlayerPassiveSkillsPath,
            NpcCorePath,
            WoundIdentityState.StatePath,
            WoundHistoryState.HistoryPath
        };
        foreach (var path in OptionalAuthorityPaths)
        {
            if (covered.Contains(path))
                required.Add(path);
        }

        var targetPaths = context.TargetKind switch
        {
            "player" => new[] { WoundCarrierCatalog.PlayerPath },
            "npc" => new[] { WoundCarrierCatalog.NpcPath },
            "combatant" or "combatant_member" => new[]
            {
                WoundCarrierCatalog.EnemiesPath,
                WoundCarrierCatalog.AlliesPath
            }.Where(covered.Contains).ToArray(),
            _ => Array.Empty<string>()
        };
        if (targetPaths.Length == 0)
        {
            issues.Add(Issue(
                "treatmentSelection.target",
                "mortal_wound_treatment_accepted_state_target_carrier_missing",
                "one signed canonical carrier path for the selected target kind",
                context.TargetKind));
        }
        foreach (var path in targetPaths)
            required.Add(path);
        return required.OrderBy(static path => path, StringComparer.Ordinal).ToArray();
    }

    private static bool TryComposeBinding(
        FileSystemManager fileSystem,
        PendingTurnSnapshotReadAuthority signed,
        ICollection<ValidationIssue> issues,
        out WoundAcceptedTurnBinding? binding,
        out string requestFingerprint)
    {
        binding = null;
        requestFingerprint = string.Empty;
        var requestJson = fileSystem.ReadFileSync(LiveTurnPreparationService.TurnRequestPath);
        if (string.IsNullOrWhiteSpace(requestJson))
        {
            issues.Add(Issue(
                LiveTurnPreparationService.TurnRequestPath,
                "mortal_wound_treatment_accepted_event_missing",
                "one complete current accepted-turn request/event authority",
                "missing"));
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(requestJson);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                HasDuplicateProperty(root) ||
                !root.TryGetProperty("gameMode", out var mode) ||
                mode.ValueKind != JsonValueKind.String ||
                !string.Equals(mode.GetString(), "normal", StringComparison.Ordinal) ||
                !root.TryGetProperty("preGeneratedDices1d20", out var dice) ||
                dice.ValueKind != JsonValueKind.Array || dice.GetArrayLength() == 0 ||
                dice.EnumerateArray().Any(static die =>
                    die.ValueKind != JsonValueKind.Number ||
                    !die.TryGetInt32(out var value) || value is < 1 or > 20))
            {
                issues.Add(Issue(
                    LiveTurnPreparationService.TurnRequestPath,
                    "mortal_wound_treatment_accepted_event_missing",
                    "one strict normal-mode current request with accepted d20 event authority",
                    "missing or malformed event source"));
                return false;
            }
            requestFingerprint = Hash(
                "mortal_wound_treatment_request_event_source",
                signed.SessionId,
                signed.RequestId,
                Number(signed.TurnNumber),
                string.Join(",", dice.EnumerateArray().Select(static die =>
                    die.GetInt32().ToString(CultureInfo.InvariantCulture))));
        }
        catch (JsonException exception)
        {
            issues.Add(Issue(
                LiveTurnPreparationService.TurnRequestPath,
                "mortal_wound_treatment_accepted_event_missing",
                "one strict current request/event authority",
                exception.GetType().Name));
            return false;
        }

        var eventRoot = EffectAcceptedTurnInputComposer.BuildAcceptedEventInput(
            signed.TurnNumber,
            EffectAcceptedTurnInputComposer.CreateEmptyCommandRoot());
        if (eventRoot["events"] is not JsonArray eventRows || eventRows.Count == 0)
        {
            issues.Add(Issue(
                "acceptedWoundEvents.events",
                "mortal_wound_treatment_accepted_event_missing",
                "one current accepted event",
                "missing"));
            return false;
        }
        var coordinates = new List<WoundAcceptedResponseEventCoordinate>(eventRows.Count);
        foreach (var row in eventRows.OfType<JsonObject>())
        {
            if (!TryString(row, "eventRef", out var eventRef) ||
                !TryString(row, "kind", out var kind) ||
                !TryString(row, "authorityId", out var authorityId))
            {
                continue;
            }
            coordinates.Add(new WoundAcceptedResponseEventCoordinate(
                eventRef,
                kind,
                authorityId));
        }
        var composed = WoundAcceptedEventAuthorityComposer.Compose(
            new WoundAcceptedResponseEventProjection(
                signed.SessionId,
                signed.RequestId,
                signed.SnapshotToken,
                signed.TurnNumber,
                coordinates),
            Array.Empty<WoundSelectedEventEvidence>());
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
        foreach (var npc in EnumerateNpcs(npcRoot))
        {
            if (!TryNpcId(npc, out var npcId))
                continue;
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

    private static IReadOnlyList<MortalWoundTreatmentAuthority.Actor> ComposeActors(
        MortalWoundTreatmentAuthority.Context context,
        IReadOnlyDictionary<string, JsonObject> roots,
        WoundCarrierCatalog woundCatalog,
        IReadOnlyList<MortalWoundTreatmentCapabilitySkillSource> playerCapabilities,
        IReadOnlyList<MortalWoundTreatmentCapabilitySkillSource> npcCapabilities,
        ICollection<ValidationIssue> issues)
    {
        var result = new List<MortalWoundTreatmentAuthority.Actor>();
        var playerSkills = ComposeSkills(
            roots[PlayerActiveSkillsPath],
            "activeSkillChanges",
            roots[PlayerPassiveSkillsPath],
            "passiveSkillChanges");
        result.Add(new MortalWoundTreatmentAuthority.Actor(
            "player",
            "player_current",
            "Player",
            "mortal_world",
            context.CurrentLocationId,
            "active",
            true,
            true,
            playerSkills,
            ComposeCapabilities(playerCapabilities),
            Array.Empty<MortalWoundTreatmentAuthority.Consent>()));

        foreach (var npc in EnumerateNpcs(roots[NpcCorePath]))
        {
            if (!TryNpcId(npc, out var npcId))
            {
                issues.Add(Issue(
                    NpcCorePath,
                    "mortal_wound_treatment_accepted_state_actor_invalid",
                    "one exact NPC identity on every selected source row",
                    Describe(npc)));
                continue;
            }
            result.Add(new MortalWoundTreatmentAuthority.Actor(
                "npc",
                npcId,
                ReadText(npc, "displayName") ?? npcId,
                ReadText(npc, "realm") ?? "mortal_world",
                ReadText(npc, "currentLocationId") ?? string.Empty,
                ReadText(npc, "lifecycle") ?? "active",
                ReadBoolean(npc, "active", true),
                ReadBoolean(npc, "reachable", true),
                ComposeSkills(npc, "activeSkills", npc, "passiveSkills"),
                ComposeCapabilities(npcCapabilities.Where(source =>
                    string.Equals(source.OwnerId, npcId, StringComparison.Ordinal)).ToArray()),
                ComposeConsents(npc, issues)));
        }

        var combatCoordinates = woundCatalog.Occurrences
            .Select(static occurrence => occurrence.Coordinate)
            .Where(static coordinate => coordinate.OwnerKind is "combatant" or "combatant_member")
            .Distinct()
            .ToArray();
        foreach (var coordinate in combatCoordinates)
        {
            result.Add(new MortalWoundTreatmentAuthority.Actor(
                coordinate.OwnerKind,
                coordinate.OwnerId,
                coordinate.OwnerId,
                coordinate.Realm,
                context.CurrentLocationId,
                "active",
                true,
                true,
                Array.Empty<MortalWoundTreatmentAuthority.Skill>(),
                Array.Empty<MortalWoundTreatmentAuthority.Capability>(),
                Array.Empty<MortalWoundTreatmentAuthority.Consent>()));
        }
        return result;
    }

    private static IReadOnlyList<MortalWoundTreatmentAuthority.Skill> ComposeSkills(
        JsonObject activeRoot,
        string activeArray,
        JsonObject passiveRoot,
        string passiveArray)
    {
        var result = new List<MortalWoundTreatmentAuthority.Skill>();
        foreach (var skill in EnumerateObjects(activeRoot[activeArray])
                     .Concat(EnumerateObjects(passiveRoot[passiveArray])))
        {
            var lifecycle = ReadText(skill, "lifecycle") ?? "active";
            var active = ReadBoolean(skill, "active", true);
            var tier = ReadInt32(skill, "tier", ReadInt32(skill, "masteryLevel", 0));
            var display = ReadText(skill, "displayName") ??
                ReadText(skill, "skillName") ?? "Skill";
            if (TryString(skill, "skillId", out var skillId))
                result.Add(new MortalWoundTreatmentAuthority.Skill(
                    skillId,
                    display,
                    tier,
                    lifecycle,
                    active));
            foreach (var capability in EnumerateObjects(
                         skill["mortalWoundTreatmentCapabilities"]))
            {
                if (TryString(capability, "capabilityRef", out var capabilityRef))
                {
                    result.Add(new MortalWoundTreatmentAuthority.Skill(
                        capabilityRef,
                        display,
                        tier,
                        lifecycle,
                        active));
                }
            }
        }
        return result;
    }

    private static IReadOnlyList<MortalWoundTreatmentAuthority.Capability>
        ComposeCapabilities(
            IReadOnlyList<MortalWoundTreatmentCapabilitySkillSource> sources) =>
        sources.SelectMany(source => source.Capabilities.Select(capability =>
                new MortalWoundTreatmentAuthority.Capability(
                    capability.CapabilityRef,
                    source.DisplayName,
                    source.Lifecycle,
                    source.Active)))
            .ToArray();

    private static IReadOnlyList<MortalWoundTreatmentAuthority.Consent> ComposeConsents(
        JsonObject actor,
        ICollection<ValidationIssue> issues)
    {
        var result = new List<MortalWoundTreatmentAuthority.Consent>();
        var index = 0;
        foreach (var consent in EnumerateObjects(actor["consents"]))
        {
            var path = NpcCorePath + $".consents[{index++}]";
            if (!TryString(consent, "consentRef", out var consentRef) ||
                !TryString(consent, "providerKind", out var providerKind) ||
                !TryString(consent, "providerId", out var providerId) ||
                !TryString(consent, "targetKind", out var targetKind) ||
                !TryString(consent, "targetId", out var targetId) ||
                !TryString(consent, "status", out var status) ||
                status is not ("granted" or "withdrawn"))
            {
                issues.Add(Issue(
                    path,
                    "mortal_wound_treatment_accepted_state_consent_invalid",
                    "one structurally complete current consent row",
                    Describe(consent)));
                continue;
            }
            result.Add(new MortalWoundTreatmentAuthority.Consent(
                consentRef,
                ReadText(consent, "displayName") ?? consentRef,
                providerKind,
                providerId,
                targetKind,
                targetId,
                status,
                ReadText(consent, "lifecycle") ?? "active",
                ReadBoolean(consent, "active", true)));
        }
        return result;
    }

    private static IReadOnlyList<MortalWoundTreatmentAuthority.Location> ComposeLocations(
        JsonObject root,
        ICollection<ValidationIssue> issues)
    {
        if (!TryString(root, "locationId", out var locationId))
        {
            issues.Add(Issue(
                CurrentLocationPath + ".locationId",
                "mortal_wound_treatment_accepted_state_location_invalid",
                "one exact current location identity",
                Describe(root["locationId"])));
            return Array.Empty<MortalWoundTreatmentAuthority.Location>();
        }
        var present = new List<MortalWoundTreatmentAuthority.ActorCoordinate>();
        foreach (var actor in EnumerateObjects(root["presentActors"]))
        {
            if (TryString(actor, "actorKind", out var actorKind) &&
                TryString(actor, "actorId", out var actorId))
            {
                present.Add(new MortalWoundTreatmentAuthority.ActorCoordinate(
                    actorKind,
                    actorId));
            }
            else
            {
                issues.Add(Issue(
                    CurrentLocationPath + ".presentActors",
                    "mortal_wound_treatment_accepted_state_presence_invalid",
                    "closed exact actor coordinates",
                    Describe(actor)));
            }
        }
        return new[]
        {
            new MortalWoundTreatmentAuthority.Location(
                locationId,
                ReadText(root, "name") ?? ReadText(root, "displayName") ?? locationId,
                ReadText(root, "realm") ?? "mortal_world",
                ReadText(root, "lifecycle") ?? "active",
                ReadBoolean(root, "active", true),
                present)
        };
    }

    private static void ValidateSelectedCoordinates(
        MortalWoundTreatmentAuthority.Context context,
        IReadOnlyList<MortalWoundTreatmentAuthority.Actor> actors,
        IReadOnlyList<MortalWoundTreatmentAuthority.Location> locations,
        ICollection<ValidationIssue> issues)
    {
        foreach (var coordinate in new[]
                 {
                     (Role: "target", Kind: context.TargetKind, Id: context.TargetId),
                     (Role: "provider", Kind: context.ProviderKind, Id: context.ProviderId)
                 })
        {
            var matches = actors.Where(actor =>
                string.Equals(actor.ActorKind, coordinate.Kind, StringComparison.Ordinal) &&
                string.Equals(actor.ActorId, coordinate.Id, StringComparison.Ordinal)).ToArray();
            if (matches.Length != 1 ||
                matches.Any(actor => !string.Equals(
                    actor.Realm,
                    context.Realm,
                    StringComparison.Ordinal)))
            {
                issues.Add(Issue(
                    context.SourcePath + "." + coordinate.Role + "Id",
                    "mortal_wound_treatment_accepted_state_actor_ambiguous",
                    "one exact current same-realm actor coordinate",
                    $"({coordinate.Kind}, {coordinate.Id}): matches={matches.Length}"));
            }
        }

        var locationMatches = locations.Where(location => string.Equals(
            location.LocationId,
            context.CurrentLocationId,
            StringComparison.Ordinal)).ToArray();
        if (locationMatches.Length != 1 ||
            !string.Equals(locationMatches.SingleOrDefault()?.Realm, context.Realm, StringComparison.Ordinal))
        {
            issues.Add(Issue(
                context.SourcePath + ".currentLocationId",
                "mortal_wound_treatment_accepted_state_location_ambiguous",
                "one exact current same-realm location coordinate",
                $"{context.CurrentLocationId}: matches={locationMatches.Length}"));
            return;
        }
        foreach (var coordinate in new[]
                 {
                     (context.TargetKind, context.TargetId),
                     (context.ProviderKind, context.ProviderId)
                 }.Distinct())
        {
            var present = locationMatches[0].PresentActors.Count(actor =>
                string.Equals(actor.ActorKind, coordinate.Item1, StringComparison.Ordinal) &&
                string.Equals(actor.ActorId, coordinate.Item2, StringComparison.Ordinal));
            if (present != 1)
            {
                issues.Add(Issue(
                    CurrentLocationPath + ".presentActors",
                    "mortal_wound_treatment_accepted_state_presence_ambiguous",
                    "one exact co-presence coordinate for each selected actor",
                    $"({coordinate.Item1}, {coordinate.Item2}): matches={present}"));
            }
        }
    }

    private static IReadOnlyList<MortalWoundTreatmentAuthority.Item> ComposeItems(
        IReadOnlyDictionary<string, JsonObject> roots,
        ICollection<ValidationIssue> issues)
    {
        var input = new MortalItemCarrierCatalogInput(
            Get(roots, PlayerInventoryPath),
            Get(roots, NpcCorePath),
            null,
            Get(roots, CurrentLocationPath),
            null,
            new Dictionary<string, JsonObject>(StringComparer.Ordinal));
        var catalog = MortalItemCarrierCatalog.Build(input);
        foreach (var issue in catalog.Issues)
        {
            issues.Add(Issue(
                issue.Path,
                issue.Code,
                "one exact canonical Mortal item carrier row",
                issue.Message));
        }
        var result = new List<MortalWoundTreatmentAuthority.Item>();
        foreach (var occurrence in catalog.Occurrences)
        {
            if (occurrence.ItemId is not { } itemId)
                continue;
            var count = ReadInt32(occurrence.Item, "count", 0);
            if (count <= 0)
            {
                issues.Add(Issue(
                    occurrence.JsonPath + ".count",
                    "mortal_wound_treatment_accepted_state_item_invalid",
                    "positive canonical item count",
                    count.ToString(CultureInfo.InvariantCulture)));
                continue;
            }
            var ownerKind = occurrence.Carrier.Kind switch
            {
                "player_inventory" => "player",
                "npc_inventory" => "npc",
                _ => "player"
            };
            var ownerId = ownerKind == "player"
                ? "player_current"
                : occurrence.Carrier.OwnerId;
            result.Add(new MortalWoundTreatmentAuthority.Item(
                itemId,
                ReadText(occurrence.Item, "displayName") ??
                    ReadText(occurrence.Item, "name") ?? itemId,
                "mortal_world",
                ownerKind,
                ownerId,
                count,
                ReadInt32(occurrence.Item, "availableCount", count),
                ReadText(occurrence.Item, "reservationState") ?? "available",
                ReadText(occurrence.Item, "lifecycle") ?? "active",
                ReadBoolean(occurrence.Item, "active", true)));
        }
        return result;
    }

    private static IReadOnlyList<MortalWoundTreatmentAuthority.Resource> ComposeResources(
        JsonObject location,
        ICollection<ValidationIssue> issues)
    {
        var result = new List<MortalWoundTreatmentAuthority.Resource>();
        foreach (var resource in EnumerateObjects(location["resources"]))
        {
            if (!TryString(resource, "resourceRef", out var resourceRef) ||
                !TryString(resource, "ownerKind", out var ownerKind) ||
                !TryString(resource, "ownerId", out var ownerId))
            {
                issues.Add(Issue(
                    CurrentLocationPath + ".resources",
                    "mortal_wound_treatment_accepted_state_resource_invalid",
                    "one complete canonical resource row",
                    Describe(resource)));
                continue;
            }
            result.Add(new MortalWoundTreatmentAuthority.Resource(
                resourceRef,
                ReadText(resource, "displayName") ?? resourceRef,
                ReadText(resource, "realm") ?? "mortal_world",
                ownerKind,
                ownerId,
                ReadInt32(resource, "currentValue", 0),
                ReadInt32(resource, "availableValue", 0),
                ReadText(resource, "reservationState") ?? "available",
                ReadText(resource, "lifecycle") ?? "active",
                ReadBoolean(resource, "active", true)));
        }
        return result;
    }

    private static IReadOnlyList<MortalWoundTreatmentAuthority.Facility> ComposeFacilities(
        JsonObject location,
        ICollection<ValidationIssue> issues)
    {
        var result = new List<MortalWoundTreatmentAuthority.Facility>();
        foreach (var facility in EnumerateObjects(location["facilities"]))
        {
            if (!TryString(facility, "facilityId", out var facilityId) ||
                !TryString(facility, "locationId", out var locationId))
            {
                issues.Add(Issue(
                    CurrentLocationPath + ".facilities",
                    "mortal_wound_treatment_accepted_state_facility_invalid",
                    "one complete canonical facility row",
                    Describe(facility)));
                continue;
            }
            result.Add(new MortalWoundTreatmentAuthority.Facility(
                facilityId,
                ReadText(facility, "displayName") ?? facilityId,
                ReadText(facility, "realm") ?? "mortal_world",
                locationId,
                ReadText(facility, "lifecycle") ?? "active",
                ReadBoolean(facility, "active", true),
                ReadBoolean(facility, "available", true)));
        }
        return result;
    }

    private static IReadOnlyList<MortalWoundTreatmentAuthority.Quest> ComposeQuests(
        IReadOnlyDictionary<string, JsonObject> roots,
        ICollection<ValidationIssue> issues)
    {
        var result = new List<MortalWoundTreatmentAuthority.Quest>();
        foreach (var path in new[] { RegularQuestsPath, SoulQuestsPath })
        {
            if (!roots.TryGetValue(path, out var root))
                continue;
            foreach (var quest in EnumerateObjects(root["quests"] ?? root["activeQuests"]))
            {
                if (!TryString(quest, "questId", out var questId) ||
                    !TryString(quest, "state", out var state))
                {
                    issues.Add(Issue(
                        path,
                        "mortal_wound_treatment_accepted_state_quest_invalid",
                        "one complete canonical quest row",
                        Describe(quest)));
                    continue;
                }
                result.Add(new MortalWoundTreatmentAuthority.Quest(
                    questId,
                    ReadText(quest, "displayName") ?? ReadText(quest, "name") ?? questId,
                    ReadText(quest, "realm") ?? "mortal_world",
                    state,
                    ReadText(quest, "lifecycle") ?? "active",
                    ReadBoolean(quest, "active", true)));
            }
        }
        return result;
    }

    private static EffectMechanicsSnapshot ComposeEffectMechanics(
        IReadOnlyDictionary<string, JsonObject> roots,
        ICollection<ValidationIssue> issues)
    {
        var snapshot = EffectMechanicsSnapshot.Build(new EffectMechanicsInput(
            new EffectCarrierCatalogInput(
                Get(roots, EffectCarrierCatalog.PlayerPath),
                Get(roots, EffectCarrierCatalog.NpcPath),
                Get(roots, EffectCarrierCatalog.EnemiesPath),
                Get(roots, EffectCarrierCatalog.AlliesPath),
                Get(roots, EffectCarrierCatalog.AfterlifeProfilesPath),
                Get(roots, EffectCarrierCatalog.SpiritualConflictPath)),
            Get(roots, EffectIdentityState.StatePath)));
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
            fields.Add(effect.CanonicalEffect.GetRawText());
        }
        foreach (var component in snapshot.Components)
        {
            fields.Add(component.EffectId);
            fields.Add(component.ComponentId);
            fields.Add(component.Profile);
            fields.Add(Number(component.Priority));
            fields.Add(Number(component.CurrentStacks));
            fields.Add(component.Payload.GetRawText());
        }
        return WoundAcceptedTurnFingerprintWriter.Compute(fields);
    }

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
            using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions
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

    private static IEnumerable<JsonObject> EnumerateNpcs(JsonObject root)
    {
        foreach (var section in new[] { "NPCsInScene", "NPCs", "UpdateNPCs" })
        {
            foreach (var npc in EnumerateObjects(root[section]))
                yield return npc;
        }
    }

    private static IEnumerable<JsonObject> EnumerateObjects(JsonNode? node) =>
        node is JsonArray array
            ? array.OfType<JsonObject>()
            : Array.Empty<JsonObject>();

    private static bool TryNpcId(JsonObject npc, out string npcId)
    {
        foreach (var field in new[] { "NPCId", "npcId", "id", "initialId" })
        {
            if (TryString(npc, field, out npcId))
                return true;
        }
        npcId = string.Empty;
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

    private static string? ReadText(JsonObject root, string field) =>
        root[field] is JsonValue node &&
        node.TryGetValue<string>(out var value) &&
        !string.IsNullOrWhiteSpace(value) &&
        string.Equals(value, value.Trim(), StringComparison.Ordinal)
            ? value
            : null;

    private static bool ReadBoolean(JsonObject root, string field, bool fallback) =>
        root[field] is JsonValue node && node.TryGetValue<bool>(out var value)
            ? value
            : fallback;

    private static int ReadInt32(JsonObject root, string field, int fallback) =>
        root[field] is JsonValue node && node.TryGetValue<int>(out var value)
            ? value
            : fallback;

    private static JsonObject? Get(
        IReadOnlyDictionary<string, JsonObject> roots,
        string path) => roots.TryGetValue(path, out var root) ? root : null;

    private static string Decode(byte[] bytes) => Encoding.UTF8.GetString(bytes);

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
            OperationLimits = capability.OperationLimits with
            {
                RemovableComplicationKinds = capability.OperationLimits
                    .RemovableComplicationKinds.ToArray()
            }
        }).ToArray());

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
