using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalWoundTreatmentResolverTests
{
    /// <summary>Feeds real composed publication after-images to the display-only reader.</summary>
    private static void AssertPhysicalTerminalPlayerHistory(AcceptedMechanicsPlan plan,
        WoundCarrierCatalogInput carriers, WoundCarrierCatalog catalog, WoundIdentityState identities,
        WoundHistoryState history, EffectCarrierCatalog effects, EffectAcceptedTurnPlan effectPlan,
        WoundMaterializationEnvelope terminalBefore, WoundMaterializationEnvelope terminalAfter)
    {
        Assert.NotNull(carriers.PlayerWounds); Assert.NotNull(carriers.NpcWounds);
        Assert.NotNull(carriers.EnemyCombatants); Assert.NotNull(carriers.AllyCombatants);
        Assert.NotNull(carriers.AfterlifeProfiles);
        Assert.Equal("mortal_world", terminalAfter.Owner.Realm);
        Assert.Equal("player", terminalAfter.Owner.OwnerKind);
        Assert.Equal("player_current", terminalAfter.Owner.OwnerId);
        Assert.Contains(terminalBefore.Display.Visibility, new[] { "public", "known_to_player" });
        Assert.Contains(terminalAfter.Display.Visibility, new[] { "public", "known_to_player" });
        var mechanics = EffectMechanicsSnapshot.Build(effects, effectPlan.IdentityIndexAfterImage,
            effectPlan.SkillScopeAuthority, out var effectIdentity);
        Assert.True(mechanics.IsAccepted, JsonSerializer.Serialize(mechanics.Issues));
        Assert.NotNull(effectIdentity);
        var resources = ResourceDefinitionCatalog.ParseCanonical(plan.DefinitionAfterImage.ToJsonString(), allowMissingPristine: false);
        Assert.True(resources.IsValid, JsonSerializer.Serialize(resources.Issues));
        var input = new WoundPlayerProjectionInput(carriers, identities, history, mechanics,
            "mortal_world", resources.Catalog!, new Dictionary<string, string>(StringComparer.Ordinal),
            HistoricalEffects: new(effects, effectIdentity!));
        var projected = WoundPlayerProjection.Build(input, catalog);
        Assert.True(projected.IsAvailable);
        Assert.Empty(projected.Active);
        var entry = Assert.Single(projected.History);
        Assert.Equal(terminalAfter.Display.Name, entry.Name);
        Assert.Equal(terminalBefore.Severity.Value, entry.Severity);
        Assert.Equal("Рана зажила", entry.Summary);
    }
}
