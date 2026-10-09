using BookOfEternityClient.Core;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

[Trait("Category", "RegressionIntegration")]
public sealed class GuardianCorrectionServiceTests : IDisposable
{
    private static readonly string[] ChangedCorrectionTransactionPaths =
    [
        GuardianCorrectionService.StatePath,
        GuardianProjectState.TrackerPath,
        GuardianPowerEventState.JournalPath,
        "game_state/meta/guardians.json",
        ResourceMaterializationContract.StatePath,
        ResourceMaterializationContract.HistoryPath,
        CanonicalResourceOwnerAuthorityComposer.AuthorityPath
    ];

    private static readonly string[] ReceiptBoundTransactionPaths =
    [
        GuardianProjectState.TrackerPath,
        GuardianPowerEventState.JournalPath,
        "game_state/meta/guardians.json",
        ResourceMaterializationContract.StatePath,
        ResourceMaterializationContract.HistoryPath,
        CanonicalResourceOwnerAuthorityComposer.AuthorityPath
    ];

    private readonly ITestOutputHelper _output;
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly ScenarioCoreService _scenarioCoreService;
    private readonly GuardianCorrectionService _guardianCorrectionService;

    public GuardianCorrectionServiceTests(ITestOutputHelper output)
    {
        _output = output;
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-guardian-corrections-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);

        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
        _scenarioCoreService = new ScenarioCoreService(_fs, NullLogger<ScenarioCoreService>.Instance);
        _guardianCorrectionService = new GuardianCorrectionService(_fs, _scenarioCoreService, NullLogger<GuardianCorrectionService>.Instance);
    }

    [Fact]
    public async Task ApplyForNewLifeAsync_CapacityChangingCorrectionPublishesCoherentGuardianQuartetForRetry()
    {
        await WriteRawAsync(ScenarioCoreService.ManifestPath, """
        {
          "scenarioCoreAssertions": [
            { "assertionId": "core_role", "category": "role_status", "value": "Игрок начинает королём", "explicit": true, "source": "structured_field" }
          ],
          "candidateAssertions": [],
          "openCorrectionSlots": [
            { "slotId": "slot_protection", "slotType": "protection_or_omen", "maxSeverity": "medium", "allowsFriendly": true, "allowsHostile": true, "sourceAssertionId": "core_role" }
          ]
        }
        """);

        var guardian = new JsonObject
        {
            ["guardianId"] = "guard_capacity_atomic",
            ["canonicalName"] = "Азалия",
            ["nameVariants"] = new JsonObject { ["default"] = "Азалия" },
            ["manifestation"] = new JsonObject
            {
                ["currentDisplayName"] = "Азалия",
                ["formFlexibility"] = "selective",
                ["currentPresentationStyle"] = "feminine",
                ["currentPronouns"] = "она/её",
                ["appearanceDescription"] = "Тестовая форма."
            },
            ["manifestationHistory"] = new JsonArray(),
            ["relationshipData"] = new JsonObject
            {
                ["currentReputation"] = 95,
                ["reputationHistory"] = new JsonArray(),
                ["lastInteraction"] = null
            },
            ["abodePower"] = new JsonObject
            {
                ["currentPower"] = 80,
                ["tier"] = "Сияющая",
                ["lastUpdatedAt"] = "2026-03-23T00:00:00Z",
                ["history"] = new JsonArray()
            },
            ["guardianRelationships"] = new JsonArray(),
            ["gachaSystem"] = new JsonObject
            {
                ["currentReturnCycleId"] = "chaos_return_capacity_atomic",
                ["gachaHistory"] = new JsonArray()
            }
        };
        var guardians = new JsonObject
        {
            ["guardians"] = new JsonArray(guardian),
            ["activeGuardian"] = guardian.DeepClone()
        };
        var profiles = new JsonObject
        {
            [AfterlifeEntityProfileState.ProfilesProperty] = new JsonArray
            {
                AfterlifeActorMaterializationTestFixture.CreateCompleteProfile(
                    actorType: "guardian",
                    actorId: "guard_capacity_atomic",
                    realm: "Chaos Sea",
                    materializedAtTurn: 1)
            }
        };
        await CanonicalResourceQuartetTestFixture.CommitFreshBootstrapAsync(
            _fs,
            new AfterlifeOwnerResourceAcceptedState(
                Profiles: profiles,
                Guardians: guardians));
        var definitions = ResourceDefinitionCatalog.ParseCanonical(
            await _fs.ReadFileAsync(ResourceMaterializationContract.DefinitionsPath),
            allowMissingPristine: false).Catalog!;
        var beforeState = ResourceStateContract.ParseCanonical(
            await _fs.ReadFileAsync(ResourceMaterializationContract.StatePath),
            definitions,
            allowMissingPristine: false).Ledger!;
        var beforeAttempts = Assert.Single(
            beforeState.Entries,
            entry => entry.Coordinate.OwnerKind == ResourceOwnerKind.AfterlifeActor &&
                     entry.Coordinate.ResourceOwnerId == "guard_capacity_atomic" &&
                     entry.Coordinate.ResourceKey == "gacha_attempts");

        await _guardianCorrectionService.ApplyForNewLifeAsync(2, turnNumber: 42);

        var afterStateResult = ResourceStateContract.ParseCanonical(
            await _fs.ReadFileAsync(ResourceMaterializationContract.StatePath),
            definitions,
            allowMissingPristine: false);
        Assert.True(afterStateResult.IsValid, string.Join(Environment.NewLine, afterStateResult.Issues));
        var afterAttempts = Assert.Single(
            afterStateResult.Ledger!.Entries,
            entry => entry.Coordinate.Equals(beforeAttempts.Coordinate));
        Assert.Equal(beforeAttempts.Maximum - 1m, afterAttempts.Maximum);
        Assert.Equal(afterAttempts.Maximum, afterAttempts.Current);

        var historyResult = ResourceHistoryState.ParseCanonical(
            await _fs.ReadFileAsync(ResourceMaterializationContract.HistoryPath),
            definitions,
            allowMissingPristine: false);
        Assert.True(historyResult.IsValid, string.Join(Environment.NewLine, historyResult.Issues));
        var reconfigure = Assert.Single(
            historyResult.History!.Transitions,
            transition => transition.Coordinate.Equals(afterAttempts.Coordinate) &&
                          transition.Operation == ResourceTransitionOperation.Reconfigure);
        Assert.Equal(afterAttempts.Maximum, reconfigure.AfterState!.Maximum);

        var exactAuthority = await CanonicalResourceOwnerAuthorityComposer.ComposeAsync(
            definitions,
            _fs.ReadFileAsync,
            afterStateResult.Ledger,
            historyResult.History,
            CanonicalResourceOwnerAuthorityPurpose.ExistingSessionValidation);
        Assert.True(exactAuthority.IsValid, string.Join(Environment.NewLine, exactAuthority.Issues));
        var persistedAuthority = await _fs.ReadFileAsync(
            CanonicalResourceOwnerAuthorityComposer.AuthorityPath);
        Assert.NotNull(persistedAuthority);
        Assert.True(JsonNode.DeepEquals(
            JsonNode.Parse(exactAuthority.CanonicalAuthorityJson!),
            JsonNode.Parse(persistedAuthority!)));

        var beforeRetry = await CaptureCorrectionTransactionBytesAsync();
        _fs.DeleteFile(ScenarioCoreService.ManifestPath);
        await _guardianCorrectionService.ApplyForNewLifeAsync(2, turnNumber: 42);
        foreach (var (path, expectedBytes) in beforeRetry)
            Assert.Equal(expectedBytes, await _fs.ReadFileBytesAsync(path));
        var retryState = ResourceStateContract.ParseCanonical(
            await _fs.ReadFileAsync(ResourceMaterializationContract.StatePath),
            definitions,
            allowMissingPristine: false);
        var retryHistory = ResourceHistoryState.ParseCanonical(
            await _fs.ReadFileAsync(ResourceMaterializationContract.HistoryPath),
            definitions,
            allowMissingPristine: false);
        var retryAuthority = await CanonicalResourceOwnerAuthorityComposer.ComposeAsync(
            definitions,
            _fs.ReadFileAsync,
            retryState.Ledger!,
            retryHistory.History!,
            CanonicalResourceOwnerAuthorityPurpose.ExistingSessionValidation);
        Assert.True(retryAuthority.IsValid, string.Join(Environment.NewLine, retryAuthority.Issues));
    }

    [Theory]
    [InlineData("incomplete", "guardian_correction_receipt_root_incomplete")]
    [InlineData("duplicate_required", "guardian_correction_receipt_duplicate_property")]
    [InlineData("missing_nested", "guardian_correction_receipt_nested_incomplete")]
    [InlineData("invalid_nested", "guardian_correction_receipt_nested_invalid")]
    public async Task ApplyForNewLifeAsync_SameLifeReplayRejectsNonCanonicalReceipt(
        string mutation,
        string expectedCode)
    {
        await SeedAppliedCorrectionAsync();
        var validReceipt = Assert.IsType<string>(
            await _fs.ReadFileAsync(GuardianCorrectionService.StatePath));
        var invalidReceipt = mutation switch
        {
            "incomplete" => """{ "lifeIncarnation": 2 }""",
            "duplicate_required" => validReceipt.Insert(
                validReceipt.IndexOf('{') + 1,
                "\n  \"lifeIncarnation\": 2,"),
            "missing_nested" => MutateReceipt(validReceipt, root =>
                root.Remove("scenarioCoreSnapshot")),
            "invalid_nested" => MutateReceipt(validReceipt, root =>
                root["scenarioCoreSnapshot"]!["openCorrectionSlots"] = "invalid"),
            _ => throw new InvalidOperationException($"Unknown receipt mutation {mutation}.")
        };
        await _fs.WriteFileAtomicAsync(
            GuardianCorrectionService.StatePath,
            invalidReceipt);
        var beforeReplay = await _fs.ReadFileBytesAsync(
            GuardianCorrectionService.StatePath);

        var error = await Assert.ThrowsAsync<InvalidDataException>(() =>
            _guardianCorrectionService.ApplyForNewLifeAsync(2, turnNumber: 42));

        Assert.Contains(expectedCode, error.Message, StringComparison.Ordinal);
        Assert.Equal(
            beforeReplay,
            await _fs.ReadFileBytesAsync(GuardianCorrectionService.StatePath));
    }

    [Theory]
    [InlineData("nested_null")]
    [InlineData("invalid_intent")]
    [InlineData("invalid_severity")]
    [InlineData("negative_cost")]
    public async Task ApplyForNewLifeAsync_SameLifeReplayRejectsSemanticallyInvalidNestedReceipt(
        string mutation)
    {
        await SeedAppliedCorrectionAsync();
        var validReceipt = Assert.IsType<string>(
            await _fs.ReadFileAsync(GuardianCorrectionService.StatePath));
        var invalidReceipt = MutateAndRefingerprintReceipt(
            validReceipt,
            root =>
            {
                var claimant = root["claimants"]!.AsArray()[0]!.AsObject();
                var correction = root["corrections"]!.AsArray()[0]!.AsObject();
                switch (mutation)
                {
                    case "nested_null":
                        claimant["guardianName"] = null;
                        break;
                    case "invalid_intent":
                        claimant["intent"] = "friendly-ish";
                        break;
                    case "invalid_severity":
                        correction["severity"] = "catastrophic";
                        break;
                    case "negative_cost":
                        correction["abodePowerCost"] = -1;
                        break;
                    default:
                        throw new InvalidOperationException(
                            $"Unknown semantic mutation {mutation}.");
                }
            });
        await _fs.WriteFileAtomicAsync(
            GuardianCorrectionService.StatePath,
            invalidReceipt);
        var beforeReplay = await _fs.ReadFileBytesAsync(
            GuardianCorrectionService.StatePath);

        var error = await Assert.ThrowsAsync<InvalidDataException>(() =>
            _guardianCorrectionService.ApplyForNewLifeAsync(2, turnNumber: 42));

        Assert.Contains(
            "guardian_correction_receipt_semantic_invalid",
            error.Message,
            StringComparison.Ordinal);
        Assert.Equal(
            beforeReplay,
            await _fs.ReadFileBytesAsync(GuardianCorrectionService.StatePath));
    }

    [Fact]
    public async Task ApplyForNewLifeAsync_SameLifeReplayRejectsReceiptForDifferentActiveGuardian()
    {
        await SeedAppliedCorrectionAsync();
        var guardians = JsonNode.Parse(Assert.IsType<string>(
            await _fs.ReadFileAsync("game_state/meta/guardians.json")))!.AsObject();
        guardians["activeGuardian"]!["guardianId"] = "guard_other_active";
        await _fs.WriteFileAtomicAsync(
            "game_state/meta/guardians.json",
            guardians.ToJsonString());

        var error = await Assert.ThrowsAsync<InvalidDataException>(() =>
            _guardianCorrectionService.ApplyForNewLifeAsync(2, turnNumber: 42));

        Assert.Contains(
            "guardian_correction_receipt_guardian_mismatch",
            error.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task ApplyForNewLifeAsync_PublishesVersionedSelfFingerprintedReceipt()
    {
        await SeedAppliedCorrectionAsync();
        var receipt = JsonNode.Parse(Assert.IsType<string>(
            await _fs.ReadFileAsync(GuardianCorrectionService.StatePath)))!.AsObject();

        Assert.NotNull(receipt["schemaVersion"]);
        Assert.Equal(1, receipt["schemaVersion"]!.GetValue<int>());
        Assert.NotNull(receipt["receiptFingerprint"]);
        Assert.Matches(
            "^sha256:[0-9a-f]{64}$",
            receipt["receiptFingerprint"]!.GetValue<string>());
        Assert.Matches(
            "^sha256:[0-9a-f]{64}$",
            receipt["transactionAfterImageFingerprint"]!.GetValue<string>());
        var manifest = Assert.IsType<JsonArray>(
            receipt["transactionAfterImagePaths"]);
        Assert.NotEmpty(manifest);
        Assert.Equal(
            manifest
                .Select(static path => path!.GetValue<string>())
                .OrderBy(static path => path, StringComparer.Ordinal),
            manifest.Select(static path => path!.GetValue<string>()));
        Assert.Equal(
            manifest.Count,
            manifest
                .Select(static path => path!.GetValue<string>())
                .Distinct(StringComparer.Ordinal)
                .Count());
        Assert.DoesNotContain(
            GuardianCorrectionService.StatePath,
            manifest.Select(static path => path!.GetValue<string>()));
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("unsorted")]
    [InlineData("backslash_alias")]
    [InlineData("receipt_path")]
    [InlineData("unknown_path")]
    [InlineData("case_alias")]
    public async Task ApplyForNewLifeAsync_SameLifeReplayRejectsInvalidAfterImageManifest(
        string mutation)
    {
        await SeedAppliedCorrectionAsync();
        var validReceipt = Assert.IsType<string>(
            await _fs.ReadFileAsync(GuardianCorrectionService.StatePath));
        var invalidReceipt = MutateManifestAndRefingerprintReceipt(
            validReceipt,
            manifest =>
            {
                switch (mutation)
                {
                    case "duplicate":
                        manifest.Add(manifest[0]!.DeepClone());
                        break;
                    case "unsorted":
                    {
                        var reversed = manifest
                            .Select(static path => path!.GetValue<string>())
                            .Reverse()
                            .Select(static path => (JsonNode)path)
                            .ToArray();
                        manifest.Clear();
                        foreach (var path in reversed)
                            manifest.Add(path);
                        break;
                    }
                    case "backslash_alias":
                        manifest[0] = manifest[0]!
                            .GetValue<string>()
                            .Replace('/', '\\');
                        break;
                    case "receipt_path":
                        manifest[0] = GuardianCorrectionService.StatePath;
                        break;
                    case "unknown_path":
                        manifest[0] =
                            "game_state/meta/unknown_guardian_companion.json";
                        break;
                    case "case_alias":
                        manifest[0] = manifest[0]!
                            .GetValue<string>()
                            .ToUpperInvariant();
                        break;
                    default:
                        throw new InvalidOperationException(
                            $"Unknown manifest mutation {mutation}.");
                }
            });
        await _fs.WriteFileAtomicAsync(
            GuardianCorrectionService.StatePath,
            invalidReceipt);
        var beforeReplay = await CaptureCorrectionTransactionBytesAsync();

        var error = await Assert.ThrowsAsync<InvalidDataException>(() =>
            _guardianCorrectionService.ApplyForNewLifeAsync(2, turnNumber: 42));

        Assert.Contains(
            "guardian_correction_receipt_manifest_invalid",
            error.Message,
            StringComparison.Ordinal);
        foreach (var (path, expectedBytes) in beforeReplay)
            Assert.Equal(expectedBytes, await _fs.ReadFileBytesAsync(path));
    }

    [Fact]
    public async Task ApplyForNewLifeAsync_SameLifeReplayRejectsOrphanReceiptAgainstCoherentPreApplyState()
    {
        await SeedFailureInjectionFixtureAsync();
        var beforeLifeThree = await CaptureCorrectionTransactionBytesAsync();
        await _guardianCorrectionService.ApplyForNewLifeAsync(3, turnNumber: 43);
        var appliedReceipt = await _fs.ReadFileBytesAsync(
            GuardianCorrectionService.StatePath);
        Assert.NotNull(appliedReceipt);

        foreach (var path in ReceiptBoundTransactionPaths)
            await RestoreCapturedPathAsync(path, beforeLifeThree[path]);

        Assert.Equal(
            appliedReceipt,
            await _fs.ReadFileBytesAsync(GuardianCorrectionService.StatePath));
        var orphanedBeforeReplay = await CaptureCorrectionTransactionBytesAsync();

        var error = await Assert.ThrowsAsync<InvalidDataException>(() =>
            _guardianCorrectionService.ApplyForNewLifeAsync(3, turnNumber: 43));

        Assert.Contains(
            "guardian_correction_receipt_after_image_mismatch",
            error.Message,
            StringComparison.Ordinal);
        foreach (var (path, expectedBytes) in orphanedBeforeReplay)
            Assert.Equal(expectedBytes, await _fs.ReadFileBytesAsync(path));
    }

    [Fact]
    public async Task ApplyForNewLifeAsync_SameLifeReplayRejectsRestoredDynamicProfileCompanion()
    {
        await SeedCorrectionInputAsync();
        var profileBeforeNormalization =
            await MakeGuardianProfileRequireRealmNormalizationAsync();

        await _guardianCorrectionService.ApplyForNewLifeAsync(
            2,
            turnNumber: 42);

        var receiptAfterApply = await _fs.ReadFileBytesAsync(
            GuardianCorrectionService.StatePath);
        var normalizedProfile = await _fs.ReadFileBytesAsync(
            AfterlifeEntityProfileState.StatePath);
        Assert.NotNull(receiptAfterApply);
        Assert.NotNull(normalizedProfile);
        Assert.False(BytesEqual(profileBeforeNormalization, normalizedProfile));
        await _fs.WriteFileAtomicBytesAsync(
            AfterlifeEntityProfileState.StatePath,
            profileBeforeNormalization);
        var orphanedBeforeReplay = await CaptureCorrectionTransactionBytesAsync();

        var error = await Assert.ThrowsAsync<InvalidDataException>(() =>
            _guardianCorrectionService.ApplyForNewLifeAsync(
                2,
                turnNumber: 42));

        Assert.Contains(
            "guardian_correction_receipt_after_image_mismatch",
            error.Message,
            StringComparison.Ordinal);
        Assert.Equal(
            receiptAfterApply,
            await _fs.ReadFileBytesAsync(GuardianCorrectionService.StatePath));
        foreach (var (path, expectedBytes) in orphanedBeforeReplay)
            Assert.Equal(expectedBytes, await _fs.ReadFileBytesAsync(path));
    }

    [Theory]
    [InlineData("candidate_claimant_intent")]
    [InlineData("winner_candidate_payload")]
    [InlineData("slot_intent_allowance")]
    [InlineData("slot_severity_ceiling")]
    [InlineData("stale_medium_identity_and_arithmetic")]
    public async Task ApplyForNewLifeAsync_SameLifeReplayRejectsCrossRecordInvariantViolation(
        string mutation)
    {
        await SeedAppliedCorrectionAsync();
        var validReceipt = Assert.IsType<string>(
            await _fs.ReadFileAsync(GuardianCorrectionService.StatePath));
        var invalidReceipt = MutateAndRefingerprintReceipt(
            validReceipt,
            root =>
            {
                var claimant = root["claimants"]!.AsArray()[0]!.AsObject();
                var contest = root["contestedSlots"]!.AsArray()[0]!.AsObject();
                var candidate = contest["candidates"]!.AsArray()[0]!.AsObject();
                var correction = root["corrections"]!.AsArray()[0]!.AsObject();
                var slot = root["scenarioCoreSnapshot"]!["openCorrectionSlots"]!
                    .AsArray()[0]!.AsObject();
                switch (mutation)
                {
                    case "candidate_claimant_intent":
                        candidate["intent"] = "hostile";
                        break;
                    case "winner_candidate_payload":
                        candidate["title"] = "Иное каноническое название";
                        break;
                    case "slot_intent_allowance":
                        slot["allowsFriendly"] = false;
                        slot["allowsHostile"] = true;
                        break;
                    case "slot_severity_ceiling":
                        slot["maxSeverity"] = "minor";
                        break;
                    case "stale_medium_identity_and_arithmetic":
                        candidate["severity"] = "minor";
                        candidate["budgetCostPoints"] = 1;
                        candidate["abodePowerCost"] = 5;
                        correction["severity"] = "minor";
                        correction["budgetCostPoints"] = 1;
                        correction["abodePowerCost"] = 5;
                        root["totalAbodePowerSpent"] = 5;
                        Assert.Equal(
                            contest["winnerCorrectionId"]!.GetValue<string>(),
                            correction["correctionId"]!.GetValue<string>());
                        Assert.Equal(
                            root["powerAfter"]!.GetValue<int>(),
                            claimant["powerAfter"]!.GetValue<int>());
                        Assert.Equal(
                            root["remainingBudgetPoints"]!.GetValue<int>(),
                            claimant["remainingBudgetPoints"]!.GetValue<int>());
                        break;
                    default:
                        throw new InvalidOperationException(
                            $"Unknown cross-record mutation {mutation}.");
                }
            });
        await _fs.WriteFileAtomicAsync(
            GuardianCorrectionService.StatePath,
            invalidReceipt);
        var beforeReplay = await CaptureCorrectionTransactionBytesAsync();

        var error = await Assert.ThrowsAsync<InvalidDataException>(() =>
            _guardianCorrectionService.ApplyForNewLifeAsync(2, turnNumber: 42));

        Assert.Contains(
            "guardian_correction_receipt_semantic_invalid",
            error.Message,
            StringComparison.Ordinal);
        foreach (var (path, expectedBytes) in beforeReplay)
            Assert.Equal(expectedBytes, await _fs.ReadFileBytesAsync(path));
    }

    [Fact]
    public async Task ApplyForNewLifeAsync_SameLifeReplayRejectsCoherentDeterministicLoserSubstitution()
    {
        await SeedContestedCorrectionInputAsync();
        await _guardianCorrectionService.ApplyForNewLifeAsync(
            4,
            turnNumber: 44);
        var validReceipt = Assert.IsType<string>(
            await _fs.ReadFileAsync(GuardianCorrectionService.StatePath));
        var invalidReceipt = MutateAndRefingerprintReceipt(
            validReceipt,
            root =>
            {
                var contest = Assert.IsType<JsonObject>(Assert.Single(
                    root["contestedSlots"]!.AsArray()));
                var candidates = contest["candidates"]!.AsArray()
                    .Select(static candidate => candidate!.AsObject())
                    .ToList();
                Assert.Equal(2, candidates.Count);
                var originalWinnerId = contest["winnerCorrectionId"]!
                    .GetValue<string>();
                var loser = Assert.Single(candidates, candidate =>
                    !string.Equals(
                        candidate["candidateCorrectionId"]!.GetValue<string>(),
                        originalWinnerId,
                        StringComparison.Ordinal));
                var originalWinnerName = contest["winnerGuardianName"]!
                    .GetValue<string>();
                var loserName = loser["sourceGuardianName"]!.GetValue<string>();

                contest["winnerGuardianId"] =
                    loser["sourceGuardianId"]!.GetValue<string>();
                contest["winnerGuardianName"] = loserName;
                contest["winnerCorrectionId"] =
                    loser["candidateCorrectionId"]!.GetValue<string>();

                var correction = Assert.IsType<JsonObject>(Assert.Single(
                    root["corrections"]!.AsArray()));
                correction["correctionId"] =
                    loser["candidateCorrectionId"]!.GetValue<string>();
                correction["sourceGuardianId"] =
                    loser["sourceGuardianId"]!.GetValue<string>();
                correction["sourceGuardianName"] = loserName;
                correction["intent"] = loser["intent"]!.GetValue<string>();
                correction["severity"] = loser["severity"]!.GetValue<string>();
                correction["budgetCostPoints"] =
                    loser["budgetCostPoints"]!.GetValue<int>();
                correction["abodePowerCost"] =
                    loser["abodePowerCost"]!.GetValue<int>();
                correction["claimStrength"] =
                    loser["claimStrength"]!.GetValue<int>();
                correction["title"] = loser["title"]!.GetValue<string>();
                correction["summary"] = correction["summary"]!
                    .GetValue<string>()
                    .Replace(
                        originalWinnerName,
                        loserName,
                        StringComparison.Ordinal);
                correction["reason"] = correction["reason"]!
                    .GetValue<string>()
                    .Replace(
                        originalWinnerName,
                        loserName,
                        StringComparison.Ordinal);

                var loserGuardianId = loser["sourceGuardianId"]!
                    .GetValue<string>();
                foreach (var claimant in root["claimants"]!.AsArray()
                             .Select(static claimant => claimant!.AsObject()))
                {
                    var initialBudget =
                        claimant["baseBudgetPoints"]!.GetValue<int>() +
                        claimant["preparationBudgetPoints"]!.GetValue<int>();
                    var currentPower = claimant["currentPower"]!.GetValue<int>();
                    var isSubstitutedWinner = string.Equals(
                        claimant["guardianId"]!.GetValue<string>(),
                        loserGuardianId,
                        StringComparison.Ordinal);
                    claimant["remainingBudgetPoints"] = initialBudget -
                        (isSubstitutedWinner
                            ? loser["budgetCostPoints"]!.GetValue<int>()
                            : 0);
                    claimant["powerAfter"] = currentPower -
                        (isSubstitutedWinner
                            ? loser["abodePowerCost"]!.GetValue<int>()
                            : 0);
                }

                var activeClaimant = Assert.Single(
                    root["claimants"]!.AsArray()
                        .Select(static claimant => claimant!.AsObject()),
                    claimant => claimant["isActivePatron"]!.GetValue<bool>());
                root["powerAfter"] =
                    activeClaimant["powerAfter"]!.GetValue<int>();
                root["remainingBudgetPoints"] =
                    activeClaimant["remainingBudgetPoints"]!.GetValue<int>();
                root["totalAbodePowerSpent"] =
                    loser["abodePowerCost"]!.GetValue<int>();
                root["resolutionOrder"] = new JsonArray(
                    $"{contest["slotId"]!.GetValue<string>()}: {loserName} [{loser["severity"]!.GetValue<string>()}]");
            });
        await _fs.WriteFileAtomicAsync(
            GuardianCorrectionService.StatePath,
            invalidReceipt);
        var beforeReplay = await CaptureCorrectionTransactionBytesAsync();

        var error = await Assert.ThrowsAsync<InvalidDataException>(() =>
            _guardianCorrectionService.ApplyForNewLifeAsync(
                4,
                turnNumber: 44));

        Assert.Contains(
            "guardian_correction_receipt_semantic_invalid",
            error.Message,
            StringComparison.Ordinal);
        foreach (var (path, expectedBytes) in beforeReplay)
            Assert.Equal(expectedBytes, await _fs.ReadFileBytesAsync(path));
    }

    [Fact]
    public async Task ApplyForNewLifeAsync_StructuralCorrectionIdsDistinguishDelimiterAmbiguousTuples()
    {
        await SeedDelimiterAmbiguousCorrectionFixtureAsync();

        await _guardianCorrectionService.ApplyForNewLifeAsync(4, turnNumber: 44);

        var state = Assert.IsType<GuardianCorrectionService.GuardianCorrectionsState>(
            await _guardianCorrectionService.ReadAsync());
        Assert.True(
            state.Corrections.Count == 2,
            JsonSerializer.Serialize(
                state,
                SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        Assert.Equal(
            state.Corrections.Count,
            state.Corrections
                .Select(static correction => correction.CorrectionId)
                .Distinct(StringComparer.Ordinal)
                .Count());
        Assert.All(
            state.Corrections,
            static correction => Assert.Matches(
                "^gcor_[0-9a-f]{64}$",
                correction.CorrectionId));
        Assert.Equal(
            state.ContestedSlots.Sum(static contest => contest.Candidates.Count),
            state.ContestedSlots
                .SelectMany(static contest => contest.Candidates)
                .Select(static candidate => candidate.CandidateCorrectionId)
                .Distinct(StringComparer.Ordinal)
                .Count());

        var journal = JsonNode.Parse(Assert.IsType<string>(
            await _fs.ReadFileAsync(GuardianPowerEventState.JournalPath)))!.AsObject();
        var spendEventIds = journal["entries"]!.AsArray()
            .OfType<JsonObject>()
            .Where(static entry => string.Equals(
                entry["reasonType"]?.GetValue<string>(),
                "correction_spend",
                StringComparison.Ordinal))
            .Select(static entry => entry["eventId"]!.GetValue<string>())
            .ToList();
        Assert.Equal(2, spendEventIds.Count);
        Assert.Equal(
            spendEventIds.Count,
            spendEventIds.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public async Task ApplyForNewLifeAsync_LateFailureRestoresInitiallyAbsentReceiptAndJournal()
    {
        await SeedCorrectionInputAsync();
        Assert.Null(await _fs.ReadFileBytesAsync(GuardianCorrectionService.StatePath));
        Assert.Null(await _fs.ReadFileBytesAsync(GuardianPowerEventState.JournalPath));
        var before = await CaptureCorrectionTransactionBytesAsync();
        var resolvedFailurePath = _fs.ResolvePath(
            CanonicalResourceOwnerAuthorityComposer.AuthorityPath);
        var injected = 0;
        var hookedFileSystem = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance,
            new FileSystemManagerHooks
            {
                AfterPhysicalFilePublishedAsync = path =>
                {
                    if (!string.Equals(
                            path,
                            resolvedFailurePath,
                            StringComparison.OrdinalIgnoreCase) ||
                        Interlocked.Exchange(ref injected, 1) != 0)
                    {
                        return Task.CompletedTask;
                    }

                    return Task.FromException(new IOException(
                        "Injected late Guardian authority publication failure."));
                }
            });
        var service = CreateCorrectionService(hookedFileSystem);

        await Assert.ThrowsAsync<IOException>(() =>
            service.ApplyForNewLifeAsync(2, turnNumber: 42));

        Assert.Equal(1, Volatile.Read(ref injected));
        foreach (var (path, expectedBytes) in before)
            Assert.Equal(expectedBytes, await _fs.ReadFileBytesAsync(path));
        Assert.Null(await _fs.ReadFileBytesAsync(GuardianCorrectionService.StatePath));
        Assert.Null(await _fs.ReadFileBytesAsync(GuardianPowerEventState.JournalPath));
    }

    [Fact]
    public async Task ApplyForNewLifeAsync_FailureInjectionFixtureChangesEveryTransactionalPath()
    {
        await SeedFailureInjectionFixtureAsync();
        var before = await CaptureCorrectionTransactionBytesAsync();

        await _guardianCorrectionService.ApplyForNewLifeAsync(3, turnNumber: 43);

        foreach (var path in ChangedCorrectionTransactionPaths)
        {
            var after = await _fs.ReadFileBytesAsync(path);
            Assert.False(
                BytesEqual(before[path], after),
                $"Failure-injection fixture did not change required transaction path '{path}'.");
        }
    }

    [Fact]
    public async Task ApplyForNewLifeAsync_SameSemanticCorrectionAcrossLivesAppendsDistinctSpendAndReplayIsNoOp()
    {
        await SeedFailureInjectionFixtureAsync();
        var journalBefore = JsonNode.Parse(Assert.IsType<string>(
            await _fs.ReadFileAsync(GuardianPowerEventState.JournalPath)))!.AsObject();
        var spendsBefore = Assert.IsType<JsonArray>(journalBefore["entries"])
            .OfType<JsonObject>()
            .Where(entry => string.Equals(
                entry["reasonType"]?.GetValue<string>(),
                "correction_spend",
                StringComparison.Ordinal))
            .ToList();

        await _guardianCorrectionService.ApplyForNewLifeAsync(3, turnNumber: 43);

        var journalAfter = JsonNode.Parse(Assert.IsType<string>(
            await _fs.ReadFileAsync(GuardianPowerEventState.JournalPath)))!.AsObject();
        var spendsAfter = Assert.IsType<JsonArray>(journalAfter["entries"])
            .OfType<JsonObject>()
            .Where(entry => string.Equals(
                entry["reasonType"]?.GetValue<string>(),
                "correction_spend",
                StringComparison.Ordinal))
            .ToList();
        Assert.Equal(spendsBefore.Count + 1, spendsAfter.Count);
        Assert.Single(
            spendsAfter
                .Select(entry => entry["audit"]!["correctionId"]!.GetValue<string>())
                .Distinct(StringComparer.Ordinal));
        Assert.Equal(
            spendsAfter.Count,
            spendsAfter
                .Select(entry => entry["eventId"]!.GetValue<string>())
                .Distinct(StringComparer.Ordinal)
                .Count());

        var beforeReplay = await CaptureCorrectionTransactionBytesAsync();
        _fs.DeleteFile(ScenarioCoreService.ManifestPath);
        await _guardianCorrectionService.ApplyForNewLifeAsync(3, turnNumber: 43);
        foreach (var (path, expectedBytes) in beforeReplay)
            Assert.Equal(expectedBytes, await _fs.ReadFileBytesAsync(path));
    }

    [Theory]
    [InlineData(GuardianCorrectionService.StatePath)]
    [InlineData(GuardianProjectState.TrackerPath)]
    [InlineData(GuardianPowerEventState.JournalPath)]
    [InlineData("game_state/meta/guardians.json")]
    [InlineData(ResourceMaterializationContract.StatePath)]
    [InlineData(ResourceMaterializationContract.HistoryPath)]
    [InlineData(CanonicalResourceOwnerAuthorityComposer.AuthorityPath)]
    public async Task ApplyForNewLifeAsync_FailureAfterEachChangedPublicationRestoresExactTransaction(
        string failurePath)
    {
        await SeedFailureInjectionFixtureAsync();
        var before = await CaptureCorrectionTransactionBytesAsync();
        var resolvedFailurePath = _fs.ResolvePath(failurePath);
        var injected = 0;
        var hookedFileSystem = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance,
            new FileSystemManagerHooks
            {
                AfterPhysicalFilePublishedAsync = path =>
                {
                    if (!string.Equals(
                            path,
                            resolvedFailurePath,
                            StringComparison.OrdinalIgnoreCase) ||
                        Interlocked.Exchange(ref injected, 1) != 0)
                    {
                        return Task.CompletedTask;
                    }

                    return Task.FromException(new IOException(
                        $"Injected Guardian correction publication failure at '{failurePath}'."));
                }
            });
        var service = CreateCorrectionService(hookedFileSystem);

        await Assert.ThrowsAsync<IOException>(() =>
            service.ApplyForNewLifeAsync(3, turnNumber: 43));

        Assert.Equal(1, Volatile.Read(ref injected));
        foreach (var (path, expectedBytes) in before)
            Assert.Equal(expectedBytes, await _fs.ReadFileBytesAsync(path));
    }

    [Fact]
    public async Task ApplyForNewLifeAsync_BaselineConflictPreservesConcurrentReceiptAndOtherPaths()
    {
        await SeedFailureInjectionFixtureAsync();
        var before = await CaptureCorrectionTransactionBytesAsync();
        var receiptPath = _fs.ResolvePath(GuardianCorrectionService.StatePath);
        var conflictBytes = System.Text.Encoding.UTF8.GetBytes(
            "{\"externalAuthority\":\"must-be-preserved\"}");
        var receiptReads = 0;
        var injected = 0;
        var hookedFileSystem = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance,
            new FileSystemManagerHooks
            {
                BeforeCanonicalReadOpenAsync = path =>
                {
                    if (!string.Equals(
                            path,
                            GuardianCorrectionService.StatePath,
                            StringComparison.Ordinal) ||
                        Interlocked.Increment(ref receiptReads) != 2)
                    {
                        return Task.CompletedTask;
                    }

                    File.WriteAllBytes(receiptPath, conflictBytes);
                    Interlocked.Exchange(ref injected, 1);
                    return Task.CompletedTask;
                }
            });
        var service = CreateCorrectionService(hookedFileSystem);

        await Assert.ThrowsAsync<IOException>(() =>
            service.ApplyForNewLifeAsync(3, turnNumber: 43));

        Assert.Equal(1, Volatile.Read(ref injected));
        Assert.Equal(
            conflictBytes,
            await _fs.ReadFileBytesAsync(GuardianCorrectionService.StatePath));
        foreach (var (path, expectedBytes) in before)
        {
            if (string.Equals(
                    path,
                    GuardianCorrectionService.StatePath,
                    StringComparison.Ordinal))
            {
                continue;
            }

            Assert.Equal(expectedBytes, await _fs.ReadFileBytesAsync(path));
        }
    }

    [Theory]
    [InlineData("tracker")]
    [InlineData("journal")]
    public async Task ApplyForNewLifeAsync_UnchangedFingerprintedBaselineConflictPreventsPublication(
        string baseline)
    {
        await SeedCorrectionInputAsync();
        var targetPath = baseline switch
        {
            "tracker" => GuardianProjectState.TrackerPath,
            "journal" => GuardianPowerEventState.JournalPath,
            _ => throw new InvalidOperationException(
                $"Unknown fingerprinted baseline {baseline}.")
        };
        if (string.Equals(baseline, "tracker", StringComparison.Ordinal))
        {
            await WriteRawAsync(
                GuardianProjectState.TrackerPath,
                """{ "completedProjects": [] }""");
        }
        else
        {
            await WriteRawAsync(ScenarioCoreService.ManifestPath, """
            {
              "scenarioCoreAssertions": [
                { "assertionId": "core_role", "category": "role_status", "value": "Игрок начинает королём", "explicit": true, "source": "structured_field" }
              ],
              "candidateAssertions": [],
              "openCorrectionSlots": []
            }
            """);
            await WriteRawAsync(
                GuardianPowerEventState.JournalPath,
                """{ "entries": [] }""");
        }

        var before = await CaptureCorrectionTransactionBytesAsync();
        var resolvedTargetPath = _fs.ResolvePath(targetPath);
        var concurrentBytes = System.Text.Encoding.UTF8.GetBytes(
            baseline == "tracker"
                ? """{ "completedProjects": [], "externalAuthority": "tracker" }"""
                : """{ "entries": [], "externalAuthority": "journal" }""");
        var targetReads = 0;
        var injected = 0;
        var hookedFileSystem = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance,
            new FileSystemManagerHooks
            {
                BeforeCanonicalReadOpenAsync = path =>
                {
                    if (!string.Equals(
                            path,
                            targetPath,
                            StringComparison.Ordinal) ||
                        Interlocked.Increment(ref targetReads) != 3)
                    {
                        return Task.CompletedTask;
                    }

                    File.WriteAllBytes(resolvedTargetPath, concurrentBytes);
                    Interlocked.Exchange(ref injected, 1);
                    return Task.CompletedTask;
                }
            });
        var service = CreateCorrectionService(hookedFileSystem);

        await Assert.ThrowsAsync<IOException>(() =>
            service.ApplyForNewLifeAsync(2, turnNumber: 42));

        Assert.Equal(1, Volatile.Read(ref injected));
        Assert.Null(await _fs.ReadFileBytesAsync(GuardianCorrectionService.StatePath));
        Assert.Equal(concurrentBytes, await _fs.ReadFileBytesAsync(targetPath));
        foreach (var (path, expectedBytes) in before)
        {
            if (string.Equals(path, targetPath, StringComparison.Ordinal))
                continue;
            Assert.Equal(expectedBytes, await _fs.ReadFileBytesAsync(path));
        }
    }

    [Fact]
    public async Task ApplyForNewLifeAsync_FriendlyGuardianCreatesCorrectionsAndSpendsAbodePower()
    {
        await WriteRawAsync(ScenarioCoreService.ManifestPath, """
        {
          "scenarioCoreAssertions": [
            { "assertionId": "core_role", "category": "role_status", "value": "Игрок начинает королём", "explicit": true, "source": "structured_field" }
          ],
          "candidateAssertions": [],
          "openCorrectionSlots": [
            { "slotId": "slot_protection", "slotType": "protection_or_omen", "maxSeverity": "medium", "allowsFriendly": true, "allowsHostile": true, "sourceAssertionId": "core_role" },
            { "slotId": "slot_ally", "slotType": "ally_thread", "maxSeverity": "medium", "allowsFriendly": true, "allowsHostile": true, "sourceAssertionId": "core_role" }
          ]
        }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardians": [
            {
              "guardianId": "guard_test_azalia",
              "canonicalName": "Азалия",
              "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Азалия",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 95, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 80, "tier": "Сияющая", "lastUpdatedAt": "2026-03-23T00:00:00Z", "history": [] },
              "gachaSystem": { "chargesPerReturn": 3, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ],
          "activeGuardian": {
            "guardianId": "guard_test_azalia",
            "canonicalName": "Азалия",
            "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
            "manifestation": {
              "currentDisplayName": "Азалия",
              "formFlexibility": "selective",
              "currentPresentationStyle": "feminine",
              "currentPronouns": "она/её",
              "appearanceDescription": "Тестовая форма."
            },
            "manifestationHistory": [],
            "relationshipData": { "currentReputation": 95, "reputationHistory": [], "lastInteraction": null },
            "abodePower": { "currentPower": 80, "tier": "Сияющая", "lastUpdatedAt": "2026-03-23T00:00:00Z", "history": [] },
            "gachaSystem": { "chargesPerReturn": 3, "chargesUsedThisReturn": 0, "gachaHistory": [] }
          }
        }
        """);

        await _guardianCorrectionService.ApplyForNewLifeAsync(2, turnNumber: 12);
        var state = await _guardianCorrectionService.ReadAsync();
        var guardiansJson = await _fs.ReadFileAsync("game_state/meta/guardians.json");

        Assert.NotNull(state);
        Assert.Equal("friendly", state!.Intent);
        Assert.NotEmpty(state.Corrections);
        Assert.True(state.PowerAfter < state.PowerBefore);
        Assert.NotNull(guardiansJson);
        Assert.Contains($"\"currentPower\": {state.PowerAfter}", guardiansJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ApplyForNewLifeAsync_HostileGuardianCreatesAtLeastOneHostileCorrection()
    {
        await WriteRawAsync(ScenarioCoreService.ManifestPath, """
        {
          "scenarioCoreAssertions": [
            { "assertionId": "core_role", "category": "role_status", "value": "Игрок начинает королём", "explicit": true, "source": "structured_field" }
          ],
          "candidateAssertions": [],
          "openCorrectionSlots": [
            { "slotId": "slot_rival", "slotType": "rival_thread", "maxSeverity": "strong", "allowsFriendly": true, "allowsHostile": true, "sourceAssertionId": "core_role" }
          ]
        }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardians": [
            {
              "guardianId": "guard_test_varak",
              "canonicalName": "Варак",
              "nameVariants": { "default": "Варак", "feminine": null, "masculine": "Варак", "neutral": null },
              "manifestation": {
                "currentDisplayName": "Варак",
                "formFlexibility": "fixed",
                "currentPresentationStyle": "masculine",
                "currentPronouns": "он/его",
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": -80, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 82, "tier": "Сияющая", "lastUpdatedAt": "2026-03-23T00:00:00Z", "history": [] },
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ],
          "activeGuardian": {
            "guardianId": "guard_test_varak",
            "canonicalName": "Варак",
            "nameVariants": { "default": "Варак", "feminine": null, "masculine": "Варак", "neutral": null },
            "manifestation": {
              "currentDisplayName": "Варак",
              "formFlexibility": "fixed",
              "currentPresentationStyle": "masculine",
              "currentPronouns": "он/его",
              "appearanceDescription": "Тестовая форма."
            },
            "manifestationHistory": [],
            "relationshipData": { "currentReputation": -80, "reputationHistory": [], "lastInteraction": null },
            "abodePower": { "currentPower": 82, "tier": "Сияющая", "lastUpdatedAt": "2026-03-23T00:00:00Z", "history": [] },
            "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
          }
        }
        """);

        await _guardianCorrectionService.ApplyForNewLifeAsync(3, turnNumber: 13);
        var state = await _guardianCorrectionService.ReadAsync();

        Assert.NotNull(state);
        Assert.Equal("hostile", state!.Intent);
        Assert.Contains(state.Corrections, correction => correction.Intent == "hostile");
        Assert.Contains(state.Corrections, correction => correction.SlotType == "rival_thread");
    }

    [Fact]
    public async Task ApplyForNewLifeAsync_RivalOffensiveClaimant_CanContestCorrectionSlots()
    {
        await WriteRawAsync(ScenarioCoreService.ManifestPath, """
        {
          "scenarioCoreAssertions": [
            { "assertionId": "core_role", "category": "role_status", "value": "Игрок начинает королём", "explicit": true, "source": "structured_field" }
          ],
          "candidateAssertions": [],
          "openCorrectionSlots": [
            { "slotId": "slot_rival", "slotType": "rival_thread", "maxSeverity": "strong", "allowsFriendly": true, "allowsHostile": true, "sourceAssertionId": "core_role" },
            { "slotId": "slot_debt", "slotType": "debt_or_oath", "maxSeverity": "medium", "allowsFriendly": false, "allowsHostile": true, "sourceAssertionId": "core_role" }
          ]
        }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardians": [
            {
              "guardianId": "guard_test_active",
              "canonicalName": "Варак",
              "nameVariants": { "default": "Варак", "feminine": null, "masculine": "Варак", "neutral": null },
              "manifestation": {
                "currentDisplayName": "Варак",
                "formFlexibility": "fixed",
                "currentPresentationStyle": "masculine",
                "currentPronouns": "он/его",
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": -80, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 82, "tier": "Сияющая", "lastUpdatedAt": "2026-03-23T00:00:00Z", "history": [] },
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            },
            {
              "guardianId": "guard_test_rival",
              "canonicalName": "Нерис",
              "nameVariants": { "default": "Нерис", "feminine": "Нерис", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Нерис",
                "formFlexibility": "adaptive",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 10, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 70, "tier": "Могущественная", "lastUpdatedAt": "2026-03-23T00:00:00Z", "history": [] },
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ],
          "activeGuardian": {
            "guardianId": "guard_test_active",
            "canonicalName": "Варак",
            "nameVariants": { "default": "Варак", "feminine": null, "masculine": "Варак", "neutral": null },
            "manifestation": {
              "currentDisplayName": "Варак",
              "formFlexibility": "fixed",
              "currentPresentationStyle": "masculine",
              "currentPronouns": "он/его",
              "appearanceDescription": "Тестовая форма."
            },
            "manifestationHistory": [],
            "relationshipData": { "currentReputation": -80, "reputationHistory": [], "lastInteraction": null },
            "abodePower": { "currentPower": 82, "tier": "Сияющая", "lastUpdatedAt": "2026-03-23T00:00:00Z", "history": [] },
            "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
          }
        }
        """);

        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "completedProjects": [
            {
              "guardianId": "guard_test_rival",
              "project": {
                "projectId": "proj_intrigue_001",
                "projectType": "offensive_intrigue",
                "projectTier": "major",
                "projectMode": "offensive",
                "projectName": "Чужая интрига",
                "targetGuardianId": "guard_test_active",
                "finalState": "Completed",
                "completionTurn": 10
              }
            }
          ]
        }
        """);

        await _guardianCorrectionService.ApplyForNewLifeAsync(5, turnNumber: 15);
        var state = await _guardianCorrectionService.ReadAsync();

        Assert.NotNull(state);
        Assert.True(state!.Claimants.Count >= 2);
        Assert.Contains(state.Claimants, claimant => claimant.GuardianId == "guard_test_rival");
        Assert.Contains(state.ContestedSlots, contest => contest.SlotId == "slot_rival" && contest.Candidates.Count >= 2);
        Assert.Contains(state.ResolutionOrder, step => step.Contains("slot_rival", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ApplyForNewLifeAsync_SoulPreparationBonuses_AggregateIntoClaimantBudget()
    {
        await WriteRawAsync(ScenarioCoreService.ManifestPath, """
        {
          "scenarioCoreAssertions": [
            { "assertionId": "core_role", "category": "role_status", "value": "Игрок начинает королём", "explicit": true, "source": "structured_field" }
          ],
          "candidateAssertions": [],
          "openCorrectionSlots": [
            { "slotId": "slot_protection", "slotType": "protection_or_omen", "maxSeverity": "medium", "allowsFriendly": true, "allowsHostile": true, "sourceAssertionId": "core_role" }
          ]
        }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardians": [
            {
              "guardianId": "guard_test_azalia",
              "canonicalName": "Азалия",
              "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Азалия",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 95, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 80, "tier": "Сияющая", "lastUpdatedAt": "2026-03-23T00:00:00Z", "history": [] },
              "gachaSystem": { "chargesPerReturn": 3, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ],
          "activeGuardian": {
            "guardianId": "guard_test_azalia",
            "canonicalName": "Азалия",
            "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
            "manifestation": {
              "currentDisplayName": "Азалия",
              "formFlexibility": "selective",
              "currentPresentationStyle": "feminine",
              "currentPronouns": "она/её",
              "appearanceDescription": "Тестовая форма."
            },
            "manifestationHistory": [],
            "relationshipData": { "currentReputation": 95, "reputationHistory": [], "lastInteraction": null },
            "abodePower": { "currentPower": 80, "tier": "Сияющая", "lastUpdatedAt": "2026-03-23T00:00:00Z", "history": [] },
            "gachaSystem": { "chargesPerReturn": 3, "chargesUsedThisReturn": 0, "gachaHistory": [] }
          }
        }
        """);

        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "completedProjects": [
            {
              "guardianId": "guard_test_azalia",
              "project": {
                "projectId": "prep_major",
                "projectType": "soul_preparation",
                "projectTier": "major",
                "projectMode": "supportive",
                "projectName": "Подготовка пути",
                "finalState": "Completed",
                "completionTurn": 10,
                "projectOutcomeAudit": {
                  "preparationBudgetPoints": 2,
                  "preparationClaimPriorityBonus": 1
                }
              }
            },
            {
              "guardianId": "guard_test_azalia",
              "project": {
                "projectId": "prep_minor",
                "projectType": "soul_preparation",
                "projectTier": "minor",
                "projectMode": "supportive",
                "projectName": "Тихая настройка",
                "finalState": "Completed",
                "completionTurn": 11,
                "projectOutcomeAudit": {
                  "preparationBudgetPoints": 1,
                  "preparationClaimPriorityBonus": 1
                }
              }
            }
          ]
        }
        """);

        await _guardianCorrectionService.ApplyForNewLifeAsync(6, turnNumber: 16);
        var state = await _guardianCorrectionService.ReadAsync();

        Assert.NotNull(state);
        var activeClaimant = Assert.Single(state!.Claimants, claimant => claimant.GuardianId == "guard_test_azalia");
        Assert.Equal(3, activeClaimant.PreparationBudgetPoints);
        Assert.Equal(7, activeClaimant.BaseBudgetPoints + activeClaimant.PreparationBudgetPoints);
    }

    [Fact]
    public async Task ApplyForNewLifeAsync_SoulPreparationBonuses_AreConsumedAfterFirstTargetLife()
    {
        await WriteRawAsync(ScenarioCoreService.ManifestPath, """
        {
          "scenarioCoreAssertions": [
            { "assertionId": "core_role", "category": "role_status", "value": "Игрок начинает королём", "explicit": true, "source": "structured_field" }
          ],
          "candidateAssertions": [],
          "openCorrectionSlots": [
            { "slotId": "slot_protection", "slotType": "protection_or_omen", "maxSeverity": "medium", "allowsFriendly": true, "allowsHostile": true, "sourceAssertionId": "core_role" }
          ]
        }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardians": [
            {
              "guardianId": "guard_test_azalia",
              "canonicalName": "Азалия",
              "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Азалия",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 95, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 80, "tier": "Сияющая", "lastUpdatedAt": "2026-03-23T00:00:00Z", "history": [] },
              "gachaSystem": { "chargesPerReturn": 3, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ],
          "activeGuardian": {
            "guardianId": "guard_test_azalia",
            "canonicalName": "Азалия",
            "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
            "manifestation": {
              "currentDisplayName": "Азалия",
              "formFlexibility": "selective",
              "currentPresentationStyle": "feminine",
              "currentPronouns": "она/её",
              "appearanceDescription": "Тестовая форма."
            },
            "manifestationHistory": [],
            "relationshipData": { "currentReputation": 95, "reputationHistory": [], "lastInteraction": null },
            "abodePower": { "currentPower": 80, "tier": "Сияющая", "lastUpdatedAt": "2026-03-23T00:00:00Z", "history": [] },
            "gachaSystem": { "chargesPerReturn": 3, "chargesUsedThisReturn": 0, "gachaHistory": [] }
          }
        }
        """);

        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "completedProjects": [
            {
              "guardianId": "guard_test_azalia",
              "project": {
                "projectId": "prep_major",
                "projectType": "soul_preparation",
                "projectTier": "major",
                "projectMode": "supportive",
                "projectName": "Подготовка пути",
                "finalState": "Completed",
                "completionTurn": 10,
                "projectOutcomeAudit": {
                  "preparationBudgetPoints": 2,
                  "preparationClaimPriorityBonus": 1
                },
                "effectState": {
                  "targetIncarnation": 6,
                  "preparationBudgetPointsGranted": 2,
                  "preparationBudgetPointsSpent": 0,
                  "preparationClaimPriorityBonusGranted": 1,
                  "consumedAtLifeStart": false
                }
              }
            }
          ]
        }
        """);

        await _guardianCorrectionService.ApplyForNewLifeAsync(6, turnNumber: 16);
        var trackerAfterFirst = await _fs.ReadFileAsync(GuardianProjectState.TrackerPath);
        Assert.NotNull(trackerAfterFirst);
        Assert.Contains("\"consumedAtLifeStart\": true", trackerAfterFirst, StringComparison.Ordinal);

        await _guardianCorrectionService.ApplyForNewLifeAsync(7, turnNumber: 17);
        var secondState = await _guardianCorrectionService.ReadAsync();
        var secondClaimant = Assert.Single(secondState!.Claimants, claimant => claimant.GuardianId == "guard_test_azalia");
        Assert.Equal(0, secondClaimant.PreparationBudgetPoints);
    }

    [Fact]
    public async Task BuildSystemReminderFragmentAsync_NamedMortalRealm_ReturnsReminder()
    {
        await WriteRawAsync(GuardianCorrectionService.StatePath, """
        {
          "lifeIncarnation": 3,
          "appliedAt": "2026-03-27T00:00:00Z",
          "guardianId": "guard_test_azalia",
          "guardianName": "Азалия",
          "intent": "friendly",
          "reputationAtApplication": 90,
          "powerBefore": 70,
          "powerAfter": 63,
          "baseBudgetPoints": 2,
          "remainingBudgetPoints": 0,
          "totalAbodePowerSpent": 7,
          "summary": "Азалия мягко корректирует старт.",
          "scenarioCoreSnapshot": {
            "scenarioCoreAssertions": [],
            "openCorrectionSlots": []
          },
          "claimants": [],
          "contestedSlots": [],
          "resolutionOrder": [],
          "corrections": []
        }
        """);

        var reminder = await _guardianCorrectionService.BuildSystemReminderFragmentAsync("Неон-Сити");

        Assert.NotNull(reminder);
        Assert.Contains("GUARDIAN CORRECTIONS FOR THIS LIFE", reminder, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ApplyForNewLifeAsync_MutualNonHostileCoalitionTrace_StrengthensActivePatronClaim()
    {
        await WriteRawAsync(ScenarioCoreService.ManifestPath, """
        {
          "scenarioCoreAssertions": [
            { "assertionId": "core_role", "category": "role_status", "value": "Игрок начинает королём", "explicit": true, "source": "structured_field" }
          ],
          "candidateAssertions": [],
          "openCorrectionSlots": [
            { "slotId": "slot_rival", "slotType": "rival_thread", "maxSeverity": "strong", "allowsFriendly": true, "allowsHostile": true, "sourceAssertionId": "core_role" }
          ]
        }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardians": [
            {
              "guardianId": "guard_test_active",
              "canonicalName": "Азалия",
              "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Азалия",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 95, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 82, "tier": "Сияющая", "lastUpdatedAt": "2026-03-23T00:00:00Z", "history": [] },
              "guardianRelationships": [
                { "targetGuardianId": "guard_test_rival", "attitudeScore": -90, "attitudeTier": "enemy", "reason": "Open rivalry", "lastChangedAt": null },
                { "targetGuardianId": "guard_test_support", "attitudeScore": 5, "attitudeTier": "neutral", "reason": "Measured respect", "lastChangedAt": null }
              ],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            },
            {
              "guardianId": "guard_test_rival",
              "canonicalName": "Варак",
              "nameVariants": { "default": "Варак", "feminine": null, "masculine": "Варак", "neutral": null },
              "manifestation": {
                "currentDisplayName": "Варак",
                "formFlexibility": "fixed",
                "currentPresentationStyle": "masculine",
                "currentPronouns": "он/его",
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": -85, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 74, "tier": "Могущественная", "lastUpdatedAt": "2026-03-23T00:00:00Z", "history": [] },
              "guardianRelationships": [
                { "targetGuardianId": "guard_test_active", "attitudeScore": -90, "attitudeTier": "enemy", "reason": "Open rivalry", "lastChangedAt": null },
                { "targetGuardianId": "guard_test_support", "attitudeScore": -30, "attitudeTier": "competitive", "reason": "Cold distance", "lastChangedAt": null }
              ],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            },
            {
              "guardianId": "guard_test_support",
              "canonicalName": "Нерис",
              "nameVariants": { "default": "Нерис", "feminine": "Нерис", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Нерис",
                "formFlexibility": "adaptive",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 10, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 68, "tier": "Могущественная", "lastUpdatedAt": "2026-03-23T00:00:00Z", "history": [] },
              "guardianRelationships": [
                { "targetGuardianId": "guard_test_active", "attitudeScore": 0, "attitudeTier": "neutral", "reason": "Measured respect", "lastChangedAt": null },
                { "targetGuardianId": "guard_test_rival", "attitudeScore": -90, "attitudeTier": "enemy", "reason": "Shared enemy", "lastChangedAt": null }
              ],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ],
          "activeGuardian": {
            "guardianId": "guard_test_active",
            "canonicalName": "Азалия",
            "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
            "manifestation": {
              "currentDisplayName": "Азалия",
              "formFlexibility": "selective",
              "currentPresentationStyle": "feminine",
              "currentPronouns": "она/её",
              "appearanceDescription": "Тестовая форма."
            },
            "manifestationHistory": [],
            "relationshipData": { "currentReputation": 95, "reputationHistory": [], "lastInteraction": null },
            "abodePower": { "currentPower": 82, "tier": "Сияющая", "lastUpdatedAt": "2026-03-23T00:00:00Z", "history": [] },
            "guardianRelationships": [
              { "targetGuardianId": "guard_test_rival", "attitudeScore": -90, "attitudeTier": "enemy", "reason": "Open rivalry", "lastChangedAt": null },
              { "targetGuardianId": "guard_test_support", "attitudeScore": 5, "attitudeTier": "neutral", "reason": "Measured respect", "lastChangedAt": null }
            ],
            "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
          }
        }
        """);

        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [
            {
              "guardianId": "guard_test_support",
              "project": {
                "projectId": "proj_support_trace",
                "projectType": "offensive_intrigue",
                "projectTier": "minor",
                "projectMode": "offensive",
                "projectName": "Скоординированное давление",
                "targetGuardianId": "guard_test_rival",
                "activeState": "Coordinating against the shared enemy",
                "totalWork": 12,
                "workDone": 4,
                "totalStages": 2,
                "currentStage": 1,
                "pressure": 4,
                "stability": 70
              }
            }
          ],
          "completedProjects": [
            {
              "guardianId": "guard_test_rival",
              "project": {
                "projectId": "proj_intrigue_001",
                "projectType": "offensive_intrigue",
                "projectTier": "major",
                "projectMode": "offensive",
                "projectName": "Чужая интрига",
                "targetGuardianId": "guard_test_active",
                "finalState": "Completed",
                "completionTurn": 10
              }
            }
          ]
        }
        """);

        await _guardianCorrectionService.ApplyForNewLifeAsync(5, turnNumber: 25);
        var state = await _guardianCorrectionService.ReadAsync();

        Assert.NotNull(state);
        var activeClaimant = Assert.Single(state!.Claimants, claimant => claimant.GuardianId == "guard_test_active");
        Assert.Contains("coalition support +1", activeClaimant.SourceSummary, StringComparison.Ordinal);
        Assert.True(activeClaimant.ClaimStrengthBase > AbodePowerRules.GetCorrectionClaimPowerBand(activeClaimant.CurrentPower));
    }

    [Fact]
    public async Task ApplyForNewLifeAsync_WithoutCurrentCoalitionTrace_DoesNotGrantActivePatronSupportBonus()
    {
        await WriteRawAsync(ScenarioCoreService.ManifestPath, """
        {
          "scenarioCoreAssertions": [
            { "assertionId": "core_role", "category": "role_status", "value": "Игрок начинает королём", "explicit": true, "source": "structured_field" }
          ],
          "candidateAssertions": [],
          "openCorrectionSlots": [
            { "slotId": "slot_rival", "slotType": "rival_thread", "maxSeverity": "strong", "allowsFriendly": true, "allowsHostile": true, "sourceAssertionId": "core_role" }
          ]
        }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardians": [
            {
              "guardianId": "guard_test_active",
              "canonicalName": "Азалия",
              "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Азалия",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 95, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 82, "tier": "Сияющая", "lastUpdatedAt": "2026-03-23T00:00:00Z", "history": [] },
              "guardianRelationships": [
                { "targetGuardianId": "guard_test_rival", "attitudeScore": -90, "attitudeTier": "enemy", "reason": "Open rivalry", "lastChangedAt": null },
                { "targetGuardianId": "guard_test_support", "attitudeScore": 55, "attitudeTier": "ally", "reason": "Trusted ally", "lastChangedAt": null }
              ],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            },
            {
              "guardianId": "guard_test_rival",
              "canonicalName": "Варак",
              "nameVariants": { "default": "Варак", "feminine": null, "masculine": "Варак", "neutral": null },
              "manifestation": {
                "currentDisplayName": "Варак",
                "formFlexibility": "fixed",
                "currentPresentationStyle": "masculine",
                "currentPronouns": "он/его",
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": -85, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 74, "tier": "Могущественная", "lastUpdatedAt": "2026-03-23T00:00:00Z", "history": [] },
              "guardianRelationships": [
                { "targetGuardianId": "guard_test_active", "attitudeScore": -90, "attitudeTier": "enemy", "reason": "Open rivalry", "lastChangedAt": null },
                { "targetGuardianId": "guard_test_support", "attitudeScore": -30, "attitudeTier": "competitive", "reason": "Cold distance", "lastChangedAt": null }
              ],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            },
            {
              "guardianId": "guard_test_support",
              "canonicalName": "Нерис",
              "nameVariants": { "default": "Нерис", "feminine": "Нерис", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Нерис",
                "formFlexibility": "adaptive",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 10, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 68, "tier": "Могущественная", "lastUpdatedAt": "2026-03-23T00:00:00Z", "history": [] },
              "guardianRelationships": [
                { "targetGuardianId": "guard_test_active", "attitudeScore": 60, "attitudeTier": "ally", "reason": "Support pact", "lastChangedAt": null },
                { "targetGuardianId": "guard_test_rival", "attitudeScore": -90, "attitudeTier": "enemy", "reason": "Shared enemy", "lastChangedAt": null }
              ],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ],
          "activeGuardian": {
            "guardianId": "guard_test_active",
            "canonicalName": "Азалия",
            "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
            "manifestation": {
              "currentDisplayName": "Азалия",
              "formFlexibility": "selective",
              "currentPresentationStyle": "feminine",
              "currentPronouns": "она/её",
              "appearanceDescription": "Тестовая форма."
            },
            "manifestationHistory": [],
            "relationshipData": { "currentReputation": 95, "reputationHistory": [], "lastInteraction": null },
            "abodePower": { "currentPower": 82, "tier": "Сияющая", "lastUpdatedAt": "2026-03-23T00:00:00Z", "history": [] },
            "guardianRelationships": [
              { "targetGuardianId": "guard_test_rival", "attitudeScore": -90, "attitudeTier": "enemy", "reason": "Open rivalry", "lastChangedAt": null },
              { "targetGuardianId": "guard_test_support", "attitudeScore": 55, "attitudeTier": "ally", "reason": "Trusted ally", "lastChangedAt": null }
            ],
            "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
          }
        }
        """);

        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "completedProjects": [
            {
              "guardianId": "guard_test_rival",
              "project": {
                "projectId": "proj_intrigue_001",
                "projectType": "offensive_intrigue",
                "projectTier": "major",
                "projectMode": "offensive",
                "projectName": "Чужая интрига",
                "targetGuardianId": "guard_test_active",
                "finalState": "Completed",
                "completionTurn": 10
              }
            }
          ]
        }
        """);

        await _guardianCorrectionService.ApplyForNewLifeAsync(5, turnNumber: 35);
        var state = await _guardianCorrectionService.ReadAsync();

        Assert.NotNull(state);
        var activeClaimant = Assert.Single(state!.Claimants, claimant => claimant.GuardianId == "guard_test_active");
        Assert.DoesNotContain("coalition support +1", activeClaimant.SourceSummary, StringComparison.Ordinal);
        Assert.Equal(AbodePowerRules.GetCorrectionClaimPowerBand(activeClaimant.CurrentPower) + 1, activeClaimant.ClaimStrengthBase);
    }

    [Fact]
    public async Task ApplyForNewLifeAsync_OneWayFriendlyButHostileReverseRelation_DoesNotGrantCoalitionSupportBonus()
    {
        await WriteRawAsync(ScenarioCoreService.ManifestPath, """
        {
          "scenarioCoreAssertions": [
            { "assertionId": "core_role", "category": "role_status", "value": "Игрок начинает королём", "explicit": true, "source": "structured_field" }
          ],
          "candidateAssertions": [],
          "openCorrectionSlots": [
            { "slotId": "slot_rival", "slotType": "rival_thread", "maxSeverity": "strong", "allowsFriendly": true, "allowsHostile": true, "sourceAssertionId": "core_role" }
          ]
        }
        """);

        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardians": [
            {
              "guardianId": "guard_test_active",
              "canonicalName": "Азалия",
              "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Азалия",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 95, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 82, "tier": "Сияющая", "lastUpdatedAt": "2026-03-23T00:00:00Z", "history": [] },
              "guardianRelationships": [
                { "targetGuardianId": "guard_test_rival", "attitudeScore": -90, "attitudeTier": "enemy", "reason": "Open rivalry", "lastChangedAt": null },
                { "targetGuardianId": "guard_test_support", "attitudeScore": -90, "attitudeTier": "enemy", "reason": "Broken accord", "lastChangedAt": null }
              ],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            },
            {
              "guardianId": "guard_test_rival",
              "canonicalName": "Варак",
              "nameVariants": { "default": "Варак", "feminine": null, "masculine": "Варак", "neutral": null },
              "manifestation": {
                "currentDisplayName": "Варак",
                "formFlexibility": "fixed",
                "currentPresentationStyle": "masculine",
                "currentPronouns": "он/его",
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": -85, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 74, "tier": "Могущественная", "lastUpdatedAt": "2026-03-23T00:00:00Z", "history": [] },
              "guardianRelationships": [
                { "targetGuardianId": "guard_test_active", "attitudeScore": -90, "attitudeTier": "enemy", "reason": "Open rivalry", "lastChangedAt": null },
                { "targetGuardianId": "guard_test_support", "attitudeScore": -30, "attitudeTier": "competitive", "reason": "Cold distance", "lastChangedAt": null }
              ],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            },
            {
              "guardianId": "guard_test_support",
              "canonicalName": "Нерис",
              "nameVariants": { "default": "Нерис", "feminine": "Нерис", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Нерис",
                "formFlexibility": "adaptive",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 10, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 68, "tier": "Могущественная", "lastUpdatedAt": "2026-03-23T00:00:00Z", "history": [] },
              "guardianRelationships": [
                { "targetGuardianId": "guard_test_active", "attitudeScore": 60, "attitudeTier": "ally", "reason": "Old loyalty", "lastChangedAt": null },
                { "targetGuardianId": "guard_test_rival", "attitudeScore": -90, "attitudeTier": "enemy", "reason": "Shared enemy", "lastChangedAt": null }
              ],
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ],
          "activeGuardian": {
            "guardianId": "guard_test_active",
            "canonicalName": "Азалия",
            "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
            "manifestation": {
              "currentDisplayName": "Азалия",
              "formFlexibility": "selective",
              "currentPresentationStyle": "feminine",
              "currentPronouns": "она/её",
              "appearanceDescription": "Тестовая форма."
            },
            "manifestationHistory": [],
            "relationshipData": { "currentReputation": 95, "reputationHistory": [], "lastInteraction": null },
            "abodePower": { "currentPower": 82, "tier": "Сияющая", "lastUpdatedAt": "2026-03-23T00:00:00Z", "history": [] },
            "guardianRelationships": [
              { "targetGuardianId": "guard_test_rival", "attitudeScore": -90, "attitudeTier": "enemy", "reason": "Open rivalry", "lastChangedAt": null },
              { "targetGuardianId": "guard_test_support", "attitudeScore": -90, "attitudeTier": "enemy", "reason": "Broken accord", "lastChangedAt": null }
            ],
            "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
          }
        }
        """);

        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "activeProjects": [
            {
              "guardianId": "guard_test_support",
              "project": {
                "projectId": "proj_support_trace",
                "projectType": "offensive_intrigue",
                "projectTier": "minor",
                "projectMode": "offensive",
                "projectName": "Запоздалая помощь",
                "targetGuardianId": "guard_test_rival",
                "activeState": "Attempting to coordinate",
                "totalWork": 12,
                "workDone": 4,
                "totalStages": 2,
                "currentStage": 1,
                "pressure": 4,
                "stability": 70
              }
            }
          ],
          "completedProjects": [
            {
              "guardianId": "guard_test_rival",
              "project": {
                "projectId": "proj_intrigue_001",
                "projectType": "offensive_intrigue",
                "projectTier": "major",
                "projectMode": "offensive",
                "projectName": "Чужая интрига",
                "targetGuardianId": "guard_test_active",
                "finalState": "Completed",
                "completionTurn": 10
              }
            }
          ]
        }
        """);

        await _guardianCorrectionService.ApplyForNewLifeAsync(5, turnNumber: 45);
        var state = await _guardianCorrectionService.ReadAsync();

        Assert.NotNull(state);
        var activeClaimant = Assert.Single(state!.Claimants, claimant => claimant.GuardianId == "guard_test_active");
        Assert.DoesNotContain("coalition support +1", activeClaimant.SourceSummary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AppendJournalEntriesAsync_ConcurrentManagersPreserveBothEvents()
    {
        await WriteRawAsync(GuardianPowerEventState.JournalPath, """{ "entries": [] }""");
        var firstReadEntered = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirstRead = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var secondWriterContended = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var paused = 0;
        var mainContentions = 0;
        var canonicalContentions = 0;
        var firstFileSystem = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance,
            new FileSystemManagerHooks
            {
                AfterCanonicalReadInitialValidationAsync = async path =>
                {
                    if (!string.Equals(
                            path,
                            GuardianPowerEventState.JournalPath,
                            StringComparison.OrdinalIgnoreCase) ||
                        Interlocked.Exchange(ref paused, 1) != 0)
                    {
                        return;
                    }

                    firstReadEntered.TrySetResult(true);
                    await releaseFirstRead.Task;
                }
            });
        var secondFileSystem = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance,
            new FileSystemManagerHooks
            {
                MainOwnerLockContendedAsync = () =>
                {
                    Interlocked.Increment(ref mainContentions);
                    secondWriterContended.TrySetResult(true);
                    return Task.CompletedTask;
                },
                CanonicalWriteLockContendedAsync = () =>
                {
                    Interlocked.Increment(ref canonicalContentions);
                    return Task.CompletedTask;
                }
            });

        var firstTask = GuardianPowerEventState.AppendJournalEntriesAsync(
            firstFileSystem,
            new[] { new JsonObject { ["eventId"] = "guardian_event_first" } });
        Task? secondTask = null;
        try
        {
            await Task.WhenAny(firstTask, firstReadEntered.Task).WaitAsync(TimeSpan.FromSeconds(30));
            if (!firstReadEntered.Task.IsCompleted) await firstTask;
            Assert.True(firstReadEntered.Task.IsCompletedSuccessfully);
            secondTask = GuardianPowerEventState.AppendJournalEntriesAsync(
                secondFileSystem,
                new[] { new JsonObject { ["eventId"] = "guardian_event_second" } });
            await Task.WhenAny(secondTask, secondWriterContended.Task).WaitAsync(TimeSpan.FromSeconds(30));
            Assert.True(secondWriterContended.Task.IsCompletedSuccessfully);
            Assert.True(mainContentions > 0);
            Assert.Equal(0, canonicalContentions);
            Assert.False(secondTask.IsCompleted);
            releaseFirstRead.TrySetResult(true);

            await Task.WhenAll(firstTask, secondTask).WaitAsync(TimeSpan.FromSeconds(30));
            var journal = JsonNode.Parse(
                Assert.IsType<string>(await _fs.ReadFileAsync(GuardianPowerEventState.JournalPath)))!.AsObject();
            Assert.Equal(
                new[] { "guardian_event_first", "guardian_event_second" },
                journal["entries"]!.AsArray().OfType<JsonObject>()
                    .Select(entry => entry["eventId"]!.GetValue<string>())
                    .OrderBy(static eventId => eventId, StringComparer.Ordinal));
        }
        finally
        {
            releaseFirstRead.TrySetResult(true);
            await Record.ExceptionAsync(() => Task.WhenAll(firstTask, secondTask ?? Task.CompletedTask));
            _output.WriteLine(JsonSerializer.Serialize(new
            {
                kind = "f18-admission-contention", method = nameof(AppendJournalEntriesAsync_ConcurrentManagersPreserveBothEvents),
                root = _rootPath, paused, mainContentions, canonicalContentions,
                firstSettled = firstTask.IsCompleted, secondSettled = secondTask?.IsCompleted
            }));
        }
    }

    private async Task WriteRawAsync(string path, string content)
    {
        if (string.Equals(
                path,
                "game_state/meta/guardians.json",
                StringComparison.Ordinal))
        {
            var guardians = JsonNode.Parse(content)!.AsObject();
            var guardianProfiles = new JsonArray();
            var seenGuardianIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var guardian in Assert.IsType<JsonArray>(guardians["guardians"])
                         .OfType<JsonObject>())
            {
                var guardianId = guardian["guardianId"]!.GetValue<string>();
                NormalizeGuardianGachaFixture(guardian, guardianId);
                if (seenGuardianIds.Add(guardianId))
                {
                    guardianProfiles.Add(
                        AfterlifeActorMaterializationTestFixture.CreateCompleteProfile(
                            actorType: "guardian",
                            actorId: guardianId,
                            realm: "Chaos Sea",
                            materializedAtTurn: 1));
                }
            }
            if (guardians["activeGuardian"] is JsonObject activeGuardian)
            {
                var activeGuardianId = activeGuardian["guardianId"]!.GetValue<string>();
                NormalizeGuardianGachaFixture(activeGuardian, activeGuardianId);
                if (seenGuardianIds.Add(activeGuardianId))
                {
                    guardianProfiles.Add(
                        AfterlifeActorMaterializationTestFixture.CreateCompleteProfile(
                            actorType: "guardian",
                            actorId: activeGuardianId,
                            realm: "Chaos Sea",
                            materializedAtTurn: 1));
                }
            }

            await CanonicalResourceQuartetTestFixture.CommitFreshBootstrapAsync(
                _fs,
                new AfterlifeOwnerResourceAcceptedState(
                    Profiles: new JsonObject
                    {
                        [AfterlifeEntityProfileState.ProfilesProperty] = guardianProfiles
                    },
                    Guardians: guardians));
            return;
        }

        await _fs.WriteFileAtomicAsync(path, content);
    }

    private async Task SeedCorrectionInputAsync()
    {
        await WriteRawAsync(ScenarioCoreService.ManifestPath, """
        {
          "scenarioCoreAssertions": [
            { "assertionId": "core_role", "category": "role_status", "value": "Игрок начинает королём", "explicit": true, "source": "structured_field" }
          ],
          "candidateAssertions": [],
          "openCorrectionSlots": [
            { "slotId": "slot_protection", "slotType": "protection_or_omen", "maxSeverity": "medium", "allowsFriendly": true, "allowsHostile": true, "sourceAssertionId": "core_role" }
          ]
        }
        """);
        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardians": [
            {
              "guardianId": "guard_receipt_authority",
              "canonicalName": "Азалия",
              "nameVariants": { "default": "Азалия" },
              "manifestation": {
                "currentDisplayName": "Азалия",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 95, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 80, "tier": "Сияющая", "lastUpdatedAt": "2026-03-23T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "currentReturnCycleId": "chaos_return_receipt_authority", "gachaHistory": [] }
            }
          ],
          "activeGuardian": {
            "guardianId": "guard_receipt_authority",
            "canonicalName": "Азалия",
            "nameVariants": { "default": "Азалия" },
            "manifestation": {
              "currentDisplayName": "Азалия",
              "formFlexibility": "selective",
              "currentPresentationStyle": "feminine",
              "currentPronouns": "она/её",
              "appearanceDescription": "Тестовая форма."
            },
            "manifestationHistory": [],
            "relationshipData": { "currentReputation": 95, "reputationHistory": [], "lastInteraction": null },
            "abodePower": { "currentPower": 80, "tier": "Сияющая", "lastUpdatedAt": "2026-03-23T00:00:00Z", "history": [] },
            "guardianRelationships": [],
            "gachaSystem": { "currentReturnCycleId": "chaos_return_receipt_authority", "gachaHistory": [] }
          }
        }
        """);
    }

    private async Task SeedAppliedCorrectionAsync()
    {
        await SeedCorrectionInputAsync();
        await _guardianCorrectionService.ApplyForNewLifeAsync(2, turnNumber: 42);
    }

    private async Task SeedFailureInjectionFixtureAsync()
    {
        await SeedAppliedCorrectionAsync();
        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "completedProjects": [
            {
              "guardianId": "guard_receipt_authority",
              "project": {
                "projectId": "prep_failure_matrix",
                "projectType": "soul_preparation",
                "projectTier": "major",
                "projectMode": "supportive",
                "projectName": "Матрица атомарности",
                "finalState": "Completed",
                "completionTurn": 42,
                "projectOutcomeAudit": {
                  "preparationBudgetPoints": 2,
                  "preparationClaimPriorityBonus": 1
                },
                "effectState": {
                  "targetIncarnation": 3,
                  "preparationBudgetPointsGranted": 2,
                  "preparationBudgetPointsSpent": 0,
                  "preparationClaimPriorityBonusGranted": 1,
                  "hostilePriorityTokensGranted": 0,
                  "hostilePriorityTokensSpent": 0,
                  "consumedAtLifeStart": false
                }
              }
            }
          ]
        }
        """);
    }

    private async Task SeedDelimiterAmbiguousCorrectionFixtureAsync()
    {
        await WriteRawAsync(ScenarioCoreService.ManifestPath, """
        {
          "scenarioCoreAssertions": [
            { "assertionId": "core_role", "category": "role_status", "value": "Игрок начинает королём", "explicit": true, "source": "structured_field" }
          ],
          "candidateAssertions": [],
          "openCorrectionSlots": [
            { "slotId": "c", "slotType": "protection_or_omen", "maxSeverity": "medium", "allowsFriendly": true, "allowsHostile": false, "sourceAssertionId": "core_role" },
            { "slotId": "b_c", "slotType": "rival_thread", "maxSeverity": "medium", "allowsFriendly": false, "allowsHostile": true, "sourceAssertionId": "core_role" }
          ]
        }
        """);
        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardians": [
            {
              "guardianId": "guard_a_b",
              "canonicalName": "Азалия",
              "nameVariants": { "default": "Азалия" },
              "manifestation": {
                "currentDisplayName": "Азалия",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": 95, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 80, "tier": "Сияющая", "lastUpdatedAt": "2026-03-23T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "currentReturnCycleId": "chaos_return_guard_a_b", "gachaHistory": [] }
            },
            {
              "guardianId": "guard_a",
              "canonicalName": "Нерис",
              "nameVariants": { "default": "Нерис" },
              "manifestation": {
                "currentDisplayName": "Нерис",
                "formFlexibility": "adaptive",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": -80, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 80, "tier": "Сияющая", "lastUpdatedAt": "2026-03-23T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "currentReturnCycleId": "chaos_return_guard_a", "gachaHistory": [] }
            }
          ],
          "activeGuardian": {
            "guardianId": "guard_a_b",
            "canonicalName": "Азалия",
            "nameVariants": { "default": "Азалия" },
            "manifestation": {
              "currentDisplayName": "Азалия",
              "formFlexibility": "selective",
              "currentPresentationStyle": "feminine",
              "currentPronouns": "она/её",
              "appearanceDescription": "Тестовая форма."
            },
            "manifestationHistory": [],
            "relationshipData": { "currentReputation": 95, "reputationHistory": [], "lastInteraction": null },
            "abodePower": { "currentPower": 80, "tier": "Сияющая", "lastUpdatedAt": "2026-03-23T00:00:00Z", "history": [] },
            "guardianRelationships": [],
            "gachaSystem": { "currentReturnCycleId": "chaos_return_guard_a_b", "gachaHistory": [] }
          }
        }
        """);
        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "completedProjects": [
            {
              "guardianId": "guard_a",
              "project": {
                "projectId": "proj_delimiter_collision",
                "projectType": "offensive_intrigue",
                "projectTier": "major",
                "projectMode": "offensive",
                "projectName": "Коллизионная интрига",
                "targetGuardianId": "guard_a_b",
                "finalState": "Completed",
                "completionTurn": 43
              }
            }
          ]
        }
        """);
    }

    private async Task SeedContestedCorrectionInputAsync()
    {
        await WriteRawAsync(ScenarioCoreService.ManifestPath, """
        {
          "scenarioCoreAssertions": [
            { "assertionId": "core_role", "category": "role_status", "value": "Игрок начинает королём", "explicit": true, "source": "structured_field" }
          ],
          "candidateAssertions": [],
          "openCorrectionSlots": [
            { "slotId": "slot_contested_rival", "slotType": "rival_thread", "maxSeverity": "medium", "allowsFriendly": false, "allowsHostile": true, "sourceAssertionId": "core_role" }
          ]
        }
        """);
        await WriteRawAsync("game_state/meta/guardians.json", """
        {
          "guardians": [
            {
              "guardianId": "guard_selection_active",
              "canonicalName": "Варак",
              "nameVariants": { "default": "Варак" },
              "manifestation": {
                "currentDisplayName": "Варак",
                "formFlexibility": "fixed",
                "currentPresentationStyle": "masculine",
                "currentPronouns": "он/его",
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": -80, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 80, "tier": "Сияющая", "lastUpdatedAt": "2026-03-23T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "currentReturnCycleId": "chaos_return_guard_selection_active", "gachaHistory": [] }
            },
            {
              "guardianId": "guard_selection_rival",
              "canonicalName": "Нерис",
              "nameVariants": { "default": "Нерис" },
              "manifestation": {
                "currentDisplayName": "Нерис",
                "formFlexibility": "adaptive",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Тестовая форма."
              },
              "manifestationHistory": [],
              "relationshipData": { "currentReputation": -80, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 80, "tier": "Сияющая", "lastUpdatedAt": "2026-03-23T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "gachaSystem": { "currentReturnCycleId": "chaos_return_guard_selection_rival", "gachaHistory": [] }
            }
          ],
          "activeGuardian": {
            "guardianId": "guard_selection_active",
            "canonicalName": "Варак",
            "nameVariants": { "default": "Варак" },
            "manifestation": {
              "currentDisplayName": "Варак",
              "formFlexibility": "fixed",
              "currentPresentationStyle": "masculine",
              "currentPronouns": "он/его",
              "appearanceDescription": "Тестовая форма."
            },
            "manifestationHistory": [],
            "relationshipData": { "currentReputation": -80, "reputationHistory": [], "lastInteraction": null },
            "abodePower": { "currentPower": 80, "tier": "Сияющая", "lastUpdatedAt": "2026-03-23T00:00:00Z", "history": [] },
            "guardianRelationships": [],
            "gachaSystem": { "currentReturnCycleId": "chaos_return_guard_selection_active", "gachaHistory": [] }
          }
        }
        """);
        await WriteRawAsync(GuardianProjectState.TrackerPath, """
        {
          "completedProjects": [
            {
              "guardianId": "guard_selection_rival",
              "project": {
                "projectId": "proj_deterministic_selection",
                "projectType": "offensive_intrigue",
                "projectTier": "major",
                "projectMode": "offensive",
                "projectName": "Детерминированная интрига",
                "targetGuardianId": "guard_selection_active",
                "finalState": "Completed",
                "completionTurn": 43
              }
            }
          ]
        }
        """);
    }

    private static GuardianCorrectionService CreateCorrectionService(
        FileSystemManager fileSystem) =>
        new(
            fileSystem,
            new ScenarioCoreService(
                fileSystem,
                NullLogger<ScenarioCoreService>.Instance),
            NullLogger<GuardianCorrectionService>.Instance);

    private static bool BytesEqual(byte[]? left, byte[]? right) =>
        left == null
            ? right == null
            : right != null && left.AsSpan().SequenceEqual(right);

    private static string MutateReceipt(
        string receipt,
        Action<JsonObject> mutation)
    {
        var root = JsonNode.Parse(receipt)!.AsObject();
        mutation(root);
        return root.ToJsonString();
    }

    private static string MutateAndRefingerprintReceipt(
        string receipt,
        Action<JsonObject> mutation)
    {
        var root = JsonNode.Parse(receipt)!.AsObject();
        mutation(root);
        var typed = JsonSerializer.Deserialize<
            GuardianCorrectionService.GuardianCorrectionsState>(
                root.ToJsonString(),
                SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed)!;
        var canonicalRoot = JsonSerializer.SerializeToNode(
            typed,
            SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed)!.AsObject();
        canonicalRoot.Remove("receiptFingerprint");
        using var fingerprint = new ResourceFingerprintBuilder(
            "guardian-correction-receipt-v1");
        fingerprint.Append(canonicalRoot.ToJsonString(
            SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        root["receiptFingerprint"] = fingerprint.Build();
        return root.ToJsonString(
            SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed);
    }

    private static string MutateManifestAndRefingerprintReceipt(
        string receipt,
        Action<JsonArray> mutation)
    {
        var root = JsonNode.Parse(receipt)!.AsObject();
        JsonArray manifest;
        if (root["transactionAfterImagePaths"] is JsonArray existingManifest)
        {
            manifest = existingManifest;
        }
        else
        {
            manifest = new JsonArray(
                new[]
                {
                    GuardianProjectState.TrackerPath,
                    GuardianPowerEventState.JournalPath,
                    "game_state/meta/guardians.json",
                    ResourceMaterializationContract.StatePath,
                    ResourceMaterializationContract.HistoryPath,
                    CanonicalResourceOwnerAuthorityComposer.AuthorityPath
                }
                .OrderBy(static path => path, StringComparer.Ordinal)
                .Select(static path => (JsonNode)path)
                .ToArray());
            var rebuiltRoot = new JsonObject();
            foreach (var property in root)
            {
                rebuiltRoot[property.Key] = property.Value?.DeepClone();
                if (string.Equals(
                        property.Key,
                        "transactionAfterImageFingerprint",
                        StringComparison.Ordinal))
                {
                    rebuiltRoot["transactionAfterImagePaths"] = manifest;
                }
            }

            root = rebuiltRoot;
        }

        mutation(manifest);
        root.Remove("receiptFingerprint");
        using var fingerprint = new ResourceFingerprintBuilder(
            "guardian-correction-receipt-v1");
        fingerprint.Append(root.ToJsonString(
            SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        root["receiptFingerprint"] = fingerprint.Build();
        return root.ToJsonString(
            SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed);
    }

    private async Task<IReadOnlyDictionary<string, byte[]?>>
        CaptureCorrectionTransactionBytesAsync()
    {
        var paths = new[]
        {
            "game_state/meta/guardians.json",
            AfterlifeEntityProfileState.StatePath,
            GuardianProjectState.TrackerPath,
            GuardianPowerEventState.JournalPath,
            GuardianCorrectionService.StatePath,
            ResourceMaterializationContract.DefinitionsPath,
            ResourceMaterializationContract.StatePath,
            ResourceMaterializationContract.HistoryPath,
            CanonicalResourceOwnerAuthorityComposer.AuthorityPath
        };
        var result = new Dictionary<string, byte[]?>(StringComparer.Ordinal);
        foreach (var path in paths)
            result[path] = await _fs.ReadFileBytesAsync(path);
        return result;
    }

    private async Task RestoreCapturedPathAsync(string path, byte[]? bytes)
    {
        if (bytes == null)
        {
            _fs.DeleteFile(path);
            return;
        }

        await _fs.WriteFileAtomicBytesAsync(path, bytes);
    }

    private async Task<byte[]> MakeGuardianProfileRequireRealmNormalizationAsync()
    {
        var profiles = JsonNode.Parse(Assert.IsType<string>(
            await _fs.ReadFileAsync(AfterlifeEntityProfileState.StatePath)))!
            .AsObject();
        var profile = Assert.Single(
            profiles[AfterlifeEntityProfileState.ProfilesProperty]!
                .AsArray())!.AsObject();
        profile["realm"] = "Chaos Sea";
        var bindings = Assert.IsType<JsonArray>(
            profile[AfterlifeEntityProfileState.ResourceOwnerBindingsProperty]);
        Assert.NotEmpty(bindings);
        foreach (var binding in bindings.Select(static binding =>
                     binding!.AsObject()))
        {
            binding["realm"] = "Chaos Sea";
        }
        await _fs.WriteFileAtomicAsync(
            AfterlifeEntityProfileState.StatePath,
            profiles.ToJsonString(
                SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        return Assert.IsType<byte[]>(await _fs.ReadFileBytesAsync(
            AfterlifeEntityProfileState.StatePath));
    }

    private static void NormalizeGuardianGachaFixture(
        JsonObject guardian,
        string guardianId)
    {
        var gacha = Assert.IsType<JsonObject>(guardian["gachaSystem"]);
        gacha.Remove("chargesPerReturn");
        gacha.Remove("chargesUsedThisReturn");
        gacha["currentReturnCycleId"] = $"chaos_return_{guardianId}";
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
            // Ignore temp cleanup failures.
        }
    }
}
