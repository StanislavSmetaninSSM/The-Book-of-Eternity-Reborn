using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json.Nodes;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ShiningFactionRequestStateTests
{
    [Fact]
    public async Task WriteFoundingRequestAsync_ReplacesRequestByFactionId()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();

            await ShiningFactionRequestState.WriteFoundingRequestAsync(fs, new ShiningFactionRequestState.PendingShiningFactionFoundingRequest
            {
                RequestId = "founding_req_dawn",
                ProposedFactionId = "faction_dawn",
                ProposedHallId = "hall_dawn",
                ProposedHallName = "Зал Рассвета",
                ProposedHallDescription = "Светлый зал",
                ProposedHallServiceTags = { "social" },
                Charter = new ShiningFactionRequestState.FactionCharterPayload
                {
                    FactionName = "Хор Рассвета",
                    FavoredArchetype = "accord",
                    PatronEffectFamily = "social",
                    Summary = "Поют утренний свет."
                }
            });

            await ShiningFactionRequestState.WriteFoundingRequestAsync(fs, new ShiningFactionRequestState.PendingShiningFactionFoundingRequest
            {
                ProposedFactionId = "faction_dawn",
                ProposedHallId = "hall_dawn_v2",
                ProposedHallName = "Зал Второго Рассвета",
                ProposedHallDescription = "Другой зал",
                ProposedHallServiceTags = { "social", "lore" },
                Charter = new ShiningFactionRequestState.FactionCharterPayload
                {
                    FactionName = "Хор Рассвета II",
                    FavoredArchetype = "accord",
                    PatronEffectFamily = "social",
                    Summary = "Новый рассвет."
                }
            });

            var requests = await ShiningFactionRequestState.ReadFoundingRequestsAsync(fs);
            var request = Assert.Single(requests);
            Assert.Equal("hall_dawn_v2", request.ProposedHallId);
            Assert.Equal("Зал Второго Рассвета", request.ProposedHallName);
            Assert.Equal(ShiningFactionRequestState.FactionFoundingCostFeathers, request.QuotedCostFeathers);
            Assert.Equal(ShiningFactionRequestState.FactionFoundingCostLightSparks, request.QuotedCostLightSparks);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task WriteFoundingRequestAsync_ReplacesRequestByHallId()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();

            await ShiningFactionRequestState.WriteFoundingRequestAsync(fs, new ShiningFactionRequestState.PendingShiningFactionFoundingRequest
            {
                RequestId = "founding_first",
                ProposedFactionId = "faction_dawn",
                ProposedHallId = "hall_shared",
                ProposedHallName = "Зал Рассвета",
                ProposedHallDescription = "Светлый зал",
                ProposedHallServiceTags = { "social" },
                Charter = new ShiningFactionRequestState.FactionCharterPayload
                {
                    FactionName = "Хор Рассвета",
                    FavoredArchetype = "accord",
                    PatronEffectFamily = "social",
                    Summary = "Поют утренний свет."
                }
            });

            await ShiningFactionRequestState.WriteFoundingRequestAsync(fs, new ShiningFactionRequestState.PendingShiningFactionFoundingRequest
            {
                RequestId = "founding_second",
                ProposedFactionId = "faction_twilight",
                ProposedHallId = "hall_shared",
                ProposedHallName = "Зал Сумерек",
                ProposedHallDescription = "Иной зал",
                ProposedHallServiceTags = { "social", "lore" },
                Charter = new ShiningFactionRequestState.FactionCharterPayload
                {
                    FactionName = "Хор Сумерек",
                    FavoredArchetype = "accord",
                    PatronEffectFamily = "social",
                    Summary = "Несут иной свет."
                }
            });

            var requests = await ShiningFactionRequestState.ReadFoundingRequestsAsync(fs);
            var request = Assert.Single(requests);
            Assert.Equal("faction_twilight", request.ProposedFactionId);
            Assert.Equal("hall_shared", request.ProposedHallId);
            Assert.Equal("Зал Сумерек", request.ProposedHallName);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task WriteFoundingRequestAsync_ReplacesRequestByHallId_CaseInsensitive()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();

            await ShiningFactionRequestState.WriteFoundingRequestAsync(fs, new ShiningFactionRequestState.PendingShiningFactionFoundingRequest
            {
                RequestId = "founding_first",
                ProposedFactionId = "faction_dawn",
                ProposedHallId = "hall_shared",
                ProposedHallName = "Зал Рассвета",
                ProposedHallDescription = "Светлый зал",
                ProposedHallServiceTags = { "social" },
                Charter = new ShiningFactionRequestState.FactionCharterPayload
                {
                    FactionName = "Хор Рассвета",
                    FavoredArchetype = "accord",
                    PatronEffectFamily = "social",
                    Summary = "Поют утренний свет."
                }
            });

            await ShiningFactionRequestState.WriteFoundingRequestAsync(fs, new ShiningFactionRequestState.PendingShiningFactionFoundingRequest
            {
                RequestId = "founding_second",
                ProposedFactionId = "faction_twilight",
                ProposedHallId = "HALL_SHARED",
                ProposedHallName = "Зал Сумерек",
                ProposedHallDescription = "Иной зал",
                ProposedHallServiceTags = { "social", "lore" },
                Charter = new ShiningFactionRequestState.FactionCharterPayload
                {
                    FactionName = "Хор Сумерек",
                    FavoredArchetype = "accord",
                    PatronEffectFamily = "social",
                    Summary = "Несут иной свет."
                }
            });

            var requests = await ShiningFactionRequestState.ReadFoundingRequestsAsync(fs);
            var request = Assert.Single(requests);
            Assert.Equal("faction_twilight", request.ProposedFactionId);
            Assert.Equal("HALL_SHARED", request.ProposedHallId);
            Assert.Equal("Зал Сумерек", request.ProposedHallName);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task WriteFoundingRequestAsync_ReplacesRequestByRequestId()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();

            await ShiningFactionRequestState.WriteFoundingRequestAsync(fs, new ShiningFactionRequestState.PendingShiningFactionFoundingRequest
            {
                RequestId = "founding_shared",
                ProposedFactionId = "faction_dawn",
                ProposedHallId = "hall_dawn",
                ProposedHallName = "Зал Рассвета",
                ProposedHallDescription = "Светлый зал",
                ProposedHallServiceTags = { "social" },
                Charter = new ShiningFactionRequestState.FactionCharterPayload
                {
                    FactionName = "Хор Рассвета",
                    FavoredArchetype = "accord",
                    PatronEffectFamily = "social",
                    Summary = "Поют утренний свет."
                }
            });

            await ShiningFactionRequestState.WriteFoundingRequestAsync(fs, new ShiningFactionRequestState.PendingShiningFactionFoundingRequest
            {
                RequestId = "founding_shared",
                ProposedFactionId = "faction_twilight",
                ProposedHallId = "hall_twilight",
                ProposedHallName = "Зал Сумерек",
                ProposedHallDescription = "Иной зал",
                ProposedHallServiceTags = { "social", "lore" },
                Charter = new ShiningFactionRequestState.FactionCharterPayload
                {
                    FactionName = "Хор Сумерек",
                    FavoredArchetype = "accord",
                    PatronEffectFamily = "social",
                    Summary = "Несут иной свет."
                }
            });

            var requests = await ShiningFactionRequestState.ReadFoundingRequestsAsync(fs);
            var request = Assert.Single(requests);
            Assert.Equal("founding_shared", request.RequestId);
            Assert.Equal("faction_twilight", request.ProposedFactionId);
            Assert.Equal("hall_twilight", request.ProposedHallId);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task WriteRealignmentRequestAsync_ReplacesRequestByRequestId()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();

            await ShiningFactionRequestState.WriteRealignmentRequestAsync(fs, new ShiningFactionRequestState.PendingShiningFactionRealignmentRequest
            {
                RequestId = "realignment_shared",
                ResidentId = "resident_alpha",
                ResidentName = "Альфа",
                SourceFactionId = "faction_old",
                SourceFactionName = "Старый Дом",
                TargetFactionId = "faction_new",
                TargetFactionName = "Новый Дом",
                RealignmentMode = ShiningFactionRequestState.RealignmentModeAcceptedTransfer,
                FactionLoyaltyLevel = 14,
                FactionLoyaltyTier = ShiningAbodeState.FactionLoyaltyTierAlienated,
                FactionRestlessness = 76,
                FactionRealignmentState = ShiningAbodeState.FactionRealignmentStateReadyToRealign
            });

            await ShiningFactionRequestState.WriteRealignmentRequestAsync(fs, new ShiningFactionRequestState.PendingShiningFactionRealignmentRequest
            {
                RequestId = "realignment_shared",
                ResidentId = "resident_beta",
                ResidentName = "Бета",
                SourceFactionId = "faction_other",
                SourceFactionName = "Иной Дом",
                TargetFactionId = "faction_new",
                TargetFactionName = "Новый Дом",
                RealignmentMode = ShiningFactionRequestState.RealignmentModeDepartureToNeutral,
                FactionLoyaltyLevel = 5,
                FactionLoyaltyTier = ShiningAbodeState.FactionLoyaltyTierAlienated,
                FactionRestlessness = 88,
                FactionRealignmentState = ShiningAbodeState.FactionRealignmentStateReadyToRealign
            });

            var requests = await ShiningFactionRequestState.ReadRealignmentRequestsAsync(fs);
            var request = Assert.Single(requests);
            Assert.Equal("realignment_shared", request.RequestId);
            Assert.Equal("resident_beta", request.ResidentId);
            Assert.Equal(ShiningFactionRequestState.RealignmentModeDepartureToNeutral, request.RealignmentMode);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task BuildSystemReminderFragmentAsync_ListsPendingPoliticalRequests()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();

            await ShiningFactionRequestState.WriteFoundingRequestAsync(fs, new ShiningFactionRequestState.PendingShiningFactionFoundingRequest
            {
                RequestId = "founding_req_dawn",
                ProposedFactionId = "faction_dawn",
                ProposedHallId = "hall_dawn",
                ProposedHallName = "Зал Рассвета",
                ProposedHallDescription = "Светлый зал",
                ProposedHallServiceTags = { "social" },
                Charter = new ShiningFactionRequestState.FactionCharterPayload
                {
                    FactionName = "Хор Рассвета",
                    FavoredArchetype = "accord",
                    PatronEffectFamily = "social",
                    Summary = "Поют утренний свет."
                },
                SupportingResidentIds = { "resident_liora", "resident_mael", "resident_serit" }
            });
            await ShiningFactionRequestState.WriteRealignmentRequestAsync(fs, new ShiningFactionRequestState.PendingShiningFactionRealignmentRequest
            {
                RequestId = "realignment_req_liora",
                ResidentId = "resident_liora",
                ResidentName = "Лиора",
                SourceFactionId = "faction_old",
                SourceFactionName = "Старый Дом",
                TargetFactionId = "faction_dawn",
                TargetFactionName = "Хор Рассвета",
                RealignmentMode = ShiningFactionRequestState.RealignmentModeAcceptedTransfer,
                FactionLoyaltyLevel = 73,
                FactionLoyaltyTier = "trusted",
                FactionRestlessness = 18,
                FactionRealignmentState = "ready_to_realign",
                CreatedAtTurn = 12,
                CreatedAtUtc = "2026-04-27T12:00:00Z"
            });

            var reminder = await ShiningFactionRequestState.BuildSystemReminderFragmentAsync(fs, "Shining Abode");

            Assert.NotNull(reminder);
            Assert.Contains("SHINING ABODE POLITICAL REQUESTS:", reminder);
            Assert.Contains("Founding pending", reminder);
            Assert.Contains("Realignment pending", reminder);
            Assert.Contains("Full pending founding DTO", reminder);
            Assert.Contains("Full pending realignment DTO", reminder);
            Assert.Contains("\"requestId\": \"founding_req_dawn\"", reminder);
            Assert.Contains("\"proposedHallDescription\": \"Светлый зал\"", reminder);
            Assert.Contains("\"supportingResidentIds\"", reminder);
            Assert.Contains("\"requestId\": \"realignment_req_liora\"", reminder);
            Assert.Contains("\"factionLoyaltyLevel\": 73", reminder);
            Assert.Contains("\"factionRestlessness\": 18", reminder);
            Assert.Contains("\"createdAtUtc\": \"2026-04-27T12:00:00Z\"", reminder);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task EnsureHealthyAsync_ChaosSeaClearsPendingRequests()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();

            await ShiningFactionRequestState.WriteFoundingRequestAsync(fs, new ShiningFactionRequestState.PendingShiningFactionFoundingRequest
            {
                ProposedFactionId = "faction_dawn",
                ProposedHallId = "hall_dawn",
                ProposedHallName = "Зал Рассвета",
                ProposedHallDescription = "Светлый зал",
                ProposedHallServiceTags = { "social" },
                Charter = new ShiningFactionRequestState.FactionCharterPayload
                {
                    FactionName = "Хор Рассвета",
                    FavoredArchetype = "accord",
                    PatronEffectFamily = "social",
                    Summary = "Поют утренний свет."
                }
            });
            await ShiningFactionRequestState.WriteRealignmentRequestAsync(fs, new ShiningFactionRequestState.PendingShiningFactionRealignmentRequest
            {
                ResidentId = "resident_liora",
                ResidentName = "Лиора",
                SourceFactionId = "faction_old",
                SourceFactionName = "Старый Дом",
                TargetFactionId = "faction_dawn",
                TargetFactionName = "Хор Рассвета",
                RealignmentMode = ShiningFactionRequestState.RealignmentModeAcceptedTransfer
            });
            await ShiningFactionRequestState.WriteLeadershipTransitionRequestAsync(fs, new ShiningFactionRequestState.PendingShiningFactionLeadershipTransitionRequest
            {
                FactionId = "faction_old",
                FactionName = "Старый Дом",
                TransitionMode = ShiningFactionRequestState.TransitionModePeacefulSuccession,
                IncumbentHeadActorType = ShiningAbodeState.HeadActorTypeGuardian,
                IncumbentHeadActorId = "guardian_old",
                CandidateHeadActorType = ShiningAbodeState.HeadActorTypePlayerSoul,
                CandidateHeadActorId = ShiningAbodeState.HeadActorTypePlayerSoul
            });

            await ShiningFactionRequestState.EnsureHealthyAsync(fs, "Chaos Sea");

            Assert.Empty(await ShiningFactionRequestState.ReadFoundingRequestsAsync(fs));
            Assert.Empty(await ShiningFactionRequestState.ReadRealignmentRequestsAsync(fs));
            Assert.Empty(await ShiningFactionRequestState.ReadLeadershipTransitionRequestsAsync(fs));
            Assert.False(fs.FileExists(ShiningFactionRequestState.PendingFoundingsRequestPath));
            Assert.False(fs.FileExists(ShiningFactionRequestState.PendingRealignmentsRequestPath));
            Assert.False(fs.FileExists(ShiningFactionRequestState.PendingLeadershipTransitionsRequestPath));
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task EnsureHealthyAsync_UnresolvedRealmPreservesPendingRequests()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();

            await ShiningFactionRequestState.WriteFoundingRequestAsync(fs, new ShiningFactionRequestState.PendingShiningFactionFoundingRequest
            {
                ProposedFactionId = "faction_dawn",
                ProposedHallId = "hall_dawn",
                ProposedHallName = "Зал Рассвета",
                ProposedHallDescription = "Светлый зал",
                ProposedHallServiceTags = { "social" },
                Charter = new ShiningFactionRequestState.FactionCharterPayload
                {
                    FactionName = "Хор Рассвета",
                    FavoredArchetype = "accord",
                    PatronEffectFamily = "social",
                    Summary = "Поют утренний свет."
                }
            });

            await ShiningFactionRequestState.EnsureHealthyAsync(fs, "");

            Assert.Single(await ShiningFactionRequestState.ReadFoundingRequestsAsync(fs));
            Assert.True(fs.FileExists(ShiningFactionRequestState.PendingFoundingsRequestPath));
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task EnsureHealthyAsync_ActiveShining_ReconcilesResolvedPoliticalRequests()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();
            await WriteMinimalShiningPoliticalStateAsync(fs);

            await ShiningFactionRequestState.WriteFoundingRequestAsync(fs, new ShiningFactionRequestState.PendingShiningFactionFoundingRequest
            {
                RequestId = "founding_resolved_1",
                ProposedFactionId = "faction_new",
                ProposedHallId = "hall_new",
                ProposedHallName = "Новый Зал",
                ProposedHallDescription = "Описание",
                ProposedHallServiceTags = { "lore", "memory" },
                Charter = new ShiningFactionRequestState.FactionCharterPayload
                {
                    FactionName = "Новый Дом",
                    FavoredArchetype = ShiningAbodeState.ProjectArchetypeRevelation,
                    PatronEffectFamily = ShiningAbodeState.EffectFamilyLore,
                    Summary = "Новая сияющая фракция."
                },
                SupportingResidentIds = { "resident_liora", "resident_mael", "resident_serit" }
            });
            await ShiningFactionRequestState.WriteRealignmentRequestAsync(fs, new ShiningFactionRequestState.PendingShiningFactionRealignmentRequest
            {
                RequestId = "realignment_resolved_1",
                ResidentId = "resident_liora",
                ResidentName = "Лиора",
                SourceFactionId = "faction_old",
                SourceFactionName = "Старый Дом",
                TargetFactionId = "faction_new",
                TargetFactionName = "Новый Дом",
                RealignmentMode = ShiningFactionRequestState.RealignmentModeAcceptedTransfer
            });
            await ShiningFactionRequestState.WriteLeadershipTransitionRequestAsync(fs, new ShiningFactionRequestState.PendingShiningFactionLeadershipTransitionRequest
            {
                RequestId = "leadership_resolved_1",
                FactionId = "faction_old",
                FactionName = "Старый Дом",
                TransitionMode = ShiningFactionRequestState.TransitionModePeacefulSuccession,
                IncumbentHeadActorType = ShiningAbodeState.HeadActorTypeGuardian,
                IncumbentHeadActorId = "guardian_old",
                CandidateHeadActorType = ShiningAbodeState.HeadActorTypePlayerSoul,
                CandidateHeadActorId = ShiningAbodeState.HeadActorTypePlayerSoul
            });

            var shiningRoot = await ReadJsonAsync(fs, ShiningAbodeState.StatePath) ?? throw new InvalidOperationException("Expected shining state.");
            ShiningAbodeState.EnsureFactionFoundingReceiptsArray(shiningRoot).Add(new JsonObject
            {
                ["requestId"] = "founding_resolved_1",
                ["proposedFactionId"] = "faction_new",
                ["proposedHallId"] = "hall_new",
                ["hallName"] = "Новый Зал",
                ["factionId"] = "faction_new",
                ["hallId"] = "hall_new",
                ["status"] = ShiningFactionRequestState.RequestStatusAccepted,
                ["supportingResidentIds"] = new JsonArray("resident_liora", "resident_mael", "resident_serit"),
                ["quotedCostFeathers"] = ShiningFactionRequestState.FactionFoundingCostFeathers,
                ["quotedCostLightSparks"] = ShiningFactionRequestState.FactionFoundingCostLightSparks,
                ["resolvedAtTurn"] = 41,
                ["resolvedAtUtc"] = "2026-04-21T12:00:00Z"
            });
            ShiningAbodeState.EnsureFactionRealignmentReceiptsArray(shiningRoot).Add(new JsonObject
            {
                ["requestId"] = "realignment_resolved_1",
                ["residentId"] = "resident_liora",
                ["sourceFactionId"] = "faction_old",
                ["targetFactionId"] = "faction_new",
                ["status"] = ShiningFactionRequestState.RequestStatusAccepted,
                ["realignmentMode"] = ShiningFactionRequestState.RealignmentModeAcceptedTransfer,
                ["residentHistoryEntryId"] = "resident_history_realignment_1",
                ["resolvedAtTurn"] = 42,
                ["resolvedAtUtc"] = "2026-04-21T12:05:00Z"
            });
            var oldFaction = shiningRoot["factions"]!.AsArray()[0]!.AsObject();
            var newFaction = shiningRoot["factions"]!.AsArray()[1]!.AsObject();
            newFaction["originType"] = ShiningAbodeState.OriginTypePlayerFounded;
            newFaction["baseStrength"] = 35;
            newFaction["leadership"] = new JsonObject
            {
                ["headActorType"] = ShiningAbodeState.HeadActorTypePlayerSoul,
                ["headActorId"] = ShiningAbodeState.HeadActorTypePlayerSoul,
                ["leadershipState"] = ShiningAbodeState.LeadershipStateSecure
            };
            oldFaction["leadershipReceipts"]!.AsArray().Add(new JsonObject
            {
                ["requestId"] = "leadership_resolved_1",
                ["transitionMode"] = ShiningFactionRequestState.TransitionModePeacefulSuccession,
                ["previousHeadActorType"] = ShiningAbodeState.HeadActorTypeGuardian,
                ["previousHeadActorId"] = "guardian_old",
                ["newHeadActorType"] = ShiningAbodeState.HeadActorTypePlayerSoul,
                ["newHeadActorId"] = ShiningAbodeState.HeadActorTypePlayerSoul,
                ["status"] = ShiningFactionRequestState.RequestStatusAccepted,
                ["resolvedAtTurn"] = 43,
                ["resolvedAtUtc"] = "2026-04-21T12:10:00Z"
            });
            oldFaction["leadershipHistory"]!.AsArray().Add(new JsonObject
            {
                ["requestId"] = "leadership_resolved_1",
                ["eventType"] = "succeeded",
                ["summary"] = "Власть мирно передана.",
                ["turnNumber"] = 43
            });
            oldFaction["leadership"] = new JsonObject
            {
                ["headActorType"] = ShiningAbodeState.HeadActorTypePlayerSoul,
                ["headActorId"] = ShiningAbodeState.HeadActorTypePlayerSoul,
                ["leadershipState"] = ShiningAbodeState.LeadershipStateSecure
            };
            await fs.WriteFileAtomicAsync(ShiningAbodeState.StatePath, shiningRoot.ToJsonString());

            var residentRoot = await ReadJsonAsync(fs, GuardianAbodeResidentState.StatePath) ?? throw new InvalidOperationException("Expected resident state.");
            foreach (var resident in residentRoot["entries"]!.AsArray().OfType<JsonObject>())
            {
                var residentId = resident["residentId"]?.GetValue<string>();
                if (residentId is "resident_liora" or "resident_mael" or "resident_serit")
                    resident["shiningFactionId"] = "faction_new";
            }
            residentRoot["historyLog"] = new JsonArray
            {
                new JsonObject
                {
                    ["entryId"] = "resident_history_realignment_1",
                    ["residentId"] = "resident_liora",
                    ["eventType"] = "faction_realignment",
                    ["turnNumber"] = 42
                }
            };
            await fs.WriteFileAtomicAsync(GuardianAbodeResidentState.StatePath, residentRoot.ToJsonString());

            await ShiningFactionRequestState.EnsureHealthyAsync(fs, "Shining Abode");

            Assert.Empty(await ShiningFactionRequestState.ReadFoundingRequestsAsync(fs));
            Assert.Empty(await ShiningFactionRequestState.ReadRealignmentRequestsAsync(fs));
            Assert.Empty(await ShiningFactionRequestState.ReadLeadershipTransitionRequestsAsync(fs));
            Assert.False(fs.FileExists(ShiningFactionRequestState.PendingFoundingsRequestPath));
            Assert.False(fs.FileExists(ShiningFactionRequestState.PendingRealignmentsRequestPath));
            Assert.False(fs.FileExists(ShiningFactionRequestState.PendingLeadershipTransitionsRequestPath));
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task ValidateFoundingRequestAgainstCurrentStateAsync_WithThreeAscendedSupporters_Passes()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();
            await WriteMinimalShiningPoliticalStateAsync(fs);

            var error = await ShiningFactionRequestState.ValidateFoundingRequestAgainstCurrentStateAsync(fs, new ShiningFactionRequestState.PendingShiningFactionFoundingRequest
            {
                ProposedFactionId = "faction_dawn",
                ProposedHallId = "hall_dawn",
                ProposedHallName = "Зал Рассвета",
                ProposedHallDescription = "Светлый зал",
                ProposedHallServiceTags = { "social", "lore" },
                Charter = new ShiningFactionRequestState.FactionCharterPayload
                {
                    FactionName = "Хор Рассвета",
                    FavoredArchetype = ShiningAbodeState.ProjectArchetypeAccord,
                    PatronEffectFamily = ShiningAbodeState.EffectFamilySocial,
                    Summary = "Поют утренний свет."
                },
                SupportingResidentIds = { "resident_liora", "resident_mael", "resident_serit" }
            });

            Assert.Null(error);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task ValidateFoundingRequestAgainstCurrentStateAsync_ReusedRequestIdAcrossDifferentPendingRequest_Fails()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();
            await WriteMinimalShiningPoliticalStateAsync(fs);

            await ShiningFactionRequestState.WriteFoundingRequestAsync(fs, new ShiningFactionRequestState.PendingShiningFactionFoundingRequest
            {
                RequestId = "shared_request_id",
                ProposedFactionId = "faction_dawn",
                ProposedHallId = "hall_dawn",
                ProposedHallName = "Зал Рассвета",
                ProposedHallDescription = "Светлый зал",
                ProposedHallServiceTags = { "social", "lore" },
                Charter = new ShiningFactionRequestState.FactionCharterPayload
                {
                    FactionName = "Хор Рассвета",
                    FavoredArchetype = ShiningAbodeState.ProjectArchetypeAccord,
                    PatronEffectFamily = ShiningAbodeState.EffectFamilySocial,
                    Summary = "Поют утренний свет."
                },
                SupportingResidentIds = { "resident_liora", "resident_mael", "resident_serit" }
            });

            var error = await ShiningFactionRequestState.ValidateFoundingRequestAgainstCurrentStateAsync(fs, new ShiningFactionRequestState.PendingShiningFactionFoundingRequest
            {
                RequestId = "shared_request_id",
                ProposedFactionId = "faction_twilight",
                ProposedHallId = "hall_twilight",
                ProposedHallName = "Зал Сумерек",
                ProposedHallDescription = "Иной зал",
                ProposedHallServiceTags = { "social", "lore" },
                Charter = new ShiningFactionRequestState.FactionCharterPayload
                {
                    FactionName = "Хор Сумерек",
                    FavoredArchetype = ShiningAbodeState.ProjectArchetypeAccord,
                    PatronEffectFamily = ShiningAbodeState.EffectFamilySocial,
                    Summary = "Несут иной свет."
                },
                SupportingResidentIds = { "resident_liora", "resident_mael", "resident_serit" }
            });

            Assert.NotNull(error);
            Assert.Contains("requestId", error, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task ValidateFoundingRequestAgainstCurrentStateAsync_DuplicatePendingFactionId_Fails()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();
            await WriteMinimalShiningPoliticalStateAsync(fs);

            await ShiningFactionRequestState.WriteFoundingRequestAsync(fs, new ShiningFactionRequestState.PendingShiningFactionFoundingRequest
            {
                RequestId = "existing_founding",
                ProposedFactionId = "faction_dawn",
                ProposedHallId = "hall_dawn",
                ProposedHallName = "Зал Рассвета",
                ProposedHallDescription = "Светлый зал",
                ProposedHallServiceTags = { "social", "lore" },
                Charter = new ShiningFactionRequestState.FactionCharterPayload
                {
                    FactionName = "Хор Рассвета",
                    FavoredArchetype = ShiningAbodeState.ProjectArchetypeAccord,
                    PatronEffectFamily = ShiningAbodeState.EffectFamilySocial,
                    Summary = "Поют утренний свет."
                },
                SupportingResidentIds = { "resident_liora", "resident_mael", "resident_serit" }
            });

            var error = await ShiningFactionRequestState.ValidateFoundingRequestAgainstCurrentStateAsync(fs, new ShiningFactionRequestState.PendingShiningFactionFoundingRequest
            {
                RequestId = "new_founding",
                ProposedFactionId = "faction_dawn",
                ProposedHallId = "hall_dawn_second",
                ProposedHallName = "Второй Зал Рассвета",
                ProposedHallDescription = "Иной светлый зал",
                ProposedHallServiceTags = { "social", "lore" },
                Charter = new ShiningFactionRequestState.FactionCharterPayload
                {
                    FactionName = "Хор Второго Рассвета",
                    FavoredArchetype = ShiningAbodeState.ProjectArchetypeAccord,
                    PatronEffectFamily = ShiningAbodeState.EffectFamilySocial,
                    Summary = "Новый хор."
                },
                SupportingResidentIds = { "resident_liora", "resident_mael", "resident_serit" }
            });

            Assert.NotNull(error);
            Assert.Contains("Pending founding request", error, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task ValidateFoundingRequestAgainstCurrentStateAsync_DuplicatePendingHallId_Fails()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();
            await WriteMinimalShiningPoliticalStateAsync(fs);

            await ShiningFactionRequestState.WriteFoundingRequestAsync(fs, new ShiningFactionRequestState.PendingShiningFactionFoundingRequest
            {
                RequestId = "existing_founding",
                ProposedFactionId = "faction_dawn",
                ProposedHallId = "hall_shared",
                ProposedHallName = "Зал Рассвета",
                ProposedHallDescription = "Светлый зал",
                ProposedHallServiceTags = { "social", "lore" },
                Charter = new ShiningFactionRequestState.FactionCharterPayload
                {
                    FactionName = "Хор Рассвета",
                    FavoredArchetype = ShiningAbodeState.ProjectArchetypeAccord,
                    PatronEffectFamily = ShiningAbodeState.EffectFamilySocial,
                    Summary = "Поют утренний свет."
                },
                SupportingResidentIds = { "resident_liora", "resident_mael", "resident_serit" }
            });

            var error = await ShiningFactionRequestState.ValidateFoundingRequestAgainstCurrentStateAsync(fs, new ShiningFactionRequestState.PendingShiningFactionFoundingRequest
            {
                RequestId = "new_founding",
                ProposedFactionId = "faction_twilight",
                ProposedHallId = "hall_shared",
                ProposedHallName = "Зал Сумерек",
                ProposedHallDescription = "Иной светлый зал",
                ProposedHallServiceTags = { "social", "lore" },
                Charter = new ShiningFactionRequestState.FactionCharterPayload
                {
                    FactionName = "Хор Сумерек",
                    FavoredArchetype = ShiningAbodeState.ProjectArchetypeAccord,
                    PatronEffectFamily = ShiningAbodeState.EffectFamilySocial,
                    Summary = "Новый хор."
                },
                SupportingResidentIds = { "resident_liora", "resident_mael", "resident_serit" }
            });

            Assert.NotNull(error);
            Assert.Contains("proposedHallId", error, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task ValidateFoundingRequestAgainstCurrentStateAsync_MaterializedHallId_Fails()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();
            await WriteMinimalShiningPoliticalStateAsync(fs);

            var error = await ShiningFactionRequestState.ValidateFoundingRequestAgainstCurrentStateAsync(fs, new ShiningFactionRequestState.PendingShiningFactionFoundingRequest
            {
                RequestId = "new_founding",
                ProposedFactionId = "faction_twilight",
                ProposedHallId = "hall_new",
                ProposedHallName = "Новый Зал Сумерек",
                ProposedHallDescription = "Иной светлый зал",
                ProposedHallServiceTags = { "lore", "memory" },
                Charter = new ShiningFactionRequestState.FactionCharterPayload
                {
                    FactionName = "Хор Сумерек",
                    FavoredArchetype = ShiningAbodeState.ProjectArchetypeRevelation,
                    PatronEffectFamily = ShiningAbodeState.EffectFamilyLore,
                    Summary = "Новый хор."
                },
                SupportingResidentIds = { "resident_liora", "resident_mael", "resident_serit" }
            });

            Assert.NotNull(error);
            Assert.Contains("proposedHallId", error, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("materialized", error, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task ValidateRealignmentRequestAgainstCurrentStateAsync_ResidentBelowReadyToRealign_Fails()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();
            await WriteMinimalShiningPoliticalStateAsync(fs);

            var residentRoot = await ReadJsonAsync(fs, GuardianAbodeResidentState.StatePath);
            var resident = ((JsonArray?)residentRoot?["entries"])?.OfType<JsonObject>().First(entry => string.Equals(entry["residentId"]?.GetValue<string>(), "resident_liora", StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(resident);
            resident!["factionRealignmentState"] = ShiningAbodeState.FactionRealignmentStateRestless;
            await fs.WriteFileAtomicAsync(GuardianAbodeResidentState.StatePath, residentRoot!.ToJsonString());

            var error = await ShiningFactionRequestState.ValidateRealignmentRequestAgainstCurrentStateAsync(fs, new ShiningFactionRequestState.PendingShiningFactionRealignmentRequest
            {
                ResidentId = "resident_liora",
                ResidentName = "Лиора",
                SourceFactionId = "faction_old",
                SourceFactionName = "Старый Дом",
                TargetFactionId = "faction_new",
                TargetFactionName = "Новый Дом",
                RealignmentMode = ShiningFactionRequestState.RealignmentModeAcceptedTransfer,
                FactionLoyaltyLevel = 15,
                FactionLoyaltyTier = ShiningAbodeState.FactionLoyaltyTierAlienated,
                FactionRestlessness = 80,
                FactionRealignmentState = ShiningAbodeState.FactionRealignmentStateReadyToRealign
            });

            Assert.NotNull(error);
            Assert.Contains("ready_to_realign", error, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task ValidateLeadershipTransitionRequestAgainstCurrentStateAsync_OutsideFactionSupporter_Fails()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();
            await WriteMinimalShiningPoliticalStateAsync(fs);

            var error = await ShiningFactionRequestState.ValidateLeadershipTransitionRequestAgainstCurrentStateAsync(fs, new ShiningFactionRequestState.PendingShiningFactionLeadershipTransitionRequest
            {
                FactionId = "faction_old",
                FactionName = "Старый Дом",
                TransitionMode = ShiningFactionRequestState.TransitionModePeacefulSuccession,
                IncumbentHeadActorType = ShiningAbodeState.HeadActorTypeGuardian,
                IncumbentHeadActorId = "guardian_old",
                CandidateHeadActorType = ShiningAbodeState.HeadActorTypePlayerSoul,
                CandidateHeadActorId = ShiningAbodeState.HeadActorTypePlayerSoul,
                SupportingResidentIds = { "resident_liora", "resident_outsider" }
            });

            Assert.NotNull(error);
            Assert.Contains("той же фракции", error, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-shining-request-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static async Task WriteMinimalShiningPoliticalStateAsync(FileSystemManager fs)
    {
        var soulRoot = new JsonObject
        {
            ["currentRealm"] = "Shining Abode"
        };
        await fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", soulRoot.ToJsonString());

        var shiningRoot = ShiningAbodeState.CreateDefaultState();
        shiningRoot["factions"] = new JsonArray
        {
            new JsonObject
            {
                ["factionId"] = "faction_old",
                ["originType"] = ShiningAbodeState.OriginTypeAscendedGuardian,
                ["hallId"] = "hall_old",
                ["charter"] = new JsonObject
                {
                    ["factionName"] = "Старый Дом",
                    ["favoredArchetype"] = ShiningAbodeState.ProjectArchetypeAccord,
                    ["patronEffectFamily"] = ShiningAbodeState.EffectFamilySocial,
                    ["summary"] = "Старая сияющая фракция."
                },
                ["leadership"] = new JsonObject
                {
                    ["headActorType"] = ShiningAbodeState.HeadActorTypeGuardian,
                    ["headActorId"] = "guardian_old",
                    ["leadershipState"] = ShiningAbodeState.LeadershipStateSecure
                },
                ["baseStrength"] = 35,
                ["factionStrength"] = 45,
                ["investCountThisAscension"] = 0,
                ["projectArchetypesCountedThisAscension"] = new JsonArray(),
                ["projects"] = new JsonArray(),
                ["leadershipReceipts"] = new JsonArray(),
                ["leadershipHistory"] = new JsonArray()
            },
            new JsonObject
            {
                ["factionId"] = "faction_new",
                ["originType"] = ShiningAbodeState.OriginTypeNativeRadiant,
                ["hallId"] = "hall_new",
                ["charter"] = new JsonObject
                {
                    ["factionName"] = "Новый Дом",
                    ["favoredArchetype"] = ShiningAbodeState.ProjectArchetypeRevelation,
                    ["patronEffectFamily"] = ShiningAbodeState.EffectFamilyLore,
                    ["summary"] = "Новая сияющая фракция."
                },
                ["leadership"] = new JsonObject
                {
                    ["headActorType"] = ShiningAbodeState.HeadActorTypeRadiantActor,
                    ["headActorId"] = "radiant_actor_new_head",
                    ["leadershipState"] = ShiningAbodeState.LeadershipStateContested
                },
                ["baseStrength"] = 55,
                ["factionStrength"] = 62,
                ["investCountThisAscension"] = 0,
                ["projectArchetypesCountedThisAscension"] = new JsonArray(),
                ["projects"] = new JsonArray(),
                ["leadershipReceipts"] = new JsonArray(),
                ["leadershipHistory"] = new JsonArray()
            }
        };
        shiningRoot["halls"] = new JsonArray
        {
            new JsonObject
            {
                ["hallId"] = "hall_old",
                ["hallName"] = "Старый Зал",
                ["description"] = "Описание",
                ["serviceTags"] = new JsonArray("social", "lore")
            },
            new JsonObject
            {
                ["hallId"] = "hall_new",
                ["hallName"] = "Новый Зал",
                ["description"] = "Описание",
                ["serviceTags"] = new JsonArray("lore", "memory")
            }
        };
        shiningRoot["shiningPoliticalActors"] = new JsonArray
        {
            new JsonObject
            {
                ["actorId"] = "radiant_actor_new_head",
                ["actorType"] = ShiningAbodeState.HeadActorTypeRadiantActor,
                ["displayName"] = "Архон Тир",
                ["summary"] = "Старый политический актор.",
                ["originFactionId"] = "faction_new",
                ["currentFactionId"] = "faction_new",
                ["politicalStatus"] = ShiningAbodeState.PoliticalStatusHead
            }
        };
        await fs.WriteFileAtomicAsync(ShiningAbodeState.StatePath, shiningRoot.ToJsonString());

        var residentRoot = new JsonObject
        {
            ["entries"] = new JsonArray
            {
                new JsonObject
                {
                    ["residentId"] = "resident_liora",
                    ["displayName"] = "Лиора",
                    ["ascensionState"] = ShiningAbodeState.AscensionStateAscended,
                    ["shiningFactionId"] = "faction_old",
                    ["factionLoyaltyLevel"] = 15,
                    ["factionLoyaltyTier"] = ShiningAbodeState.FactionLoyaltyTierAlienated,
                    ["factionRestlessness"] = 80,
                    ["factionRealignmentState"] = ShiningAbodeState.FactionRealignmentStateReadyToRealign
                },
                new JsonObject
                {
                    ["residentId"] = "resident_mael",
                    ["displayName"] = "Маэль",
                    ["ascensionState"] = ShiningAbodeState.AscensionStateAscended,
                    ["shiningFactionId"] = "faction_old",
                    ["factionLoyaltyLevel"] = 40,
                    ["factionLoyaltyTier"] = ShiningAbodeState.FactionLoyaltyTierAttached,
                    ["factionRestlessness"] = 35,
                    ["factionRealignmentState"] = ShiningAbodeState.FactionRealignmentStateWavering
                },
                new JsonObject
                {
                    ["residentId"] = "resident_serit",
                    ["displayName"] = "Серит",
                    ["ascensionState"] = ShiningAbodeState.AscensionStateAscended,
                    ["shiningFactionId"] = "faction_old",
                    ["factionLoyaltyLevel"] = 42,
                    ["factionLoyaltyTier"] = ShiningAbodeState.FactionLoyaltyTierAttached,
                    ["factionRestlessness"] = 30,
                    ["factionRealignmentState"] = ShiningAbodeState.FactionRealignmentStateWavering
                },
                new JsonObject
                {
                    ["residentId"] = "resident_outsider",
                    ["displayName"] = "Аутсайдер",
                    ["ascensionState"] = ShiningAbodeState.AscensionStateAscended,
                    ["shiningFactionId"] = "faction_new",
                    ["factionLoyaltyLevel"] = 65,
                    ["factionLoyaltyTier"] = ShiningAbodeState.FactionLoyaltyTierDevoted,
                    ["factionRestlessness"] = 10,
                    ["factionRealignmentState"] = ShiningAbodeState.FactionRealignmentStateSettled
                }
            }
        };
        await fs.WriteFileAtomicAsync(GuardianAbodeResidentState.StatePath, residentRoot.ToJsonString());

        var guardiansRoot = new JsonObject
        {
            ["activeGuardian"] = new JsonObject
            {
                ["guardianId"] = "guardian_old",
                ["guardianName"] = "Азалия"
            },
            ["guardians"] = new JsonArray
            {
                new JsonObject
                {
                    ["guardianId"] = "guardian_old",
                    ["guardianName"] = "Азалия"
                }
            }
        };
        await fs.WriteFileAtomicAsync("game_state/meta/guardians.json", guardiansRoot.ToJsonString());
    }

    private static async Task<JsonObject?> ReadJsonAsync(FileSystemManager fs, string path)
    {
        var json = await fs.ReadFileAsync(path);
        return string.IsNullOrWhiteSpace(json) ? null : JsonNode.Parse(json) as JsonObject;
    }

    private static void CleanupTempRoot(string root)
    {
        try
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
        catch
        {
            // ignored
        }
    }
}
