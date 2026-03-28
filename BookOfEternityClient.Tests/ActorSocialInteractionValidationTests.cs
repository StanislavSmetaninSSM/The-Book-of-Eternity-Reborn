using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
        var manifestFiles = rollbackBackups
            .ToDictionary(
                pair => pair.Key.Replace('\\', '/'),
                pair => pair.Value.Replace('\\', '/'),
                StringComparer.OrdinalIgnoreCase);

        var joined = string.Join("\n", manifestFiles
            .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Select(pair => $"{pair.Key}|{pair.Value}"));
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(joined));
        var manifestHash = Convert.ToHexString(hashBytes);

        var manifest = new
        {
            sessionId = "test-session",
            requestId = "test-request",
            turnNumber = 12,
            createdAtUtc = "2026-03-27T00:00:00Z",
            sourceLabel = "actor-social-validation-tests",
            files = manifestFiles,
            manifestPayloadHash = manifestHash
        };

        await WriteJsonAsync("game_state/control/pending_turn_snapshot.json", manifest);
    }

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
}
