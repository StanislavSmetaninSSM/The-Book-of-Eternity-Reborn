using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal sealed record SpiritualWoundConflictContribution(
    string EffectId,
    string ComponentId,
    EffectSourceKey WoundSource,
    EffectTargetKey Actor,
    string ResolvedSide,
    SpiritualWoundProjectionKind ProjectionKind,
    string Profile,
    string Operation,
    string SourceAxis,
    string ResolvedAxis,
    JsonElement Magnitude,
    int Priority,
    int CurrentStacks,
    JsonElement ProfilePayload);

internal sealed class SpiritualWoundConflictContributionProjection
{
    internal SpiritualWoundConflictContributionProjection(
        IEnumerable<SpiritualWoundConflictContribution> contributions,
        IEnumerable<ValidationIssue> issues)
    {
        Contributions = new ReadOnlyCollection<SpiritualWoundConflictContribution>(
            contributions.Select(Clone).ToArray());
        Issues = new ReadOnlyCollection<ValidationIssue>(issues.ToArray());
    }

    internal bool IsAccepted => Issues.Count == 0;

    internal IReadOnlyList<SpiritualWoundConflictContribution> Contributions { get; }

    internal IReadOnlyList<ValidationIssue> Issues { get; }

    private static SpiritualWoundConflictContribution Clone(
        SpiritualWoundConflictContribution value) =>
        value with
        {
            WoundSource = new EffectSourceKey(
                value.WoundSource.Realm,
                value.WoundSource.Kind,
                value.WoundSource.SourceId,
                value.WoundSource.DefinitionKey),
            Actor = new EffectTargetKey(
                value.Actor.Realm,
                value.Actor.Kind,
                value.Actor.TargetId),
            Magnitude = value.Magnitude.Clone(),
            ProfilePayload = value.ProfilePayload.Clone()
        };
}

internal static class SpiritualWoundConflictContributionProjector
{
    private const string Section = "spiritual_wound_contribution";
    private const string ConflictPath =
        "game_state/meta/afterlife_spiritual_conflict_state.json.activeConflict";
    private static readonly HashSet<string> LiveResolutionStates =
        new(StringComparer.Ordinal)
        {
            "active",
            "concession_pending",
            "surrender_pending",
            "retreat_pending",
            "ready_to_resolve"
        };

    internal static SpiritualWoundConflictContributionProjection Project(
        EffectMechanicsSnapshot snapshot,
        EffectSourceAuthority sourceAuthority,
        EffectTargetAuthority targetAuthority,
        JsonObject? spiritualConflictRoot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(sourceAuthority);
        ArgumentNullException.ThrowIfNull(targetAuthority);

        if (spiritualConflictRoot == null ||
            !spiritualConflictRoot.TryGetPropertyValue("activeConflict", out var activeNode) ||
            activeNode == null)
        {
            return AcceptedEmpty();
        }

        if (activeNode is not JsonObject active)
        {
            return Rejected(NewIssue(
                ConflictPath,
                "spiritual_wound_conflict_invalid",
                "active conflict object or null",
                activeNode.ToJsonString()));
        }

        if (!TryReadExact(active["resolutionState"], out var resolutionState))
        {
            return Rejected(NewIssue(
                ConflictPath + ".resolutionState",
                "spiritual_wound_conflict_state_invalid",
                "exact current conflict resolution state",
                Describe(active["resolutionState"])));
        }

        if (!LiveResolutionStates.Contains(resolutionState))
        {
            return Rejected(NewIssue(
                ConflictPath + ".resolutionState",
                "spiritual_wound_conflict_state_invalid",
                "active, concession_pending, surrender_pending, retreat_pending, or ready_to_resolve while activeConflict exists",
                resolutionState));
        }

        if (!snapshot.IsAccepted)
        {
            return Rejected(NewIssue(
                "accepted_effect_mechanics_snapshot_v1",
                "spiritual_wound_snapshot_rejected",
                "accepted current-generation effect mechanics snapshot",
                "rejected"));
        }

        if (sourceAuthority.Issues.Count != 0)
        {
            return Rejected(NewIssue(
                "effect_source_authority",
                "spiritual_wound_source_authority_rejected",
                "sealed accepted current-generation source authority",
                "rejected"));
        }

        if (targetAuthority.Issues.Count != 0)
        {
            return Rejected(NewIssue(
                "effect_target_authority",
                "spiritual_wound_target_authority_rejected",
                "sealed accepted current-generation target authority",
                "rejected"));
        }

        var membership = BuildMembership(active, targetAuthority);
        if (membership.Issues.Count != 0)
            return new SpiritualWoundConflictContributionProjection(
                Array.Empty<SpiritualWoundConflictContribution>(),
                membership.Issues);

        var contributions = new List<SpiritualWoundConflictContribution>();
        var issues = new List<ValidationIssue>();
        foreach (var component in snapshot.Components)
        {
            if (!SpiritualWoundEffectProfileCatalog.TryGetProfile(
                    component.Profile,
                    out var descriptor) ||
                !string.Equals(component.Realm, membership.Realm, StringComparison.Ordinal))
            {
                continue;
            }

            var actor = new EffectTargetKey(
                component.Realm,
                component.TargetKind,
                component.TargetId);
            if (!membership.Sides.TryGetValue(actor, out var side))
                continue;

            var path = $"effects[{component.EffectId}].components[{component.ComponentId}]";
            if (!TryVerifySource(
                    component,
                    actor,
                    sourceAuthority,
                    path,
                    issues))
            {
                continue;
            }

            if (!TryReadProfilePayload(
                    component,
                    descriptor,
                    path,
                    issues,
                    out var operation,
                    out var magnitude))
            {
                continue;
            }

            var componentSource = component.Source!;
            contributions.Add(new SpiritualWoundConflictContribution(
                component.EffectId,
                component.ComponentId,
                new EffectSourceKey(
                    componentSource.Realm,
                    componentSource.Kind,
                    componentSource.SourceId,
                    componentSource.DefinitionKey),
                new EffectTargetKey(actor.Realm, actor.Kind, actor.TargetId),
                side,
                descriptor.ProjectionKind,
                component.Profile,
                operation,
                descriptor.Axis,
                ResolveAxis(descriptor.Axis, side),
                magnitude.Clone(),
                component.Priority,
                component.CurrentStacks,
                component.Payload.Clone()));
        }

        if (issues.Count != 0)
        {
            return new SpiritualWoundConflictContributionProjection(
                Array.Empty<SpiritualWoundConflictContribution>(),
                issues);
        }

        return new SpiritualWoundConflictContributionProjection(
            contributions
                .OrderBy(static row => row.Priority)
                .ThenBy(static row => row.EffectId, StringComparer.Ordinal)
                .ThenBy(static row => row.ComponentId, StringComparer.Ordinal),
            Array.Empty<ValidationIssue>());
    }

    private static MembershipProjection BuildMembership(
        JsonObject active,
        EffectTargetAuthority targetAuthority)
    {
        var issues = new List<ValidationIssue>();
        if (!TryReadExact(active["conflictId"], out var conflictId))
        {
            issues.Add(NewIssue(
                ConflictPath + ".conflictId",
                "spiritual_wound_conflict_identity_invalid",
                "exact active conflictId",
                Describe(active["conflictId"])));
        }

        if (!TryReadExact(active["realm"], out var declaredRealm) ||
            !AfterlifeEntityProfileState.TryNormalizeEffectRealm(
                declaredRealm,
                out var realm))
        {
            issues.Add(NewIssue(
                ConflictPath + ".realm",
                "spiritual_wound_conflict_realm_invalid",
                "exact Chaos Sea or Shining Abode realm",
                Describe(active["realm"])));
            realm = string.Empty;
        }

        var participants = new List<Participant>();
        AddSide(active, "player", participants, issues);
        AddSide(active, "opposition", participants, issues);

        var exact = new HashSet<string>(StringComparer.Ordinal);
        var aliases = new HashSet<string>(StringComparer.Ordinal);
        foreach (var participant in participants)
        {
            if (!exact.Add(participant.ActorId) ||
                !aliases.Add(MortalLocationIdentityState.BuildConfusableKey(
                    participant.ActorId)))
            {
                issues.Add(NewIssue(
                    participant.Path + ".actorId",
                    "spiritual_wound_conflict_participant_ambiguous",
                    "one exact and confusable-unique current participant identity",
                    participant.ActorId));
            }
        }

        var sides = new Dictionary<EffectTargetKey, string>();
        if (issues.Count == 0)
        {
            foreach (var participant in participants)
            {
                if (!TryMapActorKind(participant.ActorType, out var kind))
                {
                    issues.Add(NewIssue(
                        participant.Path + ".actorType",
                        "spiritual_wound_conflict_participant_kind_invalid",
                        "closed persistent afterlife actor type",
                        participant.ActorType));
                    continue;
                }

                var key = new EffectTargetKey(realm, kind, participant.ActorId);
                if (!targetAuthority.TryResolveAcceptedTarget(key, out _))
                {
                    var resolution = targetAuthority.Resolve(
                        new JsonObject
                        {
                            ["kind"] = kind,
                            ["targetId"] = participant.ActorId
                        },
                        realm);
                    var code = resolution.Issues.Any(static issue =>
                        string.Equals(
                            issue.Code,
                            "effect_target_realm_mismatch",
                            StringComparison.Ordinal))
                        ? "spiritual_wound_conflict_participant_realm_mismatch"
                        : "spiritual_wound_conflict_participant_unresolved";
                    issues.Add(NewIssue(
                        participant.Path,
                        code,
                        "one exact current persistent target in the conflict realm",
                        key.ToString()));
                    continue;
                }

                sides.Add(key, participant.Side);
            }
        }

        return new MembershipProjection(
            conflictId,
            realm,
            new ReadOnlyDictionary<EffectTargetKey, string>(sides),
            new ReadOnlyCollection<ValidationIssue>(issues));
    }

    private static void AddSide(
        JsonObject active,
        string side,
        ICollection<Participant> participants,
        ICollection<ValidationIssue> issues)
    {
        var sidePath = ConflictPath + "." + side + "Side";
        if (active[side + "Side"] is not JsonObject sideState ||
            sideState["leadContestant"] is not JsonObject lead ||
            sideState["supporters"] is not JsonArray supporters)
        {
            issues.Add(NewIssue(
                sidePath,
                "spiritual_wound_conflict_membership_invalid",
                "side object with one leadContestant and supporters array",
                Describe(active[side + "Side"])));
            return;
        }

        AddParticipant(lead, side, sidePath + ".leadContestant", participants, issues);
        for (var index = 0; index < supporters.Count; index++)
        {
            if (supporters[index] is not JsonObject supporter)
            {
                issues.Add(NewIssue(
                    $"{sidePath}.supporters[{index}]",
                    "spiritual_wound_conflict_membership_invalid",
                    "participant object",
                    Describe(supporters[index])));
                continue;
            }

            AddParticipant(
                supporter,
                side,
                $"{sidePath}.supporters[{index}]",
                participants,
                issues);
        }
    }

    private static void AddParticipant(
        JsonObject node,
        string side,
        string path,
        ICollection<Participant> participants,
        ICollection<ValidationIssue> issues)
    {
        if (!TryReadExact(node["actorType"], out var actorType) ||
            !TryReadExact(node["actorId"], out var actorId))
        {
            issues.Add(NewIssue(
                path,
                "spiritual_wound_conflict_participant_invalid",
                "exact actorType and actorId",
                node.ToJsonString()));
            return;
        }

        participants.Add(new Participant(actorType, actorId, side, path));
    }

    private static bool TryVerifySource(
        EffectMechanicalComponent component,
        EffectTargetKey actor,
        EffectSourceAuthority sourceAuthority,
        string path,
        ICollection<ValidationIssue> issues)
    {
        var source = component.Source;
        if (source == null ||
            !string.Equals(source.Realm, component.Realm, StringComparison.Ordinal) ||
            !string.Equals(source.Kind, "wound", StringComparison.Ordinal))
        {
            issues.Add(NewIssue(
                path + ".source",
                "spiritual_wound_source_invalid",
                "typed same-realm wound source",
                source?.ToString() ?? "missing"));
            return false;
        }

        var resolution = sourceAuthority.ResolveCanonicalBinding(
            source,
            actor.Kind);
        if (!resolution.Success || resolution.Source == null)
        {
            issues.Add(NewIssue(
                path + ".source",
                "spiritual_wound_source_unresolved",
                "one exact current canonical wound source definition",
                source.ToString()));
            return false;
        }

        var definition = resolution.Source.Definition;
        var matchingComponents = definition["components"] is JsonArray components
            ? components
                .OfType<JsonObject>()
                .Where(candidate =>
                    TryReadExact(candidate["componentId"], out var componentId) &&
                    string.Equals(
                        componentId,
                        component.ComponentId,
                        StringComparison.Ordinal))
                .ToArray()
            : Array.Empty<JsonObject>();
        JsonNode? acceptedPayload = component.Payload.ValueKind == JsonValueKind.Object
            ? JsonNode.Parse(component.Payload.GetRawText())
            : null;
        if (matchingComponents.Length != 1 ||
            !TryReadExact(matchingComponents[0]["profile"], out var profile) ||
            !string.Equals(profile, component.Profile, StringComparison.Ordinal) ||
            !TryReadExactInt(matchingComponents[0]["priority"], out var priority) ||
            priority != component.Priority ||
            matchingComponents[0]["payload"] is not JsonObject definitionPayload ||
            acceptedPayload is not JsonObject ||
            !JsonNode.DeepEquals(definitionPayload, acceptedPayload))
        {
            issues.Add(NewIssue(
                path,
                "spiritual_wound_source_component_mismatch",
                "exact source componentId/profile/priority/payload agreement",
                component.Profile));
            return false;
        }

        var woundLinks = definition["links"] is JsonArray links
            ? links
                .OfType<JsonObject>()
                .Where(link =>
                    TryReadExact(link["kind"], out var kind) &&
                    string.Equals(kind, "wound", StringComparison.Ordinal) &&
                    TryReadExact(link["role"], out var role) &&
                    string.Equals(role, "source", StringComparison.Ordinal))
                .ToArray()
            : Array.Empty<JsonObject>();
        if (woundLinks.Length != 1 ||
            !TryReadExact(woundLinks[0]["targetId"], out var woundId) ||
            !string.Equals(woundId, source.SourceId, StringComparison.Ordinal))
        {
            issues.Add(NewIssue(
                path + ".source",
                "spiritual_wound_source_link_mismatch",
                "one exact source wound link equal to sourceId",
                source.SourceId));
            return false;
        }

        return true;
    }

    private static bool TryReadProfilePayload(
        EffectMechanicalComponent component,
        SpiritualWoundProfileDescriptor descriptor,
        string path,
        ICollection<ValidationIssue> issues,
        out string operation,
        out JsonElement magnitude)
    {
        operation = string.Empty;
        magnitude = default;
        var payload = component.Payload;
        if (payload.ValueKind != JsonValueKind.Object ||
            !payload.TryGetProperty("operation", out var operationNode) ||
            operationNode.ValueKind != JsonValueKind.String ||
            operationNode.GetString() is not string exactOperation ||
            exactOperation.Length == 0 ||
            !string.Equals(exactOperation, exactOperation.Trim(), StringComparison.Ordinal) ||
            !descriptor.LegalOperations.Contains(exactOperation) ||
            !payload.TryGetProperty("axis", out var axisNode) ||
            axisNode.ValueKind != JsonValueKind.String ||
            !string.Equals(axisNode.GetString(), descriptor.Axis, StringComparison.Ordinal) ||
            !payload.TryGetProperty("magnitude", out magnitude) ||
            !descriptor.IsMagnitudeValid(magnitude))
        {
            issues.Add(NewIssue(
                path + ".payload",
                "spiritual_wound_profile_payload_invalid",
                "registered operation, source axis, and bounded magnitude",
                payload.GetRawText()));
            magnitude = default;
            return false;
        }

        operation = exactOperation;
        magnitude = magnitude.Clone();
        return true;
    }

    private static string ResolveAxis(string sourceAxis, string side) =>
        sourceAxis switch
        {
            "actionCostAudit" => "actionCostAudit." + side,
            "sideStrain" when string.Equals(side, "player", StringComparison.Ordinal) =>
                "playerSideStrain",
            "sideStrain" => "oppositionSideStrain",
            _ => sourceAxis
        };

    private static bool TryMapActorKind(string actorType, out string kind)
    {
        kind = actorType switch
        {
            "player" or "player_soul" or "soul" => "player",
            "guardian" => "guardian",
            "resident" or "shining_resident" => "resident",
            "radiant_actor" => "radiant_actor",
            "afterlife_actor" or "shining_faction_head" or "saref_agent" or
                "system_actor" or "custom_afterlife_actor" => "afterlife_actor",
            _ => string.Empty
        };
        return kind.Length != 0;
    }

    private static bool TryReadExact(JsonNode? node, out string value)
    {
        value = string.Empty;
        if (node is not JsonValue jsonValue ||
            !jsonValue.TryGetValue<string>(out var text) ||
            text.Length == 0 ||
            !string.Equals(text, text.Trim(), StringComparison.Ordinal))
        {
            return false;
        }

        value = text;
        return true;
    }

    private static bool TryReadExactInt(JsonNode? node, out int value)
    {
        value = 0;
        return node is JsonValue jsonValue &&
               jsonValue.TryGetValue<int>(out value) &&
               string.Equals(
                   node.ToJsonString(),
                   value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                   StringComparison.Ordinal);
    }

    private static SpiritualWoundConflictContributionProjection AcceptedEmpty() =>
        new(
            Array.Empty<SpiritualWoundConflictContribution>(),
            Array.Empty<ValidationIssue>());

    private static SpiritualWoundConflictContributionProjection Rejected(
        params ValidationIssue[] issues) =>
        new(Array.Empty<SpiritualWoundConflictContribution>(), issues);

    private static ValidationIssue NewIssue(
        string path,
        string code,
        string expected,
        string actual) =>
        new(
            path,
            IssueSeverity.Error,
            "The persistent spiritual-wound contribution could not be proven.",
            code: code,
            section: Section,
            expected: expected,
            actual: actual,
            repairHint:
                "Repair the current conflict membership or canonical wound/effect authority, then resubmit the full turn.");

    private static string Describe(JsonNode? node) =>
        node?.ToJsonString() ?? "missing";

    private sealed record Participant(
        string ActorType,
        string ActorId,
        string Side,
        string Path);

    private sealed record MembershipProjection(
        string ConflictId,
        string Realm,
        IReadOnlyDictionary<EffectTargetKey, string> Sides,
        IReadOnlyList<ValidationIssue> Issues);
}
