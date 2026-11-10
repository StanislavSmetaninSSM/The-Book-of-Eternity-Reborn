using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class ShiningPatientTreatmentAdmissionTests
{
    [Theory]
    [InlineData("resident")][InlineData("radiant_actor")]
    public void OneMenuMaterializationComputationFeedsTargetAndDetailWithoutRetainedAuthority(string kind)
    {
        var (roots, owner, _) = Inputs(kind);
        var timings = new OperationTimingAccumulator(); using var window = timings.StartWindow(captureEvents: true);
        using var receiver = FileSystemManager.EnterOperationTimingContext(null, timings);
        var admissions = SpiritualTreatmentParticipation.ReadShiningPatientAdmissions(roots.Shining, roots.Profiles, roots.Residents);
        var admission = Assert.Single(admissions); Assert.Equal(owner, admission.Target);
        const string name = "SpiritualTreatment.ShiningPatient.Materialization";
        Assert.Equal((1L, 1L), window.CaptureNamedCounts(name));
        var empty = WoundPlayerProjectionTestData.InputInRealm("shining_abode");
        var input = empty with { Carriers = empty.Carriers with { AfterlifeProfiles = roots.Profiles } };
        var catalog = WoundCarrierCatalog.Build(input.Carriers); Assert.Empty(catalog.Issues);
        var context = new WoundPlayerReadContext(input, catalog, new(), null, WoundPlayerProjection.Build(input, catalog))
            { ShiningPatients = admissions };
        var target = Assert.Single(WoundTargetSelectionService.Build(context, null, roots.Residents), t => t.Owner == owner);
        Assert.True(WoundPlayerProjection.ProjectKnownTarget(context, target).IsAvailable);
        Assert.Equal((1L, 1L), window.CaptureNamedCounts(name));
        // A separately constructed read context cannot borrow another operation's
        // DATA through its detached target; real confirmation reloads admission.
        Assert.False(WoundPlayerProjection.ProjectKnownTarget(context with { ShiningPatients = [] }, target).IsAvailable);
        roots.Profiles["profiles"]![0]!["displayName"] = "Изменили источник после экспорта";
        Assert.NotEqual(roots.Profiles["profiles"]![0]!["displayName"]!.GetValue<string>(), target.Name);
    }

    [Theory]
    [InlineData("resident", false)][InlineData("resident", true)]
    [InlineData("radiant_actor", false)][InlineData("radiant_actor", true)]
    public void IncarnationHandoffAndMalformedPackageNeverAdvertiseOrdinaryPatient(string kind, bool malformed)
    {
        var (roots, owner, locality) = Inputs(kind);
        _ = SpiritualTreatmentParticipation.RequireShiningPatientAdmission(roots.Shining, roots.Profiles, roots.Residents, owner, locality);
        roots.Shining["preparedIncarnationPackage"] = malformed ? JsonValue.Create("broken package") : new JsonObject
        {
            ["selectedCardIds"] = new JsonArray("card_memory"),
            ["selectedCards"] = new JsonArray(new JsonObject
            { ["cardId"] = "card_memory", ["dedupeKey"] = "memory:card_memory", ["sourceType"] = "project",
                ["sourceFactionId"] = "faction_old", ["sourceActorId"] = "project_old", ["effectFamily"] = "memory",
                ["rarity"] = "common", ["displayName"] = "Память Света", ["displaySummary"] = "Сохраняет эхо.", ["effectPayload"] = new JsonObject() }),
            ["generatedFromDraftVersion"] = 1, ["preparedAtTurn"] = 10, ["preparedAtUtc"] = "2026-04-17T00:10:00Z"
        };
        Assert.Equal(malformed ? ShiningAbodeState.PreparedIncarnationPackageMode.InvalidFault : ShiningAbodeState.PreparedIncarnationPackageMode.ValidHandoff,
            ShiningAbodeState.GetPreparedIncarnationPackageMode(roots.Shining));
        Assert.Throws<InvalidDataException>(() => SpiritualTreatmentParticipation.RequireShiningPatientAdmission(roots.Shining, roots.Profiles, roots.Residents, owner, locality));
        Assert.Empty(SpiritualTreatmentParticipation.ReadShiningPatientAdmissions(roots.Shining, roots.Profiles, roots.Residents));
    }
}
