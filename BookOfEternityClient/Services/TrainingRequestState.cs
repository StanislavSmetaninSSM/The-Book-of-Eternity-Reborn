using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public static class TrainingRequestState
{
    public const string PendingRequestPath = "game_state/control/pending_training_showcase_requests.json";
    public const string AfterlifePurchaseReceiptsProperty = "afterlifeTrainingPurchaseReceipts";

    private static readonly JsonSerializerOptions JsonOpts = SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed;
    private static readonly SemaphoreSlim RequestWriteGate = new(1, 1);

    public sealed record PendingTrainingShowcaseRequest(
        string RequestId,
        string RequestKind,
        string SourceActorId,
        string SourceActorName,
        string SourceActorKind,
        string Realm,
        int CreatedAtTurn,
        DateTime CreatedAtUtc,
        string? SourceActorSnapshotHash,
        string Reason,
        JsonObject? Details = null);

    public sealed record PendingTrainingShowcaseRequestWriteResult(
        PendingTrainingShowcaseRequest Request,
        bool CreatedByThisCall);

    private sealed record PendingTrainingShowcaseRequestRoot(
        IReadOnlyList<PendingTrainingShowcaseRequest> Requests);

    public static async Task<IReadOnlyList<PendingTrainingShowcaseRequest>> ReadRequestsAsync(FileSystemManager fs)
    {
        var raw = await fs.ReadFileAsync(PendingRequestPath);
        if (string.IsNullOrWhiteSpace(raw))
            return Array.Empty<PendingTrainingShowcaseRequest>();

        try
        {
            var root = JsonSerializer.Deserialize<PendingTrainingShowcaseRequestRoot>(raw, JsonOpts);
            return root?.Requests ?? Array.Empty<PendingTrainingShowcaseRequest>();
        }
        catch (JsonException)
        {
            return Array.Empty<PendingTrainingShowcaseRequest>();
        }
    }

    public static async Task<PendingTrainingShowcaseRequest?> FindPendingRequestAsync(
        FileSystemManager fs,
        string sourceActorId,
        string requestKind) =>
        await FindPendingRequestAsync(fs, sourceActorId, requestKind, dedupeKey: null);

    public static async Task<PendingTrainingShowcaseRequest?> FindPendingRequestAsync(
        FileSystemManager fs,
        string sourceActorId,
        string requestKind,
        string? dedupeKey)
    {
        var requests = await ReadRequestsAsync(fs);
        return requests.FirstOrDefault(request =>
            string.Equals(request.SourceActorId, sourceActorId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(request.RequestKind, requestKind, StringComparison.OrdinalIgnoreCase) &&
            (string.IsNullOrWhiteSpace(dedupeKey) ||
             string.Equals(GetRequestDedupeKey(request.Details), dedupeKey, StringComparison.OrdinalIgnoreCase)));
    }

    public static async Task<PendingTrainingShowcaseRequest> WriteRequestAsync(
        FileSystemManager fs,
        string requestKind,
        string sourceActorId,
        string sourceActorName,
        string sourceActorKind,
        string realm,
        int createdAtTurn,
        string? sourceActorSnapshotHash,
        string reason) =>
        await WriteRequestAsync(
            fs,
            requestKind,
            sourceActorId,
            sourceActorName,
            sourceActorKind,
            realm,
            createdAtTurn,
            sourceActorSnapshotHash,
            reason,
            details: null);

    public static async Task<PendingTrainingShowcaseRequest> WriteRequestAsync(
        FileSystemManager fs,
        string requestKind,
        string sourceActorId,
        string sourceActorName,
        string sourceActorKind,
        string realm,
        int createdAtTurn,
        string? sourceActorSnapshotHash,
        string reason,
        JsonObject? details)
        => (await WriteRequestWithStatusAsync(
            fs,
            requestKind,
            sourceActorId,
            sourceActorName,
            sourceActorKind,
            realm,
            createdAtTurn,
            sourceActorSnapshotHash,
            reason,
            details)).Request;

    public static async Task<PendingTrainingShowcaseRequestWriteResult> WriteRequestWithStatusAsync(
        FileSystemManager fs,
        string requestKind,
        string sourceActorId,
        string sourceActorName,
        string sourceActorKind,
        string realm,
        int createdAtTurn,
        string? sourceActorSnapshotHash,
        string reason,
        JsonObject? details)
    {
        await RequestWriteGate.WaitAsync();
        try
        {
            var existing = await ReadRequestsAsync(fs);
            var dedupeKey = GetRequestDedupeKey(details);
            var alreadyPending = existing.FirstOrDefault(request =>
                string.Equals(request.SourceActorId, sourceActorId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(request.RequestKind, requestKind, StringComparison.OrdinalIgnoreCase) &&
                (string.IsNullOrWhiteSpace(dedupeKey) ||
                 string.Equals(GetRequestDedupeKey(request.Details), dedupeKey, StringComparison.OrdinalIgnoreCase)));
            if (alreadyPending != null)
                return new PendingTrainingShowcaseRequestWriteResult(alreadyPending, CreatedByThisCall: false);

            var request = new PendingTrainingShowcaseRequest(
                $"training_showcase_req_{Guid.NewGuid():N}",
                requestKind,
                sourceActorId,
                sourceActorName,
                sourceActorKind,
                realm,
                createdAtTurn,
                DateTime.UtcNow,
                sourceActorSnapshotHash,
                reason,
                details);

            await WriteRequestsCoreAsync(fs, existing.Concat(new[] { request }).ToArray());
            return new PendingTrainingShowcaseRequestWriteResult(request, CreatedByThisCall: true);
        }
        finally
        {
            RequestWriteGate.Release();
        }
    }

    internal static async Task<PendingTrainingShowcaseRequestWriteResult?> TryWriteScopedRequestAsync(
        FileSystemManager fs,
        string requestKind,
        string sourceActorId,
        string sourceActorName,
        string sourceActorKind,
        string realm,
        int createdAtTurn,
        string? sourceActorSnapshotHash,
        string reason,
        LocalInteractionScope scope,
        JsonObject? details = null)
    {
        await RequestWriteGate.WaitAsync();
        try
        {
            var previousJson = await fs.ReadFileAsync(PendingRequestPath);
            var existing = DeserializeRequests(previousJson);
            var dedupeKey = GetRequestDedupeKey(details);
            var alreadyPending = existing.FirstOrDefault(request =>
                string.Equals(request.SourceActorId, sourceActorId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(request.RequestKind, requestKind, StringComparison.OrdinalIgnoreCase) &&
                (string.IsNullOrWhiteSpace(dedupeKey) ||
                 string.Equals(GetRequestDedupeKey(request.Details), dedupeKey, StringComparison.OrdinalIgnoreCase)));
            if (alreadyPending != null)
            {
                return await CoordinatedStateWriteHelper.TryCommitAsync(
                        fs,
                        CoordinatedStateWriteHelper.CreateAuthorityGuardWrites(scope))
                    ? new PendingTrainingShowcaseRequestWriteResult(alreadyPending, CreatedByThisCall: false)
                    : null;
            }

            var request = new PendingTrainingShowcaseRequest(
                $"training_showcase_req_{Guid.NewGuid():N}",
                requestKind,
                sourceActorId,
                sourceActorName,
                sourceActorKind,
                realm,
                createdAtTurn,
                DateTime.UtcNow,
                sourceActorSnapshotHash,
                reason,
                details);
            var nextJson = JsonSerializer.Serialize(
                new PendingTrainingShowcaseRequestRoot(existing.Concat(new[] { request }).ToArray()),
                JsonOpts);
            var writes = CoordinatedStateWriteHelper.CreateAuthorityGuardWrites(scope)
                .Concat(new[]
                {
                    new CoordinatedStateWriteHelper.PlannedWrite(
                        PendingRequestPath,
                        previousJson,
                        nextJson,
                        RequireCurrentBaseline: true)
                })
                .ToArray();

            return await CoordinatedStateWriteHelper.TryCommitAsync(fs, writes)
                ? new PendingTrainingShowcaseRequestWriteResult(request, CreatedByThisCall: true)
                : null;
        }
        finally
        {
            RequestWriteGate.Release();
        }
    }

    public static async Task WriteRequestsAsync(FileSystemManager fs, IReadOnlyList<PendingTrainingShowcaseRequest> requests)
    {
        await RequestWriteGate.WaitAsync();
        try
        {
            await WriteRequestsCoreAsync(fs, requests);
        }
        finally
        {
            RequestWriteGate.Release();
        }
    }

    public static async Task RemoveRequestsAsync(
        FileSystemManager fs,
        IReadOnlyCollection<PendingTrainingShowcaseRequest> expectedRequests)
    {
        if (expectedRequests.Count == 0)
            return;

        var expectedById = expectedRequests
            .GroupBy(request => request.RequestId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => SerializeRequest(group.First()),
                StringComparer.OrdinalIgnoreCase);

        await RequestWriteGate.WaitAsync();
        try
        {
            var latest = await ReadRequestsAsync(fs);
            var remaining = latest
                .Where(request =>
                    !expectedById.TryGetValue(request.RequestId, out var expectedJson) ||
                    !string.Equals(SerializeRequest(request), expectedJson, StringComparison.Ordinal))
                .ToArray();
            if (remaining.Length != latest.Count)
                await WriteRequestsCoreAsync(fs, remaining);
        }
        finally
        {
            RequestWriteGate.Release();
        }
    }

    private static async Task WriteRequestsCoreAsync(
        FileSystemManager fs,
        IReadOnlyList<PendingTrainingShowcaseRequest> requests)
    {
        if (requests.Count == 0)
        {
            fs.DeleteFile(PendingRequestPath);
            return;
        }

        var root = new PendingTrainingShowcaseRequestRoot(requests);
        await fs.WriteFileAtomicAsync(PendingRequestPath, JsonSerializer.Serialize(root, JsonOpts));
    }

    internal static IReadOnlyList<PendingTrainingShowcaseRequest> ParseRequests(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return Array.Empty<PendingTrainingShowcaseRequest>();

        try
        {
            return JsonSerializer.Deserialize<PendingTrainingShowcaseRequestRoot>(raw, JsonOpts)?.Requests ??
                   Array.Empty<PendingTrainingShowcaseRequest>();
        }
        catch (JsonException)
        {
            return Array.Empty<PendingTrainingShowcaseRequest>();
        }
    }

    private static IReadOnlyList<PendingTrainingShowcaseRequest> DeserializeRequests(string? raw) =>
        ParseRequests(raw);

    private static string SerializeRequest(PendingTrainingShowcaseRequest request) =>
        JsonSerializer.Serialize(request, JsonOpts);

    private static string? GetRequestDedupeKey(JsonObject? details)
    {
        if (details?["dedupeKey"] is JsonValue value && value.TryGetValue<string>(out var text))
            return string.IsNullOrWhiteSpace(text) ? null : text.Trim();

        return null;
    }
}
