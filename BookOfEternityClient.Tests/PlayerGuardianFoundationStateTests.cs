using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class PlayerGuardianFoundationStateTests
{
    [Fact]
    public async Task ReadContextAsync_ChaosSeaAfterSealedShining_ReturnsEligibleContext()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();
            await WriteEligibleFoundationStateAsync(fs);

            var context = await PlayerGuardianFoundationState.ReadContextAsync(fs);

            Assert.True(context.CanCreateRequest);
            Assert.Equal("Тестовая Душа", context.SoulName);
            Assert.Equal("guardian_old", context.PreviousGuardianId);
            Assert.Equal("Азалия", context.PreviousGuardianName);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task BuildSystemReminderFragmentAsync_PendingRequest_ListsFoundationContract()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();
            await WriteEligibleFoundationStateAsync(fs);
            await PlayerGuardianFoundationState.WriteAsync(fs, new PlayerGuardianFoundationState.PendingPlayerGuardianFoundationRequest
            {
                RequestId = "foundation_req_1",
                FounderSoulName = "Тестовая Душа",
                PreviousGuardianId = "guardian_old",
                PreviousGuardianName = "Азалия",
                SourceShiningAvailability = ShiningAbodeState.AvailabilitySealedUntilNextAscension,
                ProposedDisplayName = "Трон Прилива",
                MantleSummary = "Новый покровитель памяти",
                MantleCreed = "Никто не будет забыт",
                AppearanceMotifs = new List<string> { "волны", "свечи" },
                DominantAspect = "memory",
                CreatedAtTurn = 14,
                CreatedAtUtc = "2026-04-18T00:00:00Z"
            });

            var reminder = await PlayerGuardianFoundationState.BuildSystemReminderFragmentAsync(fs, "Chaos Sea");

            Assert.NotNull(reminder);
            Assert.Contains("PLAYER-FOUNDED GUARDIAN FOUNDATION:", reminder);
            Assert.Contains("Трон Прилива", reminder);
            Assert.Contains("soulbound", reminder, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("founderBonuses", reminder, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("founder_call", reminder, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public void ApplyCanonicalFoundationSemantics_SetsSoulboundFloorAndFormerPatronRole()
    {
        var foundedGuardian = new JsonObject
        {
            ["guardianId"] = "guardian_player",
            ["relationshipData"] = new JsonObject
            {
                ["currentReputation"] = 180,
                ["reputationHistory"] = new JsonArray(),
                ["lastInteraction"] = null
            }
        };
        var formerPatronGuardian = new JsonObject
        {
            ["guardianId"] = "guardian_old",
            ["relationshipData"] = new JsonObject
            {
                ["currentReputation"] = 120,
                ["reputationHistory"] = new JsonArray(),
                ["lastInteraction"] = null
            }
        };
        var request = new PlayerGuardianFoundationState.PendingPlayerGuardianFoundationRequest
        {
            RequestId = "foundation_req_3",
            FounderSoulName = "Тестовая Душа",
            PreviousGuardianId = "guardian_old",
            PreviousGuardianName = "Азалия",
            SourceShiningAvailability = ShiningAbodeState.AvailabilitySealedUntilNextAscension,
            ProposedDisplayName = "Трон Прилива",
            MantleSummary = "Новый покровитель памяти",
            MantleCreed = "Никто не будет забыт",
            AppearanceMotifs = new List<string> { "волны" },
            CreatedAtTurn = 14,
            CreatedAtUtc = "2026-04-18T00:00:00Z"
        };

        PlayerGuardianFoundationState.ApplyCanonicalFoundedGuardianSemantics(foundedGuardian, request);
        PlayerGuardianFoundationState.ApplyCanonicalFormerPatronSemantics(formerPatronGuardian);

        Assert.Equal(PlayerGuardianFoundationState.OriginTypePlayerFoundedAscendedSoul, foundedGuardian["originType"]?.GetValue<string>());
        Assert.Equal(PlayerGuardianFoundationState.FounderLoyaltyTierSoulbound, foundedGuardian["founderLoyaltyTier"]?.GetValue<string>());
        Assert.Equal(
            PlayerGuardianFoundationState.DefaultFounderExtraGachaChargesPerReturn,
            foundedGuardian[PlayerGuardianFoundationState.FounderBonusesProperty]?[PlayerGuardianFoundationState.FounderBonusExtraGachaChargesProperty]?.GetValue<int>());
        Assert.Equal(
            PlayerGuardianFoundationState.FounderAbodeResidentAttractionModeFounderCall,
            foundedGuardian[PlayerGuardianFoundationState.FounderAbodeFeaturesProperty]?[PlayerGuardianFoundationState.FounderAbodeResidentAttractionModeProperty]?.GetValue<string>());
        Assert.True(PlayerGuardianFoundationState.IsSoulboundReputationSatisfied(
            foundedGuardian["relationshipData"]!["currentReputation"]!.GetValue<int>()));
        Assert.Equal(
            PlayerGuardianFoundationState.GuardianRoleFormerPatron,
            formerPatronGuardian["relationshipData"]![PlayerGuardianFoundationState.GuardianRoleToPlayerProperty]?.GetValue<string>());
    }

    [Fact]
    public async Task EnsureHealthyAsync_MatchingHistoryAndSoulLink_ClearsPendingRequest()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();
            await WriteEligibleFoundationStateAsync(fs);

            await PlayerGuardianFoundationState.WriteAsync(fs, new PlayerGuardianFoundationState.PendingPlayerGuardianFoundationRequest
            {
                RequestId = "foundation_req_2",
                FounderSoulName = "Тестовая Душа",
                PreviousGuardianId = "guardian_old",
                PreviousGuardianName = "Азалия",
                SourceShiningAvailability = ShiningAbodeState.AvailabilitySealedUntilNextAscension,
                ProposedDisplayName = "Трон Прилива",
                MantleSummary = "Новый покровитель памяти",
                MantleCreed = "Никто не будет забыт",
                AppearanceMotifs = new List<string> { "волны", "свечи" },
                CreatedAtTurn = 14,
                CreatedAtUtc = "2026-04-18T00:00:00Z"
            });

            var guardiansRoot = JsonNode.Parse(await fs.ReadFileAsync("game_state/meta/guardians.json"))!.AsObject();
            var guardians = guardiansRoot["guardians"]!.AsArray();
            guardians.Add(new JsonObject
            {
                ["guardianId"] = "guardian_player",
                ["canonicalName"] = "Трон Прилива",
                ["originType"] = PlayerGuardianFoundationState.OriginTypePlayerFoundedAscendedSoul,
                ["foundationRequestId"] = "foundation_req_2"
            });
            PlayerGuardianFoundationState.EnsureFoundationHistoryArray(guardiansRoot).Add(new JsonObject
            {
                ["requestId"] = "foundation_req_2",
                ["guardianId"] = "guardian_player",
                ["guardianDisplayName"] = "Трон Прилива",
                ["founderSoulName"] = "Тестовая Душа",
                ["formerPatronGuardianId"] = "guardian_old",
                ["formerPatronGuardianName"] = "Азалия",
                ["foundationSource"] = PlayerGuardianFoundationState.FoundationSourceShiningReturn,
                ["resolvedAtTurn"] = 15,
                ["resolvedAtUtc"] = "2026-04-18T00:05:00Z"
            });
            await fs.WriteFileAtomicAsync("game_state/meta/guardians.json", guardiansRoot.ToJsonString());

            var soulRoot = JsonNode.Parse(await fs.ReadFileAsync("game_state/meta/soul_state.json"))!.AsObject();
            soulRoot[PlayerGuardianFoundationState.SoulStateGuardianIdProperty] = "guardian_player";
            soulRoot[PlayerGuardianFoundationState.SoulStateFoundationStatusProperty] = PlayerGuardianFoundationState.SoulStateFoundationStatusFounded;
            await fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", soulRoot.ToJsonString());

            await PlayerGuardianFoundationState.EnsureHealthyAsync(fs, "Chaos Sea");

            Assert.False(fs.FileExists(PlayerGuardianFoundationState.PendingRequestPath));
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task EnsureHealthyAsync_UnresolvedRealm_PreservesPendingRequest()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();
            await WriteEligibleFoundationStateAsync(fs);

            await PlayerGuardianFoundationState.WriteAsync(fs, new PlayerGuardianFoundationState.PendingPlayerGuardianFoundationRequest
            {
                RequestId = "foundation_req_unresolved",
                FounderSoulName = "Тестовая Душа",
                PreviousGuardianId = "guardian_old",
                PreviousGuardianName = "Азалия",
                SourceShiningAvailability = ShiningAbodeState.AvailabilitySealedUntilNextAscension,
                ProposedDisplayName = "Трон Прилива",
                MantleSummary = "Новый покровитель памяти",
                MantleCreed = "Никто не будет забыт",
                AppearanceMotifs = new List<string> { "волны", "свечи" },
                CreatedAtTurn = 14,
                CreatedAtUtc = "2026-04-18T00:00:00Z"
            });

            await PlayerGuardianFoundationState.EnsureHealthyAsync(fs, "");

            Assert.True(fs.FileExists(PlayerGuardianFoundationState.PendingRequestPath));
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task ReadContextAsync_FoundedGuardianStatus_ExposesFoundationOverview()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();
            await WriteEligibleFoundationStateAsync(fs);

            var guardiansRoot = JsonNode.Parse(await fs.ReadFileAsync("game_state/meta/guardians.json"))!.AsObject();
            var oldGuardian = guardiansRoot["guardians"]!.AsArray()[0]!.AsObject();
            PlayerGuardianFoundationState.ApplyCanonicalFormerPatronSemantics(oldGuardian);

            var foundedGuardian = new JsonObject
            {
                ["guardianId"] = "guardian_player",
                ["canonicalName"] = "Трон Прилива",
                ["abode"] = new JsonObject
                {
                    ["abodeId"] = "abode_player",
                    ["name"] = "Обитель Прилива"
                },
                ["relationshipData"] = new JsonObject
                {
                    ["currentReputation"] = 180,
                    ["reputationHistory"] = new JsonArray(),
                    ["lastInteraction"] = null
                }
            };
            var request = new PlayerGuardianFoundationState.PendingPlayerGuardianFoundationRequest
            {
                RequestId = "foundation_req_4",
                FounderSoulName = "Тестовая Душа",
                PreviousGuardianId = "guardian_old",
                PreviousGuardianName = "Азалия",
                SourceShiningAvailability = ShiningAbodeState.AvailabilitySealedUntilNextAscension,
                ProposedDisplayName = "Трон Прилива",
                MantleSummary = "Новый покровитель памяти",
                MantleCreed = "Никто не будет забыт",
                AppearanceMotifs = new List<string> { "волны" },
                CreatedAtTurn = 14,
                CreatedAtUtc = "2026-04-18T00:00:00Z"
            };
            PlayerGuardianFoundationState.ApplyCanonicalFoundedGuardianSemantics(foundedGuardian, request);
            guardiansRoot["guardians"]!.AsArray().Add(foundedGuardian);
            guardiansRoot["activeGuardian"] = foundedGuardian.DeepClone();
            guardiansRoot["chaosSeaNavigation"] = new JsonObject
            {
                ["currentAbodeId"] = "abode_player"
            };
            PlayerGuardianFoundationState.EnsureFoundationHistoryArray(guardiansRoot).Add(JsonSerializer.SerializeToNode(
                PlayerGuardianFoundationState.BuildCanonicalHistoryEntry(request, "guardian_player", "Трон Прилива", 15, "2026-04-18T00:05:00Z"))!);
            await fs.WriteFileAtomicAsync("game_state/meta/guardians.json", guardiansRoot.ToJsonString());

            var soulRoot = JsonNode.Parse(await fs.ReadFileAsync("game_state/meta/soul_state.json"))!.AsObject();
            soulRoot[PlayerGuardianFoundationState.SoulStateGuardianIdProperty] = "guardian_player";
            soulRoot[PlayerGuardianFoundationState.SoulStateFoundationStatusProperty] = PlayerGuardianFoundationState.SoulStateFoundationStatusFounded;
            await fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", soulRoot.ToJsonString());

            var context = await PlayerGuardianFoundationState.ReadContextAsync(fs);

            Assert.True(context.HasCompletedFoundation);
            Assert.Equal("founded", context.FoundationStatus);
            Assert.Equal("Трон Прилива", context.ExistingFoundedGuardianName);
            Assert.Equal("Азалия", context.FormerPatronGuardianName);
            Assert.Equal("Обитель Прилива", context.ExistingFoundedGuardianAbodeName);
            Assert.Equal(PlayerGuardianFoundationState.DefaultFounderExtraGachaChargesPerReturn, context.ExistingFoundedGuardianExtraGachaChargesPerReturn);
            Assert.False(string.IsNullOrWhiteSpace(context.ExistingFoundedGuardianFeatureTitle));
            Assert.True(context.CurrentActiveGuardianIsFounded);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    private static async Task WriteEligibleFoundationStateAsync(FileSystemManager fs)
    {
        await fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", new JsonObject
        {
            ["soulName"] = "Тестовая Душа",
            ["currentRealm"] = "Chaos Sea",
            ["currentIncarnation"] = 3,
            ["inkFeathers"] = new JsonObject
            {
                ["current"] = 80,
                ["total"] = 120
            },
            ["soulRelics"] = new JsonObject
            {
                ["equipped"] = new JsonArray(),
                ["stored"] = new JsonArray()
            },
            ["afterlifeArchive"] = new JsonObject
            {
                ["stored"] = new JsonArray(),
                ["actionReceipts"] = new JsonArray()
            }
        }.ToJsonString());

        await fs.WriteFileAtomicAsync("game_state/meta/shining_abode_state.json", new JsonObject
        {
            ["availability"] = ShiningAbodeState.AvailabilitySealedUntilNextAscension,
            ["preparedIncarnationPackage"] = null
        }.ToJsonString());

        await fs.WriteFileAtomicAsync("game_state/meta/guardians.json", new JsonObject
        {
            ["guardians"] = new JsonArray
            {
                new JsonObject
                {
                    ["guardianId"] = "guardian_old",
                    ["canonicalName"] = "Азалия"
                }
            },
            ["activeGuardian"] = new JsonObject
            {
                ["guardianId"] = "guardian_old",
                ["canonicalName"] = "Азалия"
            },
            ["chaosSeaNavigation"] = new JsonObject
            {
                ["currentAbodeId"] = "abode_old"
            }
        }.ToJsonString());
    }

    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-player-guardian-foundation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void CleanupTempRoot(string root)
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }
}
