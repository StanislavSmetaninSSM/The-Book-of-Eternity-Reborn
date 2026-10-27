using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

/// <summary>
/// Owns immutable joint fingerprint data and its private before-images; it issues no authority.
/// </summary>
internal sealed class JointPrivateFingerprintData
{
    private readonly Prefix _prefix;
    private readonly BeforeImages _before;
    private readonly string?[] _orderedBeforeFields;
    private static readonly OperationComputation<JsonObject?> EffectSource = new(
        "JointPrivate.EffectSource", 1, value => (JsonObject?)value?.DeepClone());

    /// <summary>
    /// Retains only privately owned data produced by the capture factories.
    /// </summary>
    /// <param name="prefix">
    /// The already computed immutable input and command fields.
    /// </param>
    /// <param name="before">
    /// The immutable byte owner whose insertion order is retained for current-byte checks.
    /// </param>
    /// <param name="orderedBeforeFields">
    /// Freshly derived ordinal path and nullable byte-hash fields, owned exclusively by this instance.
    /// </param>
    private JointPrivateFingerprintData(Prefix prefix, BeforeImages before, string?[] orderedBeforeFields)
    {
        _prefix = prefix;
        _before = before;
        _orderedBeforeFields = orderedBeforeFields;
    }

    /// <summary>
    /// Computes the ordered byte hashes once after the caller has evaluated its live scalar fields.
    /// </summary>
    /// <param name="prefix">
    /// The immutable fields computed before the caller's live scalar reads.
    /// </param>
    /// <param name="before">
    /// The already copied private bytes; absent and empty values remain distinct.
    /// </param>
    /// <returns>
    /// Immutable data containing the actual byte owner and hashes derived from those same bytes.
    /// </returns>
    internal static JointPrivateFingerprintData Capture(Prefix prefix, BeforeImages before) =>
        new(prefix, before, before.ComputeOrderedFields());

    /// <summary>
    /// Appends the captured checkpoint, acquisition, command and treatment fields in their original order.
    /// </summary>
    /// <param name="fields">
    /// The caller-owned destination; no reference to it is retained.
    /// </param>
    internal void AppendPrefix(List<string?> fields) => _prefix.AppendTo(fields);

    /// <summary>
    /// Appends ordinal paths and nullable uppercase SHA-256 fields without hashing bytes again.
    /// </summary>
    /// <param name="fields">
    /// The caller-owned destination; no reference to it is retained.
    /// </param>
    internal void AppendBeforeFields(List<string?> fields) => fields.AddRange(_orderedBeforeFields);

    /// <summary>
    /// Returns a newly parsed command view from the exact captured serialization.
    /// </summary>
    /// <returns>
    /// A mutable command object detached from this instance and every other returned view.
    /// </returns>
    internal JsonObject ReadCommandRoot() => _prefix.ReadCommandRoot();

    /// <summary>
    /// Returns the before-image paths in their original insertion order.
    /// </summary>
    /// <returns>
    /// A detached path array, including paths whose captured bytes were absent.
    /// </returns>
    internal string[] ReadBeforePaths() => _before.ReadPaths();

    /// <summary>
    /// Reads one private before-image without exposing its storage.
    /// </summary>
    /// <param name="path">
    /// An existing captured path; an unknown path retains dictionary lookup failure behavior.
    /// </param>
    /// <returns>
    /// Fresh bytes, or <see langword="null"/> for a captured absent file.
    /// </returns>
    internal byte[]? ReadBeforeBytes(string path) => _before.ReadBytes(path);

    /// <summary>Reads the original detached effect-source root from the private before-image.</summary>
    /// <param name="path">The exact current source path, including its original lookup behavior.</param>
    /// <param name="operation">An existing serial DATA operation, or null for original parsing.</param>
    /// <returns>The original strict parsed object or the original absent/null result.</returns>
    internal JsonObject? ReadEffectSourceRoot(string path, ValidationOperation? operation = null)
    {
        var bytes = ReadBeforeBytes(path);
        if (bytes is null) return null;
        return ReadEffectSourceCore(path, bytes, operation, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed,
            SharedJsonOptions.PrettyCamelCaseOriginalResolver);
    }

    /// <summary>Uses the same reader with an explicit independently owned serializer policy.</summary>
    /// <param name="path">The original captured path, looked up before policy or operation inspection.</param>
    /// <param name="operation">The optional existing serial DATA owner.</param>
    /// <param name="options">The actual options used by the original strict parser.</param>
    /// <param name="originalResolver">The originally selected builtin resolver identity.</param>
    /// <returns>A detached root or the original absent/null result.</returns>
    internal JsonObject? ReadEffectSourceRoot(string path, ValidationOperation? operation,
        JsonSerializerOptions options, DefaultJsonTypeInfoResolver originalResolver)
    {
        var bytes = ReadBeforeBytes(path);
        return bytes is null ? null : ReadEffectSourceCore(path, bytes, operation, options, originalResolver);
    }

    /// <summary>Recognizes only a naturally frozen builtin policy without invoking any collaborator.</summary>
    /// <param name="options">The actual strict-parser options.</param>
    /// <param name="originalResolver">The original builtin resolver reference.</param>
    /// <returns>True only when the exact policy is immutable and contains no admitted custom collaborators.</returns>
    internal static bool HasReusableEffectSourcePolicy(JsonSerializerOptions options,
        DefaultJsonTypeInfoResolver originalResolver) => options.IsReadOnly &&
        ReferenceEquals(options.TypeInfoResolver, originalResolver) &&
        originalResolver.GetType() == typeof(DefaultJsonTypeInfoResolver) &&
        originalResolver.Modifiers.IsReadOnly && originalResolver.Modifiers.Count == 0 &&
        options.Converters.Count == 0 && options.ReferenceHandler is null &&
        ReferenceEquals(options.PropertyNamingPolicy, JsonNamingPolicy.CamelCase) &&
        options.DictionaryKeyPolicy is null && ReferenceEquals(options.Encoder, JavaScriptEncoder.UnsafeRelaxedJsonEscaping);

    private JsonObject? ReadEffectSourceCore(string path, byte[] bytes, ValidationOperation? operation,
        JsonSerializerOptions options, DefaultJsonTypeInfoResolver originalResolver)
    {
        if (operation is null) return ParseEffectSourceRoot(path, bytes, options);
        object? key = null;
        object Key() => operation.GetStructuralInputIdentity(_before,
            new OperationStructuralSnapshot([path], [options, originalResolver]));
        if (HasReusableEffectSourcePolicy(options, originalResolver))
        {
            key = Key();
            var pending = operation.TryGetAsync(EffectSource, key, []);
            if (!pending.IsCompleted)
                throw new InvalidOperationException("A cache-only effect-source lookup did not settle synchronously.");
            var found = pending.GetAwaiter().GetResult();
            if (found.Found && HasReusableEffectSourcePolicy(options, originalResolver)) return found.Value;
        }
        // Source errors must not invalidate unrelated DATA or poison an active handled ancestor.
        var parsed = ParseEffectSourceRoot(path, bytes, options);
        if (!HasReusableEffectSourcePolicy(options, originalResolver)) return parsed;
        key ??= Key();
        return operation.GetOrCompute(EffectSource, key, [], () => parsed);
    }

    /// <summary>Applies the original strict effect-source parser to already detached bytes.</summary>
    /// <param name="path">The original path used for strict diagnostics.</param>
    /// <param name="bytes">The bytes already obtained by the caller's original lookup.</param>
    /// <param name="options">Explicit actual options, or null to use the original shared parser policy.</param>
    /// <returns>The strictly parsed object or JSON null.</returns>
    internal static JsonObject? ParseEffectSourceRoot(string path, byte[] bytes, JsonSerializerOptions? options = null)
    {
        using var timing = FileSystemManager.MeasureCurrentPassiveOperation("JointEffectInput.PrivateSourceParse");
        return StrictJsonAuthority.Deserialize<JsonObject>(
            new UTF8Encoding(false, true).GetString(bytes).TrimStart('\uFEFF'),
            options ?? SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed, path);
    }

    /// <summary>
    /// Compares current bytes against the retained private bytes without replacing a current-file read.
    /// </summary>
    /// <param name="path">
    /// An existing captured path.
    /// </param>
    /// <param name="actual">
    /// The freshly read bytes, or <see langword="null"/> for an absent current file.
    /// </param>
    /// <returns>
    /// <see langword="true"/> for exact byte equality or two absent files; otherwise, <see langword="false"/>.
    /// </returns>
    internal bool MatchesBefore(string path, byte[]? actual) => _before.Matches(path, actual);

    /// <summary>
    /// Stores the immutable input fields and the single serialized source for command views.
    /// </summary>
    internal sealed class Prefix
    {
        private readonly string _checkpoint;
        private readonly string _acquisition;
        private readonly string _command;
        private readonly string _treatment;
        private readonly JsonNodeOptions? _commandOptions;

        /// <summary>
        /// Captures each private field in the original left-to-right evaluation order.
        /// </summary>
        /// <param name="checkpoint">
        /// The checkpoint projection whose required fingerprint field is read first.
        /// </param>
        /// <param name="acquisition">
        /// The retained acquisition input whose fingerprint is computed second.
        /// </param>
        /// <param name="command">
        /// The retained command clone serialized third with the normal shared options.
        /// </param>
        /// <param name="treatment">
        /// The retained treatment input whose fingerprint is computed fourth.
        /// </param>
        private Prefix(JsonObject checkpoint, WoundAcceptedTurnInput acquisition, JsonObject command,
            WoundAcceptedTurnInput treatment)
        {
            _checkpoint = checkpoint["fingerprint"]!.GetValue<string>();
            _acquisition = WoundAcceptedTurnFingerprints.ComputeInput(acquisition);
            _command = command.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed);
            _treatment = WoundAcceptedTurnFingerprints.ComputeInput(treatment);
            _commandOptions = command.Options;
        }

        /// <summary>
        /// Derives immutable fields from actual input data without accepting supplied input hashes.
        /// </summary>
        /// <param name="checkpoint">
        /// The checkpoint projection with its required fingerprint field.
        /// </param>
        /// <param name="acquisition">
        /// The acquisition input to fingerprint.
        /// </param>
        /// <param name="command">
        /// The command object to serialize; later changes do not affect the captured serialization.
        /// </param>
        /// <param name="treatment">
        /// The treatment input to fingerprint.
        /// </param>
        /// <returns>
        /// Captured data only; it does not validate or authorize any joint operation.
        /// </returns>
        internal static Prefix Capture(JsonObject checkpoint, WoundAcceptedTurnInput acquisition,
            JsonObject command, WoundAcceptedTurnInput treatment) => new(checkpoint, acquisition, command, treatment);

        /// <summary>
        /// Appends the four captured fields without retaining the destination.
        /// </summary>
        /// <param name="fields">
        /// The caller-owned field sequence to extend.
        /// </param>
        internal void AppendTo(List<string?> fields)
        {
            fields.Add(_checkpoint);
            fields.Add(_acquisition);
            fields.Add(_command);
            fields.Add(_treatment);
        }

        /// <summary>
        /// Reconstructs a detached command view from its immutable serialized source.
        /// </summary>
        /// <returns>
        /// A newly parsed object retaining the captured root node options.
        /// </returns>
        internal JsonObject ReadCommandRoot() => JsonNode.Parse(_command, _commandOptions)!.AsObject();
    }

    /// <summary>
    /// Owns a single copy of nullable before-image bytes, never exposing the private arrays.
    /// </summary>
    internal sealed class BeforeImages
    {
        private readonly Dictionary<string, byte[]?> _bytes;

        /// <summary>
        /// Copies source entries and byte arrays in the original enumeration order.
        /// </summary>
        /// <param name="source">
        /// The actual before-images; null and empty byte arrays remain distinct.
        /// </param>
        private BeforeImages(IReadOnlyDictionary<string, byte[]?> source) =>
            _bytes = source.ToDictionary(pair => pair.Key, pair => pair.Value?.ToArray(), StringComparer.Ordinal);

        /// <summary>
        /// Establishes immutable ownership before any initial fingerprint field computations.
        /// </summary>
        /// <param name="source">
        /// The before-images to copy in source enumeration order.
        /// </param>
        /// <returns>
        /// A byte owner with no mutable storage export or replacement operation.
        /// </returns>
        internal static BeforeImages Copy(IReadOnlyDictionary<string, byte[]?> source) => new(source);

        /// <summary>
        /// Returns a detached array of paths in source insertion order.
        /// </summary>
        /// <returns>
        /// The captured paths, including absent-file entries.
        /// </returns>
        internal string[] ReadPaths() => _bytes.Keys.ToArray();

        /// <summary>
        /// Returns a detached nullable byte value for one captured path.
        /// </summary>
        /// <param name="path">
        /// An existing path to look up with ordinal comparison.
        /// </param>
        /// <returns>
        /// A fresh array, or <see langword="null"/> when the captured file was absent.
        /// </returns>
        internal byte[]? ReadBytes(string path) => _bytes[path]?.ToArray();

        /// <summary>
        /// Compares a current value to private bytes without exporting either stored arrays or hashes.
        /// </summary>
        /// <param name="path">
        /// An existing captured path.
        /// </param>
        /// <param name="actual">
        /// Current bytes, or <see langword="null"/> for absence.
        /// </param>
        /// <returns>
        /// <see langword="true"/> for equal byte sequences or two absent files; otherwise, <see langword="false"/>.
        /// </returns>
        internal bool Matches(string path, byte[]? actual)
        {
            var expected = _bytes[path];
            return expected is null ? actual is null : actual is not null && expected.AsSpan().SequenceEqual(actual);
        }

        /// <summary>
        /// Derives exact ordinal path/hash fields from the owned bytes under the real hashing span.
        /// </summary>
        /// <returns>
        /// Newly allocated fields with null markers and uppercase SHA-256 strings.
        /// </returns>
        internal string?[] ComputeOrderedFields()
        {
            using var operationTiming = FileSystemManager.MeasureCurrentOperation("JointContinuation.HashBeforeImages");
            var fields = new List<string?>();
            foreach (var (path, bytes) in _bytes.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                fields.Add(path);
                fields.Add(bytes is null ? null : Convert.ToHexString(SHA256.HashData(bytes)));
            }
            return fields.ToArray();
        }
    }
}
