using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    private async Task ValidateElyaraVisitOutcomeAsync(string action, List<ValidationIssue> issues)
    {
        if (!ElyaraVisitService.HasMarker(action)) return;
        FileSystemManager.CanonicalWriteLease? owned = null;
        try
        {
            var invocation = _pendingSnapshotInvocation.Value;
            invocation?.RequireCurrent();
            var lease = invocation?.Physical?.Lease ?? (owned = await _fs.AcquireCanonicalWriteLeaseAsync());
            _fs.VerifyCurrentSessionOperation(lease);
            var intent = ElyaraVisitService.ParseAction(action);
            if (!_fs.IsCurrentSessionGeneration(lease, intent.Generation))
                throw new InvalidDataException("The accepted visit belongs to a replaced session.");
            // Reuse the existing authenticated lookup/current WorldRead owner;
            // do not launch a parallel full original-reader verification pass.
            var lookup = await LoadValidatedPendingTurnSnapshotLookupAsync(lease);
            if (lookup.Status != ValidatedPendingTurnSnapshotStatus.Usable || lookup.Manifest is not { } manifest ||
                manifest.PlayerAction != action)
                throw new InvalidDataException("The visit lacks its current authenticated original action.");
            var originals = new JsonObject?[ElyaraVisitService.SourcePaths.Count];
            for (var index = 0; index < ElyaraVisitService.SourcePaths.Count; index++)
            {
                var path = ElyaraVisitService.SourcePaths[index];
                if (!manifest.Files.ContainsKey(path))
                {
                    if (manifest.OriginalPathPresenceV1?.TryGetValue(path, out var existed) != true || existed)
                        throw new InvalidDataException("The original visit source has no authenticated presence/absence: " + path);
                    continue;
                }
                var json = await ReadValidatedPendingTurnSnapshotFileAsync(manifest, path,
                    CurrentWorldReadPhase() is null ? lease : null);
                originals[index] = StrictJsonAuthority.Deserialize<JsonObject>(json ?? throw new InvalidDataException("Missing original visit source."),
                    new JsonSerializerOptions(), path) ?? throw new InvalidDataException("Invalid original visit source object.");
            }
            var original = new ElyaraVisitInput(originals[0], originals[1], originals[2], originals[3], originals[4]);
            JsonObject Current(string path) => StrictJsonAuthority.Deserialize<JsonObject>(
                ReadCurrentTrackedFileSync(path) ?? throw new InvalidDataException("Missing current arrival source."),
                new JsonSerializerOptions(), path) ?? throw new InvalidDataException("Invalid arrival source object.");
            var current = new ElyaraVisitInput(Current(ElyaraVisitService.SourcePaths[0]), Current(ElyaraVisitService.SourcePaths[1]),
                Current(ElyaraVisitService.SourcePaths[2]), null, null);
            ElyaraVisitService.RequireAccepted(intent, original, current);
            invocation?.RequireCurrent();
        }
        catch (Exception error) when (error is not CoordinatedStatePublicationUncertainException &&
            error is (IOException or InvalidDataException or UnauthorizedAccessException or JsonException or InvalidOperationException or ArgumentException))
        {
            issues.Add(new ValidationIssue("input/turn_request.json.playerAction", IssueSeverity.Error,
                "Переход к Элиаре не соответствует исходной доступной цели или текущему прибытию.",
                code: "elyara_visit_original_or_arrival_invalid", section: "ELYARA_VISIT_V1",
                expected: "same alive canonical Elyara and ordinary destination proved by current authenticated pre-turn sources",
                actual: error.Message, repairHint: "Сохрани исходную жизнь/локацию Элиары, не подменяй pre-turn доказательство текущим профилем. Не списывай плату и не начинай активное лечение при переходе."));
        }
        finally { if (owned is not null) await owned.DisposeAsync(); }
    }
}
