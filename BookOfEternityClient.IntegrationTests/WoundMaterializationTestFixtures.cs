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
            LocationRef: "loc_pa_collapse_aid_station",
            ComplicationRef: "complication_pa_infection_risk",
            LocationAuthorityRef: "body_part_left_forearm");

        var items = CreateMortalItems(
            (refs.BandageItemId, "Стерильный бинт аварийного набора"),
            (refs.AntisepticItemId, "Антисептик из аварийного набора"),
            (refs.SterileThreadItemId, "Стерильная хирургическая нить"),
            (refs.AntibioticItemId, "Курс антибиотика широкого действия"));
        var location = CreateScenarioLocation(refs.LocationRef, "Медпункт у обрушенного перехода", "Чистый стол медпункта среди заражённых обломков.", "visited");
        var proposal = CreateMortalProposal(
            refs,
            woundType: "contaminated_laceration",
            woundName: "Загрязнённая рваная рана предплечья",
            readableLocus: "наружная сторона левого предплечья",
            cause: "Острый металлический край вскрыл предплечье во время заражённого обвала.",
            visibleSymptoms: new JsonArray("кровотечение", "пульсирующая боль"),
            complicationKind: "infection",
            complicationDisplayName: "Риск воспаления",
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
            CreateMortalWritePaths());
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
            LocationRef: "loc_mw_singing_glass_observatory",
            ComplicationRef: "complication_mw_resonance_instability",
            LocationAuthorityRef: "resonance_channel_left_hand");

        var items = CreateMortalItems(
            (refs.BandageItemId, "Резонансная повязка"),
            (refs.AntisepticItemId, "Пыль резонансного кристалла"),
            (refs.SterileThreadItemId, "Настроенный лечебный фокус"),
            (refs.AntibioticItemId, "Эликсир серебряного мха"));
        var location = CreateScenarioLocation(refs.LocationRef, "Обсерватория Поющего Стекла", "Кристаллическая обсерватория с настроенной лечебной камерой.", "discovered");
        var proposal = CreateMortalProposal(
            refs,
            woundType: "crystalline_resonance_burn",
            woundName: "Кристаллический ожог проводящих каналов",
            readableLocus: "каналы левой руки и грудной резонатор",
            cause: "Трещина в поющем кристалле обожгла тело и нарушила внутренний резонанс.",
            visibleSymptoms: new JsonArray("светящиеся трещины кожи", "дрожь при магическом усилии"),
            complicationKind: "spiritual_instability",
            complicationDisplayName: "Резонансная нестабильность",
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
            CreateMortalWritePaths());
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
        var suffix = dangerMode;
        var conflictId = $"conflict_wound_{suffix}_boundary";
        var eventRef = $"event_turn_42_spiritual_strain_{suffix}";
        var sealedD20Ref = $"sealed_d20_turn_42_spiritual_strain_{suffix}";
        var playerProfile = CreateCurrentSpiritualProfile("player_soul", playerId, "Chaos Sea");
        var opponentProfile = CreateCurrentSpiritualProfile("guardian", opponentId, "Chaos Sea");
        var conflict = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["activeConflict"] = new JsonObject
            {
                ["conflictId"] = conflictId,
                ["realm"] = "Chaos Sea",
                ["sideModel"] = "direct_duel",
                ["playerSide"] = CreateConflictSide("player", playerId),
                ["oppositionSide"] = CreateConflictSide("guardian", opponentId, includeGuardianArtAuthority: true),
                ["playerSideStrain"] = "strained",
                ["oppositionSideStrain"] = "clear",
                ["conflictPosition"] = "contested",
                ["resolutionState"] = "active",
                ["exchangeLog"] = new JsonArray()
            },
            ["recentConflicts"] = new JsonArray()
        };

        var profiles = AfterlifeEntityProfileState.CreateDefaultRoot();
        profiles[AfterlifeEntityProfileState.ProfilesProperty] = new JsonArray(playerProfile.DeepClone(), opponentProfile.DeepClone());
        var future = new JsonObject
        {
            ["standardArts"] = new JsonObject
            {
                ["player"] = CreateFutureArts(resilienceTier, healingTier),
                ["opposition"] = CreateFutureArts(1, 2)
            },
            ["dangerEnvelope"] = new JsonObject
            {
                ["dangerMode"] = dangerMode, ["escalatedFromTraining"] = hasPriorTrainingEscalation,
                ["annihilationAuthority"] = dangerMode == "annihilation" ? new JsonObject { ["authorityId"] = $"authority_{suffix}_wound_conflict" } : null,
                ["sideWoundState"] = new JsonObject { ["player"] = new JsonObject { ["newWoundId"] = null, ["opportunityCount"] = 0 }, ["opposition"] = new JsonObject { ["newWoundId"] = null, ["opportunityCount"] = 0 } }
            },
            ["acceptedStrainEvidence"] = new JsonObject { ["eventRef"] = eventRef, ["sealedD20Ref"] = sealedD20Ref, ["targetSide"] = "player", ["before"] = "strained", ["destination"] = "fractured", ["harmfulMargin"] = -8, ["appliedArtTier"] = 4, ["targetResilienceTier"] = resilienceTier, ["extraJumpSteps"] = 0, ["expectedComputedCeiling"] = "II" },
            ["priorTrainingEscalation"] = hasPriorTrainingEscalation ? new JsonObject { ["eventRef"] = $"event_turn_41_training_escalation_{suffix}", ["receiptRef"] = $"receipt_training_escalation_{suffix}", ["evidenceRef"] = $"evidence_training_escalation_{suffix}" } : null
        };
        return new SpiritualConflictScenarioFixture(
            profiles,
            conflict,
            future,
            new SpiritualConflictScenarioRefs(playerId, opponentId, conflictId, eventRef,
                sealedD20Ref, "player", "opposition"),
            new SpiritualConflictExpectedFacts(dangerMode, "strained", "fractured", "II", -8,
                resilienceTier, 4, 0, hasPriorTrainingEscalation),
            CreateSpiritualWritePaths());
    }

    internal static ElyaraScenarioFixture CreateElyaraScenario()
    {
        var manifestPath = Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient", "system_guardians", "built_in", "elyara", "manifest.json");
        var dossierPath = Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient", "system_guardians", "built_in", "elyara", "dossier.md");
        var manifest = JsonNode.Parse(File.ReadAllText(manifestPath))?.AsObject()
            ?? throw new InvalidOperationException("Built-in Elyara manifest is missing or invalid.");
        var dossier = File.ReadAllText(dossierPath);
        var profile = CreateCurrentSpiritualProfile("guardian", "elyara", "Chaos Sea");
        profile["displayName"] = manifest["displayName"]!.DeepClone();
        profile["locationId"] = "location_elyara_lazaret";
        profile["locationName"] = "Лазарет Незаживающего Света";
        var profiles = AfterlifeEntityProfileState.CreateDefaultRoot();
        profiles[AfterlifeEntityProfileState.ProfilesProperty] = new JsonArray(profile.DeepClone());
        var future = new JsonObject
        {
            ["spiritualHealing"] = new JsonObject { ["tier"] = 5, ["experience"] = 0 },
            ["healingServiceProfile"] = CreateHealingService("elyara", "chaos_sea", "location_elyara_lazaret", "public", 100)
        };
        return new ElyaraScenarioFixture(
            new ElyaraBuiltInAssets(manifest.DeepClone().AsObject(), dossier, "/alwaysAvailable"), profiles, profile, future,
            new ElyaraScenarioRefs("elyara", "location_elyara_lazaret", "service_elyara_healing", "/alwaysAvailable"),
            new ElyaraExpectedProtectedFields(DiscoverableFromFirstChaosSeaEntry: true, new[]
            {
                "/standardArts/spiritual_healing/tier", "/locationId", "/locationName",
                "/healingServiceProfile/visibility", "/healingServiceProfile/availability",
                "/healingServiceProfile/priceMultiplierPercent"
            }),
            CreateElyaraWritePaths());
    }

    internal static ShiningFactionScenarioFixture CreateShiningFactionScenario(
        int healingTier = 3,
        string? serviceVisibility = null)
    {
        if (healingTier is < 1 or > 5)
            throw new ArgumentOutOfRangeException(nameof(healingTier), "Shining healer tier must be in range 1-5.");
        if (serviceVisibility is not (null or "restricted" or "public"))
            throw new ArgumentOutOfRangeException(nameof(serviceVisibility));

        const string factionId = "faction_shining_wound_sanctuary";
        const string residentId = "resident_shining_wound_healer";
        const string locationId = "location_shining_wound_sanctuary";
        var faction = CreateCurrentShiningFaction(factionId, locationId, residentId);
        const string guardianId = "guard_system_azalia_001";
        const string abodeId = "abode_azalia_wound_sanctuary";
        var guardianRoot = CreateAzaliaGuardianRoot(guardianId, abodeId);
        var resident = CreateCurrentResident(residentId, guardianId, abodeId, factionId);
        var profile = CreateCurrentSpiritualProfile("resident", residentId, "Shining Abode");
        var shining = ShiningAbodeState.CreateDefaultState();
        shining["availability"] = ShiningAbodeState.AvailabilityActive;
        shining["radiance"] = new JsonObject { ["experience"] = 250, ["tier"] = 2 };
        shining["lightSparks"] = 80;
        shining["halls"] = new JsonArray(new JsonObject { ["hallId"] = locationId, ["hallName"] = "Санктуарий Тихих Швов", ["description"] = "Зал спокойной духовной поддержки.", ["serviceTags"] = new JsonArray("healing", "support") });
        shining["factions"] = new JsonArray(faction.DeepClone());
        shining["factionFoundingReceipts"] = new JsonArray();
        shining["factionRealignmentReceipts"] = new JsonArray();
        var profiles = AfterlifeEntityProfileState.CreateDefaultRoot();
        profiles[AfterlifeEntityProfileState.ProfilesProperty] = new JsonArray(profile.DeepClone());
        var future = new JsonObject
        {
            ["residentRole"] = new JsonObject { ["key"] = "healing_support", ["visibility"] = "known_to_player" },
            ["spiritualHealing"] = new JsonObject { ["tier"] = healingTier, ["experience"] = 0 },
            ["healingServiceProfile"] = serviceVisibility == null ? null : CreateHealingService(residentId, "shining_abode", locationId, serviceVisibility, 100)
        };

        return new ShiningFactionScenarioFixture(
            shining,
            new JsonObject
            {
                ["entries"] = new JsonArray(resident.DeepClone()),
                ["thoughtJournal"] = new JsonArray(), ["interactionLog"] = new JsonArray(),
                ["historyLog"] = new JsonArray(), ["transferReceipts"] = new JsonArray(),
                ["interactionReceipts"] = new JsonArray(), ["rosterReceipts"] = new JsonArray()
            },
            profiles,
            future,
            guardianRoot,
            new ShiningFactionScenarioRefs(factionId, residentId, residentId, abodeId,
                serviceVisibility == null ? null : $"service_{residentId}_healing"),
            new ShiningFactionExpectedFacts(healingTier, serviceVisibility ?? "absent",
                serviceVisibility == "public"),
            CreateShiningWritePaths());
    }

    private static JsonObject CreateMortalProposal(
        MortalWoundScenarioRefs refs,
        string woundType,
        string woundName,
        string readableLocus,
        string cause,
        JsonArray visibleSymptoms,
        string complicationKind,
        string complicationDisplayName,
        JsonObject hiddenRoute,
        JsonObject visibleRoute,
        JsonObject diagnosisPath) => new()
        {
            ["proposalRef"] = refs.WoundRef,
            ["eventRef"] = refs.EventRef,
            ["owner"] = new JsonObject { ["realm"] = "mortal_world", ["ownerKind"] = "player", ["ownerId"] = refs.OwnerId, ["carrierPath"] = WoundMaterializationTestContext.PlayerWoundsPath },
            ["domain"] = "physical",
            ["classification"] = new JsonObject { ["woundType"] = woundType, ["locationProfile"] = new JsonObject { ["kind"] = "anatomical", ["readableLocus"] = readableLocus, ["authorityKind"] = "body_part", ["authorityRef"] = refs.LocationAuthorityRef, ["affectedSide"] = "left" } },
            ["display"] = new JsonObject { ["name"] = woundName, ["description"] = cause, ["visibleSymptoms"] = visibleSymptoms.DeepClone(), ["prognosis"] = "Без стабилизации вероятно осложнение.", ["visibility"] = "known_to_player", ["acquisitionNarration"] = cause },
            ["severity"] = new JsonObject { ["value"] = "II", ["rank"] = 2, ["maximumAtCreation"] = "II", ["lastChangeEventRef"] = refs.EventRef },
            ["care"] = new JsonObject { ["state"] = "untreated", ["stabilizedAtTurn"] = null, ["activeCourseId"] = null, ["lastAttemptId"] = null },
            ["complications"] = new JsonArray(new JsonObject { ["complicationId"] = refs.ComplicationRef, ["kind"] = complicationKind, ["state"] = "risk", ["displayName"] = complicationDisplayName, ["treatmentDifficultyModifier"] = 0, ["ownedEffectIds"] = new JsonArray(), ["visibility"] = "hidden" }),
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

    private static JsonObject CreateContaminationDiagnosisPath(MortalWoundScenarioRefs refs) => CreateDiagnosisPath(refs, "Осмотр загрязнения");
    private static JsonObject CreateResonanceDiagnosisPath(MortalWoundScenarioRefs refs) => CreateDiagnosisPath(refs, "Проверка резонансного следа");
    private static JsonObject CreateDiagnosisPath(MortalWoundScenarioRefs refs, string displayName) => new()
    {
        ["diagnosisPathId"] = refs.DiagnosisPathId, ["displayName"] = displayName, ["visibility"] = "known_to_player",
        ["requirements"] = new JsonArray(CapabilityRequirement(refs.CapabilityRef), LocationRequirement(refs.LocationRef)),
        ["check"] = new JsonObject { ["formulaKey"] = "guaranteed_capability", ["capabilityRef"] = refs.CapabilityRef },
        ["reveals"] = new JsonArray($"route:{refs.HiddenRouteId}", $"complication:{refs.ComplicationRef}"), ["failurePolicy"] = "no_reveal"
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
            MortalLocationTestFixture.CreateCurrentProjection(location),
            MortalLocationTestFixture.CreateIdentityIndex(location),
            CreateMortalActorAuthority(refs, providerDisplayName, capabilityKind, facilityKind));
    }

    private static JsonObject[] CreateMortalItems(params (string ItemId, string Name)[] definitions) => definitions.Select(definition =>
    {
        var item = MortalItemTestFixture.CreateCanonicalRootAtTurn(definition.ItemId, Turn, "accepted_turn", "turn_outcome", "turn_42", definition.Name);
        item["type"] = "Медицинский расходник";
        item["group"] = "Материалы лечения";
        item["image_prompt"] = "лечебный расходник в тёмном фэнтези, без текста";
        return item;
    }).ToArray();

    private static JsonObject CreateScenarioLocation(string locationId, string displayName, string description, string discoveryTier)
    {
        var location = MortalLocationTestFixture.CreateCanonicalLocationWithIdentity(locationId, displayName, discoveryTier);
        location["purpose"] = description;
        location["description"] = description;
        location["image_prompt"] = "мрачная лечебная локация, без текста";
        MortalLocationTestFixture.ResealCanonicalLocation(location);
        return location;
    }

    private static JsonObject CreateMortalActorAuthority(
        MortalWoundScenarioRefs refs,
        string providerDisplayName,
        string capabilityKind,
        string facilityKind)
    {
        var actor = EffectMaterializationTestFixture.CreateSameTurnMortalActor(refs.ProviderRef);
        actor["NPCId"] = refs.ProviderRef;
        actor["name"] = providerDisplayName;
        actor["displayName"] = providerDisplayName;
        actor["description"] = $"{providerDisplayName} ведёт лечение только по точным материальным основаниям.";
        actor["image_prompt"] = "специалист по лечению в тёмном фэнтези, без текста";
        actor["currentLocationId"] = refs.LocationRef;
        actor["capabilityEvidence"] = new JsonArray(new JsonObject { ["capabilityRef"] = refs.CapabilityRef, ["kind"] = capabilityKind, ["tier"] = 2, ["facilityRef"] = refs.FacilityRef });
        actor["facilities"] = new JsonArray(new JsonObject { ["facilityRef"] = refs.FacilityRef, ["locationRef"] = refs.LocationRef, ["kind"] = facilityKind, ["visibility"] = "known_to_player" });
        return new JsonObject { ["NPCsInScene"] = new JsonArray(actor.DeepClone()) };
    }

    private static JsonObject CreateCurrentSpiritualProfile(string actorType, string actorId, string realm)
    {
        var profile = AfterlifeActorMaterializationTestFixture.CreateCompleteProfile(actorType, actorId, realm, Turn);
        return profile;
    }

    private static JsonObject CreateFutureArts(int resilienceTier, int healingTier) => new()
    {
        ["spiritual_resilience"] = new JsonObject { ["tier"] = resilienceTier, ["experience"] = 0 },
        ["spiritual_healing"] = new JsonObject { ["tier"] = healingTier, ["experience"] = 0 }
    };

    private static JsonObject CreateConflictSide(string actorType, string actorId, bool includeGuardianArtAuthority = false)
    {
        var lead = new JsonObject { ["actorType"] = actorType, ["actorId"] = actorId, ["displayName"] = "Сторона точного конфликта" };
        if (includeGuardianArtAuthority)
        {
            lead["actorArtTierSnapshot"] = new JsonObject { ["guard"] = 1 };
            lead["artAuthoritySource"] = "guardian_state";
        }
        return new JsonObject { ["leadContestant"] = lead, ["supporters"] = new JsonArray() };
    }

    private static JsonObject CreateHealingService(string providerId, string realm, string locationRef, string visibility, int priceMultiplierPercent) => new()
    {
        ["serviceId"] = $"service_{providerId}_healing", ["providerId"] = providerId, ["realm"] = realm, ["locationRef"] = locationRef,
        ["visibility"] = visibility, ["availability"] = "available", ["minimumRelationship"] = null, ["priceMultiplierPercent"] = priceMultiplierPercent,
        ["compensationKinds"] = new JsonArray("ink_feathers", "favor", "debt", "quest", "free_aid"), ["accessConditions"] = new JsonArray()
    };

    private static JsonObject CreateCurrentResident(string residentId, string guardianId, string abodeId, string factionId) => new()
    {
        ["residentId"] = residentId, ["guardianId"] = guardianId, ["abodeId"] = abodeId,
        ["displayName"] = "Хранительница тихих швов", ["residentKind"] = "attendant_spirit", ["originType"] = "native_spirit",
        ["roleLabel"] = "целительница поддержки", ["summary"] = "Дух Обители, поддерживающий исцеление без ложного обещания полной цены.",
        ["bondLevel"] = 34, ["bondTier"] = "familiar", ["canGrantCompanionRelic"] = false, ["bondRewardState"] = "none", ["historyRevealed"] = false, ["isPresent"] = true,
        ["personalityProfile"] = new JsonObject { ["archetype"] = "внимательная целительница", ["speechPattern"] = "тихая и точная", ["coreValues"] = new JsonArray("милость", "память", "ответственность") },
        ["abodeDisposition"] = new JsonObject { ["powerSensitivity"] = "medium", ["migrationDisposition"] = "selective", ["communalOrientation"] = "medium", ["stabilityNeed"] = "medium" }, ["abodeDevotionLevel"] = 28, ["abodeDevotionTier"] = "uncertain", ["restlessness"] = 12, ["migrationState"] = "restless",
        ["mortalWorldImprint"] = new JsonObject { ["originWorldSummary"] = "Она помнит только свет, в котором боль становилась выносимой.", ["futureCompanionPrompt"] = "Покажи её как целительницу, которая называет цену исцеления прямо.", ["bondReason"] = "Она сохраняет память о каждом согласившемся на помощь.", ["coreTraits"] = new JsonArray("внимательная", "стойкая"), ["archetypeHints"] = new JsonArray("healer", "witness"), ["appearanceMotifs"] = new JsonArray("серебряные нити", "тихий свет") },
        ["availableInteractions"] = new JsonArray("talk")
        , ["ascensionState"] = "ascended", ["shiningFactionId"] = factionId, ["factionLoyaltyLevel"] = 50, ["factionLoyaltyTier"] = "committed", ["factionRestlessness"] = 20, ["factionRealignmentState"] = "settled", ["visibleRole"] = "social_support"
    };

    private static JsonObject CreateAzaliaGuardianRoot(string guardianId, string abodeId) => new()
    {
        ["guardians"] = new JsonArray(new JsonObject { ["guardianId"] = guardianId, ["canonicalName"] = "Азалия", ["sourcePreset"] = new JsonObject { ["presetId"] = "azalia", ["library"] = "built_in" }, ["abode"] = new JsonObject { ["abodeId"] = abodeId, ["isDiscovered"] = true } }),
        ["activeGuardian"] = new JsonObject { ["guardianId"] = guardianId, ["canonicalName"] = "Азалия", ["abode"] = new JsonObject { ["abodeId"] = abodeId, ["isDiscovered"] = true } }
    };

    private static JsonObject CreateCurrentShiningFaction(string factionId, string hallId, string residentId) => new()
    {
        ["factionId"] = factionId, ["originType"] = ShiningAbodeState.OriginTypeNativeRadiant, ["hallId"] = hallId,
        ["creationProvenance"] = new JsonObject { ["route"] = "native_discovery", ["authorityType"] = "shining_core_action_request", ["authorityId"] = "request_discover_shining_wound_sanctuary" },
        ["charter"] = new JsonObject { ["factionName"] = "Санктуарий Тихих Швов", ["favoredArchetype"] = ShiningAbodeState.ProjectArchetypeAccord, ["patronEffectFamily"] = ShiningAbodeState.EffectFamilySocial, ["summary"] = "Фракция духовной поддержки." },
        ["currentAgenda"] = "Сохранить доступный путь к помощи для жителей Обители.", ["visibility"] = "revealed", ["storyAuthority"] = null,
        ["factionLifecycle"] = new JsonObject { ["state"] = ShiningAbodeState.FactionLifecycleStateActive },
        ["leadership"] = new JsonObject { ["headActorType"] = ShiningAbodeState.HeadActorTypeResident, ["headActorId"] = residentId, ["leadershipState"] = ShiningAbodeState.LeadershipStateSecure },
        ["strategicMemory"] = new JsonObject { ["summary"] = "Санктуарий хранит последовательность принятых обязательств.", ["lastUpdatedTurn"] = Turn, ["recentCampaigns"] = new JsonArray(), ["losses"] = new JsonArray(), ["alliances"] = new JsonArray(), ["enemies"] = new JsonArray() },
        ["chronicle"] = new JsonArray(new JsonObject { ["entryId"] = "chronicle_shining_wound_sanctuary", ["turnNumber"] = Turn, ["eventType"] = "faction_materialized", ["summary"] = "Санктуарий открыл свой зал.", ["visibility"] = "known", ["consequences"] = new JsonArray() }),
        ["baseStrength"] = 35, ["factionStrength"] = 35, ["investCountThisAscension"] = 0, ["projectArchetypesCountedThisAscension"] = new JsonArray(), ["projects"] = new JsonArray(), ["territorialInfluence"] = new JsonArray(), ["resourceLedger"] = new JsonArray(), ["tradeInventory"] = null, ["tradeInventoryReceipts"] = new JsonArray(), ["leadershipReceipts"] = new JsonArray(), ["leadershipHistory"] = new JsonArray(),
        ["materialization"] = new JsonObject { ["schemaVersion"] = 1, ["materializationId"] = "fmat_shining_wound_sanctuary_42", ["factionType"] = "shining_faction", ["factionId"] = factionId, ["materializedAtTurn"] = Turn, ["state"] = "complete", ["capabilities"] = new JsonObject { ["runsProjects"] = false, ["holdsTerritorialInfluence"] = false, ["usesResourceLedger"] = false, ["hasResidentAffiliations"] = false, ["canTrade"] = false, ["hasLeadershipHistory"] = false, ["usesStoryState"] = false }, ["sections"] = new JsonObject { ["projects"] = EmptyDisposition(), ["territorialInfluence"] = EmptyDisposition(), ["resourceLedger"] = EmptyDisposition(), ["residentAffiliations"] = EmptyDisposition(), ["trade"] = EmptyDisposition(), ["leadershipHistory"] = EmptyDisposition(), ["storyState"] = EmptyDisposition() } }
    };

    private static JsonObject EmptyDisposition() => new() { ["state"] = "empty_by_design", ["reason"] = "Не требуется для сценарного seed." };

    private static JsonObject CreateEmptyShiningGates() => new()
    {
        ["draftVersion"] = 0, ["hasOpenDraft"] = false, ["isStale"] = false, ["nextCandidateCursor"] = 0, ["rerollsRemaining"] = 0,
        ["allCandidateBlessingCards"] = new JsonArray(), ["availableBlessingCards"] = new JsonArray(), ["shownBlessingCardIds"] = new JsonArray(), ["selectedBlessingCardIds"] = new JsonArray()
    };

    private static JsonObject CreateEmptyGachaSystem() => new()
    {
        ["chargesPerReturn"] = 0, ["chargesUsedThisReturn"] = 0, ["currentReturnCycleId"] = "return_1", ["gachaHistory"] = new JsonArray()
    };

    private static IReadOnlyList<string> CreateMortalWritePaths() => CreateWritePaths(
        WoundMaterializationTestContext.PlayerWoundsPath,
        ResourceMaterializationContract.DefinitionsPath,
        ResourceMaterializationContract.StatePath,
        ResourceMaterializationContract.HistoryPath,
        InventoryEquipmentService.ItemsPath,
        MortalItemIdentityState.StatePath,
        MortalLocationMaterializationContract.WorldMapPath,
        MortalLocationMaterializationContract.CurrentLocationPath,
        MortalLocationIdentityState.StatePath,
        NpcCoreChangesContract.NpcCorePath);

    private static IReadOnlyList<string> CreateSpiritualWritePaths() => CreateWritePaths(
        WoundMaterializationTestContext.AfterlifeEntityProfilesPath,
        WoundMaterializationTestContext.AfterlifeSpiritualConflictPath);

    private static IReadOnlyList<string> CreateElyaraWritePaths() => CreateWritePaths(
        WoundMaterializationTestContext.AfterlifeEntityProfilesPath);

    private static IReadOnlyList<string> CreateShiningWritePaths() => CreateWritePaths(
        WoundMaterializationTestContext.AfterlifeEntityProfilesPath,
        WoundMaterializationTestContext.GuardianAbodeResidentsPath,
        "game_state/meta/shining_abode_state.json");

    private static IReadOnlyList<string> CreateWritePaths(params string[] paths) => Array.AsReadOnly(
        paths.Distinct(StringComparer.Ordinal).OrderBy(static path => path, StringComparer.Ordinal).ToArray());

    private static void ValidateTier(int tier, string parameterName)
    {
        if (tier is < 0 or > 5)
            throw new ArgumentOutOfRangeException(parameterName, "Standard art tier must be in range 0-5.");
    }
}

internal sealed record MortalWoundScenarioFixture(JsonObject WoundProposal, MortalAuthorityRoots AuthorityRoots, MortalWoundScenarioRefs Refs, WoundExpectedFacts Expected, IReadOnlyList<string> CanonicalWritePaths);
internal sealed record MortalAuthorityRoots(JsonObject ResourceDefinitions, JsonObject ResourceState, JsonObject ResourceHistory, JsonObject PlayerInventory, JsonObject ItemIdentityIndex, JsonObject WorldMap, JsonObject CurrentLocation, JsonObject LocationIdentityIndex, JsonObject ProviderAuthority);
internal sealed record MortalWoundScenarioRefs(string WoundRef, string OwnerId, string EventRef, string RouteId, string HiddenRouteId, string DiagnosisPathId, string BandageItemId, string AntisepticItemId, string SterileThreadItemId, string AntibioticItemId, string CapabilityRef, string ProviderRef, string FacilityRef, string LocationRef, string ComplicationRef, string LocationAuthorityRef);
internal sealed record WoundExpectedFacts(string Realm, string Domain, string Severity, string OwnerId, string EventRef, string RecoveryMode, IReadOnlyList<string> ConsumptionCoordinates);
internal sealed record SpiritualConflictScenarioFixture(JsonObject AfterlifeProfiles, JsonObject ConflictState, JsonObject FutureProposal, SpiritualConflictScenarioRefs Refs, SpiritualConflictExpectedFacts Expected, IReadOnlyList<string> CanonicalWritePaths);
internal sealed record SpiritualConflictScenarioRefs(string PlayerProfileId, string OpponentProfileId, string ConflictId, string EventRef, string SealedD20Ref, string PlayerSideId, string OppositionSideId);
internal sealed record SpiritualConflictExpectedFacts(string DangerMode, string BeforeStrain, string DestinationStrain, string ExpectedComputedCeiling, int HarmfulMargin, int TargetResilienceTier, int AppliedArtTier, int ExtraJumpSteps, bool HasPriorTrainingEscalation);
internal sealed record ElyaraScenarioFixture(ElyaraBuiltInAssets BuiltInAssets, JsonObject AfterlifeProfiles, JsonObject AfterlifeProfile, JsonObject FutureProposal, ElyaraScenarioRefs Refs, ElyaraExpectedProtectedFields Expected, IReadOnlyList<string> CanonicalWritePaths);
internal sealed record ElyaraBuiltInAssets(JsonObject Manifest, string DossierMarkdown, string DiscoverabilityPointer);
internal sealed record ElyaraScenarioRefs(string GuardianId, string LocationRef, string ServiceRef, string DiscoverabilityPointer);
internal sealed record ElyaraExpectedProtectedFields(bool DiscoverableFromFirstChaosSeaEntry, IReadOnlyList<string> ProtectedProfilePaths);
internal sealed record ShiningFactionScenarioFixture(JsonObject ShiningState, JsonObject ResidentRoster, JsonObject AfterlifeProfiles, JsonObject FutureProposal, JsonObject GuardianRoot, ShiningFactionScenarioRefs Refs, ShiningFactionExpectedFacts Expected, IReadOnlyList<string> CanonicalWritePaths);
internal sealed record ShiningFactionScenarioRefs(string FactionId, string ResidentId, string ProfileActorId, string LocationRef, string? ServiceRef);
internal sealed record ShiningFactionExpectedFacts(int HealingTier, string ServiceVisibility, bool HasPublicCommandAccess);
