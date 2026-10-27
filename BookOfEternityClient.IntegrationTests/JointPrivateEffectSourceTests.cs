using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>Checks the actual private effect-source reader without a game or issued authority.</summary>
public sealed class JointPrivateEffectSourceTests(ITestOutputHelper output)
{
    /// <summary>Requires one strict parse per present path across two equal reads after exact original contracts close.</summary>
    [Fact]
    public void EqualReadsRequireOneActualParsePerPresentRoot()
    {
        var supplied = new Dictionary<string, byte[]?>(StringComparer.Ordinal)
        {
            ["alpha"] = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { payload = new string('a', 12000), value = 11 })),
            ["beta"] = Encoding.UTF8.GetBytes("\uFEFF\uFEFF" + JsonSerializer.Serialize(new { payload = new string('b', 9000), nested = new { value = "é漢" } })),
            ["gamma"] = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { payload = new string('c', 10000), value = true })),
            ["json-null"] = Encoding.UTF8.GetBytes("null"),
            ["absent"] = null,
            ["bad-json"] = Encoding.UTF8.GetBytes("{broken"),
            ["duplicate"] = Encoding.UTF8.GetBytes("{\"nested\":{\"x\":1,\"x\":2}}"),
            ["bad-utf8"] = [0xff],
            ["array"] = Encoding.UTF8.GetBytes("[]"),
            ["empty"] = []
        };
        var owner = JointPrivateFingerprintData.BeforeImages.Copy(supplied);
        var input = WoundAcceptedTurnTestFixture.CreateNoMechanicsInput();
        var prefix = JointPrivateFingerprintData.Prefix.Capture(new JsonObject { ["fingerprint"] = "checkpoint" }, input, new JsonObject(), input);
        var data = JointPrivateFingerprintData.Capture(prefix, owner);
        supplied["alpha"]![0] = 0;
        var paths = new[] { "alpha", "beta", "gamma", "json-null", "absent" };
        var selectedBytes = paths.Sum(path => supplied[path]?.Length ?? 0);
        var expected = ReadOriginal(data, paths);
        Assert.Equal(12000, expected["alpha"]!["payload"]!.GetValue<string>().Length);
        Assert.Equal("é漢", expected["beta"]!["nested"]!["value"]!.GetValue<string>());
        Assert.Null(expected["json-null"]); Assert.Null(expected["absent"]);
        foreach (var badPaths in new[]
        {
            new[] { "bad-json", "unknown" }, new[] { "unknown", "bad-json" },
            new[] { "duplicate" }, new[] { "bad-utf8" }, new[] { "array" }, new[] { "empty" },
            new[] { "alpha", "alpha" }, new[] { null! }
        })
        {
            var oldError = Record.Exception(() => ReadOriginal(data, badPaths));
            var newError = Record.Exception(() => ReadActual(data, badPaths, null));
            Assert.NotNull(oldError); Assert.NotNull(newError);
            Assert.Equal(oldError.GetType(), newError.GetType());
            Assert.Equal(oldError.Message, newError.Message);
        }

        var callbackRecorder = new OperationTimingAccumulator();
        var callbackWindow = callbackRecorder.StartWindow();
        var callbackPaths = new[] { "alpha", "beta" };
        var callbacks = new List<(string Name, string Boundary)>();
        Action<string, string, TimeSpan> observer = (name, boundary, _) =>
        {
            callbacks.Add((name, boundary));
            if (name == "JointEffectInput.PrivateSourceParse" && boundary == "begin") callbackPaths[1] = "unknown";
        };
        using (FileSystemManager.EnterOperationTimingContext(observer, callbackRecorder))
        {
            using (FileSystemManager.MeasureCurrentOperation("T609.ObserverPositiveControl")) { }
            var observed = ReadActual(data, callbackPaths, null);
            Assert.Equal(new[] { "alpha", "beta" }, observed.Keys);
            Assert.Equal("é漢", observed["beta"]!["nested"]!["value"]!.GetValue<string>());
        }
        callbackWindow.Dispose();
        Assert.True(callbackWindow.Capture().Complete);
        Assert.Empty(callbackWindow.Capture().Rows);
        Assert.Equal(new[] { ("T609.ObserverPositiveControl", "begin"), ("T609.ObserverPositiveControl", "end") }, callbacks);
        Assert.Equal(new[] { "alpha", "beta" }, callbackPaths);

        var recorder = new OperationTimingAccumulator();
        var window = recorder.StartWindow();
        var operation = new DefaultOperationValidationService().BeginOperation();
        Dictionary<string, JsonNode?> first, second;
        long readAllocated, cloneAllocated, readTicks, cloneTicks;
        try
        {
            Assert.Equal(new ValidationOperationState(0, 0, false), operation.CaptureState());
            using (FileSystemManager.EnterOperationTimingContext(null, recorder))
            {
                var bytesBefore = GC.GetAllocatedBytesForCurrentThread(); var start = Stopwatch.GetTimestamp();
                first = ReadActual(data, paths, operation);
                second = ReadActual(data, paths, operation);
                readTicks = Stopwatch.GetTimestamp() - start;
                readAllocated = GC.GetAllocatedBytesForCurrentThread() - bytesBefore;
            }
            Assert.Equal(paths, first.Keys); Assert.Equal(paths, second.Keys);
            foreach (var path in paths) { Assert.True(JsonNode.DeepEquals(expected[path], first[path])); Assert.True(JsonNode.DeepEquals(expected[path], second[path])); }
            Assert.NotSame(first["alpha"], second["alpha"]);
            first["alpha"]!["payload"] = "changed by caller";
            Assert.Equal(12000, second["alpha"]!["payload"]!.GetValue<string>().Length);
            var cloneBefore = GC.GetAllocatedBytesForCurrentThread(); var cloneStart = Stopwatch.GetTimestamp();
            for (var i = 0; i < 50; i++) foreach (var value in second.Values) _ = value?.DeepClone();
            cloneTicks = Stopwatch.GetTimestamp() - cloneStart;
            cloneAllocated = GC.GetAllocatedBytesForCurrentThread() - cloneBefore;
            Assert.False(operation.CaptureState().Closed);
        }
        finally { operation.Dispose(); window.Dispose(); }
        Assert.True(operation.CaptureState().Closed);
        var snapshot = window.Capture(); Assert.True(snapshot.Complete);
        Assert.Equal(0L, snapshot.OpenScopes); Assert.Equal(0L, snapshot.LateCompletions);
        var parse = Assert.Single(snapshot.Rows, x => x.Name == "JointEffectInput.PrivateSourceParse");
        Assert.Equal(parse.Started, parse.Completed);
        output.WriteLine(JsonSerializer.Serialize(new { Kind = "T609OriginalReaderCost", InputBytes = selectedBytes,
            SelectedPaths = paths.Length, PresentRoots = 4, Calls = 2, ParseStarted = parse.Started,
            ParseMilliseconds = parse.InclusiveMilliseconds, ReadTicks = readTicks, ReadAllocated = readAllocated,
            CloneIterations = 50, CloneTicks = cloneTicks, CloneAllocated = cloneAllocated,
            StopwatchFrequency = Stopwatch.Frequency, CacheQualification = false }));
        // Sole intended RED: the original reader parses four present roots twice; future eligible DATA needs four sources.
        Assert.Equal(4L, parse.Started);
    }

    private static Dictionary<string, JsonNode?> ReadActual(JointPrivateFingerprintData data, IEnumerable<string> paths,
        ValidationOperation? operation) => paths.ToDictionary(path => path,
            path => (JsonNode?)data.ReadEffectSourceRoot(path, operation), StringComparer.Ordinal);

    private static Dictionary<string, JsonNode?> ReadOriginal(JointPrivateFingerprintData data, IEnumerable<string> paths) =>
        paths.ToDictionary(path => path, path => data.ReadBeforeBytes(path) is not { } bytes ? null :
            (JsonNode?)StrictJsonAuthority.Deserialize<JsonObject>(new UTF8Encoding(false, true).GetString(bytes).TrimStart('\uFEFF'),
                SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed, path), StringComparer.Ordinal);
}
