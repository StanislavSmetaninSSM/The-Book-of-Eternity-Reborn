using System.Text.Json;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Theory]
    [InlineData(nameof(WriteValidationRepairRequestAsync_PendingGuardianCreationMaterializationErrors_AddsConcreteHarnessPacket), "Actor_Profiles,Guardian_Contracts", "guardian_pending_creation_materialization_repair")]
    [InlineData(nameof(WriteValidationRepairRequestAsync_ActorMemoryErrors_AddsNarrowPersistencePacket), "Actor_Profiles,Story_Memory", "actor_memory_persistence_repair")]
    [InlineData(nameof(WriteValidationRepairRequestAsync_FactionMaterializationRepair_ShiningExactCrossLinks_UsesOnlyNamedAuthorityRoots), "Shining_Actions", "faction_materialization_repair")]
    [InlineData(nameof(WriteValidationRepairRequestAsync_ActorMaterializationErrors_AddsBoundedHarnessPacket), "Actor_Profiles", "actor_materialization_repair")]
    [InlineData(nameof(WriteValidationRepairRequestAsync_AfterlifeActionCostSequenceErrors_AddsConcreteHarnessPacket), "Spiritual_Conflict", "afterlife_spiritual_conflict_action_cost_repair")]
    [InlineData(nameof(WriteValidationRepairRequestAsync_AfterlifeConflictRewardNotAllowed_AddsConcreteHarnessPacket), "Spiritual_Conflict", "afterlife_spiritual_conflict_reward_repair")]
    [InlineData(nameof(WriteValidationRepairRequestAsync_AfterlifeEntityProfileScaffoldErrors_AddsConcreteHarnessPacket), "Actor_Profiles", "afterlife_entity_profile_scaffold_repair")]
    [InlineData(nameof(WriteValidationRepairRequestAsync_AfterlifeChronicleStringArrayErrors_AddsConcreteHarnessPacket), "Story_Memory", "afterlife_chronicle_string_array_repair")]
    [InlineData(nameof(WriteValidationRepairRequestAsync_GuardianTradeInventoryResolutionErrors_AddsConcreteHarnessPacket), "Guardian_Contracts", "guardian_trade_inventory_resolution_repair")]
    public async Task GmDocumentationRepairPacketsResolveActualBoundedBodies(string original, string topics, string kind)
    {
        using var strict = new CleanupOwnedFixture(_rootPath, line => _directGachaOutput?.WriteLine(line));
        switch (original)
        {
            case nameof(WriteValidationRepairRequestAsync_PendingGuardianCreationMaterializationErrors_AddsConcreteHarnessPacket): await WriteValidationRepairRequestAsync_PendingGuardianCreationMaterializationErrors_AddsConcreteHarnessPacket(); break;
            case nameof(WriteValidationRepairRequestAsync_ActorMemoryErrors_AddsNarrowPersistencePacket): await WriteValidationRepairRequestAsync_ActorMemoryErrors_AddsNarrowPersistencePacket(); break;
            case nameof(WriteValidationRepairRequestAsync_FactionMaterializationRepair_ShiningExactCrossLinks_UsesOnlyNamedAuthorityRoots): await WriteValidationRepairRequestAsync_FactionMaterializationRepair_ShiningExactCrossLinks_UsesOnlyNamedAuthorityRoots(); break;
            case nameof(WriteValidationRepairRequestAsync_ActorMaterializationErrors_AddsBoundedHarnessPacket): await WriteValidationRepairRequestAsync_ActorMaterializationErrors_AddsBoundedHarnessPacket(); break;
            case nameof(WriteValidationRepairRequestAsync_AfterlifeActionCostSequenceErrors_AddsConcreteHarnessPacket): await WriteValidationRepairRequestAsync_AfterlifeActionCostSequenceErrors_AddsConcreteHarnessPacket(); break;
            case nameof(WriteValidationRepairRequestAsync_AfterlifeConflictRewardNotAllowed_AddsConcreteHarnessPacket): await WriteValidationRepairRequestAsync_AfterlifeConflictRewardNotAllowed_AddsConcreteHarnessPacket(); break;
            case nameof(WriteValidationRepairRequestAsync_AfterlifeEntityProfileScaffoldErrors_AddsConcreteHarnessPacket): await WriteValidationRepairRequestAsync_AfterlifeEntityProfileScaffoldErrors_AddsConcreteHarnessPacket(); break;
            case nameof(WriteValidationRepairRequestAsync_AfterlifeChronicleStringArrayErrors_AddsConcreteHarnessPacket): await WriteValidationRepairRequestAsync_AfterlifeChronicleStringArrayErrors_AddsConcreteHarnessPacket(); break;
            case nameof(WriteValidationRepairRequestAsync_GuardianTradeInventoryResolutionErrors_AddsConcreteHarnessPacket): await WriteValidationRepairRequestAsync_GuardianTradeInventoryResolutionErrors_AddsConcreteHarnessPacket(); break;
            default: throw new ArgumentOutOfRangeException(nameof(original));
        }
        var json = await _fs.ReadFileAsync("game_state/control/validation_repair_request.json");
        Assert.False(string.IsNullOrWhiteSpace(json));
        using var document = JsonDocument.Parse(json!);
        var packet = Assert.Single(document.RootElement.GetProperty("harnessRepairPackets").EnumerateArray(),
            candidate => candidate.GetProperty("kind").GetString() == kind);
        var refs = packet.GetProperty("templateRefs").EnumerateArray().Select(value => value.GetString()!).ToArray();
        Assert.Contains("CLI_Rules_Index.md", refs);
        foreach (var topic in topics.Split(','))
        {
            Assert.Contains("OtherGuides/Afterlife_" + topic + ".md", refs);
            Assert.Contains("Examples/E_CLI_Afterlife_" + topic + ".txt", refs);
        }
        var bodyRefs = refs.Where(path => path.StartsWith("OtherGuides/Afterlife_", StringComparison.Ordinal) ||
            path.StartsWith("Examples/E_CLI_Afterlife_", StringComparison.Ordinal))
            .Where(path => path != "OtherGuides/Afterlife_Contract_Matrix.md" && path != "Examples/E_CLI_Afterlife_Turns.txt").ToArray();
        Assert.Equal(topics.Split(',').Length * 2, bodyRefs.Length);
        Assert.All(refs, path => Assert.True(File.Exists(Path.Combine(TestRepoPaths.RepoRoot, path)), path));
    }
}
