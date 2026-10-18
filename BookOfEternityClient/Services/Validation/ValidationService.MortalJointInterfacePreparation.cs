using System.Text.Json;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    // Joint-only prepare-before-write adapter. The ordinary public API retains
    // its original separately owned write-before-tail behavior.
    internal async Task<MortalJointInterfacePublication.ComputedResult> PrepareMortalJointInterfaceAsync(
        FileSystemManager.CanonicalWriteLease lease)
    {
        _fs.EnsureCanonicalWriteLeaseActive(lease);
        var issues = new List<ValidationIssue>();
        const string path = MortalJointInterfacePublication.InterfacePath;
        await ValidateAcceptedTurnTransientOutputFreshnessAsync(path, "InterfaceUpdates",
            "accepted_turn_stale_interface_updates",
            "output/interface_updates.json must be freshly rewritten for the current accepted turn",
            "Не переиспользуй старый output/interface_updates.json. Перезапиши interface_updates.json заново для текущего accepted turn, даже если в нём только dialogueOptions или image_prompt.", issues);

        // Physical bytes are captured under this actual writer's lease, before
        // the ordinary public read and its observer. A same-Main borrowed write
        // in that observer cannot be hidden by the eventual projected image.
        var before = await _fs.ReadFileBytesAsync(lease, path);
        if (before is null)
            return new(null, null, "Absent", false, null, "Absent", "NotReached", issues);
        var json = await _fs.ReadFileAsync(path);
        if (json != FileSystemManager.DecodeFileBytes(before) ||
            !MortalJointInterfacePublication.BytesEqual(before, await _fs.ReadFileBytesAsync(lease, path)))
            throw new InvalidDataException("joint_interface_consumed_read_preimage_changed");
        if (string.IsNullOrWhiteSpace(json))
        {
            issues.Add(new ValidationIssue(path, IssueSeverity.Error,
                "Если output/interface_updates.json записан, он должен быть непустым JSON object с dialogueOptions и/или image_prompt",
                code: "accepted_turn_empty_interface_updates", section: "InterfaceUpdates",
                expected: "non-empty JSON object with dialogueOptions and/or image_prompt", actual: "empty file",
                repairHint: "Либо удали output/interface_updates.json, если интерфейсных обновлений в этом ходу нет, либо запиши в него dialogueOptions и/или image_prompt текущего accepted turn."));
            return new(before, before, "NoWrite", false, null, "Blank", "NotReached", issues);
        }

        var requiresFallback = AcceptedTurnOutputProjector.RequiresInterfaceTimestampFallback(json);
        DateTimeOffset? fallback = requiresFallback ? DateTimeOffset.UtcNow : null;
        var projected = AcceptedTurnOutputProjector.ProjectInterface(json, fallback);
        var after = projected.Changed ? FileSystemManager.EncodeUtf8WithPreamble(projected.Json) : before;
        var document = "InvalidJson"; var timestamp = "NotReached";
        try
        {
            using var doc = JsonDocument.Parse(projected.Json);
            document = doc.RootElement.ValueKind == JsonValueKind.Object ? "Object" : "NonObject";
            if (EvaluateAcceptedInterfacePrefix(doc.RootElement, path, issues))
            {
                var count = issues.Count;
                ValidateRequiredIsoTimestampField(doc.RootElement, path, issues, "timestamp", "InterfaceUpdates",
                    "interface_updates_missing_timestamp", "interface_updates_invalid_timestamp",
                    "Добавь в output/interface_updates.json поле timestamp в ISO 8601 формате вместе с dialogueOptions и/или image_prompt текущего accepted turn.");
                // Observe the result of that one real ambient-policy call. Do not
                // parse again to populate DATA (especially on resume/cold).
                timestamp = issues.Count == count ? "ParseAccepted" :
                    issues[count].Code == "interface_updates_missing_timestamp" ? "MissingOrWrongKind" : "ParseRejected";
                EvaluateAcceptedInterfaceTail(doc.RootElement, path, issues);
            }
        }
        catch (JsonException error)
        {
            issues.Add(new ValidationIssue(path, IssueSeverity.Error,
                $"output/interface_updates.json должен быть валидным JSON: {error.Message}",
                code: "accepted_turn_invalid_interface_updates_json", section: "InterfaceUpdates",
                expected: "valid JSON object with dialogueOptions/image_prompt and timestamp", actual: "invalid JSON",
                repairHint: "Исправь output/interface_updates.json и оставь в нём валидный JSON-объект с dialogueOptions и/или image_prompt вместе с timestamp."));
        }
        return new(before, after, projected.Changed ? "Changed" : "NoWrite", requiresFallback,
            fallback?.ToString("O"), document, timestamp, issues);
    }
}
