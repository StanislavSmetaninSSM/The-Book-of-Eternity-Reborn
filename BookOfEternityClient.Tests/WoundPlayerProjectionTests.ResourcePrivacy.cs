using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class WoundPlayerProjectionTests
{
    /// <summary>
    /// A known foreign wound does not make its owner's private resource requirements public.
    /// Current typed target DATA permits a card only, not discovery, treatment or publication.
    /// </summary>
    [Theory]
    [InlineData(false, "owner_visible", true)]
    [InlineData(true, "owner_visible", false)]
    [InlineData(false, "player_visible", true)]
    [InlineData(true, "player_visible", true)]
    [InlineData(false, "hidden", false)]
    [InlineData(true, "hidden", false)]
    [InlineData(false, "gm_only", false)]
    [InlineData(true, "gm_only", false)]
    public void KnownPhysicalRequirements_ResourcePrivacyUsesPlayerAudience(
        bool foreignTarget, string visibility, bool readable)
    {
        const string resourceKey = "private_requirement_essence";
        const string resourceName = "Личный запас лунной эссенции";
        const int quantity = 731;
        var resources = ResourceDefinitionCatalog.CreateBuiltIn();
        var proposal = resources.ToCanonicalRoot()["definitions"]!.AsArray().OfType<JsonObject>()
            .Single(d => d["resourceKey"]!.GetValue<string>() == "energy").DeepClone().AsObject();
        proposal.Remove("materialization");
        proposal["resourceKey"] = resourceKey;
        proposal["displayName"] = resourceName;
        proposal["visibility"] = visibility;
        proposal["capacityPolicy"] = new JsonObject { ["kind"] = "instance_fixed" };
        var materialized = ResourceDefinitionCatalog.MaterializeProposal(
            JsonSerializer.SerializeToElement(proposal), resources, 42, "turn_42:resource_privacy",
            () => new("definition_requirement_essence", "seal_requirement_essence"));
        Assert.True(materialized.IsValid, string.Join(";", materialized.Issues));
        var definition = Assert.IsType<ResourceDefinition>(materialized.Definition);
        Assert.Contains(foreignTarget ? ResourceOwnerKind.Npc : ResourceOwnerKind.Player,
            definition.AllowedOwnerKinds);
        resources = resources.With(definition);

        var wound = WoundContractTestData.CreateActiveWound(
            ownerKind: foreignTarget ? "npc" : "player",
            ownerId: foreignTarget ? "npc_resource_patient" : "player_current",
            carrierPath: foreignTarget ? WoundCarrierCatalog.NpcPath : WoundCarrierCatalog.PlayerPath);
        var requirement = new JsonObject
        {
            ["kind"] = "resource_quantity", ["resourceRef"] = resourceKey,
            ["quantity"] = quantity, ["ownerRole"] = "target"
        };
        wound["treatment"]!["routes"]![0]!["requirements"]!.AsArray().Add(requirement);
        var diagnosis = WoundContractTestData.CreateMortalDiagnosisPath(consumesItem: false, durationMinutes: 3);
        diagnosis["requiresKnownFacts"] = new JsonArray("route:clean_and_suture");
        diagnosis["requirements"]!.AsArray().Add(requirement.DeepClone());
        wound["treatment"]!["diagnosisPaths"]!.AsArray().Add(diagnosis);

        // Each case owns its mutable roots. Reuse the prepared catalog in both production calls;
        // no filesystem, GameEngine, retained cache or historical result fabrication is needed.
        var input = WoundPlayerProjectionTestData.Input(wound) with { Resources = resources };
        var catalog = WoundCarrierCatalog.Build(input.Carriers);
        Assert.Empty(catalog.Issues);
        Assert.Empty(input.History.ValidateAgreement(input.Identities, catalog));
        var occurrence = Assert.Single(catalog.Occurrences);
        var currentWound = occurrence.Wound;
        Assert.True(input.Identities.TryGetEntry(currentWound.WoundId, out var identity));
        Assert.Equal("active", identity.Status);
        Assert.Equal("physical", identity.Domain);
        Assert.Equal(currentWound.Owner, new WoundOwnerCoordinate(identity.Realm, identity.OwnerKind,
            identity.OwnerId, identity.CarrierPath));
        var creation = Assert.Single(input.History.ResolveWoundTransitions(currentWound.WoundId));
        Assert.Equal("create", creation.Kind);
        Assert.Equal(currentWound.LastTransition.TransitionId, creation.TransitionId);
        Assert.Equal(identity.SemanticFingerprint, creation.AfterFingerprint);
        Assert.False(creation.Terminal);
        Assert.Null(creation.TransitionResult);
        Assert.True(input.Effects.IsAccepted);
        Assert.True(input.SelfVisible);
        Assert.True(WoundPlayerProjection.Visible(currentWound.Display.Visibility));

        var treatment = MortalWoundTreatmentContract.ParseProjection(currentWound.Treatment,
            "resourcePrivacy.treatment", currentWound.Owner.Realm, currentWound.Owner.OwnerKind,
            currentWound.Severity.Rank, currentWound.Complications, currentWound.Recovery.DeteriorationPolicy);
        Assert.True(treatment.IsValid, string.Join(";", treatment.Issues));
        var route = Assert.Single(treatment.Treatment!.Routes);
        var inspection = Assert.Single(treatment.Treatment.DiagnosisPaths);
        Assert.Contains(route.RouteId, currentWound.Treatment.KnownRouteIds);
        Assert.Equal("public", inspection.Visibility);
        Assert.Equal("route:" + route.RouteId, Assert.Single(inspection.RequiresKnownFacts).CanonicalValue);
        foreach (var requirements in new[] { route.Requirements, inspection.Requirements })
        {
            var declared = Assert.Single(requirements.OfType<MortalWoundResourceQuantityRequirement>());
            Assert.Equal(resourceKey, declared.ResourceRef);
            Assert.Equal(quantity, declared.Quantity);
            Assert.Equal("target", declared.OwnerRole);
        }

        var before = ResourcePrivacyInputState(input);
        var selfProjection = WoundPlayerProjection.Build(input, catalog);
        Assert.True(selfProjection.IsAvailable);
        WoundPlayerProjectionResult result;
        if (foreignTarget)
        {
            Assert.Empty(selfProjection.Active);
            var summary = new WoundTargetWoundSummary(currentWound.Display.Name, currentWound.Severity.Value,
                currentWound.Display.Description, currentWound.WoundId, identity.SemanticFingerprint);
            var target = new WoundTargetChoice("current_resource_patient", "Известный пациент",
                "Текущая локация", [summary], currentWound.Owner, null,
                WoundAcceptedTurnFingerprintWriter.Compute(["resource_privacy_card", currentWound.Owner.Realm,
                    currentWound.Owner.OwnerKind, currentWound.Owner.OwnerId, currentWound.Owner.CarrierPath,
                    summary.WoundId, summary.Fingerprint]));
            var context = new WoundPlayerReadContext(input, catalog, new JsonObject(), null, selfProjection)
            { MortalTargets = [target] };
            Assert.True(target.Available);
            Assert.Same(target, Assert.Single(context.MortalTargets));
            result = WoundPlayerProjection.ProjectKnownTarget(context, target);
        }
        else result = selfProjection;

        Assert.True(result.IsAvailable);
        Assert.Empty(result.History);
        var card = Assert.Single(result.Active);
        Assert.Equal(currentWound.Display.Name, card.Name);
        var method = Assert.Single(card.Methods);
        var diagnosticMethod = Assert.Single(card.Diagnosis);
        Assert.Equal(route.DisplayName, method.Name);
        Assert.Equal(inspection.DisplayName, diagnosticMethod.Name);
        Assert.Contains("не подтверждают наличие", method.Policy);
        Assert.Contains("не подтверждают наличие", diagnosticMethod.Policy);
        Assert.Equal(before, ResourcePrivacyInputState(input));
        var text = JsonSerializer.Serialize(result, new JsonSerializerOptions
        { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        Assert.DoesNotContain(resourceKey, text, StringComparison.Ordinal);

        // Named privacy oracle, after availability, exact identity/history and input-inertness premises.
        // Evaluate both consumers together so the foreign owner_visible case exposes both disclosures.
        Assert.Equal(
            (MethodName: readable, MethodQuantity: readable, DiagnosisName: readable, DiagnosisQuantity: readable),
            (MethodName: method.Requirements.Any(q => q.Contains(resourceName, StringComparison.Ordinal)),
                MethodQuantity: method.Requirements.Any(q => q.Contains("731", StringComparison.Ordinal)),
                DiagnosisName: diagnosticMethod.Requirements.Any(q => q.Contains(resourceName, StringComparison.Ordinal)),
                DiagnosisQuantity: diagnosticMethod.Requirements.Any(q => q.Contains("731", StringComparison.Ordinal))));
    }

    /// <summary>Captures all supplied projection DATA, including immutable identity/history and resource definitions.</summary>
    private static string ResourcePrivacyInputState(WoundPlayerProjectionInput input) => JsonSerializer.Serialize(new
    {
        input.Carriers, Identities = input.Identities.Entries,
        History = input.History.Transitions, input.History.NextOrdinal,
        input.Effects, input.Realm, Resources = input.Resources.ToCanonicalRoot(),
        input.KnownItemNames, input.SelfVisible, input.HistoricalEffects
    });
}
