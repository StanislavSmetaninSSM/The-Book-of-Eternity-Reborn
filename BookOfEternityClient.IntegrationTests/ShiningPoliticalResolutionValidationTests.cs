using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

[Trait("Category", "FullValidation")]
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
            ["quotedCostFeathers"] = ShiningFactionRequestState.FactionFoundingCostFeathers,
            ["quotedCostLightSparks"] = ShiningFactionRequestState.FactionFoundingCostLightSparks,
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
            ["quotedCostFeathers"] = ShiningFactionRequestState.FactionFoundingCostFeathers,
            ["quotedCostLightSparks"] = ShiningFactionRequestState.FactionFoundingCostLightSparks,
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
        await WriteValidatedSnapshotStateAsync(CreateBaseShiningRoot(), CreateSoulStateRoot(currentFeathers: 75));
        await WritePendingTurnSnapshotManifestAsync(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [ShiningFactionRequestState.PendingFoundingsRequestPath] = backupPath
        });

        var issues = await InvokeValidationAsync("ValidatePendingShiningFoundingResolutionAsync");

        Assert.DoesNotContain(issues, issue => string.Equals(issue.Code, "shining_founding_missing_resolution", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue => string.Equals(issue.Code, "shining_founding_missing_faction_materialization", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue => string.Equals(issue.Code, "shining_founding_supporter_not_reassigned", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue => string.Equals(issue.Code, "shining_founding_reserved_light_sparks_rollback", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue => string.Equals(issue.Code, "shining_founding_reserved_ink_feathers_rollback", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidatePendingShiningFoundingResolutionAsync_AcceptedFoundingRestoredLightSparks_Fails()
    {
        const string requestId = "founding_req_light_sparks_rollback";
        var request = CreateFoundingRequest(requestId, "faction_light_sparks_rollback", "hall_light_sparks_rollback");
        var preTurnShiningRoot = CreateBaseShiningRoot();
        var currentShiningRoot = CloneJsonObject(preTurnShiningRoot);
        currentShiningRoot["lightSparks"] = GetNodeInt(preTurnShiningRoot["lightSparks"]) + ShiningFactionRequestState.FactionFoundingCostLightSparks;
        var residentRoot = CreateBaseResidentRoot();
        AddAcceptedFoundingMaterialization(currentShiningRoot, residentRoot, request);

        await SeedCurrentStateAsync(currentShiningRoot, residentRoot, currentFeathers: 75);
        await SeedFoundingPendingSnapshotAsync(
            request,
            preTurnShiningRoot,
            CreateSoulStateRoot(currentFeathers: 75),
            "game_state/control/pending_turn_snapshot/pre_shining_founding_light_sparks_rollback.json");

        var issues = await InvokeValidationAsync("ValidatePendingShiningFoundingResolutionAsync");

        Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_founding_reserved_light_sparks_rollback", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidatePendingShiningFoundingResolutionAsync_AcceptedFoundingRestoredInkFeathers_Fails()
    {
        const string requestId = "founding_req_feather_rollback";
        var request = CreateFoundingRequest(requestId, "faction_feather_rollback", "hall_feather_rollback");
        var preTurnShiningRoot = CreateBaseShiningRoot();
        var currentShiningRoot = CloneJsonObject(preTurnShiningRoot);
        var residentRoot = CreateBaseResidentRoot();
        AddAcceptedFoundingMaterialization(currentShiningRoot, residentRoot, request);

        await SeedCurrentStateAsync(
            currentShiningRoot,
            residentRoot,
            currentFeathers: 75 + ShiningFactionRequestState.FactionFoundingCostFeathers);
        await SeedFoundingPendingSnapshotAsync(
            request,
            preTurnShiningRoot,
            CreateSoulStateRoot(currentFeathers: 75),
            "game_state/control/pending_turn_snapshot/pre_shining_founding_feather_rollback.json");

        var issues = await InvokeValidationAsync("ValidatePendingShiningFoundingResolutionAsync");

        Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_founding_reserved_ink_feathers_rollback", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidatePendingShiningFoundingResolutionAsync_CorruptedEchoedFoundingCost_Fails()
    {
        const string requestId = "founding_req_corrupted_cost";
        var request = new JsonObject
        {
            ["requestId"] = requestId,
            ["proposedFactionId"] = "faction_corrupted_cost",
            ["proposedHallId"] = "hall_corrupted_cost",
            ["proposedHallName"] = "Зал Ошибочной Цены",
            ["proposedHallDescription"] = "Зал с повреждённой стоимостью основания.",
            ["proposedHallServiceTags"] = new JsonArray("social", "lore"),
            ["charter"] = new JsonObject
            {
                ["factionName"] = "Дом Ошибочной Цены",
                ["favoredArchetype"] = ShiningAbodeState.ProjectArchetypeAccord,
                ["patronEffectFamily"] = ShiningAbodeState.EffectFamilySocial,
                ["summary"] = "Проверяет, что request не становится источником цены."
            },
            ["supportingResidentIds"] = new JsonArray("resident_liora", "resident_mael", "resident_serit"),
            ["quotedCostFeathers"] = 999,
            ["quotedCostLightSparks"] = 999,
            ["createdAtTurn"] = 184,
            ["createdAtUtc"] = "2026-04-16T15:20:00Z"
        };

        var shiningRoot = CreateBaseShiningRoot();
        ((JsonArray)shiningRoot["factionFoundingReceipts"]!).Add(new JsonObject
        {
            ["requestId"] = requestId,
            ["proposedFactionId"] = "faction_corrupted_cost",
            ["proposedHallId"] = "hall_corrupted_cost",
            ["hallName"] = "Зал Ошибочной Цены",
            ["factionId"] = "faction_corrupted_cost",
            ["hallId"] = "hall_corrupted_cost",
            ["status"] = ShiningFactionRequestState.RequestStatusAccepted,
            ["supportingResidentIds"] = new JsonArray("resident_liora", "resident_mael", "resident_serit"),
            ["quotedCostFeathers"] = 999,
            ["quotedCostLightSparks"] = 999,
            ["resolvedAtTurn"] = 184,
            ["resolvedAtUtc"] = "2026-04-16T15:24:00Z",
            ["reason"] = "founding_accepted"
        });

        await SeedCurrentStateAsync(shiningRoot, CreateBaseResidentRoot());
        await WriteNodeAsync(ShiningFactionRequestState.PendingFoundingsRequestPath, new JsonObject
        {
            [ShiningFactionRequestState.RequestsProperty] = new JsonArray(request.DeepClone())
        });
        const string backupPath = "game_state/control/pending_turn_snapshot/pre_shining_founding_corrupted_cost.json";
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
            ["quotedCostFeathers"] = ShiningFactionRequestState.FactionFoundingCostFeathers,
            ["quotedCostLightSparks"] = ShiningFactionRequestState.FactionFoundingCostLightSparks,
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
            ["quotedCostFeathers"] = ShiningFactionRequestState.FactionFoundingCostFeathers,
            ["quotedCostLightSparks"] = ShiningFactionRequestState.FactionFoundingCostLightSparks,
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
    public async Task ValidateShiningClosureCompositeDiffAsync_AcceptedFoundingWithUnrelatedMutations_Fails()
    {
        var request = CreateFoundingRequest("founding_req_unrelated_mutations", "faction_unrelated_mutations", "hall_unrelated_mutations");
        var preTurnShiningRoot = CreateBaseShiningRoot();
        var preTurnResidentRoot = CreateBaseResidentRoot();
        var currentShiningRoot = CloneJsonObject(preTurnShiningRoot);
        var currentResidentRoot = CloneJsonObject(preTurnResidentRoot);
        AddAcceptedFoundingMaterialization(currentShiningRoot, currentResidentRoot, request);

        currentShiningRoot["radiance"]!["experience"] = 999;
        var unrelatedResident = currentResidentRoot["entries"]!.AsArray()
            .OfType<JsonObject>()
            .First(entry => string.Equals(entry["residentId"]?.GetValue<string>(), "resident_outsider", StringComparison.OrdinalIgnoreCase));
        unrelatedResident["factionRestlessness"] = 99;

        await SeedCurrentStateAsync(currentShiningRoot, currentResidentRoot, currentFeathers: 80);
        const string backupPath = "game_state/control/pending_turn_snapshot/pre_shining_founding_unrelated_mutations.json";
        await WriteNodeAsync(ShiningFactionRequestState.PendingFoundingsRequestPath, new JsonObject
        {
            [ShiningFactionRequestState.RequestsProperty] = new JsonArray(request.DeepClone())
        });
        await WriteNodeAsync(backupPath, new JsonObject
        {
            [ShiningFactionRequestState.RequestsProperty] = new JsonArray(request.DeepClone())
        });
        await WriteValidatedSnapshotStateAsync(
            preTurnShiningRoot,
            CreateSoulStateRoot(currentFeathers: 75),
            preTurnResidentRoot);
        await WritePendingTurnSnapshotManifestAsync(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [ShiningFactionRequestState.PendingFoundingsRequestPath] = backupPath
        });

        var issues = await InvokeValidationAsync("ValidateShiningClosureCompositeDiffAsync");

        Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_closure_unexpected_shining_state_diff", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_closure_unexpected_resident_state_diff", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_closure_unexpected_soul_state_diff", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateShiningClosureCompositeDiffAsync_AcceptedFoundingWithVerifiedSchedulerDeltas_Passes()
    {
        var request = CreateFoundingRequest("founding_req_scheduler_deltas", "faction_scheduler_deltas", "hall_scheduler_deltas");
        var preTurnShiningRoot = CreateBaseShiningRoot();
        var preTurnResidentRoot = CreateBaseResidentRoot();
        var currentShiningRoot = CloneJsonObject(preTurnShiningRoot);
        var currentResidentRoot = CloneJsonObject(preTurnResidentRoot);
        AddAcceptedFoundingMaterialization(currentShiningRoot, currentResidentRoot, request);

        currentShiningRoot["radiance"]!["experience"] = 999;
        var schedulerResident = currentResidentRoot["entries"]!.AsArray()
            .OfType<JsonObject>()
            .First(entry => string.Equals(entry["residentId"]?.GetValue<string>(), "resident_outsider", StringComparison.OrdinalIgnoreCase));
        schedulerResident["factionRestlessness"] = 99;

        await SeedCurrentStateAsync(currentShiningRoot, currentResidentRoot, currentFeathers: 75);
        const string backupPath = "game_state/control/pending_turn_snapshot/pre_shining_founding_scheduler_deltas.json";
        await WriteNodeAsync(ShiningFactionRequestState.PendingFoundingsRequestPath, new JsonObject
        {
            [ShiningFactionRequestState.RequestsProperty] = new JsonArray(request.DeepClone())
        });
        await WriteNodeAsync(backupPath, new JsonObject
        {
            [ShiningFactionRequestState.RequestsProperty] = new JsonArray(request.DeepClone())
        });
        await WriteValidatedSnapshotStateAsync(
            preTurnShiningRoot,
            CreateSoulStateRoot(currentFeathers: 75),
            preTurnResidentRoot);
        await WritePendingTurnSnapshotManifestAsync(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [ShiningFactionRequestState.PendingFoundingsRequestPath] = backupPath
            },
            new ProgressionControl
            {
                CurrentRealm = "Shining Abode",
                ResidentAgencyCyclesExpectedThisTurn = 1,
                ShiningFactionCyclesExpectedThisTurn = 1
            });
        await WriteNodeAsync(ProgressionScheduleService.ReportPath, new JsonObject
        {
            ["progressionProcessingReport"] = new JsonObject
            {
                ["sessionId"] = "test-session",
                ["requestId"] = "test-request",
                ["turnNumber"] = 12,
                ["worldCyclesProcessed"] = 0,
                ["factionCyclesProcessed"] = 0,
                ["chaosSeaCyclesProcessed"] = 0,
                ["guardianProjectCyclesProcessed"] = 0,
                ["residentAgencyCyclesProcessed"] = 1,
                ["shiningAbodeCyclesProcessed"] = 0,
                ["shiningFactionCyclesProcessed"] = 1,
                ["shiningTradeCyclesProcessed"] = 0
            }
        });

        var issues = await InvokeValidationAsync("ValidateShiningClosureCompositeDiffAsync");

        Assert.DoesNotContain(issues, issue => string.Equals(issue.Code, "shining_closure_unexpected_shining_state_diff", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue => string.Equals(issue.Code, "shining_closure_unexpected_resident_state_diff", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue => string.Equals(issue.Code, "shining_closure_unexpected_soul_state_diff", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateShiningClosureCompositeDiffAsync_AcceptedFoundingWithVerifiedSchedulerAndCoreReceipt_Fails()
    {
        var request = CreateFoundingRequest("founding_req_scheduler_forbidden", "faction_scheduler_forbidden", "hall_scheduler_forbidden");
        var preTurnShiningRoot = CreateBaseShiningRoot();
        var preTurnResidentRoot = CreateBaseResidentRoot();
        var currentShiningRoot = CloneJsonObject(preTurnShiningRoot);
        var currentResidentRoot = CloneJsonObject(preTurnResidentRoot);
        AddAcceptedFoundingMaterialization(currentShiningRoot, currentResidentRoot, request);

        currentShiningRoot["radiance"]!["experience"] = 999;
        currentShiningRoot["lightSparks"] = 81;
        var coreReceipts = currentShiningRoot["coreActionReceipts"] as JsonArray ?? new JsonArray();
        coreReceipts.Add(new JsonObject
        {
            ["requestId"] = "core_receipt_without_core_request",
            ["actionType"] = "open_gates",
            ["status"] = "accepted",
            ["quotedCostFeathers"] = 0,
            ["quotedCostLightSparks"] = 0,
            ["resolvedAtTurn"] = 12,
            ["reason"] = "unrelated_core_action"
        });
        currentShiningRoot["coreActionReceipts"] = coreReceipts;

        await SeedCurrentStateAsync(currentShiningRoot, currentResidentRoot, currentFeathers: 75);
        const string backupPath = "game_state/control/pending_turn_snapshot/pre_shining_founding_scheduler_forbidden.json";
        await WriteNodeAsync(ShiningFactionRequestState.PendingFoundingsRequestPath, new JsonObject
        {
            [ShiningFactionRequestState.RequestsProperty] = new JsonArray(request.DeepClone())
        });
        await WriteNodeAsync(backupPath, new JsonObject
        {
            [ShiningFactionRequestState.RequestsProperty] = new JsonArray(request.DeepClone())
        });
        await WriteValidatedSnapshotStateAsync(
            preTurnShiningRoot,
            CreateSoulStateRoot(currentFeathers: 75),
            preTurnResidentRoot);
        await WritePendingTurnSnapshotManifestAsync(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [ShiningFactionRequestState.PendingFoundingsRequestPath] = backupPath
            },
            new ProgressionControl
            {
                CurrentRealm = "Shining Abode",
                ShiningFactionCyclesExpectedThisTurn = 1
            });
        await WriteNodeAsync(ProgressionScheduleService.ReportPath, new JsonObject
        {
            ["progressionProcessingReport"] = new JsonObject
            {
                ["sessionId"] = "test-session",
                ["requestId"] = "test-request",
                ["turnNumber"] = 12,
                ["worldCyclesProcessed"] = 0,
                ["factionCyclesProcessed"] = 0,
                ["chaosSeaCyclesProcessed"] = 0,
                ["guardianProjectCyclesProcessed"] = 0,
                ["residentAgencyCyclesProcessed"] = 0,
                ["shiningAbodeCyclesProcessed"] = 0,
                ["shiningFactionCyclesProcessed"] = 1,
                ["shiningTradeCyclesProcessed"] = 0
            }
        });

        var issues = await InvokeValidationAsync("ValidateShiningClosureCompositeDiffAsync");

        Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_closure_unexpected_shining_state_diff", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateShiningClosureCompositeDiffAsync_AcceptedFoundingWithVerifiedSchedulerAndAvailabilityChange_Fails()
    {
        var request = CreateFoundingRequest("founding_req_scheduler_availability", "faction_scheduler_availability", "hall_scheduler_availability");
        var preTurnShiningRoot = CreateBaseShiningRoot();
        var preTurnResidentRoot = CreateBaseResidentRoot();
        var currentShiningRoot = CloneJsonObject(preTurnShiningRoot);
        var currentResidentRoot = CloneJsonObject(preTurnResidentRoot);
        AddAcceptedFoundingMaterialization(currentShiningRoot, currentResidentRoot, request);

        currentShiningRoot["radiance"]!["experience"] = 999;
        currentShiningRoot["availability"] = "sealed_until_next_ascension";

        await SeedCurrentStateAsync(currentShiningRoot, currentResidentRoot, currentFeathers: 75);
        const string backupPath = "game_state/control/pending_turn_snapshot/pre_shining_founding_scheduler_availability.json";
        await WriteNodeAsync(ShiningFactionRequestState.PendingFoundingsRequestPath, new JsonObject
        {
            [ShiningFactionRequestState.RequestsProperty] = new JsonArray(request.DeepClone())
        });
        await WriteNodeAsync(backupPath, new JsonObject
        {
            [ShiningFactionRequestState.RequestsProperty] = new JsonArray(request.DeepClone())
        });
        await WriteValidatedSnapshotStateAsync(
            preTurnShiningRoot,
            CreateSoulStateRoot(currentFeathers: 75),
            preTurnResidentRoot);
        await WritePendingTurnSnapshotManifestAsync(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [ShiningFactionRequestState.PendingFoundingsRequestPath] = backupPath
            },
            new ProgressionControl
            {
                CurrentRealm = "Shining Abode",
                ShiningFactionCyclesExpectedThisTurn = 1
            });
        await WriteNodeAsync(ProgressionScheduleService.ReportPath, new JsonObject
        {
            ["progressionProcessingReport"] = new JsonObject
            {
                ["sessionId"] = "test-session",
                ["requestId"] = "test-request",
                ["turnNumber"] = 12,
                ["worldCyclesProcessed"] = 0,
                ["factionCyclesProcessed"] = 0,
                ["chaosSeaCyclesProcessed"] = 0,
                ["guardianProjectCyclesProcessed"] = 0,
                ["residentAgencyCyclesProcessed"] = 0,
                ["shiningAbodeCyclesProcessed"] = 0,
                ["shiningFactionCyclesProcessed"] = 1,
                ["shiningTradeCyclesProcessed"] = 0
            }
        });

        var issues = await InvokeValidationAsync("ValidateShiningClosureCompositeDiffAsync");

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "shining_closure_unexpected_shining_state_diff", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.Contains("availability", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateShiningClosureCompositeDiffAsync_AcceptedFoundingWithConcurrentAbodeOfferingSoulDelta_Passes()
    {
        var request = CreateFoundingRequest("founding_req_with_offering", "faction_with_offering", "hall_with_offering");
        var preTurnShiningRoot = CreateBaseShiningRoot();
        var preTurnResidentRoot = CreateBaseResidentRoot();
        var currentShiningRoot = CloneJsonObject(preTurnShiningRoot);
        var currentResidentRoot = CloneJsonObject(preTurnResidentRoot);
        AddAcceptedFoundingMaterialization(currentShiningRoot, currentResidentRoot, request);
        var offeringRequest = new JsonObject
        {
            ["guardianId"] = "guardian_old",
            ["guardianName"] = "Азалия",
            ["offeringType"] = GuardianAbodeOfferingState.OfferingTypeInkFeathers,
            ["inkFeathersOffered"] = 50,
            ["returnCycleId"] = "return_2",
            ["createdAtUtc"] = "2026-04-16T15:21:00Z"
        };

        await SeedCurrentStateAsync(currentShiningRoot, currentResidentRoot, currentFeathers: 25);
        await WriteNodeAsync(ShiningFactionRequestState.PendingFoundingsRequestPath, new JsonObject
        {
            [ShiningFactionRequestState.RequestsProperty] = new JsonArray(request.DeepClone())
        });
        await WriteNodeAsync(GuardianAbodeOfferingState.PendingRequestPath, offeringRequest.DeepClone());
        const string foundingBackupPath = "game_state/control/pending_turn_snapshot/pre_shining_founding_with_offering.json";
        const string offeringBackupPath = "game_state/control/pending_turn_snapshot/pre_abode_offering_with_founding.json";
        await WriteNodeAsync(foundingBackupPath, new JsonObject
        {
            [ShiningFactionRequestState.RequestsProperty] = new JsonArray(request.DeepClone())
        });
        await WriteNodeAsync(offeringBackupPath, offeringRequest.DeepClone());
        await WriteValidatedSnapshotStateAsync(
            preTurnShiningRoot,
            CreateSoulStateRoot(currentFeathers: 75),
            preTurnResidentRoot);
        await WritePendingTurnSnapshotManifestAsync(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [ShiningFactionRequestState.PendingFoundingsRequestPath] = foundingBackupPath,
            [GuardianAbodeOfferingState.PendingRequestPath] = offeringBackupPath
        });

        var issues = await InvokeValidationAsync("ValidateShiningClosureCompositeDiffAsync");

        Assert.DoesNotContain(issues, issue => string.Equals(issue.Code, "shining_closure_unexpected_shining_state_diff", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue => string.Equals(issue.Code, "shining_closure_unexpected_resident_state_diff", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue => string.Equals(issue.Code, "shining_closure_unexpected_soul_state_diff", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(AfterlifeArchiveActionState.RequestedModeConsultation)]
    [InlineData(AfterlifeArchiveActionState.RequestedModeProjectFuel)]
    public async Task ValidateShiningClosureCompositeDiffAsync_AcceptedFoundingWithConcurrentArchiveSoulDelta_Passes(string requestedMode)
    {
        var request = CreateFoundingRequest($"founding_req_with_{requestedMode}", $"faction_with_{requestedMode}", $"hall_with_{requestedMode}");
        var archiveRequestId = $"archive_req_{requestedMode}";
        var archiveId = $"archive_entry_{requestedMode}";
        var preTurnShiningRoot = CreateBaseShiningRoot();
        var preTurnResidentRoot = CreateBaseResidentRoot();
        var currentShiningRoot = CloneJsonObject(preTurnShiningRoot);
        var currentResidentRoot = CloneJsonObject(preTurnResidentRoot);
        AddAcceptedFoundingMaterialization(currentShiningRoot, currentResidentRoot, request);
        var preTurnSoulRoot = CreateSoulStateRootWithReservedArchive(
            75,
            archiveRequestId,
            archiveId,
            requestedMode);
        var currentSoulRoot = CloneJsonObject(preTurnSoulRoot);
        var archiveResolution = CreateAcceptedArchiveResolution(archiveRequestId, archiveId, requestedMode);
        AfterlifeArchiveState.ApplyActionResolutions(currentSoulRoot, new JsonArray(CloneJsonObject(archiveResolution)), 12);
        var archiveRequest = CreateArchiveRequest(archiveRequestId, archiveId, requestedMode);
        var archiveRequestPath = string.Equals(requestedMode, AfterlifeArchiveActionState.RequestedModeConsultation, StringComparison.OrdinalIgnoreCase)
            ? AfterlifeArchiveActionState.ConsultationRequestPath
            : AfterlifeArchiveActionState.ProjectFuelRequestPath;
        var archiveBackupPath = $"game_state/control/pending_turn_snapshot/pre_{requestedMode}_with_founding.json";

        await SeedCurrentStateAsync(currentShiningRoot, currentResidentRoot);
        await WriteNodeAsync("game_state/meta/soul_state.json", currentSoulRoot);
        await WriteNodeAsync(ShiningFactionRequestState.PendingFoundingsRequestPath, new JsonObject
        {
            [ShiningFactionRequestState.RequestsProperty] = new JsonArray(request.DeepClone())
        });
        await WriteNodeAsync(archiveRequestPath, archiveRequest.DeepClone());
        const string foundingBackupPath = "game_state/control/pending_turn_snapshot/pre_shining_founding_with_archive.json";
        await WriteNodeAsync(foundingBackupPath, new JsonObject
        {
            [ShiningFactionRequestState.RequestsProperty] = new JsonArray(request.DeepClone())
        });
        await WriteNodeAsync(archiveBackupPath, archiveRequest.DeepClone());
        await WriteValidatedSnapshotStateAsync(
            preTurnShiningRoot,
            preTurnSoulRoot,
            preTurnResidentRoot);
        await WritePendingTurnSnapshotManifestAsync(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [ShiningFactionRequestState.PendingFoundingsRequestPath] = foundingBackupPath,
            [archiveRequestPath] = archiveBackupPath
        });

        var issues = await InvokeValidationAsync("ValidateShiningClosureCompositeDiffAsync");

        Assert.DoesNotContain(issues, issue => string.Equals(issue.Code, "shining_closure_unexpected_shining_state_diff", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue => string.Equals(issue.Code, "shining_closure_unexpected_resident_state_diff", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue => string.Equals(issue.Code, "shining_closure_unexpected_soul_state_diff", StringComparison.OrdinalIgnoreCase));
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

        var issues = await _validator.ValidateGameStateAsync(
            IntegrationValidationProfiles.ShiningState);

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

        var issues = await _validator.ValidateGameStateAsync(
            IntegrationValidationProfiles.ShiningState);

        Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_realignment_duplicate_request_id", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameState_PendingShiningLeadershipTransitionsWithDuplicateRequestId_Fails()
    {
        var shiningRoot = CreateBaseShiningRoot();
        var residentRoot = CreateBaseResidentRoot();
        await SeedCurrentStateAsync(shiningRoot, residentRoot);
        await WriteNodeAsync(ShiningFactionRequestState.PendingLeadershipTransitionsRequestPath, new JsonObject
        {
            [ShiningFactionRequestState.RequestsProperty] = new JsonArray
            {
                new JsonObject
                {
                    ["requestId"] = "leadership_req_shared",
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
                },
                new JsonObject
                {
                    ["requestId"] = "leadership_req_shared",
                    ["factionId"] = "faction_new",
                    ["factionName"] = "Новый Дом",
                    ["transitionMode"] = ShiningFactionRequestState.TransitionModeRevolt,
                    ["incumbentHeadActorType"] = ShiningAbodeState.HeadActorTypeRadiantActor,
                    ["incumbentHeadActorId"] = "radiant_actor_new_head",
                    ["candidateHeadActorType"] = ShiningAbodeState.HeadActorTypePlayerSoul,
                    ["candidateHeadActorId"] = ShiningAbodeState.HeadActorTypePlayerSoul,
                    ["supportingResidentIds"] = new JsonArray("resident_outsider"),
                    ["createdAtTurn"] = 204,
                    ["createdAtUtc"] = "2026-04-16T16:41:00Z"
                }
            }
        });

        var issues = await _validator.ValidateGameStateAsync(
            IntegrationValidationProfiles.ShiningState);

        Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_leadership_duplicate_request_id", StringComparison.OrdinalIgnoreCase));
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

        var issues = await _validator.ValidateGameStateAsync(
            IntegrationValidationProfiles.ShiningState);

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

    private async Task SeedCurrentStateAsync(JsonObject shiningRoot, JsonObject residentRoot, int currentFeathers = 75)
    {
        await WriteNodeAsync("game_state/meta/soul_state.json", CreateSoulStateRoot(currentFeathers));
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

    private static JsonObject CreateSoulStateRoot(int currentFeathers) => new()
    {
        ["currentRealm"] = "Shining Abode",
        ["currentIncarnation"] = 2,
        ["soulName"] = "Тестовая душа",
        ["inkFeathers"] = new JsonObject
        {
            ["current"] = currentFeathers,
            ["total"] = Math.Max(currentFeathers, 100)
        }
    };

    private static JsonObject CreateSoulStateRootWithReservedArchive(
        int currentFeathers,
        string requestId,
        string archiveId,
        string requestedMode)
    {
        var root = CreateSoulStateRoot(currentFeathers);
        var stored = AfterlifeArchiveState.EnsureStoredArray(root);
        stored.Add(new JsonObject
        {
            ["archiveId"] = archiveId,
            ["title"] = "Запись для совместного закрытия",
            ["entryType"] = "lore_fragment",
            ["rarity"] = "rare",
            ["sourceKind"] = "test"
        });
        AfterlifeArchiveState.TryReserveEntry(
            stored,
            archiveId,
            requestedMode,
            requestId,
            "guardian_old",
            "Азалия",
            12,
            targetProjectId: "project_archive_focus",
            targetProjectName: "Архивный фокус");
        return root;
    }

    private static JsonObject CreateArchiveRequest(string requestId, string archiveId, string requestedMode)
    {
        var request = new JsonObject
        {
            ["requestId"] = requestId,
            ["guardianId"] = "guardian_old",
            ["guardianName"] = "Азалия",
            ["archiveId"] = archiveId,
            ["archiveTitle"] = "Запись для совместного закрытия",
            ["archiveEntryType"] = "lore_fragment",
            ["archiveRarity"] = "rare",
            ["archiveSourceKind"] = "test",
            ["createdAtTurn"] = 12,
            ["createdAtUtc"] = "2026-04-16T15:21:00Z",
            ["requestedMode"] = requestedMode
        };

        if (string.Equals(requestedMode, AfterlifeArchiveActionState.RequestedModeConsultation, StringComparison.OrdinalIgnoreCase))
        {
            request["targetIncarnation"] = 2;
        }
        else
        {
            request["targetProjectId"] = "project_archive_focus";
            request["targetProjectName"] = "Архивный фокус";
        }

        return request;
    }

    private static JsonObject CreateAcceptedArchiveResolution(string requestId, string archiveId, string requestedMode)
    {
        var resolution = new JsonObject
        {
            ["requestId"] = requestId,
            ["archiveId"] = archiveId,
            ["requestedMode"] = requestedMode,
            ["status"] = AfterlifeArchiveActionState.ResolutionStatusAccepted,
            ["guardianId"] = "guardian_old",
            ["guardianName"] = "Азалия",
            ["reason"] = "accepted_concurrent_archive_contract",
            ["resolvedAtTurn"] = 12,
            ["resolvedAtUtc"] = "2026-04-16T15:24:00Z"
        };

        if (string.Equals(requestedMode, AfterlifeArchiveActionState.RequestedModeConsultation, StringComparison.OrdinalIgnoreCase))
        {
            resolution[AfterlifeArchiveActionState.ConsultationOutcomeGuaranteedArchiveQuestCount] = 1;
            resolution[AfterlifeArchiveActionState.ConsultationOutcomeQuestHookCount] = 0;
            resolution[AfterlifeArchiveActionState.ConsultationOutcomeSpecialQuestLineUnlocks] = 0;
            resolution[AfterlifeArchiveActionState.ConsultationOutcomeVisibleRivalClueBonus] = 0;
            resolution[AfterlifeArchiveActionState.ConsultationOutcomeArchiveWarningTierBonus] = 0;
        }
        else
        {
            resolution["targetProjectId"] = "project_archive_focus";
            resolution["resultMode"] = AfterlifeArchiveActionState.ProjectFuelResultModePressureRelief;
            resolution["resultAmount"] = 1;
        }

        return resolution;
    }

    private static JsonObject CreateFoundingRequest(string requestId, string proposedFactionId, string proposedHallId) => new()
    {
        ["requestId"] = requestId,
        ["proposedFactionId"] = proposedFactionId,
        ["proposedHallId"] = proposedHallId,
        ["proposedHallName"] = "Зал Проверки Резервов",
        ["proposedHallDescription"] = "Зал для проверки, что GM не откатывает локально зарезервированные ресурсы.",
        ["proposedHallServiceTags"] = new JsonArray("social", "lore"),
        ["charter"] = new JsonObject
        {
            ["factionName"] = "Дом Проверки Резервов",
            ["favoredArchetype"] = ShiningAbodeState.ProjectArchetypeAccord,
            ["patronEffectFamily"] = ShiningAbodeState.EffectFamilySocial,
            ["summary"] = "Проверяет защиту founding resource reservation."
        },
        ["supportingResidentIds"] = new JsonArray("resident_liora", "resident_mael", "resident_serit"),
        ["quotedCostFeathers"] = ShiningFactionRequestState.FactionFoundingCostFeathers,
        ["quotedCostLightSparks"] = ShiningFactionRequestState.FactionFoundingCostLightSparks,
        ["createdAtTurn"] = 184,
        ["createdAtUtc"] = "2026-04-16T15:20:00Z"
    };

    private static void AddAcceptedFoundingMaterialization(JsonObject shiningRoot, JsonObject residentRoot, JsonObject request)
    {
        var factionId = request["proposedFactionId"]?.GetValue<string>() ?? string.Empty;
        var hallId = request["proposedHallId"]?.GetValue<string>() ?? string.Empty;
        var hallName = request["proposedHallName"]?.GetValue<string>() ?? string.Empty;
        var hallDescription = request["proposedHallDescription"]?.GetValue<string>() ?? string.Empty;
        var charter = request["charter"]?.AsObject() ?? new JsonObject();
        var supporters = request["supportingResidentIds"]?.AsArray()
            .OfType<JsonValue>()
            .Select(value => value.GetValue<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray() ?? Array.Empty<string>();

        ((JsonArray)shiningRoot["halls"]!).Add(new JsonObject
        {
            ["hallId"] = hallId,
            ["hallName"] = hallName,
            ["description"] = hallDescription,
            ["serviceTags"] = new JsonArray("social", "lore")
        });
        ((JsonArray)shiningRoot["factions"]!).Add(new JsonObject
        {
            ["factionId"] = factionId,
            ["originType"] = ShiningAbodeState.OriginTypePlayerFounded,
            ["hallId"] = hallId,
            ["charter"] = CloneJsonObject(charter),
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
            ["requestId"] = request["requestId"]?.GetValue<string>() ?? string.Empty,
            ["proposedFactionId"] = factionId,
            ["proposedHallId"] = hallId,
            ["hallName"] = hallName,
            ["factionId"] = factionId,
            ["hallId"] = hallId,
            ["status"] = ShiningFactionRequestState.RequestStatusAccepted,
            ["supportingResidentIds"] = new JsonArray(supporters.Select(value => (JsonNode?)value).ToArray()),
            ["quotedCostFeathers"] = ShiningFactionRequestState.FactionFoundingCostFeathers,
            ["quotedCostLightSparks"] = ShiningFactionRequestState.FactionFoundingCostLightSparks,
            ["resolvedAtTurn"] = 184,
            ["resolvedAtUtc"] = "2026-04-16T15:24:00Z",
            ["reason"] = "founding_accepted"
        });
        MoveResidentsToFaction(residentRoot, factionId, supporters);
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

    private static JsonObject CloneJsonObject(JsonObject source) =>
        JsonNode.Parse(source.ToJsonString())!.AsObject();

    private static int GetNodeInt(JsonNode? node)
    {
        if (node is JsonValue value)
        {
            if (value.TryGetValue<int>(out var intValue))
                return intValue;
            if (value.TryGetValue<long>(out var longValue))
                return (int)longValue;
            if (value.TryGetValue<string>(out var stringValue) && int.TryParse(stringValue, out var parsed))
                return parsed;
        }

        return 0;
    }

    private async Task SeedFoundingPendingSnapshotAsync(
        JsonObject request,
        JsonObject preTurnShiningRoot,
        JsonObject preTurnSoulRoot,
        string backupPath)
    {
        await WriteNodeAsync(ShiningFactionRequestState.PendingFoundingsRequestPath, new JsonObject
        {
            [ShiningFactionRequestState.RequestsProperty] = new JsonArray(request.DeepClone())
        });
        await WriteNodeAsync(backupPath, new JsonObject
        {
            [ShiningFactionRequestState.RequestsProperty] = new JsonArray(request.DeepClone())
        });
        await WriteValidatedSnapshotStateAsync(preTurnShiningRoot, preTurnSoulRoot);
        await WritePendingTurnSnapshotManifestAsync(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [ShiningFactionRequestState.PendingFoundingsRequestPath] = backupPath
        });
    }

    private async Task WriteValidatedSnapshotStateAsync(
        JsonObject preTurnShiningRoot,
        JsonObject preTurnSoulRoot,
        JsonObject? preTurnResidentRoot = null)
    {
        await WriteNodeAsync($"game_state/control/pending_turn_snapshot/{ShiningAbodeState.StatePath}", preTurnShiningRoot);
        await WriteNodeAsync($"game_state/control/pending_turn_snapshot/{GuardianAbodeResidentState.StatePath}", preTurnResidentRoot ?? CreateBaseResidentRoot());
        await WriteNodeAsync("game_state/control/pending_turn_snapshot/game_state/meta/soul_state.json", preTurnSoulRoot);
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

    private async Task WritePendingTurnSnapshotManifestAsync(
        Dictionary<string, string> rollbackBackups,
        ProgressionControl? progressionControl = null)
    {
        var manifest = new JsonObject
        {
            ["sessionId"] = "test-session",
            ["requestId"] = "test-request",
            ["turnNumber"] = 12,
            ["requestTimestamp"] = "2026-04-16T00:00:00Z",
            ["playerAction"] = "test"
        };
        if (progressionControl != null)
            manifest["progressionControl"] = JsonSerializer.SerializeToNode(progressionControl);
        manifest["files"] = new JsonObject();
        manifest["snapshotFileHashes"] = new JsonObject();
        manifest["clientOwnedValidationHashes"] = new JsonObject();
        manifest["rollbackBackups"] = new JsonObject(rollbackBackups.ToDictionary(
                pair => NormalizeRelativePath(pair.Key),
                pair => (JsonNode?)NormalizeRelativePath(pair.Value),
                StringComparer.OrdinalIgnoreCase));
        manifest["rollbackBaselineFiles"] = new JsonArray(rollbackBackups.Keys
            .Select(NormalizeRelativePath)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(path => (JsonNode?)path)
            .ToArray());
        manifest["sourceLabel"] = "shining-political-resolution-tests";
        manifest["manifestPayloadHash"] = string.Empty;

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
