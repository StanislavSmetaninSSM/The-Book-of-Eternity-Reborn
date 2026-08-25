using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal sealed record AfterlifeGachaAttemptProjection(
    string Realm,
    string ResourceOwnerId,
    string ReturnCycleId,
    decimal Current,
    decimal Maximum,
    string ResourceStateFingerprint,
    string ResourceHistoryFingerprint);

internal sealed record AfterlifeGachaAttemptProjectionResult(
    AfterlifeGachaAttemptProjection? Projection,
    string? Error)
{
    internal bool IsValid => Projection != null && Error == null;
}

internal static class AfterlifeGachaAttemptProjectionService
{
    internal static AfterlifeGachaAttemptProjectionResult ResolveShining(
        string? definitionsJson,
        string? stateJson,
        string? historyJson,
        JsonObject shiningRoot)
    {
        ArgumentNullException.ThrowIfNull(shiningRoot);
        var definitions = ResourceDefinitionCatalog.ParseCanonical(
            definitionsJson,
            allowMissingPristine: false);
        if (!definitions.IsValid || definitions.Catalog == null)
            return Failure("Shining gacha resource ledger definitions are missing or invalid.");
        var state = ResourceStateContract.ParseCanonical(
            stateJson,
            definitions.Catalog,
            allowMissingPristine: false);
        if (!state.IsValid || state.Ledger == null)
            return Failure("Shining gacha resource ledger state is missing or invalid.");
        var history = ResourceHistoryState.ParseCanonical(
            historyJson,
            definitions.Catalog,
            allowMissingPristine: false);
        if (!history.IsValid || history.History == null)
            return Failure("Shining gacha resource ledger history is missing or invalid.");
        if (history.History.ValidateStateAgreement(state.Ledger).Count != 0)
            return Failure("Shining gacha resource ledger and immutable history disagree.");

        if (shiningRoot["gachaSystem"] is not JsonObject gacha ||
            !TryReadExact(gacha["currentReturnCycleId"], out var returnCycleId) ||
            shiningRoot["resourceOwnerBindings"]?["gachaReturn"] is not JsonObject binding ||
            !TryReadExact(binding["resourceOwnerId"], out var ownerId) ||
            !TryReadExact(binding["returnCycleId"], out var boundCycleId) ||
            !string.Equals(returnCycleId, boundCycleId, StringComparison.Ordinal))
        {
            return Failure("Shining gacha resource ledger binding is missing or stale for the current return cycle.");
        }

        var coordinate = new ResourceCoordinate(
            "shining_abode",
            ResourceOwnerKind.AfterlifeScope,
            ownerId,
            "gacha_attempts");
        if (!state.Ledger.TryResolveExact(coordinate, out var entry) ||
            entry == null ||
            entry.State != ResourceLifecycleState.Active ||
            entry.Current < 0m ||
            entry.Current > entry.Maximum ||
            decimal.Truncate(entry.Current) != entry.Current ||
            decimal.Truncate(entry.Maximum) != entry.Maximum)
        {
            return Failure("Shining gacha resource ledger has no exact active gacha_attempts entry for the current return cycle.");
        }

        return new AfterlifeGachaAttemptProjectionResult(
            new AfterlifeGachaAttemptProjection(
                coordinate.Realm,
                ownerId,
                returnCycleId,
                entry.Current,
                entry.Maximum,
                state.Ledger.Fingerprint,
                history.History.Fingerprint),
            null);
    }

    private static bool TryReadExact(JsonNode? node, out string value)
    {
        value = string.Empty;
        if (node is not JsonValue scalar ||
            !scalar.TryGetValue<string>(out var text) ||
            !ResourceMaterializationContract.IsExactIdentifier(text))
        {
            return false;
        }
        value = text;
        return true;
    }

    private static AfterlifeGachaAttemptProjectionResult Failure(string error) =>
        new(null, error);
}
