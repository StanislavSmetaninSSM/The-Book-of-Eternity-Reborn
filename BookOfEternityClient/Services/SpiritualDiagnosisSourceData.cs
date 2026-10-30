using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal sealed record SpiritualDiagnosisAdmission(
    SpiritualSelfTreatmentSelection Selection, WoundMaterializationEnvelope Wound,
    int HealingTier, string PathFingerprint, string RequirementFingerprint, string SourceFingerprint);

internal sealed record SpiritualDiagnosisAdmissionResult(SpiritualDiagnosisAdmission? Data, string? IssueCode);

/// <summary>One detached menu operation's parsed DATA. Never a live source/publication capability.</summary>
internal sealed class SpiritualDiagnosisSourceData
{
    private sealed record PathData(WoundDiagnosisPath Path, string Fingerprint,
        string RequirementFingerprint, WoundRequirementProjection Requirements);
    private sealed record WoundData(WoundMaterializationEnvelope Wound, string Fingerprint,
        IReadOnlyList<PathData> Paths, IReadOnlySet<string> KnownFacts);
    private readonly JsonObject? _profiles;
    private readonly JsonObject _soul;
    private readonly string _realm;
    private readonly bool _selfVisible;
    private readonly bool _conflict;
    private readonly IReadOnlyList<WoundData> _wounds;
    private readonly string _sourceFingerprint;

    internal SpiritualDiagnosisSourceData(WoundPlayerReadContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _profiles = context.Input.Carriers.AfterlifeProfiles?.DeepClone().AsObject();
        _soul = context.Soul.DeepClone().AsObject();
        _realm = context.Input.Realm; _selfVisible = context.Input.SelfVisible;
        _conflict = context.SpiritualConflict?["activeConflict"] is not null;
        _sourceFingerprint = WoundAcceptedTurnFingerprintWriter.Compute(new[]
        {
            "book_of_eternity.spiritual_diagnosis.menu_sources", "1", _realm,
            WoundAcceptedTurnFingerprintWriter.CanonicalJson(_profiles),
            WoundAcceptedTurnFingerprintWriter.CanonicalJson(_soul)
        });
        _wounds = context.Catalog.Occurrences.Where(occurrence => occurrence.Wound.Owner.Realm == _realm &&
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
                MortalWoundTreatmentContract.ParseRequirementProjection(path.Requirements, path.SourcePath + ".requirements"))).ToArray();
            return new WoundData(wound, WoundIdentityState.ComputeSemanticFingerprint(wound), paths,
                wound.Treatment.KnownRouteIds.Select(id => "route:" + id)
                    .Concat(wound.Complications.Where(c => WoundPlayerProjection.Visible(c.Visibility)).Select(c => "complication:" + c.ComplicationId))
                    .ToHashSet(StringComparer.Ordinal));
        }).ToArray();
    }

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
        foreach (var requirement in parsed.Requirements.Requirements)
            if (!Satisfied(requirement, self, player, wound.Owner, target)) return Refuse("requirement_unavailable");
        var selection = new SpiritualSelfTreatmentSelection(wound.WoundId, data.Fingerprint)
        { Diagnosis = new(wound.Owner, path.DiagnosisPathId, parsed.Fingerprint) };
        return new(new(selection, wound, tier, parsed.Fingerprint, parsed.RequirementFingerprint,
            WoundAcceptedTurnFingerprintWriter.Compute(new[] { "spiritual_diagnosis.admission", "1", _sourceFingerprint,
                data.Fingerprint, parsed.Fingerprint, parsed.RequirementFingerprint, currentTarget?.BindingFingerprint ?? "self" })), null);
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
