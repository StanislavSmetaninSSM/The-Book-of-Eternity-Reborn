using System.Reflection;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Checks durable turn context without supplying live snapshot or publication authority.
/// </summary>
public sealed class SpiritualWoundTurnEvidenceTests
{
    /// <summary>
    /// Accepts an exchange's retained claims independently of claims for later exchanges.
    /// </summary>
    [Fact]
    public void Validate_AcceptsDetachedExchangeContextWithoutMutation()
    {
        var evidence = Evidence();
        var before = evidence.ToJsonString();
        Assert.True(IsValid(evidence));
        Assert.Equal(before, evidence.ToJsonString());
    }

    /// <summary>
    /// Rejects incomplete and open shapes at each retained context boundary.
    /// </summary>
    [Fact]
    public void Validate_RejectsMissingAndUnknownFields()
    {
        foreach (var path in new[] { "", "bounds", "diceClaims/0" })
        {
            var template = ObjectAt(Evidence(), path);
            foreach (var field in template.Select(pair => pair.Key).Append("unknown"))
            {
                var evidence = Evidence();
                var node = ObjectAt(evidence, path);
                if (field == "unknown") node[field] = 1;
                else node.Remove(field);
                Assert.False(IsValid(evidence));
            }
        }
    }

    /// <summary>
    /// Rejects changed pool values, repeated spending, foreign exchanges, reordered claims and stale digests.
    /// </summary>
    /// <param name="mutation">
    /// Independent inconsistency introduced into otherwise valid retained evidence.
    /// </param>
    [Theory]
    [InlineData("pool")]
    [InlineData("duplicate")]
    [InlineData("foreign_exchange")]
    [InlineData("reordered")]
    [InlineData("fingerprint")]
    [InlineData("out_of_pool")]
    public void Validate_RejectsInconsistentDice(string mutation)
    {
        var evidence = Evidence();
        var claims = evidence["diceClaims"]!.AsArray();
        switch (mutation)
        {
            case "pool": evidence["acceptedD20Values"]![0] = 11; break;
            case "duplicate":
                evidence["acceptedD20Values"]!.AsArray().Add(12);
                evidence["bounds"]!["diceCount"] = 3;
                claims.Add(claims[0]!.DeepClone());
                break;
            case "foreign_exchange":
                claims[0]!["exchangeOrdinal"] = 1;
                Seal(claims[0]!.AsObject());
                break;
            case "reordered":
                evidence["diceClaims"] = new JsonArray(claims[1]!.DeepClone(), claims[0]!.DeepClone());
                break;
            case "fingerprint": claims[0]!["claimFingerprint"] = Fingerprint; break;
            case "out_of_pool":
                claims[1]!["sourceIndex"] = 2;
                Seal(claims[1]!.AsObject());
                break;
        }
        Assert.False(IsValid(evidence));
    }

    /// <summary>
    /// Enforces original count relationships and checked arithmetic without a smaller gameplay cap.
    /// </summary>
    /// <param name="exchanges">
    /// Declared exchange count.
    /// </param>
    /// <param name="slots">
    /// Declared source slot count.
    /// </param>
    /// <param name="valid">
    /// Whether counts can describe this retained exchange.
    /// </param>
    [Theory]
    [InlineData(40, 80, true)]
    [InlineData(0, 0, false)]
    [InlineData(2, 3, false)]
    [InlineData(1073741824, 0, false)]
    [InlineData(-1, 0, false)]
    public void Validate_EnforcesDerivedBounds(int exchanges, int slots, bool valid)
    {
        var evidence = Evidence();
        evidence["bounds"]!["exchangeCount"] = exchanges;
        evidence["bounds"]!["sourceSlotCount"] = slots;
        Assert.Equal(valid, IsValid(evidence));
    }

    /// <summary>
    /// Rejects reuse across exchanges even when claims are individually valid and correctly ordered.
    /// </summary>
    [Fact]
    public void ValidateClaims_RejectsTurnWideReuseAcrossExchanges()
    {
        var evidence = Evidence();
        var pool = evidence["acceptedD20Values"]!.AsArray();
        var first = evidence["diceClaims"]![0]!.DeepClone().AsObject();
        var second = first.DeepClone().AsObject();
        second["exchangeOrdinal"] = 1;
        Seal(second);
        var claims = new JsonArray(first, second);
        Assert.Throws<FormatException>(() => SpiritualWoundTurnEvidence.ValidateClaims(claims, pool, 2, null));
        second["sourceIndex"] = 1;
        second["value"] = 11;
        Seal(second);
        SpiritualWoundTurnEvidence.ValidateClaims(claims, pool, 2, null);
    }

    private const string Fingerprint = "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    /// <summary>
    /// Constructs a strict context fixture for exchange zero and two selected dice.
    /// </summary>
    /// <returns>
    /// Mutable comparison data with no live authority.
    /// </returns>
    private static JsonObject Evidence()
    {
        var claims = new JsonArray();
        for (var index = 0; index < 2; index++)
        {
            var claim = new JsonObject
            {
                ["sourceIndex"] = index, ["value"] = 10 + index,
                ["exchangeOrdinal"] = 0, ["claimFingerprint"] = ""
            };
            Seal(claim);
            claims.Add(claim);
        }
        return new JsonObject
        {
            ["sessionId"] = "session-a", ["requestId"] = "request-a", ["snapshotToken"] = "snapshot-a",
            ["turn"] = 4, ["realm"] = "chaos_sea", ["originalSnapshotFingerprint"] = Fingerprint,
            ["bounds"] = new JsonObject
            {
                ["exchangeCount"] = 2, ["sourceSlotCount"] = 4, ["diceCount"] = 2, ["imagePathCount"] = 40
            },
            ["acceptedD20Values"] = new JsonArray(10, 11), ["diceClaims"] = claims
        };
    }

    /// <summary>
    /// Assigns a comparison digest to a synthetic claim fixture.
    /// </summary>
    /// <param name="claim">
    /// Mutable claim object.
    /// </param>
    private static void Seal(JsonObject claim) =>
        claim["claimFingerprint"] = SpiritualWoundStateJson.Hash(claim, "die_claim", "claimFingerprint");

    /// <summary>
    /// Selects one closed object for missing-field mutations.
    /// </summary>
    /// <param name="root">
    /// Fixture context.
    /// </param>
    /// <param name="path">
    /// Empty root path, bounds or diceClaims/0.
    /// </param>
    /// <returns>
    /// Object owned by the fixture.
    /// </returns>
    private static JsonObject ObjectAt(JsonObject root, string path) => path switch
    {
        "bounds" => root["bounds"]!.AsObject(),
        "diceClaims/0" => root["diceClaims"]![0]!.AsObject(),
        _ => root
    };

    /// <summary>
    /// Invokes the strict internal validator and distinguishes invalid evidence from missing implementation.
    /// </summary>
    /// <param name="evidence">
    /// Complete context or one deliberately malformed fixture.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when every retained context invariant is accepted;
    /// <see langword="false"/> when evidence violates the format or arithmetic bounds.
    /// </returns>
    private static bool IsValid(JsonObject evidence)
    {
        var type = typeof(ValidationService).Assembly.GetType("BookOfEternityClient.Services.SpiritualWoundTurnEvidence");
        Assert.NotNull(type);
        var method = type.GetMethod("Validate", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        try
        {
            method.Invoke(null, new object[] { evidence, 0 });
            return true;
        }
        catch (TargetInvocationException error) when (error.InnerException is FormatException or OverflowException)
        {
            return false;
        }
    }
}
