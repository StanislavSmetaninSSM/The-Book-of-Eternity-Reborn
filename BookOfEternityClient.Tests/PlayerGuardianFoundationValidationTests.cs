using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class PlayerGuardianFoundationValidationTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly ValidationService _validator;

    public PlayerGuardianFoundationValidationTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-player-guardian-foundation-validation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
        _validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
    }

    [Fact]
    public async Task ValidateGameStateAsync_PendingFoundationWithoutNewGuardian_FailsResolution()
    {
        var request = CreateFoundationRequest();
        var preTurnSoul = CreateSoulState();
        var preTurnGuardians = CreateGuardiansRoot(CreateGuardian("guardian_old", "Азалия", "abode_old"), "guardian_old", "abode_old");

        await WriteJsonAsync(PlayerGuardianFoundationState.PendingRequestPath, request);
        await WriteJsonAsync("game_state/meta/soul_state.json", preTurnSoul);
        await WriteJsonAsync("game_state/meta/guardians.json", preTurnGuardians);
        await WriteJsonAsync(GuardianPowerEventState.JournalPath, new { entries = Array.Empty<object>() });
        await WriteChaosSeaLoreBootstrapAsync();
        await WriteJsonAsync("ready/turn_complete.json", new { status = "ok" });

        await WriteJsonAsync("game_state/control/pending_turn_snapshot/game_state/meta/soul_state.json", preTurnSoul);
        await WriteJsonAsync("game_state/control/pending_turn_snapshot/game_state/meta/guardians.json", preTurnGuardians);
        await WriteJsonAsync($"game_state/control/pending_turn_snapshot/{GuardianPowerEventState.JournalPath}", new { entries = Array.Empty<object>() });
        await WriteJsonAsync($"game_state/control/pending_turn_snapshot/{GuardianProjectState.TrackerPath}", new
        {
            activeProjects = Array.Empty<object>(),
            completedProjects = Array.Empty<object>(),
            temporaryProjectModifiers = Array.Empty<object>()
        });
        const string backupPath = "game_state/control/pending_turn_snapshot/pre_pending_player_guardian_foundation.json";
        await WriteJsonAsync(backupPath, request);
        await WritePendingTurnSnapshotManifestAsync(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [PlayerGuardianFoundationState.PendingRequestPath] = backupPath
        });

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue => string.Equals(issue.Code, "player_guardian_foundation_missing_guardian_resolution", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_PendingFoundationFromShiningSnapshot_FailsWrongRealm()
    {
        var request = CreateFoundationRequest();
        var preTurnSoul = CreateSoulState();
        preTurnSoul["currentRealm"] = "Shining Abode";
        var preTurnGuardians = CreateGuardiansRoot(CreateGuardian("guardian_old", "Азалия", "abode_old"), "guardian_old", "abode_old");

        await WriteJsonAsync(PlayerGuardianFoundationState.PendingRequestPath, request);
        await WriteJsonAsync("game_state/meta/soul_state.json", preTurnSoul);
        await WriteJsonAsync("game_state/meta/guardians.json", preTurnGuardians);
        await WriteJsonAsync(GuardianPowerEventState.JournalPath, new { entries = Array.Empty<object>() });
        await WriteChaosSeaLoreBootstrapAsync();
        await WriteJsonAsync("ready/turn_complete.json", new { status = "ok" });

        await WriteJsonAsync("game_state/control/pending_turn_snapshot/game_state/meta/soul_state.json", preTurnSoul);
        await WriteJsonAsync("game_state/control/pending_turn_snapshot/game_state/meta/guardians.json", preTurnGuardians);
        await WriteJsonAsync($"game_state/control/pending_turn_snapshot/{GuardianPowerEventState.JournalPath}", new { entries = Array.Empty<object>() });
        await WriteJsonAsync($"game_state/control/pending_turn_snapshot/{GuardianProjectState.TrackerPath}", new
        {
            activeProjects = Array.Empty<object>(),
            completedProjects = Array.Empty<object>(),
            temporaryProjectModifiers = Array.Empty<object>()
        });
        const string backupPath = "game_state/control/pending_turn_snapshot/pre_pending_player_guardian_foundation.json";
        await WriteJsonAsync(backupPath, request);
        await WritePendingTurnSnapshotManifestAsync(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [PlayerGuardianFoundationState.PendingRequestPath] = backupPath
        });

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue => string.Equals(issue.Code, "player_guardian_foundation_wrong_realm", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_FoundedGuardianWithoutSoulStatus_Fails()
    {
        await WriteSuccessfulFoundationResolutionAsync(includeSoulStatus: false);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue => string.Equals(issue.Code, "player_guardian_foundation_missing_soul_status", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_FormerPatronWithoutRoleTag_Fails()
    {
        await WriteSuccessfulFoundationResolutionAsync(includeFormerPatronRole: false);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue => string.Equals(issue.Code, "player_guardian_foundation_missing_former_patron_role", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_FoundedGuardianBelowSoulboundFloor_Fails()
    {
        await WriteSuccessfulFoundationResolutionAsync(foundedGuardianReputation: 180);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue => string.Equals(issue.Code, "player_guardian_foundation_loyalty_below_soulbound_floor", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_FoundationRootSurfacesMatchGuardianAuthority()
    {
        await WriteSuccessfulFoundationResolutionAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue => string.Equals(issue.Code, "guardian_materialized_state_outside_authority", StringComparison.OrdinalIgnoreCase));
        Assert.True(
            !issues.Any(issue => string.Equals(issue.Code, "player_guardian_foundation_missing_guardian_resolution", StringComparison.OrdinalIgnoreCase)),
            string.Join(Environment.NewLine, issues.Select(issue => $"{issue.Code}: {issue.Message} actual={issue.Actual}")));
    }

    [Fact]
    public async Task ValidateGameStateAsync_CreateSurfaceWithIdOnlyActiveGuardianMirror_IsAcceptedAsRecoverableMirror()
    {
        var preTurnSoul = CreateSoulState();
        var preTurnGuardians = new JsonObject
        {
            ["guardians"] = new JsonArray(),
            ["activeGuardian"] = null,
            ["chaosSeaNavigation"] = new JsonObject
            {
                ["currentAbodeId"] = null,
                ["discoveredAbodes"] = new JsonArray()
            }
        };
        var guardian = CreateGuardian("guardian_eira", "Эйра", "abode_eira", originType: "freeform", currentReputation: 10);
        var currentGuardians = CreateGuardiansRoot(guardian, "guardian_eira", "abode_eira");
        currentGuardians["UpdateGuardians"] = new JsonArray
        {
            new JsonObject
            {
                ["command"] = "create",
                ["data"] = guardian.DeepClone()
            }
        };
        currentGuardians["activeGuardian"] = new JsonObject
        {
            ["guardianId"] = "guardian_eira"
        };

        await WriteJsonAsync("game_state/meta/soul_state.json", preTurnSoul);
        await WriteJsonAsync("game_state/meta/guardians.json", currentGuardians);
        await WriteJsonAsync(GuardianPowerEventState.JournalPath, new { entries = Array.Empty<object>() });
        await WriteChaosSeaLoreBootstrapAsync();
        await WriteJsonAsync("ready/turn_complete.json", new { status = "ok" });

        await WriteJsonAsync("game_state/control/pending_turn_snapshot/game_state/meta/soul_state.json", preTurnSoul);
        await WriteJsonAsync("game_state/control/pending_turn_snapshot/game_state/meta/guardians.json", preTurnGuardians);
        await WriteJsonAsync($"game_state/control/pending_turn_snapshot/{GuardianPowerEventState.JournalPath}", new { entries = Array.Empty<object>() });
        await WriteJsonAsync($"game_state/control/pending_turn_snapshot/{GuardianProjectState.TrackerPath}", new
        {
            activeProjects = Array.Empty<object>(),
            completedProjects = Array.Empty<object>(),
            temporaryProjectModifiers = Array.Empty<object>()
        });
        await WritePendingTurnSnapshotManifestAsync(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_materialized_state_outside_authority", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.Contains("activeGuardian", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "missing_required_string", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.StartsWith("game_state/meta/guardians.json.activeGuardian.", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            issue.Code?.StartsWith("guardian_missing_", StringComparison.OrdinalIgnoreCase) == true &&
            issue.FilePath.StartsWith("game_state/meta/guardians.json.activeGuardian.", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_FoundationNewGuardianWithoutCreateSurface_Fails()
    {
        await WriteSuccessfulFoundationResolutionAsync(includeCreateCommand: false);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue => string.Equals(issue.Code, "guardian_materialized_without_create_surface", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_FoundationCreateSurfaceDivergesFromMaterializedGuardian_Fails()
    {
        await WriteSuccessfulFoundationResolutionAsync();
        var guardiansRoot = JsonNode.Parse((await _fs.ReadFileAsync("game_state/meta/guardians.json"))!)!.AsObject();
        var createData = guardiansRoot["UpdateGuardians"]!.AsArray()[0]!.AsObject()["data"]!.AsObject();
        createData["mood"]!.AsObject()["reason"] = "tampered_create_surface";
        await WriteJsonAsync("game_state/meta/guardians.json", guardiansRoot);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(
            issues,
            issue =>
                string.Equals(issue.Code, "player_guardian_foundation_missing_guardian_resolution", StringComparison.OrdinalIgnoreCase) &&
                issue.Actual?.Contains("materialized state diverges from authority-backed create surface", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public async Task ValidateGameStateAsync_FoundationHistoryWithoutAuthorizedCreateDoesNotAuthorizeFormerPatronRole()
    {
        var preTurnSoul = CreateSoulState();
        var preTurnGuardians = CreateGuardiansRoot(CreateGuardian("guardian_old", "Азалия", "abode_old"), "guardian_old", "abode_old");

        await WriteJsonAsync("game_state/control/pending_turn_snapshot/game_state/meta/soul_state.json", preTurnSoul);
        await WriteJsonAsync("game_state/control/pending_turn_snapshot/game_state/meta/guardians.json", preTurnGuardians);
        await WriteJsonAsync($"game_state/control/pending_turn_snapshot/{GuardianPowerEventState.JournalPath}", new { entries = Array.Empty<object>() });
        await WriteJsonAsync($"game_state/control/pending_turn_snapshot/{GuardianProjectState.TrackerPath}", new
        {
            activeProjects = Array.Empty<object>(),
            completedProjects = Array.Empty<object>(),
            temporaryProjectModifiers = Array.Empty<object>()
        });
        await WritePendingTurnSnapshotManifestAsync(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

        var currentOldGuardian = CreateGuardian(
            "guardian_old",
            "Азалия",
            "abode_old",
            guardianRoleToPlayer: PlayerGuardianFoundationState.GuardianRoleFormerPatron);
        var currentGuardians = CreateGuardiansRoot(currentOldGuardian, "guardian_old", "abode_old");
        var request = JsonSerializer.Deserialize<PlayerGuardianFoundationState.PendingPlayerGuardianFoundationRequest>(
            CreateFoundationRequest().ToJsonString())!;
        currentGuardians[PlayerGuardianFoundationState.HistoryProperty] = new JsonArray
        {
            JsonSerializer.SerializeToNode(PlayerGuardianFoundationState.BuildCanonicalHistoryEntry(
                request,
                "guardian_old",
                "Азалия",
                12,
                "2026-04-18T00:05:00Z"))!
        };

        await WriteJsonAsync("game_state/meta/soul_state.json", preTurnSoul);
        await WriteJsonAsync("game_state/meta/guardians.json", currentGuardians);
        await WriteJsonAsync(GuardianPowerEventState.JournalPath, new { entries = Array.Empty<object>() });
        await WriteChaosSeaLoreBootstrapAsync();
        await WriteJsonAsync("ready/turn_complete.json", new { status = "ok" });

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue => string.Equals(issue.Code, "guardian_materialized_state_outside_authority", StringComparison.OrdinalIgnoreCase));
    }

    private static JsonObject CreateFoundationRequest() => new()
    {
        ["requestId"] = "foundation_req_1",
        ["mode"] = PlayerGuardianFoundationState.RequestMode,
        ["founderSoulName"] = "Тестовая Душа",
        ["previousGuardianId"] = "guardian_old",
        ["previousGuardianName"] = "Азалия",
        ["sourceShiningAvailability"] = ShiningAbodeState.AvailabilitySealedUntilNextAscension,
        ["proposedDisplayName"] = "Трон Прилива",
        ["mantleSummary"] = "Новый покровитель памяти",
        ["mantleCreed"] = "Никто не будет забыт",
        ["appearanceMotifs"] = new JsonArray("волны", "свечи"),
        ["dominantAspect"] = "memory",
        ["createdAtTurn"] = 11,
        ["createdAtUtc"] = "2026-04-18T00:00:00Z"
    };

    private static JsonObject CreateSoulState() => new()
    {
        ["soulName"] = "Тестовая Душа",
        ["previousSoulNames"] = new JsonArray(),
        ["currentRealm"] = "Chaos Sea",
        ["currentIncarnation"] = 3,
        ["enlightenment"] = new JsonObject
        {
            ["currentTier"] = "Вознесённая",
            ["experience"] = 500,
            ["level"] = 5
        },
        ["inkFeathers"] = new JsonObject
        {
            ["current"] = 120,
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
        },
        ["livesHistory"] = new JsonArray(),
        ["soulImprint"] = null,
        ["pendingMemoryLegacy"] = null
    };

    private static JsonObject CreateGuardiansRoot(JsonObject guardian, string activeGuardianId, string currentAbodeId) =>
        CreateGuardiansRoot(new[] { guardian }, activeGuardianId, currentAbodeId);

    private static JsonObject CreateGuardiansRoot(IEnumerable<JsonObject> guardians, string activeGuardianId, string currentAbodeId)
    {
        var guardianClones = guardians.Select(guardian => guardian.DeepClone()!.AsObject()).ToList();
        foreach (var guardian in guardianClones)
        {
            var selfGuardianId = guardian["guardianId"]?.GetValue<string>() ?? string.Empty;
            var relationships = new JsonArray();
            foreach (var otherGuardian in guardianClones)
            {
                var otherGuardianId = otherGuardian["guardianId"]?.GetValue<string>() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(otherGuardianId) ||
                    string.Equals(otherGuardianId, selfGuardianId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                relationships.Add(new JsonObject
                {
                    ["targetGuardianId"] = otherGuardianId,
                    ["targetName"] = otherGuardian["canonicalName"]?.GetValue<string>() ?? otherGuardianId,
                    ["reason"] = "known_presence",
                    ["lastChangedAt"] = "2026-04-17T00:00:00Z",
                    ["awarenessLevel"] = "known",
                    ["attitudeScore"] = 0,
                    ["attitudeTier"] = "neutral"
                });
            }

            guardian["guardianRelationships"] = relationships;
        }

        var guardianArray = new JsonArray();
        foreach (var guardian in guardianClones)
            guardianArray.Add(guardian);

        var activeGuardian = guardianClones.First(guardian =>
            string.Equals(guardian["guardianId"]?.GetValue<string>(), activeGuardianId, StringComparison.OrdinalIgnoreCase));

        return new JsonObject
        {
            ["guardians"] = guardianArray,
            ["activeGuardian"] = activeGuardian.DeepClone(),
            ["chaosSeaNavigation"] = new JsonObject
            {
                ["currentAbodeId"] = currentAbodeId,
                ["discoveredAbodes"] = new JsonArray("abode_old", "abode_player")
            }
        };
    }

    private static JsonObject CreateGuardian(
        string guardianId,
        string canonicalName,
        string abodeId,
        string? originType = null,
        string? founderSoulName = null,
        string? founderLoyaltyTier = null,
        string? formerPatronGuardianId = null,
        string? foundationSource = null,
        string? foundationRequestId = null,
        int currentReputation = 180,
        string? guardianRoleToPlayer = null)
    {
        var guardian = new JsonObject
        {
            ["guardianId"] = guardianId,
            ["canonicalName"] = canonicalName,
            ["nameVariants"] = new JsonObject
            {
                ["default"] = canonicalName,
                ["feminine"] = canonicalName,
                ["masculine"] = canonicalName,
                ["neutral"] = canonicalName
            },
            ["manifestation"] = new JsonObject
            {
                ["currentDisplayName"] = canonicalName,
                ["formFlexibility"] = "selective",
                ["currentPresentationStyle"] = "neutral",
                ["currentPronouns"] = "они/их",
                ["appearanceDescription"] = "Тестовое сияющее проявление."
            },
            ["manifestationHistory"] = new JsonArray(),
            ["domain"] = "Memory",
            ["abode"] = new JsonObject
            {
                ["abodeId"] = abodeId,
                ["name"] = canonicalName + " — Обитель",
                ["isDiscovered"] = true
            },
            ["personalityProfile"] = new JsonObject
            {
                ["archetype"] = "Keeper",
                ["speechPattern"] = "Measured",
                ["coreValues"] = new JsonArray("memory", "mercy", "clarity")
            },
            ["relationshipData"] = new JsonObject
            {
                ["currentReputation"] = currentReputation,
                ["reputationHistory"] = new JsonArray(),
                ["lastInteraction"] = null
            },
            ["abodePower"] = new JsonObject
            {
                ["currentPower"] = 75,
                ["tier"] = "Могущественная",
                ["lastUpdatedAt"] = "2026-04-17T00:00:00Z",
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
            },
            ["mood"] = new JsonObject
            {
                ["current"] = "focused",
                ["intensity"] = 40,
                ["reason"] = "foundation",
                ["since"] = 10
            },
            ["loreFragments"] = new JsonArray
            {
                CreateLoreFragment("fragment_1"),
                CreateLoreFragment("fragment_2"),
                CreateLoreFragment("fragment_3"),
                CreateLoreFragment("fragment_4"),
                CreateLoreFragment("fragment_5"),
                CreateLoreFragment("fragment_6"),
                CreateLoreFragment("fragment_7")
            },
            ["musings"] = new JsonArray()
        };

        if (!string.IsNullOrWhiteSpace(originType))
            guardian["originType"] = originType;
        if (!string.IsNullOrWhiteSpace(founderSoulName))
            guardian["founderSoulName"] = founderSoulName;
        if (!string.IsNullOrWhiteSpace(founderLoyaltyTier))
            guardian["founderLoyaltyTier"] = founderLoyaltyTier;
        if (!string.IsNullOrWhiteSpace(formerPatronGuardianId))
            guardian["formerPatronGuardianId"] = formerPatronGuardianId;
        if (!string.IsNullOrWhiteSpace(foundationSource))
            guardian["foundationSource"] = foundationSource;
        if (!string.IsNullOrWhiteSpace(foundationRequestId))
            guardian["foundationRequestId"] = foundationRequestId;
        if (!string.IsNullOrWhiteSpace(guardianRoleToPlayer) &&
            guardian["relationshipData"] is JsonObject relationshipData)
        {
            relationshipData[PlayerGuardianFoundationState.GuardianRoleToPlayerProperty] = guardianRoleToPlayer;
        }
        if (string.Equals(originType, PlayerGuardianFoundationState.OriginTypePlayerFoundedAscendedSoul, StringComparison.OrdinalIgnoreCase))
        {
            guardian[PlayerGuardianFoundationState.FounderBonusesProperty] = new JsonObject
            {
                [PlayerGuardianFoundationState.FounderBonusExtraGachaChargesProperty] = PlayerGuardianFoundationState.DefaultFounderExtraGachaChargesPerReturn
            };
            guardian[PlayerGuardianFoundationState.FounderAbodeFeaturesProperty] = new JsonObject
            {
                [PlayerGuardianFoundationState.FounderAbodeResidentAttractionModeProperty] = PlayerGuardianFoundationState.FounderAbodeResidentAttractionModeFounderCall,
                [PlayerGuardianFoundationState.FounderAbodeFeatureTitleProperty] = "Зов основанной мантии",
                [PlayerGuardianFoundationState.FounderAbodeFeatureSummaryProperty] = "Новая Обитель притягивает первых резидентов без автоматического переноса старого roster."
            };
        }

        AbodePowerRules.EnsureCanonicalState(guardian);
        GuardianGachaChargeRules.NormalizeGuardianGachaState(guardian);

        return guardian;
    }

    private static JsonObject CreateLoreFragment(string fragmentId) => new()
    {
        ["fragmentId"] = fragmentId,
        ["category"] = "personal_history",
        ["title"] = "Тестовый фрагмент",
        ["content"] = null,
        ["requiredReputation"] = 0
    };

    private async Task WriteChaosSeaLoreBootstrapAsync()
    {
        await WriteJsonAsync("lore/chaos_sea/guardians_lore.json", new
        {
            entries = new[]
            {
                new { guardianId = "guardian_old", title = "Азалия", summary = "Тестовый lore bootstrap." }
            }
        });
        await WriteJsonAsync("lore/chaos_sea/player_chronicle.json", new
        {
            entries = new[]
            {
                new { entryId = "chronicle_1", title = "Возвращение", summary = "Душа вернулась в Море Хаоса." }
            }
        });
    }

    private async Task WritePendingTurnSnapshotManifestAsync(Dictionary<string, string> rollbackBackups)
    {
        var manifest = new PendingTurnSnapshotManifest
        {
            SessionId = "test-session",
            RequestId = "test-request",
            TurnNumber = 12,
            RequestTimestamp = "2026-04-18T00:00:00Z",
            PlayerAction = "foundation-test",
            Files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            SnapshotFileHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            ClientOwnedValidationHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            RollbackBackups = rollbackBackups.ToDictionary(
                pair => NormalizeRelativePath(pair.Key),
                pair => NormalizeRelativePath(pair.Value),
                StringComparer.OrdinalIgnoreCase),
            RollbackBaselineFiles = rollbackBackups.Keys
                .Select(NormalizeRelativePath)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            SourceLabel = "player-guardian-foundation-tests",
            ManifestPayloadHash = string.Empty
        };

        await WriteJsonAsync("input/turn_request.json", new
        {
            sessionId = manifest.SessionId,
            requestId = manifest.RequestId,
            turnNumber = manifest.TurnNumber
        });

        await RegisterSnapshotFilesAsync(manifest);
        manifest.ManifestPayloadHash = ComputeManifestPayloadHash(manifest);
        await WriteJsonAsync("game_state/control/pending_turn_snapshot.json", manifest);
        await PendingTurnSnapshotTestAuthority.SyncAuthorityForCurrentManifestAsync(_fs);
    }

    private async Task RegisterSnapshotFilesAsync(PendingTurnSnapshotManifest manifest)
    {
        foreach (var pair in manifest.RollbackBackups)
        {
            var snapshotPath = $"game_state/control/pending_turn_snapshot/{pair.Key}";
            var snapshotJson = await _fs.ReadFileAsync(snapshotPath);
            if (string.IsNullOrWhiteSpace(snapshotJson))
            {
                snapshotJson = await _fs.ReadFileAsync(pair.Value);
                if (string.IsNullOrWhiteSpace(snapshotJson))
                    continue;

                await _fs.WriteFileAtomicAsync(snapshotPath, snapshotJson);
            }

            manifest.Files[pair.Key] = snapshotPath;
            manifest.SnapshotFileHashes[pair.Key] = ComputeSha256(snapshotJson);
        }

        var snapshotRoot = _fs.ResolvePath("game_state/control/pending_turn_snapshot");
        if (!Directory.Exists(snapshotRoot))
            return;

        foreach (var snapshotFile in Directory.GetFiles(snapshotRoot, "*", SearchOption.AllDirectories))
        {
            var relativeSnapshotPath = NormalizeRelativePath(Path.GetRelativePath(snapshotRoot, snapshotFile));
            if (!relativeSnapshotPath.Contains('/'))
                continue;

            if (manifest.Files.ContainsKey(relativeSnapshotPath))
                continue;

            var snapshotJson = await File.ReadAllTextAsync(snapshotFile);
            if (string.IsNullOrWhiteSpace(snapshotJson))
                continue;

            manifest.Files[relativeSnapshotPath] = $"game_state/control/pending_turn_snapshot/{relativeSnapshotPath}";
            manifest.SnapshotFileHashes[relativeSnapshotPath] = ComputeSha256(snapshotJson);
        }
    }

    private async Task WriteJsonAsync(string relativePath, object payload)
    {
        var json = payload switch
        {
            JsonObject obj => obj.ToJsonString(new JsonSerializerOptions { WriteIndented = true }),
            JsonArray arr => arr.ToJsonString(new JsonSerializerOptions { WriteIndented = true }),
            _ => JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                WriteIndented = true
            })
        };

        await _fs.WriteFileAtomicAsync(relativePath, json);
    }

    private static string ComputeManifestPayloadHash(PendingTurnSnapshotManifest manifest)
    {
        var payload = new PendingTurnSnapshotManifest
        {
            SessionId = manifest.SessionId,
            RequestId = manifest.RequestId,
            TurnNumber = manifest.TurnNumber,
            RequestTimestamp = manifest.RequestTimestamp,
            PlayerAction = manifest.PlayerAction,
            Files = manifest.Files,
            SnapshotFileHashes = manifest.SnapshotFileHashes,
            ClientOwnedValidationHashes = manifest.ClientOwnedValidationHashes,
            RollbackBackups = manifest.RollbackBackups,
            RollbackBaselineFiles = manifest.RollbackBaselineFiles,
            SourceLabel = manifest.SourceLabel,
            ManifestPayloadHash = string.Empty
        };

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        return ComputeSha256(json);
    }

    private static string ComputeSha256(string content)
    {
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(content)));
    }

    private static string NormalizeRelativePath(string path) => path.Replace('\\', '/');

    private async Task WriteSuccessfulFoundationResolutionAsync(
        bool includeSoulStatus = true,
        bool includeFormerPatronRole = true,
        bool includeCreateCommand = true,
        int foundedGuardianReputation = PlayerGuardianFoundationState.SoulboundCanonicalStartingReputation)
    {
        var request = CreateFoundationRequest();
        var requestContract = JsonSerializer.Deserialize<PlayerGuardianFoundationState.PendingPlayerGuardianFoundationRequest>(request.ToJsonString())!;
        var preTurnSoul = CreateSoulState();
        var preTurnGuardians = CreateGuardiansRoot(CreateGuardian("guardian_old", "Азалия", "abode_old"), "guardian_old", "abode_old");

        await WriteJsonAsync(PlayerGuardianFoundationState.PendingRequestPath, request);
        await WriteJsonAsync("game_state/control/pending_turn_snapshot/game_state/meta/soul_state.json", preTurnSoul);
        await WriteJsonAsync("game_state/control/pending_turn_snapshot/game_state/meta/guardians.json", preTurnGuardians);
        await WriteJsonAsync($"game_state/control/pending_turn_snapshot/{GuardianPowerEventState.JournalPath}", new { entries = Array.Empty<object>() });
        const string backupPath = "game_state/control/pending_turn_snapshot/pre_pending_player_guardian_foundation.json";
        await WriteJsonAsync(backupPath, request);
        await WritePendingTurnSnapshotManifestAsync(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [PlayerGuardianFoundationState.PendingRequestPath] = backupPath
        });

        var formerPatronRole = includeFormerPatronRole ? PlayerGuardianFoundationState.GuardianRoleFormerPatron : null;
        var oldGuardian = CreateGuardian("guardian_old", "Азалия", "abode_old", guardianRoleToPlayer: formerPatronRole);
        var foundedGuardian = CreateGuardian(
            "guardian_player",
            "Трон Прилива",
            "abode_player",
            originType: PlayerGuardianFoundationState.OriginTypePlayerFoundedAscendedSoul,
            founderSoulName: "Тестовая Душа",
            founderLoyaltyTier: PlayerGuardianFoundationState.FounderLoyaltyTierSoulbound,
            formerPatronGuardianId: "guardian_old",
            foundationSource: PlayerGuardianFoundationState.FoundationSourceShiningReturn,
            foundationRequestId: "foundation_req_1",
            currentReputation: foundedGuardianReputation);

        var currentGuardians = CreateGuardiansRoot(new[] { oldGuardian, foundedGuardian }, "guardian_player", "abode_player");
        if (includeCreateCommand)
        {
            var createData =
                currentGuardians["guardians"] is JsonArray currentGuardianArray
                    ? currentGuardianArray
                        .OfType<JsonObject>()
                        .First(guardian => string.Equals(
                            guardian["guardianId"]?.GetValue<string>(),
                            "guardian_player",
                            StringComparison.OrdinalIgnoreCase))
                        .DeepClone()
                    : foundedGuardian.DeepClone();
            currentGuardians["UpdateGuardians"] = new JsonArray
            {
                new JsonObject
                {
                    ["command"] = "create",
                    ["data"] = createData
                }
            };
        }
        currentGuardians[PlayerGuardianFoundationState.HistoryProperty] = new JsonArray
        {
            JsonSerializer.SerializeToNode(PlayerGuardianFoundationState.BuildCanonicalHistoryEntry(
                requestContract,
                "guardian_player",
                "Трон Прилива",
                12,
                "2026-04-18T00:05:00Z"))!
        };

        var currentSoul = CreateSoulState();
        currentSoul[PlayerGuardianFoundationState.SoulStateGuardianIdProperty] = "guardian_player";
        if (includeSoulStatus)
            currentSoul[PlayerGuardianFoundationState.SoulStateFoundationStatusProperty] = PlayerGuardianFoundationState.SoulStateFoundationStatusFounded;

        await WriteJsonAsync("game_state/meta/soul_state.json", currentSoul);
        await WriteJsonAsync("game_state/meta/guardians.json", currentGuardians);
        await WriteJsonAsync(GuardianPowerEventState.JournalPath, new { entries = Array.Empty<object>() });
        await WriteChaosSeaLoreBootstrapAsync();
        await WriteJsonAsync("ready/turn_complete.json", new { status = "ok" });
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
            // ignore cleanup failures in tests
        }
    }

    private sealed class PendingTurnSnapshotManifest
    {
        public string SessionId { get; set; } = string.Empty;
        public string RequestId { get; set; } = string.Empty;
        public int TurnNumber { get; set; }
        public string RequestTimestamp { get; set; } = string.Empty;
        public string PlayerAction { get; set; } = string.Empty;
        public Dictionary<string, string> Files { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> SnapshotFileHashes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> ClientOwnedValidationHashes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> RollbackBackups { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public List<string> RollbackBaselineFiles { get; set; } = new();
        public string? SourceLabel { get; set; }
        public string ManifestPayloadHash { get; set; } = string.Empty;
    }
}
