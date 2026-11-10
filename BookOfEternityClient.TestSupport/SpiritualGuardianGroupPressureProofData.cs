using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.Tests;

/// <summary>Authors only the closed desired v7 DATA over unchanged lawful prerequisite records; never issues live authority.</summary>
internal static class SpiritualGuardianGroupPressureProofData
{
    /// <summary>The private group role is test input until its production reader is implemented.</summary>
    internal const string Role = "existing_guardian_pressure_failed_action_group_v1";

    /// <summary>Adds a complete comparison to a fresh copy, preserving both original comparison-free source records.</summary>
    /// <param name="fixture">One invocation's unchanged Task1 inputs, including exact ordinary raw identity.</param>
    /// <param name="ordinaryCalculation">The independently authored literal ordinary arithmetic.</param>
    /// <returns>A sealed full source carrying desired v7 DATA, not current/signed evidence or a permission.</returns>
    internal static JsonObject CreateSource(GuardianGroupPressureData fixture, SpiritualWoundCalculation ordinaryCalculation)
    {
        var source = fixture.ActualSource.DeepClone().AsObject();
        source["strainPreimage"] = CreateProof(fixture.Retained.Parents, fixture.Ordinary,
            fixture.OriginalInput, ordinaryCalculation);
        SpiritualExistingPressureTestData.Seal(source);
        return source;
    }

    /// <summary>Serializes all independently supplied parents in their original canonical order, without selecting a subset.</summary>
    /// <param name="parents">Complete pre-sign authored envelopes; this method does not authenticate their availability.</param>
    /// <param name="ordinary">The full independently authored ordinary exchange.</param>
    /// <param name="originalInput">Exact raw conflict path, presence, fingerprint and exchange pointer.</param>
    /// <param name="ordinaryCalculation">Independent literal ordinary pressure and maximum.</param>
    /// <returns>The six-field closed group comparison with three fields per parent.</returns>
    internal static JsonObject CreateProof(IReadOnlyList<JsonObject> parents, JsonObject ordinary,
        JsonObject originalInput, SpiritualWoundCalculation ordinaryCalculation) => new()
    {
        ["version"] = 7,
        ["inputRoleRevision"] = Role,
        ["ordinaryExchangeJsonBase64"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(ordinary.ToJsonString())),
        ["originalInput"] = originalInput.DeepClone(),
        ["parents"] = new JsonArray(parents.Select(parent => (JsonNode)new JsonObject
        {
            ["parentWoundJsonBase64"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(parent.ToJsonString())),
            ["effectId"] = parent["consequences"]!["ownedEffectSources"]!["rootBindings"]![0]!["effectId"]!.GetValue<string>(),
            ["componentId"] = "component_001"
        }).ToArray()),
        ["ordinaryCalculation"] = new JsonObject
        {
            ["traumaPressure"] = ordinaryCalculation.TraumaPressure,
            ["maximumSeverityRank"] = ordinaryCalculation.MaximumSeverityRank
        }
    };
}
