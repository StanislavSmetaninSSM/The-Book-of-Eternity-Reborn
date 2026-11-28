using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

public sealed class MortalJointProgressionReplayTests(ITestOutputHelper output)
{
    [Fact]
    public async Task FrozenDataRebindsToFreshLeaseWithoutReusingOldAuthority()
    {
        using var fixture = new Fixture(output);
        var control = await fixture.InitializeAsync();
        ProgressionScheduleService.FrozenMortalOutcomeInputs first;
        await using (var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync())
            first = await fixture.Service.CaptureMortalOutcomeInputsAsync(lease, control, "s", "r", 1);
        await using var fresh = await fixture.Files.AcquireCanonicalWriteLeaseAsync();
        await Assert.ThrowsAsync<InvalidDataException>(() => first.ValidateCurrentAsync(fixture.Files, fresh));
        var second = await fixture.Service.RecaptureMortalOutcomeInputsAsync(fresh, control, "s", "r", 1,
            first.ToDiagnosticJson(), first.Fingerprint);
        Assert.NotSame(first, second);
        Assert.Equal(first.Fingerprint, second.Fingerprint);
        Assert.True(JsonNode.DeepEquals(first.ToDiagnosticJson(), second.ToDiagnosticJson()));
        await second.ValidateCurrentAsync(fixture.Files, fresh);
    }

    [Theory]
    [InlineData("source")]
    [InlineData("absence")]
    [InlineData("control")]
    [InlineData("tuple")]
    [InlineData("fingerprint")]
    [InlineData("body")]
    public async Task FrozenDataRejectsChangedDependencies(string change)
    {
        using var fixture = new Fixture(output);
        var control = await fixture.InitializeAsync();
        ProgressionScheduleService.FrozenMortalOutcomeInputs first;
        await using (var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync())
            first = await fixture.Service.CaptureMortalOutcomeInputsAsync(lease, control, "s", "r", 1);
        var body = first.ToDiagnosticJson();
        if (change == "source") await fixture.Files.WriteFileAtomicAsync("input/turn_request.json", "{}");
        if (change == "absence") await fixture.Files.WriteFileAtomicAsync(ProgressionScheduleService.ReportPath, "{}");
        if (change == "control") control.CurrentWorldTimeInMinutes++;
        if (change == "body") body["preparedAtUtc"] = "2020-01-01T00:00:00.0000000Z";
        await using var fresh = await fixture.Files.AcquireCanonicalWriteLeaseAsync();
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Service.RecaptureMortalOutcomeInputsAsync(
            fresh, control, "s", change == "tuple" ? "other" : "r", 1, body,
            change == "fingerprint" ? "foreign" : first.Fingerprint));
    }

    private sealed class Fixture : IDisposable
    {
        private readonly CleanupOwnedFixture _owned;
        internal FileSystemManager Files { get; }
        internal ProgressionScheduleService Service { get; }
        internal Fixture(ITestOutputHelper output)
        {
            var root = Path.Combine(Path.GetTempPath(), "boe-progression-replay-" + Guid.NewGuid().ToString("N"));
            _owned = new(root, output.WriteLine);
            Files = new(root, NullLogger<FileSystemManager>.Instance);
            Files.EnsureDirectoryStructure();
            Service = new(Files, NullLogger<ProgressionScheduleService>.Instance);
        }
        internal async Task<ProgressionControl> InitializeAsync()
        {
            await Files.WriteFileAtomicAsync("game_state/meta/soul_state.json", """{"currentRealm":"Mortal World"}""");
            await Files.WriteFileAtomicAsync("input/turn_request.json", """
                {"sessionId":"s","requestId":"r","turnNumber":1,"playerAction":"wait","gameMode":"normal"}
                """);
            return await Service.BuildControlForNextTurnAsync();
        }
        public void Dispose() => _owned.Dispose();
    }
}
