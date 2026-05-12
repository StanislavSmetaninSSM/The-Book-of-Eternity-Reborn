using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
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

    private async Task WriteValidatedSnapshotManifestAsync(
        SourceOfLightCapstoneState.SourceOfLightCapstoneRequest request,
        JsonObject preTurnSoul,
        JsonObject preTurnShining)
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
            ["playerAction"] = $"[SOURCE_OF_LIGHT_CAPSTONE: {request.RequestId}] Душа входит в Источник Света.",
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
