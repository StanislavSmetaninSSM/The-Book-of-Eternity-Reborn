using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal enum AcceptedMechanicsOwnerTransitionKind
{
    MortalNpcCreation,
    AfterlifeGuardianGacha
}

internal sealed class AcceptedMechanicsOwnerTransition
{
    private readonly JsonObject _payload;

    private AcceptedMechanicsOwnerTransition(
        AcceptedMechanicsOwnerTransitionKind kind,
        string path,
        string ownerRef,
        string permanentOwnerId,
        JsonObject payload)
    {
        if (!Enum.IsDefined(kind))
            throw new ArgumentOutOfRangeException(nameof(kind));
        AcceptedMechanicsPlanBinding.ValidatePath(path, nameof(path));
        if (!ResourceMaterializationContract.IsExactIdentifier(ownerRef))
            throw new ArgumentException("Expected one exact owner ref.", nameof(ownerRef));
        if (!ResourceMaterializationContract.IsExactIdentifier(permanentOwnerId))
        {
            throw new ArgumentException(
                "Expected one exact permanent owner ID.",
                nameof(permanentOwnerId));
        }

        Kind = kind;
        Path = path;
        OwnerRef = ownerRef;
        PermanentOwnerId = permanentOwnerId;
        _payload = (payload ?? throw new ArgumentNullException(nameof(payload)))
            .DeepClone().AsObject();
    }

    internal AcceptedMechanicsOwnerTransitionKind Kind { get; }

    internal string Path { get; }

    internal string OwnerRef { get; }

    internal string PermanentOwnerId { get; }

    internal JsonObject Payload => _payload.DeepClone().AsObject();

    internal JsonObject ExpectedResourceMaterialization =>
        _payload["expectedResourceMaterialization"] is JsonObject materialization
            ? materialization.DeepClone().AsObject()
            : throw new InvalidOperationException(
                "Mortal NPC transition payload has no resource materialization.");

    internal static AcceptedMechanicsOwnerTransition CreateMortalNpcCreation(
        string ownerRef,
        string permanentOwnerId,
        JsonObject expectedResourceMaterialization) =>
        new(
            AcceptedMechanicsOwnerTransitionKind.MortalNpcCreation,
            "game_state/npcs/npc_core.json",
            ownerRef,
            permanentOwnerId,
            new JsonObject
            {
                ["expectedResourceMaterialization"] =
                    expectedResourceMaterialization.DeepClone()
            });

    internal static AcceptedMechanicsOwnerTransition CreateAfterlifeGuardianGacha(
        string guardianId,
        string returnCycleId,
        JsonArray expectedHistoryPrefix,
        JsonArray appendedHistoryEntries)
    {
        if (!ResourceMaterializationContract.IsExactIdentifier(returnCycleId))
            throw new ArgumentException("Expected one exact return-cycle ID.", nameof(returnCycleId));
        ArgumentNullException.ThrowIfNull(expectedHistoryPrefix);
        ArgumentNullException.ThrowIfNull(appendedHistoryEntries);
        if (appendedHistoryEntries.Count == 0)
        {
            throw new ArgumentException(
                "Guardian gacha transition requires at least one history append.",
                nameof(appendedHistoryEntries));
        }
        return new AcceptedMechanicsOwnerTransition(
            AcceptedMechanicsOwnerTransitionKind.AfterlifeGuardianGacha,
            "game_state/meta/guardians.json",
            guardianId,
            guardianId,
            new JsonObject
            {
                ["returnCycleId"] = returnCycleId,
                ["expectedHistoryPrefix"] = expectedHistoryPrefix.DeepClone(),
                ["appendedHistoryEntries"] = appendedHistoryEntries.DeepClone()
            });
    }

    internal AcceptedMechanicsOwnerTransition Clone() =>
        new(
            Kind,
            Path,
            OwnerRef,
            PermanentOwnerId,
            _payload);

    internal JsonObject ToFingerprintNode() =>
        new()
        {
            ["kind"] = Kind.ToString(),
            ["path"] = Path,
            ["ownerRef"] = OwnerRef,
            ["permanentOwnerId"] = PermanentOwnerId,
            ["payload"] = _payload.DeepClone()
        };
}
