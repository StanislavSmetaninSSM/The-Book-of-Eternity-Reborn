using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Deterministic, independently allocated scenario seeds for the future wound v1
/// integration tests. These builders deliberately propose no canonical wound state.
/// </summary>
internal static class WoundMaterializationTestFixtures
{
    internal const int Turn = 42;
    internal const int Incarnation = 1;

    internal static MortalWoundScenarioFixture CreatePostApocalypticMortalScenario()
    {
        var refs = new MortalWoundScenarioRefs(
            WoundRef: "proposal_pa_contaminated_laceration",
            OwnerId: "player_current",
            EventRef: "event_turn_42_pa_collapse_laceration",
            RouteId: "route_pa_cleanse_and_suture",
            HiddenRouteId: "route_pa_antibiotic_course",
            DiagnosisPathId: "diagnosis_pa_contamination_assessment",
            BandageItemId: "itm_pa_bandage_roll",
            AntisepticItemId: "itm_pa_antiseptic_ampoule",
            SterileThreadItemId: "itm_pa_sterile_thread",
            AntibioticItemId: "itm_pa_antibiotic_course",
            CapabilityRef: "cap_pa_field_medicine",
            ProviderRef: "npc_pa_field_medic",
            FacilityRef: "facility_pa_clean_work_surface",
            LocationRef: "loc_pa_collapse_aid_station");

        var items = CreateMortalItems(
            (refs.BandageItemId, "Стерильный бинт аварийного набора"),
            (refs.AntisepticItemId, "Антисептик из аварийного набора"),
            (refs.SterileThreadItemId, "Стерильная хирургическая нить"),
            (refs.AntibioticItemId, "Курс антибиотика широкого действия"));
        var location = MortalLocationTestFixture.CreateCanonicalLocationWithIdentity(
            refs.LocationRef,
            "Медпункт у обрушенного перехода",
            discoveryTier: "visited");
        var proposal = CreateMortalProposal(
            refs,
            woundType: "contaminated_laceration",
            woundName: "Загрязнённая рваная рана предплечья",
            readableLocus: "наружная сторона левого предплечья",
            cause: "Острый металлический край вскрыл предплечье во время заражённого обвала.",
            visibleSymptoms: new JsonArray("кровотечение", "пульсирующая боль"),
            hiddenRoute: CreateAntibioticCourse(refs),
            visibleRoute: CreateCleanseAndSutureRoute(refs),
            diagnosisPath: CreateContaminationDiagnosisPath(refs));

        return new MortalWoundScenarioFixture(
            proposal,
            CreateMortalAuthorityRoots(items, location, refs, "Полевой медик", "field_medicine", "clean_work_surface"),
            refs,
            new WoundExpectedFacts("mortal_world", "physical", "II", refs.OwnerId, refs.EventRef,
                "requires_stabilization", new[]
                {
                    $"inventory.items[itemId={refs.AntisepticItemId}].count",
                    $"inventory.items[itemId={refs.SterileThreadItemId}].count",
                    $"inventory.items[itemId={refs.AntibioticItemId}].count"
                }),
            CreateCanonicalWritePaths());
    }

    internal static MortalWoundScenarioFixture CreateMagicalWorldMortalScenario()
    {
        var refs = new MortalWoundScenarioRefs(
            WoundRef: "proposal_mw_crystalline_burn",
            OwnerId: "player_current",
            EventRef: "event_turn_42_mw_resonance_burn",
            RouteId: "route_mw_focus_stabilization",
            HiddenRouteId: "route_mw_crystal_dust_ritual",
            DiagnosisPathId: "diagnosis_mw_resonance_trace",
            BandageItemId: "itm_mw_resonant_wrapping",
            AntisepticItemId: "itm_mw_crystal_dust",
            SterileThreadItemId: "itm_mw_tuned_healing_focus",
            AntibioticItemId: "itm_mw_silver_moss_elixir",
            CapabilityRef: "cap_mw_resonance_mending",
            ProviderRef: "npc_mw_crystal_physician",
            FacilityRef: "facility_mw_resonance_chamber",
            LocationRef: "loc_mw_singing_glass_observatory");

        var items = CreateMortalItems(
            (refs.BandageItemId, "Резонансная повязка"),
            (refs.AntisepticItemId, "Пыль резонансного кристалла"),
            (refs.SterileThreadItemId, "Настроенный лечебный фокус"),
            (refs.AntibioticItemId, "Эликсир серебряного мха"));
        var location = MortalLocationTestFixture.CreateCanonicalLocationWithIdentity(
            refs.LocationRef,
            "Обсерватория Поющего Стекла",
            discoveryTier: "discovered");
        var proposal = CreateMortalProposal(
            refs,
            woundType: "crystalline_resonance_burn",
            woundName: "Кристаллический ожог проводящих каналов",
            readableLocus: "каналы левой руки и грудной резонатор",
            cause: "Трещина в поющем кристалле обожгла тело и нарушила внутренний резонанс.",
            visibleSymptoms: new JsonArray("светящиеся трещины кожи", "дрожь при магическом усилии"),
            hiddenRoute: CreateCrystalDustRitualRoute(refs),
            visibleRoute: CreateFocusStabilizationRoute(refs),
            diagnosisPath: CreateResonanceDiagnosisPath(refs));

        return new MortalWoundScenarioFixture(
            proposal,
            CreateMortalAuthorityRoots(items, location, refs, "Настройщик лечебных фокусов", "resonance_mending", "resonance_chamber"),
            refs,
            new WoundExpectedFacts("mortal_world", "physical", "II", refs.OwnerId, refs.EventRef,
                "requires_stabilization", new[]
                {
                    $"inventory.items[itemId={refs.AntisepticItemId}].count",
                    $"inventory.items[itemId={refs.SterileThreadItemId}].count",
                    $"inventory.items[itemId={refs.AntibioticItemId}].count"
                }),
            CreateCanonicalWritePaths());
    }

    internal static SpiritualConflictScenarioFixture CreateSpiritualConflictScenario(
        string dangerMode = "hostile",
        int resilienceTier = 2,
        int healingTier = 3,
        bool hasPriorTrainingEscalation = false)
    {
        if (dangerMode is not ("training" or "controlled" or "hostile" or "annihilation"))
            throw new ArgumentOutOfRangeException(nameof(dangerMode));
        ValidateTier(resilienceTier, nameof(resilienceTier));
        ValidateTier(healingTier, nameof(healingTier));

        const string playerId = "player_soul";
        const string opponentId = "guardian_wound_conflict_opponent";
        const string conflictId = "conflict_wound_hostile_boundary";
        const string eventRef = "event_turn_42_spiritual_strain_boundary";
        const string sealedD20Ref = "sealed_d20_turn_42_spiritual_strain_boundary";
        var playerProfile = CreateSpiritualProfile("player_soul", playerId, "chaos_sea", resilienceTier, healingTier);
        var opponentProfile = CreateSpiritualProfile("guardian", opponentId, "chaos_sea", 1, 2);
        var conflict = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["activeConflict"] = new JsonObject
            {
                ["conflictId"] = conflictId,
                ["realm"] = "chaos_sea",
                ["sideModel"] = "direct_duel",
                ["playerSide"] = CreateConflictSide("player", playerId),
                ["oppositionSide"] = CreateConflictSide("guardian", opponentId),
                ["playerSideStrain"] = "strained",
                ["oppositionSideStrain"] = "clear",
                ["conflictPosition"] = "contested",
                ["resolutionState"] = "active",
                ["exchangeLog"] = new JsonArray(),
                ["dangerEnvelope"] = new JsonObject
                {
                    ["dangerMode"] = dangerMode,
                    ["escalatedFromTraining"] = hasPriorTrainingEscalation,
                    ["annihilationAuthority"] = dangerMode == "annihilation"
                        ? new JsonObject { ["authorityId"] = "authority_annihilation_wound_conflict" }
                        : null,
                    ["sideWoundState"] = new JsonObject
                    {
                        ["player"] = new JsonObject { ["newWoundId"] = null, ["opportunityCount"] = 0 },
                        ["opposition"] = new JsonObject { ["newWoundId"] = null, ["opportunityCount"] = 0 }
                    }
                },
                ["acceptedStrainEvidence"] = new JsonObject
                {
                    ["eventRef"] = eventRef,
                    ["sealedD20Ref"] = sealedD20Ref,
                    ["targetSide"] = "player",
                    ["before"] = "strained",
                    ["destination"] = "fractured",
                    ["harmfulMargin"] = -8,
                    ["appliedArtTier"] = 4,
                    ["targetResilienceTier"] = resilienceTier,
                    ["extraJumpSteps"] = 0,
                    ["expectedComputedCeiling"] = "II"
                }
            },
            ["recentConflicts"] = new JsonArray()
        };

        return new SpiritualConflictScenarioFixture(
            new JsonObject
            {
                [AfterlifeEntityProfileState.ProfilesProperty] = new JsonArray(playerProfile.DeepClone(), opponentProfile.DeepClone())
            },
            conflict,
            new SpiritualConflictScenarioRefs(playerId, opponentId, conflictId, eventRef,
                sealedD20Ref, "player", "opposition"),
            new SpiritualConflictExpectedFacts(dangerMode, "strained", "fractured", "II", -8,
                resilienceTier, 4, 0, hasPriorTrainingEscalation),
            CreateCanonicalWritePaths());
    }

    internal static ElyaraScenarioFixture CreateElyaraScenario()
    {
        var manifestPath = Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient", "system_guardians", "built_in", "elyara", "manifest.json");
        var dossierPath = Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient", "system_guardians", "built_in", "elyara", "dossier.md");
        var manifest = JsonNode.Parse(File.ReadAllText(manifestPath))?.AsObject()
            ?? throw new InvalidOperationException("Built-in Elyara manifest is missing or invalid.");
        var dossier = File.ReadAllText(dossierPath);
        var profile = CreateSpiritualProfile("guardian", "elyara", "chaos_sea", resilienceTier: 5, healingTier: 5);
        profile["displayName"] = manifest["displayName"]!.DeepClone();
        profile["locationId"] = "location_elyara_lazaret";
        profile["locationName"] = "Лазарет Незаживающего Света";
        profile["healingServiceProfile"] = CreateHealingService("elyara", "chaos_sea", "location_elyara_lazaret", "public", 100);
        var guardianLibrary = new JsonObject
        {
            ["libraryKind"] = "built_in",
            ["entries"] = new JsonArray(new JsonObject
            {
                ["presetId"] = "elyara",
                ["manifest"] = manifest.DeepClone(),
                ["dossierMarkdown"] = dossier
            })
        };
        var profiles = new JsonObject
        {
            [AfterlifeEntityProfileState.ProfilesProperty] = new JsonArray(profile.DeepClone())
        };
        return new ElyaraScenarioFixture(
            guardianLibrary, profiles, profile,
            new ElyaraScenarioRefs("elyara", "location_elyara_lazaret", "service_elyara_healing", "availability_elyara_first_chaos_sea_entry"),
            new ElyaraExpectedProtectedFields(DiscoverableFromFirstChaosSeaEntry: true, new[]
            {
                "/standardArts/spiritual_healing/tier", "/locationId", "/locationName",
                "/healingServiceProfile/visibility", "/healingServiceProfile/availability",
                "/healingServiceProfile/priceMultiplierPercent"
            }),
            CreateCanonicalWritePaths());
    }

    internal static ShiningFactionScenarioFixture CreateShiningFactionScenario(
        int healingTier = 3,
        string? serviceVisibility = null)
    {
        ValidateTier(healingTier, nameof(healingTier));
        if (serviceVisibility is not (null or "restricted" or "public"))
            throw new ArgumentOutOfRangeException(nameof(serviceVisibility));

        const string factionId = "faction_shining_wound_sanctuary";
        const string residentId = "resident_shining_wound_healer";
        const string locationId = "location_shining_wound_sanctuary";
        var faction = ShiningFactionTestMaterialization.Apply(new JsonObject
        {
            ["factionId"] = factionId,
            ["displayName"] = "Санктуарий Тихих Швов",
            ["projects"] = new JsonArray(),
            ["territorialInfluence"] = new JsonArray(),
            ["resourceLedger"] = new JsonArray(),
            ["tradeInventory"] = null,
            ["tradeInventoryReceipts"] = new JsonArray(),
            ["leadershipHistory"] = new JsonArray(),
            ["leadershipReceipts"] = new JsonArray(),
            ["visibleRosterResidentIds"] = new JsonArray(residentId)
        }, Turn, hasResidentAffiliations: true, canTrade: false);
        var resident = CreateResident(residentId, factionId, locationId, healingTier, serviceVisibility);
        var profile = CreateSpiritualProfile("resident", residentId, "shining_abode", 1, healingTier);
        if (serviceVisibility != null)
            profile["healingServiceProfile"] = CreateHealingService(residentId, "shining_abode", locationId, serviceVisibility, 100);

        return new ShiningFactionScenarioFixture(
            new JsonObject
            {
                ["availability"] = "active",
                ["halls"] = new JsonArray(),
                ["factions"] = new JsonArray(faction.DeepClone()),
                ["shiningPoliticalActors"] = new JsonArray(),
                ["gates"] = CreateEmptyShiningGates(),
                ["preparedIncarnationPackage"] = null,
                ["gachaSystem"] = CreateEmptyGachaSystem()
            },
            new JsonObject
            {
                ["entries"] = new JsonArray(resident.DeepClone()),
                ["thoughtJournal"] = new JsonArray(), ["interactionLog"] = new JsonArray(),
                ["historyLog"] = new JsonArray(), ["transferReceipts"] = new JsonArray(),
                ["interactionReceipts"] = new JsonArray(), ["rosterReceipts"] = new JsonArray()
            },
            new JsonObject { [AfterlifeEntityProfileState.ProfilesProperty] = new JsonArray(profile.DeepClone()) },
            new ShiningFactionScenarioRefs(factionId, residentId, residentId, locationId,
                serviceVisibility == null ? null : $"service_{residentId}_healing"),
            new ShiningFactionExpectedFacts(healingTier, serviceVisibility ?? "absent",
                serviceVisibility == "public"),
            CreateCanonicalWritePaths());
    }

    private static JsonObject CreateMortalProposal(
        MortalWoundScenarioRefs refs,
        string woundType,
        string woundName,
        string readableLocus,
        string cause,
        JsonArray visibleSymptoms,
        JsonObject hiddenRoute,
        JsonObject visibleRoute,
        JsonObject diagnosisPath) => new()
        {
            ["proposalRef"] = refs.WoundRef,
            ["eventRef"] = refs.EventRef,
            ["owner"] = new JsonObject { ["realm"] = "mortal_world", ["ownerKind"] = "player", ["ownerId"] = refs.OwnerId, ["carrierPath"] = WoundMaterializationTestContext.PlayerWoundsPath },
            ["domain"] = "physical",
            ["classification"] = new JsonObject { ["woundType"] = woundType, ["locationProfile"] = new JsonObject { ["kind"] = "anatomical", ["readableLocus"] = readableLocus, ["authorityKind"] = "body_part", ["authorityRef"] = "left_arm", ["affectedSide"] = "left" } },
            ["display"] = new JsonObject { ["name"] = woundName, ["description"] = cause, ["visibleSymptoms"] = visibleSymptoms.DeepClone(), ["prognosis"] = "Без стабилизации вероятно осложнение.", ["visibility"] = "known_to_player", ["acquisitionNarration"] = cause },
            ["severity"] = new JsonObject { ["value"] = "II", ["rank"] = 2, ["maximumAtCreation"] = "II", ["lastChangeEventRef"] = refs.EventRef },
            ["care"] = new JsonObject { ["state"] = "untreated", ["stabilizedAtTurn"] = null, ["activeCourseId"] = null, ["lastAttemptId"] = null },
            ["complications"] = new JsonArray(new JsonObject { ["complicationId"] = $"complication_{refs.WoundRef}_infection_risk", ["kind"] = "infection", ["state"] = "risk", ["displayName"] = "Риск воспаления", ["treatmentDifficultyModifier"] = 0, ["ownedEffectIds"] = new JsonArray(), ["visibility"] = "hidden" }),
            ["consequences"] = new JsonObject { ["slotBudget"] = 2, ["slotsUsed"] = 2, ["entries"] = new JsonArray(new JsonObject { ["slot"] = 1, ["profileKey"] = "periodic_damage", ["effectId"] = $"effect_{refs.WoundRef}_visible_bleeding", ["readableSummary"] = "Кровотечение не остановлено." }, new JsonObject { ["slot"] = 2, ["profileKey"] = "action_control", ["effectId"] = $"effect_{refs.WoundRef}_visible_pain", ["readableSummary"] = "Боль мешает точным действиям." }) },
            ["treatment"] = new JsonObject { ["diagnosisPaths"] = new JsonArray(diagnosisPath.DeepClone()), ["routes"] = new JsonArray(visibleRoute.DeepClone(), hiddenRoute.DeepClone()), ["knownRouteIds"] = new JsonArray(refs.RouteId), ["completedRouteIds"] = new JsonArray() },
            ["recovery"] = new JsonObject { ["mode"] = "requires_stabilization", ["clockKind"] = "mortal_world_time", ["cadence"] = 86400, ["currentStepProgress"] = 0, ["currentStepThreshold"] = 3, ["lastTickKey"] = null, ["blockers"] = new JsonArray("not_stabilized"), ["carryOverflow"] = true, ["deteriorationPolicy"] = null },
            ["relations"] = new JsonObject { ["priorWoundId"] = null, ["legacyRefs"] = new JsonArray(), ["independentEffectRefs"] = new JsonArray() }
        };

    private static JsonObject CreateCleanseAndSutureRoute(MortalWoundScenarioRefs refs) => CreateProcedureRoute(refs.RouteId, "Очистить и ушить рану", refs, new JsonArray(
        ItemRequirement(refs.AntisepticItemId), ItemRequirement(refs.SterileThreadItemId), CapabilityRequirement(refs.CapabilityRef), FacilityRequirement(refs.FacilityRef), LocationRequirement(refs.LocationRef)));

    private static JsonObject CreateFocusStabilizationRoute(MortalWoundScenarioRefs refs) => CreateProcedureRoute(refs.RouteId, "Настроить лечебный фокус", refs, new JsonArray(
        ItemRequirement(refs.SterileThreadItemId), CapabilityRequirement(refs.CapabilityRef), FacilityRequirement(refs.FacilityRef), LocationRequirement(refs.LocationRef)));

    private static JsonObject CreateProcedureRoute(string routeId, string displayName, MortalWoundScenarioRefs refs, JsonArray requirements) => new()
    {
        ["routeId"] = routeId, ["displayName"] = displayName, ["visibility"] = "known_to_player", ["mode"] = "procedure", ["requirements"] = requirements.DeepClone(),
        ["resourcePolicy"] = new JsonObject { ["reserveBeforeResolution"] = true, ["consumeOn"] = new JsonArray("success", "partial_success", "failed_attempt"), ["refundOn"] = new JsonArray("cancelled", "validation_failed", "rolled_back"), ["mutations"] = new JsonArray() },
        ["resolution"] = new JsonObject { ["formulaKey"] = "mortal_wound_procedure_v1", ["difficulty"] = 15, ["rollSource"] = "accepted_d20", ["acceptedEventRef"] = refs.EventRef },
        ["outcomes"] = new JsonArray(new JsonObject { ["minimumMargin"] = 0, ["results"] = new JsonArray("stabilize", "add_recovery:1") })
    };

    private static JsonObject CreateAntibioticCourse(MortalWoundScenarioRefs refs) => CreateCourseRoute(refs.HiddenRouteId, "Курс антибиотика", refs, refs.AntibioticItemId);
    private static JsonObject CreateCrystalDustRitualRoute(MortalWoundScenarioRefs refs) => CreateCourseRoute(refs.HiddenRouteId, "Ритуал кристаллической пыли", refs, refs.AntisepticItemId);

    private static JsonObject CreateCourseRoute(string routeId, string displayName, MortalWoundScenarioRefs refs, string consumableItemId) => new()
    {
        ["routeId"] = routeId, ["displayName"] = displayName, ["visibility"] = "hidden", ["mode"] = "course",
        ["requirements"] = new JsonArray(ItemRequirement(consumableItemId), CapabilityRequirement(refs.CapabilityRef), ProviderRequirement(refs.ProviderRef), LocationRequirement(refs.LocationRef)),
        ["resourcePolicy"] = new JsonObject { ["reserveBeforeResolution"] = true, ["consumeOn"] = new JsonArray("success", "partial_success", "failed_attempt"), ["refundOn"] = new JsonArray("cancelled", "validation_failed", "rolled_back"), ["mutations"] = new JsonArray() },
        ["milestones"] = new JsonArray(new JsonObject { ["milestoneId"] = $"{routeId}_dose_1", ["gameTimeOffset"] = 0, ["orderedStep"] = 1 }, new JsonObject { ["milestoneId"] = $"{routeId}_dose_2", ["gameTimeOffset"] = 86400, ["orderedStep"] = 2 }, new JsonObject { ["milestoneId"] = $"{routeId}_dose_3", ["gameTimeOffset"] = 172800, ["orderedStep"] = 3 }),
        ["outcomes"] = new JsonArray(new JsonObject { ["afterMilestone"] = 3, ["results"] = new JsonArray("stabilize", "add_recovery:2") })
    };

    private static JsonObject CreateContaminationDiagnosisPath(MortalWoundScenarioRefs refs) => CreateDiagnosisPath(refs, "Осмотр загрязнения", "route_pa_antibiotic_course");
    private static JsonObject CreateResonanceDiagnosisPath(MortalWoundScenarioRefs refs) => CreateDiagnosisPath(refs, "Проверка резонансного следа", "route_mw_crystal_dust_ritual");
    private static JsonObject CreateDiagnosisPath(MortalWoundScenarioRefs refs, string displayName, string revealedRouteId) => new()
    {
        ["diagnosisPathId"] = refs.DiagnosisPathId, ["displayName"] = displayName, ["visibility"] = "known_to_player",
        ["requirements"] = new JsonArray(CapabilityRequirement(refs.CapabilityRef), LocationRequirement(refs.LocationRef)),
        ["check"] = new JsonObject { ["formulaKey"] = "guaranteed_capability", ["capabilityRef"] = refs.CapabilityRef },
        ["reveals"] = new JsonArray($"route:{revealedRouteId}", "complication:infection_risk"), ["failurePolicy"] = "no_reveal"
    };

    private static JsonObject ItemRequirement(string itemId) => new() { ["kind"] = "item_quantity", ["itemId"] = itemId, ["quantity"] = 1 };
    private static JsonObject CapabilityRequirement(string capabilityRef) => new() { ["kind"] = "source_capability", ["capabilityRef"] = capabilityRef, ["minimum"] = 1 };
    private static JsonObject ProviderRequirement(string providerRef) => new() { ["kind"] = "provider", ["providerRef"] = providerRef };
    private static JsonObject FacilityRequirement(string facilityRef) => new() { ["kind"] = "facility", ["facilityRef"] = facilityRef };
    private static JsonObject LocationRequirement(string locationRef) => new() { ["kind"] = "location", ["locationRef"] = locationRef };

    private static MortalAuthorityRoots CreateMortalAuthorityRoots(
        JsonObject[] items,
        JsonObject location,
        MortalWoundScenarioRefs refs,
        string providerDisplayName,
        string capabilityKind,
        string facilityKind)
    {
        var bootstrap = ResourceBootstrapStateBuilder.BuildMortalPlayer(Incarnation, Turn, 10, 10, 10, 10, 10);
        if (!bootstrap.IsValid)
            throw new InvalidOperationException(string.Join(Environment.NewLine, bootstrap.Issues));
        return new MortalAuthorityRoots(
            JsonNode.Parse(bootstrap.Definitions!.ToCanonicalJson())!.AsObject(),
            JsonNode.Parse(bootstrap.State!.ToCanonicalJson())!.AsObject(),
            JsonNode.Parse(bootstrap.History!.ToCanonicalJson())!.AsObject(),
            new JsonObject { ["items"] = new JsonArray(items.Select(static item => item.DeepClone()).ToArray()), ["equippedItems"] = new JsonObject() },
            MortalItemTestFixture.CreateIndexForCarriers(items.Select(static item => (item, "player_inventory", "player", (string?)null)).ToArray()),
            MortalLocationTestFixture.CreateWorldMap(location),
            CreateMortalActorAuthority(refs, providerDisplayName, capabilityKind, facilityKind));
    }

    private static JsonObject[] CreateMortalItems(params (string ItemId, string Name)[] definitions) => definitions.Select(definition =>
        MortalItemTestFixture.CreateCanonicalRootAtTurn(definition.ItemId, Turn, "accepted_turn", "turn_outcome", "turn_42", definition.Name)).ToArray();

    private static JsonObject CreateMortalActorAuthority(
        MortalWoundScenarioRefs refs,
        string providerDisplayName,
        string capabilityKind,
        string facilityKind)
    {
        var actor = EffectMaterializationTestFixture.CreateSameTurnMortalActor(refs.ProviderRef);
        actor["NPCId"] = refs.ProviderRef;
        actor["name"] = providerDisplayName;
        actor["capabilityEvidence"] = new JsonArray(new JsonObject { ["capabilityRef"] = refs.CapabilityRef, ["kind"] = capabilityKind, ["tier"] = 2, ["facilityRef"] = refs.FacilityRef });
        actor["facilities"] = new JsonArray(new JsonObject { ["facilityRef"] = refs.FacilityRef, ["locationRef"] = refs.LocationRef, ["kind"] = facilityKind, ["visibility"] = "known_to_player" });
        return actor;
    }

    private static JsonObject CreateSpiritualProfile(string actorType, string actorId, string realm, int resilienceTier, int healingTier)
    {
        var profile = AfterlifeActorMaterializationTestFixture.CreateCompleteProfile(actorType, actorId, realm, Turn);
        profile["standardArts"] = new JsonObject
        {
            ["guard"] = new JsonObject { ["tier"] = 1, ["experience"] = 0 },
            ["spiritual_resilience"] = new JsonObject { ["tier"] = resilienceTier, ["experience"] = 0 },
            ["spiritual_healing"] = new JsonObject { ["tier"] = healingTier, ["experience"] = 0 }
        };
        return profile;
    }

    private static JsonObject CreateConflictSide(string actorType, string actorId) => new()
    {
        ["leadContestant"] = new JsonObject { ["actorType"] = actorType, ["actorId"] = actorId, ["displayName"] = "Сторона точного конфликта" },
        ["supporters"] = new JsonArray()
    };

    private static JsonObject CreateHealingService(string providerId, string realm, string locationRef, string visibility, int priceMultiplierPercent) => new()
    {
        ["serviceId"] = $"service_{providerId}_healing", ["providerId"] = providerId, ["realm"] = realm, ["locationRef"] = locationRef,
        ["visibility"] = visibility, ["availability"] = "available", ["minimumRelationship"] = null, ["priceMultiplierPercent"] = priceMultiplierPercent,
        ["compensationKinds"] = new JsonArray("ink_feathers", "favor", "debt", "quest", "free_aid"), ["accessConditions"] = new JsonArray()
    };

    private static JsonObject CreateResident(
        string residentId,
        string factionId,
        string locationId,
        int healingTier,
        string? serviceVisibility) => new()
    {
        ["residentId"] = residentId, ["guardianId"] = residentId, ["factionId"] = factionId, ["abodeId"] = locationId,
        ["displayName"] = "Хранительница тихих швов", ["residentKind"] = "attendant_spirit", ["originType"] = "native_spirit",
        ["roleLabel"] = "целительница поддержки", ["summary"] = "Дух Обители, поддерживающий исцеление без ложного обещания полной цены.",
        ["bondLevel"] = 34, ["bondTier"] = "familiar", ["canGrantCompanionRelic"] = false, ["bondRewardState"] = "none", ["historyRevealed"] = false, ["isPresent"] = true,
        ["personalityProfile"] = new JsonObject { ["archetype"] = "внимательная целительница", ["speechPattern"] = "тихая и точная", ["coreValues"] = new JsonArray("милость", "память", "ответственность") },
        ["abodeDisposition"] = "cautious", ["abodeDevotionLevel"] = 28, ["abodeDevotionTier"] = "uncertain", ["restlessness"] = 12, ["migrationState"] = "restless",
        ["mortalWorldImprint"] = new JsonObject { ["originWorldSummary"] = "Она помнит только свет, в котором боль становилась выносимой.", ["futureCompanionPrompt"] = "Покажи её как целительницу, которая называет цену исцеления прямо.", ["bondReason"] = "Она сохраняет память о каждом согласившемся на помощь.", ["coreTraits"] = new JsonArray("внимательная", "стойкая"), ["archetypeHints"] = new JsonArray("healer", "witness"), ["appearanceMotifs"] = new JsonArray("серебряные нити", "тихий свет") },
        ["primaryRole"] = new JsonObject { ["key"] = "healing_support", ["visibility"] = "known_to_player" },
        ["spiritual_healing"] = new JsonObject { ["tier"] = healingTier, ["experience"] = 0 },
        ["availableInteractions"] = serviceVisibility == "public"
            ? new JsonArray("talk", "healing_service")
            : new JsonArray("talk")
    };

    private static JsonObject CreateEmptyShiningGates() => new()
    {
        ["draftVersion"] = 0, ["hasOpenDraft"] = false, ["isStale"] = false, ["nextCandidateCursor"] = 0, ["rerollsRemaining"] = 0,
        ["allCandidateBlessingCards"] = new JsonArray(), ["availableBlessingCards"] = new JsonArray(), ["shownBlessingCardIds"] = new JsonArray(), ["selectedBlessingCardIds"] = new JsonArray()
    };

    private static JsonObject CreateEmptyGachaSystem() => new()
    {
        ["chargesPerReturn"] = 0, ["chargesUsedThisReturn"] = 0, ["currentReturnCycleId"] = "return_1", ["gachaHistory"] = new JsonArray()
    };

    private static IReadOnlyList<string> CreateCanonicalWritePaths() => Array.AsReadOnly(
        WoundMaterializationTestContext.CanonicalWoundPaths
            .Concat(new[]
            {
                ResourceMaterializationContract.DefinitionsPath,
                ResourceMaterializationContract.StatePath,
                ResourceMaterializationContract.HistoryPath,
                CanonicalResourceOwnerAuthorityComposer.AuthorityPath,
                InventoryEquipmentService.ItemsPath,
                MortalItemIdentityState.StatePath,
                MortalLocationMaterializationContract.WorldMapPath,
                MortalLocationMaterializationContract.CurrentLocationPath,
                MortalLocationIdentityState.StatePath,
                "game_state/npcs/npcs.json",
                "game_state/meta/guardians.json",
                "game_state/meta/shining_abode_state.json"
            })
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static path => path, StringComparer.Ordinal)
            .ToArray());

    private static void ValidateTier(int tier, string parameterName)
    {
        if (tier is < 0 or > 5)
            throw new ArgumentOutOfRangeException(parameterName, "Standard art tier must be in range 0-5.");
    }
}

internal sealed record MortalWoundScenarioFixture(JsonObject WoundProposal, MortalAuthorityRoots AuthorityRoots, MortalWoundScenarioRefs Refs, WoundExpectedFacts Expected, IReadOnlyList<string> CanonicalWritePaths);
internal sealed record MortalAuthorityRoots(JsonObject ResourceDefinitions, JsonObject ResourceState, JsonObject ResourceHistory, JsonObject PlayerInventory, JsonObject ItemIdentityIndex, JsonObject WorldMap, JsonObject ProviderAuthority);
internal sealed record MortalWoundScenarioRefs(string WoundRef, string OwnerId, string EventRef, string RouteId, string HiddenRouteId, string DiagnosisPathId, string BandageItemId, string AntisepticItemId, string SterileThreadItemId, string AntibioticItemId, string CapabilityRef, string ProviderRef, string FacilityRef, string LocationRef);
internal sealed record WoundExpectedFacts(string Realm, string Domain, string Severity, string OwnerId, string EventRef, string RecoveryMode, IReadOnlyList<string> ConsumptionCoordinates);
internal sealed record SpiritualConflictScenarioFixture(JsonObject AfterlifeProfiles, JsonObject ConflictState, SpiritualConflictScenarioRefs Refs, SpiritualConflictExpectedFacts Expected, IReadOnlyList<string> CanonicalWritePaths);
internal sealed record SpiritualConflictScenarioRefs(string PlayerProfileId, string OpponentProfileId, string ConflictId, string EventRef, string SealedD20Ref, string PlayerSideId, string OppositionSideId);
internal sealed record SpiritualConflictExpectedFacts(string DangerMode, string BeforeStrain, string DestinationStrain, string ExpectedComputedCeiling, int HarmfulMargin, int TargetResilienceTier, int AppliedArtTier, int ExtraJumpSteps, bool HasPriorTrainingEscalation);
internal sealed record ElyaraScenarioFixture(JsonObject GuardianLibrary, JsonObject AfterlifeProfiles, JsonObject AfterlifeProfile, ElyaraScenarioRefs Refs, ElyaraExpectedProtectedFields Expected, IReadOnlyList<string> CanonicalWritePaths);
internal sealed record ElyaraScenarioRefs(string GuardianId, string LocationRef, string ServiceRef, string FirstEntryAvailabilityRef);
internal sealed record ElyaraExpectedProtectedFields(bool DiscoverableFromFirstChaosSeaEntry, IReadOnlyList<string> ProtectedProfilePaths);
internal sealed record ShiningFactionScenarioFixture(JsonObject ShiningState, JsonObject ResidentRoster, JsonObject AfterlifeProfiles, ShiningFactionScenarioRefs Refs, ShiningFactionExpectedFacts Expected, IReadOnlyList<string> CanonicalWritePaths);
internal sealed record ShiningFactionScenarioRefs(string FactionId, string ResidentId, string ProfileActorId, string LocationRef, string? ServiceRef);
internal sealed record ShiningFactionExpectedFacts(int HealingTier, string ServiceVisibility, bool HasPublicCommandAccess);
