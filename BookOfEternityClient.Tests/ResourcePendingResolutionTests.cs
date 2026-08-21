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
            "\"schemaVersion\":1",
            "\"schemaVersion\":1,\"schemaVersion\":1",
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
        string triggerId = "trigger_periodic_damage") =>
        new(
            resolutionMode,
            "session_test",
            "request_turn_42",
            42,
            "turn_42_effect_2",
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
            "Рваная рана",
            "герой",
            "Здоровье",
            "урон");

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
