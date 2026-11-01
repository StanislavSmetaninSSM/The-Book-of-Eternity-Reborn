using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal sealed record SpiritualDiagnosisAdmission(
    SpiritualSelfTreatmentSelection Selection, WoundMaterializationEnvelope Wound,
    int HealingTier, string PathFingerprint, string RequirementFingerprint, string SourceFingerprint)
{
    private JsonArray _bindings = new();
    internal JsonArray RequirementBindings { get => _bindings.DeepClone().AsArray(); init => _bindings = value.DeepClone().AsArray(); }
    internal string RequirementBindingFingerprint => SpiritualDiagnosisRequirementBindings.Fingerprint(_bindings);
}

internal sealed record SpiritualDiagnosisAdmissionResult(SpiritualDiagnosisAdmission? Data, string? IssueCode);

/// <summary>One detached menu operation's parsed DATA. Never a live source/publication capability.</summary>
internal sealed class SpiritualDiagnosisSourceData
{
    private sealed record PathData(WoundDiagnosisPath Path, string Fingerprint,
        string RequirementFingerprint, WoundRequirementProjection Requirements, IReadOnlyList<string> StatementFingerprints);
    private sealed record WoundData(WoundMaterializationEnvelope Wound, string Fingerprint,
        IReadOnlyList<PathData> Paths, IReadOnlySet<string> KnownFacts);
    private readonly JsonObject? _profiles;
    private readonly JsonObject _soul;
    private readonly string _realm;
    private readonly bool _selfVisible;
    private readonly bool _conflict;
    private readonly IReadOnlyList<WoundData> _wounds;
    private readonly string _sourceFingerprint;
    private readonly string _profileFingerprint;
    private readonly string _soulFingerprint;
    private readonly SpiritualDiagnosisCanonicalSources _canonical;
    internal (int Compositions, int SourceReads) CanonicalResourceWork => (_canonical.ResourceOwnerCompositions, _canonical.ResourceOwnerSourceReads);

    internal SpiritualDiagnosisSourceData(WoundPlayerReadContext context) : this((context ?? throw new ArgumentNullException(nameof(context))).Catalog,
        context.Input.Carriers.AfterlifeProfiles, context.Soul, context.Input.Realm, context.Input.SelfVisible,
        context.SpiritualConflict?["activeConflict"] is not null,
        context.CurrentMenuRoots.ToDictionary(pair => pair.Key, pair => (JsonObject?)pair.Value, StringComparer.Ordinal), context.Input.Effects)
    { }

    // Reuses the actual source owner's already parsed roots; no display catalog,
    // extra read or fabricated resource/Mortal context is required by admission.
    internal SpiritualDiagnosisSourceData(WoundCarrierCatalog catalog, JsonObject? profiles, JsonObject soul,
        string realm, bool selfVisible, bool conflict) : this(catalog, profiles, soul, realm, selfVisible, conflict, null, null)
    { }

    internal SpiritualDiagnosisSourceData(WoundCarrierCatalog catalog, JsonObject? profiles, JsonObject soul,
        string realm, bool selfVisible, bool conflict,
        IReadOnlyDictionary<string, JsonObject?>? roots, EffectMechanicsSnapshot? effects = null, bool prepareCanonicalSources = true)
    {
        ArgumentNullException.ThrowIfNull(catalog); ArgumentNullException.ThrowIfNull(soul);
        _profiles = profiles?.DeepClone().AsObject(); _soul = soul.DeepClone().AsObject();
        _realm = realm; _selfVisible = selfVisible; _conflict = conflict;
        var profileJson = WoundAcceptedTurnFingerprintWriter.CanonicalJson(_profiles);
        var soulJson = WoundAcceptedTurnFingerprintWriter.CanonicalJson(_soul);
        _profileFingerprint = WoundAcceptedTurnFingerprintWriter.Compute(new[] { profileJson });
        _soulFingerprint = WoundAcceptedTurnFingerprintWriter.Compute(new[] { soulJson });
        _sourceFingerprint = WoundAcceptedTurnFingerprintWriter.Compute(new[]
        {
            "book_of_eternity.spiritual_diagnosis.menu_sources", "1", _realm,
            profileJson, soulJson
        });
        _wounds = catalog.Occurrences.Where(occurrence => occurrence.Wound.Owner.Realm == _realm &&
            occurrence.Wound.Classification.Domain == "spiritual" && occurrence.Wound.Lifecycle == "active" &&
            occurrence.Wound.Treatment.DiagnosisPaths.Count != 0).Select(occurrence =>
        {
            var wound = occurrence.Wound;
            // One complete wound projection per operation, not per requirement
            // or path. All extracted JSON is detached; typed wounds are immutable DATA.
            var canonical = JsonNode.Parse(WoundMaterializationContract.SerializeCanonical(wound))!.AsObject();
            var pathNodes = canonical["treatment"]!["diagnosisPaths"]!.AsArray();
            var paths = wound.Treatment.DiagnosisPaths.Select((path, index) => new PathData(path,
                MortalWoundTreatmentMemberFingerprint.ComputeDiagnosisPath(pathNodes[index]),
                WoundAcceptedTurnFingerprintWriter.Compute(new[] { "spiritual_diagnosis.requirements", "1",
                    WoundAcceptedTurnFingerprintWriter.CanonicalJson(pathNodes[index]!["requirements"]) }),
                MortalWoundTreatmentContract.ParseRequirementProjection(path.Requirements, path.SourcePath + ".requirements"),
                path.Requirements.Select(SpiritualDiagnosisRequirementBindings.StatementFingerprint).ToArray())).ToArray();
            return new WoundData(wound, WoundIdentityState.ComputeSemanticFingerprint(wound), paths,
                wound.Treatment.KnownRouteIds.Select(id => "route:" + id)
                    .Concat(wound.Complications.Where(c => WoundPlayerProjection.Visible(c.Visibility)).Select(c => "complication:" + c.ComplicationId))
                    .ToHashSet(StringComparer.Ordinal));
        }).ToArray();
        var actualRoots = (roots ?? new Dictionary<string, JsonObject?>()).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        actualRoots[WoundCarrierCatalog.AfterlifeProfilesPath] = _profiles;
        actualRoots[SpiritualDiagnosisCanonicalSources.SoulPath] = _soul;
        _canonical = new(realm, prepareCanonicalSources ? _wounds.Where(row => Profile(row.Wound.Owner) is { } profile && AfterlifeProfileVisibility.IsVisibleToPlayer(profile))
            .SelectMany(row => VisibleCanonicalKinds(row.Wound)) : [], actualRoots, effects);
    }

    // Final composed stable facts use the same three-kind implementation, without
    // recalculating resource/effect admission after lawful natural changes.
    internal static JsonArray ReadStableRequirementBindings(WoundCarrierCatalog catalog, JsonObject profiles, JsonObject soul,
        string realm, WoundMaterializationEnvelope wound, string pathId)
    {
        var source = new SpiritualDiagnosisSourceData(catalog, profiles, soul, realm, true, false, null, null, false);
        var data = source._wounds.Single(row => row.Wound.WoundId == wound.WoundId && row.Wound.Owner == wound.Owner);
        var path = data.Paths.Single(row => row.Path.DiagnosisPathId == pathId);
        if (!path.Requirements.IsValid) throw new InvalidDataException("Invalid stable diagnosis requirements.");
        var provider = new WoundOwnerCoordinate(realm, "player_soul", "player_soul", WoundCarrierCatalog.AfterlifeProfilesPath);
        var player = source.Profile(provider) ?? throw new InvalidDataException("Missing stable diagnosis provider.");
        var target = source.Profile(wound.Owner) ?? throw new InvalidDataException("Missing stable diagnosis target.");
        foreach (var requirement in path.Requirements.Requirements.Where(requirement => !SpiritualDiagnosisCanonicalSources.IsCanonicalKind(requirement.Kind)))
            if (!source.Satisfied(requirement, provider, player, wound.Owner, target)) throw new InvalidDataException("Stable diagnosis requirement changed.");
        return source.BuildRequirementBindings(path, provider, player, wound.Owner, target,
            new JsonObject?[path.Requirements.Requirements.Length], stableOnly: true);
    }

    internal static IEnumerable<string> VisibleCanonicalKinds(WoundMaterializationEnvelope wound) =>
        wound.Lifecycle == "active" && wound.Classification.Domain == "spiritual" && WoundPlayerProjection.Visible(wound.Display.Visibility)
        ? wound.Treatment.DiagnosisPaths.Where(path => path.Visibility != "gm_only" &&
            (path.Visibility != "hidden" || path.RequiresKnownFacts.Count != 0) && path.RequiresKnownFacts.All(fact =>
                fact.StartsWith("route:", StringComparison.Ordinal) ? wound.Treatment.KnownRouteIds.Contains(fact[6..], StringComparer.Ordinal) :
                fact.StartsWith("complication:", StringComparison.Ordinal) && wound.Complications.Any(c => c.ComplicationId == fact[13..] && WoundPlayerProjection.Visible(c.Visibility))))
            .SelectMany(path => path.Requirements).Where(row => row.ValueKind == JsonValueKind.Object && row.TryGetProperty("kind", out var kind) && kind.ValueKind == JsonValueKind.String)
            .Select(row => row.GetProperty("kind").GetString()!).Where(SpiritualDiagnosisCanonicalSources.IsCanonicalKind)
        : [];

    internal SpiritualDiagnosisAdmissionResult Admit(WoundMaterializationEnvelope requested, string pathId,
        WoundTargetChoice? currentTarget = null)
    {
        if (_conflict) return Refuse("conflict_active");
        var matching = _wounds.Where(row => row.Wound.WoundId == requested.WoundId && row.Wound.Owner == requested.Owner).ToArray();
        if (matching is not { Length: 1 }) return Refuse("target_unavailable");
        var data = matching[0]; var wound = data.Wound;
        if (!_selfVisible || _realm is not ("chaos_sea" or "shining_abode") || wound.Owner.Realm != _realm ||
            wound.Owner.CarrierPath != WoundCarrierCatalog.AfterlifeProfilesPath || wound.Classification.Domain != "spiritual" ||
            wound.Lifecycle != "active" || !WoundPlayerProjection.Visible(wound.Display.Visibility)) return Refuse("target_unavailable");
        var self = new WoundOwnerCoordinate(_realm, "player_soul", "player_soul", WoundCarrierCatalog.AfterlifeProfilesPath);
        if (wound.Owner != self && (currentTarget is null || !currentTarget.Available || currentTarget.Owner != wound.Owner))
            return Refuse("target_unavailable");
        var pathRows = data.Paths.Where(p => p.Path.DiagnosisPathId == pathId).ToArray();
        if (pathRows is not { Length: 1 }) return Refuse("path_unavailable");
        var parsed = pathRows[0]; var path = parsed.Path;
        if (path.Visibility == "gm_only" || path.Visibility == "hidden" && path.RequiresKnownFacts.Count == 0 ||
            !path.RequiresKnownFacts.All(data.KnownFacts.Contains)) return Refuse("path_unavailable");
        var player = Profile(self); var target = Profile(wound.Owner);
        if (player is null || target is null || !TryArt(self, player, "spiritual_healing", out var tier)) return Refuse("source_unavailable");
        if (path.Check.ValueKind != JsonValueKind.Object || path.Check.EnumerateObject().Any()) return Refuse("check_unsupported");
        if (!parsed.Requirements.IsValid) return Refuse("requirements_invalid");
        if (path.Reveals.Count == 0 || !path.Reveals.All(fact => ResolvesFact(wound, fact))) return Refuse("path_unavailable");
        var proofs = new JsonObject?[parsed.Requirements.Requirements.Length];
        for (var index = 0; index < proofs.Length; index++)
        {
            var requirement = parsed.Requirements.Requirements[index];
            if (SpiritualDiagnosisCanonicalSources.IsCanonicalKind(requirement.Kind))
            {
                proofs[index] = _canonical.Prove(requirement, self, wound.Owner);
                if (proofs[index] is null) return Refuse("requirement_unavailable");
            }
            else if (!Satisfied(requirement, self, player, wound.Owner, target)) return Refuse("requirement_unavailable");
        }
        var selection = new SpiritualSelfTreatmentSelection(wound.WoundId, data.Fingerprint)
        { Diagnosis = new(wound.Owner, path.DiagnosisPathId, parsed.Fingerprint) };
        var sourceFingerprint = proofs.Any(proof => proof is not null) ? WoundAcceptedTurnFingerprintWriter.Compute(new[]
            { "spiritual_diagnosis.extended_menu_sources", "1", _sourceFingerprint, _canonical.Fingerprint }) : _sourceFingerprint;
        return new(new(selection, wound, tier, parsed.Fingerprint, parsed.RequirementFingerprint,
            WoundAcceptedTurnFingerprintWriter.Compute(new[] { "spiritual_diagnosis.admission", "1", sourceFingerprint,
                data.Fingerprint, parsed.Fingerprint, parsed.RequirementFingerprint, currentTarget?.BindingFingerprint ?? "self" }))
            { RequirementBindings = BuildRequirementBindings(parsed, self, player, wound.Owner, target, proofs) }, null);
    }

    internal SpiritualSelfTreatmentSelection? DescribeSelection(WoundMaterializationEnvelope requested, string pathId)
    {
        var data = _wounds.SingleOrDefault(row => row.Wound.WoundId == requested.WoundId && row.Wound.Owner == requested.Owner);
        var parsed = data?.Paths.SingleOrDefault(path => path.Path.DiagnosisPathId == pathId);
        if (data is null || parsed is null || data.Wound.Lifecycle != "active" || !WoundPlayerProjection.Visible(data.Wound.Display.Visibility) ||
            parsed.Path.Visibility == "gm_only" || parsed.Path.Visibility == "hidden" && parsed.Path.RequiresKnownFacts.Count == 0 ||
            !parsed.Path.RequiresKnownFacts.All(data.KnownFacts.Contains)) return null;
        return new(data.Wound.WoundId, data.Fingerprint)
        { Diagnosis = new(data.Wound.Owner, parsed.Path.DiagnosisPathId, parsed.Fingerprint) };
    }

    private bool Satisfied(MortalWoundTreatmentRequirement requirement, WoundOwnerCoordinate playerOwner,
        JsonObject player, WoundOwnerCoordinate targetOwner, JsonObject target)
    {
        (WoundOwnerCoordinate Owner, JsonObject Profile) Actor(string role) => role == "provider" ? (playerOwner, player) : (targetOwner, target);
        switch (requirement)
        {
            case MortalWoundSourceCapabilityRequirement capability:
                var capabilityActor = Actor(capability.ActorRole);
                return TryArt(capabilityActor.Owner, capabilityActor.Profile, capability.CapabilityRef, out _);
            case MortalWoundSkillTierRequirement skill:
                var skillActor = Actor(skill.ActorRole);
                return skill.MinimumTier is >= 0 and <= 5 && TryArt(skillActor.Owner, skillActor.Profile, skill.CapabilityRef, out var tier) && tier >= skill.MinimumTier;
            case MortalWoundProviderRequirement provider:
                return provider.ProviderRef == playerOwner.OwnerId;
            default:
                // The coherent context has no independently parsed canonical proof
                // for these registered shapes yet. Do not fabricate Mortal snapshots,
                // infer quantities from names, reserve costs, or silently ignore them.
                return false;
        }
    }

    private JsonArray BuildRequirementBindings(PathData path, WoundOwnerCoordinate playerOwner, JsonObject player,
        WoundOwnerCoordinate targetOwner, JsonObject target, IReadOnlyList<JsonObject?> proofs, bool stableOnly = false)
    {
        var rows = new JsonArray();
        for (var index = 0; index < path.Requirements.Requirements.Length; index++)
        {
            var requirement = path.Requirements.Requirements[index];
            if (stableOnly && SpiritualDiagnosisCanonicalSources.IsCanonicalKind(requirement.Kind)) continue;
            if (proofs[index] is { } evidence)
            {
                var actorRole = requirement switch
                { MortalWoundItemQuantityRequirement r => r.OwnerRole, MortalWoundResourceQuantityRequirement r => r.OwnerRole,
                    MortalWoundLocationRequirement r => r.TargetRole, MortalWoundEffectStateRequirement r => r.TargetRole, _ => "provider" };
                rows.Add(new JsonObject { ["ordinal"] = index, ["statementFingerprint"] = path.StatementFingerprints[index],
                    ["kind"] = requirement.Kind, ["actor"] = SpiritualHealingRequestProjection.OwnerJson(actorRole == "provider" ? playerOwner : targetOwner),
                    ["evidence"] = evidence.DeepClone(), ["sourceDependencies"] = _canonical.Dependencies(requirement.Kind) });
                continue;
            }
            var role = requirement switch { MortalWoundSourceCapabilityRequirement c => c.ActorRole,
                MortalWoundSkillTierRequirement s => s.ActorRole, _ => "provider" };
            var owner = role == "provider" ? playerOwner : targetOwner;
            var profile = role == "provider" ? player : target;
            var art = requirement switch { MortalWoundSourceCapabilityRequirement c => c.CapabilityRef,
                MortalWoundSkillTierRequirement s => s.CapabilityRef, _ => null };
            int? actualTier = null;
            if (art is not null)
            {
                if (!TryArt(owner, profile, art, out var actual)) throw new InvalidDataException("A previously satisfied diagnosis art changed.");
                actualTier = actual;
            }
            var dependencies = new JsonArray(new JsonObject
            { ["path"] = WoundCarrierCatalog.AfterlifeProfilesPath, ["fingerprint"] = _profileFingerprint });
            if (owner.OwnerKind == "player_soul") dependencies.Add(new JsonObject
            { ["path"] = "game_state/meta/soul_state.json", ["fingerprint"] = _soulFingerprint });
            rows.Add(new JsonObject
            {
                ["ordinal"] = index, ["statementFingerprint"] = path.StatementFingerprints[index],
                ["kind"] = path.Path.Requirements[index].GetProperty("kind").GetString(),
                ["actor"] = SpiritualHealingRequestProjection.OwnerJson(owner), ["capabilityRef"] = art,
                ["tier"] = actualTier, ["mirrorTier"] = owner.OwnerKind == "player_soul" ? actualTier : null,
                ["sourceDependencies"] = dependencies
            });
        }
        return rows;
    }

    private JsonObject? Profile(WoundOwnerCoordinate owner)
    {
        var rows = (_profiles?["profiles"] as JsonArray)?.OfType<JsonObject>()
            .Where(profile => WoundCarrierCollectionAuthority.AfterlifeProfileMatches(profile, owner)).ToArray();
        return rows is { Length: 1 } ? rows[0] : null;
    }

    private bool TryArt(WoundOwnerCoordinate owner, JsonObject profile, string artId, out int tier)
    {
        tier = 0;
        if (artId is not ("spiritual_healing" or "spiritual_stabilization") ||
            !AfterlifeEntityProfileState.TryReadRequiredWoundArtTier(profile, artId, out _, out tier)) return false;
        if (owner.OwnerKind != "player_soul") return true;
        return owner.OwnerId == "player_soul" && _soul["currentRealm"] is JsonValue realmNode && realmNode.TryGetValue<string>(out var realm) &&
            AfterlifeEntityProfileState.TryNormalizeEffectRealm(realm, out var normalized) && normalized == owner.Realm &&
            _soul["afterlifeCombatProfile"] is JsonObject combat && combat["artTiers"] is JsonObject arts &&
            arts[artId] is JsonValue value && value.TryGetValue<int>(out var mirror) && mirror == tier;
    }

    private static bool ResolvesFact(WoundMaterializationEnvelope wound, string fact) => fact.StartsWith("route:", StringComparison.Ordinal)
        ? wound.Treatment.Routes.Any(route => route.RouteId == fact[6..])
        : fact.StartsWith("complication:", StringComparison.Ordinal) && wound.Complications.Any(c => c.ComplicationId == fact[13..]);
    private static SpiritualDiagnosisAdmissionResult Refuse(string code) => new(null, code);
}
