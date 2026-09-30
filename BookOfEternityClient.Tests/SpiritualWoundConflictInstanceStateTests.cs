using System.Reflection;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Exercises the pure durable spiritual ledger without minting live turn authority.
/// </summary>
public sealed class SpiritualWoundConflictInstanceStateTests
{
    private const string TypeName = "SpiritualWoundConflictInstanceState";
    private const string Path = "game_state/wounds/spiritual_wound_opportunity_receipts.json";
    private const string Fingerprint = "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    /// <summary>
    /// Requires strict empty state to survive canonical serialization without allocating history.
    /// </summary>
    [Fact]
    public void EmptyLedger_RoundTripsWithoutInventingRows()
    {
        var state = ValidState(Root());
        var serialized = Invoke("SerializeCanonical", state);
        Assert.True(JsonNode.DeepEquals(Root(), JsonNode.Parse(Assert.IsType<string>(serialized))));
    }

    /// <summary>
    /// Rejects malformed or open root shapes before any append/replay operation.
    /// </summary>
    /// <param name="json">
    /// Invalid serialized ledger.
    /// </param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{\"schemaVersion\":1,\"schemaVersion\":1}")]
    public void Parse_RejectsInvalidRoot(string? json)
    {
        Assert.False(Read<bool>(Invoke("Parse", json, Path), "IsValid"));
    }

    /// <summary>
    /// Pins closed root fields, exact integer counters and contiguous collection ordinals.
    /// </summary>
    [Fact]
    public void Parse_RejectsMissingUnknownAndNoncontiguousRootFields()
    {
        foreach (var key in Root().Select(pair => pair.Key))
        {
            var missing = Root();
            missing.Remove(key);
            Invalid(missing);
        }
        var unknown = Root();
        unknown["receipts"] = new JsonArray();
        Invalid(unknown);
        foreach (var counter in new[] { "nextInstanceOrdinal", "nextClosureOrdinal" })
        {
            var changed = Root();
            changed[counter] = 2;
            Invalid(changed);
            changed[counter] = "1";
            Invalid(changed);
        }
    }

    /// <summary>
    /// Keeps an accepted start immutable when a later terminal closure is appended.
    /// </summary>
    [Fact]
    public void AppendClosure_PreservesStartAndExactReplay()
    {
        var started = Root();
        started["instances"]!.AsArray().Add(Instance());
        started["nextInstanceOrdinal"] = 2;
        var before = ValidState(started);
        var closed = started.DeepClone().AsObject();
        closed["closures"]!.AsArray().Add(Closure(closed["instances"]![0]!["instanceId"]!.GetValue<string>()));
        closed["nextClosureOrdinal"] = 2;
        var after = ValidState(closed);
        var result = Invoke("PlanAppend", before, after);
        Assert.Equal("appended", Read<string>(result, "Disposition"));
        Assert.Equal("exact_replay", Read<string>(Invoke("PlanAppend", after, after), "Disposition"));
        Assert.True(JsonNode.DeepEquals(started, JsonNode.Parse((string)Invoke("SerializeCanonical", before))));
        Assert.Equal("conflict", Read<string>(Invoke("PlanAppend", after, before), "Disposition"));
    }

    /// <summary>
    /// Rejects a second closure, an unknown instance and closure before admission.
    /// </summary>
    [Fact]
    public void Parse_RejectsInvalidTerminalReferences()
    {
        var root = Root();
        var instance = Instance();
        root["instances"]!.AsArray().Add(instance);
        root["nextInstanceOrdinal"] = 2;
        var closure = Closure(instance["instanceId"]!.GetValue<string>());
        root["closures"]!.AsArray().Add(closure);
        root["nextClosureOrdinal"] = 2;
        _ = ValidState(root);
        var duplicate = closure.DeepClone().AsObject();
        duplicate["ordinal"] = 2;
        duplicate["terminalEventRef"] = "another-terminal-event";
        Seal(duplicate, "closure", "closureId", "closureFingerprint");
        root["closures"]!.AsArray().Add(duplicate);
        root["nextClosureOrdinal"] = 3;
        Invalid(root);
        root["closures"]!.AsArray().RemoveAt(1);
        root["nextClosureOrdinal"] = 2;
        closure["instanceId"] = instance["instanceId"]!.GetValue<string>();
        closure["terminalTurn"] = 3;
        Seal(closure, "closure", "closureId", "closureFingerprint");
        Invalid(root);
        closure["terminalTurn"] = 5;
        closure["instanceId"] = "missing-instance";
        Seal(closure, "closure", "closureId", "closureFingerprint");
        Invalid(root);
    }

    /// <summary>
    /// Checks canonical identifiers against supported Unicode and forbidden normalization aliases.
    /// </summary>
    /// <param name="identifier">
    /// Display identifier under examination.
    /// </param>
    /// <param name="valid">
    /// Whether the shared identifier contract permits this exact spelling.
    /// </param>
    [Theory]
    [InlineData("conflict-\U0001F409", true)]
    [InlineData("conflict-\uFF41", false)]
    [InlineData("conflict-\u2028a", false)]
    [InlineData("conflict-\u2029a", false)]
    public void Parse_PreservesCanonicalIdentifierContract(string identifier, bool valid)
    {
        var root = Root();
        var instance = Instance();
        instance["displayConflictId"] = identifier;
        Seal(instance, "instance", "instanceId", "instanceFingerprint");
        root["instances"]!.AsArray().Add(instance);
        root["nextInstanceOrdinal"] = 2;
        Assert.Equal(valid, Read<bool>(Invoke("Parse", root.ToJsonString(), Path), "IsValid"));
    }

    /// <summary>
    /// Requires new start evidence when a display identifier is reused after its closure.
    /// </summary>
    [Fact]
    public void Parse_RejectsReplayedStartButAllowsNewStartAfterClosure()
    {
        var root = Root();
        var original = Instance();
        root["instances"]!.AsArray().Add(original);
        var closure = Closure(original["instanceId"]!.GetValue<string>());
        closure["terminalTurn"] = 4;
        Seal(closure, "closure", "closureId", "closureFingerprint");
        root["closures"]!.AsArray().Add(closure);
        root["nextClosureOrdinal"] = 2;
        root["nextInstanceOrdinal"] = 2;
        var before = ValidState(root);
        var restarted = original.DeepClone().AsObject();
        restarted["ordinal"] = 2;
        Seal(restarted, "instance", "instanceId", "instanceFingerprint");
        root["instances"]!.AsArray().Add(restarted);
        root["nextInstanceOrdinal"] = 3;
        Invalid(root);
        restarted["baselineConflictFingerprint"] = "sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        Seal(restarted, "instance", "instanceId", "instanceFingerprint");
        Invalid(root);
        restarted["startRequestId"] = "request-b";
        restarted["startSnapshotToken"] = "snapshot-b";
        Seal(restarted, "instance", "instanceId", "instanceFingerprint");
        var after = ValidState(root);
        Assert.Equal("appended", Read<string>(Invoke("PlanAppend", before, after), "Disposition"));
        root["closures"]!.AsArray().Clear();
        root["nextClosureOrdinal"] = 1;
        Invalid(root);
    }

    /// <summary>
    /// Rejects a re-sealed rewrite of previously accepted evidence and keeps detached input state.
    /// </summary>
    [Fact]
    public void PlanAppend_RejectsChangedAcceptedStart()
    {
        var root = Root();
        var instance = Instance();
        root["instances"]!.AsArray().Add(instance);
        root["nextInstanceOrdinal"] = 2;
        var before = ValidState(root);
        var originalJson = (string)Invoke("SerializeCanonical", before);
        instance["startRequestId"] = "changed-request";
        Seal(instance, "instance", "instanceId", "instanceFingerprint");
        var changed = ValidState(root);
        var result = Invoke("PlanAppend", before, changed);
        Assert.Equal("conflict", Read<string>(result, "Disposition"));
        Assert.Null(Read<object?>(result, "After"));
        Assert.Equal(originalJson, Invoke("SerializeCanonical", before));
    }

    /// <summary>
    /// Pins the versioned digest against an independently calculated known preimage.
    /// </summary>
    [Fact]
    public void InstanceFingerprint_MatchesKnownCanonicalDigest()
    {
        Assert.Equal("sha256:9792c3239b62105d49af9de6c538e5bf164a8ae2fd9d3a2f017c10802da67a8f",
            Instance()["instanceFingerprint"]!.GetValue<string>());
    }

    /// <summary>
    /// Builds the supported empty ledger.
    /// </summary>
    /// <returns>
    /// A fresh JSON object with no accepted rows.
    /// </returns>
    private static JsonObject Root() => JsonNode.Parse("""
        {"schemaVersion":1,"nextInstanceOrdinal":1,"nextClosureOrdinal":1,"instances":[],"closures":[]}
        """)!.AsObject();

    /// <summary>
    /// Builds parser input for one accepted conflict start.
    /// </summary>
    /// <returns>
    /// Detached instance JSON; it is not live turn authority.
    /// </returns>
    private static JsonObject Instance()
    {
        var row = new JsonObject
        {
            ["instanceId"] = "", ["ordinal"] = 1, ["displayConflictId"] = "conflict-a",
            ["realm"] = "chaos_sea", ["startSessionId"] = "session-a", ["startRequestId"] = "request-a",
            ["startSnapshotToken"] = "snapshot-a", ["startTurn"] = 4,
            ["baselineConflictFingerprint"] = Fingerprint, ["instanceFingerprint"] = ""
        };
        Seal(row, "instance", "instanceId", "instanceFingerprint");
        return row;
    }

    /// <summary>
    /// Builds parser input for a terminal event on the supplied instance.
    /// </summary>
    /// <param name="instanceId">
    /// Exact parent instance ID.
    /// </param>
    /// <returns>
    /// Detached terminal closure JSON.
    /// </returns>
    private static JsonObject Closure(string instanceId)
    {
        var row = new JsonObject
        {
            ["closureId"] = "", ["ordinal"] = 1, ["instanceId"] = instanceId,
            ["terminalTurn"] = 5, ["terminalEventRef"] = "terminal-event",
            ["terminalConflictFingerprint"] = Fingerprint, ["closureFingerprint"] = ""
        };
        Seal(row, "closure", "closureId", "closureFingerprint");
        return row;
    }

    /// <summary>
    /// Supplies valid comparison fingerprints for parser fixtures without creating acceptance authority.
    /// </summary>
    /// <param name="row">
    /// Mutable fixture row.
    /// </param>
    /// <param name="kind">
    /// Fingerprint domain suffix.
    /// </param>
    /// <param name="idField">
    /// Derived identity field excluded from the digest.
    /// </param>
    /// <param name="fingerprintField">
    /// Digest field excluded from its own preimage.
    /// </param>
    private static void Seal(JsonObject row, string kind, string idField, string fingerprintField)
    {
        var fingerprint = (string)Invoke("ComputeRowFingerprint", row, kind);
        row[fingerprintField] = fingerprint;
        row[idField] = "spiritual_" + kind + "_" + fingerprint[7..];
    }

    /// <summary>
    /// Requires successful parsing and returns the detached parsed state.
    /// </summary>
    /// <param name="root">
    /// Serialized-state fixture.
    /// </param>
    /// <returns>
    /// Parsed pure state.
    /// </returns>
    private static object ValidState(JsonObject root)
    {
        var parsed = Invoke("Parse", root.ToJsonString(), Path);
        Assert.True(Read<bool>(parsed, "IsValid"), root.ToJsonString());
        return Read<object>(parsed, "State");
    }

    /// <summary>
    /// Requires malformed state to expose no accepted parsed state.
    /// </summary>
    /// <param name="root">
    /// Invalid state fixture.
    /// </param>
    private static void Invalid(JsonObject root) =>
        Assert.False(Read<bool>(Invoke("Parse", root.ToJsonString(), Path), "IsValid"));

    /// <summary>
    /// Invokes the internal pure state API without widening its production visibility.
    /// </summary>
    /// <param name="name">
    /// Static method name.
    /// </param>
    /// <param name="args">
    /// Exact method arguments, including <see langword="null"/> parser input.
    /// </param>
    /// <returns>
    /// Method result.
    /// </returns>
    private static object Invoke(string name, params object?[] args)
    {
        var type = typeof(ValidationService).Assembly.GetType("BookOfEternityClient.Services." + TypeName);
        Assert.NotNull(type);
        var method = type.GetMethod(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return method.Invoke(null, args)!;
    }

    /// <summary>
    /// Reads a public result property from the pure parser/reducer API.
    /// </summary>
    /// <typeparam name="T">
    /// Expected property type.
    /// </typeparam>
    /// <param name="value">
    /// Result object.
    /// </param>
    /// <param name="name">
    /// Exact property name.
    /// </param>
    /// <returns>
    /// Property value.
    /// </returns>
    private static T Read<T>(object value, string name) =>
        (T)value.GetType().GetProperty(name)!.GetValue(value)!;
}
