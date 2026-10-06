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
    internal static WorkerLedgerState Initial(WorkerLedgerTarget target) => new(2, target.RootPath, 1, 0, [], []);

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
                retired.ValueKind != JsonValueKind.Array || retired.GetArrayLength() > MaximumRetiredEntries)
                throw GmWorkerRunRecordCodec.Invalid();
            var records = entries.EnumerateArray().Select(item =>
                GmWorkerRunRecordCodec.Decode(StrictUtf8.GetBytes(item.GetRawText()))).ToArray();
            var state = new WorkerLedgerState(root.GetProperty("SchemaVersion").GetInt32(),
                GmWorkerRunRecordCodec.String(root, "RootKey"), root.GetProperty("Sequence").GetInt64(),
                root.GetProperty("EpochHighWater").GetInt64(), records, retired.EnumerateArray().Select(ReadReference).ToArray());
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
            state.Entries.Any(item => item.Identity.RunId == record.Identity.RunId || TaskKey(item.Identity) == TaskKey(record.Identity)) ||
            state.Retired.Any(item => item.RunId == record.Identity.RunId || item.TaskKeySha256 == TaskKey(record.Identity)))
            throw GmWorkerRunRecordCodec.Invalid();
        var result = state with { Sequence = checked(state.Sequence + 1), EpochHighWater = record.Identity.Epoch,
            Entries = [.. state.Entries, record] };
        Validate(result); return result;
    }

    internal static string TaskKey(WorkerRunIdentity identity) => TaskKey(identity.GenerationId, identity.WorkerId, identity.TaskId);
    internal static string TaskKey(string generation, string worker, string task) => Convert.ToHexString(SHA256.HashData(
        JsonSerializer.SerializeToUtf8Bytes(new[] { generation, worker, task }))).ToLowerInvariant();
    internal static bool RootMatches(string left, string right) => string.Equals(left, right,
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    internal static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    internal static WorkerLedgerState Transition(WorkerLedgerState state, WorkerRunIdentity identity, WorkerRunPhase phase)
    {
        Validate(state);
        var current = state.Entries.SingleOrDefault(item => item.Identity == identity) ?? throw GmWorkerRunRecordCodec.Invalid();
        if (phase == WorkerRunPhase.Uncertain ? current.Phase == WorkerRunPhase.Uncertain :
            phase is not (WorkerRunPhase.LaunchIntent or WorkerRunPhase.AbortedBeforeLaunch) || current.Phase != WorkerRunPhase.Prepared)
            throw GmWorkerRunRecordCodec.Invalid();
        var next = current with { Phase = phase };
        var result = phase == WorkerRunPhase.AbortedBeforeLaunch
            ? state with { Sequence = checked(state.Sequence + 1), Entries = state.Entries.Where(item => item != current).ToArray(),
                Retired = [.. state.Retired, new(identity.RunId, identity.Epoch, TaskKey(identity), Hash(GmWorkerRunRecordCodec.Encode(next)))] }
            : state with { Sequence = checked(state.Sequence + 1), Entries = state.Entries.Select(item => item == current ? next : item).ToArray() };
        Validate(result); return result;
    }

    // A live operation is minted by the original registered execution, never by
    // decoding a phase or supplying a generic byte/state replacement.
    internal static WorkerLedgerState TransitionLive(WorkerLedgerState state, GmWorkerDurableExecution.Mutation mutation)
    {
        Validate(state);
        var current = state.Entries.SingleOrDefault(item => item.Identity == mutation.Identity)
            ?? throw GmWorkerRunRecordCodec.Invalid();
        var next = mutation.ApplyTo(current);
        var terminal = next.Phase is WorkerRunPhase.Retired or WorkerRunPhase.AbortedBeforeLaunch;
        var result = terminal
            ? state with { Sequence = checked(state.Sequence + 1), Entries = state.Entries.Where(item => item != current).ToArray(),
                Retired = [.. state.Retired, new(next.Identity.RunId, next.Identity.Epoch, TaskKey(next.Identity), Hash(GmWorkerRunRecordCodec.Encode(next)))] }
            : state with { Sequence = checked(state.Sequence + 1), Entries = state.Entries.Select(item => item == current ? next : item).ToArray() };
        Validate(result); return result;
    }

    private static WorkerRunRetiredReference ReadReference(JsonElement element)
    {
        var item = GmWorkerRunRecordCodec.Object(element, "RunId", "Epoch", "TaskKeySha256", "RecordSha256");
        return new(GmWorkerRunRecordCodec.String(item, "RunId"), item.GetProperty("Epoch").GetInt64(),
            GmWorkerRunRecordCodec.String(item, "TaskKeySha256"), GmWorkerRunRecordCodec.String(item, "RecordSha256"));
    }

    private static void Validate(WorkerLedgerState state)
    {
        if (state.SchemaVersion != 2 || !GmWorkerRunRecordCodec.CanonicalPath(state.RootKey) ||
            state.Sequence < 1 || state.EpochHighWater < 0 || state.Sequence <= state.EpochHighWater ||
            state.Entries.Length > MaximumActiveEntries || state.Retired.Length > MaximumRetiredEntries ||
            state.EpochHighWater > MaximumRetiredEntries || state.EpochHighWater != state.Entries.Length + state.Retired.Length ||
            state.EpochHighWater == 0 && state.Sequence != 1)
            throw GmWorkerRunRecordCodec.Invalid();
        var runs = new HashSet<string>(StringComparer.Ordinal);
        var epochs = new HashSet<long>(); var tasks = new HashSet<string>(StringComparer.Ordinal);
        foreach (var record in state.Entries)
        {
            _ = GmWorkerRunRecordCodec.Encode(record);
            if (record.Phase is WorkerRunPhase.AbortedBeforeLaunch or WorkerRunPhase.Retired || !RootMatches(record.Identity.RootKey, state.RootKey) ||
                record.Identity.Epoch > state.EpochHighWater || !runs.Add(record.Identity.RunId) ||
                !epochs.Add(record.Identity.Epoch) || !tasks.Add(TaskKey(record.Identity)))
                throw GmWorkerRunRecordCodec.Invalid();
        }
        foreach (var item in state.Retired)
            if (!GmWorkerRunRecordCodec.Id(item.RunId, false) || item.Epoch < 1 || item.Epoch > state.EpochHighWater ||
                !GmWorkerRunRecordCodec.Digest(item.TaskKeySha256) || !GmWorkerRunRecordCodec.Digest(item.RecordSha256) ||
                !runs.Add(item.RunId) || !epochs.Add(item.Epoch) || !tasks.Add(item.TaskKeySha256))
                throw GmWorkerRunRecordCodec.Invalid();
    }
}
