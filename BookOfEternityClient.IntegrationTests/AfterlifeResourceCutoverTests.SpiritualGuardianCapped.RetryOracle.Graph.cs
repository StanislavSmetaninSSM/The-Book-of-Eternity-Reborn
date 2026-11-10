using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>One exact string token, optionally owning another exact JSON or base64 JSON surface.</summary>
    private sealed record GuardianCappedRetryScalar(string Path, string Pointer, string Property, string Value,
        GuardianCappedRetryDocument? Embedded, bool Base64);

    /// <summary>One duplicate-checked lexical JSON surface. Embedded text and preambles are never normalized.</summary>
    private sealed class GuardianCappedRetryDocument
    {
        internal readonly string Text;
        internal readonly string Prefix;
        internal readonly JsonNode Root;
        internal readonly List<GuardianCappedRetryScalar> Scalars = [];
        internal readonly List<(string Path, JsonObject Object)> Objects = [];
        internal readonly List<GuardianCappedRetryDocument> Children = [];

        /// <summary>Parses exactly one surface and recursively retains every embedded JSON representation.</summary>
        internal GuardianCappedRetryDocument(string text, string prefix = "")
        {
            Text = text; Prefix = prefix;
            var plain = text.StartsWith('\uFEFF') ? text[1..] : text;
            using var document = JsonDocument.Parse(plain); Check(document.RootElement);
            Root = JsonNode.Parse(plain)!; Walk(Root, "", "");
            void Check(JsonElement node)
            {
                if (node.ValueKind == JsonValueKind.Object)
                {
                    var names = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var row in node.EnumerateObject())
                    { Assert.True(names.Add(row.Name), "Duplicate JSON property at " + prefix + ": " + row.Name); Check(row.Value); }
                }
                else if (node.ValueKind == JsonValueKind.Array) foreach (var row in node.EnumerateArray()) Check(row);
            }
            void Walk(JsonNode? node, string pointer, string property)
            {
                if (node is JsonObject obj)
                {
                    Objects.Add((prefix + pointer, obj));
                    foreach (var row in obj) Walk(row.Value, pointer + "/" + GuardianCappedEscape(row.Key), row.Key);
                }
                else if (node is JsonArray rows)
                    for (var i = 0; i < rows.Count; i++) Walk(rows[i], pointer + "/" + i, i.ToString(System.Globalization.CultureInfo.InvariantCulture));
                else if (node is JsonValue value && value.TryGetValue<string>(out var scalar))
                {
                    GuardianCappedRetryDocument? embedded = null; var encoded = property.EndsWith("Base64", StringComparison.OrdinalIgnoreCase);
                    if (encoded)
                    {
                        var bytes = Convert.FromBase64String(scalar); Assert.Equal(scalar, Convert.ToBase64String(bytes));
                        string? decoded = null;
                        try { decoded = new UTF8Encoding(false, true).GetString(bytes); } catch (DecoderFallbackException) { /* Opaque non-JSON bytes remain exact DATA. */ }
                        if (decoded is not null && GuardianCappedLooksJson(decoded)) embedded = new(decoded, prefix + pointer + "!base64");
                    }
                    else if (GuardianCappedLooksJson(scalar)) embedded = new(scalar, prefix + pointer + "!json");
                    if (embedded is not null) Children.Add(embedded);
                    Scalars.Add(new(prefix + pointer, pointer, property, scalar, embedded, encoded));
                }
            }
        }

        /// <summary>Enumerates exact leaf tokens, descending into all retained representations instead of counting carriers twice.</summary>
        internal IEnumerable<GuardianCappedRetryScalar> Leaves() => Scalars.SelectMany(row => row.Embedded is null ? Enumerable.Repeat(row, 1) : row.Embedded.Leaves());
        /// <summary>Enumerates each physical representation once for schema and derived-hash checks.</summary>
        internal IEnumerable<GuardianCappedRetryDocument> Surfaces() => new[] { this }.Concat(Children.SelectMany(row => row.Surfaces()));

        /// <summary>Replaces only selected string tokens and recursively re-encodes their exact lexical carriers.</summary>
        internal string Patch(Func<GuardianCappedRetryScalar, string?> replace, bool allowContainerReplacement = false)
        {
            var replacements = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var row in Scalars)
            {
                var value = replace(row) ?? row.Value;
                if (row.Embedded is not null && value != row.Value) Assert.True(allowContainerReplacement, "Cannot erase a checked encoded subtree during graph comparison.");
                if (row.Embedded is not null && value == row.Value)
                {
                    var child = row.Embedded.Patch(replace, allowContainerReplacement);
                    value = row.Base64 ? Convert.ToBase64String(Encoding.UTF8.GetBytes(child)) : child;
                }
                if (value != row.Value) replacements.Add(row.Pointer, value);
            }
            return GuardianCappedPatchTokens(Text, replacements);
        }
    }

    /// <summary>Recognizes complete embedded object/array text without trimming away retained bytes.</summary>
    private static bool GuardianCappedLooksJson(string text)
    { var trimmed = text.TrimStart('\uFEFF', ' ', '\r', '\n', '\t'); return trimmed.StartsWith('{') || trimmed.StartsWith('['); }

    /// <summary>Escapes one literal RFC6901 property segment.</summary>
    private static string GuardianCappedEscape(string value) => value.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal);

    /// <summary>Changes quoted UTF8 tokens by structural location; all other bytes, whitespace, key order and escapes survive.</summary>
    private static string GuardianCappedPatchTokens(string text, IReadOnlyDictionary<string, string> values)
    {
        if (values.Count == 0) return text;
        var bytes = Encoding.UTF8.GetBytes(text); var bom = bytes.AsSpan().StartsWith(Encoding.UTF8.GetPreamble()) ? 3 : 0;
        var reader = new Utf8JsonReader(bytes.AsSpan(bom)); Assert.True(reader.Read());
        var edits = new List<(int Start, int End, byte[] Bytes)>(); Visit(ref reader, "");
        Assert.Equal(values.Count, edits.Count); using var output = new MemoryStream(); var offset = 0;
        foreach (var edit in edits.OrderBy(row => row.Start))
        { output.Write(bytes, offset, edit.Start - offset); output.Write(edit.Bytes); offset = edit.End; }
        output.Write(bytes, offset, bytes.Length - offset); return Encoding.UTF8.GetString(output.ToArray());
        void Visit(ref Utf8JsonReader current, string pointer)
        {
            if (current.TokenType == JsonTokenType.StartObject)
                while (current.Read() && current.TokenType != JsonTokenType.EndObject)
                { Assert.Equal(JsonTokenType.PropertyName, current.TokenType); var key = current.GetString()!; Assert.True(current.Read()); Visit(ref current, pointer + "/" + GuardianCappedEscape(key)); }
            else if (current.TokenType == JsonTokenType.StartArray)
            { var index = 0; while (current.Read() && current.TokenType != JsonTokenType.EndArray) Visit(ref current, pointer + "/" + index++); }
            else if (values.TryGetValue(pointer, out var replacement))
            { Assert.Equal(JsonTokenType.String, current.TokenType); edits.Add((bom + checked((int)current.TokenStartIndex), bom + checked((int)current.BytesConsumed), JsonSerializer.SerializeToUtf8Bytes(replacement))); }
        }
    }

    /// <summary>Compares complete surfaces after individually validated substitutions, preserving every embedded raw byte.</summary>
    private static void AssertGuardianCappedGraphEquivalent(GuardianCappedRetryAttemptData first, GuardianCappedRetryAttemptData retry)
    {
        Assert.Equal(first.Symbols.Keys.Order(StringComparer.Ordinal), retry.Symbols.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(first.Bindings.Keys.Order(StringComparer.Ordinal), retry.Bindings.Keys.Order(StringComparer.Ordinal));
        var before = new GuardianCappedRetryDocument(first.EnvelopeJson);
        var translated = before.Patch(row => first.Bindings.TryGetValue(row.Path, out var symbol) ? retry.Symbols[symbol] : null);
        // Only outer serializer presentation may differ. All inner raw JSON/base64 remains exact string DATA.
        Assert.True(JsonNode.DeepEquals(new GuardianCappedRetryDocument(translated).Root, new GuardianCappedRetryDocument(retry.EnvelopeJson).Root),
            "Difference outside independently validated full capped identity graph.");
        foreach (var pair in first.Bindings) Assert.Equal(pair.Value, retry.Bindings[pair.Key]);
    }
    /// <summary>Derives the positive 31-symbol/340-token graph from checked semantic anchors, exact field roles and producer outputs.</summary>
    private static GuardianCappedRetryAttemptData BuildGuardianCappedRetryGraph(GuardianCappedRetryFixture fixture, string json,
        GuardianCappedRetryDocument document, string[] allocations, IReadOnlyList<GuardianCappedRetryHash> hashes)
    {
        var root = document.Root; var leaves = document.Leaves().ToArray(); var firstPending = document.Surfaces().Single(row => row.Prefix == "/First/FirstPendingJson!json").Root["pending"]!;
        var imageRows = firstPending["candidateAfterImages"]!.AsArray();
        var historyIndex = GuardianCappedIndex(imageRows, row => row["path"]!.GetValue<string>() == BookOfEternityClient.Services.ResourceMaterializationContract.HistoryPath);
        var stateIndex = GuardianCappedIndex(imageRows, row => row["path"]!.GetValue<string>() == BookOfEternityClient.Services.ResourceMaterializationContract.StatePath);
        var decisionIndex = root["Completed"]!["Reduction"]!["ReceiptAfterImage"]!["decisions"]!.AsArray().Count - 1;
        var definitions = new List<(string Symbol, string Path, int Count, string Roles, string[] Dependencies)>();
        for (var i = 0; i < allocations.Length; i++) definitions.Add(("A" + i, "/Completed/AllocationJournal/" + i + "/value", i is 0 or 2 ? 32 : i is 1 or 3 ? 36 : 34,
            i is 0 or 2 ? "value OperationId operationId" : "value TransitionId transitionId lastTransitionId", []));
        Add("resourceResult", "/First/Witness/resourceResultFingerprint", 16, "resourceResultFingerprint", "A0 A1 A2 A3");
        Add("effectAfterBoundary", "/First/Interval/EffectAfter/Fingerprint", 1, "Fingerprint", "A0 A1 A2 A3");
        Add("closedEffectPrefix", "/First/Witness/effectPlanFingerprint", 16, "effectPlanFingerprint", "effectAfterBoundary");
        Add("sourceFingerprint", "/First/Witness/sourceFingerprint", 20, "sourceFingerprint", "resourceResult closedEffectPrefix");
        Add("sourceId", "/First/Witness/sourceId", 24, "sourceId", "sourceFingerprint");
        Add("historyImage", "/First/FirstPendingJson!json/pending/candidateAfterImages/" + historyIndex + "/contentFingerprint", 7, "contentFingerprint", "A0 A1 A2 A3");
        Add("stateImage", "/First/FirstPendingJson!json/pending/candidateAfterImages/" + stateIndex + "/contentFingerprint", 7, "contentFingerprint", "A1 A3");
        Add("firstPrefix", "/First/FirstPendingJson!json/pending/retainedPrefixFingerprint", 4, "retainedPrefixFingerprint", "sourceId sourceFingerprint historyImage stateImage");
        Add("firstPacket", "/First/FirstPendingJson!json/pending/packetFingerprint", 14, "packetFingerprint initialPendingPacketFingerprint priorPendingPacketFingerprint expectedPendingPacketFingerprint", "firstPrefix");
        Add("firstCheckpoint", "/First/CommittedCheckpointJson!json/checkpoint/checkpointFingerprint", 2, "checkpointFingerprint", "firstPacket A0 A1 A2 A3");
        Add("firstCheckpointImage", "/First/Transport/0/Fingerprint", 1, "Fingerprint", "firstCheckpoint");
        Add("firstPendingImage", "/First/Transport/1/Fingerprint", 1, "Fingerprint", "firstPacket");
        Add("stagedDecision", "/Completed/NoneDecision/decisionFingerprint", 7, "decisionFingerprint", "sourceId");
        Add("receiptDecisionId", "/Completed/Reduction/ReceiptAfterImage/decisions/" + decisionIndex + "/decisionId", 4, "decisionId", "stagedDecision");
        Add("receiptDecisionFingerprint", "/Completed/Reduction/ReceiptAfterImage/decisions/" + decisionIndex + "/decisionFingerprint", 4, "decisionFingerprint", "receiptDecisionId sourceId sourceFingerprint");
        Add("completedPrefix", "/Completed/CompletedPending/retainedPrefixFingerprint", 3, "retainedPrefixFingerprint", "sourceId sourceFingerprint historyImage stateImage stagedDecision");
        Add("completedPacket", "/Completed/CompletedPending/packetFingerprint", 9, "packetFingerprint resultPendingPacketFingerprint expectedPendingPacketFingerprint", "completedPrefix");
        Add("completedCheckpoint", "/Completed/CompletedCheckpoint/checkpointFingerprint", 3, "checkpointFingerprint", "firstPacket completedPacket stagedDecision A0 A1 A2 A3 A4");
        Add("completedCheckpointImage", "/Completed/Transport/0/Fingerprint", 2, "Fingerprint", "completedCheckpoint");
        Add("completedPendingImage", "/Completed/Transport/1/Fingerprint", 2, "Fingerprint", "completedPacket");
        Add("finalBoundary", "/Completed/Reduction/Resources/FullEffectBoundary/Fingerprint", 2, "Fingerprint", "A0 A1 A2 A3");
        Add("finalEffectPayload", "/Plan/EffectPlan/AcceptedBoundaryFinalPlanFingerprint", 8, "AcceptedBoundaryFinalPlanFingerprint completedPlanRef EffectPayloadFingerprint", "A4");
        Add("finalEffectImageRef", "/Plan/EffectPlan/LifecycleExecutionLedger/passes/1/initialRoster/0/beforeImageRef", 6, "beforeImageRef", "A4");
        Add("finalRosterRef", "/Plan/EffectPlan/LifecycleExecutionLedger/passes/1/initialRosterRef", 4, "initialRosterRef rosterRef", "finalEffectImageRef A4");
        Add("lifecycleLedger", "/Plan/EffectPlan/LifecycleExecutionLedgerFingerprint", 2, "LifecycleExecutionLedgerFingerprint", "A4 finalEffectPayload finalEffectImageRef finalRosterRef");
        Add("preparedPlan", "/Plan/PreparedPlanFingerprint", 1, "PreparedPlanFingerprint", "A0 A1 A2 A3 A4 receiptDecisionFingerprint finalEffectPayload");
        var symbols = definitions.ToDictionary(row => row.Symbol, row => Assert.Single(leaves.Where(leaf => leaf.Path == row.Path)).Value, StringComparer.Ordinal);
        Assert.Equal(definitions.Count, symbols.Values.Distinct(StringComparer.Ordinal).Count());
        var bindings = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var definition in definitions)
        {
            var value = symbols[definition.Symbol]; var locations = leaves.Where(row => row.Value == value).ToArray(); Assert.Equal(definition.Count, locations.Length);
            if (!definition.Symbol.StartsWith('A')) Assert.Contains(hashes, row => row.Path == definition.Path && row.Value == value && row.Calculated == value);
            foreach (var row in locations)
            {
                var roleAllowed = definition.Roles.Split(' ').Contains(row.Property, StringComparer.Ordinal);
                var framedOperand = row.Path.StartsWith("/PreparedPlanHashOperands/", StringComparison.Ordinal) || row.Path.StartsWith("/EffectPayloadHashOperands/", StringComparison.Ordinal) || row.Path.StartsWith("/Completed/Reduction/EffectPayloadHashOperands/", StringComparison.Ordinal);
                var allocatedEffect = definition.Symbol == "A4" && row.Path.EndsWith("/AllocatedTransitionIds/0", StringComparison.Ordinal);
                var decisionAdvance = definition.Symbol == "stagedDecision" && row.Path.EndsWith("/newDecisionFingerprints/0", StringComparison.Ordinal);
                Assert.True(roleAllowed || framedOperand || allocatedEffect || decisionAdvance, "Unowned identity role at " + row.Path);
                // Count/index/semantic joins above close these locations. A matching scalar value alone never adds a new role.
                Assert.True(bindings.TryAdd(row.Path, definition.Symbol), "Duplicate identity location " + row.Path);
            }
        }
        foreach (var leaf in leaves)
            foreach (var value in symbols.Values)
                if (leaf.Value != value) Assert.False(leaf.Value.Contains(value, StringComparison.Ordinal), "Hidden identity substring at " + leaf.Path);
        foreach (var surface in document.Surfaces()) foreach (var (_, obj) in surface.Objects) foreach (var key in obj.Select(row => row.Key))
            foreach (var value in symbols.Values) Assert.False(key.Contains(value, StringComparison.Ordinal), "Identity used as a property name.");
        // These counts are independently measured from BOTH actual positive envelopes, never the old zero 74-position map.
        Assert.Equal(31, symbols.Count); Assert.Equal(340, bindings.Count);
        return new(json, fixture, allocations, symbols, bindings, definitions.ToDictionary(row => row.Symbol, row => row.Dependencies, StringComparer.Ordinal));
        void Add(string symbol, string path, int count, string roles, string dependencies) => definitions.Add((symbol, path, count, roles, dependencies.Split(' ', StringSplitOptions.RemoveEmptyEntries)));
    }

    /// <summary>Revalidates all reported graph DATA rather than trusting a stale or forged map record.</summary>
    private static GuardianCappedRetryAttemptData ValidateGuardianCappedReportedAttempt(GuardianCappedRetryAttemptData reported)
    {
        var actual = ReadGuardianCappedRetryAttempt(reported.Fixture, reported.EnvelopeJson); Assert.Equal(actual.Allocations, reported.Allocations);
        Assert.Equal(actual.Symbols.OrderBy(row => row.Key, StringComparer.Ordinal), reported.Symbols.OrderBy(row => row.Key, StringComparer.Ordinal));
        Assert.Equal(actual.Bindings.OrderBy(row => row.Key, StringComparer.Ordinal), reported.Bindings.OrderBy(row => row.Key, StringComparer.Ordinal));
        Assert.Equal(actual.Dependencies.Keys.Order(StringComparer.Ordinal), reported.Dependencies.Keys.Order(StringComparer.Ordinal));
        foreach (var row in actual.Dependencies) Assert.Equal(row.Value, reported.Dependencies[row.Key]); return actual;
    }

    /// <summary>Requires one unchanged live fixture, disjoint new allocations and complete raw/semantic retry equivalence.</summary>
    private static void AssertGuardianCappedFreshRetryEquivalent(GuardianAttributionJourney setup, GuardianCappedRetryAttemptData first, GuardianCappedRetryAttemptData retry)
    {
        first = ReadGuardianCappedRetryAttempt(setup, first.EnvelopeJson, first); retry = ReadGuardianCappedRetryAttempt(setup, retry.EnvelopeJson, first);
        Assert.Equal(first.Allocations.Length + retry.Allocations.Length, first.Allocations.Concat(retry.Allocations).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(first.Fixture.Name, retry.Fixture.Name); Assert.Equal(first.Fixture.Rank, retry.Fixture.Rank);
        Assert.Equal(first.Fixture.ImmutableFirstJson, retry.Fixture.ImmutableFirstJson);
        AssertGuardianCappedGraphEquivalent(first, retry);
    }

    /// <summary>Reports full actual semantic symbols, every allowed encoding/location and dependency count without live owner access.</summary>
    private void WriteGuardianCappedRetryMap(string phase, string name, GuardianCappedRetryAttemptData attempt, int? probeCount)
    {
        var current = ValidateGuardianCappedReportedAttempt(attempt);
        _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase, name, allocations = current.Allocations, symbolCount = current.Symbols.Count,
            bindingCount = current.Bindings.Count, probeCount, symbols = current.Symbols, dependencies = current.Dependencies, bindings = current.Bindings }));
    }
}
