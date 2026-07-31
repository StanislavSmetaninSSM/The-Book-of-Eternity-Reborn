using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

[Trait("Category", "FullValidation")]
public sealed partial class CanonicalStateNormalizerTests
{
    [Fact]
    public async Task NormalizeAccumulatedStateAsync_MergesMortalTeacherShowcasePatchIntoExistingNpc()
    {
        var files = MortalBootstrapStateBuilder.BuildFreshMortalBootstrapFiles(
            incarnationNumber: 1,
            turnNumber: 4,
            characterDescription: "Асур де Вальмонт, молодой аристократ-маг.",
            worldDescription: "Столица Этернии с городскими наставниками и витринами обучения.",
            startingCircumstances: "За дверью ждёт наставница семейного архива, которая может обучить чтению печатей за плату.",
            createdAtUtc: DateTimeOffset.Parse("2026-07-07T08:00:00Z"));
        var explicitNpcRoot = MortalActorTestFixtures.CreateNpcCoreRoot();
        var explicitTeacher = Assert.Single(
            explicitNpcRoot["NPCsInScene"]!.AsArray().OfType<JsonObject>());
        explicitTeacher["name"] = "Наставница семейного архива";
        explicitTeacher["role"] = "Наставница";

        foreach (var (path, node) in files)
            await _fs.WriteFileAtomicAsync(path, node.ToJsonString());
        await _fs.WriteFileAtomicAsync(
            "game_state/npcs/npc_core.json",
            explicitNpcRoot.ToJsonString());

        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Искра Перед Рассветом",
          "currentRealm": "Mortal World",
          "currentIncarnation": 1
        }
        """);

        var npcRoot = Assert.IsType<JsonObject>(explicitNpcRoot.DeepClone());
        var teacher = Assert.Single(npcRoot["NPCsInScene"]!.AsArray().OfType<JsonObject>());
        var sourceHash = TrainingService.ComputeSourceSnapshotHash(teacher);
        npcRoot["UpdateNPCs"] = new JsonArray
        {
            new JsonObject
            {
                ["npcId"] = "npc_life_001_start_teacher",
                ["name"] = "Наставница семейного архива",
                ["inventory"] = new JsonArray(),
                ["trainingShowcase"] = new JsonObject
                {
                    ["requestId"] = "training_showcase_req_npc_life_001_start_teacher",
                    ["requestKind"] = "mortal_teacher_showcase",
                    ["realm"] = "mortal_world",
                    ["sourceActorId"] = "npc_life_001_start_teacher",
                    ["sourceActorSnapshotHash"] = sourceHash,
                    ["offers"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["offerId"] = "train_npc_life_001_start_teacher_seal_reading_1",
                            ["targetKind"] = "skill_mastery",
                            ["targetId"] = "skill_life_001_seal_reading",
                            ["targetName"] = "Чтение печатей",
                            ["currentValue"] = 0,
                            ["targetValue"] = 1,
                            ["sourceCap"] = 2,
                            ["cost"] = new JsonObject
                            {
                                ["money"] = 30,
                                ["currentLevelExperiencePercent"] = 10
                            },
                            ["summary"] = "Наставница объясняет, как увидеть ложь в воске и нитях печати."
                        }
                    }
                }
            },
            new JsonObject
            {
                ["npcId"] = "npc_life_001_start_teacher",
                ["name"] = null,
                ["role"] = null,
                ["inventory"] = null,
                ["goals"] = null,
                ["trainingShowcase"] = null
            }
        };
        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_core.json", npcRoot.ToJsonString());

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await normalizer.NormalizeAccumulatedStateAsync();

        var normalizedRoot = JsonNode.Parse((await _fs.ReadFileAsync("game_state/npcs/npc_core.json"))!)!.AsObject();
        var normalizedTeacher = Assert.Single(normalizedRoot["NPCsInScene"]!.AsArray().OfType<JsonObject>());
        Assert.NotNull(normalizedTeacher["trainingShowcase"]);
        Assert.Equal(
            "training_showcase_req_npc_life_001_start_teacher",
            normalizedTeacher["trainingShowcase"]!["requestId"]!.GetValue<string>());
        Assert.False(normalizedRoot.ContainsKey("UpdateNPCs"));

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();
        var forbiddenCodes = new[]
        {
            "npc_full_object_missing_required_fields",
            "npc_existing_inventory_resend_forbidden",
            "structured_npc_update_out_of_scope",
            "mortal_relevant_actor_missing_persistence"
        };
        Assert.DoesNotContain(issues, issue => forbiddenCodes.Contains(issue.Code, StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_CanonicalizesNpcJournalEntryStrings()
    {
        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_core.json", """
        {
          "NPCsInScene": [
            {
              "npcId": "npc_valmont_house_servant_001",
              "name": "Домашний слуга Валмонтов",
              "role": "Слуга дома Вальмонт"
            }
          ]
        }
        """);
        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_journals.json", """
        {
          "NPCJournals": [
            {
              "npcId": "npc_valmont_house_servant_001",
              "npcName": "Домашний слуга Валмонтов",
              "lastJournalNote": "Я видел посыльного у боковой лестницы.",
              "journalEntries": [
                "Я видел посыльного у боковой лестницы.",
                {
                  "entryId": "journal_servant_002",
                  "note": "Порошок на перчатке посыльного совпал с краем конверта.",
                  "timestamp": "2026-07-07T04:17:06.8974555Z"
                }
              ]
            },
            {
              "npcId": "npc_valmont_house_servant_001",
              "npcName": "Домашний слуга Валмонтов",
              "entry": "Слуга вспомнил серебряную окантовку на форме посыльного."
            }
          ]
        }
        """);

        await normalizer.NormalizeAccumulatedStateAsync();

        var root = JsonNode.Parse((await _fs.ReadFileAsync("game_state/npcs/npc_journals.json"))!)!.AsObject();
        var journals = root["NPCJournals"]!.AsArray().OfType<JsonObject>().ToList();
        Assert.Equal(2, journals.Count);
        var journal = journals[0];
        var entries = journal["journalEntries"]!.AsArray();
        var first = Assert.IsType<JsonObject>(entries[0]);
        var second = Assert.IsType<JsonObject>(entries[1]);
        Assert.Equal("Я видел посыльного у боковой лестницы.", first["description"]?.GetValue<string>());
        Assert.Equal("Порошок на перчатке посыльного совпал с краем конверта.", second["description"]?.GetValue<string>());
        Assert.Equal("journal_servant_002", second["entryId"]?.GetValue<string>());
        Assert.Equal("Слуга вспомнил серебряную окантовку на форме посыльного.", journals[1]["lastJournalNote"]?.GetValue<string>());
        var legacyEntry = Assert.Single(journals[1]["journalEntries"]!.AsArray().OfType<JsonObject>());
        Assert.Equal("Слуга вспомнил серебряную окантовку на форме посыльного.", legacyEntry["description"]?.GetValue<string>());

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();
        Assert.DoesNotContain(issues, issue =>
            issue.FilePath.Contains("npc_journals", StringComparison.OrdinalIgnoreCase) &&
            (string.Equals(issue.Code, "expected_object", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(issue.Code, "missing_required_string", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_PreservesOmittedHistoricalMortalMaterializationEnvelope()
    {
        const string path = "game_state/npcs/npc_core.json";
        const string backupPath = "game_state/control/pending_turn_snapshot/game_state/npcs/npc_core.json";
        await _fs.WriteFileAtomicAsync(path, BuildMortalNormalizerState(includeEnvelope: false));
        await _fs.WriteFileAtomicAsync(backupPath, BuildMortalNormalizerState(includeEnvelope: true));
        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);

        await normalizer.NormalizeAccumulatedStateAsync(new Dictionary<string, string>
        {
            [path] = backupPath
        });

        var root = JsonNode.Parse((await _fs.ReadFileAsync(path))!)!.AsObject();
        var actor = Assert.Single(root["NPCsInScene"]!.AsArray().OfType<JsonObject>());
        Assert.Equal(
            "mat_normalizer_mortal_historical",
            actor[ActorMaterializationContract.PropertyName]?["materializationId"]?.GetValue<string>());
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_DoesNotRestoreAmbiguousHistoricalMortalMaterializationEnvelope()
    {
        const string path = "game_state/npcs/npc_core.json";
        const string backupPath = "game_state/control/pending_turn_snapshot/game_state/npcs/npc_core.json";
        var historicalRoot = JsonNode.Parse(BuildMortalNormalizerState(includeEnvelope: true))!.AsObject();
        var duplicateActor = historicalRoot["NPCsInScene"]![0]!.DeepClone().AsObject();
        duplicateActor[ActorMaterializationContract.PropertyName]!["materializationId"] =
            "mat_normalizer_mortal_conflicting";
        historicalRoot["NPCsInScene"]!.AsArray().Add(duplicateActor);
        await _fs.WriteFileAtomicAsync(path, BuildMortalNormalizerState(includeEnvelope: false));
        await _fs.WriteFileAtomicAsync(backupPath, historicalRoot.ToJsonString());
        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);

        await normalizer.NormalizeAccumulatedStateAsync(new Dictionary<string, string>
        {
            [path] = backupPath
        });

        var root = JsonNode.Parse((await _fs.ReadFileAsync(path))!)!.AsObject();
        var actor = Assert.Single(root["NPCsInScene"]!.AsArray().OfType<JsonObject>());
        Assert.False(actor.ContainsKey(ActorMaterializationContract.PropertyName));
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_DoesNotRestoreHistoricalMortalEnvelopeWhenDuplicateIdentityLacksEnvelope()
    {
        const string path = "game_state/npcs/npc_core.json";
        const string backupPath = "game_state/control/pending_turn_snapshot/game_state/npcs/npc_core.json";
        var historicalRoot = JsonNode.Parse(BuildMortalNormalizerState(includeEnvelope: true))!.AsObject();
        var duplicateActor = historicalRoot["NPCsInScene"]![0]!.DeepClone().AsObject();
        duplicateActor.Remove(ActorMaterializationContract.PropertyName);
        historicalRoot["NPCsInScene"]!.AsArray().Add(duplicateActor);
        await _fs.WriteFileAtomicAsync(path, BuildMortalNormalizerState(includeEnvelope: false));
        await _fs.WriteFileAtomicAsync(backupPath, historicalRoot.ToJsonString());
        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);

        await normalizer.NormalizeAccumulatedStateAsync(new Dictionary<string, string>
        {
            [path] = backupPath
        });

        var root = JsonNode.Parse((await _fs.ReadFileAsync(path))!)!.AsObject();
        var actor = Assert.Single(root["NPCsInScene"]!.AsArray().OfType<JsonObject>());
        Assert.False(actor.ContainsKey(ActorMaterializationContract.PropertyName));
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_DoesNotRestoreHistoricalMortalEnvelopeForAmbiguousCurrentIdentity()
    {
        const string path = "game_state/npcs/npc_core.json";
        const string backupPath = "game_state/control/pending_turn_snapshot/game_state/npcs/npc_core.json";
        var currentRoot = JsonNode.Parse(BuildMortalNormalizerState(includeEnvelope: false))!.AsObject();
        currentRoot["NPCsInScene"]!.AsArray().Add(currentRoot["NPCsInScene"]![0]!.DeepClone());
        await _fs.WriteFileAtomicAsync(path, currentRoot.ToJsonString());
        await _fs.WriteFileAtomicAsync(backupPath, BuildMortalNormalizerState(includeEnvelope: true));
        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);

        await normalizer.NormalizeAccumulatedStateAsync(new Dictionary<string, string>
        {
            [path] = backupPath
        });

        var root = JsonNode.Parse((await _fs.ReadFileAsync(path))!)!.AsObject();
        var actors = root["NPCsInScene"]!.AsArray().OfType<JsonObject>().ToList();
        Assert.Equal(2, actors.Count);
        Assert.All(actors, actor =>
            Assert.False(actor.ContainsKey(ActorMaterializationContract.PropertyName)));
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_DoesNotRestoreMortalEnvelopeFromNonStringHistoricalIdentity()
    {
        const string path = "game_state/npcs/npc_core.json";
        const string backupPath = "game_state/control/pending_turn_snapshot/game_state/npcs/npc_core.json";
        var historicalRoot = JsonNode.Parse(BuildMortalNormalizerState(includeEnvelope: true))!.AsObject();
        var historicalActor = historicalRoot["NPCsInScene"]![0]!.AsObject();
        historicalActor.Remove("npcId");
        historicalActor["NPCId"] = 42;
        historicalActor[ActorMaterializationContract.PropertyName]!["actorId"] = "42";
        var currentRoot = JsonNode.Parse(BuildMortalNormalizerState(includeEnvelope: false))!.AsObject();
        var currentActor = currentRoot["NPCsInScene"]![0]!.AsObject();
        currentActor.Remove("npcId");
        currentActor["NPCId"] = "42";
        await _fs.WriteFileAtomicAsync(path, currentRoot.ToJsonString());
        await _fs.WriteFileAtomicAsync(backupPath, historicalRoot.ToJsonString());
        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);

        await normalizer.NormalizeAccumulatedStateAsync(new Dictionary<string, string>
        {
            [path] = backupPath
        });

        var root = JsonNode.Parse((await _fs.ReadFileAsync(path))!)!.AsObject();
        var actor = Assert.Single(root["NPCsInScene"]!.AsArray().OfType<JsonObject>());
        Assert.False(actor.ContainsKey(ActorMaterializationContract.PropertyName));
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_DoesNotRestoreMortalEnvelopeForConflictingCurrentIdentityAliases()
    {
        const string path = "game_state/npcs/npc_core.json";
        const string backupPath = "game_state/control/pending_turn_snapshot/game_state/npcs/npc_core.json";
        var currentRoot = JsonNode.Parse(BuildMortalNormalizerState(includeEnvelope: false))!.AsObject();
        currentRoot["NPCsInScene"]![0]!["npcId"] = "npc_conflicting_identity";
        await _fs.WriteFileAtomicAsync(path, currentRoot.ToJsonString());
        await _fs.WriteFileAtomicAsync(backupPath, BuildMortalNormalizerState(includeEnvelope: true));
        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);

        await normalizer.NormalizeAccumulatedStateAsync(new Dictionary<string, string>
        {
            [path] = backupPath
        });

        var root = JsonNode.Parse((await _fs.ReadFileAsync(path))!)!.AsObject();
        var actor = Assert.Single(root["NPCsInScene"]!.AsArray().OfType<JsonObject>());
        Assert.False(actor.ContainsKey(ActorMaterializationContract.PropertyName));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task NormalizeAccumulatedStateAsync_DoesNotRestoreMortalEnvelopeWhenPermanentAliasIsExplicitNull(
        bool nullAliasIsHistorical)
    {
        const string path = "game_state/npcs/npc_core.json";
        const string backupPath = "game_state/control/pending_turn_snapshot/game_state/npcs/npc_core.json";
        var historicalRoot = JsonNode.Parse(BuildMortalNormalizerState(includeEnvelope: true))!.AsObject();
        var currentRoot = JsonNode.Parse(BuildMortalNormalizerState(includeEnvelope: false))!.AsObject();
        var actor = nullAliasIsHistorical
            ? historicalRoot["NPCsInScene"]![0]!.AsObject()
            : currentRoot["NPCsInScene"]![0]!.AsObject();
        actor["npcId"] = null;
        await _fs.WriteFileAtomicAsync(path, currentRoot.ToJsonString());
        await _fs.WriteFileAtomicAsync(backupPath, historicalRoot.ToJsonString());
        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);

        await normalizer.NormalizeAccumulatedStateAsync(new Dictionary<string, string>
        {
            [path] = backupPath
        });

        var root = JsonNode.Parse((await _fs.ReadFileAsync(path))!)!.AsObject();
        var normalizedActor = Assert.Single(root["NPCsInScene"]!.AsArray().OfType<JsonObject>());
        Assert.False(normalizedActor.ContainsKey(ActorMaterializationContract.PropertyName));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NormalizeAccumulatedStateAsync_RestoresMortalEnvelopeByInitialIdWhenPermanentIdIsUnassigned(
        bool includeNullPermanentAlias)
    {
        const string path = "game_state/npcs/npc_core.json";
        const string backupPath = "game_state/control/pending_turn_snapshot/game_state/npcs/npc_core.json";
        var historicalRoot = JsonNode.Parse(BuildMortalNormalizerState(includeEnvelope: true))!.AsObject();
        var currentRoot = JsonNode.Parse(BuildMortalNormalizerState(includeEnvelope: false))!.AsObject();
        foreach (var stateRoot in new[] { historicalRoot, currentRoot })
        {
            var stateActor = stateRoot["NPCsInScene"]![0]!.AsObject();
            stateActor.Remove("NPCId");
            stateActor.Remove("npcId");
            if (includeNullPermanentAlias)
                stateActor["NPCId"] = null;
            stateActor["initialId"] = "npc_initial_materialization";
        }
        historicalRoot["NPCsInScene"]![0]![ActorMaterializationContract.PropertyName]!["actorId"] =
            "npc_initial_materialization";
        await _fs.WriteFileAtomicAsync(path, currentRoot.ToJsonString());
        await _fs.WriteFileAtomicAsync(backupPath, historicalRoot.ToJsonString());
        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);

        await normalizer.NormalizeAccumulatedStateAsync(new Dictionary<string, string>
        {
            [path] = backupPath
        });

        var normalizedRoot = JsonNode.Parse((await _fs.ReadFileAsync(path))!)!.AsObject();
        var normalizedActor = Assert.Single(normalizedRoot["NPCsInScene"]!.AsArray().OfType<JsonObject>());
        Assert.Equal(
            "npc_initial_materialization",
            normalizedActor[ActorMaterializationContract.PropertyName]?["actorId"]?.GetValue<string>());
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_DoesNotOverwriteExplicitChangedMortalMaterializationEnvelope()
    {
        const string path = "game_state/npcs/npc_core.json";
        const string backupPath = "game_state/control/pending_turn_snapshot/game_state/npcs/npc_core.json";
        await _fs.WriteFileAtomicAsync(
            path,
            BuildMortalNormalizerState(includeEnvelope: true, materializationId: "mat_normalizer_mortal_changed"));
        await _fs.WriteFileAtomicAsync(backupPath, BuildMortalNormalizerState(includeEnvelope: true));
        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);

        await normalizer.NormalizeAccumulatedStateAsync(new Dictionary<string, string>
        {
            [path] = backupPath
        });

        var root = JsonNode.Parse((await _fs.ReadFileAsync(path))!)!.AsObject();
        var actor = Assert.Single(root["NPCsInScene"]!.AsArray().OfType<JsonObject>());
        Assert.Equal(
            "mat_normalizer_mortal_changed",
            actor[ActorMaterializationContract.PropertyName]?["materializationId"]?.GetValue<string>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NormalizeAccumulatedStateAsync_DoesNotInventMortalMaterializationEnvelope(bool actorExistedPreviously)
    {
        const string path = "game_state/npcs/npc_core.json";
        const string backupPath = "game_state/control/pending_turn_snapshot/game_state/npcs/npc_core.json";
        await _fs.WriteFileAtomicAsync(path, BuildMortalNormalizerState(includeEnvelope: false));
        await _fs.WriteFileAtomicAsync(
            backupPath,
            actorExistedPreviously
                ? BuildMortalNormalizerState(includeEnvelope: false)
                : """{ "UpdateNPCs": [], "NPCsInScene": [] }""");
        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);

        await normalizer.NormalizeAccumulatedStateAsync(new Dictionary<string, string>
        {
            [path] = backupPath
        });

        var root = JsonNode.Parse((await _fs.ReadFileAsync(path))!)!.AsObject();
        var actor = Assert.Single(root["NPCsInScene"]!.AsArray().OfType<JsonObject>());
        Assert.False(actor.ContainsKey(ActorMaterializationContract.PropertyName));
    }

    private static string BuildMortalNormalizerState(
        bool includeEnvelope,
        string materializationId = "mat_normalizer_mortal_historical")
    {
        var actor = new JsonObject
        {
            ["NPCId"] = "npc_normalizer_materialization",
            ["npcId"] = "npc_normalizer_materialization",
            ["name"] = "Хранитель записи",
            ["currentLocationId"] = "loc_normalizer_archive",
            ["relationshipLevel"] = 0,
            ["attitude"] = "Нейтралитет",
            ["relationshipLock"] = new JsonObject { ["isLocked"] = false },
            ["inventory"] = new JsonArray(),
            ["goals"] = new JsonObject { ["shortTerm"] = "Сохранить запись." }
        };
        if (includeEnvelope)
        {
            actor[ActorMaterializationContract.PropertyName] = new JsonObject
            {
                ["schemaVersion"] = 1,
                ["materializationId"] = materializationId,
                ["actorType"] = "mortal_npc",
                ["actorId"] = "npc_normalizer_materialization",
                ["materializedAtTurn"] = 4,
                ["state"] = "complete",
                ["capabilities"] = new JsonObject(),
                ["sections"] = new JsonObject()
            };
        }

        return new JsonObject
        {
            ["UpdateNPCs"] = new JsonArray(),
            ["NPCsInScene"] = new JsonArray(actor)
        }.ToJsonString();
    }
}
