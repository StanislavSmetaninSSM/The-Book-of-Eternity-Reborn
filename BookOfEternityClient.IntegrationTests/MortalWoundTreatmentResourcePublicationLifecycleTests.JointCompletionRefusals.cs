using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Theory]
    [InlineData("missing_marker")]
    [InlineData("mutation_fingerprint")]
    [InlineData("mixed_schedule")]
    [InlineData("mixed_report")]
    [InlineData("basis_fingerprint")]
    [InlineData("input_control")]
    [InlineData("world_time")]
    public Task GuaranteedTreatment_AppliedJointRefusalPreservesEntireAfterTamperTree(string corruption) =>
        RunJointCompletionControlAsync(nonzeroProgression: true, pauseAppliedForCold: true, refusalScenario: corruption);

    private static async Task ApplyJointCompletionRefusalTamperAsync(FileSystemManager files,
        FileSystemManager.CanonicalWriteLease lease, string corruption)
    {
        var markerBytes = Assert.IsType<byte[]>(await files.ReadFileBytesAsync(lease, MortalQteDecisionPublication.StatePath));
        var marker = ParseJsonObjectBytes(markerBytes);
        var progression = Assert.IsType<JsonObject>(marker["progression"]);
        Assert.Equal("applied", progression["phase"]!.GetValue<string>());
        string target;
        byte[]? replacement;
        switch (corruption)
        {
            case "missing_marker":
                target = MortalQteDecisionPublication.StatePath;
                replacement = null;
                break;
            case "mutation_fingerprint":
                target = MortalQteDecisionPublication.StatePath;
                var fingerprint = progression["mutationFingerprint"]!.GetValue<string>();
                Assert.NotEmpty(fingerprint);
                progression["mutationFingerprint"] = fingerprint[..^1] + (fingerprint[^1] == '0' ? '1' : '0');
                replacement = System.Text.Encoding.UTF8.GetBytes(marker.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
                break;
            case "mixed_schedule":
                target = ProgressionScheduleService.SchedulePath;
                Assert.True(progression["scheduleBefore"]!["exists"]!.GetValue<bool>());
                replacement = Convert.FromBase64String(progression["scheduleBefore"]!["bytes"]!.GetValue<string>());
                break;
            case "mixed_report":
                target = ProgressionScheduleService.ReportPath;
                Assert.True(progression["reportBefore"]!["exists"]!.GetValue<bool>());
                Assert.False(progression["reportAfter"]!["exists"]!.GetValue<bool>());
                replacement = Convert.FromBase64String(progression["reportBefore"]!["bytes"]!.GetValue<string>());
                break;
            case "basis_fingerprint":
                target = MortalQteDecisionPublication.StatePath;
                var guards = Assert.IsType<JsonObject>(progression["sourceGuards"]);
                var basisFingerprint = guards["basisFingerprint"]!.GetValue<string>();
                Assert.NotEmpty(basisFingerprint);
                guards["basisFingerprint"] = basisFingerprint[..^1] + (basisFingerprint[^1] == '0' ? '1' : '0');
                replacement = System.Text.Encoding.UTF8.GetBytes(marker.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
                break;
            case "input_control":
                target = "input/turn_request.json";
                var request = ParseJsonObjectBytes(Assert.IsType<byte[]>(await files.ReadFileBytesAsync(lease, target)));
                var control = Assert.IsType<JsonObject>(request["progressionControl"]);
                control["currentWorldTimeInMinutes"] = checked(control["currentWorldTimeInMinutes"]!.GetValue<int>() + 1);
                replacement = System.Text.Encoding.UTF8.GetBytes(request.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
                break;
            case "world_time":
                target = "game_state/world/world_time.json";
                var worldTime = ParseJsonObjectBytes(Assert.IsType<byte[]>(await files.ReadFileBytesAsync(lease, target)));
                worldTime["currentTimeInMinutes"] = checked(worldTime["currentTimeInMinutes"]!.GetValue<int>() + 1);
                replacement = System.Text.Encoding.UTF8.GetBytes(worldTime.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
                break;
            default:
                throw new InvalidDataException("Unsupported joint refusal fixture corruption: " + corruption);
        }
        var previous = await files.ReadFileBytesAsync(lease, target);
        if (corruption == "mixed_report") Assert.Null(previous);
        else Assert.IsType<byte[]>(previous);
        Assert.True(replacement is null ? previous is not null : previous is null || !previous.AsSpan().SequenceEqual(replacement),
            "The targeted corruption must change actual bytes or presence.");
        var physicalPath = Path.Combine(files.GameSessionPath, target.Replace('/', Path.DirectorySeparatorChar));
        // This is deliberate negative corruption of one real owned member. The
        // frozen schedule is a beforeimage, never an accepted authority or reseal.
        if (replacement is null) File.Delete(physicalPath);
        else await File.WriteAllBytesAsync(physicalPath, replacement);
        Assert.Equal(replacement, await files.ReadFileBytesAsync(lease, target));
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new
        {
            Boundary = "fr063_joint_refusal_targeted_tamper",
            Observation = new { Corruption = corruption, Target = target, BeforeSha256 = previous is null ? null : MortalArchiveHash(previous),
                AfterSha256 = replacement is null ? null : MortalArchiveHash(replacement) }
        }));
    }
}
