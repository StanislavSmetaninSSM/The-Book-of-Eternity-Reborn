using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>Exercises the real optional reader with independent local serializer policies.</summary>
public sealed class JointPrivateEffectSourcePolicyTests
{
    /// <summary>Inspects policy without callbacks and permits only naturally frozen builtins.</summary>
    [Fact]
    public void PolicyInspectionAndExcludedCollaboratorsStayFresh()
    {
        var resolver = new DefaultJsonTypeInfoResolver(); var options = Options(resolver);
        Assert.False(JointPrivateFingerprintData.HasReusableEffectSourcePolicy(options, resolver));
        Assert.False(options.IsReadOnly); Assert.False(resolver.Modifiers.IsReadOnly);
        var ordinary = Data();
        using (var operation = new DefaultOperationValidationService().BeginOperation())
        {
            CountParses(1, () => { Read(ordinary, "alpha", operation, options, resolver); Read(ordinary, "alpha", operation, options, resolver); });
            Assert.True(JointPrivateFingerprintData.HasReusableEffectSourcePolicy(options, resolver));
        }
        var modifiedResolver = new DefaultJsonTypeInfoResolver(); var modifiedCalls = 0;
        modifiedResolver.Modifiers.Add(_ => modifiedCalls++);
        var modified = Options(modifiedResolver);
        Assert.False(JointPrivateFingerprintData.HasReusableEffectSourcePolicy(modified, modifiedResolver));
        Assert.Equal(0, modifiedCalls); Fallback(modified, modifiedResolver); Assert.True(modifiedCalls > 0);
        var derivedResolver = new CountingResolver(); var derived = Options(derivedResolver);
        Assert.False(JointPrivateFingerprintData.HasReusableEffectSourcePolicy(derived, derivedResolver));
        Assert.Equal(0, derivedResolver.Calls); Fallback(derived, derivedResolver); Assert.True(derivedResolver.Calls > 0);
        var wrongOriginal = new DefaultJsonTypeInfoResolver();
        Assert.False(JointPrivateFingerprintData.HasReusableEffectSourcePolicy(options, wrongOriginal));
        var converterResolver = new DefaultJsonTypeInfoResolver(); var converterOptions = Options(converterResolver);
        var converter = new CountingConverter(); converterOptions.Converters.Add(converter);
        Assert.False(JointPrivateFingerprintData.HasReusableEffectSourcePolicy(converterOptions, converterResolver));
        Assert.Equal(0, converter.Reads); Fallback(converterOptions, converterResolver); Assert.Equal(2, converter.Reads);
        foreach (var excluded in new Action<JsonSerializerOptions>[]
        {
            value => value.PropertyNamingPolicy = null,
            value => value.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
            value => value.Encoder = JavaScriptEncoder.Default
        })
        {
            var localResolver = new DefaultJsonTypeInfoResolver(); var local = Options(localResolver); excluded(local);
            Fallback(local, localResolver);
        }
    }

    /// <summary>Preserves a stateful ReferenceHandler's calls and original failure after a successful read.</summary>
    [Fact]
    public void ReferenceHandlerCallbacksAndFailuresAreNeverCached()
    {
        var resolver = new DefaultJsonTypeInfoResolver(); var options = Options(resolver);
        var handler = new CountingReferenceHandler(); options.ReferenceHandler = handler;
        Assert.False(JointPrivateFingerprintData.HasReusableEffectSourcePolicy(options, resolver));
        Assert.Equal(0, handler.Calls); Assert.False(options.IsReadOnly);
        var data = Data(); using var operation = new DefaultOperationValidationService().BeginOperation();
        Read(data, "alpha", operation, options, resolver); Assert.Equal(1, handler.Calls);
        handler.Throw = true;
        var error = Assert.Throws<InvalidOperationException>(() => Read(data, "alpha", operation, options, resolver));
        Assert.Equal("reference source refused", error.Message); Assert.Equal(2, handler.Calls);
        handler.Throw = false; Read(data, "alpha", operation, options, resolver); Assert.Equal(3, handler.Calls);
        Assert.Equal(0, operation.CaptureState().Entries);
    }

    /// <summary>Keeps exact path errors outside cache requests so unrelated and handled parent DATA survives.</summary>
    [Fact]
    public void MalformedSourcePreservesUnrelatedEntryAndHandledAncestor()
    {
        var resolver = new DefaultJsonTypeInfoResolver(); var options = Options(resolver); var data = Data();
        Read(data, "alpha", null, options, resolver); // Establish policy only through the original successful source.
        using var operation = new DefaultOperationValidationService().BeginOperation();
        var unrelated = new OperationComputation<int>("T609.Unrelated", 1, value => value); var otherKey = new object();
        var parent = new OperationComputation<int>("T609.Parent", 1, value => value); var parentKey = new object();
        var unrelatedSources = 0; var parentSources = 0;
        int Other() { unrelatedSources++; return 19; }
        Assert.Equal(19, operation.GetOrCompute(unrelated, otherKey, [], Other));
        Assert.Equal(23, operation.GetOrCompute(parent, parentKey, [], () =>
        {
            parentSources++;
            foreach (var paths in new[]
            {
                new[] { "bad-json", "unknown" }, new[] { "unknown", "bad-json" },
                new[] { "duplicate" }, new[] { "bad-utf8" }, new[] { "empty" }, new[] { "array" },
                new[] { "alpha", "alpha" }, new string[] { null! }
            })
            {
                var original = Record.Exception(() => Map(data, paths, null, options, resolver));
                var actual = Record.Exception(() => Map(data, paths, operation, options, resolver));
                Assert.NotNull(original); Assert.NotNull(actual);
                Assert.Equal(original.GetType(), actual.GetType()); Assert.Equal(original.Message, actual.Message);
                Assert.Equal(19, operation.GetOrCompute(unrelated, otherKey, [], Other));
            }
            return 23;
        }));
        Assert.Equal(23, operation.GetOrCompute(parent, parentKey, [], () => { parentSources++; return 0; }));
        Assert.Equal(1, unrelatedSources); Assert.Equal(1, parentSources);
        // Only unrelated, parent and the successful alpha before its duplicate path error may be retained.
        Assert.Equal(3, operation.CaptureState().Entries);
    }

    /// <summary>Separates byte owners/paths, null hits, mutable exports, fresh scopes and closed operation refusal.</summary>
    [Fact]
    public void OwnersCopiesFreshAndClosedContoursUseActualSources()
    {
        var resolver = new DefaultJsonTypeInfoResolver(); var options = Options(resolver); var data = Data();
        var foreign = Data(99); var operation = new DefaultOperationValidationService().BeginOperation();
        try
        {
            CountParses(9, () =>
            {
                var first = Read(data, "alpha", operation, options, resolver)!;
                var second = Read(data, "alpha", operation, options, resolver)!;
                Assert.NotSame(first, second); first["value"] = 70; Assert.Equal(11, second["value"]!.GetValue<int>());
                Assert.Equal(12, Read(data, "beta", operation, options, resolver)!["value"]!.GetValue<int>());
                Assert.Equal(99, Read(foreign, "alpha", operation, options, resolver)!["value"]!.GetValue<int>());
                Assert.Null(Read(data, "json-null", operation, options, resolver));
                Assert.Null(Read(data, "json-null", operation, options, resolver));
                Assert.Null(Read(data, "absent", operation, options, resolver));
                Assert.Null(Read(data, "absent", operation, options, resolver));
                using (operation.ForceFresh())
                {
                    Read(data, "alpha", operation, options, resolver); Read(data, "alpha", operation, options, resolver);
                    Assert.Equal(0, operation.CaptureState().Entries);
                }
                Read(data, "alpha", operation, options, resolver); Read(data, "alpha", operation, options, resolver);
                Read(data, "alpha", null, options, resolver); Read(data, "alpha", null, options, resolver);
            });
        }
        finally { operation.Dispose(); }
        Assert.Equal(new ValidationOperationState(0, 0, true), operation.CaptureState());
        Assert.Throws<ObjectDisposedException>(() => Read(data, "alpha", operation, options, resolver));
        Assert.Null(Read(data, "absent", operation, options, resolver));
    }

    /// <summary>Propagates the existing operation overlap refusal without source fallback or orphaned work.</summary>
    /// <returns>Completion after both the refusal and the original pending source settle.</returns>
    [Fact]
    public async Task OverlappingOperationIsNotDisguisedAsFallback()
    {
        var resolver = new DefaultJsonTypeInfoResolver(); var options = Options(resolver); var data = Data();
        Read(data, "alpha", null, options, resolver);
        using var operation = new DefaultOperationValidationService().BeginOperation();
        var rule = new OperationComputation<int>("T609.Pending", 1, value => value);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = operation.GetOrComputeAsync(rule, new object(), [], async _ => { await release.Task; return 7; });
        Exception? error;
        try { error = Record.Exception(() => Read(data, "alpha", operation, options, resolver)); }
        finally { release.TrySetResult(); await Record.ExceptionAsync(async () => await pending); }
        Assert.IsType<InvalidOperationException>(error); Assert.Equal(0, operation.CaptureState().Entries);
    }

    private static void Fallback(JsonSerializerOptions options, DefaultJsonTypeInfoResolver resolver)
    {
        var data = Data(); using var operation = new DefaultOperationValidationService().BeginOperation();
        CountParses(2, () => { Read(data, "alpha", operation, options, resolver); Read(data, "alpha", operation, options, resolver); });
        Assert.False(JointPrivateFingerprintData.HasReusableEffectSourcePolicy(options, resolver));
        Assert.Equal(0, operation.CaptureState().Entries);
    }
    private static void CountParses(long expected, Action action)
    {
        var recorder = new OperationTimingAccumulator(); var window = recorder.StartWindow();
        try { using (FileSystemManager.EnterOperationTimingContext(null, recorder)) action(); }
        finally { window.Dispose(); }
        var snapshot = window.Capture(); Assert.True(snapshot.Complete);
        Assert.Equal(0L, snapshot.OpenScopes); Assert.Equal(0L, snapshot.LateCompletions);
        var row = Assert.Single(snapshot.Rows, value => value.Name == "JointEffectInput.PrivateSourceParse");
        Assert.Equal(expected, row.Started); Assert.Equal(expected, row.Completed);
    }
    private static JsonObject? Read(JointPrivateFingerprintData data, string path, ValidationOperation? operation,
        JsonSerializerOptions options, DefaultJsonTypeInfoResolver resolver) => data.ReadEffectSourceRoot(path, operation, options, resolver);
    private static Dictionary<string, JsonObject?> Map(JointPrivateFingerprintData data, IEnumerable<string> paths,
        ValidationOperation? operation, JsonSerializerOptions options, DefaultJsonTypeInfoResolver resolver) =>
        paths.ToDictionary(path => path, path => Read(data, path, operation, options, resolver), StringComparer.Ordinal);
    private static JsonSerializerOptions Options(DefaultJsonTypeInfoResolver resolver) => new()
    {
        WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        TypeInfoResolver = resolver
    };
    private static JointPrivateFingerprintData Data(int alpha = 11)
    {
        var input = WoundAcceptedTurnTestFixture.CreateNoMechanicsInput();
        var prefix = JointPrivateFingerprintData.Prefix.Capture(new JsonObject { ["fingerprint"] = "checkpoint" }, input, new JsonObject(), input);
        var bytes = new Dictionary<string, byte[]?>(StringComparer.Ordinal)
        {
            ["alpha"] = Encoding.UTF8.GetBytes("{\"value\":" + alpha + "}"), ["beta"] = Encoding.UTF8.GetBytes("\uFEFF\uFEFF{\"value\":12}"),
            ["json-null"] = Encoding.UTF8.GetBytes("null"), ["absent"] = null, ["bad-json"] = Encoding.UTF8.GetBytes("{broken"),
            ["duplicate"] = Encoding.UTF8.GetBytes("{\"x\":1,\"x\":2}"), ["bad-utf8"] = [0xff], ["empty"] = [], ["array"] = Encoding.UTF8.GetBytes("[]")
        };
        return JointPrivateFingerprintData.Capture(prefix, JointPrivateFingerprintData.BeforeImages.Copy(bytes));
    }
    private sealed class CountingResolver : DefaultJsonTypeInfoResolver
    {
        internal int Calls;
        public override JsonTypeInfo GetTypeInfo(Type type, JsonSerializerOptions options) { Calls++; return base.GetTypeInfo(type, options); }
    }
    private sealed class CountingConverter : JsonConverter<JsonObject>
    {
        internal int Reads;
        public override JsonObject? Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
        { Reads++; using var document = JsonDocument.ParseValue(ref reader); return JsonNode.Parse(document.RootElement.GetRawText())!.AsObject(); }
        public override void Write(Utf8JsonWriter writer, JsonObject value, JsonSerializerOptions options) => throw new NotSupportedException();
    }
    private sealed class CountingReferenceHandler : ReferenceHandler
    {
        internal int Calls; internal bool Throw;
        public override ReferenceResolver CreateResolver()
        { Calls++; if (Throw) throw new InvalidOperationException("reference source refused"); return new Resolver(); }
        private sealed class Resolver : ReferenceResolver
        {
            public override void AddReference(string referenceId, object value) => throw new NotSupportedException();
            public override string GetReference(object value, out bool alreadyExists) { alreadyExists = false; return "unused"; }
            public override object ResolveReference(string referenceId) => throw new NotSupportedException();
        }
    }
}
