using System.Text.Json;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class ResourceDefinitionCatalogTests
{
    /// <summary>
    /// Recreates the actual catalog definition and seal without generating another identity pair.
    /// </summary>
    [Fact]
    public void SpiritualReplay_DefinitionOwnerPreservesBothIdentities()
    {
        var command = new ResourceDefinitionCreationCommand(0, "definition_ref_mana", CreateProposal(), "turn_42:definition:1", "Create mana");
        var journal = SpiritualWoundReplayJournal.CreateAppend("[]");
        var factory = new SpiritualWoundResourceIdentityFactory(journal, new AcceptedMechanicsIdentityFactory());
        using var document = JsonDocument.Parse(command.Definition.ToJsonString());
        var first = ResourceDefinitionCatalog.MaterializeProposal(document.RootElement,
            ResourceDefinitionCatalog.CreateBuiltIn(), 42, command.EventRef,
            () => new ResourceDefinitionIdentity(factory.CreateDefinitionId(command, 42), factory.CreateDefinitionSeal(command, 42)));
        Assert.True(first.IsValid);
        Assert.Equal(2, journal.Export().Count);
        var replay = SpiritualWoundReplayJournal.CreateReplay(journal.Export().ToJsonString());
        var restored = new SpiritualWoundResourceIdentityFactory(replay, new ForbiddenDefinitionAllocationFactory());
        var second = ResourceDefinitionCatalog.MaterializeProposal(document.RootElement,
            ResourceDefinitionCatalog.CreateBuiltIn(), 42, command.EventRef,
            () => new ResourceDefinitionIdentity(restored.CreateDefinitionId(command, 42), restored.CreateDefinitionSeal(command, 42)));
        Assert.True(second.IsValid);
        Assert.Equal(JsonSerializer.Serialize(first.Definition), JsonSerializer.Serialize(second.Definition));
        Assert.Equal(journal.Export().ToJsonString(), replay.Export().ToJsonString());
    }

    /// <summary>
    /// Keeps command origin in the comparison key despite its non-public CLR properties.
    /// </summary>
    [Fact]
    public void SpiritualReplay_DefinitionFactoryRejectsChangedOrigin()
    {
        var command = new ResourceDefinitionCreationCommand(0, "definition_ref_mana", CreateProposal(), "turn_42:definition:1", "Create mana");
        var journal = SpiritualWoundReplayJournal.CreateAppend("[]");
        new SpiritualWoundResourceIdentityFactory(journal, new AcceptedMechanicsIdentityFactory()).CreateDefinitionId(command, 42);
        var replay = SpiritualWoundReplayJournal.CreateReplay(journal.Export().ToJsonString());
        var changed = new ResourceDefinitionCreationCommand(0, "definition_ref_other", command.Definition, command.EventRef, command.Reason);
        var factory = new SpiritualWoundResourceIdentityFactory(replay, new ForbiddenDefinitionAllocationFactory());
        Assert.Throws<InvalidOperationException>(() => factory.CreateDefinitionId(changed, 42));
        Assert.Throws<InvalidOperationException>(() => replay.Export());
    }

    /// <summary>
    /// Makes fresh definition randomness during strict replay observable.
    /// </summary>
    private sealed class ForbiddenDefinitionAllocationFactory : AcceptedMechanicsIdentityFactory
    {
        /// <inheritdoc/>
        internal override string CreateDefinitionId(ResourceDefinitionCreationCommand creation, int turn) =>
            throw new NotSupportedException("Replay requested a fresh definition identity.");

        /// <inheritdoc/>
        internal override string CreateDefinitionSeal(ResourceDefinitionCreationCommand creation, int turn) =>
            throw new NotSupportedException("Replay requested a fresh definition seal.");
    }
}
