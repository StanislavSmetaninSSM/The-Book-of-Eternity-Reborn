using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineLivingWorldRollbackTests
{
    [Fact]
    public async Task LateCriticalHealthRepair_RequestsWholeOriginalResubmissionBeforeNormalizationReentry()
    {
        Fixture? current = null;
        var reached = false;
        var input = new CriticalRepairEscapeInput(() => current!.Files);
        var hooks = new FileSystemManagerHooks
        {
            BeforeCanonicalReadOpenAsync = path =>
            {
                if (reached || current is null || path != "lore/codex_entries.json" ||
                    !File.Exists(current.Files.ResolvePath(MortalLivingWorldSource.HistoryPath)) ||
                    File.Exists(current.Files.ResolvePath(MortalLivingWorldSource.ReportPath))) return Task.CompletedTask;
                // Only after the real ordinary owner has completed its sidecar publication.
                var history = JsonNode.Parse(File.ReadAllText(current.Files.ResolvePath(MortalLivingWorldSource.HistoryPath)))!;
                Assert.Single(history["entries"]!.AsArray());
                reached = true;
                File.WriteAllText(current.Files.ResolvePath(path), "[]");
                return Task.CompletedTask;
            }
        };
        await using var fixture = await Fixture.CreateAsync(hooks, input, criticalRepair: true);
        current = fixture;
        await fixture.SubmitAsync();

        var result = await fixture.Runtime.Engine.ValidatePreparedLivingWorldOutcomeAsync(
            "living-world critical-health reentry", fixture.Rollback, 42, fixture.Progression);

        Assert.True(reached, "The real accepted owner must publish the living-world receipt before the critical-health failure. Repair observed: " + input.Repair);
        Assert.Equal(AcceptedTurnValidationDisposition.TerminalRejected, result); // User cancels only after dispatch observation.
        var repair = Assert.IsType<JsonObject>(input.Repair);
        Assert.Contains(repair["errors"]!.AsArray(), issue => issue!["code"]!.GetValue<string>() == "critical_state_invalid_root");
        Assert.True(repair["fullTurnResubmissionRequired"]!.GetValue<bool>());
        var paths = repair["requiredResubmissionPaths"]!.AsArray().Select(path => path!.GetValue<string>()).ToArray();
        Assert.Contains(MortalLivingWorldSource.ReportPath, paths);
        Assert.DoesNotContain(MortalLivingWorldSource.HistoryPath, paths);
        Assert.False(input.HistoryExistedAtDispatch, "Private history must return to original absence before requesting the same turn again.");
        Assert.Equal(0, input.MinuteAtDispatch);
    }

    [Fact]
    public async Task CriticalRepair_OmittedReportIsRefusedThenGenuineResubmissionPublishesOnce()
    {
        Fixture? current = null;
        var firstPublished = false;
        JsonObject? repairedHistory = null;
        var input = new CriticalRepairResubmissionInput(() => current!);
        var hooks = new FileSystemManagerHooks
        {
            BeforeCanonicalReadOpenAsync = path =>
            {
                if (current is null || path != "lore/codex_entries.json" ||
                    !File.Exists(current.Files.ResolvePath(MortalLivingWorldSource.HistoryPath)) ||
                    File.Exists(current.Files.ResolvePath(MortalLivingWorldSource.ReportPath))) return Task.CompletedTask;
                var history = JsonNode.Parse(File.ReadAllText(current.Files.ResolvePath(MortalLivingWorldSource.HistoryPath)))!.AsObject();
                if (!firstPublished)
                {
                    Assert.Single(history["entries"]!.AsArray());
                    firstPublished = true;
                    File.WriteAllText(current.Files.ResolvePath(path), "[]");
                }
                else if (input.CompletedRepairs == 2 && repairedHistory is null)
                    repairedHistory = history;
                return Task.CompletedTask;
            }
        };
        await using var fixture = await Fixture.CreateAsync(hooks, input, criticalRepair: true);
        current = fixture;
        await fixture.SubmitAsync();
        input.ReportCandidate = Assert.IsType<byte[]>(await fixture.Files.ReadFileBytesAsync(MortalLivingWorldSource.ReportPath));

        await fixture.Runtime.Engine.ValidatePreparedLivingWorldOutcomeAsync(
            "living-world repaired publication boundary", fixture.Rollback, 42, fixture.Progression);

        Assert.True(firstPublished, "The first authentic publication must precede the injected critical-health error.");
        Assert.True(input.Repairs.Count >= 2, "Expected critical corruption then omitted-report resubmission refusal. Observed: " +
            string.Join("\n", input.Repairs.Select(row => row.ToJsonString())));
        Assert.Contains(input.Repairs[0]["errors"]!.AsArray(), row => row!["code"]!.GetValue<string>() == "critical_state_invalid_root");
        Assert.Contains(input.Repairs[1]["errors"]!.AsArray(), row => row!["code"]!.GetValue<string>() == "mortal_living_world_repair_resubmission_required");
        Assert.Equal(2, input.CompletedRepairs);
        for (var index = 0; index < 2; index++)
        {
            Assert.True(input.Repairs[index]["fullTurnResubmissionRequired"]!.GetValue<bool>());
            var paths = input.Repairs[index]["requiredResubmissionPaths"]!.AsArray().Select(row => row!.GetValue<string>()).ToArray();
            Assert.Contains(MortalLivingWorldSource.ReportPath, paths);
            Assert.DoesNotContain(MortalLivingWorldSource.HistoryPath, paths);
            Assert.False(input.HistoryAtDispatch[index]);
            Assert.Equal(0, input.MinuteAtDispatch[index]);
        }
        var republished = Assert.IsType<JsonObject>(repairedHistory);
        var receipt = Assert.Single(republished["entries"]!.AsArray());
        Assert.Equal(Request, receipt!["requestId"]!.GetValue<string>());
        Assert.Equal(42, receipt["turn"]!.GetValue<int>());
        Assert.DoesNotContain(input.Repairs.SelectMany(row => row["errors"]!.AsArray()),
            issue => issue!["code"]!.GetValue<string>() == "mortal_living_world_history_mutation");
        // This intentionally ends at genuine repaired normalization/publication.
        // Any unrelated sparse-fixture full-state repair afterward is cancelled,
        // rather than claiming a complete player turn was accepted.
    }

    private sealed class CriticalRepairResubmissionInput(Func<Fixture> fixture) : IConsoleInputSource
    {
        private string? _handled;
        private bool _escaped;
        internal int CompletedRepairs { get; private set; }
        internal byte[] ReportCandidate { get; set; } = [];
        internal List<JsonObject> Repairs { get; } = new();
        internal List<bool> HistoryAtDispatch { get; } = new();
        internal List<long> MinuteAtDispatch { get; } = new();
        public bool IsScripted => true;
        public bool KeyAvailable
        {
            get
            {
                if (_escaped) return false;
                var path = fixture().Files.ResolvePath("game_state/control/validation_repair_request.json");
                return File.Exists(path) && File.ReadAllText(path) != _handled;
            }
        }
        public ConsoleKeyInfo ReadKey(bool intercept = true)
        {
            var current = fixture();
            var files = current.Files;
            _handled = File.ReadAllText(files.ResolvePath("game_state/control/validation_repair_request.json"));
            var repair = JsonNode.Parse(_handled)!.AsObject();
            Repairs.Add(repair);
            HistoryAtDispatch.Add(File.Exists(files.ResolvePath(MortalLivingWorldSource.HistoryPath)));
            MinuteAtDispatch.Add(JsonNode.Parse(File.ReadAllText(files.ResolvePath(MortalLivingWorldSource.WorldTimePath)))!["currentTimeInMinutes"]!.GetValue<long>());
            var expected = CompletedRepairs == 0 ? "critical_state_invalid_root" : "mortal_living_world_repair_resubmission_required";
            if (CompletedRepairs >= 2 || !repair["errors"]!.AsArray().Any(row => row!["code"]!.GetValue<string>() == expected))
            {
                _escaped = true;
                return new ConsoleKeyInfo('\0', ConsoleKey.Escape, false, false, false);
            }
            // Act as the GM only through ordinary candidate files and a correlated ready.
            // Never rewrite a signed original, private history, or publication receipt.
            WriteCandidate(MortalLivingWorldSource.WorldTimePath, System.Text.Encoding.UTF8.GetBytes("{\"currentTimeInMinutes\":240}"));
            if (CompletedRepairs == 1) WriteCandidate(MortalLivingWorldSource.ReportPath, ReportCandidate);
            CompletedRepairs++;
            WriteCandidate("game_state/control/validation_repair_ready.json", System.Text.Encoding.UTF8.GetBytes(new JsonObject
            {
                ["sessionId"] = Session, ["requestId"] = Request, ["turnNumber"] = 42,
                ["updatedAtUtc"] = DateTime.UtcNow.ToString("O"), ["note"] = "Resubmitted the requested current-turn candidate."
            }.ToJsonString()));
            return new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, false, false);

            void WriteCandidate(string relativePath, byte[] bytes)
            {
                var destination = files.ResolvePath(relativePath);
                var temporary = destination + ".test-candidate";
                File.WriteAllBytes(temporary, bytes);
                File.Move(temporary, destination, overwrite: true);
            }
        }
        public string? ReadLine() => string.Empty;
        public void AssertCompleted() => Assert.Equal(2, CompletedRepairs);
    }

    /// <summary>Observes real repair dispatch before cancellation removes its control files.</summary>
    private sealed class CriticalRepairEscapeInput(Func<FileSystemManager> files) : IConsoleInputSource
    {
        private bool _read;
        internal JsonObject? Repair { get; private set; }
        internal bool HistoryExistedAtDispatch { get; private set; }
        internal long MinuteAtDispatch { get; private set; }
        public bool IsScripted => true;
        public bool KeyAvailable => !_read;
        public ConsoleKeyInfo ReadKey(bool intercept = true)
        {
            _read = true;
            var fs = files();
            var path = fs.ResolvePath("game_state/control/validation_repair_request.json");
            Repair = File.Exists(path) ? JsonNode.Parse(File.ReadAllText(path))?.AsObject() : null;
            HistoryExistedAtDispatch = File.Exists(fs.ResolvePath(MortalLivingWorldSource.HistoryPath));
            MinuteAtDispatch = JsonNode.Parse(File.ReadAllText(fs.ResolvePath(MortalLivingWorldSource.WorldTimePath)))!["currentTimeInMinutes"]!.GetValue<long>();
            return new ConsoleKeyInfo('\0', ConsoleKey.Escape, false, false, false);
        }
        public string? ReadLine() => string.Empty;
        public void AssertCompleted() => Assert.True(_read);
    }
}
