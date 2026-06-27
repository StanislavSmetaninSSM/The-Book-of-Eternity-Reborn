using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ChaosSeaTravelValidationTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly ValidationService _validator;

    public ChaosSeaTravelValidationTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-chaos-travel-validation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
        _validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
    }

    [Fact]
    public async Task ValidateAcceptedTurnSpecialActionOutcomesAsync_ChaosSeaTravelWrongActiveGuardian_Fails()
    {
        await WriteNodeAsync("game_state/meta/soul_state.json", CreateSoulRoot());
        await WriteNodeAsync("input/turn_request.json", CreateTravelTurnRequest());
        await WriteNodeAsync("game_state/meta/guardians.json", CreateGuardiansRoot(
            activeGuardianId: "guardian_other_001",
            currentAbodeId: "abode_other_001",
            discoveredAbodes: new[] { "abode_current_001", "abode_target_001", "abode_other_001" },
            targetAbodeDiscovered: true));
        await WritePendingTurnSnapshotAsync(CreateSoulRoot(), CreatePreTurnGuardiansRoot());

        var issues = await _validator.ValidateAcceptedTurnSpecialActionOutcomesAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "chaos_sea_travel_active_guardian_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateAcceptedTurnSpecialActionOutcomesAsync_ChaosSeaTravelTargetMissingFromDiscoveredAbodes_Fails()
    {
        await WriteNodeAsync("game_state/meta/soul_state.json", CreateSoulRoot());
        await WriteNodeAsync("input/turn_request.json", CreateTravelTurnRequest());
        await WriteNodeAsync("game_state/meta/guardians.json", CreateGuardiansRoot(
            activeGuardianId: "guardian_target_001",
            currentAbodeId: "abode_target_001",
            discoveredAbodes: new[] { "abode_current_001" },
            targetAbodeDiscovered: true));
        await WritePendingTurnSnapshotAsync(CreateSoulRoot(), CreatePreTurnGuardiansRoot());

        var issues = await _validator.ValidateAcceptedTurnSpecialActionOutcomesAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "chaos_sea_travel_target_not_discovered", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateAcceptedTurnSpecialActionOutcomesAsync_ChaosSeaTravelOutsideExactChaosSea_Fails()
    {
        await WriteNodeAsync("game_state/meta/soul_state.json", CreateSoulRoot(currentRealm: "Shining Abode"));
        await WriteNodeAsync("input/turn_request.json", CreateTravelTurnRequest());
        await WriteNodeAsync("game_state/meta/guardians.json", CreateGuardiansRoot(
            activeGuardianId: "guardian_target_001",
            currentAbodeId: "abode_target_001",
            discoveredAbodes: new[] { "abode_current_001", "abode_target_001" },
            targetAbodeDiscovered: true));
        await WritePendingTurnSnapshotAsync(CreateSoulRoot(currentRealm: "Shining Abode"), CreatePreTurnGuardiansRoot());

        var issues = await _validator.ValidateAcceptedTurnSpecialActionOutcomesAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "chaos_sea_travel_invalid_realm", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateAcceptedTurnSpecialActionOutcomesAsync_ChaosSeaTravelTargetAddedOnlyAfterTurn_Fails()
    {
        await WriteNodeAsync("game_state/meta/soul_state.json", CreateSoulRoot());
        await WriteNodeAsync("input/turn_request.json", CreateTravelTurnRequest());
        await WriteNodeAsync("game_state/meta/guardians.json", CreateGuardiansRoot(
            activeGuardianId: "guardian_target_001",
            currentAbodeId: "abode_target_001",
            discoveredAbodes: new[] { "abode_current_001", "abode_target_001" },
            targetAbodeDiscovered: true));
        await WritePendingTurnSnapshotAsync(CreateSoulRoot(), CreatePreTurnGuardiansRoot(targetPreviouslyDiscovered: false));

        var issues = await _validator.ValidateAcceptedTurnSpecialActionOutcomesAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "chaos_sea_travel_target_not_discovered", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateAcceptedTurnSpecialActionOutcomesAsync_ChaosSeaTravelExactTargetWithCaseDifferences_Passes()
    {
        await WriteNodeAsync("game_state/meta/soul_state.json", CreateSoulRoot());
        await WriteNodeAsync("input/turn_request.json", CreateTravelTurnRequest());
        await WriteNodeAsync("game_state/meta/guardians.json", CreateGuardiansRoot(
            activeGuardianId: "GUARDIAN_TARGET_001",
            currentAbodeId: "ABODE_TARGET_001",
            discoveredAbodes: new[] { "abode_current_001", "ABODE_TARGET_001" },
            targetAbodeDiscovered: true));
        await WritePendingTurnSnapshotAsync(CreateSoulRoot(), CreatePreTurnGuardiansRoot());

        var issues = await _validator.ValidateAcceptedTurnSpecialActionOutcomesAsync();

        Assert.DoesNotContain(issues, issue =>
            issue.Code?.StartsWith("chaos_sea_travel_", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public async Task DebugResolveGuardianPolicyContextAsync_ChaosSeaTravelProjectsTargetGuardianIntoAuthority()
    {
        await WriteNodeAsync("game_state/meta/soul_state.json", CreateSoulRoot());
        await WriteNodeAsync("input/turn_request.json", CreateTravelTurnRequest());
        await WriteNodeAsync("game_state/meta/guardians.json", CreateGuardiansRoot(
            activeGuardianId: "guardian_target_001",
            currentAbodeId: "abode_target_001",
            discoveredAbodes: new[] { "abode_current_001", "abode_target_001" },
            targetAbodeDiscovered: true));
        await WritePendingTurnSnapshotAsync(CreateSoulRoot(), CreatePreTurnGuardiansRoot());

        var snapshot = await _validator.DebugResolveGuardianPolicyContextAsync();

        Assert.True(snapshot.HasCurrentAuthorityRoot,
            $"CurrentAuthorityRootJson is null. Strict={snapshot.StrictPreTurnGuardianAuthorityStatus}: {snapshot.StrictPreTurnGuardianAuthorityFailureDescription}; " +
            $"Generic={snapshot.GenericSharedStrictPreTurnGuardianAuthorityStatus}: {snapshot.GenericSharedStrictPreTurnGuardianAuthorityFailureDescription}; " +
            $"Manifest={snapshot.ManifestStatus}; GuardiansSnapshot={snapshot.PreTurnGuardiansSnapshotFileStatus}");
        var authorityRoot = JsonNode.Parse(snapshot.CurrentAuthorityRootJson!)!.AsObject();
        Assert.Equal("guardian_target_001", authorityRoot["activeGuardian"]?["guardianId"]?.GetValue<string>());
        Assert.Equal("abode_target_001", authorityRoot["chaosSeaNavigation"]?["currentAbodeId"]?.GetValue<string>());
    }

    private async Task WritePendingTurnSnapshotAsync(JsonObject preTurnSoulRoot, JsonObject preTurnGuardiansRoot)
    {
        const string soulSnapshotPath = "game_state/control/pending_turn_snapshot/game_state/meta/soul_state.json";
        const string guardiansSnapshotPath = "game_state/control/pending_turn_snapshot/game_state/meta/guardians.json";
        var trackerSnapshotPath = $"game_state/control/pending_turn_snapshot/{GuardianProjectState.TrackerPath}";
        var journalSnapshotPath = $"game_state/control/pending_turn_snapshot/{GuardianPowerEventState.JournalPath}";
        var trackerRoot = new JsonObject
        {
            ["activeProjects"] = new JsonArray(),
            ["completedProjects"] = new JsonArray(),
            ["temporaryProjectModifiers"] = new JsonArray()
        };
        var journalRoot = new JsonObject
        {
            ["entries"] = new JsonArray()
        };
        await WriteNodeAsync(soulSnapshotPath, preTurnSoulRoot);
        await WriteNodeAsync(guardiansSnapshotPath, preTurnGuardiansRoot);
        await WriteNodeAsync(trackerSnapshotPath, trackerRoot);
        await WriteNodeAsync(journalSnapshotPath, journalRoot);

        var soulSnapshotJson = await _fs.ReadFileAsync(soulSnapshotPath) ?? string.Empty;
        var guardiansSnapshotJson = await _fs.ReadFileAsync(guardiansSnapshotPath) ?? string.Empty;
        var trackerSnapshotJson = await _fs.ReadFileAsync(trackerSnapshotPath) ?? string.Empty;
        var journalSnapshotJson = await _fs.ReadFileAsync(journalSnapshotPath) ?? string.Empty;
        var manifest = new JsonObject
        {
            ["sessionId"] = "session",
            ["requestId"] = "request",
            ["turnNumber"] = 7,
            ["requestTimestamp"] = "2026-04-24T00:00:00Z",
            ["playerAction"] = TravelPlayerAction,
            ["files"] = new JsonObject
            {
                ["game_state/meta/soul_state.json"] = soulSnapshotPath,
                ["game_state/meta/guardians.json"] = guardiansSnapshotPath,
                [GuardianProjectState.TrackerPath] = trackerSnapshotPath,
                [GuardianPowerEventState.JournalPath] = journalSnapshotPath
            },
            ["snapshotFileHashes"] = new JsonObject
            {
                ["game_state/meta/soul_state.json"] = PendingTurnSnapshotAuthority.ComputeSha256(soulSnapshotJson),
                ["game_state/meta/guardians.json"] = PendingTurnSnapshotAuthority.ComputeSha256(guardiansSnapshotJson),
                [GuardianProjectState.TrackerPath] = PendingTurnSnapshotAuthority.ComputeSha256(trackerSnapshotJson),
                [GuardianPowerEventState.JournalPath] = PendingTurnSnapshotAuthority.ComputeSha256(journalSnapshotJson)
            },
            ["clientOwnedValidationHashes"] = new JsonObject(),
            ["rollbackBackups"] = new JsonObject(),
            ["rollbackBaselineFiles"] = new JsonArray(),
            ["sourceLabel"] = "обычный ход игрока"
        };
        manifest["manifestPayloadHash"] = PendingTurnSnapshotTestAuthority.ComputeManifestPayloadHash(manifest);
        await WriteNodeAsync("game_state/control/pending_turn_snapshot.json", manifest);
        await PendingTurnSnapshotTestAuthority.SyncAuthorityForCurrentManifestAsync(_fs);
    }

    private async Task WriteNodeAsync(string path, JsonNode node)
    {
        await _fs.WriteFileAtomicAsync(path, node.ToJsonString());
    }

    private const string TravelPlayerAction =
        "[CHAOS_SEA_TRAVEL] Душа выбирает перемещение в обитель 'Сад Переходов' " +
        "(targetAbodeId=abode_target_001, targetGuardianId=guardian_target_001, " +
        "previousAbodeId=abode_current_001, previousActiveGuardianId=guardian_current_001).";

    private static JsonObject CreateTravelTurnRequest() => new()
    {
        ["sessionId"] = "session",
        ["requestId"] = "request",
        ["turnNumber"] = 7,
        ["playerAction"] = TravelPlayerAction
    };

    private static JsonObject CreateSoulRoot(string currentRealm = "Chaos Sea") => new()
    {
        ["currentRealm"] = currentRealm,
        ["currentIncarnation"] = 2,
        ["inkFeathers"] = new JsonObject
        {
            ["current"] = 10,
            ["total"] = 10
        }
    };

    private static JsonObject CreatePreTurnGuardiansRoot(bool targetPreviouslyDiscovered = true) => CreateGuardiansRoot(
        activeGuardianId: "guardian_current_001",
        currentAbodeId: "abode_current_001",
        discoveredAbodes: targetPreviouslyDiscovered
            ? new[] { "abode_current_001", "abode_target_001" }
            : new[] { "abode_current_001" },
        targetAbodeDiscovered: targetPreviouslyDiscovered);

    private static JsonObject CreateGuardiansRoot(
        string activeGuardianId,
        string currentAbodeId,
        IEnumerable<string> discoveredAbodes,
        bool targetAbodeDiscovered)
    {
        var guardians = new JsonArray
        {
            CreateGuardian("guardian_current_001", "Азалия", "Social", "abode_current_001", "Шелковая Обитель", true),
            CreateGuardian("guardian_target_001", "Мириэль", "Magic", "abode_target_001", "Сад Переходов", targetAbodeDiscovered),
            CreateGuardian("guardian_other_001", "Орион", "Knowledge", "abode_other_001", "Башня Ориона", true)
        };

        var activeGuardian = activeGuardianId.Contains("target", StringComparison.OrdinalIgnoreCase)
            ? CreateGuardian("guardian_target_001", "Мириэль", "Magic", "abode_target_001", "Сад Переходов", targetAbodeDiscovered)
            : activeGuardianId.Contains("current", StringComparison.OrdinalIgnoreCase)
                ? CreateGuardian("guardian_current_001", "Азалия", "Social", "abode_current_001", "Шелковая Обитель", true)
                : CreateGuardian("guardian_other_001", "Орион", "Knowledge", "abode_other_001", "Башня Ориона", true);

        return new JsonObject
        {
            ["guardians"] = guardians,
            ["activeGuardian"] = activeGuardian,
            ["chaosSeaNavigation"] = new JsonObject
            {
                ["currentAbodeId"] = currentAbodeId,
                ["discoveredAbodes"] = new JsonArray(discoveredAbodes.Select(id => JsonValue.Create(id)).ToArray())
            }
        };
    }

    private static JsonObject CreateGuardian(
        string guardianId,
        string name,
        string domain,
        string abodeId,
        string abodeName,
        bool abodeDiscovered) => new()
    {
        ["guardianId"] = guardianId,
        ["canonicalName"] = name,
        ["nameVariants"] = new JsonObject
        {
            ["default"] = name,
            ["feminine"] = name,
            ["masculine"] = null,
            ["neutral"] = null
        },
        ["manifestation"] = new JsonObject
        {
            ["currentDisplayName"] = name,
            ["formFlexibility"] = "selective",
            ["currentPresentationStyle"] = "feminine",
            ["currentPronouns"] = "она/её",
            ["appearanceDescription"] = $"{name} guards a discovered Chaos Sea abode."
        },
        ["manifestationHistory"] = new JsonArray(),
        ["domain"] = domain,
        ["abode"] = new JsonObject
        {
            ["abodeId"] = abodeId,
            ["name"] = abodeName,
            ["title"] = abodeName,
            ["isDiscovered"] = abodeDiscovered
        },
        ["personalityProfile"] = new JsonObject
        {
            ["archetype"] = $"{domain} Keeper",
            ["speechPattern"] = "Measured",
            ["coreValues"] = new JsonArray("memory", "balance")
        },
        ["relationshipData"] = new JsonObject
        {
            ["currentReputation"] = 10,
            ["reputationHistory"] = new JsonArray(),
            ["lastInteraction"] = null
        },
        ["abodePower"] = new JsonObject
        {
            ["currentPower"] = 10,
            ["tier"] = "Стабильная",
            ["lastUpdatedAt"] = "2026-04-24T00:00:00Z",
            ["history"] = new JsonArray()
        },
        ["guardianRelationships"] = new JsonArray(),
        ["questManagement"] = new JsonObject
        {
            ["availableQuests"] = new JsonArray(),
            ["activeQuests"] = new JsonArray(),
            ["completedQuests"] = new JsonArray()
        },
        ["gachaSystem"] = new JsonObject
        {
            ["chargesPerReturn"] = 0,
            ["chargesUsedThisReturn"] = 0,
            ["gachaHistory"] = new JsonArray()
        }
    };

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
