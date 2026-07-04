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
    {
        var existing = await ReadRequestsAsync(fs);
        var dedupeKey = GetRequestDedupeKey(details);
        var alreadyPending = existing.FirstOrDefault(request =>
            string.Equals(request.SourceActorId, sourceActorId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(request.RequestKind, requestKind, StringComparison.OrdinalIgnoreCase) &&
            (string.IsNullOrWhiteSpace(dedupeKey) ||
             string.Equals(GetRequestDedupeKey(request.Details), dedupeKey, StringComparison.OrdinalIgnoreCase)));
        if (alreadyPending != null)
            return alreadyPending;

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

        await WriteRequestsAsync(fs, existing.Concat(new[] { request }).ToArray());
        return request;
    }

    public static async Task WriteRequestsAsync(FileSystemManager fs, IReadOnlyList<PendingTrainingShowcaseRequest> requests)
    {
        if (requests.Count == 0)
        {
            fs.DeleteFile(PendingRequestPath);
            return;
        }

        var root = new PendingTrainingShowcaseRequestRoot(requests);
        await fs.WriteFileAtomicAsync(PendingRequestPath, JsonSerializer.Serialize(root, JsonOpts));
    }

    private static string? GetRequestDedupeKey(JsonObject? details)
    {
        if (details?["dedupeKey"] is JsonValue value && value.TryGetValue<string>(out var text))
            return string.IsNullOrWhiteSpace(text) ? null : text.Trim();

        return null;
    }
}
