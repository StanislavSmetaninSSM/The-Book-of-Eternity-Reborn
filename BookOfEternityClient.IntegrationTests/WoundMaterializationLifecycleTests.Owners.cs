using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class WoundMaterializationLifecycleTests
{
    public static IEnumerable<object[]> CanonicalWoundOwnerCases()
    {
        yield return OwnerCase(
            "mortal_world",
            "player",
            "player_current",
            WoundCarrierCatalog.PlayerPath);
        yield return OwnerCase(
            "mortal_world",
            "npc",
            "npc_owner_matrix",
            WoundCarrierCatalog.NpcPath);
        yield return OwnerCase(
            "mortal_world",
            "combatant",
            "combatant_owner_matrix",
            WoundCarrierCatalog.EnemiesPath);
        yield return OwnerCase(
            "mortal_world",
            "combatant_member",
            "member_owner_matrix",
            WoundCarrierCatalog.AlliesPath);
        yield return OwnerCase(
            "chaos_sea",
            "player_soul",
            "player_soul",
            WoundCarrierCatalog.AfterlifeProfilesPath,
            "player_soul");
        yield return OwnerCase(
            "chaos_sea",
            "guardian",
            "guardian_owner_matrix",
            WoundCarrierCatalog.AfterlifeProfilesPath,
            "guardian");
        yield return OwnerCase(
            "shining_abode",
            "resident",
            "resident_owner_matrix",
            WoundCarrierCatalog.AfterlifeProfilesPath,
            "resident");
        yield return OwnerCase(
            "shining_abode",
            "radiant_actor",
            "radiant_owner_matrix",
            WoundCarrierCatalog.AfterlifeProfilesPath,
            "radiant_actor");
        yield return OwnerCase(
            "chaos_sea",
            "afterlife_actor",
            "afterlife_owner_matrix",
            WoundCarrierCatalog.AfterlifeProfilesPath,
            "custom_afterlife_actor");
    }

    [Theory]
    [MemberData(nameof(CanonicalWoundOwnerCases))]
    public void ActiveWound_AllRegisteredOwnersHaveExactCarrierIndexHistoryAgreement(
        string realm,
        string ownerKind,
        string ownerId,
        string carrierPath,
        string? actorType)
    {
        var state = CreateOwnerState(
            realm,
            ownerKind,
            ownerId,
            carrierPath,
            actorType,
            "owner_matrix");

        AssertCanonicalAgreement(state);
    }

    [Fact]
    public void SameTurnCombatantPersistence_AllocatesPermanentEmptyCarrierBeforeWoundCreation()
    {
        const string combatantRef = "combatant_ref_owner_transition";
        const string combatantId = "combatant_persistent_owner_transition";
        var rawCombatants = new JsonArray(new JsonObject
        {
            ["combatantRef"] = combatantRef,
            ["displayName"] = "Безымянный противник",
            ["activeBuffs"] = new JsonArray(),
            ["activeDebuffs"] = new JsonArray(),
            ["activeWounds"] = new JsonArray()
        });
        var allocation = CombatantIdentityState.BuildNew(
            rawCombatants,
            new FixedCombatantIdentityFactory(combatantId));
        Assert.Empty(allocation.Issues);
        Assert.NotNull(allocation.State);
        Assert.True(allocation.State!.TryResolveCombatant(combatantRef, out var allocatedId));
        Assert.Equal(combatantId, allocatedId);

        var forgedWound = CreateCanonicalWound(
            new WoundOwnerCoordinate(
                "mortal_world",
                "combatant",
                combatantId,
                WoundCarrierCatalog.EnemiesPath),
            "gm_forged_combatant_wound");
        allocation.RewrittenCombatants[0]!["activeWounds"] = new JsonArray(
            ToWoundNode(forgedWound));

        var preTurn = new WoundCarrierCatalogInput(
            null,
            null,
            new JsonObject
            {
                ["enemiesData"] = rawCombatants.DeepClone()
            },
            null,
            null);
        var accepted = new EffectCarrierCatalogInput(
            null,
            null,
            new JsonObject
            {
                ["enemiesData"] = allocation.RewrittenCombatants.DeepClone()
            },
            null,
            null,
            null);

        var composition = WoundAcceptedOwnerCarrierAuthority.Compose(
            preTurn,
            accepted);

        Assert.True(composition.Success, Describe(composition.Issues));
        var composedCarriers = Assert.IsType<WoundCarrierCatalogInput>(
            composition.Carriers);
        var owner = new WoundOwnerCoordinate(
            "mortal_world",
            "combatant",
            combatantId,
            WoundCarrierCatalog.EnemiesPath);
        var acceptedCollection = WoundCarrierCollectionAuthority.Resolve(
            composedCarriers,
            owner);
        Assert.Empty(acceptedCollection);
        var acceptedCombatant = composedCarriers.EnemyCombatants![
            "enemiesData"]![0]!.AsObject();
        Assert.Equal(combatantId, acceptedCombatant["combatantId"]!.GetValue<string>());
        Assert.False(acceptedCombatant.ContainsKey("combatantRef"));
        Assert.Equal(
            "Безымянный противник",
            acceptedCombatant["displayName"]!.GetValue<string>());

        var wound = CreateCanonicalWound(owner, "accepted_combatant_wound");
        acceptedCollection.Add(ToWoundNode(wound));
        AssertCanonicalAgreement(CreateOwnerState(
            wound,
            composedCarriers,
            "accepted_combatant_wound"));
    }

    [Fact]
    public void MortalToAfterlife_PhysicalWoundRemainsMortalAndIsNotCopiedToSoulProfile()
    {
        var mortal = CreateOwnerState(
            "mortal_world",
            "player",
            "player_current",
            WoundCarrierCatalog.PlayerPath,
            actorType: null,
            "mortal_death_boundary");
        var soulRoot = CreateAfterlifeRoot(
            "player_soul",
            "player_soul",
            "Shining Abode");
        var preTurn = mortal.Carriers with
        {
            AfterlifeProfiles = soulRoot
        };
        var acceptedSoulRoot = AfterlifeEntityProfileState.ProjectPlayerSoulRealm(
            soulRoot,
            "chaos_sea");
        acceptedSoulRoot["profiles"]![0]!["activeWounds"] = new JsonArray(
            ToWoundNode(mortal.Wound));

        var composition = WoundAcceptedOwnerCarrierAuthority.Compose(
            preTurn,
            new EffectCarrierCatalogInput(
                null,
                null,
                null,
                null,
                acceptedSoulRoot,
                null));

        Assert.True(composition.Success, Describe(composition.Issues));
        var playerWounds = composition.Carriers!.PlayerWounds![
            "activeWounds"]!.AsArray();
        Assert.Single(playerWounds);
        Assert.Equal(mortal.Wound.WoundId, playerWounds[0]!["woundId"]!.GetValue<string>());
        var soulProfile = composition.Carriers.AfterlifeProfiles![
            "profiles"]![0]!.AsObject();
        Assert.Equal("Chaos Sea", soulProfile["realm"]!.GetValue<string>());
        Assert.Empty(soulProfile["activeWounds"]!.AsArray());
        AssertCanonicalAgreement(mortal with
        {
            Carriers = composition.Carriers
        });
    }

    [Fact]
    public void AcceptedAfterlifeProfileUpdate_PreservesSpiritualWoundRealmAndAgreement()
    {
        var existing = CreateOwnerState(
            "chaos_sea",
            "guardian",
            "guardian_profile_update",
            WoundCarrierCatalog.AfterlifeProfilesPath,
            "guardian",
            "spiritual_profile_update");
        var acceptedRoot = existing.Carriers.AfterlifeProfiles!
            .DeepClone()
            .AsObject();
        var acceptedProfile = acceptedRoot["profiles"]![0]!.AsObject();
        acceptedProfile["services"]!["revision"] = 7;
        acceptedProfile["activeWounds"] = new JsonArray();

        var composition = WoundAcceptedOwnerCarrierAuthority.Compose(
            existing.Carriers,
            new EffectCarrierCatalogInput(
                null,
                null,
                null,
                null,
                acceptedRoot,
                null));

        Assert.True(composition.Success, Describe(composition.Issues));
        var profile = composition.Carriers!.AfterlifeProfiles![
            "profiles"]![0]!.AsObject();
        Assert.Equal("Chaos Sea", profile["realm"]!.GetValue<string>());
        Assert.Equal(7, profile["services"]!["revision"]!.GetValue<int>());
        var preserved = Assert.Single(profile["activeWounds"]!.AsArray());
        Assert.Equal(existing.Wound.WoundId, preserved!["woundId"]!.GetValue<string>());
        AssertCanonicalAgreement(existing with
        {
            Carriers = composition.Carriers
        });
    }

    private static object[] OwnerCase(
        string realm,
        string ownerKind,
        string ownerId,
        string carrierPath,
        string? actorType = null) =>
        new object[] { realm, ownerKind, ownerId, carrierPath, actorType! };

    private static CanonicalWoundOwnerState CreateOwnerState(
        string realm,
        string ownerKind,
        string ownerId,
        string carrierPath,
        string? actorType,
        string scenario)
    {
        var owner = new WoundOwnerCoordinate(
            realm,
            ownerKind,
            ownerId,
            carrierPath);
        var wound = CreateCanonicalWound(owner, scenario);
        return CreateOwnerState(
            wound,
            CreateCarrier(owner, actorType, wound),
            scenario);
    }

    private static CanonicalWoundOwnerState CreateOwnerState(
        WoundMaterializationEnvelope wound,
        WoundCarrierCatalogInput carriers,
        string scenario)
    {
        var fingerprint = WoundIdentityState.ComputeSemanticFingerprint(wound);
        var transitionId = wound.LastTransition.TransitionId;
        var eventRef = wound.Origin.EventRef;
        var identity = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["entries"] = new JsonArray(new JsonObject
            {
                ["woundId"] = wound.WoundId,
                ["realm"] = wound.Owner.Realm,
                ["ownerKind"] = wound.Owner.OwnerKind,
                ["ownerId"] = wound.Owner.OwnerId,
                ["carrierPath"] = wound.Owner.CarrierPath,
                ["domain"] = wound.Classification.Domain,
                ["status"] = "active",
                ["createdAtTurn"] = wound.Origin.CreatedAtTurn,
                ["createdEventRef"] = eventRef,
                ["lastTransitionOrdinal"] = 1,
                ["terminalTransitionId"] = null,
                ["semanticFingerprint"] = fingerprint
            })
        };
        var history = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["nextOrdinal"] = 2,
            ["transitions"] = new JsonArray(new JsonObject
            {
                ["transitionId"] = transitionId,
                ["woundId"] = wound.WoundId,
                ["ordinal"] = 1,
                ["woundTransitionOrdinal"] = 1,
                ["kind"] = "create",
                ["turn"] = wound.Origin.CreatedAtTurn,
                ["eventRef"] = eventRef,
                ["operationKey"] = $"operation_{scenario}",
                ["beforeFingerprint"] = WoundHistoryState
                    .ComputeNonexistentBeforeFingerprint(wound.WoundId),
                ["afterFingerprint"] = fingerprint,
                ["sourceFingerprint"] = Fingerprint($"source_{scenario}"),
                ["attemptId"] = null,
                ["courseId"] = null,
                ["courseMilestoneOrdinal"] = null,
                ["cycleKey"] = null,
                ["paymentFingerprint"] = null,
                ["outputFingerprint"] = Fingerprint($"output_{scenario}"),
                ["readableSummary"] = "Рана получена и привязана к точному владельцу.",
                ["terminal"] = false
            })
        };
        return new CanonicalWoundOwnerState(wound, carriers, identity, history);
    }

    private static WoundMaterializationEnvelope CreateCanonicalWound(
        WoundOwnerCoordinate owner,
        string scenario)
    {
        var woundId = $"wound_{scenario}";
        var eventRef = $"event_{scenario}";
        var transitionId = $"wound_transition_{scenario}";
        var spiritual = !string.Equals(
            owner.Realm,
            "mortal_world",
            StringComparison.Ordinal);
        var consequences = spiritual
            ? CreateSpiritualConsequences(woundId, owner)
            : new WoundConsequences(
                1,
                0,
                Array.Empty<WoundConsequenceEntry>());
        var candidate = new WoundMaterializationEnvelope(
            WoundMaterializationContract.SchemaVersion,
            woundId,
            "active",
            owner,
            new WoundOrigin(
                eventRef,
                "accepted_event",
                $"source_{scenario}",
                "active",
                42,
                null,
                $"opportunity_{scenario}",
                null,
                spiritual
                    ? "Духовное давление оставило устойчивый след."
                    : "Подтверждённое событие оставило телесную рану."),
            new WoundClassification(
                spiritual ? "spiritual" : "physical",
                spiritual ? "Трещина духовной целостности" : "Рваная травма",
                spiritual
                    ? new WoundLocationProfile(
                        "spiritual_axis",
                        "воля и духовное равновесие",
                        "spiritual_axis",
                        "will_and_balance",
                        "self")
                    : new WoundLocationProfile(
                        "anatomical",
                        "левое предплечье",
                        "body_part",
                        "left_forearm",
                        "left")),
            new WoundDisplay(
                spiritual ? "Трещина воли" : "Рваная рана предплечья",
                spiritual
                    ? "Духовное равновесие нарушено."
                    : "Края раны расходятся при движении.",
                spiritual
                    ? new[] { "тяжесть духовных действий" }
                    : new[] { "боль", "кровотечение" },
                spiritual
                    ? "Поддаётся духовному исцелению."
                    : "Требует обработки и стабилизации.",
                "known_to_player",
                spiritual
                    ? "Чужое давление оставило трещину в духовной целостности."
                    : "Острый край рассёк левое предплечье."),
            new WoundSeverity("I", 1, "I", eventRef),
            new WoundCare("untreated", null, null, null),
            Array.Empty<WoundComplication>(),
            consequences,
            new WoundTreatment(
                Array.Empty<WoundDiagnosisPath>(),
                Array.Empty<WoundTreatmentRoute>(),
                Array.Empty<string>(),
                Array.Empty<string>()),
            new WoundRecovery(
                spiritual ? "progressive" : "requires_stabilization",
                spiritual ? "afterlife_safe_cycle" : "mortal_world_time",
                spiritual ? 1 : 86_400,
                0,
                spiritual ? 4 : 3,
                null,
                spiritual ? Array.Empty<string>() : new[] { "not_stabilized" },
                true,
                null),
            new WoundRelations(null, Array.Empty<string>(), Array.Empty<string>()),
            new WoundLastTransition(transitionId, 1, 42, "create"));
        var parsed = WoundMaterializationContract.Parse(
            WoundMaterializationContract.SerializeCanonical(candidate),
            $"owners.{scenario}");
        Assert.True(parsed.IsValid, Describe(parsed.Issues));
        return Assert.IsType<WoundMaterializationEnvelope>(parsed.Wound);
    }

    private static WoundConsequences CreateSpiritualConsequences(
        string woundId,
        WoundOwnerCoordinate owner)
    {
        const string profile = "spiritual_roll_hindrance";
        var definitionKey = $"definition_{woundId}";
        var effectId = $"effect_{woundId}";
        var definition = EffectMaterializationTestFixture.CreateSpiritualWoundDefinition(
            profile,
            ResolveEffectTargetKind(owner.OwnerKind),
            owner.Realm,
            woundId,
            definitionKey);
        definition["allowedRealms"] = new JsonArray("chaos_sea", "shining_abode");
        var result = new WoundConsequences(
            1,
            1,
            new[]
            {
                new WoundConsequenceEntry(
                    1,
                    profile,
                    effectId,
                    "Духовные проверки проходят с помехой.")
            })
        {
            OwnedEffectSources = new WoundOwnedEffectSources(
                new[] { ToElement(definition) },
                new[] { new WoundRootEffectBinding(effectId, definitionKey) })
        };
        return result;
    }

    private static string ResolveEffectTargetKind(string ownerKind) => ownerKind switch
    {
        "player_soul" => "player",
        "guardian" => "guardian",
        "resident" => "resident",
        "radiant_actor" => "radiant_actor",
        "afterlife_actor" => "afterlife_actor",
        _ => throw new ArgumentOutOfRangeException(nameof(ownerKind), ownerKind, null)
    };

    private static WoundCarrierCatalogInput CreateCarrier(
        WoundOwnerCoordinate owner,
        string? actorType,
        WoundMaterializationEnvelope wound)
    {
        var woundNode = ToWoundNode(wound);
        return owner.OwnerKind switch
        {
            "player" => new WoundCarrierCatalogInput(
                new JsonObject
                {
                    ["schemaVersion"] = 1,
                    ["owner"] = new JsonObject
                    {
                        ["realm"] = owner.Realm,
                        ["ownerKind"] = owner.OwnerKind,
                        ["ownerId"] = owner.OwnerId
                    },
                    ["activeWounds"] = new JsonArray(woundNode)
                },
                null,
                null,
                null,
                null),
            "npc" => new WoundCarrierCatalogInput(
                null,
                new JsonObject
                {
                    ["schemaVersion"] = 1,
                    ["entries"] = new JsonArray(new JsonObject
                    {
                        ["npcId"] = owner.OwnerId,
                        ["activeWounds"] = new JsonArray(woundNode)
                    })
                },
                null,
                null,
                null),
            "combatant" => CombatantCarrier(owner, woundNode, member: false),
            "combatant_member" => CombatantCarrier(owner, woundNode, member: true),
            _ => new WoundCarrierCatalogInput(
                null,
                null,
                null,
                null,
                CreateAfterlifeRoot(
                    actorType ?? throw new ArgumentNullException(nameof(actorType)),
                    owner.OwnerId,
                    DisplayRealm(owner.Realm),
                    woundNode))
        };
    }

    private static WoundCarrierCatalogInput CombatantCarrier(
        WoundOwnerCoordinate owner,
        JsonObject wound,
        bool member)
    {
        JsonObject combatant;
        if (member)
        {
            combatant = new JsonObject
            {
                ["combatantId"] = $"group_{owner.OwnerId}",
                ["isGroup"] = true,
                ["members"] = new JsonArray(new JsonObject
                {
                    ["memberId"] = owner.OwnerId,
                    ["activeWounds"] = new JsonArray(wound)
                })
            };
        }
        else
        {
            combatant = new JsonObject
            {
                ["combatantId"] = owner.OwnerId,
                ["activeWounds"] = new JsonArray(wound)
            };
        }

        var root = new JsonObject
        {
            [owner.CarrierPath == WoundCarrierCatalog.EnemiesPath
                ? "enemiesData"
                : "alliesData"] = new JsonArray(combatant)
        };
        return owner.CarrierPath == WoundCarrierCatalog.EnemiesPath
            ? new WoundCarrierCatalogInput(null, null, root, null, null)
            : new WoundCarrierCatalogInput(null, null, null, root, null);
    }

    private static JsonObject CreateAfterlifeRoot(
        string actorType,
        string actorId,
        string realm,
        params JsonObject[] wounds) => new()
    {
        ["schemaVersion"] = 1,
        ["profiles"] = new JsonArray(new JsonObject
        {
            ["actorType"] = actorType,
            ["actorId"] = actorId,
            ["realm"] = realm,
            ["displayName"] = $"Профиль {actorId}",
            ["activeEffects"] = new JsonArray(),
            ["activeWounds"] = new JsonArray(wounds
                .Select(static wound => (JsonNode)wound.DeepClone())
                .ToArray()),
            ["services"] = new JsonObject
            {
                ["revision"] = 1
            }
        })
    };

    private static JsonObject ToWoundNode(WoundMaterializationEnvelope wound) =>
        JsonNode.Parse(WoundMaterializationContract.SerializeCanonical(wound))!
            .AsObject();

    private static string DisplayRealm(string realm) => realm switch
    {
        "chaos_sea" => "Chaos Sea",
        "shining_abode" => "Shining Abode",
        _ => throw new ArgumentOutOfRangeException(nameof(realm), realm, null)
    };

    private static void AssertCanonicalAgreement(CanonicalWoundOwnerState state)
    {
        var catalog = WoundCarrierCatalog.Build(state.Carriers);
        Assert.Empty(catalog.Issues);
        var identity = WoundIdentityState.Parse(
            state.IdentityIndex.ToJsonString(),
            WoundIdentityState.StatePath);
        var history = WoundHistoryState.Parse(
            state.History.ToJsonString(),
            WoundHistoryState.HistoryPath);
        Assert.True(identity.IsValid, Describe(identity.Issues));
        Assert.True(history.IsValid, Describe(history.Issues));
        Assert.Empty(history.State!.ValidateAgreement(identity.State!, catalog));
    }

    private sealed record CanonicalWoundOwnerState(
        WoundMaterializationEnvelope Wound,
        WoundCarrierCatalogInput Carriers,
        JsonObject IdentityIndex,
        JsonObject History);

    private sealed class FixedCombatantIdentityFactory(string combatantId) :
        CombatantIdentityFactory
    {
        internal override string CreateCombatantId() => combatantId;
    }
}
