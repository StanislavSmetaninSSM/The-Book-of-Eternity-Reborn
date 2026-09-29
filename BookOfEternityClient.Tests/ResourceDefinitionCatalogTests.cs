using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Verifies resource definition admission and client-owned materialization identities.
/// </summary>
public sealed partial class ResourceDefinitionCatalogTests
{
    [Fact]
    public void BuiltIns_AreCompleteSealedVersionOneAndCanonicallyOrdered()
    {
        var catalog = ResourceDefinitionCatalog.CreateBuiltIn();

        Assert.Equal(
            new[]
            {
                "ammunition",
                "blessing_rerolls",
                "charges",
                "durability",
                "energy",
                "gacha_attempts",
                "health",
                "poise",
                "spiritual_action_points"
            },
            catalog.Definitions.Select(static definition => definition.ResourceKey));
        Assert.All(catalog.Definitions, definition =>
        {
            Assert.Equal(1, definition.DefinitionVersion);
            Assert.Equal(ResourceNumericKind.Integer, definition.NumericKind);
            Assert.Equal(1m, definition.Quantum);
            Assert.Equal(1, definition.Materialization.SchemaVersion);
            Assert.NotEmpty(definition.Materialization.DefinitionId);
            Assert.NotEmpty(definition.Materialization.Seal);
        });
    }

    [Fact]
    public void BuiltIns_UseClosedOwnerOperationAndCapacityPolicies()
    {
        var catalog = ResourceDefinitionCatalog.CreateBuiltIn();
        Assert.True(catalog.TryResolveExact("health", out var health));
        Assert.NotNull(health);
        Assert.Equal(ResourceCapacityKind.RegisteredFormula, health.CapacityPolicy.Kind);
        Assert.Equal(
            ResourceCapacityFormulaCatalog.MortalHealthCapacityV1,
            health.CapacityPolicy.FormulaKey);
        Assert.True(health.AllowedOwnerKinds.SetEquals(new[]
        {
            ResourceOwnerKind.Player,
            ResourceOwnerKind.Npc,
            ResourceOwnerKind.Combatant,
            ResourceOwnerKind.CombatGroupMember,
            ResourceOwnerKind.Vehicle
        }));
        Assert.Equal(
            new[] { ResourceOperation.Damage, ResourceOperation.Restore },
            health.AllowedOperations.OrderBy(static value => value));

        Assert.True(catalog.TryResolveExact("energy", out var energy));
        Assert.Equal(
            ResourceCapacityFormulaCatalog.MortalEnergyCapacityV1,
            energy!.CapacityPolicy.FormulaKey);
        Assert.True(catalog.TryResolveExact("poise", out var poise));
        Assert.Equal(
            ResourceCapacityFormulaCatalog.MortalPoiseCapacityV1,
            poise!.CapacityPolicy.FormulaKey);
        Assert.Contains(ResourceOwnerKind.Npc, poise.AllowedOwnerKinds);

        Assert.True(catalog.TryResolveExact("durability", out var durability));
        Assert.Equal(ResourceCapacityKind.InstanceFixed, durability!.CapacityPolicy.Kind);
        Assert.True(catalog.TryResolveExact("spiritual_action_points", out var actionPoints));
        Assert.Equal(
            ResourceCapacityFormulaCatalog.AfterlifeSpiritualActionPointsV1,
            actionPoints!.CapacityPolicy.FormulaKey);
        Assert.True(catalog.TryResolveExact("gacha_attempts", out var attempts));
        Assert.Equal(
            ResourceCapacityFormulaCatalog.AfterlifeReturnGachaAttemptsV1,
            attempts!.CapacityPolicy.FormulaKey);
    }

    [Theory]
    [MemberData(nameof(ValidPolicyVariants))]
    public void MaterializeProposal_AcceptsEveryClosedPolicyVariant(JsonObject proposal)
    {
        using var document = JsonDocument.Parse(proposal.ToJsonString());

        var result = ResourceDefinitionCatalog.MaterializeProposal(
            document.RootElement,
            ResourceDefinitionCatalog.CreateBuiltIn(),
            createdAtTurn: 42,
            createdEventRef: "turn_42",
            allocateIdentity: static () => new ResourceDefinitionIdentity(
                "resource_definition_mana_opaque",
                "resource_definition_seal_mana_opaque"));

        Assert.True(result.IsValid);
        Assert.NotNull(result.Definition);
        Assert.Equal("mana", result.Definition.ResourceKey);
        Assert.Equal(42, result.Definition.Materialization.CreatedAtTurn);
        Assert.Equal("turn_42", result.Definition.Materialization.CreatedEventRef);
    }

    [Fact]
    public void MaterializeProposal_AllocatesIdentityExactlyOnceAndNeverTrustsRawSeal()
    {
        var proposal = CreateProposal();
        using var validDocument = JsonDocument.Parse(proposal.ToJsonString());
        var calls = 0;

        var accepted = ResourceDefinitionCatalog.MaterializeProposal(
            validDocument.RootElement,
            ResourceDefinitionCatalog.CreateBuiltIn(),
            42,
            "turn_42",
            () =>
            {
                calls++;
                return new ResourceDefinitionIdentity("definition_once", "seal_once");
            });

        proposal["materialization"] = new JsonObject
        {
            ["definitionId"] = "forged",
            ["seal"] = "forged"
        };
        using var forgedDocument = JsonDocument.Parse(proposal.ToJsonString());
        var rejected = ResourceDefinitionCatalog.MaterializeProposal(
            forgedDocument.RootElement,
            ResourceDefinitionCatalog.CreateBuiltIn(),
            42,
            "turn_42",
            () =>
            {
                calls++;
                return new ResourceDefinitionIdentity("unused", "unused");
            });

        Assert.Equal(1, calls);
        Assert.Equal("definition_once", accepted.Definition!.Materialization.DefinitionId);
        Assert.Equal("seal_once", accepted.Definition.Materialization.Seal);
        Assert.Null(rejected.Definition);
        Assert.Contains(rejected.Issues, issue =>
            issue.Code == "resource_definition_client_field_forbidden");
    }

    [Fact]
    public void MaterializeProposal_RejectsAllocatedIdentityOrSealCollision()
    {
        var existingRoot = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["definitions"] = new JsonArray(
                CreateCanonicalDefinition("existing", "definition_collision", "seal_existing"))
        };
        var existing = ResourceDefinitionCatalog.ParseCanonical(
            existingRoot.ToJsonString(),
            allowMissingPristine: false).Catalog!;
        using var proposal = JsonDocument.Parse(CreateProposal().ToJsonString());

        var result = ResourceDefinitionCatalog.MaterializeProposal(
            proposal.RootElement,
            existing,
            42,
            "turn_42",
            static () => new ResourceDefinitionIdentity(
                "definition_collision",
                "SEAL_EXISTING"));

        Assert.Null(result.Definition);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "resource_definition_duplicate_materialization_id");
        Assert.Contains(result.Issues, issue =>
            issue.Code == "resource_definition_confusable_seal");
    }

    [Fact]
    public void MaterializeProposal_RejectsDefinitionBeyondCatalogLimitBeforeAllocation()
    {
        var definitions = new JsonArray();
        for (var index = 0; index < ResourceMaterializationContract.MaxDefinitions; index++)
        {
            var suffix = index.ToString("D3", System.Globalization.CultureInfo.InvariantCulture);
            definitions.Add(CreateCanonicalDefinition(
                "resource_" + suffix,
                "definition_" + suffix,
                "seal_" + suffix));
        }
        var root = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["definitions"] = definitions
        };
        var existingResult = ResourceDefinitionCatalog.ParseCanonical(
            root.ToJsonString(),
            allowMissingPristine: false);
        Assert.True(existingResult.IsValid);
        using var proposal = JsonDocument.Parse(CreateProposal().ToJsonString());
        var allocations = 0;

        var result = ResourceDefinitionCatalog.MaterializeProposal(
            proposal.RootElement,
            existingResult.Catalog!,
            42,
            "turn_42",
            () =>
            {
                allocations++;
                return new ResourceDefinitionIdentity("unused", "unused");
            });

        Assert.Equal(0, allocations);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "resource_definition_limit_exceeded");
    }

    [Theory]
    [InlineData("health", "resource_definition_rewrite_forbidden")]
    [InlineData("HEALTH", "resource_definition_confusable_key")]
    [InlineData("heаlth", "resource_definition_confusable_key")]
    public void MaterializeProposal_RejectsExistingOrConfusableResourceKey(
        string resourceKey,
        string expectedCode)
    {
        var proposal = CreateProposal();
        proposal["resourceKey"] = resourceKey;
        using var document = JsonDocument.Parse(proposal.ToJsonString());

        var result = ResourceDefinitionCatalog.MaterializeProposal(
            document.RootElement,
            ResourceDefinitionCatalog.CreateBuiltIn(),
            42,
            "turn_42",
            static () => new ResourceDefinitionIdentity("unused", "unused"));

        Assert.Null(result.Definition);
        Assert.Contains(result.Issues, issue => issue.Code == expectedCode);
    }

    [Theory]
    [InlineData("numericKind", "floating")]
    [InlineData("defaultFloorPolicy", "always_clamp")]
    [InlineData("defaultCapPolicy", "ignore_maximum")]
    [InlineData("visibility", "public")]
    public void MaterializeProposal_RejectsUnknownClosedToken(string field, string value)
    {
        var proposal = CreateProposal();
        proposal[field] = value;
        using var document = JsonDocument.Parse(proposal.ToJsonString());

        var result = Materialize(document.RootElement);

        Assert.Null(result.Definition);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "resource_definition_invalid_field" &&
            issue.FilePath.EndsWith('.' + field, StringComparison.Ordinal));
    }

    [Fact]
    public void MaterializeProposal_RejectsUnknownFieldsAndArbitraryFormulaSelectors()
    {
        var proposal = CreateProposal();
        proposal["futurePolicy"] = true;
        proposal["capacityPolicy"] = new JsonObject
        {
            ["kind"] = "registered_formula",
            ["formulaKey"] = "System.IO.File.ReadAllText",
            ["expression"] = "owner.power * 2",
            ["path"] = "game_state/meta/soul_state.json"
        };
        using var document = JsonDocument.Parse(proposal.ToJsonString());

        var result = Materialize(document.RootElement);

        Assert.Null(result.Definition);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "resource_definition_unknown_field");
        Assert.Contains(result.Issues, issue =>
            issue.Code == "resource_definition_formula_unknown");
    }

    [Fact]
    public void MaterializeProposal_RejectsFormulaThatCannotServeEveryAllowedOwnerKind()
    {
        var proposal = CreateProposal();
        proposal["capacityPolicy"] = new JsonObject
        {
            ["kind"] = "registered_formula",
            ["formulaKey"] = ResourceCapacityFormulaCatalog.AfterlifeReturnGachaAttemptsV1
        };
        using var document = JsonDocument.Parse(proposal.ToJsonString());

        var result = Materialize(document.RootElement);

        Assert.Null(result.Definition);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "resource_definition_formula_owner_mismatch");
    }

    [Fact]
    public void MaterializeProposal_RejectsDuplicateOrConfusableCatalogMembers()
    {
        var proposal = CreateProposal();
        proposal["allowedOwnerKinds"] = new JsonArray("player", "PLAYER");
        proposal["allowedOperations"] = new JsonArray("spend", "spend");
        using var document = JsonDocument.Parse(proposal.ToJsonString());

        var result = Materialize(document.RootElement);

        Assert.Null(result.Definition);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "resource_definition_confusable_owner_kind");
        Assert.Contains(result.Issues, issue =>
            issue.Code == "resource_definition_duplicate_operation");
    }

    [Theory]
    [InlineData("integer", "0.5", "0", "10")]
    [InlineData("integer", "1", "0.5", "10")]
    [InlineData("decimal", "0", "0", "10")]
    [InlineData("decimal", "0.3", "0", "10")]
    [InlineData("decimal", "0.25", "0", "10.1")]
    public void MaterializeProposal_RejectsInvalidQuantumNumericKindOrBounds(
        string numericKind,
        string quantum,
        string minimum,
        string maximum)
    {
        var proposal = CreateProposal();
        proposal["numericKind"] = numericKind;
        proposal["quantum"] = decimal.Parse(quantum, System.Globalization.CultureInfo.InvariantCulture);
        proposal["minimumPolicy"] = new JsonObject
        {
            ["kind"] = "definition_fixed",
            ["value"] = decimal.Parse(minimum, System.Globalization.CultureInfo.InvariantCulture)
        };
        proposal["capacityPolicy"] = new JsonObject
        {
            ["kind"] = "definition_fixed",
            ["value"] = decimal.Parse(maximum, System.Globalization.CultureInfo.InvariantCulture)
        };
        using var document = JsonDocument.Parse(proposal.ToJsonString());

        var result = Materialize(document.RootElement);

        Assert.Null(result.Definition);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "resource_definition_invalid_numeric_policy");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MaterializeProposal_RejectsFixedInitializationBelowMinimumForDynamicCapacity(
        bool registeredFormulaCapacity)
    {
        var proposal = CreateProposal();
        proposal["minimumPolicy"] = new JsonObject
        {
            ["kind"] = "definition_fixed",
            ["value"] = 5
        };
        proposal["initializationPolicy"] = new JsonObject
        {
            ["kind"] = "fixed",
            ["value"] = 4
        };
        if (registeredFormulaCapacity)
        {
            proposal["capacityPolicy"] = new JsonObject
            {
                ["kind"] = "registered_formula",
                ["formulaKey"] =
                    ResourceCapacityFormulaCatalog.AfterlifeReturnGachaAttemptsV1
            };
            proposal["allowedOwnerKinds"] = new JsonArray(
                "afterlife_actor",
                "afterlife_scope");
        }
        using var document = JsonDocument.Parse(proposal.ToJsonString());

        var result = Materialize(document.RootElement);

        Assert.Null(result.Definition);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "resource_definition_invalid_numeric_policy");
    }

    [Fact]
    public void ParseCanonical_RejectsDuplicateAndConfusableKeysAndSupportsExactLookup()
    {
        var first = CreateCanonicalDefinition("mana", "definition_mana", "seal_mana");
        var second = CreateCanonicalDefinition("MANA", "definition_mana_2", "seal_mana_2");
        var root = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["definitions"] = new JsonArray(first, second)
        };

        var rejected = ResourceDefinitionCatalog.ParseCanonical(
            root.ToJsonString(),
            allowMissingPristine: false);
        var validRoot = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["definitions"] = new JsonArray(
                CreateCanonicalDefinition("zeta", "definition_zeta", "seal_zeta"),
                CreateCanonicalDefinition("alpha", "definition_alpha", "seal_alpha"))
        };
        var accepted = ResourceDefinitionCatalog.ParseCanonical(
            validRoot.ToJsonString(),
            allowMissingPristine: false);

        Assert.Contains(rejected.Issues, issue =>
            issue.Code == "resource_definition_confusable_key");
        Assert.True(accepted.IsValid);
        Assert.Equal(
            new[] { "alpha", "zeta" },
            accepted.Catalog!.Definitions.Select(static definition => definition.ResourceKey));
        Assert.True(accepted.Catalog.TryResolveExact("alpha", out _));
        Assert.False(accepted.Catalog.TryResolveExact("ALPHA", out _));
    }

    [Fact]
    public void ParseCanonical_RejectsDuplicateOrConfusableMaterializationIdentity()
    {
        var root = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["definitions"] = new JsonArray(
                CreateCanonicalDefinition("alpha", "definition_shared", "seal_alpha"),
                CreateCanonicalDefinition("beta", "definition_shared", "SEAL_ALPHA"))
        };

        var result = ResourceDefinitionCatalog.ParseCanonical(
            root.ToJsonString(),
            allowMissingPristine: false);

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "resource_definition_duplicate_materialization_id");
        Assert.Contains(result.Issues, issue =>
            issue.Code == "resource_definition_confusable_seal");
    }

    public static IEnumerable<object[]> ValidPolicyVariants()
    {
        var minimum = CreateProposal();
        minimum["initializationPolicy"] = new JsonObject { ["kind"] = "minimum" };
        yield return new object[] { minimum };

        var maximum = CreateProposal();
        maximum["initializationPolicy"] = new JsonObject { ["kind"] = "maximum" };
        yield return new object[] { maximum };

        var fixedInitialization = CreateProposal();
        fixedInitialization["initializationPolicy"] = new JsonObject
        {
            ["kind"] = "fixed",
            ["value"] = 3
        };
        yield return new object[] { fixedInitialization };

        var formulaInitialization = CreateProposal();
        formulaInitialization["capacityPolicy"] = new JsonObject
        {
            ["kind"] = "registered_formula",
            ["formulaKey"] = ResourceCapacityFormulaCatalog.AfterlifeReturnGachaAttemptsV1
        };
        formulaInitialization["initializationPolicy"] = new JsonObject
        {
            ["kind"] = "registered_formula",
            ["formulaKey"] = ResourceCapacityFormulaCatalog.AfterlifeReturnGachaAttemptsV1
        };
        formulaInitialization["allowedOwnerKinds"] = new JsonArray("afterlife_actor", "afterlife_scope");
        yield return new object[] { formulaInitialization };
    }

    private static ResourceDefinitionMaterializationResult Materialize(JsonElement proposal) =>
        ResourceDefinitionCatalog.MaterializeProposal(
            proposal,
            ResourceDefinitionCatalog.CreateBuiltIn(),
            42,
            "turn_42",
            static () => new ResourceDefinitionIdentity("definition_mana", "seal_mana"));

    private static JsonObject CreateProposal() => new()
    {
        ["resourceKey"] = "mana",
        ["definitionVersion"] = 1,
        ["displayName"] = "Мана",
        ["numericKind"] = "integer",
        ["unit"] = "point",
        ["quantum"] = 1,
        ["minimumPolicy"] = new JsonObject
        {
            ["kind"] = "definition_fixed",
            ["value"] = 0
        },
        ["capacityPolicy"] = new JsonObject
        {
            ["kind"] = "instance_fixed"
        },
        ["initializationPolicy"] = new JsonObject
        {
            ["kind"] = "maximum"
        },
        ["allowedOwnerKinds"] = new JsonArray("player"),
        ["allowedOperations"] = new JsonArray("spend", "gain"),
        ["defaultFloorPolicy"] = "reject_below_minimum",
        ["defaultCapPolicy"] = "clamp_to_maximum",
        ["visibility"] = "player_visible"
    };

    private static JsonObject CreateCanonicalDefinition(
        string resourceKey,
        string definitionId,
        string seal)
    {
        var definition = CreateProposal();
        definition["resourceKey"] = resourceKey;
        definition["materialization"] = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["definitionId"] = definitionId,
            ["seal"] = seal,
            ["createdAtTurn"] = 1,
            ["createdEventRef"] = "bootstrap_1"
        };
        return definition;
    }
}
