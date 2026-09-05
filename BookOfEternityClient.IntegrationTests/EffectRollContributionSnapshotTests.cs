using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

[Trait("Category", "RegressionIntegration")]
public sealed class EffectRollContributionSnapshotTests
{
    private const string ActivePath = "game_state/player/skills_active.json";
    private const string TemporaryRootPrefix = "boe-roll-contribution-";
    private static readonly EffectRollContext PlayerSkillCheck =
        new("mortal_world", "player", "player_current", "skill_check", "skill_lockpicking");

    [Fact]
    public async Task Resolve_LoadAsyncMalformedSkillRoot_FailsClosedWithSnapshotDiagnostics()
    {
        var root = CreateTemporaryRoot();
        try
        {
            var fileSystem = CreateFileSystem(root);
            await fileSystem.WriteFileAtomicAsync(ActivePath, "[]");

            var snapshot = await EffectMechanicsSnapshot.LoadAsync(fileSystem);
            var resolution = EffectRollContributionResolver.Resolve(
                snapshot with
                {
                    Components = new ReadOnlyCollection<EffectMechanicalComponent>(new[]
                    {
                        Component("effect_load_failure", "component_load_failure", "advantage", Scope("all"))
                    })
                },
                PlayerSkillCheck);

            Assert.False(snapshot.IsAccepted);
            Assert.Contains(snapshot.Issues, issue => issue.Code == "effect_mechanics_invalid_authority_root");
            Assert.False(resolution.IsValid);
            Assert.Equal("normal", resolution.RollMode);
            Assert.Empty(resolution.Contributions);
            Assert.Contains(resolution.Issues, issue => issue.Code == "effect_mechanics_invalid_authority_root");
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [Fact]
    public async Task Resolve_LoadAsyncWithExistingPublicationLease_UsesExactCurrentSkillAuthority()
    {
        var root = CreateTemporaryRoot();
        try
        {
            var readPaths = new List<string>();
            var fileSystem = CreateFileSystem(root, new FileSystemManagerHooks
            {
                BeforeCanonicalReadOpenAsync = path =>
                {
                    readPaths.Add(path);
                    return Task.CompletedTask;
                }
            });
            await fileSystem.WriteFileAtomicAsync(ActivePath, RootWithSkills(Skill("skill_lockpicking", active: true)).ToJsonString());
            await fileSystem.WriteFileAtomicAsync("game_state/player/skills_passive.json", "{\"passiveSkillChanges\":[]}");
            await fileSystem.WriteFileAtomicAsync("game_state/npcs/npc_core.json", "{\"UpdateNPCs\":[]}");

            await using var lease = await fileSystem.AcquireCanonicalWriteLeaseAsync(
                CanonicalWritePurpose.PublicationReadQuiescence);
            var snapshot = await EffectMechanicsSnapshot.LoadAsync(fileSystem, lease);
            var resolution = EffectRollContributionResolver.Resolve(
                snapshot with
                {
                    Components = new ReadOnlyCollection<EffectMechanicalComponent>(new[]
                    {
                        Component("effect_loaded", "component_loaded", "advantage", Scope("skill", "skill_lockpicking"))
                    })
                },
                PlayerSkillCheck);

            Assert.True(snapshot.IsAccepted);
            Assert.Equal("advantage", resolution.RollMode);
            Assert.Single(resolution.Contributions);
            Assert.Equal(1, readPaths.Count(path => string.Equals(path, ActivePath, StringComparison.Ordinal)));
            Assert.Equal(1, readPaths.Count(path => string.Equals(path, "game_state/player/skills_passive.json", StringComparison.Ordinal)));
            Assert.Equal(1, readPaths.Count(path => string.Equals(path, "game_state/npcs/npc_core.json", StringComparison.Ordinal)));
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    private static EffectMechanicalComponent Component(
        string effectId,
        string componentId,
        string contribution,
        JsonObject scope)
    {
        var scopeKind = scope["kind"]!.GetValue<string>();
        var payload = string.Equals(scopeKind, "all", StringComparison.Ordinal)
            ? EffectMaterializationTestFixture.CreateBroadRollModifierPayload(
                contribution,
                "skill_check")
            : EffectMaterializationTestFixture.CreateFocusedRollModifierPayload(
                scope["skillId"]!.GetValue<string>(),
                contribution);
        using var document = JsonDocument.Parse(payload.ToJsonString());
        return new EffectMechanicalComponent(
            effectId,
            "mortal_world",
            "player",
            "player_current",
            true,
            "Effect",
            string.Empty,
            componentId,
            "roll_modifier",
            0,
            1,
            document.RootElement.Clone());
    }

    private static JsonObject Scope(string kind, string? skillId = null)
    {
        var scope = new JsonObject { ["kind"] = kind };
        if (skillId != null)
            scope["skillId"] = skillId;
        return scope;
    }

    private static JsonObject RootWithSkills(params JsonObject[] skills) => new()
    {
        ["activeSkillChanges"] = new JsonArray(
            skills.Select(static skill => (JsonNode?)skill.DeepClone()).ToArray())
    };

    private static JsonObject Skill(string id, bool active) => new()
    {
        ["skillId"] = id,
        ["skillName"] = "Навык",
        ["lifecycle"] = active ? "active" : "inactive",
        ["active"] = active
    };

    private static string CreateTemporaryRoot() =>
        Path.GetFullPath(Path.Combine(
            Path.GetTempPath(),
            TemporaryRootPrefix + Guid.NewGuid().ToString("N")));

    private static FileSystemManager CreateFileSystem(
        string root,
        FileSystemManagerHooks? hooks = null)
    {
        var fileSystem = new FileSystemManager(
            root,
            NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance,
            hooks);
        fileSystem.EnsureDirectoryStructure();
        return fileSystem;
    }

    private static void DeleteTemporaryRoot(string root)
    {
        var candidate = Path.GetFullPath(root)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var expectedParent = Path.GetFullPath(Path.GetTempPath())
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var candidateParent = Path.GetDirectoryName(candidate)?.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);
        var candidateName = Path.GetFileName(candidate);
        var candidateId = candidateName.StartsWith(
            TemporaryRootPrefix,
            StringComparison.Ordinal)
            ? candidateName[TemporaryRootPrefix.Length..]
            : string.Empty;

        if (!string.Equals(candidateParent, expectedParent, StringComparison.OrdinalIgnoreCase) ||
            !Guid.TryParseExact(candidateId, "N", out _))
        {
            throw new InvalidOperationException(
                $"Refusing to delete unowned roll-contribution root '{candidate}'.");
        }

        if (Directory.Exists(candidate))
            Directory.Delete(candidate, recursive: true);
    }
}
