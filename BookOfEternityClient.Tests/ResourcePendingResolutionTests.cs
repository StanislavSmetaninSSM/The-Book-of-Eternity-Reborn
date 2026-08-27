using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ResourcePendingResolutionTests
{
    private const string FingerprintA =
        "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string FingerprintB =
        "sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    [Fact]
    public void CreatePending_SealsTechnicalAuthorityAndBuildsSafeGmPacket()
    {
        var result = CreatePending(Draft());

        Assert.True(result.IsValid, Format(result.Issues));
        var state = Assert.IsType<ResourcePendingResolutionState>(result.State);
        var request = Assert.Single(state.Requests);
        Assert.Equal("resource_resolution_test", request.RequestId);
        Assert.Equal("session_test", request.SessionId);
        Assert.Equal("request_turn_42", request.AcceptedRequestId);
        Assert.Equal(42, request.RequestTurn);
        Assert.Equal("turn_42_effect_2", request.EventRef);
        Assert.Equal("effect_test_bleeding", request.EffectId);
        Assert.Equal(
            new ResourcePendingAuthorityBinding(
                "permanent",
                "effect_test_bleeding"),
            request.EffectAuthority);
        Assert.Equal(
            new ResourcePendingAuthorityBinding(
                "permanent",
                "wound_test_torn_side"),
            request.SourceAuthority);
        Assert.Equal(
            new ResourcePendingAuthorityBinding("permanent", "player_current"),
            request.TargetAuthority);
        Assert.Equal(
            new ResourcePendingAuthorityBinding("permanent", "player_current"),
            request.ResourceAuthority);
        Assert.Equal(
            new ResourceCoordinate(
                "mortal_world",
                ResourceOwnerKind.Player,
                "player_current",
                "health"),
            request.Coordinate);
        Assert.Equal(ResourceOperation.Damage, request.Operation);
        Assert.Equal(0m, request.MinimumAmount);
        Assert.Equal(5m, request.MaximumAmount);
        Assert.Equal(FingerprintA, request.SourceAuthorityFingerprint);
        Assert.Equal(FingerprintB, request.PolicyFingerprint);
        Assert.True(ResourceMaterializationContract.IsAuthorityFingerprint(
            request.ReplayFingerprint));
        Assert.Equal("pending", request.State);

        var canonical = state.ToCanonicalJson();
        Assert.Contains("resourceOwnerId", canonical, StringComparison.Ordinal);
        Assert.Contains("sourceAuthorityFingerprint", canonical, StringComparison.Ordinal);

        var packetRoot = result.SafeGmPacket!;
        var safeRequest = packetRoot["requests"]!.AsArray()[0]!.AsObject();
        Assert.Equal("resource_resolution_test", safeRequest["requestId"]!.GetValue<string>());
        Assert.Equal("Здоровье", safeRequest["resourceLabel"]!.GetValue<string>());
        Assert.Equal(
            "от 0 до 5",
            safeRequest["allowedResults"]!.AsArray()[1]!["amountInstruction"]!
                .GetValue<string>());
        var packet = packetRoot.ToJsonString();
        Assert.DoesNotContain("player_current", packet, StringComparison.Ordinal);
        Assert.DoesNotContain("effect_test_bleeding", packet, StringComparison.Ordinal);
        Assert.DoesNotContain("turn_42_effect_2", packet, StringComparison.Ordinal);
        Assert.DoesNotContain("sha256:", packet, StringComparison.Ordinal);
        Assert.DoesNotContain("resourceOwnerId", packet, StringComparison.Ordinal);
        Assert.DoesNotContain("game_state/", packet, StringComparison.Ordinal);
    }

    [Fact]
    public void CreatePending_BindsCompanionRequestsAndRejectsMoreThan64()
    {
        var pair = ResourcePendingResolutionState.CreatePending(
            canonicalJson: null,
            new[]
            {
                Draft(effectId: "effect_alpha", triggerId: "trigger_alpha"),
                Draft(effectId: "effect_beta", triggerId: "trigger_beta")
            },
            ResourceDefinitionCatalog.CreateBuiltIn(),
            AllocateIds("resource_resolution_alpha", "resource_resolution_beta"),
            new DateTimeOffset(2026, 8, 21, 15, 30, 0, TimeSpan.Zero));
        var tooMany = ResourcePendingResolutionState.CreatePending(
            canonicalJson: null,
            Enumerable.Range(0, 65)
                .Select(index => Draft(
                    effectId: $"effect_{index:D2}",
                    triggerId: $"trigger_{index:D2}"))
                .ToArray(),
            ResourceDefinitionCatalog.CreateBuiltIn(),
            () => "resource_resolution_" + Guid.NewGuid().ToString("N"),
            new DateTimeOffset(2026, 8, 21, 15, 30, 0, TimeSpan.Zero));

        Assert.True(pair.IsValid, Format(pair.Issues));
        var requests = pair.State!.Requests.ToDictionary(
            static request => request.RequestId,
            StringComparer.Ordinal);
        Assert.Equal(
            new[] { "resource_resolution_beta" },
            requests["resource_resolution_alpha"].RequiredCompanions);
        Assert.Equal(
            new[] { "resource_resolution_alpha" },
            requests["resource_resolution_beta"].RequiredCompanions);
        Assert.Contains(tooMany.Issues, issue =>
            issue.Code == "resource_pending_request_limit_exceeded");
        Assert.Null(tooMany.State);
    }

    [Theory]
    [InlineData("deterministic", "resource_pending_resolution_mode_forbidden")]
    [InlineData("bounded_receipt", "resource_pending_bounds_invalid")]
    public void CreatePending_RejectsDeterministicRoutesAndInvalidBounds(
        string resolutionMode,
        string expectedCode)
    {
        var draft = string.Equals(
                expectedCode,
                "resource_pending_bounds_invalid",
                StringComparison.Ordinal)
            ? Draft(resolutionMode: resolutionMode) with
            {
                MinimumAmount = 6,
                MaximumAmount = 5
            }
            : Draft(resolutionMode: resolutionMode);

        var result = CreatePending(draft);

        Assert.Contains(result.Issues, issue => issue.Code == expectedCode);
        Assert.Null(result.State);
    }

    [Fact]
    public void ParseCanonical_RejectsUnknownDuplicateMalformedAndConfusableRequests()
    {
        var valid = CreatePending(Draft()).State!.ToCanonicalJson();
        var unknown = JsonNode.Parse(valid)!.AsObject();
        unknown["unexpected"] = true;
        var duplicateProperty = valid.Replace(
            "\"schemaVersion\":2",
            "\"schemaVersion\":2,\"schemaVersion\":2",
            StringComparison.Ordinal);
        var confusable = JsonNode.Parse(valid)!.AsObject();
        confusable["requests"]!.AsArray().Add(
            confusable["requests"]![0]!.DeepClone());
        confusable["requests"]![1]!["requestId"] = "RESOURCE_RESOLUTION_TEST";

        var unknownResult = ResourcePendingResolutionState.ParseCanonical(
            unknown.ToJsonString(),
            ResourceDefinitionCatalog.CreateBuiltIn(),
            allowMissingPristine: false);
        var duplicateResult = ResourcePendingResolutionState.ParseCanonical(
            duplicateProperty,
            ResourceDefinitionCatalog.CreateBuiltIn(),
            allowMissingPristine: false);
        var malformedResult = ResourcePendingResolutionState.ParseCanonical(
            "[]",
            ResourceDefinitionCatalog.CreateBuiltIn(),
            allowMissingPristine: false);
        var confusableResult = ResourcePendingResolutionState.ParseCanonical(
            confusable.ToJsonString(),
            ResourceDefinitionCatalog.CreateBuiltIn(),
            allowMissingPristine: false);

        Assert.Contains(unknownResult.Issues, issue =>
            issue.Code == "resource_pending_unknown_field");
        Assert.Contains(duplicateResult.Issues, issue =>
            issue.Code == "resource_pending_duplicate_property");
        Assert.Contains(malformedResult.Issues, issue =>
            issue.Code == "resource_pending_root_invalid");
        Assert.Contains(confusableResult.Issues, issue =>
            issue.Code == "resource_pending_request_identity_confusable");
    }

    [Theory]
    [InlineData("effectAuthority")]
    [InlineData("sourceAuthority")]
    [InlineData("targetAuthority")]
    [InlineData("resourceAuthority")]
    public void ParseCanonical_RejectsTamperedAuthorityBindings(string field)
    {
        var root = JsonNode.Parse(CreatePending(Draft()).State!.ToCanonicalJson())!
            .AsObject();
        root["requests"]![0]![field]!["bindingKind"] = "opaque";

        var result = ResourcePendingResolutionState.ParseCanonical(
            root.ToJsonString(),
            ResourceDefinitionCatalog.CreateBuiltIn(),
            allowMissingPristine: false);

        Assert.Contains(result.Issues, issue =>
            issue.Code == "resource_pending_authority_binding_invalid");
        Assert.Null(result.State);
    }

    [Theory]
    [InlineData("same_turn_ref", "turn_42:effect_ref")]
    [InlineData("permanent", "effect_other")]
    public void CreatePending_RejectsInvalidEffectAuthorityBindings(
        string bindingKind,
        string authorityId)
    {
        var result = CreatePending(Draft() with
        {
            EffectAuthority = new ResourcePendingAuthorityBinding(
                bindingKind,
                authorityId)
        });

        Assert.Contains(result.Issues, issue =>
            issue.Code == "resource_pending_effect_authority_invalid");
        Assert.Null(result.State);
    }

    [Theory]
    [InlineData("same_turn_ref", "turn_42:effect_ref")]
    [InlineData("permanent", "effect_other")]
    public void ParseCanonical_RejectsInvalidEffectAuthorityBindings(
        string bindingKind,
        string authorityId)
    {
        var root = JsonNode.Parse(CreatePending(Draft()).State!.ToCanonicalJson())!
            .AsObject();
        root["requests"]![0]!["effectAuthority"]!["bindingKind"] = bindingKind;
        root["requests"]![0]!["effectAuthority"]!["authorityId"] = authorityId;

        var result = ResourcePendingResolutionState.ParseCanonical(
            root.ToJsonString(),
            ResourceDefinitionCatalog.CreateBuiltIn(),
            allowMissingPristine: false);

        Assert.Contains(result.Issues, issue =>
            issue.Code == "resource_pending_effect_authority_invalid");
        Assert.Null(result.State);
    }

    [Fact]
    public void CreatePending_AllowsAcceptedEffectAuthorityAndOtherSameTurnBindings()
    {
        var result = CreatePending(Draft() with
        {
            EffectAuthority = new ResourcePendingAuthorityBinding(
                "accepted_application",
                "turn_42:effect_application"),
            SourceAuthority = new ResourcePendingAuthorityBinding(
                "same_turn_ref",
                "turn_42:source_application"),
            TargetAuthority = new ResourcePendingAuthorityBinding(
                "same_turn_ref",
                "turn_42:target_application"),
            ResourceAuthority = new ResourcePendingAuthorityBinding(
                "same_turn_ref",
                "turn_42:resource_application")
        });

        Assert.True(result.IsValid, Format(result.Issues));
        var request = Assert.Single(result.State!.Requests);
        Assert.Equal("accepted_application", request.EffectAuthority.BindingKind);
        Assert.Equal("same_turn_ref", request.SourceAuthority.BindingKind);
        Assert.Equal("same_turn_ref", request.TargetAuthority.BindingKind);
        Assert.Equal("same_turn_ref", request.ResourceAuthority.BindingKind);
    }

    [Fact]
    public void Resolve_NarratedNoStateChangeConsumesRequestWithoutMutation()
    {
        var state = CreatePending(Draft()).State!;
        var receipt = Receipt("narrated_no_state_change");

        var result = state.Resolve(
            new JsonArray(receipt),
            Context(),
            ResourceDefinitionCatalog.CreateBuiltIn());

        Assert.True(result.IsValid, Format(result.Issues));
        Assert.Empty(result.Mutations);
        Assert.Empty(result.SourceExports);
        Assert.Empty(result.StateAfterImage!.Requests);
        var terminal = Assert.Single(result.StateAfterImage.TerminalReceipts);
        Assert.Equal("resource_resolution_test", terminal.RequestId);
        Assert.Equal("narrated_no_state_change", terminal.ResultKind);
        Assert.Null(terminal.Amount);
        Assert.Equal("resolved_and_consumed", terminal.State);
    }

    [Fact]
    public void Resolve_InBoundDeltaRestoresProtectedMutationAuthority()
    {
        var state = CreatePending(Draft()).State!;

        var result = state.Resolve(
            new JsonArray(Receipt("resource_delta", amount: 3)),
            Context(),
            ResourceDefinitionCatalog.CreateBuiltIn());

        Assert.True(result.IsValid, Format(result.Issues));
        var mutation = Assert.Single(result.Mutations);
        Assert.Equal("turn_42_effect_2", mutation.EventRef);
        Assert.Equal(
            new ResourceCoordinate(
                "mortal_world",
                ResourceOwnerKind.Player,
                "player_current",
                "health"),
            mutation.Coordinate);
        Assert.Equal(3m, mutation.Amount);
        Assert.Equal("bounded_receipt", mutation.Source.SourceKind);
        Assert.Equal("resource_resolution_test", mutation.Source.SourceId);
        Assert.Equal(ResourceOperation.Damage, mutation.Source.Operation);
        Assert.Equal("resource_resolution_test", mutation.ReceiptId);
        var source = Assert.Single(result.SourceExports);
        Assert.Equal("bounded_receipt", source.SourceKind);
        Assert.Equal("resource_resolution_test", source.SourceId);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("partial")]
    [InlineData("extra")]
    public void Resolve_RejectsMissingPartialOrExtraReceiptSets(string variant)
    {
        var created = ResourcePendingResolutionState.CreatePending(
            canonicalJson: null,
            new[]
            {
                Draft(effectId: "effect_alpha", triggerId: "trigger_alpha"),
                Draft(effectId: "effect_beta", triggerId: "trigger_beta")
            },
            ResourceDefinitionCatalog.CreateBuiltIn(),
            AllocateIds("resource_resolution_alpha", "resource_resolution_beta"),
            new DateTimeOffset(2026, 8, 21, 15, 30, 0, TimeSpan.Zero));
        var receipts = variant switch
        {
            "missing" => new JsonArray(),
            "partial" => new JsonArray(Receipt(
                "resource_delta",
                amount: 2,
                requestId: "resource_resolution_alpha")),
            "extra" => new JsonArray(
                Receipt(
                    "resource_delta",
                    amount: 2,
                    requestId: "resource_resolution_alpha"),
                Receipt(
                    "resource_delta",
                    amount: 2,
                    requestId: "resource_resolution_beta"),
                Receipt(
                    "resource_delta",
                    amount: 2,
                    requestId: "resource_resolution_unknown")),
            _ => throw new ArgumentOutOfRangeException(nameof(variant))
        };

        var result = created.State!.Resolve(
            receipts,
            Context(),
            ResourceDefinitionCatalog.CreateBuiltIn());

        Assert.Contains(result.Issues, issue => issue.Code == variant switch
        {
            "missing" => "resource_pending_receipt_missing",
            "partial" => "resource_pending_companion_missing",
            "extra" => "resource_pending_receipt_extra",
            _ => throw new ArgumentOutOfRangeException(nameof(variant))
        });
        Assert.Null(result.StateAfterImage);
        Assert.Empty(result.Mutations);
    }

    [Theory]
    [InlineData("session_other", "request_turn_42", 42, FingerprintA, "resource_pending_session_stale")]
    [InlineData("session_test", "request_other", 42, FingerprintA, "resource_pending_turn_request_stale")]
    [InlineData("session_test", "request_turn_42", 43, FingerprintA, "resource_pending_turn_stale")]
    [InlineData("session_test", "request_turn_42", 42, FingerprintB, "resource_pending_full_turn_stale")]
    public void Resolve_RejectsStaleFullTurnAuthority(
        string sessionId,
        string acceptedRequestId,
        int turn,
        string fullTurnFingerprint,
        string expectedCode)
    {
        var state = CreatePending(Draft()).State!;

        var result = state.Resolve(
            new JsonArray(Receipt("resource_delta", amount: 3)),
            new ResourcePendingResolutionContext(
                sessionId,
                acceptedRequestId,
                turn,
                fullTurnFingerprint),
            ResourceDefinitionCatalog.CreateBuiltIn());

        Assert.Contains(result.Issues, issue => issue.Code == expectedCode);
        Assert.Null(result.StateAfterImage);
        Assert.Empty(result.Mutations);
    }

    [Theory]
    [InlineData("target")]
    [InlineData("resourceKey")]
    [InlineData("operation")]
    [InlineData("realm")]
    [InlineData("policyFingerprint")]
    public void Resolve_RejectsReceiptRetargetingAndExtraProtectedOperands(
        string forbiddenField)
    {
        var state = CreatePending(Draft()).State!;
        var receipt = Receipt("resource_delta", amount: 3);
        receipt[forbiddenField] = forbiddenField switch
        {
            "target" => new JsonObject
            {
                ["kind"] = "npc",
                ["targetId"] = "npc_other"
            },
            "resourceKey" => "energy",
            "operation" => "restore",
            "realm" => "chaos_sea",
            "policyFingerprint" => FingerprintB,
            _ => throw new ArgumentOutOfRangeException(nameof(forbiddenField))
        };

        var result = state.Resolve(
            new JsonArray(receipt),
            Context(),
            ResourceDefinitionCatalog.CreateBuiltIn());

        Assert.Contains(result.Issues, issue =>
            issue.Code == "resource_pending_receipt_unknown_field");
        Assert.Null(result.StateAfterImage);
        Assert.Empty(result.Mutations);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(6)]
    [InlineData(1.5)]
    public void Resolve_RejectsOutOfBoundsOrQuantumMisalignedDelta(decimal amount)
    {
        var state = CreatePending(Draft()).State!;

        var result = state.Resolve(
            new JsonArray(Receipt("resource_delta", amount)),
            Context(),
            ResourceDefinitionCatalog.CreateBuiltIn());

        Assert.Contains(result.Issues, issue => issue.Code ==
            (amount == 1.5m
                ? "resource_pending_receipt_quantum_invalid"
                : "resource_pending_receipt_out_of_bounds"));
        Assert.Null(result.StateAfterImage);
        Assert.Empty(result.Mutations);
    }

    [Theory]
    [InlineData("narrated_no_state_change", true)]
    [InlineData("resource_delta", false)]
    public void Resolve_EnforcesClosedReceiptShape(
        string resultKind,
        bool addForbiddenAmount)
    {
        var state = CreatePending(Draft()).State!;
        var receipt = Receipt(
            resultKind,
            amount: resultKind == "resource_delta" ? 3 : null);
        if (addForbiddenAmount)
            receipt["amount"] = 0;
        else
            receipt.Remove("reason");

        var result = state.Resolve(
            new JsonArray(receipt),
            Context(),
            ResourceDefinitionCatalog.CreateBuiltIn());

        Assert.Contains(result.Issues, issue => issue.Code ==
            (addForbiddenAmount
                ? "resource_pending_receipt_amount_forbidden"
                : "resource_pending_receipt_reason_invalid"));
        Assert.Null(result.StateAfterImage);
    }

    [Fact]
    public void Resolve_ExactReplayReturnsTerminalEvidenceAndConflictFailsClosed()
    {
        var state = CreatePending(Draft()).State!;
        var receipt = Receipt("resource_delta", amount: 3);
        var first = state.Resolve(
            new JsonArray(receipt),
            Context(),
            ResourceDefinitionCatalog.CreateBuiltIn());

        var replay = first.StateAfterImage!.Resolve(
            new JsonArray(receipt.DeepClone()),
            Context(),
            ResourceDefinitionCatalog.CreateBuiltIn());
        var conflict = first.StateAfterImage.Resolve(
            new JsonArray(Receipt("resource_delta", amount: 4)),
            Context(),
            ResourceDefinitionCatalog.CreateBuiltIn());

        Assert.True(replay.IsValid, Format(replay.Issues));
        Assert.Empty(replay.Mutations);
        Assert.Single(replay.ReplayedTerminalReceipts);
        Assert.Equal(
            first.StateAfterImage.ToCanonicalJson(),
            replay.StateAfterImage!.ToCanonicalJson());
        Assert.Contains(conflict.Issues, issue =>
            issue.Code == "resource_pending_receipt_replay_conflict");
        Assert.Null(conflict.StateAfterImage);
        Assert.Empty(conflict.Mutations);
    }

    [Fact]
    public void ParseCanonical_V1RootIsRejectedWithoutMigration()
    {
        var root = JsonNode.Parse(CreatePending(Draft()).State!.ToCanonicalJson())!
            .AsObject();
        root["schemaVersion"] = 1;

        var result = ResourcePendingResolutionState.ParseCanonical(
            root.ToJsonString(),
            ResourceDefinitionCatalog.CreateBuiltIn(),
            allowMissingPristine: false);

        Assert.Contains(result.Issues, issue =>
            issue.Code == "resource_pending_schema_invalid" &&
            string.Equals(issue.Expected, "2", StringComparison.Ordinal));
        Assert.Null(result.State);
    }

    [Fact]
    public void CreatePending_V2CausalAuthorityRoundTripsExactlyAndStaysPrivate()
    {
        var created = CreatePending(Draft());

        Assert.True(created.IsValid, Format(created.Issues));
        var canonical = created.State!.ToCanonicalJson();
        var root = JsonNode.Parse(canonical)!.AsObject();
        Assert.Equal(2, root["schemaVersion"]!.GetValue<int>());
        var request = Assert.IsType<JsonObject>(Assert.Single(
            Assert.IsType<JsonArray>(root["requests"])));
        var causal = Assert.IsType<JsonObject>(request["causalAuthority"]);
        Assert.Equal("effect_test_bleeding", causal["effectId"]!.GetValue<string>());
        Assert.Equal("trigger_periodic_damage", causal["triggerId"]!.GetValue<string>());
        Assert.Equal("turn_42_effect_2", causal["activationEventRef"]!.GetValue<string>());
        Assert.Equal("turn_42_damage_applied", causal["triggerEventRef"]!.GetValue<string>());
        Assert.Equal(
            "resource_operation_damage_1",
            causal["resourceProducerOperationKey"]!.GetValue<string>());
        Assert.Equal(7, causal["priority"]!.GetValue<int>());
        Assert.Equal(3L, causal["activationOrdinal"]!.GetValue<long>());
        Assert.True(causal["consumesUse"]!.GetValue<bool>());
        Assert.Equal(2, causal["usesBefore"]!.GetValue<int>());
        Assert.Equal("component_story_resolution", causal["componentId"]!.GetValue<string>());
        Assert.Equal("component_followup", causal["afterComponentId"]!.GetValue<string>());
        Assert.Equal(FingerprintA, causal["candidateFingerprint"]!.GetValue<string>());
        Assert.Equal(
            FingerprintB,
            causal["transcriptPrefixFingerprint"]!.GetValue<string>());
        Assert.Equal(0, causal["waveOrdinal"]!.GetValue<int>());

        var parsed = ResourcePendingResolutionState.ParseCanonical(
            canonical,
            ResourceDefinitionCatalog.CreateBuiltIn(),
            allowMissingPristine: false);

        Assert.True(parsed.IsValid, Format(parsed.Issues));
        Assert.Equal(canonical, parsed.State!.ToCanonicalJson());
        var safePacket = created.SafeGmPacket!.ToJsonString();
        Assert.DoesNotContain("causalAuthority", safePacket, StringComparison.Ordinal);
        Assert.DoesNotContain("component_story_resolution", safePacket, StringComparison.Ordinal);
        Assert.DoesNotContain("resource_operation_damage_1", safePacket, StringComparison.Ordinal);
        Assert.DoesNotContain(FingerprintA, safePacket, StringComparison.Ordinal);
        Assert.DoesNotContain(FingerprintB, safePacket, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("priority")]
    [InlineData("activationOrdinal")]
    [InlineData("usesBefore")]
    [InlineData("resourceProducerOperationKey")]
    [InlineData("candidateFingerprint")]
    [InlineData("transcriptPrefixFingerprint")]
    public void ParseCanonical_RejectsStaleCausalAuthority(string field)
    {
        var root = JsonNode.Parse(CreatePending(Draft()).State!.ToCanonicalJson())!
            .AsObject();
        var request = Assert.IsType<JsonObject>(root["requests"]![0]);
        var causal = Assert.IsType<JsonObject>(request["causalAuthority"]);
        causal[field] = field switch
        {
            "priority" => JsonValue.Create(8),
            "activationOrdinal" => JsonValue.Create(4L),
            "usesBefore" => JsonValue.Create(1),
            "resourceProducerOperationKey" =>
                JsonValue.Create("resource_operation_damage_other"),
            "candidateFingerprint" => JsonValue.Create(FingerprintB),
            "transcriptPrefixFingerprint" => JsonValue.Create(FingerprintA),
            _ => throw new ArgumentOutOfRangeException(nameof(field))
        };

        var result = ResourcePendingResolutionState.ParseCanonical(
            root.ToJsonString(),
            ResourceDefinitionCatalog.CreateBuiltIn(),
            allowMissingPristine: false);

        Assert.Contains(result.Issues, issue =>
            issue.Code == "resource_pending_replay_fingerprint_mismatch");
        Assert.Null(result.State);
    }

    [Fact]
    public void Resolve_TerminalRetainsFullRequestAuthorityAndReplayableTypedBinding()
    {
        var created = CreatePending(Draft());
        var createdState = Assert.IsType<ResourcePendingResolutionState>(created.State);
        var requestRoot = JsonNode.Parse(createdState.ToCanonicalJson())!["requests"]![0]!
            .DeepClone();
        var receipt = Receipt("resource_delta", amount: 3);
        var first = createdState.Resolve(
            new JsonArray(receipt.DeepClone()),
            Context(),
            ResourceDefinitionCatalog.CreateBuiltIn());

        Assert.True(first.IsValid, Format(first.Issues));
        var firstState = Assert.IsType<ResourcePendingResolutionState>(
            first.StateAfterImage);
        var terminalRoot = JsonNode.Parse(firstState.ToCanonicalJson())!
            ["terminalReceipts"]![0]!.AsObject();
        Assert.Equal(
            requestRoot.ToJsonString(),
            terminalRoot["requestAuthority"]!.ToJsonString());
        Assert.Equal(
            requestRoot["replayFingerprint"]!.GetValue<string>(),
            terminalRoot["requestAuthorityFingerprint"]!.GetValue<string>());
        var firstBindings = ReadResolvedBindings(first);
        Assert.Single(firstBindings);
        AssertResolvedBinding(
            firstBindings[0],
            requestId: "resource_resolution_test",
            waveOrdinal: 0,
            activationOrdinal: 3L,
            resultKind: "resource_delta",
            amount: 3m);

        var parsedTerminal = ResourcePendingResolutionState.ParseCanonical(
            firstState.ToCanonicalJson(),
            ResourceDefinitionCatalog.CreateBuiltIn(),
            allowMissingPristine: false);
        Assert.True(parsedTerminal.IsValid, Format(parsedTerminal.Issues));

        var replay = parsedTerminal.State!.Resolve(
            new JsonArray(receipt.DeepClone()),
            Context(),
            ResourceDefinitionCatalog.CreateBuiltIn());

        Assert.True(replay.IsValid, Format(replay.Issues));
        Assert.Empty(replay.Mutations);
        Assert.Empty(replay.SourceExports);
        var replayState = Assert.IsType<ResourcePendingResolutionState>(
            replay.StateAfterImage);
        Assert.Single(replayState.TerminalReceipts);
        var replayBinding = Assert.Single(ReadResolvedBindings(replay));
        AssertResolvedBinding(
            replayBinding,
            requestId: "resource_resolution_test",
            waveOrdinal: 0,
            activationOrdinal: 3L,
            resultKind: "resource_delta",
            amount: 3m);
        Assert.Equal(
            firstState.ToCanonicalJson(),
            replayState.ToCanonicalJson());
    }

    [Fact]
    public void ProjectMutationSourceExport_DeltaMatchesFirstParsedAndReplayedBinding()
    {
        var created = CreatePending(Draft());
        var createdState = Assert.IsType<ResourcePendingResolutionState>(created.State);
        var receipt = Receipt("resource_delta", amount: 3);
        var first = createdState.Resolve(
            new JsonArray(receipt.DeepClone()),
            Context(),
            ResourceDefinitionCatalog.CreateBuiltIn());

        Assert.True(first.IsValid, Format(first.Issues));
        var expected = Assert.Single(first.SourceExports);
        var firstBinding = Assert.Single(first.ResolvedPendingBindings);
        Assert.True(ResourcePendingResolutionState.TryProjectMutationSourceExport(
            firstBinding,
            out var projectedFirst));
        Assert.Equal(expected, projectedFirst);

        var firstState = Assert.IsType<ResourcePendingResolutionState>(
            first.StateAfterImage);
        var parsed = ResourcePendingResolutionState.ParseCanonical(
            firstState.ToCanonicalJson(),
            ResourceDefinitionCatalog.CreateBuiltIn(),
            allowMissingPristine: false);
        Assert.True(parsed.IsValid, Format(parsed.Issues));
        var parsedState = Assert.IsType<ResourcePendingResolutionState>(parsed.State);
        var parsedBinding = Assert.Single(parsedState.ResolvedPendingBindings);
        Assert.True(ResourcePendingResolutionState.TryProjectMutationSourceExport(
            parsedBinding,
            out var projectedParsed));
        Assert.Equal(expected, projectedParsed);

        var replay = parsedState.Resolve(
            new JsonArray(receipt.DeepClone()),
            Context(),
            ResourceDefinitionCatalog.CreateBuiltIn());
        Assert.True(replay.IsValid, Format(replay.Issues));
        Assert.Empty(replay.SourceExports);
        var replayBinding = Assert.Single(replay.ResolvedPendingBindings);
        Assert.True(ResourcePendingResolutionState.TryProjectMutationSourceExport(
            replayBinding,
            out var projectedReplay));
        Assert.Equal(expected, projectedReplay);
    }

    [Fact]
    public void ProjectMutationSourceExport_NarratedBindingReturnsFalseAndNull()
    {
        var created = CreatePending(Draft());
        var createdState = Assert.IsType<ResourcePendingResolutionState>(created.State);
        var resolved = createdState.Resolve(
            new JsonArray(Receipt("narrated_no_state_change")),
            Context(),
            ResourceDefinitionCatalog.CreateBuiltIn());

        Assert.True(resolved.IsValid, Format(resolved.Issues));
        var binding = Assert.Single(resolved.ResolvedPendingBindings);
        Assert.False(ResourcePendingResolutionState.TryProjectMutationSourceExport(
            binding,
            out var projected));
        Assert.Null(projected);
    }

    [Fact]
    public void CreateAndResolve_WaveTwoPreservesPriorBindingAndReturnsCausalOrder()
    {
        var waveOne = CreatePending(Draft());
        var waveOneState = Assert.IsType<ResourcePendingResolutionState>(waveOne.State);
        var resolvedOne = waveOneState.Resolve(
            new JsonArray(Receipt("resource_delta", amount: 3)),
            Context(),
            ResourceDefinitionCatalog.CreateBuiltIn());
        Assert.True(resolvedOne.IsValid, Format(resolvedOne.Issues));
        var resolvedOneState = Assert.IsType<ResourcePendingResolutionState>(
            resolvedOne.StateAfterImage);
        var retainedWithoutReceipt = Assert.Single(
            ReadResolvedBindings(resolvedOneState));
        AssertResolvedBinding(
            retainedWithoutReceipt,
            requestId: "resource_resolution_test",
            waveOrdinal: 0,
            activationOrdinal: 3L,
            resultKind: "resource_delta",
            amount: 3m);
        var waveTwo = ResourcePendingResolutionState.CreatePending(
            resolvedOneState.ToCanonicalJson(),
            new[]
            {
                Draft(
                    effectId: "effect_wave_two",
                    triggerId: "trigger_wave_two",
                    eventRef: "turn_42_effect_wave_two",
                    triggerEventRef: "turn_42_wave_one_result",
                    resourceProducerOperationKey: null,
                    priority: 100,
                    activationOrdinal: 4,
                    componentId: "component_wave_two",
                    afterComponentId: null,
                    waveOrdinal: 1),
                Draft(
                    effectId: "effect_wave_two_late",
                    triggerId: "trigger_wave_two_late",
                    eventRef: "turn_42_effect_wave_two_late",
                    triggerEventRef: "turn_42_wave_one_result",
                    resourceProducerOperationKey: null,
                    priority: -100,
                    activationOrdinal: 5,
                    componentId: "component_wave_two_late",
                    afterComponentId: null,
                    waveOrdinal: 1)
            },
            ResourceDefinitionCatalog.CreateBuiltIn(),
            AllocateIds(
                "resource_resolution_wave_two",
                "resource_resolution_wave_two_late"),
            new DateTimeOffset(2026, 8, 21, 15, 31, 0, TimeSpan.Zero));
        Assert.True(waveTwo.IsValid, Format(waveTwo.Issues));
        var waveTwoState = Assert.IsType<ResourcePendingResolutionState>(waveTwo.State);
        Assert.Single(ReadResolvedBindings(waveTwoState));

        var parsedWaveTwo = ResourcePendingResolutionState.ParseCanonical(
            waveTwoState.ToCanonicalJson(),
            ResourceDefinitionCatalog.CreateBuiltIn(),
            allowMissingPristine: false);
        Assert.True(parsedWaveTwo.IsValid, Format(parsedWaveTwo.Issues));
        var parsedWaveTwoState = Assert.IsType<ResourcePendingResolutionState>(
            parsedWaveTwo.State);
        Assert.Single(ReadResolvedBindings(parsedWaveTwoState));

        var resolvedTwo = parsedWaveTwoState.Resolve(
            new JsonArray(
                Receipt(
                    "narrated_no_state_change",
                    requestId: "resource_resolution_wave_two"),
                Receipt(
                    "narrated_no_state_change",
                    requestId: "resource_resolution_wave_two_late")),
            Context(),
            ResourceDefinitionCatalog.CreateBuiltIn());

        Assert.True(resolvedTwo.IsValid, Format(resolvedTwo.Issues));
        var resolvedTwoState = Assert.IsType<ResourcePendingResolutionState>(
            resolvedTwo.StateAfterImage);
        var bindings = ReadResolvedBindings(resolvedTwo);
        Assert.Equal(3, bindings.Count);
        AssertResolvedBinding(
            bindings[0],
            requestId: "resource_resolution_test",
            waveOrdinal: 0,
            activationOrdinal: 3L,
            resultKind: "resource_delta",
            amount: 3m);
        AssertResolvedBinding(
            bindings[1],
            requestId: "resource_resolution_wave_two",
            waveOrdinal: 1,
            activationOrdinal: 4L,
            resultKind: "narrated_no_state_change",
            amount: null);
        Assert.Null(
            bindings[1].RequestAuthority.CausalAuthority.ResourceProducerOperationKey);
        Assert.Null(bindings[1].RequestAuthority.CausalAuthority.AfterComponentId);
        Assert.Equal(100, bindings[1].RequestAuthority.CausalAuthority.Priority);
        AssertResolvedBinding(
            bindings[2],
            requestId: "resource_resolution_wave_two_late",
            waveOrdinal: 1,
            activationOrdinal: 5L,
            resultKind: "narrated_no_state_change",
            amount: null);
        Assert.Equal(-100, bindings[2].RequestAuthority.CausalAuthority.Priority);
        Assert.Equal(3, resolvedTwoState.TerminalReceipts.Count);

        var reparsedTerminalHistory = ResourcePendingResolutionState.ParseCanonical(
            resolvedTwoState.ToCanonicalJson(),
            ResourceDefinitionCatalog.CreateBuiltIn(),
            allowMissingPristine: false);
        Assert.True(
            reparsedTerminalHistory.IsValid,
            Format(reparsedTerminalHistory.Issues));
        Assert.Equal(
            resolvedTwoState.ToCanonicalJson(),
            reparsedTerminalHistory.State!.ToCanonicalJson());
    }

    [Fact]
    public void PendingTurnFingerprints_BindStableTurnContentButExcludeTransportSnapshotTokenReceiptAndPendingSelfState()
    {
        var baseline = PendingFingerprintInput(
            new JsonObject { ["ownerTransitions"] = new JsonArray("owner-a") });
        var changedInternal = PendingFingerprintInput(
            new JsonObject { ["ownerTransitions"] = new JsonArray("owner-b") },
            internalInputsFingerprint: FingerprintB);
        var changedOwnerAuthority = PendingFingerprintInput(
            new JsonObject { ["ownerTransitions"] = new JsonArray("owner-a") },
            ownersFingerprint: FingerprintB);
        var changedPendingSelf = PendingFingerprintInput(
            new JsonObject { ["ownerTransitions"] = new JsonArray("owner-a") },
            pendingInput: new JsonObject { ["requests"] = new JsonArray("self") },
            pendingFingerprint: FingerprintB);
        var changedReceipt = PendingFingerprintInput(
            new JsonObject { ["ownerTransitions"] = new JsonArray("owner-a") },
            receipt: new JsonObject
            {
                ["requestId"] = "resolution-a",
                ["resultKind"] = "narrated_no_state_change",
                ["reason"] = "Результат описан без изменения состояния."
            },
            commandsFingerprint: FingerprintB);
        var changedCommandEnvelope = PendingFingerprintInput(
            new JsonObject { ["ownerTransitions"] = new JsonArray("owner-a") },
            resourceCommandMarker: 2);
        var changedSnapshot = PendingFingerprintInput(
            new JsonObject { ["ownerTransitions"] = new JsonArray("owner-a") },
            snapshotToken: "snapshot-pending-fingerprint-changed");

        var baselineFingerprint =
            AcceptedMechanicsPlanner.CreatePendingFullTurnFingerprint(baseline);
        var baselineSemanticFingerprint =
            AcceptedMechanicsPlanner.CreatePendingSemanticTurnFingerprint(baseline);

        Assert.NotEqual(
            baselineFingerprint,
            AcceptedMechanicsPlanner.CreatePendingFullTurnFingerprint(changedInternal));
        Assert.NotEqual(
            baselineFingerprint,
            AcceptedMechanicsPlanner.CreatePendingFullTurnFingerprint(changedOwnerAuthority));
        Assert.Equal(
            baselineFingerprint,
            AcceptedMechanicsPlanner.CreatePendingFullTurnFingerprint(changedPendingSelf));
        Assert.Equal(
            baselineFingerprint,
            AcceptedMechanicsPlanner.CreatePendingFullTurnFingerprint(changedReceipt));
        Assert.Equal(
            baselineSemanticFingerprint,
            AcceptedMechanicsPlanner.CreatePendingSemanticTurnFingerprint(
                changedInternal));
        Assert.Equal(
            baselineSemanticFingerprint,
            AcceptedMechanicsPlanner.CreatePendingSemanticTurnFingerprint(
                changedOwnerAuthority));
        Assert.Equal(
            baselineSemanticFingerprint,
            AcceptedMechanicsPlanner.CreatePendingSemanticTurnFingerprint(
                changedReceipt));
        Assert.NotEqual(
            baselineSemanticFingerprint,
            AcceptedMechanicsPlanner.CreatePendingSemanticTurnFingerprint(
                changedCommandEnvelope));
        Assert.Equal(
            baselineFingerprint,
            AcceptedMechanicsPlanner.CreatePendingFullTurnFingerprint(
                changedSnapshot));
        Assert.Equal(
            baselineSemanticFingerprint,
            AcceptedMechanicsPlanner.CreatePendingSemanticTurnFingerprint(
                changedSnapshot));
    }

    private static AcceptedMechanicsInput PendingFingerprintInput(
        JsonObject internalInputs,
        string internalInputsFingerprint = FingerprintA,
        string ownersFingerprint = FingerprintA,
        JsonObject? pendingInput = null,
        string pendingFingerprint = FingerprintA,
        JsonObject? receipt = null,
        string commandsFingerprint = FingerprintA,
        int? resourceCommandMarker = null,
        string snapshotToken = "snapshot-pending-fingerprint")
    {
        var effectCommands = new JsonObject
        {
            ["effectChanges"] = new JsonArray(),
            ["effectEventReports"] = new JsonArray(),
            ["effectResolutionReceipts"] = receipt == null
                ? new JsonArray()
                : new JsonArray(receipt.DeepClone())
        };
        var fingerprints = new AcceptedMechanicsAuthorityFingerprints(
            Definitions: FingerprintA,
            Owners: ownersFingerprint,
            ResourceState: FingerprintA,
            ResourceHistory: FingerprintA,
            EffectSources: FingerprintA,
            EffectTargets: FingerprintA,
            EffectCarriers: FingerprintA,
            EffectIdentityIndex: FingerprintA,
            AcceptedEvents: FingerprintA,
            Commands: commandsFingerprint,
            Pending: pendingFingerprint,
            InternalInputs: internalInputsFingerprint,
            WoundCarriers: FingerprintA,
            WoundIdentityIndex: FingerprintA,
            WoundHistory: FingerprintA);
        return new AcceptedMechanicsInput(
            "session-pending-fingerprint",
            "request-pending-fingerprint",
            snapshotToken,
            "mortal_world",
            42,
            new JsonObject { ["acceptedEvents"] = new JsonArray() },
            new JsonObject
            {
                ["resourceDefinitionCreations"] = new JsonArray(),
                ["resourceCapacityChanges"] = new JsonArray(),
                ["resourceChanges"] = resourceCommandMarker.HasValue
                    ? new JsonArray(new JsonObject
                    {
                        ["marker"] = resourceCommandMarker.Value
                    })
                    : new JsonArray()
            },
            effectCommands,
            pendingInput ?? new JsonObject(),
            internalInputs,
            fingerprints,
            new Dictionary<string, CanonicalBeforeImage>(StringComparer.Ordinal),
            Array.Empty<ValidationIssue>());
    }

    private static ResourcePendingResolutionCreationResult CreatePending(
        ResourcePendingResolutionDraft draft) =>
        ResourcePendingResolutionState.CreatePending(
            canonicalJson: null,
            new[] { draft },
            ResourceDefinitionCatalog.CreateBuiltIn(),
            AllocateIds("resource_resolution_test"),
            new DateTimeOffset(2026, 8, 21, 15, 30, 0, TimeSpan.Zero));

    private static ResourcePendingResolutionDraft Draft(
        string resolutionMode = "bounded_receipt",
        string effectId = "effect_test_bleeding",
        string triggerId = "trigger_periodic_damage",
        string eventRef = "turn_42_effect_2",
        string triggerEventRef = "turn_42_damage_applied",
        string? resourceProducerOperationKey = "resource_operation_damage_1",
        int priority = 7,
        int? usesBefore = 2,
        long activationOrdinal = 3,
        bool consumesUse = true,
        string componentId = "component_story_resolution",
        string? afterComponentId = "component_followup",
        int waveOrdinal = 0) =>
        new(
            resolutionMode,
            "session_test",
            "request_turn_42",
            42,
            eventRef,
            effectId,
            new ResourcePendingAuthorityBinding("permanent", effectId),
            new JsonObject
            {
                ["kind"] = "wound",
                ["sourceId"] = "wound_test_torn_side",
                ["definitionKey"] = "bleeding_consequence"
            },
            new ResourcePendingAuthorityBinding(
                "permanent",
                "wound_test_torn_side"),
            new JsonObject
            {
                ["kind"] = "player",
                ["targetId"] = "player_current"
            },
            new ResourcePendingAuthorityBinding("permanent", "player_current"),
            triggerId,
            new ResourceCoordinate(
                "mortal_world",
                ResourceOwnerKind.Player,
                "player_current",
                "health"),
            new ResourcePendingAuthorityBinding("permanent", "player_current"),
            ResourceOperation.Damage,
            0,
            5,
            FingerprintA,
            FingerprintB,
            FingerprintA,
            FingerprintA,
            "Рваная рана",
            "герой",
            "Здоровье",
            "урон",
            new ResourcePendingCausalAuthority(
                effectId,
                triggerId,
                eventRef,
                triggerEventRef,
                resourceProducerOperationKey,
                priority,
                activationOrdinal,
                consumesUse,
                usesBefore,
                componentId,
                afterComponentId,
                FingerprintA,
                FingerprintB,
                waveOrdinal));

    private static IReadOnlyList<ResourcePendingResolvedBinding> ReadResolvedBindings(
        ResourcePendingResolutionState state) => state.ResolvedPendingBindings;

    private static IReadOnlyList<ResourcePendingResolvedBinding> ReadResolvedBindings(
        ResourcePendingResolutionResult result) => result.ResolvedPendingBindings;

    private static void AssertResolvedBinding(
        ResourcePendingResolvedBinding binding,
        string requestId,
        int waveOrdinal,
        long activationOrdinal,
        string resultKind,
        decimal? amount)
    {
        Assert.Equal(requestId, binding.RequestId);
        Assert.Equal(resultKind, binding.ResultKind);
        Assert.Equal(amount, binding.Amount);
        Assert.Equal(waveOrdinal, binding.RequestAuthority.CausalAuthority.WaveOrdinal);
        Assert.Equal(
            activationOrdinal,
            binding.RequestAuthority.CausalAuthority.ActivationOrdinal);
    }

    private static ResourcePendingResolutionContext Context() =>
        new("session_test", "request_turn_42", 42, FingerprintA);

    private static JsonObject Receipt(
        string resultKind,
        decimal? amount = null,
        string requestId = "resource_resolution_test")
    {
        var receipt = new JsonObject
        {
            ["requestId"] = requestId,
            ["resultKind"] = resultKind,
            ["reason"] = "Исход подтверждён рассказчиком."
        };
        if (amount.HasValue)
            receipt["amount"] = amount.Value;
        return receipt;
    }

    private static Func<string> AllocateIds(params string[] ids)
    {
        var queue = new Queue<string>(ids);
        return () => queue.Dequeue();
    }

    private static string Format(IReadOnlyList<ValidationIssue> issues) =>
        string.Join(
            Environment.NewLine,
            issues.Select(static issue =>
                $"{issue.Code}: {issue.FilePath} expected={issue.Expected} actual={issue.Actual}"));
}
