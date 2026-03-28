using System.Text.Json;
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
    public async Task EnsureHealthyAsync_MaterializedCompanion_ClearsPendingRequestAndMarksRelicResolved()
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
                    sourceCompanionRelicId = "relic_companion_echo_001",
                    sourceAfterlifeResidentId = "resident_echo_001"
                }
            }
        });

        await GuardianAbodeResidentRequestState.EnsureHealthyAsync(_fs, "Mortal World");

        var pendingRaw = await _fs.ReadFileAsync(GuardianAbodeResidentRequestState.PendingManifestationRequestPath);
        Assert.True(string.IsNullOrWhiteSpace(pendingRaw), "Pending manifestation request should be cleared after materialization.");

        var soulRaw = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        Assert.NotNull(soulRaw);
        Assert.Contains("\"companionManifestationStatus\": \"materialized\"", soulRaw, StringComparison.Ordinal);
        Assert.Contains("\"companionManifestationResolvedRequestId\": \"resident_manifest_req_1\"", soulRaw, StringComparison.Ordinal);
        Assert.Contains("\"companionManifestationResolvedNpcId\": \"npc_manifested_liora\"", soulRaw, StringComparison.Ordinal);
        Assert.Contains("\"companionManifestationResolvedAtTurn\": 18", soulRaw, StringComparison.Ordinal);
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
                    new { relicId = "relic_imprint_001", name = "Печать Друга I", companionManifestationStatus = "pending", soulImprint = new { imprintId = "imprint_shared_1", npcName = "Тарен" } },
                    new { relicId = "relic_imprint_002", name = "Печать Друга II", companionManifestationStatus = "pending", soulImprint = new { imprintId = "imprint_shared_1", npcName = "Тарен" } }
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
                    npcId = "npc_manifested_taren",
                    npcName = "Тарен",
                    sourceSoulImprintId = "imprint_shared_1"
                }
            }
        });

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
                    new { relicId = "relic_imprint_001", name = "Печать Друга I", companionManifestationStatus = "pending", soulImprint = new { imprintId = "imprint_shared_1", npcName = "Тарен" } },
                    new { relicId = "relic_imprint_002", name = "Печать Друга II", companionManifestationStatus = "pending", soulImprint = new { imprintId = "imprint_shared_1", npcName = "Тарен" } }
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
                    npcId = "npc_manifested_taren",
                    npcName = "Тарен",
                    sourceCompanionRelicId = "relic_imprint_001",
                    sourceSoulImprintId = "imprint_shared_1"
                }
            }
        });

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
