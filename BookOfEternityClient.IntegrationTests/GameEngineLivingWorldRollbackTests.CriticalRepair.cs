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
