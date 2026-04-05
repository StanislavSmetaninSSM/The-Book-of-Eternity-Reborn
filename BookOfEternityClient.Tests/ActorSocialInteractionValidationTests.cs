using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Reflection;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ActorSocialInteractionValidationTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly ValidationService _validator;

    public ActorSocialInteractionValidationTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-actor-social-validation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
        CopyDirectory(TestRepoPaths.BaseSessionRoot, Path.Combine(_rootPath, "game_session"));
        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
    }

    [Fact]
    public async Task ValidateGameStateAsync_PendingGuardianSocialRequestWithInvalidContract_Fails()
    {
        var request = new
        {
            requestId = "guardian_social_req_1",
            guardianId = "",
            guardianName = "Азалия",
            interactionType = "invalid_type",
            createdAtTurn = 12,
            createdAtUtc = "2026-03-27T00:00:00Z"
        };

        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Тестовая Душа",
            currentRealm = "Chaos Sea",
            currentIncarnation = 1
        });
        await WriteJsonAsync("game_state/meta/guardians.json", new
        {
            guardians = new[]
            {
                new
                {
                    guardianId = "guardian_alpha",
                    canonicalName = "Азалия",
                    domain = "Порог Сна",
                    nameVariants = new
                    {
                        @default = "Азалия",
                        feminine = "Азалия",
                        masculine = (string?)null,
                        neutral = (string?)null
                    },
                    manifestation = new
                    {
                        currentDisplayName = "Азалия",
                        formFlexibility = "selective",
                        currentPresentationStyle = "feminine",
                        currentPronouns = "она/её",
                        appearanceDescription = "Тестовая форма."
                    },
                    manifestationHistory = Array.Empty<object>(),
                    relationshipData = new { currentReputation = 110, reputationHistory = Array.Empty<object>(), lastInteraction = (string?)null },
                    abodePower = new { currentPower = 10, tier = "Хрупкая", lastUpdatedAt = "2026-03-24T00:00:00Z", history = Array.Empty<object>() },
                    abode = new { abodeId = "abode_alpha", name = "Тестовая обитель" },
                    gachaSystem = new { chargesPerReturn = 0, chargesUsedThisReturn = 0, gachaHistory = Array.Empty<object>() }
                }
            }
        });
        await WriteJsonAsync(ActorSocialInteractionRequestState.PendingGuardianRequestPath, new { requests = new[] { request } });
        await WriteJsonAsync("ready/turn_complete.json", new { accepted = true });
        await WriteJsonAsync("game_state/control/pending_turn_snapshot/game_state/meta/soul_state.json", new { currentRealm = "Chaos Sea" });
        const string backupPath = "game_state/control/pending_turn_snapshot/pre_pending_guardian_social_interactions.json";
        await WriteJsonAsync(backupPath, new { requests = new[] { request } });
        await WritePendingTurnSnapshotManifestAsync(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [ActorSocialInteractionRequestState.PendingGuardianRequestPath] = backupPath
        });

        var issues = await InvokeValidationAsync("ValidatePendingGuardianSocialInteractionRequestContextAsync");

        Assert.Contains(issues, issue => string.Equals(issue.Code, "guardian_social_interactions_missing_fields", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_PendingGuardianSocialRequestWithJournalClosure_Passes()
    {
        var request = new
        {
            requestId = "guardian_social_req_2",
            guardianId = "guardian_alpha",
            guardianName = "Азалия",
            interactionType = "lore",
            createdAtTurn = 12,
            createdAtUtc = "2026-03-27T00:00:00Z"
        };

        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Тестовая Душа",
            currentRealm = "Chaos Sea",
            currentIncarnation = 1
        });
        await WriteJsonAsync("game_state/meta/guardians.json", new
        {
            guardians = new[]
            {
                new
                {
                    guardianId = "guardian_alpha",
                    canonicalName = "Азалия",
                    domain = "Порог Сна",
                    nameVariants = new
                    {
                        @default = "Азалия",
                        feminine = "Азалия",
                        masculine = (string?)null,
                        neutral = (string?)null
                    },
                    manifestation = new
                    {
                        currentDisplayName = "Азалия",
                        formFlexibility = "selective",
                        currentPresentationStyle = "feminine",
                        currentPronouns = "она/её",
                        appearanceDescription = "Тестовая форма."
                    },
                    manifestationHistory = Array.Empty<object>(),
                    relationshipData = new { currentReputation = 110, reputationHistory = Array.Empty<object>(), lastInteraction = (string?)null },
                    abodePower = new { currentPower = 10, tier = "Хрупкая", lastUpdatedAt = "2026-03-24T00:00:00Z", history = Array.Empty<object>() },
                    abode = new { abodeId = "abode_alpha", name = "Тестовая обитель" },
                    gachaSystem = new { chargesPerReturn = 0, chargesUsedThisReturn = 0, gachaHistory = Array.Empty<object>() }
                }
            }
        });
        await WriteJsonAsync(ActorSocialInteractionRequestState.PendingGuardianRequestPath, new { requests = new[] { request } });
        await WriteJsonAsync(GuardianSocialJournalState.StatePath, new
        {
            entries = new[]
            {
                new
                {
                    entryId = "guardian_social_entry_1",
                    guardianId = "guardian_alpha",
                    requestId = "guardian_social_req_2",
                    interactionType = "lore",
                    status = "accepted",
                    responseMode = "lore_revealed",
                    turn = 12,
                    timestamp = "2026-03-27T00:01:00Z",
                    title = "Азалия раскрыла нить",
                    summary = "Хранитель объяснил происхождение древнего узора."
                }
            }
        });
        await WriteJsonAsync("ready/turn_complete.json", new { accepted = true });
        await WriteJsonAsync("game_state/control/pending_turn_snapshot/game_state/meta/soul_state.json", new { currentRealm = "Chaos Sea" });
        const string backupPath = "game_state/control/pending_turn_snapshot/pre_pending_guardian_social_interactions_pass.json";
        await WriteJsonAsync(backupPath, new { requests = new[] { request } });
        await WritePendingTurnSnapshotManifestAsync(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [ActorSocialInteractionRequestState.PendingGuardianRequestPath] = backupPath
        });

        var issues = await InvokeValidationAsync("ValidatePendingGuardianSocialInteractionResolutionAsync");

        Assert.DoesNotContain(issues, issue => string.Equals(issue.Code, "guardian_social_interaction_missing_resolution", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_PendingNpcSocialRequestWithInvalidContract_Fails()
    {
        var request = new
        {
            requestId = "npc_social_req_1",
            npcId = "",
            npcName = "Старый Торговец",
            interactionType = "invalid_type",
            createdAtTurn = 7,
            createdAtUtc = "2026-03-27T00:00:00Z"
        };

        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Тестовая Душа",
            currentRealm = "Mortal World",
            currentIncarnation = 1
        });
        await WriteJsonAsync("game_state/npcs/npc_core.json", new
        {
            NPCsInScene = new[]
            {
                new
                {
                    NPCId = "npc_merchant_01",
                    name = "Старый Торговец",
                    currentLocationId = "loc_market"
                }
            }
        });
        await WriteJsonAsync(ActorSocialInteractionRequestState.PendingNpcRequestPath, new { requests = new[] { request } });
        await WriteJsonAsync("ready/turn_complete.json", new { accepted = true });
        await WriteJsonAsync("game_state/control/pending_turn_snapshot/game_state/meta/soul_state.json", new { currentRealm = "Mortal World" });
        const string backupPath = "game_state/control/pending_turn_snapshot/pre_pending_npc_social_interactions.json";
        await WriteJsonAsync(backupPath, new { requests = new[] { request } });
        await WritePendingTurnSnapshotManifestAsync(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [ActorSocialInteractionRequestState.PendingNpcRequestPath] = backupPath
        });

        var issues = await InvokeValidationAsync("ValidatePendingNpcSocialInteractionRequestContextAsync");

        Assert.Contains(issues, issue => string.Equals(issue.Code, "npc_social_interactions_missing_fields", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_PendingNpcSocialRequestWithJournalClosure_Passes()
    {
        var request = new
        {
            requestId = "npc_social_req_2",
            npcId = "npc_merchant_01",
            npcName = "Старый Торговец",
            interactionType = "talk",
            createdAtTurn = 7,
            createdAtUtc = "2026-03-27T00:00:00Z"
        };

        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Тестовая Душа",
            currentRealm = "Mortal World",
            currentIncarnation = 1
        });
        await WriteJsonAsync("game_state/npcs/npc_core.json", new
        {
            NPCsInScene = new[]
            {
                new
                {
                    NPCId = "npc_merchant_01",
                    name = "Старый Торговец",
                    currentLocationId = "loc_market"
                }
            }
        });
        await WriteJsonAsync(ActorSocialInteractionRequestState.PendingNpcRequestPath, new { requests = new[] { request } });
        await WriteJsonAsync(NpcInteractionJournalState.StatePath, new
        {
            entries = new[]
            {
                new
                {
                    entryId = "npc_social_entry_1",
                    npcId = "npc_merchant_01",
                    requestId = "npc_social_req_2",
                    interactionType = "talk",
                    status = "accepted",
                    responseMode = "talk_scene",
                    turn = 7,
                    timestamp = "2026-03-27T00:01:00Z",
                    title = "Торговец заговорил первым",
                    summary = "Старый Торговец наконец доверил часть своей тревоги."
                }
            }
        });
        await WriteJsonAsync("ready/turn_complete.json", new { accepted = true });
        await WriteJsonAsync("game_state/control/pending_turn_snapshot/game_state/meta/soul_state.json", new { currentRealm = "Mortal World" });
        const string backupPath = "game_state/control/pending_turn_snapshot/pre_pending_npc_social_interactions_pass.json";
        await WriteJsonAsync(backupPath, new { requests = new[] { request } });
        await WritePendingTurnSnapshotManifestAsync(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [ActorSocialInteractionRequestState.PendingNpcRequestPath] = backupPath
        });

        var issues = await InvokeValidationAsync("ValidatePendingNpcSocialInteractionResolutionAsync");

        Assert.DoesNotContain(issues, issue => string.Equals(issue.Code, "npc_social_interaction_missing_resolution", StringComparison.OrdinalIgnoreCase));
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
        var manifest = new PendingTurnSnapshotManifest
        {
            SessionId = "test-session",
            RequestId = "test-request",
            TurnNumber = 12,
            RequestTimestamp = "2026-03-27T00:00:00Z",
            PlayerAction = "test",
            Files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            SnapshotFileHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            ClientOwnedValidationHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            RollbackBackups = rollbackBackups.ToDictionary(
                pair => NormalizeRelativePath(pair.Key),
                pair => NormalizeRelativePath(pair.Value),
                StringComparer.OrdinalIgnoreCase),
            RollbackBaselineFiles = new List<string>(),
            SourceLabel = "actor-social-validation-tests",
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

    private async Task WriteJsonAsync(string relativePath, object payload)
    {
        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        });

        await _fs.WriteFileAtomicAsync(relativePath, json);
    }

    private static void CopyDirectory(string sourceDir, string destinationDir)
    {
        Directory.CreateDirectory(destinationDir);
        foreach (var file in Directory.GetFiles(sourceDir))
            File.Copy(file, Path.Combine(destinationDir, Path.GetFileName(file)), overwrite: true);
        foreach (var directory in Directory.GetDirectories(sourceDir))
            CopyDirectory(directory, Path.Combine(destinationDir, Path.GetFileName(directory)));
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
            // ignored
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
