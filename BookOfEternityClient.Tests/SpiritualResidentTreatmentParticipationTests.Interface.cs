using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class SpiritualResidentTreatmentParticipationTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task UnownedMemoryIsNotWhitelistedIntoOrdinaryInterfacePolicy(bool graphsPresent, bool nullMemory)
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-resident-interface-" + Guid.NewGuid().ToString("N"));
        try
        {
            var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance); files.EnsureDirectoryStructure();
            await files.WriteFileAtomicAsync(GuardianAbodeResidentState.StatePath, ResidentRoster().ToJsonString());
            var roster = await files.ReadFileBytesAsync(GuardianAbodeResidentState.StatePath);
            var response = new JsonObject
            {
                ["dialogueOptions"] = new JsonArray(), ["timestamp"] = DateTime.UtcNow.ToString("o"),
                [SpiritualTreatmentParticipation.ResidentThoughtResponseProperty] = nullMemory ? null : new JsonArray(ResidentMemoryEntry("memory_unowned"))
            };
            if (graphsPresent) response["spiritualHealingGraphs"] = new JsonArray();
            await files.WriteFileAtomicAsync("output/interface_updates.json", response.ToJsonString());
            var issues = await new ValidationService(files, NullLogger<ValidationService>.Instance).ValidateAcceptedTurnInterfacePayloadAsync();
            Assert.Contains(issues, issue => issue.Code == "spiritual_healing_interface_unbound");
            Assert.Contains(issues, issue => issue.Code == "interface_updates_unknown_field" && issue.Actual == SpiritualTreatmentParticipation.ResidentThoughtResponseProperty);
            Assert.Equal(roster, await files.ReadFileBytesAsync(GuardianAbodeResidentState.StatePath));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }
}
