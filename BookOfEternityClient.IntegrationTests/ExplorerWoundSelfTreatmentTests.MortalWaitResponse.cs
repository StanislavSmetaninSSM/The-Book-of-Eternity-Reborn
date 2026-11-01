using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class ExplorerWoundSelfTreatmentTests
{
    // Tests the actual controlled GM writer, not an accepted turn or a minted owner.
    // No world, treatment request, dice, resource bootstrap or Engine is required.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PhysicalCourseWaitResponseUsesOrdinaryClockAndOriginalReportObligations(bool pendingBeforeTurn)
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-physical-wait-response-" + Guid.NewGuid().ToString("N"));
        var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
        files.EnsureDirectoryStructure();
        try
        {
            const int beforeMinute = 2880, elapsed = 480;
            const string interfacePath = "output/interface_updates.json";
            const string journalPath = "game_state/npcs/npc_journals.json";
            var request = new TurnRequest
            {
                SessionId = "session_physical_wait_writer", RequestId = "request_physical_wait_writer", TurnNumber = 44,
                ProgressionControl = new()
                {
                    CurrentRealm = "Mortal World", CurrentWorldTimeInMinutes = beforeMinute,
                    WorldCyclesAlreadyPendingBeforeTurn = pendingBeforeTurn ? 2 : 0,
                    FactionCyclesAlreadyPendingBeforeTurn = pendingBeforeTurn ? 1 : 0,
                    LastWorldSimulationTimeInMinutes = pendingBeforeTurn ? 2400 : beforeMinute,
                    LastFactionSimulationTimeInMinutes = pendingBeforeTurn ? 1440 : beforeMinute,
                    WorldCycleMinutes = 240, FactionCycleMinutes = 1440
                }
            };
            await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
            {
                await files.WriteFileAtomicAsync(lease, EffectAcceptedTurnInputComposer.WorldTimePath,
                    new JsonObject { ["currentTimeInMinutes"] = beforeMinute }.ToJsonString());
                await files.WriteFileAtomicAsync(lease, journalPath, "{\"NPCJournals\":[]}");
                Assert.Equal(beforeMinute, ProgressionScheduleService.ResolveFrozenWorldTime(
                    await files.ReadFileBytesAsync(lease, EffectAcceptedTurnInputComposer.WorldTimePath), beforeMinute).Minutes);
                await WriteMortalTreatmentJourneyResponseAsync(files, lease, request, elapsed);
            }

            var paths = new[] { interfacePath, "output/narrative_response.json", "output/debug_logs.json",
                "ready/turn_complete.json", EffectAcceptedTurnInputComposer.WorldTimePath, ProgressionScheduleService.ReportPath, journalPath };
            var beforeValidation = new Dictionary<string, byte[]?>();
            foreach (var path in paths) beforeValidation[path] = await files.ReadFileBytesAsync(path);
            var ready = JsonNode.Parse(Assert.IsType<string>(await files.ReadFileAsync("ready/turn_complete.json")))!.AsObject();
            Assert.Equal(request.SessionId, ready["sessionId"]!.GetValue<string>());
            Assert.Equal(request.RequestId, ready["requestId"]!.GetValue<string>());
            Assert.Equal(request.TurnNumber, ready["turnNumber"]!.GetValue<int>());
            Assert.Equal("success", ready["status"]!.GetValue<string>());
            var modified = ready["filesModified"]!.AsArray().Select(value => value!.GetValue<string>()).ToArray();
            Assert.Equal(modified.Length, modified.Distinct(StringComparer.Ordinal).Count());
            foreach (var path in paths.Take(3)) Assert.Contains(path, modified);
            Assert.Equal("{\"NPCJournals\":[]}", await files.ReadFileAsync(journalPath));
            if (pendingBeforeTurn)
            {
                Assert.Contains(ProgressionScheduleService.ReportPath, modified);
                var report = JsonNode.Parse(Assert.IsType<string>(await files.ReadFileAsync(ProgressionScheduleService.ReportPath)))!
                    ["progressionProcessingReport"]!;
                Assert.Equal(request.SessionId, report["sessionId"]!.GetValue<string>());
                Assert.Equal(request.RequestId, report["requestId"]!.GetValue<string>());
                Assert.Equal(request.TurnNumber, report["turnNumber"]!.GetValue<int>());
                Assert.Equal(2, report["worldCyclesProcessed"]!.GetValue<int>());
                Assert.Equal(1, report["factionCyclesProcessed"]!.GetValue<int>());
                Assert.Equal(beforeMinute, report["newLastWorldSimulationTimeInMinutes"]!.GetValue<int>());
                Assert.Equal(beforeMinute, report["newLastFactionSimulationTimeInMinutes"]!.GetValue<int>());
            }
            else
            {
                Assert.Null(beforeValidation[ProgressionScheduleService.ReportPath]);
                Assert.DoesNotContain(ProgressionScheduleService.ReportPath, modified);
            }

            var issues = await new ValidationService(files, NullLogger<ValidationService>.Instance)
                .ValidateAcceptedTurnInterfacePayloadAsync();
            foreach (var pair in beforeValidation)
                Assert.Equal(pair.Value, await files.ReadFileBytesAsync(pair.Key));
            output.WriteLine(JsonSerializer.Serialize(new { pendingBeforeTurn, modified,
                actualInterface = await files.ReadFileAsync(interfacePath),
                actualClock = await files.ReadFileAsync(EffectAcceptedTurnInputComposer.WorldTimePath),
                issues = issues.Select(issue => new { issue.Code, issue.Actual }) }));
            if (issues.Count != 0)
            {
                var issue = Assert.Single(issues);
                Assert.Equal("interface_updates_unknown_field", issue.Code);
                Assert.Equal("timeChange", issue.Actual);
            }
            // Intended RED against the current actual writer: all preceding premises
            // pass, and only this closed-interface oracle fails. Clock expectations
            // below are GREEN obligations, not falsely claimed RED premises.
            Assert.Empty(issues);

            var clock = JsonNode.Parse(Assert.IsType<string>(await files.ReadFileAsync(EffectAcceptedTurnInputComposer.WorldTimePath)))!.AsObject();
            Assert.Equal(new[] { "timeChange" }, clock.Select(pair => pair.Key).ToArray());
            Assert.Equal(elapsed, clock["timeChange"]!.GetValue<int>());
            var resolved = ProgressionScheduleService.ResolveFrozenWorldTime(
                beforeValidation[EffectAcceptedTurnInputComposer.WorldTimePath], beforeMinute);
            Assert.False(resolved.HasUnresolvedAbsoluteOverride);
            Assert.Equal(beforeMinute + elapsed, resolved.Minutes);
            var expectedModified = new List<string> { "output/narrative_response.json", interfacePath, "output/debug_logs.json",
                EffectAcceptedTurnInputComposer.WorldTimePath };
            if (pendingBeforeTurn) expectedModified.Add(ProgressionScheduleService.ReportPath);
            Assert.Equal(expectedModified.Order(StringComparer.Ordinal), modified.Order(StringComparer.Ordinal));
            Assert.Null(await files.ReadFileBytesAsync(AcceptedMechanicsPlan.WoundCommandPath));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            Assert.False(Directory.Exists(root));
        }
    }
}
