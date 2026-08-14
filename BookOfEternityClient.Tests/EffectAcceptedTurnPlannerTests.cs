using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class EffectAcceptedTurnPlannerTests
{
    [Fact]
    public void Cache_SameAcceptedInputReturnsSamePlanAndAllocatesOnceAcrossCallers()
    {
        var factory = new CountingFactory();
        var cache = new EffectAcceptedTurnPlanCache(factory);
        var input = CreateInput();

        var rawValidation = cache.GetOrBuild(input);
        var companionValidation = cache.GetOrBuild(input);
        var mechanics = cache.GetOrBuild(input);
        var commit = cache.GetOrBuild(input);

        var plan = Assert.IsType<EffectAcceptedTurnPlan>(rawValidation.Plan);
        Assert.Same(plan, companionValidation.Plan);
        Assert.Same(plan, mechanics.Plan);
        Assert.Same(plan, commit.Plan);
        Assert.Equal(1, factory.EffectCalls);
        Assert.Equal(1, factory.TransitionCalls);
        Assert.StartsWith("effect_", plan.AllocatedEffectIds.Single(), StringComparison.Ordinal);
        Assert.StartsWith("effect_transition_", plan.AllocatedTransitionIds.Single(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("session")]
    [InlineData("snapshot")]
    [InlineData("commands")]
    [InlineData("source")]
    [InlineData("target")]
    [InlineData("event")]
    public void Cache_ChangedAuthorityInputInvalidatesPlanWithoutDerivingIds(string changedPart)
    {
        var factory = new CountingFactory();
        var cache = new EffectAcceptedTurnPlanCache(factory);
        var firstInput = CreateInput();
        var first = Assert.IsType<EffectAcceptedTurnPlan>(cache.GetOrBuild(firstInput).Plan);
        var changed = Change(firstInput, changedPart);

        var second = Assert.IsType<EffectAcceptedTurnPlan>(cache.GetOrBuild(changed).Plan);

        Assert.NotSame(first, second);
        Assert.NotEqual(first.InputFingerprint, second.InputFingerprint);
        Assert.NotEqual(first.AllocatedEffectIds.Single(), second.AllocatedEffectIds.Single());
        Assert.Equal(2, factory.EffectCalls);
        Assert.Equal(2, factory.TransitionCalls);
        Assert.DoesNotContain(changed.SessionId, second.AllocatedEffectIds.Single(), StringComparison.Ordinal);
    }

    [Fact]
    public void Cache_InvalidSourceOrTargetAuthorityReturnsIssuesWithoutAllocation()
    {
        var factory = new CountingFactory();
        var cache = new EffectAcceptedTurnPlanCache(factory);
        var input = CreateInput() with
        {
            SourceAuthority = EffectSourceAuthority.Build(new EffectSourceAuthorityInput(
                Array.Empty<EffectSourceExport>(),
                Array.Empty<EffectSourceExport>(),
                new HashSet<string>(StringComparer.Ordinal)))
        };

        var result = cache.GetOrBuild(input);

        Assert.Null(result.Plan);
        Assert.NotEmpty(result.Issues);
        Assert.Equal(0, factory.EffectCalls);
        Assert.Equal(0, factory.TransitionCalls);
    }

    [Fact]
    public void Cache_ReturnedPlanCollectionsCannotBeMutatedBehindReadOnlyInterfaces()
    {
        var plan = Assert.IsType<EffectAcceptedTurnPlan>(
            new EffectAcceptedTurnPlanCache(new CountingFactory()).GetOrBuild(CreateInput()).Plan);

        Assert.True(Assert.IsAssignableFrom<IList<string>>(plan.AllocatedEffectIds).IsReadOnly);
        Assert.True(Assert.IsAssignableFrom<IList<string>>(plan.AllocatedTransitionIds).IsReadOnly);
        Assert.True(Assert.IsAssignableFrom<IList<EffectSourceKey>>(plan.Sources).IsReadOnly);
        Assert.True(Assert.IsAssignableFrom<IList<EffectTargetKey>>(plan.Targets).IsReadOnly);
    }

    private static EffectAcceptedTurnInput CreateInput()
    {
        var source = EffectSourceAuthority.Build(new EffectSourceAuthorityInput(
            new[]
            {
                new EffectSourceExport(
                    "mortal_world",
                    "wound",
                    "wound_test_torn_side",
                    new JsonArray(EffectMaterializationTestFixture.CreateDefinition()),
                    Materializable: true,
                    Active: true,
                    SameTurn: false)
            },
            Array.Empty<EffectSourceExport>(),
            new HashSet<string>(StringComparer.Ordinal)));
        var target = EffectTargetAuthority.Build(new EffectTargetAuthorityInput(
            new[] { new EffectTargetExport("mortal_world", "player", "player_current", SameTurn: false) },
            Array.Empty<EffectTargetExport>(),
            new HashSet<string>(StringComparer.Ordinal),
            null));
        return new EffectAcceptedTurnInput(
            "session_effect_test",
            "snapshot_effect_test",
            EffectMaterializationTestFixture.CreateCommandRoot(
                EffectMaterializationTestFixture.CreateApplyCommand()),
            source,
            target,
            new JsonObject
            {
                ["turn"] = 42,
                ["eventRef"] = "turn_42:wound_opened"
            });
    }

    private static EffectAcceptedTurnInput Change(EffectAcceptedTurnInput input, string part) =>
        part switch
        {
            "session" => input with { SessionId = "session_changed" },
            "snapshot" => input with { SnapshotToken = "snapshot_changed" },
            "commands" => input with
            {
                RawCommands = CreateChangedCommands()
            },
            "source" => input with
            {
                SourceAuthority = EffectSourceAuthority.Build(new EffectSourceAuthorityInput(
                    new[]
                    {
                        new EffectSourceExport(
                            "mortal_world",
                            "wound",
                            "wound_test_torn_side",
                            new JsonArray(EffectMaterializationTestFixture.CreateDefinition()),
                            true,
                            true,
                            false),
                        new EffectSourceExport(
                            "mortal_world",
                            "skill",
                            "skill_changed",
                            new JsonArray(EffectMaterializationTestFixture.CreateDefinition()),
                            true,
                            true,
                            false)
                    },
                    Array.Empty<EffectSourceExport>(),
                    new HashSet<string>(StringComparer.Ordinal)))
            },
            "target" => input with
            {
                TargetAuthority = EffectTargetAuthority.Build(new EffectTargetAuthorityInput(
                    new[]
                    {
                        new EffectTargetExport("mortal_world", "player", "player_current", false),
                        new EffectTargetExport("mortal_world", "npc", "npc_changed", false)
                    },
                    Array.Empty<EffectTargetExport>(),
                    new HashSet<string>(StringComparer.Ordinal),
                    null))
            },
            "event" => input with
            {
                EventInput = new JsonObject { ["turn"] = 43, ["eventRef"] = "turn_43:changed" }
            },
            _ => throw new ArgumentOutOfRangeException(nameof(part), part, null)
        };

    private static JsonObject CreateChangedCommands()
    {
        var change = EffectMaterializationTestFixture.CreateApplyCommand();
        change["reason"] = "Изменившееся событие.";
        return EffectMaterializationTestFixture.CreateCommandRoot(change);
    }

    private sealed class CountingFactory : EffectIdentityFactory
    {
        internal int EffectCalls { get; private set; }
        internal int TransitionCalls { get; private set; }

        internal override string CreateEffectId()
        {
            EffectCalls++;
            return base.CreateEffectId();
        }

        internal override string CreateTransitionId()
        {
            TransitionCalls++;
            return base.CreateTransitionId();
        }
    }
}
