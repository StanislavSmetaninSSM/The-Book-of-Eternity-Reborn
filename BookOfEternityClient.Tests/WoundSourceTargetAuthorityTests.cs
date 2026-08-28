using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class WoundSourceTargetAuthorityTests
{
    [Theory]
    [InlineData(
        "mortal_world", "player", "player_current",
        "game_state/player/wounds.json", "player", "player_current")]
    [InlineData(
        "mortal_world", "npc", "npc_healer",
        "game_state/npcs/npc_wounds.json", "npc", "npc_healer")]
    [InlineData(
        "mortal_world", "combatant", "combatant_raider",
        "game_state/combat/enemies.json", "combatant", "combatant_raider")]
    [InlineData(
        "chaos_sea", "guardian", "guardian_mirror",
        "game_state/meta/afterlife_entity_profiles.json", "guardian", "guardian_mirror")]
    [InlineData(
        "shining_abode", "resident", "resident_lumen",
        "game_state/meta/afterlife_entity_profiles.json", "resident", "resident_lumen")]
    [InlineData(
        "shining_abode", "radiant_actor", "radiant_actor_iris",
        "game_state/meta/afterlife_entity_profiles.json", "radiant_actor", "radiant_actor_iris")]
    [InlineData(
        "chaos_sea", "player_soul", "player_current",
        "game_state/meta/afterlife_entity_profiles.json", "player", "player_current")]
    public void Resolve_ExactSupportedOwnerBindsOneEffectTarget(
        string realm,
        string ownerKind,
        string ownerId,
        string carrierPath,
        string effectTargetKind,
        string effectTargetId)
    {
        var fixture = BuildFixture(
            realm,
            new WoundTargetExport(
                new WoundOwnerCoordinate(realm, ownerKind, ownerId, carrierPath),
                new EffectTargetKey(realm, effectTargetKind, effectTargetId),
                SameTurn: false,
                TargetRef: null,
                DisplayName: "Проверяемая цель"));

        var result = fixture.Authority.Resolve(new WoundSourceTargetRequest(
            fixture.EventRef,
            fixture.SourceKind,
            fixture.SourceId,
            "active",
            realm,
            DomainFor(realm),
            new WoundTargetSelector(
                ownerKind,
                ownerId,
                TargetRef: null,
                TargetName: null)));

        Assert.True(result.Success);
        Assert.Empty(result.Issues);
        var resolved = Assert.IsType<WoundSourceTargetAuthority>(result.Authority);
        Assert.Equal(
            new WoundOwnerCoordinate(realm, ownerKind, ownerId, carrierPath),
            resolved.Owner);
        Assert.Equal(
            new EffectTargetKey(realm, effectTargetKind, effectTargetId),
            resolved.EffectTarget);
        Assert.Equal(fixture.EventRef, resolved.EventRef);
        Assert.Equal(fixture.SourceId, resolved.SourceId);
        Assert.StartsWith("sha256:", resolved.AuthorityFingerprint);
    }

    [Fact]
    public void Resolve_DuplicateDisplayNameNeverBecomesTargetAuthority()
    {
        var first = new WoundTargetExport(
            new WoundOwnerCoordinate(
                "mortal_world",
                "npc",
                "npc_guard_one",
                "game_state/npcs/npc_wounds.json"),
            new EffectTargetKey("mortal_world", "npc", "npc_guard_one"),
            SameTurn: false,
            TargetRef: null,
            DisplayName: "Страж");
        var second = new WoundTargetExport(
            new WoundOwnerCoordinate(
                "mortal_world",
                "npc",
                "npc_guard_two",
                "game_state/npcs/npc_wounds.json"),
            new EffectTargetKey("mortal_world", "npc", "npc_guard_two"),
            SameTurn: false,
            TargetRef: null,
            DisplayName: "Страж");
        var fixture = BuildFixture("mortal_world", first, second);

        var result = fixture.Authority.Resolve(new WoundSourceTargetRequest(
            fixture.EventRef,
            fixture.SourceKind,
            fixture.SourceId,
            "active",
            "mortal_world",
            "physical",
            new WoundTargetSelector(
                "npc",
                TargetId: null,
                TargetRef: null,
                TargetName: "Страж")));

        Assert.False(result.Success);
        Assert.Null(result.Authority);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "wound_target_name_ambiguous" &&
            issue.Actual == "Страж");
    }

    [Fact]
    public void Resolve_TargetFromAnotherRealmFailsClosed()
    {
        var target = new WoundTargetExport(
            new WoundOwnerCoordinate(
                "chaos_sea",
                "guardian",
                "guardian_mirror",
                "game_state/meta/afterlife_entity_profiles.json"),
            new EffectTargetKey("chaos_sea", "guardian", "guardian_mirror"),
            SameTurn: false,
            TargetRef: null,
            DisplayName: "Зеркальный хранитель");
        var fixture = BuildFixture("chaos_sea", target);

        var result = fixture.Authority.Resolve(new WoundSourceTargetRequest(
            fixture.EventRef,
            fixture.SourceKind,
            fixture.SourceId,
            "active",
            "mortal_world",
            "spiritual",
            new WoundTargetSelector(
                "guardian",
                "guardian_mirror",
                TargetRef: null,
                TargetName: null)));

        Assert.False(result.Success);
        Assert.Null(result.Authority);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "wound_target_realm_mismatch");
    }

    [Theory]
    [InlineData("event", "turn_42:foreign_event", "combat_action_test_001", "active")]
    [InlineData("source_id", "turn_42:wound_capable_event", "combat_action_foreign", "active")]
    [InlineData("source_state", "turn_42:wound_capable_event", "combat_action_test_001", "inactive")]
    public void Resolve_SourceCoordinatesCannotMoveAwayFromAcceptedEvent(
        string mutation,
        string eventRef,
        string sourceId,
        string sourceState)
    {
        var target = new WoundTargetExport(
            new WoundOwnerCoordinate(
                "mortal_world",
                "player",
                "player_current",
                "game_state/player/wounds.json"),
            new EffectTargetKey("mortal_world", "player", "player_current"),
            SameTurn: false,
            TargetRef: null,
            DisplayName: "Вы");
        var fixture = BuildFixture("mortal_world", target);

        var result = fixture.Authority.Resolve(new WoundSourceTargetRequest(
            eventRef,
            fixture.SourceKind,
            sourceId,
            sourceState,
            "mortal_world",
            "physical",
            new WoundTargetSelector(
                "player",
                "player_current",
                TargetRef: null,
                TargetName: null)));

        Assert.False(result.Success);
        Assert.Null(result.Authority);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "wound_source_event_binding_mismatch" &&
            issue.Actual!.Contains(mutation, StringComparison.Ordinal));
    }

    [Fact]
    public void Build_SourceEventSealMustMatchExactAcceptedEvent()
    {
        var target = new WoundTargetExport(
            new WoundOwnerCoordinate(
                "mortal_world",
                "player",
                "player_current",
                "game_state/player/wounds.json"),
            new EffectTargetKey("mortal_world", "player", "player_current"),
            SameTurn: false,
            TargetRef: null,
            DisplayName: "Вы");
        var fixture = CreateFixtureInputs("mortal_world", target);
        var forgedSource = fixture.Source with
        {
            EventSemanticFingerprint =
                "sha256:ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff"
        };

        var authority = WoundSourceAuthority.Build(new WoundSourceAuthorityInput(
            fixture.Binding,
            new[] { forgedSource },
            fixture.Targets,
            fixture.EffectTargets));

        Assert.Contains(authority.Issues, issue =>
            issue.Code == "wound_source_event_fingerprint_mismatch");
    }

    [Fact]
    public void Build_MalformedExportFailsClosedWithoutCloningIt()
    {
        var inputs = CreateFixtureInputs("mortal_world", new WoundTargetExport(
            new WoundOwnerCoordinate(
                "mortal_world",
                "player",
                "player_current",
                "game_state/player/wounds.json"),
            new EffectTargetKey("mortal_world", "player", "player_current"),
            SameTurn: false,
            TargetRef: null,
            DisplayName: "Вы"));

        var authority = WoundSourceAuthority.Build(new WoundSourceAuthorityInput(
            inputs.Binding,
            new WoundSourceEventExport[] { null! },
            inputs.Targets,
            inputs.EffectTargets));

        Assert.Contains(authority.Issues, issue =>
            issue.Code == "wound_source_event_invalid");
    }

    [Fact]
    public void Build_MalformedAcceptedEventFailsClosed()
    {
        var inputs = CreateFixtureInputs("mortal_world", new WoundTargetExport(
            new WoundOwnerCoordinate(
                "mortal_world",
                "player",
                "player_current",
                "game_state/player/wounds.json"),
            new EffectTargetKey("mortal_world", "player", "player_current"),
            SameTurn: false,
            TargetRef: null,
            DisplayName: "Вы"));
        var malformedEvents = new WoundAcceptedEventAuthority[] { null! };
        var malformedBinding = new WoundAcceptedTurnBinding(
            inputs.Binding.SessionId,
            inputs.Binding.RequestId,
            inputs.Binding.SnapshotToken,
            inputs.Binding.Realm,
            inputs.Binding.Turn,
            malformedEvents,
            WoundAcceptedEventSetFingerprint.Compute(malformedEvents));

        var authority = WoundSourceAuthority.Build(new WoundSourceAuthorityInput(
            malformedBinding,
            new[] { inputs.Source },
            inputs.Targets,
            inputs.EffectTargets));

        Assert.Contains(authority.Issues, issue =>
            issue.Code == "wound_source_binding_invalid");
    }

    private static SourceFixture BuildFixture(
        string realm,
        params WoundTargetExport[] targets)
    {
        var inputs = CreateFixtureInputs(realm, targets);
        var authority = WoundSourceAuthority.Build(new WoundSourceAuthorityInput(
            inputs.Binding,
            new[] { inputs.Source },
            inputs.Targets,
            inputs.EffectTargets));
        Assert.Empty(authority.Issues);
        return new SourceFixture(
            authority,
            inputs.Source.EventRef,
            inputs.Source.SourceKind,
            inputs.Source.SourceId);
    }

    private static FixtureInputs CreateFixtureInputs(
        string realm,
        params WoundTargetExport[] targets)
    {
        const string eventRef = "turn_42:wound_capable_event";
        const string sourceKind = "combat_action";
        const string sourceId = "combat_action_test_001";
        const string eventFingerprint =
            "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        var acceptedEvent = new WoundAcceptedEventAuthority(
            eventRef,
            "combat_resolution",
            "combat_result_test_001",
            eventFingerprint);
        var acceptedEvents = new[] { acceptedEvent };
        var binding = new WoundAcceptedTurnBinding(
            "session_test_001",
            "request_test_001",
            "snapshot_test_001",
            realm,
            42,
            acceptedEvents,
            WoundAcceptedEventSetFingerprint.Compute(acceptedEvents));
        var source = new WoundSourceEventExport(
            eventRef,
            eventFingerprint,
            sourceKind,
            sourceId,
            "active",
            realm,
            DomainFor(realm));
        var effectTargets = EffectTargetAuthority.Build(new EffectTargetAuthorityInput(
            targets.Select(target => new EffectTargetExport(
                target.EffectTarget.Realm,
                target.EffectTarget.Kind,
                target.EffectTarget.TargetId,
                target.SameTurn,
                target.TargetRef)).ToArray(),
            Array.Empty<EffectTargetExport>(),
            new HashSet<string>(StringComparer.Ordinal),
            null));
        Assert.Empty(effectTargets.Issues);
        return new FixtureInputs(binding, source, targets, effectTargets);
    }

    private static string DomainFor(string realm) =>
        realm == "mortal_world" ? "physical" : "spiritual";

    private sealed record SourceFixture(
        WoundSourceAuthority Authority,
        string EventRef,
        string SourceKind,
        string SourceId);

    private sealed record FixtureInputs(
        WoundAcceptedTurnBinding Binding,
        WoundSourceEventExport Source,
        IReadOnlyList<WoundTargetExport> Targets,
        EffectTargetAuthority EffectTargets);
}
