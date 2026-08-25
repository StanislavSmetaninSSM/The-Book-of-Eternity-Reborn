using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ResourceStateContractTests
{
    private const string FingerprintA =
        "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string FingerprintB =
        "sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    [Fact]
    public void ParseCanonical_AcceptsStrictEntriesBuildsExactIndexAndCanonicalOrder()
    {
        var later = CreateEntry(
            ownerId: "item_zeta",
            resourceKey: "charges",
            current: 2,
            maximum: 5,
            capacityKind: "instance_fixed",
            authorityKey: "capacity_zeta");
        var earlier = CreateEntry(
            ownerId: "item_alpha",
            resourceKey: "durability",
            current: 8,
            maximum: 10,
            capacityKind: "instance_fixed",
            authorityKey: "capacity_alpha");

        var result = ResourceStateContract.ParseCanonical(
            CreateRoot(later, earlier).ToJsonString(),
            ResourceDefinitionCatalog.CreateBuiltIn(),
            allowMissingPristine: false);

        Assert.True(result.IsValid);
        Assert.NotNull(result.Ledger);
        Assert.Equal(
            new[] { "item_alpha", "item_zeta" },
            result.Ledger.Entries.Select(static entry => entry.Coordinate.ResourceOwnerId));
        Assert.True(result.Ledger.TryResolveExact(
            new ResourceCoordinate(
                "mortal_world",
                ResourceOwnerKind.Item,
                "item_zeta",
                "charges"),
            out var resolved));
        Assert.Equal(2m, resolved!.Current);
        Assert.True(ResourceMaterializationContract.IsAuthorityFingerprint(
            result.Ledger.Fingerprint));
    }

    [Fact]
    public void ParseCanonical_IsSemanticallyOrderIndependentAndFingerprintSensitive()
    {
        var alpha = CreateEntry("item_alpha", "charges", 2, 5);
        var beta = CreateEntry("item_beta", "durability", 4, 10);
        var definitions = ResourceDefinitionCatalog.CreateBuiltIn();

        var forward = ResourceStateContract.ParseCanonical(
            CreateRoot(alpha, beta).ToJsonString(),
            definitions,
            allowMissingPristine: false);
        var reversed = ResourceStateContract.ParseCanonical(
            CreateRoot(beta, alpha).ToJsonString(),
            definitions,
            allowMissingPristine: false);
        var changedEntry = CreateEntry("item_alpha", "charges", 1, 5);
        var changed = ResourceStateContract.ParseCanonical(
            CreateRoot(changedEntry, beta).ToJsonString(),
            definitions,
            allowMissingPristine: false);

        Assert.Equal(forward.Ledger!.Fingerprint, reversed.Ledger!.Fingerprint);
        Assert.Equal(forward.Ledger.ToCanonicalJson(), reversed.Ledger.ToCanonicalJson());
        Assert.NotEqual(forward.Ledger.Fingerprint, changed.Ledger!.Fingerprint);
    }

    [Fact]
    public void ParseCanonical_NormalizesEquivalentDecimalScaleInFingerprintAndSerialization()
    {
        var root = CreateRoot(CreateEntry("item_alpha", "charges", 2, 5));
        var compactJson = root.ToJsonString();
        var scaledJson = compactJson
            .Replace("\"current\":2", "\"current\":2.00", StringComparison.Ordinal)
            .Replace("\"maximum\":5", "\"maximum\":5.0", StringComparison.Ordinal);

        var compact = ResourceStateContract.ParseCanonical(
            compactJson,
            ResourceDefinitionCatalog.CreateBuiltIn(),
            allowMissingPristine: false);
        var scaled = ResourceStateContract.ParseCanonical(
            scaledJson,
            ResourceDefinitionCatalog.CreateBuiltIn(),
            allowMissingPristine: false);

        Assert.True(compact.IsValid);
        Assert.True(scaled.IsValid);
        Assert.Equal(compact.Ledger!.Fingerprint, scaled.Ledger!.Fingerprint);
        Assert.Equal(compact.Ledger.ToCanonicalJson(), scaled.Ledger.ToCanonicalJson());
    }

    [Fact]
    public void ParseCanonical_DistinguishesMissingPristineFromPresentInvalidRoots()
    {
        var definitions = ResourceDefinitionCatalog.CreateBuiltIn();

        var missingAllowed = ResourceStateContract.ParseCanonical(
            null,
            definitions,
            allowMissingPristine: true);
        var missingRequired = ResourceStateContract.ParseCanonical(
            null,
            definitions,
            allowMissingPristine: false);

        Assert.True(missingAllowed.IsValid);
        Assert.True(missingAllowed.IsMissing);
        Assert.NotNull(missingAllowed.Ledger);
        Assert.Empty(missingAllowed.Ledger.Entries);
        Assert.Contains(missingRequired.Issues, issue =>
            issue.Code == "resource_state_root_missing");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   \r\n")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{broken")]
    [InlineData("{\"schemaVersion\":1,\"entries\":[],\"extra\":true}")]
    [InlineData("{\"schemaVersion\":1,\"schemaVersion\":1,\"entries\":[]}")]
    public void ParseCanonical_RejectsPresentMalformedWrongRootUnknownOrDuplicateData(string json)
    {
        var result = ResourceStateContract.ParseCanonical(
            json,
            ResourceDefinitionCatalog.CreateBuiltIn(),
            allowMissingPristine: true);

        Assert.False(result.IsValid);
        Assert.Null(result.Ledger);
    }

    [Fact]
    public void ParseCanonical_RejectsUnknownNestedFieldsAndDuplicateNestedProperties()
    {
        var unknown = CreateEntry("item_alpha", "charges", 2, 5);
        unknown["rawPath"] = "game_state/items.json";
        var duplicate = $$"""
            {
              "schemaVersion": 1,
              "entries": [
                {
                  "realm": "mortal_world",
                  "ownerKind": "item",
                  "resourceOwnerId": "item_alpha",
                  "resourceKey": "charges",
                  "current": 2,
                  "current": 3,
                  "maximum": 5,
                  "capacityBinding": {
                    "kind": "instance_fixed",
                    "authorityKey": "capacity_item_alpha",
                    "authorityFingerprint": "{{FingerprintA}}"
                  },
                  "state": "active",
                  "chronology": {
                    "createdAtTurn": 1,
                    "createdEventRef": "bootstrap_1",
                    "lastTransitionId": "transition_item_alpha",
                    "lastEventRef": "bootstrap_1",
                    "lastTransitionTurn": 1
                  }
                }
              ]
            }
            """;

        var unknownResult = Parse(CreateRoot(unknown));
        var duplicateResult = ResourceStateContract.ParseCanonical(
            duplicate,
            ResourceDefinitionCatalog.CreateBuiltIn(),
            allowMissingPristine: false);

        Assert.Contains(unknownResult.Issues, issue =>
            issue.Code == "resource_state_unknown_field");
        Assert.Contains(duplicateResult.Issues, issue =>
            issue.Code == "resource_state_duplicate_property");
    }

    [Theory]
    [InlineData("ITEM_ALPHA", "charges", "resource_state_confusable_coordinate")]
    [InlineData("item_alpha", "CHARGES", "resource_state_confusable_coordinate")]
    public void ParseCanonical_RejectsExactAndConfusableCoordinates(
        string secondOwnerId,
        string secondResourceKey,
        string expectedCode)
    {
        var first = CreateEntry("item_alpha", "charges", 2, 5);
        var second = CreateEntry(secondOwnerId, secondResourceKey, 2, 5);

        var result = Parse(CreateRoot(first, second));

        Assert.Contains(result.Issues, issue => issue.Code == expectedCode);
    }

    [Fact]
    public void ParseCanonical_RejectsDuplicateExactCoordinate()
    {
        var result = Parse(CreateRoot(
            CreateEntry("item_alpha", "charges", 2, 5),
            CreateEntry("item_alpha", "charges", 3, 5)));

        Assert.Contains(result.Issues, issue =>
            issue.Code == "resource_state_duplicate_coordinate");
    }

    [Theory]
    [InlineData(-1, 5, "resource_state_value_out_of_range")]
    [InlineData(6, 5, "resource_state_value_out_of_range")]
    [InlineData(2.5, 5, "resource_state_value_invalid")]
    [InlineData(2, 0, "resource_state_capacity_invalid")]
    public void ParseCanonical_RejectsInvalidCurrentMaximumAndIntegerValues(
        double current,
        double maximum,
        string expectedCode)
    {
        var entry = CreateEntry(
            "item_alpha",
            "charges",
            Convert.ToDecimal(current),
            Convert.ToDecimal(maximum));

        var result = Parse(CreateRoot(entry));

        Assert.Contains(result.Issues, issue => issue.Code == expectedCode);
    }

    [Fact]
    public void ParseCanonical_RejectsUnknownDefinitionAndOwnerKindMismatch()
    {
        var unknown = CreateEntry("item_alpha", "unknown_resource", 2, 5);
        var wrongOwner = CreateEntry("player_current", "charges", 2, 5);
        wrongOwner["ownerKind"] = "player";

        var unknownResult = Parse(CreateRoot(unknown));
        var ownerResult = Parse(CreateRoot(wrongOwner));

        Assert.Contains(unknownResult.Issues, issue =>
            issue.Code == "resource_state_definition_unknown");
        Assert.Contains(ownerResult.Issues, issue =>
            issue.Code == "resource_state_owner_kind_forbidden");
    }

    [Theory]
    [InlineData("paused", "resource_state_lifecycle_invalid")]
    [InlineData("ACTIVE", "resource_state_lifecycle_invalid")]
    public void ParseCanonical_RejectsNonClosedLifecycleState(string state, string expectedCode)
    {
        var entry = CreateEntry("item_alpha", "charges", 2, 5);
        entry["state"] = state;

        var result = Parse(CreateRoot(entry));

        Assert.Contains(result.Issues, issue => issue.Code == expectedCode);
    }

    [Fact]
    public void ParseCanonical_AcceptsSuspendedStateButRetainsExactChronology()
    {
        var entry = CreateEntry("item_alpha", "charges", 2, 5);
        entry["state"] = "suspended";
        entry["chronology"]!["lastTransitionId"] = "transition_suspend_alpha";
        entry["chronology"]!["lastEventRef"] = "turn_9";
        entry["chronology"]!["lastTransitionTurn"] = 9;

        var result = Parse(CreateRoot(entry));

        Assert.True(result.IsValid);
        var accepted = Assert.Single(result.Ledger!.Entries);
        Assert.Equal(ResourceLifecycleState.Suspended, accepted.State);
        Assert.Equal("transition_suspend_alpha", accepted.Chronology.LastTransitionId);
        Assert.Equal(9, accepted.Chronology.LastTransitionTurn);
    }

    [Theory]
    [InlineData(-1, 1, "resource_state_chronology_invalid")]
    [InlineData(5, 4, "resource_state_chronology_invalid")]
    public void ParseCanonical_RejectsInvalidChronology(
        int createdAtTurn,
        int lastTransitionTurn,
        string expectedCode)
    {
        var entry = CreateEntry("item_alpha", "charges", 2, 5);
        entry["chronology"]!["createdAtTurn"] = createdAtTurn;
        entry["chronology"]!["lastTransitionTurn"] = lastTransitionTurn;

        var result = Parse(CreateRoot(entry));

        Assert.Contains(result.Issues, issue => issue.Code == expectedCode);
    }

    [Theory]
    [InlineData("definition_fixed", "charges", "resource_state_capacity_kind_mismatch")]
    [InlineData("registered_formula", "wrong_formula", "resource_state_capacity_kind_mismatch")]
    [InlineData("instance_fixed", " capacity_alpha ", "resource_state_capacity_binding_invalid")]
    public void ParseCanonical_RejectsCapacityKindAuthorityAndDefinitionMismatch(
        string kind,
        string authorityKey,
        string expectedCode)
    {
        var entry = CreateEntry(
            "item_alpha",
            "charges",
            2,
            5,
            kind,
            authorityKey);

        var result = Parse(CreateRoot(entry));

        Assert.Contains(result.Issues, issue => issue.Code == expectedCode);
    }

    [Fact]
    public void ParseCanonical_RejectsMalformedAuthorityFingerprint()
    {
        var entry = CreateEntry("item_alpha", "charges", 2, 5);
        entry["capacityBinding"]!["authorityFingerprint"] = FingerprintA.ToUpperInvariant();

        var result = Parse(CreateRoot(entry));

        Assert.Contains(result.Issues, issue =>
            issue.Code == "resource_state_capacity_binding_invalid");
    }

    [Fact]
    public void ParseCanonical_ValidatesDefinitionFixedMaximumAndFingerprint()
    {
        var definitions = CreateDefinitionFixedCatalog();
        Assert.True(definitions.TryResolveExact("mana", out var mana));
        var owner = new ResourceFormulaOwner(
            "mortal_world",
            ResourceOwnerKind.Player,
            "player_current");
        var capacity = ResourceCapacityFormulaCatalog.ResolveCapacity(
            mana!,
            new DefinitionFixedCapacityInput(owner));
        Assert.True(capacity.IsValid);
        var entry = CreateEntry(
            "player_current",
            "mana",
            7,
            10,
            "definition_fixed",
            "mana",
            capacity.AuthorityFingerprint!);
        entry["ownerKind"] = "player";

        var accepted = ResourceStateContract.ParseCanonical(
            CreateRoot(entry).ToJsonString(),
            definitions,
            allowMissingPristine: false);
        entry["maximum"] = 11;
        var wrongMaximum = ResourceStateContract.ParseCanonical(
            CreateRoot(entry).ToJsonString(),
            definitions,
            allowMissingPristine: false);

        Assert.True(accepted.IsValid);
        Assert.Contains(wrongMaximum.Issues, issue =>
            issue.Code == "resource_state_capacity_invalid");
    }

    [Fact]
    public void ParseCanonical_AcceptsRegisteredFormulaBindingForExactOwnerAndDefinition()
    {
        var entry = CreateEntry(
            "player_current",
            "health",
            85,
            100,
            "registered_formula",
            ResourceCapacityFormulaCatalog.MortalHealthCapacityV1);
        entry["ownerKind"] = "player";

        var result = Parse(CreateRoot(entry));

        Assert.True(result.IsValid);
        var accepted = Assert.Single(result.Ledger!.Entries);
        Assert.Equal(ResourceCapacityKind.RegisteredFormula, accepted.CapacityBinding.Kind);
        Assert.Equal(
            ResourceCapacityFormulaCatalog.MortalHealthCapacityV1,
            accepted.CapacityBinding.AuthorityKey);
    }

    [Fact]
    public void ParseCanonical_EnforcesExactDecimalQuantumAlignment()
    {
        var definitions = CreateDecimalQuantumCatalog();
        Assert.True(definitions.TryResolveExact("focus", out var focus));
        var owner = new ResourceFormulaOwner(
            "mortal_world",
            ResourceOwnerKind.Player,
            "player_current");
        var capacity = ResourceCapacityFormulaCatalog.ResolveCapacity(
            focus!,
            new DefinitionFixedCapacityInput(owner));
        var entry = CreateEntry(
            "player_current",
            "focus",
            0.5m,
            1m,
            "definition_fixed",
            "focus",
            capacity.AuthorityFingerprint!);
        entry["ownerKind"] = "player";

        var accepted = ResourceStateContract.ParseCanonical(
            CreateRoot(entry).ToJsonString(),
            definitions,
            allowMissingPristine: false);
        entry["current"] = 0.3m;
        var misaligned = ResourceStateContract.ParseCanonical(
            CreateRoot(entry).ToJsonString(),
            definitions,
            allowMissingPristine: false);

        Assert.True(accepted.IsValid);
        Assert.Contains(misaligned.Issues, issue =>
            issue.Code == "resource_state_value_invalid");
    }

    [Fact]
    public void ParseCanonical_EnforcesLiveEntryLimitAtBoundary()
    {
        var atLimit = new JsonArray();
        for (var index = 0; index < ResourceMaterializationContract.MaxLiveEntries; index++)
        {
            atLimit.Add(CreateEntry(
                "item_" + index.ToString("D5", System.Globalization.CultureInfo.InvariantCulture),
                "charges",
                1,
                5));
        }
        var validRoot = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["entries"] = atLimit
        };
        var valid = Parse(validRoot);
        atLimit.Add(CreateEntry("item_over_limit", "charges", 1, 5));
        var invalid = Parse(validRoot);

        Assert.True(valid.IsValid);
        Assert.Equal(ResourceMaterializationContract.MaxLiveEntries, valid.Ledger!.Entries.Count);
        Assert.Contains(invalid.Issues, issue =>
            issue.Code == "resource_state_limit_exceeded");
    }

    [Fact]
    public void LedgerEntries_AreDefensiveAndCannotMutateTheIndexOrFingerprint()
    {
        var result = Parse(CreateRoot(CreateEntry("item_alpha", "charges", 2, 5)));
        var entries = Assert.IsAssignableFrom<IReadOnlyList<ResourceStateEntry>>(
            result.Ledger!.Entries);
        var originalFingerprint = result.Ledger.Fingerprint;

        Assert.False(entries is ResourceStateEntry[]);
        Assert.Equal(originalFingerprint, result.Ledger.Fingerprint);
        Assert.True(result.Ledger.TryResolveExact(entries[0].Coordinate, out var resolved));
        Assert.Equal(entries[0], resolved);
    }

    private static ResourceStateContractResult Parse(JsonObject root) =>
        ResourceStateContract.ParseCanonical(
            root.ToJsonString(),
            ResourceDefinitionCatalog.CreateBuiltIn(),
            allowMissingPristine: false);

    private static JsonObject CreateRoot(params JsonObject[] entries) => new()
    {
        ["schemaVersion"] = 1,
        ["entries"] = new JsonArray(entries.Select(static entry => entry.DeepClone()).ToArray())
    };

    private static JsonObject CreateEntry(
        string ownerId,
        string resourceKey,
        decimal current,
        decimal maximum,
        string capacityKind = "instance_fixed",
        string? authorityKey = null,
        string authorityFingerprint = FingerprintA) => new()
    {
        ["realm"] = "mortal_world",
        ["ownerKind"] = "item",
        ["resourceOwnerId"] = ownerId,
        ["resourceKey"] = resourceKey,
        ["current"] = current,
        ["maximum"] = maximum,
        ["capacityBinding"] = new JsonObject
        {
            ["kind"] = capacityKind,
            ["authorityKey"] = authorityKey ?? "capacity_" + ownerId,
            ["authorityFingerprint"] = authorityFingerprint
        },
        ["state"] = "active",
        ["chronology"] = new JsonObject
        {
            ["createdAtTurn"] = 1,
            ["createdEventRef"] = "bootstrap_1",
            ["lastTransitionId"] = "transition_" + ownerId + "_" + resourceKey,
            ["lastEventRef"] = "bootstrap_1",
            ["lastTransitionTurn"] = 1
        }
    };

    private static ResourceDefinitionCatalog CreateDefinitionFixedCatalog()
    {
        var definition = new JsonObject
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
                ["kind"] = "definition_fixed",
                ["value"] = 10
            },
            ["initializationPolicy"] = new JsonObject
            {
                ["kind"] = "maximum"
            },
            ["allowedOwnerKinds"] = new JsonArray("player"),
            ["allowedOperations"] = new JsonArray("spend", "gain"),
            ["defaultFloorPolicy"] = "reject_below_minimum",
            ["defaultCapPolicy"] = "clamp_to_maximum",
            ["visibility"] = "player_visible",
            ["materialization"] = new JsonObject
            {
                ["schemaVersion"] = 1,
                ["definitionId"] = "definition_mana",
                ["seal"] = "seal_mana",
                ["createdAtTurn"] = 1,
                ["createdEventRef"] = "bootstrap_1"
            }
        };
        var root = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["definitions"] = new JsonArray(definition)
        };
        var result = ResourceDefinitionCatalog.ParseCanonical(
            root.ToJsonString(),
            allowMissingPristine: false);
        Assert.True(result.IsValid);
        return result.Catalog!;
    }

    private static ResourceDefinitionCatalog CreateDecimalQuantumCatalog()
    {
        var definition = new JsonObject
        {
            ["resourceKey"] = "focus",
            ["definitionVersion"] = 1,
            ["displayName"] = "Фокус",
            ["numericKind"] = "decimal",
            ["unit"] = "point",
            ["quantum"] = 0.25m,
            ["minimumPolicy"] = new JsonObject
            {
                ["kind"] = "definition_fixed",
                ["value"] = 0
            },
            ["capacityPolicy"] = new JsonObject
            {
                ["kind"] = "definition_fixed",
                ["value"] = 1
            },
            ["initializationPolicy"] = new JsonObject
            {
                ["kind"] = "maximum"
            },
            ["allowedOwnerKinds"] = new JsonArray("player"),
            ["allowedOperations"] = new JsonArray("spend", "gain"),
            ["defaultFloorPolicy"] = "reject_below_minimum",
            ["defaultCapPolicy"] = "clamp_to_maximum",
            ["visibility"] = "owner_visible",
            ["materialization"] = new JsonObject
            {
                ["schemaVersion"] = 1,
                ["definitionId"] = "definition_focus",
                ["seal"] = "seal_focus",
                ["createdAtTurn"] = 1,
                ["createdEventRef"] = "bootstrap_1"
            }
        };
        var root = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["definitions"] = new JsonArray(definition)
        };
        var result = ResourceDefinitionCatalog.ParseCanonical(
            root.ToJsonString(),
            allowMissingPristine: false);
        Assert.True(result.IsValid);
        return result.Catalog!;
    }
}
