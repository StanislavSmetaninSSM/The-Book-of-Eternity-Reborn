using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class EffectRollSkillScopeAuthorityTests
{
    private const string ActivePath = "game_state/player/skills_active.json";
    private const string PassivePath = "game_state/player/skills_passive.json";
    private const string NpcPath = "game_state/npcs/npc_core.json";
    private static EffectTargetKey Player => new("mortal_world", "player", "player_current");
    private static EffectTargetKey Npc(string id = "npc_one") => new("mortal_world", "npc", id);

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, true, false)]
    public void CanonicalActiveAndPassiveSkills_BindToExactOwner(bool npc, bool passive, bool generic)
    {
        var roots = Roots(npc, passive, generic, Skill("skill_lockpicking", "Взлом"));
        var authority = Build(roots);
        var target = npc ? Npc() : Player;

        var result = authority.ResolveForNewBinding(target, "skill_lockpicking", "test");

        Assert.True(result.IsValid);
        Assert.True(result.IsUsable);
        Assert.Empty(result.Issues);
        Assert.Equal("Взлом", result.DisplayName);
        Assert.Equal(EffectRollSkillScopeState.Usable,
            authority.ResolveCurrent(target, "skill_lockpicking", "test").State);
        Assert.Equal(EffectRollSkillScopeState.Missing,
            authority.ResolveCurrent(npc ? Player : Npc(), "skill_lockpicking", "test").State);
    }

    [Theory]
    [InlineData("SKILL_LOCKPICKING")]
    [InlineData("skill_lоckpicking")]
    [InlineData("skill_unknown")]
    [InlineData("skill_lockpicking ")]
    public void SimilarOrUnknownId_IsMissingNotAnIdentityFallback(string selected)
    {
        var authority = Build(Roots(Skill("skill_lockpicking", "Взлом")));
        Assert.Equal(EffectRollSkillScopeState.Missing,
            authority.ResolveCurrent(Player, selected, "test").State);
        Assert.False(authority.ResolveForNewBinding(Player, selected, "test").IsUsable);
    }

    [Fact]
    public void MissingCatalogAndIdlessRows_DoNotAuthorizeByNameOrOtherId()
    {
        var roots = Roots(new JsonObject { ["name"] = "skill_lockpicking", ["id"] = "skill_lockpicking" });
        foreach (var input in new[] { roots, new Dictionary<string, JsonNode?>() })
        {
            var authority = Build(input);
            Assert.Equal(EffectRollSkillScopeState.Missing,
                authority.ResolveCurrent(Player, "skill_lockpicking", "test").State);
            Assert.Empty(authority.CreateGmCatalog()["targets"]!.AsArray());
        }
    }

    [Theory]
    [InlineData("active", "false")]
    [InlineData("isActive", "false")]
    [InlineData("isInactive", "true")]
    [InlineData("lifecycle", "\"inactive\"")]
    [InlineData("status", "\"disabled\"")]
    [InlineData("state", "\"removed\"")]
    [InlineData("availability", "\"locked\"")]
    [InlineData("lifecycle", "\"destroyed\"")]
    [InlineData("lifecycle", "\"Completed\"")]
    [InlineData("lifecycle", "\"failed\"")]
    [InlineData("lifecycle", "\"abandoned\"")]
    [InlineData("lifecycle", "\"cancelled\"")]
    [InlineData("lifecycle", "\"resolved\"")]
    [InlineData("lifecycle", "\"healed\"")]
    [InlineData("lifecycle", "\"closed\"")]
    [InlineData("lifecycle", "\"consumed\"")]
    public void CurrentInactiveOrTerminalRow_IsUnavailable(string field, string value)
    {
        var current = Skill("skill_one", "Изменённый");
        current[field] = JsonNode.Parse(value);
        var authority = Build(Roots(Skill("skill_one", "Навык")), Roots(current));
        var runtime = authority.ResolveCurrent(Player, "skill_one", "test");
        Assert.Equal(EffectRollSkillScopeState.Unavailable, runtime.State);
        Assert.True(runtime.IsValid);
        Assert.False(runtime.IsUsable);
        Assert.Equal("Изменённый", runtime.DisplayName);
        Assert.Empty(runtime.Issues);
        Assert.NotEmpty(authority.ResolveForNewBinding(Player, "skill_one", "test").Issues);
    }

    [Fact]
    public void OfferedUnavailableRow_CannotBindEvenWhenCurrentIsActive()
    {
        var authority = Build(Roots(Skill("skill_one", "Навык", false)), Roots(Skill("skill_one", "Навык")));
        Assert.False(authority.ResolveForNewBinding(Player, "skill_one", "test").IsUsable);
        Assert.True(authority.ResolveCurrent(Player, "skill_one", "test").IsUsable);
        Assert.Empty(authority.CreateGmCatalog()["targets"]!.AsArray());
    }

    [Theory]
    [InlineData("skill_one", true)]
    [InlineData("SKILL_ONE", true)]
    [InlineData("skill_оne", true)]
    [InlineData("skill_one", false)]
    [InlineData("SKILL_ONE", false)]
    [InlineData("skill_оne", false)]
    public void ExactOrConfusableCompetitor_InEitherHalfInvalidatesBinding(string competitor, bool offered)
    {
        var unique = Roots(Skill("skill_one", "Навык"));
        var ambiguous = Roots(Skill("skill_one", "Навык"), Skill(competitor, "Другой", false));
        var authority = Build(offered ? ambiguous : unique, offered ? unique : ambiguous);
        var binding = authority.ResolveForNewBinding(Player, "skill_one", "candidate", "custom");
        Assert.Equal(EffectRollSkillScopeState.InvalidAuthority, binding.State);
        Assert.False(binding.IsValid);
        Assert.All(binding.Issues, issue => Assert.Equal("custom", issue.Section));
        Assert.NotEmpty(binding.Issues);
        Assert.Equal(offered ? EffectRollSkillScopeState.Usable : EffectRollSkillScopeState.InvalidAuthority,
            authority.ResolveCurrent(Player, "skill_one", "runtime").State);
        Assert.Equal(offered ? 0 : 1, authority.CreateGmCatalog()["targets"]!.AsArray().Count);
    }

    [Fact]
    public void DuplicateAcrossPlayerActiveAndPassive_IsInvalid()
    {
        var roots = Roots(Skill("skill_one", "Один"));
        roots[PassivePath] = new JsonObject { ["skills"] = new JsonArray(Skill("skill_one", "Два")) };
        Assert.Equal(EffectRollSkillScopeState.InvalidAuthority,
            Build(roots).ResolveCurrent(Player, "skill_one", "test").State);
    }

    [Fact]
    public void MirroredNpcActors_AreDeduplicatedUsingCanonicalEnumeration()
    {
        var roots = Roots(true, false, false, Skill("skill_one", "Навык"));
        var root = roots[NpcPath]!.AsObject();
        root["NPCsInScene"] = root["UpdateNPCs"]!.DeepClone();
        Assert.True(Build(roots).ResolveForNewBinding(Npc(), "skill_one", "test").IsUsable);
        root["NPCsInScene"]![0]!["activeSkills"]![0]!["skillName"] = "Изменённый";
        Assert.Equal(EffectRollSkillScopeState.InvalidAuthority,
            Build(roots).ResolveCurrent(Npc(), "skill_one", "test").State);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AcceptedPlayerAddition_IsCurrentButNeverOffered(bool passive)
    {
        var pre = Roots(false, passive, false, Skill("skill_old", "Старый"));
        var accepted = Roots(false, passive, false, Skill("skill_new", "Новый"));
        var authority = EffectAcceptedTurnInputComposer.ComposeSkillScopeAuthority(pre, accepted);
        Assert.True(authority.ResolveForNewBinding(Player, "skill_old", "test").IsUsable);
        Assert.True(authority.ResolveCurrent(Player, "skill_new", "test").IsUsable);
        Assert.Equal(EffectRollSkillScopeState.Missing,
            authority.ResolveForNewBinding(Player, "skill_new", "test").State);
        Assert.DoesNotContain("skill_new", authority.CreateGmCatalog().ToJsonString());
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void AcceptedPlayerRemovalOrDisable_ChangesOnlyCurrentAuthority(bool passive, bool disable)
    {
        var pre = Roots(false, passive, false, Skill("skill_old", "Старый"));
        var path = passive ? PassivePath : ActivePath;
        var accepted = disable
            ? Roots(false, passive, false, Skill("skill_old", "Старый", false))
            : new Dictionary<string, JsonNode?>
            {
                [path] = new JsonObject
                {
                    [passive ? "removePassiveSkills" : "removeActiveSkills"] = new JsonArray("Старый")
                }
            };
        var authority = EffectAcceptedTurnInputComposer.ComposeSkillScopeAuthority(pre, accepted);
        Assert.Equal(disable ? EffectRollSkillScopeState.Unavailable : EffectRollSkillScopeState.Missing,
            authority.ResolveCurrent(Player, "skill_old", "test").State);
        Assert.False(authority.ResolveForNewBinding(Player, "skill_old", "test").IsUsable);
        Assert.Contains("skill_old", authority.CreateGmCatalog().ToJsonString());
    }

    [Fact]
    public void AcceptedNpcRoot_ReplacesCurrentWhileAbsentAcceptedRootPreservesPreTurn()
    {
        var pre = Roots(true, false, false, Skill("skill_old", "Старый"));
        var accepted = Roots(true, true, false, Skill("skill_new", "Новый"));
        var authority = EffectAcceptedTurnInputComposer.ComposeSkillScopeAuthority(pre, accepted);
        Assert.True(authority.ResolveCurrent(Npc(), "skill_new", "test").IsUsable);
        Assert.Equal(EffectRollSkillScopeState.Missing,
            authority.ResolveCurrent(Npc(), "skill_old", "test").State);
        Assert.False(authority.ResolveForNewBinding(Npc(), "skill_new", "test").IsUsable);
        Assert.True(EffectAcceptedTurnInputComposer.ComposeSkillScopeAuthority(pre)
            .ResolveForNewBinding(Npc(), "skill_old", "test").IsUsable);
        accepted[NpcPath] = null;
        Assert.Equal(EffectRollSkillScopeState.Missing,
            EffectAcceptedTurnInputComposer.ComposeSkillScopeAuthority(pre, accepted)
                .ResolveCurrent(Npc(), "skill_old", "test").State);
    }

    [Fact]
    public void InputsAndReturnedCatalog_AreDetachedFromAuthority()
    {
        var offered = Roots(Skill("skill_one", "Один"));
        var current = Roots(Skill("skill_one", "Текущий"));
        var authority = Build(offered, current);
        var fingerprint = authority.Fingerprint;
        offered[ActivePath]!["activeSkillChanges"]![0]!["skillId"] = "tampered";
        current[ActivePath]!["activeSkillChanges"]![0]!["active"] = false;
        offered.Clear();
        current.Clear();
        authority.CreateGmCatalog()["targets"]!.AsArray().Clear();
        Assert.True(authority.ResolveForNewBinding(Player, "skill_one", "test").IsUsable);
        Assert.Equal("Текущий", authority.ResolveCurrent(Player, "skill_one", "test").DisplayName);
        Assert.Single(authority.CreateGmCatalog()["targets"]!.AsArray());
        Assert.Equal(fingerprint, authority.Fingerprint);
    }

    [Fact]
    public void GmCatalog_IsClosedOrdinalOfferedProjectionWithDisplayFallbacks()
    {
        var first = Skill("skill_z", "Имя");
        first["displayName"] = "Отображаемое";
        var roots = Roots(first, new JsonObject { ["skillId"] = "skill_a", ["name"] = "Название" },
            new JsonObject { ["skillId"] = "skill_b" }, Skill("skill_c", "Навык C"), Skill("skill_off", "Нет", false));
        roots[NpcPath] = Roots(true, true, false, Skill("skill_npc", "Навык NPC"))[NpcPath];
        var expected = JsonNode.Parse("""
            {"schemaVersion":1,"targets":[
              {"realm":"mortal_world","kind":"npc","targetId":"npc_one","skills":[{"skillId":"skill_npc","displayName":"Навык NPC"}]},
              {"realm":"mortal_world","kind":"player","targetId":"player_current","skills":[
                {"skillId":"skill_a","displayName":"Название"},{"skillId":"skill_b","displayName":"Навык"},
                {"skillId":"skill_c","displayName":"Навык C"},{"skillId":"skill_z","displayName":"Отображаемое"}]}]}
            """);
        Assert.True(JsonNode.DeepEquals(expected, Build(roots).CreateGmCatalog()));
    }

    [Fact]
    public void Fingerprint_IsOrderIndependentAndSealsBothHalves()
    {
        var first = Roots(Skill("skill_b", "Б"), Skill("skill_a", "А"));
        var second = Roots(Skill("skill_a", "А"), Skill("skill_b", "Б"));
        var original = Build(first).Fingerprint;
        Assert.Equal(original, Build(second).Fingerprint);
        Assert.Equal(Build(first).CreateGmCatalog().ToJsonString(), Build(second).CreateGmCatalog().ToJsonString());
        second[ActivePath]!["activeSkillChanges"]![0]!["active"] = false;
        Assert.NotEqual(original, Build(first, second).Fingerprint);
        Assert.NotEqual(original, Build(second, first).Fingerprint);
        Assert.NotEqual(Build(first, second).Fingerprint, Build(second, first).Fingerprint);
    }

    [Theory]
    [InlineData(128, true)]
    [InlineData(129, false)]
    public void PerTargetSelectableBound_IsInclusiveAndLocal(int count, bool usable)
    {
        var roots = Roots(Enumerable.Range(0, count).Select(i => Skill($"skill_{i:D3}", $"Навык {i}")).ToArray());
        roots[NpcPath] = Roots(true, false, false, Skill("skill_other", "Другой"))[NpcPath];
        var authority = Build(roots);
        Assert.Equal(usable ? EffectRollSkillScopeState.Usable : EffectRollSkillScopeState.InvalidAuthority,
            authority.ResolveCurrent(Player, "skill_000", "test").State);
        Assert.True(authority.ResolveForNewBinding(Npc(), "skill_other", "test").IsUsable);
        Assert.Equal(usable ? 2 : 1, authority.CreateGmCatalog()["targets"]!.AsArray().Count);
    }

    [Theory]
    [InlineData(128, 1, true)]
    [InlineData(129, 1, false)]
    [InlineData(16, 128, true)]
    [InlineData(17, 121, false)]
    public void CatalogTargetAndTotalBounds_AreInclusiveAndIsolatedToCatalogHalf(int targets, int skills, bool usable)
    {
        var actors = new JsonArray();
        for (var i = 0; i < targets; i++)
            actors.Add(new JsonObject
            {
                ["NPCId"] = $"npc_{i:D3}",
                ["activeSkills"] = new JsonArray(Enumerable.Range(0, skills)
                    .Select(j => (JsonNode?)Skill($"skill_{j:D3}", $"Навык {j}")).ToArray())
            });
        var oversized = new Dictionary<string, JsonNode?> { [NpcPath] = new JsonObject { ["UpdateNPCs"] = actors } };
        var small = new Dictionary<string, JsonNode?>
        {
            [NpcPath] = new JsonObject { ["UpdateNPCs"] = new JsonArray(actors[0]!.DeepClone()) }
        };
        var authority = Build(oversized, small);
        Assert.True(authority.ResolveCurrent(Npc("npc_000"), "skill_000", "test").IsUsable);
        Assert.Equal(usable ? EffectRollSkillScopeState.Usable : EffectRollSkillScopeState.InvalidAuthority,
            authority.ResolveForNewBinding(Npc("npc_000"), "skill_000", "test").State);
        Assert.Equal(usable ? targets : 0, authority.CreateGmCatalog()["targets"]!.AsArray().Count);
        Assert.Equal(usable ? EffectRollSkillScopeState.Usable : EffectRollSkillScopeState.InvalidAuthority,
            Build(small, oversized).ResolveCurrent(Npc("npc_000"), "skill_000", "test").State);
        Assert.Empty(authority.ValidateNewComponents(Player, new JsonArray(Component("all")), "components"));
    }

    [Fact]
    public void UnselectableRows_DoNotConsumeProjectionBounds()
    {
        var roots = Roots(Enumerable.Range(0, 2049).Select(i => Skill($"skill_{i}", "Недоступен", false))
            .Append(Skill("skill_usable", "Доступен")).ToArray());
        Assert.True(Build(roots).ResolveForNewBinding(Player, "skill_usable", "test").IsUsable);
    }

    [Fact]
    public void ComponentValidation_OnlyBindsFocusedRollComponentsAndPreservesIssueLocation()
    {
        var authority = Build(Roots(Skill("skill_one", "Навык")));
        var components = new JsonArray(Component("all"), Component("skill", "skill_one"), Component("skill", "missing"));
        var issues = authority.ValidateNewComponents(Player, components, "candidate.components", "custom_section");
        var issue = Assert.Single(issues);
        Assert.Equal("candidate.components[2].payload.scope.skillId", issue.FilePath);
        Assert.Equal("custom_section", issue.Section);
        Assert.Empty(authority.ValidateNewComponents(Player, new JsonArray(), "components"));
    }

    private static JsonObject Component(string kind, string? skillId = null)
    {
        return new JsonObject
        {
            ["componentId"] = "roll_test", ["priority"] = 0, ["profile"] = "roll_modifier",
            ["payload"] = string.Equals(kind, "all", StringComparison.Ordinal)
                ? EffectMaterializationTestFixture.CreateBroadRollModifierPayload(
                    "disadvantage",
                    "skill_check")
                : EffectMaterializationTestFixture.CreateFocusedRollModifierPayload(
                    skillId!)
        };
    }

    private static EffectRollSkillScopeAuthority Build(Dictionary<string, JsonNode?> offered,
        Dictionary<string, JsonNode?>? current = null) =>
        EffectRollSkillScopeAuthority.Build(new EffectRollSkillScopeAuthorityInput(offered, current ?? offered));

    private static Dictionary<string, JsonNode?> Roots(params JsonObject[] skills) => Roots(false, false, false, skills);

    private static Dictionary<string, JsonNode?> Roots(bool npc, bool passive, bool generic, params JsonObject[] skills)
    {
        var rows = new JsonArray(skills.Select(s => (JsonNode?)s).ToArray());
        return npc
            ? new Dictionary<string, JsonNode?>
            {
                [NpcPath] = new JsonObject
                {
                    ["UpdateNPCs"] = new JsonArray(new JsonObject
                    {
                        ["NPCId"] = "npc_one", [passive ? "passiveSkills" : "activeSkills"] = rows
                    })
                }
            }
            : new Dictionary<string, JsonNode?>
            {
                [passive ? PassivePath : ActivePath] = new JsonObject
                {
                    [generic ? "skills" : passive ? "passiveSkillChanges" : "activeSkillChanges"] = rows
                }
            };
    }

    private static JsonObject Skill(string id, string name, bool active = true) => new()
    {
        ["skillId"] = id, ["skillName"] = name, ["lifecycle"] = active ? "active" : "inactive", ["active"] = active
    };
}
