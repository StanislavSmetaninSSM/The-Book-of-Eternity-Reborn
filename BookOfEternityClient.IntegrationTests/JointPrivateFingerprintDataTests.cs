using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Checks immutable fingerprint data without joint issuance, game setup or canonical writes.
/// </summary>
public sealed class JointPrivateFingerprintDataTests
{
    /// <summary>
    /// Matches the original field sequence and digest for absent, empty, binary, BOM and non-ASCII bytes.
    /// </summary>
    [Fact]
    public void CapturedDataMatchesOriginalFieldsAndOrdinalByteHashes()
    {
        var acquisition = WoundAcceptedTurnTestFixture.CreateNoMechanicsInput();
        var treatment = acquisition with { Binding = acquisition.Binding with { RequestId = "treatment-data" } };
        var checkpoint = new JsonObject { ["fingerprint"] = "checkpoint-é" };
        var command = new JsonObject { ["message"] = "é漢字\n\"\\", ["optional"] = null };
        var before = new Dictionary<string, byte[]?>(StringComparer.Ordinal)
        {
            ["z-null"] = null,
            ["a-empty"] = [],
            ["m-binary"] = [0, 255, 13, 10],
            ["b-bom"] = [239, 187, 191, 123, 125],
            ["é-text"] = Encoding.UTF8.GetBytes("漢字é")
        };
        var bytes = JointPrivateFingerprintData.BeforeImages.Copy(before);
        var prefix = JointPrivateFingerprintData.Prefix.Capture(checkpoint, acquisition, command, treatment);
        var data = JointPrivateFingerprintData.Capture(prefix, bytes);
        var expected = OriginalFields(checkpoint, acquisition, command, treatment, before, "live-1");
        var actual = CapturedFields(data, "live-1");

        Assert.Equal(expected, actual);
        Assert.Equal(WoundAcceptedTurnFingerprintWriter.Compute(expected),
            WoundAcceptedTurnFingerprintWriter.Compute(actual));
        Assert.Equal(before.Keys.ToArray(), data.ReadBeforePaths());
        Assert.Equal(before.Keys.OrderBy(path => path, StringComparer.Ordinal).ToArray(),
            actual.Skip(13).Where((_, index) => index % 2 == 0).Select(path => path!).ToArray());
        Assert.Null(actual[actual.IndexOf("z-null") + 1]);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(Array.Empty<byte>())), actual[actual.IndexOf("a-empty") + 1]);
        foreach (var (path, value) in before)
        {
            Assert.Equal(value, data.ReadBeforeBytes(path));
            Assert.True(data.MatchesBefore(path, value));
            Assert.False(data.MatchesBefore(path, value is null ? Array.Empty<byte>() : null));
        }
    }

    /// <summary>
    /// Isolates supplied and returned aliases while preserving a single exact command serialization.
    /// </summary>
    [Fact]
    public void SuppliedAndReturnedAliasesCannotChangeCapturedData()
    {
        var identity = new JsonObject { ["private-data"] = "initial" };
        var acquisition = WoundAcceptedTurnTestFixture.CreateNoMechanicsInput() with { PreTurnIdentityIndex = identity };
        var treatment = WoundAcceptedTurnTestFixture.CreateNoMechanicsInput();
        var checkpoint = new JsonObject { ["fingerprint"] = "checkpoint" };
        var command = new JsonObject(new JsonNodeOptions { PropertyNameCaseInsensitive = true })
        {
            ["Message"] = "initial",
            ["nested"] = new JsonObject { ["value"] = 7 }
        };
        byte[] supplied = [1, 2, 3];
        var before = new Dictionary<string, byte[]?> { ["original"] = supplied, ["absent"] = null };
        var owner = JointPrivateFingerprintData.BeforeImages.Copy(before);
        var prefix = JointPrivateFingerprintData.Prefix.Capture(checkpoint, acquisition, command, treatment);
        var expected = OriginalFields(checkpoint, acquisition, command, treatment, before, "live");
        supplied[0] = 99;
        before["original"] = [88];
        before["later"] = [77];
        var data = JointPrivateFingerprintData.Capture(prefix, owner);
        checkpoint["fingerprint"] = "changed";
        identity["private-data"] = "changed";
        acquisition.PreTurnIdentityIndex["private-data"] = "returned-change";
        command["Message"] = "changed";
        command["nested"]!["value"] = 999;
        data.ReadBeforeBytes("original")![1] = 66;
        var paths = data.ReadBeforePaths();
        paths[0] = "changed";
        var first = data.ReadCommandRoot();
        first["message"] = "returned-change";
        first["nested"]!["value"] = 1000;
        var second = data.ReadCommandRoot();

        Assert.Equal(expected, CapturedFields(data, "live"));
        Assert.NotSame(first, second);
        Assert.Equal("initial", second["message"]!.GetValue<string>());
        Assert.Equal(7, second["nested"]!["value"]!.GetValue<int>());
        Assert.Equal(expected[4], second.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        Assert.Equal(new byte[] { 1, 2, 3 }, data.ReadBeforeBytes("original"));
        Assert.Equal(new[] { "original", "absent" }, data.ReadBeforePaths());
        Assert.True(data.MatchesBefore("original", [1, 2, 3]));
        Assert.False(data.MatchesBefore("original", supplied));
        Assert.Throws<KeyNotFoundException>(() => data.ReadBeforeBytes("later"));
    }

    /// <summary>
    /// Observes one real hashing capture and fresh final digests with changing live scalar fields.
    /// </summary>
    [Fact]
    public void CaptureHashesOnceWhileEveryFinalDigestUsesCurrentScalarFields()
    {
        var rows = new List<(string Name, string Boundary)>();
        var files = new FileSystemManager(TestRepoPaths.RepoRoot, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
            {
                OperationTimingObserver = (name, boundary, _) => rows.Add((name, boundary))
            });
        var input = WoundAcceptedTurnTestFixture.CreateNoMechanicsInput();
        var before = JointPrivateFingerprintData.BeforeImages.Copy(
            new Dictionary<string, byte[]?> { ["bytes"] = [239, 187, 191, 195, 169] });
        using (files.EnterOperationTimingContext())
        {
            var prefix = JointPrivateFingerprintData.Prefix.Capture(
                new JsonObject { ["fingerprint"] = "checkpoint" }, input, new JsonObject(), input);
            var data = JointPrivateFingerprintData.Capture(prefix, before);
            var first = CapturedFields(data, "live-first");
            var second = CapturedFields(data, "live-second");
            _ = data.ReadBeforeBytes("bytes");
            _ = data.ReadCommandRoot();
            Assert.NotEqual(WoundAcceptedTurnFingerprintWriter.Compute(first),
                WoundAcceptedTurnFingerprintWriter.Compute(second));
            second[2] = "caller-list-change";
            Assert.Equal(first, CapturedFields(data, "live-first"));
        }
        Assert.Equal(new[]
        {
            ("JointContinuation.HashBeforeImages", "begin"),
            ("JointContinuation.HashBeforeImages", "end")
        }, rows.Where(row => row.Name == "JointContinuation.HashBeforeImages").ToArray());
    }

    /// <summary>
    /// Reproduces the original field expression independently from the immutable data representation.
    /// </summary>
    /// <param name="checkpoint">
    /// The original checkpoint projection.
    /// </param>
    /// <param name="acquisition">
    /// The original acquisition input.
    /// </param>
    /// <param name="command">
    /// The original command JSON to serialize with the normal options.
    /// </param>
    /// <param name="treatment">
    /// The original treatment input.
    /// </param>
    /// <param name="before">
    /// The original nullable byte values to hash in ordinal path order.
    /// </param>
    /// <param name="live">
    /// A scalar field evaluated between private prefix fields and byte hashing.
    /// </param>
    /// <returns>
    /// The old ordered fields, including null markers and uppercase byte hashes.
    /// </returns>
    private static List<string?> OriginalFields(JsonObject checkpoint, WoundAcceptedTurnInput acquisition,
        JsonObject command, WoundAcceptedTurnInput treatment, IReadOnlyDictionary<string, byte[]?> before, string live)
    {
        var fields = new List<string?>
        {
            "book_of_eternity.wound.joint_repair.preparation.v1", "generation", checkpoint["fingerprint"]!.GetValue<string>(),
            WoundAcceptedTurnFingerprints.ComputeInput(acquisition),
            command.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed),
            WoundAcceptedTurnFingerprints.ComputeInput(treatment),
            live, "request", "result", "resolution-authority", "finalization", "outcome", null
        };
        foreach (var (path, bytes) in before.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            fields.Add(path);
            fields.Add(bytes is null ? null : Convert.ToHexString(SHA256.HashData(bytes)));
        }
        return fields;
    }

    /// <summary>
    /// Reassembles the digest fields without memoizing any caller-provided scalar.
    /// </summary>
    /// <param name="data">
    /// The immutable private data under test.
    /// </param>
    /// <param name="live">
    /// The current scalar value to include after the private prefix.
    /// </param>
    /// <returns>
    /// A new mutable caller-owned field list.
    /// </returns>
    private static List<string?> CapturedFields(JointPrivateFingerprintData data, string live)
    {
        var fields = new List<string?> { "book_of_eternity.wound.joint_repair.preparation.v1", "generation" };
        data.AppendPrefix(fields);
        fields.AddRange(new string?[]
        {
            live, "request", "result", "resolution-authority", "finalization", "outcome", null
        });
        data.AppendBeforeFields(fields);
        return fields;
    }
}
