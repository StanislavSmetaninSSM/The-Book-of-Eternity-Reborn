using System.Globalization;

namespace BookOfEternityClient.Services;

internal sealed record WoundSourceEventExport(
    string EventRef,
    string EventSemanticFingerprint,
    string SourceKind,
    string SourceId,
    string SourceState,
    string Realm,
    string Domain);

internal sealed record WoundTargetExport(
    WoundOwnerCoordinate Owner,
    EffectTargetKey EffectTarget,
    bool SameTurn,
    string? TargetRef,
    string DisplayName);

internal sealed record WoundTargetSelector(
    string OwnerKind,
    string? TargetId,
    string? TargetRef,
    string? TargetName);

internal sealed record WoundSourceAuthorityInput(
    WoundAcceptedTurnBinding Binding,
    IReadOnlyList<WoundSourceEventExport> Sources,
    IReadOnlyList<WoundTargetExport> Targets,
    EffectTargetAuthority EffectTargets);

internal sealed record WoundSourceTargetRequest(
    string EventRef,
    string SourceKind,
    string SourceId,
    string SourceState,
    string Realm,
    string Domain,
    WoundTargetSelector Target);

internal sealed record WoundSourceTargetAuthority(
    string EventRef,
    string EventSemanticFingerprint,
    string SourceKind,
    string SourceId,
    string SourceState,
    string Realm,
    string Domain,
    WoundOwnerCoordinate Owner,
    EffectTargetKey EffectTarget,
    bool SameTurn,
    string? TargetRef,
    string AuthorityFingerprint);

internal sealed record WoundSourceTargetResolution(
    WoundSourceTargetAuthority? Authority,
    IReadOnlyList<ValidationIssue> Issues)
{
    internal bool Success => Authority is not null && Issues.Count == 0;
}

internal sealed class WoundSourceAuthority
{
    private const string AuthorityDomain =
        "book_of_eternity.wound.source_target_authority";
    private const int MaximumDisplayNameLength = 512;

    private readonly WoundAcceptedTurnBinding _binding;
    private readonly WoundSourceEventExport[] _sources;
    private readonly WoundTargetExport[] _targets;
    private readonly EffectTargetAuthority _effectTargets;

    private WoundSourceAuthority(
        WoundAcceptedTurnBinding binding,
        IReadOnlyList<WoundSourceEventExport> sources,
        IReadOnlyList<WoundTargetExport> targets,
        EffectTargetAuthority effectTargets,
        IReadOnlyList<ValidationIssue> issues)
    {
        _binding = WoundAcceptedTurnData.CloneBinding(binding)!;
        _sources = issues.Count == 0
            ? sources.Select(Clone).ToArray()
            : Array.Empty<WoundSourceEventExport>();
        _targets = issues.Count == 0
            ? targets.Select(Clone).ToArray()
            : Array.Empty<WoundTargetExport>();
        _effectTargets = effectTargets;
        Issues = issues.ToArray();
    }

    internal IReadOnlyList<ValidationIssue> Issues { get; }

    internal static WoundSourceAuthority Build(WoundSourceAuthorityInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(input.Binding);
        ArgumentNullException.ThrowIfNull(input.Sources);
        ArgumentNullException.ThrowIfNull(input.Targets);
        ArgumentNullException.ThrowIfNull(input.EffectTargets);
        var issues = new List<ValidationIssue>();
        ValidateBinding(input.Binding, issues);
        issues.AddRange(input.EffectTargets.Issues);
        ValidateSources(input.Binding, input.Sources, issues);
        ValidateTargets(input.Binding, input.Targets, input.EffectTargets, issues);
        return new WoundSourceAuthority(
            input.Binding,
            input.Sources,
            input.Targets,
            input.EffectTargets,
            issues);
    }

    internal WoundSourceTargetResolution Resolve(WoundSourceTargetRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var issues = new List<ValidationIssue>();
        if (Issues.Count != 0)
        {
            issues.AddRange(Issues);
            return new WoundSourceTargetResolution(null, issues);
        }

        var sourceMatches = _sources.Where(value => string.Equals(
            value.EventRef,
            request.EventRef,
            StringComparison.Ordinal)).ToArray();
        if (sourceMatches.Length != 1)
        {
            Add(
                issues,
                "woundSource.eventRef",
                "wound_source_event_binding_mismatch",
                "one exact accepted wound source event",
                $"event mismatch: {request.EventRef}");
            return new WoundSourceTargetResolution(null, issues);
        }
        var source = sourceMatches[0];
        if (!string.Equals(source.SourceKind, request.SourceKind, StringComparison.Ordinal))
        {
            Add(
                issues,
                "woundSource.sourceKind",
                "wound_source_event_binding_mismatch",
                source.SourceKind,
                $"source_kind mismatch: {request.SourceKind}");
        }
        if (!string.Equals(source.SourceId, request.SourceId, StringComparison.Ordinal))
        {
            Add(
                issues,
                "woundSource.sourceId",
                "wound_source_event_binding_mismatch",
                source.SourceId,
                $"source_id mismatch: {request.SourceId}");
        }
        if (!string.Equals(source.SourceState, request.SourceState, StringComparison.Ordinal))
        {
            Add(
                issues,
                "woundSource.sourceState",
                "wound_source_event_binding_mismatch",
                source.SourceState,
                $"source_state mismatch: {request.SourceState}");
        }
        if (!string.Equals(source.Realm, request.Realm, StringComparison.Ordinal) ||
            !string.Equals(_binding.Realm, request.Realm, StringComparison.Ordinal))
        {
            Add(
                issues,
                "woundSource.target.realm",
                "wound_target_realm_mismatch",
                source.Realm,
                request.Realm ?? "null");
        }
        if (!string.Equals(source.Domain, request.Domain, StringComparison.Ordinal))
        {
            Add(
                issues,
                "woundSource.domain",
                "wound_source_event_binding_mismatch",
                source.Domain,
                $"domain mismatch: {request.Domain}");
        }
        if (issues.Count != 0)
            return new WoundSourceTargetResolution(null, issues);

        var target = ResolveTarget(request.Target, source.Realm, issues);
        if (target is null || issues.Count != 0)
            return new WoundSourceTargetResolution(null, issues);
        if (!_effectTargets.TryResolveAcceptedTarget(
                target.EffectTarget,
                out var acceptedTarget) ||
            acceptedTarget is null ||
            acceptedTarget.SameTurn != target.SameTurn ||
            !string.Equals(
                acceptedTarget.TargetRef,
                target.TargetRef,
                StringComparison.Ordinal))
        {
            Add(
                issues,
                "woundSource.target",
                "wound_target_effect_authority_mismatch",
                "one exact accepted effect target",
                target.EffectTarget.ToString());
            return new WoundSourceTargetResolution(null, issues);
        }

        var fingerprint = WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
        {
            AuthorityDomain,
            "1",
            _binding.SessionId,
            _binding.RequestId,
            _binding.SnapshotToken,
            _binding.AcceptedEventsFingerprint,
            source.EventRef,
            source.EventSemanticFingerprint,
            source.SourceKind,
            source.SourceId,
            source.SourceState,
            source.Realm,
            source.Domain,
            target.Owner.Realm,
            target.Owner.OwnerKind,
            target.Owner.OwnerId,
            target.Owner.CarrierPath,
            target.EffectTarget.Realm,
            target.EffectTarget.Kind,
            target.EffectTarget.TargetId,
            target.SameTurn.ToString(CultureInfo.InvariantCulture),
            target.TargetRef
        });
        return new WoundSourceTargetResolution(
            new WoundSourceTargetAuthority(
                source.EventRef,
                source.EventSemanticFingerprint,
                source.SourceKind,
                source.SourceId,
                source.SourceState,
                source.Realm,
                source.Domain,
                target.Owner with { },
                target.EffectTarget with { },
                target.SameTurn,
                target.TargetRef,
                fingerprint),
            Array.Empty<ValidationIssue>());
    }

    private WoundTargetExport? ResolveTarget(
        WoundTargetSelector selector,
        string realm,
        ICollection<ValidationIssue> issues)
    {
        if (selector is null || !Exact(selector.OwnerKind))
        {
            Add(
                issues,
                "woundSource.target",
                "wound_target_selector_invalid",
                "one closed wound owner kind and one targetId/targetRef",
                "malformed target selector");
            return null;
        }
        var selectorCount = (selector.TargetId is null ? 0 : 1) +
                            (selector.TargetRef is null ? 0 : 1) +
                            (selector.TargetName is null ? 0 : 1);
        if (selectorCount != 1)
        {
            Add(
                issues,
                "woundSource.target",
                "wound_target_selector_invalid",
                "exactly one targetId, targetRef, or adapter-only targetName",
                "missing or multiple selectors");
            return null;
        }

        var candidates = _targets.Where(value =>
            string.Equals(value.Owner.Realm, realm, StringComparison.Ordinal) &&
            string.Equals(
                value.Owner.OwnerKind,
                selector.OwnerKind,
                StringComparison.Ordinal));
        if (selector.TargetName is not null)
        {
            var named = candidates.Where(value => string.Equals(
                value.DisplayName,
                selector.TargetName,
                StringComparison.Ordinal)).ToArray();
            var code = named.Length > 1
                ? "wound_target_name_ambiguous"
                : "wound_target_name_forbidden";
            Add(
                issues,
                "woundSource.target.targetName",
                code,
                "an exact client-selected targetId or same-turn targetRef",
                selector.TargetName);
            return null;
        }

        WoundTargetExport[] matches;
        if (selector.TargetId is not null)
        {
            matches = candidates.Where(value =>
                !value.SameTurn &&
                string.Equals(
                    value.Owner.OwnerId,
                    selector.TargetId,
                    StringComparison.Ordinal)).ToArray();
        }
        else
        {
            matches = candidates.Where(value =>
                value.SameTurn &&
                string.Equals(
                    value.TargetRef,
                    selector.TargetRef,
                    StringComparison.Ordinal)).ToArray();
        }
        if (matches.Length == 1)
            return Clone(matches[0]);

        Add(
            issues,
            "woundSource.target",
            matches.Length > 1
                ? "wound_target_selector_ambiguous"
                : "wound_target_selector_unresolved",
            "one exact accepted wound target",
            selector.TargetId ?? selector.TargetRef ?? "null");
        return null;
    }

    private static void ValidateBinding(
        WoundAcceptedTurnBinding binding,
        ICollection<ValidationIssue> issues)
    {
        var acceptedEvents = binding.AcceptedEvents;
        if (!Exact(binding.SessionId) ||
            !Exact(binding.RequestId) ||
            !Exact(binding.SnapshotToken) ||
            binding.Turn <= 0 ||
            binding.Realm is not ("mortal_world" or "chaos_sea" or "shining_abode") ||
            acceptedEvents is not { Count: > 0 } ||
            acceptedEvents.Any(value =>
                value is null ||
                !Exact(value.EventRef) ||
                !Exact(value.Kind) ||
                !Exact(value.AuthorityId) ||
                !ResourceMaterializationContract.IsAuthorityFingerprint(
                    value.SemanticFingerprint)) ||
            !string.Equals(
                binding.AcceptedEventsFingerprint,
                WoundAcceptedEventSetFingerprint.Compute(acceptedEvents),
                StringComparison.Ordinal))
        {
            Add(
                issues,
                "woundSource.binding",
                "wound_source_binding_invalid",
                "one exact accepted-turn binding and event-set fingerprint",
                "malformed binding");
        }
    }

    private static void ValidateSources(
        WoundAcceptedTurnBinding binding,
        IReadOnlyList<WoundSourceEventExport> sources,
        ICollection<ValidationIssue> issues)
    {
        var eventRefs = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < sources.Count; index++)
        {
            var value = sources[index];
            var path = $"woundSource.sources[{index}]";
            if (value is null ||
                !Exact(value.EventRef) ||
                !ResourceMaterializationContract.IsAuthorityFingerprint(
                    value.EventSemanticFingerprint) ||
                !Exact(value.SourceKind) ||
                !Exact(value.SourceId) ||
                !Exact(value.SourceState) ||
                value.Realm is not ("mortal_world" or "chaos_sea" or "shining_abode") ||
                value.Domain is not ("physical" or "spiritual") ||
                !eventRefs.Add(value.EventRef))
            {
                Add(
                    issues,
                    path,
                    "wound_source_event_invalid",
                    "one unique complete source row per accepted event",
                    "malformed, duplicate, or confusable source event");
                continue;
            }

            var accepted = binding.AcceptedEvents.Where(eventValue =>
                eventValue is not null &&
                string.Equals(
                    eventValue.EventRef,
                    value.EventRef,
                    StringComparison.Ordinal)).ToArray();
            if (accepted.Length != 1 ||
                !string.Equals(
                    accepted[0].SemanticFingerprint,
                    value.EventSemanticFingerprint,
                    StringComparison.Ordinal))
            {
                Add(
                    issues,
                    path + ".eventSemanticFingerprint",
                    "wound_source_event_fingerprint_mismatch",
                    accepted.Length == 1
                        ? accepted[0].SemanticFingerprint
                        : "one exact accepted event fingerprint",
                    value.EventSemanticFingerprint);
            }
            if (!string.Equals(value.Realm, binding.Realm, StringComparison.Ordinal))
            {
                Add(
                    issues,
                    path + ".realm",
                    "wound_source_realm_mismatch",
                    binding.Realm,
                    value.Realm);
            }
        }
    }

    private static void ValidateTargets(
        WoundAcceptedTurnBinding binding,
        IReadOnlyList<WoundTargetExport> targets,
        EffectTargetAuthority effectTargets,
        ICollection<ValidationIssue> issues)
    {
        var ownerKeys = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < targets.Count; index++)
        {
            var value = targets[index];
            var path = $"woundSource.targets[{index}]";
            if (value is null ||
                !OwnerIsValid(value.Owner) ||
                value.EffectTarget is null ||
                !ReadableName(value.DisplayName) ||
                (value.SameTurn != (value.TargetRef is not null)) ||
                (value.TargetRef is not null && !Exact(value.TargetRef)))
            {
                Add(
                    issues,
                    path,
                    "wound_target_export_invalid",
                    "one complete owner/effect-target export",
                    "malformed target export");
                continue;
            }
            var ownerKey = string.Join('\u001f',
                value.Owner.Realm,
                value.Owner.OwnerKind,
                value.Owner.OwnerId,
                value.Owner.CarrierPath);
            if (!ownerKeys.Add(ownerKey))
            {
                Add(
                    issues,
                    path,
                    "wound_target_export_duplicate",
                    "one target export per exact wound owner",
                    ownerKey);
            }
            if (!string.Equals(value.Owner.Realm, binding.Realm, StringComparison.Ordinal) ||
                !string.Equals(
                    value.EffectTarget.Realm,
                    binding.Realm,
                    StringComparison.Ordinal))
            {
                Add(
                    issues,
                    path + ".realm",
                    "wound_target_realm_mismatch",
                    binding.Realm,
                    value.Owner.Realm);
            }
            var expectedEffectKind = EffectKind(value.Owner.OwnerKind);
            if (!string.Equals(
                    expectedEffectKind,
                    value.EffectTarget.Kind,
                    StringComparison.Ordinal) ||
                (value.Owner.OwnerKind != "combatant_member" &&
                 !string.Equals(
                     value.Owner.OwnerId,
                     value.EffectTarget.TargetId,
                     StringComparison.Ordinal)) ||
                !effectTargets.TryResolveAcceptedTarget(
                    value.EffectTarget,
                    out var accepted) ||
                accepted is null ||
                accepted.SameTurn != value.SameTurn ||
                !string.Equals(
                    accepted.TargetRef,
                    value.TargetRef,
                    StringComparison.Ordinal))
            {
                Add(
                    issues,
                    path + ".effectTarget",
                    "wound_target_effect_authority_mismatch",
                    "the exact mapped accepted effect target",
                    value.EffectTarget.ToString());
            }
        }
    }

    private static string? EffectKind(string ownerKind) => ownerKind switch
    {
        "player_soul" => "player",
        "combatant_member" => "combatant",
        "player" or "npc" or "combatant" or "guardian" or "resident" or
            "radiant_actor" or "afterlife_actor" => ownerKind,
        _ => null
    };

    private static bool OwnerIsValid(WoundOwnerCoordinate value) => value switch
    {
        { Realm: "mortal_world", OwnerKind: "player", OwnerId: "player_current",
            CarrierPath: WoundCarrierCatalog.PlayerPath } => true,
        { Realm: "mortal_world", OwnerKind: "npc",
            CarrierPath: WoundCarrierCatalog.NpcPath } => Exact(value.OwnerId),
        { Realm: "mortal_world", OwnerKind: "combatant" or "combatant_member",
            CarrierPath: WoundCarrierCatalog.EnemiesPath or
                WoundCarrierCatalog.AlliesPath } => Exact(value.OwnerId),
        { Realm: "chaos_sea" or "shining_abode",
            OwnerKind: "player_soul" or "guardian" or "resident" or
                "radiant_actor" or "afterlife_actor",
            CarrierPath: WoundCarrierCatalog.AfterlifeProfilesPath } =>
            Exact(value.OwnerId),
        _ => false
    };

    private static bool ReadableName(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        string.Equals(value, value.Trim(), StringComparison.Ordinal) &&
        value.Length <= MaximumDisplayNameLength &&
        !value.Any(char.IsControl);

    private static bool Exact(string? value) =>
        ResourceMaterializationContract.IsExactIdentifier(value);

    private static WoundSourceEventExport Clone(WoundSourceEventExport value) =>
        value with { };

    private static WoundTargetExport Clone(WoundTargetExport value) =>
        value with
        {
            Owner = value.Owner with { },
            EffectTarget = value.EffectTarget with { }
        };

    private static void Add(
        ICollection<ValidationIssue> issues,
        string path,
        string code,
        string expected,
        string actual) => issues.Add(new ValidationIssue(
        path,
        IssueSeverity.Error,
        "Wound source or target violates accepted event authority.",
        code: code,
        section: "wound_source_target_authority",
        expected: expected,
        actual: actual,
        repairHint:
            "Use the exact accepted event source and a client-selected current target."));
}
