using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class MortalItemMaterializationContractTests
{
    [Fact]
    public void Validate_CompleteRawRoot_ReturnsNoIssues()
    {
        using var document = Parse(MortalItemTestFixture.CreateRawRoot());

        Assert.Empty(MortalItemMaterializationContract.Validate(
            document.RootElement,
            "items.UpdateInventory[0]",
            MortalItemMaterializationPhase.RawPreSeal));
    }

    [Theory]
    [InlineData("materialization.realm", "Shining", "mortal_item_materialization_wrong_realm")]
    [InlineData("materialization.state", "partial", "mortal_item_materialization_invalid_envelope")]
    [InlineData("materialization.sections.mechanics.state", null, "mortal_item_materialization_section_state_mismatch")]
    [InlineData("materialization.sections.mechanics.reason", "   ", "mortal_item_materialization_section_empty_reason_missing")]
    public void Validate_RawRoot_RejectsExactContractViolation(
        string path,
        string? replacement,
        string code)
    {
        var item = MortalItemTestFixture.CreateRawRoot();
        SetNode(item, path, replacement == null ? null : JsonValue.Create(replacement));

        using var document = Parse(item);
        var issues = MortalItemMaterializationContract.Validate(
            document.RootElement,
            "items.UpdateInventory[0]",
            MortalItemMaterializationPhase.RawPreSeal);

        Assert.Contains(issues, issue => issue.Code == code);
    }

    [Theory]
    [InlineData("futureEnvelopeField", "envelope")]
    [InlineData("futureAuthorityField", "sourceAuthority")]
    [InlineData("futureSection", "sections")]
    [InlineData("futureDispositionField", "disposition")]
    public void Validate_UnknownClosedContractMember_IsRejected(string field, string target)
    {
        var item = MortalItemTestFixture.CreateRawRoot();
        var materialization = item["materialization"]!.AsObject();
        switch (target)
        {
            case "envelope":
                materialization[field] = true;
                break;
            case "sourceAuthority":
                materialization["sourceAuthority"]![field] = true;
                break;
            case "sections":
                materialization["sections"]![field] = new JsonObject
                {
                    ["state"] = "populated",
                    ["reason"] = null
                };
                break;
            case "disposition":
                materialization["sections"]!["mechanics"]![field] = true;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(target));
        }

        using var document = Parse(item);
        Assert.Contains(
            MortalItemMaterializationContract.Validate(
                document.RootElement,
                "items.UpdateInventory[0]",
                MortalItemMaterializationPhase.RawPreSeal),
            issue => issue.Code == "mortal_item_materialization_unknown_field");
    }

    [Theory]
    [InlineData("realm")]
    [InlineData("kind")]
    [InlineData("state")]
    public void Validate_DuplicateJsonProperty_IsRejectedBeforeNodeConversion(string duplicateTarget)
    {
        var json = MortalItemTestFixture.CreateRawRoot().ToJsonString();
        json = duplicateTarget switch
        {
            "realm" => json.Replace(
                "\"realm\":\"Mortal\"",
                "\"realm\":\"Mortal\",\"realm\":\"Mortal\"",
                StringComparison.Ordinal),
            "kind" => json.Replace(
                "\"kind\":\"turn_outcome\"",
                "\"kind\":\"turn_outcome\",\"kind\":\"turn_outcome\"",
                StringComparison.Ordinal),
            "state" => json.Replace(
                "\"mechanics\":{\"state\":\"empty_by_design\"",
                "\"mechanics\":{\"state\":\"empty_by_design\",\"state\":\"empty_by_design\"",
                StringComparison.Ordinal),
            _ => throw new ArgumentOutOfRangeException(nameof(duplicateTarget))
        };
        using var document = JsonDocument.Parse(json);

        Assert.Contains(
            MortalItemMaterializationContract.Validate(
                document.RootElement,
                "items.UpdateInventory[0]",
                MortalItemMaterializationPhase.RawPreSeal),
            issue => issue.Code == "mortal_item_materialization_duplicate_property");
    }

    [Theory]
    [InlineData("itemId")]
    [InlineData("id")]
    [InlineData("initialId")]
    [InlineData("materializationReceipt")]
    public void Validate_RawRootRejectsGmAuthoredClientFields(string field)
    {
        var item = MortalItemTestFixture.CreateRawRoot();
        item[field] = field == "materializationReceipt"
            ? new JsonObject()
            : JsonValue.Create("forged_id");

        using var document = Parse(item);
        var issue = Assert.Single(
            MortalItemMaterializationContract.Validate(
                document.RootElement,
                "items.UpdateInventory[0]",
                MortalItemMaterializationPhase.RawPreSeal),
            issue => issue.Code == "mortal_item_materialization_gm_authored_client_field" &&
                     issue.FilePath.EndsWith(field, StringComparison.Ordinal));
        Assert.Equal("mortal_item:new:new_item_test", issue.Actor);
    }

    [Theory]
    [InlineData("")]
    [InlineData("старинный меч на камне")]
    public void Validate_InvalidImagePrompt_IsRejected(string prompt)
    {
        var item = MortalItemTestFixture.CreateRawRoot();
        item["image_prompt"] = prompt;

        using var document = Parse(item);
        Assert.Contains(
            MortalItemMaterializationContract.Validate(
                document.RootElement,
                "items.UpdateInventory[0]",
                MortalItemMaterializationPhase.RawPreSeal),
            issue => issue.Code == "mortal_item_materialization_invalid_image_prompt");
    }

    [Fact]
    public void Validate_OverlongImagePrompt_IsRejected()
    {
        var item = MortalItemTestFixture.CreateRawRoot();
        item["image_prompt"] = new string('a', 151);

        using var document = Parse(item);
        Assert.Contains(
            MortalItemMaterializationContract.Validate(
                document.RootElement,
                "items.UpdateInventory[0]",
                MortalItemMaterializationPhase.RawPreSeal),
            issue => issue.Code == "mortal_item_materialization_invalid_image_prompt");
    }

    [Fact]
    public void Validate_MismatchedCreationRefs_IsRejected()
    {
        var item = MortalItemTestFixture.CreateRawRoot();
        item["materialization"]!["creationRef"] = "new_item_elsewhere";

        using var document = Parse(item);
        Assert.Contains(
            MortalItemMaterializationContract.Validate(
                document.RootElement,
                "items.UpdateInventory[0]",
                MortalItemMaterializationPhase.RawPreSeal),
            issue => issue.Code == "mortal_item_materialization_identity_conflict");
    }

    [Theory]
    [InlineData("mechanics", "bonuses", "remove")]
    [InlineData("equipment", "requiresTwoHands", "true")]
    [InlineData("container", "capacity", "zero")]
    [InlineData("consumption", "isConsumption", "true")]
    [InlineData("readableOrSentient", "journalEntries", "null")]
    [InlineData("craftingAndDisassembly", "disassembleTo", "array")]
    [InlineData("bondsAndFateCards", "fateCards", "null")]
    [InlineData("questRole", "questLinks", "null")]
    public void Validate_EmptyByDesignRequiresExactPhysicalEmptyShape(
        string section,
        string field,
        string mutation)
    {
        var item = MortalItemTestFixture.CreateRawRoot();
        switch (mutation)
        {
            case "remove":
                item.Remove(field);
                break;
            case "true":
                item[field] = true;
                break;
            case "zero":
                item[field] = 0;
                break;
            case "null":
                item[field] = null;
                break;
            case "array":
                item[field] = new JsonArray();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation));
        }

        using var document = Parse(item);
        Assert.Contains(
            MortalItemMaterializationContract.Validate(
                document.RootElement,
                "items.UpdateInventory[0]",
                MortalItemMaterializationPhase.RawPreSeal),
            issue => issue.Code == "mortal_item_materialization_canonical_empty_surface_missing" &&
                     issue.Section == section);
    }

    [Fact]
    public void Validate_MissingGovernedField_IsRejectedEvenWhenSectionIsPopulated()
    {
        var item = MortalItemTestFixture.CreateRawRoot();
        item.Remove("volume");

        using var document = Parse(item);
        Assert.Contains(
            MortalItemMaterializationContract.Validate(
                document.RootElement,
                "items.UpdateInventory[0]",
                MortalItemMaterializationPhase.RawPreSeal),
            issue => issue.Code == "mortal_item_materialization_complete_field_missing" &&
                     issue.Section == "physical");
    }

    [Theory]
    [InlineData("equipment", "equipmentSlot")]
    [InlineData("container", "isContainer")]
    [InlineData("readableOrSentient", "textContent")]
    [InlineData("craftingAndDisassembly", "disassembleTo")]
    public void Validate_PopulatedSectionRejectsStructurallyEmptyEvidence(
        string section,
        string field)
    {
        var item = MortalItemTestFixture.CreateRawRoot();
        var disposition = item["materialization"]!["sections"]![section]!.AsObject();
        disposition["state"] = "populated";
        disposition["reason"] = null;
        switch (section)
        {
            case "equipment":
                item[field] = new JsonArray();
                break;
            case "container":
                item[field] = true;
                break;
            case "readableOrSentient":
            case "craftingAndDisassembly":
                item[field] = new JsonArray();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(section));
        }

        using var document = Parse(item);
        Assert.Contains(
            MortalItemMaterializationContract.Validate(
                document.RootElement,
                "items.UpdateInventory[0]",
                MortalItemMaterializationPhase.RawPreSeal),
            issue => issue.Code == "mortal_item_materialization_section_state_mismatch" &&
                     issue.Section == section);
    }

    [Fact]
    public void Validate_RouteAuthorityKindMustMatchRoute()
    {
        var item = MortalItemTestFixture.CreateRawRoot();
        item["materialization"]!["sourceAuthority"]!["kind"] = "craft_request";

        using var document = Parse(item);
        Assert.Contains(
            MortalItemMaterializationContract.Validate(
                document.RootElement,
                "items.UpdateInventory[0]",
                MortalItemMaterializationPhase.RawPreSeal),
            issue => issue.Code == "mortal_item_materialization_route_authority_mismatch");
    }

    [Fact]
    public void Validate_CompleteCanonicalRoot_ReturnsNoIssues()
    {
        using var document = Parse(MortalItemTestFixture.CreateCanonicalRoot());

        Assert.Empty(MortalItemMaterializationContract.Validate(
            document.RootElement,
            "items.items[0]",
            MortalItemMaterializationPhase.CanonicalPostSeal));
    }

    [Fact]
    public void Validate_UnequalCanonicalIds_IsRejected()
    {
        var item = MortalItemTestFixture.CreateCanonicalRoot();
        item["existedId"] = "itm_case_variant";

        using var document = Parse(item);
        var issue = Assert.Single(
            MortalItemMaterializationContract.Validate(
                document.RootElement,
                "items.items[0]",
                MortalItemMaterializationPhase.CanonicalPostSeal),
            issue => issue.Code == "mortal_item_materialization_identity_conflict");
        Assert.Equal("mortal_item:existing:itm_test", issue.Actor);
    }

    [Theory]
    [InlineData("futureReceiptField", "unknown")]
    [InlineData("receiptId", "missing")]
    [InlineData("instanceKind", "invalid_kind")]
    [InlineData("parentItemIds", "non_empty_root")]
    public void Validate_CanonicalReceiptUsesExactImmutableShape(string field, string mutation)
    {
        var item = MortalItemTestFixture.CreateCanonicalRoot();
        var receipt = item["materializationReceipt"]!.AsObject();
        switch (mutation)
        {
            case "unknown":
                receipt[field] = true;
                break;
            case "missing":
                receipt.Remove(field);
                break;
            case "invalid_kind":
                receipt[field] = "copy";
                break;
            case "non_empty_root":
                receipt[field] = new JsonArray("itm_parent");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation));
        }

        using var document = Parse(item);
        Assert.Contains(
            MortalItemMaterializationContract.Validate(
                document.RootElement,
                "items.items[0]",
                MortalItemMaterializationPhase.CanonicalPostSeal),
            issue => issue.Code is "mortal_item_materialization_invalid_receipt" or
                     "mortal_item_materialization_unknown_field");
    }

    [Fact]
    public void Validate_WrongReceiptSeal_IsRejected()
    {
        var item = MortalItemTestFixture.CreateCanonicalRoot();
        item["materializationReceipt"]!["seal"] = "sha256:" + new string('0', 64);

        using var document = Parse(item);
        Assert.Contains(
            MortalItemMaterializationContract.Validate(
                document.RootElement,
                "items.items[0]",
                MortalItemMaterializationPhase.CanonicalPostSeal),
            issue => issue.Code == "mortal_item_materialization_receipt_seal_mismatch");
    }

    [Fact]
    public void ComputeSeal_IsStableAcrossEnvelopePropertyOrder()
    {
        var first = MortalItemTestFixture.CreateCanonicalRoot();
        var second = first.DeepClone().AsObject();
        var reversedEnvelope = new JsonObject();
        foreach (var pair in second["materialization"]!.AsObject().Reverse())
            reversedEnvelope[pair.Key] = pair.Value?.DeepClone();
        second["materialization"] = reversedEnvelope;
        var receipt = second["materializationReceipt"]!.AsObject();
        receipt.Remove("seal");

        Assert.Equal(
            first["materializationReceipt"]!["seal"]!.GetValue<string>(),
            MortalItemMaterializationContract.ComputeSeal(second, receipt));
    }

    [Fact]
    public void ImmutableEvidenceEquals_IgnoresObjectOrderButRejectsSemanticRewrite()
    {
        var previous = MortalItemTestFixture.CreateCanonicalRoot();
        var reordered = previous.DeepClone().AsObject();
        var envelope = reordered["materialization"]!.AsObject();
        var reverse = new JsonObject();
        foreach (var pair in envelope.Reverse())
            reverse[pair.Key] = pair.Value?.DeepClone();
        reordered["materialization"] = reverse;

        Assert.True(MortalItemMaterializationContract.ImmutableEvidenceEquals(
            previous["materialization"]!,
            reordered["materialization"]!));

        reordered["materialization"]!["route"] = "craft_output";
        Assert.False(MortalItemMaterializationContract.ImmutableEvidenceEquals(
            previous["materialization"]!,
            reordered["materialization"]!));
    }

    [Fact]
    public void HasCompleteEnvelope_ReturnsFalseWhenASectionIsMissing()
    {
        var complete = MortalItemTestFixture.CreateRawRoot();
        var incomplete = complete.DeepClone().AsObject();
        incomplete["materialization"]!["sections"]!.AsObject().Remove("questRole");

        using var completeDocument = Parse(complete);
        using var incompleteDocument = Parse(incomplete);
        Assert.True(MortalItemMaterializationContract.HasCompleteEnvelope(completeDocument.RootElement));
        Assert.False(MortalItemMaterializationContract.HasCompleteEnvelope(incompleteDocument.RootElement));
    }

    private static JsonDocument Parse(JsonNode root) =>
        JsonDocument.Parse(root.ToJsonString());

    private static void SetNode(JsonObject root, string path, JsonNode? value)
    {
        var segments = path.Split('.', StringSplitOptions.RemoveEmptyEntries);
        var parent = root;
        foreach (var segment in segments[..^1])
        {
            parent = parent[segment]?.AsObject() ??
                     throw new InvalidOperationException($"Missing object segment: {segment}");
        }

        if (value == null)
            parent.Remove(segments[^1]);
        else
            parent[segments[^1]] = value;
    }
}
