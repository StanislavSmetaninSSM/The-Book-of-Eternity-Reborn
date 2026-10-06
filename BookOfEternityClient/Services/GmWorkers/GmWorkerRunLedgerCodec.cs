using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services.GmWorkers;

internal sealed record WorkerRunRetiredReference(string RunId, long Epoch, string TaskKeySha256, string RecordSha256);
internal sealed record WorkerLedgerState(int SchemaVersion, string RootKey, long Sequence, long EpochHighWater,
    WorkerRunRecord[] Entries, WorkerRunRetiredReference[] Retired);

internal static class GmWorkerRunLedgerCodec
{
    internal const int MaximumActiveEntries = 32;
    internal const int MaximumRetiredEntries = 4096;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly JsonSerializerOptions Options = new() { Converters = { new JsonStringEnumConverter() } };
    internal static WorkerLedgerState Initial(WorkerLedgerTarget target) => new(1, target.RootPath, 1, 0, [], []);

    internal static byte[] Encode(WorkerLedgerState state)
    {
        Validate(state);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(state, Options);
        if (bytes.Length > WorkerRunLedgerPersistence.MaximumStateBytes) throw GmWorkerRunRecordCodec.Invalid();
        return bytes;
    }

    internal static WorkerLedgerState Decode(WorkerLedgerTarget target, ReadOnlyMemory<byte> bytes)
    {
        if (bytes.Length is 0 or > WorkerRunLedgerPersistence.MaximumStateBytes) throw GmWorkerRunRecordCodec.Invalid();
        try
        {
            using var json = JsonDocument.Parse(StrictUtf8.GetString(bytes.Span), new JsonDocumentOptions { MaxDepth = 10 });
            var root = GmWorkerRunRecordCodec.Object(json.RootElement, "SchemaVersion", "RootKey", "Sequence", "EpochHighWater", "Entries", "Retired");
            var entries = root.GetProperty("Entries"); var retired = root.GetProperty("Retired");
            if (entries.ValueKind != JsonValueKind.Array || entries.GetArrayLength() > MaximumActiveEntries ||
                retired.ValueKind != JsonValueKind.Array || retired.GetArrayLength() != 0)
                throw GmWorkerRunRecordCodec.Invalid(); // Archive transitions have not been connected yet.
            var records = entries.EnumerateArray().Select(item =>
                GmWorkerRunRecordCodec.Decode(StrictUtf8.GetBytes(item.GetRawText()))).ToArray();
            var state = new WorkerLedgerState(root.GetProperty("SchemaVersion").GetInt32(),
                GmWorkerRunRecordCodec.String(root, "RootKey"), root.GetProperty("Sequence").GetInt64(),
                root.GetProperty("EpochHighWater").GetInt64(), records, []);
            Validate(state);
            if (!RootMatches(state.RootKey, target.RootPath)) throw GmWorkerRunRecordCodec.Invalid();
            return state;
        }
        catch (Exception error) when (error is JsonException or DecoderFallbackException or InvalidOperationException or FormatException or OverflowException)
        { throw GmWorkerRunRecordCodec.Invalid(); }
    }

    internal static WorkerLedgerState AddPrepared(WorkerLedgerState state, WorkerRunRecord record)
    {
        Validate(state); GmWorkerRunRecordCodec.Validate(record);
        if (record.Phase != WorkerRunPhase.Prepared || !RootMatches(record.Identity.RootKey, state.RootKey) ||
            state.Entries.Length >= MaximumActiveEntries || state.Entries.Length + state.Retired.Length >= MaximumRetiredEntries ||
            record.Identity.Epoch != checked(state.EpochHighWater + 1) ||
            state.Entries.Any(item => item.Identity.RunId == record.Identity.RunId || TaskKey(item.Identity) == TaskKey(record.Identity)))
            throw GmWorkerRunRecordCodec.Invalid();
        var result = state with { Sequence = checked(state.Sequence + 1), EpochHighWater = record.Identity.Epoch,
            Entries = [.. state.Entries, record] };
        Validate(result); return result;
    }

    internal static string TaskKey(WorkerRunIdentity identity) => Convert.ToHexString(SHA256.HashData(
        JsonSerializer.SerializeToUtf8Bytes(new[] { identity.GenerationId, identity.WorkerId, identity.TaskId }))).ToLowerInvariant();
    internal static bool RootMatches(string left, string right) => string.Equals(left, right,
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private static void Validate(WorkerLedgerState state)
    {
        if (state.SchemaVersion != 1 || !GmWorkerRunRecordCodec.CanonicalPath(state.RootKey) ||
            state.Sequence < 1 || state.EpochHighWater < 0 || state.Sequence <= state.EpochHighWater ||
            state.Entries.Length > MaximumActiveEntries || state.Retired.Length != 0 ||
            state.EpochHighWater != state.Entries.Length || state.EpochHighWater == 0 && state.Sequence != 1)
            throw GmWorkerRunRecordCodec.Invalid();
        var runs = new HashSet<string>(StringComparer.Ordinal);
        var epochs = new HashSet<long>(); var tasks = new HashSet<string>(StringComparer.Ordinal);
        foreach (var record in state.Entries)
        {
            _ = GmWorkerRunRecordCodec.Encode(record);
            if (record.Phase == WorkerRunPhase.AbortedBeforeLaunch || !RootMatches(record.Identity.RootKey, state.RootKey) ||
                record.Identity.Epoch > state.EpochHighWater || !runs.Add(record.Identity.RunId) ||
                !epochs.Add(record.Identity.Epoch) || !tasks.Add(TaskKey(record.Identity)))
                throw GmWorkerRunRecordCodec.Invalid();
        }
    }
}
