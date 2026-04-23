using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ShiningPoliticalResolutionValidationTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly ValidationService _validator;

    public ShiningPoliticalResolutionValidationTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-shining-political-resolution-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
        _validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
    }

    [Fact]
    public async Task ValidatePendingShiningFoundingResolutionAsync_AcceptedFoundingWithCanonicalMaterialization_Passes()
    {
        const string requestId = "founding_req_dawn_choir";
        const string proposedFactionId = "faction_dawn_choir";
        const string proposedHallId = "hall_dawn_choir";
        var request = new JsonObject
        {
            ["requestId"] = requestId,
            ["proposedFactionId"] = proposedFactionId,
            ["proposedHallId"] = proposedHallId,
            ["proposedHallName"] = "Зал Рассветного Хора",
            ["proposedHallDescription"] = "Светлый зал для союзов, клятв и общих песен.",
            ["proposedHallServiceTags"] = new JsonArray("social", "lore"),
            ["charter"] = new JsonObject
            {
                ["factionName"] = "Хор Рассвета",
                ["favoredArchetype"] = ShiningAbodeState.ProjectArchetypeAccord,
                ["patronEffectFamily"] = ShiningAbodeState.EffectFamilySocial,
                ["summary"] = "Союз резидентов, которые строят силу через согласие."
            },
            ["supportingResidentIds"] = new JsonArray("resident_liora", "resident_mael", "resident_serit"),
            ["createdAtTurn"] = 184,
            ["createdAtUtc"] = "2026-04-16T15:20:00Z"
        };

        var shiningRoot = CreateBaseShiningRoot();
        ((JsonArray)shiningRoot["halls"]!).Add(new JsonObject
        {
            ["hallId"] = proposedHallId,
            ["hallName"] = "Зал Рассветного Хора",
            ["description"] = "Светлый зал для союзов, клятв и общих песен.",
            ["serviceTags"] = new JsonArray("social", "lore")
        });
        ((JsonArray)shiningRoot["factions"]!).Add(new JsonObject
        {
            ["factionId"] = proposedFactionId,
            ["originType"] = ShiningAbodeState.OriginTypePlayerFounded,
            ["hallId"] = proposedHallId,
            ["charter"] = new JsonObject
            {
                ["factionName"] = "Хор Рассвета",
                ["favoredArchetype"] = ShiningAbodeState.ProjectArchetypeAccord,
                ["patronEffectFamily"] = ShiningAbodeState.EffectFamilySocial,
                ["summary"] = "Союз резидентов, которые строят силу через согласие."
            },
            ["leadership"] = new JsonObject
            {
                ["headActorType"] = ShiningAbodeState.HeadActorTypePlayerSoul,
                ["headActorId"] = ShiningAbodeState.HeadActorTypePlayerSoul,
                ["leadershipState"] = ShiningAbodeState.LeadershipStateSecure
            },
            ["baseStrength"] = 35,
            ["factionStrength"] = 44,
            ["investCountThisAscension"] = 0,
            ["projectArchetypesCountedThisAscension"] = new JsonArray(),
            ["projects"] = new JsonArray(),
            ["leadershipReceipts"] = new JsonArray(),
            ["leadershipHistory"] = new JsonArray()
        });
        ((JsonArray)shiningRoot["factionFoundingReceipts"]!).Add(new JsonObject
        {
            ["requestId"] = requestId,
            ["proposedFactionId"] = proposedFactionId,
            ["proposedHallId"] = proposedHallId,
            ["hallName"] = "Зал Рассветного Хора",
            ["factionId"] = proposedFactionId,
            ["hallId"] = proposedHallId,
            ["status"] = ShiningFactionRequestState.RequestStatusAccepted,
            ["supportingResidentIds"] = new JsonArray("resident_liora", "resident_mael", "resident_serit"),
            ["resolvedAtTurn"] = 184,
            ["resolvedAtUtc"] = "2026-04-16T15:24:00Z",
            ["reason"] = "founding_accepted"
        });

        var residentRoot = CreateBaseResidentRoot();
        MoveResidentsToFaction(residentRoot, proposedFactionId, "resident_liora", "resident_mael", "resident_serit");

        await SeedCurrentStateAsync(shiningRoot, residentRoot);
        await WriteNodeAsync(ShiningFactionRequestState.PendingFoundingsRequestPath, new JsonObject
        {
            [ShiningFactionRequestState.RequestsProperty] = new JsonArray(request.DeepClone())
        });
        const string backupPath = "game_state/control/pending_turn_snapshot/pre_shining_founding_request.json";
        await WriteNodeAsync(backupPath, new JsonObject
        {
            [ShiningFactionRequestState.RequestsProperty] = new JsonArray(request)
        });
        await WritePendingTurnSnapshotManifestAsync(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [ShiningFactionRequestState.PendingFoundingsRequestPath] = backupPath
        });

        var issues = await InvokeValidationAsync("ValidatePendingShiningFoundingResolutionAsync");

        Assert.DoesNotContain(issues, issue => string.Equals(issue.Code, "shining_founding_missing_resolution", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue => string.Equals(issue.Code, "shining_founding_missing_faction_materialization", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue => string.Equals(issue.Code, "shining_founding_supporter_not_reassigned", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidatePendingShiningFoundingResolutionAsync_AcceptedFoundingWithoutClosureMarkers_Fails()
    {
        const string requestId = "founding_req_missing_closure";
        var request = new JsonObject
        {
            ["requestId"] = requestId,
            ["proposedFactionId"] = "faction_dawn_choir",
            ["proposedHallId"] = "hall_dawn_choir",
            ["proposedHallName"] = "Зал Рассветного Хора",
            ["proposedHallDescription"] = "Светлый зал для союзов.",
            ["proposedHallServiceTags"] = new JsonArray("social", "lore"),
            ["charter"] = new JsonObject
            {
                ["factionName"] = "Хор Рассвета",
                ["favoredArchetype"] = ShiningAbodeState.ProjectArchetypeAccord,
                ["patronEffectFamily"] = ShiningAbodeState.EffectFamilySocial,
                ["summary"] = "Собирает утренние клятвы."
            },
            ["supportingResidentIds"] = new JsonArray("resident_liora", "resident_mael"),
            ["createdAtTurn"] = 184,
            ["createdAtUtc"] = "2026-04-16T15:20:00Z"
        };

        var shiningRoot = CreateBaseShiningRoot();
        ((JsonArray)shiningRoot["halls"]!).Add(new JsonObject
        {
            ["hallId"] = "hall_dawn_choir",
            ["hallName"] = "Зал Рассветного Хора",
            ["description"] = "Светлый зал для союзов.",
            ["serviceTags"] = new JsonArray("social", "lore")
        });
        ((JsonArray)shiningRoot["factions"]!).Add(new JsonObject
        {
            ["factionId"] = "faction_dawn_choir",
            ["originType"] = ShiningAbodeState.OriginTypePlayerFounded,
            ["hallId"] = "hall_dawn_choir",
            ["charter"] = new JsonObject
            {
                ["factionName"] = "Хор Рассвета",
                ["favoredArchetype"] = ShiningAbodeState.ProjectArchetypeAccord,
                ["patronEffectFamily"] = ShiningAbodeState.EffectFamilySocial,
                ["summary"] = "Собирает утренние клятвы."
            },
            ["leadership"] = new JsonObject
            {
                ["headActorType"] = ShiningAbodeState.HeadActorTypePlayerSoul,
                ["headActorId"] = ShiningAbodeState.HeadActorTypePlayerSoul,
                ["leadershipState"] = ShiningAbodeState.LeadershipStateSecure
            },
            ["baseStrength"] = 35,
            ["factionStrength"] = 44,
            ["investCountThisAscension"] = 0,
            ["projectArchetypesCountedThisAscension"] = new JsonArray(),
            ["projects"] = new JsonArray(),
            ["leadershipReceipts"] = new JsonArray(),
            ["leadershipHistory"] = new JsonArray()
        });
        ((JsonArray)shiningRoot["factionFoundingReceipts"]!).Add(new JsonObject
        {
            ["requestId"] = requestId,
            ["proposedFactionId"] = "faction_dawn_choir",
            ["proposedHallId"] = "hall_dawn_choir",
            ["hallName"] = "Зал Рассветного Хора",
            ["factionId"] = "faction_dawn_choir",
            ["hallId"] = "hall_dawn_choir",
            ["status"] = ShiningFactionRequestState.RequestStatusAccepted,
            ["supportingResidentIds"] = new JsonArray("resident_liora", "resident_mael"),
            ["resolvedAtTurn"] = 0,
            ["resolvedAtUtc"] = "",
            ["reason"] = "founding_accepted"
        });

        await SeedCurrentStateAsync(shiningRoot, CreateBaseResidentRoot());
        await WriteNodeAsync(ShiningFactionRequestState.PendingFoundingsRequestPath, new JsonObject
        {
            [ShiningFactionRequestState.RequestsProperty] = new JsonArray(request.DeepClone())
        });
        const string backupPath = "game_state/control/pending_turn_snapshot/pre_shining_founding_missing_closure.json";
        await WriteNodeAsync(backupPath, new JsonObject
        {
            [ShiningFactionRequestState.RequestsProperty] = new JsonArray(request)
        });
        await WritePendingTurnSnapshotManifestAsync(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [ShiningFactionRequestState.PendingFoundingsRequestPath] = backupPath
        });

        var issues = await InvokeValidationAsync("ValidatePendingShiningFoundingResolutionAsync");

        Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_founding_receipt_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameState_PendingShiningFoundingsWithDuplicateRequestId_Fails()
    {
        var shiningRoot = CreateBaseShiningRoot();
        var residentRoot = CreateBaseResidentRoot();
        await SeedCurrentStateAsync(shiningRoot, residentRoot);
        await WriteNodeAsync(ShiningFactionRequestState.PendingFoundingsRequestPath, new JsonObject
        {
            [ShiningFactionRequestState.RequestsProperty] = new JsonArray
            {
                new JsonObject
                {
                    ["requestId"] = "founding_req_shared",
                    ["proposedFactionId"] = "faction_dawn_choir",
                    ["proposedHallId"] = "hall_dawn_choir",
                    ["proposedHallName"] = "Зал Рассветного Хора",
                    ["proposedHallDescription"] = "Светлый зал",
                    ["proposedHallServiceTags"] = new JsonArray("social", "lore"),
                    ["charter"] = new JsonObject
                    {
                        ["factionName"] = "Хор Рассвета",
                        ["favoredArchetype"] = ShiningAbodeState.ProjectArchetypeAccord,
                        ["patronEffectFamily"] = ShiningAbodeState.EffectFamilySocial,
                        ["summary"] = "Поют утренний свет."
                    },
                    ["supportingResidentIds"] = new JsonArray("resident_liora", "resident_mael", "resident_serit"),
                    ["createdAtTurn"] = 184,
                    ["createdAtUtc"] = "2026-04-16T15:20:00Z"
                },
                new JsonObject
                {
                    ["requestId"] = "founding_req_shared",
                    ["proposedFactionId"] = "faction_twilight_choir",
                    ["proposedHallId"] = "hall_twilight_choir",
                    ["proposedHallName"] = "Зал Сумеречного Хора",
                    ["proposedHallDescription"] = "Иной зал",
                    ["proposedHallServiceTags"] = new JsonArray("social", "lore"),
                    ["charter"] = new JsonObject
                    {
                        ["factionName"] = "Хор Сумерек",
                        ["favoredArchetype"] = ShiningAbodeState.ProjectArchetypeAccord,
                        ["patronEffectFamily"] = ShiningAbodeState.EffectFamilySocial,
                        ["summary"] = "Несут иной свет."
                    },
                    ["supportingResidentIds"] = new JsonArray("resident_liora", "resident_mael", "resident_serit"),
                    ["createdAtTurn"] = 185,
                    ["createdAtUtc"] = "2026-04-16T15:24:00Z"
                }
            }
        });

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_founding_duplicate_request_id", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidatePendingShiningRealignmentResolutionAsync_AcceptedTransferWithoutHistory_Fails()
    {
        const string requestId = "realign_req_liora";
        var request = new JsonObject
        {
            ["requestId"] = requestId,
            ["residentId"] = "resident_liora",
            ["residentName"] = "Лиора",
            ["sourceFactionId"] = "faction_old",
            ["sourceFactionName"] = "Старый Дом",
            ["targetFactionId"] = "faction_new",
            ["targetFactionName"] = "Новый Дом",
            ["realignmentMode"] = ShiningFactionRequestState.RealignmentModeAcceptedTransfer,
            ["factionLoyaltyLevel"] = 14,
            ["factionLoyaltyTier"] = ShiningAbodeState.FactionLoyaltyTierAlienated,
            ["factionRestlessness"] = 76,
            ["factionRealignmentState"] = ShiningAbodeState.FactionRealignmentStateReadyToRealign,
            ["createdAtTurn"] = 192,
            ["createdAtUtc"] = "2026-04-16T16:05:00Z"
        };

        var shiningRoot = CreateBaseShiningRoot();
        ((JsonArray)shiningRoot["factionRealignmentReceipts"]!).Add(new JsonObject
        {
            ["requestId"] = requestId,
            ["residentId"] = "resident_liora",
            ["residentName"] = "Лиора",
            ["sourceFactionId"] = "faction_old",
            ["targetFactionId"] = "faction_new",
            ["status"] = ShiningFactionRequestState.RequestStatusAccepted,
            ["realignmentMode"] = ShiningFactionRequestState.RealignmentModeAcceptedTransfer,
            ["residentHistoryEntryId"] = "history_resident_liora_missing",
            ["resolvedAtTurn"] = 192,
            ["resolvedAtUtc"] = "2026-04-16T16:08:00Z",
            ["reason"] = "accepted_by_target_faction"
        });

        var residentRoot = CreateBaseResidentRoot();
        MoveResidentsToFaction(residentRoot, "faction_new", "resident_liora");

        await SeedCurrentStateAsync(shiningRoot, residentRoot);
        await WriteNodeAsync(ShiningFactionRequestState.PendingRealignmentsRequestPath, new JsonObject
        {
            [ShiningFactionRequestState.RequestsProperty] = new JsonArray(request.DeepClone())
        });
        const string backupPath = "game_state/control/pending_turn_snapshot/pre_shining_realignment_request.json";
        await WriteNodeAsync(backupPath, new JsonObject
        {
            [ShiningFactionRequestState.RequestsProperty] = new JsonArray(request)
        });
        await WritePendingTurnSnapshotManifestAsync(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [ShiningFactionRequestState.PendingRealignmentsRequestPath] = backupPath
        });

        var issues = await InvokeValidationAsync("ValidatePendingShiningRealignmentResolutionAsync");

        Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_realignment_missing_history_entry", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameState_PendingShiningRealignmentsWithDuplicateRequestId_Fails()
    {
        var shiningRoot = CreateBaseShiningRoot();
        var residentRoot = CreateBaseResidentRoot();
        await SeedCurrentStateAsync(shiningRoot, residentRoot);
        await WriteNodeAsync(ShiningFactionRequestState.PendingRealignmentsRequestPath, new JsonObject
        {
            [ShiningFactionRequestState.RequestsProperty] = new JsonArray
            {
                new JsonObject
                {
                    ["requestId"] = "realign_req_shared",
                    ["residentId"] = "resident_liora",
                    ["residentName"] = "Лиора",
                    ["sourceFactionId"] = "faction_old",
                    ["sourceFactionName"] = "Старый Дом",
                    ["targetFactionId"] = "faction_new",
                    ["targetFactionName"] = "Новый Дом",
                    ["realignmentMode"] = ShiningFactionRequestState.RealignmentModeAcceptedTransfer,
                    ["factionLoyaltyLevel"] = 14,
                    ["factionLoyaltyTier"] = ShiningAbodeState.FactionLoyaltyTierAlienated,
                    ["factionRestlessness"] = 76,
                    ["factionRealignmentState"] = ShiningAbodeState.FactionRealignmentStateReadyToRealign,
                    ["createdAtTurn"] = 192,
                    ["createdAtUtc"] = "2026-04-16T16:05:00Z"
                },
                new JsonObject
                {
                    ["requestId"] = "realign_req_shared",
                    ["residentId"] = "resident_mael",
                    ["residentName"] = "Маэль",
                    ["sourceFactionId"] = "faction_old",
                    ["sourceFactionName"] = "Старый Дом",
                    ["targetFactionId"] = "",
                    ["targetFactionName"] = "",
                    ["realignmentMode"] = ShiningFactionRequestState.RealignmentModeDepartureToNeutral,
                    ["factionLoyaltyLevel"] = 7,
                    ["factionLoyaltyTier"] = ShiningAbodeState.FactionLoyaltyTierAlienated,
                    ["factionRestlessness"] = 85,
                    ["factionRealignmentState"] = ShiningAbodeState.FactionRealignmentStateReadyToRealign,
                    ["createdAtTurn"] = 193,
                    ["createdAtUtc"] = "2026-04-16T16:08:00Z"
                }
            }
        });

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_realignment_duplicate_request_id", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameState_NonVacantLeadershipWithoutCanonicalHeadBinding_Fails()
    {
        var shiningRoot = CreateBaseShiningRoot();
        var faction = ((JsonArray)shiningRoot["factions"]!).OfType<JsonObject>().First();
        faction["leadership"] = new JsonObject
        {
            ["headActorType"] = ShiningAbodeState.HeadActorTypePlayerSoul,
            ["headActorId"] = "",
            ["leadershipState"] = ShiningAbodeState.LeadershipStateSecure
        };

        await SeedCurrentStateAsync(shiningRoot, CreateBaseResidentRoot());

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "shining_leadership_missing_head_binding", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(issue.Code, "shining_leadership_invalid_player_soul_binding", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidatePendingShiningLeadershipTransitionResolutionAsync_AcceptedSuccessionWithHistory_Passes()
    {
        const string requestId = "leadership_req_old";
        var request = new JsonObject
        {
            ["requestId"] = requestId,
            ["factionId"] = "faction_old",
            ["factionName"] = "Старый Дом",
            ["transitionMode"] = ShiningFactionRequestState.TransitionModePeacefulSuccession,
            ["incumbentHeadActorType"] = ShiningAbodeState.HeadActorTypeGuardian,
            ["incumbentHeadActorId"] = "guardian_old",
            ["candidateHeadActorType"] = ShiningAbodeState.HeadActorTypePlayerSoul,
            ["candidateHeadActorId"] = ShiningAbodeState.HeadActorTypePlayerSoul,
            ["supportingResidentIds"] = new JsonArray("resident_liora", "resident_mael"),
            ["createdAtTurn"] = 203,
            ["createdAtUtc"] = "2026-04-16T16:40:00Z"
        };

        var shiningRoot = CreateBaseShiningRoot();
        var oldFaction = ((JsonArray)shiningRoot["factions"]!).OfType<JsonObject>().First(faction =>
            string.Equals(faction["factionId"]?.GetValue<string>(), "faction_old", StringComparison.OrdinalIgnoreCase));
        oldFaction["leadership"] = new JsonObject
        {
            ["headActorType"] = ShiningAbodeState.HeadActorTypePlayerSoul,
            ["headActorId"] = ShiningAbodeState.HeadActorTypePlayerSoul,
            ["leadershipState"] = ShiningAbodeState.LeadershipStateSecure
        };
        ((JsonArray)oldFaction["leadershipReceipts"]!).Add(new JsonObject
        {
            ["requestId"] = requestId,
            ["transitionMode"] = ShiningFactionRequestState.TransitionModePeacefulSuccession,
            ["previousHeadActorType"] = ShiningAbodeState.HeadActorTypeGuardian,
            ["previousHeadActorId"] = "guardian_old",
            ["newHeadActorType"] = ShiningAbodeState.HeadActorTypePlayerSoul,
            ["newHeadActorId"] = ShiningAbodeState.HeadActorTypePlayerSoul,
            ["status"] = ShiningFactionRequestState.RequestStatusAccepted,
            ["resolvedAtTurn"] = 203,
            ["resolvedAtUtc"] = "2026-04-16T16:44:00Z",
            ["reason"] = "recognized_succession"
        });
        ((JsonArray)oldFaction["leadershipHistory"]!).Add(new JsonObject
        {
            ["eventId"] = "leadership_evt_old_203",
            ["requestId"] = requestId,
            ["eventType"] = "succeeded",
            ["summary"] = "Игрок мирно принял руководство Старым Домом.",
            ["turnNumber"] = 203,
            ["occurredAtUtc"] = "2026-04-16T16:44:00Z"
        });

        await SeedCurrentStateAsync(shiningRoot, CreateBaseResidentRoot());
        await WriteNodeAsync(ShiningFactionRequestState.PendingLeadershipTransitionsRequestPath, new JsonObject
        {
            [ShiningFactionRequestState.RequestsProperty] = new JsonArray(request.DeepClone())
        });
        const string backupPath = "game_state/control/pending_turn_snapshot/pre_shining_leadership_request.json";
        await WriteNodeAsync(backupPath, new JsonObject
        {
            [ShiningFactionRequestState.RequestsProperty] = new JsonArray(request)
        });
        await WritePendingTurnSnapshotManifestAsync(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [ShiningFactionRequestState.PendingLeadershipTransitionsRequestPath] = backupPath
        });

        var issues = await InvokeValidationAsync("ValidatePendingShiningLeadershipTransitionResolutionAsync");

        Assert.DoesNotContain(issues, issue => string.Equals(issue.Code, "shining_leadership_missing_resolution", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue => string.Equals(issue.Code, "shining_leadership_missing_history", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue => string.Equals(issue.Code, "shining_leadership_missing_state_update", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidatePendingShiningLeadershipTransitionResolutionAsync_AcceptedSuccessionWithContestedState_Fails()
    {
        const string requestId = "leadership_req_contested_state";
        var request = new JsonObject
        {
            ["requestId"] = requestId,
            ["factionId"] = "faction_old",
            ["factionName"] = "Старый Дом",
            ["transitionMode"] = ShiningFactionRequestState.TransitionModePeacefulSuccession,
            ["incumbentHeadActorType"] = ShiningAbodeState.HeadActorTypeGuardian,
            ["incumbentHeadActorId"] = "guardian_old",
            ["candidateHeadActorType"] = ShiningAbodeState.HeadActorTypePlayerSoul,
            ["candidateHeadActorId"] = ShiningAbodeState.HeadActorTypePlayerSoul,
            ["supportingResidentIds"] = new JsonArray("resident_liora", "resident_mael"),
            ["createdAtTurn"] = 203,
            ["createdAtUtc"] = "2026-04-16T16:40:00Z"
        };

        var shiningRoot = CreateBaseShiningRoot();
        var oldFaction = ((JsonArray)shiningRoot["factions"]!).OfType<JsonObject>().First(faction =>
            string.Equals(faction["factionId"]?.GetValue<string>(), "faction_old", StringComparison.OrdinalIgnoreCase));
        oldFaction["leadership"] = new JsonObject
        {
            ["headActorType"] = ShiningAbodeState.HeadActorTypePlayerSoul,
            ["headActorId"] = ShiningAbodeState.HeadActorTypePlayerSoul,
            ["leadershipState"] = ShiningAbodeState.LeadershipStateContested
        };
        ((JsonArray)oldFaction["leadershipReceipts"]!).Add(new JsonObject
        {
            ["requestId"] = requestId,
            ["transitionMode"] = ShiningFactionRequestState.TransitionModePeacefulSuccession,
            ["previousHeadActorType"] = ShiningAbodeState.HeadActorTypeGuardian,
            ["previousHeadActorId"] = "guardian_old",
            ["newHeadActorType"] = ShiningAbodeState.HeadActorTypePlayerSoul,
            ["newHeadActorId"] = ShiningAbodeState.HeadActorTypePlayerSoul,
            ["status"] = ShiningFactionRequestState.RequestStatusAccepted,
            ["resolvedAtTurn"] = 203,
            ["resolvedAtUtc"] = "2026-04-16T16:44:00Z",
            ["reason"] = "recognized_succession"
        });
        ((JsonArray)oldFaction["leadershipHistory"]!).Add(new JsonObject
        {
            ["eventId"] = "leadership_evt_old_203",
            ["requestId"] = requestId,
            ["eventType"] = "succeeded",
            ["summary"] = "Игрок мирно принял руководство Старым Домом.",
            ["turnNumber"] = 203,
            ["occurredAtUtc"] = "2026-04-16T16:44:00Z"
        });

        await SeedCurrentStateAsync(shiningRoot, CreateBaseResidentRoot());
        await WriteNodeAsync(ShiningFactionRequestState.PendingLeadershipTransitionsRequestPath, new JsonObject
        {
            [ShiningFactionRequestState.RequestsProperty] = new JsonArray(request.DeepClone())
        });
        const string backupPath = "game_state/control/pending_turn_snapshot/pre_shining_leadership_contested_state.json";
        await WriteNodeAsync(backupPath, new JsonObject
        {
            [ShiningFactionRequestState.RequestsProperty] = new JsonArray(request)
        });
        await WritePendingTurnSnapshotManifestAsync(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [ShiningFactionRequestState.PendingLeadershipTransitionsRequestPath] = backupPath
        });

        var issues = await InvokeValidationAsync("ValidatePendingShiningLeadershipTransitionResolutionAsync");

        Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_leadership_missing_state_update", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidatePendingShiningLeadershipTransitionResolutionAsync_AcceptedSuccessionWithoutHistory_Fails()
    {
        const string requestId = "leadership_req_old_missing_history";
        var request = new JsonObject
        {
            ["requestId"] = requestId,
            ["factionId"] = "faction_old",
            ["factionName"] = "Старый Дом",
            ["transitionMode"] = ShiningFactionRequestState.TransitionModePeacefulSuccession,
            ["incumbentHeadActorType"] = ShiningAbodeState.HeadActorTypeGuardian,
            ["incumbentHeadActorId"] = "guardian_old",
            ["candidateHeadActorType"] = ShiningAbodeState.HeadActorTypePlayerSoul,
            ["candidateHeadActorId"] = ShiningAbodeState.HeadActorTypePlayerSoul,
            ["supportingResidentIds"] = new JsonArray("resident_liora", "resident_mael"),
            ["createdAtTurn"] = 203,
            ["createdAtUtc"] = "2026-04-16T16:40:00Z"
        };

        var shiningRoot = CreateBaseShiningRoot();
        var oldFaction = ((JsonArray)shiningRoot["factions"]!).OfType<JsonObject>().First(faction =>
            string.Equals(faction["factionId"]?.GetValue<string>(), "faction_old", StringComparison.OrdinalIgnoreCase));
        oldFaction["leadership"] = new JsonObject
        {
            ["headActorType"] = ShiningAbodeState.HeadActorTypePlayerSoul,
            ["headActorId"] = ShiningAbodeState.HeadActorTypePlayerSoul,
            ["leadershipState"] = ShiningAbodeState.LeadershipStateSecure
        };
        ((JsonArray)oldFaction["leadershipReceipts"]!).Add(new JsonObject
        {
            ["requestId"] = requestId,
            ["transitionMode"] = ShiningFactionRequestState.TransitionModePeacefulSuccession,
            ["previousHeadActorType"] = ShiningAbodeState.HeadActorTypeGuardian,
            ["previousHeadActorId"] = "guardian_old",
            ["newHeadActorType"] = ShiningAbodeState.HeadActorTypePlayerSoul,
            ["newHeadActorId"] = ShiningAbodeState.HeadActorTypePlayerSoul,
            ["status"] = ShiningFactionRequestState.RequestStatusAccepted,
            ["resolvedAtTurn"] = 203,
            ["resolvedAtUtc"] = "2026-04-16T16:44:00Z",
            ["reason"] = "recognized_succession"
        });

        await SeedCurrentStateAsync(shiningRoot, CreateBaseResidentRoot());
        await WriteNodeAsync(ShiningFactionRequestState.PendingLeadershipTransitionsRequestPath, new JsonObject
        {
            [ShiningFactionRequestState.RequestsProperty] = new JsonArray(request.DeepClone())
        });
        const string backupPath = "game_state/control/pending_turn_snapshot/pre_shining_leadership_missing_history_request.json";
        await WriteNodeAsync(backupPath, new JsonObject
        {
            [ShiningFactionRequestState.RequestsProperty] = new JsonArray(request)
        });
        await WritePendingTurnSnapshotManifestAsync(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [ShiningFactionRequestState.PendingLeadershipTransitionsRequestPath] = backupPath
        });

        var issues = await InvokeValidationAsync("ValidatePendingShiningLeadershipTransitionResolutionAsync");

        Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_leadership_missing_history", StringComparison.OrdinalIgnoreCase));
    }

    private async Task SeedCurrentStateAsync(JsonObject shiningRoot, JsonObject residentRoot)
    {
        await WriteNodeAsync("game_state/meta/soul_state.json", new JsonObject
        {
            ["currentRealm"] = "Shining Abode",
            ["currentIncarnation"] = 2,
            ["soulName"] = "Тестовая душа"
        });
        await WriteNodeAsync("game_state/meta/guardians.json", new JsonObject
        {
            ["guardians"] = new JsonArray
            {
                new JsonObject
                {
                    ["guardianId"] = "guardian_old",
                    ["guardianName"] = "Азалия"
                }
            }
        });
        await WriteNodeAsync(ShiningAbodeState.StatePath, shiningRoot);
        await WriteNodeAsync(GuardianAbodeResidentState.StatePath, residentRoot);
        await WriteNodeAsync("ready/turn_complete.json", new JsonObject { ["accepted"] = true });
    }

    private static JsonObject CreateBaseShiningRoot()
    {
        var root = ShiningAbodeState.CreateDefaultState();
        root["availability"] = ShiningAbodeState.AvailabilityActive;
        root["radiance"] = new JsonObject
        {
            ["experience"] = 250,
            ["tier"] = 2
        };
        root["lightSparks"] = 80;
        root["halls"] = new JsonArray
        {
            new JsonObject
            {
                ["hallId"] = "hall_old",
                ["hallName"] = "Старый Зал",
                ["description"] = "Старый зал союза.",
                ["serviceTags"] = new JsonArray("social", "lore")
            },
            new JsonObject
            {
                ["hallId"] = "hall_new",
                ["hallName"] = "Новый Зал",
                ["description"] = "Новый зал согласия.",
                ["serviceTags"] = new JsonArray("lore", "memory")
            }
        };
        root["factions"] = new JsonArray
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
                ["factionStrength"] = 44,
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
                ["factionStrength"] = 61,
                ["investCountThisAscension"] = 0,
                ["projectArchetypesCountedThisAscension"] = new JsonArray(),
                ["projects"] = new JsonArray(),
                ["leadershipReceipts"] = new JsonArray(),
                ["leadershipHistory"] = new JsonArray()
            }
        };
        root["shiningPoliticalActors"] = new JsonArray
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
        root["factionFoundingReceipts"] = new JsonArray();
        root["factionRealignmentReceipts"] = new JsonArray();
        return root;
    }

    private static JsonObject CreateBaseResidentRoot()
    {
        return new JsonObject
        {
            ["entries"] = new JsonArray
            {
                CreateResident("resident_liora", "Лиора", "faction_old", 15, 80, ShiningAbodeState.FactionRealignmentStateReadyToRealign),
                CreateResident("resident_mael", "Маэль", "faction_old", 40, 35, ShiningAbodeState.FactionRealignmentStateWavering),
                CreateResident("resident_serit", "Серит", "faction_old", 42, 30, ShiningAbodeState.FactionRealignmentStateWavering),
                CreateResident("resident_outsider", "Аутсайдер", "faction_new", 65, 10, ShiningAbodeState.FactionRealignmentStateSettled)
            },
            [GuardianAbodeResidentState.HistoryLogProperty] = new JsonArray()
        };
    }

    private static JsonObject CreateResident(string residentId, string displayName, string factionId, int loyalty, int restlessness, string state)
    {
        return new JsonObject
        {
            ["residentId"] = residentId,
            ["displayName"] = displayName,
            ["ascensionState"] = ShiningAbodeState.AscensionStateAscended,
            ["shiningFactionId"] = factionId,
            ["factionLoyaltyLevel"] = loyalty,
            ["factionLoyaltyTier"] = ShiningAbodeState.ResolveFactionLoyaltyTier(loyalty),
            ["factionRestlessness"] = restlessness,
            ["factionRealignmentState"] = state
        };
    }

    private static void MoveResidentsToFaction(JsonObject residentRoot, string factionId, params string[] residentIds)
    {
        var entries = (JsonArray?)residentRoot["entries"] ?? new JsonArray();
        foreach (var residentId in residentIds)
        {
            var resident = entries.OfType<JsonObject>().First(entry =>
                string.Equals(entry["residentId"]?.GetValue<string>(), residentId, StringComparison.OrdinalIgnoreCase));
            resident["shiningFactionId"] = factionId;
        }
    }

    private async Task<List<ValidationIssue>> InvokeValidationAsync(string methodName)
    {
        var issues = new List<ValidationIssue>();
        var method = typeof(ValidationService).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var task = method!.Invoke(_validator, new object[] { issues }) as Task;
        Assert.NotNull(task);
        await task!;
        return issues;
    }

    private async Task WritePendingTurnSnapshotManifestAsync(Dictionary<string, string> rollbackBackups)
    {
        var manifest = new JsonObject
        {
            ["sessionId"] = "test-session",
            ["requestId"] = "test-request",
            ["turnNumber"] = 12,
            ["requestTimestamp"] = "2026-04-16T00:00:00Z",
            ["playerAction"] = "test",
            ["files"] = new JsonObject(),
            ["snapshotFileHashes"] = new JsonObject(),
            ["clientOwnedValidationHashes"] = new JsonObject(),
            ["rollbackBackups"] = new JsonObject(rollbackBackups.ToDictionary(
                pair => NormalizeRelativePath(pair.Key),
                pair => (JsonNode?)NormalizeRelativePath(pair.Value),
                StringComparer.OrdinalIgnoreCase)),
            ["rollbackBaselineFiles"] = new JsonArray(rollbackBackups.Keys
                .Select(NormalizeRelativePath)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .Select(path => (JsonNode?)path)
                .ToArray()),
            ["sourceLabel"] = "shining-political-resolution-tests",
            ["manifestPayloadHash"] = string.Empty
        };

        await WriteNodeAsync("input/turn_request.json", new JsonObject
        {
            ["sessionId"] = "test-session",
            ["requestId"] = "test-request",
            ["turnNumber"] = 12
        });

        await RegisterSnapshotFilesAsync(manifest);
        manifest["manifestPayloadHash"] = PendingTurnSnapshotTestAuthority.ComputeManifestPayloadHash(manifest);
        await WriteNodeAsync("game_state/control/pending_turn_snapshot.json", manifest);
        await PendingTurnSnapshotTestAuthority.SyncAuthorityForCurrentManifestAsync(_fs);
    }

    private async Task RegisterSnapshotFilesAsync(JsonObject manifest)
    {
        var files = manifest["files"]!.AsObject();
        var snapshotHashes = manifest["snapshotFileHashes"]!.AsObject();
        var rollbackBackups = manifest["rollbackBackups"]!.AsObject();
        foreach (var pair in rollbackBackups)
        {
            if (pair.Value is not JsonValue valueNode || !valueNode.TryGetValue<string>(out var backupPath) || string.IsNullOrWhiteSpace(backupPath))
                continue;

            var logicalPath = NormalizeRelativePath(pair.Key);
            var snapshotPath = $"game_state/control/pending_turn_snapshot/{logicalPath}";
            var snapshotJson = await _fs.ReadFileAsync(snapshotPath);
            if (string.IsNullOrWhiteSpace(snapshotJson))
            {
                snapshotJson = await _fs.ReadFileAsync(backupPath);
                if (string.IsNullOrWhiteSpace(snapshotJson))
                    continue;

                await _fs.WriteFileAtomicAsync(snapshotPath, snapshotJson);
            }

            files[logicalPath] = snapshotPath;
            snapshotHashes[logicalPath] = ComputeSha256(snapshotJson);
        }

        var snapshotRoot = _fs.ResolvePath("game_state/control/pending_turn_snapshot");
        if (!Directory.Exists(snapshotRoot))
            return;

        foreach (var snapshotFile in Directory.GetFiles(snapshotRoot, "*", SearchOption.AllDirectories))
        {
            var relativeSnapshotPath = NormalizeRelativePath(Path.GetRelativePath(snapshotRoot, snapshotFile));
            if (!relativeSnapshotPath.Contains('/'))
                continue;

            if (files.ContainsKey(relativeSnapshotPath))
                continue;

            var snapshotJson = await File.ReadAllTextAsync(snapshotFile);
            if (string.IsNullOrWhiteSpace(snapshotJson))
                continue;

            files[relativeSnapshotPath] = $"game_state/control/pending_turn_snapshot/{relativeSnapshotPath}";
            snapshotHashes[relativeSnapshotPath] = ComputeSha256(snapshotJson);
        }
    }

    private async Task WriteNodeAsync(string relativePath, JsonNode node)
    {
        await _fs.WriteFileAtomicAsync(relativePath, node.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true
        }));
    }

    private static string ComputeSha256(string content)
    {
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(content)));
    }

    private static string NormalizeRelativePath(string path) => path.Replace('\\', '/');

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_rootPath))
                Directory.Delete(_rootPath, recursive: true);
        }
        catch
        {
            // ignore cleanup issues
        }
    }
}
