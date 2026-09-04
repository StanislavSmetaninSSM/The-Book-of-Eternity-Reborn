using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class EffectRollContributionResolverTests
{
    private const string ActivePath = "game_state/player/skills_active.json";
    private static readonly EffectRollContext PlayerSkillCheck =
        new("mortal_world", "player", "player_current", "skill_check", "skill_lockpicking");

    [Fact]
    public void Resolve_BroadMatchingContribution_IsAcceptedAsImmutableEvidence()
    {
        var resolution = EffectRollContributionResolver.Resolve(
            Snapshot(Component("effect_broad", "component_broad", "advantage", Scope("all"))),
            PlayerSkillCheck);

        Assert.True(resolution.IsValid);
        Assert.Equal("advantage", resolution.RollMode);
        var contribution = Assert.Single(resolution.Contributions);
        Assert.Equal("effect_broad", contribution.EffectId);
        Assert.Equal("component_broad", contribution.ComponentId);
        Assert.Equal("advantage", contribution.Contribution);
        Assert.Empty(resolution.Issues);
        Assert.True(Assert.IsAssignableFrom<IList<EffectRollContributionEvidence>>(resolution.Contributions).IsReadOnly);
    }

    [Fact]
    public void Resolve_RejectedSnapshot_FailsClosedAndCopiesItsDiagnostics()
    {
        var issue = new ValidationIssue(
            "game_state/player/skills_active.json",
            IssueSeverity.Error,
            "Skill authority cannot be trusted.",
            code: "effect_mechanics_authority_read_failed");
        var rejected = Snapshot(Component("effect_rejected", "component_rejected", "advantage", Scope("all"))) with
        {
            IsAccepted = false,
            Issues = new ReadOnlyCollection<ValidationIssue>(new[] { issue })
        };

        var resolution = EffectRollContributionResolver.Resolve(rejected, PlayerSkillCheck);

        Assert.False(resolution.IsValid);
        Assert.Equal("normal", resolution.RollMode);
        Assert.Empty(resolution.Contributions);
        Assert.Single(resolution.Issues);
        Assert.Equal(issue.Code, resolution.Issues[0].Code);
        Assert.NotSame(rejected.Issues, resolution.Issues);
        Assert.True(Assert.IsAssignableFrom<IList<ValidationIssue>>(resolution.Issues).IsReadOnly);
    }

    [Fact]
    public void Resolve_FocusedExactCurrentSkillMatch_IsAccepted()
    {
        var resolution = EffectRollContributionResolver.Resolve(
            Snapshot(Component("effect_focused", "component_focused", "advantage", Scope("skill", "skill_lockpicking")),
                authority: Authority(Skill("skill_lockpicking", active: true))),
            PlayerSkillCheck);

        Assert.True(resolution.IsValid);
        Assert.Equal("advantage", resolution.RollMode);
        Assert.Single(resolution.Contributions);
    }

    [Theory]
    [InlineData("skill_stealth")]
    [InlineData("SKILL_LOCKPICKING")]
    [InlineData("skill_l\u043eckpicking")]
    public void Resolve_FocusedMismatchedOrSimilarSkillId_DoesNotInherit(string selectedSkillId)
    {
        var resolution = EffectRollContributionResolver.Resolve(
            Snapshot(Component("effect_focused", "component_focused", "advantage", Scope("skill", selectedSkillId)),
                authority: Authority(Skill("skill_lockpicking", active: true))),
            PlayerSkillCheck);

        Assert.True(resolution.IsValid);
        Assert.Equal("normal", resolution.RollMode);
        Assert.Empty(resolution.Contributions);
        Assert.Empty(resolution.Issues);
    }

    [Fact]
    public void Resolve_FocusedScopeWithNullContextSkillIdentity_DoesNotContribute()
    {
        var resolution = EffectRollContributionResolver.Resolve(
            Snapshot(Component("effect_focused", "component_focused", "advantage", Scope("skill", "skill_lockpicking")),
                authority: Authority(Skill("skill_lockpicking", active: true))),
            PlayerSkillCheck with { SkillId = null });

        Assert.True(resolution.IsValid);
        Assert.Equal("normal", resolution.RollMode);
        Assert.Empty(resolution.Contributions);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Resolve_FocusedMissingOrInactiveCurrentSkill_IsDormant(bool presentButInactive)
    {
        var authority = presentButInactive
            ? Authority(Skill("skill_lockpicking", active: false))
            : Authority();

        var resolution = EffectRollContributionResolver.Resolve(
            Snapshot(Component("effect_dormant", "component_dormant", "disadvantage", Scope("skill", "skill_lockpicking")),
                authority: authority),
            PlayerSkillCheck);

        Assert.True(resolution.IsValid);
        Assert.Equal("normal", resolution.RollMode);
        Assert.Empty(resolution.Contributions);
        Assert.Empty(resolution.Issues);
    }

    [Fact]
    public void Resolve_FocusedContributionRestoresExactlyWhenCurrentSkillReturns()
    {
        var component = Component("effect_restore", "component_restore", "disadvantage", Scope("skill", "skill_lockpicking"));

        var dormant = EffectRollContributionResolver.Resolve(
            Snapshot(component, authority: Authority()), PlayerSkillCheck);
        var restored = EffectRollContributionResolver.Resolve(
            Snapshot(component, authority: Authority(Skill("skill_lockpicking", active: true))), PlayerSkillCheck);

        Assert.Equal("normal", dormant.RollMode);
        Assert.Empty(dormant.Contributions);
        Assert.Equal("disadvantage", restored.RollMode);
        Assert.Single(restored.Contributions);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Resolve_MalformedOrAmbiguousCurrentSkillAuthority_FailsClosedWithoutRollAuthority(bool confusable)
    {
        var competitor = confusable ? "SKILL_LOCKPICKING" : "skill_lockpicking";
        var resolution = EffectRollContributionResolver.Resolve(
            Snapshot(Component("effect_ambiguous", "component_ambiguous", "advantage", Scope("skill", "skill_lockpicking")),
                authority: Authority(Skill("skill_lockpicking", active: true), Skill(competitor, active: true))),
            PlayerSkillCheck);

        Assert.False(resolution.IsValid);
        Assert.Equal("normal", resolution.RollMode);
        Assert.Empty(resolution.Contributions);
        Assert.Contains(resolution.Issues, issue => issue.Code == "effect_roll_skill_scope_invalid_authority");
    }

    [Fact]
    public void Resolve_OverBoundCurrentSkillAuthority_FailsClosedWithoutRollAuthority()
    {
        var authority = Authority(Enumerable.Range(0, 129)
            .Select(index => Skill($"skill_{index:D3}", active: true))
            .ToArray());
        var context = PlayerSkillCheck with { SkillId = "skill_000" };
        var resolution = EffectRollContributionResolver.Resolve(
            Snapshot(Component("effect_bound", "component_bound", "advantage", Scope("skill", "skill_000")),
                authority: authority),
            context);

        Assert.False(resolution.IsValid);
        Assert.Equal("normal", resolution.RollMode);
        Assert.Empty(resolution.Contributions);
        Assert.Contains(resolution.Issues, issue => issue.Code == "effect_roll_skill_scope_invalid_authority");
    }

    [Fact]
    public void Resolve_BuildDefaultAuthority_LeavesFocusedContributionDormant()
    {
        var snapshot = EffectMechanicsSnapshot.Build(new EffectMechanicsInput(
            new EffectCarrierCatalogInput(null, null, null, null, null, null),
            null)) with
        {
            Components = new ReadOnlyCollection<EffectMechanicalComponent>(new[]
            {
                Component("effect_default", "component_default", "advantage", Scope("skill", "skill_lockpicking"))
            })
        };

        var resolution = EffectRollContributionResolver.Resolve(snapshot, PlayerSkillCheck);

        Assert.True(snapshot.IsAccepted);
        Assert.True(resolution.IsValid);
        Assert.Equal("normal", resolution.RollMode);
        Assert.Empty(resolution.Contributions);
    }

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

    [Theory]
    [InlineData("realm")]
    [InlineData("actor_kind")]
    [InlineData("actor_id")]
    [InlineData("operation")]
    public void Resolve_WrongRealmActorOrOperation_FiltersBeforeReduction(string mismatch)
    {
        var context = mismatch switch
        {
            "realm" => PlayerSkillCheck with { Realm = "chaos_sea" },
            "actor_kind" => PlayerSkillCheck with { ActorKind = "npc" },
            "actor_id" => PlayerSkillCheck with { ActorId = "npc_one" },
            "operation" => PlayerSkillCheck with { Operation = "attack_roll" },
            _ => throw new ArgumentOutOfRangeException(nameof(mismatch), mismatch, null)
        };

        var resolution = EffectRollContributionResolver.Resolve(
            Snapshot(Component("effect_filter", "component_filter", "advantage", Scope("all"))), context);

        Assert.True(resolution.IsValid);
        Assert.Equal("normal", resolution.RollMode);
        Assert.Empty(resolution.Contributions);
    }

    [Theory]
    [InlineData("advantage", "advantage", "advantage")]
    [InlineData("disadvantage", "disadvantage", "disadvantage")]
    [InlineData("advantage", "disadvantage", "normal")]
    public void Resolve_RepeatedAndOpposingContributions_UseUnchangedCancellation(
        string first,
        string second,
        string expectedMode)
    {
        var resolution = EffectRollContributionResolver.Resolve(
            Snapshot(
                Component("effect_one", "component_one", first, Scope("all")),
                Component("effect_two", "component_two", second, Scope("all"))),
            PlayerSkillCheck);

        Assert.True(resolution.IsValid);
        Assert.Equal(expectedMode, resolution.RollMode);
        Assert.Equal(2, resolution.Contributions.Count);
        Assert.Equal(new[] { first, second }, resolution.Contributions.Select(static value => value.Contribution));
    }

    private static EffectMechanicsSnapshot Snapshot(
        EffectMechanicalComponent first,
        EffectMechanicalComponent? second = null,
        EffectRollSkillScopeAuthority? authority = null)
    {
        var components = second == null ? new[] { first } : new[] { first, second };
        var snapshot = new EffectMechanicsSnapshot(
            true,
            new ReadOnlyCollection<EffectMechanicalComponent>(components),
            Array.Empty<EffectMechanicsAuditEntry>(),
            Array.Empty<ValidationIssue>());
        return authority == null
            ? snapshot
            : snapshot with { SkillScopeAuthority = authority };
    }

    private static EffectMechanicalComponent Component(
        string effectId,
        string componentId,
        string contribution,
        JsonObject scope)
    {
        var payload = new JsonObject
        {
            ["operations"] = new JsonArray("skill_check"),
            ["contribution"] = contribution,
            ["scope"] = scope
        };
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

    private static EffectRollSkillScopeAuthority Authority(params JsonObject[] skills) =>
        EffectRollSkillScopeAuthority.Build(new EffectRollSkillScopeAuthorityInput(
            Roots(skills),
            Roots(skills)));

    private static JsonObject RootWithSkills(params JsonObject[] skills) => new()
    {
        ["activeSkillChanges"] = new JsonArray(skills.Select(static skill => (JsonNode?)skill.DeepClone()).ToArray())
    };

    private static Dictionary<string, JsonNode?> Roots(params JsonObject[] skills) => new()
    {
        [ActivePath] = new JsonObject
        {
            ["activeSkillChanges"] = new JsonArray(skills.Select(static skill => (JsonNode?)skill.DeepClone()).ToArray())
        }
    };

    private static JsonObject Skill(string id, bool active) => new()
    {
        ["skillId"] = id,
        ["skillName"] = "Навык",
        ["lifecycle"] = active ? "active" : "inactive",
        ["active"] = active
    };

    private static string CreateTemporaryRoot() =>
        Path.Combine(Path.GetTempPath(), "boe-roll-contribution-" + Guid.NewGuid().ToString("N"));

    private static FileSystemManager CreateFileSystem(string root, FileSystemManagerHooks? hooks = null)
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
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }
}
