using System.Reflection;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Protects private spiritual decision roots and their complete snapshot membership.
/// </summary>
public sealed class SpiritualWoundTopologyTests
{
    /// <summary>
    /// Requires the spiritual service roots to be private client surfaces, excluded from player status payloads.
    /// </summary>
    /// <param name="path">
    /// Exact registered spiritual decision root whose privacy and snapshot coverage are required.
    /// </param>
    [Theory]
    [InlineData("game_state/control/spiritual_wound_capture_checkpoint.json")]
    [InlineData("game_state/control/pending_spiritual_wound_decisions.json")]
    [InlineData("game_state/wounds/spiritual_wound_opportunity_receipts.json")]
    public void RootsArePrivateClientOwnedAndSnapshotRequired(string path)
    {
        var surface = AfterlifeContractRegistry.Find(path);
        Assert.NotNull(surface);
        Assert.True(surface.IsKnownClientOwnedSurface);
        var privacy = typeof(AfterlifeContractSurface).GetProperty("IsPrivateMechanicsSurface");
        Assert.NotNull(privacy);
        Assert.True(Assert.IsType<bool>(privacy.GetValue(surface)));
        Assert.Contains(path, WoundAcceptedTurnSnapshotContract.RequiredPaths);
        Assert.Contains(path, WoundAcceptedTurnSnapshotContract.PublicationAgreementPaths);

        var definitionsField = typeof(ExplorerMode).GetField("AfterlifePendingContractDefinitions",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(definitionsField);
        var definitions = Assert.IsAssignableFrom<IEnumerable<object>>(definitionsField.GetValue(null));
        var paths = definitions.Select(value => value.GetType().GetProperty("Path")?.GetValue(value)?.ToString());
        foreach (var item in AfterlifeContractRegistry.All.Where(item =>
                     Assert.IsType<bool>(privacy.GetValue(item))))
        {
            Assert.True(item.IsKnownClientOwnedSurface);
            Assert.DoesNotContain(item.Path, paths, StringComparer.OrdinalIgnoreCase);
        }

        using var inventory = JsonDocument.Parse(File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot,
            "OtherGuides", "Afterlife_Pending_Control_Surface_Inventory.json")));
        var row = Assert.Single(inventory.RootElement.GetProperty("surfaces").EnumerateArray(),
            value => value.GetProperty("path").GetString() == path);
        Assert.True(row.GetProperty("privateMechanics").GetBoolean());
    }

    /// <summary>
    /// Keeps the private checkpoint outside generic validation repair and GM resubmission.
    /// </summary>
    [Fact]
    public void CaptureCheckpoint_IsExcludedFromGenericRepairAndResubmission()
    {
        const string path = "game_state/control/spiritual_wound_capture_checkpoint.json";
        var validationFilter = typeof(ValidationService).GetMethod(
            "IsClientOwnedSurfaceValidationPath", BindingFlags.NonPublic | BindingFlags.Static);
        var resubmissionFilter = typeof(GameEngine).GetMethod(
            "IsClientOwnedRepairResubmissionPath", BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(validationFilter);
        Assert.NotNull(resubmissionFilter);
        Assert.True(Assert.IsType<bool>(validationFilter.Invoke(null, [path])));
        Assert.True(Assert.IsType<bool>(resubmissionFilter.Invoke(null, [path])));
    }
}
