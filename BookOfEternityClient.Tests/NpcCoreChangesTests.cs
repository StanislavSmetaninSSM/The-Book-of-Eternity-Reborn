using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.IO;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class NpcCoreChangesTests : IDisposable
{
    private const string NpcCorePath = "game_state/npcs/npc_core.json";
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly ValidationService _validator;

    public NpcCoreChangesTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-npc-core-changes-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
        _validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
    }

    [Fact]
    public async Task ValidateGameStateAsync_ValidNpcCoreChanges_IsRecognized()
    {
        var fixture = await WriteFixtureAsync((root, actor, _) =>
            root["NPCCoreChanges"] = new JsonArray(BuildProfileChange(actor)));

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            issue.FilePath.Contains("NPCCoreChanges", StringComparison.Ordinal) ||
            issue.Code == "npc_contract_unknown_top_level_key");
        Assert.Equal(fixture.ActorId, fixture.CurrentActor["NPCId"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("Rules/Block_19.txt")]
    [InlineData("BookOfEternityClient/game_master_daemon.ps1")]
    public async Task AuthoritativeNpcCoreChangesTemplate_PassesProductionValidationAndReduction(
        string repositoryPath)
    {
        var source = File.ReadAllText(Path.Combine(
            TestRepoPaths.RepoRoot,
            repositoryPath.Replace('/', Path.DirectorySeparatorChar)));
        var template = JsonNode.Parse(ExtractNpcCoreChangesTemplate(source))!.AsObject();
        var expectedWorldview = template["NPCCoreChanges"]![0]!["profile"]!["worldview"]!
            .GetValue<string>();
        var fixture = await WriteFixtureAsync((root, actor, _) =>
        {
            var commandRoot = template.DeepClone().AsObject();
            commandRoot["NPCCoreChanges"]![0]!["NPCId"] = actor["NPCId"]!.DeepClone();
            root["NPCCoreChanges"] = commandRoot["NPCCoreChanges"]!.DeepClone();
        });

        var issues = await InvokePreNormalizationValidationAsync();

        Assert.DoesNotContain(issues, issue =>
            issue.Code?.StartsWith("npc_core_changes_", StringComparison.Ordinal) == true);

        var normalizer = new CanonicalStateNormalizer(
            _fs,
            NullLogger<CanonicalStateNormalizer>.Instance);
        await normalizer.NormalizeAccumulatedStateAsync(new Dictionary<string, string>
        {
            [NpcCorePath] = $"game_state/control/pending_turn_snapshot/{NpcCorePath}"
        });

        var normalized = JsonNode.Parse((await _fs.ReadFileAsync(NpcCorePath))!)!.AsObject();
        Assert.False(normalized.ContainsKey("NPCCoreChanges"));
        var actor = Assert.Single(normalized["NPCsInScene"]!.AsArray().OfType<JsonObject>());
        Assert.Equal(fixture.ActorId, actor["NPCId"]!.GetValue<string>());
        Assert.Equal(expectedWorldview, actor["worldview"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("{\"NPCsInScene\":[}", "malformed")]
    [InlineData("[]", "non-object root")]
    public async Task ValidatePreNormalizationNpcCoreChanges_InvalidCurrentAuthority_IsStructuredError(
        string currentJson,
        string expectedActual)
    {
        await WriteFixtureAsync();
        await _fs.WriteFileAtomicAsync(NpcCorePath, currentJson);

        var issues = await InvokePreNormalizationValidationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "npc_core_changes_invalid_json" &&
            issue.Severity == IssueSeverity.Error &&
            issue.Actual == expectedActual);
    }

    [Fact]
    public async Task ValidatePreNormalizationNpcCoreChanges_DuplicateCurrentMember_IsStructuredError()
    {
        var fixture = await WriteFixtureAsync();
        var currentJson = fixture.CurrentRoot.ToJsonString();
        currentJson = currentJson[..^1] +
                      ",\"NPCCoreChanges\":[],\"NPCCoreChanges\":[]}";
        await _fs.WriteFileAtomicAsync(NpcCorePath, currentJson);

        var issues = await InvokePreNormalizationValidationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "npc_core_changes_duplicate_property" &&
            issue.Severity == IssueSeverity.Error &&
            issue.FilePath.Contains("NPCCoreChanges", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ValidatePreNormalizationNpcCoreChanges_DuplicateNestedCurrentMember_IsStructuredError()
    {
        await WriteFixtureAsync((root, actor, _) =>
        {
            var card = BuildLockedFateCardWithActiveSkill("fate_duplicate_nested_member");
            root["NPCCoreChanges"] = new JsonArray(new JsonObject
            {
                ["NPCId"] = actor["NPCId"]!.GetValue<string>(),
                ["reason"] = "Событие открыло новую линию судьбы.",
                ["fateCardsToAdd"] = new JsonArray(card)
            });
        });
        var currentJson = (await _fs.ReadFileAsync(NpcCorePath))!;
        currentJson = currentJson.Replace(
            "\"effectType\":\"Damage\"",
            "\"effectType\":\"Damage\",\"effectType\":\"Heal\"",
            StringComparison.Ordinal);
        await _fs.WriteFileAtomicAsync(NpcCorePath, currentJson);

        var issues = await InvokePreNormalizationValidationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "npc_core_changes_duplicate_property" &&
            issue.FilePath.EndsWith(".effectType", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ValidatePreNormalizationNpcCoreChanges_DuplicatePreTurnMember_IsStructuredError()
    {
        await WriteFixtureAsync((root, actor, _) =>
            root["NPCCoreChanges"] = new JsonArray(BuildProfileChange(actor)));
        var snapshotPath = $"game_state/control/pending_turn_snapshot/{NpcCorePath}";
        var preTurnJson = (await _fs.ReadFileAsync(snapshotPath))!;
        preTurnJson = preTurnJson[..^1] + ",\"NPCsInScene\":[]}";
        await RewriteNpcCoreSnapshotAuthorityAsync(snapshotPath, preTurnJson);

        var issues = await InvokePreNormalizationValidationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "npc_core_changes_pre_turn_authority_unavailable" &&
            issue.Severity == IssueSeverity.Error &&
            issue.Actual!.Contains("duplicate", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("protected")]
    [InlineData("nested_unknown")]
    public async Task ValidateGameStateAsync_NpcCoreChanges_RejectsProtectedOrUnknownMembers(string mutation)
    {
        await WriteFixtureAsync((root, actor, preTurnActor) =>
        {
            var change = BuildProfileChange(actor);
            if (mutation == "protected")
                change["inventory"] = new JsonArray();
            else
                change["profile"]!["name"] = "Недопустимая смена имени";
            root["NPCCoreChanges"] = new JsonArray(change);
        });

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue => issue.Code == "npc_core_changes_unknown_member");
    }

    [Theory]
    [InlineData("missing_identity", "npc_core_changes_invalid_identity")]
    [InlineData("identity_alias", "npc_core_changes_unknown_member")]
    [InlineData("same_turn_identity", "npc_core_changes_unknown_member")]
    [InlineData("unknown_identity", "npc_core_changes_target_not_existing")]
    [InlineData("initial_id_target", "npc_core_changes_target_not_existing")]
    [InlineData("name_target", "npc_core_changes_target_not_existing")]
    [InlineData("case_variant_identity", "npc_core_changes_target_not_exact")]
    [InlineData("blank_reason", "npc_core_changes_reason_required")]
    [InlineData("empty_mutation", "npc_core_changes_empty_mutation")]
    public async Task ValidatePreNormalizationNpcCoreChanges_RejectsInvalidIdentityOrEnvelope(
        string mutation,
        string expectedCode)
    {
        await WriteFixtureAsync((root, actor, preTurnActor) =>
        {
            var change = BuildProfileChange(actor);
            switch (mutation)
            {
                case "missing_identity":
                    change.Remove("NPCId");
                    break;
                case "identity_alias":
                    change["npcId"] = change["NPCId"]!.DeepClone();
                    change.Remove("NPCId");
                    break;
                case "same_turn_identity":
                    change["initialId"] = change["NPCId"]!.DeepClone();
                    change.Remove("NPCId");
                    break;
                case "unknown_identity":
                    change["NPCId"] = "npc_not_in_validated_pre_turn_state";
                    break;
                case "initial_id_target":
                    actor["initialId"] = "npc_legacy_initial_alias";
                    preTurnActor["initialId"] = "npc_legacy_initial_alias";
                    change["NPCId"] = "npc_legacy_initial_alias";
                    break;
                case "name_target":
                    change["NPCId"] = actor["name"]!.DeepClone();
                    break;
                case "case_variant_identity":
                    change["NPCId"] = actor["NPCId"]!.GetValue<string>().ToUpperInvariant();
                    break;
                case "blank_reason":
                    change["reason"] = "   ";
                    break;
                case "empty_mutation":
                    change.Remove("profile");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null);
            }

            root["NPCCoreChanges"] = new JsonArray(change);
        });

        var issues = await InvokePreNormalizationValidationAsync();

        Assert.Contains(issues, issue => issue.Code == expectedCode);
    }

    [Fact]
    public async Task ValidatePreNormalizationNpcCoreChanges_RejectsNonArrayCommandRoot()
    {
        await WriteFixtureAsync((root, _, _) =>
            root["NPCCoreChanges"] = new JsonObject { ["NPCId"] = "not-an-array" });

        var issues = await InvokePreNormalizationValidationAsync();

        Assert.Contains(issues, issue => issue.Code == "npc_core_changes_invalid_shape");
    }

    [Fact]
    public async Task ValidateGameStateAsync_CaseVariantNpcCoreChangesTopLevel_IsRejected()
    {
        await WriteFixtureAsync((root, actor, _) =>
            root["npcCoreChanges"] = new JsonArray(BuildProfileChange(actor)));

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue => issue.Code == "npc_core_changes_invalid_top_level_name");
    }

    [Theory]
    [InlineData("unknown_key", "npc_core_changes_characteristic_not_authorized")]
    [InlineData("non_numeric", "npc_core_changes_characteristic_value_invalid")]
    public async Task ValidatePreNormalizationNpcCoreChanges_RejectsInvalidCharacteristicMutation(
        string mutation,
        string expectedCode)
    {
        await WriteFixtureAsync((root, actor, _) =>
        {
            var characteristicValues = new JsonObject
            {
                [mutation == "unknown_key" ? "invented_unowned_stat" : "intelligence"] =
                    mutation == "non_numeric" ? JsonValue.Create("seven") : JsonValue.Create(7)
            };
            root["NPCCoreChanges"] = new JsonArray(new JsonObject
            {
                ["NPCId"] = actor["NPCId"]!.GetValue<string>(),
                ["reason"] = "Опыт изменил измеримую способность персонажа.",
                ["characteristicValues"] = characteristicValues
            });
        });

        var issues = await InvokePreNormalizationValidationAsync();

        Assert.Contains(issues, issue => issue.Code == expectedCode);
    }

    [Theory]
    [InlineData("missing_tuple")]
    [InlineData("negative_tuple")]
    [InlineData("transition_without_sync")]
    public async Task ValidatePreNormalizationNpcCoreChanges_RejectsIncoherentProgression(string mutation)
    {
        await WriteFixtureAsync((root, actor, _) =>
        {
            var progression = new JsonObject
            {
                ["level"] = 3,
                ["experience"] = 20,
                ["experienceForNextLevel"] = 200
            };
            if (mutation == "missing_tuple")
                progression.Remove("experienceForNextLevel");
            else if (mutation == "negative_tuple")
                progression["experience"] = -1;
            else
                progression["progressionType"] = "Companion";

            root["NPCCoreChanges"] = new JsonArray(new JsonObject
            {
                ["NPCId"] = actor["NPCId"]!.GetValue<string>(),
                ["reason"] = "Пережитый ход изменил путь развития персонажа.",
                ["progression"] = progression
            });
        });

        var issues = await InvokePreNormalizationValidationAsync();

        Assert.Contains(issues, issue => issue.Code == "npc_core_changes_progression_invalid");
    }

    [Theory]
    [InlineData("missing_pair")]
    [InlineData("both_targets")]
    [InlineData("unknown_permanent")]
    [InlineData("unknown_same_turn")]
    public async Task ValidatePreNormalizationNpcCoreChanges_RejectsInvalidLocationMutation(string mutation)
    {
        await WriteFixtureAsync((root, actor, _) =>
        {
            var location = new JsonObject
            {
                ["currentLocationId"] = actor["currentLocationId"]!.GetValue<string>(),
                ["initialLocationId"] = null
            };
            switch (mutation)
            {
                case "missing_pair":
                    location.Remove("initialLocationId");
                    break;
                case "both_targets":
                    location["initialLocationId"] = "location_same_turn_unknown";
                    break;
                case "unknown_permanent":
                    location["currentLocationId"] = "location_permanent_unknown";
                    break;
                case "unknown_same_turn":
                    location["currentLocationId"] = null;
                    location["initialLocationId"] = "location_same_turn_unknown";
                    break;
            }

            root["NPCCoreChanges"] = new JsonArray(new JsonObject
            {
                ["NPCId"] = actor["NPCId"]!.GetValue<string>(),
                ["reason"] = "Персонаж переместился после завершения сцены.",
                ["location"] = location
            });
        });

        var issues = await InvokePreNormalizationValidationAsync();

        Assert.Contains(issues, issue => issue.Code == "npc_core_changes_location_invalid");
    }

    [Fact]
    public async Task ValidatePreNormalizationNpcCoreChanges_RejectsUnknownFactionIdentity()
    {
        await WriteFixtureAsync((root, actor, _) =>
            root["NPCCoreChanges"] = new JsonArray(new JsonObject
            {
                ["NPCId"] = actor["NPCId"]!.GetValue<string>(),
                ["reason"] = "Персонаж принял обязанности в новой организации.",
                ["factionAffiliationsToUpsert"] = new JsonArray(new JsonObject
                {
                    ["factionId"] = "faction_unknown",
                    ["factionName"] = "Неизвестная фракция",
                    ["rank"] = "Советник",
                    ["branch"] = null,
                    ["membershipStatus"] = "Active"
                })
            }));

        var issues = await InvokePreNormalizationValidationAsync();

        Assert.Contains(issues, issue => issue.Code == "npc_core_changes_faction_invalid");
    }

    [Theory]
    [InlineData("add_unlocked")]
    [InlineData("add_existing_id")]
    [InlineData("remove_unknown")]
    [InlineData("remove_unlocked")]
    public async Task ValidatePreNormalizationNpcCoreChanges_RejectsUnsafeFateCardMutation(string mutation)
    {
        await WriteFixtureAsync((root, actor, preTurnActor) =>
        {
            var existingCard = BuildLockedFateCard("fate_existing");
            if (mutation == "remove_unlocked")
                existingCard["isUnlocked"] = true;
            actor["fateCards"] = new JsonArray(existingCard.DeepClone());
            preTurnActor["fateCards"] = new JsonArray(existingCard.DeepClone());

            var change = new JsonObject
            {
                ["NPCId"] = actor["NPCId"]!.GetValue<string>(),
                ["reason"] = "Событие изменило ещё не реализованные линии судьбы."
            };
            if (mutation.StartsWith("add_", StringComparison.Ordinal))
            {
                var card = BuildLockedFateCard(mutation == "add_existing_id" ? "fate_existing" : "fate_new");
                if (mutation == "add_unlocked")
                    card["isUnlocked"] = true;
                change["fateCardsToAdd"] = new JsonArray(card);
            }
            else
            {
                change["fateCardIdsToRemove"] = new JsonArray(
                    mutation == "remove_unknown" ? "fate_unknown" : "fate_existing");
            }

            root["NPCCoreChanges"] = new JsonArray(change);
        });

        var issues = await InvokePreNormalizationValidationAsync();

        Assert.Contains(issues, issue => issue.Code == "npc_core_changes_fate_card_invalid");
    }

    [Theory]
    [InlineData("non_english_image")]
    [InlineData("invalid_conjunction")]
    [InlineData("invalid_narrative_reward")]
    [InlineData("incomplete_tactical_trigger")]
    [InlineData("incomplete_active_skill")]
    [InlineData("incomplete_active_skill_effect")]
    public async Task ValidatePreNormalizationNpcCoreChanges_RejectsFateCardThatFailsFullNpcShape(
        string mutation)
    {
        await WriteFixtureAsync((root, actor, _) =>
        {
            var card = BuildLockedFateCard("fate_invalid_full_shape");
            switch (mutation)
            {
                case "non_english_image":
                    card["image_prompt"] = "архивист с фонарём";
                    break;
                case "invalid_conjunction":
                    card["unlockConditions"]!["conjunction"] = "XOR";
                    break;
                case "invalid_narrative_reward":
                    card["rewards"]!["otherNarrativeRewards"] = new JsonObject { ["text"] = "invalid" };
                    break;
                case "incomplete_tactical_trigger":
                    card["rewards"]!["tacticalTriggers"] = new JsonArray(new JsonObject
                    {
                        ["triggerCondition"] = "The archive is threatened."
                    });
                    break;
                case "incomplete_active_skill":
                    card["rewards"]!["newActiveSkills"] = new JsonArray(new JsonObject
                    {
                        ["skillName"] = "Archive Ward"
                    });
                    break;
                case "incomplete_active_skill_effect":
                    card["rewards"]!["newActiveSkills"] = new JsonArray(new JsonObject
                    {
                        ["skillName"] = "Archive Ward",
                        ["skillDescription"] = "Raises a brief protective seal.",
                        ["rarity"] = "Rare",
                        ["combatEffect"] = new JsonObject
                        {
                            ["isActivatedEffect"] = true,
                            ["actionName"] = "Raise Archive Ward",
                            ["effects"] = new JsonArray(new JsonObject())
                        }
                    });
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null);
            }

            root["NPCCoreChanges"] = new JsonArray(new JsonObject
            {
                ["NPCId"] = actor["NPCId"]!.GetValue<string>(),
                ["reason"] = "Событие открыло новую, но ещё не реализованную линию судьбы.",
                ["fateCardsToAdd"] = new JsonArray(card)
            });
        });

        var issues = await InvokePreNormalizationValidationAsync();

        Assert.Contains(issues, issue => issue.Code == "npc_core_changes_fate_card_invalid");
    }

    [Theory]
    [InlineData("missing_value")]
    [InlineData("unknown_effect_type")]
    [InlineData("timed_effect_without_duration")]
    [InlineData("poise_on_non_damage")]
    public async Task ValidatePreNormalizationNpcCoreChanges_RejectsFateCardThatFailsProductionCombatEffect(
        string mutation)
    {
        await WriteFixtureAsync((root, actor, _) =>
        {
            var card = BuildLockedFateCardWithActiveSkill("fate_invalid_production_effect");
            var effect = card["rewards"]!["newActiveSkills"]![0]!["combatEffect"]!["effects"]![0]!.AsObject();
            switch (mutation)
            {
                case "missing_value":
                    effect.Remove("value");
                    break;
                case "unknown_effect_type":
                    effect["effectType"] = "UnknownEffect";
                    break;
                case "timed_effect_without_duration":
                    effect["effectType"] = "Buff";
                    effect.Remove("poiseDamage");
                    effect.Remove("duration");
                    break;
                case "poise_on_non_damage":
                    effect["effectType"] = "Heal";
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null);
            }

            root["NPCCoreChanges"] = new JsonArray(new JsonObject
            {
                ["NPCId"] = actor["NPCId"]!.GetValue<string>(),
                ["reason"] = "Событие открыло новую, но ещё не реализованную боевую линию судьбы.",
                ["fateCardsToAdd"] = new JsonArray(card)
            });
        });

        var issues = await InvokePreNormalizationValidationAsync();

        Assert.Contains(issues, issue =>
            issue.Severity == IssueSeverity.Error &&
            issue.FilePath.Contains("combatEffect.effects[0]", StringComparison.Ordinal));
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_ProductionInvalidFateCard_RemainsAtomicAndUnconsumed()
    {
        var fixture = await WriteFixtureAsync((root, actor, _) =>
        {
            var card = BuildLockedFateCardWithActiveSkill("fate_invalid_atomicity");
            card["rewards"]!["newActiveSkills"]![0]!["combatEffect"]!["effects"]![0]!["effectType"] =
                "UnknownEffect";
            root["NPCCoreChanges"] = new JsonArray(new JsonObject
            {
                ["NPCId"] = actor["NPCId"]!.GetValue<string>(),
                ["reason"] = "Событие открыло новую, но ещё не реализованную боевую линию судьбы.",
                ["fateCardsToAdd"] = new JsonArray(card)
            });
        });
        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);

        await normalizer.NormalizeAccumulatedStateAsync(new Dictionary<string, string>
        {
            [NpcCorePath] = $"game_state/control/pending_turn_snapshot/{NpcCorePath}"
        });

        var root = JsonNode.Parse((await _fs.ReadFileAsync(NpcCorePath))!)!.AsObject();
        Assert.True(root.ContainsKey("NPCCoreChanges"));
        var actor = Assert.Single(root["NPCsInScene"]!.AsArray().OfType<JsonObject>());
        Assert.DoesNotContain(actor["fateCards"]!.AsArray().OfType<JsonObject>(), card =>
            card["cardId"]?.GetValue<string>() == "fate_invalid_atomicity");
        Assert.Equal(fixture.ActorId, actor["NPCId"]!.GetValue<string>());
    }

    [Fact]
    public async Task ValidatePreNormalizationNpcCoreChanges_DuplicatePreTurnFateCardIdentity_IsStructuredError()
    {
        await WriteFixtureAsync((root, actor, preTurnActor) =>
        {
            var first = BuildLockedFateCard("fate_duplicate_pre_turn");
            var second = first.DeepClone().AsObject();
            actor["fateCards"] = new JsonArray(first.DeepClone(), second.DeepClone());
            preTurnActor["fateCards"] = new JsonArray(first, second);
            root["NPCCoreChanges"] = new JsonArray(new JsonObject
            {
                ["NPCId"] = actor["NPCId"]!.GetValue<string>(),
                ["reason"] = "Событие закрыло нереализованную линию судьбы.",
                ["fateCardIdsToRemove"] = new JsonArray("fate_duplicate_pre_turn")
            });
        });

        var issues = await InvokePreNormalizationValidationAsync();

        Assert.Contains(issues, issue => issue.Code == "npc_core_changes_fate_card_invalid");
    }

    [Fact]
    public async Task ValidatePreNormalizationNpcCoreChanges_DivergentCurrentMirrors_AreRejected()
    {
        await WriteFixtureAsync((root, actor, _) =>
        {
            var divergentMirror = actor.DeepClone().AsObject();
            divergentMirror["history"] = "Противоречащая canonical копия.";
            root["UpdateNPCs"] = new JsonArray(divergentMirror);
            root["NPCCoreChanges"] = new JsonArray(BuildProfileChange(actor));
        });

        var issues = await InvokePreNormalizationValidationAsync();

        Assert.Contains(issues, issue => issue.Code == "npc_core_changes_divergent_mirrors");
    }

    [Fact]
    public async Task ValidatePreNormalizationNpcCoreChanges_HistoricalSceneCoreMutationWithoutCommand_IsRejected()
    {
        var fixture = await WriteFixtureAsync((_, actor, _) =>
            actor["worldview"] = "Прямое изменение в historical NPCsInScene запрещено.");

        var issues = await InvokePreNormalizationValidationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "npc_existing_core_direct_mutation_forbidden" &&
            issue.Actor == $"mortal_npc:{fixture.ActorId}" &&
            issue.FilePath == $"{NpcCorePath}.NPCsInScene[0]");
    }

    [Theory]
    [InlineData("profile")]
    [InlineData("location")]
    [InlineData("progression")]
    [InlineData("characteristics")]
    [InlineData("faction")]
    [InlineData("fate_cards")]
    public async Task ValidatePreNormalizationNpcCoreChanges_HistoricalSceneCoreDomainMutationWithoutCommand_IsRejected(
        string mutation)
    {
        var fixture = await WriteFixtureAsync((_, actor, _) =>
        {
            switch (mutation)
            {
                case "profile":
                    actor["history"] = "Прямая перепись прошлого.";
                    break;
                case "location":
                    actor["currentLocationId"] = "location_direct_bypass";
                    break;
                case "progression":
                    actor["experience"] = actor["experience"]!.GetValue<int>() + 1;
                    break;
                case "characteristics":
                    actor["characteristics"]!["intelligence"] = 9;
                    break;
                case "faction":
                    actor["factionAffiliations"] = new JsonArray(new JsonObject
                    {
                        ["factionId"] = "faction_direct_bypass"
                    });
                    break;
                case "fate_cards":
                    actor["fateCards"] = new JsonArray(BuildLockedFateCard("fate_direct_bypass"));
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null);
            }
        });

        var issues = await InvokePreNormalizationValidationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "npc_existing_core_direct_mutation_forbidden" &&
            issue.Actor == $"mortal_npc:{fixture.ActorId}");
    }

    [Theory]
    [InlineData("personality")]
    [InlineData("active_skills")]
    [InlineData("goals")]
    [InlineData("relationships")]
    [InlineData("journal")]
    [InlineData("activity")]
    [InlineData("equipment")]
    [InlineData("teacher")]
    [InlineData("trade")]
    [InlineData("custom_state")]
    public async Task ValidatePreNormalizationNpcCoreChanges_HistoricalActorOwnedDomainMutationWithoutCommand_IsRejected(
        string mutation)
    {
        var fixture = await WriteFixtureAsync((_, actor, _) =>
        {
            switch (mutation)
            {
                case "personality":
                    actor["personalityTraits"] = new JsonArray(new JsonObject
                    {
                        ["name"] = "Осторожность",
                        ["description"] = "Проверяет каждое свидетельство дважды.",
                        ["value"] = 8
                    });
                    break;
                case "active_skills":
                    actor["activeSkills"] = new JsonArray(new JsonObject
                    {
                        ["skillName"] = "Архивный заслон",
                        ["skillDescription"] = "Закрывает проход тяжёлой створкой.",
                        ["rarity"] = "common"
                    });
                    break;
                case "goals":
                    actor["goals"] = new JsonArray("Сохранить опись архива до рассвета.");
                    break;
                case "relationships":
                    actor["relationshipLocks"] = new JsonArray(new JsonObject
                    {
                        ["targetId"] = "player",
                        ["reason"] = "Доверие ещё не заслужено."
                    });
                    break;
                case "journal":
                    actor["thoughtJournal"] = new JsonArray("Я должен проверить печать ещё раз.");
                    break;
                case "activity":
                    actor["currentActivity"] = new JsonObject
                    {
                        ["activityId"] = "activity_archive_audit",
                        ["description"] = "Проверяет вечернюю опись."
                    };
                    break;
                case "equipment":
                    actor["equippedItems"] = new JsonObject { ["hands"] = "item_archive_key" };
                    break;
                case "teacher":
                    actor["teacherProfile"]!["canTeach"] = false;
                    break;
                case "trade":
                    actor["tradeState"] = new JsonObject
                    {
                        ["canTrade"] = true,
                        ["merchantProfile"] = "GeneralGoods"
                    };
                    break;
                case "custom_state":
                    actor["customStates"] = new JsonObject { ["archiveAlarm"] = true };
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null);
            }
        });

        var issues = await InvokePreNormalizationValidationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "npc_existing_core_direct_mutation_forbidden" &&
            issue.Actor == $"mortal_npc:{fixture.ActorId}");
    }

    [Fact]
    public async Task ValidatePreNormalizationNpcCoreChanges_UnchangedHistoricalSceneAndNewActor_AreAccepted()
    {
        var fixture = await WriteFixtureAsync((root, _, preTurnActor) =>
        {
            var newActor = preTurnActor.DeepClone().AsObject();
            newActor["NPCId"] = null;
            newActor.Remove("npcId");
            newActor["initialId"] = "npc_same_turn_new_core_control";
            newActor["name"] = "Новый свидетель";
            newActor["worldview"] = "Сначала увидеть, затем судить.";
            root["NPCsInScene"]!.AsArray().Add(newActor);
        });

        var issues = await InvokePreNormalizationValidationAsync();

        Assert.DoesNotContain(issues, issue =>
            issue.Code == "npc_existing_core_direct_mutation_forbidden" &&
            issue.Actor == $"mortal_npc:{fixture.ActorId}");
        Assert.DoesNotContain(issues, issue =>
            issue.Code == "npc_existing_core_direct_mutation_forbidden" &&
            issue.Actor == "mortal_npc:npc_same_turn_new_core_control");
    }

    [Fact]
    public async Task ValidatePreNormalizationNpcCoreChanges_UntouchedDivergentLegacyMirrors_AreAccepted()
    {
        var fixture = await WriteFixtureAsync((root, _, preTurnActor) =>
        {
            var preTurnUpdateMirror = preTurnActor.DeepClone().AsObject();
            preTurnUpdateMirror["history"] = "Отдельная историческая UpdateNPCs-копия.";
            preTurnActor.Parent!.Parent!.AsObject()["UpdateNPCs"] = new JsonArray(preTurnUpdateMirror);
            root["UpdateNPCs"] = new JsonArray(preTurnUpdateMirror.DeepClone());
        });

        var issues = await InvokePreNormalizationValidationAsync();

        Assert.DoesNotContain(issues, issue =>
            issue.Code == "npc_existing_core_direct_mutation_forbidden" &&
            issue.Actor == $"mortal_npc:{fixture.ActorId}");
    }

    [Fact]
    public async Task ValidatePreNormalizationNpcCoreChanges_DivergentLegacyMirrorSameSectionMutation_IsRejected()
    {
        var fixture = await WriteFixtureAsync((root, _, preTurnActor) =>
        {
            var preTurnUpdateMirror = preTurnActor.DeepClone().AsObject();
            preTurnUpdateMirror["history"] = "Отдельная историческая UpdateNPCs-копия.";
            preTurnActor.Parent!.Parent!.AsObject()["UpdateNPCs"] = new JsonArray(preTurnUpdateMirror);
            var currentUpdateMirror = preTurnUpdateMirror.DeepClone().AsObject();
            currentUpdateMirror["history"] = "Несанкционированное изменение той же UpdateNPCs-копии.";
            root["UpdateNPCs"] = new JsonArray(currentUpdateMirror);
        });

        var issues = await InvokePreNormalizationValidationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "npc_existing_core_direct_mutation_forbidden" &&
            issue.Actor == $"mortal_npc:{fixture.ActorId}" &&
            issue.FilePath == $"{NpcCorePath}.UpdateNPCs[0]");
    }

    [Fact]
    public async Task ValidatePreNormalizationNpcCoreChanges_EnvelopeFreeUpdateCarrierMutation_IsRejected()
    {
        var fixture = await WriteFixtureAsync((root, _, preTurnActor) =>
        {
            var updateCarrier = preTurnActor.DeepClone().AsObject();
            updateCarrier["activeSkills"] = new JsonArray(new JsonObject
            {
                ["skillName"] = "Несанкционированный навык",
                ["skillDescription"] = "Добавлен прямой заменой carrier.",
                ["rarity"] = "Common"
            });
            root["UpdateNPCs"] = new JsonArray(updateCarrier);
        });

        var issues = await InvokePreNormalizationValidationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "npc_existing_core_direct_mutation_forbidden" &&
            issue.Actor == $"mortal_npc:{fixture.ActorId}" &&
            issue.FilePath == $"{NpcCorePath}.UpdateNPCs[0]");
    }

    [Fact]
    public async Task ValidatePreNormalizationNpcCoreChanges_DuplicateCommandTarget_IsRejected()
    {
        await WriteFixtureAsync((root, actor, _) =>
        {
            var first = BuildProfileChange(actor);
            var second = BuildProfileChange(actor);
            second["profile"]!["history"] = "Вторая конкурирующая команда.";
            root["NPCCoreChanges"] = new JsonArray(first, second);
        });

        var issues = await InvokePreNormalizationValidationAsync();

        Assert.Contains(issues, issue => issue.Code == "npc_core_changes_duplicate_target");
    }

    [Theory]
    [InlineData("npcId")]
    [InlineData("id")]
    public async Task NormalizeAccumulatedStateAsync_LegacyLowercasePermanentAlias_IsAddressable(string alias)
    {
        var fixture = await WriteFixtureAsync((root, actor, preTurnActor) =>
        {
            var actorId = actor["NPCId"]!.GetValue<string>();
            actor.Remove("npcId");
            actor.Remove("id");
            actor[alias] = actorId;
            actor.Remove("NPCId");
            preTurnActor.Remove("npcId");
            preTurnActor.Remove("id");
            preTurnActor[alias] = actorId;
            preTurnActor.Remove("NPCId");
            root["NPCCoreChanges"] = new JsonArray(new JsonObject
            {
                ["NPCId"] = actorId,
                ["reason"] = "Событие изменило убеждения исторического персонажа.",
                ["profile"] = new JsonObject
                {
                    ["worldview"] = "Проверенное свидетельство важнее догадки."
                }
            });
        });
        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);

        await normalizer.NormalizeAccumulatedStateAsync(new Dictionary<string, string>
        {
            [NpcCorePath] = $"game_state/control/pending_turn_snapshot/{NpcCorePath}"
        });

        var root = JsonNode.Parse((await _fs.ReadFileAsync(NpcCorePath))!)!.AsObject();
        Assert.False(root.ContainsKey("NPCCoreChanges"));
        var actor = Assert.Single(root["NPCsInScene"]!.AsArray().OfType<JsonObject>());
        Assert.Equal("Проверенное свидетельство важнее догадки.", actor["worldview"]!.GetValue<string>());
        Assert.Equal(fixture.ActorId, actor[alias]!.GetValue<string>());
        Assert.False(actor.ContainsKey("NPCId"));
    }

    [Theory]
    [InlineData("npcId", "npc_conflicting_alias")]
    [InlineData("id", null)]
    public async Task ValidatePreNormalizationNpcCoreChanges_ConflictingPermanentAliases_AreRejected(
        string alias,
        string? conflictingValue)
    {
        var fixture = await WriteFixtureAsync((root, actor, preTurnActor) =>
        {
            var aliasValue = conflictingValue ?? actor["NPCId"]!.GetValue<string>();
            actor[alias] = aliasValue;
            preTurnActor[alias] = aliasValue;
            root["NPCCoreChanges"] = new JsonArray(BuildProfileChange(actor));
        });

        var issues = await InvokePreNormalizationValidationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "npc_core_changes_ambiguous_target" &&
            issue.Actor == $"mortal_npc:{fixture.ActorId}");
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_ValidNpcCoreChanges_AppliesToEveryMirrorAndConsumesCommand()
    {
        var fixture = await WriteFixtureAsync((root, actor, preTurnActor) =>
        {
            actor["materialization"] = BuildHistoricalMaterialization(actor["NPCId"]!.GetValue<string>());
            preTurnActor["materialization"] = actor["materialization"]!.DeepClone();
            actor["concurrentSibling"] = new JsonObject { ["keep"] = true };
            preTurnActor["concurrentSibling"] = actor["concurrentSibling"]!.DeepClone();
            actor["fateCards"] = new JsonArray(BuildLockedFateCard("fate_remove_locked"));
            preTurnActor["fateCards"] = actor["fateCards"]!.DeepClone();

            var mirror = actor.DeepClone().AsObject();
            root["UpdateNPCs"] = new JsonArray(mirror);
            root["NPCCoreChanges"] = new JsonArray(new JsonObject
            {
                ["NPCId"] = actor["NPCId"]!.GetValue<string>(),
                ["reason"] = "Наставник принимает роль постоянного спутника после спасения архива.",
                ["profile"] = new JsonObject
                {
                    ["worldview"] = "Знание следует защищать действием.",
                    ["race"] = "Человек северных архивов",
                    ["history"] = "После спасения архива наставник отправился вместе с героем."
                },
                ["location"] = new JsonObject
                {
                    ["currentLocationId"] = actor["currentLocationId"]!.GetValue<string>(),
                    ["initialLocationId"] = null
                },
                ["progression"] = new JsonObject
                {
                    ["level"] = 3,
                    ["experience"] = 20,
                    ["experienceForNextLevel"] = 200,
                    ["progressionType"] = "Companion",
                    ["lastPlayerXPValueOnSync"] = 1200
                },
                ["characteristicValues"] = new JsonObject
                {
                    ["intelligence"] = 7,
                    ["setting_defined_focus"] = 6.5
                },
                ["factionAffiliationsToUpsert"] = new JsonArray(new JsonObject
                {
                    ["factionId"] = fixturePlaceholderFactionId,
                    ["factionName"] = fixturePlaceholderFactionName,
                    ["rank"] = "Архивный советник",
                    ["branch"] = null,
                    ["membershipStatus"] = "Active"
                }),
                ["fateCardsToAdd"] = new JsonArray(BuildLockedFateCard("fate_added_locked")),
                ["fateCardIdsToRemove"] = new JsonArray("fate_remove_locked")
            });
        });
        ReplaceFixturePlaceholders(fixture.CurrentRoot, fixture.FactionId, fixture.FactionName);
        await _fs.WriteFileAtomicAsync(NpcCorePath, fixture.CurrentRoot.ToJsonString());
        await WriteSnapshotAsync(fixture);
        var backupPath = $"game_state/control/pending_turn_snapshot/{NpcCorePath}";
        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);

        await normalizer.NormalizeAccumulatedStateAsync(new Dictionary<string, string>
        {
            [NpcCorePath] = backupPath
        });

        var normalized = JsonNode.Parse((await _fs.ReadFileAsync(NpcCorePath))!)!.AsObject();
        Assert.False(normalized.ContainsKey("NPCCoreChanges"));
        var mirrors = new[] { "NPCsInScene", "UpdateNPCs" }
            .Select(section => Assert.Single(normalized[section]!.AsArray().OfType<JsonObject>()))
            .ToArray();
        Assert.All(mirrors, npc =>
        {
            Assert.Equal("Знание следует защищать действием.", npc["worldview"]!.GetValue<string>());
            Assert.Equal("Companion", npc["progressionType"]!.GetValue<string>());
            Assert.Equal(1200, npc["progressionTrackers"]!["lastPlayerXPValueOnSync"]!.GetValue<int>());
            Assert.Equal(6.5, npc["characteristics"]!["setting_defined_focus"]!.GetValue<double>());
            Assert.True(npc["concurrentSibling"]!["keep"]!.GetValue<bool>());
            Assert.NotNull(npc["materialization"]);
            var affiliation = Assert.Single(npc["factionAffiliations"]!.AsArray().OfType<JsonObject>());
            Assert.Equal(fixture.FactionId, affiliation["factionId"]!.GetValue<string>());
            var fateCard = Assert.Single(npc["fateCards"]!.AsArray().OfType<JsonObject>());
            Assert.Equal("fate_added_locked", fateCard["cardId"]!.GetValue<string>());
        });
        Assert.Equal(mirrors[0].ToJsonString(), mirrors[1].ToJsonString());
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_InvalidNpcCoreChanges_RemainsUnconsumed()
    {
        var fixture = await WriteFixtureAsync((root, actor, _) =>
        {
            var change = BuildProfileChange(actor);
            change["profile"]!["inventory"] = new JsonArray();
            root["NPCCoreChanges"] = new JsonArray(change);
        });
        var beforeWorldview = fixture.CurrentActor["worldview"]!.GetValue<string>();
        var beforeInventory = fixture.CurrentActor["inventory"]!.ToJsonString();
        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);

        await normalizer.NormalizeAccumulatedStateAsync(new Dictionary<string, string>
        {
            [NpcCorePath] = $"game_state/control/pending_turn_snapshot/{NpcCorePath}"
        });

        var normalized = JsonNode.Parse((await _fs.ReadFileAsync(NpcCorePath))!)!.AsObject();
        Assert.True(normalized.ContainsKey("NPCCoreChanges"));
        var normalizedActor = normalized["NPCsInScene"]![0]!.AsObject();
        Assert.Equal(beforeWorldview, normalizedActor["worldview"]!.GetValue<string>());
        Assert.Equal(beforeInventory, normalizedActor["inventory"]!.ToJsonString());
    }

    [Fact]
    public async Task GameResponseDeserializationAndStateDistributor_PersistNpcCoreChangesCommand()
    {
        var fixture = await WriteFixtureAsync();
        var response = JsonSerializer.Deserialize<GameResponse>($$"""
        {
          "NPCCoreChanges": [
            {
              "NPCId": "{{fixture.ActorId}}",
              "reason": "Полученный опыт изменил убеждения персонажа.",
              "profile": { "worldview": "Проверенное знание важнее догадки." }
            }
          ]
        }
        """, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed);
        Assert.NotNull(response);
        var responseProperty = typeof(GameResponse).GetProperty(
            "NPCCoreChanges",
            BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(responseProperty);
        Assert.NotNull(responseProperty.GetValue(response));
        var distributor = new StateDistributor(_fs, NullLogger<StateDistributor>.Instance);

        var modified = await distributor.DistributeAsync(response);

        Assert.Contains(NpcCorePath, modified);
        var root = JsonNode.Parse((await _fs.ReadFileAsync(NpcCorePath))!)!.AsObject();
        var command = Assert.Single(root["NPCCoreChanges"]!.AsArray().OfType<JsonObject>());
        Assert.Equal(fixture.ActorId, command["NPCId"]!.GetValue<string>());
        Assert.Equal(
            "Проверенное знание важнее догадки.",
            command["profile"]!["worldview"]!.GetValue<string>());
    }

    [Fact]
    public void GameResponseAndFileMapping_ExposeNpcCoreChangesAsNpcCoreNonCarrierCommand()
    {
        var property = typeof(GameResponse).GetProperty("NPCCoreChanges", BindingFlags.Instance | BindingFlags.Public);

        Assert.NotNull(property);
        Assert.Equal(typeof(JsonElement[]), property.PropertyType);
        Assert.True(FileMapping.FieldToFile.TryGetValue("NPCCoreChanges", out var mappedPath));
        Assert.Equal(NpcCorePath, mappedPath);
    }

    private const string fixturePlaceholderFactionId = "__fixture_faction_id__";
    private const string fixturePlaceholderFactionName = "__fixture_faction_name__";

    private static string ExtractNpcCoreChangesTemplate(string source)
    {
        const string marker = "\"NPCCoreChanges\": [";
        var markerIndex = source.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(markerIndex >= 0, "Authoritative source has no NPCCoreChanges JSON template.");
        var start = source.LastIndexOf('{', markerIndex);
        Assert.True(start >= 0, "Authoritative NPCCoreChanges template has no JSON object start.");

        var depth = 0;
        var inString = false;
        var escaped = false;
        for (var index = start; index < source.Length; index++)
        {
            var current = source[index];
            if (inString)
            {
                if (escaped)
                    escaped = false;
                else if (current == '\\')
                    escaped = true;
                else if (current == '"')
                    inString = false;
                continue;
            }

            if (current == '"')
            {
                inString = true;
                continue;
            }

            if (current == '{')
                depth++;
            else if (current == '}' && --depth == 0)
                return source[start..(index + 1)];
        }

        throw new Xunit.Sdk.XunitException("Authoritative NPCCoreChanges template is not a complete JSON object.");
    }

    private async Task<NpcCoreFixture> WriteFixtureAsync(
        Action<JsonObject, JsonObject, JsonObject>? mutateCurrent = null)
    {
        var files = MortalBootstrapStateBuilder.BuildFreshMortalBootstrapFiles(
            incarnationNumber: 1,
            turnNumber: 8,
            characterDescription: "Архивист пограничного города.",
            worldDescription: "Город архивов и сигнальных башен.",
            startingCircumstances: "Наставник предлагает первый урок чтения печатей.",
            createdAtUtc: DateTimeOffset.Parse("2026-07-22T00:00:00Z"));
        foreach (var (path, node) in files)
            await _fs.WriteFileAtomicAsync(path, node.ToJsonString());

        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Искра Архива",
          "currentRealm": "Mortal World",
          "currentIncarnation": 1
        }
        """);
        await _fs.WriteFileAtomicAsync("game_state/misc/characteristics.json", """
        {
          "intelligence": 5,
          "setting_defined_focus": 4
        }
        """);

        var preTurnRoot = files[NpcCorePath].DeepClone().AsObject();
        var preTurnActor = Assert.Single(preTurnRoot["NPCsInScene"]!.AsArray().OfType<JsonObject>());
        preTurnActor["factionAffiliations"] = new JsonArray();
        var currentRoot = preTurnRoot.DeepClone().AsObject();
        currentRoot["UpdateNPCs"] = new JsonArray();
        var currentActor = Assert.Single(currentRoot["NPCsInScene"]!.AsArray().OfType<JsonObject>());
        var actorId = currentActor["NPCId"]!.GetValue<string>();
        mutateCurrent?.Invoke(currentRoot, currentActor, preTurnActor);

        var faction = Assert.Single(files["game_state/factions/faction_core.json"]["factions"]!
            .AsArray().OfType<JsonObject>());
        var fixture = new NpcCoreFixture(
            currentRoot,
            preTurnRoot,
            currentActor,
            actorId,
            currentActor["currentLocationId"]!.GetValue<string>(),
            faction["factionId"]!.GetValue<string>(),
            faction["name"]!.GetValue<string>(),
            files);

        ReplaceFixturePlaceholders(currentRoot, fixture.FactionId, fixture.FactionName);
        await _fs.WriteFileAtomicAsync(NpcCorePath, currentRoot.ToJsonString());
        await WriteSnapshotAsync(fixture);
        return fixture;
    }

    private async Task WriteSnapshotAsync(NpcCoreFixture fixture)
    {
        var snapshotFiles = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [NpcCorePath] = fixture.PreTurnRoot.ToJsonString(),
            ["game_state/world/current_location.json"] = fixture.Files["game_state/world/current_location.json"].ToJsonString(),
            ["game_state/world/world_map.json"] = fixture.Files["game_state/world/world_map.json"].ToJsonString(),
            ["game_state/factions/faction_core.json"] = fixture.Files["game_state/factions/faction_core.json"].ToJsonString(),
            ["game_state/misc/characteristics.json"] = (await _fs.ReadFileAsync("game_state/misc/characteristics.json"))!
        };

        const string sessionId = "session_npc_core_changes_tests";
        const string requestId = "request_npc_core_changes_tests";
        const int turnNumber = 42;
        const string playerAction = "Validate bounded existing NPC core changes.";
        await _fs.WriteFileAtomicAsync("input/turn_request.json", $$"""
        {
          "sessionId": "{{sessionId}}",
          "requestId": "{{requestId}}",
          "turnNumber": {{turnNumber}},
          "playerAction": "{{playerAction}}"
        }
        """);

        var files = new JsonObject();
        var hashes = new JsonObject();
        var baselines = new JsonArray();
        foreach (var (path, json) in snapshotFiles)
        {
            var snapshotPath = $"game_state/control/pending_turn_snapshot/{path}";
            await _fs.WriteFileAtomicAsync(snapshotPath, json);
            files[path] = snapshotPath;
            hashes[path] = PendingTurnSnapshotAuthority.ComputeSha256(json);
            baselines.Add(path);
        }

        var manifest = new JsonObject
        {
            ["sessionId"] = sessionId,
            ["requestId"] = requestId,
            ["turnNumber"] = turnNumber,
            ["requestTimestamp"] = "2026-07-22T00:00:00Z",
            ["playerAction"] = playerAction,
            ["files"] = files,
            ["snapshotFileHashes"] = hashes,
            ["clientOwnedValidationHashes"] = new JsonObject(),
            ["rollbackBackups"] = new JsonObject(),
            ["rollbackBaselineFiles"] = baselines,
            ["sourceLabel"] = "NPC core changes contract test",
            ["manifestPayloadHash"] = string.Empty
        };
        manifest["manifestPayloadHash"] = PendingTurnSnapshotTestAuthority.ComputeManifestPayloadHash(manifest);
        await _fs.WriteFileAtomicAsync("game_state/control/pending_turn_snapshot.json", manifest.ToJsonString());
        await PendingTurnSnapshotTestAuthority.SyncAuthorityForCurrentManifestAsync(_fs);
    }

    private async Task<IReadOnlyList<ValidationIssue>> InvokePreNormalizationValidationAsync()
    {
        var method = typeof(ValidationService).GetMethod(
            "ValidateNpcCoreChangesBeforeNormalizationAsync",
            BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(method);
        var task = Assert.IsAssignableFrom<Task<IReadOnlyList<ValidationIssue>>>(method.Invoke(_validator, null));
        return await task;
    }

    private async Task RewriteNpcCoreSnapshotAuthorityAsync(string snapshotPath, string preTurnJson)
    {
        await _fs.WriteFileAtomicAsync(snapshotPath, preTurnJson);
        const string manifestPath = "game_state/control/pending_turn_snapshot.json";
        var manifest = JsonNode.Parse((await _fs.ReadFileAsync(manifestPath))!)!.AsObject();
        manifest["snapshotFileHashes"]![NpcCorePath] = PendingTurnSnapshotAuthority.ComputeSha256(preTurnJson);
        manifest["manifestPayloadHash"] = string.Empty;
        manifest["manifestPayloadHash"] = PendingTurnSnapshotTestAuthority.ComputeManifestPayloadHash(manifest);
        await _fs.WriteFileAtomicAsync(manifestPath, manifest.ToJsonString());
        await PendingTurnSnapshotTestAuthority.SyncAuthorityForCurrentManifestAsync(_fs);
    }

    private static JsonObject BuildProfileChange(JsonObject actor) =>
        new()
        {
            ["NPCId"] = actor["NPCId"]!.GetValue<string>(),
            ["reason"] = "Опыт этого хода изменил убеждения персонажа.",
            ["profile"] = new JsonObject
            {
                ["worldview"] = "Свидетельство важнее догадки."
            }
        };

    private static JsonObject BuildLockedFateCard(string cardId) =>
        new()
        {
            ["cardId"] = cardId,
            ["name"] = "Путь архивного света",
            ["image_prompt"] = "archivist holding a sealed lantern in a dark stone library, realistic fantasy art",
            ["description"] = "Скрытая возможность выбрать долг вместо безопасности.",
            ["unlockConditions"] = new JsonObject
            {
                ["requiredRelationshipLevel"] = 100,
                ["plotConditionDescription"] = "Герой возвращает утраченную страницу.",
                ["conjunction"] = "AND"
            },
            ["rewards"] = new JsonObject
            {
                ["description"] = "Наставник открывает безопасный путь через архив."
            },
            ["isUnlocked"] = false
        };

    private static JsonObject BuildLockedFateCardWithActiveSkill(string cardId)
    {
        var card = BuildLockedFateCard(cardId);
        card["rewards"]!["newActiveSkills"] = new JsonArray(new JsonObject
        {
            ["skillName"] = "Archive Ward",
            ["skillDescription"] = "Raises a brief protective seal.",
            ["rarity"] = "Rare",
            ["actionCost"] = "Main",
            ["combatEffect"] = new JsonObject
            {
                ["isActivatedEffect"] = true,
                ["actionName"] = "Raise Archive Ward",
                ["effects"] = new JsonArray(new JsonObject
                {
                    ["effectType"] = "Damage",
                    ["value"] = "10%",
                    ["targetType"] = "Enemy",
                    ["effectDescription"] = "A seal lashes the target with stored force.",
                    ["poiseDamage"] = "5%"
                })
            }
        });
        return card;
    }

    private static JsonObject BuildHistoricalMaterialization(string actorId) =>
        new()
        {
            ["schemaVersion"] = 1,
            ["materializationId"] = $"mat_{actorId}_historical",
            ["actorType"] = "mortal_npc",
            ["actorId"] = actorId,
            ["materializedAtTurn"] = 7,
            ["state"] = "complete",
            ["capabilities"] = new JsonObject
            {
                ["canFight"] = false,
                ["canTeach"] = true,
                ["canTrade"] = false,
                ["ownsItems"] = false
            },
            ["sections"] = new JsonObject
            {
                ["skills"] = new JsonObject { ["state"] = "empty_by_design", ["reason"] = "Боевые навыки не проявлены." },
                ["inventory"] = new JsonObject { ["state"] = "empty_by_design", ["reason"] = "Личных предметов нет." },
                ["fateCards"] = new JsonObject { ["state"] = "populated" },
                ["personalQuests"] = new JsonObject { ["state"] = "empty_by_design", ["reason"] = "Личная просьба не сформирована." },
                ["relationships"] = new JsonObject { ["state"] = "populated" }
            }
        };

    private static void ReplaceFixturePlaceholders(JsonObject root, string factionId, string factionName)
    {
        var json = root.ToJsonString()
            .Replace(fixturePlaceholderFactionId, factionId, StringComparison.Ordinal)
            .Replace(fixturePlaceholderFactionName, factionName, StringComparison.Ordinal);
        var replacement = JsonNode.Parse(json)!.AsObject();
        root.Clear();
        foreach (var property in replacement.ToList())
        {
            replacement.Remove(property.Key);
            root[property.Key] = property.Value;
        }
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_rootPath))
                Directory.Delete(_rootPath, recursive: true);
        }
        catch
        {
            // Ignore temporary cleanup failures.
        }
    }

    private sealed record NpcCoreFixture(
        JsonObject CurrentRoot,
        JsonObject PreTurnRoot,
        JsonObject CurrentActor,
        string ActorId,
        string LocationId,
        string FactionId,
        string FactionName,
        IReadOnlyDictionary<string, JsonObject> Files);
}
