using System.Text.Json;

namespace BookOfEternityClient.Services.GmWorkers;

internal static class AfterlifeRealmAuthorityContract
{
    internal const string StatePath = "game_state/meta/soul_state.json";

    internal static bool IsAfterlifeStatePath(string? path) =>
        !string.IsNullOrWhiteSpace(path) &&
        path.Replace('\\', '/').StartsWith("game_state/meta/", StringComparison.OrdinalIgnoreCase);

    internal static bool TryRead(
        string? json,
        out WorkerAfterlifeRealmGate realmGate,
        out string currentRealm,
        out string error)
    {
        realmGate = WorkerAfterlifeRealmGate.None;
        currentRealm = string.Empty;
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(json))
        {
            error = $"Afterlife realm authority is missing: {StatePath}.";
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                error = $"Afterlife realm authority must be one JSON object: {StatePath}.";
                return false;
            }

            if (TryFindDuplicateProperty(document.RootElement, string.Empty, out var duplicatePath))
            {
                error = $"Afterlife realm authority contains duplicate property {duplicatePath}: {StatePath}.";
                return false;
            }

            if (!document.RootElement.TryGetProperty("currentRealm", out var realmElement) ||
                realmElement.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(realmElement.GetString()))
            {
                error = $"Afterlife realm authority requires one non-empty string currentRealm: {StatePath}.";
                return false;
            }

            var realm = realmElement.GetString()!.Trim();
            if (RealmSemantics.IsChaosSea(realm))
            {
                realmGate = WorkerAfterlifeRealmGate.ChaosSea;
                currentRealm = "Chaos Sea";
                return true;
            }

            if (RealmSemantics.IsShiningRealm(realm))
            {
                realmGate = WorkerAfterlifeRealmGate.ShiningAbode;
                currentRealm = "Shining Abode";
                return true;
            }

            error = $"Afterlife realm authority currentRealm is unsupported for repair workers: {StatePath}.";
            return false;
        }
        catch (JsonException exception)
        {
            error = $"Afterlife realm authority is malformed at {StatePath}: {exception.Message}";
            return false;
        }
    }

    private static bool TryFindDuplicateProperty(
        JsonElement value,
        string path,
        out string duplicatePath)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
            {
                var propertyPath = string.IsNullOrEmpty(path)
                    ? property.Name
                    : $"{path}.{property.Name}";
                if (!names.Add(property.Name))
                {
                    duplicatePath = propertyPath;
                    return true;
                }

                if (TryFindDuplicateProperty(property.Value, propertyPath, out duplicatePath))
                    return true;
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var item in value.EnumerateArray())
            {
                if (TryFindDuplicateProperty(item, $"{path}[{index++}]", out duplicatePath))
                    return true;
            }
        }

        duplicatePath = string.Empty;
        return false;
    }
}
