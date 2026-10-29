using System.Text.Encodings.Web;
using System.Text.Json;
using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>Active reads and explicit healed History are separate player views.</summary>
public sealed class WoundHistoryViewTests
{
    /// <summary>A valid terminal-only snapshot must not clutter the default active view.</summary>
    [Fact]
    public void DefaultReadKeepsKnownHealedWoundOutOfActiveView()
    {
        var projection = WoundPlayerProjection.Build(WoundPlayerHistoryTestData.TerminalInput());
        Assert.True(projection.IsAvailable); Assert.Empty(projection.Active);
        Assert.Equal("Известная зажившая рана", Assert.Single(projection.History).Name);
        var result = ExplorerWoundCommandResultBuilder.Build("/раны", projection, null, treatment: false);
        Assert.Equal(CommandExecutionState.Completed, result.State); Assert.Empty(result.Prompts);
        Assert.Null(result.WoundCommandState); Assert.Null(result.PendingGmAction);
        var text = JsonSerializer.Serialize(result, new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        Assert.Contains(projection.StatusMessage, text);
        Assert.DoesNotContain("Известная зажившая рана", text);
    }
}
