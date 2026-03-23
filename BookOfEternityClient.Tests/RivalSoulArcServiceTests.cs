using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class RivalSoulArcServiceTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly RivalSoulArcService _service;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public RivalSoulArcServiceTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-rival-arc-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
        _service = new RivalSoulArcService(_fs, NullLogger<RivalSoulArcService>.Instance);
    }

    [Fact]
    public async Task BuildSystemReminderFragmentAsync_WithActiveArcs_ShowsSummaryAndCounterplayReminder()
    {
        await _fs.WriteFileAtomicAsync(RivalSoulArcService.StatePath, JsonSerializer.Serialize(new
        {
            arcs = new object[]
            {
                new
                {
                    arcId = "arc_hunt_001",
                    scope = "major",
                    arcType = "hostile_hunt",
                    status = "rising",
                    sponsorGuardianRef = new { mode = "guardianId", guardianId = "guard_social_azalia_001", displayName = "Азалия" },
                    rivalSoul = new { rivalSoulId = "soul_rival_001", displayNameOrMoniker = "Алый Палач", roleSummary = "Охотник", isKnownToPlayer = true },
                    objective = "Найти и убить игрока",
                    playerIntersection = new { targetsPlayerDirectly = true, stakes = "Жизнь игрока", canBecomeSoulQuest = true, recommendedCounterQuestTone = "urgent" },
                    milestones = new object[] { new { stage = 0, title = "Слух", summary = "Ходят слухи об охотнике.", visibleToPlayer = true } },
                    currentStage = 0,
                    publicSignals = new object[] { new { signalId = "signal_1", stage = 0, description = "Слухи на рынке.", source = "rumor", visibleToPlayer = true } },
                    resolution = new { outcome = "ongoing", notes = "Пока охота продолжается." }
                }
            }
        }, JsonOpts));

        var reminder = await _service.BuildSystemReminderFragmentAsync("Mortal World", 8);

        Assert.NotNull(reminder);
        Assert.Contains("ACTIVE RIVAL SOUL ARCS:", reminder, StringComparison.Ordinal);
        Assert.Contains("Алый Палач", reminder, StringComparison.Ordinal);
        Assert.Contains("Найти и убить игрока", reminder, StringComparison.Ordinal);
        Assert.Contains("Next natural pressure", reminder, StringComparison.Ordinal);
        Assert.Contains("PLAYER COUNTERPLAY REMINDER", reminder, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildSystemReminderFragmentAsync_OnInitialMortalBootstrap_ShowsOptionalIntroHint()
    {
        var reminder = await _service.BuildSystemReminderFragmentAsync("Mortal World", 1);

        Assert.NotNull(reminder);
        Assert.Contains("OPTIONAL RIVAL SOUL ARC", reminder, StringComparison.Ordinal);
        Assert.Contains("UpdateRivalSoulArcs", reminder, StringComparison.Ordinal);
        Assert.Contains("hostile hunt", reminder, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildSystemReminderFragmentAsync_InMatureWorldWithoutArcs_ShowsOpportunityNudgeOnCadence()
    {
        await _fs.WriteFileAtomicAsync("game_state/quests/plot_outline.json", """
        {
          "plotOutline": {
            "mainArc": "Секты стягивают силы вокруг древнего разлома.",
            "characterSubplots": [],
            "loomingThreatsOrOpportunities": ["Грядёт новый передел власти."],
            "lastUpdatedTurn": 8
          }
        }
        """);

        var reminder = await _service.BuildSystemReminderFragmentAsync("Mortal World", 8);

        Assert.NotNull(reminder);
        Assert.Contains("RIVAL ARC OPPORTUNITY", reminder, StringComparison.Ordinal);
        Assert.Contains("No rival soul arc is active", reminder, StringComparison.Ordinal);
        Assert.Contains("Suggested rival arc seeds:", reminder, StringComparison.Ordinal);
        Assert.Contains("political_claim", reminder, StringComparison.Ordinal);
        Assert.Contains("ideological_mission", reminder, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildSystemReminderFragmentAsync_InCyberpunkContext_SuggestsPoliticalSeed()
    {
        await _fs.WriteFileAtomicAsync("lore/current_world/world_setting.json", """
        {
          "worldType": "cyberpunk",
          "genre": "neon_noir",
          "setting": {
            "name": "Неон-Сити"
          },
          "majorThemes": [
            "Власть мегакорпораций",
            "Черные операции и корпоративные чистки"
          ],
          "currentCrisis": {
            "name": "Война советов директоров",
            "description": "Мегакорпорации готовят ликвидационные команды и охоту за ключевыми активами."
          }
        }
        """);
        await _fs.WriteFileAtomicAsync("game_state/world/world_events.json", """
        {
          "worldEventsLog": [
            {
              "eventId": "cyberpunk_event_001",
              "title": "Корпоративная охота",
              "description": "По Неон-Сити ходят слухи о manhunt и bounty notice против ключевых фигур.",
              "visibility": "Public"
            }
          ]
        }
        """);

        var reminder = await _service.BuildSystemReminderFragmentAsync("Mortal World", 8);

        Assert.NotNull(reminder);
        Assert.Contains("Suggested rival arc seeds:", reminder, StringComparison.Ordinal);
        Assert.Contains("political_claim", reminder, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildSystemReminderFragmentAsync_InHostilePressureContext_SuggestsHostileHuntSeed()
    {
        await _fs.WriteFileAtomicAsync("lore/current_world/world_setting.json", """
        {
          "worldType": "modern_thriller",
          "genre": "urban_noir",
          "setting": {
            "name": "Серый Город"
          },
          "majorThemes": [
            "Розыск и охота на свидетелей",
            "Карательные группы и ликвидационные команды"
          ],
          "currentCrisis": {
            "name": "Операция Ночной След",
            "description": "По городу идет manhunt, bounty notice и преследование всех, кто знает слишком много."
          }
        }
        """);

        var reminder = await _service.BuildSystemReminderFragmentAsync("Mortal World", 8);

        Assert.NotNull(reminder);
        Assert.Contains("Suggested rival arc seeds:", reminder, StringComparison.Ordinal);
        Assert.Contains("hostile_hunt", reminder, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildSystemReminderFragmentAsync_InScienceFictionArtifactContext_SuggestsArtifactRace()
    {
        await _fs.WriteFileAtomicAsync("lore/current_world/world_setting.json", """
        {
          "worldType": "science_fiction",
          "genre": "space_opera",
          "setting": {
            "name": "Орбитальный Предел"
          },
          "majorThemes": [
            "Аномалии глубокого космоса",
            "Поиск древних архивов и прототипов"
          ],
          "currentCrisis": {
            "name": "Разлом у Черной Станции",
            "description": "Экспедиции спорят за доступ к древнему ядру, архиву и аномалии в руинах станции."
          }
        }
        """);

        var reminder = await _service.BuildSystemReminderFragmentAsync("Mortal World", 8);

        Assert.NotNull(reminder);
        Assert.Contains("Suggested rival arc seeds:", reminder, StringComparison.Ordinal);
        Assert.Contains("artifact_race", reminder, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildSystemReminderFragmentAsync_InSpacePoliticsContext_SuggestsPoliticalClaim()
    {
        await _fs.WriteFileAtomicAsync("lore/current_world/world_setting.json", """
        {
          "worldType": "science_fiction",
          "genre": "space_opera",
          "setting": {
            "name": "Пояс Гелиоса"
          },
          "majorThemes": [
            "Федерация колоний и борьба за сектор",
            "Флот и губернаторы спорят за власть на пограничных мирах"
          ],
          "currentCrisis": {
            "name": "Выбор правителя колонии",
            "description": "Планетарная ассамблея, флот и корпорации тянут власть в разные стороны."
          }
        }
        """);

        var reminder = await _service.BuildSystemReminderFragmentAsync("Mortal World", 8);

        Assert.NotNull(reminder);
        Assert.Contains("Suggested rival arc seeds:", reminder, StringComparison.Ordinal);
        Assert.Contains("political_claim", reminder, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildSystemReminderFragmentAsync_InAiBiotechContext_SuggestsArtifactRace()
    {
        await _fs.WriteFileAtomicAsync("lore/current_world/world_setting.json", """
        {
          "worldType": "science_fiction",
          "genre": "biopunk",
          "setting": {
            "name": "Станция Нейрон-9"
          },
          "majorThemes": [
            "Генетические архивы, ИИ-ядра и синтетики",
            "Охота за прототипами и квантовыми модулями"
          ],
          "currentCrisis": {
            "name": "Пробуждение Сингулярности",
            "description": "Все ищут ИИ core, геномный архив и прототип синтетического тела."
          }
        }
        """);

        var reminder = await _service.BuildSystemReminderFragmentAsync("Mortal World", 8);

        Assert.NotNull(reminder);
        Assert.Contains("Suggested rival arc seeds:", reminder, StringComparison.Ordinal);
        Assert.Contains("artifact_race", reminder, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildSystemReminderFragmentAsync_InGuildMageHeroContext_SuggestsRivalAscension()
    {
        await _fs.WriteFileAtomicAsync("lore/current_world/world_setting.json", """
        {
          "worldType": "fantasy",
          "genre": "heroic_adventure",
          "setting": {
            "name": "Лазурные Марки"
          },
          "majorThemes": [
            "Гильдии авантюристов, магические академии и турниры героев",
            "Восходящие чемпионы, молодые маги и избранные охотники на чудовищ"
          ],
          "currentCrisis": {
            "name": "Путь к Башне Архимага",
            "description": "Молодые маги, герои и авантюристы соревнуются на турнире за славу, титулы, прорыв и право войти в высшие круги."
          }
        }
        """);

        var reminder = await _service.BuildSystemReminderFragmentAsync("Mortal World", 8);

        Assert.NotNull(reminder);
        Assert.Contains("Suggested rival arc seeds:", reminder, StringComparison.Ordinal);
        Assert.Contains("rival_ascension", reminder, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildSystemReminderFragmentAsync_InRichFantasyContext_SuggestsFantasyFriendlySeeds()
    {
        await _fs.WriteFileAtomicAsync("lore/current_world/world_setting.json", """
        {
          "worldType": "fantasy",
          "genre": "dark_epic_fantasy",
          "setting": {
            "name": "Серебряные Герцогства"
          },
          "majorThemes": [
            "Рыцарские ордена, паладины и борьба за трон",
            "Руины, драконьи клады, алхимия и охота на чудовищ"
          ],
          "currentCrisis": {
            "name": "Печать Под Лунной Рощей",
            "description": "Священный орден, герцоги и молодые чемпионы спорят о власти, пока в древних руинах просыпаются демоны и ищут драконий клад."
          }
        }
        """);

        var reminder = await _service.BuildSystemReminderFragmentAsync("Mortal World", 8);

        Assert.NotNull(reminder);
        Assert.Contains("Suggested rival arc seeds:", reminder, StringComparison.Ordinal);
        Assert.True(
            reminder.Contains("rival_ascension", StringComparison.Ordinal) ||
            reminder.Contains("political_claim", StringComparison.Ordinal) ||
            reminder.Contains("artifact_race", StringComparison.Ordinal) ||
            reminder.Contains("hostile_hunt", StringComparison.Ordinal),
            reminder);
    }

    [Fact]
    public async Task BuildSystemReminderFragmentAsync_InPostApocalypseContext_SuggestsPostApocFriendlySeeds()
    {
        await _fs.WriteFileAtomicAsync("lore/current_world/world_setting.json", """
        {
          "worldType": "post_apocalypse",
          "genre": "wasteland_survival",
          "setting": {
            "name": "Пепельные Пустоши"
          },
          "majorThemes": [
            "Анклавы выживших и дефицит воды",
            "Охота рейдеров за ключами от довоенных бункеров"
          ],
          "currentCrisis": {
            "name": "Реактор Под Пылью",
            "description": "По пустошам ходят слухи о старом бункере, реакторе и схроне старого мира, за которыми уже идут рейдеры и охотники."
          }
        }
        """);

        var reminder = await _service.BuildSystemReminderFragmentAsync("Mortal World", 8);

        Assert.NotNull(reminder);
        Assert.Contains("Suggested rival arc seeds:", reminder, StringComparison.Ordinal);
        Assert.True(
            reminder.Contains("hostile_hunt", StringComparison.Ordinal) ||
            reminder.Contains("artifact_race", StringComparison.Ordinal) ||
            reminder.Contains("political_claim", StringComparison.Ordinal),
            reminder);
    }

    [Fact]
    public async Task BuildSystemReminderFragmentAsync_InModernCityContext_SuggestsModernFriendlySeeds()
    {
        await _fs.WriteFileAtomicAsync("lore/current_world/world_setting.json", """
        {
          "worldType": "modern",
          "genre": "urban_thriller",
          "setting": {
            "name": "Новая Ривьера"
          },
          "majorThemes": [
            "Выборы мэра, давление корпораций и медиа",
            "Охота спецслужб за сервером с компроматом"
          ],
          "currentCrisis": {
            "name": "Дело Ночного Сервера",
            "description": "Город кипит: мэрская кампания, утечка досье, розыск, спецслужбы и охота за сервером с уликами."
          }
        }
        """);

        var reminder = await _service.BuildSystemReminderFragmentAsync("Mortal World", 8);

        Assert.NotNull(reminder);
        Assert.Contains("Suggested rival arc seeds:", reminder, StringComparison.Ordinal);
        Assert.True(
            reminder.Contains("political_claim", StringComparison.Ordinal) ||
            reminder.Contains("hostile_hunt", StringComparison.Ordinal) ||
            reminder.Contains("artifact_race", StringComparison.Ordinal),
            reminder);
    }

    [Fact]
    public async Task BuildSystemReminderFragmentAsync_InModernSuperhumanContext_SuggestsSuperhumanFriendlySeeds()
    {
        await _fs.WriteFileAtomicAsync("lore/current_world/world_setting.json", """
        {
          "worldType": "modern_superhuman",
          "genre": "superhero_thriller",
          "setting": {
            "name": "Город Аномалий"
          },
          "majorThemes": [
            "Сверхлюди, аномалии и героические программы",
            "Мутанты, сыворотки и отряды сдерживания"
          ],
          "currentCrisis": {
            "name": "Нулевой Разлом",
            "description": "Город спорит о реестре сверхлюдей, охоте на сорвавшихся героев и поиске ядра аномалии, дающего новые силы."
          }
        }
        """);

        var reminder = await _service.BuildSystemReminderFragmentAsync("Mortal World", 8);

        Assert.NotNull(reminder);
        Assert.Contains("Suggested rival arc seeds:", reminder, StringComparison.Ordinal);
        Assert.True(
            reminder.Contains("rival_ascension", StringComparison.Ordinal) ||
            reminder.Contains("hostile_hunt", StringComparison.Ordinal) ||
            reminder.Contains("artifact_race", StringComparison.Ordinal) ||
            reminder.Contains("political_claim", StringComparison.Ordinal),
            reminder);
    }

    [Fact]
    public async Task BuildSystemReminderFragmentAsync_InHorrorContext_SuggestsHorrorFriendlySeeds()
    {
        await _fs.WriteFileAtomicAsync("lore/current_world/world_setting.json", """
        {
          "worldType": "horror",
          "genre": "occult_horror",
          "setting": {
            "name": "Черный Пригород"
          },
          "majorThemes": [
            "Ритуальные убийства, проклятые дома и одержимость",
            "Призраки, ковены и оккультные расследования"
          ],
          "currentCrisis": {
            "name": "Дом за Шестой Улицей",
            "description": "После серии исчезновений полиция и оккультные следователи спорят о ритуале, проклятии и сущности, вышедшей из запечатанной комнаты."
          }
        }
        """);

        var reminder = await _service.BuildSystemReminderFragmentAsync("Mortal World", 8);

        Assert.NotNull(reminder);
        Assert.Contains("Suggested rival arc seeds:", reminder, StringComparison.Ordinal);
        Assert.True(
            reminder.Contains("hostile_hunt", StringComparison.Ordinal) ||
            reminder.Contains("ideological_mission", StringComparison.Ordinal) ||
            reminder.Contains("artifact_race", StringComparison.Ordinal) ||
            reminder.Contains("rival_ascension", StringComparison.Ordinal),
            reminder);
    }

    [Fact]
    public async Task BuildSystemReminderFragmentAsync_InDetectiveContext_SuggestsDetectiveFriendlySeeds()
    {
        await _fs.WriteFileAtomicAsync("lore/current_world/world_setting.json", """
        {
          "worldType": "detective",
          "genre": "noir_investigation",
          "setting": {
            "name": "Гранд-Мост"
          },
          "majorThemes": [
            "Холодные дела, коррумпированная полиция и глухие заговоры",
            "Охота за уликами, досье и пропавшими свидетелями"
          ],
          "currentCrisis": {
            "name": "Дело Серебряного Архива",
            "description": "Следователи спорят за материалы дела, пропавшие улики и сервер с архивом, пока по городу идет cover-up и преследование свидетелей."
          }
        }
        """);

        var reminder = await _service.BuildSystemReminderFragmentAsync("Mortal World", 8);

        Assert.NotNull(reminder);
        Assert.Contains("Suggested rival arc seeds:", reminder, StringComparison.Ordinal);
        Assert.True(
            reminder.Contains("artifact_race", StringComparison.Ordinal) ||
            reminder.Contains("hostile_hunt", StringComparison.Ordinal) ||
            reminder.Contains("rival_ascension", StringComparison.Ordinal) ||
            reminder.Contains("political_claim", StringComparison.Ordinal),
            reminder);
    }

    [Fact]
    public async Task BuildSystemReminderFragmentAsync_WithoutSoulQuests_UsesCurrentWorldEventAsHook()
    {
        await _fs.WriteFileAtomicAsync("lore/current_world/world_setting.json", """
        {
          "worldType": "modern",
          "genre": "urban_thriller",
          "setting": {
            "name": "Новая Ривьера"
          }
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/world/world_events.json", """
        {
          "worldEventsLog": [
            {
              "eventId": "news_001",
              "title": "Кровь на платформе Сумерек",
              "description": "Город обсуждает резонансное убийство и исчезновение ключевого свидетеля.",
              "visibility": "Public"
            }
          ]
        }
        """);

        var reminder = await _service.BuildSystemReminderFragmentAsync("Mortal World", 8);

        Assert.NotNull(reminder);
        Assert.Contains("RIVAL ARC OPPORTUNITY", reminder, StringComparison.Ordinal);
        Assert.Contains("WORLD EVENT HOOK:", reminder, StringComparison.Ordinal);
        Assert.Contains("Кровь на платформе Сумерек", reminder, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildSystemReminderFragmentAsync_WithActiveSoulQuest_DoesNotAddWorldEventHook()
    {
        await _fs.WriteFileAtomicAsync("lore/current_world/world_setting.json", """
        {
          "worldType": "modern",
          "genre": "urban_thriller",
          "setting": {
            "name": "Новая Ривьера"
          }
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/world/world_events.json", """
        {
          "worldEventsLog": [
            {
              "eventId": "news_001",
              "title": "Тени в мэрии",
              "description": "Политический скандал разрастается на глазах у города.",
              "visibility": "Public"
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/quests/soul_quests.json", """
        {
          "quests": [
            {
              "questId": "soul_quest_001",
              "questName": "Сохрани лицо",
              "status": "active"
            }
          ]
        }
        """);

        var reminder = await _service.BuildSystemReminderFragmentAsync("Mortal World", 8);

        Assert.NotNull(reminder);
        Assert.Contains("RIVAL ARC OPPORTUNITY", reminder, StringComparison.Ordinal);
        Assert.DoesNotContain("WORLD EVENT HOOK:", reminder, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildSystemReminderFragmentAsync_OutsideMortalWorld_ReturnsNull()
    {
        var reminder = await _service.BuildSystemReminderFragmentAsync("Chaos Sea", 12);

        Assert.Null(reminder);
    }

    [Fact]
    public async Task ResetForNewLifeAsync_DeletesRivalArcStateFile()
    {
        await _fs.WriteFileAtomicAsync(RivalSoulArcService.StatePath, "{\"arcs\":[]}");
        Assert.True(_fs.FileExists(RivalSoulArcService.StatePath));

        await _service.ResetForNewLifeAsync();

        Assert.False(_fs.FileExists(RivalSoulArcService.StatePath));
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_MergesUpdateRivalSoulArcsIntoCanonicalArcs()
    {
        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await _fs.WriteFileAtomicAsync(RivalSoulArcService.StatePath, """
        {
          "arcs": [
            {
              "arcId": "arc_existing",
              "scope": "minor",
              "arcType": "political_claim",
              "status": "latent",
              "sponsorGuardianRef": { "mode": "guardianId", "guardianId": "guard_social_azalia_001", "displayName": "Азалия" },
              "rivalSoul": { "rivalSoulId": "soul_existing", "displayNameOrMoniker": "Старый Соперник", "roleSummary": "Претендент", "isKnownToPlayer": false },
              "objective": "Захватить маленькое княжество",
              "playerIntersection": { "targetsPlayerDirectly": false, "stakes": "Региональный баланс", "canBecomeSoulQuest": false, "recommendedCounterQuestTone": "neutral" },
              "milestones": [ { "stage": 0, "title": "Начало", "summary": "Пока всё тихо.", "visibleToPlayer": false } ],
              "currentStage": 0,
              "publicSignals": [],
              "resolution": { "outcome": "ongoing", "notes": "Стартовая линия." }
            }
          ],
          "UpdateRivalSoulArcs": [
            {
              "arcId": "arc_existing",
              "scope": "minor",
              "arcType": "political_claim",
              "status": "rising",
              "sponsorGuardianRef": { "mode": "guardianId", "guardianId": "guard_social_azalia_001", "displayName": "Азалия" },
              "rivalSoul": { "rivalSoulId": "soul_existing", "displayNameOrMoniker": "Старый Соперник", "roleSummary": "Претендент", "isKnownToPlayer": true },
              "objective": "Захватить маленькое княжество",
              "playerIntersection": { "targetsPlayerDirectly": false, "stakes": "Региональный баланс", "canBecomeSoulQuest": false, "recommendedCounterQuestTone": "neutral" },
              "milestones": [ { "stage": 0, "title": "Начало", "summary": "Пошли слухи.", "visibleToPlayer": true } ],
              "currentStage": 0,
              "publicSignals": [ { "signalId": "signal_existing", "stage": 0, "description": "Слухи о претенденте.", "source": "rumor", "visibleToPlayer": true } ],
              "resolution": { "outcome": "ongoing", "notes": "Линия развивается." }
            },
            {
              "arcId": "arc_new",
              "scope": "major",
              "arcType": "rival_ascension",
              "status": "latent",
              "sponsorGuardianRef": { "mode": "guardianId", "guardianId": "guard_social_azalia_001", "displayName": "Азалия" },
              "rivalSoul": { "rivalSoulId": "soul_new", "displayNameOrMoniker": "Юный Император", "roleSummary": "Гений-культиватор", "isKnownToPlayer": false },
              "objective": "Стать Императором Континента Восхода",
              "playerIntersection": { "targetsPlayerDirectly": false, "stakes": "Будущий порядок мира", "canBecomeSoulQuest": true, "recommendedCounterQuestTone": "political" },
              "milestones": [ { "stage": 0, "title": "Первые знамения", "summary": "О нём начинают говорить.", "visibleToPlayer": true } ],
              "currentStage": 0,
              "publicSignals": [ { "signalId": "signal_new", "stage": 0, "description": "Слухи о юном гении.", "source": "rumor", "visibleToPlayer": true } ],
              "resolution": { "outcome": "ongoing", "notes": "Пока лишь восходящая фигура." }
            }
          ]
        }
        """);

        await normalizer.NormalizeAccumulatedStateAsync();

        var raw = await _fs.ReadFileAsync(RivalSoulArcService.StatePath);
        Assert.NotNull(raw);
        Assert.Contains("\"arcs\": [", raw, StringComparison.Ordinal);
        Assert.DoesNotContain("UpdateRivalSoulArcs", raw, StringComparison.Ordinal);
        using var doc = JsonDocument.Parse(raw);
        var arcs = doc.RootElement.GetProperty("arcs").EnumerateArray().ToList();
        Assert.Contains(arcs, arc => arc.GetProperty("arcId").GetString() == "arc_existing");
        Assert.Contains(arcs, arc => arc.GetProperty("arcId").GetString() == "arc_new");

        var existingArc = arcs.First(arc => arc.GetProperty("arcId").GetString() == "arc_existing");
        Assert.Equal("rising", existingArc.GetProperty("status").GetString());
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
            // ignored
        }
    }
}
