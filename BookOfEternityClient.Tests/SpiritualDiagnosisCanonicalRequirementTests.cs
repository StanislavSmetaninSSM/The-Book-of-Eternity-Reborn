using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>Independent canonical input DATA for the eight missing diagnosis adapters.</summary>
public sealed class SpiritualDiagnosisCanonicalRequirementTests
{
    private const string RelicId = "sr_utility_diagnosis_1672531200000";
    private const string LocationId = "diagnosis_locality_current";
    private const string QuestId = "soulquest_guardian_diagnosis_1672531200000";

    /// <summary>Every declared kind needs its real source, not a fabricated Mortal snapshot.</summary>
    [Theory]
    [InlineData("chaos_sea", "item_quantity")]
    [InlineData("shining_abode", "item_quantity")]
    [InlineData("chaos_sea", "resource_quantity")]
    [InlineData("shining_abode", "resource_quantity")]
    [InlineData("chaos_sea", "consent")]
    [InlineData("shining_abode", "consent")]
    [InlineData("chaos_sea", "facility")]
    [InlineData("shining_abode", "facility")]
    [InlineData("chaos_sea", "location")]
    [InlineData("shining_abode", "location")]
    [InlineData("chaos_sea", "quest_state")]
    [InlineData("shining_abode", "quest_state")]
    [InlineData("chaos_sea", "effect_state")]
    [InlineData("shining_abode", "effect_state")]
    [InlineData("chaos_sea", "environment")]
    [InlineData("shining_abode", "environment")]
    public async Task RealCanonicalRequirementMakesOnlyTheDeclaredDiagnosisAvailable(string realm, string kind)
    {
        var context = await ContextAsync(realm, kind);
        var wound = Assert.Single(context.Catalog.Occurrences).Wound;
        Assert.True(context.Projection.IsAvailable);
        Assert.Empty(context.Input.History.ValidateAgreement(context.Input.Identities, context.Catalog));
        Assert.Equal(kind, Assert.Single(Assert.Single(wound.Treatment.DiagnosisPaths).Requirements)
            .GetProperty("kind").GetString());
        Assert.Equal(0, context.Soul["afterlifeCombatProfile"]!["artTiers"]!["spiritual_healing"]!.GetValue<int>());
        var before = Snapshot(context);
        var state = WoundApplicationService.CreateState("canonical_diagnosis_" + kind, context);
        var choice = Assert.Single(state.Choices, value => value.Selection.Diagnosis is not null);
        Assert.Equal(wound.WoundId, choice.Selection.WoundId);
        Assert.Equal("diagnose_tissue", choice.Selection.Diagnosis!.DiagnosisPathId);
        Assert.Equal(before, Snapshot(context));
        Assert.Equal("hidden", Assert.Single(wound.Complications).Visibility);
        // The baseline must reach only this missing-adapter oracle after all
        // source/identity/history/realm and read-only premises above are valid.
        Assert.True(choice.Available, $"The exact current {kind} source must satisfy its declared diagnosis condition: {choice.Reason}");
        var admission = Assert.IsType<SpiritualDiagnosisAdmission>(new SpiritualDiagnosisSourceData(context).Admit(wound, "diagnose_tissue").Data);
        SpiritualDiagnosisRequirementBindings.Validate(admission);
        var turn = new SpiritualSelfTreatmentStageContext("session_canonical_diagnosis", "request_canonical_diagnosis", 43,
            new BookOfEternityClient.Models.ProgressionControl { CurrentRealm = context.Soul["currentRealm"]!.GetValue<string>(), NextChaosSeaTurnOrdinal = 1 });
        var request = SpiritualDiagnosisRequestCodec.Create(turn, admission.Selection, admission);
        Assert.True(SpiritualDiagnosisRequestCodec.IsExact(request, turn, admission.Selection, admission));
        var program = SpiritualDiagnosisProgram.Build(turn, request, admission, [new(wound, 0)]);
        Assert.Equal("known_to_player", Assert.Single(program.KnowledgeAfter.Complications).Visibility);
        Assert.Single(program.NaturalStages);
    }

    internal static async Task<WoundPlayerReadContext> ContextAsync(string realm, string kind)
    {
        var context = SpiritualDiagnosisAdmissionTests.Context(realm, Requirement(kind));
        var arts = AfterlifeSpiritualConflictState.CreateDefaultArtTiers();
        arts["spiritual_healing"] = 0;
        var combat = AfterlifeSpiritualConflictState.CreateDefaultCombatProfile();
        combat["artTiers"] = arts.DeepClone();
        context.Soul["afterlifeCombatProfile"] = combat;
        context.Input.Carriers.AfterlifeProfiles!["profiles"]![0]!["standardArts"] = arts.DeepClone();
        var roots = new Dictionary<string, JsonObject>(StringComparer.Ordinal)
        {
            ["game_state/meta/soul_state.json"] = context.Soul,
            [WoundCarrierCatalog.AfterlifeProfilesPath] = context.Input.Carriers.AfterlifeProfiles!,
            [WoundIdentityState.StatePath] = JsonNode.Parse(WoundIdentityState.SerializeCanonical(context.Input.Identities))!.AsObject(),
            [WoundHistoryState.HistoryPath] = JsonNode.Parse(WoundHistoryState.SerializeCanonical(context.Input.History))!.AsObject()
        };

        if (kind == "item_quantity")
        {
            context.Soul["soulRelics"] = new JsonObject
            {
                ["stored"] = new JsonArray(JsonNode.Parse("""
                    {"relicId":"sr_utility_diagnosis_1672531200000","name":"Диагностическая линза души",
                     "rarity":"Common","category":"Utility",
                     "effects":{"characteristicBonuses":{},"actionCheckBonuses":{}},
                     "equipmentData":{"equipSlot":"mainHand","enlightenmentRequirement":0},
                     "acquisitionData":{"sourceGuardian":"guardian_diagnosis","acquisitionStory":"Реликвия получена для бережного осмотра духовных ран."},
                     "gameplayStatus":{"equipped":false,"currentSlot":null}}
                    """)),
                ["equipped"] = new JsonArray()
            };
            Assert.Equal(RelicId, Assert.Single(context.Soul["soulRelics"]!["stored"]!.AsArray())!["relicId"]!.GetValue<string>());
        }
        else if (kind == "resource_quantity") context = await AddResourcesAsync(context, roots, realm);
        else if (kind == "quest_state")
        {
            roots["game_state/quests/soul_quests.json"] = JsonNode.Parse("""
                {"quests":[{"questId":"soulquest_guardian_diagnosis_1672531200000","guardianId":"guardian_diagnosis",
                 "title":"Осмотр духовной ткани","description":"Сохранить знания об осмотре между воплощениями.",
                 "objectives":[{"objectiveId":"objective_diagnosis","description":"Осмотреть рану","status":"Active"}],
                 "progress":{"completed":0,"total":1},"status":"active",
                 "rewards":{"inkFeathers":0,"enlightenmentExperience":0,"soulRelics":[],"reputationChanges":[]},
                 "crossIncarnation":true,"completionTimestamp":null}]}
                """)!.AsObject();
            Assert.Equal(QuestId, Assert.Single(roots["game_state/quests/soul_quests.json"]["quests"]!.AsArray())!["questId"]!.GetValue<string>());
        }
        else if (kind == "effect_state") context = AddActualEffects(context, roots, realm);
        else AddLocality(roots, realm, kind);

        return context with
        {
            CurrentMenuRoots = roots,
            CurrentMenuImages = roots.ToDictionary(pair => pair.Key,
                pair => new CanonicalBeforeImage(true, Encoding.UTF8.GetBytes(pair.Value.ToJsonString())), StringComparer.Ordinal)
        };
    }

    [Theory]
    [InlineData("chaos_sea", "item_quantity")][InlineData("shining_abode", "item_quantity")]
    [InlineData("chaos_sea", "resource_quantity")][InlineData("shining_abode", "resource_quantity")]
    [InlineData("chaos_sea", "consent")][InlineData("shining_abode", "consent")]
    [InlineData("chaos_sea", "facility")][InlineData("shining_abode", "facility")]
    [InlineData("chaos_sea", "location")][InlineData("shining_abode", "location")]
    [InlineData("chaos_sea", "quest_state")][InlineData("shining_abode", "quest_state")]
    [InlineData("chaos_sea", "effect_state")][InlineData("shining_abode", "effect_state")]
    [InlineData("chaos_sea", "environment")][InlineData("shining_abode", "environment")]
    public async Task ChangedCanonicalEvidenceRefusesNewAdmissionAndKeepsDetachedProof(string realm, string kind)
    {
        var context = await ContextAsync(realm, kind);
        var wound = Assert.Single(context.Catalog.Occurrences).Wound;
        var source = new SpiritualDiagnosisSourceData(context);
        var original = Assert.IsType<SpiritualDiagnosisAdmission>(source.Admit(wound, "diagnose_tissue").Data);
        SpiritualDiagnosisRequirementBindings.Validate(original);
        var frozen = original.RequirementBindings.ToJsonString();
        var export = original.RequirementBindings;
        export[0]!["evidence"]!["unrecognized"] = true;
        Assert.Equal(frozen, original.RequirementBindings.ToJsonString());
        Assert.Throws<InvalidDataException>(() => SpiritualDiagnosisRequirementBindings.Validate(original with { RequirementBindings = export }));
        var work = source.CanonicalResourceWork;
        if (kind == "resource_quantity") { Assert.Equal(1, work.Compositions); Assert.True(work.SourceReads > 0); }
        else Assert.Equal((0, 0), work);

        var roots = context.CurrentMenuRoots.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        if (kind == "item_quantity") context.Soul["soulRelics"]!["stored"]!.AsArray().Clear();
        else if (kind == "resource_quantity") roots.Remove(CanonicalResourceOwnerAuthorityComposer.AuthorityPath);
        else if (kind == "quest_state") roots[SpiritualDiagnosisCanonicalSources.QuestsPath]["quests"]!.AsArray().Clear();
        else if (kind == "effect_state")
        {
            var profile = roots[WoundCarrierCatalog.AfterlifeProfilesPath]["profiles"]![0]!;
            var effect = profile["activeEffects"]!.AsArray().OfType<JsonObject>().Single(row => row["effectId"]!.GetValue<string>() == "effect_spiritual_roll");
            effect["state"] = "suppressed";
            var actual = EffectMechanicsSnapshot.Build(new(new(null, null, null, null,
                roots[WoundCarrierCatalog.AfterlifeProfilesPath], null), roots[EffectIdentityState.StatePath]));
            context = context with { Input = context.Input with { Effects = actual } };
        }
        else
        {
            var container = realm == "chaos_sea" ? roots[SpiritualDiagnosisCanonicalSources.GuardiansPath]["guardians"]![0]!["abode"]!
                : roots[ShiningAbodeState.StatePath]["halls"]![0]!;
            if (kind == "location")
            {
                if (realm == "chaos_sea") roots[SpiritualDiagnosisCanonicalSources.GuardiansPath]["chaosSeaNavigation"]!["currentAbodeId"] = "different_locality";
                else roots[ShiningAbodeState.StatePath]["currentHallId"] = "different_locality";
            }
            else if (kind == "facility") container["customStates"]![0]!["available"] = false;
            else if (kind == "consent") container["customStates"]![0]!["status"] = "withdrawn";
            else container["customStates"]![0]!["state"] = "danger";
        }
        context = context with { CurrentMenuRoots = roots };
        Assert.Null(new SpiritualDiagnosisSourceData(context).Admit(wound, "diagnose_tissue").Data);
        Assert.Equal(frozen, Assert.IsType<SpiritualDiagnosisAdmission>(source.Admit(wound, "diagnose_tissue").Data).RequirementBindings.ToJsonString());
        Assert.Equal(work, source.CanonicalResourceWork); // Actual composer/source callbacks do not repeat inside Admit.
    }

    [Theory]
    [InlineData("duplicate")][InlineData("confusable")][InlineData("alias")][InlineData("unknown")]
    [InlineData("version")][InlineData("actor")][InlineData("player_id")][InlineData("kind_alias")]
    public void RecognizedSceneRowsRejectMalformedOrAmbiguousAuthority(string fault)
    {
        var row = new JsonObject { ["kind"] = "spiritual_diagnosis_consent", ["schemaVersion"] = 1,
            ["consentRef"] = "consent_current", ["displayName"] = "Согласие на осмотр", ["providerKind"] = "player_soul", ["providerId"] = "player_soul",
            ["targetKind"] = "player_soul", ["targetId"] = "player_soul", ["status"] = "granted" };
        var rows = new JsonArray(row);
        if (fault == "duplicate") rows.Add(row.DeepClone());
        else if (fault == "confusable") { var other = row.DeepClone(); other["consentRef"] = "CONSENT_CURRENT"; rows.Add(other); }
        else if (fault == "alias") row["ConsentRef"] = "consent_current";
        else if (fault == "unknown") row["permission"] = true;
        else if (fault == "version") row["schemaVersion"] = "1";
        else if (fault == "actor") row["targetKind"] = "npc";
        else if (fault == "player_id") row["providerId"] = "other_player";
        else row["kind"] = "SPIRITUAL_DIAGNOSIS_CONSENT";
        Assert.False(SpiritualDiagnosisSceneAuthorityContract.Parse(System.Text.Json.JsonSerializer.SerializeToElement(rows), "current.customStates").IsValid);
        // Mortal grammar never reinterprets the spiritual reserved kinds as a Mortal grant.
        Assert.Empty(MortalWoundTreatmentSceneAuthorityContract.Parse(System.Text.Json.JsonSerializer.SerializeToElement(rows), "mortal.customStates").Consents);
        var container = new JsonObject { ["customStates"] = rows.DeepClone() };
        var root = new JsonObject { ["halls"] = new JsonArray(container) };
        Assert.NotEmpty(SpiritualDiagnosisSceneAuthorityContract.ValidateContainers(System.Text.Json.JsonSerializer.SerializeToElement(root), "current", shining: true));
    }

    [Theory]
    [InlineData("chaos_sea", "item_quantity")][InlineData("shining_abode", "item_quantity")]
    [InlineData("chaos_sea", "resource_quantity")][InlineData("shining_abode", "resource_quantity")]
    [InlineData("chaos_sea", "consent")][InlineData("shining_abode", "consent")]
    [InlineData("chaos_sea", "facility")][InlineData("shining_abode", "facility")]
    [InlineData("chaos_sea", "location")][InlineData("shining_abode", "location")]
    [InlineData("chaos_sea", "quest_state")][InlineData("shining_abode", "quest_state")]
    [InlineData("chaos_sea", "effect_state")][InlineData("shining_abode", "effect_state")]
    [InlineData("chaos_sea", "environment")][InlineData("shining_abode", "environment")]
    public async Task AmbiguousOrForeignCanonicalIdentityNeverSuppliesRequirement(string realm, string kind)
    {
        var context = await ContextAsync(realm, kind);
        var wound = Assert.Single(context.Catalog.Occurrences).Wound;
        Assert.NotNull(new SpiritualDiagnosisSourceData(context).Admit(wound, "diagnose_tissue").Data);
        if (kind == "item_quantity")
        { var rows = context.Soul["soulRelics"]!["stored"]!.AsArray(); rows.Add(rows[0]!.DeepClone()); }
        else if (kind == "quest_state")
        { var rows = context.CurrentMenuRoots[SpiritualDiagnosisCanonicalSources.QuestsPath]["quests"]!.AsArray(); var other = rows[0]!.DeepClone(); other["questId"] = QuestId.ToUpperInvariant(); rows.Add(other); }
        else if (kind == "resource_quantity")
            context.Input.Carriers.AfterlifeProfiles!["profiles"]![0]![AfterlifeEntityProfileState.ResourceOwnerBindingsProperty]![0]!["realm"] = "mortal_world";
        else if (kind == "effect_state")
        {
            context.Input.Carriers.AfterlifeProfiles!["profiles"]![0]!["activeEffects"]![0]!["target"]!["targetId"] = "foreign_actor";
            var mechanics = EffectMechanicsSnapshot.Build(new(new(null, null, null, null, context.Input.Carriers.AfterlifeProfiles, null), context.CurrentMenuRoots[EffectIdentityState.StatePath]));
            context = context with { Input = context.Input with { Effects = mechanics } };
        }
        else
        {
            var root = context.CurrentMenuRoots[realm == "chaos_sea" ? SpiritualDiagnosisCanonicalSources.GuardiansPath : ShiningAbodeState.StatePath];
            var container = realm == "chaos_sea" ? root["guardians"]![0]!["abode"]! : root["halls"]![0]!;
            if (kind == "location")
            { if (realm == "chaos_sea") root["chaosSeaNavigation"]!["CurrentAbodeId"] = LocationId; else root["CurrentHallId"] = LocationId; }
            else if (kind == "consent") container["customStates"]![0]!["targetId"] = "foreign_actor";
            else { var rows = container["customStates"]!.AsArray(); rows.Add(rows[0]!.DeepClone()); }
        }
        Assert.Null(new SpiritualDiagnosisSourceData(context).Admit(wound, "diagnose_tissue").Data);
    }

    [Fact]
    public async Task CurrentShiningRosterSuppliesResidentMembershipAndConfusableContainerNeverGrants()
    {
        var context = await ContextAsync("shining_abode", "facility");
        var profiles = context.Input.Carriers.AfterlifeProfiles!;
        var resident = profiles["profiles"]![0]!.DeepClone().AsObject();
        resident["actorType"] = "resident"; resident["actorId"] = "resident_diagnosis"; resident["activeWounds"] = new JsonArray();
        resident.Remove(AfterlifeEntityProfileState.ResourceOwnerBindingsProperty);
        profiles["profiles"]!.AsArray().Add(resident);
        var roots = context.CurrentMenuRoots.ToDictionary(pair => pair.Key, pair => (JsonObject?)pair.Value, StringComparer.Ordinal);
        roots[GuardianAbodeResidentState.StatePath] = new JsonObject { ["entries"] = new JsonArray(new JsonObject
            { ["residentId"] = "resident_diagnosis", ["isPresent"] = true, ["hallId"] = LocationId }) };
        var shining = roots[ShiningAbodeState.StatePath]!;
        Assert.Contains("resident_diagnosis", ShiningCurrentHallContract.RequireCurrentScope(shining, roots[GuardianAbodeResidentState.StatePath]).LocalActorIds);
        var provider = new WoundOwnerCoordinate("shining_abode", "player_soul", "player_soul", WoundCarrierCatalog.AfterlifeProfilesPath);
        var target = new WoundOwnerCoordinate("shining_abode", "resident", "resident_diagnosis", WoundCarrierCatalog.AfterlifeProfilesPath);
        var requirement = new MortalWoundFacilityRequirement("facility_current");
        Assert.NotNull(new SpiritualDiagnosisCanonicalSources("shining_abode", ["facility"], roots, null).Prove(requirement, provider, target));
        var hall = shining["halls"]![0]!;
        hall["CustomStates"] = hall["customStates"]!.DeepClone();
        Assert.Null(new SpiritualDiagnosisCanonicalSources("shining_abode", ["facility"], roots, null).Prove(requirement, provider, target));
        Assert.NotEmpty(SpiritualDiagnosisSceneAuthorityContract.ValidateContainers(System.Text.Json.JsonSerializer.SerializeToElement(shining), "current", true));
    }

    [Theory]
    [InlineData("source_capability", "provider")][InlineData("source_capability", "target")]
    [InlineData("skill_tier", "provider")][InlineData("skill_tier", "target")]
    public async Task MixedCanonicalPathKeepsEveryStableStabilizationGuard(string kind, string role)
    {
        var context = await ContextAsync("shining_abode", "facility");
        var node = JsonNode.Parse(WoundMaterializationContract.SerializeCanonical(Assert.Single(context.Catalog.Occurrences).Wound))!.AsObject();
        var requirement = new JsonObject { ["kind"] = kind, ["capabilityRef"] = "spiritual_stabilization", ["actorRole"] = role };
        if (kind == "skill_tier") requirement["minimumTier"] = 2;
        node["treatment"]!["diagnosisPaths"]![0]!["requirements"]!.AsArray().Add(requirement);
        var input = WoundPlayerProjectionTestData.InputInRealm("shining_abode", node);
        input.Carriers.AfterlifeProfiles!["profiles"]![0]!["standardArts"] = context.Input.Carriers.AfterlifeProfiles!["profiles"]![0]!["standardArts"]!.DeepClone();
        input.Carriers.AfterlifeProfiles!["profiles"]![0]!["standardArts"]!["spiritual_stabilization"] = 2;
        context.Soul["afterlifeCombatProfile"]!["artTiers"]!["spiritual_stabilization"] = 2;
        var catalog = WoundCarrierCatalog.Build(input.Carriers); Assert.Empty(catalog.Issues);
        var roots = context.CurrentMenuRoots.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        roots[WoundCarrierCatalog.AfterlifeProfilesPath] = input.Carriers.AfterlifeProfiles;
        context = context with { Input = input, Catalog = catalog, CurrentMenuRoots = roots };
        var wound = Assert.Single(catalog.Occurrences).Wound;
        Assert.NotNull(new SpiritualDiagnosisSourceData(context).Admit(wound, "diagnose_tissue").Data);
        var stable = SpiritualDiagnosisSourceData.ReadStableRequirementBindings(catalog, input.Carriers.AfterlifeProfiles, context.Soul,
            "shining_abode", wound, "diagnose_tissue");
        Assert.Equal(2, Assert.Single(stable)!["tier"]!.GetValue<int>());
        input.Carriers.AfterlifeProfiles["profiles"]![0]!["standardArts"]!.AsObject().Remove("spiritual_stabilization");
        context.Soul["afterlifeCombatProfile"]!["artTiers"]!.AsObject().Remove("spiritual_stabilization");
        Assert.Throws<InvalidDataException>(() => SpiritualDiagnosisSourceData.ReadStableRequirementBindings(catalog,
            input.Carriers.AfterlifeProfiles, context.Soul, "shining_abode", wound, "diagnose_tissue"));
    }

    private static async Task<WoundPlayerReadContext> AddResourcesAsync(WoundPlayerReadContext context,
        Dictionary<string, JsonObject> roots, string realm)
    {
        var pristine = ResourceBootstrapStateBuilder.BuildPristine();
        Assert.True(pristine.IsValid);
        var bootstrap = await CanonicalResourceQuartetTransaction.ComposeExplicitBootstrapAsync(
            pristine.Definitions!, pristine.State!, pristine.History!,
            new AfterlifeOwnerResourceAcceptedState(Profiles: context.Input.Carriers.AfterlifeProfiles, SoulState: context.Soul),
            path => Task.FromResult(roots.GetValueOrDefault(path)?.ToJsonString()));
        Assert.True(bootstrap.IsValid, string.Join(";", bootstrap.Issues));
        var state = Assert.IsType<ResourceStateLedger>(bootstrap.StateAfterImage);
        var history = Assert.IsType<ResourceHistoryState>(bootstrap.HistoryAfterImage);
        foreach (var pair in bootstrap.OwnerAfterImages) roots[pair.Key] = pair.Value.DeepClone().AsObject();
        roots[ResourceMaterializationContract.DefinitionsPath] = JsonNode.Parse(bootstrap.Definitions.ToCanonicalJson())!.AsObject();
        roots[ResourceMaterializationContract.StatePath] = JsonNode.Parse(state.ToCanonicalJson())!.AsObject();
        roots[ResourceMaterializationContract.HistoryPath] = JsonNode.Parse(history.ToCanonicalJson())!.AsObject();
        roots[CanonicalResourceOwnerAuthorityComposer.AuthorityPath] = JsonNode.Parse(bootstrap.CanonicalAuthorityJson!)!.AsObject();
        var authority = await CanonicalResourceOwnerAuthorityComposer.ComposeAsync(bootstrap.Definitions,
            path => Task.FromResult(roots.GetValueOrDefault(path)?.ToJsonString()), state,
            history, CanonicalResourceOwnerAuthorityPurpose.ExistingSessionValidation);
        Assert.True(authority.IsValid, string.Join(";", authority.Issues));
        Assert.Empty(history.ValidateStateAgreement(state));
        var resource = Assert.Single(state.Entries, entry => entry.Coordinate.Realm == realm &&
            entry.Coordinate.OwnerKind == ResourceOwnerKind.AfterlifeActor && entry.Coordinate.ResourceKey == "spiritual_action_points");
        Assert.True(resource.Current >= 1); Assert.Equal(ResourceLifecycleState.Active, resource.State);
        var carriers = context.Input.Carriers with { AfterlifeProfiles = roots[WoundCarrierCatalog.AfterlifeProfilesPath] };
        var input = context.Input with { Carriers = carriers, Resources = bootstrap.Definitions };
        var catalog = WoundCarrierCatalog.Build(carriers); Assert.Empty(catalog.Issues);
        return context with { Input = input, Catalog = catalog, Soul = roots["game_state/meta/soul_state.json"], Projection = WoundPlayerProjection.Build(input, catalog) };
    }

    private static WoundPlayerReadContext AddActualEffects(WoundPlayerReadContext context,
        Dictionary<string, JsonObject> roots, string realm)
    {
        var wound = Assert.Single(context.Catalog.Occurrences).Wound;
        var effects = new List<JsonObject>();
        foreach (var binding in wound.Consequences.OwnedEffectSources.RootBindings)
        {
            var definition = JsonNode.Parse(wound.Consequences.OwnedEffectSources.Definitions.Single(row =>
                row.GetProperty("definitionKey").GetString() == binding.DefinitionKey).GetRawText())!.AsObject();
            var profile = wound.Consequences.Entries.Single(row => row.EffectId == binding.EffectId).ProfileKey;
            var effect = EffectMaterializationTestFixture.CreateSpiritualWoundCanonicalEffect(profile, "player", realm, wound.WoundId);
            effect["effectId"] = binding.EffectId; effect["target"]!["targetId"] = "player_soul";
            effect["source"]!["definitionKey"] = binding.DefinitionKey;
            effect["display"] = definition["display"]!.DeepClone(); effect["display"]!["sourceLabel"] = wound.Display.Name;
            effect["components"] = definition["components"]!.DeepClone(); effect["triggers"] = definition["triggers"]!.DeepClone();
            effect["removal"] = definition["removal"]!.DeepClone(); effect["stacking"]!["stackKey"] = definition["stacking"]!["stackKey"]!.DeepClone();
            effect["chronology"]!["lastTransitionId"] = "seed_effect_" + effects.Count;
            effect["chronology"]!["createdEventRef"] = "seed_event_" + binding.EffectId; effects.Add(effect);
        }
        roots[WoundCarrierCatalog.AfterlifeProfilesPath]["profiles"]![0]!["activeEffects"] = new JsonArray(effects.Select(e => (JsonNode)e.DeepClone()).ToArray());
        var index = EffectMaterializationTestFixture.CreateIdentityIndex(effects.ToArray());
        foreach (var entry in index["entries"]!.AsArray().OfType<JsonObject>())
        {
            entry["owner"]!["carrierPath"] = EffectCarrierCatalog.AfterlifeProfilesPath;
            entry["transitions"]![0]!["transitionId"] = effects.Single(e => e["effectId"]!.GetValue<string>() == entry["effectId"]!.GetValue<string>())["chronology"]!["lastTransitionId"]!.DeepClone();
            entry["transitions"]![0]!["eventRef"] = effects.Single(e => e["effectId"]!.GetValue<string>() == entry["effectId"]!.GetValue<string>())["chronology"]!["createdEventRef"]!.DeepClone();
        }
        roots[EffectIdentityState.StatePath] = index;
        var mechanics = EffectMechanicsSnapshot.Build(new(new(null, null, null, null, roots[WoundCarrierCatalog.AfterlifeProfilesPath], null), index));
        Assert.True(mechanics.IsAccepted, string.Join(";", mechanics.Issues));
        Assert.Contains(mechanics.Effects, effect => effect.EffectId == "effect_spiritual_roll" && effect.Realm == realm && effect.TargetId == "player_soul");
        var input = context.Input with { Effects = mechanics };
        return context with { Input = input, Projection = WoundPlayerProjection.Build(input, context.Catalog) };
    }

    private static void AddLocality(Dictionary<string, JsonObject> roots, string realm, string kind)
    {
        var row = kind switch
        {
            "consent" => new JsonObject { ["kind"] = "spiritual_diagnosis_consent", ["schemaVersion"] = 1,
                ["consentRef"] = "consent_current", ["displayName"] = "Согласие на осмотр", ["providerKind"] = "player_soul", ["providerId"] = "player_soul",
                ["targetKind"] = "player_soul", ["targetId"] = "player_soul", ["status"] = "granted" },
            "facility" => new JsonObject { ["kind"] = "spiritual_diagnosis_facility", ["schemaVersion"] = 1,
                ["facilityId"] = "facility_current", ["displayName"] = "Место осмотра", ["available"] = true },
            "environment" => new JsonObject { ["kind"] = "spiritual_diagnosis_environment", ["schemaVersion"] = 1,
                ["environmentId"] = "environment_current", ["displayName"] = "Спокойная среда", ["state"] = "safe" },
            _ => null
        };
        var scene = new JsonArray(); if (row is not null) scene.Add(row);
        LocalInteractionScope scope;
        if (realm == "chaos_sea")
        {
            roots["game_state/meta/guardians.json"] = new JsonObject
            {
                ["chaosSeaNavigation"] = new JsonObject { ["currentAbodeId"] = LocationId, ["currentGuardianId"] = "guardian_diagnosis" },
                ["activeGuardianId"] = "guardian_diagnosis",
                ["guardians"] = new JsonArray(new JsonObject { ["guardianId"] = "guardian_diagnosis",
                    ["abode"] = new JsonObject { ["abodeId"] = LocationId, ["name"] = "Обитель осмотра", ["customStates"] = scene } })
            };
            scope = LocalInteractionScopeService.ResolveChaosSeaScope(roots["game_state/meta/guardians.json"], null, []);
        }
        else
        {
            roots[ShiningAbodeState.StatePath] = new JsonObject { ["availability"] = "active", ["currentHallId"] = LocationId,
                ["halls"] = new JsonArray(new JsonObject { ["hallId"] = LocationId, ["hallName"] = "Зал осмотра", ["customStates"] = scene }) };
            Assert.Null(ShiningCurrentHallContract.Validate(roots[ShiningAbodeState.StatePath]));
            scope = ShiningCurrentHallContract.RequireCurrentScope(roots[ShiningAbodeState.StatePath]);
        }
        Assert.True(scope.IsResolved); Assert.Equal(LocationId, scope.LocationId);
        if (row is not null) Assert.Equal(1, row["schemaVersion"]!.GetValue<int>());
    }

    private static string Snapshot(WoundPlayerReadContext context) => string.Join("\n", context.CurrentMenuRoots
        .OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => pair.Key + "\n" + pair.Value.ToJsonString()));

    private static JsonObject Requirement(string kind) => kind switch
    {
        "item_quantity" => new() { ["kind"] = kind, ["itemRef"] = RelicId, ["quantity"] = 1, ["ownerRole"] = "provider" },
        "resource_quantity" => new() { ["kind"] = kind, ["resourceRef"] = "spiritual_action_points", ["quantity"] = 1, ["ownerRole"] = "provider" },
        "consent" => new() { ["kind"] = kind, ["consentRef"] = "consent_current", ["providerRef"] = "player_soul", ["targetRef"] = "player_soul" },
        "facility" => new() { ["kind"] = kind, ["facilityRef"] = "facility_current" },
        "location" => new() { ["kind"] = kind, ["locationRef"] = LocationId, ["targetRole"] = "target" },
        "quest_state" => new() { ["kind"] = kind, ["questRef"] = QuestId, ["requiredState"] = "active" },
        "effect_state" => new() { ["kind"] = kind, ["effectRef"] = "effect_spiritual_roll", ["requiredState"] = "active", ["targetRole"] = "target" },
        "environment" => new() { ["kind"] = kind, ["environmentRef"] = "environment_current", ["requiredState"] = "safe" },
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };
}
