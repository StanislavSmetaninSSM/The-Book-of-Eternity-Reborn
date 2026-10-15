using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

public sealed class MortalJointAcceptedOutputBasisTests(ITestOutputHelper output)
{
    private static byte[] Scene(string response = "Первая\nВторая") => Encoding.UTF8.GetBytes(
        new JsonObject { ["response"] = response, ["timestamp"] = "2026-10-15T06:00:00Z" }.ToJsonString());

    [Fact]
    public void ExactImageRetainsFormattingTimestampBomAndDetachedBytes()
    {
        var bytes = Encoding.UTF8.GetPreamble().Concat(Scene()).ToArray();
        var expected = bytes.ToArray();
        var basis = MortalJointAcceptedOutputBasis.Capture("local_impact", "wound_1", "transition_1", bytes);
        bytes[^1] = 0;
        var roundtrip = MortalJointAcceptedOutputBasis.Read(basis.ToJson());
        Assert.Equal(expected, roundtrip.NarrativeBytes);
        Assert.Equal(basis.Fingerprint, roundtrip.Fingerprint);
        Assert.Equal("local_impact", roundtrip.LocalWoundRef);
        Assert.Equal("local impact", MortalJointAcceptedOutputBasis.Read(
            MortalJointAcceptedOutputBasis.Capture("local impact", "wound_1", "transition_1", expected).ToJson()).LocalWoundRef);
        var detached = roundtrip.NarrativeBytes; detached[^1] = 0;
        Assert.Equal(expected, roundtrip.NarrativeBytes);
        Assert.NotEqual(basis.Fingerprint, MortalJointAcceptedOutputBasis.Capture(
            "local_impact", "wound_1", "transition_1", Scene()).Fingerprint);
    }

    [Fact]
    public async Task JointReadOnlyNarrativeValidationRefusesNormalizationWithoutWriting()
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-joint-readonly-" + Guid.NewGuid().ToString("N"));
        try
        {
            var files = new BookOfEternityClient.Core.FileSystemManager(root,
                Microsoft.Extensions.Logging.Abstractions.NullLogger<BookOfEternityClient.Core.FileSystemManager>.Instance);
            files.EnsureDirectoryStructure();
            await files.WriteFileAtomicAsync("output/narrative_response.json", Encoding.UTF8.GetString(Scene("Первая`nВторая")));
            var before = await files.ReadFileBytesAsync("output/narrative_response.json");
            var validator = new ValidationService(files, Microsoft.Extensions.Logging.Abstractions.NullLogger<ValidationService>.Instance);
            var issues = await validator.ValidateMortalJointAcceptedNarrativeReadOnlyAsync();
            Assert.Contains(issues, issue => issue.Code == "mortal_joint_sealed_narrative_requires_normalization");
            Assert.Equal(before, await files.ReadFileBytesAsync("output/narrative_response.json"));
            await validator.ValidateAcceptedTurnNarrativePayloadAsync();
            Assert.Equal("Первая\nВторая", JsonNode.Parse((await files.ReadFileAsync("output/narrative_response.json"))!)!["response"]!.GetValue<string>());
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            output.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { Boundary = "joint_readonly_owned_cleanup", Root = root, Removed = !Directory.Exists(root) }));
        }
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("extra")]
    [InlineData("version")]
    [InlineData("version_type")]
    [InlineData("local_empty")]
    [InlineData("local_invalid")]
    [InlineData("wound_empty")]
    [InlineData("transition_empty")]
    [InlineData("base64")]
    [InlineData("base64_space")]
    [InlineData("digest")]
    [InlineData("digest_lowercase")]
    [InlineData("invalid_utf8")]
    [InlineData("duplicate_json")]
    [InlineData("timestamp")]
    [InlineData("unknown_scene_field")]
    [InlineData("normalization")]
    public void ClosedBasisRejectsMalformedOrUnnormalizedImages(string corruption)
    {
        var root = MortalJointAcceptedOutputBasis.Capture("local_impact", "wound_1", "transition_1", Scene()).ToJson();
        switch (corruption)
        {
            case "missing": root.Remove("narrativeHash"); break;
            case "extra": root["foreign"] = true; break;
            case "version": root["version"] = 2; break;
            case "version_type": root["version"] = "1"; break;
            case "local_empty": root["localWoundRef"] = ""; break;
            case "local_invalid": root["localWoundRef"] = " local_impact"; break;
            case "wound_empty": root["woundId"] = ""; break;
            case "transition_empty": root["transitionId"] = ""; break;
            case "base64": root["narrativeBase64"] = "!"; break;
            case "base64_space": root["narrativeBase64"] = root["narrativeBase64"]!.GetValue<string>() + " "; break;
            case "digest": root["narrativeHash"] = new string('0', 64); break;
            case "digest_lowercase": root["narrativeHash"] = root["narrativeHash"]!.GetValue<string>().ToLowerInvariant(); break;
            default:
                var bytes = corruption switch
                {
                    "invalid_utf8" => new byte[] { 0xff },
                    "duplicate_json" => Encoding.UTF8.GetBytes("{\"response\":\"first\",\"response\":\"second\",\"timestamp\":\"2026-10-15T06:00:00Z\"}"),
                    "timestamp" => Encoding.UTF8.GetBytes("{\"response\":\"scene\",\"timestamp\":false}"),
                    "unknown_scene_field" => Encoding.UTF8.GetBytes("{\"response\":\"scene\",\"timestamp\":\"2026-10-15T06:00:00Z\",\"foreign\":true}"),
                    "normalization" => Scene("Первая`nВторая"),
                    _ => throw new InvalidOperationException(corruption)
                };
                root["narrativeBase64"] = Convert.ToBase64String(bytes);
                root["narrativeHash"] = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));
                break;
        }
        Assert.Throws<InvalidDataException>(() => MortalJointAcceptedOutputBasis.Read(root));
    }
}
