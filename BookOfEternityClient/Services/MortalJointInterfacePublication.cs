using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

// Committed computed DATA only. Neither a parsed record nor its hashes create a
// live admission, a historical public return, a reservation, or a plan take.
internal static class MortalJointInterfacePublication
{
    internal const string InterfacePath = "output/interface_updates.json";
    internal const string PendingField = "jointInterfacePublication";
    private static readonly string[] Fields = ["version", "state", "binding", "legacyPendingFingerprint",
        "sourceAssociationFingerprint", "beforeImage", "afterImage", "normalization", "fallback",
        "timestampCheck", "issues", "resultFingerprint", "fingerprint"];
    private static readonly string[] BindingFields = ["sessionGeneration", "sessionId", "requestId", "snapshotToken",
        "turnNumber", "realm", "stageOrdinal", "readyOrdinal", "iterationOrdinal", "checkpointFingerprint",
        "operationKey", "attemptId", "requestFingerprint"];
    private static readonly string[] IssueFields = ["filePath", "severity", "message", "category", "code", "actor",
        "section", "expected", "actual", "repairHint", "repairTargetFiles"];

    internal sealed record ComputedResult(byte[]? Before, byte[]? After, string Normalization,
        bool FallbackRequired, string? FallbackSample, string Document, string Timestamp,
        IReadOnlyList<ValidationIssue> Issues);

    internal sealed class Record
    {
        private readonly JsonObject _root;
        internal Record(JsonObject root) => _root = root.DeepClone().AsObject();
        internal string State => Text(_root, "state");
        internal string Fingerprint => Text(_root, "fingerprint");
        internal string ResultFingerprint => Text(_root, "resultFingerprint");
        internal string SourceAssociationFingerprint => Text(_root, "sourceAssociationFingerprint");
        internal JsonObject Binding => _root["binding"]!.DeepClone().AsObject();
        internal byte[]? BeforeBytes => DecodeImage(_root["beforeImage"]!.AsObject());
        internal byte[]? AfterBytes => DecodeImage(_root["afterImage"]!.AsObject());
        internal string Normalization => Text(_root, "normalization");
        internal IReadOnlyList<ValidationIssue> Issues => _root["issues"]!.AsArray()
            .Select(node => DecodeIssue(node!.AsObject())).ToArray();
        internal bool HasErrors => Issues.Any(issue => issue.Severity == IssueSeverity.Error);
        internal JsonObject ToJson() => _root.DeepClone().AsObject();
        internal Record AsPublished()
        {
            if (HasErrors) throw new InvalidDataException("joint_interface_error_result_is_held_prepared");
            var root = ToJson(); root["state"] = "Published"; Seal(root);
            return Read(JsonSerializer.SerializeToElement(root));
        }
        internal void RequireCheckpoint(JsonObject checkpoint)
        {
            var binding = Binding;
            foreach (var name in new[] { "sessionGeneration", "sessionId", "requestId", "snapshotToken", "turnNumber",
                "operationKey", "attemptId", "requestFingerprint" })
                if (!JsonNode.DeepEquals(binding[name], checkpoint[name]))
                    throw new InvalidDataException("joint_interface_checkpoint_binding_" + name);
            if (Text(binding, "checkpointFingerprint") != Text(checkpoint, "fingerprint") ||
                checkpoint["schemaVersion"]?.GetValue<int>() != 2 ||
                checkpoint["interfaceObservationProtocolVersion"]?.GetValue<int>() != 1)
                throw new InvalidDataException("joint_interface_checkpoint_protocol");
        }
        internal void RequireLegacyPending(JsonObject pending)
        {
            if (LegacyPendingFingerprint(pending) != Text(_root, "legacyPendingFingerprint"))
                throw new InvalidDataException("joint_interface_pending_graph_changed");
            RequireCheckpoint(pending["jointRepairCheckpoint"]?.AsObject() ??
                throw new InvalidDataException("joint_interface_checkpoint_missing"));
        }
    }

    internal static Record CreatePrepared(JsonObject binding, JsonObject legacyPending,
        string sourceAssociationFingerprint, ComputedResult computed)
    {
        var root = new JsonObject
        {
            ["version"] = 1, ["state"] = "Prepared", ["binding"] = binding.DeepClone(),
            ["legacyPendingFingerprint"] = LegacyPendingFingerprint(legacyPending),
            ["sourceAssociationFingerprint"] = sourceAssociationFingerprint,
            ["beforeImage"] = Image(computed.Before), ["afterImage"] = Image(computed.After),
            ["normalization"] = computed.Normalization,
            ["fallback"] = new JsonObject { ["required"] = computed.FallbackRequired,
                ["sampled"] = computed.FallbackSample is not null, ["value"] = computed.FallbackSample },
            ["timestampCheck"] = new JsonObject { ["document"] = computed.Document, ["timestamp"] = computed.Timestamp },
            ["issues"] = new JsonArray(computed.Issues.Select(issue => (JsonNode?)EncodeIssue(issue)).ToArray())
        };
        root["resultFingerprint"] = ResultSeal(root); Seal(root);
        return Read(JsonSerializer.SerializeToElement(root));
    }

    internal static Record Read(JsonElement root)
    {
        try
        {
            Exact(root, Fields);
            if (root.GetProperty("version").GetInt32() != 1 ||
                root.GetProperty("state").GetString() is not ("Prepared" or "Published"))
                throw new InvalidDataException("joint_interface_result_version_state");
            var binding = root.GetProperty("binding"); Exact(binding, BindingFields);
            foreach (var name in BindingFields.Except(["turnNumber", "stageOrdinal", "readyOrdinal", "iterationOrdinal"]))
                RequireNonblank(binding.GetProperty(name));
            if (binding.GetProperty("realm").GetString() != "mortal_world" || binding.GetProperty("turnNumber").GetInt32() < 0 ||
                new[] { "stageOrdinal", "readyOrdinal", "iterationOrdinal" }.Any(name => binding.GetProperty(name).GetInt64() <= 0))
                throw new InvalidDataException("joint_interface_result_binding");
            foreach (var name in new[] { "checkpointFingerprint", "requestFingerprint" }) RequireFingerprint(binding.GetProperty(name));
            foreach (var name in new[] { "legacyPendingFingerprint", "sourceAssociationFingerprint", "resultFingerprint", "fingerprint" })
                RequireFingerprint(root.GetProperty(name));
            ValidateImage(root.GetProperty("beforeImage")); ValidateImage(root.GetProperty("afterImage"));
            var before = DecodeImage(JsonNode.Parse(root.GetProperty("beforeImage").GetRawText())!.AsObject());
            var after = DecodeImage(JsonNode.Parse(root.GetProperty("afterImage").GetRawText())!.AsObject());
            var normalization = root.GetProperty("normalization").GetString();
            if (normalization switch
                {
                    "Absent" => before is not null || after is not null,
                    "NoWrite" => before is null || !BytesEqual(before, after),
                    "Changed" => before is null || after is null || BytesEqual(before, after),
                    _ => true
                }) throw new InvalidDataException("joint_interface_result_images");
            var fallback = root.GetProperty("fallback"); Exact(fallback, ["required", "sampled", "value"]);
            var required = fallback.GetProperty("required").GetBoolean(); var sampled = fallback.GetProperty("sampled").GetBoolean();
            var value = fallback.GetProperty("value");
            if (required != sampled || (sampled ? value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()) :
                value.ValueKind != JsonValueKind.Null) || required && normalization != "Changed")
                throw new InvalidDataException("joint_interface_result_fallback");
            if (sampled && (!DateTimeOffset.TryParseExact(value.GetString(), "O", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var sample) || sample.Offset != TimeSpan.Zero || sample.ToString("O") != value.GetString()))
                throw new InvalidDataException("joint_interface_result_fallback_utc_encoding");
            var timestamp = root.GetProperty("timestampCheck"); Exact(timestamp, ["document", "timestamp"]);
            var document = timestamp.GetProperty("document").GetString(); var stamp = timestamp.GetProperty("timestamp").GetString();
            if (document is not ("Absent" or "Blank" or "InvalidJson" or "NonObject" or "Object") ||
                stamp is not ("NotReached" or "MissingOrWrongKind" or "ParseRejected" or "ParseAccepted") ||
                (document != "Object") != (stamp == "NotReached") || normalization == "Absent" && document != "Absent")
                throw new InvalidDataException("joint_interface_result_timestamp_shape");
            var issues = root.GetProperty("issues");
            if (issues.ValueKind != JsonValueKind.Array) throw new InvalidDataException("joint_interface_result_issues");
            foreach (var issue in issues.EnumerateArray())
            {
                Exact(issue, IssueFields);
                foreach (var name in new[] { "filePath", "message" })
                    if (issue.GetProperty(name).ValueKind != JsonValueKind.String) throw new InvalidDataException("joint_interface_issue_string");
                foreach (var name in new[] { "code", "actor", "section", "expected", "actual", "repairHint" })
                    if (issue.GetProperty(name).ValueKind is not (JsonValueKind.String or JsonValueKind.Null))
                        throw new InvalidDataException("joint_interface_issue_nullable_string");
                if (issue.GetProperty("severity").GetString() is not ("Warning" or "Error") ||
                    issue.GetProperty("category").GetString() is not ("ProtocolViolation" or "StateConsistency" or "ClientOwnedSurface"))
                    throw new InvalidDataException("joint_interface_issue_enum");
                var targets = issue.GetProperty("repairTargetFiles");
                if (targets.ValueKind != JsonValueKind.Array) throw new InvalidDataException("joint_interface_issue_targets");
                var distinct = new HashSet<string>(StringComparer.Ordinal);
                foreach (var target in targets.EnumerateArray())
                {
                    RequireNonblank(target);
                    if (!distinct.Add(target.GetString()!)) throw new InvalidDataException("joint_interface_issue_duplicate_target");
                }
            }
            var node = JsonNode.Parse(root.GetRawText())!.AsObject();
            if (Text(node, "resultFingerprint") != ResultSeal(node) || Text(node, "fingerprint") != RecordSeal(node))
                throw new InvalidDataException("joint_interface_result_digest");
            var record = new Record(node);
            if (record.State == "Published" && record.HasErrors) throw new InvalidDataException("joint_interface_published_error");
            return record;
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException or FormatException or KeyNotFoundException or OverflowException)
        { throw new InvalidDataException("joint_interface_result_invalid", error); }
    }

    // Only this validated sibling may be removed for legacy graph comparison.
    internal static JsonObject LegacyPending(JsonObject pending)
    {
        var legacy = pending.DeepClone().AsObject();
        if (legacy[PendingField] is { } data)
        {
            var record = Read(JsonSerializer.SerializeToElement(data));
            record.RequireCheckpoint(legacy["jointRepairCheckpoint"]?.AsObject() ??
                throw new InvalidDataException("joint_interface_checkpoint_missing"));
            legacy.Remove(PendingField);
            record.RequireLegacyPending(legacy);
        }
        return legacy;
    }
    internal static string LegacyPendingFingerprint(JsonObject pending)
    {
        var legacy = pending.DeepClone().AsObject(); legacy.Remove(PendingField);
        return Fingerprint("legacy-pending", legacy);
    }

    internal static async Task<byte[]> CommitPreparedAsync(FileSystemManager files,
        FileSystemManager.CanonicalWriteLease lease, byte[] pendingBefore, Record prepared, Func<Task> validateOwner)
    {
        if (prepared.State != "Prepared") throw new InvalidDataException("joint_interface_prepare_state");
        var pending = ParsePending(pendingBefore); prepared.RequireLegacyPending(pending);
        if (pending.ContainsKey(PendingField)) throw new InvalidDataException("joint_interface_result_already_exists");
        pending[PendingField] = prepared.ToJson();
        var after = JsonSerializer.SerializeToUtf8Bytes(pending);
        var outcome = await files.PublishLocalFilesAsync(lease,
            [new(WoundAcceptedTurnSnapshotContract.PendingResolutionPath, pendingBefore, after)],
            validatePreparedNamespace: async () =>
            {
                await validateOwner();
                await RequireImagesAsync(files, lease, pendingBefore, prepared.BeforeBytes);
            });
        files.RequireCommittedLocalPublication(outcome);
        await RequireImagesAsync(files, lease, after, prepared.BeforeBytes);
        return after;
    }

    internal static async Task<Record> CommitPublishedAsync(FileSystemManager files,
        FileSystemManager.CanonicalWriteLease lease, byte[] pendingBefore, Func<Task> validateOwner)
    {
        var pending = ParsePending(pendingBefore);
        var record = Read(JsonSerializer.SerializeToElement(pending[PendingField] ??
            throw new InvalidDataException("joint_interface_result_missing")));
        record.RequireLegacyPending(pending);
        if (record.HasErrors) throw new InvalidDataException("joint_interface_error_prepared_held");
        if (record.State == "Published")
        {
            await validateOwner(); await RequireImagesAsync(files, lease, pendingBefore, record.AfterBytes);
            return record;
        }
        var published = record.AsPublished(); pending[PendingField] = published.ToJson();
        var after = JsonSerializer.SerializeToUtf8Bytes(pending);
        var changes = new List<CanonicalLocalFileChange>
            { new(WoundAcceptedTurnSnapshotContract.PendingResolutionPath, pendingBefore, after) };
        if (record.Normalization == "Changed") changes.Add(new(InterfacePath, record.BeforeBytes, record.AfterBytes));
        var outcome = await files.PublishLocalFilesAsync(lease, changes.ToArray(),
            validatePreparedNamespace: async () =>
            {
                await validateOwner();
                await RequireImagesAsync(files, lease, pendingBefore, record.BeforeBytes);
            });
        files.RequireCommittedLocalPublication(outcome);
        await RequireImagesAsync(files, lease, after, published.AfterBytes);
        return published;
    }

    internal static async Task RequireImagesAsync(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease,
        byte[] pending, byte[]? image)
    {
        if (!BytesEqual(pending, await files.ReadFileBytesAsync(lease, WoundAcceptedTurnSnapshotContract.PendingResolutionPath)) ||
            !BytesEqual(image, await files.ReadFileBytesAsync(lease, InterfacePath)))
            throw new InvalidDataException("joint_interface_actual_publication_preimage_or_readback_changed");
    }

    internal static bool BytesEqual(byte[]? left, byte[]? right) => left is null ? right is null :
        right is not null && left.AsSpan().SequenceEqual(right);
    private static JsonObject ParsePending(byte[] bytes) => StrictJsonAuthority.Deserialize<JsonObject>(
        CanonicalJsonUtf8.DecodeOneOptionalBom(bytes), Configuration.SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed,
        "joint interface pending") ?? throw new InvalidDataException("joint_interface_pending_object");
    private static JsonObject Image(byte[]? bytes) => new() { ["present"] = bytes is not null,
        ["base64"] = bytes is null ? null : Convert.ToBase64String(bytes),
        ["sha256"] = bytes is null ? null : Convert.ToHexString(SHA256.HashData(bytes)) };
    private static void ValidateImage(JsonElement image)
    {
        Exact(image, ["present", "base64", "sha256"]);
        if (!image.GetProperty("present").GetBoolean())
        {
            if (image.GetProperty("base64").ValueKind != JsonValueKind.Null || image.GetProperty("sha256").ValueKind != JsonValueKind.Null)
                throw new InvalidDataException("joint_interface_absent_image");
            return;
        }
        var encoded = image.GetProperty("base64").GetString() ?? throw new InvalidDataException("joint_interface_image_bytes");
        // Base64 decode allocates only in proportion to the actual record. It is
        // not subject to the unrelated rejected-draft checkpoint blob limit.
        _ = checked((encoded.Length / 4) * 3);
        var bytes = Convert.FromBase64String(encoded);
        if (Convert.ToBase64String(bytes) != encoded || Convert.ToHexString(SHA256.HashData(bytes)) != image.GetProperty("sha256").GetString())
            throw new InvalidDataException("joint_interface_image_digest");
    }
    private static byte[]? DecodeImage(JsonObject image) => image["present"]!.GetValue<bool>()
        ? Convert.FromBase64String(image["base64"]!.GetValue<string>()) : null;
    private static JsonObject EncodeIssue(ValidationIssue issue) => new()
    {
        ["filePath"] = issue.FilePath, ["severity"] = issue.Severity.ToString(), ["message"] = issue.Message,
        ["category"] = issue.Category.ToString(), ["code"] = issue.Code, ["actor"] = issue.Actor,
        ["section"] = issue.Section, ["expected"] = issue.Expected, ["actual"] = issue.Actual,
        ["repairHint"] = issue.RepairHint,
        ["repairTargetFiles"] = new JsonArray(issue.RepairTargetFiles.Select(path => (JsonNode?)JsonValue.Create(path)).ToArray())
    };
    private static ValidationIssue DecodeIssue(JsonObject issue) => new(Text(issue, "filePath"),
        Enum.Parse<IssueSeverity>(Text(issue, "severity")), Text(issue, "message"),
        issue["code"]?.GetValue<string>(), issue["actor"]?.GetValue<string>(), issue["section"]?.GetValue<string>(),
        issue["expected"]?.GetValue<string>(), issue["actual"]?.GetValue<string>(), issue["repairHint"]?.GetValue<string>(),
        Enum.Parse<IssueCategory>(Text(issue, "category")), issue["repairTargetFiles"]!.AsArray().Select(path => path!.GetValue<string>()).ToArray());
    private static void Exact(JsonElement value, string[] fields)
    {
        if (value.ValueKind != JsonValueKind.Object || !value.EnumerateObject().Select(row => row.Name)
            .Order(StringComparer.Ordinal).SequenceEqual(fields.Order(StringComparer.Ordinal)))
            throw new InvalidDataException("joint_interface_closed_shape");
    }
    private static void RequireNonblank(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
            throw new InvalidDataException("joint_interface_required_string");
    }
    private static void RequireFingerprint(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.String || !ResourceMaterializationContract.IsAuthorityFingerprint(value.GetString()))
            throw new InvalidDataException("joint_interface_required_fingerprint");
    }
    private static string Text(JsonObject root, string name) => root[name]!.GetValue<string>();
    private static string ResultSeal(JsonObject root)
    {
        var value = root.DeepClone().AsObject(); value.Remove("state"); value.Remove("resultFingerprint"); value.Remove("fingerprint");
        return Fingerprint("computed-result-v1", value);
    }
    private static string RecordSeal(JsonObject root)
    {
        var value = root.DeepClone().AsObject(); value.Remove("fingerprint"); return Fingerprint("record-v1", value);
    }
    private static void Seal(JsonObject root) => root["fingerprint"] = RecordSeal(root);
    private static string Fingerprint(string domain, JsonObject root) => WoundAcceptedTurnFingerprintWriter.Compute(
        new[] { "book_of_eternity.mortal_joint_interface." + domain, WoundAcceptedTurnFingerprintWriter.CanonicalJson(root) });
}
