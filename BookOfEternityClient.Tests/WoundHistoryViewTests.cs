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

    /// <summary>Explicit read navigation is inert and offers only the opposite view.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReadViewsSeparateKnownHistoryAndOfferOnlyReadNavigation(bool history)
    {
        var projection = WoundPlayerProjection.Build(WoundPlayerHistoryTestData.TerminalInput());
        var result = ExplorerWoundCommandResultBuilder.Build("/раны", projection, null, false, history);
        var text = Text(result);
        Assert.Equal(history, text.Contains("Известная зажившая рана", StringComparison.Ordinal));
        Assert.Equal(CommandExecutionState.Completed, result.State); Assert.Empty(result.Prompts);
        Assert.Null(result.WoundCommandState); Assert.Null(result.PendingGmAction);
        var action = Assert.Single(result.Actions);
        Assert.Equal(history ? "Активные раны" : "История заживших ран", action.Label);
        var parsed = WoundApplicationService.ParseReadCommand(action.Command);
        Assert.Equal(history ? null : "history", parsed.Subcommand?.Id);
        Assert.Equal(ExplorerCommandMutationMode.ReadOnly, parsed.Descriptor!.MutationMode);
        Assert.False(action.RequiresConfirmation); Assert.Null(action.Payload);
    }

    /// <summary>Treatment cannot expose healed records or accidentally become read navigation.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TreatmentAlwaysExcludesHistoryAndReadActions(bool history)
    {
        var projection = WoundPlayerProjection.Build(WoundPlayerHistoryTestData.TerminalInput());
        var result = ExplorerWoundCommandResultBuilder.Build("/лечить", projection, null, true, history);
        Assert.DoesNotContain("Известная зажившая рана", Text(result));
        Assert.Empty(result.Actions); Assert.Empty(result.Prompts); Assert.Null(result.PendingGmAction);
    }

    /// <summary>Empty History is explicit and does not borrow active status or infer a hidden count.</summary>
    [Theory]
    [InlineData("empty")]
    [InlineData("hidden")]
    [InlineData("other_realm")]
    public void EmptyHiddenAndOtherRealmHistoryStayEmpty(string scenario)
    {
        var input = scenario == "empty" ? WoundPlayerProjectionTestData.Input()
            : WoundPlayerHistoryTestData.TerminalInput(hidden: scenario == "hidden");
        if (scenario == "other_realm") input = input with { Realm = "chaos_sea" };
        var projection = WoundPlayerProjection.Build(input); Assert.True(projection.IsAvailable);
        var result = ExplorerWoundCommandResultBuilder.Build("/раны история", projection, null, false, true);
        Assert.Contains("История заживших ран пока пуста.", Text(result));
        Assert.DoesNotContain("Известная зажившая рана", Text(result)); Assert.Single(result.Actions);
    }

    /// <summary>Unavailable source data is never described as a genuine empty History.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnavailableReadDoesNotBecomeAnEmptyOrNavigableHistory(bool history)
    {
        var result = ExplorerWoundCommandResultBuilder.Build("/раны", WoundPlayerProjection.Unavailable(), null, false, history);
        Assert.Contains(WoundPlayerProjection.UnavailableMessage, Text(result));
        Assert.DoesNotContain("История заживших ран пока пуста.", Text(result)); Assert.Empty(result.Actions);
    }

    /// <summary>Existing command aliases share one canonical read-only History route.</summary>
    [Theory]
    [InlineData("/раны", false)]
    [InlineData("/wounds", false)]
    [InlineData("/раны история", true)]
    [InlineData("/wounds history", true)]
    [InlineData("/раны history", true)]
    [InlineData("/wounds история", true)]
    public void ReadAliasesStayReadOnlyAndCanonical(string command, bool history)
    {
        var parsed = WoundApplicationService.ParseReadCommand(command);
        Assert.Equal("wounds", parsed.Descriptor!.Id);
        Assert.Equal(ExplorerCommandMutationMode.ReadOnly, parsed.Descriptor.MutationMode);
        Assert.Equal(history ? "history" : null, parsed.Subcommand?.Id);
        Assert.Equal(history ? "/раны история" : "/раны", parsed.CanonicalCommand);
        Assert.Empty(parsed.Arguments);
    }

    /// <summary>Unknown, trailing or treatment commands cannot be interpreted as a read view.</summary>
    [Theory]
    [InlineData("/раны чужая_рана")]
    [InlineData("/wounds history extra")]
    [InlineData("/раны история extra")]
    [InlineData("/лечить")]
    [InlineData("/heal")]
    public void UnsupportedReadArgumentsAndTreatmentAreRejected(string command) =>
        Assert.Throws<InvalidDataException>(() => WoundApplicationService.ParseReadCommand(command));

    private static string Text(object value) => JsonSerializer.Serialize(value,
        new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
}
