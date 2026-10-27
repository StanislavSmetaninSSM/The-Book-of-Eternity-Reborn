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

/// <summary>Compares equal synthetic reader and consumer work without production reuse.</summary>
public sealed class JointPrivateEffectSourceCostTests(ITestOutputHelper output)
{
    private static readonly string[] Paths = ["alpha", "beta", "gamma", "json-null", "absent"];

    /// <summary>Measures fresh reads against a test-only existing-service cold and hit prototype.</summary>
    [Fact]
    public void EqualWorkOriginalColdAndHitCost()
    {
        // JIT and serializer warmup never populate either measured owner or operation.
        _ = Run(CreateData(), false);
        _ = Run(CreateData(), true);
        var original = Run(CreateData(), false);
        var prototype = Run(CreateData(), true);
        Assert.Equal(original.Signatures, prototype.Signatures);
        Assert.Equal(16L, original.ParseCount);
        Assert.Equal(4L, prototype.ParseCount);
        Assert.Equal(0, original.EntriesBeforeClose);
        Assert.Equal(4, prototype.EntriesBeforeClose);
        Assert.True(original.Closed && prototype.Closed);
        Assert.Equal(16, prototype.Lookups);
        Assert.Equal(8, prototype.CopyCalls[0]);
        Assert.Equal(new[] { 4, 4, 4 }, prototype.CopyCalls.Skip(1));
        Assert.Equal(6, prototype.NonNullCopies[0]);
        Assert.Equal(new[] { 3, 3, 3 }, prototype.NonNullCopies.Skip(1));
        output.WriteLine(JsonSerializer.Serialize(new
        {
            Kind = "T609EqualWorkSyntheticCost", InputBytes = InputBytes(), Paths = Paths.Length,
            Shape = "three objects with long scalar strings, one nested object, JSON null and absent",
            Reads = 4, StopwatchFrequency = Stopwatch.Frequency, Original = original.Metrics,
            PrototypeCold = prototype.Metrics[0], PrototypeHits = prototype.Metrics.Skip(1),
            OriginalLifecycle = original.Lifecycle, PrototypeLifecycle = prototype.Lifecycle,
            OriginalParses = original.ParseCount, PrototypeParses = prototype.ParseCount,
            prototype.CopyCalls, prototype.NonNullCopies, prototype.Lookups,
            prototype.EntriesBeforeClose, WholeDfrQualified = false
        }));
    }

    private static Result Run(JointPrivateFingerprintData data, bool prototype)
    {
        var runStart = Stopwatch.GetTimestamp();
        var runAllocation = GC.GetAllocatedBytesForCurrentThread();
        var setupStart = Stopwatch.GetTimestamp();
        var setupAllocation = GC.GetAllocatedBytesForCurrentThread();
        var operation = new DefaultOperationValidationService().BeginOperation();
        var setupTicks = Stopwatch.GetTimestamp() - setupStart;
        var setupBytes = GC.GetAllocatedBytesForCurrentThread() - setupAllocation;
        var recorder = new OperationTimingAccumulator();
        var window = recorder.StartWindow();
        var metrics = new List<Sample>();
        var signatures = new List<string>();
        var outputs = new List<Dictionary<string, JsonObject?>>();
        var copyCalls = new int[4]; var nonNullCopies = new int[4];
        var iteration = 0; var lookups = 0; long copyTicks = 0, keyTicks = 0, lookupTicks = 0;
        var rule = new OperationComputation<JsonObject?>("T609.CostPrototype", 1, value =>
        {
            var start = Stopwatch.GetTimestamp();
            copyCalls[iteration]++;
            if (value is not null) nonNullCopies[iteration]++;
            var detached = (JsonObject?)value?.DeepClone();
            copyTicks += Stopwatch.GetTimestamp() - start;
            return detached;
        });
        try
        {
            using (FileSystemManager.EnterOperationTimingContext(null, recorder))
            {
                for (iteration = 0; iteration < 4; iteration++)
                {
                    var copyBefore = copyTicks; var keyBefore = keyTicks; var lookupBefore = lookupTicks;
                    var allocationBefore = GC.GetAllocatedBytesForCurrentThread();
                    var start = Stopwatch.GetTimestamp();
                    var roots = Paths.ToDictionary(path => path, path =>
                    {
                        if (!prototype) return data.ReadEffectSourceRoot(path, null);
                        var bytes = data.ReadBeforeBytes(path);
                        if (bytes is null) return null;
                        var options = SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed;
                        var resolver = SharedJsonOptions.PrettyCamelCaseOriginalResolver;
                        Assert.True(JointPrivateFingerprintData.HasReusableEffectSourcePolicy(options, resolver));
                        var keyStart = Stopwatch.GetTimestamp();
                        var key = operation.GetStructuralInputIdentity(data, new OperationStructuralSnapshot([path], [options, resolver]));
                        keyTicks += Stopwatch.GetTimestamp() - keyStart;
                        var lookupStart = Stopwatch.GetTimestamp();
                        var pending = operation.TryGetAsync(rule, key, []);
                        Assert.True(pending.IsCompleted, "The existing cache-only lookup must settle synchronously.");
                        var found = pending.GetAwaiter().GetResult();
                        lookupTicks += Stopwatch.GetTimestamp() - lookupStart;
                        lookups++;
                        if (found.Found)
                        {
                            Assert.True(JointPrivateFingerprintData.HasReusableEffectSourcePolicy(options, resolver));
                            return found.Value;
                        }
                        var parsed = JointPrivateFingerprintData.ParseEffectSourceRoot(path, bytes);
                        Assert.True(JointPrivateFingerprintData.HasReusableEffectSourcePolicy(options, resolver));
                        return operation.GetOrCompute(rule, key, [], () => parsed);
                    }, StringComparer.Ordinal);
                    var signature = Consume(roots);
                    var ticks = Stopwatch.GetTimestamp() - start;
                    var allocated = GC.GetAllocatedBytesForCurrentThread() - allocationBefore;
                    metrics.Add(new Sample(ticks, allocated, keyTicks - keyBefore, lookupTicks - lookupBefore,
                        copyTicks - copyBefore));
                    outputs.Add(roots); signatures.Add(signature);
                }
            }
            Assert.All(signatures, value => Assert.Equal(signatures[0], value));
            Assert.NotSame(outputs[0]["alpha"], outputs[1]["alpha"]);
            outputs[0]["alpha"]!["payload"] = "changed";
            Assert.Equal(12000, outputs[1]["alpha"]!["payload"]!.GetValue<string>().Length);
            var entries = operation.CaptureState().Entries;
            var closeStart = Stopwatch.GetTimestamp();
            var closeAllocation = GC.GetAllocatedBytesForCurrentThread();
            operation.Dispose();
            var closeTicks = Stopwatch.GetTimestamp() - closeStart;
            var closeBytes = GC.GetAllocatedBytesForCurrentThread() - closeAllocation;
            var runTicks = Stopwatch.GetTimestamp() - runStart;
            var runBytes = GC.GetAllocatedBytesForCurrentThread() - runAllocation;
            window.Dispose();
            var snapshot = window.Capture();
            Assert.True(snapshot.Complete);
            Assert.Equal(0L, snapshot.OpenScopes); Assert.Equal(0L, snapshot.LateCompletions);
            var parse = Assert.Single(snapshot.Rows, row => row.Name == "JointEffectInput.PrivateSourceParse");
            Assert.Equal(parse.Started, parse.Completed);
            return new Result(signatures, metrics, parse.Started, entries, operation.CaptureState().Closed,
                lookups, copyCalls, nonNullCopies,
                new Lifecycle(setupTicks, setupBytes, closeTicks, closeBytes, runTicks, runBytes));
        }
        finally { operation.Dispose(); window.Dispose(); }
    }

    private static string Consume(Dictionary<string, JsonObject?> roots)
    {
        var count = 0;
        void Visit(JsonNode? node)
        {
            count++;
            if (node is JsonObject obj) foreach (var pair in obj) Visit(pair.Value);
            else if (node is JsonArray array) foreach (var child in array) Visit(child);
            else if (node is JsonValue value) _ = value.GetValueKind();
        }
        foreach (var root in roots.Values) Visit(root);
        return count + ":" + JsonSerializer.Serialize(roots);
    }

    private static Dictionary<string, byte[]?> Input() => new(StringComparer.Ordinal)
    {
        ["alpha"] = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { payload = new string('a', 12000), value = 11 })),
        ["beta"] = Encoding.UTF8.GetBytes("\uFEFF\uFEFF" + JsonSerializer.Serialize(new { payload = new string('b', 9000), nested = new { value = "é漢" } })),
        ["gamma"] = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { payload = new string('c', 10000), value = true })),
        ["json-null"] = Encoding.UTF8.GetBytes("null"), ["absent"] = null
    };
    private static int InputBytes() => Input().Values.Sum(value => value?.Length ?? 0);
    private static JointPrivateFingerprintData CreateData()
    {
        var input = WoundAcceptedTurnTestFixture.CreateNoMechanicsInput();
        var prefix = JointPrivateFingerprintData.Prefix.Capture(new JsonObject { ["fingerprint"] = "checkpoint" }, input, new JsonObject(), input);
        return JointPrivateFingerprintData.Capture(prefix, JointPrivateFingerprintData.BeforeImages.Copy(Input()));
    }
    private sealed record Sample(long WholeTicks, long WholeAllocatedBytes, long KeyTicks, long LookupTicks, long CopyTicks);
    // RunThroughClose includes fixture setup and assertions; nested timing components overlap and must not be summed.
    private sealed record Lifecycle(long SetupTicks, long SetupBytes, long CloseTicks, long CloseBytes,
        long RunThroughCloseTicks, long RunThroughCloseBytes);
    private sealed record Result(List<string> Signatures, List<Sample> Metrics, long ParseCount, int EntriesBeforeClose,
        bool Closed, int Lookups, int[] CopyCalls, int[] NonNullCopies, Lifecycle Lifecycle);
}
