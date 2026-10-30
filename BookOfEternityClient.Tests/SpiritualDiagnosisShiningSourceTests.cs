using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>Actual file-light read/confirm/stage boundaries, not detached permission or accepted-turn authority.</summary>
public sealed class SpiritualDiagnosisShiningSourceTests
{
    [Theory]
    [InlineData("missing")]
    [InlineData("unreadable")]
    public async Task FreshMenuAndConfirmationRefuseAnUnprovedActualShiningSource(string fault)
    {
        await using var fixture = await SpiritualSelfTreatmentRequestTests.Fixture.CreateAsync(tier: 0, realm: "shining_abode");
        await using (var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync())
            await InstallReadSourcesAsync(fixture.Files, lease);
        var baseline = fixture.CanonicalBytes();
        var opened = await WoundApplicationService.OpenAsync(fixture.Files, "/лечить", treatment: true);
        var menu = Assert.IsType<WoundCommandState>(opened.WoundCommandState);
        menu = menu.ForTarget(Assert.Single(menu.Targets, target => target.Owner.OwnerKind == "player_soul"));
        var choice = Assert.Single(menu.Choices, value => value.Available && value.Selection.Diagnosis is not null);
        await using (var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync())
            Assert.Equal(choice.Selection, await WoundApplicationService.ConfirmAsync(fixture.Files, lease, menu, choice.Value));
        Assert.Equal(baseline, fixture.CanonicalBytes());
        Assert.Null(await fixture.Files.ReadFileBytesAsync(AcceptedMechanicsPlan.WoundCommandPath));

        await using (var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync())
            await BreakShiningSourceAsync(fixture.Files, lease, fault);
        var changed = fixture.CanonicalBytes();
        var fresh = Assert.IsType<WoundCommandState>((await WoundApplicationService.OpenAsync(fixture.Files,
            "/лечить", treatment: true)).WoundCommandState);
        Exception? confirmation;
        await using (var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync())
            confirmation = await Record.ExceptionAsync(async () =>
            {
                _ = await WoundApplicationService.ConfirmAsync(fixture.Files, lease, menu, choice.Value);
            });
        Assert.Equal(changed, fixture.CanonicalBytes());
        Assert.Null(await fixture.Files.ReadFileBytesAsync(AcceptedMechanicsPlan.WoundCommandPath));
        var available = fresh.Choices.Any(value => value.Available && value.Selection.Diagnosis is not null);
        Assert.True(!available && confirmation is InvalidDataException,
            $"An unproved actual Shining source must disable fresh diagnosis and refuse stale confirmation; available={available}, confirmation={confirmation?.GetType().Name ?? "none"}.");
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("unreadable")]
    public async Task ActualPlayerStageRefusesAnUnprovedShiningSourceBeforeScheduleOrRequestWrites(string fault)
    {
        await using var fixture = await SpiritualSelfTreatmentRequestTests.Fixture.CreateAsync(tier: 0, realm: "shining_abode");
        SpiritualSelfTreatmentSelection selection;
        await using (var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync())
            selection = await InstallReadSourcesAsync(fixture.Files, lease);
        var baseline = Assert.IsType<WoundCommandState>((await WoundApplicationService.OpenAsync(fixture.Files,
            "/лечить", treatment: true)).WoundCommandState);
        Assert.Contains(baseline.Choices, choice => choice.Available && choice.Selection == selection);

        await using var owned = await fixture.Files.AcquireCanonicalWriteLeaseAsync();
        await BreakShiningSourceAsync(fixture.Files, owned, fault);
        var before = fixture.CanonicalBytes();
        var scheduler = new ProgressionScheduleService(fixture.Files, NullLogger<ProgressionScheduleService>.Instance);
        var staged = await AfterlifeWoundTreatmentAuthority.StagePlayerDiagnosisTurnAsync(fixture.Files, owned, scheduler,
            fixture.Context.SessionId, fixture.Context.RequestId, fixture.Context.Turn, selection);
        var request = await fixture.Files.ReadFileBytesAsync(owned, AcceptedMechanicsPlan.WoundCommandPath);
        var unchanged = before.SequenceEqual(fixture.CanonicalBytes(), StringComparer.Ordinal);
        Assert.True(!staged.Stage.IsStaged && staged.Control is null && request is null && unchanged,
            $"An unproved actual Shining source must refuse before schedule/request/canonical writes; staged={staged.Stage.IsStaged}, control={staged.Control is not null}, request={request is not null}, unchanged={unchanged}.");
    }

    private static async Task<SpiritualSelfTreatmentSelection> InstallReadSourcesAsync(FileSystemManager files,
        FileSystemManager.CanonicalWriteLease lease)
    {
        var selection = await SpiritualDiagnosisStagingTests.InstallDiagnosisAsync(files, lease);
        var profiles = JsonNode.Parse((await files.ReadFileAsync(lease, WoundCarrierCatalog.AfterlifeProfilesPath))!)!.AsObject();
        var wound = Assert.IsType<JsonObject>(Assert.Single(profiles["profiles"]![0]!["activeWounds"]!.AsArray()));
        var input = WoundPlayerProjectionTestData.InputInRealm("shining_abode", wound);
        foreach (var (path, root) in new[]
        {
            (WoundCarrierCatalog.PlayerPath, input.Carriers.PlayerWounds!),
            (WoundCarrierCatalog.NpcPath, input.Carriers.NpcWounds!),
            (WoundCarrierCatalog.EnemiesPath, input.Carriers.EnemyCombatants!),
            (WoundCarrierCatalog.AlliesPath, input.Carriers.AllyCombatants!)
        }) await files.WriteFileAtomicAsync(lease, path, root.ToJsonString());
        // The read owner requires its real definition catalog. This is initial
        // display DATA only; no resource state, reservation or accepted receipt.
        await files.WriteFileAtomicAsync(lease, ResourceMaterializationContract.DefinitionsPath,
            ResourceDefinitionCatalog.CreateBuiltIn().ToCanonicalJson());
        var shining = JsonNode.Parse((await files.ReadFileAsync(lease, ShiningAbodeState.StatePath))!)!.AsObject();
        Assert.Equal("hall_diagnosis_source", shining["currentHallId"]!.GetValue<string>());
        Assert.Equal(ShiningAbodeState.PreparedIncarnationPackageMode.Absent, ShiningAbodeState.GetPreparedIncarnationPackageMode(shining));
        return selection;
    }

    private static Task BreakShiningSourceAsync(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease, string fault)
    {
        if (fault == "missing")
        {
            files.DeleteFile(lease, ShiningAbodeState.StatePath);
            return Task.CompletedTask;
        }
        return files.WriteFileAtomicAsync(lease, ShiningAbodeState.StatePath, "{");
    }
}
