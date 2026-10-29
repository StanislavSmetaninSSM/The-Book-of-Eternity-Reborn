using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    // Ready-turn admission, independent of pending closure/capstone presence.
    // Reuse authenticated original/current read views; comparison DATA never grants travel.
    internal async Task ValidateShiningCurrentHallContinuityAsync(List<ValidationIssue> issues)
    {
        try
        {
            var invocation = _pendingSnapshotInvocation.Value;
            invocation?.RequireCurrent();
            var lookup = await LoadValidatedPendingTurnSnapshotLookupAsync();
            if (lookup.Status != ValidatedPendingTurnSnapshotStatus.Usable || lookup.Manifest is not { } manifest)
                throw new InvalidDataException("Current hall preservation requires an authenticated original turn.");
            const string path = ShiningAbodeState.StatePath;
            string? originalText = null;
            if (manifest.Files.ContainsKey(path))
                originalText = await ReadValidatedPendingTurnSnapshotFileAsync(manifest, path)
                    ?? throw new InvalidDataException("Missing original Shining image.");
            else if (manifest.OriginalPathPresenceV1?.TryGetValue(path, out var existed) != true || existed)
                throw new InvalidDataException("Original Shining presence/absence is not authenticated.");
            JsonObject? Parse(string? text) => text is null ? null :
                StrictJsonAuthority.Deserialize<JsonObject>(text, new JsonSerializerOptions(), path)
                    ?? throw new InvalidDataException("Invalid Shining owner object.");
            var original = Parse(originalText);
            var current = Parse(await ReadSameTurnOwnerCurrentTextAsync(path));
            if (!ShiningCurrentHallContract.Preserves(original, current))
                throw new InvalidDataException("Ordinary GM output cannot add, remove or replace currentHallId.");
            invocation?.RequireCurrent();
        }
        catch (Exception error) when (error is not CoordinatedStatePublicationUncertainException &&
            error is (IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or ArgumentException))
        {
            issues.Add(new ValidationIssue(ShiningAbodeState.StatePath + ".currentHallId", IssueSeverity.Error,
                "Текущий зал не совпадает с подтверждённым исходным состоянием.",
                code: "shining_current_hall_changed_without_authority", section: "ShiningAbode",
                expected: "authenticated original currentHallId presence/value preserved; progression and closures grant no movement",
                actual: error.Message, repairHint: "Сохрани исходный currentHallId. Отчёт цикла, лечение и political closure не разрешают перемещение между залами."));
        }
    }
}
