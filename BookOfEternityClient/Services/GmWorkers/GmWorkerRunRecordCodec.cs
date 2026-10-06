using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BookOfEternityClient.Services.GmWorkers;

// Pure bounded syntax. Quiescent record syntax alone does not prove ledger/archive consistency.
internal static class GmWorkerRunRecordCodec
{
    internal const int MaximumBytes = 64 * 1024;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly JsonSerializerOptions Options = new() { Converters = { new JsonStringEnumConverter() } };

    internal static WorkerRunRecordObservation Observe(ReadOnlyMemory<byte> bytes)
    {
        try
        {
            var record = Decode(bytes);
            return new(record.Phase is WorkerRunPhase.AbortedBeforeLaunch or WorkerRunPhase.Retired
                ? WorkerRunObservationKind.Quiescent : WorkerRunObservationKind.Uncertain, record);
        }
        catch (InvalidDataException) { return new(WorkerRunObservationKind.Blocked, null); }
    }

    internal static byte[] Encode(WorkerRunRecord record)
    {
        Validate(record);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(record, Options);
        if (bytes.Length > MaximumBytes) throw Invalid();
        return bytes;
    }

    internal static WorkerRunRecord Decode(ReadOnlyMemory<byte> bytes)
    {
        if (bytes.Length is 0 or > MaximumBytes) throw Invalid();
        try
        {
            using var json = JsonDocument.Parse(StrictUtf8.GetString(bytes.Span), new JsonDocumentOptions { MaxDepth = 8 });
            var root = Object(json.RootElement, "SchemaVersion", "Identity", "Phase", "Progress");
            var i = Object(root.GetProperty("Identity"), "RootKey", "Epoch", "RunId", "GenerationId",
                "WorkerId", "TaskId", "TaskSha256", "Backend", "Scope", "HostInstanceId", "WorkspacePath");
            var record = new WorkerRunRecord(root.GetProperty("SchemaVersion").GetInt32(), new(
                String(i, "RootKey"), i.GetProperty("Epoch").GetInt64(), String(i, "RunId"),
                String(i, "GenerationId"), String(i, "WorkerId"), String(i, "TaskId"), String(i, "TaskSha256"),
                EnumValue<WorkerRunBackend>(i, "Backend"), EnumValue<WorkerRunScope>(i, "Scope"),
                String(i, "HostInstanceId"), String(i, "WorkspacePath")), EnumValue<WorkerRunPhase>(root, "Phase"),
                ReadProgress(root.GetProperty("Progress")));
            Validate(record);
            return record;
        }
        catch (Exception error) when (error is JsonException or DecoderFallbackException or
            InvalidOperationException or FormatException or OverflowException or ArgumentException)
        {
            // Never expose parser/property/provider payload in diagnostics or inner exceptions.
            throw Invalid();
        }
    }

    internal static void Validate(WorkerRunRecord record)
    {
        var i = record?.Identity;
        if (record is null || i is null || record.SchemaVersion != 2 || !Enum.IsDefined(record.Phase) ||
            !CanonicalPath(i.RootKey) || !CanonicalPath(i.WorkspacePath) || i.Epoch < 1 ||
            !Id(i.RunId) || !Id(i.GenerationId, allowEmpty: true) || !Id(i.HostInstanceId) ||
            !Text(i.WorkerId, 1024) || !Text(i.TaskId, 1024) || !Digest(i.TaskSha256) ||
            (i.Backend, i.Scope) is not ((WorkerRunBackend.WindowsJob, WorkerRunScope.WindowsJob) or
                (WorkerRunBackend.LinuxSystemd, WorkerRunScope.SystemdUnit) or
                (WorkerRunBackend.LinuxNativeLineage, WorkerRunScope.OrdinarySamePidNamespace)))
            throw Invalid();
        ValidateProgress(record);
    }

    private static WorkerRunProgress? ReadProgress(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Null) return null;
        var progress = Object(value, "Publication", "Cleanup");
        WorkerRunPublication? publication = null; WorkerRunCleanup? cleanup = null;
        var p = progress.GetProperty("Publication");
        if (p.ValueKind != JsonValueKind.Null)
        {
            Object(p, "ProposalId", "ProposalSha256", "ContentSha256", "Committed");
            publication = new(String(p, "ProposalId"), String(p, "ProposalSha256"), String(p, "ContentSha256"), p.GetProperty("Committed").GetBoolean());
        }
        var c = progress.GetProperty("Cleanup");
        if (c.ValueKind != JsonValueKind.Null)
        {
            Object(c, "RequiredAudit", "AuditEventId", "AuditSha256");
            cleanup = new(c.GetProperty("RequiredAudit").GetBoolean(), NullableString(c, "AuditEventId"), NullableString(c, "AuditSha256"));
        }
        return new(publication, cleanup);
    }

    private static void ValidateProgress(WorkerRunRecord record)
    {
        var progress = record.Progress; var publication = progress?.Publication; var cleanup = progress?.Cleanup;
        if (progress is { Publication: null, Cleanup: null } ||
            publication is not null && (!Text(publication.ProposalId, 1024) || !Digest(publication.ProposalSha256) || !Digest(publication.ContentSha256)) ||
            cleanup is not null && (cleanup.RequiredAudit
                ? !Text(cleanup.AuditEventId, 1024) || !Digest(cleanup.AuditSha256)
                : cleanup.AuditEventId is not null || cleanup.AuditSha256 is not null)) throw Invalid();
        var valid = record.Phase switch
        {
            WorkerRunPhase.Prepared => publication == null,
            WorkerRunPhase.LaunchIntent or WorkerRunPhase.ReleaseIntent or
                WorkerRunPhase.Released or WorkerRunPhase.StopValidated => progress is null,
            WorkerRunPhase.PublicationIntent => publication is { Committed: false } && cleanup is null,
            WorkerRunPhase.Published => publication is { Committed: true } && cleanup is null,
            WorkerRunPhase.CleanupPending => publication is null or { Committed: true },
            WorkerRunPhase.Retired => cleanup is not null && (publication is null or { Committed: true }),
            WorkerRunPhase.AbortedBeforeLaunch => publication is null,
            WorkerRunPhase.Uncertain => true,
            _ => false
        };
        if (!valid) throw Invalid();
    }

    private static string? NullableString(JsonElement element, string name) =>
        element.GetProperty(name).ValueKind == JsonValueKind.Null ? null : String(element, name);

    internal static InvalidDataException Invalid() => new("Worker run record is invalid.");
    internal static bool Digest(string? value) => value is { Length: 64 } && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    internal static bool Id(string? value, bool allowEmpty = false) => Guid.TryParseExact(value, "N", out var id) &&
        (allowEmpty || id != Guid.Empty) && id.ToString("N") == value;
    internal static bool CanonicalPath(string? value)
    {
        if (!Text(value, 4096) || !Path.IsPathFullyQualified(value!)) return false;
        try { return Path.TrimEndingDirectorySeparator(Path.GetFullPath(value!)) == value; }
        catch (Exception error) when (error is ArgumentException or NotSupportedException or PathTooLongException) { return false; }
    }

    private static bool Text(string? value, int maximum)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maximum || value.Any(char.IsControl)) return false;
        try { return StrictUtf8.GetByteCount(value) <= maximum; }
        catch (EncoderFallbackException) { return false; }
    }

    internal static JsonElement Object(JsonElement element, params string[] names)
    {
        if (element.ValueKind != JsonValueKind.Object) throw Invalid();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
            if (!names.Contains(property.Name, StringComparer.Ordinal) || !seen.Add(property.Name)) throw Invalid();
        if (seen.Count != names.Length) throw Invalid();
        return element;
    }

    internal static string String(JsonElement element, string name) =>
        element.GetProperty(name).ValueKind == JsonValueKind.String ? element.GetProperty(name).GetString()! : throw Invalid();

    private static T EnumValue<T>(JsonElement element, string name) where T : struct, Enum
    {
        var text = String(element, name);
        if (!Enum.TryParse<T>(text, out var value) || Enum.GetName(value) != text) throw Invalid();
        return value;
    }
}
