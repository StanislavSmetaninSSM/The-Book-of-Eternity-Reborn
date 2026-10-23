using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Exercises the production idempotence policy over detached typed requests without initializing a game or issuing authority.
/// </summary>
public sealed class JointQteRegistryEligibilityTests
{
    /// <summary>
    /// Permits reuse only when original binding and persisted recovery would be idempotent for the same held request.
    /// </summary>
    [Fact]
    public void SameCurrentAcceptedStateAndRecoveredHeldRequestPermitDataReuse()
    {
        var state = CreateState();
        Assert.True(AcceptedTurnAuthorityRegistry.IsMortalJointQteCalculationStateIdempotent(state));
        Assert.False(AcceptedTurnAuthorityRegistry.IsMortalJointQteCalculationStateIdempotent(
            state with { HasOpenReceipt = true }));
        Assert.True(AcceptedTurnAuthorityRegistry.IsMortalJointQteCalculationStateIdempotent(state));
    }

    /// <summary>
    /// Preserves original preparation when the optional facade or pure policy lacks required input facts.
    /// </summary>
    [Fact]
    public void MissingEligibilityInputsRequireOriginalPreparation()
    {
        Assert.False(AcceptedTurnAuthorityRegistry.CanReuseMortalJointQteCalculation(
            null!, null!, null!, null!, null!));
        Assert.False(AcceptedTurnAuthorityRegistry.IsMortalJointQteCalculationStateIdempotent(default));
    }

    /// <summary>
    /// Rejects absent or replaced accepted-state identities even when every detached request field remains identical.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingOrReplacedAcceptedStateRequiresOriginalPreparation(bool replaced)
    {
        var state = CreateState() with { CurrentAcceptedState = replaced ? new object() : null };
        Assert.False(AcceptedTurnAuthorityRegistry.IsMortalJointQteCalculationStateIdempotent(state));
    }

    /// <summary>
    /// Keeps stale roots or leases, incomplete recovery and every publication boundary on the original preparation path.
    /// </summary>
    [Theory]
    [InlineData("owner")]
    [InlineData("recovery")]
    [InlineData("restart")]
    [InlineData("handoff")]
    [InlineData("receipt")]
    public void NonIdempotentRegistryStateRequiresOriginalPreparation(string changed)
    {
        var state = CreateState();
        state = changed switch
        {
            "owner" => state with { CurrentRootAndLease = false },
            "recovery" => state with { RecoveryCompleted = false },
            "restart" => state with { HasRestartBlocker = true },
            "handoff" => state with { HasValidatedHandoff = true },
            "receipt" => state with { HasOpenReceipt = true },
            _ => throw new ArgumentOutOfRangeException(nameof(changed))
        };
        Assert.False(AcceptedTurnAuthorityRegistry.IsMortalJointQteCalculationStateIdempotent(state));
    }

    /// <summary>
    /// Requires one exact original request in both recovered sets and no finalized request.
    /// </summary>
    [Theory]
    [InlineData("missing_request")]
    [InlineData("extra_request")]
    [InlineData("different_request")]
    [InlineData("missing_hold")]
    [InlineData("extra_hold")]
    [InlineData("different_hold")]
    [InlineData("finalized")]
    public void ChangedRecoveredRequestSetsRequireOriginalPreparation(string changed)
    {
        var state = CreateState();
        var sameDataReplacement = ReadRequest();
        Assert.NotSame(state.ExpectedRequest, sameDataReplacement);
        Assert.Equal(state.ExpectedRequest.RequestFingerprint, sameDataReplacement.RequestFingerprint);
        state = changed switch
        {
            "missing_request" => state with { Requests = [] },
            "extra_request" => state with { Requests = [state.ExpectedRequest, state.ExpectedRequest] },
            "different_request" => state with { Requests = [sameDataReplacement] },
            "missing_hold" => state with { HeldRequests = [] },
            "extra_hold" => state with { HeldRequests = [state.ExpectedRequest, state.ExpectedRequest] },
            "different_hold" => state with { HeldRequests = [sameDataReplacement] },
            "finalized" => state with { FinalizedRequests = [state.ExpectedRequest] },
            _ => throw new ArgumentOutOfRangeException(nameof(changed))
        };
        Assert.False(AcceptedTurnAuthorityRegistry.IsMortalJointQteCalculationStateIdempotent(state));
    }

    /// <summary>
    /// Refuses candidate finalization metadata for another request, resource authority or reservation.
    /// </summary>
    [Theory]
    [InlineData("request")]
    [InlineData("resource")]
    [InlineData("reservation")]
    public void ChangedFinalizationCompanionRequiresOriginalPreparation(string changed)
    {
        var state = CreateState();
        state = changed switch
        {
            "request" => state with { FinalizationRequestFingerprint = "different request" },
            "resource" => state with { FinalizationResourceFingerprint = "different resource" },
            "reservation" => state with { FinalizationReservationId = "different reservation" },
            _ => throw new ArgumentOutOfRangeException(nameof(changed))
        };
        Assert.False(AcceptedTurnAuthorityRegistry.IsMortalJointQteCalculationStateIdempotent(state));
    }

    /// <summary>
    /// Reads a genuine detached request from immutable fixture INPUT through the production command codec.
    /// </summary>
    /// <returns>
    /// A parsed request with no live registry, reservation or accepted-state capability.
    /// </returns>
    private static MortalWoundTreatmentAttemptRequest ReadRequest()
    {
        var root = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
            "Fixtures", "treatment-semantic-guaranteed-v1.json")))!.AsObject();
        var issues = new List<ValidationIssue>();
        Assert.True(MortalWoundTreatmentCommandCodec.TryParseRequest(root["Request"]!.AsObject(),
            "joint-qte-registry.request", issues, out var request), JsonSerializer.Serialize(issues));
        Assert.NotNull(request);
        return request;
    }

    /// <summary>
    /// Creates only non-authorizing policy facts with exact accepted and recovered-request identities.
    /// </summary>
    /// <returns>
    /// An idempotent policy input whose accepted identity is an opaque object and whose request is detached.
    /// </returns>
    private static AcceptedTurnAuthorityRegistry.MortalJointQteRegistryEligibilityState CreateState()
    {
        var acceptedIdentity = new object();
        var request = ReadRequest();
        return new(acceptedIdentity, acceptedIdentity, true, true, [request], [request], [], request,
            request.RequestFingerprint, request.ResourceAuthority.AuthorityFingerprint,
            request.ResourceAuthority.ReservationId, false, false, false);
    }
}
