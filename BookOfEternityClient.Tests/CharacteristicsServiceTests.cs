using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Linq;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class CharacteristicsServiceTests : IDisposable
{
    private sealed class PendingTurnSnapshotManifestPayload
    {
        public string SessionId { get; set; } = "";
        public string RequestId { get; set; } = "";
        public int TurnNumber { get; set; }
        public string RequestTimestamp { get; set; } = "";
        public string PlayerAction { get; set; } = "";
        public ProgressionControl? ProgressionControl { get; set; }
        public Dictionary<string, string> Files { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> SnapshotFileHashes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> ClientOwnedValidationHashes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> RollbackBackups { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public List<string> RollbackBaselineFiles { get; set; } = new();
        public string? SourceLabel { get; set; }
        public string ManifestPayloadHash { get; set; } = "";
    }

    private static readonly JsonSerializerOptions SnapshotHashJsonOpts = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly CharacteristicsService _service;

    public CharacteristicsServiceTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-characteristics-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);

        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();

        var stateManager = new StateManager(_fs, new GameSettings(), NullLogger<StateManager>.Instance);
        _service = new CharacteristicsService(_fs, stateManager, NullLogger<CharacteristicsService>.Instance);
    }

    [Fact]
    public async Task ComputeAsync_CanonicalCurrentSoulRelics_AppliesRelicCharacteristicBonuses()
    {
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulRelics = new
            {
                equipped = new object[]
                {
                    new
                    {
                        relicId = "relic_bonus_valid",
                        name = "Реликвия Очарования",
                        rarity = "Rare",
                        effects = new
                        {
                            characteristicBonuses = new
                            {
                                attractiveness = 3
                            }
                        }
                    }
                },
                stored = Array.Empty<object>()
            }
        });

        var result = await _service.ComputeAsync();

        Assert.Equal(3, result.Stats[Characteristics.Attractiveness].PermanentBonus);
        Assert.Equal(4, result.Stats[Characteristics.Attractiveness].PermanentlyModified);
    }

    [Fact]
    public async Task ComputeAsync_SourceOfLightIncarnatedLight_AppliesAllCharacteristicBonuses()
    {
        var bonuses = Characteristics.All.ToDictionary(
            characteristic => characteristic,
            _ => SourceOfLightCapstoneState.MortalCharacteristicBonus,
            StringComparer.Ordinal);
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulRelics = new
            {
                equipped = new object[]
                {
                    new
                    {
                        relicId = SourceOfLightCapstoneState.RelicId,
                        name = "Воплощенный Свет",
                        rarity = "legendary",
                        effects = new
                        {
                            characteristicBonuses = bonuses
                        }
                    }
                },
                stored = Array.Empty<object>()
            }
        });

        var result = await _service.ComputeAsync();

        foreach (var characteristic in Characteristics.All)
        {
            Assert.Equal(SourceOfLightCapstoneState.MortalCharacteristicBonus, result.Stats[characteristic].PermanentBonus);
            Assert.Equal(1 + SourceOfLightCapstoneState.MortalCharacteristicBonus, result.Stats[characteristic].PermanentlyModified);
        }
    }

    [Fact]
    public async Task ComputeAsync_MalformedCurrentSoulRelics_DoesNotApplyRelicCharacteristicBonuses()
    {
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulRelics = new
            {
                equipped = new object[]
                {
                    new
                    {
                        relicId = "relic_bonus_invalid",
                        rarity = "Rare",
                        effects = new
                        {
                            characteristicBonuses = new
                            {
                                attractiveness = 3
                            }
                        }
                    }
                },
                stored = Array.Empty<object>()
            }
        });

        var result = await _service.ComputeAsync();

        Assert.Equal(0, result.Stats[Characteristics.Attractiveness].PermanentBonus);
        Assert.Equal(1, result.Stats[Characteristics.Attractiveness].PermanentlyModified);
    }

    [Fact]
    public async Task ComputeAsync_MalformedSiblingSoulStateRoot_DoesNotApplyRelicCharacteristicBonuses()
    {
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            inkFeathers = new
            {
                current = "5"
            },
            soulRelics = new
            {
                equipped = new object[]
                {
                    new
                    {
                        relicId = "relic_bonus_blocked_by_sibling_root",
                        name = "Реликвия Заблокированного Бонуса",
                        rarity = "Rare",
                        effects = new
                        {
                            characteristicBonuses = new
                            {
                                attractiveness = 3
                            }
                        }
                    }
                },
                stored = Array.Empty<object>()
            }
        });

        var result = await _service.ComputeAsync();

        Assert.Equal(0, result.Stats[Characteristics.Attractiveness].PermanentBonus);
        Assert.Equal(1, result.Stats[Characteristics.Attractiveness].PermanentlyModified);
    }

    [Fact]
    public async Task ComputeAsync_RecordLifeCompletionWithoutTriggerLifeEnd_DoesNotApplyRelicCharacteristicBonuses()
    {
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            currentRealm = "Mortal World",
            currentIncarnation = 3,
            metaStateUpdates = new
            {
                lifeTransitions = new
                {
                    recordLifeCompletion = new
                    {
                        characterFinalState = new { causeOfDeath = "Test" },
                        majorAchievements = Array.Empty<string>(),
                        relationshipsFormed = Array.Empty<object>(),
                        moralChoices = Array.Empty<object>(),
                        skillsLearned = Array.Empty<string>(),
                        enlightenmentGained = 0
                    }
                }
            },
            soulRelics = new
            {
                equipped = new object[]
                {
                    new
                    {
                        relicId = "relic_bonus_blocked_by_trigger_context",
                        name = "Реликвия Несвоевременной Завершённой Жизни",
                        rarity = "Rare",
                        effects = new
                        {
                            characteristicBonuses = new
                            {
                                attractiveness = 3
                            }
                        }
                    }
                },
                stored = Array.Empty<object>()
            }
        });

        var result = await _service.ComputeAsync();

        Assert.Equal(0, result.Stats[Characteristics.Attractiveness].PermanentBonus);
        Assert.Equal(1, result.Stats[Characteristics.Attractiveness].PermanentlyModified);
    }

    [Fact]
    public async Task ComputeAsync_RecordLifeCompletionWithCanonicalTriggerLifeEnd_AppliesRelicCharacteristicBonuses()
    {
        await WriteJsonAsync("game_state/control/pending_turn_snapshot/game_state/meta/soul_state.json", new
        {
            currentRealm = "Mortal World",
            currentIncarnation = 3
        });
        await WritePendingTurnSnapshotManifestAsync("game_state/meta/soul_state.json");
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            currentRealm = "Mortal World",
            currentIncarnation = 3,
            metaStateUpdates = new
            {
                lifeTransitions = new
                {
                    recordLifeCompletion = new
                    {
                        characterFinalState = new { causeOfDeath = "Test" },
                        majorAchievements = Array.Empty<string>(),
                        relationshipsFormed = Array.Empty<object>(),
                        moralChoices = Array.Empty<object>(),
                        skillsLearned = Array.Empty<string>(),
                        enlightenmentGained = 0
                    }
                }
            },
            soulRelics = new
            {
                equipped = new object[]
                {
                    new
                    {
                        relicId = "relic_bonus_allowed_by_trigger_context",
                        name = "Реликвия Своевременной Завершённой Жизни",
                        rarity = "Rare",
                        effects = new
                        {
                            characteristicBonuses = new
                            {
                                attractiveness = 3
                            }
                        }
                    }
                },
                stored = Array.Empty<object>()
            }
        });
        await WriteJsonAsync("game_state/control/life_transitions.json", new
        {
            reason = "Death",
            summary = "Жизнь завершена."
        });

        var result = await _service.ComputeAsync();

        Assert.Equal(3, result.Stats[Characteristics.Attractiveness].PermanentBonus);
        Assert.Equal(4, result.Stats[Characteristics.Attractiveness].PermanentlyModified);
    }

    [Fact]
    public async Task ComputeAsync_RecordLifeCompletionWithAfterlifePreTurnRealmRewrite_DoesNotApplyRelicCharacteristicBonuses()
    {
        await WriteJsonAsync("game_state/control/pending_turn_snapshot/game_state/meta/soul_state.json", new
        {
            currentRealm = "Chaos Sea",
            currentIncarnation = 3
        });
        await WritePendingTurnSnapshotManifestAsync("game_state/meta/soul_state.json");
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            currentRealm = "Mortal World",
            currentIncarnation = 3,
            metaStateUpdates = new
            {
                lifeTransitions = new
                {
                    recordLifeCompletion = new
                    {
                        characterFinalState = new { causeOfDeath = "Test" },
                        majorAchievements = Array.Empty<string>(),
                        relationshipsFormed = Array.Empty<object>(),
                        moralChoices = Array.Empty<object>(),
                        skillsLearned = Array.Empty<string>(),
                        enlightenmentGained = 0
                    }
                }
            },
            soulRelics = new
            {
                equipped = new object[]
                {
                    new
                    {
                        relicId = "relic_bonus_blocked_by_afterlife_preturn_realm",
                        name = "Реликвия Нелегального Pre-Turn TriggerLifeEnd",
                        rarity = "Rare",
                        effects = new
                        {
                            characteristicBonuses = new
                            {
                                attractiveness = 3
                            }
                        }
                    }
                },
                stored = Array.Empty<object>()
            }
        });
        await WriteJsonAsync("game_state/control/life_transitions.json", new
        {
            reason = "Death",
            summary = "Жизнь завершена."
        });

        var result = await _service.ComputeAsync();

        Assert.Equal(0, result.Stats[Characteristics.Attractiveness].PermanentBonus);
        Assert.Equal(1, result.Stats[Characteristics.Attractiveness].PermanentlyModified);
    }

    [Fact]
    public async Task ComputeAsync_RecordLifeCompletionWithCurrentAfterlifeRewrite_DoesNotApplyRelicCharacteristicBonuses()
    {
        await WriteJsonAsync("game_state/control/pending_turn_snapshot/game_state/meta/soul_state.json", new
        {
            currentRealm = "Mortal World",
            currentIncarnation = 3
        });
        await WritePendingTurnSnapshotManifestAsync("game_state/meta/soul_state.json");
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            currentRealm = "Chaos Sea",
            currentIncarnation = 3,
            metaStateUpdates = new
            {
                lifeTransitions = new
                {
                    recordLifeCompletion = new
                    {
                        characterFinalState = new { causeOfDeath = "Test" },
                        majorAchievements = Array.Empty<string>(),
                        relationshipsFormed = Array.Empty<object>(),
                        moralChoices = Array.Empty<object>(),
                        skillsLearned = Array.Empty<string>(),
                        enlightenmentGained = 0
                    }
                }
            },
            soulRelics = new
            {
                equipped = new object[]
                {
                    new
                    {
                        relicId = "relic_bonus_blocked_by_current_afterlife_rewrite",
                        name = "Реликвия Нелегального Same-Turn Realm Switch",
                        rarity = "Rare",
                        effects = new
                        {
                            characteristicBonuses = new
                            {
                                attractiveness = 3
                            }
                        }
                    }
                },
                stored = Array.Empty<object>()
            }
        });
        await WriteJsonAsync("game_state/control/life_transitions.json", new
        {
            reason = "Death",
            summary = "Жизнь завершена."
        });

        var result = await _service.ComputeAsync();

        Assert.Equal(0, result.Stats[Characteristics.Attractiveness].PermanentBonus);
        Assert.Equal(1, result.Stats[Characteristics.Attractiveness].PermanentlyModified);
    }

    [Fact]
    public async Task ComputeAsync_RecordLifeCompletionWithOrphanedPendingSnapshot_DoesNotApplyRelicCharacteristicBonuses()
    {
        await WriteJsonAsync("game_state/control/pending_turn_snapshot/game_state/meta/soul_state.json", new
        {
            currentRealm = "Mortal World",
            currentIncarnation = 3
        });
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            currentRealm = "Mortal World",
            currentIncarnation = 3,
            metaStateUpdates = new
            {
                lifeTransitions = new
                {
                    recordLifeCompletion = new
                    {
                        characterFinalState = new { causeOfDeath = "Test" },
                        majorAchievements = Array.Empty<string>(),
                        relationshipsFormed = Array.Empty<object>(),
                        moralChoices = Array.Empty<object>(),
                        skillsLearned = Array.Empty<string>(),
                        enlightenmentGained = 0
                    }
                }
            },
            soulRelics = new
            {
                equipped = new object[]
                {
                    new
                    {
                        relicId = "relic_bonus_blocked_by_orphaned_snapshot",
                        name = "Реликвия Осиротевшего Снэпшота",
                        rarity = "Rare",
                        effects = new
                        {
                            characteristicBonuses = new
                            {
                                attractiveness = 3
                            }
                        }
                    }
                },
                stored = Array.Empty<object>()
            }
        });
        await WriteJsonAsync("game_state/control/life_transitions.json", new
        {
            reason = "Death",
            summary = "Жизнь завершена."
        });

        var result = await _service.ComputeAsync();

        Assert.Equal(0, result.Stats[Characteristics.Attractiveness].PermanentBonus);
        Assert.Equal(1, result.Stats[Characteristics.Attractiveness].PermanentlyModified);
    }

    [Fact]
    public async Task ComputeAsync_RecordLifeCompletionWithInactiveManifest_DoesNotApplyRelicCharacteristicBonuses()
    {
        await WriteJsonAsync("game_state/control/pending_turn_snapshot/game_state/meta/soul_state.json", new
        {
            currentRealm = "Mortal World",
            currentIncarnation = 3
        });
        await WritePendingTurnSnapshotManifestAsync("stale-session", "stale-request", 99, "game_state/meta/soul_state.json");
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            currentRealm = "Mortal World",
            currentIncarnation = 3,
            metaStateUpdates = new
            {
                lifeTransitions = new
                {
                    recordLifeCompletion = new
                    {
                        characterFinalState = new { causeOfDeath = "Test" },
                        majorAchievements = Array.Empty<string>(),
                        relationshipsFormed = Array.Empty<object>(),
                        moralChoices = Array.Empty<object>(),
                        skillsLearned = Array.Empty<string>(),
                        enlightenmentGained = 0
                    }
                }
            },
            soulRelics = new
            {
                equipped = new object[]
                {
                    new
                    {
                        relicId = "relic_bonus_blocked_by_inactive_manifest",
                        name = "Реликвия Устаревшего Manifest",
                        rarity = "Rare",
                        effects = new
                        {
                            characteristicBonuses = new
                            {
                                attractiveness = 3
                            }
                        }
                    }
                },
                stored = Array.Empty<object>()
            }
        });
        await WriteJsonAsync("game_state/control/life_transitions.json", new
        {
            reason = "Death",
            summary = "Жизнь завершена."
        });

        var result = await _service.ComputeAsync();

        Assert.Equal(0, result.Stats[Characteristics.Attractiveness].PermanentBonus);
        Assert.Equal(1, result.Stats[Characteristics.Attractiveness].PermanentlyModified);
    }

    private async Task WriteJsonAsync(string relativePath, object payload)
    {
        await _fs.WriteFileAtomicAsync(
            relativePath,
            JsonSerializer.Serialize(payload, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
    }

    private async Task WritePendingTurnSnapshotManifestAsync(params string[] trackedPaths)
    {
        await WritePendingTurnSnapshotManifestAsync("test-session", "test-request", 12, trackedPaths);
    }

    private async Task WritePendingTurnSnapshotManifestAsync(
        string sessionId,
        string requestId,
        int turnNumber,
        params string[] trackedPaths)
    {
        var files = trackedPaths.ToDictionary(
            path => path,
            path => $"game_state/control/pending_turn_snapshot/{path}",
            StringComparer.OrdinalIgnoreCase);

        var snapshotHashes = trackedPaths.ToDictionary(
            path => path,
            path =>
            {
                var snapshotPath = _fs.ResolvePath($"game_state/control/pending_turn_snapshot/{path}");
                return ComputeSha256(File.ReadAllText(snapshotPath, Encoding.UTF8));
            },
            StringComparer.OrdinalIgnoreCase);

        var manifest = new PendingTurnSnapshotManifestPayload
        {
            SessionId = sessionId,
            RequestId = requestId,
            TurnNumber = turnNumber,
            RequestTimestamp = "2026-03-24T00:00:00Z",
            PlayerAction = "characteristics-service-test",
            ProgressionControl = new ProgressionControl { CurrentRealm = "Mortal World" },
            Files = files,
            SnapshotFileHashes = snapshotHashes,
            ClientOwnedValidationHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            RollbackBackups = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            RollbackBaselineFiles = trackedPaths.ToList(),
            SourceLabel = "characteristics-service-tests"
        };
        manifest.ManifestPayloadHash = ComputeManifestPayloadHash(manifest);

        await WriteJsonAsync("game_state/control/pending_turn_snapshot.json", new
        {
            sessionId,
            requestId,
            turnNumber,
            requestTimestamp = manifest.RequestTimestamp,
            playerAction = manifest.PlayerAction,
            progressionControl = manifest.ProgressionControl,
            files,
            snapshotFileHashes = snapshotHashes,
            clientOwnedValidationHashes = manifest.ClientOwnedValidationHashes,
            rollbackBackups = manifest.RollbackBackups,
            rollbackBaselineFiles = manifest.RollbackBaselineFiles,
            sourceLabel = manifest.SourceLabel,
            manifestPayloadHash = manifest.ManifestPayloadHash
        });
        await PendingTurnSnapshotTestAuthority.SyncAuthorityForCurrentManifestAsync(_fs);

        await WriteJsonAsync("input/turn_request.json", new
        {
            sessionId = "test-session",
            requestId = "test-request",
            turnNumber = 12
        });
    }

    private static string ComputeManifestPayloadHash(PendingTurnSnapshotManifestPayload manifest)
    {
        var originalHash = manifest.ManifestPayloadHash;
        manifest.ManifestPayloadHash = string.Empty;
        var payload = JsonSerializer.Serialize(manifest, SnapshotHashJsonOpts);
        manifest.ManifestPayloadHash = originalHash;
        return ComputeSha256(payload);
    }

    private static string ComputeSha256(string content)
    {
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(content)));
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
            // best-effort cleanup
        }
    }
}
