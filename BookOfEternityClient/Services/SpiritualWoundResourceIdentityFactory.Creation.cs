using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal sealed partial class SpiritualWoundResourceIdentityFactory
{
    /// <inheritdoc/>
    internal override string CreateDefinitionId(ResourceDefinitionCreationCommand creation, int turn) =>
        Allocate("resource_definition", "resource_definition_creation", () => DefinitionKey(creation, turn),
            () => _underlying.CreateDefinitionId(creation, turn));

    /// <inheritdoc/>
    internal override string CreateDefinitionSeal(ResourceDefinitionCreationCommand creation, int turn) =>
        Allocate("resource_definition_seal", "resource_definition_creation", () => DefinitionKey(creation, turn),
            () => _underlying.CreateDefinitionSeal(creation, turn));

    /// <inheritdoc/>
    internal override string CreatePendingRequestId(ResourcePendingResolutionDraft draft) =>
        Allocate("resource_resolution", "resource_pending_request",
            () => ResourcePendingResolutionState.DetachDraft(draft),
            () => _underlying.CreatePendingRequestId(ResourcePendingResolutionState.DetachDraft(draft)));

    /// <inheritdoc/>
    internal override DateTimeOffset GetPendingCreatedAtUtc(IReadOnlyList<ResourcePendingResolutionDraft> drafts)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(drafts);
            var frozen = drafts.Select(ResourcePendingResolutionState.DetachDraft).ToArray();
            var key = new JsonObject { ["drafts"] = JsonSerializer.SerializeToNode(frozen) };
            var value = _journal.Request("utc_time", "resources",
                SpiritualWoundStateJson.Hash(key, "resource_pending_batch"),
                () => _underlying.GetPendingCreatedAtUtc(
                    frozen.Select(ResourcePendingResolutionState.DetachDraft).ToArray())
                    .ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
            return DateTimeOffset.ParseExact(value, "O", CultureInfo.InvariantCulture);
        }
        catch
        {
            _journal.Invalidate();
            throw;
        }
    }

    /// <summary>
    /// Explicitly projects the admitted command because its CLR properties are not public serializer inputs.
    /// </summary>
    /// <param name="creation">
    /// Non-null command with its detached definition getter.
    /// </param>
    /// <param name="turn">
    /// Positive original turn retained by the capture.
    /// </param>
    /// <returns>
    /// Complete stable definition-allocation comparison key.
    /// </returns>
    private static JsonObject DefinitionKey(ResourceDefinitionCreationCommand creation, int turn)
    {
        ArgumentNullException.ThrowIfNull(creation);
        ArgumentOutOfRangeException.ThrowIfLessThan(turn, 1);
        return new JsonObject
        {
            ["turn"] = turn, ["commandOrdinal"] = creation.CommandOrdinal,
            ["eventRef"] = creation.EventRef, ["definitionRef"] = creation.DefinitionRef,
            ["definition"] = creation.Definition
        };
    }
}
