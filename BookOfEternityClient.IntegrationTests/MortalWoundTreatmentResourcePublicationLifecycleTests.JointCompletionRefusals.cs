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
            default:
                throw new InvalidDataException("Unsupported joint refusal fixture corruption: " + corruption);
        }
        var previous = Assert.IsType<byte[]>(await files.ReadFileBytesAsync(lease, target));
        Assert.True(replacement is null || !previous.AsSpan().SequenceEqual(replacement), "The targeted corruption must change actual bytes.");
        var physicalPath = Path.Combine(files.GameSessionPath, target.Replace('/', Path.DirectorySeparatorChar));
        // This is deliberate negative corruption of one real owned member. The
        // frozen schedule is a beforeimage, never an accepted authority or reseal.
        if (replacement is null) File.Delete(physicalPath);
        else await File.WriteAllBytesAsync(physicalPath, replacement);
        Assert.Equal(replacement, await files.ReadFileBytesAsync(lease, target));
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new
        {
            Boundary = "fr063_joint_refusal_targeted_tamper",
            Observation = new { Corruption = corruption, Target = target, BeforeSha256 = MortalArchiveHash(previous),
                AfterSha256 = replacement is null ? null : MortalArchiveHash(replacement) }
        }));
    }
}
