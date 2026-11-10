using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class ExplorerWoundSelfTreatmentTests
{
    [Theory]
    [InlineData("public_roles")]
    [InlineData("ambiguous")]
    [InlineData("distinct_names")]
    [InlineData("stale_role")]
    public async Task ShiningPatientNavigationUsesKnownRosterRolesAndKeepsExactCurrentSelection(string scenario)
    {
        // Missing behavior: valid same-name patients lose their already-public
        // roster roles at the real target menu. This is not service eligibility.
        var reads = new CanonicalReadActivityAccumulator();
        await using var fixture = await AfterlifeHealingPaymentLifecycleTests.CreateFactionServiceFixtureAsync(3, 14,
            new FileSystemManagerHooks { PassiveCanonicalReads = reads }, engine: false, playerHealingTier: 3,
            additionalBeforeResourceBootstrap: async (files, lease, profiles) =>
            {
                await SeedShiningPatientAsync(files, lease, profiles, "resident");
                var residents = JsonNode.Parse((await files.ReadFileAsync(lease, GuardianAbodeResidentState.StatePath))!)!.AsObject();
                foreach (var id in new[] { AfterlifeHealingPaymentLifecycleTests.FactionHealerId, ShiningPatientId })
                {
                    var profile = profiles["profiles"]!.AsArray().OfType<JsonObject>().Single(p => p["actorId"]?.GetValue<string>() == id);
                    var row = residents["entries"]!.AsArray().OfType<JsonObject>().Single(r => r["residentId"]?.GetValue<string>() == id);
                    var name = scenario is "public_roles" or "ambiguous" ? "Знакомый житель" : id == ShiningPatientId ? "Архивный житель" : "Местный целитель";
                    profile.Remove("name"); profile["displayName"] = name; row["displayName"] = name;
                    if (id == ShiningPatientId && scenario == "ambiguous")
                    {
                        // Both remain genuine valid healers; no service is added
                        // to the patient and the faction never loses its healer.
                        row["residentRole"] = ShiningAbodeState.ResidentRoleHealingSupport;
                        profile["standardArts"]!["spiritual_healing"] = 3;
                    }
                }
                await files.WriteFileAtomicAsync(lease, WoundCarrierCatalog.AfterlifeProfilesPath, profiles.ToJsonString());
                await files.WriteFileAtomicAsync(lease, GuardianAbodeResidentState.StatePath, residents.ToJsonString());
            });

        var shining = JsonNode.Parse((await fixture.Files.ReadFileAsync(ShiningAbodeState.StatePath))!)!.AsObject();
        var residents = JsonNode.Parse((await fixture.Files.ReadFileAsync(GuardianAbodeResidentState.StatePath))!)!.AsObject();
        var profiles = JsonNode.Parse((await fixture.Files.ReadFileAsync(WoundCarrierCatalog.AfterlifeProfilesPath))!)!.AsObject();
        var faction = Assert.IsType<JsonObject>(Assert.Single(shining["factions"]!.AsArray()));
        var admissions = SpiritualTreatmentParticipation.ReadShiningPatientAdmissions(shining, profiles, residents);
        Assert.Equal(2, admissions.Count);
        Assert.Equal(2, admissions.Select(a => a.Target).Distinct().Count());
        Assert.All(admissions, a => Assert.False(a.IsFactionHead));
        var patient = Assert.Single(admissions, a => a.Target.OwnerId == ShiningPatientId);
        var original = Assert.Single((await fixture.ReadWoundsAsync()).Occurrences).Wound;
        Assert.Equal(patient.Target, original.Owner);
        Assert.Equal("public", original.Display.Visibility);
        Assert.Single(patient.Profile["activeWounds"]!.AsArray());
        var roster = ShiningFactionHealingSupport.ProjectVisibleRoster(faction, residents, profiles);
        Assert.Equal(2, roster.Count);
        Assert.Equal(scenario == "ambiguous" ? 1 : 2, roster.Select(r => r.Role).Distinct().Count());
        Assert.Contains(roster, r => r.Role == "целитель");
        if (scenario != "ambiguous") Assert.Contains(roster, r => r.Role == "архивная поддержка");

        var paths = new[] { ShiningAbodeState.StatePath, GuardianAbodeResidentState.StatePath };
        var before = fixture.CaptureMutationRoots(paths); var counts = paths.Select(reads.ReadCount).ToArray();
        var opened = await WoundApplicationService.OpenAsync(fixture.Files, "/лечить", treatment: true);
        var state = Assert.IsType<WoundCommandState>(opened.WoundCommandState);
        Assert.Equal("Себя", state.Targets[0].Name);
        var targets = state.Targets.Where(t => t.Owner.OwnerKind == "resident").ToArray();
        Assert.Equal(2, targets.Length);
        var selected = Assert.Single(targets, t => t.Owner == patient.Target);
        Assert.Single(selected.Wounds);
        Assert.Equal(original.WoundId, selected.Wounds[0].WoundId);
        Assert.DoesNotContain(state.Choices, c => c.Selection.Aid?.ShiningPatient is not null); // No consent is inferred.
        var ui = JsonSerializer.Serialize(opened);
        foreach (var token in new[] { ShiningPatientId, AfterlifeHealingPaymentLifecycleTests.FactionHealerId,
            original.WoundId, "sha256:", "carrierPath", "An ascended archive spirit" }) Assert.DoesNotContain(token, ui);
        for (var i = 0; i < paths.Length; i++) Assert.Equal(1L, reads.ReadCount(paths[i]) - counts[i]);
        fixture.AssertMutationRootsUnchanged(before);

        if (scenario == "ambiguous")
        {
            Assert.All(targets, t => Assert.False(t.Available));
            Assert.All(targets, t => Assert.Contains("различить", t.Reason));
            Exception? refusal;
            await using (var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync())
                refusal = await Record.ExceptionAsync(() => WoundApplicationService.SelectTargetAsync(fixture.Files, lease, state, selected.Value, "/лечить"));
            Assert.IsType<InvalidDataException>(refusal);
            fixture.AssertMutationRootsUnchanged(before);
            return;
        }

        if (scenario == "stale_role")
        {
            Assert.All(targets, t => Assert.True(t.Available));
            residents["entries"]!.AsArray().OfType<JsonObject>().Single(r => r["residentId"]?.GetValue<string>() == ShiningPatientId)
                ["residentRole"] = ShiningAbodeState.ResidentRoleForgeSupport;
            await fixture.Files.WriteFileAtomicAsync(GuardianAbodeResidentState.StatePath, residents.ToJsonString());
            var changed = fixture.CaptureMutationRoots(paths);
            var currentAdmission = Assert.Single(SpiritualTreatmentParticipation.ReadShiningPatientAdmissions(shining, profiles, residents), a => a.Target == patient.Target);
            Assert.NotEqual(patient.Fingerprint, currentAdmission.Fingerprint);
            Exception? refusal;
            await using (var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync())
                refusal = await Record.ExceptionAsync(() => WoundApplicationService.SelectTargetAsync(fixture.Files, lease, state, selected.Value, "/лечить"));
            Assert.IsType<InvalidDataException>(refusal);
            var fresh = await WoundApplicationService.OpenAsync(fixture.Files, "/лечить", treatment: true);
            var current = Assert.Single(fresh.WoundCommandState!.Targets, t => t.Owner == patient.Target);
            Assert.True(current.Available);
            Assert.NotEqual(selected.BindingFingerprint, current.BindingFingerprint);
            fixture.AssertMutationRootsUnchanged(changed);
            Assert.Contains("кузнечная поддержка", current.Context); // Causal changed public-context oracle.
            return;
        }

        Assert.All(targets, t => Assert.True(t.Available)); // Causal same-name public-role oracle.
        Assert.Equal(2, targets.Select(t => t.Label).Distinct(StringComparer.Ordinal).Count());
        if (scenario == "public_roles")
        {
            Assert.Contains(targets, t => t.Label.Contains("целитель", StringComparison.Ordinal));
            Assert.Contains("архивная поддержка", selected.Label);
        }
        ExplorerCommandResult detail;
        await using (var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync())
            detail = await WoundApplicationService.SelectTargetAsync(fixture.Files, lease, state, selected.Value, "/лечить");
        Assert.DoesNotContain(detail.Notifications, n => n.Severity == UiNotificationSeverity.Error);
        Assert.Equal(patient.Target, detail.WoundCommandState!.SelectedTarget!.Owner);
        Assert.Single(detail.WoundCommandState.SelectedTarget.Wounds);
        Assert.DoesNotContain(detail.WoundCommandState.Choices, c => c.Selection.Aid?.ShiningPatient is not null);
        for (var i = 0; i < paths.Length; i++) Assert.Equal(2L, reads.ReadCount(paths[i]) - counts[i]);
        fixture.AssertMutationRootsUnchanged(before);
    }
}
