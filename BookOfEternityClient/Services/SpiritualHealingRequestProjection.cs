using BookOfEternityClient.Configuration;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

/// <summary>One shared scalar program and GM graph references; never publication authority.</summary>
internal static class SpiritualHealingRequestProjection
{
    internal static IReadOnlyList<SpiritualWoundRecoveryMember> ReadNaturalMembers(
        WoundCarrierCatalog catalog, JsonObject? profiles, JsonObject? soul, string realm)
    {
        var wounds = catalog.Occurrences.Select(o => o.Wound).Where(w => w.Lifecycle == "active" &&
            w.Classification.Domain == "spiritual" && w.Severity.Rank is >= 1 and <= 4 &&
            w.Owner.Realm == realm && w.Owner.CarrierPath == WoundCarrierCatalog.AfterlifeProfilesPath)
            .OrderBy(w => w.Owner.OwnerKind, StringComparer.Ordinal).ThenBy(w => w.Owner.OwnerId, StringComparer.Ordinal)
            .ThenBy(w => w.WoundId, StringComparer.Ordinal).ToArray();
        var members = new List<SpiritualWoundRecoveryMember>();
        foreach (var owner in wounds.GroupBy(w => w.Owner))
        {
            var matching = (profiles?["profiles"] as JsonArray)?.OfType<JsonObject>()
                .Where(p => WoundCarrierCollectionAuthority.AfterlifeProfileMatches(p, owner.Key)).ToArray();
            if (matching is not { Length: 1 } || !AfterlifeEntityProfileState.TryReadRequiredWoundArtTier(
                    matching[0], AfterlifeSpiritualConflictState.SpiritualHealingArtId, out _, out var tier))
                throw new InvalidDataException("Natural recovery requires one persistent owner and explicit healing tier.");
            if (owner.Key.OwnerKind == "player_soul")
            {
                if (owner.Key.OwnerId != "player_soul" || soul is null ||
                    !AfterlifeEntityProfileState.TryNormalizeEffectRealm(soul["currentRealm"]?.GetValue<string>(), out var soulRealm) ||
                    soulRealm != realm || !AfterlifeSpiritualConflictState.TryValidateCurrentRequiredWoundArtAuthority(soul, out _) ||
                    soul["afterlifeCombatProfile"]!["artTiers"]![AfterlifeSpiritualConflictState.SpiritualHealingArtId]!.GetValue<int>() != tier)
                    throw new InvalidDataException("Natural player recovery requires its exact signed soul/profile art mirror.");
            }
            members.AddRange(owner.Select(w => new SpiritualWoundRecoveryMember(w, tier)));
        }
        return members.ToArray();
    }

    internal static (WoundMaterializationEnvelope[] Wounds, SpiritualWoundStage[] Stages) BuildProgram(
        AfterlifeWoundTreatmentAuthority.SourceData data, JsonObject request, SpiritualHealingResolution resolution)
    {
        var attempt = request["attempt"]!.AsObject();
        var members = data.NaturalMembers.OrderBy(m => m.Wound.WoundId == data.Wound.WoundId ? 0 : 1)
            .ThenBy(m => m.Wound.Owner.OwnerKind, StringComparer.Ordinal).ThenBy(m => m.Wound.Owner.OwnerId, StringComparer.Ordinal)
            .ThenBy(m => m.Wound.WoundId, StringComparer.Ordinal).ToArray();
        var wounds = members.Select(m => m.Wound).ToArray();
        var stages = members.SelectMany(m => SpiritualWoundRecoveryPlanner.Build(m.Wound, m.HealingTier,
            attempt["attemptId"]!.GetValue<string>(), attempt["cycleKey"]!.GetValue<string>(),
            m.Wound.WoundId == data.Wound.WoundId ? resolution.Calculation : null)).ToArray();
        return (wounds, stages);
    }

    internal static IEnumerable<(string WoundId, int Rank)> RequiredGraphs(
        IReadOnlyList<WoundMaterializationEnvelope> wounds, IReadOnlyList<SpiritualWoundStage> stages) =>
        stages.Where(s => s.Kind != "heal" && s.SeverityRank < wounds.Single(w => w.WoundId == s.WoundId).Severity.Rank)
            .Select(s => (s.WoundId, s.SeverityRank)).Distinct();

    internal static string GraphRef(string requestFingerprint, string woundId, int rank) =>
        SpiritualWoundRecoveryPlanner.Coordinate("spiritual_healing_graph", requestFingerprint, woundId, rank);
    internal static string ComplicationRef(string requestFingerprint, string id) =>
        SpiritualWoundRecoveryPlanner.Coordinate("spiritual_complication_ref", requestFingerprint, id, 0);
    internal static JsonArray Complications(string requestFingerprint, WoundMaterializationEnvelope wound) => new(
        wound.Complications.Select(c => (JsonNode)new JsonObject
        { ["complicationRef"] = ComplicationRef(requestFingerprint, c.ComplicationId), ["displayName"] = c.DisplayName }).ToArray());

    internal static (WoundMaterializationEnvelope[] Wounds, SpiritualWoundStage[] Stages) BuildNaturalProgram(
        AfterlifeWoundTreatmentAuthority.NaturalSourceData data) =>
        BuildNaturalProgram(data.Realm, data.CycleOrdinal, data.Members);

    internal static (WoundMaterializationEnvelope[] Wounds, SpiritualWoundStage[] Stages) BuildNaturalProgram(
        string realm, int ordinal, IReadOnlyList<SpiritualWoundRecoveryMember> members)
    {
        var cycle = AfterlifeWoundTreatmentAuthority.CycleKey(realm, ordinal);
        return (members.Select(m => m.Wound).ToArray(), members.SelectMany(m =>
            SpiritualWoundRecoveryPlanner.Build(m.Wound, m.HealingTier, null, cycle, null)).ToArray());
    }

    internal static JsonObject OwnerJson(WoundOwnerCoordinate owner) => new()
    {
        ["realm"] = owner.Realm, ["ownerKind"] = owner.OwnerKind,
        ["ownerId"] = owner.OwnerId, ["carrierPath"] = owner.CarrierPath
    };

    internal static JsonObject CreateNatural(AfterlifeWoundTreatmentAuthority.NaturalSourceData data) =>
        CreateNatural(data.Realm, data.CycleOrdinal, data.SourceFingerprint, data.Members);

    internal static JsonObject CreateNatural(string realm, int ordinal, string fingerprint,
        IReadOnlyList<SpiritualWoundRecoveryMember> members)
    {
        var program = BuildNaturalProgram(realm, ordinal, members);
        return new JsonObject
        {
            ["schemaVersion"] = 2, ["mode"] = "natural", ["sourceFingerprint"] = fingerprint,
            ["cycleOrdinal"] = ordinal, ["cycleKey"] = AfterlifeWoundTreatmentAuthority.CycleKey(realm, ordinal),
            ["cohort"] = new JsonArray(members.Select(m => (JsonNode)new JsonObject
            {
                ["woundId"] = m.Wound.WoundId, ["owner"] = OwnerJson(m.Wound.Owner),
                ["woundName"] = m.Wound.Display.Name, ["healingTier"] = m.HealingTier,
                ["woundFingerprint"] = WoundIdentityState.ComputeSemanticFingerprint(m.Wound)
            }).ToArray()),
            ["destinationGraphs"] = new JsonArray(RequiredGraphs(program.Wounds, program.Stages).Select(g => (JsonNode)new JsonObject
            {
                ["stageRef"] = GraphRef(fingerprint, g.WoundId, g.Rank), ["severityRank"] = g.Rank,
                ["woundName"] = program.Wounds.Single(w => w.WoundId == g.WoundId).Display.Name,
                ["complications"] = Complications(fingerprint, program.Wounds.Single(w => w.WoundId == g.WoundId))
            }).ToArray())
        };
    }

    internal static JsonObject Create(AfterlifeWoundTreatmentAuthority.SourceData data, JsonObject request,
        SpiritualHealingResolution? resolved = null)
    {
        var attempt = request["attempt"]!.AsObject();
        var resolution = resolved ?? SpiritualHealingResolver.ResolvePrepared(data.Source,
            attempt["roll"]!["mode"]!.GetValue<string>(), attempt["roll"]!["values"]!.AsArray().Select(v => v!.GetValue<int>()).ToArray()).Resolution
            ?? throw new InvalidDataException("The retained healing request lost its scalar program.");
        var program = BuildProgram(data, request, resolution);
        var fingerprint = request["requestFingerprint"]!.GetValue<string>();
        var projection = new JsonObject
        {
            ["schemaVersion"] = 1, ["requestFingerprint"] = fingerprint,
            ["attemptId"] = attempt["attemptId"]!.DeepClone(), ["woundName"] = data.Wound.Display.Name,
            ["healingTier"] = data.Source.HealingTier, ["roll"] = attempt["roll"]!.DeepClone(),
            ["activeResult"] = JsonSerializer.SerializeToNode(resolution.Calculation, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed),
            ["destinationGraphs"] = new JsonArray(RequiredGraphs(program.Wounds, program.Stages).Select(g => (JsonNode)new JsonObject
            {
                ["stageRef"] = GraphRef(fingerprint, g.WoundId, g.Rank), ["severityRank"] = g.Rank,
                ["woundName"] = program.Wounds.Single(w => w.WoundId == g.WoundId).Display.Name,
                ["complications"] = Complications(fingerprint, program.Wounds.Single(w => w.WoundId == g.WoundId))
            }).ToArray())
        };
        if (data.ServiceQuote is { } quote)
        {
            projection["schemaVersion"] = 7; projection["mode"] = "service";
            projection["healer"] = OwnerJson(data.Source.Healer); projection["target"] = OwnerJson(data.Source.Target);
            projection["quote"] = quote.ToJson(); projection["playerConfirmationVerified"] = true;
            projection["targetNaturalHealingTier"] = data.NaturalMembers.First(m => m.Wound.Owner == data.Wound.Owner).HealingTier;
        }
        else if (data.AidWitness is not null)
        {
            projection["schemaVersion"] = request["schemaVersion"]!.DeepClone();
            projection["mode"] = "aid";
            projection["healer"] = OwnerJson(data.Source.Healer);
            projection["target"] = OwnerJson(data.Source.Target);
            projection["targetNaturalHealingTier"] = data.NaturalMembers.First(m => m.Wound.Owner == data.Wound.Owner).HealingTier;
            if (SpiritualSelfTreatmentRequestCodec.IsReceiveAidHeader(request))
            {
                projection["direction"] = SpiritualSelfTreatmentRequestCodec.AidDirection(request["schemaVersion"]!.GetValue<int>());
                projection["playerConfirmationVerified"] = true; // Exact client original, not GM consent.
            }
            if (request["schemaVersion"]!.GetValue<int>() == 5) projection["direction"] = "player_to_resident";
            if (SpiritualTreatmentParticipation.ReadCompensation(data.AidWitness["record"]!.AsObject()) is { } compensation)
                projection["compensation"] = SpiritualTreatmentParticipation.CompensationJson(compensation);
            var locality = SpiritualTreatmentParticipation.RecordLocality(data.AidWitness["record"]!.AsObject());
            if (locality.Realm == "shining_abode") projection["locality"] = SpiritualTreatmentParticipation.LocalityJson(locality);
            projection["participationVerified"] = true; // GM comparison DATA, never permission to author a grant or request.
        }
        return projection;
    }
}
