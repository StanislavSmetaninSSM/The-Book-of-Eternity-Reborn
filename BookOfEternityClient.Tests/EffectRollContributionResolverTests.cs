using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
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

    [Fact]
    public void Capture_AllRollRowsBeforeContextFiltering_PreservesOrderAndMechanicalFieldsOnly()
    {
        const string privateName = "t171 private effect name";
        const string privateDescription = "t171 private effect description";
        const string privatePayload = "t171-private-payload-sentinel";
        const string privateOwner = "t171-private-owner-sentinel";
        const string privateCarrier = "t171-private-carrier-sentinel";
        var foreign = Component(
            "effect_foreign",
            "component_foreign",
            "disadvantage",
            Scope("all"),
            privatePayload,
            new[] { "attack_roll", "skill_check" }) with
        {
            Realm = "chaos_sea",
            TargetKind = "npc",
            TargetId = "npc_one",
            EffectName = privateName,
            EffectDescription = privateDescription
        };
        using var carrierDocument = JsonDocument.Parse(new JsonObject
        {
            ["carrier"] = privateCarrier
        }.ToJsonString());
        var snapshot = Snapshot(
            Component(
                "effect_local",
                "component_local",
                "advantage",
                Scope("all"),
                operations: new[] { "skill_check", "attack_roll" }),
            foreign) with
        {
            Effects = new[]
            {
                new EffectAcceptedInstance(
                    "effect_carrier",
                    "mortal_world",
                    "player",
                    "player_current",
                    "npc",
                    privateOwner,
                    null,
                    carrierDocument.RootElement.Clone())
            }
        };

        var capture = EffectRollContributionResolver.Capture(snapshot);

        Assert.True(capture.IsValid);
        var authority = Assert.IsType<EffectDetachedRollSourceAuthority>(capture.Authority);
        Assert.Equal(new[] { 0, 1 }, authority.Rows.Select(static row => row.Ordinal));
        Assert.Equal(new[] { "effect_local", "effect_foreign" }, authority.Rows.Select(static row => row.EffectId));
        Assert.Equal(
            new[] { "skill_check", "attack_roll" },
            authority.Rows[0].Operations);
        Assert.Equal(
            new[] { "attack_roll", "skill_check" },
            authority.Rows[1].Operations);
        var serialized = JsonSerializer.Serialize(authority);
        Assert.DoesNotContain(privateName, serialized, StringComparison.Ordinal);
        Assert.DoesNotContain(privateDescription, serialized, StringComparison.Ordinal);
        Assert.DoesNotContain(privatePayload, serialized, StringComparison.Ordinal);
        Assert.DoesNotContain(privateOwner, serialized, StringComparison.Ordinal);
        Assert.DoesNotContain(privateCarrier, serialized, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolve_SnapshotAndDetachedAuthority_ProduceExactParity()
    {
        var snapshot = Snapshot(
            Component("effect_broad", "component_broad", "advantage", Scope("all")),
            Component("effect_focused", "component_focused", "disadvantage", Scope("skill", "skill_lockpicking")),
            Authority(Skill("skill_lockpicking", active: true)));

        var live = EffectRollContributionResolver.Resolve(snapshot, PlayerSkillCheck);
        var capture = EffectRollContributionResolver.Capture(snapshot);
        var detached = EffectRollContributionResolver.Resolve(
            Assert.IsType<EffectDetachedRollSourceAuthority>(capture.Authority),
            PlayerSkillCheck,
            new EffectRollSkillUsabilityProof("mortal_world", "player", "player_current", "skill_lockpicking"));

        Assert.True(live.IsValid);
        Assert.True(detached.IsValid);
        Assert.Equal(live.RollMode, detached.RollMode);
        Assert.Equal(live.Contributions, detached.Contributions);
    }

    [Fact]
    public void Capture_RejectedSnapshot_ReturnsItsDiagnosticsAndNoAuthority()
    {
        var snapshot = Snapshot(Component("effect_rejected_capture", "component_rejected_capture", "advantage", Scope("all"))) with
        { IsAccepted = false, Issues = new ReadOnlyCollection<ValidationIssue>(new[] { new ValidationIssue("x", IssueSeverity.Error, "x", code: "rejected") }) };
        var capture = EffectRollContributionResolver.Capture(snapshot);
        Assert.False(capture.IsValid); Assert.Null(capture.Authority); Assert.Equal("rejected", Assert.Single(capture.Issues).Code);
    }

    [Fact]
    public void Capture_MalformedIrrelevantRollRowFailsClosed()
    {
        var foreignMalformed = Component(
            "effect_irrelevant_malformed",
            "component_irrelevant_malformed",
            "advantage",
            Scope("all"),
            operations: new[] { "not_an_operation" }) with
        {
            Realm = "chaos_sea",
            TargetKind = "npc",
            TargetId = "npc_one"
        };

        var capture = EffectRollContributionResolver.Capture(
            Snapshot(
                Component("effect_valid", "component_valid", "advantage", Scope("all")),
                foreignMalformed));

        Assert.False(capture.IsValid);
        Assert.Null(capture.Authority);
    }

    [Fact]
    public void Resolve_DetachedAuthority_FiltersForeignRowsInsideSharedCore()
    {
        var source = EffectDetachedRollSourceAuthority.Create(new[]
        {
            SourceRow(0, "effect_matching", "component_matching", "mortal_world", "player", "player_current", new[] { "skill_check" }, "advantage", "all", null),
            SourceRow(1, "effect_other_realm", "component_other_realm", "chaos_sea", "player", "player_current", new[] { "skill_check" }, "advantage", "all", null),
            SourceRow(2, "effect_other_kind", "component_other_kind", "mortal_world", "npc", "player_current", new[] { "skill_check" }, "advantage", "all", null),
            SourceRow(3, "effect_other_id", "component_other_id", "mortal_world", "player", "player_other", new[] { "skill_check" }, "advantage", "all", null),
            SourceRow(4, "effect_other_operation", "component_other_operation", "mortal_world", "player", "player_current", new[] { "attack_roll" }, "advantage", "all", null),
            SourceRow(5, "effect_other_skill", "component_other_skill", "mortal_world", "player", "player_current", new[] { "skill_check" }, "advantage", "skill", "skill_other")
        });
        var result = EffectRollContributionResolver.Resolve(
            source,
            PlayerSkillCheck,
            new EffectRollSkillUsabilityProof("mortal_world", "player", "player_current", "skill_lockpicking"));
        Assert.True(result.IsValid);
        Assert.Equal("advantage", result.RollMode);
        var evidence = Assert.Single(result.Contributions);
        Assert.Equal("effect_matching", evidence.EffectId);
    }

    [Fact]
    public void Authority_MalformedDuplicateOrConfusableRowsFailClosed()
    {
        var duplicate = EffectDetachedRollSourceAuthority.Create(new[]
        {
            EffectDetachedRollSourceRow.Create(0, "effect_one", "component", "mortal_world", "player", "player_current", new[] { "skill_check" }, "advantage", "all", null),
            EffectDetachedRollSourceRow.Create(1, "effect_one", "component", "mortal_world", "player", "player_current", new[] { "skill_check" }, "disadvantage", "all", null)
        });
        var result = EffectRollContributionResolver.Resolve(duplicate, PlayerSkillCheck, (EffectRollSkillUsabilityProof?)null);
        Assert.False(result.IsValid); Assert.Empty(result.Contributions);

        var confusable = EffectDetachedRollSourceAuthority.Create(new[]
        {
            EffectDetachedRollSourceRow.Create(0, "effect_confusable", "component", "mortal_world", "player", "player_current", new[] { "skill_check" }, "advantage", "all", null),
            EffectDetachedRollSourceRow.Create(1, "effect_cоnfusable", "component", "mortal_world", "player", "player_current", new[] { "skill_check" }, "advantage", "all", null)
        });
        Assert.False(confusable.HasValidSeal(out _));
        var malformedIrrelevant = EffectDetachedRollSourceAuthority.Create(new[]
        {
            EffectDetachedRollSourceRow.Create(0, "effect_bad", "component_bad", "chaos_sea", "npc", "npc_one", new[] { "not_an_operation" }, "advantage", "all", null)
        });
        Assert.False(EffectRollContributionResolver.Resolve(malformedIrrelevant, PlayerSkillCheck, (EffectRollSkillUsabilityProof?)null).IsValid);
    }

    [Theory]
    [InlineData("attack_roll")]
    [InlineData("skill_check,attack_roll")]
    public void Authority_FocusedScopeRequiresOnlySkillCheckOperation(
        string serializedOperations)
    {
        var operations = serializedOperations.Split(',', StringSplitOptions.None);
        var source = EffectDetachedRollSourceAuthority.Create(new[]
        {
            SourceRow(
                0,
                "effect_invalid_focused",
                "component_invalid_focused",
                "mortal_world",
                "player",
                "player_current",
                operations,
                "advantage",
                "skill",
                "skill_lockpicking")
        });

        var resolution = EffectRollContributionResolver.Resolve(
            source,
            PlayerSkillCheck,
            new EffectRollSkillUsabilityProof(
                "mortal_world",
                "player",
                "player_current",
                "skill_lockpicking"));

        Assert.False(source.HasValidSeal(out _));
        Assert.False(resolution.IsValid);
        Assert.Empty(resolution.Contributions);
    }

    [Fact]
    public void Authority_EnforcesMaximumRowBoundBeforeDetachedCopies()
    {
        var maximumRows = Enumerable.Range(0, 10_000)
            .Select(CreateBoundedSourceRow)
            .ToArray();
        var maximum = EffectDetachedRollSourceAuthority.Create(maximumRows);

        Assert.True(maximum.HasValidSeal(out _));

        var oversizedRows = Enumerable.Range(0, 10_001)
            .Select(CreateBoundedSourceRow)
            .ToArray();
        Assert.Throws<ArgumentOutOfRangeException>(
            () => EffectDetachedRollSourceAuthority.Create(oversizedRows));
    }

    [Fact]
    public void Capture_TooManyRowsFailsClosedBeforeAuthorityCreation()
    {
        var template = Component(
            "effect_capture_template",
            "component_capture_template",
            "advantage",
            Scope("all"));
        var components = Enumerable.Range(0, 10_001)
            .Select(index => template with
            {
                EffectId = "effect_capture_" + index.ToString(
                    "D5",
                    CultureInfo.InvariantCulture),
                ComponentId = "component_capture_" + index.ToString(
                    "D5",
                    CultureInfo.InvariantCulture)
            })
            .ToArray();
        var snapshot = new EffectMechanicsSnapshot(
            true,
            new ReadOnlyCollection<EffectMechanicalComponent>(components),
            Array.Empty<EffectMechanicsAuditEntry>(),
            Array.Empty<ValidationIssue>());

        var capture = EffectRollContributionResolver.Capture(snapshot);

        Assert.False(capture.IsValid);
        Assert.Null(capture.Authority);
        Assert.Equal(
            "effect_roll_source_capture_too_many_rows",
            Assert.Single(capture.Issues).Code);
    }

    [Fact]
    public void Authority_OperationAndJsonRowBoundsFailBeforeDetachedCopies()
    {
        var sixOperations = new[]
        {
            "attack_roll", "defense_roll", "skill_check", "saving_throw",
            "damage_roll", "initiative_roll"
        };
        var valid = EffectDetachedRollSourceAuthority.Create(new[]
        {
            SourceRow(0, "effect_six", "component_six", "mortal_world", "player", "player_current", sixOperations, "advantage", "all", null)
        });
        Assert.True(valid.HasValidSeal(out _));

        Assert.Throws<JsonException>(() => EffectDetachedRollSourceRow.Create(
            0,
            "effect_seven",
            "component_seven",
            "mortal_world",
            "player",
            "player_current",
            sixOperations.Append("attack_roll").ToArray(),
            "advantage",
            "all",
            null));

        var oversizedRows = Enumerable.Range(0, 10_001).Select(index => new JsonObject
        {
            ["ordinal"] = index,
            ["effectId"] = "effect_json_" + index.ToString("D5", CultureInfo.InvariantCulture),
            ["componentId"] = "component_json_" + index.ToString("D5", CultureInfo.InvariantCulture),
            ["realm"] = "mortal_world",
            ["targetKind"] = "player",
            ["targetId"] = "player_current",
            ["operations"] = new JsonArray("skill_check"),
            ["contribution"] = "advantage",
            ["scopeKind"] = "all",
            ["scopeSkillId"] = null
        }).ToArray();
        var oversizedJson = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["rows"] = new JsonArray(oversizedRows),
            ["authorityFingerprint"] = "untrusted"
        };

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<
            EffectDetachedRollSourceAuthority>(
            oversizedJson.ToJsonString(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }));
    }

    [Fact]
    public void Authority_CloneIsDetachedAndNullSkillPositionChangesFingerprint()
    {
        var operations = new List<string> { "skill_check", "attack_roll" };
        var sourceRow = SourceRow(
            0,
            "effect_one",
            "component_one",
            "mortal_world",
            "player",
            "player_current",
            operations,
            "advantage",
            "all",
            null);
        var broad = EffectDetachedRollSourceAuthority.Create(new[] { sourceRow });
        var clone = broad.CloneDetached();

        operations[0] = "attack_roll";

        Assert.NotSame(broad, clone);
        Assert.NotSame(broad.Rows[0], clone.Rows[0]);
        Assert.NotSame(broad.Rows[0].Operations, clone.Rows[0].Operations);
        Assert.Equal(new[] { "skill_check", "attack_roll" }, broad.Rows[0].Operations);
        Assert.True(broad.SemanticallyEquals(clone));
    }

    [Theory]
    [InlineData("ordinal")]
    [InlineData("effect")]
    [InlineData("component")]
    [InlineData("realm")]
    [InlineData("kind")]
    [InlineData("id")]
    [InlineData("operation_content")]
    [InlineData("operation_order")]
    [InlineData("contribution")]
    [InlineData("scope_kind")]
    [InlineData("null_skill")]
    public void Authority_FingerprintChangesForEverySingleSourceAxis(string axis)
    {
        var baseline = SourceRow(
            0,
            "effect_one",
            "component_one",
            "mortal_world",
            "player",
            "player_current",
            new[] { "skill_check", "attack_roll" },
            "advantage",
            "all",
            null);
        var changed = axis switch
        {
            "ordinal" => SourceRow(1, "effect_one", "component_one", "mortal_world", "player", "player_current", new[] { "skill_check", "attack_roll" }, "advantage", "all", null),
            "effect" => SourceRow(0, "effect_two", "component_one", "mortal_world", "player", "player_current", new[] { "skill_check", "attack_roll" }, "advantage", "all", null),
            "component" => SourceRow(0, "effect_one", "component_two", "mortal_world", "player", "player_current", new[] { "skill_check", "attack_roll" }, "advantage", "all", null),
            "realm" => SourceRow(0, "effect_one", "component_one", "chaos_sea", "player", "player_current", new[] { "skill_check", "attack_roll" }, "advantage", "all", null),
            "kind" => SourceRow(0, "effect_one", "component_one", "mortal_world", "npc", "player_current", new[] { "skill_check", "attack_roll" }, "advantage", "all", null),
            "id" => SourceRow(0, "effect_one", "component_one", "mortal_world", "player", "player_other", new[] { "skill_check", "attack_roll" }, "advantage", "all", null),
            "operation_content" => SourceRow(0, "effect_one", "component_one", "mortal_world", "player", "player_current", new[] { "skill_check", "defense_roll" }, "advantage", "all", null),
            "operation_order" => SourceRow(0, "effect_one", "component_one", "mortal_world", "player", "player_current", new[] { "attack_roll", "skill_check" }, "advantage", "all", null),
            "contribution" => SourceRow(0, "effect_one", "component_one", "mortal_world", "player", "player_current", new[] { "skill_check", "attack_roll" }, "disadvantage", "all", null),
            "scope_kind" => SourceRow(0, "effect_one", "component_one", "mortal_world", "player", "player_current", new[] { "skill_check", "attack_roll" }, "advantage", "skill", null),
            "null_skill" => SourceRow(0, "effect_one", "component_one", "mortal_world", "player", "player_current", new[] { "skill_check", "attack_roll" }, "advantage", "all", "skill_lockpicking"),
            _ => throw new ArgumentOutOfRangeException(nameof(axis))
        };
        var baselineAuthority = EffectDetachedRollSourceAuthority.Create(new[] { baseline });
        var changedAuthority = EffectDetachedRollSourceAuthority.Create(new[] { changed });

        Assert.True(baselineAuthority.HasValidSeal(out _));
        Assert.NotEqual(
            baselineAuthority.AuthorityFingerprint,
            changedAuthority.AuthorityFingerprint);
    }

    private static EffectDetachedRollSourceRow SourceRow(int ordinal, string effect, string component, string realm, string kind, string id, IReadOnlyList<string> operations, string contribution, string scope, string? skill) =>
        EffectDetachedRollSourceRow.Create(ordinal, effect, component, realm, kind, id, operations, contribution, scope, skill);

    private static EffectDetachedRollSourceRow CreateBoundedSourceRow(int ordinal)
    {
        var suffix = ordinal.ToString("D5", CultureInfo.InvariantCulture);
        return SourceRow(
            ordinal,
            "effect_bound_" + suffix,
            "component_bound_" + suffix,
            "mortal_world",
            "player",
            "player_current",
            new[] { "skill_check" },
            "advantage",
            "all",
            null);
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
        JsonObject scope,
        string? privatePayloadSentinel = null,
        IReadOnlyList<string>? operations = null)
    {
        var operationArray = (operations ?? new[] { "skill_check" }).ToArray();
        var scopeKind = scope["kind"]!.GetValue<string>();
        var payload = string.Equals(scopeKind, "all", StringComparison.Ordinal)
            ? EffectMaterializationTestFixture.CreateBroadRollModifierPayload(
                contribution,
                operationArray)
            : EffectMaterializationTestFixture.CreateFocusedRollModifierPayload(
                scope["skillId"]!.GetValue<string>(),
                contribution);
        if (privatePayloadSentinel is not null)
        {
            payload["privatePayload"] = privatePayloadSentinel;
            payload["ownerSentinel"] = "owner-" + privatePayloadSentinel;
            payload["carrierSentinel"] = "carrier-" + privatePayloadSentinel;
        }
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
}
