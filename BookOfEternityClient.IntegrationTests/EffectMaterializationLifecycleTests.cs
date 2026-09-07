using System.Collections;
using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.Services.GmWorkers;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    public static TheoryData<string> EffectPlayerPublicationFailureStages => new()
    {
        ResourceMaterializationContract.DefinitionsPath,
        ResourceMaterializationContract.HistoryPath,
        ResourceMaterializationContract.StatePath,
        EffectAcceptedTurnPlan.IdentityIndexPath,
        EffectCarrierCatalog.PlayerPath,
        "outputs_published_before_first_mechanical_write",
        "post_check_after_command_consumption"
    };

    public static TheoryData<string> EffectOwnerCarrierFailurePaths => new()
    {
        EffectCarrierCatalog.NpcPath,
        EffectCarrierCatalog.EnemiesPath,
        EffectCarrierCatalog.AlliesPath,
        EffectCarrierCatalog.AfterlifeProfilesPath,
        EffectCarrierCatalog.SpiritualConflictPath
    };

    [Fact]
    public async Task EffectMaterializationLifecycleTests_RollbackTracksEveryEffectSurfaceAndPreservesOperatorDiagnostic()
    {
        var effectPaths = new[]
        {
            EffectAcceptedTurnPlan.CommandPath,
            EffectAcceptedTurnPlan.IdentityIndexPath,
            EffectCarrierCatalog.PlayerPath,
            EffectCarrierCatalog.NpcPath,
            EffectCarrierCatalog.EnemiesPath,
            EffectCarrierCatalog.AlliesPath,
            EffectCarrierCatalog.AfterlifeProfilesPath,
            EffectCarrierCatalog.SpiritualConflictPath,
            ResourcePendingResolutionState.PendingPath,
            ResourceMaterializationContract.DefinitionsPath,
            ResourceMaterializationContract.StatePath,
            ResourceMaterializationContract.HistoryPath,
            "game_state/effects/effect_owned_companion.json",
            "output/narrative_response.json",
            "output/interface_updates.json"
        };
        var operatorDiagnosticPaths = new[]
        {
            "game_state/control/validation_diagnostic_failure_report.json",
            "game_state/control/gm_validation_repair_artifact_stall_report.json",
            RealmSegregationAutoRollbackService.ReportPath,
            GmWorkerAuditLog.AuditLogPath,
            "game_state/control/gm_trajectory_ledger.jsonl",
            "game_state/control/gm_artifact_write_stall_report.json",
            "game_state/control/gm_output_without_terminal_report.json",
            "game_state/control/gm_daemon_fatal_error.json",
            "game_state/control/gm_timeout_bridge_cleanup.json",
            "game_state/control/gm_live_test_notes.jsonl"
        };
        var beforeImages = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < effectPaths.Length; index++)
        {
            var bytes = Encoding.UTF8.GetBytes($"{{\"pathOrdinal\":{index},\"value\":\"baseline\"}}");
            beforeImages[effectPaths[index]] = bytes;
            await _fs.WriteFileAtomicBytesAsync(effectPaths[index], bytes);
        }
        foreach (var path in operatorDiagnosticPaths)
        {
            await _fs.WriteFileAtomicAsync(
                path,
                "{\"diagnostic\":\"baseline operator evidence\"}");
        }

        var engine = CreateGameEngine();
        var rollbackSnapshot = await InvokePrivateTaskResultAsync(
            engine,
            "CreatePreTurnBackup",
            "effect_all_surfaces");
        var baselineFilesValue = rollbackSnapshot.GetType()
            .GetProperty("BaselineFiles")?
            .GetValue(rollbackSnapshot);
        var baselineFiles = Assert.IsAssignableFrom<IEnumerable>(baselineFilesValue)
            .Cast<object>()
            .Select(static value => value?.ToString() ?? string.Empty)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.All(effectPaths, path => Assert.Contains(path, baselineFiles));
        Assert.All(
            operatorDiagnosticPaths,
            path => Assert.DoesNotContain(path, baselineFiles));

        foreach (var path in effectPaths.Take(effectPaths.Length - 1))
            await _fs.WriteFileAtomicAsync(path, "{\"value\":\"rejected\"}");
        _fs.DeleteFile(effectPaths[^1]);
        const string newTrackedPath = "game_state/effects/rejected_new_effect_state.json";
        await _fs.WriteFileAtomicAsync(newTrackedPath, "{\"value\":\"new rejected state\"}");
        foreach (var path in operatorDiagnosticPaths)
        {
            await _fs.WriteFileAtomicAsync(
                path,
                "{\"diagnostic\":\"exact failure detail survives rollback\"}");
        }

        await InvokePrivateTaskAsync(
            engine,
            "RollbackRejectedAcceptedTurnAsync",
            rollbackSnapshot,
            string.Empty);

        foreach (var (path, expectedBytes) in beforeImages)
        {
            var actualBytes = await _fs.ReadFileBytesAsync(path);
            Assert.NotNull(actualBytes);
            Assert.Equal(expectedBytes, actualBytes);
        }
        Assert.False(_fs.FileExists(newTrackedPath));
        foreach (var path in operatorDiagnosticPaths)
        {
            Assert.Equal(
                "{\"diagnostic\":\"exact failure detail survives rollback\"}",
                await _fs.ReadFileAsync(path));
        }
    }

    [Theory]
    [MemberData(nameof(EffectPlayerPublicationFailureStages))]
    public async Task EffectMaterializationLifecycleTests_PlayerPublicationFailureRestoresEntireTrackedSet(
        string failureStage)
    {
        var probe = new EffectPublicationFailureProbe();
        await using var context = await EffectMaterializationTestContext.CreateAsync(
            new FileSystemManagerHooks
            {
                AfterPhysicalFilePublishedAsync = probe.AfterPhysicalFilePublishedAsync,
                AfterCanonicalReadAttemptAsync = probe.AfterCanonicalReadAttemptAsync
            });
        await context.SeedPlayerWoundSourceAsync();
        await context.WriteJsonAsync(
            EffectCarrierCatalog.PlayerPath,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["activeEffects"] = new JsonArray()
            });
        await context.WriteJsonAsync(
            EffectAcceptedTurnPlan.IdentityIndexPath,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["entries"] = new JsonArray()
            });
        await context.CaptureValidatedPendingSnapshotAsync();

        var engine = CreateGameEngine(fileSystem: context.FileSystem);
        var rollbackSnapshot = await InvokePrivateTaskResultAsync(
            engine,
            "CreatePreTurnBackup",
            "effect_player_publication_" + SanitizeFailureStage(failureStage));
        var baseline = await CaptureRollbackTrackedSetAsync(engine, context.FileSystem);
        var backups = await context.ReadPendingSnapshotBackupsAsync();
        await context.WriteJsonAsync(
            EffectAcceptedTurnPlan.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(
                EffectMaterializationTestFixture.CreateApplyCommand()));
        await context.WriteJsonAsync(
            "output/narrative_response.json",
            new JsonObject
            {
                ["response"] = "Непринятый рассказ о вновь открывшейся ране.",
                ["timestamp"] = "2026-08-22T02:30:00Z"
            });
        await context.WriteJsonAsync(
            "output/interface_updates.json",
            new JsonObject
            {
                ["dialogueOptions"] = new JsonArray(new JsonObject
                {
                    ["text"] = "Осмотреть рану",
                    ["category"] = "continue"
                }),
                ["timestamp"] = "2026-08-22T02:30:00Z"
            });

        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();
        Assert.DoesNotContain(
            issues,
            static issue => issue.Severity == IssueSeverity.Error);

        if (string.Equals(
                failureStage,
                "post_check_after_command_consumption",
                StringComparison.Ordinal))
        {
            probe.ArmPostCheckCorruption(
                context.FileSystem,
                ResourceMaterializationContract.StatePath,
                ResourceMaterializationContract.DefinitionsPath);
        }
        else
        {
            var publicationPath = string.Equals(
                    failureStage,
                    "outputs_published_before_first_mechanical_write",
                    StringComparison.Ordinal)
                ? ResourceMaterializationContract.DefinitionsPath
                : failureStage;
            probe.ArmAfterPhysicalPublication(context.FileSystem, publicationPath);
        }

        await Assert.ThrowsAnyAsync<Exception>(() =>
            context.NormalizeAcceptedEffectsAsync(backups));
        Assert.True(probe.Triggered);
        if (string.Equals(
                failureStage,
                "post_check_after_command_consumption",
                StringComparison.Ordinal))
        {
            Assert.False(context.FileSystem.FileExists(
                EffectAcceptedTurnPlan.CommandPath));
        }
        await context.FileSystem.WriteFileAtomicAsync(
            "game_state/control/validation_diagnostic_failure_report.json",
            $"{{\"failureStage\":\"{failureStage}\"}}");

        await InvokePrivateTaskAsync(
            engine,
            "RollbackRejectedAcceptedTurnAsync",
            rollbackSnapshot,
            string.Empty);

        Assert.Equal(
            baseline,
            await CaptureRollbackTrackedSetAsync(engine, context.FileSystem));
        Assert.False(context.FileSystem.FileExists("output/narrative_response.json"));
        Assert.False(context.FileSystem.FileExists("output/interface_updates.json"));
        Assert.True(context.FileSystem.FileExists(
            "game_state/control/validation_diagnostic_failure_report.json"));
    }

    [Theory]
    [MemberData(nameof(EffectOwnerCarrierFailurePaths))]
    public async Task EffectMaterializationLifecycleTests_OwnerCarrierPublicationFailureRestoresEntireTrackedSet(
        string carrierPath)
    {
        var probe = new EffectPublicationFailureProbe();
        await using var context = await EffectMaterializationTestContext.CreateAsync(
            new FileSystemManagerHooks
            {
                AfterPhysicalFilePublishedAsync = probe.AfterPhysicalFilePublishedAsync
            });
        var command = await SeedEffectOwnerCarrierScenarioAsync(context, carrierPath);
        var engine = CreateGameEngine(fileSystem: context.FileSystem);
        var rollbackSnapshot = await InvokePrivateTaskResultAsync(
            engine,
            "CreatePreTurnBackup",
            "effect_owner_carrier_" + SanitizeFailureStage(carrierPath));
        var baseline = await CaptureRollbackTrackedSetAsync(engine, context.FileSystem);
        var backups = await context.ReadPendingSnapshotBackupsAsync();
        await context.WriteJsonAsync(
            EffectAcceptedTurnPlan.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(command));
        await context.WriteJsonAsync(
            "output/narrative_response.json",
            new JsonObject
            {
                ["response"] = "Непринятое последствие коснулось участника сцены.",
                ["timestamp"] = "2026-08-22T02:40:00Z"
            });
        await context.WriteJsonAsync(
            "output/interface_updates.json",
            new JsonObject
            {
                ["dialogueOptions"] = new JsonArray(new JsonObject
                {
                    ["text"] = "Продолжить",
                    ["category"] = "continue"
                }),
                ["timestamp"] = "2026-08-22T02:40:00Z"
            });

        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();
        Assert.True(
            issues.All(static issue => issue.Severity != IssueSeverity.Error),
            string.Join(Environment.NewLine, issues.Select(static issue =>
                $"{issue.Code}: {issue.FilePath}; expected={issue.Expected}; actual={issue.Actual}")));
        probe.ArmAfterPhysicalPublication(context.FileSystem, carrierPath);

        await Assert.ThrowsAnyAsync<Exception>(() =>
            context.NormalizeAcceptedEffectsAsync(backups));
        Assert.True(probe.Triggered);
        await context.FileSystem.WriteFileAtomicAsync(
            "game_state/control/validation_diagnostic_failure_report.json",
            $"{{\"carrierPath\":\"{carrierPath}\"}}");

        await InvokePrivateTaskAsync(
            engine,
            "RollbackRejectedAcceptedTurnAsync",
            rollbackSnapshot,
            string.Empty);

        Assert.Equal(
            baseline,
            await CaptureRollbackTrackedSetAsync(engine, context.FileSystem));
        Assert.False(context.FileSystem.FileExists("output/narrative_response.json"));
        Assert.False(context.FileSystem.FileExists("output/interface_updates.json"));
        Assert.True(context.FileSystem.FileExists(
            "game_state/control/validation_diagnostic_failure_report.json"));
    }

    [Fact]
    public async Task EffectMaterializationLifecycleTests_PendingResolutionPublicationFailureRestoresEntireTrackedSet()
    {
        var probe = new EffectPublicationFailureProbe();
        await using var context = await EffectMaterializationTestContext.CreateAsync(
            new FileSystemManagerHooks
            {
                AfterPhysicalFilePublishedAsync = probe.AfterPhysicalFilePublishedAsync
            });
        await SeedBoundedEffectResolutionAsync(context);
        var engine = CreateGameEngine(fileSystem: context.FileSystem);
        var rollbackSnapshot = await InvokePrivateTaskResultAsync(
            engine,
            "CreatePreTurnBackup",
            "effect_pending_resolution_publication");
        var baseline = await CaptureRollbackTrackedSetAsync(engine, context.FileSystem);
        var backups = await context.ReadPendingSnapshotBackupsAsync();
        await context.WriteJsonAsync(
            "output/narrative_response.json",
            new JsonObject
            {
                ["response"] = "Непринятое последствие ожидает решения рассказчика.",
                ["timestamp"] = "2026-08-22T02:50:00Z"
            });
        await context.WriteJsonAsync(
            "output/interface_updates.json",
            new JsonObject
            {
                ["dialogueOptions"] = new JsonArray(),
                ["timestamp"] = "2026-08-22T02:50:00Z"
            });

        var issues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();
        Assert.True(
            issues.All(static issue => issue.Severity != IssueSeverity.Error),
            string.Join(Environment.NewLine, issues.Select(static issue =>
                $"{issue.Code}: {issue.FilePath}; expected={issue.Expected}; actual={issue.Actual}")));
        probe.ArmAfterPhysicalPublication(
            context.FileSystem,
            ResourcePendingResolutionState.PendingPath);

        await Assert.ThrowsAnyAsync<Exception>(() =>
            context.NormalizeAcceptedEffectsAsync(backups));
        Assert.True(probe.Triggered);
        await context.FileSystem.WriteFileAtomicAsync(
            "game_state/control/validation_diagnostic_failure_report.json",
            "{\"failure\":\"pending effect publication\"}");

        await InvokePrivateTaskAsync(
            engine,
            "RollbackRejectedAcceptedTurnAsync",
            rollbackSnapshot,
            string.Empty);

        Assert.Equal(
            baseline,
            await CaptureRollbackTrackedSetAsync(engine, context.FileSystem));
        Assert.False(context.FileSystem.FileExists(
            ResourcePendingResolutionState.PendingPath));
        Assert.False(context.FileSystem.FileExists("output/narrative_response.json"));
        Assert.False(context.FileSystem.FileExists("output/interface_updates.json"));
        Assert.True(context.FileSystem.FileExists(
            "game_state/control/validation_diagnostic_failure_report.json"));
    }

    private static async Task SeedBoundedEffectResolutionAsync(
        EffectMaterializationTestContext context)
    {
        await context.SeedMortalPlayerResourcesAsync(turn: 42);
        var definition = EffectMaterializationTestFixture.CreateDefinition("periodic_damage");
        definition["triggers"]![0]!["resolutionMode"] = "bounded_receipt";
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect(
            ownerKind: "player",
            profile: "periodic_damage");
        effect["triggers"]![0]!["resolutionMode"] = "bounded_receipt";
        effect["lifetime"]!["remainingTurns"] = 2;
        await context.SeedPlayerWoundSourceAsync(definition);
        await context.WriteJsonAsync(
            EffectCarrierCatalog.PlayerPath,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["activeEffects"] = new JsonArray(effect.DeepClone())
            });
        await context.WriteJsonAsync(
            EffectAcceptedTurnPlan.IdentityIndexPath,
            EffectMaterializationTestFixture.CreateIdentityIndex(effect));
        await context.CaptureValidatedPendingSnapshotAsync(turn: 43);
    }

    private static async Task<JsonObject> SeedEffectOwnerCarrierScenarioAsync(
        EffectMaterializationTestContext context,
        string carrierPath)
    {
        if (string.Equals(carrierPath, EffectCarrierCatalog.NpcPath, StringComparison.Ordinal))
        {
            await context.SeedPlayerWoundSourceAsync(CreateNonResourceEffectDefinition());
            await context.WriteJsonAsync(
                "game_state/npcs/npc_core.json",
                new JsonObject
                {
                    ["NPCsInScene"] = new JsonArray(new JsonObject
                    {
                        ["NPCId"] = "npc_test_healer"
                    })
                });
            await context.WriteJsonAsync(
                EffectCarrierCatalog.NpcPath,
                new JsonObject
                {
                    ["schemaVersion"] = 1,
                    ["entries"] = new JsonArray(new JsonObject
                    {
                        ["NPCId"] = "npc_test_healer",
                        ["activeEffects"] = new JsonArray()
                    })
                });
            await context.CaptureValidatedPendingSnapshotAsync();
            return CreateNonResourceEffectApply("npc");
        }

        if (string.Equals(carrierPath, EffectCarrierCatalog.EnemiesPath, StringComparison.Ordinal) ||
            string.Equals(carrierPath, EffectCarrierCatalog.AlliesPath, StringComparison.Ordinal))
        {
            await context.SeedPlayerWoundSourceAsync(CreateNonResourceEffectDefinition());
            var collection = string.Equals(
                    carrierPath,
                    EffectCarrierCatalog.EnemiesPath,
                    StringComparison.Ordinal)
                ? "enemiesData"
                : "alliesData";
            await context.WriteJsonAsync(
                carrierPath,
                new JsonObject
                {
                    [collection] = new JsonArray(new JsonObject
                    {
                        ["combatantId"] = EffectMaterializationTestFixture.CombatantId,
                        ["initiative"] = 12,
                        ["activeBuffs"] = new JsonArray(),
                        ["activeDebuffs"] = new JsonArray()
                    })
                });
            await context.CaptureValidatedPendingSnapshotAsync();
            return CreateNonResourceEffectApply("combatant");
        }

        if (string.Equals(
                carrierPath,
                EffectCarrierCatalog.AfterlifeProfilesPath,
                StringComparison.Ordinal))
        {
            const string actorId = "resident_effect_rollback_target";
            const string sourceArtId = "art_effect_rollback_profile";
            var definition = CreateNonResourceEffectDefinition();
            definition["allowedRealms"] = new JsonArray("shining_abode");
            definition["allowedTargetKinds"] = new JsonArray("resident");
            await context.MaterializeAfterlifeActorAsync(
                "resident",
                actorId,
                "Shining Abode",
                sourceArtId,
                definition);
            await context.CaptureValidatedPendingSnapshotAsync(
                turn: 43,
                currentRealm: "Shining Abode");
            var command = CreateNonResourceEffectApply("resident");
            command["target"]!["targetId"] = actorId;
            command["source"] = new JsonObject
            {
                ["kind"] = "spiritual_art",
                ["sourceId"] = sourceArtId,
                ["definitionKey"] = EffectMaterializationTestFixture.DefinitionKey
            };
            command["eventRef"]!["authorityId"] = "turn_43";
            return command;
        }

        if (string.Equals(
                carrierPath,
                EffectCarrierCatalog.SpiritualConflictPath,
                StringComparison.Ordinal))
        {
            const string conflictId = "afterlife_conflict_effect_rollback";
            const string sourceArtId = "art_effect_rollback_condition";
            await context.MaterializeAfterlifeActorAsync(
                "player_soul",
                "player_soul",
                "Shining Abode",
                sourceArtId,
                CreateSpiritualRollbackDefinition());
            var spiritualConflict = CreateSpiritualRollbackConflict(conflictId);
            var resourcePlan = await AfterlifeOwnerResourceStateService.BuildAsync(
                context.FileSystem,
                new AfterlifeOwnerResourceAcceptedState(
                    SpiritualConflict: spiritualConflict),
                turn: 42);
            Assert.True(
                resourcePlan.IsValid,
                string.Join(Environment.NewLine, resourcePlan.Issues));
            Assert.True(await AfterlifeOwnerResourceStateService.TryCommitAsync(
                context.FileSystem,
                resourcePlan));
            await context.CaptureValidatedPendingSnapshotAsync(
                turn: 43,
                currentRealm: "Shining Abode");
            var command = EffectMaterializationTestFixture.CreateApplyCommand("guardian");
            command["target"] = new JsonObject
            {
                ["kind"] = "spiritual_conflict_side",
                ["targetId"] = conflictId + ":opposition"
            };
            command["source"] = new JsonObject
            {
                ["kind"] = "spiritual_art",
                ["sourceId"] = sourceArtId,
                ["definitionKey"] = EffectMaterializationTestFixture.DefinitionKey
            };
            command["parameters"] = new JsonObject();
            command["eventRef"]!["authorityId"] = "turn_43";
            return command;
        }

        throw new ArgumentOutOfRangeException(nameof(carrierPath), carrierPath, null);
    }

    private static JsonObject CreateNonResourceEffectDefinition() =>
        EffectMaterializationTestFixture.CreateDefinition("action_control");

    private static JsonObject CreateNonResourceEffectApply(string targetKind)
    {
        var command = EffectMaterializationTestFixture.CreateApplyCommand(targetKind);
        command["parameters"] = new JsonObject();
        return command;
    }

    private static JsonObject CreateSpiritualRollbackDefinition()
    {
        var definition = EffectMaterializationTestFixture.CreateDefinition(
            "afterlife_combat_condition");
        definition["allowedRealms"] = new JsonArray("shining_abode");
        definition["allowedTargetKinds"] = new JsonArray("spiritual_conflict_side");
        definition["display"]!["name"] = "Печать отката";
        definition["display"]!["description"] =
            "Печать проверяет атомарность духовного последствия.";
        definition["lifetime"] = new JsonObject
        {
            ["mode"] = "uses",
            ["initialUses"] = 2,
            ["consumingEventTypes"] = new JsonArray("afterlife_exchange_end")
        };
        definition["triggers"] = new JsonArray(new JsonObject
        {
            ["triggerId"] = "condition_exchange_consumed",
            ["eventType"] = "afterlife_exchange_end",
            ["priority"] = 100,
            ["componentIds"] = new JsonArray("component_001"),
            ["consumeUses"] = true,
            ["resolutionMode"] = "deterministic"
        });
        var payload = definition["components"]![0]!["payload"]!.AsObject();
        payload["conditionKind"] = "burden";
        payload["targetSide"] = "opposition";
        payload["actorId"] = "guardian_condition_source";
        payload["operations"] = new JsonArray("pressure");
        payload["axes"] = new JsonArray("rollMode");
        payload["counterplay"] = new JsonArray("Ответить действием guard или counter.");
        payload["payoff"] = "impose_disadvantage";
        return definition;
    }

    private static JsonObject CreateSpiritualRollbackConflict(string conflictId) =>
        new()
        {
            ["schemaVersion"] = 1,
            ["activeConflict"] = new JsonObject
            {
                ["dangerMode"] = "hostile",
                ["conflictId"] = conflictId,
                ["realm"] = "Shining Abode",
                ["sideModel"] = "direct_duel",
                ["status"] = "active",
                ["resolutionState"] = "active",
                ["conflictPosition"] = "contested",
                ["playerSideStrain"] = "clear",
                ["oppositionSideStrain"] = "clear",
                ["playerSide"] = new JsonObject
                {
                    ["leadContestant"] = new JsonObject
                    {
                        ["actorType"] = "player",
                        ["actorId"] = "player_soul",
                        ["displayName"] = "Асуран"
                    },
                    ["supporters"] = new JsonArray()
                },
                ["oppositionSide"] = new JsonObject
                {
                    ["leadContestant"] = new JsonObject
                    {
                        ["actorType"] = "guardian",
                        ["actorId"] = "guardian_condition_source",
                        ["displayName"] = "Хранитель печати",
                        ["actorArtTierSnapshot"] = new JsonObject
                        {
                            ["pressure"] = 2
                        },
                        ["artAuthoritySource"] = "guardian_state"
                    },
                    ["supporters"] = new JsonArray(),
                    ["resourceMaterialization"] = new JsonObject
                    {
                        ["resources"] = new JsonArray(new JsonObject
                        {
                            ["resourceKey"] = "spiritual_action_points",
                            ["maximum"] = 6
                        })
                    }
                },
                ["combatConditions"] = new JsonArray(),
                ["exchangeLog"] = new JsonArray()
            },
            ["recentConflicts"] = new JsonArray()
        };

    private static string SanitizeFailureStage(string value) =>
        new(value.Select(static character =>
            char.IsLetterOrDigit(character) ? character : '_').ToArray());

    private static async Task<IReadOnlyDictionary<string, string>>
        CaptureRollbackTrackedSetAsync(
            GameEngine engine,
            FileSystemManager fileSystem)
    {
        await using var writeLease = await fileSystem.AcquireCanonicalWriteLeaseAsync();
        var method = ResolvePrivateMethod(
            engine,
            "EnumerateRollbackTrackedFiles",
            new object?[] { writeLease });
        var enumerable = Assert.IsAssignableFrom<IEnumerable<string>>(
            method.Invoke(engine, new object?[] { writeLease }));
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in enumerable.OrderBy(static value => value, StringComparer.Ordinal))
        {
            var bytes = await fileSystem.ReadFileBytesAsync(writeLease, path);
            Assert.NotNull(bytes);
            result[path] = Convert.ToBase64String(bytes!);
        }
        return result;
    }

    private sealed class EffectPublicationFailureProbe
    {
        private string? _afterPhysicalPath;
        private string? _corruptAfterPhysicalPath;
        private string? _corruptPath;

        internal bool Triggered { get; private set; }

        internal void ArmAfterPhysicalPublication(
            FileSystemManager fileSystem,
            string path)
        {
            _afterPhysicalPath = fileSystem.ResolvePath(path);
            _corruptAfterPhysicalPath = null;
            _corruptPath = null;
            Triggered = false;
        }

        internal void ArmPostCheckCorruption(
            FileSystemManager fileSystem,
            string afterPublishedPath,
            string corruptPath)
        {
            _afterPhysicalPath = null;
            _corruptAfterPhysicalPath = fileSystem.ResolvePath(afterPublishedPath);
            _corruptPath = fileSystem.ResolvePath(corruptPath);
            Triggered = false;
        }

        internal Task AfterPhysicalFilePublishedAsync(string path)
        {
            if (_corruptAfterPhysicalPath != null &&
                string.Equals(
                    path,
                    _corruptAfterPhysicalPath,
                    StringComparison.OrdinalIgnoreCase))
            {
                var corruptPath = _corruptPath ?? throw new InvalidOperationException(
                    "Post-check corruption path is missing.");
                _corruptAfterPhysicalPath = null;
                _corruptPath = null;
                File.WriteAllText(
                    corruptPath,
                    "{\"injected\":\"post-check mismatch\"}",
                    Encoding.UTF8);
                Triggered = true;
                return Task.CompletedTask;
            }
            if (_afterPhysicalPath == null ||
                !string.Equals(
                    path,
                    _afterPhysicalPath,
                    StringComparison.OrdinalIgnoreCase))
            {
                return Task.CompletedTask;
            }
            _afterPhysicalPath = null;
            Triggered = true;
            return Task.FromException(new IOException(
                $"Injected failure after effect publication '{path}'."));
        }

        internal Task AfterCanonicalReadAttemptAsync(string path) =>
            Task.CompletedTask;
    }
}
