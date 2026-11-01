using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class PendingTurnSnapshotReaderTests
{
    [Theory]
    [InlineData("resource_quantity")][InlineData("quest_state")]
    public async Task CanonicalDiagnosisCapturesActualSelectedSourceAndAuthenticatedAbsence(string kind)
    {
        var context = await SpiritualDiagnosisCanonicalRequirementTests.ContextAsync("chaos_sea", kind);
        var wound = Assert.Single(context.Catalog.Occurrences).Wound;
        var admitted = Assert.IsType<SpiritualDiagnosisAdmission>(new SpiritualDiagnosisSourceData(context).Admit(wound, "diagnose_tissue").Data);
        var control = new ProgressionControl { CurrentRealm = "Chaos Sea", NextChaosSeaTurnOrdinal = 1 };
        var binding = new SpiritualSelfTreatmentStageContext("session_observed_diagnosis", "request_observed_diagnosis", 71, control);
        var staged = SpiritualDiagnosisRequestCodec.Create(binding, admitted.Selection, admitted);
        var program = SpiritualDiagnosisProgram.Build(binding, staged, admitted, [new(wound, 0)]);
        Assert.True(program.Projection[PendingTurnSnapshotPathPresenceV1.CanonicalDiagnosisHint]!.GetValue<bool>());
        await using (var lease = await _fs.AcquireCanonicalWriteLeaseAsync())
        {
            foreach (var pair in context.CurrentMenuRoots) await _fs.WriteFileAtomicAsync(lease, pair.Key, pair.Value.ToJsonString());
            await _fs.WriteFileAtomicAsync(lease, AcceptedMechanicsPlan.WoundCommandPath, staged.ToJsonString());
        }
        await new LiveTurnPreparationService(_fs).PrepareAsync(new LiveTurnPreparationOptions
        {
            SessionId = binding.SessionId, RequestId = binding.RequestId, TurnNumber = binding.Turn,
            CurrentRealm = "Chaos Sea", ProgressionControl = control, SpiritualHealing = program.Projection,
            PlayerAction = WoundApplicationService.DiagnosisActionText
        });
        var manifest = ReadRoot(LiveTurnPreparationService.PendingTurnSnapshotManifestPath);
        var presence = manifest["originalPathPresenceV1"]!.AsObject();
        var paths = AfterlifeWoundTreatmentAuthority.DiagnosisSourcePaths("chaos_sea", [kind]);
        Assert.True(PendingTurnSnapshotPathPresenceV1.IsCanonicalDiagnosisForm(presence.Select(pair => pair.Key).ToHashSet(StringComparer.Ordinal)));
        if (kind == "resource_quantity") Assert.False(presence["game_state/inventory/item_bonds.json"]!.GetValue<bool>());
        else Assert.True(presence[SpiritualDiagnosisCanonicalSources.QuestsPath]!.GetValue<bool>());
        await using var held = await _fs.AcquireCanonicalWriteLeaseAsync();
        var observed = PendingTurnSnapshotReader.ReadObservedCurrent(_fs, held,
            PendingTurnSnapshotPathSelection.CreateWithObservedOptionalPaths([AcceptedMechanicsPlan.WoundCommandPath], paths));
        Assert.True(observed.Success, Describe(observed.Issues));
        observed.Evidence!.RequireCurrent(_fs, held, observed.Snapshot!);
        if (kind == "resource_quantity") Assert.Contains("game_state/inventory/item_bonds.json", observed.Snapshot!.AbsentLogicalPaths);
        else Assert.Equal(context.CurrentMenuRoots[SpiritualDiagnosisCanonicalSources.QuestsPath].ToJsonString(),
            JsonNode.Parse(observed.Snapshot!.ReadRequiredBytes(SpiritualDiagnosisCanonicalSources.QuestsPath))!.ToJsonString());
        var unknown = paths.Append("game_state/unregistered_diagnosis_permission.json").ToArray();
        Assert.False(PendingTurnSnapshotReader.ReadObservedCurrent(_fs, held,
            PendingTurnSnapshotPathSelection.CreateWithObservedOptionalPaths([AcceptedMechanicsPlan.WoundCommandPath], unknown)).Success);
        Assert.Throws<InvalidDataException>(() => PendingTurnSnapshotPathPresenceV1.CreateForObservation(
            new Dictionary<string, string>(), new Dictionary<string, string>(), presence.Select(pair => pair.Key).Where(path => path != "game_state/meta/soul_state.json").ToArray()));
    }

    [Fact]
    public async Task OldThreeKindProjectionKeepsOriginalFormWithoutAnyNewSourceRead()
    {
        var context = SpiritualDiagnosisAdmissionTests.Context(); var wound = Assert.Single(context.Catalog.Occurrences).Wound;
        var admitted = Assert.IsType<SpiritualDiagnosisAdmission>(new SpiritualDiagnosisSourceData(context).Admit(wound, "diagnose_tissue").Data);
        var control = new ProgressionControl { CurrentRealm = "Chaos Sea", NextChaosSeaTurnOrdinal = 1 };
        var binding = new SpiritualSelfTreatmentStageContext("session_observation_old", "request_observation_old", 71, control);
        var staged = SpiritualDiagnosisRequestCodec.Create(binding, admitted.Selection, admitted);
        var projection = SpiritualDiagnosisProgram.Build(binding, staged, admitted, [new(wound, 0)]).Projection;
        Assert.Null(projection[PendingTurnSnapshotPathPresenceV1.CanonicalDiagnosisHint]);
        var request = new TurnRequest { SessionId = binding.SessionId, RequestId = binding.RequestId, TurnNumber = binding.Turn,
            ProgressionControl = control, SpiritualHealing = projection };
        // No filesystem/lease exists: the old-form pure fast path must not attempt any source access.
        Assert.Same(PendingTurnSnapshotPathPresenceV1.LogicalPaths,
            await PendingTurnSnapshotPathPresenceV1.SelectObservationAsync(null!, null!, request));
        request.RequestId = "foreign_request";
        Assert.Throws<InvalidDataException>(() => PendingTurnSnapshotPathPresenceV1.SelectDiagnosisObservation(request, staged, context.Input.Carriers.AfterlifeProfiles!));
    }
}
