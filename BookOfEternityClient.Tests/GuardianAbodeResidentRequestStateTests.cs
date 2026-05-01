using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class GuardianAbodeResidentRequestStateTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;

    public GuardianAbodeResidentRequestStateTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-abode-resident-requests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
        CopyDirectory(TestRepoPaths.BaseSessionRoot, Path.Combine(_rootPath, "game_session"));
        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
    }

    [Fact]
    public async Task EnsureManifestationRequestForCurrentIncarnationAsync_MultipleEquippedCompanionEchoRelics_CreateMultiplePendingRequests()
    {
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Тестовая Душа",
            currentRealm = "Mortal World",
            currentIncarnation = 2,
            soulRelics = new
            {
                equipped = new object[]
                {
                    new
                    {
                        relicId = "relic_companion_echo_001",
                        name = "Эхо Вестницы",
                        rarity = "Epic",
                        slot = "Neck",
                        relicType = GuardianAbodeResidentState.RelicTypeCompanionEcho,
                        companionSeed = new
                        {
                            sourceResidentId = "resident_echo_001",
                            sourceGuardianId = "guardian_social_001",
                            sourceAbodeId = "abode_social_001",
                            companionNameHint = "Лиора",
                            originWorldSummary = "Бывшая гонец при храме семи дорог.",
                            futureCompanionPrompt = "Swift wanderer with ember-thread cloak",
                            bondReason = "Она всегда возвращалась к тем, кого однажды назвала своими.",
                            coreTraits = new[] { "верность", "смелость" },
                            archetypeHints = new[] { "courier", "pathfinder" },
                            appearanceMotifs = new[] { "ember-thread cloak" }
                        }
                    },
                    new
                    {
                        relicId = "relic_companion_echo_002",
                        name = "Эхо Щитоносца",
                        rarity = "Rare",
                        slot = "Neck",
                        relicType = GuardianAbodeResidentState.RelicTypeCompanionEcho,
                        companionSeed = new
                        {
                            sourceResidentId = "resident_echo_002",
                            sourceGuardianId = "guardian_combat_001",
                            sourceAbodeId = "abode_combat_001",
                            companionNameHint = "Герен",
                            originWorldSummary = "Бывший телохранитель приграничной цитадели.",
                            futureCompanionPrompt = "Broad-shouldered sentinel with weathered shield",
                            bondReason = "Он всегда становился между опасностью и теми, кого считал своими.",
                            coreTraits = new[] { "стойкость", "преданность" },
                            archetypeHints = new[] { "bodyguard", "sentinel" },
                            appearanceMotifs = new[] { "weathered shield" }
                        }
                    }
                },
                stored = Array.Empty<object>()
            }
        });

        await GuardianAbodeResidentRequestState.EnsureManifestationRequestForCurrentIncarnationAsync(_fs, "Mortal World");

        var requestRaw = await _fs.ReadFileAsync(GuardianAbodeResidentRequestState.PendingManifestationRequestPath);
        Assert.NotNull(requestRaw);
        Assert.Contains("\"requests\": [", requestRaw, StringComparison.Ordinal);
        Assert.Contains("\"relicId\": \"relic_companion_echo_001\"", requestRaw, StringComparison.Ordinal);
        Assert.Contains("\"relicId\": \"relic_companion_echo_002\"", requestRaw, StringComparison.Ordinal);
        Assert.Contains("\"sourceResidentId\": \"resident_echo_001\"", requestRaw, StringComparison.Ordinal);
        Assert.Contains("\"sourceResidentId\": \"resident_echo_002\"", requestRaw, StringComparison.Ordinal);
        Assert.Contains("\"targetIncarnation\": 2", requestRaw, StringComparison.Ordinal);

        var soulRaw = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        Assert.NotNull(soulRaw);
        Assert.Contains("\"companionManifestationLastRequestedIncarnation\": 2", soulRaw, StringComparison.Ordinal);
        Assert.Contains("\"companionManifestationStatus\": \"pending\"", soulRaw, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EnsureManifestationRequestForCurrentIncarnationAsync_ResidentCompanionSeedCarriesPersonalityAndAbodeSnapshot()
    {
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Тестовая Душа",
            currentRealm = "Mortal World",
            currentIncarnation = 2,
            soulRelics = new
            {
                equipped = new object[]
                {
                    new
                    {
                        relicId = "relic_companion_echo_snapshot",
                        name = "Эхо Лиоры",
                        rarity = "Epic",
                        slot = "Neck",
                        relicType = GuardianAbodeResidentState.RelicTypeCompanionEcho,
                        companionSeed = new
                        {
                            sourceResidentId = "resident_echo_001",
                            sourceGuardianId = "guardian_social_001",
                            sourceAbodeId = "abode_social_001",
                            companionNameHint = "Лиора",
                            originWorldSummary = "Бывшая гонец при храме семи дорог.",
                            futureCompanionPrompt = "Swift wanderer with ember-thread cloak",
                            bondReason = "Она всегда возвращалась к тем, кого однажды назвала своими.",
                            coreTraits = new[] { "верность", "смелость" },
                            archetypeHints = new[] { "courier", "pathfinder" },
                            appearanceMotifs = new[] { "ember-thread cloak" },
                            personalityProfile = new
                            {
                                archetype = "Road Messenger",
                                worldview = "Каждая связь требует движения.",
                                culturalLayer = "Храм дорог и клятвенных маршрутов",
                                coreValues = new[] { "верность", "путь", "долг" },
                                personalityTraits = new object[]
                                {
                                    new { traitName = "Restless Loyalty", value = 8, valueDescription = "Всегда ищет дорогу обратно." }
                                }
                            },
                            abodeDisposition = new
                            {
                                powerSensitivity = "medium",
                                migrationDisposition = "selective",
                                communalOrientation = "high",
                                stabilityNeed = "medium"
                            },
                            abodeDevotionLevel = 74,
                            abodeDevotionTier = "devoted",
                            restlessness = 28,
                            migrationState = "settled"
                        }
                    }
                },
                stored = Array.Empty<object>()
            }
        });

        await GuardianAbodeResidentRequestState.EnsureManifestationRequestForCurrentIncarnationAsync(_fs, "Mortal World");

        var requests = await GuardianAbodeResidentRequestState.ReadManifestationRequestsAsync(_fs);
        var request = Assert.Single(requests);
        Assert.NotNull(request.PersonalityProfile);
        Assert.NotNull(request.AbodeDisposition);
        Assert.Equal("Road Messenger", request.PersonalityProfile!.Archetype);
        Assert.Equal(74, request.AbodeDevotionLevel);
        Assert.Equal("devoted", request.AbodeDevotionTier);
        Assert.Equal(28, request.Restlessness);
        Assert.Equal("settled", request.MigrationState);

        var reminder = await GuardianAbodeResidentRequestState.BuildSystemReminderFragmentAsync(_fs, "Mortal World");
        Assert.NotNull(reminder);
        Assert.Contains("архетип=Road Messenger", reminder, StringComparison.Ordinal);
        Assert.Contains("преданность=Предан 74/100", reminder, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EnsureManifestationRequestForCurrentIncarnationAsync_PruneUsesFreshSurvivingRequestsSet()
    {
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Тестовая Душа",
            currentRealm = "Mortal World",
            currentIncarnation = 2,
            soulRelics = new
            {
                equipped = new object[]
                {
                    new
                    {
                        relicId = "relic_companion_echo_reused",
                        name = "Эхо Лиоры",
                        rarity = "Epic",
                        slot = "Neck",
                        relicType = GuardianAbodeResidentState.RelicTypeCompanionEcho,
                        companionSeed = new
                        {
                            sourceResidentId = "resident_echo_001",
                            sourceGuardianId = "guardian_social_001",
                            sourceAbodeId = "abode_social_001",
                            companionNameHint = "Лиора",
                            originWorldSummary = "Бывшая гонец при храме семи дорог.",
                            futureCompanionPrompt = "Swift wanderer with ember-thread cloak",
                            bondReason = "Она всегда возвращалась к тем, кого однажды назвала своими.",
                            coreTraits = new[] { "верность", "смелость" },
                            archetypeHints = new[] { "courier", "pathfinder" },
                            appearanceMotifs = new[] { "ember-thread cloak" }
                        }
                    }
                },
                stored = Array.Empty<object>()
            }
        });
        await WriteJsonAsync(GuardianAbodeResidentRequestState.PendingManifestationRequestPath, new
        {
            requests = new object[]
            {
                new
                {
                    requestId = "old_manifestation_request",
                    relicId = "relic_companion_echo_reused",
                    relicName = "Эхо Лиоры",
                    manifestationSource = "resident_relic",
                    sourceResidentId = "resident_echo_001",
                    companionNameHint = "Лиора",
                    targetIncarnation = 1
                }
            }
        });

        await GuardianAbodeResidentRequestState.EnsureManifestationRequestForCurrentIncarnationAsync(_fs, "Mortal World");

        var requests = await GuardianAbodeResidentRequestState.ReadManifestationRequestsAsync(_fs);
        var request = Assert.Single(requests);
        Assert.Equal(2, request.TargetIncarnation);
        Assert.Equal("relic_companion_echo_reused", request.RelicId);
        Assert.Equal("resident_echo_001", request.SourceResidentId);
    }

    [Fact]
    public void EnsureManifestationRequestForCurrentIncarnationAsync_MustWriteRetryMarkerOnlyAfterSuccessfulBuild()
    {
        var source = File.ReadAllText(Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "Services",
            "GuardianAbodeResidentRequestState.cs"));

        var buildIndex = source.IndexOf("if (!TryBuildManifestationRequest(relic, currentIncarnation, out var request))", StringComparison.Ordinal);
        var markerIndex = source.IndexOf("relic[\"companionManifestationLastRequestedIncarnation\"] = currentIncarnation;", StringComparison.Ordinal);

        Assert.True(buildIndex >= 0);
        Assert.True(markerIndex >= 0);
        Assert.True(buildIndex < markerIndex, "retry marker must be written only after successful manifestation request build");
    }

    [Fact]
    public async Task BuildSystemReminderFragmentAsync_AfterlifePressureStates_SurfaceLeavePressureWithoutTransferInstruction()
    {
        await WriteJsonAsync("game_state/meta/guardians.json", new
        {
            guardians = new object[]
            {
                new
                {
                    guardianId = "guardian_alpha",
                    canonicalName = "Азалия",
                    manifestation = new
                    {
                        currentDisplayName = "Азалия",
                        formFlexibility = "selective",
                        currentPresentationStyle = "feminine",
                        currentPronouns = "она/её",
                        appearanceDescription = "Тестовая форма."
                    },
                    manifestationHistory = Array.Empty<object>()
                }
            }
        });
        await WriteJsonAsync(GuardianAbodeResidentState.StatePath, new
        {
            entries = new object[]
            {
                new
                {
                    residentId = "resident_alpha_1",
                    guardianId = "guardian_alpha",
                    abodeId = "abode_alpha",
                    displayName = "Лиора",
                    residentKind = "wayfaring_soul",
                    originType = "traveler_soul",
                    roleLabel = "Вестница",
                    summary = "Тонкая душа на границе светлых троп.",
                    bondLevel = 48,
                    bondTier = "trusted",
                    canGrantCompanionRelic = true,
                    bondRewardState = "none",
                    linkedSoulQuestId = "",
                    grantedRelicId = "",
                    historyRevealed = true,
                    isPresent = true,
                    personalityProfile = new
                    {
                        archetype = "Road Messenger",
                        worldview = "Belonging must still feel true to remain sacred.",
                        culturalLayer = "Way shrine pilgrim traditions",
                        coreValues = new[] { "верность", "путь" },
                        personalityTraits = new object[]
                        {
                            new
                            {
                                traitName = "sensitivity_to_decline",
                                value = 8,
                                valueDescription = "замечает упадок быстро"
                            }
                        }
                    },
                    abodeDisposition = new
                    {
                        powerSensitivity = "high",
                        migrationDisposition = "selective",
                        communalOrientation = "medium",
                        stabilityNeed = "high"
                    },
                    abodeDevotionLevel = 26,
                    abodeDevotionTier = "uncertain",
                    restlessness = 63,
                    migrationState = "considering_departure",
                    mortalWorldImprint = new
                    {
                        originWorldSummary = "Бывшая гонец при храме дорог.",
                        futureCompanionPrompt = "Swift wanderer"
                    }
                }
            }
        });

        var reminder = await GuardianAbodeResidentRequestState.BuildSystemReminderFragmentAsync(_fs, "Chaos Sea");

        Assert.NotNull(reminder);
        Assert.Contains("ABODE RESIDENT PRESSURE STATES:", reminder, StringComparison.Ordinal);
        Assert.Contains("Лиора", reminder, StringComparison.Ordinal);
        Assert.Contains("considering_departure", reminder, StringComparison.Ordinal);
        Assert.Contains("Do not reassign or transfer residents automatically", reminder, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildSystemReminderFragmentAsync_AfterlifeTransferRequests_SurfaceCanonicalTransferContract()
    {
        await GuardianAbodeResidentRequestState.WriteTransferRequestAsync(_fs, new GuardianAbodeResidentRequestState.PendingGuardianAbodeResidentTransferRequest
        {
            RequestId = "resident_transfer_req_1",
            ResidentId = "resident_liora",
            ResidentName = "Лиора",
            SourceGuardianId = "guardian_alpha",
            SourceGuardianName = "Азалия",
            SourceAbodeId = "abode_alpha",
            SourceAbodeName = "Лазурная Обитель",
            TargetGuardianId = "guardian_beta",
            TargetGuardianName = "Мириэль",
            TargetAbodeId = "abode_beta",
            TargetAbodeName = "Сад Перекрёстков",
            AbodeDevotionLevel = 12,
            AbodeDevotionTier = "alienated",
            Restlessness = 84,
            MigrationState = "ready_to_transfer",
            TransferMode = GuardianAbodeResidentState.TransferModeAcceptedTransfer,
            SelectionMode = GuardianAbodeResidentRequestState.TransferSelectionModeCompetitionRecommended,
            CompetitionScore = 78,
            CompetitionLabel = GuardianAbodeResidentState.TransferCompetitionLabelStrongPull,
            CompetitionReason = "цель заметно сильнее текущей Обители и обещает более устойчивый порядок.",
            CreatedAtTurn = 41,
            CreatedAtUtc = "2026-04-16T04:41:00Z"
        });

        var reminder = await GuardianAbodeResidentRequestState.BuildSystemReminderFragmentAsync(_fs, "Chaos Sea");

        Assert.NotNull(reminder);
        Assert.Contains("ABODE RESIDENT TRANSFER REQUESTS:", reminder, StringComparison.Ordinal);
        Assert.Contains("resident=Лиора", reminder, StringComparison.Ordinal);
        Assert.Contains("pending_guardian_abode_resident_transfers.json", reminder, StringComparison.Ordinal);
        Assert.Contains("transferReceipts[]", reminder, StringComparison.Ordinal);
        Assert.Contains("selection=системная рекомендация", reminder, StringComparison.Ordinal);
        Assert.Contains("competition=сильный зов 78/100", reminder, StringComparison.Ordinal);
        Assert.Contains("requestId=resident_transfer_req_1", reminder, StringComparison.Ordinal);
        Assert.Contains("createdAtTurn=41", reminder, StringComparison.Ordinal);
        Assert.Contains("Full pending resident-transfer DTO", reminder, StringComparison.Ordinal);
        Assert.Contains("\"selectionMode\": \"competition_recommended\"", reminder, StringComparison.Ordinal);
        Assert.Contains("\"competitionReason\": \"цель заметно сильнее текущей Обители и обещает более устойчивый порядок.\"", reminder, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildSystemReminderFragmentAsync_AfterlifeRosterAndInteractionIncludeFullDtos()
    {
        await GuardianAbodeResidentRequestState.WriteResidentsRequestAsync(_fs, new GuardianAbodeResidentRequestState.PendingGuardianAbodeResidentsRequest
        {
            RequestId = "resident_roster_req_1",
            GuardianId = "guardian_alpha",
            GuardianName = "Азалия",
            AbodeId = "abode_alpha",
            AbodeName = "Лазурная Обитель",
            CurrentReputation = 42,
            RequestMode = GuardianAbodeResidentRequestState.ResidentsRequestModeFounderAttraction,
            FounderFeatureTitle = "Зов новой мантии",
            FounderFeatureSummary = "Обитель ищет первых резидентов.",
            CreatedAtTurn = 37,
            CreatedAtUtc = "2026-04-16T04:37:00Z"
        });
        await GuardianAbodeResidentRequestState.WriteInteractionRequestAsync(_fs, new GuardianAbodeResidentRequestState.PendingGuardianAbodeResidentInteractionRequest
        {
            RequestId = "resident_interaction_req_1",
            GuardianId = "guardian_alpha",
            GuardianName = "Азалия",
            AbodeId = "abode_alpha",
            AbodeName = "Лазурная Обитель",
            ResidentId = "resident_liora",
            ResidentName = "Лиора",
            InteractionType = GuardianAbodeResidentState.InteractionTypeHistory,
            CreatedAtTurn = 38,
            CreatedAtUtc = "2026-04-16T04:38:00Z"
        });

        var reminder = await GuardianAbodeResidentRequestState.BuildSystemReminderFragmentAsync(_fs, "Chaos Sea");

        Assert.NotNull(reminder);
        Assert.Contains("ABODE RESIDENT ROSTER REQUESTS:", reminder, StringComparison.Ordinal);
        Assert.Contains("ABODE RESIDENT INTERACTION REQUESTS:", reminder, StringComparison.Ordinal);
        Assert.Contains("requestId=resident_roster_req_1", reminder, StringComparison.Ordinal);
        Assert.Contains("currentReputation=42", reminder, StringComparison.Ordinal);
        Assert.Contains("Full pending resident-roster DTO", reminder, StringComparison.Ordinal);
        Assert.Contains("\"founderFeatureSummary\": \"Обитель ищет первых резидентов.\"", reminder, StringComparison.Ordinal);
        Assert.Contains("requestId=resident_interaction_req_1", reminder, StringComparison.Ordinal);
        Assert.Contains("Full pending resident-interaction DTO", reminder, StringComparison.Ordinal);
        Assert.Contains("\"residentId\": \"resident_liora\"", reminder, StringComparison.Ordinal);
        Assert.Contains("\"createdAtUtc\": \"2026-04-16T04:38:00Z\"", reminder, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildResidentsRosterPendingGmActionText_IncludesRequestIdentityAndCreationMetadata()
    {
        var text = GuardianAbodeResidentRequestState.BuildResidentsRosterPendingGmActionText(new GuardianAbodeResidentRequestState.PendingGuardianAbodeResidentsRequest
        {
            RequestId = "resident_roster_req_text",
            GuardianId = "guardian_alpha",
            GuardianName = "Азалия",
            AbodeId = "abode_alpha",
            AbodeName = "Лазурная Обитель",
            CurrentReputation = 51,
            RequestMode = GuardianAbodeResidentRequestState.ResidentsRequestModeStandardRoster,
            CreatedAtTurn = 39,
            CreatedAtUtc = "2026-04-16T04:39:00Z"
        });

        Assert.Contains("requestId=resident_roster_req_text", text, StringComparison.Ordinal);
        Assert.Contains("currentReputation=51", text, StringComparison.Ordinal);
        Assert.Contains("requestMode=standard_roster", text, StringComparison.Ordinal);
        Assert.Contains("createdAtTurn=39", text, StringComparison.Ordinal);
        Assert.Contains("createdAtUtc=2026-04-16T04:39:00Z", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildSystemReminderFragmentAsync_ReadyToTransferResident_SurfacesCompetitionTarget()
    {
        await WriteJsonAsync("game_state/meta/guardians.json", new
        {
            guardians = new object[]
            {
                new
                {
                    guardianId = "guardian_alpha",
                    canonicalName = "Азалия",
                    abode = new { abodeId = "abode_alpha", name = "Лазурная Обитель" },
                    abodePower = new { currentPower = 18 }
                },
                new
                {
                    guardianId = "guardian_beta",
                    canonicalName = "Мириэль",
                    abode = new { abodeId = "abode_beta", name = "Сад Перекрёстков" },
                    abodePower = new { currentPower = 78 }
                }
            }
        });
        await WriteJsonAsync(GuardianAbodeResidentState.StatePath, new
        {
            entries = new object[]
            {
                new
                {
                    residentId = "resident_liora",
                    guardianId = "guardian_alpha",
                    abodeId = "abode_alpha",
                    displayName = "Лиора",
                    residentKind = "wayfaring_soul",
                    originType = "traveler_soul",
                    roleLabel = "Вестница",
                    summary = "Слушает нити дорог.",
                    bondLevel = 44,
                    bondTier = "trusted",
                    canGrantCompanionRelic = true,
                    bondRewardState = "none",
                    linkedSoulQuestId = "",
                    grantedRelicId = "",
                    historyRevealed = true,
                    isPresent = true,
                    abodeDisposition = new
                    {
                        powerSensitivity = "high",
                        migrationDisposition = "opportunistic",
                        communalOrientation = "medium",
                        stabilityNeed = "high"
                    },
                    abodeDevotionLevel = 11,
                    abodeDevotionTier = "alienated",
                    restlessness = 82,
                    migrationState = "ready_to_transfer",
                    mortalWorldImprint = new
                    {
                        originWorldSummary = "Бывшая гонец при храме дорог.",
                        futureCompanionPrompt = "Swift wanderer"
                    }
                },
                new
                {
                    residentId = "resident_beta_1",
                    guardianId = "guardian_beta",
                    abodeId = "abode_beta",
                    displayName = "Ирис",
                    residentKind = "attendant_spirit",
                    originType = "native_spirit",
                    roleLabel = "Садовница",
                    summary = "Удерживает перекрёстки троп.",
                    bondLevel = 20,
                    bondTier = "stranger",
                    canGrantCompanionRelic = false,
                    bondRewardState = "none",
                    linkedSoulQuestId = "",
                    grantedRelicId = "",
                    historyRevealed = false,
                    isPresent = true,
                    mortalWorldImprint = new
                    {
                        originWorldSummary = "Дух сада.",
                        futureCompanionPrompt = "Garden spirit"
                    }
                }
            }
        });

        var reminder = await GuardianAbodeResidentRequestState.BuildSystemReminderFragmentAsync(_fs, "Chaos Sea");

        Assert.NotNull(reminder);
        Assert.Contains("bestTarget=Мириэль / Сад Перекрёстков", reminder, StringComparison.Ordinal);
        Assert.Contains("competition=сильный зов", reminder, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildSystemReminderFragmentAsync_PendingTransferRequest_SuppressesConflictingLiveCompetitionTarget()
    {
        await WriteJsonAsync("game_state/meta/guardians.json", new
        {
            guardians = new object[]
            {
                new
                {
                    guardianId = "guardian_alpha",
                    canonicalName = "Азалия",
                    abode = new { abodeId = "abode_alpha", name = "Лазурная Обитель" },
                    abodePower = new { currentPower = 18 }
                },
                new
                {
                    guardianId = "guardian_beta",
                    canonicalName = "Мириэль",
                    abode = new { abodeId = "abode_beta", name = "Сад Перекрёстков" },
                    abodePower = new { currentPower = 78 }
                },
                new
                {
                    guardianId = "guardian_gamma",
                    canonicalName = "Севериан",
                    abode = new { abodeId = "abode_gamma", name = "Тихая Пристань" },
                    abodePower = new { currentPower = 52 }
                }
            }
        });
        await WriteJsonAsync(GuardianAbodeResidentState.StatePath, new
        {
            entries = new object[]
            {
                new
                {
                    residentId = "resident_liora",
                    guardianId = "guardian_alpha",
                    abodeId = "abode_alpha",
                    displayName = "Лиора",
                    residentKind = "wayfaring_soul",
                    originType = "traveler_soul",
                    roleLabel = "Вестница",
                    summary = "Слушает нити дорог.",
                    bondLevel = 44,
                    bondTier = "trusted",
                    canGrantCompanionRelic = true,
                    bondRewardState = "none",
                    linkedSoulQuestId = "",
                    grantedRelicId = "",
                    historyRevealed = true,
                    isPresent = true,
                    abodeDisposition = new
                    {
                        powerSensitivity = "high",
                        migrationDisposition = "opportunistic",
                        communalOrientation = "medium",
                        stabilityNeed = "high"
                    },
                    abodeDevotionLevel = 11,
                    abodeDevotionTier = "alienated",
                    restlessness = 82,
                    migrationState = "ready_to_transfer",
                    mortalWorldImprint = new
                    {
                        originWorldSummary = "Бывшая гонец при храме дорог.",
                        futureCompanionPrompt = "Swift wanderer"
                    }
                },
                new
                {
                    residentId = "resident_beta_1",
                    guardianId = "guardian_beta",
                    abodeId = "abode_beta",
                    displayName = "Ирис",
                    residentKind = "attendant_spirit",
                    originType = "native_spirit",
                    roleLabel = "Садовница",
                    summary = "Удерживает перекрёстки троп.",
                    bondLevel = 20,
                    bondTier = "stranger",
                    canGrantCompanionRelic = false,
                    bondRewardState = "none",
                    linkedSoulQuestId = "",
                    grantedRelicId = "",
                    historyRevealed = false,
                    isPresent = true,
                    mortalWorldImprint = new
                    {
                        originWorldSummary = "Дух сада.",
                        futureCompanionPrompt = "Garden spirit"
                    }
                }
            }
        });
        await GuardianAbodeResidentRequestState.WriteTransferRequestAsync(_fs, new GuardianAbodeResidentRequestState.PendingGuardianAbodeResidentTransferRequest
        {
            RequestId = "resident_transfer_req_2",
            ResidentId = "resident_liora",
            ResidentName = "Лиора",
            SourceGuardianId = "guardian_alpha",
            SourceGuardianName = "Азалия",
            SourceAbodeId = "abode_alpha",
            SourceAbodeName = "Лазурная Обитель",
            TargetGuardianId = "guardian_gamma",
            TargetGuardianName = "Севериан",
            TargetAbodeId = "abode_gamma",
            TargetAbodeName = "Тихая Пристань",
            AbodeDevotionLevel = 11,
            AbodeDevotionTier = "alienated",
            Restlessness = 82,
            MigrationState = "ready_to_transfer",
            TransferMode = GuardianAbodeResidentState.TransferModeAcceptedTransfer,
            SelectionMode = GuardianAbodeResidentRequestState.TransferSelectionModeManualOverride,
            CompetitionScore = 41,
            CompetitionLabel = GuardianAbodeResidentState.TransferCompetitionLabelWeakPull,
            CompetitionReason = "система видит слабый зов, но хранитель и резидент всё равно выбирают эту цель.",
            CreatedAtTurn = 42,
            CreatedAtUtc = "2026-04-16T05:42:00Z"
        });

        var reminder = await GuardianAbodeResidentRequestState.BuildSystemReminderFragmentAsync(_fs, "Chaos Sea");

        Assert.NotNull(reminder);
        Assert.Contains("transferRequestPending=explicit transfer request already exists; see transfer request block below", reminder, StringComparison.Ordinal);
        Assert.DoesNotContain("bestTarget=Мириэль / Сад Перекрёстков", reminder, StringComparison.Ordinal);
        Assert.Contains("targetGuardian=Севериан (guardian_gamma)", reminder, StringComparison.Ordinal);
        Assert.Contains("selection=ручной выбор поверх слабого системного зова", reminder, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildSystemReminderFragmentAsync_PressureRedirect_PrioritizesMatchingTransferRequestWithinVisibleWindow()
    {
        await WriteJsonAsync("game_state/meta/guardians.json", new
        {
            guardians = new object[]
            {
                new
                {
                    guardianId = "guardian_alpha",
                    canonicalName = "Азалия",
                    abode = new { abodeId = "abode_alpha", name = "Лазурная Обитель" },
                    abodePower = new { currentPower = 18 }
                }
            }
        });
        await WriteJsonAsync(GuardianAbodeResidentState.StatePath, new
        {
            entries = new object[]
            {
                new
                {
                    residentId = "resident_liora",
                    guardianId = "guardian_alpha",
                    abodeId = "abode_alpha",
                    displayName = "Лиора",
                    residentKind = "wayfaring_soul",
                    originType = "traveler_soul",
                    roleLabel = "Вестница",
                    summary = "Слушает нити дорог.",
                    bondLevel = 44,
                    bondTier = "trusted",
                    canGrantCompanionRelic = true,
                    bondRewardState = "none",
                    linkedSoulQuestId = "",
                    grantedRelicId = "",
                    historyRevealed = true,
                    isPresent = true,
                    abodeDisposition = new
                    {
                        powerSensitivity = "high",
                        migrationDisposition = "opportunistic",
                        communalOrientation = "medium",
                        stabilityNeed = "high"
                    },
                    abodeDevotionLevel = 11,
                    abodeDevotionTier = "alienated",
                    restlessness = 82,
                    migrationState = "ready_to_transfer",
                    mortalWorldImprint = new
                    {
                        originWorldSummary = "Бывшая гонец при храме дорог.",
                        futureCompanionPrompt = "Swift wanderer"
                    }
                }
            }
        });

        for (var index = 0; index < 5; index++)
        {
            await GuardianAbodeResidentRequestState.WriteTransferRequestAsync(_fs, new GuardianAbodeResidentRequestState.PendingGuardianAbodeResidentTransferRequest
            {
                RequestId = $"resident_transfer_other_{index}",
                ResidentId = $"resident_other_{index}",
                ResidentName = $"Другой {index}",
                SourceGuardianId = "guardian_alpha",
                SourceGuardianName = "Азалия",
                SourceAbodeId = "abode_alpha",
                SourceAbodeName = "Лазурная Обитель",
                TargetGuardianId = $"guardian_target_{index}",
                TargetGuardianName = $"Цель {index}",
                TargetAbodeId = $"abode_target_{index}",
                TargetAbodeName = $"Приют {index}",
                AbodeDevotionLevel = 10,
                AbodeDevotionTier = "alienated",
                Restlessness = 85,
                MigrationState = "ready_to_transfer",
                TransferMode = GuardianAbodeResidentState.TransferModeAcceptedTransfer,
                CreatedAtTurn = 41 + index,
                CreatedAtUtc = $"2026-04-16T04:4{index}:00Z"
            });
        }

        await GuardianAbodeResidentRequestState.WriteTransferRequestAsync(_fs, new GuardianAbodeResidentRequestState.PendingGuardianAbodeResidentTransferRequest
        {
            RequestId = "resident_transfer_liora",
            ResidentId = "resident_liora",
            ResidentName = "Лиора",
            SourceGuardianId = "guardian_alpha",
            SourceGuardianName = "Азалия",
            SourceAbodeId = "abode_alpha",
            SourceAbodeName = "Лазурная Обитель",
            TargetGuardianId = "guardian_gamma",
            TargetGuardianName = "Севериан",
            TargetAbodeId = "abode_gamma",
            TargetAbodeName = "Тихая Пристань",
            AbodeDevotionLevel = 11,
            AbodeDevotionTier = "alienated",
            Restlessness = 82,
            MigrationState = "ready_to_transfer",
            TransferMode = GuardianAbodeResidentState.TransferModeAcceptedTransfer,
            SelectionMode = GuardianAbodeResidentRequestState.TransferSelectionModeManualOverride,
            CompetitionScore = 41,
            CompetitionLabel = GuardianAbodeResidentState.TransferCompetitionLabelWeakPull,
            CompetitionReason = "цель выбрана вручную вопреки слабому системному зову.",
            CreatedAtTurn = 99,
            CreatedAtUtc = "2026-04-16T04:59:00Z"
        });

        var reminder = await GuardianAbodeResidentRequestState.BuildSystemReminderFragmentAsync(_fs, "Chaos Sea");

        Assert.NotNull(reminder);
        Assert.Contains("transferRequestPending=explicit transfer request already exists; see transfer request block below", reminder, StringComparison.Ordinal);
        Assert.Contains("resident=Лиора (resident_liora)", reminder, StringComparison.Ordinal);
        Assert.Contains("targetGuardian=Севериан (guardian_gamma)", reminder, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EnsureManifestationRequestForCurrentIncarnationAsync_ImprintRelic_CreatesManifestationRequest()
    {
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Тестовая Душа",
            currentRealm = "Mortal World",
            currentIncarnation = 3,
            soulRelics = new
            {
                equipped = new object[]
                {
                    new
                    {
                        relicId = "relic_imprint_001",
                        name = "Печать Старого Друга",
                        rarity = "Legendary",
                        slot = "Neck",
                        soulImprint = new
                        {
                            imprintId = "imprint_companion_001",
                            NPCName = "Тарен",
                            description = "Опытный спутник из прошлой жизни, привыкший идти рядом с душой.",
                            coreTraitsPreserved = new[] { "верность", "рассудительность" }
                        }
                    }
                },
                stored = Array.Empty<object>()
            }
        });

        await GuardianAbodeResidentRequestState.EnsureManifestationRequestForCurrentIncarnationAsync(_fs, "Mortal World");

        var requestRaw = await _fs.ReadFileAsync(GuardianAbodeResidentRequestState.PendingManifestationRequestPath);
        Assert.NotNull(requestRaw);
        Assert.Contains("\"manifestationSource\": \"imprint_relic\"", requestRaw, StringComparison.Ordinal);
        Assert.Contains("\"relicId\": \"relic_imprint_001\"", requestRaw, StringComparison.Ordinal);
        Assert.Contains("\"sourceImprintId\": \"imprint_companion_001\"", requestRaw, StringComparison.Ordinal);
        Assert.Contains("\"companionNameHint\": \"Тарен\"", requestRaw, StringComparison.Ordinal);
        Assert.Contains("\"targetIncarnation\": 3", requestRaw, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EnsureManifestationRequestForCurrentIncarnationAsync_MalformedCurrentCompanionSeed_DoesNotCreateManifestationRequest()
    {
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Тестовая Душа",
            currentRealm = "Mortal World",
            currentIncarnation = 2,
            soulRelics = new
            {
                equipped = new object[]
                {
                    new
                    {
                        relicId = "relic_companion_echo_broken",
                        name = "Эхо Без Семени",
                        rarity = "Epic",
                        relicType = GuardianAbodeResidentState.RelicTypeCompanionEcho,
                        companionSeed = new
                        {
                            sourceResidentId = "resident_echo_001"
                        }
                    }
                },
                stored = Array.Empty<object>()
            }
        });

        await GuardianAbodeResidentRequestState.EnsureManifestationRequestForCurrentIncarnationAsync(_fs, "Mortal World");

        Assert.False(_fs.FileExists(GuardianAbodeResidentRequestState.PendingManifestationRequestPath));
        var soulRaw = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        Assert.DoesNotContain("\"companionManifestationStatus\"", soulRaw, StringComparison.Ordinal);
        Assert.DoesNotContain("\"companionManifestationLastRequestedIncarnation\"", soulRaw, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EnsureManifestationRequestForCurrentIncarnationAsync_MalformedEmbeddedImprint_DoesNotCreateManifestationRequest()
    {
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Тестовая Душа",
            currentRealm = "Mortal World",
            currentIncarnation = 2,
            soulRelics = new
            {
                equipped = new object[]
                {
                    new
                    {
                        relicId = "relic_imprint_broken",
                        name = "Печать Без Черт",
                        rarity = "Rare",
                        soulImprint = new
                        {
                            imprintId = "imprint_guard_broken",
                            npcName = "Страж Кел",
                            description = "Бывший страж северных ворот."
                        }
                    }
                },
                stored = Array.Empty<object>()
            }
        });

        await GuardianAbodeResidentRequestState.EnsureManifestationRequestForCurrentIncarnationAsync(_fs, "Mortal World");

        Assert.False(_fs.FileExists(GuardianAbodeResidentRequestState.PendingManifestationRequestPath));
        var soulRaw = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        Assert.DoesNotContain("\"companionManifestationStatus\"", soulRaw, StringComparison.Ordinal);
        Assert.DoesNotContain("\"companionManifestationLastRequestedIncarnation\"", soulRaw, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EnsureManifestationRequestForCurrentIncarnationAsync_MalformedCurrentIncarnation_DoesNotCreateManifestationRequest()
    {
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Тестовая Душа",
            currentRealm = "Mortal World",
            currentIncarnation = new
            {
                bogus = 2
            },
            soulRelics = new
            {
                equipped = new object[]
                {
                    new
                    {
                        relicId = "relic_companion_echo_invalid_incarnation",
                        name = "Эхо Сломанной Инкарнации",
                        rarity = "Rare",
                        relicType = GuardianAbodeResidentState.RelicTypeCompanionEcho,
                        companionSeed = new
                        {
                            sourceResidentId = "resident_echo_001",
                            sourceGuardianId = "guardian_social_001",
                            sourceAbodeId = "abode_social_001",
                            companionNameHint = "Лиора",
                            originWorldSummary = "Путь между мирами.",
                            futureCompanionPrompt = "Swift wanderer with ember-thread cloak"
                        }
                    }
                },
                stored = Array.Empty<object>()
            }
        });

        await GuardianAbodeResidentRequestState.EnsureManifestationRequestForCurrentIncarnationAsync(_fs, "Mortal World");

        Assert.False(_fs.FileExists(GuardianAbodeResidentRequestState.PendingManifestationRequestPath));
        var soulRaw = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        Assert.DoesNotContain("\"companionManifestationStatus\"", soulRaw, StringComparison.Ordinal);
        Assert.DoesNotContain("\"companionManifestationLastRequestedIncarnation\"", soulRaw, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EnsureManifestationRequestForCurrentIncarnationAsync_MalformedSiblingCanonicalRoot_DoesNotCreateManifestationRequest()
    {
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Тестовая Душа",
            currentRealm = "Mortal World",
            currentIncarnation = 2,
            inkFeathers = new
            {
                current = "5"
            },
            soulRelics = new
            {
                equipped = new object[]
                {
                    new
                    {
                        relicId = "relic_companion_echo_invalid_sibling_root",
                        name = "Эхо Сломанного Корня",
                        rarity = "Rare",
                        relicType = GuardianAbodeResidentState.RelicTypeCompanionEcho,
                        companionSeed = new
                        {
                            sourceResidentId = "resident_echo_001",
                            sourceGuardianId = "guardian_social_001",
                            sourceAbodeId = "abode_social_001",
                            companionNameHint = "Лиора",
                            originWorldSummary = "Путь между мирами.",
                            futureCompanionPrompt = "Swift wanderer with ember-thread cloak"
                        }
                    }
                },
                stored = Array.Empty<object>()
            }
        });

        await GuardianAbodeResidentRequestState.EnsureManifestationRequestForCurrentIncarnationAsync(_fs, "Mortal World");

        Assert.False(_fs.FileExists(GuardianAbodeResidentRequestState.PendingManifestationRequestPath));
        var soulRaw = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        Assert.DoesNotContain("\"companionManifestationStatus\"", soulRaw, StringComparison.Ordinal);
        Assert.DoesNotContain("\"companionManifestationLastRequestedIncarnation\"", soulRaw, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EnsureManifestationRequestForCurrentIncarnationAsync_RecordLifeCompletionWithOrphanedPendingSnapshot_DoesNotCreateManifestationRequest()
    {
        await WriteJsonAsync("game_state/control/pending_turn_snapshot/game_state/meta/soul_state.json", new
        {
            currentRealm = "Mortal World",
            currentIncarnation = 2
        });
        await WriteJsonAsync("game_state/control/life_transitions.json", new
        {
            reason = "Death",
            summary = "Жизнь завершена."
        });
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Тестовая Душа",
            currentRealm = "Mortal World",
            currentIncarnation = 2,
            metaStateUpdates = new
            {
                lifeTransitions = new
                {
                    recordLifeCompletion = new
                    {
                        characterFinalState = new { causeOfDeath = "Test" },
                        majorAchievements = Array.Empty<string>(),
                        relationshipsFormed = Array.Empty<object>(),
                        moralChoices = Array.Empty<object>(),
                        skillsLearned = Array.Empty<string>(),
                        enlightenmentGained = 0
                    }
                }
            },
            soulRelics = new
            {
                equipped = new object[]
                {
                    new
                    {
                        relicId = "relic_companion_echo_orphaned_snapshot",
                        name = "Эхо Осиротевшего Снэпшота",
                        rarity = "Epic",
                        relicType = GuardianAbodeResidentState.RelicTypeCompanionEcho,
                        companionSeed = new
                        {
                            sourceResidentId = "resident_echo_001",
                            sourceGuardianId = "guardian_social_001",
                            sourceAbodeId = "abode_social_001",
                            companionNameHint = "Лиора",
                            originWorldSummary = "Бывшая гонец при храме семи дорог.",
                            futureCompanionPrompt = "Swift wanderer with ember-thread cloak",
                            bondReason = "Она всегда возвращалась к тем, кого однажды назвала своими.",
                            coreTraits = new[] { "верность", "смелость" },
                            archetypeHints = new[] { "courier", "pathfinder" },
                            appearanceMotifs = new[] { "ember-thread cloak" }
                        }
                    }
                },
                stored = Array.Empty<object>()
            }
        });

        await GuardianAbodeResidentRequestState.EnsureManifestationRequestForCurrentIncarnationAsync(_fs, "Mortal World");

        Assert.False(_fs.FileExists(GuardianAbodeResidentRequestState.PendingManifestationRequestPath));
        var soulRaw = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        Assert.DoesNotContain("\"companionManifestationStatus\"", soulRaw, StringComparison.Ordinal);
        Assert.DoesNotContain("\"companionManifestationLastRequestedIncarnation\"", soulRaw, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EnsureManifestationRequestForCurrentIncarnationAsync_PrunesUnsafeSameRelicAddButPreservesUnrelatedPendingMetaWork()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Тестовая Душа",
          "currentRealm": "Mortal World",
          "currentIncarnation": 2,
          "crossIncarnationData": {
            "legacyThreadId": "thread_alpha"
          },
          "metaStateUpdates": {
            "soulRelicOperations": {
              "addRelic": {
                "relicId": "relic_companion_echo_001"
              },
              "updateRelicField": {
                "relicId": "relic_companion_echo_001",
                "field": "companionManifestationStatus",
                "newValue": "outdated"
              },
              "removeRelic": {
                "relicId": "relic_keep"
              }
            },
            "memoryLegacyGrant": {
              "legacyId": "legacy_keep",
              "legacyType": "startingCharacteristicBonus",
              "sourceLifeHint": "life_001",
              "characteristic": "strength",
              "bonus": 2
            }
          },
          "afterlifeArchiveUpdates": [],
          "archiveActionResolutions": [],
          "soulRelics": {
            "equipped": [
              {
                "relicId": "relic_companion_echo_001",
                "name": "Эхо Вестницы",
                "rarity": "Epic",
                "slot": "Neck",
                "relicType": "companion_echo",
                "companionSeed": {
                  "sourceResidentId": "resident_echo_001",
                  "sourceGuardianId": "guardian_social_001",
                  "sourceAbodeId": "abode_social_001",
                  "companionNameHint": "Лиора",
                  "originWorldSummary": "Бывшая гонец при храме семи дорог.",
                  "futureCompanionPrompt": "Swift wanderer with ember-thread cloak"
                }
              }
            ],
            "stored": []
          }
        }
        """);

        await GuardianAbodeResidentRequestState.EnsureManifestationRequestForCurrentIncarnationAsync(_fs, "Mortal World");

        using var soulDoc = JsonDocument.Parse((await _fs.ReadFileAsync("game_state/meta/soul_state.json"))!);
        Assert.False(soulDoc.RootElement.TryGetProperty("crossIncarnationData", out _));
        var metaStateUpdates = soulDoc.RootElement.GetProperty("metaStateUpdates");
        var soulRelicOperations = metaStateUpdates.GetProperty("soulRelicOperations");
        Assert.False(soulRelicOperations.TryGetProperty("addRelic", out _));
        Assert.False(soulRelicOperations.TryGetProperty("updateRelicField", out _));
        Assert.True(soulRelicOperations.TryGetProperty("removeRelic", out var removeRelic));
        Assert.Equal("relic_keep", removeRelic.GetProperty("relicId").GetString());
        Assert.True(metaStateUpdates.TryGetProperty("memoryLegacyGrant", out _));
        Assert.True(soulDoc.RootElement.TryGetProperty("afterlifeArchiveUpdates", out _));
        Assert.True(soulDoc.RootElement.TryGetProperty("archiveActionResolutions", out _));
    }

    [Fact]
    public async Task EnsureHealthyAsync_MaterializedCompanion_PrunesUnsafeSameRelicAddAndMarksRelicResolved()
    {
        await WriteJsonAsync(GuardianAbodeResidentRequestState.PendingManifestationRequestPath, new
        {
            requests = new object[]
            {
                new
                {
                    requestId = "resident_manifest_req_1",
                    manifestationSource = "resident_relic",
                    relicId = "relic_companion_echo_001",
                    relicName = "Эхо Вестницы",
                    sourceResidentId = "resident_echo_001",
                    sourceGuardianId = "guardian_social_001",
                    sourceGuardianName = "Азалия",
                    targetIncarnation = 2,
                    companionNameHint = "Лиора",
                    originWorldSummary = "Бывшая гонец при храме семи дорог.",
                    futureCompanionPrompt = "Swift wanderer with ember-thread cloak",
                    bondReason = "Она всегда возвращалась к тем, кого однажды назвала своими.",
                    coreTraits = new[] { "верность", "смелость" },
                    archetypeHints = new[] { "courier" },
                    appearanceMotifs = new[] { "ember-thread cloak" },
                    createdAtUtc = "2026-03-27T00:00:00Z"
                }
            }
        });

        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Тестовая Душа",
            currentRealm = "Mortal World",
            currentIncarnation = 2,
            crossIncarnationData = new
            {
                legacyThreadId = "thread_alpha"
            },
            metaStateUpdates = new
            {
                soulRelicOperations = new
                {
                    addRelic = new
                    {
                        relicId = "relic_companion_echo_001"
                    },
                    updateRelicField = new
                    {
                        relicId = "relic_companion_echo_001",
                        field = "companionManifestationResolvedNpcId",
                        newValue = "npc_outdated"
                    },
                    removeRelic = new
                    {
                        relicId = "relic_keep"
                    }
                },
                memoryLegacyGrant = new
                {
                    legacyId = "legacy_keep",
                    legacyType = "startingCharacteristicBonus",
                    sourceLifeHint = "life_001",
                    characteristic = "strength",
                    bonus = 2
                }
            },
            afterlifeArchiveUpdates = Array.Empty<object>(),
            archiveActionResolutions = Array.Empty<object>(),
            soulRelics = new
            {
                equipped = new object[]
                {
                    new
                    {
                        relicId = "relic_companion_echo_001",
                        name = "Эхо Вестницы",
                        rarity = "Epic",
                        slot = "Neck",
                        relicType = GuardianAbodeResidentState.RelicTypeCompanionEcho,
                        companionManifestationStatus = "pending",
                        lastManifestationRequestId = "resident_manifest_req_1",
                        companionSeed = new
                        {
                            sourceResidentId = "resident_echo_001",
                            sourceGuardianId = "guardian_social_001",
                            sourceAbodeId = "abode_social_001",
                            companionNameHint = "Лиора",
                            originWorldSummary = "Бывшая гонец при храме семи дорог.",
                            futureCompanionPrompt = "Swift wanderer with ember-thread cloak"
                        }
                    }
                },
                stored = Array.Empty<object>()
            }
        });

        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_core.json", """
        {
          "UpdateNPCs": [
            {
              "npcId": "npc_manifested_liora",
              "npcName": "Лиора",
              "introducedAtTurn": 18,
              "introducedAtUtc": "2026-03-27T00:18:00Z",
              "sourceCompanionRelicId": "relic_companion_echo_001",
              "sourceAfterlifeResidentId": "resident_echo_001"
            }
          ]
        }
        """);

        await GuardianAbodeResidentRequestState.EnsureHealthyAsync(_fs, "Mortal World");

        var pendingRaw = await _fs.ReadFileAsync(GuardianAbodeResidentRequestState.PendingManifestationRequestPath);
        Assert.True(string.IsNullOrWhiteSpace(pendingRaw), "Pending manifestation request should be cleared after materialization.");

        using var soulDoc = JsonDocument.Parse((await _fs.ReadFileAsync("game_state/meta/soul_state.json"))!);
        Assert.False(soulDoc.RootElement.TryGetProperty("crossIncarnationData", out _));
        var metaStateUpdates = soulDoc.RootElement.GetProperty("metaStateUpdates");
        var soulRelicOperations = metaStateUpdates.GetProperty("soulRelicOperations");
        Assert.False(soulRelicOperations.TryGetProperty("addRelic", out _));
        Assert.False(soulRelicOperations.TryGetProperty("updateRelicField", out _));
        Assert.True(soulRelicOperations.TryGetProperty("removeRelic", out var removeRelic));
        Assert.Equal("relic_keep", removeRelic.GetProperty("relicId").GetString());
        Assert.True(metaStateUpdates.TryGetProperty("memoryLegacyGrant", out _));
        Assert.True(soulDoc.RootElement.TryGetProperty("afterlifeArchiveUpdates", out _));
        Assert.True(soulDoc.RootElement.TryGetProperty("archiveActionResolutions", out _));
        var resolvedRelic = soulDoc.RootElement.GetProperty("soulRelics").GetProperty("equipped").EnumerateArray().Single();
        Assert.Equal("materialized", resolvedRelic.GetProperty("companionManifestationStatus").GetString());
        Assert.Equal("resident_manifest_req_1", resolvedRelic.GetProperty("companionManifestationResolvedRequestId").GetString());
        Assert.Equal("npc_manifested_liora", resolvedRelic.GetProperty("companionManifestationResolvedNpcId").GetString());
        Assert.Equal(18, resolvedRelic.GetProperty("companionManifestationResolvedAtTurn").GetInt32());
    }

    [Fact]
    public async Task EnsureHealthyAsync_MatchedNpcWithoutReadableSoulRelics_KeepsManifestationRequestRetryable()
    {
        await WriteJsonAsync(GuardianAbodeResidentRequestState.PendingManifestationRequestPath, new
        {
            requests = new object[]
            {
                new
                {
                    requestId = "resident_manifest_req_unreadable_relics",
                    manifestationSource = "resident_relic",
                    relicId = "relic_companion_echo_unreadable",
                    relicName = "Эхо Непрочитанной Вестницы",
                    sourceResidentId = "resident_echo_001",
                    sourceGuardianId = "guardian_social_001",
                    sourceGuardianName = "Азалия",
                    targetIncarnation = 2,
                    companionNameHint = "Лиора",
                    originWorldSummary = "Бывшая гонец при храме семи дорог.",
                    futureCompanionPrompt = "Swift wanderer with ember-thread cloak",
                    createdAtUtc = "2026-03-27T00:00:00Z"
                }
            }
        });

        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Тестовая Душа",
            currentRealm = "Mortal World",
            currentIncarnation = 2
        });

        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_core.json", """
        {
          "UpdateNPCs": [
            {
              "npcId": "npc_manifested_liora",
              "npcName": "Лиора",
              "introducedAtTurn": 18,
              "introducedAtUtc": "2026-03-27T00:18:00Z",
              "sourceCompanionRelicId": "relic_companion_echo_unreadable",
              "sourceAfterlifeResidentId": "resident_echo_001"
            }
          ]
        }
        """);

        await GuardianAbodeResidentRequestState.EnsureHealthyAsync(_fs, "Mortal World");

        var pendingRaw = await _fs.ReadFileAsync(GuardianAbodeResidentRequestState.PendingManifestationRequestPath);
        Assert.NotNull(pendingRaw);
        Assert.Contains("resident_manifest_req_unreadable_relics", pendingRaw, StringComparison.Ordinal);

        using var soulDoc = JsonDocument.Parse((await _fs.ReadFileAsync("game_state/meta/soul_state.json"))!);
        Assert.False(soulDoc.RootElement.TryGetProperty("soulRelics", out _));
    }

    [Fact]
    public async Task EnsureHealthyAsync_MaterializedCompanion_DoesNotDuplicateRelicOnNextNormalizationPass()
    {
        await WriteJsonAsync(GuardianAbodeResidentRequestState.PendingManifestationRequestPath, new
        {
            requests = new object[]
            {
                new
                {
                    requestId = "resident_manifest_req_replay",
                    manifestationSource = "resident_relic",
                    relicId = "relic_companion_echo_replay",
                    relicName = "Эхо Проводника",
                    sourceResidentId = "resident_echo_001",
                    sourceGuardianId = "guardian_social_001",
                    sourceGuardianName = "Азалия",
                    targetIncarnation = 2,
                    companionNameHint = "Лиора",
                    originWorldSummary = "Путь между мирами.",
                    futureCompanionPrompt = "Swift wanderer with ember-thread cloak",
                    createdAtUtc = "2026-03-27T00:00:00Z"
                }
            }
        });

        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Тестовая Душа",
            currentRealm = "Mortal World",
            currentIncarnation = 2,
            metaStateUpdates = new
            {
                soulRelicOperations = new
                {
                    addRelic = new
                    {
                        relicId = "relic_companion_echo_replay",
                        name = "Эхо Проводника",
                        rarity = "Epic",
                        slot = "Neck",
                        relicType = GuardianAbodeResidentState.RelicTypeCompanionEcho
                    },
                    updateRelicField = new
                    {
                        relicId = "relic_companion_echo_replay",
                        field = "companionManifestationResolvedNpcId",
                        newValue = "npc_outdated"
                    }
                }
            },
            soulRelics = new
            {
                equipped = new object[]
                {
                    new
                    {
                        relicId = "relic_companion_echo_replay",
                        name = "Эхо Проводника",
                        rarity = "Epic",
                        slot = "Neck",
                        relicType = GuardianAbodeResidentState.RelicTypeCompanionEcho,
                        companionManifestationStatus = "pending",
                        lastManifestationRequestId = "resident_manifest_req_replay",
                        companionSeed = new
                        {
                            sourceResidentId = "resident_echo_001",
                            sourceGuardianId = "guardian_social_001",
                            sourceAbodeId = "abode_social_001",
                            companionNameHint = "Лиора",
                            originWorldSummary = "Путь между мирами.",
                            futureCompanionPrompt = "Swift wanderer with ember-thread cloak"
                        }
                    }
                },
                stored = Array.Empty<object>()
            }
        });

        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_core.json", """
        {
          "UpdateNPCs": [
            {
              "npcId": "npc_manifested_replay",
              "npcName": "Лиора",
              "introducedAtTurn": 18,
              "introducedAtUtc": "2026-03-27T00:18:00Z",
              "sourceCompanionRelicId": "relic_companion_echo_replay",
              "sourceAfterlifeResidentId": "resident_echo_001"
            }
          ]
        }
        """);

        await GuardianAbodeResidentRequestState.EnsureHealthyAsync(_fs, "Mortal World");

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await normalizer.NormalizeAccumulatedStateAsync();

        using var soulDoc = JsonDocument.Parse((await _fs.ReadFileAsync("game_state/meta/soul_state.json"))!);
        var soulRelics = soulDoc.RootElement.GetProperty("soulRelics");
        var duplicateCount =
            soulRelics.GetProperty("equipped").EnumerateArray().Count(relic => relic.GetProperty("relicId").GetString() == "relic_companion_echo_replay") +
            soulRelics.GetProperty("stored").EnumerateArray().Count(relic => relic.GetProperty("relicId").GetString() == "relic_companion_echo_replay");
        Assert.Equal(1, duplicateCount);
    }

    [Fact]
    public async Task EnsureHealthyAsync_MaterializedStoredCompanion_PreservesSafeSameRelicAddAndDoesNotDuplicateOnReplay()
    {
        await WriteJsonAsync(GuardianAbodeResidentRequestState.PendingManifestationRequestPath, new
        {
            requests = new object[]
            {
                new
                {
                    requestId = "resident_manifest_req_stored",
                    manifestationSource = "resident_relic",
                    relicId = "relic_companion_echo_stored",
                    relicName = "Эхо Хранительницы",
                    sourceResidentId = "resident_echo_001",
                    sourceGuardianId = "guardian_social_001",
                    sourceGuardianName = "Азалия",
                    targetIncarnation = 2,
                    companionNameHint = "Лиора",
                    originWorldSummary = "Путь между мирами.",
                    futureCompanionPrompt = "Swift wanderer with ember-thread cloak",
                    createdAtUtc = "2026-03-27T00:00:00Z"
                }
            }
        });

        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Тестовая Душа",
            currentRealm = "Mortal World",
            currentIncarnation = 2,
            metaStateUpdates = new
            {
                soulRelicOperations = new
                {
                    addRelic = new
                    {
                        relicId = "relic_companion_echo_stored",
                        name = "Эхо Хранительницы",
                        rarity = "Epic",
                        relicType = GuardianAbodeResidentState.RelicTypeCompanionEcho
                    },
                    updateRelicField = new
                    {
                        relicId = "relic_companion_echo_stored",
                        field = "companionManifestationResolvedNpcId",
                        newValue = "npc_outdated"
                    }
                }
            },
            soulRelics = new
            {
                equipped = Array.Empty<object>(),
                stored = new object[]
                {
                    new
                    {
                        relicId = "relic_companion_echo_stored",
                        name = "Эхо Хранительницы",
                        rarity = "Epic",
                        relicType = GuardianAbodeResidentState.RelicTypeCompanionEcho,
                        companionManifestationStatus = "pending",
                        lastManifestationRequestId = "resident_manifest_req_stored",
                        companionSeed = new
                        {
                            sourceResidentId = "resident_echo_001",
                            sourceGuardianId = "guardian_social_001",
                            sourceAbodeId = "abode_social_001",
                            companionNameHint = "Лиора",
                            originWorldSummary = "Путь между мирами.",
                            futureCompanionPrompt = "Swift wanderer with ember-thread cloak"
                        }
                    }
                }
            }
        });

        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_core.json", """
        {
          "UpdateNPCs": [
            {
              "npcId": "npc_manifested_stored",
              "npcName": "Лиора",
              "introducedAtTurn": 18,
              "introducedAtUtc": "2026-03-27T00:18:00Z",
              "sourceCompanionRelicId": "relic_companion_echo_stored",
              "sourceAfterlifeResidentId": "resident_echo_001"
            }
          ]
        }
        """);

        await GuardianAbodeResidentRequestState.EnsureHealthyAsync(_fs, "Mortal World");

        using (var patchedSoulDoc = JsonDocument.Parse((await _fs.ReadFileAsync("game_state/meta/soul_state.json"))!))
        {
            var metaStateUpdates = patchedSoulDoc.RootElement.GetProperty("metaStateUpdates");
            var soulRelicOperations = metaStateUpdates.GetProperty("soulRelicOperations");
            Assert.True(soulRelicOperations.TryGetProperty("addRelic", out _));
            Assert.False(soulRelicOperations.TryGetProperty("updateRelicField", out _));
        }

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await normalizer.NormalizeAccumulatedStateAsync();

        using var soulDoc = JsonDocument.Parse((await _fs.ReadFileAsync("game_state/meta/soul_state.json"))!);
        var soulRelics = soulDoc.RootElement.GetProperty("soulRelics");
        var duplicateCount =
            soulRelics.GetProperty("equipped").EnumerateArray().Count(relic => relic.GetProperty("relicId").GetString() == "relic_companion_echo_stored") +
            soulRelics.GetProperty("stored").EnumerateArray().Count(relic => relic.GetProperty("relicId").GetString() == "relic_companion_echo_stored");
        Assert.Equal(1, duplicateCount);
    }

    [Fact]
    public async Task EnsureHealthyAsync_MalformedCurrentSoulRelics_KeepsManifestationRequestRetryable()
    {
        await WriteJsonAsync(GuardianAbodeResidentRequestState.PendingManifestationRequestPath, new
        {
            requests = new object[]
            {
                new
                {
                    requestId = "resident_manifest_req_broken",
                    manifestationSource = "resident_relic",
                    relicId = "relic_companion_echo_broken",
                    relicName = "Эхо Без Семени",
                    sourceResidentId = "resident_echo_001",
                    sourceGuardianId = "guardian_social_001",
                    sourceGuardianName = "Азалия",
                    targetIncarnation = 2,
                    companionNameHint = "Лиора",
                    originWorldSummary = "Бывшая гонец при храме семи дорог.",
                    futureCompanionPrompt = "Swift wanderer with ember-thread cloak",
                    createdAtUtc = "2026-03-27T00:00:00Z"
                }
            }
        });

        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            currentRealm = "Mortal World",
            currentIncarnation = 2,
            soulRelics = new
            {
                equipped = new object[]
                {
                    new
                    {
                        relicId = "relic_companion_echo_broken",
                        name = "Эхо Без Семени",
                        rarity = "Epic",
                        relicType = GuardianAbodeResidentState.RelicTypeCompanionEcho,
                        companionManifestationStatus = "pending",
                        lastManifestationRequestId = "resident_manifest_req_broken",
                        companionSeed = new
                        {
                            sourceResidentId = "resident_echo_001"
                        }
                    }
                },
                stored = Array.Empty<object>()
            }
        });

        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_core.json", """
        {
          "UpdateNPCs": [
            {
              "npcId": "npc_manifested_liora",
              "npcName": "Лиора",
              "sourceCompanionRelicId": "relic_companion_echo_broken",
              "sourceAfterlifeResidentId": "resident_echo_001"
            }
          ]
        }
        """);

        await GuardianAbodeResidentRequestState.EnsureHealthyAsync(_fs, "Mortal World");

        var pendingRaw = await _fs.ReadFileAsync(GuardianAbodeResidentRequestState.PendingManifestationRequestPath);
        Assert.Contains("\"requestId\": \"resident_manifest_req_broken\"", pendingRaw, StringComparison.Ordinal);

        var soulRaw = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        Assert.DoesNotContain("\"companionManifestationStatus\": \"materialized\"", soulRaw, StringComparison.Ordinal);
        Assert.DoesNotContain("\"companionManifestationResolvedRequestId\": \"resident_manifest_req_broken\"", soulRaw, StringComparison.Ordinal);
        Assert.DoesNotContain("\"companionManifestationResolvedNpcId\": \"npc_manifested_liora\"", soulRaw, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EnsureHealthyAsync_MalformedCurrentIncarnation_KeepsManifestationRequestRetryable()
    {
        await WriteJsonAsync(GuardianAbodeResidentRequestState.PendingManifestationRequestPath, new
        {
            requests = new object[]
            {
                new
                {
                    requestId = "resident_manifest_req_invalid_incarnation",
                    manifestationSource = "resident_relic",
                    relicId = "relic_companion_echo_invalid_incarnation",
                    relicName = "Эхо Сломанной Инкарнации",
                    sourceResidentId = "resident_echo_001",
                    sourceGuardianId = "guardian_social_001",
                    sourceGuardianName = "Азалия",
                    targetIncarnation = 2,
                    companionNameHint = "Лиора",
                    originWorldSummary = "Бывшая гонец при храме семи дорог.",
                    futureCompanionPrompt = "Swift wanderer with ember-thread cloak",
                    createdAtUtc = "2026-03-27T00:00:00Z"
                }
            }
        });

        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            currentRealm = "Mortal World",
            currentIncarnation = new
            {
                bogus = 2
            },
            soulRelics = new
            {
                equipped = new object[]
                {
                    new
                    {
                        relicId = "relic_companion_echo_invalid_incarnation",
                        name = "Эхо Сломанной Инкарнации",
                        rarity = "Epic",
                        relicType = GuardianAbodeResidentState.RelicTypeCompanionEcho,
                        companionManifestationStatus = "pending",
                        lastManifestationRequestId = "resident_manifest_req_invalid_incarnation",
                        companionSeed = new
                        {
                            sourceResidentId = "resident_echo_001",
                            sourceGuardianId = "guardian_social_001",
                            sourceAbodeId = "abode_social_001",
                            companionNameHint = "Лиора",
                            originWorldSummary = "Бывшая гонец при храме семи дорог.",
                            futureCompanionPrompt = "Swift wanderer with ember-thread cloak"
                        }
                    }
                },
                stored = Array.Empty<object>()
            }
        });

        await GuardianAbodeResidentRequestState.EnsureHealthyAsync(_fs, "Mortal World");

        var pendingRaw = await _fs.ReadFileAsync(GuardianAbodeResidentRequestState.PendingManifestationRequestPath);
        Assert.Contains("\"requestId\": \"resident_manifest_req_invalid_incarnation\"", pendingRaw, StringComparison.Ordinal);

        var soulRaw = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        Assert.DoesNotContain("\"companionManifestationStatus\": \"materialized\"", soulRaw, StringComparison.Ordinal);
        Assert.DoesNotContain("\"companionManifestationResolvedRequestId\": \"resident_manifest_req_invalid_incarnation\"", soulRaw, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EnsureHealthyAsync_LegacyAliasNpcSectionDoesNotResolveManifestationRequest()
    {
        await WriteJsonAsync(GuardianAbodeResidentRequestState.PendingManifestationRequestPath, new
        {
            requests = new object[]
            {
                new
                {
                    requestId = "resident_manifest_req_legacy_alias",
                    manifestationSource = "resident_relic",
                    relicId = "relic_companion_echo_legacy",
                    relicName = "Эхо Вестницы",
                    sourceResidentId = "resident_echo_001",
                    sourceGuardianId = "guardian_social_001",
                    sourceGuardianName = "Азалия",
                    targetIncarnation = 2,
                    companionNameHint = "Лиора",
                    originWorldSummary = "Бывшая гонец при храме семи дорог.",
                    futureCompanionPrompt = "Swift wanderer with ember-thread cloak",
                    createdAtUtc = "2026-03-27T00:00:00Z"
                }
            }
        });

        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Тестовая Душа",
            currentRealm = "Mortal World",
            currentIncarnation = 2,
            soulRelics = new
            {
                equipped = new object[]
                {
                    new
                    {
                        relicId = "relic_companion_echo_legacy",
                        name = "Эхо Вестницы",
                        rarity = "Epic",
                        slot = "Neck",
                        relicType = GuardianAbodeResidentState.RelicTypeCompanionEcho,
                        companionManifestationStatus = "pending",
                        lastManifestationRequestId = "resident_manifest_req_legacy_alias",
                        companionSeed = new
                        {
                            sourceResidentId = "resident_echo_001",
                            sourceGuardianId = "guardian_social_001",
                            sourceAbodeId = "abode_social_001",
                            companionNameHint = "Лиора",
                            originWorldSummary = "Бывшая гонец при храме семи дорог.",
                            futureCompanionPrompt = "Swift wanderer with ember-thread cloak"
                        }
                    }
                },
                stored = Array.Empty<object>()
            }
        });
        await WriteJsonAsync("game_state/npcs/npc_core.json", new
        {
            npcs = new object[]
            {
                new
                {
                    npcId = "npc_manifested_liora",
                    npcName = "Лиора",
                    introducedAtTurn = 18,
                    introducedAtUtc = "2026-03-27T00:18:00Z",
                    sourceCompanionRelicId = "relic_companion_echo_legacy",
                    sourceAfterlifeResidentId = "resident_echo_001"
                }
            }
        });

        await GuardianAbodeResidentRequestState.EnsureHealthyAsync(_fs, "Mortal World");

        var pendingRaw = await _fs.ReadFileAsync(GuardianAbodeResidentRequestState.PendingManifestationRequestPath);
        Assert.Contains("\"requestId\": \"resident_manifest_req_legacy_alias\"", pendingRaw, StringComparison.Ordinal);

        var soulRaw = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        Assert.DoesNotContain("\"companionManifestationStatus\": \"materialized\"", soulRaw, StringComparison.Ordinal);
        Assert.DoesNotContain("\"companionManifestationResolvedRequestId\": \"resident_manifest_req_legacy_alias\"", soulRaw, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EnsureHealthyAsync_AmbiguousFallbackIdentity_DoesNotResolveMultipleRequestsWithoutSourceRelicId()
    {
        await WriteJsonAsync(GuardianAbodeResidentRequestState.PendingManifestationRequestPath, new
        {
            requests = new object[]
            {
                new
                {
                    requestId = "imprint_req_1",
                    manifestationSource = "imprint_relic",
                    relicId = "relic_imprint_001",
                    relicName = "Печать Друга I",
                    sourceImprintId = "imprint_shared_1",
                    targetIncarnation = 3,
                    companionNameHint = "Тарен",
                    originWorldSummary = "Первый отголосок.",
                    futureCompanionPrompt = "Faithful ally",
                    createdAtUtc = "2026-03-27T00:00:00Z"
                },
                new
                {
                    requestId = "imprint_req_2",
                    manifestationSource = "imprint_relic",
                    relicId = "relic_imprint_002",
                    relicName = "Печать Друга II",
                    sourceImprintId = "imprint_shared_1",
                    targetIncarnation = 3,
                    companionNameHint = "Тарен",
                    originWorldSummary = "Второй отголосок.",
                    futureCompanionPrompt = "Faithful ally",
                    createdAtUtc = "2026-03-27T00:00:01Z"
                }
            }
        });

        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            currentRealm = "Mortal World",
            currentIncarnation = 3,
            soulRelics = new
            {
                equipped = new object[]
                {
                    new { relicId = "relic_imprint_001", name = "Печать Друга I", rarity = "Rare", companionManifestationStatus = "pending", soulImprint = new { imprintId = "imprint_shared_1", npcName = "Тарен", description = "Первый отголосок спутника.", personalityTraits = new[] { "верность" } } },
                    new { relicId = "relic_imprint_002", name = "Печать Друга II", rarity = "Rare", companionManifestationStatus = "pending", soulImprint = new { imprintId = "imprint_shared_1", npcName = "Тарен", description = "Второй отголосок спутника.", personalityTraits = new[] { "стойкость" } } }
                },
                stored = Array.Empty<object>()
            }
        });

        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_core.json", """
        {
          "UpdateNPCs": [
            {
              "npcId": "npc_manifested_taren",
              "npcName": "Тарен",
              "sourceSoulImprintId": "imprint_shared_1"
            }
          ]
        }
        """);

        await GuardianAbodeResidentRequestState.EnsureHealthyAsync(_fs, "Mortal World");

        var pendingRaw = await _fs.ReadFileAsync(GuardianAbodeResidentRequestState.PendingManifestationRequestPath);
        Assert.Contains("\"relicId\": \"relic_imprint_001\"", pendingRaw, StringComparison.Ordinal);
        Assert.Contains("\"relicId\": \"relic_imprint_002\"", pendingRaw, StringComparison.Ordinal);

        var soulRaw = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        Assert.DoesNotContain("\"companionManifestationStatus\": \"materialized\"", soulRaw, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EnsureHealthyAsync_ExactRelicMatch_ResolvesOnlyMatchingRequestWhenSourcesOverlap()
    {
        await WriteJsonAsync(GuardianAbodeResidentRequestState.PendingManifestationRequestPath, new
        {
            requests = new object[]
            {
                new
                {
                    requestId = "imprint_req_1",
                    manifestationSource = "imprint_relic",
                    relicId = "relic_imprint_001",
                    relicName = "Печать Друга I",
                    sourceImprintId = "imprint_shared_1",
                    targetIncarnation = 3,
                    companionNameHint = "Тарен",
                    originWorldSummary = "Первый отголосок.",
                    futureCompanionPrompt = "Faithful ally",
                    createdAtUtc = "2026-03-27T00:00:00Z"
                },
                new
                {
                    requestId = "imprint_req_2",
                    manifestationSource = "imprint_relic",
                    relicId = "relic_imprint_002",
                    relicName = "Печать Друга II",
                    sourceImprintId = "imprint_shared_1",
                    targetIncarnation = 3,
                    companionNameHint = "Тарен",
                    originWorldSummary = "Второй отголосок.",
                    futureCompanionPrompt = "Faithful ally",
                    createdAtUtc = "2026-03-27T00:00:01Z"
                }
            }
        });

        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            currentRealm = "Mortal World",
            currentIncarnation = 3,
            soulRelics = new
            {
                equipped = new object[]
                {
                    new { relicId = "relic_imprint_001", name = "Печать Друга I", rarity = "Rare", companionManifestationStatus = "pending", soulImprint = new { imprintId = "imprint_shared_1", npcName = "Тарен", description = "Первый отголосок спутника.", personalityTraits = new[] { "верность" } } },
                    new { relicId = "relic_imprint_002", name = "Печать Друга II", rarity = "Rare", companionManifestationStatus = "pending", soulImprint = new { imprintId = "imprint_shared_1", npcName = "Тарен", description = "Второй отголосок спутника.", personalityTraits = new[] { "стойкость" } } }
                },
                stored = Array.Empty<object>()
            }
        });

        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_core.json", """
        {
          "UpdateNPCs": [
            {
              "npcId": "npc_manifested_taren",
              "npcName": "Тарен",
              "sourceCompanionRelicId": "relic_imprint_001",
              "sourceSoulImprintId": "imprint_shared_1"
            }
          ]
        }
        """);

        await GuardianAbodeResidentRequestState.EnsureHealthyAsync(_fs, "Mortal World");

        var pendingRaw = await _fs.ReadFileAsync(GuardianAbodeResidentRequestState.PendingManifestationRequestPath);
        Assert.DoesNotContain("\"relicId\": \"relic_imprint_001\"", pendingRaw, StringComparison.Ordinal);
        Assert.Contains("\"relicId\": \"relic_imprint_002\"", pendingRaw, StringComparison.Ordinal);

        var soulRaw = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        Assert.Contains("\"companionManifestationResolvedRequestId\": \"imprint_req_1\"", soulRaw, StringComparison.Ordinal);
        Assert.Contains("\"companionManifestationResolvedNpcId\": \"npc_manifested_taren\"", soulRaw, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WriteResidentsRequestAsync_DifferentAbodes_StoresRequestsArray()
    {
        await GuardianAbodeResidentRequestState.WriteResidentsRequestAsync(_fs, new GuardianAbodeResidentRequestState.PendingGuardianAbodeResidentsRequest
        {
            RequestId = "abode_req_1",
            GuardianId = "guardian_alpha",
            GuardianName = "Азалия",
            AbodeId = "abode_alpha",
            AbodeName = "Сад Нитей",
            CurrentReputation = 120,
            CreatedAtTurn = 7,
            CreatedAtUtc = "2026-03-27T00:00:00Z"
        });

        await GuardianAbodeResidentRequestState.WriteResidentsRequestAsync(_fs, new GuardianAbodeResidentRequestState.PendingGuardianAbodeResidentsRequest
        {
            RequestId = "abode_req_2",
            GuardianId = "guardian_beta",
            GuardianName = "Нерис",
            AbodeId = "abode_beta",
            AbodeName = "Башня Эха",
            CurrentReputation = 90,
            CreatedAtTurn = 8,
            CreatedAtUtc = "2026-03-27T00:10:00Z"
        });

        var raw = await _fs.ReadFileAsync(GuardianAbodeResidentRequestState.PendingResidentsRequestPath);
        Assert.NotNull(raw);
        Assert.Contains("\"requests\": [", raw, StringComparison.Ordinal);
        Assert.Contains("\"guardianId\": \"guardian_alpha\"", raw, StringComparison.Ordinal);
        Assert.Contains("\"guardianId\": \"guardian_beta\"", raw, StringComparison.Ordinal);

        var requests = await GuardianAbodeResidentRequestState.ReadResidentsRequestsAsync(_fs);
        Assert.Equal(2, requests.Count);
    }

    [Fact]
    public async Task WriteResidentsRequestAsync_FounderAttraction_PreservesFounderContext()
    {
        await GuardianAbodeResidentRequestState.WriteResidentsRequestAsync(_fs, new GuardianAbodeResidentRequestState.PendingGuardianAbodeResidentsRequest
        {
            RequestId = "abode_req_founder_1",
            GuardianId = "guardian_player",
            GuardianName = "Трон Прилива",
            AbodeId = "abode_player",
            AbodeName = "Обитель Прилива",
            CurrentReputation = 230,
            RequestMode = GuardianAbodeResidentRequestState.ResidentsRequestModeFounderAttraction,
            FounderFeatureTitle = "Зов памяти",
            FounderFeatureSummary = "Новая Обитель притягивает первых резидентов, откликнувшихся на creed.",
            CreatedAtTurn = 9,
            CreatedAtUtc = "2026-04-18T00:10:00Z"
        });

        var requests = await GuardianAbodeResidentRequestState.ReadResidentsRequestsAsync(_fs);

        var request = Assert.Single(requests);
        Assert.Equal(GuardianAbodeResidentRequestState.ResidentsRequestModeFounderAttraction, request.RequestMode);
        Assert.Equal("Зов памяти", request.FounderFeatureTitle);
        Assert.Contains("притягивает", request.FounderFeatureSummary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WriteInteractionRequestAsync_SameResidentAndType_ReplacesExistingRequest()
    {
        await GuardianAbodeResidentRequestState.WriteInteractionRequestAsync(_fs, new GuardianAbodeResidentRequestState.PendingGuardianAbodeResidentInteractionRequest
        {
            RequestId = "resident_talk_req_1",
            GuardianId = "guardian_alpha",
            GuardianName = "Азалия",
            AbodeId = "abode_alpha",
            AbodeName = "Сад Нитей",
            ResidentId = "resident_alpha_1",
            ResidentName = "Лиора",
            InteractionType = GuardianAbodeResidentState.InteractionTypeTalk,
            CreatedAtTurn = 7,
            CreatedAtUtc = "2026-03-27T00:00:00Z"
        });

        await GuardianAbodeResidentRequestState.WriteInteractionRequestAsync(_fs, new GuardianAbodeResidentRequestState.PendingGuardianAbodeResidentInteractionRequest
        {
            RequestId = "resident_talk_req_2",
            GuardianId = "guardian_alpha",
            GuardianName = "Азалия",
            AbodeId = "abode_alpha",
            AbodeName = "Сад Нитей",
            ResidentId = "resident_alpha_1",
            ResidentName = "Лиора",
            InteractionType = GuardianAbodeResidentState.InteractionTypeTalk,
            CreatedAtTurn = 8,
            CreatedAtUtc = "2026-03-27T00:10:00Z"
        });

        var requests = await GuardianAbodeResidentRequestState.ReadInteractionRequestsAsync(_fs);
        var request = Assert.Single(requests);
        Assert.Equal("resident_talk_req_2", request.RequestId);
        Assert.Equal(GuardianAbodeResidentState.InteractionTypeTalk, request.InteractionType);
    }

    [Fact]
    public async Task EnsureHealthyAsync_AfterlifePreservesMalformedResidentsBundle()
    {
        await _fs.WriteFileAtomicAsync(
            GuardianAbodeResidentRequestState.PendingResidentsRequestPath,
            """
            {
              "requests": [
                {
                  "requestId": "resident_roster_valid",
                  "guardianId": "guardian_alpha",
                  "guardianName": "Азалия",
                  "abodeId": "abode_alpha",
                  "abodeName": "Сад Нитей",
                  "createdAtTurn": 7,
                  "createdAtUtc": "2026-03-27T00:00:00Z"
                },
                {
                  "requestId":
                }
              ]
            }
            """);

        await GuardianAbodeResidentRequestState.EnsureHealthyAsync(_fs, "Chaos Sea");

        Assert.True(_fs.FileExists(GuardianAbodeResidentRequestState.PendingResidentsRequestPath));
        var raw = await _fs.ReadFileAsync(GuardianAbodeResidentRequestState.PendingResidentsRequestPath);
        Assert.Contains("\"requestId\":", raw, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EnsureHealthyAsync_NonAfterlife_ClearsPendingResidentsRosterRequest()
    {
        await WriteJsonAsync(GuardianAbodeResidentRequestState.PendingResidentsRequestPath, new
        {
            requests = new object[]
            {
                new
                {
                    requestId = "roster_req_wrong_realm",
                    guardianId = "guardian_social_001",
                    guardianName = "Азалия",
                    abodeId = "abode_social_001",
                    abodeName = "Лазурная Обитель",
                    createdAtTurn = 12,
                    createdAtUtc = "2026-04-22T02:00:00Z"
                }
            }
        });

        await GuardianAbodeResidentRequestState.EnsureHealthyAsync(_fs, "Mortal World");

        Assert.False(_fs.FileExists(GuardianAbodeResidentRequestState.PendingResidentsRequestPath));
    }

    [Fact]
    public async Task EnsureHealthyAsync_Afterlife_KeepsRosterRequestWithoutMatchingRosterReceipt()
    {
        await WriteJsonAsync(GuardianAbodeResidentRequestState.PendingResidentsRequestPath, new
        {
            requests = new object[]
            {
                new
                {
                    requestId = "roster_req_missing_receipt",
                    guardianId = "guardian_social_001",
                    guardianName = "Азалия",
                    abodeId = "abode_social_001",
                    abodeName = "Лазурная Обитель",
                    createdAtTurn = 13,
                    createdAtUtc = "2026-04-22T02:10:00Z"
                }
            }
        });
        await WriteJsonAsync(GuardianAbodeResidentState.StatePath, new
        {
            entries = new object[]
            {
                new
                {
                    residentId = "resident_echo_001",
                    guardianId = "guardian_social_001",
                    abodeId = "abode_social_001",
                    displayName = "Лиора",
                    residentKind = "wayfaring_soul",
                    roleLabel = "Вестница",
                    bondLevel = 58,
                    bondTier = "trusted",
                    abodeDevotionLevel = 63,
                    abodeDevotionTier = "attached",
                    restlessness = 14,
                    migrationState = "settled",
                    historyRevealed = true
                }
            },
            rosterReceipts = Array.Empty<object>(),
            interactionReceipts = Array.Empty<object>(),
            transferReceipts = Array.Empty<object>(),
            historyLog = Array.Empty<object>(),
            thoughtJournal = Array.Empty<object>(),
            interactionLog = Array.Empty<object>()
        });

        await GuardianAbodeResidentRequestState.EnsureHealthyAsync(_fs, "Chaos Sea");

        var pendingRaw = await _fs.ReadFileAsync(GuardianAbodeResidentRequestState.PendingResidentsRequestPath);
        Assert.NotNull(pendingRaw);
        Assert.Contains("roster_req_missing_receipt", pendingRaw, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EnsureHealthyAsync_AfterlifePreservesValidManifestationRequestsForNextLife()
    {
        await WriteJsonAsync(GuardianAbodeResidentRequestState.PendingManifestationRequestPath, new
        {
            requests = new[]
            {
                new
                {
                    requestId = "manifest_next_life",
                    manifestationSource = "resident_relic",
                    relicId = "relic_echo",
                    relicName = "Эхо Лиоры",
                    sourceResidentId = "resident_liora",
                    sourceGuardianId = "guardian_azalia",
                    sourceGuardianName = "Азалия",
                    targetIncarnation = 5,
                    companionNameHint = "Лиора",
                    originWorldSummary = "Следующая смертная жизнь.",
                    futureCompanionPrompt = "Лиора проявится как ранняя спутница в следующей смертной жизни.",
                    bondReason = "Связь закреплена через реликвию резидента.",
                    coreTraits = new[] { "loyal" },
                    archetypeHints = new[] { "guide" },
                    appearanceMotifs = new[] { "dawn" },
                    createdAtUtc = "2026-04-27T14:00:00Z"
                }
            }
        });

        await GuardianAbodeResidentRequestState.EnsureHealthyAsync(_fs, "Chaos Sea");

        var pendingRaw = await _fs.ReadFileAsync(GuardianAbodeResidentRequestState.PendingManifestationRequestPath);
        Assert.NotNull(pendingRaw);
        Assert.Contains("manifest_next_life", pendingRaw, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildSystemReminderFragmentAsync_AfterlifeMarksManifestationAsPreservedNextLifeContext()
    {
        await WriteJsonAsync(GuardianAbodeResidentRequestState.PendingManifestationRequestPath, new
        {
            requests = new[]
            {
                new
                {
                    requestId = "manifest_next_life",
                    manifestationSource = "resident_relic",
                    relicId = "relic_echo",
                    relicName = "Эхо Лиоры",
                    sourceResidentId = "resident_liora",
                    sourceGuardianId = "guardian_azalia",
                    sourceGuardianName = "Азалия",
                    targetIncarnation = 5,
                    companionNameHint = "Лиора",
                    originWorldSummary = "Следующая смертная жизнь.",
                    futureCompanionPrompt = "Лиора проявится как ранняя спутница в следующей смертной жизни.",
                    bondReason = "Связь закреплена через реликвию резидента.",
                    coreTraits = new[] { "loyal" },
                    archetypeHints = new[] { "guide" },
                    appearanceMotifs = new[] { "dawn" },
                    createdAtUtc = "2026-04-27T14:00:00Z"
                }
            }
        });

        var reminder = await GuardianAbodeResidentRequestState.BuildSystemReminderFragmentAsync(_fs, "Chaos Sea");

        Assert.NotNull(reminder);
        Assert.Contains("PRESERVED FOR NEXT LIFE", reminder, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("manifest_next_life", reminder, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("materialize an early mortal-world encounter", reminder, StringComparison.OrdinalIgnoreCase);
    }

    private async Task WriteJsonAsync(string relativePath, object payload)
    {
        await _fs.WriteFileAtomicAsync(relativePath, JsonSerializer.Serialize(payload, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
    }

    private static void CopyDirectory(string sourceDir, string destinationDir)
    {
        Directory.CreateDirectory(destinationDir);
        foreach (var directory in Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(directory.Replace(sourceDir, destinationDir));
        foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
            File.Copy(file, file.Replace(sourceDir, destinationDir), overwrite: true);
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
            // best-effort cleanup
        }
    }
}
