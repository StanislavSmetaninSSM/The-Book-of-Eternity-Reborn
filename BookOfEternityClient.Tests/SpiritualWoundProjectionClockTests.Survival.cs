using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class SpiritualWoundProjectionClockTests
{
    /// <summary>
    /// Replays the survival draft's projected soul and world through actual resource execution.
    /// </summary>
    [Fact]
    public void SurvivalConsumptionTimeReplaysActualOutcome()
    {
        var bootstrap = ResourceBootstrapStateBuilder.BuildMortalPlayer(1, 41, 10, 10, 10, 10, 10);
        Assert.True(bootstrap.IsValid);
        var journal = SpiritualWoundReplayJournal.CreateAppend("[]");
        var clock = new SpiritualWoundProjectionClock(journal, new CountingClock());
        var first = ProjectSurvival(bootstrap, clock);
        Assert.Contains("2026-09-22T08:00:01.0000000Z", first);
        var rows = journal.Export().ToJsonString();
        Assert.Single(journal.Export());
        var replay = SpiritualWoundReplayJournal.CreateReplay(rows);
        Assert.Equal(first, ProjectSurvival(bootstrap,
            new SpiritualWoundProjectionClock(replay, new CountingClock { Reject = true })));
        Assert.Equal(rows, replay.Export().ToJsonString());
    }

    /// <summary>
    /// Builds and projects the admitted survival outcome through the ordinary resource owner.
    /// </summary>
    /// <param name="bootstrap">
    /// Unchanged original resource state and history shared by record and replay.
    /// </param>
    /// <param name="clock">
    /// Explicit timestamp policy supplied to the actual outcome constructor.
    /// </param>
    /// <returns>
    /// Both complete companion images in deterministic path order.
    /// </returns>
    private static string ProjectSurvival(ResourceBootstrapStateResult bootstrap, AcceptedTurnProjectionClock clock)
    {
        var soul = new JsonObject
        {
            [ShiningBlessingEffectState.SoulStateProperty] = new JsonObject
            {
                ["pendingSurvivalEffects"] = new JsonArray(new JsonObject
                {
                    ["sourceCardId"] = "card_probe",
                    ["status"] = ShiningBlessingEffectState.SurvivalStatusPendingFirstRuinousFailure,
                    ["recovery"] = 50, ["downgrade"] = 1
                })
            }
        };
        var world = JsonNode.Parse("""
            {"events":[{"eventId":"evt_ruinous","visibility":"player_known","severity":"ruinous"}]}
            """)!.AsObject();
        var built = ShiningBlessingEffectState.TryCreateSurvivalResourceOutcomeDraftWithClock(
            42, soul, world, new JsonObject { ["events"] = new JsonArray() },
            new CanonicalBeforeImage(true, Encoding.UTF8.GetBytes(soul.ToJsonString())),
            new CanonicalBeforeImage(true, Encoding.UTF8.GetBytes(world.ToJsonString())), clock);
        Assert.True(built.IsValid, string.Join(Environment.NewLine, built.Issues));
        var draft = Assert.IsAssignableFrom<IResourceRegisteredSystemOutcomeDraft>(built.Draft);
        var sources = ResourceMutationSourceCatalog.Create(draft.SourceExports);
        Assert.True(sources.IsValid);
        var planned = AcceptedMechanicsPlanner.BuildResources(new AcceptedMechanicsResourceInput(
            42, bootstrap.Definitions!, bootstrap.State!, bootstrap.History!, sources.Catalog!, draft.Mutations),
            new AcceptedMechanicsIdentityFactory());
        Assert.True(planned.IsValid, string.Join(Environment.NewLine, planned.Issues));
        var projected = draft.Project(planned);
        Assert.True(projected.IsValid, string.Join(Environment.NewLine, projected.Issues));
        return string.Join("\n", projected.CompanionAfterImages.OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => pair.Value.ToJsonString()));
    }
}
