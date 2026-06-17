using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GuardianSystemRegressionTests
{
    [Fact]
    public async Task ValidateCrossReferences_AllowsNpcSurfacesWithoutGuardianSnapshotWhenIdle()
    {
        _fs.DeleteFile("input/turn_request.json");
        _fs.DeleteFile("ready/turn_complete.json");
        _fs.DeleteFile("ready/turn_error.json");
        _fs.DeleteFile("game_state/control/pending_turn_snapshot.json");
        _fs.DeleteFile("game_state/control/pending_turn_snapshot.authority.json");
        var snapshotDirectory = _fs.ResolvePath("game_state/control/pending_turn_snapshot");
        if (Directory.Exists(snapshotDirectory))
            Directory.Delete(snapshotDirectory, recursive: true);

        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_core.json", """
        {
          "NPCsInScene": [
            {
              "NPCId": "npc_idle_broker",
              "name": "Мирна"
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_inventory.json", """
        {
          "NPCInventoryAdds": [
            {
              "NPCId": "npc_idle_broker",
              "NPCName": "Мирна",
              "item": {
                "itemId": "npc_idle_note",
                "name": "Записка Мирны"
              }
            }
          ]
        }
        """);

        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_npc_boundary_missing_validated_preturn_guardians_snapshot", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "guardian_npc_command_crossrefs_missing_validated_preturn_guardians_snapshot", StringComparison.OrdinalIgnoreCase));
    }
}
