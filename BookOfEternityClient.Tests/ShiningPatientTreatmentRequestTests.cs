using System.Text.Json.Nodes;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>Independent codec DATA only; synthetic witness digests cannot authorize a live treatment.</summary>
public sealed class ShiningPatientTreatmentRequestTests
{
    [Theory]
    [InlineData("resident")][InlineData("radiant_actor")]
    public void FamilyEightRetainsPlayerDrawSeparateNaturalTierAndExactPatientWithoutPayment(string kind)
    {
        var (context, selected, data) = Inputs(kind);
        var frozen = data.AidWitness!.ToJsonString();
        var request = SpiritualSelfTreatmentRequestCodec.CreateActive(context, selected, data, [14]);
        Assert.Equal(8, request["schemaVersion"]!.GetValue<int>());
        Assert.Equal("player_to_shining_entity", request["direction"]!.GetValue<string>());
        Assert.False(SpiritualSelfTreatmentRequestCodec.IsReceiveAidHeader(request));
        Assert.True(SpiritualSelfTreatmentRequestCodec.IsExactActive(request, context, selected, data));
        Assert.Equal(selected, SpiritualSelfTreatmentRequestCodec.ReadActiveSelection(request));
        foreach (var field in new[] { "playerConfirmation", "currencyInvariant", "payment", "compensation" }) Assert.False(request.ContainsKey(field));
        var projection = SpiritualHealingRequestProjection.Create(data, request);
        Assert.Equal("player_to_shining_entity", projection["direction"]!.GetValue<string>());
        Assert.False(projection.ContainsKey("playerConfirmationVerified"));
        Assert.Equal(3, projection["healingTier"]!.GetValue<int>());
        Assert.Equal(0, projection["targetNaturalHealingTier"]!.GetValue<int>());
        Assert.Equal(kind, projection["target"]!["ownerKind"]!.GetValue<string>());
        Assert.Equal(WoundApplicationService.ShiningPatientAidActionText, WoundApplicationService.ActionFor(selected));
        request["participation"]!["record"]!["readableEvidence"] = "Изменён экспорт";
        projection["roll"]!["values"]![0] = 1;
        Assert.Equal(frozen, data.AidWitness.ToJsonString());
        Assert.Equal(14, SpiritualSelfTreatmentRequestCodec.CreateActive(context, selected, data, [14])["attempt"]!["roll"]!["values"]![0]!.GetValue<int>());
    }

    [Theory]
    [InlineData("resident", "revoked")][InlineData("resident", "conscious")][InlineData("resident", "capable")]
    [InlineData("resident", "same_turn")][InlineData("resident", "intervening_turn")][InlineData("resident", "expired")]
    [InlineData("radiant_actor", "revoked")][InlineData("radiant_actor", "conscious")][InlineData("radiant_actor", "capable")]
    [InlineData("radiant_actor", "same_turn")][InlineData("radiant_actor", "intervening_turn")][InlineData("radiant_actor", "expired")]
    public void RegeneratedRequestRefusesUnavailableConsentWithoutExpiryMaskingOtherChecks(string kind, string change)
    {
        var (context, selected, data) = Inputs(kind);
        Assert.NotNull(SpiritualSelfTreatmentRequestCodec.CreateActive(context, selected, data, [14]));
        var record = data.AidWitness!["record"]!.DeepClone().AsObject();
        if (change == "revoked") record["status"] = "revoked";
        if (change is "conscious" or "capable") record[change] = false;
        if (change == "same_turn") record["binding"]!["turn"] = context.Turn;
        if (change == "intervening_turn") record["binding"]!["turn"] = context.Turn - 2;
        if (change == "expired") { record["acceptedCycleOrdinal"] = 41; record["eligibleCycleOrdinal"] = 42; }
        var fp = SpiritualTreatmentParticipation.Fingerprint(record);
        selected = selected with { Aid = selected.Aid! with { ParticipationFingerprint = fp } };
        data = data with { AidWitness = new JsonObject { ["record"] = record, ["contentFingerprint"] = fp,
            ["grantResultFingerprint"] = Digest('c') } };
        Assert.Equal(change == "expired" ? 42 : 43, record["eligibleCycleOrdinal"]!.GetValue<int>());
        Assert.Throws<InvalidDataException>(() => SpiritualSelfTreatmentRequestCodec.CreateActive(context, selected, data, [14]));
    }

    [Theory]
    [InlineData("direction")][InlineData("confirmation")][InlineData("wallet")][InlineData("provider_variant")]
    [InlineData("legacy_resident")][InlineData("foreign_realm")][InlineData("guardian_alias")]
    public void NewSelectionCannotBorrowProviderOrLegacyAuthority(string change)
    {
        var (_, selected, _) = Inputs("radiant_actor"); var root = selected.ToJson();
        switch (change)
        {
            case "direction": root["direction"] = "resident_to_player"; break;
            case "confirmation": root["confirmation"] = "player_target_confirmed"; break;
            case "wallet": root["currencyInvariant"] = new JsonObject(); break;
            case "provider_variant": root["mode"] = "receive_aid"; break;
            case "legacy_resident": root["schemaVersion"] = 5; break;
            case "foreign_realm": root["target"]!["realm"] = "chaos_sea"; break;
            case "guardian_alias": root["target"]!["ownerKind"] = "guardian"; break;
        }
        Assert.Throws<InvalidDataException>(() => SpiritualSelfTreatmentSelection.Parse(root));
    }

    private static (SpiritualSelfTreatmentStageContext Context, SpiritualSelfTreatmentSelection Selection,
        AfterlifeWoundTreatmentAuthority.SourceData Data) Inputs(string kind)
    {
        var (input, owner, locality) = ShiningPatientTreatmentAdmissionTests.Inputs(kind);
        var woundRoot = WoundContractTestData.CreateSpiritualActiveWound("wound_patient", "shining_abode", kind, owner.OwnerId, owner.CarrierPath);
        var wound = WoundMaterializationContract.Parse(woundRoot.ToJsonString(), "independent.patient.codec").Wound!;
        Assert.NotNull(wound);
        input.Profiles["profiles"]![0]!["activeWounds"] = new JsonArray(woundRoot);
        var empty = WoundPlayerProjectionTestData.InputInRealm("shining_abode");
        var carriers = empty.Carriers with { AfterlifeProfiles = input.Profiles };
        var catalog = WoundCarrierCatalog.Build(carriers); Assert.Empty(catalog.Issues); Assert.Single(catalog.Occurrences);
        var effects = EffectCarrierCatalog.Build(new(null, null, carriers.EnemyCombatants, carriers.AllyCombatants, input.Profiles, null));
        var proposal = new JsonObject
        { ["schemaVersion"] = 5, ["kind"] = "shining_target_consent", ["operation"] = "grant", ["healerId"] = "player_soul",
            ["woundId"] = wound.WoundId, ["locality"] = SpiritualTreatmentParticipation.LocalityJson(locality),
            ["conscious"] = true, ["capable"] = true, ["readableEvidence"] = "Я согласен на отдельную попытку лечения игроком." };
        var record = SpiritualTreatmentParticipation.CreateOutcome(new(owner, proposal), locality, new JsonObject
        { ["sessionId"] = "session_patient", ["requestId"] = "request_grant", ["snapshotToken"] = "snapshot_grant", ["realm"] = "shining_abode", ["turn"] = 43 },
            42, "transition_patient")["record"]!.AsObject();
        var selection = new SpiritualSelfTreatmentSelection(wound.WoundId, WoundIdentityState.ComputeSemanticFingerprint(wound))
            { Aid = SpiritualTreatmentParticipation.Selection(record) };
        var source = new SpiritualHealingPreparedSource(wound.WoundId, owner,
            new("shining_abode", "player_soul", "player_soul", owner.CarrierPath), wound.Severity.Rank, 3, 0, "normal", []);
        var data = new AfterlifeWoundTreatmentAuthority.SourceData(wound, source, "shining_abode", 43, Digest('b'),
            carriers, effects, empty.Identities, empty.History, catalog, [new(wound, 0)], new JsonObject
            { ["record"] = record, ["contentFingerprint"] = SpiritualTreatmentParticipation.Fingerprint(record), ["grantResultFingerprint"] = Digest('c') });
        return (new("session_patient", "request_patient", 44, new ProgressionControl { CurrentRealm = "Shining Abode", NextChaosSeaTurnOrdinal = 43 }), selection, data);
    }
    private static string Digest(char c) => "sha256:" + new string(c, 64);
}
