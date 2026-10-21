using System.Security.Cryptography;
using System.Text.Json.Nodes;
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
