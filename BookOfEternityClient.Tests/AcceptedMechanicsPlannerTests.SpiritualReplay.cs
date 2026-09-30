using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AcceptedMechanicsPlannerTests
{
    /// <summary>
    /// Reconstructs real resource results with exactly retained IDs and no new random calls.
    /// </summary>
    /// <param name="contour">
    /// Ordinary mutations, periodic resources or capacity initialization followed by a spend.
    /// </param>
    [Theory]
    [InlineData("ordinary")]
    [InlineData("periodic")]
    [InlineData("capacity")]
    public void SpiritualReplay_ResourceOwnerReproducesExactAllocations(string contour)
    {
        var input = contour switch
        {
            "periodic" => SessionPeriodicInput(bounded: false),
            "capacity" => SpiritualReplayCapacityInput(),
            _ => SessionOrdinaryInput()
        };
        var journal = SpiritualWoundReplayJournal.CreateAppend("[]");
        var first = AcceptedMechanicsPlanner.BuildResources(input,
            new SpiritualWoundResourceIdentityFactory(journal, new AcceptedMechanicsIdentityFactory()));
        Assert.True(first.IsValid, SessionIssues(first));
        var saved = journal.Export().ToJsonString();
        Assert.NotEmpty(journal.Export());
        var replay = SpiritualWoundReplayJournal.CreateReplay(saved);
        var second = AcceptedMechanicsPlanner.BuildResources(input,
            new SpiritualWoundResourceIdentityFactory(replay,
                new AcceptedMechanicsIdentityFactory(() => throw new NotSupportedException("Replay allocated."))));
        Assert.True(second.IsValid, SessionIssues(second));
        Assert.Equal(SessionResultImage(first), SessionResultImage(second));
        Assert.Equal(saved, replay.Export().ToJsonString());
    }

    /// <summary>
    /// Keeps every mutation key component in the journal comparison coordinate.
    /// </summary>
    /// <param name="change">
    /// Causal field modified after recording an operation allocation.
    /// </param>
    [Theory]
    [InlineData("event")]
    [InlineData("source")]
    [InlineData("owner")]
    [InlineData("resource")]
    public void SpiritualReplay_ResourceFactoryRejectsChangedCausalKey(string change)
    {
        var intent = SessionOrdinaryInput().Mutations[0];
        var journal = SpiritualWoundReplayJournal.CreateAppend("[]");
        var first = new SpiritualWoundResourceIdentityFactory(journal, new AcceptedMechanicsIdentityFactory());
        first.CreateOperationId(intent);
        var altered = change switch
        {
            "event" => intent with { EventRef = "other_event" },
            "source" => intent with { Source = intent.Source with { SourceId = "other_source" } },
            "owner" => intent with { Coordinate = intent.Coordinate with { ResourceOwnerId = "other_owner" } },
            _ => intent with { Coordinate = intent.Coordinate with { ResourceKey = "other_resource" } }
        };
        var replay = SpiritualWoundReplayJournal.CreateReplay(journal.Export().ToJsonString());
        var factory = new SpiritualWoundResourceIdentityFactory(replay,
            new AcceptedMechanicsIdentityFactory(() => throw new NotSupportedException("Replay allocated.")));
        Assert.Throws<InvalidOperationException>(() => factory.CreateOperationId(altered));
        Assert.Throws<InvalidOperationException>(() => replay.Export());
    }

    /// <summary>
    /// Prevents parameterless fallback and an explicitly aborted owner from exporting a journal.
    /// </summary>
    [Fact]
    public void SpiritualReplay_UnboundOrAbortedOwnerCannotExport()
    {
        var journal = SpiritualWoundReplayJournal.CreateAppend("[]");
        var factory = new SpiritualWoundResourceIdentityFactory(journal, new AcceptedMechanicsIdentityFactory());
        Assert.Throws<InvalidOperationException>(() => factory.CreateOperationId());
        Assert.Throws<InvalidOperationException>(() => journal.Export());
        var transitions = SpiritualWoundReplayJournal.CreateAppend("[]");
        var transitionFactory = new SpiritualWoundResourceIdentityFactory(transitions, new AcceptedMechanicsIdentityFactory());
        Assert.Throws<InvalidOperationException>(() => transitionFactory.CreateTransitionId());
        Assert.Throws<InvalidOperationException>(() => transitions.Export());
        var aborted = SpiritualWoundReplayJournal.CreateAppend("[]");
        aborted.Invalidate();
        Assert.Throws<InvalidOperationException>(() => aborted.Export());
    }

    /// <summary>
    /// Builds a valid real capacity initialization followed by an ordinary spend.
    /// </summary>
    /// <returns>
    /// Resource input exercising both typed factory overload families.
    /// </returns>
    private static AcceptedMechanicsResourceInput SpiritualReplayCapacityInput()
    {
        var definitions = ResourceDefinitionCatalog.CreateBuiltIn();
        Assert.True(definitions.TryResolveExact("charges", out var definition));
        var capacity = ResolvedResourceCapacity.Resolve(definition!, ChargesCoordinate,
            new InstanceFixedCapacityInput(new ResourceFormulaOwner(ChargesCoordinate.Realm,
                ChargesCoordinate.OwnerKind, ChargesCoordinate.ResourceOwnerId), 10m, FingerprintA),
            instanceAuthorityKey: "capacity_item_alpha_charges", includeInitialization: true);
        Assert.True(capacity.IsValid);
        var history = ResourceHistoryState.ParseCanonical("{\"schemaVersion\":1,\"entries\":[]}",
            definitions, allowMissingPristine: false).History!;
        var sources = CreateCatalog(new ResourceMutationSourceExport("action_cost", "action_alpha",
            FingerprintA, ResourceMutationSourceState.Active, SameTurn: false, ChargesOwner));
        return new AcceptedMechanicsResourceInput(2, definitions,
            new ResourceStateLedger(Array.Empty<ResourceStateEntry>()), history, sources,
            new[] { Intent("turn_2:resource:2", "action_cost", "action_alpha", ResourceOperation.Spend, 2m) },
            CapacityTransitions: new[]
            {
                new ResourceCapacityIntent("turn_2:resource:1", "setting_materialization", "item_alpha",
                    ChargesCoordinate, ResourceCapacityOperation.Initialize, capacity.Capacity,
                    ResourceCurrentDisposition.InitializeFromDefinition, ResourceMutationPhase.RegisteredSystemOutcome,
                    50, new ResourceSourceEvidence("setting_materialization", "item_alpha", FingerprintA),
                    capacity.Capacity!.Initialization!.AuthorityFingerprint, null)
            });
    }
}
