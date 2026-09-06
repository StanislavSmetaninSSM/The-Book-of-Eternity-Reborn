using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static partial class WoundResponseInputComposer
{
    internal static WoundWorkingOwnedGraph ConvertTreatmentComplicationGraph(
        MortalWoundComplicationProposalDraft draft, string owningWoundId,
        WoundWorkingReferenceOrigin origin, WoundWorkingOperationAddress address)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (origin is not (WoundWorkingReferenceOrigin.DirectAddition or WoundWorkingReferenceOrigin.PolicyAddition) ||
            address.ResultIndex < 0 || address.OperationIndex < 0 ||
            !ResourceMaterializationContract.IsExactIdentifier(owningWoundId) || draft.ConsequenceDefinitions.IsDefault)
            throw new ArgumentException("A complication graph requires an exact symbolic operation namespace.");
        var complication = draft.Complication;
        var reference = new WoundWorkingReference(origin, address, complication.ComplicationRef);
        var definitions = ImmutableArray.CreateBuilder<WoundWorkingDefinition>();
        var roots = ImmutableArray.CreateBuilder<WoundWorkingRoot>();
        var entries = ImmutableArray.CreateBuilder<WoundWorkingEntry>();
        var owned = ImmutableArray.CreateBuilder<WoundWorkingReference>();
        var issues = new List<ValidationIssue>();
        var nextSlot = 1;
        foreach (var proposal in draft.ConsequenceDefinitions)
        {
            if (!ResourceMaterializationContract.IsExactIdentifier(proposal.DefinitionRef))
                throw new ArgumentException("A definition requires one exact local reference.");
            var definition = ConvertProposalDefinition(proposal.Definition, owningWoundId,
                "mortalWoundTreatment.complicationDraft.consequenceDefinitions", issues, out _);
            if (definition is null || issues.Count != 0)
                throw new ArgumentException("The complication definition is not proposal-safe.");
            var definitionRef = new WoundWorkingReference(origin, address, proposal.DefinitionRef);
            definitions.Add(new(definitionRef, JsonSerializer.SerializeToElement(BindDefinitionToLocalWound(definition, owningWoundId))));
            if (proposal.Root is not { } root) continue;
            if (root.OwnershipKind != "complication" || root.ComplicationRef != complication.ComplicationRef)
                throw new ArgumentException("Every added root must belong to the exact added complication.");
            roots.Add(new(definitionRef, definition["definitionKey"]!.GetValue<string>()));
            owned.Add(definitionRef);
            foreach (var slot in ConvertProposalRootSlots(root.Slots, ref nextSlot))
                entries.Add(new(slot.Slot, slot.ProfileKey, definitionRef, slot.ReadableSummary));
        }
        return new(ImmutableArray.Create(new WoundWorkingComplication(reference, complication.Kind,
            complication.State, complication.DisplayName, complication.TreatmentDifficultyModifier,
            owned.ToImmutable(), complication.Visibility)), definitions.ToImmutable(), roots.ToImmutable(), entries.ToImmutable());
    }

    private static JsonObject? ConvertProposalDefinition(JsonElement proposal, string owningWoundId,
        string path, ICollection<ValidationIssue> issues, out string? definitionKey)
    {
        definitionKey = null;
        if (proposal.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("A complete definition object is required.");
        var definition = JsonNode.Parse(proposal.GetRawText())!.AsObject();
        if (definition["definitionKey"] is not JsonValue key || !key.TryGetValue<string>(out var value) ||
            !ResourceMaterializationContract.IsExactIdentifier(value))
        {
            Add(issues, path + ".definition.definitionKey", "wound_response_invalid_definition",
                "one exact definitionKey", "missing or malformed definitionKey");
            return null;
        }
        definitionKey = value;
        if (definition["links"] is not JsonArray links || links.Count != 0)
        {
            Add(issues, path + ".definition.links", "wound_response_client_authority_forbidden",
                "exact empty links array; the client binds the wound source", definition["links"]?.ToJsonString() ?? "missing");
            return null;
        }
        BindLocalWoundMarkers(definition, owningWoundId, path, issues);
        return definition;
    }

    private static ImmutableArray<WoundEffectSlotAgreement> ConvertProposalRootSlots(
        ImmutableArray<WoundConsequenceSlotProposalDraft> slots, ref int nextSlot)
    {
        if (slots.IsDefault) throw new ArgumentException("Root slots must be explicitly declared.");
        var result = ImmutableArray.CreateBuilder<WoundEffectSlotAgreement>();
        foreach (var slot in slots)
        {
            result.Add(new(nextSlot, slot.ProfileKey, slot.ReadableSummary));
            nextSlot = checked(nextSlot + 1);
        }
        return result.ToImmutable();
    }

    private static void BindLocalWoundMarkers(
        JsonObject definition,
        string localWoundRef,
        string path,
        ICollection<ValidationIssue> issues)
    {
        if (definition["components"] is not JsonArray components)
            return;
        foreach (var component in components.OfType<JsonObject>())
        {
            if (component["profile"] is not JsonValue profileNode ||
                !profileNode.TryGetValue<string>(out var profile) ||
                !string.Equals(profile, "wound_consequence", StringComparison.Ordinal))
            {
                continue;
            }
            if (component["payload"] is not JsonObject payload)
                continue;
            if (payload.ContainsKey("woundId"))
            {
                Add(
                    issues,
                    path + ".definition.components[].payload.woundId",
                    "wound_response_client_authority_forbidden",
                    "no GM-authored woundId",
                    payload["woundId"]?.ToJsonString() ?? "null");
                continue;
            }
            payload["woundId"] = localWoundRef;
        }
    }

    private static JsonObject BindDefinitionToLocalWound(
        JsonObject definition,
        string localWoundRef)
    {
        var result = definition.DeepClone().AsObject();
        result["links"]!.AsArray().Add(new JsonObject
        {
            ["kind"] = "wound",
            ["targetId"] = localWoundRef,
            ["role"] = "source"
        });
        return result;
    }
}
