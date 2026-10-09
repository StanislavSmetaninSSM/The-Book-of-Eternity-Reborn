using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Theory]
    [InlineData("conflict")]
    [InlineData("profiles")]
    [InlineData("chronicle")]
    [InlineData("flags")]
    [InlineData("authority")]
    [InlineData("backup")]
    [InlineData("backup_cleanup")]
    public async Task PreparationUnknown_OriginalEngineStopsAfterActualDecision(string mode)
    {
        using var owned = new CleanupOwnedFixture(_rootPath, line => _directGachaOutput?.WriteLine(line));
        Assert.True(OperatingSystem.IsLinux());
        using var probe = new PreparationPublicationProbe();
        var files = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, probe.Hooks);
        probe.Attach(files);
        files.EnsureDirectoryStructure();
        await files.WriteFileAtomicAsync("game_state/meta/soul_state.json", "{\"currentRealm\":\"Chaos Sea\",\"currentIncarnation\":2}");
        var roots = new Dictionary<string, (string Path, JsonObject Root)>
        {
            ["conflict"] = (AfterlifeSpiritualConflictState.StatePath, AfterlifeSpiritualConflictState.CreateDefaultRoot()),
            ["profiles"] = (AfterlifeEntityProfileState.StatePath, AfterlifeEntityProfileState.CreateDefaultRoot()),
            ["chronicle"] = (AfterlifeChronicleState.StatePath, AfterlifeChronicleState.CreateDefaultRoot()),
            ["flags"] = (AfterlifeGlobalFlagState.StatePath, AfterlifeGlobalFlagState.CreateDefaultRoot())
        };
        foreach (var (key, value) in roots)
            if (key != mode) await files.WriteFileAtomicAsync(value.Path, value.Root.ToJsonString());
        await files.WriteFileAtomicAsync("lore/codex_entries.json", "{\"entries\":[]}");
        var input = new ProgressionNoInput();
        var engine = CreateGameEngine(input, s => { s.GmBridgeAutoStart = false; s.MusicEnabled = false; s.SoundEnabled = false; }, fileSystem: files);
        const string backupId = "preparation_outcome";
        var suffix = ".rollback." + backupId;
        var known = new InvalidOperationException("known second backup capture refusal");
        var knownCuts = 0;
        string? firstBackup = null;
        var isBackup = mode.StartsWith("backup", StringComparison.Ordinal);
        probe.BeforeMutation = path =>
        {
            if (probe.Cut.Armed && mode == "backup_cleanup" && knownCuts == 0 &&
                path.EndsWith(suffix, StringComparison.Ordinal) && probe.CommittedBackups(suffix).Length == 1 &&
                files.ResolvePath(path) != probe.CommittedBackups(suffix)[0])
            {
                firstBackup = probe.CommittedBackups(suffix)[0];
                knownCuts++;
                throw known;
            }
            return Task.CompletedTask;
        };
        probe.Select = (path, member) => isBackup
            ? mode == "backup_cleanup"
                ? knownCuts == 1 && path == firstBackup && !member.GetProperty("After").GetProperty("Exists").GetBoolean()
                : path.EndsWith(suffix, StringComparison.Ordinal) && probe.CommittedBackups(suffix).Length == 1 &&
                  path != probe.CommittedBackups(suffix)[0]
            : path == files.ResolvePath(mode == "authority" ? PendingTurnSnapshotAuthority.AuthorityPath : roots[mode].Path);
        probe.BeforeCut = () =>
        {
            if (isBackup) Assert.Single(probe.CommittedBackups(suffix));
            else if (mode == "authority") probe.ValidateManifest(requireAuthority: false);
            else
            {
                using var journal = CleanupPublicationCut.Metadata(File.ReadAllBytes(probe.Cut.JournalPath));
                Assert.False(journal.RootElement.GetProperty("Members")[0].GetProperty("Before").GetProperty("Exists").GetBoolean());
                Assert.NotNull(JsonNode.Parse(File.ReadAllText(probe.SelectedPath!)));
            }
        };
        probe.Arm();
        var request = CreateSnapshotByteContractRequest(mode);
        request.ProgressionControl!.CurrentRealm = "Chaos Sea";
        var failure = await Record.ExceptionAsync(async () =>
        {
            if (isBackup) await InvokePrivateTaskResultAsync(engine, "CreatePreTurnBackup", backupId);
            else await InvokePrivateTaskResultAsync(engine, "CreateCanonicalBaselineSnapshotAsync", request, null, "preparation-outcome");
        });
        _directGachaOutput?.WriteLine(JsonSerializer.Serialize(new
        {
            mode, knownCuts, firstBackup, Failure = failure?.ToString(),
            KnownCauseRetained = probe.Cut.RetainsDiagnostic(known), input.Reads, Evidence = probe.Evidence()
        }));
        probe.AssertStopped(failure);
        Assert.Equal(0, input.Reads);
        if (mode == "backup_cleanup") { Assert.Equal(1, knownCuts); Assert.True(probe.Cut.RetainsDiagnostic(known)); }
    }

    [Fact]
    public async Task PreparationUnknown_OriginalLivePrepareStopsAfterPublishedRequest()
    {
        using var owned = new CleanupOwnedFixture(_rootPath, line => _directGachaOutput?.WriteLine(line));
        Assert.True(OperatingSystem.IsLinux());
        using var probe = new PreparationPublicationProbe();
        var files = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, probe.Hooks);
        probe.Attach(files);
        files.EnsureDirectoryStructure();
        await files.WriteFileAtomicAsync("game_state/meta/soul_state.json", "{\"currentRealm\":\"Mortal World\",\"currentIncarnation\":2}");
        await files.WriteFileAtomicAsync("lore/codex_entries.json", "{\"entries\":[]}");
        probe.Select = (path, _) => path == files.ResolvePath("input/turn_request.json");
        probe.BeforeCut = () =>
        {
            var manifest = probe.ValidateManifest(requireAuthority: true);
            var request = JsonNode.Parse(File.ReadAllText(probe.SelectedPath!))!.AsObject();
            Assert.Equal(manifest["requestId"]!.GetValue<string>(), request["requestId"]!.GetValue<string>());
        };
        probe.Arm();
        var failure = await Record.ExceptionAsync(() => new LiveTurnPreparationService(files).PrepareAsync(new()
        {
            SessionId = "preparation-outcome", RequestId = "live-preparation-outcome", TurnNumber = 2,
            PlayerAction = "bounded local preparation", PreGeneratedDices1d20 = [7, 11, 13]
        }));
        _directGachaOutput?.WriteLine(JsonSerializer.Serialize(new { Failure = failure?.ToString(), Evidence = probe.Evidence() }));
        probe.AssertStopped(failure);
    }
}
