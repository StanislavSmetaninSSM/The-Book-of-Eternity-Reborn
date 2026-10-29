using System.Text.Encodings.Web;
using System.Text.Json;
using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>Actual shared read handlers expose only the selected target's currently known detail.</summary>
public sealed partial class WoundForeignDetailTests(ITestOutputHelper output)
{
    /// <summary>A valid reachable known foreign wound must expose its declared visible symptom without a treatment grant.</summary>
    [Fact]
    public async Task SelectedKnownGuardianWoundShowsVisibleSymptomWithoutGrantOrMutation()
    {
        // Break caught: replacing selected NPC detail with the former Name/Severity/Description-only summary.
        var data = WoundForeignDetailTestData.Create(); var reads = new CanonicalReadActivityAccumulator();
        await using var fixture = await WoundPlayerReadFixture.CreateAsync(output, realm: "chaos_sea", suppliedInput: data.Input, reads: reads);
        await using (var prep = await fixture.Files.AcquireCanonicalWriteLeaseAsync())
        {
            await fixture.Files.WriteFileAtomicAsync(prep, AfterlifeWoundTreatmentAuthority.AidLocalityPaths[0], data.Guardians.ToJsonString());
            await fixture.Files.WriteFileAtomicAsync(prep, GuardianAbodeResidentState.StatePath, data.Residents.ToJsonString());
            _ = fixture.Files.GetOrCreateSessionGeneration(prep);
        }
        var before = fixture.Snapshot(); var mutations = fixture.MutationCalls;
        var paths = new[] { AfterlifeWoundTreatmentAuthority.AidLocalityPaths[0], GuardianAbodeResidentState.StatePath };
        var beforeReads = paths.Select(reads.ReadCount).ToArray();
        var opened = await WoundApplicationService.OpenAsync(fixture.Files, "/лечить", treatment: true);
        var prompt = Assert.IsType<UiSelectionPrompt>(Assert.Single(opened.Prompts)); Assert.Equal("wound_target", prompt.Id);
        var target = Assert.Single(prompt.Options, o => o.Label == "Хранитель подробностей" && !o.Disabled);
        ExplorerCommandResult card;
        await using (var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync())
            card = await WoundApplicationService.SelectTargetAsync(fixture.Files, lease, opened.WoundCommandState!, target.Value, "/лечить");
        Assert.Equal(CommandExecutionState.Completed, card.State); Assert.Empty(card.Prompts); Assert.Empty(card.Actions);
        var text = JsonSerializer.Serialize(card, new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        Assert.Contains("Известная рана обитателя — II", text);
        Assert.Contains("Известное повреждение духовной ткани.", text);
        Assert.Equal(before, fixture.Snapshot()); Assert.Equal(mutations, fixture.MutationCalls);
        for (var i = 0; i < paths.Length; i++)
            Assert.Equal(2L, reads.ReadCount(paths[i]) - beforeReads[i]); // One source event for each existing Open/Select invocation.
        // Valid target/name/inertness/source assertions precede the expected semantic RED.
        Assert.Contains(WoundForeignDetailTestData.VisibleSymptom, text);
    }
}
