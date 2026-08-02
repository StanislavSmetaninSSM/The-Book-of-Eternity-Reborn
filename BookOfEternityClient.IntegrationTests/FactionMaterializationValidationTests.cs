using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class FactionMaterializationValidationTests : IDisposable
{
    private const string MortalPath = "game_state/factions/faction_core.json";
    private const string ShiningPath = "game_state/meta/shining_abode_state.json";

    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly ValidationService _validator;

    public FactionMaterializationValidationTests()
    {
        _rootPath = Path.Combine(
            Path.GetTempPath(),
            "boe-faction-materialization-validation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);

        _fs = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
        _validator = new ValidationService(
            _fs,
            NullLogger<ValidationService>.Instance);
    }

    [Theory]
    [InlineData(false, false, true, false, (int)FactionTouchKind.New)]
    [InlineData(true, false, true, false, (int)FactionTouchKind.LegacyPromotion)]
    [InlineData(true, true, true, false, (int)FactionTouchKind.AlreadyMaterialized)]
    [InlineData(true, true, false, false, (int)FactionTouchKind.AlreadyMaterialized)]
    [InlineData(true, false, false, false, (int)FactionTouchKind.UntouchedLegacy)]
    [InlineData(true, true, false, true, (int)FactionTouchKind.ClientDerivedOnly)]
    public void Classify_ReturnsExpectedTouchKind(
        bool existedPreTurn,
        bool hadReceiptPreTurn,
        bool gmAuthoredTouch,
        bool derivedOnly,
        int expected)
    {
        Assert.Equal(
            (FactionTouchKind)expected,
            FactionTouchClassifier.Classify(
                existedPreTurn,
                hadReceiptPreTurn,
                gmAuthoredTouch,
                derivedOnly));
    }

    [Fact]
    public async Task Validate_ChangedHistoricalEnvelope_ReportsImmutableFailure()
    {
        await WriteValidatedMortalFactionAsync(
            preTurnMaterializationId: "fmat_original",
            currentMaterializationId: "fmat_changed");

        var issues = await _validator.ValidateAcceptedTurnRawFactionMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "faction_materialization_immutable_receipt_changed" &&
            issue.Actor == "mortal_faction:faction_watch");
    }

    [Fact]
    public async Task Validate_DuplicateMaterializationIdAcrossFamilies_ReportsDuplicate()
    {
        await WriteMortalAndShiningCreationsAsync(
            mortalMaterializationId: "fmat_shared",
            shiningMaterializationId: "fmat_shared");

        var issues = await _validator.ValidateAcceptedTurnRawFactionMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "faction_materialization_duplicate_id" &&
            issue.Actor == "shining_faction:order_dawn");
    }

    [Fact]
    public async Task Validate_DuplicatePreTurnFactionIdentity_FailsClosed()
    {
        var preTurn = MortalRoot(
            LegacyMortalFaction("faction_watch"),
            LegacyMortalFaction("faction_watch"));
        var current = MortalRoot(
            MaterializedMortalFaction("faction_watch", "fmat_watch"));
        await WriteCurrentAndSnapshotAsync(
            (MortalPath, current.ToJsonString()),
            (MortalPath, preTurn.ToJsonString()));

        var issues = await _validator.ValidateAcceptedTurnRawFactionMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "faction_materialization_pre_turn_authority_unusable" &&
            issue.FilePath == MortalPath);
    }

    [Fact]
    public async Task Validate_DuplicateIdentityAcrossCanonicalAndFullCarriers_ReportsDuplicate()
    {
        var current = MortalRoot(
            MaterializedMortalFaction("faction_watch", "fmat_canonical"));
        current["factionDataChanges"] = new JsonArray(
            MaterializedMortalFaction("faction_watch", "fmat_full"));
        await WriteCurrentAndSnapshotAsync(
            (MortalPath, current.ToJsonString()),
            (MortalPath, MortalRoot().ToJsonString()));

        var issues = await _validator.ValidateAcceptedTurnRawFactionMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "faction_materialization_duplicate_effective_identity" &&
            issue.Actor == "mortal_faction:faction_watch");
    }

    [Fact]
    public async Task Validate_CaseVariantFactionIds_AreDistinctExactIdentities()
    {
        var preTurn = MortalRoot(
            MaterializedMortalFaction(
                "Faction_Watch",
                "fmat_historical_case_variant"));
        var current = MortalRoot(
            MaterializedMortalFaction(
                "faction_watch",
                "fmat_current_exact_identity"));
        await WriteCurrentAndSnapshotAsync(
            (MortalPath, current.ToJsonString()),
            (MortalPath, preTurn.ToJsonString()));

        var issues = await _validator.ValidateAcceptedTurnRawFactionMaterializationAsync();

        Assert.DoesNotContain(issues, issue =>
            issue.Actor == "mortal_faction:faction_watch" &&
            issue.Code is
                "faction_materialization_missing" or
                "faction_materialization_immutable_receipt_changed" or
                "faction_materialization_pre_turn_authority_unusable");
    }

    [Theory]
    [InlineData(false, null)]
    [InlineData(false, "")]
    [InlineData(false, "{")]
    [InlineData(false, "[]")]
    [InlineData(true, null)]
    [InlineData(true, "")]
    [InlineData(true, "{")]
    [InlineData(true, "[]")]
    public async Task Validate_UnusableCurrentAuthorityWithHistoricalFaction_FailsClosed(
        bool shining,
        string? currentJson)
    {
        var path = shining ? ShiningPath : MortalPath;
        var preTurn = shining
            ? ShiningRoot(
                MaterializedShiningFaction("order_dawn", "fmat_historical"))
            : MortalRoot(
                MaterializedMortalFaction("faction_watch", "fmat_historical"));
        if (currentJson != null)
            await _fs.WriteFileAtomicAsync(path, currentJson);
        await WriteValidatedSnapshotManifestAsync((path, preTurn.ToJsonString()));

        var issues = await _validator.ValidateAcceptedTurnRawFactionMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "faction_materialization_current_authority_unusable" &&
            issue.FilePath == path);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Validate_AbsentCurrentAuthorityWithoutHistoricalFactions_RemainsOptional(
        bool shining)
    {
        var path = shining ? ShiningPath : MortalPath;
        var preTurn = shining ? ShiningRoot() : MortalRoot();
        await WriteValidatedSnapshotManifestAsync((path, preTurn.ToJsonString()));

        var issues = await _validator.ValidateAcceptedTurnRawFactionMaterializationAsync();

        Assert.DoesNotContain(issues, issue =>
            issue.Code == "faction_materialization_current_authority_unusable" &&
            issue.FilePath == path);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Validate_RawCanonicalLegacyWithoutSnapshot_FailsClosed(
        bool shining)
    {
        var path = shining ? ShiningPath : MortalPath;
        var current = shining
            ? ShiningRoot(LegacyShiningFaction("order_dawn", factionStrength: 30))
            : MortalRoot(LegacyMortalFaction("faction_watch"));
        await _fs.WriteFileAtomicAsync(path, current.ToJsonString());

        var issues = await _validator.ValidateAcceptedTurnRawFactionMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "faction_materialization_pre_turn_authority_unusable" &&
            issue.FilePath == path);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Validate_PostCanonicalLegacyWithoutSnapshot_RemainsCompatible(
        bool shining)
    {
        var path = shining ? ShiningPath : MortalPath;
        var current = shining
            ? ShiningRoot(LegacyShiningFaction("order_dawn", factionStrength: 30))
            : MortalRoot(LegacyMortalFaction("faction_watch"));
        await _fs.WriteFileAtomicAsync(path, current.ToJsonString());

        var issues = await _validator.ValidateGameStateAsync(
            GameStateValidationPhase.AcceptedTurnFactionMaterializationCompleteness);

        Assert.DoesNotContain(issues, issue =>
            issue.Code is
                "faction_materialization_pre_turn_authority_unusable" or
                "faction_materialization_missing");
    }

    [Fact]
    public async Task Validate_MortalMutationWithoutUsableSnapshot_FailsClosed()
    {
        var current = new JsonObject
        {
            ["factionDataChanges"] = new JsonArray(
                MaterializedMortalFaction("faction_watch", "fmat_watch"))
        };
        await _fs.WriteFileAtomicAsync(MortalPath, current.ToJsonString());

        var issues = await _validator.ValidateAcceptedTurnRawFactionMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "faction_materialization_pre_turn_authority_unusable" &&
            issue.FilePath == MortalPath);
    }

    [Fact]
    public async Task Validate_UntouchedLegacyFaction_DoesNotRequireReceipt()
    {
        var preTurn = MortalRoot(LegacyMortalFaction("faction_watch"));
        var current = MortalRoot(LegacyMortalFaction("faction_watch"));
        await WriteCurrentAndSnapshotAsync(
            (MortalPath, current.ToJsonString()),
            (MortalPath, preTurn.ToJsonString()));

        var issues = await _validator.ValidateAcceptedTurnRawFactionMaterializationAsync();

        Assert.DoesNotContain(issues, issue =>
            issue.Actor == "mortal_faction:faction_watch" &&
            issue.Code == "faction_materialization_missing");
    }

    [Fact]
    public async Task Validate_ShiningDerivedStrengthOnly_DoesNotPromoteLegacyFaction()
    {
        var preTurnFaction = LegacyShiningFaction("order_dawn", factionStrength: 30);
        var currentFaction = LegacyShiningFaction("order_dawn", factionStrength: 31);
        await WriteCurrentAndSnapshotAsync(
            (ShiningPath, ShiningRoot(currentFaction).ToJsonString()),
            (ShiningPath, ShiningRoot(preTurnFaction).ToJsonString()));

        var issues = await _validator.ValidateAcceptedTurnRawFactionMaterializationAsync();

        Assert.DoesNotContain(issues, issue =>
            issue.Actor == "shining_faction:order_dawn" &&
            issue.Code == "faction_materialization_missing");
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
            Directory.Delete(_rootPath, recursive: true);
    }

    private async Task WriteValidatedMortalFactionAsync(
        string preTurnMaterializationId,
        string currentMaterializationId)
    {
        var preTurn = MortalRoot(
            MaterializedMortalFaction("faction_watch", preTurnMaterializationId));
        var current = MortalRoot(
            MaterializedMortalFaction("faction_watch", currentMaterializationId));
        await WriteCurrentAndSnapshotAsync(
            (MortalPath, current.ToJsonString()),
            (MortalPath, preTurn.ToJsonString()));
    }

    private async Task WriteMortalAndShiningCreationsAsync(
        string mortalMaterializationId,
        string shiningMaterializationId)
    {
        var currentMortal = MortalRoot(
            MaterializedMortalFaction("faction_watch", mortalMaterializationId));
        var currentShining = ShiningRoot(
            MaterializedShiningFaction("order_dawn", shiningMaterializationId));
        var preTurnMortal = MortalRoot();
        var preTurnShining = ShiningRoot();

        await WriteCurrentAndSnapshotAsync(
            (MortalPath, currentMortal.ToJsonString()),
            (ShiningPath, currentShining.ToJsonString()),
            (MortalPath, preTurnMortal.ToJsonString()),
            (ShiningPath, preTurnShining.ToJsonString()));
    }

    private async Task WriteCurrentAndSnapshotAsync(
        params (string Path, string Json)[] files)
    {
        var currentByPath = new Dictionary<string, string>(StringComparer.Ordinal);
        var snapshotByPath = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var file in files)
        {
            if (!currentByPath.TryAdd(file.Path, file.Json))
                snapshotByPath[file.Path] = file.Json;
        }

        foreach (var (path, json) in currentByPath)
            await _fs.WriteFileAtomicAsync(path, json);

        await WriteValidatedSnapshotManifestAsync(
            snapshotByPath.Select(entry => (entry.Key, entry.Value)).ToArray());
    }

    private async Task WriteValidatedSnapshotManifestAsync(
        params (string Path, string Json)[] snapshotFiles)
    {
        const string sessionId = "session_faction_materialization_validation";
        const string requestId = "request_faction_materialization_validation";
        const int turnNumber = 12;
        const string playerAction = "Validate faction materialization continuity.";

        await _fs.WriteFileAtomicAsync("input/turn_request.json", $$"""
        {
          "sessionId": "{{sessionId}}",
          "requestId": "{{requestId}}",
          "turnNumber": {{turnNumber}},
          "playerAction": "{{playerAction}}"
        }
        """);

        var files = new JsonObject();
        var snapshotFileHashes = new JsonObject();
        var rollbackBaselineFiles = new JsonArray();
        foreach (var (path, json) in snapshotFiles)
        {
            var snapshotPath = $"game_state/control/pending_turn_snapshot/{path}";
            await _fs.WriteFileAtomicAsync(snapshotPath, json);
            files[path] = snapshotPath;
            snapshotFileHashes[path] = PendingTurnSnapshotAuthority.ComputeSha256(json);
            rollbackBaselineFiles.Add(path);
        }

        var manifest = new JsonObject
        {
            ["sessionId"] = sessionId,
            ["requestId"] = requestId,
            ["turnNumber"] = turnNumber,
            ["requestTimestamp"] = "2026-08-03T00:00:00Z",
            ["playerAction"] = playerAction,
            ["files"] = files,
            ["snapshotFileHashes"] = snapshotFileHashes,
            ["clientOwnedValidationHashes"] = new JsonObject(),
            ["rollbackBackups"] = new JsonObject(),
            ["rollbackBaselineFiles"] = rollbackBaselineFiles,
            ["sourceLabel"] = "accepted faction turn",
            ["manifestPayloadHash"] = string.Empty
        };
        manifest["manifestPayloadHash"] =
            PendingTurnSnapshotTestAuthority.ComputeManifestPayloadHash(manifest);

        await _fs.WriteFileAtomicAsync(
            "game_state/control/pending_turn_snapshot.json",
            manifest.ToJsonString());
        await PendingTurnSnapshotTestAuthority.SyncAuthorityForCurrentManifestAsync(_fs);
    }

    private static JsonObject MortalRoot(params JsonObject[] factions) =>
        new()
        {
            ["factions"] = new JsonArray(
                factions.Select(faction => (JsonNode?)faction).ToArray())
        };

    private static JsonObject ShiningRoot(params JsonObject[] factions) =>
        new()
        {
            ["factions"] = new JsonArray(
                factions.Select(faction => (JsonNode?)faction).ToArray())
        };

    private static JsonObject LegacyMortalFaction(string factionId) =>
        new()
        {
            ["factionId"] = factionId,
            ["name"] = factionId
        };

    private static JsonObject LegacyShiningFaction(
        string factionId,
        int factionStrength) =>
        new()
        {
            ["factionId"] = factionId,
            ["baseStrength"] = 30,
            ["factionStrength"] = factionStrength
        };

    private static JsonObject MaterializedMortalFaction(
        string factionId,
        string materializationId)
    {
        var faction = LegacyMortalFaction(factionId);
        faction["materialization"] = BuildMortalEnvelope(factionId, materializationId);
        return faction;
    }

    private static JsonObject MaterializedShiningFaction(
        string factionId,
        string materializationId)
    {
        var faction = LegacyShiningFaction(factionId, factionStrength: 30);
        faction["materialization"] = BuildShiningEnvelope(factionId, materializationId);
        return faction;
    }

    private static JsonObject BuildMortalEnvelope(
        string factionId,
        string materializationId) =>
        new()
        {
            ["schemaVersion"] = 1,
            ["materializationId"] = materializationId,
            ["factionType"] = "mortal_faction",
            ["factionId"] = factionId,
            ["materializedAtTurn"] = 12,
            ["state"] = "complete",
            ["capabilities"] = new JsonObject
            {
                ["hasFormalHierarchy"] = false,
                ["usesFactionResources"] = false,
                ["maintainsRelations"] = false,
                ["runsProjects"] = false,
                ["holdsTerritoryOrInfluence"] = false,
                ["supportsPlayerMembership"] = false,
                ["usesCustomMechanics"] = false
            },
            ["sections"] = new JsonObject
            {
                ["hierarchy"] = EmptyDisposition("No ranks exist yet."),
                ["resources"] = EmptyDisposition("No formal resources exist yet."),
                ["relations"] = EmptyDisposition("No formal relations exist yet."),
                ["projects"] = EmptyDisposition("No projects exist yet."),
                ["territoryAndInfluence"] = EmptyDisposition("No territory is claimed."),
                ["playerMembership"] = EmptyDisposition("The player is not a member."),
                ["customStates"] = EmptyDisposition("No custom state exists.")
            }
        };

    private static JsonObject BuildShiningEnvelope(
        string factionId,
        string materializationId) =>
        new()
        {
            ["schemaVersion"] = 1,
            ["materializationId"] = materializationId,
            ["factionType"] = "shining_faction",
            ["factionId"] = factionId,
            ["materializedAtTurn"] = 12,
            ["state"] = "complete",
            ["capabilities"] = new JsonObject
            {
                ["runsProjects"] = false,
                ["holdsTerritorialInfluence"] = false,
                ["usesResourceLedger"] = false,
                ["hasResidentAffiliations"] = false,
                ["canTrade"] = false,
                ["hasLeadershipHistory"] = false,
                ["usesStoryState"] = false
            },
            ["sections"] = new JsonObject
            {
                ["projects"] = EmptyDisposition("No projects exist yet."),
                ["territorialInfluence"] = EmptyDisposition("No influence exists yet."),
                ["resourceLedger"] = EmptyDisposition("No resource ledger exists yet."),
                ["residentAffiliations"] = EmptyDisposition("No affiliations exist yet."),
                ["trade"] = EmptyDisposition("No trade authority exists yet."),
                ["leadershipHistory"] = EmptyDisposition("No leadership history exists yet."),
                ["storyState"] = EmptyDisposition("No story state exists yet.")
            }
        };

    private static JsonObject EmptyDisposition(string reason) =>
        new()
        {
            ["state"] = "empty_by_design",
            ["reason"] = reason
        };
}
