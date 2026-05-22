using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class SourceOfLightCapstoneValidationTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly ValidationService _validator;

    public SourceOfLightCapstoneValidationTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-source-of-light-validation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);

        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
        _validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
    }

    [Fact]
    public async Task ValidateGameStateAsync_SourceOfLightClosureWithPassiveAndRelic_DoesNotReportSourceIssues()
    {
        var request = SourceOfLightCapstoneState.CreateRequest(42, 580, 4);
        var preTurnSoul = CreatePreTurnSoulRoot();
        var preTurnShining = CreatePreTurnShiningRoot();
        var currentSoul = preTurnSoul.DeepClone().AsObject();
        var currentShining = preTurnShining.DeepClone().AsObject();
        ApplySourceOfLightRewards(currentSoul, currentShining, request);

        await WriteCurrentStateAsync(currentSoul, currentShining, request);
        await WriteValidatedSnapshotManifestAsync(request, preTurnSoul, preTurnShining);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, IsSourceOfLightIssue);
    }

    [Fact]
    public async Task ShiningAbodeJourney_SourceOfLightClosureWithProgression_CleansPendingAndLeavesTrustedTuple()
    {
        var request = SourceOfLightCapstoneState.CreateRequest(42, 580, 4);
        var preTurnSoul = CreatePreTurnSoulRoot();
        var preTurnShining = CreatePreTurnShiningRoot();
        var currentSoul = preTurnSoul.DeepClone().AsObject();
        var currentShining = preTurnShining.DeepClone().AsObject();
        ApplySourceOfLightRewards(currentSoul, currentShining, request);
        currentShining["radiance"]!["experience"] = SourceOfLightCapstoneState.RequiredRadianceExperience + 1;

        await WriteCurrentStateAsync(currentSoul, currentShining, request);
        await WriteValidatedSnapshotManifestAsync(
            request,
            preTurnSoul,
            preTurnShining,
            new ProgressionControl
            {
                CurrentRealm = "Shining Abode",
                ShiningAbodeCyclesExpectedThisTurn = 1
            });
        await WriteVerifiedProgressionReportAsync(shiningAbodeCyclesProcessed: 1);

        var issuesBeforeCleanup = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issuesBeforeCleanup, IsSourceOfLightIssue);
        Assert.True(_fs.FileExists(SourceOfLightCapstoneState.PendingRequestPath));

        await SourceOfLightCapstoneState.EnsureHealthyAsync(_fs, "Shining Abode");

        Assert.False(_fs.FileExists(SourceOfLightCapstoneState.PendingRequestPath));

        var persistedSoul = JsonNode.Parse((await _fs.ReadFileAsync("game_state/meta/soul_state.json"))!)!.AsObject();
        var persistedShining = JsonNode.Parse((await _fs.ReadFileAsync(ShiningAbodeState.StatePath))!)!.AsObject();
        Assert.True(SourceOfLightCapstoneState.HasMatchingClosure(persistedShining, persistedSoul, request));
        Assert.Equal(request.CreatedAtTurn, SourceOfLightCapstoneState.GetLightIncarnateGrantTurn(persistedSoul, persistedShining));
        Assert.Equal(1, SourceOfLightCapstoneState.CountIncarnatedLightRelics(persistedSoul));

        _fs.DeleteFile("input/turn_request.json");
        _fs.DeleteFile("ready/turn_complete.json");
        _fs.DeleteFile("game_state/control/pending_turn_snapshot.json");
        _fs.DeleteFile(ProgressionScheduleService.ReportPath);
        var snapshotDirectory = _fs.ResolvePath("game_state/control/pending_turn_snapshot");
        if (Directory.Exists(snapshotDirectory))
            Directory.Delete(snapshotDirectory, recursive: true);

        var issuesAfterCleanup = await _validator.ValidateGameStateAsync();
        Assert.DoesNotContain(issuesAfterCleanup, IsSourceOfLightIssue);
    }

    [Fact]
    public async Task ValidateGameStateAsync_SourceOfLightClosureWithoutRelic_ReportsMissingRelic()
    {
        var request = SourceOfLightCapstoneState.CreateRequest(42, 580, 4);
        var preTurnSoul = CreatePreTurnSoulRoot();
        var preTurnShining = CreatePreTurnShiningRoot();
        var currentSoul = preTurnSoul.DeepClone().AsObject();
        var currentShining = preTurnShining.DeepClone().AsObject();
        ApplySourceOfLightRewards(currentSoul, currentShining, request);
        currentSoul["soulRelics"]!["stored"] = new JsonArray();

        await WriteCurrentStateAsync(currentSoul, currentShining, request);
        await WriteValidatedSnapshotManifestAsync(request, preTurnSoul, preTurnShining);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "source_of_light_missing_incarnated_light_relic", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_SourceOfLightTurnErrorWithPending_DoesNotRequireRewardTuple()
    {
        var request = SourceOfLightCapstoneState.CreateRequest(42, 580, 4);
        var preTurnSoul = CreatePreTurnSoulRoot();
        var preTurnShining = CreatePreTurnShiningRoot();
        var currentSoul = preTurnSoul.DeepClone().AsObject();
        var currentShining = preTurnShining.DeepClone().AsObject();

        await WriteCurrentStateAsync(currentSoul, currentShining, request);
        await WriteValidatedSnapshotManifestAsync(request, preTurnSoul, preTurnShining);
        _fs.DeleteFile("ready/turn_complete.json");
        await _fs.WriteFileAtomicAsync("ready/turn_error.json", """
        {
          "sessionId": "session_source_of_light_tests",
          "requestId": "request_source_of_light_tests",
          "turnNumber": 42,
          "error": "simulated Source of Light GM failure"
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, IsSourceOfLightIssue);
    }

    [Fact]
    public async Task ValidateGameStateAsync_SourceOfLightClosureWithMutatedMarkerRadiance_ReportsMissingCompletedMarker()
    {
        var request = SourceOfLightCapstoneState.CreateRequest(42, 580, 4);
        var preTurnSoul = CreatePreTurnSoulRoot();
        var preTurnShining = CreatePreTurnShiningRoot();
        var currentSoul = preTurnSoul.DeepClone().AsObject();
        var currentShining = preTurnShining.DeepClone().AsObject();
        ApplySourceOfLightRewards(currentSoul, currentShining, request);
        currentShining[SourceOfLightCapstoneState.ShiningStateProperty]!.AsObject()["radianceTierAtRequest"] = 3;
        currentShining[SourceOfLightCapstoneState.ShiningStateProperty]!.AsObject()["radianceExperienceAtRequest"] = 400;

        await WriteCurrentStateAsync(currentSoul, currentShining, request);
        await WriteValidatedSnapshotManifestAsync(request, preTurnSoul, preTurnShining);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "source_of_light_missing_completed_marker", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_SourceOfLightClosureWithoutCompletedAtTurn_ReportsMissingCompletedMarker()
    {
        var request = SourceOfLightCapstoneState.CreateRequest(42, 580, 4);
        var preTurnSoul = CreatePreTurnSoulRoot();
        var preTurnShining = CreatePreTurnShiningRoot();
        var currentSoul = preTurnSoul.DeepClone().AsObject();
        var currentShining = preTurnShining.DeepClone().AsObject();
        ApplySourceOfLightRewards(currentSoul, currentShining, request);
        currentShining[SourceOfLightCapstoneState.ShiningStateProperty]!.AsObject().Remove("completedAtTurn");

        await WriteCurrentStateAsync(currentSoul, currentShining, request);
        await WriteValidatedSnapshotManifestAsync(request, preTurnSoul, preTurnShining);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "source_of_light_missing_completed_marker", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(issue.Code, "source_of_light_marker_completed_turn_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("rewardPassiveId")]
    [InlineData("rewardRelicId")]
    [InlineData("radianceExperienceAtRequest")]
    [InlineData("radianceTierAtRequest")]
    public async Task ValidateGameStateAsync_SourceOfLightClosureWithMissingRequiredMarkerField_ReportsMissingCompletedMarker(string fieldName)
    {
        var request = SourceOfLightCapstoneState.CreateRequest(42, 580, 4);
        var preTurnSoul = CreatePreTurnSoulRoot();
        var preTurnShining = CreatePreTurnShiningRoot();
        var currentSoul = preTurnSoul.DeepClone().AsObject();
        var currentShining = preTurnShining.DeepClone().AsObject();
        ApplySourceOfLightRewards(currentSoul, currentShining, request);
        currentShining[SourceOfLightCapstoneState.ShiningStateProperty]!.AsObject().Remove(fieldName);

        await WriteCurrentStateAsync(currentSoul, currentShining, request);
        await WriteValidatedSnapshotManifestAsync(request, preTurnSoul, preTurnShining);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "source_of_light_missing_completed_marker", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_SourceOfLightClosureWithUnrelatedSoulMutation_ReportsUnexpectedSoulDiff()
    {
        var request = SourceOfLightCapstoneState.CreateRequest(42, 580, 4);
        var preTurnSoul = CreatePreTurnSoulRoot();
        var preTurnShining = CreatePreTurnShiningRoot();
        var currentSoul = preTurnSoul.DeepClone().AsObject();
        var currentShining = preTurnShining.DeepClone().AsObject();
        ApplySourceOfLightRewards(currentSoul, currentShining, request);
        currentSoul["inkFeathers"] = new JsonObject
        {
            ["current"] = 999,
            ["total"] = 999
        };

        await WriteCurrentStateAsync(currentSoul, currentShining, request);
        await WriteValidatedSnapshotManifestAsync(request, preTurnSoul, preTurnShining);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "source_of_light_unexpected_soul_state_diff", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_SourceOfLightClosureWithUnrelatedShiningMutation_ReportsUnexpectedShiningDiff()
    {
        var request = SourceOfLightCapstoneState.CreateRequest(42, 580, 4);
        var preTurnSoul = CreatePreTurnSoulRoot();
        var preTurnShining = CreatePreTurnShiningRoot();
        var currentSoul = preTurnSoul.DeepClone().AsObject();
        var currentShining = preTurnShining.DeepClone().AsObject();
        ApplySourceOfLightRewards(currentSoul, currentShining, request);
        currentShining["sourceClosureIntrusion"] = "unexpected";

        await WriteCurrentStateAsync(currentSoul, currentShining, request);
        await WriteValidatedSnapshotManifestAsync(request, preTurnSoul, preTurnShining);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "source_of_light_unexpected_shining_state_diff", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_SourceOfLightClosureWithVerifiedShiningProgression_AllowsShiningDiff()
    {
        var request = SourceOfLightCapstoneState.CreateRequest(42, 580, 4);
        var preTurnSoul = CreatePreTurnSoulRoot();
        var preTurnShining = CreatePreTurnShiningRoot();
        var currentSoul = preTurnSoul.DeepClone().AsObject();
        var currentShining = preTurnShining.DeepClone().AsObject();
        ApplySourceOfLightRewards(currentSoul, currentShining, request);
        currentShining["radiance"]!["experience"] = SourceOfLightCapstoneState.RequiredRadianceExperience + 1;

        await WriteCurrentStateAsync(currentSoul, currentShining, request);
        await WriteValidatedSnapshotManifestAsync(
            request,
            preTurnSoul,
            preTurnShining,
            new ProgressionControl
            {
                CurrentRealm = "Shining Abode",
                ShiningAbodeCyclesExpectedThisTurn = 1
            });
        await WriteVerifiedProgressionReportAsync(shiningAbodeCyclesProcessed: 1);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "source_of_light_pending_radiance_snapshot_mismatch", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "source_of_light_unexpected_shining_state_diff", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "source_of_light_unexpected_soul_state_diff", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_SourceOfLightClosureWithVerifiedProgressionAndCoreReceipt_ReportsUnexpectedShiningDiff()
    {
        var request = SourceOfLightCapstoneState.CreateRequest(42, 580, 4);
        var preTurnSoul = CreatePreTurnSoulRoot();
        var preTurnShining = CreatePreTurnShiningRoot();
        var currentSoul = preTurnSoul.DeepClone().AsObject();
        var currentShining = preTurnShining.DeepClone().AsObject();
        ApplySourceOfLightRewards(currentSoul, currentShining, request);
        currentShining["radiance"]!["experience"] = SourceOfLightCapstoneState.RequiredRadianceExperience + 1;
        (currentShining["coreActionReceipts"] as JsonArray)!.Add(new JsonObject
        {
            ["requestId"] = "illegal_source_closure_core_receipt",
            ["actionType"] = "open_gates",
            ["status"] = "accepted",
            ["resolvedAtTurn"] = 42
        });

        await WriteCurrentStateAsync(currentSoul, currentShining, request);
        await WriteValidatedSnapshotManifestAsync(
            request,
            preTurnSoul,
            preTurnShining,
            new ProgressionControl
            {
                CurrentRealm = "Shining Abode",
                ShiningAbodeCyclesExpectedThisTurn = 1
            });
        await WriteVerifiedProgressionReportAsync(shiningAbodeCyclesProcessed: 1);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "source_of_light_unexpected_shining_state_diff", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.Contains("coreActionReceipts", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_SourceOfLightClosureWithRelicInEquipped_ReportsStoredPlacementIssue()
    {
        var request = SourceOfLightCapstoneState.CreateRequest(42, 580, 4);
        var preTurnSoul = CreatePreTurnSoulRoot();
        var preTurnShining = CreatePreTurnShiningRoot();
        var currentSoul = preTurnSoul.DeepClone().AsObject();
        var currentShining = preTurnShining.DeepClone().AsObject();
        ApplySourceOfLightRewards(currentSoul, currentShining, request);
        currentSoul["soulRelics"]!["stored"] = new JsonArray();
        currentSoul["soulRelics"]!["equipped"] = new JsonArray(SourceOfLightCapstoneState.CreateIncarnatedLightRelic(request));

        await WriteCurrentStateAsync(currentSoul, currentShining, request);
        await WriteValidatedSnapshotManifestAsync(request, preTurnSoul, preTurnShining);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "source_of_light_relic_not_stored_on_closure", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_SourceOfLightPendingWithOtherAfterlifePendingContract_IsRejected()
    {
        var request = SourceOfLightCapstoneState.CreateRequest(42, 580, 4);
        await WriteCurrentStateAsync(CreatePreTurnSoulRoot(), CreatePreTurnShiningRoot(), request);
        await _fs.WriteFileAtomicAsync(GuardianAbodeOfferingState.PendingRequestPath, """
        {
          "requestId": "blocking_offering"
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "source_of_light_pending_blocked_by_other_contract", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_SourceOfLightPendingWithActiveSpiritualConflict_IsRejected()
    {
        var request = SourceOfLightCapstoneState.CreateRequest(42, 580, 4);
        await WriteCurrentStateAsync(CreatePreTurnSoulRoot(), CreatePreTurnShiningRoot(), request);
        await _fs.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, """
        {
          "schemaVersion": 1,
          "activeConflict": {
            "conflictId": "afterlife_conflict_source_blocker",
            "realm": "Shining Abode",
            "sideModel": "direct_duel",
            "playerSide": {
              "leadContestant": {
                "actorType": "player",
                "actorId": "player_soul",
                "displayName": "Тестовая Душа"
              },
              "supporters": []
            },
            "oppositionSide": {
              "leadContestant": {
                "actorType": "resident",
                "actorId": "resident_lumen",
                "displayName": "Люмен",
                "actorArtTierSnapshot": {
                  "pressure": 1
                },
                "artAuthoritySource": "afterlife_entity_profiles"
              },
              "supporters": []
            },
            "operationType": "pressure",
            "playerSideStrain": "clear",
            "oppositionSideStrain": "clear",
            "conflictPosition": "contested",
            "actionEconomy": {
              "player": {
                "current": 6,
                "max": 6,
                "source": "Средоточие Души tier 0"
              },
              "opposition": {
                "current": 6,
                "max": 6,
                "source": "opposition spiritual authority"
              }
            },
            "resolutionState": "active",
            "exchangeLog": []
          },
          "recentConflicts": []
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "source_of_light_pending_blocked_by_other_contract", StringComparison.OrdinalIgnoreCase) &&
            issue.Actual!.Contains(AfterlifeSpiritualConflictState.StatePath, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_SourceOfLightPendingWithValidManifestationHandoff_DoesNotReportOtherContractBlocker()
    {
        var request = SourceOfLightCapstoneState.CreateRequest(42, 580, 4);
        await WriteCurrentStateAsync(CreatePreTurnSoulRoot(), CreatePreTurnShiningRoot(), request);
        await _fs.WriteFileAtomicAsync(GuardianAbodeResidentRequestState.PendingManifestationRequestPath, """
        {
          "requests": [
            {
              "requestId": "manifest_source_light_allowed_001",
              "manifestationSource": "imprint_relic",
              "relicId": "relic_companion_echo_001",
              "relicName": "Отзвук спутника",
              "targetIncarnation": 2,
              "companionNameHint": "Спутник"
            }
          ]
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "source_of_light_pending_blocked_by_other_contract", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_SourceOfLightPendingWithEmptyRequestsHygieneFiles_DoesNotReportOtherContractBlocker()
    {
        var request = SourceOfLightCapstoneState.CreateRequest(42, 580, 4);
        await WriteCurrentStateAsync(CreatePreTurnSoulRoot(), CreatePreTurnShiningRoot(), request);
        await _fs.WriteFileAtomicAsync(ActorSocialInteractionRequestState.PendingNpcRequestPath, """
        {
          "requests": []
        }
        """);
        await _fs.WriteFileAtomicAsync(NpcTradeRequestState.PendingRequestPath, """
        {
          "requests": []
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "source_of_light_pending_blocked_by_other_contract", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_SourceOfLightPendingWithMalformedManifestationHandoff_IsRejected()
    {
        var request = SourceOfLightCapstoneState.CreateRequest(42, 580, 4);
        await WriteCurrentStateAsync(CreatePreTurnSoulRoot(), CreatePreTurnShiningRoot(), request);
        await _fs.WriteFileAtomicAsync(GuardianAbodeResidentRequestState.PendingManifestationRequestPath, "{ malformed");

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "source_of_light_pending_blocked_by_other_contract", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_SourceOfLightCompletedTupleWithoutPending_DoesNotReportSourceIssues()
    {
        var request = SourceOfLightCapstoneState.CreateRequest(42, 580, 4);
        var soulRoot = CreatePreTurnSoulRoot();
        var shiningRoot = CreatePreTurnShiningRoot();
        ApplySourceOfLightRewards(soulRoot, shiningRoot, request);

        await WriteStateRootsAsync(soulRoot, shiningRoot);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, IsSourceOfLightIssue);
    }

    [Fact]
    public async Task ValidateGameStateAsync_SourceOfLightRewardWithoutValidatedPendingRequest_IsRejected()
    {
        var request = SourceOfLightCapstoneState.CreateRequest(42, 580, 4);
        var preTurnSoul = CreatePreTurnSoulRoot();
        var preTurnShining = CreatePreTurnShiningRoot();
        var currentSoul = preTurnSoul.DeepClone().AsObject();
        var currentShining = preTurnShining.DeepClone().AsObject();
        ApplySourceOfLightRewards(currentSoul, currentShining, request);

        await WriteStateRootsAsync(currentSoul, currentShining);
        await WriteValidatedSnapshotManifestWithoutSourcePendingAsync(preTurnSoul, preTurnShining);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "source_of_light_missing_validated_pending_request", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_SourceOfLightCompletedTupleWithoutPendingMissingRelicBonuses_IsRejected()
    {
        var request = SourceOfLightCapstoneState.CreateRequest(42, 580, 4);
        var soulRoot = CreatePreTurnSoulRoot();
        var shiningRoot = CreatePreTurnShiningRoot();
        ApplySourceOfLightRewards(soulRoot, shiningRoot, request);
        soulRoot["soulRelics"]!["stored"]!.AsArray().OfType<JsonObject>().Single().Remove("effects");

        await WriteStateRootsAsync(soulRoot, shiningRoot);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "source_of_light_relic_missing_characteristic_bonus", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_SourceOfLightCompletedTupleWithoutFullMarkerRadiance_IsRejected()
    {
        var request = SourceOfLightCapstoneState.CreateRequest(42, 580, 4);
        var soulRoot = CreatePreTurnSoulRoot();
        var shiningRoot = CreatePreTurnShiningRoot();
        ApplySourceOfLightRewards(soulRoot, shiningRoot, request);
        var marker = shiningRoot[SourceOfLightCapstoneState.ShiningStateProperty]!.AsObject();
        marker["radianceExperienceAtRequest"] = 0;
        marker["radianceTierAtRequest"] = 0;

        await WriteStateRootsAsync(soulRoot, shiningRoot);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "source_of_light_closure_tuple_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task EnsureHealthyAsync_SourceOfLightMatchingRewardTuple_ClearsPendingRequest()
    {
        var request = SourceOfLightCapstoneState.CreateRequest(42, 580, 4);
        var soulRoot = CreatePreTurnSoulRoot();
        var shiningRoot = CreatePreTurnShiningRoot();
        ApplySourceOfLightRewards(soulRoot, shiningRoot, request);

        await WriteCurrentStateAsync(soulRoot, shiningRoot, request);
        await SourceOfLightCapstoneState.EnsureHealthyAsync(_fs, "Shining Abode");

        Assert.False(_fs.FileExists(SourceOfLightCapstoneState.PendingRequestPath));
    }

    [Theory]
    [InlineData("passive_request_id")]
    [InlineData("relic_source_request_id")]
    [InlineData("turn_mismatch")]
    public async Task EnsureHealthyAsync_SourceOfLightMismatchedRewardTuple_PreservesPendingRequest(string drift)
    {
        var request = SourceOfLightCapstoneState.CreateRequest(42, 580, 4);
        var soulRoot = CreatePreTurnSoulRoot();
        var shiningRoot = CreatePreTurnShiningRoot();
        ApplySourceOfLightRewards(soulRoot, shiningRoot, request);
        ApplySourceOfLightTupleDrift(soulRoot, shiningRoot, drift);

        await WriteCurrentStateAsync(soulRoot, shiningRoot, request);
        await SourceOfLightCapstoneState.EnsureHealthyAsync(_fs, "Shining Abode");

        Assert.True(_fs.FileExists(SourceOfLightCapstoneState.PendingRequestPath));
    }

    [Fact]
    public async Task ValidateGameStateAsync_SourceOfLightPendingAfterCompletedReward_IsRejected()
    {
        var pendingRequest = SourceOfLightCapstoneState.CreateRequest(42, 580, 4);
        var completedRequest = SourceOfLightCapstoneState.CreateRequest(41, 580, 4);
        var soulRoot = CreatePreTurnSoulRoot();
        var shiningRoot = CreatePreTurnShiningRoot();
        ApplySourceOfLightRewards(soulRoot, shiningRoot, completedRequest);

        await WriteCurrentStateAsync(soulRoot, shiningRoot, pendingRequest);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "source_of_light_pending_duplicate_reward_state", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_SourceOfLightPendingAfterCompletedRewardWithSnapshot_IsRejected()
    {
        var pendingRequest = SourceOfLightCapstoneState.CreateRequest(42, 580, 4);
        var completedRequest = SourceOfLightCapstoneState.CreateRequest(41, 580, 4);
        var soulRoot = CreatePreTurnSoulRoot();
        var shiningRoot = CreatePreTurnShiningRoot();
        ApplySourceOfLightRewards(soulRoot, shiningRoot, completedRequest);

        await WriteCurrentStateAsync(soulRoot, shiningRoot, pendingRequest);
        await WriteValidatedSnapshotManifestAsync(pendingRequest, soulRoot, shiningRoot);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "source_of_light_pending_duplicate_reward_state", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("marker")]
    [InlineData("passive")]
    [InlineData("relic")]
    public async Task ValidateGameStateAsync_SourceOfLightPendingWithPartialRewardSurface_IsRejected(string surface)
    {
        var request = SourceOfLightCapstoneState.CreateRequest(42, 580, 4);
        var soulRoot = CreatePreTurnSoulRoot();
        var shiningRoot = CreatePreTurnShiningRoot();
        AddSourceOfLightSurface(soulRoot, shiningRoot, request, surface);

        await WriteCurrentStateAsync(soulRoot, shiningRoot, request);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "source_of_light_pending_duplicate_reward_state", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("passive_request_id")]
    [InlineData("relic_source_request_id")]
    [InlineData("turn_mismatch")]
    public async Task ValidateGameStateAsync_SourceOfLightCompletedTupleDrift_IsRejected(string drift)
    {
        var request = SourceOfLightCapstoneState.CreateRequest(42, 580, 4);
        var soulRoot = CreatePreTurnSoulRoot();
        var shiningRoot = CreatePreTurnShiningRoot();
        ApplySourceOfLightRewards(soulRoot, shiningRoot, request);
        ApplySourceOfLightTupleDrift(soulRoot, shiningRoot, drift);

        await WriteStateRootsAsync(soulRoot, shiningRoot);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "source_of_light_closure_tuple_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("marker")]
    [InlineData("passive")]
    [InlineData("relic")]
    public async Task ValidateGameStateAsync_SourceOfLightPartialRewardSurfaceWithoutPending_IsRejected(string surface)
    {
        var request = SourceOfLightCapstoneState.CreateRequest(42, 580, 4);
        var soulRoot = CreatePreTurnSoulRoot();
        var shiningRoot = CreatePreTurnShiningRoot();
        AddSourceOfLightSurface(soulRoot, shiningRoot, request, surface);

        await WriteStateRootsAsync(soulRoot, shiningRoot);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "source_of_light_closure_tuple_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_DuplicateIncarnatedLightRelics_AreRejected()
    {
        var soulRoot = CreatePreTurnSoulRoot();
        var request = SourceOfLightCapstoneState.CreateRequest(42, 580, 4);
        var relicA = SourceOfLightCapstoneState.CreateIncarnatedLightRelic(request);
        var relicB = SourceOfLightCapstoneState.CreateIncarnatedLightRelic(request);
        soulRoot["soulRelics"]!["stored"] = new JsonArray(relicA, relicB);

        await _fs.WriteFileAtomicAsync(
            "game_state/meta/soul_state.json",
            soulRoot.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "source_of_light_duplicate_incarnated_light_relic", StringComparison.OrdinalIgnoreCase));
    }

    private async Task WriteCurrentStateAsync(
        JsonObject currentSoul,
        JsonObject currentShining,
        SourceOfLightCapstoneState.SourceOfLightCapstoneRequest request)
    {
        var requestJson = JsonSerializer.Serialize(request, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed);
        await _fs.WriteFileAtomicAsync(SourceOfLightCapstoneState.PendingRequestPath, requestJson);
        await _fs.WriteFileAtomicAsync(
            "game_state/meta/soul_state.json",
            currentSoul.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        await _fs.WriteFileAtomicAsync(
            ShiningAbodeState.StatePath,
            currentShining.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
    }

    private async Task WriteStateRootsAsync(JsonObject soulRoot, JsonObject shiningRoot)
    {
        await _fs.WriteFileAtomicAsync(
            "game_state/meta/soul_state.json",
            soulRoot.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        await _fs.WriteFileAtomicAsync(
            ShiningAbodeState.StatePath,
            shiningRoot.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
    }

    private async Task WriteValidatedSnapshotManifestAsync(
        SourceOfLightCapstoneState.SourceOfLightCapstoneRequest request,
        JsonObject preTurnSoul,
        JsonObject preTurnShining,
        ProgressionControl? progressionControl = null)
    {
        const string sessionId = "session_source_of_light_tests";
        const string requestId = "request_source_of_light_tests";
        const int turnNumber = 42;
        var requestJson = JsonSerializer.Serialize(request, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed);
        var soulJson = preTurnSoul.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed);
        var shiningJson = preTurnShining.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed);
        var files = new JsonObject();
        var hashes = new JsonObject();
        var rollbackBaselineFiles = new JsonArray();

        foreach (var (path, json) in new[]
        {
            (SourceOfLightCapstoneState.PendingRequestPath, requestJson),
            ("game_state/meta/soul_state.json", soulJson),
            (ShiningAbodeState.StatePath, shiningJson)
        })
        {
            var snapshotPath = $"game_state/control/pending_turn_snapshot/{path}";
            await _fs.WriteFileAtomicAsync(snapshotPath, json);
            files[path] = snapshotPath;
            hashes[path] = PendingTurnSnapshotAuthority.ComputeSha256(json);
            rollbackBaselineFiles.Add(path);
        }

        await _fs.WriteFileAtomicAsync("input/turn_request.json", $$"""
        {
          "sessionId": "{{sessionId}}",
          "requestId": "{{requestId}}",
          "turnNumber": {{turnNumber}},
          "playerAction": "[SOURCE_OF_LIGHT_CAPSTONE: {{request.RequestId}}] Душа входит в Источник Света."
        }
        """);

        var manifest = new JsonObject
        {
            ["sessionId"] = sessionId,
            ["requestId"] = requestId,
            ["turnNumber"] = turnNumber,
            ["requestTimestamp"] = "2026-05-12T08:00:00Z",
            ["playerAction"] = $"[SOURCE_OF_LIGHT_CAPSTONE: {request.RequestId}] Душа входит в Источник Света."
        };
        if (progressionControl != null)
            manifest["progressionControl"] = JsonSerializer.SerializeToNode(progressionControl, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed);
        manifest["files"] = files;
        manifest["snapshotFileHashes"] = hashes;
        manifest["clientOwnedValidationHashes"] = new JsonObject();
        manifest["rollbackBackups"] = new JsonObject();
        manifest["rollbackBaselineFiles"] = rollbackBaselineFiles;
        manifest["sourceLabel"] = "source-of-light-capstone-validation-tests";
        manifest["manifestPayloadHash"] = string.Empty;
        manifest["manifestPayloadHash"] = PendingTurnSnapshotTestAuthority.ComputeManifestPayloadHash(manifest);

        await _fs.WriteFileAtomicAsync("game_state/control/pending_turn_snapshot.json", manifest.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        await PendingTurnSnapshotTestAuthority.SyncAuthorityForCurrentManifestAsync(_fs);
        await _fs.WriteFileAtomicAsync("ready/turn_complete.json", """
        {
          "accepted": true
        }
        """);
    }

    private async Task WriteValidatedSnapshotManifestWithoutSourcePendingAsync(
        JsonObject preTurnSoul,
        JsonObject preTurnShining)
    {
        const string sessionId = "session_source_of_light_tests";
        const string requestId = "request_source_of_light_tests";
        const int turnNumber = 42;
        var soulJson = preTurnSoul.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed);
        var shiningJson = preTurnShining.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed);
        var files = new JsonObject();
        var hashes = new JsonObject();
        var rollbackBaselineFiles = new JsonArray();

        foreach (var (path, json) in new[]
        {
            ("game_state/meta/soul_state.json", soulJson),
            (ShiningAbodeState.StatePath, shiningJson)
        })
        {
            var snapshotPath = $"game_state/control/pending_turn_snapshot/{path}";
            await _fs.WriteFileAtomicAsync(snapshotPath, json);
            files[path] = snapshotPath;
            hashes[path] = PendingTurnSnapshotAuthority.ComputeSha256(json);
            rollbackBaselineFiles.Add(path);
        }

        await _fs.WriteFileAtomicAsync("input/turn_request.json", $$"""
        {
          "sessionId": "{{sessionId}}",
          "requestId": "{{requestId}}",
          "turnNumber": {{turnNumber}},
          "playerAction": "ordinary Shining turn without Source of Light pending"
        }
        """);

        var manifest = new JsonObject
        {
            ["sessionId"] = sessionId,
            ["requestId"] = requestId,
            ["turnNumber"] = turnNumber,
            ["requestTimestamp"] = "2026-05-12T08:00:00Z",
            ["playerAction"] = "ordinary Shining turn without Source of Light pending",
            ["files"] = files,
            ["snapshotFileHashes"] = hashes,
            ["clientOwnedValidationHashes"] = new JsonObject(),
            ["rollbackBackups"] = new JsonObject(),
            ["rollbackBaselineFiles"] = rollbackBaselineFiles,
            ["sourceLabel"] = "source-of-light-capstone-validation-tests",
            ["manifestPayloadHash"] = string.Empty
        };
        manifest["manifestPayloadHash"] = PendingTurnSnapshotTestAuthority.ComputeManifestPayloadHash(manifest);

        await _fs.WriteFileAtomicAsync("game_state/control/pending_turn_snapshot.json", manifest.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        await PendingTurnSnapshotTestAuthority.SyncAuthorityForCurrentManifestAsync(_fs);
        await _fs.WriteFileAtomicAsync("ready/turn_complete.json", """
        {
          "accepted": true
        }
        """);
    }

    private async Task WriteVerifiedProgressionReportAsync(int shiningAbodeCyclesProcessed)
    {
        await _fs.WriteFileAtomicAsync(ProgressionScheduleService.ReportPath, $$"""
        {
          "progressionProcessingReport": {
            "sessionId": "session_source_of_light_tests",
            "requestId": "request_source_of_light_tests",
            "turnNumber": 42,
            "worldCyclesProcessed": 0,
            "factionCyclesProcessed": 0,
            "chaosSeaCyclesProcessed": 0,
            "guardianProjectCyclesProcessed": 0,
            "residentAgencyCyclesProcessed": 0,
            "shiningAbodeCyclesProcessed": {{shiningAbodeCyclesProcessed}},
            "shiningFactionCyclesProcessed": 0,
            "shiningTradeCyclesProcessed": 0
          }
        }
        """);
    }

    private static JsonObject CreatePreTurnSoulRoot() => new()
    {
        ["soulName"] = "Тестовая Душа",
        ["currentRealm"] = "Shining Abode",
        ["afterlifeCombatProfile"] = new JsonObject
        {
            ["capstones"] = new JsonObject()
        },
        ["soulRelics"] = new JsonObject
        {
            ["equipped"] = new JsonArray(),
            ["stored"] = new JsonArray()
        }
    };

    private static JsonObject CreatePreTurnShiningRoot() => new()
    {
        ["availability"] = ShiningAbodeState.AvailabilityActive,
        ["radiance"] = new JsonObject
        {
            ["experience"] = SourceOfLightCapstoneState.RequiredRadianceExperience,
            ["tier"] = SourceOfLightCapstoneState.RequiredRadianceTier
        },
        ["lightSparks"] = 100,
        ["pendingNativeFactionDiscovery"] = null,
        ["preparedIncarnationPackage"] = null,
        ["sourceOfLightCapstone"] = null,
        ["halls"] = new JsonArray(),
        ["factions"] = new JsonArray(),
        ["shiningPoliticalActors"] = new JsonArray(),
        ["factionFoundingReceipts"] = new JsonArray(),
        ["factionRealignmentReceipts"] = new JsonArray(),
        ["coreActionReceipts"] = new JsonArray(),
        ["gates"] = new JsonObject
        {
            ["draftVersion"] = 0,
            ["hasOpenDraft"] = false,
            ["isStale"] = false,
            ["allCandidateBlessingCards"] = new JsonArray(),
            ["availableBlessingCards"] = new JsonArray(),
            ["shownBlessingCardIds"] = new JsonArray(),
            ["selectedBlessingCardIds"] = new JsonArray(),
            ["nextCandidateCursor"] = 0,
            ["rerollsRemaining"] = 0
        },
        ["gachaSystem"] = new JsonObject
        {
            ["chargesPerReturn"] = 0,
            ["chargesUsedThisReturn"] = 0,
            ["currentReturnCycleId"] = "source_light_test_cycle",
            ["gachaHistory"] = new JsonArray()
        },
        ["treasury"] = new JsonObject
        {
            ["depositedInkFeathers"] = 0,
            ["claimableInkFeatherInterest"] = 0,
            ["totalInterestClaimed"] = 0,
            ["lastInterestSettlementCycleId"] = "",
            ["exchangeCycleId"] = "",
            ["exchangeThisCycleLightSparks"] = 0,
            ["exchangeHistory"] = new JsonArray()
        }
    };

    private static void ApplySourceOfLightRewards(
        JsonObject soulRoot,
        JsonObject shiningRoot,
        SourceOfLightCapstoneState.SourceOfLightCapstoneRequest request)
    {
        soulRoot["afterlifeCombatProfile"]![SourceOfLightCapstoneState.CapstonesProperty] = new JsonObject
        {
            [SourceOfLightCapstoneState.LightIncarnateProperty] =
                SourceOfLightCapstoneState.CreateLightIncarnatePassive(request)
        };
        soulRoot["soulRelics"]!["stored"] = new JsonArray(SourceOfLightCapstoneState.CreateIncarnatedLightRelic(request));
        shiningRoot[SourceOfLightCapstoneState.ShiningStateProperty] =
            SourceOfLightCapstoneState.CreateCompletedShiningMarker(request);
    }

    private static void AddSourceOfLightSurface(
        JsonObject soulRoot,
        JsonObject shiningRoot,
        SourceOfLightCapstoneState.SourceOfLightCapstoneRequest request,
        string surface)
    {
        switch (surface)
        {
            case "marker":
                shiningRoot[SourceOfLightCapstoneState.ShiningStateProperty] =
                    SourceOfLightCapstoneState.CreateCompletedShiningMarker(request);
                break;
            case "passive":
                soulRoot["afterlifeCombatProfile"]![SourceOfLightCapstoneState.CapstonesProperty] = new JsonObject
                {
                    [SourceOfLightCapstoneState.LightIncarnateProperty] =
                        SourceOfLightCapstoneState.CreateLightIncarnatePassive(request)
                };
                break;
            case "relic":
                soulRoot["soulRelics"]!["stored"] = new JsonArray(SourceOfLightCapstoneState.CreateIncarnatedLightRelic(request));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(surface), surface, "Unknown Source of Light surface.");
        }
    }

    private static void ApplySourceOfLightTupleDrift(JsonObject soulRoot, JsonObject shiningRoot, string drift)
    {
        var passive = soulRoot["afterlifeCombatProfile"]![SourceOfLightCapstoneState.CapstonesProperty]![SourceOfLightCapstoneState.LightIncarnateProperty]!.AsObject();
        var relic = soulRoot["soulRelics"]!["stored"]!.AsArray().OfType<JsonObject>().Single();
        switch (drift)
        {
            case "passive_request_id":
                passive["requestId"] = "source_of_light_capstone:999";
                break;
            case "relic_source_request_id":
                relic["sourceRequestId"] = "source_of_light_capstone:999";
                break;
            case "turn_mismatch":
                passive["grantedAtTurn"] = SourceOfLightCapstoneState.GetNodeInt(shiningRoot[SourceOfLightCapstoneState.ShiningStateProperty]?["completedAtTurn"]) + 1;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(drift), drift, "Unknown Source of Light tuple drift.");
        }
    }

    private static bool IsSourceOfLightIssue(ValidationIssue issue) =>
        issue.Code?.Contains("source_of_light", StringComparison.OrdinalIgnoreCase) == true ||
        issue.FilePath.Contains(SourceOfLightCapstoneState.PendingRequestPath, StringComparison.OrdinalIgnoreCase);

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
