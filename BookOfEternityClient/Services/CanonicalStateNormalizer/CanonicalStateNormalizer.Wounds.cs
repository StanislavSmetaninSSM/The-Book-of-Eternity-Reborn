using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

public partial class CanonicalStateNormalizer
{
    private static readonly string[] WoundPublicationCarrierPaths =
    {
        WoundCarrierCatalog.PlayerPath,
        WoundCarrierCatalog.NpcPath,
        WoundCarrierCatalog.EnemiesPath,
        WoundCarrierCatalog.AlliesPath,
        WoundCarrierCatalog.AfterlifeProfilesPath
    };

    private static void AddWoundPublicationWrites(
        AcceptedMechanicsPlan plan,
        Dictionary<string, JsonObject> writes)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(writes);
        foreach (var pair in ComposeWoundPublicationWrites(plan))
            AddWoundPublicationWrite(writes, pair.Key, pair.Value);
    }

    internal static IReadOnlyDictionary<string, JsonObject>
        ComposeWoundPublicationWrites(AcceptedMechanicsPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var result = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        if (plan.WoundStageBundle is null || plan.AwaitsPendingResolution)
            return result;

        foreach (var pair in plan.WoundCarrierAfterImages.OrderBy(
                     static pair => pair.Key,
                     StringComparer.Ordinal))
        {
            AddWoundPublicationWrite(result, pair.Key, pair.Value);
        }
        AddWoundPublicationWrite(
            result,
            WoundIdentityState.StatePath,
            plan.WoundIdentityAfterImage ?? throw new InvalidDataException(
                "A complete wound publication has no identity after-image."));
        AddWoundPublicationWrite(
            result,
            WoundHistoryState.HistoryPath,
            plan.WoundHistoryAfterImage ?? throw new InvalidDataException(
                "A complete wound publication has no history after-image."));
        return result;
    }

    private static void AddWoundPublicationWrite(
        Dictionary<string, JsonObject> writes,
        string path,
        JsonObject afterImage)
    {
        if (!writes.TryAdd(path, afterImage))
        {
            throw new InvalidDataException(
                $"Accepted wound publication at '{path}' conflicts with another whole-root writer.");
        }
    }

    private async Task ValidatePublishedWoundAfterImagesAsync(
        AcceptedMechanicsPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (plan.WoundStageBundle is null)
            return;
        var writeLease = _writeLease ?? throw new InvalidOperationException(
            "Accepted wound read-back requires the owning canonical write lease.");
        var pendingOnly = plan.AwaitsPendingResolution;
        var plannedCarriers = plan.WoundCarrierAfterImages;
        var carrierJson = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var path in WoundPublicationCarrierPaths)
        {
            carrierJson[path] = !pendingOnly &&
                plannedCarriers.TryGetValue(path, out var planned)
                    ? await ReadExactPublishedAfterImageAsync(path, planned)
                    : await ReadExactRetainedWoundBeforeImageAsync(plan, path);
        }

        string identityJson;
        string historyJson;
        if (pendingOnly)
        {
            identityJson = await RequireRetainedWoundJsonAsync(
                plan,
                WoundIdentityState.StatePath);
            historyJson = await RequireRetainedWoundJsonAsync(
                plan,
                WoundHistoryState.HistoryPath);
            _ = await RequireRetainedWoundJsonAsync(
                plan,
                AcceptedMechanicsPlan.WoundCommandPath);
        }
        else
        {
            identityJson = await ReadExactPublishedAfterImageAsync(
                WoundIdentityState.StatePath,
                plan.WoundIdentityAfterImage ?? throw new InvalidDataException(
                    "A complete wound publication has no identity after-image."));
            historyJson = await ReadExactPublishedAfterImageAsync(
                WoundHistoryState.HistoryPath,
                plan.WoundHistoryAfterImage ?? throw new InvalidDataException(
                    "A complete wound publication has no history after-image."));
            if (_fs.FileExists(writeLease, AcceptedMechanicsPlan.WoundCommandPath))
            {
                throw new InvalidDataException(
                    "Accepted wound publication left the consumed wound command root published.");
            }
        }

        var carriers = new WoundCarrierCatalogInput(
            ParsePublishedWoundCarrier(
                carrierJson[WoundCarrierCatalog.PlayerPath],
                WoundCarrierCatalog.PlayerPath),
            ParsePublishedWoundCarrier(
                carrierJson[WoundCarrierCatalog.NpcPath],
                WoundCarrierCatalog.NpcPath),
            ParsePublishedWoundCarrier(
                carrierJson[WoundCarrierCatalog.EnemiesPath],
                WoundCarrierCatalog.EnemiesPath),
            ParsePublishedWoundCarrier(
                carrierJson[WoundCarrierCatalog.AlliesPath],
                WoundCarrierCatalog.AlliesPath),
            ParsePublishedWoundCarrier(
                carrierJson[WoundCarrierCatalog.AfterlifeProfilesPath],
                WoundCarrierCatalog.AfterlifeProfilesPath));
        var catalog = WoundCarrierCatalog.Build(carriers);
        var identity = WoundIdentityState.Parse(
            identityJson,
            WoundIdentityState.StatePath);
        var history = WoundHistoryState.Parse(
            historyJson,
            WoundHistoryState.HistoryPath);
        var issues = catalog.Issues
            .Concat(identity.Issues)
            .Concat(history.Issues)
            .ToList();
        if (catalog.Issues.Count == 0 &&
            identity.State is not null &&
            history.State is not null)
        {
            issues.AddRange(history.State.ValidateAgreement(identity.State, catalog));
        }
        if (issues.Count != 0 || identity.State is null || history.State is null)
        {
            throw new InvalidDataException(
                "Accepted wound publication failed canonical read-back agreement: " +
                string.Join(
                    ", ",
                    issues.Select(static issue => issue.Code ?? issue.FilePath)));
        }
    }

    private async Task<string> RequireRetainedWoundJsonAsync(
        AcceptedMechanicsPlan plan,
        string path)
    {
        return await ReadExactRetainedWoundBeforeImageAsync(plan, path) ??
            throw new InvalidDataException(
                $"Pending wound publication lost retained authority at '{path}'.");
    }

    private async Task<string?> ReadExactRetainedWoundBeforeImageAsync(
        AcceptedMechanicsPlan plan,
        string path)
    {
        if (!plan.BeforeImages.TryGetValue(path, out var expected))
        {
            throw new InvalidDataException(
                $"Accepted wound publication has no before-image for retained path '{path}'.");
        }
        var current = await _fs.ReadFileBytesAsync(_writeLease!, path);
        var exact = expected.Existed == (current != null) &&
                    (expected.Bytes == null
                        ? current == null
                        : current != null &&
                          expected.Bytes.AsSpan().SequenceEqual(current));
        if (!exact)
        {
            throw new InvalidDataException(
                $"Accepted wound retained authority at '{path}' changed during publication.");
        }
        return current == null ? null : DecodeAcceptedMechanicsUtf8(current);
    }

    private static JsonObject? ParsePublishedWoundCarrier(
        string? json,
        string path)
    {
        if (json is null)
            return null;
        try
        {
            return JsonNode.Parse(json) as JsonObject ??
                throw new InvalidDataException(
                    $"Accepted wound carrier at '{path}' is not a JSON object.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                $"Accepted wound carrier at '{path}' is not valid JSON.",
                exception);
        }
    }
}
