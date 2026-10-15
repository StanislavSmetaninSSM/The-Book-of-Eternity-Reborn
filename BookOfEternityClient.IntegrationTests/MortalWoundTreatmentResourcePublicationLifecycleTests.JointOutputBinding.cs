using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Fact]
    public Task GuaranteedTreatment_ExactPresentDraftColdContinuationRetainsAcceptedScene() =>
        RunJointCompletionControlAsync(nonzeroProgression: true, pauseAppliedForCold: true, refusalScenario: "draft_exact");

    [Fact]
    public Task GuaranteedTreatment_NarrativeNormalizesBeforeFirstCheckpointAndTimestampCorrectionSealsExactImage() =>
        RunJointCompletionControlAsync(nonzeroProgression: false, normalizeNarrativeBeforeCheckpoint: true);

    [Theory]
    [InlineData("output_scene_prefix")]
    [InlineData("output_timestamp")]
    [InlineData("output_formatting")]
    [InlineData("output_missing")]
    [InlineData("output_normalization")]
    public Task GuaranteedTreatment_InitiallyChangedOutputFreshProcessRefusesWithoutMutation(string corruption) =>
        RunJointCompletionControlAsync(nonzeroProgression: true, pauseAppliedForCold: true, refusalScenario: corruption);

    [Theory]
    [InlineData("binding_missing")]
    [InlineData("binding_version")]
    [InlineData("binding_local_rehash")]
    [InlineData("binding_wound_rehash")]
    [InlineData("binding_transition_rehash")]
    public Task GuaranteedTreatment_InitiallyForeignBindingFreshProcessRefusesWithoutMutation(string corruption) =>
        RunJointCompletionControlAsync(nonzeroProgression: true, pauseAppliedForCold: true, refusalScenario: corruption);

    [Theory]
    [InlineData("draft_changed")]
    [InlineData("draft_malformed")]
    [InlineData("draft_foreign")]
    [InlineData("initial_authority_digest")]
    public Task GuaranteedTreatment_InitiallyForeignDraftOrAuthorityFreshProcessRefusesWithoutMutation(string corruption) =>
        RunJointCompletionControlAsync(nonzeroProgression: true, pauseAppliedForCold: true, refusalScenario: corruption);

    private static bool IsInitialJointOutputRefusal(string? corruption) => corruption?.StartsWith("output_", StringComparison.Ordinal) == true ||
        corruption?.StartsWith("binding_", StringComparison.Ordinal) == true || corruption is "draft_changed" or "draft_malformed" or "draft_foreign" ||
        corruption == "initial_authority_digest";

    private static async Task DamageJointOutputBeforeChildLaunchAsync(FileSystemManager files, string corruption, byte[]? acceptedPublicDraft = null)
    {
        var markerPath = Path.Combine(files.GameSessionPath, MortalQteDecisionPublication.StatePath);
        var marker = ParseJsonObjectBytes(await File.ReadAllBytesAsync(markerPath));
        var target = "output/narrative_response.json";
        var bytes = await File.ReadAllBytesAsync(Path.Combine(files.GameSessionPath, target));
        byte[]? replacement;
        if (corruption.StartsWith("binding_", StringComparison.Ordinal))
        {
            target = MortalQteDecisionPublication.StatePath;
            var binding = Assert.IsType<JsonObject>(marker["outputBinding"]);
            switch (corruption)
            {
                case "binding_missing": marker.Remove("outputBinding"); break;
                case "binding_version": binding["version"] = 2; break;
                case "binding_local_rehash": binding["localWoundRef"] = "foreign_local_ref"; break;
                case "binding_wound_rehash": binding["woundId"] = "foreign_wound_id"; break;
                case "binding_transition_rehash": binding["transitionId"] = "foreign_transition_id"; break;
                default: throw new InvalidOperationException(corruption);
            }
            marker.Remove("fingerprint");
            marker["fingerprint"] = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(
                "T070-MORTAL-QTE-JOINT-COMMON-PUBLICATION-V2\n" + WoundAcceptedTurnFingerprintWriter.CanonicalJson(marker))));
            replacement = Encoding.UTF8.GetBytes(marker.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        }
        else if (corruption.StartsWith("draft_", StringComparison.Ordinal))
        {
            target = MortalWoundOpportunityAdapter.DecisionPath;
            // Public expected draft only; verify its complete accepted payload before
            // introducing the requested one-leaf corruption. It grants no authority.
            var draft = ParseJsonObjectBytes(Assert.IsType<byte[]>(acceptedPublicDraft));
            var payloadHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(
                "T070-MORTAL-QTE-PUBLIC-DECISION-V1\n" + WoundAcceptedTurnFingerprintWriter.CanonicalJson(draft))));
            Assert.Equal(marker["decisionPayloadFingerprint"]!.GetValue<string>(), payloadHash);
            if (corruption == "draft_changed") draft["proposal"]!["severity"] = "I";
            if (corruption == "draft_foreign") draft["opportunityRef"] = "foreign_opportunity";
            replacement = Encoding.UTF8.GetBytes(corruption == "draft_malformed" ? "{" : draft.ToJsonString());
        }
        else if (corruption == "initial_authority_digest")
        {
            target = PendingTurnSnapshotAuthority.AuthorityPath;
            var authority = ParseJsonObjectBytes(await File.ReadAllBytesAsync(Path.Combine(files.GameSessionPath, target)));
            var digest = authority["payloadSha256"]!.GetValue<string>();
            authority["payloadSha256"] = digest[..^1] + (digest[^1] == '0' ? '1' : '0');
            replacement = Encoding.UTF8.GetBytes(authority.ToJsonString());
        }
        else
        {
            var scene = ParseJsonObjectBytes(bytes);
            switch (corruption)
            {
                case "output_scene_prefix": scene["response"] = "Изменённая сцена. " + scene["response"]!.GetValue<string>(); break;
                case "output_timestamp": scene["timestamp"] = "2026-10-15T23:59:59Z"; break;
                case "output_normalization": scene["response"] = scene["response"]!.GetValue<string>() + "`nДобавлено."; break;
                case "output_missing": break;
                case "output_formatting": break;
                default: throw new InvalidOperationException(corruption);
            }
            replacement = corruption == "output_missing" ? null : corruption == "output_formatting"
                ? bytes.Concat(new byte[] { 0x20 }).ToArray() : Encoding.UTF8.GetBytes(scene.ToJsonString());
        }
        var path = Path.Combine(files.GameSessionPath, target);
        var before = File.Exists(path) ? await File.ReadAllBytesAsync(path) : null;
        Assert.True(replacement is null ? before is not null : before is null || !replacement.AsSpan().SequenceEqual(before));
        if (replacement is null) File.Delete(path); else await File.WriteAllBytesAsync(path, replacement);
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { Boundary = "fr063_joint_output_parent_before_launch_tamper", Observation = new
        { Corruption = corruption, Target = target, Before = before, After = replacement, ProcessId = Environment.ProcessId } }));
    }
}
