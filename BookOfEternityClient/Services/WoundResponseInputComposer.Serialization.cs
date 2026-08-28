using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static partial class WoundResponseInputComposer
{
    private static JsonObject SerializeOpportunity(WoundOpportunityAuthority value) => new()
    {
        ["schemaVersion"] = 1,
        ["sessionId"] = value.SessionId,
        ["requestId"] = value.RequestId,
        ["snapshotToken"] = value.SnapshotToken,
        ["opportunityId"] = value.OpportunityId,
        ["publicRef"] = value.PublicRef,
        ["eventRef"] = value.EventRef,
        ["owner"] = SerializeOwner(value.Owner),
        ["domain"] = value.Domain,
        ["profileKey"] = value.ProfileKey,
        ["sourceKind"] = value.SourceKind,
        ["sourceId"] = value.SourceId,
        ["sourceState"] = value.SourceState,
        ["minimumSeverityRank"] = value.MinimumSeverityRank,
        ["maximumSeverityRank"] = value.MaximumSeverityRank,
        ["guaranteedTrigger"] = value.GuaranteedTrigger is null
            ? null
            : SerializeGuarantee(value.GuaranteedTrigger),
        ["safeContext"] = new JsonObject
        {
            ["target"] = value.SafeContext.Target,
            ["cause"] = value.SafeContext.Cause,
            ["allowedLocationKinds"] = new JsonArray(
                value.SafeContext.AllowedLocationKinds
                    .Select(static item => (JsonNode)JsonValue.Create(item)!)
                    .ToArray())
        },
        ["inputEvidenceFingerprint"] = value.InputEvidenceFingerprint,
        ["authorityFingerprint"] = value.AuthorityFingerprint
    };

    private static JsonObject SerializeGuarantee(
        WoundGuaranteedTriggerAuthority value) => new()
    {
        ["triggerId"] = value.TriggerId,
        ["sourceKind"] = value.SourceKind,
        ["sourceId"] = value.SourceId,
        ["sourceState"] = value.SourceState,
        ["realm"] = value.Realm,
        ["domain"] = value.Domain,
        ["owner"] = SerializeOwner(value.Owner),
        ["requiredSeverityRank"] = value.RequiredSeverityRank,
        ["materializedAtTurn"] = value.MaterializedAtTurn,
        ["sourceContractFingerprint"] = value.SourceContractFingerprint,
        ["authorityFingerprint"] = value.AuthorityFingerprint
    };

    private static JsonObject SerializeOwner(WoundOwnerCoordinate value) => new()
    {
        ["realm"] = value.Realm,
        ["ownerKind"] = value.OwnerKind,
        ["ownerId"] = value.OwnerId,
        ["carrierPath"] = value.CarrierPath
    };
}
