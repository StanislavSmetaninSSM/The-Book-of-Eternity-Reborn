using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Protects the exact bytes and existence distinction required for durable rollback images.
/// </summary>
public sealed class SpiritualWoundStateImageTests
{
    private const string Path = "game_state/meta/afterlife_spiritual_conflict_state.json";

    /// <summary>
    /// Preserves arbitrary before-image bytes without parsing or normalizing their contents.
    /// </summary>
    [Fact]
    public void ReadImage_PreservesRawBytesAndDetachesReturnedArrays()
    {
        var bytes = new byte[] { 0xff, 0xfe, 0, 13, 10, 32 };
        var row = Image(bytes);
        var before = row.ToJsonString();
        var parsed = (CanonicalBeforeImage)Invoke("ReadImage", row, "original_before", new[] { Path });
        Assert.Equal(bytes, parsed.Bytes);
        Assert.Equal(before, row.ToJsonString());
        var callerCopy = parsed.Bytes!;
        callerCopy[0] = 0;
        Assert.Equal(bytes, parsed.Bytes);
        Assert.Equal(new CanonicalBeforeImage(true, bytes).Fingerprint, parsed.Fingerprint);
    }

    /// <summary>
    /// Keeps absent files distinct from existing empty files through a persisted image.
    /// </summary>
    [Fact]
    public void ReadImage_DistinguishesAbsentAndEmpty()
    {
        var absent = (CanonicalBeforeImage)Invoke("ReadImage", Image(null), "original_before", new[] { Path });
        var empty = (CanonicalBeforeImage)Invoke("ReadImage", Image([]), "original_before", new[] { Path });
        Assert.False(absent.Existed);
        Assert.Null(absent.Bytes);
        Assert.True(empty.Existed);
        Assert.Empty(empty.Bytes!);
        Assert.NotEqual(absent.Fingerprint, empty.Fingerprint);
    }

    /// <summary>
    /// Rejects image shape, inventory, role, existence and fingerprint inconsistencies.
    /// </summary>
    /// <param name="mutation">
    /// One independently invalid image property.
    /// </param>
    [Theory]
    [InlineData("unknown")]
    [InlineData("missing")]
    [InlineData("path")]
    [InlineData("path_case")]
    [InlineData("role")]
    [InlineData("existence")]
    [InlineData("null_bytes")]
    [InlineData("fingerprint")]
    [InlineData("boolean_string")]
    public void ReadImage_RejectsInvalidImage(string mutation)
    {
        var row = Image(new byte[] { 1, 2 });
        switch (mutation)
        {
            case "unknown": row["unknown"] = 1; break;
            case "missing": row.Remove("contentBase64"); break;
            case "path": row["path"] = "../outside.json"; break;
            case "path_case": row["path"] = Path.ToUpperInvariant(); break;
            case "role": row["role"] = "candidate_after"; break;
            case "existence": row["existed"] = false; break;
            case "null_bytes": row["contentBase64"] = null; break;
            case "fingerprint": row["contentFingerprint"] = new CanonicalBeforeImage(false, null).Fingerprint; break;
            case "boolean_string": row["existed"] = "true"; break;
        }
        var exception = Assert.Throws<TargetInvocationException>(() =>
            Invoke("ReadImage", row, "original_before", new[] { Path }));
        Assert.IsType<FormatException>(exception.InnerException);
    }

    /// <summary>
    /// Rejects base64 aliases rather than silently accepting whitespace or unused padding bits.
    /// </summary>
    /// <param name="text">
    /// Malformed or noncanonical base64 spelling.
    /// </param>
    [Theory]
    [InlineData("Zg==\n")]
    [InlineData("Zh==")]
    [InlineData("Zg")]
    [InlineData("_")]
    public void DecodeBase64_RejectsNoncanonicalEncoding(string text)
    {
        var exception = Assert.Throws<TargetInvocationException>(() => Invoke("DecodeBase64", JsonValue.Create(text)));
        Assert.IsType<FormatException>(exception.InnerException);
    }

    /// <summary>
    /// Validates JSON syntax while retaining the exact UTF-8 payload rather than reserializing it.
    /// </summary>
    [Fact]
    public void DecodeJsonObjectBytes_PreservesWhitespaceAndEscapes()
    {
        var bytes = Encoding.UTF8.GetBytes(" { \"text\" : \"\\u0430\" }\r\n");
        Assert.Equal(bytes, (byte[])Invoke("DecodeJsonObjectBytes", JsonValue.Create(Convert.ToBase64String(bytes))));
    }

    /// <summary>
    /// Validates a canonical text writer's UTF-8 preamble without changing the retained physical bytes.
    /// </summary>
    [Fact]
    public void DecodeJsonObjectBytes_PreservesSingleUtf8Preamble()
    {
        byte[] bytes = [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(" { \"text\" : \"\\u0430\" }\r\n")];
        Assert.Equal(bytes, (byte[])Invoke("DecodeJsonObjectBytes", JsonValue.Create(Convert.ToBase64String(bytes))));
    }

    /// <summary>
    /// Rejects multiple preambles, invalid UTF-8 and duplicate JSON keys despite an initial valid marker.
    /// </summary>
    /// <param name="mutation">
    /// Invalid payload shape placed after the first UTF-8 preamble.
    /// </param>
    [Theory]
    [InlineData("double")]
    [InlineData("malformed")]
    [InlineData("duplicate")]
    public void DecodeJsonObjectBytes_PreamblePreservesStrictRejections(string mutation)
    {
        byte[] body = mutation switch
        {
            "double" => [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes("{}")],
            "malformed" => [.. Encoding.UTF8.GetBytes("{\"text\":\""), 0xff, .. Encoding.UTF8.GetBytes("\"}")],
            _ => Encoding.UTF8.GetBytes("{\"a\":1,\"a\":2}")
        };
        byte[] bytes = [.. Encoding.UTF8.GetPreamble(), .. body];
        var error = Assert.Throws<TargetInvocationException>(() =>
            Invoke("DecodeJsonObjectBytes", JsonValue.Create(Convert.ToBase64String(bytes))));
        Assert.True(error.InnerException is FormatException or System.Text.Json.JsonException);
    }

    /// <summary>
    /// Rejects duplicate properties, malformed UTF-8 and nonobject JSON payloads.
    /// </summary>
    /// <param name="base64">
    /// Base64 payload with invalid strict JSON object contents.
    /// </param>
    [Theory]
    [InlineData("eyJhIjoxLCJhIjoyfQ==")]
    [InlineData("eyJ4Ijp7ImEiOjEsImEiOjJ9fQ==")]
    [InlineData("/w==")]
    [InlineData("W10=")]
    [InlineData("bnVsbA==")]
    public void DecodeJsonObjectBytes_RejectsInvalidPayload(string base64)
    {
        var exception = Assert.Throws<TargetInvocationException>(() =>
            Invoke("DecodeJsonObjectBytes", JsonValue.Create(base64)));
        Assert.True(exception.InnerException is FormatException or System.Text.Json.JsonException);
    }

    /// <summary>
    /// Wraps raw bytes using the existing canonical before-image fingerprint contract.
    /// </summary>
    /// <param name="bytes">
    /// Exact bytes, or <see langword="null"/> for an absent file.
    /// </param>
    /// <returns>
    /// Fresh closed image fixture.
    /// </returns>
    private static JsonObject Image(byte[]? bytes) => new()
    {
        ["path"] = Path, ["role"] = "original_before", ["existed"] = bytes is not null,
        ["contentBase64"] = bytes is null ? null : Convert.ToBase64String(bytes),
        ["contentFingerprint"] = new CanonicalBeforeImage(bytes is not null, bytes).Fingerprint
    };

    /// <summary>
    /// Calls a strict internal helper and fails explicitly when its implementation is absent.
    /// </summary>
    /// <param name="name">
    /// Exact static helper name.
    /// </param>
    /// <param name="arguments">
    /// Typed helper arguments.
    /// </param>
    /// <returns>
    /// Helper result; invocation failures remain visible to rejection assertions.
    /// </returns>
    private static object Invoke(string name, params object?[] arguments)
    {
        var method = typeof(SpiritualWoundStateJson).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return method.Invoke(null, arguments)!;
    }
}
