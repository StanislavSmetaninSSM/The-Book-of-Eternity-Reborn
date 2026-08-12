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
    [InlineData("raw", "creationRef")]
    [InlineData("canonical", "itemId")]
    public void Validate_MissingExactIdentityUsesStandaloneUnknownActor(
        string phaseName,
        string identityField)
    {
        var phase = phaseName == "raw"
            ? MortalItemMaterializationPhase.RawPreSeal
            : MortalItemMaterializationPhase.CanonicalPostSeal;
        var item = phase == MortalItemMaterializationPhase.RawPreSeal
            ? MortalItemTestFixture.CreateRawRoot()
            : MortalItemTestFixture.CreateCanonicalRoot();
        item.Remove(identityField);

        using var document = Parse(item);
        var issues = MortalItemMaterializationContract.Validate(
            document.RootElement,
            "items[0]",
            phase);

        Assert.NotEmpty(issues);
        Assert.All(issues, issue => Assert.Equal("mortal_item:unknown", issue.Actor));
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

    [Fact]
    public void IdentityIndex_ParseEmptyRoot_ReturnsCurrentDeterministicShape()
    {
        var result = MortalItemIdentityState.Parse(new JsonObject
        {
            ["schemaVersion"] = 1,
            ["entries"] = new JsonArray()
        });

        Assert.Empty(result.Issues);
        Assert.Empty(result.EntriesByItemId);
        Assert.True(JsonNode.DeepEquals(
            MortalItemIdentityState.CreateEmptyRoot(),
            result.Root));
    }

    [Fact]
    public void IdentityIndex_ParseRejectsDuplicateItemAndReceiptIds()
    {
        var item = MortalItemTestFixture.CreateCanonicalRoot();
        var root = MortalItemTestFixture.CreateIndex(
            item,
            item.DeepClone().AsObject());

        var result = MortalItemIdentityState.Parse(root);

        Assert.Contains(result.Issues, issue =>
            issue.Code == "mortal_item_materialization_duplicate_item_id");
        Assert.Contains(result.Issues, issue =>
            issue.Code == "mortal_item_materialization_duplicate_receipt_id");
    }

    [Fact]
    public void IdentityIndex_ParseRejectsUnknownRootAndEntryFields()
    {
        var item = MortalItemTestFixture.CreateCanonicalRoot();
        var root = MortalItemTestFixture.CreateIndex(item);
        root["futureRootField"] = true;
        root["entries"]![0]!["futureEntryField"] = true;

        var result = MortalItemIdentityState.Parse(root);

        Assert.Equal(2, result.Issues.Count(issue =>
            issue.Code == "mortal_item_identity_unknown_field"));
    }

    [Fact]
    public void IdentityIndex_ParseRawJsonRejectsDuplicateProperty()
    {
        var result = MortalItemIdentityState.Parse(
            "{\"schemaVersion\":1,\"schemaVersion\":1,\"entries\":[]}");

        Assert.Contains(result.Issues, issue =>
            issue.Code == "mortal_item_identity_duplicate_property");
    }

    [Fact]
    public void IdentityIndex_ParseRawJsonAcceptsValidIntegerSchema()
    {
        var root = MortalItemTestFixture.CreateIndex(
            MortalItemTestFixture.CreateCanonicalRoot());

        var result = MortalItemIdentityState.Parse(root.ToJsonString());

        Assert.Empty(result.Issues);
    }

    [Fact]
    public void IdentityIndex_ParseRejectsUnsortedOrDuplicateOrigins()
    {
        var root = MortalItemTestFixture.CreateIndex(
            MortalItemTestFixture.CreateCanonicalRoot());
        root["entries"]![0]!["originMaterializationIds"] =
            new JsonArray("mat_z", "mat_a", "mat_a");

        var result = MortalItemIdentityState.Parse(root);

        Assert.Contains(result.Issues, issue =>
            issue.Code == "mortal_item_identity_origin_ids_not_sorted");
        Assert.Contains(result.Issues, issue =>
            issue.Code == "mortal_item_identity_duplicate_origin_id");
    }

    [Fact]
    public void IdentityIndex_ParseRejectsMissingOriginCreationReferences()
    {
        var root = MortalItemTestFixture.CreateIndex(
            MortalItemTestFixture.CreateCanonicalRoot());
        root["entries"]![0]!.AsObject().Remove("originCreationRefs");

        var result = MortalItemIdentityState.Parse(root);

        Assert.Contains(result.Issues, issue =>
            issue.FilePath.EndsWith(".originCreationRefs", StringComparison.Ordinal) &&
            issue.Code == "mortal_item_identity_invalid_entry");
    }

    [Theory]
    [InlineData("MAT_ITEM_TEST", "NEW_ITEM_TEST")]
    [InlineData("mat_item_cafe\u0301", "new_item_cafe\u0301")]
    public void IdentityIndex_AcceptedRootEvidenceDetectsConfusableAliases(
        string materializationId,
        string creationRef)
    {
        var item = MortalItemTestFixture.CreateCanonicalRoot();
        if (materializationId.Contains("cafe", StringComparison.Ordinal))
        {
            item = MortalItemTestFixture.CreateRawRoot(
                creationRef: "new_item_caf\u00e9",
                materializationId: "mat_item_caf\u00e9");
            var receipt = MortalItemIdentityState.CreateRootReceipt(item, "itm_cafe", 42);
            item["itemId"] = "itm_cafe";
            item["existedId"] = "itm_cafe";
            item.Remove("creationRef");
            item["materializationReceipt"] = receipt;
        }
        var index = MortalItemIdentityState.Parse(
            MortalItemTestFixture.CreateIndex(item));

        var evidence = MortalItemIdentityState.BuildAcceptedRootCreationEvidence(index);

        Assert.Equal(
            MortalItemAcceptedCreationEvidenceMatch.Confusable,
            evidence.Match(materializationId, creationRef));
    }

    [Fact]
    public void IdentityIndex_ParseRejectsCarrierStateMismatch()
    {
        var root = MortalItemTestFixture.CreateIndex(
            MortalItemTestFixture.CreateCanonicalRoot());
        root["entries"]![0]!["state"] = "destroyed";

        var result = MortalItemIdentityState.Parse(root);

        Assert.Contains(result.Issues, issue =>
            issue.Code == "mortal_item_identity_state_mismatch");
    }

    [Fact]
    public void IdentityIndex_ParseRejectsLocationCarrierWithoutStorageId()
    {
        var item = MortalItemTestFixture.CreateCanonicalRoot();
        var root = MortalItemTestFixture.CreateIndexForCarrier(
            item,
            "location_storage",
            "loc_test");

        var result = MortalItemIdentityState.Parse(root);

        Assert.Contains(result.Issues, issue =>
            issue.Code == "mortal_item_identity_invalid_carrier");
    }

    [Fact]
    public void IdentityIndex_ParseRejectsNoOpTransition()
    {
        var root = MortalItemTestFixture.CreateIndex(
            MortalItemTestFixture.CreateCanonicalRoot());
        var transition = root["entries"]![0]!["transitions"]![0]!.AsObject();
        transition["kind"] = "semantic_update";
        transition["sourceCarrier"] = transition["destinationCarrier"]!.DeepClone();
        transition["quantityBefore"] = 1;
        transition["quantityAfter"] = 1;

        var result = MortalItemIdentityState.Parse(root);

        Assert.Contains(result.Issues, issue =>
            issue.Code == "mortal_item_identity_transition_noop");
    }

    [Fact]
    public void IdentityIndex_ParseRejectsCarrierThatDisagreesWithLastTransition()
    {
        var root = MortalItemTestFixture.CreateIndex(
            MortalItemTestFixture.CreateCanonicalRoot());
        root["entries"]![0]!["currentCarrier"]!["kind"] = "npc_inventory";
        root["entries"]![0]!["currentCarrier"]!["ownerId"] = "npc_test";

        var result = MortalItemIdentityState.Parse(root);

        Assert.Contains(result.Issues, issue =>
            issue.Code == "mortal_item_identity_transition_state_mismatch");
    }

    [Fact]
    public void IdentityIndex_ParseRejectsDecreasingTransitionTurn()
    {
        var root = MortalItemTestFixture.CreateIndex(
            MortalItemTestFixture.CreateCanonicalRoot());
        var entry = root["entries"]![0]!.AsObject();
        var carrier = entry["currentCarrier"]!.AsObject();
        MortalItemIdentityState.AppendTransition(
            entry,
            MortalItemIdentityState.CreateTransition(
                "semantic_update",
                turn: 41,
                sourceItemIds: new[] { MortalItemTestFixture.ItemId },
                sourceCarrier: carrier,
                destinationCarrier: carrier,
                quantityBefore: 1,
                quantityAfter: 2,
                authorityKind: "turn_outcome",
                authorityId: "turn_41"));

        var result = MortalItemIdentityState.Parse(root);

        Assert.Contains(result.Issues, issue =>
            issue.Code == "mortal_item_identity_transition_turn_order");
    }

    [Fact]
    public void IdentityIndex_ParseRejectsMalformedRootCreationTransition()
    {
        var root = MortalItemTestFixture.CreateIndex(
            MortalItemTestFixture.CreateCanonicalRoot());
        var entry = root["entries"]![0]!.AsObject();
        entry["transitions"]![0]!["sourceCarrier"] = entry["currentCarrier"]!.DeepClone();

        var result = MortalItemIdentityState.Parse(root);

        Assert.Contains(result.Issues, issue =>
            issue.Code == "mortal_item_identity_transition_shape_mismatch");
    }

    [Fact]
    public void IdentityIndex_ParseRejectsGloballyDuplicateTransitionId()
    {
        var first = MortalItemTestFixture.CreateCanonicalRoot("itm_a");
        var second = MortalItemTestFixture.CreateCanonicalRoot("itm_b");
        var root = MortalItemTestFixture.CreateIndex(first, second);
        root["entries"]![1]!["transitions"]![0]!["transitionId"] =
            root["entries"]![0]!["transitions"]![0]!["transitionId"]!.DeepClone();

        var result = MortalItemIdentityState.Parse(root);

        Assert.Contains(result.Issues, issue =>
            issue.Code == "mortal_item_identity_duplicate_transition_id");
    }

    [Fact]
    public void IdentityIndex_ParseCanonicalizesEntryAndObjectOrder()
    {
        var item = MortalItemTestFixture.CreateCanonicalRoot();
        var ordinary = MortalItemTestFixture.CreateIndex(item);
        var reversedEntry = new JsonObject();
        foreach (var pair in ordinary["entries"]![0]!.AsObject().Reverse())
            reversedEntry[pair.Key] = pair.Value?.DeepClone();
        var reordered = new JsonObject
        {
            ["entries"] = new JsonArray(reversedEntry),
            ["schemaVersion"] = 1
        };

        var first = MortalItemIdentityState.Parse(ordinary);
        var second = MortalItemIdentityState.Parse(reordered);

        Assert.Empty(first.Issues);
        Assert.Empty(second.Issues);
        Assert.Equal(first.Root.ToJsonString(), second.Root.ToJsonString());
        Assert.Same(
            first.Root["entries"]![0],
            first.EntriesByItemId[MortalItemTestFixture.ItemId]);
    }

    [Fact]
    public void CreateRootReceipt_SealsCanonicalItemAgainstAcceptedEnvelope()
    {
        var raw = MortalItemTestFixture.CreateRawRoot();
        var receipt = MortalItemIdentityState.CreateRootReceipt(
            raw,
            MortalItemTestFixture.ItemId,
            acceptedTurn: 42);
        var canonical = raw.DeepClone().AsObject();
        canonical["itemId"] = MortalItemTestFixture.ItemId;
        canonical["existedId"] = MortalItemTestFixture.ItemId;
        canonical.Remove("creationRef");
        canonical["materializationReceipt"] = receipt;

        using var document = Parse(canonical);
        Assert.Empty(MortalItemMaterializationContract.Validate(
            document.RootElement,
            "items.items[0]",
            MortalItemMaterializationPhase.CanonicalPostSeal));
    }

    [Fact]
    public void CreateSplitReceipt_UsesNewReceiptAndDirectParentLineage()
    {
        var parent = MortalItemTestFixture.CreateCanonicalRoot();
        var receipt = MortalItemIdentityState.CreateSplitReceipt(
            parent,
            "itm_child",
            turn: 43);
        var child = parent.DeepClone().AsObject();
        child["itemId"] = "itm_child";
        child["existedId"] = "itm_child";
        child["materializationReceipt"] = receipt;

        Assert.NotEqual(
            parent["materializationReceipt"]!["receiptId"]!.GetValue<string>(),
            receipt["receiptId"]!.GetValue<string>());
        Assert.Equal("split_derived", receipt["instanceKind"]!.GetValue<string>());
        Assert.Equal(
            MortalItemTestFixture.ItemId,
            Assert.Single(receipt["parentItemIds"]!.AsArray())!.GetValue<string>());

        using var document = Parse(child);
        Assert.Empty(MortalItemMaterializationContract.Validate(
            document.RootElement,
            "items.items[1]",
            MortalItemMaterializationPhase.CanonicalPostSeal));
    }

    [Fact]
    public void CreateTransition_EmitsExactClientOwnedShape()
    {
        var transition = MortalItemIdentityState.CreateTransition(
            "transfer",
            turn: 43,
            sourceItemIds: new[] { MortalItemTestFixture.ItemId },
            sourceCarrier: new JsonObject
            {
                ["kind"] = "player_inventory",
                ["ownerId"] = "player",
                ["containerId"] = null,
                ["containerPath"] = new JsonArray()
            },
            destinationCarrier: new JsonObject
            {
                ["kind"] = "npc_inventory",
                ["ownerId"] = "npc_test",
                ["containerId"] = null,
                ["containerPath"] = new JsonArray()
            },
            quantityBefore: 1,
            quantityAfter: 1,
            authorityKind: "npc_trade_receipt",
            authorityId: "trade_43");

        Assert.Equal(
            new[]
            {
                "transitionId", "kind", "turn", "sourceItemIds", "sourceCarrier",
                "destinationCarrier", "quantityBefore", "quantityAfter", "authorityKind", "authorityId"
            },
            transition.Select(pair => pair.Key));
        Assert.StartsWith("mitrn_", transition["transitionId"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    [Fact]
    public void IdentityIndex_ValidateAgainstRejectsReceiptAndTransitionHistoryRewrite()
    {
        var beforeRoot = MortalItemTestFixture.CreateIndex(
            MortalItemTestFixture.CreateCanonicalRoot());
        var currentRoot = beforeRoot.DeepClone().AsObject();
        currentRoot["entries"]![0]!["receiptId"] = "mirec_forged";
        currentRoot["entries"]![0]!["transitions"]![0]!["authorityId"] = "turn_forged";

        var issues = MortalItemIdentityState.ValidateAgainst(
            MortalItemIdentityState.Parse(beforeRoot),
            MortalItemIdentityState.Parse(currentRoot));

        Assert.Contains(issues, issue =>
            issue.Code == "mortal_item_identity_protected_field_rewrite");
        Assert.Contains(issues, issue =>
            issue.Code == "mortal_item_identity_transition_history_rewrite");
    }

    [Fact]
    public void IdentityIndex_ValidateAgainstAllowsAppendOnlyTransfer()
    {
        var beforeRoot = MortalItemTestFixture.CreateIndex(
            MortalItemTestFixture.CreateCanonicalRoot());
        var currentRoot = beforeRoot.DeepClone().AsObject();
        var entry = currentRoot["entries"]![0]!.AsObject();
        var sourceCarrier = entry["currentCarrier"]!.AsObject();
        var destinationCarrier = new JsonObject
        {
            ["kind"] = "npc_inventory",
            ["ownerId"] = "npc_test",
            ["containerId"] = null,
            ["containerPath"] = new JsonArray()
        };
        entry["currentCarrier"] = destinationCarrier.DeepClone();
        MortalItemIdentityState.AppendTransition(
            entry,
            MortalItemIdentityState.CreateTransition(
                "transfer",
                turn: 43,
                sourceItemIds: new[] { MortalItemTestFixture.ItemId },
                sourceCarrier,
                destinationCarrier,
                quantityBefore: 1,
                quantityAfter: 1,
                authorityKind: "npc_trade_receipt",
                authorityId: "trade_43"));

        Assert.Empty(MortalItemIdentityState.ValidateAgainst(
            MortalItemIdentityState.Parse(beforeRoot),
            MortalItemIdentityState.Parse(currentRoot)));
    }

    [Fact]
    public void IdentityIndex_ValidateAgainstRejectsRetiredReactivation()
    {
        var activeRoot = MortalItemTestFixture.CreateIndex(
            MortalItemTestFixture.CreateCanonicalRoot());
        var retiredRoot = activeRoot.DeepClone().AsObject();
        retiredRoot["entries"]![0]!["state"] = "destroyed";
        retiredRoot["entries"]![0]!["currentCarrier"] = null;

        var issues = MortalItemIdentityState.ValidateAgainst(
            MortalItemIdentityState.Parse(retiredRoot),
            MortalItemIdentityState.Parse(activeRoot));

        Assert.Contains(issues, issue =>
            issue.Code == "mortal_item_identity_retired_reactivated");
    }

    [Fact]
    public void IdentityIndex_ValidateAgainstRejectsCarrierRewriteWithoutTransfer()
    {
        var beforeRoot = MortalItemTestFixture.CreateIndex(
            MortalItemTestFixture.CreateCanonicalRoot());
        var currentRoot = beforeRoot.DeepClone().AsObject();
        currentRoot["entries"]![0]!["currentCarrier"] = new JsonObject
        {
            ["kind"] = "npc_inventory",
            ["ownerId"] = "npc_test",
            ["containerId"] = null,
            ["containerPath"] = new JsonArray()
        };

        var issues = MortalItemIdentityState.ValidateAgainst(
            MortalItemIdentityState.Parse(beforeRoot),
            MortalItemIdentityState.Parse(currentRoot));

        Assert.Contains(issues, issue =>
            issue.Code == "mortal_item_identity_unrecorded_state_change");
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
