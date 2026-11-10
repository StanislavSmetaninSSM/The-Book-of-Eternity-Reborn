using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.IO;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class SpiritualResidentTreatmentParticipationTests
{
    [Theory]
    [InlineData("valid")]
    [InlineData("existing_id")]
    [InlineData("case_alias")]
    [InlineData("duplicate_new")]
    [InlineData("foreign_resident")]
    [InlineData("foreign_turn")]
    [InlineData("null_title")]
    [InlineData("empty_summary")]
    [InlineData("bad_timestamp")]
    [InlineData("unknown_field")]
    [InlineData("null_row")]
    [InlineData("empty_array")]
    public void ProtectedMemoryAppendsOnlyExactNewCurrentParticipantEntries(string scenario)
    {
        var prior = ResidentMemoryEntry("memory_prior");
        ActorJournalState.NormalizeEntryObject(prior, "residentId");
        var before = new JsonObject { [GuardianAbodeResidentState.ThoughtJournalProperty] = new JsonArray(prior) };
        var entry = ResidentMemoryEntry("memory_current"); var commands = new JsonArray(entry);
        switch (scenario)
        {
            case "existing_id": entry["entryId"] = "memory_prior"; break;
            case "case_alias": entry["entryId"] = "MEMORY_PRIOR"; break;
            case "duplicate_new": commands.Add(entry.DeepClone()); break;
            case "foreign_resident": entry["residentId"] = "resident_elsewhere"; break;
            case "foreign_turn": entry["turn"] = 43; break;
            case "null_title": entry["title"] = null; break;
            case "empty_summary": entry["summary"] = " "; break;
            case "bad_timestamp": entry["timestamp"] = "yesterday"; break;
            case "unknown_field": entry["permission"] = true; break;
            case "null_row": commands.Add((JsonNode?)null); break;
            case "empty_array": commands.Clear(); break;
        }
        var frozen = before.ToJsonString(); var proposed = commands.ToJsonString();
        if (scenario == "valid")
        {
            var admitted = SpiritualTreatmentParticipation.ValidateResidentThoughtAppend(commands, "resident_aid", 44, before);
            Assert.Single(admitted);
            var projected = before.DeepClone().AsObject();
            GuardianAbodeResidentState.ApplyThoughtJournalUpdates(projected, admitted);
            Assert.Equal(2, projected[GuardianAbodeResidentState.ThoughtJournalProperty]!.AsArray().Count);
            Assert.True(JsonNode.DeepEquals(prior, projected[GuardianAbodeResidentState.ThoughtJournalProperty]![0]));
            admitted[0]!["summary"] = "Изменена только отделённая выдача.";
        }
        else Assert.Throws<InvalidDataException>(() => SpiritualTreatmentParticipation.ValidateResidentThoughtAppend(commands, "resident_aid", 44, before));
        Assert.Equal(frozen, before.ToJsonString()); Assert.Equal(proposed, commands.ToJsonString());
    }

    [Fact]
    public async Task TypedProtectedMemoryUsesTheExistingResponseTransportWithoutWritingTheRoster()
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-resident-memory-" + Guid.NewGuid().ToString("N"));
        try
        {
            var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            files.EnsureDirectoryStructure();
            var roster = ResidentRoster().ToJsonString();
            await files.WriteFileAtomicAsync(GuardianAbodeResidentState.StatePath, roster);
            var before = await files.ReadFileBytesAsync(GuardianAbodeResidentState.StatePath);
            var entry = ResidentMemoryEntry("memory_typed");
            var response = JsonSerializer.Deserialize<GameResponse>(new JsonObject
            { [SpiritualTreatmentParticipation.ResidentThoughtResponseProperty] = new JsonArray(entry) }.ToJsonString())!;
            var distributor = new StateDistributor(files, NullLogger<StateDistributor>.Instance);
            var written = await distributor.DistributeAsync(response);
            Assert.DoesNotContain(GuardianAbodeResidentState.StatePath, written);
            Assert.Equal(before, await files.ReadFileBytesAsync(GuardianAbodeResidentState.StatePath));
            var output = JsonNode.Parse((await files.ReadFileAsync("output/interface_updates.json"))!)!.AsObject();
            Assert.True(JsonNode.DeepEquals(new JsonArray(entry.DeepClone()), output[SpiritualTreatmentParticipation.ResidentThoughtResponseProperty]));
            var refreshed = GameResponseRefreshMerger.Merge(response, new GameResponse());
            Assert.Single(refreshed.SpiritualResidentThoughtJournalUpdates!);
            Assert.Equal(entry.ToJsonString(), JsonNode.Parse(refreshed.SpiritualResidentThoughtJournalUpdates![0].GetRawText())!.ToJsonString());
            Assert.Empty(GameResponseRefreshMerger.Merge(response, new GameResponse { SpiritualResidentThoughtJournalUpdates = [] }).SpiritualResidentThoughtJournalUpdates!);

            // Ordinary resident interactions retain their existing mapped route.
            await distributor.DistributeAsync(new GameResponse
                { ResidentThoughtJournalUpdates = [JsonSerializer.SerializeToElement(entry)] });
            var ordinary = JsonNode.Parse((await files.ReadFileAsync(GuardianAbodeResidentState.StatePath))!)!.AsObject();
            Assert.Single(ordinary[GuardianAbodeResidentState.UpdateThoughtJournalProperty]!.AsArray());
            Assert.False(ordinary.ContainsKey(SpiritualTreatmentParticipation.ResidentThoughtResponseProperty));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task FailedProtectedResponseDistributionRestoresExactOutputAndLeavesRosterUntouched()
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-resident-memory-fault-" + Guid.NewGuid().ToString("N"));
        try
        {
            var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance); files.EnsureDirectoryStructure();
            await files.WriteFileAtomicAsync(GuardianAbodeResidentState.StatePath, ResidentRoster().ToJsonString());
            await files.WriteFileAtomicAsync("output/interface_updates.json", "{\"dialogueOptions\":[]}");
            var roster = await files.ReadFileBytesAsync(GuardianAbodeResidentState.StatePath);
            var output = await files.ReadFileBytesAsync("output/interface_updates.json");
            var cuts = 0;
            var distributor = new StateDistributor(files, NullLogger<StateDistributor>.Instance, new StateDistributorHooks
            {
                AfterFileMutationAppliedAsync = path =>
                {
                    if (path == "output/interface_updates.json") { cuts++; throw new InvalidOperationException("Protected memory response cut."); }
                    return Task.CompletedTask;
                }
            });
            var failure = await Record.ExceptionAsync(() => distributor.DistributeAsync(new GameResponse
                { SpiritualResidentThoughtJournalUpdates = [JsonSerializer.SerializeToElement(ResidentMemoryEntry("memory_fault"))] }));
            Assert.NotNull(failure); Assert.Equal(1, cuts);
            Assert.Equal(roster, await files.ReadFileBytesAsync(GuardianAbodeResidentState.StatePath));
            Assert.Equal(output, await files.ReadFileBytesAsync("output/interface_updates.json"));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }

    private static JsonObject ResidentMemoryEntry(string entryId) => new()
    {
        ["entryId"] = entryId, ["residentId"] = "resident_aid", ["turn"] = 44,
        ["timestamp"] = "2026-10-29T00:00:00Z", ["title"] = "Осознанное решение",
        ["summary"] = "Я понимаю предложение души и добровольно принимаю помощь."
    };
}
