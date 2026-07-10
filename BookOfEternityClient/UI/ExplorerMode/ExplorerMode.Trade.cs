using BookOfEternityClient.CommandProtocol;

namespace BookOfEternityClient.UI;

public partial class ExplorerMode
{
    private async Task ShowRealmTradeAsync()
    {
        var resolution = ExplorerRealmTradeCommandResolver.Resolve(
            _stateManager.CurrentState.CurrentRealm,
            _currentCommandRemainder);
        if (!resolution.Success)
        {
            MarkupLine($"[yellow]⚠️ {GameInterface.EscapeMarkup(resolution.ErrorMessage)}[/]");
            WaitForKey();
            return;
        }

        var routed = ExplorerCommandParser.Parse(resolution.Command);
        _currentCommandRemainder = routed.Arguments;

        switch (routed.Descriptor?.Id)
        {
            case "npc_trade":
                await ShowNpcTradeCommand();
                break;
            case "guardian_trade":
                await ShowGuardianTradeCommand();
                break;
            case "shining_trade":
                await ShowShiningTradeSelectionAsync();
                break;
        }
    }
}
