using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ResourceCapacityFormulaCatalogTests
{
    private const string OwnerFingerprintA =
        "sha256:0000000000000000000000000000000000000000000000000000000000000000";

    private const string OwnerFingerprintB =
        "sha256:1111111111111111111111111111111111111111111111111111111111111111";

    [Fact]
    public void Evaluate_PlayerHealthMatchesExistingDerivedMaximum()
    {
        var result = ResourceCapacityFormulaCatalog.Evaluate(
            ResourceCapacityFormulaCatalog.MortalHealthCapacityV1,
            new PlayerHealthCapacityFormulaInput(
                PlayerOwner(),
                OwnerFingerprintA,
                PermanentStrength: 7,
                PermanentConstitution: 11));

        Assert.True(result.IsValid);
        Assert.Equal(129m, result.Value);
        Assert.StartsWith("sha256:", result.AuthorityFingerprint, StringComparison.Ordinal);
    }

    [Fact]
    public void Evaluate_PlayerEnergyAndPoiseMatchExistingDerivedMaximaExactly()
    {
        var energy = ResourceCapacityFormulaCatalog.Evaluate(
            ResourceCapacityFormulaCatalog.MortalEnergyCapacityV1,
            new PlayerEnergyCapacityFormulaInput(
                PlayerOwner(),
                OwnerFingerprintA,
                PermanentConstitution: 11,
                PermanentIntelligence: 7,
                PermanentWisdom: 5,
                PermanentFaith: 3));
        var poise = ResourceCapacityFormulaCatalog.Evaluate(
            ResourceCapacityFormulaCatalog.MortalPoiseCapacityV1,
            new PlayerPoiseCapacityFormulaInput(
                PlayerOwner(),
                OwnerFingerprintA,
                PermanentStrength: 7,
                PermanentConstitution: 11,
                PermanentIntelligence: 5,
                PermanentWisdom: 3));

        Assert.True(energy.IsValid);
        Assert.True(poise.IsValid);
        Assert.Equal(118m, energy.Value);
        Assert.Equal(137m, poise.Value);
    }

    [Theory]
    [InlineData("health", "combatant", "30")]
    [InlineData("health", "vehicle", "28")]
    [InlineData("poise", "combat_group_member", "40")]
    [InlineData("poise", "npc", "80")]
    [InlineData("energy", "npc", "75")]
    public void ResolveCapacity_MaterializedOwnerMaximumPreservesExistingPerOwnerMechanics(
        string resourceKey,
        string ownerKindToken,
        string maximum)
    {
        Assert.True(ResourceDefinitionCatalog.TryParseOwnerKind(ownerKindToken, out var ownerKind));
        var result = ResourceCapacityFormulaCatalog.ResolveCapacity(
            GetBuiltIn(resourceKey),
            new RegisteredFormulaCapacityInput(
                new MaterializedOwnerCapacityFormulaInput(
                    new ResourceFormulaOwner("mortal_world", ownerKind, "owner_opaque"),
                    OwnerFingerprintA,
                    decimal.Parse(maximum, System.Globalization.CultureInfo.InvariantCulture))));

        Assert.True(result.IsValid);
        Assert.Equal(
            decimal.Parse(maximum, System.Globalization.CultureInfo.InvariantCulture),
            result.Value);
    }

    [Theory]
    [InlineData("3", true)]
    [InlineData("0", false)]
    [InlineData("-1", false)]
    [InlineData("2.5", false)]
    public void ResolveCapacity_InstanceFixedValidatesBoundsKindAndQuantum(
        string maximum,
        bool expectedValid)
    {
        var definition = GetBuiltIn("blessing_rerolls");
        var owner = new ResourceFormulaOwner(
            "shining_abode",
            ResourceOwnerKind.AfterlifeActor,
            "afterlife_actor_current");

        var result = ResourceCapacityFormulaCatalog.ResolveCapacity(
            definition,
            new InstanceFixedCapacityInput(
                owner,
                decimal.Parse(maximum, System.Globalization.CultureInfo.InvariantCulture),
                OwnerFingerprintA));

        Assert.Equal(expectedValid, result.IsValid);
        if (!expectedValid)
        {
            Assert.Contains(result.Issues, issue =>
                issue.Code == "resource_capacity_invalid_instance_maximum");
        }
    }

    [Theory]
    [InlineData(0, 6)]
    [InlineData(1, 7)]
    [InlineData(2, 8)]
    [InlineData(3, 10)]
    [InlineData(4, 12)]
    [InlineData(5, 15)]
    public void Evaluate_SpiritFocusMatchesExistingClosedTierRule(int tier, int expected)
    {
        var input = new SpiritFocusActionPointsFormulaInput(
            new ResourceFormulaOwner(
                "chaos_sea",
                ResourceOwnerKind.AfterlifeActor,
                "afterlife_actor_current"),
            OwnerFingerprintA,
            tier);

        var result = ResourceCapacityFormulaCatalog.Evaluate(
            ResourceCapacityFormulaCatalog.AfterlifeSpiritualActionPointsV1,
            input);

        Assert.True(result.IsValid);
        Assert.Equal((decimal)expected, result.Value);
        Assert.Equal(
            AfterlifeSpiritualConflictState.GetSpiritFocusMaxActionPoints(tier),
            result.Value);
    }

    [Fact]
    public void Evaluate_ConflictSideUsesOnlyValidatedTypedCapacityInput()
    {
        var result = ResourceCapacityFormulaCatalog.Evaluate(
            ResourceCapacityFormulaCatalog.AfterlifeSpiritualActionPointsV1,
            new ConflictSideActionPointsFormulaInput(
                new ResourceFormulaOwner(
                    "chaos_sea",
                    ResourceOwnerKind.AfterlifeConflictSide,
                    "conflict_side_opaque"),
                OwnerFingerprintA,
                ConflictId: "conflict_opaque",
                AcceptedMaximum: 9));

        Assert.True(result.IsValid);
        Assert.Equal(9m, result.Value);
    }

    [Fact]
    public void Evaluate_GuardianReturnAttemptsMatchesReputationPowerAndFounderRules()
    {
        var input = new GuardianReturnGachaFormulaInput(
            new ResourceFormulaOwner(
                "chaos_sea",
                ResourceOwnerKind.AfterlifeActor,
                "guardian_opaque"),
            OwnerFingerprintA,
            Reputation: 50,
            AbodePower: 80,
            FounderExtraCharges: 1,
            ReturnCycleId: "return_cycle_42");

        var result = ResourceCapacityFormulaCatalog.Evaluate(
            ResourceCapacityFormulaCatalog.AfterlifeReturnGachaAttemptsV1,
            input);

        Assert.True(result.IsValid);
        Assert.Equal(5m, result.Value);
        Assert.Equal(
            GuardianGachaChargeRules.GetChargesPerReturnForReputation(50, 80) + 1,
            result.Value);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 2)]
    [InlineData(4, 5)]
    public void Evaluate_ShiningReturnAttemptsMatchesRadianceRule(int tier, int expected)
    {
        var input = new ShiningReturnGachaFormulaInput(
            new ResourceFormulaOwner(
                "shining_abode",
                ResourceOwnerKind.AfterlifeScope,
                "shining_return_scope_opaque"),
            OwnerFingerprintA,
            RadianceTier: tier,
            ReturnCycleId: "return_cycle_42");

        var result = ResourceCapacityFormulaCatalog.Evaluate(
            ResourceCapacityFormulaCatalog.AfterlifeReturnGachaAttemptsV1,
            input);

        Assert.True(result.IsValid);
        Assert.Equal((decimal)expected, result.Value);
        Assert.Equal(ShiningAbodeState.GetShiningGachaChargesPerReturn(tier), result.Value);
    }

    [Fact]
    public void Evaluate_FingerprintBindsEveryExactOwnerInputAndRejectsStaleReuse()
    {
        var owner = new ResourceFormulaOwner(
            "chaos_sea",
            ResourceOwnerKind.AfterlifeActor,
            "guardian_opaque");
        var initial = new GuardianReturnGachaFormulaInput(
            owner,
            OwnerFingerprintA,
            Reputation: 49,
            AbodePower: 39,
            FounderExtraCharges: 0,
            ReturnCycleId: "return_cycle_1");
        var changed = initial with { Reputation = 50 };

        var first = ResourceCapacityFormulaCatalog.Evaluate(
            ResourceCapacityFormulaCatalog.AfterlifeReturnGachaAttemptsV1,
            initial);
        var repeated = ResourceCapacityFormulaCatalog.Evaluate(
            ResourceCapacityFormulaCatalog.AfterlifeReturnGachaAttemptsV1,
            initial,
            expectedAuthorityFingerprint: first.AuthorityFingerprint);
        var stale = ResourceCapacityFormulaCatalog.Evaluate(
            ResourceCapacityFormulaCatalog.AfterlifeReturnGachaAttemptsV1,
            changed,
            expectedAuthorityFingerprint: first.AuthorityFingerprint);

        Assert.True(first.IsValid);
        Assert.True(repeated.IsValid);
        Assert.Equal(first.AuthorityFingerprint, repeated.AuthorityFingerprint);
        Assert.False(stale.IsValid);
        Assert.Null(stale.Value);
        Assert.Contains(stale.Issues, issue =>
            issue.Code == "resource_capacity_formula_stale_input");
    }

    [Theory]
    [InlineData("System.Reflection.MethodInfo")]
    [InlineData("owner.power * 2")]
    [InlineData("game_state/meta/soul_state.json")]
    public void Evaluate_UnknownExpressionPathOrMethodCanNeverSelectFormula(string formulaKey)
    {
        var result = ResourceCapacityFormulaCatalog.Evaluate(
            formulaKey,
            new SpiritFocusActionPointsFormulaInput(
                new ResourceFormulaOwner(
                    "chaos_sea",
                    ResourceOwnerKind.AfterlifeActor,
                    "afterlife_actor_current"),
                OwnerFingerprintA,
                SpiritFocusTier: 1));

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "resource_capacity_formula_unknown");
    }

    [Fact]
    public void Evaluate_FormulaKeyRejectsWrongTypedOwnerContext()
    {
        var result = ResourceCapacityFormulaCatalog.Evaluate(
            ResourceCapacityFormulaCatalog.AfterlifeReturnGachaAttemptsV1,
            new SpiritFocusActionPointsFormulaInput(
                new ResourceFormulaOwner(
                    "chaos_sea",
                    ResourceOwnerKind.AfterlifeActor,
                    "afterlife_actor_current"),
                OwnerFingerprintA,
                SpiritFocusTier: 1));

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "resource_capacity_formula_input_invalid");
    }

    [Fact]
    public void ResolveCapacity_RegisteredFormulaMustMatchDefinitionAndQuantum()
    {
        var definition = GetBuiltIn("gacha_attempts");
        var input = new RegisteredFormulaCapacityInput(
            new ShiningReturnGachaFormulaInput(
                new ResourceFormulaOwner(
                    "shining_abode",
                    ResourceOwnerKind.AfterlifeScope,
                    "shining_return_scope_opaque"),
                OwnerFingerprintA,
                RadianceTier: 2,
                ReturnCycleId: "return_cycle_42"));

        var result = ResourceCapacityFormulaCatalog.ResolveCapacity(definition, input);

        Assert.True(result.IsValid);
        Assert.Equal(3m, result.Value);
    }

    [Fact]
    public void ResolveCapacity_RejectsOwnerKindOutsideSealedDefinition()
    {
        var result = ResourceCapacityFormulaCatalog.ResolveCapacity(
            GetBuiltIn("blessing_rerolls"),
            new InstanceFixedCapacityInput(
                new ResourceFormulaOwner("mortal_world", ResourceOwnerKind.Item, "item_opaque"),
                Maximum: 3,
                CapacityAuthorityFingerprint: OwnerFingerprintA));

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "resource_capacity_owner_kind_forbidden");
    }

    [Fact]
    public void ResolveCapacity_FingerprintBindsSealedDefinitionAndOwnerAuthority()
    {
        var definition = GetBuiltIn("health");
        var inputA = new RegisteredFormulaCapacityInput(
            new MaterializedOwnerCapacityFormulaInput(
                new ResourceFormulaOwner(
                    "mortal_world",
                    ResourceOwnerKind.Combatant,
                    "combatant_opaque"),
                OwnerFingerprintA,
                AcceptedMaximum: 30));
        var inputB = new RegisteredFormulaCapacityInput(
            ((MaterializedOwnerCapacityFormulaInput)inputA.FormulaInput) with
            {
                OwnerAuthorityFingerprint = OwnerFingerprintB
            });
        var changedSeal = definition with
        {
            Materialization = definition.Materialization with { Seal = "changed_seal" }
        };

        var first = ResourceCapacityFormulaCatalog.ResolveCapacity(definition, inputA);
        var changedOwner = ResourceCapacityFormulaCatalog.ResolveCapacity(definition, inputB);
        var changedDefinition = ResourceCapacityFormulaCatalog.ResolveCapacity(changedSeal, inputA);

        Assert.True(first.IsValid);
        Assert.NotEqual(first.AuthorityFingerprint, changedOwner.AuthorityFingerprint);
        Assert.NotEqual(first.AuthorityFingerprint, changedDefinition.AuthorityFingerprint);
    }

    [Theory]
    [InlineData("minimum", "0")]
    [InlineData("maximum", "10")]
    [InlineData("fixed", "3")]
    public void ResolveInitialValue_HandlesEverySealedStaticPolicy(
        string kindToken,
        string expected)
    {
        var kind = kindToken switch
        {
            "minimum" => ResourceInitializationKind.Minimum,
            "maximum" => ResourceInitializationKind.Maximum,
            "fixed" => ResourceInitializationKind.Fixed,
            _ => throw new ArgumentOutOfRangeException(nameof(kindToken))
        };
        var definition = GetBuiltIn("blessing_rerolls") with
        {
            InitializationPolicy = kind switch
            {
                ResourceInitializationKind.Minimum =>
                    new ResourceInitializationPolicy(kind, null, null),
                ResourceInitializationKind.Maximum =>
                    new ResourceInitializationPolicy(kind, null, null),
                _ => new ResourceInitializationPolicy(kind, 3m, null)
            }
        };
        var result = ResourceCapacityFormulaCatalog.ResolveInitialValue(
            definition,
            new SealedPolicyResourceInitializationInput(
                new ResourceFormulaOwner(
                    "shining_abode",
                    ResourceOwnerKind.AfterlifeActor,
                    "actor_opaque"),
                ResolvedMaximum: 10m,
                CapacityAuthorityFingerprint: OwnerFingerprintA));

        Assert.True(result.IsValid);
        Assert.Equal(
            decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture),
            result.Value);
    }

    [Fact]
    public void ResolveInitialValue_RegisteredFormulaBindsCapacityAndRejectsStaleReuse()
    {
        var definition = GetBuiltIn("gacha_attempts") with
        {
            InitializationPolicy = new ResourceInitializationPolicy(
                ResourceInitializationKind.RegisteredFormula,
                null,
                ResourceCapacityFormulaCatalog.AfterlifeReturnGachaAttemptsV1)
        };
        var formulaInput = new ShiningReturnGachaFormulaInput(
            new ResourceFormulaOwner(
                "shining_abode",
                ResourceOwnerKind.AfterlifeScope,
                "scope_opaque"),
            OwnerFingerprintA,
            RadianceTier: 2,
            ReturnCycleId: "return_cycle_42");
        var input = new RegisteredFormulaResourceInitializationInput(
            formulaInput,
            ResolvedMaximum: 3m,
            CapacityAuthorityFingerprint: OwnerFingerprintB);

        var first = ResourceCapacityFormulaCatalog.ResolveInitialValue(definition, input);
        var repeated = ResourceCapacityFormulaCatalog.ResolveInitialValue(
            definition,
            input,
            expectedAuthorityFingerprint: first.AuthorityFingerprint);
        var stale = ResourceCapacityFormulaCatalog.ResolveInitialValue(
            definition,
            input with { ResolvedMaximum = 4m },
            expectedAuthorityFingerprint: first.AuthorityFingerprint);

        Assert.True(first.IsValid);
        Assert.True(repeated.IsValid);
        Assert.False(stale.IsValid);
        Assert.Contains(stale.Issues, issue =>
            issue.Code == "resource_initialization_stale_input");
    }

    [Fact]
    public void Evaluate_RejectsMalformedOwnerAuthorityFingerprintAndGuardianReputation()
    {
        var malformedFingerprint = ResourceCapacityFormulaCatalog.Evaluate(
            ResourceCapacityFormulaCatalog.AfterlifeSpiritualActionPointsV1,
            new SpiritFocusActionPointsFormulaInput(
                new ResourceFormulaOwner(
                    "chaos_sea",
                    ResourceOwnerKind.AfterlifeActor,
                    "actor_opaque"),
                "not-a-fingerprint",
                SpiritFocusTier: 1));
        var invalidReputation = ResourceCapacityFormulaCatalog.Evaluate(
            ResourceCapacityFormulaCatalog.AfterlifeReturnGachaAttemptsV1,
            new GuardianReturnGachaFormulaInput(
                new ResourceFormulaOwner(
                    "chaos_sea",
                    ResourceOwnerKind.AfterlifeActor,
                    "guardian_opaque"),
                OwnerFingerprintA,
                Reputation: 301,
                AbodePower: 80,
                FounderExtraCharges: 0,
                ReturnCycleId: "return_cycle_42"));

        Assert.False(malformedFingerprint.IsValid);
        Assert.False(invalidReputation.IsValid);
    }

    private static ResourceFormulaOwner PlayerOwner() =>
        new("mortal_world", ResourceOwnerKind.Player, "player_current");

    private static ResourceDefinition GetBuiltIn(string resourceKey)
    {
        var catalog = ResourceDefinitionCatalog.CreateBuiltIn();
        Assert.True(catalog.TryResolveExact(resourceKey, out var definition));
        return definition!;
    }
}
