using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookOfEternityClient.Tests;

internal sealed class EffectMaterializationTestContext : IAsyncDisposable
{
    internal const string PlayerEffectsPath = "game_state/player/effects.json";
    internal const string NpcEffectsPath = "game_state/npcs/npc_effects.json";
    internal const string EnemyCombatantsPath = "game_state/combat/enemies.json";
    internal const string AllyCombatantsPath = "game_state/combat/allies.json";
    internal const string AfterlifeProfilesPath = "game_state/meta/afterlife_entity_profiles.json";
    internal const string SpiritualConflictPath = "game_state/meta/afterlife_spiritual_conflict_state.json";
    internal const string IdentityIndexPath = "game_state/effects/effect_identity_index.json";
    internal const string CommandPath = "game_state/effects/effect_commands.json";
    internal const string PendingResolutionPath = "game_state/control/pending_effect_resolutions.json";
    internal const string PlayerWoundsPath = "game_state/player/wounds.json";

    internal static readonly string[] OwnedPaths =
    {
        PlayerEffectsPath,
        NpcEffectsPath,
        EnemyCombatantsPath,
        AllyCombatantsPath,
        AfterlifeProfilesPath,
        SpiritualConflictPath,
        IdentityIndexPath,
        CommandPath,
        PendingResolutionPath
    };

    private readonly string _expectedTempRoot;

    private EffectMaterializationTestContext(
        string rootPath,
        FileSystemManagerHooks? hooks = null)
    {
        RootPath = Path.GetFullPath(rootPath);
        _expectedTempRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

        Directory.CreateDirectory(RootPath);
        FileSystem = new FileSystemManager(
            RootPath,
            NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance,
            hooks);
        FileSystem.EnsureDirectoryStructure();
        Validator = new ValidationService(
            FileSystem,
            NullLogger<ValidationService>.Instance);
        Normalizer = new CanonicalStateNormalizer(
            FileSystem,
            NullLogger<CanonicalStateNormalizer>.Instance);
    }

    internal FileSystemManager FileSystem { get; }

    internal ValidationService Validator { get; }

    internal CanonicalStateNormalizer Normalizer { get; }

    internal string RootPath { get; }

    internal static async Task<EffectMaterializationTestContext> CreateAsync(
        FileSystemManagerHooks? hooks = null)
    {
        var rootPath = Path.Combine(
            Path.GetTempPath(),
            "boe-effect-materialization-" + Guid.NewGuid().ToString("N"));
        var context = new EffectMaterializationTestContext(rootPath, hooks);
        await context.SeedMortalPlayerResourcesAsync(turn: 41);
        return context;
    }

    internal async Task<EffectAcceptedTurnPlan?> NormalizeAcceptedEffectsAsync(
        IReadOnlyDictionary<string, string>? backups)
    {
        await using var writeLease = await FileSystem.AcquireCanonicalWriteLeaseAsync();
        var plan = await Normalizer.BindTo(writeLease)
            .NormalizeAcceptedMechanicsAsync(backups);
        return plan?.EffectPlan;
    }

    internal async Task<AcceptedMechanicsPlan?> NormalizeAccumulatedStateWithAcceptedMechanicsAsync(
        IReadOnlyDictionary<string, string>? backups)
    {
        await using var writeLease = await FileSystem.AcquireCanonicalWriteLeaseAsync();
        return await Normalizer.BindTo(writeLease)
            .NormalizeAccumulatedStateWithPlanAsync(backups);
    }

    internal Task WriteJsonAsync(string relativePath, JsonNode value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        ArgumentNullException.ThrowIfNull(value);
        return FileSystem.WriteFileAtomicAsync(relativePath, value.ToJsonString());
    }

    internal async Task<JsonNode?> ReadJsonAsync(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        var json = await FileSystem.ReadFileAsync(relativePath);
        return string.IsNullOrWhiteSpace(json) ? null : JsonNode.Parse(json);
    }

    internal async Task CaptureValidatedPendingSnapshotAsync(
        int turn = 42,
        string currentRealm = "Mortal World")
    {
        const string sessionId = "session_effect_materialization";
        const string requestId = "request_effect_materialization";
        const string playerAction = "Validate complete effect materialization.";

        await WriteJsonAsync(
            "input/turn_request.json",
            new JsonObject
            {
                ["sessionId"] = sessionId,
                ["requestId"] = requestId,
                ["turnNumber"] = turn,
                ["currentRealm"] = currentRealm,
                ["playerAction"] = playerAction
            });

        var files = new JsonObject();
        var snapshotFileHashes = new JsonObject();
        var rollbackBaselineFiles = new JsonArray();
        var trackedPaths = CanonicalStateNormalizer.NormalizerRollbackTrackedFiles
            .Concat(OwnedPaths)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static value => value, StringComparer.Ordinal);
        foreach (var path in trackedPaths)
        {
            var bytes = await FileSystem.ReadFileBytesAsync(path);
            if (bytes == null)
                continue;

            var snapshotPath = $"game_state/control/pending_turn_snapshot/{path}";
            await FileSystem.WriteFileAtomicBytesAsync(snapshotPath, bytes);
            files[path] = snapshotPath;
            snapshotFileHashes[path] = PendingTurnSnapshotAuthority.ComputeSha256(bytes);
            rollbackBaselineFiles.Add(path);
        }

        var manifest = new JsonObject
        {
            ["sessionId"] = sessionId,
            ["requestId"] = requestId,
            ["turnNumber"] = turn,
            ["requestTimestamp"] = "2026-08-14T00:00:00Z",
            ["playerAction"] = playerAction,
            ["files"] = files,
            ["snapshotFileHashes"] = snapshotFileHashes,
            ["clientOwnedValidationHashes"] = new JsonObject(),
            ["rollbackBackups"] = new JsonObject(),
            ["rollbackBaselineFiles"] = rollbackBaselineFiles,
            ["sourceLabel"] = "Effect materialization integration test",
            ["manifestPayloadHash"] = string.Empty
        };
        manifest["manifestPayloadHash"] =
            PendingTurnSnapshotTestAuthority.ComputeManifestPayloadHash(manifest);

        await WriteJsonAsync(
            "game_state/control/pending_turn_snapshot.json",
            manifest);
        await PendingTurnSnapshotTestAuthority.SyncAuthorityForCurrentManifestAsync(FileSystem);
    }

    internal async Task<IReadOnlyDictionary<string, string?>> CaptureBytesAsync(
        params string[] paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        var result = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var path in paths.Distinct(StringComparer.Ordinal))
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            var bytes = await FileSystem.ReadFileBytesAsync(path);
            result[path] = bytes == null ? null : Convert.ToBase64String(bytes);
        }

        return result;
    }

    internal async Task<IReadOnlyDictionary<string, string>> ReadPendingSnapshotBackupsAsync()
    {
        var manifest = (await ReadJsonAsync(
            "game_state/control/pending_turn_snapshot.json"))?.AsObject()
            ?? throw new InvalidOperationException("Pending-turn snapshot manifest is missing.");
        var files = manifest["files"]?.AsObject()
            ?? throw new InvalidOperationException("Pending-turn snapshot file map is missing.");
        return files.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value?.GetValue<string>()
                ?? throw new InvalidOperationException("Pending-turn snapshot path is null."),
            StringComparer.Ordinal);
    }

    internal async Task SeedMortalPlayerResourcesAsync(int turn = 42)
    {
        var resources = ResourceBootstrapStateBuilder.BuildMortalPlayer(
            incarnationNumber: 1,
            turn,
            permanentStrength: 10,
            permanentConstitution: 10,
            permanentIntelligence: 10,
            permanentWisdom: 10,
            permanentFaith: 10);
        if (!resources.IsValid)
        {
            throw new InvalidOperationException(
                string.Join(Environment.NewLine, resources.Issues));
        }

        await WriteJsonAsync(
            ResourceMaterializationContract.DefinitionsPath,
            JsonNode.Parse(resources.Definitions!.ToCanonicalJson())!);
        await WriteJsonAsync(
            ResourceMaterializationContract.StatePath,
            JsonNode.Parse(resources.State!.ToCanonicalJson())!);
        await WriteJsonAsync(
            ResourceMaterializationContract.HistoryPath,
            JsonNode.Parse(resources.History!.ToCanonicalJson())!);
    }

    internal Task SeedPlayerWoundSourceAsync(JsonObject? definition = null)
    {
        return WriteJsonAsync(
            PlayerWoundsPath,
            new JsonArray(new JsonObject
            {
                ["woundId"] = "wound_test_torn_side",
                ["woundName"] = "Рваная рана в боку",
                ["severity"] = "severe",
                ["description"] = "Края раны снова разошлись.",
                ["activeEffectDefinitions"] = new JsonArray(
                    definition ?? EffectMaterializationTestFixture.CreateDefinition())
            }));
    }

    internal Task SyncPendingSnapshotAuthorityAsync() =>
        PendingTurnSnapshotTestAuthority.SyncAuthorityForCurrentManifestAsync(FileSystem);

    internal async Task<JsonObject> MaterializeAfterlifeActorAsync(
        string actorType,
        string actorId,
        string realm,
        string sourceArtId,
        JsonObject sourceDefinition,
        decimal resourceMaximum = 10m)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorType);
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);
        ArgumentException.ThrowIfNullOrWhiteSpace(realm);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceArtId);
        ArgumentNullException.ThrowIfNull(sourceDefinition);

        var bootstrap = ResourceBootstrapStateBuilder.BuildPristine();
        if (!bootstrap.IsValid)
        {
            throw new InvalidOperationException(
                string.Join(Environment.NewLine, bootstrap.Issues));
        }

        var definitions = WithAfterlifeIntegrity(bootstrap.Definitions!);
        await WriteJsonAsync(
            ResourceMaterializationContract.DefinitionsPath,
            JsonNode.Parse(definitions.ToCanonicalJson())!);
        await WriteJsonAsync(
            ResourceMaterializationContract.StatePath,
            JsonNode.Parse(bootstrap.State!.ToCanonicalJson())!);
        await WriteJsonAsync(
            ResourceMaterializationContract.HistoryPath,
            JsonNode.Parse(bootstrap.History!.ToCanonicalJson())!);
        await WriteJsonAsync(
            AfterlifeProfilesPath,
            AfterlifeEntityProfileState.CreateDefaultRoot());
        await WriteJsonAsync(
            "game_state/meta/soul_state.json",
            new JsonObject
            {
                ["currentRealm"] = realm,
                [AfterlifeSpiritualConflictState.SoulStateProfileProperty] = new JsonObject
                {
                    [AfterlifeSpiritualConflictState.SpiritFocusTierProperty] = 0
                }
            });
        var acceptedRoot = AfterlifeEntityProfileState.CreateDefaultRoot();
        var profile = AfterlifeActorMaterializationTestFixture.CreateCompleteProfile(
            actorType,
            actorId,
            realm,
            materializedAtTurn: 42,
            specialArts: new JsonArray(new JsonObject
            {
                ["artId"] = sourceArtId,
                ["displayName"] = "Печать точного следа",
                ["effectDescription"] = "Закрепляет проверяемое духовное последствие.",
                ["owner"] = actorId,
                ["baseOperation"] = "guard",
                ["activeEffectDefinitions"] = new JsonArray(sourceDefinition.DeepClone())
            }));
        profile["activeEffects"] = new JsonArray();
        profile["resourceMaterialization"] = new JsonObject
        {
            ["resources"] = new JsonArray(new JsonObject
            {
                ["resourceKey"] = "soul_integrity",
                ["maximum"] = resourceMaximum
            })
        };
        if (string.Equals(actorType, "player_soul", StringComparison.Ordinal))
        {
            profile.Remove(ActorMaterializationContract.PropertyName);
            profile.Remove("resourceMaterialization");
            if (!AfterlifeEntityProfileState.TryNormalizeEffectRealm(
                    realm,
                    out var effectRealm))
            {
                throw new InvalidOperationException(
                    $"Unsupported player_soul effect realm '{realm}'.");
            }
            profile[AfterlifeEntityProfileState.ResourceOwnerBindingsProperty] =
                new JsonArray(new JsonObject
                {
                    ["realm"] = effectRealm,
                    ["resourceOwnerId"] = actorId,
                    ["state"] = "active"
                });
            acceptedRoot[AfterlifeEntityProfileState.ProfilesProperty] =
                new JsonArray(profile);
            await WriteJsonAsync(AfterlifeProfilesPath, acceptedRoot);
            var resourcePlan = await AfterlifeOwnerResourceStateService.BuildAsync(
                FileSystem,
                new AfterlifeOwnerResourceAcceptedState(
                    SoulState: new JsonObject
                    {
                        ["currentRealm"] = realm,
                        [AfterlifeSpiritualConflictState.SoulStateProfileProperty] =
                            new JsonObject
                            {
                                [AfterlifeSpiritualConflictState.SpiritFocusTierProperty] = 0
                            }
                    }),
                turn: 42);
            if (!resourcePlan.IsValid)
            {
                throw new InvalidOperationException(string.Join(
                    Environment.NewLine,
                    resourcePlan.Issues));
            }
            if (!await AfterlifeOwnerResourceStateService.TryCommitAsync(
                    FileSystem,
                    resourcePlan))
            {
                throw new InvalidOperationException(
                    "Expected player_soul resource bootstrap to commit.");
            }
            await CaptureValidatedPendingSnapshotAsync(
                turn: 42,
                currentRealm: realm);
            return profile.DeepClone().AsObject();
        }

        await CaptureValidatedPendingSnapshotAsync(turn: 42, currentRealm: realm);
        acceptedRoot[AfterlifeEntityProfileState.ResponseProfilesProperty] =
            new JsonArray(profile);
        await WriteJsonAsync(AfterlifeProfilesPath, acceptedRoot);

        var issues = await Validator.ValidateAcceptedTurnRawResourceMaterializationAsync();
        var errors = issues.Where(issue => issue.Severity == IssueSeverity.Error).ToArray();
        if (errors.Length > 0)
        {
            throw new InvalidOperationException(string.Join(
                Environment.NewLine,
                errors.Select(issue =>
                    $"{issue.Code}: expected={issue.Expected}; actual={issue.Actual}")));
        }

        await using (var writeLease = await FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            var plan = await Normalizer.BindTo(writeLease)
                .NormalizeAcceptedMechanicsAsync(backups: null);
            if (plan == null)
                throw new InvalidOperationException("Expected afterlife actor materialization plan.");
        }

        var canonicalRoot = (await ReadJsonAsync(AfterlifeProfilesPath))?.AsObject()
            ?? throw new InvalidOperationException("Expected canonical afterlife profile root.");
        return canonicalRoot[AfterlifeEntityProfileState.ProfilesProperty]!
            .AsArray()
            .OfType<JsonObject>()
            .Single(candidate => string.Equals(
                candidate["actorId"]?.GetValue<string>(),
                actorId,
                StringComparison.Ordinal))
            .DeepClone()
            .AsObject();
    }

    private static ResourceDefinitionCatalog WithAfterlifeIntegrity(
        ResourceDefinitionCatalog definitions)
    {
        var proposal = new JsonObject
        {
            ["resourceKey"] = "soul_integrity",
            ["definitionVersion"] = 1,
            ["displayName"] = "Целостность души",
            ["numericKind"] = "integer",
            ["unit"] = "point",
            ["quantum"] = 1,
            ["minimumPolicy"] = new JsonObject
            {
                ["kind"] = "definition_fixed",
                ["value"] = 0
            },
            ["capacityPolicy"] = new JsonObject { ["kind"] = "instance_fixed" },
            ["initializationPolicy"] = new JsonObject { ["kind"] = "maximum" },
            ["allowedOwnerKinds"] = new JsonArray("afterlife_actor"),
            ["allowedOperations"] = new JsonArray("damage", "restore"),
            ["defaultFloorPolicy"] = "clamp_to_minimum",
            ["defaultCapPolicy"] = "clamp_to_maximum",
            ["visibility"] = "owner_visible"
        };
        using var document = JsonDocument.Parse(proposal.ToJsonString());
        var materialized = ResourceDefinitionCatalog.MaterializeProposal(
            document.RootElement,
            definitions,
            createdAtTurn: 1,
            createdEventRef: "turn_1:resource_definition:1",
            static () => new ResourceDefinitionIdentity(
                "resource_definition_soul_integrity",
                "resource_definition_seal_soul_integrity"));
        if (!materialized.IsValid)
        {
            throw new InvalidOperationException(
                string.Join(Environment.NewLine, materialized.Issues));
        }

        return definitions.With(materialized.Definition!);
    }

    public ValueTask DisposeAsync()
    {
        if (!RootPath.StartsWith(_expectedTempRoot, StringComparison.OrdinalIgnoreCase) ||
            !Path.GetFileName(RootPath).StartsWith(
                "boe-effect-materialization-",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Refusing to remove unexpected effect test root '{RootPath}'.");
        }

        try
        {
            if (Directory.Exists(RootPath))
                Directory.Delete(RootPath, recursive: true);
        }
        catch (IOException)
        {
            // A later run can reclaim an isolated temp root left by an open handle.
        }
        catch (UnauthorizedAccessException)
        {
            // Keep cleanup best-effort without hiding test assertions.
        }

        return ValueTask.CompletedTask;
    }
}
