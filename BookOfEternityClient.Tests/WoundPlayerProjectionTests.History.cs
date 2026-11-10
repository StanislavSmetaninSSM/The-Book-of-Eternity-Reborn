using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class WoundPlayerProjectionTests
{
    [Fact]
    public void Build_CompleteReconstructedTerminalSnapshotAppearsOnlyInHistory()
    {
        var input = WoundPlayerHistoryTestData.TerminalInput();
        var result = WoundPlayerProjection.Build(input);
        Assert.True(result.IsAvailable);
        Assert.Empty(result.Active);
        var healed = Assert.Single(result.History);
        Assert.Equal("Известная зажившая рана", healed.Name);
        Assert.Equal("I", healed.Severity);
        Assert.Equal("Рана зажила", healed.Summary);
        Assert.DoesNotContain("retained for replay", healed.Summary);
    }

    [Fact]
    public void Build_HiddenTerminalSnapshotAndOtherRealmNeverLeakHistory()
    {
        var hidden = WoundPlayerProjection.Build(WoundPlayerHistoryTestData.TerminalInput(hidden: true));
        Assert.True(hidden.IsAvailable);
        Assert.Empty(hidden.History);
        var other = WoundPlayerProjection.Build(WoundPlayerHistoryTestData.TerminalInput() with { Realm = "chaos_sea" });
        Assert.True(other.IsAvailable);
        Assert.Empty(other.History);
    }

    [Fact]
    public void Build_TechnicalHistorySummaryWithoutTypedVisibleSnapshotsNeverSuppliesAName()
    {
        var input = WoundPlayerHistoryTestData.TerminalInput();
        var raw = JsonNode.Parse(WoundHistoryState.SerializeCanonical(input.History))!.AsObject();
        raw["transitions"]![1]!.AsObject().Remove("transitionResult");
        var parsed = WoundHistoryState.Parse(raw.ToJsonString(), "test.history");
        Assert.True(parsed.IsValid, string.Join(";", parsed.Issues));
        Assert.Empty(parsed.State!.ValidateAgreement(input.Identities, WoundCarrierCatalog.Build(input.Carriers)));
        var result = WoundPlayerProjection.Build(input with { History = parsed.State! });
        Assert.True(result.IsAvailable);
        Assert.Empty(result.History);
    }
}
